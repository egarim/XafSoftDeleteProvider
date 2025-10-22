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
using System.Linq;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using DevExpress.Data.Filtering;
using DevExpress.Data.Filtering.Helpers;
using System.Collections.Generic;
using System.Reflection;
using System.ComponentModel;
using System.Collections.Specialized;
using DevExpress.Xpo.Metadata;
using System.Threading.Tasks;
using System.Threading;
namespace DevExpress.Xpo.Helpers {
	public static class AssociatedCollectionCriteriaHelper {
		public struct Result {
			public readonly XPClassInfo CollectionClassInfo;
			public readonly CriteriaOperator Criteria;
			public Result(XPClassInfo collectionClassInfo, CriteriaOperator criteria) {
				this.CollectionClassInfo = collectionClassInfo;
				this.Criteria = criteria;
			}
		}
		public static Result ResolveDataForCollection(object instance, XPMemberInfo member) {
			if(member.IsAssociationList) {
				XPMemberInfo relatedMember = member.GetAssociatedMember();
				if(member.IsManyToMany) {
					return new Result(relatedMember.Owner, new OperandProperty(relatedMember.Name)[new OperandProperty("This") == new OperandValue(instance)]);
				} else {
					return new Result(relatedMember.Owner, new OperandProperty(relatedMember.Name) == new OperandValue(instance));
				}
			} else if(member.IsManyToManyAlias) {
				ManyToManyAliasAttribute alias = (ManyToManyAliasAttribute)member.GetAttributeInfo(typeof(ManyToManyAliasAttribute));
				XPMemberInfo collectionMember = member.Owner.GetMember(alias.OneToManyCollectionName);
				XPMemberInfo collectionMemberAssoc = collectionMember.GetAssociatedMember();
				CriteriaOperator collectionCondition = new OperandProperty(collectionMemberAssoc.Name) == new OperandValue(instance);
				XPMemberInfo referenceMember = collectionMemberAssoc.Owner.GetMember(alias.ReferenceInTheIntermediateTableName);
				if(referenceMember.IsAssociation) {
					return new Result(referenceMember.ReferenceType, new OperandProperty(referenceMember.GetAssociatedMember().Name)[collectionCondition]);
				} else {
					return new Result(referenceMember.ReferenceType, new JoinOperand(referenceMember.Owner.FullName, new OperandProperty("^.This") == new OperandProperty(referenceMember.Name) & collectionCondition));
				}
			} else {
				throw new ArgumentException(Res.GetString(Res.Helpers_ResolveDataForCollection));
			}
		}
	}
	public class MergeXmlSerializationHelper {
		class ObjListHelper: ArrayList {
			readonly XPClassInfo ClassInfo;
			public ObjListHelper(XPClassInfo ci, ICollection source)
				: base(source) {
				this.ClassInfo = ci;
			}
			public int FindObjectIndexByKey(object key) {
				for(int i = 0; i < this.Count; ++i) {
					object objKey = ClassInfo.KeyProperty.GetValue(this[i]);
					if(object.Equals(key, objKey))
						return i;
				}
				return -1;
			}
		}
		public static object MergeChanges(UnitOfWork mergeDestination, XPClassInfo classInfo, object originalObject, object changedObject) {
			if(ReferenceEquals(originalObject, null) && ReferenceEquals(changedObject, null)) {
				return null;
			} else if(ReferenceEquals(changedObject, null)) {
				object mergeObject = mergeDestination.GetObjectByKey(classInfo, classInfo.KeyProperty.GetValue(changedObject));
				if(ReferenceEquals(mergeObject, null))
					throw new DB.Exceptions.LockingException();
				mergeDestination.Delete(mergeObject);
				return null;
			} else if(ReferenceEquals(originalObject, null)) {
				object mergeObject = classInfo.CreateNewObject(mergeDestination);
				return UpdateObject(mergeDestination, classInfo, mergeObject, null, changedObject);
			} else {
				object mergeObject = mergeDestination.GetObjectByKey(classInfo, classInfo.KeyProperty.GetValue(changedObject));
				object _mergeObject = mergeDestination.GetObjectByKey(classInfo, classInfo.KeyProperty.GetValue(originalObject));
				if(!ReferenceEquals(mergeObject, _mergeObject))
					throw new InvalidOperationException(Res.GetString(Res.Helpers_DifferentObjectsKeys));
				if(ReferenceEquals(mergeObject, null))
					throw new DB.Exceptions.LockingException();
				return UpdateObject(mergeDestination, classInfo, mergeObject, originalObject, changedObject);
			}
		}
		static object UpdateObject(UnitOfWork mergeDestination, XPClassInfo ci, object mergeObject, object originalObject, object changedObject) {
			foreach(XPMemberInfo mi in ci.CollectionProperties) {
				XPBaseCollection originalCollection = (XPBaseCollection)mi.GetValue(originalObject);
				if(originalCollection == null)
					continue;
				XPBaseCollection changedCollection = (XPBaseCollection)mi.GetValue(changedObject);
				if(originalCollection.LoadingEnabled && changedCollection.LoadingEnabled) {
					;
				} else {
					MergeChanges(mergeDestination, mi.CollectionElementType, originalCollection, changedCollection);
				}
			}
			foreach(XPMemberInfo mi in ci.Members) {
				if((mi.IsPersistent || mi.IsAliased) && !mi.IsReadOnly && !mi.IsFetchOnly) {
					object originalValue = mi.GetValue(originalObject);
					object changedValue = mi.GetValue(changedObject);
					if(mi.ReferenceType != null) {
						object newReferenceValue = MergeChanges(mergeDestination, mi.ReferenceType, originalValue, changedValue);
						if(!ReferenceEquals(newReferenceValue, mi.GetValue(mergeObject))) {
							mi.SetValue(mergeObject, newReferenceValue);
							mergeDestination.Save(mergeObject);
						}
					} else {
						if(!Equals(originalValue, changedValue)) {
							if(!Equals(originalValue, mi.GetValue(mergeObject)) && !Equals(changedValue, mi.GetValue(mergeObject)))
								throw new DB.Exceptions.LockingException();
							mi.SetValue(mergeObject, changedValue);
							mergeDestination.Save(mergeObject);
						}
					}
				}
			}
			return mergeObject;
		}
		public static void MergeChanges(UnitOfWork mergeDestination, XPClassInfo classInfo, ICollection originalImages, ICollection changedImages) {
			ObjListHelper originalList = new ObjListHelper(classInfo, originalImages);
			ObjListHelper changedList = new ObjListHelper(classInfo, changedImages);
			while(originalList.Count > 0) {
				object originalObject = originalList[0];
				int posInChanged = changedList.FindObjectIndexByKey(classInfo.KeyProperty.GetValue(originalObject));
				if(posInChanged >= 0) {
					object changedObject = changedList[posInChanged];
					MergeChanges(mergeDestination, classInfo, originalObject, changedObject);
					originalList.RemoveAt(0);
					changedList.RemoveAt(posInChanged);
				} else {
					MergeChanges(mergeDestination, classInfo, originalObject, null);
					originalList.RemoveAt(0);
				}
			}
			foreach(object added in changedList) {
				MergeChanges(mergeDestination, classInfo, null, added);
			}
		}
		public static object MergeChanges(UnitOfWork mergeDestination, Type objType, object originalObject, object changedObject) {
			return MergeChanges(mergeDestination, mergeDestination.GetClassInfo(objType), originalObject, changedObject);
		}
		public static void MergeChanges(UnitOfWork mergeDestination, Type objType, ICollection originalImages, ICollection changedImages) {
			MergeChanges(mergeDestination, mergeDestination.GetClassInfo(objType), originalImages, changedImages);
		}
	}
	public sealed class ClonerHelper {
		readonly IDictionary Mapping = new ObjectDictionary<object>();
		readonly Session Src;
		readonly Session Dst;
		ClonerHelper(Session src, Session dst) {
			if(!ReferenceEquals(src.Dictionary, dst.Dictionary))
				throw new InvalidOperationException(Res.GetString(Res.Helpers_SameDictionaryExpected));
			this.Src = src;
			this.Dst = dst;
		}
		object Clone(object proto) {
			object clone = Mapping[proto];
			if(clone != null)
				return clone;
			XPClassInfo ci = Src.GetClassInfo(proto);
			clone = ci.CreateNewObject(Dst);
			Mapping.Add(proto, clone);
			foreach(XPMemberInfo mi in ci.PersistentProperties) {
				object value = mi.GetValue(proto);
				if(value != null) {
					if(mi.ReferenceType != null) {
						value = Clone(value);
					} else {
						ICloneable cloneableValue = value as ICloneable;
						if(cloneableValue != null)
							value = cloneableValue.Clone();
					}
				}
				mi.SetValue(clone, value);
			}
			foreach(XPMemberInfo mi in ci.AssociationListProperties) {
				IList protoCollection = (IList)mi.GetValue(proto);
				IList cloneCollection = (IList)mi.GetValue(clone);
				foreach(object obj in protoCollection) {
					cloneCollection.Add(Clone(obj));
				}
			}
			return clone;
		}
		public static object Clone(Session sourceSession, object sourceObject, Session destinationSession) {
			return new ClonerHelper(sourceSession, destinationSession).Clone(sourceObject);
		}
		public static object Clone(IXPSimpleObject sourceObject, Session destinationSession) {
			return Clone(sourceObject.Session, sourceObject, destinationSession);
		}
		public static IDictionary Clone(Session sourceSession, Session destinationSession, params ICollection[] prototypes) {
			ClonerHelper helper = new ClonerHelper(sourceSession, destinationSession);
			foreach(ICollection prots in prototypes) {
				foreach(object obj in prots) {
					helper.Clone(obj);
				}
			}
			return helper.Mapping;
		}
		public static IDictionary Clone(Session sourceSession, Session destinationSession, params XPClassInfo[] prototypes) {
			ClonerHelper helper = new ClonerHelper(sourceSession, destinationSession);
			foreach(XPClassInfo prototype in prototypes) {
				foreach(object obj in new XPCollection(sourceSession, prototype)) {
					helper.Clone(obj);
				}
			}
			return helper.Mapping;
		}
		public static IDictionary Clone(Session sourceSession, Session destinationSession, params Type[] prototypes) {
			ClonerHelper helper = new ClonerHelper(sourceSession, destinationSession);
			foreach(Type prototype in prototypes) {
				foreach(object obj in new XPCollection(sourceSession, prototype)) {
					helper.Clone(obj);
				}
			}
			return helper.Mapping;
		}
	}
	public class CollectionClonerHelper {
		CollectionClonerHelper() { }
		public static XPBaseCollection Clone(XPBaseCollection source, Session destination) {
			XPClassInfo srcType = source.GetObjectClassInfo();
			XPClassInfo dstType = destination.GetClassInfo(srcType.AssemblyName, srcType.FullName);
			XPBaseCollection result = new XPCollection(destination, dstType, false);
			foreach(object obj in source) {
				result.BaseAdd(Clone(source.Session, obj, destination));
			}
			return result;
		}
		public static object Clone(Session srcSession, object obj, Session dstSession) {
			if(obj == null)
				return null;
			if(srcSession.IsNewObject(obj) || srcSession.IsObjectToSave(obj, true) || srcSession.IsObjectToDelete(obj, true))
				throw new InvalidOperationException(Res.GetString(Res.Helpers_CloningObjectModified));
			XPClassInfo srcClassInfo = srcSession.GetClassInfo(obj);
			XPClassInfo dstClassInfo = dstSession.GetClassInfo(srcClassInfo.AssemblyName, srcClassInfo.FullName);
			object keyValue = srcSession.GetKeyValue(obj);
			object result = dstSession.GetLoadedObjectByKey(dstClassInfo, keyValue);
			if(result != null)
				return result;
			result = dstClassInfo.CreateObject(dstSession);
			SessionIdentityMap.RegisterObject(dstSession, result, keyValue); 
			IXPObject loadableObject = result as IXPObject;
			if(loadableObject != null)
				loadableObject.OnLoading();
			foreach(XPMemberInfo srcMI in srcClassInfo.PersistentProperties) {
				object value = srcMI.GetValue(obj);
				if(srcMI.ReferenceType != null) {
					value = Clone(srcSession, value, dstSession);
				} else {
					ICloneable cloneableValue = value as ICloneable;
					if(cloneableValue != null) {
						value = cloneableValue.Clone();
					}
				}
				XPMemberInfo dstMI = dstClassInfo.GetMember(srcMI.Name);
				dstMI.SetValue(result, value);
			}
			if(loadableObject != null)
				loadableObject.OnLoaded();
			return result;
		}
	}
	class ListHelper {
		static public List<object> FromCollection(ICollection collection) {
			List<object> list = new List<object>(collection.Count);
			foreach(object obj in collection)
				list.Add(obj);
			return list;
		}
		static public List<T> FromCollection<T>(ICollection collection) {
			List<T> list = new List<T>(collection.Count);
			foreach(T obj in collection)
				list.Add(obj);
			return list;
		}
	}
	class GetObjectsHelper {
		public static ICollection GetObjectsFromData(Session session, XPClassInfo classInfo, List<XPMemberInfo> memberInfos, LoadDataMemberOrderItem[] membersOrder, SelectedData sprocResultData, Dictionary<XPMemberInfo, int> referenceIndexDict) {
			List<object>[] referenceKeyList;
			List<object> primaryObjects;
			ICollection result = GetObjectsFromDataCore(session, classInfo, memberInfos, membersOrder, sprocResultData, referenceIndexDict, out referenceKeyList, out primaryObjects);
			if(primaryObjects != null && primaryObjects.Count > 0) {
				ObjectsByKeyQuery[] queries = BeginLoadReferenceObjects(referenceIndexDict, referenceKeyList);
				ICollection[] referenceObjects = session.GetObjectsByKey(queries, false);
				EndLoadReferenceObjects(referenceIndexDict, referenceObjects, primaryObjects);
			}
			session.TriggerObjectsLoaded(result);
			return result;
		}
		public static async Task<ICollection> GetObjectsFromDataAsync(Session session, XPClassInfo classInfo, List<XPMemberInfo> memberInfos, LoadDataMemberOrderItem[] membersOrder, SelectedData sprocResultData, Dictionary<XPMemberInfo, int> referenceIndexDict, CancellationToken cancellationToken) {
			List<object>[] referenceKeyList;
			List<object> primaryObjects;
			ICollection result = GetObjectsFromDataCore(session, classInfo, memberInfos, membersOrder, sprocResultData, referenceIndexDict, out referenceKeyList, out primaryObjects);
			if(primaryObjects != null && primaryObjects.Count > 0) {
				ObjectsByKeyQuery[] queries = BeginLoadReferenceObjects(referenceIndexDict, referenceKeyList);
				ICollection[] referenceObjects = await session.GetObjectsByKeyAsync(queries, false, cancellationToken);
				EndLoadReferenceObjects(referenceIndexDict, referenceObjects, primaryObjects);
			}
			session.TriggerObjectsLoaded(result);
			return result;
		}
		static ICollection GetObjectsFromDataCore(Session session, XPClassInfo classInfo, List<XPMemberInfo> memberInfos, LoadDataMemberOrderItem[] membersOrder, SelectedData sprocResultData, Dictionary<XPMemberInfo, int> referenceIndexDict, out List<object>[] referenceKeyList, out List<object> primaryObjects) {
			referenceKeyList = null;
			primaryObjects = null;
			if(classInfo.IsPersistent)
				throw new ArgumentException(Res.GetString(Res.MetaData_PersistentReferenceFound, classInfo.FullName), nameof(classInfo));
			if(sprocResultData == null || sprocResultData.ResultSet == null || sprocResultData.ResultSet.Length == 0)
				return Array.Empty<object>();
			SelectStatementResult sprocResult = sprocResultData.ResultSet[0];
			if(sprocResult == null || sprocResult.Rows == null || sprocResult.Rows.Length == 0)
				return Array.Empty<object>();
			referenceKeyList = referenceIndexDict == null ? null : new List<object>[referenceIndexDict.Count];
			primaryObjects = referenceIndexDict == null ? null : new List<object>();
			SelectStatementResultRow[] rows = sprocResult.Rows;
			object[] result = new object[rows.Length];
			if(referenceIndexDict != null) {
				for(int i = 0; i < referenceKeyList.Length; i++) {
					referenceKeyList[i] = new List<object>();
				}
			}
			for(int r = 0; r < rows.Length; r++) {
				SelectStatementResultRow row = rows[r];
				object theObject = classInfo.CreateNewObject(session);
				if(memberInfos != null) {
					bool allNulls = true;
					object[] keys = referenceIndexDict == null ? null : new object[referenceIndexDict.Count];
					if(membersOrder == null) {
						if(memberInfos.Count != row.Values.Length)
							throw new InvalidOperationException(Res.GetString(Res.DirectSQL_WrongColumnCount));
					}
					int minCount = memberInfos.Count;
					for(int m = 0; m < minCount; m++) {
						XPMemberInfo mi = memberInfos[m];
						object value = row.Values[membersOrder == null ? m : membersOrder[m].IndexInResultSet];
						if(mi.ReferenceType != null) {
							if(referenceIndexDict == null)
								throw new InvalidOperationException(Res.GetString(Res.Session_InternalXPOError));
							if(value == null || value is DBNull)
								continue;
							allNulls = false;
							keys[referenceIndexDict[mi]] = value;
							continue;
						}
						if(value is DBNull)
							value = null;
						if (mi.Converter == null) {
							value = ChangeDbTypeToMemberType(value, mi.MemberType);
						}
						mi.SetValue(theObject, mi.Converter == null ? value : mi.Converter.ConvertFromStorageType(value));
					}
					if(!allNulls) {
						primaryObjects.Add(theObject);
						for(int i = 0; i < referenceKeyList.Length; i++) {
							referenceKeyList[i].Add(keys[i]);
						}
					}
				}
				result[r] = theObject;
			}
			return result;
		}
		static ObjectsByKeyQuery[] BeginLoadReferenceObjects(Dictionary<XPMemberInfo, int> referenceIndexDict, List<object>[] referenceKeyList) {
			ObjectsByKeyQuery[] queries = new ObjectsByKeyQuery[referenceIndexDict.Count];
			int queryIndex = 0;
			foreach(KeyValuePair<XPMemberInfo, int> referenceIndex in referenceIndexDict) {
				queries[queryIndex++] = new ObjectsByKeyQuery(referenceIndex.Key.ReferenceType, referenceKeyList[referenceIndex.Value]);
			}
			return queries;
		}
		static void EndLoadReferenceObjects(Dictionary<XPMemberInfo, int> referenceIndexDict, ICollection[] referenceObjects, List<object> primaryObjects) {
			foreach(KeyValuePair<XPMemberInfo, int> referenceIndexPair in referenceIndexDict) {
				int primaryObjectIndex = 0;
				foreach(object referenceObject in referenceObjects[referenceIndexPair.Value]) {
					referenceIndexPair.Key.SetValue(primaryObjects[primaryObjectIndex++], referenceObject);
				}
			}
		}
		public static object ChangeDbTypeToMemberType(object value, Type memberType) {
			if (value == null || value == DBNull.Value) {
				return value;
			}
			Type valueType = value.GetType();
			if (valueType == memberType) {
				return value;
			}
			if (memberType.IsGenericType && memberType.GetGenericTypeDefinition() == typeof(Nullable<>)) {
				memberType = Nullable.GetUnderlyingType(memberType);
			}
			if (memberType == typeof(Guid)) {
				return value is byte[]? new Guid((byte[])value) : new Guid(value.ToString());
			}
			try {
				return Convert.ChangeType(value, memberType);
			} catch (InvalidCastException) {
				return value;
			}
		}
	}
	public class ClassMetadataHelper {
		internal abstract class ItemProperties {
			public IDictionary dispMembers;
			public XPPropertyDescriptorCollection displayProps;
			public readonly IXPClassInfoAndSessionProvider Context;
			public abstract string GetDisplayableProperties();
			public ItemProperties(IXPClassInfoAndSessionProvider context) {
				this.Context = context;
			}
		}
		internal static PropertyDescriptorCollection GetItemProperties(DevExpress.Xpo.Helpers.ClassMetadataHelper.ItemProperties props, PropertyDescriptor[] listAccessors) {
			XPClassInfo info = GetMemberType(props.Context, listAccessors);
			if(info != null) {
				if(props.Context.Session.IsDesignMode && listAccessors.Length > 64)
					return new PropertyDescriptorCollection(Array.Empty<PropertyDescriptor>());
				return GetDescriptorCollection(props, GetMember(props.Context, listAccessors[listAccessors.Length - 1]), info);
			} else {
				if((listAccessors != null && listAccessors.Length != 0) || props.Context.ClassInfo == null)
					return new PropertyDescriptorCollection(Array.Empty<PropertyDescriptor>());
				if(props.displayProps == null) {
					props.displayProps = new XPPropertyDescriptorCollection(props.Context.Session, props.Context.ClassInfo, Array.Empty<PropertyDescriptor>());
					foreach(string propertyName in props.GetDisplayableProperties().Split(';')) {
						if(propertyName.Length != 0)
							props.displayProps.FindCaseSmart(propertyName);
					}
				}
				return props.displayProps;
			}
		}
		static XPPropertyDescriptorCollection GetDescriptorCollection(DevExpress.Xpo.Helpers.ClassMetadataHelper.ItemProperties props, XPMemberInfo mem, XPClassInfo info) {
			if(props.dispMembers == null)
				props.dispMembers = new Dictionary<object, object>();
			XPPropertyDescriptorCollection coll = (XPPropertyDescriptorCollection)props.dispMembers[mem];
			if(coll == null) {
				coll = new XPPropertyDescriptorCollection(props.Context.Session, info, Array.Empty<PropertyDescriptor>());
				string refMember = mem.IsCollection && !mem.IsManyToMany
					? mem.GetAssociatedMember().Name : string.Empty;
				StringCollection dispProps = ClassMetadataHelper.GetDefaultDisplayableProperties(info);
				foreach(string property in dispProps) {
					if(property.Length != 0 && refMember != property)
						coll.FindCaseSmart(property);
				}
				props.dispMembers[mem] = coll;
			}
			return coll;
		}
		static XPMemberInfo GetMember(DevExpress.Xpo.Metadata.Helpers.IXPDictionaryProvider dictionary, PropertyDescriptor prop) {
			XPPropertyDescriptor xpProp = prop as XPPropertyDescriptor;
			if(xpProp == null) {
				string name = XPPropertyDescriptor.GetMemberName(prop.Name);
				return DevExpress.Xpo.Metadata.Helpers.MemberInfoCollection.FindMember(dictionary.Dictionary.GetClassInfo(prop.ComponentType), name);
			}
			return xpProp.MemberInfo;
		}
		static XPClassInfo GetMemberType(DevExpress.Xpo.Metadata.Helpers.IXPDictionaryProvider dictionary, PropertyDescriptor[] listAccessors) {
			if(listAccessors != null && listAccessors.Length != 0 && typeof(XPBaseCollection).IsAssignableFrom(listAccessors[listAccessors.Length - 1].PropertyType)) {
				XPMemberInfo mi = GetMember(dictionary, listAccessors[listAccessors.Length - 1]);
				if(mi.IsAssociationList || mi.IsNonAssociationList)
					return mi.CollectionElementType;
				else
					return mi.ReferenceType;
			}
			return null;
		}
		public static string GetListName(PropertyDescriptor[] listAccessors) {
			if(listAccessors == null)
				return string.Empty;
			StringCollection coll = new StringCollection();
			foreach(PropertyDescriptor pd in listAccessors)
				coll.Add(pd.Name);
			return DevExpress.Xpo.DB.Helpers.StringListHelper.DelimitedText(coll, ".");
		}
		static bool IsGoodDefaultProperty(XPMemberInfo mi) {
			MemberDesignTimeVisibilityAttribute visibility = (MemberDesignTimeVisibilityAttribute)mi.FindAttributeInfo(typeof(MemberDesignTimeVisibilityAttribute));
			if(visibility != null && visibility.IsVisible)
				return true;
			if(!mi.IsPublic)
				return false;
			if(typeof(IBindingList).IsAssignableFrom(mi.MemberType) && !mi.IsCollection)
				return false;
			if(mi.MemberType != null && mi.MemberType.IsArray)
				return false;
			if(!mi.IsVisibleInDesignTime)
				return false;
			if(mi is DevExpress.Xpo.Metadata.Helpers.ServiceField)
				return false;
			return true;
		}
		public static StringCollection GetDefaultDisplayableProperties(XPClassInfo objectInfo) {
			if(objectInfo == null)
				return new StringCollection();
			StringCollection result = new StringCollection();
			foreach(XPMemberInfo mi in objectInfo.Members) {
				if(IsGoodDefaultProperty(mi)) {
					if(mi.ReferenceType != null && mi.ReferenceType.IdClass != null) {
						result.Add(mi.Name + XPPropertyDescriptor.ReferenceAsObjectTail);
						result.Add(mi.Name + XPPropertyDescriptor.ReferenceAsKeyTail);
					}
					result.Add(mi.Name);
				}
			}
			return result;
		}
	}
	class DefaultComparer {
		public static int Compare(object a, object b) {
			return Comparer.Default.Compare(a, b);
		}
	}
	public class CriteriaToDifferentSession: ClientCriteriaVisitorBase {
		protected readonly Session Session;
		protected override CriteriaOperator Visit(OperandValue theOperand) {
			IXPSimpleObject val = theOperand.Value as IXPSimpleObject;
			if(val == null)
				return theOperand;
			object resultValue = Session.GetObjectByKey(val.ClassInfo, val.Session.GetKeyValue(val));
			return theOperand is ConstantValue ? new ConstantValue(resultValue) : new OperandValue(resultValue);
		}
		CriteriaToDifferentSession(Session target) {
			this.Session = target;
		}
		public static CriteriaOperator Rebase(Session targetSession, CriteriaOperator criteria) {
			return new CriteriaToDifferentSession(targetSession).Process(criteria);
		}
	}
	public class AssociationXmlSerializationHelper: IList {
		public readonly XPBaseCollection BaseCollection;
		public IList BaseIList {
			get {
				return BaseCollection;
			}
		}
		public AssociationXmlSerializationHelper(XPBaseCollection baseColection) {
			this.BaseCollection = baseColection;
		}
		protected void PrepareBaseCollectionForDeserializationActions() {
			BaseCollection.LoadingEnabled = false;
		}
		public int Add(object value) {
			PrepareBaseCollectionForDeserializationActions();
			return BaseIList.Add(value);
		}
		public void Clear() {
			PrepareBaseCollectionForDeserializationActions();
			BaseIList.Clear();
		}
		public bool Contains(object value) {
			return BaseIList.Contains(value);
		}
		public int IndexOf(object value) {
			return BaseIList.IndexOf(value);
		}
		public void Insert(int index, object value) {
			PrepareBaseCollectionForDeserializationActions();
			BaseIList.Insert(index, value);
		}
		public bool IsFixedSize {
			get { return BaseIList.IsFixedSize; }
		}
		public bool IsReadOnly {
			get { return BaseIList.IsReadOnly; }
		}
		public void Remove(object value) {
			PrepareBaseCollectionForDeserializationActions();
			BaseIList.Remove(value);
		}
		public void RemoveAt(int index) {
			PrepareBaseCollectionForDeserializationActions();
			BaseIList.RemoveAt(index);
		}
		public object this[int index] {
			get {
				return BaseIList[index];
			}
			set {
				PrepareBaseCollectionForDeserializationActions();
				BaseIList[index] = value;
			}
		}
		public void CopyTo(Array array, int index) {
			BaseIList.CopyTo(array, index);
		}
		public int Count {
			get { return BaseIList.Count; }
		}
		public bool IsSynchronized {
			get { return BaseIList.IsSynchronized; }
		}
		public object SyncRoot {
			get { return BaseIList.SyncRoot; }
		}
		public IEnumerator GetEnumerator() {
			return BaseIList.GetEnumerator();
		}
	}
	public abstract class BaseListMorpher<I, T>: IList<I>, IList {
		public readonly IList<T> Morphed;
		protected abstract I DownCast(T value);
		protected abstract T UpCast(I value);
		protected BaseListMorpher(IList<T> morphed) {
			this.Morphed = morphed;
		}
		public int IndexOf(I item) {
			return Morphed.IndexOf(UpCast(item));
		}
		public void Insert(int index, I item) {
			Morphed.Insert(index, UpCast(item));
		}
		public void RemoveAt(int index) {
			Morphed.RemoveAt(index);
		}
		public I this[int index] {
			get {
				return DownCast(Morphed[index]);
			}
			set {
				Morphed[index] = UpCast(value);
			}
		}
		public void Add(I item) {
			Morphed.Add(UpCast(item));
		}
		public void Clear() {
			Morphed.Clear();
		}
		public bool Contains(I item) {
			return Morphed.Contains(UpCast(item));
		}
		public void CopyTo(I[] array, int arrayIndex) {
			for(int i = 0; i < Count; ++i) {
				array[i + arrayIndex] = this[i];
			}
		}
		public int Count {
			get { return Morphed.Count; }
		}
		public bool IsReadOnly {
			get { return Morphed.IsReadOnly; }
		}
		public bool Remove(I item) {
			return Morphed.Remove(UpCast(item));
		}
		public IEnumerator<I> GetEnumerator() {
			foreach(T item in Morphed)
				yield return DownCast(item);
		}
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
			return this.GetEnumerator();
		}
		int IList.Add(object value) {
			IList underlyingIList = Morphed as IList;
			if(underlyingIList != null)
				return underlyingIList.Add(UpCast((I)value));
			else {
				int c = Count;
				Add((I)value);
				return c;
			}
		}
		void IList.Clear() {
			Clear();
		}
		bool IList.Contains(object value) {
			return Contains((I)value);
		}
		int IList.IndexOf(object value) {
			return IndexOf((I)value);
		}
		void IList.Insert(int index, object value) {
			Insert(index, (I)value);
		}
		bool IList.IsFixedSize {
			get {
				IList underlyingIList = Morphed as IList;
				if(underlyingIList != null)
					return underlyingIList.IsFixedSize;
				else
					return false;
			}
		}
		bool IList.IsReadOnly {
			get { return IsReadOnly; }
		}
		void IList.Remove(object value) {
			Remove((I)value);
		}
		void IList.RemoveAt(int index) {
			RemoveAt(index);
		}
		object IList.this[int index] {
			get {
				return this[index];
			}
			set {
				this[index] = (I)value;
			}
		}
		void ICollection.CopyTo(Array array, int index) {
			for(int i = 0; i < Count; ++i) {
				array.SetValue(this[i], i + index);
			}
		}
		int ICollection.Count {
			get { return Count; }
		}
		bool ICollection.IsSynchronized {
			get {
				ICollection underlyingIC = Morphed as ICollection;
				if(underlyingIC != null)
					return underlyingIC.IsSynchronized;
				else
					return false;
			}
		}
		object ICollection.SyncRoot {
			get {
				ICollection underlyingIC = Morphed as ICollection;
				if(underlyingIC != null)
					return underlyingIC.SyncRoot;
				else
					return Morphed;
			}
		}
	}
	public class ListMorpher<I, T>: BaseListMorpher<I, T> where T: I {
		public ListMorpher(IList<T> morphed) : base(morphed) { }
		protected override I DownCast(T value) {
			return value;
		}
		protected override T UpCast(I value) {
			return (T)value;
		}
	}
	public interface IPersistentInterfaceData<T> {
		T Instance { get;}
	}
	public interface IPersistentInterface<T>  {
		IPersistentInterfaceData<T> PersistentInterfaceData { get;}
	}
	public class PersistentInterfaceMorpher<T>: BaseListMorpher<T, IPersistentInterfaceData<T>>, IXPUnloadableAssociationList {
		public PersistentInterfaceMorpher(IList<IPersistentInterfaceData<T>> morphed) : base(morphed) { }
		protected override T DownCast(IPersistentInterfaceData<T> value) {
			return value.Instance;
		}
		protected override IPersistentInterfaceData<T> UpCast(T value) {
			return ((IPersistentInterface<T>)value)?.PersistentInterfaceData;
		}
		void IXPUnloadableAssociationList.Unload() {
			((IXPUnloadableAssociationList)Morphed).Unload();
		}
		bool IXPUnloadableAssociationList.IsLoaded {
			get {
				return ((IXPUnloadableAssociationList)Morphed).IsLoaded;
			}
		}
	}
	public static class PersistentInterfaceTypedHelper {
		readonly static Dictionary<Type, ExtractorBase> cache = new Dictionary<Type, ExtractorBase>();
		public static object ExtractDataObject(Type interfaceType, object interfaceObject) {
			return GetExtractor(interfaceType).GetData(interfaceObject);
		}
		public static object ExtractInterfaceObject(Type interfaceType, object dataObject) {
			return GetExtractor(interfaceType).GetInterface(dataObject);
		}
		static ExtractorBase GetExtractor(Type t) {
			lock(cache) {
				ExtractorBase rv;
				if(!cache.TryGetValue(t, out rv)) {
					rv = (ExtractorBase)Activator.CreateInstance(typeof(Extractor<>).MakeGenericType(t));
					cache.Add(t, rv);
				}
				return rv;
			}
		}
		abstract class ExtractorBase {
			public abstract object GetData(object interfaceObject);
			public abstract object GetInterface(object dataObject);
		}
		class Extractor<T>: ExtractorBase {
			public override object GetData(object interfaceObject) {
				IPersistentInterface<T> ttt = interfaceObject as IPersistentInterface<T>;
				if(ttt == null)
					return null;
				else
					return ttt.PersistentInterfaceData;
			}
			public override object GetInterface(object dataObject) {
				IPersistentInterfaceData<T> ttt = dataObject as IPersistentInterfaceData<T>;
				if(ttt == null)
					return null;
				else
					return ttt.Instance;
			}
		}
	}
	public class ServerModeForPersistentInterfacesWrapper<T>: XpoServerCollectionWrapperBase where T: class {
		readonly XPClassInfo _ClassInfo;
		public ServerModeForPersistentInterfacesWrapper(IXpoServerModeGridDataSource nested)
			: base(nested) {
			this._ClassInfo = Session.GetClassInfo<T>();
			if(!Nested.ClassInfo.IsAssignableTo(Session.GetClassInfo<IPersistentInterfaceData<T>>())) {
				throw new InvalidOperationException("server mode source providing IPersistentInterfaceData<" + typeof(T).FullName + "> expected");
			}
		}
		public override object DXClone() {
			return new ServerModeForPersistentInterfacesWrapper<T>((IXpoServerModeGridDataSource)Nested.DXClone());
		}
		protected IPersistentInterfaceData<T> ToData(IPersistentInterface<T> i) {
			if(i == null)
				return null;
			return i.PersistentInterfaceData;
		}
		protected T ToInterface(IPersistentInterfaceData<T> d) {
			if(d == null)
				return null;
			return d.Instance;
		}
		protected IPersistentInterfaceData<T> ToData(object i) {
			return ToData((IPersistentInterface<T>)i);
		}
		protected T ToInterface(object d) {
			return ToInterface((IPersistentInterfaceData<T>)d);
		}
		public override XPClassInfo ClassInfo {
			get {
				return _ClassInfo;
			}
		}
		public override bool Contains(object value) {
			return base.Contains(ToData(value));
		}
		public override void Insert(int index, object value) {
			base.Insert(index, ToData(value));
		}
		public override int IndexOf(object value) {
			return base.IndexOf(ToData(value));
		}
		public override void Remove(object value) {
			base.Remove(ToData(value));
		}
		public override object this[int index] {
			get {
				return ToInterface(base[index]);
			}
			set {
				base[index] = ToData(value);
			}
		}
		public override void CopyTo(Array array, int index) {
			throw new NotSupportedException();
		}
		public override IEnumerator GetEnumerator() {
			foreach(object o in Nested) {
				yield return ToInterface(o);
			}
		}
		public override object AddNew() {
			return ToInterface(base.AddNew());
		}
		public override IList GetAllFilteredAndSortedRows() {
			IList inp = base.GetAllFilteredAndSortedRows();
			List<T> rv = new List<T>(inp.Count);
			foreach(object o in inp)
				rv.Add(ToInterface(o));
			return rv;
		}
		XPPropertyDescriptorCollection props;
		public override PropertyDescriptorCollection GetItemProperties(PropertyDescriptor[] listAccessors) {
			if(props == null) {
				props = new XPPropertyDescriptorCollection(Session, ClassInfo);
				foreach(PropertyDescriptor pd in Nested.GetItemProperties(null)) {
					props.Find(pd.Name, false);
				}
			}
			return props;
		}
	}
	public class ServerModeForPersistentInterfacesWrapper: XpoServerCollectionWrapperBase {
		public ServerModeForPersistentInterfacesWrapper(Type interfaceType, IXpoServerModeGridDataSource nested)
			: base(GetNestedSource(interfaceType, nested)) {
		}
		public override object DXClone() {
			return new ServerModeForPersistentInterfacesWrapper(ClassInfo.ClassType, (IXpoServerModeGridDataSource)Nested.DXClone());
		}
		static IXpoServerModeGridDataSource GetNestedSource(Type interfaceType, IXpoServerModeGridDataSource nested) {
			Type instType = typeof(ServerModeForPersistentInterfacesWrapper<>).MakeGenericType(interfaceType);
			return (IXpoServerModeGridDataSource)Activator.CreateInstance(instType, nested);
		}
	}
	public static class CannotLoadObjectsHelper {
		public class Results {
			public readonly Tuple<XPClassInfo, XPMemberInfo, Tuple<object, object>[]>[] OrphanedReferences;
			public Results(Tuple<XPClassInfo, XPMemberInfo, Tuple<object, object>[]>[] orphanedReferences) {
				this.OrphanedReferences = orphanedReferences;
			}
		}
		public static Results Analize(Session session) {
			session.TypesManager.EnsureIsTypedObjectValid();
			var persistentClasses = session.Dictionary.Classes.Cast<XPClassInfo>().Where(ci => ci.IsPersistent).ToArray();
			var references = persistentClasses.SelectMany(ci => ci.PersistentProperties.Cast<XPMemberInfo>().Where(mi => mi.IsPersistent && mi.ReferenceType != null && mi.GetMappingClass(ci) == ci).Select(mi => Tuple.Create(ci, mi))).ToArray();
			return new Results(CollectOrphanedReferences(session, references));
		}
		static Tuple<object, object>[] CollectOrphanedReferences(Session session, Tuple<XPClassInfo, XPMemberInfo> reference) {
			XPClassInfo ci = reference.Item1;
			XPMemberInfo r = reference.Item2;
			var criterion = new OperandProperty(r.Name).IsNotNull() & new JoinOperand(r.ReferenceType.FullName, new OperandProperty(r.ReferenceType.KeyProperty.Name) == new OperandProperty("^." + r.Name)).Not();
			var collector = session.SelectData(ci, new CriteriaOperatorCollection() { new OperandProperty(ci.KeyProperty.Name), new OperandProperty(r.Name) }, criterion, true, 0, null);
			return collector.Select(oa => Tuple.Create(oa[0], oa[1])).ToArray();
		}
		static Tuple<XPClassInfo, XPMemberInfo, Tuple<object, object>[]>[] CollectOrphanedReferences(Session session, IEnumerable<Tuple<XPClassInfo, XPMemberInfo>> references) {
			var res = new List<Tuple<XPClassInfo, XPMemberInfo, Tuple<object, object>[]>>();
			foreach(var reference in references) {
				var collected = CollectOrphanedReferences(session, reference);
				if(collected.Length == 0)
					continue;
				res.Add(Tuple.Create(reference.Item1, reference.Item2, collected));
			}
			return res.ToArray();
		}
	}
}
#if !NET
namespace DevExpress.Xpo {
	using System.Web.Services;
	using System.Web.Services.Protocols;
	using DevExpress.Xpo.Helpers;
	[WebServiceBinding(Namespace = WebServiceAttribute.DefaultNamespace)]
	public class WebServiceDataStore : SoapHttpClientProtocol, IDataStore, ICommandChannel {
		AutoCreateOption autoCreateOption;
		bool autoCreateOptionCached = false;
		public WebServiceDataStore(string url, AutoCreateOption autoCreateOption)
			: this(url) {
			this.autoCreateOption = autoCreateOption;
			this.autoCreateOptionCached = true;
		}
		public WebServiceDataStore(string url) {
			Url = url;
			UseDefaultCredentials = true;
		}
		public AutoCreateOption AutoCreateOption {
			get {
				if(!autoCreateOptionCached) {
					autoCreateOption = GetAutoCreateOption();
					autoCreateOptionCached = true;
				}
				return autoCreateOption;
			}
		}
		[SoapDocumentMethod]
		public AutoCreateOption GetAutoCreateOption() {
			object[] results = this.Invoke("GetAutoCreateOption", new object[0]);
			return ((AutoCreateOption)(results[0]));
		}
		[SoapDocumentMethod]
		public ModificationResult ModifyData(ModificationStatement[] dmlStatements) {
			object[] results = this.Invoke("ModifyData", new object[] {
						dmlStatements});
			return ((ModificationResult)(results[0]));
		}
		[SoapDocumentMethod]
		public SelectedData SelectData(SelectStatement[] selects) {
			object[] results = this.Invoke("SelectData", new object[] {
						selects});
			return ((SelectedData)(results[0]));
		}
		[SoapDocumentMethod]
		public UpdateSchemaResult UpdateSchema(bool doNotCreateIfFirstTableNotExist, params DBTable[] tables) {
			object[] results = this.Invoke("UpdateSchema", new object[] {
						doNotCreateIfFirstTableNotExist, tables});
			return ((UpdateSchemaResult)(results[0]));
		}
		[SoapDocumentMethod]
		public object Do(string command, object args) {
			object[] results = this.Invoke("Do", new object[] {
				command, args });
			return results[0];
		}
	}
}
#endif
namespace DevExpress.Xpo.DB {
	using DevExpress.Data.Helpers;
	using DevExpress.Xpo.Helpers;
	using System.Threading;
	using System.Threading.Tasks;
	public class DataStorePool: DataStoreForkBase, IDisposable {
		readonly AutoCreateOption aco;
		readonly string ConnectionString;
		readonly Queue<IDataStore> freiPool = new Queue<IDataStore>();
		readonly Dictionary<IDataStore, IDisposable[]> garbage = new Dictionary<IDataStore, IDisposable[]>();
		readonly int PoolSize;
		readonly int MaxConnections;
		int threads, connections;
		readonly AsyncManualResetEvent freeProvider = new AsyncManualResetEvent(false);
		bool isDisposed;
		public object SyncRoot { get { return this; } }
		public static int DefaultPoolSize = 8;
		public static int DefaultMaxConnections = -1;
		public const string XpoPoolParameterName = "XpoDataStorePool";
		public const string XpoPoolSizeParameterName = "XpoDataStorePoolSize";
		public const string XpoPoolMaxConnectionsParameterName = "XpoDataStorePoolMaxConnections";
		public DataStorePool(AutoCreateOption autoCreateOption, string connectionString) : this(autoCreateOption, connectionString, null, null) { }
		public DataStorePool(AutoCreateOption autoCreateOption, string connectionString, int? poolSize) : this(autoCreateOption, connectionString, poolSize, null) { }
		public DataStorePool(AutoCreateOption autoCreateOption, string connectionString, int? poolSize, int? maxConnections)
			: base() {
			this.aco = autoCreateOption;
			this.ConnectionString = connectionString;
			this.PoolSize = poolSize ?? DefaultPoolSize;
			if(this.PoolSize < 0)
				this.PoolSize = int.MaxValue;
			this.MaxConnections = maxConnections ?? DefaultMaxConnections;
			if(this.MaxConnections < 0)
				this.MaxConnections = int.MaxValue;
			if(this.MaxConnections < this.PoolSize)
				this.MaxConnections = this.PoolSize;
			if(this.MaxConnections == 0)
				this.MaxConnections = 1;
		}
		public override AutoCreateOption AutoCreateOption {
			get { return this.aco; }
		}
		public override IDataStore AcquireChangeProvider() {
			bool threadRegistered = false;
			for(; ; ) {
				lock(SyncRoot) {
					if(isDisposed) {
						if(threadRegistered)
							--threads;
						throw new ObjectDisposedException(this.ToString());
					}
					if(!threadRegistered) {
						threadRegistered = true;
						++threads;
					}
					if(freiPool.Count > 0) {
						return freiPool.Dequeue();
					}
					if(connections < MaxConnections) {
						++connections;
						break;
					}
					freeProvider.Reset();
				}
				freeProvider.WaitOne();
			}
			try {
				return CreateProvider();
			} catch {
				lock(SyncRoot) {
					--connections;
					if(threadRegistered) {
						--threads;
					}
				}
				throw;
			}
		}
		public override async Task<IDataStore> AcquireChangeProviderAsync(CancellationToken cancellationToken = default(CancellationToken)) {
			bool threadRegistered = false;
			for(; ; ) {
				lock(SyncRoot) {
					if(isDisposed) {
						if(threadRegistered)
							--threads;
						throw new ObjectDisposedException(this.ToString());
					}
					if(!threadRegistered) {
						threadRegistered = true;
						++threads;
					}
					if(freiPool.Count > 0) {
						return freiPool.Dequeue();
					}
					if(connections < MaxConnections) {
						++connections;
						break;
					}
					freeProvider.Reset();
				}
				await freeProvider.WaitOneAsync(cancellationToken);
			}
			try {
				return CreateProvider();
			} catch {
				lock(SyncRoot) {
					--connections;
					if(threadRegistered) {
						--threads;
					}
				}
				throw;
			}
		}
		public override void ReleaseChangeProvider(IDataStore provider) {
			lock(SyncRoot) {
				--threads;
				if((freiPool.Count < this.PoolSize || connections <= threads) && !isDisposed) {
					freiPool.Enqueue(provider);
					freeProvider.Set();
					return;
				}
				--connections;
			}
			DestroyProvider(provider);
		}
		IDataStore CreateProvider() {
			IDisposable[] toDispose;
			IDataStore rv = XpoDefault.GetConnectionProvider(this.ConnectionString, this.AutoCreateOption, out toDispose);
			lock (this.garbage) {
				this.garbage.Add(rv, toDispose);
			}
			return rv;
		}
		void DestroyProvider(IDataStore provider) {
			IDisposable[] toDispose;
			lock(this.garbage) {
				toDispose = garbage[provider];
				garbage.Remove(provider);
			}
			if(toDispose != null) {
				foreach(IDisposable d in toDispose) {
					d.Dispose();
				}
			}
		}
		public override IDataStore AcquireReadProvider() {
			return this.AcquireChangeProvider();
		}
		public override Task<IDataStore> AcquireReadProviderAsync(CancellationToken cancellationToken = default(CancellationToken)) {
			return this.AcquireChangeProviderAsync(cancellationToken);
		}
		public override void ReleaseReadProvider(IDataStore provider) {
			this.ReleaseChangeProvider(provider);
		}
		public void Dispose() {
			lock(SyncRoot) {
				if(isDisposed)
					return;
				isDisposed = true;
			}
			for(; ; ) {
				IDataStore prov;
				lock(SyncRoot) {
					if(freiPool.Count == 0)
						break;
					prov = freiPool.Dequeue();
				}
				DestroyProvider(prov);
			}
		}
	}
}
namespace DevExpress.Xpo {
	public static class XpoObjectInCriteriaProcessingHelper {
		public const string TagXpoObject = "XpoObject";
		internal static void Register() { }
		static XpoObjectInCriteriaProcessingHelper() {
			CriteriaOperator.UserValueParse += new EventHandler<UserValueProcessingEventArgs>(CriteriaOperator_UserValueParse);
			CriteriaOperator.UserValueToString += new EventHandler<UserValueProcessingEventArgs>(CriteriaOperator_UserValueToString);
		}
		static void CriteriaOperator_UserValueToString(object sender, UserValueProcessingEventArgs e) {
			if(Tweaks.SuppressXpoObjectToString)
				return;
			if(Tweaks.SuppressXpoObjectToStringWhenSessionScopeNotCreated && CurrentContext == null)
				return;
			if(e.Handled)
				return;
			Session session;
			IXPSimpleObject so = e.Value as IXPSimpleObject;
			if(so != null) {
				session = so.Session;
			} else {
				session = CurrentContext;
			}
			if(session == null)
				return;
			XPClassInfo ci = session.Dictionary.QueryClassInfo(e.Value);
			if(ci == null)
				return;
			if(!ci.IsPersistent)
				return;
			string key;
			if(session.IsNewObject(e.Value)) {
				if(Tweaks.ThrowExceptionOnNewObjectToString)
					throw new InvalidOperationException("New xpo object passed to XpoObjectInCriteriaProcessingHelper and XpoObjectInCriteriaProcessingHelper.Tweaks.ThrowExceptionOnNewObjectToString set to true");
				key = "New";
			} else {
				object keyValue = session.GetKeyValue(e.Value);
				var idList = keyValue as DevExpress.Xpo.Helpers.IdList;
				if(idList != null) {
					List<string> strings = new List<string>(idList.Count);
					foreach(var subKey in idList)
						strings.Add(new ConstantValue(subKey).ToString());
					key = string.Join(",", strings.ToArray());
				} else {
					key = new ConstantValue(keyValue).ToString();
				}
			}
			e.Tag = TagXpoObject;
			e.Data = string.Format("{0}({1})", ci.FullName, key);
			e.Handled = true;
		}
		static void CriteriaOperator_UserValueParse(object sender, UserValueProcessingEventArgs e) {
			if(e.Handled)
				return;
			if(e.Tag != TagXpoObject)
				return;
			if(Tweaks.SuppressExceptionsOnParse) {
				try {
					CriteriaOperator_UserValueParse_Core(e);
				} catch {
					e.Value = e.Data;
					e.Handled = true;
				}
			} else {
				CriteriaOperator_UserValueParse_Core(e);
			}
		}
		private static void CriteriaOperator_UserValueParse_Core(UserValueProcessingEventArgs e) {
			Session session = CurrentContext;
			if(session == null)
				throw new InvalidOperationException("Session scope is not specified for CriteriaOperator.Parse operation. Please use session.ParseCriteria or using(session.CreateParseCriteriaSessionScope()) { CriteriaOperator.Parse(...); } instead");
			if(string.IsNullOrEmpty(e.Data))
				throw new ArgumentNullException("e.Data");
			int openParPos = e.Data.IndexOf('(');
			if(openParPos < 0 || e.Data[e.Data.Length - 1] != ')')
				throw new InvalidOperationException("Invalid e.Data; expected format is 'Namespace.ClassName(KeyValue)'");
			string className = e.Data.Substring(0, openParPos);
			string keyString = e.Data.Substring(openParPos + 1, e.Data.Length - openParPos - 2);
			switch(keyString) {
				case "New":
					throw new InvalidOperationException("New object can't be restored");
			}
			XPClassInfo ci = session.GetClassInfo(null, className);
			CriteriaOperator[] ops = CriteriaOperator.ParseList(keyString);
			List<object> values = new List<object>(ops.Length);
			foreach(CriteriaOperator op in ops) {
				ConstantValue cv = op as ConstantValue;
				if(ReferenceEquals(cv, null))
					throw new InvalidOperationException("invalid key value");
				values.Add(cv.Value);
			}
			object key;
			switch(values.Count) {
				case 0:
					key = null;
					break;
				case 1:
					key = values[0];
					break;
				default:
					key = new DevExpress.Xpo.Helpers.IdList(values);
					break;
			}
			e.Value = session.GetObjectByKey(ci, key);
			e.Handled = true;
		}
		[ThreadStatic]
		static Session CurrentContext;
		class ParseCriteriaSessionScope: IDisposable {
			Session prevValue;
			Session myValue;
			bool isDisposed;
			public ParseCriteriaSessionScope(Session session) {
				this.myValue = session;
				prevValue = XpoObjectInCriteriaProcessingHelper.CurrentContext;
				XpoObjectInCriteriaProcessingHelper.CurrentContext = session;
			}
			public void Dispose() {
				if(isDisposed)
					return;
				isDisposed = true;
				if(XpoObjectInCriteriaProcessingHelper.CurrentContext != myValue) {
					throw new InvalidOperationException(string.Format("Incorrect ParseCriteriaSessionScope usage detected! Expected session: '{0}', actual: '{1}'", myValue == null ? (object)"null" : myValue, XpoObjectInCriteriaProcessingHelper.CurrentContext == null ? (object)"null" : XpoObjectInCriteriaProcessingHelper.CurrentContext));
				}
				XpoObjectInCriteriaProcessingHelper.CurrentContext = prevValue;
			}
		}
		public static IDisposable CreateParseCriteriaSessionScope(this Session session) {
			return new ParseCriteriaSessionScope(session);
		}
		public static CriteriaOperator ParseCriteria(this Session session, string stringCriteria, out OperandValue[] criteriaParametersList) {
			using(CreateParseCriteriaSessionScope(session)) {
				return CriteriaOperator.Parse(stringCriteria, out criteriaParametersList);
			}
		}
		public static CriteriaOperator ParseCriteria(this Session session, string stringCriteria, params object[] parameters) {
			using(CreateParseCriteriaSessionScope(session)) {
				return CriteriaOperator.Parse(stringCriteria, parameters);
			}
		}
		public static class Tweaks {
			public static bool SuppressXpoObjectToString;
			public static bool SuppressXpoObjectToStringWhenSessionScopeNotCreated;
			public static bool ThrowExceptionOnNewObjectToString;
			public static bool SuppressExceptionsOnParse;
		}
	}
}
