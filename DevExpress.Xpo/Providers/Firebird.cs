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
	public enum FirebirdServerType { Server = 0, Embedded = 1 }
#pragma warning disable DX0024
	public class FirebirdConnectionProvider : ConnectionProviderSql {
		public const string XpoProviderTypeString = "Firebird";
		public static string GlobalDefaultCharacterSet = null;
		public string DefaultCharacterSet = null;
		const string CharacterSetIfDefaultNotSet = "UTF8";
		string CharacterSet {
			get {
				if(DefaultCharacterSet != null || GlobalDefaultCharacterSet != null) {
					return DefaultCharacterSet != null ? DefaultCharacterSet : GlobalDefaultCharacterSet;
				}
				else {
					return CharacterSetIfDefaultNotSet;
				}
			}
		}
		ReflectConnectionHelper helper;
		ReflectConnectionHelper ConnectionHelper {
			get {
				if(helper == null)
					helper = new ReflectConnectionHelper(Connection, "FirebirdSql.Data.FirebirdClient.FbException");
				return helper;
			}
		}
		DbTypeMapperBase dbTypeMapper;
		protected override DbTypeMapperBase DbTypeMapper {
			get {
				if(dbTypeMapper == null) {
					Type fbParameterType = ConnectionHelper.GetType("FirebirdSql.Data.FirebirdClient.FbParameter");
					Type fbTypeType = ConnectionHelper.GetType("FirebirdSql.Data.FirebirdClient.FbDbType");
					dbTypeMapper = (DbTypeMapperBase)Activator.CreateInstance(typeof(DbTypeMapperFirebird<,>).MakeGenericType(fbTypeType, fbParameterType));
				}
				return dbTypeMapper;
			}
		}
		public static string GetConnectionString(string server, string userId, string password, string database, FirebirdServerType serverType, string charset) {
			string serverTypeString = (serverType == FirebirdServerType.Server) ? "Default" : serverType.ToString();
			return string.Format("{5}={6};DataSource={0};User={1};Password={2};Database={3};ServerType={4};Charset={7}",
				EscapeConnectionStringArgument(server), EscapeConnectionStringArgument(userId), EscapeConnectionStringArgument(password), EscapeConnectionStringArgument(database), serverTypeString, DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString, EscapeConnectionStringArgument(charset));
		}
		public static string GetConnectionString(string userId, string password, string database) {
			string charset = !string.IsNullOrEmpty(GlobalDefaultCharacterSet) ? GlobalDefaultCharacterSet : CharacterSetIfDefaultNotSet;
			return GetConnectionString("localhost", userId, password, database, FirebirdServerType.Embedded, charset);
		}
		public static string GetConnectionString(string server, string userId, string password, string database) {
			string charset = !string.IsNullOrEmpty(GlobalDefaultCharacterSet) ? GlobalDefaultCharacterSet : CharacterSetIfDefaultNotSet;
			return GetConnectionString(server, userId, password, database, FirebirdServerType.Server, charset);
		}
		public static string GetConnectionString(string server, int port, string userId, string password, string database) {
			return string.Concat(GetConnectionString(server, userId, password, database), ";Port=", port.ToString(CultureInfo.InvariantCulture));
		}
		public static string GetConnectionString(string server, string userId, string password, string database, string charset) {
			return GetConnectionString(server, userId, password, database, FirebirdServerType.Server, charset);
		}
		public static IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			IDbConnection connection = CreateConnection(connectionString);
			objectsToDisposeOnDisconnect = new IDisposable[] { connection };
			return CreateProviderFromConnection(connection, autoCreateOption);
		}
		public static IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return new FirebirdConnectionProvider(connection, autoCreateOption);
		}
		static FirebirdConnectionProvider() {
			RegisterDataStoreProvider(XpoProviderTypeString, new DataStoreCreationFromStringDelegate(CreateProviderFromString));
			RegisterDataStoreProvider("FbConnection", new DataStoreCreationFromConnectionDelegate(CreateProviderFromConnection));
			RegisterFactory(new FirebirdProviderFactory());
		}
		public static void Register() { }
		public FirebirdConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption)
			: base(connection, autoCreateOption, true) {
			ReadDbVersion(connection);
		}
		protected FirebirdConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption, bool openConnection)
			: base(connection, autoCreateOption, openConnection) {
			if(openConnection)
				ReadDbVersion(connection);
		}
		decimal? versionMajor;
		int versionMinor;
		protected void ReadDbVersion(IDbConnection conn) {
			try {
				using(IDbCommand c = CreateCommand(new Query("SELECT rdb$get_context('SYSTEM', 'ENGINE_VERSION') as version from rdb$database"))) {
					object result = c.ExecuteScalar();
					if(result != null && result is string) {
						SetServerVersionInternal((string)result);
					}
				}
			}
			catch { }
		}
		bool SetServerVersionInternal(string versionString) {
			string[] versionParts = versionString.Split('.');
			decimal versionMajorLocal;
			if(versionParts.Length == 3 && decimal.TryParse(string.Concat(versionParts[0], ".", versionParts[1]), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out versionMajorLocal) && int.TryParse(versionParts[2], out versionMinor)) {
				versionMajor = versionMajorLocal;
				return true;
			}
			return false;
		}
		bool SupportVersion(decimal major, int minor) {
			if(!versionMajor.HasValue)
				return false;
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
			return "char(1)";
		}
		protected override string GetSqlCreateColumnTypeForByte(DBTable table, DBColumn column) {
			return "numeric(3,0)";
		}
		protected override string GetSqlCreateColumnTypeForSByte(DBTable table, DBColumn column) {
			return "numeric(3,0)";
		}
		protected override string GetSqlCreateColumnTypeForChar(DBTable table, DBColumn column) {
			if(CharacterSet != string.Empty) {
				return string.Format("char CHARACTER SET {0}", CharacterSet);
			}
			else {
				return "char";
			}
		}
		protected override string GetSqlCreateColumnTypeForDecimal(DBTable table, DBColumn column) {
			return "decimal(18,4)";
		}
		protected override string GetSqlCreateColumnTypeForDouble(DBTable table, DBColumn column) {
			return "double precision";
		}
		protected override string GetSqlCreateColumnTypeForSingle(DBTable table, DBColumn column) {
			return "float";
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
			return "numeric(18,0)";
		}
		public const int MaximumStringSize = 4000;
		protected override string GetSqlCreateColumnTypeForString(DBTable table, DBColumn column) {
			if(column.Size > 0 && column.Size <= MaximumStringSize) {
				string sql = string.Concat("char varying (", column.Size.ToString(CultureInfo.InvariantCulture), ")");
				if(CharacterSet != string.Empty) {
					sql = string.Concat(sql, " CHARACTER SET ", CharacterSet);
				}
				return sql;
			}
			else {
				return "BLOB SUB_TYPE TEXT";
			}
		}
		protected override string GetSqlCreateColumnTypeForDateTime(DBTable table, DBColumn column) {
			return "timestamp";
		}
		protected override string GetSqlCreateColumnTypeForDateOnly(DBTable table, DBColumn column) {
			return "date";
		}
		protected override string GetSqlCreateColumnTypeForTimeOnly(DBTable table, DBColumn column) {
			return "time";
		}
		protected override string GetSqlCreateColumnTypeForGuid(DBTable table, DBColumn column) {
			return "char(36)";
		}
		protected override string GetSqlCreateColumnTypeForByteArray(DBTable table, DBColumn column) {
			return "blob";
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
				}
				else {
					if(column.DefaultValue != null && column.DefaultValue != System.DBNull.Value) {
						string formattedDefaultValue = FormatConstant(column.DefaultValue);
						result += string.Concat(" DEFAULT ", formattedDefaultValue);
					}
				}
			}
			if(column.IsKey || !column.IsNullable) {
				result += " NOT NULL";
			}
			return result;
		}
		protected override object ReformatReadValue(object value, ConnectionProviderSql.ReformatReadValueArgs args) {
			if(args.DbTypeCode == TypeCode.String) {
				if(args.TargetTypeCode == TypeCode.Boolean) {
					return ((string)value).TrimEnd() != "0";
				}
				else if(args.TargetType == typeof(TimeOnly)) {
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
			else if(args.DbTypeCode == TypeCode.Object) {
				if(value is System.Numerics.BigInteger) {
					var bi = (System.Numerics.BigInteger)value;
#if NET
					int size = bi.GetByteCount();
#else
					int size = bi.ToByteArray().Length;
#endif
					switch(args.TargetTypeCode) {
						case TypeCode.Int64:
							if(size <= sizeof(long))
								return (long)bi;
							break;
						case TypeCode.UInt64:
							if(size <= sizeof(ulong))
								return (ulong)bi;
							break;
						case TypeCode.Int32:
							if(size <= sizeof(int))
								return (int)bi;
							break;
						case TypeCode.UInt32:
							if(size <= sizeof(uint))
								return (uint)bi;
							break;
						case TypeCode.Int16:
							if(size <= sizeof(short))
								return (short)bi;
							break;
						case TypeCode.UInt16:
							if(size <= sizeof(ushort))
								return (ushort)bi;
							break;
						case TypeCode.Byte:
							if(size <= sizeof(byte))
								return (byte)bi;
							break;
						case TypeCode.SByte:
							if(size <= sizeof(sbyte))
								return (sbyte)bi;
							break;
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
						return DateTime.SpecifyKind(((DateOnly)clientValue).ToDateTime(TimeOnly.MinValue), DateTimeKind.Local);
					}
					else if(clientValue is TimeOnly) {
						return ((TimeOnly)clientValue).ToTimeSpan();
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
				case TypeCode.Single:
					return (Double)(Single)clientValue;
				case TypeCode.Boolean:
					return ((Boolean)clientValue) ? "1" : "0";
			}
			return base.ConvertToDbParameter(clientValue, clientValueTypeCode);
		}
		protected virtual string GetSeqName(string tableName) {
			return ComposeSafeConstraintName("sq_" + tableName);
		}
		class IdentityInsertSqlGenerator : BaseObjectSqlGenerator {
			protected override string InternalGenerateSql() {
				StringBuilder names = new StringBuilder();
				StringBuilder values = new StringBuilder();
				for(int i = 0; i < Root.Operands.Count; i++) {
					names.AppendFormat(CultureInfo.InvariantCulture, "{0},", Process(Root.Operands[i]));
					values.Append(GetNextParameterName(((InsertStatement)Root).Parameters[i]) + ",");
				}
				names.AppendFormat(CultureInfo.InvariantCulture, "\"{0}\",", formatter.ComposeSafeColumnName(((InsertStatement)Root).IdentityColumn));
				values.Append("@seq,");
				return formatter.FormatInsert(formatter.FormatTable(formatter.ComposeSafeSchemaName(Root.Table.Name), formatter.ComposeSafeTableName(Root.Table.Name)),
					names.ToString(0, names.Length - 1),
					values.ToString(0, values.Length - 1));
			}
			public IdentityInsertSqlGenerator(ISqlGeneratorFormatter formatter, TaggedParametersHolder identitiesByTag)
				: base(formatter, identitiesByTag, new Dictionary<OperandValue, string>()) {
			}
		}
		Query GetIdentityPrepareQuery(InsertStatement root, object identityValue, TaggedParametersHolder identitiesByTag, out long outResultId) {
			Query sql = new IdentityInsertSqlGenerator(this, identitiesByTag).GenerateSql(root);
			switch(root.IdentityColumnType) {
				case DBColumnType.Int32: {
					int id = ((IConvertible)identityValue).ToInt32(CultureInfo.InvariantCulture);
					sql.Parameters.Add(new OperandValue(id));
					outResultId = id;
				}
				break;
				case DBColumnType.Int64: {
					long id = ((IConvertible)identityValue).ToInt64(CultureInfo.InvariantCulture);
					sql.Parameters.Add(new OperandValue(id));
					outResultId = id;
				}
				break;
				default:
					throw new NotSupportedException();
			}
			sql.ParametersNames.Add("@seq");
			return sql;
		}
		protected override Int64 GetIdentity(InsertStatement root, TaggedParametersHolder identitiesByTag) {
			string seq = GetSeqName(root.Table.Name);
			object value = GetScalar(new Query(string.Concat("select GEN_ID(\"", seq, "\", 1) from RDB$DATABASE")));
			long resultId;
			Query sql = GetIdentityPrepareQuery(root, value, identitiesByTag, out resultId);
			ExecSql(sql);
			return resultId;
		}
		protected override async Task<Int64> GetIdentityAsync(InsertStatement root, TaggedParametersHolder identitiesByTag, AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken) {
			string seq = GetSeqName(root.Table.Name);
			object value = await GetScalarAsync(new Query(string.Concat("select GEN_ID(\"", seq, "\", 1) from RDB$DATABASE")), asyncOperationId, cancellationToken).ConfigureAwait(false);
			long resultId;
			Query sql = GetIdentityPrepareQuery(root, value, identitiesByTag, out resultId);
			await ExecSqlAsync(sql, asyncOperationId, cancellationToken).ConfigureAwait(false);
			return resultId;
		}
		protected override Exception WrapException(Exception e, IDbCommand query) {
			object o;
			if(ConnectionHelper.TryGetExceptionProperty(e, "Errors", out o)) {
				foreach(object error in (IEnumerable)o) {
					int number = (int)ReflectConnectionHelper.GetPropertyValue(error, "Number");
					if(number == 0x14000102 || number == 0x14000104 || number == 0x140000f9)
						return new SchemaCorrectionNeededException((string)ReflectConnectionHelper.GetPropertyValue(error, "Message"), e);
					if(number == 0x14000092 || number == 0x14000159 || number == 0x1400001d)
						return new ConstraintViolationException(query.CommandText, GetParametersString(query), e);
				}
			}
			return base.WrapException(e, query);
		}
		protected override IDbConnection CreateConnection() {
			return ConnectionHelper.GetConnection(ConnectionString);
		}
		public static IDbConnection CreateConnection(string connectionString) {
			return ReflectConnectionHelper.GetConnection("FirebirdSql.Data.FirebirdClient", "FirebirdSql.Data.FirebirdClient.FbConnection", connectionString);
		}
		Version firebirdClientVersion;
		bool SupportFirebirdClientVersion(int major, int minor, int build) {
			if(firebirdClientVersion == null) {
				firebirdClientVersion = Connection.GetType().Assembly.GetName().Version;
			}
			if(firebirdClientVersion.Major == major) {
				if(firebirdClientVersion.Minor == minor) {
					return firebirdClientVersion.Build >= build;
				}
				return firebirdClientVersion.Minor >= minor;
			}
			return firebirdClientVersion.Major > major;
		}
		protected override void CreateDataBase() {
			const int CannotOpenDatabaseError = 0x14000018;
			using(IDbConnection conn = ConnectionHelper.GetConnection("Pooling=false;" + ConnectionString)) {
				try {
					conn.Open();
				}
				catch(Exception e) {
					object[] values;
					object errorFirstItem;
					if(ConnectionHelper.TryGetExceptionProperties(e, new string[] { "ErrorCode", "Errors" }, out values)
						&& (
						(int)values[0] == CannotOpenDatabaseError ||
						((errorFirstItem = ReflectConnectionHelper.GetCollectionFirstItem((IEnumerable)values[1])) != null)
						&& ((int)ReflectConnectionHelper.GetPropertyValue(errorFirstItem, "Number")) == CannotOpenDatabaseError)
						&& CanCreateDatabase) {
						try {
							if(SupportFirebirdClientVersion(8, 0, 0)) {
								ReflectConnectionHelper.InvokeStaticMethod(ConnectionHelper.ConnectionType, "CreateDatabase", new object[] { ConnectionString, 4096, true, true }, true);
							}
							else {
								ReflectConnectionHelper.InvokeStaticMethod(ConnectionHelper.ConnectionType, "CreateDatabase", new object[] { ConnectionString, true }, true);
							}
						}
						catch(MissingMethodException) {
							ReflectConnectionHelper.InvokeStaticMethod(ConnectionHelper.ConnectionType, "CreateDatabase", new object[] { ConnectionString }, true);
						}
					}
					else {
						throw new UnableToOpenDatabaseException(XpoDefault.ConnectionStringRemovePassword(ConnectionString), e);
					}
				}
			}
		}
		delegate bool TablesFilter(DBTable table);
		SelectStatementResult GetDataForTables(ICollection tables, TablesFilter filter, string queryText) {
			QueryParameterCollection parameters = new QueryParameterCollection();
			StringCollection inList = new StringCollection();
			int i = 0;
			foreach(DBTable table in tables) {
				if(filter == null || filter(table)) {
					parameters.Add(new OperandValue(ComposeSafeTableName(table.Name)));
					inList.Add("@p" + i.ToString());
					++i;
				}
			}
			if(inList.Count == 0)
				return new SelectStatementResult();
			return SelectData(new Query(string.Format(CultureInfo.InvariantCulture, queryText, StringListHelper.DelimitedText(inList, ",")), parameters, inList));
		}
		enum FirebirdDType : short {
			Short = 7,
			Long = 8,
			Quad = 9,
			Float = 10,
			D_float = 11,
			Sql_date = 12,
			Sql_time = 13,
			Text = 14,
			Text2 = 15,
			Int64 = 16,
			Blob2 = 17,
			Domain_name = 18,
			Domain_name2 = 19,
			No_nullable = 20,
			Column_name = 21,
			Column_name2 = 22,
			Boolean = 23,
			Dec64 = 24,
			Dec128 = 25,
			Dec_fixed = 26,
			Double = 27,
			Sql_time_tz = 28,
			Timestamp_tz = 29,
			Timestamp = 35,
			Varying = 37,
			Varying2 = 38,
			CString = 40,
			CString2 = 41,
			Blob_id = 45,
			Blob = 261
		}
		string GetTypeNameFromCode(FirebirdDType type, short subType, short size, short precision, short scale) {
			switch(type) {
				case FirebirdDType.Short:
				case FirebirdDType.Long:
				case FirebirdDType.Quad:
				case FirebirdDType.Int64:
				case FirebirdDType.Blob_id:
					if(subType == 2) {
						return string.Format(CultureInfo.InvariantCulture, "decimal({0},{1})", precision, -scale);
					}
					if(subType == 1 || scale != 0) {
						return string.Format(CultureInfo.InvariantCulture, "numeric({0},{1})", precision, -scale);
					}
					break;
			}
			switch(type) {
				case FirebirdDType.Short:
					return "smallint";
				case FirebirdDType.Long:
					return "integer";
				case FirebirdDType.Quad:
					return "quad";
				case FirebirdDType.Int64:
					return "bigint";
				case FirebirdDType.Blob_id:
					return "blob_id";
				case FirebirdDType.Float:
					return "float";
				case FirebirdDType.D_float:
				case FirebirdDType.Double:
					return "double precision";
				case FirebirdDType.Sql_date:
					return "date";
				case FirebirdDType.Sql_time:
					return "time";
				case FirebirdDType.Text:
				case FirebirdDType.Text2:
					return string.Concat("char(", size.ToString(CultureInfo.InvariantCulture), ")");
				case FirebirdDType.Boolean:
					return "boolean";
				case FirebirdDType.Timestamp:
					return "timestamp";
				case FirebirdDType.Varying:
				case FirebirdDType.Varying2:
					return string.Concat("char varying (", size.ToString(CultureInfo.InvariantCulture), ")");
				case FirebirdDType.CString:
				case FirebirdDType.CString2:
					return string.Concat("cstring(", size.ToString(CultureInfo.InvariantCulture), ")");
				case FirebirdDType.Blob:
					if(subType == 1) {
						return "blob sub_type text";
					}
					return "blob";
			}
			return string.Empty;
		}
		DBColumnType GetTypeFromCode(FirebirdDType type, short subType, short size, short scale) {
			switch(type) {
				case FirebirdDType.Short:
				case FirebirdDType.Long:
				case FirebirdDType.Quad:
				case FirebirdDType.Int64:
				case FirebirdDType.Blob_id:
					if(subType == 2 || subType == 1 || scale != 0) {
						return DBColumnType.Decimal;
					}
					break;
			}
			switch(type) {
				case FirebirdDType.Short:
					return DBColumnType.Int16;
				case FirebirdDType.Long:
					return DBColumnType.Int32;
				case FirebirdDType.Quad:
				case FirebirdDType.Int64:
				case FirebirdDType.Blob_id:
					return DBColumnType.Int64;
				case FirebirdDType.Float:
					return DBColumnType.Single;
				case FirebirdDType.D_float:
				case FirebirdDType.Double:
					return DBColumnType.Double;
				case FirebirdDType.Sql_date:
					return !GlobalUseLegacyDateOnlyAndTimeOnlySupport ? DBColumnType.DateOnly : DBColumnType.DateTime;
				case FirebirdDType.Sql_time:
					return !GlobalUseLegacyDateOnlyAndTimeOnlySupport ? DBColumnType.TimeOnly : DBColumnType.DateTime;
				case FirebirdDType.Timestamp:
				case FirebirdDType.Timestamp_tz:
				case FirebirdDType.Sql_time_tz:
					return DBColumnType.DateTime;
				case FirebirdDType.Text:
				case FirebirdDType.Text2:
					return size == 1 ? DBColumnType.Char : DBColumnType.String;
				case FirebirdDType.Boolean:
					return DBColumnType.Boolean;
				case FirebirdDType.Varying:
				case FirebirdDType.Varying2:
					return DBColumnType.String;
				case FirebirdDType.CString:
				case FirebirdDType.CString2:
					return DBColumnType.String;
				case FirebirdDType.Blob:
					if(subType == 1) {
						return DBColumnType.String;
					}
					return DBColumnType.ByteArray;
			}
			return DBColumnType.Unknown;
		}
		short GetValue(object value) {
			return value is DBNull ? (short)0 : (short)value;
		}
		void GetColumns(DBTable table) {
			foreach(SelectStatementResultRow row in SelectData(new Query(string.Format(CultureInfo.InvariantCulture,
					"select r.RDB$FIELD_NAME, f.RDB$FIELD_TYPE, f.RDB$FIELD_SUB_TYPE, f.RDB$FIELD_SCALE, RDB$CHARACTER_LENGTH, r.RDB$DEFAULT_SOURCE, r.RDB$NULL_FLAG, f.RDB$FIELD_PRECISION " +
					"from RDB$RELATION_FIELDS r " +
					"join RDB$FIELDS f on r.RDB$FIELD_SOURCE  = f.RDB$FIELD_NAME where r.RDB$RELATION_NAME = '{0}'",
					ComposeSafeTableName(table.Name)))).Rows) {
				var typeCode = (FirebirdDType)GetValue(row.Values[1]);
				var subTypeCode = GetValue(row.Values[2]);
				var precision = GetValue(row.Values[7]);
				var scale = GetValue(row.Values[3]);
				var size = GetValue(row.Values[4]);
				DBColumnType type = GetTypeFromCode(typeCode, subTypeCode, size, scale);
				bool isNullable = (row.Values[6] == System.DBNull.Value);
				string dbDefaultValue = (row.Values[5] as string);
				object defaultValue = null;
				try {
					if(!string.IsNullOrEmpty(dbDefaultValue)) {
						if(dbDefaultValue.StartsWith("DEFAULT ")) {
							dbDefaultValue = dbDefaultValue.Remove(0, 8);
						}
						string scalarQuery = string.Concat("select ", dbDefaultValue, " from RDB$DATABASE");
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
				DBColumn dBColumn = new DBColumn(((string)row.Values[0]).Trim(), false, GetTypeNameFromCode(typeCode, subTypeCode, size, precision, scale), type == DBColumnType.String ? GetValue(row.Values[4]) : (short)0, type, isNullable, defaultValue);
				dBColumn.DbDefaultValue = dbDefaultValue;
				table.AddColumn(dBColumn);
			}
		}
		void GetPrimaryKey(DBTable table) {
			SelectStatementResult data = SelectData(new Query(string.Format(CultureInfo.InvariantCulture, "select RDB$INDEX_SEGMENTS.RDB$FIELD_NAME from RDB$RELATION_CONSTRAINTS join RDB$INDEX_SEGMENTS on RDB$INDEX_SEGMENTS.RDB$INDEX_NAME = RDB$RELATION_CONSTRAINTS.RDB$INDEX_NAME where RDB$RELATION_NAME = '{0}' and RDB$CONSTRAINT_TYPE = 'PRIMARY KEY' order by RDB$INDEX_SEGMENTS.RDB$FIELD_POSITION", ComposeSafeTableName(table.Name))));
			if(data.Rows.Length > 0) {
				StringCollection cols = new StringCollection();
				for(int i = 0; i < data.Rows.Length; i++) {
					object[] topRow = data.Rows[i].Values;
					string columnName = ((string)topRow[0]).Trim();
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
			SelectStatementResult data = SelectData(new Query(string.Format(CultureInfo.InvariantCulture, "select RDB$INDICES.RDB$INDEX_NAME, RDB$INDEX_SEGMENTS.RDB$FIELD_NAME, RDB$INDEX_SEGMENTS.RDB$FIELD_POSITION, RDB$INDICES.RDB$UNIQUE_FLAG from RDB$INDICES join RDB$INDEX_SEGMENTS on RDB$INDEX_SEGMENTS.RDB$INDEX_NAME = RDB$INDICES.RDB$INDEX_NAME where RDB$INDICES.RDB$RELATION_NAME = '{0}' and RDB$INDICES.RDB$FOREIGN_KEY is NULL order by RDB$INDICES.RDB$INDEX_NAME, RDB$INDEX_SEGMENTS.RDB$FIELD_POSITION", ComposeSafeTableName(table.Name))));
			DBIndex index = null;
			foreach(SelectStatementResultRow row in data.Rows) {
				if(0 == (short)row.Values[2]) {
					StringCollection list = new StringCollection();
					list.Add(((string)row.Values[1]).Trim());
					object isUnique = row.Values[3];
					index = new DBIndex(((string)row.Values[0]).Trim(), list, isUnique is short ? (short)isUnique == 1 : false);
					table.Indexes.Add(index);
				}
				else
					index.Columns.Add(((string)row.Values[1]).Trim());
			}
		}
		void GetForeignKeys(DBTable table) {
			SelectStatementResult data = SelectData(new Query(string.Format(CultureInfo.InvariantCulture,
				@"select p.RDB$FIELD_POSITION, p.RDB$FIELD_NAME, f.RDB$FIELD_NAME, pt.RDB$RELATION_NAME
from RDB$INDICES i
join RDB$INDEX_SEGMENTS f on f.RDB$INDEX_NAME = i.RDB$INDEX_NAME
join RDB$INDEX_SEGMENTS p on p.RDB$INDEX_NAME = i.RDB$FOREIGN_KEY and p.RDB$FIELD_POSITION = f.RDB$FIELD_POSITION
join RDB$INDICES pt on i.RDB$FOREIGN_KEY = pt.RDB$INDEX_NAME
where i.RDB$RELATION_NAME = '{0}' and i.RDB$FOREIGN_KEY is NOT NULL
order by i.RDB$INDEX_NAME, f.RDB$FIELD_POSITION", ComposeSafeTableName(table.Name))));
			DBForeignKey fk = null;
			foreach(SelectStatementResultRow row in data.Rows) {
				if((short)row.Values[0] == 0) {
					StringCollection pkc = new StringCollection();
					StringCollection fkc = new StringCollection();
					pkc.Add(((string)row.Values[2]).Trim());
					fkc.Add(((string)row.Values[1]).Trim());
					fk = new DBForeignKey(pkc, ((string)row.Values[3]).Trim(), fkc);
					table.ForeignKeys.Add(fk);
				}
				else {
					fk.Columns.Add(((string)row.Values[2]).Trim());
					fk.PrimaryKeyTableKeyColumns.Add(((string)row.Values[1]).Trim());
				}
			}
		}
		public override void CreateTable(DBTable table) {
			base.CreateTable(table);
			if(table.PrimaryKey != null) {
				DBColumn key = table.GetColumn(table.PrimaryKey.Columns[0]);
				if(key.IsIdentity) {
					ExecSql(new Query(string.Format(CultureInfo.InvariantCulture, "create generator \"{0}\"", GetSeqName(table.Name))));
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
			foreach(SelectStatementResultRow row in GetDataForTables(tables, null, "select RDB$RELATION_NAME, RDB$VIEW_BLR from RDB$RELATIONS where RDB$RELATION_NAME in ({0}) and RDB$SYSTEM_FLAG = 0").Rows)
				dbTables.Add(((string)row.Values[0]).Trim(), row.Values[1] != System.DBNull.Value);
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
			return 31;
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
		public override string FormatSelect(string selectedPropertiesSql, string fromSql, string whereSql, string orderBySql, string groupBySql, string havingSql, int skipSelectedRecords, int topSelectedRecords) {
			base.FormatSelect(selectedPropertiesSql, fromSql, whereSql, orderBySql, groupBySql, havingSql, skipSelectedRecords, topSelectedRecords);
			string topSql = topSelectedRecords != 0 ? string.Format(" first {0} ", topSelectedRecords) : string.Empty;
			string skipSql = skipSelectedRecords != 0 ? string.Format(" skip {0} ", skipSelectedRecords) : string.Empty;
			string modificatorsSql = string.Format("{0}{1}", topSql, skipSql);
			string expandedWhereSql = whereSql != null ? string.Format(CultureInfo.InvariantCulture, "{0}where {1}", Environment.NewLine, whereSql) : string.Empty;
			string expandedOrderBySql = orderBySql != null ? string.Format(CultureInfo.InvariantCulture, "{0}order by {1}", Environment.NewLine, orderBySql) : string.Empty;
			string expandedHavingSql = havingSql != null ? string.Format(CultureInfo.InvariantCulture, "{0}having {1}", Environment.NewLine, havingSql) : string.Empty;
			string expandedGroupBySql = groupBySql != null ? string.Format(CultureInfo.InvariantCulture, "{0}group by {1}", Environment.NewLine, groupBySql) : string.Empty;
			return string.Format(CultureInfo.InvariantCulture, "select {0}{1} from {2}{3}{4}{5}{6}", modificatorsSql, selectedPropertiesSql, fromSql, expandedWhereSql, expandedGroupBySql, expandedHavingSql, expandedOrderBySql);
		}
		public override string FormatOrder(string sortProperty, SortingDirection direction) {
			return string.Format(CultureInfo.InvariantCulture, "{0} {1}", sortProperty, direction == SortingDirection.Ascending ? "asc nulls first" : "desc nulls last");
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
		public override string FormatBinary(BinaryOperatorType operatorType, string leftOperand, string rightOperand) {
			switch(operatorType) {
				case BinaryOperatorType.Modulo:
					return string.Format(CultureInfo.InvariantCulture, "mod({0}, {1})", leftOperand, rightOperand);
				case BinaryOperatorType.BitwiseXor:
					return string.Format(CultureInfo.InvariantCulture, "bin_xor({0}, {1})", leftOperand, rightOperand);
				case BinaryOperatorType.BitwiseAnd:
					return string.Format(CultureInfo.InvariantCulture, "bin_and({0}, {1})", leftOperand, rightOperand);
				case BinaryOperatorType.BitwiseOr:
					return string.Format(CultureInfo.InvariantCulture, "bin_or({0}, {1})", leftOperand, rightOperand);
				case BinaryOperatorType.Plus:
				case BinaryOperatorType.Minus:
				case BinaryOperatorType.Multiply:
				case BinaryOperatorType.Divide:
					leftOperand = DecodeStrParam(leftOperand);
					rightOperand = DecodeStrParam(rightOperand);
					return base.FormatBinary(operatorType, leftOperand, rightOperand);
				default:
					return base.FormatBinary(operatorType, leftOperand, rightOperand);
			}
		}
		string GetFunctionStrLenName() {
			return SupportVersion(2.1m, 0) ? "CHAR_LENGTH" : "strlen";
		}
		public override string FormatFunction(FunctionOperatorType operatorType, params string[] operands) {
			for(int i = 0; i < operands.Length; i++) {
				operands[i] = DecodeStrParam(operands[i]);
			}
			switch(operatorType) {
				case FunctionOperatorType.IsNull:
					switch(operands.Length) {
						case 1:
							return string.Format(CultureInfo.InvariantCulture, "(({0}) is null)", operands[0]);
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "COALESCE({0}, {1})", operands[0], operands[1]);
					}
					goto default;
				case FunctionOperatorType.IsNullOrEmpty:
					return string.Format(CultureInfo.InvariantCulture, "(({0}) is null or ({0}) = '')", operands[0]);
				case FunctionOperatorType.Sqr:
					return string.Format(CultureInfo.InvariantCulture, "Sqrt({0})", operands[0]);
				case FunctionOperatorType.Log:
					return FnLog(operands);
				case FunctionOperatorType.Log10:
					return string.Format(CultureInfo.InvariantCulture, "log(10, {0})", operands[0]);
				case FunctionOperatorType.Acos:
					return string.Format(CultureInfo.InvariantCulture, "acos({0})", operands[0]);
				case FunctionOperatorType.Asin:
					return string.Format(CultureInfo.InvariantCulture, "asin({0})", operands[0]);
				case FunctionOperatorType.Atn:
					return string.Format(CultureInfo.InvariantCulture, "atan({0})", operands[0]);
				case FunctionOperatorType.Atn2:
					return string.Format(CultureInfo.InvariantCulture, "atan2({0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.Cosh:
					return string.Format(CultureInfo.InvariantCulture, "cosh({0})", operands[0]);
				case FunctionOperatorType.Sinh:
					return string.Format(CultureInfo.InvariantCulture, "sinh({0})", operands[0]);
				case FunctionOperatorType.Tanh:
					return string.Format(CultureInfo.InvariantCulture, "tanh({0})", operands[0]);
				case FunctionOperatorType.Max:
					return string.Format(CultureInfo.InvariantCulture, "iif({0} > {1}, {0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.Min:
					return string.Format(CultureInfo.InvariantCulture, "iif({0} < {1}, {0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.Rnd:
					return "Rand()";
				case FunctionOperatorType.BigMul:
					return string.Format(CultureInfo.InvariantCulture, "(cast({0} as bigint) * cast({1} as bigint))", operands[0], operands[1]);
				case FunctionOperatorType.GetMilliSecond:
					return string.Format(CultureInfo.InvariantCulture, "(cast(extract(second from {0}) - floor(extract(second from {0})) as numeric(4, 4)) * 1000)", operands[0]);
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
					return string.Format(CultureInfo.InvariantCulture, "extract(weekday from {0})", operands[0]);
				case FunctionOperatorType.GetDayOfYear:
					return string.Format(CultureInfo.InvariantCulture, "(extract(yearday from {0}) + 1)", operands[0]);
				case FunctionOperatorType.GetDate:
					return string.Format(CultureInfo.InvariantCulture, "cast(cast({0} as date) as timestamp)", operands[0]);
				case FunctionOperatorType.Ascii:
					return string.Format(CultureInfo.InvariantCulture, "ascii_val(substring(cast({0} as varchar(8191)) from 1 for 1))", operands[0]);
				case FunctionOperatorType.Char:
					return string.Format(CultureInfo.InvariantCulture, "ascii_char({0})", operands[0]);
				case FunctionOperatorType.ToInt:
					return string.Format(CultureInfo.InvariantCulture, "cast({0} as integer)", operands[0]);
				case FunctionOperatorType.ToLong:
					return string.Format(CultureInfo.InvariantCulture, "cast({0} as bigint)", operands[0]);
				case FunctionOperatorType.ToFloat:
					return string.Format(CultureInfo.InvariantCulture, "cast({0} as float)", operands[0]);
				case FunctionOperatorType.ToDouble:
					return string.Format(CultureInfo.InvariantCulture, "cast({0} as double precision)", operands[0]);
				case FunctionOperatorType.ToDecimal:
					return string.Format(CultureInfo.InvariantCulture, "cast({0} as decimal(18,4))", operands[0]);
				case FunctionOperatorType.ToStr:
					return string.Format(CultureInfo.InvariantCulture, "cast({0} as varchar(8191))", operands[0]);
				case FunctionOperatorType.Len:
					return string.Format(CultureInfo.InvariantCulture, GetFunctionStrLenName() + "(trim({0}))", operands[0]);
				case FunctionOperatorType.Trim:
					return string.Format(CultureInfo.InvariantCulture, "trim({0})", operands[0]);
				case FunctionOperatorType.PadLeft:
					return FnLpad(operands);
				case FunctionOperatorType.PadRight:
					return FnRpad(operands);
				case FunctionOperatorType.AddTicks:
					return string.Format(CultureInfo.InvariantCulture, "({0} + cast({1} as double precision) / cast(864000000000 as double precision))", operands[0], operands[1]);
				case FunctionOperatorType.AddMilliSeconds:
					return string.Format(CultureInfo.InvariantCulture, "({0} + cast({1} as double precision) / cast(86400000 as double precision))", operands[0], operands[1]);
				case FunctionOperatorType.AddTimeSpan:
				case FunctionOperatorType.AddSeconds:
					return string.Format(CultureInfo.InvariantCulture, "({0} + cast({1} as double precision) / cast(86400 as double precision))", operands[0], operands[1]);
				case FunctionOperatorType.AddMinutes:
					return string.Format(CultureInfo.InvariantCulture, "({0} + cast({1} as double precision) / cast(1440 as double precision))", operands[0], operands[1]);
				case FunctionOperatorType.AddHours:
					return string.Format(CultureInfo.InvariantCulture, "({0} + cast({1} as double precision) / cast(24 as double precision))", operands[0], operands[1]);
				case FunctionOperatorType.AddDays:
					return string.Format(CultureInfo.InvariantCulture, "({0} + cast({1} as double precision))", operands[0], operands[1]);
				case FunctionOperatorType.Now:
					return "cast('now' as timestamp)";
				case FunctionOperatorType.Today:
					return "cast('today' as timestamp)";
				case FunctionOperatorType.Concat:
					string args = string.Empty;
					for(int i = 0; i < operands.Length; i++) {
						if(operands[i].Length > 0)
							args += i == operands.Length - 1 ? string.Format(CultureInfo.InvariantCulture, "{0}", operands[i]) : string.Format(CultureInfo.InvariantCulture, "{0} || ", operands[i]);
					}
					return args;
				case FunctionOperatorType.DateDiffDay:
					return string.Format(CultureInfo.InvariantCulture, "datediff(day, {0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffHour:
					return string.Format(CultureInfo.InvariantCulture, "datediff(hour, {0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffMilliSecond:
					return string.Format(CultureInfo.InvariantCulture, "datediff(millisecond, {0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffMinute:
					return string.Format(CultureInfo.InvariantCulture, "datediff(minute, {0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffMonth:
					return string.Format(CultureInfo.InvariantCulture, "datediff(month, {0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffSecond:
					return string.Format(CultureInfo.InvariantCulture, "datediff(second, {0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffYear:
					return string.Format(CultureInfo.InvariantCulture, "datediff(year, {0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.Substring: {
					if(operands.Length == 3) {
						return string.Format(CultureInfo.InvariantCulture, "substring(({0}) from cast({1} as integer)+1 for cast({2} as integer))", operands[0], operands[1], operands[2]);
					}
					else {
						return string.Format(CultureInfo.InvariantCulture, "substring(({0}) from cast({1} as integer)+1)", operands[0], operands[1]);
					}
				}
				case FunctionOperatorType.AddMonths: {
					if(SupportVersion(2, 1)) {
						return string.Format("dateadd(month, cast({0} as double precision), cast({1} as timestamp))", operands[1], operands[0]);
					}
					else {
						throw new NotSupportedException();
					}
				}
				case FunctionOperatorType.AddYears: {
					if(SupportVersion(2, 1)) {
						return string.Format("dateadd(year, cast({0} as double precision), cast({1} as timestamp))", operands[1], operands[0]);
					}
					else {
						throw new NotSupportedException();
					}
				}
				case FunctionOperatorType.CharIndex: {
					if(SupportVersion(2, 1)) {
						if(operands.Length >= 3) {
							return string.Format("(position({0}, {1}, cast({2} as integer) + 1) - 1)", operands[0], operands[1], operands[2]);
						}
						else {
							return string.Format("(position({0}, {1}) - 1)", operands[0], operands[1]);
						}
					}
					else {
						throw new NotSupportedException();
					}
				}
				case FunctionOperatorType.Round: {
					if(SupportVersion(2, 1)) {
						if(operands.Length >= 2) {
							return string.Format("round(cast({0} as double precision), cast({1} as integer))", operands[0], operands[1]);
						}
						else {
							return string.Format("round(cast({0} as double precision))", operands[0]);
						}
					}
					else {
						throw new NotSupportedException();
					}
				}
				case FunctionOperatorType.Remove: {
					if(SupportVersion(2, 1)) {
						if(operands.Length >= 3) {
							return string.Format("(left({0}, cast({1} as integer)) || right({0}, char_length({0})-cast({1} as integer)-cast({2} as integer)))", operands[0], operands[1], operands[2]);
						}
						else {
							return string.Format("(left({0}, cast({1} as integer)))", operands[0], operands[1]);
						}
					}
					else {
						throw new NotSupportedException();
					}
				}
				case FunctionOperatorType.Insert: {
					if(SupportVersion(2, 1)) {
						return string.Format("(left({0}, cast({1} as integer)) || ({2}) || right({0}, char_length({0})-cast({1} as integer)))", operands[0], operands[1], operands[2]);
					}
					else {
						throw new NotSupportedException();
					}
				}
				case FunctionOperatorType.Reverse:
					if(SupportVersion(2, 1)) {
						return string.Format("reverse({0})", operands[0]);
					}
					else {
						throw new NotSupportedException();
					}
				case FunctionOperatorType.Replace:
					if(SupportVersion(2, 1)) {
						return string.Format("replace({0}, {1}, {2})", operands[0], operands[1], operands[2]);
					}
					else {
						throw new NotSupportedException();
					}
				case FunctionOperatorType.Exp:
					if(SupportVersion(2, 1)) {
						return string.Format("exp(cast({0} as double precision))", operands[0]);
					}
					else {
						throw new NotSupportedException();
					}
				case FunctionOperatorType.Power:
					if(SupportVersion(2, 1)) {
						return string.Format("power(cast({0} as double precision), cast({1} as double precision))", operands[0], operands[1]);
					}
					else {
						throw new NotSupportedException();
					}
				case FunctionOperatorType.UtcNow:
					throw new NotSupportedException();
				default:
					return base.FormatFunction(operatorType, operands);
			}
		}
		readonly static char[] achtungChars = new char[] { '_', '%' };
		public override string FormatFunction(ProcessParameter processParameter, FunctionOperatorType operatorType, params object[] operands) {
			switch(operatorType) {
				case FunctionOperatorType.StartsWith:
				case FunctionOperatorType.EndsWith:
				case FunctionOperatorType.Contains:
					object secondOperand = operands[1];
					if(secondOperand is OperandValue && ((OperandValue)secondOperand).Value is string) {
						string operandString = (string)((OperandValue)secondOperand).Value;
						bool needEscape = operandString.IndexOfAny(achtungChars) >= 0;
						if(needEscape)
							operandString = operandString.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
						string operandStringFormatted;
						switch(operatorType) {
							case FunctionOperatorType.StartsWith:
								operandStringFormatted = processParameter(new ConstantValue(operandString + "%"));
								break;
							case FunctionOperatorType.EndsWith:
								operandStringFormatted = processParameter(new ConstantValue("%" + operandString));
								break;
							case FunctionOperatorType.Contains:
								operandStringFormatted = processParameter(new ConstantValue(string.Concat("%", operandString, "%")));
								break;
							default:
								throw new NotSupportedException();
						}
						if(needEscape) {
							return string.Format(CultureInfo.InvariantCulture, @"({0} liKE {1} ESCAPE '\')", processParameter(operands[0]), operandStringFormatted);
						}
						return string.Format(CultureInfo.InvariantCulture, "({0} liKE {1})", processParameter(operands[0]), operandStringFormatted);
					}
					throw new NotSupportedException();
				case FunctionOperatorType.AddHours:
				case FunctionOperatorType.AddMinutes:
				case FunctionOperatorType.AddSeconds:
				case FunctionOperatorType.AddMilliSeconds:
					if(ResolveColumnType((CriteriaOperator)operands[0]) == typeof(TimeOnly)) {
						return FormatFunctionTimeOnly(processParameter, operatorType, operands);
					}
					break;
				case FunctionOperatorType.AddYears:
				case FunctionOperatorType.AddMonths:
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
				case FunctionOperatorType.AddYears:
					return string.Format("dateadd(year, cast({0} as double precision), cast({1} as date))", parameters[1], parameters[0]);
				case FunctionOperatorType.AddMonths:
					return string.Format("dateadd(month, cast({0} as double precision), cast({1} as date))", parameters[1], parameters[0]);
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
				case FunctionOperatorType.AddTimeSpan:
				case FunctionOperatorType.AddSeconds:
					return string.Format("cast({0} as time) + cast({1} as double precision)", parameters[0], parameters[1]);
				case FunctionOperatorType.AddMinutes:
					return string.Format("cast({0} as time) + (cast({1} as double precision) * 60)", parameters[0], parameters[1]);
				case FunctionOperatorType.AddHours:
					return string.Format("cast({0} as time) + (cast({1} as double precision) * 3600)", parameters[0], parameters[1]);
				case FunctionOperatorType.AddMilliSeconds:
					return string.Format("cast({0} as time) + (cast({1} as double precision) / 1000)", parameters[0], parameters[1]);
				default:
					throw new NotSupportedException();
			}
		}
		string FnLog(string[] operands) {
			if(operands.Length == 1) {
				return string.Format(CultureInfo.InvariantCulture, "Ln({0})", operands[0]);
			}
			if(operands.Length == 2) {
				return string.Format(CultureInfo.InvariantCulture, "log({1}, {0})", operands[0], operands[1]);
			}
			throw new NotSupportedException();
		}
		string FnLpad(string[] operands) {
			if(operands.Length == 2) {
				return string.Format(CultureInfo.InvariantCulture, "iif({2}({0}) > {1}, {0}, lpad({0}, {1}, ' '))", operands[0], operands[1], GetFunctionStrLenName());
			}
			if(operands.Length == 3) {
				return string.Format(CultureInfo.InvariantCulture, "iif({3}({0}) > {1}, {0}, lpad({0}, {1}, {2}))", operands[0], operands[1], operands[2], GetFunctionStrLenName());
			}
			throw new NotSupportedException();
		}
		string FnRpad(string[] operands) {
			if(operands.Length == 2) {
				return string.Format(CultureInfo.InvariantCulture, "iif({2}({0}) > {1}, {0}, rpad({0}, {1}, ' '))", operands[0], operands[1], GetFunctionStrLenName());
			}
			if(operands.Length == 3) {
				return string.Format(CultureInfo.InvariantCulture, "iif({3}({0}) > {1}, {0}, rpad({0}, {1}, {2}))", operands[0], operands[1], operands[2], GetFunctionStrLenName());
			}
			throw new NotSupportedException();
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
			if(parameter.Value is string) {
				var param = parameter as ParameterValue;
				if(ReferenceEquals(param, null) || (param.DBType == DBColumnType.Unknown && string.IsNullOrEmpty(param.DBTypeName))) {
					return string.Format(CultureInfo.InvariantCulture, "@s{0}_{1}", index, ((string)parameter.Value).Length);
				}
			}
			else if (parameter.Value is int) {
				var param = parameter as ParameterValue;
				if(ReferenceEquals(param, null) || (param.DBType == DBColumnType.Unknown && string.IsNullOrEmpty(param.DBTypeName))) {
					return string.Format(CultureInfo.InvariantCulture, "@i{0}", index);
				}
			}
			return "@p" + index.ToString(CultureInfo.InvariantCulture);
		}
		protected string FormatString(object value) {
			return "'" + ((string)value).Replace("'", "''") + "'";
		}
		static string DecodeStrParam(string operand) {
			if(operand.StartsWith("@s")) {
				int strLen = int.Parse(operand.Substring(operand.IndexOf('_') + 1));
				return string.Format(CultureInfo.InvariantCulture, "cast({0} as varchar({1}))", operand, strLen);
			}
			else if(operand.StartsWith("@i")) {
				return string.Format(CultureInfo.InvariantCulture, "cast({0} as integer)", operand);
			}
			return operand;
		}
		public override bool SupportNamedParameters { get { return true; } }
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
					return ((bool)value) ? "'1'" : "'0'";
				case TypeCode.Char:
					return "'" + (char)value + "'";
				case TypeCode.DateTime:
					DateTime datetimeValue = (DateTime)value;
					string dateTimeFormatPattern;
					dateTimeFormatPattern = "yyyy-MM-dd HH:mm:ss.ffff";
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
		protected override string CreateForeignKeyTemplate { get { return base.CreateForeignKeyTemplate; } }
		void ClearDatabase(IDbCommand command) {
			SelectStatementResult generators = SelectData(new Query("select RDB$GENERATOR_NAME from RDB$GENERATORS where RDB$SYSTEM_FLAG is null or RDB$SYSTEM_FLAG = 0"));
			foreach(SelectStatementResultRow row in generators.Rows) {
				command.CommandText = "drop generator \"" + ((string)row.Values[0]).Trim() + "\"";
				command.ExecuteNonQuery();
			}
			SelectStatementResult constraints = SelectData(new Query("select RDB$CONSTRAINT_NAME, RDB$RELATION_NAME  from RDB$RELATION_CONSTRAINTS where RDB$CONSTRAINT_TYPE = 'FOREIGN KEY' "));
			foreach(SelectStatementResultRow row in constraints.Rows) {
				command.CommandText = "alter table \"" + ((string)row.Values[1]).Trim() + "\" drop constraint \"" + ((string)row.Values[0]).Trim() + "\"";
				command.ExecuteNonQuery();
			}
			string[] tables = GetStorageTablesList(false);
			foreach(string table in tables) {
				command.CommandText = "drop table \"" + table + "\"";
				command.ExecuteNonQuery();
			}
		}
		protected override void ProcessClearDatabase() {
			IDbCommand command = CreateCommand();
			ClearDatabase(command);
		}
		protected override SelectedData ExecuteSprocParametrized(string sprocName, params OperandValue[] parameters) {
			throw new NotSupportedException();
		}
		protected override Task<SelectedData> ExecuteSprocParametrizedAsync(AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken, string sprocName, params OperandValue[] parameters) {
			throw new NotSupportedException();
		}
		public override string[] GetStorageTablesList(bool includeViews) {
			SelectStatementResult tables = SelectData(new Query("select RDB$RELATION_NAME, RDB$VIEW_BLR from RDB$RELATIONS where RDB$SYSTEM_FLAG = 0"));
			List<string> result = new List<string>(tables.Rows.Length);
			for(int i = 0; i < tables.Rows.Length; i++) {
				if(!includeViews && tables.Rows[i].Values[1] != DBNull.Value && tables.Rows[i].Values[1] != null)
					continue;
				result.Add(((string)tables.Rows[i].Values[0]).TrimEnd());
			}
			return result.ToArray();
		}
		ExecMethodDelegate commandBuilderDeriveParametersHandler;
		protected override void CommandBuilderDeriveParameters(IDbCommand command) {
			if(commandBuilderDeriveParametersHandler == null) {
				commandBuilderDeriveParametersHandler = ReflectConnectionHelper.GetCommandBuilderDeriveParametersDelegate("FirebirdSql.Data.FirebirdClient", "FirebirdSql.Data.FirebirdClient.FbCommandBuilder");
			}
			commandBuilderDeriveParametersHandler(command);
		}
		readonly HashSet<string> storedProceduresIgnoreDerivedParameters = new HashSet<string>(new string[] { "FINISH_SESSION", "PAUSE_SESSION", "TRANSITIONS" }, System.StringComparer.OrdinalIgnoreCase);
		public override DBStoredProcedure[] GetStoredProcedures() {
			List<DBStoredProcedure> result = new List<DBStoredProcedure>();
			using(var command = Connection.CreateCommand()) {
				command.CommandText = "SELECT rdb$procedure_name FROM rdb$procedures ORDER BY rdb$procedure_name";
				using(var reader = command.ExecuteReader()) {
					while(reader.Read()) {
						DBStoredProcedure curProc = new DBStoredProcedure();
						string procName = reader.GetString(0);
						if(string.IsNullOrEmpty(procName)) {
							continue;
						}
						curProc.Name = procName.Trim();
						result.Add(curProc);
					}
				}
			}
			foreach(DBStoredProcedure sproc in result) {
				using(var command = Connection.CreateCommand()) {
					command.CommandType = CommandType.StoredProcedure;
					command.CommandText = FormatTable(ComposeSafeSchemaName(sproc.Name), ComposeSafeTableName(sproc.Name));
					if(!SupportVersion(5, 0) || !storedProceduresIgnoreDerivedParameters.Contains(sproc.Name)) {
						CommandBuilderDeriveParameters(command);
					}
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
					}
					sproc.Arguments.AddRange(dbArguments);
				}
			}
			return result.ToArray();
		}
	}
	public class FirebirdProviderFactory : ProviderFactory {
		public override IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return FirebirdConnectionProvider.CreateProviderFromConnection(connection, autoCreateOption);
		}
		public override IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			return FirebirdConnectionProvider.CreateProviderFromString(connectionString, autoCreateOption, out objectsToDisposeOnDisconnect);
		}
		public override string GetConnectionString(Dictionary<string, string> parameters) {
			if((!parameters.ContainsKey(ServerParamID) && !parameters.ContainsKey(DatabaseParamID)) ||
				!parameters.ContainsKey(UserIDParamID) || !parameters.ContainsKey(PasswordParamID)) {
				return null;
			}
			string connectionString = "";
			if(parameters.ContainsKey(ServerParamID)) {
				string port;
				if(parameters.TryGetValue(PortParamID, out port)) {
					connectionString = FirebirdConnectionProvider.GetConnectionString(parameters[ServerParamID], Convert.ToInt32(port, CultureInfo.InvariantCulture),
						parameters[UserIDParamID], parameters[PasswordParamID], parameters[DatabaseParamID]);
				}
				else {
					connectionString = FirebirdConnectionProvider.GetConnectionString(parameters[ServerParamID], parameters[UserIDParamID],
						parameters[PasswordParamID], parameters[DatabaseParamID]);
				}
			}
			else {
				connectionString = FirebirdConnectionProvider.GetConnectionString(parameters[UserIDParamID],
					parameters[PasswordParamID], parameters[DatabaseParamID]);
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
		public override bool HasPort { get { return true; } }
		public override bool HasUserName { get { return true; } }
		public override bool HasPassword { get { return true; } }
		public override bool HasIntegratedSecurity { get { return false; } }
		public override bool HasMultipleDatabases { get { return true; } }
		public override bool IsServerbased { get { return true; } }
		public override bool IsFilebased { get { return true; } }
		public override string ProviderKey { get { return FirebirdConnectionProvider.XpoProviderTypeString; } }
		public override string[] GetDatabases(string server, string userId, string password) {
			return GetDatabases(server, 0, userId, password);
		}
		public override string[] GetDatabases(string server, int port, string userId, string password) {
			return Array.Empty<string>();
		}
		public override string FileFilter { get { return "Firebird databases|*.gdb;*.fdb"; } }
		public override bool MeanSchemaGeneration { get { return true; } }
	}
#pragma warning restore DX0024
}
namespace DevExpress.Xpo.DB.Helpers {
	using System;
	using System.Data;
	using System.Globalization;
	class DbTypeMapperFirebird<TSqlDbTypeEnum, TSqlParameter> : DbTypeMapper<TSqlDbTypeEnum, TSqlParameter>
		where TSqlDbTypeEnum : struct
		where TSqlParameter : IDbDataParameter {
		static readonly TSqlDbTypeEnum fbTypeBinary;
		static DbTypeMapperFirebird() {
			fbTypeBinary = (TSqlDbTypeEnum)Enum.Parse(typeof(TSqlDbTypeEnum), "Binary");
		}
		protected override string ParameterDbTypePropertyName { get { return "FbDbType"; } }
		protected override string GetParameterTypeNameForBoolean(out int? size, out byte? precision, out byte? scale) {
			size = 1;
			precision = scale = null;
			return "Char";
		}
		protected override string GetParameterTypeNameForByte(out byte? precision, out byte? scale) {
			precision = 3;
			scale = 0;
			return "Numeric";
		}
		protected override string GetParameterTypeNameForByteArray(out int? size) {
			size = null;
			return "Binary";
		}
		protected override string GetParameterTypeNameForChar(out int? size) {
			size = null;
			return "Char";
		}
		protected override string GetParameterTypeNameForDateTime() {
			return "TimeStamp";
		}
		protected override string GetParameterTypeNameForDecimal(out byte? precision, out byte? scale) {
			precision = 18;
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
			precision = 3;
			scale = 0;
			return "Numeric";
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
			precision = 18;
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
			sqlType = sqlType.ToUpperInvariant();
			switch(sqlType) {
				case "BOOL":
				case "BOOLEAN":
					return "Boolean";
				case "SMALLINT":
					return "SmallInt";
				case "INTEGER":
					return "Integer";
				case "BIGINT":
					return "BigInt";
				case "DECIMAL":
					return "Decimal";
				case "NUMERIC":
					return "Numeric";
				case "FLOAT":
					return "Float";
				case "DOUBLE PRECISION":
				case "DOUBLE":
					return "Double";
				case "DATE":
					return "Date";
				case "TIME":
					return "Time";
				case "TIMESTAMP":
					return "TimeStamp";
				case "CHAR":
				case "CHARACTER":
					return "Char";
				case "VARCHAR":
				case "CHAR VARYING":
				case "CHARACTER VARYING":
					return "VarChar";
				case "BLOB":
					return "Binary";
				default:
					if(sqlType.StartsWith("BLOB ", StringComparison.OrdinalIgnoreCase)) {
						return "Binary";
					}
					return null;
			}
		}
		protected override DBTypeInfoBase CustomParseSqlType(string sqlTypeWithoutParameters, string sqlTypeParameters, string sqlTypeSuffix) {
			switch(sqlTypeWithoutParameters.ToUpperInvariant()) {
				case "CHAR":
				case "CHARACTER":
				case "VARCHAR":
				case "CHAR VARYING":
				case "CHARACTER VARYING":
					int size;
					if(!string.IsNullOrEmpty(sqlTypeParameters) && Int32.TryParse(sqlTypeWithoutParameters, System.Globalization.NumberStyles.Integer, CultureInfo.InvariantCulture, out size)) {
						return CreateParameterDBTypeInfo(sqlTypeWithoutParameters, size, null, null);
					}
					else {
						return CreateParameterDBTypeInfo(sqlTypeWithoutParameters, null, null, null);
					}
			}
			return base.CustomParseSqlType(sqlTypeWithoutParameters, sqlTypeParameters, sqlTypeSuffix);
		}
		public override void SetParameterTypeAndSize(IDbDataParameter parameter, DBColumnType dbColumnType, int size) {
			if(dbColumnType == DBColumnType.String) {
				if(size <= 0 || size > FirebirdConnectionProvider.MaximumStringSize) {
					SetSqlDbTypeHandler((TSqlParameter)parameter, fbTypeBinary);
					return;
				}
			}
			base.SetParameterTypeAndSize(parameter, dbColumnType, size);
		}
	}
}
