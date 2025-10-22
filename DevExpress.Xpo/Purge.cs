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
using DevExpress.Xpo.DB;
using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using DevExpress.Xpo.Metadata;
using DevExpress.Xpo.Metadata.Helpers;
using DevExpress.Xpo.Generators;
using System.Collections.Generic;
using DevExpress.Compatibility.System;
using DevExpress.Utils;
namespace DevExpress.Xpo.Helpers {
	public class PurgerObjectUid {
		public readonly XPClassInfo IdClass;
		public readonly object Key;
		public PurgerObjectUid(XPClassInfo classInfo, object key) {
			this.IdClass = classInfo.IdClass;
			this.Key = key;
		}
		public override int GetHashCode() {
			return Key.GetHashCode();
		}
		public override bool Equals(object obj) {
			PurgerObjectUid another = obj as PurgerObjectUid;
			if(another == null)
				return false;
			return Equals(this.IdClass, another.IdClass) && Equals(this.Key, another.Key);
		}
	}
	public class PurgerObjectRecord {
		public readonly Purger Owner;
		public readonly XPClassInfo ClassInfo;
		public readonly object Key;
		List<object> _ReferencedObjects;
		List<object> _ReferredByObjects;
		public IList<object> ReferencedObjects {
			get {
				if(_ReferencedObjects == null)
					_ReferencedObjects = new List<object>();
				return _ReferencedObjects;
			}
		}
		public bool IsReferredBy {
			get {
				if(_ReferredByObjects == null)
					return false;
				else
					return ReferredByObjects.Count > 0;
			}
		}
		public IList<object> ReferredByObjects {
			get {
				if(_ReferredByObjects == null)
					_ReferredByObjects = new List<object>();
				return _ReferredByObjects;
			}
		}
		public PurgerObjectRecord(Purger owner, XPClassInfo classInfo, object key) {
			this.ClassInfo = classInfo;
			this.Key = key;
			this.Owner = owner;
		}
		public PurgerObjectUid GetUid() {
			return new PurgerObjectUid(ClassInfo, Key);
		}
		public void ClearReferencedObjects() {
			if(_ReferencedObjects == null)
				return;
			IEnumerable records = _ReferencedObjects;
			_ReferencedObjects = null;
			foreach(PurgerObjectRecord fixedRecord in records) {
				fixedRecord.ReferredByObjects.Remove(this);
			}
		}
		public void ClearReferredByObjects() {
			if(_ReferredByObjects == null)
				return;
			IEnumerable records = _ReferredByObjects;
			_ReferredByObjects = null;
			foreach(PurgerObjectRecord fixedRecord in records) {
				fixedRecord.ReferencedObjects.Remove(this);
			}
		}
	}
	[Serializable]
	public class PurgeResult {
		public int Processed;
		public int Purged;
		public int NonPurged;
		public int ReferencedByNonDeletedObjects;
	}
	public class PurgeFK {
		public readonly XPMemberInfo Reference;
		public readonly XPClassInfo MappingClass;
		public override bool Equals(object obj) {
			PurgeFK another = obj as PurgeFK;
			if(another == null)
				return false;
			return Equals(Reference, another.Reference) && Equals(MappingClass, another.MappingClass);
		}
		public override int GetHashCode() {
			return HashCodeHelper.CalculateGeneric(Reference, MappingClass);
		}
		public PurgeFK(XPMemberInfo reference, XPClassInfo mappingClass) {
			this.Reference = reference;
			this.MappingClass = mappingClass;
		}
	}
	public class Purger {
		readonly IDataLayer PurgeDataLayer;
		readonly Session PurgeSession;
		readonly Dictionary<PurgerObjectUid, PurgerObjectRecord> RecordsByUid = new Dictionary<PurgerObjectUid,PurgerObjectRecord>();
		readonly IList<PurgeFK> ReferenceMembers = new List<PurgeFK>();
		readonly PurgeResult Result = new PurgeResult();
		protected Purger(IObjectLayer objectLayer, IDataLayer dataLayer) {
			PurgeSession = new Session(objectLayer);
			PurgeDataLayer = dataLayer;
		}
		public static PurgeResult Purge(IObjectLayer objectLayer, IDataLayer dataLayer) {
			return new Purger(objectLayer, dataLayer).Purge();
		}
		public PurgeResult Purge() {
			PurgeSession.TypesManager.EnsureIsTypedObjectValid();
			EnsureTypes();
			FillRecords();
			this.Result.Processed = RecordsByUid.Count;
			FillReferenceMembers();
			ExcludeReferencedByNonMarked();
			this.Result.ReferencedByNonDeletedObjects = this.Result.Processed - RecordsByUid.Count;
			FillReferencedAndReferredBy();
			this.Result.Purged = RecordsByUid.Count;
			this.Result.NonPurged = this.Result.Processed - this.Result.Purged;
			RealPurge();
			return Result;
		}
		public static void FillProps(CriteriaOperatorCollection props, string prefix, XPMemberInfo member) {
			if(member.SubMembers.Count == 0) {
				props.Add(new OperandProperty(prefix + member.Name));
			} else {
				foreach(XPMemberInfo mi in member.SubMembers) {
					if(mi.IsPersistent) {
						props.Add(new OperandProperty(prefix + mi.Name));
					}
				}
			}
		}
		void FillReferencedAndReferredBy() {
			foreach(PurgeFK fk in ReferenceMembers) {
				if(fk.MappingClass.IsGCRecordObject) {
					CriteriaOperator criteria = new GroupOperator(GroupOperatorType.And,
						new UnaryOperator(UnaryOperatorType.Not, new UnaryOperator(UnaryOperatorType.IsNull, new OperandProperty(GCRecordField.StaticName))),
						new UnaryOperator(UnaryOperatorType.Not, new UnaryOperator(UnaryOperatorType.IsNull, new OperandProperty(fk.Reference.Name + '.' + GCRecordField.StaticName))));
					CriteriaOperatorCollection props = new CriteriaOperatorCollection();
					FillProps(props, string.Empty, fk.MappingClass.KeyProperty);
					FillProps(props, fk.Reference.Name + '.', fk.Reference.ReferenceType.KeyProperty);
					SelectStatement selectStatement = ClientSelectSqlGenerator.GenerateSelect(fk.MappingClass, criteria, props, null, null, null, new CollectionCriteriaPatcher(true, PurgeSession.TypesManager), 0, 0);
					SelectStatementResult selectResults = PurgeDataLayer.SelectData(selectStatement).ResultSet[0];
					foreach(SelectStatementResultRow row in selectResults.Rows) {
						object ownerKey = ExtractValue(string.Empty, fk.MappingClass.KeyProperty, props, row.Values);
						object targetKey = ExtractValue(fk.Reference.Name + '.', fk.Reference.ReferenceType.KeyProperty, props, row.Values);
						System.Diagnostics.Debug.Assert(ownerKey != null);
						System.Diagnostics.Debug.Assert(targetKey != null);
						PurgerObjectRecord ownerRecord;
						RecordsByUid.TryGetValue(new PurgerObjectUid(fk.MappingClass, ownerKey), out ownerRecord);
						PurgerObjectRecord targetRecord;
						RecordsByUid.TryGetValue(new PurgerObjectUid(fk.Reference.ReferenceType, targetKey), out targetRecord);
						if(ownerRecord != null && targetRecord != null) {
							if(!ownerRecord.ReferencedObjects.Contains(targetRecord)) {
								ownerRecord.ReferencedObjects.Add(targetRecord);
							}
							if(!targetRecord.ReferredByObjects.Contains(ownerRecord)) {
								targetRecord.ReferredByObjects.Add(ownerRecord);
							}
						} else if(ownerRecord != null) {
							System.Diagnostics.Debug.Assert(!ownerRecord.ReferencedObjects.Contains(targetRecord));
						} else if(targetRecord != null) {
							UnRegister(targetRecord);
						}
					}
				}
			}
		}
		void RealPurge() {
			for(; ; ) {
				PurgerObjectRecord leastReferred = DeleteAllNonReferredByAndGetLeastReferred();
				if(leastReferred == null)
					break;
				KillReferences(new List<object>(leastReferred.ReferredByObjects));
			}
		}
		void ExcludeReferencedByNonMarked(PurgeFK fk) {
			CriteriaOperatorCollection props = new CriteriaOperatorCollection();
			FillProps(props, fk.Reference.Name + '.', fk.Reference.ReferenceType.KeyProperty);
			CriteriaOperator criteria = new UnaryOperator(UnaryOperatorType.Not, new UnaryOperator(UnaryOperatorType.IsNull, new OperandProperty(fk.Reference.Name + '.' + GCRecordField.StaticName)));
			if(fk.MappingClass.IsGCRecordObject)
				criteria = GroupOperator.And(criteria, new UnaryOperator(UnaryOperatorType.IsNull, new OperandProperty(GCRecordField.StaticName)));
			SelectStatement select = ClientSelectSqlGenerator.GenerateSelect(fk.MappingClass, criteria, props, null, props, null, new CollectionCriteriaPatcher(true, PurgeSession.TypesManager), 0, 0);
			SelectStatementResult selectResults = PurgeDataLayer.SelectData(select).ResultSet[0];
			foreach(SelectStatementResultRow resultRow in selectResults.Rows) {
				object referencedKey = ExtractValue(fk.Reference.Name + '.', fk.Reference.ReferenceType.KeyProperty, props, resultRow.Values);
				System.Diagnostics.Debug.Assert(referencedKey != null);
				UnRegister(new PurgerObjectUid(fk.Reference.ReferenceType, referencedKey));
			}
		}
		void ExcludeReferencedByNonMarked() {
			IEnumerable<PurgeFK> references = new List<PurgeFK>(ReferenceMembers);
			foreach(PurgeFK reference in references) {
				ExcludeReferencedByNonMarked(reference);
			}
		}
		void FillReferenceMembers() {
			System.Diagnostics.Debug.Assert(this.ReferenceMembers.Count == 0);
			Dictionary<XPClassInfo, XPClassInfo> processedClassInfos = new Dictionary<XPClassInfo, XPClassInfo>();
			foreach(PurgerObjectRecord record in RecordsByUid.Values) {
				XPClassInfo referencedObjectClassInfo = record.ClassInfo;
				if(processedClassInfos.ContainsKey(referencedObjectClassInfo))
					continue;
				processedClassInfos.Add(referencedObjectClassInfo, referencedObjectClassInfo);
				foreach(XPObjectType ot in this.PurgeSession.TypesManager.AllTypes.Values) {
					XPClassInfo referringType = ot.TypeClassInfo;
					if (referringType == null)
						continue;
					foreach(XPMemberInfo referenceMember in referringType.ObjectProperties) {
						if(referenceMember.IsPersistent) {
							if(referencedObjectClassInfo.IsAssignableTo(referenceMember.ReferenceType)) {
								PurgeFK reference = new PurgeFK(referenceMember, referenceMember.GetMappingClass(referringType));
								if(!this.ReferenceMembers.Contains(reference)) {
									this.ReferenceMembers.Add(reference);
								}
							}
						}
					}
				}
			}
		}
		void Register(PurgerObjectRecord newRecord) {
			this.RecordsByUid.Add(newRecord.GetUid(), newRecord);
		}
		void UnRegister(PurgerObjectRecord record) {
			if(!RecordsByUid.ContainsKey(record.GetUid()))
				return;
			RecordsByUid.Remove(record.GetUid());
			foreach(PurgerObjectRecord cascadeRecord in new List<object>(record.ReferencedObjects)) {
				UnRegister(cascadeRecord);
			}
			record.ClearReferredByObjects();
		}
		void UnRegister(PurgerObjectUid uid) {
			PurgerObjectRecord record;
			RecordsByUid.TryGetValue(uid, out record);
			if(record == null)
				return;
			UnRegister(record);
		}
		void FillRecords() {
			System.Diagnostics.Debug.Assert(this.RecordsByUid.Count == 0);
			ICollection markedObjects = GetMarkDeletedObjects();
			foreach(PurgerObjectRecord record in markedObjects) {
				Register(record);
			}
		}
		void EnsureTypes() {
			List<XPClassInfo> classes = new List<XPClassInfo>(PurgeSession.TypesManager.AllTypes.Count);
			foreach(XPObjectType ot in PurgeSession.TypesManager.AllTypes.Values) {
				XPClassInfo otci = ot.GetClassInfo();
				if(otci.IsGCRecordObject || otci.HasPurgebleObjectReferences())
					classes.Add(otci);
			}
			PurgeSession.UpdateSchema(classes.ToArray());
		}
		PurgerObjectRecord DeleteAllNonReferredByAndGetLeastReferred() {
			for(; ; ) {
				int rvRefersCount = int.MaxValue;
				PurgerObjectRecord rv = null;
				List<PurgerObjectRecord> nonReferredObjects = new List<PurgerObjectRecord>();
				foreach(PurgerObjectRecord record in this.RecordsByUid.Values) {
					if(record.IsReferredBy) {
						if(record.ReferredByObjects.Count < rvRefersCount) {
							rvRefersCount = record.ReferredByObjects.Count;
							rv = record;
						}
					} else {
						nonReferredObjects.Add(record);
					}
				}
				if(nonReferredObjects.Count == 0)
					return rv;
				DoDeletes(nonReferredObjects);
				foreach(PurgerObjectRecord record in nonReferredObjects) {
					record.ClearReferencedObjects();
					UnRegister(record);
				}
			}
		}
		static ICollection GetCriteriasFromKeys(XPClassInfo ci, List<object> keys) {
			if(keys.Count == 0)
				return Array.Empty<object>();
			if(keys.Count <= 64) {
				object[] criteriaKeys = keys.ToArray(); ;
				return new object[] { new object[2] { ci, criteriaKeys } };
			}
			List<object> result = new List<object>();
			result.AddRange(ListHelper.FromCollection(GetCriteriasFromKeys(ci, GetRangeHelper.GetRange(keys, 0, keys.Count / 2))));
			result.AddRange(ListHelper.FromCollection(GetCriteriasFromKeys(ci, GetRangeHelper.GetRange(keys, keys.Count / 2, keys.Count - keys.Count / 2))));
			return result;
		}
		static ICollection GetCriteriasFromRecordsCollection(IEnumerable records) {
			Dictionary<XPClassInfo, List<object>> keysByClassInfos = new Dictionary<XPClassInfo, List<object>>();
			foreach(PurgerObjectRecord record in records) {
				List<object> myList;
				if(!keysByClassInfos.TryGetValue(record.ClassInfo, out myList)) {
					myList = new List<object>();
					keysByClassInfos.Add(record.ClassInfo, myList);
				}
				myList.Add(record.Key);
			}
			List<object> result = new List<object>();
			foreach(XPClassInfo ci in keysByClassInfos.Keys) {
				result.AddRange(ListHelper.FromCollection(GetCriteriasFromKeys(ci, keysByClassInfos[ci])));
			}
			return result;
		}
		void DoDeletes(ICollection recordsToDelete) {
			IEnumerable criterias = GetCriteriasFromRecordsCollection(recordsToDelete);
			List<ModificationStatement> allDeletes = new List<ModificationStatement>();
			BatchWideDataHolder4Modification parameters = new BatchWideDataHolder4Modification(PurgeSession);
			foreach(object[] pair in criterias) {
				XPClassInfo ci = (XPClassInfo)pair[0];
				object[] criteriaKeys = (object[])pair[1];
				CriteriaOperator criteria = new InOperator(ci.KeyProperty.Name, criteriaKeys);
				List<ModificationStatement> deletes = DeleteQueryGenerator.GenerateDelete(ci, ObjectGeneratorCriteriaSet.GetCommonCriteriaSet(criteria), parameters);
				foreach(DeleteStatement statement in deletes)
					statement.RecordsAffected = criteriaKeys.Length;
				allDeletes.AddRange(deletes);
			}
			ModificationStatement[] deleteStatements = allDeletes.ToArray();
			PurgeDataLayer.ModifyData(deleteStatements);
		}
		void KillReferences(ICollection recordsToUpdate) {
			IEnumerable criterias = GetCriteriasFromRecordsCollection(recordsToUpdate);
			List<ModificationStatement> allUpdates = new List<ModificationStatement>();
			BatchWideDataHolder4Modification parameters = new BatchWideDataHolder4Modification(PurgeSession);
			foreach(object[] pair in criterias) {
				XPClassInfo ci = (XPClassInfo)pair[0];
				object[] criteriaKeys = (object[])pair[1];
				CriteriaOperator keyCriteria = new InOperator(ci.KeyProperty.Name, criteriaKeys);
				MemberInfoCollection infos = new MemberInfoCollection(ci);
				foreach(XPMemberInfo mi in ci.ObjectProperties) {
					if(mi.IsPersistent && mi.ReferenceType.IsGCRecordObject) {
						infos.Add(mi);
					}
				}
				List<ModificationStatement> updates = UpdateQueryGenerator.GenerateUpdate(ci, infos, ObjectGeneratorCriteriaSet.GetCommonCriteriaSet(keyCriteria), parameters);
				foreach(UpdateStatement statement in updates)
					statement.RecordsAffected = criteriaKeys.Length;
				allUpdates.AddRange(updates);
			}
			ModificationStatement[] updateStatements = allUpdates.ToArray();
			PurgeDataLayer.ModifyData(updateStatements);
			foreach(PurgerObjectRecord record in recordsToUpdate) {
				record.ClearReferencedObjects();
			}
		}
		static object ExtractSimpleValue(string extractedPropertyName, CriteriaOperatorCollection requiredProperties, object[] requestResult) {
			for(int i = 0; i < requiredProperties.Count; ++i) {
				OperandProperty op = requiredProperties[i] as OperandProperty;
				if(!ReferenceEquals(op, null) && op.PropertyName == extractedPropertyName)
					return requestResult[i];
			}
			throw new InvalidOperationException();	
		}
		public static object ExtractValue(string extractedPropertyPrefix, XPMemberInfo member, CriteriaOperatorCollection requiredProperties, object[] requestResult) {
			if(member.SubMembers.Count == 0) {
				return ExtractSimpleValue(extractedPropertyPrefix + member.Name, requiredProperties, requestResult);
			} else {
				IdList result = new IdList();
				foreach(XPMemberInfo mi in member.SubMembers) {
					if(mi.IsPersistent) {
						result.Add(ExtractSimpleValue(extractedPropertyPrefix + mi.Name, requiredProperties, requestResult));
					}
				}
				return result;
			}
		}
		ICollection GetMarkDeletedObjects() {
			Dictionary<XPClassInfo, XPClassInfo> processedIdClasses = new Dictionary<XPClassInfo,XPClassInfo>();
			List<PurgerObjectRecord> result = new List<PurgerObjectRecord>();
			foreach(XPObjectType ot in PurgeSession.TypesManager.AllTypes.Values) {
				if (ot.TypeClassInfo == null) {
					continue;
				}
				XPClassInfo ci = ot.TypeClassInfo.IdClass;
				if(ci != null && ci.IsGCRecordObject && !processedIdClasses.ContainsKey(ci)) {
					processedIdClasses.Add(ci, ci);
					CriteriaOperator criteria = new UnaryOperator(UnaryOperatorType.Not, new UnaryOperator(UnaryOperatorType.IsNull, new OperandProperty(GCRecordField.StaticName)));
					CriteriaOperatorCollection requiredProperties = new CriteriaOperatorCollection();
					if(ci.IsTypedObject)
						requiredProperties.Add(new OperandProperty(XPObjectType.ObjectTypePropertyName));
					FillProps(requiredProperties, string.Empty, ci.KeyProperty);
					SelectStatement select = ClientSelectSqlGenerator.GenerateSelect(ci, criteria, requiredProperties, null, null, null, new CollectionCriteriaPatcher(true, PurgeSession.TypesManager), 0, 0);
					SelectStatementResult results = PurgeDataLayer.SelectData(select).ResultSet[0];
					foreach(SelectStatementResultRow selectResult in results.Rows) {
						result.Add(CreateNewObjectRecord(ci, requiredProperties, selectResult.Values));
					}
				}
			}
			return result;
		}
		PurgerObjectRecord CreateNewObjectRecord(XPClassInfo baseClassInfo, CriteriaOperatorCollection requiredProperties, object[] selectResult) {
			XPClassInfo objectClassInfo = baseClassInfo;
			if(baseClassInfo.IsTypedObject) {
				object objectTypeId = ExtractValue(string.Empty, baseClassInfo.GetMember(XPObjectType.ObjectTypePropertyName), requiredProperties, selectResult);
				if(objectTypeId != null) {
					objectClassInfo = PurgeSession.TypesManager.GetObjectType((int)objectTypeId).GetClassInfo();
				}
			}
			object key = ExtractValue(string.Empty, baseClassInfo.KeyProperty, requiredProperties, selectResult);
			PurgerObjectRecord newRecord = new PurgerObjectRecord(this, objectClassInfo, key);
			return newRecord;
		}
	}
}
