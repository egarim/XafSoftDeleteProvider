using System.Data;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using CustomXpoProviders;

namespace XafSoftDelete.Module {
    // Simple provider class in the Module project that depends on the CustomXpoProviders library
    public class PreserveRelationshipsModuleProvider : XPObjectSpaceProvider {
        private bool preserveRelationships = true;
        public PreserveRelationshipsModuleProvider(string connectionString, IDbConnection connection) : base(connectionString, connection) { }
        protected override IDataLayer CreateDataLayer(DevExpress.Xpo.DB.IDataStore dataStore) {
            return new PreserveRelationshipsDataLayer(XPDictionary, dataStore) {
                PreserveRelationshipsOnSoftDelete = preserveRelationships
            };
        }
        public bool PreserveRelationshipsOnSoftDelete {
            get => preserveRelationships;
            set {
                preserveRelationships = value;
                if(DataLayer is PreserveRelationshipsDataLayer pl) pl.PreserveRelationshipsOnSoftDelete = value;
            }
        }
    }
}
