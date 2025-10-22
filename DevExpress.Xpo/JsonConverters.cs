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

using DevExpress.Data.Filtering.Exceptions;
using DevExpress.Utils;
using DevExpress.Xpo.Exceptions;
using DevExpress.Xpo.Metadata;
using DevExpress.Xpo.Metadata.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace DevExpress.Xpo.Helpers {
	public class PersistentBaseConverter<T> : JsonConverter<T> where T : PersistentBase {
		IServiceProvider _serviceProvider;
		public PersistentBaseConverter(IServiceProvider serviceProvider) {
			_serviceProvider = serviceProvider;
		}
		UnitOfWork GetUnitOfWork() {
			return (UnitOfWork)_serviceProvider.GetService(typeof(UnitOfWork));
		}
		public override T Read(
			ref Utf8JsonReader reader,
			Type typeToConvert,
			JsonSerializerOptions options) {
			UnitOfWork uow = GetUnitOfWork();
			if(reader.TokenType != JsonTokenType.StartObject) {
				throw new JsonException();
			}
			XPClassInfo classInfo = uow.GetClassInfo(typeToConvert);
			Dictionary<string, object> resultDict = CollectPropertyValues(ref reader, options, classInfo, uow);
			return (T)PopulateObjectProperties(null, resultDict, uow, classInfo);
		}
		static PersistentBase PopulateObjectProperties(PersistentBase persistentObject, Dictionary<string, object> propertyValues, UnitOfWork uow, XPClassInfo classInfo) {
			object keyValue;
			if(persistentObject == null && propertyValues.TryGetValue(classInfo.KeyProperty.Name, out keyValue)) {
				persistentObject = (PersistentBase)uow.GetObjectByKey(classInfo, keyValue);
			}
			if(persistentObject == null) persistentObject = (PersistentBase)classInfo.CreateNewObject(uow);
			foreach(KeyValuePair<string, object> pair in propertyValues) {
				XPMemberInfo memberInfo = classInfo.FindMember(pair.Key);
				if(memberInfo.IsReadOnly) {
					continue;
				}
				if(memberInfo.ReferenceType != null) {
					PopulateReferenceProperty(persistentObject, uow, pair.Value, memberInfo);
				} else {
					PopulateScalarProperty(persistentObject, pair.Value, memberInfo);
				}
			}
			return persistentObject;
		}
		private static void PopulateScalarProperty(PersistentBase theObject, object theValue, XPMemberInfo memberInfo) {
			if(memberInfo == theObject.ClassInfo.OptimisticLockField) {
				SetOptimisticLockField(theObject, (int)theValue);
			} else {
				memberInfo.SetValue(theObject, theValue);
			}
		}
		private static void PopulateReferenceProperty(PersistentBase parent, UnitOfWork uow, object theValue, XPMemberInfo memberInfo) {
			if(memberInfo.IsAggregated) {
				PersistentBase propertyValue = (PersistentBase)memberInfo.GetValue(parent);
				propertyValue = PopulateObjectProperties(propertyValue, (Dictionary<string, object>)theValue, uow, memberInfo.ReferenceType);
				memberInfo.SetValue(parent, propertyValue);
			} else {
				memberInfo.SetValue(parent, uow.GetObjectByKey(memberInfo.MemberType, theValue));
			}
		}
		static Dictionary<string, object> CollectPropertyValues(ref Utf8JsonReader reader, JsonSerializerOptions options, XPClassInfo classInfo, UnitOfWork uow) {
			if(reader.TokenType != JsonTokenType.StartObject) {
				throw new JsonException(Res.GetString(Res.JsonConverter_UnexpectedToken, reader.TokenType, reader.TokenStartIndex));
			}
			Dictionary<string, object> propertyValues = new Dictionary<string, object>();
			while(reader.Read()) {
				switch(reader.TokenType) {
					case JsonTokenType.EndObject:
						return propertyValues;
					case JsonTokenType.PropertyName:
						ReadPropertyValue(ref reader, options, classInfo, uow, propertyValues);
						break;
				}
			}
			throw new JsonException();
		}
		private static void ReadPropertyValue(ref Utf8JsonReader reader, JsonSerializerOptions options, XPClassInfo classInfo, UnitOfWork uow, Dictionary<string, object> propertyValues) {
			string propertyName = reader.GetString();
			var member = classInfo.FindMember(propertyName);
			if(member != null && CanSerializeProperty(member)) {
				if(member.IsCollection || member.IsNonAssociationList && !member.IsPersistent) {
					reader.Skip();
				} else {
					reader.Read();
					if(member.ReferenceType == null) {
						propertyValues[propertyName] = JsonSerializer.Deserialize(ref reader, member.MemberType, options);
					} else {
						if(member.IsAggregated) {
							propertyValues[propertyName] = CollectPropertyValues(ref reader, options, member.ReferenceType, uow);
						} else {
							propertyValues[propertyName] = JsonSerializer.Deserialize(ref reader, member.ReferenceType.KeyProperty.MemberType, options);
						}
					}
				}
			} else {
				reader.Skip();
			}
		}
		static void SetOptimisticLockField(PersistentBase obj, int newValue) {
			obj.ClassInfo.OptimisticLockField?.SetValue(obj, newValue);
			obj.ClassInfo.OptimisticLockFieldInDataLayer?.SetValue(obj, newValue);
		}
		public override void Write(
			Utf8JsonWriter writer,
			T Value,
			JsonSerializerOptions options) {
			UnitOfWork uow = GetUnitOfWork();
			XPClassInfo classInfo = uow.GetClassInfo(Value);
			writer.WriteStartObject();
			foreach(var member in classInfo.Members) {
				if(member != null && CanSerializeProperty(member) && member.IsPublic && !member.IsCollection) { 
					object value = ((IFieldAccessor)member).GetValue(Value);
					writer.WritePropertyName(member.Name);
					if(!typeof(PersistentBase).IsAssignableFrom(member.MemberType)) {
						JsonSerializer.Serialize(writer, value, member.MemberType, options);
					} else if(member.IsAggregated) {
						JsonSerializer.Serialize(writer, value, options);
					} else {
						if(value != null) {
							value = uow.GetKeyValue(value);
						}
						JsonSerializer.Serialize(writer, value, options);
					}
				}
			}
			writer.WriteEndObject();
		}
		static bool CanSerializeProperty(XPMemberInfo member) {
			return member.Owner.ClassType != typeof(PersistentBase) && member.Owner.ClassType != typeof(XPBaseObject);
		}
		public override bool CanConvert(Type typeToConvert) {
			return typeof(PersistentBase).IsAssignableFrom(typeToConvert);
		}
	}
	public class PersistentBaseConverterFactory : JsonConverterFactory {
		readonly IServiceProvider serviceProvider;
		public PersistentBaseConverterFactory(IServiceProvider serviceProvider) {
			this.serviceProvider = serviceProvider;
		}
		public override bool CanConvert(Type typeToConvert) {
			return typeof(PersistentBase).IsAssignableFrom(typeToConvert);
		}
		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) {
			Type converterType = typeof(PersistentBaseConverter<>).MakeGenericType(typeToConvert);
			return (JsonConverter)Activator.CreateInstance(converterType, serviceProvider);
		}
	}
	public abstract class ChangesSetBase {
		public ChangesSetBase(XPClassInfo classInfo) {
			this.classInfo = classInfo;
		}
		readonly XPClassInfo classInfo;
		readonly IDictionary<string, object> data = new Dictionary<string, object>();
		internal XPClassInfo GetClassInfo() {
			return classInfo;
		}
		public bool TryGetPropertyValue(string propertyName, out object value) {
			Guard.ArgumentIsNotNullOrEmpty(propertyName, nameof(propertyName));
			return data.TryGetValue(propertyName, out value);
		}
		public bool TryGetKeyPropertyValue(out object value) {
			if(TryGetPropertyValue(classInfo.KeyProperty.Name, out value)) {
				return true;
			}
			if(classInfo.KeyProperty.IsPublic) {
				return false;
			}
			foreach(string prop in GetChangedPropertyNames()) {
				XPMemberInfo mi = classInfo.FindMember(prop);
				if(mi == null || !mi.IsAliased) continue;
				var alias = (PersistentAliasAttribute)mi.FindAttributeInfo(typeof(PersistentAliasAttribute));
				if(alias != null && alias.AliasExpression == classInfo.KeyProperty.Name) {
					return TryGetPropertyValue(mi.Name, out value);
				}
			}
			return false;
		}
		public bool TrySetPropertyValue(string propertyName, object value) {
			Guard.ArgumentIsNotNullOrEmpty(propertyName, nameof(propertyName));
			XPMemberInfo mi;
			bool isFound = TryGetMemberInfo(propertyName, out mi);
			if(!isFound) {
				return false;
			}
			data[propertyName] = value;
			return true;
		}
		public bool TryGetPropertyType(string propertyName, out Type type) {
			Guard.ArgumentIsNotNullOrEmpty(propertyName, nameof(propertyName));
			XPMemberInfo mi;
			bool isFound = TryGetMemberInfo(propertyName, out mi);
			if(isFound) {
				type = mi.MemberType;
			} else {
				type = null;
			}
			return isFound;
		}
		public bool TryGetMemberInfo(string propertyName, out XPMemberInfo memberInfo) {
			Guard.ArgumentIsNotNullOrEmpty(propertyName, nameof(propertyName));
			try {
				MemberInfoCollection members = classInfo.ParsePath(propertyName);
				memberInfo = members[members.Count - 1];
				return true;
			} catch(InvalidPropertyPathException) {
				memberInfo = null;
				return false;
			}
		}
		public string[] GetChangedPropertyNames() => data.Keys.ToArray();
		protected void PatchCore(Session session, object original) {
			XPClassInfo classInfo = session.GetClassInfo(this.classInfo.ClassType);
			foreach(var props in data) {
				XPMemberInfo mi = classInfo.GetMember(props.Key);
				if(!mi.IsPublic) {
					throw new InvalidOperationException($"Cannot modify non-public {mi.Name} member.");
				}
				if(mi.IsCollection) {
					var collection = (IList)mi.GetValue(original);
					FillCollection(session, collection, mi, (IEnumerable)props.Value);
				}
				if(mi.IsReadOnly) continue;
				if(mi.IsKey && !session.IsNewObject(original)) {
					object value = mi.GetValue(original);
					if(!object.Equals(value, props.Value)) {
						throw new InvalidOperationException();
					}
					continue;
				}
				if(mi.ReferenceType == null) {
					mi.SetValue(original, props.Value);
					continue;
				}
				var nested = props.Value as ChangesSetBase;
				if(nested == null) {
					mi.SetValue(original, session.GetObjectByKey(mi.ReferenceType, props.Value));
					continue;
				}
				object key;
				if(nested.TryGetKeyPropertyValue(out key)) {
					object refObj = session.GetObjectByKey(mi.ReferenceType, key);
					if(refObj != null) {
						nested.PatchCore(session, refObj);
					}
					mi.SetValue(original, refObj);
				} else {
					object refObj = mi.GetValue(original);
					if(refObj != null) {
						nested.PatchCore(session, refObj);
					} else {
						throw new InvalidOperationException($"Cannot update the associated object. The {mi.Name} reference property is null. To update an object and assign it to the {mi.Name} property, pass the object's identifier.");
					}
				}
			}
		}
		protected void PutCore(Session session, object original) {
			XPClassInfo classInfo = session.GetClassInfo(this.classInfo.ClassType);
			foreach(XPMemberInfo mi in classInfo.PersistentProperties) {
				if(mi.IsReadOnly || mi is ServiceField) continue;
				object value;
				if(!mi.IsPublic) {
					if(TryGetPropertyValue(mi.Name, out value)) {
						throw new InvalidOperationException($"Cannot modify non-public {mi.Name} member.");
					} else {
						continue;
					}
				}
				if(!TryGetPropertyValue(mi.Name, out value)) {
					if(!mi.IsKey) {
						mi.SetValue(original, GetDefaultValue(mi.MemberType));
					}
					continue;
				}
				if(mi.IsKey && !session.IsNewObject(original)) {
					if(!object.Equals(mi.GetValue(original), value)) {
						throw new InvalidOperationException();
					}
					continue;
				}
				if(mi.ReferenceType == null) {
					mi.SetValue(original, value);
					continue;
				}
				var nested = value as ChangesSetBase;
				if(nested == null) {
					value = session.GetObjectByKey(mi.ReferenceType, value);
					mi.SetValue(original, value);
					continue;
				}
				object key;
				if(!nested.TryGetKeyPropertyValue(out key)) {
					object refObj = mi.GetValue(original);
					if(refObj != null) {
						nested.PutCore(session, refObj);
					} else {
						throw new InvalidOperationException($"Cannot update the associated object. The {mi.Name} reference property is null. To update an object and assign it to the {mi.Name} property, pass the object's identifier.");
					}
				} else {
					object refObj = session.GetObjectByKey(mi.ReferenceType, key);
					if(refObj != null && nested.GetChangedPropertyNames().Length > 1) {
						nested.PatchCore(session, refObj);
					}
					mi.SetValue(original, refObj);
				}
			}
			foreach(XPMemberInfo mi in classInfo.CollectionProperties) {
				object value;
				if(TryGetPropertyValue(mi.Name, out value)) {
					IList collection = (IList)mi.GetValue(original);
					while(collection.Count > 0) {
						collection.Remove(collection[0]);
					}
					FillCollection(session, collection, mi, (IEnumerable)value);
				}
			}
		}
		private static void FillCollection(Session session, IList collection, XPMemberInfo mi, IEnumerable value) {
			foreach(ChangesSetBase collectionItemChangesSet in value) {
				object collectionItemKey;
				if(collectionItemChangesSet.TryGetKeyPropertyValue(out collectionItemKey)) {
					object collectionItemToAdd = session.GetObjectByKey(mi.CollectionElementType, collectionItemKey);
					collectionItemChangesSet.PatchCore(session, collectionItemToAdd);
					collection.Add(collectionItemToAdd);
				} else {
					throw new InvalidOperationException($"Cannot find the identifier for one or more objects in the {mi.Name} collection. Specify key values for all objects to update.");
				}
			}
		}
		static object GetDefaultValue(Type valueType) {
			if(valueType.IsValueType) {
				return Activator.CreateInstance(valueType);
			} else {
				return null;
			}
		}
	}
	public class ChangesSet<TModel> : ChangesSetBase {
		public ChangesSet(XPDictionary dictionary) : base(dictionary.GetClassInfo(typeof(TModel))) { }
		public void Patch(Session session, TModel original) => PatchCore(session, original);
		public void Put(Session session, TModel original) => PutCore(session, original);
	}
	public class ChangesSetJsonConverter<TImpl> : JsonConverter<TImpl> where TImpl : ChangesSetBase {
		const string SpecialPropertyODataType = "@odata.type";
		readonly XPDictionary dictionary;
		public ChangesSetJsonConverter(XPDictionary dictionary) {
			if(dictionary == null) {
				this.dictionary = new ReflectionDictionary();
				this.dictionary.GetDataStoreSchema(GetModelType(typeof(TImpl)));
			} else {
				this.dictionary = dictionary;
			}
		}
		static Type GetModelType(Type changeSetType) {
			if(!changeSetType.IsGenericType) return null;
			return changeSetType.GetGenericArguments()[0];
		}
		protected virtual void ValidateMemberInfo(XPMemberInfo xpMemberInfo)
		{
		}
		public override TImpl Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			if(reader.TokenType != JsonTokenType.StartObject) {
				throw new JsonException();
			}
			TImpl changesSet = CreateChangesSet();
			while(reader.Read()) {
				if(reader.TokenType == JsonTokenType.EndObject) {
					return changesSet;
				}
				if(reader.TokenType != JsonTokenType.PropertyName) {
					throw new JsonException();
				}
				string propertyName = reader.GetString();
				if(propertyName == SpecialPropertyODataType) {
					reader.Read();
					string value = reader.GetString();
					XPClassInfo ci;
					string className = ((string)value).Substring(1);
					if(!MemberInfoCollection.TryResolveTypeAlsoByShortName(className, changesSet.GetClassInfo(), out ci)) {
						throw new CannotResolveClassInfoException(null, className);
					}
					if(ci != changesSet.GetClassInfo()) {
						string msg = Res.GetString(CultureInfo.CurrentUICulture, Res.JsonConverter_ODataType_Mismatch, value);
						throw new NotSupportedException(msg);
					}
					continue;
				}
				if(propertyName.StartsWith('@')) {
					reader.Read();
					continue;
				}
				XPMemberInfo mi;
				if(!changesSet.TryGetMemberInfo(propertyName, out mi)) {
					string msg = Res.GetString(CultureInfo.CurrentUICulture, Res.JsonConverter_CouldNotResolvePropertyType, propertyName);
					throw new NotSupportedException(msg);
				}
				ValidateMemberInfo(mi);
				reader.Read();
				if(mi.IsCollection && reader.TokenType == JsonTokenType.StartArray) {
					var collectionElementChangesSets = new ArrayList();
					while(reader.TokenType != JsonTokenType.EndArray) {
						reader.Read();
						if(reader.TokenType == JsonTokenType.StartObject) {
							Type collectionElementChangesSetType = typeof(ChangesSet<>).MakeGenericType(mi.CollectionElementType.ClassType);
							var collectionElementChangesSet = (ChangesSetBase)JsonSerializer.Deserialize(ref reader, collectionElementChangesSetType, options);
							collectionElementChangesSets.Add(collectionElementChangesSet);
						}
					}
					changesSet.TrySetPropertyValue(mi.Name, collectionElementChangesSets);
				} else if(mi.ReferenceType == null || reader.TokenType != JsonTokenType.StartObject) {
					Type returnType = mi.ReferenceType == null ? mi.MemberType : mi.ReferenceType.KeyProperty.MemberType;
					object value = (reader.TokenType == JsonTokenType.Null) ? null : JsonSerializer.Deserialize(ref reader, returnType, options);
					if(!changesSet.TrySetPropertyValue(propertyName, value)) {
						string msg = Res.GetString(CultureInfo.CurrentUICulture, Res.JsonConverter_CouldNotAssignPropertyValue, propertyName);
						throw new NotSupportedException(msg);
					}
				} else {
					Type nestedChangesSetType = typeof(ChangesSet<>).MakeGenericType(mi.ReferenceType.ClassType);
					var nestedChangesSet = (ChangesSetBase)JsonSerializer.Deserialize(ref reader, nestedChangesSetType, options);
					changesSet.TrySetPropertyValue(mi.Name, nestedChangesSet);
				}
			}
			throw new JsonException();
		}
		public override void Write(Utf8JsonWriter writer, TImpl value, JsonSerializerOptions options) {
			throw new NotImplementedException();
		}
		TImpl CreateChangesSet() {
			foreach(ConstructorInfo ci in typeof(TImpl).GetConstructors()) {
				ParameterInfo[] parameters = ci.GetParameters();
				if(parameters.Length == 0) {
					return Activator.CreateInstance<TImpl>();
				} else if(parameters.Length == 1 && parameters[0].ParameterType == typeof(XPDictionary)) {
					return (TImpl)Activator.CreateInstance(typeof(TImpl), dictionary);
				}
			}
			string msg = Res.GetString(CultureInfo.CurrentUICulture, Res.JsonConverter_CouldNotFindAppropriateConstructor, typeof(TImpl).FullName);
			throw new NotSupportedException(msg);
		}
	}
	public class ChangesSetJsonConverterFactory : JsonConverterFactory {
		readonly IServiceProvider serviceProvider;
		public ChangesSetJsonConverterFactory(IServiceProvider serviceProvider) {
			this.serviceProvider = serviceProvider;
		}
		public override bool CanConvert(Type typeToConvert) {
			return typeof(ChangesSetBase).IsAssignableFrom(typeToConvert);
		}
		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) {
			XPDictionary dictionary = null;
			if(serviceProvider != null) {
				UnitOfWork uow = (UnitOfWork)serviceProvider.GetService(typeof(UnitOfWork));
				dictionary = uow.Dictionary;
			}
			Type converterType = typeof(ChangesSetJsonConverter<>).MakeGenericType(typeToConvert);
			return (JsonConverter)Activator.CreateInstance(converterType, dictionary);
		}
	}
}
