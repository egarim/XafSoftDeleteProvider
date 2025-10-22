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
using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using DevExpress.Xpo.Metadata;
using DevExpress.Xpo.Metadata.Helpers;
using DevExpress.Xpo.Generators;
using System.Collections.Specialized;
using System.ComponentModel;
using DevExpress.Xpo.Exceptions;
using System.Threading;
using System.Threading.Tasks;
namespace DevExpress.Xpo {
	[System.ComponentModel.Browsable(false), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
	public interface IList2<T>: IList<T>, IList { }
}
namespace DevExpress.Xpo.Helpers {
	public interface IXPAssociationList : IXPClassInfoAndSessionProvider, IXPPrefetchableAssociationList, IXPBulkLoadableCollection, IXPUnloadableAssociationList {
	}
	public interface IXPPrefetchableAssociationList : IList {
		bool NeedPrefetch();
		void FinishPrefetch(ICollection hint);
	}
	public interface IXPUnloadableAssociationList : IList {
		bool IsLoaded { get;}
		void Unload();
	}
	public interface IXPBulkLoadableCollection : IList {
		ObjectsQuery BeginLoad();
		void EndLoad(IEnumerable queriedObjects);
	}
	public class XPAssociationList : IXPAssociationList, INotifyCollectionChanged {
		protected readonly object OwnerObject;
		protected readonly XPMemberInfo Member;
		readonly Session _Session;
		public XPAssociationList(Session session, object owner, XPMemberInfo collectionMember) {
			this._Session = session;
			this.OwnerObject = owner;
			this.Member = collectionMember;
		}
		ObjectList _InnerList;
		protected IList InnerList {
			get {
				Load();
				return _InnerList;
			}
		}
		public void Load() {
			if(IsLoaded)
				return;
			ICollection lst = Session.GetObjectsInTransaction(ClassInfo, GetFillCriteria(), false);
			FinishPrefetch(lst);
		}
		public async Task LoadAsync(CancellationToken cancellationToken = default(CancellationToken)) {
			if(IsLoaded)
				return;
			ICollection lst = await Session.GetObjectsInTransactionAsync(ClassInfo, GetFillCriteria(), false, cancellationToken);
			FinishPrefetch(lst);
		}
		public bool IsLoaded {
			get {
				return _InnerList != null;
			}
		}
		public void Unload() {
			if(!IsLoaded)
				return;
			_InnerList.Clear(); 
			_InnerList = null;
			OnUnload();
		}
		public int Add(object value) {
			if(value == null)
				return -1;
			Session.ThrowIfObjectFromDifferentSession(value);
			if(!Session.GetClassInfo(value).IsAssignableTo(ClassInfo))
				throw new InvalidCastException(Res.GetString(Res.Collections_InvalidCastOnAdd, Session.GetClassInfo(value), ClassInfo));
			if(!IsLoaded) {
				Member.GetAssociatedMember().SetValue(value, OwnerObject);
				return -1; 
			}
			int index = IndexOf(value);
			if(index < 0) {
				index = InnerList.Add(value);
				Member.GetAssociatedMember().SetValue(value, OwnerObject);
				OnAdd(index, value);
			}
			return index;
		}
		public void Clear() {
			while(this.Count > 0) {
				this.Remove(Indexer(this.Count - 1));
			}
		}
		public bool Contains(object value) {
			return IndexOf(value) >= 0;
		}
		public int IndexOf(object value) {
			return InnerList.IndexOf(value);
		}
		public void Insert(int index, object value) {
			Add(value);	
		}
		public bool IsFixedSize {
			get { return false; }
		}
		public bool IsReadOnly {
			get { return false; }
		}
		static bool IsKeyOrInKey(XPMemberInfo mi) {
			for(XPMemberInfo current = mi; current != null; current = current.ValueParent) {
				if(current.IsKey)
					return true;
			}
			return false;
		}
		static bool? _DoNotSetAssociatedMemberToNullWhenRemovingDeletedObjectFromAssociationList;
		public static bool? DoNotSetAssociatedMemberToNullWhenRemovingDeletedObjectFromAssociationList {
			get { return _DoNotSetAssociatedMemberToNullWhenRemovingDeletedObjectFromAssociationList; }
			set { _DoNotSetAssociatedMemberToNullWhenRemovingDeletedObjectFromAssociationList = value; }
		}
		internal static bool CanNullifyAssociatedMember(Session session, XPMemberInfo associatedMi, object objectRemoved) {
			if(IsKeyOrInKey(associatedMi))
				return false;
			if(_DoNotSetAssociatedMemberToNullWhenRemovingDeletedObjectFromAssociationList == true) {
				if(session.IsObjectMarkedDeleted(objectRemoved))
					return false;
			}
			return true;
		}
		public void Remove(object value) {
			if(value == null)
				return;
			if(IsLoaded) {
				int indexOf = this.IndexOf(value);
				if(indexOf >= 0)
					RemoveAt(indexOf);
			} else {
				PatchObjectOnRemove(value);
			}
		}
		void PatchObjectOnRemove(object value) {
			XPMemberInfo associatedMember = Member.GetAssociatedMember();
			if(associatedMember.GetValue(value) == OwnerObject && CanNullifyAssociatedMember(Session, associatedMember, value)) {
				associatedMember.SetValue(value, null);
			}
		}
		public void RemoveAt(int index) {
			object obj = Indexer(index);
			InnerList.RemoveAt(index);
			PatchObjectOnRemove(obj);
			OnRemove(index, obj);
		}
		public object Indexer(int index) {
			return InnerList[index];
		}
		object IList.this[int index] {
			get {
				return Indexer(index);
			}
			set {
				throw new NotSupportedException();
			}
		}
		public void CopyTo(Array array, int index) {
			InnerList.CopyTo(array, index);
		}
		public int Count {
			get { return InnerList.Count; }
		}
		public bool IsSynchronized {
			get { return false; }
		}
		public object SyncRoot {
			get { return this; }
		}
		public IEnumerator GetEnumerator() {
			foreach(object item in InnerList)
				yield return item;
		}
		public XPClassInfo ClassInfo {
			get { return Member.CollectionElementType; }
		}
		public Session Session {
			get { return _Session; }
		}
		XPDictionary IXPDictionaryProvider.Dictionary {
			get { return ClassInfo.Dictionary; }
		}
		IObjectLayer IObjectLayerProvider.ObjectLayer {
			get { return Session.ObjectLayer; }
		}
		IDataLayer IDataLayerProvider.DataLayer {
			get { return Session.DataLayer; }
		}
		public bool NeedPrefetch() {
			return !IsLoaded && !Session.IsNewObject(OwnerObject);
		}
		public void FinishPrefetch(ICollection queriedObjects) {
			System.Diagnostics.Debug.Assert(_InnerList == null);
			_InnerList = new ObjectList(queriedObjects);
		}
		public ObjectsQuery BeginLoad() {
			if(Session.IsNewObject(OwnerObject))
				Load();
			if(IsLoaded)
				return null;
			return new ObjectsQuery(ClassInfo, GetFillCriteria(), null, 0, 0, new CollectionCriteriaPatcher(false, Session.TypesManager), false);
		}
		CriteriaOperator GetFillCriteria() {
			return new OperandProperty(Member.GetAssociatedMember().Name) == new OperandValue(OwnerObject);
		}
		public void EndLoad(IEnumerable queriedObjects) {
			ObjectSet transactedResults = new ObjectSet();
			XPMemberInfo gcMember = ClassInfo.FindMember(GCRecordField.StaticName);
			foreach(object obj in queriedObjects) {
				if(Session.IsObjectToDelete(obj, true))
					continue;
				if(gcMember != null && gcMember.GetValue(obj) != null)
					continue;
				object parent = Member.GetAssociatedMember().GetValue(obj);
				if(parent != OwnerObject)
					continue;
				transactedResults.Add(obj);
			}
			foreach(object obj in Session.GetObjectsToSave(true)) {
				if(Session.IsObjectToDelete(obj, true))
					continue;
				if(!Session.GetClassInfo(obj).IsAssignableTo(ClassInfo))
					continue;
				if(gcMember != null && gcMember.GetValue(obj) != null)
					continue;
				object parent = Member.GetAssociatedMember().GetValue(obj);
				if(parent != OwnerObject)
					continue;
				transactedResults.Add(obj);
			}
			FinishPrefetch(transactedResults);
		}
		[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
		public CriteriaOperator GetRealFetchCriteria() {
			return GetFillCriteria();
		}
		static int seq = 0;
		int seqNum = Interlocked.Increment(ref seq);
		public override string ToString() {
			string state;
			if(this.IsLoaded)
				state = "Count(" + this.Count.ToString() + ')';
			else
				state = "NotLoaded";
			return string.Format("{0}({1}) {2} @ {3}->{4}", base.ToString(), seqNum, state, OwnerObject, Member.Name);
		}
		protected virtual void OnAdd(int index, object value) {
			if(CollectionChanged != null)
				CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, value, index));
		}
		protected virtual void OnRemove(int index, object obj) {
			if(CollectionChanged != null)
				CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, obj, index));
		}
		protected virtual void OnUnload() {
			if(CollectionChanged != null)
				CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}
		public event NotifyCollectionChangedEventHandler CollectionChanged;
	}
	public class XPManyToManyAliasList : IList, IXPClassInfoAndSessionProvider, IXPUnloadableAssociationList, INotifyCollectionChanged {
		readonly Session _Session;
		public readonly IList AliasedList;
		public readonly XPMemberInfo SkippedReference;
		protected virtual object CreateIntermediateObject() {
			return SkippedReference.Owner.CreateNewObject(Session);
		}
		public XPManyToManyAliasList(Session session, IList aliasedCollection, XPMemberInfo skippedProperty) {
			this._Session = session;
			this.AliasedList = aliasedCollection;
			INotifyCollectionChanged aliasedNotifiableCollection = aliasedCollection as INotifyCollectionChanged;
			if(aliasedNotifiableCollection != null) {
				aliasedNotifiableCollection.CollectionChanged += AliasedListCollectionChanged;
			}
			this.SkippedReference = skippedProperty;
		}
		public int Add(object value) {
			if(value == null)
				return -1;
			if(!Session.GetClassInfo(value).IsAssignableTo(ClassInfo))
				throw new InvalidCastException(Res.GetString(Res.Collections_InvalidCastOnAdd, Session.GetClassInfo(value), ClassInfo));
			int index = this.IndexOf(value);
			if(index >= 0)
				return index;
			object newIntermediate = CreateIntermediateObject();
			SkippedReference.SetValue(newIntermediate, value);
			return AliasedList.Add(newIntermediate);
		}
		public void Clear() {
			Session.Delete(AliasedList);
		}
		public bool Contains(object value) {
			return IndexOf(value) >= 0;
		}
		public int IndexOf(object value) {
			for(int i = 0; i < Count; ++i) {
				if(Indexer(i) == value)
					return i;
			}
			return -1;
		}
		public void Insert(int index, object value) {
			Add(value);	
		}
		public bool IsFixedSize {
			get { return AliasedList.IsFixedSize; }
		}
		public bool IsReadOnly {
			get { return AliasedList.IsReadOnly; }
		}
		public void Remove(object value) {
			int index = IndexOf(value);
			if(index < 0)
				return;
			RemoveAt(index);
		}
		public void RemoveAt(int index) {
			Session.Delete(AliasedList[index]);
		}
		public object Indexer(int index) {
			return SkippedReference.GetValue(AliasedList[index]);
		}
		object IList.this[int index] {
			get {
				return Indexer(index);
			}
			set { throw new NotSupportedException(); }
		}
		public void CopyTo(Array array, int index) {
			for(int i = 0; i < Count; ++i) {
				array.SetValue(Indexer(i), i + index);
			}
		}
		public int Count {
			get { return AliasedList.Count; }
		}
		public bool IsSynchronized {
			get { return AliasedList.IsSynchronized; }
		}
		public object SyncRoot {
			get { return AliasedList.SyncRoot; }
		}
		public IEnumerator GetEnumerator() {
			foreach(object obj in AliasedList) {
				yield return SkippedReference.GetValue(obj);
			}
		}
		public async Task<IList> EnumerateAsync(CancellationToken cancellationToken = default(CancellationToken)) {
			var assocList = AliasedList as XPAssociationList;
			if(assocList != null) {
				await assocList.LoadAsync(cancellationToken);
			}
			int count = AliasedList.Count;
			object[] result = new object[count];
			for(int i = 0; i < count; i++) {
				result[i] = SkippedReference.GetValue(AliasedList[i]);
			}
			return result;
		}
		public XPClassInfo ClassInfo {
			get { return SkippedReference.ReferenceType; }
		}
		public XPDictionary Dictionary {
			get { return ClassInfo.Dictionary; }
		}
		public Session Session {
			get { return _Session; }
		}
		public IObjectLayer ObjectLayer {
			get { return Session.ObjectLayer; }
		}
		public IDataLayer DataLayer {
			get { return Session.DataLayer; }
		}
		void IXPUnloadableAssociationList.Unload() {
			((IXPUnloadableAssociationList)AliasedList).Unload();
		}
		bool IXPUnloadableAssociationList.IsLoaded {
			get {
				return ((IXPUnloadableAssociationList)AliasedList).IsLoaded;
			}
		}
		void AliasedListCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
			OnCollectionChanged(e);
		}
		IList ConvertList(IList objects) {
			if(objects == null) return null;
			List<object> resultList = new List<object>(objects.Count);
			foreach(object o in objects){
				if(o == null) resultList.Add(null);
				else resultList.Add(SkippedReference.GetValue(o));
			}
			return resultList;
		}
		[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
		public CriteriaOperator GetRealFetchCriteria(object owner, XPMemberInfo manyToManyAliasProperty) {
			if(!manyToManyAliasProperty.IsManyToManyAlias) {
				throw new ArgumentException(null, nameof(manyToManyAliasProperty));
			}
			ManyToManyAliasAttribute mma = (ManyToManyAliasAttribute)manyToManyAliasProperty.GetAttributeInfo(typeof(ManyToManyAliasAttribute));
			XPMemberInfo thisSideOneToManyCollection = manyToManyAliasProperty.Owner.GetMember(mma.OneToManyCollectionName);
			if(!thisSideOneToManyCollection.IsAssociationList
				|| thisSideOneToManyCollection.IsManyToMany
				) {
				throw new PropertyMissingException(manyToManyAliasProperty.Owner.FullName, mma.OneToManyCollectionName);
			}
			XPMemberInfo intermediateThisSideReferenceMember = thisSideOneToManyCollection.GetAssociatedMember();
			XPMemberInfo intermediateAnotherSideReferenceMember = thisSideOneToManyCollection.CollectionElementType.GetMember(mma.ReferenceInTheIntermediateTableName);
			if(intermediateAnotherSideReferenceMember.ReferenceType == null) {
				throw new PropertyMissingException(thisSideOneToManyCollection.CollectionElementType.FullName, mma.ReferenceInTheIntermediateTableName);
			}
			XPMemberInfo anotherSideOneToManyCollection = intermediateAnotherSideReferenceMember.GetAssociatedMember();
			return new AggregateOperand(anotherSideOneToManyCollection.Name, Aggregate.Exists, new OperandProperty(intermediateThisSideReferenceMember.Name) == new OperandValue(owner));
		}
		protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e) {
			if(CollectionChanged != null) {
				switch(e.Action){
					case NotifyCollectionChangedAction.Add:
						CollectionChanged(this, new NotifyCollectionChangedEventArgs(e.Action, ConvertList(e.NewItems), e.NewStartingIndex));
						break;
					case NotifyCollectionChangedAction.Remove:
						CollectionChanged(this, new NotifyCollectionChangedEventArgs(e.Action, ConvertList(e.OldItems), e.OldStartingIndex));
						break;
					case NotifyCollectionChangedAction.Reset:
						CollectionChanged(this, e);
						break;
					default:
						throw new InvalidOperationException();
				}			  
			}
		}
		public event NotifyCollectionChangedEventHandler CollectionChanged;
	}
	public class XPAssociationList<T> : XPAssociationList, IList2<T>  {
		public XPAssociationList(Session session, object owner, XPMemberInfo collectionMember) : base(session, owner, collectionMember) { }
		int IList<T>.IndexOf(T item) {
			return IndexOf(item);
		}
		void IList<T>.Insert(int index, T item) {
			Insert(index, item);
		}
		void IList<T>.RemoveAt(int index) {
			RemoveAt(index);
		}
		public T this[int index] {
			get {
				return (T)Indexer(index);
			}
			set {
				throw new NotSupportedException();
			}
		}
		void ICollection<T>.Add(T item) {
			Add(item);
		}
		void ICollection<T>.Clear() {
			Clear();
		}
		bool ICollection<T>.Contains(T item) {
			return Contains(item);
		}
		void ICollection<T>.CopyTo(T[] array, int arrayIndex) {
			CopyTo(array, arrayIndex);
		}
		int ICollection<T>.Count {
			get { return Count; }
		}
		bool ICollection<T>.IsReadOnly {
			get { return IsReadOnly; }
		}
		bool ICollection<T>.Remove(T item) {
			Remove(item);
			return true; 
		}
		IEnumerator<T> IEnumerable<T>.GetEnumerator() {
			foreach(T t in this)
				yield return t;
		}
	}
	public class XPManyToManyAliasList<T> : XPManyToManyAliasList, IList2<T> {
		public XPManyToManyAliasList(Session session, IList aliasedCollection, XPMemberInfo skippedProperty) : base(session, aliasedCollection, skippedProperty) { }
		int IList<T>.IndexOf(T item) {
			return IndexOf(item);
		}
		void IList<T>.Insert(int index, T item) {
			Insert(index, item);
		}
		void IList<T>.RemoveAt(int index) {
			RemoveAt(index);
		}
		public T this[int index] {
			get {
				return (T)Indexer(index);
			}
			set {
				throw new NotSupportedException();
			}
		}
		void ICollection<T>.Add(T item) {
			Add(item);
		}
		void ICollection<T>.Clear() {
			Clear();
		}
		bool ICollection<T>.Contains(T item) {
			return Contains(item);
		}
		void ICollection<T>.CopyTo(T[] array, int arrayIndex) {
			CopyTo(array, arrayIndex);
		}
		int ICollection<T>.Count {
			get { return Count; }
		}
		bool ICollection<T>.IsReadOnly {
			get { return IsReadOnly; }
		}
		bool ICollection<T>.Remove(T item) {
			Remove(item);
			return true; 
		}
		IEnumerator<T> IEnumerable<T>.GetEnumerator() {
			foreach(T t in this)
				yield return t;
		}
	}
}
