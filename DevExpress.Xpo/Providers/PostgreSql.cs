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
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using DevExpress.Data.Filtering;
	using DevExpress.Data.Helpers;
	using DevExpress.Utils;
	using DevExpress.Xpo;
	using DevExpress.Xpo.DB.Exceptions;
	using DevExpress.Xpo.DB.Helpers;
#if !NET
	using DevExpress.Data.NetCompatibility.Extensions;
#endif
#pragma warning disable DX0024
	public class PostgreSqlConnectionProvider : ConnectionProviderSql {
		public const string XpoProviderTypeString = "Postgres";
		const string ConectionStringEncodingParameterName = "Encoding";
		const string ConectionStringEncodingParameterValue = "UNICODE";
		ReflectConnectionHelper helper;
		private const string NpgsqlExceptionTypeName = "Npgsql.NpgsqlException";
		private const string PostgresExceptionTypeName = "Npgsql.PostgresException";
		public static bool? GlobalUseLegacyGuidSupport = false;
		public bool? UseLegacyGuidSupport;
		bool IsLegacyGuidSupport {
			get {
				if(!SupportVersion(8.3m, 0)) {
					return true;
				}
				if(UseLegacyGuidSupport.HasValue || GlobalUseLegacyGuidSupport.HasValue) {
					return UseLegacyGuidSupport.HasValue ? UseLegacyGuidSupport.Value : GlobalUseLegacyGuidSupport.Value;
				}
				else {
					return false;
				}
			}
		}
		bool IsNativeDateOnlySupported {
			get {
#if NET
				return true;
#else
				return false;
#endif
			}
		}
		ReflectConnectionHelper ConnectionHelper {
			get {
				if(helper == null) {
					if(SupportNpgsqlVersion(3, 2, 0)) {
						helper = new ReflectConnectionHelper(Connection, NpgsqlExceptionTypeName, PostgresExceptionTypeName);
					}
					else {
						helper = new ReflectConnectionHelper(Connection, NpgsqlExceptionTypeName);
					}
				}
				return helper;
			}
		}
		UpdateSchemaSqlFormatterHelper updateSchemaSqlFormatter;
		protected override UpdateSchemaSqlFormatterHelper UpdateSchemaFormatter {
			get {
				if(updateSchemaSqlFormatter == null) {
					updateSchemaSqlFormatter = new PostgreSqlUpdateSchemaSqlFormatterHelper(this, GetSqlCreateColumnFullAttributes, FormatConstraintSafe, GetIndexName, GetForeignKeyName, GetPrimaryKeyName, GetSqlCreateColumnType);
				}
				return updateSchemaSqlFormatter;
			}
		}
		protected override DBSchemaComparerSql CreateSchemaComparer() {
			return new PostgreSqlDBSchemaComparer(ComposeSafeTableName, ComposeSafeColumnName, GetSqlCreateColumnType, DbTypeMapper.ParseSqlType) {
				NeedsIndexForForeignKey = NeedsIndexForForeignKey
			};
		}
		public static string GetConnectionString(string server, string userId, string password, string database) {
			return string.Format("{4}={5};Server={0};User Id={1};Password={2};Database={3};{6}={7};",
				EscapeConnectionStringArgument(server), EscapeConnectionStringArgument(userId), EscapeConnectionStringArgument(password), EscapeConnectionStringArgument(database), DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString, ConectionStringEncodingParameterName, ConectionStringEncodingParameterValue);
		}
		public static string GetConnectionString(string server, int port, string userId, string password, string database) {
			return string.Format("{5}={6};Server={0};User Id={1};Password={2};Database={3};{7}={8};Port={4}",
				EscapeConnectionStringArgument(server), EscapeConnectionStringArgument(userId), EscapeConnectionStringArgument(password), EscapeConnectionStringArgument(database), port, DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString, ConectionStringEncodingParameterName, ConectionStringEncodingParameterValue);
		}
		public static IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			IDbConnection connection = CreateConnection(connectionString);
			objectsToDisposeOnDisconnect = new IDisposable[] { connection };
			return CreateProviderFromConnection(connection, autoCreateOption);
		}
		public static IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return new PostgreSqlConnectionProvider(connection, autoCreateOption);
		}
		static PostgreSqlConnectionProvider() {
			RegisterDataStoreProvider(XpoProviderTypeString, new DataStoreCreationFromStringDelegate(CreateProviderFromString));
			RegisterDataStoreProvider("NpgsqlConnection", new DataStoreCreationFromConnectionDelegate(CreateProviderFromConnection));
			RegisterFactory(new PostgreSqlProviderFactory());
		}
		public static void Register() { }
		public PostgreSqlConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption)
			: base(connection, autoCreateOption, true) {
			ReadDbVersion(connection);
		}
		protected PostgreSqlConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption, bool openConnection)
			: base(connection, autoCreateOption, openConnection) {
		}
		decimal? versionMajor;
		int versionMinor;
		protected void ReadDbVersion(IDbConnection conn) {
			try {
				using(IDbCommand c = CreateCommand(new Query("SHOW server_version"))) {
					object result = c.ExecuteScalar();
					if(result != null && result is string) {
						SetServerVersionInternal((string)result);
					}
				}
			}
			catch { }
		}
		bool SetServerVersionInternal(string versionString) {
			int whitespaceIndex = versionString.IndexOf(' ');
			if(whitespaceIndex > 0) {
				versionString = versionString.Substring(0, whitespaceIndex);
			}
			string[] versionParts = versionString.Split('.');
			decimal versionMajorLocal = default(decimal);
			int versionMinorLocal = default(int);
			bool couldParse = false;
			if(versionParts.Length == 3) {
				string versionMajorStr = string.Concat(versionParts[0], ".", versionParts[1]);
				couldParse = decimal.TryParse(versionMajorStr, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out versionMajorLocal)
					&& int.TryParse(versionParts[2], out versionMinorLocal);
			}
			else if(versionParts.Length == 2) {
				couldParse = decimal.TryParse(versionParts[0], out versionMajorLocal)
					&& int.TryParse(versionParts[1], out versionMinorLocal);
			}
			if(couldParse) {
				versionMajor = versionMajorLocal;
				versionMinor = versionMinorLocal;
			}
			return couldParse;
		}
		Version npgSqlVersion;
		bool SupportNpgsqlVersion(int major, int minor, int build) {
			if(npgSqlVersion == null) {
				npgSqlVersion = Connection.GetType().Assembly.GetName().Version;
			}
			if(npgSqlVersion.Major == major) {
				if(npgSqlVersion.Minor == minor) {
					return npgSqlVersion.Build >= build;
				}
				return npgSqlVersion.Minor >= minor;
			}
			return npgSqlVersion.Major > major;
		}
		bool SupportVersion(decimal major, int minor) {
			if(!versionMajor.HasValue)
				return true;
			if(versionMajor.Value > major)
				return true;
			if(versionMajor.Value == major && versionMinor >= minor)
				return true;
			return false;
		}
		public void SetServerVersion(string versionString) {
			if(!SetServerVersionInternal(versionString)) {
				throw new ArgumentException(null, nameof(versionString));
			}
		}
		protected override string GetSqlCreateColumnTypeForBoolean(DBTable table, DBColumn column) {
			return "boolean";
		}
		protected override string GetSqlCreateColumnTypeForByte(DBTable table, DBColumn column) {
			return "smallint";
		}
		protected override string GetSqlCreateColumnTypeForSByte(DBTable table, DBColumn column) {
			return "smallint";
		}
		protected override string GetSqlCreateColumnTypeForChar(DBTable table, DBColumn column) {
			return "char(1)";
		}
		protected override string GetSqlCreateColumnTypeForDecimal(DBTable table, DBColumn column) {
			return "numeric(28,8)";
		}
		protected override string GetSqlCreateColumnTypeForDouble(DBTable table, DBColumn column) {
			return "double precision";
		}
		protected override string GetSqlCreateColumnTypeForSingle(DBTable table, DBColumn column) {
			return "real";
		}
		protected override string GetSqlCreateColumnTypeForInt32(DBTable table, DBColumn column) {
			return "integer";
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
			return "bigint";
		}
		protected override string GetSqlCreateColumnTypeForUInt64(DBTable table, DBColumn column) {
			return "numeric(20,0)";
		}
		public const int MaximumStringSize = 4000;
		protected override string GetSqlCreateColumnTypeForString(DBTable table, DBColumn column) {
			if(column.Size > 0 && column.Size <= MaximumStringSize)
				return "varchar(" + column.Size.ToString(CultureInfo.InvariantCulture) + ')';
			else
				return "text";
		}
		protected override string GetSqlCreateColumnTypeForDateTime(DBTable table, DBColumn column) {
			return "timestamp";
		}
		protected override string GetSqlCreateColumnTypeForGuid(DBTable table, DBColumn column) {
			return IsLegacyGuidSupport ? "char(36)" : "uuid";
		}
		protected override string GetSqlCreateColumnTypeForByteArray(DBTable table, DBColumn column) {
			return "bytea";
		}
		protected override string GetSqlCreateColumnTypeForDateOnly(DBTable table, DBColumn column) {
			return "date";
		}
		protected override string GetSqlCreateColumnTypeForTimeOnly(DBTable table, DBColumn column) {
			return "time without time zone";
		}
		public override string GetSqlCreateColumnFullAttributes(DBTable table, DBColumn column) {
			return null;
		}
		public override string GetSqlCreateColumnFullAttributes(DBTable table, DBColumn column, bool forTableCreate) {
			string result = GetSqlCreateColumnFullAttributes(table, column);
			if(!string.IsNullOrEmpty(result)) {
				return result;
			}
			if(column.IsKey && column.IsIdentity && IsSingleColumnPKColumn(table, column)) {
				switch(column.ColumnType) {
					case DBColumnType.Int32:
						return "serial PRIMARY KEY";
					case DBColumnType.Int64:
						return "bigserial PRIMARY KEY";
				}
			}
			result = GetSqlCreateColumnType(table, column);
			if(column.IsKey || !column.IsNullable) {
				result += " NOT NULL";
			}
			else {
				result += " NULL";
			}
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
			return result;
		}
		protected override object ConvertToDbParameter(object clientValue, TypeCode clientValueTypeCode) {
			switch(clientValueTypeCode) {
				case TypeCode.Object:
					if(clientValue is Guid) {
						if(IsLegacyGuidSupport) {
							if(SupportNpgsqlVersion(3, 0, 0)) {
								return clientValue.ToString().ToCharArray();
							}
							return clientValue.ToString();
						}
					}
					else if(clientValue is DateOnly) {
						if(!IsNativeDateOnlySupported) {
							return DateTime.SpecifyKind(((DateOnly)clientValue).ToDateTime(TimeOnly.MinValue), DateTimeKind.Local);
						}
					}
					else if(clientValue is TimeOnly) {
						if(!IsNativeDateOnlySupported) {
							return ((TimeOnly)clientValue).ToTimeSpan();
						}
					}
					break;
				case TypeCode.Byte:
					return (Int16)(Byte)clientValue;
				case TypeCode.SByte:
					return (Int16)(SByte)clientValue;
				case TypeCode.UInt16:
					return (Int32)(UInt16)clientValue;
				case TypeCode.UInt32:
					return (Int64)(UInt32)clientValue;
				case TypeCode.UInt64:
					return (Decimal)(UInt64)clientValue;
				case TypeCode.DateTime:
					return DateTime.SpecifyKind((DateTime)clientValue, DateTimeKind.Local);
			}
			return base.ConvertToDbParameter(clientValue, clientValueTypeCode);
		}
		ParameterValue CreateParameterForSystemQuery(int tag, string value) {
			return new ParameterValue(tag) {
				DBTypeName = "Name",
				Value = value
			};
		}
		Query GetIdentityPrepareQuery(InsertStatement root, TaggedParametersHolder identitiesByTag) {
			string seq = GetSeqName(root.Table.Name, root.IdentityColumn);
			Query sql = new InsertSqlGenerator(this, identitiesByTag, new Dictionary<OperandValue, string>()).GenerateSql(root);
			Query returnIdSql;
			if(SupportVersion(8.2m, 0)) {
				returnIdSql = new Query(string.Concat(sql.Sql, " RETURNING ", FormatColumnSafe(root.IdentityColumn)), sql.Parameters, sql.ParametersNames);
			}
			else {
				sql.Parameters.Add(new ParameterValue(0) {
					Value = seq,
					DBType = DBColumnType.String,
					Size = 255
				});
				sql.ParametersNames.Add("@seq");
				returnIdSql = new Query(sql.Sql + ";select currval(@seq)", sql.Parameters, sql.ParametersNames);
			}
			return returnIdSql;
		}
		protected override Int64 GetIdentity(InsertStatement root, TaggedParametersHolder identitiesByTag) {
			Query returnIdSql = GetIdentityPrepareQuery(root, identitiesByTag);
			object value = GetScalar(returnIdSql);
			return ((IConvertible)value).ToInt64(CultureInfo.InvariantCulture);
		}
		protected override async Task<Int64> GetIdentityAsync(InsertStatement root, TaggedParametersHolder identitiesByTag, AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken) {
			Query returnIdSql = GetIdentityPrepareQuery(root, identitiesByTag);
			object value = await GetScalarAsync(returnIdSql, asyncOperationId, cancellationToken).ConfigureAwait(false);
			return ((IConvertible)value).ToInt64(CultureInfo.InvariantCulture);
		}
		protected override IDbConnection CreateConnection() {
			return ConnectionHelper.GetConnection(ConnectionString);
		}
		static public IDbConnection CreateConnection(string connectionString) {
			var connection = ReflectConnectionHelper.GetConnection("Npgsql", "Npgsql.NpgsqlConnection", true);
			Version npgsqlVersion = connection.GetType().Assembly.GetName().Version;
			if(npgsqlVersion.Major < 2) {
				connection.ConnectionString = connectionString;
			}
			else if(npgsqlVersion.Major == 3 && (npgsqlVersion.Minor == 0 || (npgsqlVersion.Minor == 1 && npgsqlVersion.Build < 8))) {
				var helper = new ConnectionStringParser(connectionString);
				helper.RemovePartByName(ConectionStringEncodingParameterName);
				connection.ConnectionString = helper.GetConnectionString();
			}
			else {
				var helper = new ConnectionStringParser(connectionString);
				if(helper.GetPartByName(ConectionStringEncodingParameterName) == ConectionStringEncodingParameterValue) {
					helper.RemovePartByName(ConectionStringEncodingParameterName);
				}
				connection.ConnectionString = helper.GetConnectionString();
			}
			return connection;
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
			if(param.DbType == DbType.AnsiString) {
				param.DbType = DbType.String;
			}
			else if(value is DateTime) {
				param.DbType = param.DbType;
			}
			else if(value is DateOnly) {
				param.DbType = DbType.Date;
			}
			else if(value is TimeOnly) {
				param.DbType = DbType.Time;
			}
			else if(value is byte[]) {
				param.DbType = DbType.Binary;
			}
			else {
				if(!SupportNpgsqlVersion(3, 0, 0)) {
					if(value is TimeSpan && dbType == DBColumnType.TimeOnly) {
						((IDbTypeMapperPostgreSql)DbTypeMapper).SetNpgsqlDbTypeInterval(param);
					}
				}
				if(SupportNpgsqlVersion(3, 0, 0)) {
					char[] charArray = value as char[];
					if(charArray != null) {
						((IDbTypeMapperPostgreSql)DbTypeMapper).SetNpgsqlDbTypeChar(param, charArray.Length);
					}
				}
			}
			if(parameterMode == QueryParameterMode.SetTypeAndSize) {
				ValidateParameterSize(command, param);
			}
			return param;
		}
		DbTypeMapperBase dbTypeMapper;
		protected override DbTypeMapperBase DbTypeMapper {
			get {
				if(dbTypeMapper == null) {
					string ns = ConnectionHelper.ConnectionType.Namespace;
					Type typeNpgSqlParameter = ConnectionHelper.GetType("Npgsql.NpgsqlParameter");
					Type typeNpgSqlDbType = ConnectionHelper.GetType("NpgsqlTypes.NpgsqlDbType");
					dbTypeMapper = (DbTypeMapperBase)Activator.CreateInstance(typeof(DbTypeMapperPostgresql<,>).MakeGenericType(typeNpgSqlDbType, typeNpgSqlParameter), new object[] { IsLegacyGuidSupport });
				}
				return dbTypeMapper;
			}
		}
		protected override void PreparePooledCommand(IDbCommand command) {
			foreach(IDbDataParameter parameter in command.Parameters) {
				((IDbTypeMapperPostgreSql)DbTypeMapper).ResetNpgsqlDbType(parameter);
			}
			base.PreparePooledCommand(command);
		}
		public static CommandPoolBehavior? GlobalCommandPoolBehavior;
		CommandPoolBehavior? commandPoolBehavior;
		protected override CommandPoolBehavior CommandPoolBehavior {
			get {
				if(commandPoolBehavior == null) {
					commandPoolBehavior = SupportVersion(8.3M, 0) ? CommandPoolBehavior.ConnectionSession : CommandPoolBehavior.None;
				}
				if(GlobalCommandPoolBehavior.HasValue) {
					if(GlobalCommandPoolBehavior.Value > commandPoolBehavior.Value) {
						throw new NotSupportedException(Res.GetString(Res.PostgreSQL_CommandPoolNotSupported, GlobalCommandPoolBehavior.Value, versionMajor));
					}
					return GlobalCommandPoolBehavior.Value;
				}
				return commandPoolBehavior.Value;
			}
		}
		protected override Exception WrapException(Exception e, IDbCommand query) {
			object o;
			if(ConnectionHelper.TryGetExceptionProperty(e, "Code", out o)
				|| ConnectionHelper.TryGetExceptionProperty(e, "SqlState", out o)) {
				string code = (string)o;
				if(code == "42703" || code == "42P01") {
					return new SchemaCorrectionNeededException(e);
				}
				if(code == "23503" || code == "23505") {
					return new ConstraintViolationException(query.CommandText, GetParametersString(query), e);
				}
				if(code == "42883") {
					if(e.Message.Contains("uuid")) {
						string[] operators = new string[] { "=", "<>", ">", "<" };
						string[] operands = new string[] { "character", "text" };
						foreach(string op in operators) {
							foreach(string arg in operands) {
								if(e.Message.Contains(string.Concat(arg, " ", op, " uuid"))
									|| e.Message.Contains(string.Concat("uuid ", op, " ", arg))) {
									string msg = string.Concat(e.Message + "\r\n" + Res.GetString(Res.PostgreSQL_LegacyGuidDetected));
									return new InvalidOperationException(msg, e);
								}
							}
						}
					}
				}
			}
			return base.WrapException(e, query);
		}
		public override void CreateTable(DBTable table) {
			string columns = string.Empty;
			foreach(DBColumn col in table.Columns) {
				if(columns.Length > 0)
					columns += ", ";
				columns += (FormatColumnSafe(col.Name) + ' ' + GetSqlCreateColumnFullAttributes(table, col, true));
			}
			ExecuteSqlSchemaUpdate("Table", table.Name, string.Empty, string.Format(CultureInfo.InvariantCulture,
				"create table {0} ({1}) without oids",
				FormatTableSafe(table), columns));
		}
		protected override void CreateDataBase() {
			const string CannotOpenDatabaseError = "3D000";
			try {
				Connection.Open();
			}
			catch(Exception e) {
				object[] propertiesValues;
				if(CanCreateDatabase && ConnectionHelper.TryGetExceptionProperties(e, new string[] { "Errors", "Code", "SqlState" }, out propertiesValues)
					&& propertiesValues != null && propertiesValues.Any(t => (string)t == CannotOpenDatabaseError)) {
					ConnectionStringParser helper = new ConnectionStringParser(ConnectionString);
					string dbName = helper.GetPartByName("Database");
					helper.RemovePartByName("Database");
					using(IDbConnection conn = ConnectionHelper.GetConnection(helper.GetConnectionString() + ";Database=template1")) {
						conn.Open();
						using(IDbCommand c = conn.CreateCommand()) {
							if(!dbName.StartsWith('\"'))
								dbName = '"' + dbName + '"';
							c.CommandText = "Create Database " + dbName + " WITH ENCODING='UNICODE'";
							c.ExecuteNonQuery();
						}
					}
				}
				else {
					throw new UnableToOpenDatabaseException(XpoDefault.ConnectionStringRemovePassword(ConnectionString), e);
				}
			}
		}
		protected virtual string GetSeqName(string tableName, string columnName) {
			string schema = ComposeSafeSchemaName(tableName);
			string table = ComposeSafeTableName(tableName);
			const string postfix = "_xpoView";
			string seqName = "\"" + (table.EndsWith(postfix) ? table.Substring(0, table.Length - postfix.Length) : table);
			string seqTail = string.Concat("_", ComposeSafeColumnName(columnName), "_seq\"");
			if(seqName.Length + seqTail.Length > 64)
				seqName = seqName.Substring(0, 65 - seqTail.Length);
			return string.IsNullOrEmpty(schema) ? seqName + seqTail : string.Concat("\"", schema, "\".", seqName, seqTail);
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
		DBColumnType GetTypeFromString(string typeName, int length) {
			switch(typeName) {
				case "int4":
					return DBColumnType.Int32;
				case "bytea":
					return DBColumnType.ByteArray;
				case "bpchar":
					return length <= 1 ? DBColumnType.Char : DBColumnType.String;
				case "varchar":
				case "text":
					return DBColumnType.String;
				case "bool":
					return DBColumnType.Boolean;
				case "int2":
					return DBColumnType.Int16;
				case "int8":
					return DBColumnType.Int64;
				case "numeric":
					return DBColumnType.Decimal;
				case "float8":
					return DBColumnType.Double;
				case "float4":
					return DBColumnType.Single;
				case "timestamp":
					return DBColumnType.DateTime;
				case "date":
					if(ConnectionProviderSql.GlobalUseLegacyDateOnlyAndTimeOnlySupport)
						return DBColumnType.DateTime;
					else
						return DBColumnType.DateOnly;
				case "time":
				case "time without time zone":
					return DBColumnType.TimeOnly;
				case "uuid":
					return DBColumnType.Guid;
			}
			return DBColumnType.Unknown;
		}
		void GetColumns(DBTable table) {
			string schema = ComposeSafeSchemaName(table.Name);
			if(schema == string.Empty) schema = ObjectsOwner;
			string safeTableName = ComposeSafeTableName(table.Name);
			string defaultValueQueryExpr = SupportVersion(12, 0) ?
				"case when a.atthasdef and a.attgenerated <> 's' then pg_get_expr(d.adbin, d.adrelid) else null end" :
				"d.adsrc";
			Query query = new Query(string.Concat("select a.attname, t.typname, a.atttypmod, a.attnotnull, ", defaultValueQueryExpr, @", pg_catalog.format_type(a.atttypid, a.atttypmod) from pg_attribute a
join pg_class c on a.attrelid = c.oid
join pg_type t on a.atttypid = t.oid
join pg_namespace n on c.relnamespace = n.oid
left join pg_attrdef d on a.attrelid=d.adrelid and a.attnum=d.adnum
where c.relname = @p0 AND n.nspname = @p1 AND a.attnum > 0 AND NOT a.attisdropped and (c.relkind = 'r' OR c.relkind = 'v')"),
				new QueryParameterCollection(CreateParameterForSystemQuery(0, safeTableName), CreateParameterForSystemQuery(1, schema)), new string[] { "@p0", "@p1" });
			foreach(SelectStatementResultRow row in SelectData(query).Rows) {
				int size = ((int)row.Values[2]) - 4;
				string typeName = (string)row.Values[1];
				string fullTypeName = (string)row.Values[5];
				DBColumnType type = GetTypeFromString(typeName, size);
				bool isNullable = (!Convert.ToBoolean(row.Values[3]));
				string dbDefaultValue = (row.Values[4] as string);
				object defaultValue = null;
				try {
					if(!string.IsNullOrEmpty(dbDefaultValue) && !dbDefaultValue.StartsWith("nextval")) {
						string scalarQuery = string.Concat("select ", dbDefaultValue);
						defaultValue = FixDBNullScalar(GetScalar(new Query(scalarQuery)));
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
				DBColumn dBColumn = new DBColumn((string)row.Values[0], false, fullTypeName ?? string.Empty, type == DBColumnType.String ? size : 0, type, isNullable, defaultValue);
				dBColumn.DbDefaultValue = dbDefaultValue;
				table.AddColumn(dBColumn);
			}
		}
		void GetPrimaryKey(DBTable table) {
			string schema = ComposeSafeSchemaName(table.Name);
			if(schema == string.Empty) schema = ObjectsOwner;
			string safeTableName = ComposeSafeTableName(table.Name);
			string tableNameParam = string.Concat("\"", schema, "\".\"", safeTableName, "\"");
			Query query = new Query(@"select a.attname, pg_get_serial_sequence(@p2, a.attname) as seqname, c.conname 
from pg_constraint c
join pg_attribute a on c.conrelid=a.attrelid and a.attnum = ANY (c.conkey)
join pg_class tc on c.conrelid=tc.oid
join pg_namespace n on tc.relnamespace = n.oid
where c.contype = 'p' and tc.relname = @p0 and n.nspname = @p1
order by a.attnum",
				new QueryParameterCollection(CreateParameterForSystemQuery(0, safeTableName), CreateParameterForSystemQuery(1, schema), new ParameterValue(2) { DBType = DBColumnType.String, Size = 131, Value = tableNameParam }), new string[] { "@p0", "@p1", "@p2" });
			SelectStatementResult data = SelectData(query);
			if(data.Rows.Length > 0) {
				StringCollection cols = new StringCollection();
				for(int i = 0; i < data.Rows.Length; i++) {
					var dataRow = data.Rows[i];
					string columnName = (string)dataRow.Values[0];
					string seqName = Convert.ToString(dataRow.Values[1]);
					DBColumn column = table.GetColumn(columnName);
					if(column != null) {
						column.IsKey = true;
						column.IsIdentity = !string.IsNullOrEmpty(seqName) ||
							(!string.IsNullOrEmpty(column.DbDefaultValue) &&
								column.DbDefaultValue.StartsWith("nextval", true, CultureInfo.InvariantCulture));
					}
					cols.Add(columnName);
				}
				string name = (string)data.Rows[0].Values[2];
				table.PrimaryKey = new DBPrimaryKey(name, cols);
			}
		}
		void GetIndexes(DBTable table) {
			string schema = ComposeSafeSchemaName(table.Name);
			if(schema == string.Empty) schema = ObjectsOwner;
			string safeTableName = ComposeSafeTableName(table.Name);
			Query query = new Query(@"select ind.relname, col.attname, col.attnum, i.indisunique from pg_index i
join pg_class ind on i.indexrelid = ind.oid
join pg_class tbl on i.indrelid = tbl.oid
join pg_attribute col on ind.oid = col.attrelid
join pg_namespace n on tbl.relnamespace = n.oid
where tbl.relname = @p0 and n.nspname = @p1
order by ind.relname, col.attnum
",
				new QueryParameterCollection(CreateParameterForSystemQuery(0, safeTableName), CreateParameterForSystemQuery(1, schema)), new string[] { "@p0", "@p1" });
			SelectStatementResult data = SelectData(query);
			DBIndex index = null;
			foreach(SelectStatementResultRow row in data.Rows) {
				if(1 == (short)row.Values[2]) {
					StringCollection list = new StringCollection();
					list.Add((string)row.Values[1]);
					index = new DBIndex((string)row.Values[0], list, (bool)row.Values[3]);
					table.Indexes.Add(index);
				}
				else
					index.Columns.Add((string)row.Values[1]);
			}
		}
		void GetForeignKeys(DBTable table) {
			string schema = ComposeSafeSchemaName(table.Name);
			if(schema == string.Empty) schema = ObjectsOwner;
			string safeTableName = ComposeSafeTableName(table.Name);
			Query query = new Query(@"select pos.n, col.attname ,fc.attname, tcf.relname, nf.nspname, c.conname
from generate_series(1,current_setting('max_index_keys')::int,1) pos(n), pg_constraint c
join pg_class tc on c.conrelid = tc.oid
join pg_class tcf on c.confrelid = tcf.oid
join pg_attribute col on c.conrelid = col.attrelid
join pg_attribute fc on c.confrelid = fc.attrelid
join pg_namespace n on tc.relnamespace = n.oid
join pg_namespace nf on tcf.relnamespace = nf.oid
where c.contype='f' and tc.relname = @p0 and n.nspname = @p1 and fc.attnum = c.confkey[pos.n] and col.attnum = c.conkey[pos.n]
order by c.conname, pos.n",
				new QueryParameterCollection(CreateParameterForSystemQuery(0, safeTableName), CreateParameterForSystemQuery(1, schema)), new string[] { "@p0", "@p1" });
			SelectStatementResult data = SelectData(query);
			DBForeignKey fk = null;
			foreach(SelectStatementResultRow row in data.Rows) {
				if((int)row.Values[0] == 1) {
					StringCollection pkc = new StringCollection();
					StringCollection fkc = new StringCollection();
					pkc.Add((string)row.Values[2]);
					fkc.Add((string)row.Values[1]);
					string fkTableName = (string)row.Values[3];
					string fkSchemeName = (string)row.Values[4];
					string fkFullName = fkSchemeName == ObjectsOwner ? fkTableName : string.Concat(fkSchemeName, ".", fkTableName);
					fk = new DBForeignKey(fkc, fkFullName, pkc);
					fk.Name = (string)row.Values[5];
					table.ForeignKeys.Add(fk);
				}
				else {
					fk.Columns.Add((string)row.Values[1]);
					fk.PrimaryKeyTableKeyColumns.Add((string)row.Values[2]);
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
			Dictionary<string, bool> dbTables = new Dictionary<string, bool>();
			Dictionary<string, bool> dbSchemaTables = new Dictionary<string, bool>();
			string queryString = @"select c.relname, c.relkind, n.nspname from pg_class c 
join pg_namespace n on c.relnamespace = n.oid
where c.relname in({0}) and (c.relkind = 'r' OR c.relkind = 'v')";
			foreach(SelectStatementResultRow row in GetDataForTables(tables, null, queryString).Rows) {
				if(row.Values[0] is DBNull) continue;
				string tableName = (string)row.Values[0];
				bool isView = Convert.ToString(row.Values[1]) == "v";
				string tableSchemaName = (string)row.Values[2];
				dbTables[tableName] = isView;
				dbSchemaTables.Add(string.Concat(tableSchemaName, ".", tableName), isView);
			}
			ArrayList list = new ArrayList();
			foreach(DBTable table in tables) {
				string tableName = ComposeSafeTableName(table.Name);
				string tableSchemaName = ComposeSafeSchemaName(table.Name);
				bool isView = false;
				if(!dbSchemaTables.TryGetValue(string.Concat(tableSchemaName, ".", tableName), out isView) && !dbTables.TryGetValue(tableName, out isView))
					list.Add(table);
				else
					table.IsView = isView;
			}
			return list;
		}
		protected override int GetSafeNameTableMaxLength() {
			return 63;
		}
		protected override int GetObjectNameEffectiveLength(string objectName) {
			return Encoding.UTF8.GetByteCount(objectName);
		}
		public override string FormatTable(string schema, string tableName) {
			if(string.IsNullOrEmpty(schema))
				return string.Format(CultureInfo.InvariantCulture, "\"{0}\"", tableName);
			else
				return string.Format(CultureInfo.InvariantCulture, "\"{0}\".\"{1}\"", schema, tableName);
		}
		public override string FormatTable(string schema, string tableName, string tableAlias) {
			if(string.IsNullOrEmpty(schema))
				return string.Format(CultureInfo.InvariantCulture, "\"{0}\" {1}", tableName, tableAlias);
			else
				return string.Format(CultureInfo.InvariantCulture, "\"{0}\".\"{1}\" {2}", schema, tableName, tableAlias);
		}
		public override string FormatColumn(string columnName) {
			return string.Format(CultureInfo.InvariantCulture, "\"{0}\"", columnName);
		}
		public override string FormatColumn(string columnName, string tableAlias) {
			return string.Format(CultureInfo.InvariantCulture, "{1}.\"{0}\"", columnName, tableAlias);
		}
		public override bool NativeSkipTakeSupported { get { return true; } }
		public override bool NativeOuterApplySupported {
			get {
				return SupportVersion(9.3m, 0);
			}
		}
		public override string FormatSelect(string selectedPropertiesSql, string fromSql, string whereSql, string orderBySql, string groupBySql, string havingSql, int skipSelectedRecords, int topSelectedRecords) {
			base.FormatSelect(selectedPropertiesSql, fromSql, whereSql, orderBySql, groupBySql, havingSql, skipSelectedRecords, topSelectedRecords);
			string modificatorsSql = string.Empty;
			if(skipSelectedRecords != 0 || topSelectedRecords != 0) {
				string limitSql = string.Format(CultureInfo.InvariantCulture, "LIMIT {0} ", topSelectedRecords);
				modificatorsSql = string.Format(CultureInfo.InvariantCulture, " {0}OFFSET {1} ", topSelectedRecords == 0 ? string.Empty : limitSql, skipSelectedRecords);
			}
			string expandedWhereSql = whereSql != null ? string.Format(CultureInfo.InvariantCulture, "{0}where {1}", Environment.NewLine, whereSql) : string.Empty;
			string expandedOrderBySql = orderBySql != null ? string.Format(CultureInfo.InvariantCulture, "{0}order by {1}", Environment.NewLine, orderBySql) : string.Empty;
			string expandedHavingSql = havingSql != null ? string.Format(CultureInfo.InvariantCulture, "{0}having {1}", Environment.NewLine, havingSql) : string.Empty;
			string expandedGroupBySql = groupBySql != null ? string.Format(CultureInfo.InvariantCulture, "{0}group by {1}", Environment.NewLine, groupBySql) : string.Empty;
			return string.Format(CultureInfo.InvariantCulture, "select {1} from {2}{3}{4}{5}{6}{0}", modificatorsSql, selectedPropertiesSql, fromSql, expandedWhereSql, expandedGroupBySql, expandedHavingSql, expandedOrderBySql);
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
					return string.Format(CultureInfo.InvariantCulture, "{0} % {1}", leftOperand, rightOperand);
				case BinaryOperatorType.BitwiseXor:
					return string.Format(CultureInfo.InvariantCulture, "({0} # {1})", leftOperand, rightOperand);
				default:
					return base.FormatBinary(operatorType, leftOperand, rightOperand);
			}
		}
		static string FormatMod(string arg, int multiplier, int divider, bool useFloor = true) {
			return string.Format(useFloor ? "mod((trunc(({0})::numeric * {1}))::bigint, {2})" : "mod((({0})::numeric * {1})::bigint, {2})", arg, multiplier, divider);
		}
		static string FormatGetInt(string arg, int multiplier, int divider) {
			return string.Format("((trunc(({0})::numeric * {1}))::bigint / {2})", arg, multiplier, divider);
		}
		string FnAddDateTime(string datetimeOperand, string dayPart, string secondPart, string millisecondPart) {
			if(SupportVersion(8.3m, 0)) {
				return string.Format(CultureInfo.InvariantCulture, "(({0}) + ({1} || ' day')::interval + ({2} || ' second')::interval + ({3} || ' millisecond')::interval)",
					datetimeOperand, dayPart, secondPart, millisecondPart);
			}
			return string.Format(CultureInfo.InvariantCulture, "(({0}) + ({1} || ' day')::interval + ({2} || ' second')::interval)",
				datetimeOperand, dayPart, secondPart);
		}
		public override string FormatFunction(FunctionOperatorType operatorType, params string[] operands) {
			switch(operatorType) {
				case FunctionOperatorType.Len:
					return string.Concat("length(", operands[0], ')');
				case FunctionOperatorType.Substring:
					string len = operands.Length < 3 ? string.Concat("length(", operands[0], ")", " - ", operands[1]) : operands[2];
					return string.Concat("substr(", operands[0], ", (", operands[1], ") + 1, ", len, ")");
				case FunctionOperatorType.Char:
					return string.Format(CultureInfo.InvariantCulture, "chr({0})", operands[0]);
				case FunctionOperatorType.CharIndex:
					return FnCharIndex(operands);
				case FunctionOperatorType.Log:
					return FnLog(operands);
				case FunctionOperatorType.Log10:
					return string.Format(CultureInfo.InvariantCulture, "log({0})", operands[0]);
				case FunctionOperatorType.Remove:
					return FnRemove(operands);
				case FunctionOperatorType.Rnd:
					return "random()";
				case FunctionOperatorType.Max:
					return string.Format(CultureInfo.InvariantCulture, "(case when {0} > {1} then {0} else {1} end)", operands[0], operands[1]);
				case FunctionOperatorType.Min:
					return string.Format(CultureInfo.InvariantCulture, "(case when {0} < {1} then {0} else {1} end)", operands[0], operands[1]);
				case FunctionOperatorType.Round:
					switch(operands.Length) {
						case 1:
							return string.Format(CultureInfo.InvariantCulture, "Round(cast({0} as numeric), 0)", operands[0]);
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "Round(cast({0} as numeric),{1})", operands[0], operands[1]);
					}
					break;
				case FunctionOperatorType.Sqr:
					return string.Format(CultureInfo.InvariantCulture, "sqrt({0})", operands[0]);
				case FunctionOperatorType.Acos:
					return string.Format(CultureInfo.InvariantCulture, "acos({0})", operands[0]);
				case FunctionOperatorType.Asin:
					return string.Format(CultureInfo.InvariantCulture, "asin({0})", operands[0]);
				case FunctionOperatorType.Atn:
					return string.Format(CultureInfo.InvariantCulture, "atan({0})", operands[0]);
				case FunctionOperatorType.Atn2:
					return string.Format(CultureInfo.InvariantCulture, "atan2({0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.Cosh:
					return string.Format(CultureInfo.InvariantCulture, "((exp({0}::real) + exp(-({0})::real)) / 2)", operands[0]);
				case FunctionOperatorType.Sinh:
					return string.Format(CultureInfo.InvariantCulture, "((exp({0}::real) - exp(-({0})::real)) / 2)", operands[0]);
				case FunctionOperatorType.Tanh:
					return string.Format(CultureInfo.InvariantCulture, "((exp({0}::real) - exp(-({0})::real)) / (exp({0}::real) + exp(-({0})::real)))", operands[0]);
				case FunctionOperatorType.BigMul:
					return string.Format(CultureInfo.InvariantCulture, "(({0})::bigint * ({1})::bigint)", operands[0], operands[1]);
				case FunctionOperatorType.ToInt:
					return string.Format(CultureInfo.InvariantCulture, "CAST({0} AS int)", operands[0]);
				case FunctionOperatorType.ToLong:
					return string.Format(CultureInfo.InvariantCulture, "CAST({0} AS bigint)", operands[0]);
				case FunctionOperatorType.ToFloat:
					return string.Format(CultureInfo.InvariantCulture, "CAST({0} AS real)", operands[0]);
				case FunctionOperatorType.ToDouble:
					return string.Format(CultureInfo.InvariantCulture, "CAST({0} AS double precision)", operands[0]);
				case FunctionOperatorType.ToDecimal:
					return string.Format(CultureInfo.InvariantCulture, "CAST({0} AS decimal)", operands[0]);
				case FunctionOperatorType.ToStr:
					return string.Format(CultureInfo.InvariantCulture, "(({0}) || '')", operands[0]);
				case FunctionOperatorType.Insert:
					return string.Format(CultureInfo.InvariantCulture, "(substr({0}, 0, ({1}) + 1) || {2} || substr({0}, ({1}) + 1))", operands[0], operands[1], operands[2]);
				case FunctionOperatorType.PadLeft:
					return FnLpad(operands);
				case FunctionOperatorType.PadRight:
					return FnRpad(operands);
				case FunctionOperatorType.Today:
					return "current_date";
				case FunctionOperatorType.Now:
					return "localtimestamp";
				case FunctionOperatorType.UtcNow:
					return "(now() at time zone 'UTC')";
				case FunctionOperatorType.GetMilliSecond:
					return string.Format(CultureInfo.InvariantCulture, "round(floor((extract(millisecond from {0}) / 1000 - floor(extract(millisecond from {0}) / 1000))::numeric(6, 6) * 1000000) / 1000)", operands[0]);
				case FunctionOperatorType.GetSecond:
					return string.Format(CultureInfo.InvariantCulture, "floor(extract(second from {0}))", operands[0]);
				case FunctionOperatorType.GetMinute:
					return string.Format(CultureInfo.InvariantCulture, "extract(minute from {0})", operands[0]);
				case FunctionOperatorType.GetHour:
					return string.Format(CultureInfo.InvariantCulture, "extract(hour from {0})", operands[0]);
				case FunctionOperatorType.GetDay:
					return string.Format(CultureInfo.InvariantCulture, "extract(day from {0})", operands[0]);
				case FunctionOperatorType.GetMonth:
					return string.Format(CultureInfo.InvariantCulture, "extract(month from {0})", operands[0]);
				case FunctionOperatorType.GetYear:
					return string.Format(CultureInfo.InvariantCulture, "extract(year from {0})", operands[0]);
				case FunctionOperatorType.GetTimeOfDay:
					return string.Format(CultureInfo.InvariantCulture, "floor(extract(hour from {0}) * 36000000000 + extract(minute from {0}) * 600000000 + extract(second from {0}) * 10000000)", operands[0]);
				case FunctionOperatorType.GetDayOfWeek:
					return string.Format(CultureInfo.InvariantCulture, "extract(dow from {0})", operands[0]);
				case FunctionOperatorType.GetDayOfYear:
					return string.Format(CultureInfo.InvariantCulture, "extract(doy from {0})", operands[0]);
				case FunctionOperatorType.GetDate:
					return string.Format(CultureInfo.InvariantCulture, "({0})::date", operands[0]);
				case FunctionOperatorType.AddTicks:
					if(SupportVersion(8.3m, 0)) {
						return string.Format(CultureInfo.InvariantCulture, "(({0}) + ((({1}) / 10000) || ' millisecond')::interval)", operands[0], operands[1]);
					}
					return string.Format(CultureInfo.InvariantCulture, "(({0}) + ((({1}) / 10000000) || ' second')::interval)", operands[0], operands[1]);
				case FunctionOperatorType.AddMilliSeconds:
					if(SupportVersion(8.3m, 0)) {
						return string.Format(CultureInfo.InvariantCulture, "(({0}) + (({1}) || ' millisecond')::interval)", operands[0], operands[1]);
					}
					throw new NotSupportedException();
				case FunctionOperatorType.AddTimeSpan:
				case FunctionOperatorType.AddSeconds:
					return FnAddDateTime(operands[0], FormatGetInt(operands[1], 1000, 86400000), FormatMod(operands[1], 1, 86400), FormatMod(operands[1], 1000, 1000, false));
				case FunctionOperatorType.AddMinutes:
					return FnAddDateTime(operands[0], FormatGetInt(operands[1], 60000, 86400000), FormatMod(operands[1], 60, 86400), FormatMod(operands[1], 60000, 1000, false));
				case FunctionOperatorType.AddHours:
					return FnAddDateTime(operands[0], FormatGetInt(operands[1], 3600000, 86400000), FormatMod(operands[1], 3600, 86400), FormatMod(operands[1], 3600000, 1000, false));
				case FunctionOperatorType.AddDays:
					return FnAddDateTime(operands[0], FormatGetInt(operands[1], 86400000, 86400000), FormatMod(operands[1], 86400, 86400), FormatMod(operands[1], 86400000, 1000, false));
				case FunctionOperatorType.AddMonths:
					return string.Format(CultureInfo.InvariantCulture, "(({0}) + (({1}) || ' month')::interval)", operands[0], operands[1]);
				case FunctionOperatorType.AddYears:
					return string.Format(CultureInfo.InvariantCulture, "(({0}) + (({1}) || ' year')::interval)", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffYear:
					return string.Format(CultureInfo.InvariantCulture, "(extract(year from ({1})) - extract(year from ({0})))", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffMonth:
					return string.Format(CultureInfo.InvariantCulture, "(((extract(year from ({1})) - extract(year from ({0}))) * 12) + extract(month from ({1})) - extract(month from ({0})))", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffDay:
					return string.Format(CultureInfo.InvariantCulture, "(extract(epoch from (date_trunc('day', ({1})) - date_trunc('day', ({0})))) / 86400)::int", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffHour:
					return string.Format(CultureInfo.InvariantCulture, "(extract(epoch from (date_trunc('hour', ({1})) - date_trunc('hour', ({0})))) / 3600)::int", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffMinute:
					return string.Format(CultureInfo.InvariantCulture, "(extract(epoch from (date_trunc('minute', ({1})) - date_trunc('minute', ({0})))) / 60)::int", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffSecond:
					return string.Format(CultureInfo.InvariantCulture, "(extract(epoch from (date_trunc('second', ({1})) - date_trunc('second', ({0})))))::int", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffMilliSecond:
					return string.Format(CultureInfo.InvariantCulture, "(extract(epoch from (date_trunc('millisecond', ({1})) - date_trunc('millisecond', ({0})))) * 1000)::int", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffTick:
					return string.Format(CultureInfo.InvariantCulture, "(extract(epoch from (date_trunc('millisecond', ({1})) - date_trunc('millisecond', ({0})))) * 10000000)::int", operands[0], operands[1]);
				case FunctionOperatorType.IsNull:
					switch(operands.Length) {
						case 1:
							return string.Format(CultureInfo.InvariantCulture, "(({0}) is null)", operands[0]);
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "COALESCE({0}, {1})", operands[0], operands[1]);
					}
					break;
				case FunctionOperatorType.IsNullOrEmpty:
					return string.Format(CultureInfo.InvariantCulture, "(({0}) is null or length({0}) = 0)", operands[0]);
				case FunctionOperatorType.EndsWith:
					return string.Format(CultureInfo.InvariantCulture, "(SubsTr({0}, Length({0}) - Length({1}) + 1) = ({1}))", operands[0], operands[1]);
				case FunctionOperatorType.Contains:
					return string.Format(CultureInfo.InvariantCulture, "(StrpOS({0}, {1}) > 0)", operands[0], operands[1]);
			}
			return base.FormatFunction(operatorType, operands);
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
			}
			return base.FormatFunction(processParameter, operatorType, operands);
		}
		string FnLpad(string[] operands) {
			if(operands.Length == 2) {
				return string.Format(CultureInfo.InvariantCulture, "case when length({0}) > {1} then {0} else lpad({0}, {1}, ' ') end", operands[0], operands[1]);
			}
			if(operands.Length == 3) {
				return string.Format(CultureInfo.InvariantCulture, "case when length({0}) > {1} then {0} else lpad({0}, {1}, {2}) end", operands[0], operands[1], operands[2]);
			}
			throw new NotSupportedException();
		}
		string FnRpad(string[] operands) {
			if(operands.Length == 2) {
				return string.Format(CultureInfo.InvariantCulture, "case when length({0}) > {1} then {0} else rpad({0}, {1}, ' ') end", operands[0], operands[1]);
			}
			if(operands.Length == 3) {
				return string.Format(CultureInfo.InvariantCulture, "case when length({0}) > {1} then {0} else rpad({0}, {1}, {2}) end", operands[0], operands[1], operands[2]);
			}
			throw new NotSupportedException();
		}
		string FnRemove(string[] operands) {
			if(operands.Length == 3) {
				return string.Format(CultureInfo.InvariantCulture, "(substr({0}, 0, {1} + 1) || substr({0}, {1} + {2} + 1))", operands[0], operands[1], operands[2]);
			}
			if(operands.Length == 2) {
				return string.Format(CultureInfo.InvariantCulture, "substr({0}, 0, {1} + 1)", operands[0], operands[1]);
			}
			throw new NotSupportedException();
		}
		string FnLog(string[] operands) {
			if(operands.Length == 1) {
				return string.Format(CultureInfo.InvariantCulture, "ln({0})", operands[0]);
			}
			if(operands.Length == 2) {
				return string.Format(CultureInfo.InvariantCulture, "(ln({0}) / ln({1}))", operands[0], operands[1]);
			}
			throw new NotSupportedException();
		}
		string FnCharIndex(string[] operands) {
			if(operands.Length == 2) {
				return string.Format(CultureInfo.InvariantCulture, "(position({0} in {1}) - 1)", operands[0], operands[1]);
			}
			if(operands.Length == 3) {
				return string.Format(CultureInfo.InvariantCulture, "(case position({0} in substring({1}, {2} + 1)) > 0 when true then position({0} in substring({1}, {2} + 1)) + {2} - 1 else -1 end)", operands[0], operands[1], operands[2]);
			}
			if(operands.Length == 4) {
				return string.Format(CultureInfo.InvariantCulture, "(case position({0} in substring({1}, {2} + 1, {3})) > 0 when true then position({0} in substring({1}, {2} + 1, {3})) + {2} - 1 else -1 end)", operands[0], operands[1], operands[2], operands[3]);
			}
			throw new NotSupportedException();
		}
		public override string GetParameterName(OperandValue parameter, int index, ref bool createParameter) {
			object value = parameter.Value;
			createParameter = false;
			if(parameter is ConstantValue && value != null) {
				switch(DXTypeExtensions.GetTypeCode(value.GetType())) {
					case TypeCode.Int32:
						return ((int)value).ToString(CultureInfo.InvariantCulture);
					case TypeCode.Boolean:
						return (bool)value ? "true" : "false";
					case TypeCode.String: {
						return FormatString(value);
					}
				}
			}
			createParameter = true;
			return "@p" + index.ToString(CultureInfo.InvariantCulture);
		}
		protected string FormatString(object value) {
			return string.Concat(SupportVersion(8.1m, 0) ? "E'" : "'", ((string)value).Replace("'", "''").Replace(@"\", @"\\"), "'");
		}
		public override string FormatConstraint(string constraintName) {
			return string.Format(CultureInfo.InvariantCulture, "\"{0}\"", ComposeSafeConstraintName(constraintName));
		}
		public override string FormatOrder(string sortProperty, SortingDirection direction) {
			if(SupportVersion(8.3m, 0)) {
				return string.Format(CultureInfo.InvariantCulture, "{0} {1}", sortProperty,
					direction == SortingDirection.Ascending ? "asc nulls first" : "desc nulls last");
			}
			else {
				return string.Format(CultureInfo.InvariantCulture, "{0} {1}", sortProperty,
					direction == SortingDirection.Ascending ? "asc" : "desc");
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
					return ((bool)value) ? "true" : "false";
				case TypeCode.Char:
					if(value is char && Convert.ToInt32(value) == 0) {
						ArgumentException ex = new ArgumentException(null, nameof(value));
						ex.Data["Value"] = string.Concat("\\x", Convert.ToInt32(value).ToString("X2"));
						throw ex;
					}
					if(value is char && Convert.ToInt32(value) < 32) {
						return string.Format("chr({0})", Convert.ToByte(value).ToString(CultureInfo.InvariantCulture));
					}
					else {
						return "'" + (char)value + "'";
					}
				case TypeCode.DateTime:
					DateTime datetimeValue = (DateTime)value;
					const string dateTimeFormatPattern = "yyyy-MM-dd HH:mm:ss";
					return string.Format("cast('{0}' as timestamp)", datetimeValue.ToString(dateTimeFormatPattern, CultureInfo.InvariantCulture));
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
						return string.Format("cast('{0}' as date)", dateValue.ToString(dateFormatPattern, CultureInfo.InvariantCulture));
					}
					else if(value is TimeOnly) {
						var timeValue = (TimeOnly)value;
						const string timeFormatPattern = "HH:mm:ss.fff";
						return string.Format("cast('{0}' as time without time zone)", timeValue.ToString(timeFormatPattern, CultureInfo.InvariantCulture));
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
		public override string FormatOuterApply(string sql, string alias) {
			return string.Format(CultureInfo.InvariantCulture, "cross join lateral ({0}) {1}", sql, alias);
		}
		public void ClearDatabase(IDbCommand command) {
			Query query = new Query("select CONSTRAINT_NAME, TABLE_NAME, TABLE_SCHEMA from INFORMATION_SCHEMA.TABLE_CONSTRAINTS where CONSTRAINT_TYPE = 'FOREIGN KEY'");
			SelectStatementResult constraints = SelectData(query);
			foreach(SelectStatementResultRow row in constraints.Rows) {
				string constraintName = ((string)row.Values[0]).Trim();
				string tableName = ((string)row.Values[1]).Trim();
				string schema = ((string)row.Values[2]).Trim();
				command.CommandText = string.Format("alter table {0} drop constraint {1}", FormatTable(schema, tableName), FormatConstraint(constraintName));
				command.ExecuteNonQuery();
			}
			query = new Query("select TABLE_NAME, TABLE_SCHEMA from INFORMATION_SCHEMA.VIEWS where TABLE_SCHEMA <> 'information_schema' and TABLE_SCHEMA <> 'pg_catalog'");
			SelectStatementResult views = SelectData(query);
			foreach(SelectStatementResultRow row in views.Rows) {
				string tableName = ((string)row.Values[0]).Trim();
				string schema = ((string)row.Values[1]).Trim();
				command.CommandText = string.Format("drop view {0}", FormatTable(schema, tableName));
				command.ExecuteNonQuery();
			}
			string[] tables = GetStorageTablesList(false);
			foreach(string table in tables) {
				string schema = GetSchemaName(table);
				string tableName = GetTableName(table);
				command.CommandText = string.Format("drop table {0}", FormatTable(schema, tableName));
				command.ExecuteNonQuery();
			}
		}
		string GetSchemaName(string table) {
			int dot = table.IndexOf('.');
			if(dot > 0) { return table.Substring(0, dot); }
			return string.Empty;
		}
		string GetTableName(string table) {
			int dot = table.IndexOf('.');
			if(dot > 0) return table.Remove(0, dot + 1);
			return table;
		}
		protected override void ProcessClearDatabase() {
			IDbCommand command = CreateCommand();
			ClearDatabase(command);
		}
		public override string[] GetStorageTablesList(bool includeViews) {
			Query query = new Query(string.Format(@"select TABLE_NAME, TABLE_SCHEMA from INFORMATION_SCHEMA.TABLES 
where (TABLE_TYPE = 'BASE TABLE' {0}) and TABLE_SCHEMA <> 'information_schema' and TABLE_SCHEMA <> 'pg_catalog'", includeViews ? " or TABLE_TYPE = 'VIEW'" : ""));
			SelectStatementResult tables = SelectData(query);
			List<string> result = new List<string>(tables.Rows.Length);
			foreach(SelectStatementResultRow row in tables.Rows) {
				string objectName = (string)row.Values[0];
				string owner = (string)row.Values[1];
				if(ObjectsOwner != owner && owner != null)
					result.Add(string.Concat(owner, ".", objectName));
				else
					result.Add(objectName);
			}
			return result.ToArray();
		}
		public string ObjectsOwner = "public";
		void GenerateView(DBTable table, StringBuilder result) {
			string objName = ComposeSafeTableName(string.Format("{0}_xpoView", table.Name));
			result.AppendLine(string.Format("CREATE VIEW \"{0}\"(", objName));
			for(int i = 0; i < table.Columns.Count; i++) {
				if(i != 0) { result.AppendLine(","); }
				result.Append(string.Format("\t\"{0}\"", table.Columns[i].Name));
			}
			result.AppendLine();
			result.AppendLine(")");
			result.AppendLine("AS");
			result.AppendLine("\tSELECT");
			for(int i = 0; i < table.Columns.Count; i++) {
				if(i != 0) { result.AppendLine(","); }
				result.Append(string.Format("\t\t\"{0}\"", table.Columns[i].Name));
			}
			result.AppendLine();
			result.AppendLine(string.Format("\tFROM \"{0}\";", table.Name));
		}
		void GenerateInsertSP(DBTable table, StringBuilder result) {
			string objName = ComposeSafeTableName(string.Format("sp_{0}_xpoView_insert", table.Name));
			result.AppendLine(string.Format("CREATE FUNCTION \"{0}\"(", objName));
			for(int i = 0; i < table.Columns.Count; i++) {
				if(i != 0) { result.AppendLine(","); }
				string dbType = GetRawType(GetSqlCreateColumnType(table, table.Columns[i]));
				result.Append(string.Format("\t{0}_ {1}", table.Columns[i].Name, dbType));
			}
			result.AppendLine();
			result.AppendLine(")");
			result.AppendLine("RETURNS VOID");
			result.AppendLine("AS $$");
			result.AppendLine(string.Format("\tINSERT INTO \"{0}\"(", table.Name));
			for(int i = 0; i < table.Columns.Count; i++) {
				if(i != 0) { result.AppendLine(","); }
				result.Append(string.Format("\t\t\"{0}\"", table.Columns[i].Name));
			}
			result.AppendLine();
			result.AppendLine("\t)");
			result.AppendLine("\tVALUES(");
			for(int i = 0; i < table.Columns.Count; i++) {
				if(i != 0) { result.AppendLine(","); }
				result.Append(string.Format("\t\t${0}", (i + 1).ToString(CultureInfo.InvariantCulture)));
			}
			result.AppendLine();
			result.AppendLine("\t);");
			result.AppendLine("$$ LANGUAGE SQL;");
		}
		void GenerateUpdateSP(DBTable table, StringBuilder result) {
			string objName = ComposeSafeTableName(string.Format("sp_{0}_xpoView_update", table.Name));
			result.AppendLine(string.Format("CREATE FUNCTION \"{0}\"(", objName));
			AppendKeys(table, result);
			for(int i = 0; i < table.Columns.Count; i++) {
				if(IsKey(table, table.Columns[i].Name)) { continue; }
				if(i != 0 || table.PrimaryKey.Columns.Count > 0) { result.AppendLine(","); }
				string dbType = GetRawType(GetSqlCreateColumnType(table, table.Columns[i]));
				result.AppendLine(string.Format("\told_{0} {1},", table.Columns[i].Name, dbType));
				result.Append(string.Format("\t{0}_ {1}", table.Columns[i].Name, dbType));
			}
			result.AppendLine();
			result.AppendLine(")");
			result.AppendLine("RETURNS VOID");
			result.AppendLine("AS $$");
			result.AppendLine(string.Format("\tUPDATE \"{0}\" SET", table.Name));
			bool first = true;
			int paramNum = 0;
			for(int i = 0; i < table.Columns.Count; i++) {
				if(IsKey(table, table.Columns[i].Name)) { continue; }
				if(first) { first = false; } else { result.AppendLine(","); }
				result.Append(string.Format("\t\t\"{0}\"=${1}", table.Columns[i].Name, ((paramNum++) * 2) + table.PrimaryKey.Columns.Count + 2));
			}
			result.AppendLine();
			result.AppendLine("\tWHERE");
			AppendWhere(table, result);
			result.AppendLine("$$ LANGUAGE SQL;");
		}
		void GenerateDeleteSP(DBTable table, StringBuilder result) {
			string objName = ComposeSafeTableName(string.Format("sp_{0}_xpoView_delete", table.Name));
			result.AppendLine(string.Format("CREATE FUNCTION \"{0}\"(", objName));
			AppendKeys(table, result);
			for(int i = 0; i < table.Columns.Count; i++) {
				if(IsKey(table, table.Columns[i].Name)) { continue; }
				if(i != 0 || table.PrimaryKey.Columns.Count > 0) { result.AppendLine(","); }
				string dbType = GetSqlCreateColumnType(table, table.Columns[i]);
				result.Append(string.Format("\told_{0} {1}", table.Columns[i].Name, GetRawType(dbType)));
			}
			result.AppendLine();
			result.AppendLine(")");
			result.AppendLine("RETURNS VOID");
			result.AppendLine("AS $$");
			result.AppendLine(string.Format("\tDELETE FROM \"{0}\" WHERE", table.Name));
			AppendWhere(table, result);
			result.AppendLine("$$ LANGUAGE SQL;");
		}
		void GenerateInsteadOfInsertRule(DBTable table, StringBuilder result) {
			string ruleName = ComposeSafeTableName(string.Format("r_{0}_xpoView_insert", table.Name));
			result.AppendLine(string.Format("CREATE RULE \"{0}\" AS", ruleName));
			string viewName = ComposeSafeTableName(string.Format("{0}_xpoView", table.Name));
			result.AppendLine(string.Format("ON INSERT TO \"{0}\" DO INSTEAD", viewName));
			string spName = ComposeSafeTableName(string.Format("sp_{0}_xpoView_insert", table.Name));
			result.AppendLine(string.Format("\tSELECT \"{0}\"(", spName));
			for(int i = 0; i < table.Columns.Count; i++) {
				if(i != 0) { result.AppendLine(","); }
				result.Append(string.Format("\t\tnew.\"{0}\"", table.Columns[i].Name));
			}
			result.AppendLine();
			result.AppendLine("\t);");
		}
		void GenerateInsteadOfUpdateRule(DBTable table, StringBuilder result) {
			string ruleName = ComposeSafeTableName(string.Format("r_{0}_xpoView_update", table.Name));
			result.AppendLine(string.Format("CREATE RULE \"{0}\" AS", ruleName));
			string viewName = ComposeSafeTableName(string.Format("{0}_xpoView", table.Name));
			result.AppendLine(string.Format("ON UPDATE TO \"{0}\" DO INSTEAD", viewName));
			string spName = ComposeSafeTableName(string.Format("sp_{0}_xpoView_update", table.Name));
			result.AppendLine(string.Format("\tSELECT \"{0}\"(", spName));
			for(int i = 0; i < table.PrimaryKey.Columns.Count; i++) {
				if(i != 0) { result.AppendLine(","); }
				result.Append(string.Format("\t\tnew.\"{0}\"", table.PrimaryKey.Columns[i]));
			}
			for(int i = 0; i < table.Columns.Count; i++) {
				if(IsKey(table, table.Columns[i].Name)) { continue; }
				if(i != 0 || table.PrimaryKey.Columns.Count > 0) { result.AppendLine(","); }
				result.AppendLine(string.Format("\t\told.\"{0}\",", table.Columns[i].Name));
				result.Append(string.Format("\t\tnew.\"{0}\"", table.Columns[i].Name));
			}
			result.AppendLine();
			result.AppendLine("\t);");
		}
		void GenerateInsteadOfDeleteRule(DBTable table, StringBuilder result) {
			string ruleName = ComposeSafeTableName(string.Format("t_{0}_xpoView_delete", table.Name));
			result.AppendLine(string.Format("CREATE RULE \"{0}\" AS", ruleName));
			string viewName = ComposeSafeTableName(string.Format("{0}_xpoView", table.Name));
			result.AppendLine(string.Format("ON DELETE TO \"{0}\" DO INSTEAD", viewName));
			string spName = ComposeSafeTableName(string.Format("sp_{0}_xpoView_delete", table.Name));
			result.AppendLine(string.Format("\tSELECT \"{0}\"(", spName));
			for(int i = 0; i < table.PrimaryKey.Columns.Count; i++) {
				if(i != 0) { result.AppendLine(","); }
				result.Append(string.Format("\t\told.\"{0}\"", table.PrimaryKey.Columns[i]));
			}
			for(int i = 0; i < table.Columns.Count; i++) {
				if(IsKey(table, table.Columns[i].Name)) { continue; }
				if(i != 0 || table.PrimaryKey.Columns.Count > 0) { result.AppendLine(","); }
				result.Append(string.Format("\t\told.\"{0}\"", table.Columns[i].Name));
			}
			result.AppendLine();
			result.AppendLine("\t);");
		}
		static string GetRawType(string type) {
			int braceId = type.IndexOf('(');
			if(braceId < 0) { return type; }
			return type.Substring(0, braceId);
		}
		void AppendKeys(DBTable table, StringBuilder result) {
			for(int i = 0; i < table.PrimaryKey.Columns.Count; i++) {
				if(i != 0) { result.AppendLine(","); }
				DBColumn keyColumn = GetDbColumnByName(table, table.PrimaryKey.Columns[i]);
				string dbType = GetSqlCreateColumnType(table, keyColumn);
				result.Append(string.Format("\t{0}_ {1}", keyColumn.Name, GetRawType(dbType)));
			}
		}
		void AppendWhere(DBTable table, StringBuilder result) {
			for(int i = 0; i < table.PrimaryKey.Columns.Count; i++) {
				if(i != 0) { result.AppendLine(" AND"); }
				result.Append(string.Format("\t\t\"{0}\" = ${1}", table.PrimaryKey.Columns[i], i + 1));
			}
			result.AppendLine();
			result.AppendLine("\t;");
		}
		bool EnableStoredProcedureCompatMode {
			get {
				bool result;
				if(AppContext.TryGetSwitch("Npgsql.EnableStoredProcedureCompatMode", out result)) {
					return result;
				}
				return false;
			}
		}
		readonly HashSet<string> storedFunctions = new HashSet<string>();
		bool IsStoredFunction(string procName) {
			if(!SupportVersion(11, 0)) {
				return true; 
			}
			if(storedFunctions.Contains(procName)) {
				return true;
			}
			string formattedProcName = FormatTable(ComposeSafeSchemaName(procName), ComposeSafeTableName(procName));
			using(IDbCommand command = CreateCommand()) {
				command.CommandText = "select pg_get_function_result(to_regproc(@p1))";
				var param = CreateParameter(command, formattedProcName.ToLower(), "p1", DBColumnType.String, null, formattedProcName.Length);
				command.Parameters.Add(param);
				object o = command.ExecuteScalar();
				bool isStoredFunc = (o != DBNull.Value);
				if(isStoredFunc) {
					storedFunctions.Add(procName);
				}
				return isStoredFunc;
			}
		}
		protected override void SetCommandTextAndTypeForStoredProcedure(IDbCommand command, string sprocName, IList<IDbDataParameter> parameters) {
			if(SupportNpgsqlVersion(7, 0, 0) && !EnableStoredProcedureCompatMode && IsStoredFunction(sprocName)) {
				string formattedProcName = FormatTable(ComposeSafeSchemaName(sprocName), ComposeSafeTableName(sprocName));
				string parametersString = string.Join(", ", parameters.Where(t => t.Direction != ParameterDirection.Output && t.Direction != ParameterDirection.ReturnValue).Select(p => "@" + p.ParameterName));
				command.CommandText = string.Format("select * from {0}({1})", formattedProcName, parametersString);
				command.CommandType = CommandType.Text;
			}
			else {
				base.SetCommandTextAndTypeForStoredProcedure(command, sprocName, parameters);
			}
		}
		void PrepareParametersForExecuteSproc(IDbCommand command, string sprocName, OperandValue[] parameters, out List<IDataParameter> outParameters, out IDataParameter returnParameter) {
			command.CommandType = CommandType.StoredProcedure;
			command.CommandText = FormatTable(ComposeSafeSchemaName(sprocName), ComposeSafeTableName(sprocName));
			CommandBuilderDeriveParameters(command);
			SetCommandTextAndTypeForStoredProcedure(command, sprocName, command.Parameters.Cast<IDbDataParameter>().ToList());
			PrepareParametersForExecuteSproc(parameters, command, out outParameters, out returnParameter);
		}
		protected override SelectedData ExecuteSproc(string sprocName, params OperandValue[] parameters) {
			if(SupportNpgsqlVersion(7, 0, 0) && !EnableStoredProcedureCompatMode && IsStoredFunction(sprocName)) {
				using(IDbCommand command = CreateCommand()) {
					IDataParameter returnParameter;
					List<IDataParameter> outParameters;
					PrepareParametersForExecuteSproc(command, sprocName, parameters, out outParameters, out returnParameter);
					return ExecuteSprocInternal(command, returnParameter, outParameters);
				}
			}
			return base.ExecuteSproc(sprocName, parameters);
		}
		protected override async Task<SelectedData> ExecuteSprocAsync(AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken, string sprocName, params OperandValue[] parameters) {
			if(SupportNpgsqlVersion(7, 0, 0) && !EnableStoredProcedureCompatMode && IsStoredFunction(sprocName)) {
				using(IDbCommand command = CreateCommand()) {
					IDataParameter returnParameter;
					List<IDataParameter> outParameters;
					PrepareParametersForExecuteSproc(command, sprocName, parameters, out outParameters, out returnParameter);
					return await ExecuteSprocInternalAsync(command, returnParameter, outParameters, asyncOperationId, cancellationToken).ConfigureAwait(false);
				}
			}
			return await base.ExecuteSprocAsync(asyncOperationId, cancellationToken, sprocName, parameters);
		}
		ExecMethodDelegate commandBuilderDeriveParametersHandler;
		protected override void CommandBuilderDeriveParameters(IDbCommand command) {
			if(commandBuilderDeriveParametersHandler == null) {
#if DEBUGTEST
				string assemblyName = command.GetType().Assembly.FullName;
#else
				string assemblyName = "Npgsql";
#endif
				commandBuilderDeriveParametersHandler = ReflectConnectionHelper.GetCommandBuilderDeriveParametersDelegate(assemblyName, "Npgsql.NpgsqlCommandBuilder");
			}
			commandBuilderDeriveParametersHandler(command);
		}
		public override DBStoredProcedure[] GetStoredProcedures() {
			List<DBStoredProcedure> result = new List<DBStoredProcedure>();
			Query query = new Query(@"SELECT  proname 
FROM    pg_catalog.pg_namespace n
JOIN    pg_catalog.pg_proc p
ON      pronamespace = n.oid
WHERE   nspname = @p0",
							new QueryParameterCollection(CreateParameterForSystemQuery(0, ObjectsOwner)), new string[] { "@p0" });
			SelectStatementResult data = SelectData(query);
			foreach(SelectStatementResultRow row in data.Rows) {
				result.Add(new DBStoredProcedure() {
					Name = Convert.ToString(row.Values[0])
				});
			}
			foreach(DBStoredProcedure sproc in result) {
				try {
					string sprocName = FormatTable(ComposeSafeSchemaName(sproc.Name), ComposeSafeTableName(sproc.Name));
					List<string> fakeParams = new List<string>();
					using(var command = Connection.CreateCommand()) {
						List<DBStoredProcedureArgument> dbArguments = new List<DBStoredProcedureArgument>();
						command.CommandType = CommandType.StoredProcedure;
						command.CommandText = sprocName;
						CommandBuilderDeriveParameters(command);
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
						sproc.Arguments.AddRange(dbArguments);
					}
					using(var command = Connection.CreateCommand()) {
						command.CommandType = CommandType.Text;
						command.CommandText = string.Format("select * from {0}({1}) where 1=0", sprocName, string.Join(", ", fakeParams.ToArray()));
						DBStoredProcedureResultSet curResultSet = new DBStoredProcedureResultSet();
						using(var reader = command.ExecuteReader()) {
							List<DBNameTypePair> dbColumns = new List<DBNameTypePair>();
							for(int i = 0; i < reader.FieldCount; i++) {
								DBColumnType columnType = DBColumn.GetColumnType(reader.GetFieldType(i));
								dbColumns.Add(new DBNameTypePair(reader.GetName(i), columnType));
							}
							curResultSet.Columns.AddRange(dbColumns);
						}
						sproc.ResultSets.Add(curResultSet);
					}
				}
				catch { }
			}
			return result.ToArray();
		}
	}
	public class PostgreSqlProviderFactory : ProviderFactory {
		public override IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return PostgreSqlConnectionProvider.CreateProviderFromConnection(connection, autoCreateOption);
		}
		public override IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			return PostgreSqlConnectionProvider.CreateProviderFromString(connectionString, autoCreateOption, out objectsToDisposeOnDisconnect);
		}
		public override string GetConnectionString(Dictionary<string, string> parameters) {
			if(!parameters.ContainsKey(ServerParamID) || !parameters.ContainsKey(DatabaseParamID) ||
				!parameters.ContainsKey(UserIDParamID) || !parameters.ContainsKey(PasswordParamID)) {
				return null;
			}
			string port;
			if(parameters.TryGetValue(PortParamID, out port)) {
				return PostgreSqlConnectionProvider.GetConnectionString(parameters[ServerParamID], Convert.ToInt32(port, CultureInfo.InvariantCulture),
					parameters[UserIDParamID], parameters[PasswordParamID], parameters[DatabaseParamID]);
			}
			return PostgreSqlConnectionProvider.GetConnectionString(parameters[ServerParamID], parameters[UserIDParamID],
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
		public override bool HasPort { get { return true; } }
		public override bool HasUserName { get { return true; } }
		public override bool HasPassword { get { return true; } }
		public override bool HasIntegratedSecurity { get { return false; } }
		public override bool HasMultipleDatabases { get { return true; } }
		public override bool IsServerbased { get { return true; } }
		public override bool IsFilebased { get { return false; } }
		public override string ProviderKey { get { return PostgreSqlConnectionProvider.XpoProviderTypeString; } }
		public override string[] GetDatabases(string server, int port, string userId, string password) {
			string connectionString;
			if(port != 0) {
				connectionString = PostgreSqlConnectionProvider.GetConnectionString(server, port, userId, password, "postgres");
			}
			else {
				connectionString = PostgreSqlConnectionProvider.GetConnectionString(server, userId, password, "postgres");
			}
			ConnectionStringParser helper = new ConnectionStringParser(connectionString);
			helper.RemovePartByName(DataStoreBase.XpoProviderTypeParameterName);
			connectionString = helper.GetConnectionString();
			using(IDbConnection conn = PostgreSqlConnectionProvider.CreateConnection(connectionString)) {
				try {
					conn.Open();
					using(IDbCommand cmd = conn.CreateCommand()) {
						cmd.CommandText = "select datname from pg_database order by datname";
						using(IDataReader reader = cmd.ExecuteReader()) {
							var result = new List<string>();
							while(reader.Read()) {
								result.Add((string)reader.GetValue(0));
							}
							return result.ToArray();
						}
					}
				}
				catch {
					return Array.Empty<string>();
				}
			}
		}
		public override string[] GetDatabases(string server, string userId, string password) {
			return GetDatabases(server, 0, userId, password);
		}
		public override string FileFilter { get { return null; } }
		public override bool MeanSchemaGeneration { get { return true; } }
		public override bool SupportStoredProcedures { get { return false; } }
	}
#pragma warning restore DX0024
}
namespace DevExpress.Xpo.DB.Helpers {
	using System;
	using System.Collections.Generic;
	using System.Data;
	using DevExpress.Xpo.DB;
	interface IDbTypeMapperPostgreSql {
		void SetNpgsqlDbTypeChar(IDbDataParameter param, int size);
		void SetNpgsqlDbTypeInterval(IDbDataParameter param);
		void ResetNpgsqlDbType(IDbDataParameter param);
	}
	class DbTypeMapperPostgresql<TSqlDbTypeEnum, TSqlParameter> : DbTypeMapper<TSqlDbTypeEnum, TSqlParameter>, IDbTypeMapperPostgreSql
		where TSqlDbTypeEnum : struct
		where TSqlParameter : IDbDataParameter {
		static readonly TSqlDbTypeEnum NpgSqlTypeText;
		static readonly TSqlDbTypeEnum NpgSqlTypeChar;
		static readonly TSqlDbTypeEnum NpgSqlTypeInterval;
		bool legacyGuidSupport;
		static DbTypeMapperPostgresql() {
			NpgSqlTypeText = (TSqlDbTypeEnum)Enum.Parse(typeof(TSqlDbTypeEnum), "Text");
			NpgSqlTypeChar = (TSqlDbTypeEnum)Enum.Parse(typeof(TSqlDbTypeEnum), "Char");
			NpgSqlTypeInterval = (TSqlDbTypeEnum)Enum.Parse(typeof(TSqlDbTypeEnum), "Interval");
		}
		public DbTypeMapperPostgresql(bool legacyGuidSupport) {
			this.legacyGuidSupport = legacyGuidSupport;
		}
		protected override string ParameterDbTypePropertyName { get { return "NpgsqlDbType"; } }
		protected override string GetParameterTypeNameForBoolean(out int? size, out byte? precision, out byte? scale) {
			size = null;
			precision = scale = null;
			return "Boolean";
		}
		protected override string GetParameterTypeNameForByte(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Smallint";
		}
		protected override string GetParameterTypeNameForByteArray(out int? size) {
			size = null;
			return "Bytea";
		}
		protected override string GetParameterTypeNameForChar(out int? size) {
			size = 1;
			return "Char";
		}
		protected override string GetParameterTypeNameForDateTime() {
			return "Timestamp";
		}
		protected override string GetParameterTypeNameForDecimal(out byte? precision, out byte? scale) {
			precision = 28;
			scale = 8;
			return "Numeric";
		}
		protected override string GetParameterTypeNameForDouble(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Double";
		}
		protected override string GetParameterTypeNameForGuid(out int? size) {
			if(!legacyGuidSupport) {
				size = null;
				return "Uuid";
			}
			size = 36;
			return "Char";
		}
		protected override string GetParameterTypeNameForInt16(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Smallint";
		}
		protected override string GetParameterTypeNameForInt32(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Integer";
		}
		protected override string GetParameterTypeNameForInt64(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Bigint";
		}
		protected override string GetParameterTypeNameForSByte(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Smallint";
		}
		protected override string GetParameterTypeNameForSingle(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Real";
		}
		protected override string GetParameterTypeNameForString(out int? size) {
			size = null;
			return "Varchar";
		}
		protected override string GetParameterTypeNameForTimeSpan() {
			return "Double";
		}
		protected override string GetParameterTypeNameForUInt16(out byte? precision, out byte? scale) {
			precision = 5;
			scale = 0;
			return "Numeric";
		}
		protected override string GetParameterTypeNameForUInt32(out byte? precision, out byte? scale) {
			precision = 10;
			scale = 0;
			return "Numeric";
		}
		protected override string GetParameterTypeNameForUInt64(out byte? precision, out byte? scale) {
			precision = 20;
			scale = 0;
			return "Numeric";
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
			switch(sqlType.ToUpperInvariant()) {
				case "BOOLEAN":
					return "Boolean";
				case "SMALLINT":
					return "Smallint";
				case "INTEGER":
					return "Integer";
				case "BIGINT":
					return "Bigint";
				case "REAL":
					return "Real";
				case "DOUBLE PRECISION":
					return "Double";
				case "NUMERIC":
					return "Numeric";
				case "MONEY":
					return "Money";
				case "TEXT":
					return "Text";
				case "CHARACTER VARYING":
				case "VARCHAR":
					return "Varchar";
				case "CHARACTER":
				case "CHAR":
					return "Char";
				case "CITEXT":
					return "Citext";
				case "JSON":
					return "Json";
				case "JSONB":
					return "Jsonb";
				case "XML":
					return "Xml";
				case "POINT":
					return "Point";
				case "LSEG":
					return "LSeg";
				case "PATH":
					return "Path";
				case "POLYGON":
					return "Polygon";
				case "LINE":
					return "Line";
				case "CIRCLE":
					return "Circle";
				case "BOX":
					return "Box";
				case "BIT":
					return "Bit";
				case "BIT VARYING":
					return "VarBit";
				case "HSTORE":
					return "Hstore";
				case "UUID":
					return "Uuid";
				case "CIDR":
					return "Cidr";
				case "INET":
					return "Inet";
				case "MACADDR":
					return "MacAddr";
				case "TSQUERY":
					return "TsQuery";
				case "TSVECTOR":
					return "TsVector";
				case "DATE":
					return "Date";
				case "INTERVAL":
					return "Interval";
				case "TIMESTAMP":
				case "TIMESTAMP WITHOUT TIME ZONE":
					return "Timestamp";
				case "TIMESTAMP WITH TIME ZONE":
					return "TimestampTz";
				case "TIME":
				case "TIME WITHOUT TIME ZONE":
					return "Time";
				case "TIME WITH TIME ZONE":
					return "TimeTz";
				case "BYTEA":
					return "Bytea";
				case "OID":
					return "Oid";
				case "XID":
					return "Xid";
				case "CID":
					return "Cid";
				case "OIDVECTOR":
					return "Oidvector";
				case "NAME":
					return "Name";
				case "DECIMAL":
					return "Numeric";
				default:
					return null;
			}
		}
		public override void SetParameterTypeAndSize(IDbDataParameter parameter, DBColumnType dbColumnType, int size) {
			if(dbColumnType == DBColumnType.String) {
				if(size <= 0 || size > PostgreSqlConnectionProvider.MaximumStringSize) {
					SetSqlDbTypeHandler((TSqlParameter)parameter, NpgSqlTypeText);
					return;
				}
			}
			base.SetParameterTypeAndSize(parameter, dbColumnType, size);
		}
		public override void SetParameterType(IDbDataParameter parameter, DBColumnType dbColumnType) {
			if(legacyGuidSupport && dbColumnType == DBColumnType.Guid) {
				SetParameterTypeAndSize(parameter, dbColumnType, 0);
				return;
			}
			base.SetParameterType(parameter, dbColumnType);
		}
		public void SetNpgsqlDbTypeChar(IDbDataParameter param, int size) {
			SetSqlDbTypeHandler((TSqlParameter)param, NpgSqlTypeChar);
			param.Size = size;
		}
		public void SetNpgsqlDbTypeInterval(IDbDataParameter param) {
			SetSqlDbTypeHandler((TSqlParameter)param, NpgSqlTypeInterval);
		}
		public void ResetNpgsqlDbType(IDbDataParameter param) {
			var npgSqlParam = (TSqlParameter)param;
			SetSqlDbTypeHandler(npgSqlParam, GetSqlDbTypeHandler(npgSqlParam));
		}
	}
	class PostgreSqlDBSchemaComparer : DBSchemaComparerSql {
		public PostgreSqlDBSchemaComparer(Func<string, string> tableNameMangling, Func<string, string> columnNameMangling, Func<DBTable, DBColumn, string> getSqlCreateColumnType)
			: this(tableNameMangling, columnNameMangling, getSqlCreateColumnType, DefaultDbTypeMapper.Instance.ParseSqlType) {
		}
		public PostgreSqlDBSchemaComparer(Func<string, string> tableNameMangling, Func<string, string> columnNameMangling, Func<DBTable, DBColumn, string> getSqlCreateColumnType,
			Func<string, DbTypeMapperBase.DBTypeInfoBase> parseSqlType)
			: base(tableNameMangling, columnNameMangling, getSqlCreateColumnType, parseSqlType) {
			AddCompatibleSqlTypeMapping("timestamp", "timestamp without time zone");
			AddCompatibleSqlTypeMapping("char", "character");
			AddCompatibleSqlTypeMapping("varchar", "character varying");
		}
		protected override bool IsSqlTypesCompatible(string sqlTypeX, string sqlTypeY) {
			if(base.IsSqlTypesCompatible(sqlTypeX, sqlTypeY)) {
				return true;
			}
			string typeX, sizeX, suffixX;
			SplitSqlType(sqlTypeX, out typeX, out sizeX, out suffixX);
			string typeY, sizeY, suffixY;
			SplitSqlType(sqlTypeY, out typeY, out sizeY, out suffixY);
			return base.IsSqlTypesCompatible(typeX, typeY)
				&& string.Equals(sizeX, sizeY, StringComparison.OrdinalIgnoreCase)
				&& string.Equals(suffixX, suffixY, StringComparison.OrdinalIgnoreCase);
		}
		void SplitSqlType(string sqlType, out string type, out string size, out string suffix) {
			int p1 = sqlType.IndexOf('(');
			int p2 = sqlType.IndexOf(')');
			if(p2 > p1 && p1 != 1) {
				size = sqlType.Substring(p1 + 1, p2 - p1 - 1);
				suffix = sqlType.Substring(p2 + 1);
				type = sqlType.Substring(0, p1);
			}
			else {
				type = sqlType;
				size = "";
				suffix = "";
			}
		}
	}
	class PostgreSqlUpdateSchemaSqlFormatterHelper : UpdateSchemaSqlFormatterHelper {
		Func<DBTable, DBColumn, string> getSqlCreateColumnType;
		public PostgreSqlUpdateSchemaSqlFormatterHelper(
			ISqlGeneratorFormatter sqlGeneratorFormatter,
			Func<DBTable, DBColumn, bool, string> getSqlCreateColumnFullAttributes,
			Func<string, string> formatConstraintSafe,
			Func<DBIndex, DBTable, string> getIndexName,
			Func<DBForeignKey, DBTable, string> getForeignKeyName,
			Func<DBPrimaryKey, DBTable, string> getPrimaryKeyName,
			Func<DBTable, DBColumn, string> getSqlCreateColumnType)
			: base(sqlGeneratorFormatter, getSqlCreateColumnFullAttributes,
				formatConstraintSafe, getIndexName, getForeignKeyName, getPrimaryKeyName) {
			this.getSqlCreateColumnType = getSqlCreateColumnType;
		}
		protected override string[] FormatRenameTable(RenameTableStatement statement) {
			return new string[] {
				string.Format("alter table {0} rename to {1}", FormatTableSafe(statement.Table.Name), SqlGeneratorFormatter.ComposeSafeTableName(statement.NewTableName))
			};
		}
		protected override string[] FormatRenameColumn(string tableName, string oldColumnName, string newColumnName) {
			newColumnName = SqlGeneratorFormatter.FormatColumn(SqlGeneratorFormatter.ComposeSafeColumnName(newColumnName));
			oldColumnName = SqlGeneratorFormatter.FormatColumn(SqlGeneratorFormatter.ComposeSafeColumnName(oldColumnName));
			return new string[]{
			   string.Format("alter table {0} rename column {1} to {2}", FormatTableSafe(tableName), oldColumnName, newColumnName)
			};
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
		string ChangeColumnSqlTypeToCompatible(string sqlType) {
			const string characterVarying = "character varying(";
			const string character = "character(";
			const string timestampWithoutTz = "timestamp without time zone";
			if(sqlType.StartsWith(characterVarying, StringComparison.OrdinalIgnoreCase)) {
				return "varchar(" + sqlType.Substring(characterVarying.Length);
			}
			else if(sqlType.StartsWith(character)) {
				return "char(" + sqlType.Substring(character.Length);
			}
			else if(string.Equals(sqlType, timestampWithoutTz, StringComparison.OrdinalIgnoreCase)) {
				return "timestamp";
			}
			return sqlType;
		}
		protected override string[] FormatAlterColumn(AlterColumnStatement statement) {
			var statements = new List<string>(1);
			string newColumnName = SqlGeneratorFormatter.ComposeSafeColumnName(statement.NewColumn.Name);
			string oldColumnName = SqlGeneratorFormatter.ComposeSafeColumnName(statement.OldColumn.Name);
			if(!string.Equals(oldColumnName, newColumnName, StringComparison.OrdinalIgnoreCase)) {
				statements.AddRange(FormatRenameColumn(statement.Table.Name, oldColumnName, newColumnName));
			}
			if(statement.OldColumn.IsIdentity == statement.NewColumn.IsIdentity) {
				string oldColumnSql = ChangeColumnSqlTypeToCompatible(getSqlCreateColumnType(statement.Table, statement.OldColumn));
				string newColumnSql = ChangeColumnSqlTypeToCompatible(getSqlCreateColumnType(statement.Table, statement.NewColumn));
				if(oldColumnSql != newColumnSql) {
					string sql = string.Format(AlterColumnTemplate,
						 FormatTableSafe(statement.Table.Name), FormatColumnSafe(newColumnName), newColumnSql);
					statements.Add(sql);
				}
			}
			else {
				DBColumn tmpColumn = new DBColumn() {
					Name = SqlGeneratorFormatter.ComposeSafeColumnName(string.Format("{0}Temporary{1}", statement.OldColumn.Name, Guid.NewGuid())),
					ColumnType = statement.NewColumn.ColumnType,
					DBTypeName = statement.NewColumn.DBTypeName,
					Size = statement.NewColumn.Size,
					DbDefaultValue = statement.NewColumn.DbDefaultValue,
					DefaultValue = statement.NewColumn.DefaultValue,
					IsIdentity = false,
					IsKey = false,
					IsNullable = true,
				};
				if(statement.NewColumn.IsIdentity) {
					tmpColumn.IsNullable = statement.NewColumn.IsNullable;
					string newColumnSql = (tmpColumn.ColumnType == DBColumnType.Int64) ? "bigserial" : "serial";
					statements.Add(string.Format("alter table {0} add {1} {2}",
						FormatTableSafe(statement.Table.Name), FormatColumnSafe(tmpColumn.Name), newColumnSql));
				}
				else {
					statements.AddRange(FormatCreateColumn(new CreateColumnStatement(statement.Table, tmpColumn)));
					statements.Add(string.Format("update {0} set {1}={2}", FormatTableSafe(statement.Table.Name), FormatColumnSafe(tmpColumn.Name), FormatColumnSafe(statement.OldColumn.Name)));
					if(!statement.NewColumn.IsNullable || statement.NewColumn.IsKey) {
						tmpColumn.IsNullable = false;
						string newColumnSql = getSqlCreateColumnType(statement.Table, statement.NewColumn);
						statements.Add(string.Format(AlterColumnTemplate, FormatTableSafe(statement.Table.Name), FormatColumnSafe(tmpColumn.Name), newColumnSql));
					}
				}
				statements.AddRange(FormatDropColumn(new DropColumnStatement(statement.Table, statement.OldColumn.Name)));
				statements.AddRange(FormatRenameColumn(statement.Table.Name, tmpColumn.Name, statement.NewColumn.Name));
			}
			if(statement.OldColumn.IsNullable != statement.NewColumn.IsNullable) {
				string sql;
				if(statement.NewColumn.IsNullable) {
					sql = "alter table {0} alter column {1} drop not null";
				}
				else {
					sql = "alter table {0} alter column {1} set not null";
				}
				statements.Add(string.Format(sql, FormatTableSafe(statement.Table.Name), FormatColumnSafe(newColumnName)));
			}
			return statements.ToArray();
		}
		protected override string[] FormatDropIndex(DropIndexStatement statement) {
			return new string[] {
				string.Format("drop index {0}", FormatConstraintSafe(statement.IndexName))
			};
		}
		protected override string AlterColumnTemplate {
			get { return "alter table {0} alter column {1} type {2}"; }
		}
	}
}
