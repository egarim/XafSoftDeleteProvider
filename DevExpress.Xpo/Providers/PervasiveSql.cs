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

namespace DevExpress.Xpo.DB {
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.Data;
	using System.Globalization;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using DevExpress.Data.Filtering;
	using DevExpress.Data.Helpers;
	using DevExpress.Utils;
	using DevExpress.Xpo;
	using DevExpress.Xpo.DB.Exceptions;
	using DevExpress.Xpo.DB.Helpers;
#pragma warning disable DX0024
	public class PervasiveSqlConnectionProvider : ConnectionProviderSql, ISqlGeneratorFormatterEx {
		public const int PervasiveErrorCode = -4999;
		public const string XpoProviderTypeString = "Pervasive";
		const int PervasiveDataTypeKeyColumn = 227;
		const int PervasiveDataTypeIndexColumn = 255;
		ReflectConnectionHelper helper;
		ReflectConnectionHelper ConnectionHelper {
			get {
				if(helper == null)
					helper = new ReflectConnectionHelper(Connection, "Pervasive.Data.SqlClient.PsqlException");
				return helper;
			}
		}
		DbTypeMapperBase dbTypeMapper;
		protected override DbTypeMapperBase DbTypeMapper {
			get {
				if(dbTypeMapper == null) {
					Type psqlParamType = ConnectionHelper.GetType("Pervasive.Data.SqlClient.PsqlParameter");
					Type psqlDbTypeType = ConnectionHelper.GetType("Pervasive.Data.SqlClient.PsqlDbType");
					dbTypeMapper = (DbTypeMapperBase)Activator.CreateInstance(typeof(DbTypeMapperPervasive<,>).MakeGenericType(psqlDbTypeType, psqlParamType));
				}
				return dbTypeMapper;
			}
		}
		public static string GetConnectionString(string server, string userId, string password, string database) {
			return String.Format("{4}={5};Server={0};UID={1};PWD={2};ServerDSN={3};",
				EscapeConnectionStringArgument(server), EscapeConnectionStringArgument(userId), EscapeConnectionStringArgument(password), EscapeConnectionStringArgument(database), DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString);
		}
		public static string GetConnectionString(string server, string userId, string password, string database, string encoding) {
			return String.Format("{4}={5};Server={0};UID={1};PWD={2};ServerDSN={3};encoding={6};",
				EscapeConnectionStringArgument(server), EscapeConnectionStringArgument(userId), EscapeConnectionStringArgument(password), EscapeConnectionStringArgument(database), DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString, EscapeConnectionStringArgument(encoding));
		}
		public static IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			IDbConnection connection = CreateConnection(connectionString);
			objectsToDisposeOnDisconnect = new IDisposable[] { connection };
			return CreateProviderFromConnection(connection, autoCreateOption);
		}
		public static IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return new PervasiveSqlConnectionProvider(connection, autoCreateOption);
		}
		static PervasiveSqlConnectionProvider() {
			RegisterDataStoreProvider(XpoProviderTypeString, new DataStoreCreationFromStringDelegate(CreateProviderFromString));
			RegisterDataStoreProvider("Pervasive.Data.SqlClient.PsqlConnection", new DataStoreCreationFromConnectionDelegate(CreateProviderFromConnection));
			RegisterFactory(new PervasiveProviderFactory());
		}
		public static void Register() { }
		public PervasiveSqlConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption)
			: base(connection, autoCreateOption, true) {
		}
		protected PervasiveSqlConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption, bool openConnection)
			: base(connection, autoCreateOption, openConnection) {
		}
		protected override string GetSqlCreateColumnTypeForBoolean(DBTable table, DBColumn column) {
			return "bit";
		}
		protected override string GetSqlCreateColumnTypeForByte(DBTable table, DBColumn column) {
			return "utinyint";
		}
		protected override string GetSqlCreateColumnTypeForSByte(DBTable table, DBColumn column) {
			return "tinyint";
		}
		protected override string GetSqlCreateColumnTypeForChar(DBTable table, DBColumn column) {
			return "varchar(4)";
		}
		protected override string GetSqlCreateColumnTypeForDecimal(DBTable table, DBColumn column) {
			return "decimal(20,4)";
		}
		protected override string GetSqlCreateColumnTypeForDouble(DBTable table, DBColumn column) {
			return "double";
		}
		protected override string GetSqlCreateColumnTypeForSingle(DBTable table, DBColumn column) {
			return "real";
		}
		protected override string GetSqlCreateColumnTypeForInt32(DBTable table, DBColumn column) {
			return "integer";
		}
		protected override string GetSqlCreateColumnTypeForUInt32(DBTable table, DBColumn column) {
			return "uinteger";
		}
		protected override string GetSqlCreateColumnTypeForInt16(DBTable table, DBColumn column) {
			return "smallint";
		}
		protected override string GetSqlCreateColumnTypeForUInt16(DBTable table, DBColumn column) {
			return "usmallint";
		}
		protected override string GetSqlCreateColumnTypeForInt64(DBTable table, DBColumn column) {
			return "bigint";
		}
		protected override string GetSqlCreateColumnTypeForUInt64(DBTable table, DBColumn column) {
			return "ubigint";
		}
		public const int MaximumStringSize = 4000;
		protected override string GetSqlCreateColumnTypeForString(DBTable table, DBColumn column) {
			if(column.Size > 0 && column.Size <= MaximumStringSize)
				return "varchar(" + column.Size.ToString(CultureInfo.InvariantCulture) + ')';
			else
				return "longvarchar";
		}
		protected override string GetSqlCreateColumnTypeForDateTime(DBTable table, DBColumn column) {
			return "timestamp";
		}
		protected override string GetSqlCreateColumnTypeForGuid(DBTable table, DBColumn column) {
			return "char(36)";
		}
		protected override string GetSqlCreateColumnTypeForByteArray(DBTable table, DBColumn column) {
			return "longvarbinary";
		}
		public override string GetSqlCreateColumnFullAttributes(DBTable table, DBColumn column) {
			return null;
		}
		public override string GetSqlCreateColumnFullAttributes(DBTable table, DBColumn column, bool forTableCreate) {
			string result = GetSqlCreateColumnFullAttributes(table, column);
			if(!string.IsNullOrEmpty(result)) {
				return result;
			}
			if(column.IsKey && column.IsIdentity && (column.ColumnType == DBColumnType.Int32 || column.ColumnType == DBColumnType.Int64) && IsSingleColumnPKColumn(table, column)) {
				if(column.ColumnType == DBColumnType.Int64) throw new NotSupportedException(Res.GetString(Res.ConnectionProvider_TheAutoIncrementedKeyWithX0TypeIsNotSupport, column.ColumnType, this.GetType()));
				return "identity not null";
			}
			result = GetSqlCreateColumnType(table, column);
			if(!column.IsIdentity && !column.IsKey) {
				if(!string.IsNullOrEmpty(column.DbDefaultValue)) {
					result += string.Concat(" DEFAULT ", column.DbDefaultValue);
				}
				else {
					if(column.DefaultValue != null && column.DefaultValue != System.DBNull.Value) {
						try {
							string formattedDefaultValue = FormatConstant(column.DefaultValue);
							result += string.Concat(" DEFAULT ", formattedDefaultValue);
						}
						catch(ArgumentException ex) {
							throw new ArgumentException(Res.GetString(Res.SqlConnectionProvider_CannotCreateAColumnForTheX0FieldWithTheX1D, column.Name, ex.Data["Value"]), ex);
						}
					}
				}
			}
			if(column.IsKey || !column.IsNullable) {
				result += " NOT NULL";
			}
			else {
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
			}
			return base.ConvertToDbParameter(clientValue, clientValueTypeCode);
		}
		protected override Int64 GetIdentity(Query sql) {
			ExecSql(sql);
			object value = GetScalar(new Query("select @@Identity"));
			return (value as IConvertible).ToInt64(CultureInfo.InvariantCulture);
		}
		protected override async Task<Int64> GetIdentityAsync(Query sql, AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken) {
			await ExecSqlAsync(sql, asyncOperationId, cancellationToken).ConfigureAwait(false);
			object value = await GetScalarAsync(new Query("select @@Identity"), asyncOperationId, cancellationToken).ConfigureAwait(false);
			return (value as IConvertible).ToInt64(CultureInfo.InvariantCulture);
		}
		protected override IDbConnection CreateConnection() {
			return ConnectionHelper.GetConnection(ConnectionString);
		}
		public static IDbConnection CreateConnection(string connectionString) {
			return ReflectConnectionHelper.GetConnection("Pervasive.Data.SqlClient", "Pervasive.Data.SqlClient.PsqlConnection", connectionString);
		}
		protected override void CreateDataBase() {
			try {
				Connection.Open();
			}
			catch(Exception e) {
				throw new UnableToOpenDatabaseException(XpoDefault.ConnectionStringRemovePassword(ConnectionString), e);
			}
		}
		ExecMethodDelegate commandBuilderDeriveParametersHandler;
		protected override void CommandBuilderDeriveParameters(IDbCommand command) {
			if(commandBuilderDeriveParametersHandler == null) {
				commandBuilderDeriveParametersHandler = ReflectConnectionHelper.GetCommandBuilderDeriveParametersDelegate("Pervasive.Data.SqlClient", "Pervasive.Data.SqlClient.PsqlCommandBuilder");
			}
			commandBuilderDeriveParametersHandler(command);
		}
		protected override SelectedData ExecuteSproc(string sprocName, params OperandValue[] parameters) {
			using(IDbCommand command = CreateCommand()) {
				PrepareCommandForSprocCall(command, sprocName, parameters);
				List<SelectStatementResult> selectStatementResults = GetSelectedStatementResults(command);
				return new SelectedData(selectStatementResults.ToArray());
			}
		}
		protected override async Task<SelectedData> ExecuteSprocAsync(AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken, string sprocName, params OperandValue[] parameters) {
			using(IDbCommand command = CreateCommand()) {
				PrepareCommandForSprocCall(command, sprocName, parameters);
				List<SelectStatementResult> selectStatementResults = await GetSelectedStatementResultsAsync(command, asyncOperationId, cancellationToken).ConfigureAwait(false);
				return new SelectedData(selectStatementResults.ToArray());
			}
		}
		void PrepareCommandForSprocCall(IDbCommand command, string sprocName, OperandValue[] parameters) {
			List<string> paramsList = new List<string>();
			int counter = 0;
			foreach(OperandValue p in parameters) {
				bool createParam = true;
				string paramName = GetParameterName(p, counter++, ref createParam);
				paramsList.Add(paramName);
				if(createParam) {
					ParameterValue param = p as ParameterValue;
					if(!ReferenceEquals(param, null)) {
						command.Parameters.Add(CreateParameter(command, param.Value, paramName, param.DBType, param.DBTypeName, param.Size));
					}
					else {
						command.Parameters.Add(CreateParameter(command, p.Value, paramName, DBColumnType.Unknown, null, 0));
					}
				}
			}
			command.CommandText = string.Format("call {0}({1})", sprocName, string.Join(", ", paramsList.ToArray()));
		}
		public override DBStoredProcedure[] GetStoredProcedures() {
			throw new NotSupportedException();
		}
		delegate bool TablesFilter(DBTable table);
		SelectStatementResult GetDataForTables(ICollection tables, TablesFilter filter, string queryText) {
			QueryParameterCollection parameters = new QueryParameterCollection();
			StringCollection inList = new StringCollection();
			int i = 0;
			foreach(DBTable table in tables) {
				if(filter == null || filter(table)) {
					parameters.Add(new OperandValue(ComposeSafeTableName(table.Name)));
					inList.Add("?");
					++i;
				}
			}
			if(inList.Count == 0)
				return new SelectStatementResult();
			return SelectData(new Query(string.Format(CultureInfo.InvariantCulture, queryText, StringListHelper.DelimitedText(inList, ",")), parameters, inList));
		}
		[Flags]
		enum ColumnTypeFlags : UInt16 {
			CaseSensitive = 0x0001,
			Nullable = 0x0004,
			Signed = 0x0008,
			TrueNullable = 0x0040,
			NText = 0x0800,
			Binary = 0x1000,
			DecimalEvenPrecision = 0x2000,
		}
		string GetDBColumnTypeNameByDBInfo(byte dataType, UInt16 size, byte dec, ColumnTypeFlags flags) {
			int? columnSize = null;
			byte? columnDec = null;
			string typeName = string.Empty;
			switch(dataType) {
				case 0:
					columnSize = size;
					if((flags & ColumnTypeFlags.Binary) == ColumnTypeFlags.Binary) {
						typeName = "BINARY";
						break;
					}
					typeName = "CHAR";
					break;
				case 1:
					switch(size) {
						case 1:
							typeName = "TINYINT";
							break;
						case 2:
							typeName = "SMALLINT";
							break;
						case 4:
							typeName = "INTEGER";
							break;
						case 8:
							typeName = "BIGINT";
							break;
					}
					break;
				case 2:
					switch(size) {
						case 4:
							typeName = "REAL";
							break;
						case 8:
							typeName = "DOUBLE";
							break;
					}
					break;
				case 3:
					typeName = "DATE";
					break;
				case 4:
					typeName = "TIME";
					break;
				case 5:
					columnSize = ((flags & ColumnTypeFlags.DecimalEvenPrecision) == ColumnTypeFlags.DecimalEvenPrecision) ? (size - 1) * 2 : (size * 2) - 1;
					columnDec = dec;
					typeName = "DECIMAL";
					break;
				case 8:
					columnSize = size;
					columnDec = dec;
					typeName = "NUMERIC";
					break;
				case 11:
					columnSize = size - 1;
					typeName = "VARCHAR";
					break;
				case 12:
					columnSize = -1;
					typeName = "NOTE";
					break;
				case 14:
					switch(size) {
						case 1:
							typeName = "UTINYINT";
							break;
						case 2:
							typeName = "USMALLINT";
							break;
						case 4:
							typeName = "UINTEGER";
							break;
						case 8:
							typeName = "UBIGINT";
							break;
					}
					break;
				case 15:
					switch(size) {
						case 8:
							typeName = "BIGIDENTITY";
							break;
						case 4:
							typeName = "IDENTITY";
							break;
						case 2:
							typeName = "SMALLIDENTITY";
							break;
					}
					break;
				case 7:
				case 16:
					typeName = "BIT";
					break;
				case 19:
					typeName = "CURRENCY";
					break;
				case 20:
					typeName = "TIMESTAMP";
					break;
				case 21:
					if((flags & ColumnTypeFlags.Binary) == ColumnTypeFlags.Binary) {
						typeName = "LONGVARBINARY";
						break;
					}
					if((flags & ColumnTypeFlags.NText) == ColumnTypeFlags.NText) {
						typeName = "NVARCHAR";
						break;
					}
					typeName = "LONGVARCHAR";
					break;
				case 27:
					typeName = "DECIMAL";
					break;
				case 30:
					typeName = "DATETIME";
					break;
			}
			if(columnSize == null) {
				return typeName;
			}
			if(columnDec == null) {
				return string.Concat(typeName, "(", columnSize.Value.ToString(CultureInfo.InvariantCulture), ")");
			}
			return string.Format(CultureInfo.InvariantCulture, "{0}({1},{2})", typeName, columnSize, dec);
		}
		DBColumnType GetDBColumnTypeByDBInfo(byte dataType, UInt16 size, byte dec, ColumnTypeFlags flags, out int columnSize, out bool isIdentity) {
			columnSize = 0;
			isIdentity = false;
			switch(dataType) {
				case 0: 
					if((flags & ColumnTypeFlags.Binary) == ColumnTypeFlags.Binary) { 
						return DBColumnType.ByteArray;
					}
					switch(size) {
						case 1:
							return DBColumnType.Char;
						case 36:
							return DBColumnType.Guid;
						default:
							columnSize = size;
							return DBColumnType.String;
					}
				case 1: 
					switch(size) {
						case 1:
							if((flags & ColumnTypeFlags.Signed) == ColumnTypeFlags.Signed) {
								return DBColumnType.SByte;
							}
							return DBColumnType.Byte;
						case 2:
							return DBColumnType.Int16;
						case 4:
							return DBColumnType.Int32;
						case 8:
							return DBColumnType.Int64;
					}
					goto default;
				case 2:
					switch(size) {
						case 4:
							return DBColumnType.Single;
						case 8:
							return DBColumnType.Double;
					}
					goto default;
				case 3:
				case 4:
				case 30:
					return DBColumnType.DateTime;
				case 5: 
					return DBColumnType.Decimal;
				case 8: 
					return dec > 0 ? DBColumnType.Decimal : DBColumnType.Int64;
				case 11: 
					columnSize = size - 1;
					return DBColumnType.String;
				case 12: 
					columnSize = -1;
					return DBColumnType.String;
				case 14: 
					switch(size) {
						case 1:
							return DBColumnType.Byte;
						case 2:
							return DBColumnType.UInt16;
						case 4:
							return DBColumnType.UInt32;
						case 8:
							return DBColumnType.UInt64;
					}
					goto default;
				case 15: 
					switch(size) {
						case 8:
							isIdentity = true;
							return DBColumnType.Int64;
						case 4:
							isIdentity = true;
							return DBColumnType.Int32;
						case 2:
							isIdentity = true;
							return DBColumnType.Int16;
					}
					goto default;
				case 7:
				case 16: 
					return DBColumnType.Boolean;
				case 19: 
					return DBColumnType.Decimal;
				case 20: 
					return DBColumnType.DateTime;
				case 21: 
					if((flags & ColumnTypeFlags.Binary) == ColumnTypeFlags.Binary) { 
						return DBColumnType.ByteArray;
					}
					return DBColumnType.String;
				case 25:
				case 26:
					return DBColumnType.String;
				default:
					return DBColumnType.Unknown;
			}
		}
		void GetColumns(DBTable table) {
			foreach(SelectStatementResultRow row in SelectData(new Query("select c.Xe$Name, c.Xe$DataType, c.Xe$Size, c.Xe$Dec, c.Xe$Flags, a.Xa$Attrs " +
				"from X$Field c join X$File t on c.Xe$File = t.Xf$Id " +
				"left join X$Attrib a on c.Xe$Id = a.Xa$Id and a.Xa$Type='D' " +
				"where t.Xf$Name = ? order by c.Xe$Id", new QueryParameterCollection(new OperandValue(ComposeSafeTableName(table.Name))), new string[] { "@p1" })).Rows) {
				byte dataType = Convert.ToByte(row.Values[1]);
				if(dataType == PervasiveDataTypeKeyColumn || dataType == PervasiveDataTypeIndexColumn) {
					continue;
				}
				int columnSize;
				bool isIdentity;
				byte dec = row.Values[3] is byte ? (byte)row.Values[3] : (byte)0;
				ColumnTypeFlags flags = (ColumnTypeFlags)Convert.ToUInt16(row.Values[4]);
				ushort size = Convert.ToUInt16(row.Values[2]);
				DBColumnType columnType = GetDBColumnTypeByDBInfo(dataType, size, dec, flags, out columnSize, out isIdentity);
				bool isNullable = ((Convert.ToInt32(row.Values[4]) & 0x04) != 0);
				string dbDefaultValue = (row.Values[5] as string);
				object defaultValue = null;
				if(dbDefaultValue != null) {
					decimal decimalValue;
					if((columnType == DBColumnType.Decimal || columnType == DBColumnType.Single || columnType == DBColumnType.Double)
						&& TryParseDecimal(dbDefaultValue, out decimalValue)) {
						defaultValue = decimalValue;
						dbDefaultValue = null;
					}
					else {
						if(!TryEvaluateSqlExpression(dbDefaultValue, out defaultValue)) {
							string quotedDbDfaultValue = string.Concat("'", dbDefaultValue, "'");
							if(TryEvaluateSqlExpression(quotedDbDfaultValue, out defaultValue)) {
								dbDefaultValue = null;
							}
						}
					}
				}
				if(defaultValue != null && dbDefaultValue != null) {
					ReformatReadValueArgs refmtArgs = new ReformatReadValueArgs(DBColumn.GetType(columnType));
					refmtArgs.AttachValueReadFromDb(dbDefaultValue);
					try {
						defaultValue = ReformatReadValue(dbDefaultValue, refmtArgs);
					}
					catch {
						defaultValue = null;
					}
				}
				string typeName = GetDBColumnTypeNameByDBInfo(dataType, size, dec, flags);
				DBColumn dBColumn = new DBColumn(((string)row.Values[0]).TrimEnd(), false, typeName, columnSize, columnType, isNullable, defaultValue);
				dBColumn.IsIdentity = isIdentity;
				dBColumn.DbDefaultValue = dbDefaultValue;
				table.AddColumn(dBColumn);
			}
		}
		bool TryEvaluateSqlExpression(string sqlExpr, out object result) {
			try {
				string query = string.Concat("select (", sqlExpr, ")");
				result = FixDBNullScalar(GetScalar(new Query(query)));
				return true;
			}
			catch {
				result = null;
				return false;
			}
		}
		bool TryParseDecimal(string expr, out decimal result) {
			expr = expr.Replace(',', '.');
			return decimal.TryParse(expr, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
		}
		void GetPrimaryKey(DBTable table) {
			SelectStatementResult data = SelectData(new Query(@"select c.Xe$Name from X$Index i join X$File t on i.Xi$File = t.Xf$Id 
join X$Field c on i.Xi$File = c.Xe$File and i.Xi$Field = c.Xe$Id 
where i.Xi$Flags & 16384 <> 0 and t.Xf$Name = ?
order by i.Xi$Part", new QueryParameterCollection(new OperandValue(ComposeSafeTableName(table.Name))), new string[] { "@p1" }));
			if(data.Rows.Length > 0) {
				StringCollection cols = new StringCollection();
				foreach(SelectStatementResultRow row in data.Rows) {
					string columnName = ((string)row.Values[0]).TrimEnd();
					DBColumn column = table.GetColumn(columnName);
					if(column != null)
						column.IsKey = true;
					cols.Add(columnName);
				}
				table.PrimaryKey = new DBPrimaryKey(cols);
			}
		}
		public override void CreateIndex(DBTable table, DBIndex index) {
			if(table.Name != "XPObjectType")
				base.CreateIndex(table, index);
		}
		void GetIndexes(DBTable table) {
			SelectStatementResult data = SelectData(new Query(
				@"select i.Xi$Number, c.Xe$Name, i.Xi$Flags & 1 from X$Index i
join X$File t on i.Xi$File = t.Xf$Id
join X$Field c on i.Xi$File = c.Xe$File and i.Xi$Field = c.Xe$Id
where t.Xf$Name = ? and i.Xi$Flags & 16384 = 0
order by i.Xi$Number, i.Xi$Part", new QueryParameterCollection(new OperandValue(ComposeSafeTableName(table.Name))), new string[] { "@p1" }));
			Hashtable idxs = new Hashtable();
			foreach(SelectStatementResultRow row in data.Rows) {
				DBIndex index = (DBIndex)idxs[row.Values[0]];
				if(index == null) {
					StringCollection list = new StringCollection();
					list.Add(((string)row.Values[1]).TrimEnd());
					index = new DBIndex(String.Empty, list, (Convert.ToInt32(row.Values[2]) & 1) == 0 ? true : false);
					table.Indexes.Add(index);
					idxs[row.Values[0]] = index;
				}
				else
					index.Columns.Add(((string)row.Values[1]).TrimEnd());
			}
		}
		void GetForeignKeys(DBTable table) {
			SelectStatementResult data = SelectData(new Query(
				@"select r.Xr$Name, fc.Xe$Name, pc.Xe$Name, p.Xf$Name from X$Relate r
join X$File f on f.Xf$Id = r.Xr$FId
join X$File p on p.Xf$Id = r.Xr$PId
join X$Index fi on fi.Xi$Number = r.Xr$FIndex and fi.Xi$File = r.Xr$FId
join X$Index pi1 on pi1.Xi$Number = r.Xr$Index and pi1.Xi$File = r.Xr$PId and pi1.Xi$Part = fi.Xi$Part 
join X$Field fc on fc.Xe$Id = fi.Xi$Field
join X$Field pc on pc.Xe$Id = pi1.Xi$Field
where f.Xf$Name = ?
order by r.Xr$Name, fi.Xi$Part", new QueryParameterCollection(new OperandValue(ComposeSafeTableName(table.Name))), new string[] { "@p1" }));
			Hashtable fks = new Hashtable();
			foreach(SelectStatementResultRow row in data.Rows) {
				DBForeignKey fk = (DBForeignKey)fks[row.Values[0]];
				if(fk == null) {
					StringCollection pkc = new StringCollection();
					StringCollection fkc = new StringCollection();
					pkc.Add(((string)row.Values[2]).TrimEnd());
					fkc.Add(((string)row.Values[1]).TrimEnd());
					fk = new DBForeignKey(fkc, ((string)row.Values[3]).TrimEnd(), pkc);
					table.ForeignKeys.Add(fk);
					fks[row.Values[0]] = fk;
				}
				else {
					fk.Columns.Add(((string)row.Values[1]).TrimEnd());
					fk.PrimaryKeyTableKeyColumns.Add(((string)row.Values[2]).TrimEnd());
				}
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
			foreach(SelectStatementResultRow row in GetDataForTables(tables, null, "select Xf$Name from X$File where Xf$Name in ({0})").Rows)
				dbTables.Add(((string)row.Values[0]).TrimEnd(), false);
			foreach(SelectStatementResultRow row in GetDataForTables(tables, null, "select Xv$Name from X$View where Xv$Name in ({0})").Rows)
				dbTables.Add(((string)row.Values[0]).TrimEnd(), true);
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
			return 20;
		}
		protected override int GetObjectNameEffectiveLength(string objectName) {
			return Encoding.UTF8.GetByteCount(objectName);
		}
		public override string FormatTable(string schema, string tableName) {
			return string.Format(CultureInfo.InvariantCulture, "\"{0}\"", tableName);
		}
		public override string FormatTable(string schema, string tableName, string tableAlias) {
			return string.Format(CultureInfo.InvariantCulture, "\"{0}\" {1}", tableName, tableAlias);
		}
		public override string FormatColumn(string columnName) {
			return string.Format(CultureInfo.InvariantCulture, "\"{0}\"", columnName);
		}
		public override string FormatColumn(string columnName, string tableAlias) {
			return string.Format(CultureInfo.InvariantCulture, "{1}.\"{0}\"", columnName, tableAlias);
		}
		public override string FormatSelect(string selectedPropertiesSql, string fromSql, string whereSql, string orderBySql, string groupBySql, string havingSql, int topSelectedRecords) {
			string modificatorsSql = string.Format(CultureInfo.InvariantCulture, (topSelectedRecords != 0) ? "top {0} " : string.Empty, topSelectedRecords);
			string expandedWhereSql = whereSql != null ? string.Format(CultureInfo.InvariantCulture, "{0}where {1}", Environment.NewLine, whereSql) : string.Empty;
			string expandedOrderBySql = orderBySql != null ? string.Format(CultureInfo.InvariantCulture, "{0}order by {1}", Environment.NewLine, orderBySql) : string.Empty;
			string expandedHavingSql = havingSql != null ? string.Format(CultureInfo.InvariantCulture, "{0}having {1}", Environment.NewLine, havingSql) : string.Empty;
			string expandedGroupBySql = groupBySql != null ? string.Format(CultureInfo.InvariantCulture, "{0}group by {1}", Environment.NewLine, groupBySql) : string.Empty;
			return string.Format(CultureInfo.InvariantCulture, "select {0}{1} from {2}{3}{4}{5}{6}", modificatorsSql, selectedPropertiesSql, fromSql, expandedWhereSql, expandedGroupBySql, expandedHavingSql, expandedOrderBySql);
		}
		public override string FormatInsertDefaultValues(string tableName) {
			return string.Format(CultureInfo.InvariantCulture, "insert into {0} values(default)", tableName);
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
					return string.Format(CultureInfo.InvariantCulture, "MOD({0}, {1})", leftOperand, rightOperand);
				default:
					return base.FormatBinary(operatorType, leftOperand, rightOperand);
			}
		}
		public override string FormatFunction(ProcessParameter processParameter, FunctionOperatorType operatorType, params object[] operands) {
			switch(operatorType) {
				case FunctionOperatorType.Atn2:
					return string.Format(new ProcessParameterInvariantCulture(processParameter), "(case when ({1}) = 0 then Sign({0}) * Atan(1) * 2 else Atan2({0},  {1}) end)", operands[0], operands[1]);
				case FunctionOperatorType.Cosh:
					return string.Format(new ProcessParameterInvariantCulture(processParameter), "((Exp({0}) + Exp(-({0}))) / 2.0)", operands[0]);
				case FunctionOperatorType.Sinh:
					return string.Format(new ProcessParameterInvariantCulture(processParameter), "((Exp({0}) - Exp(-({0}))) / 2.0)", operands[0]);
				case FunctionOperatorType.Tanh:
					return string.Format(new ProcessParameterInvariantCulture(processParameter), "((Exp({0}) - Exp(-({0}))) / (Exp({0}) + Exp(-({0}))))", operands[0]);
				case FunctionOperatorType.Substring:
					switch(operands.Length) {
						case 2:
							return string.Format(new ProcessParameterInvariantCulture(processParameter), "Substring({0}, ({1}) + 1, Length({0}) - ({1}))", operands[0], operands[1]);
						case 3:
							return string.Format(new ProcessParameterInvariantCulture(processParameter), "Substring({0}, ({1}) + 1, {2})", operands[0], operands[1], operands[2]);
					}
					goto default;
				case FunctionOperatorType.CharIndex:
					switch(operands.Length) {
						case 2:
							return string.Format(new ProcessParameterInvariantCulture(processParameter), "(LOCATE({0}, {1}) - 1)", operands[0], operands[1]);
						case 3:
							return string.Format(new ProcessParameterInvariantCulture(processParameter), "(LOCATE({0}, {1}, ({2}) + 1) - 1)", operands[0], operands[1], operands[2]);
						case 4:
							return string.Format(new ProcessParameterInvariantCulture(processParameter), "(LOCATE({0}, SUBSTRING({1}, 1, ({2}) + ({3})), ({2}) + 1) - 1)", operands[0], operands[1], operands[2], operands[3]);
					}
					goto default;
				case FunctionOperatorType.PadLeft:
					switch(operands.Length) {
						case 2:
							return string.Format(new ProcessParameterInvariantCulture(processParameter), "isnull(Concat(SPACE((({1}) - Length({0}))), ({0})), {0})", operands[0], operands[1]);
						case 3:
							return string.Format(new ProcessParameterInvariantCulture(processParameter), "isnull(Concat(REPLICATE({2}, (({1}) - Length({0}))), ({0})), {0})", operands[0], operands[1], operands[2]);
					}
					goto default;
				case FunctionOperatorType.PadRight:
					switch(operands.Length) {
						case 2:
							return string.Format(new ProcessParameterInvariantCulture(processParameter), "isnull(Concat(({0}), SPACE((({1}) - Length({0})))), {0})", operands[0], operands[1]);
						case 3:
							return string.Format(new ProcessParameterInvariantCulture(processParameter), "isnull(Concat(({0}), REPLICATE({2}, (({1}) - Length({0})))), {0})", operands[0], operands[1], operands[2]);
					}
					goto default;
				case FunctionOperatorType.AddTicks:
					return string.Format(new ProcessParameterInvariantCulture(processParameter), "TIMESTAMPADD(SQL_TSI_SECOND, MOD(CAST(CAST(({1}) as double) / 10000000 as bigint),86400), TIMESTAMPADD(SQL_TSI_DAY, CAST(CAST(({1}) as double) / 864000000000 as bigint), {0}))", operands[0], operands[1]);
				case FunctionOperatorType.AddMilliSeconds:
					return string.Format(new ProcessParameterInvariantCulture(processParameter), "TIMESTAMPADD(SQL_TSI_SECOND, CAST(({1}) / 1000 as bigint), {0})", operands[0], operands[1]);
				case FunctionOperatorType.AddTimeSpan:
				case FunctionOperatorType.AddSeconds:
					return string.Format(new ProcessParameterInvariantCulture(processParameter), "TIMESTAMPADD(SQL_TSI_SECOND, CAST(({1}) as bigint), {0})", operands[0], operands[1]);
				case FunctionOperatorType.AddMinutes:
					return string.Format(new ProcessParameterInvariantCulture(processParameter), "TIMESTAMPADD(SQL_TSI_SECOND, MOD(CAST(CAST(({1}) as double) * 60 as bigint),86400), TIMESTAMPADD(SQL_TSI_DAY, CAST((CAST(({1}) as double) * 60) / 86400 as bigint), {0}))", operands[0], operands[1]);
				case FunctionOperatorType.AddHours:
					return string.Format(new ProcessParameterInvariantCulture(processParameter), "TIMESTAMPADD(SQL_TSI_SECOND, MOD(CAST(CAST(({1}) as double) * 3600 as bigint),86400), TIMESTAMPADD(SQL_TSI_DAY, CAST((CAST(({1}) as double) * 3600) / 86400 as bigint), {0}))", operands[0], operands[1]);
				case FunctionOperatorType.AddDays:
					return string.Format(new ProcessParameterInvariantCulture(processParameter), "TIMESTAMPADD(SQL_TSI_SECOND, MOD(CAST(CAST(({1}) as double) * 86400 as bigint),86400), TIMESTAMPADD(SQL_TSI_DAY, CAST(CAST(({1}) as double) as bigint), {0}))", operands[0], operands[1]);
				case FunctionOperatorType.GetTimeOfDay:
					return string.Format(new ProcessParameterInvariantCulture(processParameter), "((CAST(HOUR({0}) as BIGINT) *  36000000000) + (CAST(MINUTE({0}) as BIGINT) * 600000000) + (CAST(SECOND({0}) as BIGINT) * 10000000))", operands[0]);
				case FunctionOperatorType.EndsWith:
					return string.Format(new ProcessParameterInvariantCulture(processParameter), "(SubstriNg({0}, LENGTH({0}) - LENGTH({1}) + 1, LENGTH({1})) = ({1}))", operands[0], operands[1]);
				case FunctionOperatorType.Contains:
					return string.Format(new ProcessParameterInvariantCulture(processParameter), "(LocaTE({1}, {0}) >= 1)", operands[0], operands[1]);
				case FunctionOperatorType.StartsWith:
					object secondOperand = operands[1];
					if(secondOperand is OperandValue && ((OperandValue)secondOperand).Value is string) {
						string operandString = (string)((OperandValue)secondOperand).Value;
						int likeIndex = operandString.IndexOfAny(achtungChars);
						if(likeIndex < 0) {
							return string.Format(new ProcessParameterInvariantCulture(processParameter), "({0} likE {1})", operands[0], new ConstantValue(operandString + "%"));
						}
						else if(likeIndex > 0) {
							return string.Format(new ProcessParameterInvariantCulture(processParameter), "(({0} likE {2}) And (LocatE({1}, {0}) = 1))", operands[0], secondOperand, new ConstantValue(operandString.Substring(0, likeIndex) + "%"));
						}
					}
					return string.Format(new ProcessParameterInvariantCulture(processParameter), "(LocatE({1}, {0}) = 1)", operands[0], operands[1]);
				default:
					return base.FormatFunction(processParameter, operatorType, operands);
			}
		}
		readonly static char[] achtungChars = new char[] { '_', '%' };
		public override string FormatFunction(FunctionOperatorType operatorType, params string[] operands) {
			switch(operatorType) {
				case FunctionOperatorType.Abs:
					return string.Format(CultureInfo.InvariantCulture, "Abs({0})", operands[0]);
				case FunctionOperatorType.Sign:
					return string.Format(CultureInfo.InvariantCulture, "Sign({0})", operands[0]);
				case FunctionOperatorType.BigMul:
					return string.Format(CultureInfo.InvariantCulture, "CAST((({0}) * ({1})) as BIGINT)", operands[0], operands[1]);
				case FunctionOperatorType.Cos:
					return string.Format(CultureInfo.InvariantCulture, "Cos({0})", operands[0]);
				case FunctionOperatorType.Sin:
					return string.Format(CultureInfo.InvariantCulture, "Sin({0})", operands[0]);
				case FunctionOperatorType.Tan:
					return string.Format(CultureInfo.InvariantCulture, "Tan({0})", operands[0]);
				case FunctionOperatorType.Atn:
					return string.Format(CultureInfo.InvariantCulture, "Atan({0})", operands[0]);
				case FunctionOperatorType.Acos:
					return string.Format(CultureInfo.InvariantCulture, "Acos({0})", operands[0]);
				case FunctionOperatorType.Asin:
					return string.Format(CultureInfo.InvariantCulture, "Asin({0})", operands[0]);
				case FunctionOperatorType.Log:
					switch(operands.Length) {
						case 1:
							return string.Format(CultureInfo.InvariantCulture, "Log({0})", operands[0]);
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "(Log({0}) / Log({1}))", operands[0], operands[1]);
					}
					goto default;
				case FunctionOperatorType.Log10:
					return string.Format(CultureInfo.InvariantCulture, "Log10({0})", operands[0]);
				case FunctionOperatorType.Exp:
					return string.Format(CultureInfo.InvariantCulture, "Exp({0})", operands[0]);
				case FunctionOperatorType.Power:
					return string.Format(CultureInfo.InvariantCulture, "Power({0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.Sqr:
					return string.Format(CultureInfo.InvariantCulture, "Sqrt({0})", operands[0]);
				case FunctionOperatorType.Max:
					return string.Format(CultureInfo.InvariantCulture, "if({0} > {1}, {0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.Min:
					return string.Format(CultureInfo.InvariantCulture, "if({0} < {1}, {0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.Floor:
					return string.Format(CultureInfo.InvariantCulture, "Floor({0})", operands[0]);
				case FunctionOperatorType.Ceiling:
					return string.Format(CultureInfo.InvariantCulture, "Ceiling({0})", operands[0]);
				case FunctionOperatorType.Round:
					switch(operands.Length) {
						case 1:
							return string.Format(CultureInfo.InvariantCulture, "Round({0}, 0)", operands[0]);
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "Round({0}, {1})", operands[0], operands[1]);
					}
					goto default;
				case FunctionOperatorType.Ascii:
					return string.Format(CultureInfo.InvariantCulture, "Ascii({0})", operands[0]);
				case FunctionOperatorType.Char:
					return string.Format(CultureInfo.InvariantCulture, "Char({0})", operands[0]);
				case FunctionOperatorType.ToInt:
					return string.Format(CultureInfo.InvariantCulture, "CAST({0} AS int)", operands[0]);
				case FunctionOperatorType.ToLong:
					return string.Format(CultureInfo.InvariantCulture, "CAST({0} AS bigint)", operands[0]);
				case FunctionOperatorType.ToFloat:
					return string.Format(CultureInfo.InvariantCulture, "CAST({0} AS real)", operands[0]);
				case FunctionOperatorType.ToDouble:
					return string.Format(CultureInfo.InvariantCulture, "CAST({0} AS double)", operands[0]);
				case FunctionOperatorType.ToDecimal:
					return string.Format(CultureInfo.InvariantCulture, "CAST({0} AS decimal(20,4))", operands[0]);
				case FunctionOperatorType.ToStr:
					return string.Format(CultureInfo.InvariantCulture, "CONVERT({0}, SQL_VARCHAR)", operands[0]);
				case FunctionOperatorType.Len:
					return string.Format(CultureInfo.InvariantCulture, "Length({0})", operands[0]);
				case FunctionOperatorType.Remove:
					switch(operands.Length) {
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "LEFT({0}, {1})", operands[0], operands[1]);
						case 3:
							return string.Format(CultureInfo.InvariantCulture, "Stuff({0}, ({1})+1, {2}, '')", operands[0], operands[1], operands[2]);
					}
					goto default;
				case FunctionOperatorType.Replace:
					return string.Format(CultureInfo.InvariantCulture, "(Replace({0}, {1}, {2}) + '')", operands[0], operands[1], operands[2]);
				case FunctionOperatorType.Insert:
					return string.Format(CultureInfo.InvariantCulture, "Stuff({0}, ({1})+1, 0, {2})", operands[0], operands[1], operands[2]);
				case FunctionOperatorType.GetMilliSecond:
					throw new NotSupportedException();
				case FunctionOperatorType.Concat:
					string args = String.Empty;
					foreach(string arg in operands) {
						args = (args.Length > 0) ? string.Concat("Concat(", args, ", ", arg, ")") : arg;
					}
					return args;
				case FunctionOperatorType.AddMonths:
					return string.Format(CultureInfo.InvariantCulture, "TIMESTAMPADD(SQL_TSI_MONTH, ({1}), ({0}))", operands[0], operands[1]);
				case FunctionOperatorType.AddYears:
					return string.Format(CultureInfo.InvariantCulture, "TIMESTAMPADD(SQL_TSI_YEAR, ({1}), ({0}))", operands[0], operands[1]);
				case FunctionOperatorType.GetSecond:
					return string.Format(CultureInfo.InvariantCulture, "Second({0})", operands[0]);
				case FunctionOperatorType.GetMinute:
					return string.Format(CultureInfo.InvariantCulture, "Minute({0})", operands[0]);
				case FunctionOperatorType.GetHour:
					return string.Format(CultureInfo.InvariantCulture, "Hour({0})", operands[0]);
				case FunctionOperatorType.GetDay:
					return string.Format(CultureInfo.InvariantCulture, "Day({0})", operands[0]);
				case FunctionOperatorType.GetMonth:
					return string.Format(CultureInfo.InvariantCulture, "Month({0})", operands[0]);
				case FunctionOperatorType.GetYear:
					return string.Format(CultureInfo.InvariantCulture, "Year({0})", operands[0]);
				case FunctionOperatorType.GetDayOfYear:
					return string.Format(CultureInfo.InvariantCulture, "DAYOFYEAR({0})", operands[0]);
				case FunctionOperatorType.GetDayOfWeek:
					return string.Format(CultureInfo.InvariantCulture, "MOD(DAYOFWEEK({0}) - DAYOFWEEK('1900-01-01') + 8, 7)", operands[0]);
				case FunctionOperatorType.GetDate:
					return string.Format(CultureInfo.InvariantCulture, "Cast({0} as Date)", operands[0]);
				case FunctionOperatorType.DateDiffYear:
					return string.Format(CultureInfo.InvariantCulture, "CAST(TIMESTAMPDIFF(SQL_TSI_YEAR, ({0}), ({1})) as INT)", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffMonth:
					return string.Format(CultureInfo.InvariantCulture, "CAST(TIMESTAMPDIFF(SQL_TSI_MONTH, ({0}), ({1})) as INT)", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffDay:
					return string.Format(CultureInfo.InvariantCulture, "CAST(TIMESTAMPDIFF(SQL_TSI_DAY, ({0}), ({1})) as INT)", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffHour:
					return string.Format(CultureInfo.InvariantCulture, "CAST(TIMESTAMPDIFF(SQL_TSI_HOUR, ({0}), ({1})) as INT)", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffMinute:
					return string.Format(CultureInfo.InvariantCulture, "CAST(TIMESTAMPDIFF(SQL_TSI_MINUTE, ({0}), ({1})) as INT)", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffSecond:
					return string.Format(CultureInfo.InvariantCulture, "CAST(TIMESTAMPDIFF(SQL_TSI_SECOND, ({0}), ({1})) as INT)", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffMilliSecond:
					return string.Format(CultureInfo.InvariantCulture, "(CAST(TIMESTAMPDIFF(SQL_TSI_SECOND, ({0}), ({1})) as INT) * 1000)", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffTick:
					return string.Format(CultureInfo.InvariantCulture, "(CAST(TIMESTAMPDIFF(SQL_TSI_SECOND, ({0}), ({1})) as INT) * 10000000)", operands[0], operands[1]);
				case FunctionOperatorType.Now:
					return "Now()";
				case FunctionOperatorType.UtcNow:
					return "CURRENT_TIMESTAMP()";
				case FunctionOperatorType.Today:
					return "cast(now() as date)";
				case FunctionOperatorType.Rnd:
					return "rand()";
				case FunctionOperatorType.IsNull:
					switch(operands.Length) {
						case 1:
							return string.Format(CultureInfo.InvariantCulture, "(({0}) is null)", operands[0]);
					}
					goto default;
				case FunctionOperatorType.IsNullOrEmpty:
					return string.Format(CultureInfo.InvariantCulture, "({0} is null or length({0}) = 0)", operands[0]);
				default:
					return base.FormatFunction(operatorType, operands);
			}
		}
		public override string GetParameterName(OperandValue parameter, int index, ref bool createParameter) {
			createParameter = false;
			if(parameter.Value == null || parameter.Value == DBNull.Value) {
				return FormatConstant(null);
			}
			object value = parameter.Value;
			TypeCode valueTypeCode = Type.GetTypeCode(value.GetType());
			switch(valueTypeCode) {
				case TypeCode.Byte:
					return ((byte)parameter.Value).ToString(CultureInfo.InvariantCulture);
				case TypeCode.UInt16:
					return ((UInt16)parameter.Value).ToString(CultureInfo.InvariantCulture);
				case TypeCode.UInt32:
					return ((UInt32)parameter.Value).ToString(CultureInfo.InvariantCulture);
				case TypeCode.UInt64:
					return ((UInt64)parameter.Value).ToString(CultureInfo.InvariantCulture);
				case TypeCode.Int16:
					return ((Int16)parameter.Value).ToString(CultureInfo.InvariantCulture);
				case TypeCode.Int32:
					return ((Int32)parameter.Value).ToString(CultureInfo.InvariantCulture);
				case TypeCode.Int64:
					return ((Int64)parameter.Value).ToString(CultureInfo.InvariantCulture);
				case TypeCode.SByte:
					return ((SByte)parameter.Value).ToString(CultureInfo.InvariantCulture);
				default:
					if(parameter is ConstantValue) {
						switch(valueTypeCode) {
							case TypeCode.Boolean:
								return (bool)value ? "1" : "0";
							case TypeCode.String:
								return FormatString(value);
						}
					}
					createParameter = true;
					return "?";
			}
		}
		protected string FormatString(object value) {
			return "'" + ((string)value).Replace("'", "''") + "'";
		}
		public override bool SupportNamedParameters { get { return false; } }
		public override void CreateForeignKey(DBTable table, DBForeignKey fk) {
			if(fk.PrimaryKeyTable == table.Name)
				return;
			base.CreateForeignKey(table, fk);
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
					}
					else {
						DbTypeMapper.SetParameterType(param, dbTypeName);
					}
				}
				else {
					if(parameterMode == QueryParameterMode.SetTypeAndSize) {
						DbTypeMapper.SetParameterTypeAndSize(param, dbType, size);
					}
					else {
						DbTypeMapper.SetParameterType(param, dbType);
					}
				}
			}
			if(value is byte[])
				param.DbType = DbType.Binary;
			if(value is string)
				param.DbType = DbType.String;
			if(parameterMode == QueryParameterMode.SetTypeAndSize) {
				ValidateParameterSize(command, param);
			}
			return param;
		}
		public override string FormatConstraint(string constraintName) {
			return string.Format(CultureInfo.InvariantCulture, "\"{0}\"", constraintName);
		}
		protected string FormatConstant(object value) {
			if(value == null)
				return "NULL";
			TypeCode tc = DXTypeExtensions.GetTypeCode(value.GetType());
			switch(tc) {
				case DXTypeExtensions.TypeCodeDBNull:
				case TypeCode.Empty:
					return "NULL";
				case TypeCode.Boolean:
					return ((bool)value) ? "1" : "0";
				case TypeCode.Char:
					if(value is char && Convert.ToInt32(value) == 0) {
						ArgumentException ex = new ArgumentException(null, nameof(value));
						ex.Data["Value"] = string.Concat("\\x", Convert.ToInt32(value).ToString("X2"));
						throw ex;
					}
					else {
						return "'" + (char)value + "'";
					}
				case TypeCode.DateTime:
					DateTime datetimeValue = (DateTime)value;
					return string.Concat("'", datetimeValue.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture), "'");
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
					if(value is Enum)
						return Convert.ToInt64(value).ToString();
					return value.ToString();
				case TypeCode.UInt64:
					if(value is Enum)
						return Convert.ToUInt64(value).ToString();
					return value.ToString();
				case TypeCode.Object:
				default:
					if(value is Guid) {
						return "'" + ((Guid)value).ToString() + "'";
					}
					else if(value is TimeSpan) {
						return FixNonFixedText(((TimeSpan)value).TotalSeconds.ToString("r", CultureInfo.InvariantCulture));
					}
					else {
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
			string[] tables = GetStorageTablesList(false);
			if(tables.Length == 0)
				return;
			SelectStatementResult constraints = SelectData(new Query("select r.Xr$Name, f.Xf$Name from X$Relate r join X$File f on f.Xf$Id = r.Xr$FId"));
			foreach(SelectStatementResultRow row in constraints.Rows) {
				command.CommandText = "alter table \"" + (string)row.Values[1] + "\" drop constraint \"" + (string)row.Values[0] + "\"";
				command.ExecuteNonQuery();
			}
			foreach(string table in tables) {
				command.CommandText = "drop table \"" + table + "\"";
				command.ExecuteNonQuery();
			}
		}
		protected override object ReformatReadValue(object value, ConnectionProviderSql.ReformatReadValueArgs args) {
			if(args.TargetTypeCode == TypeCode.Boolean && args.DbTypeCode == TypeCode.String) {
				return ((string)value == "1");
			}
			return base.ReformatReadValue(value, args);
		}
		protected override Exception WrapException(Exception e, IDbCommand query) {
			object[] properiesValues;
			if(ConnectionHelper.TryGetExceptionProperties(e, new string[] { "Message", "Errors" }, out properiesValues)) {
				string message = (string)properiesValues[0];
				ICollection errors = (ICollection)properiesValues[1];
				if(errors.Count > 0) {
					foreach(object error in errors) {
						int number = (int)ReflectConnectionHelper.GetPropertyValue(error, "Number");
						if(number == PervasiveErrorCode + 71 || number == PervasiveErrorCode + 5) {
							return new ConstraintViolationException(query.CommandText, GetParametersString(query), e);
						}
						break;
					}
				}
				if(message.Contains("[LNA][Pervasive][ODBC Engine Interface][Data Record Manager]There is a violation of the RI definitions") || message.Contains("(Btrieve Error 71)") ||
					message.Contains("[LNA][Pervasive][ODBC Engine Interface][Data Record Manager]The record has a key field containing a duplicate value") || message.Contains("(Btrieve Error 5)") ||
					message.Contains("[LNA][PSQL][SQL Engine]There is a violation of the RI definitions") ||
					message.Contains("[LNA][PSQL][SQL Engine]The record has a key field containing a duplicate value"))
					return new ConstraintViolationException(query.CommandText, GetParametersString(query), e);
				if(message.Contains("[LNA][Pervasive][ODBC Engine Interface]Invalid column name:") ||
					message.Contains("[LNA][Pervasive][ODBC Engine Interface][Data Record Manager]No such table or object.") ||
					message.Contains("[LNA][Pervasive][ODBC Engine Interface]Error in expression:") ||
					message.Contains("[LNA][PSQL][SQL Engine]Invalid column name:") ||
					message.Contains("[LNA][PSQL][SQL Engine][Data Record Manager]No such table or object.") ||
					message.Contains("[LNA][PSQL][SQL Engine]Error in expression:"))
					return new SchemaCorrectionNeededException(e);
			}
			return base.WrapException(e, query);
		}
		protected override void ProcessClearDatabase() {
			IDbCommand command = CreateCommand();
			ClearDatabase(command);
		}
		public override string[] GetStorageTablesList(bool includeViews) {
			SelectStatementResult tables = SelectData(new Query("select Xf$Name from X$File where Xf$Flags & 16 = 0"));
			SelectStatementResult views = includeViews ? SelectData(new Query("select Xv$Name from X$View")) : null;
			List<string> result = new List<string>(tables.Rows.Length + (views == null ? 0 : views.Rows.Length));
			foreach(SelectStatementResultRow row in tables.Rows) {
				result.Add(((string)row.Values[0]).TrimEnd(' '));
			}
			if(views != null) {
				foreach(SelectStatementResultRow row in views.Rows) {
					result.Add(((string)row.Values[0]).TrimEnd(' '));
				}
			}
			return result.ToArray();
		}
		bool inSchemaUpdate;
		protected override void BeginTransactionCore(object il) {
			if(!inSchemaUpdate)
				base.BeginTransactionCore(il);
		}
		protected override void CommitTransactionCore() {
			if(!inSchemaUpdate)
				base.CommitTransactionCore();
		}
		protected override void RollbackTransactionCore() {
			if(!inSchemaUpdate)
				base.RollbackTransactionCore();
		}
		protected override UpdateSchemaResult ProcessUpdateSchema(bool skipIfFirstTableNotExists, params DBTable[] tables) {
			inSchemaUpdate = true;
			try {
				return base.ProcessUpdateSchema(skipIfFirstTableNotExists, tables);
			}
			finally {
				inSchemaUpdate = false;
			}
		}
		protected override async Task<UpdateSchemaResult> ProcessUpdateSchemaAsync(AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken, bool skipIfFirstTableNotExists, DBTable[] tables) {
			inSchemaUpdate = true;
			try {
				return await base.ProcessUpdateSchemaAsync(asyncOperationId, cancellationToken, skipIfFirstTableNotExists, tables);
			}
			finally {
				inSchemaUpdate = false;
			}
		}
	}
#pragma warning restore DX0024
	public class PervasiveProviderFactory : ProviderFactory {
		public override IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return PervasiveSqlConnectionProvider.CreateProviderFromConnection(connection, autoCreateOption);
		}
		public override IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			return PervasiveSqlConnectionProvider.CreateProviderFromString(connectionString, autoCreateOption, out objectsToDisposeOnDisconnect);
		}
		public override string GetConnectionString(Dictionary<string, string> parameters) {
			if(!parameters.ContainsKey(ServerParamID) || !parameters.ContainsKey(DatabaseParamID) ||
				!parameters.ContainsKey(UserIDParamID) || !parameters.ContainsKey(PasswordParamID)) {
				return null;
			}
			return PervasiveSqlConnectionProvider.GetConnectionString(parameters[ServerParamID], parameters[UserIDParamID],
				parameters[PasswordParamID], parameters[DatabaseParamID]);
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
		public override bool HasUserName { get { return true; } }
		public override bool HasPassword { get { return true; } }
		public override bool HasIntegratedSecurity { get { return false; } }
		public override bool HasMultipleDatabases { get { return true; } }
		public override bool IsServerbased { get { return true; } }
		public override bool IsFilebased { get { return false; } }
		public override string ProviderKey { get { return PervasiveSqlConnectionProvider.XpoProviderTypeString; } }
		public override string[] GetDatabases(string server, string userId, string password) {
			return Array.Empty<string>();
		}
		public override string FileFilter { get { return null; } }
		public override bool MeanSchemaGeneration { get { return true; } }
	}
}
namespace DevExpress.Xpo.DB.Helpers {
	using System;
	using System.Data;
	class DbTypeMapperPervasive<TSqlDbTypeEnum, TSqlParameter> : DbTypeMapper<TSqlDbTypeEnum, TSqlParameter>
		where TSqlDbTypeEnum : struct
		where TSqlParameter : IDbDataParameter {
		static readonly TSqlDbTypeEnum psqlTypeLongVarChar;
		static DbTypeMapperPervasive() {
			psqlTypeLongVarChar = (TSqlDbTypeEnum)Enum.Parse(typeof(TSqlDbTypeEnum), "LongVarChar");
		}
		protected override string ParameterDbTypePropertyName { get { return "PsqlDbType"; } }
		protected override string GetParameterTypeNameForBoolean(out int? size, out byte? precision, out byte? scale) {
			size = null;
			precision = scale = null;
			return "Bit";
		}
		protected override string GetParameterTypeNameForByte(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "UTinyInt";
		}
		protected override string GetParameterTypeNameForByteArray(out int? size) {
			size = null;
			return "LongVarBinary";
		}
		protected override string GetParameterTypeNameForChar(out int? size) {
			size = 1;
			return "Char";
		}
		protected override string GetParameterTypeNameForDateTime() {
			return "Timestamp";
		}
		protected override string GetParameterTypeNameForDecimal(out byte? precision, out byte? scale) {
			precision = 20;
			scale = 4;
			return "Decimal";
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
			precision = scale = null;
			return "TinyInt";
		}
		protected override string GetParameterTypeNameForSingle(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Float";
		}
		protected override string GetParameterTypeNameForString(out int? size) {
			size = null;
			return "VarChar";
		}
		protected override string GetParameterTypeNameForTimeSpan() {
			return "Double";
		}
		protected override string GetParameterTypeNameForUInt16(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "USmallInt";
		}
		protected override string GetParameterTypeNameForUInt32(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "UInteger";
		}
		protected override string GetParameterTypeNameForUInt64(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "UBigInt";
		}
		protected override string GetParameterTypeNameForDateOnly(out int? size) {
			size = null;
			return "Timestamp";
		}
		protected override string GetParameterTypeNameForTimeOnly(out int? size) {
			size = null;
			return "Time";
		}
		protected override string ConvertSqlTypeToParameterType(string sqlType) {
			sqlType = sqlType.ToUpperInvariant();
			switch(sqlType) {
				case "SMALLIDENTITY":
					return "SmallIdentity";
				case "IDENTITY":
					return "Identity";
				case "BFLOAT4":
					return "BFloat4";
				case "BFLOAT8":
					return "BFloat8";
				case "LONGVARBINARY":
					return "LongVarBinary";
				case "NLONGVARCHAR":
				case "LONGVARCHAR":
					return "LongVarChar";
				case "CURRENCY":
					return "Currency";
				case "DATE":
					return "Date";
				case "DATETIME":
					return "DateTime";
				case "DECIMAL":
					return "Decimal";
				case "REAL":
					return "Real";
				case "DOUBLE":
					return "Double";
				case "UNIQUEIDENTIFIER":
					return "Guid";
				case "TINYINT":
					return "TinyInt";
				case "SMALLINT":
					return "SmallInt";
				case "INTEGER":
					return "Integer";
				case "BIGINT":
					return "BigInt";
				case "NUMERIC":
					return "Numeric";
				case "NUMERICSA":
					return "NumericSA";
				case "NUMERICSLB":
					return "Numeric";
				case "NUMERICSLS":
				case "NUMERICSTB":
					return "Numeric";
				case "NUMERICSTS":
					return "NumericSTS";
				case "BINARY":
					return "Binary";
				case "CHAR":
				case "NCHAR":
					return "Char";
				case "TIME":
					return "Time";
				case "TIMESTAMP":
					return "Timestamp";
				case "UTINYINT":
					return "UTinyInt";
				case "USMALLINT":
					return "USmallInt";
				case "UINTEGER":
					return "UInteger";
				case "UBIGINT":
					return "BigInt";
				case "NVARCHAR":
				case "VARCHAR":
					return "VarChar";
				case "BIT":
					return "Bit";
				default:
					return null;
			}
		}
		public override void SetParameterTypeAndSize(IDbDataParameter parameter, DBColumnType dbColumnType, int size) {
			if(dbColumnType == DBColumnType.String) {
				if(size <= 0 || size > PervasiveSqlConnectionProvider.MaximumStringSize) {
					SetSqlDbTypeHandler((TSqlParameter)parameter, psqlTypeLongVarChar);
					return;
				}
			}
			base.SetParameterTypeAndSize(parameter, dbColumnType, size);
		}
	}
}
