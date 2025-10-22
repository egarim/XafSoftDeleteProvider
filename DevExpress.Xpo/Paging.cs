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

namespace DevExpress.Xpo.Helpers {
	using System;
	using System.Collections;
	using DevExpress.Xpo;
	using DevExpress.Xpo.Metadata;
	using System.Collections.Generic;
	public class XPCursorEnumerator : IEnumerator {
		PageSelector pageSelector;
		int posInCurrentCollection;
		int currentCollectionBase;
		int pageSize;
		Session session;
		List<object> currentCollection;
		SortingCollection sorting;
		bool IsAfterLastElement { get { return currentCollectionBase + posInCurrentCollection >= pageSelector.Count; } }
		bool IsBeforeFirstElement { get { return currentCollectionBase + posInCurrentCollection < 0; } }
		public XPCursorEnumerator(Session session, XPClassInfo objectClassInfo, PageSelector pageSelector, int pageSize, SortingCollection sorting) {
			this.pageSelector = pageSelector;
			this.pageSize = pageSize;
			this.sorting = sorting;
			this.session = session;
			Reset();
		}
		public XPCursorEnumerator(Session session, XPClassInfo objectClassInfo, PageSelector pageSelector) : this(session, objectClassInfo, pageSelector, 512, new SortingCollection()) { }
		public object Current {
			get {
				if(IsBeforeFirstElement || IsAfterLastElement)
					throw new InvalidOperationException(Res.GetString(Res.Paging_EnumeratorPositioning));
				return currentCollection[posInCurrentCollection];
			}
		}
		public bool MoveNext() {
			if(IsAfterLastElement)
				return false;
			++posInCurrentCollection;
			if(IsAfterLastElement)
				return false;
			if(posInCurrentCollection >= pageSize) {
				currentCollectionBase += pageSize;
				posInCurrentCollection = 0;
				ICollection objects = pageSelector.FillCollectionWithPageVerified(session, sorting, currentCollectionBase, pageSize);
				currentCollection = ListHelper.FromCollection(objects);
			}
			return true;
		}
		public void Reset() {
			currentCollectionBase = -pageSize;
			posInCurrentCollection = pageSize - 1;
		}
	}
}
namespace DevExpress.Xpo {
	using System;
	using System.Collections;
	using System.ComponentModel;
	using DevExpress.Xpo.Helpers;
	using DevExpress.Xpo.Metadata;
	using DevExpress.Data.Filtering;
	using System.Collections.Generic;
	using DevExpress.Utils.Design;
	public class PageSelector {
		IList keys;
		XPClassInfo objectClassInfo;
		int currentPageBase = -1;
		int currentPageSize = -1;
		CriteriaOperator criteria;
		public XPClassInfo ObjectClassInfo { get { return objectClassInfo; } }
		public PageSelector(XPClassInfo objectClassInfo, IList keysList) {
			this.objectClassInfo = objectClassInfo;
			this.keys = keysList;
		}
		public PageSelector(Session session, XPClassInfo objectClassInfo, CriteriaOperator criteria, int topReturnedObjects, bool selectDeleted, SortingCollection sorting) {
			this.objectClassInfo = objectClassInfo;
			lock(session) {
				this.keys = new List<object>();
				CriteriaOperatorCollection props = new CriteriaOperatorCollection();
				props.Add(new OperandProperty(ObjectClassInfo.KeyProperty.Name));
				List<object[]> resultSet = session.SelectData(ObjectClassInfo, props, criteria, selectDeleted, 0, topReturnedObjects, sorting);
				foreach(object[] a in resultSet)
					this.keys.Add(a[0]);
			}
		}
		public int Count { get { return keys.Count; } }
		public CriteriaOperator GetPageCriteria(int pageBase, int pageSize) {
			if(currentPageBase != pageBase || currentPageSize != pageSize) {
				List<object> inList = new List<object>();
				for(int i = pageBase; i < pageBase + pageSize && i < Count; ++i) {
					inList.Add(keys[i]);
				}
				if(inList.Count == 0)
					criteria = new NullOperator(ObjectClassInfo.KeyProperty.Name);
				else
					criteria = new InOperator(ObjectClassInfo.KeyProperty.Name, inList);
				currentPageBase = pageBase;
				currentPageSize = pageSize;
			}
			return criteria;
		}
		public int GetElementsInPage(int pageBase, int pageSize) {
			int elements = Count - pageBase;
			if(elements > pageSize)
				elements = pageSize;
			return elements;
		}
		public ICollection FillCollectionWithPageVerified(Session session, SortingCollection sorting, int pageBase, int pageSize) {
			ICollection pageCollection = session.GetObjects(objectClassInfo, GetPageCriteria(pageBase, pageSize), sorting, 0, 0, true, false);
			if(GetElementsInPage(pageBase, pageSize) != pageCollection.Count)
				throw new InvalidOperationException(Res.GetString(Res.Paging_EnumeratorObjectModifiedOrDeleted));
			return pageCollection;
		}
	}
	[DXToolboxItem(true)]
	[DevExpress.Utils.ToolboxTabName(AssemblyInfo.DXTabOrmComponents)]
	[DefaultProperty("Collection")]
	[Description("Allows the contents of an XPCollection to be split into pages. Can serve as a data source for data-aware controls.")]
#if !NET
	[System.Drawing.ToolboxBitmap(typeof(XPPageSelector))]
#endif
	[Designer("DevExpress.Xpo.Design.XPPageSelectorDesigner, " + AssemblyInfo.SRAssemblyXpoDesignFull, Aliases.IDesigner)]
	public class XPPageSelector : Component, IListSource {
		XPBaseCollection collection;
		PageSelector pageSelector;
		int pageSize = 10;
		int currentPage;
		int Count {
			get {
				if(pageSelector == null)
					((IListSource)this).GetList();
				return pageSelector != null ? pageSelector.Count : 0;
			}
		}
		IList IListSource.GetList() {
			if(collection != null && !(collection.Session.IsDesignMode)) {
				if(pageSelector == null)
					pageSelector = new PageSelector(collection.Session, collection.GetObjectClassInfo(), collection.Criteria, collection.TopReturnedObjects, collection.SelectDeleted, collection.Sorting);
				UpdateCollection();
			}
			return collection;
		}
		bool IListSource.ContainsListCollection {
			get { return false; }
		}
		void UpdateCollection() {
			if(pageSelector != null) {
				collection.LoadingEnabled = false;
				((IList)collection).Clear();
				collection.BaseAddRange(pageSelector.FillCollectionWithPageVerified(collection.Session, collection.Sorting, CurrentPage * PageSize, PageSize));
			}
		}
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public int CurrentPage {
			get { return currentPage; }
			set {
				if(currentPage != value) {
					if(value < 0)
						throw new ArgumentOutOfRangeException("CurrentPage", value, Res.GetString(Res.Paging_CurrentPageShouldBeGreaterOrEqualZero));
					if(value >= PageCount)
						throw new ArgumentOutOfRangeException("CurrentPage", value, Res.GetString(Res.Paging_CurrentPageShouldBeLessThanPageCount));
					currentPage = value;
					UpdateCollection();
				}
			}
		}
		[Browsable(false)]
		public int PageCount { get { return (Count - 1) / PageSize + 1; } }
		public XPPageSelector()
			: base() {
		}
		public XPPageSelector(IContainer container)
			: this() {
			container.Add(this);
		}
		public XPPageSelector(XPBaseCollection collection)
			: base() {
			Collection = collection;
		}
		[Description("Gets or sets the maximum number of persistent objects that can be contained in a single page."), DefaultValue(10)]
		[Category("Data")]
		public int PageSize {
			get { return pageSize; }
			set {
				if(value <= 0)
					throw new ArgumentOutOfRangeException("PageSize", value, Res.GetString(Res.Paging_PageSizeShouldBeGreaterThanZero));
				if(pageSize != value) {
					pageSize = value;
					currentPage = 0;
					UpdateCollection();
				}
			}
		}
		[Description("Gets or sets the collection of persistent objects that the page selector is bound to."), DefaultValue(null)]
		[Category("Data")]
		public XPBaseCollection Collection {
			get { return collection; }
			set { collection = value; }
		}
	}
	public class XPCursor : ICollection {
		Session session;
		Int32 topReturnedObjects;
		XPClassInfo objInfo;
		SortingCollection sorting;
		CriteriaOperator criteria;
		int pageSize = 512;
		bool selectDeleted;
		PageSelector pageSelector;
		bool IsLoaded { get { return pageSelector != null; } }
		public void CopyTo(Array array, int index) {
			foreach(object theObject in this) {
				array.SetValue(theObject, index++);
			}
		}
		[Description("Gets the number of persistent objects within the collection.")]
public int Count {
			get {
				if(!IsLoaded)
					Load();
				return pageSelector.Count;
			}
		}
		[Description("Gets or sets the maximum number of persistent objects that can be contained in a single page.")]
public int PageSize {
			get { return pageSize; }
			set {
				if(value <= 0) {
					throw new ArgumentOutOfRangeException("PageSize", value, Res.GetString(Res.Paging_PageSizeShouldBeGreaterThanZero));
				}
				pageSize = value;
			}
		}
		bool ICollection.IsSynchronized { get { return false; } }
		object ICollection.SyncRoot { get { return this; } }
		public IEnumerator GetEnumerator() {
			if(!IsLoaded)
				Load();
			return new XPCursorEnumerator(Session, ObjectClassInfo, pageSelector, PageSize, Sorting);
		}
		[Description("Provides access to the collection whose elements identify the sorted columns in a data store.")]
public SortingCollection Sorting { get { return sorting; } }
		[Description("Gets or sets the maximum number of objects retrieved by the XPCursor collection.")]
public Int32 TopReturnedObjects {
			get { return this.topReturnedObjects; }
			set {
				topReturnedObjects = value;
				Clear();
			}
		}
		[Description("Gets the metadata information for the persistent objects retrieved by the collection.")]
public XPClassInfo ObjectClassInfo {
			get {
				return objInfo;
			}
		}
		[Description("Gets the session which is used to load and save persistent objects.")]
public Session Session { get { return session; } }
		[Description("Gets or sets whether deleted objects are retrieved by the XPCursor the next time it is reloaded.")]
public bool SelectDeleted {
			get { return selectDeleted; }
			set {
				if(selectDeleted != value) {
					selectDeleted = value;
					Clear();
				}
			}
		}
		protected void Clear() {
			pageSelector = null;
		}
		public XPCursor(Type objType) : this(objType, null) { }
		public XPCursor(Type objType, CriteriaOperator theCriteria, params SortProperty[] sortProperties) : this(XpoDefault.GetSession(), objType, theCriteria, sortProperties) { }
		public XPCursor(Session session, Type objType) : this(session, objType, (CriteriaOperator)null) { }
		public XPCursor(Session session, XPClassInfo objType) : this(session, objType, (CriteriaOperator)null) { }
		public XPCursor(Session session, Type objType, CriteriaOperator theCriteria, params SortProperty[] sortProperties) : this(session, session. Dictionary. GetClassInfo(objType), theCriteria, sortProperties) { }
		public XPCursor(Session session, XPClassInfo objType, CriteriaOperator theCriteria)
			: base() {
			this.session = session;
			this.sorting = new SortingCollection();
			this.objInfo = objType;
			this.criteria = theCriteria;
		}
		public XPCursor(Session session, XPClassInfo objType, CriteriaOperator theCriteria, params SortProperty[] sortProperties)
			: this(session, objType, theCriteria) {
			this.Sorting.AddRange(sortProperties);
		}
		public XPCursor(Session session, XPClassInfo objType, IList keysList)
			: this(session, objType, (CriteriaOperator)null) {
			this.pageSelector = new PageSelector(objType, keysList);
		}
		public XPCursor(Session session, Type objType, IList keysList)
			: this(session, session.GetClassInfo(objType), keysList) {}
		protected void Load() {
			pageSelector = new PageSelector(Session, ObjectClassInfo, criteria, TopReturnedObjects, SelectDeleted, Sorting);
		}
	}
}
