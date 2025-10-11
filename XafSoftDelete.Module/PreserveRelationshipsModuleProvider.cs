using System.Data;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using CustomXpoProviders;
using System;
using DevExpress.ExpressApp;

namespace XafSoftDelete.Module {
    // Simple provider class in the Module project that depends on the CustomXpoProviders library
    public class PreserveRelationshipsModuleProvider : XPObjectSpaceProvider {
        private bool preserveRelationships = true;
    //public PreserveRelationshipsModuleProvider(string connectionString, IDbConnection connection) : base(connectionString, connection) { }
    // Constructor that allows passing an IServiceProvider along with a connection string/connection
    public PreserveRelationshipsModuleProvider(IServiceProvider serviceProvider, string connectionString, IDbConnection connection) : base(serviceProvider, connectionString, connection, true) { }
    // New constructor used by the ApplicationBuilder.CustomCreateObjectSpaceProvider path
    public PreserveRelationshipsModuleProvider(IServiceProvider serviceProvider, IXpoDataStoreProvider dataStoreProvider) : base(serviceProvider, dataStoreProvider, true) { }
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
