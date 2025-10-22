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
using System.Text;
using System.Globalization;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.Exceptions;
using DevExpress.Xpo.Helpers;
using DevExpress.Xpo.Metadata;
using DevExpress.Xpo.Metadata.Helpers;
using System.Xml.Serialization;
using DevExpress.Data.Filtering;
using DevExpress.Data.Filtering.Exceptions;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using DevExpress.Xpo.DB.Helpers;
using DevExpress.Utils;
using DevExpress.Data.Filtering.Helpers;
namespace DevExpress.Xpo.Generators {
	public class GetRangeHelper {
		GetRangeHelper() { }
		public static List<object> GetRange(List<object> src, int index, int count) {
			List<object> rv = new List<object>(count);
			for(int i = index; i < index + count; ++i) {
				rv.Add(src[i]);
			}
			return rv;
		}
		public static List<object> GetRange(IList src, int index, int count) {
			List<object> rv = new List<object>(count);
			for(int i = index; i < index + count; ++i) {
				rv.Add(src[i]);
			}
			return rv;
		}
	}
}
namespace DevExpress.Xpo.Generators {
	public class CollectionCriteriaPatcher {
		public readonly bool SelectDeleted;
		public readonly XPObjectTypesManager TypesPatchManager;
		public CriteriaOperator PatchCriteria(XPClassInfo classInfo, CriteriaOperator originalCriteria) {
			CriteriaOperator result = originalCriteria;
			if(!SelectDeleted && classInfo.IsGCRecordObject) {
				result = GroupOperator.And(new NullOperator(GCRecordField.StaticName), result);
			}
			if(TypesPatchManager != null) {
				TypesPatchManager.EnsureIsTypedObjectValid();
				if(classInfo.TableMapType == MapInheritanceType.ParentTable) {
					if(classInfo.IsTypedObject) {
						List<XPObjectType> types = new List<XPObjectType>();
						foreach(XPObjectType objectType in TypesPatchManager.AllTypes.Values) {
							var typeObjectInfo = objectType.TypeClassInfo;
							if (typeObjectInfo != null && typeObjectInfo.IsAssignableTo(classInfo)) {
								types.Add(objectType);
							}
						}
						switch(types.Count) {
							case 0:
								result = GroupOperator.And(new BinaryOperator(XPObjectType.ObjectTypePropertyName, TypesPatchManager.GetObjectType(classInfo)), result);
								break;
							case 1:
								result = GroupOperator.And(new BinaryOperator(XPObjectType.ObjectTypePropertyName, types[0]), result);
								break;
							default:
								result = GroupOperator.And(new InOperator(XPObjectType.ObjectTypePropertyName, types), result);
								break;
						}
					}
				}
			}
			return result;
		}
		public CollectionCriteriaPatcher(bool selectDeleted, XPObjectTypesManager typesPatchManager) {
			this.SelectDeleted = selectDeleted;
			this.TypesPatchManager = typesPatchManager;
		}
		public static CollectionCriteriaPatcher CloneToAnotherSession(CollectionCriteriaPatcher original, Session targetSession) {
			if(original == null || original.TypesPatchManager == null)
				return original;
			else
				return new CollectionCriteriaPatcher(original.SelectDeleted, targetSession.TypesManager);
		}
	}
	public abstract class BatchWideDataHolder {
		int currentTag;
		public int GetNextTag() {
			return currentTag++;
		}
		public readonly XPDictionary Dictionary;
		protected BatchWideDataHolder(IXPDictionaryProvider dictionary) {
			this.Dictionary = dictionary.Dictionary;
		}
		public abstract OperandValue GetParameter(object value);
		public abstract OperandValue GetParameter(object value, DBColumnType dbType, string dbTypeName, int size);
	}
	public class BatchWideDataHolder4Modification: BatchWideDataHolder {
		ICollection deletedObjects;
		ObjectSet insertedObjects;
		ObjectDictionary<ParameterValue> identityParameters;
		ObjectDictionary<MemberInfoCollection> updatedMembersBeforeDeleteDict;
		Dictionary<QueryOperand, QueryOperand> queryOperandsCache;
		public BatchWideDataHolder4Modification(IXPDictionaryProvider dictionary) : base(dictionary) { }
		public void RegisterDeletedObjects(IEnumerable objects4Delete) {
#if DEBUGTEST
			DevExpress.Xpo.Tests.TestingHelper.AssertIsNull(deletedObjects);
#endif
			deletedObjects = objects4Delete.Cast<object>().ToArray();
		}
		public ParameterValue CreateIdentityParameter(object theObject) {
			if(theObject == null)
				throw new ArgumentNullException(nameof(theObject));
			if(identityParameters == null)
				identityParameters = new ObjectDictionary<ParameterValue>();
#if DEBUGTEST
			Dictionary.GetClassInfo(theObject);
			DevExpress.Xpo.Tests.TestingHelper.AssertIsFalse(identityParameters.ContainsKey(theObject));
#endif
			ParameterValue result = new ParameterValue(GetNextTag());
			XPClassInfo ci = Dictionary.GetClassInfo(theObject);
			result.DBType = ci.KeyProperty.MappingFieldDBType;
			result.DBTypeName = ci.KeyProperty.MappingFieldDBTypeName;
			identityParameters.Add(theObject, result);
			return result;
		}
		public bool IsObjectAlreadyInserted(object theObject) {
			if(insertedObjects == null)
				return false;
			return insertedObjects.Contains(theObject);
		}
		public void RegisterInsertedObject(object theObject) {
			if(insertedObjects == null) {
				insertedObjects = new ObjectSet();
			}
#if DEBUGTEST
			DevExpress.Xpo.Tests.TestingHelper.AssertIsFalse(insertedObjects.Contains(theObject));
#endif
			insertedObjects.Add(theObject);
		}
		public ICollection InsertedObjects { get { return insertedObjects; } }
		public ICollection DeletedObjects { get { return deletedObjects; } }
		public override OperandValue GetParameter(object value) {
			return GetParameter(value, DBColumnType.Unknown, null, 0);
		}
		public override OperandValue GetParameter(object value, DBColumnType dbType, string dbTypeName, int size) {
			XPClassInfo ci = Dictionary.QueryClassInfo(value);
			if(ci == null) {
				ParameterValue result = new ParameterValue(GetNextTag());
				result.Value = value;
				result.DBType = dbType;
				result.DBTypeName = dbTypeName;
				result.Size = size;
				return result;
			} else {
				ParameterValue result;
				if(identityParameters != null) {
					if(identityParameters.TryGetValue(value, out result))
						return result;
				}
				result = new ParameterValue(GetNextTag());
				XPMemberInfo idMember = ci.KeyProperty;
				if(idMember != null) {
					result.Value = ci.GetId(value);
					result.DBType = idMember.MappingFieldDBType;
					result.DBTypeName = idMember.MappingFieldDBTypeName;
					result.Size = idMember.MappingFieldSize;
				}
				return result;
			}
		}
		public void RegisterUpdatedMembersBeforeDelete(object theObject, MemberInfoCollection members) {
			if (updatedMembersBeforeDeleteDict == null) updatedMembersBeforeDeleteDict = new ObjectDictionary<MemberInfoCollection>();
			updatedMembersBeforeDeleteDict[theObject] = members;
		}
		public bool TryGetUpdatedMembersBeforeDelete(object theObject, out MemberInfoCollection members) {
			if (updatedMembersBeforeDeleteDict == null) {
				members = null;
				return false;
			}
			return updatedMembersBeforeDeleteDict.TryGetValue(theObject, out members);
		}
		public QueryOperand CacheQueryOperand(QueryOperand toCache) {
			if(queryOperandsCache == null)
				queryOperandsCache = new Dictionary<QueryOperand, QueryOperand>();
			QueryOperand rv;
			if(queryOperandsCache.TryGetValue(toCache, out rv))
				return rv;
			queryOperandsCache.Add(toCache, toCache);
			return toCache;
		}
	}
	public class BatchWideDataHolder4Select: BatchWideDataHolder {
		public BatchWideDataHolder4Select(IXPDictionaryProvider dictionary) : base(dictionary) { }
		Dictionary<ParameterValueDescriptor, ParameterValue> parametersByValues;
		ObjectDictionary<ParameterValue> parametersByObjects;
		public override OperandValue GetParameter(object value) {
			return GetParameter(value, DBColumnType.Unknown, null, 0);
		}
		public override OperandValue GetParameter(object value, DBColumnType dbType, string dbTypeName, int size) {
			XPClassInfo ci = Dictionary.QueryClassInfo(value);
			if(ci == null) {
				if(parametersByValues == null)
					parametersByValues = new Dictionary<ParameterValueDescriptor, ParameterValue>();
				ParameterValue result;
				ParameterValueDescriptor paramDescriptor = new ParameterValueDescriptor() {
					Value = value,
					DBType = dbType,
					DBTypeName = dbTypeName,
					Size = size
				};
				if(!parametersByValues.TryGetValue(paramDescriptor, out result)) {
					result = new ParameterValue(GetNextTag());
					result.Value = value;
					result.DBType = paramDescriptor.DBType;
					result.DBTypeName = paramDescriptor.DBTypeName;
					result.Size = paramDescriptor.Size;
					parametersByValues.Add(paramDescriptor, result);
				}
				return result;
			} else {
				if(parametersByObjects == null)
					parametersByObjects = new ObjectDictionary<ParameterValue>();
				ParameterValue result;
				if(!parametersByObjects.TryGetValue(value, out result)) {
					result = new ParameterValue(GetNextTag());
					XPMemberInfo idMember = ci.KeyProperty;
					if(idMember != null) {
						result.Value = ci.GetId(value);
						result.DBType = idMember.MappingFieldDBType;
						result.DBTypeName = idMember.MappingFieldDBTypeName;
						result.Size = idMember.MappingFieldSize;
					}
					parametersByObjects.Add(value, result);
				}
				return result;
			}
		}
		class ParameterValueDescriptor {
			public object Value;
			public DBColumnType DBType;
			public string DBTypeName;
			public int Size;
			public override int GetHashCode() {
				return HashCodeHelper.CalculateGeneric(Value, DBType, DBTypeName, Size);
			}
			public override bool Equals(object obj) {
				var other = obj as ParameterValueDescriptor;
				return (other != null && DBType == other.DBType && Size == other.Size && Equals(Value, other.Value));
			}
		}
	}
	struct SingleAggregateItem {
		public XPMemberInfo CollectionProperty;
		public CriteriaOperator Condition;
		public XPClassInfo ClassInfo;
		public SingleAggregateItem(CriteriaOperator condition, XPClassInfo classInfo, XPMemberInfo collectionProperty) {
			Condition = condition;
			ClassInfo = classInfo;
			CollectionProperty = collectionProperty;
		}
	}
	class SingleAggregateItemComparer : IEqualityComparer<SingleAggregateItem> {
		public bool Equals(SingleAggregateItem x, SingleAggregateItem y) {
			return x.ClassInfo == y.ClassInfo && CriteriaOperator.CriterionEquals(x.Condition, y.Condition) && x.CollectionProperty == y.CollectionProperty;
		}
		public int GetHashCode(SingleAggregateItem obj) {
			return HashCodeHelper.CalculateGeneric(obj.ClassInfo, obj.Condition, obj.CollectionProperty);
		}
	}
	public abstract class BaseQueryGenerator : IClientCriteriaVisitor<CriteriaOperator> {
		Dictionary<string, Dictionary<DBTable, JoinNode>> inheritanceSubNodes;
		Dictionary<string, Dictionary<string, JoinNode>> referenceSubNodes;
		Dictionary<string, Dictionary<string, JoinNode>> reverseSubNodes;
		Dictionary<string, Dictionary<SingleAggregateItem, JoinNode>> singleAggregateSubNodes;
		Dictionary<string, JoinNode> nakedSingleNodes;
		Dictionary<string, JoinNode> projectedSingleNodes;
		Dictionary<string, ProjectionNodeItem> projectionNodes;
		Dictionary<CriteriaOperator, PropertyAlias> multiColumnAliases;
		int singleNesting = 0;
		Int32 indexCount = 0;
		Stack<XPClassInfo> classInfoStack;
		Stack<JoinNode> nodeStack;
		HashSet<string> nodeStackHashSet;
		XPClassInfo classInfo;
		JoinNode rootNode;
		BaseStatement root;
		QueryParameterCollection queryParameters;
		protected readonly BatchWideDataHolder BatchWideData;
		readonly CollectionCriteriaPatcher collectionCriteriaPatcher;
		public Stack<XPClassInfo> ClassInfoStack {
			get {
				if(classInfoStack == null) classInfoStack = new Stack<XPClassInfo>();
				return classInfoStack;
			}
		}
		public Stack<JoinNode> NodeStack {
			get {
				if(nodeStack == null) nodeStack = new Stack<JoinNode>();
				return nodeStack;
			}
		}
		public HashSet<string> NodeStackHashSet {
			get {
				if(nodeStackHashSet == null) nodeStackHashSet = new HashSet<string>();
				return nodeStackHashSet;
			}
		}
		protected QueryOperand CreateOperand(XPMemberInfo member, string nodeAlias) {
			return CreateOperand(member.MappingField, nodeAlias); 
		}
		protected QueryOperand CreateOperand(string mappingField, string nodeAlias) {
			return new QueryOperand(mappingField, nodeAlias);
		}
		CriteriaOperator GetJoinCondition(string leftAlias, string rightAlias, XPMemberInfo leftMember, XPMemberInfo rightMember) {
			if(rightMember.SubMembers.Count == 0)
				return CreateOperand(leftMember, leftAlias) == CreateOperand(rightMember, rightAlias);
			else {
				CriteriaOperator condition = null;
				foreach(XPMemberInfo mi in rightMember.SubMembers) {
					if(mi.IsPersistent) {
						string leftMemberMappingPrefix = (leftMember == rightMember) ? string.Empty : leftMember.MappingField;
						CriteriaOperator miCondition = CreateOperand(leftMemberMappingPrefix + mi.MappingField, leftAlias) == CreateOperand(mi, rightAlias); 
						condition = GroupOperator.And(condition, miCondition);
					}
				}
				return condition;
			}
		}
		public void TryAddNodeIntoProjection(JoinNode prevnode, JoinNode node) {
			JoinNode projectionNode;
			if(projectedSingleNodes != null && projectedSingleNodes.TryGetValue(prevnode.Alias, out projectionNode)) {
				projectedSingleNodes.Add(node.Alias, projectionNode);
				ProjectionNodeItem projectedNodeItem;
				if(projectionNodes != null && projectionNodes.TryGetValue(projectionNode.Alias, out projectedNodeItem)) {
					projectedNodeItem.ProjectedNodes.Add(node);
				}
			}
		}
		JoinNode AppendInheritanceJoinNode(XPClassInfo branch, XPMemberInfo property, JoinNode prevnode) {
			List<XPClassInfo> classes = new List<XPClassInfo>();
			bool upCast = !branch.IsAssignableTo(property.Owner);
			if(upCast) {
				List<XPClassInfo> upClasses = new List<XPClassInfo>();
				XPClassInfo top = property.Owner;
				do {
					upClasses.Add(top);
					top = top.BaseClass;
				} while(top != null);
				upClasses.Reverse();
				int pos;
				while((pos = upClasses.IndexOf(branch)) < 0) {
					classes.Add(branch);
					branch = branch.BaseClass;
					if(branch == null)
						throw new Exception();
				}
				upClasses.RemoveRange(0, pos);
				classes.AddRange(upClasses);
			} else {
				do {
					branch = branch.BaseClass;
					classes.Add(branch);
				} while(!property.IsMappingClass(branch));
			}
			string onClauseAlias = prevnode.Alias;
			JoinNode node = null;
			for(int i = 0; i < classes.Count; i++) {
				branch = classes[i];
				DBTable mappingTable = branch.Table;
				ProjectionNodeItem projectionNodeItem;
				if(prevnode.Table.Equals(mappingTable)
					|| (projectionNodes != null && projectionNodes.TryGetValue(prevnode.Alias, out projectionNodeItem) && projectionNodeItem.ProjectedNodes.Any(n => n.Table.Equals(mappingTable)))) {
					node = prevnode;
				} else {
					if(nakedSingleNodes != null && nakedSingleNodes.ContainsKey(prevnode.Alias) && (projectedSingleNodes == null || !projectedSingleNodes.ContainsKey(prevnode.Alias))) {
						if(projectedSingleNodes == null)
							projectedSingleNodes = new Dictionary<string, JoinNode>();
						if(projectionNodes == null)
							projectionNodes = new Dictionary<string, ProjectionNodeItem>();
						var projectionNode = prevnode;
						var innerNode = new SelectStatement(prevnode.Table, prevnode.Alias) {
							SubNodes = prevnode.SubNodes
						};
						var projection = new DBProjection(innerNode);
						projectionNode.Alias = GetNextNodeAlias();
						projectionNode.SubNodes = new JoinNodeCollection();
						projectionNode.Table = projection;
						projectedSingleNodes.Add(innerNode.Alias, projectionNode);
						prevnode = innerNode;
						projectionNodes.Add(projectionNode.Alias, new ProjectionNodeItem(projectionNode, new List<JoinNode>() {
							innerNode
						}));
					}
					if(inheritanceSubNodes == null)
						inheritanceSubNodes = new Dictionary<string, Dictionary<DBTable, JoinNode>>();
					Dictionary<DBTable, JoinNode> inheritedCache;
					if(!inheritanceSubNodes.TryGetValue(prevnode.Alias, out inheritedCache)) {
						inheritedCache = new Dictionary<DBTable, JoinNode>();
						inheritanceSubNodes.Add(prevnode.Alias, inheritedCache);
					}
					if(!inheritedCache.TryGetValue(mappingTable, out node)) {
						node = new JoinNode(mappingTable, GetNextNodeAlias(), upCast ? JoinType.LeftOuter : prevnode.Type);
						node.Condition = GetJoinCondition(onClauseAlias, node.Alias, branch.KeyProperty, branch.KeyProperty);
						prevnode.SubNodes.Add(node);
						inheritedCache.Add(mappingTable, node);
						TryAddNodeIntoProjection(prevnode, node);
					}
					onClauseAlias = node.Alias;
				}
			}
			return node;
		}
		static void SetJoinTypeWithSubNodes(JoinNode node, JoinType type) {
			node.Type = type;
			foreach(JoinNode subNode in node.SubNodes)
				SetJoinTypeWithSubNodes(subNode, type);
		}
		JoinNode AppendJoinNode(XPMemberInfo property, JoinNode prevnode, JoinType type) {
			if(referenceSubNodes == null)
				referenceSubNodes = new Dictionary<string, Dictionary<string, JoinNode>>();
			JoinNode projectionNode;
			if(projectedSingleNodes != null && projectedSingleNodes.TryGetValue(prevnode.Alias, out projectionNode)) {
				prevnode = projectionNode;
			}
			Dictionary<string, JoinNode> joinCache;
			if(!referenceSubNodes.TryGetValue(prevnode.Alias, out joinCache)) {
				joinCache = new Dictionary<string, JoinNode>();
				referenceSubNodes.Add(prevnode.Alias, joinCache);
			}
			JoinNode node;
			if(joinCache.TryGetValue(property.MappingField, out node)) {
				if(type != JoinType.Inner && node.Type != type) {
					SetJoinTypeWithSubNodes(node, type);
				}
			} else {
				node = new JoinNode(property.ReferenceType.Table, GetNextNodeAlias(), type == prevnode.Type ? type : JoinType.LeftOuter);
				node.Condition = GetJoinCondition(prevnode.Alias, node.Alias, property, property.ReferenceType.KeyProperty);
				prevnode.SubNodes.Add(node);
				joinCache.Add(property.MappingField, node);
				TryAddNodeIntoProjection(prevnode, node);
			}
			return node;
		}
		JoinNode AppendReverseJoinNode(XPMemberInfo property, JoinNode prevnode, JoinType type) {
			if(reverseSubNodes == null)
				reverseSubNodes = new Dictionary<string, Dictionary<string, JoinNode>>();
			JoinNode projectionNode;
			if(projectedSingleNodes != null && projectedSingleNodes.TryGetValue(prevnode.Alias, out projectionNode)) {
				prevnode = projectionNode;
			}
			Dictionary<string, JoinNode> joinCache;
			if(!reverseSubNodes.TryGetValue(prevnode.Alias, out joinCache)) {
				joinCache = new Dictionary<string, JoinNode>();
				reverseSubNodes.Add(prevnode.Alias, joinCache);
			}
			JoinNode node;
			if(joinCache.TryGetValue(property.MappingField, out node)) {
				if(type != JoinType.Inner && node.Type != type) {
					SetJoinTypeWithSubNodes(node, type);
				}
			} else {
				node = new JoinNode(property.Owner.Table, GetNextNodeAlias(), type == prevnode.Type ? type : JoinType.LeftOuter);
				node.Condition = GetJoinCondition(prevnode.Alias, node.Alias, property.ReferenceType.KeyProperty, property);
				prevnode.SubNodes.Add(node);
				joinCache.Add(property.MappingField, node);
				TryAddNodeIntoProjection(prevnode, node);
			}
			return node;
		}
		JoinNode AppendSingleAggregateJoinNode(XPClassInfo ci, XPMemberInfo collectionProperty, CriteriaOperator condition, JoinNode prevnode, JoinType type) {
			if (singleNesting > 0) throw new InvalidOperationException(Res.GetString(Res.Generator_TheUseOfNestedSingleAggregatesIsProhibited));
			singleNesting++;
			try {
				Dictionary<SingleAggregateItem, JoinNode> singleNodeCache;
				if(singleAggregateSubNodes == null) singleAggregateSubNodes = new Dictionary<string, Dictionary<SingleAggregateItem, JoinNode>>();
				if(!singleAggregateSubNodes.TryGetValue(prevnode.Alias, out singleNodeCache)) {
					singleNodeCache = new Dictionary<SingleAggregateItem, JoinNode>(new SingleAggregateItemComparer());
					singleAggregateSubNodes.Add(prevnode.Alias, singleNodeCache);
				}
				SingleAggregateItem item = new SingleAggregateItem(condition, ci, collectionProperty);
				JoinNode singleNode;
				if(singleNodeCache.TryGetValue(item, out singleNode)) {
					if(type != JoinType.Inner && singleNode.Type != type) {
						SetJoinTypeWithSubNodes(singleNode, type);
					}
				} else {
					singleNode = new JoinNode(ci.Table, GetNextNodeAlias(), type == prevnode.Type ? type : JoinType.LeftOuter);
					singleNodeCache.Add(item, singleNode);
					if(nakedSingleNodes == null)
						nakedSingleNodes = new Dictionary<string, JoinNode>();
					nakedSingleNodes.Add(singleNode.Alias, singleNode);
					CriteriaOperator joinCondition = null;
					if(collectionProperty != null) {
						joinCondition = GetJoinCondition(prevnode.Alias, singleNode.Alias, classInfo.KeyProperty, collectionProperty.GetAssociatedMember());
					}
					singleNode.Condition = GroupOperator.And(joinCondition, ProcessLogicalInContext(ci, singleNode, PatchCriteria(condition, ci)));
					prevnode.SubNodes.Add(singleNode);
				}
				return singleNode;
			} finally {
				singleNesting--;
			}
		}
		JoinNode AppendSingleAggregateManyToManyJoinNode(XPClassInfo ci, XPMemberInfo firstProperty, XPMemberInfo secondProperty, XPMemberInfo collectionProperty, CriteriaOperator condition, JoinNode prevNode, JoinType type) {
			if (singleNesting > 0) throw new InvalidOperationException(Res.GetString(Res.Generator_TheUseOfNestedSingleAggregatesIsProhibited));
			singleNesting++;
			try {
				JoinNode intermidiateNode = AppendReverseJoinNode(firstProperty, prevNode, type);
				Dictionary<SingleAggregateItem, JoinNode> singleNodeCache;
				if(singleAggregateSubNodes == null) singleAggregateSubNodes = new Dictionary<string, Dictionary<SingleAggregateItem, JoinNode>>();
				if(!singleAggregateSubNodes.TryGetValue(intermidiateNode.Alias, out singleNodeCache)) {
					singleNodeCache = new Dictionary<SingleAggregateItem, JoinNode>(new SingleAggregateItemComparer());
					singleAggregateSubNodes.Add(intermidiateNode.Alias, singleNodeCache);
				}
				SingleAggregateItem item = new SingleAggregateItem(condition, ci, collectionProperty);
				JoinNode singleNode;
				if(singleNodeCache.TryGetValue(item, out singleNode)) {
					if(type != JoinType.Inner && singleNode.Type != type) {
						SetJoinTypeWithSubNodes(singleNode, type);
					}
				} else {
					singleNode = new JoinNode(ci.Table, GetNextNodeAlias(), type == intermidiateNode.Type ? type : JoinType.LeftOuter);
					singleNodeCache.Add(item, singleNode);
					if(nakedSingleNodes == null)
						nakedSingleNodes = new Dictionary<string, JoinNode>();
					nakedSingleNodes.Add(singleNode.Alias, singleNode);
					CriteriaOperator joinCondition = GetJoinCondition(intermidiateNode.Alias, singleNode.Alias, secondProperty, ci.KeyProperty);
					singleNode.Condition = GroupOperator.And(joinCondition, ProcessLogicalInContext(ci, singleNode, PatchCriteria(condition, ci)));
					intermidiateNode.SubNodes.Add(singleNode);
				}
				return singleNode;
			} finally {
				singleNesting--;
			}
		}
		protected abstract BaseStatement CreateRootStatement(DBTable table, string alias);
		protected virtual void InitData() {
			indexCount = 0;
			XPClassInfo branch = classInfo;
			if(!branch.IsPersistent && branch.PersistentBaseClass != null)
				branch = branch.PersistentBaseClass;
			root = CreateRootStatement(branch.Table, GetNextNodeAlias());
			rootNode = root;
		}
		protected CriteriaOperator Process(CriteriaOperator operand) {
			if(ReferenceEquals(operand, null))
				return null;
			return operand.Accept(this);
		}
		protected BaseStatement GenerateSql(CriteriaOperator criteria) {
			queryParameters = new QueryParameterCollection();
			InitData();
			InternalGenerateSql(criteria);
			if(!string.IsNullOrEmpty(root.Alias)) {
				ProjectionAliasPatcher.Patch(root, projectedSingleNodes, projectionNodes);
				StatementNormalizer.Normalize(root);
			}
			return root;
		}
		protected void BuildAssociationTree(CriteriaOperator criteria) {
			Root.Condition = ProcessLogical(criteria);
		}
		public XPClassInfo ClassInfo {
			get { return classInfo; }
			set { classInfo = value; }
		}
		protected XPDictionary Dictionary { get { return ClassInfo.Dictionary; } }
		protected BaseStatement Root { get { return root; } }
		protected BaseQueryGenerator(XPClassInfo objectInfo, BatchWideDataHolder batchWideData) : this(objectInfo, batchWideData, null) { }
		protected BaseQueryGenerator(XPClassInfo objectInfo, BatchWideDataHolder batchWideData, CollectionCriteriaPatcher collectionCriteriaPatcher) {
			objectInfo.CheckAbstractReference();
			this.classInfo = objectInfo;
			this.BatchWideData = batchWideData;
			this.collectionCriteriaPatcher = collectionCriteriaPatcher;
		}
		static string[] nodeAliasCache = Array.Empty<string>();
		protected internal virtual string GetNextNodeAlias() {
			int len = nodeAliasCache.Length;
			if(len <= indexCount) {
				string[] newCache = new string[indexCount + 10];
				Array.Copy(nodeAliasCache, newCache, len);
				for(int i = len; i < newCache.Length; i++)
					newCache[i] = "N" + i.ToString(CultureInfo.InvariantCulture);
				nodeAliasCache = newCache;
			}
			return nodeAliasCache[indexCount++];
		}
		protected PropertyAlias GetPropertyNode(MemberInfoCollection propertyPath, JoinType type) {
			JoinNode node = rootNode;
			XPMemberInfo member;
			int lastIndex = propertyPath.Count - 1;
			if(lastIndex > 0 && propertyPath[lastIndex].IsKey)
				lastIndex--;
			XPClassInfo currentClass = classInfo;
			for(int i = 0; i <= lastIndex; i++) {
				member = propertyPath[i];
				if(!member.IsMappingClass(currentClass))
					node = AppendInheritanceJoinNode(currentClass, member, node);
				if(member.IsAssociationList) {
					if(i == lastIndex)
						return new PropertyAlias(node, member);
					else
						break;
				}
				if(i != lastIndex) {
					node = AppendJoinNode(member, node, type);
					currentClass = member.ReferenceType;
				} else
					return GetMembers(node, member);
			}
			throw new Exception();
		}
		protected abstract void InternalGenerateSql(CriteriaOperator criteria);
		public bool ConvertViaDefaultValueConverter(ref object operandValue) {
			if(operandValue == null) {
				return false;
			}
			if(!ConnectionProviderSql.UseLegacyTimeSpanSupport || !(operandValue is TimeSpan)) {
				var converter = Dictionary.GetConverter(operandValue.GetType());
				if(converter != null) {
					operandValue = converter.ConvertToStorageType(operandValue);
					return true;
				}
			}
			return false;
		}
		CriteriaOperator ICriteriaVisitor<CriteriaOperator>.Visit(OperandValue theOperand) {
			Type operandType = theOperand.GetType();
			object operandValue = theOperand.Value;
			if(operandType == typeof(OperandValue) || operandType == typeof(ConstantValue)) {
				return theOperand;
			}
			return new OperandValue(operandValue);
		}
		protected virtual internal PropertyAlias GetPropertyNode(OperandProperty property, JoinType type) {
			MemberPathOperand op = property as MemberPathOperand;
			if(!ReferenceEquals(op, null)) {
				return GetPropertyNode(op.Path, type);
			}
			return (PropertyAlias)ExecuteWithPropertyNameDiving(property.PropertyName, (pn) => {
				return GetPropertyNode(ClassInfo.ParsePersistentPath(pn), type);
			});
		}
		protected CriteriaOperator ExecuteWithPropertyNameDiving(string propertyName, Func<string, CriteriaOperator> worker, bool throwOnEmptyStack = true) {
			if(worker == null) throw new ArgumentNullException(nameof(worker));
			Stack<XPClassInfo> internalClassInfoStack = null;
			Stack<JoinNode> internalNodeStack = null;
			try {
				while(!string.IsNullOrEmpty(propertyName) && propertyName.StartsWith("^.")) {
					if(internalClassInfoStack == null) {
						internalClassInfoStack = new Stack<XPClassInfo>();
						internalNodeStack = new Stack<JoinNode>();
					}
					if(ClassInfoStack.Count == 0) {
						if(throwOnEmptyStack)
							throw new InvalidOperationException();
						break;
					}
					internalClassInfoStack.Push(classInfo);
					internalNodeStack.Push(rootNode);
					classInfo = ClassInfoStack.Pop();
					rootNode = NodeStack.Pop();
					NodeStackHashSet.Remove(rootNode.Alias);
					propertyName = propertyName.Substring(2);
				}
				return worker(propertyName);
			} finally {
				if(internalClassInfoStack != null) {
					while(internalClassInfoStack.Count > 0) {
						ClassInfoStack.Push(classInfo);
						NodeStack.Push(rootNode);
						NodeStackHashSet.Add(rootNode.Alias);
						classInfo = internalClassInfoStack.Pop();
						rootNode = internalNodeStack.Pop();
					}
				}
			}
		}
		protected internal class PropertyAlias: CriteriaOperator {
			readonly object subMembers;
			readonly string prefix;
			public readonly XPMemberInfo Member;
			public readonly JoinNode Node;
			public XPMemberInfo GetMember(int i) {
				if(subMembers == null)
					throw new InvalidPropertyPathException(Res.GetString(Res.MetaData_IncorrectPathNonReferenceMember, Member.Owner.FullName, Member.Name, Member.Owner.FullName, Member.Name));
				return subMembers is List<XPMemberInfo> ? ((List<XPMemberInfo>)subMembers)[i] : (XPMemberInfo)subMembers;
			}
			public string GetMappingField(int i) {
				return prefix + GetMember(i).MappingField;
			}
			public int Count {
				get {
					if(subMembers == null)
						return 0;
					if(subMembers is List<XPMemberInfo>)
						return ((List<XPMemberInfo>)subMembers).Count;
					return 1;
				}
			}
			public PropertyAlias(JoinNode node, XPMemberInfo member, bool sub) {
				this.Member = member;
				this.Node = node;
				this.subMembers = member;
			}
			public PropertyAlias(JoinNode node, XPMemberInfo member, List<XPMemberInfo> subMembers, string prefix) {
				this.Member = member;
				this.Node = node;
				this.subMembers = subMembers;
				this.prefix = prefix;
			}
			public PropertyAlias(JoinNode node, XPMemberInfo member) {
				this.Member = member;
				this.Node = node;
			}
			public PropertyAlias() {
			}
			public static readonly PropertyAlias Empty = new PropertyAlias();
			public override T Accept<T>(ICriteriaVisitor<T> visitor) {
				throw new NotSupportedException();
			}
			public override void Accept(ICriteriaVisitor visitor) {
				throw new NotSupportedException();
			}
			protected override CriteriaOperator CloneCommon() {
				throw new NotSupportedException();
			}
		}
		class ReferenceComparer : IEqualityComparer<CriteriaOperator> {
			static readonly ReferenceComparer instance = new ReferenceComparer();
			public static ReferenceComparer Instance {
				get { return instance; }
			}
			public bool Equals(CriteriaOperator x, CriteriaOperator y) {
				return ReferenceEquals(x, y);
			}
			public int GetHashCode(CriteriaOperator obj) {
				return obj.GetHashCode();
			}
		}
		CriteriaOperator IClientCriteriaVisitor<CriteriaOperator>.Visit(OperandProperty theOperand) {
			return GetPropertyNode(theOperand, currentJoinType);
		}
		QuerySubQueryContainer ProcessSubSelectOperator(string joinTypeName, CriteriaOperator operandClause, CriteriaOperator aggregateProperty, Aggregate aggregateType) {
			XPClassInfo joinClassInfo = null;
			if(!MemberInfoCollection.TryResolveTypeAlsoByShortName(joinTypeName, classInfo, out joinClassInfo)) {
				throw new CannotResolveClassInfoException(string.Empty, joinTypeName);
			}
			SubSelectQueryGenerator gena = new SubSelectQueryGenerator(this, this.BatchWideData, string.Empty, joinClassInfo, aggregateProperty, aggregateType, collectionCriteriaPatcher);
			BaseStatement queryStatement = gena.GenerateSelect(operandClause);
			CriteriaOperator aggregatedOperand = null;
			if(queryStatement.Operands.Count > 0) {
				System.Diagnostics.Debug.Assert(queryStatement.Operands.Count == 1);
				aggregatedOperand = queryStatement.Operands[0];
			}
			return new QuerySubQueryContainer(queryStatement, aggregatedOperand, aggregateType);
		}
		QuerySubQueryContainer ProcessSubSelectOperator(string joinTypeName, CriteriaOperator operandClause, IEnumerable<CriteriaOperator> aggregateProperties, string customAggregateName) {
			XPClassInfo joinClassInfo = null;
			if(!MemberInfoCollection.TryResolveTypeAlsoByShortName(joinTypeName, classInfo, out joinClassInfo)) {
				throw new CannotResolveClassInfoException(string.Empty, joinTypeName);
			}
			SubSelectQueryGenerator gena = new SubSelectQueryGenerator(this, this.BatchWideData, string.Empty, joinClassInfo, aggregateProperties, customAggregateName, collectionCriteriaPatcher);
			BaseStatement queryStatement = gena.GenerateSelect(operandClause);
			return new QuerySubQueryContainer(queryStatement, queryStatement.Operands, customAggregateName);
		}
		QuerySubQueryContainer ProcessSubSelectOperator(OperandProperty collectionProperty, CriteriaOperator operandClause, CriteriaOperator aggregateProperty, Aggregate aggregateType) {
			PropertyAlias alias = GetPropertyNode(collectionProperty, currentJoinType);
			XPMemberInfo member = alias.Member;
			SubSelectQueryGenerator gena;
			string associatedPropertyName;
			if(member.IsManyToMany) {
				gena = new ManySubSelectQueryGenerator(member, this, this.BatchWideData, collectionProperty.PropertyName, aggregateProperty, aggregateType, collectionCriteriaPatcher);
				associatedPropertyName = "!";
			} else {
				gena = new SubSelectQueryGenerator(this, this.BatchWideData, collectionProperty.PropertyName, member.CollectionElementType, aggregateProperty, aggregateType, collectionCriteriaPatcher);
				associatedPropertyName = member.GetAssociatedMember().Name;
			}
			BaseStatement queryStatement = gena.GenerateSelect(operandClause);
			queryStatement.Condition = GroupOperator.And(CreateBinary(BinaryOperatorType.Equal, GetMembers(alias.Node, member.Owner.KeyProperty), gena.Process(new OperandProperty(associatedPropertyName))), queryStatement.Condition);
			CriteriaOperator aggregatedOperand = null;
			if(queryStatement.Operands.Count > 0) {
				System.Diagnostics.Debug.Assert(queryStatement.Operands.Count == 1);
				aggregatedOperand = queryStatement.Operands[0];
			}
			return new QuerySubQueryContainer(queryStatement, aggregatedOperand, aggregateType);
		}
		QuerySubQueryContainer ProcessSubSelectOperator(OperandProperty collectionProperty, CriteriaOperator operandClause, IEnumerable<CriteriaOperator> aggregateProperties, string customAggregateName) {
			PropertyAlias alias = GetPropertyNode(collectionProperty, currentJoinType);
			XPMemberInfo member = alias.Member;
			SubSelectQueryGenerator gena;
			string associatedPropertyName;
			if(member.IsManyToMany) {
				gena = new ManySubSelectQueryGenerator(member, this, this.BatchWideData, collectionProperty.PropertyName, aggregateProperties, customAggregateName, collectionCriteriaPatcher);
				associatedPropertyName = "!";
			} else {
				gena = new SubSelectQueryGenerator(this, this.BatchWideData, collectionProperty.PropertyName, member.CollectionElementType, aggregateProperties, customAggregateName, collectionCriteriaPatcher);
				associatedPropertyName = member.GetAssociatedMember().Name;
			}
			BaseStatement queryStatement = gena.GenerateSelect(operandClause);
			queryStatement.Condition = GroupOperator.And(CreateBinary(BinaryOperatorType.Equal, GetMembers(alias.Node, member.Owner.KeyProperty), gena.Process(new OperandProperty(associatedPropertyName))), queryStatement.Condition);
			return new QuerySubQueryContainer(queryStatement, queryStatement.Operands, customAggregateName);
		}
		protected virtual bool IsGrouped { get { return false; } }
		CriteriaOperator IClientCriteriaVisitor<CriteriaOperator>.Visit(AggregateOperand theOperator) {
			if (theOperator.IsTopLevel) {
				if(theOperator.AggregateType == Aggregate.Exists) {
					if(IsGrouped)
						return ProcessTopSubSelect(theOperator.AggregatedExpression, theOperator.Condition, theOperator.AggregateType);
					return Process(new AggregateOperand(null, theOperator.AggregatedExpression, Aggregate.Count, theOperator.Condition) > new OperandValue(0));
				}
				if (theOperator.AggregateType == Aggregate.Single) throw new InvalidOperationException(Res.GetString(Res.Generator_TheUseOfATopLevelSingleAggregateIsProhibit));
				if((object)theOperator.Condition != null)
					throw new NotSupportedException(); 
				if(theOperator.AggregateType != Aggregate.Custom) {
					object res = Process(theOperator.AggregatedExpression);
					CriteriaOperator prop;
					if(res is PropertyAlias) {
						PropertyAlias node = (PropertyAlias)res;
						prop = GetQueryOperandFromAlias(node, node, 0);
					} else {
						prop = (CriteriaOperator)res;
					}
					return new QuerySubQueryContainer(null, ReferenceEquals(theOperator.AggregatedExpression, null) ? null : prop, theOperator.AggregateType);
				} else {
					CriteriaOperator[] aggrExprs = new CriteriaOperator[theOperator.CustomAggregateOperands.Count];
					for(int i = 0; i < theOperator.CustomAggregateOperands.Count; i++) {
						object res = Process(theOperator.CustomAggregateOperands[i]);
						CriteriaOperator prop;
						if(res is PropertyAlias) {
							PropertyAlias node = (PropertyAlias)res;
							prop = GetQueryOperandFromAlias(node, node, 0);
						} else {
							prop = (CriteriaOperator)res;
						}
						aggrExprs[i] = ReferenceEquals(theOperator.CustomAggregateOperands[i], null) ? null : prop;
					}
					return new QuerySubQueryContainer(null, aggrExprs, theOperator.CustomAggregateName);
				}
			}
			if(theOperator.AggregateType == Aggregate.Single) {
				return ProcessSingleAggregateOperand(theOperator.CollectionProperty, theOperator.Condition, theOperator.AggregatedExpression);
			}
			if(theOperator.AggregateType != Aggregate.Custom) {
				return ProcessSubSelectOperator(theOperator.CollectionProperty, theOperator.Condition, theOperator.AggregatedExpression, theOperator.AggregateType);
			}
			return ProcessSubSelectOperator(theOperator.CollectionProperty, theOperator.Condition, theOperator.CustomAggregateOperands, theOperator.CustomAggregateName);
		}
		CriteriaOperator IClientCriteriaVisitor<CriteriaOperator>.Visit(JoinOperand theOperator) {
			if(theOperator.AggregateType == Aggregate.Single) {
				return ProcessSingleJoinOperand(theOperator.JoinTypeName, theOperator.Condition, theOperator.AggregatedExpression);
			}
			if(theOperator.AggregateType != Aggregate.Custom) {
				return ProcessSubSelectOperator(theOperator.JoinTypeName, theOperator.Condition, theOperator.AggregatedExpression, theOperator.AggregateType);
			}
			return ProcessSubSelectOperator(theOperator.JoinTypeName, theOperator.Condition, theOperator.CustomAggregateOperands, theOperator.CustomAggregateName);
		}
		public CriteriaOperator ProcessSingleAggregateOperand(OperandProperty collectionProperty, CriteriaOperator condition, CriteriaOperator aggregatedExpression) {
			if(ReferenceEquals(collectionProperty, null)) throw new ArgumentNullException(nameof(collectionProperty));
			return ExecuteWithPropertyNameDiving(collectionProperty.PropertyName, (pn) => {
				ManySubSelectQueryGenerator manySubSelectQueryGenerator = this as ManySubSelectQueryGenerator;
				IntermediateClassInfo intermediateClassInfo = ClassInfo as IntermediateClassInfo;
				string currentPropertyName = pn;
				if(intermediateClassInfo != null && manySubSelectQueryGenerator != null) {
					currentPropertyName = manySubSelectQueryGenerator.GetManyToManyPath(intermediateClassInfo, pn);
				}
				MemberInfoCollection mic = ClassInfo.ParsePersistentPath(currentPropertyName);
				if(mic.Count == 0) throw new InvalidOperationException();
				try {
					for(int i = 0; i < mic.Count - 1; i++) {
						PropertyAlias alias = GetPropertyNode(new MemberInfoCollection(classInfo, mic[i], mic[i + 1]), currentJoinType);
						ClassInfoStack.Push(classInfo);
						NodeStack.Push(rootNode);
						NodeStackHashSet.Add(rootNode.Alias);
						classInfo = mic[i].ReferenceType;
						rootNode = alias.Node;
					}
					PropertyAlias collectionAlias = GetPropertyNode(new MemberInfoCollection(classInfo, mic[mic.Count - 1]), currentJoinType);
					XPMemberInfo member = collectionAlias.Member;
					if(member.IsManyToMany) {
						XPClassInfo collectionCI = member.CollectionElementType;
						IntermediateClassInfo imCI = member.IntermediateClass;
						XPMemberInfo firstMember;
						XPMemberInfo secondMember;
						if(member == imCI.intermediateObjectFieldInfoLeft.refProperty) {
							firstMember = imCI.intermediateObjectFieldInfoRight;
							secondMember = imCI.intermediateObjectFieldInfoLeft;
						} else if(member == imCI.intermediateObjectFieldInfoRight.refProperty) {
							firstMember = imCI.intermediateObjectFieldInfoLeft;
							secondMember = imCI.intermediateObjectFieldInfoRight;
						} else throw new InvalidOperationException();
						JoinNode singleNode = AppendSingleAggregateManyToManyJoinNode(member.CollectionElementType, firstMember, secondMember, member, condition, rootNode, currentJoinType);
						return ProcessLogicalInContext(member.CollectionElementType, singleNode, aggregatedExpression);
					} else {
						JoinNode singleNode = AppendSingleAggregateJoinNode(member.CollectionElementType, member, condition, rootNode, currentJoinType);
						return ProcessLogicalInContext(member.CollectionElementType, singleNode, aggregatedExpression);
					}
				} finally {
					for(int i = 0; i < mic.Count - 1; i++) {
						classInfo = ClassInfoStack.Pop();
						rootNode = NodeStack.Pop();
						NodeStackHashSet.Remove(rootNode.Alias);
					}
				}
			});
		}
		public CriteriaOperator ProcessSingleJoinOperand(string joinTypeName, CriteriaOperator condition, CriteriaOperator aggregatedExpression) {
			XPClassInfo joinClassInfo = null;
			if(!MemberInfoCollection.TryResolveTypeAlsoByShortName(joinTypeName, classInfo, out joinClassInfo)) {
				throw new CannotResolveClassInfoException(string.Empty, joinTypeName);
			}
			JoinNode joinNode = AppendSingleAggregateJoinNode(joinClassInfo, null, condition, rootNode, currentJoinType);
			return ProcessLogicalInContext(joinClassInfo, joinNode, aggregatedExpression);
		}
		QuerySubQueryContainer ProcessTopSubSelect(CriteriaOperator aggregateProperty, CriteriaOperator operandClause, Aggregate aggregateType) {
			SubSelectQueryGenerator gena = new SubSelectQueryGenerator(this, this.BatchWideData, String.Empty, ClassInfo, aggregateProperty, aggregateType, collectionCriteriaPatcher);
			BaseStatement queryStatement = gena.GenerateSelect(operandClause);
			queryStatement.Condition = GroupOperator.And(GetSubJoinCriteria(gena), queryStatement.Condition);
			CriteriaOperator aggregatedOperand = null;
			if(queryStatement.Operands.Count > 0) {
				System.Diagnostics.Debug.Assert(queryStatement.Operands.Count == 1);
				aggregatedOperand = queryStatement.Operands[0];
			}
			return new QuerySubQueryContainer(queryStatement, aggregatedOperand, aggregateType);
		}
		QuerySubQueryContainer ProcessTopSubSelect(IEnumerable<CriteriaOperator> aggregateProperties, CriteriaOperator operandClause, string customAggregateName) {
			SubSelectQueryGenerator gena = new SubSelectQueryGenerator(this, this.BatchWideData, String.Empty, ClassInfo, aggregateProperties, customAggregateName, collectionCriteriaPatcher);
			BaseStatement queryStatement = gena.GenerateSelect(operandClause);
			queryStatement.Condition = GroupOperator.And(GetSubJoinCriteria(gena), queryStatement.Condition);
			return new QuerySubQueryContainer(queryStatement, queryStatement.Operands, customAggregateName);
		}
		protected virtual CriteriaOperator GetSubJoinCriteria(SubSelectQueryGenerator gena) {
			CriteriaOperator criteria = CreateBinary(BinaryOperatorType.Equal, GetPropertyNode(new OperandProperty(ClassInfo.KeyProperty.Name), currentJoinType), gena.Process(new OperandProperty(ClassInfo.KeyProperty.Name)));
			return criteria;
		}
		CriteriaOperator ICriteriaVisitor<CriteriaOperator>.Visit(BetweenOperator theOperator) {
			return Process(GroupOperator.And(
				new BinaryOperator(theOperator.TestExpression, theOperator.BeginExpression, BinaryOperatorType.GreaterOrEqual),
				new BinaryOperator(theOperator.TestExpression, theOperator.EndExpression, BinaryOperatorType.LessOrEqual))
				);
		}
		BinaryOperator CreateBinary(CriteriaOperator left, PropertyAlias leftMembers,
			CriteriaOperator right, PropertyAlias rightMembers, BinaryOperatorType opType, int index) {
			return new BinaryOperator(
				GetParameter(left, leftMembers, index),
				GetParameter(right, rightMembers, index),
				opType);
		}
		CriteriaOperator CreateBinaryGroup(PropertyAlias subMembers, CriteriaOperator left, PropertyAlias leftMembers,
			CriteriaOperator right, PropertyAlias rightMembers, BinaryOperatorType opType) {
			if(opType != BinaryOperatorType.Equal)
				throw new NotSupportedException(string.Format(opType.ToString()));  
			CriteriaOperator result = null;
			for(int i = 0; i < subMembers.Count; i++) {
				CriteriaOperator subMemberResult = CreateBinary(left, leftMembers, right, rightMembers, opType, i);
				result = GroupOperator.And(result, subMemberResult);
			}
			return result;
		}
		CriteriaOperator ICriteriaVisitor<CriteriaOperator>.Visit(BinaryOperator theOperator) {
#pragma warning disable 618
			if(theOperator.OperatorType == BinaryOperatorType.Like)
				return Process(DevExpress.Data.Filtering.Helpers.LikeCustomFunction.Convert(theOperator));
#pragma warning restore 618
			GroupOperatorType prevLeftJoinEnforcer = currentLeftJoinEnforcer;
			try {
				if(theOperator.OperatorType == BinaryOperatorType.NotEqual)
					currentLeftJoinEnforcer = (currentLeftJoinEnforcer == GroupOperatorType.Or) ? GroupOperatorType.And : GroupOperatorType.Or;
				if(ReferenceEquals(theOperator.LeftOperand, null) || ReferenceEquals(theOperator.RightOperand, null))
					throw new ArgumentNullException(nameof(theOperator), Xpo.Res.GetString(Xpo.Res.Generator_OneOfBinaryOperatorsOperandsIsNull));
				CriteriaOperator left, right;
				if(theOperator.LeftOperand is OperandValue) {
					try {
						left = Process(theOperator.LeftOperand);
					} finally {
					}
				} else {
					left = Process(theOperator.LeftOperand);
				}
				if(theOperator.RightOperand is OperandValue) {
					try {
						right = Process(theOperator.RightOperand);
					} finally {
					}
				} else {
					right = Process(theOperator.RightOperand);
				}
				return CreateBinary(theOperator.OperatorType, left, right);
			} finally {
				currentLeftJoinEnforcer = prevLeftJoinEnforcer;
			}
		}
		protected CriteriaOperator CreateBinary(BinaryOperatorType type, CriteriaOperator left, CriteriaOperator right) {
			PropertyAlias leftAlias;
			if(!(left is PropertyAlias) && TryGetMultiColumnAlias((CriteriaOperator)left, out leftAlias)) {
				left = leftAlias;
			}
			PropertyAlias rightAlias;
			if(!(right is PropertyAlias) && TryGetMultiColumnAlias((CriteriaOperator)right, out rightAlias)) {
				right = rightAlias;
			}
			PropertyAlias defaultMembers = PropertyAlias.Empty;
			if(left is PropertyAlias)
				defaultMembers = (PropertyAlias)left;
			if(right is PropertyAlias)
				defaultMembers = (PropertyAlias)right;
			PropertyAlias leftMembers = left is PropertyAlias ? (PropertyAlias)left : defaultMembers;
			if(defaultMembers.Count > 1)
				return CreateBinaryGroup(defaultMembers, left, leftMembers, right, defaultMembers,
					type);
			else
				return CreateBinary(left, leftMembers, right, defaultMembers,
					type, 0);
		}
		PropertyAlias CollectInValues(ICollection operands, IList<CriteriaOperator> values) {
			PropertyAlias defaultMembers = PropertyAlias.Empty;
			foreach(CriteriaOperator criteria in operands) {
				var value = Process(criteria);
				values.Add(value);
				if(value is PropertyAlias)
					defaultMembers = (PropertyAlias)value;
			}
			return defaultMembers;
		}
		DBColumnType CorrectParameterType(object value, DBColumnType memberType) {
			DBColumnType valueType = DBColumn.GetColumnType(value.GetType(), true);
			if(memberType == valueType) {
				return memberType;
			}
			switch(memberType) {
				case DBColumnType.Decimal:
				case DBColumnType.Double:
					if(valueType == DBColumnType.Double || valueType == DBColumnType.Decimal || valueType == DBColumnType.Single
						|| valueType == DBColumnType.Int32 || valueType == DBColumnType.UInt32
						|| valueType == DBColumnType.Int64 || valueType == DBColumnType.UInt64
						|| valueType == DBColumnType.Int16 || valueType == DBColumnType.UInt16
						|| valueType == DBColumnType.Byte) {
						return memberType;
					}
					break;
				case DBColumnType.Single:
					if(valueType == DBColumnType.Int32 || valueType == DBColumnType.UInt32
						|| valueType == DBColumnType.Int16 || valueType == DBColumnType.UInt16
						|| valueType == DBColumnType.Byte || valueType == DBColumnType.SByte) {
						return memberType;
					}
					break;
				case DBColumnType.Int32:
					if(valueType == DBColumnType.Int16 || valueType == DBColumnType.UInt16
						|| valueType == DBColumnType.Byte) {
						return memberType;
					}
					break;
				case DBColumnType.UInt32:
					if(valueType == DBColumnType.UInt16 || valueType == DBColumnType.Byte) {
						return memberType;
					}
					break;
				case DBColumnType.Int16:
				case DBColumnType.UInt16:
					if(valueType == DBColumnType.Byte) {
						return memberType;
					}
					break;
				case DBColumnType.UInt64:
					if(valueType == DBColumnType.UInt32 || valueType == DBColumnType.UInt16 || valueType == DBColumnType.Byte) {
						return memberType;
					}
					break;
				case DBColumnType.Guid:
					if(valueType == DBColumnType.String) {
						return memberType;
					}
					break;
			}
			return valueType;
		}
		OperandValue GetConstParameter(XPMemberInfo member, OperandValue operand, XPMemberInfo targetMember) {
			object value = operand.Value;
			XPClassInfo ci = Dictionary.QueryClassInfo(value);
			if(ci != null && ci.KeyProperty != null && !ci.KeyProperty.IsIdentity)
				value = ci.GetId(value);
			if(value is ArrayList || value is List<object>) {
				if(targetMember.ReferenceType != null)
					targetMember = targetMember.ReferenceType.KeyProperty;
				IList list = (IList)value;
				int index = 0;
				foreach(XPMemberInfo mi in targetMember.SubMembers) {
					if(mi.IsPersistent) {
						if(object.ReferenceEquals(mi, member)) {
							value = list[index];
							break;
						}
						index++;
					}
				}
			} else {
				value = member.GetConst(value, targetMember);
			}
			ValueConverter converter = member.Converter;
			if(converter != null) {
				value = converter.ConvertToStorageType(value);
			} else {
				ConvertViaDefaultValueConverter(ref value);
			}
			if(operand is ConstantValue) {
				return new ConstantValue(value);
			}
			if(member.MappingFieldDBType == DBColumnType.Unknown
				|| !string.IsNullOrEmpty(member.MappingFieldDBTypeName)
				|| value == null || value == DBNull.Value
				|| ci != null) {
				return GetConstParameter(value, member.MappingFieldDBType, member.MappingFieldDBTypeName, member.MappingFieldSize);
			}
			DBColumnType mappingFieldDbType = CorrectParameterType(value, member.MappingFieldDBType);
			int mappingFieldSize = (mappingFieldDbType == member.MappingFieldDBType) ? member.MappingFieldSize : 0;
			return GetConstParameter(value, mappingFieldDbType, null, mappingFieldSize);
		}
		protected OperandValue GetConstParameter(object value, DBColumnType dbType, string dbTypeName, int size) {
			if(value == null && dbType == DBColumnType.Unknown && string.IsNullOrEmpty(dbTypeName)) {
				return new OperandValue();
			}
			return BatchWideData.GetParameter(value, dbType, dbTypeName, size);
		}
		[Obsolete("Use GetConstParameter(object value, DBColumnType dbType, string dbTypeName, int size) instead.")]
		protected OperandValue GetConstParameter(object value) {
			return GetConstParameter(value, DBColumnType.Unknown, null, 0);
		}
		protected CriteriaOperator GetParameter(CriteriaOperator param, PropertyAlias alias, int index) {
			if(alias.Member == null) {
				if(param is OperandValue) {
					OperandValue p = (OperandValue)param;
					Type operandType = p.GetType();
					object operandValue = p.Value;
					if(ConvertViaDefaultValueConverter(ref operandValue)) {
						return operandType == typeof(ConstantValue) ? new ConstantValue(operandValue) : new OperandValue(operandValue);
					} else
						return (CriteriaOperator)param;
				} else
					return (CriteriaOperator)param;
			} else {
				if(param is OperandValue) {
					OperandValue p = GetConstParameter(alias.GetMember(index), (OperandValue)param, alias.Member);
					return p;
				} else
					if(param is PropertyAlias) {
					return GetQueryOperandFromAlias((PropertyAlias)param, alias, index);
				} else
					return (CriteriaOperator)param;
			}
		}
		protected CriteriaOperator GetQueryOperandFromAlias(PropertyAlias aliasFromParam, PropertyAlias alias, int index) {
			var mappingField = alias.GetMappingField(index);
			var node = aliasFromParam.Node;
			var nodeAlias = node.Alias;
			JoinNode foundProjectionNode;
			if(projectedSingleNodes != null && projectedSingleNodes.TryGetValue(nodeAlias, out foundProjectionNode) && !NodeStackHashSet.Contains(nodeAlias)) {
				node = foundProjectionNode;
				nodeAlias = node.Alias;
			}
			DBColumn memberColumn = null;
			ProjectionNodeItem projectionNodeItem;
			if(projectionNodes != null && projectionNodes.TryGetValue(nodeAlias, out projectionNodeItem)) {
				memberColumn = GetProjectedMemberColumn(mappingField, node, projectionNodeItem.ProjectedNodes);
			} else {
				memberColumn = node.GetColumn(mappingField);
			}
			return new QueryOperand(memberColumn, nodeAlias);
		}
		internal static DBColumn GetProjectedMemberColumn(string mappingField, JoinNode node, List<JoinNode> projectedNodeList) {
			var projectionNode = node;
			var foundNode = node;
			var projection = (DBProjection)projectionNode.Table;
			DBColumn memberColumn = projection.GetColumn(mappingField);
			if(memberColumn == null) {
				foreach(var projectedNode in projectedNodeList) {
					var column = projectedNode.GetColumn(mappingField);
					if(column != null) {
						memberColumn = column;
						foundNode = projectedNode;
						break;
					}
				}
				if(memberColumn == null)
					throw new InvalidOperationException(); 
				projection.AddColumn(memberColumn);
				var select = projection.Projection;
				select.Operands.Add(new QueryOperand(memberColumn, foundNode.Alias));
			}
			return memberColumn;
		}
		CriteriaOperator BuildInGroup(PropertyAlias defaultMembers, List<CriteriaOperator> values, CriteriaOperator left) {
			CriteriaOperator result = null;
			foreach(var value in values) {
				PropertyAlias rightMembers = value is PropertyAlias ? (PropertyAlias)value : defaultMembers;
				CriteriaOperator binaryGroup = CreateBinaryGroup(defaultMembers, left, defaultMembers, value, rightMembers, BinaryOperatorType.Equal);
				result = GroupOperator.Or(result, binaryGroup);
			}
			return result;
		}
		protected JoinType currentJoinType = JoinType.Inner;
		protected GroupOperatorType currentLeftJoinEnforcer = GroupOperatorType.Or;
		CriteriaOperator ICriteriaVisitor<CriteriaOperator>.Visit(InOperator theOperator) {
			var left = Process(theOperator.LeftOperand);
			JoinType prevJoinType = currentJoinType;
			if(theOperator.Operands.Count > 1)
				currentJoinType = JoinType.LeftOuter;
			try {
				var values = new List<CriteriaOperator>(theOperator.Operands.Count);
				PropertyAlias defaultMembers = CollectInValues(theOperator.Operands, values);
				if(left is PropertyAlias)
					defaultMembers = (PropertyAlias)left;
				if(defaultMembers.Count > 1)
					return BuildInGroup(defaultMembers, values, left);
				else {
					List<CriteriaOperator> list = new List<CriteriaOperator>(values.Count);
					foreach(var value in values) {
						PropertyAlias rightMembers = value is PropertyAlias ? (PropertyAlias)value : defaultMembers;
						list.Add(GetParameter(value, rightMembers, 0));
					}
					InOperator io = new InOperator(GetParameter(left, defaultMembers, 0), list);
					return io;
				}
			} finally {
				currentJoinType = prevJoinType;
			}
		}
		protected CriteriaOperator ProcessLogicalInContext(XPClassInfo ci, JoinNode singleNode, CriteriaOperator criteria) {
			ClassInfoStack.Push(classInfo);
			NodeStack.Push(rootNode);
			NodeStackHashSet.Add(rootNode.Alias);
			rootNode = singleNode;
			classInfo = ci;
			try {
				return ProcessLogical(criteria);
			} finally {
				classInfo = ClassInfoStack.Pop();
				rootNode = NodeStack.Pop();
				NodeStackHashSet.Remove(rootNode.Alias);
			}
		}
		protected CriteriaOperator ProcessLogical(CriteriaOperator operand) {
			var processedOperand = Process(operand);
			CriteriaOperator op;
			if(processedOperand is PropertyAlias) {
				PropertyAlias alias = (PropertyAlias)processedOperand;
				op = GetParameter(processedOperand, alias, 0);
				if(alias.Count > 1){
					AddMultiColumnAlias(op, alias);
				}
			} else {
				op = (CriteriaOperator)processedOperand;
			}
			if(op is OperandProperty || op is OperandValue) {
				return CreateBinary(BinaryOperatorType.Equal, processedOperand, new ConstantValue(true));
			}
			return op;
		}
		void AddMultiColumnAlias(CriteriaOperator op, PropertyAlias alias) {
			if(multiColumnAliases == null) multiColumnAliases = new Dictionary<CriteriaOperator, PropertyAlias>(ReferenceComparer.Instance);
			multiColumnAliases[op] = alias;
		}
		bool TryGetMultiColumnAlias(CriteriaOperator op, out PropertyAlias alias) {
			if(multiColumnAliases == null) {
				alias = null;
				return false;
			}
			return multiColumnAliases.TryGetValue(op, out alias);
		}
		CriteriaOperator ICriteriaVisitor<CriteriaOperator>.Visit(GroupOperator theOperator) {
			JoinType prevJoinType = currentJoinType;
			if(theOperator.OperatorType == currentLeftJoinEnforcer && theOperator.Operands.Count > 1)
				currentJoinType = JoinType.LeftOuter;
			try {
				CriteriaOperator result = null;
				foreach(CriteriaOperator op in theOperator.Operands) {
					CriteriaOperator processed = ProcessLogical(op);
					result = GroupOperator.Combine(theOperator.OperatorType, result, processed);
				}
				return result;
			} finally {
				currentJoinType = prevJoinType;
			}
		}
		protected List<XPMemberInfo> GetSubMembers(XPMemberInfo member, string prefix) {
			int count = member.SubMembers.Count;
			List<XPMemberInfo> list = new List<XPMemberInfo>(count);
			for(int i = 0; i < count; i++) {
				XPMemberInfo mi = (XPMemberInfo)member.SubMembers[i];
				if(mi.IsPersistent) {
					if(mi.ReferenceType != null && mi.ReferenceType.KeyProperty.SubMembers.Count > 0)
						throw new NotSupportedException(Xpo.Res.GetString("MetaData_ReferenceTooComplex", member.Owner.FullName, member.Name));
					list.Add(mi);
				}
			}
			return list;
		}
		protected List<XPMemberInfo> GetMembers(XPMemberInfo member, out string prefix) {
			prefix = String.Empty;
			XPMemberInfo actualMember;
			if(member.ReferenceType != null) {
				prefix = member.MappingField;
				actualMember = member.ReferenceType.KeyProperty;
			} else
				actualMember = member;
			if(actualMember.SubMembers.Count > 0) {
				return GetSubMembers(actualMember, prefix);
			} else {
				return null;
			}
		}
		protected PropertyAlias GetMembers(JoinNode node, XPMemberInfo member) {
			string prefix;
			List<XPMemberInfo> members = GetMembers(member, out prefix);
			if(members != null) {
				return new PropertyAlias(node, member, members, prefix);
			} else
				return new PropertyAlias(node, member, true);
		}
		CriteriaOperator ICriteriaVisitor<CriteriaOperator>.Visit(UnaryOperator theOperator) {
			GroupOperatorType prevLeftJoinEnforcer = currentLeftJoinEnforcer;
			JoinType prevJoinType = currentJoinType;
			try {
				if(theOperator.OperatorType == UnaryOperatorType.Not)
					currentLeftJoinEnforcer = (currentLeftJoinEnforcer == GroupOperatorType.Or) ? GroupOperatorType.And : GroupOperatorType.Or;
				if(theOperator.OperatorType == UnaryOperatorType.IsNull && currentLeftJoinEnforcer == GroupOperatorType.Or)
					currentJoinType = JoinType.LeftOuter;
				OperandProperty operandProperty = theOperator.Operand as OperandProperty;
				if(ReferenceEquals(operandProperty, null)) {
					if(theOperator.OperatorType == UnaryOperatorType.Not)
						return new UnaryOperator(UnaryOperatorType.Not, ProcessLogical(theOperator.Operand));
					else {
						var processedOperand = Process(theOperator.Operand);
						PropertyAlias alias = (processedOperand is PropertyAlias) ? (PropertyAlias)processedOperand : PropertyAlias.Empty;
						return new UnaryOperator(theOperator.OperatorType, GetParameter(processedOperand, alias, 0));
					}
				} else {
					PropertyAlias node = GetPropertyNode(operandProperty, currentJoinType);
					CriteriaOperator result = null;
					int count = node.Count;
					for(int i = 0; i < count; i++)
						result = GroupOperator.And(result, new UnaryOperator(theOperator.OperatorType, GetQueryOperandFromAlias(node, node , i)));
					return result;
				}
			} finally {
				currentLeftJoinEnforcer = prevLeftJoinEnforcer;
				currentJoinType = prevJoinType;
			}
		}
		CriteriaOperator ICriteriaVisitor<CriteriaOperator>.Visit(FunctionOperator theOperator) {
			JoinType prevJoinType = currentJoinType;
			try {
				FunctionOperatorType? newOperatorType = null;
				if(theOperator.OperatorType == FunctionOperatorType.Custom && theOperator.Operands.Count == 3) {
					string functionName = theOperator.Operands[0] is OperandValue ? ((OperandValue)theOperator.Operands[0]).Value as string : null;
					bool exactType = (functionName == IsExactTypeFunction.FunctionName);
					bool instanceOfType = (functionName == IsInstanceOfTypeFunction.FunctionName);
					if(exactType || instanceOfType) {
						string typeName = theOperator.Operands[2] is OperandValue ? ((OperandValue)theOperator.Operands[2]).Value as string : null;
						OperandProperty property = theOperator.Operands[1] as OperandProperty;
						if(string.IsNullOrEmpty(typeName) || ReferenceEquals(property, null)) throw new ArgumentException(Res.GetString(Res.Filtering_TheTypeNameArgumentOfTheX0FunctionIsNotFound, functionName));
						var propertyAlias = GetPropertyNode(property, currentJoinType);
						XPClassInfo searchedClassInfo;
						XPClassInfo objectClassInfo = propertyAlias.Member != null && propertyAlias.Member.ReferenceType != null ? propertyAlias.Member.ReferenceType : ClassInfo;
						if(objectClassInfo == null || !objectClassInfo.IsTypedObject
							|| !MemberInfoCollection.TryResolveTypeAlsoByShortName(typeName, objectClassInfo, out searchedClassInfo)
							|| searchedClassInfo == null || !searchedClassInfo.IsTypedObject) throw new ArgumentException(Res.GetString(Res.Filtering_TheTypeNameArgumentOfTheX0FunctionIsNotFound, functionName));
						XPObjectTypesManager TypesManager = collectionCriteriaPatcher.TypesPatchManager;
						string propertyStart = property.PropertyName;
						if(!string.IsNullOrEmpty(propertyStart)) {
							string[] parts = MemberInfoCollection.SplitPath(propertyStart);
							StringBuilder sb = new StringBuilder();
							for(int i = 0; i < parts.Length - 1; i++) {
								if(i > 0) {
									sb.Append('.');
								}
								sb.Append(parts[i]);
							}
							string propertyStartCut = sb.ToString();
							if(!propertyStart.EndsWith(propertyAlias.Member.Name) || propertyAlias.Member.ReferenceType == null
								|| !string.IsNullOrEmpty(propertyStartCut) && GetPropertyNode(new OperandProperty(propertyStartCut), currentJoinType).Member == propertyAlias.Member) {
								propertyStart = propertyStartCut;
							}
						}
						string propertyName = string.IsNullOrEmpty(propertyStart) ? XPObjectType.ObjectTypePropertyName : string.Concat(propertyStart, ".", XPObjectType.ObjectTypePropertyName);
						CriteriaOperator result = null;
						if(exactType) {
							result = new BinaryOperator(propertyName, TypesManager.GetObjectType(searchedClassInfo));
						} else {
							List<XPObjectType> types = new List<XPObjectType>();
							foreach(XPObjectType objectType in TypesManager.AllTypes.Values) {
								var typeClassInfo = objectType.TypeClassInfo;
								if(typeClassInfo != null && typeClassInfo.IsAssignableTo(searchedClassInfo)) {
									types.Add(objectType);
								}
							}
							switch(types.Count) {
								case 0:
									result = new BinaryOperator(propertyName, TypesManager.GetObjectType(searchedClassInfo));
									break;
								case 1:
									result = new BinaryOperator(propertyName, types[0]);
									break;
								default:
									result = new InOperator(propertyName, types);
									break;
							}
						}
						if(searchedClassInfo.IdClass == searchedClassInfo) {
							CriteriaOperator isObjectTypeNull = new UnaryOperator(UnaryOperatorType.IsNull, propertyName);
							if(!string.IsNullOrEmpty(propertyStart))
								isObjectTypeNull = GroupOperator.And(new UnaryOperator(UnaryOperatorType.Not, new UnaryOperator(UnaryOperatorType.IsNull, propertyStart)), isObjectTypeNull);
							result = GroupOperator.Or(result, isObjectTypeNull);
						}
						return Process(result);
					}
				} else if(IsFunctionOperatorNeedLeftJoin(theOperator.OperatorType)) {
					currentJoinType = JoinType.LeftOuter;
				} else if(theOperator.OperatorType == FunctionOperatorType.AddTimeSpan && !ConnectionProviderSql.UseLegacyTimeSpanSupport) {
					newOperatorType = FunctionOperatorType.AddSeconds;
				}
				CriteriaOperator[] processedOperands = new CriteriaOperator[theOperator.Operands.Count];
				for(int i = 0; i < theOperator.Operands.Count; ++i) {
					var processedOperand = Process((CriteriaOperator)theOperator.Operands[i]);
					PropertyAlias alias = (processedOperand is PropertyAlias) ? (PropertyAlias)processedOperand : PropertyAlias.Empty;
					processedOperands[i] = GetParameter(processedOperand, alias, 0);
				}
				return new FunctionOperator(newOperatorType ?? theOperator.OperatorType, processedOperands);
			} finally {
				currentJoinType = prevJoinType;
			}
		}
		static bool IsFunctionOperatorNeedLeftJoin(FunctionOperatorType operatorType) {
			switch(operatorType) {
				case FunctionOperatorType.IsNull:
				case FunctionOperatorType.IsNullOrEmpty:
				case FunctionOperatorType.Iif:
				case FunctionOperatorType.Contains:
				case FunctionOperatorType.PadLeft:
				case FunctionOperatorType.PadRight:
					return true;
			}
			return false;
		}
		protected CriteriaOperator PatchCriteria(CriteriaOperator originalCriteria) {
			return PatchCriteria(originalCriteria, ClassInfo);
		}
		protected CriteriaOperator PatchCriteria(CriteriaOperator originalCriteria, XPClassInfo classInfo) {
			if(collectionCriteriaPatcher == null)
				return originalCriteria;
			return collectionCriteriaPatcher.PatchCriteria(classInfo, originalCriteria);
		}
	}
	public class SubSelectQueryGenerator : BaseQueryGenerator {
		readonly string[] propertyName;
		readonly BaseQueryGenerator parent;
		readonly Aggregate aggregateType;
		readonly CriteriaOperator aggregateProperty;
		readonly CriteriaOperatorCollection customAggregateOperands = new CriteriaOperatorCollection();
		protected override void InternalGenerateSql(CriteriaOperator criteria) {
			CriteriaOperator patchedCriteria = PatchCriteria(criteria);
			BuildAssociationTree(patchedCriteria);
			AddSelectValue();
		}
		void AddSelectValue() {
			if(aggregateType != Aggregate.Custom) {
				if(!ReferenceEquals(aggregateProperty, null)) {
					object res = Process(aggregateProperty);
					if(res is PropertyAlias) {
						PropertyAlias param = (PropertyAlias)res;
						Root.Operands.Add(GetQueryOperandFromAlias(param, param, 0));
					} else {
						Root.Operands.Add((CriteriaOperator)res);
					}
				}
			} else {
				foreach(CriteriaOperator op in customAggregateOperands) {
					object res = Process(op);
					if(res is PropertyAlias) {
						PropertyAlias param = (PropertyAlias)res;
						Root.Operands.Add(GetQueryOperandFromAlias(param, param, 0));
					} else {
						Root.Operands.Add((CriteriaOperator)res);
					}
				}
			}
		}
		protected internal override string GetNextNodeAlias() {
			return parent.GetNextNodeAlias();
		}
		protected internal override PropertyAlias GetPropertyNode(OperandProperty property, JoinType type) {
			return (PropertyAlias)ExecuteWithPropertyNameDiving(property.PropertyName, (propertyPath) => {
				if(propertyPath.StartsWith("^.")) {
					int length = propertyName.Length;
					int i = propertyPath.LastIndexOf("^.") / 2 + 1;
					if(i >= length) {
						propertyPath = propertyPath.Remove(0, length * 2);
					} else {
						propertyPath = propertyPath.Remove(0, i * 2 - 1);
						propertyPath = string.Join(".", propertyName, 0, length - i) + propertyPath;
					}
					return parent.GetPropertyNode(new OperandProperty(propertyPath), type);
				} else
					return base.GetPropertyNode(new OperandProperty(propertyPath), type);
			}, false);
		}	   
		public SubSelectQueryGenerator(BaseQueryGenerator parent, BatchWideDataHolder batchWideData, string propertyName, XPClassInfo objectInfo, CriteriaOperator aggregateProperty, Aggregate aggregate, CollectionCriteriaPatcher collectionCriteriaPatcher)
			: base(objectInfo, batchWideData, collectionCriteriaPatcher) {
			if(aggregate == Aggregate.Custom) {
				throw new ArgumentException(null, nameof(aggregate));
			}
			this.aggregateType = aggregate;
			this.aggregateProperty = aggregateProperty;
			this.propertyName = MemberInfoCollection.SplitPath(propertyName);
			this.parent = parent;
		}
		public SubSelectQueryGenerator(BaseQueryGenerator parent, BatchWideDataHolder batchWideData, string propertyName, XPClassInfo objectInfo, IEnumerable<CriteriaOperator> aggregatedExpressions, string customAggregateName, CollectionCriteriaPatcher collectionCriteriaPatcher)
			: base(objectInfo, batchWideData, collectionCriteriaPatcher) {
			if(string.IsNullOrEmpty(customAggregateName)) {
				throw new ArgumentNullException(nameof(customAggregateName));
			}
			this.aggregateType = Aggregate.Custom;
			this.customAggregateOperands.AddRange(aggregatedExpressions);
			this.propertyName = MemberInfoCollection.SplitPath(propertyName);
			this.parent = parent;
		}
		public BaseStatement GenerateSelect(CriteriaOperator criteria) {
			return GenerateSql(criteria);
		}
		protected override BaseStatement CreateRootStatement(DBTable table, string alias) {
			return new SelectStatement(table, alias);
		}
	}
	public class ManySubSelectQueryGenerator : SubSelectQueryGenerator {
		readonly XPMemberInfo refProperty;
		protected internal override PropertyAlias GetPropertyNode(OperandProperty property, JoinType type) {
			return (PropertyAlias)ExecuteWithPropertyNameDiving(property.PropertyName, (pn) => {
				if(ClassInfoStack.Count == 0) {
					OperandProperty resultProperty = null;
					string propertyPath = pn;
					if(propertyPath == "!")
						resultProperty = new OperandProperty(refProperty.GetAssociatedMember().Name);
					else
						if(!propertyPath.StartsWith("^."))
							resultProperty = new OperandProperty(string.Concat(refProperty.Name, '.', propertyPath));
					if(ReferenceEquals(resultProperty, null))
						resultProperty = new OperandProperty(propertyPath);
					return base.GetPropertyNode(resultProperty, type);
				}
				return base.GetPropertyNode(new OperandProperty(pn), type);
			}, false);
		}
		public string GetManyToManyPath(IntermediateClassInfo classInfo, string memberName) {
			XPMemberInfo member = null;
			if(classInfo.intermediateObjectFieldInfoLeft.Name == refProperty.Name) {
				member = classInfo.intermediateObjectFieldInfoLeft.refProperty;
			} else {
				if(classInfo.intermediateObjectFieldInfoRight.Name == refProperty.Name) {
					member = classInfo.intermediateObjectFieldInfoRight.refProperty;
				}
			}
			return member == null ? memberName : string.Concat(member.Name, '.', memberName);
		}
		public ManySubSelectQueryGenerator(XPMemberInfo refProperty, BaseQueryGenerator parent, BatchWideDataHolder batchWideData, string propertyName, CriteriaOperator aggregateProperty, Aggregate aggregate, CollectionCriteriaPatcher collectionCriteriaPatcher)
			: base(parent, batchWideData, propertyName, refProperty.IntermediateClass, aggregateProperty, aggregate, collectionCriteriaPatcher) {
			this.refProperty = refProperty;
		}
		public ManySubSelectQueryGenerator(XPMemberInfo refProperty, BaseQueryGenerator parent, BatchWideDataHolder batchWideData, string propertyName, IEnumerable<CriteriaOperator> aggregatedExpressions, string customAggregateName, CollectionCriteriaPatcher collectionCriteriaPatcher)
			: base(parent, batchWideData, propertyName, refProperty.IntermediateClass, aggregatedExpressions, customAggregateName, collectionCriteriaPatcher) {
			this.refProperty = refProperty;
		}
	}
	class MemberPathOperand : OperandProperty {
		MemberInfoCollection mic;
		public MemberPathOperand(MemberInfoCollection path) {
			this.mic = path;
		}
		public MemberInfoCollection Path { get { return mic; } }
	}
	public class ClientSelectSqlGenerator : BaseQueryGenerator {
		CriteriaOperatorCollection grouping;
		CriteriaOperator groupCriteria;
		CriteriaOperatorCollection properties;
		CriteriaOperatorCollection Properties {
			get {
				return properties;
			}
		}
		SortingCollection sorting;
		new protected SelectStatement Root { get { return (SelectStatement)base.Root; } }
		protected override BaseStatement CreateRootStatement(DBTable table, string alias) {
			return new SelectStatement(table, alias);
		}
		void BuildAssociationByGrouping() {
			if(grouping == null)
				return;
			Root.GroupProperties.AddRange(BuildAssociation(grouping));
		}
		protected override bool IsGrouped {
			get {
				return Root.GroupProperties != null && Root.GroupProperties.Count > 0;
			}
		}
		protected override CriteriaOperator GetSubJoinCriteria(SubSelectQueryGenerator gena) {
			if(IsGrouped) {
				CriteriaOperator[] criteria = new CriteriaOperator[grouping.Count];
				for(int i = 0; i < grouping.Count; i++)
					criteria[i] = CreateBinary(BinaryOperatorType.Equal, Root.GroupProperties[i], grouping[i].Accept(gena));
				return GroupOperator.And(criteria);
			} else {
				return base.GetSubJoinCriteria(gena);
			}
		}
		List<CriteriaOperator> BuildAssociation(CriteriaOperatorCollection properties) {
			List<CriteriaOperator> props = new List<CriteriaOperator>(properties.Count);
			System.Diagnostics.Debug.Assert(currentJoinType == JoinType.Inner);
			currentJoinType = JoinType.LeftOuter;
			try {
				foreach(CriteriaOperator mic in properties) {
					object res = Process(mic);
					if(res is PropertyAlias) {
						PropertyAlias node = (PropertyAlias)res;
						int count = node.Count;
						for(int i = 0; i < count; i++)
							props.Add(GetQueryOperandFromAlias(node, node, i));
					} else {
						props.Add((CriteriaOperator)res);
					}
				}
			} finally {
				currentJoinType = JoinType.Inner;
			}
			return props;
		}
		void BuildAssociationByGroupCriteria() {
			Root.GroupCondition = ProcessLogical(groupCriteria);
		}
		void BuildAssociationBySorting() {
			if(sorting == null || sorting.Count == 0)
				return;
			System.Diagnostics.Debug.Assert(currentJoinType == JoinType.Inner);
			currentJoinType = JoinType.LeftOuter;
			try {
				foreach(SortProperty sp in sorting) {
					object res = Process(sp.Property);
					if(res is PropertyAlias) {
						PropertyAlias node = (PropertyAlias)res;
						int count = node.Count;
						for(int i = 0; i < count; i++)
							Root.SortProperties.Add(new SortingColumn(GetQueryOperandFromAlias(node, node, i), sp.Direction));
					} else {
						Root.SortProperties.Add(new SortingColumn((CriteriaOperator)res, sp.Direction));
					}
				}
			} finally {
				currentJoinType = JoinType.Inner;
			}
		}
		void BuildAssociationByProperties(CriteriaOperatorCollection properties) {
			if(properties == null || properties.Count == 0)
				return;
			Root.Operands.AddRange(BuildAssociation(properties));
		}
		protected override void InternalGenerateSql(CriteriaOperator criteria) {
			BuildAssociationByProperties(Properties);
			CriteriaOperator patchedCriteria = PatchCriteria(criteria);
			BuildAssociationTree(patchedCriteria);
			BuildAssociationByGrouping();
			BuildAssociationByGroupCriteria();
			BuildAssociationBySorting();
		}
		ClientSelectSqlGenerator(XPClassInfo objectInfo, BatchWideDataHolder4Select batchWideData, CriteriaOperatorCollection properties, SortingCollection sorting, CriteriaOperatorCollection grouping, CriteriaOperator groupCriteria, CollectionCriteriaPatcher collectionCriteriaPatcher)
			: base(objectInfo, batchWideData, collectionCriteriaPatcher) {
			this.sorting = sorting;
			this.properties = properties;
			this.grouping = grouping;
			this.groupCriteria = groupCriteria;
		}
		public static SelectStatement GenerateSelect(XPClassInfo objectInfo, CriteriaOperator criteria, CriteriaOperatorCollection properties, SortingCollection sorting, CriteriaOperatorCollection grouping, CriteriaOperator groupCriteria, CollectionCriteriaPatcher collectionCriteriaPatcher, int topSelectedRecords) {
			return GenerateSelect(objectInfo, criteria, properties, sorting, grouping, groupCriteria, collectionCriteriaPatcher, 0, topSelectedRecords);
		}
		public static SelectStatement GenerateSelect(XPClassInfo objectInfo, CriteriaOperator criteria, CriteriaOperatorCollection properties, SortingCollection sorting, CriteriaOperatorCollection grouping, CriteriaOperator groupCriteria, CollectionCriteriaPatcher collectionCriteriaPatcher, int skipSelectedRecords, int topSelectedRecords) {
			SelectStatement result = (SelectStatement)new ClientSelectSqlGenerator(objectInfo, new BatchWideDataHolder4Select(objectInfo.Dictionary), properties, sorting, grouping, groupCriteria, collectionCriteriaPatcher).GenerateSql(criteria);
			result.SkipSelectedRecords = skipSelectedRecords;
			result.TopSelectedRecords = topSelectedRecords;
			return result;
		}
		public static SelectStatement GenerateSelect(XPClassInfo objectInfo, CriteriaOperator criteria, MemberPathCollection properties, SortingCollection sorting, CriteriaOperatorCollection grouping, CriteriaOperator groupCriteria, CollectionCriteriaPatcher collectionCriteriaPatcher, int topSelectedRecords) {
			return GenerateSelect(objectInfo, criteria, properties, sorting, grouping, groupCriteria, collectionCriteriaPatcher, 0, topSelectedRecords);
		}
		public static SelectStatement GenerateSelect(XPClassInfo objectInfo, CriteriaOperator criteria, MemberPathCollection properties, SortingCollection sorting, CriteriaOperatorCollection grouping, CriteriaOperator groupCriteria, CollectionCriteriaPatcher collectionCriteriaPatcher, int skipSelectedRecords, int topSelectedRecords) {
			CriteriaOperatorCollection props = new CriteriaOperatorCollection(properties.Count);
			foreach(MemberInfoCollection mic in properties) {
				props.Add(new MemberPathOperand(mic));
			}
			return GenerateSelect(objectInfo, criteria, props, sorting, grouping, groupCriteria, collectionCriteriaPatcher, skipSelectedRecords, topSelectedRecords);
		}
	}
	public abstract class BaseObjectQueryGenerator : BaseQueryGenerator {
		protected new BatchWideDataHolder4Modification BatchWideData { get { return (BatchWideDataHolder4Modification)base.BatchWideData; } }
		protected object theObject;
		protected List<ModificationStatement> GenerateSql(ObjectGeneratorCriteriaSet criteriaSet, MemberInfoCollection properties) {
			return GenerateSql(criteriaSet, properties, true);
		}
		protected MemberInfoCollection properties;
		protected List<ModificationStatement> GenerateSql(ObjectGeneratorCriteriaSet criteriaSet, MemberInfoCollection properties, bool reverse) {
			if(properties == null) {
				properties = new MemberInfoCollection(ClassInfo);
				foreach(XPMemberInfo m in ClassInfo.PersistentProperties)
					properties.Add(m);
			}
			this.properties = properties;
			List<XPClassInfo> list = GetClasses();
			if(reverse)
				list.Reverse();
			List<ModificationStatement> result = new List<ModificationStatement>(list.Count);
			for(int i = 0; i < list.Count; i++) {
				ClassInfo = list[i];
				CriteriaOperator criteria = criteriaSet == null ? null : criteriaSet.GetCompleteCriteria(ClassInfo.TableName);
				result.Add((ModificationStatement)base.GenerateSql(criteria));
			}
			return result;
		}
		protected List<XPClassInfo> GetClasses() {
			List<XPClassInfo> classes = new List<XPClassInfo>(4);
			XPClassInfo t = ClassInfo;
			XPClassInfo target = t;
			do {
				if(t.IsPersistent && t.TableMapType == MapInheritanceType.OwnTable) {
					classes.Add(target);
					target = t.BaseClass;
					while(target != null && !target.IsPersistent)
						target = target.BaseClass;
				}
				t = t.BaseClass;
			}
			while(t != null);
			return classes;
		}
		OperandValue GetMemberParameter(XPMemberInfo member, object theObject) {
			if(member.ReferenceType != null) {
				object memberValue = theObject != null ? member.GetValue(theObject) : null;
				return GetMemberParameter(member.ReferenceType.KeyProperty, memberValue);
			}
			if(theObject == null) {
				return GetConstParameter(null, member.MappingFieldDBType, member.MappingFieldDBTypeName, member.MappingFieldSize);
			}
			object value = member.IsKey ? theObject : member.GetValue(theObject);
			ValueConverter converter = member.Converter;
			if(converter != null) {
				value = converter.ConvertToStorageType(value);
			} else {
				ConvertViaDefaultValueConverter(ref value);
			}
			return GetConstParameter(value, member.MappingFieldDBType, member.MappingFieldDBTypeName, member.MappingFieldSize);
		}
		protected virtual bool ShoudPersist(XPMemberInfo member) {
			return member.IsMappingClass(ClassInfo);
		}
		protected virtual void AddParameter(OperandValue parameter) {
		}
		protected void BuildFieldList() {
			int propCount = properties.Count;
			for(int i = 0; i < propCount; i++) {
				XPMemberInfo p = properties[i];
				if(!ShoudPersist(p))
					continue;
				string prefix;
				List<XPMemberInfo> members = GetMembers(p, out prefix);
				if(members == null) {
					Root.Operands.Add(BatchWideData.CacheQueryOperand(new QueryOperand(Root.GetColumn(p.MappingField), null)));
					AddParameter(GetMemberParameter(p, theObject));
				} else {
					object obj = p.ReferenceType == null || members[0] == p ? theObject : p.GetValue(theObject);
					int memberCount = members.Count;
					for(int j = 0; j < memberCount; j++) {
						XPMemberInfo member = (XPMemberInfo)members[j];
						Root.Operands.Add(BatchWideData.CacheQueryOperand(new QueryOperand(Root.GetColumn(prefix + member.MappingField), null)));
						AddParameter(GetMemberParameter(member, obj));
					}
				}
			}
		}
		protected static CriteriaOperator BuildKeyCriteria(XPDictionary dictionary, object theObject) {
			return new BinaryOperator(new OperandProperty(dictionary.GetClassInfo(theObject).KeyProperty.Name), new OperandValue(theObject), BinaryOperatorType.Equal);
		}
		protected override void InitData() {
			base.InitData();
			Root.Alias = null;
		}
		protected BaseObjectQueryGenerator(XPDictionary dictionary, BatchWideDataHolder4Modification batchWideData, object theObject)
			: this(dictionary.GetClassInfo(theObject), batchWideData) {
			this.theObject = theObject;
		}
		protected BaseObjectQueryGenerator(XPClassInfo classInfo, BatchWideDataHolder4Modification batchWideData)
			: base(classInfo, batchWideData) {
		}
	}
	public class InsertQueryGenerator : BaseObjectQueryGenerator {
		bool autoIncrement;
		new protected InsertStatement Root { get { return (InsertStatement)base.Root; } }
		protected override void InitData() {
			base.InitData();
			XPMemberInfo key = ClassInfo.KeyProperty;
			if(ClassInfo.TableName == ClassInfo.IdClass.TableName && key.IsIdentity) {
				Root.IdentityParameter = BatchWideData.CreateIdentityParameter(theObject);
				Root.IdentityColumn = key.MappingField;
				Root.IdentityColumnType = key.MemberType == typeof(Int32) ? DBColumnType.Int32 : DBColumnType.Int64;
				autoIncrement = true;
			} else
				autoIncrement = false;
		}
		protected override void AddParameter(OperandValue parameter) {
			((InsertStatement)Root).Parameters.Add(parameter);
		}
		protected override bool ShoudPersist(XPMemberInfo member) {
			if(autoIncrement && member.IsKey)
				return false;
			return member.IsMappingClass(ClassInfo);
		}
		protected override void InternalGenerateSql(CriteriaOperator criteria) {
			BuildFieldList();
		}
		InsertQueryGenerator(XPDictionary dictionary, BatchWideDataHolder4Modification batchWideData, object theObject) : base(dictionary, batchWideData, theObject) { }
		[Obsolete("Use overload with BatchWideDataHolder instead")]
		public static List<ModificationStatement> GenerateInsert(XPDictionary dictionary, object theObject, MemberInfoCollection properties) {
			return GenerateInsert(dictionary, new BatchWideDataHolder4Modification(dictionary), theObject, properties);
		}
		public static List<ModificationStatement> GenerateInsert(XPDictionary dictionary, BatchWideDataHolder4Modification batchWideData, object theObject, MemberInfoCollection properties) {
			return new InsertQueryGenerator(dictionary, batchWideData, theObject).GenerateSql(null, properties);
		}
		protected override BaseStatement CreateRootStatement(DBTable table, string alias) {
			return new InsertStatement(table, alias);
		}
	}
	public class UpdateQueryGenerator : BaseObjectQueryGenerator {
		new protected UpdateStatement Root { get { return (UpdateStatement)base.Root; } }
		protected override void InitData() {
			base.InitData();
			Root.RecordsAffected = 1;
		}
		protected override void AddParameter(OperandValue parameter) {
			((UpdateStatement)Root).Parameters.Add(parameter);
		}
		protected override void InternalGenerateSql(CriteriaOperator criteria) {
			properties.Remove(ClassInfo.KeyProperty);
			BuildFieldList();
			BuildAssociationTree(criteria);
		}
		UpdateQueryGenerator(XPDictionary dictionary, BatchWideDataHolder4Modification batchWideData, object theObject)
			: base(dictionary, batchWideData, theObject) {
		}
		UpdateQueryGenerator(XPClassInfo classInfo, BatchWideDataHolder4Modification batchWideData)
			: base(classInfo, batchWideData) {
		}
		public static List<ModificationStatement> GenerateUpdate(XPDictionary dictionary, BatchWideDataHolder4Modification batchWideData, object theObject, MemberInfoCollection properties, ObjectGeneratorCriteriaSet criteriaSet) {
			CriteriaOperator keyCriteria = BuildKeyCriteria(dictionary, theObject);
			if(criteriaSet == null) {
				criteriaSet = ObjectGeneratorCriteriaSet.GetCommonCriteriaSet(keyCriteria);
			} else {
				criteriaSet.UpdateCommonCriteria(keyCriteria);
			}
			return new UpdateQueryGenerator(dictionary, batchWideData, theObject).GenerateSql(criteriaSet, properties);
		}
		[Obsolete("Use overload with BatchWideDataHolder instead")]
		public static List<ModificationStatement> GenerateUpdate(XPDictionary dictionary, object theObject, MemberInfoCollection properties, ObjectGeneratorCriteriaSet criteriaSet) {
			return GenerateUpdate(dictionary, new BatchWideDataHolder4Modification(dictionary), theObject, properties, criteriaSet);
		}
		public static List<ModificationStatement> GenerateUpdate(XPClassInfo classInfo, MemberInfoCollection properties, ObjectGeneratorCriteriaSet criteriaSet, BatchWideDataHolder4Modification batchWideData) {
			return new UpdateQueryGenerator(classInfo, batchWideData).GenerateSql(criteriaSet, properties);
		}
		public static List<ModificationStatement> GenerateUpdate(XPDictionary dictionary, BatchWideDataHolder4Modification batchWideData, object theObject, MemberInfoCollection properties) {
			return GenerateUpdate(dictionary, batchWideData, theObject, properties, null);
		}
		protected override BaseStatement CreateRootStatement(DBTable table, string alias) {
			return new UpdateStatement(table, alias);
		}
	}
	public class DeleteQueryGenerator : BaseObjectQueryGenerator {
		new protected DeleteStatement Root { get { return (DeleteStatement)base.Root; } }
		protected override void InitData() {
			base.InitData();
			Root.RecordsAffected = 1;
		}
		protected override void InternalGenerateSql(CriteriaOperator criteria) {
			BuildAssociationTree(criteria);
		}
		DeleteQueryGenerator(XPDictionary dictionary, object theObject, BatchWideDataHolder4Modification batchWideData)
			: base(dictionary, batchWideData, theObject) {
		}
		DeleteQueryGenerator(XPClassInfo classInfo, BatchWideDataHolder4Modification batchWideData) : base(classInfo, batchWideData) { }
		public static List<ModificationStatement> GenerateDelete(XPDictionary dictionary, object theObject, LockingOption locking, BatchWideDataHolder4Modification batchWideData) {
			XPClassInfo ci = dictionary.GetClassInfo(theObject);
			ObjectGeneratorCriteriaSet criteriaSet = null;
			XPMemberInfo optimisticLockInDataLayer = ci.OptimisticLockFieldInDataLayer;
			if (locking == LockingOption.Optimistic) {
				switch(ci.OptimisticLockingBehavior){
					case OptimisticLockingBehavior.ConsiderOptimisticLockingField:
						if(optimisticLockInDataLayer != null)
							criteriaSet = ObjectGeneratorCriteriaSet.GetCriteriaSet(ci.IdClass.TableName, LockingHelper.GetLockingCriteria((int?)optimisticLockInDataLayer.GetValue(theObject), ci.OptimisticLockField));
						break;
					case OptimisticLockingBehavior.LockModified:
					case OptimisticLockingBehavior.LockAll: {
							MemberInfoCollection updatedMembers;
							if (!batchWideData.TryGetUpdatedMembersBeforeDelete(theObject, out updatedMembers)) {
								updatedMembers = null;
							}
							criteriaSet = LockingHelper.GetLockingCriteria(ci, ci.PersistentProperties, theObject, OptimisticLockingBehavior.LockAll, updatedMembers);
							break;
						}
				}
			}
			if(criteriaSet == null) {
				criteriaSet = ObjectGeneratorCriteriaSet.GetCommonCriteriaSet(BuildKeyCriteria(dictionary, theObject));
			} else {
				criteriaSet.UpdateCommonCriteria(BuildKeyCriteria(dictionary, theObject));
			}
			return new DeleteQueryGenerator(dictionary, theObject, batchWideData).GenerateSql(criteriaSet, null, false);
		}
		static void GenerateDeletes(XPClassInfo classInfo, ObjectGeneratorCriteriaSet criteriaSet, List<ModificationStatement> res, int count, BatchWideDataHolder4Modification batchWideData) {
			List<ModificationStatement> deletes = new DeleteQueryGenerator(classInfo, batchWideData).GenerateSql(criteriaSet, null, false);
			foreach(DeleteStatement statement in deletes)
				statement.RecordsAffected = count;
			res.AddRange(deletes);
		}
		static List<ModificationStatement> GenerateDelete(XPClassInfo classInfo, IList keys, LockingOption locking, BatchWideDataHolder4Modification batchWideData) {
			List<ModificationStatement> rv = new List<ModificationStatement>();
			for(int startPos = 0; startPos < keys.Count; ) {
				int cntLeft = keys.Count - startPos;
				int inSize = XpoDefault.GetTerminalInSize(cntLeft, classInfo.KeyProperty.SubMembers.Count);
				var deletes = GenerateDeleteCore(classInfo, GetRangeHelper.GetRange(keys, startPos, inSize), locking, batchWideData);
				rv.AddRange(deletes);
				startPos += inSize;
			}
			return rv;
		}
		static List<ModificationStatement> GenerateDeleteCore(XPClassInfo classInfo, IList keys, LockingOption locking, BatchWideDataHolder4Modification batchWideData) {
			if(keys.Count == 1)
				return GenerateDelete(classInfo.Dictionary, keys[0], locking, batchWideData);
			else {
				List<ModificationStatement> res = new List<ModificationStatement>();
				XPMemberInfo optimisticLockInDl = classInfo.OptimisticLockFieldInDataLayer;
				OptimisticLockingBehavior kind = classInfo.OptimisticLockingBehavior;
				if(locking != LockingOption.None && (kind == OptimisticLockingBehavior.LockModified || kind == OptimisticLockingBehavior.LockAll)) {
					for(int i = 0; i < keys.Count; i++) {
						object key = keys[i];
						MemberInfoCollection updatedMembers;
						if(!batchWideData.TryGetUpdatedMembersBeforeDelete(key, out updatedMembers)) {
							updatedMembers = null;
						}
						var criteriaSet = LockingHelper.GetLockingCriteria(classInfo, classInfo.PersistentProperties, key, OptimisticLockingBehavior.LockAll, updatedMembers);
						criteriaSet.UpdateCommonCriteria(new OperandProperty(classInfo.KeyProperty.Name) == new OperandValue(key));
						GenerateDeletes(classInfo, criteriaSet, res, 1, batchWideData);
					}
				} else if(locking == LockingOption.None || optimisticLockInDl == null) {
					CriteriaOperator criteria = new InOperator(classInfo.KeyProperty.Name, keys);
					List<ModificationStatement> deletes = new DeleteQueryGenerator(classInfo, batchWideData).GenerateSql(ObjectGeneratorCriteriaSet.GetCommonCriteriaSet(criteria), null, false);
					foreach(DeleteStatement statement in deletes)
						statement.RecordsAffected = keys.Count;
					res.AddRange(deletes);
				} else {
					Dictionary<object, List<object>> locks = new Dictionary<object, List<object>>();
					for(int i = 0; i < keys.Count; i++) {
						object lockValue = optimisticLockInDl.GetValue(keys[i]);
						if(lockValue == null)
							lockValue = DBNull.Value;
						List<object> ids;
						if(!locks.TryGetValue(lockValue, out ids)) {
							ids = new List<object>();
							locks.Add(lockValue, ids);
						}
						ids.Add(keys[i]);
					}
					foreach(KeyValuePair<object, List<object>> entry in locks) {
						int? version = entry.Key is DBNull ? null : (int?)entry.Key;
						GenerateDeletes(classInfo,
							ObjectGeneratorCriteriaSet.GetCriteriaSet(classInfo.IdClass.TableName,
							LockingHelper.GetLockingCriteria(version, classInfo.OptimisticLockField),
							new InOperator(classInfo.KeyProperty.Name, entry.Value)), res, entry.Value.Count, batchWideData);
					}
				}
				return res;
			}
		}
		static bool AreReferences(XPClassInfo ci, ICollection classInfos) {
			foreach(XPClassInfo testCi in classInfos) {
				foreach(XPMemberInfo mi in testCi.ObjectProperties) {
					if(ci.IsAssignableTo(mi.ReferenceType))
						return true;
				}
			}
			return false;
		}
		static XPClassInfo GetNonReferencedClassInfo(ICollection classInfos) {
			foreach(XPClassInfo ci in classInfos) {
				if(!AreReferences(ci, classInfos))
					return ci;
			}
			return null;
		}
		class RefHolder {
			public readonly IDictionary Pool;
			public readonly XPClassInfo Me;
			public readonly List<object> Objects = new List<object>();
			bool canLoop = true;
			public Dictionary<XPClassInfo, RefHolder> DirectlyAssignableNotDeleted;
			public static RefHolder GetRefHolder(IDictionary pool, XPClassInfo ci) {
				RefHolder holder = (RefHolder)pool[ci];
				return holder;
			}
			public static ICollection GetNonReferenced(IDictionary pool) {
				Dictionary<XPClassInfo, XPClassInfo> buff = new Dictionary<XPClassInfo, XPClassInfo>(pool.Count);
				foreach(XPClassInfo ci in pool.Keys) {
					buff.Add(ci, ci);
				}
				foreach(RefHolder rh in pool.Values) {
					foreach(XPClassInfo ci in rh.DirectlyAssignableNotDeleted.Keys) {
						buff.Remove(ci);
					}
				}
				return buff.Keys;
			}
			public bool CanReach(XPClassInfo target) {
				return CanReach(new Dictionary<XPClassInfo, XPClassInfo>(Pool.Count), target);
			}
			bool CanReach(Dictionary<XPClassInfo, XPClassInfo> processed, XPClassInfo target) {
				if(processed.ContainsKey(Me))
					return false;
				processed.Add(Me, Me);
				foreach(RefHolder rh in DirectlyAssignableNotDeleted.Values) {
					if(rh.Me == target)
						return true;
					if(rh.CanReach(processed, target))
						return true;
				}
				return false;
			}
			public bool IsInLoop() {
				if(!canLoop)
					return false;
				canLoop = CanReach(Me);
				return canLoop;
			}
			void FillDirectlyRefs() {
				DirectlyAssignableNotDeleted = new Dictionary<XPClassInfo, RefHolder>();
				foreach(XPMemberInfo mi in Me.ObjectProperties) {
					if(mi.HasAttribute(typeof(NoForeignKeyAttribute)))
						continue;
					foreach(RefHolder rh in Pool.Values) {
						if(rh.Me.IsAssignableTo(mi.ReferenceType)) {
							DirectlyAssignableNotDeleted[rh.Me] = rh;
						}
					}
				}
			}
			public RefHolder(IDictionary pool, XPClassInfo me) {
				this.Pool = pool;
				this.Me = me;
			}
			public static IDictionary CreatePool(XPDictionary dictionary, ICollection objects) {
				Dictionary<XPClassInfo, RefHolder> pool = new Dictionary<XPClassInfo, RefHolder>();
				foreach(object obj in objects) {
					XPClassInfo ci = dictionary.GetClassInfo(obj);
					RefHolder holder;
					if(!pool.TryGetValue(ci, out holder)) {
						holder = new RefHolder(pool, ci);
						pool.Add(ci, holder);
					}
					holder.Objects.Add(obj);
				}
				foreach(RefHolder rh in pool.Values) {
					rh.FillDirectlyRefs();
				}
				return pool;
			}
			public static void ProcessDelete(IDictionary pool, XPClassInfo ci) {
				pool.Remove(ci);
				foreach(RefHolder rh in pool.Values)
					rh.DirectlyAssignableNotDeleted.Remove(ci);
			}
			static List<ModificationStatement> GenerateUpdatesBeforeDelete(XPClassInfo classInfo, MemberInfoCollection updateList, IList keys, BatchWideDataHolder4Modification batchWideData, LockingOption locking) {
				List<ModificationStatement> res = new List<ModificationStatement>();
				if(keys.Count == 0 || updateList.Count == 0)
					return res;
				int inSize = XpoDefault.GetTerminalInSize(keys.Count, classInfo.KeyProperty.SubMembers.Count);
				if(keys.Count != inSize) {
					res.AddRange(GenerateUpdatesBeforeDelete(classInfo, updateList, GetRangeHelper.GetRange(keys, 0, inSize), batchWideData, locking));
					res.AddRange(GenerateUpdatesBeforeDelete(classInfo, updateList, GetRangeHelper.GetRange(keys, inSize, keys.Count - inSize), batchWideData, locking));
				} else {
					OptimisticLockingBehavior kind = classInfo.OptimisticLockingBehavior;
					if (locking == LockingOption.None || kind == OptimisticLockingBehavior.ConsiderOptimisticLockingField || kind == OptimisticLockingBehavior.NoLocking) {
						CriteriaOperator criteria = new InOperator(classInfo.KeyProperty.Name, keys);
						List<ModificationStatement> updates = UpdateQueryGenerator.GenerateUpdate(classInfo, updateList, ObjectGeneratorCriteriaSet.GetCommonCriteriaSet(criteria), batchWideData);
						foreach (UpdateStatement statement in updates)
							statement.RecordsAffected = keys.Count;
						res.AddRange(updates);
					} else {
						foreach (object key in keys) {
							var criteriaSet = LockingHelper.GetLockingCriteria(classInfo, updateList, key, OptimisticLockingBehavior.LockAll);
							CriteriaOperator keyCriteria = new OperandProperty(classInfo.KeyProperty.Name) == new OperandValue(key);
							criteriaSet.UpdateCommonCriteria(keyCriteria);
							List<ModificationStatement> updates = UpdateQueryGenerator.GenerateUpdate(classInfo, updateList, criteriaSet, batchWideData);
							foreach (UpdateStatement statement in updates)
								statement.RecordsAffected = 1;
							res.AddRange(updates);
							batchWideData.RegisterUpdatedMembersBeforeDelete(key, updateList);
						}
					}
				}
				return res;
			}
			public ICollection<ModificationStatement> DoUpdates(LockingOption locking, BatchWideDataHolder4Modification batchWideData) {
				List<XPClassInfo> refLoopClasses = new List<XPClassInfo>(DirectlyAssignableNotDeleted.Count);
				foreach(RefHolder rh in DirectlyAssignableNotDeleted.Values) {
					if(rh.CanReach(Me))
						refLoopClasses.Add(rh.Me);
				}
				MemberInfoCollection updateList = new MemberInfoCollection(Me);
				foreach(XPMemberInfo mi in Me.ObjectProperties) {
					if(mi.HasAttribute(typeof(NoForeignKeyAttribute)))
						continue;
					foreach(XPClassInfo ci in refLoopClasses) {
						if(ci.IsAssignableTo(mi.ReferenceType)) {
							updateList.Add(mi);
							break;
						}
					}
				}
				foreach(XPClassInfo ci in refLoopClasses) {
					DirectlyAssignableNotDeleted.Remove(ci);
				}
				System.Diagnostics.Debug.Assert(Objects.Count > 0);
				System.Diagnostics.Debug.Assert(updateList.Count > 0);
				return GenerateUpdatesBeforeDelete(Me, updateList, Objects, batchWideData, locking);
			}
		}
		public static List<ModificationStatement> GenerateDelete(XPDictionary dictionary, ICollection objects, LockingOption locking, BatchWideDataHolder4Modification batchWideData) {
			List<ModificationStatement> sql = new List<ModificationStatement>();
			IDictionary pool = RefHolder.CreatePool(dictionary, objects);
			while(pool.Count > 0) {
				ICollection nonReferenced = RefHolder.GetNonReferenced(pool);
				if(nonReferenced.Count > 0) {
					foreach(XPClassInfo ci in nonReferenced) {
						sql.AddRange(GenerateDelete(ci, RefHolder.GetRefHolder(pool, ci).Objects, locking, batchWideData));
						RefHolder.ProcessDelete(pool, ci);
					}
				} else {
					RefHolder victim = null;
					foreach(RefHolder rh in pool.Values) {
						if(victim != null && rh.Objects.Count >= victim.Objects.Count)
							continue;
						if(!rh.IsInLoop())
							continue;
						victim = rh;
					}
					System.Diagnostics.Debug.Assert(victim != null);
					sql.AddRange(victim.DoUpdates(locking, batchWideData));
					System.Diagnostics.Debug.Assert(!victim.IsInLoop());
				}
			}
			return sql;
		}
		public static List<ModificationStatement> GenerateDelete(XPClassInfo classInfo, ObjectGeneratorCriteriaSet criteriaSet, BatchWideDataHolder4Modification batchWideData) {
			return new DeleteQueryGenerator(classInfo, batchWideData).GenerateSql(criteriaSet, null, false);
		}
		protected override BaseStatement CreateRootStatement(DBTable table, string alias) {
			return new DeleteStatement(table, alias);
		}
	}
	public class ObjectGeneratorCriteriaSet {
		CriteriaOperator commonCriteria;
		public CriteriaOperator CommonCriteria {
			get { return commonCriteria; }
		}
		Dictionary<string, CriteriaOperator> criteriaDict = new Dictionary<string, CriteriaOperator>();
		public ObjectGeneratorCriteriaSet() { }
		public bool TryGetCriteria(string tableName, out CriteriaOperator criteria) {
			return criteriaDict.TryGetValue(tableName, out criteria);
		}
		public CriteriaOperator GetCompleteCriteria(string tableName) {
			CriteriaOperator criteria;
			if(!criteriaDict.TryGetValue(tableName, out criteria)) {
				return commonCriteria;
			}
			if(ReferenceEquals(commonCriteria, null)) {
				return criteria;
			}
			return GroupOperator.And(commonCriteria, criteria);
		}
		public void UpdateCriteria(string tableName, CriteriaOperator criteria) {
			if(ReferenceEquals(criteria, null)) return;
			CriteriaOperator groupCriteria;
			if(criteriaDict.TryGetValue(tableName, out groupCriteria)) {
				GroupOperator group = groupCriteria as GroupOperator;
				if(!ReferenceEquals(group, null) && group.OperatorType == GroupOperatorType.And) {
					group.Operands.Add(criteria);
				} else {
					criteriaDict[tableName] = GroupOperator.And(groupCriteria, criteria);
				}
			} else {
				criteriaDict.Add(tableName, criteria);
			}
		}
		public void UpdateCommonCriteria(CriteriaOperator criteria) {
			if(ReferenceEquals(criteria, null)) return;
			if(ReferenceEquals(commonCriteria, null)) {
				commonCriteria = criteria;
			} else {
				GroupOperator group = commonCriteria as GroupOperator;
				if(!ReferenceEquals(group, null) && group.OperatorType == GroupOperatorType.And) {
					group.Operands.Add(criteria);
				} else {
					commonCriteria = GroupOperator.And(commonCriteria, criteria);
				}
			}
		}
		public static ObjectGeneratorCriteriaSet GetCommonCriteriaSet(CriteriaOperator commonCriteria) {
			var criteriaSet = new ObjectGeneratorCriteriaSet();
			criteriaSet.UpdateCommonCriteria(commonCriteria);
			return criteriaSet;
		}
		public static ObjectGeneratorCriteriaSet GetCriteriaSet(string tableName, CriteriaOperator criteria) {
			var criteriaSet = new ObjectGeneratorCriteriaSet();
			criteriaSet.UpdateCriteria(tableName, criteria);
			return criteriaSet;
		}
		public static ObjectGeneratorCriteriaSet GetCriteriaSet(string tableName, CriteriaOperator criteria, CriteriaOperator commonCriteria) {
			var criteriaSet = new ObjectGeneratorCriteriaSet();
			criteriaSet.UpdateCommonCriteria(commonCriteria);
			criteriaSet.UpdateCriteria(tableName, criteria);
			return criteriaSet;
		}
	}
	public class ProjectionNodeItem {
		public readonly JoinNode Node;
		public readonly List<JoinNode> ProjectedNodes;
		public ProjectionNodeItem(JoinNode node, List<JoinNode> projectedNodes) {
			Node = node;
			ProjectedNodes = projectedNodes;
		}
	}
	public class ProjectionAliasPatcher {
		readonly BaseStatement root;
		readonly Dictionary<string, JoinNode> projectedNodes;
		readonly Dictionary<string, ProjectionNodeItem> projectionNodes;
		HashSet<string> usedProjectionNodes;
		QueryOperandCollector queryOperandCollector;
		public ProjectionAliasPatcher(BaseStatement root, Dictionary<string, JoinNode> projectedNodes, Dictionary<string, ProjectionNodeItem> projectionNodes) {
			this.root = root;
			this.projectedNodes = projectedNodes;
			this.projectionNodes = projectionNodes;
		}
		public static void Patch(BaseStatement root, Dictionary<string, JoinNode> projectedNodes, Dictionary<string, ProjectionNodeItem> projectionNodes) {
			new ProjectionAliasPatcher(root, projectedNodes, projectionNodes).Patch();
		}
		public void Patch() {
			if(projectedNodes == null || projectedNodes.Count == 0)
				return;
			queryOperandCollector = new QueryOperandCollector();
			PatchInternal(root);
		}
		void PatchInternal(BaseStatement statement) {
			if(statement == null)
				return;
			List<JoinNode> nodes;
			List<CriteriaOperator> criteriaList;
			statement.CollectJoinNodesAndCriteria(out nodes, out criteriaList);
			foreach(var criteria in criteriaList) {
				queryOperandCollector.Clear();
				queryOperandCollector.Process(criteria);
				foreach(QueryOperand operand in queryOperandCollector.OperandList) {
					if(string.IsNullOrEmpty(operand.NodeAlias))
						continue;
					JoinNode projectionNode;
					if(projectedNodes.TryGetValue(operand.NodeAlias, out projectionNode)) {
						string projectionNodeAlias = projectionNode.Alias;
						if(usedProjectionNodes != null && usedProjectionNodes.Contains(projectionNodeAlias))
							continue;
						operand.NodeAlias = projectionNodeAlias;
						ProjectionNodeItem projectionNodeItem;
						if(projectionNodes != null && projectionNodes.TryGetValue(projectionNodeAlias, out projectionNodeItem)) {
							BaseQueryGenerator.GetProjectedMemberColumn(operand.ColumnName, projectionNode, projectionNodeItem.ProjectedNodes);
						}
					} else {
						ProjectionNodeItem projectionNodeItem;
						if(projectionNodes.TryGetValue(operand.NodeAlias, out projectionNodeItem)) {
							BaseQueryGenerator.GetProjectedMemberColumn(operand.ColumnName, projectionNodeItem.Node, projectionNodeItem.ProjectedNodes);
						}
					}
				}
			}
			foreach(var node in nodes) {
				DBProjection projection = node.Table as DBProjection;
				if(projection == null)
					continue;
				if(usedProjectionNodes == null)
					usedProjectionNodes = new HashSet<string>();
				usedProjectionNodes.Add(node.Alias);
				try {
					PatchInternal(projection.Projection);
				} finally {
					usedProjectionNodes.Remove(node.Alias);
				}
			}
		}
		class QueryOperandCollector : IQueryCriteriaVisitor {
			readonly List<QueryOperand> operandList = new List<QueryOperand>();
			public List<QueryOperand> OperandList {
				get { return operandList; }
			}
			public void Clear() {
				operandList.Clear();
			}
			public void Visit(QuerySubQueryContainer theOperand) {
				Process(theOperand.AggregateProperty);
				ProcessNode(theOperand.Node);
			}
			public void Visit(QueryOperand theOperand) {
				operandList.Add(theOperand);
			}
			public void Visit(FunctionOperator theOperator) {
				ProcessList(theOperator.Operands);
			}
			public void Visit(OperandValue theOperand) {
				return;
			}
			public void Visit(GroupOperator theOperator) {
				ProcessList(theOperator.Operands);
			}
			public void Visit(InOperator theOperator) {
				Process(theOperator.LeftOperand);
				ProcessList(theOperator.Operands);
			}
			public void Visit(UnaryOperator theOperator) {
				Process(theOperator.Operand);
			}
			public void Visit(BinaryOperator theOperator) {
				Process(theOperator.LeftOperand);
				Process(theOperator.RightOperand);
			}
			public void Visit(BetweenOperator theOperator) {
				Process(theOperator.TestExpression);
				Process(theOperator.BeginExpression);
				Process(theOperator.EndExpression);
			}
			void ProcessNode(JoinNode node) {
				if(node == null)
					return;
				Process(node.Condition);
				foreach(JoinNode innerNode in node.SubNodes) {
					ProcessNode(innerNode);
				}
			}
			void ProcessList(CriteriaOperatorCollection operands) {
				if(operands == null)
					return;
				foreach(var operand in operands) {
					Process(operand);
				}
			}
			public void Process(CriteriaOperator operand) {
				if(ReferenceEquals(operand, null))
					return;
				operand.Accept(this);
			}
		}
	}
	public class StatementNormalizer {
		readonly BaseStatement root;
		readonly Dictionary<string, JoinNode> nodeDict = new Dictionary<string, JoinNode>();
		readonly Dictionary<string, IEnumerable<PlanAliasCriteriaInfo>> nodeCriteriaInfoDict = new Dictionary<string, IEnumerable<PlanAliasCriteriaInfo>>();
		public StatementNormalizer(BaseStatement root) {
			this.root = root;
			nodeDict.Add(root.Alias, root);
		}
		public void ProcessStatement() {
			FindNodes(root);
		}
		void FindNodes(JoinNode node) {
			JoinNode[] subNodes = node.SubNodes.ToArray();
			foreach(JoinNode subNode in subNodes) {
				FindNode(subNode, node);
			}
		}
		void FindNode(JoinNode subNode, JoinNode parentNode) {
			string missedNode = null;
			nodeDict[subNode.Alias] = subNode;
			IEnumerable<PlanAliasCriteriaInfo> result;
			if(!nodeCriteriaInfoDict.TryGetValue(subNode.Alias, out result)) {
				result = NodeCriteriaFinder.FindCriteria(null, subNode.Condition).Values;
				nodeCriteriaInfoDict.Add(subNode.Alias, result);
			}
			foreach(var item in result) {
				foreach(string alias in item.Aliases) {
					if(!nodeDict.ContainsKey(alias)) {
						missedNode = alias;
						break;
					}
				}
				if(missedNode != null)
					break;
			}
			if(missedNode != null) {
				Dictionary<string, JoinNode> collectedNodes = new Dictionary<string, JoinNode>();
				CollectNodes(subNode, collectedNodes);
				if(collectedNodes.ContainsKey(missedNode)) {
					ClearNodes(subNode);
					parentNode.SubNodes.Remove(subNode);
					Dictionary<string, JoinNode> collectedRootSubNodes = new Dictionary<string, JoinNode>();
					CollectNodes(parentNode, collectedRootSubNodes);
					NormalizeNodeTree(parentNode, subNode, collectedNodes, collectedRootSubNodes);
					return;
				}
			}
			FindNodes(subNode);
		}
		static void CollectNodes(JoinNode node, Dictionary<string, JoinNode> collectedNodes) {
			collectedNodes[node.Alias] = node;
			JoinNodeCollection subNodes = node.SubNodes;
			for(int i = subNodes.Count - 1; i >= 0; i--) {
				JoinNode subNode = subNodes[i];
				CollectNodes(subNode, collectedNodes);
			}
		}
		static void ClearNodes(JoinNode node) {
			JoinNodeCollection subNodes = node.SubNodes;
			for(int i = subNodes.Count - 1; i >= 0; i--) {
				JoinNode subNode = subNodes[i];
				subNodes.RemoveAt(i);
				ClearNodes(subNode);
			}
		}
		void NormalizeNodeTree(JoinNode parentNode, JoinNode subNode, Dictionary<string, JoinNode> collectedNodes, Dictionary<string, JoinNode> collectedRootSubNodes) {
			Dictionary<string, List<string>> nodeRelations = new Dictionary<string, List<string>>();
			List<PlanAliasCriteriaInfo> criteriaList = new List<PlanAliasCriteriaInfo>();
			foreach(JoinNode node in collectedNodes.Values) {
				IEnumerable<PlanAliasCriteriaInfo> infoList;
				if(!nodeCriteriaInfoDict.TryGetValue(node.Alias, out infoList)) {
					infoList = NodeCriteriaFinder.FindCriteria(null, node.Condition).Values;
					nodeCriteriaInfoDict.Add(node.Alias, infoList);
				}
				foreach(PlanAliasCriteriaInfo info in infoList) {
					string[] aliases = info.Aliases;
					for(int i = 0; i < aliases.Length; i++) {
						string currentAlias = aliases[i];
						List<string> relations;
						if(!nodeRelations.TryGetValue(currentAlias, out relations)) {
							relations = new List<string>();
							nodeRelations.Add(currentAlias, relations);
						}
						for(int j = 0; j < aliases.Length; j++) {
							if(i == j) continue;
							relations.Add(aliases[j]);
						}
					}
					criteriaList.Add(info);
				}
				node.Condition = null;
			}
			List<string> parentNodeRelationList;
			if(!nodeRelations.TryGetValue(parentNode.Alias, out parentNodeRelationList)) {
				parentNodeRelationList = new List<string>();
				nodeRelations.Add(parentNode.Alias, parentNodeRelationList);
			}
			if(!parentNodeRelationList.Contains(subNode.Alias))
				parentNodeRelationList.Add(subNode.Alias);
			PathInfo[] result = PathFinder.Find(parentNode.Alias, collectedRootSubNodes.Keys, collectedNodes.Keys, criteriaList, nodeRelations);
			if(result == null) {
				throw new InvalidOperationException();
			}
			foreach(PathInfo pi in result) {
				JoinNode leftNode = pi.LeftNode == parentNode.Alias ? parentNode : collectedNodes[pi.LeftNode];
				JoinNode rightNode = collectedNodes[pi.RightNode];
				leftNode.SubNodes.Add(rightNode);
				rightNode.Condition = pi.GetCriteria();
			}
		}
		public static void Normalize(BaseStatement statement) {
			new StatementNormalizer(statement).ProcessStatement();
		}
		class PathInfo {
			public string LeftNode;
			public string RightNode;
			public List<PlanAliasCriteriaInfo> CriteriaList;
			public CriteriaOperator GetCriteria() {
				if(CriteriaList == null || CriteriaList.Count == 0) return null;
				List<CriteriaOperator> criteriaList = new List<CriteriaOperator>();
				foreach(var info in CriteriaList) {
					criteriaList.AddRange(info.Criteria);
				}
				if(criteriaList.Count == 1) return criteriaList[0];
				return GroupOperator.And(criteriaList);
			}
		}
		class PathFinder {
			readonly int nodesCount;
			readonly string rootNode;
			PathInfo[] result;
			readonly Stack<PathInfo> pathStack = new Stack<PathInfo>();
			readonly Stack<string> nodeStack = new Stack<string>();
			readonly List<PlanAliasCriteriaInfo> leftCriteriaList;
			readonly Dictionary<string, List<string>> nodeRelations = new Dictionary<string, List<string>>();
			readonly HashSet<string> leftNodes = new HashSet<string>();
			readonly HashSet<string> usedNodes = new HashSet<string>();
			public PathFinder(string rootNode, IEnumerable<string> rootSubNodes, IEnumerable<string> nodes, IEnumerable<PlanAliasCriteriaInfo> criteriaList, Dictionary<string, List<string>> nodeRelations) {
				this.rootNode = rootNode;
				usedNodes.Add(rootNode);
				foreach(string rootSubNode in rootSubNodes) {
					usedNodes.Add(rootSubNode);
				}
				foreach(string node in nodes) {
					leftNodes.Add(node);
					nodesCount++;
				}
				leftCriteriaList = new List<PlanAliasCriteriaInfo>(criteriaList);
				this.nodeRelations = nodeRelations;
			}
			public static PathInfo[] Find(string rootNode, IEnumerable<string> rootSubNodes, IEnumerable<string> nodes, IEnumerable<PlanAliasCriteriaInfo> criteriaList, Dictionary<string, List<string>> nodeRelations) {
				return new PathFinder(rootNode, rootSubNodes, nodes, criteriaList, nodeRelations).Find();
			}
			public PathInfo[] Find() {
				result = null;
				ProcessNode(rootNode);
				return result;
			}
			public bool ProcessNode(string currentNode) {
				if(pathStack.Count == nodesCount) {
					if(leftCriteriaList.Count > 0) {
						return false;
					}
					result = pathStack.ToArray();
					Array.Reverse(result);
					return true;
				}
				List<string> relations;
				if(nodeRelations.TryGetValue(currentNode, out relations)) {
					foreach(string rel in relations) {
						if(!leftNodes.Contains(rel)) continue;
						if(MoveUp(currentNode, rel)) {
							return true;
						}
					}
				}
				return MoveDown();
			}
			public void ProcessCriteria(PathInfo pi) {
				for(int i = leftCriteriaList.Count - 1; i >= 0; i--) {
					PlanAliasCriteriaInfo criteriaInfo = leftCriteriaList[i];
					string[] aliases = criteriaInfo.Aliases;
					bool canUseCriteria = true;
					for(int j = 0; j < aliases.Length; j++) {
						string alias = aliases[j];
						if(!usedNodes.Contains(alias)) {
							canUseCriteria = false;
							break;
						}
					}
					if(canUseCriteria) {
						leftCriteriaList.RemoveAt(i);
						if(pi.CriteriaList == null)
							pi.CriteriaList = new List<PlanAliasCriteriaInfo>();
						pi.CriteriaList.Add(criteriaInfo);
					}
				}
			}
			public bool MoveUp(string left, string right) {
				nodeStack.Push(left);
				leftNodes.Remove(right);
				usedNodes.Add(right);
				PathInfo pi = new PathInfo() { LeftNode = left, RightNode = right };
				ProcessCriteria(pi);
				pathStack.Push(pi);
				try {
					return ProcessNode(right);
				} finally {
					pathStack.Pop();
					if(pi.CriteriaList != null) {
						leftCriteriaList.AddRange(pi.CriteriaList);
					}
					usedNodes.Remove(right);
					leftNodes.Add(right);
					nodeStack.Pop();
				}
			}
			public bool MoveDown() {
				if(nodeStack.Count == 0) return false;
				string currentNode = nodeStack.Pop();
				try {
					return ProcessNode(currentNode);
				} finally {
					nodeStack.Push(currentNode);
				}
			}
		}
	}
}
