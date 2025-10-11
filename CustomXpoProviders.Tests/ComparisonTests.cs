using DevExpress.Xpo.Metadata;

namespace CustomXpoProviders.Tests;

/// <summary>
/// Comparison tests between standard XPO behavior and custom PreserveRelationships behavior
/// </summary>
[TestFixture]
public class ComparisonTests {
    
    [Test]
    public void StandardXPO_ShouldNullifyRelationships() {
        // Arrange - Use standard data layer
        var connectionString = TestConfiguration.GetConnectionString();
        var standardLayer = new ThreadSafeDataLayer(
            new ReflectionDictionary(),
            XpoDefault.GetConnectionProvider(connectionString, AutoCreateOption.DatabaseAndSchema)
        );
        
        int customerId;
        int orderId;
        
        using(var uow = new UnitOfWork(standardLayer)) {
            uow.UpdateSchema(typeof(Customer), typeof(Order));
            
            var customer = new Customer(uow) {
                Name = "Standard Test",
                Email = "standard@test.com"
            };
            
            var order = new Order(uow) {
                Customer = customer,
                Amount = 100.00m,
                OrderDate = DateTime.Now
            };
            
            uow.CommitChanges();
            customerId = customer.Oid;
            orderId = order.Oid;
        }
        
        // Act - Delete with standard XPO
        using(var uow = new UnitOfWork(standardLayer)) {
            var customer = uow.GetObjectByKey<Customer>(customerId);
            customer!.Delete();
            uow.CommitChanges();
        }
        
        // Assert - Relationship should be nullified (standard behavior)
        using(var uow = new UnitOfWork(standardLayer)) {
            var order = uow.GetObjectByKey<Order>(orderId);
            Assert.That(order, Is.Not.Null);
            
            // With standard XPO, the customer reference is removed
            Assert.That(order!.Customer, Is.Null, "Standard XPO should nullify the relationship");
        }
        
        // Cleanup
        using(var uow = new UnitOfWork(standardLayer)) {
            uow.Delete(uow.Query<Order>());
            uow.Delete(uow.Query<Customer>());
            uow.PurgeDeletedObjects();
            uow.CommitChanges();
        }
    }
    
    [Test]
    public void CustomImplementation_ShouldPreserveRelationships() {
        // Arrange - Use custom data layer
        var connectionString = TestConfiguration.GetConnectionString();
        var customLayer = PreserveRelationshipsDataLayerHelper.CreateDataLayer(
            connectionString,
            preserveRelationships: true
        );
        
        int customerId;
        int orderId;
        
        using(var uow = new UnitOfWork(customLayer)) {
            uow.UpdateSchema(typeof(Customer), typeof(Order));
            
            var customer = new Customer(uow) {
                Name = "Custom Test",
                Email = "custom@test.com"
            };
            
            var order = new Order(uow) {
                Customer = customer,
                Amount = 100.00m,
                OrderDate = DateTime.Now
            };
            
            uow.CommitChanges();
            customerId = customer.Oid;
            orderId = order.Oid;
        }
        
        // Act - Delete with custom implementation
        using(var uow = new UnitOfWork(customLayer)) {
            var customer = uow.GetObjectByKey<Customer>(customerId);
            customer!.Delete();
            uow.CommitChanges();
        }
        
        // Assert - Relationship should be preserved
        using(var uow = new UnitOfWork(customLayer)) {
            var order = uow.GetObjectByKey<Order>(orderId);
            Assert.That(order, Is.Not.Null);
            
            // With custom implementation, the customer reference is preserved
            Assert.That(order!.Customer, Is.Not.Null, "Custom implementation should preserve the relationship");
            Assert.That(order.Customer!.Oid, Is.EqualTo(customerId));
        }
        
        // Cleanup
        using(var uow = new UnitOfWork(customLayer)) {
            uow.Delete(uow.Query<Order>());
            uow.Delete(uow.Query<Customer>());
            uow.PurgeDeletedObjects();
            uow.CommitChanges();
        }
    }
    
    [Test]
    public void BothImplementations_ShouldSetGCRecord() {
        var connectionString = TestConfiguration.GetConnectionString();
        
        // Test both standard and custom set GCRecord
        TestGCRecordBehavior(
            new ThreadSafeDataLayer(
                new ReflectionDictionary(),
                XpoDefault.GetConnectionProvider(connectionString, AutoCreateOption.DatabaseAndSchema)
            ),
            "Standard XPO"
        );
        
        TestGCRecordBehavior(
            PreserveRelationshipsDataLayerHelper.CreateDataLayer(connectionString, true),
            "Custom Implementation"
        );
    }
    
    private void TestGCRecordBehavior(IDataLayer dataLayer, string implementation) {
        int customerId;
        
        using(var uow = new UnitOfWork(dataLayer)) {
            uow.UpdateSchema(typeof(Customer), typeof(Order));
            
            var customer = new Customer(uow) {
                Name = $"Test {implementation}",
                Email = "test@example.com"
            };
            uow.CommitChanges();
            customerId = customer.Oid;
        }
        
        // Delete
        using(var uow = new UnitOfWork(dataLayer)) {
            var customer = uow.GetObjectByKey<Customer>(customerId);
            customer!.Delete();
            uow.CommitChanges();
        }
        
        // Verify GCRecord is set
        using(var uow = new UnitOfWork(dataLayer)) {
            var deletedCustomers = new XPCollection<Customer>(uow) {
                SelectDeleted = true,
                Criteria = CriteriaOperator.Parse("Oid = ?", customerId)
            };
            
            Assert.That(deletedCustomers.Count, Is.EqualTo(1), $"{implementation} should mark object as deleted");
            var gcRecord = deletedCustomers[0].GetMemberValue("GCRecord");
            Assert.That(gcRecord, Is.Not.Null, $"{implementation} should set GCRecord");
        }
        
        // Cleanup
        using(var uow = new UnitOfWork(dataLayer)) {
            uow.Delete(uow.Query<Order>());
            uow.Delete(uow.Query<Customer>());
            uow.PurgeDeletedObjects();
            uow.CommitChanges();
        }
    }
}
