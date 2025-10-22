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
using System.Resources;
namespace DevExpress.Xpo.DB {
	using System.Collections;
	using System.Collections.Specialized;
	using System.Data;
	using System.Globalization;
	using System.Reflection;
	using DevExpress.Xpo;
	using DevExpress.Xpo.Metadata.Helpers;
	using DevExpress.Xpo.Exceptions;
	using DevExpress.Xpo.Metadata;
	using DevExpress.Xpo.Helpers;
	using DevExpress.Xpo.DB;
	using System.Collections.Generic;
	public abstract class DBTableHelper {
		DBTableHelper() { }
		static DBColumn AddColumn(DBTable table, XPMemberInfo mi, string name, bool isKey, bool isReferenceKeyMember) {
			DBColumn column = table.GetColumn(name);
			if(column == null) {
				if(mi.ReferenceType != null) {
					mi = mi.ReferenceType.KeyProperty;
					isReferenceKeyMember = true;
				}
				column = CreateColumn(table, mi, name, isReferenceKeyMember);
				column.IsKey = isKey;
				table.AddColumn(column);
			} else
				if(isKey)
					column.IsKey = isKey;
			return column;
		}
		public static void ProcessClassInfo(DBTable table, XPClassInfo classInfo) {
			ValidateMembers(classInfo);
			Dictionary<XPMemberInfo, List<DBColumn>> memberColumns = new Dictionary<XPMemberInfo,List<DBColumn>>();
			ProcessMembers(table, classInfo, memberColumns);
			ProcessIndexes(table, classInfo, memberColumns);
		}
		static void ValidateMembers(XPClassInfo classInfo) {
			foreach(XPMemberInfo mi in classInfo.Members) {
				if(mi.IsNullable != null) {
					if(!mi.IsPersistent || mi.IsAssociationList || mi.IsAliased || mi.IsManyToManyAlias || mi.ReferenceType != null) {
						string msg = Res.GetString(Res.Metadata_NullableAttributeNotApplicable, mi.Name, mi.Owner.FullName);
						throw new InvalidOperationException(msg);
					}
				}
			}
		}
		static void ProcessMembers(DBTable table, XPClassInfo classInfo, Dictionary<XPMemberInfo, List<DBColumn>> memberColumns) {
			foreach(XPMemberInfo memberInfo in classInfo.PersistentProperties) {
				if(memberInfo.IsMappingClass(classInfo)) {
					List<DBColumn> columns = ProcessMemberColumns(table, classInfo, memberInfo);
					memberColumns.Add(memberInfo, columns);
					if(memberInfo.IsKey)
						ProcessPrimaryKey(table, columns, classInfo);
				}
			}
		}
		static void ProcessIndexes(DBTable table, XPClassInfo classInfo, Dictionary<XPMemberInfo, List<DBColumn>> memberColumns) {
			Dictionary<XPMemberInfo, bool> membersDict = new Dictionary<XPMemberInfo, bool>();
			foreach(XPMemberInfo memberInfo in classInfo.PersistentProperties) {
				if(memberInfo.IsMappingClass(classInfo)) {
					if(memberInfo.HasAttribute(typeof(IndexedAttribute))) {
						IndexedAttribute indexed = (IndexedAttribute)memberInfo.GetAttributeInfo(typeof(IndexedAttribute));
						membersDict.Clear();
						membersDict.Add(memberInfo, true);
						List<DBColumn> columns = new List<DBColumn>(memberColumns[memberInfo]);
						foreach(string memberName in indexed.AdditionalFields) {
							XPMemberInfo member = classInfo.GetMember(memberName);
							if(!memberColumns.ContainsKey(member))
								throw new PropertyMissingException(memberInfo.Owner.FullName, memberName);
							if(membersDict.ContainsKey(member))
								throw new InvalidOperationException(Res.GetString(Res.MetaData_PropertyIsDuplicatedInIndexDeclaration, memberName, classInfo.FullName, ".", memberInfo.Name, "property"));
							membersDict.Add(member, true);
							List<DBColumn> addColumns = memberColumns[member];
							columns.AddRange(addColumns);
						}
						DBIndex index = new DBIndex(indexed.Name, columns, indexed.Unique);
						if(!table.IsIndexIncluded(index))
							table.AddIndex(index);
					}
				}
			}
			if(classInfo.HasAttribute(typeof(IndicesAttribute))) {
				IndicesAttribute indices = (IndicesAttribute)classInfo.GetAttributeInfo(typeof(IndicesAttribute));
				foreach(var memberNames in indices.Indices) {
					if(memberNames.Count == 0) continue;
					XPMemberInfo memberInfo = classInfo.GetMember(memberNames[0]);
					membersDict.Clear();
					membersDict.Add(memberInfo, true);
					if(memberInfo.IsMappingClass(classInfo)) {
						if(!memberColumns.ContainsKey(memberInfo)) {
							throw new PropertyMissingException(classInfo.FullName, memberInfo.Name);
						}
						List<DBColumn> columns = new List<DBColumn>(memberColumns[memberInfo]);
						for(int i = 1; i < memberNames.Count; i++) {
							string memberName = memberNames[i];
							XPMemberInfo member = classInfo.GetMember(memberName);
							if(!memberColumns.ContainsKey(member))
								throw new PropertyMissingException(memberInfo.Owner.FullName, memberName);
							if(membersDict.ContainsKey(member))
								throw new InvalidOperationException(Res.GetString(Res.MetaData_PropertyIsDuplicatedInIndexDeclaration, memberName, classInfo.FullName, string.Empty, string.Empty, "class"));
							membersDict.Add(member, true);
							List<DBColumn> addColumns = memberColumns[member];
							columns.AddRange(addColumns);
						}
						DBIndex index = new DBIndex(null, columns, false);
						if(!table.IsIndexIncluded(index))
							table.AddIndex(index);
					}
				}
			}
		}
		static List<DBColumn> ProcessMemberColumns(DBTable table, XPClassInfo classInfo, XPMemberInfo memberInfo) {
			return ProcessMemberColumns(table, memberInfo, 
				memberInfo.SubMembers.Count == 0 ? memberInfo.MappingField : string.Empty, memberInfo.IsKey, 
				!(memberInfo.IsKey && classInfo.PersistentBaseClass != null && classInfo.PersistentBaseClass.TableName != table.Name), false);
		}
		static List<DBColumn> ProcessMemberColumns(DBTable table, XPMemberInfo processedMemberInfo, string mappingPath, bool isKey, bool processFk, bool isReferenceKeyMember) {
			if(processedMemberInfo.ReferenceType != null) {
				XPMemberInfo keyProperty = processedMemberInfo.ReferenceType.KeyProperty;
				List<DBColumn> columns = ProcessMemberColumns(table, keyProperty, mappingPath, isKey, false, true);
				if(processFk)
					ProcessForeignKey(table, processedMemberInfo, columns);
				return columns;
			} else if(processedMemberInfo.SubMembers.Count == 0) {
				List<DBColumn> columns = new List<DBColumn>(1);
				columns.Add(AddColumn(table, processedMemberInfo, mappingPath, isKey, isReferenceKeyMember));
				return columns;
			} else {
				List<DBColumn> columns = new List<DBColumn>();
				foreach(XPMemberInfo mi in processedMemberInfo.SubMembers) {
					if(mi.IsPersistent) {
						columns.AddRange(ProcessMemberColumns(table, mi, mappingPath + mi.MappingField, isKey, processFk, isReferenceKeyMember));
					}
				}
				return columns;
			}
		}
		static void ProcessForeignKey(DBTable table, XPMemberInfo memberInfo, List<DBColumn> columns) {
			if(memberInfo.HasAttribute(typeof(NoForeignKeyAttribute)))
				return;
			StringCollection todo = new StringCollection();
			XPMemberInfo refKey = memberInfo.ReferenceType.KeyProperty;
			if(refKey.SubMembers.Count > 0) {
				foreach(XPMemberInfo mi in refKey.SubMembers) {
					if(mi.IsPersistent)
						todo.Add(mi.MappingField);
				}
			} else
				todo.Add(refKey.MappingField);
			DBForeignKey fk = new DBForeignKey(columns,
				memberInfo.ReferenceType.TableName, todo);
			if(!table.IsForeignKeyIncluded(fk))
				table.AddForeignKey(fk);
		}
		static void ProcessPrimaryKey(DBTable table, List<DBColumn> columns, XPClassInfo classInfo) {
			if(table.PrimaryKey == null)
				table.PrimaryKey = new DBPrimaryKey(columns);
			if(columns.Count == 1 && classInfo == classInfo.IdClass &&
				classInfo.KeyProperty.IsIdentity) {
				((DBColumn)columns[0]).IsIdentity = true;
			}
			if(classInfo.PersistentBaseClass != null && classInfo.TableMapType == MapInheritanceType.OwnTable) {
				StringCollection todo = new StringCollection();
				foreach(DBColumn col in columns)
					todo.Add(col.Name);
				table.AddForeignKey(new DBForeignKey(columns, classInfo.PersistentBaseClass.TableName, todo));
			}
		}
		public static DBColumn CreateColumn(DBTable table, XPMemberInfo memberInfo, string name, bool isReferenceKeyMember) {
			XPMemberInfo workMemberInfo = memberInfo.ReferenceType != null ?
				memberInfo.ReferenceType.KeyProperty :
				memberInfo;
			DBColumnType colType = DBColumn.GetColumnType(workMemberInfo.StorageType);
			bool isNullable = (isReferenceKeyMember || IsColumnNullable(memberInfo));
			object defaultValue = workMemberInfo.DefaultValue;
			if(!isNullable && defaultValue == null && string.IsNullOrEmpty(workMemberInfo.DbDefaultValue)) {
				defaultValue = GetDefaultValueForColumn(colType);
			}
			DBColumn dBColumn = new DBColumn(name, memberInfo.IsKey, memberInfo.HasAttribute(typeof(DbTypeAttribute)) ?
				((DbTypeAttribute)memberInfo.GetAttributeInfo(typeof(DbTypeAttribute))).DbColumnTypeName : String.Empty,
				workMemberInfo.MappingFieldSize, colType, isNullable, defaultValue);
			dBColumn.DbDefaultValue = workMemberInfo.DbDefaultValue;
			return dBColumn;
		}
		static bool IsColumnNullable(XPMemberInfo memberInfo) {
			if(memberInfo.IsNullable != null) {
				return memberInfo.IsNullable.Value;
			}
			NullableBehavior nullableBehavior;
			if(memberInfo.Owner.NullableBehavior != NullableBehavior.Default) {
				nullableBehavior = memberInfo.Owner.NullableBehavior;
			} else {
				if(memberInfo.Owner.Dictionary.NullableBehavior != NullableBehavior.Default) {
					nullableBehavior = memberInfo.Owner.Dictionary.NullableBehavior;
				} else {
					nullableBehavior = XpoDefault.NullableBehavior;
				}
			}
			switch(nullableBehavior) {
				case NullableBehavior.ByUnderlyingType:
					if(memberInfo.MemberType.IsGenericType && memberInfo.MemberType.GetGenericTypeDefinition() == typeof(Nullable<>)) {
						return true;
					} else {
						if(memberInfo.IsKey || memberInfo is ServiceField) {
							return true;
						} else {
							return !memberInfo.MemberType.IsValueType;
						}
					};
				case NullableBehavior.AlwaysAllowNulls:
				case NullableBehavior.Default:
				default:
					return true;
			}
		}
		static object GetDefaultValueForColumn(DBColumnType type) {
			switch(type) {
				case DBColumnType.Boolean:
					return false;
				case DBColumnType.Byte:
					return (byte)0;
				case DBColumnType.Decimal:
					return 0.0m;
				case DBColumnType.Double:
					return 0.0d;
				case DBColumnType.Int16:
					return (Int16)0;
				case DBColumnType.Int32:
					return 0;
				case DBColumnType.Int64:
					return (Int64)0;
				case DBColumnType.SByte:
					return (sbyte)0;
				case DBColumnType.Single:
					return (Single)0;
				case DBColumnType.UInt16:
					return (UInt16)0;
				case DBColumnType.UInt32:
					return (uint)0;
				case DBColumnType.UInt64:
					return (UInt64)0;
				case DBColumnType.ByteArray:
				default:
					return null;
			}
		}
	}
}
