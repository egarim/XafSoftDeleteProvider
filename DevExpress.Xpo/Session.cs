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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevExpress.Data.Filtering;
using DevExpress.Data.Filtering.Helpers;
using DevExpress.Xpo.Exceptions;
using DevExpress.Xpo.Metadata;
using DevExpress.Xpo.Metadata.Helpers;
namespace DevExpress.Xpo.Helpers {
	using System.Collections;
	using System.ComponentModel;
	using System.Threading;
	using DevExpress.Data.Helpers;
	using DevExpress.Xpo.DB;
	using DevExpress.Xpo.DB.Exceptions;
	using DevExpress.Xpo.Generators;
	public interface ISessionProvider : IObjectLayerProvider, IDataLayerProvider {
		Session Session {
			get;
		}
	}
	[ToolboxItem(false)]
	public sealed class DefaultSession : Session {
		public DefaultSession(ISite site)
			: base() {
			this.Site = site;
		}
		public override XPDictionary Dictionary {
			get {
				if(dict == null) {
					dict = CreateDesignTimeDictionary(Site);
				}
				return dict;
			}
		}
	}
	public sealed class SessionIdentityMap {
		ObjectIdentityMap loadedObjects;
		public SessionIdentityMap(IObjectLayerProvider objectLayerProvider, IdentityMapBehavior cacheBehavior) {
			if(cacheBehavior == IdentityMapBehavior.Weak)
				loadedObjects = new WeakObjectIdentityMap(objectLayerProvider);
			else
				loadedObjects = new StrongObjectIdentityMap(objectLayerProvider);
		}
		public void Clear() {
			loadedObjects.Clear();
		}
		public void Compact() {
			loadedObjects.Compact();
		}
		IObjectMap GetMap(XPClassInfo ci) {
			return loadedObjects.GetObjects(ci);
		}
		object GetLoadedObjectByKey(XPClassInfo ci, object key) {
			return loadedObjects.Get(ci, key);
		}
		void RegisterObject(object theObject, object key) {
			loadedObjects.Add(theObject, key);
		}
		void UnregisterObject(object theObject) {
			loadedObjects.Remove(theObject);
		}
		public static SessionIdentityMap Extract(Session session) {
			if(session._IdentityMap == null)
				session._IdentityMap = new SessionIdentityMap(session, session.GetIdentityMapBehavior());
			return session._IdentityMap;
		}
		public static IObjectMap GetMap(Session session, XPClassInfo ci) {
			return Extract(session).GetMap(ci);
		}
		public static void RegisterObject(Session session, object theObject, object key) {
			Extract(session).RegisterObject(theObject, key);
		}
		public static void UnregisterObject(Session session, object theObject) {
			Extract(session).UnregisterObject(theObject);
		}
		public static object GetLoadedObjectByKey(Session session, XPClassInfo ci, object key) {
			return Extract(session).GetLoadedObjectByKey(ci, key);
		}
	}
	[ToolboxItem(false)]
	sealed class StrongSession : Session {
		public StrongSession(IObjectLayer layer)
			: base(layer) {
			IdentityMapBehavior = IdentityMapBehavior.Strong;
		}
	}
	public interface IWideDataStorage {
		bool WideDataContainsKey(object key);
		void SetWideDataItem(object key, object value);
		object GetWideDataItem(object key);
		bool TryGetWideDataItem(object key, out object value);
	}
	public sealed class XPObjectTypesManager {
		IObjectLayer managedLayer;
		Dictionary<XPClassInfo, XPObjectType> loadedTypes;
		IObjectMap objects;
		public XPObjectTypesManager(Session session)
			: this(session.ObjectLayer) {
		}
		public XPObjectTypesManager(IObjectLayer layer) {
			this.managedLayer = layer;
		}
		IObjectLayerAsync GetObjectLayerAsync() {
			var objLayerAsync = managedLayer as IObjectLayerAsync;
			if(objLayerAsync == null) {
				throw new NotSupportedException(Res.GetString(Res.Async_ObjectLayerDoesNotImplementIObjectLayerAsync, managedLayer.GetType().FullName));
			}
			return objLayerAsync;
		}
		Dictionary<XPClassInfo, XPObjectType> GetAllTypes() {
			if(loadedTypes == null) {
				XPClassInfo objectType = managedLayer.Dictionary.GetClassInfo(typeof(XPObjectType));
				loadedTypes = managedLayer.GetObjectLayerWideObjectTypes();
				if(loadedTypes == null) {
					using(var lockObj = managedLayer.Dictionary.LockObject.Lock()) {
						loadedTypes = managedLayer.GetObjectLayerWideObjectTypes();
						if(loadedTypes == null) {
							managedLayer.RegisterStaticTypes(objectType);
							loadedTypes = new Dictionary<XPClassInfo, XPObjectType>();
							XPObjectType type = new XPObjectType(managedLayer.Dictionary, objectType.AssemblyName, objectType.FullName);
							loadedTypes[type.TypeClassInfo] = type;
							managedLayer.SetObjectLayerWideObjectTypes(loadedTypes);
							try {
								using(Session session = new StrongSession(managedLayer)) {
									ICollection list = session.GetObjects(objectType, null, null, 0, true, false);
									FillLoadedTypes(list);
								}
							}
							catch(SchemaCorrectionNeededException) {
							}
						}
					}
				}
				objects = managedLayer.GetStaticCache(objectType);
			}
			return loadedTypes;
		}
		async Task<Dictionary<XPClassInfo, XPObjectType>> GetAllTypesAsync(AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken) {
			if(loadedTypes == null) {
				XPClassInfo objectType = managedLayer.Dictionary.GetClassInfo(typeof(XPObjectType));
				loadedTypes = managedLayer.GetObjectLayerWideObjectTypes();
				if(loadedTypes == null) {
					using(var lockObj = await managedLayer.Dictionary.LockObject.LockAsync(asyncOperationId)) {
						loadedTypes = managedLayer.GetObjectLayerWideObjectTypes();
						if(loadedTypes == null) {
							managedLayer.RegisterStaticTypes(objectType);
							loadedTypes = new Dictionary<XPClassInfo, XPObjectType>();
							XPObjectType type = new XPObjectType(managedLayer.Dictionary, objectType.AssemblyName, objectType.FullName);
							loadedTypes[type.TypeClassInfo] = type;
							managedLayer.SetObjectLayerWideObjectTypes(loadedTypes);
							try {
								using(Session session = new StrongSession(managedLayer)) {
									ICollection list = await session.GetObjectsAsync(objectType, null, null, 0, true, false, cancellationToken);
									FillLoadedTypes(list);
								}
							}
							catch(SchemaCorrectionNeededException) {
							}
						}
					}
				}
				objects = managedLayer.GetStaticCache(objectType);
			}
			return loadedTypes;
		}
		void FillLoadedTypes(ICollection objectTypesList) {
			List<object> suspectObjectTypes = ListHelper.FromCollection(objectTypesList);
			List<XPClassInfo> justProcessedClassInfos = new List<XPClassInfo>(objectTypesList.Count);
			for(; ; ) {
				bool touched = false;
				for(int i = suspectObjectTypes.Count - 1; i >= 0; --i) {
					XPObjectType type = (XPObjectType)suspectObjectTypes[i];
					if(type.IsValidType) {
						loadedTypes.Add(type.TypeClassInfo, type);
						justProcessedClassInfos.Add(type.TypeClassInfo);
						suspectObjectTypes.RemoveAt(i);
						touched = true;
					}
				}
				if(!touched)
					break;
				Dictionary<XPClassInfo, XPClassInfo> touchedHolder = new Dictionary<XPClassInfo, XPClassInfo>();
				foreach(XPClassInfo ci in justProcessedClassInfos)
					ci.TouchRecursive(touchedHolder);
				justProcessedClassInfos.Clear();
			}
		}
		public void EnsureIsTypedObjectValid() {
			GetAllTypes();
		}
		public Task EnsureIsTypedObjectValidAsync(CancellationToken cancellationToken) {
			return GetAllTypesAsync(AsyncOperationIdentifier.New(), cancellationToken);
		}
		public Dictionary<XPClassInfo, XPObjectType> AllTypes {
			get {
				return GetAllTypes();
			}
		}
		public XPObjectType GetObjectType(Int32 id) {
			GetAllTypes();
			XPObjectType type = (XPObjectType)objects.Get(id);
			if(type != null)
				return type;
			using(var lockObj = managedLayer.Dictionary.LockObject.Lock()) {
				using(Session session = new StrongSession(managedLayer))
					type = session.GetObjectByKey<XPObjectType>(id);
				if(type != null && type.IsValidType) {
					loadedTypes.Add(type.TypeClassInfo, type);
					return type;
				}
				throw new TypeNotFoundException(id);
			}
		}
		public async Task<XPObjectType> GetObjectTypeAsync(Int32 id, AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken) {
			await GetAllTypesAsync(asyncOperationId, cancellationToken);
			XPObjectType type = (XPObjectType)objects.Get(id);
			if(type != null)
				return type;
			using(var lockObj = await managedLayer.Dictionary.LockObject.LockAsync(asyncOperationId)) {
				using(Session session = new StrongSession(managedLayer))
					type = await session.GetObjectByKeyAsync<XPObjectType>(id, cancellationToken);
				if(type != null && type.IsValidType) {
					loadedTypes.Add(type.TypeClassInfo, type);
					return type;
				}
				throw new TypeNotFoundException(id);
			}
		}
		public XPObjectType GetObjectType(XPClassInfo objectType) {
			XPObjectType type;
			if(!GetAllTypes().TryGetValue(objectType, out type)) {
				using(var lockObj = managedLayer.Dictionary.LockObject.Lock()) {
					using(Session session = new StrongSession(managedLayer))
						type = session.FindObject<XPObjectType>(XPObjectType.Fields.TypeName == objectType.FullName);
					if(type == null) {
						objectType.GetRefTypes();	
						type = new XPObjectType(managedLayer.Dictionary, objectType.AssemblyName, objectType.FullName);
						managedLayer.CreateObjectType(type);
					}
					if(!GetAllTypes().ContainsKey(objectType))
						loadedTypes.Add(objectType, type);
				}
			}
			return type;
		}
		public async Task<XPObjectType> GetObjectTypeAsync(XPClassInfo objectType, AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken) {
			XPObjectType type;
			var allTypes = await GetAllTypesAsync(asyncOperationId, cancellationToken);
			if(!allTypes.TryGetValue(objectType, out type)) {
				using(var lockObj = await managedLayer.Dictionary.LockObject.LockAsync(asyncOperationId)) {
					using(Session session = new StrongSession(managedLayer))
						type = await session.FindObjectAsync<XPObjectType>(XPObjectType.Fields.TypeName == objectType.FullName, cancellationToken);
					if(type == null) {
						objectType.GetRefTypes();	
						type = new XPObjectType(managedLayer.Dictionary, objectType.AssemblyName, objectType.FullName);
						await GetObjectLayerAsync().CreateObjectTypeAsync(type, cancellationToken);
					}
					allTypes = await GetAllTypesAsync(asyncOperationId, cancellationToken);
					if(!allTypes.ContainsKey(objectType))
						loadedTypes.Add(objectType, type);
				}
			}
			return type;
		}
		public XPObjectType TryGetObjectType(XPClassInfo objectType) {
			XPObjectType type;
			if(!GetAllTypes().TryGetValue(objectType, out type)) {
				return null;
			}
			return type;
		}
	}
	class LockingHelper {
		public static CriteriaOperator GetLockingCriteria(int? dbVersion, XPMemberInfo optimisticLock) {
			if(optimisticLock == null)
				return null;
			OperandProperty prop = new OperandProperty(optimisticLock.Name);
			if(dbVersion.HasValue)
				return prop == dbVersion.Value;
			else
				return prop.IsNull();
		}
		static bool IsPropertyToCheckLocking(OptimisticLockingBehavior option, XPMemberInfo mi, bool isModified) {
			if(mi.IsKey || (mi.IsDelayed && mi.ReferenceType == null))
				return false;
			if(mi.IsOptimisticLockingIgnored)
				return false;
			if(option == OptimisticLockingBehavior.LockModified && !isModified)
				return false;
			return true;
		}
		public static ObjectGeneratorCriteriaSet GetLockingCriteria(XPClassInfo ci, IEnumerable properties, object theObject, OptimisticLockingBehavior option) {
			return GetLockingCriteria(ci, properties, theObject, option, null);
		}
		public static ObjectGeneratorCriteriaSet GetLockingCriteria(XPClassInfo ci, IEnumerable properties, object theObject, OptimisticLockingBehavior option, IList exclude) {
			if(option == OptimisticLockingBehavior.NoLocking || option == OptimisticLockingBehavior.ConsiderOptimisticLockingField) return null;
			ObjectGeneratorCriteriaSet result = new ObjectGeneratorCriteriaSet();
			XPMemberInfo gc = ci.FindMember(GCRecordField.StaticName);
			bool isGCDeleted = gc != null && gc.GetValue(theObject) != null;
			foreach(XPMemberInfo mi in properties) {
				if(exclude != null && exclude.Contains(mi))
					continue;
				bool isModified = mi.GetModified(theObject);
				if(!IsPropertyToCheckLocking(option, mi, isModified || isGCDeleted))
					continue;
				string tableName = string.IsNullOrEmpty(mi.Owner.TableName) ? ci.IdClass.TableName : mi.Owner.TableName;
				if(mi.SubMembers.Count > 0) {
					foreach(XPMemberInfo subMember in mi.SubMembers) {
						if(!subMember.IsPersistent) {
							continue;
						}
						AddLockingPropertyCriteria(theObject, result, isModified, tableName, (smi, o) => smi.GetConst(mi.GetOldValue(o), mi), subMember);
					}
				}
				else {
					AddLockingPropertyCriteria(theObject, result, isModified, tableName, (m, o) => m.GetOldValue(o), mi);
				}
			}
			return result;
		}
		static void AddLockingPropertyCriteria(object theObject, ObjectGeneratorCriteriaSet result, bool isModified, string tableName, Func<XPMemberInfo, object, object> getOldValue, XPMemberInfo memberInfo) {
			OperandProperty property = new OperandProperty(memberInfo.Name);
			object value = null;
			if(isModified) {
				value = getOldValue(memberInfo, theObject);
			}
			else {
				if(memberInfo.IsDelayed) {
					XPDelayedProperty delayedProperty = XPDelayedProperty.GetDelayedPropertyContainer(theObject, memberInfo);
					if(delayedProperty.IsLoaded) {
						value = delayedProperty.Value;
					}
					else {
						property = new OperandProperty(string.Concat(memberInfo.Name, ".", memberInfo.ReferenceType.KeyProperty.Name));
						value = delayedProperty.InternalValue;
					}
				}
				else {
					value = memberInfo.GetValue(theObject);
				}
			}
			result.UpdateCriteria(tableName, (value == null) ? property.IsNull() : (CriteriaOperator)(property == new OperandValue(value)));
		}
		public static object GetNestedValue(XPClassInfoStub classInfoStub, XPObjectStub obj, int memberIndex, out bool isModified) {
			object value = obj.Data[memberIndex];
			XPClassInfoStubOldValueContainer oldValue = obj.OldData == null ? null : obj.OldData[memberIndex];
			if(oldValue == null) {
				isModified = false;
				return value;
			}
			isModified = true;
			return oldValue.Data;
		}
		static object GetNestedValue(object theObject, XPMemberInfo mi, bool isModified) {
			object value = null;
			if(isModified) {
				value = mi.GetOldValue(theObject);
			}
			else {
				if(mi.IsDelayed) {
					XPDelayedProperty delayedProperty = XPDelayedProperty.GetDelayedPropertyContainer(theObject, mi);
					value = delayedProperty.IsLoaded ? delayedProperty.Value : delayedProperty.InternalValue;
				}
				else
					value = mi.GetValue(theObject);
			}
			return value;
		}
		static object GetParentValue(object parentObject, XPMemberInfo mi) {
			object parentValue = null;
			if(mi.GetModified(parentObject) || !mi.IsDelayed) {
				parentValue = mi.GetValue(parentObject);
			}
			else {
				XPDelayedProperty delayedProperty = XPDelayedProperty.GetDelayedPropertyContainer(parentObject, mi);
				parentValue = delayedProperty.IsLoaded ? delayedProperty.Value : delayedProperty.InternalValue;
			}
			return parentValue;
		}
		public static bool HasModified(XPClassInfoStub classInfoStub, XPObjectStub obj, IEnumerable properties, object parentObject, OptimisticLockingBehavior option) {
			if(option == OptimisticLockingBehavior.NoLocking || option == OptimisticLockingBehavior.ConsiderOptimisticLockingField) return false;
			int gcMemberIndex = classInfoStub.GetMemberIndex(GCRecordField.StaticName, false);
			bool isGCDeleted = gcMemberIndex >= 0 && obj.Data[gcMemberIndex] != null;
			foreach(XPMemberInfo mi in properties) {
				int memberIndex = classInfoStub.GetMemberIndex(mi.Name, false);
				if(memberIndex < 0 || !obj.Changed[memberIndex])
					continue;
				bool isModified;
				object value = GetNestedValue(classInfoStub, obj, memberIndex, out isModified);
				if(!IsPropertyToCheckLocking(option, mi, isModified || isGCDeleted))
					continue;
				if(mi.ReferenceType != null && value is XPObjectStub)
					value = ((XPObjectStub)value).Key;
				object parentValue = GetParentValue(parentObject, mi);
				if(IsModifiedInDataLayer(mi, value, parentValue))
					return true;
			}
			return false;
		}
		public static bool HasModified(XPClassInfo ci, IEnumerable properties, object theObject, object parentObject, OptimisticLockingBehavior option) {
			if(option == OptimisticLockingBehavior.NoLocking || option == OptimisticLockingBehavior.ConsiderOptimisticLockingField) return false;
			XPMemberInfo gc = ci.FindMember(GCRecordField.StaticName);
			bool isGCDeleted = gc != null && gc.GetValue(theObject) != null;
			foreach(XPMemberInfo mi in properties) {
				bool isModified = mi.GetModified(theObject);
				if(!IsPropertyToCheckLocking(option, mi, isModified || isGCDeleted))
					continue;
				object value = GetNestedValue(theObject, mi, isModified);
				object parentValue = GetParentValue(parentObject, mi);
				if(IsModifiedInDataLayer(mi, value, parentValue))
					return true;
			}
			return false;
		}
		static bool BytesAreEquals(byte[] oldValue, byte[] newValue) {
			if(oldValue == newValue) return true;
			if(oldValue.Length != newValue.Length) return false;
			int length = oldValue.Length;
			for(int i = 0; i < length; i++) {
				if(oldValue[i] != newValue[i]) return false;
			}
			return true;
		}
		public static bool IsModifiedInDataLayer(XPMemberInfo member, object nestedValue, object parentValue) {
			if(ReferenceEquals(nestedValue, parentValue))
				return false;
			else if(nestedValue is ValueType && parentValue is ValueType)
				return !Equals(nestedValue, parentValue);
			else if(nestedValue is string && parentValue is string)
				return !Equals(nestedValue, parentValue);
			else if(nestedValue is byte[] && parentValue is byte[])
				return !BytesAreEquals((byte[])nestedValue, (byte[])parentValue);
			else {
				if(nestedValue != null && parentValue != null) {
					if(member.ReferenceType != null) {
						Type nestedType = nestedValue.GetType();
						Type parentType = parentValue.GetType();
						if(!nestedType.IsValueType && !nestedType.IsEnum && !nestedType.IsPrimitive && !nestedType.IsGenericType)
							nestedValue = member.ReferenceType.KeyProperty.GetValue(nestedValue);
						if(!parentType.IsValueType && !parentType.IsEnum && !parentType.IsPrimitive && !parentType.IsGenericType)
							parentValue = member.ReferenceType.KeyProperty.GetValue(parentValue);
						if(object.Equals(nestedValue, parentValue)) {
							return false;
						}
					}
					if(member.IsStruct && member.SubMembers.Count > 0) {
						IdList nestedList = nestedValue as IdList;
						IdList parentList = parentValue as IdList;
						if(nestedList == null) {
							nestedList = member.ExpandId(nestedValue) as IdList;
						}
						if(parentValue == null) {
							parentList = member.ExpandId(parentValue) as IdList;
						}
						if(nestedList.Count != parentList.Count) {
							return true;
						}
						int index = 0;
						foreach(XPMemberInfo subMember in member.SubMembers) {
							if(!subMember.IsPersistent) {
								continue;
							}
							if(IsModifiedInDataLayer(subMember, nestedList[index], parentList[index])) {
								return true;
							}
							index++;
						}
						return false;
					}
				}
			}
			return true;
		}
	}
	class ProcessingSave {
		List<object> processedObjectsList;
		Session session;
		public ProcessingSave(Session session, BatchWideDataHolder4Modification batchWideData) {
			this.session = session;
			this.BatchWideData = batchWideData;
			processedObjectsList = ListHelper.FromCollection(SaveOrderer.PrepareSaveOrder(session));
		}
		Session Session {
			get { return session; }
		}
		List<object> insertList = new List<object>();
		readonly BatchWideDataHolder4Modification BatchWideData;
		class InsertUpdate {
			public readonly object TheObject;
			public readonly MemberInfoCollection Updatable;
			public InsertUpdate(object theObject, MemberInfoCollection updatable) {
				TheObject = theObject;
				Updatable = updatable;
			}
		}
		void InternalInsertObject(List<ModificationStatement> insertList, List<InsertUpdate> updateList, object theObject) {
			XPClassInfo classInfo = Session.GetClassInfo(theObject);
			XPMemberInfo optimisticLock = classInfo.OptimisticLockField;
			if(optimisticLock != null && optimisticLock.GetValue(theObject) == null)
				optimisticLock.SetValue(theObject, 0);
			XPMemberInfo key = classInfo.KeyProperty;
			MemberInfoCollection insertable = new MemberInfoCollection(classInfo);
			MemberInfoCollection updatable = new MemberInfoCollection(classInfo);
			foreach(XPMemberInfo updateMember in Session.GetPropertiesListForUpdateInsert(theObject, false, false)) {
				if(key.MappingField == updateMember.MappingField && key != updateMember)
					continue;
				if(updateMember.ReferenceType != null) {
					object referredObject = updateMember.GetValue(theObject);
					if(referredObject != null && Session.IsNewObject(referredObject) && !BatchWideData.IsObjectAlreadyInserted(referredObject))
						updatable.Add(updateMember);
					else
						insertable.Add(updateMember);
				}
				else
					insertable.Add(updateMember);
			}
			if(key.IsAutoGenerate) {
				if(key.MemberType == typeof(Guid)) {
					object keyValue = key.GetValue(theObject);
					if(keyValue == null || (Guid)keyValue == Guid.Empty)
						key.SetValue(theObject, XpoDefault.NewGuid());
				}
				else if(!key.IsIdentity)
					throw new KeysAutogenerationNonSupportedTypeException(classInfo.FullName);
			}
			else {
				Session.CheckDuplicateObjectInIdentityMap(theObject);
			}
			BatchWideData.RegisterInsertedObject(theObject);
			List<ModificationStatement> queries = InsertQueryGenerator.GenerateInsert(Session.Dictionary, BatchWideData, theObject, insertable);
			InsertStatement rootQuery = (InsertStatement)queries[0];
			if(!ReferenceEquals(rootQuery.IdentityParameter, null))
				this.insertList.Add(theObject);
			insertList.AddRange(queries);
			if(updatable.Count > 0)
				updateList.Add(new InsertUpdate(theObject, updatable));
		}
		void InternalUpdateObject(List<ModificationStatement> updateList, object theObject) {
			Session.CheckDuplicateObjectInIdentityMap(theObject);
			XPClassInfo ci = Session.GetClassInfo(theObject);
			XPMemberInfo optimisticLock = ci.OptimisticLockField;
			MemberInfoCollection propsList = Session.GetPropertiesListForUpdateInsert(theObject, true, false);
			if(Session.LockingOption == LockingOption.Optimistic) {
				switch(ci.OptimisticLockingBehavior) {
					case OptimisticLockingBehavior.ConsiderOptimisticLockingField:
						if(optimisticLock != null) {
							updateList.AddRange(UpdateQueryGenerator.
								GenerateUpdate(Session.Dictionary, BatchWideData, theObject, propsList, ObjectGeneratorCriteriaSet.GetCriteriaSet(ci.IdClass.TableName, LockingHelper.GetLockingCriteria((int?)ci.OptimisticLockFieldInDataLayer.GetValue(theObject), optimisticLock))));
							return;
						}
						break;
					case OptimisticLockingBehavior.LockAll:
					case OptimisticLockingBehavior.LockModified:
						updateList.AddRange(UpdateQueryGenerator.
							GenerateUpdate(Session.Dictionary, BatchWideData, theObject, propsList, LockingHelper.GetLockingCriteria(ci, ci.PersistentProperties, theObject, ci.OptimisticLockingBehavior)));
						return;
				}
			}
			updateList.AddRange(UpdateQueryGenerator.GenerateUpdate(Session.Dictionary, BatchWideData, theObject, propsList));
		}
		public List<ModificationStatement> Process() {
			List<XPClassInfo> usedClasses = ProcessBegin();
			Session.UpdateSchema(usedClasses.ToArray());
			List<ModificationStatement> insertList = ProcessEnd();
			return insertList;
		}
		public async Task<List<ModificationStatement>> ProcessAsync(CancellationToken cancellationToken) {
			List<XPClassInfo> usedClasses = ProcessBegin();
			await Session.UpdateSchemaAsync(cancellationToken, usedClasses.ToArray());
			List<ModificationStatement> insertList = ProcessEnd();
			return insertList;
		}
		List<XPClassInfo> ProcessBegin() {
			Dictionary<XPClassInfo, object> classes = new Dictionary<XPClassInfo, object>();
			List<XPClassInfo> usedClasses = new List<XPClassInfo>();
			foreach(object theObject in processedObjectsList) {
				XPClassInfo ci = Session.GetClassInfo(theObject);
				if(!classes.ContainsKey(ci)) {
					classes.Add(ci, null);
					usedClasses.Add(ci);
				}
			}
			return usedClasses;
		}
		List<ModificationStatement> ProcessEnd() {
			List<ModificationStatement> insertList = new List<ModificationStatement>();
			List<InsertUpdate> updateList = new List<InsertUpdate>();
			List<object> updateObjectList = new List<object>();
			foreach(object theObject in processedObjectsList) {
				if(Session.IsNewObject(theObject))
					InternalInsertObject(insertList, updateList, theObject);
				else
					updateObjectList.Add(theObject);
			}
			foreach(InsertUpdate update in updateList)
				insertList.AddRange(UpdateQueryGenerator.GenerateUpdate(Session.Dictionary, BatchWideData, update.TheObject, update.Updatable));
			foreach(object theObject in updateObjectList)
				InternalUpdateObject(insertList, theObject);
			return insertList;
		}
		public void ProcessResults(ModificationResult result) {
			if(this.insertList.Count > 0) {
				SessionStateStack.Enter(Session, SessionState.ApplyIdentities);
				try {
					for(int i = 0; i < this.insertList.Count; i++) {
						Session.SetKeyValue(this.insertList[i], ((OperandValue)result.Identities[i]).Value);
					}
				}
				finally {
					SessionStateStack.Leave(Session, SessionState.ApplyIdentities);
				}
			}
			if(BatchWideData.InsertedObjects != null)
				foreach(object obj in BatchWideData.InsertedObjects)
					Session.RegisterInsertedObject(obj);
			if(BatchWideData.DeletedObjects != null) {
				foreach(object obj in BatchWideData.DeletedObjects) {
					if(!Session.IsNewObject(obj))
						SessionIdentityMap.UnregisterObject(Session, obj);
					IXPInvalidateableObject spoilableObject = obj as IXPInvalidateableObject;
					if(spoilableObject != null)
						spoilableObject.Invalidate();
				}
			}
		}
	}
	public class SaveOrderer {
		public readonly XPDictionary Dictionary;
		public readonly IDictionary NonSavedObjects;
		public readonly ObjectDictionary<ObjectSet> ReferredBy;
		public readonly ObjectDictionary<ObjectSet> References;
		public readonly List<object> Result;
		List<object> NonReferencedObjects;
		SaveOrderer(XPDictionary dictionary, ICollection newObjectsToSave) {
			this.Dictionary = dictionary;
			Result = new List<object>(newObjectsToSave.Count);
			ReferredBy = new ObjectDictionary<ObjectSet>(newObjectsToSave.Count);
			References = new ObjectDictionary<ObjectSet>(newObjectsToSave.Count);
			foreach(object obj in newObjectsToSave) {
				ReferredBy.Add(obj, new ObjectSet());
			}
			foreach(object obj in newObjectsToSave) {
				ObjectSet refers = new ObjectSet();
				References.Add(obj, refers);
				XPClassInfo ci = Dictionary.GetClassInfo(obj);
				foreach(XPMemberInfo r in ci.ObjectProperties) {
					if(r.IsDelayed && !XPDelayedProperty.GetDelayedPropertyContainer(obj, r).IsLoaded)
						continue;
					object referredObj = r.GetValue(obj);
					if(referredObj == null)
						continue;
					if(!ReferredBy.ContainsKey(referredObj))
						continue;
					refers.Add(referredObj);
					ReferredBy[referredObj].Add(obj);
				}
			}
			NonReferencedObjects = new List<object>();
			foreach(object obj in References.Keys) {
				if(References[obj].Count == 0)
					NonReferencedObjects.Add(obj);
			}
		}
		public void MarkObjectAsSaved(object obj) {
			ObjectSet referredBy;
			if(!ReferredBy.TryGetValue(obj, out referredBy))
				return;
			foreach(object r in referredBy) {
				ObjectSet list = References[r];
				list.Remove(obj);
				if(list.Count == 0)
					NonReferencedObjects.Add(r);
			}
			foreach(object r in References[obj]) {
				ReferredBy[r].Remove(obj);
			}
			ReferredBy.Remove(obj);
			References.Remove(obj);
			Result.Add(obj);
		}
		public void Do() {
			ProcessGoodObjects();
			Dictionary<object, ObjectSet>.Enumerator enFirst = References.GetEnumerator();
			while(enFirst.MoveNext()) {
				EleminateLoop(enFirst.Current.Key);
				enFirst = References.GetEnumerator();
			}
		}
		void ProcessGoodObjects() {
			for(int i = 0; i < NonReferencedObjects.Count; i++)
				MarkObjectAsSaved(NonReferencedObjects[i]);
			NonReferencedObjects.Clear();
		}
		void EleminateLoop(object nextObject) {
			List<object> list = new List<object>();
			ObjectDictionary<int> passedObjects = new ObjectDictionary<int>();
			for(int index = 0; ; index++) {
				passedObjects[nextObject] = index;
				list.Add(nextObject);
				IEnumerator enNext = References[nextObject].GetEnumerator();
				if(!enNext.MoveNext())
					throw new InvalidOperationException(Res.GetString(Res.Session_InternalXPOError));
				nextObject = enNext.Current;
				if(passedObjects.ContainsKey(nextObject)) {
					NonReferencedObjects.Add(nextObject);
					for(int i = 0; i < NonReferencedObjects.Count; i++) {
						object obj = NonReferencedObjects[i];
						MarkObjectAsSaved(obj);
						int j;
						if(passedObjects.TryGetValue(obj, out j)) {
							if(j < index)
								index = j;
						}
					}
					NonReferencedObjects.Clear();
					index--;
					if(index < 0)
						return;
					nextObject = list[index];
					list.RemoveRange(index, list.Count - index);
					index--;
				}
			}
		}
		static List<object> DoOrder(XPDictionary dictionary, ICollection newObjectsToSave) {
			SaveOrderer instance = new SaveOrderer(dictionary, newObjectsToSave);
			instance.Do();
			return instance.Result;
		}
		public static IList PrepareSaveOrder(Session session) {
			List<object> newObjects = new List<object>();
			List<object> oldObjects = new List<object>();
			foreach(object obj in session.GetObjectsToSave()) {
				if(session.IsObjectToDelete(obj)) {
				}
				else if(session.IsNewObject(obj)) {
					newObjects.Add(obj);
				}
				else {
					oldObjects.Add(obj);
				}
			}
			List<object> result = new List<object>();
			result.AddRange(DoOrder(session.Dictionary, newObjects));
			result.AddRange(oldObjects);
			return result;
		}
	}
	[Flags]
	public enum SessionState {
		Empty = 0,
		GetObjectsNonReenterant = 1,
		BeginTransaction = 2,
		CommitTransactionNonReenterant = 4,
		CommitChangesToDataLayer = 8,
		RollbackTransaction = 16,
		LoadingObjectsIntoNestedUow = 32,
		ReceivingObjectsFromNestedUow = 64,
		CancelEdit = 128,
		CreateObjectLoadingEnforcer = 256,
		OptimisticLockFieldsProcessing = 512,
		ApplyIdentities = 1024,
		LoadingStates = GetObjectsNonReenterant | ReceivingObjectsFromNestedUow | CancelEdit | CreateObjectLoadingEnforcer | OptimisticLockFieldsProcessing | ApplyIdentities,
		SavingStates = CommitTransactionNonReenterant | LoadingObjectsIntoNestedUow | OptimisticLockFieldsProcessing,
		ProhibitedForGetObjects = BeginTransaction | RollbackTransaction | CommitChangesToDataLayer | GetObjectsNonReenterant | CancelEdit | CreateObjectLoadingEnforcer | OptimisticLockFieldsProcessing,
		ProhibitedForCancelEdit = ProhibitedForGetObjects | LoadingObjectsIntoNestedUow,
		ProhibitedGeneric = BeginTransaction | RollbackTransaction | SavingStates | LoadingStates,
		SessionObjectsToSaveOrDeleteChanging = 2048,
		ProhibitedSessionObjectsToSaveOrDeleteChanging = CommitChangesToDataLayer | GetObjectsNonReenterant | LoadingObjectsIntoNestedUow,
		CommitChangesToDataLayerInner = 4096,
		CrossThreadFailure = 8192,
	}
	public static class SessionStateStack {
		public static void ThrowIfCantEnter(Session session, SessionState newState) {
			SessionState prohibited;
			switch(newState) {
				case SessionState.BeginTransaction:
				case SessionState.CommitTransactionNonReenterant:
				case SessionState.RollbackTransaction:
				case SessionState.LoadingObjectsIntoNestedUow:
				case SessionState.ReceivingObjectsFromNestedUow:
					prohibited = SessionState.ProhibitedGeneric;
					break;
				case SessionState.GetObjectsNonReenterant:
					prohibited = SessionState.ProhibitedForGetObjects;
					break;
				case SessionState.CancelEdit:
					prohibited = SessionState.ProhibitedForCancelEdit;
					break;
				case SessionState.CommitChangesToDataLayer:
				case SessionState.CommitChangesToDataLayerInner:
				case SessionState.CreateObjectLoadingEnforcer:
				case SessionState.OptimisticLockFieldsProcessing:
				case SessionState.ApplyIdentities:
					prohibited = SessionState.LoadingStates;
					break;
				case SessionState.SessionObjectsToSaveOrDeleteChanging:
					prohibited = SessionState.ProhibitedSessionObjectsToSaveOrDeleteChanging;
					break;
				default:
					throw new ArgumentException(string.Format(Res.GetString(Res.Session_UnexpectedState), newState.ToString()), nameof(newState));
			}
			prohibited |= newState | SessionState.CrossThreadFailure;
			if((session._StateStack & prohibited) != SessionState.Empty) {
				string message = string.Format(Res.GetString(Res.Session_EnteringTheX0StateFromTheX1StateIsProhibit), newState, session._StateStack & prohibited, session._StateStack, session);
				if(newState == SessionState.GetObjectsNonReenterant) {
					if(IsInAnyOf(session, SessionState.LoadingStates)) {
						message += Res.GetString(Res.Session_MostProbablyYouAreTryingToInitiateAnObjectEx);
					}
					else if(IsInAnyOf(session, SessionState.SavingStates)) {
						message += Res.GetString(Res.Session_MostProbablyYouAreTryingToInitiateAnObject);
					}
				}
				throw new InvalidOperationException(message);
			}
		}
		static bool _SuppressCrossThreadFailuresDetection;
		[Obsolete("CrossThread Failures Detection suppressed")]
		public static bool SuppressCrossThreadFailuresDetection {
			get { return _SuppressCrossThreadFailuresDetection; }
			set { _SuppressCrossThreadFailuresDetection = value; }
		}
		public static void Enter(Session session, SessionState newState) {
			if(!_SuppressCrossThreadFailuresDetection) {
				if(session._StateStack == SessionState.Empty || newState == SessionState.GetObjectsNonReenterant || newState == SessionState.CommitTransactionNonReenterant) {
					ThrowIfCrossThreadFailureOnEnter(session, newState, 0);
				}
			}
			ThrowIfCantEnter(session, newState);
			session._StateStack |= newState;
		}
		public static async Task EnterAsync(Session session, SessionState newState, int asyncOperationId, CancellationToken cancellationToken = default(CancellationToken)) {
			if(!_SuppressCrossThreadFailuresDetection) {
				if(asyncOperationId == 0) {
					throw new ArgumentOutOfRangeException(nameof(asyncOperationId));
				}
				if(session._StateStack == SessionState.Empty || newState == SessionState.GetObjectsNonReenterant || newState == SessionState.CommitTransactionNonReenterant || newState == SessionState.LoadingObjectsIntoNestedUow) {
					await session._AsyncLockSemaphore.WaitAsync(cancellationToken);
					ThrowIfCrossThreadFailureOnEnter(session, newState, asyncOperationId);
				}
			}
			ThrowIfCantEnter(session, newState);
			session._StateStack |= newState;
		}
		static void ThrowIfCrossThreadFailureOnEnter(Session session, SessionState newState, int asyncOperationId) {
			Thread currentThread = Thread.CurrentThread;
			Thread previousThread = session._ExchangeThreadWatch(currentThread);
			int previousAsyncOperationId = session._ExchangeAsyncOperationWatch(asyncOperationId);
			if(previousThread != null && previousThread != currentThread) {
				session._StateStack |= SessionState.CrossThreadFailure;
				throw new InvalidOperationException(Res.GetString(Res.Session_CrossThreadFailureDetected, session));
			}
			if(previousAsyncOperationId != 0 && previousAsyncOperationId != asyncOperationId) {
				session._ExchangeAsyncOperationWatch(previousAsyncOperationId);
				throw new InvalidOperationException(Res.GetString(Res.Session_CrossThreadFailureDetected, session));
			}
		}
		public static void Leave(Session session, SessionState state, int asyncOperationId = 0) {
			System.Diagnostics.Debug.Assert((session._StateStack & state) == state);
			session._StateStack &= ~state;
			if(session._StateStack == SessionState.Empty) {
				try {
					Thread previousThread = session._ExchangeThreadWatch(null);
					int previousAsyncOperationId = session._ExchangeAsyncOperationWatch(0);
					if(previousThread != null && previousThread != Thread.CurrentThread && previousAsyncOperationId == 0) {
						session._StateStack |= SessionState.CrossThreadFailure;
						throw new InvalidOperationException(Res.GetString(Res.Session_CrossThreadFailureDetected, session));
					}
					if(asyncOperationId != 0 && previousAsyncOperationId != asyncOperationId) {
						throw new InvalidOperationException(Res.GetString(Res.Session_CrossThreadFailureDetected, session));
					}
				}
				finally {
					if(asyncOperationId != 0) {
						session._AsyncLockSemaphore.Release();
					}
				}
			}
		}
		public static bool IsInAnyOf(Session session, SessionState states) {
			return (session._StateStack & states) != SessionState.Empty;
		}
		static int asyncOperationSequenceId = 0;
		internal static int GetNewAsyncOperationId() {
			while(true) {
				int v = Interlocked.Increment(ref asyncOperationSequenceId);
				if(v != 0) {
					return v;
				}
			}
		}
	}
	static class AsyncOperationsHelper {
		public static SynchronizationContext CaptureSynchronizationContextOrFail() {
			var context = Data.Helpers.SyncHelper.TryCaptureSynchronizationContext();
			if(context == null)
				throw new InvalidOperationException(Xpo.Res.GetString(Xpo.Res.Async_OperationCannotBePerformedBecauseNoSyncContext));
			return context;
		}
		public static void FailIfSynchronizationContextUnavailable() {
			if(Data.Helpers.SyncHelper.CanCaptureContext())
				return;
			throw new InvalidOperationException(Xpo.Res.GetString(Xpo.Res.Async_OperationCannotBePerformedBecauseNoSyncContext));
		}
	}
}
namespace DevExpress.Xpo {
	using System.Collections;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Data;
	using System.Reflection;
	using System.Threading;
	using System.Threading.Tasks;
	using DevExpress.Data.Filtering;
	using DevExpress.Data.Helpers;
	using DevExpress.Data.Utils;
	using DevExpress.Utils.Design;
	using DevExpress.Xpo.DB;
	using DevExpress.Xpo.Generators;
	using DevExpress.Xpo.Helpers;
	using DevExpress.Xpo.Infrastructure;
	using DevExpress.Xpo.Logger;
	using DevExpress.Xpo.Metadata.Helpers;
	public class ResolveSessionEventArgs : EventArgs {
		ISessionProvider provider;
		public ISessionProvider Session {
			get { return provider; }
			set { provider = value; }
		}
		public object Tag;
	}
	public delegate void ResolveSessionEventHandler(object sender, ResolveSessionEventArgs e);
	public class ObjectManipulationEventArgs : EventArgs {
		readonly object theObject;
		readonly Session session;
		public object Object { get { return theObject; } }
		public Session Session { get { return session; } }
		public ObjectManipulationEventArgs(object theObject) {
			this.theObject = theObject;
		}
		public ObjectManipulationEventArgs(Session session, object theObject) {
			this.theObject = theObject;
			this.session = session;
		}
	}
	public delegate void ObjectManipulationEventHandler(object sender, ObjectManipulationEventArgs e);
	public class ObjectsManipulationEventArgs : EventArgs {
		readonly ICollection objects;
		readonly Session session;
		public ICollection Objects { get { return objects; } }
		public Session Session { get { return session; } }
		public ObjectsManipulationEventArgs(Session session, ICollection objects) {
			this.session = session;
			this.objects = objects;
		}
	}
	public delegate void ObjectsManipulationEventHandler(object sender, ObjectsManipulationEventArgs e);
	public class SchemaInitEventArgs : EventArgs {
		XPClassInfo table;
		IDbCommand command;
		public XPClassInfo Table { get { return table; } }
		public IDbCommand Command { get { return command; } }
		public SchemaInitEventArgs(XPClassInfo table, IDbCommand command) {
			this.table = table;
			this.command = command;
		}
	}
	public delegate void SchemaInitEventHandler(object sender, SchemaInitEventArgs e);
	public class SessionManipulationEventArgs : EventArgs {
		Session session;
		public Session Session { get { return session; } }
		public SessionManipulationEventArgs(Session session) {
			this.session = session;
		}
	}
	public class SessionOperationFailEventArgs : EventArgs {
		Exception exception;
		bool handled;
		public Exception Exception { get { return exception; } }
		public bool Handled {
			get { return handled; }
			set { handled = value; }
		}
		public SessionOperationFailEventArgs(Exception exception) {
			this.exception = exception;
		}
	}
	public delegate void SessionManipulationEventHandler(object sender, SessionManipulationEventArgs e);
	public delegate object ProcessReferenceHandler(object theObject, XPMemberInfo member);
	public delegate void SessionOperationFailEventHandler(object sender, SessionOperationFailEventArgs e);
	public enum LockingOption { None, Optimistic };
	[DXToolboxItem(true)]
	[DevExpress.Utils.ToolboxTabName(AssemblyInfo.DXTabOrmComponents)]
	[Designer("DevExpress.Xpo.Design.SessionDesigner, " + AssemblyInfo.SRAssemblyXpoDesignFull, Aliases.IDesigner)]
	[DefaultProperty("Connection")]
	[DefaultEvent("SchemaInit")]
	[Description("Loads and saves persistent objects.")]
#if !NET
	[System.Drawing.ToolboxBitmap(typeof(Session))]
#endif
	public class Session : Component, ISupportInitialize, IPersistentValueExtractor, ISessionProvider, IWideDataStorage, ICommandChannel, ICommandChannelAsync, IInfrastructure<IServiceProvider>, IInfrastructure<ISecuredPropertyAccessor> {
		bool? trackPropertiesModifications;
		public const string LogCategory = "Session";
		public const string LogParam_SessionType = "sessionType";
		public const string LogParam_SessionID = "sessionID";
		public const string LogParam_ClassInfo = "classInfo";
		public const string LogParam_Expression = "expression";
		public const string LogParam_Criteria = "criteria";
		public const string LogParam_InTransaction = "inTransaction";
		public const string LogParam_Condition = "condition";
		public const string LogParam_Sorting = "sorting";
		public const string LogParam_SkipSelectedRecords = "skipSelectedRecords";
		public const string LogParam_TopSelectedRecords = "topSelectedRecords";
		public const string LogParam_SelectDeleted = "selectDeleted";
		public const string LogParam_TheObject = "theObject";
		public const string LogParam_Props = "propeties";
		public const string LogParam_Objects = "objects";
		public const string LogParam_Property = "property";
		public const string LogParam_ObjectID = "objectID";
		public const string LogParam_AlwaysGetFromDataStore = "alwaysGetFromDataStore";
		public const string LogParam_CriteriaEvaluationBehavior = "criteriaEvaluationBehavior";
		public const string LogParam_Members = "members";
		public const string LogParam_SprocName = "sprocName";
		public const string LogParam_Parameters = "parameters";
		public const string LogParam_Sql = "sql";
		public const string LogParam_GroupProps = "groupProperties";
		public const string LogParam_GroupCriteria = "groupCriteria";
		public const string LogParam_IsInTransactionMode = "IsInTransactionMode";
		internal SessionState _StateStack;
		Thread _ThreadWatch;
		internal Thread _ExchangeThreadWatch(Thread newThreadWatchValue) {
			return Interlocked.Exchange(ref _ThreadWatch, newThreadWatchValue);
		}
		int _AsyncOperationIdWatch;
		internal int _ExchangeAsyncOperationWatch(int newAsyncOperationId) {
			return Interlocked.Exchange(ref _AsyncOperationIdWatch, newAsyncOperationId);
		}
		internal readonly SemaphoreSlim _AsyncLockSemaphore = new SemaphoreSlim(1);
		IDbConnection _connection;
		bool closeConnectionOnDisconnect;
		IdentityMapBehavior _IdentityMapBehavior = IdentityMapBehavior.Default;
		bool? caseSensitive;
		string _connectionString;
		IDisposable[] _DisposeOnDisconnect;
		AutoCreateOption _autoCreateOption = AutoCreateOption.DatabaseAndSchema;
		LockingOption _lockingOption = LockingOption.Optimistic;
		OptimisticLockingReadBehavior _OptimisticLockingReadBehavior = OptimisticLockingReadBehavior.Default;
		bool trackingChanges = false;
		readonly IServiceProvider serviceProvider;
		protected static XPDictionary CreateDesignTimeDictionary(IServiceProvider provider) {
			return new DesignTimeReflection(provider);
		}
		protected virtual void BeginInit() { }
		protected virtual void EndInit() { }
		void ISupportInitialize.BeginInit() {
			BeginInit();
		}
		void ISupportInitialize.EndInit() {
			EndInit();
		}
		static bool inTransactionMode = false;
		[Description("Enables the mode in which querying a data store for objects includes all in-memory changes into query results.")]
		public static bool InTransactionMode { get { return inTransactionMode; } set { inTransactionMode = value; } }
		protected virtual bool IsInTransactionMode { get { return InTransactionMode; } }
		[Description("Gets or sets whether string comparisons evaluated by the session on the client are case-sensitive.")]
		[Category("Options")]
		public bool CaseSensitive {
			get { return caseSensitive.HasValue ? caseSensitive.Value : XpoDefault.DefaultCaseSensitive; }
			set { caseSensitive = value; }
		}
		[Description("Specifies whether or not the current Session tracks persistent object property modifications.")]
		[DefaultValue(false)]
		[Category("Options")]
		public bool TrackPropertiesModifications {
			get { return trackPropertiesModifications.HasValue ? trackPropertiesModifications.Value : XpoDefault.TrackPropertiesModifications; }
			set { trackPropertiesModifications = value; }
		}
		bool ShouldSerializeCaseSensitive() {
			return caseSensitive.HasValue;
		}
		void ResetCaseSensitive() {
			caseSensitive = null;
		}
		public static void SetAsyncRequestPriority(object asyncResult, AsyncRequestPriority priority) {
			AsyncRequest request = asyncResult as AsyncRequest;
			if(request == null)
				return;
			request.SetPriority(priority);
		}
		public static void CancelAsyncRequest(object asyncResult) {
			AsyncRequest request = asyncResult as AsyncRequest;
			if(request == null)
				return;
			request.Cancel();
		}
		public static WaitForAsyncOperationResult WaitForAsyncRequestComplete(Session session, object asyncResult) {
			AsyncRequest request = asyncResult as AsyncRequest;
			if(request == null)
				return WaitForAsyncOperationResult.OperationComplete;
			return session.AsyncExecuteQueue.WaitForAsyncOperationEnd(request);
		}
		XPObjectTypesManager typesManager;
		IObjectLayer objectLayer;
		internal SessionIdentityMap _IdentityMap;
		public List<object[]> SelectData(XPClassInfo classInfo, CriteriaOperatorCollection properties, CriteriaOperator criteria, bool selectDeleted, int topSelectedRecords, SortingCollection sorting) {
			return SelectData(classInfo, properties, criteria, selectDeleted, 0, topSelectedRecords, sorting);
		}
		public List<object[]> SelectData(XPClassInfo classInfo, CriteriaOperatorCollection properties, CriteriaOperator criteria, bool selectDeleted, int skipSelectedRecords, int topSelectedRecords, SortingCollection sorting) {
			return SelectData(classInfo, properties, criteria, null, null, selectDeleted, skipSelectedRecords, topSelectedRecords, sorting);
		}
		public Task<List<object[]>> SelectDataAsync(XPClassInfo classInfo, CriteriaOperatorCollection properties, CriteriaOperator criteria, bool selectDeleted, int topSelectedRecords, SortingCollection sorting, CancellationToken cancellationToken = default(CancellationToken)) {
			return SelectDataAsync(classInfo, properties, criteria, selectDeleted, 0, topSelectedRecords, sorting, cancellationToken);
		}
		public Task<List<object[]>> SelectDataAsync(XPClassInfo classInfo, CriteriaOperatorCollection properties, CriteriaOperator criteria, bool selectDeleted, int skipSelectedRecords, int topSelectedRecords, SortingCollection sorting, CancellationToken cancellationToken = default(CancellationToken)) {
			return SelectDataAsync(classInfo, properties, criteria, null, null, selectDeleted, skipSelectedRecords, topSelectedRecords, sorting, cancellationToken);
		}
		static ExpandedCriteriaHolder ExpandToLogical(CriteriaOperator criteria, XPClassInfo classInfo, bool doDetectPostProcessing, IPersistentValueExtractor persistentValueExtractor) {
			ExpandedCriteriaHolder criteriaExpanded = PersistentCriterionExpander.ExpandToLogical(persistentValueExtractor, classInfo, criteria, doDetectPostProcessing);
			if(criteriaExpanded.RequiresPostProcessing)
				throw new ArgumentException(Res.GetString(Res.PersistentAliasExpander_NonPersistentCriteria, CriteriaOperator.ToString(criteria), criteriaExpanded.PostProcessingCause));
			else
				return criteriaExpanded;
		}
		static ExpandedCriteriaHolder ExpandToValue(CriteriaOperator criteria, XPClassInfo classInfo, bool doDetectPostProcessing, IPersistentValueExtractor persistentValueExtractor) {
			ExpandedCriteriaHolder criteriaExpanded = PersistentCriterionExpander.ExpandToValue(persistentValueExtractor, classInfo, criteria, doDetectPostProcessing);
			if(criteriaExpanded.RequiresPostProcessing)
				throw new ArgumentException(Res.GetString(Res.PersistentAliasExpander_NonPersistentCriteria, CriteriaOperator.ToString(criteria), criteriaExpanded.PostProcessingCause));
			else
				return criteriaExpanded;
		}
		static bool IsNull(object op) {
			return op == null;
		}
		public List<object[]> SelectData(XPClassInfo classInfo, CriteriaOperatorCollection properties, CriteriaOperator criteria, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria, bool selectDeleted, int topSelectedRecords, SortingCollection sorting) {
			return SelectData(classInfo, properties, criteria, groupProperties, groupCriteria, selectDeleted, 0, topSelectedRecords, sorting);
		}
		public Task<List<object[]>> SelectDataAsync(XPClassInfo classInfo, CriteriaOperatorCollection properties, CriteriaOperator criteria, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria, bool selectDeleted, int topSelectedRecords, SortingCollection sorting, CancellationToken cancellationToken = default(CancellationToken)) {
			return SelectDataAsync(classInfo, properties, criteria, groupProperties, groupCriteria, selectDeleted, 0, topSelectedRecords, sorting, cancellationToken);
		}
		public List<object[]> SelectData(XPClassInfo classInfo, CriteriaOperatorCollection properties, CriteriaOperator criteria, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria, bool selectDeleted, int skipSelectedRecords, int topSelectedRecords, SortingCollection sorting) {
			return LogManager.Log<List<object[]>>(LogCategory, () => {
				if(IsInTransactionMode) {
					return InTransactionLoader.SelectData(this, classInfo, properties, criteria, groupProperties, groupCriteria, selectDeleted, skipSelectedRecords, topSelectedRecords, sorting);
				}
				return SelectDataInternal(classInfo, properties, criteria, groupProperties, groupCriteria, selectDeleted, skipSelectedRecords, topSelectedRecords, sorting);
			}, (d) => {
				return CreateLogMessage("Executing SelectData()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_IsInTransactionMode, this.IsInTransactionMode,
				LogParam_ClassInfo, classInfo.FullName,
				LogParam_Props, LogMessage.CriteriaOperatorCollectionToString(properties),
				LogParam_Criteria, ReferenceEquals(criteria, null) ? "null" : criteria.ToString(),
				LogParam_GroupProps, LogMessage.CriteriaOperatorCollectionToString(groupProperties),
				LogParam_GroupCriteria, ReferenceEquals(groupCriteria, null) ? "null" : groupCriteria.ToString(),
				LogParam_SelectDeleted, selectDeleted,
				LogParam_SkipSelectedRecords, skipSelectedRecords,
				LogParam_TopSelectedRecords, topSelectedRecords,
				LogParam_Sorting, LogHelpers.SortingCollectionToString(sorting)
			});
			});
		}
		public Task<List<object[]>> SelectDataAsync(XPClassInfo classInfo, CriteriaOperatorCollection properties, CriteriaOperator criteria, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria, bool selectDeleted, int skipSelectedRecords, int topSelectedRecords, SortingCollection sorting, CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync<List<object[]>>(LogCategory, () => {
				if(IsInTransactionMode) {
					return InTransactionLoader.SelectDataAsync(this, classInfo, properties, criteria, groupProperties, groupCriteria, selectDeleted, skipSelectedRecords, topSelectedRecords, sorting, cancellationToken);
				}
				return SelectDataInternalAsync(classInfo, properties, criteria, groupProperties, groupCriteria, selectDeleted, skipSelectedRecords, topSelectedRecords, sorting, cancellationToken);
			}, (d) => {
				return CreateLogMessage("Executing SelectDataAsync()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_IsInTransactionMode, this.IsInTransactionMode,
				LogParam_ClassInfo, classInfo.FullName,
				LogParam_Props, LogMessage.CriteriaOperatorCollectionToString(properties),
				LogParam_Criteria, ReferenceEquals(criteria, null) ? "null" : criteria.ToString(),
				LogParam_GroupProps, LogMessage.CriteriaOperatorCollectionToString(groupProperties),
				LogParam_GroupCriteria, ReferenceEquals(groupCriteria, null) ? "null" : groupCriteria.ToString(),
				LogParam_SelectDeleted, selectDeleted,
				LogParam_SkipSelectedRecords, skipSelectedRecords,
				LogParam_TopSelectedRecords, topSelectedRecords,
				LogParam_Sorting, LogHelpers.SortingCollectionToString(sorting)
			});
			});
		}
		public List<object[]> SelectDataInTransaction(XPClassInfo classInfo, CriteriaOperatorCollection properties, CriteriaOperator criteria, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria, bool selectDeleted, int skipSelectedRecords, int topSelectedRecords, SortingCollection sorting) {
			return LogManager.Log<List<object[]>>(LogCategory, () => {
				return InTransactionLoader.SelectData(this, classInfo, properties, criteria, groupProperties, groupCriteria, selectDeleted, skipSelectedRecords, topSelectedRecords, sorting);
			}, (d) => {
				return CreateLogMessage("Executing SelectDataInTransaction()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_ClassInfo, classInfo.FullName,
				LogParam_Props, LogMessage.CriteriaOperatorCollectionToString(properties),
				LogParam_Criteria, ReferenceEquals(criteria, null) ? "null" : criteria.ToString(),
				LogParam_GroupProps, LogMessage.CriteriaOperatorCollectionToString(groupProperties),
				LogParam_GroupCriteria, ReferenceEquals(groupCriteria, null) ? "null" : groupCriteria.ToString(),
				LogParam_SelectDeleted, selectDeleted,
				LogParam_SkipSelectedRecords, skipSelectedRecords,
				LogParam_TopSelectedRecords, topSelectedRecords,
				LogParam_Sorting, LogHelpers.SortingCollectionToString(sorting)
			});
			});
		}
		public Task<List<object[]>> SelectDataInTransactionAsync(XPClassInfo classInfo, CriteriaOperatorCollection properties, CriteriaOperator criteria, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria, bool selectDeleted, int skipSelectedRecords, int topSelectedRecords, SortingCollection sorting, CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync<List<object[]>>(LogCategory, () => {
				return InTransactionLoader.SelectDataAsync(this, classInfo, properties, criteria, groupProperties, groupCriteria, selectDeleted, skipSelectedRecords, topSelectedRecords, sorting, cancellationToken);
			}, (d) => {
				return CreateLogMessage("Executing SelectDataInTransactionAsync()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_ClassInfo, classInfo.FullName,
				LogParam_Props, LogMessage.CriteriaOperatorCollectionToString(properties),
				LogParam_Criteria, ReferenceEquals(criteria, null) ? "null" : criteria.ToString(),
				LogParam_GroupProps, LogMessage.CriteriaOperatorCollectionToString(groupProperties),
				LogParam_GroupCriteria, ReferenceEquals(groupCriteria, null) ? "null" : groupCriteria.ToString(),
				LogParam_SelectDeleted, selectDeleted,
				LogParam_SkipSelectedRecords, skipSelectedRecords,
				LogParam_TopSelectedRecords, topSelectedRecords,
				LogParam_Sorting, LogHelpers.SortingCollectionToString(sorting)
			});
			});
		}
		internal virtual List<object[]> SelectDataInternal(XPClassInfo classInfo, CriteriaOperatorCollection properties, CriteriaOperator criteria, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria, bool selectDeleted, int skipSelectedRecords, int topSelectedRecords, SortingCollection sorting) {
			List<object[]> result = PrepareSelectData(classInfo, ref properties, ref criteria, ref groupProperties, ref groupCriteria, ref sorting, true, this);
			if(result == null)
				result = ObjectLayer.SelectData(this, new ObjectsQuery(classInfo, criteria, sorting, skipSelectedRecords, topSelectedRecords, new CollectionCriteriaPatcher(selectDeleted, TypesManager), true), properties, groupProperties, groupCriteria);
			return result;
		}
		internal virtual async Task<List<object[]>> SelectDataInternalAsync(XPClassInfo classInfo, CriteriaOperatorCollection properties, CriteriaOperator criteria, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria, bool selectDeleted, int skipSelectedRecords, int topSelectedRecords, SortingCollection sorting, CancellationToken cancellationToken = default(CancellationToken)) {
			await UpdateSchemaAsync(cancellationToken, classInfo);
			await TypesManager.EnsureIsTypedObjectValidAsync(cancellationToken);
			List<object[]> result = PrepareSelectDataInternal(classInfo, ref properties, ref criteria, ref groupProperties, ref groupCriteria, ref sorting, true, this);
			if(result == null) {
				return await GetObjectLayerAsync().SelectDataAsync(this, new ObjectsQuery(classInfo, criteria, sorting, skipSelectedRecords, topSelectedRecords, new CollectionCriteriaPatcher(selectDeleted, TypesManager), true), properties, groupProperties, groupCriteria, cancellationToken);
			}
			return result;
		}
		internal ObjectsQuery[] PrepareQueries(ObjectsQuery[] queries, bool doDetectPostProcessing, IPersistentValueExtractor persistentValueExtractor) {
			int queriesCount = queries == null ? 0 : queries.Length;
			ObjectsQuery[] result = new ObjectsQuery[queriesCount];
			TypesManager.EnsureIsTypedObjectValid();
			for(int i = 0; i < queriesCount; i++) {
				result[i] = PrepareQuery(queries[i], doDetectPostProcessing, persistentValueExtractor);
			}
			if(queriesCount > 0) {
				XPClassInfo[] classInfoList = new XPClassInfo[queriesCount];
				for(int i = 0; i < queriesCount; i++) {
					classInfoList[i] = result[i].ClassInfo;
				}
				UpdateSchema(classInfoList);
			}
			return result;
		}
		ObjectsQuery PrepareQuery(ObjectsQuery objectsQuery, bool doDetectPostProcessing, IPersistentValueExtractor persistentValueExtractor) {
			XPClassInfo classInfo = objectsQuery.ClassInfo;
			CriteriaOperator criteria = objectsQuery.Criteria;
			if(!IsNull(criteria)) {
				ExpandedCriteriaHolder criteriaHolder = ExpandToLogical(criteria, classInfo, doDetectPostProcessing, persistentValueExtractor);
				criteria = criteriaHolder.ExpandedCriteria;
			}
			SortingCollection sorting = objectsQuery.Sorting;
			if(!IsNull(sorting)) {
				SortingCollection expanded = new SortingCollection();
				for(int i = 0; i < sorting.Count; i++)
					expanded.Add(new SortProperty(ExpandToValue(sorting[i].Property, classInfo, doDetectPostProcessing, persistentValueExtractor).ExpandedCriteria, sorting[i].Direction));
				sorting = expanded;
			}
			return new ObjectsQuery(classInfo, criteria, sorting, objectsQuery.SkipSelectedRecords, objectsQuery.TopSelectedRecords, objectsQuery.CollectionCriteriaPatcher, objectsQuery.Force);
		}
		internal List<object[]> PrepareSelectData(XPClassInfo classInfo, ref CriteriaOperatorCollection properties, ref CriteriaOperator criteria, ref CriteriaOperatorCollection groupProperties, ref CriteriaOperator groupCriteria, ref SortingCollection sorting, bool doDetectPostProcessing, IPersistentValueExtractor persistentValueExtractor) {
			UpdateSchema(classInfo);
			TypesManager.EnsureIsTypedObjectValid();
			return PrepareSelectDataInternal(classInfo, ref properties, ref criteria, ref groupProperties, ref groupCriteria, ref sorting, doDetectPostProcessing, persistentValueExtractor);
		}
		internal static List<object[]> PrepareSelectDataInternal(XPClassInfo classInfo, ref CriteriaOperatorCollection properties, ref CriteriaOperator criteria, ref CriteriaOperatorCollection groupProperties, ref CriteriaOperator groupCriteria, ref SortingCollection sorting, bool doDetectPostProcessing, IPersistentValueExtractor persistentValueExtractor) {
			if(!IsNull(criteria)) {
				ExpandedCriteriaHolder criteriaHolder = ExpandToLogical(criteria, classInfo, doDetectPostProcessing, persistentValueExtractor);
				if(criteriaHolder.IsFalse)
					return new List<object[]>(0);
				else
					criteria = criteriaHolder.ExpandedCriteria;
			}
			if(!IsNull(properties)) {
				CriteriaOperatorCollection expanded = new CriteriaOperatorCollection();
				for(int i = 0; i < properties.Count; i++)
					expanded.Add(ExpandToValue(properties[i], classInfo, doDetectPostProcessing, persistentValueExtractor).ExpandedCriteria);
				properties = expanded;
			}
			if(!IsNull(groupProperties)) {
				CriteriaOperatorCollection expanded = new CriteriaOperatorCollection();
				for(int i = 0; i < groupProperties.Count; i++)
					expanded.Add(ExpandToValue(groupProperties[i], classInfo, doDetectPostProcessing, persistentValueExtractor).ExpandedCriteria);
				groupProperties = expanded;
			}
			if(!IsNull(groupCriteria)) {
				ExpandedCriteriaHolder criteriaHolder = ExpandToLogical(groupCriteria, classInfo, doDetectPostProcessing, persistentValueExtractor);
				if(criteriaHolder.IsFalse)
					return new List<object[]>(0);
				else
					groupCriteria = criteriaHolder.ExpandedCriteria;
			}
			if(!IsNull(sorting)) {
				SortingCollection expanded = new SortingCollection();
				for(int i = 0; i < sorting.Count; i++)
					expanded.Add(new SortProperty(ExpandToValue(sorting[i].Property, classInfo, doDetectPostProcessing, persistentValueExtractor).ExpandedCriteria, sorting[i].Direction));
				sorting = expanded;
			}
			return null;
		}
		public object SelectDataAsync(XPClassInfo classInfo, CriteriaOperatorCollection properties, CriteriaOperator criteria, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria, bool selectDeleted, int skipSelectedRecords, int topSelectedRecords, SortingCollection sorting, AsyncSelectDataCallback callback) {
			if(IsInTransactionMode) {
				InTransactionLoader.SelectDataAsync(this, classInfo, properties, criteria, groupProperties, groupCriteria, selectDeleted, skipSelectedRecords, topSelectedRecords, sorting, callback);
				return null;
			}
			return SelectDataInternalAsync(classInfo, properties, criteria, groupProperties, groupCriteria, selectDeleted, skipSelectedRecords, topSelectedRecords, sorting, callback);
		}
		internal virtual object SelectDataInternalAsync(XPClassInfo classInfo, CriteriaOperatorCollection properties, CriteriaOperator criteria, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria, bool selectDeleted, int skipSelectedRecords, int topSelectedRecords, SortingCollection sorting, AsyncSelectDataCallback callback) {
			AsyncOperationsHelper.FailIfSynchronizationContextUnavailable();
			Utils.Guard.ArgumentNotNull(callback, nameof(callback));
			List<object[]> result = PrepareSelectData(classInfo, ref properties, ref criteria, ref groupProperties, ref groupCriteria, ref sorting, true, this);
			if(result != null) {
				return ObjectLayer.SelectDataAsync(this, null, null, null, null, callback);
			}
			else {
				return ObjectLayer.SelectDataAsync(this, new ObjectsQuery(classInfo, criteria, sorting, skipSelectedRecords, topSelectedRecords, new CollectionCriteriaPatcher(selectDeleted, TypesManager), true), properties, groupProperties, groupCriteria, callback);
			}
		}
		public object Evaluate(Type objectType, CriteriaOperator expression, CriteriaOperator criteria) {
			return Evaluate(GetClassInfo(objectType), expression, criteria);
		}
		public Task<object> EvaluateAsync(Type objectType, CriteriaOperator expression, CriteriaOperator criteria, CancellationToken cancellationToken = default(CancellationToken)) {
			return EvaluateAsync(GetClassInfo(objectType), expression, criteria, cancellationToken);
		}
		class FalseCriteriaTopLevelEvaluatorDescriptor : EvaluatorContextDescriptor {
			public override IEnumerable GetCollectionContexts(object source, string collectionName) {
				return Array.Empty<object>();
			}
			public override EvaluatorContext GetNestedContext(object source, string propertyPath) {
				return new EvaluatorContext(this, source);
			}
			public override object GetPropertyValue(object source, EvaluatorProperty propertyPath) {
				return null;
			}
			public override IEnumerable GetQueryContexts(object source, string queryTypeName, CriteriaOperator condition, int top) {
				return Array.Empty<object>();
			}
			public override bool IsTopLevelCollectionSource {
				get {
					return true;
				}
			}
		}
		public object Evaluate(XPClassInfo classInfo, CriteriaOperator expression, CriteriaOperator criteria) {
			return Evaluate(classInfo, expression, criteria, false);
		}
		public Task<object> EvaluateAsync(XPClassInfo classInfo, CriteriaOperator expression, CriteriaOperator criteria, CancellationToken cancellationToken = default(CancellationToken)) {
			return EvaluateAsync(classInfo, expression, criteria, false, cancellationToken);
		}
		public object EvaluateInTransaction(XPClassInfo classInfo, CriteriaOperator expression, CriteriaOperator criteria) {
			return Evaluate(classInfo, expression, criteria, true);
		}
		public Task<object> EvaluateInTransactionAsync(XPClassInfo classInfo, CriteriaOperator expression, CriteriaOperator criteria, CancellationToken cancellationToken = default(CancellationToken)) {
			return EvaluateAsync(classInfo, expression, criteria, true, cancellationToken);
		}
		object Evaluate(XPClassInfo classInfo, CriteriaOperator expression, CriteriaOperator criteria, bool inTransaction) {
			return LogManager.Log<object>(LogCategory, () => {
				List<object[]> result = null;
				if(inTransaction) {
					CriteriaOperatorCollection props = new CriteriaOperatorCollection(1);
					props.Add(expression);
					result = SelectDataInTransaction(classInfo, props, criteria, null, null, false, 0, 1, null);
				}
				else {
					result = PrepareEvaluate(classInfo, ref expression, ref criteria, this, CaseSensitive);
					UpdateSchema(false, classInfo);
					TypesManager.EnsureIsTypedObjectValid();
					CriteriaOperatorCollection props = new CriteriaOperatorCollection(1);
					props.Add(expression);
					if(result == null) {
						result = SelectData(classInfo, props, criteria, null, null, false, 0, 1, null);
					}
				}
				return (result == null) || (result.Count == 0) || (result[0] == null) || (result[0].Length == 0) ? null : result[0][0];
			}, (d) => {
				return CreateLogMessage("Executing Evaluate()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_IsInTransactionMode, this.IsInTransactionMode,
				LogParam_ClassInfo, classInfo.FullName,
				LogParam_Expression, ReferenceEquals(expression, null) ? "null" : expression.ToString(),
				LogParam_Criteria, ReferenceEquals(criteria, null) ? "null" : criteria.ToString(),
				LogParam_InTransaction, inTransaction
			});
			});
		}
		Task<object> EvaluateAsync(XPClassInfo classInfo, CriteriaOperator expression, CriteriaOperator criteria, bool inTransaction, CancellationToken cancellationToken) {
			return LogManager.LogAsync<object>(LogCategory, async () => {
				List<object[]> result = null;
				if(inTransaction) {
					CriteriaOperatorCollection props = new CriteriaOperatorCollection(1);
					props.Add(expression);
					result = await SelectDataInTransactionAsync(classInfo, props, criteria, null, null, false, 0, 1, null, cancellationToken).ConfigureAwait(false);
				}
				else {
					result = PrepareEvaluate(classInfo, ref expression, ref criteria, this, CaseSensitive);
					UpdateSchema(false, classInfo);
					await TypesManager.EnsureIsTypedObjectValidAsync(cancellationToken);
					CriteriaOperatorCollection props = new CriteriaOperatorCollection(1);
					props.Add(expression);
					if(result == null) {
						result = await SelectDataAsync(classInfo, props, criteria, null, null, false, 0, 1, null, cancellationToken).ConfigureAwait(false);
					}
				}
				return (result == null) || (result.Count == 0) || (result[0] == null) || (result[0].Length == 0) ? null : result[0][0];
			}, (d) => {
				return CreateLogMessage("Executing EvaluateAsync()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_IsInTransactionMode, this.IsInTransactionMode,
				LogParam_ClassInfo, classInfo.FullName,
				LogParam_Expression, ReferenceEquals(expression, null) ? "null" : expression.ToString(),
				LogParam_Criteria, ReferenceEquals(criteria, null) ? "null" : criteria.ToString(),
				LogParam_InTransaction, inTransaction
			});
			});
		}
		internal static List<object[]> PrepareEvaluate(XPClassInfo classInfo, ref CriteriaOperator expression, ref CriteriaOperator criteria, IPersistentValueExtractor extractor, bool caseSensitive) {
			ExpandedCriteriaHolder criteriaHolder = ExpandToLogical(criteria, classInfo, true, extractor);
			if(criteriaHolder.IsFalse) {
				ExpressionEvaluator evaluator = new ExpressionEvaluator(new FalseCriteriaTopLevelEvaluatorDescriptor(), expression, caseSensitive, classInfo.Dictionary.CustomFunctionOperators, classInfo.Dictionary.CustomAggregates);
				object rv = evaluator.Evaluate(null);
				return new List<object[]>(new object[][] { new object[] { rv } });
			}
			else {
				expression = ExpandToValue(expression, classInfo, true, extractor).ExpandedCriteria;
				criteria = ExpandedCriteriaHolder.IfNeededConvertToLogicalOperator(criteria);
				return null;
			}
		}
		public object Evaluate<ClassType>(CriteriaOperator expression, CriteriaOperator criteria) {
			return Evaluate(typeof(ClassType), expression, criteria);
		}
		public object EvaluateInTransaction(Type objectType, CriteriaOperator expression, CriteriaOperator criteria) {
			return EvaluateInTransaction(GetClassInfo(objectType), expression, criteria);
		}
		public Task<object> EvaluateInTransactionAsync(Type objectType, CriteriaOperator expression, CriteriaOperator criteria, CancellationToken cancellationToken = default(CancellationToken)) {
			return EvaluateInTransactionAsync(GetClassInfo(objectType), expression, criteria, cancellationToken);
		}
		public object EvaluateInTransaction<ClassType>(CriteriaOperator expression, CriteriaOperator criteria) {
			return EvaluateInTransaction(typeof(ClassType), expression, criteria);
		}
		public Task<object> EvaluateInTransactionAsync<ClassType>(CriteriaOperator expression, CriteriaOperator criteria, CancellationToken cancellationToken = default(CancellationToken)) {
			return EvaluateInTransactionAsync(typeof(ClassType), expression, criteria, cancellationToken);
		}
		AsyncExecuteQueue asyncExecuteQueue;
		internal AsyncExecuteQueue AsyncExecuteQueue {
			get {
				if(asyncExecuteQueue == null)
					asyncExecuteQueue = new AsyncExecuteQueue();
				return asyncExecuteQueue;
			}
		}
		internal WaitForAsyncOperationResult WaitForAsyncOperationEnd() {
			if(asyncExecuteQueue != null)
				return asyncExecuteQueue.WaitForAsyncOperationEnd();
			else
				return WaitForAsyncOperationResult.EmptyQueue;
		}
		internal ICollection GetObjects(ObjectsQuery query) {
			return GetObjects(new ObjectsQuery[] { query })[0];
		}
		public virtual ICollection[] GetObjects(ObjectsQuery[] queries) {
			return LogManager.Log<ICollection[]>(LogCategory, () => {
				if(IsInTransactionMode) {
					return InTransactionLoader.GetObjects(this, queries);
				}
				return GetObjectsInternal(queries);
			}, (d) => {
				LogMessage lm = new LogMessage(LogMessageType.SessionEvent, "Executing GetObjects()", d);
				lm.ParameterList.Add(new LogMessageParameter(LogParam_SessionType, this.GetType().ToString()));
				lm.ParameterList.Add(new LogMessageParameter(LogParam_SessionID, this.ToString()));
				lm.ParameterList.Add(new LogMessageParameter(LogParam_IsInTransactionMode, IsInTransactionMode));
				for(int i = 0; i < queries.Length; i++) {
					lm.ParameterList.Add(new LogMessageParameter("query #" + i, LogHelpers.QueryToString(queries[i])));
				}
				return lm;
			});
		}
		internal virtual ICollection[] GetObjectsInternal(ObjectsQuery[] queries) {
			TypesManager.EnsureIsTypedObjectValid();
			return ObjectLayer.LoadObjects(this, queries);
		}
		public virtual object GetObjectsAsync(ObjectsQuery[] queries, AsyncLoadObjectsCallback callback) {
			return LogManager.Log<object>(LogCategory, () => {
				if(IsInTransactionMode) {
					InTransactionLoader.GetObjectsAsync(this, queries, callback);
					return null;
				}
				return GetObjectsInternalAsync(queries, callback);
			}, (d) => {
				LogMessage lm;
				lm = new LogMessage(LogMessageType.SessionEvent, "Executing GetObjectsAsync()", d);
				lm.ParameterList.Add(new LogMessageParameter(LogParam_SessionType, this.GetType().ToString()));
				lm.ParameterList.Add(new LogMessageParameter(LogParam_SessionID, this.ToString()));
				lm.ParameterList.Add(new LogMessageParameter(LogParam_IsInTransactionMode, IsInTransactionMode));
				for(int i = 0; i < queries.Length; i++) {
					lm.ParameterList.Add(new LogMessageParameter("query #" + i, LogHelpers.QueryToString(queries[i])));
				}
				return lm;
			});
		}
		internal async Task<ICollection> GetObjectsAsync(ObjectsQuery query, CancellationToken cancellationToken = default(CancellationToken)) {
			ICollection[] colls = await GetObjectsAsync(new ObjectsQuery[] { query }, cancellationToken).ConfigureAwait(false);
			return colls[0];
		}
		public virtual Task<ICollection[]> GetObjectsAsync(ObjectsQuery[] queries, CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync(LogCategory, () => {
				if(IsInTransactionMode) {
					return InTransactionLoader.GetObjectsAsync(this, queries, cancellationToken);
				}
				return GetObjectsInternalAsync(queries, cancellationToken);
			}, (d) => {
				LogMessage lm;
				lm = new LogMessage(LogMessageType.SessionEvent, "Executing GetObjectsAsync()", d);
				lm.ParameterList.Add(new LogMessageParameter(LogParam_SessionType, this.GetType().ToString()));
				lm.ParameterList.Add(new LogMessageParameter(LogParam_SessionID, this.ToString()));
				lm.ParameterList.Add(new LogMessageParameter(LogParam_IsInTransactionMode, IsInTransactionMode));
				for(int i = 0; i < queries.Length; i++) {
					lm.ParameterList.Add(new LogMessageParameter("query #" + i, LogHelpers.QueryToString(queries[i])));
				}
				return lm;
			});
		}
		internal virtual object GetObjectsInternalAsync(ObjectsQuery[] queries, AsyncLoadObjectsCallback callback) {
			TypesManager.EnsureIsTypedObjectValid();
			return ObjectLayer.LoadObjectsAsync(this, queries, callback);
		}
		internal virtual async Task<ICollection[]> GetObjectsInternalAsync(ObjectsQuery[] queries, CancellationToken cancellationToken = default(CancellationToken)) {
			await TypesManager.EnsureIsTypedObjectValidAsync(cancellationToken);
			return await GetObjectLayerAsync().LoadObjectsAsync(this, queries, cancellationToken);
		}
		public object GetObjectsAsync(XPClassInfo classInfo, CriteriaOperator criteria, SortingCollection sorting, int topSelectedRecords, bool selectDeleted, bool force, AsyncLoadObjectsCallback callback) {
			return GetObjectsAsync(classInfo, criteria, sorting, 0, topSelectedRecords, selectDeleted, force, callback);
		}
		public object GetObjectsAsync(XPClassInfo classInfo, CriteriaOperator criteria, SortingCollection sorting, int skipSelectedRecords, int topSelectedRecords, bool selectDeleted, bool force, AsyncLoadObjectsCallback callback) {
			return GetObjectsAsync(new ObjectsQuery[] { new ObjectsQuery(classInfo, criteria, sorting, skipSelectedRecords, topSelectedRecords, new CollectionCriteriaPatcher(selectDeleted, TypesManager), force) }, callback);
		}
		public Task<ICollection> GetObjectsAsync(XPClassInfo classInfo, CriteriaOperator criteria, SortingCollection sorting, int topSelectedRecords, bool selectDeleted, bool force, CancellationToken cancellationToken = default(CancellationToken)) {
			return GetObjectsAsync(classInfo, criteria, sorting, 0, topSelectedRecords, selectDeleted, force, cancellationToken);
		}
		public Task<ICollection> GetObjectsAsync(XPClassInfo classInfo, CriteriaOperator criteria, SortingCollection sorting, int skipSelectedRecords, int topSelectedRecords, bool selectDeleted, bool force, CancellationToken cancellationToken = default(CancellationToken)) {
			return GetObjectsAsync(new ObjectsQuery(classInfo, criteria, sorting, skipSelectedRecords, topSelectedRecords, new CollectionCriteriaPatcher(selectDeleted, TypesManager), force), cancellationToken);
		}
		[Obsolete("Use the method's overload with 'bool selectDeleted' parameter instead of 'CollectionCriteriaPatcher collectionCriteriaPatcher'")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public ICollection GetObjects(XPClassInfo classInfo, CriteriaOperator criteria, SortingCollection sorting, int topSelectedRecords, CollectionCriteriaPatcher collectionCriteriaPatcher, bool force) {
			return GetObjects(new ObjectsQuery(classInfo, criteria, sorting, topSelectedRecords, collectionCriteriaPatcher, force));
		}
		public ICollection GetObjects(XPClassInfo classInfo, CriteriaOperator criteria, SortingCollection sorting, int topSelectedRecords, bool selectDeleted, bool force) {
			return GetObjects(classInfo, criteria, sorting, 0, topSelectedRecords, selectDeleted, force);
		}
		public ICollection GetObjects(XPClassInfo classInfo, CriteriaOperator criteria, SortingCollection sorting, int skipSelectedRecords, int topSelectedRecords, bool selectDeleted, bool force) {
			return GetObjects(new ObjectsQuery(classInfo, criteria, sorting, skipSelectedRecords, topSelectedRecords, new CollectionCriteriaPatcher(selectDeleted, TypesManager), force));
		}
		[Obsolete("Use the method's overload without 'bool caseSensitive' parameter")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public ICollection GetObjectsInTransaction(XPClassInfo classInfo, CriteriaOperator condition, bool selectDeleted, bool caseSensitive) {
			return InTransactionLoader.GetObjects(this, new ObjectsQuery[] { new ObjectsQuery(classInfo, condition, null, 0, new CollectionCriteriaPatcher(selectDeleted, TypesManager), false) }, caseSensitive)[0];
		}
		public ICollection GetObjectsInTransaction(XPClassInfo classInfo, CriteriaOperator condition, bool selectDeleted) {
			return GetObjectsInTransaction(classInfo, condition, null, 0, 0, selectDeleted);
		}
		public Task<ICollection> GetObjectsInTransactionAsync(XPClassInfo classInfo, CriteriaOperator condition, bool selectDeleted, CancellationToken cancellationToken = default(CancellationToken)) {
			return GetObjectsInTransactionAsync(classInfo, condition, null, 0, 0, selectDeleted, cancellationToken);
		}
		public ICollection GetObjectsInTransaction(XPClassInfo classInfo, CriteriaOperator condition, SortingCollection sorting, int skipSelectedRecords, int topSelectedRecords, bool selectDeleted) {
			return LogManager.Log<ICollection>(LogCategory, () => {
				return InTransactionLoader.GetObjects(this, new ObjectsQuery[] { new ObjectsQuery(classInfo, condition, sorting, skipSelectedRecords, topSelectedRecords, new CollectionCriteriaPatcher(selectDeleted, TypesManager), false) })[0];
			}, (d) => {
				return CreateLogMessage("Executing GetObjectsInTransaction()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_ClassInfo, classInfo.FullName,
				LogParam_Condition, ReferenceEquals(condition, null) ? "null" : condition.ToString(),
				LogParam_Sorting, LogHelpers.SortingCollectionToString(sorting),
				LogParam_SkipSelectedRecords, skipSelectedRecords,
				LogParam_TopSelectedRecords, topSelectedRecords,
				LogParam_SelectDeleted, selectDeleted
			});
			});
		}
		public object GetObjectsInTransactionAsync(XPClassInfo classInfo, CriteriaOperator condition, bool selectDeleted, AsyncLoadObjectsCallback callback) {
			return GetObjectsInTransactionAsync(classInfo, condition, null, 0, 0, selectDeleted, callback);
		}
		public object GetObjectsInTransactionAsync(XPClassInfo classInfo, CriteriaOperator condition, SortingCollection sorting, int skipSelectedRecords, int topSelectedRecords, bool selectDeleted, AsyncLoadObjectsCallback callback) {
			return LogManager.Log<object>(LogCategory, () => {
				InTransactionLoader.GetObjectsAsync(this, new ObjectsQuery[] { new ObjectsQuery(classInfo, condition, sorting, skipSelectedRecords, topSelectedRecords, new CollectionCriteriaPatcher(selectDeleted, TypesManager), false) }, callback);
				return null;
			}, (d) => {
				return CreateLogMessage("Executing GetObjectsInTransactionAsync()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_ClassInfo, classInfo.FullName,
				LogParam_Condition, ReferenceEquals(condition, null) ? "null" : condition.ToString(),
				LogParam_Sorting, LogHelpers.SortingCollectionToString(sorting),
				LogParam_SkipSelectedRecords, skipSelectedRecords,
				LogParam_TopSelectedRecords, topSelectedRecords,
				LogParam_SelectDeleted, selectDeleted
			});
			});
		}
		public Task<ICollection> GetObjectsInTransactionAsync(XPClassInfo classInfo, CriteriaOperator condition, SortingCollection sorting, int skipSelectedRecords, int topSelectedRecords, bool selectDeleted, CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync(LogCategory, async () => {
				ICollection[] objects = await InTransactionLoader.GetObjectsAsync(this, new ObjectsQuery[] { new ObjectsQuery(classInfo, condition, sorting, skipSelectedRecords, topSelectedRecords, new CollectionCriteriaPatcher(selectDeleted, TypesManager), false) }, cancellationToken).ConfigureAwait(false);
				return objects[0];
			}, (d) => {
				return CreateLogMessage("Executing GetObjectsInTransactionAsync()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_ClassInfo, classInfo.FullName,
				LogParam_Condition, ReferenceEquals(condition, null) ? "null" : condition.ToString(),
				LogParam_Sorting, LogHelpers.SortingCollectionToString(sorting),
				LogParam_SkipSelectedRecords, skipSelectedRecords,
				LogParam_TopSelectedRecords, topSelectedRecords,
				LogParam_SelectDeleted, selectDeleted
			});
			});
		}
		[Browsable(false)]
		public bool IsObjectsLoading {
			get {
				return SessionStateStack.IsInAnyOf(this, SessionState.LoadingStates);
			}
		}
		[Browsable(false)]
		public bool IsObjectsSaving {
			get {
				return SessionStateStack.IsInAnyOf(this, SessionState.SavingStates);
			}
		}
		protected internal virtual object[] LoadDelayedProperties(object theObject, MemberPathCollection props) {
			return LogManager.Log<object[]>(LogCategory, () => {
				ThrowIfObjectFromDifferentSession(theObject);
				return ((IObjectLayerEx)ObjectLayer).LoadDelayedProperties(this, theObject, props);
			}, (d) => {
				return CreateLogMessage("Executing LoadDelayedProperties()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_TheObject, GetObjectString(theObject),
				LogParam_Props, LogMessage.CollectionToString<MemberInfoCollection>(props, mic => mic.ToString() )
			});
			});
		}
		protected internal virtual ObjectDictionary<object> LoadDelayedProperties(IList objects, XPMemberInfo property) {
			return LogManager.Log<ObjectDictionary<object>>(LogCategory, () => {
				return ((IObjectLayerEx)ObjectLayer).LoadDelayedProperties(this, objects, property);
				;
			}, (d) => {
				return CreateLogMessage("Executing LoadDelayedProperties()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_Objects, LogMessage.CollectionToString(objects, theObject => GetObjectString(theObject)),
				LogParam_Property, property == null ? "null" : property.Name
			});
			});
		}
		protected internal virtual Task<ObjectDictionary<object>> LoadDelayedPropertiesAsync(IList objects, XPMemberInfo property, CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync<ObjectDictionary<object>>(LogCategory, () => {
				return GetObjectLayerAsync().LoadDelayedPropertiesAsync(this, objects, property, cancellationToken);
			}, (d) => {
				return CreateLogMessage("Executing LoadDelayedPropertiesAsync()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_Objects, LogMessage.CollectionToString(objects, theObject => GetObjectString(theObject)),
				LogParam_Property, property == null ? "null" : property.Name
			});
			});
		}
		public void Reload(object theObject) {
			LogManager.Log(LogCategory, () => {
				ThrowIfObjectFromDifferentSession(theObject);
				RemoveFromLists(theObject);
				if(IsNewObject(theObject))
					return;
				XPClassInfo ci = GetClassInfo(theObject);
				if(GetObjects(ci, new BinaryOperator(new OperandProperty(ci.KeyProperty.Name), new OperandValue(theObject), BinaryOperatorType.Equal), null, 0, 1, true, true).Count == 0)
					throw new CannotLoadObjectsException(ci.FullName + "(" + GetKeyValue(theObject) + ")");
				if(ci.HasModifications(theObject)) {
					objectsMarkedSaved.Add(theObject);
				}
			}, (d) => {
				return CreateLogMessage("Executing Reload(theObject)", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_TheObject, GetObjectString(theObject)
			});
			}, null);
		}
		public Task ReloadAsync(object theObject, CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync(LogCategory, async () => {
				ThrowIfObjectFromDifferentSession(theObject);
				RemoveFromLists(theObject);
				if(IsNewObject(theObject))
					return;
				XPClassInfo ci = GetClassInfo(theObject);
				var objs = await GetObjectsAsync(ci, new BinaryOperator(new OperandProperty(ci.KeyProperty.Name), new OperandValue(theObject), BinaryOperatorType.Equal), null, 0, 1, true, true, cancellationToken);
				if(objs.Count == 0)
					throw new CannotLoadObjectsException(ci.FullName + "(" + GetKeyValue(theObject) + ")");
				if(ci.HasModifications(theObject)) {
					objectsMarkedSaved.Add(theObject);
				};
			}, (d) => {
				return CreateLogMessage("Executing ReloadAsync(theObject)", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_TheObject, GetObjectString(theObject)
			});
			}, null);
		}
		public void Reload(object theObject, bool forceAggregatesReload) {
			Reload(theObject);
			if(forceAggregatesReload) {
				foreach(XPMemberInfo mi in GetClassInfo(theObject).ObjectProperties) {
					if(mi.IsAggregated) {
						object nestedObject = mi.GetValue(theObject);
						if(nestedObject != null) {
							Reload(nestedObject, forceAggregatesReload);
						}
					}
				}
				foreach(XPMemberInfo c in GetClassInfo(theObject).AssociationListProperties) {
					if(c.IsAggregated) {
						IList collection = (IList)c.GetValue(theObject);
						if(c.IsCollection) {
							((XPBaseCollection)collection).Reload();
						}
						else if(collection is IXPUnloadableAssociationList) {
							((IXPUnloadableAssociationList)collection).Unload();
						}
						foreach(object nestedObject in ListHelper.FromCollection(collection)) {
							if(nestedObject != null) {
								Reload(nestedObject, forceAggregatesReload);
							}
						}
					}
				}
			}
		}
		public async Task ReloadAsync(object theObject, bool forceAggregatesReload, CancellationToken cancellationToken = default(CancellationToken)) {
			await ReloadAsync(theObject, cancellationToken);
			if(forceAggregatesReload) {
				foreach(XPMemberInfo mi in GetClassInfo(theObject).ObjectProperties) {
					if(mi.IsAggregated) {
						cancellationToken.ThrowIfCancellationRequested();
						object nestedObject = mi.GetValue(theObject);
						if(nestedObject != null) {
							await ReloadAsync(nestedObject, forceAggregatesReload, cancellationToken);
						}
					}
				}
				foreach(XPMemberInfo c in GetClassInfo(theObject).AssociationListProperties) {
					if(c.IsAggregated) {
						cancellationToken.ThrowIfCancellationRequested();
						IList collection = (IList)c.GetValue(theObject);
						if(c.IsCollection) {
							((XPBaseCollection)collection).Reload();
						}
						else if(collection is IXPUnloadableAssociationList) {
							((IXPUnloadableAssociationList)collection).Unload();
						}
						foreach(object nestedObject in ListHelper.FromCollection(collection)) {
							if(nestedObject != null) {
								await ReloadAsync(nestedObject, forceAggregatesReload, cancellationToken);
							}
						}
					}
				}
			}
		}
		static bool _SuppressThrowingAssociationCollectionWithDisabledLoading;
		[Description("Specifies whether exceptions are thrown when committing changes in a collection whose XPBaseCollection.LoadingEnabled property is set to false.")]
		[Obsolete("Using that property will suppress detection of some potential problems with associated collections. Use it at your own risk.")]
		public static bool SuppressThrowingAssociationCollectionWithDisabledLoading {
			get { return _SuppressThrowingAssociationCollectionWithDisabledLoading; }
			set { _SuppressThrowingAssociationCollectionWithDisabledLoading = value; }
		}
		void PreProcessSavedList() {
			ObjectSet processedItems = new ObjectSet();
			bool isObjectsToSaveCountChanged = false;
			bool? hasNewProcessedObjects = null;
			while(isObjectsToSaveCountChanged || (processedItems.Count != GetObjectsToSave().Count && (hasNewProcessedObjects == null || hasNewProcessedObjects.Value))) {
				isObjectsToSaveCountChanged = false;
				hasNewProcessedObjects = false;
				var lastObjectsToSaveCount = GetObjectsToSave().Count;
				foreach(object obj in ListHelper.FromCollection(GetObjectsToSave())) {
					if(processedItems.Contains(obj))
						continue;
					hasNewProcessedObjects = true;
					processedItems.Add(obj);
					XPClassInfo ci = GetClassInfo(obj);
					InternalBeforeSave(obj);
					TriggerObjectSaving(obj);
					if(ci.IsTypedObject)
						ci.GetMember(XPObjectType.ObjectTypePropertyName).SetValue(obj, GetObjectType(obj));
					foreach(XPMemberInfo mi in ci.ObjectProperties) {
						if(mi.IsDelayed && !XPDelayedProperty.GetDelayedPropertyContainer(obj, mi).IsLoaded)
							continue;
						object refObject = mi.GetValue(obj);
						if(refObject == null)
							continue;
						ThrowIfObjectFromDifferentSession(refObject);
						if(IsNewObject(refObject) || (mi.IsAggregated && !IsUnitOfWork)) {
							Save(refObject);
						}
					}
					foreach(XPMemberInfo mi in ci.CollectionProperties) {
						XPBaseCollection collection = (XPBaseCollection)mi.GetValue(obj);
						if(collection == null)
							continue;
						if(collection.IsLoaded && !collection.LoadingEnabled && !_SuppressThrowingAssociationCollectionWithDisabledLoading)
							throw new InvalidOperationException(Res.GetString(Res.Session_AssociationCollectionWithDisabledLoading));
						XPRefCollectionHelper.GetRefCollectionHelperChecked<XPRefCollectionHelper>(collection, obj, mi).Save();
					}
					var objectsToSaveCount = GetObjectsToSave().Count;
					if(lastObjectsToSaveCount != objectsToSaveCount) {
						isObjectsToSaveCountChanged = true;
						lastObjectsToSaveCount = objectsToSaveCount;
					}
				}
			}
			foreach(object obj in new ArrayList(GetObjectsToDelete())) {
				if(processedItems.Contains(obj))
					continue;
				processedItems.Add(obj);
				XPClassInfo ci = GetClassInfo(obj);
				foreach(XPMemberInfo mi in ci.CollectionProperties) {
					XPBaseCollection collection = (XPBaseCollection)mi.GetValue(obj);
					if(collection == null)
						continue;
					if(collection.IsLoaded && !collection.LoadingEnabled && !_SuppressThrowingAssociationCollectionWithDisabledLoading)
						throw new InvalidOperationException(Res.GetString(Res.Session_AssociationCollectionWithDisabledLoading));
					XPRefCollectionHelper.GetRefCollectionHelperChecked<XPRefCollectionHelper>(collection, obj, mi).Save();
				}
			}
		}
		async Task PreProcessSavedListAsync(CancellationToken cancellationToken) {
			ObjectSet processedItems = new ObjectSet();
			bool isObjectsToSaveCountChanged = false;
			bool? hasNewProcessedObjects = null;
			while(isObjectsToSaveCountChanged || (processedItems.Count != GetObjectsToSave().Count && (hasNewProcessedObjects == null || hasNewProcessedObjects.Value))) {
				isObjectsToSaveCountChanged = false;
				hasNewProcessedObjects = false;
				var lastObjectsToSaveCount = GetObjectsToSave().Count;
				foreach(object obj in ListHelper.FromCollection(GetObjectsToSave())) {
					if(processedItems.Contains(obj))
						continue;
					hasNewProcessedObjects = true;
					processedItems.Add(obj);
					XPClassInfo ci = GetClassInfo(obj);
					await InternalBeforeSaveAsync(obj, cancellationToken);
					TriggerObjectSaving(obj);
					if(ci.IsTypedObject)
						ci.GetMember(XPObjectType.ObjectTypePropertyName).SetValue(obj, GetObjectType(obj));
					foreach(XPMemberInfo mi in ci.ObjectProperties) {
						if(mi.IsDelayed && !XPDelayedProperty.GetDelayedPropertyContainer(obj, mi).IsLoaded)
							continue;
						object refObject = mi.GetValue(obj);
						if(refObject == null)
							continue;
						ThrowIfObjectFromDifferentSession(refObject);
						if(IsNewObject(refObject) || (mi.IsAggregated && !IsUnitOfWork)) {
							await SaveAsync(refObject, cancellationToken);
						}
					}
					foreach(XPMemberInfo mi in ci.CollectionProperties) {
						XPBaseCollection collection = (XPBaseCollection)mi.GetValue(obj);
						if(collection == null)
							continue;
						if(collection.IsLoaded && !collection.LoadingEnabled && !_SuppressThrowingAssociationCollectionWithDisabledLoading)
							throw new InvalidOperationException(Res.GetString(Res.Session_AssociationCollectionWithDisabledLoading));
						XPRefCollectionHelper.GetRefCollectionHelperChecked<XPRefCollectionHelper>(collection, obj, mi).Save();
					}
					var objectsToSaveCount = GetObjectsToSave().Count;
					if(lastObjectsToSaveCount != objectsToSaveCount) {
						isObjectsToSaveCountChanged = true;
						lastObjectsToSaveCount = objectsToSaveCount;
					}
				}
			}
			foreach(object obj in new ArrayList(GetObjectsToDelete())) {
				if(processedItems.Contains(obj))
					continue;
				processedItems.Add(obj);
				XPClassInfo ci = GetClassInfo(obj);
				foreach(XPMemberInfo mi in ci.CollectionProperties) {
					XPBaseCollection collection = (XPBaseCollection)mi.GetValue(obj);
					if(collection == null)
						continue;
					if(collection.IsLoaded && !collection.LoadingEnabled && !_SuppressThrowingAssociationCollectionWithDisabledLoading)
						throw new InvalidOperationException(Res.GetString(Res.Session_AssociationCollectionWithDisabledLoading));
					XPRefCollectionHelper.GetRefCollectionHelperChecked<XPRefCollectionHelper>(collection, obj, mi).Save();
				}
			}
		}
		void ProcessingProcessNextObject(ObjectSet markedObjectsHolder, object theObject) {
			ThrowIfObjectFromDifferentSession(theObject);
			XPClassInfo ci = GetClassInfo(theObject);
			ci.CheckAbstractReference();
			if(!markedObjectsHolder.Contains(theObject)) {
				SessionStateStack.ThrowIfCantEnter(this, SessionState.SessionObjectsToSaveOrDeleteChanging);
				markedObjectsHolder.Add(theObject);
				PersistentBase pb = theObject as PersistentBase;
				if(pb != null)
					pb.OnSessionProcessingProcessProcessed();
			}
		}
		void ProcessingProcessObjectListOrCollection(ObjectSet markedObjectsHolder, object theObjectOrCollection) {
			if(theObjectOrCollection is ICollection) {
				foreach(object obj in (ICollection)theObjectOrCollection)
					ProcessingProcessNextObject(markedObjectsHolder, obj);
			}
			else
				ProcessingProcessNextObject(markedObjectsHolder, theObjectOrCollection);
		}
		void ProcessingProcess(ObjectSet markedObjectsHolder, object theObject) {
			if(!TrackingChanges) {
				BeginTrackingChanges();
				if(IsUnitOfWork) {
					ProcessingProcessObjectListOrCollection(markedObjectsHolder, theObject);
				}
				else {
					try {
						ProcessingProcessObjectListOrCollection(markedObjectsHolder, theObject);
						FlushChanges();
					}
					catch(Exception e) {
						if(TrackingChanges) {
							try {
								DropChanges();
							}
							catch(Exception e2) {
								throw new ExceptionBundleException(e, e2);
							}
						}
						throw;
					}
				}
			}
			else {
				ProcessingProcessObjectListOrCollection(markedObjectsHolder, theObject);
			}
		}
		async Task ProcessingProcessAsync(ObjectSet markedObjectsHolder, object theObject, CancellationToken cancellationToken) {
			if(!TrackingChanges) {
				BeginTrackingChanges();
				if(IsUnitOfWork) {
					ProcessingProcessObjectListOrCollection(markedObjectsHolder, theObject);
				}
				else {
					try {
						ProcessingProcessObjectListOrCollection(markedObjectsHolder, theObject);
						await FlushChangesAsync(cancellationToken);
					}
					catch(Exception e) {
						if(TrackingChanges) {
							try {
								DropChanges();
							}
							catch(Exception e2) {
								throw new ExceptionBundleException(e, e2);
							}
						}
						throw;
					}
				}
			}
			else {
				ProcessingProcessObjectListOrCollection(markedObjectsHolder, theObject);
			}
		}
		public void Save(object theObject) {
			ThrowIfCommitChangesToDataLayerInner();
			ProcessingProcess(objectsMarkedSaved, theObject);
		}
		public Task SaveAsync(object theObject, CancellationToken cancellationToken = default(CancellationToken)) {
			ThrowIfCommitChangesToDataLayerInner();
			return ProcessingProcessAsync(objectsMarkedSaved, theObject, cancellationToken);
		}
		public void Save(ICollection objects) {
			this.Save((object)objects);
		}
		ObjectSet objectsMarkedSaved = new ObjectSet();
		ObjectSet objectsMarkedDeleted = new ObjectSet();
		internal Dictionary<XPRefCollectionHelper, object> collectionsMarkedSaved = new Dictionary<XPRefCollectionHelper, object>();
		static int NextGCRecordValue() {
			return NonCryptographicRandom.System.Next(1, int.MaxValue);
		}
		void DeleteObject(object theObject) {
			if(theObject == null)
				return;
			XPClassInfo classInfo = GetClassInfo(theObject);
			if(classInfo.IsGCRecordObject) {
				XPMemberInfo gcRecord = classInfo.GetMember(GCRecordField.StaticName);
				if(gcRecord.GetValue(theObject) != null)
					return; 
				if(this is ExplicitUnitOfWork) {
					LoadAggregatedMembers(classInfo, theObject);
				}
				TriggerObjectDeleting(theObject);
				gcRecord.SetValue(theObject, NextGCRecordValue());
				DeleteCore(classInfo, theObject);
				TriggerObjectDeleted(theObject);
				Save(theObject);
			}
			else {
				if(IsObjectToDelete(theObject))
					return;
				if(this is ExplicitUnitOfWork) {
					LoadAggregatedMembers(classInfo, theObject);
				}
				TriggerObjectDeleting(theObject);
				ProcessingProcess(objectsMarkedDeleted, theObject);
				DeleteCore(classInfo, theObject);
				TriggerObjectDeleted(theObject);
			}
		}
		async Task DeleteObjectAsync(object theObject, CancellationToken cancellationToken) {
			if(theObject == null)
				return;
			XPClassInfo classInfo = GetClassInfo(theObject);
			if(classInfo.IsGCRecordObject) {
				XPMemberInfo gcRecord = classInfo.GetMember(GCRecordField.StaticName);
				if(gcRecord.GetValue(theObject) != null)
					return; 
				if(this is ExplicitUnitOfWork) {
					await LoadAggregatedMembersAsync(classInfo, theObject, cancellationToken);
				}
				TriggerObjectDeleting(theObject);
				gcRecord.SetValue(theObject, NextGCRecordValue());
				await DeleteCoreAsync(classInfo, theObject, cancellationToken);
				TriggerObjectDeleted(theObject);
				await SaveAsync(theObject, cancellationToken);
			}
			else {
				if(IsObjectToDelete(theObject))
					return;
				if(this is ExplicitUnitOfWork) {
					await LoadAggregatedMembersAsync(classInfo, theObject, cancellationToken);
				}
				TriggerObjectDeleting(theObject);
				await ProcessingProcessAsync(objectsMarkedDeleted, theObject, cancellationToken);
				await DeleteCoreAsync(classInfo, theObject, cancellationToken);
				TriggerObjectDeleted(theObject);
			}
		}
		void DeleteObjectOrCollection(object theObject) {
			ICollection collection = theObject as ICollection;
			if(collection == null) {
				DeleteObject(theObject);
			}
			else {
				foreach(object obj in ListHelper.FromCollection(collection))
					DeleteObject(obj);
			}
		}
		async Task DeleteObjectOrCollectionAsync(object theObject, CancellationToken cancellationToken) {
			ICollection collection = theObject as ICollection;
			if(collection == null) {
				await DeleteObjectAsync(theObject, cancellationToken);
			}
			else {
				foreach(object obj in ListHelper.FromCollection(collection))
					await DeleteObjectAsync(obj, cancellationToken);
			}
		}
		void DeleteCore(XPClassInfo classInfo, object theObject) {
			foreach(XPMemberInfo mi in classInfo.AssociationListProperties) {
				if(mi.IsAggregated) {
					if(mi.IsCollection) {
						XPBaseCollection collection = (XPBaseCollection)mi.GetValue(theObject);
						CheckFilteredAggregateDeletion(theObject, mi, collection);
						using(var toDelete = LohPooled.ToListForDispose<object>(collection.Cast<object>(), collection.Count)) {
							for(int i = toDelete.Count - 1; i >= 0; i--) {
								Delete(toDelete[i]);
							}
							if(!collection.SelectDeleted) {
								for(int i = toDelete.Count - 1; i >= 0; i--) {
									collection.BaseRemove(toDelete[i]);
								}
							}
						}
					}
					else {
						IList list = (IList)mi.GetValue(theObject);
						using(var toDelete = LohPooled.ToListForDispose<object>(list.Cast<object>(), list.Count)) {
							for(int i = toDelete.Count - 1; i >= 0; i--) {
								Delete(toDelete[i]);
							}
						}
					}
				}
				else
					if(mi.IsManyToMany) {
					XPBaseCollection collection = (XPBaseCollection)mi.GetValue(theObject);
					if(!collection.SelectDeleted) {
						using(var toDelete = LohPooled.ToListForDispose<object>(collection.Cast<object>(), collection.Count)) {
							for(int i = toDelete.Count - 1; i >= 0; i--) {
								collection.BaseRemove(toDelete[i]);
							}
						}
					}
				}
			}
			foreach(XPMemberInfo mi in classInfo.ObjectProperties) {
				if(mi.IsAggregated) {
					object aggregated = mi.GetValue(theObject);
					Delete(aggregated);
				}
				if(mi.IsAssociation) {
					object associatedObject = mi.GetValue(theObject);
					if(associatedObject != null) {
						XPMemberInfo assocList = mi.GetAssociatedMember();
						IList associatedCollection = (IList)assocList.GetValue(associatedObject);
						if(associatedCollection != null) {
							associatedCollection.Remove(theObject);
						}
					}
				}
			}
		}
		async Task DeleteCoreAsync(XPClassInfo classInfo, object theObject, CancellationToken cancellationToken) {
			foreach(XPMemberInfo mi in classInfo.AssociationListProperties) {
				if(mi.IsAggregated) {
					if(mi.IsCollection) {
						XPBaseCollection collection = (XPBaseCollection)mi.GetValue(theObject);
						CheckFilteredAggregateDeletion(theObject, mi, collection);
						if(!collection.IsLoaded) {
							await collection.LoadAsync(cancellationToken);
						}
						using(var toDelete = LohPooled.ToListForDispose<object>(collection.Cast<object>(), collection.Count)) {
							for(int i = toDelete.Count - 1; i >= 0; i--) {
								await DeleteAsync(toDelete[i], cancellationToken);
							}
							if(!collection.SelectDeleted) {
								for(int i = toDelete.Count - 1; i >= 0; i--) {
									await collection.BaseRemoveAsync(toDelete[i], cancellationToken);
								}
							}
						}
					}
					else {
						IList list = (IList)mi.GetValue(theObject);
						await LoadAssociationListAsync(list, cancellationToken);
						using(var toDelete = LohPooled.ToListForDispose<object>(list.Cast<object>(), list.Count)) {
							for(int i = toDelete.Count - 1; i >= 0; i--) {
								await DeleteAsync(toDelete[i], cancellationToken);
							}
						}
					}
				}
				else
					if(mi.IsManyToMany) {
					XPBaseCollection collection = (XPBaseCollection)mi.GetValue(theObject);
					if(!collection.SelectDeleted) {
						if(!collection.IsLoaded) {
							await collection.LoadAsync(cancellationToken);
						}
						using(var toDelete = LohPooled.ToListForDispose<object>(collection.Cast<object>(), collection.Count)) {
							for(int i = toDelete.Count - 1; i >= 0; i--) {
								await collection.BaseRemoveAsync(toDelete[i], cancellationToken);
							}
						}
					}
				}
			}
			foreach(XPMemberInfo mi in classInfo.ObjectProperties) {
				if(mi.IsAggregated) {
					object aggregated = mi.GetValue(theObject);
					Delete(aggregated);
				}
				if(mi.IsAssociation) {
					object associatedObject = mi.GetValue(theObject);
					if(associatedObject != null) {
						XPMemberInfo assocList = mi.GetAssociatedMember();
						IList associatedCollection = (IList)assocList.GetValue(associatedObject);
						await LoadAssociationListAsync(associatedCollection, cancellationToken);
						if(associatedCollection != null) {
							associatedCollection.Remove(theObject);
						}
					}
				}
			}
		}
		void LoadAggregatedMembers(XPClassInfo classInfo, object theObject) {
			foreach(XPMemberInfo mi in classInfo.AssociationListProperties) {
				if(mi.IsAggregated) {
					if(mi.IsCollection) {
						XPBaseCollection collection = (XPBaseCollection)mi.GetValue(theObject);
						if(!collection.IsLoaded) {
							collection.Load();
						}
					}
					else {
						IList list = (IList)mi.GetValue(theObject);
						list.GetEnumerator();
					}
				}
				else {
					if(mi.IsManyToMany) {
						XPBaseCollection collection = (XPBaseCollection)mi.GetValue(theObject);
						if(!collection.IsLoaded) {
							collection.Load();
						}
					}
				}
			}
			foreach(XPMemberInfo mi in classInfo.ObjectProperties) {
				if(mi.IsAggregated) {
					mi.GetValue(theObject);
				}
				if(mi.IsAssociation) {
					object associatedObject = mi.GetValue(theObject);
					if(associatedObject != null) {
						XPMemberInfo assocList = mi.GetAssociatedMember();
						IList associatedCollection = (IList)assocList.GetValue(associatedObject);
						if(associatedCollection != null) {
							associatedCollection.GetEnumerator();
						}
					}
				}
			}
		}
		async Task LoadAggregatedMembersAsync(XPClassInfo classInfo, object theObject, CancellationToken cancellationToken) {
			foreach(XPMemberInfo mi in classInfo.AssociationListProperties) {
				if(mi.IsAggregated) {
					if(mi.IsCollection) {
						XPBaseCollection collection = (XPBaseCollection)mi.GetValue(theObject);
						if(!collection.IsLoaded) {
							await collection.LoadAsync(cancellationToken);
						}
					}
					else {
						object list = mi.GetValue(theObject);
						await LoadAssociationListAsync(list, cancellationToken);
					}
				}
				else {
					if(mi.IsManyToMany) {
						XPBaseCollection collection = (XPBaseCollection)mi.GetValue(theObject);
						if(!collection.IsLoaded) {
							await collection.LoadAsync(cancellationToken);
						}
					}
				}
			}
			foreach(XPMemberInfo mi in classInfo.ObjectProperties) {
				if(mi.IsAggregated) {
					mi.GetValue(theObject);
				}
				if(mi.IsAssociation) {
					object associatedObject = mi.GetValue(theObject);
					if(associatedObject != null) {
						XPMemberInfo assocList = mi.GetAssociatedMember();
						object associatedCollection = assocList.GetValue(associatedObject);
						if(associatedCollection != null) {
							await LoadAssociationListAsync(associatedCollection, cancellationToken);
						}
					}
				}
			}
		}
		async Task LoadAssociationListAsync(object list, CancellationToken cancellationToken) {
			var xpAssocList = list as XPAssociationList;
			if(xpAssocList != null) {
				await xpAssocList.LoadAsync(cancellationToken);
			}
			else {
				var xpManyToMany = list as XPManyToManyAliasList;
				if(xpManyToMany != null) {
					await xpManyToMany.EnumerateAsync(cancellationToken);
				}
				else {
					((IList)list).GetEnumerator();
				}
			}
		}
		static void CheckFilteredAggregateDeletion(object theObject, XPMemberInfo mi, XPBaseCollection collection) {
			if(!ReferenceEquals(null, collection.Criteria) || collection.SkipReturnedObjects > 0 || collection.TopReturnedObjects > 0) {
				var message = Res.GetString(Res.Collections_WantNotDeleteFilteredAggregateCollection, theObject, mi.Name);
				bool exceptionWouldBeReallyThrown = collection.GetObjectClassInfo().IsGCRecordObject || mi.GetAssociatedMember().HasAttribute(typeof(NoForeignKeyAttribute));
				List<string> causes = new List<string>();
				if(!ReferenceEquals(null, collection.Criteria)) {
					string criteriaCause = null;
					if(exceptionWouldBeReallyThrown) {
						try {
							criteriaCause = ".Criteria = '" + CriteriaOperator.ToString(collection.Criteria) + "'";
						}
						catch { }
					}
					causes.Add(criteriaCause ?? ".Criteria");
				}
				if(collection.SkipReturnedObjects > 0) {
					causes.Add(".SkipReturnedObjects = " + collection.SkipReturnedObjects);
				}
				if(collection.TopReturnedObjects > 0) {
					causes.Add(".TopReturnedObjects = " + collection.TopReturnedObjects);
				}
				if(causes.Count > 0)
					message += " (" + string.Join(", ", causes) + ")";
				InvalidOperationException ex = new InvalidOperationException(message);
				if(exceptionWouldBeReallyThrown) {
					throw ex;
				}
				else {
					try {
						throw ex;
					}
					catch { }
				}
			}
		}
		[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
		public void RemoveFromDeleteList(object theObject) {
			if(theObject == null)
				return;
			objectsMarkedDeleted.Remove(theObject);
			((IInfrastructure<ISecuredPropertyAccessor>)this).Instance?.ClearChangedSecuredProperties(theObject);
		}
		[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
		public void RemoveFromSaveList(object theObject) {
			if(theObject == null)
				return;
			objectsMarkedSaved.Remove(theObject);
			((IInfrastructure<ISecuredPropertyAccessor>)this).Instance?.ClearChangedSecuredProperties(theObject);
		}
		[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
		public void RemoveFromLists(object theObject) {
			RemoveFromDeleteList(theObject);
			RemoveFromSaveList(theObject);
		}
		public void Delete(object theObject) {
			LogManager.Log(LogCategory, () => {
				ThrowIfCommitChangesToDataLayerInner();
				if(TrackingChanges) {
					DeleteObjectOrCollection(theObject);
				}
				else {
					BeginTrackingChanges();
					try {
						DeleteObjectOrCollection(theObject);
						if(!IsUnitOfWork) {
							FlushChanges();
						}
					}
					catch(Exception e) {
						if(TrackingChanges) {
							try {
								DropChanges();
							}
							catch(Exception e2) {
								throw new ExceptionBundleException(e, e2);
							}
						}
						throw;
					}
				}
			}, (d) => {
				return CreateLogMessage("Executing Delete(theObject)", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_TheObject, GetObjectString(theObject)
			});
			}, null);
		}
		public Task DeleteAsync(object theObject, CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync(LogCategory, async () => {
				ThrowIfCommitChangesToDataLayerInner();
				if(TrackingChanges) {
					await DeleteObjectOrCollectionAsync(theObject, cancellationToken);
				}
				else {
					BeginTrackingChanges();
					try {
						await DeleteObjectOrCollectionAsync(theObject, cancellationToken);
						if(!IsUnitOfWork) {
							await FlushChangesAsync(cancellationToken);
						}
					}
					catch(Exception e) {
						if(TrackingChanges) {
							try {
								DropChanges();
							}
							catch(Exception e2) {
								throw new ExceptionBundleException(e, e2);
							}
						}
						throw;
					}
				}
			}, (d) => {
				return CreateLogMessage("Executing DeleteAsync(theObject)", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_TheObject, GetObjectString(theObject)
			});
			}, null);
		}
		public void Delete(ICollection objects) {
			this.Delete((object)objects);
		}
		public Task DeleteAsync(ICollection objects, CancellationToken cancellationToken = default(CancellationToken)) {
			return this.DeleteAsync((object)objects, cancellationToken);
		}
		public PurgeResult PurgeDeletedObjects() {
			return PurgeDeletedObjects(ObjectLayer);
		}
		public static PurgeResult PurgeDeletedObjects(IObjectLayer objectLayer) {
			return LogManager.Log<PurgeResult>(LogCategory, () => {
				return objectLayer.Purge();
			}, (d) => {
				return new LogMessage(LogMessageType.SessionEvent, "Executing PurgeDeletedObjects()", d);
			});
		}
		void InternalBeforeSave(object theObject) {
			XPClassInfo classInfo = GetClassInfo(theObject);
			classInfo.CheckAbstractReference();
			XPMemberInfo key = classInfo.KeyProperty; 
			if(classInfo.IsTypedObject || classInfo.IsGCRecordObject || classInfo.HasPurgebleObjectReferences())
				GetObjectType(classInfo);
		}
		Task InternalBeforeSaveAsync(object theObject, CancellationToken cancellationToken) {
			XPClassInfo classInfo = GetClassInfo(theObject);
			classInfo.CheckAbstractReference();
			XPMemberInfo key = classInfo.KeyProperty; 
			if(classInfo.IsTypedObject || classInfo.IsGCRecordObject || classInfo.HasPurgebleObjectReferences()) {
				return GetObjectTypeAsync(classInfo, cancellationToken);
			}
			return Task.FromResult(0);
		}
		internal void CheckDuplicateObjectInIdentityMap(object theObject) {
			XPClassInfo ci = GetClassInfo(theObject);
			object cachedObj = SessionIdentityMap.GetLoadedObjectByKey(this, ci, ci.GetId(theObject));
			if(cachedObj != null && !object.ReferenceEquals(cachedObj, theObject))
				throw new DifferentObjectsWithSameKeyException();
		}
		internal void RegisterInsertedObject(object theObject) {
			SessionIdentityMap.RegisterObject(this, theObject, Dictionary.GetId(theObject));
		}
		[Obsolete("Please use DataLayer.SchemaInit event handler")]
		[Description("Occurs after database schema is initialized or updated")]
		[EditorBrowsable(EditorBrowsableState.Never), Browsable(false)]
		public event SchemaInitEventHandler SchemaInit {
			add { ((IObjectLayerEx)ObjectLayer).SchemaInit += value; }
			remove { ((IObjectLayerEx)ObjectLayer).SchemaInit -= value; }
		}
		protected XPDictionary dict;
		bool isDisposed = false;
		protected override void Dispose(bool disposing) {
			isDisposed = true;
			if(disposing) {
				_AsyncLockSemaphore.Dispose();
				Disconnect();
			}
			else if(IsConnected) {
				if(Environment.OSVersion.Platform == PlatformID.Win32NT) {
					PerformanceCounters.SessionCount.Decrement();
					PerformanceCounters.SessionDisconnected.Increment();
				}
			}
			base.Dispose(disposing);
		}
		void TriggerObjectsSaved(ICollection objects) {
			OnObjectsSaved(new ObjectsManipulationEventArgs(this, objects));
			foreach(object theObject in objects)
				TriggerObjectSaved(theObject);
		}
		protected virtual void OnObjectsSaved(ObjectsManipulationEventArgs e) {
			if(ObjectsSaved != null)
				ObjectsSaved(this, e);
		}
		internal void TriggerObjectsLoaded(ICollection objects) {
			OnObjectsLoaded(new ObjectsManipulationEventArgs(this, objects));
			foreach(object theObject in objects)
				TriggerObjectLoaded(theObject);
			UnMuteCollections();
		}
		protected virtual void OnObjectsLoaded(ObjectsManipulationEventArgs e) {
			if(ObjectsLoaded != null)
				ObjectsLoaded(this, e);
		}
		internal void TriggerObjectSaving(object theObject) {
			OnObjectSaving(new ObjectManipulationEventArgs(this, theObject));
			IXPObject xpObj = theObject as IXPObject;
			if(xpObj != null) {
				var accessor = ((IInfrastructure<ISecuredPropertyAccessor>)this).Instance;
				accessor?.EnterObjectSaving(xpObj);
				try {
					xpObj.OnSaving();
				}
				finally {
					accessor?.ExitObjectSaving(xpObj);
				}
			}
		}
		protected virtual void OnObjectSaving(ObjectManipulationEventArgs e) {
			if(ObjectSaving != null)
				ObjectSaving(this, e);
		}
		void TriggerObjectSaved(object theObject) {
			IXPObject xpObj = theObject as IXPObject;
			if(xpObj != null)
				xpObj.OnSaved();
			OnObjectSaved(new ObjectManipulationEventArgs(this, theObject));
		}
		protected virtual void OnObjectSaved(ObjectManipulationEventArgs e) {
			if(ObjectSaved != null)
				ObjectSaved(this, e);
		}
		internal void TriggerObjectLoading(object theObject) {
			OnObjectLoading(new ObjectManipulationEventArgs(this, theObject));
			IXPObject xpObj = theObject as IXPObject;
			if(xpObj != null)
				xpObj.OnLoading();
		}
		protected virtual void OnObjectLoading(ObjectManipulationEventArgs e) {
			if(ObjectLoading != null)
				ObjectLoading(this, e);
		}
		void TriggerObjectLoaded(object theObject) {
			IXPObject xpObj = theObject as IXPObject;
			if(xpObj != null)
				xpObj.OnLoaded();
			OnObjectLoaded(new ObjectManipulationEventArgs(this, theObject));
		}
		protected virtual void OnObjectLoaded(ObjectManipulationEventArgs e) {
			if(ObjectLoaded != null)
				ObjectLoaded(this, e);
		}
		internal void TriggerObjectDeleting(object theObject) {
			OnObjectDeleting(new ObjectManipulationEventArgs(this, theObject));
			IXPObject xpObj = theObject as IXPObject;
			if(xpObj != null)
				xpObj.OnDeleting();
		}
		protected virtual void OnObjectDeleting(ObjectManipulationEventArgs e) {
			if(ObjectDeleting != null)
				ObjectDeleting(this, e);
		}
		internal void TriggerObjectDeleted(object theObject) {
			IXPObject xpObj = theObject as IXPObject;
			if(xpObj != null)
				xpObj.OnDeleted();
			OnObjectDeleted(new ObjectManipulationEventArgs(this, theObject));
		}
		protected virtual void OnObjectDeleted(ObjectManipulationEventArgs e) {
			if(ObjectDeleted != null)
				ObjectDeleted(this, e);
		}
		protected virtual void OnBeforeConnect() {
			if(BeforeConnect != null)
				BeforeConnect(this, new SessionManipulationEventArgs(this));
		}
		protected virtual void OnAfterConnect() {
			if(AfterConnect != null)
				AfterConnect(this, new SessionManipulationEventArgs(this));
		}
		protected virtual void OnBeforeDisconnect() {
			if(BeforeDisconnect != null)
				BeforeDisconnect(this, new SessionManipulationEventArgs(this));
		}
		protected virtual void OnAfterDisconnect() {
			if(AfterDisconnect != null)
				AfterDisconnect(this, new SessionManipulationEventArgs(this));
		}
		protected virtual void OnBeforeBeginTransaction() {
			if(BeforeBeginTransaction != null)
				BeforeBeginTransaction(this, new SessionManipulationEventArgs(this));
		}
		protected virtual void OnAfterBeginTransaction() {
			if(AfterBeginTransaction != null)
				AfterBeginTransaction(this, new SessionManipulationEventArgs(this));
		}
		[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
		protected virtual void OnBeforePreProcessCommitedList() {
			if(BeforePreProcessCommitedList != null)
				BeforePreProcessCommitedList(this, new SessionManipulationEventArgs(this));
		}
		protected virtual void OnBeforeCommitTransaction() {
			if(BeforeCommitTransaction != null)
				BeforeCommitTransaction(this, new SessionManipulationEventArgs(this));
		}
		protected virtual void OnAfterCommitTransaction() {
			if(AfterCommitTransaction != null)
				AfterCommitTransaction(this, new SessionManipulationEventArgs(this));
		}
		protected internal virtual void OnBeforeCommitNestedUnitOfWork(SessionManipulationEventArgs e) {
			if(BeforeCommitNestedUnitOfWork != null)
				BeforeCommitNestedUnitOfWork(this, e);
		}
		protected internal virtual void OnAfterCommitNestedUnitOfWork(SessionManipulationEventArgs e) {
			if(AfterCommitNestedUnitOfWork != null)
				AfterCommitNestedUnitOfWork(this, e);
		}
		protected virtual void OnBeforeRollbackTransaction() {
			if(BeforeRollbackTransaction != null)
				BeforeRollbackTransaction(this, new SessionManipulationEventArgs(this));
		}
		protected virtual void OnAfterRollbackTransaction() {
			if(AfterRollbackTransaction != null)
				AfterRollbackTransaction(this, new SessionManipulationEventArgs(this));
		}
		protected virtual void OnBeforeBeginTrackingChanges() {
			OnBeforeBeginTransaction();
			OnBeforeBeginTrackingChangesInternal();
		}
		protected void OnBeforeBeginTrackingChangesInternal() {
			if(BeforeBeginTrackingChanges != null)
				BeforeBeginTrackingChanges(this, new SessionManipulationEventArgs(this));
		}
		protected virtual void OnAfterBeginTrackingChanges() {
			OnAfterBeginTrackingChangesInternal();
			OnAfterBeginTransaction();
		}
		protected void OnAfterBeginTrackingChangesInternal() {
			if(AfterBeginTrackingChanges != null)
				AfterBeginTrackingChanges(this, new SessionManipulationEventArgs(this));
		}
		protected virtual void OnBeforeFlushChanges() {
			OnBeforeCommitTransaction();
			OnBeforeFlushChangesInternal();
		}
		protected void OnBeforeFlushChangesInternal() {
			if(BeforeFlushChanges != null)
				BeforeFlushChanges(this, new SessionManipulationEventArgs(this));
		}
		protected virtual void OnAfterFlushChanges() {
			OnAfterFlushChangesInternal();
			OnAfterCommitTransaction();
		}
		protected void OnAfterFlushChangesInternal() {
			if(AfterFlushChanges != null)
				AfterFlushChanges(this, new SessionManipulationEventArgs(this));
		}
		protected virtual void OnBeforeDropChanges() {
			OnBeforeRollbackTransaction();
			OnBeforeDropChangesInternal();
		}
		protected void OnBeforeDropChangesInternal() {
			if(BeforeDropChanges != null)
				BeforeDropChanges(this, new SessionManipulationEventArgs(this));
		}
		protected virtual void OnAfterDropChanges() {
			OnAfterDropChangesInternal();
			OnAfterRollbackTransaction();
		}
		protected void OnAfterDropChangesInternal() {
			if(AfterDropChanges != null)
				AfterDropChanges(this, new SessionManipulationEventArgs(this));
		}
		protected virtual bool OnFailedCommitTransaction(Exception ex) {
			SessionOperationFailEventArgs args = new SessionOperationFailEventArgs(ex);
			if(FailedCommitTransaction != null)
				FailedCommitTransaction(this, args);
			return args.Handled;
		}
		protected virtual bool OnFailedFlushChanges(Exception ex) {
			return OnFailedFlushChangesInternal(ex) || OnFailedCommitTransaction(ex);
		}
		protected bool OnFailedFlushChangesInternal(Exception ex) {
			SessionOperationFailEventArgs args = new SessionOperationFailEventArgs(ex);
			if(FailedFlushChanges != null)
				FailedFlushChanges(this, args);
			return args.Handled;
		}
		protected internal void OnAfterDropIdentityMap() {
			if(AfterDropIdentityMap != null)
				AfterDropIdentityMap(this, new SessionManipulationEventArgs(this));
		}
		bool? isObjectModifiedOnNonPersistentPropertyChange;
		[Description("Specifies whether or not a persistent object is marked modified when its non-persistent property value is changed.")]
		[Category("Options")]
		public bool? IsObjectModifiedOnNonPersistentPropertyChange {
			get { return isObjectModifiedOnNonPersistentPropertyChange; }
			set { isObjectModifiedOnNonPersistentPropertyChange = value; }
		}
		void ResetIsObjectModifiedOnNonPersistentPropertyChange() {
			isObjectModifiedOnNonPersistentPropertyChange = null;
		}
		bool ShouldSerializeIsObjectModifiedOnNonPersistentPropertyChange() {
			return isObjectModifiedOnNonPersistentPropertyChange.HasValue;
		}
		[EditorBrowsable(EditorBrowsableState.Never)]
		public void TriggerObjectChanged(object changedObject, ObjectChangeEventArgs e) {
			if(e.Reason == ObjectChangeReason.PropertyChanged) {
				XPClassInfo ci = GetClassInfo(changedObject);
				XPMemberInfo mi = null;
				bool valueChanged = !ReferenceEquals(e.OldValue, e.NewValue);
				if(e.PropertyName != null && e.PropertyName.Length > 0) {
					if(valueChanged) {
						mi = ci.FindMember(e.PropertyName);
						if(mi != null) {
							if(ci.TrackPropertiesModifications ?? TrackPropertiesModifications)
								mi.SetModified(changedObject, e.OldValue);
						}
					}
					else if(e.OldValue == null && e.NewValue == null) {
						mi = ci.FindMember(e.PropertyName);
					}
				}
				if(this.IsUnitOfWork && ci.IsPersistent &&
					(mi == null || mi.IsPersistent
					|| (!mi.IsPersistent && (IsObjectModifiedOnNonPersistentPropertyChange ?? XpoDefault.IsObjectModifiedOnNonPersistentPropertyChange)))) {
					Save(changedObject);
				}
				OnObjectChanged(changedObject, e);
				if(valueChanged && mi != null) {
					mi.ProcessAssociationRefChange(this, changedObject, e.OldValue, e.NewValue);
				}
			}
			else {
				OnObjectChanged(changedObject, e);
			}
		}
		protected virtual void OnObjectChanged(object changedObject, ObjectChangeEventArgs e) {
			if(ObjectChanged != null)
				ObjectChanged(changedObject, e);
		}
		[Description("Gets the pre-initialized default session for simple applications.")]
		public static Session DefaultSession {
			get {
				return XpoDefault.Session;
			}
		}
		[Obsolete("Use SessionIdentityMap.Extract method instead", true), EditorBrowsable(EditorBrowsableState.Never)]
		protected SessionIdentityMap Cache {
			get {
				return SessionIdentityMap.Extract(this);
			}
		}
		void SessionInitCommon() {
			this._connectionString = string.Empty;
			this._connection = null;
			this.closeConnectionOnDisconnect = false;
		}
		void SessionInitDisconnected(XPDictionary dictionary) {
			this.objectLayer = null;
			this.dict = dictionary;
			this._DisposeOnDisconnect = null;
			SessionInitCommon();
		}
		void SessionInitConnected(IObjectLayer layer, IDisposable[] disposeOnDisconnect) {
			if(layer == null)
				throw new ArgumentNullException(nameof(layer));
			OnBeforeConnect();
			this._DisposeOnDisconnect = disposeOnDisconnect;
			this.objectLayer = layer;
			this.dict = this.objectLayer.Dictionary;
			SessionInitCommon();
			OnAfterConnect();
		}
		public Session()
			: this((IServiceProvider)null) {
		}
		public Session(IServiceProvider serviceProvider)
			: this(serviceProvider, (XPDictionary)null) {
		}
		public Session(IContainer container)
			: this(null, container) {
		}
		public Session(IServiceProvider serviceProvider, IContainer container)
			: this(serviceProvider) {
			container.Add(this);
		}
		public Session(XPDictionary dictionary)
			: this(null, dictionary) {
		}
		public Session(IServiceProvider serviceProvider, XPDictionary dictionary)
			: base() {
			this.serviceProvider = serviceProvider;
			SessionInitDisconnected(dictionary);
		}
		public Session(IDataLayer layer, params IDisposable[] disposeOnDisconnect)
			: this(null, layer, disposeOnDisconnect) {
		}
		public Session(IServiceProvider serviceProvider, IDataLayer layer, params IDisposable[] disposeOnDisconnect)
			: this(serviceProvider, SimpleObjectLayer.FromDataLayer(layer), disposeOnDisconnect) {
		}
		public Session(IObjectLayer layer, params IDisposable[] disposeOnDisconnect)
			: this(null, layer, disposeOnDisconnect) {
		}
		public Session(IServiceProvider serviceProvider, IObjectLayer layer, params IDisposable[] disposeOnDisconnect)
			: base() {
			this.serviceProvider = serviceProvider;
			SessionInitConnected(layer, disposeOnDisconnect);
			if(Environment.OSVersion.Platform == PlatformID.Win32NT) {
				PerformanceCounters.SessionCount.Increment();
				PerformanceCounters.SessionConnected.Increment();
			}
		}
		public object GetLoadedObjectByKey(Type classType, object id) {
			return GetLoadedObjectByKey(Dictionary.GetClassInfo(classType), id);
		}
		public object GetLoadedObjectByKey(XPClassInfo classInfo, object id) {
			if(classInfo == null)
				throw new ArgumentNullException(LogParam_ClassInfo);
			if(classInfo.KeyProperty == null)
				classInfo.CheckAbstractReference();
			if(id == null)
				return null;
			if(!classInfo.KeyProperty.MemberType.IsAssignableFrom(id.GetType()) && !((typeof(ArrayList).IsAssignableFrom(id.GetType()) ||
 typeof(List<object>).IsAssignableFrom(id.GetType())) && classInfo.KeyProperty.IsStruct) && !(classInfo.KeyProperty.ReferenceType != null && classInfo.KeyProperty.ReferenceType.KeyProperty.MemberType.IsAssignableFrom(id.GetType())))
				throw new ArgumentException(Res.GetString(Res.Session_IncompatibleIdType, id.GetType().FullName, classInfo.FullName, classInfo.KeyProperty.Name, classInfo.KeyProperty.MemberType.FullName));
			return SessionIdentityMap.GetLoadedObjectByKey(this, classInfo, classInfo.KeyProperty.ExpandId(id));
		}
		public object GetObjectByKey(Type classType, object id) {
			return GetObjectByKey(Dictionary.GetClassInfo(classType), id);
		}
		public object GetObjectByKey(XPClassInfo classInfo, object id) {
			return GetObjectByKey(classInfo, id, false);
		}
		public object GetObjectByKey(Type classType, object id, bool alwaysGetFromDataStore) {
			return GetObjectByKey(Dictionary.GetClassInfo(classType), id, alwaysGetFromDataStore);
		}
		public Task<object> GetObjectByKeyAsync(Type classType, object id, CancellationToken cancellationToken = default(CancellationToken)) {
			return GetObjectByKeyAsync(Dictionary.GetClassInfo(classType), id, cancellationToken);
		}
		public Task<object> GetObjectByKeyAsync(XPClassInfo classInfo, object id, CancellationToken cancellationToken = default(CancellationToken)) {
			return GetObjectByKeyAsync(classInfo, id, false, cancellationToken);
		}
		public Task<object> GetObjectByKeyAsync(Type classType, object id, bool alwaysGetFromDataStore, CancellationToken cancellationToken = default(CancellationToken)) {
			return GetObjectByKeyAsync(Dictionary.GetClassInfo(classType), id, alwaysGetFromDataStore, cancellationToken);
		}
		public ICollection[] GetObjectsByKey(ObjectsByKeyQuery[] queries, bool alwaysGetFromDataStore) {
			return LogManager.Log<ICollection[]>(LogCategory, () => {
				List<List<object>> existsObjectsList = new List<List<object>>();
				List<int?> reloadQueryIndexList = new List<int?>();
				List<ObjectsByKeyQuery> reloadQueryList = new List<ObjectsByKeyQuery>();
				List<ObjectsByKeyQuery> newQueryList = new List<ObjectsByKeyQuery>();
				for(int i = 0; i < queries.Length; i++) {
					List<object> existsObjects = new List<object>();
					List<object> requestIdList = new List<object>();
					List<object> reloadIdList = new List<object>();
					ObjectsByKeyQuery query = queries[i];
					foreach(object id in query.IdCollection) {
						object obj = GetLoadedObjectByKey(query.ClassInfo, id);
						if(obj == null)
							requestIdList.Add(id);
						else {
							if(alwaysGetFromDataStore) {
								RemoveFromLists(obj);
								reloadIdList.Add(id);
								continue;
							}
							existsObjects.Add(obj);
						}
					}
					existsObjectsList.Add(existsObjects);
					if(reloadIdList.Count > 0) {
						reloadQueryIndexList.Add(reloadQueryList.Count);
						reloadQueryList.Add(new ObjectsByKeyQuery(query.ClassInfo, reloadIdList.ToArray()));
					}
					else {
						reloadQueryIndexList.Add(null);
					}
					newQueryList.Add(new ObjectsByKeyQuery(query.ClassInfo, requestIdList.ToArray()));
				}
				ICollection[] reloadedCollections = null;
				if(reloadQueryList.Count > 0) {
					reloadedCollections = ObjectLayer.GetObjectsByKey(this, reloadQueryList.ToArray());
					for(int i = 0; i < reloadedCollections.Length; i++) {
						if(reloadedCollections[i].Count != reloadQueryList[i].IdCollection.Count)
							throw new CannotLoadObjectsException(reloadQueryList[i].ClassInfo.FullName);
						int objectIndex = 0;
						foreach(object obj in reloadedCollections[i]) {
							if(obj == null)
								throw new CannotLoadObjectsException(reloadQueryList[i].ClassInfo.FullName + "(" + ((object[])reloadQueryList[i].IdCollection)[objectIndex].ToString() + ")");
							objectIndex++;
						}
					}
				}
				ICollection[] loadedCollections = ObjectLayer.GetObjectsByKey(this, OptimizeGetObjectsByKeyQueries(newQueryList));
				ICollection[] resultCollections = new ICollection[loadedCollections.Length];
				for(int i = 0; i < loadedCollections.Length; i++) {
					List<object> existsObjects = existsObjectsList[i];
					ICollection loadedCollection = loadedCollections[i];
					object[] result;
					if(reloadQueryIndexList[i].HasValue) {
						ICollection reloadedCollection = reloadedCollections[i];
						result = new object[existsObjects.Count + loadedCollection.Count + reloadedCollection.Count];
						existsObjects.CopyTo(result, 0);
						loadedCollection.CopyTo(result, existsObjects.Count);
						reloadedCollection.CopyTo(result, existsObjects.Count + loadedCollection.Count);
					}
					else {
						result = new object[existsObjects.Count + loadedCollection.Count];
						existsObjects.CopyTo(result, 0);
						loadedCollection.CopyTo(result, existsObjects.Count);
					}
					resultCollections[i] = result;
				}
				ICollection[] rv = new ICollection[queries.Length];
				for(int i = 0; i < queries.Length; ++i) {
					List<object> objs = new List<object>(queries[i].IdCollection.Count);
					XPClassInfo ci = queries[i].ClassInfo;
					foreach(object key in queries[i].IdCollection) {
						objs.Add(GetLoadedObjectByKey(ci, key));
					}
					rv[i] = objs.ToArray();
				}
				GC.KeepAlive(existsObjectsList);
				GC.KeepAlive(loadedCollections);
				GC.KeepAlive(resultCollections);
				GC.KeepAlive(reloadedCollections);
				return rv;
			}, (d) => {
				LogMessage lm = new LogMessage(LogMessageType.SessionEvent, "Executing GetObjectsByKey()", d);
				lm.ParameterList.Add(new LogMessageParameter(LogParam_SessionType, GetType().ToString()));
				lm.ParameterList.Add(new LogMessageParameter(LogParam_SessionID, this.ToString()));
				lm.ParameterList.Add(new LogMessageParameter(LogParam_AlwaysGetFromDataStore, alwaysGetFromDataStore));
				for(int i = 0; i < queries.Length; i++) {
					lm.ParameterList.Add(new LogMessageParameter("query #" + i, LogHelpers.ObjectsByKeyQueryToString(queries[i])));
				}
				return lm;
			});
		}
		public Task<ICollection[]> GetObjectsByKeyAsync(ObjectsByKeyQuery[] queries, bool alwaysGetFromDataStore, CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync<ICollection[]>(LogCategory, async () => {
				List<List<object>> existsObjectsList = new List<List<object>>();
				List<int?> reloadQueryIndexList = new List<int?>();
				List<ObjectsByKeyQuery> reloadQueryList = new List<ObjectsByKeyQuery>();
				List<ObjectsByKeyQuery> newQueryList = new List<ObjectsByKeyQuery>();
				for(int i = 0; i < queries.Length; i++) {
					List<object> existsObjects = new List<object>();
					List<object> requestIdList = new List<object>();
					List<object> reloadIdList = new List<object>();
					ObjectsByKeyQuery query = queries[i];
					foreach(object id in query.IdCollection) {
						object obj = GetLoadedObjectByKey(query.ClassInfo, id);
						if(obj == null)
							requestIdList.Add(id);
						else {
							if(alwaysGetFromDataStore) {
								RemoveFromLists(obj);
								reloadIdList.Add(id);
								continue;
							}
							existsObjects.Add(obj);
						}
					}
					existsObjectsList.Add(existsObjects);
					if(reloadIdList.Count > 0) {
						reloadQueryIndexList.Add(reloadQueryList.Count);
						reloadQueryList.Add(new ObjectsByKeyQuery(query.ClassInfo, reloadIdList.ToArray()));
					}
					else {
						reloadQueryIndexList.Add(null);
					}
					newQueryList.Add(new ObjectsByKeyQuery(query.ClassInfo, requestIdList.ToArray()));
				}
				ICollection[] reloadedCollections = null;
				if(reloadQueryList.Count > 0) {
					reloadedCollections = await GetObjectLayerAsync().GetObjectsByKeyAsync(this, reloadQueryList.ToArray(), cancellationToken);
					for(int i = 0; i < reloadedCollections.Length; i++) {
						if(reloadedCollections[i].Count != reloadQueryList[i].IdCollection.Count)
							throw new CannotLoadObjectsException(reloadQueryList[i].ClassInfo.FullName);
						int objectIndex = 0;
						foreach(object obj in reloadedCollections[i]) {
							if(obj == null)
								throw new CannotLoadObjectsException(reloadQueryList[i].ClassInfo.FullName + "(" + ((object[])reloadQueryList[i].IdCollection)[objectIndex].ToString() + ")");
							objectIndex++;
						}
					}
				}
				ICollection[] loadedCollections = await GetObjectLayerAsync().GetObjectsByKeyAsync(this, OptimizeGetObjectsByKeyQueries(newQueryList), cancellationToken);
				ICollection[] resultCollections = new ICollection[loadedCollections.Length];
				for(int i = 0; i < loadedCollections.Length; i++) {
					List<object> existsObjects = existsObjectsList[i];
					ICollection loadedCollection = loadedCollections[i];
					object[] result;
					if(reloadQueryIndexList[i].HasValue) {
						ICollection reloadedCollection = reloadedCollections[i];
						result = new object[existsObjects.Count + loadedCollection.Count + reloadedCollection.Count];
						existsObjects.CopyTo(result, 0);
						loadedCollection.CopyTo(result, existsObjects.Count);
						reloadedCollection.CopyTo(result, existsObjects.Count + loadedCollection.Count);
					}
					else {
						result = new object[existsObjects.Count + loadedCollection.Count];
						existsObjects.CopyTo(result, 0);
						loadedCollection.CopyTo(result, existsObjects.Count);
					}
					resultCollections[i] = result;
				}
				ICollection[] rv = new ICollection[queries.Length];
				for(int i = 0; i < queries.Length; ++i) {
					List<object> objs = new List<object>(queries[i].IdCollection.Count);
					XPClassInfo ci = queries[i].ClassInfo;
					foreach(object key in queries[i].IdCollection) {
						objs.Add(GetLoadedObjectByKey(ci, key));
					}
					rv[i] = objs.ToArray();
				}
				GC.KeepAlive(existsObjectsList);
				GC.KeepAlive(loadedCollections);
				GC.KeepAlive(resultCollections);
				GC.KeepAlive(reloadedCollections);
				return rv;
			}, (d) => {
				LogMessage lm = new LogMessage(LogMessageType.SessionEvent, "Executing GetObjectsByKeyAsync()", d);
				lm.ParameterList.Add(new LogMessageParameter(LogParam_SessionType, GetType().ToString()));
				lm.ParameterList.Add(new LogMessageParameter(LogParam_SessionID, this.ToString()));
				lm.ParameterList.Add(new LogMessageParameter(LogParam_AlwaysGetFromDataStore, alwaysGetFromDataStore));
				for(int i = 0; i < queries.Length; i++) {
					lm.ParameterList.Add(new LogMessageParameter("query #" + i, LogHelpers.ObjectsByKeyQueryToString(queries[i])));
				}
				return lm;
			});
		}
		ObjectsByKeyQuery[] OptimizeGetObjectsByKeyQueries(List<ObjectsByKeyQuery> queries) {
			ObjectsByKeyQuery[] result = new ObjectsByKeyQuery[queries.Count];
			for(int i = 0; i < queries.Count; i++) {
				Dictionary<object, bool> existsKeys = new Dictionary<object, bool>();
				foreach(object id in queries[i].IdCollection) {
					if(id == null)
						continue;
					existsKeys[id] = true;
				}
				object[] resultIdCollection = new object[existsKeys.Count];
				existsKeys.Keys.CopyTo(resultIdCollection, 0);
				result[i] = new ObjectsByKeyQuery(queries[i].ClassInfo, resultIdCollection);
			}
			return result;
		}
		public ICollection GetObjectsByKey(XPClassInfo classInfo, ICollection idCollection, bool alwaysGetFromDataStore) {
			return GetObjectsByKey(new ObjectsByKeyQuery[] { new ObjectsByKeyQuery(classInfo, idCollection) }, alwaysGetFromDataStore)[0];
		}
		public async Task<ICollection> GetObjectsByKeyAsync(XPClassInfo classInfo, ICollection idCollection, bool alwaysGetFromDataStore, CancellationToken cancellationToken = default(CancellationToken)) {
			ICollection[] result = await GetObjectsByKeyAsync(new ObjectsByKeyQuery[] { new ObjectsByKeyQuery(classInfo, idCollection) }, alwaysGetFromDataStore, cancellationToken).ConfigureAwait(false);
			return result[0];
		}
		public object GetObjectByKey(XPClassInfo classInfo, object id, bool alwaysGetFromDataStore) {
			return LogManager.Log<object>(LogCategory, () => {
				object result = null;
				TypesManager.EnsureIsTypedObjectValid();
				if(id != null) {
					result = GetLoadedObjectByKey(classInfo, id);
					if(result != null) {
						if(alwaysGetFromDataStore)
							Reload(result);
					}
					else {
						ICollection coll = ObjectLayer.GetObjectsByKey(this, new ObjectsByKeyQuery[] { new ObjectsByKeyQuery(classInfo, new object[] { id }) })[0];
						IEnumerator enm = coll.GetEnumerator();
						result = enm.MoveNext() ? enm.Current : null;
					}
				}
				return result;
			}, (d) => {
				return CreateLogMessage("Executing GetObjectByKey()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_ClassInfo, classInfo.FullName,
				LogParam_ObjectID, id == null ? "null" : id.ToString(),
				LogParam_AlwaysGetFromDataStore, alwaysGetFromDataStore
			});
			});
		}
		public Task<object> GetObjectByKeyAsync(XPClassInfo classInfo, object id, bool alwaysGetFromDataStore, CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync<object>(LogCategory, async () => {
				object result = null;
				await TypesManager.EnsureIsTypedObjectValidAsync(cancellationToken);
				if(id != null) {
					result = GetLoadedObjectByKey(classInfo, id);
					if(result != null) {
						if(alwaysGetFromDataStore)
							await ReloadAsync(result, cancellationToken);
					}
					else {
						ICollection[] coll = await GetObjectLayerAsync().GetObjectsByKeyAsync(this, new ObjectsByKeyQuery[] { new ObjectsByKeyQuery(classInfo, new object[] { id }) }, cancellationToken);
						IEnumerator enm = coll[0].GetEnumerator();
						result = enm.MoveNext() ? enm.Current : null;
					}
				}
				return result;
			}, (d) => {
				return CreateLogMessage("Executing GetObjectByKeyAsync()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_ClassInfo, classInfo.FullName,
				LogParam_ObjectID, id == null ? "null" : id.ToString(),
				LogParam_AlwaysGetFromDataStore, alwaysGetFromDataStore
			});
			});
		}
		public ClassType GetLoadedObjectByKey<ClassType>(object id) {
			return (ClassType)GetLoadedObjectByKey(typeof(ClassType), id);
		}
		public ClassType GetObjectByKey<ClassType>(object id) {
			return (ClassType)GetObjectByKey(typeof(ClassType), id);
		}
		public ClassType GetObjectByKey<ClassType>(object id, bool alwaysGetFromDataStore) {
			return (ClassType)GetObjectByKey(typeof(ClassType), id, alwaysGetFromDataStore);
		}
		public async Task<ClassType> GetObjectByKeyAsync<ClassType>(object id, CancellationToken cancellationToken = default(CancellationToken)) {
			return (ClassType)(await GetObjectByKeyAsync(typeof(ClassType), id, cancellationToken).ConfigureAwait(false));
		}
		public async Task<ClassType> GetObjectByKeyAsync<ClassType>(object id, bool alwaysGetFromDataStore, CancellationToken cancellationToken = default(CancellationToken)) {
			return (ClassType)(await GetObjectByKeyAsync(typeof(ClassType), id, alwaysGetFromDataStore, cancellationToken).ConfigureAwait(false));
		}
		public object FindObject(Type classType, CriteriaOperator criteria) {
			return FindObject(classType, criteria, false);
		}
		public object FindObject(XPClassInfo classInfo, CriteriaOperator criteria) {
			return FindObject(classInfo, criteria, false);
		}
		public Task<object> FindObjectAsync(Type classType, CriteriaOperator criteria, CancellationToken cancellationToken = default(CancellationToken)) {
			return FindObjectAsync(classType, criteria, false, cancellationToken);
		}
		public Task<object> FindObjectAsync(XPClassInfo classInfo, CriteriaOperator criteria, CancellationToken cancellationToken = default(CancellationToken)) {
			return FindObjectAsync(classInfo, criteria, false, cancellationToken);
		}
		private ObjectsQuery GetQueryForFindObject(XPClassInfo classInfo, CriteriaOperator criteria, bool selectDeleted) {
			return new ObjectsQuery(classInfo, criteria, null, 0, 1, new CollectionCriteriaPatcher(selectDeleted, TypesManager), false);
		}
		public object FindObject(XPClassInfo classInfo, CriteriaOperator criteria, bool selectDeleted) {
			return LogManager.Log<object>(LogCategory, () => {
				ObjectsQuery query = GetQueryForFindObject(classInfo, criteria, selectDeleted);
				ICollection coll = GetObjects(query);
				IEnumerator e = coll.GetEnumerator();
				return e.MoveNext() ? e.Current : null;
			}, (d) => {
				return CreateLogMessage("Executing FindObject()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_IsInTransactionMode, this.IsInTransactionMode,
				LogParam_ClassInfo, classInfo.FullName,
				LogParam_Criteria, ReferenceEquals(criteria, null) ? "null" : criteria.ToString(),
				LogParam_SelectDeleted, selectDeleted
			});
			});
		}
		public Task<object> FindObjectAsync(XPClassInfo classInfo, CriteriaOperator criteria, bool selectDeleted, CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync<object>(LogCategory, async () => {
				ObjectsQuery query = GetQueryForFindObject(classInfo, criteria, selectDeleted);
				ICollection coll = await GetObjectsAsync(query, cancellationToken).ConfigureAwait(false);
				IEnumerator e = coll.GetEnumerator();
				return e.MoveNext() ? e.Current : null;
			}, (d) => {
				return CreateLogMessage("Executing FindObjectAsync()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_IsInTransactionMode, this.IsInTransactionMode,
				LogParam_ClassInfo, classInfo.FullName,
				LogParam_Criteria, ReferenceEquals(criteria, null) ? "null" : criteria.ToString(),
				LogParam_SelectDeleted, selectDeleted
			});
			});
		}
		static void FindObjectAsyncResultProcess(ICollection[] res, Exception ex, AsyncFindObjectCallback callback) {
			if(ex != null) {
				try {
					callback(null, ex);
				}
				catch(Exception) { }
				return;
			}
			object resultObject = null;
			try {
				if(res != null && res.Length > 0 && res[0] != null) {
					IEnumerator e = res[0].GetEnumerator();
					resultObject = e.MoveNext() ? e.Current : null;
				}
			}
			catch(Exception iex) {
				try {
					callback(null, iex);
				}
				catch(Exception) { }
			}
			try {
				callback(resultObject, null);
			}
			catch(Exception) { }
		}
		public object FindObjectAsync<ClassType>(CriteriaOperator criteria, AsyncFindObjectCallback callback) {
			return FindObjectAsync(GetClassInfo<ClassType>(), criteria, false, callback);
		}
		public object FindObjectAsync<ClassType>(CriteriaOperator criteria, bool selectDeleted, AsyncFindObjectCallback callback) {
			return FindObjectAsync(GetClassInfo<ClassType>(), criteria, selectDeleted, callback);
		}
		public object FindObjectAsync<ClassType>(PersistentCriteriaEvaluationBehavior criteriaEvaluationBehavior, CriteriaOperator criteria, AsyncFindObjectCallback callback) {
			return FindObjectAsync(criteriaEvaluationBehavior, GetClassInfo<ClassType>(), criteria, false, callback);
		}
		public object FindObjectAsync(XPClassInfo classInfo, CriteriaOperator criteria, AsyncFindObjectCallback callback) {
			return FindObjectAsync(classInfo, criteria, false, callback);
		}
		public object FindObjectAsync(XPClassInfo classInfo, CriteriaOperator criteria, bool selectDeleted, AsyncFindObjectCallback callback) {
			ObjectsQuery query = GetQueryForFindObject(classInfo, criteria, selectDeleted);
			return GetObjectsAsync(new ObjectsQuery[] { query }, (res, ex) => { FindObjectAsyncResultProcess(res, ex, callback); });
		}
		public object FindObjectAsync(PersistentCriteriaEvaluationBehavior criteriaEvaluationBehavior, XPClassInfo classInfo, CriteriaOperator criteria, AsyncFindObjectCallback callback) {
			return FindObjectAsync(criteriaEvaluationBehavior, classInfo, criteria, false, callback);
		}
		public object FindObjectAsync(PersistentCriteriaEvaluationBehavior criteriaEvaluationBehavior, XPClassInfo classInfo, CriteriaOperator criteria, bool selectDeleted, AsyncFindObjectCallback callback) {
			return LogManager.Log<object>(LogCategory, () => {
				AsyncLoadObjectsCallback loadObjects = (res, ex) => { FindObjectAsyncResultProcess(res, ex, callback); };
				switch(criteriaEvaluationBehavior) {
					case PersistentCriteriaEvaluationBehavior.BeforeTransaction: {
						ObjectsQuery query = GetQueryForFindObject(classInfo, criteria, selectDeleted);
						return GetObjectsInternalAsync(new ObjectsQuery[] { query }, loadObjects);
					}
					case PersistentCriteriaEvaluationBehavior.InTransaction: {
						ObjectsQuery query = GetQueryForFindObject(classInfo, criteria, selectDeleted);
						return InTransactionLoader.GetObjectsAsync(this, new ObjectsQuery[] { query }, loadObjects);
					}
					default:
						throw new InvalidOperationException(Res.GetString(Res.Session_UnexpectedPersistentCriteriaEvaluationBehavior, criteriaEvaluationBehavior.ToString()));
				}
			}, (d) => {
				return CreateLogMessage("Executing FindObjectAsync()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_CriteriaEvaluationBehavior, criteriaEvaluationBehavior.ToString(),
				LogParam_ClassInfo, classInfo.FullName,
				LogParam_Criteria, ReferenceEquals(criteria, null) ? "null" : criteria.ToString(),
				LogParam_SelectDeleted, selectDeleted
				});
			});
		}
		public object FindObject(PersistentCriteriaEvaluationBehavior criteriaEvaluationBehavior, XPClassInfo classInfo, CriteriaOperator criteria) {
			return FindObject(criteriaEvaluationBehavior, classInfo, criteria, false);
		}
		public object FindObject(PersistentCriteriaEvaluationBehavior criteriaEvaluationBehavior, XPClassInfo classInfo, CriteriaOperator criteria, bool selectDeleted) {
			return LogManager.Log<object>(LogCategory, () => {
				switch(criteriaEvaluationBehavior) {
					case PersistentCriteriaEvaluationBehavior.BeforeTransaction: {
						ObjectsQuery query = GetQueryForFindObject(classInfo, criteria, selectDeleted);
						ICollection coll = GetObjectsInternal(new ObjectsQuery[] { query })[0];
						IEnumerator e = coll.GetEnumerator();
						return e.MoveNext() ? e.Current : null;
					}
					case PersistentCriteriaEvaluationBehavior.InTransaction: {
						ObjectsQuery query = GetQueryForFindObject(classInfo, criteria, selectDeleted);
						ICollection coll = InTransactionLoader.GetObjects(this, new ObjectsQuery[] { query })[0];
						IEnumerator e = coll.GetEnumerator();
						return e.MoveNext() ? e.Current : null;
					}
					default:
						throw new InvalidOperationException(Res.GetString(Res.Session_UnexpectedPersistentCriteriaEvaluationBehavior, criteriaEvaluationBehavior.ToString()));
				}
			}, (d) => {
				return CreateLogMessage("Executing FindObject()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_CriteriaEvaluationBehavior, criteriaEvaluationBehavior.ToString(),
				LogParam_ClassInfo, classInfo.FullName,
				LogParam_Criteria, ReferenceEquals(criteria, null) ? "null" : criteria.ToString(),
				LogParam_SelectDeleted, selectDeleted
				});
			});
		}
		public Task<object> FindObjectAsync(PersistentCriteriaEvaluationBehavior criteriaEvaluationBehavior, XPClassInfo classInfo, CriteriaOperator criteria, bool selectDeleted, CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync<object>(LogCategory, async () => {
				switch(criteriaEvaluationBehavior) {
					case PersistentCriteriaEvaluationBehavior.BeforeTransaction: {
						ObjectsQuery query = GetQueryForFindObject(classInfo, criteria, selectDeleted);
						ICollection coll = (await GetObjectsInternalAsync(new ObjectsQuery[] { query }, cancellationToken).ConfigureAwait(false))[0];
						IEnumerator e = coll.GetEnumerator();
						return e.MoveNext() ? e.Current : null;
					}
					case PersistentCriteriaEvaluationBehavior.InTransaction: {
						ObjectsQuery query = GetQueryForFindObject(classInfo, criteria, selectDeleted);
						ICollection coll = (await InTransactionLoader.GetObjectsAsync(this, new ObjectsQuery[] { query }, cancellationToken).ConfigureAwait(false))[0];
						IEnumerator e = coll.GetEnumerator();
						return e.MoveNext() ? e.Current : null;
					}
					default:
						throw new InvalidOperationException(Res.GetString(Res.Session_UnexpectedPersistentCriteriaEvaluationBehavior, criteriaEvaluationBehavior.ToString()));
				}
			}, (d) => {
				return CreateLogMessage("Executing FindObject()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_CriteriaEvaluationBehavior, criteriaEvaluationBehavior.ToString(),
				LogParam_ClassInfo, classInfo.FullName,
				LogParam_Criteria, ReferenceEquals(criteria, null) ? "null" : criteria.ToString(),
				LogParam_SelectDeleted, selectDeleted
				});
			});
		}
		public object FindObject(PersistentCriteriaEvaluationBehavior criteriaEvaluationBehavior, Type objType, CriteriaOperator criteria) {
			return FindObject(criteriaEvaluationBehavior, this.GetClassInfo(objType), criteria);
		}
		public object FindObject(Type classType, CriteriaOperator criteria, bool selectDeleted) {
			return FindObject(Dictionary.GetClassInfo(classType), criteria, selectDeleted);
		}
		public ClassType FindObject<ClassType>(CriteriaOperator criteria) {
			return (ClassType)FindObject(typeof(ClassType), criteria, false);
		}
		public ClassType FindObject<ClassType>(CriteriaOperator criteria, bool selectDeleted) {
			return (ClassType)FindObject(typeof(ClassType), criteria, selectDeleted);
		}
		public ClassType FindObject<ClassType>(PersistentCriteriaEvaluationBehavior criteriaEvaluationBehavior, CriteriaOperator criteria) {
			return (ClassType)FindObject(criteriaEvaluationBehavior, typeof(ClassType), criteria);
		}
		public Task<object> FindObjectAsync(PersistentCriteriaEvaluationBehavior criteriaEvaluationBehavior, Type objType, CriteriaOperator criteria, CancellationToken cancellationToken = default(CancellationToken)) {
			return FindObjectAsync(criteriaEvaluationBehavior, this.GetClassInfo(objType), criteria, false, cancellationToken);
		}
		public Task<object> FindObjectAsync(Type classType, CriteriaOperator criteria, bool selectDeleted, CancellationToken cancellationToken = default(CancellationToken)) {
			return FindObjectAsync(Dictionary.GetClassInfo(classType), criteria, selectDeleted, cancellationToken);
		}
		public async Task<ClassType> FindObjectAsync<ClassType>(CriteriaOperator criteria, CancellationToken cancellationToken = default(CancellationToken)) {
			return (ClassType)(await FindObjectAsync(typeof(ClassType), criteria, false, cancellationToken).ConfigureAwait(false));
		}
		public async Task<ClassType> FindObjectAsync<ClassType>(CriteriaOperator criteria, bool selectDeleted, CancellationToken cancellationToken = default(CancellationToken)) {
			return (ClassType)(await FindObjectAsync(typeof(ClassType), criteria, selectDeleted, cancellationToken).ConfigureAwait(false));
		}
		public async Task<ClassType> FindObjectAsync<ClassType>(PersistentCriteriaEvaluationBehavior criteriaEvaluationBehavior, CriteriaOperator criteria, CancellationToken cancellationToken = default(CancellationToken)) {
			return (ClassType)(await FindObjectAsync(criteriaEvaluationBehavior, typeof(ClassType), criteria, cancellationToken).ConfigureAwait(false));
		}
		[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Please use GetObjectsToSave() instead", true)]
		public ICollection ObjectsSavedInCurrentTransaction {
			get { return GetObjectsToSave(); }
		}
		public ICollection GetObjectsToSave() {
			return objectsMarkedSaved;
		}
		public ICollection GetObjectsToDelete() {
			return objectsMarkedDeleted;
		}
		public virtual ICollection GetObjectsToSave(bool includeParent) {
			ICollection myObjects = GetObjectsToSave();
			if(!includeParent)
				return myObjects;
			IObjectLayerOnSession objectLayerOnSession = ObjectLayer as IObjectLayerOnSession;
			if(objectLayerOnSession == null)
				return myObjects;
			ICollection parentObjects = objectLayerOnSession.GetParentObjectsToSave(this);
			if(parentObjects.Count == 0)
				return myObjects;
			ObjectSet combinedList = new ObjectSet(myObjects.Count + parentObjects.Count);
			foreach(object obj in myObjects) {
				combinedList.Add(obj);
			}
			foreach(object obj in parentObjects) {
				combinedList.Add(obj);
			}
			return combinedList;
		}
		public virtual ICollection GetObjectsToDelete(bool includeParent) {
			ICollection myObjects = GetObjectsToDelete();
			if(!includeParent)
				return myObjects;
			IObjectLayerOnSession objectLayerOnSession = ObjectLayer as IObjectLayerOnSession;
			if(objectLayerOnSession == null)
				return myObjects;
			ICollection parentObjects = objectLayerOnSession.GetParentObjectsToDelete(this);
			if(parentObjects.Count == 0)
				return myObjects;
			ObjectSet combinedList = new ObjectSet(myObjects.Count + parentObjects.Count);
			foreach(object obj in myObjects) {
				combinedList.Add(obj);
			}
			foreach(object obj in parentObjects) {
				combinedList.Add(obj);
			}
			return combinedList;
		}
		public ICollection CollectReferencingObjects(object target, PersistentCriteriaEvaluationBehavior behavior, bool selectDeleted) {
			if(target == null)
				throw new ArgumentNullException(nameof(target));
			ThrowIfObjectFromDifferentSession(target);
			XPClassInfo ci = GetClassInfo(target);
			ci.CheckAbstractReference();
			TypesManager.EnsureIsTypedObjectValid();
			Dictionary<XPClassInfo, List<XPMemberInfo>> referenceMembersByMappingClasses = new Dictionary<XPClassInfo, List<XPMemberInfo>>();
			foreach(XPClassInfo possibleReferrer in Dictionary.Classes) {
				if(!possibleReferrer.IsPersistent)
					continue;
				foreach(XPMemberInfo mi in possibleReferrer.ObjectProperties) {
					if(!ci.IsAssignableTo(mi.ReferenceType))
						continue;
					XPClassInfo referenceMappingClass = mi.GetMappingClass(possibleReferrer);
					List<XPMemberInfo> references;
					if(!referenceMembersByMappingClasses.TryGetValue(referenceMappingClass, out references)) {
						references = new List<XPMemberInfo>();
						referenceMembersByMappingClasses.Add(referenceMappingClass, references);
					}
					if(!references.Contains(mi))
						references.Add(mi);
				}
			}
			ObjectSet resultCollector = new ObjectSet();
			foreach(XPClassInfo referrer in referenceMembersByMappingClasses.Keys) {
				List<CriteriaOperator> list = new List<CriteriaOperator>();
				foreach(XPMemberInfo mi in (IList)referenceMembersByMappingClasses[referrer]) {
					list.Add(new OperandProperty(mi.Name));
				}
				InOperator criteria = new InOperator(new OperandValue(target), list);
				ICollection referringObjects;
				if(behavior == PersistentCriteriaEvaluationBehavior.InTransaction)
					referringObjects = GetObjectsInTransaction(referrer, criteria, selectDeleted);
				else
					referringObjects = GetObjectsInternal(new ObjectsQuery[] { new ObjectsQuery(referrer, criteria, null, 0, 0, new CollectionCriteriaPatcher(selectDeleted, TypesManager), false) })[0];
				foreach(object obj in referringObjects) {
					resultCollector.Add(obj);
				}
			}
			return resultCollector;
		}
		public ICollection CollectReferencingObjects(object target) {
			return CollectReferencingObjects(target, PersistentCriteriaEvaluationBehavior.BeforeTransaction, false);
		}
		public IList ProcessReferences(object target, ProcessReferenceHandler process) {
			List<object> result = ListHelper.FromCollection(CollectReferencingObjects(target));
			XPClassInfo ci = GetClassInfo(target);
			foreach(object obj in result) {
				foreach(XPMemberInfo mi in GetClassInfo(obj).ObjectProperties) {
					if(!ci.IsAssignableTo(mi.ReferenceType))
						continue;
					if(!ReferenceEquals(target, mi.GetValue(obj)))
						continue;
					process(obj, mi);
				}
			}
			return result;
		}
		public bool IsObjectToSave(object theObject) {
			if(theObject == null)
				throw new ArgumentNullException(LogParam_TheObject);
			ThrowIfObjectFromDifferentSession(theObject);
			return objectsMarkedSaved.Contains(theObject);
		}
		public bool IsObjectToDelete(object theObject) {
			if(theObject == null)
				throw new ArgumentNullException(LogParam_TheObject);
			ThrowIfObjectFromDifferentSession(theObject);
			return objectsMarkedDeleted.Contains(theObject);
		}
		public bool IsNewObject(object theObject) {
			ThrowIfObjectFromDifferentSession(theObject);
			XPClassInfo ci = GetClassInfo(theObject);
			return SessionIdentityMap.GetLoadedObjectByKey(this, ci, ci.GetId(theObject)) != theObject;
		}
		public virtual bool IsObjectToSave(object theObject, bool includeParent) {
			if(IsObjectToSave(theObject))
				return true;
			if(!includeParent)
				return false;
			IObjectLayerOnSession objectLayerOnSession = ObjectLayer as IObjectLayerOnSession;
			if(objectLayerOnSession == null)
				return false;
			return objectLayerOnSession.IsParentObjectToSave(this, theObject);
		}
		public virtual bool IsObjectToDelete(object theObject, bool includeParent) {
			if(IsObjectToDelete(theObject))
				return true;
			if(!includeParent)
				return false;
			IObjectLayerOnSession objectLayerOnSession = ObjectLayer as IObjectLayerOnSession;
			if(objectLayerOnSession == null)
				return false;
			return objectLayerOnSession.IsParentObjectToDelete(this, theObject);
		}
		[Obsolete("Use IsNewObject(object theObject) instead", true)]
		[EditorBrowsable(EditorBrowsableState.Never), Browsable(false)]
		public virtual bool IsNewObject(object theObject, bool includeParent) {
			return IsNewObject(theObject);
		}
		public bool IsObjectMarkedDeleted(object theObject) {
			if(IsObjectToDelete(theObject))
				return true;
			XPMemberInfo gc = GetClassInfo(theObject).FindMember(GCRecordField.StaticName);
			if(gc == null)
				return false;
			else
				return gc.GetValue(theObject) != null;
		}
		protected internal virtual ICollection GetTouchedClassInfosIncludeParent() {
			Dictionary<XPClassInfo, object> rv = new Dictionary<XPClassInfo, object>();
			foreach(object obj in GetObjectsToSave()) {
				rv[GetClassInfo(obj)] = null;
			}
			foreach(object obj in GetObjectsToDelete()) {
				rv[GetClassInfo(obj)] = null;
			}
			IObjectLayerOnSession objectLayerOnSession = ObjectLayer as IObjectLayerOnSession;
			if(objectLayerOnSession != null) {
				foreach(XPClassInfo ci in objectLayerOnSession.GetParentTouchedClassInfos(this)) {
					rv[ci] = null;
				}
			}
			return rv.Keys;
		}
		[Browsable(false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public void ClearDatabase() {
			DropIdentityMap();
			((IObjectLayerForTests)ObjectLayer).ClearDatabase();
			DropIdentityMap();
		}
		protected virtual NestedUnitOfWork CreateNestedUnitOfWork() {
			return new NestedUnitOfWork(this);
		}
		public NestedUnitOfWork BeginNestedUnitOfWork() {
			return CreateNestedUnitOfWork();
		}
		public virtual void BeginTransaction() {
			LogManager.Log(LogCategory, () => {
				BeginTrackingChanges();
			}, (d) => {
				return CreateLogMessage("Executing BeginTransaction()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString()
			});
			}, null);
		}
		public void BeginTrackingChanges() {
			OnBeforeBeginTrackingChanges();
			if(TrackingChanges)
				throw new TransactionSequenceException(Res.GetString(Res.Session_TranSequenceBegin));
			SessionStateStack.Enter(this, SessionState.BeginTransaction);
			try {
				Connect();
				System.Diagnostics.Debug.Assert(GetObjectsToSave().Count == 0);
				System.Diagnostics.Debug.Assert(GetObjectsToDelete().Count == 0);
				trackingChanges = true;
			}
			finally {
				SessionStateStack.Leave(this, SessionState.BeginTransaction);
			}
			OnAfterBeginTrackingChanges();
		}
		void FlushChangesInsideTransaction() {
			List<object> fullListForDelete = new List<object>();
			foreach(object obj in GetObjectsToDelete()) {
				System.Diagnostics.Debug.Assert(!GetClassInfo(obj).IsGCRecordObject);
				fullListForDelete.Add(obj);
			}
			List<object> completeListForSave = new List<object>();
			foreach(object obj in GetObjectsToSave()) {
				if(!IsObjectToDelete(obj))
					completeListForSave.Add(obj);
			}
			ObjectLayer.CommitChanges(this, fullListForDelete, completeListForSave);
		}
		Task FlushChangesInsideTransactionAsync(CancellationToken cancellationToken = default(CancellationToken)) {
			List<object> fullListForDelete = new List<object>();
			foreach(object obj in GetObjectsToDelete()) {
				System.Diagnostics.Debug.Assert(!GetClassInfo(obj).IsGCRecordObject);
				fullListForDelete.Add(obj);
			}
			List<object> completeListForSave = new List<object>();
			foreach(object obj in GetObjectsToSave()) {
				if(!IsObjectToDelete(obj))
					completeListForSave.Add(obj);
			}
			return GetObjectLayerAsync().CommitChangesAsync(this, fullListForDelete, completeListForSave, cancellationToken);
		}
		void ThrowIfCommitChangesToDataLayerInner() {
			if(SessionStateStack.IsInAnyOf(this, SessionState.CommitChangesToDataLayerInner)) {
				throw new InvalidOperationException(Res.GetString(Res.Session_ObjectModificationIsNotAllowed));
			}
		}
		public virtual object CommitTransactionAsync(AsyncCommitCallback callback) {
			return FlushChangesAsync(callback);
		}
		public object FlushChangesAsync(AsyncCommitCallback callback) {
			return LogManager.Log<object>(LogCategory, () => {
				try {
					IList objectsToFireSaved = BeginFlushChanges();
					List<object> fullListForDelete = new List<object>();
					foreach(object obj in GetObjectsToDelete()) {
						System.Diagnostics.Debug.Assert(!GetClassInfo(obj).IsGCRecordObject);
						fullListForDelete.Add(obj);
					}
					List<object> completeListForSave = new List<object>();
					foreach(object obj in GetObjectsToSave()) {
						if(!IsObjectToDelete(obj))
							completeListForSave.Add(obj);
					}
					SessionStateStack.Enter(this, SessionState.CommitChangesToDataLayer);
					return ObjectLayer.CommitChangesAsync(this, fullListForDelete, completeListForSave, new AsyncCommitCallback(delegate (Exception commitEx) {
						SessionStateStack.Leave(this, SessionState.CommitChangesToDataLayer);
						if(commitEx == null) {
							try {
								EndFlushChanges(objectsToFireSaved);
								callback(null);
							}
							catch(Exception ex) {
								try {
									callback(OnFailedFlushChanges(ex) ? null : ex);
								}
								catch(Exception) { }
							}
							return;
						}
						try {
							callback(OnFailedFlushChanges(commitEx) ? null : commitEx);
						}
						catch(Exception) { }
					}));
				}
				catch(Exception syncEx) {
					if(!OnFailedFlushChanges(syncEx)) {
						throw;
					}
					return null;
				}
			}, (d) => {
				LogMessage message = new LogMessage(LogMessageType.SessionEvent, "Executing FlushChangesAsync()", d);
				message.ParameterList.Add(new LogMessageParameter(LogParam_SessionType, this.GetType().ToString()));
				message.ParameterList.Add(new LogMessageParameter(LogParam_SessionID, this.ToString()));
				return message;
			});
		}
		public virtual Task CommitTransactionAsync(CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync(LogCategory, () => {
				return FlushChangesAsync(cancellationToken);
			}, (d) => {
				return CreateLogMessage("Executing CommitTransactionAsync()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString()
			});
			}, null);
		}
		public virtual void CommitTransaction() {
			LogManager.Log(LogCategory, () => {
				FlushChanges();
			}, (d) => {
				return CreateLogMessage("Executing CommitTransaction()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString()
			});
			}, null);
		}
		public void FlushChanges() {
			try {
				WaitForAsyncOperationEnd();
				IList objectsToFireSaved = BeginFlushChanges();
				SessionStateStack.Enter(this, SessionState.CommitTransactionNonReenterant);
				try {
					SessionStateStack.Enter(this, SessionState.CommitChangesToDataLayer);
					try {
						FlushChangesInsideTransaction();
					}
					finally {
						SessionStateStack.Leave(this, SessionState.CommitChangesToDataLayer);
					}
				}
				finally {
					SessionStateStack.Leave(this, SessionState.CommitTransactionNonReenterant);
				}
				EndFlushChanges(objectsToFireSaved);
			}
			catch(Exception ex) {
				if(!OnFailedFlushChanges(ex)) {
					throw;
				}
			}
		}
		public async Task FlushChangesAsync(CancellationToken cancellationToken = default(CancellationToken)) {
			try {
				int asyncOperationId = SessionStateStack.GetNewAsyncOperationId();
				IList objectsToFireSaved = await BeginFlushChangesAsync(asyncOperationId, cancellationToken);
				await SessionStateStack.EnterAsync(this, SessionState.CommitTransactionNonReenterant, asyncOperationId, cancellationToken);
				try {
					SessionStateStack.Enter(this, SessionState.CommitChangesToDataLayer);
					try {
						await FlushChangesInsideTransactionAsync(cancellationToken);
					}
					finally {
						SessionStateStack.Leave(this, SessionState.CommitChangesToDataLayer);
					}
				}
				finally {
					SessionStateStack.Leave(this, SessionState.CommitTransactionNonReenterant, asyncOperationId);
				}
				await EndFlushChangesAsync(objectsToFireSaved, asyncOperationId, cancellationToken);
			}
			catch(Exception ex) {
				if(!OnFailedFlushChanges(ex)) {
					throw;
				}
			}
		}
		protected virtual IList BeginFlushChanges() {
			IList objectsToFireSaved = null;
			if(!IsUnitOfWork && !TrackingChanges)
				throw new TransactionSequenceException(Res.GetString(Res.Session_TranSequenceCommit));
			ThrowIfCommitChangesToDataLayerInner();
			SessionStateStack.Enter(this, SessionState.CommitTransactionNonReenterant);
			try {
				PreProcessSavedList();
				objectsToFireSaved = BeginFlushChangesCore(objectsToFireSaved);
			}
			finally {
				SessionStateStack.Leave(this, SessionState.CommitTransactionNonReenterant);
			}
			return objectsToFireSaved;
		}
		protected virtual async Task<IList> BeginFlushChangesAsync(int asyncOperationId, CancellationToken cancellationToken = default(CancellationToken)) {
			IList objectsToFireSaved = null;
			if(!IsUnitOfWork && !TrackingChanges)
				throw new TransactionSequenceException(Res.GetString(Res.Session_TranSequenceCommit));
			ThrowIfCommitChangesToDataLayerInner();
			await SessionStateStack.EnterAsync(this, SessionState.CommitTransactionNonReenterant, asyncOperationId, cancellationToken);
			try {
				cancellationToken.ThrowIfCancellationRequested();
				await PreProcessSavedListAsync(cancellationToken);
				objectsToFireSaved = BeginFlushChangesCore(objectsToFireSaved);
			}
			finally {
				SessionStateStack.Leave(this, SessionState.CommitTransactionNonReenterant, asyncOperationId);
			}
			return objectsToFireSaved;
		}
		void EndFlushChanges(IList objectsToFireSaved) {
			SessionStateStack.Enter(this, SessionState.CommitTransactionNonReenterant);
			try {
				EndFlushChangesCore(objectsToFireSaved);
				((IInfrastructure<ISecuredPropertyAccessor>)this).Instance?.ClearAllChangedSecuredProperties();
			}
			finally {
				SessionStateStack.Leave(this, SessionState.CommitTransactionNonReenterant);
			}
			TriggerObjectsSaved(objectsToFireSaved);
			OnAfterFlushChanges();
		}
		async Task EndFlushChangesAsync(IList objectsToFireSaved, int asyncOperationId, CancellationToken cancellationToken) {
			await SessionStateStack.EnterAsync(this, SessionState.CommitTransactionNonReenterant, asyncOperationId, cancellationToken);
			try {
				cancellationToken.ThrowIfCancellationRequested();
				EndFlushChangesCore(objectsToFireSaved);
				((IInfrastructure<ISecuredPropertyAccessor>)this).Instance?.ClearAllChangedSecuredProperties();
			}
			finally {
				SessionStateStack.Leave(this, SessionState.CommitTransactionNonReenterant, asyncOperationId);
			}
			TriggerObjectsSaved(objectsToFireSaved);
			OnAfterFlushChanges();
		}
		IList BeginFlushChangesCore(IList objectsToFireSaved) {
			OnBeforeFlushChanges();
			ICollection objectsToSave = GetObjectsToSave();
			objectsToFireSaved = new List<object>(objectsToSave.Count);
			foreach(object obj in objectsToSave) {
				if(!IsObjectToDelete(obj))
					objectsToFireSaved.Add(obj);
			}
			SessionStateStack.Enter(this, SessionState.OptimisticLockFieldsProcessing);
			try {
				foreach(object obj in objectsToSave) {
					XPClassInfo ci = GetClassInfo(obj);
					XPMemberInfo olf = ci.OptimisticLockField;
					if(olf == null)
						continue;
					int? oldV = (int?)olf.GetValue(obj);
					int? newV;
					if(oldV.HasValue && oldV < int.MaxValue)
						newV = oldV.Value + 1;
					else
						newV = 0;
					olf.SetValue(obj, newV);
					if(ci.TrackPropertiesModifications ?? TrackPropertiesModifications)
						olf.SetModified(obj, oldV);
				}
				return objectsToFireSaved;
			}
			finally {
				SessionStateStack.Leave(this, SessionState.OptimisticLockFieldsProcessing);
			}
		}
		void EndFlushChangesCore(IList objectsToFireSaved) {
			SessionStateStack.Enter(this, SessionState.OptimisticLockFieldsProcessing);
			try {
				foreach(object obj in objectsToFireSaved) {
					XPClassInfo ci = GetClassInfo(obj);
					foreach(XPMemberInfo mi in ci.PersistentProperties) {
						if(!mi.IsDelayed)
							continue;
						XPDelayedProperty container = XPDelayedProperty.GetDelayedPropertyContainer(obj, mi);
						container.ResetIsModified();
					}
					XPMemberInfo olf = ci.OptimisticLockField;
					if(olf != null) {
						ci.OptimisticLockFieldInDataLayer.SetValue(obj, olf.GetValue(obj));
					}
					ci.ClearModifications(obj);
				}
			}
			finally {
				SessionStateStack.Leave(this, SessionState.OptimisticLockFieldsProcessing);
			}
			foreach(XPRefCollectionHelper c in collectionsMarkedSaved.Keys)
				c.ClearChangesCache();
			collectionsMarkedSaved.Clear();
			objectsMarkedSaved.Clear();
			objectsMarkedDeleted.Clear();
			trackingChanges = false;
		}
		void RollbackVersions() {
			foreach(KeyValuePair<object, int?> ent in objectsMarkedSaved) {
				if(ent.Value != null) {
					XPMemberInfo lockField = GetClassInfo(ent.Key).OptimisticLockField;
					if(lockField != null)
						lockField.SetValue(ent.Key, ent.Value);
				}
			}
		}
		public virtual void RollbackTransaction() {
			LogManager.Log(LogCategory, () => {
				DropChanges();
			}, (d) => {
				return CreateLogMessage("Executing RollbackTransaction()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString()
			});
			}, null);
		}
		public void DropChanges() {
			OnBeforeDropChanges();
			if(!IsUnitOfWork && !TrackingChanges)
				throw new TransactionSequenceException(Res.GetString(Res.Session_TranSequenceRollback));
			SessionStateStack.Enter(this, SessionState.RollbackTransaction);
			try {
				collectionsMarkedSaved.Clear();
				objectsMarkedSaved.Clear();
				objectsMarkedDeleted.Clear();
				trackingChanges = false;
				((IInfrastructure<ISecuredPropertyAccessor>)this).Instance?.ClearAllChangedSecuredProperties();
			}
			finally {
				SessionStateStack.Leave(this, SessionState.RollbackTransaction);
			}
			OnAfterDropChanges();
		}
		public void ExplicitBeginTransaction() {
			((ICommandChannel)this).Do(CommandChannelHelper.Command_ExplicitBeginTransaction, null);
		}
		public Task ExplicitBeginTransactionAsync(CancellationToken cancellationToken = default(CancellationToken)) {
			return ((ICommandChannelAsync)this).DoAsync(CommandChannelHelper.Command_ExplicitBeginTransaction, null, cancellationToken);
		}
		public void ExplicitBeginTransaction(IsolationLevel isolationLevel) {
			((ICommandChannel)this).Do(CommandChannelHelper.Command_ExplicitBeginTransaction, isolationLevel);
		}
		public Task ExplicitBeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default(CancellationToken)) {
			return ((ICommandChannelAsync)this).DoAsync(CommandChannelHelper.Command_ExplicitBeginTransaction, isolationLevel, cancellationToken);
		}
		public void ExplicitCommitTransaction() {
			((ICommandChannel)this).Do(CommandChannelHelper.Command_ExplicitCommitTransaction, null);
		}
		public Task ExplicitCommitTransactionAsync(CancellationToken cancellationToken = default(CancellationToken)) {
			return ((ICommandChannelAsync)this).DoAsync(CommandChannelHelper.Command_ExplicitCommitTransaction, null, cancellationToken);
		}
		public void ExplicitRollbackTransaction() {
			((ICommandChannel)this).Do(CommandChannelHelper.Command_ExplicitRollbackTransaction, null);
		}
		public Task ExplicitRollbackTransactionAsync(CancellationToken cancellationToken = default(CancellationToken)) {
			return ((ICommandChannelAsync)this).DoAsync(CommandChannelHelper.Command_ExplicitRollbackTransaction, null, cancellationToken);
		}
		public XPObjectType GetObjectType(Int32 id) {
			return TypesManager.GetObjectType(id);
		}
		Task<XPObjectType> GetObjectTypeAsync(Int32 id, CancellationToken cancellationToken) {
			return TypesManager.GetObjectTypeAsync(id, AsyncOperationIdentifier.New(), cancellationToken);
		}
		public XPObjectType GetObjectType(object theObject) {
			return GetObjectType(GetClassInfo(theObject));
		}
		public XPObjectType GetObjectType(XPClassInfo objectType) {
			return TypesManager.GetObjectType(objectType);
		}
		Task<XPObjectType> GetObjectTypeAsync(XPClassInfo objectType, CancellationToken cancellationToken) {
			return TypesManager.GetObjectTypeAsync(objectType, AsyncOperationIdentifier.New(), cancellationToken);
		}
		public XPClassInfo GetClassInfo(string assemblyName, string className) {
			return Dictionary.GetClassInfo(assemblyName, className);
		}
		public XPClassInfo GetClassInfo(Type classType) {
			return Dictionary.GetClassInfo(classType);
		}
		public XPClassInfo GetClassInfo<ClassType>() {
			return this.GetClassInfo(typeof(ClassType));
		}
		public XPClassInfo GetClassInfo(object theObject) {
			return Dictionary.GetClassInfo(theObject);
		}
		IDictionary props;
		public PropertyDescriptorCollection GetProperties(XPClassInfo classInfo) {
			if(props == null)
				props = new Dictionary<object, object>();
			PropertyDescriptorCollection prop = (PropertyDescriptorCollection)props[classInfo];
			if(prop == null) {
				prop = new XPPropertyDescriptorCollection(this, classInfo);
				props[classInfo] = prop;
			}
			return prop;
		}
		[Description("Gets or sets the action which is performed when the session is connected to a data store.")]
		[Category("Data")]
		[DefaultValue(AutoCreateOption.DatabaseAndSchema)]
		public AutoCreateOption AutoCreateOption {
			get {
				IObjectLayerEx objectLayerEx = objectLayer as IObjectLayerEx;
				return objectLayerEx == null ? _autoCreateOption : objectLayerEx.AutoCreateOption;
			}
			set {
				if(IsConnected)
					throw new CannotChangePropertyWhenSessionIsConnectedException("autocreate");
				_autoCreateOption = value;
			}
		}
		[Description("Gets or sets an IDbConnection object associated with the session.")]
		[DefaultValue(null)]
		[Category("Data")]
		public IDbConnection Connection {
			get {
				if(this._connection != null)
					return this._connection;
				IObjectLayerEx objectLayerEx = objectLayer as IObjectLayerEx;
				if(objectLayerEx != null)
					return objectLayerEx.Connection;
				return null;
			}
			set {
				if(IsConnected)
					throw new CannotChangePropertyWhenSessionIsConnectedException("connection");
				this._connectionString = string.Empty;
				this._connection = value;
			}
		}
		bool ShouldSerializeConnectionString() {
			return this._connectionString != null && this._connectionString.Length > 0;
		}
		void ResetConnectionString() {
			ConnectionString = null;
		}
		[Description("Gets or sets a connection string.")]
		[Category("Data")]
		public string ConnectionString {
			get {
				if(this._connection != null)
					return this._connection.ConnectionString;
				if(this.IsConnected && this.Connection != null && this.Connection.ConnectionString != null)
					return this.Connection.ConnectionString;
				return this._connectionString;
			}
			set {
				if(IsConnected)
					throw new CannotChangePropertyWhenSessionIsConnectedException("connection string");
				this.Connection = null;
				_connectionString = value;
			}
		}
		[Description("Gets or sets a value which controls the persistent object’s locking during a session.")]
		[DefaultValue(LockingOption.Optimistic)]
		[Category("Options")]
		public LockingOption LockingOption {
			get { return _lockingOption; }
			set {
				_lockingOption = value;
			}
		}
		[Description("Gets or sets a value which specifies how XPO behaves when reloading changed objects (objects with different versions)."), DefaultValue(OptimisticLockingReadBehavior.Default)]
		[Category("Options")]
		public OptimisticLockingReadBehavior OptimisticLockingReadBehavior {
			get { return _OptimisticLockingReadBehavior; }
			set {
				_OptimisticLockingReadBehavior = value;
			}
		}
		[Obsolete("Use GetIdentityMapBehavior() instead")]
		[Browsable(false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public CacheBehavior GetCacheBehavior() {
			return (CacheBehavior)(int)GetIdentityMapBehavior();
		}
		public IdentityMapBehavior GetIdentityMapBehavior() {
			IdentityMapBehavior cacheBehavior = IdentityMapBehavior;
			if(cacheBehavior == IdentityMapBehavior.Default)
				cacheBehavior = XpoDefault.IdentityMapBehavior;
			return cacheBehavior;
		}
		[DefaultValue(CacheBehavior.Default)]
		[Obsolete("Use IdentityMapBehavior instead")]
		[Browsable(false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public CacheBehavior CacheBehavior {
			get { return (CacheBehavior)(int)IdentityMapBehavior; }
			set {
				IdentityMapBehavior = (IdentityMapBehavior)(int)value;
			}
		}
		[Description("Gets or sets the session’s Identity Map behavior."), DefaultValue(IdentityMapBehavior.Default)]
		[Category("Options")]
		public IdentityMapBehavior IdentityMapBehavior {
			get { return _IdentityMapBehavior; }
			set {
				_IdentityMapBehavior = value;
			}
		}
		public void Connect(IDataLayer layer, params IDisposable[] disposeOnDisconnect) {
			Connect(SimpleObjectLayer.FromDataLayer(layer), disposeOnDisconnect);
		}
		public void Connect(IObjectLayer layer, params IDisposable[] disposeOnDisconnect) {
			if(isDisposed)
				throw new ObjectDisposedException(this.GetType().FullName);
			if(IsConnected)
				throw new InvalidOperationException(Res.GetString(Res.Session_AlreadyConnected));
			SessionInitConnected(layer, disposeOnDisconnect);
		}
		void ConnectOldStyle() {
			OnBeforeConnect();
			this.closeConnectionOnDisconnect = false;
			if(this._connection != null) {
				closeConnectionOnDisconnect = _connection.State != ConnectionState.Open;
				this.objectLayer = SimpleObjectLayer.FromDataLayer(XpoDefault.GetDataLayer(this._connection, this.dict, this.AutoCreateOption, out this._DisposeOnDisconnect));
			}
			else {
				this.objectLayer = SimpleObjectLayer.FromDataLayer(XpoDefault.GetDataLayer(this.ConnectionString, this.dict, this.AutoCreateOption, out this._DisposeOnDisconnect));
			}
			this.dict = this.objectLayer.Dictionary;
			OnAfterConnect();
		}
		public void Connect() {
			if(this.isDisposed)
				throw new ObjectDisposedException(this.ToString());
			if(IsConnected)
				return;
			if(this._connection != null || (this._connectionString != null && this._connectionString.Length > 0)) {
				ConnectOldStyle();
			}
			else {
				IObjectLayer layer = XpoDefault.ObjectLayer;
				if(layer == null) {
					layer = SimpleObjectLayer.FromDataLayer(XpoDefault.DataLayer);
				}
				if(layer != null) {
					if(dict != null && dict != layer.Dictionary)
						throw new InvalidOperationException(Res.GetString(Res.Session_DictConstructor));
					Connect(layer);
				}
				else {
					ConnectOldStyle();
				}
			}
			if(Environment.OSVersion.Platform == PlatformID.Win32NT) {
				PerformanceCounters.SessionCount.Increment();
				PerformanceCounters.SessionConnected.Increment();
			}
		}
		[Obsolete("Use DropIdentityMap() instead")]
		[Browsable(false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public void DropCache() {
			DropIdentityMap();
		}
		public void DropIdentityMap() {
			if(TrackingChanges)
				DropChanges();
			if(_IdentityMap != null) {
				_IdentityMap.Clear();
				_IdentityMap = null;
			}
			typesManager = null;
			OnAfterDropIdentityMap();
		}
		void Clear() {
			DropIdentityMap();
			((IInfrastructure<ISecuredPropertyAccessor>)this).Instance?.ClearAllChangedSecuredProperties();
			trackingChanges = false;
			if(this._DisposeOnDisconnect != null) {
				foreach(IDisposable disp in this._DisposeOnDisconnect) {
					if(disp != null) {
						disp.Dispose();
					}
				}
			}
			this.objectLayer = null;
			this._DisposeOnDisconnect = null;
			if(_connection != null && this.closeConnectionOnDisconnect) {
				try {
					this._connection.Close();
				}
				catch { }
			}
		}
		public void Disconnect() {
			if(InTransaction)
				RollbackTransaction();
			if(IsConnected) {
				OnBeforeDisconnect();
				Clear();
				OnAfterDisconnect();
				if(Environment.OSVersion.Platform == PlatformID.Win32NT) {
					PerformanceCounters.SessionCount.Decrement();
					PerformanceCounters.SessionDisconnected.Increment();
				}
			}
		}
		Dictionary<object, object> wideDataDictionary;
		Dictionary<object, object> WideDataDictionary {
			get {
				if(wideDataDictionary == null)
					wideDataDictionary = new Dictionary<object, object>();
				return wideDataDictionary;
			}
		}
		bool IWideDataStorage.WideDataContainsKey(object key) {
			return WideDataDictionary.ContainsKey(key);
		}
		void IWideDataStorage.SetWideDataItem(object key, object value) {
			WideDataDictionary[key] = value;
		}
		object IWideDataStorage.GetWideDataItem(object key) {
			return WideDataDictionary[key];
		}
		bool IWideDataStorage.TryGetWideDataItem(object key, out object value) {
			return WideDataDictionary.TryGetValue(key, out value);
		}
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public IObjectLayer ObjectLayer {
			get {
				if(!IsConnected) {
					if(IsDesignMode)
						throw new InvalidOperationException();
					Connect();
				}
				return objectLayer;
			}
		}
		protected IObjectLayerAsync GetObjectLayerAsync() {
			if(ObjectLayer == null) {
				return null;
			}
			var objectLayerAsync = ObjectLayer as IObjectLayerAsync;
			if(objectLayerAsync == null) {
				throw new InvalidOperationException(Xpo.Res.GetString(Xpo.Res.Async_ObjectLayerDoesNotImplementIObjectLayerAsync, ObjectLayer.GetType().FullName));
			}
			return objectLayerAsync;
		}
		public UpdateSchemaResult UpdateSchema(bool doNotCreateIfFirstTableNotExist, params XPClassInfo[] types) {
			return ((IObjectLayerEx)ObjectLayer).UpdateSchema(doNotCreateIfFirstTableNotExist, types);
		}
		public Task<UpdateSchemaResult> UpdateSchemaAsync(CancellationToken cancellationToken, bool doNotCreateIfFirstTableNotExist, params XPClassInfo[] types) {
			return GetObjectLayerAsync().UpdateSchemaAsync(cancellationToken, doNotCreateIfFirstTableNotExist, types);
		}
		public void UpdateSchema(params XPClassInfo[] types) {
			UpdateSchema(false, types);
		}
		public Task UpdateSchemaAsync(CancellationToken cancellationToken, params XPClassInfo[] types) {
			return UpdateSchemaAsync(cancellationToken, false, types);
		}
		public void UpdateSchema(params Type[] types) {
			UpdateSchema(Dictionary.CollectClassInfos(types));
		}
		public Task UpdateSchemaAsync(CancellationToken cancellationToken, params Type[] types) {
			return UpdateSchemaAsync(cancellationToken, Dictionary.CollectClassInfos(types));
		}
		public void UpdateSchema(params Assembly[] assemblies) {
			UpdateSchema(Dictionary.CollectClassInfos(assemblies));
		}
		public Task UpdateSchemaAsync(CancellationToken cancellationToken, params Assembly[] assemblies) {
			return UpdateSchemaAsync(cancellationToken, Dictionary.CollectClassInfos(assemblies));
		}
		static Assembly[] GetNonXpoAssemblies(Assembly[] input) {
			if(input == null)
				throw new ArgumentNullException(nameof(input));
			List<Assembly> result = new List<Assembly>(input.Length);
			foreach(Assembly a in input) {
				if(a != typeof(Session).Assembly)
					result.Add(a);
			}
			return result.ToArray();
		}
		internal static Assembly[] GetNonXpoAssemblies() {
			return GetNonXpoAssemblies(AppDomain.CurrentDomain.GetAssemblies());
		}
		public void UpdateSchema() {
			UpdateSchema(GetNonXpoAssemblies());
		}
		public Task UpdateSchemaAsync(CancellationToken cancellationToken = default(CancellationToken)) {
			return UpdateSchemaAsync(cancellationToken, GetNonXpoAssemblies());
		}
		public void CreateObjectTypeRecords() {
			CreateObjectTypeRecords(false);
		}
		public Task CreateObjectTypeRecordsAsync(CancellationToken cancellationToken = default(CancellationToken)) {
			return CreateObjectTypeRecordsAsync(false, cancellationToken);
		}
		public void CreateObjectTypeRecords(bool createOnlyNecessary) {
			CreateObjectTypeRecords(createOnlyNecessary, GetNonXpoAssemblies());
		}
		public Task CreateObjectTypeRecordsAsync(bool createOnlyNecessary, CancellationToken cancellationToken) {
			return CreateObjectTypeRecordsAsync(cancellationToken, createOnlyNecessary, GetNonXpoAssemblies());
		}
		public void CreateObjectTypeRecords(params XPClassInfo[] types) {
			CreateObjectTypeRecords(false, types);
		}
		public Task CreateObjectTypeRecordsAsync(CancellationToken cancellationToken, params XPClassInfo[] types) {
			return CreateObjectTypeRecordsAsync(cancellationToken, false, types);
		}
		public void CreateObjectTypeRecords(bool createOnlyNecessary, params XPClassInfo[] types) {
			if(types == null)
				return;
			foreach(XPClassInfo type in types) {
				if(type.ClassType is IXPOServiceClass || (createOnlyNecessary && !type.IsTypedObject && !type.IsGCRecordObject && !type.HasPurgebleObjectReferences()))
					continue;
				GetObjectType(type);
			}
		}
		public async Task CreateObjectTypeRecordsAsync(CancellationToken cancellationToken, bool createOnlyNecessary, params XPClassInfo[] types) {
			if(types == null)
				return;
			foreach(XPClassInfo type in types) {
				if(type.ClassType is IXPOServiceClass || (createOnlyNecessary && !type.IsTypedObject && !type.IsGCRecordObject && !type.HasPurgebleObjectReferences()))
					continue;
				await GetObjectTypeAsync(type, cancellationToken);
			}
		}
		public void CreateObjectTypeRecords(params Type[] types) {
			CreateObjectTypeRecords(false, types);
		}
		public Task CreateObjectTypeRecordsAsync(CancellationToken cancellationToken, params Type[] types) {
			return CreateObjectTypeRecordsAsync(cancellationToken, false, types);
		}
		public void CreateObjectTypeRecords(bool createOnlyNecessary, params Type[] types) {
			CreateObjectTypeRecords(createOnlyNecessary, Dictionary.CollectClassInfos(types));
		}
		public Task CreateObjectTypeRecordsAsync(CancellationToken cancellationToken, bool createOnlyNecessary, params Type[] types) {
			return CreateObjectTypeRecordsAsync(cancellationToken, createOnlyNecessary, Dictionary.CollectClassInfos(types));
		}
		public void CreateObjectTypeRecords(params Assembly[] assemblies) {
			CreateObjectTypeRecords(false, assemblies);
		}
		public Task CreateObjectTypeRecordsAsync(CancellationToken cancellationToken, params Assembly[] assemblies) {
			return CreateObjectTypeRecordsAsync(cancellationToken, false, assemblies);
		}
		public void CreateObjectTypeRecords(bool createOnlyNecessary, params Assembly[] assemblies) {
			CreateObjectTypeRecords(createOnlyNecessary, Dictionary.CollectClassInfos(assemblies));
		}
		public Task CreateObjectTypeRecordsAsync(CancellationToken cancellationToken, bool createOnlyNecessary, params Assembly[] assemblies) {
			return CreateObjectTypeRecordsAsync(cancellationToken, createOnlyNecessary, Dictionary.CollectClassInfos(assemblies));
		}
		[Browsable(false)]
		public virtual XPDictionary Dictionary {
			get {
				if(IsConnected)
					return dict;
				if(IsDesignMode2) {
					if(dict == null) {
						if(this is DefaultSession)
							dict = CreateDesignTimeDictionary(Site);
						else
							throw new InvalidOperationException();
					}
				}
				else
					Connect();
				return dict;
			}
		}
		[Browsable(false)]
		public bool IsConnected {
			get { return objectLayer != null; }
		}
		[Browsable(false)]
		public XPObjectTypesManager TypesManager {
			get {
				if(typesManager == null)
					typesManager = new XPObjectTypesManager(this);
				return typesManager;
			}
		}
		[Browsable(false)]
		public virtual bool InTransaction {
			get { return TrackingChanges; }
		}
		[Browsable(false)]
		public bool TrackingChanges {
			get { return trackingChanges; }
		}
		[Obsolete("Use ObjectSaving event instead", true), EditorBrowsable(EditorBrowsableState.Never), Browsable(false)]
		public event ObjectManipulationEventHandler BeforeSave {
			add { ObjectSaving += value; }
			remove { ObjectSaving -= value; }
		}
		public event ObjectManipulationEventHandler ObjectSaving;
		public event ObjectManipulationEventHandler ObjectSaved;
		public event ObjectManipulationEventHandler ObjectLoading;
		public event ObjectManipulationEventHandler ObjectLoaded;
		public event ObjectManipulationEventHandler ObjectDeleting;
		public event ObjectManipulationEventHandler ObjectDeleted;
		public event ObjectChangeEventHandler ObjectChanged;
		public event ObjectsManipulationEventHandler ObjectsSaved;
		public event ObjectsManipulationEventHandler ObjectsLoaded;
		[Description("Occurs before the connection to a database is established via the Connect method")]
		public event SessionManipulationEventHandler BeforeConnect;
		[Description("Occurs after the connection to a database has been established via the Connect method")]
		public event SessionManipulationEventHandler AfterConnect;
		[Description("Occurs before the connection to a database is detached via the Disconnect method")]
		public event SessionManipulationEventHandler BeforeDisconnect;
		[Description("Occurs after the connection to a database is detached via the Disconnect method")]
		public event SessionManipulationEventHandler AfterDisconnect;
		[Description("Occurs before the transaction starts via the BeginTransaction method")]
		public event SessionManipulationEventHandler BeforeBeginTransaction;
		[Description("Occurs after the transaction has started via the BeginTransaction method")]
		public event SessionManipulationEventHandler AfterBeginTransaction;
		[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
		public event SessionManipulationEventHandler BeforePreProcessCommitedList;
		[Description("Occurs before the transaction is completed via the CommitTransaction method")]
		public event SessionManipulationEventHandler BeforeCommitTransaction;
		[Description("Occurs after the transaction has completed via the CommitTransaction method")]
		public event SessionManipulationEventHandler AfterCommitTransaction;
		[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
		public event SessionManipulationEventHandler BeforeCommitNestedUnitOfWork;
		[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
		public event SessionManipulationEventHandler AfterCommitNestedUnitOfWork;
		[Description("Occurs before the transaction is finished via the RollbackTransaction method")]
		public event SessionManipulationEventHandler BeforeRollbackTransaction;
		[Description("Occurs after the transaction has completed via the RollbackTransaction method")]
		public event SessionManipulationEventHandler AfterRollbackTransaction;
		[Description("Occurs before the tracking changes mode starts via the BeginTrackingChanges method")]
		public event SessionManipulationEventHandler BeforeBeginTrackingChanges;
		[Description("Occurs after the tracking changes mode starts via the BeginTrackingChanges method")]
		public event SessionManipulationEventHandler AfterBeginTrackingChanges;
		[Description("Occurs before changes are flushed via the FlushChanges method")]
		public event SessionManipulationEventHandler BeforeFlushChanges;
		[Description("Occurs after changes are flushed via the FlushChanges method")]
		public event SessionManipulationEventHandler AfterFlushChanges;
		[Description("Occurs before changes are dropped via the DropChanges method")]
		public event SessionManipulationEventHandler BeforeDropChanges;
		[Description("Occurs after changes are dropped via the DropChanges method")]
		public event SessionManipulationEventHandler AfterDropChanges;
		public event SessionOperationFailEventHandler FailedCommitTransaction;
		public event SessionOperationFailEventHandler FailedFlushChanges;
		internal event SessionManipulationEventHandler AfterDropIdentityMap;
		public object GetKeyValue(object theObject) {
			if(theObject == null)
				throw new ArgumentNullException(LogParam_TheObject);
			return Dictionary.GetId(theObject);
		}
		public void SetKeyValue(object theObject, object keyValue) {
			if(theObject == null)
				throw new ArgumentNullException(LogParam_TheObject);
			this.GetClassInfo(theObject).KeyProperty.SetValue(theObject, keyValue);
		}
		[Obsolete("Use BulkLoad method instead", true), EditorBrowsable(EditorBrowsableState.Never)]
		public void Load(params XPBaseCollection[] collections) {
			BulkLoad(collections);
		}
		public void BulkLoad(params XPBaseCollection[] collections) {
			BulkLoad((IXPBulkLoadableCollection[])collections);
		}
		public Task BulkLoadAsync(CancellationToken cancellationToken = default(CancellationToken), params XPBaseCollection[] collections) {
			return BulkLoadAsync(cancellationToken, (IXPBulkLoadableCollection[])collections);
		}
		public void BulkLoad(params IXPBulkLoadableCollection[] collections) {
			if(collections == null)
				throw new ArgumentNullException(nameof(collections));
			List<ObjectsQuery> queries = new List<ObjectsQuery>();
			List<IXPBulkLoadableCollection> colls = new List<IXPBulkLoadableCollection>();
			for(int i = 0; i < collections.Length; i++) {
				if(collections[i] == null)
					continue;
				ObjectsQuery query = collections[i].BeginLoad();
				if(query == null)
					continue;
				queries.Add(query);
				colls.Add(collections[i]);
			}
			ICollection[] obejcts = GetObjects(queries.ToArray());
			for(int i = 0; i < obejcts.Length; i++) {
				colls[i].EndLoad(obejcts[i]);
			}
		}
		public async Task BulkLoadAsync(CancellationToken cancellationToken = default(CancellationToken), params IXPBulkLoadableCollection[] collections) {
			if(collections == null)
				throw new ArgumentNullException(nameof(collections));
			List<ObjectsQuery> queries = new List<ObjectsQuery>();
			List<IXPBulkLoadableCollection> colls = new List<IXPBulkLoadableCollection>();
			for(int i = 0; i < collections.Length; i++) {
				if(collections[i] == null)
					continue;
				ObjectsQuery query = collections[i].BeginLoad();
				if(query == null)
					continue;
				queries.Add(query);
				colls.Add(collections[i]);
			}
			ICollection[] obejcts = await GetObjectsAsync(queries.ToArray(), cancellationToken);
			for(int i = 0; i < obejcts.Length; i++) {
				colls[i].EndLoad(obejcts[i]);
			}
		}
		protected virtual internal bool IsUnitOfWork {
			get { return false; }
		}
		static int seq = 0;
		int seqNum = Interlocked.Increment(ref seq);
		public override string ToString() {
			return base.ToString() + '(' + seqNum.ToString() + ')';
		}
		protected internal virtual MemberInfoCollection GetPropertiesListForUpdateInsert(object theObject, bool isUpdate, bool addDelayedReference) {
			return XPClassInfo.GetPropertiesListForUpdateInsert(this, theObject, isUpdate, addDelayedReference);
		}
		object IPersistentValueExtractor.ExtractPersistentValue(object criterionValue) {
			ThrowIfObjectFromDifferentSession(criterionValue);
			if(Dictionary.QueryClassInfo(criterionValue) == null)
				return criterionValue;
			else if(IsNewObject(criterionValue))
				return null;
			else
				return GetKeyValue(criterionValue);
		}
		Session ISessionProvider.Session {
			get { return this; }
		}
		[Obsolete("Use PreFetch method instead", true), EditorBrowsable(EditorBrowsableState.Never)]
		public void Load<T>(IEnumerable<T> objects, params string[] collections) where T : class {
			PreFetch(objects, collections);
		}
		public void PreFetch<T>(IEnumerable<T> objects, params string[] propertyPaths) where T : class {
			PreFetch(GetClassInfo<T>(), objects, propertyPaths);
		}
		public Task PreFetchAsync<T>(IEnumerable<T> objects, CancellationToken cancellationToken, params string[] propertyPaths) {
			return PreFetchAsync(GetClassInfo<T>(), objects, cancellationToken, propertyPaths);
		}
		[Obsolete("Use PreFetch method instead", true), EditorBrowsable(EditorBrowsableState.Never)]
		public void Load(XPClassInfo classInfo, IEnumerable objects, params string[] collections) {
			PreFetch(classInfo, objects, collections);
		}
		public void PreFetch(XPClassInfo classInfo, IEnumerable objects, params string[] propertyPaths) {
			ICollection uniqueObjects = null;
			ObjectsUniquer(ref uniqueObjects, objects);
			foreach(string path in propertyPaths) {
				string[] membersPath = MemberInfoCollection.SplitPath(path);
				if(membersPath.Length == 0)
					continue;
				XPMemberInfo member = MemberInfoCollection.FindMember(classInfo, membersPath[0]);
				if(member.IsAssociationList || member.IsManyToManyAlias) {
					XPMemberInfo coreMember = member.IsAssociationList ? member : classInfo.GetMember(((ManyToManyAliasAttribute)member.GetAttributeInfo(typeof(ManyToManyAliasAttribute))).OneToManyCollectionName);
					PreFetchCore(classInfo, uniqueObjects, coreMember);
					if(membersPath.Length > 1) {
						List<object> subObjects = new List<object>();
						if(classInfo.IsAssignableTo(member.Owner)) {
							foreach(object obj in uniqueObjects) {
								ICollection collection = (ICollection)member.GetValue(obj);
								subObjects.AddRange(ListHelper.FromCollection(collection));
							}
						}
						else {
							foreach(object obj in uniqueObjects) {
								XPClassInfo objClassInfo = GetClassInfo(obj);
								if(objClassInfo.IsAssignableTo(member.Owner)) {
									ICollection collection = (ICollection)member.GetValue(obj);
									subObjects.AddRange(ListHelper.FromCollection(collection));
								}
							}
						}
						if(subObjects.Count > 0) {
							PreFetch(member.CollectionElementType, subObjects, string.Join(".", membersPath, 1, membersPath.Length - 1));
						}
					}
				}
				else {
					if(member.IsPersistent && member.IsDelayed) {
						if(member.ReferenceType == null) {
							XPDelayedProperty.PreFetchGenericProps(this, uniqueObjects, member);
						}
						else {
							XPDelayedProperty.PreFetchRefProps(this, uniqueObjects, member);
						}
					}
					if(membersPath.Length > 1 && member.ReferenceType != null) {
						IList subObjects = new List<object>();
						if(classInfo.IsAssignableTo(member.Owner)) {
							foreach(object obj in uniqueObjects) {
								subObjects.Add(member.GetValue(obj));
							}
						}
						else {
							foreach(object obj in uniqueObjects) {
								XPClassInfo objClassInfo = GetClassInfo(obj);
								if(objClassInfo.IsAssignableTo(member.Owner)) {
									subObjects.Add(member.GetValue(obj));
								}
							}
						}
						if(subObjects.Count > 0) {
							PreFetch(member.ReferenceType, subObjects, string.Join(".", membersPath, 1, membersPath.Length - 1));
						}
					}
				}
			}
		}
		public async Task PreFetchAsync(XPClassInfo classInfo, IEnumerable objects, CancellationToken cancellationToken, params string[] propertyPaths) {
			ICollection uniqueObjects = null;
			ObjectsUniquer(ref uniqueObjects, objects);
			foreach(string path in propertyPaths) {
				string[] membersPath = MemberInfoCollection.SplitPath(path);
				if(membersPath.Length == 0)
					continue;
				XPMemberInfo member = MemberInfoCollection.FindMember(classInfo, membersPath[0]);
				if(member.IsAssociationList || member.IsManyToManyAlias) {
					XPMemberInfo coreMember = member.IsAssociationList ? member : classInfo.GetMember(((ManyToManyAliasAttribute)member.GetAttributeInfo(typeof(ManyToManyAliasAttribute))).OneToManyCollectionName);
					cancellationToken.ThrowIfCancellationRequested();
					await PreFetchCoreAsync(classInfo, uniqueObjects, coreMember, cancellationToken);
					if(membersPath.Length > 1) {
						List<object> subObjects = new List<object>();
						if(classInfo.IsAssignableTo(member.Owner)) {
							foreach(object obj in uniqueObjects) {
								ICollection collection = (ICollection)member.GetValue(obj);
								subObjects.AddRange(ListHelper.FromCollection(collection));
							}
						}
						else {
							foreach(object obj in uniqueObjects) {
								XPClassInfo objClassInfo = GetClassInfo(obj);
								if(objClassInfo.IsAssignableTo(member.Owner)) {
									ICollection collection = (ICollection)member.GetValue(obj);
									subObjects.AddRange(ListHelper.FromCollection(collection));
								}
							}
						}
						cancellationToken.ThrowIfCancellationRequested();
						if(subObjects.Count > 0) {
							await PreFetchAsync(member.CollectionElementType, subObjects, cancellationToken, string.Join(".", membersPath, 1, membersPath.Length - 1));
						}
					}
				}
				else {
					if(member.IsPersistent && member.IsDelayed) {
						if(member.ReferenceType == null) {
							await XPDelayedProperty.PreFetchGenericPropsAsync(this, uniqueObjects, member, cancellationToken);
						}
						else {
							await XPDelayedProperty.PreFetchRefPropsAsync(this, uniqueObjects, member, cancellationToken);
						}
					}
					if(membersPath.Length > 1 && member.ReferenceType != null) {
						IList subObjects = new List<object>();
						if(classInfo.IsAssignableTo(member.Owner)) {
							foreach(object obj in uniqueObjects) {
								subObjects.Add(member.GetValue(obj));
							}
						}
						else {
							foreach(object obj in uniqueObjects) {
								XPClassInfo objClassInfo = GetClassInfo(obj);
								if(objClassInfo.IsAssignableTo(member.Owner)) {
									subObjects.Add(member.GetValue(obj));
								}
							}
						}
						cancellationToken.ThrowIfCancellationRequested();
						if(subObjects.Count > 0) {
							await PreFetchAsync(member.ReferenceType, subObjects, cancellationToken, string.Join(".", membersPath, 1, membersPath.Length - 1));
						}
					}
				}
			}
		}
		void ObjectsUniquer(ref ICollection uniqueObjects, IEnumerable nonUniqueObjects) {
			if(uniqueObjects != null)
				return;
			ObjectSet result = new ObjectSet();
			foreach(object obj in nonUniqueObjects) {
				if(obj == null)
					continue;
				ThrowIfObjectFromDifferentSession(obj);
				if(IsNewObject(obj))
					continue;
				result.Add(obj);
			}
			uniqueObjects = result;
		}
		internal void ThrowIfObjectFromDifferentSession(object obj) {
			ISessionProvider sessionObj = obj as ISessionProvider;
			if(sessionObj != null && sessionObj.Session != this)
				throw new SessionMixingException(sessionObj.Session, obj);
		}
		void PreFetchCore(XPClassInfo classInfo, IEnumerable objects, XPMemberInfo member) {
			List<ObjectsQuery> queries;
			ObjectDictionary<ObjectSet> objectsByOwners;
			XPClassInfo loadedClassInfo;
			if(!PreFetchCoreBegin(classInfo, objects, member, out queries, out objectsByOwners, out loadedClassInfo)) {
				return;
			}
			ICollection[] results = GetObjects(queries.ToArray());
			PreFetchCoreEnd(classInfo, objects, member, loadedClassInfo, objectsByOwners, results);
		}
		async Task PreFetchCoreAsync(XPClassInfo classInfo, IEnumerable objects, XPMemberInfo member, CancellationToken cancellationToken = default(CancellationToken)) {
			List<ObjectsQuery> queries;
			ObjectDictionary<ObjectSet> objectsByOwners;
			XPClassInfo loadedClassInfo;
			if(!PreFetchCoreBegin(classInfo, objects, member, out queries, out objectsByOwners, out loadedClassInfo)) {
				return;
			}
			cancellationToken.ThrowIfCancellationRequested();
			ICollection[] results = await GetObjectsAsync(queries.ToArray(), cancellationToken);
			PreFetchCoreEnd(classInfo, objects, member, loadedClassInfo, objectsByOwners, results);
		}
		bool PreFetchCoreBegin(XPClassInfo classInfo, IEnumerable objects, XPMemberInfo member, out List<ObjectsQuery> queries, out ObjectDictionary<ObjectSet> objectsByOwners, out XPClassInfo loadedClassInfo) {
			queries = null;
			objectsByOwners = null;
			loadedClassInfo = null;
			if(member.IsManyToManyAlias) {
				ManyToManyAliasAttribute mm = (ManyToManyAliasAttribute)member.GetAttributeInfo(typeof(ManyToManyAliasAttribute));
				XPMemberInfo aliasedMI = classInfo.GetMember(mm.OneToManyCollectionName);
				PreFetchCore(classInfo, objects, aliasedMI);
				return false;
			}
			List<object> ids = new List<object>();
			objectsByOwners = new ObjectDictionary<ObjectSet>();
			foreach(object obj in objects) {
				ThrowIfObjectFromDifferentSession(obj);
				IXPPrefetchableAssociationList coll = member.GetValue(obj) as IXPPrefetchableAssociationList;
				if(coll == null)
					continue;
				if(!coll.NeedPrefetch())
					continue;
				ids.Add(GetKeyValue(obj));
				objectsByOwners.Add(obj, new ObjectSet());
			}
			if(ids.Count == 0)
				return false;
			int n;
			queries = new List<ObjectsQuery>();
			loadedClassInfo = member.IsManyToMany ? member.IntermediateClass : member.CollectionElementType;
			for(int i = 0; i < ids.Count; i += n) {
				n = XpoDefault.GetTerminalInSize(ids.Count - i);
				queries.Add(new ObjectsQuery(loadedClassInfo, new InOperator(member.GetAssociatedMember().Name, GetRangeHelper.GetRange(ids, i, n)), null, 0, 0, new CollectionCriteriaPatcher(false, TypesManager), false));
			}
			return true;
		}
		void PreFetchCoreEnd(XPClassInfo classInfo, IEnumerable objects, XPMemberInfo member, XPClassInfo loadedClassInfo, ObjectDictionary<ObjectSet> objectsByOwners, ICollection[] results) {
			XPMemberInfo relatedMember = member.IsManyToMany ? member.IntermediateClass.GetMember(member.GetAssociatedMember().Name) : member.GetAssociatedMember();
			XPMemberInfo gcMember = loadedClassInfo.FindMember(GCRecordField.StaticName);
			foreach(ICollection list in results) {
				foreach(object obj in list) {
					if(IsObjectToDelete(obj, true))
						continue;
					if(gcMember != null && gcMember.GetValue(obj) != null)
						continue;
					object parent = relatedMember.GetValue(obj);
					if(parent == null)
						continue;
					ObjectSet hint;
					if(!objectsByOwners.TryGetValue(parent, out hint))
						continue;
					hint.Add(obj);
				}
			}
			foreach(object obj in GetObjectsToSave(true)) {
				if(IsObjectToDelete(obj, true))
					continue;
				if(!GetClassInfo(obj).IsAssignableTo(loadedClassInfo))
					continue;
				if(gcMember != null && gcMember.GetValue(obj) != null)
					continue;
				object parent = relatedMember.GetValue(obj);
				if(parent == null)
					continue;
				ObjectSet hint;
				if(!objectsByOwners.TryGetValue(parent, out hint))
					continue;
				hint.Add(obj);
			}
			foreach(KeyValuePair<object, ObjectSet> pair in objectsByOwners) {
				IXPPrefetchableAssociationList coll = (IXPPrefetchableAssociationList)member.GetValue(pair.Key);
				coll.FinishPrefetch(pair.Value);
			}
		}
		public void PreFetch(IEnumerable objects, XPMemberInfo collectionInObjects, IEnumerable collectionsContent) {
			if(collectionInObjects == null)
				throw new ArgumentNullException(nameof(collectionInObjects));
			if(!collectionInObjects.IsAssociationList)
				throw new ArgumentException(Res.GetString(Res.Session_AssociationListExpected), nameof(collectionInObjects));
			IXPBulkLoadableCollection bl1 = objects as IXPBulkLoadableCollection;
			IXPBulkLoadableCollection bl2 = collectionsContent as IXPBulkLoadableCollection;
			if(bl1 != null && bl2 != null && bl1 != bl2)
				BulkLoad(bl1, bl2);
			ObjectDictionary<ObjectSet> map = new ObjectDictionary<ObjectSet>();
			foreach(object owner in objects) {
				IXPPrefetchableAssociationList pref = collectionInObjects.GetValue(owner) as IXPPrefetchableAssociationList;
				if(pref == null)
					continue;
				if(!pref.NeedPrefetch())
					continue;
				map.Add(owner, new ObjectSet());
			}
			if(map.Count == 0)
				return;
			XPMemberInfo assMember = collectionInObjects.GetAssociatedMember();
			foreach(object obj in collectionsContent) {
				object owner = assMember.GetValue(obj);
				if(owner == null)
					continue;
				ObjectSet set;
				if(!map.TryGetValue(owner, out set))
					continue;
				set.Add(obj);
			}
			foreach(KeyValuePair<object, ObjectSet> pair in map) {
				IXPPrefetchableAssociationList pref = (IXPPrefetchableAssociationList)collectionInObjects.GetValue(pair.Key);
				pref.FinishPrefetch(pair.Value);
			}
		}
		public async Task PreFetchAsync(IEnumerable objects, XPMemberInfo collectionInObjects, IEnumerable collectionsContent, CancellationToken cancellationToken = default(CancellationToken)) {
			if(collectionInObjects == null)
				throw new ArgumentNullException(nameof(collectionInObjects));
			if(!collectionInObjects.IsAssociationList)
				throw new ArgumentException(Res.GetString(Res.Session_AssociationListExpected), nameof(collectionInObjects));
			IXPBulkLoadableCollection bl1 = objects as IXPBulkLoadableCollection;
			IXPBulkLoadableCollection bl2 = collectionsContent as IXPBulkLoadableCollection;
			if(bl1 != null && bl2 != null && bl1 != bl2)
				await BulkLoadAsync(cancellationToken, bl1, bl2);
			ObjectDictionary<ObjectSet> map = new ObjectDictionary<ObjectSet>();
			foreach(object owner in objects) {
				IXPPrefetchableAssociationList pref = collectionInObjects.GetValue(owner) as IXPPrefetchableAssociationList;
				if(pref == null)
					continue;
				if(!pref.NeedPrefetch())
					continue;
				map.Add(owner, new ObjectSet());
			}
			if(map.Count == 0)
				return;
			XPMemberInfo assMember = collectionInObjects.GetAssociatedMember();
			foreach(object obj in collectionsContent) {
				object owner = assMember.GetValue(obj);
				if(owner == null)
					continue;
				ObjectSet set;
				if(!map.TryGetValue(owner, out set))
					continue;
				set.Add(obj);
			}
			foreach(KeyValuePair<object, ObjectSet> pair in map) {
				IXPPrefetchableAssociationList pref = (IXPPrefetchableAssociationList)collectionInObjects.GetValue(pair.Key);
				pref.FinishPrefetch(pair.Value);
			}
		}
		public void PreFetch<T>(IEnumerable<T> objects, string collectionInObjects, IEnumerable collectionsContent) {
			XPMemberInfo cMember = GetClassInfo<T>().GetMember(collectionInObjects);
			PreFetch(objects, cMember, collectionsContent);
		}
		public Task PreFetchAsync<T>(IEnumerable<T> objects, string collectionInObjects, IEnumerable collectionsContent, CancellationToken cancellationToken) {
			XPMemberInfo cMember = GetClassInfo<T>().GetMember(collectionInObjects);
			return PreFetchAsync(objects, cMember, collectionsContent, cancellationToken);
		}
		protected internal virtual bool SuppressExceptionOnReferredObjectAbsentInDataStore {
			get { return GlobalSuppressExceptionOnReferredObjectAbsentInDataStore; }
		}
		[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
		public static bool GlobalSuppressExceptionOnReferredObjectAbsentInDataStore = false;
		bool? _isDesignMode;
		internal bool IsDesignMode {
			get {
				if(_isDesignMode == true)
					return true;
				return DevExpress.Data.Helpers.IsDesignModeHelper.GetIsDesignModeBypassable(this, ref _isDesignMode);
			}
		}
		bool IsDesignMode2 {
			get {
				return IsDesignMode;
			}
		}
		IDictionary<XPBaseCollection, object> mutedCollections;
		internal void MuteCollection(XPBaseCollection c) {
			if(mutedCollections == null)
				mutedCollections = new Dictionary<XPBaseCollection, object>();
			else if(mutedCollections.ContainsKey(c))
				return;
			mutedCollections.Add(c, c);
			c.SuspendChangedEvents();
		}
		void UnMuteCollections() {
			if(mutedCollections == null)
				return;
			ICollection<XPBaseCollection> toUnMute = mutedCollections.Keys;
			mutedCollections = null;
			List<Exception> exceptions = new List<Exception>();
			foreach(XPBaseCollection unMuted in toUnMute) {
				try {
					unMuted.ResumeChangedEvents();
				}
				catch(Exception e) {
					exceptions.Add(e);
				}
			}
			if(exceptions.Count > 0)
				throw new ExceptionBundleException(exceptions.ToArray());
		}
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public IDataLayer DataLayer {
			get {
				IObjectLayerEx objectLayerEx = ObjectLayer as IObjectLayerEx;
				return objectLayerEx == null ? null : objectLayerEx.DataLayer;
			}
		}
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public IServiceProvider ServiceProvider {
			get {
				return serviceProvider;
			}
		}
		public ICollection<T> GetObjectsFromSprocParametrized<T>(string sprocName, params SprocParameter[] parameters) {
			return ListHelper.FromCollection<T>(GetObjectsFromSprocParametrized(GetClassInfo<T>(), sprocName, parameters));
		}
		public async Task<ICollection<T>> GetObjectsFromSprocParametrizedAsync<T>(CancellationToken cancellationToken, string sprocName, params SprocParameter[] parameters) {
			ICollection collection = await GetObjectsFromSprocParametrizedAsync(cancellationToken, GetClassInfo<T>(), sprocName, parameters).ConfigureAwait(false);
			return ListHelper.FromCollection<T>(collection);
		}
		public ICollection GetObjectsFromSprocParametrized(XPClassInfo classInfo, string sprocName, params SprocParameter[] parameters) {
			return GetObjectsFromSprocParametrized(classInfo, classInfo.PersistentProperties as List<XPMemberInfo>, sprocName, parameters);
		}
		public Task<ICollection> GetObjectsFromSprocParametrizedAsync(CancellationToken cancellationToken, XPClassInfo classInfo, string sprocName, params SprocParameter[] parameters) {
			return GetObjectsFromSprocParametrizedAsync(cancellationToken, classInfo, (List<XPMemberInfo>)classInfo.PersistentProperties, sprocName, parameters);
		}
		public ICollection<T> GetObjectsFromSprocParametrized<T>(List<XPMemberInfo> members, string sprocName, params SprocParameter[] parameters) {
			return ListHelper.FromCollection<T>(GetObjectsFromSprocParametrized(GetClassInfo<T>(), members, sprocName, parameters));
		}
		public async Task<ICollection<T>> GetObjectsFromSprocParametrizedAsync<T>(CancellationToken cancellationToken, List<XPMemberInfo> members, string sprocName, params SprocParameter[] parameters) {
			ICollection collection = await GetObjectsFromSprocParametrizedAsync(cancellationToken, GetClassInfo<T>(), members, sprocName, parameters).ConfigureAwait(false);
			return ListHelper.FromCollection<T>(collection);
		}
		public ICollection GetObjectsFromSprocParametrized(XPClassInfo classInfo, List<XPMemberInfo> members, string sprocName, params SprocParameter[] parameters) {
			return LogManager.Log<ICollection>(LogCategory, () => {
				Dictionary<XPMemberInfo, int> referenceIndexDict = CheckClassInfoAndMembers(classInfo, members);
				SelectedData sprocResultData = ExecuteSprocParametrized(sprocName, parameters);
				return GetObjectsHelper.GetObjectsFromData(this, classInfo, members, null, sprocResultData, referenceIndexDict);
			}, (d) => {
				return CreateLogMessage("Executing GetObjectsFromSprocParametrized()", d, new object[] {
					LogParam_SessionType, this.GetType().ToString(), LogParam_SessionID, this.ToString(), LogParam_ClassInfo, classInfo.FullName,
					LogParam_Members, LogMessage.CollectionToString<XPMemberInfo>(members, mi => mi == null ? "null" : mi.Name ),
					LogParam_SprocName, sprocName,
					LogParam_Parameters, LogMessage.CriteriaOperatorCollectionToString<OperandValue>(parameters)
				});
			});
		}
		public Task<ICollection> GetObjectsFromSprocParametrizedAsync(CancellationToken cancellationToken, XPClassInfo classInfo, List<XPMemberInfo> members, string sprocName, params SprocParameter[] parameters) {
			return LogManager.LogAsync<ICollection>(LogCategory, async () => {
				Dictionary<XPMemberInfo, int> referenceIndexDict = CheckClassInfoAndMembers(classInfo, members);
				SelectedData sprocResultData = await ExecuteSprocParametrizedAsync(cancellationToken, sprocName, parameters);
				return await GetObjectsHelper.GetObjectsFromDataAsync(this, classInfo, members, null, sprocResultData, referenceIndexDict, cancellationToken);
			}, (d) => {
				return CreateLogMessage("Executing GetObjectsFromSprocParametrizedAsync()", d, new object[] {
					LogParam_SessionType, this.GetType(),
					LogParam_SessionID, this.ToString(),
					LogParam_ClassInfo, classInfo.FullName,
					LogParam_Members, LogMessage.CollectionToString<XPMemberInfo>(members, mi => mi == null ? "null" : mi.Name),
					LogParam_SprocName, sprocName,
					LogParam_Parameters, LogMessage.CriteriaOperatorCollectionToString<OperandValue>(parameters)
				});
			});
		}
		public ICollection<T> GetObjectsFromSproc<T>(string sprocName, params OperandValue[] parameters) {
			return ListHelper.FromCollection<T>(GetObjectsFromSproc(GetClassInfo<T>(), sprocName, parameters));
		}
		public async Task<ICollection<T>> GetObjectsFromSprocAsync<T>(CancellationToken cancellationToken, string sprocName, params OperandValue[] parameters) {
			ICollection collection = await GetObjectsFromSprocAsync(cancellationToken, GetClassInfo<T>(), sprocName, parameters).ConfigureAwait(false);
			return ListHelper.FromCollection<T>(collection);
		}
		public ICollection GetObjectsFromSproc(XPClassInfo classInfo, string sprocName, params OperandValue[] parameters) {
			return GetObjectsFromSproc(classInfo, classInfo.PersistentProperties as List<XPMemberInfo>, sprocName, parameters);
		}
		public Task<ICollection> GetObjectsFromSprocAsync(CancellationToken cancellationToken, XPClassInfo classInfo, string sprocName, params OperandValue[] parameters) {
			return GetObjectsFromSprocAsync(cancellationToken, classInfo, (List<XPMemberInfo>)classInfo.PersistentProperties, sprocName, parameters);
		}
		public ICollection<T> GetObjectsFromSproc<T>(List<XPMemberInfo> members, string sprocName, params OperandValue[] parameters) {
			return ListHelper.FromCollection<T>(GetObjectsFromSproc(GetClassInfo<T>(), members, sprocName, parameters));
		}
		public async Task<ICollection<T>> GetObjectsFromSprocAsync<T>(CancellationToken cancellationToken, List<XPMemberInfo> members, string sprocName, params OperandValue[] parameters) {
			ICollection collection = await GetObjectsFromSprocAsync(cancellationToken, GetClassInfo<T>(), members, sprocName, parameters).ConfigureAwait(false);
			return ListHelper.FromCollection<T>(collection);
		}
		public ICollection GetObjectsFromSproc(XPClassInfo classInfo, List<XPMemberInfo> members, string sprocName, params OperandValue[] parameters) {
			return LogManager.Log<ICollection>(LogCategory, () => {
				Dictionary<XPMemberInfo, int> referenceIndexDict = CheckClassInfoAndMembers(classInfo, members);
				SelectedData sprocResultData = ExecuteSproc(sprocName, parameters);
				return GetObjectsHelper.GetObjectsFromData(this, classInfo, members, null, sprocResultData, referenceIndexDict);
			}, (d) => {
				return CreateLogMessage("Executing GetObjectsFromSproc()", d, new object[] {
					LogParam_SessionType, this.GetType().ToString(), LogParam_SessionID, this.ToString(), LogParam_ClassInfo, classInfo.FullName,
					LogParam_Members, LogMessage.CollectionToString<XPMemberInfo>(members, mi => mi == null ? "null" : mi.Name ),
					LogParam_SprocName, sprocName,
					LogParam_Parameters, LogMessage.CriteriaOperatorCollectionToString<OperandValue>(parameters)
				});
			});
		}
		public Task<ICollection> GetObjectsFromSprocAsync(CancellationToken cancellationToken, XPClassInfo classInfo, List<XPMemberInfo> members, string sprocName, params OperandValue[] parameters) {
			return LogManager.LogAsync<ICollection>(LogCategory, async () => {
				Dictionary<XPMemberInfo, int> referenceIndexDict = CheckClassInfoAndMembers(classInfo, members);
				SelectedData sprocResultData = await ExecuteSprocAsync(cancellationToken, sprocName, parameters);
				return await GetObjectsHelper.GetObjectsFromDataAsync(this, classInfo, members, null, sprocResultData, referenceIndexDict, cancellationToken);
			}, (d) => {
				return CreateLogMessage("Executing GetObjectsFromSprocAsync()", d, new object[] {
					LogParam_SessionType, this.GetType().ToString(),
					LogParam_SessionID, this.ToString(),
					LogParam_ClassInfo, classInfo.FullName,
					LogParam_Members, LogMessage.CollectionToString<XPMemberInfo>(members, mi => mi == null ? "null" : mi.Name),
					LogParam_SprocName, sprocName,
					LogParam_Parameters, LogMessage.CriteriaOperatorCollectionToString<OperandValue>(parameters)
				});
			});
		}
		public ICollection<T> GetObjectsFromQuery<T>(string sql) {
			return ListHelper.FromCollection<T>(GetObjectsFromQuery(GetClassInfo<T>(), sql));
		}
		public async Task<ICollection<T>> GetObjectsFromQueryAsync<T>(string sql, CancellationToken cancellationToken = default(CancellationToken)) {
			ICollection collection = await GetObjectsFromQueryAsync(GetClassInfo<T>(), sql, cancellationToken).ConfigureAwait(false);
			return ListHelper.FromCollection<T>(collection);
		}
		public ICollection<T> GetObjectsFromQuery<T>(string sql, object[] parameterValues) {
			return ListHelper.FromCollection<T>(GetObjectsFromQuery(GetClassInfo<T>(), sql, parameterValues));
		}
		public async Task<ICollection<T>> GetObjectsFromQueryAsync<T>(string sql, object[] parameterValues, CancellationToken cancellationToken = default(CancellationToken)) {
			ICollection collection = await GetObjectsFromQueryAsync(GetClassInfo<T>(), sql, parameterValues, cancellationToken).ConfigureAwait(false);
			return ListHelper.FromCollection<T>(collection);
		}
		public ICollection<T> GetObjectsFromQuery<T>(string sql, QueryParameterCollection parameters) {
			return ListHelper.FromCollection<T>(GetObjectsFromQuery(GetClassInfo<T>(), sql, parameters));
		}
		public async Task<ICollection<T>> GetObjectsFromQueryAsync<T>(string sql, QueryParameterCollection parameters, CancellationToken cancellationToken = default(CancellationToken)) {
			ICollection collection = await GetObjectsFromQueryAsync(GetClassInfo<T>(), sql, parameters, cancellationToken).ConfigureAwait(false);
			return ListHelper.FromCollection<T>(collection);
		}
		public ICollection<T> GetObjectsFromQuery<T>(string sql, string[] parameterNames, object[] parameterValues) {
			return ListHelper.FromCollection<T>(GetObjectsFromQuery(GetClassInfo<T>(), sql, parameterNames, parameterValues));
		}
		public async Task<ICollection<T>> GetObjectsFromQueryAsync<T>(string sql, string[] parameterNames, object[] parameterValues, CancellationToken cancellationToken = default(CancellationToken)) {
			ICollection collection = await GetObjectsFromQueryAsync(GetClassInfo<T>(), sql, parameterNames, parameterValues, cancellationToken).ConfigureAwait(false);
			return ListHelper.FromCollection<T>(collection);
		}
		public ICollection<T> GetObjectsFromQuery<T>(string sql, string[] parameterNames, QueryParameterCollection parameters) {
			return ListHelper.FromCollection<T>(GetObjectsFromQuery(GetClassInfo<T>(), sql, parameterNames, parameters));
		}
		public async Task<ICollection<T>> GetObjectsFromQueryAsync<T>(string sql, string[] parameterNames, QueryParameterCollection parameters, CancellationToken cancellationToken = default(CancellationToken)) {
			ICollection collection = await GetObjectsFromQueryAsync(GetClassInfo<T>(), sql, parameterNames, parameters, cancellationToken).ConfigureAwait(false);
			return ListHelper.FromCollection<T>(collection);
		}
		public ICollection GetObjectsFromQuery(XPClassInfo classInfo, string sql) {
			return GetObjectsFromQuery(classInfo, classInfo.PersistentProperties as List<XPMemberInfo>, sql);
		}
		public Task<ICollection> GetObjectsFromQueryAsync(XPClassInfo classInfo, string sql, CancellationToken cancellationToken = default(CancellationToken)) {
			return GetObjectsFromQueryAsync(classInfo, (List<XPMemberInfo>)classInfo.PersistentProperties, sql, cancellationToken);
		}
		public ICollection GetObjectsFromQuery(XPClassInfo classInfo, string sql, object[] parameterValues) {
			return GetObjectsFromQuery(classInfo, classInfo.PersistentProperties as List<XPMemberInfo>, sql, parameterValues);
		}
		public Task<ICollection> GetObjectsFromQueryAsync(XPClassInfo classInfo, string sql, object[] parameterValues, CancellationToken cancellationToken = default(CancellationToken)) {
			return GetObjectsFromQueryAsync(classInfo, (List<XPMemberInfo>)classInfo.PersistentProperties, sql, parameterValues, cancellationToken);
		}
		public ICollection GetObjectsFromQuery(XPClassInfo classInfo, string sql, QueryParameterCollection parameters) {
			return GetObjectsFromQuery(classInfo, classInfo.PersistentProperties as List<XPMemberInfo>, sql, parameters);
		}
		public Task<ICollection> GetObjectsFromQueryAsync(XPClassInfo classInfo, string sql, QueryParameterCollection parameters, CancellationToken cancellationToken = default(CancellationToken)) {
			return GetObjectsFromQueryAsync(classInfo, (List<XPMemberInfo>)classInfo.PersistentProperties, sql, parameters, cancellationToken);
		}
		public ICollection GetObjectsFromQuery(XPClassInfo classInfo, string sql, string[] parameterNames, object[] parameterValues) {
			return GetObjectsFromQuery(classInfo, classInfo.PersistentProperties as List<XPMemberInfo>, sql, parameterNames, parameterValues);
		}
		public Task<ICollection> GetObjectsFromQueryAsync(XPClassInfo classInfo, string sql, string[] parameterNames, object[] parameterValues, CancellationToken cancellationToken = default(CancellationToken)) {
			return GetObjectsFromQueryAsync(classInfo, (List<XPMemberInfo>)classInfo.PersistentProperties, sql, parameterNames, parameterValues, cancellationToken);
		}
		public ICollection GetObjectsFromQuery(XPClassInfo classInfo, string sql, string[] parameterNames, QueryParameterCollection parameters) {
			return GetObjectsFromQuery(classInfo, classInfo.PersistentProperties as List<XPMemberInfo>, sql, parameterNames, parameters);
		}
		public Task<ICollection> GetObjectsFromQueryAsync(XPClassInfo classInfo, string sql, string[] parameterNames, QueryParameterCollection parameters, CancellationToken cancellationToken = default(CancellationToken)) {
			return GetObjectsFromQueryAsync(classInfo, (List<XPMemberInfo>)classInfo.PersistentProperties, sql, parameterNames, parameters, cancellationToken);
		}
		public ICollection<T> GetObjectsFromQuery<T>(List<XPMemberInfo> members, string sql) {
			return ListHelper.FromCollection<T>(GetObjectsFromQuery(GetClassInfo<T>(), members, sql));
		}
		public async Task<ICollection<T>> GetObjectsFromQueryAsync<T>(List<XPMemberInfo> members, string sql, CancellationToken cancellationToken = default(CancellationToken)) {
			ICollection collection = await GetObjectsFromQueryAsync(GetClassInfo<T>(), members, sql, cancellationToken).ConfigureAwait(false);
			return ListHelper.FromCollection<T>(collection);
		}
		public ICollection<T> GetObjectsFromQuery<T>(List<XPMemberInfo> members, string sql, object[] parameterValues) {
			return ListHelper.FromCollection<T>(GetObjectsFromQuery(GetClassInfo<T>(), members, sql, parameterValues));
		}
		public async Task<ICollection<T>> GetObjectsFromQueryAsync<T>(List<XPMemberInfo> members, string sql, object[] parameterValues, CancellationToken cancellationToken = default(CancellationToken)) {
			ICollection collection = await GetObjectsFromQueryAsync(GetClassInfo<T>(), members, sql, parameterValues, cancellationToken).ConfigureAwait(false);
			return ListHelper.FromCollection<T>(collection);
		}
		public ICollection<T> GetObjectsFromQuery<T>(List<XPMemberInfo> members, string sql, QueryParameterCollection parameters) {
			return ListHelper.FromCollection<T>(GetObjectsFromQuery(GetClassInfo<T>(), members, sql, parameters));
		}
		public async Task<ICollection<T>> GetObjectsFromQueryAsync<T>(List<XPMemberInfo> members, string sql, QueryParameterCollection parameters, CancellationToken cancellationToken = default(CancellationToken)) {
			ICollection collection = await GetObjectsFromQueryAsync(GetClassInfo<T>(), members, sql, parameters, cancellationToken).ConfigureAwait(false);
			return ListHelper.FromCollection<T>(collection);
		}
		public ICollection<T> GetObjectsFromQuery<T>(List<XPMemberInfo> members, string sql, string[] parameterNames, object[] parameterValues) {
			return ListHelper.FromCollection<T>(GetObjectsFromQuery(GetClassInfo<T>(), members, sql, parameterNames, parameterValues));
		}
		public async Task<ICollection<T>> GetObjectsFromQueryAsync<T>(List<XPMemberInfo> members, string sql, string[] parameterNames, object[] parameterValues, CancellationToken cancellationToken = default(CancellationToken)) {
			ICollection collection = await GetObjectsFromQueryAsync(GetClassInfo<T>(), members, sql, parameterNames, parameterValues, cancellationToken).ConfigureAwait(false);
			return ListHelper.FromCollection<T>(collection);
		}
		public ICollection<T> GetObjectsFromQuery<T>(List<XPMemberInfo> members, string sql, string[] parameterNames, QueryParameterCollection parameters) {
			return ListHelper.FromCollection<T>(GetObjectsFromQuery(GetClassInfo<T>(), members, sql, parameterNames, parameters));
		}
		public async Task<ICollection<T>> GetObjectsFromQueryAsync<T>(List<XPMemberInfo> members, string sql, string[] parameterNames, QueryParameterCollection parameters, CancellationToken cancellationToken = default(CancellationToken)) {
			ICollection collection = await GetObjectsFromQueryAsync(GetClassInfo<T>(), members, sql, parameterNames, parameters, cancellationToken).ConfigureAwait(false);
			return ListHelper.FromCollection<T>(collection);
		}
		public ICollection GetObjectsFromQuery(XPClassInfo classInfo, List<XPMemberInfo> members, string sql) {
			return LogManager.Log<ICollection>(LogCategory, () => {
				Dictionary<XPMemberInfo, int> referenceIndexDict = CheckClassInfoAndMembers(classInfo, members);
				SelectedData sprocResultData = ExecuteQuery(sql);
				return GetObjectsHelper.GetObjectsFromData(this, classInfo, members, null, sprocResultData, referenceIndexDict);
			}, (d) => {
				return CreateLogMessage("Executing GetObjectsFromQuery()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_ClassInfo, classInfo.FullName,
				LogParam_Members, LogMessage.CollectionToString<XPMemberInfo>(members, arg => arg == null ? "null" : arg.Name),
				LogParam_Sql, sql
				});
			});
		}
		public Task<ICollection> GetObjectsFromQueryAsync(XPClassInfo classInfo, List<XPMemberInfo> members, string sql, CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync<ICollection>(LogCategory, async () => {
				Dictionary<XPMemberInfo, int> referenceIndexDict = CheckClassInfoAndMembers(classInfo, members);
				SelectedData queryResultData = await ExecuteQueryAsync(sql, cancellationToken);
				return await GetObjectsHelper.GetObjectsFromDataAsync(this, classInfo, members, null, queryResultData, referenceIndexDict, cancellationToken);
			}, d => {
				return CreateLogMessage("Executing GetObjectsFromQueryAsync()", d, new object[] {
					LogParam_SessionType, this.GetType().ToString(),
					LogParam_SessionID, this.ToString(),
					LogParam_ClassInfo, classInfo.FullName,
					LogParam_Members, LogMessage.CollectionToString<XPMemberInfo>(members, arg => arg == null ? "null" : arg.Name),
					LogParam_Sql, sql
					});
			});
		}
		static string ParametersToString(string[] parameterNames, QueryParameterCollection parameters) {
			return LogMessage.CollectionToString(parameterNames, parameters, (name, value) => string.Format("{0}:{1}", string.IsNullOrEmpty(name) ? "_" : name, ReferenceEquals(value, null) ? "null" : value.ToString()));
		}
		static string ParametersToString(QueryParameterCollection parameters) {
			return LogMessage.CollectionToString<OperandValue>(parameters, arg => ReferenceEquals(arg, null) ? "null" : arg.ToString());
		}
		static string ParametersToString(string[] parameterNames, object[] parameterValues) {
			return LogMessage.CollectionToString(parameterNames, parameterValues, (name, value) => string.Format("{0}:{1}", string.IsNullOrEmpty(name) ? "_" : name, value == null ? "null" : value.ToString()));
		}
		static string ParametersToString(object[] parameterValues) {
			return LogMessage.CollectionToString<object>(parameterValues, arg => arg == null ? "null" : arg.ToString());
		}
		public ICollection GetObjectsFromQuery(XPClassInfo classInfo, List<XPMemberInfo> members, string sql, object[] parameterValues) {
			QueryParameterCollection paramValues = new QueryParameterCollection();
			foreach(var value in parameterValues) {
				paramValues.Add(new OperandValue(value));
			}
			return GetObjectsFromQuery(classInfo, members, sql, paramValues);
		}
		public async Task<ICollection> GetObjectsFromQueryAsync(XPClassInfo classInfo, List<XPMemberInfo> members, string sql, object[] parameterValues, CancellationToken cancellationToken = default(CancellationToken)) {
			QueryParameterCollection paramValues = new QueryParameterCollection();
			foreach(var value in parameterValues) {
				paramValues.Add(new OperandValue(value));
			}
			return await GetObjectsFromQueryAsync(classInfo, members, sql, paramValues, cancellationToken).ConfigureAwait(false);
		}
		public ICollection GetObjectsFromQuery(XPClassInfo classInfo, List<XPMemberInfo> members, string sql, QueryParameterCollection parameters) {
			return LogManager.Log<ICollection>(LogCategory, () => {
				Dictionary<XPMemberInfo, int> referenceIndexDict = CheckClassInfoAndMembers(classInfo, members);
				SelectedData sprocResultData = ExecuteQuery(sql, parameters);
				return GetObjectsHelper.GetObjectsFromData(this, classInfo, members, null, sprocResultData, referenceIndexDict);
			}, (d) => {
				return CreateLogMessage("Executing GetObjectsFromQuery()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_ClassInfo, classInfo.FullName,
				LogParam_Members, LogMessage.CollectionToString<XPMemberInfo>(members, arg => arg == null ? "null" : arg.Name ),
				LogParam_Sql, sql,
				LogParam_Parameters, ParametersToString(parameters)
				});
			});
		}
		public Task<ICollection> GetObjectsFromQueryAsync(XPClassInfo classInfo, List<XPMemberInfo> members, string sql, QueryParameterCollection parameters, CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync<ICollection>(LogCategory, async () => {
				Dictionary<XPMemberInfo, int> referenceIndexDict = CheckClassInfoAndMembers(classInfo, members);
				SelectedData queryResultData = await ExecuteQueryAsync(sql, parameters, cancellationToken);
				return await GetObjectsHelper.GetObjectsFromDataAsync(this, classInfo, members, null, queryResultData, referenceIndexDict, cancellationToken);
			}, (d) => {
				return CreateLogMessage("Executing GetObjectsFromQueryAsync()", d, new object[] {
					LogParam_SessionType, this.GetType().ToString(),
					LogParam_SessionID, this.ToString(),
					LogParam_ClassInfo, classInfo.FullName,
					LogParam_Members, LogMessage.CollectionToString<XPMemberInfo>(members, arg => arg == null ? "null" : arg.Name),
					LogParam_Sql, sql,
					LogParam_Parameters, ParametersToString(parameters)
				});
			});
		}
		public ICollection GetObjectsFromQuery(XPClassInfo classInfo, List<XPMemberInfo> members, string sql, string[] parameterNames, object[] parameterValues) {
			QueryParameterCollection paramValues = new QueryParameterCollection();
			foreach(var value in parameterValues) {
				paramValues.Add(new OperandValue(value));
			}
			return GetObjectsFromQuery(classInfo, members, sql, parameterNames, paramValues);
		}
		public async Task<ICollection> GetObjectsFromQueryAsync(XPClassInfo classInfo, List<XPMemberInfo> members, string sql, string[] parameterNames, object[] parameterValues, CancellationToken cancellationToken = default(CancellationToken)) {
			QueryParameterCollection paramValues = new QueryParameterCollection();
			foreach(var value in parameterValues) {
				paramValues.Add(new OperandValue(value));
			}
			return await GetObjectsFromQueryAsync(classInfo, members, sql, parameterNames, paramValues, cancellationToken).ConfigureAwait(false);
		}
		public ICollection GetObjectsFromQuery(XPClassInfo classInfo, List<XPMemberInfo> members, string sql, string[] parameterNames, QueryParameterCollection parameters) {
			return LogManager.Log<ICollection>(LogCategory, () => {
				Dictionary<XPMemberInfo, int> referenceIndexDict = CheckClassInfoAndMembers(classInfo, members);
				SelectedData sprocResultData = ExecuteQuery(sql, parameterNames, parameters);
				return GetObjectsHelper.GetObjectsFromData(this, classInfo, members, null, sprocResultData, referenceIndexDict);
			}, (d) => {
				return CreateLogMessage("Executing GetObjectsFromQuery()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_ClassInfo, classInfo.FullName,
				LogParam_Members, LogMessage.CollectionToString<XPMemberInfo>(members, arg => arg == null ? "null" : arg.Name),
				LogParam_Sql, sql,
				LogParam_Parameters, ParametersToString(parameterNames, parameters)
				});
			});
		}
		public Task<ICollection> GetObjectsFromQueryAsync(XPClassInfo classInfo, List<XPMemberInfo> members, string sql, string[] parameterNames, QueryParameterCollection parameters, CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync<ICollection>(LogCategory, async () => {
				Dictionary<XPMemberInfo, int> referenceIndexDict = CheckClassInfoAndMembers(classInfo, members);
				SelectedData queryResultData = await ExecuteQueryAsync(sql, parameterNames, parameters, cancellationToken);
				return await GetObjectsHelper.GetObjectsFromDataAsync(this, classInfo, members, null, queryResultData, referenceIndexDict, cancellationToken);
			}, (d) => {
				return CreateLogMessage("Executing GetObjectsFromQueryAsync()", d, new object[] {
					LogParam_SessionType, this.GetType().ToString(),
					LogParam_SessionID, this.ToString(),
					LogParam_ClassInfo, classInfo.FullName,
					LogParam_Members, LogMessage.CollectionToString<XPMemberInfo>(members, arg => arg == null ? "null": arg.Name),
					LogParam_Sql, sql,
					LogParam_Parameters, ParametersToString(parameterNames, parameters)
				});
			});
		}
		static Dictionary<XPMemberInfo, int> CheckClassInfoAndMembers(XPClassInfo classInfo, List<XPMemberInfo> members) {
			if(members == null)
				return null;
			Dictionary<XPMemberInfo, int> referenceIndexDict = null;
			foreach(XPMemberInfo mi in members) {
				if(mi.Owner != classInfo && !classInfo.IsAssignableTo(mi.Owner))
					throw new InvalidOperationException(Res.GetString(Res.Session_NotClassMember, mi.Name, classInfo.FullName));
				if(mi.ReferenceType != null) {
					if(referenceIndexDict == null) {
						referenceIndexDict = new Dictionary<XPMemberInfo, int>();
					}
					referenceIndexDict.Add(mi, referenceIndexDict.Count);
				}
				if(mi.IsCollection || mi.IsAssociationList)
					throw new NotSupportedException(Res.GetString(Res.DirectSQL_CollectionMembersAreNotSupported));
			}
			return referenceIndexDict;
		}
		public ICollection<T> GetObjectsFromSprocParametrized<T>(LoadDataMemberOrderItem[] members, string sprocName, params SprocParameter[] parameters) {
			return ListHelper.FromCollection<T>(GetObjectsFromSprocParametrized(GetClassInfo<T>(), members, sprocName, parameters));
		}
		public async Task<ICollection<T>> GetObjectsFromSprocParametrizedAsync<T>(CancellationToken cancellationToken, LoadDataMemberOrderItem[] members, string sprocName, params SprocParameter[] parameters) {
			ICollection collection = await GetObjectsFromSprocParametrizedAsync(cancellationToken, GetClassInfo<T>(), members, sprocName, parameters).ConfigureAwait(false);
			return ListHelper.FromCollection<T>(collection);
		}
		public ICollection GetObjectsFromSprocParametrized(XPClassInfo classInfo, LoadDataMemberOrderItem[] members, string sprocName, params SprocParameter[] parameters) {
			return LogManager.Log<ICollection>(LogCategory, () => {
				Dictionary<XPMemberInfo, int> referenceIndexDict;
				List<XPMemberInfo> memberInfos = CheckClassInfoAndMembers(classInfo, members, out referenceIndexDict);
				SelectedData sprocResultData = ExecuteSprocParametrized(sprocName, parameters);
				return GetObjectsHelper.GetObjectsFromData(this, classInfo, memberInfos, members, sprocResultData, referenceIndexDict);
			}, (d) => {
				return CreateLogMessage("Executing GetObjectsFromSprocParametrized()", d, new object[] {
					LogParam_SessionType, this.GetType().ToString(),
					LogParam_SessionID, this.ToString(),
					LogParam_ClassInfo, classInfo.FullName,
					LogParam_Members, LogMessage.CollectionToString<LoadDataMemberOrderItem>(members, arg => arg.ClassMemberName ),
					LogParam_SprocName, sprocName,
					LogParam_Parameters, LogMessage.CriteriaOperatorCollectionToString<OperandValue>(parameters)
				});
			});
		}
		public Task<ICollection> GetObjectsFromSprocParametrizedAsync(CancellationToken cancellationToken, XPClassInfo classInfo, LoadDataMemberOrderItem[] members, string sprocName, params SprocParameter[] parameters) {
			return LogManager.LogAsync<ICollection>(LogCategory, async () => {
				Dictionary<XPMemberInfo, int> referenceIndexDict;
				List<XPMemberInfo> memberInfos = CheckClassInfoAndMembers(classInfo, members, out referenceIndexDict);
				SelectedData sprocResultData = await ExecuteSprocParametrizedAsync(cancellationToken, sprocName, parameters);
				return await GetObjectsHelper.GetObjectsFromDataAsync(this, classInfo, memberInfos, members, sprocResultData, referenceIndexDict, cancellationToken);
			}, (d) => {
				return CreateLogMessage("Executing GetObjectsFromSprocParametrizedAsync()", d, new object[] {
					LogParam_SessionType, this.GetType().ToString(),
					LogParam_SessionID, this.ToString(),
					LogParam_ClassInfo, classInfo.FullName,
					LogParam_Members, LogMessage.CollectionToString<LoadDataMemberOrderItem>(members, arg => arg.ClassMemberName),
					LogParam_SprocName, sprocName,
					LogParam_Parameters, LogMessage.CriteriaOperatorCollectionToString<OperandValue>(parameters)
				});
			});
		}
		public ICollection<T> GetObjectsFromSproc<T>(LoadDataMemberOrderItem[] members, string sprocName, params OperandValue[] parameters) {
			return ListHelper.FromCollection<T>(GetObjectsFromSproc(GetClassInfo<T>(), members, sprocName, parameters));
		}
		public async Task<ICollection<T>> GetObjectsFromSprocAsync<T>(CancellationToken cancellationToken, LoadDataMemberOrderItem[] members, string sprocName, params OperandValue[] parameters) {
			ICollection collection = await GetObjectsFromSprocAsync(cancellationToken, GetClassInfo<T>(), members, sprocName, parameters).ConfigureAwait(false);
			return ListHelper.FromCollection<T>(collection);
		}
		public ICollection GetObjectsFromSproc(XPClassInfo classInfo, LoadDataMemberOrderItem[] members, string sprocName, params OperandValue[] parameters) {
			return LogManager.Log<ICollection>(LogCategory, () => {
				Dictionary<XPMemberInfo, int> referenceIndexDict;
				List<XPMemberInfo> memberInfos = CheckClassInfoAndMembers(classInfo, members, out referenceIndexDict);
				SelectedData sprocResultData = ExecuteSproc(sprocName, parameters);
				return GetObjectsHelper.GetObjectsFromData(this, classInfo, memberInfos, members, sprocResultData, referenceIndexDict);
			}, (d) => {
				return CreateLogMessage("Executing GetObjectsFromSproc()", d, new object[] {
					LogParam_SessionType, this.GetType().ToString(),
					LogParam_SessionID, this.ToString(),
					LogParam_ClassInfo, classInfo.FullName,
					LogParam_Members, LogMessage.CollectionToString<LoadDataMemberOrderItem>(members, arg => arg.ClassMemberName ),
					LogParam_SprocName, sprocName,
					LogParam_Parameters, LogMessage.CriteriaOperatorCollectionToString<OperandValue>(parameters)
				});
			});
		}
		public Task<ICollection> GetObjectsFromSprocAsync(CancellationToken cancellationToken, XPClassInfo classInfo, LoadDataMemberOrderItem[] members, string sprocName, params OperandValue[] parameters) {
			return LogManager.LogAsync<ICollection>(LogCategory, async () => {
				Dictionary<XPMemberInfo, int> referenceIndexDict;
				List<XPMemberInfo> memberInfos = CheckClassInfoAndMembers(classInfo, members, out referenceIndexDict);
				SelectedData sprocResultData = await ExecuteSprocAsync(cancellationToken, sprocName, parameters);
				return await GetObjectsHelper.GetObjectsFromDataAsync(this, classInfo, memberInfos, members, sprocResultData, referenceIndexDict, cancellationToken);
			}, (d) => {
				return CreateLogMessage("Executing GetObjectsFromSprocAsync()", d, new object[] {
					LogParam_SessionType, this.GetType().ToString(),
					LogParam_SessionID, this.ToString(),
					LogParam_ClassInfo, classInfo.FullName,
					LogParam_Members, LogMessage.CollectionToString<LoadDataMemberOrderItem>(members, arg => arg.ClassMemberName),
					LogParam_SprocName, sprocName,
					LogParam_Parameters, LogMessage.CriteriaOperatorCollectionToString<OperandValue>(parameters)
				});
			});
		}
		public ICollection<T> GetObjectsFromQuery<T>(LoadDataMemberOrderItem[] members, string sql) {
			return ListHelper.FromCollection<T>(GetObjectsFromQuery(GetClassInfo<T>(), members, sql));
		}
		public async Task<ICollection<T>> GetObjectsFromQueryAsync<T>(LoadDataMemberOrderItem[] members, string sql, CancellationToken cancellationToken = default(CancellationToken)) {
			ICollection collection = await GetObjectsFromQueryAsync(GetClassInfo<T>(), members, sql, cancellationToken).ConfigureAwait(false);
			return ListHelper.FromCollection<T>(collection);
		}
		public ICollection<T> GetObjectsFromQuery<T>(LoadDataMemberOrderItem[] members, string sql, object[] parameterValues) {
			return ListHelper.FromCollection<T>(GetObjectsFromQuery(GetClassInfo<T>(), members, sql, parameterValues));
		}
		public async Task<ICollection<T>> GetObjectsFromQueryAsync<T>(LoadDataMemberOrderItem[] members, string sql, object[] parameterValues, CancellationToken cancellationToken = default(CancellationToken)) {
			ICollection collection = await GetObjectsFromQueryAsync(GetClassInfo<T>(), members, sql, parameterValues, cancellationToken).ConfigureAwait(false);
			return ListHelper.FromCollection<T>(collection);
		}
		public ICollection<T> GetObjectsFromQuery<T>(LoadDataMemberOrderItem[] members, string sql, QueryParameterCollection parameters) {
			return ListHelper.FromCollection<T>(GetObjectsFromQuery(GetClassInfo<T>(), members, sql, parameters));
		}
		public async Task<ICollection<T>> GetObjectsFromQueryAsync<T>(LoadDataMemberOrderItem[] members, string sql, QueryParameterCollection parameters, CancellationToken cancellationToken = default(CancellationToken)) {
			ICollection collection = await GetObjectsFromQueryAsync(GetClassInfo<T>(), members, sql, parameters, cancellationToken).ConfigureAwait(false);
			return ListHelper.FromCollection<T>(collection);
		}
		public ICollection<T> GetObjectsFromQuery<T>(LoadDataMemberOrderItem[] members, string sql, string[] parameterNames, object[] parameterValues) {
			return ListHelper.FromCollection<T>(GetObjectsFromQuery(GetClassInfo<T>(), members, sql, parameterNames, parameterValues));
		}
		public async Task<ICollection<T>> GetObjectsFromQueryAsync<T>(LoadDataMemberOrderItem[] members, string sql, string[] parameterNames, object[] parameterValues, CancellationToken cancellationToken = default(CancellationToken)) {
			ICollection collection = await GetObjectsFromQueryAsync(GetClassInfo<T>(), members, sql, parameterNames, parameterValues, cancellationToken).ConfigureAwait(false);
			return ListHelper.FromCollection<T>(collection);
		}
		public ICollection<T> GetObjectsFromQuery<T>(LoadDataMemberOrderItem[] members, string sql, string[] parameterNames, QueryParameterCollection parameters) {
			return ListHelper.FromCollection<T>(GetObjectsFromQuery(GetClassInfo<T>(), members, sql, parameterNames, parameters));
		}
		public async Task<ICollection<T>> GetObjectsFromQueryAsync<T>(LoadDataMemberOrderItem[] members, string sql, string[] parameterNames, QueryParameterCollection parameters, CancellationToken cancellationToken = default(CancellationToken)) {
			ICollection collection = await GetObjectsFromQueryAsync(GetClassInfo<T>(), members, sql, parameterNames, parameters, cancellationToken).ConfigureAwait(false);
			return ListHelper.FromCollection<T>(collection);
		}
		public ICollection GetObjectsFromQuery(XPClassInfo classInfo, LoadDataMemberOrderItem[] members, string sql) {
			return LogManager.Log<ICollection>(LogCategory, () => {
				Dictionary<XPMemberInfo, int> referenceIndexDict;
				List<XPMemberInfo> memberInfos = CheckClassInfoAndMembers(classInfo, members, out referenceIndexDict);
				SelectedData sprocResultData = ExecuteQuery(sql);
				return GetObjectsHelper.GetObjectsFromData(this, classInfo, memberInfos, members, sprocResultData, referenceIndexDict);
			}, (d) => {
				return CreateLogMessage("Executing GetObjectsFromQuery()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_ClassInfo, classInfo.FullName,
				LogParam_Members, LogMessage.CollectionToString<LoadDataMemberOrderItem>(members, arg => arg.ClassMemberName ),
				LogParam_Sql, sql
				});
			});
		}
		public Task<ICollection> GetObjectsFromQueryAsync(XPClassInfo classInfo, LoadDataMemberOrderItem[] members, string sql, CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync<ICollection>(LogCategory, async () => {
				Dictionary<XPMemberInfo, int> referenceIndexDict;
				List<XPMemberInfo> memberInfos = CheckClassInfoAndMembers(classInfo, members, out referenceIndexDict);
				SelectedData queryResultData = await ExecuteQueryAsync(sql, cancellationToken);
				return await GetObjectsHelper.GetObjectsFromDataAsync(this, classInfo, memberInfos, members, queryResultData, referenceIndexDict, cancellationToken);
			}, (d) => {
				return CreateLogMessage("Executing GetObjectsFromQueryAsync()", d, new object[] {
					LogParam_SessionType, this.GetType().ToString(),
					LogParam_SessionID, this.ToString(),
					LogParam_ClassInfo, classInfo.FullName,
					LogParam_Members, LogMessage.CollectionToString<LoadDataMemberOrderItem>(members, arg => arg.ClassMemberName),
					LogParam_Sql, sql
				});
			});
		}
		public ICollection GetObjectsFromQuery(XPClassInfo classInfo, LoadDataMemberOrderItem[] members, string sql, object[] parameterValues) {
			QueryParameterCollection paramValues = new QueryParameterCollection();
			foreach(var value in parameterValues) {
				paramValues.Add(new OperandValue(value));
			}
			return GetObjectsFromQuery(classInfo, members, sql, paramValues);
		}
		public async Task<ICollection> GetObjectsFromQueryAsync(XPClassInfo classInfo, LoadDataMemberOrderItem[] members, string sql, object[] parameterValues, CancellationToken cancellationToken = default(CancellationToken)) {
			QueryParameterCollection paramValues = new QueryParameterCollection();
			foreach(var value in parameterValues) {
				paramValues.Add(new OperandValue(value));
			}
			return await GetObjectsFromQueryAsync(classInfo, members, sql, paramValues, cancellationToken).ConfigureAwait(false);
		}
		public ICollection GetObjectsFromQuery(XPClassInfo classInfo, LoadDataMemberOrderItem[] members, string sql, QueryParameterCollection parameters) {
			return LogManager.Log<ICollection>(LogCategory, () => {
				Dictionary<XPMemberInfo, int> referenceIndexDict;
				List<XPMemberInfo> memberInfos = CheckClassInfoAndMembers(classInfo, members, out referenceIndexDict);
				SelectedData sprocResultData = ExecuteQuery(sql, parameters);
				return GetObjectsHelper.GetObjectsFromData(this, classInfo, memberInfos, members, sprocResultData, referenceIndexDict);
			}, (d) => {
				return CreateLogMessage("Executing GetObjectsFromQuery()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_ClassInfo, classInfo.FullName,
				LogParam_Members, LogMessage.CollectionToString<LoadDataMemberOrderItem>(members, arg => arg.ClassMemberName ),
				LogParam_Sql, sql,
				LogParam_Parameters, ParametersToString(parameters)
				});
			});
		}
		public Task<ICollection> GetObjectsFromQueryAsync(XPClassInfo classInfo, LoadDataMemberOrderItem[] members, string sql, QueryParameterCollection parameters, CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync<ICollection>(LogCategory, async () => {
				Dictionary<XPMemberInfo, int> referenceIndexDict;
				List<XPMemberInfo> memberInfos = CheckClassInfoAndMembers(classInfo, members, out referenceIndexDict);
				SelectedData queryResultData = await ExecuteQueryAsync(sql, parameters, cancellationToken);
				return await GetObjectsHelper.GetObjectsFromDataAsync(this, classInfo, memberInfos, members, queryResultData, referenceIndexDict, cancellationToken);
			}, (d) => {
				return CreateLogMessage("Executing GetObjectsFromQueryAsync()", d, new object[] {
					LogParam_SessionType, this.GetType().ToString(),
					LogParam_SessionID, this.ToString(),
					LogParam_Members, LogMessage.CollectionToString<LoadDataMemberOrderItem>(members, arg => arg.ClassMemberName),
					LogParam_Sql, sql,
					LogParam_Parameters, ParametersToString(parameters)
				});
			});
		}
		public ICollection GetObjectsFromQuery(XPClassInfo classInfo, LoadDataMemberOrderItem[] members, string sql, string[] parameterNames, object[] parameterValues) {
			QueryParameterCollection paramValues = new QueryParameterCollection();
			foreach(var value in parameterValues) {
				paramValues.Add(new OperandValue(value));
			}
			return GetObjectsFromQuery(classInfo, members, sql, parameterNames, paramValues);
		}
		public async Task<ICollection> GetObjectsFromQueryAsync(XPClassInfo classInfo, LoadDataMemberOrderItem[] members, string sql, string[] parameterNames, object[] parameterValues, CancellationToken cancellationToken = default(CancellationToken)) {
			QueryParameterCollection paramValues = new QueryParameterCollection();
			foreach(var value in parameterValues) {
				paramValues.Add(new OperandValue(value));
			}
			return await GetObjectsFromQueryAsync(classInfo, members, sql, parameterNames, paramValues, cancellationToken).ConfigureAwait(false);
		}
		public ICollection GetObjectsFromQuery(XPClassInfo classInfo, LoadDataMemberOrderItem[] members, string sql, string[] parameterNames, QueryParameterCollection parameters) {
			return LogManager.Log<ICollection>(LogCategory, () => {
				Dictionary<XPMemberInfo, int> referenceIndexDict;
				List<XPMemberInfo> memberInfos = CheckClassInfoAndMembers(classInfo, members, out referenceIndexDict);
				SelectedData sprocResultData = ExecuteQuery(sql, parameterNames, parameters);
				return GetObjectsHelper.GetObjectsFromData(this, classInfo, memberInfos, members, sprocResultData, referenceIndexDict);
			}, (d) => {
				return CreateLogMessage("Executing GetObjectsFromQuery()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_ClassInfo, classInfo.FullName,
				LogParam_Members, LogMessage.CollectionToString<LoadDataMemberOrderItem>(members, arg => arg.ClassMemberName ),
				LogParam_Sql, sql,
				LogParam_Parameters, ParametersToString(parameterNames, parameters)
				});
			});
		}
		public Task<ICollection> GetObjectsFromQueryAsync(XPClassInfo classInfo, LoadDataMemberOrderItem[] members, string sql, string[] parameterNames, QueryParameterCollection parameters, CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync<ICollection>(LogCategory, async () => {
				Dictionary<XPMemberInfo, int> referenceIndexDict;
				List<XPMemberInfo> memberInfos = CheckClassInfoAndMembers(classInfo, members, out referenceIndexDict);
				SelectedData queryResultData = await ExecuteQueryAsync(sql, parameterNames, parameters, cancellationToken);
				return await GetObjectsHelper.GetObjectsFromDataAsync(this, classInfo, memberInfos, members, queryResultData, referenceIndexDict, cancellationToken);
			}, (d) => {
				return CreateLogMessage("Executing GetObjectsFromQueryAsync()", d, new object[] {
					LogParam_SessionType, this.GetType().ToString(),
					LogParam_SessionID, this.ToString(),
					LogParam_ClassInfo, classInfo.FullName,
					LogParam_Members, LogMessage.CollectionToString<LoadDataMemberOrderItem>(members, arg => arg.ClassMemberName),
					LogParam_Sql, sql,
					LogParam_Parameters, ParametersToString(parameterNames, parameters)
				});
			});
		}
		static List<XPMemberInfo> CheckClassInfoAndMembers(XPClassInfo classInfo, LoadDataMemberOrderItem[] members, out Dictionary<XPMemberInfo, int> referenceIndexDict) {
			referenceIndexDict = null;
			List<XPMemberInfo> memberInfos = new List<XPMemberInfo>(members.Length);
			for(int i = 0; i < members.Length; i++) {
				XPMemberInfo mi = classInfo.FindMember(members[i].ClassMemberName);
				if(mi.ReferenceType != null) {
					if(referenceIndexDict == null) {
						referenceIndexDict = new Dictionary<XPMemberInfo, int>();
					}
					referenceIndexDict.Add(mi, referenceIndexDict.Count);
				}
				if(mi.IsCollection || mi.IsAssociationList)
					throw new NotSupportedException(Res.GetString(Res.DirectSQL_CollectionMembersAreNotSupported));
				memberInfos.Add(mi);
			}
			return memberInfos;
		}
		public ICollection GetObjectsByKeyFromSproc(XPClassInfo classInfo, bool alwaysGetFromDataStore, string sprocName, params OperandValue[] parameters) {
			return LogManager.Log<ICollection>(LogCategory, () => {
				SelectedData sprocResultData = ExecuteSproc(sprocName, parameters);
				return GetObjectsByKeyFromQuery(classInfo, alwaysGetFromDataStore, sprocResultData);
			}, (d) => {
				return CreateLogMessage("Executing GetObjectsByKeyFromSproc()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_ClassInfo, classInfo.FullName,
				LogParam_AlwaysGetFromDataStore, alwaysGetFromDataStore,
				LogParam_SprocName, sprocName,
				LogParam_Parameters, LogMessage.CriteriaOperatorCollectionToString<OperandValue>(parameters)
				});
			});
		}
		public ICollection GetObjectsByKeyFromSprocParametrized(XPClassInfo classInfo, bool alwaysGetFromDataStore, string sprocName, params SprocParameter[] parameters) {
			return LogManager.Log<ICollection>(LogCategory, () => {
				SelectedData sprocResultData = ExecuteSprocParametrized(sprocName, parameters);
				return GetObjectsByKeyFromQuery(classInfo, alwaysGetFromDataStore, sprocResultData);
			}, (d) => {
				return CreateLogMessage("Executing GetObjectsByKeyFromSprocParametrized()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_ClassInfo, classInfo.FullName,
				LogParam_AlwaysGetFromDataStore, alwaysGetFromDataStore,
				LogParam_SprocName, sprocName,
				LogParam_Parameters, LogMessage.CriteriaOperatorCollectionToString<OperandValue>(parameters)
				});
			});
		}
		public ICollection GetObjectsByKeyFromQuery(XPClassInfo classInfo, bool alwaysGetFromDataStore, string sql) {
			return LogManager.Log<ICollection>(LogCategory, () => {
				SelectedData sprocResultData = ExecuteQuery(sql);
				return GetObjectsByKeyFromQuery(classInfo, alwaysGetFromDataStore, sprocResultData);
			}, (d) => {
				return CreateLogMessage("Executing GetObjectsByKeyFromQuery()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_ClassInfo, classInfo.FullName,
				LogParam_AlwaysGetFromDataStore, alwaysGetFromDataStore,
				LogParam_Sql, sql
				});
			});
		}
		ICollection GetObjectsByKeyFromQuery(XPClassInfo classInfo, bool alwaysGetFromDataStore, SelectedData sprocResultData) {
			if(sprocResultData == null || sprocResultData.ResultSet == null || sprocResultData.ResultSet.Length == 0)
				return Array.Empty<object>();
			SelectStatementResult sprocResult = sprocResultData.ResultSet[0];
			if(sprocResult == null || sprocResult.Rows == null || sprocResult.Rows.Length == 0)
				return Array.Empty<object>();
			SelectStatementResultRow[] rows = sprocResult.Rows;
			object[] idCollection = new object[rows.Length];
			for(int i = 0; i < rows.Length; i++) {
				idCollection[i] = GetObjectsHelper.ChangeDbTypeToMemberType(rows[i].Values[0], classInfo.KeyProperty.MemberType);
			}
			return GetObjectsByKey(classInfo, idCollection, alwaysGetFromDataStore);
		}
		public SelectedData ExecuteSproc(string sprocName, params OperandValue[] parameters) {
			return LogManager.Log<SelectedData>(LogCategory, () => {
				return CommandChannelHelper.ExecuteSproc(this, sprocName, parameters);
			}, (d) => {
				return CreateLogMessage("Executing ExecuteSproc()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_SprocName, sprocName,
				LogParam_Parameters, LogMessage.CollectionToString<OperandValue>(parameters, ov => ov.ToString() )
				});
			});
		}
		public Task<SelectedData> ExecuteSprocAsync(CancellationToken cancellationToken, string sprocName, params OperandValue[] parameters) {
			return LogManager.LogAsync<SelectedData>(LogCategory, () => {
				return CommandChannelHelper.ExecuteSprocAsync(this, cancellationToken, sprocName, parameters);
			}, (d) => {
				return CreateLogMessage("Executing ExecuteSprocAsync()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_SprocName, sprocName,
				LogParam_Parameters, LogMessage.CollectionToString<OperandValue>(parameters, ov => ov.ToString() )
				});
			});
		}
		public SelectedData ExecuteSprocParametrized(string sprocName, params SprocParameter[] parameters) {
			return LogManager.Log<SelectedData>(LogCategory, () => {
				return CommandChannelHelper.ExecuteSprocParametrized(this, sprocName, parameters);
			}, (d) => {
				return CreateLogMessage("Executing ExecuteSprocParametrized()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_SprocName, sprocName,
				LogParam_Parameters, LogMessage.CollectionToString<SprocParameter>(parameters, ov => ov.ToString() )
				});
			});
		}
		public Task<SelectedData> ExecuteSprocParametrizedAsync(CancellationToken cancellationToken, string sprocName, params SprocParameter[] parameters) {
			return LogManager.LogAsync<SelectedData>(LogCategory, () => {
				return CommandChannelHelper.ExecuteSprocParametrizedAsync(this, cancellationToken, sprocName, parameters);
			}, (d) => {
				return CreateLogMessage("Executing ExecuteSprocParametrizedAsync()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_SprocName, sprocName,
				LogParam_Parameters, LogMessage.CollectionToString<SprocParameter>(parameters, ov => ov.ToString() )
				});
			});
		}
		public int ExecuteNonQuery(string sql) {
			return LogManager.Log<int>(LogCategory, () => {
				return CommandChannelHelper.ExecuteNonQuery(this, sql);
			}, (d) => {
				return CreateLogMessage("Executing ExecuteNonQuery()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_Sql, sql
				});
			});
		}
		public Task<int> ExecuteNonQueryAsync(string sql, CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync<int>(LogCategory, () => {
				return CommandChannelHelper.ExecuteNonQueryAsync(this, sql, cancellationToken);
			}, (d) => {
				return CreateLogMessage("Executing ExecuteNonQueryAsync()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_Sql, sql
				});
			});
		}
		public int ExecuteNonQuery(string sql, string[] parameterNames, object[] parameterValues) {
			QueryParameterCollection paramValues = new QueryParameterCollection();
			foreach(object item in parameterValues)
				paramValues.Add(new OperandValue(item));
			return ExecuteNonQuery(sql, parameterNames, paramValues);
		}
		public int ExecuteNonQuery(string sql, string[] parameterNames, QueryParameterCollection parameters) {
			return LogManager.Log<int>(LogCategory, () => {
				return CommandChannelHelper.ExecuteNonQueryWithParams(this, sql, parameters, parameterNames);
			}, (d) => {
				return CreateLogMessage("Executing ExecuteNonQuery()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_Sql, sql,
				LogParam_Parameters, ParametersToString(parameterNames, parameters)
				});
			});
		}
		public Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken, string sql, string[] parameterNames, object[] parameterValues) {
			QueryParameterCollection paramValues = new QueryParameterCollection();
			foreach(object item in parameterValues)
				paramValues.Add(new OperandValue(item));
			return ExecuteNonQueryAsync(cancellationToken, sql, parameterNames, paramValues);
		}
		public Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken, string sql, string[] parameterNames, QueryParameterCollection parameters) {
			return LogManager.LogAsync<int>(LogCategory, () => {
				return CommandChannelHelper.ExecuteNonQueryWithParamsAsync(this, sql, parameters, parameterNames, cancellationToken);
			}, (d) => {
				return CreateLogMessage("Executing ExecuteNonQueryAsync()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_Sql, sql,
				LogParam_Parameters, ParametersToString(parameterNames, parameters)
				});
			});
		}
		public int ExecuteNonQuery(string sql, object[] parameterValues) {
			QueryParameterCollection paramValues = new QueryParameterCollection();
			foreach(object item in parameterValues)
				paramValues.Add(new OperandValue(item));
			return ExecuteNonQuery(sql, paramValues);
		}
		public int ExecuteNonQuery(string sql, QueryParameterCollection parameters) {
			return LogManager.Log<int>(LogCategory, () => {
				return CommandChannelHelper.ExecuteNonQueryWithParams(this, sql, parameters, Array.Empty<string>());
			}, (d) => {
				return CreateLogMessage("Executing ExecuteNonQuery()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_Sql, sql,
				LogParam_Parameters, ParametersToString(parameters)
				});
			});
		}
		public Task<int> ExecuteNonQueryAsync(string sql, object[] parameterValues, CancellationToken cancellationToken = default(CancellationToken)) {
			QueryParameterCollection paramValues = new QueryParameterCollection();
			foreach(object item in parameterValues)
				paramValues.Add(new OperandValue(item));
			return ExecuteNonQueryAsync(sql, paramValues, cancellationToken);
		}
		public Task<int> ExecuteNonQueryAsync(string sql, QueryParameterCollection parameters, CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync<int>(LogCategory, () => {
				return CommandChannelHelper.ExecuteNonQueryWithParamsAsync(this, sql, parameters, Array.Empty<string>(), cancellationToken);
			}, (d) => {
				return CreateLogMessage("Executing ExecuteNonQueryAsync()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_Sql, sql,
				LogParam_Parameters, ParametersToString(parameters)
				});
			});
		}
		public object ExecuteScalar(string sql) {
			return LogManager.Log<object>(LogCategory, () => {
				return CommandChannelHelper.ExecuteScalar(this, sql);
			}, (d) => {
				return CreateLogMessage("Executing ExecuteScalar()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_Sql, sql
				});
			});
		}
		public Task<object> ExecuteScalarAsync(string sql, CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync<object>(LogCategory, () => {
				return CommandChannelHelper.ExecuteScalarAsync(this, sql, cancellationToken);
			}, (d) => {
				return CreateLogMessage("Executing ExecuteScalarAsync()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_Sql, sql
				});
			});
		}
		public object ExecuteScalar(string sql, string[] parameterNames, object[] parameterValues) {
			QueryParameterCollection paramValues = new QueryParameterCollection();
			foreach(object item in parameterValues)
				paramValues.Add(new OperandValue(item));
			return ExecuteScalar(sql, parameterNames, paramValues);
		}
		public object ExecuteScalar(string sql, string[] parameterNames, QueryParameterCollection parameters) {
			return LogManager.Log<object>(LogCategory, () => {
				return CommandChannelHelper.ExecuteScalarWithParams(this, sql, parameters, parameterNames);
			}, (d) => {
				return CreateLogMessage("Executing ExecuteScalar()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_Sql, sql,
				LogParam_Parameters, ParametersToString(parameterNames, parameters)
				});
			});
		}
		public Task<object> ExecuteScalarAsync(string sql, string[] parameterNames, object[] parameterValues, CancellationToken cancellationToken = default(CancellationToken)) {
			QueryParameterCollection paramValues = new QueryParameterCollection();
			foreach(object item in parameterValues)
				paramValues.Add(new OperandValue(item));
			return ExecuteScalarAsync(sql, parameterNames, paramValues, cancellationToken);
		}
		public Task<object> ExecuteScalarAsync(string sql, string[] parameterNames, QueryParameterCollection parameters, CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync<object>(LogCategory, () => {
				return CommandChannelHelper.ExecuteScalarWithParamsAsync(this, sql, parameters, parameterNames, cancellationToken);
			}, (d) => {
				return CreateLogMessage("Executing ExecuteScalarAsync()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_Sql, sql,
				LogParam_Parameters, ParametersToString(parameterNames, parameters)
				});
			});
		}
		public object ExecuteScalar(string sql, object[] parameterValues) {
			QueryParameterCollection paramValues = new QueryParameterCollection();
			foreach(object item in parameterValues)
				paramValues.Add(new OperandValue(item));
			return ExecuteScalar(sql, paramValues);
		}
		public object ExecuteScalar(string sql, QueryParameterCollection parameters) {
			return LogManager.Log<object>(LogCategory, () => {
				return CommandChannelHelper.ExecuteScalarWithParams(this, sql, parameters, Array.Empty<string>());
			}, (d) => {
				return CreateLogMessage("Executing ExecuteScalar()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_Sql, sql,
				LogParam_Parameters, ParametersToString(parameters)
				});
			});
		}
		public Task<object> ExecuteScalarAsync(string sql, object[] parameterValues, CancellationToken cancellationToken = default(CancellationToken)) {
			QueryParameterCollection paramValues = new QueryParameterCollection();
			foreach(object item in parameterValues)
				paramValues.Add(new OperandValue(item));
			return ExecuteScalarAsync(sql, paramValues, cancellationToken);
		}
		public Task<object> ExecuteScalarAsync(string sql, QueryParameterCollection parameters, CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync<object>(LogCategory, () => {
				return CommandChannelHelper.ExecuteScalarWithParamsAsync(this, sql, parameters, Array.Empty<string>(), cancellationToken);
			}, (d) => {
				return CreateLogMessage("Executing ExecuteScalarAsync()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_Sql, sql,
				LogParam_Parameters, ParametersToString(parameters)
				});
			});
		}
		public SelectedData ExecuteQuery(string sql) {
			return LogManager.Log<SelectedData>(LogCategory, () => {
				return CommandChannelHelper.ExecuteQuery(this, sql);
			}, (d) => {
				return CreateLogMessage("Executing ExecuteQuery()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_Sql, sql
				});
			});
		}
		public Task<SelectedData> ExecuteQueryAsync(string sql, CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync<SelectedData>(LogCategory, () => {
				return CommandChannelHelper.ExecuteQueryAsync(this, sql, cancellationToken);
			}, (d) => {
				return CreateLogMessage("Executing ExecuteQueryAsync()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_Sql, sql
				});
			});
		}
		public SelectedData ExecuteQuery(string sql, string[] parameterNames, object[] parameterValues) {
			QueryParameterCollection parameters = new QueryParameterCollection();
			foreach(var value in parameterValues) {
				parameters.Add(new OperandValue(value));
			}
			return ExecuteQuery(sql, parameterNames, parameters);
		}
		public SelectedData ExecuteQuery(string sql, string[] parameterNames, QueryParameterCollection parameters) {
			return LogManager.Log<SelectedData>(LogCategory, () => {
				return CommandChannelHelper.ExecuteQueryWithParams(this, sql, parameters, parameterNames);
			}, (d) => {
				return CreateLogMessage("Executing ExecuteQuery()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_Sql, sql,
				LogParam_Parameters, ParametersToString(parameterNames, parameters)
				});
			});
		}
		public Task<SelectedData> ExecuteQueryAsync(string sql, string[] parameterNames, object[] parameterValues, CancellationToken cancellationToken = default(CancellationToken)) {
			QueryParameterCollection paramValues = new QueryParameterCollection();
			foreach(object item in parameterValues)
				paramValues.Add(new OperandValue(item));
			return ExecuteQueryAsync(sql, parameterNames, paramValues, cancellationToken);
		}
		public Task<SelectedData> ExecuteQueryAsync(string sql, string[] parameterNames, QueryParameterCollection parameters, CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync<SelectedData>(LogCategory, () => {
				return CommandChannelHelper.ExecuteQueryWithParamsAsync(this, sql, parameters, parameterNames, cancellationToken);
			}, (d) => {
				return CreateLogMessage("Executing ExecuteQueryAsync()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_Sql, sql,
				LogParam_Parameters, ParametersToString(parameterNames, parameters)
				});
			});
		}
		public SelectedData ExecuteQuery(string sql, object[] parameterValues) {
			QueryParameterCollection paramValues = new QueryParameterCollection();
			foreach(object item in parameterValues)
				paramValues.Add(new OperandValue(item));
			return ExecuteQuery(sql, paramValues);
		}
		public SelectedData ExecuteQuery(string sql, QueryParameterCollection parameters) {
			return LogManager.Log<SelectedData>(LogCategory, () => {
				return CommandChannelHelper.ExecuteQueryWithParams(this, sql, parameters, Array.Empty<string>());
			}, (d) => {
				return CreateLogMessage("Executing ExecuteQuery()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_Sql, sql,
				LogParam_Parameters, ParametersToString(parameters)
				});
			});
		}
		public Task<SelectedData> ExecuteQueryAsync(string sql, object[] parameterValues, CancellationToken cancellationToken = default(CancellationToken)) {
			QueryParameterCollection paramValues = new QueryParameterCollection();
			foreach(object item in parameterValues)
				paramValues.Add(new OperandValue(item));
			return ExecuteQueryAsync(sql, paramValues, cancellationToken);
		}
		public Task<SelectedData> ExecuteQueryAsync(string sql, QueryParameterCollection parameters, CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync<SelectedData>(LogCategory, () => {
				return CommandChannelHelper.ExecuteQueryWithParamsAsync(this, sql, parameters, Array.Empty<string>(), cancellationToken);
			}, (d) => {
				return CreateLogMessage("Executing ExecuteQueryAsync()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_Sql, sql,
				LogParam_Parameters, ParametersToString(parameters)
				});
			});
		}
		public SelectedData ExecuteQueryWithMetadata(string sql) {
			return LogManager.Log<SelectedData>(LogCategory, () => {
				return CommandChannelHelper.ExecuteQueryWithMetadata(this, sql);
			}, (d) => {
				return CreateLogMessage("Executing ExecuteQueryWithMetadata()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_Sql, sql
				});
			});
		}
		public Task<SelectedData> ExecuteQueryWithMetadataAsync(string sql, CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync<SelectedData>(LogCategory, () => {
				return CommandChannelHelper.ExecuteQueryWithMetadataAsync(this, sql, cancellationToken);
			}, (d) => {
				return CreateLogMessage("Executing ExecuteQueryWithMetadataAsync()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_Sql, sql
				});
			});
		}
		public SelectedData ExecuteQueryWithMetadata(string sql, string[] parameterNames, object[] parameterValues) {
			QueryParameterCollection paramValues = new QueryParameterCollection();
			foreach(object item in parameterValues)
				paramValues.Add(new OperandValue(item));
			return ExecuteQueryWithMetadata(sql, parameterNames, paramValues);
		}
		public SelectedData ExecuteQueryWithMetadata(string sql, string[] parameterNames, QueryParameterCollection parameters) {
			return LogManager.Log<SelectedData>(LogCategory, () => {
				return CommandChannelHelper.ExecuteQueryWithMetadataWithParams(this, sql, parameters, parameterNames);
			}, (d) => {
				return CreateLogMessage("Executing ExecuteQueryWithMetadata()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_Sql, sql,
				LogParam_Parameters, ParametersToString(parameterNames, parameters)
				});
			});
		}
		public Task<SelectedData> ExecuteQueryWithMetadataAsync(string sql, string[] parameterNames, object[] parameterValues, CancellationToken cancellationToken = default(CancellationToken)) {
			QueryParameterCollection paramValues = new QueryParameterCollection();
			foreach(object item in parameterValues)
				paramValues.Add(new OperandValue(item));
			return ExecuteQueryWithMetadataAsync(sql, parameterNames, paramValues, cancellationToken);
		}
		public Task<SelectedData> ExecuteQueryWithMetadataAsync(string sql, string[] parameterNames, QueryParameterCollection parameters, CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync<SelectedData>(LogCategory, () => {
				return CommandChannelHelper.ExecuteQueryWithMetadataWithParamsAsync(this, sql, parameters, parameterNames, cancellationToken);
			}, (d) => {
				return CreateLogMessage("Executing ExecuteQueryWithMetadataAsync()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_Sql, sql,
				LogParam_Parameters, ParametersToString(parameterNames, parameters)
				});
			});
		}
		public SelectedData ExecuteQueryWithMetadata(string sql, object[] parameterValues) {
			QueryParameterCollection paramValues = new QueryParameterCollection();
			foreach(object item in parameterValues)
				paramValues.Add(new OperandValue(item));
			return ExecuteQueryWithMetadata(sql, paramValues);
		}
		public SelectedData ExecuteQueryWithMetadata(string sql, QueryParameterCollection parameters) {
			return LogManager.Log<SelectedData>(LogCategory, () => {
				return CommandChannelHelper.ExecuteQueryWithMetadataWithParams(this, sql, parameters, Array.Empty<string>());
			}, (d) => {
				return CreateLogMessage("Executing ExecuteQueryWithMetadata()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_Sql, sql,
				LogParam_Parameters, ParametersToString(parameters)
				});
			});
		}
		public Task<SelectedData> ExecuteQueryWithMetadataAsync(string sql, object[] parameterValues, CancellationToken cancellationToken = default(CancellationToken)) {
			QueryParameterCollection paramValues = new QueryParameterCollection();
			foreach(object item in parameterValues)
				paramValues.Add(new OperandValue(item));
			return ExecuteQueryWithMetadataAsync(sql, paramValues, cancellationToken);
		}
		public Task<SelectedData> ExecuteQueryWithMetadataAsync(string sql, QueryParameterCollection parameters, CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync<SelectedData>(LogCategory, () => {
				return CommandChannelHelper.ExecuteQueryWithMetadataWithParamsAsync(this, sql, parameters, Array.Empty<string>(), cancellationToken);
			}, (d) => {
				return CreateLogMessage("Executing ExecuteQueryWithMetadata()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString(),
				LogParam_Sql, sql,
				LogParam_Parameters, ParametersToString(parameters)
				});
			});
		}
		object ICommandChannel.Do(string command, object args) {
			ICommandChannel commandChannel = ObjectLayer as ICommandChannel;
			if(commandChannel == null) {
				if(ObjectLayer == null) {
					throw new NotSupportedException(string.Format(CommandChannelHelper.Message_CommandIsNotSupported, command));
				}
				else {
					throw new NotSupportedException(string.Format(CommandChannelHelper.Message_CommandIsNotSupportedEx, command, ObjectLayer.GetType().FullName));
				}
			}
			return commandChannel.Do(command, args);
		}
		Task<object> ICommandChannelAsync.DoAsync(string command, object args, CancellationToken cancellationToken) {
			ICommandChannelAsync commandChannel = ObjectLayer as ICommandChannelAsync;
			if(commandChannel == null) {
				if(ObjectLayer == null) {
					throw new NotSupportedException(string.Format(CommandChannelHelper.Message_CommandIsNotSupported, command));
				}
				else {
					throw new InvalidOperationException(Xpo.Res.GetString(Xpo.Res.Async_CommandChannelDoesNotImplementICommandChannelAsync, ObjectLayer.GetType().FullName));
				}
			}
			return commandChannel.DoAsync(command, args, cancellationToken);
		}
		protected delegate object[] LogParametersHandler();
		protected LogMessage CreateLogMessage(string title, TimeSpan duration, params object[] parameters) {
			LogMessage message = new LogMessage(LogMessageType.SessionEvent, title, duration);
			if(parameters != null) {
				for(int i = 0; i < parameters.Length; i += 2) {
					message.ParameterList.Add(new LogMessageParameter((string)parameters[i], parameters[i + 1]));
				}
			}
			return message;
		}
		protected string GetObjectString(object theObject) {
			if(theObject == null)
				return "null";
			Type objectType = theObject.GetType();
			XPClassInfo classInfo = Dictionary.QueryClassInfo(objectType);
			if(classInfo == null || classInfo.KeyProperty == null)
				return objectType.ToString();
			return string.Format("{0}({1})", objectType, classInfo.GetId(theObject));
		}
		IServiceProvider IInfrastructure<IServiceProvider>.Instance => serviceProvider;
		ISecuredPropertyAccessor IInfrastructure<ISecuredPropertyAccessor>.Instance {
			get {
#if NET
				if(ObjectLayer is IInfrastructure<ISecuredPropertyAccessor> infrastructure) {
					return infrastructure.Instance;
				}
#endif
				return null;
			}
		}
	}
	[Description("Loads and saves persistent objects keeping track of every change to every persistent object during a transaction.")]
	[DXToolboxItem(true)]
	[DevExpress.Utils.ToolboxTabName(AssemblyInfo.DXTabOrmComponents)]
#if !NET
	[System.Drawing.ToolboxBitmap(typeof(UnitOfWork))]
#endif
	public class UnitOfWork : Session {
		protected internal override bool IsUnitOfWork {
			get {
				return true;
			}
		}
		public UnitOfWork()
			: this((IServiceProvider)null) { }
		public UnitOfWork(IServiceProvider serviceProvider)
			: base(serviceProvider) { }
		public UnitOfWork(IContainer container)
			: this(null, container) { }
		public UnitOfWork(IServiceProvider serviceProvider, IContainer container)
			: base(serviceProvider, container) { }
		public UnitOfWork(XPDictionary dictionary)
			: this(null, dictionary) { }
		public UnitOfWork(IServiceProvider serviceProvider, XPDictionary dictionary)
			: base(serviceProvider, dictionary) { }
		public UnitOfWork(IDataLayer layer, params IDisposable[] disposeOnDisconnect)
			: this(null, layer, disposeOnDisconnect) { }
		public UnitOfWork(IServiceProvider serviceProvider, IDataLayer layer, params IDisposable[] disposeOnDisconnect)
			: base(serviceProvider, layer, disposeOnDisconnect) { }
		public UnitOfWork(IObjectLayer layer, params IDisposable[] disposeOnDisconnect)
			: this(null, layer, disposeOnDisconnect) { }
		public UnitOfWork(IServiceProvider serviceProvider, IObjectLayer layer, params IDisposable[] disposeOnDisconnect)
			: base(serviceProvider, layer, disposeOnDisconnect) { }
		public void CommitChanges() {
			WaitForAsyncOperationEnd();
			if(!InTransaction)
				return;
			CommitTransaction();
		}
		public object CommitChangesAsync(AsyncCommitCallback callback) {
			if(!InTransaction) {
				callback(null);
				return null;
			}
			return CommitTransactionAsync(callback);
		}
		public Task CommitChangesAsync(CancellationToken cancellationToken = default(CancellationToken)) {
			if(!InTransaction) {
				return Task.FromResult(false);
			}
			return CommitTransactionAsync(cancellationToken);
		}
		public void ReloadChangedObjects() {
			if(!InTransaction)
				return;
			ObjectSet objects2reload = new ObjectSet();
			foreach(object obj in GetObjectsToDelete()) {
				objects2reload.Add(obj);
			}
			foreach(object obj in GetObjectsToSave()) {
				objects2reload.Add(obj);
			}
			foreach(object obj in objects2reload) {
				try {
					Reload(obj);
				}
				catch(CannotLoadObjectsException) { }
			}
			System.Diagnostics.Debug.Assert(GetObjectsToDelete().Count == 0 && GetObjectsToSave().Count == 0);
			RollbackTransaction();	
		}
	}
	[DXToolboxItem(true)]
	[DevExpress.Utils.ToolboxTabName(AssemblyInfo.DXTabOrmComponents)]
#if !NET
	[System.Drawing.ToolboxBitmap(typeof(ExplicitUnitOfWork))]
#endif
	public class ExplicitUnitOfWork : UnitOfWork {
		public ExplicitUnitOfWork()
			: this((IServiceProvider)null) { }
		public ExplicitUnitOfWork(IServiceProvider serviceProvider)
			: base(serviceProvider) { }
		public ExplicitUnitOfWork(IContainer container)
			: this(null, container) { }
		public ExplicitUnitOfWork(IServiceProvider serviceProvider, IContainer container)
			: base(serviceProvider, container) { }
		public ExplicitUnitOfWork(XPDictionary dictionary)
			: this(null, dictionary) { }
		public ExplicitUnitOfWork(IServiceProvider serviceProvider, XPDictionary dictionary)
			: base(serviceProvider, dictionary) { }
		public ExplicitUnitOfWork(IDataLayer layer, params IDisposable[] disposeOnDisconnect)
			: this(null, layer, disposeOnDisconnect) { }
		public ExplicitUnitOfWork(IServiceProvider serviceProvider, IDataLayer layer, params IDisposable[] disposeOnDisconnect)
			: base(serviceProvider, layer, disposeOnDisconnect) { }
		public ExplicitUnitOfWork(IObjectLayer layer, params IDisposable[] disposeOnDisconnect)
			: this(null, layer, disposeOnDisconnect) { }
		public ExplicitUnitOfWork(IServiceProvider serviceProvider, IObjectLayer layer, params IDisposable[] disposeOnDisconnect)
			: base(serviceProvider, layer, disposeOnDisconnect) { }
		bool inTransaction;
		[Description("Indicates whether the explicit unit of work has started a database level transaction or tracked data modifications.")]
		public override bool InTransaction {
			get {
				return inTransaction || TrackingChanges;
			}
		}
		public override void BeginTransaction() {
			LogManager.Log(LogCategory, () => {
				OnBeforeBeginTransaction();
				if(inTransaction)
					throw new TransactionSequenceException(Res.GetString(Res.Session_TranSequenceBegin));
				ExplicitBeginTransaction();
				inTransaction = true;
				OnAfterBeginTransaction();
			}, (d) => {
				return CreateLogMessage("Executing BeginTransaction()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString()
				});
			}, null);
		}
		public override void RollbackTransaction() {
			LogManager.Log(LogCategory, () => {
				OnBeforeRollbackTransaction();
				if(TrackingChanges)
					DropChanges();
				if(inTransaction) {
					ExplicitRollbackTransaction();
					inTransaction = false;
				}
				OnAfterRollbackTransaction();
			}, (d) => {
				return CreateLogMessage("Executing RollbackTransaction()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString()
				});
			}, null);
		}
		public Task RollbackTransactionAsync(CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync(LogCategory, async () => {
				OnBeforeRollbackTransaction();
				if(TrackingChanges)
					DropChanges();
				if(inTransaction) {
					await ExplicitRollbackTransactionAsync(cancellationToken);
					inTransaction = false;
				}
				cancellationToken.ThrowIfCancellationRequested();
				OnAfterRollbackTransaction();
			}, (d) => {
				return CreateLogMessage("Executing RollbackTransaction()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString()
				});
			}, null);
		}
		public override void CommitTransaction() {
			LogManager.Log(LogCategory, () => {
				OnBeforeCommitTransaction();
				if(TrackingChanges) {
					FlushChanges();
				}
				if(inTransaction) {
					ExplicitCommitTransaction();
					inTransaction = false;
				}
				OnAfterCommitTransaction();
			}, (d) => {
				return CreateLogMessage("Executing CommitTransaction()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString()
				});
			}, (ex) => {
				return !OnFailedCommitTransaction(ex);
			});
		}
		public override Task CommitTransactionAsync(CancellationToken cancellationToken = default(CancellationToken)) {
			return LogManager.LogAsync(LogCategory, async () => {
				OnBeforeCommitTransaction();
				if(TrackingChanges) {
					cancellationToken.ThrowIfCancellationRequested();
					await FlushChangesAsync(cancellationToken);
				}
				if(inTransaction) {
					cancellationToken.ThrowIfCancellationRequested();
					await ExplicitCommitTransactionAsync(cancellationToken);
					inTransaction = false;
				}
				cancellationToken.ThrowIfCancellationRequested();
				OnAfterCommitTransaction();
			}, (d) => {
				return CreateLogMessage("Executing CommitTransactionAsync()", d, new object[] {
				LogParam_SessionType, this.GetType().ToString(),
				LogParam_SessionID, this.ToString()
				});
			}, (ex) => {
				return !OnFailedCommitTransaction(ex);
			});
		}
		public override object CommitTransactionAsync(AsyncCommitCallback callback) {
			try {
				OnBeforeCommitTransaction();
				if(TrackingChanges) {
					return FlushChangesAsync(new AsyncCommitCallback(delegate (Exception ex) {
						try {
							if(ex == null) {
								if(inTransaction) {
									ExplicitCommitTransaction();
									inTransaction = false;
								}
								OnAfterCommitTransaction();
							}
						}
						catch(Exception e) {
							if(!OnFailedCommitTransaction(e)) {
								callback(e);
							}
							return;
						}
						if(ex == null || !OnFailedCommitTransaction(ex)) {
							callback(ex);
						}
					}));
				}
				else {
					if(inTransaction) {
						ExplicitCommitTransaction();
						inTransaction = false;
					}
					OnAfterCommitTransaction();
				}
			}
			catch(Exception syncEx) {
				if(!OnFailedCommitTransaction(syncEx)) {
					throw;
				}
			}
			return null;
		}
		protected override bool IsInTransactionMode {
			get { return false; }
		}
		protected override void OnBeforeBeginTrackingChanges() {
			OnBeforeBeginTrackingChangesInternal();
		}
		protected override void OnAfterBeginTrackingChanges() {
			OnAfterBeginTrackingChangesInternal();
		}
		protected override void OnBeforeFlushChanges() {
			OnBeforeFlushChangesInternal();
		}
		protected override void OnAfterFlushChanges() {
			OnAfterFlushChangesInternal();
		}
		protected override void OnBeforeDropChanges() {
			OnBeforeDropChangesInternal();
		}
		protected override void OnAfterDropChanges() {
			OnAfterDropChangesInternal();
		}
		protected override bool OnFailedFlushChanges(Exception ex) {
			return OnFailedFlushChangesInternal(ex);
		}
		protected override IList BeginFlushChanges() {
			if(!inTransaction) {
				BeginTransaction();
			}
			return base.BeginFlushChanges();
		}
		protected override Task<IList> BeginFlushChangesAsync(int asyncOperationId, CancellationToken cancellationToken = default(CancellationToken)) {
			if(!inTransaction) {
				BeginTransaction();
			}
			return base.BeginFlushChangesAsync(asyncOperationId, cancellationToken);
		}
		internal override List<object[]> SelectDataInternal(XPClassInfo classInfo, CriteriaOperatorCollection properties, CriteriaOperator criteria, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria, bool selectDeleted, int skipSelectedRecords, int topSelectedRecords, SortingCollection sorting) {
			List<object[]> result = PrepareSelectData(classInfo, ref properties, ref criteria, ref groupProperties, ref groupCriteria, ref sorting, true, this);
			if(result == null) {
				ObjectsQuery preparedQuery = new ObjectsQuery(classInfo, criteria, sorting, skipSelectedRecords, topSelectedRecords, new CollectionCriteriaPatcher(selectDeleted, TypesManager), true);
				if(IsFlushRequired(new ObjectsQuery[] { preparedQuery })) {
					FlushChanges();
				}
				result = ObjectLayer.SelectData(this, preparedQuery, properties, groupProperties, groupCriteria);
			}
			return result;
		}
		internal override async Task<List<object[]>> SelectDataInternalAsync(XPClassInfo classInfo, CriteriaOperatorCollection properties, CriteriaOperator criteria, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria, bool selectDeleted, int skipSelectedRecords, int topSelectedRecords, SortingCollection sorting, CancellationToken cancellationToken = default(CancellationToken)) {
			await UpdateSchemaAsync(cancellationToken, classInfo);
			await TypesManager.EnsureIsTypedObjectValidAsync(cancellationToken);
			List<object[]> result = PrepareSelectDataInternal(classInfo, ref properties, ref criteria, ref groupProperties, ref groupCriteria, ref sorting, true, this);
			if(result == null) {
				ObjectsQuery preparedQuery = new ObjectsQuery(classInfo, criteria, sorting, skipSelectedRecords, topSelectedRecords, new CollectionCriteriaPatcher(selectDeleted, TypesManager), true);
				if(IsFlushRequired(new ObjectsQuery[] { preparedQuery })) {
					await FlushChangesAsync(cancellationToken);
				}
				result = await GetObjectLayerAsync().SelectDataAsync(this, preparedQuery, properties, groupProperties, groupCriteria, cancellationToken);
			}
			return result;
		}
		internal override object SelectDataInternalAsync(XPClassInfo classInfo, CriteriaOperatorCollection properties, CriteriaOperator criteria, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria, bool selectDeleted, int skipSelectedRecords, int topSelectedRecords, SortingCollection sorting, AsyncSelectDataCallback callback) {
			AsyncOperationsHelper.FailIfSynchronizationContextUnavailable();
			Utils.Guard.ArgumentNotNull(callback, nameof(callback));
			List<object[]> result = PrepareSelectData(classInfo, ref properties, ref criteria, ref groupProperties, ref groupCriteria, ref sorting, true, this);
			if(result != null) {
				return ObjectLayer.SelectDataAsync(this, null, properties, groupProperties, groupCriteria, callback);
			}
			else {
				ObjectsQuery preparedQuery = new ObjectsQuery(classInfo, criteria, sorting, skipSelectedRecords, topSelectedRecords, new CollectionCriteriaPatcher(selectDeleted, TypesManager), true);
				if(IsFlushRequired(new ObjectsQuery[] { preparedQuery })) {
					return FlushChangesAsync(new AsyncCommitCallback(delegate (Exception ex) {
						if(ex != null) {
							callback(null, ex);
							return;
						}
						try {
							ObjectLayer.SelectDataAsync(this, preparedQuery, properties, groupProperties, groupCriteria, callback);
						}
						catch(Exception e) {
							callback(null, e);
						}
					}));
				}
				return ObjectLayer.SelectDataAsync(this, preparedQuery, properties, groupProperties, groupCriteria, callback);
			}
		}
		internal override ICollection[] GetObjectsInternal(ObjectsQuery[] queries) {
			if(IsFlushRequired(queries)) {
				FlushChanges();
			}
			return base.GetObjectsInternal(queries);
		}
		internal override async Task<ICollection[]> GetObjectsInternalAsync(ObjectsQuery[] queries, CancellationToken cancellationToken = default(CancellationToken)) {
			if(IsFlushRequired(queries)) {
				cancellationToken.ThrowIfCancellationRequested();
				await FlushChangesAsync(cancellationToken);
			}
			cancellationToken.ThrowIfCancellationRequested();
			return await base.GetObjectsInternalAsync(queries, cancellationToken);
		}
		internal override object GetObjectsInternalAsync(ObjectsQuery[] queries, AsyncLoadObjectsCallback callback) {
			AsyncOperationsHelper.FailIfSynchronizationContextUnavailable();
			Utils.Guard.ArgumentNotNull(callback, nameof(callback));
			if(IsFlushRequired(queries)) {
				return FlushChangesAsync(new AsyncCommitCallback(delegate (Exception ex) {
					if(ex != null) {
						callback(null, ex);
						return;
					}
					try {
						GetObjectsInternalAsync(queries, callback);
					}
					catch(Exception e) {
						callback(null, e);
					}
				}));
			}
			return base.GetObjectsInternalAsync(queries, callback);
		}
		bool IsFlushRequired(ObjectsQuery[] queries) {
			bool hasModifiedNodes = false;
			if(TrackingChanges) {
				ICollection objectsToSave = GetObjectsToSave(true);
				ICollection objectsToDelete = GetObjectsToDelete(true);
				if(objectsToSave.Count != 0 || objectsToDelete.Count != 0) {
					ObjectsQuery[] preparedQueries = PrepareQueries(queries, false, this);
					for(int i = 0; i < preparedQueries.Length; i++) {
						AnalyzeResult analyzeResult;
						string[] modifiedNodes = InTransactionLoader.GetModifiedNodes(this, objectsToSave, objectsToDelete, preparedQueries[i], out analyzeResult);
						if(modifiedNodes.Length != 0) {
							hasModifiedNodes = true;
							break;
						}
					}
				}
			}
			return hasModifiedNodes;
		}
	}
	public struct LoadDataMemberOrderItem {
		public readonly int IndexInResultSet;
		public readonly string ClassMemberName;
		public LoadDataMemberOrderItem(int indexInResultSet, string classMemberName) {
			this.IndexInResultSet = indexInResultSet;
			this.ClassMemberName = classMemberName;
		}
	}
}
