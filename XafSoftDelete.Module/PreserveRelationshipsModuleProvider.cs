using System.Data;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using System;
using DevExpress.ExpressApp;

namespace XafSoftDelete.Module {
    /// <summary>
    /// Custom provider that uses PreserveRelationshipsDataLayer to prevent
    /// relationship nullification during soft delete.
    /// 
    /// NOTE: We cannot override Session.Delete() because it's not virtual.
    /// The DataLayer is our only interception point.
    /// </summary>
    public class PreserveRelationshipsModuleProvider : XPObjectSpaceProvider {
        private bool preserveRelationships = true;
        
        public PreserveRelationshipsModuleProvider(IServiceProvider serviceProvider, string connectionString, IDbConnection connection) 
            : base(serviceProvider, connectionString, connection, true) { }
            
        public PreserveRelationshipsModuleProvider(IServiceProvider serviceProvider, IXpoDataStoreProvider dataStoreProvider) 
            : base(serviceProvider, dataStoreProvider, true) { }

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
