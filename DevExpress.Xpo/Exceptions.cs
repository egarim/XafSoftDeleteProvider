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

namespace DevExpress.Xpo.Exceptions {
	using System;
	using System.Globalization;
	using System.Collections;
	using DevExpress.Xpo.Helpers;
	using System.ComponentModel;
#if !NET 
	using System.Runtime.Serialization;
#endif
	using DevExpress.Xpo.Metadata;
	using Compatibility.System;
	[Serializable]
	public class CannotFindAppropriateConnectionProviderException : Exception {
#if !NET
		protected CannotFindAppropriateConnectionProviderException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif
		public CannotFindAppropriateConnectionProviderException(string connectionString) : base(Res.GetString(Res.Session_WrongConnectionString, connectionString)) { }
	}
#if !NET
	[Serializable]
#endif
	public class TypeNotFoundException : Exception {
		int typeId;
#if !NET
		protected TypeNotFoundException(SerializationInfo info, StreamingContext context)
			: base(info, context) {
		}
#endif
		public TypeNotFoundException(Int32 typeId)
			: base(Res.GetString(Res.Session_TypeNotFound, typeId)) {
			this.typeId = typeId;
		}
		[Description("Gets the object type identifier.")]
		public Int32 TypeId { get { return typeId; } set { typeId = value; } }
	}
#if !NET
	[Serializable]
#endif
	public class TypeFieldIsEmptyException : Exception {
		string baseType;
#if !NET
		protected TypeFieldIsEmptyException(SerializationInfo info, StreamingContext context)
			: base(info, context) {
		}
#endif
		public TypeFieldIsEmptyException(string baseType)
			: base(Res.GetString(Res.Session_TypeFieldIsEmpty, XPObjectType.ObjectTypePropertyName, baseType)) {
			this.baseType = baseType;
		}
		[Obsolete("Use Message instead.", false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Browsable(false)]
		[Description("For internal use.")]
		public string BaseType { get { return baseType; } }
	}
#if !NET
	[Serializable]
#endif
	public class UnableToFillRefTypeException : Exception {
		string objectName;
		string memberName;
#if !NET
		protected UnableToFillRefTypeException(SerializationInfo info, StreamingContext context)
			: base(info, context) {
		}
#endif
		[Description("Gets the name of the object type that causes the exception.")]
		public string ObjectName { get { return objectName; } set { objectName = value; } }
		[Description("Gets the property name of the currently processed object.")]
		public string MemberName { get { return memberName; } set { memberName = value; } }
		public UnableToFillRefTypeException(string objectName, string memberName, Exception innerException)
			:
			base(Res.GetString(Res.ConnectionProvider_UnableToFillRefType, objectName, memberName, innerException.Message), innerException) {
			this.objectName = objectName;
			this.memberName = memberName;
		}
	}
#if !NET
	[Serializable]
#endif
	public class KeyPropertyAbsentException : Exception {
		string className;
#if !NET
		protected KeyPropertyAbsentException(SerializationInfo info, StreamingContext context)
			: base(info, context) {
		}
#endif
		public KeyPropertyAbsentException(string className)
			: base(Res.GetString(Res.MetaData_KeyPropertyAbsent, className)) {
			this.className = className;
		}
		[Obsolete("Use Message instead.", false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Browsable(false)]
		[Description("Gets the string that specifies the class name.")]
		public string ClassName { get { return className; } }
	}
#if !NET
	[Serializable]
#endif
	public class DuplicateKeyPropertyException : Exception {
		string className;
#if !NET
		protected DuplicateKeyPropertyException(SerializationInfo info, StreamingContext context)
			: base(info, context) {
		}
#endif
		public DuplicateKeyPropertyException(string className)
			: base(Res.GetString(Res.MetaData_DuplicateKeyProperty, className)) {
			this.className = className;
		}
		[Obsolete("Use Message instead.", false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Browsable(false)]
		[Description("Gets the name of the persistent class in which the key property is declared twice.")]
		public string ClassName { get { return className; } }
	}
#if !NET
	[Serializable]
#endif
	public class CannotResolveClassInfoException : Exception {
		string typeName;
		string assemblyName;
#if !NET
		protected CannotResolveClassInfoException(SerializationInfo info, StreamingContext context)
			: base(info, context) {
		}
#endif
		public CannotResolveClassInfoException(string assemblyName, string typeName)
			: base(Res.GetString(Res.MetaData_CannotResolveClassInfo, assemblyName, typeName)) {
			this.typeName = typeName;
			this.assemblyName = assemblyName;
		}
		[Obsolete("Use Message instead.", false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Browsable(false)]
		[Description("Gets the name of the type whose XPClassInfo cannot be obtained.")]
		public string TypeName { get { return typeName; } }
		string AssemblyName { get { return assemblyName; } }
	}
#if !NET
	[Serializable]
#endif
	public class XMLDictionaryException : Exception {
#if !NET
		protected XMLDictionaryException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif
		public XMLDictionaryException(string message) : base(Res.GetString(Res.MetaData_XMLLoadError, message)) { }
	}
#if !NET
	[Serializable]
#endif
	[Obsolete("Use AssociationInvalidException instead", true)]
	public class PropertyTypeMismatchException : AssociationInvalidException {
#if !NET
		protected PropertyTypeMismatchException(SerializationInfo info, StreamingContext context)
			: base(info, context) {
		}
#endif
		public PropertyTypeMismatchException(string message)
			: base(message) {
		}
	}
#if !NET
	[Serializable]
#endif
	public class AssociationInvalidException : Exception {
		public AssociationInvalidException(string message) : base(message) { }
#if !NET
		protected AssociationInvalidException(SerializationInfo info, StreamingContext context)
			: base(info, context) {
		}
#endif
	}
#if !NET
	[Serializable]
#endif
	public class PropertyMissingException : Exception {
		string objectType;
		string propertyName;
#if !NET
		protected PropertyMissingException(SerializationInfo info, StreamingContext context)
			: base(info, context) {
		}
#endif
		public PropertyMissingException(string objectType, string propertyName)
			:
			base(Res.GetString(Res.MetaData_PropertyMissing, propertyName, objectType)) {
			this.objectType = objectType;
			this.propertyName = propertyName;
		}
		[Obsolete("Use Message instead.", false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Browsable(false)]
		[Description("Gets the object type that doesn’t contain the DevExpress.Xpo.Exceptions.PropertyMissingException.PropertyName property.")]
		public string ObjectType { get { return objectType; } }
		[Obsolete("Use Message instead.", false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Browsable(false)]
		[Description("")]
		public string PropertyName { get { return propertyName; } }
	}
#if !NET
	[Serializable]
#endif
	public class AssociationElementTypeMissingException : Exception {
		string propertyName;
#if !NET
		protected AssociationElementTypeMissingException(SerializationInfo info, StreamingContext context)
			: base(info, context) {
		}
#endif
		public AssociationElementTypeMissingException(string propertyName)
			: base(Res.GetString(Res.MetaData_AssociationElementTypeMissing, propertyName)) {
			this.propertyName = propertyName;
		}
		[Obsolete("Use Message instead.", false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Browsable(false)]
		[Description("Gets the name of the collection property that caused the current exception.")]
		public string PropertyName { get { return propertyName; } }
	}
#if !NET
	[Serializable]
#endif
	public class RequiredAttributeMissingException : Exception {
		string propertyName;
		string attributeName;
#if !NET
		protected RequiredAttributeMissingException(SerializationInfo info, StreamingContext context)
			: base(info, context) {
		}
#endif
		public RequiredAttributeMissingException(string propertyName, string attributeName)
			:
			base(Res.GetString(Res.MetaData_RequiredAttributeMissing, propertyName, attributeName)) {
			this.propertyName = propertyName;
			this.attributeName = attributeName;
		}
		[Obsolete("Use Message instead.", false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Browsable(false)]
		[Description("Gets the name of the property that is not marked with the specified attribute.")]
		public string PropertyName { get { return propertyName; } }
		[Obsolete("Use Message instead.", false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Browsable(false)]
		[Description("Gets the name of the attribute that is missing in the metadata.")]
		public string AttributeName { get { return attributeName; } }
	}
#if !NET
	[Serializable]
#endif
	public class NonPersistentReferenceFoundException : Exception {
		string objectType;
#if !NET
		protected NonPersistentReferenceFoundException(SerializationInfo info, StreamingContext context)
			: base(info, context) {
		}
#endif
		public NonPersistentReferenceFoundException(string objectType)
			:
			base(Res.GetString(Res.MetaData_NonPersistentReferenceFound, objectType)) {
			this.objectType = objectType;
		}
		[Obsolete("Use Message instead.", false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Browsable(false)]
		[Description("Gets the name of the non-persistent object’s type which you are about to reference.")]
		public string ObjectType { get { return objectType; } }
	}
#if !NET
	[Serializable]
#endif
	public class DifferentObjectsWithSameKeyException : Exception {
#if !NET
		protected DifferentObjectsWithSameKeyException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif
		public DifferentObjectsWithSameKeyException() : base(Res.GetString(Res.Session_DifferentObjectsWithSameKey)) { }
	}
#if !NET
	[Serializable]
#endif
	public class CannotChangePropertyWhenSessionIsConnectedException : Exception {
#if !NET
		protected CannotChangePropertyWhenSessionIsConnectedException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif
		public CannotChangePropertyWhenSessionIsConnectedException(string propertyName) : base(Res.GetString(Res.Session_CannotChangePropertyWhenSessionIsConnected, propertyName)) { }
	}
#if !NET
	[Serializable]
#endif
	public class CannotLoadObjectsException : Exception {
#if !NET
		protected CannotLoadObjectsException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif
		public CannotLoadObjectsException(string objects) : base(Res.GetString(Res.Session_CannotReloadPurgedObject, objects)) { }
	}
	[Obsolete("Use CannotLoadObjectsException instead", true)]
	public class CannotReloadPurgedObjectException: Exception {
	}
#if !NET
	[Serializable]
#endif
	public class TransactionSequenceException : Exception {
#if !NET
		protected TransactionSequenceException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif
		public TransactionSequenceException(string explanation) : base(explanation) { }
	}
#if !NET
	[Serializable]
#endif
	public class SessionCtorAbsentException : Exception {
		string objectType;
#if !NET
		protected SessionCtorAbsentException(SerializationInfo info, StreamingContext context)
			: base(info, context) {
		}
#endif
		public SessionCtorAbsentException(XPClassInfo classInfo)
			:
			base(Res.GetString(Res.MetaData_SessionCtorAbsent, classInfo.FullName)) {
			this.objectType = classInfo.FullName;
		}
		[Obsolete("Use Message instead.", false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Browsable(false)]
		[Description("Gets the name of the currently processed object type.")]
		public string ObjectType { get { return this.objectType; } }
	}
#if !NET
	[Serializable]
#endif
	public class SessionMixingException : Exception {
		Session session;
		object obj;
#if !NET
		protected SessionMixingException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif
		public SessionMixingException(Session session, object obj)
			:
			base(Res.GetString(Res.Session_SessionMixing, obj.GetType().FullName)) {
			this.session = session;
			this.obj = obj;
		}
		[Description("Gets the current Session object.")]
public Session Session { get { return session; } }
		[Description("Gets the persistent object which conflicts with the current session.")]
public object Object { get { return obj; } }
	}
#if !NET
	[Serializable]
#endif
	public class ObjectCacheException : Exception {
		object obj;
#if !NET
		protected ObjectCacheException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif
		public ObjectCacheException(object id, object theObject, object oldObject)
			:
			base(Res.GetString(Res.Session_CannotAddObjectToObjectCache, theObject, id, oldObject)) {
			this.obj = theObject;
		}
		[Description("Gets the object which is associated with the exception.")]
public object Object { get { return obj; } }
	}
#if !NET
	[Serializable]
#endif
	public class KeysAutogenerationNonSupportedTypeException : Exception {
		string typeName;
#if !NET
		protected KeysAutogenerationNonSupportedTypeException(SerializationInfo info, StreamingContext context)
			: base(info, context) {
		}
#endif
		public KeysAutogenerationNonSupportedTypeException(string typeName)
			: base(Res.GetString(Res.ConnectionProvider_KeysAutogenerationNonSupportedTypeException, typeName)) {
			this.typeName = typeName;
		}
		[Obsolete("Use Message instead.", false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Browsable(false)]
		[Description("Gets the string that specifies the object type name.")]
		public string TypeName { get { return typeName; } }
	}
#if !NET
	[Serializable]
#endif
	public class CannotLoadInvalidTypeException : Exception {
		string typeName;
#if !NET
		protected CannotLoadInvalidTypeException(SerializationInfo info, StreamingContext context)
			: base(info, context) {
		}
#endif
		public CannotLoadInvalidTypeException(string typeName)
			: base(Res.GetString(Res.Session_CannotLoadInvalidType, typeName)) {
			this.typeName = typeName;
		}
		[Obsolete("Use Message instead.", false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Browsable(false)]
		[Description("Gets the name of the type that cannot be loaded.")]
		public string TypeName { get { return typeName; } }
	}
#if !NET
	[Serializable]
#endif
	public class SameTableNameException : Exception {
#if !NET
		protected SameTableNameException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif
		public SameTableNameException(XPClassInfo firstClass, XPClassInfo secondClass) : base(Res.GetString(Res.Metadata_SameTableName, firstClass.FullName, secondClass.FullName)) { }
	}
#if !NET
	[Serializable]
#endif
	public class ObjectLayerSecurityException : InvalidOperationException {
		string typeName;
		string propertyName;
		bool isDeletion;
#if !NET
		protected ObjectLayerSecurityException(SerializationInfo info, StreamingContext context)
			: base(info, context) {
		}
#endif
		public ObjectLayerSecurityException()
			:base(Res.GetString(Res.Security_TheCommitOperationWasProhibitedByTheRules)){
		}
		public ObjectLayerSecurityException(string typeName, bool isDeletion)
			: base(Res.GetString(isDeletion ? Res.Security_DeletingAnObjectWasProhibitedByTheRulesX0 : Res.Security_SavingAnObjectWasProhibitedByTheRulesX0, typeName)) {
			this.typeName = typeName;
			this.isDeletion = isDeletion;
		}
		public ObjectLayerSecurityException(string typeName, string propertyName)
			:base(Res.GetString(Res.Security_SavingThePropertyWasProhibitedByTheRulesX0X1, typeName, propertyName)) {
			this.typeName = typeName;
			this.propertyName = propertyName;
		}
		public string TypeName { get { return typeName; } set { typeName = value; } }
		public string PropertyName { get { return propertyName; } set { propertyName = value; } }
		public bool IsDeletion { get { return isDeletion; } set { isDeletion = value; } }
	}
#if !NET
	[Serializable]
#endif
	public class MSSqlLocalDBApiException : InvalidOperationException {
		int? errorCode;
		public int? ErrorCode { get { return errorCode; } set { errorCode = value; } }
		public MSSqlLocalDBApiException() { }
		public MSSqlLocalDBApiException(string message) : base(message) { }
		public MSSqlLocalDBApiException(string message, int errorCode)
			: this(message) {
			this.errorCode = errorCode;
		}
		public MSSqlLocalDBApiException(string message, Exception innerException) : base(message, innerException) { }
#if !NET
		protected MSSqlLocalDBApiException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif
	}
}
