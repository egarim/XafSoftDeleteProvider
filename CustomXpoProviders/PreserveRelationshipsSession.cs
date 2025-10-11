using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DevExpress.Xpo;
using DevExpress.Xpo.Metadata;
using DevExpress.Xpo.Helpers;

namespace CustomXpoProviders {
    /// <summary>
    /// Custom Session that preserves relationships during soft delete (deferred deletion).
    /// When an object with DeferredDeletion is deleted, relationships are NOT removed.
    /// This allows you to maintain referential integrity and see what was related before deletion.
    /// </summary>
    public class PreserveRelationshipsSession : Session {
        
        /// <summary>
        /// Gets or sets whether to preserve relationships during soft delete.
        /// Default is true.
        /// </summary>
        public bool PreserveRelationshipsOnSoftDelete { get; set; } = true;

        public PreserveRelationshipsSession() : base() { }
        
        public PreserveRelationshipsSession(IDataLayer layer) : base(layer) { }
        
        public PreserveRelationshipsSession(IObjectLayer layer) : base(layer) { }
        
        public PreserveRelationshipsSession(IDataLayer layer, params IDisposable[] disposeOnDisconnect) 
            : base(layer, disposeOnDisconnect) { }
        
        public PreserveRelationshipsSession(IObjectLayer layer, params IDisposable[] disposeOnDisconnect) 
            : base(layer, disposeOnDisconnect) { }

        /// <summary>
        /// Override DeleteObject to customize behavior for objects with deferred deletion.
        /// </summary>
        protected virtual void DeleteObject(object theObject) {
            if(theObject == null)
                return;

            XPClassInfo classInfo = GetClassInfo(theObject);
            
            // Check if this is a soft delete (deferred deletion enabled)
            if(classInfo.IsGCRecordObject) {
                XPMemberInfo gcRecord = classInfo.GetMember(GCRecordField.StaticName);
                
                // Already deleted?
                if(gcRecord.GetValue(theObject) != null)
                    return;

                // Load aggregated members if needed
                if(this is ExplicitUnitOfWork) {
                    LoadAggregatedMembers(classInfo, theObject);
                }

                TriggerObjectDeleting(theObject);
                
                // Set the GCRecord to mark as deleted
                gcRecord.SetValue(theObject, NextGCRecordValue());
                
                // Custom delete logic - preserves relationships if enabled
                if(PreserveRelationshipsOnSoftDelete) {
                    DeleteCorePreserveRelationships(classInfo, theObject);
                } else {
                    // Use standard behavior (removes relationships)
                    base.DeleteCore(classInfo, theObject);
                }
                
                TriggerObjectDeleted(theObject);
                Save(theObject);
            }
            else {
                // Hard delete - use standard behavior
                if(IsObjectToDelete(theObject))
                    return;
                    
                if(this is ExplicitUnitOfWork) {
                    LoadAggregatedMembers(classInfo, theObject);
                }
                
                TriggerObjectDeleting(theObject);
                ProcessingProcess(objectsMarkedDeleted, theObject);
                base.DeleteCore(classInfo, theObject);
                TriggerObjectDeleted(theObject);
            }
        }

        /// <summary>
        /// Override DeleteObjectAsync to customize behavior for objects with deferred deletion.
        /// </summary>
        protected virtual async Task DeleteObjectAsync(object theObject, CancellationToken cancellationToken) {
            if(theObject == null)
                return;

            XPClassInfo classInfo = GetClassInfo(theObject);
            
            if(classInfo.IsGCRecordObject) {
                XPMemberInfo gcRecord = classInfo.GetMember(GCRecordField.StaticName);
                
                if(gcRecord.GetValue(theObject) != null)
                    return;

                if(this is ExplicitUnitOfWork) {
                    await LoadAggregatedMembersAsync(classInfo, theObject, cancellationToken);
                }

                TriggerObjectDeleting(theObject);
                gcRecord.SetValue(theObject, NextGCRecordValue());
                
                if(PreserveRelationshipsOnSoftDelete) {
                    await DeleteCorePreserveRelationshipsAsync(classInfo, theObject, cancellationToken);
                } else {
                    await base.DeleteCoreAsync(classInfo, theObject, cancellationToken);
                }
                
                TriggerObjectDeleted(theObject);
                await SaveAsync(theObject, cancellationToken);
            }
            else {
                if(IsObjectToDelete(theObject))
                    return;
                    
                if(this is ExplicitUnitOfWork) {
                    await LoadAggregatedMembersAsync(classInfo, theObject, cancellationToken);
                }
                
                TriggerObjectDeleting(theObject);
                await ProcessingProcessAsync(objectsMarkedDeleted, theObject, cancellationToken);
                await base.DeleteCoreAsync(classInfo, theObject, cancellationToken);
                TriggerObjectDeleted(theObject);
            }
        }

        /// <summary>
        /// Custom delete core that preserves relationships.
        /// Only handles aggregated objects and many-to-many intermediate tables.
        /// Does NOT remove the object from association lists.
        /// </summary>
        protected virtual void DeleteCorePreserveRelationships(XPClassInfo classInfo, object theObject) {
            // Handle aggregated collections - these should still be deleted
            foreach(XPMemberInfo mi in classInfo.AssociationListProperties) {
                if(mi.IsAggregated) {
                    if(mi.IsCollection) {
                        XPBaseCollection collection = (XPBaseCollection)mi.GetValue(theObject);
                        CheckFilteredAggregateDeletion(theObject, mi, collection);
                        using(var toDelete = LohPooled.ToListForDispose<object>(collection.Cast<object>(), collection.Count)) {
                            for(int i = toDelete.Count - 1; i >= 0; i--) {
                                Delete(toDelete[i]);
                            }
                            if(!collection.SelectDeleted) {
                                for(int i = toDelete.Count - 1; i >= 0; i--) {
                                    collection.BaseRemove(toDelete[i]);
                                }
                            }
                        }
                    }
                    else {
                        IList list = (IList)mi.GetValue(theObject);
                        using(var toDelete = LohPooled.ToListForDispose<object>(list.Cast<object>(), list.Count)) {
                            for(int i = toDelete.Count - 1; i >= 0; i--) {
                                Delete(toDelete[i]);
                            }
                        }
                    }
                }
                else if(mi.IsManyToMany) {
                    // For many-to-many, we still remove from the intermediate table
                    XPBaseCollection collection = (XPBaseCollection)mi.GetValue(theObject);
                    if(!collection.SelectDeleted) {
                        using(var toDelete = LohPooled.ToListForDispose<object>(collection.Cast<object>(), collection.Count)) {
                            for(int i = toDelete.Count - 1; i >= 0; i--) {
                                collection.BaseRemove(toDelete[i]);
                            }
                        }
                    }
                }
                // NOTE: Non-aggregated associations are NOT processed here
                // This preserves the relationships!
            }

            // Handle aggregated object properties
            foreach(XPMemberInfo mi in classInfo.ObjectProperties) {
                if(mi.IsAggregated) {
                    object aggregated = mi.GetValue(theObject);
                    Delete(aggregated);
                }
                
                // NOTE: Association properties (mi.IsAssociation) are NOT processed here
                // This preserves the relationships on the "one" side!
            }
        }

        /// <summary>
        /// Async version of DeleteCorePreserveRelationships
        /// </summary>
        protected virtual async Task DeleteCorePreserveRelationshipsAsync(XPClassInfo classInfo, object theObject, CancellationToken cancellationToken) {
            // Handle aggregated collections
            foreach(XPMemberInfo mi in classInfo.AssociationListProperties) {
                if(mi.IsAggregated) {
                    if(mi.IsCollection) {
                        XPBaseCollection collection = (XPBaseCollection)mi.GetValue(theObject);
                        CheckFilteredAggregateDeletion(theObject, mi, collection);
                        if(!collection.IsLoaded) {
                            await collection.LoadAsync(cancellationToken);
                        }
                        using(var toDelete = LohPooled.ToListForDispose<object>(collection.Cast<object>(), collection.Count)) {
                            for(int i = toDelete.Count - 1; i >= 0; i--) {
                                await DeleteAsync(toDelete[i], cancellationToken);
                            }
                            if(!collection.SelectDeleted) {
                                for(int i = toDelete.Count - 1; i >= 0; i--) {
                                    await collection.BaseRemoveAsync(toDelete[i], cancellationToken);
                                }
                            }
                        }
                    }
                    else {
                        IList list = (IList)mi.GetValue(theObject);
                        await LoadAssociationListAsync(list, cancellationToken);
                        using(var toDelete = LohPooled.ToListForDispose<object>(list.Cast<object>(), list.Count)) {
                            for(int i = toDelete.Count - 1; i >= 0; i--) {
                                await DeleteAsync(toDelete[i], cancellationToken);
                            }
                        }
                    }
                }
                else if(mi.IsManyToMany) {
                    XPBaseCollection collection = (XPBaseCollection)mi.GetValue(theObject);
                    if(!collection.SelectDeleted) {
                        if(!collection.IsLoaded) {
                            await collection.LoadAsync(cancellationToken);
                        }
                        using(var toDelete = LohPooled.ToListForDispose<object>(collection.Cast<object>(), collection.Count)) {
                            for(int i = toDelete.Count - 1; i >= 0; i--) {
                                await collection.BaseRemoveAsync(toDelete[i], cancellationToken);
                            }
                        }
                    }
                }
            }

            // Handle aggregated object properties
            foreach(XPMemberInfo mi in classInfo.ObjectProperties) {
                if(mi.IsAggregated) {
                    object aggregated = mi.GetValue(theObject);
                    await DeleteAsync(aggregated, cancellationToken);
                }
            }
        }

        // Make Session's protected members accessible
        private static System.Reflection.FieldInfo objectsMarkedDeletedField;
        private IList objectsMarkedDeleted {
            get {
                if(objectsMarkedDeletedField == null) {
                    objectsMarkedDeletedField = typeof(Session).GetField("objectsMarkedDeleted", 
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                }
                return (IList)objectsMarkedDeletedField.GetValue(this);
            }
        }

        private static System.Reflection.MethodInfo processingProcessMethod;
        private void ProcessingProcess(IList list, object obj) {
            if(processingProcessMethod == null) {
                processingProcessMethod = typeof(Session).GetMethod("ProcessingProcess", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            }
            processingProcessMethod.Invoke(this, new[] { list, obj });
        }

        private static System.Reflection.MethodInfo processingProcessAsyncMethod;
        private async Task ProcessingProcessAsync(IList list, object obj, CancellationToken ct) {
            if(processingProcessAsyncMethod == null) {
                processingProcessAsyncMethod = typeof(Session).GetMethod("ProcessingProcessAsync", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            }
            await (Task)processingProcessAsyncMethod.Invoke(this, new object[] { list, obj, ct });
        }

        private static System.Reflection.MethodInfo loadAggregatedMembersMethod;
        private void LoadAggregatedMembers(XPClassInfo classInfo, object theObject) {
            if(loadAggregatedMembersMethod == null) {
                loadAggregatedMembersMethod = typeof(Session).GetMethod("LoadAggregatedMembers", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            }
            loadAggregatedMembersMethod.Invoke(this, new[] { classInfo, theObject });
        }

        private static System.Reflection.MethodInfo loadAggregatedMembersAsyncMethod;
        private async Task LoadAggregatedMembersAsync(XPClassInfo classInfo, object theObject, CancellationToken ct) {
            if(loadAggregatedMembersAsyncMethod == null) {
                loadAggregatedMembersAsyncMethod = typeof(Session).GetMethod("LoadAggregatedMembersAsync", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            }
            await (Task)loadAggregatedMembersAsyncMethod.Invoke(this, new object[] { classInfo, theObject, ct });
        }

        private static System.Reflection.MethodInfo loadAssociationListAsyncMethod;
        private async Task LoadAssociationListAsync(object list, CancellationToken ct) {
            if(loadAssociationListAsyncMethod == null) {
                loadAssociationListAsyncMethod = typeof(Session).GetMethod("LoadAssociationListAsync", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            }
            await (Task)loadAssociationListAsyncMethod.Invoke(this, new object[] { list, ct });
        }

        private static System.Reflection.MethodInfo nextGCRecordValueMethod;
        private int NextGCRecordValue() {
            if(nextGCRecordValueMethod == null) {
                nextGCRecordValueMethod = typeof(Session).GetMethod("NextGCRecordValue", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            }
            return (int)nextGCRecordValueMethod.Invoke(this, null);
        }

        private static System.Reflection.MethodInfo checkFilteredAggregateDeletionMethod;
        private void CheckFilteredAggregateDeletion(object theObject, XPMemberInfo mi, XPBaseCollection collection) {
            if(checkFilteredAggregateDeletionMethod == null) {
                checkFilteredAggregateDeletionMethod = typeof(Session).GetMethod("CheckFilteredAggregateDeletion", 
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            }
            checkFilteredAggregateDeletionMethod.Invoke(null, new object[] { theObject, mi, collection });
        }
    }
}
