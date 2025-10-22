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
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using DevExpress.Xpo;
using System.Collections;
using System.Collections.Generic;
using DevExpress.Xpo.Metadata;
using DevExpress.Data.Filtering;
using DevExpress.Data.Filtering.Helpers;
using DevExpress.Xpo.Helpers;
using DevExpress.Xpo.Metadata.Helpers;
using System.Threading;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.Exceptions;
using DevExpress.Data.Helpers;
using DevExpress.Utils;
using System.Threading.Tasks;
using DevExpress.Xpo.Infrastructure;
using System.Runtime.ExceptionServices;
namespace DevExpress.Xpo.Helpers {
	public class InTransactionLoader: IPersistentValueExtractor, IInfrastructure<IServiceProvider> {
		enum InTransactionState {
			AnalyzeAndExecQuery,
			AfterAnalyzeQueryDirectGetObjects,
			AfterAnalyzeQueryNullCriteria,
			GetOriginalObjects,
			AfterGetOriginalObjectsThroughCollection,
			ReturnResult,
			SelectDataPostProcessing,
			SelectDataReturnResult
		}
		[Flags]
		enum InTransactionMode {
			Sync = 1,
			Async = 2,
			SelectData = 4
		}
		readonly Session session;
		VirtualAsyncRequest mainRequest;
		InTransactionMode currentMode;
		InTransactionState currentState;
		ICollection objectsToSave;
		ICollection objectsToDelete;
		AsyncLoadObjectsCallback loadObjectsCallback;
		AsyncSelectDataCallback selectDataCallback;
		SelectDataState selectDataState;
		LoadObjectsState loadObjectsState;
		GetOriginalObjectsState getOriginalObjectsState;
		ObjectsQuery[] queriesForExecute;
		ICollection[] queriesForExecuteResult;
		bool? caseSensitive;
		public bool CaseSensitive {
			get { return caseSensitive.HasValue ? caseSensitive.Value : session.CaseSensitive; }
		}
		bool IsSelectDataMode {
			get { return (currentMode & InTransactionMode.SelectData) == InTransactionMode.SelectData; }
		}
		InTransactionLoader(Session session) {
			this.session = session;
		}
		InTransactionLoader(Session session, bool caseSensitive)
			: this(session) {
			this.caseSensitive = caseSensitive;
		}
		public static ICollection[] GetObjects(Session session, ObjectsQuery[] queries) {
			return new InTransactionLoader(session).GetObjects(queries);
		}
		public static ICollection[] GetObjects(Session session, ObjectsQuery[] queries, bool caseSensitive) {
			return new InTransactionLoader(session, caseSensitive).GetObjects(queries);
		}
		public static object GetObjectsAsync(Session session, ObjectsQuery[] queries, AsyncLoadObjectsCallback callback) {
			return new InTransactionLoader(session).GetObjectsAsync(queries, callback);
		}
		public static object GetObjectsAsync(Session session, ObjectsQuery[] queries, AsyncLoadObjectsCallback callback, bool caseSensitive) {
			return new InTransactionLoader(session, caseSensitive).GetObjectsAsync(queries, callback);
		}
		public static Task<ICollection[]> GetObjectsAsync(Session session, ObjectsQuery[] queries, CancellationToken cancellationToken = default(CancellationToken)) {
			return new InTransactionLoader(session).GetObjectsAsync(queries, cancellationToken);
		}
		public static Task<ICollection[]> GetObjectsAsync(Session session, ObjectsQuery[] queries, bool caseSensitive, CancellationToken cancellationToken = default(CancellationToken)) {
			return new InTransactionLoader(session, caseSensitive).GetObjectsAsync(queries, cancellationToken);
		}
		public static List<object[]> SelectData(Session session, XPClassInfo classInfo, CriteriaOperatorCollection properties, CriteriaOperator criteria, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria, bool selectDeleted, int skipSelectedRecords, int topSelectedRecords, SortingCollection sorting) {
			return new InTransactionLoader(session).SelectData(classInfo, properties, criteria, groupProperties, groupCriteria, selectDeleted, skipSelectedRecords, topSelectedRecords, sorting);
		}
		public static List<object[]> SelectData(Session session, XPClassInfo classInfo, CriteriaOperatorCollection properties, CriteriaOperator criteria, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria, bool selectDeleted, int skipSelectedRecords, int topSelectedRecords, SortingCollection sorting, bool caseSensitive) {
			return new InTransactionLoader(session, caseSensitive).SelectData(classInfo, properties, criteria, groupProperties, groupCriteria, selectDeleted, skipSelectedRecords, topSelectedRecords, sorting);
		}
		public static object SelectDataAsync(Session session, XPClassInfo classInfo, CriteriaOperatorCollection properties, CriteriaOperator criteria, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria, bool selectDeleted, int skipSelectedRecords, int topSelectedRecords, SortingCollection sorting, AsyncSelectDataCallback callback) {
			return new InTransactionLoader(session).SelectDataAsync(classInfo, properties, criteria, groupProperties, groupCriteria, selectDeleted, skipSelectedRecords, topSelectedRecords, sorting, callback);
		}
		public static object SelectDataAsync(Session session, XPClassInfo classInfo, CriteriaOperatorCollection properties, CriteriaOperator criteria, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria, bool selectDeleted, int skipSelectedRecords, int topSelectedRecords, SortingCollection sorting, AsyncSelectDataCallback callback, bool caseSensitive) {
			return new InTransactionLoader(session, caseSensitive).SelectDataAsync(classInfo, properties, criteria, groupProperties, groupCriteria, selectDeleted, skipSelectedRecords, topSelectedRecords, sorting, callback);
		}
		public static Task<List<object[]>> SelectDataAsync(Session session, XPClassInfo classInfo, CriteriaOperatorCollection properties, CriteriaOperator criteria, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria, bool selectDeleted, int skipSelectedRecords, int topSelectedRecords, SortingCollection sorting, CancellationToken cancellationToken = default(CancellationToken)) {
			return new InTransactionLoader(session).SelectDataAsync(classInfo, properties, criteria, groupProperties, groupCriteria, selectDeleted, skipSelectedRecords, topSelectedRecords, sorting, cancellationToken);
		}
		public static Task<List<object[]>> SelectDataAsync(Session session, XPClassInfo classInfo, CriteriaOperatorCollection properties, CriteriaOperator criteria, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria, bool selectDeleted, int skipSelectedRecords, int topSelectedRecords, SortingCollection sorting, bool caseSensitive, CancellationToken cancellationToken = default(CancellationToken)) {
			return new InTransactionLoader(session, caseSensitive).SelectDataAsync(classInfo, properties, criteria, groupProperties, groupCriteria, selectDeleted, skipSelectedRecords, topSelectedRecords, sorting, cancellationToken);
		}
		public ICollection[] GetObjects(ObjectsQuery[] queries) {
			currentMode = InTransactionMode.Sync;
			objectsToSave = session.GetObjectsToSave(true);
			objectsToDelete = session.GetObjectsToDelete(true);
			if(objectsToSave.Count == 0 && objectsToDelete.Count == 0) {
				return session.GetObjectsInternal(queries);
			}
			StateQuery(session.PrepareQueries(queries, false, this), queries);
			Process();
			return loadObjectsState.Result;
		}
		public object GetObjectsAsync(ObjectsQuery[] queries, AsyncLoadObjectsCallback callback) {
			this.mainRequest = new VirtualAsyncRequest(AsyncOperationsHelper.CaptureSynchronizationContextOrFail());
			this.loadObjectsCallback = callback;
			currentMode = InTransactionMode.Async;
			objectsToSave = session.GetObjectsToSave(true);
			objectsToDelete = session.GetObjectsToDelete(true);
			if(objectsToSave.Count == 0 && objectsToDelete.Count == 0) {
				return session.GetObjectsInternalAsync(queries, callback);				
			}
			StateQuery(session.PrepareQueries(queries, false, this), queries);
			ProcessAsync();
			return this.mainRequest;
		}
		public Task<ICollection[]> GetObjectsAsync(ObjectsQuery[] queries, CancellationToken cancellationToken = default(CancellationToken)) {
			var tcs = new TaskCompletionSource<ICollection[]>();
			object ar = GetObjectsAsync(queries, (result, ex) => {
				if(ex == null) {
					tcs.TrySetResult(result);
				} else {
					tcs.TrySetException(ex);
				}
			});
			cancellationToken.Register(() => {
				tcs.TrySetCanceled();
				((AsyncRequest)ar).Cancel();
			});
			return tcs.Task;
		}
		public List<object[]> SelectData(XPClassInfo classInfo, CriteriaOperatorCollection properties, CriteriaOperator criteria, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria, bool selectDeleted, int skipSelectedRecords, int topSelectedRecords, SortingCollection sorting) {
			currentMode = InTransactionMode.Sync | InTransactionMode.SelectData;
			objectsToSave = session.GetObjectsToSave(true);
			objectsToDelete = session.GetObjectsToDelete(true);
			if(objectsToSave.Count == 0 && objectsToDelete.Count == 0) {
				return session.SelectDataInternal(classInfo, properties, criteria, groupProperties, groupCriteria, selectDeleted, skipSelectedRecords, topSelectedRecords, sorting);
			}
			ObjectsQuery originalQuery = GetQueryForSelectData(classInfo, criteria, selectDeleted, skipSelectedRecords, topSelectedRecords, sorting);
			List<object[]> result = session.PrepareSelectData(classInfo, ref properties, ref criteria, ref groupProperties, ref groupCriteria, ref sorting, false, this);
			if(result == null) {
				StateSelectData(originalQuery, classInfo, properties, criteria, groupProperties, groupCriteria, selectDeleted, skipSelectedRecords, topSelectedRecords, sorting);
				Process();
			} else {
				return result;
			}
			return selectDataState.Result;
		}
		public object SelectDataAsync(XPClassInfo classInfo, CriteriaOperatorCollection properties, CriteriaOperator criteria, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria, bool selectDeleted, int skipSelectedRecords, int topSelectedRecords, SortingCollection sorting, AsyncSelectDataCallback callback) {
			this.mainRequest = new VirtualAsyncRequest(AsyncOperationsHelper.CaptureSynchronizationContextOrFail());
			this.selectDataCallback = callback;
			currentMode = InTransactionMode.Async | InTransactionMode.SelectData;
			objectsToSave = session.GetObjectsToSave(true);
			objectsToDelete = session.GetObjectsToDelete(true);
			if(objectsToSave.Count == 0 && objectsToDelete.Count == 0) {
				return session.SelectDataInternalAsync(classInfo, properties, criteria, groupProperties, groupCriteria, selectDeleted, skipSelectedRecords, topSelectedRecords, sorting, callback);
			}
			ObjectsQuery originalQuery = GetQueryForSelectData(classInfo, criteria, selectDeleted, skipSelectedRecords, topSelectedRecords, sorting);
			List<object[]> result = session.PrepareSelectData(classInfo, ref properties, ref criteria, ref groupProperties, ref groupCriteria, ref sorting, false, this);
			if(result == null) {
				StateSelectData(originalQuery, classInfo, properties, criteria, groupProperties, groupCriteria, selectDeleted, skipSelectedRecords, topSelectedRecords, sorting);
				ProcessAsync();
			} else {
				StateSelectDataReturnResult(result);
				ProcessAsync();
			}
			return this.mainRequest;
		}
		public Task<List<object[]>> SelectDataAsync(XPClassInfo classInfo, CriteriaOperatorCollection properties, CriteriaOperator criteria, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria, bool selectDeleted, int skipSelectedRecords, int topSelectedRecords, SortingCollection sorting, CancellationToken cancellationToken = default(CancellationToken)) {
			var tcs = new TaskCompletionSource<List<object[]>>();
			object ar = SelectDataAsync(classInfo, properties, criteria, groupProperties, groupCriteria, selectDeleted, skipSelectedRecords, topSelectedRecords, sorting, (result, ex) => {
				if(ex == null) {
					tcs.TrySetResult(result);
				} else {
					tcs.TrySetException(ex);
				}
			});
			cancellationToken.Register(() => {
				tcs.TrySetCanceled();
				((AsyncRequest)ar).Cancel();
			});
			return tcs.Task;
		}
		void Process() {
			switch(currentState) {
				case InTransactionState.AnalyzeAndExecQuery:
					ProcessAnalyzeAndExecQuery();
					break;
				case InTransactionState.AfterAnalyzeQueryNullCriteria:
					ProcessAfterAnalyzeQueryNullCriteria();
					break;
				case InTransactionState.AfterAnalyzeQueryDirectGetObjects:
					ProcessAfterAnalyzeQueryDirectGetObjects();
					break;
				case InTransactionState.AfterGetOriginalObjectsThroughCollection:
					ProcessAfterGetOriginalObjectsThroughtCollection();
					break;
				case InTransactionState.GetOriginalObjects:
					ProcessGetOriginalObjects();
					break;
				case InTransactionState.ReturnResult:
					ProcessReturnResult();
					break;
				case InTransactionState.SelectDataPostProcessing:
					ProcessSelectDataPostProcessing();
					break;
				case InTransactionState.SelectDataReturnResult:
					ProcessSelectDataReturnResult();
					break;
				default:
					throw new InvalidOperationException(currentState.ToString());
			}
		}
		void ProcessAsync() {
			ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object obj) {
				try {
					session.AsyncExecuteQueue.Invoke(mainRequest.SyncContext, new SendOrPostCallback(delegate(object o) {
						if(mainRequest.IsCanceled) return;
							try {
							Process();
						} catch(Exception exi) {
							try {
								ProcessException(exi);
							} catch(Exception) { }
						}
					}), null, true);
				} catch(Exception exi) {
					try {
						session.AsyncExecuteQueue.Invoke(mainRequest.SyncContext, new SendOrPostCallback(delegate(object o) {
							if(mainRequest.IsCanceled) return;
							try {
								ProcessException(exi);
							} catch(Exception) { }
						}), null, true);
					} catch(Exception) { }
				}
			}));
		}
		void ExecDirectSelectData() {
			if((currentMode & InTransactionMode.Sync) == InTransactionMode.Sync) {
				StateSelectDataReturnResult(session.SelectDataInternal(selectDataState.PreparedQuery.ClassInfo,
					selectDataState.Properties,
					selectDataState.PreparedQuery.Criteria,
					selectDataState.GroupProperties,
					selectDataState.GroupCriteria,
					selectDataState.PreparedQuery.CollectionCriteriaPatcher.SelectDeleted,
					selectDataState.PreparedQuery.SkipSelectedRecords,
					selectDataState.PreparedQuery.TopSelectedRecords,
					selectDataState.PreparedQuery.Sorting));
				Process();
				return;
			}
			if((currentMode & InTransactionMode.Async) == InTransactionMode.Async) {
				mainRequest.AddNestedRequest(session.SelectDataInternalAsync(selectDataState.PreparedQuery.ClassInfo,
					selectDataState.Properties,
					selectDataState.PreparedQuery.Criteria,
					selectDataState.GroupProperties,
					selectDataState.GroupCriteria,
					selectDataState.PreparedQuery.CollectionCriteriaPatcher.SelectDeleted,
					selectDataState.PreparedQuery.SkipSelectedRecords,
					selectDataState.PreparedQuery.TopSelectedRecords,
					selectDataState.PreparedQuery.Sorting,
					new AsyncSelectDataCallback(delegate(List<object[]> result, Exception ex) {
					if(ex != null) {
						ProcessException(ex);
						return;
					}
					StateSelectDataReturnResult(result);
					ProcessAsync();
				})) as AsyncRequest);
				return;
			}
		}
		void ExecQueries(ObjectsQuery[] query, InTransactionState stateAfterExecute) {
			queriesForExecute = query;
			currentState = stateAfterExecute;
			if((currentMode & InTransactionMode.Sync) == InTransactionMode.Sync) {
				queriesForExecuteResult = session.GetObjectsInternal(queriesForExecute);
				Process();
				return;
			}
			if((currentMode & InTransactionMode.Async) == InTransactionMode.Async) {
				mainRequest.AddNestedRequest(session.GetObjectsInternalAsync(queriesForExecute, new AsyncLoadObjectsCallback(delegate(ICollection[] result, Exception ex) {
					if(ex != null) {
						ProcessException(ex);
						return;
					}
					queriesForExecuteResult = result;
					ProcessAsync();
				})) as AsyncRequest);
				return;
			}
		}
		ObjectsQuery GetQueryForSelectData(XPClassInfo classInfo, CriteriaOperator criteria, bool selectDeleted, int skipSelectedRecords, int topSelectedRecords, SortingCollection sorting) {
			return new ObjectsQuery(classInfo, criteria, sorting, skipSelectedRecords, topSelectedRecords, new DevExpress.Xpo.Generators.CollectionCriteriaPatcher(selectDeleted, session.TypesManager), false);
		}
		void StateSelectData(ObjectsQuery originalQuery, XPClassInfo classInfo, CriteriaOperatorCollection properties, CriteriaOperator criteria, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria, bool selectDeleted, int skipSelectedRecords, int topSelectedRecords, SortingCollection sorting) {
			selectDataState = new SelectDataState();
			selectDataState.OriginalQuery = originalQuery;
			selectDataState.PreparedQuery = GetQueryForSelectData(classInfo, criteria, selectDeleted, skipSelectedRecords, topSelectedRecords, sorting);
			selectDataState.Properties = properties;
			selectDataState.GroupProperties = groupProperties;
			selectDataState.GroupCriteria = groupCriteria;
			selectDataState.Grouped = (selectDataState.GroupProperties != null) && (selectDataState.GroupProperties.Count > 0);
			if(!selectDataState.Grouped && selectDataState.Properties != null && selectDataState.Properties.Count > 0) {
				for(int i = 0; i < selectDataState.Properties.Count; i++) {
					if(IsTopLevelAggregateCheckerClient.IsTopLevelAggregate(selectDataState.Properties[i])) {
						selectDataState.Grouped = true;
						break;
					}
				}
			}
			ObjectsQuery[] queries = new ObjectsQuery[] { new ObjectsQuery(classInfo, criteria, selectDataState.Grouped ? null : sorting, 
				selectDataState.Grouped ? 0 : skipSelectedRecords, selectDataState.Grouped ? 0 : topSelectedRecords,
					new DevExpress.Xpo.Generators.CollectionCriteriaPatcher(selectDeleted, session.TypesManager), false) };
			StateQuery(queries, new ObjectsQuery[] { originalQuery });
		}
		void StateQuery(ObjectsQuery[] queries, ObjectsQuery[] originalQueries) {
			loadObjectsState = new LoadObjectsState();
			loadObjectsState.Queries = queries;
			loadObjectsState.OriginalQueries = originalQueries;
			loadObjectsState.Result = new ICollection[queries.Length];
			loadObjectsState.QueryIndex = -1;
			StateNextQuery();
		}
		void StateNextQuery() {
			loadObjectsState.QueryIndex++;
			if(loadObjectsState.QueryIndex >= loadObjectsState.Queries.Length) {
				if(IsSelectDataMode) {
					currentState = InTransactionState.SelectDataPostProcessing;
					return;
				}
				currentState = InTransactionState.ReturnResult;
			} else {
				currentState = InTransactionState.AnalyzeAndExecQuery;
			}
		}
		void StateSelectDataReturnResult(List<object[]> result) {
			if(selectDataState == null) selectDataState = new SelectDataState();
			selectDataState.Result = result;
			currentState = InTransactionState.SelectDataReturnResult;
		}
		void ProcessSelectDataPostProcessing() {
			try {
				List<object[]> result;
				if(selectDataState.Grouped || (loadObjectsState.Queries.Length > 0 && loadObjectsState.Result.Length > 0 && loadObjectsState.Result[0] != null && loadObjectsState.Result[0].Count > 0)) {
					if(selectDataState.Grouped) {
						result = ProcessPostProcessingGrouped();
					} else {
						result = ProcessPostProcessingNormal();
					}
				} else {
					result = new List<object[]>(0);
				}
				StateSelectDataReturnResult(result);
				Process();
			} catch(Exception ex) {
				ProcessException(ex);
			}
		}
		List<object[]> ProcessPostProcessingGrouped() {
			Dictionary<SelectDataGroup, List<object>> groupingResult = new Dictionary<SelectDataGroup, List<object>>();
			var descriptor = selectDataState.PreparedQuery.ClassInfo.GetCriteriaCompilerDescriptor(this.session);
			var dataEvaluators = PrepareDataEvaluators(selectDataState.Properties, new GroupByObjectsDescriptor(descriptor));
			var groupEvaluators = PrepareDataEvaluators(selectDataState.GroupProperties, descriptor);
			var havingEvaluator = PrepareDataPredicate(selectDataState.GroupCriteria, descriptor);
			if(groupEvaluators.Length == 0) {
				groupingResult.Add(new SelectDataGroup(Array.Empty<object>()), new List<object>());
			}
			ObjectSet objSet = new ObjectSet();
			foreach(object obj in loadObjectsState.Result[0]) {
				objSet.Add(obj);
				SelectDataGroup group = new SelectDataGroup(GetResultRow(obj, groupEvaluators));
				List<object> groupList;
				if(!groupingResult.TryGetValue(group, out groupList)) {
					groupList = new List<object>();
					groupingResult.Add(group, groupList);
				}
				groupList.Add(obj);
			}
			List<List<object>> havingResult = new List<List<object>>();
			foreach(List<object> group in groupingResult.Values) {
				if(group.Count > 0) {
					if(havingEvaluator(group[0]))
						havingResult.Add(group);
				} else {
					if(havingEvaluator(null))
						havingResult.Add(group);
				}
			}
			if(selectDataState.PreparedQuery.Sorting != null && selectDataState.PreparedQuery.Sorting.Count > 0) {
				IComparer<List<object>> sortComparer = PrepareSortingComparer(selectDataState.PreparedQuery.Sorting, selectDataState.PreparedQuery.ClassInfo);
				havingResult.Sort(sortComparer);
			}
			int skipRecords = selectDataState.PreparedQuery.SkipSelectedRecords;
			int topRecords = selectDataState.PreparedQuery.TopSelectedRecords;
			if(skipRecords != 0) {
				if(skipRecords >= havingResult.Count)
					havingResult.Clear();
				else
					havingResult.RemoveRange(0, skipRecords);
			}
			if(topRecords != 0 && topRecords < havingResult.Count) {
				havingResult.RemoveRange(topRecords, havingResult.Count - topRecords);
			}
			int count = havingResult.Count;
			List<object[]> result = new List<object[]>(count);
			for(int i = 0; i < count; i++) {
				result.Add(GetResultRow(havingResult[i], dataEvaluators));
			}
			return result;
		}
		static object[] GetResultRow(object obj, Func<object, object>[] evaluators) {
			int count = evaluators.Length;
			object[] result = new object[count];
			for(int i = 0; i < count; i++) {
				result[i] = evaluators[i](obj);
			}
			return result;
		}
		List<object[]> ProcessPostProcessingNormal() {
			List<object[]> result = new List<object[]>();
			if(loadObjectsState.Result != null && loadObjectsState.Result[0] != null && loadObjectsState.Result[0].Count > 0) {
				if(selectDataState.Properties != null && selectDataState.Properties.Count > 0) {
					var propEvaluators = PrepareDataEvaluators(selectDataState.Properties, selectDataState.PreparedQuery.ClassInfo.GetCriteriaCompilerDescriptor(this.session));
					int propertiesCount = propEvaluators.Length;
					for(int propIndex = 0; propIndex < propertiesCount; propIndex++) {
						var evaluator = propEvaluators[propIndex];
						object[] propResult = loadObjectsState.Result[0].Cast<object>().Select(o => evaluator(o)).ToArray();
						if(result.Count == 0) {
							foreach(object value in propResult) {
								object[] newRow = new object[propertiesCount];
								newRow[propIndex] = value;
								result.Add(newRow);
							}
						} else {
							for(int i = 0; i < propResult.Length; i++) {
								result[i][propIndex] = propResult[i];
							}
						}
					}
				} else {
					object[] emptyRow = Array.Empty<object>();
					for(int i = 0; i < loadObjectsState.Result[0].Count; i++) {
						result.Add(emptyRow);
					}
				}
			}
			return result;
		}
		void ProcessSelectDataReturnResult() {
			if(!IsSelectDataMode) {
				ProcessException(new InvalidOperationException(Res.GetString(Res.InTransactionLoader_NotInSelectDataMode)));
				return;
			}
			if((currentMode & InTransactionMode.Async) == InTransactionMode.Async) {
				if(mainRequest.IsCanceled) return;
				try {
					selectDataCallback(selectDataState.Result, null);
				} catch(Exception) { }
				mainRequest.Stop();
				return;
			}
		}
		void ProcessReturnResult() {
			if((currentMode & InTransactionMode.Async) == InTransactionMode.Async) {
				if(IsSelectDataMode) {
					throw new InvalidOperationException(Res.GetString(Res.InTransactionLoader_ProcessReturnResultError));
				}
				if(mainRequest.IsCanceled) return;
				try {
					loadObjectsCallback(loadObjectsState.Result, null);
				} catch(Exception) { }
				mainRequest.Stop();
				return;
			}
		}
		void ProcessException(Exception ex) {
			if((currentMode & InTransactionMode.Sync) == InTransactionMode.Sync) {
				ExceptionDispatchInfo.Capture(ex).Throw();
			}
			if((currentMode & InTransactionMode.Async) == InTransactionMode.Async) {
				if(mainRequest.IsCanceled) return;
				try {
					if(IsSelectDataMode) {
						selectDataCallback(null, ex);
					} else {
						loadObjectsCallback(null, ex);
					}
				} catch(Exception) { }
				mainRequest.Stop();
				return;
			}
		}
		void ProcessAfterAnalyzeQueryDirectGetObjects() {
			try {
				loadObjectsState.Result[loadObjectsState.QueryIndex] = queriesForExecuteResult[0];
				StateNextQuery();
				Process();
			} catch(Exception ex) {
				ProcessException(ex);
			}
		}
		void ProcessAfterAnalyzeQueryNullCriteria() {
			try {
				ObjectsQuery query = queriesForExecute[0];
				ICollection queryResult = queriesForExecuteResult[0];
				SaveCurrentResult(queryResult, query.Criteria);
				StateNextQuery();
				Process();
			} catch(Exception ex) {
				ProcessException(ex);
			}
		}
		ObjectsQuery SanitizeQuery(ObjectsQuery query) {
			return new ObjectsQuery(query.ClassInfo, query.Criteria, query.Sorting, 0, query.CollectionCriteriaPatcher, query.Force);
		}
		void ProcessAnalyzeAndExecQuery() {
			try {
				ObjectsQuery originalObjectsQuery = loadObjectsState.OriginalQueries[loadObjectsState.QueryIndex];
				ObjectsQuery objectsQuery = loadObjectsState.Queries[loadObjectsState.QueryIndex];
				loadObjectsState.QuerySelectDeleted = objectsQuery.CollectionCriteriaPatcher.SelectDeleted;
				bool hasModifiedSelectProperties = false;
				AnalyzeResult analyzeResult;
				string[] modifiedNodes = GetModifiedNodes(session, objectsToSave, objectsToDelete, objectsQuery, out analyzeResult);
				analyzeResult.RaiseIfTopLevelAggregate();
				if(modifiedNodes.Length == 0) {
					if(IsSelectDataMode) {
						ObjectsQuery selectQuery = SanitizeQuery(objectsQuery);
						foreach(CriteriaOperator criteria in selectDataState.Properties) {
							selectQuery.Criteria = criteria;
							AnalyzeResult analyzeResultSelect;
							string[] modifiedNodesSelect = GetModifiedNodes(session, objectsToSave, objectsToDelete, selectQuery, out analyzeResultSelect);
							if(modifiedNodesSelect.Length > 0) {
								hasModifiedSelectProperties = true;
								break;
							}
						}
						if(!hasModifiedSelectProperties)
							ExecDirectSelectData();
					} else {
						ExecQueries(new ObjectsQuery[] { originalObjectsQuery }, InTransactionState.AfterAnalyzeQueryDirectGetObjects);
					}
					if(!hasModifiedSelectProperties)
						return;
				}
				if(!hasModifiedSelectProperties && ReferenceEquals(objectsQuery.Criteria, null)) {
					ExecQueries(new ObjectsQuery[] { SanitizeQuery(originalObjectsQuery) }, InTransactionState.AfterAnalyzeQueryNullCriteria);
					return;
				}
				Dictionary<string, CriteriaOperator> reverseCriteriaDict = new Dictionary<string, CriteriaOperator>();
				SmellingJoinOperandFinder joFinder = new SmellingJoinOperandFinder(originalObjectsQuery.ClassInfo, originalObjectsQuery.Criteria);
				bool n0Exists = false;
				for(int i = 0; i <= modifiedNodes.Length; i++) {
					string currentNode;
					if(i < modifiedNodes.Length) {
						currentNode = modifiedNodes[i];
						if(string.Equals(currentNode, "N0")) n0Exists = true;
					} else {
						if(n0Exists) break;
						currentNode = "N0";
					}
					CriteriaOperator result;
					if(!ReverseCriteriaAnalyzer.ReverseAndRemoveModifiedNoException(currentNode, analyzeResult, modifiedNodes, out result)) continue;
					result = AnalyzeCriteriaCleaner.Clean(result);
					if(ReferenceEquals(result, null) || joFinder.Process(result)) continue;
					reverseCriteriaDict[currentNode] = result;
				}
				string node = null;
				switch(reverseCriteriaDict.Count) {
					case 0:
						break;
					case 1:
						foreach(string reverseCriteriaNode in reverseCriteriaDict.Keys) {
							if(ComplexityAnalyzer.Analyze(analyzeResult.GetNodePath(reverseCriteriaNode, "N0")) >= 0)
								node = reverseCriteriaNode;
						}
						break;
					default: {
							int extrComplexity = 0;
							string maxComplexityNode = null;
							foreach(KeyValuePair<string, CriteriaOperator> pair in reverseCriteriaDict) {
								int criteriaComplexity = ComplexityAnalyzer.Analyze(analyzeResult.NodeClassInfoDict[pair.Key], pair.Value);
								int pathComplexity = ComplexityAnalyzer.Analyze(analyzeResult.GetNodePath(pair.Key, "N0"));
								if(pathComplexity < 0) continue;
								int complexity = criteriaComplexity / pathComplexity;
								if(extrComplexity < complexity) {
									extrComplexity = complexity;
									maxComplexityNode = pair.Key;
								}
							}
							if(!string.IsNullOrEmpty(maxComplexityNode)) {
								node = maxComplexityNode;
							}
						}
						break;
				}
				if(node == null) {
					node = "N0";
					reverseCriteriaDict["N0"] = null;
				}
				getOriginalObjectsState = null;
				loadObjectsState.QueryNode = node;
				loadObjectsState.QueryAnalyzeResult = analyzeResult;
				ExecQueries(new ObjectsQuery[] { new ObjectsQuery(analyzeResult.NodeClassInfoDict[node], reverseCriteriaDict[node], null, 0, 0, objectsQuery.CollectionCriteriaPatcher, objectsQuery.Force) { SkipDuplicateCheck = IsSelectDataMode } },
					InTransactionState.GetOriginalObjects);
			} catch(Exception ex) {
				ProcessException(ex);
			}
		}
		public static string[] GetModifiedNodes(Session session, ICollection objectsToSave, ICollection objectsToDelete, ObjectsQuery objectsQuery, out AnalyzeResult analyzeResult) {
			analyzeResult = AnalyzeCriteriaCreator.Process(objectsQuery.ClassInfo, JoinOperandExpander.Expand(objectsQuery.ClassInfo, objectsQuery.Criteria));
			return analyzeResult.GetModifiedNodes(session, objectsToSave, objectsToDelete);
		}
		void SaveCurrentResult(ICollection result, CriteriaOperator actualCondition) {
			ObjectsQuery currentQuery = loadObjectsState.Queries[loadObjectsState.QueryIndex];
			loadObjectsState.Result[loadObjectsState.QueryIndex] = FilterObjects(currentQuery.ClassInfo, result, actualCondition, currentQuery.Sorting, currentQuery.SkipSelectedRecords, currentQuery.TopSelectedRecords, currentQuery.CollectionCriteriaPatcher.SelectDeleted);
		}
		void ProcessGetOriginalObjects() {
			try {
				ICollection currentCollection;
				if(getOriginalObjectsState == null) {
					AnalyzeNodePathItem[] path = loadObjectsState.QueryAnalyzeResult.GetNodePath(loadObjectsState.QueryNode, "N0");
					if(path == null) throw new InvalidOperationException(Res.GetString(Res.CriteriaAnalyzer_PathNotFound, loadObjectsState.QueryNode, "N0"));
					getOriginalObjectsState = new GetOriginalObjectsState(path);
					ObjectsQuery query = queriesForExecute[0];
					currentCollection = FilterObjects(query.ClassInfo, queriesForExecuteResult[0], query.Criteria, query.CollectionCriteriaPatcher.SelectDeleted);
				} else {
					currentCollection = getOriginalObjectsState.Collection;
				}
				if(getOriginalObjectsState.PathIndex >= getOriginalObjectsState.Path.Length) {
					SaveCurrentResult(currentCollection, AnalyzeCriteriaCleaner.Clean(loadObjectsState.QueryAnalyzeResult.ResultOperator));
					StateNextQuery();
					Process();
					return;
				}
				AnalyzeNodePathItem pathItem = getOriginalObjectsState.Path[getOriginalObjectsState.PathIndex];
				if(pathItem.TransitionInfo.IsMemberInfo) {
					if(pathItem.TransitionInfo.MemberInfo.IsAssociationList && pathItem.Type == AnalyzeNodePathItemType.Reverse) {
						ObjectSet nextCollection = new ObjectSet();
						foreach(object collectionObject in currentCollection) {
							nextCollection.Add(pathItem.TransitionInfo.MemberInfo.GetAssociatedMember().GetValue(collectionObject));
						}
						currentCollection = nextCollection;
					} else if(!pathItem.TransitionInfo.MemberInfo.IsAssociationList && pathItem.Type == AnalyzeNodePathItemType.Direct) {
						ObjectSet nextCollection = new ObjectSet();
						foreach(object collectionObject in currentCollection) {
							nextCollection.Add(pathItem.TransitionInfo.MemberInfo.GetValue(collectionObject));
						}
						currentCollection = nextCollection;
					} else if((pathItem.TransitionInfo.MemberInfo.IsAssociationList && pathItem.Type == AnalyzeNodePathItemType.Direct) 
							|| (!pathItem.TransitionInfo.MemberInfo.IsAssociationList && pathItem.Type == AnalyzeNodePathItemType.Reverse)) {
						XPClassInfo collectionClassInfo = null;
						XPMemberInfo ownerMemberInfo = null;
						XPMemberInfo ownerKeyMemberInfo = null;
						if(!pathItem.TransitionInfo.MemberInfo.IsAssociationList && pathItem.Type == AnalyzeNodePathItemType.Reverse){
							ownerMemberInfo = pathItem.TransitionInfo.MemberInfo;
							collectionClassInfo = ownerMemberInfo.Owner;
							if(!collectionClassInfo.IsPersistent) {
								collectionClassInfo = loadObjectsState.QueryAnalyzeResult.NodeClassInfoDict[pathItem.Node];
							} else {
								collectionClassInfo = AnalyzeCriteriaCreator.GetUpClass(collectionClassInfo, loadObjectsState.QueryAnalyzeResult.NodeClassInfoDict[pathItem.Node]);
							}
							if(ownerMemberInfo.ReferenceType == null) {
								if(ownerMemberInfo.Name == "This") {
									StateNextGetOriginalPathItem(currentCollection);
									Process();
									return;
								} else throw new InvalidOperationException();
							} else {
								ownerKeyMemberInfo = ownerMemberInfo.ReferenceType.KeyProperty;
							}
						}else
							if(pathItem.TransitionInfo.MemberInfo.IsAssociationList && pathItem.Type == AnalyzeNodePathItemType.Direct) {
								collectionClassInfo = pathItem.TransitionInfo.MemberInfo.CollectionElementType;
								ownerMemberInfo = pathItem.TransitionInfo.MemberInfo.GetAssociatedMember();
								ownerKeyMemberInfo = pathItem.TransitionInfo.MemberInfo.Owner.KeyProperty;
							} else {
								throw new InvalidOperationException();
							}
						OperandProperty ownerProperty = new OperandProperty(string.Join(".", new string[] { ownerMemberInfo.Name, ownerKeyMemberInfo.Name }));
						ObjectSet nextCollection = new ObjectSet();
						int collectionPos = 0;
						int collectionCount = currentCollection.Count;
						int nextRequestCount = -1;
						List<ObjectsQuery> subQueries = new List<ObjectsQuery>();
						CriteriaOperatorCollection request = new CriteriaOperatorCollection();
						foreach(object collectionObject in currentCollection) {
							if(session.IsNewObject(collectionObject)) {
								collectionPos++;
								continue;
							}
							if(nextRequestCount < 0) {
								nextRequestCount = XpoDefault.GetTerminalInSize(collectionCount - collectionPos);
							}
							request.Add(new OperandValue(ownerKeyMemberInfo.GetValue(collectionObject)));
							collectionPos++;
							if(request.Count >= nextRequestCount) {
								nextRequestCount = -1;
								CriteriaOperator requestCriteria = new InOperator(ownerProperty, request);
								subQueries.Add(new ObjectsQuery(collectionClassInfo, requestCriteria, null, 0, 0, new DevExpress.Xpo.Generators.CollectionCriteriaPatcher(loadObjectsState.QuerySelectDeleted, session.TypesManager), false));
								request.Clear();
							}
						}
						if(request.Count > 0) {
							CriteriaOperator requestCriteria = new InOperator(ownerProperty, request);
							subQueries.Add(new ObjectsQuery(collectionClassInfo, requestCriteria, null, 0, 0, new DevExpress.Xpo.Generators.CollectionCriteriaPatcher(loadObjectsState.QuerySelectDeleted, session.TypesManager), false));
						}
						ExecQueries(subQueries.ToArray(), InTransactionState.AfterGetOriginalObjectsThroughCollection);
						return;
					} else {
						throw new InvalidOperationException();
					}
				} else {
					throw new InvalidOperationException();
				}
				StateNextGetOriginalPathItem(currentCollection);
				Process();
			} catch(Exception ex) {
				ProcessException(ex);
			}
		}
		void StateNextGetOriginalPathItem(ICollection nextCollection) {
			getOriginalObjectsState.Collection = nextCollection;
			getOriginalObjectsState.PathIndex++;
			currentState = InTransactionState.GetOriginalObjects;
		}
		void ProcessAfterGetOriginalObjectsThroughtCollection() {
			try {
				ObjectSet nextCollection = new ObjectSet();
				ICollection[] nextResult = queriesForExecuteResult;
				for(int c = 0; c < nextResult.Length; c++) {
					ICollection addCollection = FilterObjects(queriesForExecute[c].ClassInfo, nextResult[c], queriesForExecute[c].Criteria, queriesForExecute[c].CollectionCriteriaPatcher.SelectDeleted);
					foreach(object addObject in addCollection) {
						nextCollection.Add(addObject);
					}
				}
				StateNextGetOriginalPathItem(nextCollection);
				Process();
			} catch(Exception ex) {
				ProcessException(ex);
			}
		}
		ICollection FilterObjects(XPClassInfo classInfo, ICollection objects, CriteriaOperator filterCriteria, bool selectDeleted) {
			return FilterObjects(classInfo, objects, filterCriteria, null, 0, 0, selectDeleted);
		}
		ICollection FilterObjects(XPClassInfo classInfo, ICollection objects, CriteriaOperator filterCriteria, SortingCollection sorting, int skipRecords, int topRecords, bool selectDeleted) {
			if(classInfo.IsGCRecordObject && !selectDeleted) {
				if(ReferenceEquals(filterCriteria, null))
					filterCriteria = new OperandProperty(GCRecordField.StaticName).IsNull();
				else
					filterCriteria = GroupOperator.And(filterCriteria, new OperandProperty(GCRecordField.StaticName).IsNull());
			}
			ObjectSet preparedResult = new ObjectSet();
			foreach(object theObject in objects) {
				if(!selectDeleted && session.IsObjectToDelete(theObject, true))
					continue;
				preparedResult.Add(theObject);
			}
			foreach(object theObject in objectsToDelete) {
				TryCollectObjectsFromIntermediateClass(theObject, preparedResult, session, classInfo, selectDeleted);
			}
			foreach(object theObject in objectsToSave) {
				TryCollectObjectsFromIntermediateClass(theObject, preparedResult, session, classInfo, selectDeleted);
				if(!selectDeleted && session.IsObjectToDelete(theObject, true))
					continue;
				if(session.GetClassInfo(theObject).IsAssignableTo(classInfo)) {
					preparedResult.Add(theObject);
				}
			}
			ICollection result = preparedResult;
			if(!ReferenceEquals(filterCriteria, null)){
				var copyValidator = PrepareDataPredicate(filterCriteria, classInfo.GetCriteriaCompilerDescriptor(this.session));
				result = result.Cast<object>().Where(copyValidator).ToArray();
			}
			if(sorting != null && sorting.Count > 0 && result.Count > 1) {
				result = ReSort(result, classInfo, sorting);
			}
			bool doSkip = skipRecords > 0;
			bool doTop = topRecords > 0;
			if(doSkip || doTop) {
				if(doSkip && skipRecords >= result.Count) {
					return Array.Empty<object>();
				}
				if(!doSkip && doTop && topRecords >= result.Count) {
					return result;
				}
				List<object> skipTopResult = new List<object>();
				int count = 0;
				foreach(object obj in result) {
					if(doTop && skipTopResult.Count >= topRecords) break;
					if(!doSkip || (doSkip && count >= skipRecords)) {
						skipTopResult.Add(obj);
					}
					count++;
				}
				return skipTopResult.ToArray();
			}
			return result;
		}
		static void TryCollectObjectsFromIntermediateClass(object theObject, ObjectSet preparedResult, Session session, XPClassInfo currentClassInfo, bool selectDeleted) {
			var intermediateClassInfo = session.GetClassInfo(theObject) as IntermediateClassInfo;
			if(intermediateClassInfo != null) {
				if(intermediateClassInfo.intermediateObjectFieldInfoLeft.ReferenceType != null && intermediateClassInfo.intermediateObjectFieldInfoLeft.ReferenceType.IsAssignableTo(currentClassInfo)) {
					var leftValue = intermediateClassInfo.intermediateObjectFieldInfoLeft.GetValue(theObject);
					if(leftValue != null && (selectDeleted || !session.IsObjectToDelete(leftValue, true))) {
						preparedResult.Add(leftValue);
					}
				}
				if(intermediateClassInfo.intermediateObjectFieldInfoRight.ReferenceType != null && intermediateClassInfo.intermediateObjectFieldInfoRight.ReferenceType.IsAssignableTo(currentClassInfo)) {
					var rightValue = intermediateClassInfo.intermediateObjectFieldInfoRight.GetValue(theObject);
					if(rightValue != null && (selectDeleted || !session.IsObjectToDelete(rightValue, true))) {
						preparedResult.Add(rightValue);
					}
				}
			}
		}
		ICollection ReSort(ICollection rows, XPClassInfo classInfo, SortingCollection sorting) {
			if(rows.Count <= 1)
				return rows;
			IComparer comparer = XPCollectionCompareHelper.CreateComparer(sorting, classInfo.GetCriteriaCompilerDescriptor(session), new CriteriaCompilerAuxSettings(CaseSensitive, session.Dictionary.CustomFunctionOperators));
			object[] sorted = ListHelper.FromCollection(rows).ToArray();
			Array.Sort(sorted, comparer);
			return sorted;
		}
		public class GroupByObjectsDescriptor: CriteriaCompilerDescriptor {
			readonly CriteriaCompilerDescriptor ObjectDescriptor;
			CriteriaCompilerDescriptor _TopLevelDescriptor;
			CriteriaCompilerDescriptor TopLevelDescriptor {
				get {
					if(_TopLevelDescriptor == null)
						_TopLevelDescriptor = new DefaultTopLevelCriteriaCompilerContextDescriptor(ObjectDescriptor);
					return _TopLevelDescriptor;
				}
			}
			public static T GetSingleObject<T>(IEnumerable enumerable) where T: class {
				if(enumerable == null)
					return null;
				ICollection collection = enumerable as ICollection;
				if(collection != null) {
					if(collection.Count == 0)
						return null;
					IList list = enumerable as IList;
					if(list != null)
						return (T)list[0];
				}
				var enumerator = enumerable.GetEnumerator();
				if(enumerator.MoveNext())
					return (T)enumerator.Current;
				else
					return null;
			}
			public GroupByObjectsDescriptor(CriteriaCompilerDescriptor _ObjectDescriptor) {
				this.ObjectDescriptor = _ObjectDescriptor;
			}
			public override Expression MakePropertyAccess(Expression baseExpression, string propertyPath) {
				ParameterExpression rowParameter = Expression.Parameter(ObjectDescriptor.ObjectType, "row");
				Expression plainPropertyAccess = ObjectDescriptor.MakePropertyAccess(rowParameter, propertyPath);
				if(!NullableHelpers.CanAcceptNull(plainPropertyAccess.Type))
					plainPropertyAccess = Expression.Convert(plainPropertyAccess, NullableHelpers.GetUnBoxedType(plainPropertyAccess.Type));
				Expression nullProtectedPropertyAccess = Expression.Condition(Expression.Call(typeof(object), "ReferenceEquals", null, rowParameter, Expression.Constant(null)), Expression.Constant(null, plainPropertyAccess.Type), plainPropertyAccess);
				var coreLambda = Expression.Lambda(nullProtectedPropertyAccess, rowParameter);
				var toSingle = Expression.Call(typeof(GroupByObjectsDescriptor), "GetSingleObject", new Type[] { ObjectDescriptor.ObjectType }, baseExpression);
				return Expression.Invoke(coreLambda, toSingle);
			}
			public override Type ResolvePropertyType(Expression baseExpression, string propertyPath) {
				return ObjectDescriptor.ResolvePropertyType(baseExpression, propertyPath);
			}
			public override Type ObjectType {
				get { return typeof(IEnumerable); }
			}
			public override CriteriaCompilerRefResult DiveIntoCollectionProperty(Expression baseExpression, string collectionPropertyPath) {
				if(string.IsNullOrEmpty(collectionPropertyPath))
					return TopLevelDescriptor.DiveIntoCollectionProperty(baseExpression, collectionPropertyPath);
				var toSingle = Expression.Call(typeof(GroupByObjectsDescriptor), "GetSingleObject", new Type[] { ObjectDescriptor.ObjectType }, baseExpression);
				return ObjectDescriptor.DiveIntoCollectionProperty(toSingle, collectionPropertyPath);
			}
			public override LambdaExpression MakeFreeJoinLambda(string joinTypeName, CriteriaOperator condition, OperandParameter[] conditionParameters, Aggregate aggregateType, CriteriaOperator aggregateExpression, OperandParameter[] aggregateExpresssionParameters, Type[] invokeTypes) {
				return ObjectDescriptor.MakeFreeJoinLambda(joinTypeName, condition, conditionParameters, aggregateType, aggregateExpression, aggregateExpresssionParameters, invokeTypes);
			}
			public override LambdaExpression MakeFreeJoinLambda(string joinTypeName, CriteriaOperator condition, OperandParameter[] conditionParameters, string customAggregateName, IEnumerable<CriteriaOperator> aggregateExpressions, OperandParameter[] aggregateExpresssionsParameters, Type[] invokeTypes) {
				return ObjectDescriptor.MakeFreeJoinLambda(joinTypeName, condition, conditionParameters, customAggregateName, aggregateExpressions, aggregateExpresssionsParameters, invokeTypes);
			}
		}
		Func<object, object> PrepareDataEvaluator(CriteriaOperator operand, CriteriaCompilerDescriptor descriptor) {
			if(ReferenceEquals(operand, null))
				return x => null;
			return CriteriaCompiler.ToUntypedDelegate(operand, descriptor, new CriteriaCompilerAuxSettings(CaseSensitive, session.Dictionary.CustomFunctionOperators));
		}
		Func<object, bool> PrepareDataPredicate(CriteriaOperator operand, CriteriaCompilerDescriptor descriptor) {
			if(ReferenceEquals(operand, null))
				return x => true;
			return CriteriaCompiler.ToUntypedPredicate(operand, descriptor, new CriteriaCompilerAuxSettings(CaseSensitive, session.Dictionary.CustomFunctionOperators));
		}
		Func<object, object>[] PrepareDataEvaluators(CriteriaOperatorCollection operands, CriteriaCompilerDescriptor descriptor) {
			if(operands == null)
				return Array.Empty<Func<object, object>>();
			return operands.Select(op => PrepareDataEvaluator(op, descriptor)).ToArray();
		}
		SelectDataSortingListComparer PrepareSortingComparer(SortingCollection sortProperties, XPClassInfo classInfo) {
			if(sortProperties == null || sortProperties.Count == 0)
				return null;
			int count = sortProperties.Count;
			var groupByDescriptor = new GroupByObjectsDescriptor(classInfo.GetCriteriaCompilerDescriptor(this.session));
			var sortingEvaluators = new Func<object, object>[count];
			for(int i = 0; i < count; i++) {
				SortProperty sortProperty = sortProperties[i];
				sortingEvaluators[i] = PrepareDataEvaluator(sortProperty.Property, groupByDescriptor);
			}
			return new SelectDataSortingListComparer(sortingEvaluators, sortProperties.Select(sp => sp.Direction).ToArray());
		}
		class SelectDataGroup {
			public readonly object[] GroupValues;
			public SelectDataGroup(object[] groupValues) {
				this.GroupValues = groupValues;
			}
			public override bool Equals(object obj) {
				SelectDataGroup another = obj as SelectDataGroup;
				if(another == null)
					return false;
				if(this.GroupValues.Length != another.GroupValues.Length)
					return false;
				int count = GroupValues.Length;
				for(int i = 0; i < count; ++i) {
					if(!object.Equals(this.GroupValues[i], another.GroupValues[i]))
						return false;
				}
				return true;
			}
			public override int GetHashCode() {
				return HashCodeHelper.CalculateGenericList(GroupValues);
			}
		}
		class SelectDataComparerWrapper : IComparer<object> {
			IComparer comparer;
			public SelectDataComparerWrapper(IComparer comparer) {
				this.comparer = comparer;
			}
			public int Compare(object x, object y) {
				return comparer.Compare(x, y);
			}
		}
		class SelectDataSortingListComparer : IComparer<List<object>> {
			readonly Func<object, object>[] Evaluators;
			readonly SortingDirection[] SortingDirections;
			public SelectDataSortingListComparer(Func<object, object>[] sortingEvaluators, SortingDirection[] sortingDirections) {
				this.Evaluators = sortingEvaluators;
				this.SortingDirections = sortingDirections;
			}
			int IComparer<List<object>>.Compare(List<object> xRows, List<object> yRows) {
				if(xRows == null)
					throw new InvalidOperationException("Internal error: xRows == null");
				if(yRows == null)
					throw new InvalidOperationException("Internal error: yRows == null");
				int count = Evaluators.Length;
				for(int i = 0; i < count; i++) {
					var evaluator = Evaluators[i];
					int res = Comparer<object>.Default.Compare(evaluator(xRows), evaluator(yRows));
					if(res != 0)
						return SortingDirections[i] == SortingDirection.Ascending ? res : -res;
				}
				return 0;
			}
		}
		class SelectDataState {
			public List<object[]> Result;
			public bool Grouped;
			public ObjectsQuery OriginalQuery;
			public ObjectsQuery PreparedQuery;
			public CriteriaOperatorCollection Properties;
			public CriteriaOperatorCollection GroupProperties;
			public CriteriaOperator GroupCriteria;
		}
		class LoadObjectsState {
			public ICollection[] Result;
			public ObjectsQuery[] OriginalQueries;
			public ObjectsQuery[] Queries;
			public int QueryIndex = -1;
			public AnalyzeResult QueryAnalyzeResult;
			public bool QuerySelectDeleted;
			public string QueryNode;
		}
		class GetOriginalObjectsState {
			public int PathIndex;
			public AnalyzeNodePathItem[] Path;
			public ICollection Collection;
			public GetOriginalObjectsState(AnalyzeNodePathItem[] path) {
				Path = path;
			}
		}
		public object ExtractPersistentValue(object criterionValue) {
			return ((IPersistentValueExtractor)session).ExtractPersistentValue(criterionValue);
		}
		IServiceProvider IInfrastructure<IServiceProvider>.Instance {
			get {
				return ((IInfrastructure<IServiceProvider>)session)?.Instance;
			}
		}
	}
	public class SmellingJoinOperandFinder : IClientCriteriaVisitor<bool> {
		XPClassInfo currentClassInfo;
		CriteriaOperator originalCriteria;
		public SmellingJoinOperandFinder(XPClassInfo currentClassInfo, CriteriaOperator originalCriteria) {
			this.currentClassInfo = currentClassInfo;
			this.originalCriteria = JoinOperandCriteriaPreprocessor.Preprocess(originalCriteria);
		}
		public bool Visit(AggregateOperand theOperand) {
			return Process(theOperand.AggregatedExpression) || Process(theOperand.Condition) || Process(theOperand.CustomAggregateOperands);
		}
		public bool Visit(JoinOperand theOperand) {
			if(currentClassInfo != null) {
				XPClassInfo joinedCi = null;
				if(!MemberInfoCollection.TryResolveTypeAlsoByShortName(theOperand.JoinTypeName, currentClassInfo, out joinedCi)) {
					throw new CannotResolveClassInfoException(string.Empty, theOperand.JoinTypeName);
				}
				CriteriaOperator joinCondition = JoinOperandCriteriaPreprocessor.Preprocess(theOperand.Condition);
				if(currentClassInfo == joinedCi && CriteriaOperator.CriterionEquals(joinCondition, originalCriteria))
					return true;
			}
			return Process(theOperand.AggregatedExpression) || Process(theOperand.Condition) || Process(theOperand.CustomAggregateOperands);
		}
		public bool Visit(OperandProperty theOperand) {
			return false;
		}
		public bool Visit(FunctionOperator theOperator) {
			return Process(theOperator.Operands);
		}
		public bool Visit(OperandValue theOperand) {
			return false;
		}
		public bool Visit(GroupOperator theOperator) {
			return Process(theOperator.Operands);
		}
		public bool Visit(InOperator theOperator) {
			if(Process(theOperator.LeftOperand))
				return true;
			return Process(theOperator.Operands);
		}
		public bool Visit(UnaryOperator theOperator) {
			return Process(theOperator.Operand);
		}
		public bool Visit(BinaryOperator theOperator) {
			return Process(theOperator.LeftOperand) || Process(theOperator.RightOperand);
		}
		public bool Visit(BetweenOperator theOperator) {
			return Process(theOperator.TestExpression) || Process(theOperator.BeginExpression) || Process(theOperator.EndExpression);
		}
		bool Process(IEnumerable ops) {
			foreach(CriteriaOperator op in ops) {
				if(Process(op))
					return true;
			}
			return false;
		}
		public bool Process(CriteriaOperator op) {
			if(ReferenceEquals(op, null))
				return false;
			return op.Accept(this);
		}
		class JoinOperandCriteriaPreprocessor : ClientCriteriaVisitorBase {
			const string OuterPropertyPrefix = "outer_";
			int level;
			protected override CriteriaOperator Visit(OperandProperty theOperand) {
				int upLevel = 0;
				string propertyName = theOperand.PropertyName;
				while(propertyName.StartsWith("^.")) {
					upLevel++;
					propertyName = propertyName.Remove(0, 2);
				}
				if((level - upLevel) < 0) {
					return new OperandProperty(OuterPropertyPrefix + propertyName);
				}
				return base.Visit(theOperand);
			}
			protected override CriteriaOperator Visit(OperandValue theOperand) {
				if(theOperand is OperandParameter) {
					return new OperandProperty(OuterPropertyPrefix + ((OperandParameter)theOperand).ParameterName);
				}
				return base.Visit(theOperand);
			}
			protected override CriteriaOperator Visit(JoinOperand theOperand) {
				level++;
				try {
					return base.Visit(theOperand);
				} finally {
					level--;
				}
			}
			protected override CriteriaOperator Visit(AggregateOperand theOperand, bool processCollectionProperty) {
				level++;
				try {
					return base.Visit(theOperand, false);
				} finally {
					level--;
				}
			}
			protected override CriteriaOperator Visit(GroupOperator theOperator) {
				CriteriaOperator result = null;
				foreach (var op in theOperator.Operands) {
					var operand = Process(op);
					result = GroupOperator.Combine(theOperator.OperatorType, result, operand);
				}
				return result;
			}
			public static CriteriaOperator Preprocess(CriteriaOperator criteria) {
				return new JoinOperandCriteriaPreprocessor().Process(criteria);
			}
		}
	}
	public class IsTopLevelAggregateCheckerClient : IClientCriteriaVisitor<bool> {
		public bool Visit(AggregateOperand theOperand) {
			return theOperand.IsTopLevel;
		}
		public bool Visit(JoinOperand theOperand) {
			return false;
		}
		public bool Visit(OperandProperty theOperand) {
			return false;
		}
		public bool Visit(FunctionOperator theOperator) {
			return Process(theOperator.Operands);
		}
		public bool Visit(OperandValue theOperand) {
			return false;
		}
		public bool Visit(GroupOperator theOperator) {
			return Process(theOperator.Operands);
		}
		public bool Visit(InOperator theOperator) {
			if(Process(theOperator.LeftOperand))
				return true;
			return Process(theOperator.Operands);
		}
		public bool Visit(UnaryOperator theOperator) {
			return Process(theOperator.Operand);
		}
		public bool Visit(BinaryOperator theOperator) {
			return Process(theOperator.LeftOperand) || Process(theOperator.RightOperand);
		}
		public bool Visit(BetweenOperator theOperator) {
			return Process(theOperator.TestExpression) || Process(theOperator.BeginExpression) || Process(theOperator.EndExpression);
		}
		bool Process(IEnumerable ops) {
			foreach(CriteriaOperator op in ops) {
				if(Process(op))
					return true;
			}
			return false;
		}
		bool Process(CriteriaOperator op) {
			if(ReferenceEquals(op, null))
				return false;
			return op.Accept(this);
		}
		public static bool IsTopLevelAggregate(CriteriaOperator op) {
			return new IsTopLevelAggregateCheckerClient().Process(op);
		}
	}
}
