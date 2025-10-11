# Custom XPO Provider: Preserve Relationships During Soft Delete

This solution provides a custom XPO implementation that **preserves relationships** when performing soft deletes (deferred deletion). 

## The Problem

By default, when XPO performs a soft delete (deferred deletion):
1. Sets the `GCRecord` field to mark the object as deleted
2. **Removes the object from all association lists** (collections on the "many" side)
3. **Nullifies all association properties** (references on the "one" side)

This means you lose the relationship data, even though the object is still in the database.

## The Solution

Three approaches are provided, from simplest to most control:

### Approach 1: Custom Data Layer (Recommended - Simplest)

The `PreserveRelationshipsDataLayer` intercepts UPDATE statements at the database level and filters out those that nullify foreign keys during soft delete operations.

**Advantages:**
- Simple to use
- No need to change Session or UnitOfWork
- Works with existing XAF code
- Minimal code changes

**How it works:**
- Wraps your data store
- Intercepts `ModifyData` calls
- Filters UPDATE statements that set foreign keys to NULL when GCRecord is also being updated
- Allows GCRecord updates to proceed normally

### Approach 2: Custom Session

The `PreserveRelationshipsSession` overrides the delete behavior at the Session level.

**Advantages:**
- More control over delete behavior
- Can customize which relationships to preserve
- Works directly with XPO Session

**Disadvantages:**
- More complex
- Uses reflection to access protected methods
- May need updates if XPO internals change

### Approach 3: Custom XPObjectSpace (For XAF)

The `PreserveRelationshipsObjectSpaceProvider` creates custom ObjectSpace instances for XAF applications.

**Advantages:**
- Integrates seamlessly with XAF
- Easy to configure at application level
- Can toggle behavior per ObjectSpace

## Usage Examples

### Example 1: Using the Custom Data Layer (Simplest)

```csharp
using CustomXpoProviders;
using DevExpress.Xpo;

// For standalone XPO applications
string connectionString = "Your PostgreSQL connection string";

// Create a data layer that preserves relationships
IDataLayer dataLayer = PreserveRelationshipsDataLayerHelper.CreateDataLayer(
    connectionString, 
    preserveRelationships: true
);

// Use it like normal
using(UnitOfWork uow = new UnitOfWork(dataLayer)) {
    var customer = uow.GetObjectByKey<Customer>(customerId);
    
    // Delete the customer - GCRecord will be set, but relationships preserved!
    customer.Delete();
    
    uow.CommitChanges();
    
    // The customer's Orders collection still contains the orders
    // The foreign key Customer_Id in the Orders table is NOT nullified
}
```

### Example 2: Using with XAF

```csharp
using CustomXpoProviders;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;

public class YourApplication : WinApplication {
    protected override void CreateDefaultObjectSpaceProvider(CreateCustomObjectSpaceProviderEventArgs args) {
        // Option 1: Use custom data layer with standard XPObjectSpaceProvider
        var dataStore = XpoDefault.GetConnectionProvider(
            args.ConnectionString, 
            AutoCreateOption.DatabaseAndSchema
        );
        
        var dictionary = new ReflectionDictionary();
        dictionary.GetDataStoreSchema(/* your types */);
        
        var dataLayer = PreserveRelationshipsDataLayerHelper.CreateDataLayer(
            dataStore, 
            dictionary, 
            preserveRelationships: true
        );
        
        args.ObjectSpaceProvider = new XPObjectSpaceProvider(
            new DataStoreProvider(dataLayer)
        );
        
        // Option 2: Use custom ObjectSpaceProvider (more features)
        args.ObjectSpaceProvider = new PreserveRelationshipsObjectSpaceProvider(
            args.ConnectionString, 
            null
        ) {
            PreserveRelationshipsOnSoftDelete = true
        };
    }
}
```

### Example 3: Toggle Behavior Per Operation

```csharp
// Create provider with default behavior
var provider = new PreserveRelationshipsObjectSpaceProvider(connectionString, null) {
    PreserveRelationshipsOnSoftDelete = true  // Default: preserve
};

// For most operations, relationships are preserved
using(var os = provider.CreateObjectSpace()) {
    var customer = os.GetObjectByKey<Customer>(id);
    os.Delete(customer);
    os.CommitChanges();
    // Relationships preserved!
}

// For specific operations, you can disable preservation
using(var os = (PreserveRelationshipsObjectSpace)provider.CreateObjectSpace()) {
    os.PreserveRelationshipsOnSoftDelete = false;  // Override for this ObjectSpace
    
    var customer = os.GetObjectByKey<Customer>(id);
    os.Delete(customer);
    os.CommitChanges();
    // Relationships removed (standard XPO behavior)
}
```

### Example 4: Custom Session (Advanced)

```csharp
using CustomXpoProviders;

// Create a custom session
var session = new PreserveRelationshipsSession(dataLayer) {
    PreserveRelationshipsOnSoftDelete = true
};

var customer = session.GetObjectByKey<Customer>(customerId);
session.Delete(customer);
session.CommitChanges();

// Relationships are preserved
```

## Business Object Example

```csharp
using DevExpress.Xpo;

[DeferredDeletion(true)]  // Enable soft delete
public class Customer : XPObject {
    public Customer(Session session) : base(session) { }
    
    string name;
    public string Name {
        get => name;
        set => SetPropertyValue(nameof(Name), ref name, value);
    }
    
    [Association("Customer-Orders")]
    public XPCollection<Order> Orders {
        get => GetCollection<Order>(nameof(Orders));
    }
}

[DeferredDeletion(true)]  // Enable soft delete
public class Order : XPObject {
    public Order(Session session) : base(session) { }
    
    Customer customer;
    [Association("Customer-Orders")]
    public Customer Customer {
        get => customer;
        set => SetPropertyValue(nameof(Customer), ref customer, value);
    }
    
    decimal amount;
    public decimal Amount {
        get => amount;
        set => SetPropertyValue(nameof(Amount), ref amount, value);
    }
}
```

## What Gets Preserved vs Deleted

### With Standard XPO Soft Delete:
```sql
-- When you delete a Customer:
UPDATE Customer SET GCRecord = 1 WHERE OID = 123;
UPDATE Order SET Customer_Id = NULL WHERE Customer_Id = 123;  -- Relationships removed!
```

### With PreserveRelationships:
```sql
-- When you delete a Customer:
UPDATE Customer SET GCRecord = 1 WHERE OID = 123;
-- Order.Customer_Id is NOT changed - relationship preserved!
```

### What Still Happens:
1. **Aggregated objects**: Still deleted (as they should be)
2. **Many-to-many intermediate tables**: Still cleared (as they should be)
3. **GCRecord field**: Still set to mark deletion

### What Changes:
1. **Association properties**: NOT nullified
2. **Association collections**: Object NOT removed from related collections
3. **Foreign keys**: NOT set to NULL in database

## Querying Deleted Objects with Relationships

```csharp
// Get deleted customers with their orders still intact
var deletedCustomers = new XPCollection<Customer>(session) {
    SelectDeleted = true,
    Criteria = CriteriaOperator.Parse("GCRecord IS NOT NULL")
};

foreach(var customer in deletedCustomers) {
    Console.WriteLine($"Deleted Customer: {customer.Name}");
    
    // Orders collection still works!
    foreach(var order in customer.Orders) {
        Console.WriteLine($"  Order: {order.Amount}");
    }
}
```

## Benefits

1. **Audit Trail**: See what was related before deletion
2. **Data Recovery**: Restore objects with relationships intact
3. **Historical Analysis**: Query deleted objects with their relationships
4. **Soft Delete Reports**: Generate reports showing deleted entities and their connections
5. **Cascading Undelete**: When restoring, relationships are already there

## Restoring Objects

```csharp
// Find deleted customer
var customer = session.FindObject<Customer>(
    CriteriaOperator.Parse("GCRecord IS NOT NULL AND OID = ?", customerId)
);

if(customer != null) {
    // Clear GCRecord to "undelete"
    customer.SetMemberValue("GCRecord", null);
    session.CommitChanges();
    
    // Customer is active again, and all Orders are still related!
}
```

## Purging with Preserved Relationships

When you call `PurgeDeletedObjects()`, XPO will:
1. Find all objects with `GCRecord IS NOT NULL`
2. **First nullify the foreign keys** (using the `KillReferences` method)
3. Then physically delete the records

This works correctly even with preserved relationships because the purge process explicitly handles the relationship cleanup.

```csharp
var result = session.PurgeDeletedObjects();
Console.WriteLine($"Purged {result.Purged} objects");
```

## Important Notes

1. **Foreign Key Constraints**: If your database has foreign key constraints with `ON DELETE RESTRICT` or `ON DELETE NO ACTION`, you won't be able to purge deleted objects that are still referenced. Consider using `ON DELETE SET NULL` or handling this in your purge strategy.

2. **Aggregated Objects**: Aggregated child objects are still deleted immediately (as per XPO semantics), only non-aggregated relationships are preserved.

3. **Many-to-Many**: The intermediate table entries are still removed during soft delete, but the references from both sides remain intact.

4. **Performance**: Preserving relationships means deleted objects still occupy space and maintain references, which could impact query performance if you have many deleted objects. Plan your purge strategy accordingly.

## PostgreSQL Specific Considerations

### Foreign Key Constraints

If you're using PostgreSQL with foreign key constraints, consider:

```sql
-- Option 1: Allow NULL on delete (recommended)
ALTER TABLE "Order" 
DROP CONSTRAINT IF EXISTS FK_Order_Customer,
ADD CONSTRAINT FK_Order_Customer 
    FOREIGN KEY (Customer_Id) 
    REFERENCES Customer(OID) 
    ON DELETE SET NULL;

-- Option 2: No action (will prevent purge if references exist)
ALTER TABLE "Order" 
DROP CONSTRAINT IF EXISTS FK_Order_Customer,
ADD CONSTRAINT FK_Order_Customer 
    FOREIGN KEY (Customer_Id) 
    REFERENCES Customer(OID) 
    ON DELETE NO ACTION;
```

### Indexes

Since deleted objects remain in the database, ensure you have proper indexes:

```sql
-- XPO creates this automatically
CREATE INDEX IX_Customer_GCRecord ON Customer(GCRecord);

-- You might want additional indexes for querying deleted data
CREATE INDEX IX_Order_Customer_GCRecord 
    ON "Order"(Customer_Id) 
    WHERE Customer_Id IN (
        SELECT OID FROM Customer WHERE GCRecord IS NOT NULL
    );
```

## Complete Example: XAF Application with Preserved Relationships

```csharp
// In your XAF application startup (e.g., WinApplication.cs or Blazor startup)

using CustomXpoProviders;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;

public class MySolutionWindowsFormsApplication : WinApplication {
    
    protected override void CreateDefaultObjectSpaceProvider(CreateCustomObjectSpaceProviderEventArgs args) {
        // Use the custom provider that preserves relationships
        args.ObjectSpaceProvider = new PreserveRelationshipsObjectSpaceProvider(
            args.ConnectionString, 
            args.Connection
        ) {
            PreserveRelationshipsOnSoftDelete = true
        };
    }
}

// In a ViewController
public class CustomerViewController : ObjectViewController<DetailView, Customer> {
    
    protected override void OnActivated() {
        base.OnActivated();
        
        SimpleAction deleteWithRelationships = new SimpleAction(
            this, "DeletePreserveRelationships", PredefinedCategory.Edit
        );
        deleteWithRelationships.Execute += (s, e) => {
            // Delete the customer - relationships will be preserved!
            ObjectSpace.Delete(View.CurrentObject);
            ObjectSpace.CommitChanges();
            
            Application.ShowViewStrategy.ShowMessage(
                "Customer deleted, but relationships preserved!",
                InformationType.Success
            );
        };
    }
}
```

## Files Included

1. **PreserveRelationshipsDataLayer.cs** - Data layer that intercepts and filters UPDATE statements
2. **PreserveRelationshipsSession.cs** - Custom Session with overridden delete behavior
3. **PreserveRelationshipsObjectSpace.cs** - XAF ObjectSpace and ObjectSpaceProvider

Choose the approach that best fits your needs!
