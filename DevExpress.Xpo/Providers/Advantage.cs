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
	using System.Collections;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.Data;
	using System.Globalization;
	using System.Threading;
	using System.Threading.Tasks;
	using DevExpress.Data.Filtering;
	using DevExpress.Data.Helpers;
	using DevExpress.Utils;
	using DevExpress.Xpo;
	using DevExpress.Xpo.DB.Exceptions;
	using DevExpress.Xpo.DB.Helpers;
	public class AdvantageConnectionProvider : ConnectionProviderSql {
		bool isADS10;
		ReflectConnectionHelper helper;
		ReflectConnectionHelper ConnectionHelper {
			get {
				if(helper == null)
					helper = new ReflectConnectionHelper(Connection, "Advantage.Data.Provider.AdsException");
				return helper;
			}
		}
		DbTypeMapperBase dbTypeMapper;
		protected override DbTypeMapperBase DbTypeMapper {
			get {
				if(dbTypeMapper == null) {
					Type sqlParameterType = ConnectionHelper.GetType("Advantage.Data.Provider.AdsParameter");
					dbTypeMapper = (DbTypeMapperBase)Activator.CreateInstance(typeof(DbTypeMapperAdvantage<>).MakeGenericType(sqlParameterType));
				}
				return dbTypeMapper;
			}
		}
		public const string XpoProviderTypeString = "Advantage";
		public static string GetConnectionString(string database) {
			return String.Format("{1}={2};Data source={0};servertype=local;user id=ADSSYS;TrimTrailingSpaces=true", EscapeConnectionStringArgument(database), DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString);
		}
		public static string GetConnectionString(string database, string password) {
			return String.Format("{1}={2};Data source={0};servertype=local;user id=ADSSYS;Password={3};TrimTrailingSpaces=true", EscapeConnectionStringArgument(database), DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString, EscapeConnectionStringArgument(password));
		}
		public static string GetConnectionString(string database, string username, string password) {
			return String.Format("{1}={2};Data source={0};servertype=local;user id={3};Password={4};TrimTrailingSpaces=true", EscapeConnectionStringArgument(database), DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString, EscapeConnectionStringArgument(username), EscapeConnectionStringArgument(password));
		}
		public static IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			IDbConnection connection = CreateConnection(connectionString);
			objectsToDisposeOnDisconnect = new IDisposable[] { connection };
			return CreateProviderFromConnection(connection, autoCreateOption);
		}
		public static IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return new AdvantageConnectionProvider(connection, autoCreateOption);
		}
		static AdvantageConnectionProvider() {
			RegisterDataStoreProvider(XpoProviderTypeString, new DataStoreCreationFromStringDelegate(CreateProviderFromString));
			RegisterDataStoreProvider("Advantage.Data.Provider.AdsConnection", new DataStoreCreationFromConnectionDelegate(CreateProviderFromConnection));
			RegisterFactory(new AdvantageProviderFactory());
		}
		public static void Register() { }
		public AdvantageConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption)
			: base(connection, autoCreateOption, true) {
			CheckVersion(connection);
		}
		protected AdvantageConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption, bool openConnection)
			: base(connection, autoCreateOption, openConnection) {
		}
		protected void CheckVersion(IDbConnection connection) {
			using(IDbCommand command = connection.CreateCommand()) {
				command.CommandText = @"execute procedure sp_mgGetInstallInfo()";
				using(IDataReader reader = command.ExecuteReader()) {
					if(reader.Read()) {
						string versionString = null;
						for(int i = 0; i < reader.FieldCount; i++) {
							if(reader.GetName(i) == "Version") {
								versionString = reader.GetString(i);
								break;
							}
						}
						if(!string.IsNullOrEmpty(versionString)) {
							string[] parts = versionString.Split('.');
							int version;
							if(parts.Length > 0 && Int32.TryParse(parts[0], out version)) {
								isADS10 = version >= 10;
							}
						}
					}
				}
			}
		}
		protected override string GetSqlCreateColumnTypeForBoolean(DBTable table, DBColumn column) {
			return "logical";
		}
		protected override string GetSqlCreateColumnTypeForByte(DBTable table, DBColumn column) {
			return "short";
		}
		protected override string GetSqlCreateColumnTypeForSByte(DBTable table, DBColumn column) {
			return "short";
		}
		protected override string GetSqlCreateColumnTypeForChar(DBTable table, DBColumn column) {
			return isADS10 ? "nchar(1)" : "char(1)";
		}
		protected override string GetSqlCreateColumnTypeForDecimal(DBTable table, DBColumn column) {
			return "money";
		}
		protected override string GetSqlCreateColumnTypeForDouble(DBTable table, DBColumn column) {
			return "double";
		}
		protected override string GetSqlCreateColumnTypeForSingle(DBTable table, DBColumn column) {
			return "double";
		}
		protected override string GetSqlCreateColumnTypeForInt32(DBTable table, DBColumn column) {
			return "integer";
		}
		protected override string GetSqlCreateColumnTypeForUInt32(DBTable table, DBColumn column) {
			return "money";
		}
		protected override string GetSqlCreateColumnTypeForInt16(DBTable table, DBColumn column) {
			return "short";
		}
		protected override string GetSqlCreateColumnTypeForUInt16(DBTable table, DBColumn column) {
			return "integer";
		}
		protected override string GetSqlCreateColumnTypeForInt64(DBTable table, DBColumn column) {
			return "numeric(20,0)";
		}
		protected override string GetSqlCreateColumnTypeForUInt64(DBTable table, DBColumn column) {
			return "numeric(21,0)";
		}
		public const int MaximumStringSize = 4000;
		const string CastToTimestampBegin = "(cast(";
		private const string CastToTimestampEnd = " as sql_timestamp))";
		protected override string GetSqlCreateColumnTypeForString(DBTable table, DBColumn column) {
			if(column.Size > 0 && column.Size <= MaximumStringSize)
				return (isADS10 ? "nchar(" : "char(") + column.Size.ToString(CultureInfo.InvariantCulture) + ')';
			else
				return isADS10 ? "nmemo" : "memo";
		}
		protected override string GetSqlCreateColumnTypeForDateTime(DBTable table, DBColumn column) {
			return "timestamp";
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
			if(column.IsKey && column.IsIdentity && (column.ColumnType == DBColumnType.Int32 || column.ColumnType == DBColumnType.Int64) && IsSingleColumnPKColumn(table, column)) {
				if(column.ColumnType == DBColumnType.Int64) {
					throw new NotSupportedException(Res.GetString(Res.ConnectionProvider_TheAutoIncrementedKeyWithX0TypeIsNotSupport, column.ColumnType, this.GetType()));
				}
				result = "AutoInc";
			}
			if(column.IsKey || !column.IsNullable) {
				result += " CONSTRAINT NOT NULL";
			}
			if(!column.IsIdentity) {
				if(!string.IsNullOrEmpty(column.DbDefaultValue)) {
					result += string.Concat(" DEFAULT ", FormatString(column.DbDefaultValue), "");
				}
				else {
					if(column.DefaultValue != null && column.DefaultValue != System.DBNull.Value) {
						try {
							string formattedDefaultValue = FormatConstant(column.DefaultValue);
							result += string.Concat(" DEFAULT ", FormatString(formattedDefaultValue), "");
						}
						catch(ArgumentException ex) {
							throw new ArgumentException(Res.GetString(Res.SqlConnectionProvider_CannotCreateAColumnForTheX0FieldWithTheX1D, column.Name, ex.Data["Value"]), ex);
						}
					}
				}
			}
			return result;
		}
		protected override object ReformatReadValue(object value, ConnectionProviderSql.ReformatReadValueArgs args) {
			if(args.DbTypeCode == TypeCode.Decimal) {
				switch(args.TargetTypeCode) {
					case TypeCode.UInt32:
					case TypeCode.UInt64:
					case TypeCode.Int64:
						return Convert.ChangeType(((decimal)value), args.TargetTypeCode, CultureInfo.InvariantCulture);
					case TypeCode.Decimal:
						return ((decimal)value);
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
					break;
				case TypeCode.SByte:
					return (Int16)(SByte)clientValue;
				case TypeCode.UInt32:
					return (Decimal)(UInt32)clientValue;
				case TypeCode.UInt64:
					return (Decimal)(UInt64)clientValue;
				case TypeCode.Int64:
					return (Decimal)(Int64)clientValue;
				case TypeCode.Single:
					return (double)(Single)clientValue;
				case TypeCode.Decimal:
					return ((Decimal)clientValue);
			}
			return base.ConvertToDbParameter(clientValue, clientValueTypeCode);
		}
		GetPropertyValueDelegate getAdsCommandLastAutoinc;
		GetPropertyValueDelegate GetAdsCommandLastAutoinc {
			get {
				if(getAdsCommandLastAutoinc == null)
					InitDelegates();
				return getAdsCommandLastAutoinc;
			}
		}
		protected override Int64 GetIdentity(Query sql) {
			using(IDbCommand cmd = CreateCommand(sql)) {
				try {
					cmd.ExecuteNonQuery();
					return (int)GetAdsCommandLastAutoinc(cmd);
				}
				catch(Exception e) {
					throw WrapException(e, cmd);
				}
			}
		}
		protected override async Task<Int64> GetIdentityAsync(Query sql, AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken) {
			using(IDbCommand cmd = CreateCommand(sql)) {
				try {
					await CommandExecuteNonQueryAsync(cmd, asyncOperationId, cancellationToken).ConfigureAwait(false);
					return (int)GetAdsCommandLastAutoinc(cmd);
				}
				catch(OperationCanceledException) {
					throw;
				}
				catch(Exception e) {
					throw WrapException(e, cmd);
				}
			}
		}
		void InitDelegates() {
			Type adsCommandType = ConnectionHelper.GetType("Advantage.Data.Provider.AdsCommand");
			getAdsCommandLastAutoinc = ReflectConnectionHelper.CreateGetPropertyDelegate(adsCommandType, "LastAutoinc");
		}
		protected override IDbConnection CreateConnection() {
			return ConnectionHelper.GetConnection(ConnectionString);
		}
		public static IDbConnection CreateConnection(string connectionString) {
			return ReflectConnectionHelper.GetConnection("Advantage.Data.Provider", "Advantage.Data.Provider.AdsConnection", connectionString);
		}
		protected override void CreateDataBase() {
			const int CannotOpenDatabaseError = 5004;
			ConnectionStringParser str = new ConnectionStringParser(ConnectionString);
			str.RemovePartByName("Pooling");
			using(IDbConnection conn = ConnectionHelper.GetConnection(str.GetConnectionString() + ";pooling=false;")) {
				try {
					conn.Open();
				}
				catch(Exception e) {
					object o;
					if(ConnectionHelper.TryGetExceptionProperty(e, "Number", out o)) {
						int number = (int)o;
						if(number == CannotOpenDatabaseError && CanCreateDatabase) {
							ConnectionStringParser helper = new ConnectionStringParser(ConnectionString);
							string baseFullPath = helper.GetPartByName("Data Source");
							helper.RemovePartByName("Data Source");
							string baseName = System.IO.Path.GetFileNameWithoutExtension(baseFullPath);
							string basePath = System.IO.Path.GetDirectoryName(baseFullPath);
							conn.ConnectionString = helper.GetConnectionString() + ";Data Source=" + basePath + ";tabletype=ads_adt";
							conn.Open();
							using(IDbCommand c = conn.CreateCommand()) {
								c.CommandText = "Create Database " + baseName;
								c.ExecuteNonQuery();
							}
						}
						else {
							throw new UnableToOpenDatabaseException(XpoDefault.ConnectionStringRemovePassword(ConnectionString), e);
						}
					}
					else {
						throw new UnableToOpenDatabaseException(XpoDefault.ConnectionStringRemovePassword(ConnectionString), e);
					}
				}
			}
		}
		protected override Exception WrapException(Exception e, IDbCommand query) {
			object o;
			if(ConnectionHelper.TryGetExceptionProperty(e, "Number", out o)) {
				int number = (int)o;
				if(number == 2121 || number == 7041)
					return new SchemaCorrectionNeededException(e);
				if(number == 5141 || number == 7057)
					return new ConstraintViolationException(query.CommandText, GetParametersString(query), e);
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
					inList.Add(":p" + i.ToString(CultureInfo.InvariantCulture));
					++i;
				}
			}
			if(inList.Count == 0)
				return new SelectStatementResult();
			return SelectData(new Query(string.Format(CultureInfo.InvariantCulture, queryText, StringListHelper.DelimitedText(inList, ",")), parameters, inList));
		}
		string GetTypeNameFromCode(int code) {
			switch(code) {
				case 1:
					return "logical";
				case 2:
					return "numeric";
				case 3:
					return "date";
				case 4:
					return "char";
				case 5:
					return "memo";
				case 6:
					return "blob";
				case 7:
					return "image";
				case 8:
					return "varchar";
				case 9:
					return "compactdate";
				case 10:
					return "double";
				case 11:
					return "integer";
				case 12:
					return "short";
				case 13:
					return "time";
				case 14:
					return "timestamp";
				case 15:
					return "autoinc";
				case 16:
					return "raw";
				case 17:
					return "curdouble";
				case 18:
					return "money";
				case 19:
					return "longlong";
				case 20:
					return "cistring";
				case 21:
					return "rowversion";
				case 22:
					return "modtime";
				case 26:
					return "nchar";
				case 27:
					return "nvarchar";
				case 28:
					return "nmemo";
			}
			return string.Empty;
		}
		string GetFullTypeName(string typeName, short size, short scale) {
			if(string.IsNullOrEmpty(typeName)) {
				return typeName;
			}
			switch(typeName) {
				case "char":
				case "nchar":
				case "varchar":
				case "nvarchar":
					return string.Concat(typeName, "(", size.ToString(CultureInfo.InvariantCulture), ")");
				case "numeric":
					return string.Format(CultureInfo.InvariantCulture, "{0}({1},{2})", typeName, size, scale);
			}
			return typeName;
		}
		DBColumnType GetTypeFromCode(int code, out bool isIdentity, ref short size) {
			isIdentity = false;
			switch(code) {
				case 1:
					return DBColumnType.Boolean;
				case 2:
					return DBColumnType.Int64;
				case 3:
					return DBColumnType.DateTime;
				case 4:
					return DBColumnType.String;
				case 5:
					return DBColumnType.String;
				case 6:
					return DBColumnType.ByteArray;
				case 7:
					return DBColumnType.ByteArray;
				case 8:
					return DBColumnType.String;
				case 9:
					return DBColumnType.DateTime;
				case 10:
					return DBColumnType.Double;
				case 11:
					return DBColumnType.Int32;
				case 12:
					return DBColumnType.Int16;
				case 13:
					return DBColumnType.DateTime;
				case 14:
					return DBColumnType.DateTime;
				case 15:
					isIdentity = true;
					return DBColumnType.Int32;
				case 16:
					return DBColumnType.ByteArray;
				case 17:
					return DBColumnType.Decimal;
				case 18:
					return DBColumnType.Decimal;
				case 19:
					return DBColumnType.Int64;
				case 20:
				case 26: 
				case 27: 
					return DBColumnType.String;
				case 28: 
					size = -1;
					return DBColumnType.String;
				case 21:
					return DBColumnType.Int32;
				case 22:
					return DBColumnType.DateTime;
			}
			return DBColumnType.Unknown;
		}
		ParameterValue CreateParameterForSystemQuery(int tag, string value, int size = 200) {
			return new ParameterValue(tag) { Value = value, DBType = DBColumnType.String, Size = size };
		}
		void GetColumns(DBTable table) {
			var tableNameParameter = new string[] { ":p" };
			var tableNameParameterValue = new QueryParameterCollection(CreateParameterForSystemQuery(0, ComposeSafeTableName(table.Name)));
			if(!table.IsView) {
				foreach(SelectStatementResultRow row in SelectData(new Query("select Name, Field_Type, Field_Length, Field_Default_Value, Field_Can_Be_Null, Field_Decimal from system.columns where parent = :p", tableNameParameterValue, tableNameParameter)).Rows) {
					bool isIdentity;
					short size = Convert.ToInt16(row.Values[2]);
					short scale = Convert.ToInt16(row.Values[5]);
					int typeCode = Convert.ToInt32(row.Values[1]);
					DBColumnType type = GetTypeFromCode(typeCode, out isIdentity, ref size);
					string name = row.Values[0] as string;
					string dbDefaultValue = (row.Values[3] as string);
					bool isNullable = Convert.ToBoolean(row.Values[4]);
					object defaultValue = null;
					if(!string.IsNullOrEmpty(dbDefaultValue)) {
						string scalarQuery = string.Concat("select ", dbDefaultValue, " from system.iota");
						try {
							defaultValue = FixDBNullScalar(GetScalar(new Query(scalarQuery)));
						}
						catch {
							if(type == DBColumnType.DateTime) {
								DateTime dt;
								if(TryExtractTimestampFromSTOTSFunction(dbDefaultValue, out dt)) {
									defaultValue = dt;
								}
							}
							if(type == DBColumnType.Char || type == DBColumnType.String) {
								defaultValue = dbDefaultValue;
							}
						}
					}
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
					table.AddColumn(new DBColumn(name == null ? string.Empty : name.TrimEnd(), false, GetFullTypeName(GetTypeNameFromCode(typeCode), size, scale), size, type, isNullable, defaultValue) {
						IsIdentity = isIdentity,
						DbDefaultValue = dbDefaultValue
					});
				}
			}
			if(table.Columns.Count == 0) {
				string viewStatement = GetScalar(new Query("select View_Stmt from system.views where name = :p", tableNameParameterValue, tableNameParameter)) as string;
				if(!string.IsNullOrEmpty(viewStatement)) {
					table.IsView = true;
					var result = SelectData(new Query(string.Format("select top 0 * from ({0}) t", viewStatement)), true);
					if(result != null && result.Rows != null && result.Rows.Length > 0) {
						foreach(var metadataColumn in result.Rows) {
							if(metadataColumn.Values.Length == 3) {
								string name = metadataColumn.Values[0] as string;
								string dbTypeName = metadataColumn.Values[1] as string;
								string dbColumnTypeString = metadataColumn.Values[2] as string;
								DBColumnType dbColumnType;
								if(!Enum.TryParse<DBColumnType>(dbColumnTypeString, out dbColumnType)) {
									dbColumnType = DBColumnType.Unknown;
								}
								table.AddColumn(new DBColumn(name == null ? string.Empty : name.TrimEnd(), false, dbTypeName ?? string.Empty, -1, dbColumnType));
							}
						}
					}
				}
			}
		}
		bool TryExtractTimestampFromSTOTSFunction(string expression, out DateTime outDateTime) {
			const string dateFormat = "yyyyMMdd HH:mm:ss.fff";
			const string expectedPrefix = "STOTS('";
			const string expectedSuffix = "')";
			int expectedLength = expectedPrefix.Length + dateFormat.Length + expectedSuffix.Length;
			if(expression.StartsWith(expectedPrefix) && expression.EndsWith(expectedSuffix) && expression.Length == expectedLength) {
				string dateString = expression.Substring(expectedPrefix.Length, dateFormat.Length);
				return DateTime.TryParseExact(dateString, dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out outDateTime);
			}
			outDateTime = DateTime.MinValue;
			return false;
		}
		public override void CreatePrimaryKey(DBTable table) {
			base.CreateIndex(table, table.PrimaryKey);
			using(IDbCommand cmd = CreateCommand()) {
				try {
					cmd.CommandType = CommandType.StoredProcedure;
					cmd.CommandText = "sp_ModifyTableProperty";
					AddParameter(cmd, "TableName", ComposeSafeTableName(table.Name));
					AddParameter(cmd, "Property", "TABLE_PRIMARY_KEY");
					AddParameter(cmd, "Value", ComposeSafeConstraintName(GetIndexName(table.PrimaryKey, table)));
					AddParameter(cmd, "ValidationOption", DBNull.Value);
					AddParameter(cmd, "FailTable", DBNull.Value);
					cmd.ExecuteNonQuery();
				}
				catch(Exception e) {
					throw WrapException(e, cmd);
				}
			}
		}
		void AddParameter(IDbCommand cmd, string name, object value) {
			IDbDataParameter p = cmd.CreateParameter();
			p.ParameterName = name;
			p.Value = value;
			cmd.Parameters.Add(p);
		}
		void GetPrimaryKey(DBTable table) {
			SelectStatementResult data = SelectData(new Query(@"select i.Index_Expression from system.indexes i inner join system.tables t on t.Table_Primary_Key = i.Name where t.Name = :p",
				new QueryParameterCollection(CreateParameterForSystemQuery(0, ComposeSafeTableName(table.Name))), new string[] { ":p" }));
			if(data.Rows.Length > 0) {
				StringCollection cols = new StringCollection();
				if(data.Rows[0].Values[0] is DBNull) return;
				string columns = (string)data.Rows[0].Values[0];
				cols.AddRange(columns.TrimEnd().Split(';'));
				foreach(string columnName in cols) {
					DBColumn column = table.GetColumn(columnName);
					if(column != null)
						column.IsKey = true;
				}
				table.PrimaryKey = new DBPrimaryKey(cols);
			}
		}
		bool IsColumnsEqual(StringCollection first, StringCollection second) {
			if(first.Count != second.Count)
				return false;
			for(int i = 0; i < first.Count; i++)
				if(String.Compare(ComposeSafeColumnName(first[i]), ComposeSafeColumnName(second[i]), true) != 0)
					return false;
			return true;
		}
		public override void CreateIndex(DBTable table, DBIndex index) {
			if(table.Name != "XPObjectType" && (table.PrimaryKey == null || !IsColumnsEqual(table.PrimaryKey.Columns, index.Columns))) {
				base.CreateIndex(table, index);
			}
		}
		void GetIndexes(DBTable table) {
			SelectStatementResult data = SelectData(new Query(
				@"select Name, Index_Expression, Index_Options from system.indexes where Index_FTS_Delimiters is null and Parent = :p", new QueryParameterCollection(CreateParameterForSystemQuery(0, ComposeSafeTableName(table.Name))), new string[] { "p" }));
			foreach(SelectStatementResultRow row in data.Rows) {
				StringCollection list = new StringCollection();
				if(row.Values[1] is DBNull) continue;
				list.AddRange(((string)row.Values[1]).TrimEnd().Split(';'));
				DBIndex index = new DBIndex(((string)row.Values[0]).TrimEnd(), list, (Convert.ToInt32(row.Values[2]) & 1) != 0 ? true : false);
				table.Indexes.Add(index);
			}
		}
		void GetForeignKeys(DBTable table) {
			SelectStatementResult data = SelectData(new Query(
				@"select r.RI_Primary_Table, i.Index_Expression, ip.Index_Expression from system.relations r
				inner join system.indexes i on r.RI_Foreign_Index = i.Name
				inner join system.indexes ip on r.RI_Primary_Index = ip.Name
				where r.RI_Foreign_Table = :p", new QueryParameterCollection(CreateParameterForSystemQuery(0, ComposeSafeTableName(table.Name))), new string[] { "p" }));
			Hashtable fks = new Hashtable();
			foreach(SelectStatementResultRow row in data.Rows) {
				StringCollection fkc = new StringCollection();
				if(row.Values[1] is DBNull || row.Values[2] is DBNull) continue;
				fkc.AddRange(((string)row.Values[1]).TrimEnd().Split(';'));
				StringCollection pkc = new StringCollection();
				pkc.AddRange(((string)row.Values[2]).TrimEnd().Split(';'));
				DBForeignKey fk = new DBForeignKey(fkc, (string)row.Values[0], pkc);
				table.ForeignKeys.Add(fk);
			}
		}
		public override void CreateForeignKey(DBTable table, DBForeignKey fk) {
			using(IDbCommand cmd = CreateCommand()) {
				try {
					cmd.CommandType = CommandType.StoredProcedure;
					cmd.CommandText = "sp_CreateReferentialIntegrity";
					AddParameter(cmd, "Name", ComposeSafeConstraintName(GetForeignKeyName(fk, table)));
					AddParameter(cmd, "PrimaryTable", ComposeSafeTableName(fk.PrimaryKeyTable));
					AddParameter(cmd, "ForeignTable", ComposeSafeTableName(table.Name));
					AddParameter(cmd, "ForeignKey", ComposeSafeConstraintName(GetIndexName(new DBIndex(fk.Columns, false), table)));
					AddParameter(cmd, "UpdateRule", 2);
					AddParameter(cmd, "DeleteRule", 2);
					AddParameter(cmd, "FailTable", DBNull.Value);
					AddParameter(cmd, "PrimaryKeyError", DBNull.Value);
					AddParameter(cmd, "CascadeError", DBNull.Value);
					cmd.ExecuteNonQuery();
				}
				catch(Exception e) {
					throw WrapException(e, cmd);
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
			foreach(SelectStatementResultRow row in GetDataForTables(tables, null, "select name from system.tables where name in ({0})").Rows)
				dbTables.Add(((string)row.Values[0]).TrimEnd(), false);
			foreach(SelectStatementResultRow row in GetDataForTables(tables, null, "select name from system.views where name in ({0})").Rows)
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
			return 60;
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
					return string.Format(CultureInfo.InvariantCulture, "{0} % {1}", leftOperand, rightOperand);
				case BinaryOperatorType.BitwiseOr:
					return string.Format(CultureInfo.InvariantCulture, "({0} Or {1})", leftOperand, rightOperand);
				case BinaryOperatorType.BitwiseAnd:
					return string.Format(CultureInfo.InvariantCulture, "({0} And {1})", leftOperand, rightOperand);
				default:
					return base.FormatBinary(operatorType, leftOperand, rightOperand);
			}
		}
		ExecMethodDelegate commandBuilderDeriveParametersHandler;
		protected override void CommandBuilderDeriveParameters(IDbCommand command) {
			if(commandBuilderDeriveParametersHandler == null) {
				commandBuilderDeriveParametersHandler = ReflectConnectionHelper.GetCommandBuilderDeriveParametersDelegate("Advantage.Data.Provider", "Advantage.Data.Provider.AdsCommandBuilder");
			}
			commandBuilderDeriveParametersHandler(command);
		}
		protected override SelectedData ExecuteSproc(string sprocName, params OperandValue[] parameters) {
			using(IDbCommand command = CreateCommand()) {
				command.CommandText = "select count(*) from system.storedprocedures where name = :p0";
				command.Parameters.Add(CreateParameter(command, sprocName, ":p0", DBColumnType.String, null, 200));
				if(((int)command.ExecuteScalar()) > 0) {
					command.Parameters.Clear();
					command.CommandType = CommandType.StoredProcedure;
					command.CommandText = ComposeSafeTableName(sprocName);
					CommandBuilderDeriveParameters(command);
					IDataParameter returnParameter;
					List<IDataParameter> outParameters;
					PrepareParametersForExecuteSproc(parameters, command, out outParameters, out returnParameter);
					return ExecuteSprocInternal(command, returnParameter, outParameters);
				}
				PrepareCommandForExecuteSproc(command, sprocName, parameters);
				List<SelectStatementResult> selectStatementResults = GetSelectedStatementResults(command);
				return new SelectedData(selectStatementResults.ToArray());
			}
		}
		protected override async Task<SelectedData> ExecuteSprocAsync(AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken, string sprocName, params OperandValue[] parameters) {
			using(IDbCommand command = CreateCommand()) {
				command.CommandText = "select count(*) from system.storedprocedures where name = :p0";
				command.Parameters.Add(CreateParameter(command, sprocName, ":p0", DBColumnType.String, null, 200));
				if(((int)(await CommandExecuteScalarAsync(command, asyncOperationId, cancellationToken).ConfigureAwait(false))) > 0) {
					command.Parameters.Clear();
					command.CommandType = CommandType.StoredProcedure;
					command.CommandText = ComposeSafeTableName(sprocName);
					CommandBuilderDeriveParameters(command);
					IDataParameter returnParameter;
					List<IDataParameter> outParameters;
					PrepareParametersForExecuteSproc(parameters, command, out outParameters, out returnParameter);
					return await ExecuteSprocInternalAsync(command, returnParameter, outParameters, asyncOperationId, cancellationToken).ConfigureAwait(false);
				}
				PrepareCommandForExecuteSproc(command, sprocName, parameters);
				List<SelectStatementResult> selectStatementResults = await GetSelectedStatementResultsAsync(command, asyncOperationId, cancellationToken).ConfigureAwait(false);
				return new SelectedData(selectStatementResults.ToArray());
			}
		}
		void PrepareCommandForExecuteSproc(IDbCommand command, string sprocName, OperandValue[] parameters) {
			string[] parametersList = new string[parameters.Length];
			command.Parameters.Clear();
			for(int i = 0; i < parameters.Length; i++) {
				bool createParameter = true;
				parametersList[i] = GetParameterName(parameters[0], i, ref createParameter);
				if(createParameter) {
					ParameterValue param = parameters[i] as ParameterValue;
					if(!ReferenceEquals(param, null)) {
						command.Parameters.Add(CreateParameter(command, param.Value, parametersList[i], param.DBType, param.DBTypeName, param.Size));
					}
					else {
						command.Parameters.Add(CreateParameter(command, parameters[i].Value, parametersList[i], DBColumnType.Unknown, null, 0));
					}
				}
			}
			command.CommandText = string.Format("select {0}({1}) from system.iota", ComposeSafeTableName(sprocName), string.Join(", ", parametersList));
		}
		public override DBStoredProcedure[] GetStoredProcedures() {
			List<DBStoredProcedure> result = new List<DBStoredProcedure>();
			using(var command = Connection.CreateCommand()) {
				command.CommandText = "select name from system.storedprocedures";
				using(var reader = command.ExecuteReader()) {
					while(reader.Read()) {
						DBStoredProcedure curSproc = new DBStoredProcedure();
						curSproc.Name = reader.GetString(0);
						result.Add(curSproc);
					}
				}
			}
			foreach(DBStoredProcedure curSproc in result) {
				using(var command = Connection.CreateCommand()) {
					command.CommandType = CommandType.StoredProcedure;
					command.CommandText = ComposeSafeTableName(curSproc.Name);
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
					}
					curSproc.Arguments.AddRange(dbArguments);
				}
			}
			return result.ToArray();
		}
		static string FormatMod(string arg, int multiplier, int divider) {
			return string.Format("Cast(Truncate(Cast({0} as SQL_DOUBLE) * {1}, 0) % {2} as sql_integer)", arg, multiplier, divider);
		}
		static string FormatGetInt(string arg, int multiplier, int divider) {
			return string.Format("Cast(Truncate(Cast({0} as SQL_DOUBLE) * {1} / {2}, 0) as sql_integer)", arg, multiplier, divider);
		}
		static string FnAddDateTime(string datetimeOperand, string dayPart, string secondPart) {
			return string.Format(CultureInfo.InvariantCulture, "TIMESTAMPADD(SQL_TSI_Second, {2},TIMESTAMPADD(SQL_TSI_DAY, {1}, {0}))", WrapWithCastAsTimestamp(datetimeOperand), dayPart, secondPart);
		}
		static string WrapWithCastAsTimestamp(string arg) {
			if(arg.StartsWith(CastToTimestampBegin) && arg.EndsWith(CastToTimestampEnd))
				return arg;
			return string.Concat(CastToTimestampBegin + arg + CastToTimestampEnd);
		}
		public override string FormatFunction(FunctionOperatorType operatorType, params string[] operands) {
			switch(operatorType) {
				case FunctionOperatorType.Abs:
					return string.Format(CultureInfo.InvariantCulture, "(abs(cast({0} as SQL_NUMERIC(20,10))))", operands[0]);
				case FunctionOperatorType.BigMul:
					return string.Format(CultureInfo.InvariantCulture, "(cast({0} as sql_numeric(38,0)) * cast({1} as sql_numeric(38,0) ))", operands[0], operands[1]);
				case FunctionOperatorType.Sqr:
					return string.Format(CultureInfo.InvariantCulture, "sqrt({0})", operands[0]);
				case FunctionOperatorType.Rnd:
					return "rand()";
				case FunctionOperatorType.Log:
					switch(operands.Length) {
						case 1:
							return string.Format(CultureInfo.InvariantCulture, "Log({0})", operands[0]);
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "(Log({0})/Log({1}))", operands[0], operands[1]);
					}
					goto default;
				case FunctionOperatorType.Log10:
					return string.Format(CultureInfo.InvariantCulture, "Log10({0})", operands[0]);
				case FunctionOperatorType.Sinh:
					return string.Format(CultureInfo.InvariantCulture, "( (Exp({0}) - Exp(({0} * (-1) ))) / 2 )", operands[0]);
				case FunctionOperatorType.Cosh:
					return string.Format(CultureInfo.InvariantCulture, "( (Exp({0}) + Exp(({0} * (-1) ))) / 2 )", operands[0]);
				case FunctionOperatorType.Tanh:
					return string.Format(CultureInfo.InvariantCulture, "( (Exp({0}) - Exp(({0} * (-1) ))) / (Exp({0}) + Exp(({0} * (-1) ))) )", operands[0]);
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
				case FunctionOperatorType.Max:
					return string.Format(CultureInfo.InvariantCulture, "iif({0} > {1}, {0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.Min:
					return string.Format(CultureInfo.InvariantCulture, "iif({0} < {1}, {0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.Floor:
					return string.Format(CultureInfo.InvariantCulture, "Floor({0})", operands[0]);
				case FunctionOperatorType.Ceiling:
					return string.Format(CultureInfo.InvariantCulture, "Ceiling( (cast({0} as SQL_DOUBLE)) )", operands[0]);
				case FunctionOperatorType.IsNull:
					switch(operands.Length) {
						case 1:
							return string.Format(CultureInfo.InvariantCulture, "({0} is null)", operands[0]);
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "Coalesce({0}, {1})", operands[0], operands[1]);
					}
					goto default;
				case FunctionOperatorType.IsNullOrEmpty:
					return string.Format(CultureInfo.InvariantCulture, "(({0}) is null or length({0}) = 0)", operands[0]);
				case FunctionOperatorType.GetMilliSecond:
					return string.Format(CultureInfo.InvariantCulture, "Frac_Second({0})", WrapWithCastAsTimestamp(operands[0]));
				case FunctionOperatorType.GetSecond:
					return string.Format(CultureInfo.InvariantCulture, "Second({0})", operands[0]);
				case FunctionOperatorType.GetMinute:
					return string.Format(CultureInfo.InvariantCulture, "Minute({0})", operands[0]);
				case FunctionOperatorType.GetHour:
					return string.Format(CultureInfo.InvariantCulture, "Hour({0})", operands[0]);
				case FunctionOperatorType.GetDay:
					return string.Format(CultureInfo.InvariantCulture, "DayOfMonth({0})", operands[0]);
				case FunctionOperatorType.GetMonth:
					return string.Format(CultureInfo.InvariantCulture, "Month({0})", operands[0]);
				case FunctionOperatorType.GetYear:
					return string.Format(CultureInfo.InvariantCulture, "Year({0})", operands[0]);
				case FunctionOperatorType.GetTimeOfDay:
					return string.Format(CultureInfo.InvariantCulture, "(cast(((cast((cast(Hour({0}) as sql_numeric(19,0)) * 36000000000) as sql_numeric(19,0))) + " +
																		 "(cast((cast(Minute({0}) as sql_numeric(19,0)) * 600000000) as sql_numeric(19,0))) + " +
																		 "(cast((cast(Second({0}) as sql_numeric(19,0)) * 10000000) as sql_numeric(19,0)))) as sql_numeric(19,0)))", WrapWithCastAsTimestamp(operands[0]));
				case FunctionOperatorType.GetDayOfWeek:
					return string.Format(CultureInfo.InvariantCulture, "(Mod(((DayOfWeek({0})- DayOfWeek('1900-01-01'))  + 8) , 7))", WrapWithCastAsTimestamp(operands[0]));
				case FunctionOperatorType.GetDayOfYear:
					return string.Format(CultureInfo.InvariantCulture, "DayOfYear(cast({0} as sql_date))", operands[0]);
				case FunctionOperatorType.GetDate:
					return string.Format(CultureInfo.InvariantCulture, "cast({0} as sql_date)", operands[0]);
				case FunctionOperatorType.AddTicks:
					return string.Format(CultureInfo.InvariantCulture, "TIMESTAMPADD(SQL_TSI_SECOND, (cast(({1} / 10000000) as sql_integer)), {0})", WrapWithCastAsTimestamp(operands[0]), operands[1]);
				case FunctionOperatorType.AddMilliSeconds:
					return string.Format(CultureInfo.InvariantCulture, "TIMESTAMPADD(SQL_TSI_SECOND, (cast(({1} / 1000) as sql_integer)), {0})", WrapWithCastAsTimestamp(operands[0]), operands[1]);
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
					return string.Format(CultureInfo.InvariantCulture, "TIMESTAMPADD(SQL_TSI_MONTH, cast({1} as sql_integer), {0})", WrapWithCastAsTimestamp(operands[0]), operands[1]);
				case FunctionOperatorType.AddYears:
					return string.Format(CultureInfo.InvariantCulture, "TIMESTAMPADD(SQL_TSI_YEAR, cast({1} as sql_integer), {0})", WrapWithCastAsTimestamp(operands[0]), operands[1]);
				case FunctionOperatorType.DateDiffYear:
					return string.Format(CultureInfo.InvariantCulture, "TIMESTAMPDIFF(SQL_TSI_YEAR, {0}, {1})", WrapWithCastAsTimestamp(operands[0]), WrapWithCastAsTimestamp(operands[1]));
				case FunctionOperatorType.DateDiffMonth:
					return string.Format(CultureInfo.InvariantCulture, "(((Year({1}) - Year({0})) * 12) + Month({1}) - Month({0}))", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffDay:
					return string.Format(CultureInfo.InvariantCulture, "(TIMESTAMPDIFF(SQL_TSI_Day, (cast({0} as sql_date)), (cast({1} as sql_date))))", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffHour:
					return string.Format(CultureInfo.InvariantCulture, "(TIMESTAMPDIFF(SQL_TSI_Day,(cast({0} as sql_date)),(cast({1} as sql_date)))*24 + Hour({1}) - Hour({0}))", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffMinute:
					return string.Format(CultureInfo.InvariantCulture, "((TIMESTAMPDIFF(SQL_TSI_Day,(cast({0} as sql_date)),(cast({1} as sql_date)))*24 + Hour({1}) - Hour({0})) * 60 + Minute({1}) - Minute({0}))", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffSecond:
					return string.Format(CultureInfo.InvariantCulture, "(TIMESTAMPDIFF(SQL_TSI_Second, {0}, {1}) + iif((Frac_Second({1}) - Frac_Second({0})) < 1, 1, 0))", WrapWithCastAsTimestamp(operands[0]), WrapWithCastAsTimestamp(operands[1]));
				case FunctionOperatorType.DateDiffMilliSecond:
					return string.Format(CultureInfo.InvariantCulture, "(TIMESTAMPDIFF(SQL_TSI_Second, {0}, {1}) * 1000 + Frac_Second({1}) - Frac_Second({0}))", WrapWithCastAsTimestamp(operands[0]), WrapWithCastAsTimestamp(operands[1]));
				case FunctionOperatorType.DateDiffTick:
					return string.Format(CultureInfo.InvariantCulture, "(((TIMESTAMPDIFF(SQL_TSI_Second, {0}, {1})) * 1000 + Frac_Second({1}) - Frac_Second({0})) * 10000)", WrapWithCastAsTimestamp(operands[0]), WrapWithCastAsTimestamp(operands[1]));
				case FunctionOperatorType.Now:
					return "Now()";
				case FunctionOperatorType.Today:
					return "cast((Now()) as sql_date)";
				case FunctionOperatorType.Ascii:
					return string.Format(CultureInfo.InvariantCulture, "Ascii({0})", operands[0]);
				case FunctionOperatorType.Char:
					return string.Format(CultureInfo.InvariantCulture, "Char({0})", operands[0]);
				case FunctionOperatorType.ToInt:
					return string.Format(CultureInfo.InvariantCulture, "CAST({0} AS SQL_INTEGER)", operands[0]);
				case FunctionOperatorType.ToLong:
					throw new NotSupportedException();
				case FunctionOperatorType.ToFloat:
					throw new NotSupportedException();
				case FunctionOperatorType.ToDouble:
					return string.Format(CultureInfo.InvariantCulture, "CAST({0} AS SQL_DOUBLE)", operands[0]);
				case FunctionOperatorType.ToDecimal:
					return string.Format(CultureInfo.InvariantCulture, "CAST({0} as SQL_MONEY)", operands[0]);
				case FunctionOperatorType.ToStr:
					return string.Format(CultureInfo.InvariantCulture, "cast({0} as SQL_VARCHAR(32000))", operands[0]);
				case FunctionOperatorType.Concat:
					return FnConcat(operands);
				case FunctionOperatorType.Replace:
					return string.Format(CultureInfo.InvariantCulture, "Replace({0},(cast({1} as sql_varchar(64))), (cast({2} as sql_varchar(32000))))", operands[0], operands[1], operands[2]);
				case FunctionOperatorType.Reverse:
					throw new NotSupportedException();
				case FunctionOperatorType.PadLeft:
					switch(operands.Length) {
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "(iif(({1} > (Length({0}))) , space({1} - (Length({0}))), '') + {0} )", operands[0], operands[1]);
						case 3:
							return string.Format(CultureInfo.InvariantCulture, "(iif(({1} > (Length({0}))) , repeat(cast({2} as sql_varchar(1)), ({1} - (Length({0})))) , '') + {0} )", operands[0], operands[1], operands[2]);
					}
					goto default;
				case FunctionOperatorType.PadRight:
					switch(operands.Length) {
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "(cast({0} as sql_varchar(32000)) + iif(({1} > (Length({0}))) , space({1} - (Length({0}))), '') )", operands[0], operands[1]);
						case 3:
							return string.Format(CultureInfo.InvariantCulture, "(cast({0} as sql_varchar(32000)) + iif(({1} > (Length({0}))) , repeat(cast({2} as sql_varchar(1)), ({1} - (Length({0})))), ''))", operands[0], operands[1], operands[2]);
					}
					goto default;
				case FunctionOperatorType.Substring:
					switch(operands.Length) {
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "Substring({0}, {1} + 1, (Length({0}) - {1} ))", operands[0], operands[1]);
						case 3:
							return string.Format(CultureInfo.InvariantCulture, "Substring({0}, {1} + 1, {2})", operands[0], operands[1], operands[2]);
					}
					goto default;
				case FunctionOperatorType.Insert:
					return string.Format(CultureInfo.InvariantCulture, "insert({0}, {1} + 1, 0, cast({2} as sql_varchar(32000)))", operands[0], operands[1], operands[2]);
				case FunctionOperatorType.Remove:
					switch(operands.Length) {
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "insert({0}, {1} + 1,(Length({0}) - {1}), '')", operands[0], operands[1]);
						case 3:
							return string.Format(CultureInfo.InvariantCulture, "insert({0}, {1} + 1, {2}, '')", operands[0], operands[1], operands[2]);
					}
					goto default;
				case FunctionOperatorType.Len:
					return string.Format(CultureInfo.InvariantCulture, "Length({0})", operands[0]);
				case FunctionOperatorType.CharIndex:
					switch(operands.Length) {
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "(Locate( (cast({0} as sql_varchar(1))) , {1}) - 1)", operands[0], operands[1]);
						case 3:
							return string.Format(CultureInfo.InvariantCulture, "(Locate((cast({0} as sql_varchar(1))), {1}, {2} + 1) - 1)", operands[0], operands[1], operands[2]);
						case 4:
							return string.Format(CultureInfo.InvariantCulture, "(iif (Locate((cast({0} as sql_varchar(1))), Left({1}, ({2} - 1 + {3})), {2} + 1)=0,-1,Locate((cast({0} as sql_varchar(1))), Left({1}, ({2} - 1 + {3})), {2} + 1) - 1 + {2} ))", operands[0], operands[1], operands[2], operands[3]);
					}
					goto default;
				case FunctionOperatorType.EndsWith:
					return string.Format(CultureInfo.InvariantCulture, "(RigHt(Substring({0}, 1, Length({0})), Length({1})) = ({1}))", operands[0], operands[1]);
				case FunctionOperatorType.Contains:
					return string.Format(CultureInfo.InvariantCulture, "(LocaTE({1}, {0}) > 0)", operands[0], operands[1]);
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
							return string.Format(CultureInfo.InvariantCulture, "(({0} likE {2}) And (LocatE({1}, {0}) = 1))", processParameter(operands[0]), processParameter(secondOperand), new ConstantValue(operandString.Substring(0, likeIndex) + "%"));
						}
					}
					return string.Format(CultureInfo.InvariantCulture, "(LocatE({1}, {0}) = 1)", processParameter(operands[0]), processParameter(operands[1]));
				default:
					return base.FormatFunction(processParameter, operatorType, operands);
			}
		}
		string FnConcat(string[] operands) {
			string args = "(";
			foreach(string arg in operands) {
				if(args.Length > 1)
					args += " + ";
				args += string.Format("cast({0} as sql_varchar(32000))", arg);
			}
			return args + ")";
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
			return ":p" + index.ToString(CultureInfo.InvariantCulture);
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
					else {
						return "'" + (char)value + "'";
					}
				case TypeCode.DateTime:
					DateTime datetimeValue = (DateTime)value;
					string dateTimeFormatPattern = "yyyyMMdd HH:mm:ss.fff";
					return string.Format("STOTS('{0}')", datetimeValue.ToString(dateTimeFormatPattern, CultureInfo.InvariantCulture));
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
			SelectStatementResult constraints = SelectData(new Query("select Name from system.relations"));
			command.CommandText = "sp_DropReferentialIntegrity";
			command.CommandType = CommandType.StoredProcedure;
			ReflectConnectionHelper.InvokeMethod(command.Parameters, "Add", new object[] { "Name", DbType.AnsiString }, true);
			foreach(SelectStatementResultRow row in constraints.Rows) {
				ReflectConnectionHelper.SetPropertyValue(command.Parameters[0], "Value", ((string)row.Values[0]).TrimEnd());
				command.ExecuteNonQuery();
			}
			command.CommandType = CommandType.Text;
			command.Parameters.Clear();
			string[] tables = GetStorageTablesList(false);
			foreach(string table in tables) {
				command.CommandText = "drop table \"" + table + "\"";
				command.ExecuteNonQuery();
			}
		}
		protected override void ProcessClearDatabase() {
			using(IDbCommand command = CreateCommand())
				ClearDatabase(command);
		}
		public override string[] GetStorageTablesList(bool includeViews) {
			SelectStatementResult tables = SelectData(new Query("select name from system.tables"));
			ArrayList result = new ArrayList(tables.Rows.Length);
			foreach(SelectStatementResultRow row in tables.Rows) {
				result.Add(((string)row.Values[0]).TrimEnd());
			}
			if(includeViews) {
				SelectStatementResult views = SelectData(new Query("select name from system.views"));
				foreach(SelectStatementResultRow row in views.Rows) {
					result.Add(((string)row.Values[0]).TrimEnd());
				}
			}
			return (string[])result.ToArray(typeof(string));
		}
	}
	public class AdvantageProviderFactory : ProviderFactory {
		public override IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return AdvantageConnectionProvider.CreateProviderFromConnection(connection, autoCreateOption);
		}
		public override IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			return AdvantageConnectionProvider.CreateProviderFromString(connectionString, autoCreateOption, out objectsToDisposeOnDisconnect);
		}
		public override string GetConnectionString(Dictionary<string, string> parameters) {
			if(!parameters.ContainsKey(DatabaseParamID)) { return null; }
			if(!parameters.ContainsKey(UserIDParamID)) {
				if(!parameters.ContainsKey(PasswordParamID)) {
					return AdvantageConnectionProvider.GetConnectionString(parameters[DatabaseParamID]);
				}
				return AdvantageConnectionProvider.GetConnectionString(parameters[DatabaseParamID], parameters[PasswordParamID]);
			}
			return AdvantageConnectionProvider.GetConnectionString(parameters[DatabaseParamID], parameters[UserIDParamID], parameters[PasswordParamID]);
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
		public override bool HasMultipleDatabases { get { return false; } }
		public override bool IsServerbased { get { return false; } }
		public override bool IsFilebased { get { return true; } }
		public override string ProviderKey { get { return AdvantageConnectionProvider.XpoProviderTypeString; } }
		public override string[] GetDatabases(string server, string userId, string password) {
			return new string[1] { server };
		}
		public override string FileFilter { get { return "Advantage databases|*.add"; } }
		public override bool MeanSchemaGeneration { get { return true; } }
	}
}
#pragma warning restore DX0024
namespace DevExpress.Xpo.DB.Helpers {
	using System;
	using System.Data;
	class DbTypeMapperAdvantage<TSqlParameter> : DbTypeMapper<DbType, TSqlParameter>
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
			precision = scale = null;
			return nameof(DbType.Int16);
		}
		protected override string GetParameterTypeNameForSingle(out byte? precision, out byte? scale) {
			precision = scale = null;
			return nameof(DbType.Double);
		}
		protected override string GetParameterTypeNameForString(out int? size) {
			size = 0;
			return nameof(DbType.String);
		}
		protected override string GetParameterTypeNameForTimeSpan() {
			return nameof(DbType.Double);
		}
		protected override string GetParameterTypeNameForUInt16(out byte? precision, out byte? scale) {
			precision = scale = null;
			return nameof(DbType.UInt16);
		}
		protected override string GetParameterTypeNameForUInt32(out byte? precision, out byte? scale) {
			precision = scale = null;
			return nameof(DbType.Decimal);
		}
		protected override string GetParameterTypeNameForUInt64(out byte? precision, out byte? scale) {
			precision = 21;
			scale = 0;
			return nameof(DbType.Decimal);
		}
		protected override string GetParameterTypeNameForDateOnly(out int? size) {
			size = null;
			return nameof(DbType.Date);
		}
		protected override string GetParameterTypeNameForTimeOnly(out int? size) {
			size = null;
			return nameof(DbType.Time);
		}
		protected override string ConvertSqlTypeToParameterType(string sqlType) {
			switch(sqlType.ToUpperInvariant()) {
				case "CHAR":
				case "NCHAR":
				case "CHARACTER":
				case "CICHARACTER":
					return nameof(DbType.StringFixedLength);
				case "DATE":
					return nameof(DbType.DateTime);
				case "LOGICAL":
					return nameof(DbType.Boolean);
				case "MEMO":
				case "NMEMO":
					return nameof(DbType.String);
				case "DOUBLE":
					return nameof(DbType.Double);
				case "INTEGER":
					return nameof(DbType.Int32);
				case "NUMERIC":
					return nameof(DbType.Decimal);
				case "IMAGE":
				case "BLOB":
				case "BINARY":
					return nameof(DbType.Binary);
				case "SHORTINTEGER":
				case "SHORT":
					return nameof(DbType.Int16);
				case "TIME":
					return nameof(DbType.Time);
				case "TIMESTAMP":
					return nameof(DbType.DateTime);
				case "AUTOINCREMENT":
					return nameof(DbType.Int32);
				case "CURDOUBLE":
					return nameof(DbType.Double);
				case "MONEY":
					return nameof(DbType.Decimal);
				case "ROWVERSION":
					return nameof(DbType.UInt64);
				case "VARCHAR":
					return nameof(DbType.String);
				case "VARBINARY":
					return nameof(DbType.Binary);
				default:
					return null;
			}
		}
	}
}
