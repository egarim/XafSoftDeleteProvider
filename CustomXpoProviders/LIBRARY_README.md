# CustomXpoProviders - Class Library

A class library providing custom XPO implementations that preserve relationships during soft delete (deferred deletion) operations.

## Project Type

This is now a **Class Library** (not a console application). 

## What's Included

### Core Components

| File | Description |
|------|-------------|
| `PreserveRelationshipsDataLayer.cs` | Main implementation - intercepts database UPDATE statements |
| `PreserveRelationshipsObjectSpace.cs` | XAF integration - custom ObjectSpace and provider |
| `PreserveRelationshipsSession.cs` | Advanced - custom Session implementation |
| `Examples.cs` | Example code for documentation (not executable) |

### Documentation

| File | Purpose |
|------|---------|
| `README.md` | Complete documentation |
| `QUICKSTART.md` | Quick start guide |
| `SOLUTION_OVERVIEW.md` | Overview and summary |
| `IMPLEMENTATION_SUMMARY.md` | Technical details |
| `INDEX.md` | Documentation navigation |

## Usage

### Add to Your Project

**Via Project Reference:**
```xml
<ItemGroup>
  <ProjectReference Include="..\CustomXpoProviders\CustomXpoProviders.csproj" />
</ItemGroup>
```

**Or copy files directly to your solution.**

### Basic Usage

```csharp
using CustomXpoProviders;

// Create data layer
var dataLayer = PreserveRelationshipsDataLayerHelper.CreateDataLayer(
    connectionString,
    preserveRelationships: true
);

// Use with UnitOfWork
using var uow = new UnitOfWork(dataLayer);
var customer = uow.GetObjectByKey<Customer>(id);
customer.Delete(); // Soft delete with preserved relationships
uow.CommitChanges();
```

### XAF Usage

```csharp
using CustomXpoProviders;

protected override void CreateDefaultObjectSpaceProvider(CreateCustomObjectSpaceProviderEventArgs args) {
    args.ObjectSpaceProvider = new PreserveRelationshipsObjectSpaceProvider(
        args.ConnectionString, null
    ) {
        PreserveRelationshipsOnSoftDelete = true
    };
}
```

## Building

```bash
dotnet build
```

## Testing

See the **CustomXpoProviders.Tests** project for unit tests.

```bash
cd ..\CustomXpoProviders.Tests
dotnet test
```

## Dependencies

- DevExpress.Xpo 24.1+
- DevExpress.ExpressApp.Xpo 24.1+ (for XAF integration)
- Npgsql 8.0+ (PostgreSQL provider)
- .NET 6.0+

## Documentation

Start with [`INDEX.md`](INDEX.md) for complete documentation navigation.

Quick links:
- [Quick Start Guide](QUICKSTART.md)
- [Complete Documentation](README.md)
- [Solution Overview](SOLUTION_OVERVIEW.md)

## License

Custom code for your project. Modify as needed.
