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
using System.Collections.Generic;
using DevExpress.Data.Filtering;
using DevExpress.Data.Async.Helpers;
using DevExpress.Xpo.Metadata;
using System.ComponentModel;
using System.Linq;
using DevExpress.Xpo.Metadata.Helpers;
namespace DevExpress.Xpo {
	using DevExpress.Xpo.Helpers;
	using DevExpress.Data.Helpers;
	using DevExpress.Utils.Design;
	[DXToolboxItem(true)]
	[DevExpress.Utils.ToolboxTabName(AssemblyInfo.DXTabOrmComponents)]
	[DefaultEvent("ResolveSession")]
	[Designer("DevExpress.Xpo.Design.XPInstantFeedbackViewDesigner, " + AssemblyInfo.SRAssemblyXpoDesignFull, Aliases.IDesigner)]
#if !NET
	[System.Drawing.ToolboxBitmap(typeof(XPInstantFeedbackView))]
#endif
	[Description("Allows arbitrary combinations of calculated and aggregated values to be retrieved from a data store. Can serve as a data source for data-aware controls in Instant Feedback mode (working with large datasets).")]
	public class XPInstantFeedbackView: Component, IListSource, IXPClassInfoProvider, IDXCloneable {
		IServiceProvider serviceProvider;
		public XPInstantFeedbackView() { }
		public XPInstantFeedbackView(IContainer container) : this(container, null) { }
		public XPInstantFeedbackView(IServiceProvider serviceProvider) {
			this.serviceProvider = serviceProvider;
		}
		public XPInstantFeedbackView(IContainer container, IServiceProvider serviceProvider)
			: this(serviceProvider)
		{
			container.Add(this);
			this.serviceProvider = serviceProvider;
		}
		void Init(Type objectType, string assemblyName, string typeName, ServerViewProperty[] properties, CriteriaOperator fixedCriteria, EventHandler<ResolveSessionEventArgs> resolveSession, EventHandler<ResolveSessionEventArgs> dismissSession) {
			this.ObjectType = objectType;
			this._AssemblyName = assemblyName;
			this._TypeName = typeName;
			if(properties != null) {
				this.Properties.AddRangeAsCopy(properties);
			}
			this._FixedFilter = fixedCriteria;
			if(resolveSession != null)
				ResolveSession += resolveSession;
			if(dismissSession != null)
				DismissSession += dismissSession;
		}
		public XPInstantFeedbackView(Type objectType, ServerViewProperty[] properties, CriteriaOperator fixedCriteria, EventHandler<ResolveSessionEventArgs> resolveSession, EventHandler<ResolveSessionEventArgs> dismissSession) {
			Init(objectType, null, null, properties, fixedCriteria, resolveSession, dismissSession);
		}
		public XPInstantFeedbackView(string assemblyName, string typeName, ServerViewProperty[] properties, CriteriaOperator fixedCriteria, EventHandler<ResolveSessionEventArgs> resolveSession, EventHandler<ResolveSessionEventArgs> dismissSession) {
			Init(null, assemblyName, typeName, properties, fixedCriteria, resolveSession, dismissSession);
		}
		static ServerViewProperty[] FillDefaultPropertiesIfNotProvided(XPClassInfo ci, ServerViewProperty[] providedProperties) {
			if(providedProperties != null && providedProperties.Length > 0) {
				return providedProperties;
			}
			return GetDefaultProperties(ci);
		}
		static ServerViewProperty[] GetDefaultProperties(XPClassInfo ci) {
			var props = new List<ServerViewProperty>();
			var members = ci.PersistentProperties.Cast<XPMemberInfo>().Union(ci.Members.Where(t => t.IsAliased));
			foreach(XPMemberInfo mi in members) {
				if(mi.IsKey || mi is ServiceField) {
					continue;
				}
				DisplayNameAttribute dnAttribute = mi.FindAttributeInfo(typeof(DisplayNameAttribute)) as DisplayNameAttribute;
				string name = dnAttribute == null ? mi.Name : dnAttribute.DisplayName;
				props.Add(new ServerViewProperty(name, SortDirection.None, new OperandProperty(mi.Name)));
			}
			return props.ToArray();
		}
		public XPInstantFeedbackView(XPClassInfo classInfo, ServerViewProperty[] properties, CriteriaOperator fixedCriteria, EventHandler<ResolveSessionEventArgs> resolveSession, EventHandler<ResolveSessionEventArgs> dismissSession) {
			Init(classInfo.ClassType, classInfo.AssemblyName, classInfo.FullName, FillDefaultPropertiesIfNotProvided(classInfo, properties), fixedCriteria, resolveSession, dismissSession);
		}
		static EventHandler<T> ToEventHandler<T>(Action<T> action) where T : EventArgs {
			if(action == null)
				return null;
			else
				return delegate(object sender, T e) {
					action(e);
				};
		}
		public XPInstantFeedbackView(Type objectType, ServerViewProperty[] properties, CriteriaOperator fixedCriteria, Action<ResolveSessionEventArgs> resolveSession, Action<ResolveSessionEventArgs> dismissSession)
			: this(objectType, properties, fixedCriteria
			, ToEventHandler(resolveSession), ToEventHandler(dismissSession)) {
		}
		public XPInstantFeedbackView(string assemblyName, string typeName, ServerViewProperty[] properties, CriteriaOperator fixedCriteria, Action<ResolveSessionEventArgs> resolveSession, Action<ResolveSessionEventArgs> dismissSession)
			: this(assemblyName, typeName, properties, fixedCriteria
			, ToEventHandler(resolveSession), ToEventHandler(dismissSession)) {
		}
		public XPInstantFeedbackView(XPClassInfo classInfo, ServerViewProperty[] properties, CriteriaOperator fixedCriteria, Action<ResolveSessionEventArgs> resolveSession, Action<ResolveSessionEventArgs> dismissSession)
			: this(classInfo, properties, fixedCriteria
			, ToEventHandler(resolveSession), ToEventHandler(dismissSession)) {
		}
		public XPInstantFeedbackView(Type objectType, ServerViewProperty[] properties, CriteriaOperator fixedCriteria)
			: this(objectType, properties, fixedCriteria, (EventHandler<ResolveSessionEventArgs>)null, null) {
		}
		public XPInstantFeedbackView(string assemblyName, string typeName, ServerViewProperty[] properties, CriteriaOperator fixedCriteria)
			: this(assemblyName, typeName, properties, fixedCriteria, (EventHandler<ResolveSessionEventArgs>)null, null) {
		}
		public XPInstantFeedbackView(XPClassInfo classInfo, ServerViewProperty[] properties, CriteriaOperator fixedCriteria)
			: this(classInfo, properties, fixedCriteria, (EventHandler<ResolveSessionEventArgs>)null, null) {
		}
		public XPInstantFeedbackView(Type objectType)
			: this(objectType, null, null) {
		}
		public XPInstantFeedbackView(string assemblyName, string typeName)
			: this(assemblyName, typeName, null, null) {
		}
		public XPInstantFeedbackView(XPClassInfo classInfo)
			: this(classInfo, null, null) {
		}
		Type _ElementType;
		string _AssemblyName, _TypeName;
		CriteriaOperator _FixedFilter;
		public event EventHandler<ResolveSessionEventArgs> ResolveSession;
		public event EventHandler<ResolveSessionEventArgs> DismissSession;
		[Description("Gets or sets the type of items the target data table contains."), RefreshProperties(RefreshProperties.All)]
		[TypeConverter(typeof(XPInstantFeedbackSourceObjectTypeConverter))]
		[DefaultValue(null)]
		[Category("Data")]
		public Type ObjectType {
			get { return _ElementType; }
			set {
				if(ObjectType == value)
					return;
				TestCanChangeProperties();
				bool resetProperties = (_ElementType != null);
				_ElementType = value;
				if(resetProperties) {
					Properties.Clear();
					if(value != null) {
						XPClassInfo ci = GetDesignClassInfo();
						ServerViewProperty[] newProps = FillDefaultPropertiesIfNotProvided(ci, null);
						Properties.AddRange(newProps);
					}
				}
				ForceCatchUp();
			}
		}
		bool? _isDesignMode;
		protected bool IsDesignMode {
			get {
				return DevExpress.Data.Helpers.IsDesignModeHelper.GetIsDesignModeBypassable(this, ref _isDesignMode);
			}
		}
		XPDictionary GetDesignDictionary() {
			if(this.IsDesignMode)
				return XPInstantViewDesignTimeWrapper.GetDesignDictionary(this.Site);
			else
				return XpoDefault.GetDictionary();
		}
		[Description("Specifies the criteria used to filter items on the data store side. Bound data-aware controls never affect the criteria.")]
		[Editor("DevExpress.Xpo.Design.XPCollectionCriteriaEditor, " + AssemblyInfo.SRAssemblyXpoDesignFull, DevExpress.Utils.ControlConstants.UITypeEditor)]
		[TypeConverter("DevExpress.Xpo.Design.CriteriaConverter, " + AssemblyInfo.SRAssemblyXpoDesignFull)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		[Category("Data")]
		public CriteriaOperator FixedFilterCriteria {
			get { return _FixedFilter; }
			set {
				if(ReferenceEquals(FixedFilterCriteria, value))
					return;
				TestCanChangeProperties();
				_FixedFilter = value;
				ForceCatchUp();
			}
		}
		[Browsable(false)]
		[DefaultValue("")]
		public string FixedFilterString {
			get {
				return CriteriaOperator.ToString(FixedFilterCriteria);
			}
			set {
				FixedFilterCriteria = CriteriaOperator.Parse(value);
			}
		}
		ServerViewPropertiesCollection viewProperties;
		[Description("Gets a ServerViewPropertiesCollection object that contains information on a persistent type’s property names, criteria, and sort order.")]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
		[Category("View")]
		public ServerViewPropertiesCollection Properties {
			get {
				if(viewProperties == null) {
					viewProperties = new ServerViewPropertiesCollection(this);
					viewProperties.CollectionChanged += (sender, e) => {
						ForceCatchUp();
					};
				}
				return viewProperties;
			}
		}
		[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
		public bool CanChangeProperties {
			get {
				return _AsyncListServer == null;
			}
		}
		AsyncListServer2DatacontrollerProxy _AsyncListServer;
		XPInstantViewDesignTimeWrapper _DTWrapper;
		System.Collections.IList _List;
		bool IListSource.ContainsListCollection {
			get { return false; }
		}
		System.Collections.IList IListSource.GetList() {
			if(_List == null) {
				if(IsDisposed)
					throw new ObjectDisposedException(this.ToString());
				if(this.IsDesignMode) {
					_List = _DTWrapper = CreateDesignTimeWrapper();
				} else {
					_List = _AsyncListServer = GetRunTimeProxy();
			}
		}
			return _List;
		}
		XPInstantViewDesignTimeWrapper CreateDesignTimeWrapper() {
			ServerViewPropertiesCollection properties = Properties;
			if(properties.Count == 0 && ObjectType != null) {
				XPClassInfo ci = GetDesignClassInfo();
				if(ci != null) {
					properties = new ServerViewPropertiesCollection();
					properties.AddRange(GetDefaultProperties(ci));
				}
			}
			XPInstantViewDesignTimeWrapper wrapper = new XPInstantViewDesignTimeWrapper(this.Site, this.ObjectType, properties);
			return wrapper;
		}
		AsyncListServer2DatacontrollerProxy GetRunTimeProxy() {
			XPAsyncListServerCore core = CreateRuntimeCore();
			core.ListServerGet += listServerGet;
			core.ListServerFree += listServerFree;
			core.GetTypeInfo += getTypeInfo;
			core.GetPropertyDescriptors += getPropertyDescriptors;
			core.GetWorkerThreadRowInfo += getWorkerRowInfo;
			core.GetUIThreadRow += getUIRow;
			AsyncListServer2DatacontrollerProxy rv = CreateRuntimeProxy(core);
			return rv;
		}
		protected virtual AsyncListServer2DatacontrollerProxy CreateRuntimeProxy(XPAsyncListServerCore core) {
			return new AsyncListServer2DatacontrollerProxy(core);
		}
		protected virtual XPAsyncListServerCore CreateRuntimeCore() {
			return new XPAsyncListServerCore(null);
		}
		void listServerGet(object sender, ListServerGetOrFreeEventArgs e) {
			ResolveSessionEventArgs args = new ResolveSessionEventArgs();
			listServerViewGetTag tag = new listServerViewGetTag();
			tag.Args = args;
			e.Tag = tag;
			if(this.ResolveSession != null)
				this.ResolveSession(this, args);
			Session session = null;
			if(args.Session != null)
				session = args.Session.Session;
			if(session == null) {
				session = tag.OurSession = new Session(serviceProvider);
			}
			XPClassInfo ci = null;
			session.Dictionary.QueryClassInfo(ObjectType);
			if(!string.IsNullOrEmpty(_TypeName))
				ci = session.Dictionary.QueryClassInfo(_AssemblyName, _TypeName);
			if(ci == null)
				ci = session.Dictionary.QueryClassInfo(ObjectType);
			if(ci == null)
				ci = session.GetClassInfo<PersistentBase>();
			e.ListServerSource = tag.View = new XPServerModeView(session, ci, FixedFilterCriteria);
			tag.View.Properties.AddRangeAsCopy(Properties);
		}
		void listServerFree(object sender, ListServerGetOrFreeEventArgs e) {
			listServerViewGetTag tag = (listServerViewGetTag)e.Tag;
			if(tag.OurSession != null)
				tag.OurSession.Dispose();
			if(DismissSession != null)
				DismissSession(this, tag.Args);
		}
		void getTypeInfo(object sender, GetTypeInfoEventArgs e) {
#if NET
			PropertyDescriptorCollection sourceDescriptors = new Data.Browsing.DataBrowserHelper(true).GetListItemProperties(e.ListServerSource);
#else
			PropertyDescriptorCollection sourceDescriptors = System.Windows.Forms.ListBindingHelper.GetListItemProperties(e.ListServerSource);
#endif
			List <PropertyDescriptor> workers = new List<PropertyDescriptor>();
			List<PropertyDescriptor> uis = new List<PropertyDescriptor>();
			string[] properties = Array.Empty<string>();
			if(Properties.Count > 0) {
				properties = Properties.Cast<ServerViewProperty>().Select(t => t.Name).Where(t => !string.IsNullOrEmpty(t)).Distinct().ToArray();
			} else if(ObjectType != null) {
				var tmpDictionary = new ReflectionDictionary();
				XPClassInfo ci = tmpDictionary.GetClassInfo(ObjectType);
				properties = GetDefaultProperties(ci).Select(t => t.Name).ToArray();
			}
			foreach(string propName in properties) {
				PropertyDescriptor wpd = sourceDescriptors.Find(propName, false);
				PropertyDescriptor uipd = XPInstantViewDesignTimeWrapper.GetMessagingDescriptorIfUnsafe(propName, wpd);
				if(uipd == null)
					uipd = new ReadonlyThreadSafeProxyForObjectFromAnotherThreadPropertyDescriptor(wpd, workers.Count);
				uis.Add(uipd);
				if(wpd != null)
					workers.Add(wpd);
			}
			e.TypeInfo = new PropertyDescriptorCollection[] { new PropertyDescriptorCollection(uis.ToArray(), true), new PropertyDescriptorCollection(workers.ToArray(), true) };
		}
		void getPropertyDescriptors(object sender, GetPropertyDescriptorsEventArgs e) {
			e.PropertyDescriptors = ((PropertyDescriptorCollection[])e.TypeInfo)[0];
		}
		void getWorkerRowInfo(object sender, GetWorkerThreadRowInfoEventArgs e) {
			object row = e.WorkerThreadRow;
			List<object> rv = new List<object>();
			PropertyDescriptorCollection getters = ((PropertyDescriptorCollection[])e.TypeInfo)[1];
			foreach(PropertyDescriptor pd in getters)
				rv.Add(pd.GetValue(row));
			e.RowInfo = new ReadonlyThreadSafeProxyForObjectFromAnotherThread(row, rv.ToArray());
		}
		void getUIRow(object sender, GetUIThreadRowEventArgs e) {
			e.UIThreadRow = e.RowInfo;
		}
		void TestCanChangeProperties() {
			if(_AsyncListServer != null)
				throw new InvalidOperationException(Res.GetString(Res.Async_CanChangeViewProperties));
		}
		public void PopulateProperties() {
			if(ObjectType == null) {
				return;
			}
			XPClassInfo classInfo = GetDesignClassInfo();
			if(classInfo == null) {
				return;
			}
			Properties.Clear();
			Properties.AddRange(GetDefaultProperties(classInfo));
		}
		void ForceCatchUp() {
			if(_DTWrapper != null) {
				_DTWrapper.ElementType = ObjectType;
				_DTWrapper.Properties.Clear();
				_DTWrapper.Properties.AddRangeAsCopy(Properties);
			}
		}
		bool IsDisposed;
		protected override void Dispose(bool disposing) {
			IsDisposed = true;
			_List = null;
			_DTWrapper = null;
			if (_AsyncListServer != null) {
				_AsyncListServer.Dispose();
				_AsyncListServer = null;
			}
			base.Dispose(disposing);
		}
		public void Refresh() {
			if(_AsyncListServer == null)
				return;
			_AsyncListServer.Refresh();
		}
		object IDXCloneable.DXClone() {
			return DXClone();
		}
		protected virtual object DXClone() {
			XPInstantFeedbackView clone = DXCloneCreate();
			clone._AssemblyName = this._AssemblyName;
			clone.Properties.AddRangeAsCopy(this.Properties);
			clone._ElementType = this._ElementType;
			clone._FixedFilter = this._FixedFilter;
			clone._TypeName = this._TypeName;
			clone.IsDisposed = this.IsDisposed;
			clone.ResolveSession = this.ResolveSession;
			clone.DismissSession = this.DismissSession;
			return clone;
		}
		protected virtual XPInstantFeedbackView DXCloneCreate() {
			return new XPInstantFeedbackView(serviceProvider);
		}
		[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
		public object ExtractOriginalRow(object uiThreadRow) {
			return ReadonlyThreadSafeProxyForObjectFromAnotherThread.ExtractOriginalRow(uiThreadRow);
		}
		XPClassInfo GetDesignClassInfo() {
			XPDictionary dictionary = GetDesignDictionary();
			dictionary.QueryClassInfo(ObjectType);
			if(!string.IsNullOrEmpty(_TypeName)) {
				var ci = dictionary.QueryClassInfo(_AssemblyName, _TypeName);
				if(ci != null)
					return ci;
			}
			return dictionary.QueryClassInfo(ObjectType);
		}
		XPClassInfo IXPClassInfoProvider.ClassInfo {
			get { return GetDesignClassInfo(); }
		}
		XPDictionary Metadata.Helpers.IXPDictionaryProvider.Dictionary {
			get { return GetDesignDictionary(); }
		}
	}
}
namespace DevExpress.Xpo.Helpers {
	public class XPInstantViewDesignTimeWrapper
		: IBindingList, ITypedList 
	{
		readonly DefaultSession DesignSession;
		public XPInstantViewDesignTimeWrapper(ISite site, Type type, ServerViewPropertiesCollection properties) {
			this.DesignSession = new DefaultSession(site);
			this.Site = site;
			this._ElementType = type;
			this.Properties.AddRangeAsCopy(properties);
		}
		readonly ISite Site;
		Type _ElementType;
		PropertyDescriptorCollection _Descriptors;
		public Type ElementType {
			get { return _ElementType; }
			set {
				if(ElementType == value)
					return;
				_ElementType = value;
				InvalidateDescriptors();
			}
		}
		ServerViewPropertiesCollection viewProperties;		
		public ServerViewPropertiesCollection Properties {
			get {
				if(viewProperties == null) {
					viewProperties = new ServerViewPropertiesCollection();
					viewProperties.CollectionChanged += (sender, e) => {
						InvalidateDescriptors();
					};
				}
				return viewProperties;
			}
		}
		void InvalidateDescriptors() {
			if(_Descriptors != null) {
				_Descriptors = null;
				if(ListChanged != null) {
					ListChanged(this, new ListChangedEventArgs(ListChangedType.PropertyDescriptorChanged, -1));
				}
			}
		}
		public static XPInstantPropertyDescriptorJustMessage GetMessagingDescriptorIfUnsafe(string name, PropertyDescriptor prototype) {
			if(prototype == null)
				return new XPInstantPropertyDescriptorJustMessage(name, string.Format("'{0}' member does not exist", name));
			if(ReflectionDictionary.DefaultCanGetClassInfoByType(prototype.PropertyType) || (typeof(System.Collections.IEnumerable).IsAssignableFrom(prototype.PropertyType) && !DevExpress.Xpo.DB.DBColumn.IsStorableType(prototype.PropertyType)))
				return new XPInstantPropertyDescriptorJustMessage(name, string.Format("'{0}' member is not safe", name));
			return null;
		}
		PropertyDescriptorCollection GetDescriptors() {
			return GetDescriptorsCore() ?? PropertyDescriptorCollection.Empty;
		}
		PropertyDescriptorCollection GetDescriptorsCore() {
			if(ElementType == null || Properties.Count == 0) {
				return null;
			}			
			XPClassInfo ci = DesignSession.Dictionary.QueryClassInfo(ElementType);
			if(ci == null) {
				return null;
			}
			List<PropertyDescriptor> rv = new List<PropertyDescriptor>();
			for(int i = 0; i < Properties.Count; i++) {
				if(string.IsNullOrEmpty(Properties[i].Name)) {
					continue;
				}
				PropertyDescriptor prototype = null;
				try {
					Type propertyType = CriteriaTypeResolver.ResolveType(ci, Properties[i].Property);
					prototype = new XpoViewServerModePropertyDescriptor(Properties[i].Name, propertyType, i);
				} catch { }
				PropertyDescriptor pd = GetMessagingDescriptorIfUnsafe(Properties[i].Name, prototype);
				if(pd == null) {
					pd = prototype;
				}
				rv.Add(pd);
			}
			return new PropertyDescriptorCollection(rv.ToArray(), true);
		}
		public event ListChangedEventHandler ListChanged;
		PropertyDescriptorCollection ITypedList.GetItemProperties(PropertyDescriptor[] listAccessors) {
			if(_Descriptors == null) {
				_Descriptors = GetDescriptors();
			}
			return _Descriptors;
		}
		string ITypedList.GetListName(PropertyDescriptor[] listAccessors) {
			return string.Empty;
		}
#region IBindingList Members
		void IBindingList.AddIndex(PropertyDescriptor property) {
		}
		object IBindingList.AddNew() {
			throw new NotSupportedException();
		}
		bool IBindingList.AllowEdit {
			get { return false; }
		}
		bool IBindingList.AllowNew {
			get { return false; }
		}
		bool IBindingList.AllowRemove {
			get { return false; }
		}
		void IBindingList.ApplySort(PropertyDescriptor property, ListSortDirection direction) {
		}
		int IBindingList.Find(PropertyDescriptor property, object key) {
			return -1;
		}
		bool IBindingList.IsSorted {
			get { return false; }
		}
		void IBindingList.RemoveIndex(PropertyDescriptor property) {
		}
		void IBindingList.RemoveSort() {
		}
		ListSortDirection IBindingList.SortDirection {
			get {
				throw new NotSupportedException();
			}
		}
		PropertyDescriptor IBindingList.SortProperty {
			get { throw new NotSupportedException(); }
		}
		bool IBindingList.SupportsChangeNotification {
			get { return true; }
		}
		bool IBindingList.SupportsSearching {
			get { return false; }
		}
		bool IBindingList.SupportsSorting {
			get { return false; }
		}
#endregion
#region IList Members
		int System.Collections.IList.Add(object value) {
			throw new NotSupportedException();
		}
		void System.Collections.IList.Clear() {
			throw new NotSupportedException();
		}
		bool System.Collections.IList.Contains(object value) {
			return false;
		}
		int System.Collections.IList.IndexOf(object value) {
			return -1;
		}
		void System.Collections.IList.Insert(int index, object value) {
			throw new NotSupportedException();
		}
		bool System.Collections.IList.IsFixedSize {
			get { return true; }
		}
		bool System.Collections.IList.IsReadOnly {
			get { return true; }
		}
		void System.Collections.IList.Remove(object value) {
			throw new NotSupportedException();
		}
		void System.Collections.IList.RemoveAt(int index) {
			throw new NotSupportedException();
		}
		object System.Collections.IList.this[int index] {
			get {
				return null;
			}
			set {
				throw new NotSupportedException();
			}
		}
#endregion
#region ICollection Members
		void System.Collections.ICollection.CopyTo(Array array, int index) {
		}
		int System.Collections.ICollection.Count {
			get { return 0; }
		}
		bool System.Collections.ICollection.IsSynchronized {
			get { return false; }
		}
		object System.Collections.ICollection.SyncRoot {
			get { return this; }
		}
#endregion
#region IEnumerable Members
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
			return string.Empty.GetEnumerator();
		}
#endregion
		public static XPDictionary GetDesignDictionary(ISite site) {
			return new DesignTimeReflection(site);
		}
	}
	public class listServerViewGetTag {
		public ResolveSessionEventArgs Args;
		public Session OurSession;
		public XPServerModeView View;
	}
}
