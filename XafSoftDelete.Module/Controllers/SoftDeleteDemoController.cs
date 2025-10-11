using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.Persistent.Base;
using XafSoftDelete.Module.BusinessObjects;
using DevExpress.Data.Filtering;

namespace XafSoftDelete.Module.Controllers
{
    public class SoftDeleteDemoController : ObjectViewController<DetailView, Customer>
    {
        private SimpleAction deletePreserveAction;

        public SoftDeleteDemoController()
        {
            deletePreserveAction = new SimpleAction(this, "DeletePreserveRelationships", PredefinedCategory.Edit)
            {
                Caption = "Delete (preserve relationships)",
                ConfirmationMessage = "Soft-delete this object but preserve relationships?",
                ImageName = "Action_Delete"
            };
            deletePreserveAction.Execute += DeletePreserveAction_Execute;
            // Restore action
            var restoreAction = new SimpleAction(this, "RestoreDeletedCustomer", PredefinedCategory.Edit)
            {
                Caption = "Restore deleted",
                ConfirmationMessage = "Restore selected deleted customer?",
                ImageName = "Action_Undo"
            };
            restoreAction.Execute += (s, e) => {
                RestoreSelectedDeletedCustomer();
            };
            Actions.Add(restoreAction);
        }

        private void DeletePreserveAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var obj = View.CurrentObject as Customer;
            if (obj == null) return;

            ObjectSpace.Delete(obj);
            ObjectSpace.CommitChanges();

            // Show counts: active vs deleted
            using (var os = Application.CreateObjectSpace(typeof(Customer)))
            {
                // Standard query (not include deleted)
                var activeCustomers = os.GetObjects<Customer>(CriteriaOperator.Parse("GCRecord IS NULL"));
                // Deleted (GCRecord not null)
                var deletedCustomers = os.GetObjects<Customer>(CriteriaOperator.Parse("GCRecord IS NOT NULL"));

                var message = $"Deleted. Active customers: {activeCustomers.Count}, Deleted customers: {deletedCustomers.Count}";
                Application.ShowViewStrategy.ShowMessage(message, InformationType.Success);
            }
        }

            private void RestoreSelectedDeletedCustomer()
            {
                // Use the current ObjectSpace to find the selected customer including deleted ones
                var os = ObjectSpace;
                // Try to get the underlying XPO session if available
                var sessionField = typeof(DevExpress.ExpressApp.Xpo.XPObjectSpace).GetProperty("Session");
                if (sessionField == null) {
                    Application.ShowViewStrategy.ShowMessage("Restore not supported in this ObjectSpace.", InformationType.Error);
                    return;
                }

                var xpObjectSpace = os as DevExpress.ExpressApp.Xpo.XPObjectSpace;
                if (xpObjectSpace == null) {
                    Application.ShowViewStrategy.ShowMessage("Restore only supported for XPO ObjectSpaces in this demo.", InformationType.Warning);
                    return;
                }

                var session = xpObjectSpace.Session;
                // Find the deleted object by Oid
                var selected = View.CurrentObject as Customer;
                if (selected == null) return;

                // Reload object with deleted included
                var classInfo = session.GetClassInfo(selected);
                var gcMember = classInfo.GetMember("GCRecord");
                if (gcMember == null) {
                    Application.ShowViewStrategy.ShowMessage("GCRecord member not found.", InformationType.Error);
                    return;
                }

                // Load the object into the session
                var obj = session.GetObjectByKey(classInfo.ClassType, selected.Oid);
                if (obj == null) {
                    Application.ShowViewStrategy.ShowMessage("Deleted object not found in session.", InformationType.Error);
                    return;
                }

                // Clear the GCRecord to restore
                gcMember.SetValue(obj, null);
                session.Save(obj);
                Application.ShowViewStrategy.ShowMessage("Customer restored.", InformationType.Success);
            }
    }
}
