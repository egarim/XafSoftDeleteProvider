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
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using DevExpress.Data.Filtering;
using DevExpress.Data.Filtering.Helpers;
using DevExpress.Utils;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.Exceptions;
using DevExpress.Xpo.Generators;
using DevExpress.Xpo.Helpers;
using DevExpress.Xpo.Infrastructure;
using DevExpress.Xpo.Metadata;
using DevExpress.Xpo.Metadata.Helpers;
namespace DevExpress.Xpo.Helpers {
	public interface ISerializableObjectLayerProvider {
		ISerializableObjectLayer ObjectLayer { get; }
	}
}
namespace DevExpress.Xpo {
	public interface ISerializableObjectLayer : ISerializableObjectLayerProvider {
		SerializableObjectLayerResult<XPObjectStubCollection[]> LoadObjects(XPDictionaryStub dictionary, ObjectStubsQuery[] queries);
		CommitObjectStubsResult[] CommitObjects(XPDictionaryStub dictionary, XPObjectStubCollection objectsForDelete, XPObjectStubCollection objectsForSave, LockingOption lockingOption);
		SerializableObjectLayerResult<XPObjectStubCollection[]> GetObjectsByKey(XPDictionaryStub dictionary, GetObjectStubsByKeyQuery[] queries);
		object[][] SelectData(XPDictionaryStub dictionary, ObjectStubsQuery query, CriteriaOperatorCollection properties, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria);
		void CreateObjectType(string assemblyName, string typeName);
		bool CanLoadCollectionObjects { get; }
		SerializableObjectLayerResult<XPObjectStubCollection> LoadCollectionObjects(XPDictionaryStub dictionary, string refPropertyName, XPObjectStub ownerObject);
		PurgeResult Purge();
	}
	public interface ISerializableObjectLayerEx {
		SerializableObjectLayerResult<object[]> LoadDelayedProperties(XPDictionaryStub dictionary, XPObjectStub theObject, string[] props);
		SerializableObjectLayerResult<object[]> LoadDelayedProperties(XPDictionaryStub dictionary, XPObjectStubCollection objects, string property);
		bool IsParentObjectToSave(XPDictionaryStub dictionary, XPObjectStub theObject);
		bool IsParentObjectToDelete(XPDictionaryStub dictionary, XPObjectStub theObject);
		SerializableObjectLayerResult<XPObjectStubCollection> GetParentObjectsToSave();
		SerializableObjectLayerResult<XPObjectStubCollection> GetParentObjectsToDelete();
		string[] GetParentTouchedClassInfos();
	}
	[Serializable]
	[XmlType("XPObjectCollection")]
	public class XPObjectStubCollection {
		List<XPObjectStub> innerList;
		public XPObjectStubCollection() { innerList = new List<XPObjectStub>(); }
		public XPObjectStubCollection(int capacity) { innerList = new List<XPObjectStub>(capacity); }
		public XPObjectStubCollection(IEnumerable<XPObjectStub> collection) { innerList = new List<XPObjectStub>(collection); }
		[Browsable(false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		[XmlArray(ElementName = "Items", IsNullable = true)]
		public XPObjectStub[] Items {
			get { return innerList.ToArray(); }
			set {
				innerList.Clear();
				if(value == null || value.Length == 0) return;
				innerList.AddRange(value);
			}
		}
#if NET
		[XmlIgnore]
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public object[] ReferencedObjects { get; set; }
		[XmlIgnore]
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public int[][] ReferenceMap { get; set; }
#endif
		public int IndexOf(XPObjectStub item) {
			return innerList.IndexOf(item);
		}
		public void Insert(int index, XPObjectStub item) {
			innerList.Insert(index, item);
		}
		public void RemoveAt(int index) {
			innerList.RemoveAt(index);
		}
		public List<XPObjectStub> GetInnerList() {
			return innerList;
		}
		[XmlIgnore]
		public XPObjectStub this[int index] {
			get {
				return innerList[index];
			}
			set {
				innerList[index] = value;
			}
		}
		public void AddRange(IEnumerable<XPObjectStub> collection) {
			innerList.AddRange(collection);
		}
		public System.Collections.ObjectModel.ReadOnlyCollection<XPObjectStub> AsReadOnly() {
			return innerList.AsReadOnly();
		}
		public void Add(XPObjectStub item) {
			innerList.Add(item);
		}
		public void Clear() {
			innerList.Clear();
		}
		public bool Contains(XPObjectStub item) {
			return innerList.Contains(item);
		}
		public void CopyTo(XPObjectStub[] array, int arrayIndex) {
			innerList.CopyTo(array, arrayIndex);
		}
		[Description("This member supports the internal infrastructure and is not intended to be used directly from your code."),
XmlIgnore]
		public int Count {
			get { return innerList.Count; }
		}
		[Description("This member supports the internal infrastructure and is not intended to be used directly from your code."),
XmlIgnore]
		public bool IsReadOnly {
			get { return false; }
		}
		public bool Remove(XPObjectStub item) {
			return innerList.Remove(item);
		}
		public IEnumerator<XPObjectStub> GetEnumerator() {
			return innerList.GetEnumerator();
		}
	}
	[Serializable]
	[XmlType("XPMember")]
	public struct XPClassInfoStubMemberItem {
		public string Name;
		public int Index;
		public XPClassInfoStubMemberItem(string name, int index) {
			Name = name;
			Index = index;
		}
	}
	[Serializable]
	[XmlType("OldValueContainer")]
	public class XPClassInfoStubOldValueContainer {
		public object Data;
		public XPClassInfoStubOldValueContainer() { }
		public XPClassInfoStubOldValueContainer(object data) {
			Data = data;
		}
	}
	[Serializable]
	[XmlType("XPClassInfo")]
	public class XPClassInfoStub {
		string className;
		[NonSerialized]
		Dictionary<string, int> members;
		string keyFieldName;
		string optimisticLockFieldName;
		string optimisticLockFieldInDataLayerName;
		XPClassInfoStubMemberItem[] membersList;
		[Description("This member supports the internal infrastructure and is not intended to be used directly from your code.")]
		public string ClassName { get { return className; } set { className = value; } }
		[XmlIgnore]
		Dictionary<string, int> Members {
			get {
				if(members == null) {
					members = GetMembers(membersList);
				}
				return members;
			}
		}
		[Browsable(false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		[XmlArray(ElementName = "Members", IsNullable = true)]
		public XPClassInfoStubMemberItem[] MembersList {
			get { return membersList; }
			set {
				membersList = value;
				members = null;
			}
		}
		[Description("This member supports the internal infrastructure and is not intended to be used directly from your code.")]
		public string KeyFieldName { get { return keyFieldName; } set { keyFieldName = value; } }
		[Description("This member supports the internal infrastructure and is not intended to be used directly from your code.")]
		public string OptimisticLockFieldName { get { return optimisticLockFieldName; } set { optimisticLockFieldName = value; } }
		[Description("This member supports the internal infrastructure and is not intended to be used directly from your code.")]
		public string OptimisticLockFieldInDataLayerName { get { return optimisticLockFieldInDataLayerName; } set { optimisticLockFieldInDataLayerName = value; } }
		public XPClassInfoStub() : this(string.Empty) { }
		public XPClassInfoStub(string className) {
			this.className = className;
		}
		static Dictionary<string, int> GetMembers(XPClassInfoStubMemberItem[] newMembersList) {
			Dictionary<string, int> membersCreate = new Dictionary<string, int>();
			if(newMembersList != null) {
				for(int i = 0; i < newMembersList.Length; i++) {
					membersCreate.Add(newMembersList[i].Name, newMembersList[i].Index);
				}
			}
			return membersCreate;
		}
		[Description("This member supports the internal infrastructure and is not intended to be used directly from your code.")]
		public int MemberCount {
			get {
				return membersList == null ? 0 : membersList.Length;
			}
		}
		public int GetMemberIndex(string memberName, bool raiseException) {
			int memberIndex;
			if(!Members.TryGetValue(memberName, out memberIndex)) {
				if(raiseException) throw new ArgumentException(Res.GetString(Res.ObjectLayer_MemberNotFound, className, memberName));
				return -1;
			}
			return memberIndex;
		}
		public object GetMemberValue(XPObjectStub obj, string memberName) {
			if(memberName == keyFieldName) {
				return obj.Key;
			}
			return obj.Data[GetMemberIndex(memberName, true)];
		}
		public void SetMemberValue(XPObjectStub obj, string memberName, object value) {
			if(memberName == keyFieldName) {
				obj.Key = value;
				return;
			}
			int index = GetMemberIndex(memberName, true);
			obj.Data[index] = value;
			obj.Changed[index] = true;
		}
		public object GetMemberOldValue(XPObjectStub obj, string memberName) {
			if(memberName == keyFieldName) {
				return obj.Key;
			}
			if(obj.OldData == null) return null;
			return obj.OldData[GetMemberIndex(memberName, true)].Data;
		}
		public object GetMemberHasOldValue(XPObjectStub obj, string memberName) {
			if(memberName == keyFieldName) {
				return obj.Key;
			}
			if(obj.OldData == null) return false;
			return obj.OldData[GetMemberIndex(memberName, true)] != null;
		}
		public void SetMemberOldValue(XPObjectStub obj, string memberName, object value) {
			if(memberName == keyFieldName) {
				return;
			}
			int index = GetMemberIndex(memberName, true);
			if(obj.OldData == null)
				obj.OldData = new XPClassInfoStubOldValueContainer[obj.Data.Length];
			obj.OldData[index] = new XPClassInfoStubOldValueContainer(value);
		}
		public bool IsMemberChanged(XPObjectStub obj, string memberName) {
			if(memberName == keyFieldName) {
				return false;
			}
			return obj.Changed[GetMemberIndex(memberName, true)];
		}
		public override string ToString() {
			return className;
		}
		public override int GetHashCode() {
			return HashCodeHelper.GetHashCode(className);
		}
		public override bool Equals(object obj) {
			XPClassInfoStub other = obj as XPClassInfoStub;
			if(other == null) return false;
			return string.Equals(className, other.className);
		}
		public static XPClassInfoStub FromClassInfo(XPClassInfo classInfo) {
			XPClassInfoStub stub = new XPClassInfoStub(classInfo.FullName);
			List<string> propertyNames = new List<string>();
			foreach(XPMemberInfo mi in classInfo.PersistentProperties) {
				if(mi.IsKey) continue;
				propertyNames.Add(mi.Name);
			}
			propertyNames.Sort();
			XPClassInfoStubMemberItem[] properties = new XPClassInfoStubMemberItem[propertyNames.Count];
			for(int i = 0; i < propertyNames.Count; i++) {
				properties[i] = new XPClassInfoStubMemberItem(propertyNames[i], i);
			}
			stub.MembersList = properties;
			stub.KeyFieldName = classInfo.KeyProperty.Name;
			if(classInfo.OptimisticLockField != null) {
				stub.optimisticLockFieldName = classInfo.OptimisticLockField.Name;
			}
			else stub.optimisticLockFieldName = null;
			if(classInfo.OptimisticLockFieldInDataLayer != null) {
				stub.optimisticLockFieldInDataLayerName = classInfo.OptimisticLockFieldInDataLayer.Name;
			}
			else stub.optimisticLockFieldInDataLayerName = null;
			return stub;
		}
	}
	[Serializable]
	public class SerializableObjectLayerResult<T> {
		public XPDictionaryStub Dictionary;
		public T Result;
		public SerializableObjectLayerResult() { }
		public SerializableObjectLayerResult(XPDictionaryStub dictionary, T result) {
			Dictionary = dictionary;
			Result = result;
		}
	}
	[Serializable]
	[XmlType("XPDictionary")]
	public class XPDictionaryStub {
		XPClassInfoStub[] classInfoList;
		[XmlArrayItem("classInfo")]
		public XPClassInfoStub[] ClassInfoList {
			get { return classInfoList; }
			set { classInfoList = value; }
		}
		public XPDictionaryStub() { }
		public XPDictionaryStub(XPClassInfoStub[] classInfoList) {
			this.classInfoList = classInfoList;
		}
	}
	public class XPObjectClassInfoStubCache : IXPDictionaryProvider {
		readonly XPDictionary dictionary;
		Dictionary<XPClassInfo, XPClassInfoStub> classInfoDict;
		Dictionary<string, XPClassInfo> classNameDict;
		List<XPClassInfoStub> newItems;
		public XPObjectClassInfoStubCache(IXPDictionaryProvider dictionaryProvider) {
			this.dictionary = dictionaryProvider.Dictionary;
		}
		public XPObjectClassInfoStubCache(IXPDictionaryProvider dictionaryProvider, XPDictionaryStub dictionaryStub)
			: this(dictionaryProvider) {
			UpdateClasses(dictionaryStub);
		}
		public XPDictionaryStub GetNewDictionaryStub() {
			return new XPDictionaryStub(newItems == null ? null : newItems.ToArray());
		}
		public void UpdateClasses(XPDictionaryStub dictionaryStub) {
			if(dictionaryStub == null || dictionaryStub.ClassInfoList == null) return;
			if(classInfoDict == null) classInfoDict = new Dictionary<XPClassInfo, XPClassInfoStub>();
			foreach(XPClassInfoStub ciStub in dictionaryStub.ClassInfoList) {
				XPClassInfo ci = GetClassInfo(ciStub.ClassName);
				classInfoDict[ci] = ciStub;
			}
		}
		public XPClassInfoStub GetStub(XPClassInfo classInfo) {
			if(classInfoDict == null) classInfoDict = new Dictionary<XPClassInfo, XPClassInfoStub>();
			XPClassInfoStub stub;
			if(!classInfoDict.TryGetValue(classInfo, out stub)) {
				if(newItems == null) newItems = new List<XPClassInfoStub>();
				stub = XPClassInfoStub.FromClassInfo(classInfo);
				classInfoDict.Add(classInfo, stub);
				newItems.Add(stub);
			}
			return stub;
		}
		public XPClassInfo GetClassInfo(XPClassInfoStub classInfoStub) {
			XPClassInfo result = Dictionary.QueryClassInfo(string.Empty, classInfoStub.ClassName);
			if(result == null) throw new InvalidOperationException(Res.GetString(Res.ObjectLayer_XPClassInfoNotFound, classInfoStub.ClassName));
			return result;
		}
		public XPClassInfo GetClassInfo(string className) {
			if(classNameDict == null) {
				classNameDict = new Dictionary<string, XPClassInfo>();
			}
			XPClassInfo ci;
			if(!classNameDict.TryGetValue(className, out ci)) {
				ci = Dictionary.QueryClassInfo(string.Empty, className);
				if(ci == null) throw new InvalidOperationException(Res.GetString(Res.ObjectLayer_XPClassInfoNotFound, className));
				classNameDict.Add(className, ci);
			}
			return ci;
		}
		public XPClassInfoStub GetStub(string className) {
			return GetStub(GetClassInfo(className));
		}
		public XPDictionary Dictionary {
			get { return dictionary; }
		}
	}
	public class XPObjectStubCache {
		Dictionary<Guid, XPObjectStub> objectDict = new Dictionary<Guid, XPObjectStub>();
		public XPObjectStub GetStub(Guid guid) {
			XPObjectStub objectStub;
			if(!objectDict.TryGetValue(guid, out objectStub)) return null;
			return objectStub;
		}
		public void Add(Guid guid, XPObjectStub objectStub) {
			objectDict.Add(guid, objectStub);
		}
	}
	[Serializable]
	[XmlType("XPObject")]
	public class XPObjectStub {
		Guid guid;
		object key;
		object[] data;
		XPClassInfoStubOldValueContainer[] oldData;
		bool[] changed;
		string className;
		object optimisticLockFieldInDataLayer;
		bool isEmpty;
		bool isNew;
		bool isIntermediate;
		[Description("This member supports the internal infrastructure and is not intended to be used directly from your code."),
XmlIgnore]
		public bool HasGuid { get { return guid != Guid.Empty; } }
		[Description("This member supports the internal infrastructure and is not intended to be used directly from your code.")]
		public Guid Guid { get { return guid; } set { guid = value; } }
		[Description("This member supports the internal infrastructure and is not intended to be used directly from your code.")]
		public string ClassName {
			get { return className; }
			set { className = value; }
		}
		[Description("This member supports the internal infrastructure and is not intended to be used directly from your code.")]
		public object Key {
			get { return key; }
			set { key = value; }
		}
		[Browsable(false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		[XmlArray(ElementName = "Data", IsNullable = true)]
		public object[] Data { get { return data; } set { data = value; } }
		[Browsable(false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		[XmlArray(ElementName = "OldData", IsNullable = true)]
		public XPClassInfoStubOldValueContainer[] OldData { get { return oldData; } set { oldData = value; } }
		[Browsable(false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		[XmlArray(ElementName = "Changed", IsNullable = true)]
		public bool[] Changed { get { return changed; } set { changed = value; } }
		[Description("This member supports the internal infrastructure and is not intended to be used directly from your code.")]
		public bool IsNew { get { return isNew; } set { isNew = value; } }
		[Description("This member supports the internal infrastructure and is not intended to be used directly from your code.")]
		public bool IsIntermediate { get { return isIntermediate; } set { isIntermediate = value; } }
		[Description("This member supports the internal infrastructure and is not intended to be used directly from your code.")]
		public object OptimisticLockFieldInDataLayer { get { return optimisticLockFieldInDataLayer; } set { optimisticLockFieldInDataLayer = value; } }		
		[Browsable(false)]
		[DXHelpExclude(true)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public bool IsEmpty { get { return isEmpty; } }
		public XPObjectStub() { }
		public XPObjectStub(XPClassInfoStub classInfo)
			: this(classInfo.ClassName, new object[classInfo.MemberCount], null, new bool[classInfo.MemberCount]) {
		}
		public XPObjectStub(XPClassInfoStub classInfo, object[] data)
			: this(classInfo.ClassName, data, null, new bool[classInfo.MemberCount]) {
			for(int i = 0; i < changed.Length; i++) {
				changed[i] = true;
			}
		}
		public XPObjectStub(string className, object[] data, XPClassInfoStubOldValueContainer[] oldData, bool[] changed) {
			Guard.ArgumentNotNull(className, nameof(className));
			this.className = className;
			this.data = data;
			this.oldData = oldData;
			this.changed = changed;
		}
		public XPObjectStub(XPClassInfoStub classInfo, Guid guid) : this(classInfo.ClassName, guid, new object[classInfo.MemberCount], null, new bool[classInfo.MemberCount]) { }
		public XPObjectStub(XPClassInfoStub classInfo, Guid guid, object[] data)
			: this(classInfo.ClassName, guid, data, null, new bool[classInfo.MemberCount]) {
			for(int i = 0; i < changed.Length; i++) {
				changed[i] = true;
			}
		}
		public XPObjectStub(string className, Guid guid, object[] data, XPClassInfoStubOldValueContainer[] oldData, bool[] changed)
			: this(className, data, oldData, changed) {
			this.guid = guid;
		}
		public void MarkAsNew() {
			isNew = true;
		}
		public void MarkAsIntermediate() {
			isIntermediate = true;
		}
		public void MarkAsEmpty() {
			isEmpty = true;
		}
		public static EvaluatorContextDescriptor GetEvaluatorContextDescriptor(IXPDictionaryProvider dictionaryProvider) {
			return new EvaluatorContextDescriptorXpoStub(dictionaryProvider);
		}
		int GetClassNameHashCode() {
			return HashCodeHelper.GetHashCode(className);
		}
		public override int GetHashCode() {
			int hashCode = HashCodeHelper.StartGeneric(GetClassNameHashCode(), guid, optimisticLockFieldInDataLayer);
			for(int i = 0; i < data.Length; i++) {
				if(data[i] == null) {
					hashCode = HashCodeHelper.Combine(hashCode, 1);
					continue;
				}
				XPObjectStub stubData = data[i] as XPObjectStub;
				if(stubData != null) {
					hashCode = HashCodeHelper.Combine(hashCode, stubData.GetClassNameHashCode());
					hashCode = HashCodeHelper.CombineGeneric(hashCode, stubData.Key);
				}
				else {
					hashCode = HashCodeHelper.CombineGeneric(hashCode, data[i]);
				}
			}
			return HashCodeHelper.Finish(hashCode);
		}
		public override bool Equals(object obj) {
			XPObjectStub other = obj as XPObjectStub;
			if(other == null || !guid.Equals(other.guid) || data.Length != other.data.Length
				|| !string.Equals(className, other.className)) return false;
			for(int i = 0; i < data.Length; i++) {
				XPObjectStub stubData = data[i] as XPObjectStub;
				XPObjectStub otherStubData = other.data[i] as XPObjectStub;
				if(stubData != null && otherStubData != null) {
					if(!ReferenceEquals(stubData, otherStubData)) {
						if(!string.Equals(stubData.ClassName, otherStubData.ClassName)) return false;
						if(!object.Equals(stubData.Key, otherStubData.Key) && stubData.Guid != otherStubData.Guid) {
							if(!stubData.Equals(otherStubData)) return false;
						}
					}
				}
				else {
					if(!object.Equals(data[i], other.data[i])) return false;
				}
			}
			return object.Equals(optimisticLockFieldInDataLayer, other.optimisticLockFieldInDataLayer);
		}
		public override string ToString() {
			return string.Format(CultureInfo.InvariantCulture, "{0}({1})", className, Key);
		}
	}
	[Serializable]
	[XmlRoot("ObjectsQuery")]
	public class ObjectStubsQuery {
		XPClassInfoStub classInfo;
		[Description("This member supports the internal infrastructure and is not intended to be used directly from your code.")]
		public XPClassInfoStub ClassInfo {
			get { return classInfo; }
			set { classInfo = value; }
		}
		CriteriaOperator criteria;
		[Description("This member supports the internal infrastructure and is not intended to be used directly from your code.")]
		public CriteriaOperator Criteria {
			get { return criteria; }
			set { criteria = value; }
		}
		string[] sortingStrings;
		[XmlArrayItem("sorting")]
		[Browsable(false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public string[] SortingStrings {
			get { return SortingCollectionSerializer.Serialize(sorting); }
			set { sorting = SortingCollectionSerializer.Deserialize(value); }
		}
		[NonSerialized]
		SortingCollection sorting;
		[Description("This member supports the internal infrastructure and is not intended to be used directly from your code."),
XmlIgnore]
		public SortingCollection Sorting {
			get { return sorting; }
			set { sorting = value; }
		}
		int topSelectedRecords;
		[Description("This member supports the internal infrastructure and is not intended to be used directly from your code.")]
		public int TopSelectedRecords {
			get { return topSelectedRecords; }
			set { topSelectedRecords = value; }
		}
		int skipSelectedRecords;
		[Description("This member supports the internal infrastructure and is not intended to be used directly from your code.")]
		public int SkipSelectedRecords {
			get { return skipSelectedRecords; }
			set { skipSelectedRecords = value; }
		}
		bool selectDeleted;
		[Description("This member supports the internal infrastructure and is not intended to be used directly from your code.")]
		public bool SelectDeleted {
			get { return selectDeleted; }
			set { selectDeleted = value; }
		}
		bool force;
		[Description("This member supports the internal infrastructure and is not intended to be used directly from your code.")]
		public bool Force {
			get { return force; }
			set { force = value; }
		}
		public ObjectStubsQuery() { }
		public ObjectStubsQuery(ObjectsQuery query, Session session, NestedParentGuidMap map, XPObjectClassInfoStubCache classInfoCache)
			:
			this(classInfoCache.GetStub(query.ClassInfo), XPObjectStubCriteriaGenerator.GetStubCriteria(session, map, classInfoCache, query.Criteria), XPObjectStubCriteriaGenerator.PreprocessSortingCollection(session, map, classInfoCache, query.Sorting), query.SkipSelectedRecords, query.TopSelectedRecords, query.CollectionCriteriaPatcher == null ? false : query.CollectionCriteriaPatcher.SelectDeleted, query.Force) {
		}
		public ObjectStubsQuery(XPClassInfoStub classInfo, CriteriaOperator criteria, SortingCollection sorting, int skipSelectedRecords, int topSelectedRecords, bool selectDeleted, bool force) {
			Guard.ArgumentNotNull(classInfo, nameof(classInfo));
			this.classInfo = classInfo;
			this.criteria = criteria;
			this.sorting = sorting;
			this.topSelectedRecords = topSelectedRecords;
			this.skipSelectedRecords = skipSelectedRecords;
			this.selectDeleted = selectDeleted;
			this.force = force;
		}
		[OnSerializing]
		internal void OnSerializingMethod(StreamingContext context) {
			sortingStrings = SortingCollectionSerializer.Serialize(sorting);
		}
		[OnSerialized]
		internal void OnSerializedMethod(StreamingContext context) {
			sortingStrings = null;
		}
		[OnDeserialized]
		internal void OnDeserializedMethod(StreamingContext context) {
			sorting = SortingCollectionSerializer.Deserialize(sortingStrings);
			sortingStrings = null;
		}
	}
	public class SortingCollectionSerializer {
		const string SortingCollectionSerializerV2Prefix = "XPOSCSV2";
		public static string[] Serialize(SortingCollection collection) {
			if(collection == null)
				return null;
			string[] result = new string[collection.Count];
			for(int i = 0; i < collection.Count; i++)
				result[i] = Serialize(collection[i]);
			return result;
		}
		public static SortingCollection Deserialize(string[] collection) {
			if(collection == null)
				return null;
			SortProperty[] result = new SortProperty[collection.Length];
			for(int i = 0; i < collection.Length; i++)
				result[i] = Deserialize(collection[i]);
			return new SortingCollection(result);
		}
		static string Serialize(SortProperty property) {
			using(StringWriter writer = new StringWriter()) {
				writer.WriteLine(SortingCollectionSerializerV2Prefix);
				SafeXml.Cached.Serialize<SortProperty>(writer, property);
				return writer.ToString();
			}
		}
		static SortProperty Deserialize(string strProperty) {
			if(strProperty.StartsWith(SortingCollectionSerializerV2Prefix, StringComparison.Ordinal)) {
				using(StringReader reader = new StringReader(strProperty)) {
					reader.ReadLine();
					return SafeXml.Cached.Deserialize<SortProperty>(reader);
				}
			}
			return new SortProperty(strProperty.Substring(1), strProperty[0] == 'A' ? SortingDirection.Ascending : SortingDirection.Descending); 
		}
	}
	public class SerializableObjectLayerClient : IObjectLayer, IObjectLayerEx, IObjectLayerOnSession, IObjectLayerAsync, ICommandChannel, ICommandChannelAsync, IInfrastructure<ISecuredPropertyAccessor> {
		readonly ISerializableObjectLayer objectLayer;
		readonly XPDictionary dictionary;
		readonly ICommandChannel nestedCommandChannel;
		readonly ICommandChannelAsync nestedCommandChannelAsync;
		[Description("")]
		[Browsable(false)]
		public ISerializableObjectLayer SerializableObjectLayer { get { return objectLayer; } }
		public SerializableObjectLayerClient(ISerializableObjectLayer objectLayer)
			: this(objectLayer, null) {
		}
		public SerializableObjectLayerClient(ISerializableObjectLayer objectLayer, XPDictionary dictionary) {
			if(dictionary == null)
				dictionary = XpoDefault.GetDictionary();
			this.dictionary = dictionary;
			this.objectLayer = objectLayer;
			this.nestedCommandChannel = objectLayer as ICommandChannel;
			this.nestedCommandChannelAsync = objectLayer as ICommandChannelAsync;
		}
		static object nestedParentGuidMapKeyObject = new object();
		internal static NestedParentGuidMap GetNestedParentGuidMap(Session session) {
			object nestedParentGuidMap = null;
			if(!((IWideDataStorage)session).TryGetWideDataItem(nestedParentGuidMapKeyObject, out nestedParentGuidMap)) {
				if(session.GetIdentityMapBehavior() == IdentityMapBehavior.Weak) {
					nestedParentGuidMap = new WeakNestedParentGuidMap(session);
				}
				else {
					nestedParentGuidMap = new StrongNestedParentGuidMap(session);
				}
				((IWideDataStorage)session).SetWideDataItem(nestedParentGuidMapKeyObject, nestedParentGuidMap);
			}
			return (NestedParentGuidMap)nestedParentGuidMap;
		}
		public ICollection[] LoadObjects(Session session, ObjectsQuery[] queries) {
			session.WaitForAsyncOperationEnd();
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(session);
			ObjectStubsQuery[] serializableQueries = new ObjectStubsQuery[queries.Length];
			bool[] forceList = new bool[queries.Length];
			for(int i = 0; i < queries.Length; i++) {
				serializableQueries[i] = new ObjectStubsQuery(queries[i], session, GetNestedParentGuidMap(session), ciCache);
				forceList[i] = queries[i].Force;
			}
			SerializableObjectLayerResult<XPObjectStubCollection[]> serializableResults = objectLayer.LoadObjects(ciCache.GetNewDictionaryStub(), serializableQueries);
			ciCache.UpdateClasses(serializableResults.Dictionary);
			return new NestedStubLoader(session, GetNestedParentGuidMap(session), ciCache).GetNestedObjects(serializableResults.Result, forceList);
		}
		public async Task<ICollection[]> LoadObjectsAsync(Session session, ObjectsQuery[] queries, CancellationToken cancellationToken = default(CancellationToken)) {
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(session);
			ObjectStubsQuery[] serializableQueries = new ObjectStubsQuery[queries.Length];
			bool[] forceList = new bool[queries.Length];
			for(int i = 0; i < queries.Length; i++) {
				serializableQueries[i] = new ObjectStubsQuery(queries[i], session, GetNestedParentGuidMap(session), ciCache);
				forceList[i] = queries[i].Force;
			}
			SerializableObjectLayerResult<XPObjectStubCollection[]> serializableResults = await Task.Run(() => {
				cancellationToken.ThrowIfCancellationRequested();
				SerializableObjectLayerResult<XPObjectStubCollection[]> result = objectLayer.LoadObjects(ciCache.GetNewDictionaryStub(), serializableQueries);
				ciCache.UpdateClasses(result.Dictionary);
				return result;
			}, cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			return await (new NestedStubLoader(session, GetNestedParentGuidMap(session), ciCache)).GetNestedObjectsAsync(serializableResults.Result, forceList, cancellationToken);
		}
		public object LoadObjectsAsync(Session session, ObjectsQuery[] queries, AsyncLoadObjectsCallback callback) {
			SynchronizationContext syncContext = AsyncOperationsHelper.CaptureSynchronizationContextOrFail();
			Guard.ArgumentNotNull(callback, nameof(callback));
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(session);
			bool[] forceList = new bool[queries.Length];
			ObjectStubsQuery[] serializableQueries = new ObjectStubsQuery[queries.Length];
			for(int i = 0; i < queries.Length; i++) {
				serializableQueries[i] = new ObjectStubsQuery(queries[i], session, GetNestedParentGuidMap(session), ciCache);
				forceList[i] = queries[i].Force;
			}
			return new AsyncRequest(syncContext, new AsyncRequestExec(delegate (AsyncRequest request) {
				try {
					SerializableObjectLayerResult<XPObjectStubCollection[]> serializableResults = objectLayer.LoadObjects(ciCache.GetNewDictionaryStub(), serializableQueries);
					ciCache.UpdateClasses(serializableResults.Dictionary);
					session.AsyncExecuteQueue.Invoke(syncContext, new SendOrPostCallback(delegate (object oo) {
						try {
							callback(new NestedStubLoader(session, GetNestedParentGuidMap(session), ciCache).GetNestedObjects(serializableResults.Result, forceList), null);
						}
						catch(Exception ex) {
							try {
								callback(null, ex);
							}
							catch(Exception) { }
						}
					}), null, true);
				}
				catch(Exception ex) {
					try {
						session.AsyncExecuteQueue.Invoke(syncContext, new SendOrPostCallback(delegate (object oo) {
							callback(null, ex);
						}), null, true);
					}
					catch(Exception) { }
				}
			})).Start(session.AsyncExecuteQueue);
		}
		public List<object[]> SelectData(Session session, ObjectsQuery query, CriteriaOperatorCollection properties, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria) {
			session.WaitForAsyncOperationEnd();
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(session);
			NestedParentGuidMap map = GetNestedParentGuidMap(session);
			ObjectStubsQuery serializableQuery = new ObjectStubsQuery(query, session, map, ciCache);
			CriteriaOperatorCollection readyProperties = PrepareProperties(session, properties, ciCache, map);
			CriteriaOperatorCollection readyGroupProperties = PrepareProperties(session, groupProperties, ciCache, map);
			CriteriaOperator readyGroupCriteria = XPObjectStubCriteriaGenerator.GetStubCriteria(session, map, ciCache, groupCriteria);
			object[][] result = objectLayer.SelectData(ciCache.GetNewDictionaryStub(), serializableQuery, readyProperties, readyGroupProperties, readyGroupCriteria);
			PrepareSelectDataResult(query.ClassInfo, properties, result);
			return new List<object[]>(result);
		}
		public async Task<List<object[]>> SelectDataAsync(Session session, ObjectsQuery query, CriteriaOperatorCollection properties, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria, CancellationToken cancellationToken = default(CancellationToken)) {
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(session);
			NestedParentGuidMap map = GetNestedParentGuidMap(session);
			ObjectStubsQuery serializableQuery = new ObjectStubsQuery(query, session, map, ciCache);
			CriteriaOperatorCollection readyProperties = PrepareProperties(session, properties, ciCache, map);
			CriteriaOperatorCollection readyGroupProperties = PrepareProperties(session, groupProperties, ciCache, map);
			CriteriaOperator readyGroupCriteria = XPObjectStubCriteriaGenerator.GetStubCriteria(session, map, ciCache, groupCriteria);
			object[][] result = await Task.Run(() => {
				cancellationToken.ThrowIfCancellationRequested();
				object[][] data = objectLayer.SelectData(ciCache.GetNewDictionaryStub(), serializableQuery, readyProperties, readyGroupProperties, readyGroupCriteria);
				PrepareSelectDataResult(query.ClassInfo, properties, data);
				return data;
			}, cancellationToken).ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();
			return new List<object[]>(result);
		}
		static void PrepareSelectDataResult(XPClassInfo classInfo, CriteriaOperatorCollection properties, object[][] result) {
			if(properties != null && properties.Count > 0) {
				for(int i = 0; i < properties.Count; i++) {
					OperandProperty property = properties[i] as OperandProperty;
					if((object)property == null) continue;
					var path = MemberInfoCollection.ParsePath(classInfo, property.PropertyName);
					ValueConverter converter;
					if(path.Count == 0 || (converter = path[path.Count - 1].Converter) == null) continue;
					for(int rowIndex = 0; rowIndex < result.Length; rowIndex++) {
						object[] row = result[rowIndex];
						row[i] = converter.ConvertFromStorageType(row[i]);
					}
				}
			}
		}
		static CriteriaOperatorCollection PrepareProperties(Session session, CriteriaOperatorCollection properties, XPObjectClassInfoStubCache classInfoCache, NestedParentGuidMap map) {
			CriteriaOperatorCollection readyProperties;
			if(properties == null || properties.Count == 0) {
				readyProperties = properties;
			}
			else {
				readyProperties = new CriteriaOperatorCollection();
				foreach(CriteriaOperator criteria in properties) {
					readyProperties.Add(XPObjectStubCriteriaGenerator.GetStubCriteria(session, map, classInfoCache, criteria));
				}
			}
			return readyProperties;
		}
		public object SelectDataAsync(Session session, ObjectsQuery query, CriteriaOperatorCollection properties, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria, AsyncSelectDataCallback callback) {
			SynchronizationContext syncContext = AsyncOperationsHelper.CaptureSynchronizationContextOrFail();
			Guard.ArgumentNotNull(callback, nameof(callback));
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(session);
			NestedParentGuidMap map = GetNestedParentGuidMap(session);
			ObjectStubsQuery serializableQuery = new ObjectStubsQuery(query, session, map, ciCache);
			CriteriaOperatorCollection readyProperties = PrepareProperties(session, properties, ciCache, map);
			CriteriaOperatorCollection readyGroupProperties = PrepareProperties(session, groupProperties, ciCache, map);
			CriteriaOperator readyGroupCriteria = XPObjectStubCriteriaGenerator.GetStubCriteria(session, map, ciCache, groupCriteria);
			return new AsyncRequest(syncContext, new AsyncRequestExec(delegate (AsyncRequest request) {
				try {
					object[][] result = objectLayer.SelectData(ciCache.GetNewDictionaryStub(), serializableQuery, readyProperties, readyGroupProperties, readyGroupCriteria);
					PrepareSelectDataResult(query.ClassInfo, properties, result);
					session.AsyncExecuteQueue.Invoke(syncContext, new SendOrPostCallback(delegate (object obj) {
						try {
							callback(new List<object[]>(result), null);
						}
						catch(Exception ex) {
							try {
								callback(null, ex);
							}
							catch(Exception) { }
						}
					}), null, true);
				}
				catch(Exception ex) {
					try {
						session.AsyncExecuteQueue.Invoke(syncContext, new SendOrPostCallback(delegate (object obj) {
							try {
								callback(null, ex);
							}
							catch(Exception) { }
						}), null, true);
					}
					catch(Exception) { }
				}
			})).Start(session.AsyncExecuteQueue);
		}
		public ICollection[] GetObjectsByKey(Session session, ObjectsByKeyQuery[] queries) {
			if(queries == null || queries.Length == 0) return Array.Empty<object[]>();
			session.WaitForAsyncOperationEnd();
			XPObjectStubCache objCache = new XPObjectStubCache();
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(session);
			bool[] forceList = new bool[queries.Length];
			GetObjectStubsByKeyQuery[] serializableQueries = new GetObjectStubsByKeyQuery[queries.Length];
			for(int i = 0; i < queries.Length; i++) {
				serializableQueries[i] = GetObjectStubsByKeyQuery.FromObjectQuery(queries[i], ciCache);
				forceList[i] = true;
			}
			SerializableObjectLayerResult<XPObjectStubCollection[]> serializableResults = objectLayer.GetObjectsByKey(ciCache.GetNewDictionaryStub(), serializableQueries);
			ciCache.UpdateClasses(serializableResults.Dictionary);
			return new NestedStubLoader(session, GetNestedParentGuidMap(session), ciCache).GetNestedObjects(serializableResults.Result, forceList);
		}
		public async Task<ICollection[]> GetObjectsByKeyAsync(Session session, ObjectsByKeyQuery[] queries, CancellationToken cancellationToken = default(CancellationToken)) {
			if(queries == null || queries.Length == 0) return Array.Empty<object[]>();
			XPObjectStubCache objCache = new XPObjectStubCache();
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(session);
			bool[] forceList = new bool[queries.Length];
			GetObjectStubsByKeyQuery[] serializableQueries = new GetObjectStubsByKeyQuery[queries.Length];
			for(int i = 0; i < queries.Length; i++) {
				serializableQueries[i] = GetObjectStubsByKeyQuery.FromObjectQuery(queries[i], ciCache);
				forceList[i] = true;
			}
			SerializableObjectLayerResult<XPObjectStubCollection[]> serializableResults = await Task.Run(() => {
				cancellationToken.ThrowIfCancellationRequested();
				SerializableObjectLayerResult<XPObjectStubCollection[]> results = objectLayer.GetObjectsByKey(ciCache.GetNewDictionaryStub(), serializableQueries);
				ciCache.UpdateClasses(results.Dictionary);
				return results;
			}, cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			return await (new NestedStubLoader(session, GetNestedParentGuidMap(session), ciCache)).GetNestedObjectsAsync(serializableResults.Result, forceList, cancellationToken);
		}
		ICollection FilterListForDelete(Session session, NestedParentGuidMap map, ICollection fullListForDelete) {
			if(fullListForDelete == null) return null;
			if(fullListForDelete.Count == 0) return fullListForDelete;
			List<object> result = new List<object>(fullListForDelete.Count / 2);
			foreach(object objToDelete in fullListForDelete) {
				if(session.IsNewObject(objToDelete)) {
					if(map.GetParent(objToDelete) == Guid.Empty) continue;
				}
				result.Add(objToDelete);
			}
			return result;
		}
		public void CommitChanges(Session session, ICollection fullListForDelete, ICollection completeListForSave) {
			NestedParentGuidMap map = GetNestedParentGuidMap(session);
			ICollection readyListForDelete = FilterListForDelete(session, map, fullListForDelete);
			SessionStateStack.Enter(session, SessionState.CommitChangesToDataLayerInner);
			CommitObjectStubsResult[] results;
			try {
				XPObjectStubCache objCache = new XPObjectStubCache();
				XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(session);
				XPObjectStubCollection serializableListForDelete = new XPObjectStubCollection(readyListForDelete.Count);
				XPObjectStubCollection serializableListForSave = new XPObjectStubCollection(completeListForSave.Count);
				foreach(object objectForDelete in readyListForDelete) {
					if(session.LockingOption == LockingOption.None) {
						serializableListForDelete.Add(NestedStubWorksHelper.CreateParentStubObjectJustWithIdentities(session, map, ciCache, objectForDelete));
					}
					else {
						serializableListForDelete.Add(NestedStubWorksHelper.CreateParentStubObject(session, map, objCache, ciCache, objectForDelete));
					}
				}
				foreach(object objectForSave in completeListForSave) {
					serializableListForSave.Add(NestedStubWorksHelper.CreateParentStubObject(session, map, objCache, ciCache, objectForSave));
				}
				results = objectLayer.CommitObjects(ciCache.GetNewDictionaryStub(), serializableListForDelete, serializableListForSave, session.LockingOption);
			}
			finally {
				SessionStateStack.Leave(session, SessionState.CommitChangesToDataLayerInner);
			}
			if(results != null) {
				SessionStateStack.Enter(session, SessionState.ApplyIdentities);
				try {
					int index = -1;
					foreach(object objToSave in completeListForSave) {
						index++;
						CommitObjectStubsResult objResult = results[index];
						if(objResult == null) continue;
						XPClassInfo ci = session.GetClassInfo(objToSave);
						if(objResult.Key != null) {
							ci.KeyProperty.SetValue(objToSave, objResult.Key);
							SessionIdentityMap.RegisterObject(session, objToSave, ci.KeyProperty.ExpandId(objResult.Key)); 
						}
						if(objResult.OptimisticLockField == null) continue;
						XPMemberInfo olf = ci.OptimisticLockField;
						if(olf == null) continue;
						olf.SetValue(objToSave, objResult.OptimisticLockField);
					}
				}
				finally {
					SessionStateStack.Leave(session, SessionState.ApplyIdentities);
				}
			}
			foreach(object obj in fullListForDelete) {
				if(!session.IsNewObject(obj))
					SessionIdentityMap.UnregisterObject(session, obj);
				map.KickOut(obj);
				IXPInvalidateableObject spoilableObject = obj as IXPInvalidateableObject;
				if(spoilableObject != null)
					spoilableObject.Invalidate();
			}
		}
		public async Task CommitChangesAsync(Session session, ICollection fullListForDelete, ICollection completeListForSave, CancellationToken cancellationToken = default(CancellationToken)) {
			NestedParentGuidMap map = GetNestedParentGuidMap(session);
			ICollection readyListForDelete = FilterListForDelete(session, map, fullListForDelete);
			int asyncOperationId = SessionStateStack.GetNewAsyncOperationId();
			await SessionStateStack.EnterAsync(session, SessionState.CommitChangesToDataLayerInner, asyncOperationId, cancellationToken);
			CommitObjectStubsResult[] results;
			try {
				XPObjectStubCache objCache = new XPObjectStubCache();
				XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(session);
				XPObjectStubCollection serializableListForDelete = new XPObjectStubCollection(readyListForDelete.Count);
				XPObjectStubCollection serializableListForSave = new XPObjectStubCollection(completeListForSave.Count);
				foreach(object objectForDelete in readyListForDelete) {
					if(session.LockingOption == LockingOption.None) {
						serializableListForDelete.Add(NestedStubWorksHelper.CreateParentStubObjectJustWithIdentities(session, map, ciCache, objectForDelete));
					}
					else {
						serializableListForDelete.Add(NestedStubWorksHelper.CreateParentStubObject(session, map, objCache, ciCache, objectForDelete));
					}
				}
				foreach(object objectForSave in completeListForSave) {
					serializableListForSave.Add(NestedStubWorksHelper.CreateParentStubObject(session, map, objCache, ciCache, objectForSave));
				}
				results = await Task.Run(() => {
					cancellationToken.ThrowIfCancellationRequested();
					return objectLayer.CommitObjects(ciCache.GetNewDictionaryStub(), serializableListForDelete, serializableListForSave, session.LockingOption);
				}, cancellationToken);
			}
			finally {
				SessionStateStack.Leave(session, SessionState.CommitChangesToDataLayerInner, asyncOperationId);
			}
			cancellationToken.ThrowIfCancellationRequested();
			if(results != null) {
				await SessionStateStack.EnterAsync(session, SessionState.ApplyIdentities, asyncOperationId, cancellationToken);
				try {
					int index = -1;
					foreach(object objToSave in completeListForSave) {
						index++;
						CommitObjectStubsResult objResult = results[index];
						if(objResult == null) continue;
						XPClassInfo ci = session.GetClassInfo(objToSave);
						if(objResult.Key != null) {
							ci.KeyProperty.SetValue(objToSave, objResult.Key);
							SessionIdentityMap.RegisterObject(session, objToSave, ci.KeyProperty.ExpandId(objResult.Key)); 
						}
						if(objResult.OptimisticLockField == null) continue;
						XPMemberInfo olf = ci.OptimisticLockField;
						if(olf == null) continue;
						olf.SetValue(objToSave, objResult.OptimisticLockField);
					}
				}
				finally {
					SessionStateStack.Leave(session, SessionState.ApplyIdentities, asyncOperationId);
				}
			}
			foreach(object obj in fullListForDelete) {
				if(!session.IsNewObject(obj))
					SessionIdentityMap.UnregisterObject(session, obj);
				map.KickOut(obj);
				IXPInvalidateableObject spoilableObject = obj as IXPInvalidateableObject;
				if(spoilableObject != null)
					spoilableObject.Invalidate();
			}
		}
		public object CommitChangesAsync(Session session, ICollection fullListForDelete, ICollection completeListForSave, AsyncCommitCallback callback) {
			SynchronizationContext syncContext = AsyncOperationsHelper.CaptureSynchronizationContextOrFail();
			Guard.ArgumentNotNull(callback, nameof(callback));
			NestedParentGuidMap map = GetNestedParentGuidMap(session);
			ICollection readyListForDelete = FilterListForDelete(session, map, fullListForDelete);
			XPObjectStubCache objCache = new XPObjectStubCache();
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(session);
			XPObjectStubCollection serializableListForDelete = new XPObjectStubCollection(readyListForDelete.Count);
			XPObjectStubCollection serializableListForSave = new XPObjectStubCollection(completeListForSave.Count);
			SessionStateStack.Enter(session, SessionState.CommitChangesToDataLayerInner);
			try {
				foreach(object objectForDelete in readyListForDelete) {
					serializableListForDelete.Add(NestedStubWorksHelper.CreateParentStubObjectJustWithIdentities(session, map, ciCache, objectForDelete));
				}
				foreach(object objectForSave in completeListForSave) {
					serializableListForSave.Add(NestedStubWorksHelper.CreateParentStubObject(session, map, objCache, ciCache, objectForSave));
				}
			}
			catch(Exception) {
				SessionStateStack.Leave(session, SessionState.CommitChangesToDataLayerInner);
				throw;
			}
			return new AsyncRequest(syncContext, new AsyncRequestExec(delegate (AsyncRequest request) {
				try {
					objectLayer.CommitObjects(ciCache.GetNewDictionaryStub(), serializableListForDelete, serializableListForSave, session.LockingOption);
					session.AsyncExecuteQueue.Invoke(syncContext, new SendOrPostCallback(delegate (object oo) {
						try {
							SessionStateStack.Leave(session, SessionState.CommitChangesToDataLayerInner);
							foreach(object obj in fullListForDelete) {
								if(!session.IsNewObject(obj))
									SessionIdentityMap.UnregisterObject(session, obj);
								map.KickOut(obj);
								IXPInvalidateableObject spoilableObject = obj as IXPInvalidateableObject;
								if(spoilableObject != null)
									spoilableObject.Invalidate();
							}
							callback(null);
						}
						catch(Exception ex) {
							try {
								callback(ex);
							}
							catch(Exception) { }
						}
					}), null, true);
				}
				catch(Exception ex) {
					session.AsyncExecuteQueue.Invoke(syncContext, new SendOrPostCallback(delegate (object oo) {
						try {
							SessionStateStack.Leave(session, SessionState.CommitChangesToDataLayerInner);
							callback(ex);
						}
						catch(Exception) { }
					}), null, true);
				}
			})).Start(session.AsyncExecuteQueue);
		}
		public void CreateObjectType(XPObjectType type) {
			objectLayer.CreateObjectType(type.AssemblyName, type.TypeName);
		}
		public Task CreateObjectTypeAsync(XPObjectType type, CancellationToken cancellationToken) {
			return Task.Run(() => {
				CreateObjectType(type);
			}, cancellationToken);
		}
		public PurgeResult Purge() {
			return objectLayer.Purge();
		}
		[Description("Indicates if an object layer can call the SerializableObjectLayerClient.LoadCollectionObjects method to load collection properties.")]
		[Browsable(false)]
		public bool CanLoadCollectionObjects {
			get { return objectLayer.CanLoadCollectionObjects; }
		}
		[Description("Indicates if an object layer can call the SerializableObjectLayerClient.LoadCollectionObjectsAsync method to asynchronously load collection properties.")]
		[Browsable(false)]
		public bool CanLoadCollectionObjectsAsynchronously {
			get { return objectLayer.CanLoadCollectionObjects; }
		}
		public object[] LoadCollectionObjects(Session session, XPMemberInfo refProperty, object ownerObject) {
			session.WaitForAsyncOperationEnd();
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(session);
			SerializableObjectLayerResult<XPObjectStubCollection> sResult = objectLayer.LoadCollectionObjects(ciCache.GetNewDictionaryStub(), refProperty.Name, NestedStubWorksHelper.CreateParentStubObjectJustWithIdentities(session, GetNestedParentGuidMap(session), ciCache, ownerObject));
			ciCache.UpdateClasses(sResult.Dictionary);
			ICollection result = new NestedStubLoader(session, GetNestedParentGuidMap(session), ciCache).GetNestedObjects(new XPObjectStubCollection[] { sResult.Result }, null)[0];
			return ListHelper.FromCollection(result).ToArray();
		}
		public async Task<object[]> LoadCollectionObjectsAsync(Session session, XPMemberInfo refProperty, object ownerObject, CancellationToken cancellationToken = default(CancellationToken)) {
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(session);
			SerializableObjectLayerResult<XPObjectStubCollection> sResult = await Task.Run(() => {
				cancellationToken.ThrowIfCancellationRequested();
				return objectLayer.LoadCollectionObjects(ciCache.GetNewDictionaryStub(), refProperty.Name, NestedStubWorksHelper.CreateParentStubObjectJustWithIdentities(session, GetNestedParentGuidMap(session), ciCache, ownerObject));
			}, cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			ciCache.UpdateClasses(sResult.Dictionary);
			NestedStubLoader nestedStubLoader = new NestedStubLoader(session, GetNestedParentGuidMap(session), ciCache);
			ICollection[] result = await nestedStubLoader.GetNestedObjectsAsync(new XPObjectStubCollection[] { sResult.Result }, null, cancellationToken).ConfigureAwait(false);
			return ListHelper.FromCollection(result[0]).ToArray();
		}
		[Description("Returns the current SerializableObjectLayerClient object.")]
		[Browsable(false)]
		public IObjectLayer ObjectLayer {
			get { return this; }
		}
		[Description("Gets an object providing metadata on persistent objects stored in a data store.")]
		[Browsable(false)]
		public XPDictionary Dictionary {
			get { return dictionary; }
		}
		public void SetObjectLayerWideObjectTypes(Dictionary<XPClassInfo, XPObjectType> loadedTypes) {
			SetObjectLayerWideData(loadedTypesKey, loadedTypes);
		}
		public Dictionary<XPClassInfo, XPObjectType> GetObjectLayerWideObjectTypes() {
			return (Dictionary<XPClassInfo, XPObjectType>)GetObjectLayerWideData(loadedTypesKey);
		}
		public void RegisterStaticTypes(params XPClassInfo[] types) {
			Dictionary<XPClassInfo, XPClassInfo> staticTypes = GetStaticTypesDictionary();
			foreach(XPClassInfo ci in types) {
				staticTypes[ci] = ci;
			}
		}
		public bool IsStaticType(XPClassInfo type) {
			return GetStaticTypesDictionary().ContainsKey(type);
		}
		public IObjectMap GetStaticCache(XPClassInfo info) {
			Dictionary<XPClassInfo, IObjectMap> staticCache = (Dictionary<XPClassInfo, IObjectMap>)GetObjectLayerWideData(staticCacheKey);
			if(staticCache == null) {
				staticCache = new Dictionary<XPClassInfo, IObjectMap>();
				SetObjectLayerWideData(staticCacheKey, staticCache);
			}
			IObjectMap cache;
			if(!staticCache.TryGetValue(info, out cache)) {
				cache = new StrongObjectMap();
				staticCache.Add(info, cache);
			}
			return cache;
		}
		readonly static object loadedTypesKey = new object();
		readonly static object staticTypesKey = new object();
		readonly static object staticCacheKey = new object();
		readonly Dictionary<object, object> staticData = new Dictionary<object, object>();
		protected void ClearStaticData() {
			staticData.Clear();
		}
		void SetObjectLayerWideData(object key, object data) {
			staticData[key] = data;
		}
		object GetObjectLayerWideData(object key) {
			object res;
			return staticData.TryGetValue(key, out res) ? res : null;
		}
		Dictionary<XPClassInfo, XPClassInfo> GetStaticTypesDictionary() {
			Dictionary<XPClassInfo, XPClassInfo> rv = (Dictionary<XPClassInfo, XPClassInfo>)GetObjectLayerWideData(staticTypesKey);
			if(rv == null) {
				rv = new Dictionary<XPClassInfo, XPClassInfo>();
				SetObjectLayerWideData(staticTypesKey, rv);
			}
			return rv;
		}
		sealed class StrongObjectMap : Dictionary<object, object>, IObjectMap {
			void IObjectMap.Add(object theObject, object id) {
				if(!ContainsKey(id)) {
					base.Add(id, theObject);
				}
				else {
					throw new ObjectCacheException(id, theObject, this[id]);
				}
			}
			void IObjectMap.Remove(object id) {
				Remove(id);
			}
			public object Get(object id) {
				object res;
				return TryGetValue(id, out res) ? res : null;
			}
			public int CompactCache() {
				return 0;
			}
			public void ClearCache() {
			}
		}
		public UpdateSchemaResult UpdateSchema(bool doNotCreateIfFirstTableNotExist, params XPClassInfo[] types) {
			return UpdateSchemaResult.SchemaExists;
		}
		public Task<UpdateSchemaResult> UpdateSchemaAsync(CancellationToken cancellationToken, bool doNotCreateIfFirstTableNotExist, params XPClassInfo[] types) {
			return Task.FromResult(UpdateSchemaResult.SchemaExists);
		}
		public object[] LoadDelayedProperties(Session session, object theObject, MemberPathCollection props) {
			ISerializableObjectLayerEx objectLayerEx = objectLayer as ISerializableObjectLayerEx;
			if(objectLayerEx == null) throw new InvalidOperationException(Res.GetString(Res.SerializableObjectLayer_OLDoesNotImplementISerializableObjectLayerEx));
			session.WaitForAsyncOperationEnd();
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(session);
			NestedParentGuidMap map = GetNestedParentGuidMap(session);
			XPObjectStub serializableObject = NestedStubWorksHelper.CreateParentStubObjectJustWithIdentities(session, map, ciCache, theObject);
			string[] propStrings = new string[props.Count];
			for(int i = 0; i < props.Count; i++) {
				propStrings[i] = props[i].ToString();
			}
			SerializableObjectLayerResult<object[]> sResult = objectLayerEx.LoadDelayedProperties(ciCache.GetNewDictionaryStub(), serializableObject, propStrings);
			ciCache.UpdateClasses(sResult.Dictionary);
			object[] results = sResult.Result;
			NestedStubLoader stubLoader = null;
			for(int i = 0; i < results.Length; i++) {
				XPObjectStub resStub = results[i] as XPObjectStub;
				if(resStub == null) {
					int count = props[i].Count;
					if(count > 0) {
						XPMemberInfo mi = props[i][count - 1];
						if(mi.Converter != null) {
							results[i] = mi.Converter.ConvertFromStorageType(results[i]);
						}
					}
					continue;
				}
				if(stubLoader == null) stubLoader = new NestedStubLoader(session, map, ciCache);
				results[i] = stubLoader.GetNestedObject(resStub);
			}
			return results;
		}
		public async Task<object[]> LoadDelayedPropertiesAsync(Session session, object theObject, MemberPathCollection props, CancellationToken cancellationToken = default(CancellationToken)) {
			ISerializableObjectLayerEx objectLayerEx = objectLayer as ISerializableObjectLayerEx;
			if(objectLayerEx == null) throw new InvalidOperationException(Res.GetString(Res.SerializableObjectLayer_OLDoesNotImplementISerializableObjectLayerEx));
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(session);
			NestedParentGuidMap map = GetNestedParentGuidMap(session);
			XPObjectStub serializableObject = NestedStubWorksHelper.CreateParentStubObjectJustWithIdentities(session, map, ciCache, theObject);
			string[] propStrings = new string[props.Count];
			for(int i = 0; i < props.Count; i++) {
				propStrings[i] = props[i].ToString();
			}
			SerializableObjectLayerResult<object[]> sResult = await Task.Run(() => {
				cancellationToken.ThrowIfCancellationRequested();
				return objectLayerEx.LoadDelayedProperties(ciCache.GetNewDictionaryStub(), serializableObject, propStrings);
			}, cancellationToken);
			ciCache.UpdateClasses(sResult.Dictionary);
			object[] results = sResult.Result;
			NestedStubLoader stubLoader = null;
			for(int i = 0; i < results.Length; i++) {
				XPObjectStub resStub = results[i] as XPObjectStub;
				if(resStub == null) {
					int count = props[i].Count;
					if(count > 0) {
						XPMemberInfo mi = props[i][count - 1];
						if(mi.Converter != null) {
							results[i] = mi.Converter.ConvertFromStorageType(results[i]);
						}
					}
					continue;
				}
				if(stubLoader == null) stubLoader = new NestedStubLoader(session, map, ciCache);
				results[i] = stubLoader.GetNestedObject(resStub);
			}
			return results;
		}
		public ObjectDictionary<object> LoadDelayedProperties(Session session, IList objects, XPMemberInfo property) {
			ISerializableObjectLayerEx objectLayerEx = objectLayer as ISerializableObjectLayerEx;
			if(objectLayerEx == null) throw new InvalidOperationException(Res.GetString(Res.SerializableObjectLayer_OLDoesNotImplementISerializableObjectLayerEx));
			session.WaitForAsyncOperationEnd();
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(session);
			XPObjectStubCollection serializableObjects = new XPObjectStubCollection(objects.Count);
			NestedParentGuidMap map = GetNestedParentGuidMap(session);
			for(int i = 0; i < objects.Count; i++) {
				serializableObjects.Add(NestedStubWorksHelper.CreateParentStubObjectJustWithIdentities(session, map, ciCache, objects[i]));
			}
			SerializableObjectLayerResult<object[]> sResult = objectLayerEx.LoadDelayedProperties(ciCache.GetNewDictionaryStub(), serializableObjects, property.Name);
			ciCache.UpdateClasses(sResult.Dictionary);
			object[] results = sResult.Result;
			NestedStubLoader stubLoader = null;
			ObjectDictionary<object> resultDict = new ObjectDictionary<object>(results.Length);
			for(int i = 0; i < results.Length; i++) {
				XPObjectStub resStub = results[i] as XPObjectStub;
				if(resStub != null) {
					if(stubLoader == null) stubLoader = new NestedStubLoader(session, map, ciCache);
					results[i] = stubLoader.GetNestedObject(resStub);
				}
				else if(property.Converter != null) {
					results[i] = property.Converter.ConvertFromStorageType(results[i]);
				}
				resultDict.Add(objects[i], results[i]);
			}
			return resultDict;
		}
		public async Task<ObjectDictionary<object>> LoadDelayedPropertiesAsync(Session session, IList objects, XPMemberInfo property, CancellationToken cancellationToken = default(CancellationToken)) {
			ISerializableObjectLayerEx objectLayerEx = objectLayer as ISerializableObjectLayerEx;
			if(objectLayerEx == null) throw new InvalidOperationException(Res.GetString(Res.SerializableObjectLayer_OLDoesNotImplementISerializableObjectLayerEx));
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(session);
			XPObjectStubCollection serializableObjects = new XPObjectStubCollection(objects.Count);
			NestedParentGuidMap map = GetNestedParentGuidMap(session);
			for(int i = 0; i < objects.Count; i++) {
				serializableObjects.Add(NestedStubWorksHelper.CreateParentStubObjectJustWithIdentities(session, map, ciCache, objects[i]));
			}
			SerializableObjectLayerResult<object[]> sResult = await Task.Run(() => {
				cancellationToken.ThrowIfCancellationRequested();
				return objectLayerEx.LoadDelayedProperties(ciCache.GetNewDictionaryStub(), serializableObjects, property.Name);
			}, cancellationToken);
			ciCache.UpdateClasses(sResult.Dictionary);
			object[] results = sResult.Result;
			NestedStubLoader stubLoader = null;
			ObjectDictionary<object> resultDict = new ObjectDictionary<object>(results.Length);
			for(int i = 0; i < results.Length; i++) {
				XPObjectStub resStub = results[i] as XPObjectStub;
				if(resStub != null) {
					if(stubLoader == null) stubLoader = new NestedStubLoader(session, map, ciCache);
					results[i] = stubLoader.GetNestedObject(resStub);
				}
				else if(property.Converter != null) {
					results[i] = property.Converter.ConvertFromStorageType(results[i]);
				}
				resultDict.Add(objects[i], results[i]);
			}
			return resultDict;
		}
		[Description("Returns an AutoCreateOption value associated with the current object layer.")]
		[Browsable(false)]
		public AutoCreateOption AutoCreateOption {
			get { return AutoCreateOption.None; }
		}
		public event SchemaInitEventHandler SchemaInit {
			add { throw new NotSupportedException(); }
			remove { throw new NotSupportedException(); }
		}
		[Description("Provides access to the current object layer’s IDbConnection object that is used to access a database.")]
		[Browsable(false)]
		public IDbConnection Connection {
			get { return null; }
		}
#if DEBUGTEST
		public IDbCommand CreateCommand() {
			throw new NotSupportedException();
		}
#endif
		[Description("Provides access to the current object layer’s data access layer which is used to access a data store.")]
		[Browsable(false)]
		public IDataLayer DataLayer {
			get { return null; }
		}
		public bool IsParentObjectToSave(Session session, object theObject) {
			ISerializableObjectLayerEx objectLayerEx = objectLayer as ISerializableObjectLayerEx;
			if(objectLayerEx == null) throw new InvalidOperationException(Res.GetString(Res.SerializableObjectLayer_OLDoesNotImplementISerializableObjectLayerEx));
			if(theObject == null) return false;
			session.WaitForAsyncOperationEnd();
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(session);
			return objectLayerEx.IsParentObjectToSave(ciCache.GetNewDictionaryStub(), NestedStubWorksHelper.CreateParentStubObjectJustWithIdentities(session, GetNestedParentGuidMap(session), ciCache, theObject));
		}
		public bool IsParentObjectToDelete(Session session, object theObject) {
			ISerializableObjectLayerEx objectLayerEx = objectLayer as ISerializableObjectLayerEx;
			if(objectLayerEx == null) throw new InvalidOperationException(Res.GetString(Res.SerializableObjectLayer_OLDoesNotImplementISerializableObjectLayerEx));
			if(theObject == null) return false;
			session.WaitForAsyncOperationEnd();
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(session);
			return objectLayerEx.IsParentObjectToDelete(ciCache.GetNewDictionaryStub(), NestedStubWorksHelper.CreateParentStubObjectJustWithIdentities(session, GetNestedParentGuidMap(session), ciCache, theObject));
		}
		public ICollection GetParentObjectsToSave(Session session) {
			ISerializableObjectLayerEx objectLayerEx = objectLayer as ISerializableObjectLayerEx;
			if(objectLayerEx == null) throw new InvalidOperationException(Res.GetString(Res.SerializableObjectLayer_OLDoesNotImplementISerializableObjectLayerEx));
			session.WaitForAsyncOperationEnd();
			SerializableObjectLayerResult<XPObjectStubCollection> sObjectsToSave = objectLayerEx.GetParentObjectsToSave();
			return new NestedStubLoader(session, GetNestedParentGuidMap(session), new XPObjectClassInfoStubCache(session, sObjectsToSave.Dictionary)).GetNestedObjects(new XPObjectStubCollection[] { sObjectsToSave.Result }, null)[0];
		}
		public ICollection GetParentObjectsToDelete(Session session) {
			ISerializableObjectLayerEx objectLayerEx = objectLayer as ISerializableObjectLayerEx;
			if(objectLayerEx == null) throw new InvalidOperationException(Res.GetString(Res.SerializableObjectLayer_OLDoesNotImplementISerializableObjectLayerEx));
			session.WaitForAsyncOperationEnd();
			SerializableObjectLayerResult<XPObjectStubCollection> sObjectsToDelete = objectLayerEx.GetParentObjectsToDelete();
			return new NestedStubLoader(session, GetNestedParentGuidMap(session), new XPObjectClassInfoStubCache(session, sObjectsToDelete.Dictionary)).GetNestedObjects(new XPObjectStubCollection[] { sObjectsToDelete.Result }, null)[0];
		}
		public ICollection GetParentTouchedClassInfos(Session session) {
			ISerializableObjectLayerEx objectLayerEx = objectLayer as ISerializableObjectLayerEx;
			if(objectLayerEx == null) throw new InvalidOperationException(Res.GetString(Res.SerializableObjectLayer_OLDoesNotImplementISerializableObjectLayerEx));
			session.WaitForAsyncOperationEnd();
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(session);
			string[] serializableResult = objectLayerEx.GetParentTouchedClassInfos();
			XPClassInfo[] result = new XPClassInfo[serializableResult.Length];
			for(int i = 0; i < serializableResult.Length; i++) {
				result[i] = ciCache.GetClassInfo(serializableResult[i]);
			}
			return result;
		}
		object ICommandChannel.Do(string command, object args) {
			if(nestedCommandChannel == null) {
				if(objectLayer == null) {
					throw new NotSupportedException(string.Format(CommandChannelHelper.Message_CommandIsNotSupported, command));
				}
				else {
					throw new NotSupportedException(string.Format(CommandChannelHelper.Message_CommandIsNotSupportedEx, command, objectLayer.GetType().FullName));
				}
			}
			return nestedCommandChannel.Do(command, args);
		}
		Task<object> ICommandChannelAsync.DoAsync(string command, object args, CancellationToken cancellationToken) {
			if(nestedCommandChannelAsync == null) {
				if(nestedCommandChannel == null) {
					throw new NotSupportedException(string.Format(CommandChannelHelper.Message_CommandIsNotSupported, command));
				}
				else {
					throw new InvalidOperationException(Xpo.Res.GetString(Xpo.Res.Async_CommandChannelDoesNotImplementICommandChannelAsync, nestedCommandChannel.GetType().FullName));
				}
			}
			return nestedCommandChannelAsync.DoAsync(command, args, cancellationToken);
		}
		ISecuredPropertyAccessor IInfrastructure<ISecuredPropertyAccessor>.Instance {
			get {
#if NET
				if(SerializableObjectLayer is IInfrastructure<ISecuredPropertyAccessor> infrastructure) {
					return infrastructure.Instance;
				}
#endif
				return null;
			}
		}
	}
	public class SerializableObjectLayer : ISerializableObjectLayer, ISerializableObjectLayerEx, ICommandChannel, ICommandChannelAsync {
		readonly Session parentSession;
		readonly NestedGuidParentMap map;
		readonly ICommandChannel nestedCommandChannel;
		readonly ICommandChannelAsync nestedCommandChannelAsync;
		public readonly bool ThroughCommitMode;
		[Description("")]
		[Browsable(false)]
		public Session ParentSession { get { return parentSession; } }
#if DEBUGTEST
		[Description("")]
		[Browsable(false)]
		public NestedGuidParentMap Map { get { return map; } }
#endif
		public SerializableObjectLayer(Session parentSession) {
			this.parentSession = parentSession;
			this.nestedCommandChannel = parentSession as ICommandChannel;
			this.nestedCommandChannelAsync = parentSession as ICommandChannelAsync;
			if(parentSession.GetIdentityMapBehavior() == IdentityMapBehavior.Weak) {
				map = new WeakNestedGuidParentMap();
			}
			else {
				map = new StrongNestedGuidParentMap();
			}
		}
		public SerializableObjectLayer(Session parentSession, bool throughCommitMode)
			: this(parentSession) {
			this.ThroughCommitMode = throughCommitMode;
		}
		public SerializableObjectLayerResult<XPObjectStubCollection[]> LoadObjects(XPDictionaryStub dictionary, ObjectStubsQuery[] queries) {
			if(queries == null) return null;
			XPObjectStubCache objCache = new XPObjectStubCache();
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(parentSession, dictionary);
			ObjectsQuery[] sessionQueries = new ObjectsQuery[queries.Length];
			for(int i = 0; i < queries.Length; i++) {
				sessionQueries[i] = new ObjectsQuery(ciCache.GetClassInfo(queries[i].ClassInfo), XPObjectStubCriteriaGenerator.GetSessionCriteria(parentSession, map, ciCache, queries[i].Criteria),
					queries[i].Sorting, queries[i].SkipSelectedRecords, queries[i].TopSelectedRecords,
					new CollectionCriteriaPatcher(queries[i].SelectDeleted, parentSession.TypesManager), queries[i].Force);
			}
			ICollection[] sessionResults = parentSession.GetObjects(sessionQueries);
			XPObjectStubCollection[] results = new XPObjectStubCollection[sessionResults.Length];
			for(int i = 0; i < sessionResults.Length; i++) {
				ICollection sessionCollection = sessionResults[i];
				XPObjectStubCollection collection = new XPObjectStubCollection(sessionCollection.Count);
				foreach(object obj in sessionCollection) {
					XPObjectStub objStub = NestedStubWorksHelper.CreateNestedStubObject(parentSession, map, objCache, ciCache, obj);
					collection.Add(objStub);
				}
				results[i] = collection;
			}
			return new SerializableObjectLayerResult<XPObjectStubCollection[]>(ciCache.GetNewDictionaryStub(), results);
		}
		public CommitObjectStubsResult[] CommitObjects(XPDictionaryStub dictionary, XPObjectStubCollection objectsForDelete, XPObjectStubCollection objectsForSave, LockingOption lockingOption) {
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(parentSession, dictionary);
			ObjectSet lockedParentsObjects = new ObjectSet();
			XPClassInfo[] classInfoForDelete = new XPClassInfo[objectsForDelete.Count];
			XPClassInfo[] classInfoForSave = new XPClassInfo[objectsForSave.Count];
			for(int i = 0; i < objectsForDelete.Count; i++) {
				classInfoForDelete[i] = ciCache.GetClassInfo(objectsForDelete[i].ClassName);
			}
			for(int i = 0; i < objectsForSave.Count; i++) {
				classInfoForSave[i] = ciCache.GetClassInfo(objectsForSave[i].ClassName);
			}
			if(lockingOption != LockingOption.None) {
				NestedStubWorksHelper.ValidateVersions(parentSession, map, ciCache, lockedParentsObjects, classInfoForDelete, objectsForDelete, lockingOption, true);
				NestedStubWorksHelper.ValidateVersions(parentSession, map, ciCache, lockedParentsObjects, classInfoForSave, objectsForSave, lockingOption, false);
			}
			CommitChangesMode mode;
			if(!parentSession.InTransaction) {
				if(parentSession.IsUnitOfWork)
					mode = CommitChangesMode.NotInTransactionUnitOfWork;
				else
					mode = CommitChangesMode.NotInTransactionSession;
				parentSession.BeginTransaction();
			}
			else
				mode = CommitChangesMode.InTransaction;
			SessionStateStack.Enter(parentSession, SessionState.ReceivingObjectsFromNestedUow);
			switch(mode) {
				case CommitChangesMode.InTransaction:
				case CommitChangesMode.NotInTransactionUnitOfWork:
					Commit(ciCache, objectsForDelete, objectsForSave, classInfoForDelete, classInfoForSave);
					break;
				case CommitChangesMode.NotInTransactionSession: {
					try {
						Commit(ciCache, objectsForDelete, objectsForSave, classInfoForDelete, classInfoForSave);
						parentSession.CommitTransaction();
					}
					catch(Exception e) {
						if(parentSession.InTransaction) {
							try {
								parentSession.RollbackTransaction();
							}
							catch(Exception e2) {
								throw new ExceptionBundleException(e, e2);
							}
						}
						throw;
					}
				}
				break;
			}
			GC.KeepAlive(lockedParentsObjects);
			if(ThroughCommitMode && (mode == CommitChangesMode.NotInTransactionUnitOfWork || mode == CommitChangesMode.InTransaction)) {
				if(parentSession.InTransaction) {
					parentSession.CommitTransaction();
				}
				CommitObjectStubsResult[] results = null;
				object[] parentObjectsForSave = NestedStubWorksHelper.GetParentObjects(parentSession, map, classInfoForSave, objectsForSave); ;
				for(int i = 0; i < objectsForSave.Count; i++) {
					XPObjectStub obj = objectsForSave[i];
					XPClassInfo ci = classInfoForSave[i];
					object parentObj = parentObjectsForSave[i];
					if(parentObj == null) continue;
					object key = obj.IsNew ? ci.KeyProperty.GetValue(parentObj) : null;
					object olf = ci.OptimisticLockField == null ? null : ci.OptimisticLockField.GetValue(parentObj);
					if(key != null || olf != null) {
						if(results == null) results = new CommitObjectStubsResult[objectsForSave.Count];
						results[i] = new CommitObjectStubsResult(key, olf);
					}
				}
				return results;
			}
			return null;
		}
		void Commit(XPObjectClassInfoStubCache ciCache, XPObjectStubCollection objectsForDelete, XPObjectStubCollection objectsForSave, XPClassInfo[] classInfoForDelete, XPClassInfo[] classInfoForSave) {
			List<object> parentsToSave = new List<object>();
			bool needToLeave = true;
			try {
				NestedStubWorksHelper.CommitDeletedObjects(parentSession, map, ciCache, classInfoForDelete, objectsForDelete);
				parentsToSave.AddRange(NestedStubWorksHelper.CreateParentObjects(parentSession, map, classInfoForSave, objectsForSave));
				if(IsAuditEnabled(parentSession)) {
					SessionStateStack.Leave(parentSession, SessionState.ReceivingObjectsFromNestedUow);
					needToLeave = false;
				}
				for(int i = 0; i < objectsForSave.Count; i++) {
					NestedStubWorksHelper.CommitObjectProperties(parentSession, map, ciCache, classInfoForSave[i], objectsForSave[i], parentsToSave[i]);
				}
				parentSession.Save(parentsToSave);
			}
			finally {
				if(needToLeave) {
					SessionStateStack.Leave(parentSession, SessionState.ReceivingObjectsFromNestedUow);
				}
			}
			parentSession.TriggerObjectsLoaded(parentsToSave);
		}
		public void CreateObjectType(string assemblyName, string typeName) {
			parentSession.ObjectLayer.CreateObjectType(new XPObjectType(parentSession.Dictionary, assemblyName, typeName));
		}
		public SerializableObjectLayerResult<XPObjectStubCollection[]> GetObjectsByKey(XPDictionaryStub dictionary, GetObjectStubsByKeyQuery[] queries) {
			XPObjectStubCache objCache = new XPObjectStubCache();
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(parentSession, dictionary);
			ObjectsByKeyQuery[] sessionQueries = new ObjectsByKeyQuery[queries.Length];
			for(int i = 0; i < queries.Length; i++) {
				sessionQueries[i] = new ObjectsByKeyQuery(ciCache.GetClassInfo(queries[i].ClassInfo), queries[i].IdCollection);
			}
			ICollection[] sessionResults = parentSession.GetObjectsByKey(sessionQueries, false);
			XPObjectStubCollection[] results = new XPObjectStubCollection[sessionResults.Length];
			for(int i = 0; i < sessionResults.Length; i++) {
				ICollection sessionCollection = sessionResults[i];
				XPObjectStubCollection collection = new XPObjectStubCollection(sessionCollection.Count);
				foreach(object obj in sessionCollection) {
					ObjectRetrievedFromParentSession(obj);
					XPObjectStub objStub = NestedStubWorksHelper.CreateNestedStubObject(parentSession, map, objCache, ciCache, obj);
					collection.Add(objStub);
				}
				results[i] = collection;
			}
			return new SerializableObjectLayerResult<XPObjectStubCollection[]>(ciCache.GetNewDictionaryStub(), results);
		}
		public object[][] SelectData(XPDictionaryStub dictionary, ObjectStubsQuery query, CriteriaOperatorCollection properties, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria) {
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(parentSession, dictionary);
			XPClassInfo xpClassInfo = ciCache.GetClassInfo(query.ClassInfo);
			CriteriaOperator criteria = XPObjectStubCriteriaGenerator.GetSessionCriteria(parentSession, map, ciCache, query.Criteria);
			SortingCollection sorting = query.Sorting;
			SelectingData(xpClassInfo, ref properties, ref criteria, ref groupProperties, ref groupCriteria, ref sorting);
			List<object[]> result = parentSession.SelectData(xpClassInfo, properties, criteria, groupProperties, groupCriteria, query.SelectDeleted, query.SkipSelectedRecords, query.TopSelectedRecords, sorting);
			SelectedData(properties, ref result);
			if(properties != null && properties.Count > 0) {
				for(int i = 0; i < properties.Count; i++) {
					OperandProperty property = properties[i] as OperandProperty;
					if((object)property == null) continue;
					var path = MemberInfoCollection.ParsePath(xpClassInfo, property.PropertyName);
					ValueConverter converter;
					if(path.Count == 0 || (converter = path[path.Count - 1].Converter) == null) continue;
					for(int rowIndex = 0; rowIndex < result.Count; rowIndex++) {
						object[] row = result[rowIndex];
						row[i] = converter.ConvertToStorageType(row[i]);
					}
				}
			}
			return result.ToArray();
		}
		protected virtual void SelectingData(XPClassInfo classInfo, ref CriteriaOperatorCollection properties, ref CriteriaOperator criteria, ref CriteriaOperatorCollection groupProperties, ref CriteriaOperator groupCriteria, ref SortingCollection sorting) { }
		protected virtual void SelectedData(IList<CriteriaOperator> properties, ref List<object[]> data) { }
		protected virtual bool IsAuditEnabled(Session session) {
			return false;
		}
		[Description("Indicates if an object layer can call the SerializableObjectLayer.LoadCollectionObjects method to load collection properties.")]
		[Browsable(false)]
		public bool CanLoadCollectionObjects {
			get { return true; }
		}
		public SerializableObjectLayerResult<XPObjectStubCollection> LoadCollectionObjects(XPDictionaryStub dictionary, string refPropertyName, XPObjectStub ownerObject) {
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(parentSession, dictionary);
			XPObjectStubCache objCache = new XPObjectStubCache();
			XPClassInfo ci = ciCache.GetClassInfo(ownerObject.ClassName);
			object parent = NestedStubWorksHelper.GetParentObject(parentSession, map, ci, ownerObject);
			if(parent == null) {
				return new SerializableObjectLayerResult<XPObjectStubCollection>(null, new XPObjectStubCollection(0));
			}
			else {
				XPMemberInfo refProperty = ci.FindMember(refPropertyName);
				XPBaseCollection parentCollection = (XPBaseCollection)refProperty.GetValue(parent);
				parentCollection.Load();
				XPObjectStubCollection collection = new XPObjectStubCollection(parentCollection.Count);
				foreach(object obj in parentCollection.Helper.IntObjList) {
					ObjectRetrievedFromParentSession(obj);
					collection.Add(NestedStubWorksHelper.CreateNestedStubObject(parentSession, map, objCache, ciCache, obj));
				}
				return new SerializableObjectLayerResult<XPObjectStubCollection>(ciCache.GetNewDictionaryStub(), collection);
			}
		}
		protected virtual void ObjectRetrievedFromParentSession(object obj) { }
		public PurgeResult Purge() {
			return parentSession.PurgeDeletedObjects();
		}
		public SerializableObjectLayerResult<object[]> LoadDelayedProperties(XPDictionaryStub dictionary, XPObjectStub theObject, string[] props) {
			XPObjectStubCache objCache = new XPObjectStubCache();
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(parentSession, dictionary);
			XPClassInfo ci = ciCache.GetClassInfo(theObject.ClassName);
			object obj = NestedStubWorksHelper.GetParentObject(parentSession, map, ci, theObject);
			object[] result = new object[props.Length];
			for(int i = 0; i < props.Length; i++) {
				XPMemberInfo mi = ci.FindMember(props[i]);
				object value = mi.GetValue(obj);
				if(mi.ReferenceType != null && value != null) {
					ObjectRetrievedFromParentSession(value);
					result[i] = NestedStubWorksHelper.CreateNestedStubObject(parentSession, map, objCache, ciCache, value);
					continue;
				}
				if(mi.Converter != null) {
					value = mi.Converter.ConvertToStorageType(value);
				}
				result[i] = value;
			}
			return new SerializableObjectLayerResult<object[]>(ciCache.GetNewDictionaryStub(), result);
		}
		public SerializableObjectLayerResult<object[]> LoadDelayedProperties(XPDictionaryStub dictionary, XPObjectStubCollection objects, string property) {
			XPObjectStubCache objCache = new XPObjectStubCache();
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(parentSession, dictionary);
			object[] result = new object[objects.Count];
			XPClassInfo[] classInfoList = new XPClassInfo[objects.Count];
			for(int i = 0; i < objects.Count; i++) {
				classInfoList[i] = ciCache.GetClassInfo(objects[i].ClassName);
			}
			object[] parentList = NestedStubWorksHelper.GetParentObjects(parentSession, map, classInfoList, objects);
			for(int i = 0; i < objects.Count; i++) {
				XPClassInfo ci = classInfoList[i];
				object obj = parentList[i];
				XPMemberInfo mi = ci.FindMember(property);
				object value = mi.GetValue(obj);
				if(mi.ReferenceType != null && value != null) {
					ObjectRetrievedFromParentSession(value);
					result[i] = NestedStubWorksHelper.CreateNestedStubObject(parentSession, map, objCache, ciCache, value);
					continue;
				}
				if(mi.Converter != null) {
					value = mi.Converter.ConvertToStorageType(value);
				}
				result[i] = value;
			}
			return new SerializableObjectLayerResult<object[]>(ciCache.GetNewDictionaryStub(), result);
		}
		[Description("Returns the current SerializableObjectLayer object.")]
		[Browsable(false)]
		public ISerializableObjectLayer ObjectLayer {
			get { return this; }
		}
		public bool IsParentObjectToSave(XPDictionaryStub dictionary, XPObjectStub theObject) {
			if(theObject == null) return false;
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(parentSession, dictionary);
			object obj = NestedStubWorksHelper.GetParentObject(parentSession, map, ciCache.GetClassInfo(theObject.ClassName), theObject);
			if(obj == null) return false;
			return parentSession.IsObjectToSave(obj, true);
		}
		public bool IsParentObjectToDelete(XPDictionaryStub dictionary, XPObjectStub theObject) {
			if(theObject == null) return false;
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(parentSession, dictionary);
			object obj = NestedStubWorksHelper.GetParentObject(parentSession, map, ciCache.GetClassInfo(theObject.ClassName), theObject);
			if(obj == null) return false;
			return parentSession.IsObjectToDelete(obj, true);
		}
		public SerializableObjectLayerResult<XPObjectStubCollection> GetParentObjectsToSave() {
			ICollection sessionResult = parentSession.GetObjectsToSave(true);
			XPObjectStubCache objCache = new XPObjectStubCache();
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(parentSession);
			XPObjectStubCollection result = new XPObjectStubCollection(sessionResult.Count);
			foreach(object obj in sessionResult) {
				result.Add(NestedStubWorksHelper.CreateNestedStubObject(parentSession, map, objCache, ciCache, obj));
			}
			return new SerializableObjectLayerResult<XPObjectStubCollection>(ciCache.GetNewDictionaryStub(), result);
		}
		public SerializableObjectLayerResult<XPObjectStubCollection> GetParentObjectsToDelete() {
			ICollection sessionResult = parentSession.GetObjectsToDelete(true);
			XPObjectStubCache objCache = new XPObjectStubCache();
			XPObjectClassInfoStubCache ciCache = new XPObjectClassInfoStubCache(parentSession);
			XPObjectStubCollection result = new XPObjectStubCollection(sessionResult.Count);
			foreach(object obj in sessionResult) {
				result.Add(NestedStubWorksHelper.CreateNestedStubObject(parentSession, map, objCache, ciCache, obj));
			}
			return new SerializableObjectLayerResult<XPObjectStubCollection>(ciCache.GetNewDictionaryStub(), result);
		}
		public string[] GetParentTouchedClassInfos() {
			ICollection sessionResult = parentSession.GetTouchedClassInfosIncludeParent();
			string[] result = new string[sessionResult.Count];
			int i = 0;
			foreach(XPClassInfo ci in sessionResult) {
				result[i++] = ci.FullName;
			}
			return result;
		}
		object ICommandChannel.Do(string command, object args) {
			if(nestedCommandChannel == null) {
				if(parentSession == null) {
					throw new NotSupportedException(string.Format(CommandChannelHelper.Message_CommandIsNotSupported, command));
				}
				else {
					throw new NotSupportedException(string.Format(CommandChannelHelper.Message_CommandIsNotSupportedEx, command, parentSession.GetType().FullName));
				}
			}
			return nestedCommandChannel.Do(command, args);
		}
		Task<object> ICommandChannelAsync.DoAsync(string command, object args, CancellationToken cancellationToken) {
			if(nestedCommandChannelAsync == null) {
				if(parentSession == null) {
					throw new NotSupportedException(string.Format(CommandChannelHelper.Message_CommandIsNotSupported, command));
				}
				else {
					throw new InvalidOperationException(Xpo.Res.GetString(Xpo.Res.Async_CommandChannelDoesNotImplementICommandChannelAsync, nestedCommandChannel.GetType().FullName));
				}
			}
			return nestedCommandChannelAsync.DoAsync(command, args, cancellationToken);
		}
	}
	[Serializable]
	[XmlType("GetObjectsByKeyQuery")]
	[XmlInclude(typeof(IdList))]
	public class GetObjectStubsByKeyQuery {
		XPClassInfoStub classInfo;
		ICollection idCollection;
		[Description("This member supports the internal infrastructure and is not intended to be used directly from your code.")]
		public XPClassInfoStub ClassInfo { get { return classInfo; } set { classInfo = value; } }
		[Browsable(false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		[XmlArrayItem("id")]
		public object[] IdArray {
			get { return (idCollection == null || idCollection.Count == 0) ? null : ListHelper.FromCollection(idCollection).ToArray(); }
			set { idCollection = value == null ? Array.Empty<object>() : value; }
		}
		[Description("This member supports the internal infrastructure and is not intended to be used directly from your code."), XmlIgnore]
		public ICollection IdCollection { get { return idCollection; } }
		public GetObjectStubsByKeyQuery() {
			this.idCollection = Array.Empty<object>();
		}
		public GetObjectStubsByKeyQuery(XPClassInfoStub classInfo, ICollection idCollection) {
			this.classInfo = classInfo;
			this.idCollection = idCollection;
		}
		public static GetObjectStubsByKeyQuery FromObjectQuery(ObjectsByKeyQuery query, XPObjectClassInfoStubCache classInfoCache) {
			return new GetObjectStubsByKeyQuery(classInfoCache.GetStub(query.ClassInfo), query.IdCollection);
		}
	}
	[Serializable]
	[XmlType("CommitObjectsResult")]
	public class CommitObjectStubsResult {
		object key;
		object olf;
		[Description("This member supports the internal infrastructure and is not intended to be used directly from your code.")]
		public object Key {
			get { return key; }
			set { key = value; }
		}
		[Description("This member supports the internal infrastructure and is not intended to be used directly from your code.")]
		public object OptimisticLockField {
			get { return olf; }
			set { olf = value; }
		}
		public CommitObjectStubsResult() { }
		public CommitObjectStubsResult(object key, object optimisticLockField) {
			this.key = key;
			this.olf = optimisticLockField;
		}
	}
	class XPObjectStubEqualsComparerByGuid : IEqualityComparer<XPObjectStub> {
		public bool Equals(XPObjectStub x, XPObjectStub y) {
			return x.Guid.Equals(y.Guid);
		}
		public int GetHashCode(XPObjectStub obj) {
			return obj.Guid.GetHashCode();
		}
	}
}
