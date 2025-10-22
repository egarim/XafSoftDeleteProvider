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
using System.ComponentModel;
using DevExpress.Xpo.Metadata;
using DevExpress.Data.Filtering;
using DevExpress.Xpo.Helpers;
using System.Reflection;
using System.ComponentModel.Design.Serialization;
using System.Globalization;
using DevExpress.Data.Filtering.Helpers;
using System.Collections;
using DevExpress.Xpo.Metadata.Helpers;
using DevExpress.Xpo.DB;
using DevExpress.Utils.Design;
namespace DevExpress.Xpo {
	public sealed class DataViewRecord : ICustomTypeDescriptor {
		XPDataView view;
		object[] data;
		internal object[] Data { get { return data; } }
		[Description("Gets the data view to which the current record belongs.")]
		public XPDataView View {
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
		public DataViewRecord(XPDataView view, object[] data) {
			this.view = view;
			this.data = data;
		}
		AttributeCollection ICustomTypeDescriptor.GetAttributes() {
			return AttributeCollection.Empty;
		}
		string ICustomTypeDescriptor.GetClassName() {
			return typeof(DataViewRecord).FullName;
		}
		string ICustomTypeDescriptor.GetComponentName() {
			return typeof(DataViewRecord).FullName;
		}
		TypeConverter ICustomTypeDescriptor.GetConverter() {
			return null;
		}
		EventDescriptor ICustomTypeDescriptor.GetDefaultEvent() {
			return null;
		}
		PropertyDescriptor ICustomTypeDescriptor.GetDefaultProperty() {
			return null;
		}
		object ICustomTypeDescriptor.GetEditor(Type editorBaseType) {
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
	}
	sealed class DataViewPropertyDescriptor : PropertyDescriptor {
		readonly int index;
		readonly Type reportedType;
		public DataViewPropertyDescriptor(string name, Type type, int index)
			: base(name, Array.Empty<Attribute>()) {
			this.index = index;
			this.reportedType = Nullable.GetUnderlyingType(type);
			if(reportedType == null)
				reportedType = type;
		}
		public override bool IsReadOnly { get { return true; } }
		public override object GetValue(object component) {
			return ((DataViewRecord)component).Data[index];
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
	[TypeConverter(typeof(DataViewPropertyConverter))]
	public class DataViewProperty {
		string name;
		Type valueType;
		XPDataView owner;
		protected internal void SetOwner(XPDataView owner) {
			this.owner = owner;
		}
		void ResetView() {
			if(owner != null) {
				owner.ClearProps();
			}
		}
		[Description("Gets or sets the column’s name.")]
		[Category("Data")]
		public string Name {
			get { return name; }
			set { name = value; ResetView(); }
		}
		[Description("Gets or sets the column’s type.")]
		[Category("Data")]
		[Editor("DevExpress.Utils.Editors.SimpleTypeEditor, " + AssemblyInfo.SRAssemblyDataDesktopFull, DevExpress.Utils.ControlConstants.UITypeEditor)]
		[TypeConverter("DevExpress.Utils.Editors.SimpleToStringTypeConverter," + AssemblyInfo.SRAssemblyDataDesktopFull)]
		public Type ValueType {
			get { return valueType; }
			set { valueType = value; ResetView(); }
		}
		public DataViewProperty()
			: this(string.Empty) {
		}
		public DataViewProperty(string name)
			: this(name, typeof(object)) {
		}
		public DataViewProperty(string name, Type valueType) {
			this.name = name;
			this.valueType = valueType;
		}
	}
	class DataViewPropertyConverter : TypeConverter {
		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType) {
			if(destinationType == typeof(InstanceDescriptor))
				return true;
			return base.CanConvertTo(context, destinationType);
		}
		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object val, Type destinationType) {
			DataViewProperty prop = val as DataViewProperty;
			if(destinationType == typeof(InstanceDescriptor) && prop != null) {
				ConstructorInfo ctor = typeof(DataViewProperty).GetConstructor(new Type[] { typeof(string), typeof(Type) });
				if(ctor != null) {
					return new InstanceDescriptor(ctor, new object[] { prop.Name, prop.ValueType });
				}
			}
			return base.ConvertTo(context, culture, val, destinationType);
		}
	}
	[ListBindable(BindableSupport.No)]
	[Editor("DevExpress.Xpo.Design.DataViewPropertiesCollectionEditor," + AssemblyInfo.SRAssemblyXpoDesignFull, DevExpress.Utils.ControlConstants.UITypeEditor)]
	public sealed class DataViewPropertiesCollection : CollectionBase {
		XPDataView owner = null;
		public DataViewPropertiesCollection(XPDataView owner)
			: base() {
			this.owner = owner;
		}
		public void Add(DataViewProperty sortProperty) {
			((IList)this).Add(sortProperty);
		}
		public void AddRange(DataViewProperty[] sortProperties) {
			foreach(DataViewProperty sp in sortProperties)
				Add(sp);
		}
		public void Add(DataViewPropertiesCollection sortProperties) {
			foreach(DataViewProperty sp in sortProperties)
				Add(sp);
		}
		protected override void OnInsertComplete(int index, object value) {
			((DataViewProperty)value).SetOwner(owner);
			if(owner != null)
				owner.ClearProps();
		}
		protected override void OnRemoveComplete(int index, object value) {
			((DataViewProperty)value).SetOwner(null);
			if(owner != null)
				owner.ClearProps();
		}
		public DataViewProperty this[int index] { get { return (DataViewProperty)List[index]; } }
		public DataViewProperty this[string name] {
			get {
				foreach(DataViewProperty prop in this) {
					if(Equals(name, prop.Name))
						return prop;
				}
				return null;
			}
		}
	}
	[Description("Represents the data view that displays result set contents.")]
	[DXToolboxItem(true)]
	[DevExpress.Utils.ToolboxTabName(AssemblyInfo.DXTabOrmComponents)]
	[Designer("DevExpress.Xpo.Design.XPDataViewDesigner, " + AssemblyInfo.SRAssemblyXpoDesignFull, Aliases.IDesigner)]
#if !NET
	[System.Drawing.ToolboxBitmap(typeof(XPDataView))]
#endif
	public class XPDataView : Component, ISupportInitialize, IBindingList, ITypedList, IFilteredXtraBindingList, IXPDictionaryProvider {
		bool? _isDesignMode;
		protected bool IsDesignMode {
			get {
				return DevExpress.Data.Helpers.IsDesignModeHelper.GetIsDesignModeBypassable(this, ref _isDesignMode);
			}
		}
		ArrayList objects = new ArrayList();
		ArrayList Objects {
			get {
				if(IsDesignMode)
					return GetSampleData();
				return Sorted;
			}
		}
		ArrayList GetSampleData() {
			ArrayList list = new ArrayList();
			list.Add(new DataViewRecord(this, new object[DisplayProps.Count]));
			return list;
		}
		internal void Clear() {
			objects = new ArrayList();
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
			return XPCollectionCompareHelper.CreateComparer(Sorting, new CriteriaCompiledContextDescriptorDescripted(DisplayProps), new CriteriaCompilerAuxSettings(CaseSensitive, Dictionary == null ? null : Dictionary.CustomFunctionOperators));
		}
		[Description("Gets or sets whether string comparisons evaluated by the XPDataView on the client are case-sensitive.")]
		[Category("Data")]
		public bool CaseSensitive {
			get {
				return caseSensitive.HasValue ? caseSensitive.Value : XpoDefault.DefaultCaseSensitive;
			}
			set {
				caseSensitive = value;
				Reset(false);
			}
		}
		bool ShouldSerializeCaseSensitive() {
			return caseSensitive.HasValue;
		}
		void ResetCaseSensitive() {
			caseSensitive = null;
		}
		ArrayList Sorted {
			get {
				if(Sorting.Count == 0)
					return Filtered;
				if(sorted == null) {
					sorted = Filtered;
					sorted.Sort(CreateComparer());
				}
				return sorted;
			}
		}
		bool IsFilterFit(object theObject) {
			if(IsDesignMode)
				return true;
			if(fitPredicate == null)
				fitPredicate = CriteriaCompiler.ToUntypedPredicate(filter, CriteriaCompilerDescriptor.Get(((ITypedList)this).GetItemProperties(null)), new CriteriaCompilerAuxSettings(CaseSensitive, dictionary == null ? null : dictionary.CustomFunctionOperators));
			return fitPredicate(theObject);
		}
		ArrayList Filtered {
			get {
				if(ReferenceEquals(filter, null))
					return objects;
				if(filtered == null) {
					filtered = new ArrayList();
					int count = objects.Count;
					for(int i = 0; i < count; i++) {
						object obj = objects[i];
						if(IsFilterFit(obj))
							filtered.Add(obj);
					}
				}
				return filtered;
			}
		}
		public void LoadData(SelectedData data) {
			objects = LoadDataInternal(data);
			Reset(false);
		}
		public static SelectStatementResult GetTargetResultSet(SelectedData data) {
			SelectStatementResult targetResult = data.ResultSet[0];
			if(data.ResultSet.Length > 1 && data.ResultSet[0].Rows.Length > 0 && data.ResultSet[0].Rows[0].Values != null && data.ResultSet[0].Rows[0].Values.Length == 3) {
				bool allStringsOrNull = true;
				foreach(object value in data.ResultSet[0].Rows[0].Values) {
					if(value != null && !(value is string)) {
						allStringsOrNull = false;
						break;
					}
				}
				if(allStringsOrNull) {
					targetResult = data.ResultSet[1];
				}
			}
			return targetResult;
		}
		ArrayList LoadDataInternal(SelectedData data) {
			ArrayList list = new ArrayList();
			if(data == null || data.ResultSet == null || data.ResultSet.Length == 0) return list;
			SelectStatementResult targetResult = GetTargetResultSet(data);
			foreach(SelectStatementResultRow rec in targetResult.Rows) {
				if(rec.Values.Length != Properties.Count) throw new InvalidOperationException(Res.GetString(Res.DirectSQL_WrongColumnCount));
				list.Add(new DataViewRecord(this, rec.Values));
			}
			return list;
		}
		public void LoadOrderedData(LoadDataMemberOrderItem[] members, SelectedData data) {
			objects = LoadOrderedDataInternal(members, data);
			Reset(false);
		}
		ArrayList LoadOrderedDataInternal(LoadDataMemberOrderItem[] members, SelectedData data) {
			ArrayList list = new ArrayList();
			if(members == null || data == null || data.ResultSet == null || data.ResultSet.Length == 0) {
				if(Properties.Count == 0) return list;
				throw new InvalidOperationException(Res.GetString(Res.DirectSQL_WrongColumnCount));
			}
			if(members.Length != Properties.Count) throw new InvalidOperationException(Res.GetString(Res.DirectSQL_WrongColumnCount));
			SelectStatementResult targetResult = GetTargetResultSet(data);
			foreach(SelectStatementResultRow rec in targetResult.Rows) {
				DataViewRecord dataRecord = new DataViewRecord(this, new object[members.Length]);
				for(int m = 0; m < members.Length; m++) {
					dataRecord.Data[m] = rec.Values[members[m].IndexInResultSet];
				}
				list.Add(dataRecord);
			}
			return list;
		}
		public DataViewRecord this[int index] {
			get {
				return (DataViewRecord)Objects[index];
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
		DataViewPropertiesCollection props;
		[Description("Provides access to the data view’s columns.")]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
		[Category("View")]
		public DataViewPropertiesCollection Properties {
			get {
				return props;
			}
		}
		Func<object, bool> fitPredicate;
		CriteriaOperator filter;
		[Description("Gets or sets the criteria used to perform client-side filtering of data view rows.")]
		[TypeConverter("DevExpress.Xpo.Design.CriteriaConverter, " + AssemblyInfo.SRAssemblyXpoDesignFull)]
		[DefaultValue(null)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		[Editor("DevExpress.Xpo.Design.XPDataViewCriteriaEditor, " + AssemblyInfo.SRAssemblyXpoDesignFull, DevExpress.Utils.ControlConstants.UITypeEditor)]
		[Category("Data")]
		public CriteriaOperator Filter {
			get { return filter; }
			set {
				if(!ReferenceEquals(filter, value)) {
					filter = value;
					ResetValidator();
					Reset(false);
				}
			}
		}
		[Browsable(false)]
		[DefaultValue(null)]
		public string FilterString {
			get {
				CriteriaOperator filter = Filter;
				return (object)filter == null ? null : filter.ToString();
			}
			set {
				if(FilterString != value)
					Filter = CriteriaOperator.Parse(value);
			}
		}
		ArrayList sorted;
		ArrayList filtered;
		void ResetValidator() {
			fitPredicate = null;
		}
		void Reset(bool metadataChanged) {
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
		[Description("Provides access to the collection whose elements specify sorting options for the data view.")]
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
		public XPDataView() {
			props = new DataViewPropertiesCollection(this);
		}
		public XPDataView(IContainer container)
			: this() {
			container.Add(this);
		}
		public XPDataView(XPDictionary dictionary)
			: this() {
			this.dictionary = dictionary;
		}
		public XPDataView(XPDictionary dictionary, IList<string> names, IList<Type> types)
			: this(dictionary) {
			for(int i = 0; i < names.Count; i++) {
				AddProperty(names[i], types[i]);
			}
		}
		public XPDataView(XPDictionary dictionary, XPClassInfo classInfo)
			: this(dictionary, classInfo.PersistentProperties as List<XPMemberInfo>) {
		}
		public XPDataView(XPDictionary dictionary, XPClassInfo classInfo, SelectedData data)
			: this(dictionary, classInfo) {
			LoadData(data);
		}
		public XPDataView(XPDictionary dictionary, List<XPMemberInfo> memberInfoList)
			: this(dictionary) {
			PopulateProperties(memberInfoList);
		}
		public XPDataView(XPDictionary dictionary, XPClassInfo classInfo, params string[] members)
			: this(dictionary) {
			PopulateProperties(classInfo, members);
		}
		public XPDataView(XPDictionary dictionary, XPClassInfo classInfo, LoadDataMemberOrderItem[] members, SelectedData data)
			: this(dictionary) {
			if(members == null) return;
			PopulatePropertiesOrdered(classInfo, members);
			LoadOrderedData(members, data);
		}
		public void PopulateProperties(XPClassInfo classInfo) {
			PopulateProperties(classInfo.PersistentProperties as List<XPMemberInfo>);
		}
		public void PopulateProperties(List<XPMemberInfo> memberInfoList) {
			Properties.Clear();
			Reset(true);
			if(memberInfoList == null) return;
			foreach(XPMemberInfo mi in memberInfoList) {
				AddProperty(mi);
			}
		}
		public void PopulateProperties(XPClassInfo classInfo, params string[] members) {
			Properties.Clear();
			Reset(true);
			if(members == null) return;
			for(int i = 0; i < members.Length; i++) {
				AddProperty(classInfo.FindMember(members[i]));
			}
		}
		public void PopulatePropertiesOrdered(XPClassInfo classInfo, LoadDataMemberOrderItem[] members) {
			Properties.Clear();
			Reset(true);
			if(members == null) return;
			for(int i = 0; i < members.Length; i++) {
				XPMemberInfo mi = classInfo.FindMember(members[i].ClassMemberName);
				if(mi == null) throw new InvalidOperationException(string.Format(Res.GetString(Res.Session_NotClassMember), members[i].ClassMemberName, classInfo.FullName));
				AddProperty(mi);
			}
		}
		void AddProperty(XPMemberInfo mi) {
			if(mi.ReferenceType != null || mi.IsStruct) {
				XPMemberInfo currentMemberInfo = mi;
				do {
					if(currentMemberInfo.IsStruct) throw new NotSupportedException(Res.GetString(Res.DataView_ReferenceMembersWithCompoundKeyAreNotSupported));
					currentMemberInfo = currentMemberInfo.ReferenceType.KeyProperty;
				} while(currentMemberInfo.ReferenceType != null || currentMemberInfo.IsStruct);
				AddProperty(mi.Name, currentMemberInfo.MemberType);
			} else {
				if(mi.IsCollection || mi.IsAssociationList) throw new NotSupportedException(Res.GetString(Res.DirectSQL_CollectionMembersAreNotSupported));
				AddProperty(mi.Name, mi.MemberType);
			}
		}
		public DataViewProperty AddProperty(string name, Type valueType) {
			DataViewProperty prop = new DataViewProperty(name, valueType);
			Properties.Add(prop);
			Reset(true);
			return prop;
		}
		PropertyDescriptorCollection GetProperties(DataViewPropertiesCollection properties) {
			PropertyDescriptorCollection props = new PropertyDescriptorCollection(null);
			int i = 0;
			foreach(DataViewProperty prop in properties) {
				try {
					props.Add(new DataViewPropertyDescriptor(prop.Name, prop.ValueType, i));
					i++;
				} catch {
					if(!IsDesignMode)
						throw;
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
			Objects.CopyTo(array, index);
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
		XPDictionary dictionary;
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public XPDictionary Dictionary {
			get {
				return IsDesignMode ? DesignDictionary : dictionary;
			}
			set {
				if(dictionary != value) {
					dictionary = value;
					Reset(false);
				}
			}
		}
	}
}
