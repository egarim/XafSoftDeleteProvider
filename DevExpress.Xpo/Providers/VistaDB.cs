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
	using System.Collections.Concurrent;
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
	public abstract class VistaDBConnectionProviderBase : ConnectionProviderSql, IDisposable {
		const string VistaDBExclusiveReadWriteString = "ExclusiveReadWrite";
		const string VistaDBNonexclusiveReadWriteString = "NonexclusiveReadWrite";
		const string VistaDBSingleProcessReadWriteString = "SingleProcessReadWrite";
		const string VistaDBSingleProcessReadOnlyString = "SingleProcessReadOnly";
		const string VistaDBMultiProcessReadWriteString = "MultiProcessReadWrite";
		const string VistaDBMultiProcessReadOnlyString = "MultiProcessReadOnly";
		protected const string VistaDB4AssemblyName = "VistaDB.4";
		protected const string VistaDB5AssemblyName = "VistaDB.5.NET40";
		protected const string VistaDB6AssemblyName = "VistaDB.6";
		internal static IDataStore CreateProviderFromConnectionSelector(IDbConnection connection, AutoCreateOption autoCreateOption) {
			if(connection != null) {
				var vistaDBAssemblyName = connection.GetType().Assembly.FullName;
				if(vistaDBAssemblyName.StartsWith(VistaDB5AssemblyName)) {
					return new VistaDB5ConnectionProvider(connection, autoCreateOption);
				}
				if(vistaDBAssemblyName.StartsWith(VistaDB6AssemblyName)) {
					return new VistaDB6ConnectionProvider(connection, autoCreateOption);
				}
			}
			return new VistaDBConnectionProvider(connection, autoCreateOption);
		}
		ReflectConnectionHelper helper;
		ReflectConnectionHelper ConnectionHelper {
			get {
				if(helper == null)
					helper = new ReflectConnectionHelper(Connection, "VistaDB.Diagnostic.VistaDBException");
				return helper;
			}
		}
		DbTypeMapperBase dbTypeMapper;
		protected override DbTypeMapperBase DbTypeMapper {
			get {
				if(dbTypeMapper == null) {
					Type saParamType = ConnectionHelper.GetType("VistaDB.Provider.VistaDBParameter");
					Type saDbTypeType = ConnectionHelper.GetType("VistaDB.VistaDBType");
					dbTypeMapper = CreateDbTypeMapper(saDbTypeType, saParamType);
				}
				return dbTypeMapper;
			}
		}
		protected abstract DbTypeMapperBase CreateDbTypeMapper(Type vistaDbTypeEnumType, Type vistaDbParameterType);
		static readonly ConcurrentDictionary<Type, XPVistaDBDA> engineDictionary = new ConcurrentDictionary<Type, XPVistaDBDA>();
		static XPVistaDBDA GetEngine(Type connectionType) {
			return engineDictionary.GetOrAdd(connectionType, (ct) => XPVistaDBDA.GetVistaDBDA(ct));
		}
		protected VistaDBConnectionProviderBase(IDbConnection connection, AutoCreateOption autoCreateOption, bool openConnection)
			: base(connection, autoCreateOption, openConnection) {
		}
		protected override string GetSqlCreateColumnTypeForBoolean(DBTable table, DBColumn column) {
			throw new NotSupportedException();
		}
		protected override string GetSqlCreateColumnTypeForByte(DBTable table, DBColumn column) {
			throw new NotSupportedException();
		}
		protected override string GetSqlCreateColumnTypeForSByte(DBTable table, DBColumn column) {
			throw new NotSupportedException();
		}
		protected override string GetSqlCreateColumnTypeForChar(DBTable table, DBColumn column) {
			throw new NotSupportedException();
		}
		protected override string GetSqlCreateColumnTypeForDecimal(DBTable table, DBColumn column) {
			throw new NotSupportedException();
		}
		protected override string GetSqlCreateColumnTypeForDouble(DBTable table, DBColumn column) {
			throw new NotSupportedException();
		}
		protected override string GetSqlCreateColumnTypeForSingle(DBTable table, DBColumn column) {
			throw new NotSupportedException();
		}
		protected override string GetSqlCreateColumnTypeForInt32(DBTable table, DBColumn column) {
			throw new NotSupportedException();
		}
		protected override string GetSqlCreateColumnTypeForUInt32(DBTable table, DBColumn column) {
			throw new NotSupportedException();
		}
		protected override string GetSqlCreateColumnTypeForInt16(DBTable table, DBColumn column) {
			throw new NotSupportedException();
		}
		protected override string GetSqlCreateColumnTypeForUInt16(DBTable table, DBColumn column) {
			throw new NotSupportedException();
		}
		protected override string GetSqlCreateColumnTypeForInt64(DBTable table, DBColumn column) {
			throw new NotSupportedException();
		}
		protected override string GetSqlCreateColumnTypeForUInt64(DBTable table, DBColumn column) {
			throw new NotSupportedException();
		}
		protected override string GetSqlCreateColumnTypeForString(DBTable table, DBColumn column) {
			throw new NotSupportedException();
		}
		protected override string GetSqlCreateColumnTypeForDateTime(DBTable table, DBColumn column) {
			throw new NotSupportedException();
		}
		protected override string GetSqlCreateColumnTypeForGuid(DBTable table, DBColumn column) {
			throw new NotSupportedException();
		}
		protected override string GetSqlCreateColumnTypeForByteArray(DBTable table, DBColumn column) {
			throw new NotSupportedException();
		}
		public override string GetSqlCreateColumnFullAttributes(DBTable table, DBColumn column) {
			throw new NotSupportedException();
		}
		protected override object ConvertToDbParameter(object clientValue, TypeCode clientValueTypeCode) {
			switch(clientValueTypeCode) {
				case TypeCode.Byte:
					return (int)(Byte)clientValue;
				case TypeCode.SByte:
					return (int)(SByte)clientValue;
				case TypeCode.Int16:
					return (Int32)(Int16)clientValue;
				case TypeCode.UInt16:
					return (Int32)(UInt16)clientValue;
				case TypeCode.UInt32:
					return (Int64)(UInt32)clientValue;
				case TypeCode.UInt64:
					return (long)(UInt64)clientValue;
				case TypeCode.Single:
					return (Double)(Single)clientValue;
			}
			return base.ConvertToDbParameter(clientValue, clientValueTypeCode);
		}
		public override bool BraceJoin { get { return false; } }
		protected override Int64 GetIdentity(InsertStatement root, TaggedParametersHolder identitiesByTag) {
			Query sql = new InsertSqlGenerator(this, identitiesByTag, new Dictionary<OperandValue, string>()).GenerateSql(root);
			ExecSql(sql);
			object value = GetScalar(new Query(string.Format("SELECT LastIdentity([{0}]) FROM [{1}]", ComposeSafeColumnName(root.IdentityColumn), ComposeSafeTableName(root.Table.Name))));
			return (value as IConvertible).ToInt64(CultureInfo.InvariantCulture);
		}
		protected override async Task<Int64> GetIdentityAsync(InsertStatement root, TaggedParametersHolder identitiesByTag, AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken) {
			Query sql = new InsertSqlGenerator(this, identitiesByTag, new Dictionary<OperandValue, string>()).GenerateSql(root);
			await ExecSqlAsync(sql, asyncOperationId, cancellationToken).ConfigureAwait(false);
			object value = await GetScalarAsync(new Query(string.Format("SELECT LastIdentity([{0}]) FROM [{1}]", ComposeSafeColumnName(root.IdentityColumn), ComposeSafeTableName(root.Table.Name))), asyncOperationId, cancellationToken).ConfigureAwait(false);
			return (value as IConvertible).ToInt64(CultureInfo.InvariantCulture);
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
				if(parameterMode == QueryParameterMode.SetTypeAndSize) {
					ValidateParameterSize(command, param);
				}
			}
			return param;
		}
		protected override Exception WrapException(Exception e, IDbCommand query) {
			object[] values;
			XPVistaDBDA vistaDbda = GetEngine(ConnectionHelper.ConnectionType);
			Exception sqlException = e;
			while(sqlException != null && ConnectionHelper.TryGetExceptionProperties(sqlException, new string[] { "ErrorId" }, out values)) {
				if(values != null && values[0] is int) {
					int errorId = (int)values[0];
					if(errorId == vistaDbda.Error_sql_ColumnDoesNotExist || errorId == vistaDbda.Error_sql_TableNotExist)
						return new SchemaCorrectionNeededException(sqlException);
					if(errorId == vistaDbda.Error_dda_CreateRow || errorId == vistaDbda.Error_dda_DeleteRow)
						return new ConstraintViolationException(query.CommandText, GetParametersString(query), e);
				}
				sqlException = sqlException.InnerException;
			}
			return base.WrapException(e, query);
		}
		protected override void OpenConnectionInternal() {
			if(fDataBase != null) {
				XPVistaDBDA vistaDbda = GetEngine(Connection.GetType());
				if(vistaDbda.GetConnectionOpenmode(Connection) == VistaDBExclusiveReadWriteString) {
					CloseDatabase();
				}
			}
			base.OpenConnectionInternal();
		}
		XPVistaDBDatabase fDataBase;
		XPVistaDBDatabase DataBase {
			get {
				if(fDataBase == null) {
					XPVistaDBDA vistaDbda = GetEngine(Connection.GetType());
					string openMode = vistaDbda.GetConnectionOpenmode(Connection);
					if(Connection.State == ConnectionState.Open && openMode == VistaDBExclusiveReadWriteString) {
						Connection.Close();
					}
					bool isProcessMode = (openMode == VistaDBSingleProcessReadWriteString
										  || openMode == VistaDBSingleProcessReadOnlyString
										  || openMode == VistaDBMultiProcessReadWriteString
										  || openMode == VistaDBMultiProcessReadOnlyString);
					fDataBase = vistaDbda.OpenDatabase(vistaDbda.GetConnectionSource(Connection),
													   isProcessMode ? openMode : VistaDBNonexclusiveReadWriteString,
													   vistaDbda.GetConnectionPassword(Connection) == string.Empty ? null : vistaDbda.GetConnectionPassword(Connection));
				}
				return fDataBase;
			}
		}
		protected override void CreateDataBase() {
			try {
				OpenConnectionInternal();
			}
			catch(Exception e) {
				object[] values;
				XPVistaDBDA vistaDbda = GetEngine(ConnectionHelper.ConnectionType);
				if(ConnectionHelper.TryGetExceptionProperties(e, new string[] { "ErrorId" }, out values)
					&& values != null && values[0] is int && ((int)(values[0])) == vistaDbda.Error_dda_OpenDatabase
					&& CanCreateDatabase) {
					vistaDbda.CreateDatabase(vistaDbda.GetConnectionSource(Connection), false, vistaDbda.GetConnectionPassword(Connection) == string.Empty ? null : vistaDbda.GetConnectionPassword(Connection), 0, CultureInfo.CurrentCulture.LCID, false).Close();
				}
				else {
					throw new UnableToOpenDatabaseException(XpoDefault.ConnectionStringRemovePassword(ConnectionString), e);
				}
			}
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
			if(Transaction != null)
				throw new InvalidOperationException(Res.GetString(Res.VistaDB_UpdatingSchemaIsForbiddenWhileExplicitTran));
			inSchemaUpdate = true;
			try {
				CloseDatabase();
				UpdateSchemaResult res = base.ProcessUpdateSchema(skipIfFirstTableNotExists, tables);
				Connection.Close();
				return res;
			}
			finally {
				inSchemaUpdate = false;
				CloseDatabase();
			}
		}
		protected override async Task<UpdateSchemaResult> ProcessUpdateSchemaAsync(AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken, bool skipIfFirstTableNotExists, DBTable[] tables) {
			if(Transaction != null)
				throw new InvalidOperationException(Res.GetString(Res.VistaDB_UpdatingSchemaIsForbiddenWhileExplicitTran));
			inSchemaUpdate = true;
			try {
				CloseDatabase();
				UpdateSchemaResult res = await base.ProcessUpdateSchemaAsync(asyncOperationId, cancellationToken, skipIfFirstTableNotExists, tables);
				Connection.Close();
				return res;
			}
			finally {
				inSchemaUpdate = false;
				CloseDatabase();
			}
		}
		void CloseDatabase() {
			if(fDataBase != null) {
				fDataBase.Close();
				fDataBase = null;
			}
		}
		protected virtual string MaxStringDataType { get { return ((IDbTypeMapperVistaDb)DbTypeMapper).MaxStringDataType; } }
		protected virtual string MaxBinaryDataType { get { return ((IDbTypeMapperVistaDb)DbTypeMapper).MaxBinaryDataType; } }
		protected virtual string SingleDataType { get { return ((IDbTypeMapperVistaDb)DbTypeMapper).SingleDataType; } }
		protected virtual int MaxStringSize { get { return ((IDbTypeMapperVistaDb)DbTypeMapper).MaxStringSize; } }
		protected virtual int MaxBinarySize { get { return ((IDbTypeMapperVistaDb)DbTypeMapper).MaxBinarySize; } }
		protected string GetDBTypeString(DBColumn column, out short size) {
			return ((IDbTypeMapperVistaDb)DbTypeMapper).GetDBTypeString(column, out size);
		}
		public override void CreateTable(DBTable table) {
			XPVistaDBTableSchema tb = DataBase.NewTable(ComposeSafeTableName(table.Name));
			DBColumn identity = null;
			foreach(DBColumn col in table.Columns) {
				if(col.IsIdentity) {
					identity = col;
				}
				AddColumnToTableSchema(table, col, tb);
			}
			if(identity != null) {
				tb.DefineIdentity(identity.Name, "1", "1");
			}
			DataBase.CreateTable(tb, false, false).Close();
		}
		public override void CreateColumn(DBTable table, DBColumn column) {
			string tableName = ComposeSafeTableName(table.Name);
			XPVistaDBTableSchema tb = DataBase.TableSchema(tableName);
			AddColumnToTableSchema(table, column, tb);
			DataBase.AlterTable(tableName, tb);
		}
		XPVistaDBColumnAttributes AddColumnToTableSchema(DBTable table, DBColumn column, XPVistaDBTableSchema vistaTableSchema) {
			string tableName = ComposeSafeTableName(table.Name);
			string columnName = ComposeSafeColumnName(column.Name);
			short size;
			string type = GetDBTypeString(column, out size);
			XPVistaDBColumnAttributes vistaDbcolAttributes = vistaTableSchema.AddColumn(columnName, type, size, 0);
			if(!column.IsNullable) {
				vistaTableSchema.DefineColumnAttributes(columnName, false, false, false, false, "");
			}
			if(!string.IsNullOrEmpty(column.DbDefaultValue)) {
				vistaTableSchema.DefineDefaultValue(columnName, column.DbDefaultValue, false, "");
			}
			else {
				if(column.DefaultValue != null) {
					string formattedDefaultValue = FormatConstant(column.DefaultValue);
					vistaTableSchema.DefineDefaultValue(columnName, formattedDefaultValue, false, "");
				}
			}
			return vistaDbcolAttributes;
		}
		public override void CreateIndex(DBTable table, DBIndex index) {
			string ind = string.Empty;
			foreach(string col in index.Columns) {
				if(ind != string.Empty)
					ind += ';';
				ind += ComposeSafeColumnName(col);
			}
			XPVistaDBTable tb = DataBase.OpenTable(ComposeSafeTableName(table.Name), false, false);
			tb.CreateIndex(ComposeSafeConstraintName(GetIndexName(index, table)), ind, false, index.IsUnique);
			tb.Close();
		}
		public override void CreatePrimaryKey(DBTable table) {
			string index = string.Empty;
			foreach(string col in table.PrimaryKey.Columns) {
				if(index != string.Empty)
					index += ';';
				index += ComposeSafeColumnName(col);
			}
			XPVistaDBTable tb = DataBase.OpenTable(ComposeSafeTableName(table.Name), false, false);
			tb.CreateIndex(ComposeSafeConstraintName(GetPrimaryKeyName(table.PrimaryKey, table)), index, true, true);
			tb.Close();
		}
		public override void CreateForeignKey(DBTable table, DBForeignKey fk) {
			string cols = string.Empty;
			foreach(string col in fk.Columns) {
				if(cols != string.Empty)
					cols += ';';
				cols += ComposeSafeColumnName(col);
			}
			string refcols = string.Empty;
			foreach(string col in fk.PrimaryKeyTableKeyColumns) {
				if(refcols != string.Empty)
					refcols += ';';
				refcols += ComposeSafeColumnName(col);
			}
			XPVistaDBTable tb = DataBase.OpenTable(ComposeSafeTableName(table.Name), false, false);
			tb.CreateForeignKey(ComposeSafeConstraintName(GetForeignKeyName(fk, table)), cols, ComposeSafeTableName(fk.PrimaryKeyTable),
				"None", "None", string.Empty);
			tb.Close();
		}
		void GetColumns(DBTable table, XPVistaDBTableSchema vtable) {
			Dictionary<string, XPVistaDBDefaultValueInformation> defaultValuesInfo = vtable.GetDefaultValues();
			foreach(XPVistaDBColumnAttributes col in vtable.GetColumns()) {
				object defaultValue = null;
				string dbDefaultValue = null;
				if(defaultValuesInfo != null) {
					XPVistaDBDefaultValueInformation vdbdvi;
					if(defaultValuesInfo.TryGetValue(col.Name, out vdbdvi)) {
						dbDefaultValue = vdbdvi.Expression;
						if(!string.IsNullOrEmpty(dbDefaultValue)) {
							try {
								string scalarQuery = string.Concat("select ", dbDefaultValue);
								defaultValue = FixDBNullScalar(GetScalar(new Query(scalarQuery)));
							}
							catch { }
						}
						if(defaultValue != null) {
							ReformatReadValueArgs refmtArgs = new ReformatReadValueArgs(col.SystemType);
							refmtArgs.AttachValueReadFromDb(defaultValue);
							try {
								defaultValue = ReformatReadValue(defaultValue, refmtArgs);
							}
							catch {
								defaultValue = null;
							}
						}
					}
				}
				DBColumn dBColumn = new DBColumn(col.Name, false, string.Empty, col.MaxLength, DBColumn.GetColumnType(col.SystemType), col.AllowNull, defaultValue);
				dBColumn.DbDefaultValue = dbDefaultValue;
				table.AddColumn(dBColumn);
			}
		}
		void GetPrimaryKey(DBTable table, XPVistaDBTableSchema vtable) {
			var vtableGetIndices = vtable.GetIndices();
			if(vtableGetIndices == null) {
				return;
			}
			foreach(var index in vtableGetIndices) {
				if(index.Primary) {
					StringCollection cols = new StringCollection();
					foreach(string col in index.KeyExpression.Split(';')) {
						var column = table.GetColumn(col);
						if(column != null) {
							column.IsKey = true;
						}
						cols.Add(col);
					}
					table.PrimaryKey = new DBPrimaryKey(index.Name, cols);
					break;
				}
			}
			var vtableGetIdentities = vtable.GetIdentities();
			if(vtableGetIdentities == null) {
				return;
			}
			foreach(var identity in vtableGetIdentities) {
				var column = table.GetColumn(identity.ColumnName);
				if(column != null) {
					column.IsIdentity = true;
				}
			}
		}
		void GetIndexes(DBTable table, XPVistaDBTableSchema vtable) {
			List<XPVistaDBIndexInformation> vtableGetIndices = vtable.GetIndices();
			if(vtableGetIndices == null)
				return;
			foreach(XPVistaDBIndexInformation index in vtableGetIndices) {
				if(!index.Primary) {
					StringCollection cols = new StringCollection();
					foreach(string col in index.KeyExpression.Split(';'))
						cols.Add(col);
					table.Indexes.Add(new DBIndex(index.Name, cols, index.Unique));
				}
			}
		}
		void GetForeignKeys(DBTable table, XPVistaDBTableSchema vtable) {
			List<XPVistaDBRelationshipInformation> vtableGetForeignKeys = vtable.GetForeignKeys();
			if(vtableGetForeignKeys == null)
				return;
			foreach(XPVistaDBRelationshipInformation fk in vtableGetForeignKeys) {
				StringCollection cols = new StringCollection();
				foreach(string col in fk.ForeignKey.Split(';'))
					cols.Add(col);
				StringCollection rcols = new StringCollection();
				foreach(string col in fk.ForeignKey.Split(';'))
					rcols.Add(col);
				DBTable pk = new DBTable();
				GetPrimaryKey(pk, DataBase.TableSchema(fk.PrimaryTable));
				table.ForeignKeys.Add(new DBForeignKey(cols, fk.PrimaryTable, pk.PrimaryKey.Columns));
			}
		}
		public override void GetTableSchema(DBTable table, bool checkIndexes, bool checkForeignKeys) {
			XPVistaDBTableSchema tb = DataBase.TableSchema(ComposeSafeTableName(table.Name));
			GetColumns(table, tb);
			GetPrimaryKey(table, tb);
			if(checkIndexes)
				GetIndexes(table, tb);
			if(checkForeignKeys)
				GetForeignKeys(table, tb);
		}
		public override ICollection CollectTablesToCreate(ICollection tables) {
			IEnumerable<string> exist = DataBase.GetTableNames();
			Dictionary<string, bool> dbTables = new Dictionary<string, bool>();
			foreach(string t in exist)
				dbTables.Add(t, false);
			SelectStatementResult data = SelectData(new Query("SELECT VIEW_NAME FROM GetViews() ORDER BY VIEW_NAME"));
			foreach(SelectStatementResultRow row in data.Rows)
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
		protected override bool NeedsIndexForForeignKey { get { return false; } }
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
			return string.Format(CultureInfo.InvariantCulture, "select {0}{1} from {2}{3}{4}{5}{6}", modificatorsSql, selectedPropertiesSql, fromSql, expandedWhereSql, expandedGroupBySql, expandedHavingSql, expandedOrderBySql);
		}
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
		public override string FormatDelete(string tableName, string whereClause) {
			return string.Format(CultureInfo.InvariantCulture, "delete from {0} where {1}", tableName, whereClause);
		}
		public override string FormatFunction(FunctionOperatorType operatorType, params string[] operands) {
			switch(operatorType) {
				case FunctionOperatorType.Abs:
					return string.Format(CultureInfo.InvariantCulture, "Abs({0})", operands[0]);
				case FunctionOperatorType.Sign:
					return string.Format(CultureInfo.InvariantCulture, "Sign({0})", operands[0]);
				case FunctionOperatorType.Round:
					switch(operands.Length) {
						case 1:
							return string.Format(CultureInfo.InvariantCulture, "Round({0}, 0)", operands[0]);
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "Round({0}, {1})", operands[0], operands[1]);
					}
					goto default;
				case FunctionOperatorType.Floor:
					return string.Format(CultureInfo.InvariantCulture, "Floor({0})", operands[0]);
				case FunctionOperatorType.Ceiling:
					return string.Format(CultureInfo.InvariantCulture, "Ceiling({0})", operands[0]);
				case FunctionOperatorType.Power:
					return string.Format(CultureInfo.InvariantCulture, "Power({0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.Cos:
					return string.Format(CultureInfo.InvariantCulture, "Cos({0})", operands[0]);
				case FunctionOperatorType.Sin:
					return string.Format(CultureInfo.InvariantCulture, "Sin({0})", operands[0]);
				case FunctionOperatorType.Exp:
					return string.Format(CultureInfo.InvariantCulture, "Exp({0})", operands[0]);
				case FunctionOperatorType.Log:
					switch(operands.Length) {
						case 1:
							return string.Format(CultureInfo.InvariantCulture, "Log({0})", operands[0]);
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "(Log({0}) / Log({1}))", operands[0], operands[1]);
					}
					goto default;
				case FunctionOperatorType.BigMul:
					return string.Format(CultureInfo.InvariantCulture, "(Convert(bigint, {0}) * CONVERT(bigint,  {1}))", operands[0], operands[1]);
				case FunctionOperatorType.Log10:
					return string.Format(CultureInfo.InvariantCulture, "Log10({0})", operands[0]);
				case FunctionOperatorType.Tan:
					return string.Format(CultureInfo.InvariantCulture, "Tan({0})", operands[0]);
				case FunctionOperatorType.Atn:
					return string.Format(CultureInfo.InvariantCulture, "Atan({0})", operands[0]);
				case FunctionOperatorType.Atn2:
					return string.Format(CultureInfo.InvariantCulture, "Atn2({0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.Acos:
					return string.Format(CultureInfo.InvariantCulture, "Acos({0})", operands[0]);
				case FunctionOperatorType.Asin:
					return string.Format(CultureInfo.InvariantCulture, "Asin({0})", operands[0]);
				case FunctionOperatorType.Cosh:
					return string.Format(CultureInfo.InvariantCulture, "((Exp({0}) + Exp(-({0}))) / 2)", operands[0]);
				case FunctionOperatorType.Sinh:
					return string.Format(CultureInfo.InvariantCulture, "((Exp({0}) - Exp(-({0}))) / 2)", operands[0]);
				case FunctionOperatorType.Tanh:
					return string.Format(CultureInfo.InvariantCulture, "((Exp({0}) - Exp(-({0}))) / (Exp({0}) + Exp(-({0}))))", operands[0]);
				case FunctionOperatorType.Max:
					return string.Format(CultureInfo.InvariantCulture, "iif({0} > {1}, {0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.Min:
					return string.Format(CultureInfo.InvariantCulture, "iif({0} < {1}, {0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.Rnd:
					return "RAND()";
				case FunctionOperatorType.Sqr:
					return string.Format(CultureInfo.InvariantCulture, "Sqrt({0})", operands[0]);
				case FunctionOperatorType.GetMilliSecond:
					return string.Format(CultureInfo.InvariantCulture, "DATEPART(Millisecond, {0})", operands[0]);
				case FunctionOperatorType.GetSecond:
					return string.Format(CultureInfo.InvariantCulture, "DATEPART(Second, {0})", operands[0]);
				case FunctionOperatorType.GetMinute:
					return string.Format(CultureInfo.InvariantCulture, "DATEPART(Minute, {0})", operands[0]);
				case FunctionOperatorType.GetHour:
					return string.Format(CultureInfo.InvariantCulture, "DATEPART(Hour, {0})", operands[0]);
				case FunctionOperatorType.GetDay:
					return string.Format(CultureInfo.InvariantCulture, "DATEPART(Day, {0})", operands[0]);
				case FunctionOperatorType.GetMonth:
					return string.Format(CultureInfo.InvariantCulture, "DATEPART(Month, {0})", operands[0]);
				case FunctionOperatorType.GetYear:
					return string.Format(CultureInfo.InvariantCulture, "DATEPART(Year, {0})", operands[0]);
				case FunctionOperatorType.GetTimeOfDay:
					return string.Format(CultureInfo.InvariantCulture, "(CONVERT(BigInt,((CONVERT(BigInt,DATEPART(HOUR, {0}))) * 36000000000) + ((CONVERT(BigInt,DATEPART(MINUTE, {0}))) * 600000000) + ((CONVERT(BigInt,DATEPART(SECOND, {0}))) * 10000000) + ((CONVERT(BigInt,DATEPART(MILLISECOND, {0}))) * 10000)))", operands[0]);
				case FunctionOperatorType.GetDayOfWeek:
					return string.Format(CultureInfo.InvariantCulture, "CONVERT(Int, (DATEPART(dw, {0}) - DATEPART(dw, '1900.01.01') + 8) % 7)", operands[0]);
				case FunctionOperatorType.GetDayOfYear:
					return string.Format(CultureInfo.InvariantCulture, "DATEPART(DayOfYear, {0})", operands[0]);
				case FunctionOperatorType.GetDate:
					return string.Format(CultureInfo.InvariantCulture, "DATEADD(HOUR, -DATEPART(HOUR, {0}), DATEADD(MINUTE, -DATEPART(MINUTE, {0}), DATEADD(SECOND, -DATEPART(SECOND, {0}), DATEADD(MILLISECOND, -DATEPART(MILLISECOND, {0}), {0}))))", operands[0]);
				case FunctionOperatorType.AddTicks:
					return string.Format(CultureInfo.InvariantCulture, "DATEADD(ms, CONVERT(BigInt, ({1}) / 10000) % 86400000, DATEADD(day, ({1}) / 864000000000, {0}))", operands[0], operands[1]);
				case FunctionOperatorType.AddMilliSeconds:
					return string.Format(CultureInfo.InvariantCulture, "DATEADD(ms, {1}, {0})", operands[0], operands[1]);
				case FunctionOperatorType.AddTimeSpan:
				case FunctionOperatorType.AddSeconds:
					return string.Format(CultureInfo.InvariantCulture, "DATEADD(ms, (CONVERT(decimal(38, 19),({1})) * 1000) % 86400000, DATEADD(day, (CONVERT(decimal(38, 19),({1})) * 1000) / 86400000, {0}))", operands[0], operands[1]);
				case FunctionOperatorType.AddMinutes:
					return string.Format(CultureInfo.InvariantCulture, "DATEADD(ms, (CONVERT(decimal(38, 19),({1})) * 60000) % 86400000, DATEADD(day, (CONVERT(decimal(38, 19),({1})) * 60000) / 86400000, {0}))", operands[0], operands[1]);
				case FunctionOperatorType.AddHours:
					return string.Format(CultureInfo.InvariantCulture, "DATEADD(ms, (CONVERT(decimal(38, 19),({1})) * 3600000) % 86400000, DATEADD(day, (CONVERT(decimal(38, 19),({1})) * 3600000) / 86400000, {0}))", operands[0], operands[1]);
				case FunctionOperatorType.AddDays:
					return string.Format(CultureInfo.InvariantCulture, "DATEADD(ms, (CONVERT(decimal(38, 19),({1})) * 86400000) % 86400000, DATEADD(day, (CONVERT(decimal(38, 19),({1})) * 86400000) / 86400000, {0}))", operands[0], operands[1]);
				case FunctionOperatorType.AddMonths:
					return string.Format(CultureInfo.InvariantCulture, "DATEADD(MONTH, {1}, {0})", operands[0], operands[1]);
				case FunctionOperatorType.AddYears:
					return string.Format(CultureInfo.InvariantCulture, "DATEADD(YEAR, {1}, {0})", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffDay:
					return string.Format(CultureInfo.InvariantCulture, "DATEDIFF(day, {0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffHour:
					return string.Format(CultureInfo.InvariantCulture, "((DATEDIFF(day, {0}, {1}) * 24) + DATEPART(Hour, {1}) - DATEPART(Hour, {0}))", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffMilliSecond:
					return string.Format(CultureInfo.InvariantCulture, "DATEDIFF(millisecond, {0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffMinute:
					return string.Format(CultureInfo.InvariantCulture, "((((DATEDIFF(day, {0}, {1}) * 24) + DATEPART(Hour, {1}) - DATEPART(Hour, {0})) * 60) + DATEPART(Minute, {1}) - DATEPART(Minute, {0}))", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffMonth:
					return string.Format(CultureInfo.InvariantCulture, "DATEDIFF(month, {0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffSecond:
					return string.Format(CultureInfo.InvariantCulture, "DATEDIFF(second, DATEADD(ms, -DATEPART(Millisecond, {0}), {0}), DATEADD(ms, -DATEPART(Millisecond, {1}), {1}))", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffTick:
					return string.Format(CultureInfo.InvariantCulture, "((DATEDIFF(millisecond, {0}, {1})) * 10000)", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffYear:
					return string.Format(CultureInfo.InvariantCulture, "DATEDIFF(year, {0}, {1})", operands[0], operands[1]);
				case FunctionOperatorType.Now:
					return "GETDATE()";
				case FunctionOperatorType.UtcNow:
					return "GETUTCDATE()";
				case FunctionOperatorType.Today:
					return "DATEADD(day, DATEDIFF(day, '00:00:00', getdate()), '00:00:00')";
				case FunctionOperatorType.Ascii:
					return string.Format(CultureInfo.InvariantCulture, "Ascii({0})", operands[0]);
				case FunctionOperatorType.Char:
					return string.Format(CultureInfo.InvariantCulture, "Char({0})", operands[0]);
				case FunctionOperatorType.Replace:
					return string.Format(CultureInfo.InvariantCulture, "Replace({0}, {1}, {2})", operands[0], operands[1], operands[2]);
				case FunctionOperatorType.Reverse:
					return string.Format(CultureInfo.InvariantCulture, "Reverse({0})", operands[0]);
				case FunctionOperatorType.Remove:
					switch(operands.Length) {
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "LEFT({0}, {1})", operands[0], operands[1]);
						case 3:
							return string.Format(CultureInfo.InvariantCulture, "Stuff({0}, ({1})+1, {2}, '')", operands[0], operands[1], operands[2]);
					}
					goto default;
				case FunctionOperatorType.Insert:
					return string.Format(CultureInfo.InvariantCulture, "Stuff({0}, ({1}) + 1, 0, {2})", operands[0], operands[1], operands[2]);
				case FunctionOperatorType.Concat:
					string args = string.Empty;
					foreach(string arg in operands) {
						if(args.Length > 0)
							args += " + ";
						args += arg;
					}
					return args;
				case FunctionOperatorType.CharIndex:
					switch(operands.Length) {
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "(Charindex({0}, {1}) - 1)", operands[0], operands[1]);
						case 3:
							return string.Format(CultureInfo.InvariantCulture, "(Charindex({0}, {1}, ({2}) + 1) - 1)", operands[0], operands[1], operands[2]);
						case 4:
							return string.Format(CultureInfo.InvariantCulture, "(Charindex({0}, SUBSTRING({1}, 1, ({2}) + ({3})), ({2}) + 1) - 1)", operands[0], operands[1], operands[2], operands[3]);
					}
					goto default;
				case FunctionOperatorType.PadLeft:
					switch(operands.Length) {
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "isnull(REPLICATE(' ', (({1}) - LEN({0}))) + ({0}), {0})", operands[0], operands[1]);
						case 3:
							return string.Format(CultureInfo.InvariantCulture, "isnull(REPLICATE({2}, (({1}) - LEN({0}))) + ({0}), {0})", operands[0], operands[1], operands[2]);
					}
					goto default;
				case FunctionOperatorType.PadRight:
					switch(operands.Length) {
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "isnull(({0}) + REPLICATE(' ', (({1}) - LEN({0}))), {0})", operands[0], operands[1]);
						case 3:
							return string.Format(CultureInfo.InvariantCulture, "isnull(({0}) + REPLICATE({2}, (({1}) - LEN({0}))), {0})", operands[0], operands[1], operands[2]);
					}
					goto default;
				case FunctionOperatorType.IsNull:
					switch(operands.Length) {
						case 1:
							return string.Format(CultureInfo.InvariantCulture, "(({0}) is null)", operands[0]);
						case 2:
							return string.Format(CultureInfo.InvariantCulture, "isnull({0}, {1})", operands[0], operands[1]);
					}
					goto default;
				case FunctionOperatorType.IsNullOrEmpty:
					return string.Format(CultureInfo.InvariantCulture, "(({0}) is null or len({0}) = 0)", operands[0]);
				case FunctionOperatorType.EndsWith:
					return string.Format(CultureInfo.InvariantCulture, "(RigHt({0}, Len({1})) = ({1}))", operands[0], operands[1]);
				case FunctionOperatorType.Contains:
					return string.Format(CultureInfo.InvariantCulture, "(isnull(CharIndEX({1}, {0}), 0) > 0)", operands[0], operands[1]);
				case FunctionOperatorType.ToInt:
					return string.Format(CultureInfo.InvariantCulture, "Cast(({0}) as int)", operands[0]);
				case FunctionOperatorType.ToLong:
					return string.Format(CultureInfo.InvariantCulture, "Cast(({0}) as bigint)", operands[0]);
				case FunctionOperatorType.ToFloat:
					return string.Format(CultureInfo.InvariantCulture, "Cast(({0}) as real)", operands[0]);
				case FunctionOperatorType.ToDouble:
					return string.Format(CultureInfo.InvariantCulture, "Cast(({0}) as float)", operands[0]);
				case FunctionOperatorType.ToDecimal:
					return string.Format(CultureInfo.InvariantCulture, "Cast(({0}) as money)", operands[0]);
				case FunctionOperatorType.ToStr:
					return string.Format(CultureInfo.InvariantCulture, "Cast(({0}) as nvarchar(max))", operands[0]);
				default:
					return base.FormatFunction(operatorType, operands);
			}
		}
		readonly static char[] achtungChars = new char[] { '_', '%', '[', ']' };
		public override string FormatFunction(ProcessParameter processParameter, FunctionOperatorType operatorType, params object[] operands) {
			switch(operatorType) {
				case FunctionOperatorType.StartsWith: {
					object secondOperand = operands[1];
					if(secondOperand is OperandValue && ((OperandValue)secondOperand).Value is string) {
						string operandString = (string)((OperandValue)secondOperand).Value;
						int likeIndex = operandString.IndexOfAny(achtungChars);
						if(likeIndex < 0) {
							return string.Format(CultureInfo.InvariantCulture, "({0} likE {1})", processParameter(operands[0]), processParameter(new ConstantValue(operandString + "%")));
						}
						else if(likeIndex > 0) {
							return string.Format(CultureInfo.InvariantCulture, "(({0} likE {2}) And (CharIndeX({1}, {0}) = 1))", processParameter(operands[0]), processParameter(secondOperand), processParameter(new ConstantValue(operandString.Substring(0, likeIndex) + "%")));
						}
					}
					return string.Format(CultureInfo.InvariantCulture, "(CharIndeX({1}, {0}) = 1)", processParameter(operands[0]), processParameter(secondOperand));
				}
				default:
					return base.FormatFunction(processParameter, operatorType, operands);
			}
		}
		public override string FormatBinary(BinaryOperatorType operatorType, string leftOperand, string rightOperand) {
			switch(operatorType) {
				case BinaryOperatorType.Modulo:
					return string.Format(CultureInfo.InvariantCulture, "({0}) % ({1})", leftOperand, rightOperand);
				default:
					return base.FormatBinary(operatorType, leftOperand, rightOperand);
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
		public override string FormatConstraint(string constraintName) {
			return string.Format(CultureInfo.InvariantCulture, "\"{0}\"", constraintName);
		}
		protected virtual string FormatConstant(object value) {
			TypeCode tc = DXTypeExtensions.GetTypeCode(value.GetType());
			switch(tc) {
				case DXTypeExtensions.TypeCodeDBNull:
				case TypeCode.Empty:
					return "NULL";
				case TypeCode.Boolean:
					return ((bool)value) ? "1" : "0";
				case TypeCode.Char:
					return "'" + (char)value + "'";
				case TypeCode.DateTime:
					DateTime datetimeValue = (DateTime)value;
					string dateTimeFormatPattern = "yyyy-MM-dd HH:mm:ss.fff";
					return string.Format("convert(datetime, '{0}', 121)", datetimeValue.ToString(dateTimeFormatPattern, CultureInfo.InvariantCulture));
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
						return "Cast('" + ((Guid)value).ToString() + "' as uniqueidentifier)";
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
		protected override int GetSafeNameTableMaxLength() {
			return 63;
		}
		protected override void ProcessClearDatabase() {
			Connection.Close();
			string[] tables = GetStorageTablesList(false);
			foreach(string table in tables) {
				XPVistaDBTableSchema schema = DataBase.TableSchema(table);
				XPVistaDBTable t = DataBase.OpenTable(table, true, false);
				List<XPVistaDBRelationshipInformation> schemaGetForeignKeys = schema.GetForeignKeys();
				if(schemaGetForeignKeys != null) {
					foreach(XPVistaDBRelationshipInformation fk in schemaGetForeignKeys) {
						t.DropForeignKey(fk.Name);
					}
				}
				t.Close();
			}
			CloseDatabase();
			foreach(string table in tables) {
				DataBase.DropTable(table);
			}
			if(Connection.State == ConnectionState.Open) Connection.Close();
			CloseDatabase();
		}
		public override string[] GetStorageTablesList(bool includeViews) {
			List<string> list = new List<string>();
			IEnumerable<string> tables = DataBase.GetTableNames();
			CloseDatabase();
			foreach(string tableName in tables)
				list.Add(tableName);
			if(!includeViews) return list.ToArray();
			string query = "SELECT name FROM [database schema] WHERE  typeid = 10";
			SelectStatementResult result = SelectData(new Query(query));
			if(result.Rows.Length == 0) return list.ToArray();
			foreach(SelectStatementResultRow row in result.Rows) {
				list.Add(((string)row.Values[0]).TrimEnd());
			}
			return list.ToArray();
		}
		protected override void CommandBuilderDeriveParameters(IDbCommand command) {
			throw new NotSupportedException();
		}
		public override DBStoredProcedure[] GetStoredProcedures() {
			throw new NotSupportedException();
		}
		public void Dispose() {
			if(Connection != null && Connection.State == ConnectionState.Open) Connection.Close();
			CloseDatabase();
		}
	}
	public class VistaDBConnectionProvider : VistaDBConnectionProviderBase {
		public const string XpoProviderTypeString = "VistaDB";
		public static string GetConnectionString(string database) {
			return string.Format("{1}={2};Data Source={0}",
				EscapeConnectionStringArgument(database), DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString);
		}
		public static string GetConnectionString(string database, string password) {
			return string.Format("{2}={3};Data Source={0};Password={1}",
				EscapeConnectionStringArgument(database), EscapeConnectionStringArgument(password), DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString);
		}
		public static IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			IDbConnection connection = CreateConnection(connectionString);
			IDataStore result = CreateProviderFromConnection(connection, autoCreateOption);
			objectsToDisposeOnDisconnect = new IDisposable[] { connection, (IDisposable)result };
			return result;
		}
		public static IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return CreateProviderFromConnectionSelector(connection, autoCreateOption);
		}
		public static IDbConnection CreateConnection(string connectionString) {
			return ReflectConnectionHelper.GetConnection(VistaDB4AssemblyName, "VistaDB.Provider.VistaDBConnection", connectionString);
		}
		static VistaDBConnectionProvider() {
			RegisterDataStoreProvider(XpoProviderTypeString, new DataStoreCreationFromStringDelegate(CreateProviderFromString));
			RegisterDataStoreProvider("VistaDB.Provider.VistaDBConnection", new DataStoreCreationFromConnectionDelegate(CreateProviderFromConnection));
			RegisterFactory(new VistaDBProviderFactory());
		}
		public static void Register() { }
		public VistaDBConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption)
			: base(connection, autoCreateOption, true) {
		}
		protected VistaDBConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption, bool openConnection)
			: base(connection, autoCreateOption, openConnection) {
		}
		protected override IDbConnection CreateConnection() {
			return VistaDBConnectionProvider.CreateConnection(ConnectionString);
		}
		protected override DbTypeMapperBase CreateDbTypeMapper(Type vistaDbTypeEnumType, Type vistaDbParameterType) {
			return (DbTypeMapperBase)Activator.CreateInstance(typeof(DbTypeMapperVistaDb<,>).MakeGenericType(vistaDbTypeEnumType, vistaDbParameterType));
		}
	}
	public class VistaDB5ConnectionProvider : VistaDBConnectionProviderBase {
		public const string XpoProviderTypeString = "VistaDB5";
		public static string GetConnectionString(string database) {
			return string.Format("{1}={2};Data Source={0}",
				EscapeConnectionStringArgument(database), DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString);
		}
		public static string GetConnectionString(string database, string password) {
			return string.Format("{2}={3};Data Source={0};Password={1}",
				EscapeConnectionStringArgument(database), EscapeConnectionStringArgument(password), DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString);
		}
		public static IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			IDbConnection connection = CreateConnection(connectionString);
			IDataStore result = CreateProviderFromConnection(connection, autoCreateOption);
			objectsToDisposeOnDisconnect = new IDisposable[] { connection, (IDisposable)result };
			return result;
		}
		public static IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return CreateProviderFromConnectionSelector(connection, autoCreateOption);
		}
		public static IDbConnection CreateConnection(string connectionString) {
			return ReflectConnectionHelper.GetConnection(VistaDB5AssemblyName, "VistaDB.Provider.VistaDBConnection", connectionString);
		}
		static VistaDB5ConnectionProvider() {
			RegisterDataStoreProvider(XpoProviderTypeString, new DataStoreCreationFromStringDelegate(CreateProviderFromString));
			RegisterDataStoreProvider("VistaDB.Provider.VistaDBConnection", new DataStoreCreationFromConnectionDelegate(CreateProviderFromConnection));
			RegisterFactory(new VistaDB5ProviderFactory());
		}
		public static void Register() { }
		public VistaDB5ConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption)
			: base(connection, autoCreateOption, true) {
		}
		protected VistaDB5ConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption, bool openConnection)
			: base(connection, autoCreateOption, openConnection) {
		}
		protected override IDbConnection CreateConnection() {
			return VistaDB5ConnectionProvider.CreateConnection(ConnectionString);
		}
		public override bool NativeSkipTakeSupported { get { return true; } }
		public override string FormatSelect(string selectedPropertiesSql, string fromSql, string whereSql, string orderBySql, string groupBySql, string havingSql, int skipSelectedRecords, int topSelectedRecords) {
			if(skipSelectedRecords != 0) {
				if(!NativeSkipTakeSupported) {
					throw new NotSupportedException();
				}
				if(orderBySql == null) {
					throw new InvalidOperationException("Can not skip records without ORDER BY clause.");
				}
			}
			string expandedWhereSql = whereSql == null ? null : ("\nwhere " + whereSql);
			string expandedOrderBySql = orderBySql != null ? "\norder by " + orderBySql : string.Empty;
			string expandedHavingSql = havingSql != null ? "\nhaving " + havingSql : string.Empty;
			string expandedGroupBySql = groupBySql != null ? "\ngroup by " + groupBySql : string.Empty;
			if(skipSelectedRecords == 0) {
				string topSql = string.Format(CultureInfo.InvariantCulture, "top {0} ", topSelectedRecords);
				return string.Format(CultureInfo.InvariantCulture, "select {0}{1} from {2}{3}{4}{5}{6}", topSql, selectedPropertiesSql, fromSql, expandedWhereSql, expandedGroupBySql, expandedHavingSql, expandedOrderBySql);
			}
			string fetchRowsSql = topSelectedRecords != 0 ? string.Format(CultureInfo.InvariantCulture, "\nfetch next {0} rows only", topSelectedRecords) : string.Empty;
			return string.Format(CultureInfo.InvariantCulture, "select {0} from {1}{2}{3}{4}{5}\noffset {6} rows{7}", selectedPropertiesSql, fromSql, expandedWhereSql, expandedGroupBySql, expandedHavingSql, expandedOrderBySql, skipSelectedRecords, fetchRowsSql);
		}
		public override string FormatInsertDefaultValues(string tableName) {
			return string.Format(CultureInfo.InvariantCulture, "insert into {0} default values", tableName);
		}
		protected override string FormatConstant(object value) {
			if(value == null) {
				return base.FormatConstant(value);
			}
			TypeCode tc = DXTypeExtensions.GetTypeCode(value.GetType());
			switch(tc) {
				case TypeCode.DateTime:
					DateTime datetimeValue = (DateTime)value;
					string dateTimeFormatPattern = "yyyy-MM-dd HH:mm:ss.fffffff";
					return string.Format("convert(datetime, '{0}', 121)", datetimeValue.ToString(dateTimeFormatPattern, CultureInfo.InvariantCulture));
				default:
					return base.FormatConstant(value);
			}
		}
		public override void GetTableSchema(DBTable table, bool checkIndexes, bool checkForeignKeys) {
			var dbConnection = Connection as System.Data.Common.DbConnection;
			if(dbConnection != null) {
				var oldState = dbConnection.State;
				if(oldState != ConnectionState.Open)
					OpenConnection();
				try {
					var viewSchema = dbConnection.GetSchema("VIEWCOLUMNS", new string[] { null, null, ComposeSafeTableName(table.Name), null });
					if(viewSchema.Rows.Count > 0) {
						foreach(DataRow dataRow in viewSchema.Rows) {
							table.AddColumn(CreateColumnFromSchemaDataRow(dataRow));
						}
						return;
					}
				}
				finally {
					if(oldState != ConnectionState.Open)
						Connection.Close();
				}
			}
			base.GetTableSchema(table, checkIndexes, checkForeignKeys);
		}
		DBColumn CreateColumnFromSchemaDataRow(DataRow dataRow) {
			var columnType = Convert.ToString(dataRow["DATA_TYPE"]);
			var columnSize = Convert.ToInt16(dataRow["CHARACTER_MAXIMUM_LENGTH"]);
			return new DBColumn(Convert.ToString(dataRow["COLUMN_NAME"]), Convert.ToBoolean(dataRow["PRIMARY_KEY"]), columnType, columnSize, GetDBColumnType(columnType, Convert.ToInt16(dataRow["CHARACTER_MAXIMUM_LENGTH"])));
		}
		DBColumnType GetDBColumnType(string columnType, short size) {
			switch(columnType) {
				case "VarChar":
				case "NVarChar":
				case "Text":
				case "NText":
					return DBColumnType.String;
				case "Date":
				case "DateTime":
				case "DateTime2":
					return DBColumnType.DateTime;
				case "Bit":
					return DBColumnType.Boolean;
				case "Char":
				case "NChar":
					return size == 1 ? DBColumnType.Char : DBColumnType.String;
				case "UniqueIdentifier":
					return DBColumnType.Guid;
				case "Money":
				case "SmallMoney":
				case "Decimal":
					return DBColumnType.Decimal;
				case "Float":
					return DBColumnType.Double;
				case "Real":
					return DBColumnType.Single;
				case "TinyInt":
					return DBColumnType.Byte;
				case "SmallInt":
					return DBColumnType.Int16;
				case "Int":
					return DBColumnType.Int32;
				case "BigInt":
					return DBColumnType.Int64;
				case "Image":
				case "Binary":
				case "VarBinary":
					return DBColumnType.ByteArray;
			}
			return DBColumnType.Unknown;
		}
		protected override DbTypeMapperBase CreateDbTypeMapper(Type vistaDbTypeEnumType, Type vistaDbParameterType) {
			return (DbTypeMapperBase)Activator.CreateInstance(typeof(DbTypeMapperVistaDb5<,>).MakeGenericType(vistaDbTypeEnumType, vistaDbParameterType));
		}
	}
	public class VistaDB6ConnectionProvider : VistaDBConnectionProviderBase {
		public const string XpoProviderTypeString = "VistaDB6";
		public static string GetConnectionString(string database) {
			return string.Format("{1}={2};Data Source={0}",
				EscapeConnectionStringArgument(database), DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString);
		}
		public static string GetConnectionString(string database, string password) {
			return string.Format("{2}={3};Data Source={0};Password={1}",
				EscapeConnectionStringArgument(database), EscapeConnectionStringArgument(password), DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString);
		}
		public static IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			IDbConnection connection = CreateConnection(connectionString);
			IDataStore result = CreateProviderFromConnection(connection, autoCreateOption);
			objectsToDisposeOnDisconnect = new IDisposable[] { connection, (IDisposable)result };
			return result;
		}
		public static IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return CreateProviderFromConnectionSelector(connection, autoCreateOption);
		}
		public static IDbConnection CreateConnection(string connectionString) {
			IDbConnection connection = ReflectConnectionHelper.GetConnection(VistaDB6AssemblyName, "VistaDB.Provider.VistaDBConnection", true);
			connection.ConnectionString = connectionString;
			return connection;
		}
		static VistaDB6ConnectionProvider() {
			RegisterDataStoreProvider(XpoProviderTypeString, new DataStoreCreationFromStringDelegate(CreateProviderFromString));
			RegisterDataStoreProvider("VistaDB.Provider.VistaDBConnection", new DataStoreCreationFromConnectionDelegate(CreateProviderFromConnection));
			RegisterFactory(new VistaDB6ProviderFactory());
		}
		public static void Register() { }
		public VistaDB6ConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption)
			: base(connection, autoCreateOption, true) {
		}
		protected VistaDB6ConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption, bool openConnection)
			: base(connection, autoCreateOption, openConnection) {
		}
		protected override IDbConnection CreateConnection() {
			return VistaDB6ConnectionProvider.CreateConnection(ConnectionString);
		}
		public override bool NativeSkipTakeSupported { get { return true; } }
		public override string FormatSelect(string selectedPropertiesSql, string fromSql, string whereSql, string orderBySql, string groupBySql, string havingSql, int skipSelectedRecords, int topSelectedRecords) {
			if(skipSelectedRecords != 0) {
				if(!NativeSkipTakeSupported) {
					throw new NotSupportedException();
				}
				if(orderBySql == null) {
					throw new InvalidOperationException("Can not skip records without ORDER BY clause.");
				}
			}
			string expandedWhereSql = whereSql == null ? null : ("\nwhere " + whereSql);
			string expandedOrderBySql = orderBySql != null ? "\norder by " + orderBySql : string.Empty;
			string expandedHavingSql = havingSql != null ? "\nhaving " + havingSql : string.Empty;
			string expandedGroupBySql = groupBySql != null ? "\ngroup by " + groupBySql : string.Empty;
			if(skipSelectedRecords == 0) {
				string topSql = string.Format(CultureInfo.InvariantCulture, "top {0} ", topSelectedRecords);
				return string.Format(CultureInfo.InvariantCulture, "select {0}{1} from {2}{3}{4}{5}{6}", topSql, selectedPropertiesSql, fromSql, expandedWhereSql, expandedGroupBySql, expandedHavingSql, expandedOrderBySql);
			}
			string fetchRowsSql = topSelectedRecords != 0 ? string.Format(CultureInfo.InvariantCulture, "\nfetch next {0} rows only", topSelectedRecords) : string.Empty;
			return string.Format(CultureInfo.InvariantCulture, "select {0} from {1}{2}{3}{4}{5}\noffset {6} rows{7}", selectedPropertiesSql, fromSql, expandedWhereSql, expandedGroupBySql, expandedHavingSql, expandedOrderBySql, skipSelectedRecords, fetchRowsSql);
		}
		public override string FormatInsertDefaultValues(string tableName) {
			return string.Format(CultureInfo.InvariantCulture, "insert into {0} default values", tableName);
		}
		protected override string FormatConstant(object value) {
			if(value == null) {
				return base.FormatConstant(value);
			}
			TypeCode tc = DXTypeExtensions.GetTypeCode(value.GetType());
			switch(tc) {
				case TypeCode.DateTime:
					DateTime datetimeValue = (DateTime)value;
					string dateTimeFormatPattern = "yyyy-MM-dd HH:mm:ss.fffffff";
					return string.Format("convert(datetime, '{0}', 121)", datetimeValue.ToString(dateTimeFormatPattern, CultureInfo.InvariantCulture));
				default:
					return base.FormatConstant(value);
			}
		}
		public override void GetTableSchema(DBTable table, bool checkIndexes, bool checkForeignKeys) {
			var dbConnection = Connection as System.Data.Common.DbConnection;
			if(dbConnection != null) {
				var oldState = dbConnection.State;
				if(oldState != ConnectionState.Open)
					OpenConnection();
				try {
					var viewSchema = dbConnection.GetSchema("VIEWCOLUMNS", new string[] { null, null, ComposeSafeTableName(table.Name), null });
					if(viewSchema.Rows.Count > 0) {
						foreach(DataRow dataRow in viewSchema.Rows) {
							table.AddColumn(CreateColumnFromSchemaDataRow(dataRow));
						}
						return;
					}
				}
				finally {
					if(oldState != ConnectionState.Open)
						Connection.Close();
				}
			}
			base.GetTableSchema(table, checkIndexes, checkForeignKeys);
		}
		DBColumn CreateColumnFromSchemaDataRow(DataRow dataRow) {
			var columnType = Convert.ToString(dataRow["DATA_TYPE"]);
			var columnSize = Convert.ToInt16(dataRow["CHARACTER_MAXIMUM_LENGTH"]);
			return new DBColumn(Convert.ToString(dataRow["COLUMN_NAME"]), Convert.ToBoolean(dataRow["PRIMARY_KEY"]), columnType, columnSize, GetDBColumnType(columnType, Convert.ToInt16(dataRow["CHARACTER_MAXIMUM_LENGTH"])));
		}
		DBColumnType GetDBColumnType(string columnType, short size) {
			switch(columnType) {
				case "VarChar":
				case "NVarChar":
				case "Text":
				case "NText":
					return DBColumnType.String;
				case "Date":
				case "DateTime":
				case "DateTime2":
					return DBColumnType.DateTime;
				case "Bit":
					return DBColumnType.Boolean;
				case "Char":
				case "NChar":
					return size == 1 ? DBColumnType.Char : DBColumnType.String;
				case "UniqueIdentifier":
					return DBColumnType.Guid;
				case "Money":
				case "SmallMoney":
				case "Decimal":
					return DBColumnType.Decimal;
				case "Float":
					return DBColumnType.Double;
				case "Real":
					return DBColumnType.Single;
				case "TinyInt":
					return DBColumnType.Byte;
				case "SmallInt":
					return DBColumnType.Int16;
				case "Int":
					return DBColumnType.Int32;
				case "BigInt":
					return DBColumnType.Int64;
				case "Image":
				case "Binary":
				case "VarBinary":
					return DBColumnType.ByteArray;
			}
			return DBColumnType.Unknown;
		}
		protected override DbTypeMapperBase CreateDbTypeMapper(Type vistaDbTypeEnumType, Type vistaDbParameterType) {
			return (DbTypeMapperBase)Activator.CreateInstance(typeof(DbTypeMapperVistaDb5<,>).MakeGenericType(vistaDbTypeEnumType, vistaDbParameterType));
		}
	}
	public class VistaDBProviderFactory : ProviderFactory {
		public override IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return VistaDBConnectionProvider.CreateProviderFromConnection(connection, autoCreateOption);
		}
		public override IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			return VistaDBConnectionProvider.CreateProviderFromString(connectionString, autoCreateOption, out objectsToDisposeOnDisconnect);
		}
		public override string GetConnectionString(Dictionary<string, string> parameters) {
			if(!parameters.ContainsKey(DatabaseParamID) || !parameters.ContainsKey(PasswordParamID)) {
				return null;
			}
			return VistaDBConnectionProvider.GetConnectionString(parameters[DatabaseParamID], parameters[PasswordParamID]);
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
		public override string ProviderKey { get { return VistaDBConnectionProvider.XpoProviderTypeString; } }
		public override string[] GetDatabases(string server, string userId, string password) {
			return new string[1] { server };
		}
		public override string FileFilter { get { return "VistaDB 4 databases|*.vdb4"; } }
		public override string DisplayName { get { return "VistaDB 4"; } }
		public override bool MeanSchemaGeneration { get { return true; } }
	}
	public class VistaDB5ProviderFactory : VistaDBProviderFactory {
		public override IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return VistaDB5ConnectionProvider.CreateProviderFromConnection(connection, autoCreateOption);
		}
		public override IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			return VistaDB5ConnectionProvider.CreateProviderFromString(connectionString, autoCreateOption, out objectsToDisposeOnDisconnect);
		}
		public override string GetConnectionString(Dictionary<string, string> parameters) {
			if(!parameters.ContainsKey(DatabaseParamID) || !parameters.ContainsKey(PasswordParamID)) {
				return null;
			}
			return VistaDB5ConnectionProvider.GetConnectionString(parameters[DatabaseParamID], parameters[PasswordParamID]);
		}
		public override string ProviderKey { get { return VistaDB5ConnectionProvider.XpoProviderTypeString; } }
		public override string FileFilter { get { return "VistaDB 5 databases|*.vdb5"; } }
		public override string DisplayName { get { return "VistaDB 5"; } }
	}
	public class VistaDB6ProviderFactory : ProviderFactory {
		public override IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return VistaDB6ConnectionProvider.CreateProviderFromConnection(connection, autoCreateOption);
		}
		public override IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			return VistaDB6ConnectionProvider.CreateProviderFromString(connectionString, autoCreateOption, out objectsToDisposeOnDisconnect);
		}
		public override string GetConnectionString(Dictionary<string, string> parameters) {
			if(!parameters.ContainsKey(DatabaseParamID) || !parameters.ContainsKey(PasswordParamID)) {
				return null;
			}
			return VistaDB6ConnectionProvider.GetConnectionString(parameters[DatabaseParamID], parameters[PasswordParamID]);
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
		public override string[] GetDatabases(string server, string userId, string password) {
			return new string[1] { server };
		}
		public override string ProviderKey { get { return VistaDB6ConnectionProvider.XpoProviderTypeString; } }
		public override string FileFilter { get { return "VistaDB 6 databases|*.vdb6"; } }
		public override string DisplayName { get { return "VistaDB 6"; } }
		public override bool MeanSchemaGeneration { get { return true; } }
	}
}
namespace DevExpress.Xpo.DB.Helpers {
	using System;
	using System.Collections.Generic;
	using System.Data;
	using System.Reflection;
	using DevExpress.Data.Internal;
	public abstract class XPVistaDBDA {
		public abstract object InternalObject { get; }
		public abstract XPVistaDBDatabase CreateDatabase(string fileName, bool stayExclusive, string encryptionKeyString, int pageSize, int LCID, bool caseSensitive);
		public abstract XPVistaDBDatabase OpenDatabase(string fileName, string mode, string encryptionKeyString);
		public abstract int Error_sql_ColumnDoesNotExist { get; }
		public abstract int Error_sql_TableNotExist { get; }
		public abstract int Error_dda_DeleteRow { get; }
		public abstract int Error_dda_CreateRow { get; }
		public abstract int Error_dda_OpenDatabase { get; }
		public abstract string GetConnectionSource(IDbConnection vistaDBConnection);
		public abstract string GetConnectionPassword(IDbConnection vistaDBConnection);
		public abstract string GetConnectionOpenmode(IDbConnection vistaDBConnection);
		public static XPVistaDBDA GetVistaDBDA(Type vistaDBConnectionType) {
			Assembly vistaAssembly = vistaDBConnectionType.Assembly;
			Type vDBEngineType = SafeTypeResolver.GetKnownType(vistaAssembly, "VistaDB.DDA.VistaDBEngine", false);
			Type vDBDA = SafeTypeResolver.GetKnownType(vistaAssembly, "VistaDB.DDA.IVistaDBDDA", false);
			Type vDBDatabase = SafeTypeResolver.GetKnownType(vistaAssembly, "VistaDB.DDA.IVistaDBDatabase", false);
			Type vDBDatabaseOpenMode = SafeTypeResolver.GetKnownType(vistaAssembly, "VistaDB.VistaDBDatabaseOpenMode", false);
			Type xPVistaDBDA = typeof(XPVistaDBDA<,,,,>).MakeGenericType(vDBEngineType, vDBDA, vDBDatabase, vDBDatabaseOpenMode, vistaDBConnectionType);
			return (XPVistaDBDA)Activator.CreateInstance(xPVistaDBDA);
		}
	}
	public class XPVistaDBDA<VDBEngine, VDBDA, VDBDatabase, VDBDatabaseOpenMode, VDBConnection> : XPVistaDBDA {
		static readonly VDBEngine engine;
		static readonly GetDBDAHandler getDBDA;
		static readonly CreateDatabaseHandler createDataBase;
		static readonly OpenDatabaseHandler openDataBase;
		static readonly GetConnectionProperty getConnectionSource;
		static readonly GetConnectionProperty getConnectionPassword;
		static readonly GetConnectionOpenmodeHandler getConnectionOpenmode;
		static readonly CreateXPDatabaseInstanceHandler createXPDatabaseInstance;
		static readonly Dictionary<string, VDBDatabaseOpenMode> vDBDatabaseOpenModeDict = new Dictionary<string, VDBDatabaseOpenMode>();
		static readonly Dictionary<VDBDatabaseOpenMode, string> vDBDatabaseOpenModeStringDict = new Dictionary<VDBDatabaseOpenMode, string>();
		static readonly Type xpVistaDBDatabaseType;
		static readonly int sql_ColumnDoesNotExist;
		static readonly int dda_DeleteRow;
		static readonly int sql_TableNotExist;
		static readonly int dda_CreateRow;
		static readonly int dda_OpenDatabase;
#if DEBUGTEST
		[Data.Tests.IgnoreReflectionUsageDetector]
#endif
		static XPVistaDBDA() {
			FieldInfo fi = typeof(VDBEngine).GetField("Connections");
			engine = (VDBEngine)fi.GetValue(null);
			MethodInfo mi = typeof(VDBEngine).GetMethod("OpenDDA", BindingFlags.Public | BindingFlags.Instance, null, Array.Empty<Type>(), null);
			getDBDA = (GetDBDAHandler)Delegate.CreateDelegate(typeof(GetDBDAHandler), null, mi);
			mi = typeof(VDBDA).GetMethod("CreateDatabase", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string), typeof(bool), typeof(string), typeof(int), typeof(int), typeof(bool) }, null);
			createDataBase = (CreateDatabaseHandler)Delegate.CreateDelegate(typeof(CreateDatabaseHandler), null, mi);
			mi = typeof(VDBDA).GetMethod("OpenDatabase", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string), typeof(VDBDatabaseOpenMode), typeof(string) }, null);
			openDataBase = (OpenDatabaseHandler)Delegate.CreateDelegate(typeof(OpenDatabaseHandler), null, mi);
			Array vDBDatabaseOpenModeValues = Enum.GetValues(typeof(VDBDatabaseOpenMode));
			foreach(VDBDatabaseOpenMode value in vDBDatabaseOpenModeValues) {
				string name = Enum.GetName(typeof(VDBDatabaseOpenMode), value);
				vDBDatabaseOpenModeDict.Add(name, value);
				vDBDatabaseOpenModeStringDict.Add(value, name);
			}
			PropertyInfo pi = typeof(VDBConnection).GetProperty("DataSource", typeof(string));
			mi = pi.GetGetMethod();
			getConnectionSource = (GetConnectionProperty)Delegate.CreateDelegate(typeof(GetConnectionProperty), null, mi);
			pi = typeof(VDBConnection).GetProperty("Password", typeof(string));
			mi = pi.GetGetMethod();
			getConnectionPassword = (GetConnectionProperty)Delegate.CreateDelegate(typeof(GetConnectionProperty), null, mi);
			pi = typeof(VDBConnection).GetProperty("OpenMode", typeof(VDBDatabaseOpenMode));
			mi = pi.GetGetMethod();
			getConnectionOpenmode = (GetConnectionOpenmodeHandler)Delegate.CreateDelegate(typeof(GetConnectionOpenmodeHandler), null, mi);
			Assembly vistaDBAssembly = typeof(VDBEngine).Assembly;
			Type errorsType = SafeTypeResolver.GetKnownType(vistaDBAssembly, "VistaDB.Diagnostic.Errors", false);
			fi = errorsType.GetField("sql_ColumnDoesNotExist");
			sql_ColumnDoesNotExist = (int)fi.GetValue(null);
			fi = errorsType.GetField("dda_DeleteRow");
			dda_DeleteRow = (int)fi.GetValue(null);
			fi = errorsType.GetField("sql_TableNotExist");
			sql_TableNotExist = (int)fi.GetValue(null);
			fi = errorsType.GetField("dda_CreateRow");
			dda_CreateRow = (int)fi.GetValue(null);
			fi = errorsType.GetField("dda_OpenDatabase");
			dda_OpenDatabase = (int)fi.GetValue(null);
			Type vDBTableSchemaType = SafeTypeResolver.GetKnownType(vistaDBAssembly, "VistaDB.DDA.IVistaDBTableSchema", false);
			Type vDBTableType = SafeTypeResolver.GetKnownType(vistaDBAssembly, "VistaDB.DDA.IVistaDBTable", false);
			Type vDBTableNameCollection = SafeTypeResolver.GetKnownType(vistaDBAssembly, "VistaDB.DDA.IVistaDBTableNameCollection", false);
			xpVistaDBDatabaseType = typeof(XPVistaDBDatabase<,,,>).MakeGenericType(typeof(VDBDatabase), vDBTableSchemaType, vDBTableType, vDBTableNameCollection);
			mi = xpVistaDBDatabaseType.GetMethod("CreateInstance", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(VDBDatabase) }, null);
			createXPDatabaseInstance = (CreateXPDatabaseInstanceHandler)Delegate.CreateDelegate(typeof(CreateXPDatabaseInstanceHandler), mi);
		}
		readonly VDBDA vistaDBDA;
		public XPVistaDBDA() {
			vistaDBDA = getDBDA(engine);
		}
		public override object InternalObject {
			get { return vistaDBDA; }
		}
		public override XPVistaDBDatabase CreateDatabase(string fileName, bool stayExclusive, string encryptionKeyString, int pageSize, int LCID, bool caseSensitive) {
			VDBDatabase vistaDBDatabase = createDataBase(vistaDBDA, fileName, stayExclusive, encryptionKeyString, pageSize, LCID, caseSensitive);
			return createXPDatabaseInstance(vistaDBDatabase);
		}
		public override XPVistaDBDatabase OpenDatabase(string fileName, string mode, string encryptionKeyString) {
			VDBDatabaseOpenMode vistaOpenMode;
			if(!vDBDatabaseOpenModeDict.TryGetValue(mode, out vistaOpenMode)) throw new ArgumentException(null, nameof(mode));
			VDBDatabase vistaDBDatabase = openDataBase(vistaDBDA, fileName, vistaOpenMode, encryptionKeyString);
			return createXPDatabaseInstance(vistaDBDatabase);
		}
		public override string GetConnectionOpenmode(IDbConnection vistaDBConnection) {
			string result;
			if(!vDBDatabaseOpenModeStringDict.TryGetValue(getConnectionOpenmode((VDBConnection)vistaDBConnection), out result)) throw new ArgumentException(null, nameof(vistaDBConnection));
			return result;
		}
		public override int Error_dda_CreateRow {
			get { return dda_CreateRow; }
		}
		public override int Error_dda_DeleteRow {
			get { return dda_DeleteRow; }
		}
		public override int Error_sql_ColumnDoesNotExist {
			get { return sql_ColumnDoesNotExist; }
		}
		public override int Error_sql_TableNotExist {
			get { return sql_TableNotExist; }
		}
		public override int Error_dda_OpenDatabase {
			get { return dda_OpenDatabase; }
		}
		public override string GetConnectionPassword(IDbConnection vistaDBConnection) {
			return getConnectionPassword((VDBConnection)vistaDBConnection);
		}
		public override string GetConnectionSource(IDbConnection vistaDBConnection) {
			return getConnectionSource((VDBConnection)vistaDBConnection);
		}
		delegate VDBDatabase CreateDatabaseHandler(VDBDA vistaDBDA, string fileName, bool stayExclusive, string encryptionKeyString, int pageSize, int LCID, bool caseSensitive);
		delegate VDBDatabase OpenDatabaseHandler(VDBDA vistaDBDA, string fileName, VDBDatabaseOpenMode mode, string encryptionKeyString);
		delegate VDBDA GetDBDAHandler(VDBEngine instance);
		delegate string GetConnectionProperty(VDBConnection connection);
		delegate VDBDatabaseOpenMode GetConnectionOpenmodeHandler(VDBConnection connection);
		delegate XPVistaDBDatabase CreateXPDatabaseInstanceHandler(VDBDatabase vistaDatabase);
	}
	public class XPVistaDBDatabase<VDBDatabase, VDBTableSchema, VDBTable, VDBTableNameCollection> : XPVistaDBDatabase {
		static readonly AlterTableHandler alterTable;
		static readonly CreateTableHandler createTable;
		static readonly DropTableHandler dropTable;
		static readonly GetTableNamesHandler getTableNames;
		static readonly NewTableHandler newTable;
		static readonly OpenTableHandler openTable;
		static readonly TableSchemaHandler tableSchema;
		static readonly CloseHandler close;
		static readonly Type xpVistaDBTableType;
		static readonly Type xpVistaDBTableSchemaType;
		static readonly CreateXPTableSchemaInstanceHandler createXPSchemaInstance;
		static readonly CreateXPTableInstanceHandler createXPTableInstance;
		static XPVistaDBDatabase() {
			Type vDBDatabaseType = typeof(VDBDatabase);
			MethodInfo mi = vDBDatabaseType.GetMethod("AlterTable", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string), typeof(VDBTableSchema) }, null);
			alterTable = (AlterTableHandler)Delegate.CreateDelegate(typeof(AlterTableHandler), null, mi);
			mi = vDBDatabaseType.GetMethod("CreateTable", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(VDBTableSchema), typeof(bool), typeof(bool) }, null);
			createTable = (CreateTableHandler)Delegate.CreateDelegate(typeof(CreateTableHandler), null, mi);
			mi = vDBDatabaseType.GetMethod("DropTable", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string) }, null);
			dropTable = (DropTableHandler)Delegate.CreateDelegate(typeof(DropTableHandler), null, mi);
			mi = vDBDatabaseType.GetMethod("GetTableNames", BindingFlags.Public | BindingFlags.Instance, null, Array.Empty<Type>(), null);
			getTableNames = (GetTableNamesHandler)Delegate.CreateDelegate(typeof(GetTableNamesHandler), null, mi);
			mi = vDBDatabaseType.GetMethod("NewTable", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string) }, null);
			newTable = (NewTableHandler)Delegate.CreateDelegate(typeof(NewTableHandler), null, mi);
			mi = vDBDatabaseType.GetMethod("OpenTable", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string), typeof(bool), typeof(bool) }, null);
			openTable = (OpenTableHandler)Delegate.CreateDelegate(typeof(OpenTableHandler), null, mi);
			mi = vDBDatabaseType.GetMethod("TableSchema", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string) }, null);
			tableSchema = (TableSchemaHandler)Delegate.CreateDelegate(typeof(TableSchemaHandler), null, mi);
			mi = typeof(VDBTable).GetMethod("Close", BindingFlags.Public | BindingFlags.Instance, null, Array.Empty<Type>(), null);
			close = (CloseHandler)Delegate.CreateDelegate(typeof(CloseHandler), null, mi);
			Assembly vistaDBAssembly = vDBDatabaseType.Assembly;
			Type vDBReferentialIntegrityType = SafeTypeResolver.GetKnownType(vistaDBAssembly, "VistaDB.DDA.VistaDBReferentialIntegrity", false);
			xpVistaDBTableType = typeof(XPVistaDBTable<,>).MakeGenericType(typeof(VDBTable), vDBReferentialIntegrityType);
			mi = xpVistaDBTableType.GetMethod("CreateInstance", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(VDBTable) }, null);
			createXPTableInstance = (CreateXPTableInstanceHandler)Delegate.CreateDelegate(typeof(CreateXPTableInstanceHandler), mi);
			Type vDBCA = SafeTypeResolver.GetKnownType(vistaDBAssembly, "VistaDB.DDA.IVistaDBColumnAttributes", false);
			Type vDBII = SafeTypeResolver.GetKnownType(vistaDBAssembly, "VistaDB.DDA.IVistaDBIndexInformation", false);
			Type vDBIIC = SafeTypeResolver.GetKnownType(vistaDBAssembly, "VistaDB.DDA.IVistaDBIndexCollection", false);
			Type vDBRI = SafeTypeResolver.GetKnownType(vistaDBAssembly, "VistaDB.DDA.IVistaDBRelationshipInformation", false);
			Type vDBRIC = SafeTypeResolver.GetKnownType(vistaDBAssembly, "VistaDB.DDA.IVistaDBRelationshipCollection", false);
			Type vDBIDI = SafeTypeResolver.GetKnownType(vistaDBAssembly, "VistaDB.DDA.IVistaDBIdentityInformation", false);
			Type vDBIDIC = SafeTypeResolver.GetKnownType(vistaDBAssembly, "VistaDB.DDA.IVistaDBIdentityCollection", false);
			Type vDBType = SafeTypeResolver.GetKnownType(vistaDBAssembly, "VistaDB.VistaDBType", false);
			Type vDBDVI = SafeTypeResolver.GetKnownType(vistaDBAssembly, "VistaDB.DDA.IVistaDBDefaultValueInformation", false);
			Type vDBDVIC = SafeTypeResolver.GetKnownType(vistaDBAssembly, "VistaDB.DDA.IVistaDBDefaultValueCollection", false);
			xpVistaDBTableSchemaType = typeof(XPVistaDBTableSchema<,,,,,,,,,,>).MakeGenericType(typeof(VDBTableSchema), vDBType, vDBCA, vDBII, vDBRI, vDBIDI, vDBIIC, vDBRIC, vDBIDIC, vDBDVI, vDBDVIC);
			mi = xpVistaDBTableSchemaType.GetMethod("CreateInstance", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(VDBTableSchema) }, null);
			createXPSchemaInstance = (CreateXPTableSchemaInstanceHandler)Delegate.CreateDelegate(typeof(CreateXPTableSchemaInstanceHandler), mi);
		}
		public static XPVistaDBDatabase CreateInstance(VDBDatabase vistaDatabase) {
			return new XPVistaDBDatabase<VDBDatabase, VDBTableSchema, VDBTable, VDBTableNameCollection>(vistaDatabase);
		}
		readonly VDBDatabase vistaDatabase;
		public XPVistaDBDatabase(VDBDatabase vistaDatabase) {
			this.vistaDatabase = vistaDatabase;
		}
		public override object InternalObject {
			get { return vistaDatabase; }
		}
		public override void AlterTable(string oldName, XPVistaDBTableSchema schema) {
			VDBTableSchema vistaTableSchema = (VDBTableSchema)schema.InternalObject;
			alterTable(vistaDatabase, oldName, vistaTableSchema);
		}
		public override XPVistaDBTable CreateTable(XPVistaDBTableSchema schema, bool exclusive, bool readOnly) {
			VDBTableSchema vistaTableSchema = (VDBTableSchema)schema.InternalObject;
			VDBTable vistaTable = createTable(vistaDatabase, vistaTableSchema, exclusive, readOnly);
			return createXPTableInstance(vistaTable);
		}
		public override void DropTable(string name) {
			dropTable(vistaDatabase, name);
		}
		public override IEnumerable<string> GetTableNames() {
			return (IEnumerable<string>)getTableNames(vistaDatabase);
		}
		public override XPVistaDBTableSchema NewTable(string name) {
			VDBTableSchema vistaTableSchema = newTable(vistaDatabase, name);
			return createXPSchemaInstance(vistaTableSchema);
		}
		public override XPVistaDBTable OpenTable(string name, bool exclusive, bool readOnly) {
			VDBTable vistaTable = openTable(vistaDatabase, name, exclusive, readOnly);
			return createXPTableInstance(vistaTable);
		}
		public override XPVistaDBTableSchema TableSchema(string name) {
			VDBTableSchema vistaTableSchema = tableSchema(vistaDatabase, name);
			return createXPSchemaInstance(vistaTableSchema);
		}
		public override void Close() {
			close(vistaDatabase);
		}
		delegate void AlterTableHandler(VDBDatabase vistaDatabase, string oldName, VDBTableSchema schema);
		delegate VDBTable CreateTableHandler(VDBDatabase vistaDatabase, VDBTableSchema schema, bool exclusive, bool readOnly);
		delegate void DropTableHandler(VDBDatabase vistaDatabase, string name);
		delegate VDBTableNameCollection GetTableNamesHandler(VDBDatabase vistaDatabase);
		delegate VDBTableSchema NewTableHandler(VDBDatabase vistaDatabase, string name);
		delegate VDBTable OpenTableHandler(VDBDatabase vistaDatabase, string name, bool exclusive, bool readOnly);
		delegate VDBTableSchema TableSchemaHandler(VDBDatabase vistaDatabase, string name);
		delegate void CloseHandler(VDBDatabase vistaDatabase);
		delegate XPVistaDBTableSchema CreateXPTableSchemaInstanceHandler(VDBTableSchema schema);
		delegate XPVistaDBTable CreateXPTableInstanceHandler(VDBTable schema);
	}
	public abstract class XPVistaDBDatabase {
		public abstract object InternalObject { get; }
		public abstract IEnumerable<string> GetTableNames();
		public abstract XPVistaDBTableSchema NewTable(string name);
		public abstract XPVistaDBTableSchema TableSchema(string name);
		public abstract void AlterTable(string oldName, XPVistaDBTableSchema schema);
		public abstract XPVistaDBTable CreateTable(XPVistaDBTableSchema schema, bool exclusive, bool readOnly);
		public abstract XPVistaDBTable OpenTable(string name, bool exclusive, bool readOnly);
		public abstract void DropTable(string name);
		public abstract void Close();
	}
	public class XPVistaDBTableSchema<VDBTableSchema, VistaDbType, VDBCA, VDBII, VDBRI, VDBIDI, VDBIIC, VDBRIC, VDBIDIC, VDBDVI, VDBDVIC> : XPVistaDBTableSchema {
		static readonly AddColumnHandler addColumn;
		static readonly DefineColumnAttributesHandler4 defineColumnAttributes4;
		static readonly DefineColumnAttributesHandler5 defineColumnAttributes5;
		static readonly DefineIdentityHandler defineIdentity;
		static readonly DefineDefaultValueHandler defineDefaultValue;
		static readonly DefaultValuesHandler getDefaultValues;
		static readonly IndexInfoHandler getIndeces;
		static readonly IdentityInfoHandler getIdentities;
		static readonly RelationInfoHandler getForeignKeys;
		static readonly Dictionary<string, VistaDbType> vistaDBTypeDict = new Dictionary<string, VistaDbType>();
		static XPVistaDBTableSchema() {
			Array vistaDBTypeValues = Enum.GetValues(typeof(VistaDbType));
			foreach(VistaDbType value in vistaDBTypeValues) {
				vistaDBTypeDict.Add(Enum.GetName(typeof(VistaDbType), value), value);
			}
			Type vDBTableSchemaType = typeof(VDBTableSchema);
			MethodInfo mi = vDBTableSchemaType.GetMethod("AddColumn", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string), typeof(VistaDbType), typeof(int), typeof(int) }, null);
			addColumn = (AddColumnHandler)Delegate.CreateDelegate(typeof(AddColumnHandler), null, mi);
			mi = vDBTableSchemaType.GetMethod("DefineColumnAttributes", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(string) }, null);
			if(mi != null) {
				defineColumnAttributes5 = (DefineColumnAttributesHandler5)Delegate.CreateDelegate(typeof(DefineColumnAttributesHandler5), null, mi);
			}
			else {
				mi = vDBTableSchemaType.GetMethod("DefineColumnAttributes", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(string), typeof(string) }, null);
				defineColumnAttributes4 = (DefineColumnAttributesHandler4)Delegate.CreateDelegate(typeof(DefineColumnAttributesHandler4), null, mi);
			}
			mi = vDBTableSchemaType.GetMethod("DefineIdentity", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string), typeof(string), typeof(string) }, null);
			defineIdentity = (DefineIdentityHandler)Delegate.CreateDelegate(typeof(DefineIdentityHandler), null, mi);
			mi = vDBTableSchemaType.GetMethod("DefineDefaultValue", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string), typeof(string), typeof(bool), typeof(string) }, null);
			defineDefaultValue = (DefineDefaultValueHandler)Delegate.CreateDelegate(typeof(DefineDefaultValueHandler), null, mi);
			PropertyInfo pi = vDBTableSchemaType.GetProperty("Indexes", typeof(VDBIIC));
			mi = pi.GetGetMethod();
			getIndeces = (IndexInfoHandler)Delegate.CreateDelegate(typeof(IndexInfoHandler), null, mi);
			pi = vDBTableSchemaType.GetProperty("DefaultValues", typeof(VDBDVIC));
			mi = pi.GetGetMethod();
			getDefaultValues = (DefaultValuesHandler)Delegate.CreateDelegate(typeof(DefaultValuesHandler), null, mi);
			pi = vDBTableSchemaType.GetProperty("Identities", typeof(VDBIDIC));
			mi = pi.GetGetMethod();
			getIdentities = (IdentityInfoHandler)Delegate.CreateDelegate(typeof(IdentityInfoHandler), null, mi);
			pi = vDBTableSchemaType.GetProperty("ForeignKeys", typeof(VDBRIC));
			mi = pi.GetGetMethod();
			getForeignKeys = (RelationInfoHandler)Delegate.CreateDelegate(typeof(RelationInfoHandler), null, mi);
		}
		public static XPVistaDBTableSchema CreateInstance(VDBTableSchema vistaTableSchema) {
			return new XPVistaDBTableSchema<VDBTableSchema, VistaDbType, VDBCA, VDBII, VDBRI, VDBIDI, VDBIIC, VDBRIC, VDBIDIC, VDBDVI, VDBDVIC>(vistaTableSchema);
		}
		readonly VDBTableSchema vistaTableSchema;
		public XPVistaDBTableSchema(VDBTableSchema vistaTableSchema) {
			this.vistaTableSchema = vistaTableSchema;
		}
		public override XPVistaDBColumnAttributes AddColumn(string name, string vistaDbTypeString, int maxLen, int codePage) {
			VistaDbType vistaDbType;
			if(!vistaDBTypeDict.TryGetValue(vistaDbTypeString, out vistaDbType)) throw new ArgumentException(null, nameof(vistaDbTypeString));
			return new XPVistaDBColumnAttributes<VDBCA>(addColumn(vistaTableSchema, name, vistaDbType, maxLen, codePage));
		}
		public override XPVistaDBColumnAttributes DefineColumnAttributes(string name, bool allowNull, bool readOnly, bool encrypted, bool packed, string description) {
			if(defineColumnAttributes5 != null) {
				return new XPVistaDBColumnAttributes<VDBCA>(defineColumnAttributes5(vistaTableSchema, name, allowNull, readOnly, encrypted, packed, description));
			}
			else {
				return new XPVistaDBColumnAttributes<VDBCA>(defineColumnAttributes4(vistaTableSchema, name, allowNull, readOnly, encrypted, packed, "", description));
			}
		}
		public override void DefineIdentity(string columnName, string seedValue, string stepExpression) {
			defineIdentity(vistaTableSchema, columnName, seedValue, stepExpression);
		}
		public override void DefineDefaultValue(string columnName, string scriptExpression, bool useInUpdate, string description) {
			defineDefaultValue(vistaTableSchema, columnName, scriptExpression, useInUpdate, description);
		}
		public override Dictionary<string, XPVistaDBDefaultValueInformation> GetDefaultValues() {
			IEnumerable<VDBDVI> values = (IEnumerable<VDBDVI>)getDefaultValues(vistaTableSchema);
			if(values == null) return null;
			var result = new Dictionary<string, XPVistaDBDefaultValueInformation>();
			foreach(VDBDVI val in values) {
				var vdbdvi = new XPVistaDBDefaultValueInformation<VDBDVI>(val);
				result[vdbdvi.ColumnName] = vdbdvi;
			}
			return result;
		}
		public override List<XPVistaDBColumnAttributes> GetColumns() {
			IEnumerable<VDBCA> vistaColumns = (IEnumerable<VDBCA>)vistaTableSchema;
			List<XPVistaDBColumnAttributes> result = new List<XPVistaDBColumnAttributes>();
			foreach(VDBCA columnAttributes in vistaColumns) {
				result.Add(new XPVistaDBColumnAttributes<VDBCA>(columnAttributes));
			}
			return result;
		}
		public override List<XPVistaDBIndexInformation> GetIndices() {
			IEnumerable<VDBII> vistaIIC = (IEnumerable<VDBII>)getIndeces(vistaTableSchema);
			if(vistaIIC == null) return null;
			List<XPVistaDBIndexInformation> result = new List<XPVistaDBIndexInformation>();
			foreach(VDBII index in vistaIIC) {
				result.Add(new XPVistaDBIndexInformation<VDBII>(index));
			}
			return result;
		}
		public override List<XPVistaDBRelationshipInformation> GetForeignKeys() {
			IEnumerable<VDBRI> vistaRIC = (IEnumerable<VDBRI>)getForeignKeys(vistaTableSchema);
			if(vistaRIC == null) return null;
			List<XPVistaDBRelationshipInformation> result = new List<XPVistaDBRelationshipInformation>();
			foreach(VDBRI fk in vistaRIC) {
				result.Add(new XPVistaDBRelationshipInformation<VDBRI>(fk));
			}
			return result;
		}
		public override List<XPVistaDBIdentityInformation> GetIdentities() {
			IEnumerable<VDBIDI> vistaRIC = (IEnumerable<VDBIDI>)getIdentities(vistaTableSchema);
			if(vistaRIC == null) return null;
			List<XPVistaDBIdentityInformation> result = new List<XPVistaDBIdentityInformation>();
			foreach(VDBIDI identity in vistaRIC) {
				result.Add(new XPVistaDBIdentityInformation<VDBIDI>(identity));
			}
			return result;
		}
		public override object InternalObject {
			get { return vistaTableSchema; }
		}
		delegate VDBCA AddColumnHandler(VDBTableSchema vistaTableSchema, string name, VistaDbType vistaDbType, int maxLen, int codePage);
		delegate VDBCA DefineColumnAttributesHandler5(VDBTableSchema vistaTableSchema, string name, bool allowNull, bool readOnly, bool encrypted, bool packed, string description);
		delegate VDBCA DefineColumnAttributesHandler4(VDBTableSchema vistaTableSchema, string name, bool allowNull, bool readOnly, bool encrypted, bool packed, string caption, string description);
		delegate void DefineIdentityHandler(VDBTableSchema vistaTableSchema, string columnName, string seedValue, string stepExpression);
		delegate void DefineDefaultValueHandler(VDBTableSchema vistaTableSchema, string columnName, string scriptExpression, bool useInUpdate, string description);
		delegate VDBDVIC DefaultValuesHandler(VDBTableSchema vistaTableSchema);
		delegate VDBIIC IndexInfoHandler(VDBTableSchema vistaTableSchema);
		delegate VDBIDIC IdentityInfoHandler(VDBTableSchema vistaTableSchema);
		delegate VDBRIC RelationInfoHandler(VDBTableSchema vistaTableSchema);
	}
	public abstract class XPVistaDBTableSchema {
		public abstract object InternalObject { get; }
		public abstract XPVistaDBColumnAttributes AddColumn(string name, string vistaDbType, int maxLen, int codePage);
		public abstract XPVistaDBColumnAttributes DefineColumnAttributes(string name, bool allowNull, bool readOnly, bool encrypted, bool packed, string description);
		public abstract void DefineIdentity(string columnName, string seedValue, string stepExpression);
		public abstract void DefineDefaultValue(string columnName, string scriptExpression, bool useInUpdate, string description);
		public abstract List<XPVistaDBColumnAttributes> GetColumns();
		public abstract List<XPVistaDBIndexInformation> GetIndices();
		public abstract Dictionary<string, XPVistaDBDefaultValueInformation> GetDefaultValues();
		public abstract List<XPVistaDBRelationshipInformation> GetForeignKeys();
		public abstract List<XPVistaDBIdentityInformation> GetIdentities();
	}
	public class XPVistaDBColumnAttributes<VDBCA> : XPVistaDBColumnAttributes {
		static readonly Int32Handler maxLength;
		static readonly StringHandler name;
		static readonly TypeHandler systemType;
		static readonly BoolHandler allowNull;
		static XPVistaDBColumnAttributes() {
			Assembly vistaAssembly = typeof(VDBCA).Assembly;
			Type vdbcType = SafeTypeResolver.GetKnownType(vistaAssembly, "VistaDB.DDA.IVistaDBColumn", false);
			Type vdbvType = SafeTypeResolver.GetKnownType(vistaAssembly, "VistaDB.IVistaDBValue", false);
			PropertyInfo pi = vdbcType.GetProperty("MaxLength", typeof(int));
			MethodInfo mi = pi.GetGetMethod();
			maxLength = (Int32Handler)Delegate.CreateDelegate(typeof(Int32Handler), null, mi);
			pi = vdbcType.GetProperty("Name", typeof(string));
			mi = pi.GetGetMethod();
			name = (StringHandler)Delegate.CreateDelegate(typeof(StringHandler), null, mi);
			pi = vdbvType.GetProperty("SystemType", typeof(Type));
			mi = pi.GetGetMethod();
			systemType = (TypeHandler)Delegate.CreateDelegate(typeof(TypeHandler), null, mi);
			pi = vdbcType.GetProperty("AllowNull", typeof(bool));
			mi = pi.GetGetMethod();
			allowNull = (BoolHandler)Delegate.CreateDelegate(typeof(BoolHandler), null, mi);
		}
		readonly VDBCA vistaCA;
		public XPVistaDBColumnAttributes(VDBCA vistaCA) {
			this.vistaCA = vistaCA;
		}
		public override int MaxLength {
			get { return maxLength(vistaCA); }
		}
		public override string Name {
			get { return name(vistaCA); }
		}
		public override Type SystemType {
			get { return systemType(vistaCA); }
		}
		public override bool AllowNull {
			get { return allowNull(vistaCA); }
		}
		delegate int Int32Handler(VDBCA vistaCA);
		delegate string StringHandler(VDBCA vistaCA);
		delegate Type TypeHandler(VDBCA vistaCA);
		delegate bool BoolHandler(VDBCA vistaCA);
	}
	public abstract class XPVistaDBColumnAttributes {
		public abstract string Name { get; }
		public abstract int MaxLength { get; }
		public abstract Type SystemType { get; }
		public abstract bool AllowNull { get; }
	}
	public class XPVistaDBDefaultValueInformation<VDBDVI> : XPVistaDBDefaultValueInformation {
		static readonly StringHandler getColumnName;
		static readonly StringHandler getExpression;
		static readonly BoolHandler getUseInUpdate;
		static XPVistaDBDefaultValueInformation() {
			Type vdbdviType = typeof(VDBDVI);
			PropertyInfo pi = vdbdviType.GetProperty("ColumnName", typeof(string));
			MethodInfo mi = pi.GetGetMethod();
			getColumnName = (StringHandler)Delegate.CreateDelegate(typeof(StringHandler), null, mi);
			pi = vdbdviType.GetProperty("Expression", typeof(string));
			mi = pi.GetGetMethod();
			getExpression = (StringHandler)Delegate.CreateDelegate(typeof(StringHandler), null, mi);
			pi = vdbdviType.GetProperty("UseInUpdate", typeof(bool));
			mi = pi.GetGetMethod();
			getUseInUpdate = (BoolHandler)Delegate.CreateDelegate(typeof(BoolHandler), null, mi);
		}
		readonly VDBDVI vistaDVI;
		public XPVistaDBDefaultValueInformation(VDBDVI vistaDVI) {
			this.vistaDVI = vistaDVI;
		}
		public override string ColumnName {
			get {
				return getColumnName(vistaDVI);
			}
		}
		public override string Expression {
			get {
				return getExpression(vistaDVI);
			}
		}
		public override bool UseInUpdate {
			get {
				return getUseInUpdate(vistaDVI);
			}
		}
		delegate string StringHandler(VDBDVI vistaDVI);
		delegate bool BoolHandler(VDBDVI vistaDVI);
	}
	public abstract class XPVistaDBDefaultValueInformation {
		public abstract string ColumnName { get; }
		public abstract string Expression { get; }
		public abstract bool UseInUpdate { get; }
	}
	public class XPVistaDBIndexInformation<VDBII> : XPVistaDBIndexInformation {
		static readonly StringHandler name;
		static readonly StringHandler keyExpression;
		static readonly BoolHandler primary;
		static readonly BoolHandler unique;
		static XPVistaDBIndexInformation() {
			Type vdbcaType = typeof(VDBII);
			PropertyInfo pi = vdbcaType.GetProperty("KeyExpression", typeof(string));
			MethodInfo mi = pi.GetGetMethod();
			keyExpression = (StringHandler)Delegate.CreateDelegate(typeof(StringHandler), null, mi);
			pi = vdbcaType.GetProperty("Name", typeof(string));
			mi = pi.GetGetMethod();
			name = (StringHandler)Delegate.CreateDelegate(typeof(StringHandler), null, mi);
			pi = vdbcaType.GetProperty("Primary", typeof(bool));
			mi = pi.GetGetMethod();
			primary = (BoolHandler)Delegate.CreateDelegate(typeof(BoolHandler), null, mi);
			pi = vdbcaType.GetProperty("Unique", typeof(bool));
			mi = pi.GetGetMethod();
			unique = (BoolHandler)Delegate.CreateDelegate(typeof(BoolHandler), null, mi);
		}
		readonly VDBII vistaII;
		public XPVistaDBIndexInformation(VDBII vistaII) {
			this.vistaII = vistaII;
		}
		public override string Name {
			get { return name(vistaII); }
		}
		public override string KeyExpression {
			get { return keyExpression(vistaII); }
		}
		public override bool Primary {
			get { return primary(vistaII); }
		}
		public override bool Unique {
			get { return unique(vistaII); }
		}
		delegate string StringHandler(VDBII vistaII);
		delegate bool BoolHandler(VDBII vistaII);
	}
	public abstract class XPVistaDBIndexInformation {
		public abstract bool Primary { get; }
		public abstract string Name { get; }
		public abstract string KeyExpression { get; }
		public abstract bool Unique { get; }
	}
	public class XPVistaDBRelationshipInformation<VDBRI> : XPVistaDBRelationshipInformation {
		static readonly StringHandler primaryTable;
		static readonly StringHandler foreignKey;
		static readonly StringHandler name;
		static XPVistaDBRelationshipInformation() {
			Type vdbcaType = typeof(VDBRI);
			PropertyInfo pi = vdbcaType.GetProperty("PrimaryTable", typeof(string));
			MethodInfo mi = pi.GetGetMethod();
			primaryTable = (StringHandler)Delegate.CreateDelegate(typeof(StringHandler), null, mi);
			pi = vdbcaType.GetProperty("ForeignKey", typeof(string));
			mi = pi.GetGetMethod();
			foreignKey = (StringHandler)Delegate.CreateDelegate(typeof(StringHandler), null, mi);
			pi = vdbcaType.GetProperty("Name", typeof(string));
			mi = pi.GetGetMethod();
			name = (StringHandler)Delegate.CreateDelegate(typeof(StringHandler), null, mi);
		}
		readonly VDBRI vistaRI;
		public XPVistaDBRelationshipInformation(VDBRI vistaRI) {
			this.vistaRI = vistaRI;
		}
		public override string Name {
			get { return name(vistaRI); }
		}
		public override string PrimaryTable {
			get { return primaryTable(vistaRI); }
		}
		public override string ForeignKey {
			get { return foreignKey(vistaRI); }
		}
		delegate string StringHandler(VDBRI vistaRI);
	}
	public abstract class XPVistaDBRelationshipInformation {
		public abstract string Name { get; }
		public abstract string PrimaryTable { get; }
		public abstract string ForeignKey { get; }
	}
	public class XPVistaDBIdentityInformation<VBIDI> : XPVistaDBIdentityInformation {
		static readonly StringHandler columnName;
		static XPVistaDBIdentityInformation() {
			Type vdbcaType = typeof(VBIDI);
			PropertyInfo pi = vdbcaType.GetProperty("ColumnName", typeof(string));
			MethodInfo mi = pi.GetGetMethod();
			columnName = (StringHandler)Delegate.CreateDelegate(typeof(StringHandler), null, mi);
		}
		readonly VBIDI vistaIDI;
		public XPVistaDBIdentityInformation(VBIDI vistaIDI) {
			this.vistaIDI = vistaIDI;
		}
		public override string ColumnName {
			get { return columnName(vistaIDI); }
		}
		delegate string StringHandler(VBIDI vistaIDI);
	}
	public abstract class XPVistaDBIdentityInformation {
		public abstract string ColumnName { get; }
	}
	public class XPVistaDBTable<VDBTable, VDBReferentialIntegrity> : XPVistaDBTable {
		static readonly Dictionary<string, VDBReferentialIntegrity> vistaDBRIDict = new Dictionary<string, VDBReferentialIntegrity>();
		static readonly CloseHandler close;
		static readonly CreateForeignKeyHandler createForeignKey;
		static readonly CreateIndexHandler createIndex;
		static readonly DropForeignKeyHandler dropForeignKey;
		static XPVistaDBTable() {
			Array vistaValues = Enum.GetValues(typeof(VDBReferentialIntegrity));
			foreach(VDBReferentialIntegrity value in vistaValues) {
				vistaDBRIDict.Add(Enum.GetName(typeof(VDBReferentialIntegrity), value), value);
			}
			Type vDBTableType = typeof(VDBTable);
			MethodInfo mi = vDBTableType.GetMethod("Close", BindingFlags.Public | BindingFlags.Instance, null, Array.Empty<Type>(), null);
			close = (CloseHandler)Delegate.CreateDelegate(typeof(CloseHandler), null, mi);
			mi = vDBTableType.GetMethod("CreateForeignKey", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string), typeof(string), typeof(string), typeof(VDBReferentialIntegrity), typeof(VDBReferentialIntegrity), typeof(string) }, null);
			createForeignKey = (CreateForeignKeyHandler)Delegate.CreateDelegate(typeof(CreateForeignKeyHandler), null, mi);
			mi = vDBTableType.GetMethod("CreateIndex", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string), typeof(string), typeof(bool), typeof(bool) }, null);
			createIndex = (CreateIndexHandler)Delegate.CreateDelegate(typeof(CreateIndexHandler), null, mi);
			mi = vDBTableType.GetMethod("DropForeignKey", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string) }, null);
			dropForeignKey = (DropForeignKeyHandler)Delegate.CreateDelegate(typeof(DropForeignKeyHandler), null, mi);
		}
		public static XPVistaDBTable CreateInstance(VDBTable vistaTable) {
			return new XPVistaDBTable<VDBTable, VDBReferentialIntegrity>(vistaTable);
		}
		readonly VDBTable vistaTable;
		public XPVistaDBTable(VDBTable vistaTable) {
			this.vistaTable = vistaTable;
		}
		public override void Close() {
			close(vistaTable);
		}
		public override void CreateForeignKey(string constraintName, string foreignKey, string primaryTable, string updateIntegrityStr, string deleteIntegrityStr, string description) {
			VDBReferentialIntegrity updateIntegrity;
			VDBReferentialIntegrity deleteIntegrity;
			if(!vistaDBRIDict.TryGetValue(updateIntegrityStr, out updateIntegrity) || !vistaDBRIDict.TryGetValue(deleteIntegrityStr, out deleteIntegrity))
				throw new ArgumentException("updateIntegrityStr or deleteIntegrityStr not exist in vistaDBRIDict");
			createForeignKey(vistaTable, constraintName, foreignKey, primaryTable, updateIntegrity, deleteIntegrity, description);
		}
		public override void CreateIndex(string name, string keyExpression, bool primary, bool unique) {
			createIndex(vistaTable, name, keyExpression, primary, unique);
		}
		public override void DropForeignKey(string constraintName) {
			dropForeignKey(vistaTable, constraintName);
		}
		public override object InternalObject {
			get { return vistaTable; }
		}
		delegate void CloseHandler(VDBTable vistaTable);
		delegate void CreateForeignKeyHandler(VDBTable vistaTable, string constraintName, string foreignKey, string primaryTable, VDBReferentialIntegrity updateIntegrity, VDBReferentialIntegrity deleteIntegrity, string description);
		delegate void CreateIndexHandler(VDBTable vistaTable, string name, string keyExpression, bool primary, bool unique);
		delegate void DropForeignKeyHandler(VDBTable vistaTable, string constraintName);
	}
	public abstract class XPVistaDBTable {
		public abstract object InternalObject { get; }
		public abstract void CreateIndex(string name, string keyExpression, bool primary, bool unique);
		public abstract void CreateForeignKey(string constraintName, string foreignKey, string primaryTable, string updateIntegrity, string deleteIntegrity, string description);
		public abstract void DropForeignKey(string constraintName);
		public abstract void Close();
	}
	interface IDbTypeMapperVistaDb {
		string MaxStringDataType { get; }
		string MaxBinaryDataType { get; }
		string SingleDataType { get; }
		int MaxStringSize { get; }
		int MaxBinarySize { get; }
		string GetDBTypeString(DBColumn column, out short size);
	}
	abstract class DbTypeMapperVistaDbBase<TSqlDbTypeEnum, TSqlParameter> : DbTypeMapper<TSqlDbTypeEnum, TSqlParameter>, IDbTypeMapperVistaDb
		where TSqlDbTypeEnum : struct
		where TSqlParameter : IDbDataParameter {
		readonly TSqlDbTypeEnum maxStringDbType;
		readonly TSqlDbTypeEnum maxBinaryDbType;
		protected override string ParameterDbTypePropertyName { get { return "VistaDBType"; } }
		public abstract string MaxStringDataType { get; }
		public abstract string MaxBinaryDataType { get; }
		public abstract string SingleDataType { get; }
		public abstract int MaxStringSize { get; }
		public abstract int MaxBinarySize { get; }
		public DbTypeMapperVistaDbBase() {
			maxStringDbType = (TSqlDbTypeEnum)(Enum.Parse(typeof(TSqlDbTypeEnum), MaxStringDataType));
			maxBinaryDbType = (TSqlDbTypeEnum)(Enum.Parse(typeof(TSqlDbTypeEnum), MaxBinaryDataType));
		}
		protected override string GetParameterTypeNameForBoolean(out int? size, out byte? precision, out byte? scale) {
			size = null;
			precision = scale = null;
			return "Bit";
		}
		protected override string GetParameterTypeNameForByte(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Int";
		}
		protected override string GetParameterTypeNameForByteArray(out int? size) {
			size = null;
			return "VarBinary";
		}
		protected override string GetParameterTypeNameForChar(out int? size) {
			size = 1;
			return "NChar";
		}
		protected override string GetParameterTypeNameForDateTime() {
			return "DateTime";
		}
		protected override string GetParameterTypeNameForDecimal(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Decimal";
		}
		protected override string GetParameterTypeNameForDouble(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Float";
		}
		protected override string GetParameterTypeNameForSingle(out byte? precision, out byte? scale) {
			precision = scale = null;
			return SingleDataType;
		}
		protected override string GetParameterTypeNameForGuid(out int? size) {
			size = null;
			return "UniqueIdentifier";
		}
		protected override string GetParameterTypeNameForInt16(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "SmallInt";
		}
		protected override string GetParameterTypeNameForInt32(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Int";
		}
		protected override string GetParameterTypeNameForInt64(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "BigInt";
		}
		protected override string GetParameterTypeNameForSByte(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "SmallInt";
		}
		protected override string GetParameterTypeNameForString(out int? size) {
			size = null;
			return "NVarChar";
		}
		protected override string GetParameterTypeNameForTimeSpan() {
			return "Float";
		}
		protected override string GetParameterTypeNameForUInt16(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Int";
		}
		protected override string GetParameterTypeNameForUInt32(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "BigInt";
		}
		protected override string GetParameterTypeNameForUInt64(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "BigInt";
		}
		protected override string GetParameterTypeNameForDateOnly(out int? size) {
			size = null;
			return "DateTime";
		}
		protected override string GetParameterTypeNameForTimeOnly(out int? size) {
			size = null;
			return "DateTime";
		}
		protected override string ConvertSqlTypeToParameterType(string sqlType) {
			return null;
		}
		public override void SetParameterTypeAndSize(IDbDataParameter parameter, DBColumnType dbColumnType, int size) {
			if(dbColumnType == DBColumnType.String) {
				if(size <= 0 || size > MaxStringSize) {
					SetSqlDbTypeHandler((TSqlParameter)parameter, maxStringDbType);
					return;
				}
			}
			else if(dbColumnType == DBColumnType.ByteArray) {
				if(size <= 0 || size > MaxBinarySize) {
					SetSqlDbTypeHandler((TSqlParameter)parameter, maxBinaryDbType);
					return;
				}
			}
			base.SetParameterTypeAndSize(parameter, dbColumnType, size);
		}
		public string GetDBTypeString(DBColumn column, out short size) {
			size = 0;
			switch(column.ColumnType) {
				case DBColumnType.String:
					size = (short)(column.Size <= 0 ? 0 : column.Size);
					return column.Size <= 0 || column.Size > MaxStringSize ? MaxStringDataType : "NVarChar";
				case DBColumnType.DateTime:
					return "DateTime";
				case DBColumnType.Boolean:
					return "Bit";
				case DBColumnType.Char:
					size = 1;
					return "NChar";
				case DBColumnType.Guid:
					return "UniqueIdentifier";
				case DBColumnType.Decimal:
					return "Decimal";
				case DBColumnType.Double:
					return "Float";
				case DBColumnType.Single:
					return SingleDataType;
				case DBColumnType.UInt64:
					return "BigInt";
				case DBColumnType.UInt32:
					return "BigInt";
				case DBColumnType.Int16:
					return "SmallInt";
				case DBColumnType.SByte:
					return "SmallInt";
				case DBColumnType.Int64:
					return "BigInt";
				case DBColumnType.ByteArray:
					size = (short)(column.Size <= 0 ? 0 : column.Size);
					return column.Size <= 0 || column.Size > MaxBinarySize ? MaxBinaryDataType : "VarBinary";
			}
			return "Int";
		}
	}
	class DbTypeMapperVistaDb<TSqlDbTypeEnum, TSqlParameter> : DbTypeMapperVistaDbBase<TSqlDbTypeEnum, TSqlParameter>
		where TSqlDbTypeEnum : struct
		where TSqlParameter : IDbDataParameter {
		public override string MaxStringDataType { get { return "NText"; } }
		public override string MaxBinaryDataType { get { return "Image"; } }
		public override string SingleDataType { get { return "Float"; } }
		public override int MaxStringSize { get { return 8000; } }
		public override int MaxBinarySize { get { return Int16.MaxValue; } }
	}
	class DbTypeMapperVistaDb5<TSqlDbTypeEnum, TSqlParameter> : DbTypeMapperVistaDbBase<TSqlDbTypeEnum, TSqlParameter>
		where TSqlDbTypeEnum : struct
		where TSqlParameter : IDbDataParameter {
		public override string MaxStringDataType { get { return "NText"; } }
		public override string MaxBinaryDataType { get { return "Image"; } }
		public override string SingleDataType { get { return "Real"; } }
		public override int MaxStringSize { get { return 8000; } }
		public override int MaxBinarySize { get { return 8000; } }
	}
}
