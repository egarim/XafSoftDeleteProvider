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
using System.Data;
using System.Collections;
using System.Collections.Generic;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.Metadata;
using DevExpress.Data.Filtering.Helpers;
using DevExpress.Data.Filtering;
using DevExpress.Xpo.Metadata.Helpers;
using System.IO;
using DevExpress.Xpo.Helpers;
using DevExpress.Xpo.Generators;
using DevExpress.Xpo.DB.Exceptions;
using DevExpress.Data.Filtering.Exceptions;
using System.Xml.Serialization;
using System.Threading.Tasks;
using System.Threading;
namespace DevExpress.Xpo.Helpers {
	public abstract class NestedGuidParentMap {
		protected NestedGuidParentMap() { }
		public abstract void Add(object parent, Guid nestedGuid, bool hasValidKey);
		public abstract object GetParent(Guid nestedGuid);
		public abstract Guid GetNested(object parent);
		public abstract void KickOut(object parent);
		public abstract void KickOut(Guid nestedGuid);
	}
	public class StrongNestedGuidParentMap : NestedGuidParentMap {
		Dictionary<Guid, object> Parents = new Dictionary<Guid, object>();
		ObjectDictionary<Guid> Nesteds = new ObjectDictionary<Guid>();
		public override void Add(object parent, Guid nestedGuid, bool hasValidKey) {
			Parents.Add(nestedGuid, parent);
			Nesteds.Add(parent, nestedGuid);
		}
		public override object GetParent(Guid nestedGuid) {
			object rv;
			if(Parents.TryGetValue(nestedGuid, out rv))
				return rv;
			else
				return null;
		}
		public override Guid GetNested(object parent) {
			Guid rv;
			if(Nesteds.TryGetValue(parent, out rv))
				return rv;
			else
				return Guid.Empty;
		}
		public override void KickOut(Guid nestedGuid) {
			object parent;
			if(!Parents.TryGetValue(nestedGuid, out parent))
				return;
			Parents.Remove(nestedGuid);
			Nesteds.Remove(parent);
		}
		public override void KickOut(object parent) {
			Guid nestedGuid;
			if(!Nesteds.TryGetValue(parent, out nestedGuid))
				return;
			Parents.Remove(nestedGuid);
			Nesteds.Remove(parent);
		}
	}
	public class WeakNestedGuidParentMap : StrongNestedGuidParentMap {
		Dictionary<Guid, ObjectRecord> Parents = new Dictionary<Guid, ObjectRecord>();
		Dictionary<ObjectRecord, Guid> Nesteds = new Dictionary<ObjectRecord, Guid>();
		public override void Add(object parent, Guid nestedGuid, bool hasValidKey) {
			if(hasValidKey) {
				ObjectRecord pRecord = ObjectRecord.GetObjectRecord(parent);
				Nesteds[pRecord] = nestedGuid;
				Parents[nestedGuid] = pRecord;
			} else {
				base.Add(parent, nestedGuid, hasValidKey);
			}
		}
		public override object GetParent(Guid nestedGuid) {
			object rv = base.GetParent(nestedGuid);
			if(rv != null)
				return rv;
			ObjectRecord pRecord;
			if(Parents.TryGetValue(nestedGuid, out pRecord))
				return pRecord.Object;
			else
				return null;
		}
		public override Guid GetNested(object parent) {
			Guid rv = base.GetNested(parent);
			if(rv != Guid.Empty)
				return rv;
			ObjectRecord pRecord = ObjectRecord.GetObjectRecord(parent);
			Guid nestedGuid;
			if(Nesteds.TryGetValue(pRecord, out nestedGuid))
				return nestedGuid;
			else
				return Guid.Empty;
		}
		public override void KickOut(Guid nestedGuid) {
			base.KickOut(nestedGuid);
			ObjectRecord pRecord;
			if(!Parents.TryGetValue(nestedGuid, out pRecord))
				return;
			Parents.Remove(nestedGuid);
			Nesteds.Remove(pRecord);
		}
		public override void KickOut(object parent) {
			base.KickOut(parent);
			ObjectRecord pRecord = ObjectRecord.GetObjectRecord(parent);
			Guid nestedGuid;
			if(!Nesteds.TryGetValue(pRecord, out nestedGuid))
				return;
			Parents.Remove(nestedGuid);
			Nesteds.Remove(pRecord);
		}
	}
	public static class NestedStubWorksHelper {
		public static object CommitObjectProperties(Session parentSession, NestedGuidParentMap map, XPObjectClassInfoStubCache ciCache, XPClassInfo ci, XPObjectStub obj, object parent) {
			parentSession.TriggerObjectLoading(parent);
			XPClassInfoStub ciStub = ciCache.GetStub(ci);
			bool trackModifications = ci.TrackPropertiesModifications ?? parentSession.TrackPropertiesModifications;
			ci.KeyProperty.SetValue(parent, obj.Key);
			foreach(XPMemberInfo mi in ci.PersistentProperties) {
				if(mi.IsReadOnly)
					continue;
				if(mi.IsFetchOnly)
					continue;
				if(!ciStub.IsMemberChanged(obj, mi.Name)) 
					continue;
				object value = ciStub.GetMemberValue(obj, mi.Name);
				if (mi.ReferenceType != null && value != null) {
					XPObjectStub refStub = value as XPObjectStub;
					if (refStub == null) 
						continue;
					value = NestedStubWorksHelper.CreateParentObject(parentSession, map, mi.ReferenceType, refStub);
				}
				if(mi.ReferenceType != null) {
					object oldValue = mi.GetValue(parent);
					mi.SetValue(parent, value);
					if(trackModifications && !PersistentBase.CanSkipAssignment(oldValue, value))
						mi.SetModified(parent, oldValue);
					mi.ProcessAssociationRefChange(parentSession, parent, oldValue, value);
				} else {
					if(mi.Converter != null) {
						value = mi.Converter.ConvertFromStorageType(value);
					}
					if(trackModifications) {
						object oldValue = mi.GetValue(parent);
						mi.SetValue(parent, value);
						if(!PersistentBase.CanSkipAssignment(oldValue, value)) 
							mi.SetModified(parent, oldValue);						   
					} else
						mi.SetValue(parent, value);
				}
			}
			if(parent is IntermediateObject && parentSession.IsNewObject(parent)) {
				IntermediateObject intObj = (IntermediateObject)parent;
				IntermediateClassInfo intObjClassInfo = (IntermediateClassInfo)parentSession.GetClassInfo(intObj);
				System.Diagnostics.Debug.Assert(intObj.LeftIntermediateObjectField != null);
				System.Diagnostics.Debug.Assert(intObj.RightIntermediateObjectField != null);
				XPBaseCollection leftC = (XPBaseCollection)intObjClassInfo.intermediateObjectFieldInfoRight.refProperty.GetValue(intObj.LeftIntermediateObjectField);
				XPRefCollectionHelperManyToMany leftHelper = (XPRefCollectionHelperManyToMany)leftC.Helper;
				leftHelper.AddIntermediateObject(intObj, intObj.RightIntermediateObjectField);
				leftC.BaseAdd(intObj.RightIntermediateObjectField);
			}
			return parent;
		}
		public static void CommitDeletedObjects(Session parentSession, NestedGuidParentMap map, XPObjectClassInfoStubCache ciCache, XPClassInfo[] classInfoList, XPObjectStubCollection objectList) {
			object[] parentObjects = NestedStubWorksHelper.GetParentObjects(parentSession, map, classInfoList, objectList);
			for(int i = 0; i < objectList.Count; i++) {
				XPObjectStub obj = objectList[i];
				XPClassInfo ci = classInfoList[i];
				object parentObj = parentObjects[i];
				if(parentObj == null)
					continue;
				XPClassInfoStub ciStub = ciCache.GetStub(ci);
				bool trackModifications = ci.TrackPropertiesModifications ?? parentSession.TrackPropertiesModifications;
				if(trackModifications) {
					foreach(XPMemberInfo mi in ci.PersistentProperties) {
						if(mi.IsReadOnly)
							continue;
						if(mi.IsFetchOnly)
							continue;
						if(!ciStub.IsMemberChanged(obj, mi.Name)) 
							continue;
						if(!mi.GetModified(parentObj))
							mi.SetModified(parentObj, mi.GetValue(parentObj));
					}
				}
				if(obj.IsIntermediate) {
					IntermediateObject intParent = (IntermediateObject)parentObj;
					IntermediateClassInfo intObjClassInfo = (IntermediateClassInfo)parentSession.GetClassInfo(intParent);
					if(intParent.LeftIntermediateObjectField != null && intParent.RightIntermediateObjectField != null) {
						XPBaseCollection leftC = (XPBaseCollection)intObjClassInfo.intermediateObjectFieldInfoRight.refProperty.GetValue(intParent.LeftIntermediateObjectField);
						leftC.BaseRemove(intParent.RightIntermediateObjectField);
					}
				}
				parentSession.Delete(parentObj);
			}
		}
		public static object[] CreateParentObjects(Session parentSession, NestedGuidParentMap map, XPClassInfo[] classInfoList, XPObjectStubCollection objectList) {
			object[] parents = NestedStubWorksHelper.GetParentObjects(parentSession, map, classInfoList, objectList);
			for(int i = 0; i < objectList.Count; i++) {
				object parent = parents[i];
				if(parent != null) continue;
				parent = classInfoList[i].CreateObject(parentSession);
				XPObjectStub obj = objectList[i];
				map.Add(parent, obj.Guid, !obj.IsNew);
				parents[i] = parent;
			}
			return parents;
		}
		public static object CreateParentObject(Session parentSession, NestedGuidParentMap map, XPClassInfo ci, XPObjectStub obj) {
			object parent = NestedStubWorksHelper.GetParentObject(parentSession, map, ci, obj);
			if(parent != null)
				return parent;
			parent = ci.CreateObject(parentSession);
			map.Add(parent, obj.Guid, !obj.IsNew);
			return parent;
		}
		public static void ValidateVersions(Session parentSession, NestedGuidParentMap map, XPObjectClassInfoStubCache ciCache, ObjectSet lockedParentsObjects, XPClassInfo[] objectClassInfoList, XPObjectStubCollection nestedObjects, LockingOption lockingOption, bool objectsToDelete) {
			object[] parentObjects = NestedStubWorksHelper.GetParentObjects(parentSession, map, objectClassInfoList, nestedObjects);
			for(int i = 0; i < nestedObjects.Count; i++) {
				XPClassInfo ci = objectClassInfoList[i];
				XPObjectStub obj = nestedObjects[i];
				object parentObj = parentObjects[i];
				if(parentObj == null) {
					if(!obj.IsNew)
						throw new LockingException();
					continue;
				}
				if(lockedParentsObjects.Contains(parentObj))
					continue;
				lockedParentsObjects.Add(parentObj);
				if(obj.IsNew != parentSession.IsNewObject(parentObj))
					throw new LockingException();
				if (lockingOption == LockingOption.Optimistic) {
					OptimisticLockingBehavior kind = ci.OptimisticLockingBehavior;
					switch (kind) {
						case OptimisticLockingBehavior.ConsiderOptimisticLockingField: {
								XPMemberInfo olf = ci.OptimisticLockField;
								if (olf == null)
									continue;
								int? parentV = (int?)olf.GetValue(parentObj);
								int? childV = (int?)obj.OptimisticLockFieldInDataLayer;
								if (parentV != childV) {
									throw new LockingException();
								}
							} break;
						case OptimisticLockingBehavior.LockAll:
						case OptimisticLockingBehavior.LockModified:
							if (LockingHelper.HasModified(ciCache.GetStub(ci), obj, ci.PersistentProperties, parentObj, objectsToDelete ? OptimisticLockingBehavior.LockAll : kind))
								throw new LockingException();
							break;
					}
				}
			}
		}
		public static object[] GetParentObjects(Session parentSession, NestedGuidParentMap map, XPClassInfo[] classInfoList, XPObjectStubCollection objectList) {
			object[] result = new object[objectList.Count];
			List<int> indexListToLoad = null;
			for(int i = 0; i < objectList.Count; i++) {
				object parent = null;
				XPObjectStub obj = objectList[i];
				if(obj.HasGuid) {
					parent = map.GetParent(obj.Guid);
					if(parent != null) {
						result[i] = parent;
						continue;
					}
				}
				if(obj.IsNew) {
					result[i] = parent;
					continue;
				}
				if(indexListToLoad == null) indexListToLoad = new List<int>(objectList.Count);
				indexListToLoad.Add(i);
			}
			if(indexListToLoad == null) return result;
			Dictionary<XPClassInfo, bool> usedClassInfoDict = new Dictionary<XPClassInfo, bool>();
			for(int i = 0; i < indexListToLoad.Count; i++) {
				int index = indexListToLoad[i];
				XPClassInfo currentClassInfo = classInfoList[index];
				if(usedClassInfoDict.ContainsKey(currentClassInfo)) continue;
				usedClassInfoDict.Add(currentClassInfo, true);
				List<int> indexList = new List<int>(objectList.Count - i);
				List<object> keyList = new List<object>(objectList.Count - i);
				indexList.Add(index);
				keyList.Add(objectList[index].Key);
				for(int j = (i + 1); j < indexListToLoad.Count; j++) {
					int jIndex = indexListToLoad[j];
					if(classInfoList[jIndex] != currentClassInfo) continue;
					indexList.Add(jIndex);
					keyList.Add(objectList[jIndex].Key);
				}
				ICollection currentResult = parentSession.GetObjectsByKey(currentClassInfo, keyList, false);
				bool isStaticType = parentSession.ObjectLayer.IsStaticType(currentClassInfo);
				int collectionIndex = 0;
				foreach(object parent in currentResult) {
					result[indexList[collectionIndex]] = parent;
					if(parent != null) {
						map.KickOut(parent);
						map.Add(parent, objectList[indexList[collectionIndex]].Guid, true);
					}
					collectionIndex++;
				}
			}
			return result;
		}
		public static object GetParentObject(Session parentSession, NestedGuidParentMap map, XPClassInfo ci, XPObjectStub obj) {
			object parent = null;
			if(obj.HasGuid) {
				parent = map.GetParent(obj.Guid);
				if(parent != null)
					return parent;
			}
			if(obj.IsNew)
				return parent;
			object key = obj.Key;
			parent = parentSession.GetObjectByKey(ci, key);
			if(parent == null)
				return null;
			map.KickOut(parent);
			map.Add(parent, obj.Guid, true);
			return parent;
		}
		[ThreadStatic]
		static Dictionary<Guid, bool> createNestedStubDictionary;
		public static Dictionary<Guid, bool> CreateNestedStubDictionary {
			get {
				if(createNestedStubDictionary == null) {
					createNestedStubDictionary = new Dictionary<Guid, bool>();
				}
				return createNestedStubDictionary;
			}
		}
		public static XPObjectStub CreateNestedStubObjectJustWithIdentities(Session session, NestedGuidParentMap map, XPObjectClassInfoStubCache ciCache, object obj) {
			if(obj == null) return null;
			Guid nestedGuid = map.GetNested(obj);
			bool isNew = session.IsNewObject(obj);
			if(nestedGuid == Guid.Empty) {
				nestedGuid = Guid.NewGuid();
				map.Add(obj, nestedGuid, !isNew);
			}
			XPClassInfo ci = session.GetClassInfo(obj);
			XPClassInfoStub ciStub = ciCache.GetStub(ci);
			XPObjectStub objStub = new XPObjectStub(ciStub, nestedGuid);
			objStub.MarkAsEmpty();
			if(isNew) objStub.MarkAsNew();
			if(obj is IntermediateObject) objStub.MarkAsIntermediate();
			objStub.Key = ci.KeyProperty.GetValue(obj);
			if(ci.OptimisticLockField != null) {
				ciStub.SetMemberValue(objStub, ciStub.OptimisticLockFieldName, ci.OptimisticLockField.GetValue(obj));
			}
			if(ci.OptimisticLockFieldInDataLayer != null) {
				objStub.OptimisticLockFieldInDataLayer = ci.OptimisticLockFieldInDataLayer.GetValue(obj);
			}
			return objStub;
		}
		public static XPObjectStub CreateNestedStubObject(Session session, NestedGuidParentMap map, XPObjectStubCache objCache, XPObjectClassInfoStubCache ciCache, object obj) {
			if(obj == null) return null;
			Guid nestedGuid = map.GetNested(obj);
			bool isNew = session.IsNewObject(obj);
			if(nestedGuid == Guid.Empty) {
				nestedGuid = Guid.NewGuid();
				map.Add(obj, nestedGuid, !isNew);
			} else {
				if(CreateNestedStubDictionary.ContainsKey(nestedGuid)) {
					return CreateNestedStubObjectJustWithIdentities(session, map, ciCache, obj);
				}
				if(objCache != null) {
					XPObjectStub objStubCached = objCache.GetStub(nestedGuid);
					if(objStubCached != null) return objStubCached;
				}
			}
			CreateNestedStubDictionary.Add(nestedGuid, true);
			try {
				XPClassInfo ci = session.GetClassInfo(obj);
				XPClassInfoStub ciStub = ciCache.GetStub(ci);
				XPObjectStub objStub = new XPObjectStub(ciStub, nestedGuid);
				if(isNew) objStub.MarkAsNew();
				if(obj is IntermediateObject) objStub.MarkAsIntermediate();
				if(objCache != null) objCache.Add(nestedGuid, objStub);
				objStub.Key = ci.KeyProperty.GetValue(obj);
				foreach(XPMemberInfo mi in ci.PersistentProperties) {
					if(mi.IsReadOnly || mi.IsKey)
						continue;
					object value;
					if(mi.IsDelayed && !session.IsNewObject(obj)) {
						XPDelayedProperty srcDelayed = XPDelayedProperty.GetDelayedPropertyContainer(obj, mi);
						if(mi.ReferenceType == null) {
							if(srcDelayed.IsLoaded) {
								value = srcDelayed.Value;
							} else {
								continue;
							}
						} else {
							if(!srcDelayed.IsLoaded) {
								ciStub.SetMemberValue(objStub, mi.Name, srcDelayed.InternalValue);
								continue;
							}
							value = srcDelayed.Value;
							if(value == null) {
								ciStub.SetMemberValue(objStub, mi.Name, null);
								continue;
							}
						}
					} else
						value = mi.GetValue(obj);
					if(mi.ReferenceType != null && value != null)
						value = NestedStubWorksHelper.CreateNestedStubObject(session, map, objCache, ciCache, value);
					if(mi.Converter != null) {
						value = mi.Converter.ConvertToStorageType(value);
					}
					ciStub.SetMemberValue(objStub, mi.Name, value);
				}
				return objStub;
			} finally {
				CreateNestedStubDictionary.Remove(nestedGuid);
			}
		}
		public static XPObjectStub CreateParentStubObjectJustWithIdentities(Session session, NestedParentGuidMap map, XPObjectClassInfoStubCache ciCache, object obj) {
			if(obj == null) return null;
			bool isNew = session.IsNewObject(obj);
			Guid parentGuid = map.GetParent(obj);
			if(parentGuid == Guid.Empty) {
				parentGuid = Guid.NewGuid();
				map.Add(parentGuid, obj, !isNew);
			}
			XPClassInfo ci = session.GetClassInfo(obj);
			XPClassInfoStub ciStub = ciCache.GetStub(ci);
			XPObjectStub objStub = new XPObjectStub(ciStub, parentGuid);
			if(session.IsNewObject(obj)) objStub.MarkAsNew();
			if(obj is IntermediateObject) objStub.MarkAsIntermediate();
			objStub.Key = ci.KeyProperty.GetValue(obj);
			if(ci.OptimisticLockField != null) {
				ciStub.SetMemberValue(objStub, ciStub.OptimisticLockFieldName, ci.OptimisticLockField.GetValue(obj));
			}
			if(ci.OptimisticLockFieldInDataLayer != null) {
				objStub.OptimisticLockFieldInDataLayer = ci.OptimisticLockFieldInDataLayer.GetValue(obj);
			}
			return objStub;
		}
		[ThreadStatic]
		static Dictionary<Guid, bool> createParentStubDictionary;
		public static Dictionary<Guid, bool> CreateParentStubDictionary {
			get {
				if(createParentStubDictionary == null) {
					createParentStubDictionary = new Dictionary<Guid, bool>();
				}
				return createParentStubDictionary;
			}
		}
		public static XPObjectStub CreateParentStubObject(Session session, NestedParentGuidMap map, XPObjectStubCache objCache, XPObjectClassInfoStubCache ciCache, object obj) {
			if(obj == null) return null;
			bool isNew = session.IsNewObject(obj);
			Guid parentGuid = map.GetParent(obj);
			if(parentGuid == Guid.Empty) {
				parentGuid = Guid.NewGuid();
				map.Add(parentGuid, obj, !isNew);
			} else {
				if(CreateParentStubDictionary.ContainsKey(parentGuid)) {
					return CreateParentStubObjectJustWithIdentities(session, map, ciCache, obj);
				}
				if(objCache != null) {
					XPObjectStub objStubCached = objCache.GetStub(parentGuid);
					if(objStubCached != null) return objStubCached;
				}
			}
			CreateParentStubDictionary.Add(parentGuid, true);
			try {
				XPClassInfo ci = session.GetClassInfo(obj);
				XPClassInfoStub ciStub = ciCache.GetStub(ci);
				XPObjectStub objStub = new XPObjectStub(ciStub, parentGuid);
				if(isNew) objStub.MarkAsNew();
				if(obj is IntermediateObject) objStub.MarkAsIntermediate();
				if(objCache != null) objCache.Add(parentGuid, objStub);
				objStub.Key = ci.KeyProperty.GetValue(obj);
				foreach(XPMemberInfo mi in session.GetPropertiesListForUpdateInsert(obj, !isNew, true)) {
					if(mi.IsReadOnly || mi.IsKey)
						continue;
					object value;
					if(mi.IsDelayed && !session.IsNewObject(obj)) {
						XPDelayedProperty srcDelayed = XPDelayedProperty.GetDelayedPropertyContainer(obj, mi);
						if(mi.ReferenceType == null) {
							if(srcDelayed.IsLoaded && srcDelayed.IsModified) {
								value = srcDelayed.Value;
							} else {
								continue;
							}
						} else {
							if(!srcDelayed.IsLoaded) {
								ciStub.SetMemberValue(objStub, mi.Name, srcDelayed.InternalValue);
								continue;
							}
							value = srcDelayed.Value;
							if(value == null) {
								ciStub.SetMemberValue(objStub, mi.Name, null);
								if(mi.GetModified(obj)) {
									object oldValue = mi.GetOldValue(obj);
									if(oldValue != null)
										oldValue = NestedStubWorksHelper.CreateParentStubObject(session, map, objCache, ciCache, oldValue);
									ciStub.SetMemberOldValue(objStub, mi.Name, oldValue);
								}
								continue;
							}
						}
					} else
						value = mi.GetValue(obj);
					value = PrepareMemberValue(session, map, objCache, ciCache, mi, value);
					ciStub.SetMemberValue(objStub, mi.Name, value);
					if(mi.GetModified(obj)) {
						ciStub.SetMemberOldValue(objStub, mi.Name, PrepareMemberValue(session, map, objCache, ciCache, mi, mi.GetOldValue(obj)));
					}
				}
				if(ci.OptimisticLockFieldInDataLayer != null) {
					objStub.OptimisticLockFieldInDataLayer = ci.OptimisticLockFieldInDataLayer.GetValue(obj);
				}
				return objStub;
			} finally {
				CreateParentStubDictionary.Remove(parentGuid);
			}
		}
		static object PrepareMemberValue(Session session, NestedParentGuidMap map, XPObjectStubCache objCache, XPObjectClassInfoStubCache ciCache, XPMemberInfo mi, object value) {
			if(mi.ReferenceType != null && value != null)
				value = NestedStubWorksHelper.CreateParentStubObject(session, map, objCache, ciCache, value);
			if(mi.Converter != null) {
				value = mi.Converter.ConvertToStorageType(value);
			}
			return value;
		}
#if DEBUGTEST
		public static object GetNestedObject(Session nestedSession, SerializableObjectLayer serializableObjectLayer, object parentObject) {
			if(parentObject == null) return null;
			serializableObjectLayer.ParentSession.GetClassInfo(parentObject);
			nestedSession.GetClassInfo(parentObject.GetType());
			XPObjectStubCache objCache = new XPObjectStubCache();
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(nestedSession);
			XPObjectStub nestedStub = CreateNestedStubObject(serializableObjectLayer.ParentSession, serializableObjectLayer.Map, objCache, ciCache, parentObject);
			return new NestedStubLoader(nestedSession, SerializableObjectLayerClient.GetNestedParentGuidMap(nestedSession), ciCache).GetNestedObject(nestedStub);
		}
		public static object GetParentObject(Session nestedSession, SerializableObjectLayer serializableObjectLayer, object nestedObject) {
			if(nestedObject == null) return null;
			serializableObjectLayer.ParentSession.GetClassInfo(nestedObject.GetType());
			nestedSession.GetClassInfo(nestedObject);
			XPObjectStub parentStub = CreateParentStubObjectJustWithIdentities(nestedSession, SerializableObjectLayerClient.GetNestedParentGuidMap(nestedSession), new XPObjectClassInfoStubCache(serializableObjectLayer.ParentSession), nestedObject);
			return GetParentObject(serializableObjectLayer.ParentSession, serializableObjectLayer.Map, new XPObjectClassInfoStubCache(serializableObjectLayer.ParentSession).GetClassInfo(parentStub.ClassName), parentStub);
		}
#endif
	}
	public abstract class NestedParentGuidMap {
		public static NestedParentMap Extract(NestedUnitOfWork source) {
			return source.Map;
		}
		protected NestedParentGuidMap(Session session) {
			session.AfterDropIdentityMap += AfterDropIdentityMapHandler;
		}
		void AfterDropIdentityMapHandler(object sender, SessionManipulationEventArgs e) {
			Clear();
		}
		public abstract void Add(Guid parentGuid, object nested, bool hasValidKey);
		public abstract Guid GetParent(object nested);
		public abstract object GetNested(Guid parentGuid);
		public abstract void KickOut(object nested);
		public abstract void Clear();
	}
	public class StrongNestedParentGuidMap : NestedParentGuidMap {
		ObjectDictionary<Guid> Parents = new ObjectDictionary<Guid>();
		Dictionary<Guid, object> Nesteds = new Dictionary<Guid, object>();
		public StrongNestedParentGuidMap(Session session)
			: base(session) {
		}
		public override void Add(Guid parentGuid, object nested, bool hasValidKey) {
			Parents.Add(nested, parentGuid);
			Nesteds.Add(parentGuid, nested);
		}
		public override Guid GetParent(object nested) {
			Guid rv;
			if(Parents.TryGetValue(nested, out rv))
				return rv;
			else
				return Guid.Empty;
		}
		public override object GetNested(Guid parentGuid) {
			object rv;
			if(Nesteds.TryGetValue(parentGuid, out rv))
				return rv;
			else
				return null;
		}
		public override void KickOut(object nested) {
			Guid parentGuid;
			if(!Parents.TryGetValue(nested, out parentGuid))
				return;
			Parents.Remove(nested);
			Nesteds.Remove(parentGuid);
		}
		public override void Clear() {
			Parents.Clear();
			Nesteds.Clear();
		}
	}
	public class WeakNestedParentGuidMap : StrongNestedParentGuidMap {
		Dictionary<ObjectRecord, Guid> Parents = new Dictionary<ObjectRecord, Guid>();
		Dictionary<Guid, ObjectRecord> Nesteds = new Dictionary<Guid, ObjectRecord>();
		public WeakNestedParentGuidMap(Session session)
			: base(session) {
		}
		public override void Add(Guid parentGuid, object nested, bool hasValidKey) {
			if(hasValidKey) {
				ObjectRecord nRecord = ObjectRecord.GetObjectRecord(nested);
				Nesteds[parentGuid] = nRecord;
				Parents[nRecord] = parentGuid;
			} else {
				base.Add(parentGuid, nested, hasValidKey);
			}
		}
		public override Guid GetParent(object nested) {
			Guid rv = base.GetParent(nested);
			if(rv != Guid.Empty)
				return rv;
			ObjectRecord nRecord = ObjectRecord.GetObjectRecord(nested);
			Guid parentGuid;
			if(Parents.TryGetValue(nRecord, out parentGuid))
				return parentGuid;
			else
				return Guid.Empty;
		}
		public override object GetNested(Guid parentGuid) {
			object rv = base.GetNested(parentGuid);
			if(rv != null)
				return rv;
			ObjectRecord nRecord;
			if(Nesteds.TryGetValue(parentGuid, out nRecord))
				return nRecord.Object;
			else
				return null;
		}
		public override void KickOut(object nested) {
			base.KickOut(nested);
			ObjectRecord nRecord = ObjectRecord.GetObjectRecord(nested);
			Guid pRecord;
			if(!Parents.TryGetValue(nRecord, out pRecord))
				return;
			Parents.Remove(nRecord);
			Nesteds.Remove(pRecord);
		}
		public override void Clear() {
			base.Clear();
			Parents.Clear();
			Nesteds.Clear();
		}
	}
	public class NestedStubLoader {
		readonly ObjectSet forced = new ObjectSet();
		readonly Queue<ObjectPair> toProcess = new Queue<ObjectPair>();
		readonly NestedParentGuidMap Map;
		struct ObjectPair {
			public readonly XPObjectStub Source;
			public readonly object Destination;
			public readonly OptimisticLockingReadMergeBehavior LoadMerge;
			public ObjectPair(XPObjectStub source, object destination, OptimisticLockingReadMergeBehavior loadMerge) {
				Source = source;
				Destination = destination;
				LoadMerge = loadMerge;
			}
		}
		public readonly Session Owner;
		public readonly Session OwnerParent;
		readonly XPObjectClassInfoStubCache ciCache;
		public NestedStubLoader(Session owner, NestedParentGuidMap map, XPObjectClassInfoStubCache ciCache) {
			this.Owner = owner;
			this.Map = map;
			this.ciCache = ciCache ?? new XPObjectClassInfoStubCache(owner);
		}
		void CloneObjects(out IList toFireLoaded) {
			toFireLoaded = new List<object>();
			Dictionary<Guid, bool> processedDict = new Dictionary<Guid, bool>();
			List<ObjectPair> processed = new List<ObjectPair>();
			List<object> temp = new List<object>();
			do {
				ObjectPair s = toProcess.Dequeue();
				toFireLoaded.Add(s.Destination);
				XPObjectStub source = s.Source;
				XPClassInfo ci = ciCache.GetClassInfo(source.ClassName);
				XPClassInfoStub ciStub = ciCache.GetStub(ci);
				foreach(XPMemberInfo mi in ci.ObjectProperties) {
					if(mi.IsReadOnly)
						continue;
					if(!ciStub.IsMemberChanged(source, mi.Name))
						continue;
					object value = ciStub.GetMemberValue(source, mi.Name);
					if(value != null) {
						XPObjectStub objStub = value as XPObjectStub;
						if(objStub != null)
							temp.Add(GetNestedObjectCore(objStub, false));
					}
				}
				if(processedDict.ContainsKey(s.Source.Guid)) continue;
				processed.Add(s);
				processedDict.Add(s.Source.Guid, true);
			} while(toProcess.Count > 0);
			for(int i = 0; i < processed.Count; i++) {
				ObjectPair s = (ObjectPair)processed[i];
				Owner.TriggerObjectLoading(s.Destination);
				CloneData(s.Source, s.Destination, s.LoadMerge);
			}
			GC.KeepAlive(temp);
		}
		void CloneData(XPObjectStub source, object destination, OptimisticLockingReadMergeBehavior loadMerge) {
			XPClassInfo ci = ciCache.GetClassInfo(source.ClassName);
			XPClassInfoStub ciStub = ciCache.GetStub(ci);
			foreach(XPMemberInfo mi in ci.PersistentProperties) {
				if(mi.IsReadOnly)
					continue;
				object value;
				if(mi.IsDelayed && !source.IsNew) {
					if (mi.ReferenceType == null) {
						if (!ObjectCollectionLoader.AcceptLoadPropertyAndResetModified(Owner.TrackPropertiesModifications, loadMerge, destination, ci, mi, ReturnArgument, null))
							continue;
						XPDelayedProperty.Init(Owner, destination, mi, null);
						continue;
					} else {
						bool skipInit = false;
						object delayedValue = ciStub.GetMemberValue(source, mi.Name);
						if (delayedValue != null) {
							XPObjectStub objectValue = delayedValue as XPObjectStub;
							if (objectValue != null){
								if (objectValue.IsNew) {
									skipInit = true;
								} else {
									delayedValue = objectValue.Key;
								}
							}
						}
						if (!skipInit) {
							if (!ObjectCollectionLoader.AcceptLoadPropertyAndResetModified(Owner.TrackPropertiesModifications, loadMerge, destination, ci, mi, ReturnArgument, delayedValue))
								continue;
							XPDelayedProperty.Init(Owner, destination, mi, delayedValue);
							continue;
						}
					}
				}
				value = ciStub.GetMemberValue(source, mi.Name);
				if(mi.ReferenceType != null && value != null) {
					value = Map.GetNested(((XPObjectStub)value).Guid);
					if(value == null)
						throw new InvalidOperationException(Res.GetString(Res.NestedSession_NotCloneable, ci.FullName, mi.Name));	
				}
				if(mi.Converter != null) {
					value = mi.Converter.ConvertFromStorageType(value);
				}
				if (!ObjectCollectionLoader.AcceptLoadPropertyAndResetModified(Owner.TrackPropertiesModifications, loadMerge, destination, ci, mi, ReturnArgument, value))
					continue;
				mi.SetValue(destination, value);
			}
		}
		static Func<object, object> ReturnArgument = arg => arg;
		object CreateNestedObject(XPObjectStub pObject, bool forceLoad) {
			XPClassInfo ci = ciCache.GetClassInfo(pObject.ClassName);
			object nObject;
			bool foundById = false;
			if(pObject.IsNew) {
				nObject = ci.CreateObject(Owner);
			} else {
				object key = pObject.Key;
				nObject = Owner.GetLoadedObjectByKey(ci, key);
				if(nObject == null) {
					nObject = ci.CreateObject(Owner);
					SessionIdentityMap.RegisterObject(Owner, nObject, ci.KeyProperty.ExpandId(key)); 
				} else {
					Map.KickOut(nObject);
					foundById = true;
				}
			}
			Map.Add(pObject.Guid, nObject, !pObject.IsNew);
			OptimisticLockingReadMergeBehavior loadMerge = OptimisticLockingReadMergeBehavior.Default;
			if (!foundById || ObjectCollectionLoader.NeedReload(Owner, ci, forceLoad, out loadMerge, () => { return IsObjectVersionChanged(nObject, pObject); })) {
				toProcess.Enqueue(new ObjectPair(pObject, nObject, loadMerge));
			}
			return nObject;
		}
		object GetNestedObjectCore(XPObjectStub parentObject, bool forceLoad) {
			object rv = Map.GetNested(parentObject.Guid);
			if (rv != null) {
				IXPInvalidateableObject invalidatedObject = rv as IXPInvalidateableObject;
				if (invalidatedObject == null || !invalidatedObject.IsInvalidated) {
					OptimisticLockingReadMergeBehavior loadMerge = OptimisticLockingReadMergeBehavior.Default;
					if (ObjectCollectionLoader.NeedReload(Owner, ciCache.GetClassInfo(parentObject.ClassName), forceLoad, out loadMerge, () => { return IsObjectVersionChanged(rv, parentObject); }) && !forced.Contains(rv)) {
						toProcess.Enqueue(new ObjectPair(parentObject, rv, loadMerge));
						forced.Add(rv);
					}
					return rv;
				} else {
					Map.KickOut(rv);
				}
			}
			rv = CreateNestedObject(parentObject, forceLoad);
			return rv;
		}
		public bool IsObjectVersionChanged(object nestedObject, XPObjectStub parentObject) {
			XPClassInfo ci = ciCache.GetClassInfo(parentObject.ClassName);
			XPClassInfoStub ciStub = ciCache.GetStub(ci);
			if(ci.OptimisticLockField == null) return false;
			return ((int?)ci.OptimisticLockField.GetValue(nestedObject)) != ((int?)ciStub.GetMemberValue(parentObject, ciStub.OptimisticLockFieldName)) &&
				((int?)ci.OptimisticLockFieldInDataLayer.GetValue(nestedObject)) != ((int?)parentObject.OptimisticLockFieldInDataLayer);
		}
		public ICollection[] GetNestedObjects(XPObjectStubCollection[] parentObjects, bool[] force) {
			ICollection[] result;
			IList objectsToFireLoaded;
			SessionStateStack.Enter(Owner, SessionState.GetObjectsNonReenterant);
			try {
				result = GetNestedObjectsCore(parentObjects, force, out objectsToFireLoaded);
			} finally {
				SessionStateStack.Leave(Owner, SessionState.GetObjectsNonReenterant);
			}
			if(objectsToFireLoaded != null)
				Owner.TriggerObjectsLoaded(objectsToFireLoaded);
			return result;
		}
		public async Task<ICollection[]> GetNestedObjectsAsync(XPObjectStubCollection[] parentObjects, bool[] force, CancellationToken cancellationToken = default(CancellationToken)) {
			ICollection[] result;
			IList objectsToFireLoaded;
			int asyncOperationId = SessionStateStack.GetNewAsyncOperationId();
			await SessionStateStack.EnterAsync(Owner, SessionState.GetObjectsNonReenterant, asyncOperationId, cancellationToken);
			try {
				result = GetNestedObjectsCore(parentObjects, force, out objectsToFireLoaded);
			} finally {
				SessionStateStack.Leave(Owner, SessionState.GetObjectsNonReenterant, asyncOperationId);
			}
			if(objectsToFireLoaded != null)
				Owner.TriggerObjectsLoaded(objectsToFireLoaded);
			return result;
		}
		public ICollection[] GetNestedObjectsCore(XPObjectStubCollection[] parentObjects, bool[] force, out IList objectsToFireLoaded) {
			objectsToFireLoaded = null;
			ICollection[] result = new ICollection[parentObjects.Length];
			for(int i = 0; i < parentObjects.Length; i++) {
				bool forceLoad = force == null ? false : force[i];
				List<object> list = new List<object>(parentObjects[i].Count);
				foreach(XPObjectStub parentObj in parentObjects[i])
					list.Add(parentObj == null ? null : GetNestedObjectCore(parentObj, forceLoad));
				result[i] = list;
			}
			if(toProcess.Count > 0) {
				CloneObjects(out objectsToFireLoaded);
			}
			if(objectsToFireLoaded != null) {
				foreach(object obj in objectsToFireLoaded) {
					XPClassInfo ci = Owner.GetClassInfo(obj);
					XPMemberInfo olf = ci.OptimisticLockField;
					if(olf != null) {
						ci.OptimisticLockFieldInDataLayer.SetValue(obj, olf.GetValue(obj));
					}
				}
			}
			return result;
		}
		public object GetNestedObject(XPObjectStub parentObject) {
			ICollection[] res = GetNestedObjects(new XPObjectStubCollection[] { new XPObjectStubCollection(new XPObjectStub[] { parentObject }) }, null);
			IEnumerator en = res[0].GetEnumerator();
			en.MoveNext();
			return en.Current;
		}
	}
	[Serializable]
	public class XPStubOperandValue : OperandValue {
		bool isConstant;
		XPObjectStub objectValue;
		public XPStubOperandValue():base() { }
		public XPStubOperandValue(XPObjectStub value, bool isConstant)
			: base() {
			this.objectValue = value;
			this.isConstant = isConstant;
		}
		protected override object GetXmlValue() {
			return null;
		}
		public bool IsConstant {
			get { return isConstant; }
			set { isConstant = value; }
		}
		public XPObjectStub ObjectValue {
			get { return objectValue; }
			set { objectValue = value; }
		}
		public override object Value {
			get { return objectValue; }
			set { objectValue = (XPObjectStub)value; }
		}
	}
	public class XPObjectStubCriteriaGenerator : ClientCriteriaVisitorBase {
		protected readonly Session session;
		protected readonly NestedParentGuidMap npgMap;
		protected readonly NestedGuidParentMap ngpMap;
		protected readonly XPObjectClassInfoStubCache ciCache;
		protected XPObjectStubCriteriaGenerator(Session session, NestedParentGuidMap map, XPObjectClassInfoStubCache ciCache) {
			this.session = session;
			this.npgMap = map;
			this.ciCache = ciCache;
		}
		protected XPObjectStubCriteriaGenerator(Session session, NestedGuidParentMap map, XPObjectClassInfoStubCache ciCache) {
			this.session = session;
			this.ngpMap = map;
			this.ciCache = ciCache;
		}
		protected override CriteriaOperator Visit(InOperator theOperator) {
			var result = base.Visit(theOperator);
			var inOperator = result as InOperator;
			return ReferenceEquals(inOperator, null) || inOperator.GetType() == typeof(InOperator) ? result : new InOperator(inOperator.LeftOperand, inOperator.Operands);
		}
		protected override CriteriaOperator Visit(OperandValue theOperand) {
			if(theOperand.Value is Enum) {
				object resultValue = null;
				switch(((IConvertible)theOperand.Value).GetTypeCode()) {
					case TypeCode.Byte:
						resultValue = (Byte)theOperand.Value;
						break;
					case TypeCode.Int16:
						resultValue = (Int16)theOperand.Value;
						break;
					case TypeCode.Int32:
						resultValue = (Int32)theOperand.Value;
						break;
					case TypeCode.Int64:
						resultValue = (Int16)theOperand.Value;
						break;
					case TypeCode.SByte:
						resultValue = (SByte)theOperand.Value;
						break;
					case TypeCode.UInt16:
						resultValue = (UInt16)theOperand.Value;
						break;
					case TypeCode.UInt32:
						resultValue = (UInt32)theOperand.Value;
						break;
					case TypeCode.UInt64:
						resultValue = (UInt64)theOperand.Value;
						break;
				}
				if(resultValue != null) {
					return theOperand is ConstantValue ? new ConstantValue(resultValue) : new OperandValue(resultValue);
				}
			}
			if(npgMap != null) {
				XPClassInfo ci = session.Dictionary.QueryClassInfo(theOperand.Value);
				if(ci == null) {
					Type operandValueType = theOperand.GetType();
					if(operandValueType != typeof(OperandValue) && operandValueType != typeof(ConstantValue)) {
						return new OperandValue(theOperand.Value);
					}
					return theOperand;
				} else {
					XPObjectStub resultValue = NestedStubWorksHelper.CreateParentStubObjectJustWithIdentities(session, npgMap, ciCache, theOperand.Value);
					if(theOperand is ConstantValue)
						return new XPStubOperandValue(resultValue, true);
					return new XPStubOperandValue(resultValue, false);
				}
			}
			if(ngpMap != null) {
				XPObjectStub objStub = theOperand.Value as XPObjectStub;
				if(objStub == null) {
					Type operandValueType = theOperand.GetType();
					if(operandValueType != typeof(OperandValue) && operandValueType != typeof(ConstantValue)) {
						return new OperandValue(theOperand.Value);
					}
					return theOperand;
				} else {
					object resultValue = NestedStubWorksHelper.GetParentObject(session, ngpMap, ciCache.GetClassInfo(objStub.ClassName), objStub);
					if(theOperand is XPStubOperandValue) {
						return ((XPStubOperandValue)theOperand).IsConstant ? new ConstantValue(resultValue) : new OperandValue(resultValue);
					}
					return theOperand is ConstantValue ? new ConstantValue(resultValue) : new OperandValue(resultValue);
				}
			}
			throw new InvalidOperationException(Res.GetString(Res.SerOLHelpers_NestedParentMapIsNull));
		}
		public static SortingCollection PreprocessSortingCollection(Session session, NestedParentGuidMap map, XPObjectClassInfoStubCache ciCache, SortingCollection sortingCollection) {
			if(sortingCollection == null) {
				return null;
			}
			bool modified = false;
			SortingCollection resultCollection = new SortingCollection();
			foreach(SortProperty sortProperty in sortingCollection) {
				CriteriaOperator resultProperty = GetStubCriteria(session, map, ciCache, sortProperty.Property);
				if(!ReferenceEquals(resultProperty, sortProperty.Property)) {
					modified = true;
					resultCollection.Add(new SortProperty(resultProperty, sortProperty.Direction));
				} else {
					resultCollection.Add(sortProperty);
				}
			}
			return modified ? resultCollection : sortingCollection;
		}
		public static CriteriaOperator GetStubCriteria(Session session, NestedParentGuidMap map, XPObjectClassInfoStubCache ciCache, CriteriaOperator op) {
			return new XPObjectStubCriteriaGenerator(session, map, ciCache).Process(op);
		}
		public static CriteriaOperator GetSessionCriteria(Session session, NestedGuidParentMap map, XPObjectClassInfoStubCache ciCache, CriteriaOperator op) {
			return new XPObjectStubCriteriaGenerator(session, map, ciCache).Process(op);
		}
	}
	public class EvaluatorContextDescriptorXpoStub : EvaluatorContextDescriptor {
		XPObjectClassInfoStubCache ciCache;
		public EvaluatorContextDescriptorXpoStub(IXPDictionaryProvider dictionaryProvider) {
			ciCache = dictionaryProvider as XPObjectClassInfoStubCache;
			if(ciCache == null) ciCache = new XPObjectClassInfoStubCache(dictionaryProvider);
		}
		public override object GetPropertyValue(object source, EvaluatorProperty propertyPath) {
			if(source == null)
				return null;
			XPObjectStub stubObject = source as XPObjectStub;
			if(stubObject == null)
				return null;
			XPClassInfoStub ciStub = ciCache.GetStub(stubObject.ClassName);
			int memberIndex = ciStub.GetMemberIndex(propertyPath.PropertyPath, false);
			if(memberIndex >= 0) {
				return stubObject.Data[memberIndex];
			} else if(ciStub.KeyFieldName == propertyPath.PropertyPath) {
				return stubObject.Key;
			}
			if(EvaluatorProperty.GetIsThisProperty(propertyPath.PropertyPath))
				return source;
			string[] path = propertyPath.PropertyPathTokenized;
			EvaluatorProperty current = propertyPath.SubProperty;
			for(int i = 1; i < path.Length; ++i) {
				string subPath = string.Join(".", path, 0, i);
				memberIndex = ciStub.GetMemberIndex(subPath, false);
				if(memberIndex >= 0) {
					object value = stubObject.Data[memberIndex];
					if(value == null) return null;
					stubObject = value as XPObjectStub;
					if(stubObject != null) {
						return XPObjectStub.GetEvaluatorContextDescriptor(ciCache).GetPropertyValue(stubObject, current);
					}
				}
				current = current.SubProperty;
			}
			throw new InvalidPropertyPathException(Res.GetString(Res.MetaData_IncorrectPath, stubObject.ClassName, propertyPath.PropertyPath));
		}
		public override EvaluatorContext GetNestedContext(object source, string propertyPath) {
			if(source == null)
				return null;
			XPObjectStub stubObject = source as XPObjectStub;
			if(stubObject == null)
				return null;
			XPClassInfoStub ciStub = ciCache.GetStub(stubObject.ClassName);
			int memberIndex = ciStub.GetMemberIndex(propertyPath, false);
			if(memberIndex >= 0) {
				XPObjectStub nestedStubObject = stubObject.Data[memberIndex] as XPObjectStub;
				if(nestedStubObject == null) 
					return null;
				EvaluatorContextDescriptor nestedDescriptor = XPObjectStub.GetEvaluatorContextDescriptor(ciCache);
				return new EvaluatorContext(nestedDescriptor, nestedStubObject);
			}
			throw new InvalidPropertyPathException(Res.GetString(Res.MetaData_IncorrectPath, ciStub.ClassName, propertyPath));
		}
		public override IEnumerable GetCollectionContexts(object source, string collectionName) {
			throw new NotSupportedException();
		}
		public override IEnumerable GetQueryContexts(object source, string queryTypeName, CriteriaOperator condition, int top) {
			throw new NotSupportedException();
		}
	}
}
