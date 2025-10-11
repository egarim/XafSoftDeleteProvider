using System;
using System.Data;
using DevExpress.Xpo;
// NOTE: This file contains XAF integration code that may need adjustment for different DevExpress versions
// The main solution (PreserveRelationshipsDataLayer) does not depend on this file

namespace CustomXpoProviders {
    
    /* 
     * IMPORTANT: This XAF integration code is version-specific and may not compile with all DevExpress versions.
     * 
     * For a simpler, more compatible solution, use PreserveRelationshipsDataLayer instead.
     * See PreserveRelationshipsDataLayer.cs and the documentation for the recommended approach.
     * 
     * To use XAF integration, you may need to adjust the base class constructors and method overrides
     * based on your specific DevExpress version.
     */
    
    // The XAF-specific provider below provides a straightforward way to plug
    // the PreserveRelationshipsDataLayer into XAF by overriding the data layer
    // creation. This class intentionally keeps implementation small and version
    // resilient by only changing the IDataLayer created for XPObjectSpaceProvider.

    using System.Data;
    using DevExpress.ExpressApp;
    using DevExpress.ExpressApp.Xpo;

    /// <summary>
    /// Custom XPObjectSpaceProvider that creates a PreserveRelationshipsDataLayer
    /// instead of the default data layer. Use this provider when registering
    /// custom ObjectSpaceProviders in your XAF application's startup code.
    /// </summary>
    public class PreserveRelationshipsObjectSpaceProvider : XPObjectSpaceProvider {
        private bool preserveRelationships = true;

        public PreserveRelationshipsObjectSpaceProvider(string connectionString, IDbConnection? connection)
            : base(connectionString, connection) {
        }

        public PreserveRelationshipsObjectSpaceProvider(IXpoDataStoreProvider dataStoreProvider)
            : base(dataStoreProvider) {
        }

        public PreserveRelationshipsObjectSpaceProvider(string connectionString, IDbConnection? connection, bool threadSafe)
            : base(connectionString, connection, threadSafe) {
        }

        public PreserveRelationshipsObjectSpaceProvider(IXpoDataStoreProvider dataStoreProvider, bool threadSafe)
            : base(dataStoreProvider, threadSafe) {
        }

        /// <summary>
        /// When true, the provider will create PreserveRelationshipsDataLayer instances
        /// that filter nullifying UPDATE statements during soft delete.
        /// </summary>
        public bool PreserveRelationshipsOnSoftDelete {
            get => preserveRelationships;
            set {
                preserveRelationships = value;
                if(DataLayer is PreserveRelationshipsDataLayer pl) {
                    pl.PreserveRelationshipsOnSoftDelete = value;
                }
            }
        }

        /// <summary>
        /// Override the data layer creation to return our custom implementation.
        /// </summary>
        protected override IDataLayer CreateDataLayer(IDataStore dataStore) {
            // Use the XPDictionary field provided by the base provider
            var dataLayer = new PreserveRelationshipsDataLayer(XPDictionary, dataStore) {
                PreserveRelationshipsOnSoftDelete = preserveRelationships
            };
            return dataLayer;
        }
    }
}
