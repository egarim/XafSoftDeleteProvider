# Using CustomXpoProviders in XAF Applications

This guide explains how to integrate the `PreserveRelationshipsDataLayer` into your DevExpress XAF (eXpressApp Framework) application.

## Overview

The custom data layer preserves relationships during soft delete (deferred deletion) operations. In standard XPO/XAF, when you delete an object with `[DeferredDeletion(true)]`, all foreign key references are set to NULL. This custom implementation keeps those relationships intact.

## Integration Methods

There are **3 ways** to integrate the custom data layer into XAF:

### ✅ Method 1: Custom XPObjectSpaceProvider (Recommended)

This is the cleanest approach - create a custom `XPObjectSpaceProvider` that uses your custom data layer.

#### Step 1: Create Custom Provider Class

Create a new file in your XAF project (e.g., `PreserveRelationshipsObjectSpaceProvider.cs`):

```csharp
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.DC.Xpo;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using CustomXpoProviders;

namespace YourXafApp.Module {
    
    /// <summary>
    /// Custom XPObjectSpaceProvider that preserves relationships during soft delete
    /// </summary>
    public class PreserveRelationshipsObjectSpaceProvider : XPObjectSpaceProvider {
        
        private readonly bool preserveRelationships;
        
        public PreserveRelationshipsObjectSpaceProvider(
            string connectionString, 
            bool preserveRelationships = true)
            : base(connectionString, null) {
            this.preserveRelationships = preserveRelationships;
        }
        
        public PreserveRelationshipsObjectSpaceProvider(
            IXpoDataStoreProvider dataStoreProvider,
            bool preserveRelationships = true)
            : base(dataStoreProvider) {
            this.preserveRelationships = preserveRelationships;
        }
        
        /// <summary>
        /// Override to create PreserveRelationshipsDataLayer instead of standard SimpleDataLayer/ThreadSafeDataLayer
        /// </summary>
        protected override IDataLayer CreateDataLayer(IDataStore dataStore) {
            // Use custom data layer that preserves relationships
            var dataLayer = new PreserveRelationshipsDataLayer(XPDictionary, dataStore) {
                PreserveRelationshipsOnSoftDelete = preserveRelationships
            };
            
            // If you need thread-safe version, wrap it:
            // return new ThreadSafeDataLayer(XPDictionary, dataStore);
            // Note: ThreadSafeDataLayer wrapping may require additional configuration
            
            return dataLayer;
        }
        
        /// <summary>
        /// Enable/disable relationship preservation at runtime
        /// </summary>
        public bool PreserveRelationshipsOnSoftDelete {
            get => preserveRelationships;
            set {
                if (DataLayer is PreserveRelationshipsDataLayer customLayer) {
                    customLayer.PreserveRelationshipsOnSoftDelete = value;
                }
            }
        }
    }
}
```

#### Step 2: Register in Application Builder (XAF 23.1+)

In your `YourAppNameWindowsFormsApplication.cs` or `YourAppNameBlazorApplication.cs`:

```csharp
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.ApplicationBuilder;
using DevExpress.ExpressApp.Win.ApplicationBuilder;

namespace YourXafApp.Win {
    public class ApplicationBuilder : IDesignTimeApplicationFactory {
        public static WinApplication BuildApplication(string connectionString) {
            var builder = WinApplication.CreateBuilder();
            
            // Configure the application
            builder.UseApplication<YourXafAppWindowsFormsApplication>();
            
            // Register custom ObjectSpaceProvider
            builder.ObjectSpaceProviders
                .AddXpo((serviceProvider, options) => {
                    options.ConnectionString = connectionString;
                    options.ThreadSafe = false;
                })
                .UseCustomObjectSpaceProvider<PreserveRelationshipsObjectSpaceProvider>(
                    (serviceProvider, connectionString) => {
                        return new PreserveRelationshipsObjectSpaceProvider(
                            connectionString, 
                            preserveRelationships: true
                        );
                    }
                );
            
            builder.Modules
                .AddYourModules(); // Your module registration
            
            return builder.Build();
        }
    }
}
```

#### Step 3: Legacy Registration (Pre-XAF 23.1)

In your `YourAppNameWindowsFormsApplication.cs` `CreateDefaultObjectSpaceProvider` method:

```csharp
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;

namespace YourXafApp.Win {
    public partial class YourXafAppWindowsFormsApplication : WinApplication {
        
        protected override void CreateDefaultObjectSpaceProvider(CreateCustomObjectSpaceProviderEventArgs args) {
            // Use custom provider instead of standard XPObjectSpaceProvider
            args.ObjectSpaceProvider = new PreserveRelationshipsObjectSpaceProvider(
                args.ConnectionString,
                preserveRelationships: true
            );
        }
    }
}
```

---

### Method 2: Direct IDataLayer Injection

If you want more control, you can create the data layer directly and inject it.

#### In Module.cs or Application.cs:

```csharp
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using CustomXpoProviders;

namespace YourXafApp.Module {
    public sealed partial class YourXafAppModule : ModuleBase {
        
        public override void Setup(XafApplication application) {
            base.Setup(application);
            
            // Get connection string
            string connectionString = application.ConnectionString;
            
            // Create custom data layer
            var dataStore = XpoDefault.GetConnectionProvider(
                connectionString, 
                AutoCreateOption.DatabaseAndSchema
            );
            
            var dictionary = new DevExpress.Xpo.Metadata.ReflectionDictionary();
            var dataLayer = new PreserveRelationshipsDataLayer(dictionary, dataStore) {
                PreserveRelationshipsOnSoftDelete = true
            };
            
            // Create and register ObjectSpaceProvider
            var objectSpaceProvider = new XPObjectSpaceProvider(
                new CustomDataStoreProvider(dataLayer)
            );
            
            application.CreateCustomObjectSpaceProvider += (s, e) => {
                e.ObjectSpaceProvider = objectSpaceProvider;
            };
        }
    }
    
    // Helper class to wrap existing data layer
    class CustomDataStoreProvider : IXpoDataStoreProvider {
        private readonly IDataLayer dataLayer;
        
        public CustomDataStoreProvider(IDataLayer dataLayer) {
            this.dataLayer = dataLayer;
        }
        
        public IDataStore CreateWorkingStore(out IDisposable[] disposableObjects) {
            disposableObjects = Array.Empty<IDisposable>();
            return dataLayer.Connection;
        }
        
        public IDataStore CreateUpdatingStore(bool allowUpdateSchema, out IDisposable[] disposableObjects) {
            disposableObjects = Array.Empty<IDisposable>();
            return dataLayer.Connection;
        }
        
        public IDataStore CreateSchemaCheckingStore(out IDisposable[] disposableObjects) {
            disposableObjects = Array.Empty<IDisposable>();
            return dataLayer.Connection;
        }
        
        public string ConnectionString => dataLayer.Connection.ConnectionString;
    }
}
```

---

### Method 3: Runtime Switching (Advanced)

For scenarios where you want to toggle behavior at runtime:

```csharp
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;

namespace YourXafApp.Module {
    public class CustomObjectSpaceProvider : XPObjectSpaceProvider {
        
        public CustomObjectSpaceProvider(string connectionString) 
            : base(connectionString, null) {
        }
        
        protected override IDataLayer CreateDataLayer(IDataStore dataStore) {
            // Check application settings or user preferences
            bool shouldPreserveRelationships = GetPreserveRelationshipsSetting();
            
            if (shouldPreserveRelationships) {
                return new PreserveRelationshipsDataLayer(XPDictionary, dataStore) {
                    PreserveRelationshipsOnSoftDelete = true
                };
            } else {
                // Fall back to standard XPO behavior
                return base.CreateDataLayer(dataStore);
            }
        }
        
        private bool GetPreserveRelationshipsSetting() {
            // Read from config, database, or user settings
            return ConfigurationManager.AppSettings["PreserveRelationships"] == "true";
        }
    }
}
```

---

## Testing in XAF

### Sample Business Objects

```csharp
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace YourXafApp.Module.BusinessObjects {
    
    [DefaultClassOptions]
    [DeferredDeletion(true)] // Enable soft delete
    public class Customer : BaseObject {
        public Customer(Session session) : base(session) { }
        
        private string name;
        public string Name {
            get => name;
            set => SetPropertyValue(nameof(Name), ref name, value);
        }
        
        [Association("Customer-Orders")]
        public XPCollection<Order> Orders {
            get => GetCollection<Order>(nameof(Orders));
        }
    }
    
    [DefaultClassOptions]
    public class Order : BaseObject {
        public Order(Session session) : base(session) { }
        
        private decimal amount;
        public decimal Amount {
            get => amount;
            set => SetPropertyValue(nameof(Amount), ref amount, value);
        }
        
        private Customer customer;
        [Association("Customer-Orders")]
        public Customer Customer {
            get => customer;
            set => SetPropertyValue(nameof(Customer), ref customer, value);
        }
    }
}
```

### Test Scenario

1. **Create a Customer and Order:**
   ```csharp
   using (var objectSpace = Application.CreateObjectSpace()) {
       var customer = objectSpace.CreateObject<Customer>();
       customer.Name = "John Doe";
       
       var order = objectSpace.CreateObject<Order>();
       order.Customer = customer;
       order.Amount = 1000;
       
       objectSpace.CommitChanges();
   }
   ```

2. **Delete Customer (Soft Delete):**
   ```csharp
   using (var objectSpace = Application.CreateObjectSpace()) {
       var customer = objectSpace.FindObject<Customer>(
           CriteriaOperator.Parse("Name = 'John Doe'")
       );
       
       objectSpace.Delete(customer); // Soft delete
       objectSpace.CommitChanges();
   }
   ```

3. **Verify Relationship is Preserved:**
   ```csharp
   using (var objectSpace = Application.CreateObjectSpace()) {
       var order = objectSpace.GetObjects<Order>()[0];
       
       // With STANDARD XPO: order.Customer would be NULL
       // With CustomXpoProviders: order.Customer is STILL SET! ✅
       
       Assert.NotNull(order.Customer);
       Console.WriteLine($"Order still references: {order.Customer.Name}");
   }
   ```

---

## Configuration Options

### Enable/Disable at Runtime

```csharp
// Get your custom provider
if (Application.ObjectSpaceProvider is PreserveRelationshipsObjectSpaceProvider customProvider) {
    // Toggle behavior
    customProvider.PreserveRelationshipsOnSoftDelete = true; // or false
}
```

### Per-Transaction Control

If you need different behavior for specific operations:

```csharp
using (var objectSpace = Application.CreateObjectSpace()) {
    var dataLayer = ((XPObjectSpace)objectSpace).Session.DataLayer;
    
    if (dataLayer is PreserveRelationshipsDataLayer customLayer) {
        // Temporarily disable for this operation
        customLayer.PreserveRelationshipsOnSoftDelete = false;
        
        objectSpace.Delete(someObject);
        objectSpace.CommitChanges();
        
        // Re-enable
        customLayer.PreserveRelationshipsOnSoftDelete = true;
    }
}
```

---

## Troubleshooting

### Issue: "Cannot modify Dictionary because ThreadSafeDataLayer uses it"

**Solution:** Don't wrap `PreserveRelationshipsDataLayer` in `ThreadSafeDataLayer`. Instead, use it directly:

```csharp
protected override IDataLayer CreateDataLayer(IDataStore dataStore) {
    // DON'T wrap in ThreadSafeDataLayer
    return new PreserveRelationshipsDataLayer(XPDictionary, dataStore) {
        PreserveRelationshipsOnSoftDelete = true
    };
}
```

### Issue: Changes not being persisted

**Cause:** Make sure you're calling `objectSpace.CommitChanges()` or `uow.CommitChanges()`.

### Issue: Still getting NULL references

**Checklist:**
1. ✅ Custom provider is registered correctly
2. ✅ `PreserveRelationshipsOnSoftDelete = true`
3. ✅ Business object has `[DeferredDeletion(true)]` attribute
4. ✅ Using the custom DLL (not standard XPO)

---

## Performance Considerations

- **Minimal Overhead:** The custom data layer only adds reflection-based inspection during DELETE operations
- **No Impact on Reads:** Query performance is identical to standard XPO
- **Memory:** No additional memory overhead

---

## Migration from Standard XPO

If you have an existing XAF application:

1. **Add Reference:** Reference `CustomXpoProviders.dll`
2. **Replace Provider:** Change from `XPObjectSpaceProvider` to `PreserveRelationshipsObjectSpaceProvider`
3. **Test:** Verify soft delete behavior with your business objects
4. **Deploy:** No database schema changes required!

---

## Advanced: Selective Preservation

If you only want to preserve relationships for specific types:

```csharp
public class SelectivePreservationDataLayer : PreserveRelationshipsDataLayer {
    private readonly HashSet<string> typesToPreserve;
    
    public SelectivePreservationDataLayer(
        XPDictionary dictionary, 
        IDataStore dataStore,
        params Type[] typesToPreserve) 
        : base(dictionary, dataStore) {
        this.typesToPreserve = new HashSet<string>(
            typesToPreserve.Select(t => t.Name)
        );
    }
    
    protected override bool ShouldPreserveForType(string tableName) {
        return typesToPreserve.Contains(tableName);
    }
}
```

---

## Summary

✅ **Easiest:** Use Method 1 (Custom `XPObjectSpaceProvider`)  
✅ **Most Control:** Use Method 2 (Direct `IDataLayer` injection)  
✅ **Most Flexible:** Use Method 3 (Runtime switching)

The custom data layer integrates seamlessly with XAF and requires **no changes** to your business objects or database schema!

## Questions?

- Check `README.md` for library documentation
- Check `STATUS.md` for current implementation status
- Check `DeferredDeletionAnalysis.md` for XPO internals explanation

