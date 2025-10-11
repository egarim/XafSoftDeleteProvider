using System.Configuration;
using DevExpress.ExpressApp;
using Microsoft.Extensions.DependencyInjection;
using DevExpress.ExpressApp.ApplicationBuilder;
using DevExpress.ExpressApp.Win.ApplicationBuilder;
using DevExpress.ExpressApp.Security;
using DevExpress.ExpressApp.Win;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.XtraEditors;
using DevExpress.Persistent.BaseImpl.PermissionPolicy;
using DevExpress.ExpressApp.Design;

namespace XafSoftDelete.Win;

public class ApplicationBuilder : IDesignTimeApplicationFactory {
    public static WinApplication BuildApplication(string connectionString) {
        var builder = WinApplication.CreateBuilder();
        // Register custom services for Dependency Injection. For more information, refer to the following topic: https://docs.devexpress.com/eXpressAppFramework/404430/
        // builder.Services.AddScoped<CustomService>();
        // Register 3rd-party IoC containers (like Autofac, Dryloc, etc.)
        // builder.UseServiceProviderFactory(new DryIocServiceProviderFactory());
        // builder.UseServiceProviderFactory(new AutofacServiceProviderFactory());

        builder.UseApplication<XafSoftDeleteWindowsFormsApplication>();
        builder.Modules
            .AddCloning()
            .AddConditionalAppearance()
            .AddValidation(options => {
                options.AllowValidationDetailsAccess = false;
            })
            .Add<XafSoftDelete.Module.XafSoftDeleteModule>()
            .Add<XafSoftDeleteWinModule>();
        var dataStoreProviderManager = new DevExpress.ExpressApp.ApplicationBuilder.DataStoreProviderManager();
        builder.ObjectSpaceProviders
            .AddSecuredXpo((serviceProvider, options) => {
                options.ConnectionString = connectionString;
                options.ThreadSafe = true;
                options.UseSharedDataStoreProvider = true;
                // Create the custom provider using the shared data store provider when the application builder provides context
                options.CustomCreateObjectSpaceProvider = (context) => {
                    var dataStoreProvider = dataStoreProviderManager.GetSharedDataStoreProvider(options.ConnectionString, true);
                    // Use the DI context's service provider when available (may be null in some hosts)
                    return new XafSoftDelete.Module.PreserveRelationshipsModuleProvider(context?.ServiceProvider, dataStoreProvider) {
                        PreserveRelationshipsOnSoftDelete = true
                    };
                };
            })
            .AddNonPersistent();
        builder.Security
            .UseIntegratedMode(options => {
                options.Lockout.Enabled = true;

                options.RoleType = typeof(PermissionPolicyRole);
                options.UserType = typeof(XafSoftDelete.Module.BusinessObjects.ApplicationUser);
                options.UserLoginInfoType = typeof(XafSoftDelete.Module.BusinessObjects.ApplicationUserLoginInfo);
                options.UseXpoPermissionsCaching();
                options.Events.OnSecurityStrategyCreated += securityStrategy => {
                   // Use the 'PermissionsReloadMode.NoCache' option to load the most recent permissions from the database once
                   // for every Session instance when secured data is accessed through this instance for the first time.
                   // Use the 'PermissionsReloadMode.CacheOnFirstAccess' option to reduce the number of database queries.
                   // In this case, permission requests are loaded and cached when secured data is accessed for the first time
                   // and used until the current user logs out.
                   // See the following article for more details: https://docs.devexpress.com/eXpressAppFramework/DevExpress.ExpressApp.Security.SecurityStrategy.PermissionsReloadMode.
                   ((SecurityStrategy)securityStrategy).PermissionsReloadMode = PermissionsReloadMode.NoCache;
                };
            })
            .AddPasswordAuthentication();
        builder.AddBuildStep(application => {
            application.ConnectionString = connectionString;
#if DEBUG
            if(System.Diagnostics.Debugger.IsAttached && application.CheckCompatibilityType == CheckCompatibilityType.DatabaseSchema) {
                application.DatabaseUpdateMode = DatabaseUpdateMode.UpdateDatabaseAlways;
            }
#endif
        });
        var winApplication = builder.Build();
        try {
            // Build a service provider from the configured services and set it as a global fallback for the updater
            var sp = builder.Services.BuildServiceProvider();
            XafSoftDelete.Module.ServiceProviderAccessor.Global = sp;
        }
        catch {
            // ignore if building provider fails for design-time scenarios
        }
        return winApplication;
    }

    XafApplication IDesignTimeApplicationFactory.Create()
        => BuildApplication(XafApplication.DesignTimeConnectionString);
}
