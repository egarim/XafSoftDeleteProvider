using System.Data;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;

namespace XafSoftDelete.Module {
    /// <summary>
    /// XPObjectSpaceProvider implementation that creates PreserveRelationshipsDataLayer instances
    /// so that soft-delete does not nullify relationships.
    /// </summary>
    public class PreserveRelationshipsObjectSpaceProvider : XPObjectSpaceProvider {
        private bool preserveRelationships = true;
        public PreserveRelationshipsObjectSpaceProvider(string connectionString, IDbConnection connection) : base(connectionString, connection) { }
        public PreserveRelationshipsObjectSpaceProvider(IXpoDataStoreProvider dataStoreProvider) : base(dataStoreProvider) { }

        protected override IDataLayer CreateDataLayer(IDataStore dataStore) {
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
