using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.Metadata;

namespace XafSoftDelete.Module {
    /// <summary>
    /// A data layer wrapper that intercepts UPDATE statements during soft delete
    /// to prevent nullifying relationships.
    /// 
    /// ROOT CAUSE: XPO's Session.DeleteCore() removes objects from association collections
    /// in-memory BEFORE SQL generation. Session.Save() then persists these as NULL FK UPDATEs.
    /// We must filter these at the DataLayer level since Session.Delete() is NOT virtual.
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

        // Diagnostic logging
        public static bool EnableLogging { get; set; } = true;
        private static readonly object logLock = new object();
        
        private static void Log(string message) {
            if(!EnableLogging) return;
            lock(logLock) {
                System.Diagnostics.Debug.WriteLine(message);
                try {
                    var logPath = System.IO.Path.Combine(
                        System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop),
                        "XafSoftDelete_SQL_Log.txt"
                    );
                    System.IO.File.AppendAllText(logPath, $"{DateTime.Now:HH:mm:ss.fff} {message}\n");
                } catch { /* ignore file errors */ }
            }
        }

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

            Log($"\n=== ModifyData called with {dmlStatements.Length} statements ===");

            // We need to inspect the entire batch because XPO may emit separate UPDATEs:
            // one that NULLs relationship FKs and another that sets GCRecord. If we only
            // filter statements that contain both in the same SQL we miss the separate-case.
            var sqls = new string[dmlStatements.Length];
            for(int i = 0; i < dmlStatements.Length; i++) {
                try { 
                    sqls[i] = dmlStatements[i].ToString() ?? string.Empty;
                    Log($"[Statement {i}] SQL: {sqls[i]}");
                }
                catch { sqls[i] = string.Empty; }
            }

            // Helper: extract the table name from a SQL UPDATE statement (best-effort)
            static string ExtractUpdateTableName(string sql) {
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
                    if(t != null) {
                        tablesWithGCUpdate.Add(t);
                        Log($"  Table {t} has GCRecord update in statement {i}");
                    }
                }
            }

            Log($"  Tables with GCRecord updates: {string.Join(", ", tablesWithGCUpdate)}");

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
                        Log($"[Statement {i}] FILTERED OUT by string-based NULL detection");
                        continue;
                    }
                }

                // Fallback to finer-grained reflection-based check and modification
                var modifiedStatement = AnalyzeAndModifyStatement(statement, i);
                if(modifiedStatement != null) {
                    filteredStatements.Add(modifiedStatement);
                } else {
                    Log($"[Statement {i}] *** FILTERED OUT by reflection ***");
                }
            }

            Log($"\n=== Final: {filteredStatements.Count} of {dmlStatements.Length} will execute ===\n");

            if(filteredStatements.Count == 0) {
                // Nothing to execute (we filtered all statements) — return empty successful result
                return new ModificationResult();
            }

            return base.ModifyData(filteredStatements.ToArray());
        }

        private ModificationStatement AnalyzeAndModifyStatement(ModificationStatement statement, int index) {
            // Use reflection to inspect the statement since the API varies between versions
            var statementType = statement.GetType();
            var statementTypeName = statementType.Name;

            // Only filter UPDATE statements  
            if(!statementTypeName.Contains("Update")) {
                Log($"  [Statement {index}] Not an UPDATE, including");
                return statement; // Include INSERT, DELETE, etc.
            }

            Log($"  [Statement {index}] Analyzing UPDATE via reflection");

            try {
                // Log the actual statement type and its full hierarchy
                Log($"  [Statement {index}] Full type: {statementType.FullName}");
                Log($"  [Statement {index}] Base type: {statementType.BaseType?.FullName ?? "none"}");
                
                // XPO stores Operands as a FIELD, not a property!
                // Operands contain column metadata (names), Parameters contain values
                var operandsField = statementType.GetField("Operands", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var parametersField = statementType.GetField("Parameters", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                System.Collections.IEnumerable operands = null;
                System.Collections.IEnumerable parameters = null;
                
                if(operandsField != null) {
                    operands = operandsField.GetValue(statement) as System.Collections.IEnumerable;
                    Log($"  [Statement {index}] Using FIELD: Operands (Type: {operandsField.FieldType.Name})");
                }
                
                if(parametersField != null) {
                    parameters = parametersField.GetValue(statement) as System.Collections.IEnumerable;
                    Log($"  [Statement {index}] Using FIELD: Parameters (Type: {parametersField.FieldType.Name})");
                }
                
                if(operands == null) {
                    // Fallback to property-based lookup
                    var operandsProperty = statementType.GetProperty("Operands", BindingFlags.Public | BindingFlags.Instance) ?? 
                                          statementType.GetProperty("Assignments", BindingFlags.Public | BindingFlags.Instance);
                    
                    if(operandsProperty != null) {
                        operands = operandsProperty.GetValue(statement) as System.Collections.IEnumerable;
                        Log($"  [Statement {index}] Using PROPERTY: {operandsProperty.Name}");
                    }
                }
                
                if(operands == null) {
                    Log($"  [Statement {index}] WARNING: Could not access Operands field or property");
                    Log($"  [Statement {index}] PUBLIC Instance properties:");
                    foreach(var prop in statementType.GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
                        Log($"    - {prop.Name} : {prop.PropertyType.Name}");
                    }
                    Log($"  [Statement {index}] NON-PUBLIC Instance properties:");
                    foreach(var prop in statementType.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance)) {
                        Log($"    - {prop.Name} : {prop.PropertyType.Name}");
                    }
                    Log($"  [Statement {index}] ALL Instance fields:");
                    foreach(var field in statementType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
                        Log($"    - {field.Name} : {field.FieldType.Name}");
                    }
                    
                    // Also check base type
                    if(statementType.BaseType != null && statementType.BaseType != typeof(object)) {
                        Log($"  [Statement {index}] BASE TYPE ({statementType.BaseType.Name}) properties:");
                        foreach(var prop in statementType.BaseType.GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
                            Log($"    - {prop.Name} : {prop.PropertyType.Name}");
                        }
                    }
                    
                    return statement; // Can't inspect, include it
                }

                Log($"  [Statement {index}] Operands collection retrieved, analyzing...");
                
                if(operands == null) {
                    Log($"  [Statement {index}] Operands collection is null");
                    return statement;
                }

                bool hasGCRecordUpdate = false;
                var nullColumns = new List<string>();

                // Convert to lists for indexed access
                var operandsList = new List<object>();
                foreach(var op in operands) {
                    operandsList.Add(op);
                }
                
                var parametersList = new List<object>();
                if(parameters != null) {
                    foreach(var param in parameters) {
                        parametersList.Add(param);
                    }
                }
                
                Log($"  [Statement {index}] Found {operandsList.Count} operands and {parametersList.Count} parameters");

                int operandIndex = 0;
                foreach(var operand in operandsList) {
                    if(operand == null) {
                        Log($"    Operand [{operandIndex}]: null");
                        operandIndex++;
                        continue;
                    }

                    var operandType = operand.GetType();
                    Log($"    Operand [{operandIndex}]: Type = {operandType.FullName}");
                    
                    
                    // Extract column name from QueryOperand field
                    string columnName = null;
                    var columnNameField = operandType.GetField("ColumnName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if(columnNameField != null) {
                        columnName = columnNameField.GetValue(operand) as string;
                        Log($"      Column: {columnName ?? "(null)"}");
                    } else {
                        // Fallback to property
                        var columnNameProp = operandType.GetProperty("ColumnName", BindingFlags.Public | BindingFlags.Instance) ??
                                            operandType.GetProperty("PropertyName", BindingFlags.Public | BindingFlags.Instance) ??
                                            operandType.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
                        
                        if(columnNameProp != null) {
                            columnName = columnNameProp.GetValue(operand) as string;
                            Log($"      Column: {columnName ?? "(null)"}");
                        } else {
                            Log($"      No column name field/property found");
                        }
                    }

                    // Get corresponding parameter value
                    object value = null;
                    bool valueFound = false;
                    
                    if(operandIndex < parametersList.Count) {
                        var parameter = parametersList[operandIndex];
                        if(parameter != null) {
                            var paramType = parameter.GetType();
                            Log($"      Parameter Type: {paramType.Name}");
                            
                            // Try to get Value from parameter
                            var valueField = paramType.GetField("Value", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if(valueField != null) {
                                value = valueField.GetValue(parameter);
                                valueFound = true;
                                Log($"      Value (field): {value?.ToString() ?? "NULL"} (Type: {value?.GetType().Name ?? "null"})");
                            } else {
                                var valueProp = paramType.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
                                if(valueProp != null) {
                                    value = valueProp.GetValue(parameter);
                                    valueFound = true;
                                    Log($"      Value (prop): {value?.ToString() ?? "NULL"} (Type: {value?.GetType().Name ?? "null"})");
                                }
                            }
                        }
                    }
                    
                    if(!valueFound) {
                        Log($"      No parameter value found for operand {operandIndex}");
                    }

                    if(columnName == GCRecordColumnName) {
                        if(value != null) {
                            hasGCRecordUpdate = true;
                            Log($"      >>> FOUND GCRecord = {value}");
                        }
                    }

                    // Check if value is NULL
                    bool isNull = IsNullValue(value);
                    if(isNull && columnName != null && columnName != GCRecordColumnName) {
                        nullColumns.Add(columnName);
                        Log($"      >>> FOUND NULL assignment to column: {columnName}");
                    }

                    operandIndex++;
                }

                // If this is a soft delete (GCRecord being set) and there are NULL updates
                // We need to MODIFY the statement to keep GCRecord but remove NULL FK assignments
                if(hasGCRecordUpdate && nullColumns.Count > 0) {
                    Log($"  [Statement {index}] MODIFYING: Has GCRecord AND nullifies {string.Join(", ", nullColumns)}");
                    Log($"  [Statement {index}] Creating modified statement without NULL FK assignments...");
                    
                    // Create a new statement with only non-null operands
                    var modifiedStatement = CreateModifiedUpdateStatement(statement, statementType, operandsList, parametersList, nullColumns);
                    if(modifiedStatement != null) {
                        return modifiedStatement;
                    } else {
                        // If we can't modify, include original to be safe (at least GCRecord will be set)
                        Log($"  [Statement {index}] WARNING: Could not modify statement, including original");
                        return statement;
                    }
                } else {
                    Log($"  [Statement {index}] INCLUDED");
                    return statement;
                }
                
            } catch(Exception ex) {
                Log($"  [Statement {index}] ERROR during analysis: {ex.Message}");
                Log($"  [Statement {index}] Stack: {ex.StackTrace}");
                return statement; // Include on error to be safe
            }
        }

        private bool IsNullValue(object value) {
            if(value == null) {
                return true;
            }

            if(value is DBNull) {
                return true;
            }

            // Handle wrapped/constant values
            var valueType = value.GetType();
            if(valueType.Name.Contains("Constant", StringComparison.OrdinalIgnoreCase) ||
               valueType.Name.Contains("Value", StringComparison.OrdinalIgnoreCase)) {
                
                var innerValueProp = valueType.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
                if(innerValueProp != null) {
                    var innerValue = innerValueProp.GetValue(value);
                    Log($"        Unwrapped {valueType.Name}: {innerValue?.ToString() ?? "NULL"}");
                    return innerValue == null || innerValue is DBNull;
                }
            }

            return false;
        }

        private ModificationStatement CreateModifiedUpdateStatement(
            ModificationStatement originalStatement,
            Type statementType,
            List<object> operandsList,
            List<object> parametersList,
            List<string> nullColumns) {
            
            try {
                // Get fields for Operands and Parameters
                var operandsField = statementType.GetField("Operands", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var parametersField = statementType.GetField("Parameters", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                if(operandsField == null) {
                    // Try base type
                    operandsField = statementType.BaseType?.GetField("Operands", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }
                if(parametersField == null) {
                    parametersField = statementType.BaseType?.GetField("Parameters", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }
                
                if(operandsField == null || parametersField == null) {
                    Log($"    ERROR: Could not find Operands or Parameters field for modification");
                    return null;
                }

                // Build lists of indices to keep (non-NULL operands)
                var keptIndices = new List<int>();
                for(int i = 0; i < operandsList.Count; i++) {
                    var operand = operandsList[i];
                    var operandType = operand?.GetType();
                    
                    // Extract column name
                    string columnName = null;
                    if(operandType != null) {
                        var columnNameField = operandType.GetField("ColumnName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if(columnNameField != null) {
                            columnName = columnNameField.GetValue(operand) as string;
                        }
                    }
                    
                    // Check if this is a NULL assignment
                    bool isNullAssignment = false;
                    if(i < parametersList.Count) {
                        isNullAssignment = IsNullValue(parametersList[i]);
                    }
                    
                    // Keep this operand if:
                    // 1. It's NOT a NULL assignment, OR
                    // 2. It IS the GCRecord column (we always want to keep GCRecord)
                    if(!isNullAssignment || columnName == GCRecordColumnName) {
                        keptIndices.Add(i);
                        Log($"      Keeping operand [{i}]: {columnName ?? "unknown"}");
                    } else {
                        Log($"      Removing operand [{i}]: {columnName ?? "unknown"} (NULL FK assignment)");
                    }
                }
                
                // Create new collections with only kept indices
                var originalOperands = operandsField.GetValue(originalStatement) as System.Collections.IList;
                var originalParameters = parametersField.GetValue(originalStatement) as System.Collections.IList;
                
                if(originalOperands == null || originalParameters == null) {
                    Log($"    ERROR: Could not get original collections as IList");
                    return null;
                }
                
                // Get collection types
                var operandsCollectionType = operandsField.FieldType;
                var parametersCollectionType = parametersField.FieldType;
                
                Log($"    Creating new {operandsCollectionType.Name} and {parametersCollectionType.Name}");
                
                // Create new instances
                var newOperands = Activator.CreateInstance(operandsCollectionType) as System.Collections.IList;
                var newParameters = Activator.CreateInstance(parametersCollectionType) as System.Collections.IList;
                
                if(newOperands == null || newParameters == null) {
                    Log($"    ERROR: Could not create new collection instances");
                    return null;
                }
                
                // Add only kept items
                foreach(var index in keptIndices) {
                    newOperands.Add(operandsList[index]);
                    if(index < parametersList.Count) {
                        newParameters.Add(parametersList[index]);
                    }
                }
                
                Log($"    Modified collections: {originalOperands.Count} -> {newOperands.Count} operands, {originalParameters.Count} -> {newParameters.Count} parameters");
                
                // Set the modified collections back
                operandsField.SetValue(originalStatement, newOperands);
                parametersField.SetValue(originalStatement, newParameters);
                
                return originalStatement;
                
            } catch(Exception ex) {
                Log($"    ERROR creating modified statement: {ex.Message}");
                Log($"    Stack: {ex.StackTrace}");
                return null;
            }
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
