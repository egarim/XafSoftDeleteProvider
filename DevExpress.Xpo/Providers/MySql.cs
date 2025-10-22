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
	using System.Reflection;
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
	public class MySqlConnectionProvider : ConnectionProviderSql {
		#region Registration
		public const string XpoProviderTypeString = "MySql";
		public static string GetConnectionString(string server, string userId, string password, string database) {
			return string.Format("{4}={5};server={0};user id={1}; password={2}; database={3};persist security info=true;CharSet=utf8;", EscapeConnectionStringArgument(server), EscapeConnectionStringArgument(userId), EscapeConnectionStringArgument(password), EscapeConnectionStringArgument(database), DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString);
		}
		public static string GetConnectionString(string server, int port, string userId, string password, string database) {
			return string.Concat(GetConnectionString(server, userId, password, database), "Port=", port.ToString(CultureInfo.InvariantCulture), ";");
		}
		static int foundConnectorIndex = -1;
		public static IDbConnection CreateConnection(string connectionString) {
			ConnectionStringParser helper = new ConnectionStringParser(connectionString);
			string assemblyName = helper.GetPartByName("ForcedConnectorAssemblyName");
			if(!string.IsNullOrEmpty(assemblyName)) {
				for(int i = 0; i < connectors.Length; i++) {
					if(connectors[i].AssemblyName == assemblyName) {
						foundConnectorIndex = i;
						break;
					}
				}
				helper.RemovePartByName("ForcedConnectorAssemblyName");
				connectionString = helper.GetConnectionString();
			}
			if(foundConnectorIndex < 0) {
				string[] assemblyNames = new string[connectors.Length];
				string[] connectionTypeNames = new string[connectors.Length];
				for(int i = 0; i < connectors.Length; i++) {
					assemblyNames[i] = connectors[i].AssemblyName;
					connectionTypeNames[i] = connectors[i].ConnectionTypeName;
				}
				IDbConnection connection = ReflectConnectionHelper.GetConnection(assemblyNames, connectionTypeNames, true, ref foundConnectorIndex);
				if(connection != null) {
					connection.ConnectionString = connectionString;
				}
				return connection;
			}
			return ReflectConnectionHelper.GetConnection(connectors[foundConnectorIndex].AssemblyName, connectors[foundConnectorIndex].ConnectionTypeName, connectionString);
		}
		public static IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			IDbConnection connection = CreateConnection(connectionString);
			objectsToDisposeOnDisconnect = new IDisposable[] { connection };
			return CreateProviderFromConnection(connection, autoCreateOption);
		}
		public static IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return new MySqlConnectionProvider(connection, autoCreateOption);
		}
		struct ConnectorInfo {
			public string AssemblyName;
			public string ConnectionTypeName;
			public string ExceptionTypeName;
			public string DbTypeTypeName;
			public string ParameterTypeName;
			public string CommandBuilderTypeName;
		}
		static ConnectorInfo[] connectors;
		static MySqlConnectionProvider() {
			connectors = new ConnectorInfo[] {
				new ConnectorInfo(){
					AssemblyName = "MySqlConnector",
					ConnectionTypeName = "MySqlConnector.MySqlConnection",
					ExceptionTypeName  = "MySqlConnector.MySqlException",
					DbTypeTypeName = "MySqlConnector.MySqlDbType",
					ParameterTypeName = "MySqlConnector.MySqlParameter",
					CommandBuilderTypeName = "MySqlConnector.MySqlCommandBuilder"
				},
				new ConnectorInfo() {
					AssemblyName = "MySql.Data",
					ConnectionTypeName = "MySql.Data.MySqlClient.MySqlConnection",
					ExceptionTypeName = "MySql.Data.MySqlClient.MySqlException",
					DbTypeTypeName = "MySql.Data.MySqlClient.MySqlDbType",
					ParameterTypeName = "MySql.Data.MySqlClient.MySqlParameter",
					CommandBuilderTypeName = "MySql.Data.MySqlClient.MySqlCommandBuilder"
				}
			};
			RegisterDataStoreProvider(XpoProviderTypeString, new DataStoreCreationFromStringDelegate(CreateProviderFromString));
			foreach(var info in connectors) {
				RegisterDataStoreProvider(info.ConnectionTypeName, new DataStoreCreationFromConnectionDelegate(CreateProviderFromConnection));
			}
			RegisterFactory(new MySqlProviderFactory());
		}
		public static void Register() { }
		#endregion
		int? connectorIndex;
		int ConnectionIndex {
			get {
				if(this.connectorIndex.HasValue)
					return this.connectorIndex.Value;
				string assemblyName = Connection.GetType().Assembly.GetName().Name;
				for(int i = 0; i < connectors.Length; i++) {
					if(connectors[i].AssemblyName == assemblyName) {
						connectorIndex = i;
						return i;
					}
				}
				return 0;
			}
		}
		protected string ProviderAssemblyName { get { return connectors[ConnectionIndex].AssemblyName; } }
		protected string ExceptionTypeName { get { return connectors[ConnectionIndex].ExceptionTypeName; } }
		protected string DbTypeTypeName { get { return connectors[ConnectionIndex].DbTypeTypeName; } }
		protected string ParameterTypeName { get { return connectors[ConnectionIndex].ParameterTypeName; } }
		protected string CommandBuilderTypeName { get { return connectors[ConnectionIndex].CommandBuilderTypeName; } }
		ReflectConnectionHelper helper;
		ReflectConnectionHelper ConnectionHelper {
			get {
				if(helper == null) {
					helper = new ReflectConnectionHelper(Connection, ExceptionTypeName);
				}
				return helper;
			}
		}
		DbTypeMapperBase dbTypeMapper;
		protected override DbTypeMapperBase DbTypeMapper {
			get {
				if(dbTypeMapper == null) {
					Type mySqlParameterType = ConnectionHelper.GetType(ParameterTypeName);
					Type mySqlDbTypeType = ConnectionHelper.GetType(DbTypeTypeName);
					bool isSupport80 = SupportVersion(8, 0);
					dbTypeMapper = (DbTypeMapperBase)Activator.CreateInstance(typeof(DbTypeMapperMySql<,>).MakeGenericType(mySqlDbTypeType, mySqlParameterType), new object[] { isSupport80 });
				}
				return dbTypeMapper;
			}
		}
		UpdateSchemaSqlFormatterHelper updateSchemaSqlFormatter;
		protected override UpdateSchemaSqlFormatterHelper UpdateSchemaFormatter {
			get {
				if(updateSchemaSqlFormatter == null) {
					updateSchemaSqlFormatter = new MySqlUpdateSchemaSqlFormatterHelper(this, GetSqlCreateColumnFullAttributes, FormatConstraintSafe, GetIndexName, GetForeignKeyName, GetPrimaryKeyName);
				}
				return updateSchemaSqlFormatter;
			}
		}
		protected override DBSchemaComparerSql CreateSchemaComparer() {
			var comparer = base.CreateSchemaComparer();
			comparer.AddCompatibleSqlTypeMapping("bit", "bit(1)");
			comparer.AddCompatibleSqlTypeMapping("tinyint unsigned", "tinyint(3) unsigned");
			comparer.AddCompatibleSqlTypeMapping("tinyint", "tinyint(4)");
			comparer.AddCompatibleSqlTypeMapping("real", "double");
			comparer.AddCompatibleSqlTypeMapping("int", "int(11)");
			comparer.AddCompatibleSqlTypeMapping("char", "char(1)");
			comparer.AddCompatibleSqlTypeMapping("smallint", "smallint(6)");
			comparer.AddCompatibleSqlTypeMapping("bigint", "bigint(20)");
			comparer.AddCompatibleSqlTypeMapping("smallint unsigned", "smallint(5) unsigned");
			comparer.AddCompatibleSqlTypeMapping("int unsigned", "int(10) unsigned");
			comparer.AddCompatibleSqlTypeMapping("bigint unsigned", "bigint(20) unsigned");
			return comparer;
		}
		protected virtual void PrepareDelegates() { }
		public MySqlConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption)
			: base(connection, autoCreateOption, true) {
			ReadDbVersion(connection);
		}
		protected MySqlConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption, bool openConnection)
			: base(connection, autoCreateOption, openConnection) {
		}
		protected override string GetSqlCreateColumnTypeForBoolean(DBTable table, DBColumn column) {
			return "bit";
		}
		protected override string GetSqlCreateColumnTypeForByte(DBTable table, DBColumn column) {
			return "tinyint unsigned";
		}
		protected override string GetSqlCreateColumnTypeForSByte(DBTable table, DBColumn column) {
			return "tinyint";
		}
		protected override string GetSqlCreateColumnTypeForChar(DBTable table, DBColumn column) {
			return "char";
		}
		protected override string GetSqlCreateColumnTypeForDecimal(DBTable table, DBColumn column) {
			return "decimal(28,8)";
		}
		protected override string GetSqlCreateColumnTypeForDouble(DBTable table, DBColumn column) {
			return "double";
		}
		protected override string GetSqlCreateColumnTypeForSingle(DBTable table, DBColumn column) {
			return "real";
		}
		protected override string GetSqlCreateColumnTypeForInt32(DBTable table, DBColumn column) {
			return "int";
		}
		protected override string GetSqlCreateColumnTypeForUInt32(DBTable table, DBColumn column) {
			return "int unsigned";
		}
		protected override string GetSqlCreateColumnTypeForInt16(DBTable table, DBColumn column) {
			return "smallint";
		}
		protected override string GetSqlCreateColumnTypeForUInt16(DBTable table, DBColumn column) {
			return "smallint unsigned";
		}
		protected override string GetSqlCreateColumnTypeForInt64(DBTable table, DBColumn column) {
			return "bigint";
		}
		protected override string GetSqlCreateColumnTypeForUInt64(DBTable table, DBColumn column) {
			return "bigint unsigned";
		}
		public const int MaximumStringSize = 255;
		protected override string GetSqlCreateColumnTypeForString(DBTable table, DBColumn column) {
			if(column.Size < 0 || column.Size > 16777215)
				return "LONGTEXT";
			if(column.Size > 65535)
				return "MEDIUMTEXT";
			if(column.Size > 255)
				return "TEXT";
			return "varchar(" + column.Size.ToString(CultureInfo.InvariantCulture) + ')';
		}
		string DateTimeColumnSqlType {
			get {
				return SupportVersion(8, 0) ? "datetime(6)" : "datetime";
			}
		}
		string TimeColumnSqlType {
			get => SupportVersion(8, 0) ? "time(6)" : "time";
		}
		protected override string GetSqlCreateColumnTypeForDateTime(DBTable table, DBColumn column) {
			return DateTimeColumnSqlType;
		}
		protected override string GetSqlCreateColumnTypeForDateOnly(DBTable table, DBColumn column) {
			return "date";
		}
		protected override string GetSqlCreateColumnTypeForTimeOnly(DBTable table, DBColumn column) {
			return TimeColumnSqlType;
		}
		protected override string GetSqlCreateColumnTypeForGuid(DBTable table, DBColumn column) {
			return "char(38)";
		}
		protected override string GetSqlCreateColumnTypeForByteArray(DBTable table, DBColumn column) {
			if(column.Size <= 0 || column.Size > 16777215)
				return "LONGBLOB";
			if(column.Size > 65535)
				return "MEDIUMBLOB";
			if(column.Size > 127)
				return "BLOB";
			return "TINYBLOB";
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
				result += " NOT NULL";
			}
			else {
				result += " NULL";
			}
			if(!column.IsIdentity) {
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
			if(column.IsKey && column.IsIdentity && (column.ColumnType == DBColumnType.Int32 || column.ColumnType == DBColumnType.Int64) && IsSingleColumnPKColumn(table, column)) {
				result += " AUTO_INCREMENT PRIMARY KEY";
			}
			return result;
		}
		protected override Int64 GetIdentity(Query sql) {
			object value = GetScalar(new Query(sql.Sql + ";\nselect last_insert_id()", sql.Parameters, sql.ParametersNames));
			return (value as IConvertible).ToInt64(CultureInfo.InvariantCulture);
		}
		protected override async Task<Int64> GetIdentityAsync(Query sql, AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken) {
			object value = await GetScalarAsync(new Query(sql.Sql + ";\nselect last_insert_id()", sql.Parameters, sql.ParametersNames), asyncOperationId, cancellationToken).ConfigureAwait(false);
			return (value as IConvertible).ToInt64(CultureInfo.InvariantCulture);
		}
		MethodInfo miServerVersion;
		protected void ReadDbVersion(IDbConnection conn) {
			if(miServerVersion == null) {
				Type connType = conn.GetType();
				PropertyInfo pi = connType.GetProperty("ServerVersion", BindingFlags.Instance | BindingFlags.Public);
				if(pi != null && pi.CanRead) {
					miServerVersion = pi.GetGetMethod();
				}
			}
			if(miServerVersion != null) {
				string versionString = (string)miServerVersion.Invoke(conn, Array.Empty<object>());
				SetServerVersionInternal(versionString);
			}
			else {
				versionMajor = 5.0m;
				versionMinor = 0;
			}
		}
		decimal? versionMajor;
		int versionMinor;
		bool SetServerVersionInternal(string versionString) {
			string[] versionParts = versionString.Split('.');
			decimal versionMajorLocal;
			if(versionParts.Length >= 3 && decimal.TryParse(string.Concat(versionParts[0], ".", versionParts[1]), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out versionMajorLocal)) {
				versionMajor = versionMajorLocal;
				string versionMinorString = versionParts[2];
				int p = versionMinorString.IndexOf('-');
				if(p != -1) {
					versionMinorString = versionMinorString.Substring(0, p);
				}
				Int32.TryParse(versionMinorString, NumberStyles.Integer, CultureInfo.InvariantCulture, out versionMinor);
				return true;
			}
			return false;
		}
		protected bool SupportVersion(decimal major, int minor) {
			if(!versionMajor.HasValue)
				return true;
			if(versionMajor.Value > major)
				return true;
			if(versionMajor.Value == major && versionMinor >= minor)
				return true;
			return false;
		}
		protected override Exception WrapException(Exception e, IDbCommand query) {
			object o;
			if(ConnectionHelper.TryGetExceptionProperty(e, "Number", out o)) {
				int number = (int)o;
				if(number == 0x41e || number == 0x47a)
					return new SchemaCorrectionNeededException(e);
				if(number == 0x5ab || number == 0x426)
					return new ConstraintViolationException(query.CommandText, GetParametersString(query), e);
			}
			return base.WrapException(e, query);
		}
		protected override IDbConnection CreateConnection() {
			return ConnectionHelper.GetConnection(ConnectionString);
		}
		protected override void CreateDataBase() {
			try {
				Connection.Open();
			}
			catch(Exception e) {
				object o;
				bool gotDatabaseNotFoundException = false;
				Exception currentException = e;
				while(currentException != null) {
					if(ConnectionHelper.TryGetExceptionProperty(currentException, "Number", out o) && ((int)o) == 1049) {
						gotDatabaseNotFoundException = true;
						break;
					}
					currentException = currentException.InnerException;
				}
				ConnectionStringParser helper = new ConnectionStringParser(ConnectionString);
				if(gotDatabaseNotFoundException && CanCreateDatabase) {
					string dbName = helper.GetPartByName("database");
					helper.RemovePartByName("database");
					string connectToServer = helper.GetConnectionString();
					using(IDbConnection conn = ConnectionHelper.GetConnection(connectToServer)) {
						conn.Open();
						using(IDbCommand c = conn.CreateCommand()) {
							c.CommandText = "Create Database " + dbName;
							c.ExecuteNonQuery();
						}
					}
					Connection.Open();
				}
				else {
					throw new UnableToOpenDatabaseException(XpoDefault.ConnectionStringRemovePassword(helper), e);
				}
			}
		}
		DBColumnType GetTypeFromString(string typeName, out int size) {
			size = 0;
			if(string.IsNullOrEmpty(typeName))
				return DBColumnType.Unknown;
			switch(typeName) {
				case "char(1)":
					return DBColumnType.Char;
				case "bit(1)":
				case "tinyint(1)":
					return DBColumnType.Boolean;
				case "time(6)":
					return DBColumnType.TimeOnly;
				default:
					break;
			}
			string typeWithoutBrackets = RemoveBrackets(typeName);
			switch(typeWithoutBrackets.ToLowerInvariant()) {
				case "int":
					return DBColumnType.Int32;
				case "int unsigned":
					return DBColumnType.UInt32;
				case "longblob":
				case "tinyblob":
				case "mediumblob":
				case "blob":
					return DBColumnType.ByteArray;
				case "text":
				case "mediumtext":
				case "longtext":
					return DBColumnType.String;
				case "bit":
				case "tinyint unsigned":
					return DBColumnType.Byte;
				case "tinyint":
					return DBColumnType.SByte;
				case "smallint":
					return DBColumnType.Int16;
				case "smallint unsigned":
					return DBColumnType.UInt16;
				case "bigint":
					return DBColumnType.Int64;
				case "bigint unsigned":
					return DBColumnType.UInt64;
				case "double":
					return DBColumnType.Double;
				case "float":
					return DBColumnType.Single;
				case "datetime":
					return DBColumnType.DateTime;
				case "date":
					if(ConnectionProviderSql.GlobalUseLegacyDateOnlyAndTimeOnlySupport)
						return DBColumnType.DateTime;
					else
						return DBColumnType.DateOnly;
				case "time":
					return DBColumnType.TimeOnly;
				case "decimal":
					return DBColumnType.Decimal;
				default:
					break;
			}
			if(typeName.StartsWith("char(")) {
				size = Int32.Parse(typeName.Substring(5, typeName.Length - 6));
				return DBColumnType.String;
			}
			if(typeName.StartsWith("varchar(")) {
				size = Int32.Parse(typeName.Substring(8, typeName.Length - 9));
				return DBColumnType.String;
			}
			return DBColumnType.Unknown;
		}
		static string RemoveBrackets(string typeName) {
			string typeWithoutBrackets = typeName;
			int bracketOpen = typeName.IndexOf('(');
			if(bracketOpen >= 0) {
				int bracketClose = typeName.IndexOf(')', bracketOpen);
				if(bracketClose >= 0) {
					typeWithoutBrackets = typeName.Remove(bracketOpen, bracketClose - bracketOpen + 1);
				}
			}
			return typeWithoutBrackets;
		}
		void GetColumns(DBTable table) {
			foreach(SelectStatementResultRow row in SelectData(new Query(string.Format(CultureInfo.InvariantCulture, "show columns from `{0}`", ComposeSafeTableName(table.Name)))).Rows) {
				int size;
				string typeName, rowValue2, rowValue4, rowValue5, rowValue0 = string.Empty;
				if(row.Values[1].GetType() == typeof(System.Byte[])) {
					typeName = DXEncoding.Default.GetString((byte[])row.Values[1]);
					rowValue2 = DXEncoding.Default.GetString((byte[])row.Values[2]);
					rowValue4 = DXEncoding.Default.GetString((byte[])row.Values[4]);
					rowValue5 = DXEncoding.Default.GetString((byte[])row.Values[5]);
					rowValue0 = DXEncoding.Default.GetString((byte[])row.Values[0]);
				}
				else {
					typeName = (string)row.Values[1];
					rowValue2 = (string)row.Values[2];
					rowValue4 = (row.Values[4] != System.DBNull.Value) ? row.Values[4].ToString() : null;
					rowValue5 = (string)row.Values[5];
					rowValue0 = (string)row.Values[0];
				}
				bool isNullable = (rowValue2 == "YES");
				DBColumnType type = GetTypeFromString(typeName, out size);
				bool isAutoIncrement = false;
				string extraValue = rowValue5;
				if(!string.IsNullOrEmpty(extraValue) && extraValue.Contains("auto_increment")) {
					isAutoIncrement = true;
				}
				object defaultValue = null;
				string dbDefaultValue = rowValue4;
				if(dbDefaultValue != null) {
					if(type == DBColumnType.Boolean) {
						if(dbDefaultValue != string.Empty) {
							defaultValue = (dbDefaultValue == "\u0001" || dbDefaultValue == "b'1'");
						}
					}
					else {
						if(dbDefaultValue != string.Empty) {
							ReformatReadValueArgs refmtArgs = new ReformatReadValueArgs(DBColumn.GetType(type));
							refmtArgs.AttachValueReadFromDb(dbDefaultValue);
							try {
								defaultValue = ReformatReadValue(dbDefaultValue, refmtArgs);
							}
							catch {
								defaultValue = null;
							}
						}
						else {
							if(type == DBColumnType.Char || type == DBColumnType.String) {
								if(isNullable) {
									defaultValue = dbDefaultValue;
								}
							}
						}
					}
				}
				DBColumn column = new DBColumn(rowValue0, false, typeName ?? string.Empty, type == DBColumnType.String ? size : 0, type, isNullable, defaultValue);
				column.IsIdentity = isAutoIncrement;
				table.AddColumn(column);
			}
		}
		void GetPrimaryKey(DBTable table) {
			SelectStatementResult data = SelectData(new Query(string.Format(CultureInfo.InvariantCulture, "show index from `{0}`", ComposeSafeTableName(table.Name))));
			if(data.Rows.Length > 0) {
				StringCollection cols = new StringCollection();
				for(int i = 0; i < data.Rows.Length; i++) {
					object[] topRow = data.Rows[i].Values;
					string topRow2, topRow4 = string.Empty;
					if(topRow[2].GetType() == typeof(System.Byte[])) {
						topRow2 = DXEncoding.Default.GetString((byte[])topRow[2]);
						topRow4 = DXEncoding.Default.GetString((byte[])topRow[4]);
					}
					else {
						topRow2 = (string)topRow[2];
						topRow4 = (string)topRow[4];
					}
					if(topRow2 == "PRIMARY") {
						DBColumn column = table.GetColumn(topRow4);
						if(column != null)
							column.IsKey = true;
						cols.Add(topRow4);
					}
				}
				if(cols.Count > 0) {
					table.PrimaryKey = new DBPrimaryKey(cols);
				}
			}
		}
		void GetIndexes(DBTable table) {
			SelectStatementResult data = SelectData(new Query(string.Format(CultureInfo.InvariantCulture, "show index from `{0}`", ComposeSafeTableName(table.Name))));
			DBIndex index = null;
			foreach(SelectStatementResultRow row in data.Rows) {
				string rowValues2, rowValues4 = string.Empty;
				int nonUnique = Convert.ToInt32(row.Values[1]);
				if(row.Values[2].GetType() == typeof(System.Byte[])) {
					rowValues2 = DXEncoding.Default.GetString((byte[])row.Values[2]);
					rowValues4 = DXEncoding.Default.GetString((byte[])row.Values[4]);
				}
				else {
					rowValues2 = (string)row.Values[2];
					rowValues4 = (string)row.Values[4];
				}
				if(index == null || index.Name != rowValues2) {
					StringCollection list = new StringCollection();
					list.Add(rowValues4);
					index = new DBIndex(rowValues2, list, nonUnique == 0);
					table.Indexes.Add(index);
				}
				else
					index.Columns.Add(rowValues4);
			}
		}
		void GetForeignKeys(DBTable table) {
			SelectStatementResult data = SelectData(new Query(string.Format(CultureInfo.InvariantCulture, "show create table `{0}`", ComposeSafeTableName(table.Name))));
			if(data.Rows.Length > 0) {
				object val = data.Rows[0].Values[1];
				string s = val as string;
				if(s == null)
					s = DXEncoding.Default.GetString((byte[])val);
				int pos = 0;
				do {
					pos = s.IndexOf("CONSTRAINT", pos + 1);
					if(pos == -1)
						break;
					int colsIndex = s.IndexOf("FOREIGN KEY", pos);
					int refsIndex = s.IndexOf("REFERENCES", pos);
					if(colsIndex < 0 || refsIndex < 0)
						break;
					int primesIndex = s.IndexOf('(', refsIndex);
					int primesEndIndex = s.IndexOf(')', primesIndex);
					if(primesIndex < 0 || primesEndIndex < 0)
						break;
					string refTable = s.Substring(refsIndex + 12, primesIndex - 12 - refsIndex).Trim('`', ' ');
					string refs = s.Substring(colsIndex + 12, refsIndex - 12 - colsIndex).Trim('`', ' ', '(', ')');
					StringCollection cols = new StringCollection();
					foreach(string col in refs.Split(','))
						cols.Add(col.Trim('`', ' '));
					refs = s.Substring(primesIndex, primesEndIndex - primesIndex).Trim(' ', '(', ')');
					StringCollection fcols = new StringCollection();
					foreach(string col in refs.Split(','))
						fcols.Add(col.Trim('`', ' '));
					string name = s.Substring(pos + 11, colsIndex - pos - 12).Trim('`', ' ');
					table.AddForeignKey(new DBForeignKey(cols, refTable, fcols) { Name = name });
				} while(true);
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
			if(SupportVersion(5, 0)) {
				Hashtable dbTables = new Hashtable();
				foreach(SelectStatementResultRow row in SelectData(new Query(string.Format(CultureInfo.InvariantCulture, "select TABLE_NAME, TABLE_TYPE from INFORMATION_SCHEMA.TABLES where table_schema = '{0}' and TABLE_TYPE in ('BASE TABLE', 'VIEW')", Connection.Database))).Rows) {
					string rowValues0, rowValues1 = string.Empty;
					if(row.Values[0].GetType() == typeof(System.Byte[])) {
						rowValues0 = DXEncoding.Default.GetString((byte[])row.Values[0]);
						rowValues1 = DXEncoding.Default.GetString((byte[])row.Values[1]);
					}
					else {
						rowValues0 = (string)row.Values[0];
						rowValues1 = (string)row.Values[1];
					}
					dbTables.Add(rowValues0.ToLower(), rowValues1 == "VIEW");
				}
				ArrayList list = new ArrayList();
				foreach(DBTable table in tables) {
					object o = dbTables[ComposeSafeTableName(table.Name).ToLower()];
					if(o == null)
						list.Add(table);
					else
						table.IsView = (bool)o;
				}
				return list;
			}
			else {
				Hashtable dbTables = new Hashtable();
				foreach(SelectStatementResultRow row in SelectData(new Query(string.Format(CultureInfo.InvariantCulture, "show tables from `{0}`", Connection.Database))).Rows) {
					string rowValues0, rowValues1 = string.Empty;
					if(row.Values[0].GetType() == typeof(System.Byte[])) {
						rowValues0 = DXEncoding.Default.GetString((byte[])row.Values[0]);
					}
					else {
						rowValues0 = (string)row.Values[0];
					}
					dbTables.Add(rowValues0.ToLower(), null);
				}
				ArrayList list = new ArrayList();
				foreach(DBTable table in tables)
					if(!dbTables.Contains(ComposeSafeTableName(table.Name).ToLower()))
						list.Add(table);
				return list;
			}
		}
		ExecMethodDelegate commandBuilderDeriveParametersHandler;
		protected override void CommandBuilderDeriveParameters(IDbCommand command) {
			if(this.ProviderAssemblyName == "MySqlConnector") {
				Guard.ArgumentNotNull(command, nameof(command));
				Guard.ArgumentMatch(command.CommandType, nameof(command.CommandType), v => v == CommandType.StoredProcedure);
				Guard.ArgumentIsNotNullOrEmpty(command.CommandText, nameof(command.CommandText));
				Guard.ArgumentMatch(command.Connection.State, nameof(command.Connection.State), v => v == ConnectionState.Open);
				if(SupportVersion(5.5m, 3)) {
					command.Parameters.Clear();
					DeriveParametersCore(command);
				}
				else {
					throw new NotSupportedException("The database server doesn't support INFORMATION_SCHEMA.Parameters.");
				}
				return;
			}
			if(commandBuilderDeriveParametersHandler == null) {
				commandBuilderDeriveParametersHandler = ReflectConnectionHelper.GetCommandBuilderDeriveParametersDelegate(ProviderAssemblyName, CommandBuilderTypeName);
			}
			commandBuilderDeriveParametersHandler(command);
		}
		public override DBStoredProcedure[] GetStoredProcedures() {
			List<DBStoredProcedure> result = new List<DBStoredProcedure>();
			using(var command = Connection.CreateCommand()) {
				if(SupportVersion(8, 0)) {
					command.CommandText = @"select routine_name from information_schema.routines where routine_schema = database()";
				}
				else {
					command.CommandText = @"select name from mysql.proc where db = database()";
				}
				using(IDataReader reader = command.ExecuteReader()) {
					while(reader.Read()) {
						DBStoredProcedure curProc = new DBStoredProcedure();
						curProc.Name = reader.GetString(0);
						result.Add(curProc);
					}
				}
			}
			foreach(DBStoredProcedure sproc in result) {
				using(var command = Connection.CreateCommand()) {
					try {
						command.CommandType = CommandType.StoredProcedure;
						command.CommandText = FormatTable(ComposeSafeSchemaName(sproc.Name), ComposeSafeTableName(sproc.Name));
						CommandBuilderDeriveParameters(command);
						List<string> fakeParams = new List<string>();
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
							if(columnType == DBColumnType.Unknown) {
								if(((IDbTypeMapperMySql)DbTypeMapper).IsByteArraySqlDbType(parameter)) {
									columnType = DBColumnType.ByteArray;
								}
							}
							else if(columnType == DBColumnType.UInt64) {
								if(((IDbTypeMapperMySql)DbTypeMapper).IsBoolSqlDbType(parameter)) {
									int size = ((IDbDataParameter)parameter).Size;
									if(size == 0 || size == 1) {
										columnType = DBColumnType.Boolean;
									}
								}
							}
							dbArguments.Add(new DBStoredProcedureArgument(parameter.ParameterName, columnType, direction));
						}
						sproc.Arguments.AddRange(dbArguments);
					}
					catch(Exception) {
						throw;
					}
				}
			}
			return result.ToArray();
		}
		protected override int GetSafeNameTableMaxLength() {
			return 64;
		}
		protected override int GetObjectNameEffectiveLength(string objectName) {
			return Encoding.UTF8.GetByteCount(objectName);
		}
		public override string FormatTable(string schema, string tableName) {
			return string.Format(CultureInfo.InvariantCulture, "`{0}`", tableName);
		}
		public override string FormatTable(string schema, string tableName, string tableAlias) {
			return string.Format(CultureInfo.InvariantCulture, "`{0}` {1}", tableName, tableAlias);
		}
		public override string FormatColumn(string columnName) {
			return string.Format(CultureInfo.InvariantCulture, "`{0}`", columnName);
		}
		public override string FormatColumn(string columnName, string tableAlias) {
			return string.Format(CultureInfo.InvariantCulture, "{1}.`{0}`", columnName, tableAlias);
		}
		public override string FormatSelect(string selectedPropertiesSql, string fromSql, string whereSql, string orderBySql, string groupBySql, string havingSql, int skipSelectedRecords, int topSelectedRecords) {
			base.FormatSelect(selectedPropertiesSql, fromSql, whereSql, orderBySql, groupBySql, havingSql, skipSelectedRecords, topSelectedRecords);
			string modificatorsSql = string.Empty;
			if(skipSelectedRecords == 0) {
				modificatorsSql = string.Format(CultureInfo.InvariantCulture, (topSelectedRecords != 0) ? "limit {0} " : string.Empty, topSelectedRecords);
			}
			else {
				int topSelectedRecordsValue = topSelectedRecords == 0 ? int.MaxValue : topSelectedRecords;
				modificatorsSql = string.Format(CultureInfo.InvariantCulture, "limit {0}, {1} ", skipSelectedRecords, topSelectedRecordsValue);
			}
			string expandedWhereSql = whereSql != null ? string.Format(CultureInfo.InvariantCulture, "{0}where {1}", Environment.NewLine, whereSql) : string.Empty;
			string expandedOrderBySql = orderBySql != null ? string.Format(CultureInfo.InvariantCulture, "{0}order by {1}", Environment.NewLine, orderBySql) : string.Empty;
			string expandedHavingSql = havingSql != null ? string.Format(CultureInfo.InvariantCulture, "{0}having {1}", Environment.NewLine, havingSql) : string.Empty;
			string expandedGroupBySql = groupBySql != null ? string.Format(CultureInfo.InvariantCulture, "{0}group by {1}", Environment.NewLine, groupBySql) : string.Empty;
			return string.Format(CultureInfo.InvariantCulture, "select {1} from {2}{3}{4}{5}{6} {0}", modificatorsSql, selectedPropertiesSql, fromSql, expandedWhereSql, expandedGroupBySql, expandedHavingSql, expandedOrderBySql);
		}
		public override bool NativeSkipTakeSupported { get { return true; } }
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
		static string FormatMod(string arg, int multiplier, int divider) {
			return string.Format("(Truncate(Cast({0} as decimal(65,30)) * {1}, 0) % {2})", arg, multiplier, divider);
		}
		static string FormatGetInt(string arg, int multiplier, int divider) {
			return string.Format("(Cast({0} as decimal(65,30)) * {1} Div {2})", arg, multiplier, divider);
		}
		string FnAddDateTime(string datetimeOperand, string dayPart, string secondPart) {
			return string.Format(CultureInfo.InvariantCulture, "cast(AddDate(AddDate({0}, interval {1} day), interval {2} second) as {3})", datetimeOperand, dayPart, secondPart, DateTimeColumnSqlType);
		}
		string FnAddTime(string timeOperand, string secondPart) {
			string addSeconds = string.Format(CultureInfo.InvariantCulture, "TIME_TO_SEC({0}) + {1}", timeOperand, secondPart);
			string truncateToDay = string.Format("cast(SEC_TO_TIME((({0}) % 86400 + 86400) % 86400) as {1})", addSeconds, TimeColumnSqlType);
			return truncateToDay;
		}
		public override string FormatFunction(FunctionOperatorType operatorType, params string[] operands) {
			switch(operatorType) {
				case FunctionOperatorType.Concat:
					string args = string.Empty;
					for(int i = 0; i < operands.Length; i++) {
						if(operands[i].Length > 0)
							args += i == operands.Length - 1 ? string.Format(CultureInfo.InvariantCulture, "{0}", operands[i]) : string.Format(CultureInfo.InvariantCulture, "{0}, ", operands[i]);
					}
					return string.Format(CultureInfo.InvariantCulture, "CONCAT({0})", args);
				case FunctionOperatorType.Len:
					return string.Format(CultureInfo.InvariantCulture, "LENGTH({0})", operands[0]);
				case FunctionOperatorType.Substring:
					return operands.Length < 3 ? string.Format(CultureInfo.InvariantCulture, "SUBSTR({0},{1} + 1)", operands[0], operands[1]) : string.Format(CultureInfo.InvariantCulture, "SUBSTR({0}, {1} + 1, {2})", operands[0], operands[1], operands[2]);
				case FunctionOperatorType.Trim:
					return string.Format(CultureInfo.InvariantCulture, "Trim({0})", operands[0]);
				case FunctionOperatorType.Upper:
					return string.Format(CultureInfo.InvariantCulture, "Upper({0})", operands[0]);
				case FunctionOperatorType.Lower:
					return string.Format(CultureInfo.InvariantCulture, "Lower({0})", operands[0]);
				case FunctionOperatorType.Ascii:
					return string.Format(CultureInfo.InvariantCulture, "Ascii({0})", operands[0]);
				case FunctionOperatorType.Char:
					return string.Format(CultureInfo.InvariantCulture, "cast(Char({0}) as char(1))", operands[0]);
				case FunctionOperatorType.ToInt:
					return string.Format(CultureInfo.InvariantCulture, "Cast({0} as signed)", operands[0]);
				case FunctionOperatorType.ToLong:
					return string.Format(CultureInfo.InvariantCulture, "Cast({0} as decimal(20, 0))", operands[0]);
				case FunctionOperatorType.ToFloat:
					throw new NotSupportedException();
				case FunctionOperatorType.ToDouble:
					throw new NotSupportedException();
				case FunctionOperatorType.ToDecimal:
					return string.Format(CultureInfo.InvariantCulture, "Cast({0} as decimal(65,30))", operands[0]);
				case FunctionOperatorType.ToStr:
					return string.Format(CultureInfo.InvariantCulture, "Cast({0} as char)", operands[0]);
				case FunctionOperatorType.Replace:
					return string.Format(CultureInfo.InvariantCulture, "Replace({0},{1},{2})", operands[0], operands[1], operands[2]);
				case FunctionOperatorType.PadLeft:
					switch(operands.Length) {
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "If(Length({0}) >= {1}, {0}, LPad({0}, {1}, ' '))", operands[0], operands[1]);
						case 3:
							return string.Format(CultureInfo.InvariantCulture, "If(Length({0}) >= {1}, {0}, LPad({0}, {1}, {2}))", operands[0], operands[1], operands[2]);
					}
					goto default;
				case FunctionOperatorType.PadRight:
					switch(operands.Length) {
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "If(Length({0}) >= {1}, {0},RPad({0}, {1}, ' '))", operands[0], operands[1]);
						case 3:
							return string.Format(CultureInfo.InvariantCulture, "If(Length({0}) >= {1}, {0},RPad({0}, {1}, {2}))", operands[0], operands[1], operands[2]);
					}
					goto default;
				case FunctionOperatorType.Reverse:
					return string.Format(CultureInfo.InvariantCulture, "Reverse({0})", operands[0]);
				case FunctionOperatorType.Insert:
					return string.Format(CultureInfo.InvariantCulture, "Concat(Left({0},{1}),{2},Right({0},Length({0})-({1})))", operands[0], operands[1], operands[2]);
				case FunctionOperatorType.CharIndex:
					switch(operands.Length) {
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "(Instr({1},{0})-1)", operands[0], operands[1]);
						case 3:
							return string.Format(CultureInfo.InvariantCulture, "( if( Instr(Right({1},(Length({1}) - {2})),{0}) = 0, -1 , Instr(Right({1},(Length({1}) - {2})),{0}) -1 + {2}) )", operands[0], operands[1], operands[2]);
						case 4:
							return string.Format(CultureInfo.InvariantCulture, "(if( instr(Left( Right({1},(Length({1}) - {2} ) ) ,{3}),{0}) = 0, -1, instr(Left( Right({1},(Length({1}) - {2} ) ) ,{3}),{0}) - 1 + {2}))", operands[0], operands[1], operands[2], operands[3]);
					}
					goto default;
				case FunctionOperatorType.Remove:
					switch(operands.Length) {
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "Left({0},{1})", operands[0], operands[1]);
						case 3:
							return string.Format(CultureInfo.InvariantCulture, "Concat(Left({0},{1}),Right({0},Length({0})-{1}-{2}))", operands[0], operands[1], operands[2]);
					}
					goto default;
				case FunctionOperatorType.Abs:
					return string.Format(CultureInfo.InvariantCulture, "Abs({0})", operands[0]);
				case FunctionOperatorType.BigMul:
					return string.Format(CultureInfo.InvariantCulture, "(cast({0} as signed int) * cast({1} as signed int))", operands[0], operands[1]);
				case FunctionOperatorType.Sqr:
					return string.Format(CultureInfo.InvariantCulture, "Sqrt({0})", operands[0]);
				case FunctionOperatorType.Sinh:
					return string.Format(CultureInfo.InvariantCulture, "( (Exp({0}) - Exp(({0} * (-1) ))) / 2 )", operands[0]);
				case FunctionOperatorType.Cosh:
					return string.Format(CultureInfo.InvariantCulture, "( (Exp({0}) + Exp(({0} * (-1) ))) / 2 )", operands[0]);
				case FunctionOperatorType.Tanh:
					return string.Format(CultureInfo.InvariantCulture, "( (Exp({0}) - Exp(({0} * (-1) ))) / (Exp({0}) + Exp(({0} * (-1) ))) )", operands[0]);
				case FunctionOperatorType.Rnd:
					return "Rand()";
				case FunctionOperatorType.Log:
					switch(operands.Length) {
						case 1:
							return string.Format(CultureInfo.InvariantCulture, "Log({0})", operands[0]);
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "Log({1},{0})", operands[0], operands[1]);
					}
					goto default;
				case FunctionOperatorType.Log10:
					return string.Format(CultureInfo.InvariantCulture, "Log10({0})", operands[0]);
				case FunctionOperatorType.Sin:
					return string.Format(CultureInfo.InvariantCulture, "Sin({0})", operands[0]);
				case FunctionOperatorType.Asin:
					return string.Format(CultureInfo.InvariantCulture, "Asin({0})", operands[0]);
				case FunctionOperatorType.Tan:
					return string.Format(CultureInfo.InvariantCulture, "Tan({0})", operands[0]);
				case FunctionOperatorType.Atn:
					return string.Format(CultureInfo.InvariantCulture, "Atan({0})", operands[0]);
				case FunctionOperatorType.Atn2:
					return string.Format(CultureInfo.InvariantCulture, "Atan2({0},{1})", operands[0], operands[1]);
				case FunctionOperatorType.Cos:
					return string.Format(CultureInfo.InvariantCulture, "Cos({0})", operands[0]);
				case FunctionOperatorType.Acos:
					return string.Format(CultureInfo.InvariantCulture, "Acos({0})", operands[0]);
				case FunctionOperatorType.Exp:
					return string.Format(CultureInfo.InvariantCulture, "Exp({0})", operands[0]);
				case FunctionOperatorType.Power:
					return string.Format(CultureInfo.InvariantCulture, "Power({0},{1})", operands[0], operands[1]);
				case FunctionOperatorType.Iif: {
					if((operands.Length % 2) == 0)
						throw new ArgumentException(Res.GetString(Res.Filtering_TheIifFunctionOperatorRequiresThree));
					if(operands.Length == 1)
						return string.Format(CultureInfo.InvariantCulture, "({0})", operands[0]);
					if(operands.Length == 3)
						return string.Format(CultureInfo.InvariantCulture, "If({0}, {1}, {2})", operands[0], operands[1], operands[2]);
					StringBuilder sb = new StringBuilder();
					int index = -2;
					int counter = 0;
					do {
						index += 2;
						sb.AppendFormat("If({0}, {1}, ", operands[index], operands[index + 1]);
						counter++;
					} while((index + 3) < operands.Length);
					sb.AppendFormat("{0}", operands[index + 2]);
					sb.Append(new string(')', counter));
					return sb.ToString();
				}
				case FunctionOperatorType.Max:
					return string.Format(CultureInfo.InvariantCulture, "if({0} > {1}, {0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.Min:
					return string.Format(CultureInfo.InvariantCulture, "if({0} < {1}, {0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.Round:
					switch(operands.Length) {
						case 1:
							return string.Format(CultureInfo.InvariantCulture, "Round({0},0)", operands[0]);
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "Round({0},{1})", operands[0], operands[1]);
					}
					goto default;
				case FunctionOperatorType.Sign:
					return string.Format(CultureInfo.InvariantCulture, "Sign({0})", operands[0]);
				case FunctionOperatorType.Floor:
					return string.Format(CultureInfo.InvariantCulture, "Floor({0})", operands[0]);
				case FunctionOperatorType.Ceiling:
					return string.Format(CultureInfo.InvariantCulture, "Ceiling({0})", operands[0]);
				case FunctionOperatorType.IsNull:
					switch(operands.Length) {
						case 1:
							return string.Format(CultureInfo.InvariantCulture, "({0} is null)", operands[0]);
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "Coalesce({0},{1})", operands[0], operands[1]);
					}
					goto default;
				case FunctionOperatorType.IsNullOrEmpty:
					return string.Format(CultureInfo.InvariantCulture, "(({0}) is null or length({0}) = 0)", operands[0]);
				case FunctionOperatorType.EndsWith:
					return string.Format(CultureInfo.InvariantCulture, "(RigHt({0}, Length({1})) = ({1}))", operands[0], operands[1]);
				case FunctionOperatorType.Contains:
					return string.Format(CultureInfo.InvariantCulture, "(InsTR({0}, {1}) > 0)", operands[0], operands[1]);
				case FunctionOperatorType.GetMilliSecond:
					if(SupportVersion(8, 0)) {
						return string.Format(CultureInfo.InvariantCulture, "Round(Microsecond({0})/1000)", operands[0]);
					}
					throw new NotSupportedException();
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
				case FunctionOperatorType.GetTimeOfDay:
					return string.Format(CultureInfo.InvariantCulture, "(((Hour({0})) * 36000000000) + ((Minute({0})) * 600000000) + (Second({0}) * 10000000))", operands[0]);
				case FunctionOperatorType.GetDayOfWeek:
					return string.Format(CultureInfo.InvariantCulture, "((DayOfWeek({0})- DayOfWeek('1900-01-01')  + 8) % 7)", operands[0]);
				case FunctionOperatorType.GetDayOfYear:
					return string.Format(CultureInfo.InvariantCulture, "DayOfYear({0})", operands[0]);
				case FunctionOperatorType.GetDate:
					return string.Format(CultureInfo.InvariantCulture, "Date({0})", operands[0]);
				case FunctionOperatorType.AddTicks:
					return string.Format(CultureInfo.InvariantCulture, "cast(Adddate({0}, interval ({1} div 10000000 ) second )  as {2})", operands[0], operands[1], DateTimeColumnSqlType);
				case FunctionOperatorType.AddMilliSeconds:
					return string.Format(CultureInfo.InvariantCulture, "cast(Adddate({0}, interval ( {1} div 1000) second )  as {2})", operands[0], operands[1], DateTimeColumnSqlType);
				case FunctionOperatorType.AddTimeSpan:
				case FunctionOperatorType.AddSeconds:
					return FnAddDateTime(operands[0], FormatGetInt(operands[1], 1, 86400), FormatMod(operands[1], 1, 86400));
				case FunctionOperatorType.AddMinutes:
					return FnAddDateTime(operands[0], FormatGetInt(operands[1], 60, 86400), FormatMod(operands[1], 60, 86400));
				case FunctionOperatorType.AddHours:
					return FnAddDateTime(operands[0], FormatGetInt(operands[1], 3600, 86400), FormatMod(operands[1], 3600, 86400));
				case FunctionOperatorType.AddDays:
					return FnAddDateTime(operands[0], FormatGetInt(operands[1], 86400, 86400), FormatMod(operands[1], 86400, 86400));
				case FunctionOperatorType.AddMonths:
					return string.Format(CultureInfo.InvariantCulture, "cast(Adddate({0}, interval {1} month )  as {2})", operands[0], operands[1], DateTimeColumnSqlType);
				case FunctionOperatorType.AddYears:
					return string.Format(CultureInfo.InvariantCulture, "cast(Adddate({0}, interval {1} year )  as {2})", operands[0], operands[1], DateTimeColumnSqlType);
				case FunctionOperatorType.DateDiffYear:
					return string.Format(CultureInfo.InvariantCulture, "(Year({1}) - Year({0}))", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffMonth:
					return string.Format(CultureInfo.InvariantCulture, "(((Year({1}) - Year({0})) * 12) + Month({1}) - Month({0}))", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffDay:
					return string.Format(CultureInfo.InvariantCulture, SupportVersion(5, 0) ? "DATEDIFF({1}, {0})" : "(TO_DAYS({1}) - TO_DAYS({0}))", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffHour:
					return string.Format(CultureInfo.InvariantCulture, "((" + (SupportVersion(5, 0) ? "DATEDIFF({1}, {0})" : "(TO_DAYS({1}) - TO_DAYS({0}))") + " * 24) + Hour({1}) - Hour({0}))", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffMinute:
					return string.Format(CultureInfo.InvariantCulture, "((((" + (SupportVersion(5, 0) ? "DATEDIFF({1}, {0})" : "(TO_DAYS({1}) - TO_DAYS({0}))") + " * 24) + Hour({1}) - Hour({0})) * 60) + Minute({1}) - Minute({0}))", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffSecond:
					if(SupportVersion(8, 0)) {
						return string.Format(CultureInfo.InvariantCulture, "(floor(UNIX_TIMESTAMP({1})) - floor(UNIX_TIMESTAMP({0})))", operands[0], operands[1]);
					}
					return string.Format(CultureInfo.InvariantCulture, "(UNIX_TIMESTAMP({1}) - UNIX_TIMESTAMP({0}))", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffMilliSecond:
					return string.Format(CultureInfo.InvariantCulture, "((UNIX_TIMESTAMP({1}) - UNIX_TIMESTAMP({0})) * 1000)", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffTick:
					return string.Format(CultureInfo.InvariantCulture, "((UNIX_TIMESTAMP({1}) - UNIX_TIMESTAMP({0}))) * 10000000", operands[0], operands[1]);
				case FunctionOperatorType.Now:
					return "Now()";
				case FunctionOperatorType.UtcNow:
					return "UTC_TIMESTAMP()";
				case FunctionOperatorType.Today:
					return "CurDate()";
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
							return string.Format(CultureInfo.InvariantCulture, "(({0} likE {2}) And (LefT({0}, Length({1})) = ({1})))", processParameter(operands[0]), processParameter(secondOperand), processParameter(new ConstantValue(operandString.Substring(0, likeIndex) + "%")));
						}
					}
					return string.Format(CultureInfo.InvariantCulture, "(LefT({0}, Length({1})) = ({1}))", processParameter(operands[0]), processParameter(operands[1]));
				case FunctionOperatorType.AddTimeSpan:
				case FunctionOperatorType.AddSeconds:
				case FunctionOperatorType.AddMinutes:
				case FunctionOperatorType.AddHours:
				case FunctionOperatorType.DateDiffHour:
				case FunctionOperatorType.DateDiffMinute:
				case FunctionOperatorType.DateDiffSecond:
					if(ResolveColumnType((CriteriaOperator)operands[0]) == typeof(TimeOnly)) {
						return FormatFunctionTimeOnly(processParameter, operatorType, operands);
					}
					break;
			}
			return base.FormatFunction(processParameter, operatorType, operands);
		}
		string FormatFunctionTimeOnly(ProcessParameter processParameter, FunctionOperatorType operatorType, params object[] operands) {
			string[] parameters = new string[operands.Length];
			for(int i = 0; i < operands.Length; i++) {
				object operand = operands[i];
				string processedParameter = processParameter(operand);
				parameters[i] = processedParameter;
			}
			switch(operatorType) {
				case FunctionOperatorType.AddTimeSpan:
				case FunctionOperatorType.AddSeconds:
					return FnAddTime(parameters[0], parameters[1]);
				case FunctionOperatorType.AddMinutes:
					return FnAddTime(parameters[0], string.Format("({0}) * 60", parameters[1]));
				case FunctionOperatorType.AddHours:
					return FnAddTime(parameters[0], string.Format("({0}) * 3600", parameters[1]));
				case FunctionOperatorType.DateDiffHour:
					return string.Format("hour({1}) - hour({0})", parameters[0], parameters[1]);
				case FunctionOperatorType.DateDiffMinute:
					return string.Format("((hour({1}) - hour({0})) * 60) + minute({1}) - minute({0})", parameters[0], parameters[1]);
				case FunctionOperatorType.DateDiffSecond:
					return string.Format("time_to_sec({1}) - time_to_sec({0})", parameters[0], parameters[1]);
				default:
					throw new NotSupportedException();
			}
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
						if(escapeChars.ContainsKey((char)value)) {
							return FormatString(value);
						}
						else {
							ArgumentException ex = new ArgumentException(null, nameof(value));
							ex.Data["Value"] = string.Concat("\\x", Convert.ToInt32(value).ToString("X2"));
							throw ex;
						}
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
						string timeFormatPattern = SupportVersion(8, 0) ? "HH:mm:ss.ffffff" : "HH:mm:ss";
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
		public override string FormatBinary(BinaryOperatorType operatorType, string leftOperand, string rightOperand) {
			switch(operatorType) {
				case BinaryOperatorType.Modulo:
					return string.Format(CultureInfo.InvariantCulture, "{0} % {1}", leftOperand, rightOperand);
				default:
					return base.FormatBinary(operatorType, leftOperand, rightOperand);
			}
		}
		protected override object ReformatReadValue(object value, ConnectionProviderSql.ReformatReadValueArgs args) {
			if(value != null) {
				if(args.DbTypeCode == TypeCode.Object && args.TargetTypeCode == TypeCode.DateTime && args.DbType == typeof(byte[])) {
					DateTime result;
					if(DateTime.TryParse(Encoding.ASCII.GetString((byte[])value), CultureInfo.InvariantCulture, DateTimeStyles.None, out result)) {
						return result;
					}
				}
				else if(args.DbTypeCode == TypeCode.String && args.DbType == typeof(string)) {
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
			}
			return base.ReformatReadValue(value, args);
		}
		protected override object ConvertToDbParameter(object clientValue, TypeCode clientValueTypeCode) {
			if(clientValueTypeCode == TypeCode.Object && (clientValue is Guid)) {
				return clientValue.ToString();
			}
			else if(clientValue is DateOnly) {
				return DateTime.SpecifyKind(((DateOnly)clientValue).ToDateTime(TimeOnly.MinValue), DateTimeKind.Local);
			}
			else if(clientValue is TimeOnly) {
				return ((TimeOnly)clientValue).ToTimeSpan();
			}
			return base.ConvertToDbParameter(clientValue, clientValueTypeCode);
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
			else if(parameter.Value is TimeOnly) {
				return FormatConstant(parameter.Value);
			}
			createParameter = true;
			return "?p" + index.ToString(CultureInfo.InvariantCulture);
		}
		readonly Dictionary<char, string> escapeChars = new Dictionary<char, string>() {
			{ '\0', "\\0"}, {'\b', "\\b"}, {'\n', "\\n"}, {'\r', "\\r"}, {'\t', "\\t"}, {'\x1a', "\\Z"}, {'\'', "''" }, {'\\', "\\\\" }
		};
		protected string FormatString(object value) {
			string inputString = value.ToString();
			StringBuilder formattedString = new StringBuilder("'");
			foreach(char c in inputString) {
				string replaceTo;
				if(escapeChars.TryGetValue(c, out replaceTo)) {
					formattedString.Append(replaceTo);
				}
				else {
					formattedString.Append(c);
				}
			}
			formattedString.Append('\'');
			return formattedString.ToString();
		}
		public override string FormatConstraint(string constraintName) {
			return string.Format(CultureInfo.InvariantCulture, "`{0}`", constraintName);
		}
		protected override string CreateForeignKeyTemplate { get { return base.CreateForeignKeyTemplate; } }
		void ClearDatabase(IDbCommand command) {
			command.CommandText = "SET FOREIGN_KEY_CHECKS = 0";
			command.ExecuteNonQuery();
			string[] tables = GetStorageTablesList(false);
			foreach(string table in tables) {
				command.CommandText = "drop table `" + table + "`";
				command.ExecuteNonQuery();
			}
			command.CommandText = "SET FOREIGN_KEY_CHECKS = 1";
			command.ExecuteNonQuery();
		}
		protected override void ProcessClearDatabase() {
			IDbCommand command = CreateCommand();
			ClearDatabase(command);
		}
		public override string[] GetStorageTablesList(bool includeViews) {
			SelectStatementResult tables = SelectData(new Query(string.Format(CultureInfo.InvariantCulture,
				SupportVersion(5, 0) ?
				"select TABLE_NAME from INFORMATION_SCHEMA.TABLES where table_schema = '{0}' and (TABLE_TYPE = 'BASE TABLE' " + (includeViews ? " or TABLE_TYPE='VIEW')" : ")") :
				"show tables from {0}"
				, Connection.Database)));
			string[] result = new string[tables.Rows.Length];
			for(int i = 0; i < tables.Rows.Length; i++) {
				result[i] = (string)tables.Rows[i].Values[0];
			}
			return result;
		}
		#region MySqlConnector specific
		private void DeriveParametersCore(IDbCommand sourceCommand) {
			using(var command = Connection.CreateCommand()) {
				var name = sourceCommand.CommandText;
				if(name.StartsWithInvariantCulture("`") && name.EndsWithInvariantCulture("`")) {
					name = name.Substring(1, name.Length - 2);
				}
				command.CommandText = string.Format(CultureInfo.InvariantCulture, "SELECT ORDINAL_POSITION, PARAMETER_MODE, PARAMETER_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, CHARACTER_OCTET_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE, DTD_IDENTIFIER FROM INFORMATION_SCHEMA.Parameters WHERE SPECIFIC_SCHEMA = database() AND SPECIFIC_NAME = '{0}' ORDER BY ORDINAL_POSITION;", name);
				using(var reader = command.ExecuteReader()) {
					while(reader.Read()) {
						var values = new object[reader.FieldCount];
						reader.GetValues(values);
						var p = sourceCommand.CreateParameter();
						p.Direction = ParseDirection(ConvertDBNull(values[1], v => (string)v));
						p.ParameterName = ConvertDBNull(values[2], v => "@" + (string)v);
						DbTypeMapper.SetParameterTypeAndSize(p, (string)values[8]);
						sourceCommand.Parameters.Add(p);
					}
				}
			}
		}
		private T ConvertDBNull<T>(object v, Func<object, T> converter) {
			return (v == DBNull.Value) ? default(T) : converter(v);
		}
		private ParameterDirection ParseDirection(string v) {
			if(v == null)
				return ParameterDirection.ReturnValue;
			switch(v.ToUpperInvariant()) {
				case "IN":
					return ParameterDirection.Input;
				case "OUT":
					return ParameterDirection.Output;
				case "INOUT":
					return ParameterDirection.InputOutput;
			}
			throw new NotSupportedException();
		}
		#endregion
	}
	public class MySqlProviderFactory : ProviderFactory {
		string GetConnectionString(string server, string userId, string password, string database, int? port = null) {
			if(port.HasValue) {
				return MySqlConnectionProvider.GetConnectionString(server, port.Value, userId, password, database);
			}
			else {
				return MySqlConnectionProvider.GetConnectionString(server, userId, password, database);
			}
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
		public override string[] GetDatabases(string server, string userId, string password) {
			return GetDatabases(server, 0, userId, password);
		}
		public override string GetConnectionString(Dictionary<string, string> parameters) {
			if(!parameters.ContainsKey(ServerParamID) || !parameters.ContainsKey(DatabaseParamID) ||
				!parameters.ContainsKey(UserIDParamID) || !parameters.ContainsKey(PasswordParamID)) {
				return null;
			}
			string port;
			if(parameters.TryGetValue(PortParamID, out port)) {
				return this.GetConnectionString(parameters[ServerParamID], parameters[UserIDParamID], parameters[PasswordParamID], parameters[DatabaseParamID], Convert.ToInt32(port, CultureInfo.InvariantCulture));
			}
			return this.GetConnectionString(parameters[ServerParamID], parameters[UserIDParamID], parameters[PasswordParamID], parameters[DatabaseParamID]);
		}
		public override string[] GetDatabases(string server, int port, string userId, string password) {
			string connectionString;
			if(port != 0) {
				connectionString = this.GetConnectionString(server, userId, password, "", port);
			}
			else {
				connectionString = this.GetConnectionString(server, userId, password, "");
			}
			ConnectionStringParser helper = new ConnectionStringParser(connectionString);
			helper.RemovePartByName("database");
			helper.RemovePartByName(DataStoreBase.XpoProviderTypeParameterName);
			string connectToServer = helper.GetConnectionString();
			using(IDbConnection conn = MySqlConnectionProvider.CreateConnection(connectToServer)) {
				try {
					conn.Open();
					using(IDbCommand comm = conn.CreateCommand()) {
						comm.CommandText = "show databases";
						using(IDataReader reader = comm.ExecuteReader()) {
							List<string> result = new List<string>();
							while(reader.Read())
								result.Add((string)reader.GetValue(0));
							return result.ToArray();
						}
					}
				}
				catch {
					return Array.Empty<string>();
				}
			}
		}
		public override string ProviderKey { get { return MySqlConnectionProvider.XpoProviderTypeString; } }
		public override IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return MySqlConnectionProvider.CreateProviderFromConnection(connection, autoCreateOption);
		}
		public override IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			return MySqlConnectionProvider.CreateProviderFromString(connectionString, autoCreateOption, out objectsToDisposeOnDisconnect);
		}
		public override bool HasPort { get { return true; } }
		public override bool HasUserName { get { return true; } }
		public override bool HasPassword { get { return true; } }
		public override bool HasIntegratedSecurity { get { return false; } }
		public override bool HasMultipleDatabases { get { return true; } }
		public override bool IsServerbased { get { return true; } }
		public override bool IsFilebased { get { return false; } }
		public override string FileFilter { get { return null; } }
		public override bool MeanSchemaGeneration { get { return true; } }
	}
#pragma warning restore DX0024
}
namespace DevExpress.Xpo.DB.Helpers {
	using System;
	using System.Collections.Generic;
	using System.Data;
	using System.Linq;
	using DevExpress.Xpo.DB;
#if !NET
	using DevExpress.Data.NetCompatibility.Extensions;
#endif
	interface IDbTypeMapperMySql {
		bool IsByteArraySqlDbType(IDataParameter parameter);
		bool IsBoolSqlDbType(IDataParameter parameter);
	}
	class DbTypeMapperMySql<TSqlDbTypeEnum, TSqlParameter> : DbTypeMapper<TSqlDbTypeEnum, TSqlParameter>, IDbTypeMapperMySql
		where TSqlDbTypeEnum : struct
		where TSqlParameter : IDbDataParameter {
		bool isSupport80;
		static readonly TSqlDbTypeEnum mySqlTypeLongText;
		static readonly TSqlDbTypeEnum mySqlTypeMediumText;
		static readonly TSqlDbTypeEnum mySqlTypeText;
		static readonly TSqlDbTypeEnum mySqlTypeLongBlob;
		static readonly TSqlDbTypeEnum mySqlTypeMediumBlob;
		static readonly TSqlDbTypeEnum mySqlTypeTinyBlob;
		static readonly TSqlDbTypeEnum mySqlTypeBlob;
		static readonly TSqlDbTypeEnum mySqlTypeBit;
		static DbTypeMapperMySql() {
			mySqlTypeLongText = (TSqlDbTypeEnum)Enum.Parse(typeof(TSqlDbTypeEnum), "LongText");
			mySqlTypeMediumText = (TSqlDbTypeEnum)Enum.Parse(typeof(TSqlDbTypeEnum), "MediumText");
			mySqlTypeText = (TSqlDbTypeEnum)Enum.Parse(typeof(TSqlDbTypeEnum), "Text");
			mySqlTypeLongBlob = (TSqlDbTypeEnum)Enum.Parse(typeof(TSqlDbTypeEnum), "LongBlob");
			mySqlTypeMediumBlob = (TSqlDbTypeEnum)Enum.Parse(typeof(TSqlDbTypeEnum), "MediumBlob");
			mySqlTypeTinyBlob = (TSqlDbTypeEnum)Enum.Parse(typeof(TSqlDbTypeEnum), "TinyBlob");
			mySqlTypeBlob = (TSqlDbTypeEnum)Enum.Parse(typeof(TSqlDbTypeEnum), "Blob");
			mySqlTypeBit = (TSqlDbTypeEnum)Enum.Parse(typeof(TSqlDbTypeEnum), "Bit");
		}
		public DbTypeMapperMySql(bool isSupport80) {
			this.isSupport80 = isSupport80;
		}
		protected override string ParameterDbTypePropertyName { get { return "MySqlDbType"; } }
		protected override string GetParameterTypeNameForBoolean(out int? size, out byte? precision, out byte? scale) {
			size = 1;
			precision = scale = null;
			return "Bit";
		}
		protected override string GetParameterTypeNameForByte(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "UByte";
		}
		protected override string GetParameterTypeNameForByteArray(out int? size) {
			size = null;
			return "LongBlob";
		}
		protected override string GetParameterTypeNameForChar(out int? size) {
			size = 1;
			return "String";
		}
		protected override string GetParameterTypeNameForDateTime() {
			return "DateTime";
		}
		protected override string GetParameterTypeNameForDecimal(out byte? precision, out byte? scale) {
			precision = 28;
			scale = 8;
			return "Decimal";
		}
		protected override string GetParameterTypeNameForDouble(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Double";
		}
		protected override string GetParameterTypeNameForGuid(out int? size) {
			size = 38;
			return "String";
		}
		protected override string GetParameterTypeNameForInt16(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Int16";
		}
		protected override string GetParameterTypeNameForInt32(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Int32";
		}
		protected override string GetParameterTypeNameForInt64(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Int64";
		}
		protected override string GetParameterTypeNameForSByte(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Byte";
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
			return "UInt16";
		}
		protected override string GetParameterTypeNameForUInt32(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "UInt32";
		}
		protected override string GetParameterTypeNameForUInt64(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "UInt64";
		}
		protected override string GetParameterTypeNameForDateOnly(out int? size) {
			size = null;
			return "Date";
		}
		protected override string GetParameterTypeNameForTimeOnly(out int? size) {
			size = 6;
			return "Time";
		}
		protected override string ConvertSqlTypeToParameterType(string sqlType) {
			switch(sqlType.ToUpperInvariant()) {
				case "TINYINT":
					return "Byte";
				case "TINYINT UNSIGNED":
					return "UByte";
				case "SMALLINT":
					return "Int16";
				case "SMALLINT UNSIGNED":
					return "UInt16";
				case "MEDIUMINT":
					return "Int24";
				case "MEDIUMINT UNSIGNED":
					return "UInt24";
				case "INT":
				case "INTEGER":
					return "Int32";
				case "INT UNSIGNED":
				case "INTEGER UNSIGNED":
					return "UInt32";
				case "BIGINT":
					return "Int64";
				case "BIGINT UNSIGNED":
					return "UInt64";
				case "FLOAT":
					return "Float";
				case "DOUBLE":
				case "REAL":
					return "Double";
				case "DECIMAL":
				case "NUMERIC":
					return "Decimal";
				case "BIT":
					return "Bit";
				case "YEAR":
					return "Year";
				case "DATE":
					if(ConnectionProviderSql.GlobalUseLegacyDateOnlyAndTimeOnlySupport)
						return "DateTime";
					else
						return "Date";
				case "TIME":
					return "Time";
				case "DATETIME":
					return "DateTime";
				case "TIMESTAMP":
					return "Timestamp";
				case "CHAR":
				case "NCHAR":
					return "String";
				case "BINARY":
					return "Binary";
				case "VARCHAR":
				case "NVARCHAR":
					return "VarChar";
				case "VARBINARY":
					return "VarBinary";
				case "TINYBLOB":
					return "TinyBlob";
				case "TINYTEXT":
					return "TinyText";
				case "BLOB":
					return "Blob";
				case "TEXT":
					return "Text";
				case "MEDIUMBLOB":
					return "MediumBlob";
				case "MEDIUMTEXT":
					return "MediumText";
				case "LONGBLOB":
					return "LongBlob";
				case "LONGTEXT":
					return "LongText";
				case "ENUM":
					return "Enum";
				case "SET":
					return "Set";
				default:
					return null;
			}
		}
		protected override DBTypeInfoBase CustomParseSqlType(string sqlTypeWithoutParameters, string sqlTypeParameters, string sqlTypeSuffix) {
			int? size = null;
			byte? precision = null;
			byte? scale = null;
			string typeString = sqlTypeWithoutParameters;
			if(!string.IsNullOrEmpty(sqlTypeSuffix) && sqlTypeSuffix.Contains("UNSIGNED", StringComparison.OrdinalIgnoreCase)) {
				typeString += " UNSIGNED";
			}
			if(!string.IsNullOrEmpty(sqlTypeParameters)) {
				var parameters = sqlTypeParameters.Split(',').Select(v => v.Trim()).ToArray();
				int value0;
				if(parameters.Length >= 1 && Int32.TryParse(parameters[0], out value0)) {
					size = value0;
				}
				byte value1;
				if(parameters.Length >= 2 && Byte.TryParse(parameters[1], out value1)) {
					precision = value1;
				}
			}
			return CreateParameterDBTypeInfo(typeString, size, precision, scale);
		}
		public override void SetParameterTypeAndSize(IDbDataParameter parameter, DBColumnType dbColumnType, int size) {
			if(dbColumnType == DBColumnType.String) {
				if(size <= 0 || size > 16777215) {
					SetSqlDbTypeHandler((TSqlParameter)parameter, mySqlTypeLongText);
					return;
				}
				else if(size > 65535) {
					SetSqlDbTypeHandler((TSqlParameter)parameter, mySqlTypeMediumText);
					return;
				}
				else if(size > 255) {
					SetSqlDbTypeHandler((TSqlParameter)parameter, mySqlTypeText);
					return;
				}
			}
			else if(dbColumnType == DBColumnType.ByteArray) {
				if(size <= 0 || size > 16777215) {
					SetSqlDbTypeHandler((TSqlParameter)parameter, mySqlTypeLongBlob);
				}
				else if(size > 65535) {
					SetSqlDbTypeHandler((TSqlParameter)parameter, mySqlTypeMediumBlob);
				}
				else if(size > 127) {
					SetSqlDbTypeHandler((TSqlParameter)parameter, mySqlTypeBlob);
				}
				else {
					SetSqlDbTypeHandler((TSqlParameter)parameter, mySqlTypeTinyBlob);
				}
				return;
			}
			else if(isSupport80 && dbColumnType == DBColumnType.DateTime) {
				size = 6;
			}
			base.SetParameterTypeAndSize(parameter, dbColumnType, size);
		}
		public bool IsByteArraySqlDbType(IDataParameter parameter) {
			TSqlDbTypeEnum type = GetSqlDbTypeHandler((TSqlParameter)parameter);
			return Equals(type, mySqlTypeBlob) || Equals(type, mySqlTypeLongBlob) || Equals(type, mySqlTypeMediumBlob) || Equals(type, mySqlTypeTinyBlob);
		}
		public bool IsBoolSqlDbType(IDataParameter parameter) {
			TSqlDbTypeEnum type = GetSqlDbTypeHandler((TSqlParameter)parameter);
			return Equals(type, mySqlTypeBit);
		}
	}
	class MySqlUpdateSchemaSqlFormatterHelper : UpdateSchemaSqlFormatterHelper {
		public MySqlUpdateSchemaSqlFormatterHelper(
			ISqlGeneratorFormatter sqlGeneratorFormatter,
			Func<DBTable, DBColumn, bool, string> getSqlCreateColumnFullAttributes,
			Func<string, string> formatConstraintSafe,
			Func<DBIndex, DBTable, string> getIndexName,
			Func<DBForeignKey, DBTable, string> getForeignKeyName,
			Func<DBPrimaryKey, DBTable, string> getPrimaryKeyName)
			: base(sqlGeneratorFormatter, getSqlCreateColumnFullAttributes,
				formatConstraintSafe, getIndexName, getForeignKeyName, getPrimaryKeyName) {
		}
		protected override string[] FormatRenameTable(RenameTableStatement statement) {
			return new string[] {
				string.Format("rename table {0} to {1}", FormatTableSafe(statement.Table.Name), SqlGeneratorFormatter.ComposeSafeTableName(statement.NewTableName))
			};
		}
		protected override string[] FormatRenameColumn(string tableName, string oldColumnName, string newColumnName) {
			return Array.Empty<string>();
		}
		protected override string[] FormatCreateTable(CreateTableStatement statement) {
			var statements = new List<string>();
			statements.AddRange(base.FormatCreateTable(statement));
			if(statement.Table.PrimaryKey != null) {
				DBColumn key = statement.Table.GetColumn(statement.Table.PrimaryKey.Columns[0]);
				if(!key.IsIdentity) {
					statements.AddRange(FormatCreatePrimaryKey(new CreatePrimaryKeyStatement(statement.Table, statement.Table.PrimaryKey.Columns)));
				}
			}
			return statements.ToArray();
		}
		protected override string[] FormatAlterColumn(AlterColumnStatement statement) {
			var statements = new List<string>(1);
			string oldColumnName = SqlGeneratorFormatter.ComposeSafeColumnName(statement.OldColumn.Name);
			string newColumnName = SqlGeneratorFormatter.ComposeSafeColumnName(statement.NewColumn.Name);
			string oldColumnSql = GetSqlCreateColumnFullAttributes(statement.Table, statement.OldColumn, false);
			string newColumnSql = GetSqlCreateColumnFullAttributes(statement.Table, statement.NewColumn, false);
			if(oldColumnSql != newColumnSql || oldColumnName != newColumnName) {
				const string pkConstraint = " AUTO_INCREMENT PRIMARY KEY";
				if(newColumnSql.EndsWith(pkConstraint)) {
					newColumnSql = newColumnSql.Remove(newColumnSql.Length - pkConstraint.Length);
				}
				string sql = string.Format(AlterColumnTemplate,
					 FormatTableSafe(statement.Table.Name), FormatColumnSafe(oldColumnName), FormatColumnSafe(newColumnName), newColumnSql);
				statements.Add(sql);
			}
			return statements.ToArray();
		}
		protected override string[] FormatCreatePrimaryKey(CreatePrimaryKeyStatement statement) {
			if(statement.PrimaryKeyColumns.Length == 1) {
				DBColumn key = statement.PrimaryKeyColumns[0];
				if(key != null && key.IsIdentity && (key.ColumnType == DBColumnType.Int32 || key.ColumnType == DBColumnType.Int64)) {
					string sqlType = GetSqlCreateColumnFullAttributes(statement.Table, key, false);
					string sql = string.Format("alter table {0} modify {1} {2}", FormatTableSafe(statement.Table.Name), FormatColumnSafe(key.Name), sqlType);
					return new string[] { sql };
				}
			}
			return base.FormatCreatePrimaryKey(statement);
		}
		protected override string[] FormatDropPrimaryKey(DropPrimaryKeyStatement statement) {
			var statements = new List<string>(1);
			if(statement.Table.PrimaryKey.Columns.Count == 1) {
				DBColumn key = statement.Table.GetColumn(statement.Table.PrimaryKey.Columns[0]);
				if(key != null && key.IsIdentity && (key.ColumnType == DBColumnType.Int32 || key.ColumnType == DBColumnType.Int64)) {
					string sqlType = GetSqlCreateColumnFullAttributes(statement.Table, key, false);
					const string pkConstraint = " AUTO_INCREMENT PRIMARY KEY";
					if(sqlType.EndsWith(pkConstraint)) {
						sqlType = sqlType.Remove(sqlType.Length - pkConstraint.Length);
						statements.Add(string.Format("alter table {0} modify {1} {2}", FormatTableSafe(statement.Table.Name), FormatColumnSafe(key.Name), sqlType));
					}
				}
			}
			statements.Add(string.Format(DropPrimaryKeyTemplate, FormatTableSafe(statement.Table.Name)));
			return statements.ToArray();
		}
		protected override string AlterColumnTemplate {
			get { return "alter table {0} change {1} {2} {3}"; }
		}
		protected override string DropForeignKeyTemplate {
			get { return "alter table {0} drop foreign key {1}"; }
		}
		protected override string DropPrimaryKeyTemplate {
			get { return "alter table {0} drop primary key"; }
		}
	}
}
