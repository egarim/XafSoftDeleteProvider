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
using System.Linq;
using DevExpress.Data;
using DevExpress.Data.Filtering;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.Metadata;
namespace DevExpress.Xpo.Helpers {
	using DevExpress.Xpo;
	using DevExpress.Data.Filtering.Helpers;
	using System.Collections.Specialized;
	using DevExpress.Xpo.DB.Helpers;
	using DevExpress.Xpo.Metadata.Helpers;
	using DevExpress.Data.Helpers;
#if !NET
	using DevExpress.Data.NetCompatibility.Extensions;
#endif
	public class XpoServerModeCache : ServerModeKeyedCache {
		public readonly Session Session;
		public readonly XPClassInfo ClassInfo;
		public readonly CriteriaOperator ExternalCriteria;
		public XpoServerModeCache(Session session, XPClassInfo classInfo, CriteriaOperator externalCriteria, CriteriaOperator[] keyCriteria, ServerModeOrderDescriptor[][] sortInfo, int groupCount, ServerModeSummaryDescriptor[] summary, ServerModeSummaryDescriptor[] totalSummary)
			: base(keyCriteria, sortInfo, groupCount, summary, totalSummary) {
			this.Session = session;
			this.ClassInfo = classInfo;
			this.ExternalCriteria = externalCriteria;
		}
		protected override Func<object, object> GetOnInstanceEvaluator(CriteriaOperator criteriaOperator) {
			return CriteriaCompiler.ToUntypedDelegate(criteriaOperator, ClassInfo.GetCriteriaCompilerDescriptor(Session), new CriteriaCompilerAuxSettings(Session.CaseSensitive, Session.Dictionary.CustomFunctionOperators));
		}
		public static SortingCollection OrderDescriptorsToSortingCollection(IEnumerable<ServerModeOrderDescriptor> order) {
			List<SortProperty> result = new List<SortProperty>();
			foreach(ServerModeOrderDescriptor ord in order) {
				SortProperty sp = new SortProperty(ord.SortExpression, ord.IsDesc ? SortingDirection.Descending : SortingDirection.Ascending);
				result.Add(sp);
			}
			return new SortingCollection(result.ToArray());
		}
		protected override object[] FetchKeys(CriteriaOperator where, ServerModeOrderDescriptor[] order, int skip, int take) {
			CriteriaOperatorCollection props = new CriteriaOperatorCollection();
			props.AddRange(this.KeysCriteria);
			CriteriaOperator filter = ExternalCriteria & where;
			SortingCollection sorting = OrderDescriptorsToSortingCollection(order);
			IList<object[]> keys = Session.SelectData(ClassInfo, props, filter, false, skip, take, sorting);
			if(this.KeysCriteria.Length == 1)
				return keys.Select(ks => ks[0]).ToArray();
			else
				return keys.Select(ks => new ServerModeCompoundKey(ks)).ToArray();
		}
		protected override object[] FetchRows(CriteriaOperator where, ServerModeOrderDescriptor[] order, int take) {
			CriteriaOperator filter = ExternalCriteria & where;
			SortingCollection sorting = OrderDescriptorsToSortingCollection(order);
			ICollection objects = Session.GetObjects(ClassInfo, filter, sorting, take, false, false);
			return ListHelper.FromCollection(objects).ToArray();
		}
		bool UseGetObjectsByKeysForFetchRowsByKeys {
			get {
				return this.KeysCriteria.Length == 1 && ClassInfo.OptimisticLockField == null && ReferenceEquals(ExternalCriteria, null);
			}
		}
		protected override int MagicNumberMaxPageSizeForFillKeysToFetchListWeb {
			get {
				if(UseGetObjectsByKeysForFetchRowsByKeys)
					return int.MaxValue;
				else
					return base.MagicNumberMaxPageSizeForFillKeysToFetchListWeb;
			}
		}
		protected override object[] FetchRowsByKeys(object[] keys) {
			if(UseGetObjectsByKeysForFetchRowsByKeys)
				return Session.GetObjectsByKey(ClassInfo, keys, false).OfType<object>().ToArray();
			else
				return base.FetchRowsByKeys(keys);
		}
		protected override int GetCount(CriteriaOperator criteriaOperator) {
			return ((int?)Session.Evaluate(ClassInfo, AggregateOperand.TopLevel(Aggregate.Count), ExternalCriteria & criteriaOperator)) ?? 0;
		}
		protected override Func<object, object> GetKeyComponentFromRowGetter(CriteriaOperator keyComponent) {
			var op = keyComponent as OperandProperty;
			if(!ReferenceEquals(op, null)) {
				var mi = ClassInfo.GetMember(op.PropertyName);
				if(mi.ReferenceType == null)
					return row => mi.GetValue(row);
				else
					return row => this.Session.GetKeyValue(mi.GetValue(row));
			}
			return base.GetKeyComponentFromRowGetter(keyComponent);
		}
		internal static Type ExtractKeyPropertyType(XPMemberInfo mi) {
			if(mi.SubMembers.Count > 0)
				throw new InvalidOperationException("mi.SubMembers.Count > 0");
			else if(mi.ReferenceType != null)
				return ExtractKeyPropertyType(mi.ReferenceType.KeyProperty);
			else
				return mi.StorageType;
		}
		protected override Type ResolveKeyType(CriteriaOperator singleKeyCriterion) {
			var op = singleKeyCriterion as OperandProperty;
			if(ReferenceEquals(op, null))
				throw new ArgumentException(null, nameof(singleKeyCriterion));
			string propertyName = op.PropertyName;
			var prop = ClassInfo.GetMember(propertyName);
			return ExtractKeyPropertyType(prop);
		}
		protected override Type ResolveRowType() {
			return typeof(object);
		}
		public static XPClassInfo ObjectExpressionType(XPClassInfo ci, CriteriaOperator expression) {
			OperandProperty prop = expression as OperandProperty;
			if(ReferenceEquals(prop, null))
				return null;
			string path = prop.PropertyName;
			if(string.IsNullOrEmpty(path))
				return null;
			MemberInfoCollection mic = ci.ParsePersistentPath(path);
			return mic[mic.Count - 1].ReferenceType;
		}
		internal static ServerModeGroupInfoData[] PrepareChildrenCommon(Session session, XPClassInfo classInfo, CriteriaOperator externalFilter, Func<CriteriaOperator, XPClassInfo> objectExpressionTypeGetter, CriteriaOperator groupWhere, CriteriaOperator[] groupByCriteria, CriteriaOperator[] orderByCriteria, bool[] isDescOrder, ServerModeSummaryDescriptor[] summaries) {
			int posCount = 0;
			int posGroupValues = posCount + 1;
			int posGroupValuesCount = groupByCriteria.Length;
			int posSummary = posGroupValues + posGroupValuesCount;
			int posSummaryCount = summaries.Length;
			CriteriaOperatorCollection props = new CriteriaOperatorCollection();
			props.Add(AggregateOperand.TopLevel(Aggregate.Count));
			props.AddRange(groupByCriteria);
			props.AddRange(summaries.Select(sd => ConvertToAggregate(sd)));
			CriteriaOperatorCollection groupBy = new CriteriaOperatorCollection();
			groupBy.AddRange(groupByCriteria);
			SortingCollection sorting = new SortingCollection(Enumerable.Range(0, orderByCriteria.Length).Select(i => new SortProperty(orderByCriteria[i], isDescOrder[i] ? SortingDirection.Descending : SortingDirection.Ascending)).ToArray());
			List<object[]> selected = session.SelectData(classInfo, props, externalFilter & groupWhere, groupBy, null, false, 0, 0, sorting);
			int[] counts = new int[selected.Count];
			List<object[]> groupValues = new List<object[]>(selected.Count);
			List<object[]> summaryValues = new List<object[]>(selected.Count);
			summaryValues.AddRange(Enumerable.Range(0, selected.Count).Select(i => new object[posSummaryCount]));
			groupValues.AddRange(Enumerable.Range(0, selected.Count).Select(i => new object[posGroupValuesCount]));
			List<int> groupValuesDirectCopyIndices = new List<int>();
			List<int> groupValuesObjectsIndices = new List<int>();
			List<XPClassInfo> groupValuesObjectsTypes = new List<XPClassInfo>();
			List<object[]> groupValuesObjectsKeys = new List<object[]>(selected.Count);
			for(int i = 0; i < posGroupValuesCount; ++i) {
				var groupByObjectType = objectExpressionTypeGetter(groupByCriteria[i]);
				if(groupByObjectType == null) {
					groupValuesDirectCopyIndices.Add(i);
				}
				else {
					groupValuesObjectsIndices.Add(i);
					groupValuesObjectsTypes.Add(groupByObjectType);
					groupValuesObjectsKeys.Add(new object[selected.Count]);
				}
			}
			for(int rowIndex = 0; rowIndex < selected.Count; ++rowIndex) {
				var row = selected[rowIndex];
				counts[rowIndex] = (int)row[posCount];
				if(posSummaryCount > 0) {
					var summ = summaryValues[rowIndex];
					for(int i = 0; i < posSummaryCount; ++i) {
						summ[i] = row[posSummary + i];
					}
				}
				if(groupValuesDirectCopyIndices.Count > 0) {
					var grp = groupValues[rowIndex];
					for(int i = 0; i < groupValuesDirectCopyIndices.Count; ++i) {
						var directCopyIndex = groupValuesDirectCopyIndices[i];
						grp[directCopyIndex] = row[posGroupValues + directCopyIndex];
					}
				}
				for(int i = 0; i < groupValuesObjectsIndices.Count; ++i) {
					groupValuesObjectsKeys[i][rowIndex] = row[posGroupValues + groupValuesObjectsIndices[i]];
				}
			}
			for(int i = 0; i < groupValuesObjectsIndices.Count; ++i) {
				var keys = groupValuesObjectsKeys[i];
				groupValuesObjectsKeys[i] = null;
				var objects = ListHelper.FromCollection(session.GetObjectsByKey(groupValuesObjectsTypes[i], keys, false));
				int pos = groupValuesObjectsIndices[i];
				for(int rowIndex = 0; rowIndex < groupValues.Count; ++rowIndex) {
					groupValues[rowIndex][pos] = objects[rowIndex];
				}
			}
			return Enumerable.Range(0, groupValues.Count).Select(i => new ServerModeGroupInfoData(groupValues[i], counts[i], summaryValues[i])).ToArray();
		}
		protected override ServerModeGroupInfoData[] PrepareChildren(CriteriaOperator groupWhere, CriteriaOperator[] groupByCriteria, CriteriaOperator[] orderByCriteria, bool[] isDescOrder, ServerModeSummaryDescriptor[] summaries) {
			return PrepareChildrenCommon(Session, ClassInfo, ExternalCriteria, c => ObjectExpressionType(ClassInfo, c)
				, groupWhere, groupByCriteria, orderByCriteria, isDescOrder, summaries);
		}
		internal static ServerModeGroupInfoData PrepareTopGroupInfoCommon(ServerModeSummaryDescriptor[] summaries, Session session, XPClassInfo classInfo, CriteriaOperator externalFilter) {
			CriteriaOperatorCollection props = new CriteriaOperatorCollection();
			props.Add(AggregateOperand.TopLevel(Aggregate.Count));
			foreach(ServerModeSummaryDescriptor d in summaries) {
				props.Add(ConvertToAggregate(d));
			}
			IList<object[]> selected = session.SelectData(classInfo, props, externalFilter, false, 0, 0, null);
			if(selected.Count == 0) {
				return new ServerModeGroupInfoData(null, 0, new object[summaries.Length]);
			}
			else if(selected.Count == 1) {
				List<object> rv = new List<object>(selected[0]);
				int count = ((int?)rv[0]) ?? 0;
				rv.RemoveAt(0);
				return new ServerModeGroupInfoData(null, count, rv.ToArray());
			}
			else {
				throw new InvalidOperationException(Res.GetString(Res.ServerModeGridSource_WrongTopLevelAggregate));
			}
		}
		protected override ServerModeGroupInfoData PrepareTopGroupInfo(ServerModeSummaryDescriptor[] summaries) {
			return PrepareTopGroupInfoCommon(summaries, Session, ClassInfo, ExternalCriteria);
		}
		static CriteriaOperator ConvertToAggregate(ServerModeSummaryDescriptor d) {
			CriteriaOperator op;
			switch(d.SummaryType) {
				case Aggregate.Count:
					op = AggregateOperand.TopLevel(Aggregate.Count);
					break;
				case Aggregate.Avg:
				case Aggregate.Max:
				case Aggregate.Min:
				case Aggregate.Sum:
					op = AggregateOperand.TopLevel(d.SummaryType, d.SummaryExpression);
					break;
				case Aggregate.Custom:
					op = AggregateOperand.TopLevel(d.SummaryCustomAggregateName, d.SummaryCustomAggregateExpressions);
					break;
				default:
					throw new NotSupportedException(Res.GetString(Res.ServerModeGridSource_SummaryItemTypeNotSupported, d.SummaryType.ToString()));
			}
			return op;
		}
	}
	public interface IXpoServerModeGridDataSource :
		IListServerHints, IXtraRefreshable, IDXCloneable,
		IFilteredXtraBindingList, ITypedList,
		IListServer, IXPClassInfoAndSessionProvider, IColumnsServerActions {
		void SetFixedCriteria(CriteriaOperator fixedCriteria);
	}
	public class XpoServerModeCore : ServerModeCore, IXpoServerModeGridDataSource {
		internal static CriteriaOperator[] GetKeyCriteria(XPClassInfo ci) {
			if(!ci.IsPersistent) {
				throw new NotSupportedException(Res.GetString(Res.XpoServerMode_NonPersistentIsNotSupported, ci.FullName));
			}
			var key = ci.KeyProperty;
			switch(key.SubMembers.Count) {
				case 0:
					return new CriteriaOperator[] { new OperandProperty(key.Name) };
				case 1:
					return new CriteriaOperator[] { new OperandProperty(((XPMemberInfo)key.SubMembers[0]).Name) };
				default:
					return key.SubMembers.Cast<XPMemberInfo>().Where(mi => mi.IsPersistent).Select(mi => new OperandProperty(mi.Name)).ToArray<CriteriaOperator>();
			}
		}
		public XpoServerModeCore(Session initialSession, XPClassInfo initialClassInfo, CriteriaOperator initialFixedCriteria, string displayableProps, string defaultSorting)
			: base(GetKeyCriteria(initialClassInfo)) {
			this._Session = initialSession;
			this._ClassInfo = initialClassInfo;
			this._FixedCriteria = ExpandFilter(initialSession, initialClassInfo, initialFixedCriteria);
			this._DisplayableProperties = displayableProps;
			this.DefaultSorting = defaultSorting;
		}
		public override bool AllowInvalidFilterCriteria => true;
		protected override ServerModeCore DXClone() {
			return base.DXClone();
		}
		protected override ServerModeCore DXCloneCreate() {
			return new XpoServerModeCore(this.Session, this.ClassInfo, this.FixedCriteria, this.DisplayableProperties, this.DefaultSorting);
		}
		protected override ServerModeCache CreateCacheCore() {
			XpoServerModeCache rv = new XpoServerModeCache(Session, ClassInfo, FixedCriteria & this.FilterClause, this.KeysCriteria, this.SortInfo, this.GroupCount, this.SummaryInfo, this.TotalSummaryInfo);
			rv.IsFetchRowsGoodIdeaForSureHint_FullestPossibleCriteria = this.FixedCriteria & this.FilterClause;
			return rv;
		}
		CriteriaOperator _FixedCriteria;
		public virtual void SetFixedCriteria(CriteriaOperator op) {
			if(ReferenceEquals(_FixedCriteria, op))
				return;
			_FixedCriteria = ExpandFilter(Session, ClassInfo, op);
			Refresh();
		}
		public CriteriaOperator FixedCriteria {
			get { return _FixedCriteria; }
		}
		CriteriaOperator IFilteredDataSource.Filter {
			get { return FixedCriteria; }
			set { SetFixedCriteria(value); }
		}
		protected CriteriaOperator ExpandFilter(IPersistentValueExtractor session, XPClassInfo ci, CriteriaOperator op) {
			ExpandedCriteriaHolder h = PersistentCriterionExpander.Expand(session, ci, op);
			if(h.RequiresPostProcessing)
				throw new ArgumentException(Res.GetString(Res.PersistentAliasExpander_NonPersistentCriteria, CriteriaOperator.ToString(op), h.PostProcessingCause));
			int exceptions = 0;
			return ExtractExpressionLogical(h.ExpandedCriteria, ref exceptions);
		}
		Session _Session;
		public Session Session {
			get { return _Session; }
		}
		XPClassInfo _ClassInfo;
		public XPClassInfo ClassInfo {
			get { return _ClassInfo; }
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
		public XPDictionary Dictionary {
			get {
				if(this.Session == null)
					return null;
				return Session.Dictionary;
			}
		}
		public IObjectLayer ObjectLayer {
			get {
				if(this.Session == null)
					return null;
				return Session.ObjectLayer;
			}
		}
		public IDataLayer DataLayer {
			get {
				if(this.Session == null)
					return null;
				return Session.DataLayer;
			}
		}
		public static bool IColumnsServerActionsAllowAction(Session session, XPClassInfo ci, string fieldName) {
			if(ci == null || fieldName == null)
				return true;
			if(session == null) {
				try {
					var path = ci.ParsePath(fieldName);
					return !path.HasNonPersistent;
				}
				catch {
					return true;
				}
			}
			else {
				try {
					var expanded = PersistentCriterionExpander.Expand(session, ci, new OperandProperty(fieldName));
					return !expanded.RequiresPostProcessing;
				}
				catch {
					return true;
				}
			}
		}
		bool IColumnsServerActions.AllowAction(string fieldName, ColumnServerActionType action) {
			return IColumnsServerActionsAllowAction(Session, ClassInfo, fieldName);
		}
		static readonly OperandProperty ThisCriterion = new OperandProperty("This");
		protected override CriteriaOperator ExtractExpressionCore(CriteriaOperator input) {
			if(Equals(ThisCriterion, input))
				throw new ArgumentException(Res.GetString(Res.PersistentAliasExpander_NonPersistentCriteriaThisValueMember, CriteriaOperator.ToString(input), CriteriaOperator.ToString(ThisCriterion)));
			CriteriaOperator op = base.ExtractExpressionCore(input);
			ExpandedCriteriaHolder expressionExpanded = PersistentCriterionExpander.Expand(ClassInfo, Session, op);
			if(expressionExpanded.RequiresPostProcessing)
				throw new ArgumentException(Res.GetString(Res.PersistentAliasExpander_NonPersistentCriteria, CriteriaOperator.ToString(op), expressionExpanded.PostProcessingCause));
			return expressionExpanded.ExpandedCriteria;
		}
		string _DisplayableProperties = null;
		public string DisplayableProperties {
			get {
				if(_DisplayableProperties == null)
					_DisplayableProperties = string.Empty;
				return _DisplayableProperties;
			}
		}
		class ItemProperties : DevExpress.Xpo.Helpers.ClassMetadataHelper.ItemProperties {
			public ItemProperties(XpoServerModeCore context) : base(context) { }
			public override string GetDisplayableProperties() {
				return ((XpoServerModeCore)Context).DisplayableProperties;
			}
		}
		ItemProperties itemProperties;
		public virtual PropertyDescriptorCollection GetItemProperties(PropertyDescriptor[] listAccessors) {
			if(itemProperties == null)
				itemProperties = new ItemProperties(this);
			return ClassMetadataHelper.GetItemProperties(itemProperties, listAccessors);
		}
		public virtual string GetListName(PropertyDescriptor[] listAccessors) {
			return ClassMetadataHelper.GetListName(listAccessors);
		}
		public override object[] GetUniqueColumnValuesCore(CriteriaOperator valuesExpression, int maxCount, CriteriaOperator filterExpression, bool ignoreAppliedFilter) {
			int exceptions = 0;
			CriteriaOperator expression = ExtractExpressionValue(valuesExpression, ref exceptions);
			if(exceptions > 0)
				return Array.Empty<object>();
			CriteriaOperator filter = ignoreAppliedFilter ? filterExpression : filterExpression & FilterClause;
			return WithReentryProtection(() => GetUniqueValues(valuesExpression, expression, maxCount, filter));
		}
		protected override object[] GetUniqueValues(CriteriaOperator expression, int maxCount, CriteriaOperator filter) {
			return GetUniqueValues(expression, expression, maxCount, filter);
		}
		object[] GetUniqueValues(CriteriaOperator originalExpression, CriteriaOperator expandedExpression, int maxCount, CriteriaOperator filter) {
			int top;
			if(maxCount > 0)
				top = maxCount;
			else
				top = 0;
			CriteriaOperatorCollection props = new CriteriaOperatorCollection();
			props.Add(expandedExpression);
			SortingCollection sorting = new SortingCollection(new SortProperty(expandedExpression, SortingDirection.Ascending));
			IList<object[]> selected = Session.SelectData(ClassInfo, props, FixedCriteria & filter, props, null, false, 0, top, sorting);
			List<object> rv = new List<object>(selected.Count);
			foreach(object[] row in selected) {
				rv.Add(row[0]);
			}
			XPClassInfo objectExpressionType = GetObjectExpressionType(originalExpression);
			if(objectExpressionType == null) {
				objectExpressionType = XpoServerModeCache.ObjectExpressionType(ClassInfo, expandedExpression);
			}
			if(objectExpressionType != null) {
				rv = ListHelper.FromCollection(Session.GetObjectsByKey(objectExpressionType, rv, false));
			}
			else {
				Type expressionType = CriteriaTypeResolver.ResolveType(ClassInfo, originalExpression);
				expressionType = Nullable.GetUnderlyingType(expressionType) ?? expressionType;
				if(expressionType.IsEnum) {
					ConvertItemsToEnum(rv, expressionType);
				}
			}
			return rv.ToArray();
		}
		XPClassInfo GetObjectExpressionType(CriteriaOperator originalExpression) {
			var property = originalExpression as OperandProperty;
			if(ReferenceEquals(property, null) || property.PropertyName == null || !property.PropertyName.EndsWith('!')) {
				return null;
			}
			string[] parts = property.PropertyName.Split('.');
			for(int i = 0; i < parts.Length; i++) {
				if(parts[i].EndsWith('!')) {
					parts[i] = parts[i].Substring(0, parts[i].Length - 1);
				}
				else if(parts[i].EndsWith("!Key", StringComparison.OrdinalIgnoreCase)) {
					return null;
				}
			}
			string path = string.Join(".", parts);
			MemberInfoCollection mic = ClassInfo.ParsePath(path);
			if(!mic.HasNonPersistent) {
				return mic[mic.Count - 1].ReferenceType;
			}
			return null;
		}
		void ConvertItemsToEnum(IList<object> list, Type enumType) {
			Type enumUnderlyingType = enumType.GetEnumUnderlyingType();
			for(int i = 0; i < list.Count; i++) {
				object v = list[i];
				if(v != null && v.GetType() == enumUnderlyingType) {
					list[i] = Enum.ToObject(enumType, v);
				}
			}
		}
		public override void Refresh() {
			base.Refresh();
			if(ListChanged != null)
				ListChanged(this, new ListChangedEventArgs(ListChangedType.Reset, -1));
		}
		public override IList GetAllFilteredAndSortedRows() {
			return ListHelper.FromCollection(Session.GetObjects(ClassInfo, FixedCriteria & FilterClause, XpoServerModeCache.OrderDescriptorsToSortingCollection(SortInfo.SelectMany(os => os)), 0, 0, false, false));
		}
	}
	public abstract class XpoServerCollectionWrapperBase : IXpoServerModeGridDataSource {
		public readonly IXpoServerModeGridDataSource Nested;
		protected XpoServerCollectionWrapperBase(IXpoServerModeGridDataSource nested) {
			this.Nested = nested;
		}
		public virtual XPClassInfo ClassInfo {
			get { return Nested.ClassInfo; }
		}
		public XPDictionary Dictionary {
			get { return Nested.Dictionary; }
		}
		public Session Session {
			get { return Nested.Session; }
		}
		public IObjectLayer ObjectLayer {
			get { return Nested.ObjectLayer; }
		}
		public IDataLayer DataLayer {
			get { return Nested.DataLayer; }
		}
		public event EventHandler<ServerModeInconsistencyDetectedEventArgs> InconsistencyDetected { add { Nested.InconsistencyDetected += value; } remove { Nested.InconsistencyDetected -= value; } }
		public event EventHandler<ServerModeExceptionThrownEventArgs> ExceptionThrown { add { Nested.ExceptionThrown += value; } remove { Nested.ExceptionThrown -= value; } }
		public virtual PropertyDescriptorCollection GetItemProperties(PropertyDescriptor[] listAccessors) {
			return Nested.GetItemProperties(listAccessors);
		}
		public virtual string GetListName(PropertyDescriptor[] listAccessors) {
			return Nested.GetListName(listAccessors);
		}
		public virtual void Apply(CriteriaOperator filterCriteria, ICollection<ServerModeOrderDescriptor[]> sortInfo, int groupCount, ICollection<ServerModeSummaryDescriptor> summaryInfo, ICollection<ServerModeSummaryDescriptor> totalSummaryInfo) {
			Nested.Apply(filterCriteria, sortInfo, groupCount, summaryInfo, totalSummaryInfo);
		}
		public virtual int FindIncremental(CriteriaOperator expression, string value, int startIndex, bool searchUp, bool ignoreStartRow, bool allowLoop) {
			return Nested.FindIncremental(expression, value, startIndex, searchUp, ignoreStartRow, allowLoop);
		}
		public virtual int LocateByValue(CriteriaOperator expression, object value, int startIndex, bool searchUp) {
			return Nested.LocateByValue(expression, value, startIndex, searchUp);
		}
		public virtual int LocateByExpression(CriteriaOperator expression, int startIndex, bool searchUp) {
			return Nested.LocateByExpression(expression, startIndex, searchUp);
		}
		public virtual List<ListSourceGroupInfo> GetGroupInfo(ListSourceGroupInfo parentGroup) {
			return Nested.GetGroupInfo(parentGroup);
		}
		public virtual int GetRowIndexByKey(object key) {
			return Nested.GetRowIndexByKey(key);
		}
		public virtual object GetRowKey(int index) {
			return Nested.GetRowKey(index);
		}
		public virtual List<object> GetTotalSummary() {
			return Nested.GetTotalSummary();
		}
		public virtual object[] GetUniqueColumnValues(CriteriaOperator valuesExpression, int maxCount, CriteriaOperator filterExpression, bool ignoreAppliedFilter) {
			return Nested.GetUniqueColumnValues(valuesExpression, maxCount, filterExpression, ignoreAppliedFilter);
		}
		public virtual int Add(object value) {
			return Nested.Add(value);
		}
		public virtual void Clear() {
			Nested.Clear();
		}
		public virtual bool Contains(object value) {
			return Nested.Contains(value);
		}
		public virtual int IndexOf(object value) {
			return Nested.IndexOf(value);
		}
		public virtual void Insert(int index, object value) {
			Nested.Insert(index, value);
		}
		public virtual bool IsFixedSize {
			get { return Nested.IsFixedSize; }
		}
		public virtual bool IsReadOnly {
			get { return Nested.IsReadOnly; }
		}
		public virtual void Remove(object value) {
			Nested.Remove(value);
		}
		public virtual void RemoveAt(int index) {
			Nested.RemoveAt(index);
		}
		public virtual object this[int index] {
			get {
				return Nested[index];
			}
			set {
				Nested[index] = value;
			}
		}
		public virtual void CopyTo(Array array, int index) {
			Nested.CopyTo(array, index);
		}
		public virtual int Count {
			get { return Nested.Count; }
		}
		public virtual bool IsSynchronized {
			get { return Nested.IsSynchronized; }
		}
		public object SyncRoot {
			get { return Nested.SyncRoot; }
		}
		public virtual IEnumerator GetEnumerator() {
			return Nested.GetEnumerator();
		}
		void IBindingList.AddIndex(PropertyDescriptor property) {
			Nested.AddIndex(property);
		}
		public virtual object AddNew() {
			return Nested.AddNew();
		}
		public virtual bool AllowEdit {
			get { return Nested.AllowEdit; }
		}
		public virtual bool AllowNew {
			get { return Nested.AllowNew; }
		}
		public virtual bool AllowRemove {
			get { return Nested.AllowRemove; }
		}
		void IBindingList.ApplySort(PropertyDescriptor property, ListSortDirection direction) {
			Nested.ApplySort(property, direction);
		}
		int IBindingList.Find(PropertyDescriptor property, object key) {
			return Nested.Find(property, key);
		}
		bool IBindingList.IsSorted {
			get { return Nested.IsSorted; }
		}
		void IBindingList.RemoveIndex(PropertyDescriptor property) {
			Nested.RemoveIndex(property);
		}
		void IBindingList.RemoveSort() {
			Nested.RemoveSort();
		}
		ListSortDirection IBindingList.SortDirection {
			get { return Nested.SortDirection; }
		}
		PropertyDescriptor IBindingList.SortProperty {
			get { return Nested.SortProperty; }
		}
		public virtual bool SupportsChangeNotification {
			get { return Nested.SupportsChangeNotification; }
		}
		bool IBindingList.SupportsSearching {
			get { return Nested.SupportsSearching; }
		}
		bool IBindingList.SupportsSorting {
			get { return Nested.SupportsSorting; }
		}
		CriteriaOperator IFilteredDataSource.Filter {
			get { return Nested.Filter; }
			set { Nested.Filter = value; }
		}
		public virtual event ListChangedEventHandler ListChanged {
			add { Nested.ListChanged += value; }
			remove { Nested.ListChanged -= value; }
		}
		public virtual void Refresh() {
			((IListServer)Nested).Refresh();
		}
		public virtual bool RefreshSupported => true;
		public virtual void SetFixedCriteria(CriteriaOperator fixedCriteria) {
			Nested.SetFixedCriteria(fixedCriteria);
		}
		public virtual bool AllowAction(string fieldName, ColumnServerActionType action) {
			return Nested.AllowAction(fieldName, action);
		}
		public virtual IList GetAllFilteredAndSortedRows() {
			return Nested.GetAllFilteredAndSortedRows();
		}
		public virtual bool PrefetchRows(ListSourceGroupInfo[] groupsToPrefetch, System.Threading.CancellationToken cancellationToken) {
			return Nested.PrefetchRows(groupsToPrefetch, cancellationToken);
		}
		void IListServerHints.HintGridIsPaged(int pageSize) {
			IListServerHints n = Nested as IListServerHints;
			if(n == null)
				return;
			n.HintGridIsPaged(pageSize);
		}
		void IListServerHints.HintMaxVisibleRowsInGrid(int rowsInGrid) {
			IListServerHints n = Nested as IListServerHints;
			if(n == null)
				return;
			n.HintMaxVisibleRowsInGrid(rowsInGrid);
		}
		public abstract object DXClone();
	}
	public class XpoServerCollectionChangeTracker : XpoServerCollectionWrapperBase {
		public XpoServerCollectionChangeTracker(IXpoServerModeGridDataSource nested) : base(nested) {
		}
		public override object DXClone() {
			return new XpoServerCollectionChangeTracker((IXpoServerModeGridDataSource)Nested.DXClone());
		}
		void OnNestedSessionCommiting(object sender, SessionManipulationEventArgs e) {
			Session.BeforeCommitNestedUnitOfWork -= new SessionManipulationEventHandler(OnNestedSessionCommited);
			Session.BeforeCommitNestedUnitOfWork += new SessionManipulationEventHandler(OnNestedSessionCommited);
			Session.ObjectLoaded -= new ObjectManipulationEventHandler(OnObjectLoaded);
			Session.ObjectLoaded += new ObjectManipulationEventHandler(OnObjectLoaded);
		}
		void OnNestedSessionCommited(object sender, SessionManipulationEventArgs e) {
			Session.BeforeCommitNestedUnitOfWork -= new SessionManipulationEventHandler(OnNestedSessionCommited);
			Session.ObjectLoaded -= new ObjectManipulationEventHandler(OnObjectLoaded);
		}
		void OnNestedListChanged(object sender, ListChangedEventArgs e) {
			RaiseChanged(e);
		}
		void OnObjectChanged(object sender, ObjectChangeEventArgs e) {
			if(e.Reason != ObjectChangeReason.PropertyChanged)
				return;
			NotifyObjectChangedIfNeeded(sender);
		}
		void OnObjectLoaded(object sender, ObjectManipulationEventArgs e) {
			NotifyObjectChangedIfNeeded(e.Object);
		}
		void NotifyObjectChangedIfNeeded(object obj) {
			XPClassInfo ci = Dictionary.QueryClassInfo(obj);
			if(ci == null)
				return;
			if(!ci.IsAssignableTo(ClassInfo))
				return;
			int index = Nested.IndexOf(obj);
			if(index >= 0) {
				RaiseChanged(new ListChangedEventArgs(ListChangedType.ItemChanged, index));
			}
		}
		event ListChangedEventHandler _ListChanged;
		public override event ListChangedEventHandler ListChanged {
			add {
				_ListChanged += value;
				EventsSubscribeUnSubscribe();
			}
			remove {
				_ListChanged -= value;
				EventsSubscribeUnSubscribe();
			}
		}
		void EventsSubscribeUnSubscribe() {
			Nested.ListChanged -= new ListChangedEventHandler(OnNestedListChanged);
			Session.ObjectChanged -= new ObjectChangeEventHandler(OnObjectChanged);
			Session.BeforeCommitNestedUnitOfWork -= new SessionManipulationEventHandler(OnNestedSessionCommiting);
			if(_ListChanged != null) {
				Nested.ListChanged += new ListChangedEventHandler(OnNestedListChanged);
				Session.ObjectChanged += new ObjectChangeEventHandler(OnObjectChanged);
				Session.BeforeCommitNestedUnitOfWork += new SessionManipulationEventHandler(OnNestedSessionCommiting);
			}
		}
		protected virtual void RaiseChanged(ListChangedEventArgs e) {
			if(_ListChanged == null)
				return;
			_ListChanged(this, e);
		}
		public override bool SupportsChangeNotification {
			get {
				return true;
			}
		}
	}
	public class XpoServerCollectionFlagger : XpoServerCollectionWrapperBase {
		readonly bool allowEdit;
		readonly bool allowAddNew;
		readonly bool allowRemove;
		public XpoServerCollectionFlagger(IXpoServerModeGridDataSource nested, bool allowEdit, bool allowAddNew, bool allowRemove) : base(nested) {
			this.allowEdit = allowEdit;
			this.allowAddNew = allowAddNew;
			this.allowRemove = allowRemove;
		}
		public override object DXClone() {
			return new XpoServerCollectionFlagger((IXpoServerModeGridDataSource)Nested.DXClone(), allowEdit, allowAddNew, allowRemove);
		}
		public override bool AllowEdit {
			get {
				return allowEdit;
			}
		}
		public override bool AllowNew {
			get {
				return base.AllowNew && allowAddNew;
			}
		}
		public override bool AllowRemove {
			get {
				return base.AllowRemove && allowRemove;
			}
		}
		public override bool IsReadOnly {
			get {
				return !(AllowEdit || AllowNew || AllowRemove);
			}
		}
	}
	public class XpoServerCollectionAdderRemover : XpoServerCollectionWrapperBase {
		readonly bool deleteOnRemove;
		List<object> addedItems = new List<object>();
		ObjectSet addedItemsDictionary = new ObjectSet();
		ObjectSet removedItemsDictionary = new ObjectSet();
		int currentGroupDepth = 0;
		bool CollectionInModifiedState { get { return addedItemsDictionary.Count != 0 || removedItemsDictionary.Count != 0; } }
		bool AddingRemovingAllowed { get { return currentGroupDepth == 0; } }
		public XpoServerCollectionAdderRemover(IXpoServerModeGridDataSource nested, bool deleteOnRemove)
			: base(nested) {
			this.deleteOnRemove = deleteOnRemove;
			Nested.ListChanged += new ListChangedEventHandler(OnNestedListChanged);
		}
		public override object DXClone() {
			if(CollectionInModifiedState)
				throw new InvalidOperationException("Can't clone modified collection");
			return new XpoServerCollectionAdderRemover((IXpoServerModeGridDataSource)Nested.DXClone(), deleteOnRemove);
		}
		public override int Add(object value) {
			int index = IndexOf(value);
			if(index >= 0)
				return index;
			if(!AddingRemovingAllowed)
				throw new InvalidOperationException(Res.GetString(Res.ServerModeGridSource_GroupAndAddOrRemoveIsNotAllowed));
			if(removedItemsDictionary.Contains(value)) {
				removedItemsDictionary.Remove(value);
			}
			else {
				addedItemsDictionary.Add(value);
				addedItems.Add(value);
			}
			index = IndexOf(value);
			RaiseChanged(new ListChangedEventArgs(ListChangedType.ItemAdded, index));
			return index;
		}
		public override void Remove(object value) {
			RemoveAt(IndexOf(value));
		}
		public override void RemoveAt(int index) {
			System.Diagnostics.Debug.Assert(addedItemsDictionary.Count == addedItems.Count);
			if(index < 0 || index >= Count)
				return;
			if(!AddingRemovingAllowed)
				throw new InvalidOperationException(Res.GetString(Res.ServerModeGridSource_GroupAndAddOrRemoveIsNotAllowed));
			object obj = this[index];
			if(addedItemsDictionary.Contains(obj)) {
				addedItemsDictionary.Remove(obj);
				addedItems.Remove(obj);
			}
			else {
				removedItemsDictionary.Add(obj);
			}
			RaiseChanged(new ListChangedEventArgs(ListChangedType.ItemDeleted, index));
			if(this.deleteOnRemove)
				Session.Delete(obj);
		}
		public override int Count {
			get {
				return base.Count + addedItemsDictionary.Count - removedItemsDictionary.Count;
			}
		}
		public override void Apply(CriteriaOperator filterCriteria, ICollection<ServerModeOrderDescriptor[]> sortInfo, int groupCount, ICollection<ServerModeSummaryDescriptor> summaryInfo, ICollection<ServerModeSummaryDescriptor> totalSummaryInfo) {
			if(groupCount > 0 && CollectionInModifiedState)
				throw new InvalidOperationException(Res.GetString(Res.ServerModeGridSource_GroupAndAddOrRemoveIsNotAllowed));
			currentGroupDepth = groupCount;
			base.Apply(filterCriteria, sortInfo, groupCount, summaryInfo, totalSummaryInfo);
			ValidateLists();
		}
		public override bool Contains(object value) {
			if(addedItemsDictionary.Contains(value))
				return true;
			if(removedItemsDictionary.Contains(value))
				return false;
			return base.Contains(value);
		}
		public override void CopyTo(Array array, int index) {
			throw new NotSupportedException();
		}
		public override IEnumerator GetEnumerator() {
			throw new NotSupportedException();
		}
		public override List<ListSourceGroupInfo> GetGroupInfo(ListSourceGroupInfo parentGroup) {
			return base.GetGroupInfo(parentGroup);
		}
		public override int GetRowIndexByKey(object key) {
			if(!CollectionInModifiedState)
				return base.GetRowIndexByKey(key);
			if(key == null)
				return -1;
			XPClassInfo ci = Dictionary.QueryClassInfo(key);
			object obj;
			if(ci != null && ci.IsAssignableTo(ClassInfo)) {
				obj = key;
			}
			else {
				try {
					obj = Session.GetObjectByKey(ClassInfo, key);
				}
				catch {
					obj = null;
				}
			}
			return IndexOf(obj);
		}
		public override object GetRowKey(int index) {
			object obj = this[index];
			if(obj == null)
				return null;
			if(Session.IsNewObject(obj))
				return null;
			return Session.GetKeyValue(obj);
		}
		public override List<object> GetTotalSummary() {
			return base.GetTotalSummary();
		}
		public override int IndexOf(object value) {
			if(value == null)
				return -1;
			if(addedItemsDictionary.Contains(value)) {
				for(int i = 0; ; ++i) {
					if(ReferenceEquals(addedItems[i], value))
						return base.Count - removedItemsDictionary.Count + i;
				}
			}
			else if(removedItemsDictionary.Contains(value)) {
				return -1;
			}
			else {
				return IndexFromBase(base.IndexOf(value));
			}
		}
		protected int IndexFromBase(int baseIndex) {
			if(baseIndex < 0)
				return -1;
			int outerIndex = baseIndex;
			foreach(object obj in removedItemsDictionary) {
				int removedObjIndex = base.IndexOf(obj);
				if(removedObjIndex >= 0 && removedObjIndex < baseIndex)
					--outerIndex;
			}
			return outerIndex;
		}
		protected int IndexToBase(int outerIndex) {
			if(removedItemsDictionary.Count == 0) {
				return outerIndex;
			}
			else {
				int[] map = new int[removedItemsDictionary.Count];
				int mapIndex = 0;
				foreach(object obj in removedItemsDictionary) {
					int objIndex = base.IndexOf(obj);
					map[mapIndex++] = objIndex;
				}
				Array.Sort<int>(map);
				int baseIndex = outerIndex;
				foreach(int deletedObjIndex in map) {
					if(deletedObjIndex > baseIndex)
						break;
					baseIndex++;
				}
				return baseIndex;
			}
		}
		public override object this[int index] {
			get {
				System.Diagnostics.Debug.Assert(addedItemsDictionary.Count == addedItems.Count);
				int addedIndex = index - base.Count + removedItemsDictionary.Count;
				if(addedIndex >= 0) {
					return addedItems[addedIndex];
				}
				else {
					return base[IndexToBase(index)];
				}
			}
			set {
				throw new NotSupportedException();
			}
		}
		public override bool IsReadOnly {
			get {
				return true;
			}
		}
		public override bool IsFixedSize {
			get {
				return !AddingRemovingAllowed;
			}
		}
		public override void Insert(int index, object value) {
			Add(value);
		}
		public override bool AllowRemove {
			get {
				return AddingRemovingAllowed;
			}
		}
		public override bool AllowNew {
			get {
				return AddingRemovingAllowed;
			}
		}
		public override void Refresh() {
			this.addedItems.Clear();
			this.addedItemsDictionary.Clear();
			this.removedItemsDictionary.Clear();
			base.Refresh();
		}
		void ValidateLists() {
			for(int i = addedItems.Count - 1; i >= 0; --i) {
				object obj = addedItems[i];
				if(base.Contains(obj)) {
					addedItemsDictionary.Remove(obj);
					addedItems.RemoveAt(i);
				}
			}
			List<object> removedItemsList = new List<object>(removedItemsDictionary.Count);
			foreach(object obj in removedItemsDictionary) {
				removedItemsList.Add(obj);
			}
			foreach(object obj in removedItemsList) {
				if(!base.Contains(obj)) {
					removedItemsDictionary.Remove(obj);
				}
			}
		}
		void OnNestedListChanged(object sender, ListChangedEventArgs e) {
			ValidateLists();
			RaiseChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
		}
		event ListChangedEventHandler _ListChanged;
		public override event ListChangedEventHandler ListChanged {
			add {
				_ListChanged += value;
			}
			remove {
				_ListChanged -= value;
			}
		}
		protected virtual void RaiseChanged(ListChangedEventArgs e) {
			if(_ListChanged == null)
				return;
			_ListChanged(this, e);
		}
		public override bool SupportsChangeNotification {
			get {
				return true;
			}
		}
		protected virtual object CreateAddNewInstance() {
			return ClassInfo.CreateNewObject(Session);
		}
		object nowAdding;
		public override object AddNew() {
			object obj = CreateAddNewInstance();
			this.Add(obj);
			SubscribeNewlyAdded(obj);
			return obj;
		}
		private void SubscribeNewlyAdded(object obj) {
			IXPObjectWithChangedEvent newlyAdded = obj as IXPObjectWithChangedEvent;
			var handler = new ObjectChangeEventHandler(NewAddedObjectChangedHandler);
			nowAdding = obj;
			if(newlyAdded != null) {
				newlyAdded.Changed += handler;
			}
			else {
				if(Session != null) {
					Session.ObjectChanged -= handler;
					Session.ObjectChanged += handler;
				}
			}
		}
		private void UnsubscribeNewlyAdded(object obj) {
			IXPObjectWithChangedEvent newlyAdded = obj as IXPObjectWithChangedEvent;
			var handler = new ObjectChangeEventHandler(NewAddedObjectChangedHandler);
			nowAdding = null;
			if(newlyAdded != null) {
				newlyAdded.Changed -= handler;
			}
			else {
				if(Session != null) {
					Session.ObjectChanged -= handler;
				}
			}
		}
		void NewAddedObjectChangedHandler(object sender, ObjectChangeEventArgs e) {
			if(sender != nowAdding) {
				return;
			}
			switch(e.Reason) {
				case ObjectChangeReason.EndEdit:
					UnsubscribeNewlyAdded(sender);
					break;
				case ObjectChangeReason.CancelEdit:
					UnsubscribeNewlyAdded(sender);
					Remove(sender);
					break;
			}
		}
		public override IList GetAllFilteredAndSortedRows() {
			List<object> lst = new List<object>(ListHelper.FromCollection(base.GetAllFilteredAndSortedRows()));
			foreach(object o in addedItemsDictionary)
				lst.Remove(o);
			foreach(object o in removedItemsDictionary)
				lst.Remove(o);
			lst.AddRange(addedItems);
			return lst;
		}
		public override bool AllowAction(string fieldName, ColumnServerActionType action) {
			bool allow = base.AllowAction(fieldName, action);
			if(action == ColumnServerActionType.Group) {
				allow = allow && !CollectionInModifiedState;
			}
			return allow;
		}
	}
}
namespace DevExpress.Xpo {
	using System.ComponentModel;
	using System.Runtime.ExceptionServices;
	using DevExpress.Data.Helpers;
	using DevExpress.Utils.Design;
	using DevExpress.Xpo.DB.Helpers;
	using DevExpress.Xpo.Helpers;
	[DXToolboxItem(true)]
	[DevExpress.Utils.ToolboxTabName(AssemblyInfo.DXTabOrmComponents)]
	[Designer("DevExpress.Xpo.Design.XPServerCollectionSourceDesigner, " + AssemblyInfo.SRAssemblyXpoDesignFull, Aliases.IDesigner)]
	[Description("Serves as a data source for data-aware controls in server mode (working with large datasets).")]
#if !NET
	[System.Drawing.ToolboxBitmap(typeof(XPServerCollectionSource))]
#endif
	public class XPServerCollectionSource : Component, ISupportInitializeNotification, IListSource, IXPClassInfoAndSessionProvider, IColumnsServerActions, IDXCloneable {
		Session _Session;
		string displayableProperties;
		XPClassInfo _ClassInfo;
		Type _Type;
		string _DefaultSorting;
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
#if DEBUG
		internal IXpoServerModeGridDataSource ServerList {
			get { return List as IXpoServerModeGridDataSource; }
		}
#endif
		bool _TrackChanges = false;
		bool _AllowEdit = false;
		bool _AllowRemove = false;
		bool _DeleteObjectOnRemove = false;
		bool _AllowNew = false;
		public XPServerCollectionSource() {
		}
		public XPServerCollectionSource(IContainer container)
			: this() {
			container.Add(this);
		}
		public XPServerCollectionSource(Session session, XPClassInfo objectClassInfo, CriteriaOperator fixedFilterCriteria)
			: this() {
			this._Session = session;
			this._ClassInfo = objectClassInfo;
			this._FixedFilter = fixedFilterCriteria;
		}
		public XPServerCollectionSource(Session session, XPClassInfo objectClassInfo) : this(session, objectClassInfo, null) { }
		public XPServerCollectionSource(Session session, Type objectType, CriteriaOperator fixedFilterCriteria) : this(session, session.GetClassInfo(objectType), fixedFilterCriteria) { }
		public XPServerCollectionSource(Session session, Type objectType) : this(session, session.GetClassInfo(objectType)) { }
		[Description("Gets or sets whether the XPServerCollectionSource tracks item changes."), DefaultValue(false)]
		[Category("Options")]
		public bool TrackChanges {
			get { return this._TrackChanges; }
			set {
				if(_List is IXpoServerModeGridDataSource) {
					throw new InvalidOperationException(Res.GetString(Res.Collections_CannotAssignProperty, "TrackChanges", GetType().Name));
				}
				if(TrackChanges == value)
					return;
				this._TrackChanges = value;
			}
		}
		[Description("Gets or sets whether data editing is allowed."), DefaultValue(false)]
		[Category("Options")]
		public bool AllowEdit {
			get { return this._AllowEdit; }
			set {
				if(_List is IXpoServerModeGridDataSource) {
					throw new InvalidOperationException(Res.GetString(Res.Collections_CannotAssignProperty, "AllowEdit", GetType().Name));
				}
				if(AllowEdit == value)
					return;
				this._AllowEdit = value;
			}
		}
		[Description("Gets or sets whether items can be removed from a collection by a bound control."), DefaultValue(false)]
		[Category("Options")]
		public bool AllowRemove {
			get { return this._AllowRemove; }
			set {
				if(_List is IXpoServerModeGridDataSource) {
					throw new InvalidOperationException(Res.GetString(Res.Collections_CannotAssignProperty, "AllowRemove", GetType().Name));
				}
				if(AllowRemove == value)
					return;
				this._AllowRemove = value;
			}
		}
		[Description("Gets or sets whether the persistent object is deleted from the data store when it is removed from the collection."), DefaultValue(false)]
		[Category("Options")]
		public bool DeleteObjectOnRemove {
			get { return this._DeleteObjectOnRemove; }
			set {
				if(_List is IXpoServerModeGridDataSource) {
					throw new InvalidOperationException(Res.GetString(Res.Collections_CannotAssignProperty, "DeleteObjectOnRemove", GetType().Name));
				}
				if(DeleteObjectOnRemove == value)
					return;
				this._DeleteObjectOnRemove = value;
			}
		}
		[Description("Gets or sets whether new items can be added to a collection by a bound control."), DefaultValue(false)]
		[Category("Options")]
		public bool AllowNew {
			get { return this._AllowNew; }
			set {
				if(_List is IXpoServerModeGridDataSource) {
					throw new InvalidOperationException(Res.GetString(Res.Collections_CannotAssignProperty, "AllowNew", GetType().Name));
				}
				if(AllowNew == value)
					return;
				this._AllowNew = value;
			}
		}
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
				XPCollection cll = new XPCollection();
				cll.Site = this.Site;
				return cll;
			}
			else {
				if(IsInitialized()) {
					IXpoServerModeGridDataSource result = EquipServerModeCore(CreateServerModeCore());
					result.InconsistencyDetected += new EventHandler<ServerModeInconsistencyDetectedEventArgs>(result_InconsistencyDetected);
					result.ExceptionThrown += new EventHandler<ServerModeExceptionThrownEventArgs>(result_ExceptionThrown);
					return result;
				}
				else {
					return Array.Empty<object>();
				}
			}
		}
		protected virtual IXpoServerModeGridDataSource EquipServerModeCore(IXpoServerModeGridDataSource result) {
			if(AllowRemove || AllowNew)
				result = new XpoServerCollectionAdderRemover(result, DeleteObjectOnRemove);
			if(AllowEdit || AllowRemove || AllowNew)
				result = new XpoServerCollectionFlagger(result, AllowEdit, AllowNew, AllowRemove);
			if(TrackChanges)
				result = new XpoServerCollectionChangeTracker(result);
			return result;
		}
		protected virtual IXpoServerModeGridDataSource CreateServerModeCore() {
			return new XpoServerModeCore(Session, ObjectClassInfo, FixedFilterCriteria, DisplayableProperties, DefaultSorting);
		}
		void KillList() {
			_List = null;
		}
		bool ShouldSerializeSession() { return !(Session is DefaultSession); }
		void ResetSession() { Session = null; }
		[Description("Gets or sets the Session used by the current XPServerCollectionSource object.")]
		[TypeConverter("DevExpress.Xpo.Design.SessionReferenceConverter, " + AssemblyInfo.SRAssemblyXpoDesignFull)]
		[RefreshProperties(RefreshProperties.All)]
		[Category("Data")]
		public Session Session {
			get {
				if(IsDesignMode)
					return ((XPBaseCollection)List).Session;
				if(_Session == null) {
					_Session = DoResolveSession();
				}
				return _Session;
			}
			set {
				if(IsDesignMode) {
					((XPBaseCollection)List).Session = value;
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
		bool ShouldSerializeDisplayableProperties() {
			return DisplayableProperties != StringListHelper.DelimitedText(ClassMetadataHelper.GetDefaultDisplayableProperties(ObjectClassInfo), ";");
		}
		void ResetDisplayableProperties() {
			DisplayableProperties = null;
		}
		[Description("Gets or sets properties that are available for binding in a bound data-aware control at design time.")]
		[Editor("DevExpress.Xpo.Design.DisplayablePropertiesEditor, " + AssemblyInfo.SRAssemblyXpoDesignFull, DevExpress.Utils.ControlConstants.UITypeEditor)]
		[Category("View")]
		public string DisplayableProperties {
			get {
				if(IsDesignMode)
					return ((XPBaseCollection)List).DisplayableProperties;
				if(String.IsNullOrEmpty(displayableProperties))
					displayableProperties = StringListHelper.DelimitedText(ClassMetadataHelper.GetDefaultDisplayableProperties(ObjectClassInfo), ";");
				return displayableProperties;
			}
			set {
				if(IsDesignMode) {
					((XPBaseCollection)List).DisplayableProperties = value;
					return;
				}
				if(displayableProperties != value) {
					displayableProperties = value;
				}
			}
		}
		bool ShouldSerializeDefaultSorting() {
			return !string.IsNullOrEmpty(DefaultSorting);
		}
		void ResetDefaultSorting() {
			DefaultSorting = null;
		}
		[Description("Specifies how data source contents are sorted by default, when sort order is not specified by the bound control.")]
		[Category("Data")]
		public string DefaultSorting {
			get { return _DefaultSorting; }
			set {
				if(_List is IXpoServerModeGridDataSource) {
					throw new InvalidOperationException(Res.GetString(Res.Collections_CannotAssignProperty, "DefaultSorting", GetType().Name));
				}
				this._DefaultSorting = value;
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
					((XPCollection)List).ObjectType = value;
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
		[Description("Gets a XPClassInfo object that describes the target data table in the data store."), DefaultValue(null)]
		[RefreshProperties(RefreshProperties.All)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		[TypeConverter("DevExpress.Xpo.Design.ObjectClassInfoTypeConverter, " + AssemblyInfo.SRAssemblyXpoDesignFull)]
		[MergableProperty(false)]
		[Category("Data")]
		public XPClassInfo ObjectClassInfo {
			get {
				if(IsDesignMode)
					return ((XPBaseCollection)List).GetObjectClassInfo();
				if(_ClassInfo == null && _Type != null && Session != null) {
					_ClassInfo = IsDesignMode ? DesignDictionary.GetClassInfo(_Type) : Session.GetClassInfo(_Type);
				}
				return _ClassInfo;
			}
			set {
				if(ObjectClassInfo == value)
					return;
				if(IsDesignMode) {
					XPCollection designHelper = (XPCollection)List;
					designHelper.ObjectType = null;
					designHelper.ObjectClassInfo = value;
					FixedFilterCriteria = null;
					return;
				}
				if(!this.IsInit)
					throw new InvalidOperationException(Res.GetString(Res.Collections_CannotAssignProperty, "ObjectClassInfo", GetType().Name));
				_ClassInfo = value;
				KillList();
			}
		}
		[Description("Gets or sets the criteria used to filter objects on the data store side. These criteria are never affected by the data-aware control that is bound to the current XPServerCollectionSource object.")]
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
					IXpoServerModeGridDataSource ds = List as IXpoServerModeGridDataSource;
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
		public void Reload() {
			var src = List as IListServer;
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
			IColumnsServerActions list = List as IColumnsServerActions;
			if(list != null) {
				return list.AllowAction(fieldName, action);
			}
			return XpoServerModeCore.IColumnsServerActionsAllowAction(Session, ObjectClassInfo, fieldName);
		}
		object IDXCloneable.DXClone() {
			return DXClone();
		}
		protected virtual object DXClone() {
			XPServerCollectionSource clone = DXCloneCreate();
			clone._AllowEdit = this._AllowEdit;
			clone._AllowNew = this._AllowNew;
			clone._AllowRemove = this._AllowRemove;
			clone._ClassInfo = this._ClassInfo;
			clone._DefaultSorting = this._DefaultSorting;
			clone._DeleteObjectOnRemove = this._DeleteObjectOnRemove;
			clone._FixedFilter = this._FixedFilter;
			clone._ServerExceptionThrown = this._ServerExceptionThrown;
			clone._Session = this._Session;
			clone._TrackChanges = this._TrackChanges;
			clone._Type = this._Type;
			clone.displayableProperties = this.displayableProperties;
			clone._ResolveSession = this._ResolveSession;
			return clone;
		}
		protected virtual XPServerCollectionSource DXCloneCreate() {
			return new XPServerCollectionSource();
		}
	}
	public delegate void ServerExceptionThrownEventHandler(object sender, ServerExceptionThrownEventArgs e);
	public enum ServerExceptionThrownAction { Skip, Rethrow }
	public class ServerExceptionThrownEventArgs : EventArgs {
		Exception _Exception;
		ServerExceptionThrownAction _Action;
		public ServerExceptionThrownEventArgs(Exception exception, ServerExceptionThrownAction action) {
			this._Exception = exception;
			this._Action = action;
		}
		public ServerExceptionThrownEventArgs(Exception exception) : this(exception, ServerExceptionThrownAction.Skip) { }
		public ServerExceptionThrownAction Action {
			get { return _Action; }
			set { _Action = value; }
		}
		public Exception Exception {
			get { return _Exception; }
		}
	}
}
