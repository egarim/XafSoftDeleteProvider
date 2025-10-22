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

using DevExpress.Utils;
namespace DevExpress.Xpo.DB {
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.Data;
	using System.Globalization;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using DevExpress.Data.Filtering;
	using DevExpress.Data.Helpers;
	using DevExpress.Xpo;
	using DevExpress.Xpo.DB.Exceptions;
	using DevExpress.Xpo.DB.Helpers;
#if !NET
	using DevExpress.Data.NetCompatibility.Extensions;
#endif
#pragma warning disable DX0024
	public class SQLiteConnectionProvider : ConnectionProviderSql {
		public const string XpoProviderTypeString = "SQLite";
		ReflectConnectionHelper helper;
		ReflectConnectionHelper ConnectionHelper {
			get {
				if(helper == null)
					helper = new ReflectConnectionHelper(Connection, ProviderAssemblyInfo.ExceptionTypeName);
				return helper;
			}
		}
		DbTypeMapperBase dbTypeMapper;
		protected override DbTypeMapperBase DbTypeMapper {
			get {
				if(dbTypeMapper == null) {
					Type sqlParameterType = ConnectionHelper.GetType(ProviderAssemblyInfo.ParameterTypeName);
					if(providerAssemblyIndex == 0) {
						dbTypeMapper = (DbTypeMapperBase)Activator.CreateInstance(typeof(DbTypeMapperSqlite<>).MakeGenericType(sqlParameterType));
					}
					else {
						Type sqlDbType = ConnectionHelper.GetType(ProviderAssemblyInfo.SqliteDbTypeName);
						dbTypeMapper = (DbTypeMapperBase)Activator.CreateInstance(typeof(DbTypeMapperSqliteMicrosoft<,>).MakeGenericType(sqlDbType, sqlParameterType));
					}
				}
				return dbTypeMapper;
			}
		}
		public static string GetConnectionString(string database) {
			return String.Format("{1}={2};Data Source={0}",
				EscapeConnectionStringArgument(database), DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString);
		}
		public static string GetConnectionString(string database, string password) {
			return String.Format("{2}={3};Data Source={0};Password={1}",
				EscapeConnectionStringArgument(database), EscapeConnectionStringArgument(password), DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString);
		}
		public static IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			IDbConnection connection = CreateConnection(connectionString);
			objectsToDisposeOnDisconnect = new IDisposable[] { connection };
			return CreateProviderFromConnection(connection, autoCreateOption);
		}
		public static IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return new SQLiteConnectionProvider(connection, autoCreateOption);
		}
		static SQLiteConnectionProvider() {
			RegisterDataStoreProvider(XpoProviderTypeString, new DataStoreCreationFromStringDelegate(CreateProviderFromString));
			RegisterDataStoreProvider("System.Data.SQLite.SQLiteConnection", new DataStoreCreationFromConnectionDelegate(CreateProviderFromConnection));
			RegisterDataStoreProvider("Microsoft.Data.Sqlite.SqliteConnection", new DataStoreCreationFromConnectionDelegate(CreateProviderFromConnection));
			RegisterFactory(new SQLiteProviderFactory());
		}
		public static void Register() { }
		public SQLiteConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption)
			: this(connection, autoCreateOption, true) {
		}
		protected SQLiteConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption, bool openConnection)
			: base(connection, autoCreateOption, openConnection) {
			string assemblyName = connection.GetType().Assembly.GetName().Name;
			for(int i = 0; i < assemblyNames.Length; i++) {
				if(assemblyNames[i].AssemblyName == assemblyName) {
					providerAssemblyIndex = i;
					break;
				}
			}
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
			return "nchar(1)";
		}
		protected override string GetSqlCreateColumnTypeForDecimal(DBTable table, DBColumn column) {
			return "money";
		}
		protected override string GetSqlCreateColumnTypeForDouble(DBTable table, DBColumn column) {
			return "double";
		}
		protected override string GetSqlCreateColumnTypeForSingle(DBTable table, DBColumn column) {
			return "float";
		}
		protected override string GetSqlCreateColumnTypeForInt32(DBTable table, DBColumn column) {
			return "int";
		}
		protected override string GetSqlCreateColumnTypeForUInt32(DBTable table, DBColumn column) {
			return "numeric(10,0)";
		}
		protected override string GetSqlCreateColumnTypeForInt16(DBTable table, DBColumn column) {
			return "smallint";
		}
		protected override string GetSqlCreateColumnTypeForUInt16(DBTable table, DBColumn column) {
			return "numeric(5,0)";
		}
		protected override string GetSqlCreateColumnTypeForInt64(DBTable table, DBColumn column) {
			return "numeric(20,0)";
		}
		protected override string GetSqlCreateColumnTypeForUInt64(DBTable table, DBColumn column) {
			return "numeric(20,0)";
		}
		protected override string GetSqlCreateColumnTypeForDateOnly(DBTable table, DBColumn column) {
			return "date";
		}
		protected override string GetSqlCreateColumnTypeForTimeOnly(DBTable table, DBColumn column) {
			return "time";
		}
		public const int MaximumStringSize = 800;
		private const string SystemSQLiteAssemblyName = "System.Data.SQLite";
		private const string MicrosoftSQLiteAssemblyName = "Microsoft.Data.Sqlite";
		protected override string GetSqlCreateColumnTypeForString(DBTable table, DBColumn column) {
			if(column.Size > 0 && column.Size <= MaximumStringSize)
				return "nvarchar(" + column.Size.ToString(CultureInfo.InvariantCulture) + ')';
			else
				return "text";
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
		public override string GetSqlCreateColumnFullAttributes(DBTable table, DBColumn column) {
			return null;
		}
		public override string GetSqlCreateColumnFullAttributes(DBTable table, DBColumn column, bool forTableCreate) {
			string result = GetSqlCreateColumnFullAttributes(table, column);
			if(!string.IsNullOrEmpty(result)) {
				return result;
			}
			result = GetSqlCreateColumnType(table, column);
			if(column.IsKey || !column.IsNullable) {
				if(column.IsIdentity && (column.ColumnType == DBColumnType.Int32 || column.ColumnType == DBColumnType.Int64) && IsSingleColumnPKColumn(table, column)) {
					result = "INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL";
				}
				else {
					result += " NOT NULL";
				}
			}
			if(!column.IsIdentity) {
				if(!string.IsNullOrEmpty(column.DbDefaultValue)) {
					result += string.Concat(" DEFAULT ", column.DbDefaultValue);
				}
				else {
					if(column.DefaultValue != null && column.DefaultValue != System.DBNull.Value) {
						string formattedDefaultValue = FormatConstant(column.DefaultValue);
						result += string.Concat(" DEFAULT (", formattedDefaultValue, ")");
					}
				}
			}
			return result;
		}
		protected override object ReformatReadValue(object value, ConnectionProviderSql.ReformatReadValueArgs args) {
			if(args.DbTypeCode == TypeCode.String) {
				if(args.TargetType == typeof(TimeOnly)) {
					TimeOnly result;
					if(TimeOnly.TryParse((string)value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result)) {
						return result;
					}
				}
				else if(args.TargetType == typeof(DateOnly)) {
					DateOnly result;
					if(DateOnly.TryParse((string)value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result)) {
						return result;
					}
				}
			}
			return base.ReformatReadValue(value, args);
		}
		protected override object ConvertToDbParameter(object clientValue, TypeCode clientValueTypeCode) {
			switch(clientValueTypeCode) {
				case TypeCode.Object:
					if(clientValue is Guid) {
						return clientValue.ToString();
					}
					else if(clientValue is DateOnly) {
						return ((DateOnly)clientValue).ToString("yyyy-MM-dd");
					}
					else if(clientValue is TimeOnly) {
						return ((TimeOnly)clientValue).ToString("HH:mm:ss.fff");
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
		protected override IDataParameter CreateParameter(IDbCommand command, object value, string name, DBColumnType dbType, string dbTypeName, int size) {
			var parameter = base.CreateParameter(command, value, name, dbType, dbTypeName, size);
			if(parameter.DbType == DbType.Decimal) {
				parameter.DbType = DbType.Double;
			}
			QueryParameterMode parameterMode = GetQueryParameterMode();
			if(parameterMode == QueryParameterMode.SetTypeAndSize) {
				ValidateParameterSize(command, (IDbDataParameter)parameter);
			}
			return parameter;
		}
		protected override bool IsConnectionBroken(Exception e) {
			object o;
			if(ConnectionHelper.TryGetExceptionProperty(e, ProviderAssemblyInfo.SqliteErrorCodeName, true, out o) && (o != null)) {
				int errorCode = Convert.ToInt32(o);
				bool broken = Connection.State != ConnectionState.Open
					 || errorCode == 10  
					 || errorCode == 13  
					 || errorCode == 26; 
				if(broken) {
					Connection.Close();
				}
				return broken;
			}
			return false;
		}
		protected override CommandPoolBehavior CommandPoolBehavior { get { return CommandPoolBehavior.TransactionNoPrepare; } }
		protected override Int64 GetIdentity(Query sql) {
			ExecSql(sql);
			object value = GetScalar(new Query("select last_insert_rowid()"));
			return (value as IConvertible).ToInt64(CultureInfo.InvariantCulture);
		}
		protected override async Task<Int64> GetIdentityAsync(Query sql, AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken) {
			await ExecSqlAsync(sql, asyncOperationId, cancellationToken).ConfigureAwait(false);
			object value = await GetScalarAsync(new Query("select last_insert_rowid()"), asyncOperationId, cancellationToken).ConfigureAwait(false);
			return (value as IConvertible).ToInt64(CultureInfo.InvariantCulture);
		}
		protected override IDbConnection CreateConnection() {
			return ConnectionHelper.GetConnection(ConnectionString);
		}
		class SQLiteAssemblyInfo {
			public readonly string AssemblyName;
			public readonly string ConnectionTypeName;
			public readonly string ExceptionTypeName;
			public readonly string SqliteErrorCodeName;
			public readonly string ParameterTypeName;
			public readonly string SqliteDbTypeName;
			public SQLiteAssemblyInfo(string assemblyName, string connectionTypeName, string exceptionTypeName, string sqliteErrorCodeName, string parameterTypeName, string sqliteDbTypeName) {
				AssemblyName = assemblyName;
				ConnectionTypeName = connectionTypeName;
				ExceptionTypeName = exceptionTypeName;
				SqliteErrorCodeName = sqliteErrorCodeName;
				ParameterTypeName = parameterTypeName;
				SqliteDbTypeName = sqliteDbTypeName;
			}
		}
		readonly static SQLiteAssemblyInfo[] assemblyNames = new SQLiteAssemblyInfo[]{
			new SQLiteAssemblyInfo( SystemSQLiteAssemblyName, SystemSQLiteAssemblyName + ".SQLiteConnection", SystemSQLiteAssemblyName + ".SQLiteException", "ErrorCode", SystemSQLiteAssemblyName + ".SQLiteParameter", null),
			new SQLiteAssemblyInfo( MicrosoftSQLiteAssemblyName, MicrosoftSQLiteAssemblyName + ".SqliteConnection", MicrosoftSQLiteAssemblyName + ".SqliteException", "SqliteErrorCode", MicrosoftSQLiteAssemblyName + ".SqliteParameter", MicrosoftSQLiteAssemblyName + ".SqliteType"),
		};
		readonly int providerAssemblyIndex;
		SQLiteAssemblyInfo ProviderAssemblyInfo {
			get {
				return assemblyNames[providerAssemblyIndex];
			}
		}
		bool IsMicrosoftDataSQLite {
			get { return providerAssemblyIndex == 1; }
		}
		static int assemblyNameFoundIndex;
		public static IDbConnection CreateConnection(string connectionString) {
			var assemblies = new string[assemblyNames.Length];
			var types = new string[assemblyNames.Length];
			for(int i = 0; i < assemblyNames.Length; i++) {
				assemblies[i] = assemblyNames[i].AssemblyName;
				types[i] = assemblyNames[i].ConnectionTypeName;
			}
			IDbConnection connection = ReflectConnectionHelper.GetConnection(assemblies, types, true, ref assemblyNameFoundIndex);
			connection.ConnectionString = connectionString;
			return connection;
		}
		protected override void CreateDataBase() {
			try {
				Connection.Open();
			}
			catch(Exception e) {
				throw new UnableToOpenDatabaseException(XpoDefault.ConnectionStringRemovePassword(ConnectionString), e);
			}
		}
		protected override Exception WrapException(Exception e, IDbCommand query) {
			object[] values;
			if(ConnectionHelper.TryGetExceptionProperties(e, new string[] { "Message", ProviderAssemblyInfo.SqliteErrorCodeName }, new bool[] { false, true }, out values)) {
				string message = (string)values[0];
				if(message.IndexOf("no such column") > 0 ||
					message.IndexOf("no column named") > 0 ||
					message.IndexOf("no such table") > 0 ||
					message.IndexOf("no table named") > 0)
					return new SchemaCorrectionNeededException(e);
				if(values[1] != null) {
					int errCode = Convert.ToInt32(values[1]);
					if(errCode == 19) { 
						return new ConstraintViolationException(query.CommandText, GetParametersString(query), e);
					}
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
					parameters.Add(new ParameterValue(i) { Value = ComposeSafeTableName(table.Name), DBTypeName = "TEXT" });
					inList.Add("@p" + i.ToString(CultureInfo.InvariantCulture));
					++i;
				}
			}
			if(inList.Count == 0)
				return new SelectStatementResult();
			return SelectData(new Query(string.Format(CultureInfo.InvariantCulture, queryText, StringListHelper.DelimitedText(inList, ",")), parameters, inList));
		}
		DBColumnType GetTypeFromString(string typeName, out int size) {
			size = 0;
			typeName = typeName.ToLowerInvariant();
			switch(typeName) {
				case "bigint":
				case "integer":
				case "int":
					return DBColumnType.Int64;
				case "boolean":
				case "bit":
					return DBColumnType.Boolean;
				case "tinyint":
					return DBColumnType.Byte;
				case "text":
					return DBColumnType.String;
				case "time":
					return !GlobalUseLegacyDateOnlyAndTimeOnlySupport ? DBColumnType.TimeOnly : DBColumnType.DateTime;
				case "date":
					return !GlobalUseLegacyDateOnlyAndTimeOnlySupport ? DBColumnType.DateOnly : DBColumnType.DateTime;
				case "datetime":
					return DBColumnType.DateTime;
				case "money":
					return DBColumnType.Decimal;
				case "real":
				case "double precision":
				case "double":
					return DBColumnType.Double;
				case "float":
					return DBColumnType.Single;
				case "smallint":
					return DBColumnType.Int16;
				case "blob":
					return DBColumnType.ByteArray;
			}
			if(typeName.StartsWith("nvarchar") || typeName.StartsWith("varchar") || typeName.StartsWith("text")) {
				int pos = typeName.IndexOf('(') + 1;
				if(pos > 0) {
					size = Int32.Parse(typeName.Substring(pos, typeName.IndexOf(')') - pos));
				}
				return DBColumnType.String;
			}
			if(typeName.StartsWith("numeric"))
				return DBColumnType.Decimal;
			if(typeName.StartsWith("char") || typeName.StartsWith("nchar")) {
				int pos = typeName.IndexOf('(') + 1;
				if(pos == 0)
					return DBColumnType.Char;
				size = Int32.Parse(typeName.Substring(pos, typeName.IndexOf(')') - pos));
				if(size == 1)
					return DBColumnType.Char;
				return DBColumnType.String;
			}
			if(typeName.StartsWith("image")) {
				int pos = typeName.IndexOf('(') + 1;
				if(pos == 0)
					return DBColumnType.ByteArray;
				size = Int32.Parse(typeName.Substring(pos, typeName.IndexOf(')') - pos));
				return DBColumnType.ByteArray;
			}
			return DBColumnType.Unknown;
		}
		void GetColumns(DBTable table) {
			foreach(SelectStatementResultRow row in SelectData(new Query("PRAGMA table_info('" + ComposeSafeTableName(table.Name) + "')")).Rows) {
				int size;
				string dbType = (string)row.Values[2];
				DBColumnType type = GetTypeFromString(dbType, out size);
				bool isNullable = (Convert.ToInt64(row.Values[3]) != 1);
				string dbDefaultValue = (row.Values[4] as string);
				object defaultValue = null;
				try {
					if(!string.IsNullOrEmpty(dbDefaultValue)) {
						decimal decimalDefaultValue;
						if(type == DBColumnType.Decimal && TryParseDbDecimal(dbDefaultValue, out decimalDefaultValue)) {
							defaultValue = decimalDefaultValue;
						}
						else {
							string scalarQuery = string.Concat("select ", dbDefaultValue);
							defaultValue = FixDBNullScalar(GetScalar(new Query(scalarQuery)));
						}
					}
				}
				catch { }
				if(defaultValue != null) {
					ReformatReadValueArgs refmtArgs = new ReformatReadValueArgs(DBColumn.GetType(type));
					refmtArgs.AttachValueReadFromDb(defaultValue);
					try {
						defaultValue = ReformatReadValue(defaultValue, refmtArgs);
					}
					catch {
						defaultValue = null;
					}
				}
				DBColumn dBColumn = new DBColumn((string)row.Values[1], false, dbType ?? String.Empty, size, type, isNullable, defaultValue);
				dBColumn.DbDefaultValue = dbDefaultValue;
				table.AddColumn(dBColumn);
			}
		}
		bool TryParseDbDecimal(string decimalValueString, out decimal outValue) {
			while(decimalValueString.StartsWith('(') && decimalValueString.EndsWith(')')) {
				decimalValueString = decimalValueString.Substring(1, decimalValueString.Length - 2);
			}
			return decimal.TryParse(decimalValueString, NumberStyles.Any, CultureInfo.InvariantCulture, out outValue);
		}
		public override void CreatePrimaryKey(DBTable table) {
		}
		void GetPrimaryKey(DBTable table) {
			StringCollection cols = new StringCollection();
			foreach(SelectStatementResultRow row in SelectData(new Query("PRAGMA table_info('" + ComposeSafeTableName(table.Name) + "')")).Rows) {
				if((Int64)row.Values[5] == 1) {
					string columnName = (string)row.Values[1];
					DBColumn column = table.GetColumn(columnName);
					if(column != null)
						column.IsKey = true;
					cols.Add(columnName);
				}
			}
			table.PrimaryKey = cols.Count > 0 ? new DBPrimaryKey(cols) : null;
		}
		void GetIndexes(DBTable table) {
			SelectStatementResult data = SelectData(new Query("pragma index_list('" + ComposeSafeTableName(table.Name) + "')"));
			foreach(SelectStatementResultRow row in data.Rows) {
				SelectStatementResult indexData = SelectData(new Query("pragma index_info('" + (string)row.Values[1] + "')"));
				StringCollection cols = new StringCollection();
				foreach(SelectStatementResultRow index in indexData.Rows) {
					cols.Add((string)index.Values[2]);
				}
				table.Indexes.Add(new DBIndex((string)row.Values[1], cols, (long)row.Values[2] == 1));
			}
		}
		public override void CreateForeignKey(DBTable table, DBForeignKey fk) {
		}
		void GetForeignKeys(DBTable table) {
			SelectStatementResult data = SelectData(new Query("pragma foreign_key_list('" + ComposeSafeTableName(table.Name) + "')"));
			foreach(SelectStatementResultRow row in data.Rows) {
				StringCollection pkc = new StringCollection();
				StringCollection fkc = new StringCollection();
				pkc.Add((string)row.Values[3]);
				fkc.Add((string)row.Values[4]);
				table.ForeignKeys.Add(new DBForeignKey(pkc, ((string)row.Values[2]).Trim('\"'), fkc));
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
			Dictionary<string, bool> dbTables = new Dictionary<string, bool>();
			foreach(SelectStatementResultRow row in GetDataForTables(tables, null, "SELECT [name] FROM [sqlite_master] WHERE [type] LIKE 'table' and [name] in ({0})").Rows)
				dbTables.Add((string)row.Values[0], false);
			foreach(SelectStatementResultRow row in GetDataForTables(tables, null, "SELECT [name] FROM [sqlite_master] WHERE [type] LIKE 'view' and [name] in ({0})").Rows)
				dbTables.Add((string)row.Values[0], true);
			ArrayList list = new ArrayList();
			foreach(DBTable table in tables) {
				bool isView;
				if(dbTables.TryGetValue(ComposeSafeTableName(table.Name), out isView))
					table.IsView = isView;
				else
					list.Add(table);
			}
			return list;
		}
		public override void CreateTable(DBTable table) {
			string columns = "";
			foreach(DBColumn col in table.Columns) {
				if(columns.Length > 0)
					columns += ", ";
				columns += (FormatColumnSafe(col.Name) + ' ' + GetSqlCreateColumnFullAttributes(table, col, true));
			}
			if(table.PrimaryKey != null && !table.GetColumn(table.PrimaryKey.Columns[0]).IsIdentity) {
				StringCollection formattedColumns = new StringCollection();
				for(Int32 i = 0; i < table.PrimaryKey.Columns.Count; ++i)
					formattedColumns.Add(FormatColumnSafe(table.PrimaryKey.Columns[i]));
				ExecuteSqlSchemaUpdate("Table", table.Name, string.Empty, String.Format(CultureInfo.InvariantCulture,
					"create table {0} ({1}, primary key ({2}))",
					FormatTableSafe(table), columns, StringListHelper.DelimitedText(formattedColumns, ",")));
			}
			else {
				ExecuteSqlSchemaUpdate("Table", table.Name, string.Empty, String.Format(CultureInfo.InvariantCulture,
					"create table {0} ({1})",
					FormatTableSafe(table), columns));
			}
		}
		protected override int GetSafeNameTableMaxLength() {
			return 1024;
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
		public override string FormatFunction(FunctionOperatorType operatorType, params string[] operands) {
			switch(operatorType) {
				case FunctionOperatorType.Abs:
					return string.Format("Abs({0})", operands[0]);
				case FunctionOperatorType.Sign:
					return string.Format("Sign({0})", operands[0]);
				case FunctionOperatorType.BigMul:
					return string.Format("CAST(({0}) * ({1}) as BIGINT)", operands[0], operands[1]);
				case FunctionOperatorType.Round:
					switch(operands.Length) {
						case 1:
							return string.Format("Round({0})", operands[0]);
						case 2:
							return string.Format("Round({0},{1})", operands[0], operands[1]);
					}
					goto default;
				case FunctionOperatorType.Floor:
					return string.Format("Floor({0})", operands[0]);
				case FunctionOperatorType.Ceiling:
					return string.Format("Ceiling({0})", operands[0]);
				case FunctionOperatorType.Cos:
					return string.Format("Cos({0})", operands[0]);
				case FunctionOperatorType.Sin:
					return string.Format("Sin({0})", operands[0]);
				case FunctionOperatorType.Acos:
					return string.Format("ACos({0})", operands[0]);
				case FunctionOperatorType.Asin:
					return string.Format("ASin({0})", operands[0]);
				case FunctionOperatorType.Cosh:
					return string.Format("Cosh({0})", operands[0]);
				case FunctionOperatorType.Sinh:
					return string.Format("Sinh({0})", operands[0]);
				case FunctionOperatorType.Exp:
					return string.Format("Exp({0})", operands[0]);
				case FunctionOperatorType.Log:
					switch(operands.Length) {
						case 1:
							if(IsMicrosoftDataSQLite) {
								return string.Format(CultureInfo.InvariantCulture, "Ln({0})", operands[0]);
							}
							else {
								return string.Format(CultureInfo.InvariantCulture, "Log({0})", operands[0]);
							}
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "(Log({0}) / Log({1}))", operands[0], operands[1]);
					}
					goto default;
				case FunctionOperatorType.Log10:
					return string.Format("Log10({0})", operands[0]);
				case FunctionOperatorType.Atn:
					return string.Format("Atan({0})", operands[0]);
				case FunctionOperatorType.Atn2:
					return string.Format(CultureInfo.InvariantCulture, "Atan2({0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.Tan:
					return string.Format("Tan({0})", operands[0]);
				case FunctionOperatorType.Tanh:
					return string.Format("Tanh({0})", operands[0]);
				case FunctionOperatorType.Power:
					return string.Format("Power({0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.Sqr:
					return string.Format("Sqrt({0})", operands[0]);
				case FunctionOperatorType.Rnd:
					return "((random() / 18446744073709551614) + 0.5)";
				case FunctionOperatorType.Now:
					return "datetime('now','localtime')";
				case FunctionOperatorType.UtcNow:
					return "datetime('now')";
				case FunctionOperatorType.Today:
					return "datetime(date('now','localtime'))";
				case FunctionOperatorType.Replace:
					return string.Format("Replace({0}, {1}, {2})", operands[0], operands[1], operands[2]);
				case FunctionOperatorType.Reverse:
					return string.Format("Reverse({0})", operands[0]);
				case FunctionOperatorType.Insert:
					return string.Format(CultureInfo.InvariantCulture, "(SUBSTR({0}, 1, {1}) || ({2}) || SUBSTR({0}, ({1}) + 1))", operands[0], operands[1], operands[2]);
				case FunctionOperatorType.Remove:
					switch(operands.Length) {
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "SUBSTR({0}, 1, {1})", operands[0], operands[1]);
						case 3:
							return string.Format(CultureInfo.InvariantCulture, "(SUBSTR({0}, 1, {1}) || SUBSTR({0}, ({1}) + ({2}) + 1))", operands[0], operands[1], operands[2]);
					}
					goto default;
				case FunctionOperatorType.AddTicks:
					return string.Format(CultureInfo.InvariantCulture, "strftime('%Y-%m-%d %H:%M:%f', {0} , CAST((({1}) / 10000000) as text) || ' second')", operands[0], operands[1]);
				case FunctionOperatorType.AddMilliSeconds:
					return string.Format(CultureInfo.InvariantCulture, "strftime('%Y-%m-%d %H:%M:%f', {0} , CAST((({1}) / 1000) as text) || ' second')", operands[0], operands[1]);
				case FunctionOperatorType.AddTimeSpan:
				case FunctionOperatorType.AddSeconds:
					return string.Format(CultureInfo.InvariantCulture, "strftime('%Y-%m-%d %H:%M:%f', {0} , CAST({1} as text) || ' second')", operands[0], operands[1]);
				case FunctionOperatorType.AddMinutes:
					return string.Format(CultureInfo.InvariantCulture, "strftime('%Y-%m-%d %H:%M:%f', {0} , CAST({1} as text) || ' minute')", operands[0], operands[1]);
				case FunctionOperatorType.AddHours:
					return string.Format(CultureInfo.InvariantCulture, "strftime('%Y-%m-%d %H:%M:%f', {0} , CAST({1} as text) || ' hour')", operands[0], operands[1]);
				case FunctionOperatorType.AddDays:
					return string.Format(CultureInfo.InvariantCulture, "strftime('%Y-%m-%d %H:%M:%f', {0} , CAST({1} as text) || ' day')", operands[0], operands[1]);
				case FunctionOperatorType.AddMonths:
					return string.Format(CultureInfo.InvariantCulture, "strftime('%Y-%m-%d %H:%M:%f', {0} , CAST({1} as text) || ' month')", operands[0], operands[1]);
				case FunctionOperatorType.AddYears:
					return string.Format(CultureInfo.InvariantCulture, "strftime('%Y-%m-%d %H:%M:%f', {0} , CAST({1} as text) || ' year')", operands[0], operands[1]);
				case FunctionOperatorType.GetMilliSecond:
					return string.Format(CultureInfo.InvariantCulture, "(CAST((strftime('%f', {0}) * 1000) as integer) % 1000)", operands[0]);
				case FunctionOperatorType.GetSecond:
					return string.Format(CultureInfo.InvariantCulture, "CAST(strftime('%S', {0}) as integer)", operands[0]);
				case FunctionOperatorType.GetMinute:
					return string.Format(CultureInfo.InvariantCulture, "CAST(strftime('%M', {0}) as integer)", operands[0]);
				case FunctionOperatorType.GetHour:
					return string.Format(CultureInfo.InvariantCulture, "CAST(strftime('%H', {0}) as integer)", operands[0]);
				case FunctionOperatorType.GetDay:
					return string.Format(CultureInfo.InvariantCulture, "CAST(strftime('%d', {0}) as integer)", operands[0]);
				case FunctionOperatorType.GetMonth:
					return string.Format(CultureInfo.InvariantCulture, "CAST(strftime('%m', {0}) as integer)", operands[0]);
				case FunctionOperatorType.GetYear:
					return string.Format(CultureInfo.InvariantCulture, "CAST(strftime('%Y', {0}) as integer)", operands[0]);
				case FunctionOperatorType.GetDayOfWeek:
					return string.Format(CultureInfo.InvariantCulture, "CAST(strftime('%w', {0}) as integer)", operands[0]);
				case FunctionOperatorType.GetDayOfYear:
					return string.Format(CultureInfo.InvariantCulture, "CAST(strftime('%j', {0}) as integer)", operands[0]);
				case FunctionOperatorType.GetTimeOfDay:
					return string.Format(CultureInfo.InvariantCulture, "(CAST((strftime('%H', {0}) * 36000000000) as integer) + CAST((strftime('%M', {0}) * 600000000) as integer) + CAST((strftime('%f', {0}) * 10000000) as integer))", operands[0]);
				case FunctionOperatorType.GetDate:
					return string.Format(CultureInfo.InvariantCulture, "datetime(date({0}))", operands[0]);
				case FunctionOperatorType.DateDiffYear:
					return string.Format(CultureInfo.InvariantCulture, "(strftime('%Y', {1}) - strftime('%Y', {0}))", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffMonth:
					return string.Format(CultureInfo.InvariantCulture, "(((strftime('%Y', {1}) - strftime('%Y', {0})) * 12) + strftime('%m', {1}) - strftime('%m', {0}))", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffDay:
					return string.Format(CultureInfo.InvariantCulture, "(julianday(date({1})) - julianday(date({0})))", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffHour:
					return string.Format(CultureInfo.InvariantCulture, "(((julianday(date({1})) - julianday(date({0}))) * 24) + (strftime('%H', {1}) - strftime('%H', {0})))", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffMinute:
					return string.Format(CultureInfo.InvariantCulture, "((((((julianday(date({1})) - julianday(date({0}))) * 24) + (strftime('%H', {1}) - strftime('%H', {0})))) * 60)  + (strftime('%M', {1}) - strftime('%M', {0})))", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffSecond:
					return string.Format(CultureInfo.InvariantCulture, "(strftime('%s', {1}) - strftime('%s', {0}))", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffMilliSecond:
					return string.Format(CultureInfo.InvariantCulture, "((strftime('%s', {1}) - strftime('%s', {0})) * 1000)", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffTick:
					return string.Format(CultureInfo.InvariantCulture, "((strftime('%s', {1}) - strftime('%s', {0}))) * 10000000", operands[0], operands[1]);
				case FunctionOperatorType.Len: {
					string args = string.Empty;
					foreach(string arg in operands) {
						if(args.Length > 0)
							args += ", ";
						args += arg;
					}
					return "Length(" + args + ")";
				}
				case FunctionOperatorType.PadLeft:
					switch(operands.Length) {
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "PadL({0}, {1})", operands[0], operands[1]);
						default:
							throw new NotSupportedException();
					}
				case FunctionOperatorType.PadRight:
					switch(operands.Length) {
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "PadR({0}, {1})", operands[0], operands[1]);
						default:
							throw new NotSupportedException();
					}
				case FunctionOperatorType.CharIndex:
					switch(operands.Length) {
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "(Instr({1}, {0}) - 1)", operands[0], operands[1]);
						case 3:
							return string.Format(CultureInfo.InvariantCulture, "(Charindex({0}, {1}, ({2}) + 1) - 1)", operands[0], operands[1], operands[2]);
						case 4:
							return string.Format(CultureInfo.InvariantCulture, "(Charindex({0}, Substr({1}, 1, ({2}) + ({3})), ({2}) + 1) - 1)", operands[0], operands[1], operands[2], operands[3]);
					}
					goto default;
				case FunctionOperatorType.Substring: {
					switch(operands.Length) {
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "substr({0}, ({1}) + 1)", operands[0], operands[1]);
						case 3:
							return string.Format(CultureInfo.InvariantCulture, "substr({0}, ({1}) + 1, {2})", operands[0], operands[1], operands[2]);
					}
					goto default;
				}
				case FunctionOperatorType.ToInt:
					return string.Format(CultureInfo.InvariantCulture, "CAST({0} as int)", operands[0]);
				case FunctionOperatorType.ToLong:
					return string.Format(CultureInfo.InvariantCulture, "CAST({0} as bigint)", operands[0]);
				case FunctionOperatorType.ToFloat:
					return string.Format(CultureInfo.InvariantCulture, "CAST({0} as real)", operands[0]);
				case FunctionOperatorType.ToDouble:
					return string.Format(CultureInfo.InvariantCulture, "CAST({0} as double precision)", operands[0]);
				case FunctionOperatorType.ToDecimal:
					return string.Format(CultureInfo.InvariantCulture, "CAST({0} as money)", operands[0]);
				case FunctionOperatorType.ToStr:
					return string.Format(CultureInfo.InvariantCulture, "CAST({0} as text)", operands[0]);
				case FunctionOperatorType.Ascii:
				case FunctionOperatorType.Char:
				case FunctionOperatorType.Max:
				case FunctionOperatorType.Min:
					throw new NotSupportedException();
				case FunctionOperatorType.IsNull:
					switch(operands.Length) {
						case 1:
							return string.Format(CultureInfo.InvariantCulture, "(({0}) is null)", operands[0]);
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "COALESCE({0}, {1})", operands[0], operands[1]);
					}
					goto default;
				case FunctionOperatorType.IsNullOrEmpty:
					return string.Format(CultureInfo.InvariantCulture, "(({0}) is null or length({0}) = 0)", operands[0]);
				case FunctionOperatorType.EndsWith:
					return string.Format(CultureInfo.InvariantCulture, "(SubsTr({0}, Length({0}) - Length({1}) + 1) = ({1}))", operands[0], operands[1]);
				case FunctionOperatorType.Contains:
					return string.Format(CultureInfo.InvariantCulture, "(InsTR({0}, {1}) > 0)", operands[0], operands[1]);
				default:
					return base.FormatFunction(operatorType, operands);
			}
		}
		readonly static char[] achtungChars = new char[] { '_', '%' };
		public override string FormatFunction(ProcessParameter processParameter, FunctionOperatorType operatorType, params object[] operands) {
			switch(operatorType) {
				case FunctionOperatorType.StartsWith:
					object secondOperand = operands[1];
					if(secondOperand is OperandValue && ((OperandValue)secondOperand).Value is string) {
						string operandString = (string)((OperandValue)secondOperand).Value;
						int likeIndex = operandString.IndexOfAny(achtungChars);
						if(likeIndex < 0) {
							return string.Format(CultureInfo.InvariantCulture, "({0} likE {1})", processParameter(operands[0]), processParameter(new ConstantValue(operandString + "%")));
						}
						else if(likeIndex > 0) {
							return string.Format(CultureInfo.InvariantCulture, "(({0} likE {2}) And (SubstR({0}, 1, Length({1})) = ({1})))", processParameter(operands[0]), processParameter(secondOperand), processParameter(new ConstantValue(operandString.Substring(0, likeIndex) + "%")));
						}
					}
					return string.Format(CultureInfo.InvariantCulture, "(SubstR({0}, 1, Length({1})) = ({1}))", processParameter(operands[0]), processParameter(operands[1]));
				case FunctionOperatorType.AddHours:
				case FunctionOperatorType.AddMinutes:
				case FunctionOperatorType.AddSeconds:
					if(ResolveColumnType((CriteriaOperator)operands[0]) == typeof(TimeOnly)) {
						return FormatFunctionTimeOnly(processParameter, operatorType, operands);
					}
					break;
				case FunctionOperatorType.AddDays:
				case FunctionOperatorType.AddMonths:
				case FunctionOperatorType.AddYears:
					if(ResolveColumnType((CriteriaOperator)operands[0]) == typeof(DateOnly)) {
						return FormatFunctionDateOnly(processParameter, operatorType, operands);
					}
					break;
			}
			return base.FormatFunction(processParameter, operatorType, operands);
		}
		string FormatFunctionDateOnly(ProcessParameter processParameter, FunctionOperatorType operatorType, params object[] operands) {
			string[] parameters = new string[operands.Length];
			for(int i = 0; i < operands.Length; i++) {
				object operand = operands[i];
				string processedParameter = processParameter(operand);
				parameters[i] = processedParameter;
			}
			switch(operatorType) {
				case FunctionOperatorType.AddDays:
					return string.Format(CultureInfo.InvariantCulture, "strftime('%Y-%m-%d', {0}, cast({1} as text) || ' days')", parameters[0], parameters[1]);
				case FunctionOperatorType.AddYears:
					return string.Format(CultureInfo.InvariantCulture, "strftime('%Y-%m-%d', {0}, cast({1} as text) || ' years')", parameters[0], parameters[1]);
				case FunctionOperatorType.AddMonths:
					return string.Format(CultureInfo.InvariantCulture, "strftime('%Y-%m-%d', {0}, cast({1} as text) || ' months')", parameters[0], parameters[1]);
				default:
					throw new NotSupportedException();
			}
		}
		string FormatFunctionTimeOnly(ProcessParameter processParameter, FunctionOperatorType operatorType, params object[] operands) {
			string[] parameters = new string[operands.Length];
			for(int i = 0; i < operands.Length; i++) {
				object operand = operands[i];
				string processedParameter = processParameter(operand);
				parameters[i] = processedParameter;
			}
			switch(operatorType) {
				case FunctionOperatorType.AddSeconds:
					return string.Format(CultureInfo.InvariantCulture, "strftime('%H:%M:%f', {0}, cast({1} as text) || ' seconds')", parameters[0], parameters[1]);
				case FunctionOperatorType.AddMinutes:
					return string.Format(CultureInfo.InvariantCulture, "strftime('%H:%M:%f', {0}, cast({1} as text) || ' minutes')", parameters[0], parameters[1]);
				case FunctionOperatorType.AddHours:
					return string.Format(CultureInfo.InvariantCulture, "strftime('%H:%M:%f', {0}, cast({1} as text) || ' hours')", parameters[0], parameters[1]);
				default:
					throw new NotSupportedException();
			}
		}
		public override string FormatSelect(string selectedPropertiesSql, string fromSql, string whereSql, string orderBySql, string groupBySql, string havingSql, int skipSelectedRecords, int topSelectedRecords) {
			base.FormatSelect(selectedPropertiesSql, fromSql, whereSql, orderBySql, groupBySql, havingSql, skipSelectedRecords, topSelectedRecords);
			string expandedWhereSql = whereSql != null ? string.Format(CultureInfo.InvariantCulture, "\nwhere {0}", whereSql) : string.Empty;
			string expandedOrderBySql = orderBySql != null ? string.Format(CultureInfo.InvariantCulture, "\norder by {0}", orderBySql) : string.Empty;
			string expandedHavingSql = havingSql != null ? string.Format(CultureInfo.InvariantCulture, "\nhaving {0}", havingSql) : string.Empty;
			string expandedGroupBySql = groupBySql != null ? string.Format(CultureInfo.InvariantCulture, "\ngroup by {0}", groupBySql) : string.Empty;
			string modificatorsSql = string.Empty;
			if(skipSelectedRecords != 0 || topSelectedRecords != 0) {
				modificatorsSql = string.Format(CultureInfo.InvariantCulture, " LIMIT {0} OFFSET {1} ", topSelectedRecords == 0 ? Int32.MaxValue : topSelectedRecords, skipSelectedRecords);
			}
			return string.Format(CultureInfo.InvariantCulture, "select {0} from {1}{2}{3}{4}{5}{6}", selectedPropertiesSql, fromSql, expandedWhereSql, expandedGroupBySql, expandedHavingSql, expandedOrderBySql, modificatorsSql);
		}
		public override bool NativeSkipTakeSupported { get { return true; } }
		public override string FormatInsertDefaultValues(string tableName) {
			return string.Format(CultureInfo.InvariantCulture, "insert into {0} values(null)", tableName);
		}
		public override string FormatInsert(string tableName, string fields, string values) {
			return string.Format(CultureInfo.InvariantCulture, "insert into {0}({1})values({2})",
				tableName, fields, values);
		}
		public override string FormatUpdate(string tableName, string sets, string whereClause) {
			return string.Format(CultureInfo.InvariantCulture, "update {0} set {1} where {2}",
				tableName, sets, whereClause);
		}
		public override bool BraceJoin { get { return false; } }
		public override string FormatDelete(string tableName, string whereClause) {
			return string.Format(CultureInfo.InvariantCulture, "delete from {0} where {1}", tableName, whereClause);
		}
		public override string FormatBinary(BinaryOperatorType operatorType, string leftOperand, string rightOperand) {
			switch(operatorType) {
				case BinaryOperatorType.BitwiseAnd:
					return string.Format(CultureInfo.InvariantCulture, "{0} & {1}", leftOperand, rightOperand);
				case BinaryOperatorType.BitwiseOr:
					return string.Format(CultureInfo.InvariantCulture, "{0} | {1}", leftOperand, rightOperand);
				case BinaryOperatorType.BitwiseXor:
					return string.Format(CultureInfo.InvariantCulture, "(~({0}&{1}) & ({0}|{1}))", leftOperand, rightOperand);
				case BinaryOperatorType.Modulo:
					return string.Format(CultureInfo.InvariantCulture, "{0} % {1}", leftOperand, rightOperand);
				default:
					return base.FormatBinary(operatorType, leftOperand, rightOperand);
			}
		}
		public override string GetParameterName(OperandValue parameter, int index, ref bool createParameter) {
			object value = parameter.Value;
			createParameter = false;
			if(parameter is ConstantValue && value != null) {
				switch(DXTypeExtensions.GetTypeCode(value.GetType())) {
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
		protected string FormatString(object value) {
			return "'" + ((string)value).Replace("'", "''") + "'";
		}
		public override string FormatConstraint(string constraintName) {
			return string.Format(CultureInfo.InvariantCulture, "[{0}]", constraintName);
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
					if(value is char && Convert.ToInt32(value) < 32) {
						return string.Format("char({0})", Convert.ToByte(value).ToString(CultureInfo.InvariantCulture));
					}
					else {
						return "'" + (char)value + "'";
					}
				case TypeCode.DateTime:
					DateTime datetimeValue = (DateTime)value;
					string dateTimeFormatPattern;
					dateTimeFormatPattern = "yyyy-MM-dd HH:mm:ss.fff";
					return string.Concat("'", datetimeValue.ToString(dateTimeFormatPattern, CultureInfo.InvariantCulture), "'");
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
					else if(value is DateOnly) {
						var dateValue = (DateOnly)value;
						const string dateFormatPattern = "yyyy-MM-dd";
						return string.Format("'{0}'", dateValue.ToString(dateFormatPattern, CultureInfo.InvariantCulture));
					}
					else if(value is TimeOnly) {
						var timeValue = (TimeOnly)value;
						string timeFormatPattern = "HH:mm:ss.ffff";
						return string.Format("'{0}'", timeValue.ToString(timeFormatPattern, CultureInfo.InvariantCulture));
					}
					else {
						ArgumentException ex = new ArgumentException(null, nameof(value));
						ex.Data["Value"] = value.ToString();
						throw ex;
					}
			}
		}
		string FixNonFixedText(string toFix) {
			if(toFix.IndexOfAny(new char[] { '.', 'e', 'E' }) < 0)
				toFix += ".0";
			return toFix;
		}
		protected override void ProcessClearDatabase() {
			Connection.Close();
			ConnectionStringParser helper = new ConnectionStringParser(ConnectionString);
			string file = helper.GetPartByName("Data Source");
			if(File.Exists(file))
				File.Delete(file);
		}
		public override string[] GetStorageTablesList(bool includeViews) {
			SelectStatementResult tables = SelectData(new Query(String.Format("SELECT [name] FROM [sqlite_master] WHERE [type] LIKE 'table'{0} and [name] not like 'SQLITE_%'", (includeViews ? " or [type] LIKE 'view'" : ""))));
			ArrayList result = new ArrayList(tables.Rows.Length);
			foreach(SelectStatementResultRow row in tables.Rows) {
				result.Add(row.Values[0]);
			}
			return (string[])result.ToArray(typeof(string));
		}
		protected override SelectedData ExecuteSproc(string sprocName, params OperandValue[] parameters) {
			throw new NotSupportedException();
		}
		protected override Task<SelectedData> ExecuteSprocAsync(AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken, string sprocName, params OperandValue[] parameters) {
			throw new NotSupportedException();
		}
		protected override void CommandBuilderDeriveParameters(IDbCommand command) {
			throw new NotSupportedException();
		}
		public override DBStoredProcedure[] GetStoredProcedures() {
			throw new NotSupportedException();
		}
	}
#pragma warning restore DX0024
	public class SQLiteProviderFactory : ProviderFactory {
		public override IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return SQLiteConnectionProvider.CreateProviderFromConnection(connection, autoCreateOption);
		}
		public override IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			return SQLiteConnectionProvider.CreateProviderFromString(connectionString, autoCreateOption, out objectsToDisposeOnDisconnect);
		}
		public override string GetConnectionString(Dictionary<string, string> parameters) {
			if(!parameters.ContainsKey(DatabaseParamID)) { return null; }
			string connectionString;
			string password;
			if(parameters.TryGetValue(PasswordParamID, out password) && !string.IsNullOrEmpty(password)) {
				connectionString = SQLiteConnectionProvider.GetConnectionString(parameters[DatabaseParamID], parameters[PasswordParamID]);
			}
			else {
				connectionString = SQLiteConnectionProvider.GetConnectionString(parameters[DatabaseParamID]);
			}
			return connectionString;
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
		public override bool HasUserName { get { return false; } }
		public override bool HasPassword { get { return true; } }
		public override bool HasIntegratedSecurity { get { return false; } }
		public override bool HasMultipleDatabases { get { return false; } }
		public override bool IsServerbased { get { return false; } }
		public override bool IsFilebased { get { return true; } }
		public override string ProviderKey { get { return SQLiteConnectionProvider.XpoProviderTypeString; } }
		public override string[] GetDatabases(string server, string userId, string password) {
			return new string[1] { server };
		}
		public override string FileFilter { get { return "SQLite databases (*.db, *.sqlite, *.sqlite3)|*.db;*.sqlite;*.sqlite3|All files (*.*)|*.*"; } }
		public override bool MeanSchemaGeneration { get { return true; } }
	}
}
namespace DevExpress.Xpo.DB.Helpers {
	using System.Data;
	class DbTypeMapperSqlite<TSqlParameter> : DbTypeMapper<DbType, TSqlParameter>
		where TSqlParameter : IDbDataParameter {
		protected override string ParameterDbTypePropertyName { get { return "DbType"; } }
		protected override string GetParameterTypeNameForBoolean(out int? size, out byte? precision, out byte? scale) {
			size = null;
			precision = scale = null;
			return nameof(DbType.Boolean);
		}
		protected override string GetParameterTypeNameForByte(out byte? precision, out byte? scale) {
			precision = scale = null;
			return nameof(DbType.Byte);
		}
		protected override string GetParameterTypeNameForByteArray(out int? size) {
			size = null;
			return nameof(DbType.Binary);
		}
		protected override string GetParameterTypeNameForChar(out int? size) {
			size = 1;
			return nameof(DbType.StringFixedLength);
		}
		protected override string GetParameterTypeNameForDateTime() {
			return nameof(DbType.DateTime);
		}
		protected override string GetParameterTypeNameForDecimal(out byte? precision, out byte? scale) {
			precision = scale = null;
			return nameof(DbType.Decimal);
		}
		protected override string GetParameterTypeNameForDouble(out byte? precision, out byte? scale) {
			precision = scale = null;
			return nameof(DbType.Double);
		}
		protected override string GetParameterTypeNameForGuid(out int? size) {
			size = 36;
			return nameof(DbType.StringFixedLength);
		}
		protected override string GetParameterTypeNameForInt16(out byte? precision, out byte? scale) {
			precision = scale = null;
			return nameof(DbType.Int16);
		}
		protected override string GetParameterTypeNameForInt32(out byte? precision, out byte? scale) {
			precision = scale = null;
			return nameof(DbType.Int32);
		}
		protected override string GetParameterTypeNameForInt64(out byte? precision, out byte? scale) {
			precision = 20;
			scale = 0;
			return nameof(DbType.Decimal);
		}
		protected override string GetParameterTypeNameForSByte(out byte? precision, out byte? scale) {
			precision = 3;
			scale = 0;
			return nameof(DbType.Decimal);
		}
		protected override string GetParameterTypeNameForSingle(out byte? precision, out byte? scale) {
			precision = scale = null;
			return nameof(DbType.Single);
		}
		protected override string GetParameterTypeNameForString(out int? size) {
			size = 0;
			return nameof(DbType.String);
		}
		protected override string GetParameterTypeNameForTimeSpan() {
			return nameof(DbType.Double);
		}
		protected override string GetParameterTypeNameForUInt16(out byte? precision, out byte? scale) {
			precision = 5;
			scale = 0;
			return nameof(DbType.Decimal);
		}
		protected override string GetParameterTypeNameForUInt32(out byte? precision, out byte? scale) {
			precision = 10;
			scale = 0;
			return nameof(DbType.Decimal);
		}
		protected override string GetParameterTypeNameForUInt64(out byte? precision, out byte? scale) {
			precision = 20;
			scale = 0;
			return nameof(DbType.Decimal);
		}
		protected override string GetParameterTypeNameForDateOnly(out int? size) {
			size = null;
			return nameof(DbType.DateTime);
		}
		protected override string GetParameterTypeNameForTimeOnly(out int? size) {
			size = null;
			return nameof(DbType.Time);
		}
		protected override string ConvertSqlTypeToParameterType(string sqlType) {
			switch(sqlType.ToUpperInvariant()) {
				case "INT":
				case "INTEGER":
					return nameof(DbType.Int32);
				case "TINYINT":
					return nameof(DbType.SByte);
				case "SMALLINT":
					return nameof(DbType.Int16);
				case "MEDIUMINT":
					return nameof(DbType.Int32);
				case "BIGINT":
					return nameof(DbType.Int64);
				case "UNSIGNED BIG INT":
					return nameof(DbType.UInt64);
				case "INT2":
					return nameof(DbType.Int16);
				case "INT8":
					return nameof(DbType.Int64);
				case "CHARACTER":
					return nameof(DbType.StringFixedLength);
				case "VARCHAR":
				case "VARYING CHARACTER":
					return nameof(DbType.String);
				case "NCHAR":
				case "NATIVE CHARACTER":
					return nameof(DbType.StringFixedLength);
				case "NVARCHAR":
				case "TEXT":
				case "CLOB":
					return nameof(DbType.String);
				case "BLOB":
					return nameof(DbType.Binary);
				case "REAL":
				case "DOUBLE":
				case "DOUBLE PRECISION":
					return nameof(DbType.Double);
				case "FLOAT":
					return nameof(DbType.Single);
				case "NUMERIC":
				case "DECIMAL":
				case "MONEY":
					return nameof(DbType.Decimal);
				case "BOOLEAN":
					return nameof(DbType.Boolean);
				case "DATE":
					return nameof(DbType.Date);
				case "DATETIME":
					return nameof(DbType.DateTime);
				default:
					return null;
			}
		}
	}
	class DbTypeMapperSqliteMicrosoft<TSqlDbType, TSqlParameter> : DbTypeMapper<TSqlDbType, TSqlParameter>
		where TSqlDbType : struct
		where TSqlParameter : IDbDataParameter {
		protected override string ParameterDbTypePropertyName { get { return "SqliteType"; } }
		protected override string GetParameterTypeNameForBoolean(out int? size, out byte? precision, out byte? scale) {
			size = null;
			precision = scale = null;
			return "Integer";
		}
		protected override string GetParameterTypeNameForByte(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Real";
		}
		protected override string GetParameterTypeNameForByteArray(out int? size) {
			size = null;
			return "Blob";
		}
		protected override string GetParameterTypeNameForChar(out int? size) {
			size = 1;
			return "Text";
		}
		protected override string GetParameterTypeNameForDateTime() {
			return "Text";
		}
		protected override string GetParameterTypeNameForDecimal(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Real";
		}
		protected override string GetParameterTypeNameForDouble(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Real";
		}
		protected override string GetParameterTypeNameForGuid(out int? size) {
			size = 36;
			return "Text";
		}
		protected override string GetParameterTypeNameForInt16(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Integer";
		}
		protected override string GetParameterTypeNameForInt32(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Integer";
		}
		protected override string GetParameterTypeNameForInt64(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Integer";
		}
		protected override string GetParameterTypeNameForSByte(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Integer";
		}
		protected override string GetParameterTypeNameForSingle(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Real";
		}
		protected override string GetParameterTypeNameForString(out int? size) {
			size = null;
			return "Text";
		}
		protected override string GetParameterTypeNameForTimeSpan() {
			return "Real";
		}
		protected override string GetParameterTypeNameForUInt16(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Integer";
		}
		protected override string GetParameterTypeNameForUInt32(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Integer";
		}
		protected override string GetParameterTypeNameForUInt64(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Integer";
		}
		protected override string GetParameterTypeNameForDateOnly(out int? size) {
			size = null;
			return "Text";
		}
		protected override string GetParameterTypeNameForTimeOnly(out int? size) {
			size = null;
			return "Text";
		}
		protected override string ConvertSqlTypeToParameterType(string sqlType) {
			switch(sqlType.ToUpperInvariant()) {
				case "INT":
				case "INTEGER":
				case "TINYINT":
				case "SMALLINT":
				case "MEDIUMINT":
				case "BIGINT":
				case "UNSIGNED BIG INT":
				case "INT2":
				case "INT8":
				case "BOOLEAN":
					return "Integer";
				case "CHARACTER":
				case "VARCHAR":
				case "VARYING CHARACTER":
				case "NCHAR":
				case "NATIVE CHARACTER":
				case "NVARCHAR":
				case "TEXT":
				case "CLOB":
				case "DATE":
				case "TIME":
				case "DATETIME":
					return "Text";
				case "BLOB":
					return "Blob";
				case "REAL":
				case "DOUBLE":
				case "DOUBLE PRECISION":
				case "FLOAT":
				case "NUMERIC":
				case "DECIMAL":
				case "MONEY":
					return "Real";
				default:
					return null;
			}
		}
	}
}
