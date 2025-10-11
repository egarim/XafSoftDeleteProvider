# Quick Reference: Using CustomXpoProviders in XAF

## 🚀 Quick Start (3 Steps)

### 1. Add Reference
```
Right-click project → Add Reference → Browse → CustomXpoProviders.dll
```

### 2. Create Custom Provider
```csharp
using DevExpress.ExpressApp.Xpo;
using CustomXpoProviders;

public class PreserveRelationshipsObjectSpaceProvider : XPObjectSpaceProvider {
    public PreserveRelationshipsObjectSpaceProvider(string connectionString) 
        : base(connectionString, null) { }
    
    protected override IDataLayer CreateDataLayer(IDataStore dataStore) {
        return new PreserveRelationshipsDataLayer(XPDictionary, dataStore) {
            PreserveRelationshipsOnSoftDelete = true
        };
    }
}
```

### 3. Register in XAF

**XAF 23.1+ (Application Builder):**
```csharp
builder.ObjectSpaceProviders.AddXpo()
    .UseCustomObjectSpaceProvider<PreserveRelationshipsObjectSpaceProvider>();
```

**Legacy XAF:**
```csharp
protected override void CreateDefaultObjectSpaceProvider(CreateCustomObjectSpaceProviderEventArgs args) {
    args.ObjectSpaceProvider = new PreserveRelationshipsObjectSpaceProvider(args.ConnectionString);
}
```

## ✅ Complete Example

```csharp
// Business Objects
[DeferredDeletion(true)]
public class Customer : BaseObject {
    public string Name { get; set; }
    
    [Association("Customer-Orders")]
    public XPCollection<Order> Orders { get; }
}

public class Order : BaseObject {
    public decimal Amount { get; set; }
    
    [Association("Customer-Orders")]
    public Customer Customer { get; set; }
}

// Usage in XAF
using (var os = Application.CreateObjectSpace()) {
    var customer = os.CreateObject<Customer>();
    customer.Name = "John";
    
    var order = os.CreateObject<Order>();
    order.Customer = customer;
    order.Amount = 1000;
    
    os.CommitChanges();
    
    // Soft delete customer
    os.Delete(customer);
    os.CommitChanges();
    
    // ✅ Order STILL has Customer reference!
    // (Standard XPO would set to NULL)
}
```

## 🎯 Key Differences

| Scenario | Standard XPO | CustomXpoProviders |
|----------|-------------|-------------------|
| Delete customer with orders | Sets `Order.Customer = NULL` | **Keeps `Order.Customer` set** ✅ |
| PurgeDeletedObjects() | Permanently deletes | Same behavior |
| Restore deleted object | Works | Works |
| Performance | Fast | Fast (minimal overhead) |

## 🔧 Configuration

```csharp
// Toggle at runtime
if (Application.ObjectSpaceProvider is PreserveRelationshipsObjectSpaceProvider custom) {
    custom.PreserveRelationshipsOnSoftDelete = false; // Standard XPO behavior
    custom.PreserveRelationshipsOnSoftDelete = true;  // Preserve relationships
}

// Per-transaction
var dataLayer = ((XPObjectSpace)objectSpace).Session.DataLayer;
if (dataLayer is PreserveRelationshipsDataLayer layer) {
    layer.PreserveRelationshipsOnSoftDelete = false; // Temporarily disable
}
```

## 📋 Checklist

Before using, ensure:
- ✅ Referenced `CustomXpoProviders.dll` (from `bin\Debug\net8.0\`)
- ✅ Created custom `XPObjectSpaceProvider` class
- ✅ Registered in XAF application
- ✅ Business objects have `[DeferredDeletion(true)]`
- ✅ Target framework is .NET 8.0
- ✅ DevExpress 25.1.* is installed

## ❓ FAQ

**Q: Will this break existing code?**  
A: No! It only changes DELETE behavior. All other operations work exactly the same.

**Q: Do I need to modify my database?**  
A: No! Works with existing database schema.

**Q: Can I use this with ThreadSafeDataLayer?**  
A: Not directly. `PreserveRelationshipsDataLayer` doesn't need threading wrapper for most scenarios.

**Q: Does it work with SecuritySystem?**  
A: Yes! Compatible with XAF Security System.

**Q: Performance impact?**  
A: Minimal. Only affects DELETE operations, uses efficient reflection.

## 📚 More Info

- **Full Guide:** `XAF_INTEGRATION_GUIDE.md`
- **Library Docs:** `README.md`
- **Current Status:** `STATUS.md`
- **XPO Internals:** `DeferredDeletionAnalysis.md`

## 🆘 Troubleshooting

**Problem:** "Cannot modify Dictionary because ThreadSafeDataLayer uses it"  
**Fix:** Don't wrap `PreserveRelationshipsDataLayer` in `ThreadSafeDataLayer`

**Problem:** Still getting NULL references  
**Fix:** Check that `PreserveRelationshipsOnSoftDelete = true` and `[DeferredDeletion(true)]` is on class

**Problem:** Compilation errors  
**Fix:** Ensure .NET 8.0 target framework and DevExpress 25.1.* packages

## 💡 Pro Tips

1. **Test First:** Try in a development environment before production
2. **Logging:** Enable XPO logging to see actual SQL statements
3. **Backup:** Always backup database before major changes
4. **Documentation:** Document which classes use deferred deletion

## 🎓 Example Project Structure

```
YourXafApp.Module/
├── BusinessObjects/
│   ├── Customer.cs
│   └── Order.cs
├── Providers/
│   └── PreserveRelationshipsObjectSpaceProvider.cs
└── YourXafAppModule.cs

YourXafApp.Win/
└── YourXafAppWindowsFormsApplication.cs (register provider here)

References/
└── CustomXpoProviders.dll ← Add this!
```

---

**Ready to use?** Follow the 3 steps at the top and you're done! 🎉
