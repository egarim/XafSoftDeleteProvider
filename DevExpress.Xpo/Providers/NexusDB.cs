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
	using System.Data.Common;
	using System.Reflection;
	using DevExpress.Data.Helpers;
	using System.Threading;
	using System.Threading.Tasks;
#pragma warning disable DX0024
	public class NexusDBConnectionProvider : ConnectionProviderSql {
		public const string XpoProviderTypeString = "NexusDBV2";
		public static string GetConnectionString(string server, string userId, string password, string database) {
			return String.Format("{4}={5};Server={0};User Id={1};Password={2};Database={3};Encoding=UNICODE;",
				server, userId, password, database, DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString);
		}
		public static string GetConnectionString(string server, int port, string userId, string password, string database) {
			return String.Format("{5}={6};Server={0};User Id={1};Password={2};Database={3};Encoding=UNICODE;Port={4}",
				server, userId, password, database, port, DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString);
		}
		public static IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			IDbConnection connection = CreateConnection(connectionString);
			objectsToDisposeOnDisconnect = new IDisposable[] { connection };
			return CreateProviderFromConnection(connection, autoCreateOption);
		}
		public static IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return new NexusDBConnectionProvider(connection, autoCreateOption);
		}
		static NexusDBConnectionProvider() {
			RegisterDataStoreProvider(XpoProviderTypeString, new DataStoreCreationFromStringDelegate(CreateProviderFromString));
			RegisterDataStoreProvider("NxConnection", new DataStoreCreationFromConnectionDelegate(CreateProviderFromConnection));
			RegisterFactory(new NexusDBProviderFactory());
		}
		public static void Register() { }
		public NexusDBConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption)
			: base(connection, autoCreateOption, true) {
		}
		protected override string GetSqlCreateColumnTypeForBoolean(DBTable table, DBColumn column) {
			return "bool";
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
			return "decimal(28,8)";
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
				return "nvarchar(" + column.Size.ToString(CultureInfo.InvariantCulture) + ')';
			else
				return "text";
		}
		protected override string GetSqlCreateColumnTypeForDateTime(DBTable table, DBColumn column) {
			return "timestamp";
		}
		protected override string GetSqlCreateColumnTypeForGuid(DBTable table, DBColumn column) {
			return "GUID";
		}
		protected override string GetSqlCreateColumnTypeForByteArray(DBTable table, DBColumn column) {
			return "blob";
		}
		public override string GetSqlCreateColumnFullAttributes(DBTable table, DBColumn column) {
			if(column.IsKey && column.IsIdentity && column.ColumnType == DBColumnType.Int32 && IsSingleColumnPKColumn(table, column))
				return "AUTOINC PRIMARY KEY";
			string result = GetSqlCreateColumnType(table, column);
			if(column.IsKey)
				result += " NOT NULL";
			return result;
		}
		protected override object ConvertToDbParameter(object clientValue, TypeCode clientValueTypeCode) {
			switch(clientValueTypeCode) {
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
			}
			return base.ConvertToDbParameter(clientValue, clientValueTypeCode);
		}
		protected override Int64 GetIdentity(InsertStatement root, TaggedParametersHolder identitiesByTag) {
			Query sql = new InsertSqlGenerator(this, identitiesByTag, new Dictionary<OperandValue, string>()).GenerateSql(root);
			object value = GetScalar(new Query(sql.Sql + ";select LASTAUTOINC from #dummy", sql.Parameters, sql.ParametersNames));
			long id = ((IConvertible)value).ToInt64(CultureInfo.InvariantCulture);
			return id;
		}
		protected override async Task<Int64> GetIdentityAsync(InsertStatement root, TaggedParametersHolder identitiesByTag, AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken) {
			Query sql = new InsertSqlGenerator(this, identitiesByTag, new Dictionary<OperandValue, string>()).GenerateSql(root);
			object value = await GetScalarAsync(new Query(sql.Sql + ";select LASTAUTOINC from #dummy", sql.Parameters, sql.ParametersNames), asyncOperationId, cancellationToken).ConfigureAwait(false);
			long id = ((IConvertible)value).ToInt64(CultureInfo.InvariantCulture);
			return id;
		}
		protected override IDbConnection CreateConnection() {
			return CreateConnection(ConnectionString);
		}
		static public IDbConnection CreateConnection(string connectionString) {
			return ReflectConnectionHelper.GetConnection("NexusDB.ADOProvider", "NexusDB.ADOProvider.NxConnection", connectionString); ;
		}
		protected override IDataParameter CreateParameter(IDbCommand command, object value, string name, DBColumnType dbType, string dbTypeName, int size) {
			IDataParameter param = CreateParameter(command);
			param.Value = value;
			if (name[0]!=':')
				param.ParameterName = name;
			else
				param.ParameterName = name.Substring(1);
			if (param.DbType == DbType.AnsiString)
				param.DbType = DbType.String;
			if(value is DateTime)
				param.DbType = DbType.DateTime;
			if(value is byte[])
				param.DbType = DbType.Binary;
			if(value is Guid)
				param.DbType = DbType.Guid;
			return param;
		}
		public override void CreateTable(DBTable table) {
			string columns = string.Empty;
			foreach(DBColumn col in table.Columns) {
				if(columns.Length > 0)
					columns += ", ";
				columns += (FormatColumnSafe(col.Name) + ' ' + GetSqlCreateColumnFullAttributes(table, col));
			}
			bool transactionStopped = false;
			if (Transaction != null)
			{
				CommitTransaction();
				transactionStopped = true;
			}
			ExecuteSqlSchemaUpdate("Table", table.Name, string.Empty, String.Format(CultureInfo.InvariantCulture,
				"create table {0} ({1})",
				FormatTableSafe(table), columns));
			if (transactionStopped)
			{
				BeginTransaction();
			}
		}
		public override void CreateIndex(DBTable table, DBIndex index)
		{
			StringCollection formattedColumns = new StringCollection();
			for (Int32 i = 0; i < index.Columns.Count; ++i)
				formattedColumns.Add(FormatColumnSafe(index.Columns[i]));
			bool transactionStopped = false;
			if (Transaction != null)
			{
				CommitTransaction();
				transactionStopped = true;
			}
			ExecuteSqlSchemaUpdate("Index", GetIndexName(index, table), table.Name, String.Format(CultureInfo.InvariantCulture,
				CreateIndexTemplate,
				index.IsUnique ? "unique" : string.Empty, FormatConstraintSafe(GetIndexName(index, table)), FormatTableSafe(table), StringListHelper.DelimitedText(formattedColumns, ",")));
			if (transactionStopped)
			{
				BeginTransaction();
			}
		}
		protected override void CreateDataBase()
		{
			Connection.Open();
			IDbCommand aCommand = Connection.CreateCommand();
			aCommand.CommandText = "#opt::SESSION::NONSTRICTASSIGNMENT='1'\n\r";
			aCommand.ExecuteNonQuery();
		}
		delegate bool TablesFilter(DBTable table);
		SelectStatementResult GetDataForTables(ICollection tables, TablesFilter filter, string queryText) {
			QueryParameterCollection parameters = new QueryParameterCollection();
			StringCollection inList = new StringCollection();
			int i = 0;
			foreach(DBTable table in tables) {
				if(filter == null || filter(table)) {
					parameters.Add(new OperandValue(ComposeSafeTableName(table.Name)));
					inList.Add(":p"+i.ToString());
					++i;
				}
			}
			if(inList.Count == 0)
				return new SelectStatementResult();
			return SelectData(new Query(string.Format(CultureInfo.InvariantCulture, queryText, StringListHelper.DelimitedText(inList, ",")), parameters, inList));
		}
		public static DBColumnType TypeFromString(string enmNxType, ref int size)
		{
			switch (enmNxType)
			{
				case "nxtBoolean": return DBColumnType.Boolean;
				case "nxtChar": return DBColumnType.Char;
				case "nxtWideChar": return DBColumnType.Char;
				case "nxtByte": return DBColumnType.Byte;
				case "nxtWord16": return DBColumnType.UInt16;
				case "nxtWord32": return DBColumnType.UInt32;
				case "nxtInt8": return DBColumnType.SByte;
				case "nxtInt16": return DBColumnType.Int16;
				case "nxtInt32": return DBColumnType.Int32;
				case "nxtInt64": return DBColumnType.Int64;
				case "nxtAutoInc": return DBColumnType.UInt32;
				case "nxtSingle": return DBColumnType.Single;
				case "nxtDouble": return DBColumnType.Double;
				case "nxtExtended": return DBColumnType.Decimal;
				case "nxtCurrency": return DBColumnType.Double;
				case "nxtDate": return DBColumnType.DateTime;
				case "nxtTime": return DBColumnType.DateTime;
				case "nxtDateTime": return DBColumnType.DateTime;
				case "nxtInterval": return DBColumnType.Double;
				case "nxtBLOB": return DBColumnType.ByteArray;
			case "nxtBLOBMemo":
				size = -1;
				return DBColumnType.String;
			case "nxtBLOBWideMemo":
				size = -1;
				return DBColumnType.String;
			case "nxtBLOBGraphic":
				return DBColumnType.ByteArray;
				case "nxtByteArray": return DBColumnType.ByteArray;
				case "nxtShortString": return DBColumnType.String;
				case "nxtNullString": return DBColumnType.String;
				case "nxtNullStringFixed": return DBColumnType.String;
			case "nxtWideStringFixed":
				size /= 2;
				return DBColumnType.String;
			case "nxtWideString":
				size /= 2;
				return DBColumnType.String;
				case "nxtRecRev": return DBColumnType.UInt32;
				case "nxtBCD": return DBColumnType.Decimal;
				case "nxtGUID": return DBColumnType.Guid;
			}
			return DBColumnType.Unknown;
		}
		void GetColumns(DBTable table) {
			SelectStatementResult result = SelectData(new Query(
				"select FIELD_NAME, FIELD_TYPE_NEXUS, FIELD_LENGTH from #fields where lower(TABLE_NAME)=lower('"+ ComposeSafeTableName(table.Name)+"')"));
			foreach (SelectStatementResultRow row in result.Rows)
				{
				int size = Convert.ToInt32(row.Values[2]);
				DBColumnType type = TypeFromString((string)row.Values[1], ref size);
				size--;
				table.AddColumn(new DBColumn((string)row.Values[0], false, String.Empty, type == DBColumnType.String ? size : 0, type));
			}
		}
		void GetPrimaryKey(DBTable table) 
		{
		  SelectStatementResult data = SelectData(new Query(
				@"SELECT 
                    Segment_Field, 
                    Segment_Index, 
                    i.INDEX_NAME, 
                    i.TABLE_NAME, 
                    COALESCE(i.CONSTRAINT_NAME, I.INDEX_NAME) CONSTRAINT_NAME 
                  from 
                    #indices i, 
                    #indexfields f 
                  where
                    i.table_name=:p0 
                    and INDEX_ISDEFAULT = true 
                    and Index_AllowsDups <> 'YES' 
                    and i.table_name=f.table_name 
                    and i.index_index = f.Index_index;"
				, new QueryParameterCollection(new OperandValue(ComposeSafeTableName(table.Name))), new string[] { "p0" }));
			if(data.Rows.Length > 0) {
				StringCollection cols = new StringCollection();
				for(int i = 0; i < data.Rows.Length; i++)
					cols.Add((string)data.Rows[i].Values[0]);
				table.PrimaryKey = new DBPrimaryKey(table.Name, cols);
			}
 		}
		void GetIndexes(DBTable table) {
			SelectStatementResult data = SelectData(new Query(
				@"select 
                    i.INDEX_NAME, 
                    Segment_Field, 
                    Segment_Index,  
                    Index_AllowsDups
                  from 
                    #indexes i, 
                    #indexfields f
                  where 
                    i.table_name=:p0 
                    and i.table_name=f.table_name 
                    and i.index_index = f.Index_index
                  order by      
                    index_name, 
                    segment_index"
				, new QueryParameterCollection(new OperandValue(ComposeSafeTableName(table.Name))), new string[] { "p0" }));
			DBIndex index = null;
			foreach(SelectStatementResultRow row in data.Rows) {
				if(0 == Convert.ToInt32(row.Values[2])) 
				{
					StringCollection list = new StringCollection();
					list.Add((string)row.Values[1]);
					index = new DBIndex((string)row.Values[0], list, ((string)row.Values[3]=="YES")); 
					table.Indexes.Add(index);
				} else
					index.Columns.Add((string)row.Values[1]);
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
			foreach(SelectStatementResultRow row in GetDataForTables(tables, null, 
				@"select 
                    TABLE_NAME 
                  from 
                    #tables t 
                  where 
                    t.TABLE_NAME in({0})"
				).Rows)
				dbTables.Add(row.Values[0], false);
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
			return 63;
		}
		protected override int GetSafeNameColumnMaxLength() {
			return 63;
		}
		protected override int GetSafeNameConstraintMaxLength() {
			return 63;
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
			string modificatorsSql = string.Format(CultureInfo.InvariantCulture, (topSelectedRecords != 0) ? " TOP {0} " : string.Empty, topSelectedRecords); 
			string expandedWhereSql = whereSql != null ? string.Format(CultureInfo.InvariantCulture, "{0}where {1}", Environment.NewLine, whereSql) : string.Empty;
			string expandedOrderBySql = orderBySql != null ? string.Format(CultureInfo.InvariantCulture, "{0}order by {1}", Environment.NewLine, orderBySql) : string.Empty;
			string expandedHavingSql = havingSql != null ? string.Format(CultureInfo.InvariantCulture, "{0}having {1}", Environment.NewLine, havingSql) : string.Empty;
			string expandedGroupBySql = groupBySql != null ? string.Format(CultureInfo.InvariantCulture, "{0}group by {1}", Environment.NewLine, groupBySql) : string.Empty;
			string result = string.Format(CultureInfo.InvariantCulture, "select {0} {1} from {2}{3}{4}{5}{6}", modificatorsSql, selectedPropertiesSql, fromSql, expandedWhereSql, expandedGroupBySql, expandedHavingSql, expandedOrderBySql);
			return result;
		}
		public override string FormatInsertDefaultValues(string tableName) {
			return string.Format(CultureInfo.InvariantCulture, "insert into {0} values (default)", tableName);
		}
		public override string FormatInsert(string tableName, string fields, string values) {
			return string.Format(CultureInfo.InvariantCulture, "insert into {0}({1}) values ({2})",
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
				default:
					return base.FormatBinary(operatorType, leftOperand, rightOperand);
			}
		}
		public override string GetParameterName(OperandValue parameter, int index, ref bool createParameter) {
			return ":p"+index.ToString();
		}
		public override string FormatConstraint(string constraintName) {
			return string.Format(CultureInfo.InvariantCulture, "\"{0}\"", ComposeSafeConstraintName(constraintName));
		}
		protected override void ProcessClearDatabase() {
			IDbCommand command = CreateCommand();
			ClearDatabase(command);
		}
		public override string[] GetStorageTablesList(bool includeViews) {
			SelectStatementResult tables = SelectData(new Query("select TABLE_NAME from #tables"));
			ArrayList result = new ArrayList(tables.Rows.Length);
			foreach(SelectStatementResultRow row in tables.Rows) {
				result.Add(row.Values[0]);
			}
			return (string[])result.ToArray(typeof(string));
		}
		public override void CreatePrimaryKey(DBTable table)
		{
			StringCollection formattedColumns = new StringCollection();
			for (Int32 i = 0; i < table.PrimaryKey.Columns.Count; ++i)
				formattedColumns.Add(FormatColumnSafe(table.PrimaryKey.Columns[i]));
			bool transactionStopped = false;
			if (Transaction != null)
			{
				CommitTransaction();
				transactionStopped = true;
			}
			ExecuteSqlSchemaUpdate("PrimaryKey", GetPrimaryKeyName(table.PrimaryKey, table), table.Name, String.Format(CultureInfo.InvariantCulture,
				"alter table {0} add constraint {1} primary key ({2})",
				FormatTableSafe(table), FormatConstraintSafe(GetPrimaryKeyName(table.PrimaryKey, table)), StringListHelper.DelimitedText(formattedColumns, ",")));
			if (transactionStopped)
			{
				BeginTransaction();
			}
		}
		public override void CreateColumn(DBTable table, DBColumn column)
		{
			bool transactionStopped = false;
			if (Transaction != null)
			{
				CommitTransaction();
				transactionStopped = true;
			}
			ExecuteSqlSchemaUpdate("Column", column.Name, table.Name, String.Format(CultureInfo.InvariantCulture,
				"alter table {0} add {1} {2}",
				FormatTableSafe(table), FormatColumnSafe(column.Name), GetSqlCreateColumnFullAttributes(table, column)));
			if (transactionStopped)
			{
				BeginTransaction();
			}
		}
		public override void CreateForeignKey(DBTable table, DBForeignKey fk)
		{
			StringCollection formattedColumns = new StringCollection();
			for (Int32 i = 0; i < fk.Columns.Count; ++i)
				formattedColumns.Add(FormatColumnSafe(fk.Columns[i]));
			StringCollection formattedRefColumns = new StringCollection();
			for (Int32 i = 0; i < fk.PrimaryKeyTableKeyColumns.Count; ++i)
				formattedRefColumns.Add(FormatColumnSafe(fk.PrimaryKeyTableKeyColumns[i]));
			bool transactionStopped = false;
			if (Transaction != null)
			{
				CommitTransaction();
				transactionStopped = true;
			}
			ExecuteSqlSchemaUpdate("ForeignKey", GetForeignKeyName(fk, table), table.Name, String.Format(CultureInfo.InvariantCulture,
				CreateForeignKeyTemplate,
				FormatTableSafe(table),
				FormatConstraintSafe(GetForeignKeyName(fk, table)),
				StringListHelper.DelimitedText(formattedColumns, ","),
				FormatTable(ComposeSafeSchemaName(fk.PrimaryKeyTable), ComposeSafeTableName(fk.PrimaryKeyTable)),
				StringListHelper.DelimitedText(formattedRefColumns, ",")));
			if (transactionStopped)
			{
				BeginTransaction();
			}
		}
		public void ClearDatabase(IDbCommand command) {
			SelectStatementResult constraints = SelectData(new Query(@"SELECT fk.FK_CONSTRAINT_NAME, fk.fk_Constraint_table_name from #foreignkey_constraints fk where fk.FK_CONSTRAINT_NAME<>''"));
			foreach(SelectStatementResultRow row in constraints.Rows) {
				command.CommandText = "alter table \"" + ((string)row.Values[1]).Trim() + "\" drop constraint \"" + ((string)row.Values[0]).Trim() + "\"";
				command.ExecuteNonQuery();
			}
			SelectStatementResult views = SelectData(new Query(@"select VIEW_NAME from #views"));
			foreach(SelectStatementResultRow row in views.Rows) {
				command.CommandText = "drop view \"" + ((string)row.Values[0]).Trim() + "\"";
				command.ExecuteNonQuery();
			}
			foreach(string table in GetStorageTablesList(false)) {
				command.CommandText = "drop table \"" + table + "\"";
				command.ExecuteNonQuery();
			}
		}
		void GetForeignKeys(DBTable table)
			{
							 SelectStatementResult data = SelectData(new Query(
								@"         
        SELECT distinct                                                
          fc.FK_CONSTRAINT_REFERENCING_COLUMNS_INDEX ColumnIndex,      
          fc.FK_CONSTRAINT_REFERENCING_COLUMNS_NAME FKColumnName,      
          pc.FK_CONSTRAINT_REFERENCED_COLUMNS_NAME PKColumnName,       
          fk.fk_Constraint_References_table_name PKTABLE,              

          fk.FK_CONSTRAINT_NAME FKNAME,                                
          fk.FK_CONSTRAINT_REFERENCES_CONSTRAINT_NAME PKNAME,          
          fk.fk_constraint_table_name FKTABLE,                         
          fk.FK_CONSTRAINT_UPDATE_RULE UpdateRule,                     
          fk.FK_CONSTRAINT_DELETE_RULE DeleteRule                      
        from                                                           
          #foreignkey_constraints fk,                                  
          #foreignkey_constraints_referenced_columns pc,               
          #foreignkey_constraints_referencing_columns fc               
        where                                                          
          fk.FK_CONSTRAINT_NAME<>'' 
          and fk.FK_CONSTRAINT_NAME=pc.FK_CONSTRAINT_NAME 
          and fk.FK_CONSTRAINT_NAME=fc.FK_CONSTRAINT_NAME 
          and fc.FK_CONSTRAINT_REFERENCING_COLUMNS_INDEX = pc.FK_CONSTRAINT_REFERENCED_COLUMNS_INDEX                 
          and LOWER(fk.fk_Constraint_table_name)=lower(:p0)
        order by                                                       
          FKNAME, FKTABLE, PKTABLE, ColumnIndex                        
                            ",
							new QueryParameterCollection(new OperandValue(ComposeSafeTableName(table.Name))), new string[] { ":p0" }));
							DBForeignKey fk = null;
							foreach(SelectStatementResultRow row in data.Rows) {
								if((int)row.Values[0] == 0) {
									StringCollection pkc = new StringCollection();
									StringCollection fkc = new StringCollection();
									pkc.Add((string)row.Values[2]);
									fkc.Add((string)row.Values[1]);
									fk = new DBForeignKey(fkc, (string)row.Values[3], pkc);
									table.ForeignKeys.Add(fk);
								} else {
									fk.Columns.Add((string)row.Values[1]);
									fk.PrimaryKeyTableKeyColumns.Add((string)row.Values[2]);
								}
							}
			}
			protected override Exception WrapException(Exception e, IDbCommand query) {
				if(e.Message.Contains("Unknown column:") || e.Message.Contains("Unable to resolve the identifier") || e.Message.Contains("Unable to open table"))
					return new SchemaCorrectionNeededException(e.Message, e);
				if(e.Message.Contains("Referential Integrity Violation.") || e.Message.Contains("Key violation."))
					return new ConstraintViolationException(query.CommandText, GetParametersString(query), e);
				return base.WrapException(e, query);
			}
		protected override void CommandBuilderDeriveParameters(IDbCommand command) {
			throw new NotImplementedException();
		}
	}
#pragma warning restore DX0024
	public class NexusDBProviderFactory : ProviderFactory {
		public override IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return NexusDBConnectionProvider.CreateProviderFromConnection(connection, autoCreateOption);
		}
		public override IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			return NexusDBConnectionProvider.CreateProviderFromString(connectionString, autoCreateOption, out objectsToDisposeOnDisconnect);
		}
		public override string GetConnectionString(Dictionary<string, string> parameters) {
			if(!parameters.ContainsKey(ServerParamID) || !parameters.ContainsKey(DatabaseParamID) || !parameters.ContainsKey(UserIDParamID) || !parameters.ContainsKey(PasswordParamID)) {
				return null;
			}
			return NexusDBConnectionProvider.GetConnectionString(parameters[ServerParamID], parameters[UserIDParamID],
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
		public override string ProviderKey { get { return NexusDBConnectionProvider.XpoProviderTypeString; } }
		public override string[] GetDatabases(string server, string userId, string password) {
#pragma warning disable DX0004
			Type toolsType = Xpo.Helpers.XPTypeActivator.GetType("NexusDB.ADOProvider", "NexusDB.ADOProvider.NxProviderTools", false);
			Type connectionType = Xpo.Helpers.XPTypeActivator.GetType("NexusDB.ADOProvider", "NexusDB.ADOProvider.NxConnectionType", false);
#pragma warning restore DX0004
			if(toolsType == null || connectionType == null)
				return Array.Empty<string>();
			string connection = string.Format("Server={0};UserID={1};password={2}", ConnectionProviderSql.EscapeConnectionStringArgument(server), ConnectionProviderSql.EscapeConnectionStringArgument(userId), ConnectionProviderSql.EscapeConnectionStringArgument(password));
			MethodInfo getdbs = toolsType.GetMethod("GetDatabases", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public, null, new Type[] { connectionType, typeof(string) }, null);
			if(getdbs == null)
				return Array.Empty<string>();
			return (string[])getdbs.Invoke(null, new object[] { Enum.Parse(connectionType, "nxctTCPDirect"), connection});
		}
		public override string FileFilter { get { return null; } }
		public override bool MeanSchemaGeneration { get { return true; } }
	}
}
