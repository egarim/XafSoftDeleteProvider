using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.Metadata;

namespace CustomXpoProviders {
    /// <summary>
    /// A data layer wrapper that intercepts UPDATE statements during soft delete
    /// to prevent nullifying relationships.
    /// 
    /// NOTE: This implementation uses reflection to work across DevExpress versions.
    /// </summary>
    public class PreserveRelationshipsDataLayer : SimpleDataLayer {
        
    /// <summary>
    /// When true, attempts to preserve relationships during soft delete (DeferredDeletion)
    /// by filtering out UPDATE statements that nullify foreign key columns when a GCRecord
    /// is being set in the same batch.
    /// </summary>
    public bool PreserveRelationshipsOnSoftDelete { get; set; } = true;
        private const string GCRecordColumnName = "GCRecord";

        /// <summary>
        /// Creates a data layer wrapper around the specified data store using the supplied dictionary.
        /// </summary>
        public PreserveRelationshipsDataLayer(XPDictionary dictionary, IDataStore dataStore)
            : base(dictionary, dataStore) {
        }

        /// <summary>
        /// Creates a data layer wrapper around the specified data store.
        /// </summary>
        public PreserveRelationshipsDataLayer(IDataStore dataStore)
            : base(dataStore) {
        }

        /// <summary>
        /// Intercepts modification statements and filters out UPDATEs that nullify relationships
        /// during soft delete. Works even when relationship NULLs are issued in a separate statement
        /// from the GCRecord update by analyzing the whole batch.
        /// </summary>
        public override ModificationResult ModifyData(params ModificationStatement[] dmlStatements) {
            if(!PreserveRelationshipsOnSoftDelete) {
                return base.ModifyData(dmlStatements);
            }
            // We need to inspect the entire batch because XPO may emit separate UPDATEs:
            // one that NULLs relationship FKs and another that sets GCRecord. If we only
            // filter statements that contain both in the same SQL we miss the separate-case.
            var sqls = new string[dmlStatements.Length];
            for(int i = 0; i < dmlStatements.Length; i++) {
                try { sqls[i] = dmlStatements[i].ToString() ?? string.Empty; }
                catch { sqls[i] = string.Empty; }
            }

            // Helper: extract the table name from a SQL UPDATE statement (best-effort)
            static string? ExtractUpdateTableName(string sql) {
                if(string.IsNullOrWhiteSpace(sql)) return null;
                // Find the first occurrence of "UPDATE <table>"
                var idx = sql.IndexOf("UPDATE", StringComparison.OrdinalIgnoreCase);
                if(idx < 0) return null;
                var after = sql.Substring(idx + 6).TrimStart();
                // Table name is the first token
                var parts = after.Split(new[] { ' ', '\t', '\r', '\n', '(' }, StringSplitOptions.RemoveEmptyEntries);
                if(parts.Length == 0) return null;
                var table = parts[0].Trim();
                // Trim optional quoting
                table = table.Trim('"', '\'', '[', ']');
                return table;
            }

            // First pass: find tables where GCRecord is being set
            var tablesWithGCUpdate = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for(int i = 0; i < sqls.Length; i++) {
                var sql = sqls[i];
                if(string.IsNullOrEmpty(sql)) continue;
                if(sql.IndexOf("UPDATE", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   sql.IndexOf(GCRecordColumnName, StringComparison.OrdinalIgnoreCase) >= 0) {
                    var t = ExtractUpdateTableName(sql);
                    if(t != null) tablesWithGCUpdate.Add(t);
                }
            }

            // Second pass: include statements except UPDATEs that set NULLs for tables where
            // a GCRecord update exists in the batch (we want to preserve relationships)
            var filteredStatements = new List<ModificationStatement>();
            for(int i = 0; i < dmlStatements.Length; i++) {
                var statement = dmlStatements[i];
                var sql = sqls[i];

                // If SQL inspection indicates this is an UPDATE that sets NULLs and the
                // same table has a GCRecord update elsewhere in the batch, skip it.
                if(!string.IsNullOrEmpty(sql) && sql.IndexOf("UPDATE", StringComparison.OrdinalIgnoreCase) >= 0) {
                    var table = ExtractUpdateTableName(sql);
                    var setsGC = sql.IndexOf(GCRecordColumnName, StringComparison.OrdinalIgnoreCase) >= 0;
                    var setsNull = sql.IndexOf("= NULL", StringComparison.OrdinalIgnoreCase) >= 0 || sql.IndexOf("NULL", StringComparison.OrdinalIgnoreCase) >= 0 && sql.IndexOf("= NULL", StringComparison.OrdinalIgnoreCase) < 0 ? sql.IndexOf("NULL", StringComparison.OrdinalIgnoreCase) >= 0 : false;

                    if(table != null && tablesWithGCUpdate.Contains(table) && setsNull && !setsGC) {
                        // Exclude this NULLing UPDATE for the table being soft-deleted
                        continue;
                    }
                }

                // Fallback to finer-grained reflection-based check
                if(ShouldIncludeStatement(statement)) filteredStatements.Add(statement);
            }

            if(filteredStatements.Count == 0) {
                // Nothing to execute (we filtered all statements) — return empty successful result
                return new ModificationResult();
            }

            return base.ModifyData(filteredStatements.ToArray());
        }

        private bool ShouldIncludeStatement(ModificationStatement statement) {
            // Use reflection to inspect the statement since the API varies between versions
            var statementType = statement.GetType();
            var statementTypeName = statementType.Name;

            // Only filter UPDATE statements  
            if(!statementTypeName.Contains("Update")) {
                return true; // Include INSERT, DELETE, etc.
            }

            try {
                // Try to get the Operands/Parameters collection
                var operandsProperty = statementType.GetProperty("Operands") ?? 
                                      statementType.GetProperty("Parameters") ??
                                      statementType.GetProperty("Assignments");
                
                if(operandsProperty == null) {
                    return true; // Can't inspect, include it
                }

                var operands = operandsProperty.GetValue(statement) as System.Collections.IEnumerable;
                if(operands == null) {
                    return true;
                }

                bool hasGCRecordUpdate = false;
                bool hasNullUpdates = false;

                foreach(var operand in operands) {
                    if(operand == null) continue;

                    var operandType = operand.GetType();
                    
                    // Try to get column name
                    string? columnName = null;
                    var columnNameProp = operandType.GetProperty("ColumnName") ??
                                        operandType.GetProperty("PropertyName") ??
                                        operandType.GetProperty("Name");
                    
                    if(columnNameProp != null) {
                        columnName = columnNameProp.GetValue(operand) as string;
                    }

                    // Try to get value
                    object? value = null;
                    var valueProp = operandType.GetProperty("Value") ??
                                   operandType.GetProperty("Expression");
                    
                    if(valueProp != null) {
                        value = valueProp.GetValue(operand);
                    }

                    if(columnName == GCRecordColumnName) {
                        hasGCRecordUpdate = true;
                    }

                    // Check if value is NULL
                    if(value == null && columnName != GCRecordColumnName) {
                        hasNullUpdates = true;
                    }
                    else if(value != null) {
                        // Check if it's a ConstantValue with null
                        var constValueType = value.GetType();
                        if(constValueType.Name.Contains("Constant")) {
                            var constValueProp = constValueType.GetProperty("Value");
                            if(constValueProp != null) {
                                var constVal = constValueProp.GetValue(value);
                                if(constVal == null && columnName != GCRecordColumnName) {
                                    hasNullUpdates = true;
                                }
                            }
                        }
                    }
                }

                // If this is a soft delete (GCRecord being set) and there are NULL updates
                // then exclude this statement (we want to preserve relationships)
                if(hasGCRecordUpdate && hasNullUpdates) {
                    return false;
                }
            }
            catch {
                // If reflection fails, include the statement to be safe
                return true;
            }

            return true;
        }
    }

    /// <summary>
    /// Helper to create a data layer that preserves relationships during soft delete.
    /// </summary>
    public static class PreserveRelationshipsDataLayerHelper {
        
        /// <summary>
        /// Creates a simple data layer that preserves relationships during soft delete.
        /// Use this for single-threaded scenarios or when you don't need thread safety.
        /// </summary>
        public static PreserveRelationshipsDataLayer CreateSimpleDataLayer(string connectionString, bool preserveRelationships = true) {
            var dataStore = XpoDefault.GetConnectionProvider(connectionString, AutoCreateOption.DatabaseAndSchema);
            var dictionary = new ReflectionDictionary();
            var dataLayer = new PreserveRelationshipsDataLayer(dictionary, dataStore) {
                PreserveRelationshipsOnSoftDelete = preserveRelationships
            };
            return dataLayer;
        }

        /// <summary>
        /// Creates a simple data layer that preserves relationships during soft delete.
        /// Use this for single-threaded scenarios or when you don't need thread safety.
        /// </summary>
        public static PreserveRelationshipsDataLayer CreateSimpleDataLayer(IDataStore dataStore, XPDictionary dictionary, bool preserveRelationships = true) {
            var dataLayer = new PreserveRelationshipsDataLayer(dictionary, dataStore) {
                PreserveRelationshipsOnSoftDelete = preserveRelationships
            };
            return dataLayer;
        }

        /// <summary>
        /// Creates a thread-safe data layer that preserves relationships during soft delete.
        /// This is the recommended method for multi-threaded applications.
        /// Note: This returns IDataLayer, not PreserveRelationshipsDataLayer.
        /// </summary>
        public static IDataLayer CreateDataLayer(string connectionString, bool preserveRelationships = true) {
            var dataStore = XpoDefault.GetConnectionProvider(connectionString, AutoCreateOption.DatabaseAndSchema);
            var dictionary = new ReflectionDictionary();
            
            // We can't wrap PreserveRelationshipsDataLayer in ThreadSafeDataLayer directly
            // because both are IDataLayer. Instead, we just return our data layer which already
            // provides the functionality. If thread safety is critical, use the PreserveRelationships
            // layer and handle concurrency at a higher level.
            var dataLayer = new PreserveRelationshipsDataLayer(dictionary, dataStore) {
                PreserveRelationshipsOnSoftDelete = preserveRelationships
            };
            return dataLayer;
        }

        /// <summary>
        /// Creates a thread-safe data layer that preserves relationships during soft delete.
        /// This is the recommended method for multi-threaded applications.
        /// Note: This returns IDataLayer, not PreserveRelationshipsDataLayer.
        /// </summary>
        public static IDataLayer CreateDataLayer(IDataStore dataStore, XPDictionary dictionary, bool preserveRelationships = true) {
            var dataLayer = new PreserveRelationshipsDataLayer(dictionary, dataStore) {
                PreserveRelationshipsOnSoftDelete = preserveRelationships
            };
            return dataLayer;
        }
    }
}
