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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using DevExpress.Data.Filtering;
using DevExpress.Data.Filtering.Helpers;
using DevExpress.Data.Helpers;
using DevExpress.Utils;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.Helpers;
using DevExpress.Xpo.Infrastructure;
using DevExpress.Xpo.Metadata;
using DevExpress.Xpo.Metadata.Helpers;
#if !NET
using DevExpress.Data.NetCompatibility.Extensions;
#endif
namespace DevExpress.Xpo.Helpers {
	class EnumerableWrapper<T> : IEnumerable<T> {
		IEnumerable<T> parent;
		public EnumerableWrapper(IEnumerable<T> parent) {
			this.parent = parent;
		}
		IEnumerator IEnumerable.GetEnumerator() {
			return parent.GetEnumerator();
		}
		IEnumerator<T> IEnumerable<T>.GetEnumerator() {
			return parent.GetEnumerator();
		}
	}
	public class ExpressionAccessOperator : CriteriaOperator {
		public Expression LinqExpression;
		public bool InsertFirstNull;
		public CriteriaOperator[] SourceItems;
		public ExpressionAccessOperator(Expression linqExpression, params CriteriaOperator[] sourceItems)
			: this(linqExpression, false, sourceItems) {
		}
		public ExpressionAccessOperator(Expression linqExpression, bool insertFirstNull, params CriteriaOperator[] sourceItems) {
			InsertFirstNull = insertFirstNull;
			LinqExpression = linqExpression;
			SourceItems = sourceItems;
		}
		public override void Accept(ICriteriaVisitor visitor) {
			throw new InvalidOperationException();
		}
		public override T Accept<T>(ICriteriaVisitor<T> visitor) {
			ILinqExtendedCriteriaVisitor<T> miVisitor;
			if((miVisitor = visitor as ILinqExtendedCriteriaVisitor<T>) == null) throw new InvalidOperationException();
			return miVisitor.Visit(this);
		}
		protected override CriteriaOperator CloneCommon() {
			throw new InvalidOperationException();
		}
		Type[] cachedSourceTypes;
		Type GetSourceType(CriteriaTypeResolver resolver, CriteriaOperator prop) {
			if(prop is GroupSet) return null;
			return resolver.Resolve(prop);
		}
		public Type[] GetSourceTypes(Type type, CriteriaTypeResolver resolver) {
			if(cachedSourceTypes == null) {
				Type[] sourceTypes = new Type[SourceItems.Length];
				for(int i = 0; i < sourceTypes.Length; i++) {
					sourceTypes[i] = GetSourceType(resolver, SourceItems[i]);
				}
				cachedSourceTypes = sourceTypes;
			}
			return cachedSourceTypes;
		}
		public override int GetHashCode() {
			int hashCode = HashCodeHelper.Start();
			if(LinqExpression != null)
				hashCode = HashCodeHelper.CombineGeneric(hashCode, LinqExpression.NodeType, LinqExpression.Type);
			var sourceItems = SourceItems;
			if(SourceItems != null) {
				hashCode = HashCodeHelper.CombineGenericList(hashCode, SourceItems);
			}
			return HashCodeHelper.Finish(hashCode);
		}
		public override bool Equals(object obj) {
			if(ReferenceEquals(this, obj))
				return true;
			if(obj == null)
				return false;
			if(!object.ReferenceEquals(this.GetType(), obj.GetType()))
				return false;
			ExpressionAccessOperator another = (ExpressionAccessOperator)obj;
			if(LinqExpression == another.LinqExpression) return true;
			if(LinqExpression == null || another.LinqExpression == null) return false;
			if(LinqExpression.NodeType != another.LinqExpression.NodeType || LinqExpression.Type != another.LinqExpression.Type) return false;
			if(SourceItems == another.SourceItems) return true;
			if(another.SourceItems == null || SourceItems.Length != another.SourceItems.Length) return false;
			for(int i = 0; i < SourceItems.Length; i++) {
				if(!CriterionEquals(SourceItems[i], another.SourceItems[i])) return false;
			}
			return true;
		}
	}
	public class QuerySet : CriteriaOperator {
		public CriteriaOperator Condition;
		public OperandProperty Property;
		public MemberInitOperator Projection;
		public QuerySet() { }
		public QuerySet(string name) {
			if(name != null)
				Property = new OperandProperty(name);
		}
		public QuerySet(OperandProperty property, CriteriaOperator condition) {
			Property = property;
			Condition = condition;
		}
		public QuerySet(MemberInitOperator projection) {
			Projection = projection;
		}
		public bool TryGetProjectionSingleProperty(out CriteriaOperator property) {
			property = null;
			return !ReferenceEquals(Projection, null) && Projection.TryGetSingleProperty(out property);
		}
		public override void Accept(ICriteriaVisitor visitor) {
			IClientCriteriaVisitor clientVisitor = visitor as IClientCriteriaVisitor;
			if(IsEmpty) {
				if(clientVisitor != null) {
					clientVisitor.Visit(Parser.ThisCriteria);
					return;
				}
			}
			else if(ReferenceEquals(Projection, null) && !ReferenceEquals(Property, null)) {
				if(clientVisitor != null) {
					clientVisitor.Visit(GetPropertyForVisitor(Property));
					return;
				}
			}
			else if(!ReferenceEquals(Projection, null) && ReferenceEquals(Property, null)) {
				CriteriaOperator property;
				if(TryGetProjectionSingleProperty(out property)) {
					if(clientVisitor != null) {
						property.Accept(clientVisitor);
					}
				}
			}
			throw new InvalidOperationException();
		}
		public override T Accept<T>(ICriteriaVisitor<T> visitor) {
			var linqVisitor = visitor as ILinqExtendedCriteriaVisitor<T>;
			if(linqVisitor != null) {
				return linqVisitor.Visit(this);
			}
			var clientVisitor = visitor as IClientCriteriaVisitor<T>;
			if(IsEmpty) {
				if(clientVisitor != null) {
					return clientVisitor.Visit(Parser.ThisCriteria);
				}
			}
			else if(ReferenceEquals(Projection, null) && !ReferenceEquals(Property, null)) {
				if(clientVisitor != null) {
					return clientVisitor.Visit(GetPropertyForVisitor(Property));
				}
			}
			else if(!ReferenceEquals(Projection, null) && ReferenceEquals(Property, null)) {
				CriteriaOperator property;
				if(TryGetProjectionSingleProperty(out property)) {
					if(clientVisitor != null) {
						return property.Accept(clientVisitor);
					}
				}
			}
			throw new InvalidOperationException();
		}
		public CriteriaOperator CreateCriteriaOperator(CriteriaOperator expression, Aggregate aggregateType) {
			return CreateCriteriaOperator(Condition, expression, aggregateType);
		}
		public virtual CriteriaOperator CreateCriteriaOperator(CriteriaOperator condition, CriteriaOperator expression, Aggregate aggregateType) {
			return new AggregateOperand(Property, expression, aggregateType, condition);
		}
		public CriteriaOperator CreateCriteriaOperator(IEnumerable<CriteriaOperator> expressions, string customAggregateName) {
			return CreateCriteriaOperator(Condition, expressions, customAggregateName);
		}
		public virtual CriteriaOperator CreateCriteriaOperator(CriteriaOperator condition, IEnumerable<CriteriaOperator> expressions, string customAggregateName) {
			return new AggregateOperand(Property, expressions, customAggregateName, condition);
		}
		OperandProperty GetPropertyForVisitor(OperandProperty operandProperty) {
			return operandProperty.PropertyName.EndsWith('^') ? new OperandProperty(operandProperty.PropertyName + ".This") : operandProperty;
		}
		protected override CriteriaOperator CloneCommon() {
			return new QuerySet() {
				Condition = Condition,
				Property = Property,
				Projection = Projection
			};
		}
		public bool IsEmpty {
			get { return ReferenceEquals(Condition, null) && ReferenceEquals(Property, null) && ReferenceEquals(Projection, null); }
		}
		internal static QuerySet Empty = new QuerySet();
		public override int GetHashCode() {
			return HashCodeHelper.CalculateGeneric(Condition, Property, Projection);
		}
		public override bool Equals(object obj) {
			if(ReferenceEquals(this, obj))
				return true;
			if(obj == null)
				return false;
			if(!object.ReferenceEquals(this.GetType(), obj.GetType()))
				return false;
			QuerySet qs = (QuerySet)obj;
			return CriterionEquals(Condition, qs.Condition) && CriterionEquals(Property, qs.Property) && CriterionEquals(Projection, qs.Projection);
		}
	}
	public class GroupSet : QuerySet {
		public CriteriaOperator Key;
		public GroupSet() { }
		public GroupSet(MemberInitOperator projection, CriteriaOperator key)
			: base(projection) {
			this.Key = key;
		}
		public override void Accept(ICriteriaVisitor visitor) {
			throw new InvalidOperationException();
		}
		public override T Accept<T>(ICriteriaVisitor<T> visitor) {
			var linqVisitor = visitor as ILinqExtendedCriteriaVisitor<T>;
			if(linqVisitor != null) {
				return linqVisitor.Visit(this);
			}
			throw new InvalidOperationException();
		}
		protected override CriteriaOperator CloneCommon() {
			throw new InvalidOperationException();
		}
		public override int GetHashCode() {
			return Key.GetHashCodeNullSafe();
		}
		public override bool Equals(object obj) {
			if(!base.Equals(obj)) return false;
			GroupSet gs = (GroupSet)obj;
			return CriterionEquals(Key, gs.Key);
		}
	}
	class JoinOperandInfo {
		public readonly Aggregate AggregateType;
		public readonly string CustomAggregateName;
		public readonly CriteriaOperator Condition;
		public readonly string JoinTypeName;
		JoinOperandInfo(string joinTypeName, CriteriaOperator condition, Aggregate aggregateType, string customAggregateName) {
			if(aggregateType == Aggregate.Custom && string.IsNullOrEmpty(customAggregateName)) {
				throw new ArgumentNullException(nameof(customAggregateName));
			}
			AggregateType = aggregateType;
			CustomAggregateName = customAggregateName;
			Condition = condition;
			JoinTypeName = joinTypeName;
		}
		public JoinOperandInfo(string joinTypeName, CriteriaOperator condition, Aggregate aggregateType)
			: this(joinTypeName, condition, aggregateType, null) { }
		public JoinOperandInfo(string joinTypeName, CriteriaOperator condition, string customAggregateName)
			: this(joinTypeName, condition, Aggregate.Custom, customAggregateName) { }
		public JoinOperandInfo(JoinOperand joinOperand)
			: this(joinOperand.JoinTypeName, joinOperand.Condition, joinOperand.AggregateType, joinOperand.CustomAggregateName) {
		}
		public override bool Equals(object obj) {
			JoinOperandInfo other = obj as JoinOperandInfo;
			if(other == null) return false;
			return JoinTypeName == other.JoinTypeName && CriteriaOperator.CriterionEquals(Condition, other.Condition) && AggregateType == other.AggregateType && CustomAggregateName == other.CustomAggregateName;
		}
		public override int GetHashCode() {
			return HashCodeHelper.CalculateGeneric(AggregateType, JoinTypeName, Condition, CustomAggregateName);
		}
	}
	public class FreeQuerySet : QuerySet {
		struct FreeJoinPair {
			public readonly CriteriaOperator UpOperand;
			public readonly CriteriaOperator Operand;
			public FreeJoinPair(CriteriaOperator upOperand, CriteriaOperator operand) {
				UpOperand = upOperand;
				Operand = operand;
			}
		}
		public Type JoinType;
		JoinOperandInfo MasterJoin;
		List<FreeJoinPair> MasterJoinEqualsOperands;
		public bool HasMasterJoin {
			get { return MasterJoin != null; }
		}
		public override CriteriaOperator CreateCriteriaOperator(CriteriaOperator condition, CriteriaOperator expression, Aggregate aggregateType) {
			if(HasMasterJoin) {
				JoinOperand nestedJoin = new JoinOperand(JoinType.FullName, GetGroup(condition, MasterJoinEqualsOperands), aggregateType, expression);
				return new JoinOperand(MasterJoin.JoinTypeName, MasterJoin.Condition, MasterJoin.AggregateType, nestedJoin);
			}
			return new JoinOperand(JoinType.FullName, condition, aggregateType, expression);
		}
		public override CriteriaOperator CreateCriteriaOperator(CriteriaOperator condition, IEnumerable<CriteriaOperator> expressions, string customAggregateName) {
			if(HasMasterJoin) {
				JoinOperand nestedJoin = new JoinOperand(JoinType.FullName, GetGroup(condition, MasterJoinEqualsOperands), customAggregateName, expressions);
				return new JoinOperand(MasterJoin.JoinTypeName, MasterJoin.Condition, MasterJoin.CustomAggregateName, new CriteriaOperator[] { nestedJoin });
			}
			return new JoinOperand(JoinType.FullName, condition, customAggregateName, expressions);
		}
		public FreeQuerySet() { }
		public FreeQuerySet(Type joinType, CriteriaOperator upLevelOperand, CriteriaOperator operand) : this(joinType, upLevelOperand, operand, null) { }
		static GroupOperator GetGroup(CriteriaOperator condition, List<FreeJoinPair> joinOperands) {
			GroupOperator group = new GroupOperator(GroupOperatorType.And);
			for(int i = 0; i < joinOperands.Count; i++) {
				group.Operands.Add(new BinaryOperator(joinOperands[i].UpOperand, joinOperands[i].Operand, BinaryOperatorType.Equal));
			}
			if(!ReferenceEquals(condition, null)) group.Operands.Add(condition);
			return group;
		}
		public FreeQuerySet(Type joinType, CriteriaOperator upLevelOperand, CriteriaOperator operand, CriteriaOperator condition) {
			JoinType = joinType;
			MemberInitOperator upLevelMemberInit = upLevelOperand as MemberInitOperator;
			MemberInitOperator memberInit = operand as MemberInitOperator;
			bool isUpLevelMemberInit = !ReferenceEquals(upLevelMemberInit, null);
			bool isMemberInit = !ReferenceEquals(memberInit, null);
			if(isUpLevelMemberInit != isMemberInit) throw new NotSupportedException(Res.GetString(Res.LinqToXpo_SpecifiedJoinKeySelectorsNotCompatibleX0X1, upLevelOperand, operand));
			if(isUpLevelMemberInit && isMemberInit) {
				if(upLevelMemberInit.Members.Count != memberInit.Members.Count) throw new NotSupportedException(Res.GetString(Res.LinqToXpo_SpecifiedJoinKeySelectorsNotCompatibleX0X1, upLevelMemberInit, memberInit));
				JoinOperandInfo foundJoinOperand = null;
				List<FreeJoinPair> joinOperands = new List<FreeJoinPair>(memberInit.Members.Count);
				for(int i = 0; i < upLevelMemberInit.Members.Count; i++) {
					Dictionary<JoinOperandInfo, bool> upJoinOperandList;
					CriteriaOperator upLevelProperty = DownLevelReprocessor.Reprocess(upLevelMemberInit.Members[i].Property, out upJoinOperandList);
					if(upJoinOperandList != null) {
						if(upJoinOperandList.Count != 1) throw new NotSupportedException(Res.GetString(Res.LinqToXpo_TheJoinWithManyTablesSimultaneouslyInASing));
						JoinOperandInfo joinOperand = upJoinOperandList.Keys.First();
						if(foundJoinOperand == null) {
							foundJoinOperand = joinOperand;
						}
						else if(!foundJoinOperand.Equals(joinOperand)) throw new NotSupportedException(Res.GetString(Res.LinqToXpo_TheJoinWithManyTablesSimultaneouslyInASing));
					}
					joinOperands.Add(new FreeJoinPair(upLevelProperty, memberInit.Members[i].Property));
				}
				if(!ReferenceEquals(foundJoinOperand, null)) {
					MasterJoin = foundJoinOperand;
					MasterJoinEqualsOperands = joinOperands;
					Condition = condition;
					return;
				}
				Condition = GetGroup(condition, joinOperands);
				return;
			}
			Dictionary<JoinOperandInfo, bool> joinOperandList;
			CriteriaOperator upOperand = DownLevelReprocessor.Reprocess(upLevelOperand, out joinOperandList);
			if(joinOperandList != null) {
				if(joinOperandList.Count != 1) throw new NotSupportedException(Res.GetString(Res.LinqToXpo_DoesNotSupportNestedJoinsWithLevel));
				MasterJoin = joinOperandList.Keys.First();
				MasterJoinEqualsOperands = new List<FreeJoinPair>();
				MasterJoinEqualsOperands.Add(new FreeJoinPair(upOperand, operand));
				Condition = condition;
				return;
			}
			if(!ReferenceEquals(condition, null))
				Condition = GroupOperator.And(new BinaryOperator(upOperand, operand, BinaryOperatorType.Equal), condition);
			else
				Condition = new BinaryOperator(upOperand, operand, BinaryOperatorType.Equal);
		}
		public FreeQuerySet(Type joinType, CriteriaOperator condition) {
			JoinType = joinType;
			Condition = condition;
		}
		public override void Accept(ICriteriaVisitor visitor) {
			IClientCriteriaVisitor clientVisitor = visitor as IClientCriteriaVisitor;
			if(clientVisitor == null) return;
			CriteriaOperator property;
			if(TryGetProjectionSingleProperty(out property)) {
				clientVisitor.Visit((JoinOperand)CreateCriteriaOperator(property, Aggregate.Single));
				return;
			}
			clientVisitor.Visit((JoinOperand)CreateCriteriaOperator(Parser.ThisCriteria, Aggregate.Single));
		}
		public override T Accept<T>(ICriteriaVisitor<T> visitor) {
			var clientVisitor = visitor as IClientCriteriaVisitor<T>;
			if(clientVisitor == null) return default(T);
			CriteriaOperator property;
			if(TryGetProjectionSingleProperty(out property)) {
				return clientVisitor.Visit((JoinOperand)CreateCriteriaOperator(property, Aggregate.Single));
			}
			return clientVisitor.Visit((JoinOperand)CreateCriteriaOperator(Parser.ThisCriteria, Aggregate.Single));
		}
		protected override CriteriaOperator CloneCommon() {
			return new FreeQuerySet() {
				Condition = Condition,
				Property = Property,
				Projection = Projection,
				JoinType = JoinType,
				MasterJoin = MasterJoin,
				MasterJoinEqualsOperands = MasterJoinEqualsOperands == null ? null : new List<FreeJoinPair>(MasterJoinEqualsOperands)
			};
		}
		public override int GetHashCode() {
			int hashCode = base.GetHashCode();
			hashCode = HashCodeHelper.CombineGeneric(hashCode, JoinType, MasterJoin);
			if(MasterJoinEqualsOperands != null) {
				int count = MasterJoinEqualsOperands.Count;
				for(int i = 0; i < count; i++) {
					FreeJoinPair pair = MasterJoinEqualsOperands[i];
					hashCode = HashCodeHelper.CombineGeneric(hashCode, pair.UpOperand, pair.Operand);
				}
			}
			return HashCodeHelper.Finish(hashCode);
		}
		public override bool Equals(object obj) {
			if(!base.Equals(obj)) return false;
			FreeQuerySet other = obj as FreeQuerySet;
			if(ReferenceEquals(other, null) || JoinType != other.JoinType) return false;
			if(!ReferenceEquals(MasterJoin, other.MasterJoin) && (ReferenceEquals(MasterJoin, null) || ReferenceEquals(other.MasterJoin, null))) return false;
			if(MasterJoin != null && !MasterJoin.Equals(other.MasterJoin)) return false;
			if(MasterJoinEqualsOperands == other.MasterJoinEqualsOperands) return true;
			if(MasterJoinEqualsOperands == null || other.MasterJoinEqualsOperands == null || MasterJoinEqualsOperands.Count != other.MasterJoinEqualsOperands.Count) return false;
			int count = MasterJoinEqualsOperands.Count;
			for(int i = 0; i < count; i++) {
				if(!CriteriaOperator.CriterionEquals(MasterJoinEqualsOperands[i].UpOperand, other.MasterJoinEqualsOperands[i].UpOperand)
					|| !CriteriaOperator.CriterionEquals(MasterJoinEqualsOperands[i].Operand, other.MasterJoinEqualsOperands[i].Operand)) return false;
			}
			return true;
		}
	}
	public interface ILinqExtendedCriteriaVisitor<T> {
		T Visit(MemberInitOperator theOperand);
		T Visit(ExpressionAccessOperator theOperand);
		T Visit(QuerySet theOperand);
	}
	public class MemberInitOperator : CriteriaOperator {
		ConstructorInfo constructor;
		internal static Type GetMemberType(MemberInfo mi) {
			switch(mi.MemberType) {
				case MemberTypes.Field: return ((FieldInfo)mi).FieldType;
				case MemberTypes.Property: return ((PropertyInfo)mi).PropertyType;
				case MemberTypes.Method: return ((MethodInfo)mi).ReturnType;
			}
			throw new ArgumentException($"{nameof(mi)}.{nameof(mi.MemberType)}", nameof(mi));
		}
		Type GetDeclaringTypeInternal() {
			if(string.IsNullOrEmpty(DeclaringTypeAssemblyName))
				throw new InvalidOperationException(Res.GetString(Res.LinqToXpo_TheDeclaringTypeAssemblyNamePropertyIsEmpty));
			if(string.IsNullOrEmpty(DeclaringTypeName))
				throw new InvalidOperationException(Res.GetString(Res.LinqToXpo_TheDeclaringTypeNamePropertyIsEmpty));
#pragma warning disable DX0005
			return DevExpress.Xpo.Helpers.XPTypeActivator.GetType(DeclaringTypeAssemblyName, DeclaringTypeName);
#pragma warning restore DX0005
		}
		public Type GetDeclaringType() {
			if(constructor == null) return GetDeclaringTypeInternal();
			return constructor.DeclaringType;
		}
		Type[] cachedSourceTypes;
		Type GetSourceType(CriteriaTypeResolver resolver, CriteriaOperator prop) {
			if(prop is GroupSet) return null;
			return resolver.Resolve(prop);
		}
		public Type[] GetSourceTypes(Type type, CriteriaTypeResolver resolver) {
			if(cachedSourceTypes == null) {
				if(CreateNewObject) {
					if(UseConstructor) {
						ConstructorInfo con = GetConstructor(type);
						ParameterInfo[] parameters = con.GetParameters();
						Type[] sourceTypes = new Type[parameters.Length];
						for(int i = 0; i < parameters.Length; i++)
							sourceTypes[i] = GetSourceType(resolver, Members[i].Property);
						cachedSourceTypes = sourceTypes;
					}
					else {
						Type[] sourceTypes = new Type[Members.Count];
						for(int i = 0; i < Members.Count; i++) {
							sourceTypes[i] = GetSourceType(resolver, Members[i].Property);
						}
						cachedSourceTypes = sourceTypes;
					}
				}
				else {
					Type elementType = type.GetElementType();
					Type[] sourceTypes = new Type[Members.Count];
					for(int i = 0; i < sourceTypes.Length; i++) {
						sourceTypes[i] = GetSourceType(resolver, Members[i].Property);
					}
					cachedSourceTypes = sourceTypes;
				}
			}
			return cachedSourceTypes;
		}
		public ConstructorInfo GetConstructor(Type type) {
			if(constructor == null) {
				if(type == null) {
					type = GetDeclaringTypeInternal();
					if(type == null)
						throw new ArgumentException(null, nameof(type));
				}
				Type[] types = new Type[UseConstructor ? Members.Count : 0];
				if(UseConstructor) {
					for(int i = 0; i < types.Length; i++) {
						MemberInfo mi = Members[i].GetMember(type);
						types[i] = GetMemberType(mi);
					}
				}
				constructor = type.GetConstructor(types);
			}
			return constructor;
		}
		[XmlAttribute]
		public string DeclaringTypeAssemblyName;
		[XmlAttribute]
		public string DeclaringTypeName;
		[XmlAttribute]
		public bool UseConstructor;
		[XmlAttribute]
		public bool CreateNewObject;
		public XPMemberAssignmentCollection Members;
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool EnableUnsafeDeserialization = false;
		public MemberInitOperator() {
			if(!EnableUnsafeDeserialization) {
				throw new SerializationException(Res.GetString(Res.InitOperator_DeserializationProhibitedDueSecurityIssue));
			}
		}
		public MemberInitOperator(MemberInitOperator source, XPMemberAssignmentCollection newMembers) {
			if(ReferenceEquals(source, null)) {
				throw new ArgumentNullException(nameof(source));
			}
			CreateNewObject = source.CreateNewObject;
			UseConstructor = source.UseConstructor;
			constructor = source.constructor;
			DeclaringTypeAssemblyName = source.DeclaringTypeAssemblyName;
			DeclaringTypeName = source.DeclaringTypeName;
			Members = newMembers;
		}
		public MemberInitOperator(string declaringTypeAssemblyName, string declaringTypeName, XPMemberAssignmentCollection members, bool createNewObject) {
			Members = members;
			CreateNewObject = createNewObject;
			DeclaringTypeAssemblyName = declaringTypeAssemblyName;
			DeclaringTypeName = declaringTypeName;
		}
		public MemberInitOperator(Type declaringType, XPMemberAssignmentCollection members, bool createNewObject) {
			Members = members;
			CreateNewObject = createNewObject;
			if(declaringType != null) {
				DeclaringTypeAssemblyName = declaringType.Assembly.FullName;
				DeclaringTypeName = declaringType.FullName;
			}
		}
		public MemberInitOperator(bool useConstructor, XPMemberAssignmentCollection members, ConstructorInfo constructor) {
			Members = members;
			this.constructor = constructor;
			UseConstructor = useConstructor;
			CreateNewObject = true;
			DeclaringTypeAssemblyName = constructor.DeclaringType.Assembly.FullName;
			DeclaringTypeName = constructor.DeclaringType.FullName;
		}
		public bool TryGetSingleQuerySet(out QuerySet set) {
			CriteriaOperator property;
			if(TryGetSingleProperty(out property)) {
				set = property as QuerySet;
				return !ReferenceEquals(set, null);
			}
			set = null;
			return false;
		}
		public bool TryGetSingleProperty(out CriteriaOperator property) {
			bool canAccept = !CreateNewObject && !UseConstructor && !ReferenceEquals(Members, null) && Members.Count == 1 && !ReferenceEquals(Members[0].Property, null);
			property = canAccept ? Members[0].Property : null;
			return canAccept;
		}
		public override void Accept(ICriteriaVisitor visitor) {
			throw new InvalidOperationException();
		}
		public override T Accept<T>(ICriteriaVisitor<T> visitor) {
			ILinqExtendedCriteriaVisitor<T> miVisitor;
			if((miVisitor = visitor as ILinqExtendedCriteriaVisitor<T>) == null) throw new InvalidOperationException();
			return miVisitor.Visit(this);
		}
		protected override CriteriaOperator CloneCommon() {
			throw new InvalidOperationException();
		}
		public override int GetHashCode() {
			var hashCode = HashCodeHelper.StartGeneric(UseConstructor, CreateNewObject);
			if(Members != null) {
				hashCode = HashCodeHelper.CombineGenericList(hashCode, Members);
			}
			return HashCodeHelper.Finish(hashCode);
		}
		public override bool Equals(object obj) {
			if(ReferenceEquals(this, obj))
				return true;
			if(obj == null)
				return false;
			if(!object.ReferenceEquals(this.GetType(), obj.GetType()))
				return false;
			MemberInitOperator another = (MemberInitOperator)obj;
			if(UseConstructor != another.UseConstructor || CreateNewObject != another.CreateNewObject) return false;
			if((UseConstructor && GetConstructor(null) != another.GetConstructor(null)) || DeclaringTypeName != another.DeclaringTypeName) return false;
			if(Members == another.Members) return true;
			if(Members == null || another.Members == null || Members.Count != another.Members.Count) return false;
			for(int i = 0; i < Members.Count; i++) {
				if(Members[i].MemberName != another.Members[i].MemberName || !CriterionEquals(Members[i].Property, another.Members[i].Property)) return false;
			}
			return true;
		}
		public override string ToString() {
			if(CreateNewObject) {
				StringBuilder sb = new StringBuilder();
				sb.Append("new ");
				if(!DeclaringTypeName.Contains("<>")) {
					sb.Append(DeclaringTypeName);
					sb.Append(' ');
				}
				string closingString;
				if(UseConstructor) {
					sb.Append('(');
					closingString = ")";
				}
				else {
					sb.Append("{ ");
					closingString = " }";
				}
				if(Members != null) {
					for(int i = 0; i < Members.Count; i++) {
						var member = Members[i];
						if(i > 0)
							sb.Append(",  ");
						sb.Append(member.MemberName);
						sb.Append(" = ");
						sb.Append(ReferenceEquals(member.Property, null) ? "null" : member.Property.ToString());
					}
				}
				sb.Append(closingString);
				return sb.ToString();
			}
			return Members == null || Members.Count == 0 ? "{ Empty }" : (ReferenceEquals(Members[0].Property, null) ? "{ null }" : Members[0].Property.ToString());
		}
		internal static MemberInitOperator CreateUntypedMemberInitOperator(CriteriaOperator operand) {
			return new MemberInitOperator(null, new XPMemberAssignmentCollection() { new XPMemberAssignment(operand) }, false);
		}
	}
	public class XPMemberAssignmentCollection : List<XPMemberAssignment> {
		public XPMemberAssignmentCollection()
			: base() {
		}
		public XPMemberAssignmentCollection(IEnumerable<XPMemberAssignment> colletcion)
			: base(colletcion) {
		}
	}
	public class XPMemberAssignment {
		MemberInfo member;
		public MemberInfo GetMember(Type type) {
			if(member == null && type != null)
				member = type.GetMember(memberName)[0];
			if(member == null)
				throw new InvalidOperationException(Res.GetString(Res.ObjectLayer_MemberNotFound, (type == null ? "" : type.Name), memberName));
			return member;
		}
		string memberName;
		[XmlAttribute]
		public string MemberName {
			get { return memberName; }
			set { memberName = value; }
		}
		CriteriaOperator property;
		public CriteriaOperator Property {
			get { return property; }
			set { property = value; }
		}
		public XPMemberAssignment() { }
		public XPMemberAssignment(MemberInfo member, CriteriaOperator property) {
			this.member = member;
			memberName = member.Name;
			this.property = property;
		}
		public XPMemberAssignment(XPMemberAssignment source, CriteriaOperator property) {
			this.member = source.member;
			this.memberName = source.memberName;
			this.Property = property;
		}
		public XPMemberAssignment(CriteriaOperator property) {
			this.property = property;
		}
	}
	public class QueryProviderEx {
		public static IQueryable CreateQuery<T>(IQueryProvider provider, Expression e) {
			return provider.CreateQuery<T>(e);
		}
		public delegate IQueryable CreateQueryHandler(IQueryProvider provider, Expression e);
	}
	public class InOperatorCompiler : InOperator {
		OperandValue e;
		XPDictionary dictionary;
		Func<CriteriaOperatorCollection> expression;
		public InOperatorCompiler() { expression = delegate { return GetBaseOperands(); }; }
		CriteriaOperatorCollection GetBaseOperands() {
			return base.Operands;
		}
		public InOperatorCompiler(XPDictionary dictionary, CriteriaOperator leftOperand, OperandValue expression)
			: base(leftOperand) {
			e = expression;
			this.dictionary = dictionary;
		}
		public override CriteriaOperatorCollection Operands {
			get {
				if(expression == null) {
					expression = delegate {
						CriteriaOperatorCollection ops = new CriteriaOperatorCollection();
						var list = (IEnumerable)e.Value;
						if(list == null) {
							return ops;
						}
						foreach(object value in list) {
							ops.Add(new RefCompiler(dictionary, value));
						}
						return ops;
					};
				}
				return expression();
			}
		}
	}
	public class RefCompiler : OperandValue {
		XPDictionary dictionary;
		public RefCompiler() {
		}
		public RefCompiler(XPDictionary dictionary) {
			this.dictionary = dictionary;
		}
		public RefCompiler(XPDictionary dictionary, object value)
			: base(value) {
			this.dictionary = dictionary;
		}
		protected override object GetXmlValue() {
			object value = Value;
			if(dictionary != null) {
				XPClassInfo ci = dictionary.QueryClassInfo(value);
				if(ci != null && ci.KeyProperty != null) {
					return ci.GetId(value);
				}
			}
			return value;
		}
	}
	public class ParameterOperandValue : OperandValue {
		Func<object, object> getter;
		public Func<object, object> Getter { get { return getter; } }
		public object BaseValue { get { return base.Value; } }
		public ParameterOperandValue() { getter = (o) => BaseValue; }
		public ParameterOperandValue(object value) : this(value, v => v) {
		}
		public ParameterOperandValue(object value, Func<object, object> getter)
			: base(value) {
			this.getter = getter;
		}
		public override object Value {
			get {
				return getter(base.Value);
			}
		}
	}
	[EditorBrowsable(EditorBrowsableState.Never)]
	public class MemeberAccessOperator : MemberAccessOperator {
	}
	public class MemberAccessOperator : RefCompiler {
		static Dictionary<MemberInfo, Func<object, object>> compiledExpressions = new Dictionary<MemberInfo, Func<object, object>>();
		MemberExpression memberExpression;
		Func<object, object> expression;
		public MemberAccessOperator(MemberExpression expression) {
			memberExpression = expression;
		}
		public MemberAccessOperator() { expression = GetBaseValue; }
		object GetBaseValue(object value) {
			return base.Value;
		}
		public override object Value {
			get {
				if(expression == null) {
					expression = GetExpression(memberExpression.Member, memberExpression.Expression.Type);
				}
				return expression(memberExpression == null ? null : ((ConstantExpression)memberExpression.Expression).Value);
			}
		}
		public static Func<object, object> GetExpression(MemberInfo member, Type t) {
			lock(compiledExpressions) {
				Func<object, object> expression;
				if(!compiledExpressions.TryGetValue(member, out expression)) {
					var param = Expression.Parameter(typeof(object), "value");
					Expression obj = t.IsValueType ? Expression.Convert(param, member.DeclaringType) : Expression.TypeAs(param, member.DeclaringType);
					Expression<Func<object, object>> l = Expression.Lambda<Func<object, object>>(Expression.Convert(Expression.MakeMemberAccess(obj, member), typeof(object)), param);
					expression = l.Compile();
					compiledExpressions.Add(member, expression);
				}
				return expression;
			}
		}
	}
	public class ConstantCompiler : RefCompiler {
		Expression e;
		public Expression Expression {
			get { return e; }
		}
		Func<object> expression;
		public ConstantCompiler() { expression = GetBaseValue; }
		object GetBaseValue() {
			return base.Value;
		}
		public ConstantCompiler(XPDictionary dictionary, Expression expression)
			: base(dictionary) {
			e = expression;
		}
		public override object Value {
			get {
				if(expression == null) {
					Expression<Func<object>> l = Expression.Lambda<Func<object>>(Expression.Convert(e, typeof(object)));
					expression = l.Compile();
				}
				return expression();
			}
		}
	}
	[XmlInclude(typeof(MemberAccessOperator))]
	[XmlInclude(typeof(MemeberAccessOperator))]
	[XmlInclude(typeof(ConstantCompiler))]
	[XmlInclude(typeof(ConstantValue))]
	[XmlInclude(typeof(QuerySet))]
	[XmlInclude(typeof(FreeQuerySet))]
	[XmlInclude(typeof(GroupSet))]
	[XmlInclude(typeof(ParameterOperandValue))]
	[XmlInclude(typeof(InOperatorCompiler))]
	public class XPQueryData {
		CriteriaOperator criteria;
		public CriteriaOperator Criteria {
			get { return criteria; }
			set { criteria = value; }
		}
		CriteriaOperator groupKey;
		public CriteriaOperator GroupKey {
			get { return groupKey; }
			set { groupKey = value; }
		}
		CriteriaOperator groupCriteria;
		public CriteriaOperator GroupCriteria {
			get { return groupCriteria; }
			set { groupCriteria = value; }
		}
		MemberInitOperator projection;
		public MemberInitOperator Projection {
			get { return projection; }
			set { projection = value; }
		}
		SortingCollection sorting;
		public SortingCollection Sorting {
			get { return sorting; }
			set { sorting = value; }
		}
		string objectTypeName;
		[XmlAttribute]
		public string ObjectTypeName {
			get { return objectTypeName; }
			set { objectTypeName = value; }
		}
		int? top;
		[XmlIgnore]
		public int? Top {
			get { return top; }
			set { top = value; }
		}
		int? skip;
		[XmlIgnore]
		public int? Skip {
			get { return skip; }
			set { skip = value; }
		}
		[XmlAttribute]
		public string TopValue {
			get { return top.HasValue ? top.Value.ToString(CultureInfo.InvariantCulture) : String.Empty; }
			set { top = string.IsNullOrEmpty(value) ? (int?)null : int.Parse(value); }
		}
		[XmlAttribute]
		public string SkipValue {
			get { return skip.HasValue ? skip.Value.ToString(CultureInfo.InvariantCulture) : String.Empty; }
			set { skip = string.IsNullOrEmpty(value) ? (int?)null : int.Parse(value); }
		}
		bool inTransaction;
		[XmlAttribute]
		public bool InTransaction {
			get { return inTransaction; }
			set { inTransaction = value; }
		}
		bool withDeleted;
		[XmlAttribute]
		public bool WithDeleted {
			get { return withDeleted; }
			set { withDeleted = value; }
		}
		HashSet<CriteriaOperator> existingJoins;
		public HashSet<CriteriaOperator> ExistingJoins {
			get { return existingJoins; }
			set { existingJoins = value; }
		}
	}
	class DownLevelReprocessor : IClientCriteriaVisitor<CriteriaOperator> {
		Dictionary<JoinOperandInfo, bool> joinOperandList;
		public Dictionary<JoinOperandInfo, bool> JoinOperandList {
			get { return joinOperandList; }
		}
		public static CriteriaOperator Reprocess(CriteriaOperator criteria, out Dictionary<JoinOperandInfo, bool> joinOperandList) {
			DownLevelReprocessor processor = new DownLevelReprocessor();
			CriteriaOperator result = processor.Process(criteria);
			joinOperandList = processor.JoinOperandList;
			return result;
		}
		public CriteriaOperator Process(CriteriaOperator criteria) {
			if(ReferenceEquals(criteria, null))
				return null;
			return criteria.Accept(this);
		}
		CriteriaOperator IClientCriteriaVisitor<CriteriaOperator>.Visit(JoinOperand theOperand) {
			if(joinOperandList == null) joinOperandList = new Dictionary<JoinOperandInfo, bool>();
			joinOperandList[new JoinOperandInfo(theOperand)] = true;
			if(theOperand.AggregateType != Aggregate.Custom) {
				return Process(theOperand.AggregatedExpression);
			}
			else {
				if(theOperand.CustomAggregateOperands.Count == 1) {
					return Process(theOperand.CustomAggregateOperands[0]);
				}
				JoinOperand result = new JoinOperand(theOperand.JoinTypeName, theOperand.Condition, theOperand.CustomAggregateName, new CriteriaOperatorCollection());
				ProcessOperands(result.CustomAggregateOperands, theOperand.CustomAggregateOperands);
				return result;
			}
		}
		CriteriaOperator IClientCriteriaVisitor<CriteriaOperator>.Visit(AggregateOperand theOperand) {
			throw new NotImplementedException();
		}
		CriteriaOperator IClientCriteriaVisitor<CriteriaOperator>.Visit(OperandProperty theOperand) {
			return new OperandProperty("^." + theOperand.PropertyName);
		}
		void ProcessOperands(CriteriaOperatorCollection newCollection, CriteriaOperatorCollection oldCollection) {
			int count = oldCollection.Count;
			for(int i = 0; i < count; i++) {
				newCollection.Add(Process(oldCollection[i]));
			}
		}
		CriteriaOperator ICriteriaVisitor<CriteriaOperator>.Visit(FunctionOperator theOperator) {
			FunctionOperator result = new FunctionOperator(theOperator.OperatorType);
			ProcessOperands(result.Operands, theOperator.Operands);
			return result;
		}
		CriteriaOperator ICriteriaVisitor<CriteriaOperator>.Visit(OperandValue theOperand) {
			return theOperand is ConstantValue ? new ConstantValue(theOperand.Value) : new OperandValue(theOperand.Value);
		}
		CriteriaOperator ICriteriaVisitor<CriteriaOperator>.Visit(GroupOperator theOperator) {
			GroupOperator result = new GroupOperator(theOperator.OperatorType);
			ProcessOperands(result.Operands, theOperator.Operands);
			return result;
		}
		CriteriaOperator ICriteriaVisitor<CriteriaOperator>.Visit(InOperator theOperator) {
			InOperator result = new InOperator(Process(theOperator.LeftOperand));
			ProcessOperands(result.Operands, theOperator.Operands);
			return result;
		}
		CriteriaOperator ICriteriaVisitor<CriteriaOperator>.Visit(UnaryOperator theOperator) {
			return new UnaryOperator(theOperator.OperatorType, Process(theOperator.Operand));
		}
		CriteriaOperator ICriteriaVisitor<CriteriaOperator>.Visit(BinaryOperator theOperator) {
			return new BinaryOperator(Process(theOperator.LeftOperand), Process(theOperator.RightOperand), theOperator.OperatorType);
		}
		CriteriaOperator ICriteriaVisitor<CriteriaOperator>.Visit(BetweenOperator theOperator) {
			return new BetweenOperator(Process(theOperator.TestExpression), Process(theOperator.BeginExpression), Process(theOperator.EndExpression));
		}
	}
	class ParamExpression {
		string[] names;
		object[] values;
		bool used;
		public ParamExpression(string[] names, object[] values) {
			this.names = names;
			this.values = values;
		}
		public bool Use() {
			bool prev = used;
			used = false;
			return prev;
		}
		public bool UnUse(bool value) {
			bool prev = used;
			if(value)
				used = value;
			return prev;
		}
		public void SetUsed() {
			used = true;
		}
		public bool MemeberAccessOperator(MemberExpression expression, out object value) {
			int index = Array.IndexOf<string>(names, expression.Member.Name);
			if(index < 0) {
				value = null;
				return false;
			}
			else {
				value = values[index];
				SetUsed();
				return true;
			}
		}
	}
	abstract class CachedQueryBase {
		MethodCallExpression nextExpression;
		protected CachedQueryBase next;
		readonly string[] names;
		MethodInfo[] converters;
		Expression[] arguments;
		class MemberAccessChecker : ExpressionVisitor {
			string[] names;
			bool memberAccessed;
			MemberAccessChecker(string[] names) {
				this.names = names;
			}
			protected override Expression VisitMember(MemberExpression node) {
				if(Array.IndexOf<string>(names, node.Member.Name) >= 0) {
					memberAccessed = true;
				}
				return base.VisitMember(node);
			}
			public static bool HasParameters(Expression e, string[] names) {
				MemberAccessChecker v = new MemberAccessChecker(names);
				v.Visit(e);
				return v.memberAccessed;
			}
		}
		static readonly Expression emptyParams = Expression.Constant(new ParamExpression(Array.Empty<string>(), Array.Empty<object>()));
		protected void SetExpression(Expression expression) {
			nextExpression = (MethodCallExpression)expression;
			converters = new MethodInfo[nextExpression.Arguments.Count];
			for(int i = 1; i < converters.Length; i++) {
				converters[i] = mi.MakeGenericMethod(nextExpression.Arguments[i].Type);
			}
			if(!MemberAccessChecker.HasParameters(expression, names)) {
				arguments = new Expression[nextExpression.Arguments.Count];
				for(int i = 1; i < arguments.Length; i++) {
					arguments[i] = Expression.Call(converters[i], nextExpression.Arguments[i], emptyParams);
				}
			}
		}
		static MethodInfo mi;
		static T GetFirst<T>(T value, ParamExpression param) {
			return value;
		}
		static CachedQueryBase() {
			mi = typeof(CachedQueryBase).GetMethod("GetFirst", BindingFlags.Static | BindingFlags.NonPublic);
		}
		public object Process(IQueryable source, params object[] values) {
			return Process(source, Expression.Constant(new ParamExpression(names, values)));
		}
		public object Process(IQueryable source) {
			return Process(source, (Expression)null);
		}
		protected object Process(IQueryable source, Expression values) {
			MethodCallExpression e = nextExpression;
			if(e == null) {
				return source;
			}
			Expression[] args = new Expression[e.Arguments.Count];
			args[0] = Expression.Constant(source);
			for(int i = 1; i < args.Length; i++) {
				args[i] = arguments == null ? Expression.Call(converters[i], e.Arguments[i], values) : arguments[i];
			}
			e = Expression.Call(e.Method, args);
			if(next != null) {
				return next.Process(source.Provider, e, values);
			}
			return source.Provider.Execute(e);
		}
		protected abstract object Process(IQueryProvider iQueryProvider, MethodCallExpression e, Expression values);
		protected CachedQueryBase(MethodInfo method) {
			ParameterInfo[] parameters = method.GetParameters();
			names = new string[parameters.Length - 1];
			for(int i = 0; i < parameters.Length - 1; i++) {
				names[i] = parameters[i + 1].Name;
			}
		}
		protected CachedQueryBase(CachedQueryBase baseQuery) {
			this.names = baseQuery.names;
		}
	}
	sealed class CachedQuery<T> : CachedQueryBase, IOrderedQueryable<T>, IQueryProvider {
		CachedQuery(CachedQueryBase baseQuery)
			: base(baseQuery) {
		}
		public CachedQuery(MethodInfo method)
			: base(method) {
		}
		protected override object Process(IQueryProvider provider, MethodCallExpression e, Expression values) {
			return Process(provider.CreateQuery<T>(e), values);
		}
		public IQueryable<TElement> CreateQuery<TElement>(Expression expression) {
			SetExpression(expression);
			CachedQuery<TElement> query = new CachedQuery<TElement>(this);
			next = query;
			return query;
		}
		public TResult Execute<TResult>(Expression expression) {
			SetExpression(expression);
			return default(TResult);
		}
		public Expression Expression {
			get { return Expression.Constant(this); }
		}
		public IQueryProvider Provider {
			get { return this; }
		}
		public IEnumerator<T> GetEnumerator() {
			throw new NotImplementedException();
		}
		IEnumerator IEnumerable.GetEnumerator() {
			throw new NotImplementedException();
		}
		public Type ElementType {
			get { throw new NotImplementedException(); }
		}
		public IQueryable CreateQuery(Expression expression) {
			throw new NotImplementedException();
		}
		public object Execute(Expression expression) {
			throw new NotImplementedException();
		}
	}
	public class XPQueryExecutePreprocessor : ClientCriteriaVisitorBase, ILinqExtendedCriteriaVisitor<CriteriaOperator> {
		static XPQueryExecutePreprocessor Instance = new XPQueryExecutePreprocessor();
		public static CriteriaOperator Preprocess(CriteriaOperator criteria) {
			return Instance.Process(criteria);
		}
		public static CriteriaOperatorCollection Preprocess(CriteriaOperatorCollection criteriaCollection) {
			if(criteriaCollection == null) {
				return null;
			}
			bool modified;
			var resultCollection = Instance.ProcessCollection(criteriaCollection, out modified);
			return modified ? resultCollection : criteriaCollection;
		}
		public static SortingCollection Preprocess(SortingCollection sortingCollection) {
			if(sortingCollection == null) {
				return null;
			}
			bool modified = false;
			SortingCollection resultCollection = new SortingCollection();
			foreach(SortProperty sortProperty in sortingCollection) {
				CriteriaOperator resultProperty = Instance.Process(sortProperty.Property);
				if(!ReferenceEquals(resultProperty, sortProperty.Property)) {
					modified = true;
					resultCollection.Add(new SortProperty(resultProperty, sortProperty.Direction));
				}
				else {
					resultCollection.Add(sortProperty);
				}
			}
			return modified ? resultCollection : sortingCollection;
		}
		public CriteriaOperator Visit(MemberInitOperator theOperand) {
			return theOperand;
		}
		public CriteriaOperator Visit(ExpressionAccessOperator theOperand) {
			return theOperand;
		}
		public CriteriaOperator Visit(QuerySet theOperand) {
			return theOperand;
		}
		bool CheckOperandValueEqualToNullSafe(CriteriaOperator co) {
			OperandValue operandValue = co as OperandValue;
			if(!ReferenceEquals(operandValue, null)) {
				try {
					return operandValue.Value == null;
				}
				catch {
					return false;
				}
			}
			return false;
		}
		protected override CriteriaOperator Visit(BinaryOperator theOperator) {
			CriteriaOperator left = Process(theOperator.LeftOperand);
			CriteriaOperator right = Process(theOperator.RightOperand);
			if(theOperator.OperatorType == BinaryOperatorType.Equal || theOperator.OperatorType == BinaryOperatorType.NotEqual) {
				if(CheckOperandValueEqualToNullSafe(right)) {
					return theOperator.OperatorType == BinaryOperatorType.NotEqual ? new NotOperator(new NullOperator(left)) : (CriteriaOperator)new NullOperator(left);
				}
				if(CheckOperandValueEqualToNullSafe(left)) {
					return theOperator.OperatorType == BinaryOperatorType.NotEqual ? new NotOperator(new NullOperator(right)) : (CriteriaOperator)new NullOperator(right);
				}
			}
			if(ReferenceEquals(left, theOperator.LeftOperand) && ReferenceEquals(right, theOperator.RightOperand)) {
				return theOperator;
			}
			return new BinaryOperator(left, right, theOperator.OperatorType);
		}
		protected override CriteriaOperator Visit(UnaryOperator theOperator) {
			CriteriaOperator operand = Process(theOperator.Operand);
			UnaryOperator unaryOperator = operand as UnaryOperator;
			if(!ReferenceEquals(unaryOperator, null) && unaryOperator.OperatorType == UnaryOperatorType.Not) {
				return unaryOperator.Operand;
			}
			return ReferenceEquals(operand, theOperator.Operand) ? theOperator : new UnaryOperator(theOperator.OperatorType, operand);
		}
	}
	public class PropertyValueConverterFinder : ContextClientCriteriaVisitorBase<ValueConverter> {
		public PropertyValueConverterFinder(XPClassInfo classInfo)
			: base(classInfo) {
		}
		public override ValueConverter Process(CriteriaOperator criteria) {
			if(ReferenceEquals(criteria, null)) {
				return null;
			}
			return criteria.Accept(this);
		}
		public override ValueConverter VisitInternalProperty(string propertyName) {
			if(string.IsNullOrEmpty(propertyName)) {
				return null;
			}
			var miPath = CurrentClassInfo.ParsePath(propertyName);
			if(miPath.Count == 0) {
				return null;
			}
			return miPath[miPath.Count - 1].Converter;
		}
		public override ValueConverter VisitInternalAggregate(ValueConverter collectionPropertyResult, ValueConverter aggregateResult, Aggregate aggregateType, ValueConverter conditionResult) {
			if(aggregateType == Aggregate.Count || aggregateType == Aggregate.Exists)
				return null;
			return aggregateResult;
		}
		public override ValueConverter VisitInternalAggregate(ValueConverter collectionPropertyResult, IEnumerable<ValueConverter> aggregateResult, string customAggregateName, ValueConverter conditionResult) {
			return aggregateResult.FirstOrDefault();
		}
		public override ValueConverter VisitInternalJoinOperand(ValueConverter conditionResult, ValueConverter agregatedResult, Aggregate aggregateType) {
			if(aggregateType == Aggregate.Count || aggregateType == Aggregate.Exists)
				return null;
			return agregatedResult;
		}
		public override ValueConverter VisitInternalJoinOperand(ValueConverter conditionResult, IEnumerable<ValueConverter> agregatedResult, string customAggregateName) {
			return agregatedResult.FirstOrDefault();
		}
		public override ValueConverter VisitInternalBetween(BetweenOperator theOperator) {
			return null;
		}
		public override ValueConverter VisitInternalBinary(ValueConverter left, ValueConverter right, BinaryOperatorType operatorType) {
			return null;
		}
		public override ValueConverter VisitInternalFunction(FunctionOperator theOperator) {
			return null;
		}
		public override ValueConverter VisitInternalGroup(GroupOperatorType operatorType, List<ValueConverter> results) {
			return null;
		}
		public override ValueConverter VisitInternalInOperator(InOperator theOperator) {
			return null;
		}
		public override ValueConverter VisitInternalOperand(object value) {
			return null;
		}
		public override ValueConverter VisitInternalUnary(UnaryOperator theOperator) {
			return null;
		}
	}
	class GetTimeOfDayValueFinder : IClientCriteriaVisitor<bool> {
		static readonly GetTimeOfDayValueFinder instance = new GetTimeOfDayValueFinder();
		public static bool TryToFind(CriteriaOperator criteria) {
			return instance.Process(criteria);
		}
		bool Process(CriteriaOperator criteria) {
			if(ReferenceEquals(criteria, null))
				return false;
			return (bool)criteria.Accept(this);
		}
		bool IClientCriteriaVisitor<bool>.Visit(JoinOperand theOperand) {
			if(theOperand.AggregateType == Aggregate.Count || theOperand.AggregateType == Aggregate.Exists)
				return false;
			if(theOperand.AggregateType != Aggregate.Custom) {
				return Process(theOperand.AggregatedExpression);
			}
			foreach(var op in theOperand.CustomAggregateOperands) {
				if(Process(op)) {
					return true;
				}
			}
			return false;
		}
		bool ICriteriaVisitor<bool>.Visit(BinaryOperator theOperator) {
			switch(theOperator.OperatorType) {
				case BinaryOperatorType.Equal:
				case BinaryOperatorType.Greater:
				case BinaryOperatorType.GreaterOrEqual:
				case BinaryOperatorType.Less:
				case BinaryOperatorType.LessOrEqual:
#pragma warning disable 618
				case BinaryOperatorType.Like:
#pragma warning restore 618
				case BinaryOperatorType.NotEqual:
					return false;
			}
			return Process(theOperator.LeftOperand) || Process(theOperator.RightOperand);
		}
		bool ICriteriaVisitor<bool>.Visit(InOperator theOperator) {
			return false;
		}
		bool ICriteriaVisitor<bool>.Visit(OperandValue theOperand) {
			return false;
		}
		bool ICriteriaVisitor<bool>.Visit(FunctionOperator theOperator) {
			switch(theOperator.OperatorType) {
				case FunctionOperatorType.GetTimeOfDay:
					return true;
				case FunctionOperatorType.Iif:
					for(int i = 1; i < theOperator.Operands.Count; i += 2) {
						if(Process(theOperator.Operands[i]) || Process(theOperator.Operands[i + 1])) {
							return true;
						}
					}
					break;
				case FunctionOperatorType.IsNull:
					if(theOperator.Operands.Count >= 2) {
						return Process(theOperator.Operands[0]) || Process(theOperator.Operands[1]);
					}
					break;
				case FunctionOperatorType.Abs:
				case FunctionOperatorType.BigMul:
				case FunctionOperatorType.Ceiling:
				case FunctionOperatorType.Floor:
				case FunctionOperatorType.Max:
				case FunctionOperatorType.Min:
				case FunctionOperatorType.Power:
				case FunctionOperatorType.Sqr:
				case FunctionOperatorType.ToDecimal:
				case FunctionOperatorType.ToDouble:
				case FunctionOperatorType.ToFloat:
				case FunctionOperatorType.ToInt:
				case FunctionOperatorType.ToLong:
					for(int i = 1; i < theOperator.Operands.Count; i++) {
						if(Process(theOperator.Operands[i])) {
							return true;
						}
					}
					break;
			}
			return false;
		}
		bool ICriteriaVisitor<bool>.Visit(GroupOperator theOperator) {
			return false;
		}
		bool ICriteriaVisitor<bool>.Visit(UnaryOperator theOperator) {
			if(theOperator.OperatorType == UnaryOperatorType.Minus || theOperator.OperatorType == UnaryOperatorType.Plus || theOperator.OperatorType == UnaryOperatorType.BitwiseNot)
				return Process(theOperator.Operand);
			return false;
		}
		bool ICriteriaVisitor<bool>.Visit(BetweenOperator theOperator) {
			return false;
		}
		bool IClientCriteriaVisitor<bool>.Visit(OperandProperty theOperand) {
			return false;
		}
		bool IClientCriteriaVisitor<bool>.Visit(AggregateOperand theOperand) {
			if(theOperand.AggregateType == Aggregate.Count || theOperand.AggregateType == Aggregate.Exists)
				return false;
			if(theOperand.AggregateType != Aggregate.Custom) {
				return Process(theOperand.AggregatedExpression);
			}
			foreach(var op in theOperand.CustomAggregateOperands) {
				if(Process(op)) {
					return true;
				}
			}
			return false;
		}
	}
}
namespace DevExpress.Xpo {
	internal class XPQueryPostEvaluatorCore : ExpressionEvaluatorCore {
		public XPQueryPostEvaluatorCore(bool caseSensitive, EvaluateCustomFunctionHandler customFunctionHandler)
			: base(caseSensitive, customFunctionHandler) {
		}
		public XPQueryPostEvaluatorCore(bool caseSensitive, EvaluateCustomFunctionHandler customFunctionHandler, CustomAggregateResolveHandler customAggregateResolveHandler)
			: base(caseSensitive, customFunctionHandler, customAggregateResolveHandler) {
		}
		public override object Visit(OperandProperty theOperand) {
			EvaluatorProperty property = PropertyCache[theOperand];
			return GetContext(property.UpDepth).GetPropertyValue(property);
		}
	}
	internal class XPQueryPostEvaluator : ExpressionEvaluator {
		readonly ExpressionEvaluatorCoreBase evalCore;
		protected override ExpressionEvaluatorCoreBase EvaluatorCore {
			get { return evalCore; }
		}
		public XPQueryPostEvaluator(EvaluatorContextDescriptor descriptor, CriteriaOperator criteria, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions)
			: base(descriptor, criteria, caseSensitive, false, customFunctions) {
			evalCore = new XPQueryPostEvaluatorCore(caseSensitive, new EvaluateCustomFunctionHandler(EvaluateCustomFunction), new CustomAggregateResolveHandler(ResolveCustomAggregate));
		}
		public XPQueryPostEvaluator(EvaluatorContextDescriptor descriptor, CriteriaOperator criteria, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions, ICollection<ICustomAggregate> customAggregates)
			: base(descriptor, criteria, caseSensitive, false, customFunctions, customAggregates) {
			evalCore = new XPQueryPostEvaluatorCore(caseSensitive, new EvaluateCustomFunctionHandler(EvaluateCustomFunction), new CustomAggregateResolveHandler(ResolveCustomAggregate));
		}
	}
	public abstract class XPQueryBase : IPersistentValueExtractor, IInfrastructure<IServiceProvider> {
		static readonly CriteriaOperator oneConstantValue = new ConstantValue(1);
		XPQueryData query;
		Session session;
		IDataLayer layer;
		XPDictionary dictionary;
		[Description("Gets or sets the XPDictionary class descendant’s instance which provides metadata on persistent objects in a data store.")]
		public XPDictionary Dictionary {
			get { return dictionary; }
		}
		Session GetSession() {
			if(session == null)
				throw new InvalidOperationException(Res.GetString(Res.LinqToXpo_SessionIsNull));
			return session;
		}
		IDataLayer GetLayer() {
			if(session == null)
				throw new InvalidOperationException(Res.GetString(Res.LinqToXpo_SessionIsNull));
			return layer;
		}
		public static bool SuppressNonPersistentPropertiesCheck;
		[Description("Gets or sets the session that is used to retrieve persistent objects in queries.")]
		public Session Session {
			get {
				return session;
			}
			set {
				if(dictionary != value.Dictionary)
					throw new ArgumentException(Res.GetString(Res.Helpers_SameDictionaryExpected));
				session = value;
			}
		}
		protected IDataLayer DataLayer {
			get { return layer; }
		}
		CriteriaOperator Criteria {
			get { return query.Criteria; }
			set { query.Criteria = value; }
		}
		internal CriteriaOperator GroupKey {
			get { return query.GroupKey; }
			set { query.GroupKey = value; }
		}
		bool IsGroup {
			get { return !IsNull(query.GroupKey); }
		}
		CriteriaOperator GroupCriteria {
			get { return query.GroupCriteria; }
			set { query.GroupCriteria = value; }
		}
		internal MemberInitOperator Projection {
			get { return query.Projection; }
			set { query.Projection = value; }
		}
		SortingCollection Sorting {
			get { return query.Sorting; }
			set { query.Sorting = value; }
		}
		XPClassInfo objectClassInfo;
		protected XPClassInfo ObjectClassInfo {
			get { return objectClassInfo; }
		}
		int? Top {
			get { return query.Top; }
			set { query.Top = value; }
		}
		int? Skip {
			get { return query.Skip; }
			set { query.Skip = value; }
		}
		bool InTransaction {
			get { return query.InTransaction; }
			set { query.InTransaction = value; }
		}
		bool WithDeleted {
			get { return query.WithDeleted; }
			set { query.WithDeleted = value; }
		}
		HashSet<CriteriaOperator> ExistingJoins {
			get { return query.ExistingJoins; }
			set { query.ExistingJoins = value; }
		}
		CustomCriteriaCollection customCriteriaCollection;
		internal CustomCriteriaCollection CustomCriteriaCollection { get { return customCriteriaCollection; } }
		object IPersistentValueExtractor.ExtractPersistentValue(object criterionValue) {
			if(Dictionary.QueryClassInfo(criterionValue) != null)
				throw new DevExpress.Xpo.Exceptions.CannotResolveClassInfoException((criterionValue == null ? "null" : criterionValue.GetType().AssemblyQualifiedName), (criterionValue == null ? "null" : criterionValue.GetType().FullName));
			return criterionValue;
		}
		bool IPersistentValueExtractor.CaseSensitive { get { return false; } }
		List<object[]> SessionSelectData(XPClassInfo classInfo, CriteriaOperatorCollection properties, CriteriaOperator criteria, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria, int skipSelectedRecords, int topSelectedRecords, SortingCollection sorting) {
			if(groupProperties != null) {
				groupProperties = new CriteriaOperatorCollection(groupProperties.Where(x => !x.Is<OperandValue>()));
			}
			properties = XPQueryExecutePreprocessor.Preprocess(properties);
			criteria = XPQueryExecutePreprocessor.Preprocess(criteria);
			groupProperties = XPQueryExecutePreprocessor.Preprocess(groupProperties);
			groupCriteria = XPQueryExecutePreprocessor.Preprocess(groupCriteria);
			sorting = XPQueryExecutePreprocessor.Preprocess(sorting);
			if(InTransaction) {
				return GetSession().SelectDataInTransaction(classInfo, properties, criteria, groupProperties, groupCriteria, WithDeleted, skipSelectedRecords, topSelectedRecords, sorting);
			}
			if(layer != null) {
				if(WithDeleted) {
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_WithDeletedOptionNotSupported));
				}
				List<object[]> data = Session.PrepareSelectDataInternal(classInfo, ref properties, ref criteria, ref groupProperties, ref groupCriteria, ref sorting, true, this);
				if(data != null)
					return data;
				return SimpleObjectLayer.SelectDataInternal(layer, new ObjectsQuery(classInfo, criteria, sorting, skipSelectedRecords, topSelectedRecords, null, true), properties, groupProperties, groupCriteria);
			}
			return GetSession().SelectData(classInfo, properties, criteria, groupProperties, groupCriteria, WithDeleted, skipSelectedRecords, topSelectedRecords, sorting);
		}
		Task<List<object[]>> SessionSelectDataAsync(XPClassInfo classInfo, CriteriaOperatorCollection properties, CriteriaOperator criteria, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria, int skipSelectedRecords, int topSelectedRecords, SortingCollection sorting, CancellationToken cancellationToken = default(CancellationToken)) {
			if(groupProperties != null) {
				groupProperties = new CriteriaOperatorCollection(groupProperties.Where(x => !x.Is<OperandValue>()));
			}
			properties = XPQueryExecutePreprocessor.Preprocess(properties);
			criteria = XPQueryExecutePreprocessor.Preprocess(criteria);
			groupProperties = XPQueryExecutePreprocessor.Preprocess(groupProperties);
			groupCriteria = XPQueryExecutePreprocessor.Preprocess(groupCriteria);
			sorting = XPQueryExecutePreprocessor.Preprocess(sorting);
			cancellationToken.ThrowIfCancellationRequested();
			if(InTransaction) {
				return GetSession().SelectDataInTransactionAsync(classInfo, properties, criteria, groupProperties, groupCriteria, WithDeleted, skipSelectedRecords, topSelectedRecords, sorting, cancellationToken);
			}
			if(layer != null) {
				if(WithDeleted) {
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_WithDeletedOptionNotSupported));
				}
				List<object[]> data = Session.PrepareSelectDataInternal(classInfo, ref properties, ref criteria, ref groupProperties, ref groupCriteria, ref sorting, true, this);
				cancellationToken.ThrowIfCancellationRequested();
				if(data != null) {
					return Task.FromResult(data);
				}
				return SimpleObjectLayer.SelectDataInternalAsync(layer, new ObjectsQuery(classInfo, criteria, sorting, skipSelectedRecords, topSelectedRecords, null, true), properties, groupProperties, groupCriteria, cancellationToken);
			}
			return GetSession().SelectDataAsync(classInfo, properties, criteria, groupProperties, groupCriteria, WithDeleted, skipSelectedRecords, topSelectedRecords, sorting, cancellationToken);
		}
		ICollection SessionGetObjects(XPClassInfo classInfo, CriteriaOperator condition, SortingCollection sorting, int skipSelectedRecords, int topSelectedRecords) {
			condition = XPQueryExecutePreprocessor.Preprocess(condition);
			sorting = XPQueryExecutePreprocessor.Preprocess(sorting);
			if(InTransaction) {
				return GetSession().GetObjectsInTransaction(classInfo, condition, sorting, skipSelectedRecords, topSelectedRecords, WithDeleted);
			}
			return GetSession().GetObjects(classInfo, condition, sorting, skipSelectedRecords, topSelectedRecords, WithDeleted, false);
		}
		Task<ICollection> SessionGetObjectsAsync(XPClassInfo classInfo, CriteriaOperator condition, SortingCollection sorting, int skipSelectedRecords, int topSelectedRecords, CancellationToken cancellationToken = default(CancellationToken)) {
			condition = XPQueryExecutePreprocessor.Preprocess(condition);
			sorting = XPQueryExecutePreprocessor.Preprocess(sorting);
			if(InTransaction) {
				return GetSession().GetObjectsInTransactionAsync(classInfo, condition, sorting, skipSelectedRecords, topSelectedRecords, WithDeleted, cancellationToken);
			}
			return GetSession().GetObjectsAsync(classInfo, condition, sorting, skipSelectedRecords, topSelectedRecords, WithDeleted, false, cancellationToken);
		}
		void SessionGetObjectsAsync(XPClassInfo classInfo, CriteriaOperator condition, SortingCollection sorting, int skipSelectedRecords, int topSelectedRecords, AsyncLoadObjectsCallback callback) {
			condition = XPQueryExecutePreprocessor.Preprocess(condition);
			sorting = XPQueryExecutePreprocessor.Preprocess(sorting);
			if(InTransaction) {
				GetSession().GetObjectsInTransactionAsync(classInfo, condition, sorting, skipSelectedRecords, topSelectedRecords, WithDeleted, callback);
			}
			else {
				GetSession().GetObjectsAsync(classInfo, condition, sorting, skipSelectedRecords, topSelectedRecords, WithDeleted, false, callback);
			}
		}
		object SessionEvaluate(XPClassInfo classInfo, CriteriaOperator expression, CriteriaOperator condition) {
			var result = SessionSelectData(classInfo, new CriteriaOperatorCollection() { expression }, condition, null, null, 0, 1, null);
			return (result == null) || (result.Count == 0) || (result[0] == null) || (result[0].Length == 0) ? null : result[0][0];
		}
		async Task<object> SessionEvaluateAsync(XPClassInfo classInfo, CriteriaOperator expression, CriteriaOperator condition, CancellationToken cancellationToken = default(CancellationToken)) {
			var result = await SessionSelectDataAsync(classInfo, new CriteriaOperatorCollection() { expression }, condition, null, null, 0, 1, null, cancellationToken);
			return (result == null) || (result.Count == 0) || (result[0] == null) || (result[0].Length == 0) ? null : result[0][0];
		}
		object SessionFindObject(XPClassInfo classInfo, CriteriaOperator criteria) {
			criteria = XPQueryExecutePreprocessor.Preprocess(criteria);
			if(InTransaction) {
				return GetSession().FindObject(PersistentCriteriaEvaluationBehavior.InTransaction, ObjectClassInfo, criteria, WithDeleted);
			}
			return GetSession().FindObject(ObjectClassInfo, criteria, WithDeleted);
		}
		Task<object> SessionFindObjectAsync(XPClassInfo classInfo, CriteriaOperator criteria, CancellationToken cancellationToken = default(CancellationToken)) {
			criteria = XPQueryExecutePreprocessor.Preprocess(criteria);
			if(InTransaction) {
				return GetSession().FindObjectAsync(PersistentCriteriaEvaluationBehavior.InTransaction, ObjectClassInfo, criteria, WithDeleted, cancellationToken);
			}
			return GetSession().FindObjectAsync(ObjectClassInfo, criteria, WithDeleted, cancellationToken);
		}
		protected XPQueryBase(XPQueryBase baseQuery)
			: this(baseQuery.Session, baseQuery.layer, baseQuery.Dictionary) {
			Assign(baseQuery);
			create = baseQuery.create;
		}
		protected XPQueryBase(XPQueryBase baseQuery, bool? inTransaction = null, bool? withDeleted = null)
			: this(baseQuery) {
			if(inTransaction.HasValue) {
				InTransaction = inTransaction.Value;
			}
			if(withDeleted.HasValue) {
				WithDeleted = withDeleted.Value;
			}
		}
		protected XPQueryBase(XPQueryBase baseQuery, CustomCriteriaCollection customCriteriaCollection)
			: this(baseQuery) {
			if(this.customCriteriaCollection == null) this.customCriteriaCollection = customCriteriaCollection;
			else this.customCriteriaCollection.Add(customCriteriaCollection);
		}
		protected XPQueryBase(IDataLayer layer, Type type)
			: this(layer.Dictionary, type, false) {
			this.layer = layer;
		}
		protected XPQueryBase(Session session, Type type, bool inTransaction)
			: this(session.Dictionary, type, inTransaction) {
			this.session = session;
		}
		protected XPQueryBase(XPDictionary dictionary, Type type, bool inTransaction) {
			this.dictionary = dictionary;
			query = new XPQueryData();
			query.InTransaction = inTransaction;
			this.Sorting = new SortingCollection();
			objectClassInfo = Dictionary.GetClassInfo(type);
			this.query.ObjectTypeName = objectClassInfo.FullName;
		}
		protected XPQueryBase(Session session, IDataLayer dataLayer, XPDictionary dictionary) {
			this.session = session;
			this.dictionary = dictionary;
			this.layer = dataLayer;
			query = new XPQueryData();
			this.Sorting = new SortingCollection();
		}
		protected XPQueryBase(Session session, IDataLayer dataLayer, XPDictionary dictionary, string data)
			: this(dictionary, data) {
			this.session = session;
			this.layer = dataLayer;
		}
		protected XPQueryBase(XPDictionary dictionary, string data) {
			this.query = SafeXml.Deserialize<XPQueryData>(data);
			this.dictionary = dictionary;
			this.objectClassInfo = Dictionary.GetClassInfo("", query.ObjectTypeName);
		}
		internal static CriteriaOperator GetFreeQuerySet(XPQueryBase query) {
			if(query.IsGroup || query.Skip > 0 || query.Top > 0)
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_X0WithSkipOrTakeOrGroupingIsNotSupported, "Subquery"));
			if(!IsNull(query.Projection)) {
				if(query.Projection.CreateNewObject || query.Projection.Members.Count > 1)
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ComplexDataSelectionIsNotSupportedPerhapsY));
				return new FreeQuerySet(query.ObjectClassInfo.ClassType, query.Criteria) {
					Projection = query.Projection
				};
			}
			return new FreeQuerySet(query.ObjectClassInfo.ClassType, query.Criteria);
		}
		public string Serialize() {
			using(StringWriter textWriter = new StringWriter()) {
				SafeXml.Serialize<XPQueryData>(textWriter, query);
				return textWriter.ToString();
			}
		}
		protected internal void Call(MethodCallExpression call, XPQueryBase prev) {
			Assign(prev);
			Call(call);
		}
		protected void Assign(XPQueryBase prev) {
			Criteria = prev.Criteria;
			for(int i = 0; i < prev.Sorting.Count; i++)
				Sorting.Add(prev.Sorting[i]);
			Projection = prev.Projection;
			GroupCriteria = prev.GroupCriteria;
			query.ObjectTypeName = prev.query.ObjectTypeName;
			objectClassInfo = prev.objectClassInfo;
			Top = prev.Top;
			Skip = prev.Skip;
			GroupKey = prev.GroupKey;
			InTransaction = prev.InTransaction;
			WithDeleted = prev.WithDeleted;
			ExistingJoins = prev.ExistingJoins == null ? null : new HashSet<CriteriaOperator>(prev.ExistingJoins);
			customCriteriaCollection = prev.CustomCriteriaCollection;
		}
		protected static bool IsNull(object val) {
			return val == null;
		}
		CriteriaOperator ParseExpression(Expression expression, params CriteriaOperator[] maps) {
			return Parser.ParseExpression(ObjectClassInfo, customCriteriaCollection, expression, maps);
		}
		CriteriaOperator ParseObjectExpression(Expression expression, params CriteriaOperator[] maps) {
			return Parser.ParseObjectExpression(ObjectClassInfo, customCriteriaCollection, expression, maps);
		}
		void Call(MethodCallExpression call) {
			switch(call.Method.Name) {
				case "Where":
					Where(call);
					break;
				case "OrderBy":
					Order(call, false, SortingDirection.Ascending);
					break;
				case "ThenBy":
					Order(call, true, SortingDirection.Ascending);
					break;
				case "OrderByDescending":
					Order(call, false, SortingDirection.Descending);
					break;
				case "ThenByDescending":
					Order(call, true, SortingDirection.Descending);
					break;
				case "Reverse":
					Reverse();
					break;
				case "Select":
					Select(call);
					break;
				case "Distinct":
					Distinct(call);
					break;
				case "GroupBy":
					GroupBy(call);
					break;
				case "Take":
					Take(call);
					break;
				case "Skip":
					SkipFn(call);
					break;
				case "Join":
					Join(call, false);
					break;
				case "GroupJoin":
					Join(call, true);
					break;
				case "OfType":
					OfType(call);
					break;
				case "SelectMany":
					SelectMany(call);
					break;
				case "Intersect":
					Intersect(call);
					break;
				case "Union":
					Union(call);
					break;
				case "SkipWhile":
				case "TakeWhile":
				case "DefaultIfEmpty":
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_TheX0MethodIsNotSupported, call.Method.Name));
				case "Cast":
					break;
				default:
					throw new ArgumentException(call.Method.Name);
			}
		}
		protected bool CanIntersect() {
			return !Top.HasValue && !Skip.HasValue && Sorting.Count == 0 && IsNull(Projection) && !IsGroup;
		}
		void Union(MethodCallExpression call) {
			XPQueryBase next = (XPQueryBase)((ConstantExpression)call.Arguments[1]).Value;
			if(IsNull(Criteria) || IsNull(next.Criteria))
				Criteria = null;
			else
				Criteria = GroupOperator.Or(Criteria, next.Criteria);
		}
		void Intersect(MethodCallExpression call) {
			XPQueryBase next = (XPQueryBase)((ConstantExpression)call.Arguments[1]).Value;
			Criteria = GroupOperator.And(Criteria, next.Criteria);
		}
		void SelectMany(MethodCallExpression call) {
			if(IsGroup || Top.HasValue || Skip.HasValue)
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_X0WithSkipOrTakeOrGroupingIsNotSupported, "SelectMany"));
			if(call.Arguments.Count < 2)
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_X0WithSoManyParametersIsNotSupported, "SelectMany"));
			CriteriaOperator co = ParseExpression(call.Arguments[1], ParseExpression(call.Arguments[0]));
			QuerySet set = co as QuerySet;
			if(call.Arguments.Count == 3) {
				CriteriaOperator parent = co;
				if(!IsNull(set) && !set.IsEmpty) {
					CriteriaOperator aggregateExpression;
					if(IsNull(set.Projection)) {
						aggregateExpression = new OperandProperty("This");
					}
					else {
						if(!set.TryGetProjectionSingleProperty(out aggregateExpression)) {
							throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, call.Arguments[1], "SelectMany"));
						}
					}
					FreeQuerySet freeQuerySet = set as FreeQuerySet;
					if(IsNull(freeQuerySet)) {
						if(IsNull(set.Property)) {
							throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, call.Arguments[1], "SelectMany"));
						}
						parent = set.CreateCriteriaOperator(aggregateExpression, Aggregate.Single);
					}
					else {
						parent = freeQuerySet.CreateCriteriaOperator(aggregateExpression, Aggregate.Single);
					}
				}
				var projectionOperator = ParseExpression(call.Arguments[2], Projection, parent);
				if(projectionOperator is QuerySet || projectionOperator is IAggregateOperand) {
					Projection = MemberInitOperator.CreateUntypedMemberInitOperator(projectionOperator);
					return;
				}
				var projectionMemberInitOperator = projectionOperator as MemberInitOperator;
				if(!IsNull(projectionMemberInitOperator)) {
					Projection = projectionMemberInitOperator;
					return;
				}
				return;
			}
			if(IsNull(set)) {
				if(!Parser.TryConvertToQuerySet(co, ObjectClassInfo, out set)) {
					var init = co as MemberInitOperator;
					if(!IsNull(init)) {
						Projection = init;
						return;
					}
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, call.Arguments[1], "SelectMany"));
				}
			}
			if(call.Arguments.Count != 2)
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_X0WithSoManyParametersIsNotSupported, "SelectMany"));
			if(IsNull(set.Property)) {
				FreeQuerySet freeSet = set as FreeQuerySet;
				if(!IsNull(freeSet)) {
					if(!IsNull(freeSet.Projection)) {
						foreach(var member in freeSet.Projection.Members) {
							member.Property = freeSet.CreateCriteriaOperator(member.Property, Aggregate.Single);
						}
						Projection = freeSet.Projection;
					}
					else {
						Projection = MemberInitOperator.CreateUntypedMemberInitOperator(freeSet);
					}
					return;
				}
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, call.Arguments[1], "SelectMany"));
			}
			MemberInfoCollection col = MemberInfoCollection.ParsePersistentPath(ObjectClassInfo, set.Property.PropertyName);
			if(col.Count != 1 || !col[0].IsAssociationList)
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, call.Arguments[1], "SelectMany"));
			XPClassInfo firstClassInfo = objectClassInfo;
			objectClassInfo = col[0].CollectionElementType;
			XPMemberInfo associatedMember = col[0].GetAssociatedMember();
			string refMember = associatedMember.Name;
			if(col[0].IsManyToManyAlias || col[0].IsManyToMany) {
				bool useUpcasting = firstClassInfo != associatedMember.CollectionElementType;
				if(useUpcasting) {
					Criteria = CriteriaOperator.And(Criteria, new FunctionOperator(IsInstanceOfTypeFunction.FunctionName, Parser.ThisCriteria, firstClassInfo.FullName));
				}
				Criteria = CriteriaOperator.And(set.Condition, new AggregateOperand(refMember, Aggregate.Exists, Criteria));
				Projection = set.Projection;
				if(Sorting != null && Sorting.Count > 0)
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_SortingIsNotSupportedForSelectManyOperation));
			}
			else {
				bool useUpCasting = associatedMember.ReferenceType != null && firstClassInfo != associatedMember.ReferenceType;
				string refMemberForPatch = useUpCasting ? string.Format("{0}.<{1}>", refMember, firstClassInfo.FullName) : refMember;
				Criteria = CriteriaOperator.And(set.Condition, PatchParentCriteria(Criteria, refMemberForPatch), new NotOperator(new NullOperator(refMember)));
				if(useUpCasting) {
					Criteria = CriteriaOperator.And(Criteria, new FunctionOperator(IsInstanceOfTypeFunction.FunctionName, new OperandProperty(refMember), new OperandValue(firstClassInfo.FullName)));
				}
				Projection = set.Projection;
				if(Sorting != null) {
					foreach(SortProperty s in Sorting)
						s.Property = PatchParentCriteria(s.Property, refMemberForPatch);
				}
			}
		}
		CriteriaOperator PatchParentCriteria(CriteriaOperator criteria, string p) {
			return PersistentCriterionExpander.Prefix(p, criteria);
		}
		void OfType(MethodCallExpression call) {
			Type type = call.Method.GetGenericArguments()[0];
			XPClassInfo newObjectClassInfo = Dictionary.GetClassInfo(type);
			if(!newObjectClassInfo.IsAssignableTo(objectClassInfo))
				throw new InvalidOperationException(); 
			objectClassInfo = newObjectClassInfo;
			this.query.ObjectTypeName = objectClassInfo.FullName;
		}
		void Join(MethodCallExpression call, bool groupJoin) {
			CriteriaOperator p = ParseExpression(call.Arguments[2], Projection);
			XPQueryBase related = ((ConstantExpression)call.Arguments[1]).Value as XPQueryBase;
			if((related.Top.HasValue && related.Top.Value > 0) || (related.Skip.HasValue && related.Skip.Value > 0)) {
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_QueryWithAppliedTheSkipOrTheTakeOperations));
			}
			Type projType = related.ObjectClassInfo.ClassType;
			CriteriaOperator relatedP = related.ParseExpression(call.Arguments[3], related.Projection);
			if(IsNull(p)) {
				OperandProperty prop = relatedP as OperandProperty;
				if(IsNull(prop))
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, call.Arguments[3], "Join")); 
				XPMemberInfo member = Dictionary.GetClassInfo(projType).FindMember(prop.PropertyName);
				if(member == null)
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, call.Arguments[3], "Join")); 
				MemberInitOperator current = (MemberInitOperator)ParseExpression(call.Arguments[4], Projection,
					new QuerySet(new OperandProperty(member.GetAssociatedMember().Name), related.Criteria));
				Projection = current;
			}
			else {
				var freeQuery = new FreeQuerySet(projType, p, relatedP, related.Criteria);
				freeQuery.Projection = related.Projection;
				CriteriaOperator co = ParseExpression(call.Arguments[4], Projection, freeQuery);
				MemberInitOperator current = co as MemberInitOperator;
				if(ReferenceEquals(current, null)) {
					QuerySet qs = co as QuerySet;
					if(ReferenceEquals(qs, null)) {
						XPMemberAssignmentCollection members = new XPMemberAssignmentCollection();
						members.Add(new XPMemberAssignment(co));
						Projection = new MemberInitOperator((Type)null, members, false);
					}
					else {
						FreeQuerySet fqs = qs as FreeQuerySet;
						if(ReferenceEquals(fqs, null)) {
							if(!IsNull(qs.Property))
								throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, call.Arguments[4], "Join")); 
							Projection = qs.Projection;
						}
						else {
							XPMemberAssignmentCollection members = new XPMemberAssignmentCollection();
							members.Add(new XPMemberAssignment(fqs));
							Projection = new MemberInitOperator((Type)null, members, false);
							if(!groupJoin) CheckJoinExists(members[0].Property);
						}
					}
				}
				else {
					if(IsNull(current) || !current.Equals(Projection)) {
						Projection = current;
						if(!IsNull(Projection) && Projection.Members.Count == 2) {
							if(!groupJoin) CheckJoinExists(Projection.Members[1].Property);
						}
					}
				}
			}
		}
		void CheckJoinExists(CriteriaOperator operand) {
			FreeQuerySet freeSet = operand as FreeQuerySet;
			if(!IsNull(freeSet))
				operand = freeSet.CreateCriteriaOperator(new OperandProperty("This"), Aggregate.Single);
			JoinOperand joinOperand = operand as JoinOperand;
			if(IsNull(joinOperand)) return;
			if(ExistingJoins == null)
				ExistingJoins = new HashSet<CriteriaOperator>();
			if(ExistingJoins.Contains(joinOperand))
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_DuplicateJoinOperatorFound));
			ExistingJoins.Add(joinOperand);
		}
		void Take(MethodCallExpression call) {
			int top = GetSafeInt(((OperandValue)ParseExpression(call.Arguments[1])).Value);
			if(!Top.HasValue || top < Top)
				Top = top;
		}
		void SkipFn(MethodCallExpression call) {
			int skip = GetSafeInt(((OperandValue)ParseExpression(call.Arguments[1])).Value);
			if(!Skip.HasValue || skip < Skip)
				Skip = skip;
		}
		void GroupBy(MethodCallExpression call) {
			if(Top.HasValue || Skip.HasValue)
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_X0OperatorAfterSkipOrTakeIsNotSupported, "GroupBy operator"));
			Expression elementSelector = null;
			Expression resultSelector = null;
			for(int i = 2; i < call.Arguments.Count; i++) {
				UnaryExpression quote = call.Arguments[i] as UnaryExpression;
				if(quote == null) {
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_GroupingWithACustomComparerIsNotSupported));
				}
				else {
					LambdaExpression lambda = (LambdaExpression)quote.Operand;
					if(lambda.Parameters.Count == 1) {
						elementSelector = lambda;
					}
					else {
						resultSelector = lambda;
					}
				}
			}
			CriteriaOperator keyOperator = ParseExpression(call.Arguments[1], Projection);
			MemberInitOperator init;
			if(elementSelector != null) {
				CriteriaOperator elementOperator = ParseExpression(elementSelector, Projection);
				QuerySet qs;
				if(elementOperator.Is(out qs) && qs.IsEmpty) {
					Projection = null;
				}
				else {
					Projection = elementOperator.Is(out init) ? init : MemberInitOperator.CreateUntypedMemberInitOperator(elementOperator);
				}
			}
			if(resultSelector != null) {
				CriteriaOperator resultOperator = ParseExpression(resultSelector, keyOperator, new QuerySet(Projection));
				Projection = resultOperator.Is(out init) ? init : MemberInitOperator.CreateUntypedMemberInitOperator(resultOperator);
			}
			GroupKey = keyOperator;
			Sorting.Clear(); 
		}
		void Distinct(MethodCallExpression call) {
			if(Top.HasValue || Skip.HasValue)
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_X0OperatorAfterSkipOrTakeIsNotSupported, "Distinct operator"));
			if(call.Arguments.Count != 1)
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_X0WithSoManyParametersIsNotSupported, "Distinct operator"));
			if(!IsNull(Projection)) {
				GroupKey = Projection;
				Sorting.Clear(); 
			}
		}
		void Select(MethodCallExpression call) {
			QuerySet set = (QuerySet)ParseExpression(call);
			FreeQuerySet freeQuerySet = set as FreeQuerySet;
			if(!ReferenceEquals(freeQuerySet, null) && ReferenceEquals(freeQuerySet.Projection, null)) {
				Projection = MemberInitOperator.CreateUntypedMemberInitOperator(freeQuerySet);
			}
			else {
				Projection = set.Projection;
			}
		}
		void Reverse() {
			if(Sorting.Count > 0) {
				if(Top.HasValue || Skip.HasValue)
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_X0OperatorAfterSkipOrTakeIsNotSupported, "Reverse operator"));
				for(int i = 0; i < Sorting.Count; i++)
					Sorting[i].Direction = Sorting[i].Direction == SortingDirection.Ascending ? SortingDirection.Descending : SortingDirection.Ascending;
			}
		}
		void Order(MethodCallExpression call, bool thenBy, SortingDirection direction) {
			if(call.Arguments.Count > 2)
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_X0WithSoManyParametersIsNotSupported, "OrderBy operator"));
			if(Top.HasValue || Skip.HasValue)
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_X0OperatorAfterSkipOrTakeIsNotSupported, "OrderBy operator"));
			if(!thenBy) {
				Sorting.Clear();
			}
			var sortingCriteria = ParseObjectExpression(call.Arguments[1], ParseExpression(call.Arguments[0]));
			if(Parser.HasExpressionAccess(sortingCriteria))
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, Parser.GetCauseStringOfExpressionAccess(sortingCriteria), string.Concat("the ", call.Method.Name, " clause")));
			List<CriteriaOperator> sortList = new List<CriteriaOperator>();
			sortList.Add(sortingCriteria);
			for(int i = 0; i < sortList.Count; i++) {
				var sorting = sortList[i];
				MemberInitOperator memberInitOperator = sorting as MemberInitOperator;
				if(!IsNull(memberInitOperator)) {
					bool canToAdd = (i + 1) >= sortList.Count;
					int insertCounter = 1;
					foreach(var member in memberInitOperator.Members) {
						if(canToAdd) {
							sortList.Add(member.Property);
						}
						else {
							sortList.Insert(i + insertCounter++, member.Property);
						}
					}
					continue;
				}
				Sorting.Add(new SortProperty(sorting, direction));
			}
		}
		void Where(MethodCallExpression call) {
			if(Top.HasValue || Skip.HasValue)
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_X0OperatorAfterSkipOrTakeIsNotSupported, "Where operator"));
			var whereCriteria = ParseExpression(call);
			if(Parser.HasExpressionAccess(whereCriteria))
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, Parser.GetCauseStringOfExpressionAccess(whereCriteria), "the Where clause"));
			QuerySet val = (QuerySet)whereCriteria;
			if(!IsGroup)
				Criteria = GroupOperator.And(Criteria, val.Condition);
			else
				GroupCriteria = GroupOperator.And(GroupCriteria, val.Condition);
		}
		protected object Execute(Expression expression) {
			if(expression.NodeType == ExpressionType.Call) {
				MethodCallExpression call = (MethodCallExpression)expression;
				if(call.Method.DeclaringType != typeof(Queryable) && call.Method.DeclaringType != typeof(Enumerable)) {
					ICustomAggregate customAgg = GetCustomAggregate(call.Method.Name);
					if(IsValidCustomAggregateQueryable(call, customAgg)) {
						return AggregateCall(call);
					}
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_IncorrectDeclaringTypeX0InTheMethodCallQue, call.Method.DeclaringType));
				}
				switch(call.Method.Name) {
					case "Count":
						return Count(call);
					case "LongCount":
						return LongCount(call);
					case "Min":
					case "Max":
					case "Sum":
						return AggregateCall(call);
					case "Contains":
						return Contains(call);
					case "Average":
						return Average(call);
					case "FirstOrDefault":
						return ExecuteSingle(call, true, SortAction.Sort);
					case "First":
						return ExecuteSingle(call, false, SortAction.Sort);
					case "Single":
						return ExecuteSingle(call, false, SortAction.None);
					case "SingleOrDefault":
						return ExecuteSingle(call, true, SortAction.None);
					case "Last":
						return ExecuteSingle(call, false, SortAction.Reverse);
					case "LastOrDefault":
						return ExecuteSingle(call, true, SortAction.Reverse);
					case "All":
						return All(call);
					case "Any":
						return Any(call);
					case "ElementAt":
						return ExecuteElementAt(call, false);
					case "ElementAtOrDefault":
						return ExecuteElementAt(call, true);
					case "Aggregate":
					case "SequenceEqual":
						throw new NotSupportedException(Res.GetString(Res.LinqToXpo_TheX0MethodIsNotSupported, call.Method.Name));
					default:
						throw new ArgumentException(call.Method.Name);
				}
			}
			throw new NotSupportedException();
		}
		protected internal async Task<object> ExecuteAsync(Expression expression, CancellationToken cancellationToken = default(CancellationToken)) {
			if(expression.NodeType == ExpressionType.Call) {
				MethodCallExpression call = (MethodCallExpression)expression;
				if(call.Method.DeclaringType != typeof(Queryable) && call.Method.DeclaringType != typeof(Enumerable)) {
					ICustomAggregate customAgg = GetCustomAggregate(call.Method.Name);
					if(IsValidCustomAggregateQueryable(call, customAgg)) {
						return await AggregateCallAsync(call, cancellationToken);
					}
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_IncorrectDeclaringTypeX0InTheMethodCallQue, call.Method.DeclaringType));
				}
				switch(call.Method.Name) {
					case "Count":
						return await CountAsync(call, cancellationToken).ConfigureAwait(false);
					case "LongCount":
						return await LongCountAsync(call, cancellationToken).ConfigureAwait(false);
					case "Min":
					case "Max":
					case "Sum":
						return await AggregateCallAsync(call, cancellationToken).ConfigureAwait(false);
					case "Contains":
						return await ContainsAsync(call, cancellationToken).ConfigureAwait(false);
					case "Average":
						return await AverageAsync(call, cancellationToken).ConfigureAwait(false);
					case "FirstOrDefault":
						return await ExecuteSingleAsync(call, true, SortAction.Sort, cancellationToken).ConfigureAwait(false);
					case "First":
						return await ExecuteSingleAsync(call, false, SortAction.Sort, cancellationToken).ConfigureAwait(false);
					case "Single":
						return await ExecuteSingleAsync(call, false, SortAction.None, cancellationToken).ConfigureAwait(false);
					case "SingleOrDefault":
						return await ExecuteSingleAsync(call, true, SortAction.None, cancellationToken).ConfigureAwait(false);
					case "Last":
						return await ExecuteSingleAsync(call, false, SortAction.Reverse, cancellationToken).ConfigureAwait(false);
					case "LastOrDefault":
						return await ExecuteSingleAsync(call, true, SortAction.Reverse, cancellationToken).ConfigureAwait(false);
					case "All":
						return await AllAsync(call, cancellationToken).ConfigureAwait(false);
					case "Any":
						return await AnyAsync(call, cancellationToken).ConfigureAwait(false);
					case "ElementAt":
						return await ExecuteElementAtAsync(call, false, cancellationToken).ConfigureAwait(false);
					case "ElementAtOrDefault":
						return await ExecuteElementAtAsync(call, true, cancellationToken).ConfigureAwait(false);
					case "Aggregate":
					case "SequenceEqual":
						throw new NotSupportedException(Res.GetString(Res.LinqToXpo_TheX0MethodIsNotSupported, call.Method.Name));
					default:
						throw new ArgumentException(call.Method.Name);
				}
			}
			throw new NotSupportedException();
		}
		internal ICustomAggregate GetCustomAggregate(string customAggregateName) {
			ICustomAggregate agg = Dictionary.CustomAggregates.GetCustomAggregate(customAggregateName);
			if(agg == null) {
				if(CriteriaOperator.CustomAggregateCount > 0) {
					return CriteriaOperator.GetCustomAggregate(customAggregateName);
				}
			}
			return agg;
		}
		static bool IsValidCustomAggregateQueryable(MethodCallExpression call, ICustomAggregate customAggregate) {
			var customAggregateQueryable = customAggregate as ICustomAggregateQueryable;
			if(customAggregateQueryable == null) {
				return false;
			}
			MethodInfo methodInfo = customAggregateQueryable.GetMethodInfo();
			if(methodInfo.Name == call.Method.Name && methodInfo.DeclaringType == call.Method.DeclaringType) {
				return true;
			}
			return false;
		}
		bool Contains(MethodCallExpression call) {
			if(Top.HasValue || Skip.HasValue)
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_X0OperatorAfterSkipOrTakeIsNotSupported, "Contains operator"));
			if(IsNull(Projection)) {
				return GetSafeBool(SessionEvaluate(ObjectClassInfo,
					new BinaryOperator(AggregateOperand.TopLevel(Aggregate.Count), new ConstantValue(0), BinaryOperatorType.Greater),
					GroupOperator.And(Criteria,
					new BinaryOperator(new OperandProperty(ObjectClassInfo.KeyProperty.Name), ParseExpression(call.Arguments[1]), BinaryOperatorType.Equal))));
			}
			else {
				Type type = call.Arguments[1].Type;
				MemberInitOperator init;
				if(IsGroup) {
					init = GroupKey as MemberInitOperator;
				}
				else
					init = Projection;
				CriteriaOperator criteria;
				if(IsNull(init))
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_CurrentExpressionWithX0IsNotSupported, "Contains operator")); 
				if(init.GetConstructor(type) == null) {
					if(!type.IsArray)
						criteria = new BinaryOperator(init.Members[0].Property, new ConstantCompiler(Dictionary, call.Arguments[1]), BinaryOperatorType.Equal);
					else {
						CriteriaOperator[] ops = new CriteriaOperator[init.Members.Count];
						for(int i = 0; i < ops.Length; i++)
							ops[i] = new BinaryOperator(init.Members[i].Property, new ConstantCompiler(Dictionary, Expression.ArrayIndex(call.Arguments[1], Expression.Constant(i))), BinaryOperatorType.Equal);
						criteria = GroupOperator.And(ops);
					}
				}
				else {
					if(init.UseConstructor) {
						CriteriaOperator[] ops = new CriteriaOperator[init.Members.Count];
						for(int i = 0; i < ops.Length; i++) {
							MemberInfo m = init.Members[i].GetMember(type);
							MethodInfo mi = m is MethodInfo ? (MethodInfo)m : ((PropertyInfo)m).GetGetMethod();
							ops[i] = new BinaryOperator(init.Members[i].Property, new ConstantCompiler(Dictionary, Expression.Call(call.Arguments[1], mi)), BinaryOperatorType.Equal);
						}
						criteria = GroupOperator.And(ops);
					}
					else
						throw new NotSupportedException(Res.GetString(Res.LinqToXpo_CurrentExpressionWithX0IsNotSupported, "Contains operator")); 
				}
				if(IsGroup) {
					CriteriaOperatorCollection props = new CriteriaOperatorCollection();
					props.Add(new ConstantValue(1));
					List<object[]> data = SessionSelectData(ObjectClassInfo, props, Criteria, GetGrouping(), GroupOperator.And(GroupCriteria, criteria), 0, 1, null);
					return data.Count > 0;
				}
				else
					return GetSafeBool(SessionEvaluate(ObjectClassInfo,
						new BinaryOperator(AggregateOperand.TopLevel(Aggregate.Count), new ConstantValue(0), BinaryOperatorType.Greater),
						GroupOperator.And(Criteria, criteria)));
			}
		}
		async Task<bool> ContainsAsync(MethodCallExpression call, CancellationToken cancellationToken = default(CancellationToken)) {
			if(Top.HasValue || Skip.HasValue)
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_X0OperatorAfterSkipOrTakeIsNotSupported, "Contains operator"));
			if(IsNull(Projection)) {
				object result = await SessionEvaluateAsync(ObjectClassInfo,
					new BinaryOperator(AggregateOperand.TopLevel(Aggregate.Count), new ConstantValue(0), BinaryOperatorType.Greater),
					GroupOperator.And(Criteria,
					new BinaryOperator(new OperandProperty(ObjectClassInfo.KeyProperty.Name), ParseExpression(call.Arguments[1]), BinaryOperatorType.Equal)), cancellationToken).ConfigureAwait(false);
				return GetSafeBool(result);
			}
			else {
				Type type = call.Arguments[1].Type;
				MemberInitOperator init;
				if(IsGroup) {
					init = GroupKey as MemberInitOperator;
				}
				else
					init = Projection;
				CriteriaOperator criteria;
				if(IsNull(init))
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_CurrentExpressionWithX0IsNotSupported, "Contains operator")); 
				if(init.GetConstructor(type) == null) {
					if(!type.IsArray)
						criteria = new BinaryOperator(init.Members[0].Property, new ConstantCompiler(Dictionary, call.Arguments[1]), BinaryOperatorType.Equal);
					else {
						CriteriaOperator[] ops = new CriteriaOperator[init.Members.Count];
						for(int i = 0; i < ops.Length; i++)
							ops[i] = new BinaryOperator(init.Members[i].Property, new ConstantCompiler(Dictionary, Expression.ArrayIndex(call.Arguments[1], Expression.Constant(i))), BinaryOperatorType.Equal);
						criteria = GroupOperator.And(ops);
					}
				}
				else {
					if(init.UseConstructor) {
						CriteriaOperator[] ops = new CriteriaOperator[init.Members.Count];
						for(int i = 0; i < ops.Length; i++) {
							MemberInfo m = init.Members[i].GetMember(type);
							MethodInfo mi = m is MethodInfo ? (MethodInfo)m : ((PropertyInfo)m).GetGetMethod();
							ops[i] = new BinaryOperator(init.Members[i].Property, new ConstantCompiler(Dictionary, Expression.Call(call.Arguments[1], mi)), BinaryOperatorType.Equal);
						}
						criteria = GroupOperator.And(ops);
					}
					else
						throw new NotSupportedException(Res.GetString(Res.LinqToXpo_CurrentExpressionWithX0IsNotSupported, "Contains operator")); 
				}
				if(IsGroup) {
					CriteriaOperatorCollection props = new CriteriaOperatorCollection();
					props.Add(new ConstantValue(1));
					List<object[]> data = await SessionSelectDataAsync(ObjectClassInfo, props, Criteria, GetGrouping(), GroupOperator.And(GroupCriteria, criteria), 0, 1, null, cancellationToken).ConfigureAwait(false);
					return data.Count > 0;
				}
				else {
					object result = await SessionEvaluateAsync(ObjectClassInfo,
						new BinaryOperator(AggregateOperand.TopLevel(Aggregate.Count), new ConstantValue(0), BinaryOperatorType.Greater),
						GroupOperator.And(Criteria, criteria), cancellationToken).ConfigureAwait(false);
					return GetSafeBool(result);
				}
			}
		}
		long LongCount(MethodCallExpression call) {
			return Convert.ToInt64(CountCore(call));
		}
		async Task<long> LongCountAsync(MethodCallExpression call, CancellationToken cancellationToken = default(CancellationToken)) {
			return Convert.ToInt64(await CountCoreAsync(call, cancellationToken).ConfigureAwait(false));
		}
		object CountCore(MethodCallExpression call) {
			if(call.Arguments.Count > 2)
				throw new ArgumentException(Res.GetString(Res.LinqToXpo_X0WithSoManyParametersIsNotSupported, "Count operator"));
			if(ZeroTop)
				return 0;
			if(IsGroup) {
				CriteriaOperatorCollection props = new CriteriaOperatorCollection();
				props.Add(new ConstantValue(1));
				AggregateOperand ag = (AggregateOperand)ParseExpression(call);
				CriteriaOperator criteria = Criteria;
				if(!IsNull(ag.Condition)) {
					if(Top.HasValue || Skip.HasValue)
						throw new NotSupportedException(Res.GetString(Res.LinqToXpo_X0OperatorAfterSkipOrTakeIsNotSupported, "Count operator with condition"));
					criteria = GroupOperator.And(Criteria, ag.Condition);
				}
				return SessionSelectData(ObjectClassInfo, props, criteria, GetGrouping(), GroupCriteria,
					Skip.HasValue ? Skip.Value : 0, Top.HasValue ? Top.Value : 0, null).Count;
			}
			else {
				var ag = ParseExpression(call) as IAggregateOperand;
				if(ag == null) {
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, call.Arguments[0], "the Count method call"));
				}
				var currentClassInfo = ObjectClassInfo;
				if(ag is JoinOperand) {
					XPClassInfo joinClassInfo = null;
					if(!MemberInfoCollection.TryResolveTypeAlsoByShortName((string)ag.AggregationObject, currentClassInfo, out joinClassInfo)) {
						throw new DevExpress.Xpo.Exceptions.CannotResolveClassInfoException(string.Empty, (string)ag.AggregationObject);
					}
					currentClassInfo = joinClassInfo;
				}
				CriteriaOperator criteria = Criteria;
				if(!IsNull(ag.Condition)) {
					if(Top.HasValue || Skip.HasValue)
						throw new NotSupportedException(Res.GetString(Res.LinqToXpo_X0OperatorAfterSkipOrTakeIsNotSupported, "Count operator with condition"));
					criteria = GroupOperator.And(Criteria, ag.Condition);
				}
				int count = GetSafeInt(SessionEvaluate(currentClassInfo, AggregateOperand.TopLevel(ag.AggregateType, ag.AggregatedExpression), criteria));
				int result = count;
				if(Skip.HasValue) {
					result = Math.Max(0, result - Skip.Value);
				}
				if(Top.HasValue) {
					result = Math.Min(result, Top.Value);
				}
				return result;
			}
		}
		async Task<object> CountCoreAsync(MethodCallExpression call, CancellationToken cancellationToken = default(CancellationToken)) {
			if(call.Arguments.Count > 2)
				throw new ArgumentException(Res.GetString(Res.LinqToXpo_X0WithSoManyParametersIsNotSupported, "Count operator"));
			if(ZeroTop)
				return 0;
			if(IsGroup) {
				CriteriaOperatorCollection props = new CriteriaOperatorCollection();
				props.Add(new ConstantValue(1));
				AggregateOperand ag = (AggregateOperand)ParseExpression(call);
				CriteriaOperator criteria = Criteria;
				if(!IsNull(ag.Condition)) {
					if(Top.HasValue || Skip.HasValue)
						throw new NotSupportedException(Res.GetString(Res.LinqToXpo_X0OperatorAfterSkipOrTakeIsNotSupported, "Count operator with condition"));
					criteria = GroupOperator.And(Criteria, ag.Condition);
				}
				List<object[]> list = await SessionSelectDataAsync(ObjectClassInfo, props, criteria, GetGrouping(), GroupCriteria,
					Skip.HasValue ? Skip.Value : 0, Top.HasValue ? Top.Value : 0, null, cancellationToken).ConfigureAwait(false);
				return list.Count;
			}
			else {
				var ag = ParseExpression(call) as IAggregateOperand;
				if(ag == null) {
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, call.Arguments[0], "the Count method call"));
				}
				var currentClassInfo = ObjectClassInfo;
				if(ag is JoinOperand) {
					XPClassInfo joinClassInfo = null;
					if(!MemberInfoCollection.TryResolveTypeAlsoByShortName((string)ag.AggregationObject, currentClassInfo, out joinClassInfo)) {
						throw new DevExpress.Xpo.Exceptions.CannotResolveClassInfoException(string.Empty, (string)ag.AggregationObject);
					}
					currentClassInfo = joinClassInfo;
				}
				CriteriaOperator criteria = Criteria;
				if(!IsNull(ag.Condition)) {
					if(Top.HasValue || Skip.HasValue)
						throw new NotSupportedException(Res.GetString(Res.LinqToXpo_X0OperatorAfterSkipOrTakeIsNotSupported, "Count operator with condition"));
					criteria = GroupOperator.And(Criteria, ag.Condition);
				}
				object obj = await SessionEvaluateAsync(currentClassInfo, AggregateOperand.TopLevel(ag.AggregateType, ag.AggregatedExpression), criteria, cancellationToken).ConfigureAwait(false);
				int count = GetSafeInt(obj);
				int result = count;
				if(Skip.HasValue) {
					result = Math.Max(0, result - Skip.Value);
				}
				if(Top.HasValue) {
					result = Math.Min(result, Top.Value);
				}
				return result;
			}
		}
		static int GetSafeInt(object obj) {
			return obj == null ? 0 : (int)obj;
		}
		static bool GetSafeBool(object obj) {
			return obj == null ? false : (bool)obj;
		}
		bool ZeroTop {
			get { return Top.HasValue && Top.Value <= 0; }
		}
		async Task<int> CountAsync(MethodCallExpression call, CancellationToken cancellationToken = default(CancellationToken)) {
			return Convert.ToInt32(await CountCoreAsync(call, cancellationToken).ConfigureAwait(false));
		}
		int Count(MethodCallExpression call) {
			return Convert.ToInt32(CountCore(call));
		}
		object AggregateCall(MethodCallExpression call) {
			if(IsGroup)
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_X0OverGroupingIsNotSupported, call.Method.Name));
			if(Top.HasValue || Skip.HasValue)
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_X0OperatorAfterSkipOrTakeIsNotSupported, call.Method.Name));
			CriteriaOperator expression = ParseExpression(call);
			var result = SessionEvaluate(ObjectClassInfo, expression, Criteria);
			ValueConverter converter = new PropertyValueConverterFinder(ObjectClassInfo).Process(expression);
			if(converter == null) {
				if(call.Type == typeof(TimeSpan) && result is long && GetTimeOfDayValueFinder.TryToFind(expression)) {
					return new TimeSpan((long)result);
				}
				converter = Dictionary.GetConverter(call.Type);
			}
			if(converter != null) {
				return converter.ConvertFromStorageType(result);
			}
			return result;
		}
		async Task<object> AggregateCallAsync(MethodCallExpression call, CancellationToken cancellationToken = default(CancellationToken)) {
			if(IsGroup)
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_X0OverGroupingIsNotSupported, call.Method.Name));
			if(Top.HasValue || Skip.HasValue)
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_X0OperatorAfterSkipOrTakeIsNotSupported, call.Method.Name));
			CriteriaOperator expression = ParseExpression(call);
			var result = await SessionEvaluateAsync(ObjectClassInfo, expression, Criteria, cancellationToken);
			ValueConverter converter = new PropertyValueConverterFinder(ObjectClassInfo).Process(expression);
			if(converter == null) {
				if(call.Type == typeof(TimeSpan) && result is long && GetTimeOfDayValueFinder.TryToFind(expression)) {
					return new TimeSpan((long)result);
				}
				converter = Dictionary.GetConverter(call.Type);
			}
			if(converter != null) {
				return converter.ConvertFromStorageType(result);
			}
			return result;
		}
		object Average(MethodCallExpression call) {
			object res = AggregateCall(call);
			IConvertible conv = res as IConvertible;
			if(res != null && conv == null && res.GetType() != call.Type)
				throw new InvalidOperationException();
			return conv != null ? conv.ToType(call.Type, CultureInfo.InvariantCulture) : res;
		}
		async Task<object> AverageAsync(MethodCallExpression call, CancellationToken cancellationToken) {
			object res = await AggregateCallAsync(call, cancellationToken).ConfigureAwait(false);
			IConvertible conv = res as IConvertible;
			if(res != null && conv == null && res.GetType() != call.Type)
				throw new InvalidOperationException();
			return conv != null ? conv.ToType(call.Type, CultureInfo.InvariantCulture) : res;
		}
		bool All(MethodCallExpression call) {
			if(IsGroup)
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_X0OverGroupingIsNotSupported, "All operator"));
			CriteriaOperator val = GroupOperator.And(Criteria, new NotOperator(ParseExpression(call.Arguments[1], Projection)));
			return GetSafeInt(SessionEvaluate(ObjectClassInfo, new AggregateOperand(String.Empty, Aggregate.Count), val)) == 0;
		}
		async Task<bool> AllAsync(MethodCallExpression call, CancellationToken cancellationToken = default(CancellationToken)) {
			if(IsGroup)
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_X0OverGroupingIsNotSupported, "All operator"));
			CriteriaOperator val = GroupOperator.And(Criteria, new NotOperator(ParseExpression(call.Arguments[1], Projection)));
			return GetSafeInt(await SessionEvaluateAsync(ObjectClassInfo, new AggregateOperand(String.Empty, Aggregate.Count), val, cancellationToken).ConfigureAwait(false)) == 0;
		}
		bool Any(MethodCallExpression call) {
			if(IsGroup)
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_X0OverGroupingIsNotSupported, "Any operator"));
			CriteriaOperator val;
			if(call.Arguments.Count > 1) {
				val = GroupOperator.And(Criteria, ParseExpression(call.Arguments[1], Projection));
			}
			else
				val = Criteria;
			return GetSafeInt(SessionEvaluate(ObjectClassInfo, new AggregateOperand(String.Empty, Aggregate.Count), val)) > 0;
		}
		async Task<bool> AnyAsync(MethodCallExpression call, CancellationToken cancellationToken = default(CancellationToken)) {
			if(IsGroup)
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_X0OverGroupingIsNotSupported, "Any operator"));
			CriteriaOperator val;
			if(call.Arguments.Count > 1) {
				val = GroupOperator.And(Criteria, ParseExpression(call.Arguments[1], Projection));
			}
			else
				val = Criteria;
			return GetSafeInt(await SessionEvaluateAsync(ObjectClassInfo, new AggregateOperand(String.Empty, Aggregate.Count), val, cancellationToken).ConfigureAwait(false)) > 0;
		}
		bool TryParseValue(Expression expr, out object value) {
			var op = ParseExpression(expr);
			if(op is OperandValue) {
				value = ((OperandValue)op).Value;
				return true;
			}
			value = null;
			return false;
		}
		void ParseSingleCallArgs(MethodCallExpression call, bool allowDefault, out CriteriaOperator condition, out object defaultValue) {
			condition = null;
			defaultValue = null;
			if(call.Arguments.Count > 1) {
				if(call.Arguments.Count > 2) {
					condition = ParseExpression(call.Arguments[1], Projection);
					TryParseValue(call.Arguments[2], out defaultValue);
				}
				else {
					if(!(allowDefault && call.Type.IsAssignableFrom(call.Arguments[1].Type)
						&& TryParseValue(call.Arguments[1], out defaultValue))) {
						condition = ParseExpression(call.Arguments[1], Projection);
					}
				}
			}
		}
		enum SortAction { None, Sort, Reverse };
		object ExecuteSingle(MethodCallExpression call, bool allowDefault, SortAction sort) {
			bool single = sort == SortAction.None;
			CriteriaOperator condition;
			object defaultValue;
			ParseSingleCallArgs(call, allowDefault, out condition, out defaultValue);
			return IsNull(Projection) && !IsGroup
				? GetSingleObject(condition, allowDefault, sort, single, null, defaultValue)
				: GetSingleData(call.Type, condition, allowDefault, sort, single, null, defaultValue);
		}
		Task<object> ExecuteSingleAsync(MethodCallExpression call, bool allowDefault, SortAction sort, CancellationToken cancellationToken = default(CancellationToken)) {
			bool single = sort == SortAction.None;
			CriteriaOperator val;
			if(call.Arguments.Count > 1) {
				val = ParseExpression(call.Arguments[1], Projection);
			}
			else
				val = null;
			return IsNull(Projection) && !IsGroup
				? GetSingleObjectAsync(val, allowDefault, sort, single, null, cancellationToken)
				: GetSingleDataAsync(call.Type, val, allowDefault, sort, single, null, cancellationToken);
		}
		object ExecuteElementAt(MethodCallExpression call, bool allowDefault) {
			int? elementAt = null;
			if(call.Arguments.Count > 1) {
				var operandValue = ParseExpression(call.Arguments[1]) as OperandValue;
				if(!ReferenceEquals(operandValue, null) && operandValue.Value is int) {
					elementAt = (int)operandValue.Value;
				}
			}
			return IsNull(Projection) && !IsGroup
				? GetSingleObject(null, allowDefault, SortAction.Sort, false, elementAt)
				: GetSingleData(call.Type, null, allowDefault, SortAction.Sort, false, elementAt);
		}
		Task<object> ExecuteElementAtAsync(MethodCallExpression call, bool allowDefault, CancellationToken cancellationToken = default(CancellationToken)) {
			int? elementAt = null;
			if(call.Arguments.Count > 1) {
				var operandValue = ParseExpression(call.Arguments[1]) as OperandValue;
				if(!ReferenceEquals(operandValue, null) && operandValue.Value is int) {
					elementAt = (int)operandValue.Value;
				}
			}
			return IsNull(Projection) && !IsGroup
				? GetSingleObjectAsync(null, allowDefault, SortAction.Sort, false, elementAt, cancellationToken)
				: GetSingleDataAsync(call.Type, null, allowDefault, SortAction.Sort, false, elementAt, cancellationToken);
		}
		CriteriaOperatorCollection GetGrouping() {
			if(IsNull(GroupKey) || GroupKey.Is<OperandValue>()) {
				return null;
			}
			else {
				CriteriaOperatorCollection groupProperties = new CriteriaOperatorCollection();
				MemberInitOperator init = GroupKey as MemberInitOperator;
				if(!IsNull(init)) {
					PopulateGroupingRecursive(init, groupProperties);
				}
				else {
					groupProperties.Add(GroupKey);
				}
				return groupProperties;
			}
		}
		void PopulateGroupingRecursive(MemberInitOperator init, CriteriaOperatorCollection groupProperties) {
			foreach(XPMemberAssignment m in init.Members) {
				var innerInit = m.Property as MemberInitOperator;
				if(!IsNull(innerInit)) {
					PopulateGroupingRecursive(innerInit, groupProperties);
				}
				else {
					groupProperties.Add(m.Property);
				}
			}
		}
		object GetSingleData(Type type, CriteriaOperator val, bool allowDefault, SortAction sort, bool single, int? elementAt, object defaultValue = null) {
			val = GroupOperator.And(Criteria, val);
			if(IsNull(Projection)) {
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_CurrentExpressionWithX0IsNotSupported, "Single operator"));
			}
			if(Sorting == null && Skip.HasValue) {
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_SkipOperationIsNotSupportedWithoutSorting));
			}
			if((Sorting == null || Sorting.Count == 0) && elementAt.HasValue) {
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ElemAtOperationIsNotSupportedWithoutSorting));
			}
			CriteriaOperatorCollection props = new CriteriaOperatorCollection();
			CriteriaOperatorCollection correspondingProps = new CriteriaOperatorCollection();
			MemberInitOperator last = Projection;
			foreach(XPMemberAssignment mi in last.Members) {
				SelectionPropertyPatcher.ProcessAndAdd(this, props, correspondingProps, mi.Property);
			}
			SortingCollection sortCol;
			if(sort == SortAction.Reverse) {
				if(elementAt.HasValue) {
					throw new ArgumentException(null, nameof(elementAt));
				}
				sortCol = new SortingCollection();
				for(int i = 0; i < Sorting.Count; i++)
					sortCol.Add(new SortProperty(Sorting[i].Property, Sorting[i].Direction == SortingDirection.Ascending ? SortingDirection.Descending : SortingDirection.Ascending));
			}
			else
				sortCol = Sorting;
			if(ZeroTop) {
				if(!allowDefault)
					throw new InvalidOperationException(Res.GetString(Res.LinqToXpo_SequenceContainsNoMatchingElement));
				else
					return defaultValue;
			}
			List<object[]> data = SessionSelectData(ObjectClassInfo, props, val, GetGrouping(), GroupCriteria,
					(elementAt ?? 0) + (Skip ?? 0), single && Top != 1 ? 2 : 1, sortCol);
			if(data.Count == 0) {
				if(!allowDefault)
					throw new InvalidOperationException(Res.GetString(Res.LinqToXpo_SequenceContainsNoMatchingElement));
				else
					return defaultValue;
			}
			if(single && data.Count > 1)
				throw new InvalidOperationException(Res.GetString(Res.LinqToXpo_SequenceContainsMoreThanOneElement));
			DataPostProcessing(correspondingProps, data, type);
			return CreateItem(type, last, props)(data[0]);
		}
		async Task<object> GetSingleDataAsync(Type type, CriteriaOperator val, bool allowDefault, SortAction sort, bool single, int? elementAt, CancellationToken cancellationToken = default(CancellationToken)) {
			val = GroupOperator.And(Criteria, val);
			if(IsNull(Projection)) {
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_CurrentExpressionWithX0IsNotSupported, "Single operator"));
			}
			if(Sorting == null && Skip.HasValue) {
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_SkipOperationIsNotSupportedWithoutSorting));
			}
			if((Sorting == null || Sorting.Count == 0) && elementAt.HasValue) {
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ElemAtOperationIsNotSupportedWithoutSorting));
			}
			CriteriaOperatorCollection props = new CriteriaOperatorCollection();
			CriteriaOperatorCollection correspondingProps = new CriteriaOperatorCollection();
			MemberInitOperator last = Projection;
			foreach(XPMemberAssignment mi in last.Members) {
				SelectionPropertyPatcher.ProcessAndAdd(this, props, correspondingProps, mi.Property);
			}
			SortingCollection sortCol;
			if(sort == SortAction.Reverse) {
				if(elementAt.HasValue) {
					throw new ArgumentException(null, nameof(elementAt));
				}
				sortCol = new SortingCollection();
				for(int i = 0; i < Sorting.Count; i++)
					sortCol.Add(new SortProperty(Sorting[i].Property, Sorting[i].Direction == SortingDirection.Ascending ? SortingDirection.Descending : SortingDirection.Ascending));
			}
			else
				sortCol = Sorting;
			if(ZeroTop) {
				if(!allowDefault)
					throw new InvalidOperationException(Res.GetString(Res.LinqToXpo_SequenceContainsNoMatchingElement));
				else
					return null;
			}
			cancellationToken.ThrowIfCancellationRequested();
			List<object[]> data = await SessionSelectDataAsync(ObjectClassInfo, props, val, GetGrouping(), GroupCriteria,
					(elementAt ?? 0) + (Skip ?? 0), single && Top != 1 ? 2 : 1, sortCol, cancellationToken);
			if(data.Count == 0) {
				if(!allowDefault)
					throw new InvalidOperationException(Res.GetString(Res.LinqToXpo_SequenceContainsNoMatchingElement));
				else
					return null;
			}
			if(single && data.Count > 1)
				throw new InvalidOperationException(Res.GetString(Res.LinqToXpo_SequenceContainsMoreThanOneElement));
			cancellationToken.ThrowIfCancellationRequested();
			await DataPostProcessingAsync(correspondingProps, data, type, cancellationToken);
			return CreateItem(type, last, props)(data[0]);
		}
		struct PostloadGroupCacheItem {
			public GroupSet Group;
			public Type GroupType;
			public PostloadGroupCacheItem(GroupSet group, Type groupType) {
				Group = group;
				GroupType = groupType;
			}
		}
		class DataPostProcessingContext {
			public readonly CriteriaOperatorCollection Props;
			public readonly List<object[]> Data;
			public readonly Type Type;
			public int SourcePos;
			public readonly Dictionary<PostloadGroupCacheItem, int> GroupCache;
			public XPClassInfo ClassInfo;
			public OperandProperty Op;
			public ExpressionEvaluator PostEval;
			public DataPostProcessingContext(CriteriaOperatorCollection props, List<object[]> data, Type type) {
				SourcePos = 0;
				GroupCache = new Dictionary<PostloadGroupCacheItem, int>();
				this.Props = props;
				this.Data = data;
				this.Type = type;
			}
		}
		void DataPostProcessingBegin(DataPostProcessingContext context, int propertyIndex) {
			Stack<XPClassInfo> classInfoStack = new Stack<XPClassInfo>();
			XPClassInfo currentClassInfo = ObjectClassInfo;
			CriteriaOperator currentProperty = context.Props[propertyIndex];
			classInfoStack.Push(currentClassInfo);
			CriteriaOperator newCurrentProperty;
			XPClassInfo newCurrentClassInfo;
			GetAggregatedPropertyForPostProcess(currentProperty, currentClassInfo, classInfoStack, out newCurrentProperty, out newCurrentClassInfo);
			currentProperty = newCurrentProperty;
			currentClassInfo = newCurrentClassInfo;
			context.Op = currentProperty as OperandProperty;
			List<OperandProperty> otherOperands = new List<OperandProperty>();
			if(IsNull(context.Op)) {
				FunctionOperator funcOp = currentProperty as FunctionOperator;
				if(!IsNull(funcOp) && funcOp.OperatorType == FunctionOperatorType.Iif) {
					for(int i = 1; i < funcOp.Operands.Count - 1; i += 2) {
						var operand = funcOp.Operands[i] as OperandProperty;
						if(!IsNull(operand)) {
							otherOperands.Add(operand);
						}
					}
					OperandProperty last = funcOp.Operands.Last() as OperandProperty;
					if(!IsNull(last)) {
						context.Op = last;
					}
					else {
						if(otherOperands.Count > 0) {
							context.Op = otherOperands.Last();
							otherOperands.RemoveAt(otherOperands.Count - 1);
						}
					}
				}
			}
			context.ClassInfo = null;
			context.PostEval = null;
			if(!IsNull(context.Op)) {
				if(CriteriaOperator.Equals(context.Op, Parser.ThisCriteria)) {
					context.ClassInfo = currentClassInfo;
				}
				else {
					MemberInfoCollection col = MemberInfoCollection.ParsePath(currentClassInfo, context.Op.PropertyName);
					XPMemberInfo m = col[col.Count - 1];
					context.ClassInfo = m.ReferenceType;
					if(otherOperands.Count > 0) {
						foreach(OperandProperty op in otherOperands) {
							MemberInfoCollection col2 = MemberInfoCollection.ParsePath(currentClassInfo, op.PropertyName);
							XPMemberInfo m2 = col2[col2.Count - 1];
							context.ClassInfo = AnalyzeCriteriaCreator.GetDownClass(context.ClassInfo, m2.ReferenceType);
						}
					}
				}
			}
			else {
				QuerySet qs = currentProperty as QuerySet;
				if(!IsNull(qs) && !(qs is FreeQuerySet)) {
					context.ClassInfo = currentClassInfo;
					if(!qs.IsEmpty && !IsNull(qs.Property)) {
						var setPropertyName = qs.Property.PropertyName;
						DivePropertyName(classInfoStack, ref setPropertyName, ref context.ClassInfo);
						var divedProperty = new OperandProperty(setPropertyName);
						if(InTransaction) {
							context.PostEval = new XPQueryPostEvaluator(context.ClassInfo.GetEvaluatorContextDescriptorInTransaction(), divedProperty, false, Dictionary.CustomFunctionOperators, Dictionary.CustomAggregates);
						}
						else {
							context.PostEval = new XPQueryPostEvaluator(context.ClassInfo.GetEvaluatorContextDescriptor(), divedProperty, false, Dictionary.CustomFunctionOperators, Dictionary.CustomAggregates);
						}
						context.Op = divedProperty;
					}
				}
				else {
					IAggregateOperand aggregateOperand = currentProperty as IAggregateOperand;
					if(aggregateOperand != null && aggregateOperand.AggregateType == Aggregate.Custom) {
						context.ClassInfo = currentClassInfo;
					}
				}
			}
		}
		void GetAggregatedPropertyForPostProcess(CriteriaOperator currentProperty, XPClassInfo currentClassInfo, Stack<XPClassInfo> classInfoStack, out CriteriaOperator outAggregatedProperty, out XPClassInfo outAggregatedPropertyClassInfo) {
			outAggregatedProperty = currentProperty;
			outAggregatedPropertyClassInfo = currentClassInfo;
			IAggregateOperand aggregateOperand = currentProperty as IAggregateOperand;
			if(IsNull(aggregateOperand) || (aggregateOperand.AggregateType != Aggregate.Single && aggregateOperand.AggregateType != Aggregate.Max && aggregateOperand.AggregateType != Aggregate.Min && aggregateOperand.AggregateType != Aggregate.Custom)) {
				return;
			}
			XPClassInfo nextClassInfo = null;
			if(aggregateOperand is JoinOperand) {
				var joinTypeString = (string)aggregateOperand.AggregationObject;
				if(!MemberInfoCollection.TryResolveTypeAlsoByShortName(joinTypeString, currentClassInfo, out nextClassInfo)) {
					throw new DevExpress.Xpo.Exceptions.CannotResolveClassInfoException(string.Empty, joinTypeString);
				}
			}
			else {
				OperandProperty collectionProperty = aggregateOperand.AggregationObject as OperandProperty;
				if(IsNull(collectionProperty)) {
					nextClassInfo = currentClassInfo;
				}
				else {
					MemberInfoCollection col = MemberInfoCollection.ParsePath(currentClassInfo, collectionProperty.PropertyName);
					XPMemberInfo m = col[col.Count - 1];
					nextClassInfo = m.CollectionElementType;
				}
			}
			if(aggregateOperand.AggregateType != Aggregate.Custom) {
				currentClassInfo = nextClassInfo;
				classInfoStack.Push(currentClassInfo);
				currentProperty = aggregateOperand.AggregatedExpression;
				GetAggregatedPropertyForPostProcess(currentProperty, currentClassInfo, classInfoStack, out outAggregatedProperty, out outAggregatedPropertyClassInfo);
			}
			else {
				var typeResolver = new Resolver(classInfoStack.ToArray(), Dictionary);
				Type customAggregateResultType = typeResolver.Resolve(currentProperty);
				if(customAggregateResultType.IsValueType || !Dictionary.CanGetClassInfoByType(customAggregateResultType)) {
					outAggregatedProperty = currentProperty;
					outAggregatedPropertyClassInfo = null;
					return;
				}
				outAggregatedProperty = currentProperty;
				outAggregatedPropertyClassInfo = Dictionary.GetClassInfo(customAggregateResultType);
				return;
			}
		}
		void DataPostProcessingEnd(DataPostProcessingContext context, int propertyIndex) {
			if(context.SourcePos != propertyIndex) {
				for(int j = 0; j < context.Data.Count; j++) {
					context.Data[j][propertyIndex] = context.Data[j][context.SourcePos];
				}
			}
		}
		private Type GroupDataPostProcessingBegin(GroupSet group) {
			var groupResolver = new Resolver(ObjectClassInfo);
			Type elementType = IsNull(group.Projection) ? ObjectClassInfo.ClassType : groupResolver.Resolve(group.Projection);
			Type keyType = groupResolver.Resolve(GroupKey);
			return typeof(IGrouping<,>).MakeGenericType(keyType, elementType);
		}
		private void GroupDataPostProcessingEnd(DataPostProcessingContext context, int i, Type groupType) {
			GroupSet group = (GroupSet)context.Props[i];
			PostloadGroupCacheItem gcItem = new PostloadGroupCacheItem(group, groupType);
			CriteriaOperatorCollection groupProperties = GetGrouping();
			int cachedSourcePos;
			if(!context.GroupCache.TryGetValue(gcItem, out cachedSourcePos)) {
				CreateItemDelegate createGroup = CreateGroupItemCore(groupType, group.Projection);
				for(int j = 0; j < context.Data.Count; j++) {
					object[] groupKey = new object[groupProperties.Count];
					Array.Copy(context.Data[j], context.SourcePos, groupKey, 0, groupKey.Length);
					context.Data[j][i] = createGroup(groupKey);
				}
				context.GroupCache.Add(gcItem, context.SourcePos);
			}
			else {
				for(int j = 0; j < context.Data.Count; j++) {
					context.Data[j][i] = context.Data[j][cachedSourcePos];
				}
			}
			context.SourcePos += groupProperties.Count - 1;
		}
		async Task DataPostProcessingAsync(CriteriaOperatorCollection props, List<object[]> data, Type type, CancellationToken cancellationToken = default(CancellationToken)) {
			var context = new DataPostProcessingContext(props, data, type);
			for(int i = 0; i < context.Props.Count; i++, context.SourcePos++) {
				GroupSet group = context.Props[i] as GroupSet;
				if(!IsNull(group)) {
					Type groupType = GroupDataPostProcessingBegin(group);
					await DataPostProcessingAsync(GetGrouping(), context.Data, groupType, cancellationToken);
					GroupDataPostProcessingEnd(context, i, groupType);
				}
				else {
					DataPostProcessingBegin(context, i);
					if(context.ClassInfo != null) {
						var currentSession = GetSession();
						object[] ids = new object[data.Count];
						object[] fetchedIds = null;
						bool allIdsAreFetched = true;
						for(int j = 0; j < data.Count; j++) {
							var id = data[j][context.SourcePos];
							if(id != null && Type.GetTypeCode(id.GetType()) == TypeCode.Object) {
								var idClassInfo = Dictionary.QueryClassInfo(id);
								if(idClassInfo != null && idClassInfo.IsAssignableTo(context.ClassInfo)) {
									if(fetchedIds == null) {
										fetchedIds = new object[data.Count];
									}
									fetchedIds[j] = id;
									ids[j] = currentSession.GetKeyValue(id);
									continue;
								}
							}
							allIdsAreFetched = false;
							ids[j] = id;
						}
						cancellationToken.ThrowIfCancellationRequested();
						ICollection result = allIdsAreFetched && fetchedIds != null
							? fetchedIds
							: await currentSession.GetObjectsByKeyAsync(context.ClassInfo, ids, false, cancellationToken);
						if(context.PostEval != null) {
							if(!IsNull(context.Op)) {
								cancellationToken.ThrowIfCancellationRequested();
								await currentSession.PreFetchAsync(context.ClassInfo, result, cancellationToken, context.Op.PropertyName);
							}
							cancellationToken.ThrowIfCancellationRequested();
							result = context.PostEval.EvaluateOnObjects(result);
						}
						int jC = 0;
						foreach(object resObject in result) {
							data[jC++][context.SourcePos] = resObject;
						}
					}
					DataPostProcessingEnd(context, i);
				}
			}
		}
		void DataPostProcessing(CriteriaOperatorCollection props, List<object[]> data, Type type) {
			var context = new DataPostProcessingContext(props, data, type);
			for(int i = 0; i < props.Count; i++, context.SourcePos++) {
				GroupSet group = context.Props[i] as GroupSet;
				if(!IsNull(group)) {
					Type groupType = GroupDataPostProcessingBegin(group);
					DataPostProcessing(GetGrouping(), context.Data, groupType.GenericTypeArguments[0]);
					GroupDataPostProcessingEnd(context, i, groupType);
				}
				else {
					DataPostProcessingBegin(context, i);
					if(context.ClassInfo != null) {
						var currentSession = GetSession();
						object[] ids = new object[data.Count];
						object[] fetchedIds = null;
						bool allIdsAreFetched = true;
						for(int j = 0; j < data.Count; j++) {
							var id = data[j][context.SourcePos];
							if(id != null && Type.GetTypeCode(id.GetType()) == TypeCode.Object) {
								var idClassInfo = Dictionary.QueryClassInfo(id);
								if(idClassInfo != null && idClassInfo.IsAssignableTo(context.ClassInfo)) {
									if(fetchedIds == null) {
										fetchedIds = new object[data.Count];
									}
									fetchedIds[j] = id;
									ids[j] = currentSession.GetKeyValue(id);
									continue;
								}
							}
							allIdsAreFetched = false;
							ids[j] = id;
						}
						ICollection result = allIdsAreFetched && fetchedIds != null ? fetchedIds : currentSession.GetObjectsByKey(context.ClassInfo, ids, false);
						if(context.PostEval != null) {
							if(!IsNull(context.Op)) {
								currentSession.PreFetch(context.ClassInfo, result, context.Op.PropertyName);
							}
							result = context.PostEval.EvaluateOnObjects(result);
						}
						int jC = 0;
						foreach(object resObject in result) {
							data[jC++][context.SourcePos] = resObject;
						}
					}
					DataPostProcessingEnd(context, i);
				}
			}
		}
		protected void DivePropertyName(Stack<XPClassInfo> classInfoStack, ref string propertyName, ref XPClassInfo classInfo, bool throwOnEmptyStack = true) {
			while(!string.IsNullOrEmpty(propertyName) && (propertyName.StartsWith("^.") || propertyName == "^")) {
				if(classInfoStack.Count < 2) {
					if(throwOnEmptyStack)
						throw new InvalidOperationException();
					break;
				}
				classInfoStack.Pop();
				propertyName = propertyName == "^" ? "This" : propertyName.Substring(2);
			}
			classInfo = classInfoStack.Peek();
		}
		object GetSingleObject(CriteriaOperator val, bool allowDefault, SortAction sort, bool single, int? elementAt, object defaultValue = null) {
			if(Sorting == null && Skip.HasValue) {
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_SkipOperationIsNotSupportedWithoutSorting));
			}
			if((Sorting == null || Sorting.Count == 0) && elementAt.HasValue) {
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ElemAtOperationIsNotSupportedWithoutSorting));
			}
			val = GroupOperator.And(Criteria, val);
			object res;
			if(ZeroTop) {
				if(!allowDefault)
					throw new InvalidOperationException(Res.GetString(Res.LinqToXpo_SequenceContainsNoMatchingElement));
				return defaultValue;
			}
			if(Sorting == null || (single && (!Top.HasValue || Top != 1))) {
				if(!single)
					res = SessionFindObject(ObjectClassInfo, val);
				else {
					ICollection col = SessionGetObjects(ObjectClassInfo, val, Sorting, (elementAt ?? 0) + (Skip ?? 0), Top.HasValue && Top == 1 ? 1 : 2);
					IEnumerator enumerator = col.GetEnumerator();
					if(enumerator.MoveNext()) {
						if(col.Count > 1)
							throw new InvalidOperationException(Res.GetString(Res.LinqToXpo_SequenceContainsMoreThanOneElement));
						res = enumerator.Current;
					}
					else
						res = null;
				}
			}
			else {
				SortingCollection sortCol;
				bool returnNull = false;
				if(sort == SortAction.Reverse) {
					if(elementAt.HasValue) {
						throw new ArgumentException(null, nameof(elementAt));
					}
					if(Skip.HasValue) {
						returnNull = (Convert.ToInt32(SessionEvaluate(ObjectClassInfo, AggregateOperand.TopLevel(Aggregate.Count), val)) - Skip.Value) < 1;
					}
					sortCol = new SortingCollection();
					for(int i = 0; i < Sorting.Count; i++)
						sortCol.Add(new SortProperty(Sorting[i].Property, Sorting[i].Direction == SortingDirection.Ascending ? SortingDirection.Descending : SortingDirection.Ascending));
				}
				else
					sortCol = Sorting;
				if(returnNull)
					res = null;
				else {
					ICollection col = SessionGetObjects(ObjectClassInfo, val, sortCol, sort == SortAction.Reverse ? 0 : (elementAt ?? 0) + (Skip ?? 0), 1);
					IEnumerator enumerator = col.GetEnumerator();
					if(enumerator.MoveNext())
						res = enumerator.Current;
					else
						res = null;
				}
			}
			if(res == null)
				if(!allowDefault) {
					throw new InvalidOperationException(Res.GetString(Res.LinqToXpo_SequenceContainsNoMatchingElement));
				}
				else {
					res = defaultValue;
				}
			return res;
		}
		async Task<object> GetSingleObjectAsync(CriteriaOperator val, bool allowDefault, SortAction sort, bool single, int? elementAt, CancellationToken cancellationToken = default(CancellationToken)) {
			if(Sorting == null && Skip.HasValue) {
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_SkipOperationIsNotSupportedWithoutSorting));
			}
			if((Sorting == null || Sorting.Count == 0) && elementAt.HasValue) {
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ElemAtOperationIsNotSupportedWithoutSorting));
			}
			val = GroupOperator.And(Criteria, val);
			object res;
			if(ZeroTop) {
				if(!allowDefault)
					throw new InvalidOperationException(Res.GetString(Res.LinqToXpo_SequenceContainsNoMatchingElement));
				return null;
			}
			if(Sorting == null || (single && (!Top.HasValue || Top != 1))) {
				if(!single) {
					res = await SessionFindObjectAsync(ObjectClassInfo, val, cancellationToken).ConfigureAwait(false);
				}
				else {
					ICollection col = await SessionGetObjectsAsync(ObjectClassInfo, val, Sorting, (elementAt ?? 0) + (Skip ?? 0), Top.HasValue && Top == 1 ? 1 : 2, cancellationToken).ConfigureAwait(false);
					IEnumerator enumerator = col.GetEnumerator();
					if(enumerator.MoveNext()) {
						if(col.Count > 1)
							throw new InvalidOperationException(Res.GetString(Res.LinqToXpo_SequenceContainsMoreThanOneElement));
						res = enumerator.Current;
					}
					else
						res = null;
				}
			}
			else {
				SortingCollection sortCol;
				bool returnNull = false;
				if(sort == SortAction.Reverse) {
					if(elementAt.HasValue) {
						throw new ArgumentException(null, nameof(elementAt));
					}
					if(Skip.HasValue) {
						returnNull = (Convert.ToInt32(await SessionEvaluateAsync(ObjectClassInfo, AggregateOperand.TopLevel(Aggregate.Count), val, cancellationToken)) - Skip.Value) < 1;
					}
					sortCol = new SortingCollection();
					for(int i = 0; i < Sorting.Count; i++)
						sortCol.Add(new SortProperty(Sorting[i].Property, Sorting[i].Direction == SortingDirection.Ascending ? SortingDirection.Descending : SortingDirection.Ascending));
				}
				else {
					sortCol = Sorting;
				}
				if(returnNull) {
					res = null;
				}
				else {
					ICollection col = await SessionGetObjectsAsync(ObjectClassInfo, val, sortCol, sort == SortAction.Reverse ? 0 : (elementAt ?? 0) + (Skip ?? 0), 1, cancellationToken).ConfigureAwait(false);
					IEnumerator enumerator = col.GetEnumerator();
					if(enumerator.MoveNext())
						res = enumerator.Current;
					else
						res = null;
				}
			}
			if(!allowDefault && res == null)
				throw new InvalidOperationException(Res.GetString(Res.LinqToXpo_SequenceContainsNoMatchingElement));
			return res;
		}
		delegate object CreateItemDelegate(object[] row);
		CreateItemDelegate create;
		CreateItemDelegate CreateItem(Type type, MemberInitOperator last, CriteriaOperatorCollection effectiveProps) {
			if(create == null)
				create = CreateItemCore(type, last, effectiveProps);
			return create;
		}
		static Expression ConvertToType(Expression exp, Type targetType, Type sourceType) {
			var expressionType = exp.Type;
			if(sourceType == null) {
				return expressionType == targetType ? exp : Expression.Convert(exp, targetType);
			}
			Expression result;
			if(!expressionType.IsValueType && sourceType.IsValueType && Nullable.GetUnderlyingType(targetType) == null) {
				sourceType = NullableHelpers.GetBoxedType(sourceType);
				result = Expression.Condition(Expression.Equal(exp, Expression.Constant(null)), Expression.Default(sourceType), Expression.Convert(exp, sourceType));
			}
			else if(!expressionType.IsValueType && sourceType.IsValueType && exp.Type == typeof(object)) {
				if(Nullable.GetUnderlyingType(targetType) != null) {
					if(targetType != sourceType) {
						return Expression.Condition(Expression.Equal(exp, Expression.Constant(null)), Expression.Constant(null, targetType), Expression.Convert(Expression.Convert(exp, sourceType), targetType));
					}
					return Expression.Condition(Expression.Equal(exp, Expression.Constant(null)), Expression.Constant(null, sourceType), Expression.Convert(exp, sourceType));
				}
				result = Expression.Condition(Expression.Equal(exp, Expression.Constant(null)), Expression.Default(sourceType), Expression.Convert(exp, sourceType));
			}
			else if(expressionType != sourceType) {
				result = Expression.Convert(exp, sourceType);
			}
			else {
				result = exp;
			}
			if(targetType != sourceType) {
				var convertionResult = ElementwiseConversion(result, targetType);
				if(convertionResult == null) {
					if(sourceType == typeof(long) && targetType == typeof(TimeSpan)) {
						result = Expression.Call(typeof(TimeSpan), "FromTicks", null, result);
					}
					else if(sourceType == typeof(double) && targetType == typeof(TimeSpan)) {
						result = Expression.Call(typeof(TimeSpan), "FromSeconds", null, result);
					}
					else {
						result = Expression.Convert(result, targetType);
					}
				}
				else {
					result = convertionResult;
				}
			}
			return result;
		}
		static Type GetSourceType(CriteriaTypeResolver resolver, CriteriaOperator prop) {
			if(prop is GroupSet)
				return null;
			return resolver.Resolve(prop);
		}
		class CreateItemCommonKeyComparer : IEqualityComparer<CreateItemCommonKey> {
			public bool Equals(CreateItemCommonKey x, CreateItemCommonKey y) {
				if(x.Type != y.Type) return false;
				if(x.ProjectionSample == y.ProjectionSample) return true;
				if(x.ProjectionSample == null || y.ProjectionSample == null || x.ProjectionSample.Count != y.ProjectionSample.Count) return false;
				int count = x.ProjectionSample.Count;
				for(int i = 0; i < count; i++) {
					if(!object.Equals(x.ProjectionSample[i], y.ProjectionSample[i])) return false;
				}
				return true;
			}
			public int GetHashCode(CreateItemCommonKey obj) {
				int hashCode = HashCodeHelper.StartGeneric(obj.Type);
				if(obj.ProjectionSample != null) {
					hashCode = HashCodeHelper.CombineGenericList(hashCode, obj.ProjectionSample);
				}
				return HashCodeHelper.Finish(hashCode);
			}
		}
		struct CreateItemCommonKey {
			public Type Type;
			public List<object> ProjectionSample;
		}
		readonly static Dictionary<CreateItemCommonKey, CreateItemDelegate> createItemCommonCache = new Dictionary<CreateItemCommonKey, CreateItemDelegate>(new CreateItemCommonKeyComparer());
		readonly static Dictionary<Type, Delegate> elementwiseConversionCache = new Dictionary<Type, Delegate>();
		class Resolver : CriteriaTypeResolver, IClientCriteriaVisitor<CriteriaTypeResolverResult>, ILinqExtendedCriteriaVisitor<CriteriaTypeResolverResult> {
			readonly XPClassInfo classInfo;
			public Resolver(XPClassInfo info)
				: base(info, CriteriaTypeResolveKeyBehavior.AsIs) {
				classInfo = info;
			}
			public Resolver(XPClassInfo[] upLevels, XPDictionary dictionary)
				: base(upLevels, dictionary, CriteriaTypeResolveKeyBehavior.AsIs) {
				classInfo = upLevels.FirstOrDefault();
			}
			CriteriaTypeResolverResult ILinqExtendedCriteriaVisitor<CriteriaTypeResolverResult>.Visit(MemberInitOperator theOperand) {
				if(!theOperand.CreateNewObject && theOperand.Members.Count == 1) {
					CriteriaOperator property = theOperand.Members[0].Property;
					if(IsNull(property)) {
						return new DevExpress.Data.Filtering.Helpers.CriteriaTypeResolverResult(typeof(object));
					}
					return property.Accept(this);
				}
				return new DevExpress.Data.Filtering.Helpers.CriteriaTypeResolverResult(theOperand.GetDeclaringType());
			}
			CriteriaTypeResolverResult ILinqExtendedCriteriaVisitor<CriteriaTypeResolverResult>.Visit(ExpressionAccessOperator theOperand) {
				return new DevExpress.Data.Filtering.Helpers.CriteriaTypeResolverResult(theOperand.LinqExpression.Type);
			}
			CriteriaTypeResolverResult ILinqExtendedCriteriaVisitor<CriteriaTypeResolverResult>.Visit(QuerySet theOperand) {
				if(theOperand.IsEmpty) {
					return Parser.ThisCriteria.Accept(this);
				}
				if(IsNull(theOperand.Property)) {
					if(!IsNull(theOperand.Projection)) {
						return theOperand.Projection.Accept(this);
					}
				}
				else {
					if(!IsNull(theOperand.Projection)) {
						MemberInfoCollection path = MemberInfoCollection.ParsePath(classInfo, theOperand.Property.PropertyName);
						XPMemberInfo member = path[path.Count - 1];
						if(member.IsAssociationList) {
							return new Resolver(member.CollectionElementType).Process(theOperand.Projection);
						}
					}
					else {
						return theOperand.Property.Accept(this);
					}
				}
				throw new NotSupportedException();
			}
		}
		Expression GetArrayIndexExpression(ParameterExpression row, int rowIndex, Type sourceType) {
			var arrayIndexExpression = Expression.ArrayIndex(row, Expression.Constant(rowIndex));
			var valueConverter = Dictionary.GetConverter(sourceType);
			if(valueConverter != null) {
				return Expression.Condition(Expression.TypeIs(arrayIndexExpression, valueConverter.StorageType),
					Expression.Call(Expression.Constant(valueConverter), "ConvertFromStorageType", null, arrayIndexExpression),
					arrayIndexExpression);
			}
			return arrayIndexExpression;
		}
		Expression CreateSubItemCore(Type subType, MemberInitOperator last, CriteriaTypeResolver resolver, ParameterExpression row, ref int rowIndex) {
			if(subType == null) {
				subType = last.GetDeclaringType();
			}
			if(last.CreateNewObject) {
				var declaringType = last.GetDeclaringType();
				if(subType != declaringType && subType.IsAssignableFrom(declaringType)) {
					subType = declaringType;
				}
				if(last.UseConstructor) {
					ConstructorInfo con = last.GetConstructor(subType);
					ParameterInfo[] parameters = con.GetParameters();
					Type[] sourceTypes = last.GetSourceTypes(subType, resolver);
					List<Expression> init = new List<Expression>(parameters.Length);
					int paramArrayIndex = -1;
					for(int i = 0; i < parameters.Length; i++) {
						if(parameters[i].ParameterType.IsArray && parameters[i].GetCustomAttribute<ParamArrayAttribute>() != null
							&& !(sourceTypes[i].IsArray && parameters[i].ParameterType.GetElementType().IsAssignableFrom(sourceTypes[i].GetElementType()))) {
							paramArrayIndex = i;
							break;
						}
						CriteriaOperator property = last.Members[i].Property;
						if(property is MemberInitOperator) {
							init.Add(CreateSubItemCore(sourceTypes[i], (MemberInitOperator)property, resolver, row, ref rowIndex));
							continue;
						}
						if(property is ExpressionAccessOperator) {
							init.Add(CreateSubItemCore(sourceTypes[i], (ExpressionAccessOperator)property, resolver, row, ref rowIndex));
							continue;
						}
						if(property is ConstantCompiler) {
							var compilerExpression = ((ConstantCompiler)property).Expression;
							init.Add(ConvertToType(compilerExpression, compilerExpression.Type, sourceTypes[i]));
							continue;
						}
						init.Add(ConvertToType(GetArrayIndexExpression(row, rowIndex++, sourceTypes[i]), parameters[i].ParameterType, sourceTypes[i]));
					}
					if(paramArrayIndex != -1) {
						List<Expression> paramArrayExprs = new List<Expression>(last.Members.Count - paramArrayIndex);
						Type paramArrayType = parameters[paramArrayIndex].ParameterType.GetElementType();
						for(int i = paramArrayIndex; i < last.Members.Count; i++) {
							CriteriaOperator property = last.Members[i].Property;
							if(property is MemberInitOperator) {
								paramArrayExprs.Add(CreateSubItemCore(sourceTypes[i], (MemberInitOperator)property, resolver, row, ref rowIndex));
								continue;
							}
							if(property is ExpressionAccessOperator) {
								paramArrayExprs.Add(CreateSubItemCore(sourceTypes[i], (ExpressionAccessOperator)property, resolver, row, ref rowIndex));
								continue;
							}
							if(property is ConstantCompiler) {
								var compilerExpression = ((ConstantCompiler)property).Expression;
								paramArrayExprs.Add(ConvertToType(compilerExpression, compilerExpression.Type, sourceTypes[i]));
								continue;
							}
							paramArrayExprs.Add(ConvertToType(GetArrayIndexExpression(row, rowIndex++, sourceTypes[i]), paramArrayType, sourceTypes[i]));
						}
						init.Add(Expression.NewArrayInit(paramArrayType, paramArrayExprs));
					}
					return Expression.New(con, init);
				}
				else {
					MemberInfo[] members = new MemberInfo[last.Members.Count];
					Type[] sourceTypes = last.GetSourceTypes(subType, resolver);
					for(int i = 0; i < members.Length; i++) {
						members[i] = last.Members[i].GetMember(subType);
					}
					MemberBinding[] init = new MemberBinding[members.Length];
					for(int i = 0; i < members.Length; i++) {
						CriteriaOperator property = last.Members[i].Property;
						if(property is MemberInitOperator) {
							init[i] = Expression.Bind(members[i],
								CreateSubItemCore(sourceTypes[i], (MemberInitOperator)property, resolver, row, ref rowIndex));
							continue;
						}
						if(property is ExpressionAccessOperator) {
							init[i] = Expression.Bind(members[i],
								CreateSubItemCore(sourceTypes[i], (ExpressionAccessOperator)property, resolver, row, ref rowIndex));
							continue;
						}
						if(property is ConstantCompiler) {
							var compilerExpression = ((ConstantCompiler)property).Expression;
							init[i] = Expression.Bind(members[i], ConvertToType(compilerExpression, compilerExpression.Type, sourceTypes[i]));
							continue;
						}
						init[i] = Expression.Bind(members[i],
							ConvertToType(GetArrayIndexExpression(row, rowIndex++, sourceTypes[i]), MemberInitOperator.GetMemberType(members[i]), sourceTypes[i]));
					}
					return Expression.MemberInit(Expression.New(subType), init);
				}
			}
			else {
				bool valueMode = (subType == typeof(byte[]) || !subType.IsArray) && last.Members.Count == 1;
				Type elementType = valueMode ? subType : subType.GetElementType();
				Type[] sourceTypes = last.GetSourceTypes(subType, resolver);
				Expression[] init = new Expression[last.Members.Count];
				for(int i = 0; i < last.Members.Count; i++) {
					CriteriaOperator property = last.Members[i].Property;
					if(property is MemberInitOperator) {
						init[i] = CreateSubItemCore(sourceTypes[i], (MemberInitOperator)last.Members[i].Property, resolver, row, ref rowIndex);
						continue;
					}
					if(property is ExpressionAccessOperator) {
						init[i] = CreateSubItemCore(sourceTypes[i], (ExpressionAccessOperator)property, resolver, row, ref rowIndex);
						continue;
					}
					if(property is ConstantCompiler) {
						var compilerExpression = ((ConstantCompiler)property).Expression;
						init[i] = ConvertToType(compilerExpression, compilerExpression.Type, sourceTypes[i]);
						continue;
					}
					init[i] = ConvertToType(GetArrayIndexExpression(row, rowIndex++, sourceTypes[i]), elementType, sourceTypes[i]);
				}
				return valueMode ? init[0] : Expression.NewArrayInit(elementType, init);
			}
		}
		static Expression[] GetArgumentExpressions(Expression[] init, MethodInfo method, bool shiftArguments, bool isLifted) {
			ParameterInfo[] parameters = method == null ? null : method.GetParameters();
			Expression[] argumentExpressions = new Expression[shiftArguments ? init.Length - 1 : init.Length];
			for(int i = 0; i < argumentExpressions.Length; i++) {
				var currentArgument = init[shiftArguments ? i + 1 : i];
				if(parameters == null) {
					argumentExpressions[i] = currentArgument;
					continue;
				}
				var parameterType = parameters[i].ParameterType;
				parameterType = isLifted && !NullableHelpers.CanAcceptNull(parameterType) ? NullableHelpers.MakeNullableType(parameterType) : parameterType;
				argumentExpressions[i] = parameterType != currentArgument.Type ? Expression.Convert(currentArgument, parameterType) : currentArgument;
			}
			return argumentExpressions;
		}
		static Expression[] GetArgumentExpressions(Expression[] arguments, Type[] parameterTypes, Type[] sourceTypes) {
			Expression[] result = new Expression[arguments.Length];
			for(int i = 0; i < result.Length; i++) {
				result[i] = ConvertToType(arguments[i], parameterTypes[i], sourceTypes[i]);
			}
			return result;
		}
		Expression CreateBinaryItemWithMethod(BinaryExpression bin, Expression[] init, Type[] sourceTypes) {
			Expression[] args;
			if(bin.Method == null) {
				args = GetArgumentExpressions(init, new Type[] { bin.Left.Type, bin.Right.Type }, sourceTypes);
			}
			else {
				args = GetArgumentExpressions(init, bin.Method, false, bin.IsLifted);
			}
			switch(bin.NodeType) {
				case ExpressionType.Add:
					return Expression.Add(args[0], args[1], bin.Method);
				case ExpressionType.AddChecked:
					return Expression.AddChecked(args[0], args[1], bin.Method);
				case ExpressionType.And:
					return Expression.And(args[0], args[1], bin.Method);
				case ExpressionType.AndAlso:
					return Expression.AndAlso(args[0], args[1], bin.Method);
				case ExpressionType.GreaterThan:
					return Expression.GreaterThan(args[0], args[1], bin.IsLiftedToNull, bin.Method);
				case ExpressionType.GreaterThanOrEqual:
					return Expression.GreaterThanOrEqual(args[0], args[1], bin.IsLiftedToNull, bin.Method);
				case ExpressionType.LessThan:
					return Expression.LessThan(args[0], args[1], bin.IsLiftedToNull, bin.Method);
				case ExpressionType.LessThanOrEqual:
					return Expression.LessThanOrEqual(args[0], args[1], bin.IsLiftedToNull, bin.Method);
				case ExpressionType.Modulo:
					return Expression.Modulo(args[0], args[1], bin.Method);
				case ExpressionType.Multiply:
					return Expression.Multiply(args[0], args[1], bin.Method);
				case ExpressionType.MultiplyChecked:
					return Expression.MultiplyChecked(args[0], args[1], bin.Method);
				case ExpressionType.Equal:
					return Expression.Equal(args[0], args[1], bin.IsLiftedToNull, bin.Method);
				case ExpressionType.NotEqual:
					return Expression.NotEqual(args[0], args[1], bin.IsLiftedToNull, bin.Method);
				case ExpressionType.Or:
					return Expression.Or(args[0], args[1], bin.Method);
				case ExpressionType.OrElse:
					return Expression.OrElse(args[0], args[1], bin.Method);
				case ExpressionType.Subtract:
					return Expression.Subtract(args[0], args[1], bin.Method);
				case ExpressionType.SubtractChecked:
					return Expression.SubtractChecked(args[0], args[1], bin.Method);
			}
			throw new NotSupportedException(Res.GetString(Res.LinqToXpo_CurrentExpressionWithX0IsNotSupported, bin));
		}
		int isInsideNestedXPQueryCounter;
		Expression TryMakeNullSafeIif(Expression expression, Expression nullableInstance) {
			if(nullableInstance == null || isInsideNestedXPQueryCounter > 0) {
				return expression;
			}
			return !NullableHelpers.CanAcceptNull(nullableInstance.Type)
						? expression
						: Expression.Condition(
							Expression.Equal(nullableInstance, Expression.Constant(null, nullableInstance.Type)),
							Expression.Default(expression.Type),
							expression);
		}
		Expression CreateSubItemCore(Type subType, ExpressionAccessOperator expressionOperator, CriteriaTypeResolver resolver, ParameterExpression row, ref int rowIndex) {
			if(expressionOperator.SourceItems == null || expressionOperator.SourceItems.Length == 0)
				return expressionOperator.LinqExpression;
			Expression[] init = new Expression[expressionOperator.SourceItems.Length];
			Type[] sourceTypes = expressionOperator.GetSourceTypes(subType, resolver);
			int oldIsInsideNestedXPQueryCounter = isInsideNestedXPQueryCounter;
			try {
				for(int i = 0; i < init.Length; i++) {
					CriteriaOperator source = expressionOperator.SourceItems[i];
					if(IsNull(source)) {
						init[i] = null;
						continue;
					}
					Type sourceType = sourceTypes[i];
					if(sourceType != null && expressionOperator.LinqExpression.NodeType == ExpressionType.Call
											&& sourceType.IsGenericType && typeof(XPQueryBase).IsAssignableFrom(sourceType)) {
						isInsideNestedXPQueryCounter++;
					}
					if(source is MemberInitOperator) {
						init[i] = CreateSubItemCore(sourceType, (MemberInitOperator)source, resolver, row, ref rowIndex);
					}
					else if(source is ExpressionAccessOperator) {
						init[i] = CreateSubItemCore(sourceType, (ExpressionAccessOperator)source, resolver, row, ref rowIndex);
					}
					else if(source is ConstantCompiler) {
						init[i] = ((ConstantCompiler)source).Expression;
					}
					else {
						if(sourceType == typeof(long) && GetTimeOfDayValueFinder.TryToFind(source)) {
							init[i] = CreateTimeStampFromTicksExpression(Expression.ArrayIndex(row, Expression.Constant(rowIndex++)));
						}
						else {
							init[i] = Expression.ArrayIndex(row, Expression.Constant(rowIndex++));
						}
					}
				}
			}
			finally {
				isInsideNestedXPQueryCounter = oldIsInsideNestedXPQueryCounter;
			}
			switch(expressionOperator.LinqExpression.NodeType) {
				case ExpressionType.Invoke:
					return Expression.Invoke(init[0], GetArgumentExpressions(init, null, true, false));
				case ExpressionType.MemberAccess: {
					MemberInfo mi = ((MemberExpression)expressionOperator.LinqExpression).Member;
					Expression objectExpression = init[0];
					if(objectExpression.Type != mi.DeclaringType) {
						objectExpression = Expression.Convert(objectExpression, mi.DeclaringType);
					}
					var memberAccessResult = Expression.MakeMemberAccess(objectExpression, ((MemberExpression)expressionOperator.LinqExpression).Member);
					return TryMakeNullSafeIif(memberAccessResult, init[0]);
				}
				case ExpressionType.Call: {
					MethodCallExpression mce = (MethodCallExpression)expressionOperator.LinqExpression;
					Expression objectExpression = expressionOperator.InsertFirstNull ? null : init[0];
					if(objectExpression != null && mce.Method.DeclaringType != objectExpression.Type) {
						objectExpression = Expression.Convert(objectExpression, mce.Method.DeclaringType);
					}
					var callResult = Expression.Call(objectExpression, mce.Method, GetArgumentExpressions(init, mce.Method, !expressionOperator.InsertFirstNull, false));
					return TryMakeNullSafeIif(callResult, expressionOperator.InsertFirstNull ? null : init[0]);
				}
				case ExpressionType.Constant:
					return expressionOperator.LinqExpression;
				case ExpressionType.Add:
				case ExpressionType.AddChecked:
				case ExpressionType.And:
				case ExpressionType.AndAlso:
				case ExpressionType.Coalesce:
				case ExpressionType.Divide:
				case ExpressionType.Equal:
				case ExpressionType.GreaterThan:
				case ExpressionType.GreaterThanOrEqual:
				case ExpressionType.LessThan:
				case ExpressionType.LessThanOrEqual:
				case ExpressionType.Modulo:
				case ExpressionType.Multiply:
				case ExpressionType.MultiplyChecked:
				case ExpressionType.NotEqual:
				case ExpressionType.Or:
				case ExpressionType.OrElse:
				case ExpressionType.Subtract:
				case ExpressionType.SubtractChecked:
					return CreateBinaryItemWithMethod((BinaryExpression)expressionOperator.LinqExpression, init, sourceTypes);
				case ExpressionType.ArrayIndex:
					return Expression.ArrayIndex(init[0], init[1].Type == typeof(int) ? init[1] : Expression.Convert(init[1], typeof(int)));
				case ExpressionType.ArrayLength:
					return Expression.ArrayLength(init[0]);
				case ExpressionType.Convert: {
					UnaryExpression original = ((UnaryExpression)expressionOperator.LinqExpression);
					Expression currentOperand = init[0];
					if(currentOperand.Type == typeof(object)) {
						if(original.Type == typeof(object)) {
							return currentOperand;
						}
						if(original.Operand.Type != typeof(object)) {
							currentOperand = Expression.Convert(currentOperand, original.Operand.Type);
						}
					}
					return ElementwiseConversion(currentOperand, expressionOperator.LinqExpression.Type) ?? Expression.Convert(currentOperand, expressionOperator.LinqExpression.Type, original.Method);
				}
				case ExpressionType.ConvertChecked: {
					UnaryExpression original = ((UnaryExpression)expressionOperator.LinqExpression);
					Expression currentOperand = init[0];
					if(currentOperand.Type == typeof(object) && original.Operand.Type != typeof(object)) {
						currentOperand = Expression.Convert(currentOperand, original.Operand.Type);
					}
					return ElementwiseConversion(currentOperand, expressionOperator.LinqExpression.Type) ?? Expression.ConvertChecked(currentOperand, expressionOperator.LinqExpression.Type, ((UnaryExpression)expressionOperator.LinqExpression).Method);
				}
				case ExpressionType.Negate:
					return Expression.Negate(init[0], ((UnaryExpression)expressionOperator.LinqExpression).Method);
				case ExpressionType.NegateChecked:
					return Expression.NegateChecked(init[0], ((UnaryExpression)expressionOperator.LinqExpression).Method);
				case ExpressionType.Not:
					return Expression.Not(init[0], ((UnaryExpression)expressionOperator.LinqExpression).Method);
				case ExpressionType.Quote:
					return Expression.Quote(init[0]);
				case ExpressionType.UnaryPlus:
					return Expression.UnaryPlus(init[0], ((UnaryExpression)expressionOperator.LinqExpression).Method);
				case ExpressionType.Conditional: {
					ConditionalExpression conditional = (ConditionalExpression)expressionOperator.LinqExpression;
					if(init[0].Type != conditional.Test.Type) init[0] = Expression.Convert(init[0], conditional.Test.Type);
					if(init[1].Type != conditional.IfTrue.Type) init[1] = Expression.Convert(init[1], conditional.IfTrue.Type);
					if(init[2].Type != conditional.IfFalse.Type) init[2] = Expression.Convert(init[2], conditional.IfFalse.Type);
					return Expression.Condition(init[0], init[1], init[2]);
				}
				case ExpressionType.Lambda: {
					LambdaExpression lampda = (LambdaExpression)expressionOperator.LinqExpression;
					return Expression.Lambda(init[0], lampda.Parameters.ToArray());
				}
				case ExpressionType.TypeIs:
					return Expression.TypeIs(init[0], ((TypeBinaryExpression)expressionOperator.LinqExpression).TypeOperand);
				case ExpressionType.TypeAs:
					return Expression.TypeAs(init[0], ((UnaryExpression)expressionOperator.LinqExpression).Type);
			}
			throw new NotSupportedException(Res.GetString(Res.LinqToXpo_CurrentExpressionWithX0IsNotSupported, expressionOperator.LinqExpression));
		}
		readonly static Type[] convertedInterfaces = new Type[] {
			typeof(IEnumerable<>),
			typeof(ICollection<>),
			typeof(IList<>)
		};
		static readonly ConstructorInfo timeSpanFromTicksConstructor = typeof(TimeSpan).GetConstructor(new Type[] { typeof(long) });
		Expression CreateTimeStampFromTicksExpression(Expression ticksEpression) {
			if(ticksEpression.Type != typeof(long)) {
				ticksEpression = Expression.Convert(ticksEpression, typeof(long));
			}
			return Expression.New(timeSpanFromTicksConstructor, ticksEpression);
		}
		static Delegate GetElementwiseConversionDelegate(Type destTypeArgument, Type sourceTypeArgument, ParameterExpression parameter, out Type delegateType) {
			delegateType = typeof(Func<,>).MakeGenericType(sourceTypeArgument, destTypeArgument);
			Delegate func;
			lock(elementwiseConversionCache) {
				if(!elementwiseConversionCache.TryGetValue(delegateType, out func)) {
					func = Expression.Lambda(delegateType, Expression.Convert(parameter, destTypeArgument), parameter).Compile();
					elementwiseConversionCache.Add(delegateType, func);
				}
				return func;
			}
		}
		static Expression MakeElementwiseConversionExpression(Expression source, Type targetTypeArgument, Type sourceTypeArgument) {
			var parameter = Expression.Parameter(sourceTypeArgument, "s");
			Type delegateType;
			var func = Expression.Constant(GetElementwiseConversionDelegate(targetTypeArgument, sourceTypeArgument, parameter, out delegateType), delegateType);
			var select = Expression.Call(typeof(Enumerable), "Select", new Type[] { sourceTypeArgument, targetTypeArgument }, source, func);
			return Expression.Call(typeof(Enumerable), "ToList", new Type[] { targetTypeArgument }, select);
		}
		static Expression ElementwiseConversion(Expression source, Type targetType) {
			Type sourceType = source.Type;
			if(targetType.IsInterface && targetType.IsGenericType) {
				Type targetTypeGenericDefinition = targetType.GetGenericTypeDefinition();
				if(convertedInterfaces.Any(i => i == targetTypeGenericDefinition)) {
					Type destTypeArgument = targetType.GetGenericArguments()[0];
					if(sourceType.IsGenericType) {
						if(Parser.IsImplementsInterface(targetType, sourceType) || Parser.IsImplementsInterface(sourceType, targetType))
							return null;
						Type[] sourceTypeArguments = sourceType.GetGenericArguments();
						if(sourceTypeArguments.Length == 1) {
							return MakeElementwiseConversionExpression(source, destTypeArgument, sourceTypeArguments[0]);
						}
					}
					else if(sourceType == typeof(IEnumerable) || sourceType.GetInterfaces().Any(i => i == typeof(IEnumerable))) {
						if(Parser.IsImplementsInterface(sourceType, targetType))
							return null;
						Type typedSourceType = sourceType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
						Expression typedSource;
						Type sourceArgumentType;
						if(typedSourceType == null) {
							sourceArgumentType = typeof(object);
							typedSource = Expression.Call(typeof(Enumerable), "OfType", new Type[] { sourceArgumentType }, source);
						}
						else {
							sourceArgumentType = typedSourceType.GetGenericArguments()[0];
							typedSource = Expression.Convert(source, typedSourceType);
						}
						return MakeElementwiseConversionExpression(typedSource, destTypeArgument, sourceArgumentType);
					}
				}
			}
			return null;
		}
		CreateItemDelegate CreateItemCore(Type type, MemberInitOperator last, CriteriaOperatorCollection effectiveProps) {
			if(!last.CreateNewObject) {
				if(!type.IsArray) {
					var singleProperty = last.Members[0].Property;
					if(!(singleProperty is ExpressionAccessOperator) && !(singleProperty is ConstantCompiler) && (effectiveProps == null || effectiveProps.Count > 0)) {
						if(type.IsValueType && Nullable.GetUnderlyingType(type) == null) {
							var defaultValue = Activator.CreateInstance(type);
							if(type == typeof(TimeSpan)) {
								return (row) => {
									if(row[0] is long) {
										return new TimeSpan((long)row[0]);
									}
									else if(row[0] is double) {
										return TimeSpan.FromSeconds((double)row[0]);
									}
									return row[0] ?? defaultValue;
								};
							}
							else {
								return (row) => { return row[0] ?? defaultValue; };
							}
						}
						else {
							return (row) => { return row[0]; };
						}
					}
				}
				else {
					if(type == typeof(object[])) {
						return (row) => { return row; };
					}
				}
			}
			CriteriaTypeResolver resolver = new Resolver(ObjectClassInfo);
			List<object> projectionSample = new List<object>(40);
			if(GetProjectionSample(type, resolver, last, effectiveProps, projectionSample)) {
				CreateItemCommonKey key = new CreateItemCommonKey { Type = type, ProjectionSample = projectionSample };
				lock(createItemCommonCache) {
					CreateItemDelegate create;
					if(!createItemCommonCache.TryGetValue(key, out create)) {
						create = CreateCreateItemDelegate(type, last, resolver);
						createItemCommonCache.Add(key, create);
					}
					return create;
				}
			}
			return CreateCreateItemDelegate(type, last, resolver);
		}
		CreateItemDelegate CreateCreateItemDelegate(Type type, MemberInitOperator last, CriteriaTypeResolver resolver) {
			ParameterExpression row = Expression.Parameter(typeof(object[]), "row");
			int rowIndex = 0;
			Expression createExpression = CreateSubItemCore(type, last, resolver, row, ref rowIndex);
			if(createExpression.NodeType == ExpressionType.MemberInit) {
				createExpression = Expression.Convert(createExpression, typeof(object));
			}
			if(createExpression.NodeType == ExpressionType.Quote) {
				createExpression = ((UnaryExpression)createExpression).Operand;
			}
			if(createExpression.NodeType == ExpressionType.Lambda) {
				createExpression = ((LambdaExpression)createExpression).Body;
			}
			if(createExpression.Type != typeof(object)) {
				createExpression = Expression.Convert(createExpression, typeof(object));
			}
			return Expression.Lambda<CreateItemDelegate>(createExpression, row).Compile();
		}
		static readonly object projectionSamplePropsStart = new object();
		static readonly object projectionSamplePropsNull = new object();
		static readonly object projectionSamplePropsEnd = new object();
		static readonly object projectionSampleMIO = new object();
		static readonly object projectionSampleEAO = new object();
		static readonly object projectionSampleBegin = new object();
		static readonly object projectionFreeQueryThis = new object();
		static readonly object projectionSampleEnd = new object();
		bool GetProjectionSample(Type type, CriteriaTypeResolver resolver, CriteriaOperator property, CriteriaOperatorCollection effectiveProps, List<object> projectionSample) {
			if(effectiveProps != null) {
				projectionSample.Add(projectionSamplePropsStart);
				foreach(CriteriaOperator prop in effectiveProps) {
					projectionSample.Add(IsNull(prop) ? projectionSamplePropsNull : GetSourceType(resolver, prop));
				}
				projectionSample.Add(projectionSamplePropsEnd);
			}
			if(property is GroupSet) {
				CriteriaOperatorCollection grouping = GetGrouping();
				for(int i = 0; i < grouping.Count; i++) {
					projectionSample.Add(GetSourceType(resolver, grouping[i]));
				}
				return true;
			}
			if(property is QuerySet) {
				QuerySet qs = (QuerySet)property;
				if(qs is FreeQuerySet) {
					FreeQuerySet fqs = (FreeQuerySet)qs;
					if(IsNull(fqs.Projection)) {
						projectionSample.Add(projectionFreeQueryThis);
					}
					else {
						CriteriaOperator expression;
						if(!fqs.TryGetProjectionSingleProperty(out expression)) {
							throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ComplexDataSelectionIsNotSupportedPerhapsY));
						}
						projectionSample.Add(expression);
					}
					projectionSample.Add(fqs.JoinType);
					return true;
				}
				if(!qs.IsEmpty && IsNull(qs.Property) || !IsNull(qs.Condition)) {
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ComplexDataSelectionIsNotSupportedPerhapsY));
				}
				projectionSample.Add(type);
				return true;
			}
			if(property is MemberInitOperator) {
				MemberInitOperator nestedMemberInit = (MemberInitOperator)property;
				projectionSample.Add(projectionSampleMIO);
				projectionSample.Add(nestedMemberInit.CreateNewObject);
				projectionSample.Add(nestedMemberInit.UseConstructor);
				projectionSample.Add(nestedMemberInit.DeclaringTypeName);
				if(nestedMemberInit.UseConstructor) {
					projectionSample.Add(nestedMemberInit.GetConstructor(type));
				}
				projectionSample.Add(projectionSampleBegin);
				Type[] sourceTypes = nestedMemberInit.GetSourceTypes(type, resolver);
				for(int i = 0; i < nestedMemberInit.Members.Count; i++) {
					projectionSample.Add(nestedMemberInit.Members[i].MemberName);
					if(!GetProjectionSample(sourceTypes[i], resolver, nestedMemberInit.Members[i].Property, null, projectionSample))
						return false;
				}
				projectionSample.Add(projectionSampleEnd);
				return true;
			}
			if(property is ExpressionAccessOperator) {
				ExpressionAccessOperator expressionOp = (ExpressionAccessOperator)property;
				projectionSample.Add(projectionSampleEAO);
				projectionSample.Add(expressionOp.LinqExpression.NodeType);
				projectionSample.Add(expressionOp.LinqExpression.Type);
				switch(expressionOp.LinqExpression.NodeType) {
					case ExpressionType.Call:
						projectionSample.Add(((MethodCallExpression)expressionOp.LinqExpression).Method);
						break;
					case ExpressionType.MemberAccess:
						projectionSample.Add(((MemberExpression)expressionOp.LinqExpression).Member);
						break;
				}
				projectionSample.Add(projectionSampleBegin);
				Type[] sourceTypes = expressionOp.GetSourceTypes(type, resolver);
				if(expressionOp.SourceItems.Length == 0) {
					projectionSample.Add(expressionOp.LinqExpression.ToString());
				}
				else {
					for(int i = 0; i < expressionOp.SourceItems.Length; i++) {
						if(!GetProjectionSample(sourceTypes[i], resolver, expressionOp.SourceItems[i], null, projectionSample))
							return false;
					}
				}
				projectionSample.Add(projectionSampleEnd);
				return true;
			}
			if(property is OperandProperty) {
				projectionSample.Add(((OperandProperty)property).PropertyName);
			}
			if(property is ConstantCompiler) {
				return false;
			}
			if(ReferenceEquals(property, null)) {
				projectionSample.Add(typeof(int));
				return true;
			}
			projectionSample.Add(type);
			return true;
		}
		class SelectionPropertyPatcher : ClientCriteriaLazyPatcherBase, ILinqExtendedCriteriaVisitor<CriteriaOperator> {
			readonly CriteriaOperatorCollection props;
			readonly CriteriaOperatorCollection correspondingProps;
			readonly XPQueryBase owner;
			bool isRoot = true;
			SelectionPropertyPatcher(XPQueryBase owner, CriteriaOperatorCollection props, CriteriaOperatorCollection correspondingProps) {
				this.props = props;
				this.correspondingProps = correspondingProps;
				this.owner = owner;
			}
			public static void ProcessAndAdd(XPQueryBase owner, CriteriaOperatorCollection props, CriteriaOperatorCollection correspondingProps, CriteriaOperator property) {
				if(ReferenceEquals(property, null)) {
					correspondingProps.Add(property);
					props.Add(new ConstantValue(0));
					return;
				}
				var result = new SelectionPropertyPatcher(owner, props, correspondingProps).Process(property);
				if(ReferenceEquals(result, null)) {
					return;
				}
				correspondingProps.Add(result);
				props.Add(result);
			}
			OperandProperty ProcessCollectionProperty(OperandProperty collectionProperty) {
				var processed = Process(collectionProperty);
				if(ReferenceEquals(processed, null) || (processed is OperandProperty))
					return (OperandProperty)processed;
				throw new InvalidOperationException("Process within ProcessCollectionProperty expected to return OperandProperty or null; " + processed.GetType().FullName + " (" + processed.ToString() + ") returned instead");
			}
			public override CriteriaOperator Visit(AggregateOperand theOperand) {
				return GoesOutOfRoot((op) => {
					if(op.AggregateType != Aggregate.Custom) {
						CriteriaOperator aggregateExpression;
						if(NeedGetOutOfContextUp(Process(op.AggregatedExpression), out aggregateExpression)) {
							return aggregateExpression;
						}
						return NewIfDifferent(op, ProcessCollectionProperty(op.CollectionProperty), aggregateExpression, Process(op.Condition));
					}
					return NewIfDifferent(op, ProcessCollectionProperty(op.CollectionProperty), Process(op.CustomAggregateOperands), Process(op.Condition));
				}, theOperand);
			}
			public override CriteriaOperator Visit(JoinOperand theOperand) {
				return GoesOutOfRoot((op) => {
					if(op.AggregateType != Aggregate.Custom) {
						CriteriaOperator aggregateExpression;
						if(NeedGetOutOfContextUp(Process(op.AggregatedExpression), out aggregateExpression)) {
							return aggregateExpression;
						}
						return NewIfDifferent(op, Process(op.Condition), aggregateExpression);
					}
					return NewIfDifferent(op, Process(op.Condition), Process(op.CustomAggregateOperands));
				}, theOperand);
			}
			public override CriteriaOperator Visit(FunctionOperator theOperator) {
				return GoesOutOfRoot((op) => {
					return base.Visit(op);
				}, theOperator);
			}
			public override CriteriaOperator Visit(InOperator theOperator) {
				return GoesOutOfRoot((op) => {
					return base.Visit(op);
				}, theOperator);
			}
			public override CriteriaOperator Visit(BetweenOperator theOperator) {
				return GoesOutOfRoot((op) => {
					return base.Visit(op);
				}, theOperator);
			}
			public override CriteriaOperator Visit(BinaryOperator theOperator) {
				return GoesOutOfRoot((op) => {
					return base.Visit(op);
				}, theOperator);
			}
			public override CriteriaOperator Visit(GroupOperator theOperator) {
				return GoesOutOfRoot((op) => {
					return base.Visit(op);
				}, theOperator);
			}
			public override CriteriaOperator Visit(UnaryOperator theOperator) {
				return GoesOutOfRoot((op) => {
					return base.Visit(op);
				}, theOperator);
			}
			CriteriaOperator ILinqExtendedCriteriaVisitor<CriteriaOperator>.Visit(MemberInitOperator theOperand) {
				if(!isRoot) {
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ComplexDataSelectionIsNotSupportedPerhapsY));
				}
				foreach(XPMemberAssignment nestedMi in theOperand.Members) {
					AddToPropsIfNotProcessed(nestedMi.Property);
				}
				return null;
			}
			CriteriaOperator ILinqExtendedCriteriaVisitor<CriteriaOperator>.Visit(ExpressionAccessOperator theOperand) {
				if(!isRoot) {
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ComplexDataSelectionIsNotSupportedPerhapsY));
				}
				for(int i = 0; i < theOperand.SourceItems.Length; i++) {
					AddToPropsIfNotProcessed(theOperand.SourceItems[i]);
				}
				return null;
			}
			CriteriaOperator ILinqExtendedCriteriaVisitor<CriteriaOperator>.Visit(QuerySet theOperand) {
				if(theOperand is GroupSet) {
					if(!isRoot) {
						throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ComplexDataSelectionIsNotSupportedPerhapsY));
					}
					correspondingProps.Add(theOperand);
					props.AddRange(owner.GetGrouping());
					return null;
				}
				if(!IsNull(theOperand.Property) && theOperand.Property.PropertyName == "^" && IsNull(theOperand.Projection) && IsNull(theOperand.Condition)) {
					var result = Parser.UpThisCriteria;
					if(isRoot) {
						correspondingProps.Add(result);
						props.Add(result);
						return null;
					}
					return result;
				}
				if(theOperand is FreeQuerySet || (!IsNull(theOperand.Projection) && !IsNull(theOperand.Property)) || (!isRoot && !IsNull(theOperand.Property))) {
					CriteriaOperator expression;
					if(IsNull(theOperand.Projection)) {
						expression = Parser.ThisCriteria;
					}
					else {
						if(!theOperand.TryGetProjectionSingleProperty(out expression)) {
							throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ComplexDataSelectionIsNotSupportedPerhapsY));
						}
					}
					expression = GoesOutOfRoot((op) => Process(op), expression);
					CriteriaOperator result;
					if(NeedGetOutOfContextUp(expression, out expression)) {
						result = expression;
					}
					else {
						result = theOperand.CreateCriteriaOperator(expression, Aggregate.Single);
					}
					if(isRoot) {
						correspondingProps.Add(result);
						props.Add(result);
						return null;
					}
					return result;
				}
				if(!theOperand.IsEmpty && IsNull(theOperand.Property) || !IsNull(theOperand.Condition)) {
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ComplexDataSelectionIsNotSupportedPerhapsY));
				}
				if(isRoot) {
					correspondingProps.Add(theOperand);
					props.Add(Parser.ThisCriteria);
					return null;
				}
				return Parser.ThisCriteria;
			}
			bool NeedGetOutOfContextUp(CriteriaOperator theOperand, out CriteriaOperator fixedOperand) {
				fixedOperand = theOperand;
				var property = theOperand as OperandProperty;
				if(!IsNull(property) && !IsNull(property.PropertyName) && property.PropertyName == Parser.UpThisCriteria.PropertyName) {
					fixedOperand = Parser.ThisCriteria;
					return true;
				}
				var set = theOperand as QuerySet;
				if(!IsNull(set)) {
					if(set is FreeQuerySet || IsNull(set.Property) || IsNull(set.Property.PropertyName)) {
						return false;
					}
					if(set.Property.PropertyName == "^") {
						fixedOperand = new QuerySet() {
							Projection = set.Projection,
							Condition = set.Condition,
							Property = null
						};
						return true;
					}
					if(set.Property.PropertyName.StartsWith("^.")) {
						fixedOperand = new QuerySet() {
							Projection = set.Projection,
							Condition = set.Condition,
							Property = new OperandProperty(set.Property.PropertyName.Substring(2))
						};
						return true;
					}
				}
				var aggregateOperand = theOperand as AggregateOperand;
				if(!IsNull(aggregateOperand) && aggregateOperand.AggregateType == Aggregate.Single) {
					if(IsNull(aggregateOperand.CollectionProperty) || IsNull(aggregateOperand.CollectionProperty.PropertyName)) {
						return false;
					}
					if(aggregateOperand.CollectionProperty.PropertyName == "^") {
						if(!IsNull(aggregateOperand.Condition)) {
							throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ComplexDataSelectionIsNotSupportedPerhapsY));
						}
						fixedOperand = aggregateOperand.AggregatedExpression;
						return true;
					}
					if(aggregateOperand.CollectionProperty.PropertyName.StartsWith("^.")) {
						fixedOperand = new AggregateOperand(
							new OperandProperty(aggregateOperand.CollectionProperty.PropertyName.Substring(2)),
							aggregateOperand.AggregatedExpression,
							Aggregate.Single,
							aggregateOperand.Condition
						);
						return true;
					}
				}
				return false;
			}
			public override CriteriaOperator Visit(OperandValue theOperand) {
				if(isRoot) {
					if(theOperand is ConstantCompiler) {
						return null;
					}
				}
				return GoesOutOfRoot((op) => base.Visit(op), theOperand);
			}
			CriteriaOperator GoesOutOfRoot<T>(Func<T, CriteriaOperator> work, T operand) where T : CriteriaOperator {
				bool oldIsRoot = isRoot;
				try {
					isRoot = false;
					return work(operand);
				}
				finally {
					isRoot = oldIsRoot;
				}
			}
			void AddToPropsIfNotProcessed(CriteriaOperator source) {
				if(IsNull(source)) {
					return;
				}
				var result = Process(source);
				if(IsNull(result)) {
					return;
				}
				correspondingProps.Add(result);
				props.Add(result);
			}
		}
		ICollection GetData(Type type) {
			if(IsNull(Projection)) {
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_CurrentExpressionWithX0IsNotSupported, "Select"));
			}
			if(Sorting == null && Skip.HasValue) {
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_SkipOperationIsNotSupportedWithoutSorting));
			}
			CriteriaOperatorCollection props = new CriteriaOperatorCollection();
			CriteriaOperatorCollection correspondingProps = new CriteriaOperatorCollection();
			MemberInitOperator last = Projection;
			StringBuilder projectionString = new StringBuilder();
			foreach(XPMemberAssignment mi in last.Members) {
				SelectionPropertyPatcher.ProcessAndAdd(this, props, correspondingProps, mi.Property);
			}
			List<object[]> data = SessionSelectData(ObjectClassInfo, props.Count > 0 ? props : new CriteriaOperatorCollection() { oneConstantValue }, Criteria, GetGrouping(), GroupCriteria,
					 Skip ?? 0, Top ?? 0, Sorting);
			DataPostProcessing(correspondingProps, data, type);
			List<object> list = new List<object>(data.Count);
			CreateItemDelegate create = CreateItem(type, last, props);
			foreach(object[] rec in data)
				list.Add(create(rec));
			return list;
		}
		async Task<ICollection> GetDataAsync(Type type, CancellationToken cancellationToken = default(CancellationToken)) {
			if(IsNull(Projection)) {
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_CurrentExpressionWithX0IsNotSupported, "Select"));
			}
			if(Sorting == null && Skip.HasValue) {
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_SkipOperationIsNotSupportedWithoutSorting));
			}
			CriteriaOperatorCollection props = new CriteriaOperatorCollection();
			CriteriaOperatorCollection correspondingProps = new CriteriaOperatorCollection();
			MemberInitOperator last = Projection;
			StringBuilder projectionString = new StringBuilder();
			foreach(XPMemberAssignment mi in last.Members) {
				SelectionPropertyPatcher.ProcessAndAdd(this, props, correspondingProps, mi.Property);
			}
			List<object[]> data = await SessionSelectDataAsync(ObjectClassInfo, props.Count > 0 ? props : new CriteriaOperatorCollection() { oneConstantValue }, Criteria, GetGrouping(), GroupCriteria,
					 Skip ?? 0, Top ?? 0, Sorting, cancellationToken);
			await DataPostProcessingAsync(correspondingProps, data, type, cancellationToken);
			List<object> list = new List<object>(data.Count);
			CreateItemDelegate create = CreateItem(type, last, props);
			foreach(object[] rec in data)
				list.Add(create(rec));
			return list;
		}
		ICollection GetObjects() {
			if(Sorting == null && Skip.HasValue) {
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_SkipOperationIsNotSupportedWithoutSorting));
			}
			return SessionGetObjects(ObjectClassInfo, Criteria, Sorting, Skip ?? 0,
				Top.HasValue ? Top.Value : 0);
		}
		Task<ICollection> GetObjectsAsync(CancellationToken cancellationToken = default(CancellationToken)) {
			if(Sorting == null && Skip.HasValue) {
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_SkipOperationIsNotSupportedWithoutSorting));
			}
			return SessionGetObjectsAsync(ObjectClassInfo, Criteria, Sorting, Skip ?? 0,
				Top.HasValue ? Top.Value : 0, cancellationToken);
		}
		internal void EnumerateAsync(Type type, AsyncLoadObjectsCallback callback) {
			if(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IGrouping<,>))
				throw new NotSupportedException();
			if(!IsNull(Projection) || IsGroup)
				throw new NotSupportedException();
			if(Sorting == null && Skip.HasValue) {
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_SkipOperationIsNotSupportedWithoutSorting));
			}
			SessionGetObjectsAsync(ObjectClassInfo, Criteria, Sorting, Skip.HasValue ? Skip.Value : 0,
				Top.HasValue ? Top.Value : 0, callback);
		}
		protected ICollection Enumerate(Type type) {
			if(ZeroTop)
				return new List<object>();
			if(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IGrouping<,>))
				return EnumerateGroups(type);
			return IsNull(Projection) && !IsGroup ? GetObjects() : GetData(type);
		}
		internal Task<ICollection> EnumerateAsync(Type type, CancellationToken cancellationToken = default(CancellationToken)) {
			if(ZeroTop) {
				return Task.FromResult<ICollection>(new List<object>());
			}
			if(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IGrouping<,>)) {
				return EnumerateGroupsAsync(type, cancellationToken);
			}
			if(IsNull(Projection) && !IsGroup) {
				return GetObjectsAsync(cancellationToken);
			}
			return GetDataAsync(type, cancellationToken);
		}
		ICollection EnumerateGroups(Type type) {
			if(Sorting == null && Skip.HasValue) {
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_SkipOperationIsNotSupportedWithoutSorting));
			}
			OperandValue groupKeyValue;
			List<object[]> data;
			if(GroupKey.Is(out groupKeyValue)) {
				data = new List<object[]>(new[] { new[] { groupKeyValue.Value } });
			}
			else {
				data = SessionSelectData(ObjectClassInfo, GetGrouping(), Criteria, GetGrouping(), GroupCriteria,
									Skip.HasValue ? Skip.Value : 0, Top.HasValue ? Top.Value : 0, Sorting);
			}
			MemberInitOperator init = GroupKey as MemberInitOperator;
			CriteriaOperatorCollection props = new CriteriaOperatorCollection();
			CriteriaOperatorCollection correspondingProps = new CriteriaOperatorCollection();
			if(IsNull(init)) {
				XPMemberAssignmentCollection members = new XPMemberAssignmentCollection();
				members.Add(new XPMemberAssignment(GroupKey));
				props.Add(GroupKey);
				correspondingProps.Add(GroupKey);
				init = new MemberInitOperator(type, members, false);
			}
			else {
				foreach(XPMemberAssignment mi in init.Members) {
					SelectionPropertyPatcher.ProcessAndAdd(this, props, correspondingProps, mi.Property);
				}
			}
			DataPostProcessing(correspondingProps, data, type);
			List<object> list = new List<object>(data.Count);
			CreateItemDelegate create = CreateGroupItem(type, Projection);
			foreach(object[] rec in data)
				list.Add(create(rec));
			return list;
		}
		async Task<ICollection> EnumerateGroupsAsync(Type type, CancellationToken cancellationToken = default(CancellationToken)) {
			if(Sorting == null && Skip.HasValue) {
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_SkipOperationIsNotSupportedWithoutSorting));
			}
			OperandValue groupKeyValue;
			List<object[]> data;
			if(GroupKey.Is(out groupKeyValue)) {
				data = new List<object[]>(new[] { new[] { groupKeyValue.Value } });
			}
			else {
				data = await SessionSelectDataAsync(ObjectClassInfo, GetGrouping(), Criteria, GetGrouping(), GroupCriteria,
					Skip.HasValue ? Skip.Value : 0, Top.HasValue ? Top.Value : 0, Sorting, cancellationToken);
			}
			MemberInitOperator init = GroupKey as MemberInitOperator;
			CriteriaOperatorCollection props = new CriteriaOperatorCollection();
			CriteriaOperatorCollection correspondingProps = new CriteriaOperatorCollection();
			if(IsNull(init)) {
				XPMemberAssignmentCollection members = new XPMemberAssignmentCollection();
				members.Add(new XPMemberAssignment(GroupKey));
				props.Add(GroupKey);
				correspondingProps.Add(GroupKey);
				init = new MemberInitOperator(type, members, false);
			}
			else {
				foreach(XPMemberAssignment mi in init.Members) {
					SelectionPropertyPatcher.ProcessAndAdd(this, props, correspondingProps, mi.Property);
				}
			}
			cancellationToken.ThrowIfCancellationRequested();
			await DataPostProcessingAsync(correspondingProps, data, type, cancellationToken);
			List<object> list = new List<object>(data.Count);
			CreateItemDelegate create = CreateGroupItem(type, Projection);
			foreach(object[] rec in data)
				list.Add(create(rec));
			return list;
		}
		CreateItemDelegate CreateGroupItem(Type type, MemberInitOperator last) {
			if(create == null)
				create = CreateGroupItemCore(type, last);
			return create;
		}
		CreateItemDelegate CreateGroupItemCore(Type type, MemberInitOperator last) {
			Type genericGroup = typeof(GroupCollection<,>).MakeGenericType(type.GetGenericArguments());
			ParameterExpression row = Expression.Parameter(typeof(object[]), "row");
			MemberInitOperator init = GroupKey as MemberInitOperator;
			if(IsNull(init)) {
				XPMemberAssignmentCollection members = new XPMemberAssignmentCollection();
				members.Add(new XPMemberAssignment(GroupKey));
				init = new MemberInitOperator(type, members, false);
			}
			Type keyType = type.GetGenericArguments()[0];
			CreateItemDelegate keyCreator = CreateItemCore(keyType, init, null);
			Expression<CreateItemDelegate> l = Expression.Lambda<CreateItemDelegate>(
				Expression.New(genericGroup.GetConstructor(new Type[] { keyType, typeof(XPQueryBase), typeof(CriteriaOperatorCollection), typeof(MemberInitOperator), typeof(object[]) }),
				ConvertToType(Expression.Invoke(Expression.Constant(keyCreator), row), keyType, keyType), Expression.Constant(this), Expression.Constant(GetGrouping() ?? new CriteriaOperatorCollection()), Expression.Constant(last, typeof(MemberInitOperator)), row), row);
			return l.Compile();
		}
		class GroupCollection<TKey, TElement> : XPQuery<TElement>, IGrouping<TKey, TElement> {
			readonly TKey key;
			public GroupCollection(TKey key, XPQueryBase parent, CriteriaOperatorCollection props, MemberInitOperator projection, object[] row)
				: base(parent.session, parent.session.DataLayer, parent.session.Dictionary) {
				this.key = key;
				query.InTransaction = parent.query.InTransaction;
				query.ObjectTypeName = parent.query.ObjectTypeName;
				objectClassInfo = parent.objectClassInfo;
				List<CriteriaOperator> ops = new List<CriteriaOperator>();
				ops.Add(parent.Criteria);
				for(int i = 0; i < props.Count; i++) {
					ops.Add(new BinaryOperator(props[i], new OperandValue(row[i]), BinaryOperatorType.Equal));
				}
				Criteria = GroupOperator.And(ops.ToArray());
				query.Projection = projection;
			}
			public TKey Key {
				get { return key; }
			}
		}
		protected abstract object CloneCore();
		IServiceProvider IInfrastructure<IServiceProvider>.Instance {
			get {
				return ((IInfrastructure<IServiceProvider>)session)?.Instance;
			}
		}
	}
	sealed class Parser {
		internal static readonly OperandProperty ThisCriteria = new OperandProperty("This");
		internal static readonly OperandProperty UpThisCriteria = new OperandProperty("^.This");
		static bool IsNull(object val) {
			return val == null;
		}
		XPDictionary Dictionary { get { return classInfo.Dictionary; } }
		readonly Dictionary<ParameterExpression, CriteriaOperator> parameters;
		readonly XPClassInfo[] upDepthList;
		readonly XPClassInfo classInfo;
		readonly CustomCriteriaCollection customCriteriaCollection;
		readonly Dictionary<Expression, CriteriaOperator> cache;
		readonly ParamExpression Resolver;
		int forceExpressionOperatorForConversion = 0;
		int forceExpressionOperatorForQuerySet = 0;
		Parser(XPClassInfo[] upDepthList, CustomCriteriaCollection customCriteriaCollection, ParamExpression resolver, Dictionary<ParameterExpression, CriteriaOperator> parameters) {
			this.classInfo = upDepthList[0];
			this.upDepthList = upDepthList;
			this.customCriteriaCollection = customCriteriaCollection;
			this.parameters = parameters;
			if(resolver != null) {
				Resolver = resolver;
				cache = classInfo.CreateCache(() => new Dictionary<Expression, CriteriaOperator>());
			}
		}
		Parser(XPClassInfo classInfo, CustomCriteriaCollection customCriteriaCollection, ParamExpression resolver, Dictionary<ParameterExpression, CriteriaOperator> parameters) {
			this.classInfo = classInfo;
			this.upDepthList = new XPClassInfo[] { classInfo };
			this.customCriteriaCollection = customCriteriaCollection;
			this.parameters = parameters;
			if(resolver != null) {
				Resolver = resolver;
				cache = classInfo.CreateCache(() => new Dictionary<Expression, CriteriaOperator>());
			}
		}
		internal static bool HasExpressionAccess(params CriteriaOperator[] operands) {
			if(operands == null || operands.Length == 0) return false;
			foreach(CriteriaOperator operand in operands) {
				if(operand is ExpressionAccessOperator)
					return true;
			}
			return false;
		}
		internal static bool HasExpressionAccess(CriteriaOperator operand) {
			return operand is ExpressionAccessOperator;
		}
		internal static ExpressionAccessOperator GetCauseOfExpressionAccess(CriteriaOperator operand) {
			ExpressionAccessOperator current = operand as ExpressionAccessOperator;
			if(IsNull(current))
				return null;
			bool isFound;
			do {
				isFound = false;
				foreach(var op in current.SourceItems) {
					var expressionAccessOperator = op as ExpressionAccessOperator;
					if(!IsNull(expressionAccessOperator)) {
						current = expressionAccessOperator;
						isFound = true;
						break;
					}
				}
			} while(isFound);
			return current;
		}
		internal static string GetCauseStringOfExpressionAccess(CriteriaOperator operand) {
			var result = GetCauseOfExpressionAccess(operand);
			return IsNull(result) ? "unknown" : result.LinqExpression.ToString(); 
		}
		internal static bool HasExpressionAccess(CriteriaOperator operand1, CriteriaOperator operand2) {
			return operand1 is ExpressionAccessOperator || operand2 is ExpressionAccessOperator;
		}
		internal static bool HasExpressionAccess(CriteriaOperator operand1, CriteriaOperator operand2, CriteriaOperator operand3) {
			return operand1 is ExpressionAccessOperator || operand2 is ExpressionAccessOperator || operand3 is ExpressionAccessOperator;
		}
		internal static bool IsNotParsableQuerySet(CriteriaOperator co) {
			QuerySet qSet = co as QuerySet;
			if(IsNull(qSet)) {
				return false;
			}
			if(!IsNull(qSet.Property) && IsNull(qSet.Projection)) {
				return true;
			}
			return false;
		}
		internal static bool HasExpressionOrMemberInitOrQuerySet(params CriteriaOperator[] operands) {
			if(operands == null || operands.Length == 0) return false;
			foreach(CriteriaOperator operand in operands) {
				if(operand is ExpressionAccessOperator || operand is MemberInitOperator || IsNotParsableQuerySet(operand))
					return true;
			}
			return false;
		}
		internal static bool HasExpressionOrMemberInitOrQuerySet(CriteriaOperator operand) {
			return operand is ExpressionAccessOperator || operand is MemberInitOperator || IsNotParsableQuerySet(operand);
		}
		internal static bool HasExpressionOrMemberInitOrQuerySet(CriteriaOperator operand1, CriteriaOperator operand2) {
			if(operand1 is ExpressionAccessOperator || operand1 is MemberInitOperator || IsNotParsableQuerySet(operand1)) return true;
			return operand2 is ExpressionAccessOperator || operand2 is MemberInitOperator || IsNotParsableQuerySet(operand2);
		}
		internal static bool HasExpressionOrMemberInitOrQuerySet(CriteriaOperator operand1, CriteriaOperator operand2, CriteriaOperator operand3) {
			if(operand1 is ExpressionAccessOperator || operand1 is MemberInitOperator || IsNotParsableQuerySet(operand1)) return true;
			if(operand2 is ExpressionAccessOperator || operand2 is MemberInitOperator || IsNotParsableQuerySet(operand2)) return true;
			return operand3 is ExpressionAccessOperator || operand3 is MemberInitOperator || IsNotParsableQuerySet(operand3);
		}
		static IList<ParameterExpression> GetLambdaParams(Expression call, int count) {
			UnaryExpression u = call as UnaryExpression;
			if(u != null)
				call = u.Operand;
			LambdaExpression l = call as LambdaExpression;
			if(l != null) {
				IList<ParameterExpression> lParams = l.Parameters;
				if(lParams.Count != count)
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_TheLambdaExpressionWithSuchParametersIsNot, l));
				return lParams;
			}
			if(count == 0)
				return null;
			throw new NotSupportedException(Res.GetString(Res.LinqToXpo_LambdaExpressionIsExpectedX0, call));
		}
		static Expression ExtractLambdaExpressionIfNeeded(Expression call) {
			if(call.NodeType == ExpressionType.MemberAccess && typeof(LambdaExpression).IsAssignableFrom(call.Type)) {
				var memberAccessOperator = new MemberAccessOperator((MemberExpression)call);
				return (Expression)memberAccessOperator.Value;
			}
			return call;
		}
		CriteriaOperator ParseExpression(Type type, Expression expression, params CriteriaOperator[] maps) {
			expression = ExtractLambdaExpressionIfNeeded(expression);
			XPClassInfo oldClassInfo = classInfo;
			if(!IsNull(type)) {
				oldClassInfo = Dictionary.QueryClassInfo(type);
			}
			ParamExpression resolver = GetResolver(ref expression);
			Dictionary<ParameterExpression, CriteriaOperator> parameters = PrepareParametersForJoin();
			AddMaps(expression, maps, parameters);
			Parser p = new Parser(oldClassInfo, customCriteriaCollection, Resolver ?? resolver, parameters);
			return p.ParseExpression(expression);
		}
		static ParamExpression GetResolver(ref Expression expression) {
			MethodCallExpression call = expression as MethodCallExpression;
			ReadOnlyCollection<Expression> arguments;
			if(call == null || (arguments = call.Arguments).Count != 2)
				return null;
			ConstantExpression constant = arguments[1] as ConstantExpression;
			if(constant == null)
				return null;
			ParamExpression resolver = constant.Value as ParamExpression;
			if(resolver != null)
				expression = arguments[0];
			return resolver;
		}
		CriteriaOperator ParseExpression(OperandProperty col, Expression expression, params CriteriaOperator[] maps) {
			expression = ExtractLambdaExpressionIfNeeded(expression);
			int upDepth = 0;
			List<XPClassInfo> newUpDepthList = new List<XPClassInfo>(upDepthList);
			if(!IsNull(col)) {
				XPClassInfo oldClassInfo = classInfo;
				int currentUpDepth = 0;
				string propertyName = col.PropertyName;
				while(propertyName.StartsWith("^.")) {
					propertyName = propertyName.Remove(0, 2);
					currentUpDepth++;
					if(newUpDepthList.Count == 0) throw new InvalidOperationException();
					newUpDepthList.RemoveAt(0);
				}
				MemberInfoCollection path = MemberInfoCollection.ParsePersistentPath(upDepthList[currentUpDepth], propertyName);
				upDepth = path.Count;
				oldClassInfo = path[path.Count - 1].CollectionElementType;
				newUpDepthList.Insert(0, oldClassInfo);
			}
			ParamExpression resolver = GetResolver(ref expression);
			Dictionary<ParameterExpression, CriteriaOperator> parameters = PrepareParameters(expression, col, upDepth);
			AddMaps(expression, maps, parameters);
			Parser p = new Parser(newUpDepthList.ToArray(), customCriteriaCollection, Resolver ?? resolver, parameters);
			return p.ParseExpression(expression);
		}
		public static CriteriaOperator ParseObjectExpression(XPClassInfo classInfo, CustomCriteriaCollection customCriteriaCollection, Expression expression, params CriteriaOperator[] maps) {
			expression = ExtractLambdaExpressionIfNeeded(expression);
			ParamExpression resolver = GetResolver(ref expression);
			Dictionary<ParameterExpression, CriteriaOperator> parameters = new Dictionary<ParameterExpression, CriteriaOperator>();
			AddMaps(expression, maps, parameters);
			Parser p = new Parser(classInfo, customCriteriaCollection, resolver, parameters);
			return p.ParseObjectExpression(expression);
		}
		public static CriteriaOperator ParseExpression(XPClassInfo classInfo, CustomCriteriaCollection customCriteriaCollection, Expression expression, params CriteriaOperator[] maps) {
			expression = ExtractLambdaExpressionIfNeeded(expression);
			ParamExpression resolver = GetResolver(ref expression);
			Dictionary<ParameterExpression, CriteriaOperator> parameters = new Dictionary<ParameterExpression, CriteriaOperator>();
			AddMaps(expression, maps, parameters);
			Parser p = new Parser(classInfo, customCriteriaCollection, resolver, parameters);
			return p.ParseExpression(expression);
		}
		static void AddMaps(Expression expression, CriteriaOperator[] maps, Dictionary<ParameterExpression, CriteriaOperator> parameters) {
			Guard.ArgumentNotNull(maps, nameof(maps));
			IList<ParameterExpression> lParams = GetLambdaParams(expression, maps.Length);
			for(int i = 0; i < maps.Length; i++) {
				parameters[lParams[i]] = IsNull(maps[i]) ? QuerySet.Empty : maps[i];
			}
		}
		CriteriaOperator ParseWithExpressionOperatorForceForConversion(Expression expression) {
			Interlocked.Increment(ref forceExpressionOperatorForConversion);
			try {
				return ParseExpression(expression);
			}
			finally {
				Interlocked.Decrement(ref forceExpressionOperatorForConversion);
			}
		}
		CriteriaOperator ParseWithExpressionOperatorForceForQuerySet(Expression expression) {
			Interlocked.Increment(ref forceExpressionOperatorForQuerySet);
			try {
				return ParseExpression(expression);
			}
			finally {
				Interlocked.Decrement(ref forceExpressionOperatorForQuerySet);
			}
		}
		CriteriaOperator ParseExpression(Expression expression) {
			CriteriaOperator op;
			if(cache != null) {
				lock(cache) {
					if(cache.TryGetValue(expression, out op)) {
						return op;
					}
				}
				bool used = Resolver.Use();
				op = ParseExpressionCore(expression);
				if(!Resolver.UnUse(used) && !(expression is ParameterExpression))
					lock(cache)
						cache[expression] = op;
			}
			else
				op = ParseExpressionCore(expression);
			return op;
		}
		public CriteriaOperator ParseExpressionCore(Expression expression) {
			switch(expression.NodeType) {
				case ExpressionType.Add:
				case ExpressionType.AddChecked:
					return Binary((BinaryExpression)expression, BinaryOperatorType.Plus);
				case ExpressionType.And:
					return IsLogical((BinaryExpression)expression) ? Group((BinaryExpression)expression, GroupOperatorType.And) : Binary((BinaryExpression)expression, BinaryOperatorType.BitwiseAnd);
				case ExpressionType.AndAlso:
					return Group((BinaryExpression)expression, GroupOperatorType.And);
				case ExpressionType.ArrayIndex:
					return ArrayIndex((BinaryExpression)expression);
				case ExpressionType.ArrayLength:
					return ArrayLength((UnaryExpression)expression);
				case ExpressionType.Call:
					return Call((MethodCallExpression)expression);
				case ExpressionType.Coalesce:
					return Coalesce((BinaryExpression)expression);
				case ExpressionType.Conditional:
					return Conditional((ConditionalExpression)expression);
				case ExpressionType.Constant:
					return Constant((ConstantExpression)expression);
				case ExpressionType.Convert:
				case ExpressionType.ConvertChecked:
					return Convert((UnaryExpression)expression);
				case ExpressionType.Divide:
					return Binary((BinaryExpression)expression, BinaryOperatorType.Divide);
				case ExpressionType.Equal:
					return Equal((BinaryExpression)expression);
				case ExpressionType.GreaterThan:
					return LogicalBinary((BinaryExpression)expression, BinaryOperatorType.Greater);
				case ExpressionType.GreaterThanOrEqual:
					return LogicalBinary((BinaryExpression)expression, BinaryOperatorType.GreaterOrEqual);
				case ExpressionType.Lambda: {
					CriteriaOperator bodyOperand = ParseExpression(((LambdaExpression)expression).Body);
					if(bodyOperand is ExpressionAccessOperator) return new ExpressionAccessOperator(expression, bodyOperand);
					return bodyOperand;
				}
				case ExpressionType.LessThan:
					return LogicalBinary((BinaryExpression)expression, BinaryOperatorType.Less);
				case ExpressionType.LessThanOrEqual:
					return LogicalBinary((BinaryExpression)expression, BinaryOperatorType.LessOrEqual);
				case ExpressionType.MemberAccess:
					return MemberAccess((MemberExpression)expression);
				case ExpressionType.MemberInit:
					return MemberInit((MemberInitExpression)expression);
				case ExpressionType.Modulo:
					return Binary((BinaryExpression)expression, BinaryOperatorType.Modulo);
				case ExpressionType.Multiply:
				case ExpressionType.MultiplyChecked:
					return Binary((BinaryExpression)expression, BinaryOperatorType.Multiply);
				case ExpressionType.Negate:
				case ExpressionType.NegateChecked:
					return Unary((UnaryExpression)expression, UnaryOperatorType.Minus);
				case ExpressionType.New:
					return New((NewExpression)expression);
				case ExpressionType.NewArrayInit:
					return NewArrayInit((NewArrayExpression)expression);
				case ExpressionType.Not:
					return Unary((UnaryExpression)expression, UnaryOperatorType.Not);
				case ExpressionType.NotEqual:
					return NotEqual((BinaryExpression)expression);
				case ExpressionType.Or:
					return IsLogical((BinaryExpression)expression) ? Group((BinaryExpression)expression, GroupOperatorType.Or) : Binary((BinaryExpression)expression, BinaryOperatorType.BitwiseOr);
				case ExpressionType.OrElse:
					return Group((BinaryExpression)expression, GroupOperatorType.Or);
				case ExpressionType.Parameter:
					return Parameter((ParameterExpression)expression);
				case ExpressionType.Subtract:
				case ExpressionType.SubtractChecked:
					return Binary((BinaryExpression)expression, BinaryOperatorType.Minus);
				case ExpressionType.TypeIs:
					return TypeIs((TypeBinaryExpression)expression);
				case ExpressionType.TypeAs:
					return TypeAs((UnaryExpression)expression);
				case ExpressionType.Quote: {
					CriteriaOperator qOperand = ParseExpression(((UnaryExpression)expression).Operand);
					if(qOperand is ExpressionAccessOperator) return new ExpressionAccessOperator(expression, qOperand);
					return qOperand;
				}
				case ExpressionType.UnaryPlus:
					return Unary((UnaryExpression)expression, UnaryOperatorType.Plus);
				case ExpressionType.Invoke:
					return ParseInvoke((InvocationExpression)expression);
				case ExpressionType.Default:
					return ParseDefault((DefaultExpression)expression);
			}
			throw new NotSupportedException(Res.GetString(Res.LinqToXpo_CurrentExpressionWithX0IsNotSupported, expression.NodeType));
		}
		public CriteriaOperator ParseInvoke(InvocationExpression invoke) {
			CriteriaOperator[] arguments = new CriteriaOperator[invoke.Arguments.Count + 1];
			arguments[0] = ParseExpression(invoke.Expression);
			for(int i = 0; i < arguments.Length - 1; i++) {
				arguments[i + 1] = ParseExpression(invoke.Arguments[i]);
			}
			return new ExpressionAccessOperator(invoke, arguments);
		}
		static bool IsLogical(BinaryExpression binaryExpression) {
			return binaryExpression.Left.Type == typeof(bool) || binaryExpression.Left.Type == typeof(bool?);
		}
		CriteriaOperator ArrayLength(UnaryExpression expression) {
			CriteriaOperator operand = ParseExpression(expression.Operand);
			if(HasExpressionAccess(operand))
				return new ExpressionAccessOperator(expression, operand);
			if(!(operand is OperandValue))
				return new ExpressionAccessOperator(expression);
			return new ConstantCompiler(Dictionary, expression);
		}
		CriteriaOperator ArrayIndex(BinaryExpression expression) {
			CriteriaOperator left = ParseExpression(expression.Left);
			CriteriaOperator right = ParseExpression(expression.Right);
			if(HasExpressionAccess(left, right))
				return new ExpressionAccessOperator(expression, left, right);
			if(!(left is OperandValue) || !(right is OperandValue))
				return new ExpressionAccessOperator(expression, new ExpressionAccessOperator(expression.Left), right);
			return new ConstantCompiler(Dictionary, expression);
		}
		CriteriaOperator TypeIs(TypeBinaryExpression expression) {
			CriteriaOperator obj = ParseExpression(expression.Expression);
			if(HasExpressionOrMemberInitOrQuerySet(obj)) {
				return new ExpressionAccessOperator(expression, obj);
			}
			return new FunctionOperator("IsInstanceOfType", obj, new OperandValue(expression.TypeOperand.FullName));
		}
		CriteriaOperator TypeAs(UnaryExpression expression) {
			CriteriaOperator obj = ParseExpression(expression.Operand);
			if(HasExpressionOrMemberInitOrQuerySet(obj)) {
				return new ExpressionAccessOperator(expression, obj);
			}
			return Convert(expression);
		}
		CriteriaOperator Group(BinaryExpression expression, GroupOperatorType type) {
			CriteriaOperator left = ParseExpression(expression.Left);
			CriteriaOperator right = ParseExpression(expression.Right);
			if(HasExpressionOrMemberInitOrQuerySet(left, right))
				return new ExpressionAccessOperator(expression, left, right);
			return new GroupOperator(type, left, right);
		}
		bool TryCallConvertClass(MethodCallExpression call, out CriteriaOperator outCriteriaOperator) {
			switch(call.Method.Name) {
				case "ToString": {
					outCriteriaOperator = FnToStr(call, call.Arguments[0]);
					return true;
				}
				case "ToDecimal":
				case "ToDouble":
				case "ToInt32":
				case "ToInt64":
				case "ToSingle":
				case "ToUInt32":
				case "ToUInt64": {
					outCriteriaOperator = ConvertCore(call, call.Arguments[0], call.Type, classInfo);
					return true;
				}
			}
			outCriteriaOperator = null;
			return false;
		}
		static readonly ConcurrentDictionary<Type, object> typeDefaultValues = new ConcurrentDictionary<Type, object>();
		CriteriaOperator ParseDefault(DefaultExpression expression) {
			if(expression.Type == null || !expression.Type.IsValueType) {
				return null;
			}
			object defaultValue = typeDefaultValues.GetOrAdd(expression.Type, (t) => Activator.CreateInstance(t));
			return new ConstantValue(defaultValue);
		}
		CriteriaOperator Unary(UnaryExpression expression, UnaryOperatorType type) {
			CriteriaOperator op = ParseExpression(expression.Operand);
			if(op is OperandValue)
				return new ConstantCompiler(Dictionary, expression);
			if(HasExpressionOrMemberInitOrQuerySet(op))
				return new ExpressionAccessOperator(expression, op);
			UnaryOperator unaryOperator = op as UnaryOperator;
			if(!IsNull(unaryOperator) && unaryOperator.OperatorType == UnaryOperatorType.Not) {
				return unaryOperator.Operand;
			}
			return new UnaryOperator(type, op);
		}
		readonly static object boxedZero = 0;
		public bool BinaryDetectCompare(Expression expression, Expression leftExpression, Expression rightExpression, CriteriaOperator right, BinaryOperatorType type, bool rightIsExpressionAccess, out CriteriaOperator result) {
			MethodCallExpression leftMethodCall = leftExpression as MethodCallExpression;
			if(leftExpression.Type == typeof(int) && rightExpression.Type == typeof(int) && leftMethodCall != null) {
				if(leftMethodCall.Method.Name == "CompareTo" && leftMethodCall.Arguments.Count == 1 && right is OperandValue && object.Equals(((OperandValue)right).Value, boxedZero)) {
					CriteriaOperator innerLeft = ParseExpression(leftMethodCall.Object);
					CriteriaOperator innerRight = ParseExpression(leftMethodCall.Arguments[0]);
					if(rightIsExpressionAccess || HasExpressionOrMemberInitOrQuerySet(innerLeft, innerRight)) {
						result = new ExpressionAccessOperator(expression, ParseExpression(leftExpression), right);
						return true;
					}
					result = new BinaryOperator(innerLeft, innerRight, type);
					return true;
				}
				if(leftMethodCall.Method.Name == "Compare" && leftMethodCall.Arguments.Count == 2 && right is OperandValue && object.Equals(((OperandValue)right).Value, boxedZero)) {
					CriteriaOperator innerLeft = ParseExpression(leftMethodCall.Arguments[0]);
					CriteriaOperator innerRight = ParseExpression(leftMethodCall.Arguments[1]);
					if(rightIsExpressionAccess || HasExpressionOrMemberInitOrQuerySet(innerLeft, innerRight)) {
						result = new ExpressionAccessOperator(expression, ParseExpression(leftExpression), right);
						return true;
					}
					result = new BinaryOperator(innerLeft, innerRight, type);
					return true;
				}
			}
			result = null;
			return false;
		}
		CriteriaOperator Binary(BinaryExpression expression, BinaryOperatorType type) {
			if(type == BinaryOperatorType.Plus && (ReferenceEquals(expression.Left.Type, typeof(string)) || ReferenceEquals(expression.Right.Type, typeof(string))))
				return FnConcat(expression, expression.Left, expression.Right);
			CriteriaOperator right = ParseExpression(expression.Right);
			bool rightIsExpressionAccess = HasExpressionOrMemberInitOrQuerySet(right);
			CriteriaOperator detectCompareResult;
			if(BinaryDetectCompare(expression, expression.Left, expression.Right, right, type, rightIsExpressionAccess, out detectCompareResult))
				return detectCompareResult;
			CriteriaOperator left = ParseExpression(expression.Left);
			bool leftIsOperandValue = left is OperandValue;
			bool rightIsOperandValue = right is OperandValue;
			if(leftIsOperandValue && rightIsOperandValue)
				return new ConstantCompiler(Dictionary, expression);
			if(rightIsExpressionAccess || HasExpressionOrMemberInitOrQuerySet(left))
				return new ExpressionAccessOperator(expression, left, right);
			if(expression.Left.Type == typeof(TimeSpan) && expression.Right.Type == typeof(TimeSpan)) {
				bool leftIsTimeOfDay = GetTimeOfDayValueFinder.TryToFind(left);
				bool rightIsTimeOfDay = GetTimeOfDayValueFinder.TryToFind(right);
				if(leftIsOperandValue && rightIsTimeOfDay) {
					OperandValue ov = (OperandValue)left;
					if(ov.Value != null) {
						left = new OperandValue(((TimeSpan)ov.Value).Ticks);
					}
				}
				if(rightIsOperandValue && leftIsTimeOfDay) {
					OperandValue ov = (OperandValue)right;
					if(ov.Value != null) {
						right = new OperandValue(((TimeSpan)ov.Value).Ticks);
					}
				}
				if(!leftIsOperandValue && rightIsTimeOfDay && !leftIsTimeOfDay) {
					right = new BinaryOperator(right, new ConstantValue(10000000.0), BinaryOperatorType.Divide);
					right = new FunctionOperator(FunctionOperatorType.ToDouble, right);
				}
				if(!rightIsOperandValue && leftIsTimeOfDay && !rightIsTimeOfDay) {
					CriteriaOperator tmp = new BinaryOperator(left, new ConstantValue(10000000.0), BinaryOperatorType.Divide);
					tmp = new FunctionOperator(FunctionOperatorType.ToDouble, tmp);
					left = right;
					right = tmp;
				}
			}
			else if(expression.Left.Type == typeof(DateTime) && expression.Right.Type == typeof(TimeSpan)) {
				if(type == BinaryOperatorType.Plus || type == BinaryOperatorType.Minus) {
					if(type == BinaryOperatorType.Minus) {
						right = new UnaryOperator(UnaryOperatorType.Minus, right);
					}
					if(GetTimeOfDayValueFinder.TryToFind(right)) {
						return new FunctionOperator(FunctionOperatorType.AddTicks, left, right);
					}
					else {
						return new FunctionOperator(FunctionOperatorType.AddTimeSpan, left, right);
					}
				}
			}
			return new BinaryOperator(left, right, type);
		}
		CriteriaOperator Conditional(ConditionalExpression expression) {
			CriteriaOperator test = ParseExpression(expression.Test);
			CriteriaOperator ifTrue = ParseExpression(expression.IfTrue);
			CriteriaOperator ifFalse = ParseExpression(expression.IfFalse);
			if(HasExpressionOrMemberInitOrQuerySet(test, ifTrue, ifFalse))
				return new ExpressionAccessOperator(expression, test, ifTrue, ifFalse);
			FunctionOperator nestedIif = ifFalse as FunctionOperator;
			if(!IsNull(nestedIif) && nestedIif.OperatorType == FunctionOperatorType.Iif && nestedIif.Operands.Count >= 3) {
				var operands = new List<CriteriaOperator>(2 + nestedIif.Operands.Count);
				operands.Add(test);
				operands.Add(ifTrue);
				operands.AddRange(nestedIif.Operands);
				return new FunctionOperator(FunctionOperatorType.Iif, operands);
			}
			return new FunctionOperator(FunctionOperatorType.Iif, test, ifTrue, ifFalse);
		}
		CriteriaOperator Coalesce(BinaryExpression expression) {
			CriteriaOperator left = ParseExpression(expression.Left);
			CriteriaOperator right = ParseExpression(expression.Right);
			if(HasExpressionOrMemberInitOrQuerySet(left, right))
				return new ExpressionAccessOperator(expression, left, right);
			return new FunctionOperator(FunctionOperatorType.IsNull, left, right);
		}
		CriteriaOperator Constant(ConstantExpression expression) {
			XPQueryBase query = expression.Value as XPQueryBase;
			if(query != null) {
				IQueryable queryable = query as IQueryable;
				if(queryable != null && queryable.ElementType.IsGenericType && queryable.ElementType.GetGenericTypeDefinition() == typeof(IGrouping<,>))
					return new GroupSet(query.Projection, query.GroupKey);
				else
					return new QuerySet(query.Projection);
			}
			return expression.Value == null || IsPrimitive(expression.Value.GetType()) ? (CriteriaOperator)new ConstantValue(expression.Value) : new ConstantCompiler(Dictionary, expression);
		}
		public static bool IsPrimitive(Type type) {
			return type.IsPrimitive || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) || type == typeof(DateOnly) || type == typeof(TimeOnly) || type == typeof(Guid) || type == typeof(TimeSpan) || (type.IsArray && IsPrimitive(type.GetElementType()));
		}
		static HashSet<Type> containsDeclearingTypes = null;
		static HashSet<Type> ContainsDeclearingTypes {
			get {
				if(containsDeclearingTypes == null) {
					HashSet<Type> newContainsDeclearingTypes = new HashSet<Type> {
						typeof(List<>),
						typeof(ICollection<>),
						typeof(Collection<>),
						typeof(ReadOnlyCollection<>)
					};
					containsDeclearingTypes = newContainsDeclearingTypes;
				}
				return containsDeclearingTypes;
			}
		}
		CriteriaOperator Call(MethodCallExpression call) {
			CriteriaOperator callConvertCriteria;
			if(call.Method.DeclaringType == typeof(Convert) && TryCallConvertClass(call, out callConvertCriteria)) {
				return callConvertCriteria;
			}
			else
				if(call.Method.Name == "ToString") {
				return FnToStr(call, call.Object);
			}
			else
					if(call.Method.Name == "CompareTo" && call.Arguments.Count == 1) {
				CriteriaOperator obj = ParseExpression(call.Object);
				CriteriaOperator arg = ParseExpression(call.Arguments[0]);
				if(HasExpressionOrMemberInitOrQuerySet(obj, arg))
					return new ExpressionAccessOperator(call, obj, arg);
				return FnCompareTo(obj, arg);
			}
			else
						if(call.Method.DeclaringType == typeof(string)) {
				return CallString(call);
			}
			else
							if(call.Method.DeclaringType == typeof(object) && call.Method.Name == "Equals") {
				if(call.Object == null) {
					return EqualsCore(call, call.Arguments[0], call.Arguments[1], false);
				}
				else {
					return EqualsCore(call, call.Object, call.Arguments[0], false);
				}
			}
			else
								if(call.Method.DeclaringType == typeof(Math)) {
				return CallMath(call);
			}
			else
									if(call.Method.DeclaringType == typeof(DateTime)) {
				return CallDateTime(call);
			}
			else
									if(call.Method.DeclaringType == typeof(DateOnly)) {
				return CallDateTime(call);
			}
			else
									if(call.Method.DeclaringType == typeof(TimeOnly)) {
				return CallDateTime(call);
			}
			else
										if(call.Method.DeclaringType.FullName == "System.Data.Linq.SqlClient.SqlMethods") {
				return CallSqlMethods(call);
			}
			else
											if(call.Method.DeclaringType == typeof(Queryable) || call.Method.DeclaringType == typeof(Enumerable)) {
				return CallQueryable(call);
			}
			else
												if(CheckMethodIsContains(call.Method, call.Arguments)) {
				return ListContains(call);
			}
			else
													if(call.Method.DeclaringType == typeof(XPQueryExtensions) && call.Method.Name.StartsWith("Query")) {
				CriteriaOperator result;
				if(TryGetNewXPQueryCriteria(call, out result)) {
					return result;
				}
			}
			return CallDefault(call);
		}
		CriteriaOperator FnToStr(MethodCallExpression toStringCall, Expression argument) {
			if(argument.Type == typeof(object) && argument.NodeType == ExpressionType.Convert || argument.NodeType == ExpressionType.ConvertChecked) {
				argument = ((UnaryExpression)argument).Operand;
			}
			var obj = ParseExpression(argument);
			if(HasExpressionOrMemberInitOrQuerySet(obj)) {
				return toStringCall == null ? obj : new ExpressionAccessOperator(toStringCall, obj);
			}
			var argumentType = Nullable.GetUnderlyingType(argument.Type) ?? argument.Type;
			if(argumentType == typeof(char)) {
				return obj;
			}
			if(argumentType.IsEnum) {
				List<CriteriaOperator> operands = new List<CriteriaOperator>();
				foreach(object enumValue in Enum.GetValues(argumentType)) {
					operands.Add(obj == new ConstantValue(enumValue));
					operands.Add(new ConstantValue(enumValue.ToString()));
				}
				operands.Add(obj.IsNull());
				operands.Add(new ConstantValue(""));
				operands.Add(new FunctionOperator(FunctionOperatorType.ToStr, obj));
				return new FunctionOperator(FunctionOperatorType.Iif, operands);
			}
			else if(argumentType == typeof(bool)) {
				return new FunctionOperator(FunctionOperatorType.Iif, obj == new ConstantValue(true), new ConstantValue("True"), obj == new ConstantValue(false), new ConstantValue("False"), new ConstantValue(""));
			}
			else if(argumentType == typeof(Guid)) {
				return new FunctionOperator(FunctionOperatorType.Lower, new FunctionOperator(FunctionOperatorType.ToStr, obj));
			}
			return new FunctionOperator(FunctionOperatorType.ToStr, obj);
		}
		bool CheckMethodIsContains(MethodInfo method, ReadOnlyCollection<Expression> arguments) {
			if(!(method.Name == "Contains" && method.DeclaringType.IsGenericType
				&& arguments.Count == 1 && method.DeclaringType.GetGenericArguments()[0] == method.GetParameters()[0].ParameterType
				&& method.ReturnType == typeof(bool)))
				return false;
			Type declType = method.DeclaringType;
			do {
				if(declType.GetGenericTypeDefinition() == typeof(ICollection<>)
					|| (!declType.IsInterface && declType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>))))
					return true;
				declType = declType.GetBaseType();
			} while(declType != null && declType.IsGenericType);
			return false;
		}
		private CriteriaOperator CallSqlMethods(MethodCallExpression call) {
			CriteriaOperator arg0 = null;
			CriteriaOperator arg1 = null;
			if(call.Arguments.Count == 2) {
				arg0 = ParseExpression(call.Arguments[0]);
				arg1 = ParseExpression(call.Arguments[1]);
				if(HasExpressionOrMemberInitOrQuerySet(arg0, arg1)) return new ExpressionAccessOperator(call, null, arg0, arg1);
			}
			switch(call.Method.Name) {
				case "DateDiffDay":
					return new FunctionOperator(FunctionOperatorType.DateDiffDay, arg0, arg1);
				case "DateDiffHour":
					return new FunctionOperator(FunctionOperatorType.DateDiffHour, arg0, arg1);
				case "DateDiffMillisecond":
					return new FunctionOperator(FunctionOperatorType.DateDiffMilliSecond, arg0, arg1);
				case "DateDiffMinute":
					return new FunctionOperator(FunctionOperatorType.DateDiffMinute, arg0, arg1);
				case "DateDiffMonth":
					return new FunctionOperator(FunctionOperatorType.DateDiffMonth, arg0, arg1);
				case "DateDiffSecond":
					return new FunctionOperator(FunctionOperatorType.DateDiffSecond, arg0, arg1);
				case "DateDiffYear":
					return new FunctionOperator(FunctionOperatorType.DateDiffYear, arg0, arg1);
				case "Like":
					return LikeCustomFunction.Create(arg0, arg1);
				default: return CallDefault(call);
			}
		}
		private CriteriaOperator CallString(MethodCallExpression call) {
			switch(call.Method.Name) {
				case "Concat":
					return FnConcatList(call, call.Arguments);
				case "IsNullOrEmpty":
					return FunctionUniversal(call, FunctionOperatorType.IsNullOrEmpty, false, 1, 1);
				case "ToLower":
					return FunctionUniversal(call, FunctionOperatorType.Lower, true, 0, 0);
				case "ToUpper":
					return FunctionUniversal(call, FunctionOperatorType.Upper, true, 0, 0);
				case "Trim":
					return FunctionUniversal(call, FunctionOperatorType.Trim, true, 0, 0);
				case "StartsWith":
					return FunctionUniversal(call, FunctionOperatorType.StartsWith, true, 1, 1);
				case "Equals":
					if(call.Arguments.Count == 1) {
						return EqualsCore(call, call.Object, call.Arguments[0], false);
					}
					else
						return CallDefault(call);
				case "EndsWith":
					return FunctionUniversal(call, FunctionOperatorType.EndsWith, true, 1, 1);
				case "Substring":
					if(call.Arguments.Count == 2) {
						CriteriaOperator obj = ParseExpression(call.Object);
						CriteriaOperator arg0 = ParseExpression(call.Arguments[0]);
						CriteriaOperator arg1 = ParseExpression(call.Arguments[1]);
						if(HasExpressionOrMemberInitOrQuerySet(obj, arg0, arg1)) return new ExpressionAccessOperator(call, obj, arg0, arg1);
						return new FunctionOperator(FunctionOperatorType.Substring, obj, arg0, arg1);
					}
					else {
						CriteriaOperator obj = ParseExpression(call.Object);
						CriteriaOperator arg0 = ParseExpression(call.Arguments[0]);
						if(HasExpressionOrMemberInitOrQuerySet(obj, arg0)) return new ExpressionAccessOperator(call, obj, arg0);
						return new FunctionOperator(FunctionOperatorType.Substring, obj, arg0);
					}
				case "PadLeft":
					return FunctionUniversal(call, FunctionOperatorType.PadLeft, true, 1, 2);
				case "PadRight":
					return FunctionUniversal(call, FunctionOperatorType.PadRight, true, 1, 2);
				case "IndexOf":
					return FnIndexOf(call);
				case "Insert":
					return FunctionUniversal(call, FunctionOperatorType.Insert, true, 2, 2);
				case "Remove":
					return FunctionUniversal(call, FunctionOperatorType.Remove, true, 1, 2);
				case "Replace":
					return FunctionUniversal(call, FunctionOperatorType.Replace, true, 2, 2);
				case "Contains":
					return FunctionUniversal(call, FunctionOperatorType.Contains, true, 1, 1);
				default: return CallDefault(call);
			}
		}
		private CriteriaOperator CallMath(MethodCallExpression call) {
			switch(call.Method.Name) {
				case "Abs":
					return FunctionUniversal(call, FunctionOperatorType.Abs, false, 1, 1);
				case "Atan":
					return FunctionUniversal(call, FunctionOperatorType.Atn, false, 1, 1);
				case "Atan2":
					return FunctionUniversal(call, FunctionOperatorType.Atn2, false, 2, 2);
				case "BigMul":
					return FunctionUniversal(call, FunctionOperatorType.BigMul, false, 2, 2);
				case "Ceiling":
					return FunctionUniversal(call, FunctionOperatorType.Ceiling, false, 1, 1);
				case "Cos":
					return FunctionUniversal(call, FunctionOperatorType.Cos, false, 1, 1);
				case "Cosh":
					return FunctionUniversal(call, FunctionOperatorType.Cosh, false, 1, 1);
				case "Exp":
					return FunctionUniversal(call, FunctionOperatorType.Exp, false, 1, 1);
				case "Floor":
					return FunctionUniversal(call, FunctionOperatorType.Floor, false, 1, 1);
				case "Log10":
					return FunctionUniversal(call, FunctionOperatorType.Log10, false, 1, 1);
				case "Log":
					return FunctionUniversal(call, FunctionOperatorType.Log, false, 1, 2);
				case "Pow":
					return FunctionUniversal(call, FunctionOperatorType.Power, false, 2, 2);
				case "Round":
					return FunctionUniversal(call, FunctionOperatorType.Round, false, 1, 2);
				case "Sign":
					return FunctionUniversal(call, FunctionOperatorType.Sign, false, 1, 1);
				case "Sin":
					return FunctionUniversal(call, FunctionOperatorType.Sin, false, 1, 1);
				case "Sinh":
					return FunctionUniversal(call, FunctionOperatorType.Sinh, false, 1, 1);
				case "Tan":
					return FunctionUniversal(call, FunctionOperatorType.Tan, false, 1, 1);
				case "Tanh":
					return FunctionUniversal(call, FunctionOperatorType.Tanh, false, 1, 1);
				case "Sqrt":
					return FunctionUniversal(call, FunctionOperatorType.Sqr, false, 1, 1);
				case "Acos":
					return FunctionUniversal(call, FunctionOperatorType.Acos, false, 1, 1);
				case "Asin":
					return FunctionUniversal(call, FunctionOperatorType.Asin, false, 1, 1);
				case "Max":
					return FunctionUniversal(call, FunctionOperatorType.Max, false, 2, 2);
				case "Min":
					return FunctionUniversal(call, FunctionOperatorType.Min, false, 2, 2);
				default: return CallDefault(call);
			}
		}
		private CriteriaOperator CallDateTime(MethodCallExpression call) {
			switch(call.Method.Name) {
				case "Add":
					return CallDateAddTimeSpan(call);
				case "AddMilliseconds":
					return FunctionUniversal(call, FunctionOperatorType.AddMilliSeconds, true, 1, 1);
				case "AddSeconds":
					return FunctionUniversal(call, FunctionOperatorType.AddSeconds, true, 1, 1);
				case "AddMinutes":
					return FunctionUniversal(call, FunctionOperatorType.AddMinutes, true, 1, 1);
				case "AddHours":
					return FunctionUniversal(call, FunctionOperatorType.AddHours, true, 1, 1);
				case "AddDays":
					return FunctionUniversal(call, FunctionOperatorType.AddDays, true, 1, 1);
				case "AddMonths":
					return FunctionUniversal(call, FunctionOperatorType.AddMonths, true, 1, 1);
				case "AddYears":
					return FunctionUniversal(call, FunctionOperatorType.AddYears, true, 1, 1);
				case "AddTicks":
					return FunctionUniversal(call, FunctionOperatorType.AddTicks, true, 1, 1);
				default:
					return CallDefault(call);
			}
		}
		CriteriaOperator CallDateAddTimeSpan(MethodCallExpression call) {
			CriteriaOperator operand = ParseExpression(call.Arguments[0]);
			FunctionOperatorType functionType;
			if(GetTimeOfDayValueFinder.TryToFind(operand)) {
				functionType = FunctionOperatorType.AddTicks;
			}
			else {
				functionType = FunctionOperatorType.AddTimeSpan;
			}
			return FunctionUniversal(call, functionType, true, 1, 1);
		}
		CriteriaOperator DefaultIfEmpty(MethodCallExpression call) {
			return ParseExpression(call.Arguments[0]);
		}
		private CriteriaOperator CallQueryable(MethodCallExpression call) {
			switch(call.Method.Name) {
				case "All":
					return All(call);
				case "Any":
					return Any(call);
				case "Average":
					return Average(call);
				case "Contains":
					return Contains(call);
				case "Count":
				case "LongCount":
					return Count(call);
				case "Max":
					return Max(call);
				case "Min":
					return Min(call);
				case "Select":
					return Select(call);
				case "Sum":
					return Sum(call);
				case "Where":
					return Where(call);
				case "OfType":
					return OfType(call);
				case "DefaultIfEmpty":
					return DefaultIfEmpty(call);
				default:
					return CallDefault(call);
			}
		}
		public CriteriaOperator FnCompareTo(CriteriaOperator obj, CriteriaOperator argument) {
			return new FunctionOperator(FunctionOperatorType.Iif, new BinaryOperator(obj, argument, BinaryOperatorType.Equal), new ConstantValue(0), new BinaryOperator(obj, argument, BinaryOperatorType.Greater), new ConstantValue(1), new ConstantValue(-1));
		}
		CriteriaOperator FnConcat(BinaryExpression binary, Expression left, Expression right) {
			return FnConcatList(binary, new Expression[] { left, right });
		}
		CriteriaOperator FnConcatList(Expression expression, IList<Expression> arguments) {
			bool createExpressionAccess = false;
			if(arguments.Count == 1 && arguments[0].Type.IsArray) {
				var arrayExpr = arguments[0] as NewArrayExpression;
				if(arrayExpr != null) {
					arguments = arrayExpr.Expressions;
				}
			}
			CriteriaOperator[] resultOperands = new CriteriaOperator[arguments.Count];
			for(int i = 0; i < arguments.Count; i++) {
				var argument = arguments[i];
				CriteriaOperator operand = argument.Type != typeof(string) ? FnToStr(null, argument) : ParseExpression(argument);
				if(HasExpressionOrMemberInitOrQuerySet(operand))
					createExpressionAccess = true;
				resultOperands[i] = operand;
			}
			if(createExpressionAccess)
				return new ExpressionAccessOperator(expression, true, resultOperands);
			return new FunctionOperator(FunctionOperatorType.Concat, resultOperands);
		}
		CriteriaOperator FnIndexOf(MethodCallExpression call) {
			CriteriaOperator obj = ParseExpression(call.Object);
			CriteriaOperator arg0 = ParseExpression(call.Arguments[0]);
			if(call.Arguments.Count == 3) {
				CriteriaOperator arg1 = ParseExpression(call.Arguments[1]);
				CriteriaOperator arg2 = ParseExpression(call.Arguments[2]);
				if(HasExpressionOrMemberInitOrQuerySet(obj, arg0, arg1, arg2)) return new ExpressionAccessOperator(call, obj, arg0, arg1, arg2);
				return new FunctionOperator(FunctionOperatorType.CharIndex, arg0, obj, arg1, arg2);
			}
			else if(call.Arguments.Count == 2) {
				CriteriaOperator arg1 = ParseExpression(call.Arguments[1]);
				if(HasExpressionOrMemberInitOrQuerySet(obj, arg0, arg1)) return new ExpressionAccessOperator(call, obj, arg0, arg1);
				return new FunctionOperator(FunctionOperatorType.CharIndex, arg0, obj, arg1);
			}
			if(HasExpressionOrMemberInitOrQuerySet(obj, arg0)) return new ExpressionAccessOperator(call, obj, arg0);
			return new FunctionOperator(FunctionOperatorType.CharIndex, arg0, obj);
		}
		CriteriaOperator FunctionUniversal(MethodCallExpression call, FunctionOperatorType fType, bool obj, int minCount, int maxCount) {
			bool createExpressionAccess = false;
			bool insertFirstNull = true;
			CriteriaOperator objOperand = null;
			if(call.Object != null) {
				objOperand = ParseExpression(call.Object);
				insertFirstNull = false;
				if(HasExpressionOrMemberInitOrQuerySet(objOperand))
					createExpressionAccess = true;
			}
			List<CriteriaOperator> operands = new List<CriteriaOperator>(call.Arguments.Count);
			for(int i = 0; i < call.Arguments.Count; i++) {
				CriteriaOperator operand = ParseExpression(call.Arguments[i]);
				if(HasExpressionOrMemberInitOrQuerySet(operand))
					createExpressionAccess = true;
				operands.Add(operand);
			}
			if(operands.Count < minCount)
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_X0WithSoManyParametersIsNotSupported, call.Method.Name));
			if(operands.Count > maxCount) {
				operands.RemoveRange(maxCount, operands.Count - maxCount);
			}
			if(obj) {
				operands.Insert(0, objOperand);
			}
			if(createExpressionAccess) {
				return new ExpressionAccessOperator(call, insertFirstNull, operands.ToArray());
			}
			return new FunctionOperator(fType, operands);
		}
		CriteriaOperator Select(MethodCallExpression call) {
			CriteriaOperator coParent = ParseExpression(call.Arguments[0]);
			QuerySet parent = coParent as QuerySet;
			if(IsNull(parent)) {
				if(HasExpressionOrMemberInitOrQuerySet(coParent) && call.Arguments.Count == 2) {
					return new ExpressionAccessOperator(call, call.Object == null, coParent, new ExpressionAccessOperator(call.Arguments[1]));
				}
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, call.Arguments[0], "Select"));
			}
			if(call.Arguments[0].NodeType != ExpressionType.Constant && call.Arguments.Count == 2 && HasExpressionOrMemberInitOrQuerySet(coParent)) {
				return new ExpressionAccessOperator(call, call.Object == null, ParseWithExpressionOperatorForceForQuerySet(call.Arguments[0]), new ExpressionAccessOperator(call.Arguments[1]));
			}
			FreeQuerySet freeParent = parent as FreeQuerySet;
			QuerySet newSub;
			CriteriaOperator op;
			if(IsNull(freeParent)) {
				newSub = new QuerySet(parent.Property, parent.Condition);
				if(IsNull(parent.Property)) {
					op = ParseExpression(parent.Property, call.Arguments[1], parent);
				}
				else {
					op = ParseExpression(parent.Property, call.Arguments[1], QuerySet.Empty);
				}
			}
			else {
				newSub = parent;
				op = ParseExpression(freeParent.JoinType, call.Arguments[1], new QuerySet(freeParent.Projection));
			}
			MemberInitOperator init = op as MemberInitOperator;
			QuerySet set = op as QuerySet;
			FreeQuerySet freeSet = op as FreeQuerySet;
			if(!IsNull(freeSet)) {
				return freeSet;
			}
			if(!IsNull(set)) {
				init = set.Projection;
			}
			if(IsNull(init) && !IsNull(op)) {
				CriteriaOperator opForProjection = null;
				if(IsNull(set)) {
					opForProjection = op;
				}
				else if(!IsNull(set.Property)) {
					opForProjection = set;
				}
				if(!IsNull(opForProjection)) {
					XPMemberAssignmentCollection members = new XPMemberAssignmentCollection();
					members.Add(new XPMemberAssignment(opForProjection));
					if(call.Object == null) init = new MemberInitOperator((Type)null, members, false);
					else
						init = new MemberInitOperator(call.Object.Type, members, false);
				}
			}
			newSub.Projection = init;
			return newSub;
		}
		CriteriaOperator OfType(MethodCallExpression call) {
			var result = ParseExpression(call.Arguments[0]);
			if(HasExpressionAccess(result)) {
				return new ExpressionAccessOperator(call, call.Object == null, result);
			}
			QuerySet querySet = result as QuerySet;
			if(IsNull(querySet)) {
				if(!Parser.TryConvertToQuerySet(result, classInfo, out querySet)) {
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, call.Arguments[0], "OfType"));
				}
			}
			Expression sourceArgument = call.Arguments[0];
			while(!sourceArgument.Type.IsGenericType) {
				if(sourceArgument.NodeType != ExpressionType.Convert) {
					break;
				}
				sourceArgument = ((UnaryExpression)sourceArgument).Operand;
			}
			Type[] sourceGenericArguments = sourceArgument.Type.GetGenericArguments();
			if(sourceGenericArguments.Length == 0) {
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, call.Arguments[0], "OfType"));
			}
			Type sourceType = sourceGenericArguments[0];
			XPClassInfo sourcClassInfo = Dictionary.QueryClassInfo(sourceType);
			Type destType = call.Method.GetGenericArguments()[0];
			XPClassInfo destClassInfo = Dictionary.QueryClassInfo(destType);
			if(sourcClassInfo == null || destClassInfo == null || !destClassInfo.IsAssignableTo(sourcClassInfo)) {
				return new ExpressionAccessOperator(call, call.Object == null, ParseWithExpressionOperatorForceForQuerySet(call.Arguments[0]));
			}
			FreeQuerySet freeQuerySet = querySet as FreeQuerySet;
			if(IsNull(freeQuerySet)) {
				if(querySet.IsEmpty) {
					return new FreeQuerySet(destClassInfo.ClassType, querySet.Condition) {
						Projection = querySet.Projection
					};
				}
				else {
					querySet.Condition = GroupOperator.And(querySet.Condition, new FunctionOperator(IsInstanceOfTypeFunction.FunctionName, new OperandProperty("This"), new ConstantValue(destClassInfo.FullName)));
					return querySet;
				}
			}
			else {
				freeQuerySet.JoinType = destClassInfo.ClassType;
			}
			return freeQuerySet;
		}
		CriteriaOperator Where(MethodCallExpression call) {
			CriteriaOperator co = ParseExpression(call.Arguments[0]);
			QuerySet parent = co as QuerySet;
			if(IsNull(parent)) {
				OperandProperty op = co as OperandProperty;
				if(IsNull(op)) {
					if(!TryConvertToQuerySet(co, classInfo, out parent)) {
						throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, call.Arguments[0], "the Where clause"));
					}
				}
				else {
					parent = new QuerySet(op, null); 
				}
			}
			FreeQuerySet freeParent = parent as FreeQuerySet;
			if(IsNull(freeParent)) {
				CriteriaOperator criteria = ParseExpression(parent.Property, call.Arguments[1], parent is GroupSet ? new GroupSet(parent.Projection, ((GroupSet)parent).Key) : new QuerySet(parent.Projection));
				if(HasExpressionAccess(criteria))
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, GetCauseStringOfExpressionAccess(criteria), "the Where clause"));
				if(IsNull(parent.Condition)) {
					return new QuerySet(parent.Property, criteria);
				}
				return new QuerySet(parent.Property, GroupOperator.And(parent.Condition, criteria));
			}
			else {
				CriteriaOperator additionalCriteria = ParseExpression(freeParent.JoinType, call.Arguments[1], new QuerySet(freeParent.Projection));
				if(HasExpressionAccess(additionalCriteria))
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, GetCauseStringOfExpressionAccess(additionalCriteria), "the Where clause"));
				if(IsNull(additionalCriteria)) {
					return new FreeQuerySet(freeParent.JoinType, freeParent.Condition);
				}
				return new FreeQuerySet(freeParent.JoinType, GroupOperator.And(freeParent.Condition, additionalCriteria));
			}
		}
		CriteriaOperator CallDefault(MethodCallExpression call) {
			CriteriaOperator op = CallCustomCriteria(call);
			if(!IsNull(op)) return op;
			op = CallCustomFunction(call);
			if(!IsNull(op)) return op;
			op = CallCustomAggregate(call);
			if(!IsNull(op)) return op;
			bool createExpressionAccessOperator = false;
			List<CriteriaOperator> arguments = new List<CriteriaOperator>();
			for(int i = 0; i < call.Arguments.Count; i++) {
				op = ParseExpression(call.Arguments[i]);
				arguments.Add(op);
				if(!(op is OperandValue)) {
					createExpressionAccessOperator = true;
				}
			}
			op = call.Object != null ? ParseExpression(call.Object) : null;
			arguments.Insert(0, op);
			if(!createExpressionAccessOperator) {
				if(op is OperandValue || IsNull(op))
					return new ConstantCompiler(Dictionary, call);
			}
			return new ExpressionAccessOperator(call, arguments.ToArray());
		}
		CriteriaOperator CallCustomFunction(MethodCallExpression call) {
			FunctionOperatorType customFunctionType;
			ICustomFunctionOperator customFunction = Dictionary.CustomFunctionOperators.GetCustomFunction(call.Method.Name);
			if(!IsValidCustomFunctionQueryable(call, customFunction, out customFunctionType)) {
				if(CriteriaOperator.CustomFunctionCount > 0) {
					customFunction = CriteriaOperator.GetCustomFunction(call.Method.Name);
					if(!IsValidCustomFunctionQueryable(call, customFunction, out customFunctionType)) {
						return null;
					}
				}
				else {
					return null;
				}
			}
			List<CriteriaOperator> operands = new List<CriteriaOperator>();
			operands.Add(new OperandValue(customFunction.Name));
			bool insertFirstNull = true;
			if(call.Object != null) {
				operands.Add(ParseExpression(call.Object));
				insertFirstNull = false;
			}
			for(int i = 0; i < call.Arguments.Count; i++) {
				operands.Add(ParseExpression(call.Arguments[i]));
			}
			CriteriaOperator[] operandsArray = operands.ToArray();
			if(HasExpressionOrMemberInitOrQuerySet(operandsArray))
				return new ExpressionAccessOperator(call, insertFirstNull, operandsArray);
			return new FunctionOperator(customFunctionType, operandsArray);
		}
		CriteriaOperator CallCustomAggregate(MethodCallExpression call) {
			ICustomAggregate customAggregate = Dictionary.CustomAggregates.GetCustomAggregate(call.Method.Name);
			if(!IsValidCustomAggregateQueryable(call, customAggregate)) {
				if(CriteriaOperator.CustomAggregateCount > 0) {
					customAggregate = CriteriaOperator.GetCustomAggregate(call.Method.Name);
					if(!IsValidCustomAggregateQueryable(call, customAggregate)) {
						return null;
					}
				}
				else {
					return null;
				}
			}
			return AggregateCall(call, customAggregate.Name);
		}
		static bool IsValidCustomFunctionQueryable(MethodCallExpression call, ICustomFunctionOperator customFunction, out FunctionOperatorType customFunctionType) {
			customFunctionType = FunctionOperatorType.Custom;
			if(customFunction == null) return false;
			if((customFunction is ICustomFunctionOperatorQueryable) && ReferenceEquals(((ICustomFunctionOperatorQueryable)customFunction).GetMethodInfo(), call.Method)) {
				if(customFunction is ICustomNonDeterministicFunctionOperatorQueryable)
					customFunctionType = FunctionOperatorType.CustomNonDeterministic;
				return true;
			}
			return false;
		}
		static bool IsValidCustomAggregateQueryable(MethodCallExpression call, ICustomAggregate customAggregate) {
			var customAggregateQueryable = customAggregate as ICustomAggregateQueryable;
			if(customAggregateQueryable == null) {
				return false;
			}
			MethodInfo methodInfo = customAggregateQueryable.GetMethodInfo();
			if(methodInfo.Name == call.Method.Name && methodInfo.DeclaringType == call.Method.DeclaringType) {
				return true;
			}
			return false;
		}
		CriteriaOperator CallCustomCriteria(MethodCallExpression call) {
			ICustomCriteriaOperatorQueryable customCriteria = null;
			if(customCriteriaCollection != null) {
				customCriteria = customCriteriaCollection.GetItem(call.Method);
			}
			if(customCriteria == null) {
				if(CustomCriteriaManager.RegisteredCriterionCount > 0) {
					customCriteria = CustomCriteriaManager.GetCriteria(call.Method);
					if(customCriteria == null) {
						return null;
					}
				}
				else {
					return null;
				}
			}
			List<CriteriaOperator> operands = new List<CriteriaOperator>();
			bool insertFirstNull = true;
			if(call.Object != null) {
				operands.Add(ParseExpression(call.Object));
				insertFirstNull = false;
			}
			for(int i = 0; i < call.Arguments.Count; i++) {
				operands.Add(ParseExpression(call.Arguments[i]));
			}
			CriteriaOperator[] operandsArray = operands.ToArray();
			if(HasExpressionOrMemberInitOrQuerySet(operandsArray))
				return new ExpressionAccessOperator(call, insertFirstNull, operandsArray);
			return customCriteria.GetCriteria(operandsArray);
		}
		CriteriaOperator LogicalBinary(BinaryExpression expression, BinaryOperatorType type) {
			if(IsVBStringCompare(expression))
				return CompareVB((MethodCallExpression)expression.Left, type);
			return Binary(expression, type);
		}
		public static bool IsImplementsInterface(Type classType, Type interfaceType) {
			return interfaceType.IsInterface && classType.GetInterfaces().Any(i => i == interfaceType);
		}
		CriteriaOperator Convert(UnaryExpression expression) {
			return ConvertCore(expression, expression.Operand, expression.Type, classInfo);
		}
		CriteriaOperator ConvertCore(Expression expression, Expression expressionOperand, Type resultType, XPClassInfo currentClassInfo) {
			return ConvertCore(expression, ParseExpression(expressionOperand), expressionOperand.Type, resultType, currentClassInfo);
		}
		CriteriaOperator ConvertCore(Expression expression, CriteriaOperator operandCriteriaOperantor, Type operandType, Type resultType, XPClassInfo currentClassInfo) {
			if(HasExpressionAccess(operandCriteriaOperantor))
				return new ExpressionAccessOperator(expression, operandCriteriaOperantor);
			if(operandType == typeof(char) && resultType == typeof(int)) {
				return new FunctionOperator(FunctionOperatorType.Ascii, operandCriteriaOperantor);
			}
			Type t = Nullable.GetUnderlyingType(resultType) ?? resultType;
			Type tObject = Nullable.GetUnderlyingType(operandType) ?? operandType;
			if(t == tObject) {
				return operandCriteriaOperantor;
			}
			if(IsImplementsInterface(t, tObject) || IsImplementsInterface(tObject, t)) {
				return operandCriteriaOperantor;
			}
			if(forceExpressionOperatorForConversion > 0) {
				return new ExpressionAccessOperator(expression, operandCriteriaOperantor);
			}
			if((tObject.IsEnum && t == typeof(int)) || (operandCriteriaOperantor is ConstantValue && ((ConstantValue)operandCriteriaOperantor).Value == null)) {
				return operandCriteriaOperantor;
			}
			switch(Type.GetTypeCode(t)) {
				case TypeCode.Int32:
				case TypeCode.UInt32:
					return new FunctionOperator(FunctionOperatorType.ToInt, operandCriteriaOperantor);
				case TypeCode.Int64:
				case TypeCode.UInt64:
					return new FunctionOperator(FunctionOperatorType.ToLong, operandCriteriaOperantor);
				case TypeCode.Single:
					return new FunctionOperator(FunctionOperatorType.ToFloat, operandCriteriaOperantor);
				case TypeCode.Double:
					return new FunctionOperator(FunctionOperatorType.ToDouble, operandCriteriaOperantor);
				case TypeCode.Decimal:
					return new FunctionOperator(FunctionOperatorType.ToDecimal, operandCriteriaOperantor);
			}
			OperandProperty prop = operandCriteriaOperantor as OperandProperty;
			QuerySet set = operandCriteriaOperantor as QuerySet;
			IAggregateOperand aggregateOperand = operandCriteriaOperantor as IAggregateOperand;
			if(!IsNull(prop)) {
				if(Dictionary.QueryClassInfo(resultType) != null && !resultType.IsAssignableFrom(operandType)) {
					operandCriteriaOperantor = ConvertPropertyCore(prop, resultType);
				}
			}
			else if(!IsNull(set)) {
				if(Dictionary.QueryClassInfo(resultType) != null && !resultType.IsAssignableFrom(operandType)) {
					if(set.IsEmpty) {
						operandCriteriaOperantor = new OperandProperty(string.Concat("<", resultType.Name, ">"));
					}
					else if(IsNull(set.Projection)) {
						var newSet = (QuerySet)((ICloneable)operandCriteriaOperantor).Clone();
						newSet.Projection = MemberInitOperator.CreateUntypedMemberInitOperator(new OperandProperty(string.Concat("<", resultType.Name, ">")));
						return newSet;
					}
				}
			}
			else if(!IsNull(aggregateOperand)) {
				Aggregate aggrType = aggregateOperand.AggregateType;
				if(aggrType != Aggregate.Custom) {
					CriteriaOperator aggregatedExpression = aggregateOperand.AggregatedExpression;
					if(Dictionary.QueryClassInfo(resultType) != null && !resultType.IsAssignableFrom(operandType)) {
						OperandProperty aggregatedProp = aggregatedExpression as OperandProperty;
						if(!IsNull(aggregatedProp)) {
							aggregatedExpression = ConvertPropertyCore(aggregatedProp, resultType);
						}
						else {
							if(aggregateOperand is JoinOperand) {
								string joinType = (string)aggregateOperand.AggregationObject;
								XPClassInfo joinClassInfo = null;
								if(!MemberInfoCollection.TryResolveTypeAlsoByShortName(joinType, currentClassInfo, out joinClassInfo)) {
									throw new DevExpress.Xpo.Exceptions.CannotResolveClassInfoException(string.Empty, joinType);
								}
								CriteriaOperator convertedExpression = ConvertCore(expression, aggregatedExpression, operandType, resultType, joinClassInfo);
								if(HasExpressionAccess(convertedExpression)) {
									return new ExpressionAccessOperator(expression, operandCriteriaOperantor);
								}
								aggregatedExpression = convertedExpression;
							}
							else {
								OperandProperty property = (OperandProperty)aggregateOperand.AggregationObject;
								XPClassInfo aggregateCollectionElementClassInfo;
								if(IsPropertyAssociationList(property.PropertyName, currentClassInfo, out aggregateCollectionElementClassInfo)) {
									CriteriaOperator convertedExpression = ConvertCore(expression, aggregatedExpression, operandType, resultType, aggregateCollectionElementClassInfo);
									if(HasExpressionAccess(convertedExpression)) {
										return new ExpressionAccessOperator(expression, operandCriteriaOperantor);
									}
									aggregatedExpression = convertedExpression;
								}
							}
						}
						operandCriteriaOperantor = aggregateOperand is JoinOperand
							? new JoinOperand((string)aggregateOperand.AggregationObject, aggregateOperand.Condition, aggregateOperand.AggregateType, aggregatedExpression)
							: (CriteriaOperator)new AggregateOperand((OperandProperty)aggregateOperand.AggregationObject, aggregatedExpression, aggregateOperand.AggregateType, aggregateOperand.Condition);
					}
				}
				else {
					ICustomAggregateOperand customAggregateOperand = ((ICustomAggregateOperand)aggregateOperand);
					CriteriaOperatorCollection aggregatedExpressions = customAggregateOperand.CustomAggregateOperands;
					if(Dictionary.QueryClassInfo(resultType) != null && !resultType.IsAssignableFrom(operandType)) {
						CriteriaOperator[] aggregatedProps = new CriteriaOperator[aggregatedExpressions.Count];
						for(int i = 0; i < aggregatedExpressions.Count; i++) {
							OperandProperty aggregatedProp = aggregatedExpressions[i] as OperandProperty;
							if(!IsNull(aggregatedProp)) {
								aggregatedProp = ConvertPropertyCore(aggregatedProp, resultType);
							}
							aggregatedProps[i] = aggregatedProp;
						}
						if(aggregatedProps.Any(p => !IsNull(p))) {
							operandCriteriaOperantor = customAggregateOperand is JoinOperand
								? (CriteriaOperator)new AggregateOperand((OperandProperty)customAggregateOperand.AggregationObject, aggregatedProps, customAggregateOperand.CustomAggregateName, aggregateOperand.Condition)
								: new JoinOperand((string)customAggregateOperand.AggregationObject, customAggregateOperand.Condition, customAggregateOperand.CustomAggregateName, aggregatedProps);
						}
					}
				}
			}
			return operandCriteriaOperantor;
		}
		static OperandProperty ConvertPropertyCore(OperandProperty property, Type resultType) {
			string propertyName = string.Concat(property.PropertyName, ".<", resultType.Name, ">");
			if(propertyName.StartsWith("This.")) propertyName = propertyName.Substring(5);
			else propertyName = propertyName.Replace(".This.", ".");
			return new OperandProperty(propertyName);
		}
		CriteriaOperator NewArrayInit(NewArrayExpression expression) {
			XPMemberAssignmentCollection members = new XPMemberAssignmentCollection();
			for(int i = 0; i < expression.Expressions.Count; i++) {
				Expression e = expression.Expressions[i];
				members.Add(new XPMemberAssignment(ConvertStructAccessIfNeeded(e, ParseExpression(e))));
			}
			return new MemberInitOperator(expression.Type, members, false);
		}
		bool TryGetNewXPQueryCriteria(Expression expression, out CriteriaOperator criteria) {
			if(typeof(XPQueryBase).IsAssignableFrom(expression.Type)) {
				Type xpQueryType = expression.Type;
				if(xpQueryType.IsGenericType) {
					criteria = new FreeQuerySet(xpQueryType.GetGenericArguments()[0], null);
					return true;
				}
				Expression<Func<XPQueryBase>> ex = LambdaExpression.Lambda<Func<XPQueryBase>>(expression);
				Func<XPQueryBase> getXPQuery = ex.Compile();
				XPQueryBase related = getXPQuery();
				criteria = new FreeQuerySet(((IQueryable)related).ElementType, null);
				return true;
			}
			criteria = null;
			return false;
		}
		CriteriaOperator New(NewExpression expression) {
			CriteriaOperator result;
			if(TryGetNewXPQueryCriteria(expression, out result)) {
				return result;
			}
			XPMemberAssignmentCollection members = new XPMemberAssignmentCollection();
			bool isConstant = true;
			for(int i = 0; i < expression.Arguments.Count; i++) {
				Expression e = expression.Arguments[i];
				CriteriaOperator exp = ParseExpression(e);
				if(!(exp is OperandValue))
					isConstant = false;
				exp = ConvertStructAccessIfNeeded(e, exp);
				members.Add(expression.Members == null ? new XPMemberAssignment(exp) : new XPMemberAssignment(expression.Members[i], exp));
			}
			if(isConstant)
				return new ConstantCompiler(Dictionary, expression);
			return new MemberInitOperator(true, members, expression.Constructor);
		}
		bool IsStructType(Type type) {
			return type.IsValueType && !type.IsEnum && !type.IsPrimitive && !type.IsGenericType && type != typeof(decimal) && type != typeof(DateTime) && type != typeof(DateOnly) && type != typeof(TimeOnly) && type != typeof(Guid) && type != typeof(TimeSpan);
		}
		CriteriaOperator ConvertStructAccessIfNeeded(Expression expression, CriteriaOperator criteria) {
			if((expression.NodeType == ExpressionType.Convert || expression.NodeType == ExpressionType.ConvertChecked) && expression.Type == typeof(object) && !HasExpressionAccess(criteria)) {
				UnaryExpression ue = ((UnaryExpression)expression);
				CriteriaOperator result = ConvertStructAccessIfNeeded(ue.Operand, criteria);
				if(ReferenceEquals(result, criteria) && (ue.Operand.NodeType == ExpressionType.Convert || ue.Operand.NodeType == ExpressionType.ConvertChecked)) {
					return ParseWithExpressionOperatorForceForConversion(ue);
				}
				return new ExpressionAccessOperator(expression, result);
			}
			else
				if(!HasExpressionAccess(criteria) && IsStructType(expression.Type)) {
				Type structType = expression.Type;
				XPMemberAssignmentCollection structMembers = new XPMemberAssignmentCollection();
				foreach(MemberInfo mi in structType.GetFields(BindingFlags.Instance | BindingFlags.Public)) {
					structMembers.Add(new XPMemberAssignment(mi, MemberAccess(Expression.MakeMemberAccess(expression, mi), criteria, classInfo)));
				}
				foreach(MemberInfo mi in structType.GetProperties(BindingFlags.Instance | BindingFlags.Public)) {
					if(((PropertyInfo)mi).CanWrite) {
						structMembers.Add(new XPMemberAssignment(mi, MemberAccess(Expression.MakeMemberAccess(expression, mi), criteria, classInfo)));
					}
				}
				criteria = new MemberInitOperator(structType, structMembers, true);
			}
			return criteria;
		}
		CriteriaOperator MemberInit(MemberInitExpression expression) {
			XPMemberAssignmentCollection members = new XPMemberAssignmentCollection();
			foreach(MemberBinding b in expression.Bindings) {
				if(b.BindingType == MemberBindingType.Assignment) {
					MemberAssignment ma = (MemberAssignment)b;
					CriteriaOperator memberOperator = ParseExpression(ma.Expression);
					members.Add(new XPMemberAssignment(ma.Member, ConvertStructAccessIfNeeded(ma.Expression, memberOperator)));
				}
			}
			if(expression.NewExpression.Constructor == null)
				return new MemberInitOperator(expression.NewExpression.Type, members, true);
			return new MemberInitOperator(false, members, expression.NewExpression.Constructor);
		}
		CriteriaOperator ParseObjectExpression(Expression expression) {
			CriteriaOperator result = ParseExpression(expression);
			if(HasExpressionAccess(result))
				return result;
			QuerySet set = result as QuerySet;
			if(IsNull(set))
				return result;
			if(!IsNull(set.Property)) {
				if(set.Property.PropertyName == "^") {
					return Parser.UpThisCriteria;
				}
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupported, expression));
			}
			CriteriaOperator propertyFromProjection;
			set.TryGetProjectionSingleProperty(out propertyFromProjection);
			FreeQuerySet freeSet = set as FreeQuerySet;
			if(!IsNull(freeSet)) {
				if(IsNull(set.Projection)) {
					return freeSet.CreateCriteriaOperator(new OperandProperty("This"), Aggregate.Single);
				}
				else {
					if(!IsNull(propertyFromProjection))
						return freeSet.CreateCriteriaOperator(propertyFromProjection, Aggregate.Single);
				}
			}
			return IsNull(propertyFromProjection) ? ThisCriteria : propertyFromProjection;
		}
		CriteriaOperator NotEqual(BinaryExpression expression) {
			var leftExpression = expression.Left;
			var rightExpression = expression.Right;
			if(IsVBStringCompare(expression)) {
				return CompareVB((MethodCallExpression)leftExpression, BinaryOperatorType.NotEqual);
			}
			return EqualsCore(expression, leftExpression, rightExpression, true);
		}
		CriteriaOperator CompareVB(MethodCallExpression expression, BinaryOperatorType type) {
			CriteriaOperator left = ParseObjectExpression(expression.Arguments[0]);
			CriteriaOperator right = ParseObjectExpression(expression.Arguments[1]);
			if(HasExpressionOrMemberInitOrQuerySet(left, right)) return new ExpressionAccessOperator(expression, left, right);
			return new BinaryOperator(left, right, type);
		}
		CriteriaOperator Parameter(ParameterExpression expression) {
			CriteriaOperator val;
			if(!parameters.TryGetValue(expression, out val))
				return new ExpressionAccessOperator(expression);
			if(Resolver != null) {
				QuerySet q = val as QuerySet;
				if(!q.IsEmpty)
					Resolver.SetUsed();
			}
			return val;
		}
		CriteriaOperator Equal(BinaryExpression expression) {
			var leftExpression = expression.Left;
			var rightExpression = expression.Right;
			if(IsVBStringCompare(expression)) {
				return CompareVB((MethodCallExpression)leftExpression, BinaryOperatorType.Equal);
			}
			return EqualsCore(expression, leftExpression, rightExpression, false);
		}
		CriteriaOperator EqualsCore(Expression expression, Expression leftExpression, Expression rightExpression, bool negation) {
			if(BinaryDetectCheckCollectionIsNull(leftExpression, rightExpression)) {
				return new ConstantValue(negation);
			}
			CriteriaOperator result;
			if(BinaryDetectCheckIsInstanceOfType(leftExpression, rightExpression, negation, out result)) {
				return result;
			}
			CriteriaOperator right = ParseObjectExpression(rightExpression);
			bool rightIsExpressionAccess = HasExpressionOrMemberInitOrQuerySet(right);
			if(BinaryDetectCompare(expression, leftExpression, rightExpression, right, negation ? BinaryOperatorType.NotEqual : BinaryOperatorType.Equal, rightIsExpressionAccess, out result)) {
				return result;
			}
			CriteriaOperator left = ParseObjectExpression(leftExpression);
			if(rightIsExpressionAccess || HasExpressionOrMemberInitOrQuerySet(left)) {
				return new ExpressionAccessOperator(expression, left, right);
			}
			ConstantValue constantRight = right as ConstantValue;
			if(!IsNull(constantRight) && constantRight.Value == null) {
				return negation ? new NotOperator(new NullOperator(left)) : (CriteriaOperator)new NullOperator(left);
			}
			ConstantValue constantLeft = left as ConstantValue;
			if(!IsNull(constantLeft) && constantLeft.Value == null) {
				return negation ? new NotOperator(new NullOperator(right)) : (CriteriaOperator)new NullOperator(right);
			}
			return new BinaryOperator(left, right, negation ? BinaryOperatorType.NotEqual : BinaryOperatorType.Equal);
		}
		bool BinaryDetectCheckCollectionIsNull(Expression left, Expression right) {
			var leftConstant = left as ConstantExpression;
			var rightConstant = right as ConstantExpression;
			Expression checkExpr = null;
			if(rightConstant != null && rightConstant.Value == null) {
				checkExpr = left;
			}
			else if(leftConstant != null && leftConstant.Value == null) {
				checkExpr = right;
			}
			var checkMember = checkExpr as MemberExpression;
			if(checkMember != null && checkMember.Member.MemberType == MemberTypes.Property) {
				return typeof(XPBaseCollection).IsAssignableFrom(checkMember.Type) || (typeof(IList).IsAssignableFrom(checkMember.Type) && !checkMember.Type.IsArray);
			}
			return false;
		}
		bool BinaryDetectCheckIsInstanceOfType(Expression left, Expression right, bool negation, out CriteriaOperator result) {
			var leftUnary = left as UnaryExpression;
			if(leftUnary != null && leftUnary.NodeType == ExpressionType.TypeAs) {
				var rightConstant = right as ConstantExpression;
				if(rightConstant.Value == null) {
					if(leftUnary.Type.IsAssignableFrom(leftUnary.Operand.Type)) {
						result = new ConstantValue(negation);
					}
					else {
						result = TypeIs(Expression.TypeIs(leftUnary.Operand, leftUnary.Type));
						if(!negation) {
							result = new UnaryOperator(UnaryOperatorType.Not, result);
						}
					}
					return true;
				}
			}
			result = null;
			return false;
		}
		CriteriaOperator MemberAccess(MemberExpression expression) {
			CriteriaOperator parent = null;
			XPQueryBase xpQueryBase;
			if(TryExtractXPQuery(expression, out xpQueryBase)) {
				return XPQueryBase.GetFreeQuerySet(xpQueryBase);
			}
			if(expression.Expression != null)
				parent = ParseExpression(expression.Expression);
			return MemberAccess(expression, parent, classInfo);
		}
		internal static bool TryExtractXPQuery(Expression expression, out XPQueryBase xpQueryBase) {
			Expression xpQueryBaseExpression = null;
			xpQueryBase = null;
			if(typeof(XPQueryBase).IsAssignableFrom(expression.Type)) {
				xpQueryBaseExpression = expression;
			}
			else if(expression.Type.IsGenericType && typeof(IQueryable<>).IsAssignableFrom(expression.Type.GetGenericTypeDefinition())) {
				xpQueryBaseExpression = Expression.TypeAs(expression, typeof(XPQueryBase));
			}
			if(xpQueryBaseExpression != null) {
				if(expression.NodeType == ExpressionType.MemberAccess) {
					MemberExpression memberExpression = expression as MemberExpression;
					if(memberExpression.Expression.NodeType == ExpressionType.Parameter) {
						return false;
					}
				}
				var xpQueryBaseLambda = LambdaExpression.Lambda<Func<XPQueryBase>>(xpQueryBaseExpression);
				Func<XPQueryBase> getXPQuery = xpQueryBaseLambda.Compile();
				xpQueryBase = getXPQuery();
				if(xpQueryBase != null) {
					return true;
				}
			}
			return false;
		}
		CriteriaOperator MemberAccess(MemberExpression expression, CriteriaOperator parent, XPClassInfo currentClassInfo) {
			if(HasExpressionAccess(parent)) {
				return new ExpressionAccessOperator(expression, parent);
			}
			IAggregateOperand aggregateOperandParent = parent as IAggregateOperand;
			ICustomAggregateOperand customAggregateParent = parent as ICustomAggregateOperand;
			if(!IsNull(aggregateOperandParent) && aggregateOperandParent.AggregateType == Aggregate.Single && !IsNull(aggregateOperandParent.AggregationObject)) {
				if(aggregateOperandParent is JoinOperand) {
					string joinType = (string)aggregateOperandParent.AggregationObject;
					XPClassInfo joinClassInfo = null;
					if(!MemberInfoCollection.TryResolveTypeAlsoByShortName(joinType, classInfo, out joinClassInfo)) {
						throw new DevExpress.Xpo.Exceptions.CannotResolveClassInfoException(string.Empty, joinType);
					}
					CriteriaOperator joinExpression = MemberAccess(expression, aggregateOperandParent.AggregatedExpression, joinClassInfo);
					if(HasExpressionAccess(joinExpression)) {
						return new ExpressionAccessOperator(expression, parent);
					}
					return new JoinOperand(joinType, aggregateOperandParent.Condition, aggregateOperandParent.AggregateType, joinExpression);
				}
				else {
					OperandProperty property = (OperandProperty)aggregateOperandParent.AggregationObject;
					XPClassInfo aggregateCollectionElementClassInfo;
					if(IsPropertyAssociationList(property.PropertyName, currentClassInfo, out aggregateCollectionElementClassInfo)) {
						CriteriaOperator joinExpression = MemberAccess(expression, aggregateOperandParent.AggregatedExpression, aggregateCollectionElementClassInfo);
						if(HasExpressionAccess(joinExpression)) {
							return new ExpressionAccessOperator(expression, parent);
						}
						return new AggregateOperand(property, joinExpression, aggregateOperandParent.AggregateType, aggregateOperandParent.Condition);
					}
				}
			}
			OperandValue constant = parent as OperandValue;
			if(!IsNull(constant)) {
				if(expression.Expression is ConstantExpression) {
					object value;
					if(Resolver != null && Resolver.MemeberAccessOperator(expression, out value))
						return new ParameterOperandValue(value);
					return new MemberAccessOperator(expression);
				}
				if(constant is ParameterOperandValue) {
					ParameterOperandValue parameter = (ParameterOperandValue)constant;
					Func<object, object> getter = MemberAccessOperator.GetExpression(expression.Member, expression.Expression.Type);
					Func<object, object> prev = parameter.Getter;
					return new ParameterOperandValue(parameter.BaseValue, v => getter(prev(v)));
				}
				return new ConstantCompiler(Dictionary, expression);
			}
			MemberInfo member = expression.Member;
			string name = member.Name;
			bool canCreateConstantCompiler = false;
			PropertyInfo pi = member as PropertyInfo;
			FieldInfo fi = member as FieldInfo;
			if((pi != null && pi.GetGetMethod(true).IsStatic) || (fi != null && fi.IsStatic)) {
				canCreateConstantCompiler = true;
			}
			QuerySet set = parent as QuerySet;
			GroupSet group = parent as GroupSet;
			if(member.DeclaringType == typeof(DateTime)) {
				CriteriaOperator dateTimeResult;
				if(AccessDateTime(parent, name, out dateTimeResult)) {
					return dateTimeResult;
				}
				if(!canCreateConstantCompiler) throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupported, expression));
			}
			if(member.DeclaringType == typeof(DateOnly)) {
				CriteriaOperator dateOnlyResult;
				if(AccessDateTime(parent, name, out dateOnlyResult)) {
					return dateOnlyResult;
				}
				if(!canCreateConstantCompiler) throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupported, expression));
			}
			if(member.DeclaringType == typeof(TimeOnly)) {
				CriteriaOperator timeOnlyResult;
				if(AccessDateTime(parent, name, out timeOnlyResult)) {
					return timeOnlyResult;
				}
				if(!canCreateConstantCompiler) throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupported, expression));
			}
			if(canCreateConstantCompiler) {
				return new ConstantCompiler(Dictionary, expression);
			}
			if(member.DeclaringType == typeof(string)) {
				return AccessString(parent, name);
			}
			if((member.DeclaringType.IsGenericType && member.DeclaringType.GetGenericTypeDefinition() == typeof(ICollection<>)) || member.DeclaringType == typeof(XPBaseCollection)) {
				if(IsNull(set))
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupported, expression));
				if(name == "Count") {
					FreeQuerySet freeSet = set as FreeQuerySet;
					if(IsNull(freeSet))
						return set.CreateCriteriaOperator(null, Aggregate.Count);
					else
						return freeSet.CreateCriteriaOperator(null, Aggregate.Count);
				}
			}
			if(Nullable.GetUnderlyingType(member.DeclaringType) != null) {
				return AccessNullable(parent, name);
			}
			FreeQuerySet fSet = parent as FreeQuerySet;
			if(!IsNull(fSet)) {
				if(IsNull(fSet.Projection)) {
					var fSetClassInfo = Dictionary.GetClassInfo(fSet.JoinType);
					if(IsPropertyNonPersistent(name, fSetClassInfo) || forceExpressionOperatorForQuerySet > 0) {
						return new ExpressionAccessOperator(expression, parent);
					}
					XPClassInfo propertyClassInfo;
					if(IsPropertyAssociationList(name, fSetClassInfo, out propertyClassInfo)) {
						return fSet.CreateCriteriaOperator(new QuerySet(name), Aggregate.Single);
					}
					return fSet.CreateCriteriaOperator(new OperandProperty(name), Aggregate.Single);
				}
				CriteriaOperator projectionProperty;
				if(fSet.TryGetProjectionSingleProperty(out projectionProperty)) {
					CriteriaOperator nestedResult = MemberAccess(expression, projectionProperty, Dictionary.GetClassInfo(fSet.JoinType));
					if(HasExpressionAccess(nestedResult)) {
						return new ExpressionAccessOperator(expression, parent);
					}
					return fSet.CreateCriteriaOperator(nestedResult, Aggregate.Single);
				}
			}
			MemberInitOperator init = IsNull(set) ? parent as MemberInitOperator : set.Projection;
			if(!IsNull(group) && name == "Key")
				return group.Key;
			if(!IsNull(init)) {
				foreach(XPMemberAssignment m in init.Members) {
					if(m.MemberName == null)
						continue;
					MemberInfo mi = m.GetMember(member.DeclaringType);
					if(mi == member || (pi != null && mi == pi.GetGetMethod()))
						return m.Property;
					if(mi.DeclaringType == member.DeclaringType && mi.ReflectedType != member.ReflectedType && mi.Name == member.Name
						&& (mi.ReflectedType.IsAssignableFrom(member.ReflectedType) || member.ReflectedType.IsAssignableFrom(mi.ReflectedType))) {
						return m.Property;
					}
				}
				if(!init.CreateNewObject && init.Members.Count == 1) {
					return MemberAccess(expression, init.Members[0].Property, currentClassInfo);
				}
				return new ExpressionAccessOperator(expression, init);
			}
			OperandProperty parentProp = IsNull(set) ? parent as OperandProperty : set.Property;
			if(!IsNull(parentProp)) {
				XPClassInfo parentElmentClassInfo;
				if(!IsNull(set) && IsPropertyAssociationList(parentProp.PropertyName, classInfo, out parentElmentClassInfo)) {
					if(IsNull(set.Projection)) {
						if(IsPropertyNonPersistent(name, parentElmentClassInfo) || forceExpressionOperatorForQuerySet > 0) {
							return new ExpressionAccessOperator(expression, parent);
						}
						XPClassInfo propertyClassInfo;
						if(IsPropertyAssociationList(name, parentElmentClassInfo, out propertyClassInfo)) {
							return set.CreateCriteriaOperator(new QuerySet(name), Aggregate.Single);
						}
						return set.CreateCriteriaOperator(new OperandProperty(name), Aggregate.Single);
					}
					CriteriaOperator projectionProperty;
					if(set.TryGetProjectionSingleProperty(out projectionProperty)) {
						CriteriaOperator nestedResult = MemberAccess(expression, projectionProperty, currentClassInfo);
						if(HasExpressionAccess(nestedResult)) {
							return new ExpressionAccessOperator(expression, parent);
						}
						return set.CreateCriteriaOperator(nestedResult, Aggregate.Single);
					}
				}
				if(parentProp.PropertyName[parentProp.PropertyName.Length - 1] == '>')
					name = parentProp.PropertyName + name;
				else
					name = string.Concat(parentProp.PropertyName, ".", name);
			}
			else {
				if(!IsNull(parent) && IsNull(set)) {
					FunctionOperator func = parent as FunctionOperator;
					if(!ReferenceEquals(func, null) && func.OperatorType == FunctionOperatorType.Iif) {
						FunctionOperator funcProcessed = new FunctionOperator(FunctionOperatorType.Iif);
						for(int i = 1; i < func.Operands.Count - 1; i += 2) {
							funcProcessed.Operands.Add(func.Operands[i - 1]);
							OperandValue operandVal = (func.Operands[i] as OperandValue);
							if(!ReferenceEquals(null, operandVal) && operandVal.Value == null) {
								funcProcessed.Operands.Add(operandVal);
							}
							else {
								CriteriaOperator nestedResult = MemberAccess(expression, func.Operands[i], currentClassInfo);
								if(HasExpressionOrMemberInitOrQuerySet(nestedResult)) {
									return new ExpressionAccessOperator(expression, parent);
								}
								funcProcessed.Operands.Add(nestedResult);
							}
						}
						CriteriaOperator lastOperand = func.Operands.Last();
						OperandValue lastOperandVal = (lastOperand as OperandValue);
						if(!ReferenceEquals(null, lastOperandVal) && lastOperandVal.Value == null) {
							funcProcessed.Operands.Add(lastOperandVal);
						}
						else {
							CriteriaOperator lastNestedResult = MemberAccess(expression, lastOperand, currentClassInfo);
							if(HasExpressionOrMemberInitOrQuerySet(lastNestedResult)) {
								return new ExpressionAccessOperator(expression, parent);
							}
							funcProcessed.Operands.Add(lastNestedResult);
						}
						return funcProcessed;
					}
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupported, expression));
				}
			}
			if(name.StartsWith("This.")) {
				name = name.Substring(5);
			}
			else {
				name = name.Replace(".This.", ".");
			}
			XPClassInfo collectionElementClassInfo;
			if(IsPropertyAssociationList(name, currentClassInfo, out collectionElementClassInfo)) {
				if(forceExpressionOperatorForQuerySet > 0) {
					return new ExpressionAccessOperator(expression, parent);
				}
				return new QuerySet(name);
			}
			if(IsPropertyNonPersistent(name, currentClassInfo) && ThisCriteria.PropertyName != name) {
				return new ExpressionAccessOperator(expression, parent);
			}
			XPMemberInfo structMemberInfo;
			if(IsStructProperty(name, currentClassInfo, out structMemberInfo)) {
				var memberCollection = new XPMemberAssignmentCollection();
				foreach(XPMemberInfo subMemberInfo in structMemberInfo.SubMembers) {
					if(!subMemberInfo.IsPersistent) {
						continue;
					}
					string[] subMemberNameParts = subMemberInfo.Name.Split('.');
					string subMemberName = subMemberNameParts[subMemberNameParts.Length - 1];
					memberCollection.Add(new XPMemberAssignment() {
						MemberName = subMemberName,
						Property = new OperandProperty(string.Concat(name, ".", subMemberName))
					});
				}
				return new MemberInitOperator(structMemberInfo.MemberType, memberCollection, true);
			}
			return new OperandProperty(name);
		}
		static bool IsStructProperty(string name, XPClassInfo currentClassInfo, out XPMemberInfo structMemberInfo) {
			if(!string.IsNullOrEmpty(name) && name[0] != '^' && name[name.Length - 1] != '^') {
				var col = currentClassInfo.ParsePath(name);
				if(col.Count > 0 && (structMemberInfo = col[col.Count - 1]).IsStruct) {
					return true;
				}
			}
			structMemberInfo = null;
			return false;
		}
		static bool IsPropertyAssociationList(string name, XPClassInfo currentClassInfo, out XPClassInfo collectionElementClassInfo) {
			if(!string.IsNullOrEmpty(name) && name[0] != '^') {
				var col = currentClassInfo.ParsePath(name);
				var property = col[col.Count - 1];
				if(property.IsAssociationList) {
					collectionElementClassInfo = property.CollectionElementType;
					return true;
				}
			}
			collectionElementClassInfo = null;
			return false;
		}
		static bool IsPropertyNonPersistent(string name, XPClassInfo currentClassInfo) {
#pragma warning disable 618
			if(!XPQueryBase.SuppressNonPersistentPropertiesCheck) {
#pragma warning restore 618
				if(!string.IsNullOrEmpty(name) && name[0] != '^' && name[name.Length - 1] != '^') {
					var col = currentClassInfo.ParsePath(name);
					if(col.Count > 0 && col.Any(m => !m.IsExpandableToPersistent)) {
						return true;
					}
				}
			}
			return false;
		}
		static bool AccessDateTime(CriteriaOperator parent, string name, out CriteriaOperator result) {
			switch(name) {
				case "Date":
					result = new FunctionOperator(FunctionOperatorType.GetDate, parent);
					return true;
				case "Millisecond":
					result = new FunctionOperator(FunctionOperatorType.GetMilliSecond, parent);
					return true;
				case "Second":
					result = new FunctionOperator(FunctionOperatorType.GetSecond, parent);
					return true;
				case "Minute":
					result = new FunctionOperator(FunctionOperatorType.GetMinute, parent);
					return true;
				case "Hour":
					result = new FunctionOperator(FunctionOperatorType.GetHour, parent);
					return true;
				case "Day":
					result = new FunctionOperator(FunctionOperatorType.GetDay, parent);
					return true;
				case "Month":
					result = new FunctionOperator(FunctionOperatorType.GetMonth, parent);
					return true;
				case "Year":
					result = new FunctionOperator(FunctionOperatorType.GetYear, parent);
					return true;
				case "DayOfWeek":
					result = new FunctionOperator(FunctionOperatorType.GetDayOfWeek, parent);
					return true;
				case "DayOfYear":
					result = new FunctionOperator(FunctionOperatorType.GetDayOfYear, parent);
					return true;
				case "TimeOfDay":
					result = new FunctionOperator(FunctionOperatorType.GetTimeOfDay, parent);
					return true;
				case "Now":
					result = new FunctionOperator(FunctionOperatorType.Now);
					return true;
				case "UtcNow":
					result = new FunctionOperator(FunctionOperatorType.UtcNow);
					return true;
				case "Today":
					result = new FunctionOperator(FunctionOperatorType.Today);
					return true;
				default:
					result = null;
					return false;
			}
		}
		private static CriteriaOperator AccessNullable(CriteriaOperator parent, string name) {
			switch(name) {
				case "Value":
					return parent;
				case "HasValue":
					return new NotOperator(new NullOperator(parent));
				default:
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_MethodX0ForX1IsNotSupported, name, "nullable type"));
			}
		}
		private static CriteriaOperator AccessString(CriteriaOperator parent, string name) {
			switch(name) {
				case "Length":
					return new FunctionOperator(FunctionOperatorType.Len, parent);
				default:
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_MethodX0ForX1IsNotSupported, name, "string"));
			}
		}
		internal static bool TryConvertToQuerySet(CriteriaOperator op, XPClassInfo classInfo, out QuerySet querySet) {
			ExpressionAccessOperator expressionAccessOperator = op as ExpressionAccessOperator;
			if(!IsNull(expressionAccessOperator)) {
				var expression = expressionAccessOperator.LinqExpression;
				if(expression.NodeType == ExpressionType.Call) {
					MethodCallExpression call = (MethodCallExpression)expression;
					switch(call.Method.Name) {
						case "AsQueryable":
							if(expressionAccessOperator.SourceItems != null && expressionAccessOperator.SourceItems.Length == 2) {
								querySet = expressionAccessOperator.SourceItems[1] as QuerySet;
								if(!IsNull(querySet)) {
									return true;
								}
								op = expressionAccessOperator.SourceItems[1];
							}
							break;
					}
				}
			}
			IAggregateOperand operand = op as IAggregateOperand;
			if(operand != null && operand.AggregateType == Aggregate.Single) {
				var projection = ReferenceEquals(operand.AggregatedExpression, null) ? null : MemberInitOperator.CreateUntypedMemberInitOperator(operand.AggregatedExpression);
				if(operand is JoinOperand) {
					XPClassInfo joinClassInfo = null;
					if(!MemberInfoCollection.TryResolveTypeAlsoByShortName((string)operand.AggregationObject, classInfo, out joinClassInfo)) {
						throw new DevExpress.Xpo.Exceptions.CannotResolveClassInfoException(string.Empty, (string)operand.AggregationObject);
					}
					querySet = new FreeQuerySet(joinClassInfo.ClassType, operand.Condition) {
						Projection = projection
					};
				}
				else {
					querySet = new QuerySet((OperandProperty)operand.AggregationObject, operand.Condition) {
						Projection = projection
					};
				}
				return true;
			}
			querySet = null;
			return false;
		}
		CriteriaOperator AggregateCall(MethodCallExpression call, Aggregate type) {
			QuerySet col = GetCollectionPropertyForAggregateCall(call);
			FreeQuerySet freeSet = col as FreeQuerySet;
			if(IsNull(freeSet)) {
				CriteriaOperator expression;
				if(call.Arguments.Count == 2) {
					expression = ParseExpression(col.Property, call.Arguments[1], new QuerySet(col.Projection));
				}
				else {
					if(IsNull(col.Projection) || col.Projection.CreateNewObject || !col.Projection.TryGetSingleProperty(out expression))
						throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, call.Arguments[0], call.Method.Name));
				}
				if(HasExpressionAccess(expression))
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, GetCauseStringOfExpressionAccess(expression), call.Method.Name));
				return col.CreateCriteriaOperator(expression, type);
			}
			else {
				CriteriaOperator expression;
				if(call.Arguments.Count == 2) {
					expression = ParseExpression(freeSet.JoinType, call.Arguments[1], new QuerySet(freeSet.Projection));
				}
				else {
					if(IsNull(col.Projection) || col.Projection.CreateNewObject || !col.Projection.TryGetSingleProperty(out expression))
						throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, call.Arguments[0], call.Method.Name));
				}
				if(HasExpressionAccess(expression))
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, GetCauseStringOfExpressionAccess(expression), call.Method.Name));
				return freeSet.CreateCriteriaOperator(expression, type);
			}
		}
		CriteriaOperator AggregateCall(MethodCallExpression call, string customAggregateName) {
			QuerySet col = GetCollectionPropertyForAggregateCall(call);
			FreeQuerySet freeSet = col as FreeQuerySet;
			if(IsNull(freeSet)) {
				CriteriaOperator[] aggrExprs = new CriteriaOperator[call.Arguments.Count - 1];
				QuerySet projectionQuerySet = new QuerySet(col.Projection);
				for(int i = 0; i < call.Arguments.Count - 1; i++) {
					CriteriaOperator expression = ParseExpression(col.Property, call.Arguments[i + 1], projectionQuerySet);
					if(HasExpressionAccess(expression))
						throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, GetCauseStringOfExpressionAccess(expression), call.Method.Name));
					aggrExprs[i] = expression;
				}
				return col.CreateCriteriaOperator(aggrExprs, customAggregateName);
			}
			else {
				CriteriaOperator[] aggrExprs = new CriteriaOperator[call.Arguments.Count - 1];
				QuerySet projectionQuerySet = new QuerySet(freeSet.Projection);
				for(int i = 0; i < call.Arguments.Count - 1; i++) {
					CriteriaOperator expression = ParseExpression(freeSet.JoinType, call.Arguments[i + 1], new QuerySet(freeSet.Projection));
					if(HasExpressionAccess(expression))
						throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, GetCauseStringOfExpressionAccess(expression), call.Method.Name));
					aggrExprs[i] = expression;
				}
				return freeSet.CreateCriteriaOperator(aggrExprs, customAggregateName);
			}
		}
		QuerySet GetCollectionPropertyForAggregateCall(MethodCallExpression aggregateCallExpression) {
			CriteriaOperator co = ParseExpression(aggregateCallExpression.Arguments[0]);
			QuerySet col = co as QuerySet;
			if(IsNull(col)) {
				OperandProperty prop = co as OperandProperty;
				if(IsNull(prop)) {
					if(!TryConvertToQuerySet(co, classInfo, out col)) {
						throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, aggregateCallExpression.Arguments[0], aggregateCallExpression.Method.Name));
					}
				}
				else {
					col = new QuerySet(prop, null);
				}
			}
			return col;
		}
		CriteriaOperator Sum(MethodCallExpression call) {
			return AggregateCall(call, Aggregate.Sum);
		}
		CriteriaOperator Average(MethodCallExpression call) {
			return AggregateCall(call, Aggregate.Avg);
		}
		CriteriaOperator Min(MethodCallExpression call) {
			return AggregateCall(call, Aggregate.Min);
		}
		CriteriaOperator Max(MethodCallExpression call) {
			return AggregateCall(call, Aggregate.Max);
		}
		CriteriaOperator All(MethodCallExpression call) {
			CriteriaOperator co = ParseExpression(call.Arguments[0]);
			QuerySet col = co as QuerySet;
			if(IsNull(col)) {
				OperandProperty op = co as OperandProperty;
				if(IsNull(op)) {
					if(!TryConvertToQuerySet(co, classInfo, out col)) {
						throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, call.Arguments[0], "the All method call")); ;
					}
				}
				else {
					col = new QuerySet(op, null);
				}
			}
			FreeQuerySet freeCol = col as FreeQuerySet;
			if(call.Arguments.Count == 2) {
				if(!IsNull(freeCol)) {
					return new NotOperator(freeCol.CreateCriteriaOperator(GroupOperator.And(new NotOperator(ParseExpression(freeCol.JoinType, call.Arguments[1], new QuerySet(freeCol.Projection))), freeCol.Condition),
						null, Aggregate.Exists));
				}
				if(!IsNull(col))
					return new NotOperator(col.CreateCriteriaOperator(new NotOperator(GroupOperator.And(ParseExpression(col.Property, call.Arguments[1], new QuerySet(col.Projection)))),
						null, Aggregate.Exists));
			}
			throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, call.Arguments[0], "the All method call"));
		}
		CriteriaOperator Any(MethodCallExpression call) {
			var co = ParseExpression(call.Arguments[0]);
			QuerySet col = co as QuerySet;
			if(IsNull(col)) {
				OperandProperty op = co as OperandProperty;
				if(IsNull(op)) {
					MemberInitOperator memberInit = co as MemberInitOperator;
					OperandValue operandValue = co as OperandValue;
					if(!IsNull(memberInit) && memberInit.GetDeclaringType().GetInterfaces().Any(i => i == typeof(IEnumerable))) {
						List<object> list = new List<object>(memberInit.Members.Count);
						bool success = false;
						foreach(var member in memberInit.Members) {
							OperandValue memberValue = member.Property as OperandValue;
							if(IsNull(memberValue)) {
								success = false;
								break;
							}
							list.Add(memberValue.Value);
							success = true;
						}
						if(success) {
							operandValue = new OperandValue(list);
						}
					}
					if(!IsNull(operandValue) && operandValue.Value != null && (operandValue.Value is IEnumerable) && call != null) {
						Expression body = ExtractLambdaBody(call.Arguments[1]);
						if(body.NodeType == ExpressionType.Equal) {
							BinaryExpression binary = (BinaryExpression)body;
							InOperator inOperator = null;
							if(binary.Left.NodeType == ExpressionType.Parameter) {
								inOperator = new InOperator() {
									LeftOperand = ParseExpression(binary.Right),
								};
							}
							if(binary.Right.NodeType == ExpressionType.Parameter) {
								inOperator = new InOperator() {
									LeftOperand = ParseExpression(binary.Left),
								};
							}
							if(!IsNull(inOperator)) {
								foreach(var item in (IEnumerable)operandValue.Value) {
									inOperator.Operands.Add(new OperandValue(item));
								}
								return inOperator;
							}
						}
					}
					if(!TryConvertToQuerySet(co, classInfo, out col)) {
						throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, call.Arguments[0], "the Any method call")); ;
					}
				}
				else {
					col = new QuerySet(op, null);
				}
			}
			if(!IsNull(col)) {
				if(call.Arguments.Count == 2) {
					FreeQuerySet freeCol = col as FreeQuerySet;
					if(!IsNull(freeCol)) {
						return freeCol.CreateCriteriaOperator(GroupOperator.And(ParseExpression(freeCol.JoinType, call.Arguments[1], new QuerySet(freeCol.Projection)), freeCol.Condition), null, Aggregate.Exists);
					}
					if(IsNull(col.Condition))
						return col.CreateCriteriaOperator(GroupOperator.And(ParseExpression(col.Property, call.Arguments[1], new QuerySet(col.Projection))), null, Aggregate.Exists);
					else
						return col.CreateCriteriaOperator(GroupOperator.And(col.Condition, GroupOperator.And(ParseExpression(col.Property, call.Arguments[1], new QuerySet(col.Projection)))), null, Aggregate.Exists);
				}
				else {
					CriteriaOperator result;
					if(AnyCore(call, col, out result)) {
						return result;
					}
				}
			}
			throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, call.Arguments[0], "the Any method call"));
		}
		bool AnyCore(MethodCallExpression call, QuerySet col, out CriteriaOperator result) {
			FreeQuerySet freeCol = col as FreeQuerySet;
			QuerySet nestedSet = null;
			if(!IsNull(col) && (IsNull(col.Projection) || !col.Projection.TryGetSingleQuerySet(out nestedSet))) {
				if(!IsNull(freeCol)) {
					result = freeCol.CreateCriteriaOperator(null, Aggregate.Exists);
					return true;
				}
				else {
					result = col.CreateCriteriaOperator(null, Aggregate.Exists);
					return true;
				}
			}
			else if(!IsNull(nestedSet)) {
				CriteriaOperator nestedExists;
				if(AnyCore(null, nestedSet, out nestedExists)) {
					if(!IsNull(freeCol)) {
						result = freeCol.CreateCriteriaOperator(GroupOperator.And(freeCol.Condition, nestedExists), null, Aggregate.Exists);
						return true;
					}
					else {
						result = col.CreateCriteriaOperator(GroupOperator.And(col.Condition, nestedExists), null, Aggregate.Exists);
						return true;
					}
				}
			}
			result = null;
			return false;
		}
		Expression ExtractLambdaBody(Expression lambda) {
			Expression result = lambda;
			do {
				switch(result.NodeType) {
					case ExpressionType.Lambda:
						result = ((LambdaExpression)result).Body;
						continue;
					case ExpressionType.Quote:
						result = ((UnaryExpression)result).Operand;
						continue;
				}
				break;
			} while(true);
			return result;
		}
		CriteriaOperator Contains(MethodCallExpression call) {
			if(call.Arguments.Count != 2)
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_X0WithSoManyParametersIsNotSupported, "Contains operator"));
			if(call.Arguments[0].NodeType == ExpressionType.NewArrayInit) {
				List<CriteriaOperator> ops = new List<CriteriaOperator>();
				foreach(Expression e in ((NewArrayExpression)call.Arguments[0]).Expressions)
					ops.Add(ParseExpression(e));
				return new InOperator(ParseObjectExpression(call.Arguments[1]), ops);
			}
			CriteriaOperator inList = ParseExpression(call.Arguments[0]);
			QuerySet col = inList as QuerySet;
			if(IsNull(col)) {
				OperandValue constantList = inList as OperandValue;
				if(!IsNull(constantList)) {
					return new InOperatorCompiler(Dictionary, ParseObjectExpression(call.Arguments[1]), constantList);
				}
				if(!TryConvertToQuerySet(inList, classInfo, out col)) {
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupported, call.Arguments[0]));
				}
			}
			FreeQuerySet freePath = col as FreeQuerySet;
			CriteriaOperator leftOperand = ThisCriteria;
			if(!IsNull(freePath)) {
				if(!IsNull(freePath.Projection)) {
					if(!freePath.TryGetProjectionSingleProperty(out leftOperand)) {
						throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupported, call.Arguments[0]));
					}
				}
				return freePath.CreateCriteriaOperator(GroupOperator.And(freePath.Condition, new BinaryOperator(leftOperand, ParseExpression(freePath.JoinType, call.Arguments[1]), BinaryOperatorType.Equal)), null, Aggregate.Exists);
			}
			if(!IsNull(col.Projection)) {
				if(!col.TryGetProjectionSingleProperty(out leftOperand)) {
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupported, call.Arguments[0]));
				}
			}
			return col.CreateCriteriaOperator(GroupOperator.And(col.Condition, new BinaryOperator(leftOperand, ParseExpression(call.Arguments[1]), BinaryOperatorType.Equal)), null, Aggregate.Exists);
		}
		CriteriaOperator ListContains(MethodCallExpression call) {
			CriteriaOperator inList = ParseExpression(call.Object);
			QuerySet col = inList as QuerySet;
			if(IsNull(col)) {
				OperandValue constantList = inList as OperandValue;
				if(!IsNull(constantList)) {
					return new InOperatorCompiler(Dictionary, ParseObjectExpression(call.Arguments[0]), constantList);
				}
				if(!TryConvertToQuerySet(inList, classInfo, out col)) {
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupported, call));
				}
			}
			return col.CreateCriteriaOperator(GroupOperator.And(col.Condition, new BinaryOperator(ThisCriteria, ParseExpression(call.Arguments[0]), BinaryOperatorType.Equal)), null, Aggregate.Exists);
		}
		CriteriaOperator Count(MethodCallExpression call) {
			CriteriaOperator co = ParseExpression(call.Arguments[0]);
			QuerySet path = co as QuerySet;
			if(IsNull(path)) {
				OperandProperty op = co as OperandProperty;
				if(!IsNull(op)) {
					path = new QuerySet(op, null);
				}
				else {
					if(!TryConvertToQuerySet(co, classInfo, out path)) {
						throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, call.Arguments[0], "the Count method call"));
					}
				}
			}
			return CountCore(call, path);
		}
		CriteriaOperator CountCore(MethodCallExpression call, QuerySet path) {
			QuerySet nestedSet;
			if(IsNull(path.Projection) || !path.Projection.TryGetSingleQuerySet(out nestedSet)) {
				FreeQuerySet freePath = path as FreeQuerySet;
				if(IsNull(freePath)) {
					CriteriaOperator criteria = call != null && call.Arguments.Count > 1 ? ParseExpression(path.Property, call.Arguments[1], new QuerySet(path.Projection)) : null;
					return new AggregateOperand(path.Property, null, Aggregate.Count, GroupOperator.And(path.Condition, criteria));
				}
				else {
					CriteriaOperator criteria = call != null && call.Arguments.Count > 1 ? ParseExpression(freePath.JoinType, call.Arguments[1], new QuerySet(path.Projection)) : null;
					return freePath.CreateCriteriaOperator(GroupOperator.And(freePath.Condition, criteria), null, Aggregate.Count);
				}
			}
			else {
				var expression = CountCore(null, nestedSet);
				FreeQuerySet freePath = path as FreeQuerySet;
				if(IsNull(freePath)) {
					CriteriaOperator criteria = call != null && call.Arguments.Count > 1 ? ParseExpression(path.Property, call.Arguments[1], new QuerySet(path.Projection)) : null;
					return new AggregateOperand(path.Property, expression, Aggregate.Single, GroupOperator.And(path.Condition, criteria));
				}
				else {
					CriteriaOperator criteria = call != null && call.Arguments.Count > 1 ? ParseExpression(freePath.JoinType, call.Arguments[1], new QuerySet(path.Projection)) : null;
					return freePath.CreateCriteriaOperator(GroupOperator.And(freePath.Condition, criteria), expression, Aggregate.Single);
				}
			}
		}
		Dictionary<ParameterExpression, CriteriaOperator> PrepareParametersForJoin() {
			return PatchParameters("^");
		}
		Dictionary<ParameterExpression, CriteriaOperator> PrepareParameters(Expression call, OperandProperty path, int upDepth) {
			string pathStr = String.Empty;
			if(!IsNull(path)) {
				for(int i = 0; i < upDepth; i++) {
					pathStr += string.IsNullOrEmpty(pathStr) ? "^" : ".^";
				}
				return PatchParameters(pathStr);
			}
			else
				return parameters;
		}
		Dictionary<ParameterExpression, CriteriaOperator> PatchParameters(string pathStr) {
			Dictionary<ParameterExpression, CriteriaOperator> newParameters = new Dictionary<ParameterExpression, CriteriaOperator>();
			foreach(KeyValuePair<ParameterExpression, CriteriaOperator> p in parameters) {
				CriteriaOperator parameter = PatchParameter(pathStr, p.Value);
				newParameters.Add(p.Key, parameter);
			}
			return newParameters;
		}
		class PatchParameterVisitor : ClientCriteriaLazyPatcherBase, ILinqExtendedCriteriaVisitor<CriteriaOperator> {
			int upDepth;
			readonly string patchPath;
			public PatchParameterVisitor(string patchPath) {
				this.patchPath = patchPath;
			}
			public CriteriaOperator Visit(MemberInitOperator theOperand) {
				XPMemberAssignmentCollection list = new XPMemberAssignmentCollection();
				bool isModified = false;
				foreach(XPMemberAssignment ma in theOperand.Members) {
					var processed = Process(ma.Property);
					if(ReferenceEquals(processed, ma.Property)) {
						list.Add(ma);
						continue;
					}
					isModified = true;
					list.Add(new XPMemberAssignment(ma, processed));
				}
				return isModified ? new MemberInitOperator(theOperand, list) : theOperand;
			}
			public CriteriaOperator Visit(ExpressionAccessOperator theOperand) {
				using(var processed = LohPooled.ToListForDispose(theOperand.SourceItems)) {
					if(AreSequenceReferenceEqual(theOperand.SourceItems, processed))
						return theOperand;
					return new ExpressionAccessOperator(theOperand.LinqExpression, theOperand.InsertFirstNull, processed.ToArray());
				}
			}
			readonly OperandProperty EmptyProperty = new OperandProperty(null);
			public CriteriaOperator Visit(QuerySet theOperand) {
				FreeQuerySet freeSet = theOperand as FreeQuerySet;
				if(!IsNull(freeSet)) {
					upDepth++;
					try {
						var processedCondition = Process(freeSet.Condition);
						var processedProjection = (MemberInitOperator)Process(freeSet.Projection);
						if(freeSet.HasMasterJoin) {
							throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, theOperand, "Join"));
						}
						if(ReferenceEquals(processedCondition, freeSet.Condition) && ReferenceEquals(processedProjection, freeSet.Projection)) {
							return freeSet;
						}
						return new FreeQuerySet() {
							JoinType = freeSet.JoinType,
							Projection = processedProjection,
							Condition = processedCondition
						};
					}
					finally {
						upDepth--;
					}
				}
				else {
					if(!IsNull(theOperand.Condition)) {
						throw new NotSupportedException(Res.GetString(Res.LinqToXpo_ExpressionX0IsNotSupportedInX1, theOperand, "Join"));
					}
					CriteriaOperator processedKey = null;
					OperandProperty operandProperty = theOperand.Property;
					OperandProperty processedProperty = IsNull(theOperand.Property) ? (!IsNull(theOperand.Projection) ? operandProperty : ProcessCollectionProperty(EmptyProperty)) : ProcessCollectionProperty(operandProperty);
					MemberInitOperator processedProjection = null;
					int currentUpDepth = 0;
					if(!IsNull(theOperand.Property) && !IsNull(theOperand.Property.PropertyName)) {
						string[] propertyPath = MemberInfoCollection.SplitPath(theOperand.Property.PropertyName);
						currentUpDepth = propertyPath.Length;
					}
					GroupSet groupSet = theOperand as GroupSet;
					upDepth += currentUpDepth;
					try {
						if(!IsNull(theOperand.Projection)) {
							processedProjection = IsNull(theOperand.Property) ? (MemberInitOperator)Process(theOperand.Projection) : theOperand.Projection;
						}
						if(!IsNull(groupSet)) {
							processedKey = IsNull(theOperand.Property) ? Process(groupSet.Key) : groupSet.Key;
						}
					}
					finally {
						upDepth -= currentUpDepth;
					}
					if(!IsNull(groupSet)) {
						if(ReferenceEquals(processedKey, groupSet.Key) && ReferenceEquals(processedProperty, operandProperty) && ReferenceEquals(processedProjection, theOperand.Projection)) {
							return theOperand;
						}
						return new GroupSet() {
							Key = processedKey,
							Property = processedProperty,
							Projection = processedProjection
						};
					}
					if(ReferenceEquals(processedProperty, operandProperty) && ReferenceEquals(processedProjection, theOperand.Projection)) {
						return theOperand;
					}
					return new QuerySet() {
						Property = processedProperty,
						Projection = processedProjection
					};
				}
			}
			protected virtual OperandProperty ProcessCollectionProperty(OperandProperty collectionProperty) {
				var processed = Process(collectionProperty);
				if(IsNull(processed) || (processed is OperandProperty))
					return (OperandProperty)processed;
				throw new InvalidOperationException("Process within ProcessCollectionProperty expected to return OperandProperty or null; " + processed.GetType().FullName + " (" + processed.ToString() + ") returned instead");
			}
			public override CriteriaOperator Visit(AggregateOperand theOperand) {
				if(theOperand.IsTopLevel) {
					if(theOperand.AggregateType != Aggregate.Custom) {
						return NewIfDifferent(theOperand, ProcessCollectionProperty(theOperand.CollectionProperty), Process(theOperand.AggregatedExpression), Process(theOperand.Condition));
					}
					return NewIfDifferent(theOperand, ProcessCollectionProperty(theOperand.CollectionProperty), Process(theOperand.CustomAggregateOperands), Process(theOperand.Condition));
				}
				string[] propertyPath = MemberInfoCollection.SplitPath(theOperand.CollectionProperty.PropertyName);
				OperandProperty processedProperty = ProcessCollectionProperty(theOperand.CollectionProperty);
				upDepth += propertyPath.Length;
				try {
					if(theOperand.AggregateType != Aggregate.Custom) {
						return NewIfDifferent(theOperand, processedProperty, Process(theOperand.AggregatedExpression), Process(theOperand.Condition));
					}
					return NewIfDifferent(theOperand, processedProperty, Process(theOperand.CustomAggregateOperands), Process(theOperand.Condition));
				}
				finally {
					upDepth -= propertyPath.Length;
				}
			}
			public override CriteriaOperator Visit(JoinOperand theOperand) {
				upDepth++;
				try {
					if(theOperand.AggregateType != Aggregate.Custom) {
						return NewIfDifferent(theOperand, Process(theOperand.Condition), Process(theOperand.AggregatedExpression));
					}
					return NewIfDifferent(theOperand, Process(theOperand.Condition), Process(theOperand.CustomAggregateOperands));
				}
				finally {
					upDepth--;
				}
			}
			public override CriteriaOperator Visit(OperandProperty theOperand) {
				if(theOperand.PropertyName == null) {
					return upDepth == 0 ? new OperandProperty(patchPath) : theOperand;
				}
				string[] propertyPath = MemberInfoCollection.SplitPath(theOperand.PropertyName);
				int downDepth = propertyPath.Where(p => p == "^").Count();
				int currentUpDepth = upDepth - downDepth;
				return currentUpDepth <= 0 ? new OperandProperty(string.Concat(patchPath, ".", theOperand.PropertyName)) : theOperand;
			}
			public static CriteriaOperator Patch(string patchPath, CriteriaOperator criteria) {
				return new PatchParameterVisitor(patchPath).Process(criteria);
			}
		}
		static CriteriaOperator PatchParameter(string pathStr, CriteriaOperator p) {
			return PatchParameterVisitor.Patch(pathStr, p);
		}
		bool IsVBStringCompare(BinaryExpression expression) {
			return expression.Left.NodeType == ExpressionType.Call
				&& expression.Right.NodeType == ExpressionType.Constant
				&& 0.Equals(((ConstantExpression)expression.Right).Value)
				&& (((MethodCallExpression)expression.Left).Method.DeclaringType.FullName == "Microsoft.VisualBasic.CompilerServices.Operators" || ((MethodCallExpression)expression.Left).Method.DeclaringType.FullName == "Microsoft.VisualBasic.CompilerServices.EmbeddedOperators")
				&& ((MethodCallExpression)expression.Left).Method.Name == "CompareString";
		}
	}
	public class XPQuery<T> : XPQueryBase, IOrderedQueryable<T>, IQueryProvider {
		public XPQuery(IDataLayer dataLayer) : base(dataLayer, typeof(T)) { }
		public XPQuery(Session session) : base(session, typeof(T), false) { }
		public XPQuery(XPDictionary dictionary) : base(dictionary, typeof(T), false) { }
		public XPQuery(Session session, bool inTransaction) : base(session, typeof(T), inTransaction) { }
		public XPQuery(XPDictionary dictionary, bool inTransaction) : base(dictionary, typeof(T), inTransaction) { }
		XPQuery(XPQuery<T> baseQuery) : base(baseQuery) { }
		XPQuery(XPQuery<T> baseQuery, bool? inTransaction = null, bool? withDeleted = null) : base(baseQuery, inTransaction, withDeleted) { }
		XPQuery(XPQuery<T> baseQuery, CustomCriteriaCollection customCriteriaCollection) : base(baseQuery, customCriteriaCollection) { }
		internal XPQuery(Session session, IDataLayer dataLayer, XPDictionary dictionary) : base(session, dataLayer, dictionary) { }
		internal XPQuery(Session session, IDataLayer dataLayer, XPDictionary dictionary, string data) : base(session, dataLayer, dictionary, data) { }
		internal XPQuery(XPDictionary dictionary, string data) : base(dictionary, data) { }
		public static XPQuery<T> Deserialize(Session session, string data) {
			return new XPQuery<T>(session, null, session.Dictionary, data);
		}
		public static XPQuery<T> Deserialize(IDataLayer dataLayer, string data) {
			return new XPQuery<T>(null, dataLayer, dataLayer.Dictionary, data);
		}
		public static XPQuery<T> Deserialize(XPDictionary dictionary, string data) {
			return new XPQuery<T>(dictionary, data);
		}
		static CriteriaOperator[] ThisParams = new CriteriaOperator[] { Parser.ThisCriteria };
		public CriteriaOperator TransformExpression(Expression<Func<T, bool>> expression) {
			return TransformExpression(expression, null);
		}
		public CriteriaOperator TransformExpression(Expression<Func<T, bool>> expression, CustomCriteriaCollection customCriteriaCollection) {
			return TransformExpression(Dictionary, expression, customCriteriaCollection);
		}
		public static CriteriaOperator TransformExpression(Session session, Expression<Func<T, bool>> expression) {
			return TransformExpression(session, expression, null);
		}
		public static CriteriaOperator TransformExpression(XPDictionary dictionary, Expression<Func<T, bool>> expression, CustomCriteriaCollection customCriteriaCollection) {
			return XPQueryExecutePreprocessor.Preprocess(Parser.ParseExpression(dictionary.GetClassInfo(typeof(T)), customCriteriaCollection, expression, ThisParams));
		}
		public static CriteriaOperator TransformExpression(Session session, Expression<Func<T, bool>> expression, CustomCriteriaCollection customCriteriaCollection) {
			return TransformExpression(session.Dictionary, expression, customCriteriaCollection);
		}
		IQueryable IQueryProvider.CreateQuery(Expression expression) {
			Type returnType;
			switch(expression.NodeType) {
				case ExpressionType.Call:
					returnType = ((MethodCallExpression)expression).Method.ReturnType;
					break;
				case ExpressionType.Constant:
					returnType = ((ConstantExpression)expression).Type;
					break;
				default:
					throw new NotSupportedException(Res.GetString(Res.LinqToXpo_TheCallExpressionIsExpectedX0, expression));
			}
			if(returnType == null)
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_TheCallExpressionReturnTypeX0, expression));
			Type[] genArguments = returnType.GetGenericArguments();
			if(genArguments.Length == 0)
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_TheCallExpressionGenericReturnTypeX0, expression));
			return GetCreateQueryMethod(genArguments[0])(this, expression);
		}
		readonly static ConcurrentDictionary<Type, QueryProviderEx.CreateQueryHandler> createQueryMethods = new ConcurrentDictionary<Type, QueryProviderEx.CreateQueryHandler>();
		static QueryProviderEx.CreateQueryHandler GetCreateQueryMethod(Type type) {
			return createQueryMethods.GetOrAdd(type, (t) => {
				MethodInfo mi = typeof(QueryProviderEx).GetMethod("CreateQuery").MakeGenericMethod(t);
				return (QueryProviderEx.CreateQueryHandler)Delegate.CreateDelegate(typeof(QueryProviderEx.CreateQueryHandler), mi);
			});
		}
		IQueryable<S> IQueryProvider.CreateQuery<S>(Expression expression) {
			if(expression.NodeType == ExpressionType.Constant && expression.Type == typeof(XPQuery<S>)) {
				XPQueryBase query;
				if(Parser.TryExtractXPQuery(expression, out query)) {
					return new XPQuery<S>((XPQuery<S>)query);
				}
			}
			XPQuery<S> col = new XPQuery<S>(Session, DataLayer, Dictionary);
			if(expression.NodeType != ExpressionType.Call)
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_TheCallExpressionIsExpectedX0, expression));
			MethodCallExpression call = (MethodCallExpression)expression;
			if(call.Method.DeclaringType != typeof(Queryable) && call.Method.DeclaringType != typeof(Enumerable))
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_IncorrectDeclaringTypeX0InTheMethodCallQue, call.Method.DeclaringType));
			var arguments = call.Arguments;
			if(arguments[0].NodeType == ExpressionType.Call) {
				IQueryable q = (IQueryable)((IQueryProvider)this).CreateQuery(arguments[0]);
				List<Expression> newArguments = new List<Expression>(arguments);
				newArguments[0] = Expression.Constant(q);
				return q.Provider.CreateQuery<S>(Expression.Call(call.Method, newArguments.ToArray()));
			}
			ConstantExpression prevConst = arguments[0] as ConstantExpression;
			if(prevConst == null)
				throw new NotSupportedException(Res.GetString(Res.LinqToXpo_TheCallExpressionXPQueryX0, arguments[0]));
			XPQueryBase prevBase = prevConst.Value as XPQueryBase;
			XPQuery<T> prev = prevConst.Value as XPQuery<T>;
			switch(call.Method.Name) {
				case "Union":
				case "Intersect": {
					if(prev == null)
						throw new NotSupportedException(Res.GetString(Res.LinqToXpo_TheCallExpressionXPQueryX0, arguments[0]));
					Type type = call.Method.GetGenericArguments()[0];
					XPQuery<T> next = ((ConstantExpression)arguments[1]).Value as XPQuery<T>;
					XPClassInfo newObjectClassInfo = Dictionary.QueryClassInfo(type);
					if(next == null || type != typeof(T) || newObjectClassInfo == null ||
						!prev.CanIntersect() || !next.CanIntersect())
						return prev.CallGeneric<S>(call);
					break;
				}
				case "Except":
				case "Concat":
					if(prev == null)
						throw new NotSupportedException(Res.GetString(Res.LinqToXpo_TheCallExpressionXPQueryX0, arguments[0]));
					return prev.CallGeneric<S>(call);
				case "Cast": {
					if(prev == null)
						throw new NotSupportedException(Res.GetString(Res.LinqToXpo_TheCallExpressionXPQueryX0, arguments[0]));
					Type type = call.Method.GetGenericArguments()[0];
					XPClassInfo newObjectClassInfo = Dictionary.QueryClassInfo(type);
					if(newObjectClassInfo == null || !prev.ObjectClassInfo.IsAssignableTo(newObjectClassInfo))
						return prev.CallGeneric<S>(call);
					break;
				}
				default:
					break;
			}
			col.Call(call, prevBase);
			return col;
		}
		IQueryable<S> CallGeneric<S>(MethodCallExpression e) {
			IQueryable q = Queryable.AsQueryable(new EnumerableWrapper<T>(this));
			List<Expression> arguments = new List<Expression>(e.Arguments);
			arguments[0] = Expression.Constant(q);
			return q.Provider.CreateQuery<S>(Expression.Call(e.Method, arguments.ToArray()));
		}
		object IQueryProvider.Execute(Expression expression) {
			return Execute(expression);
		}
		S IQueryProvider.Execute<S>(Expression expression) {
			object res = Execute(expression);
			return res == null ? default(S) : (S)res;
		}
		public async Task<S> ExecuteAsync<S>(Expression expression, CancellationToken cancellationToken = default(CancellationToken)) {
			object res = await ExecuteAsync(expression, cancellationToken).ConfigureAwait(false);
			return res == null ? default(S) : (S)res;
		}
		Type IQueryable.ElementType { get { return typeof(T); } }
		Expression IQueryable.Expression { get { return Expression.Constant(this); } }
		IQueryProvider IQueryable.Provider { get { return this; } }
		IEnumerator<T> IEnumerable<T>.GetEnumerator() {
			return GetEnumerator();
		}
		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}
		IEnumerator<T> GetEnumerator() {
			ICollection data = Enumerate(typeof(T));
			List<T> list = new List<T>(data.Count);
			foreach(T item in data)
				list.Add(item);
			return list.GetEnumerator();
		}
		protected override object CloneCore() {
			return Clone();
		}
		public XPQuery<T> Clone() {
			return new XPQuery<T>(this);
		}
		public XPQuery<T> InTransaction() {
			return new XPQuery<T>(this, true, null);
		}
		public XPQuery<T> WithDeleted() {
			return new XPQuery<T>(this, null, true);
		}
		public XPQuery<T> WithCustomCriteria(CustomCriteriaCollection customCriteriaCollection) {
			return new XPQuery<T>(this, customCriteriaCollection);
		}
		public XPQuery<T> WithCustomCriteria(ICustomCriteriaOperatorQueryable customCriteria) {
			Guard.ArgumentNotNull(customCriteria, nameof(customCriteria));
			CustomCriteriaCollection addColection = new CustomCriteriaCollection();
			addColection.Add(customCriteria);
			return WithCustomCriteria(addColection);
		}
	}
	public static class XPQueryExtensions {
		public delegate void AsyncEnumerateCallback<T>(IEnumerable<T> result, Exception ex);
		public delegate void AsyncEnumerateCallback(IEnumerable result, Exception ex);
		static void ThrowIfNotXPQuery<T>(IQueryable<T> query) {
			if(!(query is XPQueryBase)) {
				throw new ArgumentException(null, nameof(query));
			}
		}
		public static void EnumerateAsync<T>(this IQueryable<T> query, AsyncEnumerateCallback<T> callback) {
			ThrowIfNotXPQuery(query);
			((XPQueryBase)query).EnumerateAsync(typeof(T), delegate (ICollection[] result, Exception ex) {
				if(ex != null)
					callback(null, ex);
				else {
					ICollection data = result[0];
					List<T> list = new List<T>(data.Count);
					foreach(T item in data)
						list.Add(item);
					callback(list, ex);
				}
			});
		}
		public static void EnumerateAsync<T>(this IQueryable<T> query, AsyncEnumerateCallback callback) {
			query.EnumerateAsync(delegate (IEnumerable<T> result, Exception ex) { callback(result, ex); });
		}
		public static async Task<IEnumerable<T>> EnumerateAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default(CancellationToken)) {
			ThrowIfNotXPQuery(query);
			ICollection data = await ((XPQueryBase)query).EnumerateAsync(typeof(T), cancellationToken).ConfigureAwait(false);
			return data.Cast<T>();
		}
		public static XPQuery<T> Query<T>(this Session session) {
			if(session == null) throw new NullReferenceException();
			return new XPQuery<T>(session);
		}
		public static XPQuery<T> Query<T>(this IDataLayer layer) {
			if(layer == null) throw new NullReferenceException();
			return new XPQuery<T>(layer);
		}
		public static XPQuery<T> QueryInTransaction<T>(this Session session) {
			if(session == null) throw new NullReferenceException();
			return new XPQuery<T>(session, true);
		}
		class Cache<TSource> {
			public static Dictionary<Delegate, CachedQuery<TSource>> cache = new Dictionary<Delegate, CachedQuery<TSource>>();
			public static bool GetCached(Delegate f, out CachedQuery<TSource> q) {
				if(!cache.TryGetValue(f, out q)) {
					if(f.Target != null && (Attribute.GetCustomAttribute(f.Target.GetType(), typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)) == null ||
											!Array.TrueForAll(f.Target.GetType().GetFields(), ff => ff.IsStatic))) {
						throw new InvalidOperationException(Res.GetString(Res.LinqToXpo_CachedExpressionIsIncompatible));
					}
					q = new CachedQuery<TSource>(f.Method);
					cache.Add(f, q);
					return false;
				}
				return true;
			}
		}
		public static TResult CachedExpression<TSource, TResult>(this IQueryable<TSource> source, Func<IQueryable<TSource>, TResult> f) {
			CachedQuery<TSource> q;
			lock(Cache<TSource>.cache) {
				if(!Cache<TSource>.GetCached(f, out q))
					f(q);
			}
			return (TResult)q.Process(source);
		}
		public static TResult CachedExpression<TSource, TArg1, TResult>(this IQueryable<TSource> source, Func<IQueryable<TSource>, TArg1, TResult> f, TArg1 a1) {
			CachedQuery<TSource> q;
			lock(Cache<TSource>.cache) {
				if(!Cache<TSource>.GetCached(f, out q))
					f(q, default(TArg1));
			}
			return (TResult)q.Process(source, a1);
		}
		public static TResult CachedExpression<TSource, TArg1, TArg2, TResult>(this IQueryable<TSource> source, Func<IQueryable<TSource>, TArg1, TArg2, TResult> f, TArg1 a1, TArg2 a2) {
			CachedQuery<TSource> q;
			lock(Cache<TSource>.cache) {
				if(!Cache<TSource>.GetCached(f, out q))
					f(q, default(TArg1), default(TArg2));
			}
			return (TResult)q.Process(source, a1, a2);
		}
		public static TResult CachedExpression<TSource, TArg1, TArg2, TArg3, TResult>(this IQueryable<TSource> source, Func<IQueryable<TSource>, TArg1, TArg2, TArg3, TResult> f, TArg1 a1, TArg2 a2, TArg3 a3) {
			CachedQuery<TSource> q;
			lock(Cache<TSource>.cache) {
				if(!Cache<TSource>.GetCached(f, out q))
					f(q, default(TArg1), default(TArg2), default(TArg3));
			}
			return (TResult)q.Process(source, a1, a2, a3);
		}
		public static TResult CachedExpression<TSource, TArg1, TArg2, TArg3, TArg4, TResult>(this IQueryable<TSource> source, Func<IQueryable<TSource>, TArg1, TArg2, TArg3, TArg4, TResult> f, TArg1 a1, TArg2 a2, TArg3 a3, TArg4 a4) {
			CachedQuery<TSource> q;
			lock(Cache<TSource>.cache) {
				if(!Cache<TSource>.GetCached(f, out q))
					f(q, default(TArg1), default(TArg2), default(TArg3), default(TArg4));
			}
			return (TResult)q.Process(source, a1, a2, a3, a4);
		}
		public static TResult CachedExpression<TSource, TArg1, TArg2, TArg3, TArg4, TArg5, TResult>(this IQueryable<TSource> source, Func<IQueryable<TSource>, TArg1, TArg2, TArg3, TArg4, TArg5, TResult> f, TArg1 a1, TArg2 a2, TArg3 a3, TArg4 a4, TArg5 a5) {
			CachedQuery<TSource> q;
			lock(Cache<TSource>.cache) {
				if(!Cache<TSource>.GetCached(f, out q))
					f(q, default(TArg1), default(TArg2), default(TArg3), default(TArg4), default(TArg5));
			}
			return (TResult)q.Process(source, a1, a2, a3, a4, a5);
		}
		public static async Task<List<T>> ToListAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default(CancellationToken)) {
			return (await query.EnumerateAsync(cancellationToken).ConfigureAwait(false)).ToList();
		}
		public static async Task<T[]> ToArrayAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default(CancellationToken)) {
			return (await query.EnumerateAsync(cancellationToken).ConfigureAwait(false)).ToArray();
		}
		public static async Task<Dictionary<TKey, T>> ToDictionaryAsync<T, TKey>(this IQueryable<T> query, Func<T, TKey> keySelector, CancellationToken cancellationToken = default(CancellationToken)) {
			return (await query.EnumerateAsync(cancellationToken)).ToDictionary(keySelector);
		}
		public static async Task<Dictionary<TKey, TElement>> ToDictionaryAsync<T, TKey, TElement>(this IQueryable<T> query, Func<T, TKey> keySelector, Func<T, TElement> elementSelector, IEqualityComparer<TKey> equalityComparer, CancellationToken cancellationToken = default(CancellationToken)) {
			return (await query.EnumerateAsync(cancellationToken)).ToDictionary(keySelector, elementSelector, equalityComparer);
		}
		public static async Task<Dictionary<TKey, TElement>> ToDictionaryAsync<T, TKey, TElement>(this IQueryable<T> query, Func<T, TKey> keySelector, Func<T, TElement> elementSelector, CancellationToken cancellationToken = default(CancellationToken)) {
			return (await query.EnumerateAsync(cancellationToken)).ToDictionary(keySelector, elementSelector);
		}
		public static async Task<Dictionary<TKey, T>> ToDictionaryAsync<T, TKey>(this IQueryable<T> query, Func<T, TKey> keySelector, IEqualityComparer<TKey> equalityComparer, CancellationToken cancellationToken = default(CancellationToken)) {
			return (await query.EnumerateAsync(cancellationToken)).ToDictionary(keySelector, equalityComparer);
		}
		public static async Task<ILookup<TKey, T>> ToLookupAsync<T, TKey>(this IQueryable<T> query, Func<T, TKey> keySelector, CancellationToken cancellationToken = default(CancellationToken)) {
			return (await query.EnumerateAsync(cancellationToken)).ToLookup(keySelector);
		}
		public static async Task<ILookup<TKey, TElement>> ToLookupAsync<T, TKey, TElement>(this IQueryable<T> query, Func<T, TKey> keySelector, Func<T, TElement> elementSelector, CancellationToken cancellationToken = default(CancellationToken)) {
			return (await query.EnumerateAsync(cancellationToken)).ToLookup(keySelector, elementSelector);
		}
		public static async Task<ILookup<TKey, TElement>> ToLookupAsync<T, TKey, TElement>(this IQueryable<T> query, Func<T, TKey> keySelector, Func<T, TElement> elementSelector, IEqualityComparer<TKey> equalityComparer, CancellationToken cancellationToken = default(CancellationToken)) {
			return (await query.EnumerateAsync(cancellationToken)).ToLookup(keySelector, elementSelector, equalityComparer);
		}
		public static async Task<ILookup<TKey, T>> ToLookupAsync<T, TKey>(this IQueryable<T> query, Func<T, TKey> keySelector, IEqualityComparer<TKey> equalityComparer, CancellationToken cancellationToken = default(CancellationToken)) {
			return (await query.EnumerateAsync(cancellationToken)).ToLookup(keySelector, equalityComparer);
		}
		public static Task<T> ElementAtAsync<T>(this IQueryable<T> query, int index, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miElementAt == null) {
				miElementAt = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.ElementAt) && m.GetParameters().Length == 2);
			}
			return ExecuteWithTopLevelFunctionAsync<T, T>(query, miElementAt, Expression.Constant(index), cancellationToken);
		}
		public static Task<T> ElementAtOrDefaultAsync<T>(this IQueryable<T> query, int index, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miElementAtOrDefault == null) {
				miElementAtOrDefault = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.ElementAtOrDefault) && m.GetParameters().Length == 2);
			}
			return ExecuteWithTopLevelFunctionAsync<T, T>(query, miElementAtOrDefault, Expression.Constant(index), cancellationToken);
		}
		public static Task<int> CountAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miCount == null) {
				miCount = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Count) && m.GetParameters().Length == 1);
			}
			return ExecuteWithTopLevelFunctionAsync<T, int>(query, miCount, cancellationToken);
		}
		public static Task<int> CountAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miCountWithPredicate == null) {
				miCountWithPredicate = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Count) && m.GetParameters().Length == 2);
			}
			return ExecuteWithTopLevelFunctionAsync<T, int>(query, miCountWithPredicate, predicate, cancellationToken);
		}
		public static Task<long> LongCountAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miLongCount == null) {
				miLongCount = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.LongCount) && m.GetParameters().Length == 1);
			}
			return ExecuteWithTopLevelFunctionAsync<T, long>(query, miLongCount, cancellationToken);
		}
		public static Task<long> LongCountAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miLongCountWithPredicate == null) {
				miLongCountWithPredicate = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.LongCount) && m.GetParameters().Length == 2);
			}
			return ExecuteWithTopLevelFunctionAsync<T, long>(query, miLongCountWithPredicate, predicate, cancellationToken);
		}
		public static Task<bool> ContainsAsync<T>(this IQueryable<T> query, T item, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miContains == null) {
				miContains = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Contains) && m.GetParameters().Length == 2);
			}
			return ExecuteWithTopLevelFunctionAsync<T, bool>(query, miContains, Expression.Constant(item, typeof(T)), cancellationToken);
		}
		public static Task<bool> AnyAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miAny == null) {
				miAny = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Any) && m.GetParameters().Length == 1);
			}
			return ExecuteWithTopLevelFunctionAsync<T, bool>(query, miAny, cancellationToken);
		}
		public static Task<bool> AnyAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miAnyWithPredicate == null) {
				miAnyWithPredicate = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Any) && m.GetParameters().Length == 2);
			}
			return ExecuteWithTopLevelFunctionAsync<T, bool>(query, miAnyWithPredicate, predicate, cancellationToken);
		}
		public static Task<bool> AllAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miAllWithPredicate == null) {
				miAllWithPredicate = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.All) && m.GetParameters().Length == 2);
			}
			return ExecuteWithTopLevelFunctionAsync<T, bool>(query, miAllWithPredicate, predicate, cancellationToken);
		}
		public static Task<T> FirstAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miFirst == null) {
				miFirst = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.First) && m.GetParameters().Length == 1);
			}
			return ExecuteWithTopLevelFunctionAsync<T, T>(query, miFirst, cancellationToken);
		}
		public static Task<T> FirstAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miFirstWithPredicate == null) {
				miFirstWithPredicate = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.First) && m.GetParameters().Length == 2);
			}
			return ExecuteWithTopLevelFunctionAsync<T, T>(query, miFirstWithPredicate, predicate, cancellationToken);
		}
		public static Task<T> FirstOrDefaultAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miFirstOrDefault == null) {
				miFirstOrDefault = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.FirstOrDefault) && m.GetParameters().Length == 1);
			}
			return ExecuteWithTopLevelFunctionAsync<T, T>(query, miFirstOrDefault, cancellationToken);
		}
		public static Task<T> FirstOrDefaultAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miFirstOrDefaultWithPredicate == null) {
				miFirstOrDefaultWithPredicate = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.FirstOrDefault) && m.GetParameters().Length == 2 && typeof(LambdaExpression).IsAssignableFrom(m.GetParameters()[1].ParameterType));
			}
			return ExecuteWithTopLevelFunctionAsync<T, T>(query, miFirstOrDefaultWithPredicate, predicate, cancellationToken);
		}
		public static Task<T> LastAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miLast == null) {
				miLast = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Last) && m.GetParameters().Length == 1);
			}
			return ExecuteWithTopLevelFunctionAsync<T, T>(query, miLast, cancellationToken);
		}
		public static Task<T> LastAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miLastWithPredicate == null) {
				miLastWithPredicate = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Last) && m.GetParameters().Length == 2);
			}
			return ExecuteWithTopLevelFunctionAsync<T, T>(query, miLastWithPredicate, predicate, cancellationToken);
		}
		public static Task<T> LastOrDefaultAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miLastOrDefault == null) {
				miLastOrDefault = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.LastOrDefault) && m.GetParameters().Length == 1);
			}
			return ExecuteWithTopLevelFunctionAsync<T, T>(query, miLastOrDefault, cancellationToken);
		}
		public static Task<T> LastOrDefaultAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miLastOrDefaultWithPredicate == null) {
				miLastOrDefaultWithPredicate = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.LastOrDefault) && m.GetParameters().Length == 2 && typeof(LambdaExpression).IsAssignableFrom(m.GetParameters()[1].ParameterType));
			}
			return ExecuteWithTopLevelFunctionAsync<T, T>(query, miLastOrDefaultWithPredicate, predicate, cancellationToken);
		}
		public static Task<T> SingleAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miSingle == null) {
				miSingle = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Single) && m.GetParameters().Length == 1);
			}
			return ExecuteWithTopLevelFunctionAsync<T, T>(query, miSingle, cancellationToken);
		}
		public static Task<T> SingleAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miSingleWithPredicate == null) {
				miSingleWithPredicate = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Single) && m.GetParameters().Length == 2);
			}
			return ExecuteWithTopLevelFunctionAsync<T, T>(query, miSingleWithPredicate, predicate, cancellationToken);
		}
		public static Task<T> SingleOrDefaultAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miSingleOrDefault == null) {
				miSingleOrDefault = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.SingleOrDefault) && m.GetParameters().Length == 1);
			}
			return ExecuteWithTopLevelFunctionAsync<T, T>(query, miSingleOrDefault, cancellationToken);
		}
		public static Task<T> SingleOrDefaultAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miSingleOrDefaultWithPredicate == null) {
				miSingleOrDefaultWithPredicate = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.SingleOrDefault) && m.GetParameters().Length == 2 && typeof(LambdaExpression).IsAssignableFrom(m.GetParameters()[1].ParameterType));
			}
			return ExecuteWithTopLevelFunctionAsync<T, T>(query, miSingleOrDefaultWithPredicate, predicate, cancellationToken);
		}
		public static Task<T> MinAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miMin == null) {
				miMin = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Min) && m.GetParameters().Length == 1);
			}
			return ExecuteWithTopLevelFunctionAsync<T, T>(query, miMin, cancellationToken);
		}
		public static Task<TResult> MinAsync<T, TResult>(this IQueryable<T> query, Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miMinWithSelector == null) {
				miMinWithSelector = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Min) && m.GetParameters().Length == 2 && typeof(LambdaExpression).IsAssignableFrom(m.GetParameters()[1].ParameterType));
			}
			return ExecuteWithTopLevelFunctionAsync<T, TResult>(query, miMinWithSelector, selector, cancellationToken);
		}
		public static Task<T> MaxAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miMax == null) {
				miMax = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Max) && m.GetParameters().Length == 1);
			}
			return ExecuteWithTopLevelFunctionAsync<T, T>(query, miMax, cancellationToken);
		}
		public static Task<TResult> MaxAsync<T, TResult>(this IQueryable<T> query, Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miMaxWithSelector == null) {
				miMaxWithSelector = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Max) && m.GetParameters().Length == 2 && typeof(LambdaExpression).IsAssignableFrom(m.GetParameters()[1].ParameterType));
			}
			return ExecuteWithTopLevelFunctionAsync<T, TResult>(query, miMaxWithSelector, selector, cancellationToken);
		}
		public static Task<int> SumAsync(this IQueryable<int> query, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miSumInt == null) {
				miSumInt = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Sum) && m.ReturnType == typeof(int) && m.GetParameters().Length == 1);
			}
			return ExecuteWithTopLevelFunctionAsync<int, int>(query, miSumInt, cancellationToken);
		}
		public static Task<int> SumAsync<T>(this IQueryable<T> query, Expression<Func<T, int>> selector, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miSumIntWithSelector == null) {
				miSumIntWithSelector = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Sum) && m.ReturnType == typeof(int) && m.GetParameters().Length == 2);
			}
			return ExecuteWithTopLevelFunctionAsync<T, int>(query, miSumIntWithSelector, selector, cancellationToken);
		}
		public static Task<int?> SumAsync(this IQueryable<int?> query, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miSumNullableInt == null) {
				miSumNullableInt = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Sum) && m.ReturnType == typeof(int?) && m.GetParameters().Length == 1);
			}
			return ExecuteWithTopLevelFunctionAsync<int?, int?>(query, miSumNullableInt, cancellationToken);
		}
		public static Task<int?> SumAsync<T>(this IQueryable<T> query, Expression<Func<T, int?>> selector, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miSumNullableIntWithSelector == null) {
				miSumNullableIntWithSelector = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Sum) && m.ReturnType == typeof(int?) && m.GetParameters().Length == 2);
			}
			return ExecuteWithTopLevelFunctionAsync<T, int?>(query, miSumNullableIntWithSelector, selector, cancellationToken);
		}
		public static Task<long> SumAsync(this IQueryable<long> query, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miSumLong == null) {
				miSumLong = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Sum) && m.ReturnType == typeof(long) && m.GetParameters().Length == 1);
			}
			return ExecuteWithTopLevelFunctionAsync<long, long>(query, miSumLong, cancellationToken);
		}
		public static Task<long> SumAsync<T>(this IQueryable<T> query, Expression<Func<T, long>> selector, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miSumLongWithSelector == null) {
				miSumLongWithSelector = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Sum) && m.ReturnType == typeof(long) && m.GetParameters().Length == 2);
			}
			return ExecuteWithTopLevelFunctionAsync<T, long>(query, miSumLongWithSelector, selector, cancellationToken);
		}
		public static Task<long?> SumAsync(this IQueryable<long?> query, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miSumNullableLong == null) {
				miSumNullableLong = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Sum) && m.ReturnType == typeof(long?) && m.GetParameters().Length == 1);
			}
			return ExecuteWithTopLevelFunctionAsync<long?, long?>(query, miSumNullableLong, cancellationToken);
		}
		public static Task<long?> SumAsync<T>(this IQueryable<T> query, Expression<Func<T, long?>> selector, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miSumNullableLongWithSelector == null) {
				miSumNullableLongWithSelector = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Sum) && m.ReturnType == typeof(long?) && m.GetParameters().Length == 2);
			}
			return ExecuteWithTopLevelFunctionAsync<T, long?>(query, miSumNullableLongWithSelector, selector, cancellationToken);
		}
		public static Task<decimal> SumAsync(this IQueryable<decimal> query, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miSumDecimal == null) {
				miSumDecimal = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Sum) && m.ReturnType == typeof(decimal) && m.GetParameters().Length == 1);
			}
			return ExecuteWithTopLevelFunctionAsync<decimal, decimal>(query, miSumDecimal, cancellationToken);
		}
		public static Task<decimal> SumAsync<T>(this IQueryable<T> query, Expression<Func<T, decimal>> selector, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miSumDecimalWithSelector == null) {
				miSumDecimalWithSelector = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Sum) && m.ReturnType == typeof(decimal) && m.GetParameters().Length == 2);
			}
			return ExecuteWithTopLevelFunctionAsync<T, decimal>(query, miSumDecimalWithSelector, selector, cancellationToken);
		}
		public static Task<decimal?> SumAsync(this IQueryable<decimal?> query, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miSumNullableDecimal == null) {
				miSumNullableDecimal = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Sum) && m.ReturnType == typeof(decimal?) && m.GetParameters().Length == 1);
			}
			return ExecuteWithTopLevelFunctionAsync<decimal?, decimal?>(query, miSumNullableDecimal, cancellationToken);
		}
		public static Task<decimal?> SumAsync<T>(this IQueryable<T> query, Expression<Func<T, decimal?>> selector, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miSumNullableDecimalWithSelector == null) {
				miSumNullableDecimalWithSelector = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Sum) && m.ReturnType == typeof(decimal?) && m.GetParameters().Length == 2);
			}
			return ExecuteWithTopLevelFunctionAsync<T, decimal?>(query, miSumNullableDecimalWithSelector, selector, cancellationToken);
		}
		public static Task<float> SumAsync(this IQueryable<float> query, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miSumFloat == null) {
				miSumFloat = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Sum) && m.ReturnType == typeof(float) && m.GetParameters().Length == 1);
			}
			return ExecuteWithTopLevelFunctionAsync<float, float>(query, miSumFloat, cancellationToken);
		}
		public static Task<float> SumAsync<T>(this IQueryable<T> query, Expression<Func<T, float>> selector, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miSumFloatWithSelector == null) {
				miSumFloatWithSelector = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Sum) && m.ReturnType == typeof(float) && m.GetParameters().Length == 2);
			}
			return ExecuteWithTopLevelFunctionAsync<T, float>(query, miSumFloatWithSelector, selector, cancellationToken);
		}
		public static Task<float?> SumAsync(this IQueryable<float?> query, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miSumNullableFloat == null) {
				miSumNullableFloat = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Sum) && m.ReturnType == typeof(float?) && m.GetParameters().Length == 1);
			}
			return ExecuteWithTopLevelFunctionAsync<float?, float?>(query, miSumNullableFloat, cancellationToken);
		}
		public static Task<float?> SumAsync<T>(this IQueryable<T> query, Expression<Func<T, float?>> selector, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miSumNullableFloatWithSelector == null) {
				miSumNullableFloatWithSelector = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Sum) && m.ReturnType == typeof(float?) && m.GetParameters().Length == 2);
			}
			return ExecuteWithTopLevelFunctionAsync<T, float?>(query, miSumNullableFloatWithSelector, selector, cancellationToken);
		}
		public static Task<double> SumAsync(this IQueryable<double> query, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miSumDouble == null) {
				miSumDouble = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Sum) && m.ReturnType == typeof(double) && m.GetParameters().Length == 1);
			}
			return ExecuteWithTopLevelFunctionAsync<double, double>(query, miSumDouble, cancellationToken);
		}
		public static Task<double> SumAsync<T>(this IQueryable<T> query, Expression<Func<T, double>> selector, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miSumDoubleWithSelector == null) {
				miSumDoubleWithSelector = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Sum) && m.ReturnType == typeof(double) && m.GetParameters().Length == 2);
			}
			return ExecuteWithTopLevelFunctionAsync<T, double>(query, miSumDoubleWithSelector, selector, cancellationToken);
		}
		public static Task<double?> SumAsync(this IQueryable<double?> query, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miSumNullableDouble == null) {
				miSumNullableDouble = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Sum) && m.ReturnType == typeof(double?) && m.GetParameters().Length == 1);
			}
			return ExecuteWithTopLevelFunctionAsync<double?, double?>(query, miSumNullableDouble, cancellationToken);
		}
		public static Task<double?> SumAsync<T>(this IQueryable<T> query, Expression<Func<T, double?>> selector, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miSumNullableDoubleWithSelector == null) {
				miSumNullableDoubleWithSelector = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Sum) && m.ReturnType == typeof(double?) && m.GetParameters().Length == 2);
			}
			return ExecuteWithTopLevelFunctionAsync<T, double?>(query, miSumNullableDoubleWithSelector, selector, cancellationToken);
		}
		public static Task<double> AverageAsync(this IQueryable<int> query, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miAverageInt == null) {
				miAverageInt = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Average) && m.ReturnType == typeof(double)
				&& m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(IQueryable<int>));
			}
			return ExecuteWithTopLevelFunctionAsync<int, double>(query, miAverageInt, cancellationToken);
		}
		public static Task<double> AverageAsync<T>(this IQueryable<T> query, Expression<Func<T, int>> selector, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miAverageIntWithSelector == null) {
				miAverageIntWithSelector = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Average) && m.ReturnType == typeof(double)
				&& m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType.GenericTypeArguments[0].GenericTypeArguments[1] == typeof(int));
			}
			return ExecuteWithTopLevelFunctionAsync<T, double>(query, miAverageIntWithSelector, selector, cancellationToken);
		}
		public static Task<double> AverageAsync(this IQueryable<long> query, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miAverageLong == null) {
				miAverageLong = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Average) && m.ReturnType == typeof(double)
				&& m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(IQueryable<long>));
			}
			return ExecuteWithTopLevelFunctionAsync<long, double>(query, miAverageLong, cancellationToken);
		}
		public static Task<double> AverageAsync<T>(this IQueryable<T> query, Expression<Func<T, long>> selector, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miAverageLongWithSelector == null) {
				miAverageLongWithSelector = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Average) && m.ReturnType == typeof(double)
					&& m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType.GenericTypeArguments[0].GenericTypeArguments[1] == typeof(long));
			}
			return ExecuteWithTopLevelFunctionAsync<T, double>(query, miAverageLongWithSelector, selector, cancellationToken);
		}
		public static Task<decimal> AverageAsync(this IQueryable<decimal> query, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miAverageDecimal == null) {
				miAverageDecimal = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Average) && m.ReturnType == typeof(decimal)
				&& m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(IQueryable<decimal>));
			}
			return ExecuteWithTopLevelFunctionAsync<decimal, decimal>(query, miAverageDecimal, cancellationToken);
		}
		public static Task<decimal> AverageAsync<T>(this IQueryable<T> query, Expression<Func<T, decimal>> selector, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miAverageDecimalWithSelector == null) {
				miAverageDecimalWithSelector = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Average) && m.ReturnType == typeof(decimal)
				&& m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType.GenericTypeArguments[0].GenericTypeArguments[1] == typeof(decimal));
			}
			return ExecuteWithTopLevelFunctionAsync<T, decimal>(query, miAverageDecimalWithSelector, selector, cancellationToken);
		}
		public static Task<float> AverageAsync(this IQueryable<float> query, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miAverageFloat == null) {
				miAverageFloat = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Average) && m.ReturnType == typeof(float)
				&& m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(IQueryable<float>));
			}
			return ExecuteWithTopLevelFunctionAsync<float, float>(query, miAverageFloat, cancellationToken);
		}
		public static Task<float> AverageAsync<T>(this IQueryable<T> query, Expression<Func<T, float>> selector, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miAverageFloatWithSelector == null) {
				miAverageFloatWithSelector = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Average) && m.ReturnType == typeof(float)
				&& m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType.GenericTypeArguments[0].GenericTypeArguments[1] == typeof(float));
			}
			return ExecuteWithTopLevelFunctionAsync<T, float>(query, miAverageFloatWithSelector, selector, cancellationToken);
		}
		public static Task<double> AverageAsync(this IQueryable<double> query, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miAverageDouble == null) {
				miAverageDouble = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Average) && m.ReturnType == typeof(double)
				&& m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(IQueryable<double>));
			}
			return ExecuteWithTopLevelFunctionAsync<double, double>(query, miAverageDouble, cancellationToken);
		}
		public static Task<double> AverageAsync<T>(this IQueryable<T> query, Expression<Func<T, double>> selector, CancellationToken cancellationToken = default(CancellationToken)) {
			if(miAverageDoubleWithSelector == null) {
				miAverageDoubleWithSelector = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Average) && m.ReturnType == typeof(double)
				&& m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType.GenericTypeArguments[0].GenericTypeArguments[1] == typeof(double));
			}
			return ExecuteWithTopLevelFunctionAsync<T, double>(query, miAverageDoubleWithSelector, selector, cancellationToken);
		}
		static MethodInfo miCount = null;
		static MethodInfo miCountWithPredicate = null;
		static MethodInfo miLongCount = null;
		static MethodInfo miLongCountWithPredicate = null;
		static MethodInfo miContains = null;
		static MethodInfo miElementAt = null;
		static MethodInfo miElementAtOrDefault = null;
		static MethodInfo miAny = null;
		static MethodInfo miAnyWithPredicate = null;
		static MethodInfo miAllWithPredicate = null;
		static MethodInfo miFirst = null;
		static MethodInfo miFirstWithPredicate = null;
		static MethodInfo miFirstOrDefault = null;
		static MethodInfo miFirstOrDefaultWithPredicate = null;
		static MethodInfo miLast = null;
		static MethodInfo miLastWithPredicate = null;
		static MethodInfo miLastOrDefault = null;
		static MethodInfo miLastOrDefaultWithPredicate = null;
		static MethodInfo miSingle = null;
		static MethodInfo miSingleWithPredicate = null;
		static MethodInfo miSingleOrDefault = null;
		static MethodInfo miSingleOrDefaultWithPredicate = null;
		static MethodInfo miMin = null;
		static MethodInfo miMinWithSelector = null;
		static MethodInfo miMax = null;
		static MethodInfo miMaxWithSelector = null;
		static MethodInfo miSumDecimal = null;
		static MethodInfo miSumNullableDecimal = null;
		static MethodInfo miSumDecimalWithSelector = null;
		static MethodInfo miSumNullableDecimalWithSelector = null;
		static MethodInfo miSumInt = null;
		static MethodInfo miSumNullableInt = null;
		static MethodInfo miSumIntWithSelector = null;
		static MethodInfo miSumNullableIntWithSelector = null;
		static MethodInfo miSumLong = null;
		static MethodInfo miSumNullableLong = null;
		static MethodInfo miSumLongWithSelector = null;
		static MethodInfo miSumNullableLongWithSelector = null;
		static MethodInfo miSumFloat = null;
		static MethodInfo miSumNullableFloat = null;
		static MethodInfo miSumFloatWithSelector = null;
		static MethodInfo miSumNullableFloatWithSelector = null;
		static MethodInfo miSumDouble = null;
		static MethodInfo miSumNullableDouble = null;
		static MethodInfo miSumDoubleWithSelector = null;
		static MethodInfo miSumNullableDoubleWithSelector = null;
		static MethodInfo miAverageDecimal = null;
		static MethodInfo miAverageDecimalWithSelector = null;
		static MethodInfo miAverageInt = null;
		static MethodInfo miAverageIntWithSelector = null;
		static MethodInfo miAverageLong = null;
		static MethodInfo miAverageLongWithSelector = null;
		static MethodInfo miAverageFloat = null;
		static MethodInfo miAverageFloatWithSelector = null;
		static MethodInfo miAverageDouble = null;
		static MethodInfo miAverageDoubleWithSelector = null;
		static Task<TResult> ExecuteWithTopLevelFunctionAsync<T, TResult>(IQueryable<T> query, MethodInfo topLevelMethodInfo, CancellationToken cancellationToken = default(CancellationToken)) {
			ThrowIfNotXPQuery(query);
			if(topLevelMethodInfo.IsGenericMethod) {
				topLevelMethodInfo = topLevelMethodInfo.MakeGenericMethod(typeof(T));
			}
			MethodCallExpression call = Expression.Call(null, topLevelMethodInfo, query.Expression);
			return ExecuteAsync<TResult>((XPQueryBase)query, call, cancellationToken);
		}
		static Task<TResult> ExecuteWithTopLevelFunctionAsync<T, TResult>(IQueryable<T> query, MethodInfo topLevelMethodInfo, LambdaExpression expression, CancellationToken cancellationToken = default(CancellationToken)) {
			return ExecuteWithTopLevelFunctionAsync<T, TResult>(query, topLevelMethodInfo, Expression.Quote(expression), cancellationToken);
		}
		static Task<TResult> ExecuteWithTopLevelFunctionAsync<T, TResult>(IQueryable<T> query, MethodInfo topLevelMethodInfo, LambdaExpression[] expressions, CancellationToken cancellationToken = default(CancellationToken)) {
			Expression[] exprs = null;
			if(expressions != null) {
				exprs = new Expression[expressions.Length];
				for(int i = 0; i < expressions.Length; i++) {
					exprs[i] = Expression.Quote(expressions[i]);
				}
			}
			return ExecuteWithTopLevelFunctionAsync<T, TResult>(query, topLevelMethodInfo, exprs, cancellationToken);
		}
		static Task<TResult> ExecuteWithTopLevelFunctionAsync<T, TResult>(IQueryable<T> query, MethodInfo topLevelMethodInfo, Expression argument, CancellationToken cancellationToken = default(CancellationToken)) {
			return ExecuteWithTopLevelFunctionAsync<T, TResult>(query, topLevelMethodInfo, new Expression[] { argument }, cancellationToken);
		}
		static Task<TResult> ExecuteWithTopLevelFunctionAsync<T, TResult>(IQueryable<T> query, MethodInfo topLevelMethodInfo, Expression[] arguments, CancellationToken cancellationToken = default(CancellationToken)) {
			ThrowIfNotXPQuery(query);
			if(topLevelMethodInfo.IsGenericMethod) {
				if(topLevelMethodInfo.GetGenericArguments().Length == 1) {
					topLevelMethodInfo = topLevelMethodInfo.MakeGenericMethod(typeof(T));
				}
				else {
					topLevelMethodInfo = topLevelMethodInfo.MakeGenericMethod(typeof(T), typeof(TResult));
				}
			}
			Expression[] callArguments = CombineExpressions(query.Expression, arguments);
			MethodCallExpression call = Expression.Call(null, topLevelMethodInfo, callArguments);
			return ExecuteAsync<TResult>((XPQueryBase)query, call, cancellationToken);
		}
		static TResult ExecuteWithTopLevelFunction<T, TResult>(IQueryable<T> query, MethodInfo topLevelMethodInfo, LambdaExpression[] arguments) {
			ThrowIfNotXPQuery(query);
			if(topLevelMethodInfo.IsGenericMethod) {
				topLevelMethodInfo = topLevelMethodInfo.MakeGenericMethod(typeof(T));
			}
			Expression[] callArguments = CombineExpressions(query.Expression, arguments);
			MethodCallExpression call = Expression.Call(null, topLevelMethodInfo, callArguments);
			return ((IQueryProvider)query).Execute<TResult>(call);
		}
		static Expression[] CombineExpressions(Expression firstArgument, Expression[] otherArguments) {
			Expression[] callArguments;
			if(firstArgument != null) {
				callArguments = new Expression[1 + otherArguments.Length];
				for(int i = 0; i < otherArguments.Length; i++) {
					callArguments[i + 1] = otherArguments[i];
				}
			}
			else {
				callArguments = new Expression[1];
			}
			callArguments[0] = firstArgument;
			return callArguments;
		}
		static async Task<TResult> ExecuteAsync<TResult>(XPQueryBase query, Expression expression, CancellationToken cancellationToken = default(CancellationToken)) {
			object res = await query.ExecuteAsync(expression, cancellationToken).ConfigureAwait(false);
			return res == null ? default(TResult) : (TResult)res;
		}
		public static object CustomAggregate<T>(this IQueryable<T> query, string customAggregateName, params Expression<Func<T, object>>[] arguments) {
			ThrowIfNotXPQuery(query);
			ICustomAggregate agg = ((XPQueryBase)query).GetCustomAggregate(customAggregateName);
			if(agg == null) {
				throw new InvalidOperationException(Res.GetString(Res.CustomAggregate_NotFound, customAggregateName));
			}
			ICustomAggregateQueryable aggQuaryable = agg as ICustomAggregateQueryable;
			if(aggQuaryable == null) {
				throw new InvalidOperationException(Res.GetString(Res.CustomAggregate_DoesNotImplementInterface, customAggregateName, typeof(ICustomAggregateQueryable)));
			}
			MethodInfo methodInfo = aggQuaryable.GetMethodInfo();
			return ExecuteWithTopLevelFunction<T, object>(query, methodInfo, arguments);
		}
		public static Task<object> CustomAggregateAsync<T>(this IQueryable<T> query, string customAggregateName, params Expression<Func<T, object>>[] arguments) {
			return CustomAggregateAsync(query, customAggregateName, arguments, CancellationToken.None);
		}
		public static Task<object> CustomAggregateAsync<T>(this IQueryable<T> query, string customAggregateName, Expression<Func<T, object>> argument, CancellationToken cancellationToken = default(CancellationToken)) {
			return CustomAggregateAsync(query, customAggregateName, new Expression<Func<T, object>>[] { argument }, cancellationToken);
		}
		public static Task<object> CustomAggregateAsync<T>(this IQueryable<T> query, string customAggregateName, Expression<Func<T, object>>[] arguments, CancellationToken cancellationToken) {
			ThrowIfNotXPQuery(query);
			ICustomAggregate agg = ((XPQueryBase)query).GetCustomAggregate(customAggregateName);
			if(agg == null) {
				throw new InvalidOperationException(Res.GetString(Res.CustomAggregate_NotFound, customAggregateName));
			}
			ICustomAggregateQueryable aggQuaryable = agg as ICustomAggregateQueryable;
			if(aggQuaryable == null) {
				throw new InvalidOperationException(Res.GetString(Res.CustomAggregate_DoesNotImplementInterface, customAggregateName, typeof(ICustomAggregateQueryable)));
			}
			MethodInfo methodInfo = aggQuaryable.GetMethodInfo();
			return ExecuteWithTopLevelFunctionAsync<T, object>(query, methodInfo, arguments, cancellationToken);
		}
	}
	public interface ICustomFunctionOperatorQueryable {
		MethodInfo GetMethodInfo();
	}
	public interface ICustomAggregateQueryable {
		MethodInfo GetMethodInfo();
	}
	public interface ICustomNonDeterministicFunctionOperatorQueryable : ICustomFunctionOperatorQueryable {
	}
	public interface ICustomCriteriaOperatorQueryable {
		CriteriaOperator GetCriteria(params CriteriaOperator[] operands);
		MethodInfo GetMethodInfo();
	}
	public class CustomCriteriaCollection : CustomDictionaryCollection<MethodInfo, ICustomCriteriaOperatorQueryable> {
		public CustomCriteriaCollection() : base() { }
		protected override MethodInfo GetKey(ICustomCriteriaOperatorQueryable item) {
			return item.GetMethodInfo();
		}
	}
	public static class CustomCriteriaManager {
		static CustomCriteriaCollection registeredCustomCriteria = new CustomCriteriaCollection();
		public static void RegisterCriterion(ICustomCriteriaOperatorQueryable customCriterion) {
			lock(registeredCustomCriteria) {
				registeredCustomCriteria.Add(customCriterion);
			}
		}
		public static void RegisterCriteria(IEnumerable<ICustomCriteriaOperatorQueryable> customCriteria) {
			lock(registeredCustomCriteria) {
				registeredCustomCriteria.Add(customCriteria);
			}
		}
		public static bool UnregisterCriterion(ICustomCriteriaOperatorQueryable customCriterion) {
			lock(registeredCustomCriteria) {
				return registeredCustomCriteria.Remove(customCriterion);
			}
		}
		public static bool UnregisterCriterion(MethodInfo methodInfo) {
			lock(registeredCustomCriteria) {
				ICustomCriteriaOperatorQueryable customCriterion = registeredCustomCriteria.GetItem(methodInfo);
				if(customCriterion == null) return false;
				return registeredCustomCriteria.Remove(customCriterion);
			}
		}
		[Description("Returns custom criteria registered in an application via the CustomCriteriaManager.RegisterCriterion and CustomCriteriaManager.RegisterCriteria method calls.")]
		public static int RegisteredCriterionCount {
			get { return registeredCustomCriteria.Count; }
		}
		public static ICustomCriteriaOperatorQueryable GetCriteria(MethodInfo methodInfo) {
			return registeredCustomCriteria.GetItem(methodInfo);
		}
		public static CustomCriteriaCollection GetRegisteredCriteria() {
			CustomCriteriaCollection result = new CustomCriteriaCollection();
			lock(registeredCustomCriteria) {
				result.Add(registeredCustomCriteria);
			}
			return result;
		}
	}
}
