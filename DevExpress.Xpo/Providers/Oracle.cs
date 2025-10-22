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

using System.Data;
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
	using DevExpress.Xpo.Helpers;
	using DevExpress.Data.Db;
	using DevExpress.Xpo.Logger;
	using DevExpress.Data.Helpers;
	using System.Threading;
	using System.Threading.Tasks;
	public abstract class BaseOracleConnectionProvider : ConnectionProviderSql {
		public BaseOracleConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption)
			: base(connection, autoCreateOption, true) {
			PrepareDelegates();
			ObjectsOwner = GetCurrentUser();
			SysUsersAvailable = CheckSysUsers();
			ReadDbCharset();
		}
		protected BaseOracleConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption, bool openConnection)
			: base(connection, autoCreateOption, openConnection) {
			PrepareDelegates();
		}
		UpdateSchemaSqlFormatterHelper updateSchemaSqlFormatter;
		protected override UpdateSchemaSqlFormatterHelper UpdateSchemaFormatter {
			get {
				if(updateSchemaSqlFormatter == null) {
					updateSchemaSqlFormatter = new OracleUpdateSchemaSqlFormatterHelper(this, GetSqlCreateColumnFullAttributes, FormatConstraintSafe, GetIndexName, GetForeignKeyName, GetPrimaryKeyName, GetSeqName);
				}
				return updateSchemaSqlFormatter;
			}
		}
		protected override DBSchemaComparerSql CreateSchemaComparer() {
			var comparer = new DBSchemaComparerSql(ComposeSafeTableName, ComposeSafeColumnName, GetSqlCreateColumnType, DbTypeMapper.ParseSqlType) {
				NeedsIndexForForeignKey = NeedsIndexForForeignKey,
				NeedsSetColumnNotNullableBeforeCreatePrimaryKey = false
			};
			comparer.AddCompatibleSqlTypeMapping("int", "number");
			comparer.AddCompatibleSqlTypeMapping("double precision", "float");
			comparer.AddCompatibleSqlTypeMapping("numeric(10,0)", "number(10,0)");
			return comparer;
		}
		protected virtual void PrepareDelegates() { }
		protected override string GetSqlCreateColumnTypeForBoolean(DBTable table, DBColumn column) {
			return "number(1,0)";
		}
		protected override string GetSqlCreateColumnTypeForByte(DBTable table, DBColumn column) {
			return "number(3,0)";
		}
		protected override string GetSqlCreateColumnTypeForSByte(DBTable table, DBColumn column) {
			return "number(3,0)";
		}
		protected override string GetSqlCreateColumnTypeForChar(DBTable table, DBColumn column) {
			return "nchar";
		}
		protected override string GetSqlCreateColumnTypeForDecimal(DBTable table, DBColumn column) {
			return "number(19,5)";
		}
		protected override string GetSqlCreateColumnTypeForDouble(DBTable table, DBColumn column) {
			return "double precision";
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
			return "number(5,0)";
		}
		protected override string GetSqlCreateColumnTypeForUInt16(DBTable table, DBColumn column) {
			return "number(5,0)";
		}
		protected override string GetSqlCreateColumnTypeForInt64(DBTable table, DBColumn column) {
			return "number(20,0)";
		}
		protected override string GetSqlCreateColumnTypeForUInt64(DBTable table, DBColumn column) {
			return "number(20,0)";
		}
		public const int MaximumStringSize = 2000;
		protected override string GetSqlCreateColumnTypeForString(DBTable table, DBColumn column) {
			if (column.Size > 0 && column.Size <= MaximumStringSize)
				return "nvarchar2(" + column.Size.ToString(CultureInfo.InvariantCulture) + ')';
			else
				return "nclob";
		}
		protected override string GetSqlCreateColumnTypeForDateTime(DBTable table, DBColumn column) {
			return "date";
		}
		protected override string GetSqlCreateColumnTypeForDateOnly(DBTable table, DBColumn column) {
			return "date";
		}
		protected override string GetSqlCreateColumnTypeForTimeOnly(DBTable table, DBColumn column) {
			return "date";
		}
		protected override string GetSqlCreateColumnTypeForGuid(DBTable table, DBColumn column) {
			return "char(36)";
		}
		public const int MaximumBinarySize = 2000;
		protected override string GetSqlCreateColumnTypeForByteArray(DBTable table, DBColumn column) {
			if (column.Size > 0 && column.Size < MaximumBinarySize)
				return "raw(" + column.Size.ToString(CultureInfo.InvariantCulture) + ')';
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
			if(column.IsKey && column.IsIdentity && (column.ColumnType == DBColumnType.Int32 || column.ColumnType == DBColumnType.Int64) && IsSingleColumnPKColumn(table, column)) {
				return "number PRIMARY KEY";
			}
			result = GetSqlCreateColumnType(table, column);
			if(!string.IsNullOrEmpty(column.DbDefaultValue)) {
				result += string.Concat(" DEFAULT ", column.DbDefaultValue);
			} else {
				if(column.DefaultValue != null && column.DefaultValue != System.DBNull.Value) {
					string formattedDefaultValue = OracleFormatterHelper.FormatConstant(column.DefaultValue);
					result += string.Concat(" DEFAULT ", formattedDefaultValue);
				}
			}
			if(column.IsKey || !column.IsNullable) {
				result += " NOT NULL";
			}
			return result;
		}
		class IdentityInsertSqlGenerator : BaseObjectSqlGenerator {
			readonly string SeqName;
			readonly string IdentityParameterName;
			protected override string InternalGenerateSql() {
				bool hasIdentityParameter = !string.IsNullOrEmpty(IdentityParameterName);
				StringBuilder names = new StringBuilder();
				StringBuilder values = new StringBuilder();
				for (int i = 0; i < Root.Operands.Count; i++) {
					names.Append(Process(Root.Operands[i]));
					names.Append(',');
					values.Append(GetNextParameterName(((InsertStatement)Root).Parameters[i]));
					values.Append(',');
				}
				names.AppendFormat(Process(new QueryOperand(((InsertStatement)Root).IdentityColumn, Root.Alias)));
				names.Append(',');
				if(hasIdentityParameter) {
					values.Append(IdentityParameterName);
					values.Append(',');
				} else {
					values.AppendFormat(CultureInfo.InvariantCulture, "{0}.nextval,", SeqName);
				}
				string sql = formatter.FormatInsert(formatter.FormatTable(formatter.ComposeSafeSchemaName(Root.Table.Name), formatter.ComposeSafeTableName(Root.Table.Name)),
					names.ToString(0, names.Length - 1),
					values.ToString(0, values.Length - 1));
				if (hasIdentityParameter)
					sql = string.Format(CultureInfo.InvariantCulture, "select {0}.nextval into {1} from DUAL;{2};\n", SeqName, IdentityParameterName, sql);
				return sql;
			}
			public IdentityInsertSqlGenerator(ISqlGeneratorFormatter formatter, string seqName, string identityParameterName, TaggedParametersHolder identitiesByTag, Dictionary<OperandValue, string> parameters)
				: base(formatter, identitiesByTag, parameters) {
				this.SeqName = seqName;
				this.IdentityParameterName = identityParameterName;
			}
		}
		protected override Int64 GetIdentity(InsertStatement root, TaggedParametersHolder identitiesByTag) {
			string seq = GetSeqName(root.Table.Name);
			Query sql = new IdentityInsertSqlGenerator(this, seq, null, identitiesByTag, new Dictionary<OperandValue, string>()).GenerateSql(root);
			ExecSql(sql);
			object value = GetScalar(new Query(string.Format(CultureInfo.InvariantCulture, "select {0}.currval from DUAL", seq)));
			long id = ((IConvertible)value).ToInt64(CultureInfo.InvariantCulture);
			return id;
		}
		protected override async Task<Int64> GetIdentityAsync(InsertStatement root, TaggedParametersHolder identitiesByTag, AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken) {
			string seq = GetSeqName(root.Table.Name);
			Query sql = new IdentityInsertSqlGenerator(this, seq, null, identitiesByTag, new Dictionary<OperandValue, string>()).GenerateSql(root);
			await ExecSqlAsync(sql, asyncOperationId, cancellationToken).ConfigureAwait(false);
			object value = await GetScalarAsync(new Query(string.Format(CultureInfo.InvariantCulture, "select {0}.currval from DUAL", seq)), asyncOperationId, cancellationToken).ConfigureAwait(false);
			long id = ((IConvertible)value).ToInt64(CultureInfo.InvariantCulture);
			return id;
		}
		IDataParameter ExecPrepareReturnParameter(IDbCommand command) {
			IDataParameter ret = command.CreateParameter();
			ret.DbType = DbType.Int32;
			ret.Direction = ParameterDirection.Output;
			ret.ParameterName = ":n";
			command.Parameters.Add(ret);
			command.CommandText = "declare\nbegin\n" + command.CommandText + ret.ParameterName + " := 1; end;";
			Trace.WriteLineIf(xpoSwitch.TraceInfo, new DbCommandTracer(command));
			return ret;
		}
		bool ExecProcessReturnParameter(IDbCommand command, Dictionary<OperandValue, string> parameters, IDataParameter returnParameter) {
			int rowsAffected = (int)returnParameter.Value;
			foreach(KeyValuePair<OperandValue, string> entry in parameters) {
				IDbDataParameter p = (IDbDataParameter)command.Parameters[entry.Value];
				if(p.Direction != ParameterDirection.Output)
					continue;
				((ParameterValue)entry.Key).Value = p.Value;
			}
			return rowsAffected != 0;
		}
		bool Exec(IDbCommand command, Dictionary<OperandValue, string> parameters) {
			try {
				IDataParameter ret = ExecPrepareReturnParameter(command);
				LogManager.Log(LogManager.LogCategorySQL, () => {
					command.ExecuteNonQuery();
				}, (d) => {
					return LogMessage.CreateMessage(this, command, d);
				}, null);
				return ExecProcessReturnParameter(command, parameters, ret);
			} catch (Exception e) {
				if(IsConnectionBroken(e)) {
					CloseConnectionInternal();
				}
				throw WrapException(e, command);
			}
		}
		async Task<bool> ExecAsync(IDbCommand command, Dictionary<OperandValue, string> parameters, AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken) {
			try {
				IDataParameter ret = ExecPrepareReturnParameter(command);
				await LogManager.LogAsync(LogManager.LogCategorySQL, () => {
					return CommandExecuteNonQueryAsync(command, asyncOperationId, cancellationToken);
				}, (d) => {
					return LogMessage.CreateMessage(this, command, d);
				}, null).ConfigureAwait(false);
				return ExecProcessReturnParameter(command, parameters, ret);
			} catch(Exception e) {
				if(IsConnectionBroken(e)) {
					CloseConnectionInternal();
				}
				throw WrapException(e, command);
			}
		}
		bool HasLob(BaseStatement query) {
			QueryParameterCollection parameters = null;
			if (query is InsertStatement)
				parameters = ((InsertStatement)query).Parameters;
			if (query is UpdateStatement)
				parameters = ((UpdateStatement)query).Parameters;
			if (parameters == null)
				return false;
			foreach (OperandValue p in parameters) {
				if (p.Value is string && ((string)p.Value).Length > 15000)
					return true;
				if (p.Value is byte[] && ((byte[])p.Value).Length > 30000)
					return true;
			}
			return false;
		}
		protected virtual bool IsBatchingForbidden(ModificationStatement dml) {
			return HasLob(dml);
		}
		void ProcessModificationStatement(ModificationStatement dml, IDbCommand command, TaggedParametersHolder identitiesByTag, List<ParameterValue> result, Dictionary<OperandValue, string> parameters, StringBuilder sql) {
			if(dml is InsertStatement) {
				InsertStatement ins = (InsertStatement)dml;
				if(!ReferenceEquals(ins.IdentityParameter, null)) {
					ins.IdentityParameter.Value = DBNull.Value;
				}
				if(ReferenceEquals(ins.IdentityParameter, null)) {
					Query query = new InsertSqlGenerator(this, identitiesByTag, parameters).GenerateSql(ins);
					sql.Append(query.Sql);
					PrepareParameters(command, query);
					sql.Append(';');
				} else {
					identitiesByTag.ConsolidateIdentity(ins.IdentityParameter);
					result.Add(ins.IdentityParameter);
					IDataParameter param = command.CreateParameter();
					param.DbType = ins.IdentityColumnType == DBColumnType.Int32 ? DbType.Int32 : DbType.Int64;
					bool createParameter = true;
					param.ParameterName = GetParameterName(ins.IdentityParameter, parameters.Count, ref createParameter);
					command.Parameters.Add(param);
					param.Direction = ParameterDirection.Output;
					parameters.Add(ins.IdentityParameter, param.ParameterName);
					Query query = GenerateBatchedIdentityInsert(ins, param.ParameterName, identitiesByTag, parameters);
					PrepareParameters(command, query);
					sql.Append(query.Sql);
				}
			} else if(dml is UpdateStatement) {
				Query query = new UpdateSqlGenerator(this, identitiesByTag, parameters).GenerateSql(dml);
				if(query.Sql != null) {
					sql.Append(query.Sql);
					PrepareParameters(command, query);
					if(dml.RecordsAffected != 0)
						sql.Append(";IF SQL%ROWCOUNT <> " + dml.RecordsAffected + " THEN :n := 0; RETURN; END IF;");
					else
						sql.Append(';');
				}
			} else if(dml is DeleteStatement) {
				Query query = new DeleteSqlGenerator(this, identitiesByTag, parameters).GenerateSql(dml);
				sql.Append(query.Sql);
				PrepareParameters(command, query);
				if(dml.RecordsAffected != 0)
					sql.Append(";IF SQL%ROWCOUNT <> " + dml.RecordsAffected + " THEN :n := 0; RETURN; END IF;");
				else
					sql.Append(';');
			} else {
				throw new InvalidOperationException();
			}
		}
		void ProcessIdentityParameters(ModificationStatement[] dmlStatements) {
			foreach(ModificationStatement dml in dmlStatements) {
				InsertStatement ins = dml as InsertStatement;
				if(ins == null || ReferenceEquals(ins.IdentityParameter, null) || ins.IdentityParameter.Value == null) continue;
				switch(ins.IdentityColumnType) {
					case DBColumnType.Int32:
						if(ins.IdentityParameter.Value.GetType() == typeof(int)) continue;
						ins.IdentityParameter.Value = (ins.IdentityParameter.Value as IConvertible).ToInt32(CultureInfo.InvariantCulture);
						break;
					case DBColumnType.Int64:
						if(ins.IdentityParameter.Value.GetType() == typeof(long)) continue;
						ins.IdentityParameter.Value = (ins.IdentityParameter.Value as IConvertible).ToInt64(CultureInfo.InvariantCulture);
						break;
					default:
						throw new NotSupportedException(Res.GetString(Res.ConnectionProvider_TheAutoIncrementedKeyWithX0TypeIsNotSupport, ins.IdentityColumnType, this.GetType()));
				}
			}
		}
		protected override ModificationResult ProcessModifyData(params ModificationStatement[] dmlStatements) {
			BeginTransaction();
			try {
				IDbCommand command = CreateCommand();
				TaggedParametersHolder identitiesByTag = new TaggedParametersHolder();
				List<ParameterValue> result = new List<ParameterValue>();
				Dictionary<OperandValue, string> parameters = new Dictionary<OperandValue, string>();
				StringBuilder sql = new StringBuilder();
				foreach (ModificationStatement dml in dmlStatements) {
					if (IsBatchingForbidden(dml)) {
						command.CommandText = sql.ToString();
						if (!Exec(command, parameters)) {
							command = null;
							parameters = null;
							RollbackTransaction();
							throw new LockingException();
						}
						command = null;
						sql.Length = 0;
						parameters = null;
						ParameterValue res = UpdateRecord(dml, identitiesByTag);
						if (!object.ReferenceEquals(res, null))
							result.Add(res);
						command = CreateCommand();
						parameters = new Dictionary<OperandValue, string>();
					} else {
						ProcessModificationStatement(dml, command, identitiesByTag, result, parameters, sql);
						if (sql.Length > 1024 * 4) {
							command.CommandText = sql.ToString();
							if (!Exec(command, parameters)) {
								command = null;
								parameters = null;
								RollbackTransaction();
								throw new LockingException();
							}
							command = CreateCommand();
							sql.Length = 0;
							parameters = new Dictionary<OperandValue, string>();
						}
					}
				}
				if (sql.Length > 0) {
					command.CommandText = sql.ToString();
					if (!Exec(command, parameters)) {
						RollbackTransaction();
						throw new LockingException();
					}
				}
				CommitTransaction();
				ProcessIdentityParameters(dmlStatements);
				return new ModificationResult(result);
			} catch (Exception e) {
				try {
					RollbackTransaction();
				} catch (Exception e2) {
					throw new DevExpress.Xpo.Exceptions.ExceptionBundleException(e, e2);
				}
				throw;
			}
		}
		protected override async Task<ModificationResult> ProcessModifyDataAsync(AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken, params ModificationStatement[] dmlStatements) {
			BeginTransaction();
			try {
				IDbCommand command = CreateCommand();
				TaggedParametersHolder identitiesByTag = new TaggedParametersHolder();
				List<ParameterValue> result = new List<ParameterValue>();
				Dictionary<OperandValue, string> parameters = new Dictionary<OperandValue, string>();
				StringBuilder sql = new StringBuilder();
				foreach(ModificationStatement dml in dmlStatements) {
					if(IsBatchingForbidden(dml)) {
						command.CommandText = sql.ToString();
						if(!await ExecAsync(command, parameters, asyncOperationId, cancellationToken).ConfigureAwait(false)) {
							command = null;
							parameters = null;
							RollbackTransaction();
							throw new LockingException();
						}
						command = null;
						sql.Length = 0;
						parameters = null;
						ParameterValue res = await UpdateRecordAsync(dml, identitiesByTag, asyncOperationId, cancellationToken).ConfigureAwait(false);
						if(!object.ReferenceEquals(res, null))
							result.Add(res);
						command = CreateCommand();
						parameters = new Dictionary<OperandValue, string>();
					} else {
						ProcessModificationStatement(dml, command, identitiesByTag, result, parameters, sql);
						if(sql.Length > 1024 * 4) {
							command.CommandText = sql.ToString();
							if(!await ExecAsync(command, parameters, asyncOperationId, cancellationToken).ConfigureAwait(false)) {
								command = null;
								parameters = null;
								RollbackTransaction();
								throw new LockingException();
							}
							command = CreateCommand();
							sql.Length = 0;
							parameters = new Dictionary<OperandValue, string>();
						}
					}
				}
				if(sql.Length > 0) {
					command.CommandText = sql.ToString();
					if(!await ExecAsync(command, parameters, asyncOperationId, cancellationToken).ConfigureAwait(false)) {
						RollbackTransaction();
						throw new LockingException();
					}
				}
				CommitTransaction();
				ProcessIdentityParameters(dmlStatements);
				return new ModificationResult(result);
			} catch(Exception e) {
				try {
					RollbackTransaction();
				} catch(Exception e2) {
					throw new DevExpress.Xpo.Exceptions.ExceptionBundleException(e, e2);
				}
				throw;
			}
		}
		protected virtual Query GenerateBatchedIdentityInsert(InsertStatement insert, string identityParameterName, TaggedParametersHolder identitiesByTag, Dictionary<OperandValue, string> parameters) {
			string seqName = GetSeqName(insert.Table.Name);
			Query query = new IdentityInsertSqlGenerator(this, seqName, identityParameterName, identitiesByTag, parameters).GenerateSql(insert);
			return query;
		}
		protected override void CreateDataBase() {
			try {
				Connection.Open();
			} catch (Exception e) {
				throw new UnableToOpenDatabaseException(XpoDefault.ConnectionStringRemovePassword(ConnectionString), e);
			}
		}
		protected virtual string GetSeqName(string tableName) {
			string schema = ComposeSafeSchemaName(tableName);
			string table = ComposeSafeTableName(tableName);
			string sqname = ComposeSafeConstraintName(string.Concat("sq_", table));
			return string.IsNullOrEmpty(schema) ? string.Concat("\"", sqname, "\"") : string.Concat("\"", schema, "\".\"", sqname, "\"");
		}
		protected virtual string GetSeqViewName(string tableName) {
			string schema = ComposeSafeSchemaName(tableName);
			string table = ComposeSafeTableName(tableName);
			string sqname = ComposeSafeConstraintName(string.Concat("sq_", table, "_xpoView"));
			return string.IsNullOrEmpty(schema) ? string.Concat("\"", sqname, "\"") : string.Concat("\"", schema, "\".\"", sqname, "\"");
		}
#if DEBUGTEST
		public virtual string GetSeqNameForTest(string tableName) {
			return GetSeqName(tableName);
		}
		public virtual string GetSeqViewNameForTest(string tableName) {
			return GetSeqViewName(tableName);
		}
		public virtual SelectStatementResult SelectDataForTest(Query query) {
			return SelectData(query);
		}
		public virtual object GetScalarForTest(Query query) {
			return GetScalar(query);
		}
#endif
		public override void CreateTable(DBTable table) {
			base.CreateTable(table);
			if (table.PrimaryKey != null) {
				DBColumn key = table.GetColumn(table.PrimaryKey.Columns[0]);
				if (key.IsIdentity) {
					ExecSql(new Query(string.Format(CultureInfo.InvariantCulture, "create sequence {0} START WITH 1 INCREMENT BY 1", GetSeqName(table.Name))));
				}
			}
		}
		protected override object ConvertToDbParameter(object clientValue, TypeCode clientValueTypeCode) {
			switch(clientValueTypeCode) {
				case TypeCode.Object:
					if(clientValueTypeCode == TypeCode.Object) {
						if(clientValue is DateOnly) {
							return ((DateOnly)clientValue).ToDateTime(TimeOnly.MinValue);
						}
						else if(clientValue is TimeOnly) {
							return DateTime.MinValue.Add(((TimeOnly)clientValue).ToTimeSpan());
						}
					}
					break;
			}
			return base.ConvertToDbParameter(clientValue, clientValueTypeCode);
		}
		ParameterValue CreateParameterForSystemQuery(int tag, string value, int size = 30) {
			return new ParameterValue(tag) { Value = value, DBType = DBColumnType.String, Size = size, DBTypeName = "VARCHAR2" };
		}
		delegate bool TablesFilter(DBTable table);
		SelectStatementResult GetDataForTables(ICollection tables, TablesFilter filter, string queryText) {
			QueryParameterCollection parameters = new QueryParameterCollection();
			List<SelectStatementResult> resultList = new List<SelectStatementResult>();
			int paramIndex = 0;
			int pos = 0;
			int count = tables.Count;
			int currentSize = 0;
			StringCollection inGroup = null;
			foreach (DBTable table in tables) {
				if (currentSize == 0) {
					if (inGroup == null) {
						inGroup = new StringCollection();
					} else {
						if (inGroup.Count == 0) {
							resultList.Add(new SelectStatementResult());
						}
						resultList.Add(SelectData(new Query(string.Format(CultureInfo.InvariantCulture, queryText, StringListHelper.DelimitedText(inGroup, ",")), parameters, inGroup)));
						inGroup.Clear();
						parameters.Clear();
					}
					paramIndex = 0;
					currentSize = (pos < count) ? (count - pos < 15 ? count - pos : 15) : 0;
				}
				if (filter == null || filter(table)) {
					parameters.Add(CreateParameterForSystemQuery(paramIndex, ComposeSafeTableName(table.Name)));
					inGroup.Add(":p" + paramIndex.ToString(CultureInfo.InvariantCulture));
					++paramIndex;
					--currentSize;
				}
				++pos;
			}
			if (inGroup != null && inGroup.Count > 0) {
				resultList.Add(SelectData(new Query(string.Format(CultureInfo.InvariantCulture, queryText, StringListHelper.DelimitedText(inGroup, ",")), parameters, inGroup)));
			}
			if (resultList.Count == 0) return new SelectStatementResult();
			if (resultList.Count == 1) return resultList[0];
			int fullResultSize = 0;
			for (int i = 0; i < resultList.Count; i++) {
				fullResultSize += resultList[i].Rows.Length;
			}
			if (fullResultSize == 0) return new SelectStatementResult();
			SelectStatementResultRow[] fullResult = new SelectStatementResultRow[fullResultSize];
			int copyPos = 0;
			for (int i = 0; i < resultList.Count; i++) {
				Array.Copy(resultList[i].Rows, 0, fullResult, copyPos, resultList[i].Rows.Length);
				copyPos += resultList[i].Rows.Length;
			}
			return new SelectStatementResult(fullResult);
		}
		const int DefaultFloatPrecistion = 126;
		DBColumnType GetTypeFromString(string typeName, int size, int? precision, int scale, string charLength, out string fullTypeName) {
			fullTypeName = typeName;
			switch (typeName.ToLower()) {
				case "int":
					return DBColumnType.Int32;
				case "blob":
					return DBColumnType.ByteArray;
				case "raw":
					fullTypeName = string.Concat(typeName, "(", size.ToString(CultureInfo.InvariantCulture), ")");
					return DBColumnType.ByteArray;
				case "number":
					if(precision.HasValue) {
						fullTypeName = string.Format(CultureInfo.InvariantCulture, "{0}({1},{2})", typeName, precision, scale);
					}
					if (precision == 0 || scale != 0)
						return DBColumnType.Decimal;
					if (precision == 1)
						return DBColumnType.Boolean;
					if (precision <= 3)
						return DBColumnType.Byte;
					if (precision <= 5)
						return DBColumnType.Int16;
					if (precision == null || precision <= 10)
						return DBColumnType.Int32;
					if (precision <= 20)
						return DBColumnType.Int64;
					return DBColumnType.Decimal;
				case "nchar":
				case "char":
					if (size > 1) {
						fullTypeName = string.Concat(typeName, "(", size.ToString(CultureInfo.InvariantCulture), ")");
						return DBColumnType.String;
					}
					return DBColumnType.Char;
				case "money":
					return DBColumnType.Decimal;
				case "float":
					if(precision != null && precision != DefaultFloatPrecistion) {
						fullTypeName = string.Concat(typeName, "(", precision.Value.ToString(CultureInfo.InvariantCulture), ")");
					}
					return DBColumnType.Double;
				case "varchar":
				case "varchar2":
					fullTypeName = string.Concat(typeName, "(", charLength.ToString(CultureInfo.InvariantCulture), ")");
					return DBColumnType.String;
				case "nvarchar":
				case "nvarchar2":
					fullTypeName = string.Concat(typeName, "(", size.ToString(CultureInfo.InvariantCulture), ")");
					return DBColumnType.String;
				case "date":
					return DBColumnType.DateTime;
				case "clob":
				case "nclob":
					return DBColumnType.String;
			}
			return DBColumnType.Unknown;
		}
		void GetColumns(DBTable table) {
			string schema = ComposeSafeSchemaName(table.Name);
			string safeTableName = ComposeSafeTableName(table.Name);
			Query query;
			if(schema == string.Empty) {
				query = new Query("SELECT COLUMN_NAME, DATA_TYPE, CHAR_COL_DECL_LENGTH, DATA_PRECISION, DATA_SCALE, NULLABLE, CASE WHEN VIRTUAL_COLUMN = 'NO' THEN DATA_DEFAULT ELSE NULL END, DATA_LENGTH, CHAR_USED, CHAR_LENGTH from USER_TAB_COLS where TABLE_NAME = :p0 and HIDDEN_COLUMN = 'NO'", new QueryParameterCollection(CreateParameterForSystemQuery(0, safeTableName)), new string[] { ":p0" });
			} else {
				query = new Query("SELECT COLUMN_NAME, DATA_TYPE, CHAR_COL_DECL_LENGTH, DATA_PRECISION, DATA_SCALE, NULLABLE, CASE WHEN VIRTUAL_COLUMN = 'NO' THEN DATA_DEFAULT ELSE NULL END, DATA_LENGTH, CHAR_USED, CHAR_LENGTH from ALL_TAB_COLS where OWNER = :p0 and TABLE_NAME = :p1 and HIDDEN_COLUMN = 'NO'", new QueryParameterCollection(CreateParameterForSystemQuery(0, schema), CreateParameterForSystemQuery(1, safeTableName)), new string[] { ":p0", ":p1" });
			}
			foreach (SelectStatementResultRow row in SelectData(query).Rows) {
				int size = row.Values[2] != DBNull.Value ? ((IConvertible)row.Values[2]).ToInt32(CultureInfo.InvariantCulture) : (row.Values[7] != DBNull.Value ? ((IConvertible)row.Values[7]).ToInt32(CultureInfo.InvariantCulture) : 0);
				int? precision = row.Values[3] != DBNull.Value ? ((IConvertible)row.Values[3]).ToInt32(CultureInfo.InvariantCulture) : (int?)null;
				int scale = row.Values[4] != DBNull.Value ? ((IConvertible)row.Values[4]).ToInt32(CultureInfo.InvariantCulture) : 0;
				string typeName = (string)row.Values[1];
				string fullTypeName;
				string charLength = row.Values[8] != DBNull.Value && row.Values[9] != DBNull.Value ?
					string.Format("{0} {1}", ((IConvertible)row.Values[9]).ToInt32(CultureInfo.InvariantCulture).ToString(), row.Values[8].ToString() == "C" ? "CHAR" : "BYTE") : null;
				DBColumnType type = GetTypeFromString(typeName, size, precision, scale, charLength, out fullTypeName);
				bool isNullable = (row.Values[5].ToString() == "Y");
				string dbDefaultValue = (row.Values[6] as string);
				object defaultValue = null;
				try {
					if(!string.IsNullOrEmpty(dbDefaultValue)) {
						string scalarQuery = string.Format("select {0} from dual", dbDefaultValue);
						defaultValue = FixDBNullScalar(GetScalar(new Query(scalarQuery)));
					}
				} catch { }
				if(defaultValue != null) {
					var defaultValueType = type;
					switch(defaultValueType) {
						case DBColumnType.Byte:
							defaultValueType = DBColumnType.Int16;
							break;
						case DBColumnType.Int16:
							defaultValueType = DBColumnType.Int32;
							break;
						case DBColumnType.Int32:
							defaultValueType = DBColumnType.Int64;
							break;
						case DBColumnType.Int64:
							defaultValueType = DBColumnType.Decimal;
							break;
					}
					ReformatReadValueArgs refmtArgs = new ReformatReadValueArgs(DBColumn.GetType(defaultValueType));
					refmtArgs.AttachValueReadFromDb(defaultValue);
					try {
						defaultValue = ReformatReadValue(defaultValue, refmtArgs);
					} catch {
						defaultValue = null;
					}
				}
				DBColumn dbColumn = new DBColumn((string)row.Values[0], false, fullTypeName ?? string.Empty, type == DBColumnType.String || type == DBColumnType.ByteArray ? size : 0, type, isNullable, defaultValue);
				dbColumn.DbDefaultValue = dbDefaultValue;
				table.AddColumn(dbColumn);
			}
		}
		void GetPrimaryKey(DBTable table) {
			string schema = ComposeSafeSchemaName(table.Name);
			string safeTableName = ComposeSafeTableName(table.Name);
			Query query;
			if (schema == string.Empty)
				query = new Query(
				@"select tc.COLUMN_NAME, c.CONSTRAINT_NAME from USER_CONS_COLUMNS tc
left join USER_CONSTRAINTS c on tc.CONSTRAINT_NAME = c.CONSTRAINT_NAME
where c.CONSTRAINT_TYPE = 'P' and tc.TABLE_NAME = :p0
order by tc.POSITION",
				new QueryParameterCollection(CreateParameterForSystemQuery(0, safeTableName)), new string[] { ":p0" });
			else
				query = new Query(
					@"select tc.COLUMN_NAME, c.CONSTRAINT_NAME from ALL_CONS_COLUMNS tc
left join ALL_CONSTRAINTS c on tc.CONSTRAINT_NAME = c.CONSTRAINT_NAME
where c.CONSTRAINT_TYPE = 'P' and c.OWNER = :p0  and tc.TABLE_NAME = :p1
order by tc.POSITION",
					new QueryParameterCollection(CreateParameterForSystemQuery(0, schema), CreateParameterForSystemQuery(1, safeTableName)), new string[] { ":p0", ":p1" });
			SelectStatementResult data = SelectData(query);
			if (data.Rows.Length > 0) {
				StringCollection cols = new StringCollection();
				for(int i = 0; i < data.Rows.Length; i++) {
					string columnName = (string)data.Rows[i].Values[0];
					DBColumn column = table.GetColumn(columnName);
					if(column != null) {
						column.IsKey = true;
						if(data.Rows.Length == 1) {
							column.IsIdentity = IsSequenceForTableExists(schema, safeTableName);
						}
					}
					cols.Add(columnName);
				}
				table.PrimaryKey = new DBPrimaryKey(table.Name, cols);
				table.PrimaryKey.Name = (string)data.Rows[0].Values[1];
			}
		}
		bool IsSequenceForTableExists(string schema, string safeTableName) {
			Query query;
			string sequenceName = ComposeSafeConstraintName(string.Concat("sq_", safeTableName));
			if(SysUsersAvailable && !string.IsNullOrEmpty(schema)) {
				query = new Query(
					@"select count(1) from SYS.ALL_SEQUENCES o inner join SYS.USER$ u on o.""SEQUENCE_OWNER"" = u.""NAME""
where u.""NAME"" <> 'SYS' and u.""NAME"" <> 'SYSTEM' and u.""TYPE#"" = 1 and u.ASTATUS = 0
and o.SEQUENCE_OWNER = :p0 AND o.SEQUENCE_NAME = :p1",
					new QueryParameterCollection(CreateParameterForSystemQuery(0, schema), CreateParameterForSystemQuery(1, sequenceName)), new string[] { ":p0", ":p1" });
			} else {
				query = new Query(
					@"select count(1) from USER_SEQUENCES where SEQUENCE_NAME = :p0",
					new QueryParameterCollection(CreateParameterForSystemQuery(0, sequenceName)), new string[] { ":p0" });
			}
			return Convert.ToInt32(GetScalar(query)) > 0;
		}
		void GetIndexes(DBTable table) {
			string schema = ComposeSafeSchemaName(table.Name);
			string safeTableName = ComposeSafeTableName(table.Name);
			Query query;
			if(schema == string.Empty) {
				query = new Query(
				@"select ind.INDEX_NAME, cols.COLUMN_NAME, cols.COLUMN_POSITION, ind.uniqueness, ind.INDEX_NAME
from USER_INDEXES ind
join USER_IND_COLUMNS cols on ind.INDEX_NAME = cols.INDEX_NAME
where ind.TABLE_NAME = :p0
order by ind.INDEX_NAME, cols.COLUMN_POSITION",
				new QueryParameterCollection(CreateParameterForSystemQuery(0, safeTableName)), new string[] { ":p0" });
			} else {
				query = new Query(
				@"select ind.INDEX_NAME, cols.COLUMN_NAME, cols.COLUMN_POSITION, ind.uniqueness, ind.INDEX_NAME
from ALL_INDEXES ind
join ALL_IND_COLUMNS cols on ind.INDEX_NAME = cols.INDEX_NAME
where ind.TABLE_OWNER = :p0  and ind.TABLE_NAME = :p1
order by ind.INDEX_NAME, cols.COLUMN_POSITION",
				new QueryParameterCollection(CreateParameterForSystemQuery(0, schema), CreateParameterForSystemQuery(1, safeTableName)), new string[] { ":p0", ":p1" });
			}
			SelectStatementResult data = SelectData(query);
			DBIndex index = null;
			foreach(SelectStatementResultRow row in data.Rows) {
				if(Convert.ToDecimal(row.Values[2]) == 1m) {
					StringCollection list = new StringCollection();
					list.Add((string)row.Values[1]);
					index = new DBIndex((string)row.Values[0], list, string.Equals(row.Values[3], "UNIQUE"));
					index.Name = (string)row.Values[4];
					table.Indexes.Add(index);
				} else
					index.Columns.Add((string)row.Values[1]);
			}
		}
		bool getForeignKeysHasNoRights = false;
		void GetForeignKeys(DBTable table) {
			string schema = ComposeSafeSchemaName(table.Name);
			string safeTableName = ComposeSafeTableName(table.Name);
			SelectStatementResult data = null;
			if (schema == string.Empty) {
				do {
					Query query = new Query(string.Format(@"select tc.POSITION, tc.COLUMN_NAME, fc.COLUMN_NAME, fc.TABLE_NAME, c.CONSTRAINT_NAME from USER_CONSTRAINTS c
join USER_CONS_COLUMNS tc  on tc.CONSTRAINT_NAME = c.CONSTRAINT_NAME 
join {0} fc on c.R_CONSTRAINT_NAME = fc.CONSTRAINT_NAME and tc.POSITION = fc.POSITION 
where c.TABLE_NAME = :p0
order by c.CONSTRAINT_NAME, tc.POSITION", getForeignKeysHasNoRights ? "USER_CONS_COLUMNS" : "ALL_CONS_COLUMNS"),
						new QueryParameterCollection(CreateParameterForSystemQuery(0, safeTableName)), new string[] { ":p0" });
					try {
						data = SelectData(query);
					} catch(Exception) {
						if(getForeignKeysHasNoRights)
							throw;
						getForeignKeysHasNoRights = true;
					}
				} while(data == null);
			} else {
				Query query = new Query(@"select tc.POSITION, tc.COLUMN_NAME, fc.COLUMN_NAME, fc.TABLE_NAME, c.CONSTRAINT_NAME from ALL_CONSTRAINTS c
join ALL_CONS_COLUMNS tc  on tc.CONSTRAINT_NAME = c.CONSTRAINT_NAME 
join ALL_CONS_COLUMNS fc on c.R_CONSTRAINT_NAME = fc.CONSTRAINT_NAME and tc.POSITION = fc.POSITION 
where c.OWNER = :p0 and c.TABLE_NAME = :p1
order by c.CONSTRAINT_NAME, tc.POSITION",
				new QueryParameterCollection(CreateParameterForSystemQuery(0, schema), CreateParameterForSystemQuery(1, safeTableName)), new string[] { ":p0", ":p1" });
				data = SelectData(query);
			}
			DBForeignKey fk = null;
			foreach (SelectStatementResultRow row in data.Rows) {
				if (Convert.ToDecimal(row.Values[0]) == 1m) {
					StringCollection pkc = new StringCollection();
					StringCollection fkc = new StringCollection();
					pkc.Add((string)row.Values[2]);
					fkc.Add((string)row.Values[1]);
					fk = new DBForeignKey(fkc, (string)row.Values[3], pkc);
					fk.Name = (string)row.Values[4];
					table.ForeignKeys.Add(fk);
				} else {
					fk.Columns.Add((string)row.Values[1]);
					fk.PrimaryKeyTableKeyColumns.Add((string)row.Values[2]);
				}
			}
		}
		public override void GetTableSchema(DBTable table, bool checkIndexes, bool checkForeignKeys) {
			GetColumns(table);
			GetPrimaryKey(table);
			if (checkIndexes)
				GetIndexes(table);
			if (checkForeignKeys)
				GetForeignKeys(table);
		}
		public override ICollection CollectTablesToCreate(ICollection tables) {
			Dictionary<string, bool> dbTables = new Dictionary<string, bool>();
			Dictionary<string, bool> dbSchemaTables = new Dictionary<string, bool>();
			if (SysUsersAvailable) {
				string queryString = @"select o.TABLE_NAME, o.OWNER from SYS.ALL_TABLES o inner join SYS.USER$ u on o.""OWNER"" = u.""NAME""
where u.""NAME"" <> 'SYS' and u.""NAME"" <> 'SYSTEM' and u.""TYPE#"" = 1 and u.ASTATUS = 0 and o.TABLE_NAME in ({0})";
				foreach (SelectStatementResultRow row in GetDataForTables(tables, null, queryString).Rows) {
					if (row.Values[0] is DBNull) continue;
					string tableName = (string)row.Values[0];
					string tableSchemaName = (string)row.Values[1];
					dbSchemaTables.Add(string.Concat(tableSchemaName, ".", tableName), false);
				}
				queryString = @"select o.VIEW_NAME, o.OWNER from SYS.ALL_VIEWS o inner join SYS.USER$ u on o.""OWNER"" = u.""NAME""
where u.""NAME"" <> 'SYS' and u.""NAME"" <> 'SYSTEM' and u.""TYPE#"" = 1 and u.ASTATUS = 0 and o.VIEW_NAME in ({0})";
				foreach (SelectStatementResultRow row in GetDataForTables(tables, null, queryString).Rows) {
					if (row.Values[0] is DBNull) continue;
					string tableName = (string)row.Values[0];
					string tableSchemaName = (string)row.Values[1];
					dbSchemaTables.Add(string.Concat(tableSchemaName, ".", tableName), true);
				}
			}
			foreach (SelectStatementResultRow row in GetDataForTables(tables, null, "select TABLE_NAME from USER_TABLES where TABLE_NAME in ({0})").Rows)
				dbTables.Add((string)row.Values[0], false);
			foreach (SelectStatementResultRow row in GetDataForTables(tables, null, "select VIEW_NAME from USER_VIEWS where VIEW_NAME in ({0})").Rows)
				dbTables.Add((string)row.Values[0], true);
			ArrayList list = new ArrayList();
			foreach (DBTable table in tables) {
				string tableName = ComposeSafeTableName(table.Name);
				string tableSchemaName = ComposeSafeSchemaName(table.Name);
				bool isView = false;
				if (!dbSchemaTables.TryGetValue(string.Concat(tableSchemaName, ".", tableName), out isView) && !dbTables.TryGetValue(tableName, out isView))
					list.Add(table);
				else
					table.IsView = isView;
			}
			return list;
		}
		protected override int GetSafeNameTableMaxLength() {
			return 30;
		}
		protected override int GetObjectNameEffectiveLength(string objectName) {
			return string.IsNullOrEmpty(objectName)? 0 : ( IsUtf8Charset ? Encoding.UTF8.GetByteCount(objectName) : objectName.Length);
		}
		public override string FormatTable(string schema, string tableName) {
			if (string.IsNullOrEmpty(schema))
				return string.Format(CultureInfo.InvariantCulture, "\"{0}\"", tableName);
			else
				return string.Format(CultureInfo.InvariantCulture, "\"{0}\".\"{1}\"", schema, tableName);
		}
		public override string FormatTable(string schema, string tableName, string tableAlias) {
			if (string.IsNullOrEmpty(schema))
				return string.Format(CultureInfo.InvariantCulture, "\"{0}\" {1}", tableName, tableAlias);
			else
				return string.Format(CultureInfo.InvariantCulture, "\"{0}\".\"{1}\" {2}", schema, tableName, tableAlias);
		}
		public override string FormatColumn(string columnName) {
			return OracleFormatterHelper.FormatColumn(columnName);
		}
		public override string FormatColumn(string columnName, string tableAlias) {
			return OracleFormatterHelper.FormatColumn(columnName, tableAlias);
		}
		public override string FormatSelect(string selectedPropertiesSql, string fromSql, string whereSql, string orderBySql, string groupBySql, string havingSql, int skipSelectedRecords, int topSelectedRecords) {
			base.FormatSelect(selectedPropertiesSql, fromSql, whereSql, orderBySql, groupBySql, havingSql, skipSelectedRecords, topSelectedRecords);
			string expandedWhereSql = whereSql == null ? null : string.Format(CultureInfo.InvariantCulture, "\nwhere {0}", whereSql);
			string expandedOrderBySql = orderBySql != null ? string.Format(CultureInfo.InvariantCulture, "{0}order by {1}", Environment.NewLine, orderBySql) : string.Empty;
			string expandedHavingSql = havingSql != null ? string.Format(CultureInfo.InvariantCulture, "{0}having {1}", Environment.NewLine, havingSql) : string.Empty;
			string expandedGroupBySql = groupBySql != null ? string.Format(CultureInfo.InvariantCulture, "{0}group by {1}", Environment.NewLine, groupBySql) : string.Empty;
			string[] cols = SimpleSqlParser.GetColumns(selectedPropertiesSql);
			StringBuilder expandedSelectedProperties = SimpleSqlParser.GetExpandedProperties(cols, "a");
			if(skipSelectedRecords == 0) {
				if(topSelectedRecords == 0) {
					return string.Format(CultureInfo.InvariantCulture, "select {0} from {1}{2}{3}{4}{5}",
						selectedPropertiesSql, fromSql, expandedWhereSql, expandedGroupBySql, expandedHavingSql, expandedOrderBySql);
				}
				return string.Format(CultureInfo.InvariantCulture, "select * from (select {1} from {2}{3}{4}{5}{6}) where RowNum <= {0}",
					topSelectedRecords, string.Join(",", cols), fromSql, expandedWhereSql, expandedGroupBySql, expandedHavingSql, expandedOrderBySql);
			}
			string baseFormat = "select {0} from(select {0}, RowNum rNum from(select {1} from {2}{3}{4}{5}{6})a)a where rNum > {7}";
			if (topSelectedRecords != 0) {
				baseFormat = string.Format("{0} {1}", baseFormat, "and RowNum <= {8}");
			}
			return string.Format(CultureInfo.InvariantCulture, baseFormat, expandedSelectedProperties, string.Join(",", cols), fromSql,
				expandedWhereSql, expandedGroupBySql, expandedHavingSql, expandedOrderBySql, skipSelectedRecords, topSelectedRecords);
		}
		public override bool NativeSkipTakeSupported { get { return true; } }
		public override string FormatInsertDefaultValues(string tableName) {
			return OracleFormatterHelper.FormatInsertDefaultValues(tableName);
		}
		public override string FormatInsert(string tableName, string fields, string values) {
			return OracleFormatterHelper.FormatInsert(tableName, fields, values);
		}
		public override string FormatUpdate(string tableName, string sets, string whereClause) {
			return string.Format(CultureInfo.InvariantCulture, "update {0} set {1} where {2}",
				tableName, sets, whereClause);
		}
		public override string FormatDelete(string tableName, string whereClause) {
			return OracleFormatterHelper.FormatDelete(tableName, whereClause);
		}
		public override string FormatFunction(FunctionOperatorType operatorType, params string[] operands) {
			string format = OracleFormatterHelper.FormatFunction(operatorType, operands);
			return format == null ? base.FormatFunction(operatorType, operands) : format;
		}
		public override string FormatFunction(ProcessParameter processParameter, FunctionOperatorType operatorType, params object[] operands) {
			string format = OracleFormatterHelper.FormatFunction(processParameter, ResolveColumnType, operatorType, operands);
			return format == null ? base.FormatFunction(processParameter, operatorType, operands) : format;
		}
		public override string FormatBinary(BinaryOperatorType operatorType, string leftOperand, string rightOperand) {
			return OracleFormatterHelper.FormatBinary(operatorType, leftOperand, rightOperand);
		}
		public override string FormatOrder(string sortProperty, SortingDirection direction) {
			return OracleFormatterHelper.FormatOrder(sortProperty, direction);
		}
		public override string GetParameterName(OperandValue parameter, int index, ref bool createParameter) {
			object value = parameter.Value;
			createParameter = false;
			if (parameter is ConstantValue && value != null) {
				switch (Type.GetTypeCode(value.GetType())) {
					case TypeCode.Int32:
						return ((int)value).ToString(CultureInfo.InvariantCulture);
					case TypeCode.Boolean:
						return (bool)value ? "1" : "0";
					case TypeCode.String:
						return "N'" + ((string)value).Replace("'", "''") + "'";
				}
			}
			createParameter = true;
			return ":p" + index.ToString(CultureInfo.InvariantCulture);
		}
		public override string FormatConstraint(string constraintName) {
			return OracleFormatterHelper.FormatConstraint(constraintName);
		}
		const string IdentitySequencePrefix = "ISEQ$$_";
		public void ClearDatabase(IDbCommand command) {
			if (SysUsersAvailable) {
				Query query = new Query(@"select o.SEQUENCE_NAME, o.SEQUENCE_OWNER from SYS.ALL_SEQUENCES o inner join SYS.USER$ u on o.""SEQUENCE_OWNER"" = u.""NAME""
where u.""NAME"" <> 'SYS' and u.""NAME"" <> 'SYSTEM' and u.""TYPE#"" = 1 and u.ASTATUS = 0");
				SelectStatementResult generators = SelectData(query);
				foreach (SelectStatementResultRow row in generators.Rows) {
					string sequenceName = ((string)row.Values[0]).Trim();
					if(sequenceName.StartsWith(IdentitySequencePrefix)) {
						continue;
					}
					string schema = ((string)row.Values[1]).Trim();
					command.CommandText = "drop sequence " + FormatTable(schema, sequenceName);
					command.ExecuteNonQuery();
				}
				query = new Query(@"select o.CONSTRAINT_NAME, o.TABLE_NAME, o.OWNER from SYS.ALL_CONSTRAINTS o inner join SYS.USER$ u on o.""OWNER"" = u.""NAME""
where o.CONSTRAINT_TYPE = 'R' and u.""NAME"" <> 'SYS' and u.""NAME"" <> 'SYSTEM' and u.""TYPE#"" = 1 and u.ASTATUS = 0");
				SelectStatementResult constraints = SelectData(query);
				foreach (SelectStatementResultRow row in constraints.Rows) {
					string constraintName = ((string)row.Values[0]).Trim();
					string tableName = ((string)row.Values[1]).Trim();
					string schema = ((string)row.Values[2]).Trim();
					command.CommandText = string.Format("alter table {0} drop constraint {1}", FormatTable(schema, tableName), FormatConstraint(constraintName));
					command.ExecuteNonQuery();
				}
				string[] tables = GetStorageTablesList(false);
				foreach (string table in tables) {
					string schema = GetSchemaName(table);
					string tableName = GetTableName(table);
					command.CommandText = string.Format("drop table {0}", FormatTable(schema, tableName));
					command.ExecuteNonQuery();
				}
			} else {
				SelectStatementResult generators = SelectData(new Query("select SEQUENCE_NAME from USER_SEQUENCES"));
				foreach (SelectStatementResultRow row in generators.Rows) {
					string sequenceName = ((string)row.Values[0]).Trim();
					if(sequenceName.StartsWith(IdentitySequencePrefix)) {
						continue;
					}
					command.CommandText = string.Concat("drop sequence \"", sequenceName, "\"");
					command.ExecuteNonQuery();
				}
				SelectStatementResult constraints = SelectData(new Query("select CONSTRAINT_NAME, TABLE_NAME from USER_CONSTRAINTS where CONSTRAINT_TYPE = 'R'"));
				foreach (SelectStatementResultRow row in constraints.Rows) {
					command.CommandText = "alter table \"" + ((string)row.Values[1]).Trim() + "\" drop constraint \"" + ((string)row.Values[0]).Trim() + "\"";
					command.ExecuteNonQuery();
				}
				string[] tables = GetStorageTablesList(false);
				foreach (string table in tables) {
					command.CommandText = "drop table \"" + table + "\"";
					command.ExecuteNonQuery();
				}
			}
		}
		protected override void ProcessClearDatabase() {
			IDbCommand command = CreateCommand();
			ClearDatabase(command);
		}
		public override string[] GetStorageTablesList(bool includeViews) {
			string[] queryStrings;
			if (SysUsersAvailable) {
				queryStrings = new string[] {
					@"select o.TABLE_NAME, o.OWNER from SYS.All_TABLES o inner join SYS.USER$ u on o.""OWNER"" = u.""NAME""
where u.""NAME"" <> 'SYS' and u.""NAME"" <> 'SYSTEM' and u.""TYPE#"" = 1 and u.ASTATUS = 0",
					@"select o.VIEW_NAME, o.OWNER from SYS.All_VIEWS o inner join SYS.USER$ u on o.""OWNER"" = u.""NAME""
where u.""NAME"" <> 'SYS' and u.""NAME"" <> 'SYSTEM' and u.""TYPE#"" = 1 and u.ASTATUS = 0"
				};
			} else {
				queryStrings = new string[] {
					@"select TABLE_NAME from USER_TABLES",
					@"select VIEW_NAME from USER_VIEWS"
				};
			}
			int resultsCount = 0;
			List<SelectStatementResult> results = new List<SelectStatementResult>(includeViews ? 2 : 1);
			foreach (string queryString in queryStrings) {
				Query query = new Query(queryString);
				SelectStatementResult result = SelectData(query);
				results.Add(result);
				resultsCount += result.Rows.Length;
				if (!includeViews) break;
			}
			List<string> resultList = new List<string>(resultsCount);
			foreach (SelectStatementResult result in results) {
				foreach (SelectStatementResultRow row in result.Rows) {
					if (!((string)row.Values[0]).StartsWith("BIN$")) {
						if (SysUsersAvailable) {
							string objectName = (string)row.Values[0];
							string owner = (string)row.Values[1];
							if (ObjectsOwner != owner && owner != null)
								resultList.Add(string.Concat(owner, ".", objectName));
							else
								resultList.Add(objectName);
						} else {
							resultList.Add((string)row.Values[0]);
						}
					}
				}
			}
			return resultList.ToArray();
		}
		public string ObjectsOwner = string.Empty;
		public bool SysUsersAvailable = true;
		public bool IsUtf8Charset;
		public virtual string GetCurrentUser() {
			Query query = new Query("select user CURRENT_USER from DUAL");
			SelectStatementResult result = SelectData(query);
			return result.Rows[0].Values[0].ToString();
		}
		public virtual bool CheckSysUsers() {
			Query query = new Query(@"SELECT count(*) FROM ""SYS"".""ALL_TABLES"" WHERE ""OWNER"" = 'SYS' AND ""TABLE_NAME"" = 'USER$'");
			Query query2 = new Query(@"SELECT count(*) FROM ""SYS"".""USER$""");
			bool result = true;
			try {
				object count = this.GetScalar(query);
				result = count != null && Convert.ToInt32(count) == 1;
				if(result) {
					count = this.GetScalar(query2);
				}
			} catch(Exception) {
				result = false;
			}
			return result;
		}
		protected void ReadDbCharset() {
			Query query = new Query(@"SELECT VALUE FROM NLS_DATABASE_PARAMETERS WHERE PARAMETER = 'NLS_CHARACTERSET'");
			try {
				object value = this.GetScalar(query);
				if(value != null && value is string) {
					string charset = (string)value;
					IsUtf8Charset = charset.Contains("UTF8");
				}
			} catch(Exception) { }
		}
		string GetSchemaName(string table) {
			int dot = table.IndexOf('.');
			if (dot > 0) { return table.Substring(0, dot); }
			return string.Empty;
		}
		string GetTableName(string table) {
			int dot = table.IndexOf('.');
			if (dot > 0) return table.Remove(0, dot + 1);
			return table;
		}
		public override string GenerateStoredProcedures(DBTable table, out string dropLines) {
			List<string> dropList = new List<string>();
			StringBuilder result = new StringBuilder();
			GenerateView(table, result, dropList);
			GenerateInsertSP(table, result, dropList);
			GenerateUpdateSP(table, result, dropList);
			GenerateDeleteSP(table, result, dropList);
			GenerateInsteadOfInsertTrigger(table, result, dropList);
			GenerateInsteadOfUpdateTrigger(table, result, dropList);
			GenerateInsteadOfDeleteTrigger(table, result, dropList);
			if (dropList.Count > 0) {
				StringBuilder dropResult = new StringBuilder();
				for (int i = dropList.Count - 1; i >= 0; i--) {
					dropResult.AppendLine(dropList[i]);
					dropResult.AppendLine("/");
				}
				dropLines = dropResult.ToString();
			} else {
				dropLines = string.Empty;
			}
			return result.ToString();
		}
		void GenerateView(DBTable table, StringBuilder result, List<string> dropList) {
			string objName = ComposeSafeTableName(string.Format("{0}_xpoView", table.Name));
			result.AppendLine(string.Format("CREATE VIEW \"{0}\"(", objName));
			dropList.Add(string.Format("DROP VIEW \"{0}\";", objName));
			for (int i = 0; i < table.Columns.Count; i++) {
				if (i != 0) { result.AppendLine(","); }
				result.Append(string.Format("\t\"{0}\"", table.Columns[i].Name));
			}
			result.AppendLine();
			result.AppendLine(")");
			result.AppendLine("AS");
			result.AppendLine("\tSELECT");
			for (int i = 0; i < table.Columns.Count; i++) {
				if (i != 0) { result.AppendLine(","); }
				result.Append(string.Format("\t\t\"{0}\"", table.Columns[i].Name));
			}
			result.AppendLine();
			result.AppendLine(string.Format("\tFROM \"{0}\";", table.Name));
			result.AppendLine("/");
			objName = GetSeqViewName(table.Name);
			result.AppendLine(string.Format("CREATE SYNONYM {0}", objName));
			dropList.Add(string.Format("DROP SYNONYM {0};", objName));
			result.AppendLine(string.Format("\tFOR {0}", GetSeqName(table.Name)));
			result.AppendLine("/");
		}
		void GenerateInsertSP(DBTable table, StringBuilder result, List<string> dropList) {
			string objName = ComposeSafeTableName(string.Format("sp_{0}_xpoView_insert", table.Name));
			result.AppendLine(string.Format("CREATE PROCEDURE \"{0}\"(", objName));
			dropList.Add(string.Format("DROP PROCEDURE \"{0}\";", objName));
			for (int i = 0; i < table.Columns.Count; i++) {
				if (i != 0) { result.AppendLine(","); }
				string dbType = GetRawType(GetSqlCreateColumnType(table, table.Columns[i]));
				result.Append(string.Format("\t{0}_ {1}", table.Columns[i].Name, dbType));
			}
			result.AppendLine();
			result.AppendLine(")");
			result.AppendLine("AS");
			result.AppendLine("BEGIN");
			result.AppendLine(string.Format("\tINSERT INTO \"{0}\"(", table.Name));
			for (int i = 0; i < table.Columns.Count; i++) {
				if (i != 0) { result.AppendLine(","); }
				result.Append(string.Format("\t\t\"{0}\"", table.Columns[i].Name));
			}
			result.AppendLine();
			result.AppendLine("\t)");
			result.AppendLine("\tVALUES(");
			for (int i = 0; i < table.Columns.Count; i++) {
				if (i != 0) { result.AppendLine(","); }
				result.Append(string.Format("\t\t{0}_", table.Columns[i].Name));
			}
			result.AppendLine();
			result.AppendLine("\t);");
			result.AppendLine("END;");
			result.AppendLine("/");
		}
		void GenerateUpdateSP(DBTable table, StringBuilder result, List<string> dropList) {
			string objName = ComposeSafeTableName(string.Format("sp_{0}_xpoView_update", table.Name));
			result.AppendLine(string.Format("CREATE PROCEDURE \"{0}\"(", objName));
			dropList.Add(string.Format("DROP PROCEDURE \"{0}\";", objName));
			AppendKeys(table, result);
			for (int i = 0; i < table.Columns.Count; i++) {
				if (IsKey(table, table.Columns[i].Name)) { continue; }
				if (i != 0 || table.PrimaryKey.Columns.Count > 0) { result.AppendLine(","); }
				string dbType = GetRawType(GetSqlCreateColumnType(table, table.Columns[i]));
				result.AppendLine(string.Format("\told_{0} {1},", table.Columns[i].Name, dbType));
				result.Append(string.Format("\t{0}_ {1}", table.Columns[i].Name, dbType));
			}
			result.AppendLine();
			result.AppendLine(")");
			result.AppendLine("AS");
			result.AppendLine("BEGIN");
			result.AppendLine(string.Format("\tUPDATE \"{0}\" SET", table.Name));
			bool first = true;
			for (int i = 0; i < table.Columns.Count; i++) {
				if (IsKey(table, table.Columns[i].Name)) { continue; }
				if (first) { first = false; } else { result.AppendLine(","); }
				result.Append(string.Format("\t\t\"{0}\"={0}_", table.Columns[i].Name));
			}
			result.AppendLine();
			result.AppendLine("\tWHERE");
			AppendWhere(table, result);
			result.AppendLine("END;");
			result.AppendLine("/");
		}
		void GenerateDeleteSP(DBTable table, StringBuilder result, List<string> dropList) {
			string objName = ComposeSafeTableName(string.Format("sp_{0}_xpoView_delete", table.Name));
			result.AppendLine(string.Format("CREATE PROCEDURE \"{0}\"(", objName));
			dropList.Add(string.Format("DROP PROCEDURE \"{0}\";", objName));
			AppendKeys(table, result);
			for (int i = 0; i < table.Columns.Count; i++) {
				if (IsKey(table, table.Columns[i].Name)) { continue; }
				if (i != 0 || table.PrimaryKey.Columns.Count > 0) { result.AppendLine(","); }
				string dbType = GetSqlCreateColumnType(table, table.Columns[i]);
				result.Append(string.Format("\told_{0} {1}", table.Columns[i].Name, GetRawType(dbType)));
			}
			result.AppendLine();
			result.AppendLine(")");
			result.AppendLine("AS");
			result.AppendLine("BEGIN");
			result.AppendLine(string.Format("\tDELETE FROM \"{0}\" WHERE", table.Name));
			AppendWhere(table, result);
			result.AppendLine("END;");
			result.AppendLine("/");
		}
		void GenerateInsteadOfInsertTrigger(DBTable table, StringBuilder result, List<string> dropList) {
			string objName = ComposeSafeTableName(string.Format("t_{0}_xpoView_insert", table.Name));
			result.AppendLine(string.Format("CREATE TRIGGER \"{0}\"", objName));
			dropList.Add(string.Format("DROP TRIGGER \"{0}\";", objName));
			result.AppendLine("INSTEAD OF INSERT");
			result.AppendLine(string.Format("ON \"{0}_xpoView\"", table.Name));
			result.AppendLine("FOR EACH ROW");
			result.AppendLine("BEGIN");
			objName = ComposeSafeTableName(string.Format("sp_{0}_xpoView_insert", table.Name));
			result.AppendLine(string.Format("\t\"{0}\"(", objName));
			for (int i = 0; i < table.Columns.Count; i++) {
				if (i != 0) { result.AppendLine(","); }
				result.Append(string.Format("\t\t:new.\"{0}\"", table.Columns[i].Name));
			}
			result.AppendLine();
			result.AppendLine("\t);");
			result.AppendLine("END;");
			result.AppendLine("/");
		}
		void GenerateInsteadOfUpdateTrigger(DBTable table, StringBuilder result, List<string> dropList) {
			string objName = ComposeSafeTableName(string.Format("t_{0}_xpoView_update", table.Name));
			result.AppendLine(string.Format("CREATE TRIGGER \"{0}\"", objName));
			dropList.Add(string.Format("DROP TRIGGER \"{0}\";", objName));
			result.AppendLine("INSTEAD OF UPDATE");
			result.AppendLine(string.Format("ON \"{0}_xpoView\"", table.Name));
			result.AppendLine("FOR EACH ROW");
			result.AppendLine("BEGIN");
			objName = ComposeSafeTableName(string.Format("sp_{0}_xpoView_update", table.Name));
			result.AppendLine(string.Format("\t\"{0}\"(", objName));
			for (int i = 0; i < table.PrimaryKey.Columns.Count; i++) {
				if (i != 0) { result.AppendLine(","); }
				result.Append(string.Format("\t\t:new.\"{0}\"", table.PrimaryKey.Columns[i]));
			}
			for (int i = 0; i < table.Columns.Count; i++) {
				if (IsKey(table, table.Columns[i].Name)) { continue; }
				if (i != 0 || table.PrimaryKey.Columns.Count > 0) { result.AppendLine(","); }
				result.AppendLine(string.Format("\t\t:old.\"{0}\",", table.Columns[i].Name));
				result.Append(string.Format("\t\t:new.\"{0}\"", table.Columns[i].Name));
			}
			result.AppendLine();
			result.AppendLine("\t);");
			result.AppendLine("END;");
			result.AppendLine("/");
		}
		void GenerateInsteadOfDeleteTrigger(DBTable table, StringBuilder result, List<string> dropList) {
			string objName = ComposeSafeTableName(string.Format("t_{0}_xpoView_delete", table.Name));
			result.AppendLine(string.Format("CREATE TRIGGER \"{0}\"", objName));
			dropList.Add(string.Format("DROP TRIGGER \"{0}\";", objName));
			result.AppendLine("INSTEAD OF DELETE");
			result.AppendLine(string.Format("ON \"{0}_xpoView\"", table.Name));
			result.AppendLine("FOR EACH ROW");
			result.AppendLine("BEGIN");
			objName = ComposeSafeTableName(string.Format("sp_{0}_xpoView_delete", table.Name));
			result.AppendLine(string.Format("\t\"{0}\"(", objName));
			for (int i = 0; i < table.PrimaryKey.Columns.Count; i++) {
				if (i != 0) { result.AppendLine(","); }
				result.Append(string.Format("\t\t:old.\"{0}\"", table.PrimaryKey.Columns[i]));
			}
			for (int i = 0; i < table.Columns.Count; i++) {
				if (IsKey(table, table.Columns[i].Name)) { continue; }
				if (i != 0 || table.PrimaryKey.Columns.Count > 0) { result.AppendLine(","); }
				result.Append(string.Format("\t\t:old.\"{0}\"", table.Columns[i].Name));
			}
			result.AppendLine();
			result.AppendLine("\t);");
			result.AppendLine("END;");
			result.AppendLine("/");
		}
		static string GetRawType(string type) {
			int braceId = type.IndexOf('(');
			if (braceId < 0) { return type; }
			return type.Substring(0, braceId);
		}
		void AppendKeys(DBTable table, StringBuilder result) {
			for (int i = 0; i < table.PrimaryKey.Columns.Count; i++) {
				if (i != 0) { result.AppendLine(","); }
				DBColumn keyColumn = GetDbColumnByName(table, table.PrimaryKey.Columns[i]);
				string dbType = GetSqlCreateColumnType(table, keyColumn);
				result.Append(string.Format("\t{0}_ {1}", keyColumn.Name, GetRawType(dbType)));
			}
		}
		void AppendWhere(DBTable table, StringBuilder result) {
			for (int i = 0; i < table.PrimaryKey.Columns.Count; i++) {
				if (i != 0) { result.AppendLine(" AND"); }
				result.Append(string.Format("\t\t\"{0}\" = {0}_", table.PrimaryKey.Columns[i]));
			}
			result.AppendLine();
			result.AppendLine("\t;");
		}
		public override DBStoredProcedure[] GetStoredProcedures() {
			List<DBStoredProcedure> result = new List<DBStoredProcedure>();
			string query = "SELECT object_name FROM User_Procedures";
			IDbCommand cmd = CreateCommand(new Query(query));
			using (IDataReader rdr = cmd.ExecuteReader()) {
				while (rdr.Read()) {
					if (rdr[0] == DBNull.Value) continue;
					DBStoredProcedure proc = new DBStoredProcedure();
					proc.Name = (string)rdr[0];
					result.Add(proc);
				}
			}
			for (int i = result.Count - 1; i >= 0; i--) {
				query = string.Format("SELECT argument_name, data_type, data_length, data_precision, data_scale, CHAR_USED, CHAR_LENGTH in_out FROM all_arguments WHERE object_name = '{0}' ORDER BY position", result[i].Name);
				cmd = CreateCommand(new Query(query));
				bool skipProc = false;
				using (IDataReader rdr = cmd.ExecuteReader()) {
					while (rdr.Read()) {
						DBStoredProcedureArgument arg = new DBStoredProcedureArgument();
						arg.Name = rdr[0] == DBNull.Value ? string.Empty : (string)rdr[0];
						int size = rdr[2] == DBNull.Value ? 0 : Convert.ToInt32(rdr[2]);
						int precision = rdr[3] == DBNull.Value ? 0 : Convert.ToInt32(rdr[3]);
						int scale = rdr[4] == DBNull.Value ? 0 : Convert.ToInt32(rdr[4]);
						if (rdr[1] == DBNull.Value || rdr[5] == DBNull.Value) {
							skipProc = true;
							break;
						}
						string charLength = rdr[5] != DBNull.Value && rdr[6] != DBNull.Value ?
							string.Format("{0} {1}", ((IConvertible)rdr[6]).ToInt32(CultureInfo.InvariantCulture).ToString(), rdr[5].ToString() == "C" ? "CHAR" : "BYTE") : null;
						string fullTypeName;
						arg.Type = GetTypeFromString((string)rdr[1], size, precision, scale, charLength, out fullTypeName);
						string rawDirection = ((string)rdr[5]).ToUpper();
						switch (rawDirection) {
							case "IN":
								arg.Direction = DBStoredProcedureArgumentDirection.In;
								break;
							case "OUT":
								arg.Direction = DBStoredProcedureArgumentDirection.Out;
								break;
							case "INOUT":
								arg.Direction = DBStoredProcedureArgumentDirection.InOut;
								break;
							default:
								skipProc = true;
								break;
						}
						if (skipProc) break;
						result[i].Arguments.Add(arg);
					}
					if (skipProc) result.RemoveAt(i);
				}
			}
			return result.ToArray();
		}
	}
	public class OracleProviderFactory : ProviderFactory {
		public override IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return OracleConnectionProvider.CreateProviderFromConnection(connection, autoCreateOption);
		}
		public override IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			return OracleConnectionProvider.CreateProviderFromString(connectionString, autoCreateOption, out objectsToDisposeOnDisconnect);
		}
		public override string GetConnectionString(Dictionary<string, string> parameters) {
			if (!parameters.ContainsKey(ServerParamID) || !parameters.ContainsKey(UserIDParamID) || !parameters.ContainsKey(PasswordParamID)) { return null; }
			return OracleConnectionProvider.GetConnectionString(parameters[ServerParamID], parameters[UserIDParamID], parameters[PasswordParamID]);
		}
		public override IDataStore CreateProvider(Dictionary<string, string> parameters, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			string connectionString = this.GetConnectionString(parameters);
			if (connectionString == null) {
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
		public override bool IsServerbased { get { return true; } }
		public override bool IsFilebased { get { return false; } }
		public override string ProviderKey { get { return OracleConnectionProvider.XpoProviderTypeString; } }
		public override string[] GetDatabases(string server, string userId, string password) {
			return new string[1] { server };
		}
		public override string FileFilter { get { return null; } }
		public override bool MeanSchemaGeneration { get { return true; } }
		public override bool SupportStoredProcedures { get { return true; } }
	}
}
namespace DevExpress.Xpo.DB {
	using System.Data;
	using System;
	using DevExpress.Xpo.DB.Helpers;
	using DevExpress.Xpo.DB.Exceptions;
	using System.Collections.Generic;
	using DevExpress.Data.Filtering;
	using System.Reflection;
	public class OracleConnectionProvider : BaseOracleConnectionProvider {
		public const string XpoProviderTypeString = "Oracle";
		ReflectConnectionHelper helper;
		ReflectConnectionHelper ConnectionHelper {
			get {
				if(helper == null)
					helper = new ReflectConnectionHelper(Connection, "System.Data.OracleClient.OracleException");
				return helper;
			}
		}
		public static string GetConnectionString(string server, string userId, string password) {
			return string.Format("{3}={4};Data Source={0};user id={1}; password={2};", EscapeConnectionStringArgument(server), EscapeConnectionStringArgument(userId), EscapeConnectionStringArgument(password), DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString);
		}
		public static IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			IDbConnection connection = CreateConnection(connectionString);
			objectsToDisposeOnDisconnect = new IDisposable[] { connection };
			return CreateProviderFromConnection(connection, autoCreateOption);
		}
		public static IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return new OracleConnectionProvider(connection, autoCreateOption);
		}
		static OracleConnectionProvider() {
			RegisterDataStoreProvider(XpoProviderTypeString, new DataStoreCreationFromStringDelegate(CreateProviderFromString));
			RegisterDataStoreProvider("System.Data.OracleClient.OracleConnection", new DataStoreCreationFromConnectionDelegate(CreateProviderFromConnection));
			RegisterFactory(new OracleProviderFactory());
		}
		public static void Register() { }
		public OracleConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption)
			: base(connection, autoCreateOption) {
		}
		protected OracleConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption, bool openConnection)
			: base(connection, autoCreateOption, openConnection) {
		}
		bool HasUnicode(string value) {
			foreach(char c in value)
				if((int)c >= 256)
					return true;
			return false;
		}
		protected override IDataParameter CreateParameter(IDbCommand command, object value, string name, DBColumnType dbType, string dbTypeName, int size) {
			IDbDataParameter param = (IDbDataParameter)CreateParameter(command);
			bool isParameterTypeChanged = false;
			if(value is Guid) {
				string guid = value.ToString();
				param.Value = guid;
				((IDbTypeMapperBaseOracle)DbTypeMapper).SetOracleDbTypeChar(param, guid.Length);
				isParameterTypeChanged = true;
			} else {
				param.Value = value;
			}
			param.ParameterName = name;
			QueryParameterMode parameterMode = GetQueryParameterMode();
			if(!isParameterTypeChanged && parameterMode != QueryParameterMode.Legacy) {
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
			if(value is string && ((string)value).Length > 2000) {
				((IDbTypeMapperBaseOracle)DbTypeMapper).FixVarcharParameterType(param);
			}
			if(parameterMode == QueryParameterMode.SetTypeAndSize) {
				ValidateParameterSize(command, param);
			}
			return param;
		}
		protected override bool IsConnectionBroken(Exception e) {
			object codeObject;
			if(ConnectionHelper.TryGetExceptionProperty(e, "Code", out codeObject)) {
				int code = (int)codeObject;
				if(code == 0x311b || code == 3114 || code == 12152 || code == 3135) {
					Connection.Close();
					return true;
				}
			}
			return base.IsConnectionBroken(e);
		}
		protected override Exception WrapException(Exception e, IDbCommand query) {
			object codeObject;
			if(ConnectionHelper.TryGetExceptionProperty(e, "Code", out codeObject)) {
				int code = (int)codeObject;
				if(code == 0x388 || code == 0x3ae || code == 0x1996)
					return new SchemaCorrectionNeededException(e);
				if(code == 0x8f4 || code == 1)
					return new ConstraintViolationException(query.CommandText, GetParametersString(query), e);
			}
			return base.WrapException(e, query);
		}
		protected override object ConvertToDbParameter(object clientValue, TypeCode clientValueTypeCode) {
			switch(clientValueTypeCode) {
				case TypeCode.String:
					string stringValue = (string)clientValue;
					if(stringValue.Length == 0)
						return DBNull.Value;
					else
						return clientValue;
			}
			return base.ConvertToDbParameter(clientValue, clientValueTypeCode);
		}
		protected override IDbConnection CreateConnection() {
			return ConnectionHelper.GetConnection(ConnectionString);
		}
		static public IDbConnection CreateConnection(string connectionString) {
			return ReflectConnectionHelper.GetConnection("System.Data.OracleClient", "System.Data.OracleClient.OracleConnection", connectionString);
		}
		protected override bool IsFieldTypesNeeded { get { return true; } }
		ReflectionGetValuesHelperBase getValuesHelper;
		private ReflectionGetValuesHelperBase GetValuesHelper {
			get {
				if(getValuesHelper == null) {
					Type oracleDataReaderType = ConnectionHelper.GetType("System.Data.OracleClient.OracleDataReader");
					Type oracleNumberType = ConnectionHelper.GetType("System.Data.OracleClient.OracleNumber");
					Type oracleBooleanType = ConnectionHelper.GetType("System.Data.OracleClient.OracleBoolean");
					getValuesHelper = (ReflectionGetValuesHelperBase)Activator.CreateInstance(typeof(ReflectionGetValuesHelper<,,>).MakeGenericType(oracleDataReaderType, oracleNumberType, oracleBooleanType));
				}
				return getValuesHelper;
			}
		}
		DbTypeMapperBase dbTypeMapper;
		protected override DbTypeMapperBase DbTypeMapper {
			get {
				if(dbTypeMapper == null) {
					Type oracleParameterType = ConnectionHelper.GetType("System.Data.OracleClient.OracleParameter");
					Type oracleTypeType = ConnectionHelper.GetType("System.Data.OracleClient.OracleType");
					dbTypeMapper = (DbTypeMapperBase)Activator.CreateInstance(typeof(DbTypeMapperOracle<,>).MakeGenericType(oracleTypeType, oracleParameterType));
				}
				return dbTypeMapper;
			}
		}
		protected override void GetValues(IDataReader reader, Type[] fieldTypes, object[] values) {
			if(GetValuesHelper.GetValues(reader, fieldTypes, values)) return;
			base.GetValues(reader, fieldTypes, values);
		}
		ExecMethodDelegate commandBuilderDeriveParametersHandler;
		protected override void CommandBuilderDeriveParameters(IDbCommand command) {
			if(commandBuilderDeriveParametersHandler == null) {
				commandBuilderDeriveParametersHandler = ReflectConnectionHelper.GetCommandBuilderDeriveParametersDelegate("System.Data.OracleClient", "System.Data.OracleClient.OracleCommandBuilder");
			}
			commandBuilderDeriveParametersHandler(command);
		}
		class ReflectionGetValuesHelperBase {
			public virtual bool GetValues(IDataReader reader, Type[] fieldTypes, object[] values) {
				return false;
			}
		}
		class ReflectionGetValuesHelper<R, N, B> : ReflectionGetValuesHelperBase where R : IDataReader, IDataRecord {
			static readonly N oracleNumberMax;
			static readonly N oracleNumberMin;
			static readonly N oracleNumberZero;
			static readonly N oracleNumberTen;
			static readonly N oracleNumberMagicMantissaMax;
			static readonly OracleDataReaderGetOracleNumberDelegate getOracleNumber;
			static readonly OracleNumberComparisonDelegate oDGreaterThan;
			static readonly OracleNumberComparisonDelegate oDEquals;
			static readonly OracleNumberOperationTwoArgs oDDivide;
			static readonly OracleNumberOperationWithInt oDPow;
			static readonly OracleNumberOperationWithInt oDTruncate;
			static readonly OracleNumberOperationWithInt oDRound;
			static readonly OracleNumberOperation oDLog;
			static readonly OracleNumberOperation oDAbs;
			static readonly OracleNumberToDecimal oDToDecimal;
			static readonly OracleBooleanToBoolean oBToBoolean;
			static ReflectionGetValuesHelper() {
				Type oracleNumberType = typeof(N);
				Type oracleBooleanType = typeof(B);
				oracleNumberMax = (N)Activator.CreateInstance(oracleNumberType, Decimal.MaxValue);
				oracleNumberMin = (N)Activator.CreateInstance(oracleNumberType, Decimal.MinValue);
				oracleNumberMagicMantissaMax = (N)Activator.CreateInstance(oracleNumberType, Decimal.MaxValue / (decimal)Math.Pow(10, 28));
				oracleNumberZero = (N)Activator.CreateInstance(oracleNumberType, Decimal.Zero);
				oracleNumberTen = (N)Activator.CreateInstance(oracleNumberType, 10);
				MethodInfo mi = typeof(R).GetMethod("GetOracleNumber", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(int) }, null);
				getOracleNumber = (OracleDataReaderGetOracleNumberDelegate)Delegate.CreateDelegate(typeof(OracleDataReaderGetOracleNumberDelegate), null, mi);
				mi = oracleNumberType.GetMethod("GreaterThan", BindingFlags.Public | BindingFlags.Static, null, new Type[] { oracleNumberType, oracleNumberType }, null);
				oDGreaterThan = (OracleNumberComparisonDelegate)Delegate.CreateDelegate(typeof(OracleNumberComparisonDelegate), mi);
				mi = oracleNumberType.GetMethod("Equals", BindingFlags.Public | BindingFlags.Static, null, new Type[] { oracleNumberType, oracleNumberType }, null);
				oDEquals = (OracleNumberComparisonDelegate)Delegate.CreateDelegate(typeof(OracleNumberComparisonDelegate), mi);
				mi = oracleNumberType.GetMethod("Divide", BindingFlags.Public | BindingFlags.Static, null, new Type[] { oracleNumberType, oracleNumberType }, null);
				oDDivide = (OracleNumberOperationTwoArgs)Delegate.CreateDelegate(typeof(OracleNumberOperationTwoArgs), mi);
				mi = oracleNumberType.GetMethod("Pow", BindingFlags.Public | BindingFlags.Static, null, new Type[] { oracleNumberType, typeof(int) }, null);
				oDPow = (OracleNumberOperationWithInt)Delegate.CreateDelegate(typeof(OracleNumberOperationWithInt), mi);
				mi = oracleNumberType.GetMethod("Truncate", BindingFlags.Public | BindingFlags.Static, null, new Type[] { oracleNumberType, typeof(int) }, null);
				oDTruncate = (OracleNumberOperationWithInt)Delegate.CreateDelegate(typeof(OracleNumberOperationWithInt), mi);
				mi = oracleNumberType.GetMethod("Round", BindingFlags.Public | BindingFlags.Static, null, new Type[] { oracleNumberType, typeof(int) }, null);
				oDRound = (OracleNumberOperationWithInt)Delegate.CreateDelegate(typeof(OracleNumberOperationWithInt), mi);
				mi = oracleNumberType.GetMethod("Log10", BindingFlags.Public | BindingFlags.Static, null, new Type[] { oracleNumberType }, null);
				oDLog = (OracleNumberOperation)Delegate.CreateDelegate(typeof(OracleNumberOperation), mi);
				mi = oracleNumberType.GetMethod("Abs", BindingFlags.Public | BindingFlags.Static, null, new Type[] { oracleNumberType }, null);
				oDAbs = (OracleNumberOperation)Delegate.CreateDelegate(typeof(OracleNumberOperation), mi);
				MethodInfo[] miList = oracleNumberType.GetMethods(BindingFlags.Public | BindingFlags.Static);
				for(int i = 0; i < miList.Length; i++) {
					MethodInfo currentMi = miList[i];
					if(currentMi.Name == "op_Explicit" && currentMi.ReturnType == typeof(decimal)) {
						oDToDecimal = (OracleNumberToDecimal)Delegate.CreateDelegate(typeof(OracleNumberToDecimal), currentMi);
					}
				}
				miList = oracleBooleanType.GetMethods(BindingFlags.Public | BindingFlags.Static);
				for(int i = 0; i < miList.Length; i++) {
					MethodInfo currentMi = miList[i];
					if(currentMi.Name == "op_Explicit" && currentMi.ReturnType == typeof(Boolean)) {
						oBToBoolean = (OracleBooleanToBoolean)Delegate.CreateDelegate(typeof(OracleBooleanToBoolean), currentMi);
					}
				}
				if(oDToDecimal == null || oBToBoolean == null) throw new InvalidOperationException("Methods 'ToDecimal' or 'ToBoolean' not found.");
			}
			public override bool GetValues(IDataReader reader, Type[] fieldTypes, object[] values) {
				if(fieldTypes == null || !(reader is R)) {
					return false;
				}
				R oReader = (R)reader;
				for(int i = fieldTypes.Length - 1; i >= 0; i--) {
					if(oReader.IsDBNull(i)) {
						values[i] = DBNull.Value;
						continue;
					}
					if(fieldTypes[i].Equals(typeof(decimal))) {
						values[i] = GetDecimal(oReader, i);
						continue;
					}
					values[i] = oReader.GetValue(i);
				}
				return true;
			}
			object GetDecimal(R dataReader, int i) {
				N od = getOracleNumber(dataReader, i);
				if (od.Equals(oracleNumberZero)) {
					return 0M;
				}
				int exp = (int)oDToDecimal(oDTruncate(oDLog(oDAbs(od)), 0));
				N oPow = oDPow(oracleNumberTen, exp);
				if (oBToBoolean(oDGreaterThan(od, oracleNumberMax)) || oBToBoolean(oDGreaterThan(oracleNumberMin, od))) {
					N oMantissa = oDRound(oDDivide(od, oPow), 14);
					return  ((double)oDToDecimal(oMantissa)) * Math.Pow(10, exp);
				}
				else {
					N oMantissa = oDDivide(od, oPow);
					oMantissa = oDTruncate(oMantissa, oBToBoolean(oDGreaterThan(oDAbs(oMantissa), oracleNumberMagicMantissaMax)) ? 26 : 27);
					return oDToDecimal(oMantissa) * (decimal)Math.Pow(10, exp);
				}
			}
			delegate N OracleDataReaderGetOracleNumberDelegate(R reader, int i);
			delegate B OracleNumberComparisonDelegate(N left, N right);
			delegate N OracleNumberOperationWithInt(N od, int i);
			delegate N OracleNumberOperation(N od);
			delegate N OracleNumberOperationTwoArgs(N left, N right);
			delegate double OracleNumberToDouble(N od);
			delegate decimal OracleNumberToDecimal(N od);
			delegate bool OracleBooleanToBoolean(B ob);
		}
	}
}
#pragma warning restore DX0024
namespace DevExpress.Xpo.DB.Helpers {
	using DevExpress.Xpo.DB;
	using System;
	using System.Collections.Generic;
	using System.Data;
	using System.Globalization;
	using System.Text;
	interface IDbTypeMapperBaseOracle {
		void SetOracleDbTypeChar(IDbDataParameter parameter, int size);
		void FixVarcharParameterType(IDbDataParameter parameter);
	}
	abstract class DbTypeMapperBaseOracle<TSqlDbTypeEnum, TSqlParameter> : DbTypeMapper<TSqlDbTypeEnum, TSqlParameter>
		where TSqlDbTypeEnum : struct
		where TSqlParameter : IDbDataParameter {
		protected static readonly TSqlDbTypeEnum OracleDbTypeChar;
		protected static readonly TSqlDbTypeEnum OracleDbTypeNClob;
		protected static readonly TSqlDbTypeEnum OracleDbTypeBlob;
		static DbTypeMapperBaseOracle() {
			OracleDbTypeChar = (TSqlDbTypeEnum)Enum.Parse(typeof(TSqlDbTypeEnum), "Char");
			OracleDbTypeNClob = (TSqlDbTypeEnum)Enum.Parse(typeof(TSqlDbTypeEnum), "NClob");
			OracleDbTypeBlob = (TSqlDbTypeEnum)Enum.Parse(typeof(TSqlDbTypeEnum), "Blob");
		}
		protected override string GetParameterTypeNameForByteArray(out int? size) {
			size = null;
			return "Raw";
		}
		protected override string GetParameterTypeNameForChar(out int? size) {
			size = null;
			return "NChar";
		}
		protected override string GetParameterTypeNameForDouble(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Double";
		}
		protected override string GetParameterTypeNameForGuid(out int? size) {
			size = 36;
			return "Char";
		}
		protected override string GetParameterTypeNameForInt32(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Int32";
		}
		protected override string GetParameterTypeNameForTimeSpan() {
			return "Double";
		}
		protected override string ConvertSqlTypeToParameterType(string sqlType) {
			switch(sqlType.ToUpperInvariant()) {
				case "CHAR":
					return "Char";
				case "NCHAR":
					return "NChar";
				case "RAW":
					return "Raw";
				case "LONG RAW":
					return "LongRaw";
				case "INTEGER":
				case "INT":
					return "Int32";
				case "SMALLINT":
					return "Int16";
				case "DOUBLE PRECISION":
					return "Double";
				case "BFILE":
					return "BFile";
				case "BLOB":
					return "Blob";
				case "CLOB":
					return "Clob";
				case "NCLOB":
					return "NClob";
				default:
					return null;
			}
		}
		protected override DBTypeInfoBase CustomParseSqlType(string sqlTypeWithoutParameters, string sqlTypeParameters, string sqlTypeSuffix) {
			if(string.Equals(sqlTypeWithoutParameters, "TIMESTAMP", StringComparison.OrdinalIgnoreCase)) {
				if(string.Equals(sqlTypeSuffix, "WITH LOCAL TIME ZONE", StringComparison.OrdinalIgnoreCase)) {
					return CreateParameterDBTypeInfo("TIMESTAMP WITH LOCAL TIME ZONE", null, null, null);
				} else if(string.Equals(sqlTypeSuffix, "WITH TIME ZONE", StringComparison.OrdinalIgnoreCase)) {
					return CreateParameterDBTypeInfo("TIMESTAMP WITH TIME ZONE", null, null, null);
				}
			} else if(string.Equals(sqlTypeWithoutParameters, "INTERVAL DAY", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(sqlTypeWithoutParameters, "INTERVAL YEAR", StringComparison.OrdinalIgnoreCase)) {
				return CreateParameterDBTypeInfo(sqlTypeWithoutParameters, null, null, null);
			}
			return base.CustomParseSqlType(sqlTypeWithoutParameters, sqlTypeParameters, sqlTypeSuffix);
		}
		public override void SetParameterTypeAndSize(IDbDataParameter parameter, DBColumnType dbColumnType, int size) {
			switch(dbColumnType) {
				case DBColumnType.String:
					if(size < 0 || size > BaseOracleConnectionProvider.MaximumStringSize) {
						SetSqlDbTypeHandler((TSqlParameter)parameter, OracleDbTypeNClob);
						parameter.Size = 0;
						return;
					}
					break;
				case DBColumnType.ByteArray:
					if(size < 0 || size >= BaseOracleConnectionProvider.MaximumBinarySize) {
						SetSqlDbTypeHandler((TSqlParameter)parameter, OracleDbTypeBlob);
						parameter.Size = 0;
						return;
					}
					break;
			}
			base.SetParameterTypeAndSize(parameter, dbColumnType, size);
		}
	}
	class DbTypeMapperOracle<TSqlDbTypeEnum, TSqlParameter> : DbTypeMapperBaseOracle<TSqlDbTypeEnum, TSqlParameter>, IDbTypeMapperBaseOracle
		where TSqlDbTypeEnum : struct
		where TSqlParameter : IDbDataParameter {
		static readonly TSqlDbTypeEnum OracleDbTypeVarchar;
		static DbTypeMapperOracle() {
			OracleDbTypeVarchar = (TSqlDbTypeEnum)Enum.Parse(typeof(TSqlDbTypeEnum), "VarChar");
		}
		protected override string ParameterDbTypePropertyName { get { return "OracleType"; } }
		protected override string GetParameterTypeNameForBoolean(out int? size, out byte? precision, out byte? scale) {
			size = null;
			precision = 1;
			scale = 0;
			return "Number";
		}
		protected override string GetParameterTypeNameForByte(out byte? precision, out byte? scale) {
			precision = 3;
			scale = 0;
			return "Number";
		}
		protected override string GetParameterTypeNameForDecimal(out byte? precision, out byte? scale) {
			precision = 19;
			scale = 5;
			return "Number";
		}
		protected override string GetParameterTypeNameForInt16(out byte? precision, out byte? scale) {
			precision = 5;
			scale = 0;
			return "Number";
		}
		protected override string GetParameterTypeNameForInt64(out byte? precision, out byte? scale) {
			precision = 20;
			scale = 0;
			return "Number";
		}
		protected override string GetParameterTypeNameForSByte(out byte? precision, out byte? scale) {
			precision = 3;
			scale = 0;
			return "Number";
		}
		protected override string GetParameterTypeNameForSingle(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Float";
		}
		protected override string GetParameterTypeNameForUInt16(out byte? precision, out byte? scale) {
			precision = 5;
			scale = 0;
			return "Number";
		}
		protected override string GetParameterTypeNameForUInt32(out byte? precision, out byte? scale) {
			precision = 10;
			scale = 0;
			return "Number";
		}
		protected override string GetParameterTypeNameForUInt64(out byte? precision, out byte? scale) {
			precision = 20;
			scale = 0;
			return "Number";
		}
		protected override string GetParameterTypeNameForString(out int? size) {
			size = null;
			return "NVarChar";
		}
		protected override string GetParameterTypeNameForDateTime() {
			return "DateTime";
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
			switch(sqlType.ToUpperInvariant()) {
				case "NVARCHAR":
				case "NVARCHAR2":
					return "NVarChar";
				case "VARCHAR":
				case "VARCHAR2":
					return "VarChar";
				case "NUMBER":
				case "NUMERIC":
				case "DEC":
				case "DECIMAL":
					return "Number";
				case "REAL":
				case "FLOAT":
					return "Float";
				case "LONG":
					return "LongVarChar";
				case "DATE":
					return "DateTime";
				case "TIMESTAMP":
					return "Timestamp";
				case "TIMESTAMP WITH LOCAL TIME ZONE":
					return "TimestampLocal";
				case "TIMESTAMP WITH TIME ZONE":
					return "TimestampWithTZ";
				case "INTERVAL YEAR":
					return "IntervalYearToMonth";
				case "INTERVAL DAY":
					return "IntervalDayToSecond";
				case "ROWID":
					return "RowId";
				case "UROWID":
					return "VarChar";
				case "XMLTYPE":
					return null;
				default:
					return base.ConvertSqlTypeToParameterType(sqlType);
			}
		}
		public void SetOracleDbTypeChar(IDbDataParameter parameter, int size) {
			SetSqlDbTypeHandler((TSqlParameter)parameter, OracleDbTypeChar);
			parameter.Size = size;
		}
		public void FixVarcharParameterType(IDbDataParameter parameter) {
			var param = (TSqlParameter)parameter;
			if(object.Equals(GetSqlDbTypeHandler(param), OracleDbTypeVarchar))
				SetSqlDbTypeHandler(param, OracleDbTypeNClob);
		}
	}
	class OracleUpdateSchemaSqlFormatterHelper : UpdateSchemaSqlFormatterHelper {
		Func<string, string> GetSequenceNameForTable;
		public OracleUpdateSchemaSqlFormatterHelper(
			ISqlGeneratorFormatter sqlGeneratorFormatter,
			Func<DBTable, DBColumn, bool, string> getSqlCreateColumnFullAttributes,
			Func<string, string> formatConstraintSafe,
			Func<DBIndex, DBTable, string> getIndexName,
			Func<DBForeignKey, DBTable, string> getForeignKeyName,
			Func<DBPrimaryKey, DBTable, string> getPrimaryKeyName,
			Func<string,string> getSequenceNameForTable)
			: base(sqlGeneratorFormatter, getSqlCreateColumnFullAttributes,
				formatConstraintSafe, getIndexName, getForeignKeyName, getPrimaryKeyName) {
			this.GetSequenceNameForTable = getSequenceNameForTable;
		}
		protected override string[] FormatCreateTable(CreateTableStatement statement) {
			var statements = new List<string>();
			statements.AddRange(base.FormatCreateTable(statement));
			if(statement.Table.PrimaryKey != null) {
				DBColumn key = statement.Table.GetColumn(statement.Table.PrimaryKey.Columns[0]);
				if(key.IsIdentity) {
					statements.Add(FormatCreateSequence(statement.Table.Name));
				} else {
					statements.AddRange(FormatCreatePrimaryKey(new CreatePrimaryKeyStatement(statement.Table, statement.Table.PrimaryKey.Columns)));
				}
			}
			return statements.ToArray();
		}
		protected override string[] FormatRenameTable(RenameTableStatement statement) {
			var statements = new List<string>();
			statements.Add(string.Format("rename {0} to {1}", FormatTableSafe(statement.Table.Name), FormatTableSafe(statement.NewTableName)));
			if(statement.Table.PrimaryKey != null) {
				DBColumn key = statement.Table.GetColumn(statement.Table.PrimaryKey.Columns[0]);
				if(key.IsIdentity) {
					string oldSeqName = GetSequenceNameForTable(statement.Table.Name);
					string newSeqName = GetSequenceNameForTable(statement.NewTableName);
					statements.Add(string.Format("rename {0} to {1}", oldSeqName, newSeqName));
				}
			}
			return statements.ToArray();
		}
		protected override string[] FormatRenameColumn(string tableName, string oldColumnName, string newColumnName) {
			newColumnName = SqlGeneratorFormatter.FormatColumn(SqlGeneratorFormatter.ComposeSafeColumnName(newColumnName));
			oldColumnName = SqlGeneratorFormatter.FormatColumn(SqlGeneratorFormatter.ComposeSafeColumnName(oldColumnName));
			return new string[]{
			   string.Format("alter table {0} rename column {1} to {2}", FormatTableSafe(tableName), oldColumnName, newColumnName)
			};
		}
		protected override string[] FormatAlterColumn(AlterColumnStatement statement) {
			var statements = new List<string>(1);
			if(statement.OldColumn.IsIdentity != statement.NewColumn.IsIdentity) {
				if(statement.NewColumn.IsIdentity == true) {
					statements.Add(FormatCreateSequence(statement.Table.Name));
				} else {
					statements.Add(FormatDropSequence(statement.Table.Name));
				}
			}
			string newColumnName = SqlGeneratorFormatter.ComposeSafeColumnName(statement.NewColumn.Name);
			string oldColumnName = SqlGeneratorFormatter.ComposeSafeColumnName(statement.OldColumn.Name);
			if(!string.Equals(oldColumnName, newColumnName, StringComparison.OrdinalIgnoreCase)) {
				statements.AddRange(FormatRenameColumn(statement.Table.Name, oldColumnName, newColumnName));
			}
			if(statement.OldColumn.IsNullable != statement.NewColumn.IsNullable) {
				if(statement.NewColumn.IsNullable) {
					if(!statement.OldColumn.IsIdentity) {
						string sql = string.Format(AlterColumnTemplate,
							FormatTableSafe(statement.Table.Name), FormatColumnSafe(newColumnName), "NULL");
						statements.Add(sql);
					}
				} else {
					string sql = string.Format(AlterColumnTemplate,
						FormatTableSafe(statement.Table.Name), FormatColumnSafe(newColumnName), "NOT NULL");
					statements.Add(sql);
				}				
			}
			string oldColumnSql = GetSqlCreateColumnFullAttributes(statement.Table, statement.OldColumn, false);
			string newColumnSql = GetSqlCreateColumnFullAttributes(statement.Table, statement.NewColumn, false);
			const string notNullConstraint = " NOT NULL";
			if(newColumnSql.EndsWith(notNullConstraint)) {
				newColumnSql = newColumnSql.Remove(newColumnSql.Length - notNullConstraint.Length);
			}
			if(oldColumnSql != newColumnSql) {
				string sql = string.Format(AlterColumnTemplate,
					 FormatTableSafe(statement.Table.Name), FormatColumnSafe(newColumnName), newColumnSql);
				statements.Add(sql);
			}
			return statements.ToArray();
		}
		protected override string[] FormatDropTable(DropTableStatement statement) {
			var statements = new List<string>();
			statements.AddRange(base.FormatDropTable(statement));
			if(statement.Table.PrimaryKey != null) {
				DBColumn key = statement.Table.GetColumn(statement.Table.PrimaryKey.Columns[0]);
				if(key.IsIdentity) {
					statements.Add(FormatDropSequence(statement.Table.Name));
				}
			}
			return statements.ToArray();
		}
		protected override string[] FormatDropColumn(DropColumnStatement statement) {
			var statements = new List<string>();
			statements.AddRange(base.FormatDropColumn(statement));
			if(statement.Table.PrimaryKey != null) {
				DBColumn column = statement.Table.GetColumn(statement.ColumnName);
				if(column.IsIdentity) {
					statements.Add(FormatDropSequence(statement.Table.Name));
				}
			}
			return statements.ToArray();
		}
		protected override string[] FormatDropIndex(DropIndexStatement statement) {
			string sql = string.Format("drop index {0}", FormatConstraintSafe(statement.IndexName));
			return new string[] { sql };
		}
		string FormatCreateSequence(string tableName) {
			string seqName = GetSequenceNameForTable(tableName);
			return string.Format(CultureInfo.InvariantCulture, "create sequence {0} START WITH 1 INCREMENT BY 1", seqName);
		}
		string FormatDropSequence(string tableName) {
			string seqName = GetSequenceNameForTable(tableName);
			return string.Format("drop sequence {0}", seqName);
		}
		protected override string AlterColumnTemplate {
			get { return "alter table {0} modify({1} {2})"; }
		}
	}
}
