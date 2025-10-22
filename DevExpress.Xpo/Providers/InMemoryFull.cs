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
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using DevExpress.Data.Filtering;
using DevExpress.Data.Filtering.Exceptions;
using DevExpress.Data.Filtering.Helpers;
using DevExpress.Data.Helpers;
using DevExpress.Utils;
using DevExpress.Xpo.DB.Exceptions;
using DevExpress.Xpo.Helpers;
namespace DevExpress.Xpo.DB.Helpers {
	public class IsTopLevelAggregateCheckerFull : IQueryCriteriaVisitor<bool> {
		public bool Visit(QuerySubQueryContainer theOperand) {
			if(theOperand.Node == null)
				return true;
			else
				return false;
		}
		public bool Visit(QueryOperand theOperand) {
			return false;
		}
		public bool Visit(FunctionOperator theOperator) {
			return Process(theOperator.Operands);
		}
		public bool Visit(OperandValue theOperand) {
			return false;
		}
		public bool Visit(GroupOperator theOperator) {
			return Process(theOperator.Operands);
		}
		public bool Visit(InOperator theOperator) {
			if(Process(theOperator.LeftOperand))
				return true;
			return Process(theOperator.Operands);
		}
		public bool Visit(UnaryOperator theOperator) {
			return Process(theOperator.Operand);
		}
		public bool Visit(BinaryOperator theOperator) {
			return Process(theOperator.LeftOperand) || Process(theOperator.RightOperand);
		}
		public bool Visit(BetweenOperator theOperator) {
			return Process(theOperator.TestExpression) || Process(theOperator.BeginExpression) || Process(theOperator.EndExpression);
		}
		bool Process(IEnumerable ops) {
			foreach(CriteriaOperator op in ops) {
				if(Process(op))
					return true;
			}
			return false;
		}
		bool Process(CriteriaOperator op) {
			if(ReferenceEquals(op, null))
				return false;
			return op.Accept(this);
		}
		public static bool IsTopLevelAggregate(CriteriaOperator op) {
			return new IsTopLevelAggregateCheckerFull().Process(op);
		}
		public static bool IsGrouped(SelectStatement selectStatement) {
			if(selectStatement.GroupProperties.Count > 0)
				return true;
			if(!ReferenceEquals(selectStatement.GroupCondition, null))
				return true;
			int count = selectStatement.Operands.Count;
			for(int i = 0; i < count; i++) {
				if(IsTopLevelAggregate(selectStatement.Operands[i]))
					return true;
			}
			return false;
		}
	}
	public class ContextDependenceChecker : IQueryCriteriaVisitor<bool> {
		HashSet<string> upNodes;
		public ContextDependenceChecker(HashSet<string> upNodes) {
			this.upNodes = upNodes;
		}
		public bool Process(JoinNode root) {
			foreach(var nodeInfo in DataSetStoreHelpersFull.GetAllNodes(root)) {
				if(Process(nodeInfo.Node.Condition)) return true;
			}
			return false;
		}
		public static bool Process(HashSet<string> upNodes, JoinNode root) {
			return new ContextDependenceChecker(upNodes).Process(root);
		}
		bool Process(CriteriaOperator criteria) {
			if(ReferenceEquals(criteria, null))
				return false;
			if(upNodes == null) return false;
			return criteria.Accept(this);
		}
		public bool Visit(QuerySubQueryContainer theOperand) {
			return Process(theOperand.AggregateProperty) || Process(theOperand.Node);
		}
		public bool Visit(QueryOperand theOperand) {
			return upNodes.Contains(theOperand.NodeAlias);
		}
		public bool Visit(FunctionOperator theOperator) {
			return ProcessOperands(theOperator.Operands);
		}
		bool ProcessOperands(CriteriaOperatorCollection operands) {
			int count = operands.Count;
			for(int i = 0; i < count; i++) {
				if(Process(operands[i])) return true;
			}
			return false;
		}
		public bool Visit(OperandValue theOperand) {
			return false;
		}
		public bool Visit(GroupOperator theOperator) {
			return ProcessOperands(theOperator.Operands);
		}
		public bool Visit(InOperator theOperator) {
			if(Process(theOperator.LeftOperand)) return true;
			return ProcessOperands(theOperator.Operands);
		}
		public bool Visit(UnaryOperator theOperator) {
			return Process(theOperator.Operand);
		}
		public bool Visit(BinaryOperator theOperator) {
			return Process(theOperator.LeftOperand) || Process(theOperator.RightOperand);
		}
		public bool Visit(BetweenOperator theOperator) {
			return Process(theOperator.TestExpression) || Process(theOperator.BeginExpression) || Process(theOperator.EndExpression);
		}
	}
	public class IndexFinderResult {
		public readonly IndexFinderItem[][] Result;
		public IndexFinderResult(IndexFinderItem[][] result) {
			this.Result = result;
		}
	}
	public class IndexFinderItem {
		public readonly InMemoryColumn Column;
		public readonly object Value;
		public readonly bool ValueIsQueryOperand;
		public IndexFinderItem(InMemoryColumn column, object value, bool valueIsQueryOperand) {
			this.Column = column;
			this.Value = value;
			this.ValueIsQueryOperand = valueIsQueryOperand;
		}
		public override string ToString() {
			return string.Format(CultureInfo.InvariantCulture, "{0} = {1}", Column, Value);
		}
	}
	public class IndexFinderItemComparerByColumnIndex : IComparer<IndexFinderItem> {
		public int Compare(IndexFinderItem left, IndexFinderItem right) {
			if(ReferenceEquals(left, right)) return 0;
			if(ReferenceEquals(left, null)) return 1;
			if(ReferenceEquals(right, null)) return -1;
			return left.Column.ColumnIndex.CompareTo(right.Column.ColumnIndex);
		}
	}
	public class IndexFinder : IQueryCriteriaVisitor<object> {
		string alias;
		InMemoryTable table;
		Dictionary<string, InMemoryColumn> columnsWithIndecies = new Dictionary<string, InMemoryColumn>();
		bool returnProperies;
		object Process(CriteriaOperator criteria) {
			if(ReferenceEquals(criteria, null))
				return null;
			return criteria.Accept(this);
		}
		object ICriteriaVisitor<object>.Visit(FunctionOperator theOperator) {
			return null;
		}
		object ICriteriaVisitor<object>.Visit(OperandValue theOperand) {
			return theOperand.Value;
		}
		object ICriteriaVisitor<object>.Visit(GroupOperator theOperator) {
			if(theOperator.OperatorType == GroupOperatorType.And) return VisitGroupAnd(theOperator.Operands);
			return VisitGroupOr(theOperator.Operands);
		}
		object VisitGroupAnd(CriteriaOperatorCollection operands) {
			int count = operands.Count;
			List<List<IndexFinderItem>> result = null;
			List<IndexFinderItem> list = null;
			for(int operandIndex = 0; operandIndex < count; operandIndex++) {
				object val = Process((CriteriaOperator)operands[operandIndex]);
				IndexFinderItem key = val as IndexFinderItem;
				List<List<IndexFinderItem>> keyList = val as List<List<IndexFinderItem>>;
				if(result == null) {
					if(key != null) {
						if(list == null) list = new List<IndexFinderItem>();
						list.Add(key);
					}
					else if(keyList != null) {
						result = keyList;
						if(list == null) continue;
						for(int i = 0; i < keyList.Count; i++) {
							keyList[i].AddRange(list);
						}
					}
				}
				else {
					if(key != null) {
						for(int i = 0; i < result.Count; i++) {
							result[i].Add(key);
						}
					}
					else if(keyList != null) {
						List<List<IndexFinderItem>> newResult = new List<List<IndexFinderItem>>();
						for(int i = 0; i < result.Count; i++) {
							for(int j = 0; j < keyList.Count; j++) {
								List<IndexFinderItem> currentList = new List<IndexFinderItem>(result[i]);
								currentList.AddRange(keyList[j]);
								newResult.Add(currentList);
							}
						}
					}
				}
			}
			if(result == null && list != null) {
				result = new List<List<IndexFinderItem>>();
				result.Add(list);
			}
			return result;
		}
		object VisitGroupOr(CriteriaOperatorCollection operands) {
			int count = operands.Count;
			List<List<IndexFinderItem>> result = new List<List<IndexFinderItem>>();
			for(int operandIndex = 0; operandIndex < count; operandIndex++) {
				object val = Process((CriteriaOperator)operands[operandIndex]);
				IndexFinderItem key = val as IndexFinderItem;
				List<List<IndexFinderItem>> keyList = val as List<List<IndexFinderItem>>;
				if(key != null) {
					List<IndexFinderItem> list = new List<IndexFinderItem>();
					list.Add(key);
					result.Add(list);
				}
				else if(keyList != null) {
					result.AddRange(keyList);
				}
				else {
					return null;
				}
			}
			return result;
		}
		object ICriteriaVisitor<object>.Visit(InOperator theOperator) {
			object left = Process(theOperator.LeftOperand);
			if(!(left is InMemoryColumn))
				return null;
			int count = theOperator.Operands.Count;
			List<List<IndexFinderItem>> result = new List<List<IndexFinderItem>>(count);
			for(int i = 0; i < count; i++) {
				object val = Process((CriteriaOperator)theOperator.Operands[i]);
				if(val != null) {
					List<IndexFinderItem> list = new List<IndexFinderItem>();
					list.Add(new IndexFinderItem((InMemoryColumn)left, val, val is QueryOperand));
					result.Add(list);
				}
			}
			return result.Count > 0 ? result : null;
		}
		object ICriteriaVisitor<object>.Visit(UnaryOperator theOperator) {
			return null;
		}
		object ICriteriaVisitor<object>.Visit(BinaryOperator theOperator) {
			if(theOperator.OperatorType != BinaryOperatorType.Equal)
				return null;
			object left = Process(theOperator.LeftOperand);
			object right = Process(theOperator.RightOperand);
			if((left is InMemoryColumn) && right != null) {
				return new IndexFinderItem((InMemoryColumn)left, right, right is QueryOperand);
			}
			if((right is InMemoryColumn) && left != null) {
				return new IndexFinderItem((InMemoryColumn)right, left, left is QueryOperand);
			}
			return null;
		}
		object ICriteriaVisitor<object>.Visit(BetweenOperator theOperator) {
			return null;
		}
		public IndexFinder(string alias, InMemoryTable table, bool returnProperies)
			: this(alias, table) {
			this.returnProperies = returnProperies;
		}
		public IndexFinder(string alias, InMemoryTable table) {
			this.alias = alias;
			this.table = table;
			foreach(InMemoryColumn column in table.Columns) {
				if(column.HasIndexes) {
					columnsWithIndecies.Add(column.Name, column);
				}
			}
		}
		public static IndexFinderResult Find(string alias, InMemoryTable table, CriteriaOperator criteria) {
			return Find(alias, table, criteria, false);
		}
		public static IndexFinderResult Find(string alias, InMemoryTable table, CriteriaOperator criteria, bool returnProperties) {
			return new IndexFinder(alias, table, returnProperties).Find(criteria);
		}
		public IndexFinderResult Find(CriteriaOperator criteria) {
			if(ReferenceEquals(criteria, null)) return null;
			if(table.Indexes.Count == 0) return null;
			object objResult = criteria.Accept(this);
			IndexFinderItem oneResult = objResult as IndexFinderItem;
			if(oneResult != null) {
				foreach(InMemoryIndexWrapper index in table.Indexes) {
					if(index.Columns.Count == 1 && ReferenceEquals(index.Columns[0], oneResult.Column))
						return new IndexFinderResult(new IndexFinderItem[][] { new IndexFinderItem[] { oneResult } });
				}
			}
			List<List<IndexFinderItem>> result = objResult as List<List<IndexFinderItem>>;
			if(result == null) return null;
			if(result.Count == 1 && result[0].Count == 1) {
				foreach(InMemoryIndexWrapper index in table.Indexes) {
					if(index.Columns.Count == 1 && ReferenceEquals(index.Columns[0], result[0][0].Column))
						return new IndexFinderResult(new IndexFinderItem[][] { new IndexFinderItem[] { result[0][0] } });
				}
				return null;
			}
			List<IndexFinderItem[]> resultChecked = new List<IndexFinderItem[]>();
			for(int i = 0; i < result.Count; i++) {
				List<IndexFinderItem> bestResultList = null;
				List<IndexFinderItem> currentList = result[i];
				currentList.Sort(new IndexFinderItemComparerByColumnIndex());
				foreach(InMemoryIndexWrapper index in table.Indexes) {
					List<IndexFinderItem> resultList = GetIndexIntersection(index, currentList);
					if(resultList == null) continue;
					if(bestResultList == null) bestResultList = resultList;
					if(bestResultList.Count < resultList.Count) bestResultList = resultList;
				}
				if(bestResultList == null) return null;
				resultChecked.Add(bestResultList.ToArray());
			}
			return new IndexFinderResult(resultChecked.ToArray());
		}
		static List<IndexFinderItem> GetIndexIntersection(InMemoryIndexWrapper index, List<IndexFinderItem> list) {
			int itemIndex = 0;
			List<IndexFinderItem> resultList = null;
			for(int i = 0; i < index.Columns.Count; i++) {
				InMemoryColumn indexColumn = index.Columns[i];
				while(itemIndex < list.Count) {
					if(ReferenceEquals(indexColumn, list[itemIndex].Column)) {
						if(resultList == null) resultList = new List<IndexFinderItem>();
						resultList.Add(list[itemIndex]);
						itemIndex++;
						break;
					}
					itemIndex++;
				}
			}
			if(resultList != null && resultList.Count == index.Columns.Count) return resultList;
			return null;
		}
		object IQueryCriteriaVisitor<object>.Visit(QuerySubQueryContainer theOperand) {
			return null;
		}
		object IQueryCriteriaVisitor<object>.Visit(QueryOperand theOperand) {
			InMemoryColumn column;
			string nodeAlias = theOperand.NodeAlias != null ? theOperand.NodeAlias : string.Empty;
			if(alias == nodeAlias && columnsWithIndecies.TryGetValue(theOperand.ColumnName, out column)) {
				return column;
			}
			return returnProperies ? theOperand : null;
		}
		public static IndexFinderItem[][] GetFullIndexResult(IndexFinderItem[][] inputResult) {
			List<IndexFinderItem[]> result = new List<IndexFinderItem[]>();
			List<IndexFinderItem> vector = new List<IndexFinderItem>(inputResult.Length);
			FillResultList(result, inputResult, vector, 0);
			return result.ToArray();
		}
		static void FillResultList(List<IndexFinderItem[]> resultList, IndexFinderItem[][] inputResult, List<IndexFinderItem> vector, int level) {
			IndexFinderItem[] currentList = inputResult[level];
			for(int i = 0; i < currentList.Length; i++) {
				vector.Add(currentList[i]);
				if(level < (inputResult.Length - 1)) FillResultList(resultList, inputResult, vector, level + 1);
				else resultList.Add(vector.ToArray());
				vector.RemoveAt(vector.Count - 1);
			}
		}
		public static bool HasQueryOperand(IndexFinderResult keys) {
			if(keys == null)
				return false;
			for(int j = 0; j < keys.Result.Length; j++) {
				IndexFinderItem[] key = keys.Result[j];
				for(int i = 0; i < key.Length; i++) {
					if(key[i].ValueIsQueryOperand)
						return true;
				}
			}
			return false;
		}
		public static InMemoryIndexWrapper[] GetIndexList(InMemoryTable table, IndexFinderResult keys) {
			if(keys == null)
				return null;
			InMemoryIndexWrapper[] result = new InMemoryIndexWrapper[keys.Result.Length];
			for(int j = 0; j < keys.Result.Length; j++) {
				IndexFinderItem[] key = keys.Result[j];
				InMemoryColumn[] columns = new InMemoryColumn[key.Length];
				for(int i = 0; i < key.Length; i++) {
					columns[i] = key[i].Column;
				}
				result[j] = table.Rows.FindIndex(columns);
			}
			return result;
		}
	}
	public struct NodeCriteriaFinderResult {
		public bool Processed;
		public string MainAlias;
		public List<string> List;
		public NodeCriteriaFinderResult(bool processed, string mainAlias, List<string> list) {
			Processed = processed;
			MainAlias = mainAlias;
			List = list;
		}
		public static NodeCriteriaFinderResult New(bool processed) {
			return new NodeCriteriaFinderResult(processed, null, new List<string>());
		}
		public void Union(NodeCriteriaFinderResult anotherResult) {
			if(!string.IsNullOrEmpty(anotherResult.MainAlias)) {
				if(string.IsNullOrEmpty(MainAlias)) {
					MainAlias = anotherResult.MainAlias;
				}
				else {
					if(MainAlias != anotherResult.MainAlias) {
						throw new InvalidOperationException(string.Format("MainAlias({0}) != anotherResult.MainAlias({1})", MainAlias, anotherResult.MainAlias));
					}
				}
			}
			if(List == null) {
				List = anotherResult.List;
			}
			else if(anotherResult.List != null) {
				List.AddRange(anotherResult.List);
			}
		}
		public string ProcessNodesString() {
			if(List == null) {
				return string.IsNullOrEmpty(MainAlias) ? MainAlias : ("*" + MainAlias);
			}
			List.Sort();
			string prevNode = null;
			for(int i = (List.Count - 1); i >= 0; i--) {
				if(prevNode == List[i]) {
					List.RemoveAt(i);
				}
				else {
					prevNode = List[i];
				}
			}
			StringBuilder sb = new StringBuilder();
			int count = List.Count;
			prevNode = null;
			for(int i = -1; i < count; i++) {
				string currentNode;
				if(i >= 0) {
					currentNode = List[i];
					if(currentNode == prevNode) {
						continue;
					}
					if(sb.Length > 0) {
						sb.Append('.');
					}
					prevNode = currentNode;
				}
				else {
					currentNode = MainAlias;
					if(!string.IsNullOrEmpty(MainAlias)) {
						sb.Append('*');
					}
				}
				sb.Append(currentNode);
			}
			return sb.ToString();
		}
	}
	public class NodeCriteriaFinder : IQueryCriteriaVisitor<NodeCriteriaFinderResult> {
		int inAtomicGroupLevel;
		Dictionary<string, PlanAliasCriteriaInfo> criteriaDict;
		string currentNodeAlias;
		public NodeCriteriaFinder() { }
		public static Dictionary<string, PlanAliasCriteriaInfo> FindCriteria(string currentNodeAlias, CriteriaOperator criteria) {
			return new NodeCriteriaFinder().Find(currentNodeAlias, criteria);
		}
		public static void FindCriteria(string currentNodeAlias, CriteriaOperator criteria, Dictionary<string, PlanAliasCriteriaInfo> previousDict) {
			new NodeCriteriaFinder().Find(currentNodeAlias, criteria, previousDict);
		}
		public Dictionary<string, PlanAliasCriteriaInfo> Find(string currentNodeAlias, CriteriaOperator criteria) {
			Find(currentNodeAlias, criteria, new Dictionary<string, PlanAliasCriteriaInfo>());
			return criteriaDict;
		}
		public void Find(string currentNodeAlias, CriteriaOperator criteria, Dictionary<string, PlanAliasCriteriaInfo> previousDict) {
			Guard.ArgumentNotNull(previousDict, nameof(previousDict));
			this.currentNodeAlias = currentNodeAlias;
			criteriaDict = previousDict;
			ProcessAddOperands(new CriteriaOperator[] { criteria });
		}
		NodeCriteriaFinderResult Process(CriteriaOperator criteria) {
			if(ReferenceEquals(criteria, null))
				return new NodeCriteriaFinderResult(true, null, null);
			return criteria.Accept(this);
		}
		NodeCriteriaFinderResult IQueryCriteriaVisitor<NodeCriteriaFinderResult>.Visit(QuerySubQueryContainer theOperand) {
			inAtomicGroupLevel++;
			try {
				List<string> internalNodes = new List<string>();
				var result = NodeCriteriaFinderResult.New(false);
				var propertyResult = Process(theOperand.AggregateProperty);
				result.Union(propertyResult);
				if(!ReferenceEquals(theOperand.Node, null)) {
					var allNodes = DataSetStoreHelpersFull.GetAllNodes(theOperand.Node);
					foreach(var nodeInfo in allNodes) {
						internalNodes.Add(nodeInfo.Node.Alias);
						var conditionResult = Process(nodeInfo.Node.Condition);
						result.Union(conditionResult);
					}
				}
				for(int i = result.List.Count - 1; i >= 0; i--) {
					if(internalNodes.Contains(result.List[i])) {
						result.List.RemoveAt(i);
					}
				}
				return result;
			}
			finally {
				inAtomicGroupLevel--;
			}
		}
		NodeCriteriaFinderResult IQueryCriteriaVisitor<NodeCriteriaFinderResult>.Visit(QueryOperand theOperand) {
			return new NodeCriteriaFinderResult(false, null, new List<string>() { theOperand.NodeAlias });
		}
		NodeCriteriaFinderResult ICriteriaVisitor<NodeCriteriaFinderResult>.Visit(FunctionOperator theOperator) {
			inAtomicGroupLevel++;
			try {
				return ProcessOperands(theOperator.Operands);
			}
			finally {
				inAtomicGroupLevel--;
			}
		}
		NodeCriteriaFinderResult ICriteriaVisitor<NodeCriteriaFinderResult>.Visit(OperandValue theOperand) {
			return new NodeCriteriaFinderResult(false, null, null);
		}
		NodeCriteriaFinderResult ICriteriaVisitor<NodeCriteriaFinderResult>.Visit(GroupOperator theOperator) {
			if(theOperator.OperatorType == GroupOperatorType.Or) {
				inAtomicGroupLevel++;
				try {
					return ProcessOperands(theOperator.Operands);
				}
				finally {
					inAtomicGroupLevel--;
				}
			}
			else {
				if(inAtomicGroupLevel > 0) return ProcessOperands(theOperator.Operands);
				return ProcessAddOperands(theOperator.Operands.ToArray());
			}
		}
		NodeCriteriaFinderResult ProcessAddOperands(CriteriaOperator[] operands) {
			int count = operands.Length;
			for(int i = 0; i < count; i++) {
				var currentResult = Process(operands[i]);
				if(currentResult.List == null) {
					if(!string.IsNullOrEmpty(currentNodeAlias) && !currentResult.Processed) {
						currentResult.MainAlias = currentNodeAlias;
					}
					else {
						continue;
					}
				}
				else {
					if(!string.IsNullOrEmpty(currentNodeAlias)) {
						currentResult.MainAlias = currentNodeAlias;
					}
				}
				string nodesString = currentResult.ProcessNodesString();
				PlanAliasCriteriaInfo criteriaInfo = null;
				if(!criteriaDict.TryGetValue(nodesString, out criteriaInfo)) {
					criteriaInfo = new PlanAliasCriteriaInfo(currentResult.MainAlias, currentResult.List == null ? Array.Empty<string>() : currentResult.List.ToArray(), new List<CriteriaOperator>());
					criteriaDict.Add(nodesString, criteriaInfo);
				}
				criteriaInfo.Criteria.Add(operands[i]);
			}
			return new NodeCriteriaFinderResult(true, null, null);
		}
		NodeCriteriaFinderResult ProcessOperands(List<CriteriaOperator> operands) {
			var result = new NodeCriteriaFinderResult();
			int count = operands.Count;
			for(int i = 0; i < count; i++) {
				var currentResult = Process(operands[i]);
				if(currentResult.List == null) {
					continue;
				}
				if(result.List == null) {
					result.List = currentResult.List;
				}
				else {
					result.List.AddRange(currentResult.List);
				}
			}
			return result;
		}
		NodeCriteriaFinderResult ICriteriaVisitor<NodeCriteriaFinderResult>.Visit(InOperator theOperator) {
			inAtomicGroupLevel++;
			try {
				List<CriteriaOperator> operands = new List<CriteriaOperator>();
				operands.Add(theOperator.LeftOperand);
				operands.AddRange(theOperator.Operands);
				return ProcessOperands(operands);
			}
			finally {
				inAtomicGroupLevel--;
			}
		}
		NodeCriteriaFinderResult ICriteriaVisitor<NodeCriteriaFinderResult>.Visit(UnaryOperator theOperator) {
			inAtomicGroupLevel++;
			try {
				return Process(theOperator.Operand);
			}
			finally {
				inAtomicGroupLevel--;
			}
		}
		NodeCriteriaFinderResult ICriteriaVisitor<NodeCriteriaFinderResult>.Visit(BinaryOperator theOperator) {
			inAtomicGroupLevel++;
			try {
				var leftResult = Process(theOperator.LeftOperand);
				var rightResult = Process(theOperator.RightOperand);
				if(leftResult.List == null) return rightResult;
				if(rightResult.List == null) return leftResult;
				leftResult.List.AddRange(rightResult.List);
				return leftResult;
			}
			finally {
				inAtomicGroupLevel--;
			}
		}
		NodeCriteriaFinderResult ICriteriaVisitor<NodeCriteriaFinderResult>.Visit(BetweenOperator theOperator) {
			inAtomicGroupLevel++;
			try {
				var beginResult = Process(theOperator.BeginExpression);
				var endResult = Process(theOperator.EndExpression);
				var testResult = Process(theOperator.TestExpression);
				var tempResult = new NodeCriteriaFinderResult();
				if(endResult.List == null) {
					tempResult = testResult;
				}
				else {
					tempResult = endResult;
					if(testResult.List != null) {
						tempResult.List.AddRange(testResult.List);
					}
				}
				if(beginResult.List == null) return tempResult;
				if(tempResult.List == null) return beginResult;
				beginResult.List.AddRange(tempResult.List);
				return beginResult;
			}
			finally {
				inAtomicGroupLevel--;
			}
		}
	}
	public class DataSetStoreHelpersFull {
		DataSetStoreHelpersFull() { }
		static InMemoryColumn[] GetColumns(InMemoryTable table, StringCollection columns) {
			InMemoryColumn[] dataColumns = new InMemoryColumn[columns.Count];
			int count = columns.Count;
			for(int i = 0; i < count; i++) {
				dataColumns[i] = table.Columns[columns[i]];
			}
			return dataColumns;
		}
		static string GetIndexName(InMemoryTable table, DBIndex index) {
			StringBuilder name = new StringBuilder(table.Name);
			int count = index.Columns.Count;
			for(int i = 0; i < count; i++) {
				name.Append(index.Columns[i]);
			}
			return name.ToString();
		}
		static bool IsEqual(DBIndex index, InMemoryIndexWrapper constraint) {
			int count = constraint.Columns.Count;
			if(index.Columns.Count != count)
				return false;
			List<string> indexNames = new List<string>();
			List<string> constraintNames = new List<string>();
			for(int i = 0; i < count; ++i) {
				constraintNames.Add(constraint.Columns[i].Name);
				indexNames.Add(index.Columns[i]);
			}
			indexNames.Sort();
			constraintNames.Sort();
			for(int i = 0; i < count; ++i) {
				if(indexNames[i] != constraintNames[i])
					return false;
			}
			return true;
		}
		static string IsExists(InMemoryTable table, DBIndex index) {
			foreach(InMemoryIndexWrapper currentIndex in table.Indexes) {
				if(IsEqual(index, currentIndex))
					return currentIndex.Name;
			}
			return null;
		}
		static bool IsExists(InMemoryTable table, DBPrimaryKey index) {
			if(table.PrimaryKey == null)
				return false;
			return IsEqual(index, table.PrimaryKey);
		}
		static bool IsEqual(DBForeignKey fk, InMemoryRelation relation) {
			if(fk.PrimaryKeyTable != relation.PTable.Name)
				return false;
			int count = relation.Pairs.Length;
			if(fk.Columns.Count != count)
				return false;
			for(int i = 0; i < count; ++i) {
				if(relation.Pairs[i].FKey.Name != fk.Columns[i] || fk.PrimaryKeyTableKeyColumns[i] != relation.Pairs[i].PKey.Name)
					return false;
			}
			return true;
		}
		static bool IsExists(InMemoryTable table, DBForeignKey fk) {
			for(int i = 0; i < table.FRelations.Count; i++) {
				if(IsEqual(fk, table.FRelations[i])) return true;
			}
			return false;
		}
		static bool IsExists(InMemoryTable table, DBColumn column) {
			return table.Columns[column.Name] != null;
		}
		public static InMemoryTable QueryTable(InMemorySet dataSet, string tableName) {
			return dataSet.GetTable(tableName);
		}
		public static InMemoryTable GetTable(InMemorySet dataSet, string tableName) {
			InMemoryTable result = QueryTable(dataSet, tableName);
			if(result == null)
				throw new SchemaCorrectionNeededException(tableName);
			return result;
		}
		static string Create(InMemoryTable table, DBIndex index) {
			int count = index.Columns.Count;
			InMemoryColumn[] columns = new InMemoryColumn[count];
			for(int i = 0; i < count; i++) {
				columns[i] = table.Columns[index.Columns[i]];
			}
			if(count > 1) Array.Sort<InMemoryColumn>(columns, new InMemoryColumnIndexComparer());
			return table.CreateIndex(columns, index.IsUnique);
		}
		static void Create(InMemoryTable table, DBPrimaryKey index) {
			string name = CreateIfNotExists(table, (DBIndex)index);
			table.SetPrimaryKey(name);
		}
		static void Create(InMemoryTable table, DBForeignKey fk) {
			var pkTable = table.BaseSet.GetTable(fk.PrimaryKeyTable);
			if(pkTable == null)
				throw new SchemaCorrectionNeededException(Res.GetString(Res.InMemoryFull_TableNotFound, fk.PrimaryKeyTable));
			InMemoryColumn[] pColumns = GetColumns(pkTable, fk.PrimaryKeyTableKeyColumns);
			InMemoryColumn[] fColumns = GetColumns(table, fk.Columns);
			if(pColumns.Length != fColumns.Length) throw new ArgumentException(Res.GetString(Res.InMemoryFull_DifferentColumnListLengths));
			InMemoryRelationPair[] pairs = new InMemoryRelationPair[pColumns.Length];
			for(int i = 0; i < pairs.Length; i++) {
				pairs[i] = new InMemoryRelationPair(pColumns[i], fColumns[i]);
			}
			table.BaseSet.AddRelation(pairs);
		}
		static void Create(InMemoryTable table, DBColumn column) {
			if(column.IsIdentity) {
				table.Columns.Add(column.Name, DBColumn.GetType(column.ColumnType), true);
			}
			else {
				table.Columns.Add(column.Name, DBColumn.GetType(column.ColumnType), column.IsNullable, column.DefaultValue);
			}
			if(column.ColumnType == DBColumnType.String && column.Size != 0)
				table.Columns[column.Name].MaxLength = column.Size;
			table.Columns.CommitColumnsChanges();
		}
		static InMemoryTable Create(InMemorySet inMemorySet, DBTable table) {
			return inMemorySet.CreateTable(table.Name);
		}
		public static void CreateIfNotExists(InMemoryTable table, DBForeignKey dbObj) {
			if(!IsExists(table, dbObj))
				Create(table, dbObj);
		}
		public static string CreateIfNotExists(InMemoryTable table, DBIndex dbObj) {
			string name = IsExists(table, dbObj);
			if(string.IsNullOrEmpty(name))
				return Create(table, dbObj);
			return name;
		}
		public static void CreateIfNotExists(InMemoryTable table, DBPrimaryKey dbObj) {
			if(!IsExists(table, dbObj))
				Create(table, dbObj);
		}
		public static void CreateIfNotExists(InMemoryTable table, DBColumn dbObj) {
			if(!IsExists(table, dbObj))
				Create(table, dbObj);
		}
		public static InMemoryTable CreateIfNotExists(InMemorySet inMemorySet, DBTable table) {
			InMemoryTable result = QueryTable(inMemorySet, table.Name);
			if(result == null)
				result = Create(inMemorySet, table);
			return result;
		}
		static void UpdateRow(IInMemoryRow row, object[] values, InMemoryColumn[] columns) {
			int count = columns.Length;
			row.BeginEdit();
			try {
				for(int i = 0; i < count; i++) {
					row[columns[i].ColumnIndex] = values[i];
				}
				row.EndEdit();
			}
			catch {
				row.CancelEdit();
				throw;
			}
		}
		public static object[] GetResultRow(InMemoryComplexRow row, ExpressionEvaluator[] evaluators) {
			int count = evaluators.Length;
			object[] result = new object[count];
			for(int i = 0; i < count; i++) {
				result[i] = evaluators[i].Evaluate(row);
			}
			return result;
		}
		public static object[] GetResultRow(List<InMemoryComplexRow> rows, ExpressionEvaluator[] evaluators) {
			int count = evaluators.Length;
			object[] result = new object[count];
			for(int i = 0; i < count; i++) {
				result[i] = evaluators[i].Evaluate(rows);
			}
			return result;
		}
		public static SelectStatementResult DoGetData(IInMemoryDataElector dataElector, InMemoryDataElectorContextDescriptor descriptor, ExpressionEvaluator[] dataEvaluators, SortingComparerFull sortingComparer, int skipRecords, int topRecords) {
			InMemoryComplexSet rows = dataElector.Process(descriptor);
			if(sortingComparer != null) {
				rows.Sort(sortingComparer);
			}
			else {
				rows.Randomize();
			}
			if(skipRecords != 0) {
				if(skipRecords >= rows.Count)
					rows.Clear();
				else
					rows.RemoveRange(0, skipRecords);
			}
			if(topRecords != 0 && topRecords < rows.Count)
				rows.RemoveRange(topRecords, rows.Count - topRecords);
			int count = rows.Count;
			SelectStatementResultRow[] result = new SelectStatementResultRow[count];
			for(int i = 0; i < count; i++) {
				result[i] = new SelectStatementResultRow(GetResultRow(rows[i], dataEvaluators));
			}
			return new SelectStatementResult(result);
		}
		public static int DoInsertRecord(InMemoryTable table, QueryParameterCollection parameters, CriteriaOperatorCollection operands, ParameterValue identityParameter, TaggedParametersHolder identitiesByTag) {
			object[] values = new object[table.Columns.Count];
			int count = parameters.Count;
			identitiesByTag.ConsolidateIdentity(identityParameter);
			bool[] initializedColumns = new bool[table.Columns.Count];
			for(int i = 0; i < count; i++) {
				OperandValue parameter = identitiesByTag.ConsolidateParameter(parameters[i]);
				QueryOperand operand = (QueryOperand)operands[i];
				InMemoryColumn dColumn = table.Columns[operand.ColumnName];
				if(dColumn == null)
					throw new SchemaCorrectionNeededException(table.Name + "." + operand.ColumnName);
				values[dColumn.ColumnIndex] = parameter.Value;
				initializedColumns[dColumn.ColumnIndex] = true;
			}
			for(int i = 0; i < table.Columns.Count; i++) {
				if(table.Columns[i].AutoIncrement && ReferenceEquals(values[i], null)) {
					values[i] = InMemoryAutoIncrementValue.Value;
					continue;
				}
				if(values[i] == null && !initializedColumns[i]) {
					values[i] = table.Columns[i].DefaultValue;
				}
			}
			InMemoryRow row = table.Rows.AddNewRow(values);
			if(!ReferenceEquals(identityParameter, null)) {
				int colCount = table.Columns.Count;
				for(int i = 0; i < colCount; i++) {
					InMemoryColumn column = table.Columns[i];
					if(column.AutoIncrement) {
						identityParameter.Value = row[column.ColumnIndex];
						break;
					}
				}
			}
			return 1;
		}
		public static int DoUpdateRecord(IInMemoryPlanner planner, InMemoryTable table, QueryParameterCollection parameters, CriteriaOperatorCollection operands, TaggedParametersHolder identitiesByTag, CriteriaOperator condition, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions) {
			return DoUpdateRecord(planner, table, parameters, operands, identitiesByTag, condition, caseSensitive, customFunctions, null);
		}
		public static int DoUpdateRecord(IInMemoryPlanner planner, InMemoryTable table, QueryParameterCollection parameters, CriteriaOperatorCollection operands, TaggedParametersHolder identitiesByTag, CriteriaOperator condition, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions, ICollection<ICustomAggregate> customAggregates) {
			int count = operands.Count;
			object[] values = new object[count];
			InMemoryColumn[] columns = new InMemoryColumn[count];
			for(int i = 0; i < count; i++) {
				OperandValue parameter = identitiesByTag.ConsolidateParameter(parameters[i]);
				QueryOperand operand = (QueryOperand)operands[i];
				columns[i] = table.Columns[operand.ColumnName];
				if(columns[i] == null)
					throw new SchemaCorrectionNeededException(table.Name + "." + operand.ColumnName);
				values[i] = parameter.Value;
			}
			IInMemoryDataElector elector = planner.GetPlan(string.Empty, table, QueryParamsReprocessor.ReprocessCriteria(condition, identitiesByTag));
			InMemoryComplexSet rows = elector.Process(new InMemoryDataElectorContextDescriptor(planner, caseSensitive, customFunctions, customAggregates));
			int rowCount = rows.Count;
			for(int i = 0; i < rowCount; i++) {
				DataSetStoreHelpersFull.UpdateRow(rows[i][0], values, columns);
			}
			return rowCount;
		}
		public static int DoDeleteRecord(IInMemoryPlanner planner, InMemoryTable table, CriteriaOperator condition, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions) {
			return DoDeleteRecord(planner, table, condition, caseSensitive, customFunctions, null);
		}
		public static int DoDeleteRecord(IInMemoryPlanner planner, InMemoryTable table, CriteriaOperator condition, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions, ICollection<ICustomAggregate> customAggregates) {
			IInMemoryDataElector elector = planner.GetPlan(string.Empty, table, condition);
			InMemoryComplexSet rows = elector.Process(new InMemoryDataElectorContextDescriptor(planner, caseSensitive, customFunctions, customAggregates));
			int count = rows.Count;
			for(int i = 0; i < count; i++) {
				((InMemoryRow)rows[i][0]).Delete();
			}
			return count;
		}
		public static List<List<InMemoryComplexRow>> DoGetGroupedDataCore(ExpressionEvaluator[] groupEvaluators, ExpressionEvaluator havingEvaluator, SortingListComparerFull sortingComparer, int skipRecords, int topRecords, InMemoryComplexSet rows, bool doRandomizeIfNoSorting) {
			Dictionary<InMemoryGroupFull, List<InMemoryComplexRow>> groupingResult = new Dictionary<InMemoryGroupFull, List<InMemoryComplexRow>>();
			if(sortingComparer == null) {
				rows.Randomize();
			}
			if(groupEvaluators.Length == 0) {
				groupingResult.Add(new InMemoryGroupFull(Array.Empty<object>()), new List<InMemoryComplexRow>());
			}
			int count = rows.Count;
			for(int i = 0; i < count; i++) {
				InMemoryComplexRow row = rows[i];
				InMemoryGroupFull group = new InMemoryGroupFull(GetResultRow(row, groupEvaluators));
				List<InMemoryComplexRow> groupList;
				if(!groupingResult.TryGetValue(group, out groupList)) {
					groupList = new List<InMemoryComplexRow>();
					groupingResult.Add(group, groupList);
				}
				groupList.Add(row);
			}
			List<List<InMemoryComplexRow>> havingResult = new List<List<InMemoryComplexRow>>();
			foreach(List<InMemoryComplexRow> group in groupingResult.Values) {
				if(group.Count > 0) {
					if(havingEvaluator.Fit(group))
						havingResult.Add(group);
				}
				else {
					if(havingEvaluator.Fit(null))
						havingResult.Add(group);
				}
			}
			if(sortingComparer != null) {
				havingResult.Sort(sortingComparer);
			}
			if(skipRecords != 0) {
				if(skipRecords >= havingResult.Count)
					havingResult.Clear();
				else
					havingResult.RemoveRange(0, skipRecords);
			}
			if(topRecords != 0 && topRecords < havingResult.Count) {
				havingResult.RemoveRange(topRecords, havingResult.Count - topRecords);
			}
			return havingResult;
		}
		public static SelectStatementResult DoGetGroupedData(IInMemoryDataElector dataElector, InMemoryDataElectorContextDescriptor descriptor, ExpressionEvaluator[] groupEvaluators, ExpressionEvaluator havingEvaluator, SortingListComparerFull sortingComparer, int skipRecords, int topRecords, ExpressionEvaluator[] dataEvaluators) {
			InMemoryComplexSet rows = dataElector.Process(descriptor);
			var havingResult = DoGetGroupedDataCore(groupEvaluators, havingEvaluator, sortingComparer, skipRecords, topRecords, rows, true);
			int count = havingResult.Count;
			SelectStatementResultRow[] result = new SelectStatementResultRow[count];
			for(int i = 0; i < count; i++) {
				result[i] = new SelectStatementResultRow(GetResultRow(havingResult[i], dataEvaluators));
			}
			return new SelectStatementResult(result);
		}
		public struct NodeInfo {
			public readonly JoinNode Node;
			public readonly JoinNode ParentNode;
			public NodeInfo(JoinNode node, JoinNode parentNode) {
				Node = node;
				ParentNode = parentNode;
			}
		}
		public static List<NodeInfo> GetAllNodes(JoinNode node) {
			List<NodeInfo> nodes = new List<NodeInfo>();
			nodes.Add(new NodeInfo(node, null));
			GetAllNodesInternal(node, nodes);
			return nodes;
		}
		static void GetAllNodesInternal(JoinNode node, List<NodeInfo> nodes) {
			foreach(JoinNode subNode in node.SubNodes) {
				nodes.Add(new NodeInfo(subNode, node));
				GetAllNodesInternal(subNode, nodes);
			}
		}
		public static ExpressionEvaluator PrepareDataEvaluator(CriteriaOperator operand, EvaluatorContextDescriptor descriptor, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions) {
			return PrepareDataEvaluator(operand, descriptor, caseSensitive, customFunctions, null);
		}
		public static ExpressionEvaluator PrepareDataEvaluator(CriteriaOperator operand, EvaluatorContextDescriptor descriptor, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions, ICollection<ICustomAggregate> customAggregates) {
			return new QueryableExpressionEvaluator(descriptor, operand, caseSensitive, customFunctions, customAggregates);
		}
		public static ExpressionEvaluator[] PrepareDataEvaluators(CriteriaOperatorCollection operands, EvaluatorContextDescriptor descriptor, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions) {
			return PrepareDataEvaluators(operands, descriptor, caseSensitive, customFunctions);
		}
		public static ExpressionEvaluator[] PrepareDataEvaluators(CriteriaOperatorCollection operands, EvaluatorContextDescriptor descriptor, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions, ICollection<ICustomAggregate> customAggregates) {
			int count = operands.Count;
			ExpressionEvaluator[] evaluators = new ExpressionEvaluator[count];
			for(int i = 0; i < count; i++) {
				evaluators[i] = PrepareDataEvaluator(operands[i], descriptor, caseSensitive, customFunctions, customAggregates);
			}
			return evaluators;
		}
		public static SortingComparerFull PrepareSortingComparer(QuerySortingCollection sortProperties, EvaluatorContextDescriptor descriptor, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions) {
			return PrepareSortingComparer(sortProperties, descriptor, caseSensitive, customFunctions, null);
		}
		public static SortingComparerFull PrepareSortingComparer(QuerySortingCollection sortProperties, EvaluatorContextDescriptor descriptor, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions, ICollection<ICustomAggregate> customAggregates) {
			if(sortProperties.Count == 0)
				return null;
			int count = sortProperties.Count;
			ExpressionEvaluator[] sortingEvaluators = new ExpressionEvaluator[count];
			for(int i = 0; i < count; i++) {
				SortingColumn sortingColumn = sortProperties[i];
				sortingEvaluators[i] = PrepareDataEvaluator(sortingColumn.Property, descriptor, caseSensitive, customFunctions, customAggregates);
			}
			return new SortingComparerFull(sortingEvaluators, sortProperties);
		}
		public static SortingListComparerFull PrepareSortingListComparer(QuerySortingCollection sortProperties, EvaluatorContextDescriptor descriptor, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions) {
			return PrepareSortingListComparer(sortProperties, descriptor, caseSensitive, customFunctions, null);
		}
		public static SortingListComparerFull PrepareSortingListComparer(QuerySortingCollection sortProperties, EvaluatorContextDescriptor descriptor, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions, ICollection<ICustomAggregate> customAggregates) {
			if(sortProperties.Count == 0)
				return null;
			int count = sortProperties.Count;
			ExpressionEvaluator[] sortingEvaluators = new ExpressionEvaluator[count];
			for(int i = 0; i < count; i++) {
				SortingColumn sortingColumn = sortProperties[i];
				sortingEvaluators[i] = PrepareDataEvaluator(sortingColumn.Property, descriptor, caseSensitive, customFunctions, customAggregates);
			}
			return new SortingListComparerFull(sortingEvaluators, sortProperties);
		}
	}
	public class SortingComparerFull : IComparer<InMemoryComplexRow>, IComparer {
		ExpressionEvaluator[] evaluators;
		QuerySortingCollection operands;
		public SortingComparerFull(ExpressionEvaluator[] sortingEvaluators, QuerySortingCollection operands) {
			this.evaluators = sortingEvaluators;
			this.operands = operands;
		}
		int IComparer<InMemoryComplexRow>.Compare(InMemoryComplexRow x, InMemoryComplexRow y) {
			return ((IComparer)this).Compare(x, y);
		}
		int IComparer.Compare(object x, object y) {
			int i = 0;
			foreach(ExpressionEvaluator evaluator in evaluators) {
				int res = Comparer<object>.Default.Compare(evaluator.Evaluate(x), evaluator.Evaluate(y));
				if(res != 0)
					return operands[i].Direction == SortingDirection.Ascending ? res : -res;
				i++;
			}
			return 0;
		}
	}
	public class SortingListComparerFull : IComparer<List<InMemoryComplexRow>>, IComparer {
		ExpressionEvaluator[] evaluators;
		QuerySortingCollection operands;
		SortingComparerFull sortComparer;
		public SortingListComparerFull(ExpressionEvaluator[] sortingEvaluators, QuerySortingCollection operands) {
			this.evaluators = sortingEvaluators;
			this.operands = operands;
			this.sortComparer = new SortingComparerFull(sortingEvaluators, operands);
		}
		int IComparer<List<InMemoryComplexRow>>.Compare(List<InMemoryComplexRow> x, List<InMemoryComplexRow> y) {
			return ((IComparer)this).Compare(x, y);
		}
		int IComparer.Compare(object x, object y) {
			List<InMemoryComplexRow> xRows = x as List<InMemoryComplexRow>;
			List<InMemoryComplexRow> yRows = y as List<InMemoryComplexRow>;
			if(xRows == null && yRows == null) return 0;
			if(yRows == null) {
				xRows.Sort(sortComparer); 
				return operands[0].Direction == SortingDirection.Ascending ? -1 : 1;
			}
			if(xRows == null) {
				yRows.Sort(sortComparer);
				return operands[0].Direction == SortingDirection.Ascending ? 1 : -1;
			}
			int i = 0;
			foreach(ExpressionEvaluator evaluator in evaluators) {
				int res = Comparer<object>.Default.Compare(evaluator.Evaluate(xRows), evaluator.Evaluate(yRows));
				if(res != 0)
					return operands[i].Direction == SortingDirection.Ascending ? res : -res;
				i++;
			}
			return 0;
		}
	}
	public class InMemoryGroupFull {
		public readonly object[] GroupValues;
		public InMemoryGroupFull(object[] groupValues) {
			this.GroupValues = groupValues;
		}
		public override bool Equals(object obj) {
			InMemoryGroupFull another = obj as InMemoryGroupFull;
			if(another == null)
				return false;
			if(this.GroupValues.Length != another.GroupValues.Length)
				return false;
			int count = GroupValues.Length;
			for(int i = 0; i < count; ++i) {
				if(!object.Equals(this.GroupValues[i], another.GroupValues[i]))
					return false;
			}
			return true;
		}
		public override int GetHashCode() {
			return HashCodeHelper.CalculateGenericList(GroupValues);
		}
	}
	public class InMemoryDataElectorContextSource {
		public readonly string NodeAlias;
		public readonly InMemoryRow Row;
		public readonly InMemoryTable Table;
		public readonly InMemoryComplexRow ComplexRow;
		public readonly InMemoryComplexRow ComplexRowRight;
		public InMemoryDataElectorContextSource(InMemoryComplexRow complexRow, InMemoryComplexRow complexRowRight) {
			this.ComplexRow = complexRow;
			this.ComplexRowRight = complexRowRight;
		}
		public InMemoryDataElectorContextSource(string nodeAlias, InMemoryRow row, InMemoryTable table) : this(nodeAlias, row, table, null) { }
		public InMemoryDataElectorContextSource(string nodeAlias, InMemoryRow row, InMemoryTable table, InMemoryComplexRow complexRow) {
			if(ReferenceEquals(nodeAlias, null)) this.NodeAlias = string.Empty;
			else this.NodeAlias = nodeAlias;
			this.Row = row;
			this.Table = table;
			this.ComplexRow = complexRow;
		}
	}
	public class InMemoryDataElectorContextDescriptor : QuereableEvaluatorContextDescriptor {
		IInMemoryPlanner planner;
		bool caseSensitive;
		ICollection<ICustomFunctionOperator> customFunctions;
		ICollection<ICustomAggregate> customAggregates;
		public bool CaseSensitive { get { return caseSensitive; } }
		public ICollection<ICustomFunctionOperator> CustomFunctions { get { return customFunctions; } }
		public ICollection<ICustomAggregate> CustomAggregates { get { return customAggregates; } }
		List<object> nestedSource = new List<object>();
		Dictionary<JoinNode, InMemoryPlanCacheItem> existsPlanCashe = new Dictionary<JoinNode, InMemoryPlanCacheItem>();
		public InMemoryDataElectorContextDescriptor(IInMemoryPlanner planner, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions)
			: this(planner, caseSensitive, customFunctions, null) { }
		public InMemoryDataElectorContextDescriptor(IInMemoryPlanner planner, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions, ICollection<ICustomAggregate> customAggregates) {
			this.planner = planner;
			this.caseSensitive = caseSensitive;
			this.customFunctions = customFunctions;
			this.customAggregates = customAggregates;
		}
		public override IEnumerable GetCollectionContexts(object source, string collectionName) {
			throw new NotSupportedException();
		}
		public override EvaluatorContext GetNestedContext(object source, string propertyPath) {
			throw new NotSupportedException();
		}
		public override object GetPropertyValue(object source, EvaluatorProperty propertyPath) {
			throw new NotSupportedException();
		}
		public override IEnumerable GetQueryContexts(object source, string queryTypeName, CriteriaOperator condition, int top) {
			throw new NotSupportedException();
		}
		public override object GetOperandValue(object currentSource, QueryOperand theOperand) {
			if(currentSource == null && nestedSource.Count == 0)
				return null;
			string alias = theOperand.NodeAlias;
			if(alias == null) alias = string.Empty;
			int sourceCounter = nestedSource.Count;
			while(sourceCounter >= 0) {
				object source = sourceCounter >= nestedSource.Count ? currentSource : nestedSource[sourceCounter];
				InMemoryDataElectorContextSource contextSource = source as InMemoryDataElectorContextSource;
				try {
					if(contextSource == null) {
						InMemoryComplexRow row = source as InMemoryComplexRow;
						if(row != null) {
							object result;
							if(GetComplexRowData(alias, theOperand, row, out result)) return result;
						}
						else {
							IList<InMemoryComplexRow> list = source as IList<InMemoryComplexRow>;
							if(list != null) {
								object result;
								if(GetComplexRowData(alias, theOperand, list[0], out result)) return result;
							}
						}
					}
					else {
						if(string.Equals(alias, contextSource.NodeAlias)) {
							return contextSource.Row[theOperand.ColumnName];
						}
						else {
							object result;
							if(GetComplexRowData(alias, theOperand, contextSource.ComplexRowRight, out result)) return result;
							if(GetComplexRowData(alias, theOperand, contextSource.ComplexRow, out result)) return result;
						}
					}
				}
				catch(InMemorySetException) {
					throw new InvalidPropertyPathException(string.Format(CultureInfo.InvariantCulture, FilteringExceptionsText.ExpressionEvaluatorInvalidPropertyPath, string.Join(".", new string[] { alias, theOperand.ColumnName })));
				}
				sourceCounter--;
			}
			throw new InvalidPropertyPathException(string.Format(CultureInfo.InvariantCulture, FilteringExceptionsText.ExpressionEvaluatorInvalidPropertyPath, string.Join(".", new string[] { alias, theOperand.ColumnName })));
		}
		static bool GetComplexRowData(string alias, QueryOperand theOperand, InMemoryComplexRow complexRow, out object result) {
			result = null;
			if(ReferenceEquals(complexRow, null)) return false;
			int tableIndex = complexRow.ComplexSet.GetTableIndex(alias);
			if(tableIndex < 0) {
				tableIndex = complexRow.ComplexSet.GetTableIndex(alias, theOperand.ColumnName);
			}
			if(tableIndex >= 0) {
				IInMemoryRow row = complexRow[tableIndex];
				if(row == null) {
					if(!complexRow.ComplexSet.GetTable(tableIndex).ExistsColumn(theOperand.ColumnName))
						throw new InvalidPropertyPathException(string.Format(CultureInfo.InvariantCulture, FilteringExceptionsText.ExpressionEvaluatorInvalidPropertyPath, string.Join(".", new string[] { alias, theOperand.ColumnName })));
					return true;
				}
				result = row[theOperand.ColumnName];
				return true;
			}
			return false;
		}
		public override object GetQueryResult(JoinNode root) {
			IInMemoryDataElector elector;
			InMemoryPlanCacheItem planCasheItem;
			if(!existsPlanCashe.TryGetValue(root, out planCasheItem)) {
				HashSet<string> nodesSet = new HashSet<string>();
				for(int i = 0; i < nestedSource.Count; i++) {
					InMemoryDataElectorSource.FillNodesDict(nestedSource[i], nodesSet);
				}
				if(ContextDependenceChecker.Process(nodesSet, root)) {
					elector = planner.GetPlan(root, nodesSet);
					planCasheItem = new InMemoryPlanCacheItem(elector);
					existsPlanCashe.Add(root, planCasheItem);
				}
				else {
					elector = planner.GetPlan(root).Process(this);
					planCasheItem = new InMemoryPlanCacheItem(elector);
					existsPlanCashe.Add(root, planCasheItem);
				}
			}
			else {
				elector = planCasheItem.Elector;
			}
			return elector.Process(this);
		}
		class InMemoryPlanCacheItem {
			public readonly IInMemoryDataElector Elector;
			public InMemoryPlanCacheItem(IInMemoryDataElector elector) {
				this.Elector = elector;
			}
		}
		public override void PushNestedSource(object source) {
			nestedSource.Add(source);
		}
		public override void PopNestedSource() {
			nestedSource.RemoveAt(nestedSource.Count - 1);
		}
	}
	class InMemoryProjectionTable : IInMemoryTable {
		readonly string name;
		readonly List<string> columns;
		readonly Dictionary<string, int> columnDictionary;
		public readonly bool IsTransitive;
		public InMemoryProjectionTable(DBProjection projection, string projectionAlias) {
			name = projectionAlias;
			var select = projection.Projection;
			var selectOperands = select.Operands;
			columns = new List<string>(selectOperands.Count);
			columnDictionary = new Dictionary<string, int>(selectOperands.Count);
			IsTransitive = true;
			for(int i = 0; i < selectOperands.Count; i++) {
				var operand = selectOperands[i];
				string column;
				var queryOperand = operand as QueryOperand;
				if(projection.Columns != null && i < projection.Columns.Count) {
					column = projection.Columns[i].Name;
					if(ReferenceEquals(queryOperand, null) || (column != queryOperand.ColumnName)) {
						IsTransitive = false;
					}
				}
				else {
					if(!ReferenceEquals(queryOperand, null)) {
						column = queryOperand.ColumnName;
					}
					else {
						column = string.Concat("PrP", i.ToString());
						IsTransitive = false;
					}
				}
				columns.Add(column);
				columnDictionary.Add(column, i);
			}
		}
		public string Name {
			get { return name; }
		}
		public bool ExistsColumn(string columnName) {
			return columnDictionary.ContainsKey(columnName);
		}
		public IEnumerable<string> GetColumnNames() {
			return columns.ToArray();
		}
		public bool TryGetColumnIndex(string columnName, out int columnIndex) {
			return columnDictionary.TryGetValue(columnName, out columnIndex);
		}
		public int ColumnsCount {
			get { return columns.Count; }
		}
	}
	class InMemoryProjectionRow : IInMemoryRow {
		readonly InMemoryProjectionTable table;
		readonly object[] data;
		public InMemoryProjectionRow(InMemoryProjectionTable table)
			: this(table, new object[table.ColumnsCount]) {
		}
		public InMemoryProjectionRow(InMemoryProjectionTable table, object[] data) {
			this.table = table;
			this.data = data;
		}
		public IInMemoryTable Table {
			get { return table; }
		}
		public object this[int columnIndex] {
			get { return data[columnIndex]; }
			set { data[columnIndex] = value; }
		}
		public object this[string columnName] {
			get {
				int columnIndex;
				if(!table.TryGetColumnIndex(columnName, out columnIndex)) throw new InMemorySetException(string.Format(Res.GetString(Res.InMemorySet_ColumnNotFound), columnName));
				return data[columnIndex];
			}
			set {
				int columnIndex;
				if(!table.TryGetColumnIndex(columnName, out columnIndex)) throw new InMemorySetException(string.Format(Res.GetString(Res.InMemorySet_ColumnNotFound), columnName));
				data[columnIndex] = value;
			}
		}
		public void BeginEdit() {
			throw new NotSupportedException();
		}
		public void EndEdit() {
			throw new NotSupportedException();
		}
		public void CancelEdit() {
			throw new NotSupportedException();
		}
	}
	class InMemoryProjectionElector : IInMemoryDataElector {
		readonly string projectionAlias;
		readonly DBProjection projection;
		readonly IInMemoryDataElector inputData;
		Func<InMemoryComplexSet, InMemoryComplexSet> makeProjectionHandler;
		public InMemoryProjectionElector(IInMemoryDataElector inputData, DBProjection projection, string projectionAlias) {
			this.inputData = inputData;
			this.projectionAlias = projectionAlias;
			this.projection = projection;
		}
		static void PrepareRowsBeforeProjection(InMemoryComplexSet rows, SelectStatement select, SortingComparerFull sortingComparer) {
			if(sortingComparer != null)
				rows.Sort(sortingComparer);
			int skipRecords = select.SkipSelectedRecords;
			if(skipRecords > 0) {
				if(skipRecords >= rows.Count)
					rows.Clear();
				else
					rows.RemoveRange(0, skipRecords);
			}
			int topRecords = select.TopSelectedRecords;
			if(topRecords > 0 && topRecords < rows.Count)
				rows.RemoveRange(topRecords, rows.Count - topRecords);
		}
		public InMemoryComplexSet Process(InMemoryDataElectorContextDescriptor descriptor) {
			if(makeProjectionHandler == null) {
				var select = projection.Projection;
				var projectionTable = new InMemoryProjectionTable(projection, projectionAlias);
				if(IsTopLevelAggregateCheckerFull.IsGrouped(select)) {
					var groupEvaluators = DataSetStoreHelpersFull.PrepareDataEvaluators(select.GroupProperties, descriptor, descriptor.CaseSensitive, descriptor.CustomFunctions, descriptor.CustomAggregates);
					var dataEvaluators = DataSetStoreHelpersFull.PrepareDataEvaluators(select.Operands, descriptor, descriptor.CaseSensitive, descriptor.CustomFunctions, descriptor.CustomAggregates);
					var havingEvaluator = DataSetStoreHelpersFull.PrepareDataEvaluator(select.GroupCondition, descriptor, descriptor.CaseSensitive, descriptor.CustomFunctions, descriptor.CustomAggregates);
					var sortingComparer = DataSetStoreHelpersFull.PrepareSortingListComparer(select.SortProperties, descriptor, descriptor.CaseSensitive, descriptor.CustomFunctions, descriptor.CustomAggregates);
					makeProjectionHandler = (rows) => {
						var groups = DataSetStoreHelpersFull.DoGetGroupedDataCore(groupEvaluators, havingEvaluator, sortingComparer, select.SkipSelectedRecords, select.TopSelectedRecords, rows, false);
						var resultRows = new InMemoryComplexSet();
						resultRows.AddTable(projectionAlias, projectionTable);
						foreach(var group in groups) {
							var data = new InMemoryProjectionRow(projectionTable, DataSetStoreHelpersFull.GetResultRow(group, dataEvaluators));
							var resultRow = new InMemoryComplexRow(resultRows);
							resultRow[0] = data;
							resultRows.AddRow(resultRow);
						}
						return resultRows;
					};
				}
				else {
					ExpressionEvaluator[] dataEvaluators = !projectionTable.IsTransitive ? DataSetStoreHelpersFull.PrepareDataEvaluators(select.Operands, descriptor, descriptor.CaseSensitive, descriptor.CustomFunctions, descriptor.CustomAggregates) : null;
					var sortingComparer = DataSetStoreHelpersFull.PrepareSortingComparer(select.SortProperties, descriptor, descriptor.CaseSensitive, descriptor.CustomFunctions, descriptor.CustomAggregates);
					if(dataEvaluators == null) {
						makeProjectionHandler = (rows) => {
							PrepareRowsBeforeProjection(rows, select, sortingComparer);
							rows.MakeProjection(projectionAlias);
							return rows;
						};
					}
					else {
						makeProjectionHandler = (rows) => {
							PrepareRowsBeforeProjection(rows, select, sortingComparer);
							var resultRows = new InMemoryComplexSet();
							resultRows.AddTable(projectionAlias, projectionTable);
							foreach(var row in rows) {
								var data = new InMemoryProjectionRow(projectionTable, DataSetStoreHelpersFull.GetResultRow(row, dataEvaluators));
								var resultRow = new InMemoryComplexRow(resultRows);
								resultRow[0] = data;
								resultRows.AddRow(resultRow);
							}
							return resultRows;
						};
					}
				}
			}
			var resultSet = inputData.Process(descriptor);
			return makeProjectionHandler(resultSet);
		}
	}
	public interface IInMemoryPlanner {
		IInMemoryDataElector GetPlan(JoinNode root);
		IInMemoryDataElector GetPlan(JoinNode subSelectNode, IEnumerable<string> existsNodeAliases);
		IInMemoryDataElector GetPlan(string alias, InMemoryTable table, CriteriaOperator condition);
	}
	public class PlanAliasCriteriaInfo {
		public readonly string MainAlias;
		public readonly string[] Aliases;
		public readonly List<CriteriaOperator> Criteria;
		public PlanAliasCriteriaInfo(string mainAlias, string[] aliases, List<CriteriaOperator> criteria) {
			this.MainAlias = mainAlias;
			this.Aliases = aliases;
			this.Criteria = criteria;
		}
	}
	class InMemoryWeighedPlanner : IInMemoryPlanner {
		readonly static ReadOnlySet<string> emptySet = new ReadOnlySet<string>(Array.Empty<string>());
		readonly InMemorySet dataSet;
		public InMemoryWeighedPlanner(InMemorySet dataSet) {
			this.dataSet = dataSet;
		}
		public IInMemoryDataElector GetPlan(JoinNode root) {
			return GetPlanInternal(root, emptySet);
		}
		public IInMemoryDataElector GetPlan(JoinNode subSelectNode, IEnumerable<string> existsNodeAliases) {
			return GetPlanInternal(subSelectNode, new ReadOnlySet<string>(existsNodeAliases));
		}
		public IInMemoryDataElector GetPlan(string alias, InMemoryTable table, CriteriaOperator condition) {
			return new InMemoryDataElectorTableSearch(alias, table, condition);
		}
		IInMemoryDataElector GetPlanInternal(JoinNode root, ISet<string> outerNodes) {
			NodeCriteriaFinder criteriaFinder = new NodeCriteriaFinder();
			var criteriaDict = new Dictionary<string, PlanAliasCriteriaInfo>();
			var nodeInfoList = DataSetStoreHelpersFull.GetAllNodes(root);
			Dictionary<string, PlanNodeInfo> nodes = new Dictionary<string, PlanNodeInfo>();
			int nodeCounter = 0;
			foreach(var nodeInfo in nodeInfoList) {
				var node = nodeInfo.Node;
				criteriaFinder.Find(node.Alias, node.Condition, criteriaDict);
				DBProjection projection = node.Table as DBProjection;
				PlanNodeInfo planNodeInfo = projection != null
					? new PlanNodeInfo(node.Alias, GetPlan(projection.Projection), node, nodeCounter, nodeInfo.ParentNode?.Alias)
					: new PlanNodeInfo(node.Alias, GetTable(node.Table.Name), node, nodeCounter, nodeInfo.ParentNode?.Alias);
				nodes.Add(node.Alias, planNodeInfo);
				nodeCounter++;
			}
			if(nodes.Count == 1) {
				PlanNodeInfo nodeInfo = nodes.Values.Single();
				if(nodeInfo.IsSubquery()) {
					return new InMemoryDataElectorResultSearch(nodeInfo.SubqueryElector, nodeInfo.Node.Condition);
				}
				return new InMemoryDataElectorTableSearch(nodeInfo.Alias, nodeInfo.Table, nodeInfo.Node.Condition);
			}
			var relations = FindRelations(nodes, criteriaDict.Values);
			using(var planPathFinder = new PlanPathFinder(dataSet, nodes, relations)) {
				var plan = planPathFinder.Find();
				if(plan == null) {
					throw new InMemorySetException(Res.GetString(Res.InMemoryFull_CannotPrepareQueryPlan));
				}
				var elector = CreateElector(plan, nodes, criteriaDict, outerNodes);
				if(criteriaDict.Count > 0) {
					throw new InMemorySetException(Res.GetString(Res.InMemoryFull_CannotPrepareQueryPlanX0, string.Join(", ", criteriaDict.Values.SelectMany(cd => cd.Criteria).Select(c => c.ToString()).ToArray())));
				}
				return elector;
			}
		}
		static Dictionary<string, List<PlanRelationInfo>> FindRelations(Dictionary<string, PlanNodeInfo> nodes, IEnumerable<PlanAliasCriteriaInfo> aliasCriteriaDict) {
			Dictionary<string, List<PlanRelationInfo>> relationDict = new Dictionary<string, List<PlanRelationInfo>>();
			foreach(PlanNodeInfo nodeInfo in nodes.Values) {
				var nodeIsLeftOuter = nodeInfo.Node.Type == JoinType.LeftOuter;
				IndexFinder indexFinder = nodeInfo.IsSubquery() ? null : new IndexFinder(nodeInfo.Alias, nodeInfo.Table, true);
				if(indexFinder != null) {
					foreach(PlanAliasCriteriaInfo aliasCriteriaInfo in aliasCriteriaDict) {
						if(!aliasCriteriaInfo.Aliases.Any(a => a == nodeInfo.Alias)) {
							continue;
						}
						foreach(CriteriaOperator op in aliasCriteriaInfo.Criteria) {
							IndexFinderResult indexFinderResult = indexFinder.Find(op);
							if(indexFinderResult == null) {
								continue;
							}
							foreach(IndexFinderItem[] indexResultList in indexFinderResult.Result) {
								foreach(IndexFinderItem indexFinderItem in indexResultList) {
									if(indexFinderItem.ValueIsQueryOperand) {
										var anotherAlias = ((QueryOperand)indexFinderItem.Value).NodeAlias;
										PlanNodeInfo anotherNode;
										if(!nodes.TryGetValue(anotherAlias, out anotherNode)) {
											continue;
										}
										var anotherNodeIsLeftOuter = anotherNode.Node.Type == JoinType.LeftOuter;
										if(anotherNode.IsLeftFor(nodeInfo)) {
											TryAddRelationInfo(relationDict, new PlanRelationInfo(((QueryOperand)indexFinderItem.Value).NodeAlias, nodeInfo.Alias, true, nodeIsLeftOuter));
										}
										else if(!anotherNodeIsLeftOuter) {
											TryAddRelationInfo(relationDict, new PlanRelationInfo(((QueryOperand)indexFinderItem.Value).NodeAlias, nodeInfo.Alias, true, false));
										}
									}
								}
							}
						}
					}
				}
				if(nodeInfo.ParentAlias != null) {
					var parentNode = nodes[nodeInfo.ParentAlias];
					TryAddRelationInfo(relationDict, new PlanRelationInfo(parentNode.Alias, nodeInfo.Alias, false, nodeIsLeftOuter));
					if(!nodeIsLeftOuter) {
						TryAddRelationInfo(relationDict, new PlanRelationInfo(nodeInfo.Alias, parentNode.Alias, false, nodeIsLeftOuter));
					}
				}
			}
			return relationDict;
		}
		static void TryAddRelationInfo(Dictionary<string, List<PlanRelationInfo>> relationDict, PlanRelationInfo newRelation) {
			List<PlanRelationInfo> relationList;
			if(!relationDict.TryGetValue(newRelation.Left, out relationList)) {
				relationList = new List<PlanRelationInfo>();
				relationDict.Add(newRelation.Left, relationList);
			}
			for(int i = relationList.Count - 1; i >= 0; i--) {
				var relation = relationList[i];
				if(relation.Right == newRelation.Right) {
					if(!relation.IsLookup && newRelation.IsLookup || !relation.IsLeftOuter && newRelation.IsLeftOuter) {
						relationList[i] = newRelation;
					}
					return;
				}
			}
			relationList.Add(newRelation);
		}
		IInMemoryDataElector CreateElector(IPlanPathItem plan, Dictionary<string, PlanNodeInfo> nodes, Dictionary<string, PlanAliasCriteriaInfo> criteriaDict, ISet<string> outerNodes) {
			IInMemoryDataElector dataElector = null;
			switch(plan.Type) {
				case PlanPathItemType.Table: {
					string nodeAlias = plan.Nodes.Single();
					var nodeInfo = nodes[nodeAlias];
					dataElector = new InMemoryDataElectorTableSearch(nodeAlias, nodeInfo.Table,
						TakeCriteria(plan.JoinType, plan.Nodes, nodes, criteriaDict, outerNodes));
				}
				break;
				case PlanPathItemType.ComplexSet: {
					string nodeAlias = plan.Nodes.Single();
					var nodeInfo = nodes[nodeAlias];
					if(nodeInfo.IsSubquery()) {
						dataElector = nodeInfo.SubqueryElector;
					}
					else {
						dataElector = new InMemoryDataElectorTableSearch(nodeAlias, nodeInfo.Table,
							TakeCriteria(plan.JoinType, plan.Nodes, nodes, criteriaDict, outerNodes));
					}
				}
				break;
				case PlanPathItemType.LookupInRight: {
					string rightAlias = plan.Right.Nodes.Single();
					var rightNodeInfo = nodes[rightAlias];
					if(plan.Left.Type == PlanPathItemType.Table) {
						string leftAlias = plan.Left.Nodes.Single();
						var leftNodeInfo = nodes[leftAlias];
						dataElector = new InMemoryDataElectorTablesJoinSearch(leftAlias, leftNodeInfo.Table,
							rightAlias, rightNodeInfo.Table,
							TakeCriteria(plan.JoinType, plan.Nodes, nodes, criteriaDict, outerNodes),
							plan.JoinType);
					}
					else {
						dataElector = new InMemoryDataElectorTableJoinSearch(rightAlias, rightNodeInfo.Table,
							CreateElector(plan.Left, nodes, criteriaDict, outerNodes),
							TakeCriteria(plan.JoinType, plan.Nodes, nodes, criteriaDict, outerNodes),
							plan.JoinType, false);
					}
				}
				break;
				case PlanPathItemType.NestedLoop: {
					if(plan.Left.Type == PlanPathItemType.Table && plan.Right.Type == PlanPathItemType.Table) {
						string leftAlias = plan.Left.Nodes.Single();
						var leftNodeInfo = nodes[leftAlias];
						string rightAlias = plan.Right.Nodes.Single();
						var rightNodeInfo = nodes[rightAlias];
						dataElector = new InMemoryDataElectorTablesJoinSearch(
							leftAlias, leftNodeInfo.Table,
							rightAlias, rightNodeInfo.Table,
							TakeCriteria(plan.JoinType, plan.Nodes, nodes, criteriaDict, outerNodes),
							plan.JoinType);
					}
					else if(plan.Right.Type == PlanPathItemType.Table) {
						string rightAlias = plan.Right.Nodes.Single();
						var rightNodeInfo = nodes[rightAlias];
						dataElector = new InMemoryDataElectorTableJoinSearch(
							rightAlias, rightNodeInfo.Table,
							CreateElector(plan.Left, nodes, criteriaDict, outerNodes),
							TakeCriteria(plan.JoinType, plan.Nodes, nodes, criteriaDict, outerNodes),
							plan.JoinType, false);
					}
					else if(plan.Left.Type == PlanPathItemType.Table) {
						string leftAlias = plan.Left.Nodes.Single();
						var leftNodeInfo = nodes[leftAlias];
						dataElector = new InMemoryDataElectorTableJoinSearch(
							leftAlias, leftNodeInfo.Table,
							CreateElector(plan.Right, nodes, criteriaDict, outerNodes),
							TakeCriteria(plan.JoinType, plan.Nodes, nodes, criteriaDict, outerNodes),
							plan.JoinType, true);
					}
					else {
						dataElector = new InMemoryDataElectorResultJoinSearch(
							CreateElector(plan.Left, nodes, criteriaDict, outerNodes),
							CreateElector(plan.Right, nodes, criteriaDict, outerNodes),
							TakeCriteria(plan.JoinType, plan.Nodes, nodes, criteriaDict, outerNodes),
							plan.JoinType);
					}
				}
				break;
			}
			CriteriaOperator co = TakeCriteria(JoinType.Inner, plan.Nodes, nodes, criteriaDict, outerNodes);
			if(!ReferenceEquals(co, null)) {
				dataElector = new InMemoryDataElectorResultSearch(dataElector, co);
			}
			return dataElector;
		}
		static CriteriaOperator TakeCriteria(JoinType currentJoinType, ISet<string> usedNodes, Dictionary<string, PlanNodeInfo> nodes, Dictionary<string, PlanAliasCriteriaInfo> criteriaDict, ISet<string> outerNodes) {
			List<CriteriaOperator> criteriaList = null;
			List<string> deleteList = null;
			foreach(KeyValuePair<string, PlanAliasCriteriaInfo> nodePair in criteriaDict) {
				var criteriaInfo = nodePair.Value;
				if(!usedNodes.Contains(criteriaInfo.MainAlias) && !outerNodes.Contains(criteriaInfo.MainAlias)
					|| !criteriaInfo.Aliases.All(n => usedNodes.Contains(n) || outerNodes.Contains(n))) {
					continue;
				}
				if(nodes[criteriaInfo.MainAlias].Node.Type != currentJoinType) {
					continue;
				}
				if(criteriaList == null) {
					deleteList = new List<string>();
					criteriaList = new List<CriteriaOperator>(nodePair.Value.Criteria);
				}
				else {
					criteriaList.AddRange(nodePair.Value.Criteria);
				}
				deleteList.Add(nodePair.Key);
			}
			if(criteriaList == null || criteriaList.Count == 0) {
				return null;
			}
			if(criteriaList.Count == 1) {
				criteriaDict.Remove(deleteList[0]);
				return criteriaList[0];
			}
			foreach(string deleteString in deleteList) {
				criteriaDict.Remove(deleteString);
			}
			GroupOperator group = new GroupOperator(GroupOperatorType.And, criteriaList);
			return group;
		}
		InMemoryTable GetTable(string tableName) {
			InMemoryTable table = dataSet.GetTable(tableName);
			if(table == null) throw new SchemaCorrectionNeededException(Res.GetString(Res.InMemoryFull_TableNotFound, tableName));
			return table;
		}
		enum PlanPathItemType {
			Table,
			ComplexSet,
			LookupInRight,
			NestedLoop
		}
		interface IPlanPathItem {
			IPlanPathItem Left { get; }
			IPlanPathItem Right { get; }
			ReadOnlySet<string> Nodes { get; }
			PlanPathItemType Type { get; }
			double Cost { get; }
			long EstimatedRowsCount { get; }
			JoinType JoinType { get; }
		}
		class TablePlanPathItem : IPlanPathItem {
			readonly string nodeAlias;
			readonly int rowsCount;
			readonly ReadOnlySet<string> nodes;
			public ReadOnlySet<string> Nodes {
				get { return nodes; }
			}
			public IPlanPathItem Left {
				get { return null; }
			}
			public IPlanPathItem Right {
				get { return null; }
			}
			public PlanPathItemType Type {
				get { return PlanPathItemType.Table; }
			}
			public long EstimatedRowsCount {
				get { return rowsCount; }
			}
			public double Cost {
				get { return 1; }
			}
			public JoinType JoinType {
				get { return JoinType.Inner; }
			}
			public TablePlanPathItem(string nodeAlias, int rowsCount) {
				this.nodeAlias = nodeAlias;
				this.nodes = new ReadOnlySet<string>(new string[] { nodeAlias });
				this.rowsCount = rowsCount;
			}
			public override string ToString() {
				return nodeAlias;
			}
		}
		class ComplexSetPlanPathItem : IPlanPathItem {
			readonly int rowsCount;
			readonly ReadOnlySet<string> nodes;
			readonly string toStringMessage;
			public ReadOnlySet<string> Nodes {
				get { return nodes; }
			}
			public IPlanPathItem Left {
				get { return null; }
			}
			public IPlanPathItem Right {
				get { return null; }
			}
			public PlanPathItemType Type {
				get { return PlanPathItemType.ComplexSet; }
			}
			public double Cost {
				get { return rowsCount; }
			}
			public long EstimatedRowsCount {
				get { return rowsCount; }
			}
			public JoinType JoinType {
				get { return JoinType.Inner; }
			}
			public ComplexSetPlanPathItem(string nodeAlias, int rowsCount)
				: this(new string[] { nodeAlias }, rowsCount) {
			}
			public ComplexSetPlanPathItem(string[] nodes, int rowsCount) {
				this.nodes = new ReadOnlySet<string>(nodes);
				this.rowsCount = rowsCount;
				this.toStringMessage = string.Concat("(", string.Join(",", nodes.ToArray()), ")");
			}
			public override string ToString() {
				return toStringMessage;
			}
		}
		class NestedLoopJoinPlanPathItem : IPlanPathItem {
			readonly IPlanPathItem left;
			readonly IPlanPathItem right;
			readonly ReadOnlySet<string> nodes;
			readonly JoinType joinType;
			readonly string toStringMessage;
			public ReadOnlySet<string> Nodes {
				get { return nodes; }
			}
			public IPlanPathItem Left {
				get { return left; }
			}
			public IPlanPathItem Right {
				get { return right; }
			}
			public PlanPathItemType Type {
				get { return PlanPathItemType.NestedLoop; }
			}
			double? cost;
			public double Cost {
				get {
					if(cost == null) {
						cost = left.Cost + right.Cost + (left.EstimatedRowsCount * right.EstimatedRowsCount);
					}
					return cost.Value;
				}
			}
			long? estimatedRowsCount;
			public long EstimatedRowsCount {
				get {
					if(estimatedRowsCount == null) {
						estimatedRowsCount = joinType == JoinType.Inner
							? Math.Min(left.EstimatedRowsCount, right.EstimatedRowsCount) / 2
							: left.EstimatedRowsCount * right.EstimatedRowsCount / 2;
					}
					return estimatedRowsCount.Value;
				}
			}
			public JoinType JoinType {
				get { return joinType; }
			}
			public NestedLoopJoinPlanPathItem(IPlanPathItem left, IPlanPathItem right, bool isLeftOuter) {
				this.left = left;
				this.right = right;
				this.joinType = isLeftOuter ? JoinType.LeftOuter : JoinType.Inner;
				this.nodes = new ReadOnlySet<string>(left.Nodes, right.Nodes);
				this.toStringMessage = string.Format(isLeftOuter ? "[{0} *> {1}]" : "[{0} <*> {1}]", left.ToString(), right.ToString());
			}
			public override string ToString() {
				return toStringMessage;
			}
		}
		class LookupJoinPlanPathItem : IPlanPathItem {
			readonly IPlanPathItem left;
			readonly IPlanPathItem right;
			readonly ReadOnlySet<string> nodes;
			readonly JoinType joinType;
			readonly string toStringMessage;
			public ReadOnlySet<string> Nodes {
				get { return nodes; }
			}
			public IPlanPathItem Left {
				get { return left; }
			}
			public IPlanPathItem Right {
				get { return right; }
			}
			public PlanPathItemType Type {
				get { return PlanPathItemType.LookupInRight; }
			}
			double? cost;
			public double Cost {
				get {
					if(cost == null) {
						cost = left.Cost + right.Cost + left.EstimatedRowsCount;
					}
					return cost.Value;
				}
			}
			long? estimatedRowsCount;
			public long EstimatedRowsCount {
				get {
					if(estimatedRowsCount == null) {
						estimatedRowsCount = left.EstimatedRowsCount / 2;
					}
					return estimatedRowsCount.Value;
				}
			}
			public JoinType JoinType {
				get { return joinType; }
			}
			public LookupJoinPlanPathItem(IPlanPathItem left, IPlanPathItem right, bool isLeftOuter) {
				this.left = left;
				if(right.Type != PlanPathItemType.Table) {
					throw new ArgumentException(null, nameof(right));
				}
				this.right = right;
				this.joinType = isLeftOuter ? JoinType.LeftOuter : JoinType.Inner;
				this.nodes = new ReadOnlySet<string>(left.Nodes, right.Nodes);
				this.toStringMessage = string.Format(isLeftOuter ? "[{0} +> {1}]" : "[{0} <+> {1}]", left.ToString(), right.ToString());
			}
			public override string ToString() {
				return toStringMessage;
			}
		}
		class PlanRelationInfo {
			public readonly string Left;
			public readonly string Right;
			public readonly bool IsLookup;
			public readonly bool IsLeftOuter;
			public PlanRelationInfo(string left, string right, bool isLookup, bool isLeftOuter) {
				this.Left = left;
				this.Right = right;
				this.IsLookup = isLookup;
				this.IsLeftOuter = isLeftOuter;
			}
			public override string ToString() {
				StringBuilder sb = new StringBuilder();
				sb.Append(Left);
				if(IsLeftOuter) {
					sb.Append(' ');
					sb.Append(IsLookup ? "+" : "*");
					sb.Append("> (");
				}
				else {
					sb.Append(" <");
					sb.Append(IsLookup ? "+" : "*");
					sb.Append("> (");
				}
				sb.Append(Right);
				sb.Append(')');
				return sb.ToString();
			}
		}
		class PlanNodeInfo {
			public readonly string Alias;
			public readonly InMemoryTable Table;
			public readonly IInMemoryDataElector SubqueryElector;
			public readonly JoinNode Node;
			public readonly int Index;
			public readonly string ParentAlias;
			PlanNodeInfo(string alias, JoinNode node, int index, string parentAlias) {
				this.Alias = alias;
				this.Node = node;
				this.Index = index;
				this.ParentAlias = parentAlias;
			}
			public PlanNodeInfo(string alias, InMemoryTable table, JoinNode node, int index, string parentAlias)
				: this(alias, node, index, parentAlias) {
				this.Table = table;
			}
			public PlanNodeInfo(string alias, IInMemoryDataElector subqueryElector, JoinNode node, int index, string parentAlias)
				: this(alias, node, index, parentAlias) {
				this.SubqueryElector = new InMemoryProjectionElector(subqueryElector, node.Table as DBProjection, node.Alias);
			}
			public bool IsLeftFor(PlanNodeInfo other) {
				return Index < other.Index;
			}
			public bool IsSubquery() {
				return SubqueryElector != null;
			}
			public override string ToString() {
				if(IsSubquery()) {
					return string.Format(CultureInfo.InvariantCulture, "{0} join ({1}) {2}({3})", Node.Type, Table.ToString(), Alias, Index);
				}
				return string.Format(CultureInfo.InvariantCulture, "{0} join {1} {2}({3})", Node.Type, Table.Name, Alias, Index);
			}
		}
		class NodeSetEqulityComparer : IEqualityComparer<ReadOnlySet<string>> {
			public static readonly NodeSetEqulityComparer Instance = new NodeSetEqulityComparer();
			public bool Equals(ReadOnlySet<string> x, ReadOnlySet<string> y) {
				if(ReferenceEquals(x, y)) {
					return true;
				}
				if(x == null || y == null || x.Count != y.Count) {
					return false;
				}
				foreach(var xNode in x) {
					if(!y.Contains(xNode)) {
						return false;
					}
				}
				return true;
			}
			public int GetHashCode(ReadOnlySet<string> nodeSet) {
				if(nodeSet == null) {
					return 0;
				}
				int hashCode = 0;
				foreach(var node in nodeSet) {
					hashCode ^= node.GetHashCode();
				}
				hashCode ^= nodeSet.Count;
				return hashCode;
			}
		}
		class PlanPathFinder : IDisposable {
			const int SearchCostFoundPlansMaxRatio = 1500;
			const int SearchCostNodeCountMaxRatio = 500;
			IPlanPathItem bestPlan;
			int bestPlanMissCounter;
			int chunkCostMissCounter;
			double? bestPlanCost;
			readonly InMemorySet set;
			readonly LohPooled.OrdinaryDictionary<string, IPlanPathItem> tableItemCache;
			readonly Dictionary<string, PlanNodeInfo> nodes;
			readonly Dictionary<string, List<PlanRelationInfo>> relations;
			LohPooled.OrdinaryDictionary<ReadOnlySet<string>, double> chunkCostDictionary;
			public PlanPathFinder(InMemorySet set, Dictionary<string, PlanNodeInfo> nodes, Dictionary<string, List<PlanRelationInfo>> relations) {
				this.set = set;
				this.nodes = nodes;
				this.relations = relations;
				this.tableItemCache = new LohPooled.OrdinaryDictionary<string, IPlanPathItem>(nodes.Count);
				this.chunkCostDictionary = new LohPooled.OrdinaryDictionary<ReadOnlySet<string>, double>(nodes.Count * nodes.Count * 10, NodeSetEqulityComparer.Instance);
			}
			public IPlanPathItem Find() {
				if(chunkCostDictionary == null) {
					return null;
				}
				bestPlan = null;
				bestPlanCost = null;
				bestPlanMissCounter = 0;
				chunkCostMissCounter = 0;
				chunkCostDictionary.Clear();
				foreach(var firstNode in nodes.Values) {
					FindInternal(CreateTableItem(firstNode.Alias));
				}
				return bestPlan;
			}
			void FindInternal(IPlanPathItem item) {
				var currentPlanCost = item.Cost;
				var nodesCount = nodes.Count;
				if(item.Nodes.Count == nodesCount) {
					if(bestPlanCost == null || bestPlanCost > currentPlanCost) {
						bestPlan = item;
						bestPlanCost = currentPlanCost;
					}
					else {
						bestPlanMissCounter++;
					}
					return;
				}
				if(bestPlanCost != null) {
					if(currentPlanCost > bestPlanCost) {
						return;
					}
					if(bestPlanMissCounter > 0 && (chunkCostMissCounter / bestPlanMissCounter) > SearchCostFoundPlansMaxRatio) {
						return;
					}
					if(nodesCount > 0 && (chunkCostMissCounter / nodesCount) > SearchCostNodeCountMaxRatio) {
						return;
					}
				}
				double chunkCost;
				if(chunkCostDictionary.TryGetValue(item.Nodes, out chunkCost)) {
					if(chunkCost <= currentPlanCost) {
						chunkCostMissCounter++;
						return;
					}
					chunkCostDictionary[item.Nodes] = currentPlanCost;
				}
				else {
					chunkCostDictionary.Add(item.Nodes, currentPlanCost);
				}
				foreach(var usedNode in item.Nodes) {
					List<PlanRelationInfo> nodeRelations;
					if(relations.TryGetValue(usedNode, out nodeRelations)) {
						foreach(var nodeRelation in nodeRelations) {
							if(!nodeRelation.IsLookup) {
								continue;
							}
							if(!item.Nodes.Contains(nodeRelation.Right)) {
								FindInternal(CreateJoinWithTableItem(item, nodeRelation));
							}
						}
						foreach(var nodeRelation in nodeRelations) {
							if(nodeRelation.IsLookup) {
								continue;
							}
							if(!item.Nodes.Contains(nodeRelation.Right)) {
								FindInternal(CreateJoinWithTableItem(item, nodeRelation));
							}
						}
					}
				}
			}
			IPlanPathItem CreateTableItem(string nodeAlias) {
				IPlanPathItem tableItem;
				if(!tableItemCache.TryGetValue(nodeAlias, out tableItem)) {
					var nodeInfo = nodes[nodeAlias];
					if(nodeInfo.IsSubquery()) {
						DBProjection projection = nodeInfo.Node.Table as DBProjection;
						int rowsCount = GetProjectionRowCount(projection);
						tableItem = new ComplexSetPlanPathItem(nodeAlias, rowsCount);
					}
					else {
						var table = set.GetTable(nodeInfo.Table.Name);
						tableItem = new TablePlanPathItem(nodeAlias, table.Rows.Count);
					}
					tableItemCache.Add(nodeAlias, tableItem);
				}
				return tableItem;
			}
			int GetProjectionRowCount(DBProjection projection) {
				var nodes = DataSetStoreHelpersFull.GetAllNodes(projection.Projection).Where(n => n.Node.Table is DBTable);
				int maxRowCount = 0;
				foreach(var node in nodes) {
					projection = node.Node.Table as DBProjection;
					if(projection != null) {
						maxRowCount = Math.Max(GetProjectionRowCount(projection), maxRowCount);
					}
					else {
						maxRowCount = Math.Max(set.GetTable(node.Node.Table.Name).Rows.Count, maxRowCount);
					}
				}
				return maxRowCount;
			}
			IPlanPathItem CreateJoinWithTableItem(IPlanPathItem left, PlanRelationInfo nodeRelation) {
				var rightTable = CreateTableItem(nodeRelation.Right);
				if(rightTable.Type == PlanPathItemType.Table && nodeRelation.IsLookup) {
					return new LookupJoinPlanPathItem(left, rightTable, nodeRelation.IsLeftOuter);
				}
				return new NestedLoopJoinPlanPathItem(left, rightTable, nodeRelation.IsLeftOuter);
			}
			public void Dispose() {
				tableItemCache.Dispose();
				chunkCostDictionary.Dispose();
				chunkCostDictionary = null;
			}
		}
		class ReadOnlySet<T> : ISet<T> {
			readonly ICollection<T> internalList;
			public ReadOnlySet(IEnumerable<T> source) {
				if(source == null) {
					throw new ArgumentNullException(nameof(source));
				}
				var collection = source as ICollection<T>;
				if(collection == null || collection.Count > 3) {
					internalList = new HashSet<T>(source);
				}
				else {
					internalList = collection;
				}
			}
			public ReadOnlySet(ICollection<T> collection1, ICollection<T> collection2) {
				Guard.ArgumentNotNull(collection1, nameof(collection1));
				Guard.ArgumentNotNull(collection2, nameof(collection2));
				if((collection1.Count + collection2.Count) > 3) {
					var tempSet = new HashSet<T>(collection1);
					tempSet.UnionWith(collection2);
					internalList = tempSet;
				}
				else {
					var tempList = new List<T>(collection1.Count + collection2.Count);
					tempList.AddRange(collection1);
					tempList.AddRange(collection2);
					internalList = tempList;
				}
			}
			public int Count {
				get { return internalList.Count; }
			}
			public bool IsReadOnly {
				get { return true; }
			}
			public bool Add(T item) {
				throw new NotSupportedException("readonly");
			}
			public void Clear() {
				throw new NotSupportedException("readonly");
			}
			public bool Contains(T item) {
				return internalList.Contains(item);
			}
			public void CopyTo(T[] array, int arrayIndex) {
				internalList.CopyTo(array, arrayIndex);
			}
			public void ExceptWith(IEnumerable<T> other) {
				throw new NotSupportedException("readonly");
			}
			public IEnumerator<T> GetEnumerator() {
				return internalList.GetEnumerator();
			}
			public void IntersectWith(IEnumerable<T> other) {
				throw new NotSupportedException("readonly");
			}
			public bool IsProperSubsetOf(IEnumerable<T> other) {
				throw new NotImplementedException();
			}
			public bool IsProperSupersetOf(IEnumerable<T> other) {
				throw new NotImplementedException();
			}
			public bool IsSubsetOf(IEnumerable<T> other) {
				throw new NotImplementedException();
			}
			public bool IsSupersetOf(IEnumerable<T> other) {
				throw new NotImplementedException();
			}
			public bool Overlaps(IEnumerable<T> other) {
				throw new NotImplementedException();
			}
			public bool Remove(T item) {
				throw new NotSupportedException("readonly");
			}
			public bool SetEquals(IEnumerable<T> other) {
				throw new NotImplementedException();
			}
			public void SymmetricExceptWith(IEnumerable<T> other) {
				throw new NotSupportedException("readonly");
			}
			public void UnionWith(IEnumerable<T> other) {
				throw new NotSupportedException("readonly");
			}
			void ICollection<T>.Add(T item) {
				throw new NotSupportedException("readonly");
			}
			IEnumerator IEnumerable.GetEnumerator() {
				return ((IEnumerable)internalList).GetEnumerator();
			}
			public override string ToString() {
				return string.Concat("Count = " + internalList.Count.ToString());
			}
		}
	}
	public class InMemoryDataElectorTableSearch : IInMemoryDataElector {
		string tableAlias;
		InMemoryTable table;
		CriteriaOperator criteria;
		IndexFinderResult keys;
		InMemoryIndexWrapper[] indexList;
		public InMemoryDataElectorTableSearch(string tableAlias, InMemoryTable table, CriteriaOperator criteria) {
			if(ReferenceEquals(table, null)) throw new NullReferenceException();
			this.tableAlias = tableAlias;
			this.table = table;
			this.criteria = criteria;
			this.keys = new IndexFinder(tableAlias, table).Find(criteria);
			this.indexList = IndexFinder.GetIndexList(table, keys);
		}
		public InMemoryComplexSet Process(InMemoryDataElectorContextDescriptor descriptor) {
			InMemoryComplexSet result = new InMemoryComplexSet();
			ExpressionEvaluator eval = new QueryableExpressionEvaluator(descriptor, criteria, descriptor.CaseSensitive, descriptor.CustomFunctions, descriptor.CustomAggregates);
			result.AddTable(tableAlias, table);
			InMemoryRowList rows = table.Rows;
			if(keys == null) {
				for(int i = 0; i < rows.Count; i++) {
					if(rows[i].State != InMemoryItemState.Deleted && eval.Fit(new InMemoryDataElectorContextSource(tableAlias, rows[i], table))) {
						result.AddNewRow(0, rows[i]);
					}
				}
			}
			else {
				HashSet<InMemoryRow> rowSet = new HashSet<InMemoryRow>();
				for(int j = 0; j < keys.Result.Length; j++) {
					IndexFinderItem[] key = keys.Result[j];
					object[] indexValues = new object[key.Length];
					for(int i = 0; i < key.Length; i++) {
						if(key[i].ValueIsQueryOperand) throw new InMemorySetException(Res.GetString(Res.InMemoryFull_WrongIndexInfo));
						indexValues[i] = key[i].Value;
					}
					InMemoryRow[] findRows = indexList[j].Find(indexValues, false);
					for(int i = 0; i < findRows.Length; i++) {
						InMemoryRow row = findRows[i];
						if(row != null && !rowSet.Contains(row) && eval.Fit(new InMemoryDataElectorContextSource(tableAlias, row, table))) {
							result.AddNewRow(0, row);
							rowSet.Add(row);
						}
					}
				}
			}
			return result;
		}
	}
	public delegate List<InMemoryComplexRow> InMemoryRowFitHandler(InMemoryRow row);
	public delegate List<InMemoryRow> InMemoryRowsFitHandler(InMemoryRow row);
	public delegate List<InMemoryRow> InMemoryComplexRowFitHandler(InMemoryComplexRow resultRow);
	public delegate List<InMemoryComplexRow> InMemoryComplexRowsFitHandler(InMemoryComplexRow resultRow);
	public class InMemoryDataElectorTablesJoinSearch : IInMemoryDataElector {
		string tableLeftAlias;
		InMemoryTable tableLeft;
		string tableRightAlias;
		InMemoryTable tableRight;
		CriteriaOperator criteria;
		JoinType joinType;
		IndexFinderResult keysLeft;
		IndexFinderResult keysRight;
		InMemoryIndexWrapper[] indexListLeft;
		InMemoryIndexWrapper[] indexListRight;
		bool queryOperandDetectedRight;
		bool queryOperandDetectedLeft;
		public InMemoryDataElectorTablesJoinSearch(string tableLeftAlias, InMemoryTable tableLeft, string tableRightAlias, InMemoryTable tableRight, CriteriaOperator criteria, JoinType joinType) {
			Guard.ArgumentNotNull(tableLeft, nameof(tableLeft));
			Guard.ArgumentIsNotNullOrEmpty(tableLeftAlias, nameof(tableLeftAlias));
			this.tableLeftAlias = tableLeftAlias;
			this.tableLeft = tableLeft;
			this.tableRightAlias = tableRightAlias;
			this.tableRight = tableRight;
			this.criteria = criteria;
			this.joinType = joinType;
			this.keysLeft = new IndexFinder(tableLeftAlias, tableLeft, joinType == JoinType.Inner).Find(criteria);
			this.indexListLeft = IndexFinder.GetIndexList(tableLeft, keysLeft);
			queryOperandDetectedLeft = IndexFinder.HasQueryOperand(keysLeft);
			this.keysRight = new IndexFinder(tableRightAlias, tableRight, joinType == JoinType.LeftOuter || !queryOperandDetectedLeft).Find(criteria);
			this.indexListRight = IndexFinder.GetIndexList(tableRight, keysRight);
			queryOperandDetectedRight = IndexFinder.HasQueryOperand(keysRight);
		}
		public InMemoryComplexSet Process(InMemoryDataElectorContextDescriptor descriptor) {
			return joinType == JoinType.LeftOuter ? ProcessIfLeftJoin(descriptor) : ProcessIfInnerJoin(descriptor);
		}
		delegate void ProcessRow(InMemoryRow row);
		InMemoryComplexSet ProcessIfLeftJoin(InMemoryDataElectorContextDescriptor descriptor) {
			if(queryOperandDetectedLeft)
				throw new InMemorySetException(Res.GetString(Res.InMemoryFull_WrongIndexInfo));
			ExpressionEvaluator eval = new QueryableExpressionEvaluator(descriptor, criteria, descriptor.CaseSensitive, descriptor.CustomFunctions, descriptor.CustomAggregates);
			InMemoryComplexSet resultSet = new InMemoryComplexSet();
			int tableLeftIndex = resultSet.AddTable(tableLeftAlias, tableLeft);
			int tableRightIndex = resultSet.AddTable(tableRightAlias, tableRight);
			IEnumerable<InMemoryRow> leftRows = GetRows(keysLeft, indexListLeft, tableLeft);
			if(!queryOperandDetectedRight) {
				IEnumerable<InMemoryRow> rightRows = GetRows(keysRight, indexListRight, tableRight);
				foreach(InMemoryRow leftRow in leftRows) {
					bool hasFit = false;
					foreach(InMemoryRow rightRow in rightRows)
						if(AddRow(eval, resultSet, tableLeftIndex, tableRightIndex, leftRow, rightRow))
							hasFit = true;
					if(!hasFit)
						resultSet.AddNewRow(tableLeftIndex, leftRow);
				}
			}
			else {
				foreach(InMemoryRow leftRow in leftRows) {
					bool hasFit = false;
					GetRows(keysRight, indexListRight, tableRight, descriptor, new InMemoryDataElectorContextSource(tableLeftAlias, leftRow, tableLeft), delegate (InMemoryRow rightRow) {
						if(AddRow(eval, resultSet, tableLeftIndex, tableRightIndex, leftRow, rightRow))
							hasFit = true;
					});
					if(!hasFit)
						resultSet.AddNewRow(tableLeftIndex, leftRow);
				}
			}
			return resultSet;
		}
		static bool AddRow(ExpressionEvaluator eval, InMemoryComplexSet resultSet, int tableLeftIndex, int tableRightIndex, InMemoryRow leftRow, InMemoryRow rightRow) {
			InMemoryComplexRow complexRow = new InMemoryComplexRow(resultSet);
			complexRow[tableLeftIndex] = leftRow;
			complexRow[tableRightIndex] = rightRow;
			if(!eval.Fit(complexRow))
				return false;
			resultSet.AddRow(complexRow);
			return true;
		}
		static IEnumerable<InMemoryRow> GetRows(IndexFinderResult keys, InMemoryIndexWrapper[] indexList, InMemoryTable table) {
			if(keys == null)
				return table.Rows;
			HashSet<InMemoryRow> rowSet = new HashSet<InMemoryRow>();
			for(int j = 0; j < keys.Result.Length; j++) {
				IndexFinderItem[] key = keys.Result[j];
				object[] keyValues = new object[key.Length];
				for(int i = 0; i < key.Length; i++) {
					IndexFinderItem item = key[i];
					keyValues[i] = item.Value;
				}
				InMemoryRow[] currentRows = indexList[j].Find(keyValues, false);
				for(int i = 0; i < currentRows.Length; i++) {
					InMemoryRow row = currentRows[i];
					rowSet.Add(row);
				}
			}
			return rowSet;
		}
		static void GetRows(IndexFinderResult keys, InMemoryIndexWrapper[] indexList, InMemoryTable table, QuereableEvaluatorContextDescriptor descriptor,
			InMemoryDataElectorContextSource source, ProcessRow process) {
			HashSet<InMemoryRow> rowSet;
			if(keys.Result.Length > 1)
				rowSet = new HashSet<InMemoryRow>();
			else
				rowSet = null;
			for(int j = 0; j < keys.Result.Length; j++) {
				IndexFinderItem[] key = keys.Result[j];
				object[] keyValues = new object[key.Length];
				for(int i = 0; i < key.Length; i++) {
					IndexFinderItem item = key[i];
					if(item.ValueIsQueryOperand)
						keyValues[i] = descriptor.GetOperandValue(source, (QueryOperand)item.Value);
					else
						keyValues[i] = item.Value;
				}
				InMemoryRow[] currentRows = indexList[j].Find(keyValues, false);
				for(int i = 0; i < currentRows.Length; i++) {
					InMemoryRow row = currentRows[i];
					if(rowSet == null)
						process(row);
					else if(rowSet.Add(row)) {
						process(row);
					}
				}
			}
		}
		InMemoryComplexSet ProcessIfInnerJoin(InMemoryDataElectorContextDescriptor descriptor) {
			if(queryOperandDetectedLeft && queryOperandDetectedRight)
				throw new InMemorySetException(Res.GetString(Res.InMemoryFull_WrongIndexInfo));
			ExpressionEvaluator eval = new QueryableExpressionEvaluator(descriptor, criteria, descriptor.CaseSensitive, descriptor.CustomFunctions, descriptor.CustomAggregates);
			InMemoryComplexSet resultSet = new InMemoryComplexSet();
			int tableLeftIndex = resultSet.AddTable(tableLeftAlias, tableLeft);
			int tableRightIndex = resultSet.AddTable(tableRightAlias, tableRight);
			if(queryOperandDetectedLeft) {
				IEnumerable<InMemoryRow> rightRows = GetRows(keysRight, indexListRight, tableRight);
				foreach(InMemoryRow rightRow in rightRows) {
					GetRows(keysLeft, indexListLeft, tableLeft, descriptor, new InMemoryDataElectorContextSource(tableRightAlias, rightRow, tableRight), delegate (InMemoryRow leftRow) {
						AddRow(eval, resultSet, tableLeftIndex, tableRightIndex, leftRow, rightRow);
					});
				}
			}
			else {
				if(queryOperandDetectedRight) {
					IEnumerable<InMemoryRow> leftRows = GetRows(keysLeft, indexListLeft, tableLeft);
					foreach(InMemoryRow leftRow in leftRows) {
						GetRows(keysRight, indexListRight, tableRight, descriptor, new InMemoryDataElectorContextSource(tableLeftAlias, leftRow, tableLeft), delegate (InMemoryRow rightRow) {
							AddRow(eval, resultSet, tableRightIndex, tableLeftIndex, rightRow, leftRow);
						});
					}
				}
				else {
					IEnumerable<InMemoryRow> rightRows = GetRows(keysRight, indexListRight, tableRight);
					IEnumerable<InMemoryRow> leftRows = GetRows(keysLeft, indexListLeft, tableLeft);
					foreach(InMemoryRow rightRow in rightRows) {
						foreach(InMemoryRow leftRow in leftRows)
							AddRow(eval, resultSet, tableLeftIndex, tableRightIndex, leftRow, rightRow);
					}
				}
			}
			return resultSet;
		}
	}
	public class InMemoryDataElectorTableJoinSearch : IInMemoryDataElector {
		bool isTableLeft;
		string tableAlias;
		InMemoryTable table;
		IInMemoryDataElector inputData;
		CriteriaOperator criteria;
		JoinType joinType;
		IndexFinderResult keys;
		InMemoryIndexWrapper[] indexList;
		public InMemoryDataElectorTableJoinSearch(string tableAlias, InMemoryTable table, IInMemoryDataElector inputData, CriteriaOperator criteria, JoinType joinType, bool isTableLeft) {
			Guard.ArgumentNotNull(table, nameof(table));
			Guard.ArgumentIsNotNullOrEmpty(tableAlias, nameof(tableAlias));
			this.tableAlias = tableAlias;
			this.table = table;
			this.inputData = inputData;
			this.criteria = criteria;
			this.joinType = joinType;
			this.isTableLeft = isTableLeft;
			bool searchOperandProperties = !isTableLeft || joinType == JoinType.Inner;
			this.keys = new IndexFinder(tableAlias, table, searchOperandProperties).Find(criteria);
			this.indexList = IndexFinder.GetIndexList(table, keys);
		}
		public InMemoryComplexSet Process(InMemoryDataElectorContextDescriptor descriptor) {
			if(isTableLeft && joinType == JoinType.LeftOuter) return ProcessIfTableLeft(descriptor);
			return ProcessIfTableRightOrInnerJoin(descriptor);
		}
		InMemoryComplexSet ProcessIfTableLeft(InMemoryDataElectorContextDescriptor descriptor) {
			InMemoryComplexSet resultSet = inputData.Process(descriptor);
			ExpressionEvaluator eval = new QueryableExpressionEvaluator(descriptor, criteria, descriptor.CaseSensitive, descriptor.CustomFunctions, descriptor.CustomAggregates);
			int tableIndex = resultSet.AddTable(tableAlias, table);
			IEnumerable<InMemoryRow> rows;
			int rowsCount;
			if(keys == null) {
				rows = table.Rows;
				rowsCount = table.Rows.Count;
			}
			else {
				HashSet<InMemoryRow> rowSet = new HashSet<InMemoryRow>();
				for(int j = 0; j < keys.Result.Length; j++) {
					IndexFinderItem[] key = keys.Result[j];
					InMemoryIndexWrapper index = indexList[j];
					object[] keyValues = new object[key.Length];
					for(int i = 0; i < key.Length; i++) {
						if(key[i].ValueIsQueryOperand) throw new InMemorySetException(Res.GetString(Res.InMemoryFull_WrongIndexInfo));
						keyValues[i] = key[i].Value;
					}
					InMemoryRow[] currentRows = index.Find(keyValues, false);
					for(int i = 0; i < currentRows.Length; i++) {
						InMemoryRow row = currentRows[i];
						rowSet.Add(row);
					}
				}
				rows = rowSet;
				rowsCount = rowSet.Count;
			}
			ComplexSetFitIfTableLeft(resultSet, rows, rowsCount, tableIndex, delegate (InMemoryRow tableRow) {
				List<InMemoryComplexRow> fitRows = null;
				for(int j = 0; j < resultSet.Count; j++) {
					InMemoryComplexRow complexRow = resultSet[j];
					if(eval.Fit(new InMemoryDataElectorContextSource(tableAlias, tableRow, table, complexRow))) {
						if(fitRows == null) fitRows = new List<InMemoryComplexRow>();
						fitRows.Add(complexRow);
					}
				}
				return fitRows;
			});
			return resultSet;
		}
		InMemoryComplexSet ProcessIfTableRightOrInnerJoin(InMemoryDataElectorContextDescriptor descriptor) {
			InMemoryComplexSet resultSet = inputData.Process(descriptor);
			ExpressionEvaluator eval = new QueryableExpressionEvaluator(descriptor, criteria, descriptor.CaseSensitive, descriptor.CustomFunctions, descriptor.CustomAggregates);
			int tableIndex = resultSet.AddTable(tableAlias, table);
			InMemoryRowList rows = table.Rows;
			if(keys == null) {
				ComplexSetFitIfTableRight(resultSet, tableIndex, delegate (InMemoryComplexRow resultRow) {
					List<InMemoryRow> fitRows = null;
					for(int i = 0; i < rows.Count; i++) {
						if(rows[i].State != InMemoryItemState.Deleted && eval.Fit(new InMemoryDataElectorContextSource(tableAlias, rows[i], table, resultRow))) {
							if(fitRows == null) fitRows = new List<InMemoryRow>();
							fitRows.Add(rows[i]);
						}
					}
					return fitRows;
				});
			}
			else {
				ComplexSetFitIfTableRight(resultSet, tableIndex, delegate (InMemoryComplexRow resultRow) {
					List<InMemoryRow> fitRows = null;
					HashSet<InMemoryRow> rowSet = new HashSet<InMemoryRow>();
					for(int j = 0; j < keys.Result.Length; j++) {
						IndexFinderItem[] key = keys.Result[j];
						InMemoryIndexWrapper index = indexList[j];
						object[] keyValues = new object[key.Length];
						for(int i = 0; i < key.Length; i++) {
							keyValues[i] = ProcessKey(descriptor, key[i], resultRow);
						}
						InMemoryRow[] currentRows = index.Find(keyValues, false);
						for(int i = 0; i < currentRows.Length; i++) {
							InMemoryRow row = currentRows[i];
							if(row != null && !rowSet.Contains(row)
										&& eval.Fit(new InMemoryDataElectorContextSource(tableAlias, row, table, resultRow))) {
								if(fitRows == null) fitRows = new List<InMemoryRow>();
								fitRows.Add(row);
								rowSet.Add(row);
							}
						}
					}
					return fitRows;
				});
			}
			return resultSet;
		}
		object ProcessKey(InMemoryDataElectorContextDescriptor descriptor, IndexFinderItem keyItem, InMemoryComplexRow resultRow) {
			if(keyItem.ValueIsQueryOperand) {
				return descriptor.GetOperandValue(resultRow, (QueryOperand)keyItem.Value);
			}
			else
				return keyItem.Value;
		}
		void ComplexSetFitIfTableLeft(InMemoryComplexSet resultSet, IEnumerable<InMemoryRow> rows, int rowsCount, int tableIndex, InMemoryRowFitHandler fit) {
			HashSet<InMemoryComplexRow> complexRowSet = new HashSet<InMemoryComplexRow>();
			List<InMemoryComplexRow> insertList = new List<InMemoryComplexRow>(rowsCount);
			foreach(InMemoryRow resultRow in rows) {
				if(resultRow.State == InMemoryItemState.Deleted) continue;
				List<InMemoryComplexRow> fitRows = fit(resultRow);
				if(fitRows == null) {
					InMemoryComplexRow newRow = new InMemoryComplexRow(resultSet);
					newRow[tableIndex] = resultRow;
					insertList.Add(newRow);
				}
				else {
					for(int i = fitRows.Count - 1; i >= 0; i--) {
						InMemoryComplexRow fitRow = fitRows[i];
						if(i == 0 && !complexRowSet.Contains(fitRow)) {
							fitRow[tableIndex] = resultRow;
							insertList.Add(fitRow);
							complexRowSet.Add(fitRow);
							break;
						}
						InMemoryComplexRow newRow = new InMemoryComplexRow(fitRow);
						newRow[tableIndex] = resultRow;
						insertList.Add(newRow);
					}
				}
			}
			resultSet.Clear();
			resultSet.AddRows(insertList);
		}
		void ComplexSetFitIfTableRight(InMemoryComplexSet resultSet, int tableIndex, InMemoryComplexRowFitHandler fit) {
			int deleteFirstIndex = 0;
			int deleteCount = 0;
			List<InMemoryComplexRow> insertList = new List<InMemoryComplexRow>();
			for(int j = 0; j < resultSet.Count; j++) {
				InMemoryComplexRow resultRow = resultSet[j];
				List<InMemoryRow> fitRows = fit(resultRow);
				if(fitRows == null) {
					if(joinType == JoinType.Inner) {
						if(deleteCount == 0) deleteFirstIndex = j;
						deleteCount++;
					}
				}
				else {
					if(deleteCount > 0) {
						if(deleteCount == 1) {
							resultSet.RemoveAt(deleteFirstIndex);
							j--;
						}
						else {
							resultSet.RemoveRange(deleteFirstIndex, deleteCount);
							j -= deleteCount;
						}
						deleteCount = 0;
					}
					for(int i = fitRows.Count - 1; i >= 0; i--) {
						if(i == 0) {
							resultRow[tableIndex] = fitRows[i];
							break;
						}
						InMemoryComplexRow newRow = new InMemoryComplexRow(resultRow);
						newRow[tableIndex] = fitRows[i];
						insertList.Add(newRow);
					}
				}
			}
			if(deleteCount > 0) {
				if(deleteCount == 1) resultSet.RemoveAt(deleteFirstIndex);
				else resultSet.RemoveRange(deleteFirstIndex, deleteCount);
			}
			resultSet.AddRows(insertList);
		}
	}
	public class InMemoryDataElectorResultJoinSearch : IInMemoryDataElector {
		IInMemoryDataElector inputDataLeft;
		IInMemoryDataElector inputDataRight;
		CriteriaOperator criteria;
		JoinType joinType;
		public InMemoryDataElectorResultJoinSearch(IInMemoryDataElector inputDataLeft, IInMemoryDataElector inputDataRight, CriteriaOperator criteria, JoinType joinType) {
			this.inputDataLeft = inputDataLeft;
			this.inputDataRight = inputDataRight;
			this.criteria = criteria;
			this.joinType = joinType;
		}
		public InMemoryComplexSet Process(InMemoryDataElectorContextDescriptor descriptor) {
			InMemoryComplexSet resultSetLeft = inputDataLeft.Process(descriptor);
			InMemoryComplexSet resultSetRight = inputDataRight.Process(descriptor);
			ExpressionEvaluator eval = new QueryableExpressionEvaluator(descriptor, criteria, descriptor.CaseSensitive, descriptor.CustomFunctions, descriptor.CustomAggregates);
			int leftOldTableCount = resultSetLeft.TableCount;
			int rightTableCount = resultSetRight.TableCount;
			ReadOnlyCollection<string> aliasRightList = resultSetRight.GetAliasList();
			ReadOnlyCollection<IInMemoryTable> tableRightList = resultSetRight.GetTableList();
			for(int i = 0; i < aliasRightList.Count; i++) {
				resultSetLeft.AddTable(aliasRightList[i], tableRightList[i]);
			}
			resultSetLeft.AddProjectionFromSet(resultSetRight);
			int deleteFirstIndex = 0;
			int deleteCount = 0;
			List<InMemoryComplexRow> insertList = new List<InMemoryComplexRow>();
			for(int j = 0; j < resultSetLeft.Count; j++) {
				InMemoryComplexRow leftRow = resultSetLeft[j];
				List<InMemoryComplexRow> fitRightRows = null;
				for(int i = 0; i < resultSetRight.Count; i++) {
					if(!eval.Fit(new InMemoryDataElectorContextSource(leftRow, resultSetRight[i]))) continue;
					if(fitRightRows == null) fitRightRows = new List<InMemoryComplexRow>();
					fitRightRows.Add(resultSetRight[i]);
				}
				if(fitRightRows == null) {
					if(joinType == JoinType.Inner) {
						if(deleteCount == 0) deleteFirstIndex = j;
						deleteCount++;
					}
				}
				else {
					if(deleteCount > 0) {
						if(deleteCount == 1) {
							resultSetLeft.RemoveAt(deleteFirstIndex);
							j--;
						}
						else {
							resultSetLeft.RemoveRange(deleteFirstIndex, deleteCount);
							j -= deleteCount;
						}
						deleteCount = 0;
					}
					for(int i = fitRightRows.Count - 1; i >= 0; i--) {
						if(i == 0) {
							AddRowToRow(leftRow, fitRightRows[i], leftOldTableCount, rightTableCount);
							break;
						}
						InMemoryComplexRow newRow = new InMemoryComplexRow(leftRow);
						AddRowToRow(newRow, fitRightRows[i], leftOldTableCount, rightTableCount);
						insertList.Add(newRow);
					}
				}
			}
			if(deleteCount > 0) {
				if(deleteCount == 1) resultSetLeft.RemoveAt(deleteFirstIndex);
				else resultSetLeft.RemoveRange(deleteFirstIndex, deleteCount);
			}
			resultSetLeft.AddRows(insertList);
			return resultSetLeft;
		}
		static void AddRowToRow(InMemoryComplexRow leftRow, InMemoryComplexRow rightRow, int leftCount, int rightCount) {
			for(int i = 0; i < rightCount; i++) {
				leftRow[leftCount + i] = rightRow[i];
			}
		}
	}
	public class InMemoryDataElectorSource : IInMemoryDataElector {
		const string WrongSource = "Wrong source.";
		object source;
		public object Source {
			get { return source; }
			set { source = value; }
		}
		public InMemoryDataElectorSource(object source) {
			this.source = source;
		}
		public static void FillNodesDict(object source, HashSet<string> nodesSet) {
			List<InMemoryComplexRow> list = source as List<InMemoryComplexRow>;
			if(list != null) {
				if(list.Count > 0) {
					foreach(string alias in list[0].ComplexSet.GetAliasList()) {
						nodesSet.Add(alias);
					}
				}
				return;
			}
			InMemoryComplexRow complexRow = source as InMemoryComplexRow;
			if(complexRow != null) {
				foreach(string alias in complexRow.ComplexSet.GetAliasList()) {
					nodesSet.Add(alias);
				}
				return;
			}
			InMemoryDataElectorContextSource contextSource = source as InMemoryDataElectorContextSource;
			if(contextSource != null) {
				if(contextSource.ComplexRow != null) {
					ReadOnlyCollection<string> aliasList = contextSource.ComplexRow.ComplexSet.GetAliasList();
					foreach(string alias in aliasList) {
						nodesSet.Add(alias);
					}
				}
				if(contextSource.ComplexRowRight != null) {
					ReadOnlyCollection<string> aliasList = contextSource.ComplexRowRight.ComplexSet.GetAliasList();
					foreach(string alias in aliasList) {
						nodesSet.Add(alias);
					}
				}
				if(contextSource.NodeAlias != null) nodesSet.Add(contextSource.NodeAlias);
				return;
			}
			throw new ArgumentException(WrongSource);
		}
		public InMemoryComplexSet Process(InMemoryDataElectorContextDescriptor descriptor) {
			if(source == null) return new InMemoryComplexSet();
			List<InMemoryComplexRow> list = source as List<InMemoryComplexRow>;
			if(list != null) {
				return ProcessComplexRowList(list);
			}
			InMemoryComplexRow complexRow = source as InMemoryComplexRow;
			if(complexRow != null) {
				return ProcessComplexRow(complexRow);
			}
			InMemoryDataElectorContextSource contextSource = source as InMemoryDataElectorContextSource;
			if(contextSource != null) {
				return ProcessContextSource(contextSource);
			}
			throw new InvalidOperationException(WrongSource);
		}
		static InMemoryComplexSet ProcessContextSource(InMemoryDataElectorContextSource source) {
			if(source.ComplexRow != null) {
				InMemoryComplexSet resultSet = ProcessComplexRow(source.ComplexRow);
				if(source.ComplexRowRight != null) {
					InMemoryComplexSet inputSetRight = source.ComplexRowRight.ComplexSet;
					int tableCount = inputSetRight.TableCount;
					ReadOnlyCollection<string> aliasList = inputSetRight.GetAliasList();
					ReadOnlyCollection<IInMemoryTable> tableList = inputSetRight.GetTableList();
					List<int> tableIndexList = new List<int>();
					for(int i = 0; i < tableCount; i++) {
						tableIndexList.Add(resultSet.AddTableIfNotExists(aliasList[i], tableList[i]));
					}
					for(int i = 0; i < tableCount; i++) {
						resultSet[0][tableIndexList[i]] = source.ComplexRowRight[i];
					}
					resultSet.AddProjectionFromSet(inputSetRight);
				}
				else if(source.NodeAlias != null) {
					int tableIndex = resultSet.AddTableIfNotExists(source.NodeAlias, source.Table);
					resultSet[0][tableIndex] = source.Row;
				}
				return resultSet;
			}
			else {
				if(source.NodeAlias != null) {
					InMemoryComplexSet resultSet = new InMemoryComplexSet(1);
					resultSet.AddTable(source.NodeAlias, source.Table);
					resultSet.AddNewRow(0, source.Row);
					return resultSet;
				}
			}
			throw new InvalidOperationException(WrongSource);
		}
		static InMemoryComplexSet ProcessComplexRow(InMemoryComplexRow row) {
			InMemoryComplexSet resultSet = new InMemoryComplexSet(1);
			InMemoryComplexSet inputSet = row.ComplexSet;
			ReadOnlyCollection<string> aliasList = inputSet.GetAliasList();
			ReadOnlyCollection<IInMemoryTable> tableList = inputSet.GetTableList();
			for(int i = 0; i < aliasList.Count; i++) {
				resultSet.AddTable(aliasList[i], tableList[i]);
			}
			resultSet.AddProjectionFromSet(inputSet);
			resultSet.AddRow(new InMemoryComplexRow(resultSet, row));
			return resultSet;
		}
		static InMemoryComplexSet ProcessComplexRowList(List<InMemoryComplexRow> list) {
			InMemoryComplexSet resultSet = new InMemoryComplexSet(list.Count);
			InMemoryComplexSet inputSet = list[0].ComplexSet;
			ReadOnlyCollection<string> aliasList = inputSet.GetAliasList();
			ReadOnlyCollection<IInMemoryTable> tableList = inputSet.GetTableList();
			for(int i = 0; i < aliasList.Count; i++) {
				resultSet.AddTable(aliasList[i], tableList[i]);
			}
			resultSet.AddProjectionFromSet(inputSet);
			for(int i = 0; i < list.Count; i++) {
				resultSet.AddRow(new InMemoryComplexRow(resultSet, list[i]));
			}
			return resultSet;
		}
	}
	public class InMemoryDataElectorResultSearch : IInMemoryDataElector {
		IInMemoryDataElector inputData;
		CriteriaOperator criteria;
		public InMemoryDataElectorResultSearch(IInMemoryDataElector inputData, CriteriaOperator criteria) {
			this.inputData = inputData;
			this.criteria = criteria;
		}
		public InMemoryComplexSet Process(InMemoryDataElectorContextDescriptor descriptor) {
			InMemoryComplexSet resultSet = inputData.Process(descriptor);
			ExpressionEvaluator eval = new QueryableExpressionEvaluator(descriptor, criteria, descriptor.CaseSensitive, descriptor.CustomFunctions, descriptor.CustomAggregates);
			int deleteFirstIndex = 0;
			int deleteCount = 0;
			for(int j = 0; j < resultSet.Count; j++) {
				if(!eval.Fit(resultSet[j])) {
					if(deleteCount == 0) deleteFirstIndex = j;
					deleteCount++;
				}
				else {
					if(deleteCount > 0) {
						if(deleteCount == 1) {
							resultSet.RemoveAt(deleteFirstIndex);
							j--;
						}
						else {
							resultSet.RemoveRange(deleteFirstIndex, deleteCount);
							j -= deleteCount;
						}
						deleteCount = 0;
					}
				}
			}
			if(deleteCount > 0) {
				if(deleteCount == 1) resultSet.RemoveAt(deleteFirstIndex);
				else resultSet.RemoveRange(deleteFirstIndex, deleteCount);
			}
			return resultSet;
		}
	}
	public class QueryParamsReprocessor : IQueryCriteriaVisitor<CriteriaOperator> {
		TaggedParametersHolder identitiesByTag;
		CriteriaOperator ICriteriaVisitor<CriteriaOperator>.Visit(OperandValue theOperand) {
			return identitiesByTag.ConsolidateParameter(theOperand);
		}
		CriteriaOperator IQueryCriteriaVisitor<CriteriaOperator>.Visit(QueryOperand operand) {
			return operand;
		}
		CriteriaOperator IQueryCriteriaVisitor<CriteriaOperator>.Visit(QuerySubQueryContainer container) {
			return container;
		}
		public static CriteriaOperator ReprocessCriteria(CriteriaOperator op, TaggedParametersHolder identitiesByTag) {
			return new QueryParamsReprocessor().Reprocess(op, identitiesByTag);
		}
		public CriteriaOperator Reprocess(CriteriaOperator op, TaggedParametersHolder identitiesByTag) {
			this.identitiesByTag = identitiesByTag;
			return Process(op);
		}
		CriteriaOperator Process(CriteriaOperator op) {
			if(ReferenceEquals(op, null))
				return null;
			return op.Accept(this);
		}
		CriteriaOperator[] Process(CriteriaOperatorCollection operands) {
			CriteriaOperator[] newOperands = new CriteriaOperator[operands.Count];
			int count = newOperands.Length;
			for(int i = 0; i < count; ++i) {
				newOperands[i] = Process((CriteriaOperator)operands[i]);
			}
			return newOperands;
		}
		CriteriaOperator ICriteriaVisitor<CriteriaOperator>.Visit(BetweenOperator theOperator) {
			return new BetweenOperator(Process(theOperator.TestExpression), Process(theOperator.BeginExpression), Process(theOperator.EndExpression));
		}
		CriteriaOperator ICriteriaVisitor<CriteriaOperator>.Visit(BinaryOperator theOperator) {
			return new BinaryOperator(Process(theOperator.LeftOperand), Process(theOperator.RightOperand), theOperator.OperatorType);
		}
		CriteriaOperator ICriteriaVisitor<CriteriaOperator>.Visit(InOperator theOperator) {
			return new InOperator(Process(theOperator.LeftOperand), Process(theOperator.Operands));
		}
		CriteriaOperator ICriteriaVisitor<CriteriaOperator>.Visit(GroupOperator theOperator) {
			return GroupOperator.Combine(theOperator.OperatorType, Process(theOperator.Operands));
		}
		CriteriaOperator ICriteriaVisitor<CriteriaOperator>.Visit(UnaryOperator theOperator) {
			return new UnaryOperator(theOperator.OperatorType, Process(theOperator.Operand));
		}
		CriteriaOperator ICriteriaVisitor<CriteriaOperator>.Visit(FunctionOperator theOperator) {
			return new FunctionOperator(theOperator.OperatorType, Process(theOperator.Operands));
		}
	}
}
namespace DevExpress.Xpo.DB {
	using System.Collections.Generic;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using System.Xml;
	using DevExpress.Data.Helpers;
	using DevExpress.Xpo.DB.Helpers;
	using DevExpress.Xpo.Logger;
	public class InMemoryDataStore : DataStoreBase, IDataStoreSchemaExplorer, IDataStoreSchemaExplorerSp {
		public const string XpoProviderTypeString = "InMemoryDataStore";
		public static string GetConnectionString(string path) {
			return String.Format("{0}={1};data source={2};", DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString, ConnectionProviderSql.EscapeConnectionStringArgument(path));
		}
		public static string GetConnectionString(string path, bool readOnly) {
			return String.Format("{0}={1};data source={2};read only={3}", DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString, ConnectionProviderSql.EscapeConnectionStringArgument(path), readOnly);
		}
		public static string GetConnectionStringInMemory(bool caseSensitive) {
			return String.Format("{0}={1};case sensitive={2}", DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString, caseSensitive);
		}
		public static IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			ConnectionStringParser parser = new ConnectionStringParser(connectionString);
			string path = parser.GetPartByName("data source");
			if(string.IsNullOrEmpty(path)) {
				objectsToDisposeOnDisconnect = Array.Empty<IDisposable>();
				string caseSensitive = parser.GetPartByName("case sensitive");
				if(string.IsNullOrEmpty(caseSensitive)) {
					return new InMemoryDataStore(autoCreateOption);
				}
				bool isCaseSensitive = caseSensitive == "1" || caseSensitive.ToLower() == "true";
				return new InMemoryDataStore(autoCreateOption, isCaseSensitive);
			}
			string readOnly = parser.GetPartByName("read only");
			bool isReadOnly = readOnly == "1" || readOnly.ToLower() == "true";
			try {
				XmlFileDataStore result = new XmlFileDataStore(path.Trim('"'), autoCreateOption, isReadOnly);
				objectsToDisposeOnDisconnect = new IDisposable[] { result };
				return result;
			}
			catch(Exception e) {
				throw new UnableToOpenDatabaseException(connectionString, (e is UnableToOpenDatabaseException) ? e.InnerException : e);
			}
		}
		static InMemoryDataStore() {
			RegisterDataStoreProvider(XpoProviderTypeString, new DataStoreCreationFromStringDelegate(CreateProviderFromString));
			RegisterFactory(new InMemoryProviderFactory());
		}
		public static void Register() { }
		bool caseSensitive;
		readonly InMemorySet InMemorySet;
		readonly IInMemoryPlanner planner;
		[Description("Gets whether the InMemoryDataStore performs case-sensitive comparisons for strings during expression evaluations or sorting.")]
		[Browsable(false)]
		public bool CaseSensitive { get { return caseSensitive; } }
		[Description("Gets whether the InMemoryDataStore object is allowed to create a schema in the associated DataSet.")]
		[Browsable(false)]
		public bool CanCreateSchema { get { return AutoCreateOption == AutoCreateOption.SchemaOnly || AutoCreateOption == AutoCreateOption.DatabaseAndSchema; } }
		[Description("Gets an object that can be used to synchronize access to the InMemoryDataStore.")]
		[Obsolete("SyncRoot is obsolette, use LockHelper.Lock() or LockHelper.LockAsync() instead.")]
		[Browsable(false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override object SyncRoot { get { return this.InMemorySet; } }
		public InMemoryDataStore() : this(AutoCreateOption.DatabaseAndSchema) { }
		public InMemoryDataStore(AutoCreateOption autoCreateOption) : this(autoCreateOption, true) { }
		public InMemoryDataStore(AutoCreateOption autoCreateOption, bool caseSensitive)
			: base(autoCreateOption) {
			this.InMemorySet = new InMemorySet(caseSensitive);
			this.planner = new InMemoryWeighedPlanner(InMemorySet);
			this.caseSensitive = caseSensitive;
		}
		public InMemoryDataStore(InMemoryDataStore originalStore, AutoCreateOption autoCreateOption)
			: base(autoCreateOption) {
			this.InMemorySet = originalStore.InMemorySet;
			this.planner = originalStore.planner;
			this.caseSensitive = originalStore.CaseSensitive;
		}
		protected override UpdateSchemaResult ProcessUpdateSchema(bool skipIfFirstTableNotExists, params DBTable[] tables) {
			return LogManager.Log<UpdateSchemaResult>(LogManager.LogCategorySQL, () => {
				if(skipIfFirstTableNotExists && tables.Length > 0) {
					IEnumerator te = tables.GetEnumerator();
					te.MoveNext();
					if(DataSetStoreHelpersFull.QueryTable(InMemorySet, ((DBTable)te.Current).Name) == null)
						return UpdateSchemaResult.FirstTableNotExists;
				}
				if(CanCreateSchema) {
					InMemorySet.BeginUpdateSchema();
					try {
						foreach(DBTable table in tables) {
							InMemoryTable newTable = DataSetStoreHelpersFull.CreateIfNotExists(InMemorySet, table);
							foreach(DBColumn column in table.Columns) {
								DataSetStoreHelpersFull.CreateIfNotExists(newTable, column);
							}
							if(table.PrimaryKey != null)
								DataSetStoreHelpersFull.CreateIfNotExists(newTable, table.PrimaryKey);
							foreach(DBIndex index in table.Indexes) {
								DataSetStoreHelpersFull.CreateIfNotExists(newTable, index);
							}
						}
						foreach(DBTable table in tables) {
							InMemoryTable newTable = DataSetStoreHelpersFull.GetTable(InMemorySet, table.Name);
							foreach(DBForeignKey fk in table.ForeignKeys) {
								DataSetStoreHelpersFull.CreateIfNotExists(newTable, fk);
							}
						}
					}
					finally {
						InMemorySet.EndUpdateSchema();
					}
				}
				return UpdateSchemaResult.SchemaExists;
			}, (d) => {
				return LogMessage.CreateMessage(this, string.Concat("UpdateSchema: ",
					LogMessage.CollectionToString<DBTable>(tables, delegate (DBTable table) { return table.Name; })), d);
			});
		}
		protected override Task<UpdateSchemaResult> ProcessUpdateSchemaAsync(AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken, bool doNotCreateIfFirstTableNotExist, DBTable[] tables) {
			return Task.FromResult(ProcessUpdateSchema(doNotCreateIfFirstTableNotExist, tables));
		}
		readonly CustomFunctionCollection customFunctionCollection = new CustomFunctionCollection();
		readonly CustomAggregateCollection customAggregateCollection = new CustomAggregateCollection();
		public void RegisterCustomFunctionOperators(ICollection<ICustomFunctionOperator> customFunctions) {
			foreach(ICustomFunctionOperator function in customFunctions) {
				RegisterCustomFunctionOperator(function);
			}
		}
		public void RegisterCustomFunctionOperator(ICustomFunctionOperator customFunction) {
			if(customFunction == null) throw new ArgumentNullException(nameof(customFunction));
			customFunctionCollection.Add(customFunction);
		}
		public void RegisterCustomAggregates(ICollection<ICustomAggregate> customAggregates) {
			foreach(ICustomAggregate aggregate in customAggregates) {
				RegisterCustomAggregate(aggregate);
			}
		}
		public void RegisterCustomAggregate(ICustomAggregate customAggregate) {
			if(customAggregate == null) throw new ArgumentNullException(nameof(customAggregate));
			customAggregateCollection.Add(customAggregate);
		}
		protected SelectStatementResult GetDataNormal(SelectStatement root) {
			CriteriaOperator condition = root.Condition;
			InMemoryDataElectorContextDescriptor descriptor = new InMemoryDataElectorContextDescriptor(planner, CaseSensitive, customFunctionCollection, customAggregateCollection);
			ExpressionEvaluator[] dataEvaluators = DataSetStoreHelpersFull.PrepareDataEvaluators(root.Operands, descriptor, CaseSensitive, customFunctionCollection, customAggregateCollection);
			SortingComparerFull sortingComparer = DataSetStoreHelpersFull.PrepareSortingComparer(root.SortProperties, descriptor, CaseSensitive, customFunctionCollection, customAggregateCollection);
			IInMemoryDataElector dataElector = planner.GetPlan(root);
			return DataSetStoreHelpersFull.DoGetData(dataElector, descriptor, dataEvaluators, sortingComparer, root.SkipSelectedRecords, root.TopSelectedRecords);
		}
		protected SelectStatementResult GetDataGrouped(SelectStatement root) {
			CriteriaOperator condition = root.Condition;
			InMemoryDataElectorContextDescriptor descriptor = new InMemoryDataElectorContextDescriptor(planner, CaseSensitive, customFunctionCollection, customAggregateCollection);
			ExpressionEvaluator[] groupEvaluators = DataSetStoreHelpersFull.PrepareDataEvaluators(root.GroupProperties, descriptor, CaseSensitive, customFunctionCollection, customAggregateCollection);
			ExpressionEvaluator[] dataEvaluators = DataSetStoreHelpersFull.PrepareDataEvaluators(root.Operands, descriptor, CaseSensitive, customFunctionCollection, customAggregateCollection);
			ExpressionEvaluator havingEvaluator = DataSetStoreHelpersFull.PrepareDataEvaluator(root.GroupCondition, descriptor, CaseSensitive, customFunctionCollection, customAggregateCollection);
			SortingListComparerFull sortingComparer = DataSetStoreHelpersFull.PrepareSortingListComparer(root.SortProperties, descriptor, CaseSensitive, customFunctionCollection, customAggregateCollection);
			IInMemoryDataElector dataElector = planner.GetPlan(root);
			return DataSetStoreHelpersFull.DoGetGroupedData(dataElector, descriptor, groupEvaluators, havingEvaluator, sortingComparer, root.SkipSelectedRecords, root.TopSelectedRecords, dataEvaluators);
		}
		protected override SelectStatementResult ProcessSelectData(SelectStatement selects) {
			return LogManager.Log<SelectStatementResult>(LogManager.LogCategorySQL, () => {
				try {
					if(IsTopLevelAggregateCheckerFull.IsGrouped(selects)) {
						return GetDataGrouped(selects);
					}
					else {
						return GetDataNormal(selects);
					}
				}
				catch(InvalidPropertyPathException e) {
					throw new SchemaCorrectionNeededException(e);
				}
			}, (d) => {
				if(selects == null) return null;
				return LogMessage.CreateMessage(this, selects.ToString(), d);
			});
		}
		protected override Task<SelectStatementResult> ProcessSelectDataAsync(SelectStatement selects, AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken) {
			cancellationToken.ThrowIfCancellationRequested();
			return Task.FromResult(ProcessSelectData(selects));
		}
		protected override ModificationResult ProcessModifyData(params ModificationStatement[] dmlStatements) {
			return LogManager.LogMany<ModificationResult>(LogManager.LogCategorySQL, () => {
				BeginTransaction();
				try {
					TaggedParametersHolder identitiesByTag = new TaggedParametersHolder();
					List<ParameterValue> result = new List<ParameterValue>();
					int count = dmlStatements.Length;
					for(int i = 0; i < count; i++) {
						ModificationStatement root = dmlStatements[i];
						int recordsUpdated;
						try {
							InMemoryTable table = DataSetStoreHelpersFull.GetTable(InMemorySet, root.Table.Name);
							if(root is InsertStatement) {
								ParameterValue identityParameter = ((InsertStatement)root).IdentityParameter;
								recordsUpdated = DataSetStoreHelpersFull.DoInsertRecord(table, ((InsertStatement)root).Parameters, root.Operands, identityParameter, identitiesByTag);
								if(!ReferenceEquals(identityParameter, null))
									result.Add(identityParameter);
							}
							else if(root is UpdateStatement) {
								recordsUpdated = DataSetStoreHelpersFull.DoUpdateRecord(planner, table, ((UpdateStatement)root).Parameters, root.Operands, identitiesByTag, root.Condition, CaseSensitive, customFunctionCollection, customAggregateCollection);
							}
							else if(root is DeleteStatement) {
								recordsUpdated = DataSetStoreHelpersFull.DoDeleteRecord(planner, table, root.Condition, CaseSensitive, customFunctionCollection, customAggregateCollection);
							}
							else {
								throw new InvalidOperationException();	
							}
						}
						catch(InvalidPropertyPathException e) {
							throw new SchemaCorrectionNeededException(e.Message, e);
						}
						catch(SchemaCorrectionNeededException) {
							throw;
						}
						catch(InMemoryConstraintException e) {
							throw new ConstraintViolationException(root.ToString(), string.Empty, e);
						}
						catch(Exception e) {
							throw new SqlExecutionErrorException(root.ToString(), string.Empty, e);
						}
						if(root.RecordsAffected != 0 && root.RecordsAffected != recordsUpdated) {
							throw new LockingException();
						}
					}
					try {
						DoCommit();
					}
					catch(SchemaCorrectionNeededException) {
						throw;
					}
					catch(InMemoryConstraintException e) {
						throw new ConstraintViolationException(string.Empty, string.Empty, e);
					}
					return new ModificationResult(result);
				}
				catch(Exception e) {
					try {
						DoRollback();
					}
					catch(Exception e2) {
						throw new DevExpress.Xpo.Exceptions.ExceptionBundleException(e, e2);
					}
					throw;
				}
			}, (d) => {
				if(dmlStatements == null || dmlStatements.Length == 0) return null;
				LogMessage[] messages = new LogMessage[dmlStatements.Length];
				for(int i = 0; i < dmlStatements.Length; i++) {
					messages[i] = LogMessage.CreateMessage(this, dmlStatements[i].ToString(), d);
				}
				return messages;
			});
		}
		protected override Task<ModificationResult> ProcessModifyDataAsync(AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken, params ModificationStatement[] dmlStatements) {
			cancellationToken.ThrowIfCancellationRequested();
			return Task.FromResult(ProcessModifyData(dmlStatements));
		}
		protected virtual void BeginTransaction() {
			InMemorySet.BeginTransaction();
		}
		protected virtual void DoCommit() {
			InMemorySet.Commit();
		}
		protected virtual void DoRollback() {
			InMemorySet.Rollback();
		}
		static void ClearDataSet(InMemorySet dataSet) {
			dataSet.ClearRelations();
			dataSet.ClearTables();
		}
		protected override void ProcessClearDatabase() {
			ClearDataSet(InMemorySet);
		}
		public DBTable GetTableSchema(string tableName) {
			DBTable result = new DBTable(tableName);
			InMemoryTable table = DataSetStoreHelpersFull.GetTable(InMemorySet, result.Name);
			foreach(InMemoryColumn column in table.Columns) {
				DBColumn dbColumn = new DBColumn(column.Name, table.PrimaryKey != null && table.PrimaryKey.Columns.IndexOf(column) >= 0, null, column.Type == typeof(string) ? column.MaxLength : 0, DBColumn.GetColumnType(column.Type), column.AllowNull, column.AutoIncrement ? null : column.DefaultValue);
				result.AddColumn(dbColumn);
			}
			if(table.PrimaryKey != null && table.PrimaryKey.Columns.Count > 0) {
				StringCollection pkcols = new StringCollection();
				foreach(InMemoryColumn column in table.PrimaryKey.Columns) {
					pkcols.Add(column.Name);
				}
				result.PrimaryKey = new DBPrimaryKey(pkcols);
			}
			foreach(InMemoryIndexWrapper index in table.Indexes) {
				StringCollection cols = new StringCollection();
				foreach(InMemoryColumn column in index.Columns) {
					cols.Add(column.Name);
				}
				result.AddIndex(new DBIndex(index.Name, cols, index.Unique));
			}
			foreach(InMemoryRelation rel in table.FRelations) {
				StringCollection cols = new StringCollection();
				StringCollection relcols = new StringCollection();
				foreach(InMemoryRelationPair pair in rel.Pairs) {
					cols.Add(pair.FKey.Name);
					relcols.Add(pair.PKey.Name);
				}
				result.AddForeignKey(new DBForeignKey(cols, rel.PTable.Name, relcols));
			}
			return result;
		}
		public string[] GetStorageTablesList(bool includeViews) {
			List<string> result = new List<string>();
			foreach(InMemoryTable table in InMemorySet.Tables)
				result.Add(table.Name);
			return result.ToArray();
		}
		public DBTable[] GetStorageTables(params string[] tables) {
			if(tables == null)
				tables = GetStorageTablesList(false);
			int count = tables.Length;
			DBTable[] result = new DBTable[count];
			for(int i = 0; i < count; i++)
				result[i] = GetTableSchema(tables[i]);
			return result;
		}
		public void WriteXml(string fileName) {
			using(LockHelper.Lock()) {
				InMemorySet.WriteXml(fileName);
			}
		}
		public void WriteXml(XmlWriter writer) {
			using(LockHelper.Lock()) {
				InMemorySet.WriteXml(writer);
			}
		}
		public void ReadXml(string fileName) {
			using(LockHelper.Lock()) {
				InMemorySet.ReadXml(fileName);
			}
		}
		public void ReadXml(XmlReader reader) {
			using(LockHelper.Lock()) {
				InMemorySet.ReadXml(reader);
			}
		}
		public void ReadFromInMemoryDataStore(InMemoryDataStore dataStore) {
			using(LockHelper.Lock()) {
				using(dataStore.LockHelper.Lock()) {
					InMemorySet.ReadFromInMemorySet(dataStore.InMemorySet);
				}
			}
		}
		class XmlFileDataStore : DataStoreSerializedBase, IDataStoreForTests, IDataStoreSchemaExplorer, IDataStoreSchemaExplorerSp, IDisposable {
			protected readonly InMemoryDataStore Nested;
			protected readonly FileStream fileStream;
			public XmlFileDataStore(string fileName, AutoCreateOption autoCreateOption, bool readOnly) {
				InMemoryDataStore dataStore = new InMemoryDataStore(autoCreateOption);
				if(!XpoDefault.TryResolveAspDataDirectory(ref fileName)) {
					throw new UnableToOpenDatabaseException(fileName, null);
				}
				try {
					FileMode mode = (autoCreateOption == AutoCreateOption.DatabaseAndSchema) ? FileMode.OpenOrCreate : FileMode.Open;
					fileStream = new FileStream(fileName, mode, readOnly ? FileAccess.Read : FileAccess.ReadWrite);
					if(fileStream.Length > 0) {
						dataStore.ReadXml(SafeXml.CreateReader(fileStream));
					}
				}
				catch(Exception e) {
					throw new UnableToOpenDatabaseException(fileName, e);
				}
				Nested = dataStore;
			}
			void Flush() {
				fileStream.Position = 0;
				fileStream.SetLength(0);
				using(XmlWriter writer = XmlWriter.Create(fileStream)) {
					Nested.WriteXml(writer);
				}
				fileStream.Flush();
			}
			Task FlushAsync(CancellationToken cancellationToken) {
				fileStream.Position = 0;
				fileStream.SetLength(0);
				using(XmlWriter writer = XmlWriter.Create(fileStream)) {
					Nested.WriteXml(writer);
				}
				return fileStream.FlushAsync(cancellationToken);
			}
			public override AutoCreateOption AutoCreateOption {
				get { return Nested.AutoCreateOption; }
			}
			protected override ModificationResult ProcessModifyData(params ModificationStatement[] dmlStatements) {
				if(!fileStream.CanWrite) throw new NotSupportedException(Res.GetString(Res.InMemory_IsReadOnly));
				ModificationResult result = Nested.ModifyData(dmlStatements);
				Flush();
				return result;
			}
			protected override async Task<ModificationResult> ProcessModifyDataAsync(AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken, params ModificationStatement[] dmlStatements) {
				if(!fileStream.CanWrite) throw new NotSupportedException(Res.GetString(Res.InMemory_IsReadOnly));
				ModificationResult result = await Nested.ModifyDataAsync(cancellationToken, dmlStatements).ConfigureAwait(false);
				await FlushAsync(cancellationToken).ConfigureAwait(false);
				return result;
			}
			protected override SelectedData ProcessSelectData(params SelectStatement[] selects) {
				return Nested.SelectData(selects);
			}
			protected override Task<SelectedData> ProcessSelectDataAsync(AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken, params SelectStatement[] selects) {
				return Nested.SelectDataAsync(cancellationToken, selects);
			}
			protected override UpdateSchemaResult ProcessUpdateSchema(bool doNotCreateIfFirstTableNotExist, params DBTable[] tables) {
				int schemaHash = GetSchemaHash(Nested.InMemorySet);
				UpdateSchemaResult result = Nested.UpdateSchema(doNotCreateIfFirstTableNotExist, tables);
				if(result != UpdateSchemaResult.FirstTableNotExists && GetSchemaHash(Nested.InMemorySet) != schemaHash)
					Flush();
				return result;
			}
			protected override async Task<UpdateSchemaResult> ProcessUpdateSchemaAsync(AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken, bool doNotCreateIfFirstTableNotExist, DBTable[] tables) {
				int schemaHash = GetSchemaHash(Nested.InMemorySet);
				UpdateSchemaResult result = await Nested.UpdateSchemaAsync(cancellationToken, doNotCreateIfFirstTableNotExist, tables);
				if(result != UpdateSchemaResult.FirstTableNotExists && GetSchemaHash(Nested.InMemorySet) != schemaHash)
					await FlushAsync(cancellationToken);
				return result;
			}
			static int GetSchemaHash(InMemorySet inMemorySet) {
				int hashCode = HashCodeHelper.StartGeneric(inMemorySet.TablesCount);
				foreach(var table in inMemorySet.Tables) {
					hashCode = HashCodeHelper.CombineGeneric(hashCode, table.Name);
					foreach(var column in table.Columns) {
						hashCode = HashCodeHelper.Combine(hashCode, GetColumnHash(column));
					}
					foreach(var index in table.Indexes) {
						hashCode = HashCodeHelper.CombineGeneric(hashCode, index.Name, index.Unique);
						foreach(var column in index.Columns) {
							hashCode = HashCodeHelper.CombineGeneric(hashCode, column.ColumnIndex);
						}
					}
					if(table.PrimaryKey == null) {
						hashCode = HashCodeHelper.Combine(hashCode, -1);
					}
					else {
						hashCode = HashCodeHelper.CombineGeneric(hashCode, table.PrimaryKey.Name, table.PrimaryKey.Unique);
						foreach(var column in table.PrimaryKey.Columns) {
							hashCode = HashCodeHelper.CombineGeneric(hashCode, column.ColumnIndex);
						}
					}
					foreach(var relation in table.AllRelations) {
						hashCode = HashCodeHelper.CombineGeneric(hashCode, relation.Name);
					}
				}
				return HashCodeHelper.Finish(hashCode);
			}
			static int GetColumnHash(InMemoryColumn column) {
				return HashCodeHelper.CalculateGeneric(column.Name, column.DefaultValue, column.AllowNull, column.AutoIncrement, column.ColumnIndex, column.MaxLength, column.Type);
			}
			[Obsolete("SyncRoot is obsolette, use LockHelper.Lock() or LockHelper.LockAsync() instead.")]
			public override object SyncRoot {
				get { return Nested.SyncRoot; }
			}
			public DBTable[] GetStorageTables(params string[] tables) {
				using(LockHelper.Lock()) {
					return Nested.GetStorageTables(tables);
				}
			}
			public string[] GetStorageTablesList(bool includeViews) {
				using(LockHelper.Lock()) {
					return Nested.GetStorageTablesList(includeViews);
				}
			}
			void IDataStoreForTests.ClearDatabase() {
				using(LockHelper.Lock()) {
					((IDataStoreForTests)Nested).ClearDatabase();
					Flush();
				}
			}
			public void Dispose() {
				fileStream.Close();
			}
			public DBStoredProcedure[] GetStoredProcedures() {
				using(LockHelper.Lock()) {
					return Nested.GetStoredProcedures();
				}
			}
		}
		#region DataSet redirects
		[Obsolete("Use DataSetDataStore class instead of InMemoryDataStore", true), Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
		public DataSet Data { get { throw new NotSupportedException(); } }
		[Obsolete("Use DataSetDataStore class or .ctor without DataSet instead"), Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
		public InMemoryDataStore(DataSet data, AutoCreateOption autoCreateOption) : this(data, autoCreateOption, true) { }
		[Obsolete("Use DataSetDataStore class or .ctor without DataSet instead"), Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
		public InMemoryDataStore(DataSet data, AutoCreateOption autoCreateOption, bool caseSensitive)
			: this(autoCreateOption, caseSensitive) {
			if(data.Tables.Count > 0)
				throw new NotSupportedException(Res.GetString(Res.InMemoryFull_UseDataSetDataStoreOrCtor));
		}
		#endregion
		public DBStoredProcedure[] GetStoredProcedures() {
			throw new NotSupportedException();
		}
	}
	public class InMemoryProviderFactory : ProviderFactory {
		public override IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			throw new NotSupportedException();
		}
		public override IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			return InMemoryDataStore.CreateProviderFromString(connectionString, autoCreateOption, out objectsToDisposeOnDisconnect);
		}
		public override string GetConnectionString(Dictionary<string, string> parameters) {
			if(!parameters.ContainsKey(DatabaseParamID)) { return null; }
			return InMemoryDataStore.GetConnectionString(parameters[DatabaseParamID], parameters.ContainsKey(ReadOnlyParamID) ? parameters[ReadOnlyParamID] != "0" : false);
		}
		public override IDataStore CreateProvider(Dictionary<string, string> parameters, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			string connectionString = GetConnectionString(parameters);
			if(connectionString == null) {
				objectsToDisposeOnDisconnect = Array.Empty<IDisposable>();
				return null;
			}
			ConnectionStringParser helper = new ConnectionStringParser(connectionString);
			helper.RemovePartByName(DataStoreBase.XpoProviderTypeParameterName);
			return CreateProviderFromString(helper.GetConnectionString(), autoCreateOption, out objectsToDisposeOnDisconnect);
		}
		public override bool HasUserName { get { return false; } }
		public override bool HasPassword { get { return false; } }
		public override bool HasIntegratedSecurity { get { return false; } }
		public override bool HasMultipleDatabases { get { return false; } }
		public override bool IsServerbased { get { return false; } }
		public override bool IsFilebased { get { return true; } }
		public override string ProviderKey { get { return "InMemorySetFull"; } }
		public override string[] GetDatabases(string server, string userId, string password) {
			return new string[1] { server };
		}
		public override string FileFilter { get { return "Xml files|*.xml"; } }
		public override bool MeanSchemaGeneration { get { return true; } }
	}
}
