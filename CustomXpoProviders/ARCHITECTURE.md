# XAF Integration Architecture

## How It Works: CustomXpoProviders in XAF

```
┌─────────────────────────────────────────────────────────────────────┐
│                         XAF Application Layer                        │
│                                                                       │
│  ┌─────────────────┐     ┌──────────────────┐                       │
│  │  Controllers    │     │   Detail Views   │                       │
│  │  List Views     │────▶│   List Views     │                       │
│  └─────────────────┘     └──────────────────┘                       │
└────────────────────────────────┬─────────────────────────────────────┘
                                 │
                    objectSpace.Delete(customer)
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      IObjectSpace Interface                          │
│                                                                       │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │         XPObjectSpace (from XPObjectSpaceProvider)           │   │
│  │                                                               │   │
│  │  ┌────────────────────────────────────────────────────────┐  │   │
│  │  │  PreserveRelationshipsObjectSpaceProvider              │  │   │
│  │  │  (Your Custom Provider)                                │  │   │
│  │  │                                                         │  │   │
│  │  │  protected override IDataLayer CreateDataLayer() {     │  │   │
│  │  │      return new PreserveRelationshipsDataLayer(...);   │  │   │
│  │  │  }                                                      │  │   │
│  │  └────────────────────────────────────────────────────────┘  │   │
│  └──────────────────────────────────────────────────────────────┘   │
└────────────────────────────────┬─────────────────────────────────────┘
                                 │
                         session.Delete(obj)
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│                        XPO Session Layer                             │
│                                                                       │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │                      UnitOfWork / Session                     │   │
│  │                                                                │   │
│  │  • Tracks object changes                                      │   │
│  │  • Manages object lifecycle                                   │   │
│  │  • Calls dataLayer.ModifyData(statements)                     │   │
│  └──────────────────────────────────────────────────────────────┘   │
└────────────────────────────────┬─────────────────────────────────────┘
                                 │
                    dataLayer.ModifyData(statements)
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    🎯 Custom Data Layer (YOU ARE HERE!)              │
│                                                                       │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │        PreserveRelationshipsDataLayer                         │   │
│  │        (from CustomXpoProviders.dll)                          │   │
│  │                                                                │   │
│  │  public override ModificationResult ModifyData(               │   │
│  │      params ModificationStatement[] statements) {             │   │
│  │                                                                │   │
│  │    ┌────────────────────────────────────────────────┐         │   │
│  │    │ 1. Inspect each statement using reflection     │         │   │
│  │    │ 2. Identify soft delete (GCRecord update)      │         │   │
│  │    │ 3. Filter out NULL assignments to FKs          │         │   │
│  │    │ 4. Keep only GCRecord update                   │         │   │
│  │    └────────────────────────────────────────────────┘         │   │
│  │                                                                │   │
│  │    return base.ModifyData(filteredStatements);                │   │
│  │  }                                                             │   │
│  └──────────────────────────────────────────────────────────────┘   │
└────────────────────────────────┬─────────────────────────────────────┘
                                 │
                  Base.ModifyData(filtered statements)
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      SimpleDataLayer (Base)                          │
│                                                                       │
│  • Converts statements to SQL                                        │
│  • Sends to IDataStore                                               │
└────────────────────────────────┬─────────────────────────────────────┘
                                 │
                         Execute SQL
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     IDataStore (Database Provider)                   │
│                                                                       │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │  PostgreSqlConnectionProvider / MSSqlConnectionProvider      │   │
│  │                                                                │   │
│  │  • Executes SQL: UPDATE Customer SET GCRecord = 1            │   │
│  │  • (NO NULL assignments sent!)                               │   │
│  └──────────────────────────────────────────────────────────────┘   │
└────────────────────────────────┬─────────────────────────────────────┘
                                 │
                                 ▼
                        ┌─────────────────┐
                        │    PostgreSQL    │
                        │    SQL Server    │
                        │    Database      │
                        └─────────────────┘
```

## What Gets Filtered Out

### Standard XPO Behavior
```sql
-- When deleting Customer (standard XPO):
UPDATE Customer SET GCRecord = 1 WHERE OID = 123;          -- Soft delete marker
UPDATE Order SET Customer = NULL WHERE Customer = 123;     -- ❌ Removes relationship
UPDATE Invoice SET Customer = NULL WHERE Customer = 123;   -- ❌ Removes relationship
```

### CustomXpoProviders Behavior
```sql
-- When deleting Customer (CustomXpoProviders):
UPDATE Customer SET GCRecord = 1 WHERE OID = 123;          -- ✅ Soft delete marker
-- ✅ NULL assignments are FILTERED OUT!
-- Order.Customer and Invoice.Customer remain unchanged!
```

## Data Flow Diagram

```
User Action: Delete Customer
        │
        ├──► XAF Controller receives command
        │
        ├──► ObjectSpace.Delete(customer)
        │
        ├──► XPO Session prepares modification statements:
        │    ┌─────────────────────────────────────────┐
        │    │ Statement 1: UPDATE Customer            │
        │    │              SET GCRecord = 1           │
        │    │                                         │
        │    │ Statement 2: UPDATE Order               │
        │    │              SET Customer = NULL        │
        │    │                                         │
        │    │ Statement 3: UPDATE Invoice             │
        │    │              SET Customer = NULL        │
        │    └─────────────────────────────────────────┘
        │
        ├──► PreserveRelationshipsDataLayer.ModifyData()
        │    │
        │    ├──► Reflection inspection:
        │    │    • Is this an UPDATE? ✓
        │    │    • Does it set GCRecord? ✓ (keep it)
        │    │    • Does it set FK to NULL? ✗ (filter it out)
        │    │
        │    └──► Filtered statements:
        │         ┌─────────────────────────────────┐
        │         │ Statement 1: UPDATE Customer    │
        │         │              SET GCRecord = 1   │
        │         │                                 │
        │         │ (Statements 2 & 3 removed!)     │
        │         └─────────────────────────────────┘
        │
        ├──► SimpleDataLayer.ModifyData()
        │
        ├──► PostgreSQL/SQL Server executes:
        │    UPDATE Customer SET GCRecord = 1
        │
        └──► Result: Customer soft-deleted, 
             relationships preserved! ✅
```

## Class Hierarchy

```
IDataLayer (interface)
    │
    ├── BaseDataLayer (abstract)
    │       │
    │       ├── SimpleDataLayer ◄─────┐
    │       │                          │
    │       │                          │ Extends
    │       ├── ThreadSafeDataLayer    │
    │       │                          │
    │       └── PreserveRelationshipsDataLayer ✅ (Your custom layer)
    │
    └── Used by XPObjectSpaceProvider
```

## Integration Points

### 1. Application Startup
```
XAF Application.CreateDefaultObjectSpaceProvider()
    │
    └──► new PreserveRelationshipsObjectSpaceProvider(connectionString)
            │
            └──► Stores for later use
```

### 2. Creating ObjectSpace
```
Application.CreateObjectSpace()
    │
    └──► XPObjectSpaceProvider.CreateObjectSpace()
            │
            ├──► CreateDataLayer(dataStore) ◄── Your override!
            │       │
            │       └──► new PreserveRelationshipsDataLayer(...)
            │
            └──► new XPObjectSpace(dataLayer)
```

### 3. Delete Operation
```
ObjectSpace.Delete(customer)
    │
    └──► Session.Delete(customer)
            │
            ├──► Mark as deleted
            │
            └──► On CommitChanges():
                    │
                    └──► DataLayer.ModifyData(statements) ◄── Interception point!
```

## Key Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `PreserveRelationshipsDataLayer` | CustomXpoProviders.dll | Core logic - filters statements |
| `PreserveRelationshipsObjectSpaceProvider` | Your XAF project | Integrates custom layer into XAF |
| `XPObjectSpace` | DevExpress.ExpressApp.Xpo | XAF's ObjectSpace implementation |
| `SimpleDataLayer` | DevExpress.Xpo | Base data layer class |
| `IDataStore` | DevExpress.Xpo.DB | Database provider interface |

## Execution Timeline

```
Time ──────────────────────────────────────────────────────────────────▶

1. App Start
   └─ Register PreserveRelationshipsObjectSpaceProvider

2. User Opens View
   └─ CreateObjectSpace()
       └─ CreateDataLayer() called
           └─ PreserveRelationshipsDataLayer instantiated

3. User Clicks "Delete"
   └─ ObjectSpace.Delete(customer)
       └─ Session marks for deletion

4. User Clicks "Save" / Auto-save triggers
   └─ ObjectSpace.CommitChanges()
       └─ Session.CommitChanges()
           └─ Generate SQL statements
               └─ ModifyData(statements) ◄── INTERCEPTION HERE
                   └─ Filter statements
                       └─ Execute filtered SQL
                           └─ Database updated ✅

5. View Refreshes
   └─ Query database
       └─ Order.Customer still set! ✅
```

## Summary

The custom data layer sits between XPO's Session layer and the database, intercepting modification statements and filtering out unwanted NULL assignments. This happens transparently to your XAF application - no code changes needed in controllers, views, or business objects!

