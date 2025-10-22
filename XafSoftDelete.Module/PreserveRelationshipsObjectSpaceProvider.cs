using System.Data;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;

namespace XafSoftDelete.Module {
    /// <summary>
    /// XPObjectSpaceProvider implementation that uses PreserveRelationshipsDataLayer
    /// to prevent soft-delete from nullifying relationships.
    /// 
    /// NOTE: We cannot override Session.Delete() because it's not virtual. The DataLayer
    /// is our only interception point to filter out NULL-assigning UPDATE statements.
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
