# XAF Sample Applications

This folder contains example XAF (DevExpress eXpressApp Framework) applications that demonstrate how to use the `CustomXpoProviders` library in real XAF projects.

## Projects

### XafSoftDelete.Module
- **Type:** XAF Module (shared business logic)
- **Purpose:** Contains business objects and shared XAF module configuration
- **Dependencies:** DevExpress.ExpressApp packages

### XafSoftDelete.Win
- **Type:** Windows Forms XAF Application
- **Purpose:** Desktop application demonstrating soft delete with preserved relationships
- **Dependencies:** DevExpress.ExpressApp.Win packages

### XafSoftDelete.Blazor.Server
- **Type:** Blazor Server XAF Application
- **Purpose:** Web application demonstrating soft delete with preserved relationships
- **Dependencies:** DevExpress.ExpressApp.Blazor packages

## Setup Requirements

These projects require DevExpress XAF packages (version 25.1.*) which are not included in the CustomXpoProviders library. To use these samples:

1. **Install DevExpress XAF** - Ensure you have DevExpress Universal subscription with XAF
2. **Restore NuGet Packages** - Run `dotnet restore` in each project folder
3. **Reference CustomXpoProviders** - Add project reference to the CustomXpoProviders library

## Using CustomXpoProviders in XAF

The key integration point is in your XAF application startup where you configure the XPObjectSpaceProvider:

```csharp
using CustomXpoProviders;
using DevExpress.ExpressApp.Xpo;

// In your XAF application setup (Program.cs or Startup.cs)

// Create custom XPObjectSpaceProvider
var connectionString = "your-postgres-connection-string";
var objectSpaceProvider = new CustomXPObjectSpaceProvider(connectionString);

// Use it in your XAF application
winApplication.CreateCustomObjectSpaceProvider += (s, e) => {
    e.ObjectSpaceProvider = objectSpaceProvider;
};
```

### Custom XPObjectSpaceProvider Example

```csharp
public class CustomXPObjectSpaceProvider : XPObjectSpaceProvider
{
    public CustomXPObjectSpaceProvider(string connectionString) 
        : base(connectionString, null)
    {
    }

    protected override IDataLayer CreateDataLayer(XPDictionary dictionary, IDataStore dataStore)
    {
        // Use PreserveRelationshipsDataLayer instead of default
        return new PreserveRelationshipsDataLayer(dictionary, dataStore)
        {
            PreserveRelationshipsOnSoftDelete = true
        };
    }
}
```

## Documentation

For complete integration instructions, see:
- [XAF Integration Guide](../CustomXpoProviders/XAF_INTEGRATION_GUIDE.md)
- [XAF Quick Reference](../CustomXpoProviders/XAF_QUICK_REFERENCE.md)
- [Architecture Overview](../CustomXpoProviders/ARCHITECTURE.md)

## Note

These are **example projects** to demonstrate integration patterns. You may need to:
- Update package versions to match your DevExpress installation
- Adjust connection strings
- Add your own business logic
- Configure security settings

For production use, adapt these examples to your specific requirements.
