# CustomXpoProviders.Tests

NUnit test project for the CustomXpoProviders library.

## Running Tests

### Using Visual Studio
1. Open Test Explorer (Test > Test Explorer)
2. Click "Run All Tests"

### Using Command Line
```bash
cd CustomXpoProviders.Tests
dotnet test
```

### With Coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Configuration

By default, tests run against an **in-memory database** (InMemoryDataStore) which doesn't require any external setup.

### To Test Against PostgreSQL

Set the environment variable:

**Windows (PowerShell):**
```powershell
$env:PostgresSoftDelete = "Server=localhost;Database=xpo_test;User Id=postgres;Password=yourpassword;"
dotnet test
```

**Windows (CMD):**
```cmd
set PostgresSoftDelete=Server=localhost;Database=xpo_test;User Id=postgres;Password=yourpassword;
dotnet test
```

**Linux/Mac:**
```bash
export PostgresSoftDelete="Server=localhost;Database=xpo_test;User Id=postgres;Password=yourpassword;"
dotnet test
```

### Alternative: Edit TestConfiguration.cs

You can also uncomment and modify the connection string in `TestConfiguration.cs`:

```csharp
// return "Server=localhost;Database=xpo_test;User Id=postgres;Password=yourpassword;";
```

## Test Structure

### Test Files

| File | Purpose |
|------|---------|
| `TestBusinessObjects.cs` | Business objects for testing (Customer, Order) |
| `TestConfiguration.cs` | Test configuration and connection strings |
| `PreserveRelationshipsDataLayerTests.cs` | Core functionality tests |
| `ComparisonTests.cs` | Standard XPO vs Custom implementation |

### Test Categories

#### PreserveRelationshipsDataLayerTests
- ✅ Soft delete sets GCRecord
- ✅ Relationships are preserved after soft delete
- ✅ Deleted objects filtered from standard queries
- ✅ Deleted objects can be restored
- ✅ Multiple relationships preserved
- ✅ Purge removes soft-deleted objects

#### ComparisonTests
- ✅ Standard XPO nullifies relationships
- ✅ Custom implementation preserves relationships
- ✅ Both implementations set GCRecord correctly

## Test Coverage

Current test coverage includes:
- ✅ Basic soft delete operations
- ✅ Relationship preservation
- ✅ Query filtering (SelectDeleted)
- ✅ Object restoration (undelete)
- ✅ Multiple relationships
- ✅ Purge operations
- ✅ Comparison with standard XPO behavior

## Adding New Tests

Create a new test file in this project:

```csharp
namespace CustomXpoProviders.Tests;

[TestFixture]
public class MyNewTests {
    
    [Test]
    public void MyTest() {
        // Arrange
        var dataLayer = PreserveRelationshipsDataLayerHelper.CreateDataLayer(
            TestConfiguration.GetConnectionString(),
            preserveRelationships: true
        );
        
        // Act
        // ... your test code ...
        
        // Assert
        Assert.That(result, Is.Not.Null);
    }
}
```

## Troubleshooting

### Tests Fail with Connection Errors
- Check if PostgreSQL is running (if using PostgreSQL)
- Verify connection string is correct
- Try using in-memory mode (default)

### Tests Fail with Schema Errors
- Ensure database user has CREATE TABLE permissions
- Check if database exists
- Verify AutoCreateOption is set correctly

### In-Memory Tests Behave Differently
- Some features work differently in InMemoryDataStore vs PostgreSQL
- For full testing, use PostgreSQL
- In-memory is good for quick unit tests

## Continuous Integration

For CI/CD pipelines, use in-memory mode:

```yaml
# Example GitHub Actions
- name: Run Tests
  run: dotnet test --logger "console;verbosity=detailed"
```

For PostgreSQL in CI:

```yaml
services:
  postgres:
    image: postgres:14
    env:
      POSTGRES_PASSWORD: testpassword
      POSTGRES_DB: xpo_test
    ports:
      - 5432:5432

- name: Run Tests
  env:
    XPO_TEST_CONNECTION_STRING: "Server=localhost;Database=xpo_test;User Id=postgres;Password=testpassword;"
  run: dotnet test
```

## Performance Tests

To add performance tests, create a new file `PerformanceTests.cs`:

```csharp
[TestFixture]
public class PerformanceTests {
    
    [Test]
    [Category("Performance")]
    public void BulkDelete_ShouldBePerformant() {
        // Test bulk operations
    }
}
```

Run only performance tests:
```bash
dotnet test --filter "Category=Performance"
```

## Test Output

Tests produce output showing:
- Number of tests run
- Pass/fail status
- Execution time
- Coverage (if enabled)

Example:
```
Passed!  - Failed:     0, Passed:    10, Skipped:     0, Total:    10
```
