using DevExpress.Xpo.Metadata;

namespace CustomXpoProviders.Tests;

/// <summary>
/// Tests for PreserveRelationshipsDataLayer
/// </summary>
[TestFixture]
public class PreserveRelationshipsDataLayerTests {
    
    private IDataLayer? dataLayer;
    
    [SetUp]
    public void Setup() {
        var connectionString = TestConfiguration.GetConnectionString();

        dataLayer = PreserveRelationshipsDataLayerHelper.CreateDataLayer(
            connectionString,
            preserveRelationships: true
        );
        
        // Create schema
        using var uow = new UnitOfWork(dataLayer);
        uow.UpdateSchema(typeof(Customer), typeof(Order));
        uow.CreateObjectTypeRecords(typeof(Customer), typeof(Order));
        uow.CommitChanges();
    }
    
    [TearDown]
    public void Cleanup() {
        if(dataLayer != null) {
            using var uow = new UnitOfWork(dataLayer);
            uow.Delete(uow.Query<Order>());
            uow.Delete(uow.Query<Customer>());
            uow.PurgeDeletedObjects();
            uow.CommitChanges();
            
            // Dispose the data layer
            dataLayer.Dispose();
            dataLayer = null;
        }
    }
    
    [Test]
    public void SoftDelete_ShouldSetGCRecord() {
        // Arrange
        Assert.That(dataLayer, Is.Not.Null);
        int customerId;
        
        using(var uow = new UnitOfWork(dataLayer)) {
            var customer = new Customer(uow) {
                Name = "Test Customer",
                Email = "test@example.com"
            };
            uow.CommitChanges();
            customerId = customer.Oid;
        }
        
        // Act - Delete the customer
        using(var uow = new UnitOfWork(dataLayer)) {
            var customer = uow.GetObjectByKey<Customer>(customerId);
            Assert.That(customer, Is.Not.Null);
            customer!.Delete();
            uow.CommitChanges();
        }
        
        // Assert - GCRecord should be set
        using(var uow = new UnitOfWork(dataLayer)) {
            var deletedCustomers = new XPCollection<Customer>(uow) {
                SelectDeleted = true,
                Criteria = CriteriaOperator.Parse("Oid = ?", customerId)
            };
            
            Assert.That(deletedCustomers.Count, Is.EqualTo(1));
            var customer = deletedCustomers[0];
            var gcRecord = customer.GetMemberValue("GCRecord");
            Assert.That(gcRecord, Is.Not.Null);
        }
    }
    
    [Test]
    public void SoftDelete_ShouldPreserveRelationships() {
        // Arrange
        Assert.That(dataLayer, Is.Not.Null);
        int customerId;
        int orderId;
        
        using(var uow = new UnitOfWork(dataLayer)) {
            var customer = new Customer(uow) {
                Name = "Test Customer",
                Email = "test@example.com"
            };
            
            var order = new Order(uow) {
                Customer = customer,
                Amount = 1500.00m,
                OrderDate = DateTime.Now
            };
            
            uow.CommitChanges();
            customerId = customer.Oid;
            orderId = order.Oid;
        }
        
        // Act - Delete the customer
        using(var uow = new UnitOfWork(dataLayer)) {
            var customer = uow.GetObjectByKey<Customer>(customerId);
            Assert.That(customer, Is.Not.Null);
            customer!.Delete();
            uow.CommitChanges();
        }
        
        // Assert - Order should still reference the customer
        using(var uow = new UnitOfWork(dataLayer)) {
            var order = uow.GetObjectByKey<Order>(orderId);
            Assert.That(order, Is.Not.Null);
            Assert.That(order!.Customer, Is.Not.Null, "Customer reference should be preserved");
            Assert.That(order.Customer!.Oid, Is.EqualTo(customerId));
        }
    }
    
    [Test]
    public void SoftDelete_DeletedObjectsNotInStandardQueries() {
        // Arrange
        Assert.That(dataLayer, Is.Not.Null);
        int customerId;
        
        using(var uow = new UnitOfWork(dataLayer)) {
            var customer = new Customer(uow) {
                Name = "Test Customer",
                Email = "test@example.com"
            };
            uow.CommitChanges();
            customerId = customer.Oid;
        }
        
        // Act - Delete
        using(var uow = new UnitOfWork(dataLayer)) {
            var customer = uow.GetObjectByKey<Customer>(customerId);
            customer!.Delete();
            uow.CommitChanges();
        }
        
        // Assert - Should not appear in standard queries
        using(var uow = new UnitOfWork(dataLayer)) {
            var activeCustomers = new XPCollection<Customer>(uow);
            Assert.That(activeCustomers.Count, Is.EqualTo(0));
            
            // But should appear with SelectDeleted = true
            var allCustomers = new XPCollection<Customer>(uow) {
                SelectDeleted = true
            };
            Assert.That(allCustomers.Count, Is.EqualTo(1));
        }
    }
    
    [Test]
    public void SoftDelete_CanRestoreDeletedObject() {
        // Arrange
        Assert.That(dataLayer, Is.Not.Null);
        int customerId;
        
        using(var uow = new UnitOfWork(dataLayer)) {
            var customer = new Customer(uow) {
                Name = "Test Customer",
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
        
        // Act - Restore by clearing GCRecord
        using(var uow = new UnitOfWork(dataLayer)) {
            var customer = new XPCollection<Customer>(uow) {
                SelectDeleted = true,
                Criteria = CriteriaOperator.Parse("Oid = ?", customerId)
            }.FirstOrDefault();
            
            Assert.That(customer, Is.Not.Null);
            customer!.SetMemberValue("GCRecord", null);
            uow.CommitChanges();
        }
        
        // Assert - Should be in standard queries again
        using(var uow = new UnitOfWork(dataLayer)) {
            var customer = uow.GetObjectByKey<Customer>(customerId);
            Assert.That(customer, Is.Not.Null);
            Assert.That(customer!.Name, Is.EqualTo("Test Customer"));
        }
    }
    
    [Test]
    public void SoftDelete_WithMultipleOrders_PreservesAllRelationships() {
        // Arrange
        Assert.That(dataLayer, Is.Not.Null);
        int customerId;
        List<int> orderIds = new();
        
        using(var uow = new UnitOfWork(dataLayer)) {
            var customer = new Customer(uow) {
                Name = "Test Customer",
                Email = "test@example.com"
            };
            
            for(int i = 0; i < 3; i++) {
                var order = new Order(uow) {
                    Customer = customer,
                    Amount = 100m * (i + 1),
                    OrderDate = DateTime.Now.AddDays(-i)
                };
                orderIds.Add(order.Oid);
            }
            
            uow.CommitChanges();
            customerId = customer.Oid;
        }
        
        // Act - Delete customer
        using(var uow = new UnitOfWork(dataLayer)) {
            var customer = uow.GetObjectByKey<Customer>(customerId);
            customer!.Delete();
            uow.CommitChanges();
        }
        
        // Assert - All orders should still reference the customer
        using(var uow = new UnitOfWork(dataLayer)) {
            foreach(var orderId in orderIds) {
                var order = uow.GetObjectByKey<Order>(orderId);
                Assert.That(order, Is.Not.Null);
                Assert.That(order!.Customer, Is.Not.Null, $"Order {orderId} should still reference customer");
                Assert.That(order.Customer!.Oid, Is.EqualTo(customerId));
            }
        }
    }
    
    [Test]
    public void PurgeDeletedObjects_ShouldRemoveSoftDeletedObjects() {
        // Arrange
        Assert.That(dataLayer, Is.Not.Null);
        int customerId;
        
        using(var uow = new UnitOfWork(dataLayer)) {
            var customer = new Customer(uow) {
                Name = "Test Customer",
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
        
        // Act - Purge
        using(var uow = new UnitOfWork(dataLayer)) {
            var result = uow.PurgeDeletedObjects();
            Assert.That(result.Processed, Is.GreaterThan(0));
        }
        
        // Assert - Should be completely gone
        using(var uow = new UnitOfWork(dataLayer)) {
            var allCustomers = new XPCollection<Customer>(uow) {
                SelectDeleted = true,
                Criteria = CriteriaOperator.Parse("Oid = ?", customerId)
            };
            Assert.That(allCustomers.Count, Is.EqualTo(0));
        }
    }
}
