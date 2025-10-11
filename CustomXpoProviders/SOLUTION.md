# CustomXpoProviders Solution

This solution contains a class library and test project for preserving relationships during soft delete in DevExpress XPO.

## Projects

### CustomXpoProviders (Class Library)
The main library containing the implementation.

**Location:** `CustomXpoProviders/`

**Key Files:**
- `PreserveRelationshipsDataLayer.cs` - Core implementation
- `PreserveRelationshipsObjectSpace.cs` - XAF integration
- `PreserveRelationshipsSession.cs` - Advanced session

**Documentation:** See `CustomXpoProviders/INDEX.md`

### CustomXpoProviders.Tests (NUnit Test Project)
Comprehensive tests for the library.

**Location:** `CustomXpoProviders.Tests/`

**Test Files:**
- `PreserveRelationshipsDataLayerTests.cs` - Core functionality tests
- `ComparisonTests.cs` - Standard XPO vs Custom comparison
- `TestBusinessObjects.cs` - Test business classes

**Documentation:** See `CustomXpoProviders.Tests/README.md`

## Building the Solution

```bash
# Build everything
dotnet build

# Build library only
cd CustomXpoProviders
dotnet build

# Build tests only
cd CustomXpoProviders.Tests
dotnet build
```

## Running Tests

```bash
# From solution root
dotnet test

# From test project
cd CustomXpoProviders.Tests
dotnet test

# With detailed output
dotnet test --logger "console;verbosity=detailed"

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Project Structure

```
XpoSource/
├── CustomXpoProviders/                    [Class Library]
│   ├── PreserveRelationshipsDataLayer.cs
│   ├── PreserveRelationshipsObjectSpace.cs
│   ├── PreserveRelationshipsSession.cs
│   ├── Examples.cs
│   ├── CustomXpoProviders.csproj
│   ├── INDEX.md
│   ├── README.md
│   ├── QUICKSTART.md
│   ├── SOLUTION_OVERVIEW.md
│   ├── IMPLEMENTATION_SUMMARY.md
│   ├── LIBRARY_README.md
│   └── Program.cs (excluded from build)
│
└── CustomXpoProviders.Tests/              [Test Project]
    ├── PreserveRelationshipsDataLayerTests.cs
    ├── ComparisonTests.cs
    ├── TestBusinessObjects.cs
    ├── TestConfiguration.cs
    ├── GlobalUsings.cs
    ├── CustomXpoProviders.Tests.csproj
    ├── README.md
    └── .gitignore
```

## Quick Start

### 1. For Library Users

Add reference to your project:
```xml
<ItemGroup>
  <ProjectReference Include="..\CustomXpoProviders\CustomXpoProviders.csproj" />
</ItemGroup>
```

Use in code:
```csharp
using CustomXpoProviders;

var dataLayer = PreserveRelationshipsDataLayerHelper.CreateDataLayer(
    connectionString, true
);
```

### 2. For Developers/Contributors

Run tests:
```bash
cd CustomXpoProviders.Tests
dotnet test
```

### 3. For Documentation

Start here: `CustomXpoProviders/INDEX.md`

## Configuration

### Test Configuration

Tests use in-memory database by default. For PostgreSQL:

```bash
# Windows PowerShell
$env:PostgresSoftDelete = "Server=localhost;Database=xpo_test;..."
dotnet test

# Linux/Mac
export PostgresSoftDelete="Server=localhost;Database=xpo_test;..."
dotnet test
```

## CI/CD

### GitHub Actions Example

```yaml
name: Build and Test

on: [push, pull_request]

jobs:
  build:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '6.0.x'
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --no-restore
    
    - name: Test
      run: dotnet test --no-build --verbosity normal
```

### With PostgreSQL Service

```yaml
services:
  postgres:
    image: postgres:14
    env:
      POSTGRES_PASSWORD: testpassword
      POSTGRES_DB: xpo_test
    ports:
      - 5432:5432
    options: >-
      --health-cmd pg_isready
      --health-interval 10s
      --health-timeout 5s
      --health-retries 5

steps:
  # ... other steps ...
  
  - name: Test with PostgreSQL
    env:
      PostgresSoftDelete: "Server=localhost;Database=xpo_test;User Id=postgres;Password=testpassword;"
    run: dotnet test --verbosity normal
```

## NuGet Package (Optional)

To create a NuGet package:

```bash
cd CustomXpoProviders
dotnet pack -c Release
```

The package will be in `bin/Release/CustomXpoProviders.1.0.0.nupkg`

## Version History

### v1.0.0
- Initial release
- Core PreserveRelationshipsDataLayer implementation
- XAF ObjectSpace integration
- Advanced Session implementation
- Comprehensive test suite
- Complete documentation

## Support

- **Documentation:** `CustomXpoProviders/INDEX.md`
- **Tests:** `CustomXpoProviders.Tests/`
- **Examples:** `CustomXpoProviders/Examples.cs`

## License

Custom code for your project. Modify as needed.
