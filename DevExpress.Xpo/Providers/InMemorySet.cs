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
using System.Text;
using System.Collections;
using System.Globalization;
using System.Collections.ObjectModel;
using System.Xml.Schema;
using System.Xml;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Xml.Serialization;
using DevExpress.Utils;
#if !NET 
using System.Runtime.Serialization;
using DevExpress.Data.NetCompatibility.Extensions;
#endif
using DevExpress.Data.Internal;
namespace DevExpress.Xpo.DB.Helpers {
	public class InMemorySet : IXmlSerializable {
		bool isLoadingMode;
		bool inTransaction;
		UpdatingSchemaStatus updatingSchemaStatus;
		readonly bool caseSensitive;
		string inMemorySetName = "DefaultName";
		public string InMemorySetName {
			get { return inMemorySetName; }
			set { inMemorySetName = value; }
		}
		public bool CaseSensitive {
			get { return caseSensitive; }
		}
		internal bool IsLoadingMode {
			get { return isLoadingMode; }
		}
		public InMemorySet()
			: this(true) {
		}
		public InMemorySet(bool caseSensitive) {
			this.caseSensitive = caseSensitive;
		}
		public InMemorySet(string inMemorySetName)
			: this(inMemorySetName, true) {
		}
		public InMemorySet(string inMemorySetName, bool caseSensitive)
			: this(caseSensitive) {
			this.inMemorySetName = inMemorySetName;
		}
		Dictionary<string, InMemoryTable> tables = new Dictionary<string, InMemoryTable>();
		Dictionary<string, InMemoryRelation> relationsUniqueDict = new Dictionary<string, InMemoryRelation>();
		Dictionary<InMemoryColumn, List<InMemoryRelation>> pDict = new Dictionary<InMemoryColumn, List<InMemoryRelation>>();
		Dictionary<InMemoryColumn, List<InMemoryRelation>> fDict = new Dictionary<InMemoryColumn, List<InMemoryRelation>>();
		InMemoryRollBackOrderList rollbackOrder = new InMemoryRollBackOrderList();
		internal InMemoryRollBackOrderList RollbackOrder { get { return rollbackOrder; } }
		readonly List<InMemoryRelation> emptyRelationList = new List<InMemoryRelation>(0);
		public bool InTransaction { get { return inTransaction; } }
		public IEnumerable<InMemoryTable> Tables { get { return tables.Values; } }
		public IEnumerable<InMemoryRelation> Relations { get { return relationsUniqueDict.Values; } }
		public int TablesCount { get { return tables.Count; } }
		public int RelationsCount { get { return relationsUniqueDict.Count; } }
		public InMemoryRelationCollection GetRelationList() { return new InMemoryRelationCollection(relationsUniqueDict.Values); }
		public InMemoryTable CreateTable(string name) {
			InMemoryTable newTable = new InMemoryTable(this, name);
			try {
				tables.Add(name, newTable);
			}
			catch(ArgumentException) {
				throw new InMemoryDuplicateNameException(name);
			}
			return newTable;
		}
		public bool DropTable(string name) {
			InMemoryTable table;
			if(tables.TryGetValue(name, out table)) {
				tables.Remove(name);
			}
			return false;
		}
		public InMemoryTable GetTable(string name) {
			InMemoryTable result;
			if(tables.TryGetValue(name, out result)) {
				return result;
			}
			return null;
		}
		public void BeginTransaction() {
			inTransaction = true;
		}
		public void Commit() {
			HashSet<InMemoryRow> deletedRows = new HashSet<InMemoryRow>();
			for(int i = 0; i < rollbackOrder.Count; i++) {
				InMemoryRollbackOrderInfo info = rollbackOrder[i];
				switch(info.NewState) {
					case InMemoryItemState.Deleted:
						info.Row.CheckPRelations();
						deletedRows.Add(info.Row);
						break;
					case InMemoryItemState.Updated:
						info.Row.CheckPRelationsModified();
						break;
				}
			}
			int i1 = 0;
			try {
				for(i1 = 0; i1 < rollbackOrder.Count; i1++) {
					InMemoryRollbackOrderInfo info = rollbackOrder[i1];
					switch(info.NewState) {
						case InMemoryItemState.Inserted:
							info.Row.CheckAndEnterFRelations();
							break;
						case InMemoryItemState.Updated:
							if(!deletedRows.Contains(info.Row)) {
								info.Row.CheckAndEnterFRelations();
							}
							break;
					}
				}
			}
			catch {
				i1--;
				for(int i = i1; i >= 0; i--) {
					InMemoryRollbackOrderInfo info = rollbackOrder[i];
					switch(info.NewState) {
						case InMemoryItemState.Inserted:
							info.Row.LeaveFRelations();
							break;
						case InMemoryItemState.Updated:
							info.Row.LeaveFRelations();
							break;
					}
				}
				throw;
			}
			for(int i = 0; i < rollbackOrder.Count; i++) {
				InMemoryRollbackOrderInfo info = rollbackOrder[i];
				switch(info.NewState) {
					case InMemoryItemState.Deleted:
						info.Row.Table.Rows.RemoveInternal(info.Row);
						break;
					case InMemoryItemState.Inserted:
					case InMemoryItemState.Updated:
						info.Row.Commit(info);
						break;
				}
			}
			rollbackOrder.Clear();
			inTransaction = false;
		}
		public void Rollback() {
			for(int i = rollbackOrder.Count - 1; i >= 0; i--) {
				InMemoryRollbackOrderInfo info = rollbackOrder[i];
				if(info.IsNewRow) {
					info.Row.Table.Rows.RemoveInternal(info.Row);
					continue;
				}
				info.Row.Rollback(info);
			}
			for(int i = rollbackOrder.Count - 1; i >= 0; i--) {
				InMemoryRollbackOrderInfo info = rollbackOrder[i];
				switch(info.NewState) {
					case InMemoryItemState.Deleted:
						info.Row.CheckAndEnterFRelations();
						break;
					case InMemoryItemState.Updated:
						info.Row.CheckAndEnterFRelations();
						break;
				}
			}
			rollbackOrder.Clear();
			inTransaction = false;
		}
		public void BeginUpdateSchema() {
			updatingSchemaStatus = new UpdatingSchemaStatus();
		}
		public void EndUpdateSchema() {
			var lastStatus = updatingSchemaStatus;
			updatingSchemaStatus = null;
			if(lastStatus != null) {
				if(lastStatus.FixRelations) {
					FixRelations();
				}
			}
		}
		public void AddRelation(InMemoryRelationPair[] pairs) {
			Array.Sort<InMemoryRelationPair>(pairs, new InMemoryRelationPairPNameComparer());
			InMemoryRelation relation = new InMemoryRelation(pairs);
			InMemoryColumn[] pColumns = relation.GetPColumns();
			bool pIndexExists = false;
			foreach(IInMemoryIndex cindex in relation.PTable.IndexesInternal) {
				if(cindex.EqualsColumns(pColumns)) {
					pIndexExists = true;
					break;
				}
			}
			if(!pIndexExists) {
				relation.PTable.CreateIndex(pColumns, false);
			}
			try {
				relationsUniqueDict.Add(relation.Name, relation);
			}
			catch(ArgumentException ex) {
				throw new InMemoryDuplicateNameException(Res.GetString(Res.InMemorySet_AddRelation), ex);
			}
			foreach(InMemoryRelationPair pair in pairs) {
				AddColumnToDictionary(pDict, pair.PKey, relation);
				AddColumnToDictionary(fDict, pair.FKey, relation);
			}
			try {
				relation.CheckRelation();
				FixRelations();
			}
			catch(Exception) {
				RemoveRelation(relation.Pairs);
				throw;
			}
		}
		public List<InMemoryRelation> GetPRelations(InMemoryColumn pKey) {
			List<InMemoryRelation> result = GetRelationsInternal(pDict, pKey);
			return result ?? emptyRelationList;
		}
		public List<InMemoryRelation> GetFRelations(InMemoryColumn fKey) {
			List<InMemoryRelation> result = GetRelationsInternal(fDict, fKey);
			return result ?? emptyRelationList;
		}
		public InMemoryRelation GetRelation(string name) {
			InMemoryRelation relation;
			if(relationsUniqueDict.TryGetValue(name, out relation)) {
				return relation;
			}
			return null;
		}
		public bool RemoveRelation(InMemoryRelationPair[] pairs) {
			List<int> deleteIndex = new List<int>();
			List<List<InMemoryRelation>> deleteList = new List<List<InMemoryRelation>>();
			InMemoryRelation relation = null;
			Array.Sort<InMemoryRelationPair>(pairs, new InMemoryRelationPairPNameComparer());
			foreach(InMemoryRelation rel in relationsUniqueDict.Values) {
				if(rel.Pairs.Length != pairs.Length) continue;
				bool success = true;
				for(int i = 0; i < pairs.Length; i++) {
					if(pairs[i].PKey.Equals(rel.Pairs[i].PKey) && pairs[i].FKey.Equals(rel.Pairs[i].FKey)) continue;
					success = false;
					break;
				}
				if(!success) continue;
				relation = rel;
				break;
			}
			if(relation == null) return false;
			foreach(InMemoryRelationPair pair in pairs) {
				int indexP = -1;
				int indexF = -1;
				List<InMemoryRelation> pList = GetRelationsInternal(pDict, pair.PKey);
				if(pList != null) {
					for(int i = 0; i < pList.Count; i++) {
						if(relation == pList[i]) {
							indexP = i;
							break;
						}
					}
				}
				List<InMemoryRelation> fList = GetRelationsInternal(fDict, pair.FKey);
				if(fList != null) {
					for(int i = 0; i < fList.Count; i++) {
						if(relation == fList[i]) {
							indexF = i;
							break;
						}
					}
				}
				if(indexP >= 0 && indexF >= 0) {
					deleteIndex.Add(indexP);
					deleteIndex.Add(indexF);
					deleteList.Add(pList);
					deleteList.Add(fList);
					continue;
				}
				return false;
			}
			for(int i = 0; i < deleteIndex.Count; i++) {
				deleteList[i].RemoveAt(deleteIndex[i]);
			}
			relationsUniqueDict.Remove(relation.Name);
			FixRelations();
			return true;
		}
		public void RemoveRelations(InMemoryColumn column) {
			RemoveRelations(column, true);
		}
		public void RemoveRelations(IEnumerable<InMemoryColumn> columns) {
			foreach(InMemoryColumn column in columns) {
				RemoveRelations(column, false);
			}
			FixRelations();
		}
		void RemoveRelations(InMemoryColumn column, bool doFix) {
			List<InMemoryRelation> fRel = GetRelationsInternal(fDict, column);
			List<InMemoryRelation> pRel = GetRelationsInternal(pDict, column);
			fDict.Remove(column);
			pDict.Remove(column);
			if(fRel != null) {
				foreach(InMemoryRelation rel in fRel) {
					foreach(InMemoryRelationPair pair in rel.Pairs) {
						GetRelationsInternal(pDict, pair.PKey).Remove(rel);
						relationsUniqueDict.Remove(rel.Name);
					}
				}
			}
			if(pRel != null) {
				foreach(InMemoryRelation rel in pRel) {
					foreach(InMemoryRelationPair pair in rel.Pairs) {
						GetRelationsInternal(fDict, pair.FKey).Remove(rel);
						relationsUniqueDict.Remove(rel.Name);
					}
				}
			}
			if(doFix) FixRelations();
		}
		void FixRelations() {
			if(updatingSchemaStatus != null) {
				updatingSchemaStatus.FixRelations = true;
				return;
			}
			foreach(InMemoryTable currentTable in tables.Values) {
				currentTable.FixRelations();
			}
		}
		public void ClearRelations() {
			ClearRelations(true);
		}
		public void ClearRelations(bool doFixRelations) {
			relationsUniqueDict.Clear();
			pDict.Clear();
			fDict.Clear();
			if(doFixRelations) FixRelations();
		}
		public void ClearTables() {
			tables.Clear();
		}
		static List<InMemoryRelation> GetRelationsInternal(Dictionary<InMemoryColumn, List<InMemoryRelation>> dict, InMemoryColumn column) {
			List<InMemoryRelation> relations;
			if(dict.TryGetValue(column, out relations)) {
				return relations;
			}
			return null;
		}
		static void AddColumnToDictionary(Dictionary<InMemoryColumn, List<InMemoryRelation>> dict, InMemoryColumn column, InMemoryRelation relation) {
			List<InMemoryRelation> relList;
			if(!dict.TryGetValue(column, out relList)) {
				relList = new List<InMemoryRelation>();
				dict.Add(column, relList);
			}
			else {
				relList.Contains(relation); 
			}
			relList.Add(relation);
		}
		static string GetTypeString(Type type, out int size) {
			size = 0;
			switch(DXTypeExtensions.GetTypeCode(type)) {
				case TypeCode.Int32:
					return "xs:int";
				case TypeCode.UInt32:
					return "xs:unsignedInt";
				case TypeCode.String:
					return xsStringTypeString;
				case TypeCode.Object:
					return GetObjectTypeString(type);
				case TypeCode.Boolean:
					return "xs:boolean";
				case TypeCode.Byte:
					return "xs:unsignedByte";
				case TypeCode.Char:
					size = 1;
					return xsStringTypeString;
				case TypeCode.DateTime:
					return "xs:dateTime";
				case TypeCode.Decimal:
					return "xs:decimal";
				case TypeCode.Double:
					return "xs:double";
				case TypeCode.Int16:
					return "xs:short";
				case TypeCode.Int64:
					return "xs:long";
				case TypeCode.SByte:
					return "xs:byte";
				case TypeCode.Single:
					return "xs:float";
				case TypeCode.UInt16:
					return "xs:unsignedShort";
				case TypeCode.UInt64:
					return "xs:unsignedLong";
				default:
					return null;
			}
		}
		static Type GetTypeFromString(string str, out Type convertFromType) {
			convertFromType = null;
			switch(str) {
				case "xs:int":
				case "xs:integer":
					return typeof(Int32);
				case "xs:unsignedInt":
					return typeof(UInt32);
				case xsStringTypeString:
					return typeof(string);
				case xsHexBinaryString:
				case xsBase64String:
					return typeof(byte[]);
				case "xs:boolean":
					return typeof(bool);
				case "xs:unsignedByte":
					return typeof(byte);
				case "xs:dateTime":
				case "xs:date":
					return typeof(DateTime);
				case "xs:decimal":
					return typeof(decimal);
				case "xs:double":
					return typeof(double);
				case "xs:duration":
					if(ConnectionProviderSql.UseLegacyTimeSpanSupport) {
						return typeof(TimeSpan);
					}
					else {
						convertFromType = typeof(TimeSpan);
						return typeof(double);
					}
				case "xs:short":
					return typeof(Int16);
				case "xs:long":
					return typeof(Int64);
				case "xs:byte":
					return typeof(sbyte);
				case "xs:float":
					return typeof(Single);
				case "xs:unsignedShort":
					return typeof(UInt16);
				case "xs:unsignedLong":
					return typeof(UInt64);
				default:
					throw new ArgumentException(Res.GetString(Res.InMemorySet_InvalidTypeString));
			}
		}
		static string GetObjectTypeString(Type type) {
			if(ReferenceEquals(typeof(byte[]), type)) {
				return xsBase64String;
			}
			if(ReferenceEquals(typeof(TimeSpan), type)) {
				return "xs:double";
			}
			return null;
		}
		public XmlSchema GetSchema() {
			throw new NotSupportedException();
		}
		public string GetXmlSchema() {
			MemoryStream ms = new MemoryStream();
			XmlWriterSettings xws = new XmlWriterSettings();
			xws.Indent = true;
			XmlWriter xw = XmlWriter.Create(ms, xws);
			GetXmlSchemaCore(xw);
			xw.Flush();
#if !DXRESTRICTED
			xw.Close();
#endif
			ms.Flush();
			ms.Position = 0;
			StreamReader sr = new StreamReader(ms);
			return sr.ReadToEnd();
		}
		const string xs = "http://www.w3.org/2001/XMLSchema";
		const string xsi = "http://www.w3.org/2001/XMLSchema-instance";
		const string msdata = "urn:schemas-microsoft-com:xml-msdata";
		const char hexUpperAChar = 'A';
		const char hexLowerAChar = 'a';
		const char hexZeroChar = '0';
		const string xsHexBinaryString = "xs:hexBinary";
		const string xsBase64String = "xs:base64Binary";
		const string xsAnyTypeString = "xs:anyType";
		const string xsStringTypeString = "xs:string";
		void GetXmlSchemaCore(XmlWriter writer) {
			writer.WriteStartElement("xs", "schema", xs);
			writer.WriteAttributeString("id", InMemorySetName);
			writer.WriteAttributeString("xmlns", "msdata", null, msdata);
			writer.WriteStartElement("element", xs);
			writer.WriteAttributeString("name", InMemorySetName);
			writer.WriteAttributeString("IsDataSet", msdata, "true");
			writer.WriteAttributeString("UseCurrentLocale", msdata, "true");
			writer.WriteStartElement("complexType", xs);
			writer.WriteStartElement("choice", xs);
			writer.WriteAttributeString("minOccurs", "0");
			writer.WriteAttributeString("maxOccurs", "unbounded");
			foreach(InMemoryTable table in Tables) {
				writer.WriteStartElement("element", xs);
				writer.WriteAttributeString("name", XmlConvert.EncodeLocalName(table.Name));
				writer.WriteStartElement("complexType", xs);
				writer.WriteStartElement("sequence", xs);
				foreach(InMemoryColumn column in table.Columns) {
					writer.WriteStartElement("element", xs);
					writer.WriteAttributeString("name", XmlConvert.EncodeLocalName(column.Name));
					if(column.AutoIncrement) {
						writer.WriteAttributeString("AutoIncrement", msdata, "true");
						writer.WriteAttributeString("AutoIncrementSeed", msdata, "1");
					}
					int size;
					string type = GetTypeString(column.Type, out size);
					if(type == null) {
						if(ReferenceEquals(column.Type, typeof(Guid)))
							type = xsStringTypeString;
						else
							type = xsAnyTypeString;
						writer.WriteAttributeString("DataType", msdata, column.Type.AssemblyQualifiedName);
						writer.WriteAttributeString("type", type);
						writer.WriteAttributeString("minOccurs", "0");
					}
					else {
						if(size == 0) {
							if(column.MaxLength > 0) {
								writer.WriteAttributeString("minOccurs", "0");
								writer.WriteStartElement("simpleType", xs);
								writer.WriteStartElement("restriction", xs);
								writer.WriteAttributeString("base", type);
								writer.WriteStartElement("maxLength", xs);
								writer.WriteAttributeString("value", column.MaxLength.ToString(CultureInfo.InvariantCulture));
								writer.WriteEndElement();
								writer.WriteEndElement();
								writer.WriteEndElement();
							}
							else {
								writer.WriteAttributeString("type", type);
								writer.WriteAttributeString("minOccurs", "0");
							}
						}
						else {
							writer.WriteAttributeString("minOccurs", "0");
							writer.WriteStartElement("simpleType", xs);
							writer.WriteStartElement("restriction", xs);
							writer.WriteAttributeString("base", type);
							writer.WriteStartElement("length", xs);
							writer.WriteAttributeString("value", size.ToString(CultureInfo.InvariantCulture));
							writer.WriteEndElement();
							writer.WriteEndElement();
							writer.WriteEndElement();
						}
					}
					writer.WriteEndElement();
				}
				writer.WriteEndElement();
				writer.WriteEndElement();
				writer.WriteEndElement();
			}
			writer.WriteEndElement();
			writer.WriteEndElement();
			foreach(InMemoryTable table in Tables) {
				foreach(InMemoryIndexWrapper index in table.Indexes) {
					if(!index.Unique) { continue; }
					writer.WriteStartElement("unique", xs);
					writer.WriteAttributeString("name", XmlConvert.EncodeLocalName(index.Name));
					if(table.PrimaryKey != null && index.Name == table.PrimaryKey.Name) {
						writer.WriteAttributeString("PrimaryKey", msdata, XmlConvert.ToString(true));
					}
					writer.WriteStartElement("selector", xs);
					writer.WriteAttributeString("xpath", string.Concat(".//", XmlConvert.EncodeLocalName(table.Name)));
					writer.WriteEndElement();
					foreach(InMemoryColumn column in index.Columns) {
						writer.WriteStartElement("field", xs);
						writer.WriteAttributeString("xpath", XmlConvert.EncodeLocalName(column.Name));
						writer.WriteEndElement();
					}
					writer.WriteEndElement();
				}
			}
			foreach(InMemoryRelation relation in Relations) {
				writer.WriteStartElement("unique", xs);
				writer.WriteAttributeString("name", XmlConvert.EncodeLocalName(relation.Name + "Idx"));
				writer.WriteStartElement("selector", xs);
				writer.WriteAttributeString("xpath", ".//" + XmlConvert.EncodeLocalName(relation.Pairs[0].PKey.Table.Name));
				writer.WriteEndElement();
				for(int i = 0; i < relation.Pairs.Length; i++) {
					writer.WriteStartElement("field", xs);
					writer.WriteAttributeString("xpath", XmlConvert.EncodeLocalName(relation.Pairs[i].PKey.Name));
					writer.WriteEndElement();
				}
				writer.WriteEndElement();
			}
			foreach(InMemoryRelation relation in Relations) {
				writer.WriteStartElement("keyref", xs);
				writer.WriteAttributeString("name", XmlConvert.EncodeLocalName(relation.Name));
				writer.WriteAttributeString("refer", XmlConvert.EncodeLocalName(relation.Name + "Idx"));
				writer.WriteStartElement("selector", xs);
				writer.WriteAttributeString("xpath", ".//" + XmlConvert.EncodeLocalName(relation.Pairs[0].FKey.Table.Name));
				writer.WriteEndElement();
				for(int i = 0; i < relation.Pairs.Length; i++) {
					writer.WriteStartElement("field", xs);
					writer.WriteAttributeString("xpath", XmlConvert.EncodeLocalName(relation.Pairs[i].FKey.Name));
					writer.WriteEndElement();
				}
				writer.WriteEndElement();
			}
			writer.WriteEndElement();
			writer.WriteEndElement();
		}
#if DEBUGTEST
		public string WriteXml() {
			MemoryStream ms = new MemoryStream();
			XmlWriterSettings xws = new XmlWriterSettings();
			xws.Indent = true;
			xws.OmitXmlDeclaration = true;
			XmlWriter xw = XmlWriter.Create(ms, xws);
			this.WriteXml(xw);
			xw.Flush();
#if !DXRESTRICTED
			xw.Close();
#endif
			ms.Flush();
			ms.Position = 0;
			StreamReader sr = new StreamReader(ms);
			return sr.ReadToEnd();
		}
#endif
		public void WriteXml(string fileName) {
			using(FileStream fs = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.Write)) {
				XmlWriterSettings xws = new XmlWriterSettings();
				xws.Indent = true;
				xws.OmitXmlDeclaration = true;
				XmlWriter xw = XmlWriter.Create(fs, xws);
				this.WriteXml(xw);
				xw.Flush();
#if !DXRESTRICTED
				xw.Close();
#endif
			}
		}
		public void WriteXml(XmlWriter writer) {
			writer.WriteStartElement(InMemorySetName);
			GetXmlSchemaCore(writer);
			foreach(InMemoryTable table in Tables) {
				foreach(InMemoryRow row in table.Rows) {
					writer.WriteStartElement(XmlConvert.EncodeLocalName(table.Name));
					for(int i = 0; i < table.Columns.Count; i++) {
						object o = row[i];
						if(o == null || o is DBNull) continue;
						Type type = o.GetType();
						if(type == table.Columns[i].Type) {
							TypeCode typeCode = DXTypeExtensions.GetTypeCode(type);
							if(typeCode != TypeCode.Object) {
								if(typeCode == TypeCode.Char) {
									writer.WriteStartElement(XmlConvert.EncodeLocalName(table.Columns[i].Name));
									writer.WriteValue(new String((char)o, 1));
									writer.WriteEndElement();
								}
								else {
									writer.WriteStartElement(XmlConvert.EncodeLocalName(table.Columns[i].Name));
									writer.WriteValue(o);
									writer.WriteEndElement();
								}
							}
							else if(ReferenceEquals(type, typeof(byte[]))) {
								byte[] buffer = (byte[])o;
								writer.WriteStartElement(XmlConvert.EncodeLocalName(table.Columns[i].Name));
								writer.WriteBase64(buffer, 0, buffer.Length);
								writer.WriteEndElement();
							}
							else if(ReferenceEquals(type, typeof(Guid))) {
								writer.WriteStartElement(XmlConvert.EncodeLocalName(table.Columns[i].Name));
								writer.WriteValue(XmlConvert.ToString((Guid)o));
								writer.WriteEndElement();
							}
							else if(ReferenceEquals(type, typeof(TimeSpan))) {
								writer.WriteStartElement(XmlConvert.EncodeLocalName(table.Columns[i].Name));
								writer.WriteValue(XmlConvert.ToString((TimeSpan)o));
								writer.WriteEndElement();
							}
							else if(ReferenceEquals(type, typeof(DateOnly))) {
								var date = ((DateOnly)o).ToDateTime(TimeOnly.MinValue);
								writer.WriteStartElement(XmlConvert.EncodeLocalName(table.Columns[i].Name));
								writer.WriteValue(date);
								writer.WriteEndElement();
							}
							else if(ReferenceEquals(type, typeof(TimeOnly))) {
								var timeSpan = ((TimeOnly)o).ToTimeSpan();
								writer.WriteStartElement(XmlConvert.EncodeLocalName(table.Columns[i].Name));
								writer.WriteValue(XmlConvert.ToString(timeSpan));
								writer.WriteEndElement();
							}
							else {
								if(!(o is IXmlSerializable)) { throw new InvalidOperationException(type.Name + " does not implement IXmlSerializable."); }
								writer.WriteStartElement(XmlConvert.EncodeLocalName(table.Columns[i].Name));
								((IXmlSerializable)o).WriteXml(writer);
								writer.WriteEndElement();
							}
						}
						else {
							int size;
							string typeStr = GetTypeString(type, out size);
							if(typeStr == null) {
								if(!(o is IXmlSerializable)) { throw new InvalidOperationException(type.Name + " does not implement IXmlSerializable."); }
								writer.WriteStartElement(XmlConvert.EncodeLocalName(table.Columns[i].Name));
								writer.WriteAttributeString("msdata", "InstanceType", msdata, type.AssemblyQualifiedName);
								((IXmlSerializable)o).WriteXml(writer);
								writer.WriteEndElement();
							}
							else if(typeStr == xsStringTypeString && size == 1) {
								writer.WriteStartElement(XmlConvert.EncodeLocalName(table.Columns[i].Name));
								writer.WriteAttributeString("msdata", "InstanceType", msdata, "System.Char");
								writer.WriteValue(((char)o).ToString());
								writer.WriteEndElement();
							}
							else if(typeStr == xsBase64String) {
								writer.WriteStartElement(XmlConvert.EncodeLocalName(table.Columns[i].Name));
								writer.WriteAttributeString("xsi", "type", xsi, typeStr);
								writer.WriteAttributeString("xmlns", "xs", null, xs);
								byte[] buffer = (byte[])o;
								writer.WriteBase64(buffer, 0, buffer.Length);
								writer.WriteEndElement();
							}
							else {
								writer.WriteStartElement(XmlConvert.EncodeLocalName(table.Columns[i].Name));
								writer.WriteAttributeString("xsi", "type", xsi, typeStr);
								writer.WriteAttributeString("xmlns", "xs", null, xs);
								writer.WriteValue(o.ToString());
								writer.WriteEndElement();
							}
						}
					}
					writer.WriteEndElement();
				}
			}
			writer.WriteEndElement();
		}
		public void ReadXml(string fileName) {
			using(FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read)) {
				using(XmlReader xr = SafeXml.CreateReader(fs)) {
					this.ReadXml(xr);
				}
			}
		}
		public void ReadXml(XmlReader rdr) {
			this.ClearRelations();
			this.ClearTables();
			const string STR_Mstns = "mstns:";
			if(!rdr.ReadToDescendant("schema", xs)) { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_SchemaNodeNotFound)); }
			this.InMemorySetName = rdr.GetAttribute("id");
			if(!rdr.ReadToDescendant("element", xs)) { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_ElementNodeNotFound)); }
			if(rdr.GetAttribute("IsDataSet", msdata) != "true") { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_NotDataSetNode)); }
			if(!rdr.ReadToDescendant("complexType", xs)) { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_ComplexTypeNodeNotFound)); }
			while(rdr.Read() && rdr.NodeType == XmlNodeType.Whitespace) { }
			if(rdr.Name != "xs:choice" && rdr.Name != "xs:sequence") { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_choiceNodeNotFound)); }
			isLoadingMode = true;
			Dictionary<InMemoryColumn, Type> convertFromTypeDictionary = new Dictionary<InMemoryColumn, Type>();
			Dictionary<InMemoryColumn, string> columnTypeStringDictionary = new Dictionary<InMemoryColumn, string>();
			try {
				BeginUpdateSchema();
				try {
					for(; ; ) {
						rdr.Read();
						if(rdr.NodeType == XmlNodeType.Whitespace) { rdr.Read(); }
						if(rdr.Name != "xs:element") { break; }
						string tableName = XmlConvert.DecodeName(rdr.GetAttribute("name"));
						if(string.IsNullOrEmpty(tableName)) { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_TableNameIsNotSpecified)); }
						InMemoryTable tbl = this.CreateTable(tableName);
						rdr.Read();
						if(rdr.NodeType == XmlNodeType.Whitespace) { rdr.Read(); }
						if(rdr.Name != "xs:complexType") { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_XScomplexTypeExpected)); }
						rdr.Read();
						if(rdr.NodeType == XmlNodeType.Whitespace) { rdr.Read(); }
						if(rdr.Name != "xs:sequence") { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_XSsequenceExpected)); }
						for(; ; ) {
							rdr.Read();
							if(rdr.NodeType == XmlNodeType.Whitespace) { rdr.Read(); }
							if(rdr.Name != "xs:element") { break; }
							int? maxLength = null;
							string name = XmlConvert.DecodeName(rdr.GetAttribute("name"));
							if(string.IsNullOrEmpty(name)) { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_ColumnNameIsNotSpecified)); }
							string autoIncrement = rdr.GetAttribute("AutoIncrement", msdata);
							string typeStr = rdr.GetAttribute("type");
							Type type = null;
							Type convertFromType = null;
							if(typeStr == null) {
								rdr.Read();
								if(rdr.NodeType == XmlNodeType.Whitespace) { rdr.Read(); }
								if(rdr.Name != "xs:simpleType") { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_XSsimpleTypeExpected)); }
								rdr.Read();
								if(rdr.NodeType == XmlNodeType.Whitespace) { rdr.Read(); }
								if(rdr.Name != "xs:restriction") { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_XSrestrictionExpected)); }
								if(rdr.GetAttribute("base") != xsStringTypeString) { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_InvalidBaseAttributeValue)); }
								rdr.Read();
								if(rdr.NodeType == XmlNodeType.Whitespace) { rdr.Read(); }
								if(rdr.Name == "xs:maxLength") {
									maxLength = Int32.Parse(rdr.GetAttribute("value"));
								}
								else {
									if(rdr.Name != "xs:length") { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_XSlengthExpected)); }
									if(rdr.GetAttribute("value") != "1") { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_InvalidValueAttributeValue)); }
								}
								rdr.Read();
								if(rdr.NodeType == XmlNodeType.Whitespace) { rdr.Read(); }
								rdr.Read();
								if(rdr.NodeType == XmlNodeType.Whitespace) { rdr.Read(); }
								rdr.Read();
								if(rdr.NodeType == XmlNodeType.Whitespace) { rdr.Read(); }
								if(rdr.Name != "xs:element") { throw new XmlException(); }
								if(maxLength.HasValue) {
									type = typeof(string);
								}
								else {
									type = typeof(char);
								}
							}
							else {
								if(!typeStr.StartsWith("xs:")) { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_InvalidColumnTypeDeclaration)); }
								if(typeStr == xsAnyTypeString) {
									typeStr = rdr.GetAttribute("DataType", msdata);
									if(string.IsNullOrEmpty(typeStr)) { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_DataTypeAttributeNotFound)); }
									type = SafeTypeResolver.GetKnownUserType(typeStr);
								}
								else if(typeStr == xsStringTypeString) {
									string dataType = rdr.GetAttribute("DataType", msdata);
									if(!string.IsNullOrEmpty(dataType) && ReferenceEquals(SafeTypeResolver.GetKnownUserType(dataType), typeof(Guid))) {
										type = typeof(Guid);
									}
									else {
										type = GetTypeFromString(typeStr, out convertFromType);
									}
								}
								else {
									type = GetTypeFromString(typeStr, out convertFromType);
								}
							}
							if(type == null) { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_CantGetColumnType)); }
							if(autoIncrement == "true") tbl.Columns.Add(name, type, true);
							else tbl.Columns.Add(name, type);
							columnTypeStringDictionary.Add(tbl.Columns[name], typeStr);
							if(convertFromType != null) {
								convertFromTypeDictionary.Add(tbl.Columns[name], convertFromType);
							}
						}
						tbl.Columns.CommitColumnsChanges();
						rdr.Read();
						if(rdr.NodeType == XmlNodeType.Whitespace) { rdr.Read(); }
						rdr.Read();
						if(rdr.NodeType == XmlNodeType.Whitespace) { rdr.Read(); }
						if(rdr.Name != "xs:element") { throw new XmlException(); }
					}
					rdr.Read();
					if(rdr.NodeType == XmlNodeType.Whitespace) { rdr.Read(); }
					rdr.Read();
					if(rdr.NodeType == XmlNodeType.Whitespace) { rdr.Read(); }
					if(rdr.Name == "xs:unique") {
						List<IndexSerializationInfo> indexes = new List<IndexSerializationInfo>();
						bool firstIdx = true;
						for(; ; ) {
							if(!firstIdx) {
								rdr.Read();
								if(rdr.NodeType == XmlNodeType.Whitespace) { rdr.Read(); }
								if(rdr.Name != "xs:unique") { break; }
							}
							firstIdx = false;
							string idxName = XmlConvert.DecodeName(rdr.GetAttribute("name"));
							if(string.IsNullOrEmpty(idxName)) { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_NameAttributeExpected)); }
							string pkAttr = rdr.GetAttribute("msdata:PrimaryKey");
							IndexSerializationInfo idxSerInfo = new IndexSerializationInfo(idxName, string.IsNullOrEmpty(pkAttr) ? false : XmlConvert.ToBoolean(pkAttr));
							rdr.Read();
							if(rdr.NodeType == XmlNodeType.Whitespace) { rdr.Read(); }
							if(rdr.Name != "xs:selector") { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_XSselectorNodeExpected)); }
							string idxTableName = XmlConvert.DecodeName(rdr.GetAttribute("xpath"));
							if(string.IsNullOrEmpty(idxTableName)) { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_XpathAttributeExpected)); }
							if(!idxTableName.StartsWith(".//")) { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_XpathAttributeWrongFormat)); }
							idxTableName = idxTableName.Remove(0, 3);
							if(idxTableName.StartsWith(STR_Mstns))
								idxTableName = idxTableName.Substring(STR_Mstns.Length);
							if(!this.tables.ContainsKey(idxTableName)) throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_TableNotDeclared, idxTableName));
							idxSerInfo.TableName = idxTableName;
							for(; ; ) {
								rdr.Read();
								if(rdr.NodeType == XmlNodeType.Whitespace) { rdr.Read(); }
								if(rdr.Name != "xs:field") { break; }
								string idxFieldName = XmlConvert.DecodeName(rdr.GetAttribute("xpath"));
								if(idxFieldName.StartsWith(STR_Mstns))
									idxFieldName = idxFieldName.Substring(STR_Mstns.Length);
								if(string.IsNullOrEmpty(idxFieldName)) { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_XpathAttributeExpected)); }
								idxSerInfo.Fields.Add(this.tables[idxSerInfo.TableName].Columns[idxFieldName]);
							}
							indexes.Add(idxSerInfo);
						}
						bool hasRels = false;
						if(rdr.Name == "xs:keyref") {
							hasRels = true;
							List<RelationSerializationInfo> relations = new List<RelationSerializationInfo>();
							bool firstRel = true;
							for(; ; ) {
								if(!firstRel) {
									rdr.Read();
									if(rdr.NodeType == XmlNodeType.Whitespace) { rdr.Read(); }
									if(rdr.Name != "xs:keyref") { break; }
								}
								firstRel = false;
								string relName = XmlConvert.DecodeName(rdr.GetAttribute("name"));
								if(string.IsNullOrEmpty(relName)) { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_NameAttributeExpected)); }
								string referName = XmlConvert.DecodeName(rdr.GetAttribute("refer"));
								if(string.IsNullOrEmpty(referName)) { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_ReferAttributeExpected)); }
								bool indexFound = false;
								for(int i = 0; i < indexes.Count; i++) {
									if(indexes[i].Name == referName) {
										indexFound = true;
										break;
									}
								}
								if(!indexFound) { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_ReferNotFound, referName)); }
								RelationSerializationInfo relSerInfo = new RelationSerializationInfo(relName, referName);
								rdr.Read();
								if(rdr.NodeType == XmlNodeType.Whitespace) { rdr.Read(); }
								if(rdr.Name != "xs:selector") { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_XSselectorNodeExpected)); }
								string relTableName = XmlConvert.DecodeName(rdr.GetAttribute("xpath"));
								if(string.IsNullOrEmpty(relTableName)) { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_XpathAttributeExpected)); }
								if(!relTableName.StartsWith(".//")) { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_XpathAttributeWrongFormat)); }
								relTableName = relTableName.Remove(0, 3);
								if(!this.tables.ContainsKey(relTableName)) { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_TableNotDeclared, relTableName)); }
								relSerInfo.TableName = relTableName;
								for(; ; ) {
									rdr.Read();
									if(rdr.NodeType == XmlNodeType.Whitespace) { rdr.Read(); }
									if(rdr.Name != "xs:field") { break; }
									string relFieldName = XmlConvert.DecodeName(rdr.GetAttribute("xpath"));
									if(string.IsNullOrEmpty(relFieldName)) { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_XpathAttributeExpected)); }
									relSerInfo.Fields.Add(this.tables[relSerInfo.TableName].Columns[relFieldName]);
								}
								relations.Add(relSerInfo);
							}
							for(int i = 0; i < indexes.Count - relations.Count; i++) {
								IndexSerializationInfo indexInfo = indexes[i];
								InMemoryTable currentTable = this.tables[indexInfo.TableName];
								string indexName = currentTable.CreateIndex(indexInfo.Fields.ToArray(), true);
								if(indexInfo.PrimaryKey) {
									currentTable.SetPrimaryKey(indexName);
								}
							}
							for(int i = 0; i < relations.Count; i++) {
								List<InMemoryColumn> primaryColumns = null;
								for(int j = 0; j < indexes.Count; j++) {
									if(indexes[j].Name == relations[i].ReferredIndex) {
										primaryColumns = indexes[j].Fields;
									}
								}
								if(primaryColumns == null) { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_NullPrimaryColumns, relations[i].ReferredIndex)); }
								if(primaryColumns.Count != relations[i].Fields.Count) { throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_InconsistentForeignKeyCount)); }
								InMemoryRelationPair[] pairs = new InMemoryRelationPair[relations[i].Fields.Count];
								for(int j = 0; j < relations[i].Fields.Count; j++) {
									pairs[j] = new InMemoryRelationPair(primaryColumns[j], relations[i].Fields[j]);
								}
								this.AddRelation(pairs);
							}
						}
						if(!hasRels) {
							for(int i = 0; i < indexes.Count; i++) {
								IndexSerializationInfo indexInfo = indexes[i];
								InMemoryTable currentTable = this.tables[indexInfo.TableName];
								string indexName = currentTable.CreateIndex(indexInfo.Fields.ToArray(), true);
								if(indexInfo.PrimaryKey) {
									currentTable.SetPrimaryKey(indexName);
								}
							}
						}
					}
				}
				finally {
					EndUpdateSchema();
				}
				while(!(rdr.Name == "xs:schema" && rdr.NodeType == XmlNodeType.EndElement) && !rdr.EOF) {
					rdr.Read();
				}
				this.BeginTransaction();
				while(!rdr.EOF) {
					rdr.Read();
					if(rdr.NodeType == XmlNodeType.Whitespace) { rdr.Read(); }
					string tblName = XmlConvert.DecodeName(rdr.Name);
					if(!this.tables.ContainsKey(tblName)) { break; }
					InMemoryRow addedRow = null;
					for(; ; ) {
						rdr.Read();
						if(rdr.NodeType == XmlNodeType.Whitespace) { rdr.Read(); }
						string columnName = XmlConvert.DecodeName(rdr.Name);
						if(columnName == tblName && rdr.NodeType == XmlNodeType.EndElement) { break; }
						bool emptyElement = rdr.IsEmptyElement;
						InMemoryTable currentTable = tables[tblName];
						if(!currentTable.Columns.Contains(columnName)) { break; }
						if(addedRow == null) {
							addedRow = this.tables[tblName].Rows.AddNewRow();
							addedRow.BeginEdit();
						}
						var currentColumn = currentTable.Columns[columnName];
						var currentColumnType = currentColumn.Type;
						if(emptyElement) {
							if(currentColumnType == typeof(string))
								addedRow[currentColumn.ColumnIndex] = string.Empty;
							continue;
						}
						Type convertFromColumnType;
						convertFromTypeDictionary.TryGetValue(currentColumn, out convertFromColumnType);
						string columnTypeString;
						columnTypeStringDictionary.TryGetValue(currentColumn, out columnTypeString);
						addedRow[currentColumn.ColumnIndex] = ReadXmlValue(rdr, currentColumnType, convertFromColumnType, columnTypeString);
						while(!(XmlConvert.DecodeName(rdr.Name) == columnName && rdr.NodeType == XmlNodeType.EndElement) && !rdr.EOF) {
							rdr.Read();
						}
					}
					if(addedRow != null) addedRow.EndEdit();
					while(!(XmlConvert.DecodeName(rdr.Name) == tblName && rdr.NodeType == XmlNodeType.EndElement) && !rdr.EOF) {
						rdr.Read();
					}
				}
				this.Commit();
			}
			finally {
				isLoadingMode = false;
			}
		}
		static bool TryConvertFromHexBinary(string value, out byte[] result) {
			if(value == null) {
				result = null;
				return true;
			}
			if(value.Length % 2 != 0) {
				result = null;
				return false;
			}
			result = new byte[value.Length >> 1];
			for(int i = 0; i < value.Length; i += 2) {
				int major;
				int minor;
				if(!HexCharToInt(value[i], out major) || !HexCharToInt(value[i + 1], out minor)) {
					result = null;
					return false;
				}
				result[i >> 1] = (byte)((major << 4) + minor);
			}
			return true;
		}
#if DEBUGTEST
		public
#endif
		static bool HexCharToInt(char value, out int result) {
			int intValue = (int)value;
			if(intValue >= hexUpperAChar && intValue <= 'F') {
				result = intValue + 10 - hexUpperAChar;
				return true;
			}
			if(intValue >= hexLowerAChar && intValue <= 'f') {
				result = intValue + 10 - hexLowerAChar;
				return true;
			}
			if(intValue >= hexZeroChar && intValue <= '9') {
				result = intValue - hexZeroChar;
				return true;
			}
			result = -1;
			return false;
		}
		static object ReadXmlValue(XmlReader rdr, Type currentColumnType, Type convertFromColumnType, string columnTypeString) {
			TypeCode typeCode = DXTypeExtensions.GetTypeCode(currentColumnType);
			if(typeCode != TypeCode.Object) {
				rdr.Read();
				if(rdr.NodeType == XmlNodeType.Whitespace) { rdr.Read(); }
				switch(typeCode) {
					case TypeCode.Char:
						return rdr.Value[0];
					case TypeCode.UInt32:
						return XmlConvert.ToUInt32(rdr.Value);
					case TypeCode.Int16:
						return XmlConvert.ToInt16(rdr.Value);
					case TypeCode.SByte:
						return XmlConvert.ToInt16(rdr.Value);
					case TypeCode.UInt16:
						return XmlConvert.ToUInt16(rdr.Value);
					case TypeCode.UInt64:
						return XmlConvert.ToUInt64(rdr.Value);
					case TypeCode.String:
						return rdr.Value;
					case TypeCode.DateTime:
						return XmlConvert.ToDateTime(rdr.Value, XmlDateTimeSerializationMode.Unspecified);
					case TypeCode.Boolean:
						return XmlConvert.ToBoolean(rdr.Value.ToLowerInvariant());
					case TypeCode.Byte:
						return XmlConvert.ToByte(rdr.Value);
					case TypeCode.Decimal:
						return XmlConvert.ToDecimal(rdr.Value);
					case TypeCode.Single:
						return XmlConvert.ToSingle(rdr.Value);
					case TypeCode.Double:
						if(convertFromColumnType == typeof(TimeSpan)) {
							return XmlConvert.ToTimeSpan(rdr.Value).TotalSeconds;
						}
						return XmlConvert.ToDouble(rdr.Value);
					case TypeCode.Int32:
						return XmlConvert.ToInt32(rdr.Value);
					case TypeCode.Int64:
						return XmlConvert.ToInt64(rdr.Value);
#if !DXRESTRICTED
					case TypeCode.DBNull:
#endif
					case TypeCode.Empty:
						return null;
					default:
						throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_CantRestoreObjectFromXML));
				}
			}
			else {
				if(currentColumnType == typeof(TimeSpan)) {
					rdr.Read();
					if(rdr.NodeType == XmlNodeType.Whitespace) { rdr.Read(); }
					return XmlConvert.ToTimeSpan(rdr.Value);
				}
				else if(currentColumnType == typeof(byte[])) {
					rdr.Read();
					if(rdr.NodeType == XmlNodeType.Whitespace) { rdr.Read(); }
					if(columnTypeString == xsHexBinaryString) {
						byte[] binaryResult;
						if(TryConvertFromHexBinary(rdr.Value, out binaryResult)) {
							return binaryResult;
						}
					}
					return Convert.FromBase64String(rdr.Value);
				}
				else if(currentColumnType == typeof(Guid)) {
					rdr.Read();
					if(rdr.NodeType == XmlNodeType.Whitespace) { rdr.Read(); }
					return XmlConvert.ToGuid(rdr.Value);
				}
				else if(currentColumnType == typeof(object)) {
					string xsiType = rdr.GetAttribute("type", xsi);
					string instanceTypeStr = rdr.GetAttribute("InstanceType", msdata);
					if(!string.IsNullOrEmpty(xsiType)) {
						rdr.Read();
						if(rdr.NodeType == XmlNodeType.Whitespace) { rdr.Read(); }
						if(xsiType == xsHexBinaryString) {
							byte[] binaryResult;
							if(!TryConvertFromHexBinary(rdr.Value, out binaryResult)) {
								throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_CantRestoreObjectFromXML));
							}
							return binaryResult;
						}
						else if(xsiType == xsBase64String) {
							return Convert.FromBase64String(rdr.Value);
						}
						else {
							Type convertFromType;
							var currentType = GetTypeFromString(xsiType, out convertFromType);
							if(currentType == typeof(double) && convertFromType == typeof(TimeSpan)) {
								return XmlConvert.ToTimeSpan(rdr.Value).TotalSeconds;
							}
							return ((IConvertible)rdr.Value).ToType(currentType, CultureInfo.InvariantCulture);
						}
					}
					else if(!string.IsNullOrEmpty(instanceTypeStr)) {
						if(instanceTypeStr == "System.Char") {
							rdr.Read();
							if(rdr.NodeType == XmlNodeType.Whitespace) { rdr.Read(); }
							return rdr.Value[0];
						}
						else {
							Type instanceType = SafeTypeResolver.GetKnownUserType(instanceTypeStr);
							ConstructorInfo[] constructros = instanceType.GetConstructors();
							for(int i = 0; i < constructros.Length; i++) {
								if(constructros[i].GetParameters().Length == 0) {
									IXmlSerializable restoredObject = (IXmlSerializable)constructros[i].Invoke(null);
									restoredObject.ReadXml(rdr);
									return restoredObject;
								}
							}
						}
					}
				}
				else {
					ConstructorInfo[] constructros = currentColumnType.GetConstructors();
					for(int i = 0; i < constructros.Length; i++) {
						if(constructros[i].GetParameters().Length == 0) {
							IXmlSerializable restoredObject = (IXmlSerializable)constructros[i].Invoke(null);
							restoredObject.ReadXml(rdr);
							return restoredObject;
						}
					}
				}
				throw new XmlException(Res.GetString(Res.InMemorySet_XMLException_CantRestoreObjectFromXML));
			}
		}
		public void ReadFromInMemorySet(InMemorySet otherSet) {
			if(this.InTransaction || otherSet.InTransaction) throw new InvalidOperationException();
			this.ClearRelations();
			this.ClearTables();
			this.InMemorySetName = otherSet.InMemorySetName;
			isLoadingMode = true;
			try {
				BeginUpdateSchema();
				try {
					foreach(InMemoryTable table in otherSet.Tables) {
						InMemoryTable tbl = this.CreateTable(table.Name);
						foreach(InMemoryColumn column in table.Columns) {
							if(column.AutoIncrement) {
								tbl.Columns.Add(column.Name, column.Type, true);
							}
							else tbl.Columns.Add(column.Name, column.Type);
						}
						tbl.Columns.CommitColumnsChanges();
						foreach(InMemoryIndexWrapper index in table.Indexes) {
							InMemoryColumn[] columns = new InMemoryColumn[index.Columns.Count];
							for(int i = 0; i < columns.Length; i++) {
								columns[i] = tbl.Columns[index.Columns[i].ColumnIndex];
							}
							tbl.CreateIndex(columns, index.Unique);
							if(table.PrimaryKey != null) {
								tbl.SetPrimaryKey(table.PrimaryKey.Name);
							}
						}
					}
					foreach(InMemoryRelation relation in otherSet.Relations) {
						InMemoryTable pTable = this.tables[relation.PTable.Name];
						InMemoryTable fTable = this.tables[relation.FTable.Name];
						InMemoryRelationPair[] pairs = new InMemoryRelationPair[relation.Pairs.Length];
						for(int i = 0; i < relation.Pairs.Length; i++) {
							pairs[i] = new InMemoryRelationPair(pTable.Columns[relation.Pairs[i].PKey.ColumnIndex], fTable.Columns[relation.Pairs[i].FKey.ColumnIndex]);
						}
						this.AddRelation(pairs);
					}
				}
				finally {
					EndUpdateSchema();
				}
				this.BeginTransaction();
				try {
					foreach(InMemoryTable table in otherSet.Tables) {
						InMemoryTable tbl = this.tables[table.Name];
						int columnCount = table.Columns.Count;
						foreach(InMemoryRow row in table.Rows) {
							object[] data = new object[columnCount];
							for(int i = 0; i < columnCount; i++) {
								data[i] = row[i];
							}
							tbl.Rows.AddNewRow(data);
						}
					}
					this.Commit();
				}
				catch(Exception) {
					this.Rollback();
					throw;
				}
			}
			finally {
				isLoadingMode = false;
			}
		}
		struct IndexSerializationInfo {
			bool primaryKey;
			string name;
			public string TableName;
			List<InMemoryColumn> fields;
			public string Name { get { return name; } }
			public List<InMemoryColumn> Fields { get { return fields; } }
			public bool PrimaryKey { get { return primaryKey; } }
			public IndexSerializationInfo(string name, bool pk) {
				this.name = name;
				this.TableName = null;
				this.primaryKey = pk;
				this.fields = new List<InMemoryColumn>();
			}
		}
		struct RelationSerializationInfo {
			string name;
			string referedIndex;
			public string TableName;
			List<InMemoryColumn> fields;
			public string Name { get { return name; } }
			public string ReferredIndex { get { return referedIndex; } }
			public List<InMemoryColumn> Fields { get { return fields; } }
			public RelationSerializationInfo(string name, string referedIndex) {
				this.name = name;
				this.referedIndex = referedIndex;
				this.TableName = null;
				this.fields = new List<InMemoryColumn>();
			}
		}
		class UpdatingSchemaStatus {
			public bool FixRelations;
		}
	}
	public interface IInMemoryTable {
		string Name { get; }
		bool ExistsColumn(string columnName);
		IEnumerable<string> GetColumnNames();
	}
	public class InMemoryTable : IInMemoryTable {
		readonly string name;
		readonly InMemoryColumnList columns;
		readonly InMemoryRowList rows;
		readonly InMemorySet baseSet;
		InMemoryIndexWrapper primaryKey;
		readonly InMemoryIndexWrapperCollection indexWrappers;
		readonly Dictionary<string, IInMemoryIndex> indexes = new Dictionary<string, IInMemoryIndex>();
		ReadOnlyCollection<InMemoryRelation> pRelations;
		ReadOnlyCollection<InMemoryRelation> fRelations;
		ReadOnlyCollection<InMemoryRelation> allRelations;
		public InMemoryIndexWrapper PrimaryKey { get { return primaryKey; } }
		internal IEnumerable<IInMemoryIndex> IndexesInternal { get { return indexes.Values; } }
		public InMemoryIndexWrapperCollection Indexes { get { return indexWrappers; } }
		public string Name { get { return name; } }
		public InMemoryColumnList Columns { get { return columns; } }
		public InMemoryRowList Rows { get { return rows; } }
		public InMemorySet BaseSet { get { return baseSet; } }
		public ReadOnlyCollection<InMemoryRelation> PRelations { get { return pRelations; } }
		public ReadOnlyCollection<InMemoryRelation> FRelations { get { return fRelations; } }
		public ReadOnlyCollection<InMemoryRelation> AllRelations { get { return allRelations; } }
		public InMemoryTable(InMemorySet set, string name) {
			baseSet = set;
			this.name = name;
			columns = new InMemoryColumnList(this);
			rows = new InMemoryRowList(this);
			indexWrappers = new InMemoryIndexWrapperCollection(indexes);
			pRelations = new ReadOnlyCollection<InMemoryRelation>(Array.Empty<InMemoryRelation>());
			fRelations = new ReadOnlyCollection<InMemoryRelation>(Array.Empty<InMemoryRelation>());
			allRelations = new ReadOnlyCollection<InMemoryRelation>(Array.Empty<InMemoryRelation>());
		}
		public void SetPrimaryKey(string name) {
			if(string.IsNullOrEmpty(name)) {
				primaryKey = null;
				return;
			}
			primaryKey = new InMemoryIndexWrapper(indexes[name]);
		}
		public InMemoryColumn GetNewColumn(string name, Type type) {
			return new InMemoryColumn(this, name, type);
		}
		public string CreateIndex(InMemoryColumn[] columns, bool unique) {
			InMemoryDictionaryIndex index = new InMemoryDictionaryIndex(this, columns, unique);
			try {
				indexes.Add(index.Name, index);
			}
			catch(ArgumentException) {
				throw new InMemoryDuplicateNameException(index.Name);
			}
			try {
				FixIndexes();
				FillIndex(index);
			}
			catch(Exception) {
				throw;
			}
			return index.Name;
		}
		public bool RemoveIndex(string name) {
			IInMemoryIndex index;
			if(indexes.TryGetValue(name, out index)) {
				indexes.Remove(name);
				FixIndexes();
				return true;
			}
			return false;
		}
		public InMemoryRelation GetPRelation(string name) {
			InMemoryRelation rel = baseSet.GetRelation(name);
			if(rel.PTable == this) return rel;
			return null;
		}
		public InMemoryRelation GetFRelation(string name) {
			InMemoryRelation rel = baseSet.GetRelation(name);
			if(rel.FTable == this) return rel;
			return null;
		}
		InMemoryRelation[] GetPRelations() {
			List<InMemoryRelation> result = new List<InMemoryRelation>();
			foreach(InMemoryRelation rel in baseSet.Relations) {
				if(rel.PTable == this) result.Add(rel);
			}
			return result.ToArray();
		}
		InMemoryRelation[] GetFRelations() {
			List<InMemoryRelation> result = new List<InMemoryRelation>();
			foreach(InMemoryRelation rel in baseSet.Relations) {
				if(rel.FTable == this) result.Add(rel);
			}
			return result.ToArray();
		}
		InMemoryRelation[] GetAllRelations() {
			List<InMemoryRelation> result = new List<InMemoryRelation>();
			foreach(InMemoryRelation rel in baseSet.Relations) {
				if(rel.PTable == this || rel.FTable == this) result.Add(rel);
			}
			return result.ToArray();
		}
		internal void FixColumns() {
			for(int i = 0; i < rows.Count; i++) {
				rows[i].FixColumns();
			}
			foreach(IInMemoryIndex index in indexes.Values) {
				index.FixColumnsOrder();
			}
		}
		internal void FixRelations() {
			pRelations = new ReadOnlyCollection<InMemoryRelation>(GetPRelations());
			fRelations = new ReadOnlyCollection<InMemoryRelation>(GetFRelations());
			allRelations = new ReadOnlyCollection<InMemoryRelation>(GetAllRelations());
			for(int i = 0; i < columns.Count; i++) {
				columns[i].FixColumnRelations();
			}
		}
		void FixIndexes() {
			for(int i = 0; i < columns.Count; i++) {
				columns[i].FixIndexes();
			}
		}
		void FillIndex(IInMemoryIndex index) {
			for(int i = 0; i < rows.Count; i++) {
				index.AddRow(rows[i]);
			}
		}
		public override string ToString() {
			return name;
		}
		bool IInMemoryTable.ExistsColumn(string columnName) {
			return Columns.FindColumnIndex(columnName) >= 0;
		}
		IEnumerable<string> IInMemoryTable.GetColumnNames() {
			return Columns.Select(c => c.Name);
		}
	}
	public class InMemoryRelationCollection : List<InMemoryRelation> {
		public InMemoryRelationCollection(IEnumerable<InMemoryRelation> collection) : base(collection) { }
	}
	public class InMemoryRollBackOrderList : List<InMemoryRollbackOrderInfo> { }
	public class InMemoryRowList : IEnumerable<InMemoryRow> {
		InMemoryTable table;
		List<InMemoryRow> rows = new List<InMemoryRow>();
		public InMemoryRow this[int index] { get { return rows[index]; } }
		public int Count { get { return rows.Count; } }
		public InMemoryRowList(InMemoryTable table) {
			this.table = table;
		}
		public InMemoryRow AddNewRow(ICollection<object> initData) {
			InMemoryRow row = new InMemoryRow(table, initData);
			rows.Add(row);
			CommitIfNotInTransaction();
			return row;
		}
		void CommitIfNotInTransaction() {
			if(table.BaseSet.InTransaction) return;
			try {
				table.BaseSet.Commit();
			}
			catch {
				table.BaseSet.Rollback();
				throw;
			}
		}
		public InMemoryRow AddNewRow() {
			return AddNewRow(null);
		}
		public InMemoryRow InsertNewRow(int index, ICollection<object> initData) {
			InMemoryRow row = new InMemoryRow(table, initData);
			rows.Insert(index, row);
			CommitIfNotInTransaction();
			return row;
		}
		public InMemoryRow InsertNewRow(int index) {
			return InsertNewRow(index, null);
		}
		internal void RemoveInternal(InMemoryRow row) {
			rows.Remove(row);
			row.Dispose();
		}
		void RemoveAtInternal(int index, InMemoryRow row) {
			rows.RemoveAt(index);
			row.Dispose();
		}
		public bool Remove(InMemoryRow row) {
			row.Delete();
			CommitIfNotInTransaction();
			return true;
		}
		public void RemoveAt(int index) {
			InMemoryRow row = this[index];
			row.Delete();
			CommitIfNotInTransaction();
		}
		public void Clear() {
			for(int i = 0; i < rows.Count; i++) {
				rows[i].Dispose();
			}
			rows.Clear();
			foreach(InMemoryColumn column in table.Columns) {
				if(!column.Indexes.HasIndexes) continue;
				column.Indexes.Clear();
			}
		}
		public InMemoryRow FindFirst(InMemoryColumn column, object value) {
			return FindFirst(new InMemoryColumn[] { column }, new object[] { value });
		}
		public InMemoryRow[] Find(InMemoryColumn column, object value) {
			return Find(new InMemoryColumn[] { column }, new object[] { value });
		}
		public InMemoryRow[] FindWithDeleted(InMemoryColumn column, object value) {
			return FindWithDeleted(new InMemoryColumn[] { column }, new object[] { value });
		}
		public InMemoryRow FindFirst(InMemoryColumn[] columns, object[] values) {
			InMemoryRow[] currentRows = Find(columns, values, true, false);
			if(currentRows.Length == 0) return null;
			return currentRows[0];
		}
		public InMemoryRow[] Find(InMemoryColumn[] columns, object[] values) {
			return Find(columns, values, false, false);
		}
		public InMemoryRow[] FindWithDeleted(InMemoryColumn[] columns, object[] values) {
			return Find(columns, values, false, true);
		}
		InMemoryRow[] Find(InMemoryColumn[] columns, object[] values, bool findFirst, bool returnDeleted) {
			foreach(IInMemoryIndex index in table.IndexesInternal) {
				if(index.EqualsColumns(columns))
					return index.Find(values, returnDeleted);
			}
			return InMemoryHelper.FindInRows(rows, columns, values, findFirst, returnDeleted, table.BaseSet.CaseSensitive).ToArray();
		}
		public InMemoryIndexWrapper FindIndex(InMemoryColumn[] columns) {
			foreach(IInMemoryIndex index in table.IndexesInternal) {
				if(index.EqualsColumns(columns))
					return index.Wrapper;
			}
			return null;
		}
		public IEnumerator<InMemoryRow> GetEnumerator() {
			for(int i = 0; i < rows.Count; i++) {
				yield return rows[i];
			}
		}
		IEnumerator IEnumerable.GetEnumerator() {
			for(int i = 0; i < rows.Count; i++) {
				yield return rows[i];
			}
		}
	}
	sealed class StringComparer : IEqualityComparer<string> {
		public bool Equals(string x, string y) {
			return x == y;
		}
		public int GetHashCode(string obj) {
			return obj.GetHashCode();
		}
		public static StringComparer Default = new StringComparer();
	}
	public class InMemoryColumnList : IEnumerable<InMemoryColumn> {
		InMemoryTable table;
		Dictionary<string, int> indexDict = new Dictionary<string, int>(StringComparer.Default);
		List<InMemoryColumn> columns = new List<InMemoryColumn>();
		public InMemoryTable Table { get { return table; } }
		public InMemoryColumn this[int index] { get { return columns[index]; } }
		public InMemoryColumn this[string name] { get { return FindColunm(name); } }
		public int Count { get { return columns.Count; } }
		public InMemoryColumnList(InMemoryTable table) {
			this.table = table;
		}
		public void Add(string name, Type type) {
			if(indexDict.ContainsKey(name)) throw new InMemoryDuplicateNameException(name);
			AddColumn(new InMemoryColumn(table, name, type));
		}
		public void Add(string name, Type type, bool autoIncrement) {
			if(indexDict.ContainsKey(name)) throw new InMemoryDuplicateNameException(name);
			AddColumn(new InMemoryColumn(table, name, type, autoIncrement));
		}
		public void Add(string name, Type type, bool allowNull, object defaultValue) {
			if(indexDict.ContainsKey(name)) throw new InMemoryDuplicateNameException(name);
			AddColumn(new InMemoryColumn(table, name, type, allowNull, defaultValue));
		}
		public void Insert(int index, string name, Type type) {
			if(indexDict.ContainsKey(name)) throw new InMemoryDuplicateNameException(name);
			InsertColumn(index, new InMemoryColumn(table, name, type));
		}
		public void Insert(int index, string name, Type type, bool autoIncrement) {
			if(indexDict.ContainsKey(name)) throw new InMemoryDuplicateNameException(name);
			InsertColumn(index, new InMemoryColumn(table, name, type, autoIncrement));
		}
		public void Insert(int index, string name, Type type, bool allowNull, object defaultValue) {
			if(indexDict.ContainsKey(name)) throw new InMemoryDuplicateNameException(name);
			InsertColumn(index, new InMemoryColumn(table, name, type, allowNull, defaultValue));
		}
		void AddColumn(InMemoryColumn column) {
			indexDict[column.Name] = columns.Count;
			columns.Add(column);
		}
		void InsertColumn(int index, InMemoryColumn column) {
			columns.Insert(index, column);
			for(int i = index; i < columns.Count; i++) {
				indexDict[columns[i].Name] = i;
			}
		}
		void UpdateIndexDict() {
			indexDict.Clear();
			for(int i = 0; i < columns.Count; i++) {
				indexDict[columns[i].Name] = i;
			}
		}
		public int FindColumnIndex(string name) {
			int index;
			if(indexDict.TryGetValue(name, out index)) {
				return index;
			}
			return -1;
		}
		InMemoryColumn FindColunm(string name) {
			int index = FindColumnIndex(name);
			if(index < 0) return null;
			return columns[index];
		}
		public IEnumerator<InMemoryColumn> GetEnumerator() {
			return columns.GetEnumerator();
		}
		IEnumerator IEnumerable.GetEnumerator() {
			return columns.GetEnumerator();
		}
		public void SetAllDeleted() {
			foreach(InMemoryColumn column in columns) {
				column.Delete();
			}
		}
		public bool Contains(InMemoryColumn column) {
			for(int i = 0; i < columns.Count; i++) {
				if(columns[i] == column) {
					return true;
				}
			}
			return false;
		}
		public bool Contains(string columnName) {
			return indexDict.ContainsKey(columnName);
		}
		public void CommitColumnsChanges() {
			foreach(InMemoryColumn currentColumn in columns) {
				if(currentColumn.State != InMemoryItemState.Updated) continue;
				if(currentColumn.ColumnForUpdate.Name != currentColumn.Name && FindColumnIndex(currentColumn.ColumnForUpdate.Name) >= 0) throw new InMemoryDuplicateNameException(currentColumn.ColumnForUpdate.Name);
				if((currentColumn.FRelations != null && (currentColumn.FRelations.Count > 0))
					|| (currentColumn.PRelations != null && (currentColumn.PRelations.Count > 0))) throw new InvalidOperationException(Res.GetString(Res.InMemorySet_CantUpdateRelationColumn));
			}
			table.FixColumns();
			List<InMemoryColumn> deleteColumns = new List<InMemoryColumn>();
			for(int i = 0; i < columns.Count; i++) {
				InMemoryColumn column = columns[i];
				switch(column.State) {
					case InMemoryItemState.Updated:
						if(column.ColumnForUpdate != null) {
							column = column.ColumnForUpdate;
							columns[i] = column;
						}
						break;
					case InMemoryItemState.Deleted:
						deleteColumns.Add(column);
						columns.RemoveAt(i);
						i--;
						continue;
				}
			}
			UpdateIndexDict();
			for(int i = 0; i < columns.Count; i++) {
				columns[i].ColumnsCommitted();
			}
			if(deleteColumns.Count > 0) {
				table.BaseSet.RemoveRelations(deleteColumns);
			}
		}
		public InMemoryColumn[] ToArray() {
			return columns.ToArray();
		}
	}
	public class InMemoryRollbackItem {
		public bool Updated;
		public object Data;
		public void Reset() {
			Updated = false;
			Data = null;
		}
	}
	public class InMemoryRollbackOrderInfo {
		InMemoryItemState oldState;
		InMemoryItemState newState;
		InMemoryRow row;
		bool isNewRow;
		public bool IsNewRow { get { return isNewRow; } }
		public InMemoryRow Row { get { return row; } }
		public InMemoryItemState OldState { get { return oldState; } }
		public InMemoryItemState NewState { get { return newState; } }
		public InMemoryRollbackOrderInfo(InMemoryRow row, InMemoryItemState oldState, InMemoryItemState newState, bool isNewRow) {
			this.row = row;
			this.oldState = oldState;
			this.newState = newState;
			this.isNewRow = isNewRow;
		}
	}
	public struct InMemoryAutoIncrementValue {
		static InMemoryAutoIncrementValue value = new InMemoryAutoIncrementValue();
		public static InMemoryAutoIncrementValue Value {
			get { return value; }
		}
	}
	public class InMemoryRowModificationInfo {
		public object[] OldData;
		public bool[] Modified;
		public InMemoryRowModificationInfo() { }
		public InMemoryRowModificationInfo(object[] data) {
			OldData = new object[data.Length];
			data.CopyTo(OldData, 0);
			Modified = new bool[data.Length];
		}
	}
	public interface IInMemoryRow {
		IInMemoryTable Table { get; }
		object this[int columnIndex] { get; set; }
		object this[string columnName] { get; set; }
		void BeginEdit();
		void EndEdit();
		void CancelEdit();
	}
	public class InMemoryRow : IDisposable, IInMemoryRow {
		private const string StringValueIsTooLong = "String value is too long.";
		private const string ColumnIsAutoincremented = "Column is autoincremented.";
		private const string RowIsDeleted = "Row is deleted.";
		private const string NotInEditMode = "Not in edit mode.";
		InMemoryTable table;
		object[] data;
		InMemoryRowModificationInfo editInfo;
		InMemoryRowModificationInfo modificationInfo;
		InMemoryItemState state = InMemoryItemState.Inserted;
		public InMemoryItemState State { get { return state; } }
		public InMemoryTable Table { get { return table; } }
		IInMemoryTable IInMemoryRow.Table { get { return Table; } }
		public bool EditMode { get { return editInfo != null; } }
		public InMemoryRow(InMemoryTable table) : this(table, null) { }
		public InMemoryRow(InMemoryTable table, ICollection<object> initData) {
			this.table = table;
			if(initData == null) {
				this.data = new object[table.Columns.Count];
				this.modificationInfo = new InMemoryRowModificationInfo(data);
				CheckValues(true, true);
			}
			else {
				if(table.Columns.Count != initData.Count) throw new InMemorySetException(Res.GetString(Res.InMemorySet_WrongInitData));
				this.data = new object[initData.Count];
				initData.CopyTo(data, 0);
				this.modificationInfo = new InMemoryRowModificationInfo(data);
				CheckValues(false, true);
			}
			if(!table.BaseSet.IsLoadingMode) {
				for(int i = 0; i < data.Length; i++) {
					if(table.Columns[i].AutoIncrement) {
						data[i] = table.Columns[i].GetAutoIncrementNextValue();
					}
				}
			}
			try {
				AddToIndex();
			}
			catch {
				RemoveFromIndex();
				throw;
			}
			table.BaseSet.RollbackOrder.Add(new InMemoryRollbackOrderInfo(this, InMemoryItemState.Default, InMemoryItemState.Inserted, true));
		}
		void CheckValues(bool setDefaults, bool checkAutoIncrement) {
			for(int i = 0; i < data.Length; i++) {
				InMemoryColumn column = table.Columns[i];
				if(setDefaults) data[i] = column.DefaultValue;
				else {
					if(column.AutoIncrement) {
						if(!checkAutoIncrement) continue;
						if(table.BaseSet.IsLoadingMode) column.UpdateAutoIncrementValue(data[i]);
						else
							if(!(data[i] is InMemoryAutoIncrementValue)) throw new InMemorySetException(string.Format(Res.GetString(Res.InMemorySet_InsertingDataIntoAutoincrementColumn), column.Name));
					}
					if(data[i] == null) {
						if(!column.AllowNull) throw new InMemoryNoNullAllowedException();
					}
					else {
						if(!column.AutoIncrement && !column.Type.IsInstanceOfType(data[i])) {
							data[i] = InMemoryHelper.Convert(data[i], data[i].GetType(), column.Type, false);
						}
						if(column.Type.Equals(typeof(string)) && column.MaxLength > 0 && ((string)data[i]).Length > column.MaxLength) throw new InMemorySetException(StringValueIsTooLong);
					}
				}
			}
		}
		internal void CheckAndEnterFRelations() {
			CheckAndEnterFRelations(data);
		}
		internal void CheckAndEnterFRelationsOldData() {
			CheckAndEnterFRelations(modificationInfo.OldData);
		}
		void CheckAndEnterFRelations(object[] newData) {
			InMemoryRow[][] enterRows = new InMemoryRow[table.FRelations.Count][];
			for(int i = 0; i < table.FRelations.Count; i++) {
				enterRows[i] = table.FRelations[i].CheckFAssociation(newData);
			}
			for(int i = 0; i < table.FRelations.Count; i++) {
				table.FRelations[i].EnterFAssociation(enterRows[i]);
			}
		}
		internal void CheckPRelations() {
			CheckPRelations(data, null);
		}
		internal void CheckPRelationsModified() {
			CheckPRelations(modificationInfo.OldData, modificationInfo.Modified);
		}
		void CheckPRelations(object[] oldDataList, bool[] modifiedList) {
			for(int i = 0; i < table.PRelations.Count; i++) {
				table.PRelations[i].CheckPAssociation(this, oldDataList, modifiedList);
			}
		}
		internal void LeaveFRelations() {
			LeaveFRelations(data);
		}
		internal void LeaveFRelationsOldData() {
			LeaveFRelations(modificationInfo.OldData);
		}
		void LeaveFRelations(object[] leaveData) {
			for(int i = 0; i < table.FRelations.Count; i++) {
				table.FRelations[i].LeaveFAssociation(this, leaveData);
			}
		}
		public object this[int columnIndex] {
			get { return data[columnIndex]; }
			set { SetData(columnIndex, value); }
		}
		void AddToIndex() {
			foreach(IInMemoryIndex index in table.IndexesInternal) {
				index.AddRow(this);
			}
		}
		void RemoveFromIndex() {
			foreach(IInMemoryIndex index in table.IndexesInternal) {
				index.RemoveRow(this);
			}
		}
		public void BeginEdit() {
			if(editInfo != null) throw new InMemorySetException(Res.GetString(Res.InMemorySet_InEditMode));
			if(state == InMemoryItemState.Deleted) throw new InMemorySetException(RowIsDeleted);
			editInfo = new InMemoryRowModificationInfo(data);
		}
		public void EndEdit() {
			if(editInfo == null) throw new InMemorySetException(NotInEditMode);
			if(state == InMemoryItemState.Deleted) throw new InMemorySetException(RowIsDeleted);
			CheckValues(false, false);
			object[] newData = data;
			data = editInfo.OldData;
			RemoveFromIndex();
			data = newData;
			try {
				AddToIndex();
			}
			catch(Exception) {
				RemoveFromIndex();
				data = editInfo.OldData;
				AddToIndex();
				data = newData;
				throw;
			}
			InMemoryRowModificationInfo tModificationInfo = null;
			if(modificationInfo != null) {
				tModificationInfo = new InMemoryRowModificationInfo(modificationInfo.OldData);
				for(int i = 0; i < modificationInfo.Modified.Length; i++) {
					if(editInfo.Modified[i]) {
						if(table.Columns[i].AutoIncrement && !table.BaseSet.IsLoadingMode) throw new InMemorySetException(ColumnIsAutoincremented);
						modificationInfo.Modified[i] = true;
					}
				}
			}
			if(modificationInfo == null) {
				modificationInfo = new InMemoryRowModificationInfo(editInfo.OldData);
				if(table.BaseSet.InTransaction) {
					SetRollbackData(state, InMemoryItemState.Updated);
					state = InMemoryItemState.Updated;
				}
				LeaveFRelationsOldData();
			}
			if(!table.BaseSet.InTransaction) {
				try {
					CheckAndEnterFRelations();
					AcceptChanges(null);
				}
				catch {
					CheckAndEnterFRelationsOldData();
					modificationInfo = tModificationInfo;
					throw;
				}
			}
			editInfo = null;
		}
		public void CancelEdit() {
			if(editInfo == null) throw new InMemorySetException(NotInEditMode);
			data = editInfo.OldData;
			editInfo = null;
		}
		void SetData(int index, object value) {
			if(state == InMemoryItemState.Deleted) throw new InMemorySetException(RowIsDeleted);
			InMemoryColumn column = table.Columns[index];
			if(column.AutoIncrement) {
				if(table.BaseSet.IsLoadingMode) column.UpdateAutoIncrementValue(value);
				else throw new InMemorySetException(ColumnIsAutoincremented);
			}
			if(editInfo != null) {
				data[index] = value;
				editInfo.Modified[index] = true;
				return;
			}
			object oldValue = data[index];
			object newValue = value;
			if(newValue == null) {
				if(!column.AllowNull) throw new InMemoryNoNullAllowedException();
			}
			else {
				if(!column.Type.IsInstanceOfType(newValue)) {
					newValue = InMemoryHelper.Convert(newValue, newValue.GetType(), column.Type, false);
				}
				if(column.Type.Equals(typeof(string)) && column.MaxLength > 0 && ((string)newValue).Length > column.MaxLength) throw new InMemorySetException(StringValueIsTooLong);
			}
			if(column.Indexes.HasIndexes) {
				column.Indexes.RemoveRow(this);
				data[index] = newValue;
				try {
					column.Indexes.AddRow(this);
				}
				catch(Exception) {
					column.Indexes.RemoveRow(this);
					data[index] = oldValue;
					column.Indexes.AddRow(this);
					throw;
				}
			}
			if(ModifyOneValue(column, oldValue, newValue)) {
				LeaveFRelationsOldData();
			}
			if(!table.BaseSet.InTransaction) {
				try {
					CheckAndEnterFRelations();
					AcceptChanges(column);
				}
				catch {
					CheckAndEnterFRelationsOldData();
					RejectChanges();
					throw;
				}
			}
		}
		bool ModifyOneValue(InMemoryColumn column, object oldValue, object newValue) {
			data[column.ColumnIndex] = newValue;
			if(modificationInfo == null) {
				modificationInfo = new InMemoryRowModificationInfo(data);
				modificationInfo.Modified[column.ColumnIndex] = true;
				modificationInfo.OldData[column.ColumnIndex] = oldValue;
				if(table.BaseSet.InTransaction) {
					SetRollbackData(state, InMemoryItemState.Updated);
					state = InMemoryItemState.Updated;
				}
				return true;
			}
			return false;
		}
		void SetRollbackData(InMemoryItemState oldState, InMemoryItemState newState) {
			table.BaseSet.RollbackOrder.Add(new InMemoryRollbackOrderInfo(this, oldState, newState, false));
		}
		void AcceptChanges(InMemoryColumn column) {
			if(modificationInfo == null) return;
			if(column != null) {
				if(modificationInfo.Modified[column.ColumnIndex]) column.CheckPRelations(this, modificationInfo.OldData[column.ColumnIndex]);
				column.CheckFRelations(this, data[column.ColumnIndex]);
			}
			modificationInfo = null;
		}
		void RejectChanges() {
			if(modificationInfo == null) return;
			RemoveFromIndex();
			data = modificationInfo.OldData;
			AddToIndex();
			modificationInfo = null;
		}
		public object this[string columnName] {
			get {
				InMemoryColumn column = table.Columns[columnName];
				if(column == null) throw new InMemorySetException(string.Format(Res.GetString(Res.InMemorySet_ColumnNotFound), columnName));
				return this[column.ColumnIndex];
			}
			set {
				InMemoryColumn column = table.Columns[columnName];
				if(column == null) throw new InMemorySetException(string.Format(Res.GetString(Res.InMemorySet_ColumnNotFound), columnName));
				this[column.ColumnIndex] = value;
			}
		}
		T[] ArrayInsert<T>(T[] array, int index, T item) {
			T[] res = new T[array.Length + 1];
			if(index != 0)
				Array.Copy(array, 0, res, 0, index);
			res[index] = item;
			if(index != array.Length)
				Array.Copy(array, index, res, index + 1, array.Length - index);
			return res;
		}
		T[] ArrayRemoveAt<T>(T[] array, int index) {
			T[] res = new T[array.Length - 1];
			if(index != 0)
				Array.Copy(array, 0, res, 0, index);
			if(index != array.Length - 1)
				Array.Copy(array, index + 1, res, index, array.Length - index - 1);
			return res;
		}
		internal void FixColumns() {
			int shift = 0;
			for(int i = 0; i < table.Columns.Count; i++) {
				InMemoryColumn column = table.Columns[i];
				int iShifted = i + shift;
				switch(column.State) {
					case InMemoryItemState.Deleted:
						if(modificationInfo == null) modificationInfo = new InMemoryRowModificationInfo(data);
						data = ArrayRemoveAt(data, iShifted);
						modificationInfo.Modified = ArrayRemoveAt(modificationInfo.Modified, iShifted);
						modificationInfo.OldData = ArrayRemoveAt(modificationInfo.OldData, iShifted);
						if(editInfo != null) {
							editInfo.OldData = ArrayRemoveAt(editInfo.OldData, iShifted);
							editInfo.Modified = ArrayRemoveAt(editInfo.Modified, iShifted);
						}
						shift--;
						break;
					case InMemoryItemState.Updated:
						data[iShifted] = InMemoryHelper.Convert(data[iShifted], column);
						if(modificationInfo != null && modificationInfo.OldData != null) {
							modificationInfo.OldData[iShifted] = InMemoryHelper.Convert(modificationInfo.OldData[iShifted], column);
						}
						if(editInfo != null) {
							editInfo.OldData[iShifted] = InMemoryHelper.Convert(editInfo.OldData[iShifted], column);
						}
						break;
					case InMemoryItemState.Inserted:
						if(modificationInfo == null) modificationInfo = new InMemoryRowModificationInfo(data);
						data = ArrayInsert(data, iShifted, column.DefaultValue);
						modificationInfo.Modified = ArrayInsert(modificationInfo.Modified, iShifted, false);
						modificationInfo.OldData = ArrayInsert(modificationInfo.OldData, iShifted, column.DefaultValue);
						if(editInfo != null) {
							editInfo.OldData = ArrayInsert(editInfo.OldData, iShifted, column.DefaultValue);
							editInfo.Modified = ArrayInsert(editInfo.Modified, iShifted, false);
						}
						break;
				}
			}
		}
		internal void Commit(InMemoryRollbackOrderInfo info) {
			Guard.ArgumentNotNull(info, nameof(info));
			if(info.Row != this) throw new InMemorySetException(Res.GetString(Res.InMemorySet_WrongCommitInfo));
			if(state == InMemoryItemState.Default) return;
			switch(info.NewState) {
				case InMemoryItemState.Updated:
				case InMemoryItemState.Inserted:
					AcceptChanges(null);
					break;
			}
			state = InMemoryItemState.Default;
		}
		internal void Rollback(InMemoryRollbackOrderInfo info) {
			Guard.ArgumentNotNull(info, nameof(info));
			if(info.Row != this) throw new InMemorySetException(Res.GetString(Res.InMemorySet_WrongRollbackInfo));
			if(state == InMemoryItemState.Default) return;
			switch(info.NewState) {
				case InMemoryItemState.Updated:
					RejectChanges();
					state = info.OldState;
					return;
				case InMemoryItemState.Deleted:
					state = info.OldState;
					return;
			}
			state = InMemoryItemState.Default;
		}
		internal void Delete() {
			if(state != InMemoryItemState.Deleted) {
				LeaveFRelations();
				table.BaseSet.RollbackOrder.Add(new InMemoryRollbackOrderInfo(this, state, InMemoryItemState.Deleted, false));
				state = InMemoryItemState.Deleted;
			}
		}
		public void Dispose() {
			RemoveFromIndex();
			table = null;
			data = null;
		}
		public override string ToString() {
			StringBuilder sb = new StringBuilder();
			sb.Append("{ ");
			for(int i = 0; i < data.Length; i++) {
				if(i > 0) sb.Append(", ");
				string s;
				if(data[i] == null) s = "null";
				else s = data[i].ToString();
				if(s.Length > 100) {
					sb.Append(s.AsSpan(0, 100));
					sb.Append("...");
					continue;
				}
				sb.Append(s);
			}
			sb.Append(" }");
			return sb.ToString();
		}
	}
	public class InMemoryColumn {
		string name;
		int columnIndex;
		Type type;
		bool allowNull;
		bool autoIncrement;
		object autoIncrementNextValue;
		object defaultValue;
		int maxLength;
		InMemoryTable table;
		InMemoryItemState state = InMemoryItemState.Inserted;
		InMemoryColumn columnForUpdate;
		ReadOnlyCollection<InMemoryRelation> pRelations;
		ReadOnlyCollection<InMemoryRelation> fRelations;
		InMemoryIndexesHolder indexes = new InMemoryIndexesHolder(null);
		public string Name { get { return name; } }
		public Type Type { get { return type; } }
		public bool AllowNull { get { return allowNull; } }
		public object DefaultValue { get { return defaultValue; } }
		public InMemoryTable Table { get { return table; } }
		public InMemoryItemState State { get { return state; } }
		public int ColumnIndex { get { return columnIndex; } }
		public InMemoryColumn ColumnForUpdate { get { return columnForUpdate; } }
		public ReadOnlyCollection<InMemoryRelation> PRelations { get { return pRelations; } }
		public ReadOnlyCollection<InMemoryRelation> FRelations { get { return fRelations; } }
		internal InMemoryIndexesHolder Indexes { get { return indexes; } }
		public bool HasIndexes { get { return indexes.Count > 0; } }
		public int MaxLength {
			get { return maxLength; }
			set { maxLength = value; }
		}
		public bool AutoIncrement { get { return autoIncrement; } }
		public InMemoryColumn(InMemoryTable table, string name, Type type) : this(table, name, type, false, true, null) { }
		public InMemoryColumn(InMemoryTable table, string name, Type type, bool autoIncrement) : this(table, name, type, autoIncrement, !autoIncrement, null) { }
		public InMemoryColumn(InMemoryTable table, string name, Type type, bool allowNull, object defaultValue) : this(table, name, type, false, allowNull, defaultValue) { }
		InMemoryColumn(InMemoryTable table, string name, Type type, bool autoIncrement, bool allowNull, object defaultValue) {
			this.autoIncrement = autoIncrement;
			if(autoIncrement) {
				if(allowNull) throw new InMemoryNoAutoIncrementAllowedException();
				switch(DXTypeExtensions.GetTypeCode(type)) {
					case TypeCode.Byte:
					case TypeCode.Int16:
					case TypeCode.Int32:
					case TypeCode.Int64:
					case TypeCode.SByte:
					case TypeCode.UInt16:
					case TypeCode.UInt32:
					case TypeCode.UInt64:
						break;
					default:
						throw new InMemoryNoAutoIncrementAllowedException();
				}
				autoIncrementNextValue = ((IConvertible)1).ToType(type, CultureInfo.InvariantCulture);
				defaultValue = InMemoryAutoIncrementValue.Value;
			}
			else {
				if(defaultValue == null) {
					if(!allowNull) {
						if(table.Rows.Count != 0) {
							throw new InMemoryNoNullAllowedException(string.Format(Res.GetString(Res.InMemorySet_NullDefaultValueNotAllowed), name));
						}
					}
				}
				else {
					if(!type.IsInstanceOfType(defaultValue)) {
						defaultValue = InMemoryHelper.Convert(defaultValue, defaultValue.GetType(), type, true);
					}
				}
			}
			this.table = table;
			this.name = name;
			this.type = type;
			this.allowNull = allowNull;
			this.defaultValue = defaultValue;
			this.pRelations = new ReadOnlyCollection<InMemoryRelation>(Array.Empty<InMemoryRelation>());
			this.fRelations = new ReadOnlyCollection<InMemoryRelation>(Array.Empty<InMemoryRelation>());
		}
		internal void UpdateAutoIncrementValue(object value) {
			if(!table.BaseSet.IsLoadingMode) throw new InvalidOperationException(string.Format(Res.GetString(Res.InMemorySet_NotLoadingMode), name));
			object newValue = IncrementNum(value);
			if(Comparer<object>.Default.Compare(autoIncrementNextValue, newValue) < 0) {
				autoIncrementNextValue = newValue;
			}
		}
		public object GetAutoIncrementNextValue() {
			object result = autoIncrementNextValue;
			autoIncrementNextValue = IncrementNum(autoIncrementNextValue);
			return result;
		}
		object IncrementNum(object value) {
			if(value == null) return null;
			switch(DXTypeExtensions.GetTypeCode(type)) {
				case TypeCode.Byte:
					return ((Byte)value) + 1;
				case TypeCode.Int16:
					return ((Int16)value) + 1;
				case TypeCode.Int32:
					return ((Int32)value) + 1;
				case TypeCode.Int64:
					return ((Int64)value) + 1;
				case TypeCode.SByte:
					return ((SByte)value) + 1;
				case TypeCode.UInt16:
					return ((UInt16)value) + 1;
				case TypeCode.UInt32:
					return ((UInt32)value) + 1;
				case TypeCode.UInt64:
					return ((UInt64)value) + 1;
			}
			throw new ArgumentException("IncrementValue");
		}
		public void Delete() {
			state = InMemoryItemState.Deleted;
		}
		public void Update(string name, Type type) {
			state = InMemoryItemState.Updated;
			columnForUpdate = new InMemoryColumn(table, name, type);
		}
		public void Update(string name, Type type, bool autoIncrement) {
			state = InMemoryItemState.Updated;
			columnForUpdate = new InMemoryColumn(table, name, type, autoIncrement);
		}
		public void Update(string name, Type type, bool allowNull, object defaultValue) {
			state = InMemoryItemState.Updated;
			columnForUpdate = new InMemoryColumn(table, name, type, allowNull, defaultValue);
		}
		public void Reset() {
			state = InMemoryItemState.Default;
			columnForUpdate = null;
		}
		internal void ColumnsCommitted() {
			columnIndex = table.Columns.FindColumnIndex(name);
			Reset();
		}
		internal void FixColumnRelations() {
			pRelations = new ReadOnlyCollection<InMemoryRelation>(table.BaseSet.GetPRelations(this));
			fRelations = new ReadOnlyCollection<InMemoryRelation>(table.BaseSet.GetFRelations(this));
		}
		internal void FixIndexes() {
			List<IInMemoryIndex> indexList = new List<IInMemoryIndex>();
			foreach(IInMemoryIndex index in table.IndexesInternal) {
				foreach(InMemoryColumn column in index.Columns) {
					if(column == this) {
						indexList.Add(index);
						break;
					}
				}
			}
			indexes = new InMemoryIndexesHolder(indexList);
		}
		public void CheckPRelations(InMemoryRow row, object oldValue) {
			if(pRelations == null) return;
			foreach(InMemoryRelation rel in pRelations) {
				rel.CheckPAssociation(this, row, oldValue);
			}
		}
		public void CheckFRelations(InMemoryRow row, object newValue) {
			if(fRelations == null) return;
			InMemoryRow[][] enterRows = new InMemoryRow[fRelations.Count][];
			for(int i = 0; i < fRelations.Count; i++) {
				enterRows[i] = fRelations[i].CheckFAssociation(this, row, newValue);
			}
			for(int i = 0; i < fRelations.Count; i++) {
				fRelations[i].EnterFAssociation(enterRows[i]);
			}
		}
		public override string ToString() {
			return name;
		}
		public override bool Equals(object obj) {
			if(obj == null) return false;
			InMemoryColumn column = obj as InMemoryColumn;
			if(column == null) return false;
			return table.Equals(column.table) && name.Equals(column.name) && type.Equals(column.type);
		}
		public override int GetHashCode() {
			return HashCodeHelper.CalculateGeneric(table, name, type);
		}
	}
	public class InMemoryColumnIndexComparer : IComparer<InMemoryColumn> {
		public int Compare(InMemoryColumn x, InMemoryColumn y) {
			if(x == y) return 0;
			Guard.ArgumentNotNull(x, nameof(x));
			Guard.ArgumentNotNull(y, nameof(y));
			return x.ColumnIndex.CompareTo(y.ColumnIndex);
		}
	}
	public class InMemoryRelationPairPNameComparer : IComparer<InMemoryRelationPair> {
		public int Compare(InMemoryRelationPair x, InMemoryRelationPair y) {
			if(x == y) return 0;
			Guard.ArgumentNotNull(x, nameof(x));
			Guard.ArgumentNotNull(y, nameof(y));
			return x.PKey.Name.CompareTo(y.PKey.Name);
		}
	}
	public class InMemoryRelationPairPIndexComparer : IComparer<InMemoryRelationPair> {
		public int Compare(InMemoryRelationPair x, InMemoryRelationPair y) {
			if(x == y) return 0;
			Guard.ArgumentNotNull(x, nameof(x));
			Guard.ArgumentNotNull(y, nameof(y));
			return x.PKey.ColumnIndex.CompareTo(y.PKey.ColumnIndex);
		}
	}
	public class InMemoryRelationPairFIndexComparer : IComparer<InMemoryRelationPair> {
		public int Compare(InMemoryRelationPair x, InMemoryRelationPair y) {
			if(x == y) return 0;
			Guard.ArgumentNotNull(x, nameof(x));
			Guard.ArgumentNotNull(y, nameof(y));
			return x.FKey.ColumnIndex.CompareTo(y.FKey.ColumnIndex);
		}
	}
	sealed class IntComparer : IEqualityComparer<int> {
		public bool Equals(int x, int y) {
			return x == y;
		}
		public int GetHashCode(int obj) {
			return obj;
		}
		static public IntComparer Default = new IntComparer();
	}
	public class InMemoryDictionaryIndex : IInMemoryIndex {
		string name;
		readonly bool caseSensitive;
		bool unique;
		InMemoryTable table;
		InMemoryColumn[] columns;
		InMemoryIndexWrapper wrapper;
		ReadOnlyCollection<InMemoryColumn> columnsReadOnly;
		Dictionary<int, object> dictionary = new Dictionary<int, object>(IntComparer.Default);
		HashSet<InMemoryRow> nullList = new HashSet<InMemoryRow>();
		public string Name { get { return name; } }
		public bool Unique { get { return unique; } }
		public ReadOnlyCollection<InMemoryColumn> Columns { get { return columnsReadOnly; } }
		public InMemoryDictionaryIndex(InMemoryTable table, InMemoryColumn[] columns, bool unique) {
			if(columns == null || columns.Length == 0)
				throw new ArgumentException(null, nameof(columns));
			this.table = table;
			this.caseSensitive = table.BaseSet.CaseSensitive;
			this.columns = columns;
			this.columnsReadOnly = new ReadOnlyCollection<InMemoryColumn>(columns);
			FixColumnsOrder();
			this.unique = unique;
			this.wrapper = new InMemoryIndexWrapper(this);
			this.name = GetName(columns);
		}
		static string GetName(InMemoryColumn[] columns) {
			StringBuilder sb = new StringBuilder();
			sb.Append("Idx");
			List<string> names = new List<string>();
			for(int i = 0; i < columns.Length; i++) {
				names.Add(columns[i].Name);
			}
			names.Sort();
			for(int i = 0; i < names.Count; i++) {
				sb.Append('_');
				sb.Append(names[i]);
			}
			return sb.ToString();
		}
		public void AddRow(InMemoryRow row) {
			if(InMemoryHelper.IsNullRow(row, columns)) {
				if(nullList.Contains(row)) return;
				if(unique && nullList.Count > 0) throw new InMemoryConstraintException(string.Format(Res.GetString(Res.InMemorySet_UniqueIndex), name));
				nullList.Add(row);
				return;
			}
			int hash = InMemoryHelper.GetRowHashCode(row, columns, caseSensitive);
			object rowsObject;
			if(!dictionary.TryGetValue(hash, out rowsObject) || rowsObject == null) {
				dictionary[hash] = row;
				return;
			}
			List<InMemoryRow> rowsList;
			InMemoryRow rowInRows = rowsObject as InMemoryRow;
			if(rowInRows != null) {
				if(ReferenceEquals(rowInRows, row)) return;
				if(unique && rowInRows.State != InMemoryItemState.Deleted && InMemoryHelper.AreEqualRows(rowInRows, row, columns, caseSensitive)) throw new InMemoryConstraintException(string.Format(Res.GetString(Res.InMemorySet_UniqueIndex), name));
				rowsList = new List<InMemoryRow>(2);
				rowsList.Add(rowInRows);
				rowsList.Add(row);
				dictionary[hash] = rowsList;
				return;
			}
			rowsList = rowsObject as List<InMemoryRow>;
			if(rowsList == null) throw new InvalidOperationException(string.Format(Res.GetString(Res.InMemorySet_WrongDictionaryIndex), name));
			if(rowsList.Contains(row)) return;
			if(unique && InMemoryHelper.FindAnyRow(row, columns, rowsList, caseSensitive)) throw new InMemoryConstraintException(string.Format(Res.GetString(Res.InMemorySet_UniqueIndex), name));
			rowsList.Add(row);
		}
		public void RemoveRow(InMemoryRow row) {
			if(InMemoryHelper.IsNullRow(row, columns)) {
				nullList.Remove(row);
				return;
			}
			int hash = InMemoryHelper.GetRowHashCode(row, columns, caseSensitive);
			object rowsObject;
			if(dictionary.TryGetValue(hash, out rowsObject) && rowsObject != null) {
				InMemoryRow rowInRows = rowsObject as InMemoryRow;
				if(rowInRows != null) {
					if(ReferenceEquals(rowInRows, row)) dictionary[hash] = null;
					return;
				}
				List<InMemoryRow> rowsList = rowsObject as List<InMemoryRow>;
				if(rowsList == null) throw new InvalidOperationException(string.Format(Res.GetString(Res.InMemorySet_WrongDictionaryIndex), name));
				rowsList.Remove(row);
				if(rowsList.Count == 1) {
					dictionary[hash] = rowsList[0];
				}
			}
		}
		public InMemoryRow[] Find(object[] findValues, bool returnDeleted) {
			if(columns.Length != findValues.Length)
				throw new ArgumentException("columns.Length != findValues.Length");
			if(InMemoryHelper.IsNullRow(findValues)) return nullList.ToArray();
			int hash = InMemoryHelper.GetRowHashCode(findValues, caseSensitive);
			List<InMemoryRow> result = new List<InMemoryRow>();
			object rowsObject;
			if(dictionary.TryGetValue(hash, out rowsObject) && rowsObject != null) {
				InMemoryRow rowInRows = rowsObject as InMemoryRow;
				if(rowInRows != null) {
					if(InMemoryHelper.AreEqualRows(rowInRows, columns, findValues, caseSensitive)
						&& (returnDeleted || rowInRows.State != InMemoryItemState.Deleted))
						result.Add(rowInRows);
				}
				else {
					List<InMemoryRow> rowsList = rowsObject as List<InMemoryRow>;
					if(rowsList == null) throw new InvalidOperationException(string.Format(Res.GetString(Res.InMemorySet_WrongDictionaryIndex), name));
					for(int i = 0; i < rowsList.Count; i++) {
						InMemoryRow row = rowsList[i];
						if(!returnDeleted && row.State == InMemoryItemState.Deleted) continue;
						if(InMemoryHelper.AreEqualRows(row, columns, findValues, caseSensitive))
							result.Add(row);
					}
				}
			}
			return result.ToArray();
		}
		public void Clear() {
			dictionary.Clear();
			nullList.Clear();
		}
		public override int GetHashCode() {
			return name.GetHashCode();
		}
		public override bool Equals(object obj) {
			if(obj == null) return false;
			InMemoryDictionaryIndex index = obj as InMemoryDictionaryIndex;
			if(index == null || (!name.Equals(index.name))) return false;
			return true;
		}
		public bool EqualsColumns(InMemoryColumn[] needColumns) {
			if(columns.Length != needColumns.Length) return false;
			for(int i = 0; i < columns.Length; i++) {
				if(columns[i] != needColumns[i]) return false;
			}
			return true;
		}
		public void FixColumnsOrder() {
			Array.Sort<InMemoryColumn>(this.columns, new InMemoryColumnIndexComparer());
		}
		public InMemoryIndexWrapper Wrapper {
			get { return wrapper; }
		}
	}
	public enum InMemoryItemState {
		Default,
		Inserted,
		Deleted,
		Updated
	}
	internal interface IInMemoryIndex {
		string Name { get; }
		bool Unique { get; }
		ReadOnlyCollection<InMemoryColumn> Columns { get; }
		InMemoryIndexWrapper Wrapper { get; }
		void AddRow(InMemoryRow row);
		void RemoveRow(InMemoryRow row);
		void Clear();
		InMemoryRow[] Find(object[] values, bool returnDeleted);
		bool EqualsColumns(InMemoryColumn[] columns);
		void FixColumnsOrder();
	}
	public class InMemoryIndexWrapper {
		IInMemoryIndex index;
		internal InMemoryIndexWrapper(IInMemoryIndex index) {
			this.index = index;
		}
		public bool Unique { get { return index.Unique; } }
		public string Name { get { return index.Name; } }
		public ReadOnlyCollection<InMemoryColumn> Columns { get { return index.Columns; } }
		public InMemoryRow[] Find(object[] values, bool returnDeleted) { return index.Find(values, returnDeleted); }
	}
	public class InMemoryRelationPair {
		InMemoryColumn pKey;
		InMemoryColumn fKey;
		public InMemoryColumn PKey { get { return pKey; } }
		public InMemoryColumn FKey { get { return fKey; } }
		public InMemoryRelationPair(InMemoryColumn pKey, InMemoryColumn fKey) {
			if(!pKey.Type.Equals(fKey.Type)) throw new InMemorySetException(Res.GetString(Res.InMemorySet_DifferentTypes));
			this.pKey = pKey;
			this.fKey = fKey;
		}
	}
	public class InMemoryRelation {
		string name;
		InMemoryRelationPair[] pairs;
		InMemoryRelationPair[] pairsSortedByPIndex;
		InMemoryRelationPair[] pairsSortedByFIndex;
		InMemoryTable pTable;
		InMemoryTable fTable;
		bool fCounterDictReady;
		Dictionary<InMemoryRow, int> fCountersDict = new Dictionary<InMemoryRow, int>();
		public string Name { get { return name; } }
		public InMemoryRelationPair[] Pairs { get { return pairs; } }
		public InMemoryRelationPair[] PairsSortedByPIndex { get { return pairsSortedByPIndex; } }
		public InMemoryRelationPair[] PairsSortedByFIndex { get { return pairsSortedByFIndex; } }
		public InMemoryTable PTable { get { return pTable; } }
		public InMemoryTable FTable { get { return fTable; } }
		public InMemoryRelation(InMemoryRelationPair[] pairs) {
			if(pairs == null || pairs.Length == 0)
				throw new ArgumentException(null, nameof(pairs));
			this.pairs = pairs;
			this.pairsSortedByPIndex = ClonePairs(pairs);
			this.pairsSortedByFIndex = ClonePairs(pairs);
			this.pTable = pairs[0].PKey.Table;
			this.fTable = pairs[0].FKey.Table;
			Array.Sort<InMemoryRelationPair>(pairsSortedByPIndex, new InMemoryRelationPairPIndexComparer());
			Array.Sort<InMemoryRelationPair>(pairsSortedByFIndex, new InMemoryRelationPairFIndexComparer());
			this.name = GetName();
		}
		public override bool Equals(object obj) {
			if(obj == null) return false;
			InMemoryRelation rel = obj as InMemoryRelation;
			if(rel == null) return false;
			return name.Equals(rel.name);
		}
		static InMemoryRelationPair[] ClonePairs(InMemoryRelationPair[] pairs) {
			InMemoryRelationPair[] newPairs = new InMemoryRelationPair[pairs.Length];
			for(int i = 0; i < pairs.Length; i++) {
				newPairs[i] = pairs[i];
			}
			return newPairs;
		}
		string GetName() {
			StringBuilder sb = new StringBuilder();
			sb.Append("FK_");
			List<string> list = new List<string>();
			foreach(InMemoryRelationPair pair in pairs) {
				list.Add(string.Format(CultureInfo.InvariantCulture, "{0}#{1}#{2}#{3}", pair.PKey.Table.Name, pair.PKey.Name, pair.FKey.Table.Name, pair.FKey.Name));
			}
			list.Sort();
			foreach(string item in list) {
				sb.Append('_');
				sb.Append(item);
			}
			return sb.ToString();
		}
		public void CheckPAssociation(InMemoryColumn col, InMemoryRow row, object oldValue) {
			InMemoryRelationPair[] currentPairs = pairsSortedByFIndex;
			InMemoryColumn[] columns = new InMemoryColumn[currentPairs.Length];
			object[] values = new object[currentPairs.Length];
			for(int i = 0; i < currentPairs.Length; i++) {
				columns[i] = currentPairs[i].FKey;
				if(currentPairs[i].PKey == col)
					values[i] = oldValue;
				else
					values[i] = row[currentPairs[i].PKey.ColumnIndex];
			}
			if(InMemoryHelper.IsNullRow(values)) return;
			if(fCounterDictReady) {
				if(FastFCheck(row)) throw new InMemoryConstraintException();
			}
			else
				if(FTable.Rows.FindFirst(columns, values) != null) throw new InMemoryConstraintException();
		}
		void RaiseConstraintException(InMemoryColumn[] columns) {
			StringBuilder sb = new StringBuilder();
			foreach(InMemoryColumn column in columns) {
				if(sb.Length > 0) {
					sb.Append(", ");
				}
				sb.Append(column.Name);
			}
			throw new InMemoryConstraintException(string.Format(Res.GetString(Res.InMemorySet_ConstraintConflict), FTable.Name, sb.ToString()));
		}
		public InMemoryRow[] CheckFAssociation(InMemoryColumn col, InMemoryRow row, object newValue) {
			InMemoryRelationPair[] currentPairs = pairsSortedByPIndex;
			InMemoryColumn[] columns = new InMemoryColumn[currentPairs.Length];
			object[] values = new object[currentPairs.Length];
			for(int i = 0; i < currentPairs.Length; i++) {
				columns[i] = currentPairs[i].PKey;
				if(currentPairs[i].FKey == col)
					values[i] = newValue;
				else
					values[i] = row[currentPairs[i].FKey.ColumnIndex];
			}
			if(InMemoryHelper.IsNullRow(values)) return Array.Empty<InMemoryRow>();
			if(fCounterDictReady) {
				InMemoryRow[] foundRows = PTable.Rows.Find(columns, values);
				if(foundRows.Length == 0) RaiseConstraintException(columns);
				return foundRows;
			}
			else
				if(PTable.Rows.FindFirst(columns, values) == null) throw new InMemoryConstraintException();
			return Array.Empty<InMemoryRow>();
		}
		public void CheckPAssociation(InMemoryRow row, object[] rowOldData, bool[] modified) {
			InMemoryRelationPair[] currentPairs = pairsSortedByFIndex;
			InMemoryColumn[] columns = new InMemoryColumn[currentPairs.Length];
			object[] values = new object[currentPairs.Length];
			int modifiedCount = 0;
			for(int i = 0; i < currentPairs.Length; i++) {
				columns[i] = currentPairs[i].FKey;
				int pColumnIndex = currentPairs[i].PKey.ColumnIndex;
				if(modified != null && modified[pColumnIndex]) modifiedCount++;
				values[i] = rowOldData[pColumnIndex];
			}
			if(InMemoryHelper.IsNullRow(values)) return;
			if(modified != null && modifiedCount == 0) return;
			if(fCounterDictReady) {
				if(FastFCheck(row)) throw new InMemoryConstraintException();
			}
			else
				if(FTable.Rows.FindFirst(columns, values) != null) throw new InMemoryConstraintException();
		}
		public InMemoryRow[] CheckFAssociation(object[] rowNewData) {
			InMemoryRelationPair[] currentPairs = pairsSortedByPIndex;
			InMemoryColumn[] columns = new InMemoryColumn[currentPairs.Length];
			object[] values = new object[currentPairs.Length];
			for(int i = 0; i < currentPairs.Length; i++) {
				columns[i] = currentPairs[i].PKey;
				values[i] = rowNewData[currentPairs[i].FKey.ColumnIndex];
			}
			if(InMemoryHelper.IsNullRow(values)) return Array.Empty<InMemoryRow>();
			if(fCounterDictReady) {
				InMemoryRow[] foundRows = PTable.Rows.Find(columns, values);
				if(foundRows.Length == 0) RaiseConstraintException(columns);
				return foundRows;
			}
			else
				if(PTable.Rows.FindFirst(columns, values) == null) throw new InMemoryConstraintException();
			return Array.Empty<InMemoryRow>();
		}
		public void CheckRelation() {
			InMemoryRowList pRows = pTable.Rows;
			InMemoryRowList fRows = fTable.Rows;
			InMemoryColumn[] pColumns = new InMemoryColumn[pairs.Length];
			object[] values = new object[pairsSortedByPIndex.Length];
			for(int i = 0; i < pairsSortedByPIndex.Length; i++) {
				pColumns[i] = pairs[i].PKey;
			}
			for(int i = 0; i < fRows.Count; i++) {
				InMemoryRow fRow = fRows[i];
				for(int j = 0; j < pColumns.Length; j++) {
					values[j] = fRow[pairs[j].FKey.ColumnIndex];
				}
				if(InMemoryHelper.IsNullRow(values)) continue;
				InMemoryRow[] foundRows = pRows.Find(pColumns, values);
				if(foundRows.Length == 0) RaiseConstraintException(pColumns);
				for(int f = 0; f < foundRows.Length; f++) {
					IncrementRowFCounter(foundRows[f]);
				}
			}
			fCounterDictReady = true;
		}
		public void EnterFAssociation(InMemoryRow[] fRows) {
			if(fCounterDictReady) {
				for(int f = 0; f < fRows.Length; f++) {
					IncrementRowFCounter(fRows[f]);
				}
			}
		}
		public void LeaveFAssociation(InMemoryRow row, object[] rowOldData) {
			InMemoryRelationPair[] currentPairs = pairsSortedByPIndex;
			InMemoryColumn[] columns = new InMemoryColumn[currentPairs.Length];
			object[] values = new object[currentPairs.Length];
			for(int i = 0; i < currentPairs.Length; i++) {
				columns[i] = currentPairs[i].PKey;
				values[i] = rowOldData[currentPairs[i].FKey.ColumnIndex];
			}
			if(InMemoryHelper.IsNullRow(values)) return;
			if(fCounterDictReady) {
				InMemoryRow[] foundRows = PTable.Rows.FindWithDeleted(columns, values);
				for(int f = 0; f < foundRows.Length; f++) {
					DecrementRowFCounter(foundRows[f]);
				}
			}
		}
		bool FastFCheck(InMemoryRow foundRow) {
			int fCounter;
			if(!fCountersDict.TryGetValue(foundRow, out fCounter)) return false;
			return (fCounter > 0);
		}
		void IncrementRowFCounter(InMemoryRow foundRow) {
			int fCounter;
			if(!fCountersDict.TryGetValue(foundRow, out fCounter)) {
				fCountersDict.Add(foundRow, 1);
				return;
			}
			fCountersDict[foundRow] = ++fCounter;
		}
		void DecrementRowFCounter(InMemoryRow foundRow) {
			int fCounter;
			if(!fCountersDict.TryGetValue(foundRow, out fCounter)) return;
			if(fCounter == 0) return;
			if(fCounter == 1) fCountersDict.Remove(foundRow);
			else fCountersDict[foundRow] = --fCounter;
		}
		public InMemoryRow[] GetRows(InMemoryRow row) {
			if(row.Table == PTable) return GetFRows(row);
			return GetPRows(row);
		}
		public InMemoryColumn[] GetPColumns() {
			InMemoryRelationPair[] currentPairs = pairsSortedByPIndex;
			InMemoryColumn[] columns = new InMemoryColumn[currentPairs.Length];
			for(int i = 0; i < currentPairs.Length; i++) {
				columns[i] = currentPairs[i].PKey;
			}
			return columns;
		}
		public InMemoryRow[] GetPRows(InMemoryRow row) {
			InMemoryRelationPair[] currentPairs = pairsSortedByPIndex;
			InMemoryColumn[] columns = new InMemoryColumn[currentPairs.Length];
			object[] values = new object[currentPairs.Length];
			for(int i = 0; i < currentPairs.Length; i++) {
				columns[i] = currentPairs[i].PKey;
				values[i] = row[currentPairs[i].FKey.ColumnIndex];
			}
			return PTable.Rows.Find(columns, values);
		}
		public InMemoryRow[] GetFRows(InMemoryRow row) {
			InMemoryRelationPair[] currentPairs = pairsSortedByFIndex;
			InMemoryColumn[] columns = new InMemoryColumn[currentPairs.Length];
			object[] values = new object[currentPairs.Length];
			for(int i = 0; i < currentPairs.Length; i++) {
				columns[i] = currentPairs[i].FKey;
				values[i] = row[currentPairs[i].PKey.ColumnIndex];
			}
			return FTable.Rows.Find(columns, values);
		}
		public override int GetHashCode() {
			return name.GetHashCode();
		}
		public override string ToString() {
			return name;
		}
	}
	internal class InMemoryIndexesHolder {
		List<IInMemoryIndex> indexes;
		public bool HasIndexes { get { return indexes == null || indexes.Count == 0 ? false : true; } }
		public int Count { get { return indexes == null ? 0 : indexes.Count; } }
		public IInMemoryIndex this[int i] {
			get {
				if(indexes == null) throw new IndexOutOfRangeException();
				return indexes[i];
			}
		}
		public InMemoryIndexesHolder(List<IInMemoryIndex> indexes) {
			this.indexes = indexes;
		}
		public void AddRow(InMemoryRow row) {
			for(int i = 0; i < indexes.Count; i++) {
				indexes[i].AddRow(row);
			}
		}
		public void RemoveRow(InMemoryRow row) {
			for(int i = 0; i < indexes.Count; i++) {
				indexes[i].RemoveRow(row);
			}
		}
		public void Clear() {
			for(int i = 0; i < indexes.Count; i++) {
				indexes[i].Clear();
			}
		}
		public IInMemoryIndex GetIndex(InMemoryColumn[] columns) {
			for(int i = 0; i < indexes.Count; i++) {
				if(indexes[i].EqualsColumns(columns)) return indexes[i];
			}
			return null;
		}
	}
	public class InMemoryIndexWrapperCollection : IEnumerable<InMemoryIndexWrapper> {
		Dictionary<string, IInMemoryIndex> indexes;
		internal InMemoryIndexWrapperCollection(Dictionary<string, IInMemoryIndex> indexes) {
			this.indexes = indexes;
		}
		public InMemoryIndexWrapper this[string name] { get { return indexes[name].Wrapper; } }
		public IEnumerable<string> Names { get { return indexes.Keys; } }
		public int Count { get { return indexes.Count; } }
		public bool Contains(string name) {
			return indexes.ContainsKey(name);
		}
		public IEnumerator<InMemoryIndexWrapper> GetEnumerator() {
			foreach(IInMemoryIndex index in indexes.Values) {
				yield return index.Wrapper;
			}
		}
		IEnumerator IEnumerable.GetEnumerator() {
			foreach(IInMemoryIndex index in indexes.Values) {
				yield return index.Wrapper;
			}
		}
	}
	public interface IInMemoryDataElector {
		InMemoryComplexSet Process(InMemoryDataElectorContextDescriptor descriptor);
	}
	public class InMemoryComplexSet : IInMemoryDataElector, IEnumerable<InMemoryComplexRow>, IEnumerable {
		HashSet<string> projectionSet;
		Dictionary<ProjectionColumnItem, int> projectionColumnDict;
		Dictionary<string, int> complexDict = new Dictionary<string, int>(StringComparer.Default);
		List<string> aliasList = new List<string>();
		List<IInMemoryTable> tableList = new List<IInMemoryTable>();
		List<InMemoryComplexRow> rows;
		public InMemoryComplexRow this[int rowIndex] { get { return rows[rowIndex]; } }
		public int TableCount { get { return complexDict.Count; } }
		public int Count { get { return rows.Count; } }
		public InMemoryComplexSet() {
			rows = new List<InMemoryComplexRow>();
		}
		public InMemoryComplexSet(int capacity) {
			rows = new List<InMemoryComplexRow>(capacity);
		}
		public void MakeProjection(string projectionAlias) {
			CreateProjectionDicts();
			projectionSet.Add(projectionAlias);
			for(int i = 0; i < tableList.Count; i++) {
				var table = tableList[i];
				foreach(var columnName in table.GetColumnNames()) {
					var item = new ProjectionColumnItem(projectionAlias, columnName);
					if(!projectionColumnDict.ContainsKey(item))
						projectionColumnDict.Add(item, i);
				}
			}
		}
		void CreateProjectionDicts() {
			if(projectionSet == null) {
				projectionSet = new HashSet<string>(StringComparer.Default);
				projectionColumnDict = new Dictionary<ProjectionColumnItem, int>(ProjectionColumnItemComparer.Instance);
			}
		}
		public void AddProjectionFromSet(InMemoryComplexSet set) {
			if(set == null || set.projectionSet == null)
				return;
			CreateProjectionDicts();
			foreach(var projection in set.projectionSet) {
				projectionSet.Add(projection);
			}
			foreach(var projectionColumn in set.projectionColumnDict) {
				var tableAlias = set.aliasList[projectionColumn.Value];
				int columnTableIndex = aliasList.IndexOf(tableAlias);
				if(columnTableIndex < 0)
					throw new InvalidOperationException(Res.GetString(Res.InMemoryFull_TableNotFound, set.tableList[projectionColumn.Value].Name));
				projectionColumnDict.Add(projectionColumn.Key, columnTableIndex);
			}
		}
		public ReadOnlyCollection<string> GetProjectionList() {
			return new ReadOnlyCollection<string>(new List<string>(projectionSet));
		}
		public ReadOnlyCollection<IInMemoryTable> GetTableList() {
			return new ReadOnlyCollection<IInMemoryTable>(tableList);
		}
		public ReadOnlyCollection<string> GetAliasList() {
			return new ReadOnlyCollection<string>(aliasList);
		}
		public int AddTableIfNotExists(string alias, IInMemoryTable table) {
			int foundIndex;
			if(!complexDict.TryGetValue(alias, out foundIndex)) {
				return AddTable(alias, table);
			}
			if(tableList[foundIndex] == table) return foundIndex;
			throw new InMemoryDuplicateNameException(alias);
		}
		public int AddTable(string alias, IInMemoryTable table) {
			try {
				int newIndex = complexDict.Count;
				complexDict.Add(alias, newIndex);
				aliasList.Add(alias);
				tableList.Add(table);
				return newIndex;
			}
			catch(Exception ex) {
				throw new InMemoryDuplicateNameException(alias, ex);
			}
		}
		public int GetTableIndex(string alias) {
			int result;
			if(complexDict.TryGetValue(alias, out result))
				return result;
			return -1;
		}
		public int GetTableIndex(string projectionAlias, string columnName) {
			int result;
			if(projectionColumnDict != null && projectionColumnDict.TryGetValue(new ProjectionColumnItem(projectionAlias, columnName), out result))
				return result;
			return -1;
		}
		public IInMemoryTable GetTable(int index) {
			return tableList[index];
		}
		public IInMemoryTable GetTable(string alias) {
			int index;
			if(complexDict.TryGetValue(alias, out index)) {
				return tableList[index];
			}
			return null;
		}
		public IInMemoryTable GetTable(string projectionAlias, string columnName) {
			int index;
			if(projectionColumnDict != null && projectionColumnDict.TryGetValue(new ProjectionColumnItem(projectionAlias, columnName), out index))
				return tableList[index];
			return null;
		}
		public void AddRows(IEnumerable<InMemoryComplexRow> collection) {
			rows.AddRange(collection);
		}
		public void AddRow(InMemoryComplexRow row) {
			if(row.ComplexSet != this) throw new InMemorySetException(Res.GetString(Res.InMemorySet_DifferentComplexSet));
			rows.Add(row);
		}
		public InMemoryComplexRow AddNewRow() {
			InMemoryComplexRow row = new InMemoryComplexRow(this);
			rows.Add(row);
			return row;
		}
		public InMemoryComplexRow AddNewRow(int tableIndex, InMemoryRow row) {
			InMemoryComplexRow cRow = AddNewRow();
			cRow[tableIndex] = row;
			return cRow;
		}
		public InMemoryComplexRow AddNewRow(string tableAlias, InMemoryRow row) {
			return AddNewRow(GetTableIndex(tableAlias), row);
		}
		public void RemoveAt(int rowIndex) {
			rows.RemoveAt(rowIndex);
		}
		public void RemoveRange(int rowIndex, int rowCount) {
			rows.RemoveRange(rowIndex, rowCount);
		}
		public void Sort() {
			rows.Sort();
		}
		public void Sort(IComparer<InMemoryComplexRow> comparer) {
			rows.Sort(comparer);
		}
		public void Clear() {
			rows.Clear();
		}
		public void Randomize() {
			var r = Data.Utils.NonCryptographicRandom.Default;
			for(int i = 0; i < rows.Count - 1; i++) {
				int pos = r.Next(i, rows.Count);
				InMemoryComplexRow temp = rows[pos];
				rows[pos] = rows[i];
				rows[i] = temp;
			}
		}
		public InMemoryComplexSet Process(InMemoryDataElectorContextDescriptor descriptor) {
			return this;
		}
		IEnumerator IEnumerable.GetEnumerator() { return ((IEnumerable)rows).GetEnumerator(); }
		public IEnumerator<InMemoryComplexRow> GetEnumerator() { return rows.GetEnumerator(); }
		struct ProjectionColumnItem : IEquatable<ProjectionColumnItem> {
			public readonly string ProjectionAlias;
			public readonly string ColumnName;
			public ProjectionColumnItem(string projectionAlias, string columnName) {
				if(string.IsNullOrEmpty(projectionAlias)) throw new ArgumentNullException(nameof(projectionAlias));
				if(string.IsNullOrEmpty(columnName)) throw new ArgumentNullException(nameof(columnName));
				ProjectionAlias = projectionAlias;
				ColumnName = columnName;
			}
			public override bool Equals(object obj) {
				if(!(obj is ProjectionColumnItem))
					return false;
				return Equals((ProjectionColumnItem)obj);
			}
			public bool Equals(ProjectionColumnItem other) {
				return ProjectionAlias == other.ProjectionAlias && ColumnName == other.ColumnName;
			}
			public override int GetHashCode() {
				return HashCodeHelper.CalculateGeneric(ProjectionAlias, ColumnName);
			}
		}
		class ProjectionColumnItemComparer : IEqualityComparer<ProjectionColumnItem> {
			static ProjectionColumnItemComparer instance = new ProjectionColumnItemComparer();
			public static ProjectionColumnItemComparer Instance {
				get { return instance; }
			}
			public bool Equals(ProjectionColumnItem x, ProjectionColumnItem y) {
				return x.ProjectionAlias == y.ProjectionAlias && x.ColumnName == y.ColumnName;
			}
			public int GetHashCode(ProjectionColumnItem obj) {
				return HashCodeHelper.CalculateGeneric(obj.ProjectionAlias, obj.ColumnName);
			}
		}
	}
	internal class InMemoryComplexRowAccessList : IList<IInMemoryRow> {
		readonly string alias;
		readonly int tableIndex;
		readonly InMemoryComplexSet complexSet;
		public IInMemoryRow this[int index] {
			get { return complexSet[index][tableIndex]; }
			set { throw new InMemorySetException(Res.GetString(Res.InMemorySet_OperationNotAllowed)); }
		}
		public int Count { get { return complexSet.Count; } }
		public bool IsReadOnly { get { return true; } }
		public InMemoryComplexRowAccessList(InMemoryComplexSet complexSet, string alias) {
			this.alias = alias;
			this.complexSet = complexSet;
			this.tableIndex = complexSet.GetTableIndex(alias);
			if(this.tableIndex < 0) throw new ArgumentException(Res.GetString(Res.InMemorySet_AliasNotFound, alias));
		}
		public int IndexOf(IInMemoryRow item) {
			throw new InMemorySetException(Res.GetString(Res.InMemorySet_OperationNotAllowed));
		}
		public void Insert(int index, IInMemoryRow item) {
			throw new InMemorySetException(Res.GetString(Res.InMemorySet_OperationNotAllowed));
		}
		public void RemoveAt(int index) {
			throw new InMemorySetException(Res.GetString(Res.InMemorySet_OperationNotAllowed));
		}
		public void Add(IInMemoryRow item) {
			throw new InMemorySetException(Res.GetString(Res.InMemorySet_OperationNotAllowed));
		}
		public void Clear() {
			throw new InMemorySetException(Res.GetString(Res.InMemorySet_OperationNotAllowed));
		}
		public bool Contains(IInMemoryRow item) {
			throw new InMemorySetException(Res.GetString(Res.InMemorySet_OperationNotAllowed));
		}
		public void CopyTo(IInMemoryRow[] array, int arrayIndex) {
			throw new InMemorySetException(Res.GetString(Res.InMemorySet_OperationNotAllowed));
		}
		public bool Remove(IInMemoryRow item) {
			throw new InMemorySetException(Res.GetString(Res.InMemorySet_OperationNotAllowed));
		}
		public IEnumerator<IInMemoryRow> GetEnumerator() {
			throw new InMemorySetException(Res.GetString(Res.InMemorySet_OperationNotAllowed));
		}
		IEnumerator IEnumerable.GetEnumerator() {
			throw new InMemorySetException(Res.GetString(Res.InMemorySet_OperationNotAllowed));
		}
	}
	public class InMemoryComplexRow {
		InMemoryComplexSet complexSet;
		List<IInMemoryRow> tablesRows;
		public InMemoryComplexSet ComplexSet { get { return complexSet; } }
		public InMemoryComplexRow(InMemoryComplexSet resultSet) {
			this.complexSet = resultSet;
			this.tablesRows = new List<IInMemoryRow>(resultSet.TableCount);
		}
		public InMemoryComplexRow(InMemoryComplexSet resultSet, InMemoryComplexRow otherRow)
			: this(resultSet) {
			for(int i = 0; i < otherRow.ComplexSet.TableCount; i++) {
				tablesRows.Add(otherRow[i]);
			}
		}
		public InMemoryComplexRow(InMemoryComplexRow complexRow)
			: this(complexRow.ComplexSet, complexRow) {
		}
		public IInMemoryRow this[int tableIndex] {
			get { return (tableIndex < 0 || tableIndex >= tablesRows.Count) ? null : tablesRows[tableIndex]; }
			set {
				if(tableIndex < 0) throw new IndexOutOfRangeException();
				if(tableIndex >= complexSet.TableCount) throw new IndexOutOfRangeException();
				if(tableIndex >= tablesRows.Count) {
					for(int i = tablesRows.Count - 1; i < tableIndex; i++)
						tablesRows.Add(null);
				}
				tablesRows[tableIndex] = value;
			}
		}
		public IInMemoryRow this[string tableAlias] {
			get { return this[complexSet.GetTableIndex(tableAlias)]; }
		}
		public override string ToString() {
			StringBuilder sb = new StringBuilder();
			ReadOnlyCollection<string> aliasList = complexSet.GetAliasList();
			sb.Append("{ ");
			for(int i = 0; i < aliasList.Count; i++) {
				if(i > 0) sb.Append(", ");
				sb.Append('"');
				sb.Append(aliasList[i]);
				sb.Append("\" ");
				if(i >= tablesRows.Count || tablesRows[i] == null) {
					sb.Append("null");
					continue;
				}
				sb.Append(tablesRows[i].ToString());
			}
			return sb.ToString();
		}
	}
	public static class InMemoryHelper {
		public const string NullsAreNotAllowed = "Nulls are not allowed.";
		public static object[] GetValues(InMemoryRow row, InMemoryColumn[] columns) {
			object[] res = new object[columns.Length];
			for(int i = 0; i < columns.Length; i++) {
				res[i] = row[columns[i].ColumnIndex];
			}
			return res;
		}
		public static bool IsNullRow(object[] values) {
			for(int i = 0; i < values.Length; i++) {
				if(values[i] != null) return false;
			}
			return true;
		}
		public static bool IsNullRow(InMemoryRow row, InMemoryColumn[] columns) {
			for(int i = 0; i < columns.Length; i++) {
				if(row[columns[i].ColumnIndex] != null) return false;
			}
			return true;
		}
		public static int GetValueHash(object value, bool caseSensitive) {
			if(caseSensitive) {
				return value.GetHashCode();
			}
			string stringValue = value as string;
			if(stringValue == null) {
				return value.GetHashCode();
			}
			return stringValue.ToLower().GetHashCode();
		}
		private static bool AreEqualValues(bool caseSensitive, object valueLeft, object valueRight) {
			string stringValueLeft;
			if(caseSensitive || (stringValueLeft = valueLeft as string) == null)
				return valueLeft == valueRight || (valueLeft != null && valueRight != null && valueLeft.Equals(valueRight));
			return stringValueLeft.Equals(valueRight as string, StringComparison.CurrentCultureIgnoreCase);
		}
		public static int GetRowHashCode(object[] values, bool caseSensitive) {
			int res = 0;
			for(int i = 0; i < values.Length; i++) {
				object value = values[i];
				if(value != null) {
					res = ((res << 1) | (res >> 31)) ^ GetValueHash(value, caseSensitive);
				}
			}
			return res;
		}
		public static int GetRowHashCode(InMemoryRow row, InMemoryColumn[] columns, bool caseSensitive) {
			int res = 0;
			for(int i = 0; i < columns.Length; i++) {
				object value = row[columns[i].ColumnIndex];
				if(value != null) {
					res = ((res << 1) | (res >> 31)) ^ GetValueHash(value, caseSensitive);
				}
			}
			return res;
		}
		public static bool AreEqualRows(InMemoryRow rowLeft, InMemoryRow rowRight, InMemoryColumn[] columns, bool caseSensitive) {
			for(int i = 0; i < columns.Length; i++) {
				int columnIndex = columns[i].ColumnIndex;
				object valueLeft = rowLeft[columnIndex];
				object valueRight = rowRight[columnIndex];
				if(valueRight == null) {
					if(valueLeft != null) return false;
				}
				else if(!AreEqualValues(caseSensitive, valueLeft, valueRight)) return false;
			}
			return true;
		}
		public static bool AreEqualRows(InMemoryRow row, InMemoryColumn[] columns, object[] valuesLeft, bool caseSensitive) {
			if(!columns.Length.Equals(valuesLeft.Length)) return false;
			for(int i = 0; i < valuesLeft.Length; i++) {
				object value = row[columns[i].ColumnIndex];
				if(valuesLeft[i] == null) {
					if(value != null) return false;
				}
				else if(!AreEqualValues(caseSensitive, valuesLeft[i], value)) return false;
			}
			return true;
		}
		public static bool FindAnyRow(InMemoryRow row, InMemoryColumn[] columns, List<InMemoryRow> rows, bool caseSensitive) {
			foreach(InMemoryRow sRow in rows) {
				if(sRow.State == InMemoryItemState.Deleted) continue;
				bool found = true;
				foreach(InMemoryColumn column in columns) {
					int columnIndex = column.ColumnIndex;
					if(row[columnIndex] == null) {
						if(sRow[columnIndex] != null) {
							found = false;
						}
					}
					else if(!AreEqualValues(caseSensitive, row[columnIndex], sRow[columnIndex])) {
						found = false;
						break;
					}
				}
				if(found) return true;
			}
			return false;
		}
		public static List<InMemoryRow> FindInRows(List<InMemoryRow> rows, InMemoryColumn[] columns, object[] values, bool findFirst, bool returnDeleted, bool caseSensitive) {
			List<InMemoryRow> result = new List<InMemoryRow>();
			for(int i = 0; i < rows.Count; i++) {
				InMemoryRow row = rows[i];
				if(!returnDeleted && row.State == InMemoryItemState.Deleted) continue;
				bool equals = true;
				for(int j = 0; j < columns.Length; j++) {
					if(values[j] == null) {
						if(row[columns[j].ColumnIndex] == null) {
							equals = false;
							break;
						}
					}
					else {
						if(!AreEqualValues(caseSensitive, values[j], row[columns[j].ColumnIndex])) {
							equals = false;
							break;
						}
					}
				}
				if(equals) {
					result.Add(row);
					if(findFirst) break;
				}
			}
			return result;
		}
		public static object Convert(object value, InMemoryColumn column) {
			return Convert(value, column.Type, column.ColumnForUpdate.Type, column.ColumnForUpdate.AllowNull);
		}
		public static object Convert(object value, Type oldType, Type newType, bool allowNull) {
			if(value != null) {
				if(!oldType.Equals(newType)) {
					try {
						return ((IConvertible)value).ToType(newType, CultureInfo.InvariantCulture);
					}
					catch(Exception ex) {
						throw new InvalidCastException(string.Empty, ex);
					}
				}
			}
			if(!allowNull) throw new InMemoryNoNullAllowedException();
			return value;
		}
		public static object[] PrepareKey(object key) {
			if(DXTypeExtensions.GetTypeCode(key.GetType()) != TypeCode.Object) return new object[] { key };
			IEnumerable enumerable = key as IEnumerable;
			if(enumerable == null) {
				return new object[] { key };
			}
			List<object> result = new List<object>();
			foreach(object obj in enumerable) {
				result.Add(obj);
			}
			return result.ToArray();
		}
	}
#if !NET
	[Serializable]
#endif
	public class InMemorySetException : Exception {
		public InMemorySetException()
			: base() {
		}
		public InMemorySetException(string message)
			: base(message) {
		}
		public InMemorySetException(string message, Exception innerException)
			: base(message, innerException) {
		}
#if !NET
		public InMemorySetException(SerializationInfo info, StreamingContext context)
			: base(info, context) {
		}
#endif
	}
#if !NET
	[Serializable]
#endif
	public class InMemoryConstraintException : InMemorySetException {
		public InMemoryConstraintException()
			: base() {
		}
		public InMemoryConstraintException(string message)
			: base(message) {
		}
		public InMemoryConstraintException(string message, Exception innerException)
			: base(message, innerException) {
		}
#if !NET
		public InMemoryConstraintException(SerializationInfo info, StreamingContext context)
			: base(info, context) {
		}
#endif
	}
#if !NET
	[Serializable]
#endif
	public class InMemoryDuplicateNameException : InMemorySetException {
		public InMemoryDuplicateNameException()
			: base() {
		}
		public InMemoryDuplicateNameException(string message)
			: base(message) {
		}
		public InMemoryDuplicateNameException(string message, Exception innerException)
			: base(message, innerException) {
		}
#if !NET
		public InMemoryDuplicateNameException(SerializationInfo info, StreamingContext context)
			: base(info, context) {
		}
#endif
	}
#if !NET
	[Serializable]
#endif
	public class InMemoryNoNullAllowedException : InMemorySetException {
		public InMemoryNoNullAllowedException()
			: base() {
		}
		public InMemoryNoNullAllowedException(string message)
			: base(message) {
		}
		public InMemoryNoNullAllowedException(string message, Exception innerException)
			: base(message, innerException) {
		}
#if !NET
		public InMemoryNoNullAllowedException(SerializationInfo info, StreamingContext context)
			: base(info, context) {
		}
#endif
	}
#if !NET
	[Serializable]
#endif
	public class InMemoryNoAutoIncrementAllowedException : InMemorySetException {
		public InMemoryNoAutoIncrementAllowedException()
			: base() {
		}
		public InMemoryNoAutoIncrementAllowedException(string message)
			: base(message) {
		}
		public InMemoryNoAutoIncrementAllowedException(string message, Exception innerException)
			: base(message, innerException) {
		}
#if !NET
		public InMemoryNoAutoIncrementAllowedException(SerializationInfo info, StreamingContext context)
			: base(info, context) {
		}
#endif
	}
}
