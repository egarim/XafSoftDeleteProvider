using System;
using CustomXpoProviders;
using CustomXpoProviders.Examples;

namespace CustomXpoProviders {
    /// <summary>
    /// Simple console application to test the PreserveRelationships implementation
    /// </summary>
    class Program {
        static void Main(string[] args) {
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║   XPO Preserve Relationships During Soft Delete - Demo      ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            
            // Get connection string
            string connectionString = GetConnectionString(args);
            
            if(string.IsNullOrEmpty(connectionString)) {
                Console.WriteLine("No connection string provided.");
                Console.WriteLine();
                Console.WriteLine("Usage:");
                Console.WriteLine("  dotnet run -- \"Server=localhost;Database=testdb;User Id=postgres;Password=***\"");
                Console.WriteLine();
                Console.WriteLine("Or edit Program.cs to set a default connection string.");
                return;
            }
            
            Console.WriteLine($"Connection: {MaskPassword(connectionString)}");
            Console.WriteLine();
            
            // Show menu
            while(true) {
                Console.WriteLine("Select an option:");
                Console.WriteLine("  1. Run basic example (create, delete, verify, restore)");
                Console.WriteLine("  2. Run comparison test (standard XPO vs custom)");
                Console.WriteLine("  3. Run both");
                Console.WriteLine("  4. Exit");
                Console.WriteLine();
                Console.Write("Choice: ");
                
                var choice = Console.ReadLine();
                Console.WriteLine();
                
                try {
                    switch(choice) {
                        case "1":
                            PreserveRelationshipsExample.RunExample(connectionString);
                            break;
                            
                        case "2":
                            PreserveRelationshipsExample.RunComparisonExample(connectionString);
                            break;
                            
                        case "3":
                            PreserveRelationshipsExample.RunExample(connectionString);
                            Console.WriteLine("\n" + new string('=', 60) + "\n");
                            PreserveRelationshipsExample.RunComparisonExample(connectionString);
                            break;
                            
                        case "4":
                            Console.WriteLine("Exiting...");
                            return;
                            
                        default:
                            Console.WriteLine("Invalid choice. Please try again.");
                            break;
                    }
                }
                catch(Exception ex) {
                    Console.WriteLine();
                    Console.WriteLine("ERROR:");
                    Console.WriteLine(ex.Message);
                    Console.WriteLine();
                    Console.WriteLine("Stack trace:");
                    Console.WriteLine(ex.StackTrace);
                }
                
                Console.WriteLine();
                Console.WriteLine(new string('-', 60));
                Console.WriteLine();
            }
        }
        
        static string GetConnectionString(string[] args) {
            // 1. Check command line arguments
            if(args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])) {
                return args[0];
            }
            
            // 2. Check environment variable
            var envConnectionString = Environment.GetEnvironmentVariable("XPO_CONNECTION_STRING");
            if(!string.IsNullOrWhiteSpace(envConnectionString)) {
                return envConnectionString;
            }
            
            // 3. Default for testing (edit this for your environment)
            // Uncomment and modify the line below:
            // return "Server=localhost;Database=xpo_test;User Id=postgres;Password=yourpassword;";
            
            return null;
        }
        
        static string MaskPassword(string connectionString) {
            if(string.IsNullOrEmpty(connectionString))
                return connectionString;
                
            var parts = connectionString.Split(';');
            for(int i = 0; i < parts.Length; i++) {
                if(parts[i].Trim().StartsWith("Password=", StringComparison.OrdinalIgnoreCase)) {
                    parts[i] = "Password=***";
                }
            }
            return string.Join(";", parts);
        }
    }
}
