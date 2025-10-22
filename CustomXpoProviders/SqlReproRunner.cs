using System;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;

namespace CustomXpoProviders {
    // Simple runner to reproduce soft-delete SQL and log to console
    public static class SqlReproRunner {
        public static void Run(string connectionString) {
            XpoDefault.DataLayer = PreserveRelationshipsDataLayerHelper.CreateDataLayer(connectionString);
            XpoDefault.Session = null;
            XpoDefault.Session = new Session(XpoDefault.DataLayer);

            // Enable SQL tracing
            XpoDefault.DataLayer.EnableEntityEvents = true;
            DevExpress.Xpo.DB.DataStoreBase.Log += (s, e) => Console.WriteLine(e.Message);

            using(var uow = new UnitOfWork()) {
                // Try to find two example types in the dictionary
                var types = uow.Dictionary.GetClasses();
                Console.WriteLine("Classes in dictionary:");
                foreach(var c in types) Console.WriteLine(c.ClassType?.FullName);

                // For a minimal reproducible test, user should replace below with real types.
                Console.WriteLine("No automatic repro created. Please run your app or tests against this data layer to observe SQL.");
            }
        }
    }
}
