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
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Xml;
using DevExpress.Data.Filtering;
using DevExpress.Data.Filtering.Exceptions;
using DevExpress.Data.Filtering.Helpers;
using DevExpress.Data.Helpers;
using DevExpress.Data.Linq;
using DevExpress.Utils;
using DevExpress.Xpo.Exceptions;
namespace DevExpress.Xpo.Metadata.Helpers {
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Reflection;
	using System.Text;
	using DevExpress.Data.Utils;
	using DevExpress.Xpo.Helpers;
	interface ICriteriaTypeResolver {
		Type Resolve(CriteriaOperator criteria);
	}
#if NET
	public interface IQueryContextProvider {
		public ICollection GetObjects(Session session, XPClassInfo classInfo, CriteriaOperator condition, int topSelectedRecords);
	}
#endif
	public enum CriteriaTypeResolveKeyBehavior {
		AsIs,
		AlwaysKey,
		AlwaysReference
	}
	public class CriteriaTypeResolver : CriteriaTypeResolverBase, IClientCriteriaVisitor<CriteriaTypeResolverResult>, ICriteriaTypeResolver {
		readonly CriteriaTypeResolveKeyBehavior resolveKeyBehavior = CriteriaTypeResolveKeyBehavior.AlwaysKey;
		XPClassInfo[] upLevels;
		XPDictionary dictionary;
		CriteriaTypeResolverResult IClientCriteriaVisitor<CriteriaTypeResolverResult>.Visit(OperandProperty theOriginalOperand) {
			int level;
			bool addKeyTail;
			MemberInfoCollection path;
			OperandProperty theOperand = PersistentCriterionExpander.FixPropertyExclamation(theOriginalOperand, out addKeyTail);
			string propertyName = GetPropertyContext(theOperand.PropertyName, out level, out path);
			XPMemberInfo member = path[path.Count - 1];
			if(EvaluatorProperty.GetIsThisProperty(propertyName))
				return new CriteriaTypeResolverResult(upLevels[level].ClassType, upLevels[level]);
			if(member.IsKey && resolveKeyBehavior == CriteriaTypeResolveKeyBehavior.AlwaysReference) {
				if(path.Count == 1) return new CriteriaTypeResolverResult(upLevels[level].ClassType, upLevels[level]);
				member = path[path.Count - 2];
			}
			return member.ReferenceType == null ?
						new CriteriaTypeResolverResult(member.MemberType) :
							((member.ReferenceType.ClassType != null && member.ReferenceType.ClassType.IsInterface) || resolveKeyBehavior != CriteriaTypeResolveKeyBehavior.AlwaysKey ?
								new CriteriaTypeResolverResult(member.ReferenceType.ClassType, member.ReferenceType)
									: new CriteriaTypeResolverResult(member.ReferenceType.KeyProperty.MemberType, member.ReferenceType));
		}
		CriteriaTypeResolverResult IClientCriteriaVisitor<CriteriaTypeResolverResult>.Visit(AggregateOperand theOperand) {
			switch(theOperand.AggregateType) {
				case Aggregate.Exists:
					return new CriteriaTypeResolverResult(typeof(bool));
				case Aggregate.Count:
					return new CriteriaTypeResolverResult(typeof(int));
				default: {
					CriteriaTypeResolverResult result;
					if(!theOperand.IsTopLevel) {
						int level;
						MemberInfoCollection path;
						GetPropertyContext(theOperand.CollectionProperty.PropertyName, out level, out path);
						XPClassInfo collectionClassInfo = path[path.Count - 1].CollectionElementType;
						if(theOperand.AggregateType != Aggregate.Custom) {
							result = ResolveTypeInContext(collectionClassInfo, level, theOperand.AggregatedExpression, CriteriaTypeResolveKeyBehavior.AsIs);
						}
						else {
							Type[] argTypes = new Type[theOperand.CustomAggregateOperands.Count];
							for(int i = 0; i < theOperand.CustomAggregateOperands.Count; i++) {
								argTypes[i] = ResolveTypeInContext(collectionClassInfo, level, theOperand.CustomAggregateOperands[i], CriteriaTypeResolveKeyBehavior.AsIs).Type;
							}
							return new CriteriaTypeResolverResult(GetCustomAggregateType(theOperand.CustomAggregateName, argTypes));
						}
					}
					else {
						if(theOperand.AggregateType != Aggregate.Custom) {
							result = Process(theOperand.AggregatedExpression);
						}
						else {
							Type[] argTypes = new Type[theOperand.CustomAggregateOperands.Count];
							for(int i = 0; i < theOperand.CustomAggregateOperands.Count; i++) {
								argTypes[i] = Process(theOperand.CustomAggregateOperands[i]).Type;
							}
							return new CriteriaTypeResolverResult(GetCustomAggregateType(theOperand.CustomAggregateName, argTypes));
						}
					}
					if(theOperand.AggregateType == Aggregate.Avg) {
						Type uType = Nullable.GetUnderlyingType(result.Type);
						Type type = uType ?? result.Type;
						if(type != typeof(decimal) && type != typeof(Single)) {
							return new CriteriaTypeResolverResult(typeof(double), result.Tag);
						}
					}
					return result;
				}
			}
		}
		private string GetPropertyContext(string originalPropertyName, out int level, out MemberInfoCollection path) {
			level = 0;
			string propertyName = originalPropertyName;
			while(propertyName.StartsWith("^.")) {
				level++;
				propertyName = propertyName.Substring(2);
			}
			path = upLevels[level].ParsePath(propertyName);
			return propertyName;
		}
		CriteriaTypeResolverResult IClientCriteriaVisitor<CriteriaTypeResolverResult>.Visit(JoinOperand theOperand) {
			switch(theOperand.AggregateType) {
				case Aggregate.Exists:
					return new CriteriaTypeResolverResult(typeof(bool));
				case Aggregate.Count:
					return new CriteriaTypeResolverResult(typeof(int));
				default: {
					XPClassInfo joinedCi = null;
					if(!MemberInfoCollection.TryResolveTypeAlsoByShortName(theOperand.JoinTypeName, upLevels[0], out joinedCi)) {
						throw new CannotResolveClassInfoException(string.Empty, theOperand.JoinTypeName);
					}
					if(theOperand.AggregateType != Aggregate.Custom) {
						return ResolveTypeInContext(joinedCi, 0, theOperand.AggregatedExpression, CriteriaTypeResolveKeyBehavior.AsIs);
					}
					else {
						Type[] operandTypes = new Type[theOperand.CustomAggregateOperands.Count];
						for(int i = 0; i < theOperand.CustomAggregateOperands.Count; i++) {
							operandTypes[i] = ResolveTypeInContext(joinedCi, 0, theOperand.CustomAggregateOperands[i], CriteriaTypeResolveKeyBehavior.AsIs).Type;
						}
						return new CriteriaTypeResolverResult(GetCustomAggregateType(theOperand.CustomAggregateName, operandTypes));
					}
				}
			}
		}
		CriteriaTypeResolverResult ResolveTypeInContext(XPClassInfo joinedCi, int level, CriteriaOperator criteria, CriteriaTypeResolveKeyBehavior resolveKeyBehavior) {
			XPClassInfo[] newUpLevels = new XPClassInfo[upLevels.Length + 1];
			Array.Copy(upLevels, level, newUpLevels, 1, upLevels.Length - level);
			newUpLevels[0] = joinedCi;
			return ResolveTypeResult(newUpLevels, dictionary, criteria, resolveKeyBehavior);
		}
		public CriteriaTypeResolver(XPClassInfo info)
			: this(info, CriteriaTypeResolveKeyBehavior.AlwaysKey) {
		}
		public CriteriaTypeResolver(XPClassInfo info, CriteriaTypeResolveKeyBehavior resolveKeyBehavior)
			: this(new XPClassInfo[] { info }, info.Dictionary, resolveKeyBehavior) {
		}
		public CriteriaTypeResolver(XPClassInfo[] upLevels, XPDictionary dictionary, CriteriaTypeResolveKeyBehavior resolveKeyBehavior)
			: this(upLevels, dictionary) {
			this.resolveKeyBehavior = resolveKeyBehavior;
		}
		public CriteriaTypeResolver(XPClassInfo[] upLevels, XPDictionary dictionary) {
			this.upLevels = upLevels;
			this.dictionary = dictionary;
		}
		public Type Resolve(CriteriaOperator criteria) {
			return Process(criteria).Type;
		}
		static public Type ResolveType(XPClassInfo info, CriteriaOperator criteria) {
			return new CriteriaTypeResolver(info).Process(criteria).Type;
		}
		static public Type ResolveType(XPClassInfo[] upLevels, XPDictionary dictionary, CriteriaOperator criteria) {
			return new CriteriaTypeResolver(upLevels, dictionary).Process(criteria).Type;
		}
		static public Type ResolveType(XPClassInfo info, CriteriaOperator criteria, CriteriaTypeResolveKeyBehavior resolveKeyBehavior) {
			return new CriteriaTypeResolver(info, resolveKeyBehavior).Process(criteria).Type;
		}
		static public Type ResolveType(XPClassInfo[] upLevels, XPDictionary dictionary, CriteriaOperator criteria, CriteriaTypeResolveKeyBehavior resolveKeyBehavior) {
			return new CriteriaTypeResolver(upLevels, dictionary, resolveKeyBehavior).Process(criteria).Type;
		}
		static public CriteriaTypeResolverResult ResolveTypeResult(XPClassInfo info, CriteriaOperator criteria) {
			return new CriteriaTypeResolver(info).Process(criteria);
		}
		static public CriteriaTypeResolverResult ResolveTypeResult(XPClassInfo[] upLevels, XPDictionary dictionary, CriteriaOperator criteria) {
			return new CriteriaTypeResolver(upLevels, dictionary).Process(criteria);
		}
		static public CriteriaTypeResolverResult ResolveTypeResult(XPClassInfo info, CriteriaOperator criteria, CriteriaTypeResolveKeyBehavior resolveKeyBehavior) {
			return new CriteriaTypeResolver(info, resolveKeyBehavior).Process(criteria);
		}
		static public CriteriaTypeResolverResult ResolveTypeResult(XPClassInfo[] upLevels, XPDictionary dictionary, CriteriaOperator criteria, CriteriaTypeResolveKeyBehavior resolveKeyBehavior) {
			return new CriteriaTypeResolver(upLevels, dictionary, resolveKeyBehavior).Process(criteria);
		}
		protected override Type GetCustomFunctionType(string functionName, params Type[] operands) {
			ICustomFunctionOperator customFunction = dictionary.CustomFunctionOperators.GetCustomFunction(functionName);
			if(customFunction == null) {
				return base.GetCustomFunctionType(functionName, operands);
			}
			return customFunction.ResultType(operands);
		}
		protected override Type GetCustomAggregateType(string customAggregateName, params Type[] operands) {
			ICustomAggregate customAggregate = dictionary.CustomAggregates.GetCustomAggregate(customAggregateName);
			if(customAggregate == null) {
				return base.GetCustomAggregateType(customAggregateName, operands);
			}
			return customAggregate.ResultType(operands);
		}
	}
	public interface IXPDictionaryProvider {
		XPDictionary Dictionary { get; }
	}
	public class EnumsConverter : ValueConverter {
		Type enumType;
		public EnumsConverter(Type enumType) {
			this.enumType = enumType;
		}
		public override object ConvertFromStorageType(object value) {
			return value == null ? null : Enum.ToObject(enumType, value);
		}
		public override object ConvertToStorageType(object value) {
			if(value == null)
				return null;
			if(value is string)
				value = Enum.Parse(enumType, (string)value, false);
			value = Convert.ChangeType(value, StorageType, CultureInfo.InvariantCulture);
			return value;
		}
		public override Type StorageType {
			get { return Enum.GetUnderlyingType(enumType); }
		}
	}
	public class DefaultTimeSpanConverter : ValueConverter {
		public override object ConvertFromStorageType(object value) {
			if(value == null)
				return null;
			if(value is TimeSpan)
				return value;
			double seconds = Convert.ToDouble(value);
			if(seconds > TimeSpan.MaxValue.TotalSeconds - 0.0005 && seconds < TimeSpan.MaxValue.TotalSeconds + 0.0005)
				return TimeSpan.MaxValue;
			if(seconds < TimeSpan.MinValue.TotalSeconds + 0.0005 && seconds > TimeSpan.MinValue.TotalSeconds - 0.0005)
				return TimeSpan.MinValue;
			return TimeSpan.FromSeconds(seconds);
		}
		public override object ConvertToStorageType(object value) {
			if(value == null)
				return null;
			if(value is double)
				return value;
			if(value is float)
				return Convert.ToDouble(value);
			return ((TimeSpan)value).TotalSeconds;
		}
		public override Type StorageType {
			get { return typeof(double); }
		}
	}
	[NonPersistent, MemberDesignTimeVisibility(false)]
	public sealed class IntermediateObject : XPBaseObject {
		public IntermediateObject(Session session, XPClassInfo classInfo) : base(session, classInfo) { }
		[NonPersistent]
		public object LeftIntermediateObjectField;
		[NonPersistent]
		public object RightIntermediateObjectField;
		[NonPersistent]
		public object IntermediateObjectOid;
	}
	public sealed class IntermediateObjectFieldInfo : XPMemberInfo {
		internal XPMemberInfo refProperty;
		bool isLeft;
		public override string Name { get { return refProperty.Name; } }
		public override XPClassInfo ReferenceType {
			get { return refProperty.CollectionElementType; }
		}
		public IntermediateObjectFieldInfo(XPMemberInfo refProperty, XPClassInfo owner, bool isLeft)
			: base(owner, false) {
			this.refProperty = refProperty;
			this.isLeft = isLeft;
			Owner.AddMember(this);
			AddAttribute(new ExplicitLoadingAttribute());
		}
		protected override bool CanPersist { get { return true; } }
		public override bool IsPublic { get { return true; } }
		public override Type MemberType { get { return this.ReferenceType.ClassType; } }
		public override object GetValue(object theObject) {
			if(isLeft)
				return ((IntermediateObject)theObject).LeftIntermediateObjectField;
			else
				return ((IntermediateObject)theObject).RightIntermediateObjectField;
		}
		public override void SetValue(object theObject, object theValue) {
			if(isLeft)
				((IntermediateObject)theObject).LeftIntermediateObjectField = theValue;
			else
				((IntermediateObject)theObject).RightIntermediateObjectField = theValue;
		}
		public override bool GetModified(object theObject) {
			return PersistentBase.GetModificationsStore(theObject).GetPropertyModified(this);
		}
		public override void SetModified(object theObject, object oldValue) {
			PersistentBase.GetModificationsStore(theObject).SetPropertyModified(this, oldValue);
		}
		public override object GetOldValue(object theObject) {
			return PersistentBase.GetModificationsStore(theObject).GetPropertyOldValue(this);
		}
		public override void ResetModified(object theObject) {
			PersistentBase.GetModificationsStore(theObject).ResetPropertyModified(this);
		}
		public override Expression MakeGetExpression(Expression ownerExpression) {
			string fieldName = isLeft ? "LeftIntermediateObjectField" : "RightIntermediateObjectField";
			return Expression.Convert(Expression.PropertyOrField(ownerExpression, fieldName), MemberType);
		}
	}
	public sealed class IntermediateObjectKeyFieldInfo : XPMemberInfo {
		Type keyType;
		public override string Name { get { return "OID"; } }
		public IntermediateObjectKeyFieldInfo(XPClassInfo owner, Type keyType)
			: base(owner, false) {
			this.keyType = keyType;
			AddAttribute(new KeyAttribute(true));
			Owner.AddMember(this);
		}
		protected override bool CanPersist { get { return true; } }
		public override bool IsPublic { get { return true; } }
		public override Type MemberType { get { return this.keyType; } }
		public override object GetValue(object theObject) {
			return ((IntermediateObject)theObject).IntermediateObjectOid;
		}
		public override void SetValue(object theObject, object theValue) {
			((IntermediateObject)theObject).IntermediateObjectOid = theValue;
		}
		public override bool GetModified(object theObject) {
			return PersistentBase.GetModificationsStore(theObject).GetPropertyModified(this);
		}
		public override void SetModified(object theObject, object oldValue) {
			PersistentBase.GetModificationsStore(theObject).SetPropertyModified(this, oldValue);
		}
		public override object GetOldValue(object theObject) {
			return PersistentBase.GetModificationsStore(theObject).GetPropertyOldValue(this);
		}
		public override void ResetModified(object theObject) {
			PersistentBase.GetModificationsStore(theObject).ResetPropertyModified(this);
		}
		public override Expression MakeGetExpression(Expression ownerExpression) {
			return Expression.Convert(Expression.PropertyOrField(ownerExpression, "IntermediateObjectOid"), NullableHelpers.GetUnBoxedType(MemberType));
		}
	}
	public sealed class IntermediateClassInfo : XPClassInfo {
		MembersCollection ownMembers = new MembersCollection();
		string name;
		internal IntermediateObjectFieldInfo intermediateObjectFieldInfoLeft;
		internal IntermediateObjectFieldInfo intermediateObjectFieldInfoRight;
		void internalCollectMembers(Type currentType) {
			foreach(FieldInfo fi in currentType.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
				new ReflectionFieldInfo(this, fi, null);
			}
			foreach(PropertyInfo pi in currentType.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
				new ReflectionPropertyInfo(this, pi, null);
			}
		}
		public override bool HasModifications(object theObject) {
			return PersistentBase.GetModificationsStore(theObject).HasModifications();
		}
		public override void ClearModifications(object theObject) {
			PersistentBase.GetModificationsStore(theObject).ClearModifications();
		}
		public override void AddMember(XPMemberInfo newMember) {
			ownMembers.Add(newMember);
			base.AddMember(newMember);
		}
		public IntermediateClassInfo(XPMemberInfo refProperty, XPMemberInfo relatedProperty, XPDictionary dictionary, string name)
			: base(dictionary) {
			Dictionary.GetClassInfo(typeof(IntermediateObject));
			Dictionary.GetClassInfo(typeof(XPBaseObject));
			this.name = name;
			internalCollectMembers(typeof(IntermediateObject));
			intermediateObjectFieldInfoLeft = new IntermediateObjectFieldInfo(refProperty, this, true);
			IndexedAttribute unique = new IndexedAttribute(relatedProperty.Name);
			unique.Unique = true;
			intermediateObjectFieldInfoLeft.AddAttribute(unique);
			intermediateObjectFieldInfoRight = new IntermediateObjectFieldInfo(relatedProperty, this, false);
			Type keyType = typeof(int);
			if(intermediateObjectFieldInfoLeft.ReferenceType.KeyProperty.MemberType == typeof(Guid) && intermediateObjectFieldInfoRight.ReferenceType.KeyProperty.MemberType == typeof(Guid))
				keyType = typeof(Guid);
			new IntermediateObjectKeyFieldInfo(this, keyType);
			optimisticLockingCache = new OptimisticLockingCacheItem(OptimisticLockingBehavior.ConsiderOptimisticLockingField,
				new OptimisticLockField(this, OptimisticLockFieldName),
				new OptimisticLockFieldInDataLayer(this, OptimisticLockFieldInDataLayerName));
			Dictionary.AddClassInfo(this);
		}
		public override XPClassInfo BaseClass { get { return Dictionary.GetClassInfo(typeof(IntermediateObject)); } }
		public override ICollection<XPMemberInfo> OwnMembers { get { return ownMembers; } }
		public override string FullName { get { return name; } }
		public const string IntermediateObjectAssemblyName = "";	
		public override string AssemblyName { get { return IntermediateObjectAssemblyName; } }
		protected override bool CanPersist { get { return true; } }
		public override Type ClassType { get { return typeof(IntermediateObject); } }
		public override bool CanGetByClassType {
			get {
				return false;
			}
		}
		protected internal override object CreateObjectInstance(Session session, XPClassInfo instantiationClassInfo) {
			return new IntermediateObject(session, instantiationClassInfo);
		}
		public IntermediateObjectFieldInfo GetFieldInfo(XPMemberInfo refProperty) {
			if(intermediateObjectFieldInfoLeft.refProperty == refProperty)
				return intermediateObjectFieldInfoLeft;
			if(intermediateObjectFieldInfoRight.refProperty == refProperty)
				return intermediateObjectFieldInfoRight;
			return null;
		}
	}
	class MembersCollection : ReadOnlyCollection<XPMemberInfo> {
		public MembersCollection()
			: base(new List<XPMemberInfo>()) {
		}
		public void Add(XPMemberInfo member) {
			Items.Add(member);
		}
	}
	public abstract class ServiceField : XPMemberInfo {
		protected ServiceField(XPClassInfo owner, bool isReadOnly) : base(owner, isReadOnly) { }
		protected override bool CanPersist { get { return true; } }
		public override bool IsPublic { get { return false; } }
	}
	public sealed class ObjectTypeField : ServiceField {
		public override string Name { get { return XPObjectType.ObjectTypePropertyName; } }
		public override Type MemberType { get { return typeof(XPObjectType); } }
		public override object GetValue(object theObject) {
			IXPClassInfoAndSessionProvider sessionObject = theObject as IXPClassInfoAndSessionProvider;
			if(sessionObject != null) {	
				return sessionObject.Session.GetObjectType(sessionObject);
			}
			else {
				return PersistentBase.GetCustomPropertyStore(theObject).GetCustomPropertyValue(this);
			}
		}
		public override void SetValue(object theObject, object theValue) {
			IXPClassInfoAndSessionProvider sessionObject = theObject as IXPClassInfoAndSessionProvider;
			if(sessionObject != null) {	
			}
			else {
				PersistentBase.GetCustomPropertyStore(theObject).SetCustomPropertyValue(this, theValue);
			}
		}
		public override bool GetModified(object theObject) {
			return false;
		}
		public override object GetOldValue(object theObject) {
			return null;
		}
		public override void ResetModified(object theObject) { }
		public override void SetModified(object theObject, object oldValue) { }
		internal ObjectTypeField(XPClassInfo owner)
			: base(owner, false) {
			Owner.AddMember(this);
		}
	}
	public sealed class GCRecordField : ServiceField {
		public static string StaticName { get { return "GCRecord"; } }
		public override string Name { get { return GCRecordField.StaticName; } }
		public override Type MemberType { get { return typeof(int); } }
		public override object GetValue(object theObject) {
			return PersistentBase.GetCustomPropertyStore(theObject).GetCustomPropertyValue(this);
		}
		public override void SetValue(object theObject, object theValue) {
			PersistentBase.GetCustomPropertyStore(theObject).SetCustomPropertyValue(this, theValue);
		}
		public override bool GetModified(object theObject) {
			return PersistentBase.GetModificationsStore(theObject).GetPropertyModified(this);
		}
		public override void SetModified(object theObject, object oldValue) {
			PersistentBase.GetModificationsStore(theObject).SetPropertyModified(this, oldValue);
		}
		public override object GetOldValue(object theObject) {
			return PersistentBase.GetModificationsStore(theObject).GetPropertyOldValue(this);
		}
		public override void ResetModified(object theObject) {
			PersistentBase.GetModificationsStore(theObject).ResetPropertyModified(this);
		}
		public GCRecordField(XPClassInfo owner)
			: base(owner, false) {
			Owner.AddMember(this);
			if(CreateIndex != false)
				this.AddAttribute(new IndexedAttribute());
		}
		public static bool? CreateIndex;
	}
	public sealed class OptimisticLockField : ServiceField {
		string name;
		public override string Name { get { return name; } }
		public override Type MemberType { get { return typeof(int); } }
		internal static object ConvertDbVersionToInt(object dbVersion) {
			if(dbVersion is DBNull)
				return null;
			return dbVersion;
		}
		public override object GetValue(object theObject) {
			object ver = PersistentBase.GetCustomPropertyStore(theObject).GetCustomPropertyValue(this);
			return ver;
		}
		public override void SetValue(object theObject, object theValue) {
			PersistentBase.GetCustomPropertyStore(theObject).SetCustomPropertyValue(this, ConvertDbVersionToInt(theValue));
		}
		public override bool GetModified(object theObject) {
			return PersistentBase.GetModificationsStore(theObject).GetPropertyModified(this);
		}
		public override object GetOldValue(object theObject) {
			return PersistentBase.GetModificationsStore(theObject).GetPropertyOldValue(this);
		}
		public override void ResetModified(object theObject) {
			PersistentBase.GetModificationsStore(theObject).ResetPropertyModified(this);
		}
		public override void SetModified(object theObject, object oldValue) {
			PersistentBase.GetModificationsStore(theObject).SetPropertyModified(this, oldValue);
		}
		public OptimisticLockField(XPClassInfo owner, string name)
			: base(owner, false) {
			this.name = name;
			Owner.AddMember(this);
		}
	}
	public sealed class OptimisticLockFieldInDataLayer : ServiceField {
		string name;
		public override string Name { get { return name; } }
		public override Type MemberType { get { return typeof(int); } }
		protected override bool CanPersist { get { return false; } }
		public override object GetValue(object theObject) {
			object ver = PersistentBase.GetCustomPropertyStore(theObject).GetCustomPropertyValue(this);
			return ver;
		}
		public override void SetValue(object theObject, object theValue) {
			PersistentBase.GetCustomPropertyStore(theObject).SetCustomPropertyValue(this, OptimisticLockField.ConvertDbVersionToInt(theValue));
		}
		public override bool GetModified(object theObject) {
			return PersistentBase.GetModificationsStore(theObject).GetPropertyModified(this);
		}
		public override object GetOldValue(object theObject) {
			return PersistentBase.GetModificationsStore(theObject).GetPropertyOldValue(this);
		}
		public override void ResetModified(object theObject) {
			PersistentBase.GetModificationsStore(theObject).ResetPropertyModified(this);
		}
		public override void SetModified(object theObject, object oldValue) {
			PersistentBase.GetModificationsStore(theObject).SetPropertyModified(this, oldValue);
		}
		public OptimisticLockFieldInDataLayer(XPClassInfo owner, string name)
			: base(owner, false) {
			this.name = name;
			Owner.AddMember(this);
		}
	}
	public sealed class MemberInfoCollection : List<XPMemberInfo> {
		XPClassInfo classInfo;
		bool hasNonPersistent;
		public bool HasNonPersistent { get { return hasNonPersistent; } }
		public MemberInfoCollection(XPClassInfo classInfo, int count) {
			this.classInfo = classInfo;
		}
		public MemberInfoCollection(XPClassInfo classInfo)
			: this(classInfo, 4) {
		}
		public MemberInfoCollection(XPClassInfo classInfo, params XPMemberInfo[] members)
			: this(classInfo, members.Length) {
			AddRange(members);
		}
		public MemberInfoCollection(XPClassInfo classInfo, string path) : this(classInfo, path, false) { }
		public MemberInfoCollection(XPClassInfo classInfo, string path, bool addNonPersistent) : this(classInfo, path, addNonPersistent, true) { }
		public MemberInfoCollection(XPClassInfo classInfo, string path, bool addNonPersistent, bool throwOnError)
			: this(classInfo, SplitPath(path), addNonPersistent, throwOnError) {
		}
		public MemberInfoCollection(XPClassInfo classInfo, string[] matches, bool addNonPersistent, bool throwOnError)
			: this(classInfo, matches, addNonPersistent, false, throwOnError) { }
		public MemberInfoCollection(XPClassInfo classInfo, string[] matches, bool addNonPersistent, bool allowCollectionsInPath, bool throwOnError)
			: this(classInfo) {
			XPClassInfo currentClassInfo = classInfo;
			for(int i = 0; i < matches.Length && currentClassInfo != null; ++i) {
				XPMemberInfo mi = FindMember(currentClassInfo, matches[i]);
				while(mi != null && mi.IsStruct && i < matches.Length - 1) {
					++i;
					mi = currentClassInfo.FindMember(mi.Name + '.' + matches[i]);
				}
				if(mi == null ||
					(!addNonPersistent && !mi.IsExpandableToPersistent) ||
					(i < matches.Length - 1 && mi.ReferenceType == null && !allowCollectionsInPath)) {
					if(throwOnError)
						throw new InvalidPropertyPathException(Res.GetString(Res.MetaData_IncorrectPathMemberNotExists, classInfo.FullName, string.Join(".", matches), currentClassInfo.FullName, matches[i]));
					Clear();
					return;
				}
				if(!mi.IsExpandableToPersistent)
					hasNonPersistent = true;
				this.Add(mi);
				if(allowCollectionsInPath && (mi.IsAssociationList || mi.IsManyToMany || mi.IsManyToManyAlias)) {
					currentClassInfo = mi.CollectionElementType;
				}
				else {
					currentClassInfo = mi.ReferenceType;
				}
			}
		}
		public static MemberInfoCollection ParsePath(XPClassInfo classInfo, string path) {
			return classInfo.ParsePath(path);
		}
		public static MemberInfoCollection ParsePersistentPath(XPClassInfo classInfo, string path) {
			return classInfo.ParsePersistentPath(path);
		}
		public static XPMemberInfo FindMember(XPClassInfo currentClassInfo, string match) {
			if(match.Length == 0 || match[0] != '<')
				return currentClassInfo.FindMember(match);
			int pos = match.IndexOf('>');
			if(pos < 0)
				return null;
			string className = match.Substring(1, pos - 1);
			string memberName = match.Substring(pos + 1);
			XPClassInfo resolvedWithNamespace;
			if(TryResolveType(className, currentClassInfo, out resolvedWithNamespace)) {
				if(resolvedWithNamespace != null && resolvedWithNamespace.IsAssignableTo(currentClassInfo)) {
					XPMemberInfo rv = FindMember(resolvedWithNamespace, memberName);
					if(rv != null)
						return rv;
				}
			}
			XPClassInfo resolvedWithoutNamespace = currentClassInfo.Dictionary.QueryClassInfo(string.Empty, className);
			if(resolvedWithoutNamespace != null && resolvedWithoutNamespace.IsAssignableTo(currentClassInfo)) {
				XPMemberInfo rv = FindMember(resolvedWithoutNamespace, memberName);
				if(rv != null)
					return rv;
			}
			XPMemberInfo result = null;
			foreach(XPClassInfo ci in currentClassInfo.Dictionary.Classes) {
				if(!ci.FullName.EndsWith(className))
					continue;
				int dotPos = ci.FullName.Length - className.Length - 1;
				if(dotPos >= 0) {
					char shouldBeClassNameDelimiter = ci.FullName[dotPos];
					if(shouldBeClassNameDelimiter != '.' && shouldBeClassNameDelimiter != '+')
						continue;
				}
				XPMemberInfo rv = FindMember(ci, memberName);
				if(rv == null)
					continue;
				if(ci.IsPersistent && currentClassInfo.IsPersistent && ci.IdClass != currentClassInfo.IdClass)
					continue;
				if(result != null)
					throw new InvalidOperationException(Res.GetString(Res.Metadata_AmbiguousClassName, className, rv.Owner.FullName, result.Owner.FullName));
				result = rv;
			}
			return result;
		}
		public override string ToString() {
			XPClassInfo currentClassInfo = classInfo;
			StringBuilder res = new StringBuilder();
			for(int i = 0; i < Count; i++) {
				if(res.Length != 0)
					res.Append('.');
				if(this[i] != null) {
					if(currentClassInfo != null && !currentClassInfo.IsAssignableTo(this[i].Owner)) {
						string name = this[i].Owner.FullName;
						res.Append('<');
						res.Append(name);
						res.Append('>');
					}
					res.Append(this[i].Name);
					currentClassInfo = this[i].ReferenceType;
				}
				else
					res.Append('^');
			}
			return res.ToString();
		}
		static char[] upcastSymbols = new char[] { '<', '>' };
		public static string[] SplitPath(string path) {
			if(path.IndexOf('.') < 0)
				return new string[] { path };
			if(path.IndexOfAny(upcastSymbols) < 0) {
				return path.Split('.');
			}
			List<string> result = new List<string>();
			bool inUpCast = false;
			int prevCutPos = 0;
			for(int i = 0; i < path.Length; i++) {
				switch(path[i]) {
					case '.':
						if(inUpCast) continue;
						result.Add(path.Substring(prevCutPos, i - prevCutPos));
						prevCutPos = i + 1;
						break;
					case '<':
						inUpCast = true;
						break;
					case '>':
						inUpCast = false;
						break;
					default:
						break;
				}
			}
			if(prevCutPos < path.Length) {
				if(prevCutPos == 0) {
					return new string[] { path };
				}
				else if(path[prevCutPos] != '<' || path[path.Length - 1] != '>') {
					result.Add(path.Substring(prevCutPos));
				}
			}
			return result.ToArray();
		}
		static char[] namespaceSplitters = new char[] { '.', '+' };
		public static int LastIndexOfSplittingDotInPath(string path) {
			int lastDotPos = path.LastIndexOf('.');
			if(lastDotPos < 0)
				return lastDotPos;
			int closingAngBr = path.IndexOf('>', lastDotPos);
			if(closingAngBr < 0)
				return lastDotPos;
			int openingAngBr = path.LastIndexOf('<', closingAngBr);
			if(openingAngBr < 0)
				throw new InvalidOperationException("Unbalansed <upcasting> within '" + path + "' property path");
			return path.LastIndexOf('.', openingAngBr);
		}
		public static int GetSplitPartsCount(string path) {
			if(path.Length == 0)
				return 0;
			int cnt = 1;
			for(int pos = 0; pos < path.Length; ++pos) {
				char ch = path[pos];
				if(ch == '.') {
					++cnt;
				}
				else if(ch == '<') {
					while(path[pos] != '>') {
						++pos;
						if(pos >= path.Length)
							throw new InvalidOperationException("Unbalanced upcasting '" + path + "'");
					}
				}
			}
			return cnt;
		}
		public static bool TryResolveType(string className, XPClassInfo rootClassInfo, out XPClassInfo classInfo) {
			classInfo = null;
			int namespaceLenInCurrentClassInfo = rootClassInfo.FullName.LastIndexOfAny(namespaceSplitters);
			if(namespaceLenInCurrentClassInfo >= 0) {
				XPClassInfo resolvedWithNamespace;
				if(className.LastIndexOfAny(namespaceSplitters) >= 0) {
					resolvedWithNamespace = rootClassInfo.Dictionary.QueryClassInfo(rootClassInfo.AssemblyName, className);
				}
				else {
					resolvedWithNamespace = rootClassInfo.Dictionary.QueryClassInfo(rootClassInfo.AssemblyName, rootClassInfo.FullName.Substring(0, namespaceLenInCurrentClassInfo + 1) + className);
				}
				classInfo = resolvedWithNamespace;
			}
			if(classInfo == null) return false;
			return true;
		}
		public static bool TryResolveTypeAlsoByShortName(string className, XPClassInfo rootClassInfo, out XPClassInfo classInfo) {
			classInfo = null;
			if(!MemberInfoCollection.TryResolveType(className, rootClassInfo, out classInfo)) {
				bool isFullClassName = className.LastIndexOfAny(namespaceSplitters) >= 0;
				foreach(XPClassInfo ci in rootClassInfo.Dictionary.Classes) {
					if(ci.FullName != className) {
						if(isFullClassName) {
							continue;
						}
						else {
							if(ci.ClassType == null) {
								int dotIndex = ci.FullName.LastIndexOfAny(namespaceSplitters);
								if(!(((dotIndex >= 0) && (ci.FullName.Substring(dotIndex) == className)) || ((dotIndex < 0) && (ci.FullName == className)))) continue;
							}
							else if(ci.ClassType.Name != className) continue;
						}
					}
					classInfo = ci;
					return true;
				}
				return false;
			}
			return true;
		}
	}
	public sealed class MemberPathCollection : List<MemberInfoCollection> {
		public void AddRange(ICollection range) {
			foreach(MemberInfoCollection mic in range)
				Add(mic);
		}
		public MemberPathCollection() : base() { }
		public MemberPathCollection(XPClassInfo classInfo, XPMemberInfo member)
			: base() {
			Add(new MemberInfoCollection(classInfo, member));
		}
		public MemberPathCollection(XPClassInfo classInfo, string pathes)
			: base() {
			string[] pathElements = pathes.Split(';');
			for(Int32 i = 0; i < pathElements.Length; ++i) {
				this.Add(classInfo.ParsePersistentPath(pathElements[i]));
			}
		}
	}
	public class EvaluatorContextDescriptorXpo : EvaluatorContextDescriptor {
		public readonly XPClassInfo Owner;
		public readonly bool IsInTransaction;
		public EvaluatorContextDescriptorXpo(XPClassInfo owner) {
			this.Owner = owner;
		}
		public EvaluatorContextDescriptorXpo(XPClassInfo owner, bool inTransaction)
			: this(owner) {
			IsInTransaction = inTransaction;
		}
		public override object GetPropertyValue(object source, EvaluatorProperty propertyPath) {
			if(source == null)
				return null;
			XPMemberInfo mi = MemberInfoCollection.FindMember(Owner, propertyPath.PropertyPath);
			if(mi != null) {
				if(propertyPath.PropertyPath.Length > 0 && propertyPath.PropertyPath[0] == '<') {
					XPClassInfo realCi = Owner.Dictionary.GetClassInfo(source);
					if(!realCi.IsAssignableTo(mi.Owner))
						return null;
				}
				return mi.GetValue(source);
			}
			if(EvaluatorProperty.GetIsThisProperty(propertyPath.PropertyPath))
				return source;
			string[] path = propertyPath.PropertyPathTokenized;
			EvaluatorProperty current = propertyPath.SubProperty;
			for(int i = 1; i < path.Length; ++i) {
				string subPath = string.Join(".", path, 0, i);
				mi = MemberInfoCollection.FindMember(Owner, subPath);
				if(mi != null && mi.ReferenceType != null) {
					if(subPath.Length > 0 && subPath[0] == '<') {
						XPClassInfo realCi = Owner.Dictionary.GetClassInfo(source);
						if(!realCi.IsAssignableTo(mi.Owner))
							return null;
					}
					object nestedObject = mi.GetValue(source);
					return mi.ReferenceType.GetEvaluatorContextDescriptor().GetPropertyValue(nestedObject, current);
				}
				current = current.SubProperty;
			}
			throw new InvalidPropertyPathException(Res.GetString(Res.MetaData_IncorrectPath, Owner.FullName, propertyPath.PropertyPath));
		}
		public override EvaluatorContext GetNestedContext(object source, string propertyPath) {
			if(source == null)
				return null;
			XPMemberInfo mi = MemberInfoCollection.FindMember(Owner, propertyPath);
			if(mi == null) {
				throw new InvalidPropertyPathException(Res.GetString(Res.MetaData_IncorrectPath, Owner.FullName, propertyPath));
			}
			else if(mi.ReferenceType == null) {
				throw new InvalidPropertyPathException(Res.GetString(Res.MetaData_IncorrectPathNonReferenceMember, Owner.FullName, propertyPath, Owner.FullName, propertyPath));
			}
			else {
				object nestedSource = mi.GetValue(source);
				if(nestedSource == null)
					return null;
				EvaluatorContextDescriptor nestedDescriptor = mi.ReferenceType.GetEvaluatorContextDescriptor();
				return new EvaluatorContext(nestedDescriptor, nestedSource);
			}
		}
		public override IEnumerable GetCollectionContexts(object source, string collectionName) {
			if(source == null)
				return null;
			XPMemberInfo mi = MemberInfoCollection.FindMember(Owner, collectionName);
			if(mi == null)
				throw new InvalidPropertyPathException(Res.GetString(Res.MetaData_IncorrectPath, Owner.FullName, collectionName));
			if(!mi.IsAssociationList && !mi.IsNonAssociationList)
				throw new ArgumentException(Res.GetString(Res.Metadata_AssociationListExpected, Owner.FullName, collectionName), nameof(collectionName));
			IList collection = (IList)mi.GetValue(source);
			if(collection == null)
				return null;
			EvaluatorContextDescriptor elementsDescriptor = mi.CollectionElementType.GetEvaluatorContextDescriptor();
			return new CollectionContexts(elementsDescriptor, collection);
		}
		public override IEnumerable GetQueryContexts(object source, string queryTypeName, CriteriaOperator condition, int top) {
			XPClassInfo queryType;
			if(!MemberInfoCollection.TryResolveTypeAlsoByShortName(queryTypeName, Owner, out queryType)) throw new InvalidOperationException(Res.GetString(Res.Metadata_TypeNotFound, queryTypeName));
			EvaluatorContextDescriptor descriptor = IsInTransaction ? queryType.GetEvaluatorContextDescriptorInTransaction() : queryType.GetEvaluatorContextDescriptor();
			ISessionProvider sessionProvider = source as ISessionProvider;
			if(sessionProvider == null) {
				IEnumerable enumerableSource = source as IEnumerable;
				if(enumerableSource != null) {
					foreach(object obj in enumerableSource) {
						sessionProvider = obj as ISessionProvider;
						break;
					}
				}
			}
			if(sessionProvider == null) throw new InvalidOperationException(Res.GetString(Res.Metadata_NullSessionProvider));
			ICollection collection = null;
#if NET
			var customQueryContextProvider = sessionProvider.Session.ServiceProvider?.GetService<IQueryContextProvider>();
			collection = customQueryContextProvider?.GetObjects(sessionProvider.Session, queryType, condition, top);
#endif
			if(collection == null) {
				collection = IsInTransaction ? sessionProvider.Session.GetObjectsInTransaction(queryType, condition, null, 0, top, false) : sessionProvider.Session.GetObjects(queryType, condition, null, 0, top, false, false);
			}
			return new CollectionContexts(descriptor, collection);
		}
	}
	public class CriteriaCompilerDescriptorXpo : CriteriaCompilerDescriptor {
		public readonly XPClassInfo Owner;
		public readonly Session Session;
		public CriteriaCompilerDescriptorXpo(XPClassInfo owner, Session session) {
			this.Owner = owner;
			this.Session = session;
		}
		public override Type ObjectType {
			get { return Owner.RealInstanceType; }
		}
		static Func<object, XPClassInfo, bool> isReallyUpCasted = (instance, ci) => instance != null && ci.Dictionary.GetClassInfo(instance).IsAssignableTo(ci);
		public override Expression MakePropertyAccess(Expression baseExpression, string propertyPath) {
			if(EvaluatorProperty.GetIsThisProperty(propertyPath))
				return baseExpression;
			XPMemberInfo mi;
			string subProperty;
			{
				KeyValuePair<XPMemberInfo, string> solution = ResolveDiveStep(propertyPath);
				mi = solution.Key;
				subProperty = solution.Value;
			}
			if(subProperty == null)
				return MakePropertyAccessCore(baseExpression, mi);
			ParameterExpression sub = Expression.Parameter(mi.ReferenceType.ClassType, "sub");
			Expression subAccess = GetCriteriaCompilerDescriptor(mi.ReferenceType).MakePropertyAccess(sub, subProperty);
			if(!NullableHelpers.CanAcceptNull(subAccess.Type))
				subAccess = Expression.Convert(subAccess, NullableHelpers.GetUnBoxedType(subAccess.Type));
			Expression body = Expression.Condition(Expression.Call(typeof(object), "ReferenceEquals", null, sub, Expression.Constant(null)), Expression.Constant(null, subAccess.Type), subAccess);
			LambdaExpression l = Expression.Lambda(body, sub);
			Expression access = MakePropertyAccessCore(baseExpression, mi);
			return Expression.Invoke(l, access);
		}
		public override Type ResolvePropertyType(Expression baseExpression, string propertyPath) {
			KeyValuePair<XPMemberInfo, string> solution = ResolveDiveStep(propertyPath);
			return solution.Key?.MemberType ?? typeof(object);
		}
		KeyValuePair<XPMemberInfo, string> ResolveDiveStep(string path) {
			if(string.IsNullOrEmpty(path))
				throw new InvalidPropertyPathException(Res.GetString(Res.MetaData_IncorrectPath, Owner.FullName, "(Empty)"));
			string[] splitPath = MemberInfoCollection.SplitPath(path);
			for(int testLength = 1; ; ++testLength) {
				string testPath = testLength == splitPath.Length ? path : string.Join(".", splitPath, 0, testLength);
				XPMemberInfo mi = MemberInfoCollection.FindMember(Owner, testPath);
				if(mi == null) {
					if(testLength == splitPath.Length)
						throw new InvalidPropertyPathException(Res.GetString(Res.MetaData_IncorrectPath, Owner.FullName, testPath));
					else
						continue;
				}
				if(testLength == splitPath.Length)
					return new KeyValuePair<XPMemberInfo, string>(mi, null);
				if(mi.ReferenceType != null)
					return new KeyValuePair<XPMemberInfo, string>(mi, string.Join(".", splitPath, testLength, splitPath.Length - testLength));
			}
		}
		Expression MakePropertyAccessCore(Expression baseExpression, XPMemberInfo mi) {
			if(this.Owner.IsAssignableTo(mi.Owner))
				return mi.MakeGetExpression(baseExpression);
			else {
				ParameterExpression upCasted = Expression.Parameter(mi.Owner.ClassType, "upCasted");
				Expression access = mi.MakeGetExpression(upCasted);
				if(!NullableHelpers.CanAcceptNull(access.Type))
					access = Expression.Convert(access, NullableHelpers.GetUnBoxedType(access.Type));
				Expression.Constant(this.Owner.Dictionary);
				Expression body = Expression.Condition(Expression.Invoke(Expression.Constant(isReallyUpCasted), upCasted, Expression.Constant(mi.Owner)), access, Expression.Constant(null, access.Type));
				Expression lambda = Expression.Lambda(body, upCasted);
				return Expression.Invoke(lambda, Expression.TypeAs(baseExpression, mi.Owner.ClassType));
			}
		}
		public override CriteriaCompilerRefResult DiveIntoCollectionProperty(Expression baseExpression, string collectionPropertyPath) {
			if(EvaluatorProperty.GetIsThisProperty(collectionPropertyPath))
				throw new InvalidOperationException("unexpected top level collection " + collectionPropertyPath);
			XPMemberInfo mi;
			string subProperty;
			{
				KeyValuePair<XPMemberInfo, string> solution = ResolveDiveStep(collectionPropertyPath);
				mi = solution.Key;
				subProperty = solution.Value;
			}
			CriteriaCompilerDescriptor descriptor;
			if(subProperty == null) {
				if(!mi.IsAssociationList && !mi.IsNonAssociationList)
					throw new ArgumentException(Res.GetString(Res.Metadata_AssociationListExpected, Owner.FullName, collectionPropertyPath), nameof(collectionPropertyPath));
				descriptor = GetCriteriaCompilerDescriptor(mi.CollectionElementType);
			}
			else {
				descriptor = GetCriteriaCompilerDescriptor(mi.ReferenceType);
			}
			Expression diveExpression = MakePropertyAccessCore(baseExpression, mi);
			return new CriteriaCompilerRefResult(new CriteriaCompilerLocalContext(diveExpression, descriptor), subProperty);
		}
		public static object FreeJoinDo(Session session, XPClassInfo classInfo, CriteriaOperator topLevelExpression, CriteriaOperator condition, OperandParameter[] parameters, object[] parameterValues) {
			for(int i = 0; i < parameters.Length; ++i) {
				parameters[i].Value = parameterValues[i];
			}
			object rawResult = session.EvaluateInTransaction(classInfo, topLevelExpression, condition);
			if(rawResult != null) {
				var resultType = CriteriaTypeResolver.ResolveType(classInfo, topLevelExpression);
				var resultClassInfo = session.Dictionary.QueryClassInfo(resultType);
				if(resultClassInfo != null) {
					return session.GetObjectByKey(resultClassInfo, rawResult);
				}
			}
			return rawResult;
		}
		LambdaExpression MakeFreeJoinLambdaInternal(string joinTypeName, CriteriaOperator condition, OperandParameter[] conditionParameters, Aggregate aggregateType, string customAggregateName, CriteriaOperator[] aggregateExpressions, OperandParameter[] aggregateExpresssionsParameters, Type[] invokeTypes) {
			XPClassInfo queryType;
			if(!MemberInfoCollection.TryResolveTypeAlsoByShortName(joinTypeName, Owner, out queryType))
				throw new InvalidOperationException(Res.GetString(Res.Metadata_TypeNotFound, joinTypeName));
			queryType.CheckAbstractReference();
			CriteriaOperator topLevelAggregate;
			if(aggregateType != Aggregate.Custom) {
				topLevelAggregate = aggregateType == Aggregate.Single ? aggregateExpressions[0] : AggregateOperand.TopLevel(aggregateType, aggregateExpressions[0]);
			}
			else {
				topLevelAggregate = AggregateOperand.TopLevel(customAggregateName, aggregateExpressions);
			}
			OperandParameter[] allParameters = conditionParameters.Concat(aggregateExpresssionsParameters).ToArray();
			ParameterExpression[] lambdaParameters = invokeTypes.Select((t, i) => Expression.Parameter(t, "p" + i.ToString() + "_" + allParameters[i].ParameterName)).ToArray();
			Expression parametersArray = Expression.NewArrayInit(typeof(object), lambdaParameters.Select(p => p.Type != typeof(object) ? (Expression)Expression.Convert(p, typeof(object)) : p));
			Expression sessionParameter = MakeGetSession();
			Func<object[], Session, object> clojure = (values, session) => FreeJoinDo(session, queryType, topLevelAggregate, condition, allParameters, values);
			Expression body = Expression.Invoke(Expression.Constant(clojure), parametersArray, sessionParameter);
			Type retType;
			switch(aggregateType) {
				case Aggregate.Count:
					retType = typeof(int?);
					break;
				case Aggregate.Exists:
					retType = typeof(bool?);
					break;
				case Aggregate.Max:
				case Aggregate.Min:
				case Aggregate.Single:
					retType = NullableHelpers.GetUnBoxedType(CriteriaTypeResolver.ResolveType(queryType, aggregateExpressions[0]));
					break;
				case Aggregate.Sum:
					retType = CriteriaTypeResolver.ResolveType(queryType, aggregateExpressions[0]);
					retType = EvalHelpers.GetBinaryNumericPromotionType(retType, retType);
					retType = NullableHelpers.GetUnBoxedType(retType);
					break;
				case Aggregate.Avg:
					retType = CriteriaTypeResolver.ResolveType(queryType, aggregateExpressions[0]);
					retType = EvalHelpers.GetBinaryNumericPromotionType(retType, retType);
					if(retType != typeof(Single) && retType != typeof(Decimal))
						retType = typeof(Double);
					retType = NullableHelpers.GetUnBoxedType(retType);
					break;
				case Aggregate.Custom:
					retType = typeof(object);
					break;
				default:
					throw new NotImplementedException(aggregateType.ToString());
			}
			if(body.Type != retType)
				body = Expression.Convert(body, retType);
			return Expression.Lambda(body, lambdaParameters);
		}
		public override LambdaExpression MakeFreeJoinLambda(string joinTypeName, CriteriaOperator condition, OperandParameter[] conditionParameters, Aggregate aggregateType, CriteriaOperator aggregateExpression, OperandParameter[] aggregateExpresssionParameters, Type[] invokeTypes) {
			return MakeFreeJoinLambdaInternal(joinTypeName, condition, conditionParameters, aggregateType, null, new CriteriaOperator[] { aggregateExpression }, aggregateExpresssionParameters, invokeTypes);
		}
		public override LambdaExpression MakeFreeJoinLambda(string joinTypeName, CriteriaOperator condition, OperandParameter[] conditionParameters, string customAggregateName, IEnumerable<CriteriaOperator> aggregateExpressions, OperandParameter[] aggregateExpresssionsParameters, Type[] invokeTypes) {
			return MakeFreeJoinLambdaInternal(joinTypeName, condition, conditionParameters, Aggregate.Custom, customAggregateName, aggregateExpressions.ToArray(), aggregateExpresssionsParameters, invokeTypes);
		}
		protected virtual CriteriaCompilerDescriptor GetCriteriaCompilerDescriptor(XPClassInfo classInfo) {
			return classInfo.GetCriteriaCompilerDescriptor(Session);
		}
		protected virtual Expression MakeGetSession() {
			return Expression.Constant(Session, typeof(Session));
		}
	}
	public class ClassInfoByFullNameComparer : IComparer<XPClassInfo> {
		public static readonly ClassInfoByFullNameComparer Instance = new ClassInfoByFullNameComparer();
		ClassInfoByFullNameComparer() { }
		public int Compare(XPClassInfo x, XPClassInfo y) {
			if(x == null) {
				if(y == null) {
					return 0;
				}
				return -1;
			}
			if(y == null) {
				return 1;
			}
			return StringExtensions.ComparerInvariantCulture.Compare(x.FullName, y.FullName);
		}
	}
}
namespace DevExpress.Xpo.Metadata {
	using System.Collections.Generic;
	using System.ComponentModel.Design;
	using System.Data;
	using System.Threading;
	using DevExpress.Data.Internal;
	using DevExpress.Entity.ProjectModel;
	using DevExpress.Xpo.DB;
	using DevExpress.Xpo.Helpers;
	using DevExpress.Xpo.Metadata.Helpers;
	public abstract class ValueConverter {
		[Description("When overridden in a derived class, gets the type that the property’s value is converted to when it’s saved in a data store.")]
		public abstract Type StorageType { get; }
		public abstract object ConvertToStorageType(object value);
		public abstract object ConvertFromStorageType(object value);
	}
	public class UtcDateTimeConverter : ValueConverter {
		public override object ConvertFromStorageType(object value) {
			return value == null ? null : (object)((DateTime)value).ToLocalTime();
		}
		public override object ConvertToStorageType(object value) {
			return value == null ? null : (object)((DateTime)value).ToUniversalTime();
		}
		[Description("Gets the type that the property’s value will be converted to when it’s saved in a data store.")]
		public override Type StorageType { get { return typeof(DateTime); } }
	}
	public class ImageValueConverter : ValueConverter {
		static readonly TypeConverter imageConverterInstance;
		static ImageValueConverter() {
			Type imageConverterType = SafeTypeResolver.GetKnownType(typeof(System.Drawing.Image).Assembly, "System.Drawing.ImageConverter", false);
			if(imageConverterType != null) {
				imageConverterInstance = (TypeConverter)Activator.CreateInstance(imageConverterType);
			}
		}
		[Description("Gets the type that the property’s value is converted to when it’s saved in a data store.")]
		public override Type StorageType { get { return typeof(byte[]); } }
		public override object ConvertToStorageType(object value) {
			if(value == null) {
				return null;
			}
			else {
				return CallConvertFunction(() => imageConverterInstance.ConvertTo(value, StorageType));
			}
		}
		public override object ConvertFromStorageType(object value) {
			if(value == null) {
				return null;
			}
			else {
				return CallConvertFunction(() => imageConverterInstance.ConvertFrom(value));
			}
		}
		object CallConvertFunction(Func<object> fn) {
			if(imageConverterInstance == null)
				throw new PlatformNotSupportedException(Res.GetString(Res.ImageValueConverter_NotPresent));
			else
				return fn();
		}
	}
	public abstract class XPTypeInfo {
		Dictionary<string, Attribute> attributes;
		Dictionary<Type, Attribute> typedAttributes;
		static string GetAttributeName(Attribute attribute) {
			return attribute.GetType() == typeof(CustomAttribute) ? ((CustomAttribute)attribute).Name : attribute.GetType().Name;
		}
		public bool HasAttribute(Type attributeType) { return typedAttributes != null && typedAttributes.ContainsKey(attributeType); }
		public bool HasAttribute(string name) { return attributes != null && attributes.ContainsKey(name); }
		public Attribute FindAttributeInfo(Type attributeType) {
			Attribute res;
			return typedAttributes == null ? null : (typedAttributes.TryGetValue(attributeType, out res) ? res : null);
		}
		public Attribute FindAttributeInfo(string attributeName) {
			Attribute res;
			return attributes == null ? null : (attributes.TryGetValue(attributeName, out res) ? res : null);
		}
		public Attribute GetAttributeInfo(string name) {
			Attribute a = FindAttributeInfo(name);
			if(a == null) throw new RequiredAttributeMissingException(this.ToString(), name);
			return a;
		}
		public Attribute GetAttributeInfo(Type attributeType) {
			Attribute a = FindAttributeInfo(attributeType);
			if(a == null) throw new RequiredAttributeMissingException(this.ToString(), attributeType.Name);
			return a;
		}
		public void RemoveAttribute(Type attributeType) {
			if(typedAttributes != null) {
				typedAttributes.Remove(attributeType);
				attributes.Remove(attributeType.Name);
			}
		}
		public void AddAttribute(Attribute attribute) {
#pragma warning disable CS0618
			if(attribute is MapToAttribute) {
				MapToAttribute mapTo = (MapToAttribute)attribute;
				attribute = new PersistentAttribute(mapTo.MappingName);
			}
#pragma warning restore CS0618
			if(attribute is PersistentAttribute || attribute is NonPersistentAttribute || attribute is PersistentAliasAttribute) {
				RemoveAttribute(typeof(PersistentAttribute));
				RemoveAttribute(typeof(NonPersistentAttribute));
				RemoveAttribute(typeof(PersistentAliasAttribute));
			}
			string attributeName = GetAttributeName(attribute);
			if(attributes == null) {
				attributes = new Dictionary<string, Attribute>();
				typedAttributes = new Dictionary<Type, Attribute>();
			}
			Attribute oldAttribute;
			if(attributes.TryGetValue(attributeName, out oldAttribute)) {
				attributes.Remove(attributeName);
				typedAttributes.Remove(attribute.GetType());
			}
			else
				attributeName = String.Intern(attributeName);
			attributes.Add(attributeName, attribute);
			if(attribute.GetType() != typeof(CustomAttribute))
				typedAttributes.Add(attribute.GetType(), attribute);
			DropCache();
		}
		internal void DoDrop() {
			DropCache();
		}
		protected virtual void DropCache() {
			_IsPersistent = null;
		}
		[Description("Gets the attributes for this type.")]
		public Attribute[] Attributes {
			get {
				if(attributes == null)
					return Array.Empty<Attribute>();
				Attribute[] result = new Attribute[attributes.Values.Count];
				attributes.Values.CopyTo(result, 0);
				return result;
			}
		}
		protected abstract bool CanPersist { get; }
		bool? _IsPersistent;
		[Description("Gets whether a class or member of this type is persistent.")]
		public bool IsPersistent {
			get {
				if(!_IsPersistent.HasValue)
					_IsPersistent = CanPersist;
				return _IsPersistent.Value;
			}
		}
		[Description("Gets whether a property or class is visible at design time.")]
		public bool IsVisibleInDesignTime {
			get {
				MemberDesignTimeVisibilityAttribute vis = (MemberDesignTimeVisibilityAttribute)FindAttributeInfo(typeof(MemberDesignTimeVisibilityAttribute));
				return vis != null ? vis.IsVisible : true;
			}
		}
	}
	public class ClassInfoEventArgs : EventArgs {
		XPClassInfo ci;
		public XPClassInfo ClassInfo { get { return ci; } }
		public ClassInfoEventArgs(XPClassInfo classInfo) {
			this.ci = classInfo;
		}
	}
	public delegate void ClassInfoEventHandler(object sender, ClassInfoEventArgs e);
	public abstract class XPClassInfo : XPTypeInfo, IXPClassInfoProvider {
		class OptimisticLockingReadBehaviorAttributeCacheItem {
			public readonly bool? TrackPropertiesModifications;
			public readonly OptimisticLockingReadBehavior OptimisticLockingReadBehavior;
			public OptimisticLockingReadBehaviorAttributeCacheItem(bool? trackPropertiesModifications, OptimisticLockingReadBehavior optimisticLockingReadBehavior) {
				TrackPropertiesModifications = trackPropertiesModifications;
				OptimisticLockingReadBehavior = optimisticLockingReadBehavior;
			}
		}
		protected class OptimisticLockingCacheItem {
			public OptimisticLockingBehavior OptimisticLockingKind;
			public XPMemberInfo OptimisticLockField;
			public XPMemberInfo OptimisticLockFieldInDataLayer;
			public OptimisticLockingCacheItem(OptimisticLockingBehavior optimisticLockingKind, XPMemberInfo optimisticLockField, XPMemberInfo optimisticLockFieldInDataLayer) {
				OptimisticLockingKind = optimisticLockingKind;
				OptimisticLockField = optimisticLockField;
				OptimisticLockFieldInDataLayer = optimisticLockFieldInDataLayer;
			}
		}
		string tableName;
		IList<XPMemberInfo> members;
		IEnumerable objects;
		IEnumerable collections;
		IEnumerable assocLists;
		XPMemberInfo key;
		EvaluatorContextDescriptor _evaluatorContextDescriptor;
		EvaluatorContextDescriptor _evaluatorContextDescriptorInTransaction;
		XPDictionary dictionary;
		protected OptimisticLockingCacheItem optimisticLockingCache;
		OptimisticLockingReadBehaviorAttributeCacheItem optimisticLockingReadBehaviorCache;
		Dictionary<object, object> cache;
		internal T CreateCache<T>(Func<T> creator) {
			lock(this) {
				if(cache == null)
					cache = new Dictionary<object, object>();
				object res;
				if(!cache.TryGetValue(creator, out res)) {
					res = creator();
					if(creator.Target != null &&
						(Attribute.GetCustomAttribute(creator.Target.GetType(), typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)) == null ||
						!Array.TrueForAll(creator.Target.GetType().GetFields(), ff => ff.IsStatic)))
						throw new ArgumentException(null);
					cache.Add(creator, res);
				}
				return (T)res;
			}
		}
		protected override void DropCache() {
			tableName = null;
			key = null;
			members = null;
			objects = null;
			cache = null;
			collections = null;
			assocLists = null;
			persistentMembers = null;
			propertiesForInsert = null;
			propertiesForUpdate = null;
			idClass = null;
			table = null;
			directlyRefTypes = null;
			_evaluatorContextDescriptor = null;
			_evaluatorContextDescriptorInTransaction = null;
			cachedpaths = null;
			optimisticLockingReadBehaviorCache = null;
			isNullableBehaviorCached = false;
			base.DropCache();
			dictionary.OnClassInfoCacheDropped(this);
		}
		[Description("Gets a collection of XPMemberInfo objects that are owned by the current persistent class metadata information and all its ancestors.")]
		public ICollection<XPMemberInfo> Members {
			get {
				if(members == null) {
					List<XPClassInfo> classes = new List<XPClassInfo>();
					for(XPClassInfo ci = this; ci != null; ci = ci.BaseClass) {
						classes.Add(ci);
					}
					if(this.IsInterface) {
						foreach(Type t in this.ClassType.GetInterfaces()) {
							XPClassInfo ci = Dictionary.QueryClassInfo(t);
							if(ci == null)
								continue;
							classes.Add(ci);
						}
					}
					List<XPMemberInfo> list = new List<XPMemberInfo>();
					for(int i = classes.Count - 1; i >= 0; --i) {
						list.AddRange(classes[i].OwnMembers);
					}
					members = list.AsReadOnly();
				}
				return members;
			}
		}
		XPMemberInfo GetKeyPropertyOfIdClass() {
			if(key == null) {
				foreach(XPMemberInfo mi in Members) {
					if(mi.IsKey) {
						key = mi;
						break;
					}
				}
			}
			return key;
		}
		MapInheritanceType GetTableMapType() {
			MapInheritanceAttribute attribute = (MapInheritanceAttribute)FindAttributeInfo(typeof(MapInheritanceAttribute));
			return attribute != null ? attribute.MapType : MapInheritanceType.OwnTable;
		}
		public virtual void AddMember(XPMemberInfo newMember) {
			DropCache();
		}
		protected internal virtual XPMemberInfo QueryOwnMember(string memberName) {
			foreach(XPMemberInfo mi in OwnMembers)
				if(mi.Name == memberName)
					return mi;
			return null;
		}
		void InitOptimisticLockingReadBehavior() {
			for(XPClassInfo bas = IdClass; bas != null; bas = bas.BaseClass) {
				OptimisticLockingReadBehaviorAttribute attribute = (OptimisticLockingReadBehaviorAttribute)bas.FindAttributeInfo(typeof(OptimisticLockingReadBehaviorAttribute));
				if(attribute != null) {
					optimisticLockingReadBehaviorCache = new OptimisticLockingReadBehaviorAttributeCacheItem(attribute.TrackPropertiesModifications, attribute.Behavior);
					break;
				}
			}
			if(optimisticLockingReadBehaviorCache == null)
				optimisticLockingReadBehaviorCache = new OptimisticLockingReadBehaviorAttributeCacheItem(null, OptimisticLockingReadBehavior.Default);
		}
		public bool? TrackPropertiesModifications {
			get {
				if(optimisticLockingReadBehaviorCache == null) {
					InitOptimisticLockingReadBehavior();
				}
				return optimisticLockingReadBehaviorCache.TrackPropertiesModifications;
			}
		}
		public OptimisticLockingReadBehavior OptimisticLockingReadBehavior {
			get {
				if(optimisticLockingReadBehaviorCache == null) {
					InitOptimisticLockingReadBehavior();
				}
				return optimisticLockingReadBehaviorCache.OptimisticLockingReadBehavior;
			}
		}
		[Description("Gets the name of the system field that is used to control object locking for objects that have the object locking option enabled.")]
		public string OptimisticLockFieldName {
			get {
				for(XPClassInfo bas = IdClass; bas != null; bas = bas.BaseClass) {
					OptimisticLockingAttribute attribute = (OptimisticLockingAttribute)bas.FindAttributeInfo(typeof(OptimisticLockingAttribute));
					if(attribute != null)
						return attribute.Enabled ? attribute.FieldName : null;
				}
				return null;
			}
		}
		[Description("This member supports the .NET Framework infrastructure and cannot be used directly from your code.")]
		public string OptimisticLockFieldInDataLayerName {
			get {
				string olfn = OptimisticLockFieldName;
				if(olfn == null)
					return null;
				else
					return olfn + "InDataLayer";
			}
		}
		public OptimisticLockingBehavior OptimisticLockingBehavior {
			get {
				if(this == IdClass)
					return optimisticLockingCache == null ? OptimisticLockingBehavior.NoLocking : optimisticLockingCache.OptimisticLockingKind;
				return IdClass.OptimisticLockingBehavior;
			}
		}
		[Description("Gets the metadata of the member that represents the optimistic lock field.")]
		public XPMemberInfo OptimisticLockField {
			get {
				if(this == IdClass)
					return optimisticLockingCache == null ? null : optimisticLockingCache.OptimisticLockField;
				return IdClass.OptimisticLockField;
			}
		}
		[Description("This member supports the .NET Framework infrastructure and cannot be used directly from your code.")]
		public XPMemberInfo OptimisticLockFieldInDataLayer {
			get {
				if(this == IdClass)
					return optimisticLockingCache == null ? null : optimisticLockingCache.OptimisticLockFieldInDataLayer;
				return IdClass.OptimisticLockFieldInDataLayer;
			}
		}
		bool isNullableBehaviorCached;
		NullableBehavior nullableBehaviorCachedItem;
		public NullableBehavior NullableBehavior {
			get {
				if(!isNullableBehaviorCached) {
					nullableBehaviorCachedItem = NullableBehavior.Default;
					for(XPClassInfo ci = this; ci != null; ci = ci.BaseClass) {
						NullableBehaviorAttribute attr = (NullableBehaviorAttribute)ci.FindAttributeInfo(typeof(NullableBehaviorAttribute));
						if(attr != null) {
							nullableBehaviorCachedItem = attr.NullableBehavior;
							break;
						}
					}
					isNullableBehaviorCached = true;
				}
				return nullableBehaviorCachedItem;
			}
		}
		protected void InitServiceMembers() {
			if(this == IdClass) {
				string name = OptimisticLockFieldName;
				OptimisticLockingBehavior kind = OptimisticLockingBehavior.NoLocking;
				for(XPClassInfo bas = IdClass; bas != null; bas = bas.BaseClass) {
					OptimisticLockingAttribute attribute = (OptimisticLockingAttribute)bas.FindAttributeInfo(typeof(OptimisticLockingAttribute));
					if(attribute != null) {
						kind = attribute.LockingKind;
						break;
					}
				}
				if(name != null && kind == OptimisticLockingBehavior.ConsiderOptimisticLockingField) {
					optimisticLockingCache = new OptimisticLockingCacheItem(kind, new OptimisticLockField(this, name), new OptimisticLockFieldInDataLayer(this, OptimisticLockFieldInDataLayerName));
				}
				else {
					optimisticLockingCache = new OptimisticLockingCacheItem(kind, null, null);
				}
				bool deferredDeletionEnabled = false;
				for(XPClassInfo ci = this; ci != null; ci = ci.BaseClass) {
					DeferredDeletionAttribute dda = (DeferredDeletionAttribute)ci.FindAttributeInfo(typeof(DeferredDeletionAttribute));
					if(dda != null) {
						deferredDeletionEnabled = dda.Enabled;
						break;
					}
				}
				if(deferredDeletionEnabled)
					new GCRecordField(this);
			}
			else {
				if(BaseClass != null && IdClass != null && !BaseClass.IsTypedObject) {
					new ObjectTypeField(IdClass);
					IdClass.isTypedObject = true;
				}
			}
		}
		protected void CheckMembers() {
			bool keyFound = false;
			Dictionary<string, object> mappings = new Dictionary<string, object>();
			foreach(XPMemberInfo mi in PersistentProperties) {
				if(mappings.ContainsKey(mi.MappingField))
					throw new InvalidOperationException(Res.GetString(Res.Metadata_DuplicateMappingField, mi.MappingField, FullName));
				mappings.Add(mi.MappingField, null);
				if(mi.IsKey) {
					if(keyFound)
						throw new DuplicateKeyPropertyException(FullName);
					keyFound = true;
				}
			}
			foreach(XPMemberInfo mi in Members) {
				if(mi.IsKey && (!mi.IsPersistent || mi.IsReadOnly))
					throw new InvalidOperationException(Res.GetString(Res.Metadata_NonPersistentKey, this.FullName, mi.Name));
				if(mi.Converter != null && (mi.IsKey || mi.ReferenceType != null))
					throw new InvalidOperationException(Res.GetString(Res.Metadata_ConverterOnKeyOrReference, this.FullName, mi.Name));
				if(mi.IsKey) {
					foreach(XPMemberInfo subKey in mi.SubMembers) {
						if(subKey.Converter != null)
							throw new InvalidOperationException(Res.GetString(Res.Metadata_ConverterOnKeyOrReference, this.FullName, subKey.Name));
					}
				}
				if(mi.IsAliased && Dictionary.UseStrictMetadataValidation) {
					PersistentAliasAttribute att = (PersistentAliasAttribute)mi.GetAttributeInfo(typeof(PersistentAliasAttribute));
					DevExpress.Data.Filtering.OperandProperty prop = att.Criteria as DevExpress.Data.Filtering.OperandProperty;
					if(!ReferenceEquals(prop, null)) {
						if(prop.PropertyName == mi.Name) {
							throw new InvalidOperationException(Res.GetString(Res.Metadata_PersistentAliasCircular, mi.Owner.FullName, mi.Name, att.Criteria));
						}
					}
				}
				if(mi.IsFetchOnly) {
					if(!mi.IsPersistent || mi.IsCollection || mi.IsKey
						|| mi.IsAssociation || mi.IsAssociationList || mi.IsAliased || mi.IsManyToMany || mi.IsManyToManyAlias
						|| (mi.ValueParent != null && mi.ValueParent.IsStruct)) {
						throw new InvalidOperationException(Res.GetString(Res.Metadata_FetchOnlyAttributeNotApplicable, mi.Owner.FullName, mi.Name));
					}
				}
			}
		}
		public XPMemberInfo FindMember(string memberName) {
			if(memberName == null)
				return null;
			for(XPClassInfo ci = this; ci != null; ci = ci.BaseClass) {
				XPMemberInfo member = ci.QueryOwnMember(memberName);
				if(member != null)
					return member;
			}
			if(IsInterface) {
				foreach(Type t in this.ClassType.GetInterfaces()) {
					XPClassInfo ci = Dictionary.QueryClassInfo(t);
					if(ci == null)
						continue;
					XPMemberInfo member = ci.QueryOwnMember(memberName);
					if(member != null)
						return member;
				}
			}
			return null;
		}
		public XPMemberInfo GetMember(string memberName) {
			XPMemberInfo rv = FindMember(memberName);
			if(rv != null)
				return rv;
			else
				throw new PropertyMissingException(this.FullName, memberName == null ? "<null>" : memberName);
		}
		Dictionary<string, MemberInfoCollection> cachedpaths;
		internal MemberInfoCollection ParsePersistentPath(string path) {
			MemberInfoCollection col = ParsePath(path);
			if(col.HasNonPersistent)
				throw new InvalidPropertyPathException(Res.GetString(Res.MetaData_IncorrectPathMemberNotExists, FullName, path, String.Empty, String.Empty));
			return col;
		}
		[EditorBrowsable(EditorBrowsableState.Never)]
		public MemberInfoCollection ParsePath(string path) {
			MemberInfoCollection col;
			if(cachedpaths == null || !cachedpaths.TryGetValue(path, out col)) {
				col = new MemberInfoCollection(this, path, true);
				lock(this) {
					if(cachedpaths == null)
						cachedpaths = new Dictionary<string, MemberInfoCollection>();
					if(!cachedpaths.ContainsKey(path))
						cachedpaths.Add(path, col);
				}
			}
			return col;
		}
		public XPMemberInfo GetPersistentMember(string memberName) {
			foreach(XPMemberInfo mi in PersistentProperties) {
				if(mi.Name == memberName) return mi;
			}
			return null;
		}
		[Description("When implemented in a derived class, returns the full name of the class.")]
		public abstract string FullName { get; }
		protected static XPClassInfo[] EmptyClassInfos = Array.Empty<XPClassInfo>();
		public override string ToString() {
			return FullName;
		}
		[Description("Gets the name of the table in the data layer in which the object’s data is stored.")]
		public string TableName {
			get {
				if(tableName == null)
					tableName = GetTableName();
				return tableName;
			}
		}
		protected virtual string GetDefaultTableName() {
			return this.FullName;
		}
		protected virtual string GetTableName() {
			if(IsPersistent && GetTableMapType() == MapInheritanceType.OwnTable) {
				PersistentAttribute attribute = (PersistentAttribute)FindAttributeInfo(typeof(PersistentAttribute));
				if(attribute != null && attribute.MapTo != null && attribute.MapTo.Length > 0)
					return attribute.MapTo;
				else
					return GetDefaultTableName();
			}
			if(IsPersistent && GetTableMapType() != MapInheritanceType.OwnTable && IdClass == this) {
				throw new InvalidOperationException(Res.GetString(Res.Metadata_YouCannotApplyTheMapInheritanceParentTable));
			}
			if(BaseClass != null)
				return BaseClass.TableName;
			return null;
		}
		ICollection directlyRefTypes = null;
		static void AddClassToDirectlyRefTypes(Dictionary<XPClassInfo, object> targetList, XPClassInfo info, string refClassName, string refMemberName) {
			try {
				info.CheckAbstractReference();
			}
			catch(Exception e) {
				throw new UnableToFillRefTypeException(refClassName, refMemberName, e);
			}
			if(!targetList.ContainsKey(info))
				targetList.Add(info, null);
		}
		ICollection GetDirectlyRefTypes() {
			if(directlyRefTypes == null) {
				Dictionary<XPClassInfo, object> tmpOwnRefTypes = new Dictionary<XPClassInfo, object>();
				if(this.PersistentBaseClass != null) {
					AddClassToDirectlyRefTypes(tmpOwnRefTypes, this.PersistentBaseClass, this.FullName, this.KeyProperty.Name);
					if(this.TableMapType == MapInheritanceType.ParentTable) {
						foreach(XPMemberInfo mi in this.OwnMembers) {
							if(mi.IsPersistent && mi.ReferenceType != null) {
								lock(this.PersistentBaseClass) {
									if(this.PersistentBaseClass.childrenWithRelationsMappedToMe == null) {
										this.PersistentBaseClass.childrenWithRelationsMappedToMe = new List<XPClassInfo>();
									}
									if(!this.PersistentBaseClass.childrenWithRelationsMappedToMe.Contains(this)) {
										this.PersistentBaseClass.childrenWithRelationsMappedToMe.Add(this);
									}
								}
								break;
							}
						}
					}
				}
				foreach(XPMemberInfo mi in ObjectProperties) {
					AddClassToDirectlyRefTypes(tmpOwnRefTypes, mi.ReferenceType, this.FullName, mi.Name);
				}
				foreach(XPMemberInfo mi in AssociationListProperties) {
					AddClassToDirectlyRefTypes(tmpOwnRefTypes, mi.CollectionElementType, this.FullName, mi.Name);
					if(mi.IsManyToMany)
						AddClassToDirectlyRefTypes(tmpOwnRefTypes, mi.IntermediateClass, this.FullName, mi.Name);
				}
				if(IsGCRecordObject) {
					AddClassToDirectlyRefTypes(tmpOwnRefTypes, Dictionary.GetClassInfo(typeof(XPObjectType)), this.FullName, GCRecordField.StaticName);
				}
				directlyRefTypes = ListHelper.FromCollection(tmpOwnRefTypes.Keys);
			}
			return directlyRefTypes;
		}
		List<XPClassInfo> childrenWithRelationsMappedToMe;
		void FillRefTypes(Dictionary<XPClassInfo, object> filled) {
			this.CheckAbstractReference();
			if(filled.ContainsKey(this))
				return;
			filled.Add(this, null);
			foreach(XPClassInfo refType in GetDirectlyRefTypes()) {
				refType.FillRefTypes(filled);
			}
			if(childrenWithRelationsMappedToMe != null) {
				foreach(XPClassInfo child in childrenWithRelationsMappedToMe) {
					child.FillRefTypes(filled);
				}
			}
		}
		public ICollection GetRefTypes() {
			Dictionary<XPClassInfo, object> refTypes = new Dictionary<XPClassInfo, object>();
			this.FillRefTypes(refTypes);
			ICollection result = ListHelper.FromCollection(refTypes.Keys);
			return result;
		}
		volatile DBTable table;
		[Description("Gets the DBTable object which the public properties and public fields are saved in.")]
		public DBTable Table {
			get {
				if(table == null) {
					if(TableName == null)
						return null;
					lock(this) {
						if(table == null)
							CreateTable();
					}
				}
				return table;
			}
		}
		void CreateTable() {
			DBTable tmpTable;
			if(BaseClass != null && BaseClass.TableName == this.TableName)
				tmpTable = BaseClass.Table;
			else
				tmpTable = new DBTable(TableName);
			if(IsPersistent)
				DBTableHelper.ProcessClassInfo(tmpTable, this);
			table = tmpTable;
		}
		[Description("Determines whether an instance of the current type is abstract.")]
		public virtual bool IsAbstract { get { return ClassType != null && ClassType.IsAbstract; } }
		[Description("When implemented in a derived class, gets the type of the class which is described by the current XPClassInfo object.")]
		public abstract Type ClassType { get; }
		protected internal virtual Type RealInstanceType { get { return ClassType ?? typeof(object); } }
		public virtual bool CanGetByClassType { get { return true; } }
		[Description("Gets information on the key property or key field.")]
		public XPMemberInfo KeyProperty {
			get {
				if((key == null) && (IdClass != null)) {
					key = IdClass.GetKeyPropertyOfIdClass();
					if(key == null)
						throw new KeyPropertyAbsentException(IdClass.FullName);
				}
				return key;
			}
		}
		[Description("Gets a value that specifies which table persistent properties and fields are saved to.")]
		public MapInheritanceType TableMapType { get { return GetTableMapType(); } }
		[Description("When implemented in a derived class, gets the metadata information of the base class.")]
		public abstract XPClassInfo BaseClass { get; }
		[Description("Gets the information about the nearest persistent parent class in the inheritance hierarchy.")]
		public XPClassInfo PersistentBaseClass {
			get {
				for(XPClassInfo bc = BaseClass; bc != null; bc = bc.BaseClass)
					if(bc.IsPersistent) return bc;
				return null;
			}
		}
		XPClassInfo idClass;
		[Description("Gets the metadata information for the persistent class which provides the key value for the current object.")]
		public XPClassInfo IdClass {
			get {
				if(idClass == null) {
					XPClassInfo type = this;
					while(type != null) {
						if(type.IsPersistent)
							idClass = type;
						type = type.BaseClass;
					}
				}
				return idClass;
			}
		}
		bool IsAssignableToCore(XPClassInfo classInfo) {
			if(classInfo == this)
				return true;
			if(BaseClass == null)
				return false;
			return BaseClass.IsAssignableToCore(classInfo);
		}
		public bool IsAssignableTo(XPClassInfo classInfo) {
			if(classInfo == null)
				return false;
			if(IsAssignableToCore(classInfo))
				return true;
			if(classInfo.IsInterface && this.ClassType != null) {
				foreach(Type i in this.ClassType.GetInterfaces())
					if(i == classInfo.ClassType)
						return true;
			}
			return false;
		}
		protected virtual bool IsInterface { get { return false; } }
		IEnumerable persistentMembers;
		[Description("Gets the collection of persistent properties and fields owned by the current persistent class metadata information.")]
		public IEnumerable PersistentProperties {
			get {
				if(persistentMembers == null) {
					List<XPMemberInfo> list = new List<XPMemberInfo>();
					foreach(XPMemberInfo mi in Members) {
						if(mi.IsPersistent && mi.ValueParent == null)
							list.Add(mi);
					}
					persistentMembers = list.Count == 0 ? EmptyEnumerable.Instance : list;
				}
				return persistentMembers;
			}
		}
		bool hasDelayedProperties;
		MemberInfoCollection propertiesForInsert;
		MemberInfoCollection propertiesForUpdate;
		MemberInfoCollection GetPropertiesListForUpdateInsert(object theObject, bool isUpdate, bool addDelayedReference) {
			if(isUpdate) {
				if(propertiesForUpdate == null) {
					MemberInfoCollection list = new MemberInfoCollection(this);
					foreach(XPMemberInfo m in PersistentProperties) {
						if(m is ObjectTypeField || m.IsKey)
							continue;
						if(m.IsDelayed) {
							hasDelayedProperties = true;
							XPDelayedProperty delayedContainer = XPDelayedProperty.GetDelayedPropertyContainer(theObject, m);
							if(XPDelayedProperty.UpdateModifiedOnly(m)) {
								if(!delayedContainer.IsModified)
									continue;
							}
							else {
								if(!delayedContainer.IsLoaded) {
									if(!addDelayedReference || m.ReferenceType == null)
										continue;
								}
							}
						}
						if(m.IsFetchOnly) {
							if(m.ReferenceType != null) {
								throw new InvalidOperationException(Res.GetString(Res.Metadata_FetchOnlyAttributeNotApplicableToReference, m.Owner.FullName, m.Name));
							}
							continue;
						}
						list.Add(m);
					}
					if(!hasDelayedProperties) {
						propertiesForUpdate = list;
					}
					return list;
				}
				return propertiesForUpdate;
			}
			if(propertiesForInsert == null) {
				MemberInfoCollection list = new MemberInfoCollection(this);
				foreach(XPMemberInfo m in PersistentProperties) {
					if(m.IsFetchOnly) {
						if(m.ReferenceType != null) {
							throw new InvalidOperationException(Res.GetString(Res.Metadata_FetchOnlyAttributeNotApplicableToReference, m.Owner.FullName, m.Name));
						}
						continue;
					}
					list.Add(m);
				}
				propertiesForInsert = list;
				return list;
			}
			return propertiesForInsert;
		}
		public static MemberInfoCollection GetPropertiesListForUpdateInsert(Session session, object theObject, bool isUpdate, bool addDelayedReference) {
			XPClassInfo ci = session.GetClassInfo(theObject);
			return ci.GetPropertiesListForUpdateInsert(theObject, isUpdate, addDelayedReference);
		}
		public abstract bool HasModifications(object theObject);
		public abstract void ClearModifications(object theObject);
		protected class EmptyEnumerable : IEnumerable, IEnumerator {
			public static readonly IEnumerable Instance = new EmptyEnumerable();
			public IEnumerator GetEnumerator() {
				return this;
			}
			public object Current {
				get { throw new InvalidOperationException(); }
			}
			public bool MoveNext() {
				return false;
			}
			public void Reset() {
			}
		}
		[Description("Returns the IEnumerable interface which populates XPMemberInfo objects for properties with the IXPSimpleObject interface declaration.")]
		public IEnumerable ObjectProperties {
			get {
				if(objects == null) {
					List<XPMemberInfo> list = new List<XPMemberInfo>();
					foreach(XPMemberInfo mi in Members) {
						if(mi.IsPersistent && mi.ReferenceType != null)
							list.Add(mi);
					}
					objects = list.Count == 0 ? EmptyEnumerable.Instance : list;
				}
				return objects;
			}
		}
		[Description("Gets the IEnumerable interface which populates XPMemberInfo objects for the XPCollection type properties.")]
		public IEnumerable CollectionProperties {
			get {
				if(collections == null) {
					List<object> list = new List<object>();
					foreach(XPMemberInfo mi in AssociationListProperties) {
						if(mi.IsCollection)
							list.Add(mi);
					}
					collections = list.Count == 0 ? EmptyEnumerable.Instance : list;
				}
				return collections;
			}
		}
		[Description("Gets a list of members that represent the “many” side of the association.")]
		public IEnumerable AssociationListProperties {
			get {
				if(assocLists == null) {
					List<XPMemberInfo> list = new List<XPMemberInfo>();
					foreach(XPMemberInfo mi in Members) {
						if(mi.IsAssociationList)
							list.Add(mi);
					}
					assocLists = list.Count == 0 ? EmptyEnumerable.Instance : list;
				}
				return assocLists;
			}
		}
		public void CheckAbstractReference() {
			if(!IsPersistent)
				throw new NonPersistentReferenceFoundException(FullName);
		}
		[Description("Gets the XPDictionary object which the current XPClassInfo object belongs to.")]
		public XPDictionary Dictionary {
			get { return dictionary; }
		}
		[Description("Gets a collection of XPMemberInfo objects that provide metadata information on all the members owned by the class.")]
		public abstract ICollection<XPMemberInfo> OwnMembers { get; }
		bool isTypedObject = false;
		internal bool IsTypedObject {
			get {
				if(IdClass == null)
					return isTypedObject;
				return IdClass.isTypedObject;
			}
		}
		[Description("Gets whether the class described by the current XPClassInfo object has descendants.")]
		public bool HasDescendants {
			get {
				return Dictionary.HasDescendants(this);
			}
		}
		internal bool IsGCRecordObject {
			get {
				return FindMember(GCRecordField.StaticName) != null;
			}
		}
		internal bool IsDesignTimeReflection {
			get {
				return Dictionary is DesignTimeReflection;
			}
		}
		internal bool HasPurgebleObjectReferences() {
			foreach(XPMemberInfo mi in ObjectProperties) {
				if(mi.ReferenceType.IsGCRecordObject)
					return true;
			}
			return false;
		}
		[Description("Gets the name of the assembly that the class is declared in.")]
		public abstract string AssemblyName { get; }
		[Obsolete("Use session.IsObjectsLoading instead", true), EditorBrowsable(EditorBrowsableState.Never)]
		public bool IsObjectLoading {
			get {
				return false;
			}
		}
		[Obsolete("Use session.IsObjectsLoading instead", true), EditorBrowsable(EditorBrowsableState.Never)]
		public bool GetObjectLoading(Session session) {
			return session.IsObjectsLoading;
		}
		public virtual object CreateObject(Session session) {
			if(session.IsObjectsLoading) {
				return CreateObjectInstance(session, this);
			}
			else {
				SessionStateStack.Enter(session, SessionState.CreateObjectLoadingEnforcer);
				try {
					return CreateObjectInstance(session, this);
				}
				finally {
					SessionStateStack.Leave(session, SessionState.CreateObjectLoadingEnforcer);
				}
			}
		}
		public virtual object CreateNewObject(Session session) {
			return CreateObjectInstance(session, this);
		}
		protected internal abstract object CreateObjectInstance(Session session, XPClassInfo instantiationClassInfo);
		public XPClassInfo(XPDictionary dictionary) {
			this.dictionary = dictionary;
		}
		protected internal virtual bool SupportObjectsReferencesFromCustomMembers { get { return typeof(IXPCustomPropertyStore).IsAssignableFrom(ClassType); } }
		XPCustomMemberInfo CreateMember(string propertyName, Type propertyType, XPClassInfo referenceType, bool nonPersistent, bool nonPublic, params Attribute[] attributes) {
			XPCustomMemberInfo newMemberInfo;
			if((!SupportObjectsReferencesFromCustomMembers
 && !(this is XPDataTableClassInfo)
) && (referenceType != null || Dictionary.QueryClassInfo(propertyType) != null || typeof(XPBaseCollection).IsAssignableFrom(propertyType)))
				throw new ArgumentException(Res.GetString(Res.Metadata_CustomProperties_ReferenceOrCollectionInSessionStore, FullName, propertyName, typeof(IXPCustomPropertyStore).FullName));
			newMemberInfo = new XPCustomMemberInfo(this, propertyName, propertyType, referenceType, nonPersistent, nonPublic);
			foreach(Attribute attribute in attributes)
				newMemberInfo.AddAttribute(attribute);
			return newMemberInfo;
		}
		public XPCustomMemberInfo CreateMember(string propertyName, Type propertyType, bool nonPersistent, bool nonPublic, params Attribute[] attributes) {
			return CreateMember(propertyName, propertyType, null, nonPersistent, nonPublic, attributes);
		}
		public XPCustomMemberInfo CreateMember(string propertyName, Type propertyType, bool nonPersistent, params Attribute[] attributes) {
			return CreateMember(propertyName, propertyType, nonPersistent, false, attributes);
		}
		public XPCustomMemberInfo CreateMember(string propertyName, Type propertyType, params Attribute[] attributes) {
			return CreateMember(propertyName, propertyType, false, attributes);
		}
		public XPCustomMemberInfo CreateMember(string propertyName, XPClassInfo referenceType, bool nonPersistent, bool nonPublic, params Attribute[] attributes) {
			return CreateMember(propertyName, referenceType.ClassType, referenceType, nonPersistent, nonPublic, attributes);
		}
		public XPCustomMemberInfo CreateMember(string propertyName, XPClassInfo referenceType, bool nonPersistent, params Attribute[] attributes) {
			return CreateMember(propertyName, referenceType, nonPersistent, false, attributes);
		}
		public XPCustomMemberInfo CreateMember(string propertyName, XPClassInfo referenceType, params Attribute[] attributes) {
			return CreateMember(propertyName, referenceType, false, attributes);
		}
		public object GetId(object obj) {
			XPMemberInfo key = this.KeyProperty;
			if(key == null)
				return null;
			return key.ExpandId(key.GetValue(obj));
		}
		public EvaluatorContextDescriptor GetEvaluatorContextDescriptor() {
			if(_evaluatorContextDescriptor == null) {
				_evaluatorContextDescriptor = new EvaluatorContextDescriptorXpo(this);
			}
			return _evaluatorContextDescriptor;
		}
		public CriteriaCompilerDescriptor GetCriteriaCompilerDescriptor(Session session) {
			return new CriteriaCompilerDescriptorXpo(this, session);
		}
		public EvaluatorContextDescriptor GetEvaluatorContextDescriptorInTransaction() {
			if(_evaluatorContextDescriptorInTransaction == null) {
				_evaluatorContextDescriptorInTransaction = new EvaluatorContextDescriptorXpo(this, true);
			}
			return _evaluatorContextDescriptorInTransaction;
		}
		internal void TouchRecursive(Dictionary<XPClassInfo, XPClassInfo> processedClassInfos) {
			if(processedClassInfos.ContainsKey(this))
				return;
			processedClassInfos.Add(this, this);
			if(this.BaseClass != null)
				this.BaseClass.TouchRecursive(processedClassInfos);
			foreach(XPMemberInfo mi in new List<XPMemberInfo>(this.OwnMembers)) {
				if(mi.ReferenceType != null)
					mi.ReferenceType.TouchRecursive(processedClassInfos);
				if(mi.IsAssociationList) {
					if(mi.IsManyToMany)
						mi.IntermediateClass.TouchRecursive(processedClassInfos);
					else
						mi.CollectionElementType.TouchRecursive(processedClassInfos);
				}
			}
		}
		static public string GetShortAssemblyName(Assembly assembly) {
			int pos = assembly.FullName.IndexOf(',');
			return pos < 0 ? assembly.FullName : assembly.FullName.Substring(0, pos);
		}
		XPDictionary IXPDictionaryProvider.Dictionary {
			get { return Dictionary; }
		}
		XPClassInfo IXPClassInfoProvider.ClassInfo {
			get { return this; }
		}
	}
	public abstract class XPMemberInfo : XPTypeInfo {
		protected static readonly List<XPMemberInfo> EmptyList = new List<XPMemberInfo>(0);
		protected List<XPMemberInfo> subMembersArray = EmptyList;
		protected override void DropCache() {
			mappingField = null;
			isDelayedCached = false;
			isAliasedCached = false;
			collectionElementType = null;
			isCollection = null;
			isAssociationList = null;
			isNonAssociationList = null;
			isManyToManyAlias = null;
			isNullableCached = false;
			isDefaultValueCached = false;
			isDbDefaultValueCached = false;
			isFetchOnly = null;
			isMappingFieldInitialized = false;
			base.DropCache();
			Owner.DoDrop();
		}
		bool isReadOnly;
		[Description("Gets whether the member is read-only.")]
		public bool IsReadOnly { get { return isReadOnly; } }
		[Description("Gets whether the member represents a key member.")]
		public bool IsKey {
			get {
				return HasAttribute(typeof(KeyAttribute));
			}
		}
		[Description("Gets whether the member is the auto-generated key.")]
		public bool IsAutoGenerate {
			get {
				KeyAttribute key = (KeyAttribute)FindAttributeInfo(typeof(KeyAttribute));
				if(key == null)
					return false;
				return key.AutoGenerate;
			}
		}
		[Description("Gets whether the member is an auto-generated integer key.")]
		public bool IsIdentity {
			get {
				return (StorageType == typeof(int) || StorageType == typeof(long)) && IsAutoGenerate;
			}
		}
		bool? isNullable;
		bool isNullableCached;
		public bool? IsNullable {
			get {
				if(!isNullableCached) {
					NullableAttribute attr = (NullableAttribute)FindAttributeInfo(typeof(NullableAttribute));
					if(attr != null) {
						isNullable = attr.IsNullable;
					}
					else {
						isNullable = null;
					}
					isNullableCached = true;
				}
				return isNullable;
			}
		}
		object defaultValue;
		bool isDefaultValueCached;
		public object DefaultValue {
			get {
				if(!isDefaultValueCached) {
					ColumnDefaultValueAttribute attr = (ColumnDefaultValueAttribute)FindAttributeInfo(typeof(ColumnDefaultValueAttribute));
					if(attr != null) {
						defaultValue = attr.DefaultValue;
					}
					else {
						defaultValue = null;
					}
					isDefaultValueCached = true;
				}
				return defaultValue;
			}
		}
		string dbDefaultValue;
		bool isDbDefaultValueCached;
		public string DbDefaultValue {
			get {
				if(!isDbDefaultValueCached) {
					ColumnDbDefaultValueAttribute attr = (ColumnDbDefaultValueAttribute)FindAttributeInfo(typeof(ColumnDbDefaultValueAttribute));
					if(attr != null) {
						dbDefaultValue = attr.DbDefaultValue;
					}
					else {
						dbDefaultValue = null;
					}
					isDbDefaultValueCached = true;
				}
				return dbDefaultValue;
			}
		}
		bool? isFetchOnly;
		public bool IsFetchOnly {
			get {
				if(!isFetchOnly.HasValue) {
					FetchOnlyAttribute attr = (FetchOnlyAttribute)FindAttributeInfo(typeof(FetchOnlyAttribute));
					isFetchOnly = (attr != null);
				}
				return isFetchOnly.Value;
			}
		}
		[Description("Gets a collection of sub members.")]
		public IList SubMembers { get { return subMembersArray; } }
		protected XPMemberInfo valueParent;
		internal XPMemberInfo ValueParent { get { return valueParent; } }
		bool isDelayedCached;
		bool isDelayed;
		[Description("Gets whether the property is marked as delayed.")]
		public bool IsDelayed {
			get {
				if(!isDelayedCached) {
					isDelayed = HasAttribute(typeof(DelayedAttribute));
					isDelayedCached = true;
				}
				return isDelayed;
			}
		}
		bool isAliasedCached;
		bool isAliased;
		[Description("Gets whether a PersistentAliasAttribute attribute is applied to the property.")]
		public bool IsAliased {
			get {
				if(!isAliasedCached) {
					isAliased = HasAttribute(typeof(PersistentAliasAttribute));
					isAliasedCached = true;
				}
				return isAliased;
			}
		}
		[Description("When implemented in a derived class, gets whether the member is declared as public.")]
		public abstract bool IsPublic { get; }
		[Description("When implemented in a derived class, gets the member’s type.")]
		public abstract Type MemberType { get; }
		[Description("Gets the value converter.")]
		public ValueConverter Converter {
			get {
				ValueConverterAttribute valueConverterAttribute = (ValueConverterAttribute)FindAttributeInfo(typeof(ValueConverterAttribute));
				if(valueConverterAttribute != null)
					return valueConverterAttribute.Converter;
				return Owner.Dictionary.GetConverter(MemberType);
			}
		}
		[Description("Gets the type that the member’s value is saved as in the data store.")]
		public Type StorageType {
			get {
				ValueConverter converter = Converter;
				if(converter != null)
					return converter.StorageType;
				if(MemberType != null) {
					Type underlyingNullableType = Nullable.GetUnderlyingType(MemberType);
					if(underlyingNullableType != null)
						return underlyingNullableType;
				}
				return MemberType;
			}
		}
		string mappingField;
		[Description("Gets the column’s name in the data store which the member’s value is stored in.")]
		public string MappingField {
			get {
				if(mappingField == null && (IsPersistent || IsStruct)) {
					PersistentAttribute attribute = (PersistentAttribute)FindAttributeInfo(PersistentAttribute.AttributeType);
					if(attribute != null)
						mappingField = attribute.MapTo;
					if(mappingField == null || (mappingField.Length == 0 && (ReferenceType == null || ReferenceType.KeyProperty == null || !ReferenceType.KeyProperty.IsStruct)))
						mappingField = GetDefaultMappingField();
				}
				return mappingField;
			}
		}
		protected virtual string GetDefaultMappingField() {
			return Name;
		}
		bool isMappingFieldInitialized = false;
		void InitializeMappingField() {
			InitializeMappingFieldSize();
			InitializeMappingFieldDBType();
			InitializeMappingFieldDBTypeName();
			isMappingFieldInitialized = true;
		}
		void InitializeMappingFieldSize() {
			if(StorageType == typeof(string)) {
				SizeAttribute attribute = (SizeAttribute)FindAttributeInfo(typeof(SizeAttribute));
				if(attribute != null) {
					mappingFieldSize = attribute.Size;
				}
				else {
					mappingFieldSize = XpoDefault.DefaultStringMappingFieldSize;
				}
			}
			else if(ReferenceType != null && ReferenceType.KeyProperty != null) {
				mappingFieldSize = ReferenceType.KeyProperty.MappingFieldSize;
			}
			else {
				mappingFieldSize = 0;
			}
		}
		void InitializeMappingFieldDBType() {
			if(IsStruct || IsAssociationList || IsManyToMany || IsManyToManyAlias) {
				mappingFieldDBType = DBColumnType.Unknown;
			}
			else if(ReferenceType != null && ReferenceType.KeyProperty != null) {
				mappingFieldDBType = ReferenceType.KeyProperty.MappingFieldDBType;
			}
			else {
				mappingFieldDBType = DBColumn.GetColumnType(StorageType, true);
			}
		}
		void InitializeMappingFieldDBTypeName() {
			if(ReferenceType == null || ReferenceType.KeyProperty == null) {
				DbTypeAttribute attribute = (DbTypeAttribute)FindAttributeInfo(typeof(DbTypeAttribute));
				if(attribute != null) {
					mappingFieldDBTypeName = attribute.DbColumnTypeName;
				}
				else {
					mappingFieldDBTypeName = null;
				}
			}
			else {
				mappingFieldDBTypeName = ReferenceType.KeyProperty.MappingFieldDBTypeName;
			}
		}
		int mappingFieldSize;
		[Description("Gets the maximum number of characters that can be stored in a field which the member is mapped to.")]
		public int MappingFieldSize {
			get {
				if(!isMappingFieldInitialized) {
					InitializeMappingField();
				}
				return mappingFieldSize;
			}
		}
		DBColumnType mappingFieldDBType;
		[Description("Gets the XPMemberInfo.MappingField‘s data type.")]
		public DBColumnType MappingFieldDBType {
			get {
				if(!isMappingFieldInitialized) {
					InitializeMappingField();
				}
				return mappingFieldDBType;
			}
		}
		string mappingFieldDBTypeName;
		[Description("Gets the XPMemberInfo.MappingField‘s SQL type name.")]
		public string MappingFieldDBTypeName {
			get {
				if(!isMappingFieldInitialized) {
					InitializeMappingField();
				}
				return mappingFieldDBTypeName;
			}
		}
		public virtual object GetConst(object target, XPMemberInfo targetMember) { return target; }
		public abstract object GetValue(object theObject);
		public abstract void SetValue(object theObject, object theValue);
		public abstract bool GetModified(object theObject);
		public abstract object GetOldValue(object theObject);
		public abstract void SetModified(object theObject, object oldValue);
		public abstract void ResetModified(object theObject);
		XPClassInfo owner;
		[Description("Gets the XPClassInfo object which owns this XPMemberInfo object.")]
		public XPClassInfo Owner { get { return owner; } }
		protected XPMemberInfo(XPClassInfo owner, bool isReadOnly) {
			this.owner = owner;
			this.isReadOnly = isReadOnly;
		}
		public bool IsMappingClass(XPClassInfo branch) {
			XPClassInfo mci = GetMappingClass(branch);
			if(mci == null)
				return false;
			return branch.TableName == mci.TableName;
		}
		[Description("When implemented in a derived class, gets the member’s name.")]
		public abstract string Name { get; }
		[Description("Gets the member’s display name.")]
		public string DisplayName {
			get {
				foreach(Attribute attribute in Attributes) {
					DisplayNameAttribute display = attribute as DisplayNameAttribute;
					if(display != null)
						return display.DisplayName;
				}
				foreach(Attribute attribute in Attributes) {
					System.ComponentModel.DisplayNameAttribute display = attribute as System.ComponentModel.DisplayNameAttribute;
					if(display != null)
						return display.DisplayName;
				}
				return String.Empty;
			}
		}
		public override string ToString() {
			return Name;
		}
		public XPClassInfo GetMappingClass(XPClassInfo branch) {
			if(!branch.IsAssignableTo(Owner))
				return null;
			if(!branch.IsPersistent)
				return null;
			if(IsKey)
				return branch;
			if(valueParent != null)
				return valueParent.GetMappingClass(branch);
			if(Owner.IsPersistent)
				return Owner;
			XPClassInfo firstPersistentOwnerDescendant = branch;
			for(XPClassInfo current = firstPersistentOwnerDescendant.BaseClass; current != Owner; current = current.BaseClass) {
				if(current.IsPersistent)
					firstPersistentOwnerDescendant = current;
			}
			return firstPersistentOwnerDescendant;
		}
		XPMemberInfo associatedMember;
		public XPMemberInfo GetAssociatedMember() {
			if(associatedMember == null) {
				AssociationAttribute myAssAtt = GetAssociationAttributeInfo();
				string assName = myAssAtt.Name;
				if(assName == null)
					assName = string.Empty;
				if(!this.Owner.IsPersistent)
					throw new AssociationInvalidException(Res.GetString(Res.Metadata_AssociationInvalid_NonPersistentClassInTheAssociation, Owner.FullName, this.Name, Owner.FullName));
				XPClassInfo refCi;
				if(IsAssociationList) {
					refCi = CollectionElementType;
				}
				else if(ReferenceType != null && IsPersistent) {
					refCi = ReferenceType;
				}
				else {
					throw new AssociationInvalidException(Res.GetString(Res.Metadata_AssociationInvalid_AssociationAttributeOnlyForListOrReference, assName, Owner.FullName, this.Name));
				}
				if(!refCi.IsPersistent)
					throw new AssociationInvalidException(Res.GetString(Res.Metadata_AssociationInvalid_NonPersistentClassInTheAssociation, Owner.FullName, this.Name, refCi.FullName));
				XPMemberInfo result = null;
				foreach(XPMemberInfo mi in new List<XPMemberInfo>(refCi.OwnMembers)) {
					if(ReferenceEquals(this, mi))
						continue;
					if((mi.ReferenceType == null || !mi.IsPersistent) && !mi.IsAssociationList)
						continue;
					AssociationAttribute relAssAtt = mi.FindAssociationAttributeInfo();
					if(relAssAtt == null)
						continue;
					string relAssName = relAssAtt.Name;
					if(relAssName == null)
						relAssName = string.Empty;
					if(assName != relAssName)
						continue;
					XPClassInfo relRefCi = mi.IsAssociationList ? mi.CollectionElementType : mi.ReferenceType;
					if(!ReferenceEquals(relRefCi, this.Owner)) {
						if(assName.Length == 0)
							continue;
						throw new AssociationInvalidException(Res.GetString(Res.Metadata_AssociationInvalid_PropertyTypeMismatch, assName, this.Owner.FullName, this.Name, mi.Owner.FullName, mi.Name, relRefCi.FullName));
					}
					if(result != null)
						throw new AssociationInvalidException(Res.GetString(Res.Metadata_AssociationInvalid_MoreThenOneAssociatedMemberFound, assName, refCi.FullName, result.Name, mi.Name));
					if(!this.IsAssociationList && !mi.IsAssociationList)
						throw new AssociationInvalidException(Res.GetString(Res.Metadata_AssociationInvalid_NoAssociationListInAssociation, assName, this.Owner.FullName, this.Name, mi.Owner.FullName, mi.Name));
					if(this.IsAssociationList && mi.IsAssociationList) {
						if(!(this.IsCollection && mi.IsCollection))
							throw new AssociationInvalidException(Res.GetString(Res.Metadata_AssociationInvalid_TwoAssociationListsInAssociation, assName, this.Owner.FullName, this.Name, mi.Owner.FullName, mi.Name));
					}
					if(relAssAtt.UseAssociationNameAsIntermediateTableName != myAssAtt.UseAssociationNameAsIntermediateTableName)
						throw new AssociationInvalidException(Res.GetString(Res.Metadata_AssociationInvalid_UseAssociationNameAsIntermediateTableNameMismatch, assName, this.Owner.FullName, this.Name, mi.Owner.FullName, mi.Name));
					result = mi;
				}
				if(result == null)
					throw new AssociationInvalidException(Res.GetString(Res.Metadata_AssociationInvalid_NotFound, assName, this.Owner.FullName, this.Name, refCi.FullName));
				associatedMember = result;
				if(this.IsCollection && associatedMember.IsCollection) {
					string intermediateTableNameOverload = myAssAtt.UseAssociationNameAsIntermediateTableName ? myAssAtt.Name : null;
					InitIntermediateClassInfo(associatedMember, intermediateTableNameOverload);
				}
			}
			return associatedMember;
		}
		[Obsolete("Use GetAssociatedMember method instead", true), EditorBrowsable(EditorBrowsableState.Never)]
		public XPMemberInfo GetAssociatedProperty() {
			if(IsAssociation)
				return GetAssociatedMember();
			else
				return null;
		}
		bool? isCollection;
		[Description("Gets whether the member represents a collection and is involved in associations.")]
		public bool IsCollection {
			get {
				if(!isCollection.HasValue)
					isCollection = IsAssociationList && (MemberType.IsSubclassOf(typeof(XPBaseCollection)));
				return isCollection.Value;
			}
		}
		bool? isAssociationList;
		[Description("Indicates whether the current member represents the “many” side of the association.")]
		public bool IsAssociationList {
			get {
				if(!isAssociationList.HasValue)
					isAssociationList = IsAssociation && !Owner.Dictionary.CanGetClassInfoByType(MemberType) && IsAssociationListType();
				return isAssociationList.Value;
			}
		}
		bool? isNonAssociationList;
		[Description("Indicates whether the current member represents a collection that is not decorated with AssociationAttribute.")]
		public bool IsNonAssociationList {
			get {
				if(!isNonAssociationList.HasValue)
					isNonAssociationList = !IsAssociation && !Owner.Dictionary.CanGetClassInfoByType(MemberType) && Owner.Dictionary.CanGetClassInfoByType(GetGenericIListTypeArgument(MemberType));
				return isNonAssociationList.Value;
			}
		}
		static Type GetGenericIListTypeArgument(Type elementType) {
			if(elementType == null)
				return null;
			if(elementType.IsInterface && elementType.IsGenericType && elementType.GetGenericTypeDefinition() == typeof(IList<>)) {
				return elementType.GetGenericArguments()[0];
			}
			foreach(Type iface in elementType.GetInterfaces()) {
				if(iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IList<>)) {
					return iface.GetGenericArguments()[0];
				}
			}
			return null;
		}
		bool IsAssociationListType() {
			if(typeof(IList).IsAssignableFrom(MemberType))
				return true;
			return Owner.Dictionary.CanGetClassInfoByType(GetGenericIListTypeArgument(MemberType));
		}
		bool? isManyToManyAlias;
		[Description("Indicates whether the ManyToManyAliasAttribute attribute is applied.")]
		public bool IsManyToManyAlias {
			get {
				if(!isManyToManyAlias.HasValue)
					isManyToManyAlias = !IsAssociationList && HasAttribute(typeof(ManyToManyAliasAttribute));
				return isManyToManyAlias.Value;
			}
		}
		XPClassInfo collectionElementType;
		[Description("Gets the XPClassInfo of the persistent object if the current member is a collection of these persistent objects.")]
		public XPClassInfo CollectionElementType {
			get {
				if(collectionElementType == null) {
					if(IsAssociationList) {
						AssociationAttribute elementType = FindAssociationAttributeInfo();
						if(elementType != null) {
							if(elementType.ElementType != null)
								collectionElementType = Owner.Dictionary.GetClassInfo(elementType.ElementType);
							if(collectionElementType == null) {
								if(elementType.ElementTypeName.Length > 0) {
									collectionElementType = Owner.Dictionary.GetClassInfo(elementType.AssemblyName, elementType.ElementTypeName);
								}
								else {
									Type genericIListType = GetGenericIListTypeArgument(MemberType);
									if(genericIListType != null) {
										collectionElementType = Owner.Dictionary.GetClassInfo(genericIListType);
									}
								}
							}
						}
						if(collectionElementType == null)
							throw new AssociationElementTypeMissingException(this.Name);
					}
					else if(IsNonAssociationList) {
						Type genericIListType = GetGenericIListTypeArgument(MemberType);
						collectionElementType = Owner.Dictionary.GetClassInfo(genericIListType);
					}
					else
						throw new InvalidOperationException(Res.GetString(Res.Metadata_NotCollection, this.Owner.FullName, this.Name));
				}
				return collectionElementType;
			}
		}
		[Obsolete("Use GetAssociatedMember method instead", true), EditorBrowsable(EditorBrowsableState.Never)]
		public XPMemberInfo GetAssociatedCollectionProperty() {
			return GetAssociatedMember();
		}
		public void ProcessAssociationRefChange(Session session, object referenceMemberOwner, object oldValue, object newValue) {
			ProcessAssociationRefChange(session, referenceMemberOwner, oldValue, newValue, false);
		}
		internal void ProcessAssociationRefChange(Session session, object referenceMemberOwner, object oldValue, object newValue, bool skipNonLoadedCollections) {
			if(ReferenceEquals(oldValue, newValue))
				return;
			if(this.ReferenceType == null)
				return;
			session.ThrowIfObjectFromDifferentSession(newValue);
			if(!this.IsAssociation)
				return;
			XPMemberInfo assocProperty = this.GetAssociatedMember();
			if(oldValue != null) {
				IList oldCollection = (IList)assocProperty.GetValue(oldValue);
				if(oldCollection != null) {
					if(skipNonLoadedCollections && assocProperty.IsCollection && !((XPBaseCollection)oldCollection).IsLoaded) {
					}
					else {
						if(assocProperty.IsCollection) {
							XPRefCollectionHelperOneToMany helper = XPRefCollectionHelper.GetRefCollectionHelperChecked<XPRefCollectionHelperOneToMany>((XPBaseCollection)oldCollection, oldValue, assocProperty);
							object bkp = helper.AssocRefChangeRemovingObject;
							helper.AssocRefChangeRemovingObject = referenceMemberOwner;
							try {
								oldCollection.Remove(referenceMemberOwner);
							}
							finally {
								helper.AssocRefChangeRemovingObject = bkp;
							}
						}
						else {
							oldCollection.Remove(referenceMemberOwner);
						}
					}
				}
			}
			if(newValue != null) {
				IList newCollection = (IList)assocProperty.GetValue(newValue);
				if(newCollection != null) {
					if(skipNonLoadedCollections && assocProperty.IsCollection && !((XPBaseCollection)newCollection).IsLoaded) {
					}
					else {
						newCollection.Add(referenceMemberOwner);
					}
				}
			}
		}
		protected AssociationAttribute FindAssociationAttributeInfo() {
			return (AssociationAttribute)FindAttributeInfo(typeof(AssociationAttribute));
		}
		protected AssociationAttribute GetAssociationAttributeInfo() {
			return (AssociationAttribute)GetAttributeInfo(typeof(AssociationAttribute));
		}
		[Description("Gets whether the member sets up the relation.")]
		public bool IsAssociation { get { return HasAttribute(typeof(AssociationAttribute)); } }
		[Description("Gets whether a member is involved in a many-to-many association.")]
		public bool IsManyToMany { get { return IntermediateClass != null; } }
		IntermediateClassInfo intermediateClass;
		[Description("This member supports the internal infrastructure and is not intended to be used directly from your code.")]
		public IntermediateClassInfo IntermediateClass {
			get {
				if(!IsCollection)
					return null;
				GetAssociatedMember();	
				return intermediateClass;
			}
		}
		void InitIntermediateClassInfo(XPMemberInfo relatedProperty, string tableNameOverload) {
			bool isLeft;
			string relationTableName = GetRelationTableName(out isLeft, relatedProperty, tableNameOverload);
			intermediateClass = (IntermediateClassInfo)Owner.Dictionary.QueryClassInfo(IntermediateClassInfo.IntermediateObjectAssemblyName, relationTableName);
			if(intermediateClass == null)
				intermediateClass = new IntermediateClassInfo(isLeft ? this : relatedProperty, isLeft ? relatedProperty : this, Owner.Dictionary, relationTableName);
		}
		string GetRelationTableName(out bool isLeft, XPMemberInfo relatedProperty, string tableNameOverload) {
			string refTable = CollectionElementType.TableName + Name;
			string ownerTable = relatedProperty.CollectionElementType.TableName + relatedProperty.Name;
			isLeft = ownerTable.CompareTo(refTable) > 0;
			if(tableNameOverload != null && tableNameOverload.Length > 0)
				return tableNameOverload;
			return String.Format(CultureInfo.InvariantCulture, isLeft ? "{0}_{1}" : "{1}_{0}",
				ownerTable, refTable);
		}
		[Obsolete("Use GetAssociatedMember method instead", true), EditorBrowsable(EditorBrowsableState.Never)]
		public XPMemberInfo ManyToManyRelatedProperty {
			get {
				if(IsAssociation && IsManyToMany)
					return GetAssociatedMember();
				else
					return null;
			}
		}
		bool isReferenceTypeCached = false;
		XPClassInfo referenceType;
		[Description("Gets the XPClassInfo of the referenced object if the member is a reference to another persistent object.")]
		public virtual XPClassInfo ReferenceType {
			get {
				if(!isReferenceTypeCached) {
					XPClassInfo ci = Owner.Dictionary.QueryClassInfo(MemberType);
					if(ci is XPDataTableClassInfo)
						ci = null;
					referenceType = ci;
					isReferenceTypeCached = true;
				}
				return referenceType;
			}
		}
		[Description("Gets whether the member represents a data structure.")]
		public virtual bool IsStruct { get { return false; } }
		public object ExpandId(object id) {
			if(this.SubMembers.Count > 0) {
				if(id is ArrayList)
					id = new IdList((ArrayList)id);
				else {
					if(id is List<object>) {
						id = id is IdList ? id : new IdList((List<object>)id);
					}
					else {
						IdList values = new IdList();
						foreach(XPMemberInfo mi in this.SubMembers) {
							if(mi.IsPersistent) {
								values.Add(mi.ExpandId(mi.GetConst(id, this)));
							}
						}
						id = values;
					}
				}
			}
			else {
				if(this.ReferenceType != null) {
					XPClassInfo idCi = Owner.Dictionary.QueryClassInfo(id);
					if(idCi != null) {
						id = this.ReferenceType.GetId(id);
					}
				}
			}
			return id;
		}
		[Description("Gets whether the member references other aggregated persistent objects.")]
		public bool IsAggregated { get { return HasAttribute(typeof(AggregatedAttribute)); } }
		public virtual Expression MakeGetExpression(Expression ownerExpression) {
			Expression body = Expression.Call(Expression.Constant(this), "GetValue", null, ownerExpression);
			if(MemberType != typeof(object)) {
				body = Expression.Convert(body, NullableHelpers.GetUnBoxedType(MemberType));
			}
			return body;
		}
		OptimisticLockingReadMergeBehavior? mergeCollisionBehavior;
		public OptimisticLockingReadMergeBehavior MergeCollisionBehavior {
			get {
				if(!mergeCollisionBehavior.HasValue) {
					MergeCollisionBehaviorAttribute attr = (MergeCollisionBehaviorAttribute)FindAttributeInfo(typeof(MergeCollisionBehaviorAttribute));
					if(attr == null) {
						mergeCollisionBehavior = OptimisticLockingReadMergeBehavior.Default;
					}
					else {
						mergeCollisionBehavior = attr.Behavior;
					}
				}
				return mergeCollisionBehavior.Value;
			}
		}
		bool? isOptimisticLockingIgnored;
		public bool IsOptimisticLockingIgnored {
			get {
				if(!isOptimisticLockingIgnored.HasValue) {
					OptimisticLockingIgnoredAttribute attr = (OptimisticLockingIgnoredAttribute)FindAttributeInfo(typeof(OptimisticLockingIgnoredAttribute));
					isOptimisticLockingIgnored = attr != null;
				}
				return isOptimisticLockingIgnored.Value;
			}
		}
		public bool IsExpandableToPersistent {
			get {
				return IsPersistent || IsAliased || IsAssociationList || IsManyToManyAlias;
			}
		}
	}
	public class XPCustomMemberInfo : XPMemberInfo {
		readonly string propertyName;
		readonly Type propertyType;
		readonly XPClassInfo referenceType;
		readonly bool isPublic;
		readonly bool isPersistent;
		public XPCustomMemberInfo(XPClassInfo owner, string propertyName, Type propertyType, XPClassInfo referenceType, bool nonPersistent, bool nonPublic)
			: base(owner, false) {
			if(propertyType == null && referenceType == null)
				throw new ArgumentNullException("propertyType, referenceType");
			if(propertyName == null)
				throw new ArgumentNullException(nameof(propertyName));
			this.propertyName = propertyName;
			this.propertyType = propertyType;
			this.referenceType = referenceType;
			this.isPublic = !nonPublic;
			this.isPersistent = !nonPersistent;
			Owner.AddMember(this);
		}
		[Description("Gets the XPClassInfo of the referenced object if the member is a reference to another persistent object.")]
		public override XPClassInfo ReferenceType {
			get {
				if(referenceType != null)
					return referenceType;
				else
					return base.ReferenceType;
			}
		}
		[Description("Gets the member’s name.")]
		public override string Name { get { return propertyName; } }
		[Description("Gets whether the member is public.")]
		public override bool IsPublic { get { return isPublic; } }
		[Description("Gets the member’s type.")]
		public override Type MemberType { get { return propertyType; } }
		protected override bool CanPersist { get { return isPersistent; } }
		protected virtual IXPCustomPropertyStore GetStore(object theObject) {
			return PersistentBase.GetCustomPropertyStore(theObject);
		}
		public override object GetValue(object theObject) {
			object result = GetStore(theObject).GetCustomPropertyValue(this);
			if(this.IsDelayed)
				return ((XPDelayedProperty)result).Value;
			else
				return result;
		}
		public override void SetValue(object theObject, object theValue) {
			if(this.IsDelayed)
				((XPDelayedProperty)GetStore(theObject).GetCustomPropertyValue(this)).SetValue(theValue);
			else
				GetStore(theObject).SetCustomPropertyValue(this, theValue);
		}
		public override bool GetModified(object theObject) {
			return PersistentBase.GetModificationsStore(theObject).GetPropertyModified(this);
		}
		public override void SetModified(object theObject, object oldValue) {
			PersistentBase.GetModificationsStore(theObject).SetPropertyModified(this, oldValue);
		}
		public override object GetOldValue(object theObject) {
			return PersistentBase.GetModificationsStore(theObject).GetPropertyOldValue(this);
		}
		public override void ResetModified(object theObject) {
			PersistentBase.GetModificationsStore(theObject).ResetPropertyModified(this);
		}
	}
	[NonPersistent, MemberDesignTimeVisibility(false), OptimisticLocking(false)]
	public class XPDataTableObject : XPBaseObject {
		public XPDataTableObject(Session session, XPClassInfo classInfo) : base(session, classInfo) { }
	}
	public class XPDataTableClassInfo : XPClassInfo {
		IList<XPMemberInfo> _ownMembers = new List<XPMemberInfo>();
		XPClassInfo baseClass;
		string className;
		Type type;
		string tableName;
		public XPDataTableClassInfo(XPDictionary dictionary, Type type)
			: base(dictionary) {
			this.type = type;
			this.baseClass = dictionary.QueryClassInfo(typeof(XPDataTableObject));
			this.className = type.FullName;
			DataTable table = null;
			if(type.DeclaringType != null && typeof(DataSet).IsAssignableFrom(type.DeclaringType)) {
				DataSet ds = (DataSet)Activator.CreateInstance(type.DeclaringType);
				foreach(DataTable t in ds.Tables)
					if(type.IsInstanceOfType(t)) {
						table = t;
						break;
					}
			}
			if(table == null)
				table = (DataTable)Activator.CreateInstance(type);
			this.tableName = table.TableName;
			if(table.PrimaryKey == null || table.PrimaryKey.Length != 1)
				throw new NotSupportedException(); 
			Dictionary.AddClassInfo(this);
			foreach(DataColumn column in table.Columns) {
				XPClassInfo related = null;
				string association = null;
				foreach(DataRelation r in table.ParentRelations) {
					if(r.ParentColumns.Length == 1 && r.ChildColumns[0] == column && r.ParentTable.PrimaryKey[0] == r.ParentColumns[0]) {
						related = Dictionary.QueryClassInfo(r.ParentTable.GetType());
						association = r.RelationName;
						break;
					}
				}
				XPCustomMemberInfo member = related != null ? CreateMember(column.ColumnName, related) : CreateMember(column.Caption, column.DataType);
				member.AddAttribute(new PersistentAttribute(column.ColumnName));
				member.AddAttribute(new DisplayNameAttribute(column.Caption));
				if(association != null)
					member.AddAttribute(new AssociationAttribute(association));
				if(table.PrimaryKey[0] == column)
					member.AddAttribute(new KeyAttribute(column.AutoIncrement));
			}
			foreach(DataRelation r in table.ChildRelations) {
				if(r.ParentColumns.Length == 1 && r.ParentTable.PrimaryKey[0] == r.ParentColumns[0]) {
					XPClassInfo ci = Dictionary.QueryClassInfo(r.ChildTable.GetType());
					if(ci != null)
						CreateMember(r.RelationName, typeof(XPCollection), true, new AssociationAttribute(r.RelationName, ci.ClassType));
				}
			}
		}
		protected override string GetTableName() {
			return tableName;
		}
		public override Type ClassType { get { return type; } }
		protected internal override Type RealInstanceType => typeof(XPDataTableObject);
		protected internal override object CreateObjectInstance(Session session, XPClassInfo instantiationClassInfo) {
			return BaseClass.CreateObjectInstance(session, instantiationClassInfo);
		}
		public override XPClassInfo BaseClass { get { return this.baseClass; } }
		public override string FullName { get { return this.className; } }
		public override string AssemblyName { get { return XPDataObjectClassInfo.DataObjectsAssembly; } }
		protected override bool CanPersist {
			get {
				return !HasAttribute(NonPersistentAttribute.AttributeType);
			}
		}
		public override void AddMember(XPMemberInfo newMember) {
			this._ownMembers.Add(newMember);
			base.AddMember(newMember);
		}
		public override ICollection<XPMemberInfo> OwnMembers {
			get { return this._ownMembers; }
		}
		public override bool HasModifications(object theObject) {
			return PersistentBase.GetModificationsStore(theObject).HasModifications();
		}
		public override void ClearModifications(object theObject) {
			PersistentBase.GetModificationsStore(theObject).ClearModifications();
		}
	}
	public class XPDataObjectClassInfo : XPClassInfo {
		MembersCollection _ownMembers = new MembersCollection();
		XPClassInfo baseClass;
		string className;
		public static readonly string DataObjectsAssembly = string.Empty;
		public static readonly Type DataObjectsBaseType = typeof(XPDataObject);
		public XPDataObjectClassInfo(XPDictionary dictionary, XPClassInfo baseClass, string className, params Attribute[] attributes)
			: base(dictionary) {
			if(baseClass == null)
				baseClass = dictionary.GetClassInfo(DataObjectsBaseType);
			this.baseClass = baseClass;
			this.className = className;
			if(attributes != null && attributes.Length > 0) {
				foreach(Attribute a in attributes) {
					this.AddAttribute(a);
				}
			}
			InitServiceMembers();
			CheckMembers();
			Dictionary.AddClassInfo(this);
		}
		public XPDataObjectClassInfo(XPDictionary dictionary, string className, params Attribute[] attributes)
			: this(dictionary, null, className, attributes) { }
		public XPDataObjectClassInfo(XPClassInfo baseClass, string className, params Attribute[] attributes)
			: this(baseClass.Dictionary, baseClass, className, attributes) { }
		static XPClassInfo ExtractBaseClassInfo(XPDictionary dictionary, XmlNode node) {
			if(node.Attributes["basetype"] == null)
				return null;
			return dictionary.GetClassInfo(DataObjectsAssembly, node.Attributes["basetype"].Value);
		}
		public XPDataObjectClassInfo(XPDictionary dictionary, XmlNode node)
			: this(dictionary, ExtractBaseClassInfo(dictionary, node), node.Attributes["type"].Value) { }
		protected internal override object CreateObjectInstance(Session session, XPClassInfo instantiationClassInfo) {
			return BaseClass.CreateObjectInstance(session, instantiationClassInfo);
		}
		[Description("Gets the metadata information of the base class.")]
		public override XPClassInfo BaseClass { get { return this.baseClass; } }
		[Description("Gets the full name of a class.")]
		public override string FullName { get { return this.className; } }
		[Description("Gets the type of the class.")]
		public override Type ClassType { get { return BaseClass.ClassType; } }
		public override bool CanGetByClassType {
			get {
				return false;
			}
		}
		protected internal override bool SupportObjectsReferencesFromCustomMembers {
			get {
				return BaseClass.SupportObjectsReferencesFromCustomMembers;
			}
		}
		[Description("Gets the name of the assembly that the class is declared in.")]
		public override string AssemblyName { get { return DataObjectsAssembly; } }
		protected override bool CanPersist {
			get {
				return !HasAttribute(NonPersistentAttribute.AttributeType);
			}
		}
		public override void AddMember(XPMemberInfo newMember) {
			this._ownMembers.Add(newMember);
			base.AddMember(newMember);
		}
		[Description("Gets a collection of XPMemberInfo objects that provide metadata information on all the members owned by the class.")]
		public override ICollection<XPMemberInfo> OwnMembers {
			get { return this._ownMembers; }
		}
		public override bool HasModifications(object theObject) {
			return PersistentBase.GetModificationsStore(theObject).HasModifications();
		}
		public override void ClearModifications(object theObject) {
			PersistentBase.GetModificationsStore(theObject).ClearModifications();
		}
	}
	public class XPDataObjectMemberInfo : XPCustomMemberInfo {
		protected XPDataObjectMemberInfo(XPClassInfo owner, string propertyName, Type propertyType, XPClassInfo referenceType)
			: base(owner, propertyName, propertyType, referenceType, false, false) { }
		static string ExtractPropertyName(XmlNode node) {
			XmlAttribute xmlAttr = node.Attributes["name"];
			if(xmlAttr == null)
				return null;
			return xmlAttr.Value;
		}
		static XPClassInfo ExtractReferenceType(XPClassInfo ownerInfo, XmlNode node) {
			XmlAttribute xmlAttr = node.Attributes["type"];
			if(xmlAttr == null)
				return null;
			XPClassInfo reference = ownerInfo.Dictionary.QueryClassInfo(XPDataObjectClassInfo.DataObjectsAssembly, xmlAttr.Value);
			return reference;
		}
		static Type ExtractPropertyType(XPClassInfo ownerInfo, XmlNode node) {
			XPClassInfo reference = ExtractReferenceType(ownerInfo, node);
			if(reference != null)
				return reference.ClassType;
			XmlAttribute xmlAttr = node.Attributes["type"];
			if(xmlAttr == null)
				return null;
			Type type = DevExpress.Data.Internal.SafeTypeResolver.GetKnownUserType(xmlAttr.Value);
			return type;
		}
		public XPDataObjectMemberInfo(XPClassInfo owner, XmlNode node)
			: this(owner, ExtractPropertyName(node), ExtractPropertyType(owner, node), ExtractReferenceType(owner, node)) { }
	}
	public abstract class XPDictionary : IXPDictionaryProvider {
		NullableBehavior nullableBehavior = NullableBehavior.Default;
		internal readonly AsyncLockHelper LockObject = new AsyncLockHelper();
		static XPDictionary() {
			IsExactTypeFunction.Register();
			IsInstanceOfTypeFunction.Register();
			IsExactTypeFunction.EvaluateExternal = IsExactTypeFunctionXpoHelper.Evaluate;
			IsInstanceOfTypeFunction.EvaluateExternal = IsInstanceOfTypeFunctionXpoHelper.Evaluate;
			XpoObjectInCriteriaProcessingHelper.Register();
		}
		class LoadXMLMetadataContext {
			public Hashtable ClassInfos = new Hashtable();
			public Hashtable MemberInfos = new Hashtable();
			[Obsolete("We do not recommend that you use this method due to potential vulnerabilities if the input XML document contains names of untrusted assemblies and types. Validate or sanitize the input XML documents even in testing and non-production environments for the best security.", false)]
			public static void LoadInfosDescriptions(Hashtable table, XmlNode parentNode, string infoTag, string attributeName, XmlNamespaceManager nsmgr, Type[] constructorArguments) {
				foreach(XmlNode infoNode in parentNode.SelectNodes(infoTag, nsmgr)) {
#pragma warning disable DX0005
					Type infoType = XPTypeActivator.GetType(infoNode.Attributes["assembly"].Value, infoNode.Attributes["type"].Value);
#pragma warning restore DX0005
					if(infoType == null)
						throw new XMLDictionaryException(Res.GetString(Res.MetaData_XMLLoadErrorCannotFindClassinfoType));
					ConstructorInfo constructor = infoType.GetConstructor(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, constructorArguments, null);
					if(constructor == null)
						throw new XMLDictionaryException(string.Format(Res.GetString(Res.MetaData_XMLLoadErrorCannotFindConstructor), infoType.FullName));
					table.Add(infoNode.Attributes[attributeName].Value, constructor);
				}
			}
		}
		public NullableBehavior NullableBehavior {
			get { return nullableBehavior; }
			set { nullableBehavior = value; }
		}
		protected internal virtual bool UseStrictMetadataValidation {
			get { return true; }
		}
		[Obsolete("We do not recommend that you use this method due to potential vulnerabilities if the input XML document contains names of untrusted assemblies and types. Validate or sanitize the input XML documents even in testing and non-production environments for the best security.", false)]
		void LoadXmlMetadata(XmlDocument doc) {
			XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
			nsmgr.AddNamespace("xpons", @"http://www.devexpress.com/products/xpo/schemas/1.9/xpometadata.xsd");
			XmlNode model = doc.DocumentElement.SelectSingleNode("/xpons:Model", nsmgr);
			if(model == null) throw new XMLDictionaryException(Res.GetString(Res.MetaData_XMLLoadErrorModelTagAbsent));
			LoadXMLMetadataContext context = new LoadXMLMetadataContext();
			LoadXMLMetadataContext.LoadInfosDescriptions(context.ClassInfos, model, "xpons:ClassInfoDescription", "classinfo", nsmgr, new Type[] { typeof(XPDictionary), typeof(XmlNode) });
			LoadXMLMetadataContext.LoadInfosDescriptions(context.MemberInfos, model, "xpons:MemberInfoDescription", "memberinfo", nsmgr, new Type[] { typeof(XPClassInfo), typeof(XmlNode) });
			IEnumerable list = model.SelectNodes("xpons:Class", nsmgr);
			while(true) {
				ArrayList newlist = new ArrayList();
				Exception ex = null;
				bool throwEx = true;
				foreach(XmlNode classNode in list) {
					string assembly = classNode.Attributes["assembly"] != null ? classNode.Attributes["assembly"].Value : string.Empty;
					XPClassInfo ci = QueryClassInfo(assembly, classNode.Attributes["type"].Value);
					if(ci == null) {
						if(classNode.Attributes["classinfo"] == null)
							throw new XMLDictionaryException(Res.GetString(Res.MetaData_XMLLoadErrorCannotResolveClassinfoInstanceType));
						try {
							ci = (XPClassInfo)((ConstructorInfo)context.ClassInfos[classNode.Attributes["classinfo"].Value]).Invoke(new object[] { this, classNode });
						}
						catch(TargetInvocationException e) {
							ex = e.InnerException == null ? e : e.InnerException;
							newlist.Add(classNode);
							continue;
						}
					}
					throwEx = false;
					LoadAttributes(classNode, ci, nsmgr);
				}
				if(newlist.Count == 0)
					break;
				if(throwEx)
					throw ex;
				list = newlist;
			}
			foreach(XmlNode classNode in model.SelectNodes("xpons:Class", nsmgr)) {
				string assembly = classNode.Attributes["assembly"] != null ? classNode.Attributes["assembly"].Value : "";
				string type = classNode.Attributes["type"].Value;
				XPClassInfo ci = GetClassInfo(assembly, type);
				foreach(XmlNode memberNode in classNode.SelectNodes("xpons:Member", nsmgr)) {
					string name = memberNode.Attributes["name"].Value;
					XPMemberInfo mi = ci.QueryOwnMember(name);
					if(mi == null) {
						if(memberNode.Attributes["memberinfo"] == null)
							throw new XMLDictionaryException(Res.GetString(Res.MetaData_XMLLoadErrorCannotLoadMember, assembly, type, name));
						try {
							mi = (XPMemberInfo)((ConstructorInfo)context.MemberInfos[memberNode.Attributes["memberinfo"].Value]).Invoke(new object[] { ci, memberNode });
						}
						catch(TargetInvocationException e) {
							throw e.InnerException == null ? e : e.InnerException;
						}
					}
					LoadAttributes(memberNode, mi, nsmgr);
				}
			}
		}
		void LoadAttributes(XmlNode parentNode, XPTypeInfo info, XmlNamespaceManager nsmgr) {
			XmlNode attributes = parentNode.SelectSingleNode("xpons:Attributes", nsmgr);
			if(attributes != null) {
				foreach(XmlNode attributeNode in attributes.ChildNodes) {
					if(attributeNode.NodeType != XmlNodeType.Element)
						continue;
					string name = attributeNode.Name;
					if(name.IndexOf("Attribute") < 0)
						name += "Attribute";
					if(name.IndexOf('.') < 0)
						name = typeof(CustomAttribute).Namespace + '.' + name;
					Type atributeType = SafeTypeResolver.GetKnownType(typeof(CustomAttribute).Assembly, name, true);
					Attribute attribute = null;
					if(atributeType != null) {
						ConstructorInfo constructor = atributeType.GetConstructor(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(XmlNode) }, null);
						if(constructor != null)
							attribute = (Attribute)constructor.Invoke(new object[] { attributeNode });
						else {
							constructor = atributeType.GetConstructor(Array.Empty<Type>());
							if(constructor != null)
								attribute = (Attribute)constructor.Invoke(Array.Empty<object>());
						}
					}
					if(attribute == null)
						throw new XMLDictionaryException(Res.GetString(Res.MetaData_XMLLoadErrorUnknownAttribute, attributeNode.Name));
					info.AddAttribute(attribute);
				}
			}
		}
		[Description("When implemented by a class, gets a collection of the XPClassInfo objects that are supplied by the current metadata provider.")]
		public abstract ICollection Classes { get; }
		public void AddClassInfo(XPClassInfo info) {
			if(!ReferenceEquals(this, info.Dictionary))
				throw new InvalidOperationException(Res.GetString(Res.Metadata_DictionaryMixing, info.FullName));
			AddClassInfoCore(info);
			TriggerClassInfoChanged(info);
		}
		protected abstract void AddClassInfoCore(XPClassInfo info);
		public abstract XPClassInfo QueryClassInfo(Type classType);
		public abstract XPClassInfo QueryClassInfo(string assemblyName, string className);
		[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
		public abstract bool CanGetClassInfoByType(Type classType);
		public XPClassInfo GetClassInfo(string assemblyName, string className) {
			XPClassInfo rv = QueryClassInfo(assemblyName, className);
			if(rv == null)
				throw new CannotResolveClassInfoException(assemblyName, className);
			return rv;
		}
		public XPClassInfo GetClassInfo(Type classType) {
			if(classType == null)
				throw new ArgumentNullException(nameof(classType));
			XPClassInfo rv = QueryClassInfo(classType);
			if(rv == null)
				throw new CannotResolveClassInfoException(classType.Assembly.FullName, classType.FullName);
			return rv;
		}
		public XPClassInfo QueryClassInfo(object theObject) {
			if(theObject == null)
				return null;
			IXPSimpleObject classObject = theObject as IXPSimpleObject;
			if(classObject != null)
				return classObject.ClassInfo;
			else
				return QueryClassInfo(theObject.GetType());
		}
		public XPClassInfo GetClassInfo(object theObject) {
			IXPSimpleObject classObject = theObject as IXPSimpleObject;
			if(classObject != null)
				return classObject.ClassInfo;
			else {
				if(theObject == null)
					throw new ArgumentNullException(nameof(theObject));
				return GetClassInfo(theObject.GetType());
			}
		}
		[Obsolete("We do not recommend that you use this method due to potential vulnerabilities if the input XML document contains names of untrusted assemblies and types. Validate or sanitize the input XML documents even in testing and non-production environments for the best security.", false)]
		public void LoadXmlMetadata(XmlReader reader) {
			const string ResourceName = "DevExpress.Xpo.XPOMetadata.xsd";
			XmlTextReader schema = SafeXml.CreateTextReader(new StreamReader(GetType().Assembly.GetManifestResourceStream(ResourceName)));
			try {
				XmlDocument doc = SafeXml.CreateDocument(reader, settings: settings => {
					settings.ValidationType = ValidationType.Schema;
					settings.Schemas.Add(null, schema);
				});
				LoadXmlMetadata(doc);
			}
			finally {
				reader.Close();
				schema.Close();
			}
		}
		[Obsolete("We do not recommend that you use this method due to potential vulnerabilities if the input XML document contains names of untrusted assemblies and types. Validate or sanitize the input XML documents even in testing and non-production environments for the best security.", false)]
		public void LoadXmlMetadata(TextReader txtReader) {
			XmlTextReader xmlTxtReader = SafeXml.CreateTextReader(txtReader);
			try {
				LoadXmlMetadata(xmlTxtReader);
			}
			finally {
				xmlTxtReader.Close();
			}
		}
		[Obsolete("We do not recommend that you use this method due to potential vulnerabilities if the input XML document contains names of untrusted assemblies and types. Validate or sanitize the input XML documents even in testing and non-production environments for the best security.", false)]
		public void LoadXmlMetadata(string filename) {
			XmlTextReader xmlTxtReader = SafeXml.CreateTextReader(filename);
			try {
				LoadXmlMetadata(xmlTxtReader);
			}
			finally {
				xmlTxtReader.Close();
			}
		}
		public event ClassInfoEventHandler ClassInfoChanged;
		void TriggerClassInfoChanged(XPClassInfo changedClassInfo) {
			ClassInfoChanged?.Invoke(this, new ClassInfoEventArgs(changedClassInfo));
		}
		internal void OnClassInfoCacheDropped(XPClassInfo changedClassInfo) {
			TriggerClassInfoChanged(changedClassInfo);
			DropDescendantsCache(changedClassInfo);
		}
		protected virtual void DropDescendantsCache(XPClassInfo changedClassInfo) {
			foreach(XPClassInfo ci in this.Classes) {
				if(ci.BaseClass == changedClassInfo)
					ci.DoDrop();
			}
		}
		public bool HasDescendants(XPClassInfo classInfo) {
			return HasDescendantsCore(classInfo);
		}
		protected virtual bool HasDescendantsCore(XPClassInfo classInfo) {
			foreach(XPClassInfo ci in this.Classes) {
				if(ci.BaseClass == classInfo)
					return true;
			}
			return false;
		}
		Dictionary<Type, ValueConverter> converters = new Dictionary<Type, ValueConverter>();
		public ValueConverter GetConverter(Type memberType) {
			if(memberType == null)
				return null;
			ValueConverter result;
			if(converters.TryGetValue(memberType, out result))
				return result;
			Type targetType = memberType;
			Type underlyingNullableType = Nullable.GetUnderlyingType(targetType);
			if(underlyingNullableType != null)
				targetType = underlyingNullableType;
			if(targetType.IsEnum) {
				result = new EnumsConverter(targetType);
				converters[memberType] = result;
				return result;
			}
			if(!ConnectionProviderSql.UseLegacyTimeSpanSupport && targetType == typeof(TimeSpan)) {
				result = new DefaultTimeSpanConverter();
				converters[memberType] = result;
				return result;
			}
			converters[memberType] = null;
			return null;
		}
		public void RegisterValueConverter(ValueConverter converter, Type memberType) {
			converters[memberType] = converter;
		}
		public object GetId(object obj) {
			XPClassInfo classInfo = this.GetClassInfo(obj);
			return classInfo.GetId(obj);
		}
		static readonly string xpoName = XPClassInfo.GetShortAssemblyName(typeof(XPDictionary).Assembly);
		protected virtual bool CanAssemblyContainPersistentClasses(Assembly assembly) {
			if(XPClassInfo.GetShortAssemblyName(assembly) == xpoName)
				return true;
			foreach(AssemblyName name in assembly.GetReferencedAssemblies()) {
				if(name.Name == xpoName) {
					return true;
				}
			}
			return false;
		}
		public XPClassInfo[] CollectClassInfos(bool addNonPersistent, params Assembly[] assemblies) {
			return CollectClassInfos(addNonPersistent, (IEnumerable<Assembly>)assemblies);
		}
		public XPClassInfo[] CollectClassInfos(bool addNonPersistent, IEnumerable<Assembly> assemblies) {
			if(assemblies == null)
				throw new ArgumentNullException(nameof(assemblies));
			List<XPClassInfo> typesList = new List<XPClassInfo>();
			foreach(Assembly assembly in assemblies) {
				if(!CanAssemblyContainPersistentClasses(assembly))
					continue;
				Type[] types;
				try {
					types = assembly.GetTypes();
				}
				catch(Exception ex) {
					ThrowCouldNotCollectClassInfoFromAssemblyWarning(assembly.FullName, ex);
					continue;
				}
				foreach(Type type in types) {
					XPClassInfo ci = QueryClassInfo(type);
					if(ci != null && (addNonPersistent || ci.IsPersistent) && !(ci is XPDataTableClassInfo)) {
						typesList.Add(ci);
					}
				}
			}
			return typesList.ToArray();
		}
		static bool couldNotCollectClassInfoFromAssemblyWarningThrown = false;
		static void ThrowCouldNotCollectClassInfoFromAssemblyWarning(string assemblyName, Exception innerException) {
			if(!couldNotCollectClassInfoFromAssemblyWarningThrown) {
				couldNotCollectClassInfoFromAssemblyWarningThrown = true;
				string msg = Res.GetString(CultureInfo.CurrentUICulture, Res.CollectClassInfos_CouldNotLoadAssembly, assemblyName);
				try {
					throw new WarningException(msg, innerException);
				}
				catch { }
			}
		}
		public XPClassInfo[] CollectClassInfos(params Assembly[] assemblies) {
			return CollectClassInfos(false, assemblies);
		}
		public XPClassInfo[] CollectClassInfos(IEnumerable<Assembly> assemblies) {
			return CollectClassInfos(false, assemblies);
		}
		public XPClassInfo[] CollectClassInfos(params Type[] types) {
			return CollectClassInfos((IEnumerable<Type>)types);
		}
		public XPClassInfo[] CollectClassInfos(IEnumerable<Type> types) {
			if(types == null)
				throw new ArgumentNullException(nameof(types));
			return types.Select(GetClassInfo).ToArray();
		}
		static void CheckDuplicateNames(ICollection<XPClassInfo> newTypes, ICollection<XPClassInfo> alreadyEnsuredTypes) {
			List<XPClassInfo> allTypes = new List<XPClassInfo>(alreadyEnsuredTypes);
			allTypes.AddRange(newTypes);
			foreach(XPClassInfo newType in newTypes) {
				if(newType.TableMapType != MapInheritanceType.OwnTable)
					continue;
				foreach(XPClassInfo type in allTypes) {
					if(Object.ReferenceEquals(type, newType))
						continue;
					if(type.TableMapType != MapInheritanceType.OwnTable)
						continue;
					if(type.TableName == newType.TableName)
						throw new SameTableNameException(type, newType);
				}
			}
		}
		internal ICollection<XPClassInfo> ExpandTypesToEnsure(ICollection<XPClassInfo> inputTypesToEnsure) {
			return ExpandTypesToEnsure(inputTypesToEnsure, new Dictionary<XPClassInfo, XPClassInfo>());
		}
		internal ICollection<XPClassInfo> ExpandTypesToEnsure(ICollection<XPClassInfo> inputTypesToEnsure, IDictionary<XPClassInfo, XPClassInfo> alreadyEnsuredTypes) {
			if(inputTypesToEnsure == null)
				throw new ArgumentNullException(nameof(inputTypesToEnsure));
			Dictionary<XPClassInfo, XPClassInfo> typesToEnsure = new Dictionary<XPClassInfo, XPClassInfo>();
			foreach(XPClassInfo type in inputTypesToEnsure) {
				if(!object.ReferenceEquals(type.Dictionary, this))
					throw new InvalidOperationException(Res.GetString(Res.Metadata_DictionaryMixing, type.FullName));
				if(!alreadyEnsuredTypes.ContainsKey(type)) {
					foreach(XPClassInfo refType in type.GetRefTypes()) {
						if(!alreadyEnsuredTypes.ContainsKey(refType)) {
							typesToEnsure[refType] = refType;
						}
					}
				}
			}
			ICollection<XPClassInfo> result = typesToEnsure.Keys;
			if(result.Count > 0)
				CheckDuplicateNames(result, alreadyEnsuredTypes.Keys);
			return result;
		}
		internal static DBTable[] CollectTables(ICollection<XPClassInfo> classInfos) {
			Dictionary<DBTable, string> tables = new Dictionary<DBTable, string>(classInfos.Count);
			XPDictionary dictionary = null;
			foreach(XPClassInfo type in classInfos) {
				if(dictionary == null) {
					dictionary = type.Dictionary;
				}
				else {
					if(!ReferenceEquals(dictionary, type.Dictionary))
						throw new InvalidOperationException(Res.GetString(Res.Metadata_DictionaryMixing, type.FullName));
				}
				tables[type.Table] = string.Empty;
			}
			return tables.Keys.ToArray();
		}
		public DBTable[] GetDataStoreSchema(params XPClassInfo[] types) {
			ICollection<XPClassInfo> allTypes = ExpandTypesToEnsure(types);
			DBTable[] result = CollectTables(allTypes);
			Array.Sort(result, (left, right) => string.CompareOrdinal(left.Name, right.Name));
			return result;
		}
		public DBTable[] GetDataStoreSchema(IEnumerable<XPClassInfo> types) {
			if(types == null)
				throw new ArgumentNullException(nameof(types));
			return GetDataStoreSchema(types.ToArray());
		}
		public DBTable[] GetDataStoreSchema(params Assembly[] assemblies) {
			return GetDataStoreSchema(CollectClassInfos(assemblies));
		}
		public DBTable[] GetDataStoreSchema(IEnumerable<Assembly> assemblies) {
			return GetDataStoreSchema(CollectClassInfos(assemblies));
		}
		public DBTable[] GetDataStoreSchema(params Type[] types) {
			return GetDataStoreSchema(CollectClassInfos(types));
		}
		public DBTable[] GetDataStoreSchema(IEnumerable<Type> types) {
			return GetDataStoreSchema(CollectClassInfos(types));
		}
		public XPClassInfo CreateClass(XPClassInfo baseClassInfo, string className, params Attribute[] attributes) {
			return new XPDataObjectClassInfo(this, baseClassInfo, className, attributes);
		}
		public XPClassInfo CreateClass(string className, params Attribute[] attributes) {
			return CreateClass(null, className, attributes);
		}
		static int seq = 0;
		int seqNum = Interlocked.Increment(ref seq);
		public override string ToString() {
			return base.ToString() + '(' + seqNum.ToString() + ')';
		}
		XPDictionary IXPDictionaryProvider.Dictionary {
			get { return this; }
		}
		class DummyPersistentValuesSource : IPersistentValueExtractor {
			public static DummyPersistentValuesSource Instance = new DummyPersistentValuesSource();
			public object ExtractPersistentValue(object criterionValue) {
				return criterionValue;
			}
			public bool CaseSensitive {
				get { return true; }
			}
		}
		[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
		public void Validate(XPClassInfo[] inputInfos) {
			foreach(XPClassInfo ci in new List<XPClassInfo>(inputInfos)) {
				foreach(XPMemberInfo mi in ci.Members) {
					if(mi.IsAssociation) {
						mi.GetAssociatedMember();
					}
					else if(mi.IsAliased) {
						PersistentAliasAttribute a = (PersistentAliasAttribute)mi.GetAttributeInfo(typeof(PersistentAliasAttribute));
						PersistentCriterionExpander.Expand(ci, DummyPersistentValuesSource.Instance, a.Criteria);
					}
				}
			}
		}
		CustomFunctionCollection customFunctionCollection = new CustomFunctionCollection();
		[Description("Gets a collection of custom function operators supplied by the current metadata provider.")]
		public CustomFunctionCollection CustomFunctionOperators { get { return customFunctionCollection; } }
		CustomAggregateCollection customAggregateCollection = new CustomAggregateCollection();
		[Description("Gets a collection of custom function operators supplied by the current metadata provider.")]
		public CustomAggregateCollection CustomAggregates { get { return customAggregateCollection; } }
	}
	public class ReflectionClassInfo : XPClassInfo {
		Type classType;
		bool constructed;
		MembersCollection ownMembers = new MembersCollection();
		Dictionary<string, XPMemberInfo> membersCache;
		protected override void DropCache() {
			if(!constructed)
				return;
			membersCache = null;
			base.DropCache();
		}
		bool IsNullableType(Type currentType) {
			return Nullable.GetUnderlyingType(currentType) != null;
		}
		void InternalCollectMembers(Type currentType, XPMemberInfo currentValueParent, bool inherited) {
			BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
			if(!inherited)
				flags |= BindingFlags.DeclaredOnly;
			foreach(FieldInfo fi in currentType.GetFields(flags)) {
				if(IsSameMemberInBase(fi))
					continue;
				XPMemberInfo mi = new ReflectionFieldInfo(this, fi, currentValueParent);
				if(mi.IsStruct && !IsNullableType(mi.MemberType) && mi.HasAttribute(PersistentAttribute.AttributeType))
					InternalCollectMembers(mi.MemberType, mi, false);
			}
			foreach(PropertyInfo pi in currentType.GetProperties(flags)) {
				try {
					MethodInfo getMethodInfo = pi.GetGetMethod(true);
					if(getMethodInfo == null)
						continue;
					if(getMethodInfo.GetParameters().Length != 0)
						continue;
					if(IsSameMemberInBase(pi))
						continue;
					XPMemberInfo mi = new ReflectionPropertyInfo(this, pi, currentValueParent);
					if(mi.IsStruct && !IsNullableType(mi.MemberType) && mi.HasAttribute(PersistentAttribute.AttributeType))
						InternalCollectMembers(mi.MemberType, mi, false);
				}
				catch(System.Security.SecurityException) {
				}
			}
		}
		bool IsSameMemberInBase(MemberInfo mi) {
			XPMemberInfo sameMemberFound = null;
			if(BaseClass != null) {
				XPMemberInfo f = BaseClass.FindMember(mi.Name);
				if(IsBaseMemberSuppressCurrent(mi, f)) {
					sameMemberFound = f;
				}
			}
			if(sameMemberFound != null) {
				if(!_SuppressSuspiciousMemberInheritanceCheck) {
#if !NET
					if(!(this.Dictionary is DesignTimeReflection))
#endif
					{
						foreach(Attribute a in mi.GetCustomAttributes(false)) {
							if(a.GetType().Namespace == typeof(PersistentAttribute).Namespace)
								throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, Res.GetString(Res.Metadata_SuppressSuspiciousMemberInheritanceCheckError, FullName, mi.Name, a.GetType().Name, sameMemberFound.Owner.FullName)));
						}
					}
				}
				return true;
			}
			return false;
		}
		static bool IsBaseMemberSuppressCurrent(MemberInfo currentMember, XPMemberInfo baseMember) {
			if(baseMember == null)
				return false;
			if(baseMember.IsPersistent)
				return true;
			if(baseMember.IsAliased)
				return true;
			if(baseMember.IsAssociationList)
				return true;
			if(baseMember.IsPublic)
				return true;
			PropertyInfo pi = currentMember as PropertyInfo;
			if(pi != null) {
				MethodInfo getMethod = pi.GetGetMethod(true);
				if(getMethod != null && getMethod.GetBaseDefinition() != getMethod)
					return true;
				MethodInfo setMethod = pi.GetSetMethod(true);
				if(setMethod != null && setMethod.GetBaseDefinition() != setMethod)
					return true;
			}
			return false;
		}
		static bool _SuppressSuspiciousMemberInheritanceCheck;
		[Description("Specifies whether exceptions are thrown when overridden properties have attributes from the Xpo namespace applied.")]
		[Obsolete("SuppressSuspiciousMemberInheritanceCheck accessed")]
		public static bool SuppressSuspiciousMemberInheritanceCheck {
			get { return _SuppressSuspiciousMemberInheritanceCheck; }
			set { _SuppressSuspiciousMemberInheritanceCheck = value; }
		}
		void CollectMembers(Type currentType) {
			InternalCollectMembers(classType, null, BaseClass == null);
		}
		public override void AddMember(XPMemberInfo newMember) {
			ownMembers.Add(newMember);
			base.AddMember(newMember);
		}
		protected internal override XPMemberInfo QueryOwnMember(string memberName) {
			if(membersCache == null) {
				Dictionary<string, XPMemberInfo> list = new Dictionary<string, XPMemberInfo>();
				foreach(XPMemberInfo mi in OwnMembers) {
					list.Add(mi.Name, mi);
				}
				membersCache = list;
			}
			XPMemberInfo member;
			membersCache.TryGetValue(memberName, out member);
			return member;
		}
		public ReflectionClassInfo(Type classType, XPDictionary dictionary)
			: base(dictionary) {
			this.classType = classType;
			this.baseClass = Dictionary.QueryClassInfo(classType.GetBaseType());
			bool hasPrsistentAttribute = false;
			foreach(Attribute attribute in GetCustomAttributes(classType)) {
#pragma warning disable CS0618
				if(attribute is PersistentAttribute || attribute is NonPersistentAttribute || attribute is MapToAttribute) {
#pragma warning restore CS0618
					if(hasPrsistentAttribute)
						throw new InvalidOperationException(Res.GetString(Res.Metadata_ClassAttributeExclusive, this.FullName));
					hasPrsistentAttribute = true;
				}
				AddAttribute(attribute);
			}
			CollectMembers(classType);
			InitServiceMembers();
			CheckMembers();
			constructed = true;
			Dictionary.AddClassInfo(this);
		}
		private object[] GetCustomAttributes(Type classType) {
			if(IsDesignTimeReflection) {
				try {
					return classType.GetCustomAttributes(false);
				}
				catch(Exception ex) {
					if((ex is BadImageFormatException) || (ex is FileNotFoundException)) {
						return Array.Empty<Attribute>();
					}
					else {
						throw;
					}
				}
			}
			else {
				return classType.GetCustomAttributes(false);
			}
		}
		IConstructor creator;
		IConstructor Creator {
			get {
				if(creator == null)
					creator = CreateConstructor();
				return creator;
			}
		}
		public interface ICreator {
			object Create(Session session, XPClassInfo ci);
		}
		interface IConstructor {
			object Create(Session session, XPClassInfo ci);
		}
		delegate object CreatorDelegate(Session session, XPClassInfo ci);
		class ConstructorWithoutSession : IConstructor, ICreator {
			CreatorDelegate creator;
			protected CreatorDelegate Creator { get { return creator; } }
			static Dictionary<ConstructorInfo, CreatorDelegate> creators = new Dictionary<ConstructorInfo, CreatorDelegate>();
			ConstructorInfo constr;
			protected ConstructorInfo Constr { get { return constr; } }
			public ConstructorWithoutSession(ConstructorInfo constr) {
				this.constr = constr;
				if(XpoDefault.UseFastAccessors) {
					if(!creators.TryGetValue(Constr, out creator)) {
						lock(creators) {
							if(!creators.TryGetValue(Constr, out creator)) {
								ParameterExpression sParam = Expression.Parameter(typeof(Session));
								ParameterExpression ciParam = Expression.Parameter(typeof(XPClassInfo));
								creator = Expression.Lambda<CreatorDelegate>(Expression.New(Constr, CreateArguments(sParam, ciParam)), sParam, ciParam).Compile();
								creators[Constr] = creator;
							}
						}
					}
				}
				else {
					creator = new CreatorDelegate(InvokeConstructor);
				}
			}
			protected virtual void CheckParameters(Session session, XPClassInfo ci) {
				if(session != XpoDefault.Session && typeof(IXPSimpleObject).IsAssignableFrom(ci.ClassType))
					throw new SessionCtorAbsentException(ci);
			}
			protected virtual object InvokeConstructor(Session session, XPClassInfo ci) {
				return Constr.Invoke(null);
			}
			object ICreator.Create(Session session, XPClassInfo ci) {
				return InvokeConstructor(session, ci);
			}
			object IConstructor.Create(Session session, XPClassInfo ci) {
				CheckParameters(session, ci);
				return Creator(session, ci);
			}
			protected virtual Expression[] CreateArguments(ParameterExpression sParam, ParameterExpression ciParam) {
				return null;
			}
		}
		class Constructor : ConstructorWithoutSession {
			Type sessionType;
			protected Type SessionType {
				get {
					if(sessionType == null) {
						sessionType = Constr.GetParameters()[0].ParameterType;
					}
					return sessionType;
				}
			}
			protected override Expression[] CreateArguments(ParameterExpression sParam, ParameterExpression ciParam) {
				return new Expression[] { Expression.Convert(sParam, SessionType) };
			}
			protected override void CheckParameters(Session session, XPClassInfo ci) {
				if(!SessionType.IsInstanceOfType(session))
					throw new SessionCtorAbsentException(ci);
			}
			protected override object InvokeConstructor(Session session, XPClassInfo ci) {
				return Constr.Invoke(new object[] { session });
			}
			public Constructor(ConstructorInfo constr)
				: base(constr) {
			}
		}
		sealed class ConstructorWithClassInfo : Constructor {
			public ConstructorWithClassInfo(ConstructorInfo constr) : base(constr) { }
			protected override Expression[] CreateArguments(ParameterExpression sParam, ParameterExpression ciParam) {
				return new Expression[] { Expression.Convert(sParam, SessionType), ciParam };
			}
			protected override object InvokeConstructor(Session session, XPClassInfo ci) {
				return Constr.Invoke(new object[] { session, ci });
			}
		}
		class NullConstructor : IConstructor {
			object IConstructor.Create(Session session, XPClassInfo classInfo) {
				throw new SessionCtorAbsentException(classInfo);
			}
		}
		IConstructor CreateConstructor() {
			if(IsAbstract)
				return new NullConstructor();
			ConstructorInfo[] constrArr = classType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			foreach(ConstructorInfo c in constrArr) {
				ParameterInfo[] cParams = c.GetParameters();
				if(cParams.Length == 2 &&
					typeof(Session).IsAssignableFrom(cParams[0].ParameterType) &&
					typeof(XPClassInfo).IsAssignableFrom(cParams[1].ParameterType)) {
					return new ConstructorWithClassInfo(c);
				}
			}
			foreach(ConstructorInfo c in constrArr) {
				ParameterInfo[] cParams = c.GetParameters();
				if(cParams.Length == 1 && typeof(Session).IsAssignableFrom(cParams[0].ParameterType)) {
					return new Constructor(c);
				}
			}
			foreach(ConstructorInfo c in constrArr) {
				ParameterInfo[] cParams = c.GetParameters();
				if(cParams.Length == 0) {
					return new ConstructorWithoutSession(c);
				}
			}
			return new NullConstructor();
		}
		XPClassInfo baseClass;
		[Description("Gets the metadata information of the base class.")]
		public override XPClassInfo BaseClass { get { return baseClass; } }
		[Description("Gets a collection of XPMemberInfo objects that provide metadata information on all the members owned by the class.")]
		public override ICollection<XPMemberInfo> OwnMembers { get { return ownMembers; } }
		[Description("Gets the full name of a class.")]
		public override string FullName {
			get {
				return ReflectionDictionary.GetFullName(classType);
			}
		}
		[Description("Gets the name of the assembly that the class is declared in.")]
		public override string AssemblyName {
			get {
				string name = ClassType.Assembly.FullName;
				int pos = name.IndexOf(',');
				if(pos > 0)
					name = name.Substring(0, pos).TrimEnd();
				return name;
			}
		}
		protected override string GetDefaultTableName() {
			return ClassType.Name;
		}
		protected override bool CanPersist {
			get {
				if(ClassType.IsGenericType || ClassType.ContainsGenericParameters) {
					if(HasAttribute(PersistentAttribute.AttributeType))
						throw new InvalidOperationException(Res.GetString(Res.Metadata_CantPersistGenericType, ClassType.FullName));
					return false;
				}
				if(ClassType.IsInterface)
					return false;
				return !HasAttribute(NonPersistentAttribute.AttributeType);
			}
		}
		[Description("Gets the type of the class whose metadata is provided by this ReflectionClassInfo object.")]
		public override Type ClassType { get { return classType; } }
		protected internal override object CreateObjectInstance(Session session, XPClassInfo instantiationClassInfo) {
			object obj = Creator.Create(session, instantiationClassInfo);
			session.ThrowIfObjectFromDifferentSession(obj);
			IXPClassInfoProvider ciObject = obj as IXPClassInfoProvider;
			if(ciObject != null) {
				if(ciObject.ClassInfo != instantiationClassInfo)
					throw new InvalidOperationException(Res.GetString(Res.Metadata_WrongObjectType, ciObject, ciObject.ClassInfo, instantiationClassInfo));
			}
			return obj;
		}
		[Description("Gets the rule that determines which members are persistent by default.")]
		public DefaultMembersPersistence DefaultMembersPersistence {
			get {
				DefaultMembersPersistenceAttribute attribute = (DefaultMembersPersistenceAttribute)FindAttributeInfo(typeof(DefaultMembersPersistenceAttribute));
				if(attribute == null) {
					ReflectionClassInfo baseReflectionClassInfo = BaseClass as ReflectionClassInfo;
					if(baseReflectionClassInfo == null) {
						return DefaultMembersPersistence.Default;
					}
					else {
						return baseReflectionClassInfo.DefaultMembersPersistence;
					}
				}
				else {
					return attribute.DefaultMembersPersistence;
				}
			}
		}
		protected override bool IsInterface {
			get {
				return this.ClassType.IsInterface;
			}
		}
		public override bool HasModifications(object theObject) {
			return PersistentBase.GetModificationsStore(theObject).HasModifications();
		}
		public override void ClearModifications(object theObject) {
			PersistentBase.GetModificationsStore(theObject).ClearModifications();
		}
	}
	public interface IFieldAccessor {
		void SetValue(object theObject, object value);
		object GetValue(object theObject);
	}
	public abstract class ReflectionMemberInfo : XPMemberInfo, IFieldAccessor {
		delegate object GetValueDelegate(object obj);
		delegate void SetValueDelegate(object obj, object value);
		class DelegateFieldAccessor : IFieldAccessor {
			GetValueDelegate get;
			SetValueDelegate set;
			public DelegateFieldAccessor(GetValueDelegate get, SetValueDelegate set) {
				this.get = get;
				this.set = set;
			}
			public void SetValue(object theObject, object value) {
				set(theObject, value);
			}
			public object GetValue(object theObject) {
				return get(theObject);
			}
		}
		static readonly Dictionary<MemberInfo, IFieldAccessor> accessors = new Dictionary<MemberInfo, IFieldAccessor>();
		protected virtual IFieldAccessor CreateAccessorInternal() {
			ParameterExpression param = Expression.Parameter(typeof(object));
			Expression obj;
			if(ValueParent != null)
				obj = Expression.Unbox(param, ValueParent.MemberType);
			else
				obj = Expression.Convert(param, Info.DeclaringType);
			GetValueDelegate get = Expression.Lambda<GetValueDelegate>(Expression.Convert(Expression.MakeMemberAccess(obj, Info), typeof(object)), param).Compile();
			ParameterExpression setvalue = Expression.Parameter(typeof(object));
			SetValueDelegate set;
			if(IsReadOnly)
				set = null;
			else {
				Expression val;
				if(MemberType.IsValueType && Nullable.GetUnderlyingType(MemberType) == null) {
					val = Expression.Condition(Expression.Equal(setvalue, Expression.Constant(null)), Expression.New(MemberType), Expression.Unbox(setvalue, MemberType));
				}
				else {
					val = Expression.Convert(setvalue, MemberType);
				}
				set = Expression.Lambda<SetValueDelegate>(Expression.Assign(Expression.MakeMemberAccess(obj, Info), val), param, setvalue).Compile();
			}
			return new DelegateFieldAccessor(get, set);
		}
		protected virtual bool CanUseFastAccessors {
			get { return XpoDefault.UseFastAccessors; }
		}
		IFieldAccessor fieldAccessor;
		protected abstract MemberInfo Info { get; }
		bool Constructed {
			get { return name != null; }
		}
		void CreateAccessor() {
			IFieldAccessor accessor;
			if(CanUseFastAccessors) {
				if(!accessors.TryGetValue(Info, out accessor)) {
					lock(accessors) {
						if(!accessors.TryGetValue(Info, out accessor)) {
							accessor = CreateAccessorInternal();
							accessors[Info] = accessor;
						}
					}
				}
			}
			else {
				accessor = this;
			}
			if(ValueParent != null)
				accessor = new ValueParentAccessor(ValueParent, accessor);
			fieldAccessor = accessor;
		}
		IFieldAccessor Accessor {
			get {
				if(fieldAccessor == null)
					CreateAccessor();
				return fieldAccessor;
			}
		}
		protected override void DropCache() {
			if(!Constructed)
				return;
			isStruct = null;
			nullValue = emtpyNullValue;
			base.DropCache();
		}
		string name;
		static readonly object emtpyNullValue = new Object();
		object nullValue = emtpyNullValue;
		object NullValue {
			get {
				object value = nullValue;
				if(emtpyNullValue == value) {
					value = GetNullValue();
					nullValue = value;
				}
				return value;
			}
		}
		protected ReflectionMemberInfo(XPClassInfo owner, MemberInfo info, XPMemberInfo valueParent, bool isReadOnly)
			: base(owner, isReadOnly) {
			this.valueParent = valueParent;
			ReflectionMemberInfo parent = valueParent as ReflectionMemberInfo;
			while(parent != null) {
				if(parent.subMembersArray == XPMemberInfo.EmptyList)
					parent.subMembersArray = new List<XPMemberInfo>(4);
				parent.subMembersArray.Add(this);
				parent = parent.valueParent as ReflectionMemberInfo;
			}
			name = valueParent != null ? this.valueParent.Name + '.' + info.Name : info.Name;
			bool hasPrsistentAttribute = false;
			foreach(Attribute attribute in GetCustomAttributes(owner, info)) {
				if(attribute is PersistentAttribute || attribute is NonPersistentAttribute || attribute is PersistentAliasAttribute) {
					if(hasPrsistentAttribute)
						throw new InvalidOperationException(Res.GetString(Res.Metadata_MemberAttributeExclusive, this.Owner.FullName, this.Name));
					hasPrsistentAttribute = true;
				}
				AddAttribute(attribute);
			}
			Owner.AddMember(this);
		}
		private object[] GetCustomAttributes(XPClassInfo owner, MemberInfo info) {
			if(Owner.IsDesignTimeReflection) {
				try {
					return info.GetCustomAttributes(true);
				}
				catch(Exception ex) {
					if((ex is BadImageFormatException) || (ex is FileNotFoundException)) {
						return Array.Empty<Attribute>();
					}
					else {
						throw;
					}
				}
			}
			else {
				return info.GetCustomAttributes(true);
			}
		}
		object GetNullValue() {
			NullValueAttribute attribute = (NullValueAttribute)FindAttributeInfo(typeof(NullValueAttribute));
			return attribute != null ? attribute.Value :
				(MemberType == typeof(DateTime) ? (object)new DateTime(0) : null);
		}
		[Description("Gets the member’s name.")]
		public override string Name { get { return name; } }
		public override object GetConst(object target, XPMemberInfo targetMember) {
			if(this == targetMember)
				return target;
			object theValue = valueParent != null ? ((ValueParentAccessor)Accessor).Accessor.GetValue(valueParent.GetConst(target, targetMember)) : target;
			if(theValue != null && theValue.Equals(NullValue))
				theValue = null;
			return theValue;
		}
		public override object GetValue(object theObject) {
			object theValue = Accessor.GetValue(theObject);
			return theValue != null && theValue.Equals(NullValue) ? null : theValue;
		}
		sealed class ValueParentAccessor : IFieldAccessor {
			XPMemberInfo valueParent;
			IFieldAccessor accessor;
			object IFieldAccessor.GetValue(object theObject) {
				return accessor.GetValue(valueParent.GetValue(theObject));
			}
			void IFieldAccessor.SetValue(object theObject, object theValue) {
				object valueParentValue = valueParent.GetValue(theObject);
				accessor.SetValue(valueParentValue, theValue);
				valueParent.SetValue(theObject, valueParentValue);
			}
			public ValueParentAccessor(XPMemberInfo valueParent, IFieldAccessor accessor) {
				this.valueParent = valueParent;
				this.accessor = accessor;
			}
			public IFieldAccessor Accessor { get { return accessor; } }
		}
		public override void SetValue(object theObject, object theValue) {
			if(!IsReadOnly)
				Accessor.SetValue(theObject, theValue == null ? NullValue : theValue);
		}
		public override bool GetModified(object theObject) {
			return PersistentBase.GetModificationsStore(theObject).GetPropertyModified(this);
		}
		public override void SetModified(object theObject, object oldValue) {
			PersistentBase.GetModificationsStore(theObject).SetPropertyModified(this, oldValue == null ? NullValue : oldValue);
		}
		public override object GetOldValue(object theObject) {
			object theValue = PersistentBase.GetModificationsStore(theObject).GetPropertyOldValue(this);
			return theValue != null && theValue.Equals(NullValue) ? null : theValue;
		}
		public override void ResetModified(object theObject) {
			PersistentBase.GetModificationsStore(theObject).ResetPropertyModified(this);
		}
		protected override string GetDefaultMappingField() {
			return valueParent != null ? valueParent.MappingField + Info.Name : Info.Name; ;
		}
		bool? isStruct;
		[Description("Gets whether the member represents a data structure.")]
		public override bool IsStruct {
			get {
				if(!isStruct.HasValue)
					isStruct = MemberType.IsValueType && !DBColumn.IsStorableType(StorageType);
				return isStruct.Value;
			}
		}
		public override Expression MakeGetExpression(Expression ownerExpression) {
			if(!this.IsPublic)
				return base.MakeGetExpression(ownerExpression);
			if(this.ValueParent == null) {
				if(this.Owner.ClassType.IsInterface && this.Owner.ClassType != ownerExpression.Type) {
					ownerExpression = Expression.Convert(ownerExpression, this.Owner.ClassType);
				}
				return GetExpressionFinalTouch(Expression.MakeMemberAccess(ownerExpression, this.Info));
			}
			else {
				Expression rv = ownerExpression;
				foreach(string navigation in this.Name.Split('.')) {
					rv = Expression.PropertyOrField(rv, navigation);
				}
				return GetExpressionFinalTouch(rv);
			}
		}
		Expression GetExpressionFinalTouch(Expression expression) {
			if(this.ReferenceType != null) {
				if(this.ReferenceType.ClassType.IsAssignableFrom(expression.Type))
					return expression;
				else
					return Expression.Convert(expression, this.ReferenceType.ClassType);
			}
			else {
				object nullValueSubst = this.NullValue;
				if(nullValueSubst == null)
					return expression;
				return GetExpressionFixNullHelperMethods.MakePatchNullValues(expression, nullValueSubst);
			}
		}
		[EditorBrowsable(EditorBrowsableState.Never), Browsable(false)]
		public static class GetExpressionFixNullHelperMethods {
			public static Expression MakePatchNullValues(Expression expression, object nullValueSubst) {
				string methodName;
				Type nullType = nullValueSubst.GetType();
				Type boxedExprType = NullableHelpers.GetBoxedType(expression.Type);
				Type requiredEqType = typeof(IEquatable<>).MakeGenericType(boxedExprType);
				if(requiredEqType.IsAssignableFrom(nullType)) {
					if(expression.Type.IsValueType) {
						if(Nullable.GetUnderlyingType(expression.Type) != null)
							methodName = "GetExpressionFixNullEquatableNullable";
						else
							methodName = "GetExpressionFixNullEquatableStruct";
					}
					else
						methodName = "GetExpressionFixNullEquatableClass";
				}
				else {
					if(expression.Type.IsValueType) {
						if(Nullable.GetUnderlyingType(expression.Type) != null)
							methodName = "GetExpressionFixNullNullable";
						else
							methodName = "GetExpressionFixNullStruct";
					}
					else
						methodName = "GetExpressionFixNullClass";
				}
				ParameterExpression valueParam = Expression.Parameter(expression.Type, "v");
				Expression body = Expression.Call(typeof(GetExpressionFixNullHelperMethods), methodName, new[] { expression.Type, nullType }, valueParam, Expression.Constant(nullValueSubst));
				var lmbd = Expression.Lambda(body, valueParam);
				return Expression.Invoke(lmbd, expression);
			}
			public static T? GetExpressionFixNullEquatableStruct<T, N>(T value, N nullValue)
				where T : struct
				where N : IEquatable<T> {
				if(nullValue.Equals(value))
					return null;
				else
					return value;
			}
			public static T GetExpressionFixNullEquatableClass<T, N>(T value, N nullValue)
				where T : class
				where N : IEquatable<T> {
				if(nullValue.Equals(value))
					return null;
				else
					return value;
			}
			public static T? GetExpressionFixNullEquatableNullable<T, N>(T? value, N nullValue)
				where T : struct
				where N : IEquatable<T> {
				if(value.HasValue && nullValue.Equals(value.Value))
					return null;
				else
					return value;
			}
			public static T? GetExpressionFixNullStruct<T, N>(T value, N nullValue)
				where T : struct {
				if(nullValue.Equals(value))
					return null;
				else
					return value;
			}
			public static T? GetExpressionFixNullNullable<T, N>(T? value, N nullValue)
				where T : struct {
				if(value.HasValue && nullValue.Equals(value))
					return null;
				else
					return value;
			}
			public static T GetExpressionFixNullClass<T, N>(T value, N nullValue)
				where T : class {
				if(nullValue.Equals(value))
					return null;
				else
					return value;
			}
		}
	}
	public sealed class ReflectionPropertyInfo : ReflectionMemberInfo, IFieldAccessor {
		PropertyInfo info;
		protected override MemberInfo Info { get { return info; } }
		class DelegateFieldAccessor<T, P> : IFieldAccessor {
			public delegate T GetValueDelegate(P obj);
			public delegate void SetValueDelegate(P obj, T value);
			GetValueDelegate get;
			SetValueDelegate set;
			public DelegateFieldAccessor(Delegate get, Delegate set) {
				this.get = (GetValueDelegate)get;
				this.set = (SetValueDelegate)set;
			}
			public void SetValue(object theObject, object value) {
				set((P)theObject, value == null ? default(T) : (T)value);
			}
			public object GetValue(object theObject) {
				return get((P)theObject);
			}
		}
		static bool isVista = (Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version.Major >= 6);
		protected override bool CanUseFastAccessors {
			get { return base.CanUseFastAccessors || (isVista && !Info.DeclaringType.IsValueType); }
		}
		protected override IFieldAccessor CreateAccessorInternal() {
			if(base.CanUseFastAccessors)
				return base.CreateAccessorInternal();
			Type[] genParams = new Type[] { info.PropertyType, Info.DeclaringType };
			MethodInfo getMi = info.GetGetMethod(true);
			MethodInfo setMi = info.GetSetMethod(true);
#if NET
			Delegate get = getMi != null ? getMi.CreateDelegate(typeof(DelegateFieldAccessor<,>.GetValueDelegate).MakeGenericType(genParams)) : null;
			Delegate set = setMi != null ? setMi.CreateDelegate(typeof(DelegateFieldAccessor<,>.SetValueDelegate).MakeGenericType(genParams)) : null;
#else
			Delegate get = getMi != null ? Delegate.CreateDelegate(typeof(DelegateFieldAccessor<,>.GetValueDelegate).MakeGenericType(genParams), null, getMi) : null;
			Delegate set = setMi != null ? Delegate.CreateDelegate(typeof(DelegateFieldAccessor<,>.SetValueDelegate).MakeGenericType(genParams), null, setMi) : null;
#endif
			return (IFieldAccessor)Activator.CreateInstance(typeof(DelegateFieldAccessor<,>).MakeGenericType(genParams), get, set);
		}
		public ReflectionPropertyInfo(XPClassInfo owner, PropertyInfo propertyInfo, XPMemberInfo valueParent)
			: base(owner, propertyInfo, valueParent, !propertyInfo.CanWrite) {
			info = propertyInfo;
		}
		[Description("Gets whether the property is declared as public.")]
		public override bool IsPublic { get { return info.GetAccessors().Length != 0; } }
		protected override bool CanPersist {
			get {
				if(HasAttribute(PersistentAttribute.AttributeType)) {
					if(IsStruct) {
						return ValueParent == null;
					}
					return true;
				}
				if(Owner is ReflectionClassInfo && ((ReflectionClassInfo)Owner).DefaultMembersPersistence == DefaultMembersPersistence.OnlyDeclaredAsPersistent)
					return false;
				if(HasAttribute(NonPersistentAttribute.AttributeType))
					return false;
				if(IsAliased)
					return false;
				if(info.DeclaringType.GetBaseType() != null && info.DeclaringType.GetBaseType().GetProperty(Name) != null)
					return false;
				if(info.GetGetMethod() == null)
					return false;
				if(info.GetSetMethod() == null)
					return false;
				if(DBColumn.IsStorableType(StorageType))
					return true;
				if(Owner.Dictionary.CanGetClassInfoByType(StorageType))
					return true;
				return false;
			}
		}
		[Description("Gets the type of this property.")]
		public override Type MemberType { get { return info.PropertyType; } }
		object IFieldAccessor.GetValue(object theObject) {
			return info.GetValue(theObject, null);
		}
		void IFieldAccessor.SetValue(object theObject, object theValue) {
			info.SetValue(theObject, theValue, null);
		}
	}
	public sealed class ReflectionFieldInfo : ReflectionMemberInfo, IFieldAccessor {
		protected override MemberInfo Info { get { return info; } }
		MemberInfo[] infoArray;
		FieldInfo info;
		public ReflectionFieldInfo(XPClassInfo owner, FieldInfo fieldInfo, XPMemberInfo valueParent)
			: base(owner, fieldInfo, valueParent, fieldInfo.IsInitOnly) {
			info = fieldInfo;
			infoArray = new MemberInfo[] { info };
		}
		protected override bool CanPersist {
			get {
				if(HasAttribute(PersistentAttribute.AttributeType)) {
					if(IsStruct) {
						return ValueParent == null;
					}
					return true;
				}
				if(Owner is ReflectionClassInfo && ((ReflectionClassInfo)Owner).DefaultMembersPersistence == DefaultMembersPersistence.OnlyDeclaredAsPersistent)
					return false;
				if(HasAttribute(NonPersistentAttribute.AttributeType))
					return false;
				if(IsAliased)
					return false;
				if(!info.IsPublic)
					return false;
				if(info.IsInitOnly)
					return false;
				if(DBColumn.IsStorableType(StorageType))
					return true;
				if(Owner.Dictionary.CanGetClassInfoByType(StorageType))
					return true;
				return false;
			}
		}
		[Description("Indicates whether the field is public.")]
		public override bool IsPublic { get { return info.IsPublic; } }
		[Description("Gets the type of this field.")]
		public override Type MemberType { get { return info.FieldType; } }
		object IFieldAccessor.GetValue(object theObject) {
#if !NET
			return XpoDefault.UseFastAccessors ? GetValueFast(theObject) : info.GetValue(theObject);
#else
			return info.GetValue(theObject);
#endif
		}
		void IFieldAccessor.SetValue(object theObject, object theValue) {
#if !NET
			if(theValue == null || !XpoDefault.UseFastAccessors)
#endif
			info.SetValue(theObject, theValue);
#if !NET
			else SetValueFast(theObject, theValue);
#endif
		}
#if !NET // reflection-based implementation / not related to deserializing
		object GetValueFast(object theObject) {
			return System.Runtime.Serialization.FormatterServices.GetObjectData(theObject, infoArray)[0];
		}
		void SetValueFast(object theObject, object theValue) {
			System.Runtime.Serialization.FormatterServices.PopulateObjectMembers(theObject, infoArray, new object[] { theValue });
		}
#endif
	}
	public class CanGetClassInfoByTypeEventArgs : EventArgs {
		public readonly ReflectionDictionary Dictionary;
		public readonly Type ClassType;
		public bool? CanGetClassInfo;
		public CanGetClassInfoByTypeEventArgs(ReflectionDictionary dictionary, Type classType) {
			this.Dictionary = dictionary;
			this.ClassType = classType;
		}
	}
	public class ResolveClassInfoByTypeEventArgs : EventArgs {
		public readonly ReflectionDictionary Dictionary;
		public readonly Type ClassType;
		public XPClassInfo ClassInfo;
		public ResolveClassInfoByTypeEventArgs(ReflectionDictionary dictionary, Type classType) {
			this.Dictionary = dictionary;
			this.ClassType = classType;
		}
	}
	public class ReflectionDictionary : XPDictionary {
		public static bool DefaultCanGetClassInfoByType(Type classType) {
			if(classType == null)
				return false;
			if(!classType.IsClass && !classType.IsInterface)
				return false;
			if(classType.ContainsGenericParameters)
				return false;
			if(classType.IsClass && typeof(IXPSimpleObject).IsAssignableFrom(classType))
				return true;
			try {
				if(classType.GetCustomAttributes(PersistentAttribute.AttributeType, true).Length > 0)
					return true;
			}
			catch { }
			try {
				if(classType.GetCustomAttributes(NonPersistentAttribute.AttributeType, true).Length > 0)
					return true;
			}
			catch { }
			return false;
		}
		public override bool CanGetClassInfoByType(Type classType) {
			if(classType == null)
				return false;
			XPClassInfo resolvedClassInfo;
			if(classesByType.TryGetValue(classType, out resolvedClassInfo)) {
				return resolvedClassInfo != null;
			}
			bool resolved;
			EventHandler<CanGetClassInfoByTypeEventArgs> l = CanGetClassInfoByTypeHandler;
			EventHandler<CanGetClassInfoByTypeEventArgs> g = CanGetClassInfoByTypeGlobalHandler;
			if(l != null || g != null) {
				CanGetClassInfoByTypeEventArgs args = new CanGetClassInfoByTypeEventArgs(this, classType);
				if(l != null)
					l(this, args);
				if(g != null)
					g(this, args);
				if(args.CanGetClassInfo.HasValue) {
					resolved = args.CanGetClassInfo.Value;
					if(!resolved && !typeof(DataTable).IsAssignableFrom(classType)) {
						lock(this) {
							if(!classesByType.ContainsKey(classType))
								classesByType.Add(classType, null);
						}
					}
				}
				else {
					resolved = DefaultCanGetClassInfoByType(classType);
				}
			}
			else {
				resolved = DefaultCanGetClassInfoByType(classType);
			}
			return resolved;
		}
		protected virtual XPClassInfo ResolveClassInfoByType(Type classType) {
			if(CanGetClassInfoByType(classType)) {
				XPClassInfo ci;
				EventHandler<ResolveClassInfoByTypeEventArgs> l = ResolveClassInfoByTypeHandler;
				EventHandler<ResolveClassInfoByTypeEventArgs> g = ResolveClassInfoByTypeGlobalHandler;
				if(l != null || g != null) {
					ResolveClassInfoByTypeEventArgs args = new ResolveClassInfoByTypeEventArgs(this, classType);
					if(l != null)
						l(this, args);
					if(g != null)
						g(this, args);
					ci = args.ClassInfo;
					if(ci != null) {
						lock(this) {
							if(!classesByType.ContainsKey(classType))
								classesByType.Add(classType, ci);
							string fullName = ReflectionDictionary.GetFullName(classType);
							if(!classesByName.ContainsKey(fullName))
								classesByName.Add(fullName, ci);
						}
					}
					else {
						ci = CreateClassInfo(classType);
					}
				}
				else {
					ci = CreateClassInfo(classType);
				}
				return ci;
			}
			if(typeof(DataTable).IsAssignableFrom(classType)) {
				try {
					return new XPDataTableClassInfo(this, classType);
				}
				catch { }
			}
			return null;
		}
		protected virtual XPClassInfo CreateClassInfo(Type classType) {
			return new ReflectionClassInfo(classType, this);
		}
		protected virtual XPClassInfo ResolveClassInfoByName(string assemblyName, string typeName) {
			if(assemblyName == null || assemblyName == string.Empty)
				return null;
#pragma warning disable DX0005
			Type type = XPTypeActivator.GetType(assemblyName, typeName);
#pragma warning restore DX0005
			if(type == null)
				return null;
			return ResolveClassInfoByType(type);
		}
		public EventHandler<CanGetClassInfoByTypeEventArgs> CanGetClassInfoByTypeHandler;
		public static EventHandler<CanGetClassInfoByTypeEventArgs> CanGetClassInfoByTypeGlobalHandler;
		public EventHandler<ResolveClassInfoByTypeEventArgs> ResolveClassInfoByTypeHandler;
		public static EventHandler<ResolveClassInfoByTypeEventArgs> ResolveClassInfoByTypeGlobalHandler;
		protected Dictionary<string, XPClassInfo> classesByName = new Dictionary<string, XPClassInfo>();
		protected Dictionary<Type, XPClassInfo> classesByType = new Dictionary<Type, XPClassInfo>();
		[Description("Gets a collection of the XPClassInfo objects that are supplied by the current ReflectionDictionary instance.")]
		public override ICollection Classes {
			get {
				List<XPClassInfo> list = new List<XPClassInfo>();
				foreach(KeyValuePair<string, XPClassInfo> entry in classesByName) {
					XPClassInfo ci = entry.Value;
					if(ci != null)
						list.Add(ci);
				}
				return list;
			}
		}
		protected override void DropDescendantsCache(XPClassInfo changedClassInfo) {
			foreach(KeyValuePair<string, XPClassInfo> entry in classesByName) {
				var ci = entry.Value;
				if(ci != null && ci.BaseClass == changedClassInfo) {
					ci.DoDrop();
				}
			}
		}
		protected override bool HasDescendantsCore(XPClassInfo classInfo) {
			foreach(KeyValuePair<string, XPClassInfo> entry in classesByName) {
				var ci = entry.Value;
				if(ci != null && ci.BaseClass == classInfo) {
					return true;
				}
			}
			return false;
		}
		protected override void AddClassInfoCore(XPClassInfo info) {
			classesByName[info.FullName] = info;
			if(info.CanGetByClassType)
				classesByType.Add(info.ClassType, info);
		}
		public override XPClassInfo QueryClassInfo(Type classType) {
			if(classType == null)
				return null;
			XPClassInfo classInfo;
			if(classesByType.TryGetValue(classType, out classInfo))
				return classInfo;
			lock(this) {
				if(classesByType.TryGetValue(classType, out classInfo))
					return classInfo;
				string fullName = GetFullName(classType);
				if(fullName != null) {
					XPClassInfo classByName;
					if(classesByName.TryGetValue(fullName, out classByName) && classByName != null)
						throw new InvalidOperationException(Res.GetString(Res.Metadata_SeveralClassesWithSameName, classType.Assembly.FullName, classType.FullName));
				}
				XPClassInfo resolved = ResolveClassInfoByType(classType);
				if(resolved == null)
					classesByType[classType] = null;
				return resolved;
			}
		}
		internal static string GetFullName(Type classType) {
			string name = classType.FullName;
			if(classType.ContainsGenericParameters)
				return null;
			if(classType.IsGenericType) {
				name = classType.Namespace + "." + classType.Name + "<";
				bool addComma = false;
				foreach(Type t in classType.GetGenericArguments()) {
					if(addComma)
						name += ",";
					else
						addComma = true;
					name += GetFullName(t);
				}
				name += ">";
			}
			return name;
		}
		public override XPClassInfo QueryClassInfo(string assemblyName, string className) {
			if(className == null)
				return null;
			XPClassInfo classInfo;
			if(classesByName.TryGetValue(className, out classInfo))
				return classInfo;
			lock(this) {
				if(classesByName.TryGetValue(className, out classInfo))
					return classInfo;
				XPClassInfo resolved = ResolveClassInfoByName(assemblyName, className);
				if(resolved == null)
					classesByName.Add(className, null);
				return resolved;
			}
		}
		private void ClearClasses() {
			classesByName = new Dictionary<string, XPClassInfo>();
			classesByType = new Dictionary<Type, XPClassInfo>();
		}
	}
	public class DesignTimeReflection : ReflectionDictionary {
		readonly IDesignerHost host;
		readonly ITypeResolutionService typeResolution;
		readonly ISolutionTypesProvider solutionTypesProvider;
		public DesignTimeReflection(IServiceProvider provider) : this(provider, false) { }
		public DesignTimeReflection(IServiceProvider provider, bool useSolutionTypesProvider) {
			IServiceContainer sc = (IServiceContainer)provider.GetService(typeof(IServiceContainer));
			host = (IDesignerHost)sc.GetService(typeof(IDesignerHost));
			typeResolution = (ITypeResolutionService)sc.GetService(typeof(ITypeResolutionService));
			if(useSolutionTypesProvider) {
				solutionTypesProvider = (ISolutionTypesProvider)sc.GetService(typeof(ISolutionTypesProvider));
			}
		}
		Dictionary<AssemblyName, bool> list = new Dictionary<AssemblyName, bool>(new AssemblyNameEqualityComparer());
		protected internal override bool UseStrictMetadataValidation {
			get { return false; }
		}
		bool Init(AssemblyName name) {
			if(list.ContainsKey(name)) {
				return list[name];
			}
			Assembly assembly = null;
			try {
				assembly = typeResolution.GetAssembly(name, false);
			}
			catch { }
			if(assembly == null) {
				try {
					assembly = SafeTypeResolver.GetOrLoadAssembly(name);
				}
				catch { }
			}
			if(assembly == null)
				return false;
			return Init(assembly);
		}
		bool Init(Assembly assembly) {
			AssemblyName definition = assembly.GetName();
			if(!list.ContainsKey(definition)) {
				list.Add(definition, false);
			}
			bool contains = false;
			string xpoAssembly = typeof(IXPSimpleObject).Assembly.GetName().Name;
			if(assembly.GetName().Name == xpoAssembly) {
				contains = true;
			}
			else {
				foreach(AssemblyName assemblyN in assembly.GetReferencedAssemblies()) {
					if(Init(assemblyN))
						contains = true;
				}
			}
			if(contains) {
				contains = false;
				Type[] types;
				try {
					types = assembly.GetTypes();
				}
				catch {
					types = Array.Empty<Type>();
				}
				foreach(Type t in types) {
					Type target = typeResolution.GetType(t.FullName);
					if(target != null && !typeof(DataTable).IsAssignableFrom(target)) {
						try {
							if(QueryClassInfo(target) != null) {
								contains = true;
							}
						}
						catch { }
					}
				}
			}
			list[definition] = contains;
			return contains;
		}
		bool inited = false;
		public override ICollection Classes {
			get {
				Init();
				return base.Classes;
			}
		}
		void Init() {
			bool resetClasses = false;
			foreach(XPClassInfo ci in base.Classes) {
				if(ci.ClassType != null && ci.ClassType != typeResolution.GetType(ci.ClassType.FullName)) {
					inited = false;
					resetClasses = true;
					break;
				}
			}
			if(!inited) {
				if(solutionTypesProvider != null) {
					InitUsingISolutionTypesProvider(resetClasses);
				}
				else {
					InitUsingITypeDiscoveryService(resetClasses);
				}
			}
		}
		void InitUsingITypeDiscoveryService(bool resetClasses) {
			Type rootType = typeResolution.GetType(host.RootComponentClassName);
			if(rootType != null) {
				if(resetClasses) {
					classesByName.Clear();
					classesByType.Clear();
					designCollections = null;
				}
				inited = true;
				Init(typeResolution.GetType(host.RootComponentClassName).Assembly);
				ITypeDiscoveryService tr = (ITypeDiscoveryService)host.GetService(typeof(ITypeDiscoveryService));
				list.Clear();
			}
			else if(!resetClasses) {
				Hashtable assemblies = new Hashtable();
				foreach(XPClassInfo ci in base.Classes) {
					if(ci.ClassType != null)
						assemblies[ci.ClassType.Assembly] = null;
				}
				foreach(Assembly assembly in assemblies.Keys)
					Init(assembly.GetName());
				list.Clear();
			}
		}
		void InitUsingISolutionTypesProvider(bool resetClasses) {
			if(resetClasses) {
				classesByName.Clear();
				classesByType.Clear();
				designCollections = null;
			}
			inited = true;
			IEnumerable<IDXTypeInfo> solutionTypes = solutionTypesProvider.GetTypes();
			var resolvedTypes = new Dictionary<IDXTypeInfo, Type>();
			foreach(IDXTypeInfo typeInfo in solutionTypes) {
				Type target = typeResolution.GetType(typeInfo.FullName);
				if(target != null && !typeof(DataTable).IsAssignableFrom(target)) {
					resolvedTypes[typeInfo] = target;
					try {
						QueryClassInfo(target);
					}
					catch { }
				}
			}
			list.Clear();
		}
		protected override XPClassInfo ResolveClassInfoByName(string assemblyName, string typeName) {
			Init();
			Type type = typeResolution.GetType(typeName);
			if(type == null)
				return null;
			XPClassInfo ci;
			if(classesByName.TryGetValue(typeName, out ci))
				return ci;
			return ResolveClassInfoByType(type);
		}
		protected override void DropDescendantsCache(XPClassInfo changedClassInfo) { }
		Dictionary<XPClassInfo, XPCollection> designCollections;
		internal object GetDesignCollection(XPClassInfo classInfo, Session session) {
			XPCollection col;
			if(designCollections == null)
				designCollections = new Dictionary<XPClassInfo, XPCollection>();
			else
				if(designCollections.TryGetValue(classInfo, out col)) {
				if(col.Count == 0)
					return col;
			}
			col = new XPCollection(session, classInfo, false);
			GC.SuppressFinalize(col);
			col.BindingBehavior = CollectionBindingBehavior.AllowNew | CollectionBindingBehavior.AllowRemove;
			designCollections[classInfo] = col;
			return col;
		}
		internal sealed class AssemblyNameEqualityComparer : IEqualityComparer<AssemblyName> {
			bool IEqualityComparer<AssemblyName>.Equals(AssemblyName x, AssemblyName y) {
				return AssemblyName.ReferenceMatchesDefinition(y, x);
			}
			int IEqualityComparer<AssemblyName>.GetHashCode(AssemblyName obj) {
				return obj.FullName.GetHashCode();
			}
		}
	}
	class DataModelWizardDictionary : ReflectionDictionary {
		protected internal override bool UseStrictMetadataValidation {
			get { return false; }
		}
	}
	static class IsInstanceOfTypeFunctionXpoHelper {
		public static bool? Evaluate(object obj, string typeString) {
			IXPSimpleObject simpleobject = obj as IXPSimpleObject;
			ISessionProvider sessionProviderObject = obj as ISessionProvider;
			XPClassInfo objectClassInfo = null;
			if(simpleobject != null) {
				objectClassInfo = simpleobject.ClassInfo;
			}
			else if(sessionProviderObject != null) {
				objectClassInfo = sessionProviderObject.Session.Dictionary.QueryClassInfo(obj);
			}
			if(objectClassInfo != null) {
				return IsInstanceOfType(typeString, objectClassInfo);
			}
			return null;
		}
		public static bool IsInstanceOfType(string typeString, XPClassInfo objectClassInfo) {
			XPClassInfo searchedClassInfo = null;
			if(!MemberInfoCollection.TryResolveTypeAlsoByShortName(typeString, objectClassInfo, out searchedClassInfo)) return false;
			return objectClassInfo.IsAssignableTo(searchedClassInfo);
		}
		public static bool IsInstanceOfType(string typeString, XPClassInfo objectClassInfo, out XPClassInfo searchedClassInfo) {
			if(!MemberInfoCollection.TryResolveTypeAlsoByShortName(typeString, objectClassInfo, out searchedClassInfo)) return false;
			return objectClassInfo.IsAssignableTo(searchedClassInfo);
		}
	}
	static class IsExactTypeFunctionXpoHelper {
		public static bool? Evaluate(object obj, string typeString) {
			IXPSimpleObject simpleObject = obj as IXPSimpleObject;
			ISessionProvider sessionProviderObject = obj as ISessionProvider;
			string objectTypeString = null;
			if(simpleObject != null) {
				objectTypeString = simpleObject.ClassInfo.FullName;
			}
			else if(sessionProviderObject != null) {
				XPClassInfo classInfo = sessionProviderObject.Session.Dictionary.QueryClassInfo(obj);
				if(classInfo != null) {
					objectTypeString = classInfo.FullName;
				}
			}
			if(objectTypeString != null) {
				return IsInstanceOfTypeFunctionHelper.EqualsType(typeString, objectTypeString);
			}
			return null;
		}
	}
}
