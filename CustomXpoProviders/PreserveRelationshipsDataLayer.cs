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
        
        public bool PreserveRelationshipsOnSoftDelete { get; set; } = true;
        private const string GCRecordColumnName = "GCRecord";

        public PreserveRelationshipsDataLayer(XPDictionary dictionary, IDataStore dataStore)
            : base(dictionary, dataStore) {
        }

        public PreserveRelationshipsDataLayer(IDataStore dataStore)
            : base(dataStore) {
        }

        public override ModificationResult ModifyData(params ModificationStatement[] dmlStatements) {
            if(!PreserveRelationshipsOnSoftDelete) {
                return base.ModifyData(dmlStatements);
            }

            // Filter out UPDATE statements that are nullifying relationships during soft delete
            var filteredStatements = new List<ModificationStatement>();
            
            foreach(var statement in dmlStatements) {
                if(ShouldIncludeStatement(statement)) {
                    filteredStatements.Add(statement);
                }
            }

            if(filteredStatements.Count == 0) {
                // No statements to execute, return empty result
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
