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

#pragma warning disable DX0024
namespace DevExpress.Xpo.DB {
	using System;
	using System.Data;
	using System.Text;
	using System.Collections;
	using System.Collections.Specialized;
	using System.Diagnostics;
	using System.Globalization;
	using DevExpress.Xpo.DB;
	using System.Text.RegularExpressions;
	using DevExpress.Data.Filtering;
	using DevExpress.Xpo.DB.Exceptions;
	using DevExpress.Xpo.DB.Helpers;
	using DevExpress.Xpo;
	using System.Collections.Generic;
	using DevExpress.Utils;
	using DevExpress.Data.Helpers;
	using System.Threading;
	using System.Threading.Tasks;
	public class AseConnectionProvider : ConnectionProviderSql {
		public const string XpoProviderTypeString = "Ase";
		const string MulticolumnIndexesAreNotSupported = "Multicolumn indexes are not supported.";
		ReflectConnectionHelper helper;
		ReflectConnectionHelper ConnectionHelper {
			get {
				if(helper == null)
					helper = new ReflectConnectionHelper(Connection, "Sybase.Data.AseClient.AseException");
				return helper;
			}
		}
		DbTypeMapperBase dbTypeMapper;
		protected override DbTypeMapperBase DbTypeMapper {
			get {
				if(dbTypeMapper == null) {
					Type aseParamType = ConnectionHelper.GetType("Sybase.Data.AseClient.AseParameter");
					Type aseDbTypeType = ConnectionHelper.GetType("Sybase.Data.AseClient.AseDbType");
					dbTypeMapper = (DbTypeMapperBase)Activator.CreateInstance(typeof(DbTypeMapperAse<,>).MakeGenericType(aseDbTypeType, aseParamType));
				}
				return dbTypeMapper;
			}
		}
		public static bool? GlobalExecuteUpdateSchemaInTransaction;
		public bool? ExecuteUpdateSchemaInTransaction = true;
		protected override bool InternalExecuteUpdateSchemaInTransaction {
			get {
				if(ExecuteUpdateSchemaInTransaction.HasValue || GlobalExecuteUpdateSchemaInTransaction.HasValue) {
					return ExecuteUpdateSchemaInTransaction.HasValue ? ExecuteUpdateSchemaInTransaction.Value : GlobalExecuteUpdateSchemaInTransaction.Value;
				} else {
					return false;
				}
			}
		}
		public static string GetConnectionString(string server, string database, string userId, string password) {
			return GetConnectionString(server, 5000, database, userId, password);
		}
		public static string GetConnectionString(string server, int port, string database, string userId, string password) {
			return String.Format("{5}={6};Port={4};Data Source={0};User ID={1};Password={2};Initial Catalog={3};persist security info=true",
			   EscapeConnectionStringArgument(server), EscapeConnectionStringArgument(userId), EscapeConnectionStringArgument(password), EscapeConnectionStringArgument(database), port, DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString);
		}
		public static IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			IDbConnection connection = CreateConnection(connectionString);
			objectsToDisposeOnDisconnect = new IDisposable[] { connection };
			return CreateProviderFromConnection(connection, autoCreateOption);
		}
		public static IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return new AseConnectionProvider(connection, autoCreateOption);
		}
		static AseConnectionProvider() {
			RegisterDataStoreProvider(XpoProviderTypeString, new DataStoreCreationFromStringDelegate(CreateProviderFromString));
			RegisterDataStoreProvider("AseConnection", new DataStoreCreationFromConnectionDelegate(CreateProviderFromConnection));
			RegisterFactory(new AseProviderFactory());
		}
		public static void Register() { }
		public AseConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption)
			: base(connection, autoCreateOption, true) {
		}
		protected AseConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption, bool openConnection)
			: base(connection, autoCreateOption, openConnection) {
		}
		protected override string GetSqlCreateColumnTypeForBoolean(DBTable table, DBColumn column) {
			return "bit";
		}
		protected override string GetSqlCreateColumnTypeForByte(DBTable table, DBColumn column) {
			return "tinyint";
		}
		protected override string GetSqlCreateColumnTypeForSByte(DBTable table, DBColumn column) {
			return "numeric(3,0)";
		}
		protected override string GetSqlCreateColumnTypeForChar(DBTable table, DBColumn column) {
			return "unichar(1)";
		}
		protected override string GetSqlCreateColumnTypeForDecimal(DBTable table, DBColumn column) {
			return "money";
		}
		protected override string GetSqlCreateColumnTypeForDouble(DBTable table, DBColumn column) {
			return "double precision";
		}
		protected override string GetSqlCreateColumnTypeForSingle(DBTable table, DBColumn column) {
			return "real";
		}
		protected override string GetSqlCreateColumnTypeForInt32(DBTable table, DBColumn column) {
			return "int";
		}
		protected override string GetSqlCreateColumnTypeForUInt32(DBTable table, DBColumn column) {
			return "unsigned integer";
		}
		protected override string GetSqlCreateColumnTypeForInt16(DBTable table, DBColumn column) {
			return "smallint";
		}
		protected override string GetSqlCreateColumnTypeForUInt16(DBTable table, DBColumn column) {
			return "unsigned smallint";
		}
		protected override string GetSqlCreateColumnTypeForInt64(DBTable table, DBColumn column) {
			return "bigint";
		}
		protected override string GetSqlCreateColumnTypeForUInt64(DBTable table, DBColumn column) {
			return "unsigned bigint";
		}
		public const int MaximumStringSize = 800;
		protected override string GetSqlCreateColumnTypeForString(DBTable table, DBColumn column) {
			if(column.Size > 0 && column.Size <= MaximumStringSize)
				return "univarchar(" + column.Size.ToString(CultureInfo.InvariantCulture) + ')';
			else
				return "unitext";
		}
		protected override string GetSqlCreateColumnTypeForDateTime(DBTable table, DBColumn column) {
			return "datetime";
		}
		protected override string GetSqlCreateColumnTypeForGuid(DBTable table, DBColumn column) {
			return "char(36)";
		}
		protected override string GetSqlCreateColumnTypeForByteArray(DBTable table, DBColumn column) {
			return "image";
		}
		const int MaxVarLength = 16384;
		string GetSqlCreateColumnTypeSp(DBTable table, DBColumn column) {
			string type = GetSqlCreateColumnType(table, column);
			switch(type) {
				case "image":
					return string.Format("varbinary({0})", MaxVarLength);
				case "unitext":
					return string.Format("varchar({0})", MaxVarLength);
			}
			return type;
		}
		public override string GetSqlCreateColumnFullAttributes(DBTable table, DBColumn column) {
			return null;
		}
		public override string GetSqlCreateColumnFullAttributes(DBTable table, DBColumn column, bool forTableCreate) {
			string result = GetSqlCreateColumnFullAttributes(table, column);
			if(!string.IsNullOrEmpty(result)) {
				return result;
			}
			result = GetSqlCreateColumnType(table, column);
			if(!column.IsIdentity) {
				if(!string.IsNullOrEmpty(column.DbDefaultValue)) {
					result += string.Concat(" DEFAULT ", column.DbDefaultValue);
				} else {
					if(column.DefaultValue != null && column.DefaultValue != System.DBNull.Value) {
						string formattedDefaultValue = FormatConstant(column.DefaultValue);
						result += string.Concat(" DEFAULT ", formattedDefaultValue);
					}
				}
			}
			if(column.IsKey || !column.IsNullable || column.ColumnType == DBColumnType.Boolean) {
				if(column.IsIdentity && (column.ColumnType == DBColumnType.Int32 || column.ColumnType == DBColumnType.Int64) && IsSingleColumnPKColumn(table, column)) {
					result += " IDENTITY";
				} else {
					result += " NOT NULL";
				}
			} else {
				result += " NULL";
			}
			return result;
		}
		protected override object ConvertToDbParameter(object clientValue, TypeCode clientValueTypeCode) {
			switch(clientValueTypeCode) {
				case TypeCode.Object:
					if(clientValue is Guid) {
						return clientValue.ToString();
					}
					break;
				case TypeCode.SByte:
					return (Int16)(SByte)clientValue;
				case TypeCode.UInt16:
					return (Int32)(UInt16)clientValue;
				case TypeCode.UInt32:
					return (Int64)(UInt32)clientValue;
				case TypeCode.UInt64:
					return (Decimal)(UInt64)clientValue;
				case TypeCode.Int64:
					return (Decimal)(Int64)clientValue;
			}
			return base.ConvertToDbParameter(clientValue, clientValueTypeCode);
		}
		protected override Int64 GetIdentity(Query sql) {
			object value = GetScalar(new Query(sql.Sql + "\nselect @@Identity", sql.Parameters, sql.ParametersNames));
			return (value as IConvertible).ToInt64(CultureInfo.InvariantCulture);
		}
		protected override async Task<Int64> GetIdentityAsync(Query sql, AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken) {
			object value = await GetScalarAsync(new Query(sql.Sql + "\nselect @@Identity", sql.Parameters, sql.ParametersNames), asyncOperationId, cancellationToken);
			return (value as IConvertible).ToInt64(CultureInfo.InvariantCulture);
		}
		protected override IDbConnection CreateConnection() {
			return ConnectionHelper.GetConnection(ConnectionString);
		}
		readonly static string[] assemblyNames = new string[]{
			"Sybase.AdoNet4.AseClient",
			"Sybase.AdoNet35.AseClient",
			"Sybase.AdoNet2.AseClient",
			"Sybase.Data.AseClient"
		};
		static int assemblyFoundIndex;
		public static IDbConnection CreateConnection(string connectionString) {
			var assemblies = new string[assemblyNames.Length];
			var types = new string[assemblyNames.Length];
			for(int i = 0; i < assemblyNames.Length; i++) {
				assemblies[i] = assemblyNames[i];
				types[i] = "Sybase.Data.AseClient.AseConnection";
			}
			IDbConnection connection = ReflectConnectionHelper.GetConnection(assemblies, types, true, ref assemblyFoundIndex);
			connection.ConnectionString = connectionString;
			return connection;
		}
		protected override void CreateDataBase() {
			const int CannotOpenDatabaseError = 911;
			try {
				ConnectionStringParser parser = new ConnectionStringParser(ConnectionString);
				parser.RemovePartByName("Pooling");
				string connectString = parser.GetConnectionString() + ";Pooling=false";
				using(IDbConnection conn = ConnectionHelper.GetConnection(connectString)) {
					conn.Open();
				}
			} catch(Exception e) {
				object o;
				if(ConnectionHelper.TryGetExceptionProperty(e, "Errors", out o)
					&& ((ICollection)o).Count > 0
					&& ((int)ReflectConnectionHelper.GetPropertyValue(ReflectConnectionHelper.GetCollectionFirstItem(((ICollection)o)), "MessageNumber")) == CannotOpenDatabaseError
					&& CanCreateDatabase) {
					ConnectionStringParser helper = new ConnectionStringParser(ConnectionString);
					string dbName = helper.GetPartByName("initial catalog");
					helper.RemovePartByName("initial catalog");
					string connectToServer = helper.GetConnectionString();
					using(IDbConnection conn = ConnectionHelper.GetConnection(connectToServer)) {
						conn.Open();
						using(IDbCommand c = conn.CreateCommand()) {
							c.CommandText = "Create Database " + dbName;
							c.ExecuteNonQuery();
							c.CommandText = "exec master.dbo.sp_dboption " + dbName + ", 'ddl in tran', true";
							c.ExecuteNonQuery();
						}
					}
				} else
					throw new UnableToOpenDatabaseException(XpoDefault.ConnectionStringRemovePassword(ConnectionString), e);
			}
		}
		protected override bool IsConnectionBroken(Exception e) {
			object o;
			if(ConnectionHelper.TryGetExceptionProperty(e, "Errors", out o)
				&& ((ICollection)o).Count > 0) {
				object error = ReflectConnectionHelper.GetCollectionFirstItem((ICollection)o);
				int messageNumber = (int)ReflectConnectionHelper.GetPropertyValue(error, "MessageNumber");
				if(messageNumber == 30046) {
					Connection.Close();
					return true;
				}
			}
			return base.IsConnectionBroken(e);
		}
		protected override Exception WrapException(Exception e, IDbCommand query) {
			object o;
			if(ConnectionHelper.TryGetExceptionProperty(e, "Errors", out o)
				&& ((ICollection)o).Count > 0) {
				object error = ReflectConnectionHelper.GetCollectionFirstItem((ICollection)o);
				int messageNumber = (int)ReflectConnectionHelper.GetPropertyValue(error, "MessageNumber");
				if(messageNumber == 208 || messageNumber == 207) {
					return new SchemaCorrectionNeededException((string)ReflectConnectionHelper.GetPropertyValue(error, "Message"), e);
				}
				if(messageNumber == 2601 || messageNumber == 547) {
					return new ConstraintViolationException(query.CommandText, GetParametersString(query), e);
				}
				if(messageNumber == 226) {
					string msg = Res.GetString(Res.ASE_CommandNotAllowedWithinMultiStatementTransaction);
					return new DataException(msg, e);
				}
			}
			return base.WrapException(e, query);
		}
		delegate bool TablesFilter(DBTable table);
		SelectStatementResult GetDataForTables(ICollection tables, TablesFilter filter, string queryText) {
			QueryParameterCollection parameters = new QueryParameterCollection();
			StringCollection inList = new StringCollection();
			int i = 0;
			foreach(DBTable table in tables) {
				if(filter == null || filter(table)) {
					parameters.Add(CreateParameterForSystemQuery(i, ComposeSafeTableName(table.Name)));
					inList.Add("@p" + i.ToString(CultureInfo.InvariantCulture));
					++i;
				}
			}
			if(inList.Count == 0)
				return new SelectStatementResult();
			return SelectData(new Query(string.Format(CultureInfo.InvariantCulture, queryText, StringListHelper.DelimitedText(inList, ",")), parameters, inList));
		}
		ParameterValue CreateParameterForSystemQuery(int tag, string value, int size = 255) {
			return new ParameterValue(tag) { Value = value, DBType = DBColumnType.String, Size = size };
		}
		DBColumnType GetTypeFromNumber(byte type, byte prec, int length, short userType, byte charSize, out int effectiveLength) {
			effectiveLength = 0;
			switch(type) {
				case 38:
					switch(length) {
						case 1:
							return DBColumnType.Byte;
						case 2:
							return DBColumnType.Int16;
						case 4:
							return DBColumnType.Int32;
						case 8:
							return DBColumnType.Int64;
						default:
							return DBColumnType.Unknown;
					}
				case 68:
					switch(length) {
						case 1:
							return DBColumnType.Byte;
						case 2:
							return DBColumnType.UInt16;
						case 4:
							return DBColumnType.UInt32;
						case 8:
							return DBColumnType.UInt64;
						default:
							return DBColumnType.Unknown;
					}
				case 56:
					return DBColumnType.Int32;
				case 66:
					return DBColumnType.UInt32;
				case 52:
					return DBColumnType.Int16;
				case 65:
					return DBColumnType.UInt16;
				case 50:
					return DBColumnType.Boolean;
				case 39:
				case 35:
					if(userType == 2)
						effectiveLength = length;
					else
						effectiveLength = length / charSize;
					return DBColumnType.String;
				case 174:
				case 155:
					effectiveLength = length / 2;
					return DBColumnType.String;
				case 34:
				case 45:
					effectiveLength = length;
					return DBColumnType.ByteArray;
				case 111:
				case 61:
					return DBColumnType.DateTime;
				case 109:
					return DBColumnType.Double;
				case 110:
				case 60:
					return DBColumnType.Decimal;
				case 108:
				case 63:
					if(prec <= 3)
						return DBColumnType.SByte;
					if(prec <= 5)
						return DBColumnType.Int16;
					if(prec <= 10)
						return DBColumnType.Int32;
					return DBColumnType.Int64;
			}
			return DBColumnType.Unknown;
		}
		static string GetFullTypeName(string typeName, byte precision, byte scale, int length) {
			if(string.IsNullOrEmpty(typeName)) {
				return null;
			}
			switch(typeName) {
				case "unichar":
				case "univarchar":
				case "char":
				case "varchar":
					return string.Concat(typeName, "(", length.ToString(CultureInfo.InvariantCulture), ")");
				case "binary":
				case "varbinary":
					return string.Concat(typeName, "(", length.ToString(CultureInfo.InvariantCulture), ")");
				case "float":
					return precision == 0 ? typeName : string.Concat(typeName, "(", precision.ToString(CultureInfo.InvariantCulture), ")");
				case "decimal":
				case "numeric":
					return precision == 0 ? typeName : string.Format(CultureInfo.InvariantCulture, "{0}({1},{2})", typeName, precision, scale);
			}
			return typeName;
		}
		void GetColumns(DBTable table) {
			foreach(SelectStatementResultRow row in SelectData(new Query("select c.name, c.type, c.prec, c.length, c.usertype, @@ncharsize, c.status, dflt.name defaultValueName, tp.name, c.scale " +
				"from syscolumns c " +
				"left join sysobjects t on c.id = t.id " +
				"left join systypes tp on c.usertype = tp.usertype " +
				"left join sysobjects dflt on c.cdefault=dflt.id and dflt.type='D' " +
				"where t.name = @p1",
				new QueryParameterCollection(CreateParameterForSystemQuery(1, ComposeSafeTableName(table.Name))), new string[] { "@p1" })).Rows) {
				int effectiveLength;
				string typeName = (string)row.Values[8];
				byte typeCode = (byte)row.Values[1];
				byte precision = row.Values[2] is DBNull ? (byte)0 : (byte)row.Values[2];
				byte scale = row.Values[9] is DBNull ? (byte)0 : (byte)row.Values[9];
				int length = (int)row.Values[3];
				short userTypeCode = (short)row.Values[4];
				byte charSize = (byte)row.Values[5];
				DBColumnType type = GetTypeFromNumber(typeCode, precision, length, userTypeCode, charSize, out effectiveLength);
				bool isNullable = (Convert.ToInt32(row.Values[6]) & 0x08) != 0;
				string dbDefaultValue = null;
				object defaultValue = null;
				string dbDefaultValueName = (row.Values[7] as string);
				if(!string.IsNullOrEmpty(dbDefaultValueName)) {
					dbDefaultValue = GetColumnDefaultValueSqlExpression(dbDefaultValueName);
					if(!string.IsNullOrEmpty(dbDefaultValue)) {
						if(dbDefaultValue == "''" && (type == DBColumnType.Char || type == DBColumnType.String)) {
							defaultValue = "";
						} else {
							string scalarQuery = string.Concat("select ", dbDefaultValue);
							try {
								defaultValue = FixDBNullScalar(GetScalar(new Query(scalarQuery)));
							} catch { }
						}
					}
					if(defaultValue != null) {
						ReformatReadValueArgs refmtArgs = new ReformatReadValueArgs(DBColumn.GetType(type));
						refmtArgs.AttachValueReadFromDb(defaultValue);
						try {
							defaultValue = ReformatReadValue(defaultValue, refmtArgs);
						} catch {
							defaultValue = null;
						}
					}
				}
				DBColumn column = new DBColumn((string)row.Values[0], false, GetFullTypeName(typeName, precision, scale, effectiveLength), effectiveLength, type, isNullable, defaultValue);
				column.IsIdentity = (Convert.ToInt32(row.Values[6]) & 128) == 128;
				column.DbDefaultValue = dbDefaultValue;
				table.AddColumn(column);
			}
		}
		string GetColumnDefaultValueSqlExpression(string dbDefaultValueName) {
			Query query = new Query("sp_helptext @p1", new QueryParameterCollection(new ConstantValue(dbDefaultValueName)), new string[] { "p1" });
			using(IDbCommand command = CreateCommand(query)) {
				SelectStatementResult[] results = InternalGetData(command, null, 0, 0, false);
				StringBuilder sb = new StringBuilder();
				foreach(SelectStatementResultRow row in results[1].Rows) {
					sb.Append(row.Values[0].ToString());
				}
				string sqlExpr = sb.ToString();
				if(sqlExpr.StartsWith("DEFAULT ")) {
					return sqlExpr.Remove(0, 8).Trim();
				} else {
					return sqlExpr.Trim();
				}
			}
		}
		void GetPrimaryKey(DBTable table) {
			SelectStatementResult data = SelectData(new Query("select index_col(o.name, i.indid, 1), i.keycnt from sysindexes i join sysobjects o on o.id = i.id where i.status & 2048 <> 0 and o.name = @p1", new QueryParameterCollection(CreateParameterForSystemQuery(1, ComposeSafeTableName(table.Name))), new string[] { "@p1" }));
			if(data.Rows.Length > 0) {
				if((short)data.Rows[0].Values[1] != 1)
					throw new NotImplementedException(MulticolumnIndexesAreNotSupported); 
				StringCollection cols = new StringCollection();
				cols.Add((string)data.Rows[0].Values[0]);
				foreach(string columnName in cols) {
					DBColumn column = table.GetColumn(columnName);
					if(column != null)
						column.IsKey = true;
				}
				table.PrimaryKey = new DBPrimaryKey(cols);
			}
		}
		public override void CreateIndex(DBTable table, DBIndex index) {
			if(table.Name != "XPObjectType")
				base.CreateIndex(table, index);
		}
		void GetIndexes(DBTable table) {
			SelectStatementResult data = SelectData(new Query("select index_col(o.name, i.indid, 1), i.keycnt, (i.status & 2) from sysindexes i join sysobjects o on o.id = i.id where o.name = @p1 and i.keycnt > 1 and i.status & 2048 = 0", new QueryParameterCollection(CreateParameterForSystemQuery(1, ComposeSafeTableName(table.Name))), new string[] { "@p1" }));
			foreach(SelectStatementResultRow row in data.Rows) {
				if((short)row.Values[1] != 2)
					throw new NotImplementedException(MulticolumnIndexesAreNotSupported); 
				StringCollection cols = new StringCollection();
				cols.Add((string)row.Values[0]);
				table.Indexes.Add(new DBIndex(cols, Convert.ToInt32(row.Values[2]) == 2));
			}
		}
		void GetForeignKeys(DBTable table) {
			SelectStatementResult data = SelectData(new Query(
@"select f.keycnt, fc.name, pc.name, r.name from sysreferences f
join sysobjects o on o.id = f.tableid
join sysobjects r on r.id = f.reftabid
join syscolumns fc on f.fokey1 = fc.colid and fc.id = o.id
join syscolumns pc on f.refkey1 = pc.colid and pc.id = r.id
where o.name = @p1", new QueryParameterCollection(CreateParameterForSystemQuery(1, ComposeSafeTableName(table.Name))), new string[] { "@p1" }));
			foreach(SelectStatementResultRow row in data.Rows) {
				if((short)row.Values[0] != 1)
					throw new NotImplementedException(MulticolumnIndexesAreNotSupported); 
				StringCollection pkc = new StringCollection();
				StringCollection fkc = new StringCollection();
				pkc.Add((string)row.Values[1]);
				fkc.Add((string)row.Values[2]);
				table.ForeignKeys.Add(new DBForeignKey(pkc, (string)row.Values[3], fkc));
			}
		}
		public override void GetTableSchema(DBTable table, bool checkIndexes, bool checkForeignKeys) {
			GetColumns(table);
			GetPrimaryKey(table);
			if(checkIndexes)
				GetIndexes(table);
			if(checkForeignKeys)
				GetForeignKeys(table);
		}
		public override ICollection CollectTablesToCreate(ICollection tables) {
			Hashtable dbTables = new Hashtable();
			foreach(SelectStatementResultRow row in GetDataForTables(tables, null, "select name,type from sysobjects where name in ({0}) and type in ('U', 'V')").Rows)
				dbTables.Add(row.Values[0], ((string)row.Values[1]).Trim() == "V");
			ArrayList list = new ArrayList();
			foreach(DBTable table in tables) {
				object o = dbTables[ComposeSafeTableName(table.Name)];
				if(o == null)
					list.Add(table);
				else
					table.IsView = (bool)o;
			}
			return list;
		}
		protected override int GetSafeNameTableMaxLength() {
			return 28;
		}
		public override string FormatTable(string schema, string tableName) {
			return string.Format(CultureInfo.InvariantCulture, "[{0}]", tableName);
		}
		public override string FormatTable(string schema, string tableName, string tableAlias) {
			return string.Format(CultureInfo.InvariantCulture, "[{0}] {1}", tableName, tableAlias);
		}
		public override string FormatColumn(string columnName) {
			return string.Format(CultureInfo.InvariantCulture, "[{0}]", columnName);
		}
		public override string FormatColumn(string columnName, string tableAlias) {
			return string.Format(CultureInfo.InvariantCulture, "{1}.[{0}]", columnName, tableAlias);
		}
		public override string FormatSelect(string selectedPropertiesSql, string fromSql, string whereSql, string orderBySql, string groupBySql, string havingSql, int topSelectedRecords) {
			string modificatorsSql = string.Format(CultureInfo.InvariantCulture, (topSelectedRecords != 0) ? "top {0} " : string.Empty, topSelectedRecords);
			string expandedWhereSql = whereSql != null ? string.Format(CultureInfo.InvariantCulture, "{0}where {1}", Environment.NewLine, whereSql) : string.Empty;
			string expandedOrderBySql = orderBySql != null ? string.Format(CultureInfo.InvariantCulture, "{0}order by {1}", Environment.NewLine, orderBySql) : string.Empty;
			string expandedHavingSql = havingSql != null ? string.Format(CultureInfo.InvariantCulture, "{0}having {1}", Environment.NewLine, havingSql) : string.Empty;
			string expandedGroupBySql = groupBySql != null ? string.Format(CultureInfo.InvariantCulture, "{0}group by {1}", Environment.NewLine, groupBySql) : string.Empty;
			if(topSelectedRecords == 0)
				return string.Format(CultureInfo.InvariantCulture, "select {0}{1} from {2}{3}{4}{5}{6}", modificatorsSql, selectedPropertiesSql, fromSql, expandedWhereSql, expandedGroupBySql, expandedHavingSql, expandedOrderBySql);
			else
				return string.Format(CultureInfo.InvariantCulture, "set rowcount {0} select {1} from {2}{3}{4}{5}{6} set rowcount 0", topSelectedRecords, selectedPropertiesSql, fromSql, expandedWhereSql, expandedGroupBySql, expandedHavingSql, expandedOrderBySql);
		}
		public override string FormatInsertDefaultValues(string tableName) {
			return string.Format(CultureInfo.InvariantCulture, "insert into {0} values()", tableName);
		}
		public override string FormatInsert(string tableName, string fields, string values) {
			return string.Format(CultureInfo.InvariantCulture, "insert into {0}({1})values({2})",
				tableName, fields, values);
		}
		public override string FormatUpdate(string tableName, string sets, string whereClause) {
			return string.Format(CultureInfo.InvariantCulture, "update {0} set {1} where {2}",
				tableName, sets, whereClause);
		}
		public override string FormatDelete(string tableName, string whereClause) {
			return string.Format(CultureInfo.InvariantCulture, "delete from {0} where {1}", tableName, whereClause);
		}
		public override string FormatBinary(BinaryOperatorType operatorType, string leftOperand, string rightOperand) {
			switch(operatorType) {
				case BinaryOperatorType.Modulo:
					return string.Format(CultureInfo.InvariantCulture, "{0} % {1}", leftOperand, rightOperand);
				case BinaryOperatorType.BitwiseAnd:
					return string.Format(CultureInfo.InvariantCulture, "({0} & {1})", leftOperand, rightOperand);
				case BinaryOperatorType.BitwiseOr:
					return string.Format(CultureInfo.InvariantCulture, "({0} | {1})", leftOperand, rightOperand);
				case BinaryOperatorType.BitwiseXor:
					return string.Format(CultureInfo.InvariantCulture, "({0} ^ {1})", leftOperand, rightOperand);
				default:
					return base.FormatBinary(operatorType, leftOperand, rightOperand);
			}
		}
		public override string FormatFunction(FunctionOperatorType operatorType, params string[] operands) {
			switch(operatorType) {
				case FunctionOperatorType.Acos:
					return string.Format(CultureInfo.InvariantCulture, "acos({0})", operands[0]);
				case FunctionOperatorType.Asin:
					return string.Format(CultureInfo.InvariantCulture, "asin({0})", operands[0]);
				case FunctionOperatorType.Atn:
					return string.Format(CultureInfo.InvariantCulture, "atan({0})", operands[0]);
				case FunctionOperatorType.Atn2:
					return string.Format(CultureInfo.InvariantCulture, "(case when {0} = 0 then (case when {1} >= 0 then 0 else atan(1) * 4 end) else 2 * atan({0} / (sqrt({1} * {1} + {0} * {0}) + {1})) end)", operands[0], operands[1]);
				case FunctionOperatorType.Cosh:
					return string.Format(CultureInfo.InvariantCulture, "((exp({0}) + exp({0} * -1)) / 2)", operands[0]);
				case FunctionOperatorType.Sinh:
					return string.Format(CultureInfo.InvariantCulture, "((exp({0}) - exp({0} * -1)) / 2)", operands[0]);
				case FunctionOperatorType.Tanh:
					return string.Format(CultureInfo.InvariantCulture, "((exp({0} * 2) - 1) / (exp({0} * 2) + 1))", operands[0]);
				case FunctionOperatorType.Log:
					return FnLog(operands);
				case FunctionOperatorType.Log10:
					return string.Format(CultureInfo.InvariantCulture, "log10({0})", operands[0]);
				case FunctionOperatorType.Round:
					switch(operands.Length) {
						case 1:
							return string.Format(CultureInfo.InvariantCulture, "round({0}, 0)", operands[0]);
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "round({0}, {1})", operands[0], operands[1]);
					}
					goto default;
				case FunctionOperatorType.Sqr:
					return string.Format(CultureInfo.InvariantCulture, "sqrt({0})", operands[0]);
				case FunctionOperatorType.ToInt:
					return string.Format(CultureInfo.InvariantCulture, "cast({0} AS integer)", operands[0]);
				case FunctionOperatorType.ToLong:
					return string.Format(CultureInfo.InvariantCulture, "cast({0} AS bigint)", operands[0]);
				case FunctionOperatorType.ToFloat:
					return string.Format(CultureInfo.InvariantCulture, "cast({0} AS real)", operands[0]);
				case FunctionOperatorType.ToDouble:
					return string.Format(CultureInfo.InvariantCulture, "cast({0} AS double precision)", operands[0]);
				case FunctionOperatorType.ToDecimal:
					return string.Format(CultureInfo.InvariantCulture, "cast({0} AS money)", operands[0]);
				case FunctionOperatorType.BigMul:
					return string.Format(CultureInfo.InvariantCulture, "cast({0} * {1} as bigint)", operands[0], operands[1]);
				case FunctionOperatorType.Max:
					return string.Format(CultureInfo.InvariantCulture, "(case when {0} > {1} then {0} else {1} end)", operands[0], operands[1]);
				case FunctionOperatorType.Min:
					return string.Format(CultureInfo.InvariantCulture, "(case when {0} < {1} then {0} else {1} end)", operands[0], operands[1]);
				case FunctionOperatorType.Rnd:
					return "Rand()";
				case FunctionOperatorType.CharIndex:
					return FnCharIndex(operands);
				case FunctionOperatorType.PadLeft:
					return FnLpad(operands);
				case FunctionOperatorType.PadRight:
					return FnRpad(operands);
				case FunctionOperatorType.Remove:
					return FnRemove(operands);
				case FunctionOperatorType.GetMilliSecond:
					return string.Format(CultureInfo.InvariantCulture, "datepart(ms, {0})", operands[0]);
				case FunctionOperatorType.AddTicks:
					return string.Format(CultureInfo.InvariantCulture, "dateadd(ms, (cast({1} as bigint) / 10000) % 86400000, dateadd(day, (cast({1} as bigint) / 10000) / 86400000, {0}))", operands[0], operands[1]);
				case FunctionOperatorType.AddMilliSeconds:
					return string.Format(CultureInfo.InvariantCulture, "dateadd(ms, {1}, {0})", operands[0], operands[1]);
				case FunctionOperatorType.AddTimeSpan:
				case FunctionOperatorType.AddSeconds:
					return string.Format(CultureInfo.InvariantCulture, "dateadd(ms, cast((cast({1} as numeric(38,19)) * 1000) as bigint) % 86400000, dateadd(day, cast((cast({1} as numeric(38,19)) * 1000) / 86400000 as bigint), {0}))", operands[0], operands[1]);
				case FunctionOperatorType.AddMinutes:
					return string.Format(CultureInfo.InvariantCulture, "dateadd(ms, cast((cast({1} as numeric(38,19)) * 60000) as bigint) % 86400000, dateadd(day, cast((cast({1} as numeric(38,19)) * 60000) / 86400000 as bigint), {0}))", operands[0], operands[1]);
				case FunctionOperatorType.AddHours:
					return string.Format(CultureInfo.InvariantCulture, "dateadd(ms, cast((cast({1} as numeric(38,19)) * 3600000) as bigint) % 86400000, dateadd(day, cast((cast({1} as numeric(38,19)) * 3600000) / 86400000 as bigint), {0}))", operands[0], operands[1]);
				case FunctionOperatorType.AddDays:
					return string.Format(CultureInfo.InvariantCulture, "dateadd(ms, cast((cast({1} as numeric(38,19)) * 86400000) as bigint) % 86400000, dateadd(day, cast((cast({1} as numeric(38,19)) * 86400000) / 86400000 as bigint), {0}))", operands[0], operands[1]);
				case FunctionOperatorType.AddMonths:
					return string.Format(CultureInfo.InvariantCulture, "dateadd(month, {1}, {0})", operands[0], operands[1]);
				case FunctionOperatorType.AddYears:
					return string.Format(CultureInfo.InvariantCulture, "dateadd(year, {1}, {0})", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffYear:
					return string.Format(CultureInfo.InvariantCulture, "datediff(yy, {0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffMonth:
					return string.Format(CultureInfo.InvariantCulture, "datediff(mm, {0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffDay:
					return string.Format(CultureInfo.InvariantCulture, "datediff(dd, {0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffHour:
					return string.Format(CultureInfo.InvariantCulture, "datediff(hh, {0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffMinute:
					return string.Format(CultureInfo.InvariantCulture, "datediff(mi, {0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffSecond:
					return string.Format(CultureInfo.InvariantCulture, "datediff(ss, {0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffMilliSecond:
					return string.Format(CultureInfo.InvariantCulture, "datediff(ms, {0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffTick:
					return string.Format(CultureInfo.InvariantCulture, "(datediff(ms, {0}, {1}) * 10000)", operands[0], operands[1]);
				case FunctionOperatorType.Now:
					return "getdate()";
				case FunctionOperatorType.UtcNow:
					return "getutcdate()";
				case FunctionOperatorType.Today:
					return "cast(cast(getdate() as date) as datetime)";
				case FunctionOperatorType.GetDate:
					return string.Format(CultureInfo.InvariantCulture, "cast(cast({0} as date) as datetime)", operands[0]);
				case FunctionOperatorType.IsNull:
					switch(operands.Length) {
						case 1:
							return string.Format(CultureInfo.InvariantCulture, "(({0}) is null)", operands[0]);
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "isnull({0}, {1})", operands[0], operands[1]);
					}
					goto default;
				case FunctionOperatorType.IsNullOrEmpty:
					return string.Format(CultureInfo.InvariantCulture, "(({0}) is null or ({0}) = '')", operands[0]);
				case FunctionOperatorType.Contains:
					return string.Format(CultureInfo.InvariantCulture, "(CharIndEX({1}, {0}) > 0)", operands[0], operands[1]);
				case FunctionOperatorType.EndsWith:
					return string.Format(CultureInfo.InvariantCulture, "(RigHt({0}, Len({1})) = ({1}))", operands[0], operands[1]);
				default:
					return base.FormatFunction(operatorType, operands);
			}
		}
		readonly static char[] achtungChars = new char[] { '_', '%', '[', ']' };
		public override string FormatFunction(ProcessParameter processParameter, FunctionOperatorType operatorType, params object[] operands) {
			switch(operatorType) {
				case FunctionOperatorType.StartsWith:
					object secondOperand = operands[1];
					if(secondOperand is OperandValue && ((OperandValue)secondOperand).Value is string) {
						string operandString = (string)((OperandValue)secondOperand).Value;
						int likeIndex = operandString.IndexOfAny(achtungChars);
						if(likeIndex < 0) {
							return string.Format(CultureInfo.InvariantCulture, "({0} likE {1})", processParameter(operands[0]), processParameter(new ConstantValue(operandString + "%")));
						} else if(likeIndex > 0) {
							return string.Format(CultureInfo.InvariantCulture, "(({0} likE {2}) And (CharIndeX({1}, {0}) = 1))", processParameter(operands[0]), processParameter(secondOperand), processParameter(new ConstantValue(operandString.Substring(0, likeIndex) + "%")));
						}
					}
					return string.Format(CultureInfo.InvariantCulture, "(CharIndeX({1}, {0}) = 1)", processParameter(operands[0]), processParameter(operands[1]));
				default:
					return base.FormatFunction(processParameter, operatorType, operands);
			}
		}
		string FnRemove(string[] operands) {
			switch(operands.Length) {
				case 2:
					return string.Format(CultureInfo.InvariantCulture, "substring({0}, 1, {1})", operands[0], operands[1]);
				case 3:
					return string.Format(CultureInfo.InvariantCulture, "stuff({0}, {1} + 1, {2}, null)", operands[0], operands[1], operands[2]);
				default:
					throw new NotSupportedException();
			}
		}
		string FnRpad(string[] operands) {
			switch(operands.Length) {
				case 2:
					return string.Format(CultureInfo.InvariantCulture, "({0} + replicate(' ', {1} - char_length({0})))", operands[0], operands[1]);
				case 3:
					return string.Format(CultureInfo.InvariantCulture, "({0} + replicate({2}, {1} - char_length({0})))", operands[0], operands[1], operands[2]);
				default:
					throw new NotSupportedException();
			}
		}
		string FnLpad(string[] operands) {
			switch(operands.Length) {
				case 2:
					return string.Format(CultureInfo.InvariantCulture, "(replicate(' ', {1} - char_length({0})) + {0})", operands[0], operands[1]);
				case 3:
					return string.Format(CultureInfo.InvariantCulture, "(replicate({2}, {1} - char_length({0})) + {0})", operands[0], operands[1], operands[2]);
				default:
					throw new NotSupportedException();
			}
		}
		string FnLog(string[] operands) {
			switch(operands.Length) {
				case 1:
					return string.Format(CultureInfo.InvariantCulture, "log({0})", operands[0]);
				case 2:
					return string.Format(CultureInfo.InvariantCulture, "(log({0}) / log({1}))", operands[0], operands[1]);
				default:
					throw new NotSupportedException();
			}
		}
		string FnCharIndex(string[] operands) {
			switch(operands.Length) {
				case 2:
					return string.Format(CultureInfo.InvariantCulture, "(charindex({0}, {1}) - 1)", operands[0], operands[1]);
				case 3:
					return string.Format(CultureInfo.InvariantCulture, "(case when charindex({0}, substring({1}, {2} + 1, char_length({1}) - {2})) > 0 then charindex({0}, substring({1}, {2} + 1, char_length({1}) - {2})) + {2} - 1 else -1 end)", operands[0], operands[1], operands[2]);
				case 4:
					return string.Format(CultureInfo.InvariantCulture, "(case when charindex({0}, substring({1}, {2} + 1, {3} - {2})) > 0 then charindex({0}, substring({1}, {2} + 1, {3} - {2})) + {2} - 1 else -1 end)", operands[0], operands[1], operands[2], operands[3]);
				default:
					throw new NotSupportedException();
			}
		}
		public override string GetParameterName(OperandValue parameter, int index, ref bool createParameter) {
			object value = parameter.Value;
			createParameter = false;
			if(parameter is ConstantValue && value != null) {
				switch(Type.GetTypeCode(value.GetType())) {
					case TypeCode.Int32:
						return ((int)value).ToString(CultureInfo.InvariantCulture);
					case TypeCode.Boolean:
						return (bool)value ? "1" : "0";
					case TypeCode.String:
						return FormatString(value);
				}
			}
			createParameter = true;
			return "@p" + index.ToString(CultureInfo.InvariantCulture);
		}
		string FormatString(object value) {
			return "'" + ((string)value).Replace("'", "''") + "'";
		}
		protected override IDataParameter CreateParameter(IDbCommand command, object value, string name, DBColumnType dbType, string dbTypeName, int size) {
			IDbDataParameter param = (IDbDataParameter)CreateParameter(command);
			param.Value = value;
			param.ParameterName = name;
			QueryParameterMode parameterMode = GetQueryParameterMode();
			if(parameterMode != QueryParameterMode.Legacy) {
				if(!string.IsNullOrEmpty(dbTypeName)) {
					if(parameterMode == QueryParameterMode.SetTypeAndSize) {
						DbTypeMapper.SetParameterTypeAndSize(param, dbTypeName);
					} else {
						DbTypeMapper.SetParameterType(param, dbTypeName);
					}
				} else {
					if(parameterMode == QueryParameterMode.SetTypeAndSize) {
						DbTypeMapper.SetParameterTypeAndSize(param, dbType, size);
					} else {
						DbTypeMapper.SetParameterType(param, dbType);
					}
				}
			}
			if(value is byte[]) {
				((IDbTypeMapperAse)DbTypeMapper).SetParameterTypeImage(param);
			} else if(value is string) {
				if(((string)value).Length > MaximumStringSize) {
					((IDbTypeMapperAse)DbTypeMapper).SetParameterTypeUniText(param);
				} else {
					((IDbTypeMapperAse)DbTypeMapper).SetParameterTypeUniVarChar(param);
				}
			}
			if(parameterMode == QueryParameterMode.SetTypeAndSize) {
				ValidateParameterSize(command, param);
			}
			return param;
		}
		public override string FormatConstraint(string constraintName) {
			return string.Format(CultureInfo.InvariantCulture, "[{0}]", constraintName);
		}
		protected string FormatConstant(object value) {
			TypeCode tc = DXTypeExtensions.GetTypeCode(value.GetType());
			switch(tc) {
				case DXTypeExtensions.TypeCodeDBNull:
				case TypeCode.Empty:
					return "NULL";
				case TypeCode.Boolean:
					return ((bool)value) ? "1" : "0";
				case TypeCode.Char:
					if(value is char && Convert.ToInt32(value) < 32) {
						return string.Concat("char(", Convert.ToInt32(value).ToString(CultureInfo.InvariantCulture), ")");
					} else {
						return "'" + (char)value + "'";
					}
				case TypeCode.DateTime:
					DateTime datetimeValue = (DateTime)value;
					string dateTimeFormatPattern = "yyyy-MM-dd HH:mm:ss";
					return string.Format("cast('{0}' as datetime)", datetimeValue.ToString(dateTimeFormatPattern, CultureInfo.InvariantCulture));
				case TypeCode.String:
					return FormatString(value);
				case TypeCode.Decimal:
					return FixNonFixedText(((Decimal)value).ToString(CultureInfo.InvariantCulture));
				case TypeCode.Double:
					return FixNonFixedText(((Double)value).ToString("r", CultureInfo.InvariantCulture));
				case TypeCode.Single:
					return FixNonFixedText(((Single)value).ToString("r", CultureInfo.InvariantCulture));
				case TypeCode.Byte:
				case TypeCode.SByte:
				case TypeCode.Int16:
				case TypeCode.UInt16:
				case TypeCode.Int32:
				case TypeCode.UInt32:
				case TypeCode.Int64:
					return Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture);
				case TypeCode.UInt64:
					return Convert.ToUInt64(value).ToString(CultureInfo.InvariantCulture);
				case TypeCode.Object:
				default:
					if(value is Guid) {
						return string.Concat("'", ((Guid)value).ToString(), "'");
					} else if(value is TimeSpan) {
						return FixNonFixedText(((TimeSpan)value).TotalSeconds.ToString("r", CultureInfo.InvariantCulture));
					} else {
						throw new ArgumentException(value.ToString());
					}
			}
		}
		string FixNonFixedText(string toFix) {
			if(toFix.IndexOfAny(new char[] { '.', 'e', 'E' }) < 0)
				toFix += ".0";
			return toFix;
		}
		void ClearDatabase(IDbCommand command) {
			SelectStatementResult constraints = SelectData(new Query("select o.name, t.name  from sysreferences f join sysobjects o on f.constrid = o.id join sysobjects t on f.tableid = t.id"));
			foreach(SelectStatementResultRow row in constraints.Rows) {
				command.CommandText = "alter table [" + (string)row.Values[1] + "] drop constraint [" + (string)row.Values[0] + "]";
				command.ExecuteNonQuery();
			}
			string[] tables = GetStorageTablesList(false);
			foreach(string table in tables) {
				command.CommandText = "drop table [" + table + "]";
				command.ExecuteNonQuery();
			}
		}
		protected override void ProcessClearDatabase() {
			using(IDbCommand command = CreateCommand()) {
				ClearDatabase(command);
			}
		}
		public override string[] GetStorageTablesList(bool includeViews) {
			SelectStatementResult tables = SelectData(new Query(string.Format("select name from sysobjects where type in ('U'{0})", includeViews ? ", 'V'" : string.Empty)));
			ArrayList result = new ArrayList(tables.Rows.Length);
			foreach(SelectStatementResultRow row in tables.Rows) {
				result.Add(row.Values[0]);
			}
			return (string[])result.ToArray(typeof(string));
		}
		ExecMethodDelegate commandBuilderDeriveParametersHandler;
		protected override void CommandBuilderDeriveParameters(IDbCommand command) {
			if(commandBuilderDeriveParametersHandler == null) {
				commandBuilderDeriveParametersHandler = ReflectConnectionHelper.GetCommandBuilderDeriveParametersDelegate(Connection.GetType().Assembly.FullName, "Sybase.Data.AseClient.AseCommandBuilder");
			}
			commandBuilderDeriveParametersHandler(command);
		}
		public override DBStoredProcedure[] GetStoredProcedures() {
			List<DBStoredProcedure> result = new List<DBStoredProcedure>();
			using(var command = Connection.CreateCommand()) {
				command.CommandText = "select * from sysobjects where type = 'P'";
				using(var reader = command.ExecuteReader()) {
					while(reader.Read()) {
						DBStoredProcedure curSproc = new DBStoredProcedure();
						curSproc.Name = reader.GetString(0);
						result.Add(curSproc);
					}
				}
			}
			foreach(DBStoredProcedure curSproc in result) {
				List<string> fakeParams = new List<string>();
				using(var command = Connection.CreateCommand()) {
					command.CommandType = CommandType.StoredProcedure;
					command.CommandText = curSproc.Name;
					CommandBuilderDeriveParameters(command);
					List<DBStoredProcedureArgument> dbArguments = new List<DBStoredProcedureArgument>();
					foreach(IDataParameter parameter in command.Parameters) {
						DBStoredProcedureArgumentDirection direction = DBStoredProcedureArgumentDirection.In;
						if(parameter.Direction == ParameterDirection.InputOutput) {
							direction = DBStoredProcedureArgumentDirection.InOut;
						}
						if(parameter.Direction == ParameterDirection.Output) {
							direction = DBStoredProcedureArgumentDirection.Out;
						}
						DBColumnType columnType = GetColumnType(parameter.DbType, true);
						dbArguments.Add(new DBStoredProcedureArgument(parameter.ParameterName, columnType, direction));
						fakeParams.Add("null");
					}
					curSproc.Arguments.AddRange(dbArguments);
				}
				using(var command = Connection.CreateCommand()) {
					command.CommandType = CommandType.Text;
					command.CommandText = string.Format(
@"set showplan on
set fmtonly on
exec [{0}] {1}
set fmtonly off
set showplan off", ComposeSafeTableName(curSproc.Name), string.Join(", ", fakeParams.ToArray()));
					using(var reader = command.ExecuteReader()) {
						DBStoredProcedureResultSet curResultSet = new DBStoredProcedureResultSet();
						List<DBNameTypePair> dbColumns = new List<DBNameTypePair>();
						for(int i = 0; i < reader.FieldCount; i++) {
							dbColumns.Add(new DBNameTypePair(reader.GetName(i), DBColumn.GetColumnType(reader.GetFieldType(i))));
						}
						curResultSet.Columns.AddRange(dbColumns);
						curSproc.ResultSets.Add(curResultSet);
					}
				}
			}
			return result.ToArray();
		}
	}
	public class AseProviderFactory : ProviderFactory {
		public override IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return AseConnectionProvider.CreateProviderFromConnection(connection, autoCreateOption);
		}
		public override IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			return AseConnectionProvider.CreateProviderFromString(connectionString, autoCreateOption, out objectsToDisposeOnDisconnect);
		}
		public override string GetConnectionString(Dictionary<string, string> parameters) {
			if(!parameters.ContainsKey(ServerParamID) || !parameters.ContainsKey(DatabaseParamID) ||
				!parameters.ContainsKey(UserIDParamID) || !parameters.ContainsKey(PasswordParamID)) {
				return null;
			}
			string port;
			if(parameters.TryGetValue(PortParamID, out port)) {
				return AseConnectionProvider.GetConnectionString(parameters[ServerParamID], Convert.ToInt32(port, CultureInfo.InvariantCulture), parameters[DatabaseParamID],
					parameters[UserIDParamID], parameters[PasswordParamID]);
			}
			return AseConnectionProvider.GetConnectionString(parameters[ServerParamID], parameters[DatabaseParamID],
				parameters[UserIDParamID], parameters[PasswordParamID]);
		}
		public override IDataStore CreateProvider(Dictionary<string, string> parameters, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			string connectionString = GetConnectionString(parameters);
			if(connectionString == null) {
				objectsToDisposeOnDisconnect = Array.Empty<IDisposable>();
				return null;
			}
			ConnectionStringParser helper = new ConnectionStringParser(connectionString);
			helper.RemovePartByName(DataStoreBase.XpoProviderTypeParameterName);
			return CreateProviderFromString(helper.GetConnectionString(), autoCreateOption, out objectsToDisposeOnDisconnect);
		}
		public override bool HasPort { get { return true; } }
		public override bool HasUserName { get { return true; } }
		public override bool HasPassword { get { return true; } }
		public override bool HasIntegratedSecurity { get { return false; } }
		public override bool HasMultipleDatabases { get { return true; } }
		public override bool IsServerbased { get { return true; } }
		public override bool IsFilebased { get { return false; } }
		public override string ProviderKey { get { return AseConnectionProvider.XpoProviderTypeString; } }
		public override string[] GetDatabases(string server, string userId, string password) {
			return Array.Empty<string>();
		}
		public override string[] GetDatabases(string server, int port, string userId, string password) {
			return Array.Empty<string>();
		}
		public override string FileFilter { get { return null; } }
		public override bool MeanSchemaGeneration { get { return true; } }
		public override bool SupportStoredProcedures { get { return false; } }
	}
}
#pragma warning restore DX0024
namespace DevExpress.Xpo.DB.Helpers {
	using System;
	using System.Data;
	interface IDbTypeMapperAse {
		void SetParameterTypeUniText(IDbDataParameter parameter);
		void SetParameterTypeUniVarChar(IDbDataParameter parameter);
		void SetParameterTypeImage(IDbDataParameter parameter);
	}
	class DbTypeMapperAse<TSqlDbTypeEnum, TSqlParameter> : DbTypeMapper<TSqlDbTypeEnum, TSqlParameter>, IDbTypeMapperAse
		where TSqlDbTypeEnum : struct
		where TSqlParameter : IDbDataParameter {
		static readonly TSqlDbTypeEnum aseDbTypeUniText;
		static readonly TSqlDbTypeEnum aseDbTypeUniVarChar;
		static readonly TSqlDbTypeEnum aseDbTypeImage;
		static DbTypeMapperAse() {
			aseDbTypeUniText = (TSqlDbTypeEnum)Enum.Parse(typeof(TSqlDbTypeEnum), "Unitext");
			aseDbTypeUniVarChar = (TSqlDbTypeEnum)Enum.Parse(typeof(TSqlDbTypeEnum), "UniVarChar");
			aseDbTypeImage = (TSqlDbTypeEnum)Enum.Parse(typeof(TSqlDbTypeEnum), "Image");
		}
		protected override string ParameterDbTypePropertyName { get { return "AseDbType"; } }
		protected override string GetParameterTypeNameForBoolean(out int? size, out byte? precision, out byte? scale) {
			size = null;
			precision = scale = null;
			return "Bit";
		}
		protected override string GetParameterTypeNameForByte(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "TinyInt";
		}
		protected override string GetParameterTypeNameForByteArray(out int? size) {
			size = null;
			return "Image";
		}
		protected override string GetParameterTypeNameForChar(out int? size) {
			size = 1;
			return "UniChar";
		}
		protected override string GetParameterTypeNameForDateTime() {
			return "DateTime";
		}
		protected override string GetParameterTypeNameForDecimal(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Money";
		}
		protected override string GetParameterTypeNameForDouble(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Double";
		}
		protected override string GetParameterTypeNameForGuid(out int? size) {
			size = 36;
			return "Char";
		}
		protected override string GetParameterTypeNameForInt16(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "SmallInt";
		}
		protected override string GetParameterTypeNameForInt32(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Integer";
		}
		protected override string GetParameterTypeNameForInt64(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "BigInt";
		}
		protected override string GetParameterTypeNameForSByte(out byte? precision, out byte? scale) {
			precision = 3;
			scale = 0;
			return "Numeric";
		}
		protected override string GetParameterTypeNameForSingle(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Real";
		}
		protected override string GetParameterTypeNameForString(out int? size) {
			size = null;
			return "UniVarChar";
		}
		protected override string GetParameterTypeNameForTimeSpan() {
			return "Double";
		}
		protected override string GetParameterTypeNameForUInt16(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "UnsignedSmallInt";
		}
		protected override string GetParameterTypeNameForUInt32(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "UnsignedInt";
		}
		protected override string GetParameterTypeNameForUInt64(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "UnsignedBigInt";
		}
		protected override string GetParameterTypeNameForDateOnly(out int? size) {
			size = null;
			return "Date";
		}
		protected override string GetParameterTypeNameForTimeOnly(out int? size) {
			size = null;
			return "Time";
		}
		protected override string ConvertSqlTypeToParameterType(string sqlType) {
			sqlType = sqlType.ToUpperInvariant();
			switch(sqlType) {
				case "CHAR":
					return "Char";
				case "NCHAR":
					return "NChar";
				case "UNICHAR":
					return "UniChar";
				case "VARCHAR":
					return "VarChar";
				case "NVARCHAR":
					return "NVarChar";
				case "UNIVARCHAR":
					return "UniVarChar";
				case "TEXT":
					return "Text";
				case "UNITEXT":
					return "Unitext";
				case "BIGINT":
					return "BigInt";
				case "UNSIGNED BIGINT":
					return "UnsignedBigInt";
				case "INT":
				case "INTEGER":
					return "Integer";
				case "UNSIGNED INT":
				case "UNSIGNED INTEGER":
					return "UnsignedInt";
				case "SMALLINT":
					return "SmallInt";
				case "UNSIGNED SMALLINT":
					return "UnsignedSmallInt";
				case "TINYINT":
					return "TinyInt";
				case "NUMERIC":
					return "Numeric";
				case "DECIMAL":
					return "Decimal";
				case "FLOAT":
					return "Float";
				case "REAL":
					return "Real";
				case "DOUBLE PRECISION":
					return "Double";
				case "DATE":
					return "Date";
				case "TIME":
					return "Time";
				case "DATETIME":
					return "BigDateTime";
				case "SMALLDATETIME":
					return "SmallDateTime";
				case "BINARY":
					return "Binary";
				case "VARBINARY":
					return "VarBinary";
				case "IMAGE":
					return "Image";
				case "BIT":
					return "Bit";
				case "MONEY":
					return "Money";
				case "SMALLMONEY":
					return "SmallMoney";
				case "TIMESTAMP":
					return "TimeStamp";
				default:
					return null;
			}
		}
		public override void SetParameterTypeAndSize(IDbDataParameter parameter, DBColumnType dbColumnType, int size) {
			if(dbColumnType == DBColumnType.String) {
				if(size <= 0 || size > AseConnectionProvider.MaximumStringSize) {
					SetSqlDbTypeHandler((TSqlParameter)parameter, aseDbTypeUniText);
					return;
				}
			}
			base.SetParameterTypeAndSize(parameter, dbColumnType, size);
		}
		public void SetParameterTypeUniText(IDbDataParameter parameter) {
			SetSqlDbTypeHandler((TSqlParameter)parameter, aseDbTypeUniText);
		}
		public void SetParameterTypeUniVarChar(IDbDataParameter parameter) {
			SetSqlDbTypeHandler((TSqlParameter)parameter, aseDbTypeUniVarChar);
		}
		public void SetParameterTypeImage(IDbDataParameter parameter) {
			SetSqlDbTypeHandler((TSqlParameter)parameter, aseDbTypeImage);
		}
	}
}
