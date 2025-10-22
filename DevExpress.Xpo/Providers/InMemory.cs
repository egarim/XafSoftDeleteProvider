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
using System.Text;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.DB.Exceptions;
using DevExpress.Data.Filtering;
using DevExpress.Data.Filtering.Exceptions;
using DevExpress.Data.Filtering.Helpers;
using System.Reflection;
using System.Globalization;
using System.Collections.Generic;
using DevExpress.Xpo.Helpers;
using DevExpress.Utils;
namespace DevExpress.Xpo.DB.Helpers {
	public class DataViewEvaluatorContextDescriptor : EvaluatorContextDescriptor {
		Hashtable children = new Hashtable();
		object cache;
		bool contextCaching;
		public bool ContextCaching {
			set {
				if(contextCaching == value)
					return;
				contextCaching = value;
				if(!value)
					cache = null;
				if(children.Count > 0) {
					foreach(DataViewEvaluatorContextDescriptor child in children.Values)
						child.ContextCaching = value;
				}
			}
		}
		public readonly DataTable Table;
		DataRelation relation;
		public DataViewEvaluatorContextDescriptor(DataRelation relation) {
			this.Table = relation.ChildTable;
			this.relation = relation;
		}
		public DataViewEvaluatorContextDescriptor(DataTable table) {
			this.Table = table;
		}
		public override object GetPropertyValue(object source, EvaluatorProperty propertyPath) {
			if(source == null)
				return null;
			string[] path = propertyPath.PropertyPathTokenized;
			if(path.Length == 1) {
				DataColumn c = Table.Columns[path[0]];
				if(c == null)
					throw new InvalidPropertyPathException(string.Format(CultureInfo.InvariantCulture, FilteringExceptionsText.ExpressionEvaluatorInvalidPropertyPath, propertyPath));
				DataRow row = (DataRow)source;
				return row[c];
			}
			EvaluatorContext nestedContext = this.GetNestedContext(source, path[0]);
			if(nestedContext == null)
				return null;
			return nestedContext.GetPropertyValue(propertyPath.SubProperty);
		}
		static object noResult = new object();
		public override EvaluatorContext GetNestedContext(object source, string propertyPath) {
			DataRow row = (DataRow)source;
			DataViewEvaluatorContextDescriptor descriptor = (DataViewEvaluatorContextDescriptor)children[propertyPath];
			DataRelation r;
			if(descriptor == null) {
				r = row.Table.ChildRelations[propertyPath];
				descriptor = new DataViewEvaluatorContextDescriptor(r);
				descriptor.ContextCaching = contextCaching;
				children[propertyPath] = descriptor;
			} else
				r = descriptor.relation;
			if(contextCaching) {
				object child = descriptor.cache;
				if(child != null)
					return child == noResult ? null : (EvaluatorContext)child;
			}
			DataRow[] rows = row.GetChildRows(r);
			if(rows.Length > 1)
				throw new ArgumentException(Res.GetString(Res.InMemory_SingleRowExpected, propertyPath, rows.Length.ToString()));
			EvaluatorContext context = rows.Length == 0 ? null : new EvaluatorContext(descriptor, rows[0]);
			if(contextCaching)
				descriptor.cache = context == null ? noResult : context;
			return context;
		}
		public override IEnumerable GetCollectionContexts(object source, string collectionName) {
			if(string.IsNullOrEmpty(collectionName)) {
				return new CollectionContexts(this, (IEnumerable)source);
			} else {
				DataRelation r = Table.ChildRelations[collectionName];
				if(r == null)
					throw new ArgumentException(Res.GetString(Res.InMemory_NotACollection, collectionName));
				DataRow row = (DataRow)source;
				return new CollectionContexts(new DataViewEvaluatorContextDescriptor(r.ChildTable), row.GetChildRows(r));
			}
		}
		public override IEnumerable GetQueryContexts(object source, string queryTypeName, CriteriaOperator condition, int top) {
			throw new NotSupportedException();
		}
	}
	public class IsTopLevelAggregateChecker : IQueryCriteriaVisitor<bool> {
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
			return new IsTopLevelAggregateChecker().Process(op);
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
	public class KeyFinder : IClientCriteriaVisitor<object> {
		DataTable table;
		static readonly object key = new object();
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
			int count = theOperator.Operands.Count;
			ArrayList list = null;
			for(int i = 0; i < count; i++) {
				object val = Process((CriteriaOperator)theOperator.Operands[i]);
				ArrayList keys = val as ArrayList;
				if(keys != null) {
					if(list == null)
						list = keys;
					else
						list.AddRange(keys);
				} else {
					if(theOperator.OperatorType == GroupOperatorType.Or)
						return null;
				}
			}
			return list;
		}
		object ICriteriaVisitor<object>.Visit(InOperator theOperator) {
			object left = Process(theOperator.LeftOperand);
			if(left != key)
				return null;
			int count = theOperator.Operands.Count;
			ArrayList list = new ArrayList(count);
			for(int i = 0; i < count; i++) {
				object val = Process((CriteriaOperator)theOperator.Operands[i]);
				if(val != null)
					list.Add(val);
			}
			return list.Count > 0 ? list : null;
		}
		object ICriteriaVisitor<object>.Visit(UnaryOperator theOperator) {
			return null;
		}
		object ICriteriaVisitor<object>.Visit(BinaryOperator theOperator) {
			if(theOperator.OperatorType != BinaryOperatorType.Equal)
				return null;
			object left = Process(theOperator.LeftOperand);
			object rigth = Process(theOperator.RightOperand);
			if(left == key && rigth != null) {
				ArrayList list = new ArrayList(1);
				list.Add(rigth);
				return list;
			}
			if(rigth == key && left != null) {
				ArrayList list = new ArrayList(1);
				list.Add(left);
				return list;
			}
			return null;
		}
		object ICriteriaVisitor<object>.Visit(BetweenOperator theOperator) {
			return null;
		}
		public KeyFinder(DataTable table) {
			this.table = table;
		}
		public IList Find(CriteriaOperator criteria) {
			if(table.PrimaryKey == null || table.PrimaryKey.Length != 1)
				return null;
			return (IList)Process(criteria);
		}
		object IClientCriteriaVisitor<object>.Visit(OperandProperty theOperand) {
			if(table.PrimaryKey[0].ColumnName == theOperand.PropertyName)
				return key;
			return null;
		}
		object IClientCriteriaVisitor<object>.Visit(AggregateOperand theOperand) {
			return null;
		}
		object IClientCriteriaVisitor<object>.Visit(JoinOperand theOperand) {
			return null;
		}
	}
	public class QueryCriteriaReprocessor : IQueryCriteriaVisitor<CriteriaOperator> {
		readonly JoinNode root;
		TaggedParametersHolder identitiesByTag;
		NodeInfo[] localRoot = Array.Empty<NodeInfo>();
		readonly IDictionary NodesInfos;
		NodeInfo GetNodeInfo(string alias) {
			if(alias == null)
				alias = string.Empty;
			return (NodeInfo)NodesInfos[alias];
		}
		public QueryCriteriaReprocessor(DataSet dataSet, JoinNode root) : this(dataSet, root, new TaggedParametersHolder()) { }
		public QueryCriteriaReprocessor(DataSet dataSet, JoinNode root, TaggedParametersHolder identitiesByTag) {
			this.root = root;
			this.identitiesByTag = identitiesByTag;
			NodesInfos = NodeInfo.CreateNodesDictionary(dataSet, root);
		}
		CriteriaOperator ICriteriaVisitor<CriteriaOperator>.Visit(OperandValue theOperand) {
			return identitiesByTag.ConsolidateParameter(theOperand);
		}
		CriteriaOperator IQueryCriteriaVisitor<CriteriaOperator>.Visit(QueryOperand operand) {
			return new OperandProperty(GetName((QueryOperand)operand));
		}
		CriteriaOperator IQueryCriteriaVisitor<CriteriaOperator>.Visit(QuerySubQueryContainer container) {
			if(container.Node != null) {
				NodeInfo[] rememberedLocalRoot = this.localRoot;
				this.localRoot = GetNodeInfo(container.Node.Alias).RootRelationalPath;
				try {
					OperandProperty operandProperty = new OperandProperty(string.Join(".", NodeInfo.GetDiffPath(rememberedLocalRoot, localRoot)));
					CriteriaOperator criteria = Process(container.Node.Condition);
					if(container.AggregateType != Aggregate.Custom) {
						CriteriaOperator aggregateProperty = null;
						if(!ReferenceEquals(container.AggregateProperty, null))
							aggregateProperty = Process(container.AggregateProperty);
						return new AggregateOperand(operandProperty, aggregateProperty, container.AggregateType, criteria);
					} else {
						CriteriaOperatorCollection aggregateProps = new CriteriaOperatorCollection(container.CustomAggregateOperands.Count);
						foreach(CriteriaOperator op in container.CustomAggregateOperands) {
							CriteriaOperator aggregateProperty = null;
							if(!ReferenceEquals(op, null)) {
								aggregateProperty = Process(op);
							}
							aggregateProps.Add(aggregateProperty);
						}
						return new AggregateOperand(operandProperty, aggregateProps, container.CustomAggregateName, criteria);
					}
				} finally {
					this.localRoot = rememberedLocalRoot;
				}
			} else {
				OperandProperty operandProperty = null;
				CriteriaOperator criteria = null;
				if(container.AggregateType != Aggregate.Custom) {
					CriteriaOperator aggregateProperty = null;
					if(!ReferenceEquals(container.AggregateProperty, null)) {
						aggregateProperty = Process(container.AggregateProperty);
					}
					return new AggregateOperand(operandProperty, aggregateProperty, container.AggregateType, criteria);
				} else {
					CriteriaOperatorCollection aggregateProps = new CriteriaOperatorCollection(container.CustomAggregateOperands.Count);
					foreach(CriteriaOperator op in container.CustomAggregateOperands) {
						CriteriaOperator aggregateProperty = null;
						if(!ReferenceEquals(op, null)) {
							aggregateProperty = Process(op);
						}
						aggregateProps.Add(aggregateProperty);
					}
					return new AggregateOperand(operandProperty, aggregateProps, container.CustomAggregateName, criteria);
				}
			}
		}
		public CriteriaOperator Process(CriteriaOperator op) {
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
		public class NodeInfo {
			public readonly JoinNode Node;
			public readonly NodeInfo RelationParent;
			public readonly DataRelation Relation;
			NodeInfo[] _rrPath;
			public NodeInfo[] RootRelationalPath {
				get {
					if(_rrPath == null) {
						if(RelationParent == null) {
							_rrPath = Array.Empty<NodeInfo>();
						} else {
							NodeInfo[] parentPath = RelationParent.RootRelationalPath;
							_rrPath = new NodeInfo[parentPath.Length + 1];
							parentPath.CopyTo(_rrPath, 0);
							_rrPath[_rrPath.Length - 1] = this;
						}
					}
					return _rrPath;
				}
			}
			public NodeInfo(JoinNode node, NodeInfo relationParent, DataRelation relation) {
				this.Node = node;
				this.RelationParent = relationParent;
				this.Relation = relation;
			}
			static bool GetIsReferenceContainsColumnsPair(ReferenceElement[] res, DataColumn parentColumn, DataColumn childColumn) {
				int count = res.Length;
				for(int i = 0; i < count; i++) {
					ReferenceElement element = res[i];
					if(element.NodeColumn.ColumnName == parentColumn.ColumnName && element.SubNodeColumn.ColumnName == childColumn.ColumnName)
						return true;
				}
				return false;
			}
			static bool GetIsRelationJoinsSubset(DataRelation relation, ReferenceElement[] references) {
				int count = relation.ChildColumns.Length;
				for(int i = 0; i < count; ++i) {
					if(!GetIsReferenceContainsColumnsPair(references, relation.ParentColumns[i], relation.ChildColumns[i]))
						return false;
				}
				return true;
			}
			static DataRelation FindDataRelation(DataRelationCollection relations, JoinNode node, JoinNode subNode) {
				ReferenceElement[] references = ReferenceColumnsFinder.FindJoinReferences(subNode, node);
				if(references.Length == 0)
					return null;
				int count = relations.Count;
				for(int i = 0; i < count; i++) {
					DataRelation relation = relations[i];
					if(node.Table is DBProjection || subNode.Table is DBProjection)
						throw new NotSupportedException(); 
					if(node.Table.Name == relation.ParentTable.TableName
						&& subNode.Table.Name == relation.ChildTable.TableName) {
						if(GetIsRelationJoinsSubset(relation, references)) {
							return relation;
						}
					}
				}
				return null;
			}
			static void ProcessSubQueries(DataRelationCollection relations, IDictionary dictionary, JoinNodeCollection subQueries) {
				int count = subQueries.Count;
				for(int i = 0; i < count; i++)
					Process(relations, dictionary, subQueries[i]);
			}
			static void Process(DataRelationCollection relations, IDictionary dictionary, JoinNode node) {
				NodeInfo currentNode = null;
				if(dictionary.Count == 0) {
					currentNode = new NodeInfo(node, null, null);
				} else {
					foreach(NodeInfo ni in dictionary.Values) {
						DataRelation dr = FindDataRelation(relations, ni.Node, node);
						if(dr == null)
							continue;
						currentNode = new NodeInfo(node, ni, dr);
						break;
					}
					if(currentNode == null)
						throw new InvalidOperationException(Res.GetString(Res.InMemory_CannotFindParentRelationForNode, node.Alias));
				}
				string alias = node.Alias;
				if(alias == null)
					alias = string.Empty;
				dictionary.Add(alias, currentNode);
				ProcessSubQueries(relations, dictionary, node.SubNodes);
				ProcessSubQueries(relations, dictionary, SubQueriesFinder.FindSubQueries(node.Condition));
				SelectStatement ss = node as SelectStatement;
				if(ss != null) {
					ProcessSubQueries(relations, dictionary, SubQueriesFinder.FindSubQueries(ss.Operands));
					ProcessSubQueries(relations, dictionary, SubQueriesFinder.FindSubQueries(ss.SortProperties));
					ProcessSubQueries(relations, dictionary, SubQueriesFinder.FindSubQueries(ss.GroupProperties));
					HybridDictionary tempDict = new HybridDictionary();
					ProcessSubQueries(relations, tempDict, SubQueriesFinder.FindSubQueries(ss.GroupCondition));
					foreach(DictionaryEntry entry in tempDict)
						dictionary.Add(entry.Key, entry.Value);
				}
			}
			public static IDictionary CreateNodesDictionary(DataSet dataSet, JoinNode rootNode) {
				try {
					IDictionary result = new HybridDictionary();
					Process(dataSet.Relations, result, rootNode);
					return result;
				} catch(Exception e) {
					throw new SchemaCorrectionNeededException(e.ToString() + "\n" + rootNode.ToString(), e);
				}
			}
			public static string[] GetDiffPath(NodeInfo[] localRootPath, NodeInfo[] subject) {
				int diffIndex = 0;
				while(diffIndex < localRootPath.Length
					&& diffIndex < subject.Length
					&& ReferenceEquals(localRootPath[diffIndex], subject[diffIndex])
					) {
					++diffIndex;
				}
				List<string> resultList = new List<string>();
				for(int i = diffIndex; i < localRootPath.Length; ++i) {
					resultList.Add("^");
				}
				for(int i = diffIndex; i < subject.Length; ++i) {
					resultList.Add(subject[i].Relation.RelationName);
				}
				return resultList.ToArray();
			}
		}
		string GetName(QueryOperand operand) {
			NodeInfo ni = GetNodeInfo(operand.NodeAlias);
			string[] nodePath = NodeInfo.GetDiffPath(localRoot, ni.RootRelationalPath);
			string[] result = new string[nodePath.Length + 1];
			nodePath.CopyTo(result, 0);
			result[result.Length - 1] = operand.ColumnName;
			return string.Join(".", result);
		}
	}
	public class ReferenceElement {
		public readonly QueryOperand NodeColumn;
		public readonly QueryOperand SubNodeColumn;
		public ReferenceElement(QueryOperand nodeColumn, QueryOperand subNodeColumn) {
			this.NodeColumn = nodeColumn;
			this.SubNodeColumn = subNodeColumn;
		}
	}
	public class ReferenceColumnsFinder : IQueryCriteriaVisitor {
		readonly string SubNodeAlias;
		readonly string ParentNodeAlias;
		readonly List<ReferenceElement> References = new List<ReferenceElement>();
		ReferenceColumnsFinder(string subNodeAlias, string parentNodeAlias) {
			this.SubNodeAlias = subNodeAlias;
			this.ParentNodeAlias = parentNodeAlias;
		}
		void ICriteriaVisitor.Visit(BetweenOperator theOperator) {
		}
		void ProcessBinary(BinaryOperator theOperator) {
			if(theOperator.OperatorType != BinaryOperatorType.Equal)
				return;
			QueryOperand leftOperand = theOperator.LeftOperand as QueryOperand;
			QueryOperand rightOperand = theOperator.RightOperand as QueryOperand;
			if(ReferenceEquals(leftOperand, null) || ReferenceEquals(rightOperand, null))
				return;
			if(SubNodeAlias == leftOperand.NodeAlias && ParentNodeAlias == rightOperand.NodeAlias) {
				References.Add(new ReferenceElement(rightOperand, leftOperand));
			} else if(ParentNodeAlias == leftOperand.NodeAlias && SubNodeAlias == rightOperand.NodeAlias) {
				References.Add(new ReferenceElement(leftOperand, rightOperand));
			}
		}
		void ICriteriaVisitor.Visit(BinaryOperator theOperator) {
			ProcessBinary(theOperator);
		}
		void ICriteriaVisitor.Visit(UnaryOperator theOperator) {
		}
		void ICriteriaVisitor.Visit(InOperator theOperator) {
		}
		void ICriteriaVisitor.Visit(GroupOperator theOperator) {
			if(theOperator.OperatorType != GroupOperatorType.And)
				return;
			int count = theOperator.Operands.Count;
			for(int i = 0; i < count; i++)
				SearchAt((CriteriaOperator)theOperator.Operands[i]);
		}
		void ICriteriaVisitor.Visit(OperandValue theOperand) {
		}
		void ICriteriaVisitor.Visit(FunctionOperator theOperator) {
		}
		void IQueryCriteriaVisitor.Visit(QueryOperand theOperand) {
		}
		void IQueryCriteriaVisitor.Visit(QuerySubQueryContainer theOperand) {
		}
		void SearchAt(CriteriaOperator criteria) {
			if(ReferenceEquals(criteria, null))
				return;
			criteria.Accept(this);
		}
		public static ReferenceElement[] FindJoinReferences(JoinNode subNode, JoinNode parentNode) {
			ReferenceColumnsFinder finder = new ReferenceColumnsFinder(subNode.Alias, parentNode.Alias);
			finder.SearchAt(subNode.Condition);
			return finder.References.ToArray();
		}
	}
	public class DataSetStoreHelpers {
		DataSetStoreHelpers() { }
		static string GetRelationName(DataTable table, DBForeignKey fk) {
			StringBuilder name = new StringBuilder("FK_");
			name.Append(Escape(table.TableName));
			int count = fk.Columns.Count;
			for(int i = 0; i < count; i++) {
				name.Append(fk.Columns[i]);
			}
			count = fk.PrimaryKeyTableKeyColumns.Count;
			for(int i = 0; i < count; i++) {
				name.Append(fk.PrimaryKeyTableKeyColumns[i]);
			}
			name.Append(Escape(fk.PrimaryKeyTable));
			return name.ToString();
		}
		static string Escape(string name) {
			return name.Replace('.', '_');
		}
		static DataColumn[] GetColumns(DataTable table, StringCollection columns) {
			DataColumn[] dataColumns = new DataColumn[columns.Count];
			int count = columns.Count;
			for(int i = 0; i < count; i++) {
				dataColumns[i] = table.Columns[columns[i]];
			}
			return dataColumns;
		}
		static string GetIndexName(DataTable table, DBIndex index) {
			StringBuilder name = new StringBuilder(table.TableName);
			int count = index.Columns.Count;
			for(int i = 0; i < count; i++) {
				name.Append(index.Columns[i]);
			}
			return name.ToString();
		}
		static bool IsEqual(DBIndex index, UniqueConstraint constraint) {
			int count = constraint.Columns.Length;
			if(index.Columns.Count != count)
				return false;
			for(int i = 0; i < count; ++i) {
				if(constraint.Columns[i].ColumnName != index.Columns[i])
					return false;
			}
			return true;
		}
		static bool IsExists(DataTable table, DBIndex index) {
			int count = table.Constraints.Count;
			for(int i = 0; i < count; i++) {
				if(table.Constraints[i] is UniqueConstraint && IsEqual(index, (UniqueConstraint)table.Constraints[i]))
					return true;
			}
			return false;
		}
		static bool IsExists(DataTable table, DBPrimaryKey index) {
			if(table.PrimaryKey == null)
				return false;
			int count = table.PrimaryKey.Length;
			if(index.Columns.Count != count)
				return false;
			for(int i = 0; i < count; ++i) {
				if(table.PrimaryKey[i].ColumnName != index.Columns[i])
					return false;
			}
			return true;
		}
		static bool GetIsReferenceContainsColumnsPair(ReferenceElement[] res, DataColumn parentColumn, DataColumn childColumn) {
			int count = res.Length;
			return false;
		}
		static bool IsEqual(DBForeignKey fk, ForeignKeyConstraint constraint) {
			if(fk.PrimaryKeyTable != constraint.RelatedTable.TableName)
				return false;
			int count = constraint.Columns.Length;
			if(fk.Columns.Count != count)
				return false;
			for(int i = 0; i < count; ++i) {
				if(constraint.Columns[i].ColumnName != fk.Columns[i] || fk.PrimaryKeyTableKeyColumns[i] != constraint.RelatedColumns[i].ColumnName)
					return false;
			}
			return true;
		}
		static bool IsExists(DataTable table, DBForeignKey fk) {
			int count = table.Constraints.Count;
			for(int i = 0; i < count; i++) {
				if(table.Constraints[i] is ForeignKeyConstraint && IsEqual(fk, (ForeignKeyConstraint)table.Constraints[i]))
					return true;
			}
			return false;
		}
		static bool IsExists(DataTable table, DBColumn column) {
			return table.Columns[column.Name] != null;
		}
		public static DataTable QueryTable(DataSet dataSet, string tableName) {
			return dataSet.Tables[tableName];
		}
		public static DataTable GetTable(DataSet dataSet, string tableName) {
			DataTable result = QueryTable(dataSet, tableName);
			if(result == null)
				throw new SchemaCorrectionNeededException(tableName);
			return result;
		}
		static void Create(DataTable table, DBIndex index) {
			int count = index.Columns.Count;
			DataColumn[] columns = new DataColumn[count];
			for(int i = 0; i < count; i++) {
				columns[i] = table.Columns[index.Columns[i]];
			}
			table.Constraints.Add(GetIndexName(table, index), columns, false);
		}
		static void Create(DataTable table, DBPrimaryKey index) {
			int count = index.Columns.Count;
			DataColumn[] columns = new DataColumn[count];
			for(int i = 0; i < count; i++) {
				columns[i] = table.Columns[index.Columns[i]];
			}
			CreateIfNotExists(table, (DBIndex)index);
			table.PrimaryKey = columns;
		}
		static void Create(DataTable table, DBForeignKey fk) {
			DataTable pkTable = table.DataSet.Tables[fk.PrimaryKeyTable];
			if(pkTable == null)
				throw new SchemaCorrectionNeededException(Res.GetString(Res.InMemoryFull_TableNotFound, fk.PrimaryKeyTable));
			DataRelation relation = table.ParentRelations.Add(GetRelationName(table, fk), GetColumns(pkTable, fk.PrimaryKeyTableKeyColumns), GetColumns(table, fk.Columns), true);
			relation.ChildKeyConstraint.DeleteRule = Rule.None;
			table.ChildRelations.Add(GetRelationName(table, fk) + "!", GetColumns(table, fk.Columns), GetColumns(pkTable, fk.PrimaryKeyTableKeyColumns), false);
		}
		static void Create(DataTable table, DBColumn column) {
			DataColumn dataColumn = table.Columns.Add(column.Name, DBColumn.GetType(column.ColumnType));
			dataColumn.AutoIncrement = column.IsIdentity;
			if(column.ColumnType == DBColumnType.String && column.Size != 0)
				dataColumn.MaxLength = column.Size;
		}
		static DataTable Create(DataSet dataSet, DBTable table) {
			return dataSet.Tables.Add(table.Name);
		}
		public static void CreateIfNotExists(DataTable table, DBForeignKey dbObj) {
			if(!IsExists(table, dbObj))
				Create(table, dbObj);
		}
		public static void CreateIfNotExists(DataTable table, DBIndex dbObj) {
			if(!IsExists(table, dbObj))
				Create(table, dbObj);
		}
		public static void CreateIfNotExists(DataTable table, DBPrimaryKey dbObj) {
			if(!IsExists(table, dbObj))
				Create(table, dbObj);
		}
		public static void CreateIfNotExists(DataTable table, DBColumn dbObj) {
			if(!IsExists(table, dbObj))
				Create(table, dbObj);
		}
		public static DataTable CreateIfNotExists(DataSet dataSet, DBTable table) {
			DataTable result = QueryTable(dataSet, table.Name);
			if(result == null)
				result = Create(dataSet, table);
			return result;
		}
		static void UpdateRow(DataRow row, object[] values, DataColumn[] columns) {
			int count = columns.Length;
			row.BeginEdit();
			try {
				for(int i = 0; i < count; i++) {
					row[columns[i]] = values[i];
				}
				row.EndEdit();
			} catch {
				row.CancelEdit();
				throw;
			}
		}
		static void RandomSort(IList rows) {
			var r = Data.Utils.NonCryptographicRandom.Default;
			for(int i = 0; i < rows.Count - 1; i++) {
				int pos = r.Next(i, rows.Count);
				object temp = rows[pos];
				rows[pos] = rows[i];
				rows[i] = temp;
			}
		}
		static List<DataRow> FindRows(DataViewEvaluatorContextDescriptor descriptor, CriteriaOperator criteria, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions, int count) {
			return FindRows(descriptor, criteria, caseSensitive, customFunctions, null, count);
		}
		static List<DataRow> FindRows(DataViewEvaluatorContextDescriptor descriptor, CriteriaOperator criteria, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions, ICollection<ICustomAggregate> customAggregates, int count) {
			try {
				List<DataRow> result;
				if(ReferenceEquals(criteria, null)) {
					result = new List<DataRow>(descriptor.Table.Rows.Count);
					foreach(DataRow row in descriptor.Table.Rows)
						result.Add(row);
					RandomSort(result);
					return result;
				}
				ExpressionEvaluator eval;
				eval = new ExpressionEvaluator(descriptor, criteria, caseSensitive, customFunctions, customAggregates);
				result = new List<DataRow>();
				IList keys = new KeyFinder(descriptor.Table).Find(criteria);
				if(keys == null) {
					foreach(DataRow row in descriptor.Table.Rows) {
						descriptor.ContextCaching = true;
						try {
							if(row.RowState != DataRowState.Deleted && eval.Fit(row)) {
								result.Add(row);
								count--;
								if(count == 0)
									break;
							}
						} finally {
							descriptor.ContextCaching = false;
						}
					}
				} else {
					if(keys.Count == 1) {
						DataRow row = descriptor.Table.Rows.Find(keys[0]);
						if(row != null && row.RowState != DataRowState.Deleted && eval.Fit(row))
							result.Add(row);
					} else {
						Hashtable list = new Hashtable();
						foreach(object key in keys) {
							DataRow row = descriptor.Table.Rows.Find(key);
							if(row != null && row.RowState != DataRowState.Deleted && !list.Contains(row) && eval.Fit(row)) {
								result.Add(row);
								list.Add(row, null);
								count--;
								if(count == 0)
									break;
							}
						}
					}
				}
				RandomSort(result);
				return result;
			} catch(InvalidPropertyPathException e) {
				throw new SchemaCorrectionNeededException(e);
			}
		}
		static object[] GetResultRow(object row, ExpressionEvaluator[] evaluators) {
			int count = evaluators.Length;
			object[] result = new object[count];
			for(int i = 0; i < count; i++) {
				object value = evaluators[i].Evaluate(row);
				if(value == DBNull.Value)
					value = null;
				result[i] = value;
			}
			return result;
		}
		public static SelectStatementResult DoGetData(DataViewEvaluatorContextDescriptor descriptor, CriteriaOperator condition, ExpressionEvaluator[] dataEvaluators, SortingComparer sortingComparer, int skipRecords, int topRecords, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunction) {
			return DoGetData(descriptor, condition, dataEvaluators, sortingComparer, skipRecords, topRecords, caseSensitive, customFunction, null);
		}
		public static SelectStatementResult DoGetData(DataViewEvaluatorContextDescriptor descriptor, CriteriaOperator condition, ExpressionEvaluator[] dataEvaluators, SortingComparer sortingComparer, int skipRecords, int topRecords, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunction, ICollection<ICustomAggregate> customAggregates) {
			int findTopRows = (sortingComparer != null || topRecords == 0) ? 0 : topRecords + skipRecords;
			List<DataRow> rows = DataSetStoreHelpers.FindRows(descriptor, condition, caseSensitive, customFunction, customAggregates, findTopRows);
			if(sortingComparer != null)
				rows.Sort(sortingComparer);
			if(skipRecords != 0) {
				if(rows.Count <= skipRecords)
					rows.Clear();
				else
					rows.RemoveRange(0, skipRecords);
			}
			if(topRecords != 0 && topRecords < rows.Count)
				rows.RemoveRange(topRecords, rows.Count - topRecords);
			int count = rows.Count;
			ArrayList result = new ArrayList(count);
			for(int i = 0; i < count; i++) {
				descriptor.ContextCaching = true;
				try {
					result.Add(GetResultRow(rows[i], dataEvaluators));
				} finally {
					descriptor.ContextCaching = false;
				}
			}
			return new SelectStatementResult(result);
		}
		public static int DoInsertRecord(DataTable table, QueryParameterCollection parameters, CriteriaOperatorCollection operands, ParameterValue identityParameter, TaggedParametersHolder identitiesByTag, List<DataRow> affectedRows) {
			object[] values = new object[table.Columns.Count];
			int count = parameters.Count;
			identitiesByTag.ConsolidateIdentity(identityParameter);
			for(int i = 0; i < count; i++) {
				OperandValue parameter = identitiesByTag.ConsolidateParameter(parameters[i]);
				QueryOperand operand = (QueryOperand)operands[i];
				object value = parameter.Value;
				if(value == null)
					value = DBNull.Value;
				DataColumn dColumn = table.Columns[operand.ColumnName];
				if(dColumn == null)
					throw new SchemaCorrectionNeededException(table.TableName + "." + operand.ColumnName);
				values[dColumn.Ordinal] = FixTimeSpan(value, dColumn);
			}
			DataRow row = table.Rows.Add(values);
			affectedRows.Add(row);
			if(!ReferenceEquals(identityParameter, null)) {
				int colCount = table.Columns.Count;
				for(int i = 0; i < colCount; i++) {
					DataColumn column = table.Columns[i];
					if(column.AutoIncrement) {
						identityParameter.Value = row[column];
						break;
					}
				}
			}
			return 1;
		}
		static object FixTimeSpan(object value, DataColumn dColumn) {
			if(dColumn.DataType == typeof(TimeSpan) && (value is double)) {
				return TimeSpan.FromSeconds(((double)value));
			}
			return value;
		}
		public static int DoUpdateRecord(DataViewEvaluatorContextDescriptor descriptor, QueryCriteriaReprocessor processor, QueryParameterCollection parameters, CriteriaOperatorCollection operands, TaggedParametersHolder identitiesByTag, CriteriaOperator condition, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunction, List<DataRow> affectedRows) {
			return DoUpdateRecord(descriptor, processor, parameters, operands, identitiesByTag, condition, caseSensitive, customFunction, null, affectedRows);
		}
		public static int DoUpdateRecord(DataViewEvaluatorContextDescriptor descriptor, QueryCriteriaReprocessor processor, QueryParameterCollection parameters, CriteriaOperatorCollection operands, TaggedParametersHolder identitiesByTag, CriteriaOperator condition, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunction, ICollection<ICustomAggregate> customAggregates, List<DataRow> affectedRows) {
			int count = operands.Count;
			object[] values = new object[count];
			DataColumn[] columns = new DataColumn[count];
			for(int i = 0; i < count; i++) {
				OperandValue parameter = identitiesByTag.ConsolidateParameter(parameters[i]);
				QueryOperand operand = (QueryOperand)operands[i];
				columns[i] = descriptor.Table.Columns[operand.ColumnName];
				if(columns[i] == null)
					throw new SchemaCorrectionNeededException(descriptor.Table.TableName + "." + operand.ColumnName);
				object value = parameter.Value;
				if(value == null)
					value = DBNull.Value;
				values[i] = FixTimeSpan(value, columns[i]);
			}
			List<DataRow> rows = FindRows(descriptor, processor.Process(condition), caseSensitive, customFunction, customAggregates, 0);
			int rowCount = rows.Count;
			for(int i = 0; i < rowCount; i++) {
				affectedRows.Add(rows[i]);
				DataSetStoreHelpers.UpdateRow(rows[i], values, columns);
			}
			return rowCount;
		}
		public static int DoDeleteRecord(DataViewEvaluatorContextDescriptor descriptor, CriteriaOperator condition, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions, List<DataRow> affectedRows) {
			return DoDeleteRecord(descriptor, condition, caseSensitive, customFunctions, null, affectedRows);
		}
		public static int DoDeleteRecord(DataViewEvaluatorContextDescriptor descriptor, CriteriaOperator condition, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions, ICollection<ICustomAggregate> customAggregates, List<DataRow> affectedRows) {
			List<DataRow> rows = FindRows(descriptor, condition, caseSensitive, customFunctions, customAggregates, 0);
			int count = rows.Count;
			for(int i = 0; i < count; i++) {
				affectedRows.Add(rows[i]);
				rows[i].Delete();
			}
			return count;
		}
		public static SelectStatementResult DoGetGroupedData(DataViewEvaluatorContextDescriptor descriptor, CriteriaOperator condition, ExpressionEvaluator[] groupEvaluators, ExpressionEvaluator havingEvaluator, SortingComparer sortingComparer, int skipRecords, int topRecords, ExpressionEvaluator[] dataEvaluators, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions) {
			return DoGetGroupedData(descriptor, condition, groupEvaluators, havingEvaluator, sortingComparer, skipRecords, topRecords, dataEvaluators, caseSensitive, customFunctions, null);
		}
		public static SelectStatementResult DoGetGroupedData(DataViewEvaluatorContextDescriptor descriptor, CriteriaOperator condition, ExpressionEvaluator[] groupEvaluators, ExpressionEvaluator havingEvaluator, SortingComparer sortingComparer, int skipRecords, int topRecords, ExpressionEvaluator[] dataEvaluators, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions, ICollection<ICustomAggregate> customAggregates) {
			List<DataRow> rows = FindRows(descriptor, condition, caseSensitive, customFunctions, customAggregates, 0);
			Dictionary<InMemoryGroup, List<DataRow>> groupingResult = new Dictionary<InMemoryGroup, List<DataRow>>();
			if(groupEvaluators.Length == 0) {
				groupingResult.Add(new InMemoryGroup(Array.Empty<object>()), new List<DataRow>());
			}
			int count = rows.Count;
			for(int i = 0; i < count; i++) {
				DataRow row = rows[i];
				InMemoryGroup group = new InMemoryGroup(GetResultRow(row, groupEvaluators));
				List<DataRow> groupList;
				if(!groupingResult.TryGetValue(group, out groupList)) {
					groupList = new List<DataRow>();
					groupingResult.Add(group, groupList);
				}
				groupList.Add(row);
			}
			ArrayList havingResult = new ArrayList();
			foreach(List<DataRow> group in groupingResult.Values) {
				if(havingEvaluator.Fit(group))
					havingResult.Add(group);
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
			count = havingResult.Count;
			ArrayList result = new ArrayList(count);
			for(int i = 0; i < count; i++) {
				result.Add(GetResultRow(havingResult[i], dataEvaluators));
			}
			return new SelectStatementResult(result);
		}
	}
	public class SortingComparer : IComparer<DataRow>, IComparer {
		ExpressionEvaluator[] evaluators;
		QuerySortingCollection operands;
		public SortingComparer(ExpressionEvaluator[] sortingEvaluators, QuerySortingCollection operands) {
			this.evaluators = sortingEvaluators;
			this.operands = operands;
		}
		int IComparer<DataRow>.Compare(DataRow x, DataRow y) {
			return ((IComparer)this).Compare(x, y);
		}
		int IComparer.Compare(object x, object y) {
			int count = evaluators.Length;
			for(int i = 0; i < count; i++) {
				ExpressionEvaluator evaluator = evaluators[i];
				int res = Comparer.Default.Compare(evaluator.Evaluate(x), evaluator.Evaluate(y));
				if(res != 0)
					return operands[i].Direction == SortingDirection.Ascending ? res : -res;
			}
			return 0;
		}
	}
	public class InMemoryGroup {
		public readonly object[] GroupValues;
		public InMemoryGroup(object[] groupValues) {
			this.GroupValues = groupValues;
		}
		public override bool Equals(object obj) {
			InMemoryGroup another = obj as InMemoryGroup;
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
	public class InMemoryGroupDescriptor : EvaluatorContextDescriptor {
		public readonly EvaluatorContextDescriptor InnerDescriptor;
		public InMemoryGroupDescriptor(EvaluatorContextDescriptor innerDescriptor) {
			this.InnerDescriptor = innerDescriptor;
		}
		object GetInnerSource(object outerSource) {
			if(outerSource == null)
				return null;
			IList list = (IList)outerSource;
			if(list.Count == 0)
				return null;
			return list[0];
		}
		public override object GetPropertyValue(object source, EvaluatorProperty propertyPath) {
			object innerSource = GetInnerSource(source);
			if(innerSource == null)
				return null;
			return InnerDescriptor.GetPropertyValue(innerSource, propertyPath);
		}
		public override EvaluatorContext GetNestedContext(object source, string propertyPath) {
			object innerSource = GetInnerSource(source);
			if(innerSource == null)
				return null;
			return InnerDescriptor.GetNestedContext(innerSource, propertyPath);
		}
		public override IEnumerable GetCollectionContexts(object source, string collectionName) {
			if(!string.IsNullOrEmpty(collectionName))
				throw new InvalidOperationException(Res.GetString(Res.InMemory_MalformedAggregate));
			return new CollectionContexts(InnerDescriptor, (IList)source);
		}
		public override IEnumerable GetQueryContexts(object source, string queryTypeName, CriteriaOperator condition, int top) {
			throw new NotSupportedException();
		}
		public override bool IsTopLevelCollectionSource {
			get {
				return true;
			}
		}
	}
	public class OfflineDataStore : DataSetDataStore {
		public readonly DataSet OfflineData;
		public OfflineDataStore(DataSet offlineData)
			: base(offlineData.Clone(), AutoCreateOption.None) {
			this.Data.AcceptChanges();
			this.OfflineData = offlineData;
		}
		protected override void DoCommit(ICollection<DataRow> rowsAffected) {
			DataSet diff = this.Data.GetChanges();
			this.OfflineData.Merge(diff, true, MissingSchemaAction.Error);
			base.DoCommit(rowsAffected);
		}
	}
}
namespace DevExpress.Xpo.DB {
	using DevExpress.Xpo.DB.Helpers;
	using System.IO;
	using System.Collections.Generic;
	using DevExpress.Xpo.Helpers;
	using DevExpress.Data.Helpers;
	using System.Threading.Tasks;
	using System.Threading;
	public class DataSetDataStore : DataStoreBase, IDataStoreSchemaExplorer, IDataStoreSchemaExplorerSp {
		public const string XpoProviderTypeString = "XmlDataSet";
		public static string GetConnectionString(string path) {
			return String.Format("{0}={1};data source={2};", DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString, path);
		}
		public static string GetConnectionString(string path, bool readOnly) {
			return String.Format("{0}={1};data source={2};read only={3}", DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString, path, readOnly);
		}
		public static IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			ConnectionStringParser parser = new ConnectionStringParser(connectionString);
			string path = parser.GetPartByName("data source");
			string readOnly = parser.GetPartByName("read only");
			bool isReadOnly = readOnly == "1" || readOnly.ToLower() == "true";
			try {
				XmlFileDataSetStore result = new XmlFileDataSetStore(path, autoCreateOption, isReadOnly);
				objectsToDisposeOnDisconnect = new IDisposable[] { result };
				return result;
			} catch(Exception e) {
				throw new UnableToOpenDatabaseException(connectionString, (e is UnableToOpenDatabaseException) ? e.InnerException : e);
			}
		}
		static DataSetDataStore() {
			RegisterDataStoreProvider(XpoProviderTypeString, new DataStoreCreationFromStringDelegate(CreateProviderFromString));
			RegisterFactory(new DataSetProviderFactory());
		}
		public static void Register() { }
		bool caseSensitive;
		DataSet data;
		public bool CanCreateSchema { get { return AutoCreateOption == AutoCreateOption.SchemaOnly || AutoCreateOption == AutoCreateOption.DatabaseAndSchema; } }
		public DataSet Data {
			get { return data; }
		}
		[Obsolete("SyncRoot is obsolette, use LockHelper.Lock() or LockHelper.LockAsync() instead.")]
		public override object SyncRoot { get { return this.Data; } }
		public DataSetDataStore(DataSet data, AutoCreateOption autoCreateOption) : this(data, autoCreateOption, true) { }
		public DataSetDataStore(DataSet data, AutoCreateOption autoCreateOption, bool caseSensitive)
			: base(autoCreateOption) {
			if(data == null)
				throw new ArgumentNullException(nameof(data));
			if(data.HasChanges())
				throw new ArgumentException(Res.GetString(Res.InMemory_DataSetUncommitted), nameof(data));
			this.data = data;
			this.caseSensitive = caseSensitive;
		}
		protected override UpdateSchemaResult ProcessUpdateSchema(bool skipIfFirstTableNotExists, params DBTable[] tables) {
			if(skipIfFirstTableNotExists && tables.Length > 0) {
				IEnumerator te = tables.GetEnumerator();
				te.MoveNext();
				if(DataSetStoreHelpers.QueryTable(Data, ((DBTable)te.Current).Name) == null)
					return UpdateSchemaResult.FirstTableNotExists;
			}
			if(CanCreateSchema) {
				foreach(DBTable table in tables) {
					DataTable newTable = DataSetStoreHelpers.CreateIfNotExists(Data, table);
					foreach(DBColumn column in table.Columns) {
						DataSetStoreHelpers.CreateIfNotExists(newTable, column);
					}
					if(table.PrimaryKey != null)
						DataSetStoreHelpers.CreateIfNotExists(newTable, table.PrimaryKey);
					foreach(DBIndex index in table.Indexes) {
						if(!index.IsUnique)
							continue;
						DataSetStoreHelpers.CreateIfNotExists(newTable, index);
					}
				}
				foreach(DBTable table in tables) {
					DataTable newTable = DataSetStoreHelpers.GetTable(Data, table.Name);
					foreach(DBForeignKey fk in table.ForeignKeys) {
						DataSetStoreHelpers.CreateIfNotExists(newTable, fk);
					}
				}
			}
			return UpdateSchemaResult.SchemaExists;
		}
		protected override Task<UpdateSchemaResult> ProcessUpdateSchemaAsync(AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken, bool doNotCreateIfFirstTableNotExist, DBTable[] tables) {
			return Task.FromResult(ProcessUpdateSchema(doNotCreateIfFirstTableNotExist, tables));
		}
		readonly CustomFunctionCollection customFunctionCollection = new CustomFunctionCollection();
		readonly CustomAggregateCollection customAggregateCollection = new CustomAggregateCollection();
		public void RegisterCustomFunctionOperators(ICollection<ICustomFunctionOperator> customFunctions) {
			foreach (ICustomFunctionOperator function in customFunctions) {
				RegisterCustomFunctionOperator(function);
			}
		}
		public void RegisterCustomFunctionOperator(ICustomFunctionOperator customFunction) {
			if (customFunction == null) throw new ArgumentNullException(nameof(customFunction));
			customFunctionCollection.Add(customFunction);
		}
		public void RegisterCustomAggregates(ICollection<ICustomAggregate> customAggregates) {
			foreach(ICustomAggregate agg in customAggregates) {
				RegisterCustomAggregate(agg);
			}
		}
		public void RegisterCustomAggregate(ICustomAggregate customAggregate) {
			if(customAggregate == null) throw new ArgumentNullException(nameof(customAggregate));
			customAggregateCollection.Add(customAggregate);
		}
		static ExpressionEvaluator PrepareDataEvaluator(QueryCriteriaReprocessor processor, CriteriaOperator operand, EvaluatorContextDescriptor descriptor, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions, ICollection<ICustomAggregate> customAggregates) {
			return new ExpressionEvaluator(descriptor, processor.Process(operand), caseSensitive, customFunctions, customAggregates);
		}
		static ExpressionEvaluator[] PrepareDataEvaluators(QueryCriteriaReprocessor processor, CriteriaOperatorCollection operands, EvaluatorContextDescriptor descriptor, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions, ICollection<ICustomAggregate> customAggregates) {
			int count = operands.Count;
			ExpressionEvaluator[] evaluators = new ExpressionEvaluator[count];
			for(int i = 0; i < count; i++) {
				evaluators[i] = PrepareDataEvaluator(processor, operands[i], descriptor, caseSensitive, customFunctions, customAggregates);
			}
			return evaluators;
		}
		static SortingComparer PrepareSortingComparer(QueryCriteriaReprocessor processor, QuerySortingCollection sortProperties, EvaluatorContextDescriptor descriptor, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions, ICollection<ICustomAggregate> customAggregates) {
			if(sortProperties.Count == 0)
				return null;
			int count = sortProperties.Count;
			ExpressionEvaluator[] sortingEvaluators = new ExpressionEvaluator[count];
			for(int i = 0; i < count; i++) {
				SortingColumn sortingColumn = sortProperties[i];
				sortingEvaluators[i] = PrepareDataEvaluator(processor, sortingColumn.Property, descriptor, caseSensitive, customFunctions, customAggregates);
			}
			return new SortingComparer(sortingEvaluators, sortProperties);
		}
		protected SelectStatementResult GetDataNormal(SelectStatement root) {
			if(root.Table is DBProjection)
				throw new NotSupportedException(); 
			DataTable table = DataSetStoreHelpers.GetTable(Data, root.Table.Name);
			QueryCriteriaReprocessor processor = new QueryCriteriaReprocessor(Data, root);
			CriteriaOperator condition = processor.Process(root.Condition);
			DataViewEvaluatorContextDescriptor descriptor = GetDescriptor(table);
			ExpressionEvaluator[] dataEvaluators = PrepareDataEvaluators(processor, root.Operands, descriptor, caseSensitive, customFunctionCollection, customAggregateCollection);
			SortingComparer sortingComparer = PrepareSortingComparer(processor, root.SortProperties, descriptor, caseSensitive, customFunctionCollection, customAggregateCollection);
			return DataSetStoreHelpers.DoGetData(descriptor, condition, dataEvaluators, sortingComparer, root.SkipSelectedRecords, root.TopSelectedRecords, caseSensitive, customFunctionCollection, customAggregateCollection);
		}
		protected SelectStatementResult GetDataGrouped(SelectStatement root) {
			if(root.Table is DBProjection)
				throw new NotSupportedException(); 
			DataTable table = DataSetStoreHelpers.GetTable(Data, root.Table.Name);
			QueryCriteriaReprocessor processor = new QueryCriteriaReprocessor(Data, root);
			CriteriaOperator condition = processor.Process(root.Condition);
			DataViewEvaluatorContextDescriptor descriptor = GetDescriptor(table);
			ExpressionEvaluator[] groupEvaluators = PrepareDataEvaluators(processor, root.GroupProperties, descriptor, caseSensitive, customFunctionCollection, customAggregateCollection);
			EvaluatorContextDescriptor groupedDataDescriptor = new InMemoryGroupDescriptor(descriptor);
			ExpressionEvaluator[] dataEvaluators = PrepareDataEvaluators(processor, root.Operands, groupedDataDescriptor, caseSensitive, customFunctionCollection, customAggregateCollection);
			CriteriaOperatorCollection havingCollection = new CriteriaOperatorCollection();
			ExpressionEvaluator havingEvaluator = PrepareDataEvaluator(processor, root.GroupCondition, groupedDataDescriptor, caseSensitive, customFunctionCollection, customAggregateCollection);
			SortingComparer sortingComparer = PrepareSortingComparer(processor, root.SortProperties, groupedDataDescriptor, caseSensitive, customFunctionCollection, customAggregateCollection);
			return DataSetStoreHelpers.DoGetGroupedData(descriptor, condition, groupEvaluators, havingEvaluator, sortingComparer, root.SkipSelectedRecords, root.TopSelectedRecords, dataEvaluators, caseSensitive, customFunctionCollection, customAggregateCollection);
		}
		protected override SelectStatementResult ProcessSelectData(SelectStatement selects) {
			try {
				if(IsTopLevelAggregateChecker.IsGrouped(selects)) {
					return GetDataGrouped(selects);
				} else {
					return GetDataNormal(selects);
				}
			} catch(InvalidPropertyPathException e) {
				throw new SchemaCorrectionNeededException(e);
			}
		}
		protected override Task<SelectStatementResult> ProcessSelectDataAsync(SelectStatement selects, AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken) {
			cancellationToken.ThrowIfCancellationRequested();
			return Task.FromResult(ProcessSelectData(selects));
		}
		Hashtable descriptors = new Hashtable();
		DataViewEvaluatorContextDescriptor GetDescriptor(DataTable table) {
			DataViewEvaluatorContextDescriptor descriptor = (DataViewEvaluatorContextDescriptor)descriptors[table];
			if(descriptor == null) {
				descriptor = new DataViewEvaluatorContextDescriptor(table);
				descriptors[table] = descriptor;
			}
			return descriptor;
		}
		protected override ModificationResult ProcessModifyData(params ModificationStatement[] dmlStatements) {
			List<DataRow> affectedRows = new List<DataRow>();
			try {
				TaggedParametersHolder identitiesByTag = new TaggedParametersHolder();
				List<ParameterValue> result = new List<ParameterValue>();
				int count = dmlStatements.Length;
				for(int i = 0; i < count; i++) {
					ModificationStatement root = dmlStatements[i];
					int recordsUpdated;
					try {
						if(root.Table is DBProjection)
							throw new NotSupportedException(); 
						DataTable table = DataSetStoreHelpers.GetTable(Data, root.Table.Name);
						if(root is InsertStatement) {
							ParameterValue identityParameter = ((InsertStatement)root).IdentityParameter;
							recordsUpdated = DataSetStoreHelpers.DoInsertRecord(table, ((InsertStatement)root).Parameters, root.Operands, identityParameter, identitiesByTag, affectedRows);
							if(!ReferenceEquals(identityParameter, null))
								result.Add(identityParameter);
						} else if(root is UpdateStatement) {
							recordsUpdated = DataSetStoreHelpers.DoUpdateRecord(GetDescriptor(table), new QueryCriteriaReprocessor(Data, root, identitiesByTag), ((UpdateStatement)root).Parameters, root.Operands, identitiesByTag, root.Condition, caseSensitive, customFunctionCollection, customAggregateCollection, affectedRows);
						} else if(root is DeleteStatement) {
							recordsUpdated = DataSetStoreHelpers.DoDeleteRecord(GetDescriptor(table), new QueryCriteriaReprocessor(Data, root, identitiesByTag).Process(root.Condition), caseSensitive, customFunctionCollection, customAggregateCollection, affectedRows);
						} else {
							throw new InvalidOperationException();	
						}
					} catch(SchemaCorrectionNeededException) {
						throw;
					} catch(InvalidConstraintException e) {
						throw new ConstraintViolationException(root.ToString(), string.Empty, e);
					} catch(ConstraintException e) {
						throw new ConstraintViolationException(root.ToString(), string.Empty, e);
					} catch(Exception e) {
						throw new SqlExecutionErrorException(root.ToString(), string.Empty, e);
					}
					if(root.RecordsAffected != 0 && root.RecordsAffected != recordsUpdated) {
						throw new LockingException();
					}
				}
				DoCommit(GetUniqe(affectedRows));
				return new ModificationResult(result);
			} catch(Exception e) {
				try {
					affectedRows.Reverse();
					DoRollback(GetUniqe(affectedRows));
				} catch(Exception e2) {
					throw new DevExpress.Xpo.Exceptions.ExceptionBundleException(e, e2);
				}
				throw;
			}
		}
		protected override Task<ModificationResult> ProcessModifyDataAsync(AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken, params ModificationStatement[] dmlStatements) {
			cancellationToken.ThrowIfCancellationRequested();
			return Task.FromResult(ProcessModifyData(dmlStatements));
		}
		ICollection<DataRow> GetUniqe(List<DataRow> affectedRows) {
			Dictionary<DataRow, DataRow> list = new Dictionary<DataRow,DataRow>();
			for(int i = affectedRows.Count - 1; i >= 0; i--) {
				if(list.ContainsKey(affectedRows[i]))
					affectedRows.RemoveAt(i);
				else
					list.Add(affectedRows[i], null);
			}
			return affectedRows;
		}
		protected virtual void DoCommit(ICollection<DataRow> affectedRows) {
			foreach(DataRow row in affectedRows)
				row.AcceptChanges();
		}
		protected virtual void DoRollback(ICollection<DataRow> affectedRows) {
			Data.RejectChanges();
		}
		public static void ClearDataSet(DataSet dataSet) {
			int count = dataSet.Tables.Count;
			for(int i = 0; i < count; i++) {
				ConstraintCollection cons = dataSet.Tables[i].Constraints;
				for(int j = 0; j < cons.Count; j++) {
					Constraint c = cons[j];
					if(!(c is UniqueConstraint)) {
						cons.RemoveAt(j);
						j--;
					}
				}
			}
			for(int i = 0; i < count; i++) {
				dataSet.Tables[i].Constraints.Clear();
				dataSet.Tables[i].ChildRelations.Clear();
			}
			dataSet.Tables.Clear();
		}
		protected override void ProcessClearDatabase() {
			ClearDataSet(Data);
			descriptors.Clear();
		}
		public DBTable GetTableSchema(string tableName) {
			DBTable result = new DBTable(tableName);
			DataTable table = DataSetStoreHelpers.GetTable(Data, result.Name);
			foreach(DataColumn column in table.Columns) {
				result.AddColumn(new DBColumn(column.ColumnName, Array.IndexOf(table.PrimaryKey, column) >= 0, null, column.DataType == typeof(string) ? column.MaxLength : 0, DBColumn.GetColumnType(column.DataType)));
			}
			if(table.PrimaryKey != null && table.PrimaryKey.Length > 0) {
				StringCollection pkcols = new StringCollection();
				foreach(DataColumn column in table.PrimaryKey) {
					pkcols.Add(column.ColumnName);
				}
				result.PrimaryKey = new DBPrimaryKey(pkcols);
			}
			foreach(Constraint con in table.Constraints) {
				UniqueConstraint unique = con as UniqueConstraint;
				if(unique == null)
					continue;
				StringCollection cols = new StringCollection();
				foreach(DataColumn column in unique.Columns) {
					cols.Add(column.ColumnName);
				}
				result.AddIndex(new DBIndex(unique.ConstraintName, cols, true));
			}
			foreach(Constraint con in table.Constraints) {
				ForeignKeyConstraint fk = con as ForeignKeyConstraint;
				if(fk == null)
					continue;
				StringCollection cols = new StringCollection();
				foreach(DataColumn column in fk.Columns) {
					cols.Add(column.ColumnName);
				}
				StringCollection relcols = new StringCollection();
				foreach(DataColumn column in fk.RelatedColumns) {
					relcols.Add(column.ColumnName);
				}
				result.AddForeignKey(new DBForeignKey(cols, fk.RelatedTable.TableName, relcols));
			}
			return result;
		}
		public string[] GetStorageTablesList(bool includeViews) {
			int count = Data.Tables.Count;
			string[] result = new string[count];
			for(int i = 0; i < count; i++)
				result[i] = Data.Tables[i].TableName;
			return result;
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
		class XmlFileDataSetStore : DataStoreSerializedBase, IDataStoreForTests, IDataStoreSchemaExplorer, IDataStoreSchemaExplorerSp, IDisposable {
			protected readonly DataSetDataStore Nested;
			protected readonly FileStream fileStream;
			public XmlFileDataSetStore(string fileName, AutoCreateOption autoCreateOption, bool readOnly) {
				DataSet ds = new DataSet("XpoDataSet");
				if(!XpoDefault.TryResolveAspDataDirectory(ref fileName)) {
					throw new UnableToOpenDatabaseException(fileName, null);
				}
				try {
					FileMode mode = (autoCreateOption == AutoCreateOption.DatabaseAndSchema) ? FileMode.OpenOrCreate : FileMode.Open;
					fileStream = new FileStream(fileName, mode, readOnly ? FileAccess.Read : FileAccess.ReadWrite);
					if(fileStream.Length > 0) {
						ds.ReadXml(fileStream, XmlReadMode.ReadSchema);
						ds.AcceptChanges();
					}
				} catch(Exception e) {
					throw new UnableToOpenDatabaseException(fileName, e);
				}
				Nested = new DataSetDataStore(ds, autoCreateOption);
			}
			void Flush() {
				fileStream.Position = 0;
				fileStream.SetLength(0);
				Nested.Data.WriteXml(fileStream, XmlWriteMode.WriteSchema);
				fileStream.Flush();
			}
			Task FlushAsync(CancellationToken cancellationToken) {
				fileStream.Position = 0;
				fileStream.SetLength(0);
				Nested.Data.WriteXml(fileStream, XmlWriteMode.WriteSchema);
				return fileStream.FlushAsync(cancellationToken);
			}
			public override AutoCreateOption AutoCreateOption {
				get { return Nested.AutoCreateOption; }
			}
			protected override ModificationResult ProcessModifyData(params ModificationStatement[] dmlStatements) {
				ModificationResult result = Nested.ModifyData(dmlStatements);
				Flush();
				return result;
			}
			protected override async Task<ModificationResult> ProcessModifyDataAsync(AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken, params ModificationStatement[] dmlStatements) {
				ModificationResult result = await Nested.ModifyDataAsync(cancellationToken, dmlStatements).ConfigureAwait(false);
				await FlushAsync(cancellationToken);
				return result;
			}
			protected override SelectedData ProcessSelectData(params SelectStatement[] selects) {
				return Nested.SelectData(selects);
			}
			protected override Task<SelectedData> ProcessSelectDataAsync(AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken, params SelectStatement[] selects) {
				return Nested.SelectDataAsync(cancellationToken, selects);
			}
			protected override UpdateSchemaResult ProcessUpdateSchema(bool doNotCreateIfFirstTableNotExist, params DBTable[] tables) {
				int cnt = Nested.Data.Tables.Count;
				UpdateSchemaResult result = Nested.UpdateSchema(doNotCreateIfFirstTableNotExist, tables);
				if(result != UpdateSchemaResult.FirstTableNotExists && Nested.Data.Tables.Count != cnt)
					Flush();
				return result;
			}
			protected override async Task<UpdateSchemaResult> ProcessUpdateSchemaAsync(AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken, bool doNotCreateIfFirstTableNotExist, DBTable[] tables) {
				int cnt = Nested.Data.Tables.Count;
				UpdateSchemaResult result = await Nested.UpdateSchemaAsync(cancellationToken, doNotCreateIfFirstTableNotExist, tables);
				if(result != UpdateSchemaResult.FirstTableNotExists && Nested.Data.Tables.Count != cnt)
					await FlushAsync(cancellationToken);
				return result;
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
				throw new NotSupportedException();
			}
		}
		public DBStoredProcedure[] GetStoredProcedures() {
			throw new NotSupportedException();
		}
	}
	public class DataSetProviderFactory : ProviderFactory {
		public override IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			throw new NotSupportedException();
		}
		public override IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			return DataSetDataStore.CreateProviderFromString(connectionString, autoCreateOption, out objectsToDisposeOnDisconnect);
		}
		public override string GetConnectionString(Dictionary<string, string> parameters) {
			if(!parameters.ContainsKey(DatabaseParamID)) { return null; }
			return DataSetDataStore.GetConnectionString(parameters[DatabaseParamID], parameters.ContainsKey(ReadOnlyParamID) ? parameters[ReadOnlyParamID] != "0" : false);
		}
		public override IDataStore CreateProvider(Dictionary<string, string> parameters, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			string connectionString = this.GetConnectionString(parameters);
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
		public override string ProviderKey { get { return "XmlDataSet"; } }
		public override string[] GetDatabases(string server, string userId, string password) {
			return new string[1] { server };
		}
		public override string FileFilter { get { return "Xml files|*.xml"; } }
		public override bool MeanSchemaGeneration { get { return true; } }
	}
}
