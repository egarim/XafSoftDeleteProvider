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
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using DevExpress.Data.Filtering;
using DevExpress.Data.Filtering.Helpers;
using DevExpress.Data.Helpers;
using DevExpress.Utils;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.DB.Exceptions;
using DevExpress.Xpo.Exceptions;
using DevExpress.Xpo.Generators;
using DevExpress.Xpo.Metadata;
using DevExpress.Xpo.Metadata.Helpers;
namespace DevExpress.Xpo.Helpers {
	public sealed class QueryData {
		MemberPathCollection properties;
		Dictionary<XPMemberInfo, ValueAccessor> members;
		public MemberPathCollection Properties {
			get { return properties; }
		}
		SelectStatementResult data;
		int pos;
		SelectStatementResultRow row;
		public void SetData(SelectStatementResult data) {
			this.data = data;
			pos = 0;
		}
		class EmptyConverter : ValueConverter {
			public EmptyConverter() { }
			public override Type StorageType { get { return null; } }
			public override object ConvertToStorageType(object value) {
				return value;
			}
			public override object ConvertFromStorageType(object value) {
				return value;
			}
			public static readonly EmptyConverter Default = new EmptyConverter();
		}
		static public ValueConverter CreateDataConverter(XPMemberInfo member) {
			ValueConverter converter = member.Converter;
			if(converter != null)
				return converter;
			return EmptyConverter.Default;
		}
		public abstract class ValueAccessor {
			public readonly XPMemberInfo Member;
			public ValueAccessor(XPMemberInfo member) {
				this.Member = member;
			}
			public abstract object Value { get; }
			public static ValueAccessor CreateAccessor(QueryData data, XPMemberInfo member, ref int index) {
				XPMemberInfo realMember = member.ReferenceType == null ? member : member.ReferenceType.KeyProperty;
				if(realMember.SubMembers.Count == 0) {
					ValueConverter converter = CreateDataConverter(realMember);
					return converter is EmptyConverter ? new SimpleValueAccessor(data, index++, member) : new ConvertedValueAccessor(data, index++, member, converter);
				}
				else {
					List<ValueAccessor> nested = new List<ValueAccessor>();
					foreach(XPMemberInfo subMember in realMember.SubMembers) {
						if(subMember.IsPersistent)
							nested.Add(CreateAccessor(data, subMember, ref index));
					}
					return new SubValueAccessor(nested, member);
				}
			}
		}
		public sealed class TypedRefAccessor : ValueAccessor {
			ValueAccessor value;
			ValueAccessor type;
			public TypedRefAccessor(ValueAccessor value, ValueAccessor type)
				: base(null) {
				this.value = value;
				this.type = type;
			}
			public override object Value {
				get { return value.Value; }
			}
			public object TypeId {
				get { return type.Value; }
			}
		}
		class SimpleValueAccessor : ValueAccessor {
			QueryData data;
			int index;
			public SimpleValueAccessor(QueryData data, int index, XPMemberInfo member)
				: base(member) {
				this.data = data;
				this.index = index;
			}
			public override object Value {
				get { return data.row.Values[index]; }
			}
		}
		sealed class ConvertedValueAccessor : SimpleValueAccessor {
			ValueConverter converter;
			public ConvertedValueAccessor(QueryData data, int index, XPMemberInfo member, ValueConverter converter)
				: base(data, index, member) {
				this.converter = converter;
			}
			public override object Value {
				get { return converter.ConvertFromStorageType(base.Value); }
			}
		}
		sealed class NullValueAccessor : ValueAccessor {
			public NullValueAccessor(XPMemberInfo member)
				: base(member) {
			}
			public override object Value {
				get {
					return null;
				}
			}
		}
		public sealed class SubValueAccessor : ValueAccessor {
			public List<ValueAccessor> Nested;
			public SubValueAccessor(List<ValueAccessor> nested, XPMemberInfo member)
				: base(member) {
				this.Nested = nested;
			}
			public override object Value {
				get {
					List<object> value = new IdList();
					bool isnull = true;
					foreach(ValueAccessor accessor in Nested) {
						object subVal = accessor.Value;
						if(subVal != null)
							isnull = false;
						value.Add(subVal);
					}
					return isnull ? null : value;
				}
			}
		}
		public QueryData GetNestedData(XPMemberInfo mi) {
			QueryData data;
			return nestedDatas.TryGetValue(mi, out data) ? data : null;
		}
		public int Count { get { return data.Rows.Length; } }
		public bool MoveNext() {
			if(pos >= data.Rows.Length)
				return false;
			row = data.Rows[pos];
			pos++;
			return true;
		}
		Dictionary<XPMemberInfo, QueryData> nestedDatas = new Dictionary<XPMemberInfo, QueryData>();
		XPClassInfo classInfo;
		QueryData() {
		}
		void AddMember(XPMemberInfo mi, QueryData root, MemberInfoCollection parentPath, ref int index) {
			members[mi] = ValueAccessor.CreateAccessor(root, mi, ref index);
			MemberInfoCollection col = new MemberInfoCollection(root.classInfo, parentPath.Count + 1);
			col.AddRange(parentPath);
			col.Add(mi);
			properties.Add(col);
		}
		void Init(QueryData root, XPClassInfo classInfo, MemberInfoCollection parentPath, ref int index) {
			properties = new MemberPathCollection();
			members = new Dictionary<XPMemberInfo, ValueAccessor>();
			if(classInfo.IsTypedObject && classInfo.IsAbstract) {
				AddMember(classInfo.KeyProperty, root, parentPath, ref index);
				AddMember(classInfo.GetMember(XPObjectType.ObjectTypePropertyName), root, parentPath, ref index);
				XPMemberInfo optimistickLock = classInfo.OptimisticLockField;
				if(optimistickLock != null) {
					AddMember(optimistickLock, root, parentPath, ref index);
				}
			}
			else {
				foreach(XPMemberInfo m in classInfo.PersistentProperties) {
					if((!m.IsDelayed || m.ReferenceType != null) && !m.IsReadOnly) {
						ExplicitLoadingAttribute attribute = (ExplicitLoadingAttribute)m.FindAttributeInfo(typeof(ExplicitLoadingAttribute));
						int depth = attribute != null ? attribute.Depth : 1;
						XPClassInfo refType = m.ReferenceType;
						if(parentPath.Count <= depth && refType != null && !m.IsKey && !m.IsDelayed &&
							(m.IsAggregated || (attribute != null && depth > 0))) {
							QueryData nestedData = new QueryData();
							MemberInfoCollection col = new MemberInfoCollection(root.classInfo);
							col.AddRange(parentPath);
							col.Add(m);
							nestedData.Init(root, refType, col, ref index);
							nestedDatas.Add(m, nestedData);
							properties.AddRange(nestedData.Properties);
							members[m] = new NullValueAccessor(m);
						}
						else {
							AddMember(m, root, parentPath, ref index);
							if(!m.IsDelayed && refType != null && refType.IsTypedObject && refType.HasDescendants) {
								MemberInfoCollection refCol = new MemberInfoCollection(root.classInfo);
								refCol.AddRange(parentPath);
								refCol.Add(m);
								XPMemberInfo typeMember = refType.GetMember(XPObjectType.ObjectTypePropertyName);
								refCol.Add(typeMember);
								properties.Add(refCol);
								members[m] = new TypedRefAccessor(GetAccessor(m), ValueAccessor.CreateAccessor(root, typeMember, ref index));
							}
						}
					}
				}
			}
		}
		public QueryData(XPClassInfo classInfo) {
			int i = 0;
			this.classInfo = classInfo;
			Init(this, classInfo, new MemberInfoCollection(classInfo), ref i);
		}
		List<ValueAccessor> linearMembers;
		public ValueAccessor GetAccessor(int index) {
			return linearMembers[index];
		}
		public QueryData(XPClassInfo classInfo, CriteriaOperatorCollection properties) {
			int index = 0;
			linearMembers = new List<ValueAccessor>();
			foreach(CriteriaOperator prop in properties) {
				OperandProperty member = prop as OperandProperty;
				if((object)member == null) {
					member = TryExtractOperandPropertyFromExpression(prop);
				}
				if((object)member == null) {
					SimpleValueAccessor accessor = new SimpleValueAccessor(this, index++, null);
					linearMembers.Add(accessor);
				}
				else {
					MemberInfoCollection path = classInfo.ParsePersistentPath(member.PropertyName);
					linearMembers.Add(ValueAccessor.CreateAccessor(this, path[path.Count - 1], ref index));
				}
			}
		}
		OperandProperty TryExtractOperandPropertyFromExpression(CriteriaOperator op) {
			var func = op as FunctionOperator;
			if(!ReferenceEquals(func, null) && func.OperatorType == FunctionOperatorType.Iif) {
				if(func.Operands.Count == 3) {
					var valueIfFalse = func.Operands[2] as OperandValue;
					if(!ReferenceEquals(valueIfFalse, null) && valueIfFalse.Value == null) {
						return func.Operands[1] as OperandProperty;
					}
				}
			}
			return null;
		}
		public object GetValue(XPMemberInfo member) {
			return GetAccessor(member).Value;
		}
		public ValueAccessor GetAccessor(XPMemberInfo member) {
			ValueAccessor a;
			members.TryGetValue(member, out a);
			return a;
		}
	}
	public enum WaitForAsyncOperationResult {
		EmptyQueue,
		ThreadReentrancy,
		IdleQueue,
		OperationComplete
	}
	internal class LinkedUniqueQueue<T> {
		Dictionary<T, LinkedQueueItem> linksDictionary = new Dictionary<T, LinkedQueueItem>();
		LinkedQueueItem head;
		LinkedQueueItem tail;
		public class LinkedQueueItem {
			public LinkedQueueItem Prev;
			public LinkedQueueItem Next;
			public T Data;
			public LinkedQueueItem(T data) {
				Data = data;
			}
		}
		public bool EnqueueItem(T data) {
			if(linksDictionary.ContainsKey(data)) return false;
			LinkedQueueItem item = new LinkedQueueItem(data);
			if(head == null) {
				head = item;
			}
			else {
				tail.Next = item;
				item.Prev = tail;
			}
			tail = item;
			linksDictionary.Add(data, item);
			return true;
		}
		public bool DequeueItem(out T data) {
			if(head == null) {
				data = default(T);
				return false;
			}
			LinkedQueueItem item = head;
			head = item.Next;
			if(head == null) tail = null;
			data = item.Data;
			linksDictionary.Remove(data);
			return true;
		}
		public bool Remove(T data) {
			LinkedQueueItem item;
			if(!linksDictionary.TryGetValue(data, out item)) return false;
			if(head == item) {
				head = item.Next;
				if(head == null) tail = null;
				item.Next = null;
				linksDictionary.Remove(data);
				return true;
			}
			if(tail == item) {
				tail = item.Prev;
				tail.Next = null;
				item.Prev = null;
				linksDictionary.Remove(data);
				return true;
			}
			item.Prev.Next = item.Next;
			item.Next.Prev = item.Prev;
			item.Prev = null;
			item.Next = null;
			return true;
		}
		public bool Contains(T data) {
			return linksDictionary.ContainsKey(data);
		}
		public int Count {
			get { return linksDictionary.Count; }
		}
	}
	internal class PriorityRequestQueue {
		LinkedUniqueQueue<AsyncRequest> idleQueue = new LinkedUniqueQueue<AsyncRequest>();
		LinkedUniqueQueue<AsyncRequest> normalQueue = new LinkedUniqueQueue<AsyncRequest>();
		LinkedUniqueQueue<AsyncRequest> highQueue = new LinkedUniqueQueue<AsyncRequest>();
		public void Enqueue(AsyncRequest request) {
			if(request == null) return;
			if(request.IsCanceled) return;
			switch(request.Priority) {
				case AsyncRequestPriority.Normal:
					normalQueue.EnqueueItem(request);
					break;
				case AsyncRequestPriority.Idle:
					idleQueue.EnqueueItem(request);
					break;
				case AsyncRequestPriority.High:
					highQueue.EnqueueItem(request);
					break;
				default:
					throw new InvalidOperationException();
			}
		}
		public AsyncRequest Dequeue() {
			if(Count == 0) return null;
			AsyncRequest result;
			do {
				if(highQueue.DequeueItem(out result) && !result.IsCanceled) return result;
			} while(result != null);
			do {
				if(normalQueue.DequeueItem(out result) && !result.IsCanceled) return result;
			} while(result != null);
			do {
				if(idleQueue.DequeueItem(out result) && !result.IsCanceled) return result;
			} while(result != null);
			return null;
		}
		public void NotifyPriorityChanged(AsyncRequest request) {
			if(idleQueue.Remove(request) || normalQueue.Remove(request) || highQueue.Remove(request)) {
				Enqueue(request);
			}
		}
		public bool Contains(AsyncRequest request) {
			return idleQueue.Contains(request) || normalQueue.Contains(request) || highQueue.Contains(request);
		}
		public int Count {
			get { return idleQueue.Count + normalQueue.Count + highQueue.Count; }
		}
		public bool HasOnlyIdle {
			get { return idleQueue.Count > 0 && normalQueue.Count == 0 && highQueue.Count == 0; }
		}
	}
	internal class AsyncExecuteQueue {
		int syncRequestsCount = 0;
		AutoResetEvent syncExecuteEvent = new AutoResetEvent(false);
		SendOrPostCallback syncExecuteDelegate;
		object syncExecuteState;
		Thread executingThread;
		public AsyncExecuteQueue() {
			syncExecuteDelegate = new SendOrPostCallback(ExecuteInvoke);
		}
		public bool ContainsRequest(AsyncRequest request, bool includeCurrentRequest) {
			return (includeCurrentRequest && currentRequest == request) || asyncQueue.Contains(request);
		}
		public WaitForAsyncOperationResult WaitForAsyncOperationEnd() {
			return WaitForAsyncOperationEnd(null);
		}
		public WaitForAsyncOperationResult WaitForAsyncOperationEnd(AsyncRequest request) {
			if(executingThread == Thread.CurrentThread) return WaitForAsyncOperationResult.ThreadReentrancy;
			lock(this) {
				if(syncRequestsCount < 0) syncRequestsCount = 0;	
				syncRequestsCount++;
			}
			lock(asyncQueue) {
				if(request != null && !request.IsCanceled && !request.IsInQueueOrCurrent(this)) throw new InvalidOperationException("Request not found.");
			}
			while(true) {
				lock(asyncQueue) {
					if(asyncThreadNotWorkingButHasExecTask) {
						asyncThreadNotWorkingButHasExecTask = false;
					}
					else {
						if(!asyncThreadWorking) {
							lock(this) {
								syncRequestsCount--;
							}
							return WaitForAsyncOperationResult.EmptyQueue;
						}
						else if(asyncThreadWorkingButAllAreIdle && (request == null || request.IsCanceled || !request.IsInQueueOrCurrent(this))) {
							return WaitForAsyncOperationResult.IdleQueue;
						}
					}
				}
				if(syncExecuteEvent.WaitOne()) {
					lock(asyncQueue) {
						if(asyncThreadNotWorkingButHasExecTask) {
							asyncThreadNotWorkingButHasExecTask = false;
						}
						else {
							if(!asyncThreadWorking) {
								lock(this) {
									syncRequestsCount--;
								}
								return WaitForAsyncOperationResult.EmptyQueue;
							}
							else if(asyncThreadWorkingButAllAreIdle && (request == null || request.IsCanceled || !request.IsInQueueOrCurrent(this))) {
								return WaitForAsyncOperationResult.IdleQueue;
							}
						}
					}
					ExecuteInvokeInfo lastInfo = syncExecuteState as ExecuteInvokeInfo;
					if(lastInfo == null || !lastInfo.Executed) {
						syncExecuteDelegate(syncExecuteState);
						syncExecuteState = null;
						if(lastInfo != null && lastInfo.OperationEnd) {
							bool allowExit = request == null;
							if(!allowExit) {
								if(request.IsCanceled) { 
									allowExit = true;
								}
								else {
									lock(asyncQueue) {
										allowExit = request.IsInQueue(this);
									}
								}
							}
							if(allowExit) {
								lock(this) {
									syncRequestsCount--;
								}
								return WaitForAsyncOperationResult.OperationComplete;
							}
						}
					}
					if(request != null) {
						if(request.IsCanceled) return WaitForAsyncOperationResult.OperationComplete;
						lock(asyncQueue) {
							if(!request.IsInQueueOrCurrent(this)) return WaitForAsyncOperationResult.OperationComplete;
						}
					}
				}
			}
		}
		public void Invoke(SynchronizationContext syncContext, SendOrPostCallback d, object state, bool operationEnd) {
			ExecuteInvokeInfo invokeInfo = new ExecuteInvokeInfo(d, state, operationEnd);
			do {
				bool posted = false;
				lock(this) {
					syncExecuteState = invokeInfo;
					syncExecuteEvent.Set();
					if(syncRequestsCount <= 0) {
						posted = true;
						syncContext.Post(syncExecuteDelegate, syncExecuteState);
					}
				}
				if(posted) {
					invokeInfo.OperationEndEvent.WaitOne();
					break;
				}
			} while(!invokeInfo.OperationEndEvent.WaitOne(100));
			if(invokeInfo.Error != null) throw invokeInfo.Error;
		}
		void ExecuteInvoke(object state) {
			ExecuteInvokeInfo info = state as ExecuteInvokeInfo;
			if(info == null) return;
			try {
				lock(info) {
					if(info.Executed) return;
					executingThread = Thread.CurrentThread;
					info.Executed = true;
				}
				try {
					if(info.D == null)
						throw new InvalidOperationException(Res.GetString(Res.Async_InternalErrorAsyncActionIsAlreadyDisposed));
					info.D(info.State);
				}
				catch(Exception ex) {
					info.Error = ex;
				}
			}
			finally {
				executingThread = null;
				info.Dispose();
				info.OperationEndEvent.Set();
			}
		}
		class ExecuteInvokeInfo : IDisposable {
			SendOrPostCallback d;
			object state;
			bool executed;
			Exception error;
			readonly bool operationEnd;
			readonly ManualResetEvent operationEndEvent = new ManualResetEvent(false);
			public ManualResetEvent OperationEndEvent {
				get { return operationEndEvent; }
			}
			public SendOrPostCallback D {
				get { return d; }
			}
			public object State {
				get { return state; }
			}
			public bool Executed {
				get { return executed; }
				set { executed = value; }
			}
			public Exception Error {
				get { return error; }
				set { error = value; }
			}
			public bool OperationEnd {
				get { return operationEnd; }
			}
			public ExecuteInvokeInfo(SendOrPostCallback d, object state, bool operationEnd) {
				this.d = d;
				this.state = state;
				this.operationEnd = operationEnd;
			}
			public void Dispose() {
				d = null;
				state = null;
			}
		}
		bool asyncThreadWorking;
		bool asyncThreadNotWorkingButHasExecTask;
		bool asyncThreadWorkingButAllAreIdle;
		PriorityRequestQueue asyncQueue = new PriorityRequestQueue();
		public void ExecRequest(AsyncRequest request) {
			lock(asyncQueue) {
				asyncQueue.Enqueue(request);
				if(!asyncThreadWorking) {
					asyncThreadWorking = true;
					asyncThreadWorkingButAllAreIdle = false;
					ThreadPool.QueueUserWorkItem(new WaitCallback(AsyncWorkCycle));
				}
			}
		}
		public void NotifyPriorityChanged(AsyncRequest request) {
			lock(asyncQueue) {
				asyncQueue.NotifyPriorityChanged(request);
			}
		}
		AsyncRequest currentRequest;
		void AsyncWorkCycle(object state) {
			bool hasExecTask = false;
			while(true) {
				lock(asyncQueue) {
					if(asyncQueue.Count == 0) {
						asyncThreadWorking = false;
						asyncThreadNotWorkingButHasExecTask = hasExecTask;
						syncExecuteEvent.Set();
						currentRequest = null;
						return;
					}
					if(asyncQueue.HasOnlyIdle) {
						asyncThreadWorkingButAllAreIdle = true;
						syncExecuteEvent.Set();
					}
					currentRequest = asyncQueue.Dequeue();
				}
				currentRequest.Exec();
				hasExecTask = true;
			}
		}
	}
	public enum AsyncRequestPriority {
		Normal,
		Idle,
		High
	}
	internal class VirtualAsyncRequest : AsyncRequest {
		bool executed = false;
		public void Stop() {
			executed = true;
		}
		public VirtualAsyncRequest(SynchronizationContext context)
			: base(context) {
		}
		public override bool IsInQueue(AsyncExecuteQueue q) {
			return !executed || base.IsInQueue(q);
		}
		public override bool IsInQueueOrCurrent(AsyncExecuteQueue q) {
			return !executed || base.IsInQueueOrCurrent(q);
		}
	}
	internal class AsyncRequest {
		bool isCanceled;
		AsyncExecuteQueue queue;
		AsyncRequestPriority priority;
		readonly SynchronizationContext syncContext;
		readonly AsyncRequestExec exec;
		readonly List<AsyncRequest> nestedRequests = new List<AsyncRequest>();
		public SynchronizationContext SyncContext { get { return syncContext; } }
		public AsyncRequestPriority Priority { get { return priority; } }
		public bool IsCanceled { get { return isCanceled; } }
		public AsyncRequest(SynchronizationContext syncContext) {
			this.syncContext = syncContext;
		}
		public AsyncRequest(SynchronizationContext syncContext, AsyncRequestExec exec)
			: this(syncContext) {
			this.exec = exec;
		}
		public AsyncRequest AddNestedRequest(AsyncRequest nested) {
			if(nested == null) return nested;
			lock(nestedRequests) {
				if(IsCanceled) nested.Cancel();
				nested.SetPriority(Priority);
				nestedRequests.Add(nested);
			}
			return nested;
		}
		public AsyncRequest RemoveNestedRequest(AsyncRequest nested) {
			if(nested == null) return nested;
			lock(nestedRequests) {
				nestedRequests.Remove(nested);
			}
			return nested;
		}
		public void Cancel() {
			isCanceled = true;
			lock(nestedRequests) {
				foreach(AsyncRequest nested in nestedRequests) {
					nested.Cancel();
				}
			}
		}
		public void SetPriority(AsyncRequestPriority priority) {
			if(this.priority == priority) return;
			this.priority = priority;
			if(queue != null) queue.NotifyPriorityChanged(this);
			lock(nestedRequests) {
				foreach(AsyncRequest nested in nestedRequests) {
					nested.SetPriority(priority);
				}
			}
		}
		public virtual bool IsInQueue(AsyncExecuteQueue q) {
			if(q != null && q.ContainsRequest(this, false)) return true;
			lock(nestedRequests) {
				foreach(AsyncRequest nested in nestedRequests) {
					if(nested.IsInQueue(q)) return true;
				}
			}
			return false;
		}
		public virtual bool IsInQueueOrCurrent(AsyncExecuteQueue q) {
			if(q != null && q.ContainsRequest(this, true)) return true;
			lock(nestedRequests) {
				foreach(AsyncRequest nested in nestedRequests) {
					if(nested.IsInQueueOrCurrent(q)) return true;
				}
			}
			return false;
		}
		public void Exec() {
			if(exec != null) {
				exec(this);
			}
		}
		public AsyncRequest Start(AsyncExecuteQueue queue) {
			return StartWithPriority(queue, AsyncRequestPriority.Normal);
		}
		public AsyncRequest StartWithPriority(AsyncExecuteQueue queue, AsyncRequestPriority priority) {
			if(this.queue != null || queue == null || exec == null || syncContext == null) return null;
			SetPriority(priority);
			this.queue = queue;
			this.queue.ExecRequest(this);
			return this;
		}
	}
	internal delegate void AsyncRequestExec(AsyncRequest request);
	public delegate void AsyncLoadObjectsCallback(ICollection[] result, Exception ex);
	public delegate void AsyncFindObjectCallback(object result, Exception ex);
	class ObjectCollectionLoader {
		readonly IDataLayer dataLayer;
		public IDataLayer DataLayer { get { return dataLayer; } }
		readonly Session session;
		Session Session { get { return session; } }
		public ObjectCollectionLoader(Session session, IDataLayer dataLayer) {
			this.session = session;
			this.dataLayer = dataLayer;
		}
		readonly Dictionary<XPClassInfo, StubMap> stubObjects = new Dictionary<XPClassInfo, StubMap>();
		readonly Dictionary<XPClassInfo, IDictionary<object, object>> delayedObjects = new Dictionary<XPClassInfo, IDictionary<object, object>>();
		public ObjectSet objectsToFireLoaded = new ObjectSet();
		static readonly IDictionary<object, object> emptyList = new Dictionary<object, object>();
		IDataLayerAsync GetDataLayerAsync() {
			if(DataLayer == null) {
				return null;
			}
			var dataLayerAsync = DataLayer as IDataLayerAsync;
			if(dataLayerAsync == null) {
				throw new InvalidOperationException(Xpo.Res.GetString(Xpo.Res.Async_DataLayerDoesNotImplementIDataLayerAsync, DataLayer.GetType().FullName));
			}
			return dataLayerAsync;
		}
		IDictionary<object, object> GetDelayedList(XPClassInfo type) {
			IDictionary<object, object> list;
			return delayedObjects.TryGetValue(type, out list) ? list : emptyList;
		}
		IDictionary<object, object> CreateDelayedList(XPClassInfo type) {
			IDictionary<object, object> list;
			if(!delayedObjects.TryGetValue(type, out list)) {
				list = new Dictionary<object, object>();
				delayedObjects[type] = list;
			}
			return list;
		}
		void DelayLoading(XPClassInfo type, object id, object obj) {
			CreateDelayedList(type)[id] = obj;
		}
		void ProcessLoaded(XPClassInfo classInfo, object id) {
			GetDelayedList(classInfo).Remove(id);
		}
		bool IsDelayed(XPClassInfo classInfo, object id) {
			return GetDelayedList(classInfo).ContainsKey(id);
		}
		IObjectMap CreateStubMap(XPClassInfo type) {
			StubMap list;
			if(!stubObjects.TryGetValue(type.IdClass, out list)) {
				list = new StubMap();
				stubObjects[type.IdClass] = list;
			}
			return list;
		}
		public static bool NeedReload(Session session, XPClassInfo ci, bool forceReload, out OptimisticLockingReadMergeBehavior loadMergeState, Func<bool> isObjectVersionChanged) {
			loadMergeState = OptimisticLockingReadMergeBehavior.Default;
			OptimisticLockingReadBehavior versionChangedAction = ci.OptimisticLockingReadBehavior;
			if(versionChangedAction == OptimisticLockingReadBehavior.Default)
				versionChangedAction = session.OptimisticLockingReadBehavior;
			if(versionChangedAction == OptimisticLockingReadBehavior.Default)
				versionChangedAction = XpoDefault.OptimisticLockingReadBehavior;
			if(forceReload) {
				switch(versionChangedAction) {
					case OptimisticLockingReadBehavior.MergeCollisionIgnore:
						if(session.InTransaction && (ci.TrackPropertiesModifications ?? session.TrackPropertiesModifications)) {
							loadMergeState = OptimisticLockingReadMergeBehavior.Ignore;
						}
						break;
					case OptimisticLockingReadBehavior.MergeCollisionThrowException:
						if(session.InTransaction && (ci.TrackPropertiesModifications ?? session.TrackPropertiesModifications)) {
							loadMergeState = OptimisticLockingReadMergeBehavior.ThrowException;
						}
						break;
					case OptimisticLockingReadBehavior.MergeCollisionReload:
						if(session.InTransaction && (ci.TrackPropertiesModifications ?? session.TrackPropertiesModifications)) {
							loadMergeState = OptimisticLockingReadMergeBehavior.Reload;
						}
						break;
				}
				return true;
			}
			if((ci.OptimisticLockingBehavior == OptimisticLockingBehavior.ConsiderOptimisticLockingField
				|| ci.OptimisticLockingBehavior == OptimisticLockingBehavior.NoLocking) && !isObjectVersionChanged())
				return false;
			switch(versionChangedAction) {
				case OptimisticLockingReadBehavior.MergeCollisionIgnore:
					if(!session.InTransaction)
						return true;
					if(ci.TrackPropertiesModifications ?? session.TrackPropertiesModifications) {
						loadMergeState = OptimisticLockingReadMergeBehavior.Ignore;
						return true;
					}
					return false;
				case OptimisticLockingReadBehavior.MergeCollisionThrowException:
					if(!session.InTransaction)
						return true;
					if(ci.TrackPropertiesModifications ?? session.TrackPropertiesModifications) {
						loadMergeState = OptimisticLockingReadMergeBehavior.ThrowException;
						return true;
					}
					return false;
				case OptimisticLockingReadBehavior.MergeCollisionReload:
					if(!session.InTransaction)
						return true;
					if(ci.TrackPropertiesModifications ?? session.TrackPropertiesModifications) {
						loadMergeState = OptimisticLockingReadMergeBehavior.Reload;
						return true;
					}
					return false;
				case OptimisticLockingReadBehavior.Ignore:
					return false;
				case OptimisticLockingReadBehavior.ReloadObject:
					return true;
				case OptimisticLockingReadBehavior.Mixed:
					return !session.InTransaction;
				case OptimisticLockingReadBehavior.ThrowException:
					throw new LockingException();
				default:
					throw new InvalidOperationException(Res.GetString(Res.Loader_ReloadError, typeof(OptimisticLockingReadBehavior).Name, versionChangedAction.ToString()));
			}
		}
		public static bool AcceptLoadPropertyAndResetModified(bool trackPropertiesModifications, OptimisticLockingReadMergeBehavior loadMerge, object currentObject, XPClassInfo classInfo, XPMemberInfo member, Func<object, object> getNewValue, object getNewValueArgument) {
			OptimisticLockingReadMergeBehavior currentMergeBehavior = member == classInfo.OptimisticLockField
				? OptimisticLockingReadMergeBehavior.Default
				: (member.MergeCollisionBehavior == OptimisticLockingReadMergeBehavior.Default ? loadMerge : member.MergeCollisionBehavior);
			switch(currentMergeBehavior) {
				case OptimisticLockingReadMergeBehavior.Ignore:
					if(member.GetModified(currentObject)) {
						member.ResetModified(currentObject);
						member.SetModified(currentObject, getNewValue(getNewValueArgument));
						return false;
					}
					break;
				case OptimisticLockingReadMergeBehavior.Reload:
					if(member.GetModified(currentObject)) {
						if(member.IsDelayed && member.ReferenceType == null) return false;
						if(!LockingHelper.IsModifiedInDataLayer(member, member.GetOldValue(currentObject), getNewValue(getNewValueArgument)))
							return false;
						member.ResetModified(currentObject);
					}
					break;
				case OptimisticLockingReadMergeBehavior.ThrowException:
					if(member.GetModified(currentObject)) {
						if(member.IsDelayed && member.ReferenceType == null) return false;
						if(!LockingHelper.IsModifiedInDataLayer(member, member.GetOldValue(currentObject), getNewValue(getNewValueArgument)))
							return false;
						throw new LockingException();
					}
					break;
				default:
					if(trackPropertiesModifications) {
						if(member.GetModified(currentObject))
							member.ResetModified(currentObject);
					}
					break;
			}
			return true;
		}
		class AsyncLoadRequest : AsyncRequest {
			LoadObjectsContext loadObjectsContext;
			AsyncLoadObjectsCallback callback;
			public LoadObjectsContext LoadObjectsContext { get { return loadObjectsContext; } }
			public AsyncLoadObjectsCallback Callback { get { return callback; } }
			public AsyncLoadRequest(LoadObjectsContext loadObjectsContext, AsyncLoadObjectsCallback callback, SynchronizationContext syncContext, AsyncRequestExec exec)
				: base(syncContext, exec) {
				this.loadObjectsContext = loadObjectsContext;
				this.callback = callback;
			}
		}
		class LoadDataResult {
			ObjectsQuery[] queries;
			QueryData[] data;
			SelectedData result;
			public ObjectsQuery[] Queries { get { return queries; } }
			public QueryData[] Data { get { return data; } }
			public SelectedData Result { get { return result; } }
			public LoadDataResult(ObjectsQuery[] queries, QueryData[] data, SelectedData result) {
				this.queries = queries;
				this.data = data;
				this.result = result;
			}
		}
		class LoadObjectsContext {
			ObjectsQuery[] queries;
			ICollection[] result;
			ExpandedCriteriaHolder[] expanded;
			bool[] requiresResorting;
			ObjectsQuery[] loadDataQuery;
			public ObjectsQuery[] Queries { get { return queries; } }
			public ICollection[] Result { get { return result; } }
			public ExpandedCriteriaHolder[] Expanded { get { return expanded; } }
			public bool[] RequiresResorting { get { return requiresResorting; } }
			public ObjectsQuery[] LoadDataQuery { get { return loadDataQuery; } }
			public LoadObjectsContext(ObjectsQuery[] queries, ICollection[] result, ExpandedCriteriaHolder[] expanded, bool[] requiresResorting, ObjectsQuery[] loadDataQuery)
				: this(queries, result, expanded, requiresResorting) {
				this.loadDataQuery = loadDataQuery;
			}
			public LoadObjectsContext(ObjectsQuery[] queries, ICollection[] result, ExpandedCriteriaHolder[] expanded, bool[] requiresResorting) {
				this.queries = queries;
				this.result = result;
				this.expanded = expanded;
				this.requiresResorting = requiresResorting;
			}
		}
		class LoadStub {
			OptimisticLockingReadMergeBehavior loadMerge;
			object id;
			bool isProceed;
			bool isNeedReload;
			XPClassInfo classInfo;
			object loadedObject;
			Dictionary<XPMemberInfo, bool> delayedMembers = new Dictionary<XPMemberInfo, bool>();
			Dictionary<XPMemberInfo, object> memberValues = new Dictionary<XPMemberInfo, object>();
			public object Id {
				get { return id; }
			}
			public object LoadedObject {
				get { return loadedObject; }
				set { loadedObject = value; }
			}
			public bool IsProceed {
				get { return isProceed; }
			}
			public bool IsNeedReload {
				get { return isNeedReload; }
			}
			public XPClassInfo ClassInfo {
				get { return classInfo; }
			}
			public OptimisticLockingReadMergeBehavior LoadMerge {
				get { return loadMerge; }
				set { loadMerge = value; }
			}
			public LoadStub(XPClassInfo classInfo, object id) {
				this.classInfo = classInfo;
				this.id = id;
			}
			public LoadStub(XPClassInfo classInfo, object id, object loadedObject)
				: this(classInfo, id) {
				this.loadedObject = loadedObject;
			}
			public void SetMemberValue(XPMemberInfo mi, object value, bool isDelayed) {
				memberValues[mi] = value;
				delayedMembers[mi] = true;
			}
			public void SetMemberValue(XPMemberInfo mi, object value) {
				memberValues[mi] = value;
			}
			public object GetMemberValue(XPMemberInfo mi) {
				object result;
				if(memberValues.TryGetValue(mi, out result)) return result;
				if(loadedObject == null) return null;
				return mi.GetValue(loadedObject);
			}
			public bool IsDelayedMember(XPMemberInfo mi) {
				return delayedMembers.ContainsKey(mi);
			}
			public ICollection Members { get { return memberValues.Keys; } }
			public void Proceed() { isProceed = true; }
			public void NeedReload() { isNeedReload = true; }
		}
		class ProcessAssociationStubData {
			public readonly object OldData;
			public readonly object NewData;
			public ProcessAssociationStubData(object oldData, object newData) {
				this.OldData = oldData;
				this.NewData = newData;
			}
		}
		class StubMap : Dictionary<object, object>, IObjectMap {
			void IObjectMap.Add(object theObject, object id) {
				base[id] = theObject;
			}
			void IObjectMap.Remove(object id) {
				base.Remove(id);
			}
			public object Get(object id) {
				object res;
				return TryGetValue(id, out res) ? res : null;
			}
			public int CompactCache() {
				return 0;
			}
			public void ClearCache() {
				Clear();
			}
		}
		public object LoadObjectsAsync(ObjectsQuery[] queries, AsyncLoadObjectsCallback callback, SynchronizationContext syncContext) {
			Guard.ArgumentNotNull(callback, nameof(callback));
			Guard.ArgumentNotNull(syncContext, nameof(syncContext));
			return new AsyncLoadRequest(PrepareLoadObjects(queries), callback, syncContext, new AsyncRequestExec(LoadObjectsExecRequest)).Start(session.AsyncExecuteQueue);
		}
		LoadObjectsContext asyncLoadObjectsContext;
		AsyncLoadObjectsCallback asyncCallback;
		SynchronizationContext syncContext;
		void LoadObjectsExecRequest(AsyncRequest ar) {
			AsyncLoadRequest request = (AsyncLoadRequest)ar;
			this.asyncLoadObjectsContext = request.LoadObjectsContext;
			this.asyncCallback = request.Callback;
			this.syncContext = request.SyncContext;
			try {
				if(asyncLoadObjectsContext.LoadDataQuery == null) {
					session.AsyncExecuteQueue.Invoke(syncContext, new SendOrPostCallback(delegate (object param) {
						asyncCallback(asyncLoadObjectsContext.Result, null);
					}), null, true);
					return;
				}
				LoadDataResult loadDataResult = InternalLoadData(asyncLoadObjectsContext.LoadDataQuery);
				List<ICollection> loadObjectsResult = new List<ICollection>();
				session.AsyncExecuteQueue.Invoke(syncContext, new SendOrPostCallback(InternalLoadObjectsInStack), new object[] { loadDataResult, loadObjectsResult }, false);
				while(delayedObjects.Count > 0) {
					LoadDelayedObjectsAsync();
				}
				if(loadObjectsResult.Count == 0 && asyncLoadObjectsContext.Queries.Length > 0) {
					for(int i = 0; i < asyncLoadObjectsContext.Queries.Length; i++) {
						loadObjectsResult.Add(Array.Empty<object>());
					}
				}
				session.AsyncExecuteQueue.Invoke(syncContext, new SendOrPostCallback(EndLoadObjectsAndCallBack), loadObjectsResult.ToArray(), true);
			}
			catch(Exception ex) {
				try {
					session.AsyncExecuteQueue.Invoke(syncContext, new SendOrPostCallback(CallBackException), ex, true);
				}
				catch(Exception) { }
			}
		}
		void LoadDelayedObjectsAsync() {
			ObjectSet toLoad = new ObjectSet();
			ObjectsQuery[] criteria = PrepareLoadDelayedObjects(toLoad);
			if(criteria.Length > 0) {
				LoadDataResult loadDataResult = InternalLoadData(criteria);
				session.AsyncExecuteQueue.Invoke(syncContext, new SendOrPostCallback(EndLoadDelayedObjectsInStack), new object[] { loadDataResult, toLoad }, false);
			}
			else {
				delayedObjects.Clear();
			}
		}
		void PrepareLoadObjectsInStack(object param) {
			ObjectsQuery[] queries = (ObjectsQuery[])param;
			asyncLoadObjectsContext = PrepareLoadObjects(queries);
		}
		void EndLoadObjectsAndCallBack(object param) {
			ICollection[] result = EndLoadObjects(asyncLoadObjectsContext, (ICollection[])param, true);
			SessionIdentityMap.Extract(session).Compact();
			asyncCallback(result, null);
		}
		void InternalLoadObjectsInStack(object param) {
			object[] oParams = (object[])param;
			LoadDataResult loadDataResult = (LoadDataResult)oParams[0];
			List<ICollection> loadObjectsResult = (List<ICollection>)oParams[1];
			loadObjectsResult.AddRange(InternalLoadObjects(loadDataResult, true));
		}
		void EndLoadDelayedObjectsInStack(object param) {
			object[] oParams = (object[])param;
			LoadDataResult loadDataResult = (LoadDataResult)oParams[0];
			ObjectSet toLoad = (ObjectSet)oParams[1];
			EndLoadDelayedObjects(loadDataResult, toLoad, true);
		}
		void CallBackException(object param) {
			asyncCallback(null, (Exception)param);
		}
		void LoadDelayedObjects(bool useStubs) {
			ObjectSet toLoad = new ObjectSet();
			ObjectsQuery[] criterions = PrepareLoadDelayedObjects(toLoad);
			if(criterions.Length > 0) {
				LoadDataResult loadDataResult = InternalLoadData(criterions);
				EndLoadDelayedObjects(loadDataResult, toLoad, useStubs);
			}
			else {
				delayedObjects.Clear();
			}
		}
		async Task LoadDelayedObjectsAsync(bool useStubs, CancellationToken cancellationToken = default(CancellationToken)) {
			ObjectSet toLoad = new ObjectSet();
			ObjectsQuery[] criterions = PrepareLoadDelayedObjects(toLoad);
			if(criterions.Length > 0) {
				LoadDataResult loadDataResult = await InternalLoadDataAsync(criterions, cancellationToken);
				EndLoadDelayedObjects(loadDataResult, toLoad, useStubs);
			}
			else {
				delayedObjects.Clear();
			}
		}
#if DEBUGTEST
		public static bool LoadObjectsUseAsyncCore;
#endif
		public ICollection[] LoadObjects(ObjectsQuery[] queries) {
			session.WaitForAsyncOperationEnd();
#if DEBUGTEST
			bool stubsFlag = LoadObjectsUseAsyncCore;
#else
			const bool stubsFlag = false;
#endif
			LoadObjectsContext loadObjectsContext = PrepareLoadObjects(queries);
			if(loadObjectsContext.LoadDataQuery == null) return loadObjectsContext.Result;
			ICollection[] objs;
			SessionStateStack.Enter(Session, SessionState.GetObjectsNonReenterant);
			try {
				objs = InternalLoadObjects(InternalLoadData(loadObjectsContext.LoadDataQuery), stubsFlag);
				while(delayedObjects.Count > 0) {
					LoadDelayedObjects(stubsFlag);
				}
			}
			finally {
				SessionStateStack.Leave(Session, SessionState.GetObjectsNonReenterant);
			}
			return EndLoadObjects(loadObjectsContext, objs, stubsFlag);
		}
		public async Task<ICollection[]> LoadObjectsAsync(ObjectsQuery[] queries, CancellationToken cancellationToken = default(CancellationToken)) {
			LoadObjectsContext loadObjectsContext = await PrepareLoadObjectsAsync(queries, cancellationToken);
			if(loadObjectsContext.LoadDataQuery == null) return loadObjectsContext.Result;
			ICollection[] objs;
			int asyncOperationId = SessionStateStack.GetNewAsyncOperationId();
			await SessionStateStack.EnterAsync(Session, SessionState.GetObjectsNonReenterant, asyncOperationId, cancellationToken);
			try {
				cancellationToken.ThrowIfCancellationRequested();
				LoadDataResult loadDataResult = await InternalLoadDataAsync(loadObjectsContext.LoadDataQuery, cancellationToken);
				objs = InternalLoadObjects(loadDataResult, false);
				while(delayedObjects.Count > 0) {
					cancellationToken.ThrowIfCancellationRequested();
					await LoadDelayedObjectsAsync(false, cancellationToken);
				}
			}
			finally {
				SessionStateStack.Leave(Session, SessionState.GetObjectsNonReenterant, asyncOperationId);
			}
			cancellationToken.ThrowIfCancellationRequested();
			return EndLoadObjects(loadObjectsContext, objs, false);
		}
		ObjectsQuery[] PrepareLoadDelayedObjects(ObjectSet toLoad) {
			List<ObjectsQuery> criterions = new List<ObjectsQuery>();
			foreach(KeyValuePair<XPClassInfo, IDictionary<object, object>> entry in delayedObjects) {
				if(entry.Value.Count > 0) {
					List<object> list = new List<object>(entry.Value.Keys);
					foreach(KeyValuePair<object, object> obj in entry.Value)
						toLoad.Add(obj.Value);
					list.Sort();
					for(int i = 0; i < list.Count;) {
						int inSize = XpoDefault.GetTerminalInSize(list.Count - i);
						criterions.Add(new ObjectsQuery(entry.Key, new InOperator(entry.Key.KeyProperty.Name, GetRangeHelper.GetRange(list, i, inSize)), null, 0, 0, null, false));
						i += inSize;
					}
				}
			}
			return criterions.ToArray();
		}
		void EndLoadDelayedObjects(LoadDataResult loadDataResult, ObjectSet toLoad, bool useStubs) {
			foreach(ICollection objects in InternalLoadObjects(loadDataResult, useStubs)) {
				foreach(object obj in objects)
					toLoad.Remove(obj);
			}
			if(toLoad.Count > 0) {
				if(Session.SuppressExceptionOnReferredObjectAbsentInDataStore) {
					foreach(object obj in toLoad) {
						XPClassInfo ci;
						if(useStubs) {
							ci = ((LoadStub)obj).ClassInfo;
						}
						else {
							ci = Session.GetClassInfo(obj);
						}
						IDictionary<object, object> list = GetDelayedList(ci);
						foreach(KeyValuePair<object, object> ent in list) {
							if(ReferenceEquals(ent.Value, obj)) {
								list.Remove(ent.Key);
								break;
							}
						}
					}
				}
				else {
					string objsList = string.Empty;
					foreach(object obj in toLoad) {
						XPClassInfo ci;
						if(useStubs) {
							ci = ((LoadStub)obj).ClassInfo;
						}
						else {
							ci = Session.GetClassInfo(obj);
						}
						IDictionary<object, object> list = GetDelayedList(ci);
						foreach(KeyValuePair<object, object> ent in list) {
							if(ReferenceEquals(ent.Value, obj)) {
								if(objsList.Length > 0)
									objsList += ", ";
								objsList += ci.FullName + "(" + ent.Key.ToString() + ")";
								break;
							}
						}
					}
					throw new CannotLoadObjectsException(objsList);
				}
			}
		}
		bool DoEnsureIsTypedObjectValid = true;
		LoadDataResult InternalLoadData(ObjectsQuery[] queries) {
			if(DoEnsureIsTypedObjectValid) {
				Session.TypesManager.EnsureIsTypedObjectValid();
				DoEnsureIsTypedObjectValid = false;
			}
			QueryData[] data = new QueryData[queries.Length];
			SelectStatement[] roots = new SelectStatement[queries.Length];
			for(int i = 0; i < queries.Length; i++) {
				data[i] = new QueryData(queries[i].ClassInfo);
				roots[i] = ClientSelectSqlGenerator.GenerateSelect(queries[i].ClassInfo, queries[i].Criteria,
					data[i].Properties, queries[i].Sorting, null, null, queries[i].CollectionCriteriaPatcher, queries[i].SkipSelectedRecords, queries[i].TopSelectedRecords);
			}
			SelectedData result = dataLayer.SelectData(roots);
			return new LoadDataResult(queries, data, result);
		}
		async Task<LoadDataResult> InternalLoadDataAsync(ObjectsQuery[] queries, CancellationToken cancellationToken = default(CancellationToken)) {
			if(DoEnsureIsTypedObjectValid) {
				Session.TypesManager.EnsureIsTypedObjectValid();
				DoEnsureIsTypedObjectValid = false;
			}
			QueryData[] data = new QueryData[queries.Length];
			SelectStatement[] roots = new SelectStatement[queries.Length];
			for(int i = 0; i < queries.Length; i++) {
				data[i] = new QueryData(queries[i].ClassInfo);
				roots[i] = ClientSelectSqlGenerator.GenerateSelect(queries[i].ClassInfo, queries[i].Criteria,
					data[i].Properties, queries[i].Sorting, null, null, queries[i].CollectionCriteriaPatcher, queries[i].SkipSelectedRecords, queries[i].TopSelectedRecords);
			}
			cancellationToken.ThrowIfCancellationRequested();
			SelectedData result = await GetDataLayerAsync().SelectDataAsync(cancellationToken, roots).ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();
			return new LoadDataResult(queries, data, result);
		}
		ICollection[] InternalLoadObjects(LoadDataResult loadResult, bool useStubs) {
			ICollection[] results = new ICollection[loadResult.Result.ResultSet.Length];
			for(int i = 0; i < loadResult.Result.ResultSet.Length; i++) {
				QueryData queryData = loadResult.Data[i];
				ObjectLoader loader = new ObjectLoader(this, queryData, loadResult.Queries[i].ClassInfo, loadResult.Queries[i].Force, useStubs);
				queryData.SetData(loadResult.Result.ResultSet[i]);
				List<object> objs = new List<object>(queryData.Count);
				while(queryData.MoveNext()) {
					objs.Add(loader.LoadObject());
				}
				results[i] = objs;
			}
			return results;
		}
		LoadObjectsContext PrepareLoadObjects(ObjectsQuery[] queries) {
			if(queries == null)
				throw new ArgumentNullException(nameof(queries));
			ICollection[] result;
			ExpandedCriteriaHolder[] expanded;
			List<ObjectsQuery> loadDataQuery = new List<ObjectsQuery>();
			bool[] requiresResorting;
			result = new ICollection[queries.Length];
			expanded = new ExpandedCriteriaHolder[queries.Length];
			requiresResorting = new bool[queries.Length];
			for(int i = 0; i < queries.Length; i++) {
				XPClassInfo loadClassInfo = queries[i].ClassInfo;
				expanded[i] = PersistentCriterionExpander.ExpandToLogical(Session, loadClassInfo, queries[i].Criteria, true);
				if(expanded[i].IsFalse || Session.UpdateSchema(true, loadClassInfo) == UpdateSchemaResult.FirstTableNotExists)
					result[i] = Array.Empty<object>();
				else {
					requiresResorting[i] = PrepareLoadObjectsProcessQuery(queries[i], expanded[i], loadDataQuery);
				}
			}
			if(loadDataQuery.Count == 0)
				return new LoadObjectsContext(queries, result, null, null);
			return new LoadObjectsContext(queries, result, expanded, requiresResorting, loadDataQuery.ToArray());
		}
		async Task<LoadObjectsContext> PrepareLoadObjectsAsync(ObjectsQuery[] queries, CancellationToken cancellationToken) {
			if(queries == null)
				throw new ArgumentNullException(nameof(queries));
			ICollection[] result;
			ExpandedCriteriaHolder[] expanded;
			List<ObjectsQuery> loadDataQuery = new List<ObjectsQuery>();
			bool[] requiresResorting;
			result = new ICollection[queries.Length];
			expanded = new ExpandedCriteriaHolder[queries.Length];
			requiresResorting = new bool[queries.Length];
			for(int i = 0; i < queries.Length; i++) {
				XPClassInfo loadClassInfo = queries[i].ClassInfo;
				expanded[i] = PersistentCriterionExpander.ExpandToLogical(Session, loadClassInfo, queries[i].Criteria, true);
				if(expanded[i].IsFalse || (await Session.UpdateSchemaAsync(cancellationToken, true, loadClassInfo)) == UpdateSchemaResult.FirstTableNotExists)
					result[i] = Array.Empty<object>();
				else {
					requiresResorting[i] = PrepareLoadObjectsProcessQuery(queries[i], expanded[i], loadDataQuery);
				}
			}
			if(loadDataQuery.Count == 0)
				return new LoadObjectsContext(queries, result, null, null);
			return new LoadObjectsContext(queries, result, expanded, requiresResorting, loadDataQuery.ToArray());
		}
		bool PrepareLoadObjectsProcessQuery(ObjectsQuery query, ExpandedCriteriaHolder expanded, List<ObjectsQuery> loadDataQuery) {
			bool requiresResorting = false;
			XPClassInfo loadClassInfo = query.ClassInfo;
			SortingCollection srt = new SortingCollection();
			if(query.Sorting != null) {
				foreach(SortProperty sp in query.Sorting) {
					ExpandedCriteriaHolder expandedSort = PersistentCriterionExpander.ExpandToValue(Session, loadClassInfo, sp.Property, true);
					if(expandedSort.RequiresPostProcessing) {
						requiresResorting = true;
						srt.Clear();
						break;
					}
					else {
						srt.Add(new SortProperty(ExpandedCriteriaHolder.IfNeededConvertToBoolOperator(expandedSort.ExpandedCriteria), sp.Direction));
					}
				}
			}
			int skipPhisicallySelected = query.SkipSelectedRecords;
			int topPhisicallySelected = query.TopSelectedRecords;
			if(topPhisicallySelected + skipPhisicallySelected != 0 && expanded.RequiresPostProcessing || requiresResorting) {
				topPhisicallySelected = 0;
				skipPhisicallySelected = 0;
			}
			loadDataQuery.Add(new ObjectsQuery(loadClassInfo, expanded.ExpandedCriteria,
				srt, skipPhisicallySelected, topPhisicallySelected, query.CollectionCriteriaPatcher, query.Force));
			return requiresResorting;
		}
		ICollection[] ProcessStubs(ICollection[] objs) {
			SessionStateStack.Enter(Session, SessionState.GetObjectsNonReenterant);
			try {
				foreach(StubMap map in stubObjects.Values) {
					foreach(LoadStub stub in map.Values) {
						ProcessStub(map, stub);
					}
				}
				ICollection[] result = new ICollection[objs.Length];
				for(int i = 0; i < objs.Length; i++) {
					List<object> list = new List<object>();
					foreach(object obj in objs[i]) {
						LoadStub stub = obj as LoadStub;
						if(stub != null)
							list.Add(stub.LoadedObject);
						else
							list.Add(obj);
					}
					result[i] = list;
				}
				return result;
			}
			finally {
				SessionStateStack.Leave(Session, SessionState.GetObjectsNonReenterant);
			}
		}
		object ProcessStub(StubMap map, LoadStub stub) {
			if(stub.IsProceed) {
				return stub.LoadedObject;
			}
			stub.Proceed();
			if(stub.LoadedObject == null) {
				IObjectMap objMap = SessionIdentityMap.GetMap(Session, stub.ClassInfo);
				stub.LoadedObject = objMap.Get(stub.Id);
				if(stub.LoadedObject == null) {
					stub.LoadedObject = stub.ClassInfo.CreateObject(Session);
					objMap.Add(stub.LoadedObject, stub.Id);
				}
				else {
					return stub.LoadedObject;
				}
			}
			if(!stub.IsNeedReload) {
				return stub.LoadedObject;
			}
			Session.TriggerObjectLoading(stub.LoadedObject);
			Dictionary<XPMemberInfo, object> membersDictionary = new Dictionary<XPMemberInfo, object>();
			foreach(XPMemberInfo mi in stub.Members) {
				object memberValue = stub.GetMemberValue(mi);
				LoadStub memberStub = memberValue as LoadStub;
				if(memberStub != null) {
					StubMap subMap = (StubMap)(memberStub.ClassInfo.Equals(stub.ClassInfo) ? map : CreateStubMap(memberStub.ClassInfo));
					memberValue = ProcessStub(subMap, memberStub);
				}
				membersDictionary.Add(mi, memberValue);
				if(mi.ValueParent != null) {
					object parentObjectValue;
					if(!membersDictionary.TryGetValue(mi.ValueParent, out parentObjectValue)) {
						parentObjectValue = new IdList();
						membersDictionary.Add(mi.ValueParent, parentObjectValue);
					}
					IdList parentValue = (IdList)parentObjectValue;
					ProcessAssociationStubData memberPASData = memberValue as ProcessAssociationStubData;
					if(memberPASData != null) {
						object newData = memberPASData.NewData;
						LoadStub newDataStub = newData as LoadStub;
						if(newDataStub != null) {
							StubMap subMap = (StubMap)(newDataStub.ClassInfo.Equals(stub.ClassInfo) ? map : CreateStubMap(newDataStub.ClassInfo));
							newData = ProcessStub(subMap, newDataStub);
						}
						memberValue = newData;
					}
					parentValue.Add(memberValue);
				}
			}
			foreach(XPMemberInfo mi in stub.Members) {
				if(mi.ValueParent != null) {
					if(!AcceptLoadPropertyAndResetModified(Session.TrackPropertiesModifications, stub.LoadMerge, stub.LoadedObject, stub.ClassInfo, mi.ValueParent, ReturnArgument, membersDictionary[mi.ValueParent]))
						continue;
				}
				object memberValue = membersDictionary[mi];
				ProcessAssociationStubData memberPASData = memberValue as ProcessAssociationStubData;
				if(memberPASData != null) {
					object newData = memberPASData.NewData;
					LoadStub newDataStub = newData as LoadStub;
					if(newDataStub != null) {
						StubMap subMap = (StubMap)(newDataStub.ClassInfo.Equals(stub.ClassInfo) ? map : CreateStubMap(newDataStub.ClassInfo));
						newData = ProcessStub(subMap, newDataStub);
					}
					if(mi.ValueParent == null && !AcceptLoadPropertyAndResetModified(Session.TrackPropertiesModifications, stub.LoadMerge, stub.LoadedObject, stub.ClassInfo, mi, ReturnArgument, newData))
						continue;
					mi.SetValue(stub.LoadedObject, newData);
					mi.ProcessAssociationRefChange(Session, stub.LoadedObject, memberPASData.OldData, newData, true);
				}
				else {
					if(mi.ValueParent == null && !AcceptLoadPropertyAndResetModified(Session.TrackPropertiesModifications, stub.LoadMerge, stub.LoadedObject, stub.ClassInfo, mi, ReturnArgument, memberValue))
						continue;
					if(stub.IsDelayedMember(mi)) {
						XPDelayedProperty.Init(Session, stub.LoadedObject, mi, memberValue);
						continue;
					}
					mi.SetValue(stub.LoadedObject, memberValue);
				}
			}
			if(stub.ClassInfo.OptimisticLockField != null)
				stub.ClassInfo.OptimisticLockFieldInDataLayer.SetValue(stub.LoadedObject, stub.ClassInfo.OptimisticLockField.GetValue(stub.LoadedObject));
			return stub.LoadedObject;
		}
		object ReturnArgument(object arg) {
			return arg;
		}
		ObjectSet ProcessFileLoaded(ObjectSet fileLoaded) {
			ObjectSet flNew = new ObjectSet(fileLoaded.Count);
			foreach(object obj in fileLoaded) {
				LoadStub stub = obj as LoadStub;
				if(stub != null) {
					flNew.Add(stub.LoadedObject);
				}
				else {
					flNew.Add(obj);
				}
			}
			return flNew;
		}
		ICollection[] EndLoadObjects(LoadObjectsContext loadObjectsContext, ICollection[] objs, bool proceesStubs) {
			if(proceesStubs) {
				objs = ProcessStubs(objs);
				Session.TriggerObjectsLoaded(ProcessFileLoaded(objectsToFireLoaded));
			}
			else {
				Session.TriggerObjectsLoaded(objectsToFireLoaded);
			}
			objectsToFireLoaded.Clear();
			ICollection[] result = loadObjectsContext.Result;
			int j = 0;
			for(int i = 0; i < loadObjectsContext.Queries.Length; i++) {
				if(result[i] != null)
					continue;
				ICollection rows = objs[j];
				++j;
				ObjectsQuery currentQuery = loadObjectsContext.Queries[i];
				if(!currentQuery.SkipDuplicateCheck) {
					ObjectSet uniqueObjects = new ObjectSet(rows.Count);
					foreach(object row in rows) {
						uniqueObjects.Add(row);
					}
					if(uniqueObjects.Count < rows.Count) throw new InvalidOperationException("The result collection contains duplicate objects.");
					uniqueObjects = null;
				}
				bool requiresFiltering = loadObjectsContext.Expanded[i].RequiresPostProcessing;
				bool requiresSorting = loadObjectsContext.RequiresResorting[i];
				if(requiresSorting && (currentQuery.SkipSelectedRecords != 0 || currentQuery.TopSelectedRecords != 0 && rows.Count > currentQuery.TopSelectedRecords)) {
					rows = ReSort(rows, currentQuery);
					requiresSorting = false;
					if(!requiresFiltering) {
						rows = GetSkipTopRows(rows, currentQuery);
					}
				}
				if(requiresFiltering) {
					rows = ReFilter(rows, currentQuery);
					requiresFiltering = false;
				}
				if(requiresSorting) {
					rows = ReSort(rows, currentQuery);
					requiresSorting = false;
				}
				result[i] = rows;
			}
			return result;
		}
		ICollection GetSkipTopRows(ICollection rows, ObjectsQuery currentQuery) {
			List<object> lst = new List<object>(currentQuery.TopSelectedRecords);
			int skipCounter = currentQuery.SkipSelectedRecords;
			foreach(object obj in rows) {
				if(skipCounter > 0) {
					skipCounter--;
					continue;
				}
				lst.Add(obj);
				if(lst.Count == currentQuery.TopSelectedRecords)
					break;
			}
			return lst;
		}
		ICollection ReSort(ICollection rows, ObjectsQuery currentQuery) {
			if(rows.Count <= 1)
				return rows;
			IComparer comparer = XPCollectionCompareHelper.CreateComparer(currentQuery.Sorting, currentQuery.ClassInfo.GetCriteriaCompilerDescriptor(Session), new CriteriaCompilerAuxSettings(Session.CaseSensitive, Session.Dictionary.CustomFunctionOperators));
			object[] sorted = ListHelper.FromCollection(rows).ToArray();
			Array.Sort(sorted, comparer);
			return sorted;
		}
		ICollection ReFilter(ICollection rows, ObjectsQuery currentQuery) {
			if(rows.Count == 0)
				return rows;
			Func<object, bool> isFit = XPBaseCollection.CreatePredicate(Session, currentQuery.ClassInfo, currentQuery.Criteria, Session.CaseSensitive);
			List<object> list = new List<object>();
			int skipCounter = currentQuery.SkipSelectedRecords;
			foreach(object obj in rows) {
				if(isFit(obj)) {
					if(skipCounter > 0) {
						skipCounter--;
						continue;
					}
					list.Add(obj);
					if(currentQuery.TopSelectedRecords == list.Count)
						break;
				}
			}
			return list;
		}
		class ObjectLoader {
			object theObject;
			readonly bool useStubs;
			readonly ObjectCollectionLoader loader;
			readonly QueryData data;
			readonly XPClassInfo classInfo;
			readonly XPMemberInfo optimistickLock;
			readonly XPMemberInfo optimistickLockInDataLayer;
			readonly DataMember[] members;
			readonly IObjectMap stubObjects;
			readonly IObjectMap classObjects;
			readonly QueryData.ValueAccessor keyAccessor;
			readonly QueryData.ValueAccessor typeAccessor;
			readonly bool forceReload;
			class DataMember {
				public QueryData.ValueAccessor Index;
				public XPMemberInfo Member;
				protected ObjectLoader loader;
				protected void SetValue(object value) {
					if(loader.useStubs) {
						((LoadStub)loader.theObject).SetMemberValue(Member, value);
					}
					else {
						Member.SetValue(loader.theObject, value);
					}
				}
				public virtual void AcquireValue() {
					SetValue(Index.Value);
				}
				protected DataMember(ObjectLoader loader, XPMemberInfo mi, QueryData.ValueAccessor index) {
					this.loader = loader;
					this.Member = mi;
					this.Index = index;
				}
				public static DataMember CreateDataMember(ObjectLoader loader, XPMemberInfo mi, QueryData.ValueAccessor index) {
					if(mi.IsDelayed)
						return new DelayedDataMember(loader, mi, index);
					if(mi.SubMembers.Count > 0)
						return new SubDataMember(loader, mi, index);
					if(mi.ReferenceType != null) {
						ObjectLoader subLoader = loader.GetSubLoader(mi, mi.ReferenceType);
						if(subLoader == null)
							return new RefDataMember(loader, mi, index);
						else
							return new RefLoadDataMember(loader, mi, subLoader, index);
					}
					return new DataMember(loader, mi, index);
				}
			}
			class RefDataMemberBase : DataMember {
				protected RefDataMemberBase(ObjectLoader loader, XPMemberInfo mi, QueryData.ValueAccessor index)
					: base(loader, mi, index) { }
				protected void SetRefValue(object newValue) {
					if(loader.useStubs) {
						LoadStub stub = loader.theObject as LoadStub;
						if(IsProcessingRefChanges()) {
							object oldValue = stub.GetMemberValue(Member);
							stub.SetMemberValue(Member, new ProcessAssociationStubData(oldValue, newValue));
						}
						else {
							stub.SetMemberValue(Member, newValue);
						}
					}
					else {
						if(IsProcessingRefChanges()) {
							object oldValue = Member.GetValue(loader.theObject);
							SetValue(newValue);
							Member.ProcessAssociationRefChange(loader.loader.Session, loader.theObject, oldValue, newValue, true);
						}
						else {
							SetValue(newValue);
						}
					}
				}
				bool? processRefChange;
				bool IsProcessingRefChanges() {
					if(!processRefChange.HasValue)
						processRefChange = Member.IsAssociation;
					return processRefChange.Value;
				}
			}
			sealed class RefDataMember : RefDataMemberBase {
				IObjectMap stubObjects;
				IObjectMap classObjects;
				public RefDataMember(ObjectLoader loader, XPMemberInfo mi, QueryData.ValueAccessor index)
					: base(loader, mi, index) {
					stubObjects = loader.loader.CreateStubMap(mi.ReferenceType);
					classObjects = SessionIdentityMap.GetMap(loader.loader.session, mi.ReferenceType);
				}
				public override void AcquireValue() {
					object val = Index.Value;
					if(val != null) {
						object obj;
						if(loader.useStubs) {
							obj = stubObjects.Get(val);
							if(obj == null) {
								obj = classObjects.Get(val);
								if(obj != null) {
									obj = new LoadStub(Member.ReferenceType, val, obj);
									stubObjects.Add(obj, val);
								}
							}
						}
						else
							obj = classObjects.Get(val);
						if(obj == null) {
							XPClassInfo type;
							QueryData.TypedRefAccessor refAcc = Index as QueryData.TypedRefAccessor;
							if(refAcc != null) {
								object typeId = refAcc.TypeId;
								if(typeId != null) {
									XPObjectType objType = loader.loader.Session.GetObjectType((Int32)typeId);
									type = objType.GetClassInfo();
								}
								else
									type = Member.ReferenceType;
							}
							else
								type = Member.ReferenceType;
							obj = loader.DelayLoading(loader.useStubs ? stubObjects : classObjects, type, val);
						}
						val = obj;
					}
					SetRefValue(val);
				}
			}
			sealed class RefLoadDataMember : RefDataMemberBase {
				ObjectLoader subLoader;
				public RefLoadDataMember(ObjectLoader loader, XPMemberInfo mi, ObjectLoader subLoader, QueryData.ValueAccessor index)
					: base(loader, mi, index) {
					this.subLoader = subLoader;
				}
				public override void AcquireValue() {
					SetRefValue(subLoader.LoadObject());
				}
			}
			sealed class SubDataMember : DataMember {
				List<DataMember> subMembers;
				public SubDataMember(ObjectLoader loader, XPMemberInfo mi, QueryData.ValueAccessor index)
					: base(loader, mi, index) {
					subMembers = new List<DataMember>();
					foreach(QueryData.ValueAccessor subAccessor in ((QueryData.SubValueAccessor)index).Nested)
						subMembers.Add(DataMember.CreateDataMember(loader, subAccessor.Member, subAccessor));
				}
				public override void AcquireValue() {
					foreach(DataMember dm in subMembers)
						dm.AcquireValue();
				}
			}
			sealed class DelayedDataMember : DataMember {
				public DelayedDataMember(ObjectLoader loader, XPMemberInfo mi, QueryData.ValueAccessor index) : base(loader, mi, index) { }
				public override void AcquireValue() {
					if(loader.useStubs) {
						((LoadStub)loader.theObject).SetMemberValue(Member, Index == null ? null : Index.Value, true);
						return;
					}
					XPDelayedProperty.Init(loader.loader.Session, loader.theObject, Member, Index == null ? null : Index.Value);
				}
			}
			public ObjectLoader(ObjectCollectionLoader loader, QueryData data, XPClassInfo classInfo, bool forceReload, bool useStubs) {
				this.useStubs = useStubs;
				this.forceReload = forceReload;
				this.loader = loader;
				this.data = data;
				this.classInfo = classInfo;
				this.stubObjects = loader.CreateStubMap(classInfo);
				this.classObjects = SessionIdentityMap.GetMap(loader.session, classInfo);
				classInfo.CheckAbstractReference();
				if(classInfo.IsTypedObject) {
					typeAccessor = data.GetAccessor(classInfo.GetMember(XPObjectType.ObjectTypePropertyName));
					if(typeAccessor == null)
						throw new TypeFieldIsEmptyException(classInfo.FullName);
				}
				optimistickLock = classInfo.OptimisticLockField;
				optimistickLockInDataLayer = classInfo.OptimisticLockFieldInDataLayer;
				keyAccessor = data.GetAccessor(classInfo.KeyProperty);
				List<DataMember> members = new List<DataMember>();
				foreach(XPMemberInfo pi in classInfo.PersistentProperties) {
					QueryData.ValueAccessor accessor = data.GetAccessor(pi);
					if((accessor != null || pi.IsDelayed) && (typeAccessor == null || accessor != typeAccessor))
						members.Add(DataMember.CreateDataMember(this, pi, accessor));
				}
				this.members = members.ToArray();
			}
			void InternalLoadObject(object id, OptimisticLockingReadMergeBehavior loadMerge) {
				loader.ProcessLoaded(classInfo, id);
				if(!useStubs) {
					loader.Session.TriggerObjectLoading(theObject);
					loader.objectsToFireLoaded.Add(theObject);
					LoadProperties(loadMerge);
				}
				else {
					var stubObject = (LoadStub)theObject;
					if(loadMerge != OptimisticLockingReadMergeBehavior.Default) {
						stubObject.LoadMerge = loadMerge;
					}
					stubObject.NeedReload();
					loader.objectsToFireLoaded.Add(theObject);
					LoadProperties(OptimisticLockingReadMergeBehavior.Default);
				}
			}
			bool IsObjectVersionChanged() {
				if(useStubs) {
					return optimistickLock != null &&
						(int?)OptimisticLockField.ConvertDbVersionToInt(data.GetValue(optimistickLock)) != (int?)((LoadStub)theObject).GetMemberValue(optimistickLock.Owner.OptimisticLockFieldInDataLayer);
				}
				else {
					return optimistickLock != null &&
						(int?)OptimisticLockField.ConvertDbVersionToInt(data.GetValue(optimistickLock)) != (int?)optimistickLock.Owner.OptimisticLockFieldInDataLayer.GetValue(theObject);
				}
			}
			public object LoadObject() {
				object id = keyAccessor.Value;
				if(id == null)
					return null;
				if(useStubs) {
					theObject = stubObjects.Get(id);
					if(theObject == null) {
						theObject = classObjects.Get(id);
						if(theObject != null) {
							theObject = new LoadStub(loader.Session.GetClassInfo(theObject), id, theObject);
							stubObjects.Add(theObject, id);
						}
					}
				}
				else
					theObject = classObjects.Get(id);
				OptimisticLockingReadMergeBehavior loadMergeState = OptimisticLockingReadMergeBehavior.Default;
				XPClassInfo resolvedType = ResolveClassInfo();
				if(theObject == null) {
					CreateTypedObject(id, resolvedType);
					ValidateTypedObjectType(resolvedType);
				}
				else {
					ValidateTypedObjectType(resolvedType);
					if(loader.HasObjectLoaded(theObject) || (!loader.IsDelayed(resolvedType, id) && !NeedReload(loader.Session, resolvedType, forceReload, out loadMergeState, IsObjectVersionChanged)))
						return theObject;
				}
				if(resolvedType != classInfo)
					loader.DelayLoading(resolvedType, id, theObject);
				else
					InternalLoadObject(id, loadMergeState);
				return theObject;
			}
			void ValidateTypedObjectType(XPClassInfo resolvedType) {
				XPClassInfo ci = useStubs ? ((LoadStub)theObject).ClassInfo : loader.Session.GetClassInfo(theObject);
				if(resolvedType != ci)
					throw new InvalidOperationException(Res.GetString(Res.Loader_InternalErrorOrUnsupportedReferenceStructure, ci, resolvedType));
			}
			void CreateTypedObject(object id, XPClassInfo classInfo) {
				if(useStubs) {
					theObject = new LoadStub(classInfo, id);
					loader.CreateStubMap(classInfo).Add(theObject, id);
				}
				else {
					theObject = classInfo.CreateObject(loader.Session);
					classObjects.Add(theObject, id);
				}
			}
			XPClassInfo ResolveClassInfo() {
				if(typeAccessor == null)
					return classInfo;
				object val = typeAccessor.Value;
				if(val == null)
					return classInfo;
				XPObjectType objType = loader.Session.GetObjectType((Int32)val);
				return objType.GetClassInfo();
			}
			static Func<object, object> ReturnMemberValue = arg => {
				DataMember member = arg as DataMember;
				return member == null || member.Index == null ? null : member.Index.Value;
			};
			void LoadProperties(OptimisticLockingReadMergeBehavior loadMerge) {
				foreach(var member in members) {
					if(!AcceptLoadPropertyAndResetModified(loader.Session.TrackPropertiesModifications, loadMerge, theObject, classInfo, member.Member, ReturnMemberValue, member))
						continue;
					member.AcquireValue();
				}
				if(optimistickLock != null && !useStubs)
					optimistickLockInDataLayer.SetValue(theObject, optimistickLock.GetValue(theObject));
			}
			ObjectLoader GetSubLoader(XPMemberInfo property, XPClassInfo objType) {
				QueryData nestedData = data.GetNestedData(property);
				if(nestedData == null)
					return null;
				return new ObjectLoader(loader, nestedData, objType, false, useStubs);
			}
			object DelayLoading(IObjectMap map, XPClassInfo classInfo, object id) {
				object obj;
				if(map is StubMap) {
					obj = new LoadStub(classInfo, id);
					loader.CreateStubMap(classInfo).Add(obj, id);
				}
				else {
					obj = classInfo.CreateObject(loader.Session);
					map.Add(obj, id);
				}
				loader.DelayLoading(classInfo, id, obj);
				return obj;
			}
		}
		public bool HasObjectLoaded(object theObject) {
			return objectsToFireLoaded.Contains(theObject);
		}
	}
}
