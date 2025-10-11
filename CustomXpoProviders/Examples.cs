using System;
using System.Linq;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;

namespace CustomXpoProviders.Examples {
    
    // NOTE: Example business objects moved to CustomXpoProviders.Tests project
    // This file now contains only demonstration code for documentation purposes
    
    /// <summary>
    /// Demonstrates the difference between standard XPO soft delete
    /// and the custom PreserveRelationships implementation
    /// </summary>
    public class PreserveRelationshipsExample {
        
        public static void RunExample(string connectionString) {
            Console.WriteLine("=== Preserve Relationships During Soft Delete Example ===\n");
            
            // Create data layer that preserves relationships
            var dataLayer = PreserveRelationshipsDataLayerHelper.CreateDataLayer(
                connectionString, 
                preserveRelationships: true
            );
            
            // Setup schema
            using(var uow = new UnitOfWork(dataLayer)) {
                uow.UpdateSchema(typeof(Customer), typeof(Order));
                uow.CreateObjectTypeRecords(typeof(Customer), typeof(Order));
            }
            
            int customerId;
            
            // Step 1: Create test data
            Console.WriteLine("Step 1: Creating test data...");
            using(var uow = new UnitOfWork(dataLayer)) {
                var customer = new Customer(uow) {
                    Name = "Acme Corporation",
                    Email = "contact@acme.com"
                };
                
                new Order(uow) {
                    Customer = customer,
                    Amount = 1500.00m,
                    OrderDate = DateTime.Now.AddDays(-30)
                };
                
                new Order(uow) {
                    Customer = customer,
                    Amount = 2300.50m,
                    OrderDate = DateTime.Now.AddDays(-15)
                };
                
                new Order(uow) {
                    Customer = customer,
                    Amount = 890.75m,
                    OrderDate = DateTime.Now.AddDays(-5)
                };
                
                uow.CommitChanges();
                customerId = customer.Oid;
                
                Console.WriteLine($"Created customer '{customer.Name}' with {customer.Orders.Count} orders");
                Console.WriteLine();
            }
            
            // Step 2: Soft delete the customer
            Console.WriteLine("Step 2: Performing soft delete on customer...");
            using(var uow = new UnitOfWork(dataLayer)) {
                var customer = uow.GetObjectByKey<Customer>(customerId);
                
                Console.WriteLine($"Before delete: Customer has {customer.Orders.Count} orders");
                
                // Soft delete
                customer.Delete();
                uow.CommitChanges();
                
                Console.WriteLine("Customer deleted (GCRecord set)");
                Console.WriteLine();
            }
            
            // Step 3: Verify relationships are preserved
            Console.WriteLine("Step 3: Verifying relationships are preserved...");
            using(var uow = new UnitOfWork(dataLayer)) {
                // Query for deleted customer (must use SelectDeleted)
                var deletedCustomers = new XPCollection<Customer>(uow) {
                    SelectDeleted = true
                };
                deletedCustomers.Criteria = CriteriaOperator.Parse(
                    "GCRecord IS NOT NULL AND Oid = ?", 
                    customerId
                );
                
                var customer = deletedCustomers.FirstOrDefault();
                
                if(customer != null) {
                    Console.WriteLine($"✓ Found deleted customer: {customer.Name}");
                    Console.WriteLine($"✓ GCRecord value: {customer.GetMemberValue("GCRecord")}");
                    
                    // Load orders with SelectDeleted = false to get active orders
                    var activeOrders = new XPCollection<Order>(uow);
                    activeOrders.Criteria = CriteriaOperator.Parse("Customer.Oid = ?", customerId);
                    
                    Console.WriteLine($"✓ Active orders still referencing this customer: {activeOrders.Count}");
                    
                    foreach(var order in activeOrders) {
                        Console.WriteLine($"  - Order #{order.Oid}: ${order.Amount:N2} on {order.OrderDate:yyyy-MM-dd}");
                        Console.WriteLine($"    Customer reference preserved: {order.Customer?.Name ?? "NULL"}");
                    }
                } else {
                    Console.WriteLine("✗ ERROR: Could not find deleted customer!");
                }
                
                Console.WriteLine();
            }
            
            // Step 4: Demonstrate restoring the customer
            Console.WriteLine("Step 4: Restoring the customer (undelete)...");
            using(var uow = new UnitOfWork(dataLayer)) {
                var customer = new XPCollection<Customer>(uow) {
                    SelectDeleted = true,
                    Criteria = CriteriaOperator.Parse("Oid = ?", customerId)
                }.FirstOrDefault();
                
                if(customer != null) {
                    // Clear GCRecord to undelete
                    customer.SetMemberValue("GCRecord", null);
                    uow.CommitChanges();
                    
                    Console.WriteLine($"✓ Customer '{customer.Name}' restored!");
                }
            }
            
            // Step 5: Verify customer is active with relationships intact
            Console.WriteLine("\nStep 5: Verifying restored customer...");
            using(var uow = new UnitOfWork(dataLayer)) {
                var customer = uow.GetObjectByKey<Customer>(customerId);
                
                if(customer != null) {
                    Console.WriteLine($"✓ Customer is active: {customer.Name}");
                    Console.WriteLine($"✓ Customer has {customer.Orders.Count} orders");
                    
                    foreach(var order in customer.Orders) {
                        Console.WriteLine($"  - Order #{order.Oid}: ${order.Amount:N2}");
                    }
                    
                    Console.WriteLine("\n✓ All relationships preserved and intact!");
                } else {
                    Console.WriteLine("✗ ERROR: Customer not found!");
                }
            }
            
            // Step 6: Demonstrate purging
            Console.WriteLine("\nStep 6: Demonstrating purge operation...");
            using(var uow = new UnitOfWork(dataLayer)) {
                var customer = uow.GetObjectByKey<Customer>(customerId);
                
                // Delete again
                customer.Delete();
                uow.CommitChanges();
                
                Console.WriteLine("Customer soft-deleted again");
                
                // Now purge
                var result = uow.PurgeDeletedObjects();
                
                Console.WriteLine($"Purge result:");
                Console.WriteLine($"  - Processed: {result.Processed}");
                Console.WriteLine($"  - Purged: {result.Purged}");
                Console.WriteLine($"  - Non-Purged: {result.NonPurged}");
                Console.WriteLine($"  - Referenced by active objects: {result.ReferencedByNonDeletedObjects}");
            }
            
            // Cleanup
            Console.WriteLine("\nCleaning up...");
            using(var uow = new UnitOfWork(dataLayer)) {
                uow.Delete(uow.Query<Order>());
                uow.Delete(uow.Query<Customer>());
                uow.CommitChanges();
            }
            
            Console.WriteLine("\n=== Example Complete ===");
        }
        
        /// <summary>
        /// Comparison example showing standard XPO behavior vs custom behavior
        /// </summary>
        public static void RunComparisonExample(string connectionString) {
            Console.WriteLine("=== Standard XPO vs PreserveRelationships Comparison ===\n");
            
            // Test 1: Standard XPO behavior
            Console.WriteLine("TEST 1: Standard XPO Soft Delete");
            Console.WriteLine("-----------------------------------");
            var standardLayer = new ThreadSafeDataLayer(
                new ReflectionDictionary(),
                XpoDefault.GetConnectionProvider(connectionString, AutoCreateOption.DatabaseAndSchema)
            );
            
            RunDeleteTest(standardLayer, "Standard XPO");
            
            Console.WriteLine("\n");
            
            // Test 2: Custom PreserveRelationships behavior
            Console.WriteLine("TEST 2: PreserveRelationships Soft Delete");
            Console.WriteLine("-----------------------------------");
            var customLayer = PreserveRelationshipsDataLayerHelper.CreateDataLayer(
                connectionString, 
                preserveRelationships: true
            );
            
            RunDeleteTest(customLayer, "PreserveRelationships");
            
            Console.WriteLine("\n=== Comparison Complete ===");
        }
        
        private static void RunDeleteTest(IDataLayer dataLayer, string testName) {
            int customerId;
            
            // Create
            using(var uow = new UnitOfWork(dataLayer)) {
                uow.UpdateSchema(typeof(Customer), typeof(Order));
                
                var customer = new Customer(uow) {
                    Name = $"Test Customer ({testName})",
                    Email = "test@example.com"
                };
                
                new Order(uow) {
                    Customer = customer,
                    Amount = 100.00m,
                    OrderDate = DateTime.Now
                };
                
                uow.CommitChanges();
                customerId = customer.Oid;
            }
            
            // Delete
            using(var uow = new UnitOfWork(dataLayer)) {
                var customer = uow.GetObjectByKey<Customer>(customerId);
                customer.Delete();
                uow.CommitChanges();
            }
            
            // Check relationships
            using(var uow = new UnitOfWork(dataLayer)) {
                var orders = new XPCollection<Order>(uow);
                orders.Criteria = CriteriaOperator.Parse("Customer.Oid = ?", customerId);
                
                Console.WriteLine($"Orders still referencing deleted customer: {orders.Count}");
                
                if(orders.Count > 0) {
                    var order = orders[0];
                    var customerRef = order.Customer;
                    Console.WriteLine($"Order.Customer is: {(customerRef == null ? "NULL" : customerRef.Name)}");
                    Console.WriteLine($"✓ Relationships PRESERVED");
                } else {
                    Console.WriteLine($"✗ Relationships REMOVED");
                }
            }
            
            // Cleanup
            using(var uow = new UnitOfWork(dataLayer)) {
                uow.Delete(uow.Query<Order>());
                uow.Delete(uow.Query<Customer>());
                uow.PurgeDeletedObjects();
            }
        }
    }
}
