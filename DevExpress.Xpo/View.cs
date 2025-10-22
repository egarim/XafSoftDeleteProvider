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
using System.ComponentModel.Design.Serialization;
using System.Globalization;
using System.Reflection;
using DevExpress.Data;
using DevExpress.Data.Filtering;
using DevExpress.Data.Filtering.Helpers;
using DevExpress.Utils.Design;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.Helpers;
using DevExpress.Xpo.Metadata;
using DevExpress.Xpo.Metadata.Helpers;
namespace DevExpress.Xpo {
	public sealed class ViewRecord : ICustomTypeDescriptor {
		XPView view;
		object[] data;
		internal object[] Data { get { return data; } }
		[Description("Gets the view to which this record belongs.")]
		public XPView View {
			get { return view; }
		}
		public object this[int index] {
			get {
				return View.DisplayProps[index].GetValue(this);
			}
		}
		public object this[string name] {
			get {
				return View.DisplayProps.Find(name, true).GetValue(this);
			}
		}
		public object this[CriteriaOperator property] {
			get {
				ViewProperty prop = View.Properties[property];
				if(prop == null || !prop.Fetch)
					throw new ArgumentException(Res.GetString(Res.View_PropertyNotFetched, CriteriaOperator.ToString(property)));
				return this[prop.Name];
			}
		}
		public ViewRecord(XPView view, object[] data) {
			this.view = view;
			this.data = data;
		}
		public object GetObject() {
			object keyValue;
			XPMemberInfo keyProperty = View.ObjectClassInfo.KeyProperty;
			if(keyProperty.SubMembers.Count == 0) {
				keyValue = this[new OperandProperty(keyProperty.Name)];
			}
			else {
				IdList listKey = new IdList();
				foreach(XPMemberInfo subKey in keyProperty.SubMembers) {
					if(subKey.IsPersistent) {
						listKey.Add(this[new OperandProperty(subKey.Name)]);
					}
				}
				keyValue = listKey;
			}
			return View.Session.GetObjectByKey(View.ObjectClassInfo, keyValue);
		}
		#region ICustomTypeDescriptor Members
		AttributeCollection ICustomTypeDescriptor.GetAttributes() {
			return AttributeCollection.Empty;
		}
		string ICustomTypeDescriptor.GetClassName() {
			return View.ObjectClassInfo.FullName;
		}
		string ICustomTypeDescriptor.GetComponentName() {
			return View.ObjectClassInfo.FullName;
		}
		TypeConverter ICustomTypeDescriptor.GetConverter() {
			return null;
		}
		object ICustomTypeDescriptor.GetEditor(Type editorBaseType) {
			return null;
		}
		PropertyDescriptor ICustomTypeDescriptor.GetDefaultProperty() {
			return null;
		}
		EventDescriptor ICustomTypeDescriptor.GetDefaultEvent() {
			return null;
		}
		EventDescriptorCollection ICustomTypeDescriptor.GetEvents(Attribute[] attributes) {
			return EventDescriptorCollection.Empty;
		}
		EventDescriptorCollection ICustomTypeDescriptor.GetEvents() {
			return EventDescriptorCollection.Empty;
		}
		PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties(Attribute[] attributes) {
			return View.DisplayProps;
		}
		PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties() {
			return View.DisplayProps;
		}
		object ICustomTypeDescriptor.GetPropertyOwner(PropertyDescriptor pd) {
			return this;
		}
		#endregion
	}
	sealed class ViewPropertyDescriptor : PropertyDescriptor {
		readonly int index;
		readonly Type reportedType;
		public ViewPropertyDescriptor(string name, Type type, int index)
			: base(name, Array.Empty<Attribute>()) {
			this.index = index;
			this.reportedType = Nullable.GetUnderlyingType(type);
			if(reportedType == null)
				reportedType = type;
		}
		public override bool IsReadOnly { get { return true; } }
		public override object GetValue(object component) {
			return ((ViewRecord)component).Data[index];
		}
		public override bool CanResetValue(object component) {
			return false;
		}
		public override void ResetValue(object component) {
		}
		public override void SetValue(object component, object value) {
			throw new NotSupportedException();
		}
		public override bool ShouldSerializeValue(object component) {
			return false;
		}
		public override Type PropertyType {
			get {
				return reportedType;
			}
		}
		public override Type ComponentType { get { return typeof(object[]); } }
	}
	public enum SortDirection { None, Ascending, Descending }
	[TypeConverter(typeof(ViewPropertyConverter))]
	public class ViewProperty : IDataColumnInfoProvider {
		string name;
		SortDirection sorting;
		CriteriaOperator property;
		bool group;
		bool fetch;
		XPView owner;
		protected internal void SetOwner(XPView owner) {
			this.owner = owner;
		}
		void ResetView() {
			if(owner != null) {
				owner.ClearProps();
			}
		}
		[Description("Gets or sets the property’s name.")]
		[DefaultValue("")]
		[Category("Data")]
		public string Name {
			get { return name; }
			set { name = value; ResetView(); }
		}
		[Description("Gets or sets the column’s sort order."), DefaultValue(SortDirection.None)]
		[Category("Data")]
		public SortDirection Sorting {
			get { return sorting; }
			set { sorting = value; }
		}
		[Description("Gets or sets the expression used to filter rows, calculate the values in a column, or create an aggregate column.")]
		[TypeConverter("DevExpress.Xpo.Design.CriteriaConverter, " + AssemblyInfo.SRAssemblyXpoDesignFull)]
		[Editor("DevExpress.Xpo.Design.XPViewExpressionEditor, " + AssemblyInfo.SRAssemblyXpoDesignFull, DevExpress.Utils.ControlConstants.UITypeEditor)]
		[Category("Data")]
		public CriteriaOperator Property {
			get { return property; }
			set { property = value; ResetView(); }
		}
		[Description("Gets or sets whether the view is grouped by the values of this property (column)."), DefaultValue(false)]
		[Category("Data")]
		public bool Group {
			get { return group; }
			set { group = value; }
		}
		[Description("Gets or sets whether to retrieve data for the property from a data store."), DefaultValue(true)]
		[Category("Data")]
		public bool Fetch {
			get { return fetch; }
			set { fetch = value; ResetView(); }
		}
		bool ShouldSerializeProperty() {
			return !ReferenceEquals(property, null);
		}
		public ViewProperty() {
			sorting = SortDirection.None;
			fetch = true;
			name = string.Empty;
		}
		public ViewProperty(string name, SortDirection sorting, CriteriaOperator property, bool group, bool fetch) {
			this.name = name;
			this.sorting = sorting;
			this.property = property;
			this.group = group;
			this.fetch = fetch;
		}
		public ViewProperty(string name, SortDirection sorting, string property, bool group, bool fetch)
			: this(name, sorting, CriteriaOperator.Parse(property), group, fetch) {
		}
		#region IDataColumnInfoProvider Members
		IDataColumnInfo IDataColumnInfoProvider.GetInfo(object arguments) {
			return new ViewPropertyIDataPropertyInfoWrapper(this);
		}
		#endregion
		internal class ViewPropertyMemberIDataPropertyInfoWrapper : IDataColumnInfo {
			readonly XPMemberInfo memberInfo;
			readonly List<IDataColumnInfo> columns = new List<IDataColumnInfo>(0);
			public ViewPropertyMemberIDataPropertyInfoWrapper(XPMemberInfo memberInfo) {
				this.memberInfo = memberInfo;
			}
			public string Caption { get { return string.IsNullOrEmpty(memberInfo.DisplayName) ? memberInfo.Name : memberInfo.DisplayName; } }
			public List<IDataColumnInfo> Columns { get { return columns; } }
			public DataControllerBase Controller { get { return null; } }
			public string FieldName { get { return memberInfo.Name; } }
			public Type FieldType { get { return memberInfo.MemberType; } }
			public string Name { get { return memberInfo.Name; } }
			public string UnboundExpression { get { return memberInfo.Name; } }
		}
		internal class ViewPropertyIDataPropertyInfoWrapper : IDataColumnInfo {
			readonly ViewProperty column;
			public ViewPropertyIDataPropertyInfoWrapper(ViewProperty column) {
				this.column = column;
			}
			string IDataColumnInfo.Caption { get { return column.Name; } }
			List<IDataColumnInfo> IDataColumnInfo.Columns {
				get {
					List<IDataColumnInfo> res = new List<IDataColumnInfo>();
					if(column.owner == null || column.owner.ObjectClassInfo == null) return res;
					foreach(XPMemberInfo mi in column.owner.ObjectClassInfo.PersistentProperties) {
						res.Add(new ViewPropertyMemberIDataPropertyInfoWrapper(mi));
					}
					foreach(XPMemberInfo miCol in column.owner.ObjectClassInfo.CollectionProperties) {
						res.Add(new ViewPropertyMemberIDataPropertyInfoWrapper(miCol));
					}
					return res;
				}
			}
			DataControllerBase IDataColumnInfo.Controller { get { return null; } }
			string IDataColumnInfo.FieldName { get { return ReferenceEquals(column.Property, null) ? "" : column.Property.ToString(); } }
			Type IDataColumnInfo.FieldType {
				get {
					if(column.owner == null || column.owner.ObjectClassInfo == null) return typeof(object);
					return CriteriaTypeResolver.ResolveType(column.owner.ObjectClassInfo, column.Property);
				}
			}
			string IDataColumnInfo.Name { get { return column.Name; } }
			string IDataColumnInfo.UnboundExpression { get { return ReferenceEquals(column.Property, null) ? "" : column.Property.ToString(); } }
		}
	}
	class ViewPropertyConverter : TypeConverter {
		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType) {
			if(destinationType == typeof(InstanceDescriptor))
				return true;
			return base.CanConvertTo(context, destinationType);
		}
		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object val, Type destinationType) {
			ViewProperty prop = val as ViewProperty;
			if(destinationType == typeof(InstanceDescriptor) && prop != null) {
				ConstructorInfo ctor = typeof(ViewProperty).GetConstructor(new Type[] { typeof(string), typeof(SortDirection), typeof(string), typeof(bool), typeof(bool) });
				if(ctor != null) {
					return new InstanceDescriptor(ctor, new object[] { prop.Name, prop.Sorting, ReferenceEquals(prop.Property, null) ? String.Empty : prop.Property.ToString(), prop.Group, prop.Fetch });
				}
			}
			return base.ConvertTo(context, culture, val, destinationType);
		}
	}
	[ListBindable(BindableSupport.No)]
	[Editor("DevExpress.Xpo.Design.ViewPropertiesCollectionEditor," + AssemblyInfo.SRAssemblyXpoDesignFull, DevExpress.Utils.ControlConstants.UITypeEditor)]
	public sealed class ViewPropertiesCollection : CollectionBase {
		XPView owner = null;
		public ViewPropertiesCollection(XPView owner)
			: base() {
			this.owner = owner;
		}
		public void Add(ViewProperty sortProperty) {
			((IList)this).Add(sortProperty);
		}
		public void AddRange(ViewProperty[] sortProperties) {
			foreach(ViewProperty sp in sortProperties)
				Add(sp);
		}
		public void Add(ViewPropertiesCollection sortProperties) {
			foreach(ViewProperty sp in sortProperties)
				Add(sp);
		}
		protected override void OnInsertComplete(int index, object value) {
			((ViewProperty)value).SetOwner(owner);
			if(owner != null)
				owner.ClearProps();
		}
		protected override void OnRemoveComplete(int index, object value) {
			((ViewProperty)value).SetOwner(null);
			if(owner != null)
				owner.ClearProps();
		}
		public ViewProperty this[int index] { get { return (ViewProperty)List[index]; } }
		public ViewProperty this[CriteriaOperator expression] {
			get {
				foreach(ViewProperty prop in this) {
					if(Equals(expression, prop.Property))
						return prop;
				}
				return null;
			}
		}
		public ViewProperty this[string name] {
			get {
				foreach(ViewProperty prop in this) {
					if(Equals(name, prop.Name))
						return prop;
				}
				return null;
			}
		}
	}
	[DXToolboxItem(true)]
	[DevExpress.Utils.ToolboxTabName(AssemblyInfo.DXTabOrmComponents)]
	[DesignerSerializer("DevExpress.Xpo.Design.XPViewSerializer, " + AssemblyInfo.SRAssemblyXpoDesignFull, Aliases.CodeDomSerializer)]
	[Designer("DevExpress.Xpo.Design.XPViewDesigner, " + AssemblyInfo.SRAssemblyXpoDesignFull, Aliases.IDesigner)]
	[DefaultProperty("ObjectClassInfo")]
	[Description("Allows arbitrary combinations of calculated and aggregated values to be retrieved from a data store. Can serve as a data source for data-aware controls.")]
#if !NET
	[System.Drawing.ToolboxBitmap(typeof(XPView))]
#endif
	public class XPView : Component, ISupportInitialize, IBindingList, ITypedList, IFilteredXtraBindingList, IXPClassInfoAndSessionProvider {
		bool selectDeleted;
		Session session;
		XPClassInfo info;
		List<object> objects;
		List<object> Objects {
			get {
				if(Session.IsDesignMode)
					return GetSampleData();
				if(isInAsyncLoading) {
					if(objects == null)
						return new List<object>(0);
					return Sorted;
				}
				if(objects == null)
					objects = Load();
				return Sorted;
			}
		}
		List<object> GetSampleData() {
			if(ObjectClassInfo == null)
				return new List<object>(0);
			List<object> list = new List<object>();
			list.Add(new ViewRecord(this, new object[DisplayProps.Count]));
			return list;
		}
		internal void Clear() {
			objects = null;
		}
		internal void ClearProps() {
			Clear();
			displayProps = null;
		}
		bool Initializing;
		bool? caseSensitive;
		bool hasChangesDuringInit;
		void ISupportInitialize.BeginInit() {
			Initializing = true;
			hasChangesDuringInit = false;
		}
		void ISupportInitialize.EndInit() {
			ClearProps();
			Initializing = false;
			if(hasChangesDuringInit)
				Reset(true);
			hasChangesDuringInit = false;
		}
		IComparer CreateComparer() {
			return XPCollectionCompareHelper.CreateComparer(Sorting, new CriteriaCompiledContextDescriptorDescripted(DisplayProps), new CriteriaCompilerAuxSettings(CaseSensitive, Session.Dictionary.CustomFunctionOperators));
		}
		[Description("Gets or sets whether string comparisons evaluated by the XPView on the client are case-sensitive.")]
		[Category("Options")]
		public bool CaseSensitive {
			get {
				return caseSensitive.HasValue ? caseSensitive.Value : Session.CaseSensitive;
			}
			set {
				caseSensitive = value;
				Reset(false);
			}
		}
		List<object> Sorted {
			get {
				if(Sorting.Count == 0)
					return Filtered;
				if(sorted == null) {
					sorted = Filtered;
					sorted.Sort(new XPViewComparerWrapper(CreateComparer()));
				}
				return sorted;
			}
		}
		class XPViewComparerWrapper : IComparer<object> {
			IComparer comparer;
			public XPViewComparerWrapper(IComparer comparer) {
				this.comparer = comparer;
			}
			public int Compare(object x, object y) {
				return comparer.Compare(x, y);
			}
		}
		List<object> Filtered {
			get {
				if(ReferenceEquals(filter, null))
					return objects;
				if(filtered == null) {
					filtered = new List<object>();
					int count = objects.Count;
					for(int i = 0; i < count; i++) {
						object obj = objects[i];
						if(fitPredicate(obj))
							filtered.Add(obj);
					}
				}
				return filtered;
			}
		}
		bool isInAsyncLoading;
		public bool LoadAsync() {
			return LoadAsync(null);
		}
		public bool LoadAsync(AsyncLoadObjectsCallback callback) {
			if(isInAsyncLoading) return false;
			isInAsyncLoading = true;
			try {
				CriteriaOperatorCollection properties;
				CriteriaOperatorCollection groupProperties;
				SortingCollection sorting;
				PrepareLoad(out properties, out groupProperties, out sorting);
				Session.SelectDataAsync(ObjectClassInfo, properties, Criteria, groupProperties, GroupCriteria, SelectDeleted, SkipReturnedRecords, TopReturnedRecords, sorting
					, new AsyncSelectDataCallback(delegate (List<object[]> data, Exception ex) {
						isInAsyncLoading = false;
						if(ex == null) {
							List<object> list = new List<object>();
							foreach(object[] rec in data)
								list.Add(new ViewRecord(this, rec));
							objects = list;
							Reset(false);
							if(callback != null) {
								callback(new ICollection[] { this }, null);
							}
						}
						else {
							objects = new List<object>(0);
							Reset(false);
							if(callback != null) {
								callback(null, ex);
							}
						}
					}));
				return true;
			}
			catch {
				isInAsyncLoading = false;
				throw;
			}
		}
		List<object> Load() {
			CriteriaOperatorCollection properties;
			CriteriaOperatorCollection groupProperties;
			SortingCollection sorting;
			PrepareLoad(out properties, out groupProperties, out sorting);
			return EndLoad(properties, groupProperties, sorting);
		}
		void PrepareLoad(out CriteriaOperatorCollection properties, out CriteriaOperatorCollection groupProperties, out SortingCollection sorting) {
			properties = new CriteriaOperatorCollection();
			groupProperties = new CriteriaOperatorCollection();
			sorting = new SortingCollection();
			foreach(ViewProperty prop in Properties) {
				if(prop.Fetch)
					properties.Add(prop.Property);
				if(prop.Group)
					groupProperties.Add(prop.Property);
				if(prop.Sorting != SortDirection.None)
					sorting.Add(new SortProperty(prop.Property, prop.Sorting == SortDirection.Ascending ? SortingDirection.Ascending : SortingDirection.Descending));
			}
			if(properties.Count == 0)
				throw new InvalidOperationException(Res.GetString(Res.View_AtLeastOneFetchPropertyShouldBeDefined));
		}
		List<object> EndLoad(CriteriaOperatorCollection properties, CriteriaOperatorCollection groupProperties, SortingCollection sorting) {
			List<object[]> data;
			data = Session.SelectData(ObjectClassInfo, properties, Criteria, groupProperties, GroupCriteria, SelectDeleted, SkipReturnedRecords, TopReturnedRecords, sorting);
			List<object> list = new List<object>();
			foreach(object[] rec in data)
				list.Add(new ViewRecord(this, rec));
			return list;
		}
		public void Reload() {
			if(isInAsyncLoading) throw new InvalidOperationException(Res.GetString(Res.View_View_IsInLoading));
			Clear();
			Reset(false);
		}
		public ViewRecord this[int index] {
			get {
				return (ViewRecord)Objects[index];
			}
		}
		[Description("Gets or sets the session which is used to load and save persistent objects.")]
		[TypeConverter("DevExpress.Xpo.Design.SessionReferenceConverter, " + AssemblyInfo.SRAssemblyXpoDesignFull)]
		[Category("Data")]
		public Session Session {
			get {
				if(session == null) {
					session = DoResolveSession();
				}
				return session;
			}
			set {
				if(session != value) {
					session = value;
					if(info != null) {
						objectType = info.ClassType;
						info = null;
					}
					Clear();
					Reset(true);
				}
			}
		}
		bool ShouldSerializeSession() {
			return session != null && !(session is DefaultSession);
		}
		void ResetSession() {
			Session = null;
		}
		bool ShouldSerializeCaseSensitive() {
			return caseSensitive.HasValue;
		}
		void ResetCaseSensitive() {
			caseSensitive = null;
		}
		bool? _isDesignMode;
		protected bool IsDesignMode {
			get {
				return DevExpress.Data.Helpers.IsDesignModeHelper.GetIsDesignModeBypassable(this, ref _isDesignMode);
			}
		}
		Session DoResolveSession() {
			if(IsDesignMode) {
				return new DevExpress.Xpo.Helpers.DefaultSession(this.Site);
			}
			ResolveSessionEventArgs args = new ResolveSessionEventArgs();
			OnResolveSession(args);
			if(args.Session != null) {
				Session resolved = args.Session.Session;
				if(resolved != null) {
					return resolved;
				}
			}
			return XpoDefault.GetSession();
		}
		protected virtual void OnResolveSession(ResolveSessionEventArgs args) {
			if(Events[EventResolveSession] != null)
				((ResolveSessionEventHandler)Events[EventResolveSession])(this, args);
		}
		static readonly object EventResolveSession = new object();
		public event ResolveSessionEventHandler ResolveSession {
			add {
				Events.AddHandler(EventResolveSession, value);
			}
			remove {
				Events.RemoveHandler(EventResolveSession, value);
			}
		}
		XPDictionary designDictionary;
		internal XPDictionary DesignDictionary {
			get {
				if(designDictionary == null) {
					if(!IsDesignMode)
						throw new InvalidOperationException();
					designDictionary = new DesignTimeReflection(Site);
				}
				return designDictionary;
			}
		}
		[Description("Gets the metadata information for the persistent objects retrieved by the view.")]
		[TypeConverter("DevExpress.Xpo.Design.ObjectClassInfoTypeConverter, " + AssemblyInfo.SRAssemblyXpoDesignFull)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		[Category("Data")]
		public XPClassInfo ObjectClassInfo {
			get {
				if(info == null && objectType != null && Session != null) {
					info = IsDesignMode ? DesignDictionary.GetClassInfo(objectType) : Session.GetClassInfo(objectType);
				}
				return info;
			}
			set {
				if(!this.IsDesignMode && !this.Initializing)
					throw new NotSupportedException(Res.GetString(Res.Collections_CannotAssignProperty, "ObjectClassInfo", GetType().Name));
				if(info != value) {
					info = value;
					objectType = null;
					if(!this.Initializing) {
						ClearProps();
					}
					Reset(true);
				}
			}
		}
		Type objectType;
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
		[DefaultValue(null)]
		public Type ObjectType {
			get { return ObjectClassInfo != null ? ObjectClassInfo.ClassType : null; }
			set {
				if(!this.IsDesignMode && !this.Initializing)
					throw new NotSupportedException(Res.GetString(Res.Collections_CannotAssignProperty, "ObjectType", GetType().Name));
				objectType = value;
				Reset(true);
			}
		}
		ViewPropertiesCollection props;
		[Description("Gets a collection of ViewProperty objects that represent view columns.")]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
		[Category("View")]
		public ViewPropertiesCollection Properties {
			get {
				return props;
			}
		}
		Func<object, bool> fitPredicate;
		CriteriaOperator filter;
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public CriteriaOperator Filter {
			get { return filter; }
			set {
				if(ReferenceEquals(filter, value))
					return;
				fitPredicate = CriteriaCompiler.ToUntypedPredicate(value, CriteriaCompilerDescriptor.Get(((ITypedList)this).GetItemProperties(null)), new CriteriaCompilerAuxSettings(CaseSensitive, Session.Dictionary.CustomFunctionOperators));
				filter = value;
				Reset(false);
			}
		}
		int skipReturnedRecords;
		[Description("Gets or sets the number of records to exclude when populating the view."), DefaultValue(0)]
		[Category("Data")]
		public int SkipReturnedRecords {
			get { return skipReturnedRecords; }
			set { skipReturnedRecords = value; }
		}
		int topReturnedRecords;
		[Description("Gets or sets the maximum number of records retrieved by the view."), DefaultValue(0)]
		[Category("Data")]
		public int TopReturnedRecords {
			get { return topReturnedRecords; }
			set { topReturnedRecords = value; }
		}
		List<object> sorted;
		List<object> filtered;
		internal void Reset(bool metadataChanged) {
			filtered = null;
			sorted = null;
			if(Initializing)
				hasChangesDuringInit = true;
			else
				if(listChanged != null) {
				if(metadataChanged)
					listChanged(this, new ListChangedEventArgs(ListChangedType.PropertyDescriptorChanged, null));
				listChanged(this, new ListChangedEventArgs(ListChangedType.Reset, -1));
			}
		}
		SortingCollection sorting;
		[Description("Provides access to the collection whose elements identify the sorted columns within the view.")]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
		[Editor("DevExpress.Xpo.Design.XPViewSortingCollectionEditor, " + AssemblyInfo.SRAssemblyXpoDesignFull, DevExpress.Utils.ControlConstants.UITypeEditor)]
		[Category("Data")]
		public SortingCollection Sorting {
			get {
				if(sorting == null)
					sorting = new SortingCollection();
				return sorting;
			}
			set {
				sorting = value;
				Reset(false);
			}
		}
		bool ShouldSerializeSorting() {
			return sorting != null;
		}
		void ResetSorting() {
			sorting = null;
			Reset(false);
		}
		CriteriaOperator criteria;
		[Description("Gets or sets the criteria associated with the view.")]
		[TypeConverter("DevExpress.Xpo.Design.CriteriaConverter, " + AssemblyInfo.SRAssemblyXpoDesignFull)]
		[Editor("DevExpress.Xpo.Design.XPViewCriteriaEditor, " + AssemblyInfo.SRAssemblyXpoDesignFull, DevExpress.Utils.ControlConstants.UITypeEditor)]
		[DefaultValue(null)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		[Category("Data")]
		public CriteriaOperator Criteria {
			get {
				return criteria;
			}
			set {
				criteria = value;
				Reload();
			}
		}
		[Browsable(false)]
		[DefaultValue(null)]
		public string CriteriaString {
			get {
				CriteriaOperator criteria = Criteria;
				return (object)criteria == null ? null : criteria.ToString();
			}
			set {
				if(CriteriaString != value)
					Criteria = CriteriaOperator.Parse(value);
			}
		}
		[Description("Specifies whether objects marked as deleted are retrieved by the XPView."), DefaultValue(false)]
		[Category("Options")]
		public bool SelectDeleted {
			get { return selectDeleted; }
			set {
				if(selectDeleted != value) {
					selectDeleted = value;
					Reload();
				}
			}
		}
		CriteriaOperator groupCriteria;
		[Description("Gets or sets the grouping criteria which is associated with the view.")]
		[TypeConverter("DevExpress.Xpo.Design.CriteriaConverter, " + AssemblyInfo.SRAssemblyXpoDesignFull)]
		[Editor("DevExpress.Xpo.Design.XPViewCriteriaEditor, " + AssemblyInfo.SRAssemblyXpoDesignFull, DevExpress.Utils.ControlConstants.UITypeEditor)]
		[DefaultValue(null)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		[Category("Data")]
		public CriteriaOperator GroupCriteria {
			get {
				return groupCriteria;
			}
			set {
				groupCriteria = value;
				Reload();
			}
		}
		[Browsable(false)]
		[DefaultValue(null)]
		public string GroupCriteriaString {
			get {
				CriteriaOperator criteria = GroupCriteria;
				return (object)criteria == null ? null : criteria.ToString();
			}
			set {
				if(GroupCriteriaString != value)
					GroupCriteria = CriteriaOperator.Parse(value);
			}
		}
		public XPView() {
			props = new ViewPropertiesCollection(this);
		}
		public XPView(IContainer container)
			: this() {
			container.Add(this);
		}
		public XPView(Session session, XPClassInfo info, CriteriaOperatorCollection properties, CriteriaOperator criteria)
			: this(session, info, properties, criteria, null) {
		}
		public XPView(Session session, XPClassInfo info, CriteriaOperatorCollection properties, CriteriaOperator criteria, CriteriaOperator groupCriteria)
			: this() {
			this.session = session;
			this.info = info;
			if(properties != null) {
				foreach(CriteriaOperator prop in properties) {
					AddProperty(prop);
				}
			}
			this.criteria = criteria;
			this.groupCriteria = groupCriteria;
		}
		static CriteriaOperatorCollection GetProperties(XPClassInfo info, string properties) {
			CriteriaOperatorCollection res = new CriteriaOperatorCollection();
			if(properties != null) {
				CriteriaOperator[] propertyList = CriteriaOperator.ParseList(properties);
				for(int i = 0; i < propertyList.Length; i++)
					res.Add(propertyList[i]);
			}
			return res;
		}
		public XPView(Session session, Type objType)
			: this(session, session.GetClassInfo(objType)) {
		}
		public XPView(Session session, XPClassInfo info)
			: this(session, info, String.Empty, null) {
		}
		public XPView(Session session, Type objType, CriteriaOperatorCollection properties, CriteriaOperator criteria)
			: this(session, session.GetClassInfo(objType), properties, criteria) {
		}
		public XPView(Session session, Type objType, string properties, CriteriaOperator criteria)
			: this(session, session.GetClassInfo(objType), properties, criteria) {
		}
		public XPView(Session session, XPClassInfo info, string properties, CriteriaOperator criteria)
			: this(session, info, GetProperties(info, properties), criteria) {
		}
		public ViewProperty AddProperty(CriteriaOperator property) {
			string name;
			if(property is OperandProperty)
				name = ((OperandProperty)property).PropertyName;
			else
				name = "Column" + (Properties.Count + 1).ToString(CultureInfo.InvariantCulture);
			return AddProperty(name, property);
		}
		public ViewProperty AddProperty(string property) {
			return AddProperty(CriteriaOperator.Parse(property));
		}
		public ViewProperty AddProperty(string name, string property) {
			return AddProperty(name, property, false);
		}
		public ViewProperty AddProperty(string name, CriteriaOperator property) {
			return AddProperty(name, property, false);
		}
		public ViewProperty AddProperty(string name, string property, bool group) {
			return AddProperty(name, CriteriaOperator.Parse(property), group, true, SortDirection.None);
		}
		public ViewProperty AddProperty(string name, CriteriaOperator property, bool group) {
			return AddProperty(name, property, group, true, SortDirection.None);
		}
		public ViewProperty AddProperty(string name, string property, bool group, bool fetch, SortDirection sorting) {
			return AddProperty(name, CriteriaOperator.Parse(property), group, fetch, sorting);
		}
		public ViewProperty AddProperty(string name, CriteriaOperator property, bool group, bool fetch, SortDirection sorting) {
			ViewProperty prop = new ViewProperty(name, sorting, property, group, fetch);
			Properties.Add(prop);
			Reset(true);
			return prop;
		}
		PropertyDescriptorCollection GetProperties(ViewPropertiesCollection properties) {
			PropertyDescriptorCollection props = new PropertyDescriptorCollection(null);
			int i = 0;
			foreach(ViewProperty prop in properties) {
				if(prop.Fetch) {
					try {
						Type type = CriteriaTypeResolver.ResolveType(ObjectClassInfo, prop.Property);
						props.Add(new ViewPropertyDescriptor(prop.Name, type, i));
						i++;
					}
					catch {
						if(!IsDesignMode)
							throw;
					}
				}
			}
			return props;
		}
		#region ITypedList impl
		PropertyDescriptorCollection displayProps;
		internal PropertyDescriptorCollection DisplayProps {
			get {
				if(displayProps == null)
					displayProps = GetProperties(Properties);
				return displayProps;
			}
		}
		PropertyDescriptorCollection ITypedList.GetItemProperties(PropertyDescriptor[] listAccessors) {
			return listAccessors != null && listAccessors.Length > 0 ? new PropertyDescriptorCollection(null) : DisplayProps;
		}
		string ITypedList.GetListName(PropertyDescriptor[] listAccessors) {
			return string.Empty;
		}
		#endregion
		#region ICollection Impl
		[Browsable(false)]
		public int Count {
			get {
				return Objects.Count;
			}
		}
		bool ICollection.IsSynchronized { get { return false; } }
		object ICollection.SyncRoot { get { return this; } }
		void ICollection.CopyTo(Array array, int index) {
			((ICollection)Objects).CopyTo(array, index);
		}
		#endregion
		#region IEnumerable Impl
		IEnumerator IEnumerable.GetEnumerator() {
			return Objects.GetEnumerator();
		}
		#endregion
		#region IList Impl
		bool IList.IsFixedSize { get { return true; } }
		bool IList.IsReadOnly { get { return true; } }
		object IList.this[int index] {
			get { return Objects[index]; }
			set { }
		}
		int IList.Add(object value) {
			throw new NotSupportedException();
		}
		void IList.Clear() {
			throw new NotSupportedException();
		}
		bool IList.Contains(object value) {
			return Objects.Contains(value);
		}
		int IList.IndexOf(object value) { return Objects.IndexOf(value); }
		void IList.Insert(int index, object value) {
			throw new NotSupportedException();
		}
		void IList.Remove(object value) {
			throw new NotSupportedException();
		}
		void IList.RemoveAt(int index) {
			throw new NotSupportedException();
		}
		#endregion
		#region IBinding Impl
		bool IBindingList.AllowEdit { get { return false; } }
		bool IBindingList.AllowNew { get { return false; } }
		bool IBindingList.AllowRemove { get { return false; } }
		bool IBindingList.IsSorted { get { return Sorting.Count > 0; } }
		ListSortDirection IBindingList.SortDirection {
			get { return Sorting.Count == 0 || Sorting[0].Direction != SortingDirection.Descending ? ListSortDirection.Ascending : ListSortDirection.Descending; }
		}
		PropertyDescriptor IBindingList.SortProperty {
			get { return (Sorting.Count > 0 && Objects.Count > 0) ? DisplayProps.Find(Sorting[0].PropertyName, false) : null; }
		}
		bool IBindingList.SupportsChangeNotification { get { return true; } }
		bool IBindingList.SupportsSearching { get { return true; } }
		bool IBindingList.SupportsSorting { get { return true; } }
		void IBindingList.AddIndex(PropertyDescriptor property) { }
		object IBindingList.AddNew() {
			throw new NotSupportedException();
		}
		void IBindingList.ApplySort(PropertyDescriptor property, ListSortDirection direction) {
			Sorting = new SortingCollection(new SortProperty(new OperandProperty(property.Name),
				direction == ListSortDirection.Ascending ? SortingDirection.Ascending : SortingDirection.Descending));
		}
		int IBindingList.Find(PropertyDescriptor property, object key) {
			int count = Count;
			for(int i = 0; i < count; i++) {
				if(property.GetValue(this[i]).Equals(key))
					return i;
			}
			return -1;
		}
		void IBindingList.RemoveIndex(PropertyDescriptor property) { }
		void IBindingList.RemoveSort() {
			Sorting = new SortingCollection();
		}
		event ListChangedEventHandler listChanged;
		public event ListChangedEventHandler ListChanged {
			add { listChanged += value; }
			remove { listChanged -= value; }
		}
		#endregion
		IObjectLayer IObjectLayerProvider.ObjectLayer {
			get {
				if(Session == null)
					return null;
				return Session.ObjectLayer;
			}
		}
		IDataLayer IDataLayerProvider.DataLayer {
			get {
				if(Session == null)
					return null;
				return Session.DataLayer;
			}
		}
		XPDictionary IXPDictionaryProvider.Dictionary {
			get {
				if(Session == null)
					return null;
				return IsDesignMode ? DesignDictionary : Session.Dictionary;
			}
		}
		XPClassInfo IXPClassInfoProvider.ClassInfo {
			get { return ObjectClassInfo; }
		}
	}
}
