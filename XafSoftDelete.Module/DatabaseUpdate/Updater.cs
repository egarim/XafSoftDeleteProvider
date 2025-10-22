using DevExpress.ExpressApp;
using DevExpress.Data.Filtering;
using DevExpress.Persistent.Base;
using DevExpress.ExpressApp.Updating;
using DevExpress.ExpressApp.Security;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.ExpressApp.Security.Strategy;
using DevExpress.Xpo;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.BaseImpl.PermissionPolicy;
using XafSoftDelete.Module.BusinessObjects;
using Microsoft.Extensions.DependencyInjection;

namespace XafSoftDelete.Module.DatabaseUpdate;

// For more typical usage scenarios, be sure to check out https://docs.devexpress.com/eXpressAppFramework/DevExpress.ExpressApp.Updating.ModuleUpdater
public class Updater : ModuleUpdater {
    public Updater(IObjectSpace objectSpace, Version currentDBVersion) :
        base(objectSpace, currentDBVersion) {
    }
    public override void UpdateDatabaseAfterUpdateSchema() {
        base.UpdateDatabaseAfterUpdateSchema();
        
        // Create test data: Customer and Order for soft delete testing
        CreateTestCustomerAndOrder();

        // The code below creates users and roles for testing purposes only.
        // In production code, you can create users and assign roles to them automatically, as described in the following help topic:
        // https://docs.devexpress.com/eXpressAppFramework/119064/data-security-and-safety/security-system/authentication
#if !RELEASE
        // If a role doesn't exist in the database, create this role
        var defaultRole = CreateDefaultRole();
        var adminRole = CreateAdminRole();

        ObjectSpace.CommitChanges(); //This line persists created object(s).

        var serviceProvider = GetServiceProviderForObjectSpace(ObjectSpace);
        // In some update contexts (design-time or when the application DI container isn't available)
        // the ServiceProvider may be null. In that case skip user auto-creation.
        if(serviceProvider == null) {
            return;
        }

        UserManager userManager = serviceProvider.GetRequiredService<UserManager>();

        // If a user named 'User' doesn't exist in the database, create this user
        if(userManager.FindUserByName<ApplicationUser>(ObjectSpace, "User") == null) {
            // Set a password if the standard authentication type is used
            string EmptyPassword = "";
            _ = userManager.CreateUser<ApplicationUser>(ObjectSpace, "User", EmptyPassword, (user) => {
                // Add the Users role to the user
                user.Roles.Add(defaultRole);
            });
        }

        // If a user named 'Admin' doesn't exist in the database, create this user
        if(userManager.FindUserByName<ApplicationUser>(ObjectSpace, "Admin") == null) {
            // Set a password if the standard authentication type is used
            string EmptyPassword = "";
            _ = userManager.CreateUser<ApplicationUser>(ObjectSpace, "Admin", EmptyPassword, (user) => {
                // Add the Administrators role to the user
                user.Roles.Add(adminRole);
            });
        }

        ObjectSpace.CommitChanges(); //This line persists created object(s).
#endif
    }
    public override void UpdateDatabaseBeforeUpdateSchema() {
        base.UpdateDatabaseBeforeUpdateSchema();
        //if(CurrentDBVersion < new Version("1.1.0.0") && CurrentDBVersion > new Version("0.0.0.0")) {
        //    RenameColumn("DomainObject1Table", "OldColumnName", "NewColumnName");
        //}
    }
    private PermissionPolicyRole CreateAdminRole() {
        PermissionPolicyRole adminRole = ObjectSpace.FirstOrDefault<PermissionPolicyRole>(r => r.Name == "Administrators");
        if(adminRole == null) {
            adminRole = ObjectSpace.CreateObject<PermissionPolicyRole>();
            adminRole.Name = "Administrators";
            adminRole.IsAdministrative = true;
        }
        return adminRole;
    }
    private PermissionPolicyRole CreateDefaultRole() {
        PermissionPolicyRole defaultRole = ObjectSpace.FirstOrDefault<PermissionPolicyRole>(role => role.Name == "Default");
        if(defaultRole == null) {
            defaultRole = ObjectSpace.CreateObject<PermissionPolicyRole>();
            defaultRole.Name = "Default";

            defaultRole.AddObjectPermissionFromLambda<ApplicationUser>(SecurityOperations.Read, cm => cm.Oid == (Guid)CurrentUserIdOperator.CurrentUserId(), SecurityPermissionState.Allow);
            defaultRole.AddNavigationPermission(@"Application/NavigationItems/Items/Default/Items/MyDetails", SecurityPermissionState.Allow);
            defaultRole.AddMemberPermissionFromLambda<ApplicationUser>(SecurityOperations.Write, "ChangePasswordOnFirstLogon", cm => cm.Oid == (Guid)CurrentUserIdOperator.CurrentUserId(), SecurityPermissionState.Allow);
            defaultRole.AddMemberPermissionFromLambda<ApplicationUser>(SecurityOperations.Write, "StoredPassword", cm => cm.Oid == (Guid)CurrentUserIdOperator.CurrentUserId(), SecurityPermissionState.Allow);
            defaultRole.AddTypePermissionsRecursively<PermissionPolicyRole>(SecurityOperations.Read, SecurityPermissionState.Deny);
            defaultRole.AddObjectPermission<ModelDifference>(SecurityOperations.ReadWriteAccess, "UserId = ToStr(CurrentUserId())", SecurityPermissionState.Allow);
            defaultRole.AddObjectPermission<ModelDifferenceAspect>(SecurityOperations.ReadWriteAccess, "Owner.UserId = ToStr(CurrentUserId())", SecurityPermissionState.Allow);
            defaultRole.AddTypePermissionsRecursively<ModelDifference>(SecurityOperations.Create, SecurityPermissionState.Allow);
            defaultRole.AddTypePermissionsRecursively<ModelDifferenceAspect>(SecurityOperations.Create, SecurityPermissionState.Allow);
        }
        return defaultRole;
    }
    private IServiceProvider GetServiceProviderForObjectSpace(IObjectSpace objectSpace) {
        if(objectSpace == null) return null;
        // First, try the ObjectSpace.ServiceProvider (available in DI-aware ObjectSpaces)
        try {
            var sp = objectSpace.ServiceProvider;
            if(sp != null) return sp;
        }
        catch { }

        // If the ObjectSpace is an XPO-based ObjectSpace, try to get the Session.ServiceProvider
        var xpObjectSpace = objectSpace as DevExpress.ExpressApp.Xpo.XPObjectSpace;
        if(xpObjectSpace != null) {
            try {
                var sp = xpObjectSpace.ServiceProvider;
                if(sp != null) return sp;
            }
            catch { }
        }

        // Unable to resolve a service provider in this context
        // As a last resort, use an ambient ServiceProvider set by the host when running DB updates
        try {
            var ambient = ServiceProviderAccessor.Current;
            if(ambient != null) return ambient;
        }
        catch { }
        return null;
    }
    
    private void CreateTestCustomerAndOrder() {
        // Check if we already have test customer
        var customer = ObjectSpace.FirstOrDefault<Customer>(c => c.Name == "Test Customer");
        if(customer == null) {
            // Create a test customer
            customer = ObjectSpace.CreateObject<Customer>();
            customer.Name = "Test Customer";
            customer.Email = "test@example.com";
        }
        
        // Check if customer already has an order
        var existingOrder = ObjectSpace.FirstOrDefault<Order>(o => o.Customer == customer);
        if(existingOrder != null) {
            return; // Test order already exists for this customer
        }
        
        // Create a test order associated with the customer
        var order = ObjectSpace.CreateObject<Order>();
        order.Amount = 100.00m;
        order.OrderDate = DateTime.Now;
        order.Customer = customer; // Associate order with customer
        
        // Commit the changes
        ObjectSpace.CommitChanges();
    }
}
