using DevExpress.Xpo;

namespace CustomXpoProviders.Tests;

/// <summary>
/// Test business classes with deferred deletion enabled
/// </summary>
[DeferredDeletion(true)]
public class Customer : XPObject {
    public Customer(Session session) : base(session) { }
    
    string name = string.Empty;
    public string Name {
        get => name;
        set => SetPropertyValue(nameof(Name), ref name, value);
    }
    
    string email = string.Empty;
    public string Email {
        get => email;
        set => SetPropertyValue(nameof(Email), ref email, value);
    }
    
    [Association("Customer-Orders")]
    public XPCollection<Order> Orders {
        get => GetCollection<Order>(nameof(Orders));
    }
}

[DeferredDeletion(true)]
public class Order : XPObject {
    public Order(Session session) : base(session) { }
    
    Customer? customer;
    [Association("Customer-Orders")]
    public Customer? Customer {
        get => customer;
        set => SetPropertyValue(nameof(Customer), ref customer, value);
    }
    
    decimal amount;
    public decimal Amount {
        get => amount;
        set => SetPropertyValue(nameof(Amount), ref amount, value);
    }
    
    DateTime orderDate;
    public DateTime OrderDate {
        get => orderDate;
        set => SetPropertyValue(nameof(OrderDate), ref orderDate, value);
    }
}
