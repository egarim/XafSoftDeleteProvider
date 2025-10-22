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

#if !NET
using System;
using System.ComponentModel;
using System.Collections;
using System.Collections.Specialized;
using System.Web.UI;
using System.Web;
using System.Web.Compilation;
using DevExpress.Xpo.Helpers;
using DevExpress.Xpo.Metadata;
using DevExpress.Xpo.Metadata.Helpers;
using DevExpress.Xpo.DB;
using DevExpress.Data.Filtering;
using System.Web.UI.WebControls;
using DevExpress.Data.Helpers;
using DevExpress.Utils.Design;
using DevExpress.Utils;
namespace DevExpress.Xpo {
	public class XpoDataSourceInsertedEventArgs : EventArgs {
		object value;
		public object Value { get { return value; } }
		public XpoDataSourceInsertedEventArgs(object value) {
			this.value = value;
		}
	}
	public delegate void XpoDataSourceInsertedEventHandler(object sender, XpoDataSourceInsertedEventArgs e);
	[DXWebToolboxItem(true)]
	[DevExpress.Utils.ToolboxTabName(AssemblyInfo.DXTabOrmComponents)]
	[PersistChildren(false), ParseChildren(true),
	Designer("DevExpress.Xpo.Design.XpoDataSourceDesigner, " + AssemblyInfo.SRAssemblyXpoDesignFull, Aliases.IDesigner),
#if !NET
	System.Drawing.ToolboxBitmap(typeof(XpoDataSource)),
#endif
	System.ComponentModel.DisplayName("Xpo Source " + AssemblyInfo.VersionShort)]
	public class XpoDataSource : DataSourceControl, IXPClassInfoProvider {
		protected override DataSourceView GetView(string viewName) {
			if ((viewName != null) && ((viewName.Length == 0) || string.Equals(viewName, "DefaultView", StringComparison.OrdinalIgnoreCase))) {
				return this.GetView();
			}
			throw new ArgumentException("", "viewName");
		}
		public XpoDataSource() {
		}
		XpoDataSourceView _view;
		XpoDataSourceView GetView() {
			if (this._view == null) {
				this._view = new XpoDataSourceView(this, "DefaultView", this.Context);
				if(IsTrackingViewState) {
					((IStateManager)_view).TrackViewState();
				}
			}
			return this._view;
		}
		string[] _viewNames;
		protected override ICollection GetViewNames() {
			if (_viewNames == null)
				this._viewNames = new string[] { "DefaultView" };
			return this._viewNames;
		}
		protected override void OnInit(EventArgs e) {
			base.OnInit(e);
			if (this.Page != null) {
				this.Page.LoadComplete += new EventHandler(LoadCompleteEventHandler);
			}
		}
		void LoadCompleteEventHandler(object sender, EventArgs e) {
			CriteriaParameters.UpdateValues(this.Context, this);
		}
		[Description("Gets or sets whether server mode is enabled for the data-aware control bound to the current data source control."), DefaultValue(false)]
		[Category("Options")]
		public bool ServerMode {
			get {
				return GetView().ServerMode;
			}
			set {
				GetView().ServerMode = value;
			}
		}
		[Description("Specifies how data source contents are sorted by default, when sort order is not specified by the bound control."), DefaultValue("")]
		[Editor("DevExpress.Xpo.Design.XpoDataSourceDefaultSortingCollectionEditor, " + AssemblyInfo.SRAssemblyXpoDesignFull, DevExpress.Utils.ControlConstants.UITypeEditor)]
		[Category("Data")]
		public string DefaultSorting {
			get {
				return GetView().DefaultSorting;
			}
			set {
				GetView().DefaultSorting = value;
			}
		}
		[Description("Gets or sets the type name of persistent objects to be retrieved by the data source control.")]
		[TypeConverter("DevExpress.Xpo.Design.XpoDataSourceTypeNameConverter, " + AssemblyInfo.SRAssemblyXpoDesignFull)]
		[DefaultValue("")]
		[Category("Data")]
		public string TypeName {
			get { return GetView().TypeName; }
			set { GetView().TypeName = value; }
		}
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public Session Session {
			get {
				return GetView().Session;
			}
			set {
				GetView().Session = value;
			}
		}
		[Description("Gets or sets the string representation of criteria used to filter persistent objects to be retrieved by the data source control.")]
		[Editor("DevExpress.Xpo.Design.XpoDataSourceCriteriaEditor, " + AssemblyInfo.SRAssemblyXpoDesignFull, DevExpress.Utils.ControlConstants.UITypeEditor)]
		[DefaultValue(null)]
		[Category("Data")]
		public string Criteria {
			get {
				return GetView().Criteria;
			}
			set {
				GetView().Criteria = value;
			}
		}
		[Description("Provides access to the collection of parameter values that are used to build the data source control’s filter criteria.")]
		[PersistenceMode(PersistenceMode.InnerProperty)]
		[DefaultValue((string)null)]
		[Editor("System.Web.UI.Design.WebControls.ParameterCollectionEditor, System.Design, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", ControlConstants.UITypeEditor)]
		[MergableProperty(false)]
		[Category("Data")]
		public ParameterCollection CriteriaParameters {
			get {
				return GetView().CriteriaParameters;
			}
		}
		public event XpoDataSourceInsertedEventHandler Inserted {
			add {
				this.GetView().Inserted += value;
			}
			remove {
				this.GetView().Inserted -= value;
			}
		}
		XPClassInfo IXPClassInfoProvider.ClassInfo {
			get { return GetView().GetClassInfo(); }
		}
		XPDictionary IXPDictionaryProvider.Dictionary {
			get { throw new NotImplementedException(); }
		}
		protected override void TrackViewState() {
			base.TrackViewState();
			if(_view != null) {
				((IStateManager)_view).TrackViewState();
			}
		}
		protected override void LoadViewState(object savedState) {
			if(savedState == null) {
				base.LoadViewState(null);
			} else {
				Pair myState = (Pair)savedState;
				base.LoadViewState(myState.First);
				if(myState.Second != null) {
					((IStateManager)GetView()).LoadViewState(myState.Second);
				}
			}
		}
		protected override object SaveViewState() {
			Pair state = new Pair();
			state.First = base.SaveViewState();
			if(_view != null) {
				state.Second = ((IStateManager)_view).SaveViewState();
			}
			if((state.First == null) &&
				(state.Second == null)) {
				return null;
			}
			return state;
		}
	}
	public class XpoDataSourceView : DataSourceView, IColumnsServerActions, IStateManager {
		Session session;
		string _criteria;
		XpoDataSource owner;
		HttpContext context;
		ParameterCollection _criteriaParameters;
		private bool _trackViewState;
		public XpoDataSourceView(XpoDataSource owner, string name, HttpContext context)
			: base(owner, name) {
			this.owner = owner;
			this.context = context;
		}
		protected override System.Collections.IEnumerable ExecuteSelect(DataSourceSelectArguments arguments) {
			if (TypeName == null)
				return null;
			SortingCollection sorting = null;
			if (!String.IsNullOrEmpty(arguments.SortExpression)) {
				string[] sorts = arguments.SortExpression.Split(',');
				sorting = new SortingCollection();
				foreach (string sort in sorts) {
					string name = sort.Trim();
					SortingDirection direction = SortingDirection.Ascending;
					if (name.EndsWith(" DESC")) {
						name = name.Substring(0, name.Length - 5).TrimEnd();
						direction = SortingDirection.Descending;
					} else if (name.EndsWith(" ASC")) {
						name = name.Substring(0, name.Length - 4).TrimEnd();
					}
					sorting.Add(new SortProperty(new OperandProperty(name), direction));
				}
			}
			XPClassInfo ci = GetClassInfo();
			IOrderedDictionary values = CriteriaParameters.GetValues(context, owner);
			object[] parameters = new object[values.Count];
			for (int p = 0; p < values.Count; p++)
				parameters[p] = values[p];
			CriteriaOperator criteria = CriteriaOperator.Parse(Criteria, parameters);
			if (ServerMode) {
				XPServerCollectionSource srv = new XPServerCollectionSource(Session, ci, criteria);
				srv.DefaultSorting = this.DefaultSorting;
				GC.SuppressFinalize(srv);
				return ((IListSource)srv).GetList();
			} else {
				if (arguments.RetrieveTotalRowCount) {
					arguments.TotalRowCount = ((int?)Session.Evaluate(ci, AggregateOperand.TopLevel(Aggregate.Count), criteria)) ?? 0;
				}
				XPCollection col = new XPCollection(Session, ci, criteria);
				GC.SuppressFinalize(col);
				if (sorting == null)
					sorting = GetDefaultSorting();
				if (sorting != null)
					col.Sorting = sorting;
				if (arguments.MaximumRows > 0 && arguments.StartRowIndex >= 0) {
					col.SkipReturnedObjects = arguments.StartRowIndex;
					col.TopReturnedObjects = arguments.MaximumRows;
				}
				return col;
			}
		}
		SortingCollection GetDefaultSorting() {
			DevExpress.Data.ServerModeOrderDescriptor[] descriptors = ServerModeCore.GetSortingDescriptors(this.DefaultSorting);
			if (descriptors == null || descriptors.Length == 0)
				return null;
			SortingCollection rv = new SortingCollection();
			foreach (DevExpress.Data.ServerModeOrderDescriptor d in descriptors) {
				rv.Add(new SortProperty(d.SortExpression, d.IsDesc ? SortingDirection.Descending : SortingDirection.Ascending));
			}
			return rv;
		}
		protected override int ExecuteUpdate(IDictionary keys, IDictionary values, IDictionary oldValues) {
			if (keys.Count == 0)
				throw new NotSupportedException();
			object keyVal = null;
			foreach (DictionaryEntry key in keys) {
				keyVal = key.Value;
			}
			XPClassInfo ci = GetClassInfo();
			XPClassInfo keyCi = Session.Dictionary.QueryClassInfo(keyVal);
			object obj = keyCi != null && keyCi.IsAssignableTo(ci) ? keyVal : Session.GetObjectByKey(ci, keyVal);
			if (obj == null)
				return 0;
			SetValues(values, ci, obj);
			Session.Save(obj);
			OnDataSourceViewChanged(EventArgs.Empty);
			return 1;
		}
		protected override int ExecuteInsert(IDictionary values) {
			XPClassInfo ci = GetClassInfo();
			object obj = ci.CreateNewObject(Session);
			SetValues(values, ci, obj);
			Session.Save(obj);
			OnInserted(new XpoDataSourceInsertedEventArgs(obj));
			OnDataSourceViewChanged(EventArgs.Empty);
			return 1;
		}
		void SetValues(IDictionary values, XPClassInfo ci, object rootObj) {
			foreach (DictionaryEntry val in values) {
				string name = (string)val.Key;
				bool referenceKey = name.EndsWith(XPPropertyDescriptor.ReferenceAsKeyTail);
				name = name.Replace(XPPropertyDescriptor.ReferenceAsKeyTail, String.Empty);
				MemberInfoCollection members = MemberInfoCollection.ParsePath(ci, name);
				XPMemberInfo mi = members[0];
				object obj = rootObj;
				for (int i = 1; i < members.Count; i++) {
					obj = mi.GetValue(obj);
					if (obj == null)
						break;
					mi = members[i];
				}
				if (obj == null)
					continue;
				if (mi.ReferenceType != null && referenceKey) {
					object refObj = val.Value == null ? null : Session.GetObjectByKey(mi.ReferenceType, ConvertValue(val.Value, mi.ReferenceType.KeyProperty.MemberType));
					mi.SetValue(obj, refObj);
				} else
					mi.SetValue(obj, val.Value == null ? null : ConvertValue(val.Value, mi.MemberType));
			}
		}
		object ConvertValue(object value, Type targetType) {
			if (targetType.IsInstanceOfType(value))
				return value;
			Type type = GetType(targetType);
			string stringValue = value as string;
			if (stringValue != null) {
				TypeConverter converter = TypeDescriptor.GetConverter(type);
				if (converter != null)
					return converter.ConvertFromString(stringValue);
			}
			return value;
		}
		private Type GetType(Type type) {
			Type nullType = Nullable.GetUnderlyingType(type);
			return nullType == null ? type : nullType;
		}
		protected override int ExecuteDelete(IDictionary keys, IDictionary oldValues) {
			if (keys.Count == 0)
				throw new NotSupportedException();
			object keyVal = null;
			foreach (DictionaryEntry key in keys) {
				keyVal = key.Value;
			}
			XPClassInfo ci = GetClassInfo();
			object obj = Session.GetObjectByKey(ci, keyVal);
			if (obj == null)
				return 0;
			Session.Delete(obj);
			OnDataSourceViewChanged(EventArgs.Empty);
			return 1;
		}
		void CriteriaParametersChangedEventHandler(object o, EventArgs e) {
			this.OnDataSourceViewChanged(EventArgs.Empty);
		}
		public override bool CanUpdate {
			get {
				return true;
			}
		}
		public override bool CanInsert {
			get {
				return true;
			}
		}
		public override bool CanDelete {
			get {
				return true;
			}
		}
		public override bool CanSort {
			get {
				return true;
			}
		}
		public override bool CanPage {
			get {
				return true;
			}
		}
		public override bool CanRetrieveTotalRowCount {
			get {
				return true;
			}
		}
		public Session Session {
			get {
				if (session != null)
					return session;
				Session res = XpoDefault.Session;
				if (res == null)
					throw new ArgumentNullException("XpoDefault.Session");
				return res;
			}
			set {
				session = value;
			}
		}
		bool serverMode = false;
		public bool ServerMode {
			get { return serverMode; }
			set { serverMode = value; }
		}
		string _DefaultSorting;
		public string DefaultSorting {
			get { return _DefaultSorting; }
			set { _DefaultSorting = value ?? string.Empty; }
		}
		string _typeName;
		public string TypeName {
			get { return _typeName; }
			set { _typeName = value; }
		}
		public string Criteria {
			get { return _criteria; }
			set { _criteria = value; }
		}
		public ParameterCollection CriteriaParameters {
			get {
				if (_criteriaParameters == null) {
					_criteriaParameters = new ParameterCollection();
					_criteriaParameters.ParametersChanged += new EventHandler(CriteriaParametersChangedEventHandler);
					if(_trackViewState) {
						((IStateManager)_criteriaParameters).TrackViewState();
					}
				}
				return _criteriaParameters;
			}
		}
		static readonly object EventInserted = new object();
		protected virtual void OnInserted(XpoDataSourceInsertedEventArgs e) {
			XpoDataSourceInsertedEventHandler handler = base.Events[EventInserted] as XpoDataSourceInsertedEventHandler;
			if (handler != null) {
				handler(this, e);
			}
		}
		public event XpoDataSourceInsertedEventHandler Inserted {
			add {
				Events.AddHandler(EventInserted, value);
			}
			remove {
				Events.RemoveHandler(EventInserted, value);
			}
		}
		bool IColumnsServerActions.AllowAction(string fieldName, ColumnServerActionType action) {
			return XpoServerModeCore.IColumnsServerActionsAllowAction(Session, GetClassInfo(), fieldName);
		}
		public XPClassInfo GetClassInfo() {
			if (string.IsNullOrEmpty(TypeName)) return null;
			Type type = BuildManager.GetType(TypeName, false, true);
			if (type != null)
				return Session.GetClassInfo(type);
			return Session.GetClassInfo("", TypeName);
		}
		protected virtual void LoadViewState(object savedState) {
			((IStateManager)CriteriaParameters).LoadViewState(savedState);
		}
		protected virtual object SaveViewState() {
			return ((IStateManager)CriteriaParameters).SaveViewState();
		}
		protected bool IsTrackingViewState {
			get {
				return _trackViewState;
			}
		}
		protected virtual void TrackViewState() {
			_trackViewState = true;
			if(_criteriaParameters != null) {
				((IStateManager)_criteriaParameters).TrackViewState();
			}
		}
#region IStateManager
		bool IStateManager.IsTrackingViewState {
			get {
				return IsTrackingViewState;
			}
		}
		void IStateManager.LoadViewState(object savedState) {
			LoadViewState(savedState);
		}
		object IStateManager.SaveViewState() {
			return SaveViewState();
		}
		void IStateManager.TrackViewState() {
			TrackViewState();
		}
#endregion
	}
}
#endif
