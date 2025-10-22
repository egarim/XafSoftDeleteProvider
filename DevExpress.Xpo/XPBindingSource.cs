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
using System.ComponentModel;
using System.ComponentModel.Design;
using System.ComponentModel.Design.Serialization;
using System.Linq;
using DevExpress.Data.Filtering.Exceptions;
using DevExpress.Utils.Design;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.Helpers;
using DevExpress.Xpo.Metadata;
using DevExpress.Xpo.Metadata.Helpers;
namespace DevExpress.Xpo {
	[DXToolboxItem(true)]
	[DevExpress.Utils.ToolboxTabName(AssemblyInfo.DXTabOrmComponents)]
	[DefaultProperty("DataSource"), DefaultEvent("ListChanged")]
	[Description("A bindable collection of persistent objects. Can serve as a data source for data-aware controls.")]
#if !NET
	[System.Drawing.ToolboxBitmap(typeof(XPBindingSource))]
#endif
	[DesignerSerializer("DevExpress.Xpo.Design.XPBindingSourceSerializer, " + AssemblyInfo.SRAssemblyXpoDesignFull, Aliases.CodeDomSerializer)]
	[Designer("DevExpress.Xpo.Design.XPBindingSourceDesigner, " + AssemblyInfo.SRAssemblyXpoDesignFull, Aliases.IDesigner)]
	public class XPBindingSource : Component, IBindingList, ITypedList, ISupportInitialize, IXPClassInfoProvider, ISessionProvider, IObjectChange {
		protected bool Initializing;
		bool hasChangesDuringInit;
		IServiceProvider serviceProvider;
		public XPBindingSource() : this((IServiceProvider)(null)) { }
		public XPBindingSource(IServiceProvider serviceProvider)
			: base() {
			this.serviceProvider = serviceProvider;
			InitData();
		}
		public XPBindingSource(IContainer container)
			: this(null, container) { }
		public XPBindingSource(IServiceProvider serviceProvider, IContainer container)
			: this(serviceProvider) {
			container.Add(this);
		}
		void InitData() {
			BindingBehavior = CollectionBindingBehavior.AllowNew | CollectionBindingBehavior.AllowRemove;
		}
		void ISupportInitialize.BeginInit() {
			Initializing = true;
			hasChangesDuringInit = false;
		}
		void ISupportInitialize.EndInit() {
			Initializing = false;
			if(hasChangesDuringInit) {
				PropertyDescriptorChanged();
				Reset();
			}
			hasChangesDuringInit = false;
		}
		bool? _isDesignMode;
		bool IsDesignMode {
			get {
				return !disableDesignModeDetection && DevExpress.Data.Helpers.IsDesignModeHelper.GetIsDesignModeBypassable(this, ref _isDesignMode);
			}
		}
		bool disableDesignModeDetection;
		[EditorBrowsable(EditorBrowsableState.Never)]
		public void DisableDesignModeDetection() {
			disableDesignModeDetection = true;
		}
		bool IsDesignerHosted(bool useSession) {
			return GetDesignerHostSite(useSession) != null;
		}
		ISite GetDesignerHostSite(bool useSession) {
			if(IsDesignerHostSite(this.Site)) {
				return this.Site;
			}
			var ds = dataSource as Component;
			if(ds != null) {
				if(IsDesignerHostSite(ds.Site)) {
					return ds.Site;
				}
			}
			if(useSession) {
				Session session = GetSession();
				if(IsDesignerHostSite(session.Site)) {
					return session.Site;
				}
			}
			return null;
		}
		bool IsDesignerHostSite(ISite site) {
			if(site == null) {
				return false;
			}
			var sc = (IServiceContainer)site.GetService(typeof(IServiceContainer));
			if(sc == null) {
				return false;
			}
			var host = (IDesignerHost)sc.GetService(typeof(IDesignerHost));
			if(host != null) {
				return true;
			}
			return false;
		}
		object dataSource;
		[Description("Gets or sets a data source the XPBindingSource binds to a control."), DefaultValue(null)]
		[TypeConverter("DevExpress.Xpo.Design.XPBindingSourceDataSourceReferenceConverter, " + AssemblyInfo.SRAssemblyXpoDesignFull)]
		[RefreshProperties(RefreshProperties.All)]
		[Category("Data")]
		public object DataSource {
			get {
				return dataSource;
			}
			set {
				if(dataSource != value) {
					UnsubscribeDataSourceEvents();
					dataSource = value;
					innerList = null;
					isSingleObjectDataSource = false;
					if(value is IXPDictionaryProvider) {
						dictionary = null;
					}
					var ciProvider = value as IXPClassInfoProvider;
					if(ciProvider != null) {
						objectClassInfo = null;
						objectType = null;
					}
					else {
						if(value != null) {
							var enumerable = value as IEnumerable;
							if(enumerable != null) {
								objectType = GetEnumerableElementType(enumerable);
								objectClassInfo = null;
							}
							else {
								objectType = value.GetType();
								objectClassInfo = null;
							}
						}
						else {
							objectType = typeof(object);
							objectClassInfo = null;
						}
					}
					if(!Initializing) {
						InvokeDataSourceChanged();
					}
					PropertyDescriptorChanged();
					Reset();
				}
			}
		}
		Type GetEnumerableElementType(IEnumerable enumerable) {
			Type type = enumerable.GetType();
			if(type.IsGenericType) {
				Type[] genericArgs = type.GenericTypeArguments;
				if(genericArgs.Length == 1) {
					return genericArgs[0];
				}
			}
			else if(type.IsArray && type.HasElementType) {
				return type.GetElementType();
			}
			return typeof(object);
		}
		void SubscribeDataSourceEvents() {
			if(InnerList == null) {
				return;
			}
			if(!isSingleObjectDataSource) {
				var bindingList = InnerList as IBindingList;
				if(bindingList != null) {
					bindingList.ListChanged += OnInnerListChanged;
				}
				else {
					var observable = InnerList as INotifyCollectionChanged;
					if(observable != null) {
						observable.CollectionChanged += OnInnerListNotifyCollectionChanged;
					}
				}
			}
			else {
				var notifyPropChange = dataSource as INotifyPropertyChanged;
				if(notifyPropChange != null) {
					notifyPropChange.PropertyChanged += OnInnerObjectNotifyPropertyChanged;
				}
			}
		}
		void UnsubscribeDataSourceEvents() {
			if(innerList == null) {
				return;
			}
			if(!isSingleObjectDataSource) {
				var bindingList = InnerList as IBindingList;
				if(bindingList != null) {
					bindingList.ListChanged -= OnInnerListChanged;
				}
				else {
					var observable = InnerList as INotifyCollectionChanged;
					if(observable != null) {
						observable.CollectionChanged -= OnInnerListNotifyCollectionChanged;
					}
				}
			}
			else {
				var notifyPropChange = dataSource as INotifyPropertyChanged;
				if(notifyPropChange != null) {
					notifyPropChange.PropertyChanged -= OnInnerObjectNotifyPropertyChanged;
				}
			}
		}
		bool isSingleObjectDataSource;
		IList innerList;
		IList InnerList {
			get {
				if(innerList != null) {
					return innerList;
				}
				if(dataSource != null) {
					isSingleObjectDataSource = false;
					var bindingList = dataSource as IBindingList;
					if(bindingList != null) {
						innerList = bindingList;
					}
					else {
						var list = dataSource as IList;
						if(list != null) {
							innerList = list;
						}
						else {
							var enumerable = dataSource as IEnumerable;
							if(enumerable != null) {
								innerList = enumerable.Cast<object>().ToList();
							}
							else {
								var listSource = dataSource as IListSource;
								if(listSource != null) {
									innerList = listSource.GetList();
								}
								else {
									isSingleObjectDataSource = true;
									innerList = new object[] { dataSource };
								}
							}
						}
					}
				}
				else {
					innerList = new List<object>();
				}
				if(innerList != null) {
					SubscribeDataSourceEvents();
				}
				return innerList;
			}
		}
		CollectionBindingBehavior bindingBehavior;
		[Description("Defines operations the bound control can perform with the data source."), DefaultValue(CollectionBindingBehavior.AllowNew | CollectionBindingBehavior.AllowRemove)]
		[Editor("DevExpress.Xpo.Design.FlagsEnumEditor, " + AssemblyInfo.SRAssemblyXpoDesignFull, DevExpress.Utils.ControlConstants.UITypeEditor)]
		[Category("Options")]
		public CollectionBindingBehavior BindingBehavior {
			get { return bindingBehavior; }
			set { bindingBehavior = value; }
		}
		string displayableProperties;
		[Description("Gets or sets a list of properties available for binding.")]
		[Editor("DevExpress.Xpo.Design.DisplayablePropertiesEditor, " + AssemblyInfo.SRAssemblyXpoDesignFull, DevExpress.Utils.ControlConstants.UITypeEditor)]
		[Category("View")]
		[Localizable(false)]
		public string DisplayableProperties {
			get {
				if(!string.IsNullOrEmpty(displayableProperties)) {
					return displayableProperties;
				}
				string[] props = GetDefaultDisplayableProperties();
				if(props != null) {
					return string.Join(";", props);
				}
				return null;
			}
			set {
				if(displayableProperties != value) {
					displayableProperties = value;
					PropertyDescriptorChanged();
				}
			}
		}
		string[] GetDefaultDisplayableProperties() {
			string[] dataSourceProps = GetDataSourceDisplayableProperties();
			if(dataSourceProps != null) {
				return dataSourceProps;
			}
			else {
				return GetClassInfoDisplayableProperties(GetObjectClassInfo());
			}
		}
		string[] GetClassInfoDisplayableProperties(XPClassInfo classInfo) {
			if(classInfo == null) {
				return Array.Empty<string>();
			}
			return ClassMetadataHelper.GetDefaultDisplayableProperties(classInfo)
				.Cast<string>()
				.Where(t => !t.EndsWith(XPPropertyDescriptorBase.ReferenceAsObjectTail))
				.ToArray();
		}
		string[] GetDataSourceDisplayableProperties() {
			XPBaseCollection col = dataSource as XPBaseCollection;
			if(col != null) {
				if(!string.IsNullOrEmpty(col.DisplayableProperties)) {
					return col.DisplayableProperties.Split(';');
				}
				return null;
			}
			XPBindingSource bs = dataSource as XPBindingSource;
			if(bs != null) {
				if(!string.IsNullOrEmpty(bs.DisplayableProperties)) {
					return bs.DisplayableProperties.Split(';');
				}
				return null;
			}
			XPServerCollectionSource srv = dataSource as XPServerCollectionSource;
			if(srv != null) {
				if(!string.IsNullOrEmpty(srv.DisplayableProperties)) {
					return srv.DisplayableProperties.Split(';');
				}
				return null;
			}
			return null;
		}
		[Description("Gets or sets the XPClassInfo metadata that describes the type of items the data source contains."), DefaultValue(null)]
		[TypeConverter("DevExpress.Xpo.Design.ObjectClassInfoTypeConverter, " + AssemblyInfo.SRAssemblyXpoDesignFull)]
		[MergableProperty(false)]
		[RefreshProperties(RefreshProperties.All)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		[Category("Data")]
		public XPClassInfo ObjectClassInfo {
			get {
				return GetObjectClassInfo();
			}
			set {
				if(!Initializing) {
					ThrowIfBadClassInfo(value);
				}
				if(CanSetObjectClassInfo()) {
					objectClassInfo = value;
					objectType = null;
					PropertyDescriptorChanged();
				}
			}
		}
		void ThrowIfBadClassInfo(XPClassInfo newClassInfo) {
			var ciProvider = dataSource as IXPClassInfoProvider;
			if(ciProvider == null || ciProvider.ClassInfo == newClassInfo) {
				return;
			}
			if(IsDesignMode) {
				if(ciProvider.ClassInfo != null && newClassInfo != null && ciProvider.ClassInfo.FullName == newClassInfo.FullName) {
					return;
				}
			}
			if(ciProvider.ClassInfo != newClassInfo) {
				throw new InvalidOperationException(Res.GetString(Res.XPBindingSource_BadClassInfo_DataSource));
			}
			var dictProvider = dataSource as IXPDictionaryProvider;
			if(newClassInfo != null && dictProvider != null && dictProvider.Dictionary != newClassInfo.Dictionary) {
				throw new InvalidOperationException(Res.GetString(Res.XPBindingSource_BadClassInfo_DataSourceDictionary));
			}
			if(newClassInfo != null && dictionary != null && newClassInfo.Dictionary != dictionary) {
				throw new InvalidOperationException(Res.GetString(Res.XPBindingSource_BadClassInfo_Dictionary));
			}
		}
		void ThrowIfBadObjectType(Type newObjectType) {
			var ciProvider = dataSource as IXPClassInfoProvider;
			if(ciProvider != null && ciProvider.ClassInfo != null && ciProvider.ClassInfo.ClassType != newObjectType) {
				throw new InvalidOperationException(Res.GetString(Res.XPBindingSource_BadObjectType_DataSource));
			}
		}
		bool CanSetObjectClassInfo() {
			if(dataSource is IXPClassInfoProvider) {
				return false;
			}
			return true;
		}
		Type objectType;
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
		public Type ObjectType {
			get {
				XPClassInfo ci = GetObjectClassInfo();
				return (ci != null) ? ci.ClassType : null;
			}
			set {
				if(objectType != value) {
					if(!Initializing) {
						ThrowIfBadObjectType(value);
					}
					objectType = value;
					objectClassInfo = null;
					PropertyDescriptorChanged();
				}
			}
		}
		[Browsable(false)]
		Session ISessionProvider.Session {
			get { return GetSession(); }
		}
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public XPDictionary Dictionary {
			get { return GetDictionary(true); }
			set {
				dictionary = value;
				if(objectClassInfo != null && objectClassInfo.Dictionary != value) {
					objectClassInfo = null;
				}
			}
		}
		IObjectLayer IObjectLayerProvider.ObjectLayer {
			get {
				Session session = GetSession();
				return session.ObjectLayer;
			}
		}
		IDataLayer IDataLayerProvider.DataLayer {
			get {
				Session session = GetSession();
				return session.DataLayer;
			}
		}
		XPClassInfo IXPClassInfoProvider.ClassInfo {
			get {
				return GetObjectClassInfo();
			}
		}
		Session inMemorySession;
		Session GetSession() {
			if(IsDesignMode && IsDesignerHosted(false)) {
				return new DevExpress.Xpo.Helpers.DefaultSession(GetDesignerHostSite(false));
			}
			else {
				if(dataSource != null) {
					var sessionProvider = dataSource as ISessionProvider;
					if(sessionProvider != null && sessionProvider.Session != null) {
						return sessionProvider.Session;
					}
				}
				XPDictionary dict = GetDictionary(false);
				if(inMemorySession == null || (dict != null && dict != inMemorySession.Dictionary)) {
					inMemorySession = new UnitOfWork(serviceProvider, new SimpleDataLayer(dict, new InMemoryDataStore()));
				}
				return inMemorySession;
			}
		}
		XPDictionary designDictionary;
		XPDictionary dictionary;
		XPDictionary GetDictionary(bool useSession) {
			if(IsDesignMode && IsDesignerHosted(useSession)) {
				if(designDictionary == null) {
					designDictionary = new DesignTimeReflection(GetDesignerHostSite(useSession));
				}
				return designDictionary;
			}
			if(dictionary != null) {
				return dictionary;
			}
			if(objectClassInfo != null) {
				return objectClassInfo.Dictionary;
			}
			var provider = dataSource as IXPDictionaryProvider;
			if(provider != null) {
				return provider.Dictionary;
			}
			if(useSession) {
				Session session = GetSession();
				return session.Dictionary;
			}
			return null;
		}
		XPClassInfo objectClassInfo;
		XPClassInfo GetObjectClassInfo() {
			if(objectClassInfo != null) {
				return objectClassInfo;
			}
			var ciProvider = dataSource as IXPClassInfoProvider;
			if(ciProvider != null) {
				return ciProvider.ClassInfo;
			}
			if(objectType == null) {
				return null;
			}
			XPDictionary dict = GetDictionary(true);
			if(dict != null) {
				return dict.QueryClassInfo(objectType);
			}
			return null;
		}
		void Reset() {
			InvokeListChanged(ListChangedType.Reset, -1, -1);
		}
		void PropertyDescriptorChanged() {
			InvokeListChanged(ListChangedType.PropertyDescriptorChanged, 0, 0);
		}
		void InvokeListChanged(ListChangedType changeType, int newIndex, int oldIndex) {
			if(Initializing) {
				hasChangesDuringInit = true;
			}
			else {
				if(ListChanged != null) {
					ListChanged(this, new ListChangedEventArgs(changeType, newIndex, oldIndex));
				}
			}
		}
		void InvokeDataSourceChanged() {
			if(DataSourceChanged != null) {
				DataSourceChanged(this, EventArgs.Empty);
			}
		}
		void OnInnerListChanged(object sender, ListChangedEventArgs e) {
			if(Initializing) {
				hasChangesDuringInit = true;
				return;
			}
			if(ListChanged != null) {
				ListChanged(this, e);
			}
		}
		void OnInnerObjectNotifyPropertyChanged(object sender, PropertyChangedEventArgs e) {
			if(ListChanged != null) {
				ListChanged(this, new ListChangedEventArgs(ListChangedType.ItemChanged, 0, 0));
			}
		}
		void OnInnerListNotifyCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
			switch(e.Action) {
				case NotifyCollectionChangedAction.Add:
					for(int i = 0; i < e.NewItems.Count; i++) {
						InvokeListChanged(ListChangedType.ItemAdded, e.NewStartingIndex + i, -1);
					}
					break;
				case NotifyCollectionChangedAction.Move:
					for(int i = 0; i < e.OldItems.Count; i++) {
						InvokeListChanged(ListChangedType.ItemMoved, e.NewStartingIndex + i, e.OldStartingIndex + i);
					}
					break;
				case NotifyCollectionChangedAction.Remove:
					for(int i = 0; i < e.OldItems.Count; i++) {
						InvokeListChanged(ListChangedType.ItemDeleted, e.OldStartingIndex + i, -1);
					}
					break;
				case NotifyCollectionChangedAction.Replace:
					for(int i = 0; i < e.OldItems.Count; i++) {
						InvokeListChanged(ListChangedType.ItemChanged, e.OldStartingIndex + i, e.OldStartingIndex + i);
					}
					break;
				case NotifyCollectionChangedAction.Reset:
					Reset();
					break;
			}
		}
		[Description("Occurs in one of the following cases: the XPBindingSource.DataSource property value changed, the data source’s element list changed, or individual elements in the data source changed.")]
		public event ListChangedEventHandler ListChanged;
		[Description("Occurs when the XPBindingSource.DataSource property value changes.")]
		public event EventHandler DataSourceChanged;
		bool ShouldSerializeDisplayableProperties() {
			return !string.IsNullOrEmpty(displayableProperties);
		}
		void ResetDisplayableProperties() {
			DisplayableProperties = null;
		}
		bool ShouldSerializeObjectType() {
			return ObjectType != null && CanSetObjectClassInfo();
		}
		void ResetObjectType() {
			ObjectType = null;
		}
		void IObjectChange.OnObjectChanged(object sender, ObjectChangeEventArgs args) {
			if(args.Reason == ObjectChangeReason.CancelEdit) {
				if(IsDesignMode) {
					nowAddingObject = null;
					InvokeListChanged(ListChangedType.ItemDeleted, 0, 0);
				}
				else if(ReferenceEquals(nowAddingObject, args.Object)) {
					if(InnerList != null && !(InnerList is IBindingList)) {
						InnerList.Remove(nowAddingObject);
						XPBaseObject.RemoveChangedEventHandler(nowAddingObject, ((IObjectChange)this).OnObjectChanged);
						Session session = GetSession();
						session.Delete(nowAddingObject);
						nowAddingObject = null;
					}
				}
			}
			var inner = InnerList as IObjectChange;
			if(inner != null) {
				inner.OnObjectChanged(sender, args);
			}
		}
		[System.Runtime.CompilerServices.IndexerNameAttribute("Object"), Browsable(false)]
		public object this[int index] {
			get {
				if(IsDesignMode) {
					return nowAddingObject;
				}
				if(InnerList != null) {
					return InnerList[index];
				}
				return null;
			}
			set {
				if(InnerList != null) {
					InnerList[index] = value;
				}
			}
		}
		bool IBindingList.AllowNew {
			get {
				if(IsDesignMode && !IsDesignerHosted(true)) {
					return false;
				}
				if((this.BindingBehavior & CollectionBindingBehavior.AllowNew) == 0) {
					return false;
				}
				var inner = InnerList as IBindingList;
				if(inner != null) {
					return inner.AllowNew;
				}
				if(InnerList == null || InnerList.IsFixedSize || InnerList.IsReadOnly || !(InnerList is ISessionProvider)) {
					return false;
				}
				XPClassInfo ci = GetObjectClassInfo();
				return (ci != null) ? !ci.IsAbstract : false;
			}
		}
		bool IBindingList.AllowEdit {
			get {
				if(dataSource == null) {
					return false;
				}
				var inner = InnerList as IBindingList;
				if(inner != null) {
					return inner.AllowEdit;
				}
				return (InnerList != null && !InnerList.IsReadOnly);
			}
		}
		bool IBindingList.AllowRemove {
			get {
				if(dataSource == null || (this.BindingBehavior & CollectionBindingBehavior.AllowRemove) == 0) {
					return false;
				}
				var inner = InnerList as IBindingList;
				if(inner != null) {
					return inner.AllowRemove;
				}
				return (InnerList != null && !InnerList.IsFixedSize);
			}
		}
		bool IBindingList.SupportsChangeNotification {
			get {
				return true;
			}
		}
		bool IBindingList.SupportsSearching {
			get {
				if(dataSource == null) {
					return false;
				}
				var inner = InnerList as IBindingList;
				return (inner != null && inner.SupportsSearching);
			}
		}
		bool IBindingList.SupportsSorting {
			get {
				if(dataSource == null) {
					return false;
				}
				var inner = InnerList as IBindingList;
				return (inner != null && inner.SupportsSorting);
			}
		}
		bool IBindingList.IsSorted {
			get {
				if(dataSource == null) {
					return false;
				}
				var inner = InnerList as IBindingList;
				return (inner != null && inner.IsSorted);
			}
		}
		PropertyDescriptor IBindingList.SortProperty {
			get {
				if(dataSource == null) {
					return null;
				}
				var inner = InnerList as IBindingList;
				if(inner == null || inner.SortProperty == null) {
					return null;
				}
				PropertyDescriptorCollection props = ((ITypedList)this).GetItemProperties(null);
				return props.Find(inner.SortProperty.Name, false);
			}
		}
		ListSortDirection IBindingList.SortDirection {
			get {
				if(dataSource == null) {
					return ListSortDirection.Ascending;
				}
				var inner = InnerList as IBindingList;
				return inner != null ? inner.SortDirection : ListSortDirection.Ascending;
			}
		}
		object nowAddingObject;
		object IBindingList.AddNew() {
			Session session = GetSession();
			XPClassInfo classInfo = GetObjectClassInfo();
			if(IsDesignMode) {
				if(classInfo != null) {
					if(IsDesignerHosted(true)) {
						nowAddingObject = new IntermediateObject(session, classInfo);
						((IntermediateObject)nowAddingObject).Changed += ((IObjectChange)this).OnObjectChanged;
						InvokeListChanged(ListChangedType.ItemAdded, 0, 0);
						return nowAddingObject;
					}
					else {
						nowAddingObject = classInfo.CreateNewObject(session);
						XPBaseObject.AddChangedEventHandler(nowAddingObject, ((IObjectChange)this).OnObjectChanged);
						InnerList.Add(nowAddingObject);
						return nowAddingObject;
					}
				}
			}
			else {
				var inner = InnerList as IBindingList;
				if(inner != null) {
					return inner.AddNew();
				}
				if(classInfo != null) {
					nowAddingObject = classInfo.CreateNewObject(session);
					XPBaseObject.AddChangedEventHandler(nowAddingObject, ((IObjectChange)this).OnObjectChanged);
					InnerList.Add(nowAddingObject);
					return nowAddingObject;
				}
			}
			return null;
		}
		void IBindingList.ApplySort(PropertyDescriptor property, ListSortDirection direction) {
			if(dataSource == null) {
				return;
			}
			var inner = InnerList as IBindingList;
			if(inner != null) {
				inner.ApplySort(property, direction);
			}
		}
		void IBindingList.AddIndex(PropertyDescriptor property) { }
		void IBindingList.RemoveIndex(PropertyDescriptor property) { }
		void IBindingList.RemoveSort() {
			if(dataSource == null) {
				return;
			}
			var inner = InnerList as IBindingList;
			if(inner != null) {
				inner.RemoveSort();
			}
		}
		bool IList.IsReadOnly {
			get {
				if(dataSource == null) {
					return true;
				}
				return InnerList != null ? InnerList.IsReadOnly : true;
			}
		}
		bool IList.IsFixedSize {
			get {
				if(dataSource == null) {
					return true;
				}
				return InnerList != null ? InnerList.IsFixedSize : true;
			}
		}
		[Browsable(false)]
		public int Count {
			get {
				if(IsDesignMode) {
					return nowAddingObject != null ? 1 : 0;
				}
				if(Initializing) {
					return 0;
				}
				return InnerList != null ? InnerList.Count : 0;
			}
		}
		object ICollection.SyncRoot {
			get {
				return this;
			}
		}
		bool ICollection.IsSynchronized {
			get {
				return false;
			}
		}
		int IList.Add(object value) {
			if(InnerList == null) {
				throw new InvalidOperationException();
			}
			return InnerList.Add(value);
		}
		void IList.Clear() {
			if(InnerList == null) {
				throw new InvalidOperationException();
			}
			InnerList.Clear();
		}
		bool IList.Contains(object value) {
			if(InnerList == null) {
				return false;
			}
			return InnerList.Contains(value);
		}
		void ICollection.CopyTo(Array array, int index) {
			if(dataSource == null) {
				throw new InvalidOperationException();
			}
			var inner = InnerList as ICollection;
			if(inner == null) {
				throw new InvalidOperationException();
			}
			inner.CopyTo(array, index);
		}
		int IBindingList.Find(PropertyDescriptor property, object key) {
			if(dataSource == null) {
				return -1;
			}
			var inner = InnerList as IBindingList;
			if(inner == null) {
				return -1;
			}
			return inner.Find(property, key);
		}
		IEnumerator IEnumerable.GetEnumerator() {
			if(IsDesignMode || dataSource == null || InnerList == null) {
				return Array.Empty<object>().GetEnumerator();
			}
			return InnerList.GetEnumerator();
		}
		int IList.IndexOf(object value) {
			if(dataSource == null) {
				return -1;
			}
			return (InnerList != null) ? InnerList.IndexOf(value) : -1;
		}
		void IList.Insert(int index, object value) {
			if(dataSource == null || InnerList == null) {
				throw new InvalidOperationException();
			}
			InnerList.Insert(index, value);
		}
		void IList.Remove(object value) {
			if(dataSource == null || InnerList == null) {
				throw new InvalidOperationException();
			}
			InnerList.Remove(value);
		}
		void IList.RemoveAt(int index) {
			if(dataSource == null || InnerList == null) {
				throw new InvalidOperationException();
			}
			InnerList.RemoveAt(index);
		}
		PropertyDescriptorCollection ITypedList.GetItemProperties(PropertyDescriptor[] listAccessors) {
			XPClassInfo classInfo = GetObjectClassInfo();
			if(classInfo == null) {
				return new PropertyDescriptorCollection(Array.Empty<PropertyDescriptor>());
			}
			if(listAccessors == null || listAccessors.Length == 0) {
				string[] dispProps = DisplayableProperties.Split(';');
				return new XPBindingSourcePropertyDescriptorCollection(classInfo, dispProps, this);
			}
			for(int i = 0; i < listAccessors.Length; i++) {
				XPMemberInfo memberInfo = classInfo.FindMember(listAccessors[i].Name);
				if(memberInfo == null) {
					classInfo = null;
					break;
				}
				if(memberInfo.ReferenceType != null) {
					classInfo = memberInfo.ReferenceType;
				}
				else if(memberInfo.IsCollection || memberInfo.IsAssociationList || memberInfo.IsNonAssociationList) {
					classInfo = memberInfo.CollectionElementType;
				}
				else {
					classInfo = null;
					break;
				}
			}
			if(classInfo != null) {
				string[] props = GetClassInfoDisplayableProperties(classInfo);
				return new XPBindingSourcePropertyDescriptorCollection(classInfo, props, this);
			}
			return new PropertyDescriptorCollection(Array.Empty<PropertyDescriptor>());
		}
		string ITypedList.GetListName(PropertyDescriptor[] listAccessors) {
			return ClassMetadataHelper.GetListName(listAccessors);
		}
		protected override void Dispose(bool disposing) {
			if(disposing) {
				UnsubscribeDataSourceEvents();
			}
			base.Dispose(disposing);
		}
	}
	sealed class XPBindingSourcePropertyDescriptorCollection : PropertyDescriptorCollection {
		readonly XPClassInfo objectType;
		readonly ISessionProvider sessionProvider;
		public XPBindingSourcePropertyDescriptorCollection(XPClassInfo objectType, string[] displayableProperties, ISessionProvider sessionProvider)
			: base(null) {
			this.objectType = objectType;
			this.sessionProvider = sessionProvider;
			foreach(var prop in displayableProperties) {
				Find(prop, false);
			}
		}
		public override PropertyDescriptor Find(string name, bool ignoreCase) {
			if(name == null || name.Length == 0) {
				return null;
			}
			PropertyDescriptor pd = base.Find(name, ignoreCase);
			if(pd == null) {
				var col = new MemberInfoCollection(objectType, XPPropertyDescriptorBase.GetMemberName(name), true, false);
				if(col.Count == 0) {
					return null;
				}
				if(name.EndsWith(XPPropertyDescriptorBase.ReferenceAsObjectTail)) {
					return null;
				}
				pd = new XPBindingSourcePropertyDescriptor(objectType, name, sessionProvider);
				Add(pd);
			}
			return pd;
		}
		public override PropertyDescriptor this[string itemIndex] {
			get { return FindCaseSmart(itemIndex); }
		}
		public override PropertyDescriptor this[int itemIndex] {
			get { return base[itemIndex]; }
		}
		public PropertyDescriptor FindCaseSmart(string name) {
			PropertyDescriptor rv = Find(name, false);
			if(rv != null) {
				return rv;
			}
			return Find(name, true);
		}
	}
	sealed class XPBindingSourcePropertyDescriptor : XPPropertyDescriptorBase, IObjectChange {
		class XPBindingSourceAccessor : ValueAccessor {
			protected readonly XPMemberInfo Member;
			readonly bool upCast;
			public XPBindingSourceAccessor(XPClassInfo objectType, XPMemberInfo member) {
				this.Member = member;
				upCast = !objectType.IsAssignableTo(member.Owner);
			}
			public override object GetValue(object obj) {
				if(obj == null || (upCast && !Member.Owner.Dictionary.GetClassInfo(obj).IsAssignableTo(Member.Owner))) {
					return null;
				}
				object value = Member.GetValue(obj);
				XPBindingSource bindingSource = new XPBindingSource();
				bindingSource.DataSource = value;
				return bindingSource;
			}
			public override bool SetValue(object obj, object value) {
				throw new InvalidOperationException(Res.GetString(Res.Object_ReferencePropertyViaCollectionAccessor));
			}
			public override string ChangedPropertyName {
				get { return Member.Name; }
			}
		}
		XPMemberInfo targetMember;
		XPClassInfo objectType;
		MemberInfoCollection path;
		Type propertyType;
		ValueAccessor accessor;
		string displayName;
		string memberName;
		public override string DisplayName {
			get {
				return displayName;
			}
		}
		public XPBindingSourcePropertyDescriptor(XPClassInfo objectType, string propertyName, ISessionProvider sessionProvider)
			: base(propertyName) {
			this.objectType = objectType;
			var memberPath = new MemberInfoCollection(objectType, XPPropertyDescriptorBase.GetMemberName(propertyName), true);
			this.targetMember = memberPath[memberPath.Count - 1];
			memberName = XPPropertyDescriptorBase.GetMemberName(propertyName);
			displayName = targetMember.DisplayName;
			if(displayName == string.Empty) {
				displayName = memberName;
			}
			memberPath.RemoveAt(memberPath.Count - 1);
			this.path = memberPath;
			if(sessionProvider.Session != null && sessionProvider.Session.IsDesignMode) {
				if(propertyName.EndsWith(XPPropertyDescriptorBase.ReferenceAsKeyTail)) {
					if(targetMember.ReferenceType != null) {
						propertyType = targetMember.ReferenceType.KeyProperty.MemberType;
					}
				}
				else if(targetMember.IsAssociationList || targetMember.IsNonAssociationList) {
					propertyType = typeof(XPBindingSource);
				}
				else {
					propertyType = targetMember.MemberType;
				}
				accessor = new DesignAccessor(PropertyType, sessionProvider, (targetMember.IsAssociationList || targetMember.IsNonAssociationList) ? targetMember.CollectionElementType : targetMember.ReferenceType);
			}
			else {
				XPClassInfo targetType = memberPath.Count == 0 ? objectType : memberPath[memberPath.Count - 1].Owner;
				if(propertyName.EndsWith(XPPropertyDescriptorBase.ReferenceAsKeyTail)) {
					if(targetMember.ReferenceType == null) {
						throw new InvalidPropertyPathException(propertyName);
					}
					propertyType = targetMember.ReferenceType.KeyProperty.MemberType;
					accessor = new RelationAsKeyAccessor(targetType, targetMember, sessionProvider);
				}
				else if(targetMember.IsAssociationList) {
					propertyType = typeof(XPBindingSource);
					accessor = new XPBindingSourceAccessor(targetType, targetMember);
				}
				else {
					propertyType = targetMember.MemberType;
					accessor = new MemberAccessor(targetType, targetMember);
				}
				for(int i = memberPath.Count - 1; i >= 0; i--) {
					accessor = new RelatedMemberAccessor(i == 0 ? objectType : memberPath[i - 1].Owner, memberPath[i], accessor);
				}
			}
		}
		public override bool IsReadOnly {
			get {
				if(targetMember.IsReadOnly || propertyType == typeof(XPCollection) || propertyType == typeof(XPBindingSource) || targetMember.IsFetchOnly)
					return true;
				if(((ReadOnlyAttribute)this.Attributes[typeof(ReadOnlyAttribute)]).IsReadOnly) {
					return true;
				}
				for(int i = 0; i < path.Count; i++) {
					if(!path[i].IsAggregated) {
						return true;
					}
				}
				return false;
			}
		}
		protected override void FillAttributes(IList attributeList) {
			foreach(object att in targetMember.Attributes)
				attributeList.Add(att);
		}
		public override object GetValue(object component) {
			return accessor.GetValue(component);
		}
		public override bool CanResetValue(object component) {
			return false;
		}
		public override void ResetValue(object component) {
		}
		public override void SetValue(object component, object value) {
			accessor.SetValue(component, Convert.IsDBNull(value) ? null : value);
		}
		public override bool ShouldSerializeValue(object component) {
			return false;
		}
		public override Type PropertyType {
			get {
				if(propertyType == null) {
					return typeof(object);
				}
				return propertyType;
			}
		}
		public override Type ComponentType { get { return objectType.ClassType; } }
		public XPMemberInfo MemberInfo { get { return targetMember; } }
		public override void AddValueChanged(object component, EventHandler handler) {
			if(component == null) {
				return;
			}
			XPBaseObject.AddChangedEventHandler(component, this);
			base.AddValueChanged(component, handler);
		}
		public override void RemoveValueChanged(object component, EventHandler handler) {
			if(component == null) {
				return;
			}
			base.RemoveValueChanged(component, handler);
			XPBaseObject.RemoveChangedEventHandler(component, this);
		}
		void IObjectChange.OnObjectChanged(object sender, ObjectChangeEventArgs arg) {
			if(arg.Reason == ObjectChangeReason.Reset || (arg.Reason == ObjectChangeReason.PropertyChanged && arg.PropertyName == memberName))
				OnValueChanged(sender, EventArgs.Empty);
		}
	}
}
