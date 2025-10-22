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

namespace DevExpress.Xpo {
	using System;
	using System.Collections.Specialized;
	using System.Xml;
	using System.Globalization;
	using DevExpress.Data.Filtering;
	using DevExpress.Xpo.Metadata;
	using System.ComponentModel;
	using DevExpress.Utils;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using DevExpress.Xpo.Metadata.Helpers;
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
	public sealed class ValueConverterAttribute : Attribute {
		Type converterType;
		[Description("Gets the converter’s type.")]
		public Type ConverterType { get { return converterType; } set { converterType = value; } }
		public ValueConverterAttribute(Type converterType) {
			this.converterType = converterType;
		}
		ValueConverterAttribute(XmlNode attributeNode) {
			if(attributeNode.Attributes["ConverterType"] != null)
				converterType = DevExpress.Data.Internal.SafeTypeResolver.GetKnownUserType(attributeNode.Attributes["ConverterType"].Value);
		}
		ValueConverter converter;
		[Description("Gets the value converter.")]
		public ValueConverter Converter {
			get {
				if(converter == null) {
					converter = (ValueConverter)Activator.CreateInstance(ConverterType);
				}
				return converter;
			}
		}
	}
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
	public sealed class NullValueAttribute : Attribute {
		object attributeValue;
		[Description("Gets or sets a null value for a property or a field.")]
		public object Value { get { return attributeValue; } set { this.attributeValue = value; } }
		public NullValueAttribute(object attributeValue) {
			this.attributeValue = attributeValue;
		}
		public NullValueAttribute(Byte attributeValue) : this((object)attributeValue) { }
		public NullValueAttribute(short attributeValue) : this((object)attributeValue) { }
		public NullValueAttribute(int attributeValue) : this((object)attributeValue) { }
		public NullValueAttribute(long attributeValue) : this((object)attributeValue) { }
		public NullValueAttribute(Single attributeValue) : this((object)attributeValue) { }
		public NullValueAttribute(Double attributeValue) : this((object)attributeValue) { }
		public NullValueAttribute(Char attributeValue) : this((object)attributeValue) { }
		public NullValueAttribute(string attributeValue) : this((object)attributeValue) { }
		public NullValueAttribute(bool attributeValue) : this((object)attributeValue) { }
		public NullValueAttribute(Type type, string attributeValue) {
			try {
				this.attributeValue = System.ComponentModel.TypeDescriptor.GetConverter(type).ConvertFromInvariantString(attributeValue);
			} catch { }
		}
	}
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
	public sealed class ExplicitLoadingAttribute : Attribute {
		int depth;
		[Description("Gets or sets the reference depth of the current class from the root class.")]
		public int Depth { get { return depth; } set { depth = value; } }
		public ExplicitLoadingAttribute() : this(1) { }
		public ExplicitLoadingAttribute(int depth) {
			this.depth = depth;
		}
		ExplicitLoadingAttribute(XmlNode attributeNode) {
			if(attributeNode.Attributes["Depth"] != null)
				depth = ((IConvertible)attributeNode.Attributes["Depth"].Value).ToInt32(CultureInfo.InvariantCulture);
		}
	}
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
	public sealed class DbTypeAttribute : Attribute {
		string dbColumnTypeName = string.Empty;
		[Description("Gets or sets the database type of the column which a property marked with DbTypeAttribute is mapped to.")]
		public string DbColumnTypeName { get { return dbColumnTypeName; } set { dbColumnTypeName = value; } }
		public DbTypeAttribute(string dbColumnTypeName) {
			this.dbColumnTypeName = dbColumnTypeName;
		}
		DbTypeAttribute(XmlNode attributeNode) {
			if(attributeNode.Attributes["DbColumnTypeName"] != null)
				dbColumnTypeName = attributeNode.Attributes["DbColumnTypeName"].Value;
		}
	}
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
	public sealed class AggregatedAttribute : Attribute { }
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
	public sealed class MergeCollisionBehaviorAttribute : Attribute {
		OptimisticLockingReadMergeBehavior behavior;
		public OptimisticLockingReadMergeBehavior Behavior {
			get { return behavior; }
		}
		public MergeCollisionBehaviorAttribute(OptimisticLockingReadMergeBehavior behavior) {
			this.behavior = behavior;
		}
	}
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
	public sealed class OptimisticLockingIgnoredAttribute : Attribute { }
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = true)]
	public sealed class OptimisticLockingReadBehaviorAttribute : Attribute {
		bool? trackPropertiesModifications;
		public bool? TrackPropertiesModifications {
			get { return trackPropertiesModifications; }
		}
		OptimisticLockingReadBehavior behavior;
		public OptimisticLockingReadBehavior Behavior {
			get { return behavior; }
		}
		public OptimisticLockingReadBehaviorAttribute(OptimisticLockingReadBehavior behavior) {
			this.behavior = behavior;
		}
		public OptimisticLockingReadBehaviorAttribute(OptimisticLockingReadBehavior behavior, bool trackPropertiesModifications)
			: this(behavior) {
			this.trackPropertiesModifications = trackPropertiesModifications;
		}
	}
	public enum OptimisticLockingBehavior {
		NoLocking,
		ConsiderOptimisticLockingField,
		LockModified,
		LockAll
	}
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = true)]
	public sealed class OptimisticLockingAttribute : Attribute {
		OptimisticLockingBehavior lockingKind;
		public const string DefaultFieldName = "OptimisticLockField";
		string fieldName;
		[Description("Gets or sets whether optimistic locking is enabled.")]
		public bool Enabled { get { return lockingKind == OptimisticLockingBehavior.ConsiderOptimisticLockingField; } set { lockingKind = value ? OptimisticLockingBehavior.ConsiderOptimisticLockingField : OptimisticLockingBehavior.NoLocking; } }
		[Description("Gets the name of the system field which is used to control object locking for objects that have the object locking option enabled.")]
		public string FieldName { get { return fieldName; } }
		public OptimisticLockingBehavior LockingKind {
			get { return lockingKind; }
			set { lockingKind = value; }
		}
		public OptimisticLockingAttribute() : this(true) { }
		public OptimisticLockingAttribute(bool enabled) {
			this.Enabled = enabled;
			fieldName = DefaultFieldName;
		}
		public OptimisticLockingAttribute(string fieldName) {
			this.fieldName = fieldName;
			this.Enabled = true;
		}
		public OptimisticLockingAttribute(OptimisticLockingBehavior lockingKind) {
			fieldName = DefaultFieldName;
			this.lockingKind = lockingKind;
		}
		OptimisticLockingAttribute(XmlNode attributeNode) {
			if(attributeNode.Attributes["Enabled"] != null)
				Enabled = attributeNode.Attributes["Enabled"].Value.ToLower(CultureInfo.InvariantCulture) == "true";
			if(attributeNode.Attributes["FieldName"] != null)
				fieldName = attributeNode.Attributes["FieldName"].Value;
		}
	}
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
	public sealed class KeyAttribute : Attribute {
		bool autoGenerate;
		[Description("Gets or sets whether the key is generated automatically.")]
		public bool AutoGenerate { get { return autoGenerate; } set { autoGenerate = value; } }
		public KeyAttribute() : this(false) { }
		public KeyAttribute(bool autoGenerate) {
			this.autoGenerate = autoGenerate;
		}
		KeyAttribute(XmlNode attributeNode) {
			if(attributeNode.Attributes["AutoGenerate"] != null)
				autoGenerate = attributeNode.Attributes["AutoGenerate"].Value.ToLower(CultureInfo.InvariantCulture) == "true";
		}
	}
	public enum MapInheritanceType { ParentTable, OwnTable }
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false)]
	public sealed class MapInheritanceAttribute : Attribute {
		MapInheritanceType mapType;
		[Description("Gets a value which specifies the table that persistent properties and fields are saved to.")]
		public MapInheritanceType MapType { get { return mapType; } }
		public MapInheritanceAttribute(MapInheritanceType mapType) {
			this.mapType = mapType;
		}
		MapInheritanceAttribute(XmlNode attributeNode) {
			this.mapType = (MapInheritanceType)Enum.Parse(typeof(MapInheritanceType), attributeNode.Attributes["MapType"].Value, false);
		}
	}
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Interface, Inherited = true)]
	public sealed class PersistentAttribute : Attribute {
		string mapTo;
		[Description("Gets the name of the table or column to which to map a class or a property/field.")]
		public string MapTo { get { return mapTo; } }
		public static readonly Type AttributeType = typeof(PersistentAttribute);
		public PersistentAttribute() { }
		public PersistentAttribute(string mapTo) {
			this.mapTo = mapTo;
		}
		PersistentAttribute(XmlNode attributeNode) {
			if(attributeNode.Attributes["MapTo"] != null)
				mapTo = attributeNode.Attributes["MapTo"].Value;
		}
	}
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
	public sealed class AssociationAttribute : Attribute {
		string name;
		string assemblyName;
		string elementTypeName;
		Type elementType;
		bool _UseAssociationNameAsIntermediateTableName = false;
		[Description("Gets the name of the association.")]
		public string Name { get { return name; } }
		[Description("Gets or sets the full name of the type which describes the object on the opposite end of the association.")]
		public string ElementTypeName {
			get { return elementTypeName; }
			set { elementTypeName = value; }
		}
		internal Type ElementType {
			get { return elementType; }
		}
		[Description("Gets or sets the assembly name where the type which is associated with the object at the opposite end of the association is declared.")]
		public string AssemblyName {
			get { return assemblyName; }
			set { assemblyName = value; }
		}
		[Description("Gets or sets whether the association’s name is used as the name of a junction table in a many-to-many relationship.")]
		public bool UseAssociationNameAsIntermediateTableName {
			get { return _UseAssociationNameAsIntermediateTableName; }
			set { _UseAssociationNameAsIntermediateTableName = value; }
		}
		AssociationAttribute(XmlNode attributeNode)
			: this(attributeNode.Attributes["Name"].Value, attributeNode.Attributes["AssemblyName"].Value, attributeNode.Attributes["ElementTypeName"].Value) {
		}
		public AssociationAttribute(string name, string elementAssemblyName, string elementTypeName) {
			this.name = name;
			this.assemblyName = elementAssemblyName;
			this.elementTypeName = elementTypeName;
		}
		public AssociationAttribute() : this(string.Empty, string.Empty, string.Empty) { }
		public AssociationAttribute(string name) : this(name, string.Empty, string.Empty) { }
		public AssociationAttribute(string name, Type elementType)
			: this(name, XPClassInfo.GetShortAssemblyName(elementType.Assembly), elementType.FullName) {
			this.elementType = elementType;
		}
		public AssociationAttribute(Type elementType) : this(string.Empty, elementType) { }
	}
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Interface)]
	public sealed class NonPersistentAttribute : Attribute {
		public static readonly Type AttributeType = typeof(NonPersistentAttribute);
	}
	[Obsolete("Please use Persistent attribute with mapTo parameter instead")]
	[AttributeUsage(AttributeTargets.Class, Inherited = true)]
	[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
	public sealed class MapToAttribute : Attribute {
		string mappingName;
		[Description("")]
		public string MappingName { get { return mappingName; } }
		MapToAttribute(XmlNode attributeNode) {
			this.mappingName = attributeNode.Attributes["MappingName"].Value;
		}
		[Obsolete("Please use Persistent attribute with mapTo parameter instead")]
		public MapToAttribute(string mappingName) {
			this.mappingName = mappingName;
		}
	}
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
	public sealed class SizeAttribute : Attribute {
		Int32 size = 0;
		[Description("Gets the size of the database column which the member’s data is stored in.")]
		public Int32 Size { get { return size; } }
		SizeAttribute(XmlNode attributeNode) {
			string ssize = attributeNode.Attributes["Size"].Value;
			this.size = ssize == "Unlimited" ? SizeAttribute.Unlimited : Convert.ToInt32(ssize);
		}
		public SizeAttribute(Int32 size) {
			this.size = size;
		}
		public const Int32 Unlimited = -1;
		public const Int32 DefaultStringMappingFieldSize = 100;
	}
	[AttributeUsage(AttributeTargets.Property, Inherited = true)]
	public sealed class DelayedAttribute : Attribute {
		bool updateModifiedOnly;
		string fieldName;
		string groupName;
		DelayedAttribute(XmlNode attributeNode) {
			XmlAttribute fld = attributeNode.Attributes["FieldName"];
			if(fld != null)
				this.fieldName = fld.Value;
			XmlAttribute gr = attributeNode.Attributes["GroupName"];
			if(gr != null)
				this.groupName = gr.Value;
		}
		public DelayedAttribute() : this(null, null) { }
		public DelayedAttribute(bool updateModifiedOnly) : this(null, null, updateModifiedOnly) { }
		public DelayedAttribute(string fieldName)
			: this(fieldName, null) { }
		public DelayedAttribute(string fieldName, bool updateModifiedOnly)
			: this(fieldName, null, updateModifiedOnly) { }
		public DelayedAttribute(string fieldName, string groupName)
			: this(fieldName, groupName, false) { }
		public DelayedAttribute(string fieldName, string groupName, bool updateModifiedOnly) {
			this.fieldName = fieldName;
			this.groupName = groupName;
			this.updateModifiedOnly = updateModifiedOnly;
		}
		[Description("Gets the name of the field which stores the value of the delayed property.")]
		public string FieldName { get { return fieldName; } }
		[Description("Gets the group’s name.")]
		public string GroupName { get { return groupName; } }
		[Description("Gets whether the delayed property stores all or only modified values to a data store.")]
		public bool UpdateModifiedOnly { get { return updateModifiedOnly; } }
	}
	[AttributeUsage(AttributeTargets.Property, Inherited = true)]
	public sealed class PersistentAliasAttribute : Attribute {
		string _aliasExpression;
		CriteriaOperator _expression;
		PersistentAliasAttribute(XmlNode attributeNode) {
			this._aliasExpression = attributeNode.Attributes["AliasExpression"].Value;
		}
		public PersistentAliasAttribute(string aliasExpression) { this._aliasExpression = aliasExpression; }
		[Description("Gets the expression  which determines how the property’s value is calculated.")]
		public string AliasExpression { get { return _aliasExpression; } }
		internal CriteriaOperator Criteria {
			get {
				if(ReferenceEquals(_expression, null))
					_expression = CriteriaOperator.Parse(AliasExpression);
				return _expression;
			}
		}
	}
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = true)]
	public sealed class CustomAttribute : Attribute {
		string theName;
		string theValue;
		[Description("Gets the attribute’s name.")]
		public string Name { get { return theName; } }
		[Description("Gets the attribute’s value.")]
		public string Value { get { return theValue; } }
		CustomAttribute(XmlNode attributeNode) {
			theName = attributeNode.Attributes["Name"].Value;
			theValue = attributeNode.Attributes["Value"].Value;
		}
		public CustomAttribute(string theName, string theValue) {
			this.theName = theName;
			this.theValue = theValue;
		}
	}
	[AttributeUsage(AttributeTargets.Class, Inherited = true)]
	public sealed class IndicesAttribute : Attribute {
		static char[] splitter = new char[] { ';' };
		IList<StringCollection> indices;
		public IList<StringCollection> Indices {
			get { return indices; }
		}
		public IndicesAttribute()
			: this((string[])null) {
		}
		public IndicesAttribute(string index)
			: this(new string[] { index }) {
		}
		public IndicesAttribute(string index1, string index2)
			: this(new string[] { index1, index2 }) {
		}
		public IndicesAttribute(string index1, string index2, string index3)
			: this(new string[] { index1, index2, index3 }) {
		}
		public IndicesAttribute(params string[] indices) {
			if(indices == null || indices.Length == 0) {
				this.indices = Array.Empty<StringCollection>();
				return;
			}
			List<StringCollection> result = new List<StringCollection>(indices.Length);
			foreach(string index in indices) {
				StringCollection indexColumns = new StringCollection();
				indexColumns.AddRange(index.Split(splitter, StringSplitOptions.RemoveEmptyEntries));
				result.Add(indexColumns);
			}
			this.indices = result.AsReadOnly();
		}
	}
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true)]
	public sealed class IndexedAttribute : Attribute {
		bool unique;
		string name;
		StringCollection additionalFields = new StringCollection();
		[Description("Gets or sets whether the index created by a property or field is a unique one.")]
		public bool Unique {
			get { return unique; }
			set { unique = value; }
		}
		[Description("Gets or sets the name of the index.")]
		public string Name {
			get { return name; }
			set { name = value; }
		}
		[Description("Gets the names of additional columns that affect index creation.")]
		public StringCollection AdditionalFields {
			get { return additionalFields; }
		}
		IndexedAttribute(XmlNode attributeNode) {
			if(attributeNode.Attributes["Unique"] != null)
				unique = Convert.ToBoolean(attributeNode.Attributes["Unique"].Value);
			if(attributeNode.Attributes["Name"] != null)
				name = attributeNode.Attributes["Name"].Value;
			XmlNode additionalFieldsNode = null;
			foreach(XmlNode chNode in attributeNode.ChildNodes) {
				if(chNode.Name == "AdditionalFields") {
					additionalFieldsNode = chNode;
					break;
				}
			}
			if(additionalFieldsNode != null) {
				foreach(XmlNode chNode in additionalFieldsNode.ChildNodes) {
					if(chNode.Name == "string") {
						additionalFields.Add(chNode.InnerText);
					}
				}
			}
		}
		public IndexedAttribute(string additionalFields)
			: this(additionalFields.Split(';')) {
		}
		public IndexedAttribute() {
			unique = false;
		}
		public IndexedAttribute(params string[] additionalFields) {
			unique = false;
			this.additionalFields.AddRange(additionalFields);
		}
		public IndexedAttribute(string additionalField1, string additionalField2)
			: this(new string[] { additionalField1, additionalField2 }) {
		}
		public IndexedAttribute(string additionalField1, string additionalField2, string additionalField3)
			: this(new string[] { additionalField1, additionalField2, additionalField3 }) {
		}
	}
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class, Inherited = true)]
	public sealed class MemberDesignTimeVisibilityAttribute : Attribute {
		bool isVisible = true;
		MemberDesignTimeVisibilityAttribute(XmlNode attributeNode) {
			if(attributeNode.Attributes["IsVisible"] != null)
				this.isVisible = Convert.ToBoolean(attributeNode.Attributes["IsVisible"].Value);
		}
		public MemberDesignTimeVisibilityAttribute() { }
		public MemberDesignTimeVisibilityAttribute(bool isVisible) { this.isVisible = isVisible; }
		[Description("Gets whether a property or class is visible at design time.")]
		public bool IsVisible { get { return isVisible; } }
	}
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true)]
	public sealed class NoForeignKeyAttribute : Attribute {
	}
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = true)]
	public sealed class DeferredDeletionAttribute : Attribute {
		bool enabled;
		[Description("Gets or sets whether deferred object deletion is enabled.")]
		public bool Enabled { get { return enabled; } set { enabled = value; } }
		public DeferredDeletionAttribute() : this(true) { }
		public DeferredDeletionAttribute(bool enabled) {
			this.enabled = enabled;
		}
		DeferredDeletionAttribute(XmlNode attributeNode) {
			enabled = true;
			if(attributeNode.Attributes["Enabled"] != null)
				enabled = attributeNode.Attributes["Enabled"].Value.ToLower(CultureInfo.InvariantCulture) == "true";
		}
	}
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true)]
	public class DisplayNameAttribute : Attribute {
		string displayName;
		DisplayNameAttribute(XmlNode attributeNode) {
			if(attributeNode.Attributes["DisplayName"] != null)
				this.displayName = attributeNode.Attributes["DisplayName"].Value;
		}
		public DisplayNameAttribute() { }
		public DisplayNameAttribute(string displayName) { this.displayName = displayName; }
		[Description("Gets the member’s display name.")]
		public virtual string DisplayName { get { return displayName; } }
	}
	public enum DefaultMembersPersistence { Default, OnlyDeclaredAsPersistent }
	[AttributeUsage(AttributeTargets.Class, Inherited = true)]
	public sealed class DefaultMembersPersistenceAttribute : Attribute {
		DefaultMembersPersistence defaultPersistence;
		[Description("Gets a value that determines which members are persistent by default.")]
		public DefaultMembersPersistence DefaultMembersPersistence { get { return defaultPersistence; } }
		public DefaultMembersPersistenceAttribute(DefaultMembersPersistence defaultPersistence) {
			this.defaultPersistence = defaultPersistence;
		}
		DefaultMembersPersistenceAttribute(XmlNode attributeNode) {
			this.defaultPersistence = (DefaultMembersPersistence)Enum.Parse(typeof(DefaultMembersPersistence), attributeNode.Attributes["DefaultMembersPersistence"].Value, false);
		}
	}
	[AttributeUsage(AttributeTargets.Property, Inherited = true)]
	public sealed class ManyToManyAliasAttribute : Attribute {
		string _OneToManyCollectionName;
		string _ReferenceInTheIntermediateTableName;
		public string OneToManyCollectionName {
			get { return _OneToManyCollectionName; }
			set { _OneToManyCollectionName = value; }
		}
		public string ReferenceInTheIntermediateTableName {
			get { return _ReferenceInTheIntermediateTableName; }
			set { _ReferenceInTheIntermediateTableName = value; }
		}
		ManyToManyAliasAttribute(XmlNode attributeNode)
			: this(attributeNode.Attributes["OneToManyCollectionName"].Value, attributeNode.Attributes["ReferenceInTheIntermediateTableName"].Value) {
		}
		public ManyToManyAliasAttribute(string oneToManyCollectionName, string referenceInTheIntermediateTableName) {
			this._OneToManyCollectionName = oneToManyCollectionName;
			this._ReferenceInTheIntermediateTableName = referenceInTheIntermediateTableName;
		}
	}
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true)]
	public sealed class NullableAttribute : Attribute {
		bool isNullable;
		public NullableAttribute(bool isNullable) {
			this.isNullable = isNullable;
		}
		NullableAttribute(XmlNode attributeNode) {
			if(attributeNode.Attributes["IsNullable"] != null)
				isNullable = attributeNode.Attributes["IsNullable"].Value.ToLower(CultureInfo.InvariantCulture) == "true";
		}
		public bool IsNullable {
			get { return isNullable; }
			set { isNullable = value; }
		}
	}
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true)]
	public sealed class ColumnDefaultValueAttribute : Attribute {
		object defaultValue;
		public ColumnDefaultValueAttribute(object defaultValue) {
			this.defaultValue = defaultValue;
		}
		public ColumnDefaultValueAttribute(int dateYear, int dateMonth, int dateDay, int dateHour, int dateMinute, int dateSecond, int dateMillisecond) {
			this.defaultValue = new DateTime(dateYear, dateMonth, dateDay, dateHour, dateMinute, dateSecond, dateMillisecond);
		}
		public ColumnDefaultValueAttribute(int guidPartA, short guidPartB, short guidPartC, byte guidPartD, byte guidPartE, byte guidPartF, byte guidPartG, byte guidPartH, byte guidPartI, byte guidPartJ, byte guidPartK) {
			this.defaultValue = new Guid(guidPartA, guidPartB, guidPartC, guidPartD, guidPartE, guidPartF, guidPartG, guidPartH, guidPartI, guidPartJ, guidPartK);
		}
		ColumnDefaultValueAttribute(XmlNode attributeNode) {
			if(attributeNode.Attributes["DefaultValue"] != null) {
				defaultValue = attributeNode.Attributes["DefaultValue"].Value;
			}
		}
		public object DefaultValue {
			get { return defaultValue; }
			set { defaultValue = value; }
		}
	}
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true)]
	public sealed class ColumnDbDefaultValueAttribute : Attribute {
		string dbDefaultValue;
		public ColumnDbDefaultValueAttribute(string dbDefaultValue) {
			this.dbDefaultValue = dbDefaultValue;
		}
		ColumnDbDefaultValueAttribute(XmlNode attributeNode) {
			if(attributeNode.Attributes["DbDefaultValue"] != null) {
				dbDefaultValue = attributeNode.Attributes["DbDefaultValue"].Value;
			}
		}
		public string DbDefaultValue {
			get { return dbDefaultValue; }
			set { dbDefaultValue = value; }
		}
	}
	[AttributeUsage(AttributeTargets.Class, Inherited = true)]
	public sealed class NullableBehaviorAttribute : Attribute {
		NullableBehavior nullableBehavior;
		public NullableBehaviorAttribute(NullableBehavior nullableBehavior) {
			this.nullableBehavior = nullableBehavior;
		}
		NullableBehaviorAttribute(XmlNode attributeNode) {
			nullableBehavior = (NullableBehavior)Enum.Parse(typeof(NullableBehavior), attributeNode.Attributes["NullableBehavior"].Value, false);
		}
		public NullableBehavior NullableBehavior {
			get { return nullableBehavior; }
		}
	}
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true)]
	public sealed class FetchOnlyAttribute : Attribute {
		public FetchOnlyAttribute() { }
		FetchOnlyAttribute(XmlNode attributeNode) { }
	}
}
