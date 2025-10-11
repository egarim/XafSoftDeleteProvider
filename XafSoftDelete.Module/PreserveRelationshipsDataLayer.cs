using System;
using System.Collections.Generic;
using System.Reflection;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.Metadata;

namespace XafSoftDelete.Module {
    // NOTE: This is a copy of the PreserveRelationshipsDataLayer from the CustomXpoProviders
    // project adapted into the Module so the Module can be self-contained for the sample.

    public class PreserveRelationshipsDataLayer : SimpleDataLayer {
        public bool PreserveRelationshipsOnSoftDelete { get; set; } = true;
        private const string GCRecordColumnName = "GCRecord";

        public PreserveRelationshipsDataLayer(XPDictionary dictionary, IDataStore dataStore)
            : base(dictionary, dataStore) { }

        public PreserveRelationshipsDataLayer(IDataStore dataStore)
            : base(dataStore) { }

        public override ModificationResult ModifyData(params ModificationStatement[] dmlStatements) {
            if(!PreserveRelationshipsOnSoftDelete) {
                return base.ModifyData(dmlStatements);
            }

            var filteredStatements = new List<ModificationStatement>();
            foreach(var statement in dmlStatements) {
                if(ShouldIncludeStatement(statement)) {
                    filteredStatements.Add(statement);
                }
            }

            if(filteredStatements.Count == 0) {
                return new ModificationResult();
            }

            return base.ModifyData(filteredStatements.ToArray());
        }

        private bool ShouldIncludeStatement(ModificationStatement statement) {
            var statementType = statement.GetType();
            var statementTypeName = statementType.Name;

            if(!statementTypeName.Contains("Update")) {
                return true;
            }

            try {
                var operandsProperty = statementType.GetProperty("Operands") ??
                                      statementType.GetProperty("Parameters") ??
                                      statementType.GetProperty("Assignments");
                if(operandsProperty == null) return true;

                var operands = operandsProperty.GetValue(statement) as System.Collections.IEnumerable;
                if(operands == null) return true;

                bool hasGCRecordUpdate = false;
                bool hasNullUpdates = false;

                foreach(var operand in operands) {
                    if(operand == null) continue;
                    var operandType = operand.GetType();

                    string columnName = null;
                    var columnNameProp = operandType.GetProperty("ColumnName") ??
                                        operandType.GetProperty("PropertyName") ??
                                        operandType.GetProperty("Name");

                    if(columnNameProp != null) {
                        columnName = columnNameProp.GetValue(operand) as string;
                    }

                    object value = null;
                    var valueProp = operandType.GetProperty("Value") ??
                                   operandType.GetProperty("Expression");
                    if(valueProp != null) {
                        value = valueProp.GetValue(operand);
                    }

                    if(columnName == GCRecordColumnName) hasGCRecordUpdate = true;

                    if(value == null && columnName != GCRecordColumnName) {
                        hasNullUpdates = true;
                    }
                    else if(value != null) {
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

                if(hasGCRecordUpdate && hasNullUpdates) return false;
            }
            catch {
                return true;
            }

            return true;
        }
    }

    public static class PreserveRelationshipsDataLayerHelper {
        public static PreserveRelationshipsDataLayer CreateSimpleDataLayer(string connectionString, bool preserveRelationships = true) {
            var dataStore = XpoDefault.GetConnectionProvider(connectionString, AutoCreateOption.DatabaseAndSchema);
            var dictionary = new ReflectionDictionary();
            var dataLayer = new PreserveRelationshipsDataLayer(dictionary, dataStore) {
                PreserveRelationshipsOnSoftDelete = preserveRelationships
            };
            return dataLayer;
        }

        public static PreserveRelationshipsDataLayer CreateSimpleDataLayer(IDataStore dataStore, XPDictionary dictionary, bool preserveRelationships = true) {
            var dataLayer = new PreserveRelationshipsDataLayer(dictionary, dataStore) {
                PreserveRelationshipsOnSoftDelete = preserveRelationships
            };
            return dataLayer;
        }

        public static IDataLayer CreateDataLayer(string connectionString, bool preserveRelationships = true) {
            var dataStore = XpoDefault.GetConnectionProvider(connectionString, AutoCreateOption.DatabaseAndSchema);
            var dictionary = new ReflectionDictionary();
            var dataLayer = new PreserveRelationshipsDataLayer(dictionary, dataStore) {
                PreserveRelationshipsOnSoftDelete = preserveRelationships
            };
            return dataLayer;
        }

        public static IDataLayer CreateDataLayer(IDataStore dataStore, XPDictionary dictionary, bool preserveRelationships = true) {
            var dataLayer = new PreserveRelationshipsDataLayer(dictionary, dataStore) {
                PreserveRelationshipsOnSoftDelete = preserveRelationships
            };
            return dataLayer;
        }
    }
}
