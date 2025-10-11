# PostgreSQL XPO Provider with Preserved Relationships - Quick Start

## What This Does

This custom XPO implementation prevents relationship removal during soft deletes. When you delete an object with `[DeferredDeletion(true)]`:

**Standard XPO:**
- ✓ Sets GCRecord field
- ✗ Removes object from collections
- ✗ Nullifies foreign key references

**Custom Implementation:**
- ✓ Sets GCRecord field  
- ✓ Keeps object in collections
- ✓ Preserves foreign key references

## Quick Start - PostgreSQL + XAF

### 1. Add Files to Your Project

Copy these files to your XAF solution:
- `PreserveRelationshipsDataLayer.cs`
- `PreserveRelationshipsObjectSpace.cs` (if using XAF)

### 2. Update Your Application Startup

```csharp
// In your WinApplication.cs, WebApplication.cs, or Blazor startup
using CustomXpoProviders;

protected override void CreateDefaultObjectSpaceProvider(CreateCustomObjectSpaceProviderEventArgs args) {
    // Use custom provider
    args.ObjectSpaceProvider = new PreserveRelationshipsObjectSpaceProvider(
        args.ConnectionString, 
        null
    ) {
        PreserveRelationshipsOnSoftDelete = true
    };
}
```

### 3. Enable Deferred Deletion on Your Business Objects

```csharp
[DefaultClassOptions]
[DeferredDeletion(true)]  // This enables soft delete
public class Customer : BaseObject {
    // ... your properties ...
    
    [Association("Customer-Orders")]
    public XPCollection<Order> Orders {
        get => GetCollection<Order>(nameof(Orders));
    }
}
```

### 4. That's It!

Now when you delete objects:
```csharp
ObjectSpace.Delete(customer);
ObjectSpace.CommitChanges();

// Customer is marked deleted (GCRecord set)
// BUT relationships to Orders are preserved!
```

## Quick Start - Standalone XPO (Non-XAF)

### 1. Create Data Layer

```csharp
using CustomXpoProviders;

string connectionString = "Server=localhost;Database=mydb;User Id=postgres;Password=***;";

IDataLayer dataLayer = PreserveRelationshipsDataLayerHelper.CreateDataLayer(
    connectionString,
    preserveRelationships: true
);
```

### 2. Use Normally

```csharp
using(var uow = new UnitOfWork(dataLayer)) {
    var customer = uow.GetObjectByKey<Customer>(id);
    
    customer.Delete();  // Soft delete with preserved relationships
    
    uow.CommitChanges();
}
```

## Querying Deleted Objects

```csharp
// Get deleted objects
var deleted = new XPCollection<Customer>(session) {
    SelectDeleted = true,
    Criteria = CriteriaOperator.Parse("GCRecord IS NOT NULL")
};

foreach(var customer in deleted) {
    // Relationships still work!
    Console.WriteLine($"{customer.Name} had {customer.Orders.Count} orders");
}
```

## Restoring Deleted Objects

```csharp
// Undelete by clearing GCRecord
customer.SetMemberValue("GCRecord", null);
objectSpace.CommitChanges();

// Customer is active again with all relationships intact!
```

## Purging (Permanent Delete)

```csharp
// Physically remove soft-deleted objects
var result = session.PurgeDeletedObjects();

Console.WriteLine($"Purged {result.Purged} objects");
```

## PostgreSQL Database Setup

### Option 1: Let Foreign Keys Be Nullified During Purge
```sql
ALTER TABLE "Order" 
DROP CONSTRAINT IF EXISTS FK_Order_Customer,
ADD CONSTRAINT FK_Order_Customer 
    FOREIGN KEY ("Customer") 
    REFERENCES "Customer"("OID") 
    ON DELETE SET NULL;
```

### Option 2: Prevent Purge if References Exist
```sql
ALTER TABLE "Order" 
DROP CONSTRAINT IF EXISTS FK_Order_Customer,
ADD CONSTRAINT FK_Order_Customer 
    FOREIGN KEY ("Customer") 
    REFERENCES "Customer"("OID") 
    ON DELETE NO ACTION;
```

## Files Overview

| File | Purpose |
|------|---------|
| `PreserveRelationshipsDataLayer.cs` | Core implementation - intercepts UPDATE statements |
| `PreserveRelationshipsObjectSpace.cs` | XAF integration - custom ObjectSpace/Provider |
| `PreserveRelationshipsSession.cs` | Advanced - custom Session (alternative approach) |
| `Examples.cs` | Working examples and demos |
| `README.md` | Comprehensive documentation |
| `QUICKSTART.md` | This file - quick reference |

## Common Scenarios

### Scenario 1: Audit Trail
Keep track of what was related before deletion:
```csharp
// View deleted customer with order history
var deletedCustomer = session.FindObject<Customer>(
    CriteriaOperator.Parse("GCRecord IS NOT NULL AND Oid = ?", id),
    selectDeleted: true
);

// Generate audit report
Console.WriteLine($"Deleted: {deletedCustomer.Name}");
Console.WriteLine($"Had {deletedCustomer.Orders.Count} orders totaling ${deletedCustomer.Orders.Sum(o => o.Amount)}");
```

### Scenario 2: Data Recovery
Restore with relationships intact:
```csharp
// Find and restore
var customer = FindDeletedCustomer(id);
customer.SetMemberValue("GCRecord", null);
objectSpace.CommitChanges();

// All orders automatically reconnected!
```

### Scenario 3: Historical Analysis
Analyze deleted entities:
```csharp
// Which deleted customers had the most orders?
var deletedCustomers = new XPCollection<Customer>(session) {
    SelectDeleted = true,
    Criteria = CriteriaOperator.Parse("GCRecord IS NOT NULL")
};

var topCustomers = deletedCustomers
    .OrderByDescending(c => c.Orders.Count)
    .Take(10);
```

## Troubleshooting

### Foreign Key Constraint Violations During Purge
**Problem:** Can't purge because of foreign key constraints

**Solution:** 
1. Use `ON DELETE SET NULL` in your FK constraints, OR
2. Delete related objects first before purging, OR
3. Disable FK constraints temporarily during purge (not recommended)

### Relationships Appearing Null
**Problem:** After delete, relationships show as null

**Solution:** Make sure you're using `SelectDeleted = true` when querying deleted objects:
```csharp
var deleted = new XPCollection<Customer>(session) {
    SelectDeleted = true  // Important!
};
```

### Objects Not Being Soft Deleted
**Problem:** Objects are being hard-deleted instead of soft-deleted

**Solution:** Ensure `[DeferredDeletion(true)]` is on your class:
```csharp
[DeferredDeletion(true)]  // Add this!
public class MyClass : XPObject {
```

## Performance Considerations

1. **Indexes**: XPO automatically creates an index on GCRecord
2. **Database Size**: Deleted objects remain until purged - plan purge strategy
3. **Query Performance**: Filter out deleted objects in WHERE clauses when needed:
   ```sql
   WHERE GCRecord IS NULL  -- Only active objects
   ```

## Need Help?

- See `README.md` for detailed documentation
- Check `Examples.cs` for working code samples
- Review the XPO deferred deletion analysis document

## Version Compatibility

- DevExpress XPO: 20.1+
- DevExpress XAF: 20.1+
- PostgreSQL: 9.6+
- Npgsql: 4.0+
- .NET: .NET Framework 4.6.2+ or .NET 6.0+
