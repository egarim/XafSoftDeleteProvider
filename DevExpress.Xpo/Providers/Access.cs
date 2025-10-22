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
using DevExpress.Xpo;
using DevExpress.Xpo.DB.Helpers;
#pragma warning disable DX0024
namespace DevExpress.Xpo.DB {
	using System.Collections;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.ComponentModel;
	using System.Data;
	using System.Globalization;
	using System.Reflection;
	using System.Runtime.Versioning;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Threading;
	using System.Threading.Tasks;
	using DevExpress.Data.Db;
	using DevExpress.Data.Filtering;
	using DevExpress.Data.Helpers;
	using DevExpress.Xpo.DB;
	using DevExpress.Xpo.DB.Exceptions;
	using DevExpress.Xpo.DB.Helpers;
	using DevExpress.Xpo.Helpers;
	public abstract class OleDBConnectionProvider : ConnectionProviderSql {
		public static bool SortColumnsAlphabetically = true;
		public OleDBConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption) : base(connection, autoCreateOption, true) { }
		protected OleDBConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption, bool openConnection) : base(connection, autoCreateOption, openConnection) { }
		ReflectConnectionHelper helper;
		protected ReflectConnectionHelper ConnectionHelper {
			get {
				if(helper == null)
					helper = new ReflectConnectionHelper(Connection, "System.Data.OleDb.OleDbException");
				return helper;
			}
		}
		OleDbReflectionHelperBase reflectionHelper;
		protected OleDbReflectionHelperBase ReflectionHelper {
			get {
				if(reflectionHelper == null) {
					Type oleDbConnectionType = ConnectionHelper.ConnectionType;
					Type oleDbSchemaGuidType = ConnectionHelper.GetType("System.Data.OleDb.OleDbSchemaGuid");
					Type oleDbExceptionType = ConnectionHelper.ExceptionTypes[0];
					Type oleDbErrorType = ConnectionHelper.GetType("System.Data.OleDb.OleDbError");
					reflectionHelper = (OleDbReflectionHelperBase)Activator.CreateInstance(typeof(OleDbReflectionHelper<,,,>).MakeGenericType(oleDbConnectionType, oleDbSchemaGuidType, oleDbExceptionType, oleDbErrorType));
				}
				return reflectionHelper;
			}
		}
		static readonly ConcurrentDictionary<Type, MethodInfo> getProviderNameMethods = new ConcurrentDictionary<Type, MethodInfo>();
		protected static string GetOleDbProviderName(IDbConnection connection) {
			MethodInfo mi = getProviderNameMethods.GetOrAdd(connection.GetType(), t => {
				PropertyInfo pi = t.GetProperty("Provider", BindingFlags.Public | BindingFlags.Instance);
				return pi != null ? pi.GetGetMethod() : null;
			});
			if(mi != null) {
				return (string)mi.Invoke(connection, null);
			}
			return null;
		}
		protected internal static IDbConnection CreateConnection(string connectionString) {
#if !NET
			const string assembly = "System.Data";
#else
			const string assembly = "System.Data.OleDb";
#endif
			return ReflectConnectionHelper.GetConnection(assembly, "System.Data.OleDb.OleDbConnection", connectionString);
		}
		public override ICollection CollectTablesToCreate(ICollection tables) {
			ArrayList list = new ArrayList();
			foreach(DBTable table in tables) {
				DataTable data = ReflectionHelper.GetTables(Connection, new object[] { null, null, ComposeSafeTableName(table.Name), null });
				DataTable proc = ReflectionHelper.GetProcedures(Connection, new object[] { null, null, ComposeSafeTableName(table.Name), null });
				if(data.Rows.Count == 0 && proc.Rows.Count == 0) {
					list.Add(table);
				}
				else if(data.Rows.Count != 0)
					table.IsView = (string)data.Rows[0]["TABLE_TYPE"] == "VIEW";
				else
					table.IsView = !ProcedureContainsParameters((string)proc.Rows[0]["PROCEDURE_DEFINITION"]);
			}
			return list;
		}
		protected bool ProcedureContainsParameters(string sourceString) {
			return sourceString.StartsWith("PARA", StringComparison.InvariantCultureIgnoreCase) ||
				Regex.Match(sourceString, "Forms]?!", RegexOptions.IgnoreCase).Success;
		}
		void GetColumns(DBTable table) {
			DataTable t = ReflectionHelper.GetColumns(Connection, new object[] { null, null, ComposeSafeTableName(table.Name), null });
			if(SortColumnsAlphabetically) {
				foreach(DataRow r in t.Rows) {
					table.AddColumn(GetColumnFromOleDbMetadata(r));
				}
			}
			else {
				SortedList cols = new SortedList();
				foreach(DataRow r in t.Rows) {
					cols.Add(r["ORDINAL_POSITION"], GetColumnFromOleDbMetadata(r));
				}
				foreach(DictionaryEntry de in cols) {
					table.AddColumn(de.Value as DBColumn);
				}
			}
		}
		protected DBColumn GetColumnFromOleDbMetadata(DataRow colDefinition) {
			DBColumnType type = ((IDbTypeMapperAccess)DbTypeMapper).GetDbColumnType(colDefinition["DATA_TYPE"]);
			int charMaxLength = (colDefinition["CHARACTER_MAXIMUM_LENGTH"] != System.DBNull.Value) ? (int)(long)colDefinition["CHARACTER_MAXIMUM_LENGTH"] : 0;
			bool isNullable = Convert.ToBoolean(colDefinition["IS_NULLABLE"]);
			bool hasDefaultValue = Convert.ToBoolean(colDefinition["COLUMN_HASDEFAULT"]);
			string dbDefaultValue = colDefinition["COLUMN_DEFAULT"].ToString();
			object defaultValue = null;
			if(!string.IsNullOrEmpty(dbDefaultValue)) {
				if(type != DBColumnType.String || dbDefaultValue.Length <= 255) {
					try {
						string scalarQuery = String.Concat("select ", dbDefaultValue);
						defaultValue = FixDBNullScalar(GetScalar(new Query(scalarQuery)));
					}
					catch { }
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
			DBColumn dbColumn = new DBColumn((string)colDefinition["COLUMN_NAME"], false, String.Empty, type == DBColumnType.String ? charMaxLength : 0, type, isNullable, defaultValue);
			dbColumn.DbDefaultValue = dbDefaultValue;
			return dbColumn;
		}
		void GetPrimaryKey(DBTable table) {
			DataTable data = ReflectionHelper.GetPrimaryKeys(Connection, new object[] { null, null, ComposeSafeTableName(table.Name) });
			if(data.Rows.Count > 0) {
				ArrayList cols = new ArrayList();
				foreach(DataRow row in data.Rows) {
					DBColumn column = table.GetColumn((string)row["COLUMN_NAME"]);
					column.IsKey = true;
					cols.Add(column);
				}
				table.PrimaryKey = new DBPrimaryKey(cols);
			}
			if(table.PrimaryKey != null && table.PrimaryKey.Columns.Count == 1) {
				using(IDbCommand cmd = CreateCommand()) {
					cmd.CommandText = "select [" + table.PrimaryKey.Columns[0] + "] from [" + ComposeSafeTableName(table.Name) + "]";
					using(IDataReader r = cmd.ExecuteReader(CommandBehavior.SchemaOnly | CommandBehavior.KeyInfo)) {
						DataTable t = r.GetSchemaTable();
						table.GetColumn(table.PrimaryKey.Columns[0]).IsIdentity = (bool)t.Rows[0]["IsAutoIncrement"] == true;
					}
				}
			}
		}
		void GetIndexes(DBTable table) {
			DataTable dt = ReflectionHelper.GetIndexes(Connection, new object[] { null, null, null, null, ComposeSafeTableName(table.Name) });
			DBIndex index = null;
			foreach(DataRow row in dt.Rows) {
				if(index == null || index.Name != (string)row["INDEX_NAME"]) {
					StringCollection list = new StringCollection();
					list.Add((string)row["COLUMN_NAME"]);
					index = new DBIndex((string)row["INDEX_NAME"], list, (bool)row["UNIQUE"]);
					table.Indexes.Add(index);
				}
				else
					index.Columns.Add((string)row["COLUMN_NAME"]);
			}
		}
		void GetForeignKeys(DBTable table) {
			DataTable dt = ReflectionHelper.GetForeignKeys(Connection, new object[] { null, null, null, null, null, ComposeSafeTableName(table.Name) });
			Hashtable fks = new Hashtable();
			foreach(DataRow row in dt.Rows) {
				DBForeignKey fk = (DBForeignKey)fks[row["FK_NAME"]];
				int ord = (int)(Int64)row["ORDINAL"];
				if(fk == null) {
					StringCollection pkc = new StringCollection();
					StringCollection fkc = new StringCollection();
					pkc.Add((string)row["FK_COLUMN_NAME"]);
					fkc.Add((string)row["PK_COLUMN_NAME"]);
					fk = new DBForeignKey(pkc, (string)row["PK_TABLE_NAME"], fkc);
					table.ForeignKeys.Add(fk);
					fks[row["FK_NAME"]] = fk;
				}
				else {
					fk.Columns.Add((string)row["FK_COLUMN_NAME"]);
					fk.PrimaryKeyTableKeyColumns.Add((string)row["PK_COLUMN_NAME"]);
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
		public override Task<SelectedData> SelectDataAsync(CancellationToken cancellationToken, params SelectStatement[] selects) {
			cancellationToken.ThrowIfCancellationRequested();
			return Task.FromResult(SelectData(selects));
		}
		public override Task<ModificationResult> ModifyDataAsync(CancellationToken cancellationToken, params ModificationStatement[] dmlStatements) {
			cancellationToken.ThrowIfCancellationRequested();
			return Task.FromResult(ModifyData(dmlStatements));
		}
		public override Task<UpdateSchemaResult> UpdateSchemaAsync(CancellationToken cancellationToken, bool doNotCreateIfFirstTableNotExist, params DBTable[] tables) {
			cancellationToken.ThrowIfCancellationRequested();
			return Task.FromResult(UpdateSchema(doNotCreateIfFirstTableNotExist, tables));
		}
	}
	public class Access97ProviderFactory : ProviderFactory {
		public override IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return AccessConnectionProvider.CreateProviderFromConnection(connection, autoCreateOption);
		}
		public override IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			return AccessConnectionProvider.CreateProviderFromString(connectionString, autoCreateOption, out objectsToDisposeOnDisconnect);
		}
		public override string GetConnectionString(Dictionary<string, string> parameters) {
			if(!parameters.ContainsKey(DatabaseParamID) || !parameters.ContainsKey(UserIDParamID) || !parameters.ContainsKey(PasswordParamID)) {
				return null;
			}
			return AccessConnectionProvider.GetConnectionString(parameters[DatabaseParamID], parameters[UserIDParamID], parameters[PasswordParamID]);
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
		public override string ProviderKey { get { return "Access97"; } }
		public override string[] GetDatabases(string server, string userId, string password) {
			return new string[1] { server };
		}
		public override string FileFilter { get { return "Access 97 databases|*.mdb"; } }
		public override bool MeanSchemaGeneration { get { return true; } }
	}
	public class Access2007ProviderFactory : Access97ProviderFactory {
		public override IDataStore CreateProvider(Dictionary<string, string> parameters, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			if(!parameters.ContainsKey(DatabaseParamID) || !parameters.ContainsKey(PasswordParamID)) {
				objectsToDisposeOnDisconnect = Array.Empty<IDisposable>();
				return null;
			}
			string connectionString = AccessConnectionProvider.GetConnectionStringACE(parameters[DatabaseParamID],
						 parameters[PasswordParamID]);
			ConnectionStringParser helper = new ConnectionStringParser(connectionString);
			helper.RemovePartByName(DataStoreBase.XpoProviderTypeParameterName);
			return CreateProviderFromString(helper.GetConnectionString(), autoCreateOption, out objectsToDisposeOnDisconnect);
		}
		public override string GetConnectionString(Dictionary<string, string> parameters) {
			if(!parameters.ContainsKey(DatabaseParamID) || !parameters.ContainsKey(PasswordParamID)) {
				return null;
			}
			return AccessConnectionProvider.GetConnectionStringACE(parameters[DatabaseParamID], parameters[PasswordParamID]);
		}
		public override bool HasUserName { get { return false; } }
		public override string ProviderKey { get { return "Access2007"; } }
		public override string FileFilter { get { return "Access 2007 databases|*.accdb"; } }
	}
	public class AccessConnectionProvider : OleDBConnectionProvider, ISqlGeneratorFormatterEx {
		public const string XpoProviderTypeString = "MSAccess";
		public static string GetConnectionString(string database, string userId, string password) {
			return String.Format("{3}={4};Provider=Microsoft.Jet.OLEDB.4.0;Mode=Share Deny None;data source={0};user id={1};{5}password={2};", EscapeConnectionStringArgument(database), EscapeConnectionStringArgument(userId), EscapeConnectionStringArgument(password), DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString, string.IsNullOrEmpty(password) ? string.Empty : "Jet OLEDB:Database ");
		}
		public static string GetConnectionStringACE(string database, string password) {
			return String.Format("{2}={3};Provider=Microsoft.ACE.OLEDB.12.0;Mode=Share Deny None;data source={0};Jet OLEDB:Database Password={1};", EscapeConnectionStringArgument(database), EscapeConnectionStringArgument(password), DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString);
		}
		public static IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			IDbConnection connection = CreateConnection(connectionString);
			objectsToDisposeOnDisconnect = new IDisposable[] { connection };
			return CreateProviderFromConnection(connection, autoCreateOption);
		}
		public static IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			string provider = GetOleDbProviderName(connection);
			if(provider != null && (provider.StartsWith("Microsoft.Jet.OLEDB", StringComparison.InvariantCultureIgnoreCase) || provider.StartsWith("Microsoft.ACE.OLEDB", StringComparison.InvariantCultureIgnoreCase)))
				return new AccessConnectionProvider(connection, autoCreateOption);
			else
				return null;
		}
		static AccessConnectionProvider() {
			RegisterDataStoreProvider(XpoProviderTypeString, new DataStoreCreationFromStringDelegate(CreateProviderFromString));
			RegisterDataStoreProvider("System.Data.OleDb.OleDbConnection", new DataStoreCreationFromConnectionDelegate(CreateProviderFromConnection));
			RegisterFactory(new Access97ProviderFactory());
			RegisterFactory(new Access2007ProviderFactory());
		}
		public static void Register() { }
		public AccessConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption)
			: base(connection, autoCreateOption, true) {
		}
		protected AccessConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption, bool openConnection)
			: base(connection, autoCreateOption, openConnection) {
		}
		protected override string GetSqlCreateColumnTypeForBoolean(DBTable table, DBColumn column) {
			return "bit";
		}
		protected override string GetSqlCreateColumnTypeForByte(DBTable table, DBColumn column) {
			return "byte";
		}
		protected override string GetSqlCreateColumnTypeForSByte(DBTable table, DBColumn column) {
			return "short";
		}
		protected override string GetSqlCreateColumnTypeForChar(DBTable table, DBColumn column) {
			return "char(1)";
		}
		protected override string GetSqlCreateColumnTypeForDecimal(DBTable table, DBColumn column) {
			return "currency";
		}
		protected override string GetSqlCreateColumnTypeForDouble(DBTable table, DBColumn column) {
			return "double";
		}
		protected override string GetSqlCreateColumnTypeForSingle(DBTable table, DBColumn column) {
			return "single";
		}
		protected override string GetSqlCreateColumnTypeForInt32(DBTable table, DBColumn column) {
			return "int";
		}
		protected override string GetSqlCreateColumnTypeForUInt32(DBTable table, DBColumn column) {
			return "decimal(10,0)";
		}
		protected override string GetSqlCreateColumnTypeForInt16(DBTable table, DBColumn column) {
			return "short";
		}
		protected override string GetSqlCreateColumnTypeForUInt16(DBTable table, DBColumn column) {
			return "int";
		}
		protected override string GetSqlCreateColumnTypeForInt64(DBTable table, DBColumn column) {
			return "decimal(20,0)";
		}
		protected override string GetSqlCreateColumnTypeForUInt64(DBTable table, DBColumn column) {
			return "decimal(20,0)";
		}
		public const int MaximumStringSize = 255;
		protected override string GetSqlCreateColumnTypeForString(DBTable table, DBColumn column) {
			if(column.Size > 0 && column.Size <= MaximumStringSize)
				return "varchar(" + column.Size.ToString(CultureInfo.InvariantCulture) + ')';
			else
				return "LONGTEXT";
		}
		protected override string GetSqlCreateColumnTypeForDateTime(DBTable table, DBColumn column) {
			return "datetime";
		}
		protected override string GetSqlCreateColumnTypeForGuid(DBTable table, DBColumn column) {
			return "guid";
		}
		protected override string GetSqlCreateColumnTypeForByteArray(DBTable table, DBColumn column) {
			return "longbinary";
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
						string formattedDefaultValue = AccessFormatterHelper.FormatConstant(column.DefaultValue);
						if(column.ColumnType == DBColumnType.DateTime) {
							formattedDefaultValue = formattedDefaultValue.Replace("#", "'");
						}
						result += string.Concat(" DEFAULT ", formattedDefaultValue);
					}
				}
			}
			if(column.IsKey && column.IsIdentity && (column.ColumnType == DBColumnType.Int32 || column.ColumnType == DBColumnType.Int64) && IsSingleColumnPKColumn(table, column)) {
				if(column.ColumnType == DBColumnType.Int64) throw new NotSupportedException(Res.GetString(Res.ConnectionProvider_TheAutoIncrementedKeyWithX0TypeIsNotSupport, column.ColumnType, this.GetType()));
				result += " IDENTITY";
			}
			return result;
		}
		protected override object ConvertToDbParameter(object clientValue, TypeCode clientValueTypeCode) {
			if(GetQueryParameterMode() == QueryParameterMode.Legacy) {
				switch(clientValueTypeCode) {
					case TypeCode.DateTime:
						return ((DateTime)clientValue).ToOADate();
					case TypeCode.Decimal:
						return (Double)(Decimal)clientValue;
				}
			}
			return base.ConvertToDbParameter(clientValue, clientValueTypeCode);
		}
		protected override Int64 GetIdentity(Query sql) {
			ExecSql(sql);
			return (Int32)GetScalar(new Query("select @@Identity"));
		}
		protected override async Task<Int64> GetIdentityAsync(Query sql, AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken) {
			await ExecSqlAsync(sql, asyncOperationId, cancellationToken).ConfigureAwait(false);
			return (Int32)(await GetScalarAsync(new Query("select @@Identity"), asyncOperationId, cancellationToken).ConfigureAwait(false));
		}
		protected override Exception WrapException(Exception e, IDbCommand query) {
			const int noValueGivenColumnAbsentInSelect = -2147217904;
			const int unknownFieldName = -2147217900;
			const int couldNotFindOutputTable = -2147217865;
			const int eFail = -2147467259;
			object errorCode;
			if(ReflectionHelper.IsOleDbException(e) && ConnectionHelper.TryGetExceptionProperty(e, "ErrorCode", out errorCode)) {
				switch((int)errorCode) {
					case noValueGivenColumnAbsentInSelect:
					case unknownFieldName:
					case couldNotFindOutputTable:
						return new SchemaCorrectionNeededException(e);
					case eFail:
						int nativeError = ReflectionHelper.GetOleDbExceptionNativeError(e);
						if(nativeError == -534971980 || nativeError == -105121349)
							return new ConstraintViolationException(query.CommandText, GetParametersString(query), e);
						break;
				}
			}
			return base.WrapException(e, query);
		}
		bool ExtractMajorPartOfNativeErrorCode(Exception e, out int errorCode) {
			int n = ReflectionHelper.GetOleDbExceptionNativeError(e);
			if(n == 0) {
				errorCode = 0;
				return false;
			}
			errorCode = (System.Math.Abs(n) & 0xFFFF) * (n < 0 ? -1 : 1);
			return true;
		}
		protected override IDbConnection CreateConnection() {
			return CreateConnection(ConnectionString);
		}
		[System.Security.SecuritySafeCritical]
#if NET
		[SupportedOSPlatform("windows")]
#endif
		protected override void CreateDataBase() {
			if(Connection.State == ConnectionState.Open)
				return;
			const int FileNotFoundErrorCode = -1811;
			try {
				Connection.Open();
			}
			catch(Exception e) {
				int errorCode;
				if(ReflectionHelper.IsOleDbException(e)
					&& (ExtractMajorPartOfNativeErrorCode(e, out errorCode) && errorCode == FileNotFoundErrorCode)
					&& CanCreateDatabase) {
					Type objClassType = Type.GetTypeFromProgID("ADOX.Catalog");
					object cat = Activator.CreateInstance(objClassType);
					cat.GetType().InvokeMember("Create", BindingFlags.InvokeMethod, null, cat, new object[] { ConnectionString });
					object con = cat.GetType().InvokeMember("ActiveConnection", BindingFlags.GetProperty, null, cat, null);
					if(con != null)
						con.GetType().InvokeMember("Close", BindingFlags.InvokeMethod, null, con, null);
				}
				else
					throw new UnableToOpenDatabaseException(XpoDefault.ConnectionStringRemovePassword(ConnectionString), e);
			}
		}
		protected override int GetSafeNameTableMaxLength() {
			return 64;
		}
		protected override string GetSafeNameRoot(string originalName) {
			return GetSafeNameAccess(originalName);
		}
		public override void CreateForeignKey(DBTable table, DBForeignKey foreignKey) {
			if(foreignKey.PrimaryKeyTable == "XPObjectType")
				return;
			base.CreateForeignKey(table, foreignKey);
		}
		public override string FormatTable(string schema, string tableName) {
			return AccessFormatterHelper.FormatTable(schema, tableName);
		}
		public override string FormatTable(string schema, string tableName, string tableAlias) {
			return AccessFormatterHelper.FormatTable(schema, tableName, tableAlias);
		}
		public override string FormatColumn(string columnName) {
			return AccessFormatterHelper.FormatColumn(columnName);
		}
		public override string FormatColumn(string columnName, string tableAlias) {
			return AccessFormatterHelper.FormatColumn(columnName, tableAlias);
		}
		public override string FormatSelect(string selectedPropertiesSql, string fromSql, string whereSql, string orderBySql, string groupBySql, string havingSql, int topSelectedRecords) {
			string modificatorsSql = string.Format(CultureInfo.InvariantCulture, (topSelectedRecords != 0) ? "top {0} " : string.Empty, topSelectedRecords);
			string expandedWhereSql = whereSql == null ? null : string.Format(CultureInfo.InvariantCulture, "\nwhere {0}", whereSql);
			string expandedGroupBySql = groupBySql != null ? string.Format(CultureInfo.InvariantCulture, "{0}group by {1}", Environment.NewLine, groupBySql) : string.Empty;
			string expandedHavingSql = havingSql != null ? string.Format(CultureInfo.InvariantCulture, "{0}having {1}", Environment.NewLine, havingSql) : string.Empty;
			string expandedOrderBySql = orderBySql != null ? string.Format(CultureInfo.InvariantCulture, "{0}order by {1}", Environment.NewLine, orderBySql) : string.Empty;
			return string.Format(CultureInfo.InvariantCulture, "select {0}{1} from {2}{3}{4}{5}{6}",
				modificatorsSql, selectedPropertiesSql, fromSql, expandedWhereSql, expandedGroupBySql, expandedHavingSql, expandedOrderBySql);
		}
		public override string FormatInsertDefaultValues(string tableName) {
			return AccessFormatterHelper.FormatInsertDefaultValues(tableName);
		}
		public override string FormatInsert(string tableName, string fields, string values) {
			return AccessFormatterHelper.FormatInsert(tableName, fields, values);
		}
		public override string FormatUpdate(string tableName, string sets, string whereClause) {
			return AccessFormatterHelper.FormatUpdate(tableName, sets, whereClause);
		}
		public override string FormatDelete(string tableName, string whereClause) {
			return AccessFormatterHelper.FormatDelete(tableName, whereClause);
		}
		public override string FormatBinary(BinaryOperatorType operatorType, string leftOperand, string rightOperand) {
			return AccessFormatterHelper.FormatBinary(operatorType, leftOperand, rightOperand);
		}
		public override string FormatFunction(FunctionOperatorType operatorType, params string[] operands) {
			string format = AccessFormatterHelper.FormatFunction(operatorType, operands);
			return format ?? base.FormatFunction(operatorType, operands);
		}
		public override string FormatFunction(ProcessParameter processParameter, FunctionOperatorType operatorType, params object[] operands) {
			string format = AccessFormatterHelper.FormatFunction(processParameter, operatorType, operands);
			return format ?? base.FormatFunction(processParameter, operatorType, operands);
		}
		string GetParameterType(OperandValue parameter) {
			if(GetQueryParameterMode() != QueryParameterMode.Legacy) {
				ParameterValue paramValue = parameter as ParameterValue;
				if(!Equals(null, paramValue)) {
					if(!string.IsNullOrEmpty(paramValue.DBTypeName)) {
						return paramValue.DBTypeName;
					}
					if(paramValue.DBType != DBColumnType.Unknown) {
						return GetSqlCreateColumnType(null, new DBColumn { ColumnType = paramValue.DBType });
					}
				}
			}
			object value = parameter.Value;
			DBColumn c = new DBColumn();
			c.ColumnType = DBColumn.GetColumnType(value.GetType());
			return GetSqlCreateColumnType(null, c);
		}
		protected override IDbCommand CreateCommand(Query query) {
			StringBuilder res = new StringBuilder();
			for(int i = 0; i < query.Parameters.Count; i++) {
				if(res.Length > 0)
					res.Append(',');
				res.Append(query.ParametersNames[i]);
				res.Append(' ');
				res.Append(GetParameterType(query.Parameters[i]));
			}
			if(res.Length > 0)
				query = new Query(string.Concat("parameters ", res, ";", query.Sql), query.Parameters, query.ParametersNames, query.SkipSelectedRecords, query.TopSelectedRecords);
			return base.CreateCommand(query);
		}
		static string[] parameterNameCache = Array.Empty<string>();
		public override string GetParameterName(OperandValue parameter, int index, ref bool createParameter) {
			int len = parameterNameCache.Length;
			if(len <= index) {
				string[] newCache = new string[len + 10];
				Array.Copy(parameterNameCache, newCache, len);
				for(int i = len; i < newCache.Length; i++)
					newCache[i] = string.Concat("[@p", i.ToString(CultureInfo.InvariantCulture), "]");
				parameterNameCache = newCache;
			}
			return parameterNameCache[index];
		}
		public override string FormatConstraint(string constraintName) {
			return AccessFormatterHelper.FormatConstraint(constraintName);
		}
		public static string GetConnectionString(string database) {
			return GetConnectionString(database, "Admin", String.Empty);
		}
		protected override void ProcessClearDatabase() {
			using(IDbCommand command = CreateCommand()) {
				DataTable fks = ReflectionHelper.GetForeignKeys(Connection, new object[] { null, null, null, null, null, null });
				foreach(DataRow row in fks.Rows) {
					if(Convert.ToInt32(row["ORDINAL"]) == 1) {
						command.CommandText = string.Concat("alter table [", row["FK_TABLE_NAME"], "] drop constraint [", row["FK_NAME"], "]");
						command.ExecuteNonQuery();
					}
				}
				DataTable tables = ReflectionHelper.GetTables(Connection, new object[] { null, null,  null, "TABLE" });
				foreach(DataRow row in tables.Rows) {
					command.CommandText = string.Concat("drop table [", row["TABLE_NAME"], "]");
					command.ExecuteNonQuery();
				}
			}
#if DEBUGTEST
			var p = new ConnectionStringParser(Connection.ConnectionString);
			Connection.Close();
			string fileName = p.GetPartByName("data source");
			try {
				System.IO.File.Delete(fileName);
#pragma warning disable CA1416
				CreateDataBase();
#pragma warning restore CA1416
			}
			catch {
			}
			finally {
				OpenConnection();
			}
#endif
		}
		public override string[] GetStorageTablesList(bool includeViews) {
			List<string> result = new List<string>();
			DataTable tables = ReflectionHelper.GetTables(Connection, new object[] { null, null, null, "TABLE" });
			foreach(DataRow row in tables.Rows) {
				result.Add((string)row["TABLE_NAME"]);
			}
			if(includeViews) {
				DataTable views = ReflectionHelper.GetTables(Connection, new object[] { null, null, null, "VIEW" });
				DataTable procedures = ReflectionHelper.GetProcedures(Connection, Array.Empty<object>());
				foreach(DataRow row in views.Rows) {
					result.Add((string)row["TABLE_NAME"]);
				}
				foreach(DataRow row in procedures.Rows) {
					if(!ProcedureContainsParameters((string)row["PROCEDURE_DEFINITION"])) result.Add((string)row["PROCEDURE_NAME"]);
				}
			}
			return result.ToArray();
		}
		protected override bool NeedsIndexForForeignKey { get { return false; } }
		protected override SelectedData ExecuteSproc(string sprocName, params OperandValue[] parameters) {
			using(IDbCommand command = CreateCommand()) {
				PrepareCommandForSprocCall(command, sprocName, parameters);
				List<SelectStatementResult> selectStatmentResults = GetSelectedStatementResults(command);
				return new SelectedData(selectStatmentResults.ToArray());
			}
		}
		protected override async Task<SelectedData> ExecuteSprocAsync(AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken, string sprocName, params OperandValue[] parameters) {
			using(IDbCommand command = CreateCommand()) {
				PrepareCommandForSprocCall(command, sprocName, parameters);
				List<SelectStatementResult> selectStatmentResults = await GetSelectedStatementResultsAsync(command, asyncOperationId, cancellationToken).ConfigureAwait(false);
				return new SelectedData(selectStatmentResults.ToArray());
			}
		}
		void PrepareCommandForSprocCall(IDbCommand command, string sprocName, OperandValue[] parameters) {
			command.CommandType = CommandType.StoredProcedure;
			command.CommandText = sprocName;
			int counter = 0;
			foreach(OperandValue p in parameters) {
				bool createParam = true;
				string paramName = GetParameterName(p, counter++, ref createParam);
				if(createParam) {
					ParameterValue param = p as ParameterValue;
					if(!ReferenceEquals(param, null)) {
						command.Parameters.Add(CreateParameter(command, p.Value, paramName, param.DBType, param.DBTypeName, param.Size));
					}
					else {
						command.Parameters.Add(CreateParameter(command, p.Value, paramName, DBColumnType.Unknown, null, 0));
					}
				}
			}
		}
		public override DBStoredProcedure[] GetStoredProcedures() {
			throw new NotSupportedException();
		}
		protected override void CommandBuilderDeriveParameters(IDbCommand command) {
			throw new NotSupportedException();
		}
		DbTypeMapperBase dbTypeMapper;
		protected override DbTypeMapperBase DbTypeMapper {
			get {
				if(dbTypeMapper == null) {
					Type oleDbParameterType = ConnectionHelper.GetType("System.Data.OleDb.OleDbParameter");
					Type oleDbTypeType = ConnectionHelper.GetType("System.Data.OleDb.OleDbType");
					dbTypeMapper = (DbTypeMapperBase)Activator.CreateInstance(typeof(DbTypeMapperAccess<,>).MakeGenericType(oleDbTypeType, oleDbParameterType));
				}
				return dbTypeMapper;
			}
		}
	}
	public class AccessConnectionProviderMultiUserThreadSafe : MarshalByRefObject, IDataStore, IDataStoreAsync, IDataStoreSchemaExplorer, IDataStoreForTests, ICommandChannel, ICommandChannelAsync {
		public const string XpoProviderTypeString = "MSAccessSafe";
		public static string GetConnectionString(string database, string userId, string password) {
			return String.Format("{3}={4};Provider=Microsoft.Jet.OLEDB.4.0;Mode=Share Deny None;data source={0};user id={1};{5}password={2};", database, userId, password, DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString, string.IsNullOrEmpty(password) ? string.Empty : "Jet OLEDB:Database ");
		}
		public static string GetConnectionString(string database) {
			return GetConnectionString(database, "Admin", String.Empty);
		}
		public static IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			objectsToDisposeOnDisconnect = Array.Empty<IDisposable>();
			return new AccessConnectionProviderMultiUserThreadSafe(connectionString, autoCreateOption);
		}
		static AccessConnectionProviderMultiUserThreadSafe() {
			DataStoreBase.RegisterDataStoreProvider(XpoProviderTypeString, new DataStoreCreationFromStringDelegate(CreateProviderFromString));
		}
		public static void Register() { }
		public readonly string ConnectionString;
		readonly AutoCreateOption _AutoCreateOption;
		protected IDbConnection GetConnection() {
			return OleDBConnectionProvider.CreateConnection(ConnectionString);
		}
		public AccessConnectionProviderMultiUserThreadSafe(string connectionString, AutoCreateOption autoCreateOption) {
			this.ConnectionString = connectionString;
			this._AutoCreateOption = autoCreateOption;
		}
		[Description("Returns which operations are performed when a session is connected to a data store.")]
		[Browsable(false)]
		public AutoCreateOption AutoCreateOption {
			get { return this._AutoCreateOption; }
		}
		public SelectedData SelectData(params SelectStatement[] selects) {
			using(IDbConnection connection = GetConnection()) {
				return AccessConnectionProvider.CreateProviderFromConnection(connection, AutoCreateOption).SelectData(selects);
			}
		}
		public ModificationResult ModifyData(params ModificationStatement[] dmlStatements) {
			using(IDbConnection connection = GetConnection()) {
				return AccessConnectionProvider.CreateProviderFromConnection(connection, AutoCreateOption).ModifyData(dmlStatements);
			}
		}
		public async Task<SelectedData> SelectDataAsync(CancellationToken cancellationToken, params SelectStatement[] selects) {
			using(IDbConnection connection = GetConnection()) {
				return await ((IDataStoreAsync)AccessConnectionProvider.CreateProviderFromConnection(connection, AutoCreateOption)).SelectDataAsync(cancellationToken, selects).ConfigureAwait(false);
			}
		}
		public async Task<ModificationResult> ModifyDataAsync(CancellationToken cancellationToken, params ModificationStatement[] dmlStatements) {
			using(IDbConnection connection = GetConnection()) {
				return await ((IDataStoreAsync)AccessConnectionProvider.CreateProviderFromConnection(connection, AutoCreateOption)).ModifyDataAsync(cancellationToken, dmlStatements).ConfigureAwait(false);
			}
		}
		public UpdateSchemaResult UpdateSchema(bool doNotCreateIfFirstTableNotExist, params DBTable[] tables) {
			using(IDbConnection connection = GetConnection()) {
				return AccessConnectionProvider.CreateProviderFromConnection(connection, AutoCreateOption).UpdateSchema(doNotCreateIfFirstTableNotExist, tables);
			}
		}
		public Task<UpdateSchemaResult> UpdateSchemaAsync(CancellationToken cancellationToken, bool doNotCreateIfFirstTableNotExist, params DBTable[] tables) {
			return Task.FromResult(UpdateSchema(doNotCreateIfFirstTableNotExist, tables));
		}
		public DBTable[] GetStorageTables(params string[] tables) {
			using(IDbConnection connection = GetConnection()) {
				return ((IDataStoreSchemaExplorer)AccessConnectionProvider.CreateProviderFromConnection(connection, AutoCreateOption)).GetStorageTables(tables);
			}
		}
		public string[] GetStorageTablesList(bool includeViews) {
			using(IDbConnection connection = GetConnection()) {
				return ((IDataStoreSchemaExplorer)AccessConnectionProvider.CreateProviderFromConnection(connection, AutoCreateOption)).GetStorageTablesList(includeViews);
			}
		}
		void IDataStoreForTests.ClearDatabase() {
			using(IDbConnection connection = GetConnection()) {
				((IDataStoreForTests)AccessConnectionProvider.CreateProviderFromConnection(connection, AutoCreateOption)).ClearDatabase();
			}
		}
		void ThrowIfCommandNotSupported(string command, IDataStore provider) {
			ICommandChannel signalReceptor = provider as ICommandChannel;
			switch(command) {
				case CommandChannelHelper.Command_ExecuteNonQuerySQL:
				case CommandChannelHelper.Command_ExecuteQuerySQL:
				case CommandChannelHelper.Command_ExecuteScalarSQL:
				case CommandChannelHelper.Command_ExecuteStoredProcedure:
					break;
				default:
					throw new NotSupportedException(string.Format(CommandChannelHelper.Message_CommandIsNotSupportedEx, command, typeof(AccessConnectionProviderMultiUserThreadSafe).FullName));
			}
			if(signalReceptor == null) {
				if(provider == null) {
					throw new NotSupportedException(string.Format(CommandChannelHelper.Message_CommandIsNotSupported, command));
				}
				else {
					throw new NotSupportedException(string.Format(CommandChannelHelper.Message_CommandIsNotSupportedEx, command, provider.GetType().FullName));
				}
			}
		}
		object ICommandChannel.Do(string command, object args) {
			using(IDbConnection connection = GetConnection()) {
				IDataStore provider = AccessConnectionProvider.CreateProviderFromConnection(connection, AutoCreateOption);
				var signalReceptor = provider as ICommandChannel;
				ThrowIfCommandNotSupported(command, provider);
				return signalReceptor.Do(command, args);
			}
		}
		async Task<object> ICommandChannelAsync.DoAsync(string command, object args, CancellationToken cancellationToken) {
			using(IDbConnection connection = GetConnection()) {
				IDataStore provider = AccessConnectionProvider.CreateProviderFromConnection(connection, AutoCreateOption);
				var signalReceptor = provider as ICommandChannelAsync;
				ThrowIfCommandNotSupported(command, provider);
				return await signalReceptor.DoAsync(command, args, cancellationToken).ConfigureAwait(false);
			}
		}
	}
}
#pragma warning restore DX0024
namespace DevExpress.Xpo.DB.Helpers {
	using System.Collections;
	using System.Data;
	using System.Linq;
	public abstract class OleDbReflectionHelperBase {
		protected static Guid OleDbSchemaGuidTables;
		protected static Guid OleDbSchemaGuidProcedures;
		protected static Guid OleDbSchemaGuidColumns;
		protected static Guid OleDbSchemaGuidPrimaryKeys;
		protected static Guid OleDbSchemaGuidIndexes;
		protected static Guid OleDbSchemaGuidForeignKeys;
		public DataTable GetTables(IDbConnection connection, object[] restrictions) {
			return GetOleDbSchemaTable(connection, OleDbSchemaGuidTables, restrictions);
		}
		public DataTable GetProcedures(IDbConnection connection, object[] restrictions) {
			return GetOleDbSchemaTable(connection, OleDbSchemaGuidProcedures, restrictions);
		}
		public DataTable GetColumns(IDbConnection connection, object[] restrictions) {
			return GetOleDbSchemaTable(connection, OleDbSchemaGuidColumns, restrictions);
		}
		public DataTable GetPrimaryKeys(IDbConnection connection, object[] restrictions) {
			return GetOleDbSchemaTable(connection, OleDbSchemaGuidPrimaryKeys, restrictions);
		}
		public DataTable GetIndexes(IDbConnection connection, object[] restrictions) {
			return GetOleDbSchemaTable(connection, OleDbSchemaGuidIndexes, restrictions);
		}
		public DataTable GetForeignKeys(IDbConnection connection, object[] restrictions) {
			return GetOleDbSchemaTable(connection, OleDbSchemaGuidForeignKeys, restrictions);
		}
		protected abstract DataTable GetOleDbSchemaTable(IDbConnection connection, Guid schema, object[] restrictions);
		public abstract int GetOleDbExceptionNativeError(Exception ex);
		public abstract bool IsOleDbException(Exception ex);
	}
	class OleDbReflectionHelper<TOleDbConnection, TOleDbSchemaGuid, TOleDbException, TOleDbError> : OleDbReflectionHelperBase
		where TOleDbException : Exception {
		static readonly Func<TOleDbConnection, Guid, object[], DataTable> getOldDbSchemaTableHandler;
		static readonly GetPropertyValueDelegate getSqlErrorsHandler;
		static readonly GetPropertyValueDelegate getSqlErrorNativeErrorHandler;
#if DEBUGTEST
		[Data.Tests.IgnoreReflectionUsageDetector]
#endif
		static OleDbReflectionHelper() {
			OleDbSchemaGuidTables = (Guid)typeof(TOleDbSchemaGuid).GetField("Tables").GetValue(null);
			OleDbSchemaGuidProcedures = (Guid)typeof(TOleDbSchemaGuid).GetField("Procedures").GetValue(null);
			OleDbSchemaGuidColumns = (Guid)typeof(TOleDbSchemaGuid).GetField("Columns").GetValue(null);
			OleDbSchemaGuidPrimaryKeys = (Guid)typeof(TOleDbSchemaGuid).GetField("Primary_Keys").GetValue(null);
			OleDbSchemaGuidIndexes = (Guid)typeof(TOleDbSchemaGuid).GetField("Indexes").GetValue(null);
			OleDbSchemaGuidForeignKeys = (Guid)typeof(TOleDbSchemaGuid).GetField("Foreign_Keys").GetValue(null);
			getOldDbSchemaTableHandler = (Func<TOleDbConnection, Guid, object[], DataTable>)typeof(TOleDbConnection).GetMethod("GetOleDbSchemaTable", new Type[] { typeof(Guid), typeof(object[]) }).CreateDelegate(typeof(Func<TOleDbConnection, Guid, object[], DataTable>));
			getSqlErrorsHandler = ReflectConnectionHelper.CreateGetPropertyDelegate(typeof(TOleDbException), "Errors");
			getSqlErrorNativeErrorHandler = ReflectConnectionHelper.CreateGetPropertyDelegate(typeof(TOleDbError), "NativeError");
		}
		protected override DataTable GetOleDbSchemaTable(IDbConnection connection, Guid schema, object[] restrictions) {
			return getOldDbSchemaTableHandler((TOleDbConnection)connection, schema, restrictions);
		}
		public override int GetOleDbExceptionNativeError(Exception ex) {
			TOleDbException sqlException = ex as TOleDbException;
			if(sqlException != null) {
				var errorsCollection = (ICollection)getSqlErrorsHandler(sqlException);
				if(errorsCollection.Count > 0) {
					TOleDbError error = errorsCollection.Cast<TOleDbError>().First();
					return (int)getSqlErrorNativeErrorHandler(error);
				}
			}
			return 0;
		}
		public override bool IsOleDbException(Exception ex) {
			return ex is TOleDbException;
		}
	}
	interface IDbTypeMapperAccess {
		DBColumnType GetDbColumnType(object oleDbType);
	}
	class DbTypeMapperAccess<TOleDbType, TOleDbParameter> : DbTypeMapper<TOleDbType, TOleDbParameter>, IDbTypeMapperAccess
		where TOleDbType : struct
		where TOleDbParameter : IDbDataParameter {
		static readonly TOleDbType oledDbTypeLongVarChar;
		protected override string ParameterDbTypePropertyName { get { return "OleDbType"; } }
		static DbTypeMapperAccess() {
			oledDbTypeLongVarChar = (TOleDbType)Enum.Parse(typeof(TOleDbType), "LongVarChar");
		}
		protected override string GetParameterTypeNameForBoolean(out int? size, out byte? precision, out byte? scale) {
			size = null;
			precision = scale = null;
			return "Boolean";
		}
		protected override string GetParameterTypeNameForByte(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "UnsignedTinyInt";
		}
		protected override string GetParameterTypeNameForByteArray(out int? size) {
			size = null;
			return "VarBinary";
		}
		protected override string GetParameterTypeNameForChar(out int? size) {
			size = 1;
			return "Char";
		}
		protected override string GetParameterTypeNameForDateTime() {
			return "Date";
		}
		protected override string GetParameterTypeNameForDecimal(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Currency";
		}
		protected override string GetParameterTypeNameForDouble(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Double";
		}
		protected override string GetParameterTypeNameForGuid(out int? size) {
			size = null;
			return "Guid";
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
			return "Single";
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
			return "Integer";
		}
		protected override string GetParameterTypeNameForUInt32(out byte? precision, out byte? scale) {
			precision = 10;
			scale = 0;
			return "Decimal";
		}
		protected override string GetParameterTypeNameForUInt64(out byte? precision, out byte? scale) {
			precision = 20;
			scale = 0;
			return "Decimal";
		}
		protected override string GetParameterTypeNameForDateOnly(out int? size) {
			size = null;
			return "Date";
		}
		protected override string GetParameterTypeNameForTimeOnly(out int? size) {
			size = 50;
			return "VarChar";
		}
		protected override string ConvertSqlTypeToParameterType(string sqlType) {
			sqlType = sqlType.ToUpperInvariant();
			switch(sqlType) {
				case "INT":
					return "Integer";
				case "DECIMAL":
					return "Decimal";
				case "CHAR":
					return "Char";
				case "LONGBINARY":
					return "LongVarBinary";
				case "BINARY":
					return "Binary";
				case "BIT":
					return "Boolean";
				case "COUNTER":
					return "Integer";
				case "CURRENCY":
					return "Currency";
				case "DATETIME":
				case "DATE":
					return "Date";
				case "GUID":
					return "Guid";
				case "LONGTEXT":
				case "LONGVARCHAR":
					return "LongVarChar";
				case "SINGLE":
					return "Single";
				case "DOUBLE":
					return "Double";
				case "BYTE":
					return "TinyInt";
				case "UNSIGNED BYTE":
					return "UnsignedTinyInt";
				case "SHORT":
					return "SmallInt";
				case "LONG":
					return "BigInt";
				case "NUMERIC":
					return "Numeric";
				case "VARCHAR":
					return "VarChar";
				case "VARBINARY":
					return "VarBinary";
				case "NCHAR":
					return "WChar";
				case "NVARCHAR":
					return "VarWChar";
				default:
					return null;
			}
		}
		public DBColumnType GetDbColumnType(object oleDbType) {
			string name = Enum.GetName(typeof(TOleDbType), oleDbType);
			switch(name) {
				case "Integer":
					return DBColumnType.Int32;
				case "VarBinary":
				case "Binary":
					return DBColumnType.ByteArray;
				case "VarWChar":
				case "LongVarWChar":
				case "WChar":
				case "Char":
					return DBColumnType.String;
				case "Boolean":
					return DBColumnType.Boolean;
				case "SmallInt":
					return DBColumnType.Int16;
				case "UnsignedTinyInt":
					return DBColumnType.Byte;
				case "Decimal":
				case "Currency":
					return DBColumnType.Decimal;
				case "Single":
					return DBColumnType.Single;
				case "Double":
					return DBColumnType.Double;
				case "Date":
					return DBColumnType.DateTime;
				case "Guid":
					return DBColumnType.Guid;
				case "Numeric":
					return DBColumnType.Decimal;
			}
			return DBColumnType.Unknown;
		}
		public override void SetParameterTypeAndSize(IDbDataParameter parameter, DBColumnType dbColumnType, int size) {
			if(dbColumnType == DBColumnType.String) {
				if(size <= 0 || size > AccessConnectionProvider.MaximumStringSize) {
					SetSqlDbTypeHandler((TOleDbParameter)parameter, oledDbTypeLongVarChar);
					return;
				}
			}
			base.SetParameterTypeAndSize(parameter, dbColumnType, size);
		}
	}
}
