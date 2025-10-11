namespace CustomXpoProviders.Tests;

/// <summary>
/// Configuration for tests - set your connection string here
/// </summary>
public static class TestConfiguration {
    
    /// <summary>
    /// PostgreSQL connection string for tests.
    /// Override with environment variable: PostgresSoftDelete
    /// </summary>
    public static string GetConnectionString() {
        // 1. Try environment variable first
        var envConnectionString = Environment.GetEnvironmentVariable("PostgresSoftDelete");
        envConnectionString = "XpoProvider=Postgres;Server=127.0.0.1;User ID=postgres;Password=1234567890;Database=PostgresSoftDelete;Encoding=UNICODE";
        if (!string.IsNullOrWhiteSpace(envConnectionString)) {
            return envConnectionString;
        }
        
        // 2. Fall back to in-memory SQLite for unit tests (if you don't have PostgreSQL)
        // Note: Some features may not work exactly the same with SQLite
        return "XpoProvider=InMemoryDataStore";
        
        // 3. Uncomment and modify this for PostgreSQL testing:
        // return "Server=localhost;Database=xpo_test;User Id=postgres;Password=yourpassword;";
    }
    
    /// <summary>
    /// Check if we're using a real database or in-memory
    /// </summary>
    public static bool IsInMemoryDatabase => 
        GetConnectionString().Contains("InMemoryDataStore", StringComparison.OrdinalIgnoreCase);
}
