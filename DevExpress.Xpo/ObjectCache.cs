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
using System.Reflection;
using System.Runtime.InteropServices;
using System.Collections;
using System.Linq;
using DevExpress.Xpo;
using DevExpress.Xpo.Exceptions;
using DevExpress.Xpo.Metadata;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.Serialization;
using DevExpress.Data.Helpers;
using DevExpress.Utils;
namespace DevExpress.Xpo.Helpers {
	sealed class ObjectRecordsHashTable: LohPooled.OrdinaryDictionary<object, ObjectRecord> {
		sealed class ObjectAndObjectRecordsComparer: IEqualityComparer<object> {
			bool IEqualityComparer<object>.Equals(object x, object y) {
				if(x == y)
					return true;
				ObjectRecord recX = x as ObjectRecord;
				ObjectRecord recY = y as ObjectRecord;
				if(recX != null) {
					if(recY != null)
						return false;
					x = recX.Object;
				} else if(recY != null) {
					y = recY.Object;
				}
				return y == x;
			}
			int IEqualityComparer<object>.GetHashCode(object obj) {
				var bo = obj as IXPImmutableHashCode;
				if(bo != null)
					return bo.GetImmutableHashCode();
				else
					return obj.GetHashCode();
			}
			public static ObjectAndObjectRecordsComparer Instance = new ObjectAndObjectRecordsComparer();
		}
		public ObjectRecordsHashTable() : this(0) { }
		public ObjectRecordsHashTable(int size) : base(size, ObjectAndObjectRecordsComparer.Instance) { }
		public int BucketsLength { get { return ProtectedCurrentPrimeModulo; } }
	}
	public class WeakObjectIdentityMap : ObjectIdentityMap {
		sealed class WeakObjectMap : Dictionary<object, ObjectRecord>, IObjectMap {
			WeakObjectIdentityMap cache;
			public WeakObjectMap(WeakObjectIdentityMap cache) {
				this.cache = cache;
			}
			void IObjectMap.Add(object theObject, object id) {
				ObjectRecord or;
				if(!TryGetValue(id, out or)) {
					or = ObjectRecord.GetObjectRecord(theObject);
					base.Add(id, or);
					if(Environment.OSVersion.Platform == PlatformID.Win32NT) {
						PerformanceCounters.ObjectsInCache.Increment();
						PerformanceCounters.ObjectsInCacheAdded.Increment();
					}
					cache.count++;
				} else {
					if(or.IsAlive)
						throw new ObjectCacheException(id, theObject, or.Object);
					or = ObjectRecord.GetObjectRecord(theObject);
					this[id] = or;
				}
			}
			void IObjectMap.Remove(object id) {
				Remove(id);
			}
			public object Get(object id) {
				ObjectRecord wr;
				return TryGetValue(id, out wr) ? wr.Object : null;
			}
			public int CompactCache() {
				var keysToRemove = this.Where(pair => !pair.Value.IsAlive).Select(pair => pair.Key).ToList();
				foreach(object key in keysToRemove)
					Remove(key);
				return keysToRemove.Count;
			}
			public void ClearCache() {
				foreach(ObjectRecord rec in Values) {
					IXPInvalidateableObject spoilableObject = rec.Object as IXPInvalidateableObject;
					if(spoilableObject != null)
						spoilableObject.Invalidate();
				}
				Clear();
			}
		}
		protected override IObjectMap CreateMap() {
			return new WeakObjectMap(this);
		}
		int count;
		public WeakObjectIdentityMap(IObjectLayerProvider objectLayerProvider)
			: base(objectLayerProvider) {
		}
		void CompactCache() {
			int count = 0;
			foreach(IObjectMap ent in classes.Values)
				count += ent.CompactCache();
			this.count -= count;
			if(Environment.OSVersion.Platform == PlatformID.Win32NT) {
				PerformanceCounters.ObjectsInCache.Decrement(count);
				PerformanceCounters.ObjectsInCacheRemoved.Increment(count);
			}
		}
		int _lastCompactGC;
		public override void Compact() {
			int prevCompactGC = _lastCompactGC;
			int currentGC = GC.CollectionCount(GC.MaxGeneration);
			if(prevCompactGC == currentGC)
				return;
			_lastCompactGC = currentGC;
			if(prevCompactGC != 0) {
				CompactCache();
			}
			ObjectRecord.MayBeCompact();
		}
		public override void Clear() {
			foreach(IObjectMap ent in classes.Values) {
				ent.ClearCache();
			}
			classes.Clear();
			if(Environment.OSVersion.Platform == PlatformID.Win32NT) {
				PerformanceCounters.ObjectsInCache.Decrement(count);
				PerformanceCounters.ObjectsInCacheRemoved.Increment(count);
			}
			count = 0;
			ObjectRecord.MayBeCompact();
		}
		#region IDisposable Members
		~WeakObjectIdentityMap() {
			if(Environment.OSVersion.Platform == PlatformID.Win32NT) {
				PerformanceCounters.ObjectsInCache.Decrement(count);
				PerformanceCounters.ObjectsInCacheRemoved.Increment(count);
			}
		}
		#endregion
	}
	public interface IObjectMap {
		void Add(object theObject, object id);
		object Get(object id);
		void Remove(object id);
		int CompactCache();
		void ClearCache();
	}
	public class StrongObjectIdentityMap : ObjectIdentityMap {
		sealed class StrongObjectMap : Dictionary<object, object>, IObjectMap {
			void IObjectMap.Add(object theObject, object id) {
				try {
					base.Add(id, theObject);
				} catch(ArgumentException) {
					throw new ObjectCacheException(id, theObject, this[id]);
				}
			}
			void IObjectMap.Remove(object id) {
				Remove(id);
			}
			public object Get(object id) {
				object res;
				return TryGetValue(id, out res) ? res : null;
			}
			public int CompactCache() {
				return 0;
			}
			public void ClearCache() {
				foreach(object obj in Values) {
					IXPInvalidateableObject spoilableObject = obj as IXPInvalidateableObject;
					if(spoilableObject != null)
						spoilableObject.Invalidate();
				}
				Clear();
			}
		}
		public StrongObjectIdentityMap(IObjectLayerProvider objectLayerProvider)
			: base(objectLayerProvider) {
		}
		protected override IObjectMap CreateMap() {
			return new StrongObjectMap();
		}
		public override void Clear() {
			foreach(IObjectMap list in classes.Values) {
				list.ClearCache();
			}
			classes.Clear();
		}
	}
	public abstract class ObjectIdentityMap {
		readonly IObjectLayer objectLayer;
		protected XPDictionary Dictionary { get { return ObjectLayer.Dictionary; } }
		protected IObjectLayer ObjectLayer { get { return objectLayer; } }
		public ObjectIdentityMap(IObjectLayerProvider objectLayerProvider) {
			this.objectLayer = objectLayerProvider.ObjectLayer;
		}
		protected Dictionary<XPClassInfo, IObjectMap> classes = new Dictionary<XPClassInfo, IObjectMap>();
		protected abstract IObjectMap CreateMap();
		public IObjectMap GetObjects(XPClassInfo classInfo) {
			IObjectMap objects;
			if(!classes.TryGetValue(classInfo.IdClass, out objects)) {
				objects = ObjectLayer.IsStaticType(classInfo) ? ObjectLayer.GetStaticCache(classInfo) : CreateMap();
				classes[classInfo.IdClass] = objects;
			}
			return objects;
		}
		public object Get(IObjectMap objects, object id) {
			return objects.Get(id);
		}
		public object Get(XPClassInfo objectClass, object id) {
			if(id == null)
				return null;
			return Get(GetObjects(objectClass), id);
		}
		public void Add(IObjectMap objects, object theObject, object id) {
			objects.Add(theObject, id);
		}
		public void Add(object theObject, object id) {
			Add(GetObjects(Dictionary.GetClassInfo(theObject)), theObject, id);
		}
		public void Remove(object theObject) {
			if(theObject == null)
				return;
			XPClassInfo ci = Dictionary.GetClassInfo(theObject);
			IObjectMap objects = GetObjects(ci);
			objects.Remove(ci.GetId(theObject));
		}
		public abstract void Clear();
		public virtual void Compact() {
		}
	}
	[CollectionDataContract]
	public sealed class IdList : List<object>, IComparable {
		public IdList() : base() { }
		public IdList(ICollection list) : base(ListHelper.FromCollection(list)) { }
		public override bool Equals(object anotherObject) {
			IdList another = anotherObject as IdList;
			if(another == null)
				return false;
			if(Count != another.Count)
				return false;
			for(int i = 0; i < Count; i++) {
				if(this[i] == null || !this[i].Equals(another[i]))
					return false;
			}
			return true;
		}
		public override int GetHashCode() {
			return HashCodeHelper.CalculateGenericList(this);
		}
		int IComparable.CompareTo(object anotherObject) {
			IdList another = anotherObject as IdList;
			if(another == null)
				throw new ArgumentNullException(nameof(anotherObject));
			int res = 0;
			for(int i = 0; i < Count; i++) {
				res = DefaultComparer.Compare(this[i], another[i]);
				if(res != 0)
					break;
			}
			return res;
		}
	}
	public interface IObjectChange {
		void OnObjectChanged(object sender, ObjectChangeEventArgs args);
	}
	[System.Security.SecuritySafeCritical]
	public class NakedObjectRecord : ObjectRecord, IXPCustomPropertyStore, IXPModificationsStore {
		Dictionary<XPMemberInfo, object> customPropertyStore;
		Dictionary<XPMemberInfo, object> CustomPropertyStore {
			get {
				if(customPropertyStore == null)
					customPropertyStore = new Dictionary<XPMemberInfo, object>(3);
				return customPropertyStore;
			}
		}
		public NakedObjectRecord(object theObject) : base(theObject) {
		}
		public bool HasCustomPropertyStore {
			get { return customPropertyStore != null; }
		}
		object IXPCustomPropertyStore.GetCustomPropertyValue(XPMemberInfo property) {
			object res;
			return !HasCustomPropertyStore ? null : (CustomPropertyStore.TryGetValue(property, out res) ? res : null);
		}
		bool IXPCustomPropertyStore.SetCustomPropertyValue(XPMemberInfo property, object theValue) {
			if(HasCustomPropertyStore || theValue != null) {
				object oldValue = ((IXPCustomPropertyStore)this).GetCustomPropertyValue(property);
				if(PersistentBase.CanSkipAssignment(oldValue, theValue))
					return false;
				CustomPropertyStore[property] = theValue;
				return true;
			}
			return false;
		}
		#region IXPModificationsStore Members
		Dictionary<XPMemberInfo, object> modificationsStore;
		internal Dictionary<XPMemberInfo, object> ModificationsStore {
			get {
				if (modificationsStore == null)
					modificationsStore = new Dictionary<XPMemberInfo, object>();
				return modificationsStore;
			}
		}
		internal bool HasModificationStore { get { return modificationsStore != null; } }
		void IXPModificationsStore.ClearModifications() {
			if (HasModificationStore) ModificationsStore.Clear();
		}
		void IXPModificationsStore.SetPropertyModified(XPMemberInfo property, object oldValue) {
			if (ModificationsStore.ContainsKey(property)) return;
			ModificationsStore.Add(property, oldValue);
		}
		bool IXPModificationsStore.GetPropertyModified(XPMemberInfo property) {
			return HasModificationStore && ModificationsStore.ContainsKey(property);
		}
		object IXPModificationsStore.GetPropertyOldValue(XPMemberInfo property) {
			object result;
			if (HasModificationStore && ModificationsStore.TryGetValue(property, out result)) return result;
			return null;
		}
		void IXPModificationsStore.ResetPropertyModified(XPMemberInfo property) {
			if (HasModificationStore)
				ModificationsStore.Remove(property);
		}
		bool IXPModificationsStore.HasModifications() {
			if(!HasModificationStore) return false;
			return ModificationsStore.Count > 0;
		}
		#endregion
	}
	[System.Security.SecuritySafeCritical]
	public class ObjectRecord {
		readonly int hash;
		GCHandle handle;
		static GCHandle zeroHandle;
		ChangeHandler changeHandler;
		static ObjectRecordsHashTable nonSavedObjects = new ObjectRecordsHashTable();
		public override int GetHashCode() {
			return hash;
		}
		static ObjectRecord() {
			GCHandle h = GCHandle.Alloc(new Object());
			h.Free();
			zeroHandle = h;
		}
		protected ObjectRecord(object theObject) {
			var immutableHash = theObject as IXPImmutableHashCode;
			if(immutableHash != null)
				this.hash = immutableHash.GetImmutableHashCode();
			else
				this.hash = theObject.GetHashCode();
			handle = GCHandle.Alloc(theObject, GCHandleType.Weak);
		}
		public void Dispose() {
			Dispose(true);
		}
		void Dispose(bool disposing) {
			GCHandle h = handle;
			handle = zeroHandle;
			if(h.IsAllocated)
				h.Free();
			if(disposing)
				GC.SuppressFinalize(this);
		}
		~ObjectRecord() {
			Dispose(false);
		}
		static bool FindObjectRecord(object theObject, out ObjectRecord rec) {
			return nonSavedObjects.TryGetValue(theObject, out rec);
		}
		public bool IsAlive {
			get {
				return Object != null;
			}
		}
		public object Object {
			get {
				GCHandle h = handle;
				if(!h.IsAllocated)
					return null;
				object res = h.Target;
				return handle.IsAllocated ? res : null;
			}
		}
		static IEnumerator<ObjectRecord> FilterDead(IEnumerable<ObjectRecord> whole) {
			foreach(var rec in whole) {
				object target = rec.Object;
				if(target != null)
					yield return rec;
				else
					rec.Dispose();
			}
		}
		static void CompactBeforeOverflow() {
			var oldCount = nonSavedObjects.Count;
			if(oldCount < 2048 || oldCount < nonSavedObjects.BucketsLength)
				return;
			var records = LohPooled.ToListAndDisposeToOnceEnumerableCollection(FilterDead(nonSavedObjects.Values));
			int newCapacity;
			try {
				newCapacity = (records.Count > oldCount / 2) ? oldCount * 2 : oldCount;
			} catch(OverflowException) {
				newCapacity = oldCount;
			}
			nonSavedObjects.Dispose();
			nonSavedObjects = new ObjectRecordsHashTable(newCapacity);
			foreach(ObjectRecord rec in records) {
				nonSavedObjects.Add(rec, rec);
			}
		}
		public static void Compact() {
			lock(lockObject) {
				var oldCount = nonSavedObjects.Count;
				if(oldCount == 0)
					return;
				var records = LohPooled.ToListAndDisposeToOnceEnumerableCollection(FilterDead(nonSavedObjects.Values));
				int newCapacity;
				try {
					newCapacity = checked(records.Count * 2 + 128);
				} catch(OverflowException) {
					newCapacity = oldCount;
				}
				nonSavedObjects.Dispose();
				nonSavedObjects = new ObjectRecordsHashTable(newCapacity);
				foreach(ObjectRecord rec in records) {
					nonSavedObjects.Add(rec, rec);
				}
			}
		}
		static int _lastCollection;
		public static int CompactPeriodicity = 8;
		public static void MayBeCompact() {
			int prevGC = _lastCollection;
			int currentGC = GC.CollectionCount(GC.MaxGeneration);
			if(currentGC >= prevGC && currentGC - prevGC < CompactPeriodicity)
				return;
			bool otherThreadWon = prevGC != System.Threading.Interlocked.CompareExchange(ref _lastCollection, currentGC, prevGC);
			if(otherThreadWon)
				return;
			Compact();
		}
		static readonly object lockObject = new object();
		static ObjectRecord InternalCreateObjectRecord(object theObject) {
			CompactBeforeOverflow();
			ObjectRecord rec = theObject is IXPCustomPropertyStore && theObject is IXPModificationsStore ? new ObjectRecord(theObject) : new NakedObjectRecord(theObject);
			nonSavedObjects.Add(rec, rec);
			return rec;
		}
		public static ObjectRecord GetObjectRecord(object theObject) {
			lock(lockObject) {
				ObjectRecord rec;
				return FindObjectRecord(theObject, out rec) ? rec : InternalCreateObjectRecord(theObject);
			}
		}
		static ObjectRecord GetChangeHandler(ObjectChangeEventHandler handler) {
			ObjectRecord target = GetObjectRecord(handler.Target);
			return target;
		}
		public static void AddChangeHandler(object theObject, ObjectRecord target) {
			AddChangeHandler(GetObjectRecord(theObject), target);
		}
		public static void AddChangeHandler(ObjectRecord record, ObjectRecord target) {
			record.changeHandler = new InterfacedChangeHandler(target, record.changeHandler);
		}
		public static void AddChangeHandler(object theObject, ObjectChangeEventHandler handler) {
			object target = handler.Target;
			if(target == null) {
				ObjectRecord record = GetObjectRecord(theObject);
				record.changeHandler = new StaticChangeHandler(handler, record.changeHandler);
			} else {
				ObjectRecord record = GetChangeHandler(handler);
				ObjectRecord rec = GetObjectRecord(theObject);
				rec.changeHandler = new ObjectRecordChangeHandler(record, handler, rec.changeHandler);
			}
		}
		public static void RemoveChangeHandler(ObjectRecord record, ObjectRecord target) {
			ChangeHandler handler = record.changeHandler;
			ChangeHandler prev = null;
			while(handler != null) {
				InterfacedChangeHandler rec = handler as InterfacedChangeHandler;
				if(rec != null && Object.ReferenceEquals(rec.target, target)) {
					if(prev == null)
						record.changeHandler = handler.next;
					else
						prev.next = handler.next;
					break;
				}
				prev = handler;
				handler = handler.next;
			}
		}
		static void RemoveChangeHandler(ObjectRecord record, ObjectRecord target, ObjectChangeEventHandler handler_) {
			ChangeHandler handler = record.changeHandler;
			ChangeHandler prev = null;
			MethodInfo method = handler_.Method;
			while (handler != null) {
				ObjectRecordChangeHandler rec = handler as ObjectRecordChangeHandler;
				if(rec != null && Object.ReferenceEquals(rec.target, target) && Object.ReferenceEquals(method, rec.method)) {
					if(prev == null)
						record.changeHandler = handler.next;
					else
						prev.next = handler.next;
					break;
				}
				prev = handler;
				handler = handler.next;
			}
		}
		public static void RemoveChangeHandler(object theObject, ObjectRecord target) {
			lock(lockObject) {
				ObjectRecord rec;
				if(FindObjectRecord(theObject, out rec))
					RemoveChangeHandler(rec, target);
			}
		}
		public static void RemoveChangeHandler(object theObject, ObjectChangeEventHandler handler) {
			lock(lockObject) {
				object targetObject = handler.Target;
				if(targetObject != null) {
					ObjectRecord target;
					ObjectRecord rec;
					if(FindObjectRecord(targetObject, out target) && FindObjectRecord(theObject, out rec))
						RemoveChangeHandler(rec, target, handler);
				} else {
					ObjectRecord record;
					if(FindObjectRecord(theObject, out record))
						RemoveChangeHandler(record, handler);
				}
			}
		}
		static void RemoveChangeHandler(ObjectRecord record, ObjectChangeEventHandler handler) {
			ChangeHandler current = record.changeHandler;
			ChangeHandler prev = null;
			while(handler != null) {
				StaticChangeHandler rec = current as StaticChangeHandler;
				if(rec != null && Object.Equals(rec.handler, handler)) {
					if(prev == null)
						record.changeHandler = current.next;
					else
						prev.next = current.next;
					break;
				}
				prev = current;
				current = current.next;
			}
		}
		abstract class ChangeHandler {
			public ChangeHandler next;
			public ChangeHandler(ChangeHandler next) {
				this.next = next;
			}
			public abstract void Invoke(object sender, ref object[] parameters, ObjectChangeEventArgs args);
		}
		class ObjectRecordChangeHandler : ChangeHandler {
			public ObjectRecord target;
			public MethodInfo method;
			public ObjectRecordChangeHandler(ObjectRecord target, ObjectChangeEventHandler handler, ChangeHandler next)
				: base(next) {
				this.target = target;
				method = handler.Method;
			}
			public override void Invoke(object sender, ref object[] parameters, ObjectChangeEventArgs args) {
				object obj = target.Object;
				if(obj != null) {
					if(obj is IObjectChange)
						((IObjectChange)obj).OnObjectChanged(sender, args);
					else {
						if(parameters == null)
							parameters = new object[] { sender, args };
						method.Invoke(obj, parameters);
					}
				}
			}
		}
		class InterfacedChangeHandler : ChangeHandler {
			public ObjectRecord target;
			public InterfacedChangeHandler(ObjectRecord target, ChangeHandler next)
				: base(next) {
				this.target = target;
			}
			public override void Invoke(object sender, ref object[] parameters, ObjectChangeEventArgs args) {
				IObjectChange obj = target.Object as IObjectChange;
				if(obj != null) {
					obj.OnObjectChanged(sender, args);
				}
			}
		}
		class StaticChangeHandler : ChangeHandler {
			public ObjectChangeEventHandler handler;
			public StaticChangeHandler(ObjectChangeEventHandler handler, ChangeHandler next)
				: base(next) {
				this.handler = handler;
			}
			public override void Invoke(object sender, ref object[] parameters, ObjectChangeEventArgs args) {
				handler(sender, args);
			}
		}
		void OnChange(object sender, ObjectChangeEventArgs args) {
			object[] param = null;
			ChangeHandler handler = changeHandler;
			while(handler != null) {
				handler.Invoke(sender, ref param, args);
				handler = handler.next;
			}
		}
		public static void OnChange(object sender, object theObject, ObjectChangeEventArgs args) {
			GetObjectRecord(theObject).OnChange(sender, args);
		}
	}
}
