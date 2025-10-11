using System;
using DevExpress.Xpo;
using DevExpress.Persistent.Base;

namespace XafSoftDelete.Module.BusinessObjects {
    [DefaultClassOptions]
    [DeferredDeletion(true)]
    public class Order : XPObject {
        public Order(Session session) : base(session) { }

        Customer customer;
        [Association("Customer-Orders")]
        public Customer Customer {
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
}
