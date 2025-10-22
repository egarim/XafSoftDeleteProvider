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
using System.ComponentModel.Design.Serialization;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using DevExpress.Data;
using DevExpress.Data.Filtering;
using DevExpress.Data.Filtering.Helpers;
using DevExpress.Data.Helpers;
using DevExpress.Utils.Design;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.Helpers;
using DevExpress.Xpo.Metadata;
using DevExpress.Xpo.Metadata.Helpers;
namespace DevExpress.Xpo.Helpers {
	public class XpoViewServerModeCache : ServerModeKeyedCache {
		public readonly Session Session;
		public readonly XPClassInfo ClassInfo;
		public readonly CriteriaOperator ExternalCriteria;
		readonly CriteriaOperator[] SelectProperties;
		public XpoViewServerModeCache(Session session, XPClassInfo classInfo, CriteriaOperator externalCriteria, CriteriaOperator[] displayableProps, CriteriaOperator[] keyCriteria, ServerModeOrderDescriptor[][] sortInfo, int groupCount, ServerModeSummaryDescriptor[] summary, ServerModeSummaryDescriptor[] totalSummary)
			: base(keyCriteria, sortInfo, groupCount, summary, totalSummary) {
			this.Session = session;
			this.ClassInfo = classInfo;
			this.ExternalCriteria = externalCriteria;
			var exprs = displayableProps.ToList();
			foreach(var nonSelectedKeySubCriterion in this.KeysCriteria.Where(k => !exprs.Contains(k))) {
				exprs.Add(nonSelectedKeySubCriterion);
			}
			foreach(var nonSelectedsortExpression in this.SortDescription.SelectMany(d => d).Select(d => d.SortExpression).Where(d => !exprs.Contains(d))) {
				exprs.Add(nonSelectedsortExpression);
			}
			this.SelectProperties = exprs.ToArray();
		}
		protected override object[] FetchKeys(CriteriaOperator where, ServerModeOrderDescriptor[] order, int skip, int take) {
			CriteriaOperatorCollection props = new CriteriaOperatorCollection();
			props.AddRange(this.KeysCriteria);
			CriteriaOperator filter = ExternalCriteria & where;
			SortingCollection sorting = XpoServerModeCache.OrderDescriptorsToSortingCollection(order);
			IList<object[]> keys = Session.SelectData(ClassInfo, props, filter, false, skip, take, sorting);
			if(this.KeysCriteria.Length == 1)
				return keys.Select(ks => ks[0]).ToArray();
			else
				return keys.Select(ks => new ServerModeCompoundKey(ks)).ToArray();
		}
		protected override object[] FetchRows(CriteriaOperator where, ServerModeOrderDescriptor[] order, int take) {
			CriteriaOperatorCollection props = new CriteriaOperatorCollection();
			props.AddRange(this.SelectProperties);
			CriteriaOperator filter = ExternalCriteria & where;
			SortingCollection sorting = XpoServerModeCache.OrderDescriptorsToSortingCollection(order);
			IList<object[]> data = Session.SelectData(ClassInfo, props, filter, false, 0, take, sorting);
			return data.ToArray();
		}
		protected override int GetCount(CriteriaOperator criteriaOperator) {
			return ((int?)Session.Evaluate(ClassInfo, AggregateOperand.TopLevel(Aggregate.Count), ExternalCriteria & criteriaOperator)) ?? 0;
		}
		protected override ServerModeGroupInfoData PrepareTopGroupInfo(ServerModeSummaryDescriptor[] summaries) {
			return XpoServerModeCache.PrepareTopGroupInfoCommon(summaries, Session, ClassInfo, ExternalCriteria);
		}
		protected override Type ResolveKeyType(CriteriaOperator singleKeyCriterion) {
			var op = singleKeyCriterion as OperandProperty;
			if(ReferenceEquals(op, null))
				throw new ArgumentException(null, nameof(singleKeyCriterion));
			string propertyName = op.PropertyName;
			var prop = ClassInfo.GetMember(propertyName);
			return XpoServerModeCache.ExtractKeyPropertyType(prop);
		}
		protected override Type ResolveRowType() {
			return typeof(object[]);
		}
		protected override Func<object, object> GetOnInstanceEvaluator(CriteriaOperator toEvaluate) {
			var ind = Array.IndexOf(SelectProperties, toEvaluate);
			if(ind < 0)
				throw new InvalidOperationException($"'{toEvaluate}' not found in SelectProperties!");
			return row => ((object[])row)[ind];
		}
		protected override ServerModeGroupInfoData[] PrepareChildren(CriteriaOperator groupWhere, CriteriaOperator[] groupByCriteria, CriteriaOperator[] orderByCriteria, bool[] isDescOrder, ServerModeSummaryDescriptor[] summaries) {
			return XpoServerModeCache.PrepareChildrenCommon(Session, ClassInfo, ExternalCriteria
				, c => { return null; }
				, groupWhere, groupByCriteria, orderByCriteria, isDescOrder, summaries);
		}
	}
	public class XpoViewServerModeCore : ServerModeCore,  IBindingList, ITypedList {
		readonly Session Session;
		readonly XPClassInfo ClassInfo;
		CriteriaOperator _FixedCriteria;
		readonly CriteriaOperator[] SelectExpressions;
		readonly string[] PDNames;  
		readonly bool lastPropertyIsExtraKey;
#if DEBUG
		public XpoViewServerModeCore(Session initialSession, XPClassInfo initialClassInfo, CriteriaOperator initialFixedCriteria, IEnumerable<string> displayableProps, string defaultSorting, bool lastPropertyIsExtraKey)
			: this(initialSession, initialClassInfo, initialFixedCriteria, displayableProps.Select(propName => Tuple.Create<string, CriteriaOperator>(propName, new OperandProperty(propName))), defaultSorting, lastPropertyIsExtraKey) {
		}
		public XpoViewServerModeCore(Session initialSession, XPClassInfo initialClassInfo, CriteriaOperator initialFixedCriteria, IEnumerable<string> displayableProps, string defaultSorting)
			: this(initialSession, initialClassInfo, initialFixedCriteria, displayableProps.Select(propName => Tuple.Create<string, CriteriaOperator>(propName, new OperandProperty(propName))), defaultSorting, false) {
		}
#endif
		public XpoViewServerModeCore(Session initialSession, XPClassInfo initialClassInfo, CriteriaOperator initialFixedCriteria, IEnumerable<Tuple<string, CriteriaOperator>> props, string defaultSorting)
			: this(initialSession, initialClassInfo, initialFixedCriteria, props, defaultSorting, false) {
		}
		public XpoViewServerModeCore(Session initialSession, XPClassInfo initialClassInfo, CriteriaOperator initialFixedCriteria, IEnumerable<Tuple<string, CriteriaOperator>> props, string defaultSorting, bool lastPropertyIsExtraKey)
		   : base(XpoServerModeCore.GetKeyCriteria(initialClassInfo)) {
			this.Session = initialSession;
			this.ClassInfo = initialClassInfo;
			this._FixedCriteria = ExpandFixedFilter(initialFixedCriteria);
			this.DefaultSorting = defaultSorting;
			this.lastPropertyIsExtraKey = lastPropertyIsExtraKey;
			var pdNames = new List<string>();
			var exprs = new List<CriteriaOperator>();
			foreach(var p in props) {
				pdNames.Add(p.Item1);
				exprs.Add(p.Item2);
			}
			this.PDNames = pdNames.ToArray();
			this.SelectExpressions = exprs.ToArray();
		}
		public override bool AllowInvalidFilterCriteria => true;
		sealed class ViewProps2ClassPropsConverter : ClientCriteriaLazyPatcherBase {
			readonly XpoViewServerModeCore InfoSource;
			public ViewProps2ClassPropsConverter(XpoViewServerModeCore _InfoSource) {
				InfoSource = _InfoSource;
			}
			public override CriteriaOperator Visit(AggregateOperand theOperand) {
				throw new NotSupportedException($"Aggregates on view properties '{theOperand}'");
			}
			public override CriteriaOperator Visit(JoinOperand theOperand) {
				throw new NotSupportedException($"Aggregates on view properties '{theOperand}'");
			}
			public override CriteriaOperator Visit(OperandProperty theOperand) {
				var i = Array.IndexOf(InfoSource.PDNames, theOperand.PropertyName);
				if(i < 0)
					throw new ArgumentException($"View property not found '{theOperand.PropertyName}'");
				var selectExpr = InfoSource.SelectExpressions[i];
				if(selectExpr.Is<OperandProperty>(seop => seop.PropertyName == theOperand.PropertyName))
					return theOperand;
				else
					return selectExpr;
			}
		}
		ViewProps2ClassPropsConverter _ConverterInstance;
		protected override CriteriaOperator ExtractExpressionCore(CriteriaOperator d) {
			if(_ConverterInstance == null)
				_ConverterInstance = new ViewProps2ClassPropsConverter(this);
			var converted = _ConverterInstance.Process(d);
			var expanded = EnsurePersistent(converted);
			return base.ExtractExpressionCore(expanded);
		}
		sealed class CompilerDescriptor : CriteriaCompilerDescriptor {
			static Func<object[], object> GetPropertyAccessor(XpoViewServerModeCore _Core, string propertyName) {
				var propIndex = Array.IndexOf(_Core.PDNames, propertyName);
				if(propIndex < 0)
					throw new InvalidOperationException($"'{propertyName}' not found in PDNames");
				return row => row != null ? row[propIndex] : null;
			}
			public static Func<object, object> Compile(XpoViewServerModeCore _Core, CriteriaOperator op) {
				if(op.ReferenceEqualsNull())
					return x => null;
				OperandProperty prop;
				if(op.Is<OperandProperty>(out prop)) {
					var acc = GetPropertyAccessor(_Core, prop.PropertyName);
					return row => acc((object[])row);
				}
				return CriteriaCompiler.ToUntypedDelegate(op, new CompilerDescriptor(_Core));
			}
			readonly XpoViewServerModeCore Core;
			public CompilerDescriptor(XpoViewServerModeCore _Core) {
				this.Core = _Core;
			}
			public override Type ObjectType => typeof(object[]);
			public override Expression MakePropertyAccess(Expression baseExpression, string propertyPath) {
				return Expression.Invoke(Expression.Constant(GetPropertyAccessor(Core, propertyPath)), baseExpression);
			}
			public override Type ResolvePropertyType(Expression baseExpression, string propertyPath) {
				return typeof(object);
			}
		}
		protected override Func<object, object> GetOnInstanceEvaluator(CriteriaOperator dirtyExpression, CriteriaOperator extractedExpression) {
			return CompilerDescriptor.Compile(this, dirtyExpression);
		}
		protected override ServerModeCore DXClone() {
			return base.DXClone();
		}
		protected override ServerModeCore DXCloneCreate() {
			return new XpoViewServerModeCore(this.Session, this.ClassInfo, this.FixedCriteria, this.PDNames.Select((name, i) => Tuple.Create(name, SelectExpressions[i])), this.DefaultSorting, this.lastPropertyIsExtraKey);
		}
		protected override ServerModeCache CreateCacheCore() {
			XpoViewServerModeCache rv = new XpoViewServerModeCache(Session, ClassInfo, FixedCriteria & this.FilterClause, this.SelectExpressions, this.KeysCriteria, this.SortInfo, this.GroupCount, this.SummaryInfo, this.TotalSummaryInfo);
			rv.IsFetchRowsGoodIdeaForSureHint_FullestPossibleCriteria = this.FixedCriteria & this.FilterClause;
			return rv;
		}
		public virtual void SetFixedCriteria(CriteriaOperator op) {
			if(ReferenceEquals(_FixedCriteria, op))
				return;
			_FixedCriteria = ExpandFixedFilter(op);
			Refresh();
		}
		CriteriaOperator FixedCriteria {
			get { return _FixedCriteria; }
		}
		protected CriteriaOperator EnsurePersistent(CriteriaOperator op) {
			ExpandedCriteriaHolder h = PersistentCriterionExpander.Expand(Session, ClassInfo, op);
			if(h.RequiresPostProcessing)
				throw new ArgumentException(Res.GetString(Res.PersistentAliasExpander_NonPersistentCriteria, CriteriaOperator.ToString(op), h.PostProcessingCause));
			return h.ExpandedCriteria;
		}
		CriteriaOperator ExpandFixedFilter(CriteriaOperator op) {
			try {
				op = EnsurePersistent(op);
				if(ForceCaseInsensitiveForAnySource)
					op = StringsTolowerCloningHelper.Process(op);
				return op;
			}
			catch(Exception e) {
				RaiseExceptionThrown(new ServerModeExceptionThrownEventArgs(e));
			}
			return LogicalFalseStub;
		}
		public override IList GetAllFilteredAndSortedRows() {
			CriteriaOperatorCollection props = new CriteriaOperatorCollection();
			props.AddRange(this.SelectExpressions);
			CriteriaOperator filter = FixedCriteria & FilterClause;
			SortingCollection sorting = XpoServerModeCache.OrderDescriptorsToSortingCollection(SortInfo.SelectMany(d => d));
			IList<object[]> data = Session.SelectData(ClassInfo, props, filter, false, 0, 0, sorting);
			return data.ToArray();
		}
		protected override object[] GetUniqueValues(CriteriaOperator expression, int maxCount, CriteriaOperator filter) {
			int top;
			if(maxCount > 0)
				top = maxCount;
			else
				top = 0;
			CriteriaOperatorCollection props = new CriteriaOperatorCollection();
			props.Add(expression);
			SortingCollection sorting = new SortingCollection(new SortProperty(expression, SortingDirection.Ascending));
			IList<object[]> selected = Session.SelectData(ClassInfo, props, FixedCriteria & filter, props, null, false, 0, top, sorting);
			return selected.Select(r => r[0]).ToArray();
		}
		public override void Refresh() {
			base.Refresh();
			ListChanged?.Invoke(this, new ListChangedEventArgs(ListChangedType.Reset, -1));
		}
		#region IBindingList Members
		void IBindingList.AddIndex(PropertyDescriptor property) { }
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
			throw new NotSupportedException();
		}
		int IBindingList.Find(PropertyDescriptor property, object key) {
			throw new NotSupportedException();
		}
		public event ListChangedEventHandler ListChanged;
		bool IBindingList.IsSorted {
			get {
				throw new NotSupportedException();
			}
		}
		void IBindingList.RemoveIndex(PropertyDescriptor property) { }
		void IBindingList.RemoveSort() {
			throw new NotSupportedException();
		}
		ListSortDirection IBindingList.SortDirection {
			get {
				throw new NotSupportedException();
			}
		}
		PropertyDescriptor IBindingList.SortProperty {
			get {
				throw new NotSupportedException();
			}
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
		#region ITypedList Members
		string ITypedList.GetListName(PropertyDescriptor[] listAccessors) {
			return ClassMetadataHelper.GetListName(listAccessors);
		}
		PropertyDescriptorCollection propertyDescriptors;
		PropertyDescriptorCollection ITypedList.GetItemProperties(PropertyDescriptor[] listAccessors) {
			if(listAccessors != null && listAccessors.Length > 0) {
				return new PropertyDescriptorCollection(null);
			}
			if(propertyDescriptors != null) {
				return propertyDescriptors;
			}
			propertyDescriptors = new PropertyDescriptorCollection(null);
			int pdNamesCount = lastPropertyIsExtraKey ? PDNames.Length - 1 : PDNames.Length;
			for(int i = 0; i < pdNamesCount; i++) {
				Type type = CriteriaTypeResolver.ResolveType(ClassInfo, SelectExpressions[i]);
				propertyDescriptors.Add(new XpoViewServerModePropertyDescriptor(PDNames[i], type, i));
			}
			return propertyDescriptors;
		}
		#endregion
	}
	sealed class XpoViewServerModePropertyDescriptor : PropertyDescriptor {
		readonly int index;
		readonly Type reportedType;
		public XpoViewServerModePropertyDescriptor(string name, Type type, int index)
			: base(name, Array.Empty<Attribute>()) {
			this.index = index;
			this.reportedType = type;
		}
		public override bool IsReadOnly { get { return true; } }
		public override object GetValue(object component) {
			IList list = component as IList;
			if(list == null) {
				return null;
			}
			return list[index];
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
}
namespace DevExpress.Xpo {
	[DXToolboxItem(true)]
	[DevExpress.Utils.ToolboxTabName(AssemblyInfo.DXTabOrmComponents)]
	[Designer("DevExpress.Xpo.Design.XPServerModeViewDesigner, " + AssemblyInfo.SRAssemblyXpoDesignFull, Aliases.IDesigner)]
#if !NET
	[System.Drawing.ToolboxBitmap(typeof(XPServerModeView))]
#endif
	[Description("Allows arbitrary combinations of calculated and aggregated values to be retrieved from a data store. Can serve as a data source for data-aware controls in server mode (working with large datasets).")]
	[DefaultProperty("ObjectClassInfo")]
	public class XPServerModeView : Component, ISupportInitializeNotification, IListSource, IXPClassInfoAndSessionProvider, IColumnsServerActions, IDXCloneable {
		Session _Session;
		XPClassInfo _ClassInfo;
		Type _Type;
		CriteriaOperator _FixedFilter;
		IList _List;
		IList List {
			get {
				if(_List == null) {
					_List = CreateList();
				}
				return _List;
			}
		}
		public XPServerModeView() {
		}
		public XPServerModeView(IContainer container)
			: this() {
			container.Add(this);
		}
		public XPServerModeView(Session session, XPClassInfo objectClassInfo, CriteriaOperator fixedFilterCriteria)
			: this() {
			this._Session = session;
			this._ClassInfo = objectClassInfo;
			this._FixedFilter = fixedFilterCriteria;
		}
		public XPServerModeView(Session session, XPClassInfo objectClassInfo) : this(session, objectClassInfo, null) { }
		public XPServerModeView(Session session, Type objectType, CriteriaOperator fixedFilterCriteria) : this(session, session.GetClassInfo(objectType), fixedFilterCriteria) { }
		public XPServerModeView(Session session, Type objectType) : this(session, session.GetClassInfo(objectType)) { }
		bool? _isDesignMode;
#if DEBUGTEST
		[Description("")]
		[Browsable(false)]
		public bool IsDesignMode {
#else
		protected bool IsDesignMode {
#endif
			get {
				return _isDesignMode == true || DevExpress.Data.Helpers.IsDesignModeHelper.GetIsDesignModeBypassable(this, ref _isDesignMode);
			}
		}
		private IList CreateList() {
			if(IsDesignMode) {
				XPView designView = new XPView();
				designView.Site = this.Site;
				return designView;
			}
			else {
				if(IsInitialized()) {
					XpoViewServerModeCore result = CreateServerModeCore();
					result.InconsistencyDetected += new EventHandler<ServerModeInconsistencyDetectedEventArgs>(result_InconsistencyDetected);
					result.ExceptionThrown += new EventHandler<ServerModeExceptionThrownEventArgs>(result_ExceptionThrown);
					return result;
				}
				else {
					return Array.Empty<object>();
				}
			}
		}
		protected virtual XpoViewServerModeCore CreateServerModeCore() {
			bool isKeyAddedToLastItem;
			var props = GetPropertiesForServerModeCore(out isKeyAddedToLastItem);
			string sorting = GetSortingForServerModeCore();
			return new XpoViewServerModeCore(Session, ObjectClassInfo, FixedFilterCriteria, props, sorting, isKeyAddedToLastItem);
		}
		List<Tuple<string, CriteriaOperator>> GetPropertiesForServerModeCore(out bool isKeyAddedToLastItem) {
			var props = new List<Tuple<string, CriteriaOperator>>();
			bool isKeyIncluded = false;
			isKeyAddedToLastItem = false;
			XPMemberInfo keyMemberInfo = (ObjectClassInfo != null) ? ObjectClassInfo.KeyProperty : null;
			if(Properties.Count > 0) {
				foreach(ServerViewProperty prop in Properties) {
					props.Add(Tuple.Create(prop.Name, prop.Property));
					if(!isKeyIncluded && keyMemberInfo != null) {
						OperandProperty operandProperty = (prop.Property as OperandProperty);
						if(!ReferenceEquals(operandProperty, null)) {
							if(string.Compare(operandProperty.PropertyName, keyMemberInfo.Name, true) == 0) {
								isKeyIncluded = true;
							}
						}
					}
				}
			}
			else if(ObjectClassInfo != null) {
				var members = ObjectClassInfo.PersistentProperties.Cast<XPMemberInfo>().Union(ObjectClassInfo.Members.Where(t => t.IsAliased));
				foreach(XPMemberInfo mi in members) {
					if(mi == keyMemberInfo || mi is ServiceField) {
						continue;
					}
					DisplayNameAttribute dnAttribute = mi.FindAttributeInfo(typeof(DisplayNameAttribute)) as DisplayNameAttribute;
					string name = dnAttribute == null ? mi.Name : dnAttribute.DisplayName;
					props.Add(Tuple.Create<string, CriteriaOperator>(name, new OperandProperty(mi.Name)));
				}
			}
			if(!isKeyIncluded && keyMemberInfo != null) {
				string keyName = keyMemberInfo.Name;
				while(props.Any(t => t.Item1 == keyName)) {
					keyName += "_";
				}
				props.Add(Tuple.Create<string, CriteriaOperator>(keyName, new OperandProperty(keyMemberInfo.Name)));
				isKeyAddedToLastItem = true;
			}
			return props;
		}
		string GetSortingForServerModeCore() {
			if(Properties.Count == 0) {
				return null;
			}
			StringBuilder sb = new StringBuilder();
			foreach(ServerViewProperty prop in Properties) {
				if(prop.Sorting != SortDirection.None) {
					if(sb.Length != 0) {
						sb.Append(';');
					}
					sb.Append(prop.Name);
					if(prop.Sorting == SortDirection.Ascending) {
						sb.Append(" ASC");
					}
					else {
						sb.Append(" DESC");
					}
				}
			}
			return sb.ToString();
		}
		void KillList() {
			_List = null;
		}
		void Reset() {
			if(!IsDesignMode) {
				KillList();
			}
			else {
				SetupDesignViewProperties();
				((XPView)List).Reset(true);
			}
		}
		void SetupDesignViewProperties() {
			bool isKeyAddedToLastItem;
			List<Tuple<string, CriteriaOperator>> props = GetPropertiesForServerModeCore(out isKeyAddedToLastItem);
			if(isKeyAddedToLastItem) {
				props.RemoveAt(props.Count - 1);
			}
			XPView view = (XPView)List;
			view.Properties.Clear();
			foreach(var prop in props) {
				string name = prop.Item1;
				CriteriaOperator criteria = prop.Item2;
				view.Properties.Add(new ViewProperty(name, SortDirection.None, criteria, false, true));
			}
		}
		public void PopulateProperties() {
			if(ObjectClassInfo == null) {
				return;
			}
			Properties.Clear();
			var members = ObjectClassInfo.PersistentProperties.Cast<XPMemberInfo>().Union(ObjectClassInfo.Members.Where(t => t.IsAliased));
			foreach(XPMemberInfo mi in members) {
				if(mi.IsKey || mi is ServiceField) {
					continue;
				}
				DisplayNameAttribute dnAttribute = mi.FindAttributeInfo(typeof(DisplayNameAttribute)) as DisplayNameAttribute;
				string name = dnAttribute == null ? mi.Name : dnAttribute.DisplayName;
				AddProperty(name, new OperandProperty(mi.Name));
			}
		}
		bool ShouldSerializeSession() { return !(Session is DefaultSession); }
		void ResetSession() { Session = null; }
		[Description("Gets or sets a session used to load persistent objects.")]
		[TypeConverter("DevExpress.Xpo.Design.SessionReferenceConverter, " + AssemblyInfo.SRAssemblyXpoDesignFull)]
		[RefreshProperties(RefreshProperties.All)]
		[Category("Data")]
		public Session Session {
			get {
				if(IsDesignMode)
					return ((ISessionProvider)List).Session;
				if(_Session == null) {
					_Session = DoResolveSession();
				}
				return _Session;
			}
			set {
				if(IsDesignMode) {
					((XPView)List).Session = value;
					return;
				}
				if(_Session == value)
					return;
				if(!this.IsInit)
					throw new InvalidOperationException(Res.GetString(Res.Collections_CannotAssignProperty, "Session", GetType().Name));
				_Session = value;
				if(_ClassInfo != null) {
					_Type = _ClassInfo.ClassType;
					_ClassInfo = null;
				}
				KillList();
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
			if(_ResolveSession != null)
				_ResolveSession(this, args);
		}
		event ResolveSessionEventHandler _ResolveSession;
		public event ResolveSessionEventHandler ResolveSession {
			add {
				_ResolveSession += value;
			}
			remove {
				_ResolveSession -= value;
			}
		}
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
		[DefaultValue(null)]
		public Type ObjectType {
			get {
				return ObjectClassInfo != null ? ObjectClassInfo.ClassType : null;
			}
			set {
				if(IsDesignMode) {
					((XPView)List).ObjectType = value;
					SetupDesignViewProperties();
					return;
				}
				if(!this.IsInit)
					throw new InvalidOperationException(Res.GetString(Res.Collections_CannotAssignProperty, "ObjectType", GetType().Name));
				_ClassInfo = null;
				_Type = value;
				KillList();
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
		[Description("Gets or sets the XPClassInfo that describes the type of items the target data table contains."), DefaultValue(null)]
		[RefreshProperties(RefreshProperties.All)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		[TypeConverter("DevExpress.Xpo.Design.ObjectClassInfoTypeConverter, " + AssemblyInfo.SRAssemblyXpoDesignFull)]
		[MergableProperty(false)]
		[Category("Data")]
		public XPClassInfo ObjectClassInfo {
			get {
				if(IsDesignMode) {
					return ((XPView)List).ObjectClassInfo;
				}
				if(_ClassInfo == null && _Type != null && Session != null) {
					_ClassInfo = IsDesignMode ? DesignDictionary.GetClassInfo(_Type) : Session.GetClassInfo(_Type);
				}
				return _ClassInfo;
			}
			set {
				if(ObjectClassInfo == value)
					return;
				if(IsDesignMode) {
					XPView designHelper = (XPView)List;
					designHelper.ObjectType = null;
					designHelper.ObjectClassInfo = value;
					FixedFilterCriteria = null;
					SetupDesignViewProperties();
					return;
				}
				if(!this.IsInit)
					throw new InvalidOperationException(Res.GetString(Res.Collections_CannotAssignProperty, "ObjectClassInfo", GetType().Name));
				_ClassInfo = value;
				KillList();
			}
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
				_FixedFilter = value;
				if(!IsDesignMode) {
					XpoViewServerModeCore ds = List as XpoViewServerModeCore;
					if(ds != null)
						ds.SetFixedCriteria(FixedFilterCriteria);
					else
						KillList();
				}
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
		protected bool IsInit = false;
		event EventHandler Initialized;
		event EventHandler ISupportInitializeNotification.Initialized {
			add { Initialized += value; }
			remove { Initialized -= value; }
		}
		bool ISupportInitializeNotification.IsInitialized {
			get {
				return IsInitialized();
			}
		}
		protected bool IsInitialized() {
			if(IsInit)
				return false;
			return ObjectClassInfo != null;
		}
		void ISupportInitialize.BeginInit() {
			if(IsDesignMode)
				return;
			if(IsInit)
				throw new InvalidOperationException();
			KillList();
			IsInit = true;
		}
		void ISupportInitialize.EndInit() {
			if(IsDesignMode)
				return;
			if(!IsInit)
				throw new InvalidOperationException();
			IsInit = false;
			KillList();
			if(Initialized != null && IsInitialized())
				Initialized(this, EventArgs.Empty);
		}
		#region ISessionProvider Members
		Session ISessionProvider.Session {
			get { return Session; }
		}
		#endregion
		#region IDataLayerProvider Members
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
		#endregion
		#region IXPDictionaryProvider Members
		XPDictionary DevExpress.Xpo.Metadata.Helpers.IXPDictionaryProvider.Dictionary {
			get {
				if(Session == null)
					return null;
				return IsDesignMode ? DesignDictionary : Session.Dictionary;
			}
		}
		#endregion
		#region IListSource Members
		bool IListSource.ContainsListCollection {
			get { return false; }
		}
		IList IListSource.GetList() {
			return List;
		}
		#endregion
		ServerViewPropertiesCollection viewProperties;
		[Description("Gets a ServerViewPropertiesCollection object that contains information on a persistent type’s property names, criteria, and sort order.")]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
		[Category("View")]
		public ServerViewPropertiesCollection Properties {
			get {
				if(viewProperties == null) {
					viewProperties = new ServerViewPropertiesCollection(this);
					viewProperties.CollectionChanged += (sender, e) => {
						Reset();
					};
				}
				return viewProperties;
			}
		}
		public ServerViewProperty AddProperty(CriteriaOperator property) {
			string name;
			if(property is OperandProperty)
				name = ((OperandProperty)property).PropertyName;
			else
				name = "Column" + (Properties.Count + 1).ToString(CultureInfo.InvariantCulture);
			return AddProperty(name, property);
		}
		public ServerViewProperty AddProperty(string property) {
			return AddProperty(CriteriaOperator.Parse(property));
		}
		public ServerViewProperty AddProperty(string name, string property) {
			return AddProperty(name, CriteriaOperator.Parse(property), SortDirection.None);
		}
		public ServerViewProperty AddProperty(string name, CriteriaOperator property) {
			return AddProperty(name, property, SortDirection.None);
		}
		public ServerViewProperty AddProperty(string name, string property, SortDirection sorting) {
			return AddProperty(name, CriteriaOperator.Parse(property), sorting);
		}
		public ServerViewProperty AddProperty(string name, CriteriaOperator property, SortDirection sorting) {
			ServerViewProperty prop = new ServerViewProperty(name, sorting, property);
			Properties.Add(prop);
			if(!IsDesignMode) {
				KillList();
				Reload();
			}
			return prop;
		}
		public void Reload() {
			XpoViewServerModeCore src = List as XpoViewServerModeCore;
			if(src != null)
				src.Refresh();
		}
		protected virtual void OnServerExceptionThrown(ServerExceptionThrownEventArgs e) {
			if(_ServerExceptionThrown != null)
				_ServerExceptionThrown(this, e);
		}
		event ServerExceptionThrownEventHandler _ServerExceptionThrown;
		public event ServerExceptionThrownEventHandler ServerExceptionThrown {
			add {
				_ServerExceptionThrown += value;
			}
			remove {
				_ServerExceptionThrown -= value;
			}
		}
		void result_ExceptionThrown(object sender, ServerModeExceptionThrownEventArgs e) {
			FatalException(e.Exception);
		}
		protected virtual void FatalException(Exception e) {
			ServerExceptionThrownEventArgs args = new ServerExceptionThrownEventArgs(e);
			OnServerExceptionThrown(args);
			if(args.Action == ServerExceptionThrownAction.Rethrow)
				ExceptionDispatchInfo.Capture(e).Throw();
		}
		void result_InconsistencyDetected(object sender, ServerModeInconsistencyDetectedEventArgs e) {
			Inconsistent(e);
			if(e.Handled)
				return;
			e.Handled = true;
			InconsistentHelper.PostponedInconsistent(() => Reload(), null);
		}
		protected virtual void Inconsistent(ServerModeInconsistencyDetectedEventArgs e) {
		}
		XPClassInfo IXPClassInfoProvider.ClassInfo {
			get { return ObjectClassInfo; }
		}
		bool IColumnsServerActions.AllowAction(string fieldName, ColumnServerActionType action) {
			return XpoServerModeCore.IColumnsServerActionsAllowAction(Session, ObjectClassInfo, fieldName);
		}
		object IDXCloneable.DXClone() {
			return DXClone();
		}
		protected virtual object DXClone() {
			XPServerModeView clone = DXCloneCreate();
			clone._ClassInfo = this._ClassInfo;
			clone._FixedFilter = this._FixedFilter;
			clone._ServerExceptionThrown = this._ServerExceptionThrown;
			clone._Session = this._Session;
			clone._Type = this._Type;
			clone._ResolveSession = this._ResolveSession;
			clone.Properties.AddRangeAsCopy(this.Properties);
			return clone;
		}
		protected virtual XPServerModeView DXCloneCreate() {
			return new XPServerModeView();
		}
	}
	[TypeConverter(typeof(ServerViewPropertyConverter))]
	public class ServerViewProperty : IDataColumnInfoProvider, INotifyPropertyChanged {
		string name;
		SortDirection sorting;
		CriteriaOperator property;
		IXPClassInfoProvider owner;
		protected internal void SetOwner(IXPClassInfoProvider owner) {
			this.owner = owner;
		}
		[Description("Gets or sets the property’s name.")]
		[DefaultValue("")]
		[Category("Data")]
		public string Name {
			get { return name; }
			set {
				name = value;
				if(PropertyChanged != null) {
					PropertyChanged(this, new PropertyChangedEventArgs(nameof(Name)));
				}
			}
		}
		[Description("Gets or sets the sort order for the property values to be retrieved from the data server."), DefaultValue(SortDirection.None)]
		[Category("Data")]
		public SortDirection Sorting {
			get { return sorting; }
			set {
				sorting = value;
				if(PropertyChanged != null) {
					PropertyChanged(this, new PropertyChangedEventArgs(nameof(Sorting)));
				}
			}
		}
		[Description("Gets or sets the criteria to calculate property values.")]
		[TypeConverter("DevExpress.Xpo.Design.CriteriaConverter, " + AssemblyInfo.SRAssemblyXpoDesignFull)]
		[Editor("DevExpress.Xpo.Design.XPViewExpressionEditor, " + AssemblyInfo.SRAssemblyXpoDesignFull, DevExpress.Utils.ControlConstants.UITypeEditor)]
		[Category("Data")]
		public CriteriaOperator Property {
			get { return property; }
			set {
				property = value;
				if(string.IsNullOrEmpty(Name)) {
					var operandProperty = value as OperandProperty;
					if(!ReferenceEquals(operandProperty, null)) {
						name = operandProperty.PropertyName;
					}
				}
				if(PropertyChanged != null) {
					PropertyChanged(this, new PropertyChangedEventArgs(nameof(Property)));
				}
			}
		}
		bool ShouldSerializeProperty() {
			return !ReferenceEquals(property, null);
		}
		public ServerViewProperty() {
			name = string.Empty;
			sorting = SortDirection.None;
		}
		public ServerViewProperty(string name, string property)
			: this(name, SortDirection.None, property) {
		}
		public ServerViewProperty(string name, SortDirection sorting, CriteriaOperator property) {
			this.name = name;
			this.property = property;
			this.sorting = sorting;
		}
		public ServerViewProperty(string name, SortDirection sorting, string property)
			: this(name, sorting, CriteriaOperator.Parse(property)) {
		}
		#region IDataColumnInfoProvider Members
		IDataColumnInfo IDataColumnInfoProvider.GetInfo(object arguments) {
			return new ServerViewPropertyIDataPropertyInfoWrapper(this);
		}
		#endregion
		internal class ServerViewPropertyMemberIDataPropertyInfoWrapper : IDataColumnInfo {
			readonly XPMemberInfo memberInfo;
			readonly List<IDataColumnInfo> columns = new List<IDataColumnInfo>(0);
			public ServerViewPropertyMemberIDataPropertyInfoWrapper(XPMemberInfo memberInfo) {
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
		internal class ServerViewPropertyIDataPropertyInfoWrapper : IDataColumnInfo {
			readonly ServerViewProperty column;
			public ServerViewPropertyIDataPropertyInfoWrapper(ServerViewProperty column) {
				this.column = column;
			}
			string IDataColumnInfo.Caption { get { return column.Name; } }
			List<IDataColumnInfo> IDataColumnInfo.Columns {
				get {
					List<IDataColumnInfo> res = new List<IDataColumnInfo>();
					if(column.owner == null || column.owner.ClassInfo == null) return res;
					foreach(XPMemberInfo mi in column.owner.ClassInfo.PersistentProperties) {
						if(mi.IsKey || !(mi is ServiceField)) {
							res.Add(new ServerViewPropertyMemberIDataPropertyInfoWrapper(mi));
						}
					}
					foreach(XPMemberInfo mi in column.owner.ClassInfo.Members) {
						if(mi.IsAliased) {
							res.Add(new ServerViewPropertyMemberIDataPropertyInfoWrapper(mi));
						}
					}
					foreach(XPMemberInfo miCol in column.owner.ClassInfo.CollectionProperties) {
						res.Add(new ServerViewPropertyMemberIDataPropertyInfoWrapper(miCol));
					}
					return res;
				}
			}
			DataControllerBase IDataColumnInfo.Controller { get { return null; } }
			string IDataColumnInfo.FieldName { get { return ReferenceEquals(column.Property, null) ? "" : column.Property.ToString(); } }
			Type IDataColumnInfo.FieldType {
				get {
					if(column.owner == null || column.owner.ClassInfo == null) return typeof(object);
					return CriteriaTypeResolver.ResolveType(column.owner.ClassInfo, column.Property);
				}
			}
			string IDataColumnInfo.Name { get { return column.Name; } }
			string IDataColumnInfo.UnboundExpression { get { return ReferenceEquals(column.Property, null) ? "" : column.Property.ToString(); } }
		}
		public event PropertyChangedEventHandler PropertyChanged;
	}
	class ServerViewPropertyConverter : TypeConverter {
		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType) {
			if(destinationType == typeof(InstanceDescriptor))
				return true;
			return base.CanConvertTo(context, destinationType);
		}
		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object val, Type destinationType) {
			ServerViewProperty prop = val as ServerViewProperty;
			if(destinationType == typeof(InstanceDescriptor) && prop != null) {
				ConstructorInfo ctor = typeof(ServerViewProperty).GetConstructor(new Type[] { typeof(string), typeof(SortDirection), typeof(string) });
				if(ctor != null) {
					return new InstanceDescriptor(ctor, new object[] { prop.Name, prop.Sorting, ReferenceEquals(prop.Property, null) ? String.Empty : prop.Property.ToString() });
				}
			}
			return base.ConvertTo(context, culture, val, destinationType);
		}
	}
	[ListBindable(BindableSupport.No)]
	[Editor("DevExpress.Xpo.Design.ServerViewPropertiesCollectionEditor," + AssemblyInfo.SRAssemblyXpoDesignFull, DevExpress.Utils.ControlConstants.UITypeEditor)]
	public sealed class ServerViewPropertiesCollection : CollectionBase, INotifyCollectionChanged {
		IXPClassInfoProvider owner;
		public ServerViewPropertiesCollection() { }
		public ServerViewPropertiesCollection(IXPClassInfoProvider owner)
			: base() {
			this.owner = owner;
		}
		public void Add(ServerViewProperty property) {
			((IList)this).Add(property);
		}
		public void AddRange(ServerViewProperty[] properties) {
			foreach(ServerViewProperty sp in properties)
				Add(sp);
		}
		public void AddRangeAsCopy(IEnumerable properties) {
			foreach(ServerViewProperty sp in properties) {
				var copy = new ServerViewProperty(sp.Name, sp.Sorting, sp.Property);
				copy.PropertyChanged += OnItemPropertyChanged;
				Add(copy);
			}
		}
		protected override void OnInsertComplete(int index, object value) {
			((ServerViewProperty)value).SetOwner(owner);
			((ServerViewProperty)value).PropertyChanged += OnItemPropertyChanged;
			if(CollectionChanged != null) {
				CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, new object[] { value }, index));
			}
		}
		protected override void OnRemoveComplete(int index, object value) {
			((ServerViewProperty)value).SetOwner(null);
			((ServerViewProperty)value).PropertyChanged -= OnItemPropertyChanged;
			if(CollectionChanged != null) {
				CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, new object[] { value }, index));
			}
		}
		protected override void OnClear() {
			base.OnClear();
			foreach(ServerViewProperty sp in this) {
				sp.PropertyChanged -= OnItemPropertyChanged;
			}
		}
		protected override void OnClearComplete() {
			base.OnClearComplete();
			if(CollectionChanged != null) {
				CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			}
		}
		protected override void OnSetComplete(int index, object oldValue, object newValue) {
			base.OnSetComplete(index, oldValue, newValue);
			if(CollectionChanged != null) {
				CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, newValue, oldValue));
			}
		}
		public ServerViewProperty this[int index] { get { return (ServerViewProperty)List[index]; } }
		public ServerViewProperty this[CriteriaOperator expression] {
			get {
				foreach(ServerViewProperty prop in this) {
					if(Equals(expression, prop.Property))
						return prop;
				}
				return null;
			}
		}
		public ServerViewProperty this[string name] {
			get {
				foreach(ServerViewProperty prop in this) {
					if(Equals(name, prop.Name))
						return prop;
				}
				return null;
			}
		}
		public event NotifyCollectionChangedEventHandler CollectionChanged;
		void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
			if(CollectionChanged != null) {
				CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, sender, sender));
			}
		}
	}
}
