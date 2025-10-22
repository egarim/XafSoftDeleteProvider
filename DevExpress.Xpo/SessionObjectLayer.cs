#region Copyright (c) 2000-2025 Developer Express Inc.
/*
{*******************************************************************}
{                                                                   }
{       Developer Express .NET Component Library                    }
{                                                                   }
{                                                                   }
{       Copyright (c) 2000-2025 Developer Express Inc.              }
{       ALL RIGHTS RESERVED                                         }
{                                                                   }
{   The entire contents of this file is protected by U.S. and       }
{   International Copyright Laws. Unauthorized reproduction,        }
{   reverse-engineering, and distribution of all or any portion of  }
{   the code contained in this file is strictly prohibited and may  }
{   result in severe civil and criminal penalties and will be       }
{   prosecuted to the maximum extent possible under the law.        }
{                                                                   }
{   RESTRICTIONS                                                    }
{                                                                   }
{   THIS SOURCE CODE AND ALL RESULTING INTERMEDIATE FILES           }
{   ARE CONFIDENTIAL AND PROPRIETARY TRADE                          }
{   SECRETS OF DEVELOPER EXPRESS INC. THE REGISTERED DEVELOPER IS   }
{   LICENSED TO DISTRIBUTE THE PRODUCT AND ALL ACCOMPANYING .NET    }
{   CONTROLS AS PART OF AN EXECUTABLE PROGRAM ONLY.                 }
{                                                                   }
{   THE SOURCE CODE CONTAINED WITHIN THIS FILE AND ALL RELATED      }
{   FILES OR ANY PORTION OF ITS CONTENTS SHALL AT NO TIME BE        }
{   COPIED, TRANSFERRED, SOLD, DISTRIBUTED, OR OTHERWISE MADE       }
{   AVAILABLE TO OTHER INDIVIDUALS WITHOUT EXPRESS WRITTEN CONSENT  }
{   AND PERMISSION FROM DEVELOPER EXPRESS INC.                      }
{                                                                   }
{   CONSULT THE END USER LICENSE AGREEMENT FOR INFORMATION ON       }
{   ADDITIONAL RESTRICTIONS.                                        }
{                                                                   }
{*******************************************************************}
*/
#endregion Copyright (c) 2000-2025 Developer Express Inc.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevExpress.Data.Filtering;
using DevExpress.Utils;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.Exceptions;
using DevExpress.Xpo.Helpers;
using DevExpress.Xpo.Infrastructure;
using DevExpress.Xpo.Metadata;
using DevExpress.Xpo.Metadata.Helpers;
namespace DevExpress.Xpo {
	public class SessionObjectLayer :
		IObjectLayerForTests
		, IObjectLayerEx
		, IObjectLayerOnSession
		, IObjectLayerAsync
		, ICommandChannel
		, ICommandChannelAsync {
		public readonly bool ThroughCommitMode;
		public readonly Session ParentSession;
		public event EventHandler<CustomSecurityCriteriaPatcherEventArgs> CustomSecurityCriteriaPatcher;
		public event EventHandler<ObjectsLoadedEventArgs> ObjectsLoaded;
		readonly ICommandChannel nestedCommandChannel;
		readonly ICommandChannelAsync nestedCommandChannelAsync;
		SecurityContext _securityContextMain;
		readonly IGenericSecurityRule genericSecurityRule;
		readonly ISecurityRuleProvider securityDictionary;
		readonly object securityCustomContext;
		public SessionObjectLayer(Session parentSession)
			: this(parentSession, false) {
		}
		public SessionObjectLayer(Session parentSession, bool throughCommitMode)
			: this(parentSession, throughCommitMode, null, null, null) {
		}
		public SessionObjectLayer(Session parentSession, bool throughCommitMode, IGenericSecurityRule genericSecurityRule, ISecurityRuleProvider securityDictionary, object securityCustomContext) {
			this.ParentSession = parentSession;
			this.nestedCommandChannel = ParentSession as ICommandChannel;
			this.nestedCommandChannelAsync = ParentSession as ICommandChannelAsync;
			this.ThroughCommitMode = throughCommitMode;
			this.securityDictionary = securityDictionary;
			this.securityCustomContext = securityCustomContext;
			this.genericSecurityRule = genericSecurityRule;
		}
		protected virtual SecurityContext CreateSecurityContext(SessionObjectLayer parentObjectLayer, IGenericSecurityRule genericSecurityRule, ISecurityRuleProvider securityDictionary, object customContext) {
			return new SecurityContext(parentObjectLayer, genericSecurityRule, securityDictionary, customContext);
		}
		SecurityContext SecurityContextMain {
			get {
				if(_securityContextMain == null && securityDictionary != null) {
					_securityContextMain = CreateSecurityContext(this, genericSecurityRule, securityDictionary, securityCustomContext);
				}
				return _securityContextMain;
			}
		}
		SecurityContext GetSecurityContext(Session nestedSession) {
			return SecurityContextMain == null ? null : SecurityContextMain.Clone(nestedSession);
		}
		public void ClearDatabase() {
			ParentSession.ClearDatabase();
		}
		public UpdateSchemaResult UpdateSchema(bool doNotCreateIfFirstTableNotExist, params XPClassInfo[] types) {
			return ParentSession.UpdateSchema(doNotCreateIfFirstTableNotExist, types);
		}
		public Task<UpdateSchemaResult> UpdateSchemaAsync(CancellationToken cancellationToken, bool doNotCreateIfFirstTableNotExist, params XPClassInfo[] types) {
			return ParentSession.UpdateSchemaAsync(cancellationToken, doNotCreateIfFirstTableNotExist, types);
		}
		static object nestedParentMapKeyObject = new object();
		protected internal NestedParentMap GetNestedParentMap(Session session) {
			object nestedParentMap = null;
			if(!((IWideDataStorage)session).TryGetWideDataItem(nestedParentMapKeyObject, out nestedParentMap)) {
				session.IdentityMapBehavior = ParentSession.IdentityMapBehavior;
				if(session.GetIdentityMapBehavior() == IdentityMapBehavior.Weak) {
					nestedParentMap = new WeakNestedParentMap(session);
				}
				else {
					nestedParentMap = new StrongNestedParentMap(session);
				}
				((IWideDataStorage)session).SetWideDataItem(nestedParentMapKeyObject, nestedParentMap);
			}
			return (NestedParentMap)nestedParentMap;
		}
		public ICollection[] LoadObjects(Session session, ObjectsQuery[] queries) {
			if(queries == null || queries.Length == 0) return Array.Empty<object[]>();
			SecurityContext securityContext = GetSecurityContext(session);
			ObjectsQuery[] parentQueries = GetParentQueries(session, queries, securityContext);
			ICollection[] parentObjects = ParentSession.GetObjects(parentQueries);
			NestedParentMap nestedParentMap = GetNestedParentMap(session);
			ICollection[] nestedObjects = new NestedLoader(session, ParentSession, nestedParentMap, securityContext).GetNestedObjects(parentObjects, GetForceList(queries));
			OnLoadedObjects(parentObjects, nestedObjects, nestedParentMap, securityContext);
			return nestedObjects;
		}
		public async Task<ICollection[]> LoadObjectsAsync(Session session, ObjectsQuery[] queries, CancellationToken cancellationToken = default(CancellationToken)) {
			if(queries == null || queries.Length == 0) return Array.Empty<object[]>();
			SecurityContext securityContext = GetSecurityContext(session);
			ObjectsQuery[] parentQueries = GetParentQueries(session, queries, securityContext);
			cancellationToken.ThrowIfCancellationRequested();
			ICollection[] parentObjects = await ParentSession.GetObjectsAsync(parentQueries, cancellationToken);
			NestedParentMap nestedParentMap = GetNestedParentMap(session);
			cancellationToken.ThrowIfCancellationRequested();
			int asyncOperationId = SessionStateStack.GetNewAsyncOperationId();
			ICollection[] nestedObjects = await (new NestedLoader(session, ParentSession, nestedParentMap, securityContext)).GetNestedObjectsAsync(parentObjects, GetForceList(queries), asyncOperationId, cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			OnLoadedObjects(parentObjects, nestedObjects, nestedParentMap, securityContext);
			return nestedObjects;
		}
		class ParentAsyncResultInternal {
			Exception ex;
			object obj;
			public Exception Ex { get { return ex; } set { ex = value; } }
			public object Obj { get { return obj; } set { obj = value; } }
		}
		public object LoadObjectsAsync(Session session, ObjectsQuery[] queries, AsyncLoadObjectsCallback callback) {
			SynchronizationContext syncContext = AsyncOperationsHelper.CaptureSynchronizationContextOrFail();
			Guard.ArgumentNotNull(callback, nameof(callback));
			bool[] force = GetForceList(queries);
			SecurityContext securityContext = GetSecurityContext(session);
			ManualResetEvent parentSessionLoadComplete = new ManualResetEvent(false);
			ParentAsyncResultInternal parentSessionLoadObjectsResult = new ParentAsyncResultInternal();
			ObjectsQuery[] parentQueries = GetParentQueries(session, queries, securityContext);
			AsyncRequest nestedRequest = ParentSession.GetObjectsAsync(parentQueries, new AsyncLoadObjectsCallback(delegate (ICollection[] collection, Exception ex) {
				parentSessionLoadObjectsResult.Obj = collection;
				parentSessionLoadObjectsResult.Ex = ex;
				parentSessionLoadComplete.Set();
			})) as AsyncRequest;
			AsyncRequest mainRequest = new AsyncRequest(syncContext, new AsyncRequestExec(delegate (AsyncRequest ar) {
				ar.RemoveNestedRequest(nestedRequest);
				parentSessionLoadComplete.WaitOne();
				ICollection[] parentCollection = (ICollection[])parentSessionLoadObjectsResult.Obj;
				Exception parentEx = parentSessionLoadObjectsResult.Ex;
				try {
					session.AsyncExecuteQueue.Invoke(ar.SyncContext, new SendOrPostCallback(delegate (object obj) {
						try {
							if(parentEx != null)
								callback(null, parentEx);
							else
								callback(new NestedLoader(session, ParentSession, GetNestedParentMap(session), securityContext).GetNestedObjects(parentCollection, force), null);
						}
						catch(Exception ex) {
							try {
								callback(null, ex);
							}
							catch(Exception) { }
						}
					}), null, true);
				}
				catch(Exception ex) {
					try {
						session.AsyncExecuteQueue.Invoke(ar.SyncContext, new SendOrPostCallback(delegate (object obj) {
							try {
								callback(null, ex);
							}
							catch(Exception) { }
						}), null, true);
					}
					catch(Exception) { }
				}
			}));
			mainRequest.AddNestedRequest(nestedRequest);
			return mainRequest.Start(session.AsyncExecuteQueue);
		}
		bool[] GetForceList(ObjectsQuery[] queries) {
			bool[] force = new bool[queries.Length];
			for(int i = 0; i < force.Length; i++) {
				force[i] = queries[i].Force;
			}
			return force;
		}
		ObjectsQuery[] GetParentQueries(Session session, ObjectsQuery[] queries, SecurityContext securityContext) {
			ObjectsQuery[] parentQueries = new ObjectsQuery[queries.Length];
			for(int i = 0; i < queries.Length; i++) {
				CriteriaOperator criteria = queries[i].Criteria;
				SortingCollection sorting = queries[i].Sorting;
				XPClassInfo classInfo = queries[i].ClassInfo;
				if(securityContext != null) {
					CriteriaOperator patchCriteria;
					ISecurityRule rule = null;
					ISecurityRule2 rule2 = null;
					ISecurityRuleProvider2 securityRuleProvider2 = securityContext.SecurityRuleProvider as ISecurityRuleProvider2;
					ISecurityCriteriaPatcher securityCriteriaPatcher = GetSecurityCriteriaPatcher(securityContext, classInfo);
					if(securityRuleProvider2 != null && securityRuleProvider2.Enabled == true) {
						rule2 = securityRuleProvider2.GetRule(classInfo);
					}
					else {
						rule = securityContext.SecurityRuleProvider.GetRule(classInfo);
					}
					if(rule != null) {
						if(!SecurityContext.IsSystemClass(classInfo) && rule.GetSelectFilterCriteria(securityContext, classInfo, out patchCriteria))
							criteria = GroupOperator.And(securityCriteriaPatcher.Process(criteria), securityContext.ExpandToLogical(classInfo, patchCriteria));
						else
							criteria = securityCriteriaPatcher.Process(criteria);
					}
					if(rule2 != null) {
						if(!SecurityContext.IsSystemClass(classInfo) && rule2.GetSelectFilterCriteria(securityContext, classInfo, out patchCriteria))
							criteria = GroupOperator.And(securityCriteriaPatcher.Process(criteria), securityContext.ExpandToLogical(classInfo, patchCriteria));
						else
							criteria = securityCriteriaPatcher.Process(criteria);
					}
					if(sorting != null) {
						SortingCollection patchedSorting = new SortingCollection();
						for(int s = 0; s < sorting.Count; s++) {
							patchedSorting.Add(new SortProperty(securityCriteriaPatcher.Process(sorting[s].Property), sorting[s].Direction));
						}
						sorting = patchedSorting;
					}
				}
				parentQueries[i] = new ObjectsQuery(classInfo, GetNestedCriteria(session, criteria, securityContext), sorting, queries[i].SkipSelectedRecords,
					queries[i].TopSelectedRecords, Generators.CollectionCriteriaPatcher.CloneToAnotherSession(queries[i].CollectionCriteriaPatcher, ParentSession), queries[i].Force);
			}
			return parentQueries;
		}
		private ISecurityCriteriaPatcher GetSecurityCriteriaPatcher(SecurityContext securityContext, XPClassInfo classInfo) {
			ISecurityCriteriaPatcher securityCriteriaPatcher = null;
			if(securityContext != null) {
				if(CustomSecurityCriteriaPatcher != null) {
					CustomSecurityCriteriaPatcherEventArgs customSecurityCriteriaPatcherEventArgs = new CustomSecurityCriteriaPatcherEventArgs(classInfo, securityContext);
					CustomSecurityCriteriaPatcher(this, customSecurityCriteriaPatcherEventArgs);
					if(customSecurityCriteriaPatcherEventArgs.Handled) {
						securityCriteriaPatcher = customSecurityCriteriaPatcherEventArgs.CustomSecurityCriteriaPatcher;
					}
				}
				if(securityCriteriaPatcher == null) {
					securityCriteriaPatcher = new SecurityCriteriaPatcher(classInfo, securityContext);
				}
			}
			return securityCriteriaPatcher;
		}
		public List<object[]> SelectData(Session session, ObjectsQuery query, CriteriaOperatorCollection properties, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria) {
			SecurityContext securityContext = GetSecurityContext(session);
			ObjectsQuery parentQuery = GetParentQueries(session, new ObjectsQuery[] { query }, securityContext)[0];
			ISecurityCriteriaPatcher securityCriteriaPatcher = GetSecurityCriteriaPatcher(securityContext, parentQuery.ClassInfo);
			return ParentSession.SelectData(parentQuery.ClassInfo,
				SecurityPatchProperties(securityCriteriaPatcher, properties),
				parentQuery.Criteria,
				SecurityPatchProperties(securityCriteriaPatcher, groupProperties),
				securityCriteriaPatcher == null ? groupCriteria : securityCriteriaPatcher.Process(groupCriteria),
				parentQuery.CollectionCriteriaPatcher.SelectDeleted, parentQuery.SkipSelectedRecords, parentQuery.TopSelectedRecords, parentQuery.Sorting);
		}
		public Task<List<object[]>> SelectDataAsync(Session session, ObjectsQuery query, CriteriaOperatorCollection properties, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria, CancellationToken cancellationToken = default(CancellationToken)) {
			SecurityContext securityContext = GetSecurityContext(session);
			ObjectsQuery parentQuery = GetParentQueries(session, new ObjectsQuery[] { query }, securityContext)[0];
			cancellationToken.ThrowIfCancellationRequested();
			ISecurityCriteriaPatcher securityCriteriaPatcher = GetSecurityCriteriaPatcher(securityContext, parentQuery.ClassInfo);
			return ParentSession.SelectDataAsync(parentQuery.ClassInfo,
				SecurityPatchProperties(securityCriteriaPatcher, properties),
				parentQuery.Criteria,
				SecurityPatchProperties(securityCriteriaPatcher, groupProperties),
				securityCriteriaPatcher == null ? groupCriteria : securityCriteriaPatcher.Process(groupCriteria),
				parentQuery.CollectionCriteriaPatcher.SelectDeleted, parentQuery.SkipSelectedRecords, parentQuery.TopSelectedRecords, parentQuery.Sorting,
				cancellationToken);
		}
		CriteriaOperatorCollection SecurityPatchProperties(ISecurityCriteriaPatcher securityCriteriaPatcher, CriteriaOperatorCollection properties) {
			CriteriaOperatorCollection resultCriteriaCollection = properties;
			if(securityCriteriaPatcher != null && properties != null && properties.Count > 0) {
				CriteriaOperatorCollection patchedCollection = new CriteriaOperatorCollection();
				bool patched = false;
				for(int i = 0; i < properties.Count; i++) {
					CriteriaOperator patchedCriteria = securityCriteriaPatcher.Process(properties[i]);
					if(!ReferenceEquals(patchedCriteria, properties[i])) patched = true;
					patchedCollection.Add(patchedCriteria);
				}
				if(patched) {
					resultCriteriaCollection = patchedCollection;
				}
			}
			return resultCriteriaCollection;
		}
		public object SelectDataAsync(Session session, ObjectsQuery query, CriteriaOperatorCollection properties, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria, AsyncSelectDataCallback callback) {
			SynchronizationContext syncContext = AsyncOperationsHelper.CaptureSynchronizationContextOrFail();
			Guard.ArgumentNotNull(callback, nameof(callback));
			SecurityContext securityContext = GetSecurityContext(session);
			ManualResetEvent parentSessionSelectDataEvent = new ManualResetEvent(false);
			ParentAsyncResultInternal parentSessionSelectDataResult = new ParentAsyncResultInternal();
			ObjectsQuery parentQuery = GetParentQueries(session, new ObjectsQuery[] { query }, securityContext)[0];
			ISecurityCriteriaPatcher securityCriteriaPatcher = GetSecurityCriteriaPatcher(securityContext, parentQuery.ClassInfo);
			AsyncRequest nestedRequest = ParentSession.SelectDataAsync(parentQuery.ClassInfo,
				SecurityPatchProperties(securityCriteriaPatcher, properties),
				parentQuery.Criteria,
				SecurityPatchProperties(securityCriteriaPatcher, groupProperties),
				securityCriteriaPatcher == null ? groupCriteria : securityCriteriaPatcher.Process(groupCriteria),
				parentQuery.CollectionCriteriaPatcher.SelectDeleted, parentQuery.SkipSelectedRecords, parentQuery.TopSelectedRecords, parentQuery.Sorting,
					new AsyncSelectDataCallback(delegate (List<object[]> result, Exception ex) {
						parentSessionSelectDataResult.Obj = result;
						parentSessionSelectDataResult.Ex = ex;
						parentSessionSelectDataEvent.Set();
					})) as AsyncRequest;
			AsyncRequest mainRequest = new AsyncRequest(syncContext, new AsyncRequestExec(delegate (AsyncRequest request) {
				request.RemoveNestedRequest(nestedRequest);
				parentSessionSelectDataEvent.WaitOne();
				try {
					session.AsyncExecuteQueue.Invoke(request.SyncContext, new SendOrPostCallback(delegate (object obj) {
						try {
							callback((List<object[]>)parentSessionSelectDataResult.Obj, parentSessionSelectDataResult.Ex);
						}
						catch(Exception ex) {
							try {
								callback(null, ex);
							}
							catch(Exception) { }
						}
					}), null, true);
				}
				catch(Exception ex) {
					try {
						session.AsyncExecuteQueue.Invoke(request.SyncContext, new SendOrPostCallback(delegate (object obj) {
							try {
								callback(null, ex);
							}
							catch(Exception) { }
						}), null, true);
					}
					catch(Exception) { }
				}
			}));
			mainRequest.AddNestedRequest(nestedRequest);
			return mainRequest.Start(session.AsyncExecuteQueue);
		}
		class AsyncCommitContext {
			public readonly Session Session;
			public readonly ICollection ReadyListForDelete;
			public readonly ICollection CompleteListForSave;
			public readonly ICollection FullListForDelete;
			public readonly NestedParentMap Map;
			public readonly List<object> ParentsToSave;
			public CommitChangesMode Mode;
			public ObjectSet LockedParentsObjects;
			public readonly SecurityContext SecurityContext;
			ObjectDictionary<XPMemberInfo[]> membersNotToSaveDictionary;
			public bool IsMembersNotToSaveDictionaryExists {
				get { return membersNotToSaveDictionary != null; }
			}
			public ObjectDictionary<XPMemberInfo[]> MembersNotToSaveDictionary {
				get {
					if(membersNotToSaveDictionary == null) {
						membersNotToSaveDictionary = new ObjectDictionary<XPMemberInfo[]>();
					}
					return membersNotToSaveDictionary;
				}
			}
			public AsyncCommitContext(Session session, ICollection fullListForDelete, ICollection readyListForDelete, ICollection completeListForSave, NestedParentMap map, SecurityContext securityContext) {
				this.Session = session;
				this.ReadyListForDelete = readyListForDelete;
				this.CompleteListForSave = completeListForSave;
				this.FullListForDelete = fullListForDelete;
				this.Map = map;
				this.ParentsToSave = new List<object>();
				this.LockedParentsObjects = new ObjectSet();
				this.SecurityContext = securityContext;
			}
		}
		ICollection FilterListForDelete(Session session, NestedParentMap map, ICollection fullListForDelete) {
			if(fullListForDelete == null) return null;
			if(fullListForDelete.Count == 0) return fullListForDelete;
			List<object> result = new List<object>(fullListForDelete.Count / 2);
			foreach(object objToDelete in fullListForDelete) {
				if(session.IsNewObject(objToDelete)) {
					object parent = NestedWorksHelper.GetParentObject(session, ParentSession, map, objToDelete);
					if(parent == null) continue;
				}
				result.Add(objToDelete);
			}
			return result;
		}
		void InvalidateDeletedObjects(AsyncCommitContext context) {
			foreach(object obj in context.FullListForDelete) {
				if(!context.Session.IsNewObject(obj)) {
					SessionIdentityMap.UnregisterObject(context.Session, obj);
				}
				context.Map.KickOut(obj);
				IXPInvalidateableObject spoilableObject = obj as IXPInvalidateableObject;
				if(spoilableObject != null) {
					spoilableObject.Invalidate();
				}
			}
		}
		public void CommitChanges(Session session, ICollection fullListForDelete, ICollection completeListForSave) {
			NestedParentMap map = GetNestedParentMap(session);
			ICollection readyListForDelete = FilterListForDelete(session, map, fullListForDelete);
			AsyncCommitContext context = new AsyncCommitContext(session, fullListForDelete, readyListForDelete, completeListForSave, map, GetSecurityContext(session));
			BeginCommitChanges(context);
			EndCommitChanges(context);
			if(ThroughCommitMode && (context.Mode == CommitChangesMode.NotInTransactionUnitOfWork || context.Mode == CommitChangesMode.InTransaction)) {
				if(ParentSession.TrackingChanges) {
					ParentSession.FlushChanges();
				}
				ThroughCommitExec(context);
			}
			InvalidateDeletedObjects(context);
		}
		public async Task CommitChangesAsync(Session session, ICollection fullListForDelete, ICollection completeListForSave, CancellationToken cancellationToken = default(CancellationToken)) {
			NestedParentMap map = GetNestedParentMap(session);
			ICollection readyListForDelete = FilterListForDelete(session, map, fullListForDelete);
			AsyncCommitContext context = new AsyncCommitContext(session, fullListForDelete, readyListForDelete, completeListForSave, map, GetSecurityContext(session));
			BeginCommitChanges(context);
			await EndCommitChangesAsync(context, cancellationToken);
			if(ThroughCommitMode && (context.Mode == CommitChangesMode.NotInTransactionUnitOfWork || context.Mode == CommitChangesMode.InTransaction)) {
				if(ParentSession.TrackingChanges) {
					await ParentSession.FlushChangesAsync(cancellationToken);
				}
				ThroughCommitExec(context);
			}
			InvalidateDeletedObjects(context);
		}
		public object CommitChangesAsync(Session session, ICollection fullListForDelete, ICollection completeListForSave, AsyncCommitCallback callback) {
			SynchronizationContext syncContext = AsyncOperationsHelper.CaptureSynchronizationContextOrFail();
			Guard.ArgumentNotNull(callback, nameof(callback));
			NestedParentMap map = GetNestedParentMap(session);
			ICollection readyListForDelete = FilterListForDelete(session, map, fullListForDelete);
			AsyncCommitContext context = new AsyncCommitContext(session, fullListForDelete, readyListForDelete, completeListForSave, map, GetSecurityContext(session));
			BeginCommitChanges(context);
			return new AsyncRequest(syncContext, new AsyncRequestExec(delegate (AsyncRequest request) {
				try {
					session.AsyncExecuteQueue.Invoke(request.SyncContext, new SendOrPostCallback(delegate (object obj) {
						try {
							EndCommitChanges(context);
							if(ThroughCommitMode && (context.Mode == CommitChangesMode.NotInTransactionUnitOfWork || context.Mode == CommitChangesMode.InTransaction)) {
								if(ParentSession.TrackingChanges) {
									request.AddNestedRequest(ParentSession.CommitTransactionAsync(new AsyncCommitCallback(delegate (Exception parentEx) {
										try {
											ThroughCommitExec(context);
											InvalidateDeletedObjects(context);
											callback(null);
										}
										catch(Exception ex) {
											try {
												callback(ex);
											}
											catch(Exception) { }
										}
									})) as AsyncRequest);
									return;
								}
								ThroughCommitExec(context);
							}
							InvalidateDeletedObjects(context);
							callback(null);
						}
						catch(Exception ex) {
							try {
								callback(ex);
							}
							catch(Exception) { }
						}
					}), null, true);
				}
				catch(Exception ex) {
					try {
						session.AsyncExecuteQueue.Invoke(request.SyncContext, new SendOrPostCallback(delegate (object obj) {
							try {
								callback(ex);
							}
							catch(Exception) { }
						}), request, true);
					}
					catch(Exception) { }
				}
			})).Start(session.AsyncExecuteQueue);
		}
		void ThroughCommitExec(AsyncCommitContext context) {
			SessionStateStack.Enter(context.Session, SessionState.ApplyIdentities);
			try {
				foreach(object objToSave in context.CompleteListForSave) {
					XPClassInfo ci = context.Session.GetClassInfo(objToSave);
					object parentObj = NestedWorksHelper.GetParentObject(context.Session, ParentSession, context.Map, objToSave);
					if(parentObj == null) continue;
					if(context.Session.IsNewObject(objToSave)) {
						object key = ci.KeyProperty.GetValue(parentObj);
						ci.KeyProperty.SetValue(objToSave, key);
						SessionIdentityMap.RegisterObject(context.Session, objToSave, ci.KeyProperty.ExpandId(key)); 
					}
					XPMemberInfo olf = ci.OptimisticLockField;
					if(olf == null) continue;
					olf.SetValue(objToSave, olf.GetValue(parentObj));
				}
			}
			finally {
				SessionStateStack.Leave(context.Session, SessionState.ApplyIdentities);
			}
		}
		void BeginCommitChanges(AsyncCommitContext context) {
			if(context.Session.LockingOption != LockingOption.None) {
				NestedWorksHelper.ValidateVersions(context.Session, ParentSession, GetNestedParentMap(context.Session), context.LockedParentsObjects, context.ReadyListForDelete, context.Session.LockingOption, true);
				NestedWorksHelper.ValidateVersions(context.Session, ParentSession, GetNestedParentMap(context.Session), context.LockedParentsObjects, context.CompleteListForSave, context.Session.LockingOption, false);
			}
			if(context.SecurityContext != null && !((context.SecurityContext.SecurityRuleProvider is ISecurityRuleProvider2) && ((ISecurityRuleProvider2)context.SecurityContext.SecurityRuleProvider).Enabled)) {
				ValidateObjectsOnCommit(context);
			}
			if(!ParentSession.TrackingChanges) {
				if(ParentSession.IsUnitOfWork)
					context.Mode = CommitChangesMode.NotInTransactionUnitOfWork;
				else
					context.Mode = CommitChangesMode.NotInTransactionSession;
				ParentSession.BeginTrackingChanges();
			}
			else
				context.Mode = CommitChangesMode.InTransaction;
			SessionStateStack.Enter(context.Session, SessionState.CommitChangesToDataLayerInner);
			SessionStateStack.Enter(ParentSession, SessionState.ReceivingObjectsFromNestedUow);
		}
		void EndCommitChanges(AsyncCommitContext context) {
			switch(context.Mode) {
				case CommitChangesMode.InTransaction:
				case CommitChangesMode.NotInTransactionUnitOfWork:
					Commit(context);
					break;
				case CommitChangesMode.NotInTransactionSession: {
					try {
						Commit(context);
						ParentSession.FlushChanges();
					}
					catch(Exception e) {
						if(ParentSession.TrackingChanges) {
							try {
								ParentSession.DropChanges();
							}
							catch(Exception e2) {
								throw new ExceptionBundleException(e, e2);
							}
						}
						throw;
					}
				}
				break;
			}
			GC.KeepAlive(context.LockedParentsObjects);
		}
		async Task EndCommitChangesAsync(AsyncCommitContext context, CancellationToken cancellationToken = default(CancellationToken)) {
			switch(context.Mode) {
				case CommitChangesMode.InTransaction:
				case CommitChangesMode.NotInTransactionUnitOfWork:
					Commit(context);
					break;
				case CommitChangesMode.NotInTransactionSession: {
					try {
						Commit(context);
						cancellationToken.ThrowIfCancellationRequested();
						await ParentSession.FlushChangesAsync(cancellationToken);
					}
					catch(Exception e) {
						if(ParentSession.TrackingChanges) {
							try {
								ParentSession.DropChanges();
							}
							catch(Exception e2) {
								throw new ExceptionBundleException(e, e2);
							}
						}
						throw;
					}
				}
				break;
			}
			GC.KeepAlive(context.LockedParentsObjects);
		}
		void Commit(AsyncCommitContext context) {
			bool abortChangesInParentSession = false;
			try {
				ISecurityRuleProvider2 ruleProvider2 = null;
				if(context.SecurityContext != null && (context.SecurityContext.SecurityRuleProvider is ISecurityRuleProvider2)) {
					ruleProvider2 = (ISecurityRuleProvider2)context.SecurityContext.SecurityRuleProvider;
				}
				if(ruleProvider2 != null && ruleProvider2.Enabled) {
					ValidateObjectOnCommitGenericSecurityRule(context);
					if(context.SecurityContext != null) {
						foreach(object objToDel in context.ReadyListForDelete) {
							ValidateDeleteObjects(context, ruleProvider2, objToDel);
						}
					}
					Dictionary<object, XPMemberInfo[]> membersDoNotSaveDictionary = new Dictionary<object, XPMemberInfo[]>();
					foreach(object objToSave in context.CompleteListForSave) {
						XPMemberInfo[] membersDoNotSave = Array.Empty<XPMemberInfo>();
						XPClassInfo ci = context.Session.GetClassInfo(objToSave);
						ISecurityRule2 rulev2 = ruleProvider2.GetRule(ci);
						if(rulev2 != null) {
							object realObject = NestedWorksHelper.GetParentObject(context.Session, ParentSession, context.Map, objToSave);
							if(realObject != null) {
								if(context.Session.IsObjectMarkedDeleted(objToSave)) {
									if(!rulev2.ValidateObjectOnDelete(context.SecurityContext, ci, realObject)) {
										throw new ObjectLayerSecurityException(ci.FullName, true);
									}
								}
								else {
									membersDoNotSave = ValidateBeforeCommitObject(ruleProvider2, rulev2, context, objToSave, realObject);
								}
							}
							membersDoNotSaveDictionary.Add(objToSave, membersDoNotSave);
						}
					}
					foreach(object objToDel in context.ReadyListForDelete) {
						NestedWorksHelper.CommitDeletedObject(context.Session, ParentSession, context.Map, objToDel);
					}
					ObjectDictionary<Dictionary<XPMemberInfo, object>> modifyMemberDictionary = new ObjectDictionary<Dictionary<XPMemberInfo, object>>();
					foreach(object objToSave in context.CompleteListForSave) {
						XPMemberInfo[] membersNotToSave = null;
						membersDoNotSaveDictionary.TryGetValue(objToSave, out membersNotToSave);
						Dictionary<XPMemberInfo, object> modifyMembers = null;
						object objectToSave = NestedWorksHelper.CommitObject2(context.Session, ParentSession, context.Map, objToSave, membersNotToSave, out modifyMembers);
						modifyMemberDictionary.Add(objectToSave, modifyMembers);
						context.ParentsToSave.Add(objectToSave);
					}
					foreach(object realObjectToSave in context.ParentsToSave) {
						if(!ParentSession.IsObjectMarkedDeleted(realObjectToSave)) {
							try {
								ValidateAfterCommitObject(ruleProvider2, context, realObjectToSave, modifyMemberDictionary[realObjectToSave], ref abortChangesInParentSession);
							}
							catch(Exception) {
								abortChangesInParentSession = true;
								RollbackParentObjects(context, modifyMemberDictionary);
								throw;
							}
						}
					}
					if(context.SecurityContext != null) {
						foreach(object objToDel in context.ReadyListForDelete) {
							if(objToDel is IntermediateObject) {
								ValidateDeleteObjects(context, ruleProvider2, objToDel);
							}
						}
					}
					ParentSession.Save(context.ParentsToSave);
				}
				else {
					foreach(object objToDel in context.ReadyListForDelete) {
						NestedWorksHelper.CommitDeletedObject(context.Session, ParentSession, context.Map, objToDel);
					}
					foreach(object objToSave in context.CompleteListForSave) {
						XPMemberInfo[] membersNotToSave = null;
						if(context.SecurityContext != null && context.IsMembersNotToSaveDictionaryExists)
							context.MembersNotToSaveDictionary.TryGetValue(objToSave, out membersNotToSave);
						context.ParentsToSave.Add(NestedWorksHelper.CommitObject(context.Session, ParentSession, context.Map, objToSave, membersNotToSave));
					}
					ParentSession.Save(context.ParentsToSave);
				}
			}
			finally {
				SessionStateStack.Leave(ParentSession, SessionState.ReceivingObjectsFromNestedUow);
				SessionStateStack.Leave(context.Session, SessionState.CommitChangesToDataLayerInner);
				if(abortChangesInParentSession) {
					if(ParentSession.IsUnitOfWork || ParentSession.TrackingChanges) {
						ParentSession.DropChanges();
					}
					context.ParentsToSave.Clear();
				}
			}
			ParentSession.TriggerObjectsLoaded(context.ParentsToSave);
		}
		private void ValidateDeleteObjects(AsyncCommitContext context, ISecurityRuleProvider2 ruleProvider2, object objToDel) {
			object realObject = NestedWorksHelper.GetParentObject(context.Session, ParentSession, context.Map, objToDel);
			ValidateDeleteObjectsOnCommit(ruleProvider2, context, realObject);
		}
		void ValidateObjectsOnCommit(AsyncCommitContext context) {
			SecurityContext securityContext = context.SecurityContext;
			SecurityContextValidateItem[] objectsForSave = new SecurityContextValidateItem[context.CompleteListForSave.Count];
			SecurityContextValidateItem[] objectsForDelete = new SecurityContextValidateItem[context.ReadyListForDelete.Count];
			int counter = 0;
			foreach(object objToSave in context.CompleteListForSave) {
				objectsForSave[counter++] = new SecurityContextValidateItem(context.Session.GetClassInfo(objToSave), objToSave,
					NestedWorksHelper.GetParentObject(context.Session, ParentSession, context.Map, objToSave));
			}
			counter = 0;
			foreach(object objToDelete in context.ReadyListForDelete) {
				objectsForDelete[counter++] = new SecurityContextValidateItem(context.Session.GetClassInfo(objToDelete), objToDelete,
					NestedWorksHelper.GetParentObject(context.Session, ParentSession, context.Map, objToDelete));
			}
			if(securityContext.GenericSecurityRule != null
				&& !securityContext.GenericSecurityRule.ValidateObjectsOnCommit(securityContext, objectsForSave, objectsForDelete))
				throw new ObjectLayerSecurityException();
			for(int i = 0; i < objectsForSave.Length; i++) {
				SecurityContextValidateItem saveItem = objectsForSave[i];
				XPClassInfo ci = saveItem.ClassInfo;
				ISecurityRule rule = securityContext.SecurityRuleProvider.GetRule(ci);
				if(rule != null) {
					if(!rule.ValidateObjectOnSave(securityContext, ci, saveItem.TheObject, saveItem.RealObject))
						throw new ObjectLayerSecurityException(ci.FullName, false);
					List<XPMemberInfo> membersNotToSave = null;
					IEnumerable<string> securityBypassProperties = GetPropertiesForBypassSecurity(saveItem.TheObject);
					foreach(XPMemberInfo mi in ci.PersistentProperties) {
						if(mi.IsDelayed && !XPDelayedProperty.GetDelayedPropertyContainer(saveItem.TheObject, mi).IsLoaded)
							continue;
						if(mi.IsReadOnly)
							continue;
						if(mi.IsFetchOnly)
							continue;
						object value = mi.GetValue(saveItem.TheObject);
						if(SecurityContext.IsSystemProperty(mi)) {
							continue;
						}
						object oldValue = null;
						object realOldValue = null;
						if(saveItem.RealObject != null) {
							realOldValue = mi.GetValue(saveItem.RealObject);
							CriteriaOperator memberExpression;
							if(rule.GetSelectMemberExpression(securityContext, ci, mi, out memberExpression)) {
								oldValue = securityContext.Evaluate(AnalyzeCriteriaCreator.GetUpClass(ci, mi.Owner), memberExpression, saveItem.RealObject);
							}
							else {
								oldValue = realOldValue;
							}
						}
						if(mi.ReferenceType != null && oldValue != null) {
							ISecurityRule referenceRule = securityContext.SecurityRuleProvider.GetRule(mi.ReferenceType);
							if(referenceRule != null) {
								oldValue = referenceRule.ValidateObjectOnSelect(securityContext, mi.ReferenceType, oldValue) ? oldValue : null;
							}
						}
						if(!securityBypassProperties.Contains(mi.Name)) {
							switch(rule.ValidateMemberOnSave(securityContext, mi, saveItem.TheObject, saveItem.RealObject, value, oldValue, realOldValue)) {
								case ValidateMemberOnSaveResult.DoRaiseException:
									throw new ObjectLayerSecurityException(saveItem.ClassInfo.FullName, mi.Name);
								case ValidateMemberOnSaveResult.DoNotSaveMember:
									if(membersNotToSave == null) membersNotToSave = new List<XPMemberInfo>();
									membersNotToSave.Add(mi);
									break;
							}
						}
					}
					if(membersNotToSave != null && membersNotToSave.Count > 0) {
						context.MembersNotToSaveDictionary.Add(saveItem.TheObject, membersNotToSave.ToArray());
					}
				}
			}
			for(int i = 0; i < objectsForDelete.Length; i++) {
				SecurityContextValidateItem deleteItem = objectsForDelete[i];
				ISecurityRule rule = securityContext.SecurityRuleProvider.GetRule(deleteItem.ClassInfo);
				if(rule != null && !rule.ValidateObjectOnDelete(securityContext, deleteItem.ClassInfo, deleteItem.TheObject, deleteItem.RealObject)) {
					throw new ObjectLayerSecurityException(deleteItem.ClassInfo.FullName, true);
				}
			}
		}
		private void ValidateAfterCommitObject(ISecurityRuleProvider2 ruleProvider2, AsyncCommitContext context, object realObject, Dictionary<XPMemberInfo, object> modifyMember, ref bool abortChangesInParentSession) {
			Guard.ArgumentNotNull(realObject, "realObject");
			Guard.ArgumentNotNull(ruleProvider2, "ruleProvider2");
			XPClassInfo ci = context.Session.GetClassInfo(realObject);
			ISecurityRule2 rulev2 = ruleProvider2.GetRule(ci);
			SecurityContext securityContext = context.SecurityContext;
			if(securityContext != null) {
				if(!rulev2.ValidateObjectOnSave(securityContext, realObject)) {
					throw new ObjectLayerSecurityException(ci.FullName, false);
				}
				var objectToSave = context.Map.GetNested(realObject);
				IEnumerable<string> securityBypassProperties = GetPropertiesForBypassSecurity(objectToSave);
				foreach(KeyValuePair<XPMemberInfo, object> keyMi in modifyMember) {
					if(keyMi.Key.IsDelayed && !XPDelayedProperty.GetDelayedPropertyContainer(realObject, keyMi.Key).IsLoaded)
						continue;
					if(keyMi.Key.IsReadOnly)
						continue;
					if(SecurityContext.IsSystemProperty(keyMi.Key)) {
						continue;
					}
					if(!securityBypassProperties.Contains(keyMi.Key.Name)
						&& !rulev2.ValidateMemberOnSave(securityContext, keyMi.Key, realObject)) {
						throw new ObjectLayerSecurityException(ci.FullName, keyMi.Key.Name);
					}
				}
			}
		}
		private void RollbackParentObjects(AsyncCommitContext context, ObjectDictionary<Dictionary<XPMemberInfo, object>> modifiedObjects) {
			RollbackDeletedObjects(context);
			RollbackModifiedObjects(context, modifiedObjects);
			RemoveParentObjects(context);
		}
		private void RollbackModifiedObjects(AsyncCommitContext context, ObjectDictionary<Dictionary<XPMemberInfo, object>> modifiedObjects) {
			NestedParentMap nestedParentMap = GetNestedParentMap(context.Session);
			foreach(KeyValuePair<object, Dictionary<XPMemberInfo, object>> modifiedMember in modifiedObjects) {
				object parentObject = modifiedMember.Key;
				if(parentObject is IntermediateObject) {
					IntermediateObject intParent = (IntermediateObject)parentObject;
					IntermediateClassInfo intObjClassInfo = (IntermediateClassInfo)ParentSession.GetClassInfo(intParent);
					if(intParent.LeftIntermediateObjectField != null && intParent.RightIntermediateObjectField != null) {
						XPBaseCollection leftC = (XPBaseCollection)intObjClassInfo.intermediateObjectFieldInfoRight.refProperty.GetValue(intParent.LeftIntermediateObjectField);
						leftC.BaseRemove(intParent.RightIntermediateObjectField);
					}
				}
				foreach(KeyValuePair<XPMemberInfo, object> keyMi in modifiedMember.Value) {
					XPMemberInfo member = keyMi.Key;
					object memberValue = keyMi.Value;
					if(member.ReferenceType != null) {
						object oldValue = member.GetValue(parentObject);
						if(!Equals(oldValue, memberValue)) {
							member.SetValue(parentObject, memberValue);
						}
						member.ProcessAssociationRefChange(ParentSession, parentObject, oldValue, memberValue);
					}
					else {
						member.SetValue(parentObject, memberValue);
					}
				}
				object nestedObj = nestedParentMap.GetNested(modifiedMember.Key);
				XPClassInfo ciObj = context.Session.GetClassInfo(nestedObj);
				if(ciObj.OptimisticLockField != null && ciObj.OptimisticLockFieldInDataLayer != null) {
					object realVal = ciObj.OptimisticLockFieldInDataLayer.GetValue(nestedObj);
					ciObj.OptimisticLockField.SetValue(modifiedMember.Key, realVal);
				}
				nestedParentMap.KickOut(modifiedMember.Key);
			}
		}
		private void RollbackDeletedObjects(AsyncCommitContext context) {
			foreach(object objToDelete in context.ReadyListForDelete) {
				object parentObj = NestedWorksHelper.GetParentObject(context.Session, ParentSession, context.Map, objToDelete);
				if(parentObj == null)
					continue;
				XPClassInfo ci = context.Session.GetClassInfo(objToDelete);
				bool trackModifications = ci.TrackPropertiesModifications ?? ParentSession.TrackPropertiesModifications;
				bool nestedTrackModifications = ci.TrackPropertiesModifications ?? context.Session.TrackPropertiesModifications;
				foreach(XPMemberInfo mi in ci.PersistentProperties) {
					if(nestedTrackModifications && trackModifications && mi.GetModified(objToDelete) && mi.GetModified(parentObj)) {
						mi.ResetModified(parentObj);
					}
				}
				if(parentObj is IntermediateObject) {
					IntermediateObject intParent = (IntermediateObject)parentObj;
					IntermediateClassInfo intObjClassInfo = (IntermediateClassInfo)ParentSession.GetClassInfo(intParent);
					if(intParent.LeftIntermediateObjectField != null && intParent.RightIntermediateObjectField != null) {
						XPBaseCollection leftC = (XPBaseCollection)intObjClassInfo.intermediateObjectFieldInfoRight.refProperty.GetValue(intParent.LeftIntermediateObjectField);
						leftC.BaseAdd(intParent.RightIntermediateObjectField);
					}
				}
			}
		}
		private void RemoveParentObjects(AsyncCommitContext context) {
			NestedParentMap nestedParentMap = GetNestedParentMap(context.Session);
			foreach(object obj in context.CompleteListForSave) {
				nestedParentMap.KickOut(obj);
			}
		}
		public object GetParentObject(Session session, object obj) {
			var map = GetNestedParentMap(session);
			var parent = map.GetParent(obj);
			return parent;
		}
		private XPMemberInfo[] ValidateBeforeCommitObject(ISecurityRuleProvider2 securityRuleProvider2, ISecurityRule2 rulev2, AsyncCommitContext context, object objToSave, object realObject) {
			Guard.ArgumentNotNull(realObject, "realObject");
			Guard.ArgumentNotNull(objToSave, "objToSave");
			Guard.ArgumentNotNull(rulev2, "rulev2");
			List<XPMemberInfo> membersDoNotSave = new List<XPMemberInfo>();
			SecurityContext securityContext = context.SecurityContext;
			if(securityContext != null) {
				XPClassInfo ci = context.Session.GetClassInfo(objToSave);
				if(!rulev2.ValidateObjectOnSave(securityContext, realObject)) {
					throw new ObjectLayerSecurityException(ci.FullName, false);
				}
				IEnumerable<string> securityBypassProperties = GetPropertiesForBypassSecurity(objToSave);
				foreach(XPMemberInfo mi in ci.PersistentProperties) {
					if(mi.IsDelayed && !XPDelayedProperty.GetDelayedPropertyContainer(objToSave, mi).IsLoaded)
						continue;
					if(mi.IsReadOnly)
						continue;
					if(mi.IsFetchOnly)
						continue;
					if(SecurityContext.IsSystemProperty(mi)) {
						continue;
					}
					object value = mi.GetValue(objToSave);
					object oldValue = null;
					object realOldValue = null;
					realOldValue = mi.GetValue(realObject);
					CriteriaOperator memberExpression;
					if(rulev2.GetSelectMemberExpression(securityContext, ci, mi, out memberExpression)) {
						oldValue = securityContext.Evaluate(AnalyzeCriteriaCreator.GetUpClass(ci, mi.Owner), memberExpression, realObject);
					}
					else {
						oldValue = realOldValue;
					}
					if(mi.ReferenceType != null && oldValue != null) {
						ISecurityRule2 referenceRule2 = securityRuleProvider2.GetRule(mi.ReferenceType);
						if(referenceRule2 != null) {
							oldValue = referenceRule2.ValidateObjectOnSelect(securityContext, mi.ReferenceType, oldValue) ? oldValue : null;
						}
						else {
							ISecurityRule referenceRule = securityContext.SecurityRuleProvider.GetRule(mi.ReferenceType);
							if(referenceRule != null) {
								oldValue = referenceRule.ValidateObjectOnSelect(securityContext, mi.ReferenceType, oldValue) ? oldValue : null;
							}
						}
					}
					if(!PersistentPropertiesEquals(context, mi, value, oldValue) && !rulev2.IsDoNotSaveMember(securityContext, realObject, mi, value, oldValue)) {
						if(!securityBypassProperties.Contains(mi.Name) && !rulev2.ValidateMemberOnSave(securityContext, mi, realObject)) {
							throw new ObjectLayerSecurityException(ci.FullName, mi.Name);
						}
					}
					else {
						membersDoNotSave.Add(mi);
					}
				}
			}
			return membersDoNotSave.ToArray();
		}
		bool PersistentPropertiesEquals(AsyncCommitContext context, XPMemberInfo mi, object value, object valueOnLoad) {
			if(mi.ReferenceType != null && valueOnLoad != null && value != null) {
				value = NestedWorksHelper.GetParentObject(context.Session, ParentSession, context.Map, value);
			}
			return Equals(valueOnLoad, value);
		}
		void ValidateObjectOnCommitGenericSecurityRule(AsyncCommitContext context) {
			SecurityContext securityContext = context.SecurityContext;
			SecurityContextValidateItem[] objectsForSave = new SecurityContextValidateItem[context.CompleteListForSave.Count];
			SecurityContextValidateItem[] objectsForDelete = new SecurityContextValidateItem[context.ReadyListForDelete.Count];
			int counter = 0;
			foreach(object objToSave in context.CompleteListForSave) {
				objectsForSave[counter++] = new SecurityContextValidateItem(context.Session.GetClassInfo(objToSave), objToSave,
					NestedWorksHelper.GetParentObject(context.Session, ParentSession, context.Map, objToSave));
			}
			counter = 0;
			foreach(object objToDelete in context.ReadyListForDelete) {
				objectsForDelete[counter++] = new SecurityContextValidateItem(context.Session.GetClassInfo(objToDelete), objToDelete,
					NestedWorksHelper.GetParentObject(context.Session, ParentSession, context.Map, objToDelete));
			}
			if(securityContext.GenericSecurityRule != null
				&& !securityContext.GenericSecurityRule.ValidateObjectsOnCommit(securityContext, objectsForSave, objectsForDelete))
				throw new ObjectLayerSecurityException();
		}
		void ValidateDeleteObjectsBeforeCommit(ISecurityRuleProvider2 securityRuleProvider2, AsyncCommitContext context, object realObject) {
			SecurityContext securityContext = context.SecurityContext;
			XPClassInfo ci = context.Session.GetClassInfo(realObject);
			ISecurityRule2 rule = securityRuleProvider2.GetRule(ci);
			if(rule != null && !rule.ValidateObjectOnDelete(securityContext, ci, realObject)) {
				throw new ObjectLayerSecurityException(ci.FullName, true);
			}
		}
		void ValidateDeleteObjectsOnCommit(ISecurityRuleProvider2 securityRuleProvider2, AsyncCommitContext context, object realObject) {
			SecurityContext securityContext = context.SecurityContext;
			XPClassInfo ci = context.Session.GetClassInfo(realObject);
			ISecurityRule2 rule = securityRuleProvider2?.GetRule(ci);
			if(rule != null && !rule.ValidateObjectOnDelete(securityContext, ci, realObject)) {
				throw new ObjectLayerSecurityException(ci.FullName, true);
			}
		}
		public void CreateObjectType(XPObjectType type) {
			ParentSession.ObjectLayer.CreateObjectType(type);
		}
		public Task CreateObjectTypeAsync(XPObjectType type, CancellationToken cancellationToken) {
			var objLayerAsync = ParentSession.ObjectLayer as IObjectLayerAsync;
			if(objLayerAsync == null) {
				throw new NotSupportedException(Res.GetString(Res.Async_ObjectLayerDoesNotImplementIObjectLayerAsync, ParentSession.ObjectLayer));
			}
			return objLayerAsync.CreateObjectTypeAsync(type, cancellationToken);
		}
		public ICollection[] GetObjectsByKey(Session session, ObjectsByKeyQuery[] queries) {
			if(queries == null || queries.Length == 0) return Array.Empty<object[]>();
			ICollection[] parentObjects = ParentSession.GetObjectsByKey(queries, false);
			bool[] force = new bool[parentObjects.Length];
			for(int i = 0; i < force.Length; i++) force[i] = true;
			SecurityContext securityContext = GetSecurityContext(session);
			NestedParentMap nestedParentMap = GetNestedParentMap(session);
			ICollection[] nestedObjects = new NestedLoader(session, ParentSession, nestedParentMap, securityContext).GetNestedObjects(parentObjects, force);
			OnLoadedObjects(parentObjects, nestedObjects, nestedParentMap, securityContext);
			return nestedObjects;
		}
		public async Task<ICollection[]> GetObjectsByKeyAsync(Session session, ObjectsByKeyQuery[] queries, CancellationToken cancellationToken = default(CancellationToken)) {
			if(queries == null || queries.Length == 0) return Array.Empty<object[]>();
			cancellationToken.ThrowIfCancellationRequested();
			ICollection[] parentObjects = await ParentSession.GetObjectsByKeyAsync(queries, false, cancellationToken);
			bool[] force = new bool[parentObjects.Length];
			for(int i = 0; i < force.Length; i++) force[i] = true;
			SecurityContext securityContext = GetSecurityContext(session);
			NestedParentMap nestedParentMap = GetNestedParentMap(session);
			int asyncOperationId = SessionStateStack.GetNewAsyncOperationId();
			ICollection[] nestedObjects = await (new NestedLoader(session, ParentSession, nestedParentMap, securityContext)).GetNestedObjectsAsync(parentObjects, force, asyncOperationId, cancellationToken);
			OnLoadedObjects(parentObjects, nestedObjects, nestedParentMap, securityContext);
			return nestedObjects;
		}
		public object[] LoadDelayedProperties(Session session, object theObject, MemberPathCollection props) {
			object[] result = new object[props.Count];
			SecurityContext securityContext = GetSecurityContext(session);
			object parentObject = NestedWorksHelper.GetParentObject(session, ParentSession, GetNestedParentMap(session), theObject);
			for(int i = 0; i < props.Count; ++i) {
				XPMemberInfo mi = props[i][0];
				if(securityContext != null) {
					result[i] = securityContext.GetValueBySecurityRule(parentObject, mi);
				}
				else {
					result[i] = mi.GetValue(parentObject);
				}
			}
			return result;
		}
		public async Task<object[]> LoadDelayedPropertiesAsync(Session session, object theObject, MemberPathCollection props, CancellationToken cancellationToken = default(CancellationToken)) {
			object[] result = new object[props.Count];
			SecurityContext securityContext = GetSecurityContext(session);
			object parentObject = await NestedWorksHelper.GetParentObjectAsync(session, ParentSession, GetNestedParentMap(session), theObject, cancellationToken);
			for(int i = 0; i < props.Count; ++i) {
				XPMemberInfo mi = props[i][0];
				if(securityContext != null) {
					result[i] = securityContext.GetValueBySecurityRule(parentObject, mi);
				}
				else {
					result[i] = mi.GetValue(parentObject);
				}
			}
			return result;
		}
		public ObjectDictionary<object> LoadDelayedProperties(Session session, IList objects, XPMemberInfo property) {
			ObjectDictionary<object> result = new ObjectDictionary<object>(objects.Count);
			SecurityContext securityContext = GetSecurityContext(session);
			foreach(object obj in objects) {
				object parentObj = NestedWorksHelper.GetParentObject(session, ParentSession, GetNestedParentMap(session), obj);
				if(securityContext != null) {
					result.Add(obj, securityContext.GetValueBySecurityRule(parentObj, property));
				}
				else {
					result.Add(obj, property.GetValue(parentObj));
				}
			}
			return result;
		}
		public async Task<ObjectDictionary<object>> LoadDelayedPropertiesAsync(Session session, IList objects, XPMemberInfo property, CancellationToken cancellationToken = default(CancellationToken)) {
			ObjectDictionary<object> result = new ObjectDictionary<object>(objects.Count);
			SecurityContext securityContext = GetSecurityContext(session);
			foreach(object obj in objects) {
				object parentObj = await NestedWorksHelper.GetParentObjectAsync(session, ParentSession, GetNestedParentMap(session), obj, cancellationToken);
				if(securityContext != null) {
					result.Add(obj, securityContext.GetValueBySecurityRule(parentObj, property));
				}
				else {
					result.Add(obj, property.GetValue(parentObj));
				}
			}
			return result;
		}
		public PurgeResult Purge() {
			return ParentSession.PurgeDeletedObjects();
		}
		public void SetObjectLayerWideObjectTypes(Dictionary<XPClassInfo, XPObjectType> loadedTypes) {
			ParentSession.ObjectLayer.SetObjectLayerWideObjectTypes(loadedTypes);
		}
		public Dictionary<XPClassInfo, XPObjectType> GetObjectLayerWideObjectTypes() {
			return ParentSession.ObjectLayer.GetObjectLayerWideObjectTypes();
		}
		public void RegisterStaticTypes(params XPClassInfo[] types) {
			ParentSession.ObjectLayer.RegisterStaticTypes(types);
		}
		public bool IsStaticType(XPClassInfo type) {
			return ParentSession.ObjectLayer.IsStaticType(type);
		}
		public IObjectMap GetStaticCache(XPClassInfo info) {
			return ParentSession.ObjectLayer.GetStaticCache(info);
		}
#pragma warning disable 618
		public event SchemaInitEventHandler SchemaInit {
			add { ParentSession.SchemaInit += value; }
			remove { ParentSession.SchemaInit -= value; }
		}
#pragma warning restore 618
		[Description("Provides access to the current object layer’s IDbConnection object that is used to access a database.")]
		[Browsable(false)]
		public IDbConnection Connection {
			get { return null; }
		}
#if DEBUGTEST
		public IDbCommand CreateCommand() {
			throw new NotSupportedException();
		}
#endif
		[Description("Returns an AutoCreateOption value associated with the current object layer.")]
		[Browsable(false)]
		public AutoCreateOption AutoCreateOption {
			get { return ParentSession.AutoCreateOption; }
		}
		[Description("Returns the current SessionObjectLayer object.")]
		[Browsable(false)]
		public IObjectLayer ObjectLayer {
			get { return this; }
		}
		[Description("Gets an object providing metadata on persistent objects stored in a data store.")]
		[Browsable(false)]
		public XPDictionary Dictionary {
			get { return ParentSession.Dictionary; }
		}
		CriteriaOperator GetNestedCriteria(Session session, CriteriaOperator criteria, SecurityContext securityContext) {
			return ParentCriteriaGenerator.GetNestedCriteria(session, ParentSession, GetNestedParentMap(session), securityContext, criteria);
		}
		[Description("Provides access to the current object layer’s data access layer that is used to access a data store.")]
		[Browsable(false)]
		public IDataLayer DataLayer {
			get { return ParentSession.DataLayer; }
		}
		public bool IsParentObjectToSave(Session session, object theObject) {
			if(theObject == null) return false;
			object parent = NestedWorksHelper.GetParentObject(session, ParentSession, GetNestedParentMap(session), theObject);
			if(parent == null)
				return false;
			return ParentSession.IsObjectToSave(parent, true);
		}
		public bool IsParentObjectToDelete(Session session, object theObject) {
			if(theObject == null) return false;
			object parent = NestedWorksHelper.GetParentObject(session, ParentSession, GetNestedParentMap(session), theObject);
			if(parent == null)
				return false;
			return ParentSession.IsObjectToDelete(parent, true);
		}
		public ICollection GetParentObjectsToSave(Session session) {
			ICollection parentResult = ParentSession.GetObjectsToSave(true);
			object[] result = new object[parentResult.Count];
			int index = 0;
			SecurityContext securityContext = GetSecurityContext(session);
			foreach(object obj in parentResult) {
				result[index++] = NestedWorksHelper.GetNestedObject(session, ParentSession, GetNestedParentMap(session), obj, securityContext);
			}
			return result;
		}
		public ICollection GetParentObjectsToDelete(Session session) {
			ICollection parentResult = ParentSession.GetObjectsToDelete(true);
			object[] result = new object[parentResult.Count];
			int index = 0;
			SecurityContext securityContext = GetSecurityContext(session);
			foreach(object obj in parentResult) {
				result[index++] = NestedWorksHelper.GetNestedObject(session, ParentSession, GetNestedParentMap(session), obj, securityContext);
			}
			return result;
		}
		public ICollection GetParentTouchedClassInfos(Session session) {
			return ParentSession.GetTouchedClassInfosIncludeParent();
		}
		[Description("Indicates if an object layer can call the SessionObjectLayer.LoadCollectionObjects method to load collection properties.")]
		[Browsable(false)]
		public bool CanLoadCollectionObjects {
			get {
				return true;
			}
		}
		[Description("Indicates if an object layer can call the SessionObjectLayer.LoadCollectionObjectsAsync method to asynchronously load collection properties.")]
		[Browsable(false)]
		public bool CanLoadCollectionObjectsAsynchronously {
			get {
				return true;
			}
		}
		public object[] LoadCollectionObjects(Session session, XPMemberInfo refProperty, object owner) {
			object parent = NestedWorksHelper.GetParentObject(session, ParentSession, GetNestedParentMap(session), owner);
			if(parent == null) {
				return Array.Empty<object>();
			}
			else {
				XPBaseCollection nestedCollection = (XPBaseCollection)refProperty.GetValue(owner);
				XPBaseCollection parentCollection = (XPBaseCollection)refProperty.GetValue(parent);
				if(parentCollection.IsLoaded && (nestedCollection.IsLoaded || nestedCollection.ClearCount > 0))
					parentCollection.Reload();
				parentCollection.Load();
				object[] parentContent = ListHelper.FromCollection(parentCollection.Helper.IntObjList).ToArray();
				return ListHelper.FromCollection(new NestedLoader(session, ParentSession, GetNestedParentMap(session), GetSecurityContext(session)).GetNestedObjects(new ICollection[] { parentContent })[0]).ToArray();
			}
		}
		public async Task<object[]> LoadCollectionObjectsAsync(Session session, XPMemberInfo refProperty, object owner, CancellationToken cancellationToken = default(CancellationToken)) {
			object parent = await NestedWorksHelper.GetParentObjectAsync(session, ParentSession, GetNestedParentMap(session), owner, cancellationToken);
			if(parent == null) {
				return Array.Empty<object>();
			}
			else {
				XPBaseCollection nestedCollection = (XPBaseCollection)refProperty.GetValue(owner);
				XPBaseCollection parentCollection = (XPBaseCollection)refProperty.GetValue(parent);
				if(parentCollection.IsLoaded && (nestedCollection.IsLoaded || nestedCollection.ClearCount > 0))
					parentCollection.Reload();
				await parentCollection.LoadAsync(cancellationToken);
				object[] parentContent = ListHelper.FromCollection(parentCollection.Helper.IntObjList).ToArray();
				NestedLoader nestedLoader = new NestedLoader(session, ParentSession, GetNestedParentMap(session), GetSecurityContext(session));
				int asyncOperationId = SessionStateStack.GetNewAsyncOperationId();
				ICollection[] nestedObjects = await nestedLoader.GetNestedObjectsAsync(new ICollection[] { parentContent }, asyncOperationId, cancellationToken).ConfigureAwait(false);
				return ListHelper.FromCollection(nestedObjects[0]).ToArray();
			}
		}
		protected virtual void OnLoadedObjects(ICollection[] parentObjects, ICollection[] nestedObjects, NestedParentMap nestedParentMap, SecurityContext securityContext) {
			if(ObjectsLoaded != null) {
				ObjectsLoadedEventArgs objectsLoadedEventArgs = new ObjectsLoadedEventArgs(parentObjects, nestedObjects, nestedParentMap, securityContext);
				ObjectsLoaded(this, objectsLoadedEventArgs);
			}
		}
		protected virtual bool AllowICommandChannelDoWithSecurityContext { get { return false; } }
		object ICommandChannel.Do(string command, object args) {
			ThrowIfNotAllowICommandChannelDo();
			if(nestedCommandChannel == null) {
				if(ParentSession == null) {
					throw new NotSupportedException(string.Format(CommandChannelHelper.Message_CommandIsNotSupported, command));
				}
				else {
					throw new NotSupportedException(string.Format(CommandChannelHelper.Message_CommandIsNotSupportedEx, command, ParentSession.GetType().FullName));
				}
			}
			return nestedCommandChannel.Do(command, args);
		}
		Task<object> ICommandChannelAsync.DoAsync(string command, object args, CancellationToken cancellationToken) {
			ThrowIfNotAllowICommandChannelDo();
			if(nestedCommandChannelAsync == null) {
				if(nestedCommandChannel == null) {
					throw new NotSupportedException(string.Format(CommandChannelHelper.Message_CommandIsNotSupported, command));
				}
				else {
					throw new InvalidOperationException(Xpo.Res.GetString(Xpo.Res.Async_CommandChannelDoesNotImplementICommandChannelAsync, nestedCommandChannel.GetType().FullName));
				}
			}
			return nestedCommandChannelAsync.DoAsync(command, args, cancellationToken);
		}
		private void ThrowIfNotAllowICommandChannelDo() {
			if(SecurityContextMain != null && !AllowICommandChannelDoWithSecurityContext)
				throw new InvalidOperationException(Xpo.Res.GetString(Xpo.Res.Security_ICommandChannel_TransferringRequestsIsProhibited));
		}
		IEnumerable<string> GetPropertiesForBypassSecurity(object obj) {
#if NET
			if(this is IInfrastructure<ISecuredPropertyAccessor> infrastructure) {
				return infrastructure.Instance?.GetChangedSecuredProperties(obj);
			}
			return Array.Empty<string>();
#else
			return new string[0];
#endif
		}
	}
	public class ObjectsLoadedEventArgs : EventArgs {
		public ICollection ParentObjects { get; private set; }
		public ICollection NestedObjects { get; private set; }
		public NestedParentMap NestedParentMap { get; private set; }
		public SecurityContext SecurityContext { get; private set; }
		public ObjectsLoadedEventArgs(ICollection parentObjects, ICollection nestedObjects, NestedParentMap nestedParentMap, SecurityContext securityContext) {
			ParentObjects = parentObjects;
			NestedObjects = nestedObjects;
			NestedParentMap = nestedParentMap;
			SecurityContext = securityContext;
		}
	}
	public class CustomSecurityCriteriaPatcherEventArgs : HandledEventArgs {
		public XPClassInfo ClassInfo { get; set; }
		public SecurityContext SecurityContext { get; set; }
		public ISecurityCriteriaPatcher CustomSecurityCriteriaPatcher { get; set; }
		public CustomSecurityCriteriaPatcherEventArgs(XPClassInfo xPClassInfo, SecurityContext securityContext) {
			this.ClassInfo = xPClassInfo;
			this.SecurityContext = securityContext;
		}
	}
}
