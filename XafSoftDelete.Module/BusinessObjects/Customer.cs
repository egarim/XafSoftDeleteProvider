using System;
using DevExpress.Xpo;
using DevExpress.Persistent.Base;

namespace XafSoftDelete.Module.BusinessObjects {
    [DefaultClassOptions]
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
}
