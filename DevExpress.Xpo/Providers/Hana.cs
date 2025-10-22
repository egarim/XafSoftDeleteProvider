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
	using DevExpress.Xpo.DB.Exceptions;
	using DevExpress.Xpo.DB.Helpers;
#pragma warning disable DX0024
	public class HanaConnectionProvider : ConnectionProviderSql {
		ReflectConnectionHelper helper;
		ReflectConnectionHelper ConnectionHelper {
			get {
				if(helper == null)
					helper = new ReflectConnectionHelper(Connection, "Sap.Data.Hana.HanaException");
				return helper;
			}
		}
		public string ObjectsOwner = String.Empty;
		public HanaConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption) : this(connection, autoCreateOption, true) {
		}
		protected HanaConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption, bool openConnection)
			: base(connection, autoCreateOption, openConnection) {
			ConnectionStringParser parser = new ConnectionStringParser(ConnectionString);
			string userId = parser.GetPartByName("UserId");
			if(!String.IsNullOrEmpty(userId)) {
				ObjectsOwner = userId;
			}
		}
		protected override IDbConnection CreateConnection() {
			return ConnectionHelper.GetConnection(ConnectionString);
		}
		protected override void CreateDataBase() {
			try {
				Connection.Open();
			}
			catch(Exception ex) {
				throw new UnableToOpenDatabaseException(XpoDefault.ConnectionStringRemovePassword(ConnectionString), ex);
			}
		}
		protected override void ProcessClearDatabase() {
			using(IDbCommand command = CreateCommand()) {
				string[] tables = GetStorageTablesList(false, false).Keys.ToArray();
				foreach(string table in tables) {
					string schema = GetSchemaName(table);
					string tableName = GetTableName(table);
					command.CommandText = string.Format("DROP TABLE {0} CASCADE", FormatTable(schema, tableName));
					command.ExecuteNonQuery();
				}
			}
		}
		public override ICollection CollectTablesToCreate(ICollection tables) {
			var dbTableNames = GetStorageTablesList(true, false);
			var list = new List<DBTable>();
			foreach(DBTable table in tables) {
				var objectName = ComposeSafeObjectName(table.Name);
				string fullName = string.Concat(objectName.SchemaName, ".", objectName.TableName);
				bool isView;
				if(dbTableNames.TryGetValue(fullName, out isView)) {
					table.IsView = isView;
				}
				else {
					list.Add(table);
				}
			}
			return list;
		}
		string GetSchemaName(string table) {
			int dot = table.IndexOf('.');
			if(dot > 0) { return table.Substring(0, dot); }
			return string.Empty;
		}
		string GetTableName(string table) {
			int dot = table.IndexOf('.');
			if(dot > 0)
				return table.Remove(0, dot + 1);
			return table;
		}
		#region Formatting
		public override string FormatColumn(string columnName) {
			return string.Format(CultureInfo.InvariantCulture, "\"{0}\"", columnName);
		}
		public override string FormatColumn(string columnName, string tableAlias) {
			return string.Format(CultureInfo.InvariantCulture, "{1}.\"{0}\"", columnName, tableAlias);
		}
		public override string FormatConstraint(string constraintName) {
			return string.Format(CultureInfo.InvariantCulture, "\"{0}\"", ComposeSafeConstraintName(constraintName));
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
		public override string FormatSelect(string selectedPropertiesSql, string fromSql, string whereSql, string orderBySql, string groupBySql, string havingSql, int skipSelectedRecords, int topSelectedRecords) {
			base.FormatSelect(selectedPropertiesSql, fromSql, whereSql, orderBySql, groupBySql, havingSql, skipSelectedRecords, topSelectedRecords);
			string modificatorsSql = string.Empty;
			if(topSelectedRecords > 0 || skipSelectedRecords > 0) {
				modificatorsSql = string.Format(CultureInfo.InvariantCulture, "{1}LIMIT {0}", topSelectedRecords > 0 ? topSelectedRecords : Int32.MaxValue, Environment.NewLine);
				if(skipSelectedRecords > 0) {
					modificatorsSql = string.Concat(modificatorsSql, string.Format(CultureInfo.InvariantCulture, " OFFSET {0}", skipSelectedRecords));
				}
			}
			string expandedWhereSql = whereSql != null ? string.Concat(Environment.NewLine, "where ", whereSql) : string.Empty;
			string expandedOrderBySql = orderBySql != null ? string.Concat(Environment.NewLine, "order by ", orderBySql) : string.Empty;
			string expandedHavingSql = havingSql != null ? string.Concat(Environment.NewLine, "having ", havingSql) : string.Empty;
			string expandedGroupBySql = groupBySql != null ? string.Concat(Environment.NewLine, "group by ", groupBySql) : string.Empty;
			return string.Concat("select ", selectedPropertiesSql, " from ", fromSql, expandedWhereSql, expandedGroupBySql, expandedHavingSql, expandedOrderBySql, modificatorsSql);
		}
		protected override long GetIdentity(Query sql) {
			int result = ExecSql(sql);
			Query returnIdSql = new Query("SELECT CURRENT_IDENTITY_VALUE() ID FROM DUMMY");
			object value = GetScalar(returnIdSql);
			return Convert.ToInt64(value, CultureInfo.InvariantCulture);
		}
		protected override async Task<long> GetIdentityAsync(Query sql, AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken) {
			int result = await ExecSqlAsync(sql, asyncOperationId, cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			Query returnIdSql = new Query("SELECT CURRENT_IDENTITY_VALUE() ID FROM DUMMY");
			object value = await GetScalarAsync(returnIdSql, asyncOperationId, cancellationToken).ConfigureAwait(false);
			return Convert.ToInt64(value, CultureInfo.InvariantCulture);
		}
		public override string FormatInsert(string tableName, string fields, string values) {
			return string.Format(CultureInfo.InvariantCulture, "insert into {0} ({1}) values({2})", tableName, fields, values);
		}
		public override string FormatInsertDefaultValues(string tableName) {
			throw new NotSupportedException();
		}
		public override string FormatUpdate(string tableName, string sets, string whereClause) {
			return string.Format(CultureInfo.InvariantCulture, "update {0} set {1} where {2}", tableName, sets, whereClause);
		}
		public override string FormatDelete(string tableName, string whereClause) {
			return string.Format(CultureInfo.InvariantCulture, "delete from {0} where {1}", tableName, whereClause);
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
					return ((bool)value) ? "TRUE" : "FALSE";
				case TypeCode.Char:
					int code = Convert.ToInt32(value);
					return FormatString(((char)value).ToString(CultureInfo.InvariantCulture));
				case TypeCode.DateTime:
					DateTime datetimeValue = (DateTime)value;
					string dateTimeFormatPattern = "yyyy-MM-dd HH:mm:ss.fffffff";
					return "'" + datetimeValue.ToString(dateTimeFormatPattern, CultureInfo.InvariantCulture) + "'";
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
					return Convert.ToString(value, CultureInfo.InvariantCulture);
				case TypeCode.UInt64:
					if(value is Enum)
						return Convert.ToUInt64(value).ToString();
					return Convert.ToString(value, CultureInfo.InvariantCulture);
				case TypeCode.Object:
				default:
					if(value is Guid) {
						return "'" + ((Guid)value).ToString() + "'";
					}
					else if(value is TimeSpan) {
						return FixNonFixedText(((TimeSpan)value).TotalSeconds.ToString("r", CultureInfo.InvariantCulture));
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
		protected string FormatString(object value) {
			return "'" + ((string)value).Replace("'", "''") + "'";
		}
		public override string FormatBinary(BinaryOperatorType operatorType, string leftOperand, string rightOperand) {
			switch(operatorType) {
				case BinaryOperatorType.Modulo:
					return string.Format(CultureInfo.InvariantCulture, "MOD({0},{1})", leftOperand, rightOperand);
				case BinaryOperatorType.BitwiseAnd:
					return string.Format(CultureInfo.InvariantCulture, "BITAND({0},{1})", leftOperand, rightOperand);
				case BinaryOperatorType.BitwiseOr:
					return string.Format(CultureInfo.InvariantCulture, "BITOR({0},{1})", leftOperand, rightOperand);
				case BinaryOperatorType.BitwiseXor:
					return string.Format(CultureInfo.InvariantCulture, "BITXOR({0},{1})", leftOperand, rightOperand);
				default:
					return base.FormatBinary(operatorType, leftOperand, rightOperand);
			}
		}
		public override string FormatFunction(FunctionOperatorType operatorType, params string[] operands) {
			switch(operatorType) {
				case FunctionOperatorType.Sqr:
					return string.Format(CultureInfo.InvariantCulture, "SQRT({0})", operands[0]);
				case FunctionOperatorType.Acos:
					return string.Format(CultureInfo.InvariantCulture, "ACOS({0})", operands[0]);
				case FunctionOperatorType.Asin:
					return string.Format(CultureInfo.InvariantCulture, "ASIN({0})", operands[0]);
				case FunctionOperatorType.Atn:
					return string.Format(CultureInfo.InvariantCulture, "ATAN({0})", operands[0]);
				case FunctionOperatorType.Atn2:
					return string.Format(CultureInfo.InvariantCulture, "ATAN2({0},{1})", operands[0], operands[1]);
				case FunctionOperatorType.Cosh:
					return string.Format(CultureInfo.InvariantCulture, "COSH({0})", operands[0]);
				case FunctionOperatorType.Sinh:
					return string.Format(CultureInfo.InvariantCulture, "SINH({0})", operands[0]);
				case FunctionOperatorType.Tanh:
					return string.Format(CultureInfo.InvariantCulture, "TANH({0})", operands[0]);
				case FunctionOperatorType.Log:
					return FnLog(operands);
				case FunctionOperatorType.Log10:
					return string.Format(CultureInfo.InvariantCulture, "LOG(10,{0})", operands[0]);
				case FunctionOperatorType.BigMul:
					return string.Format(CultureInfo.InvariantCulture, "(TO_BIGINT({0})*TO_BIGINT({1}))", operands[0], operands[1]);
				case FunctionOperatorType.Max:
					return string.Format(CultureInfo.InvariantCulture, "GREATEST({0},{1})", operands[0], operands[1]);
				case FunctionOperatorType.Min:
					return string.Format(CultureInfo.InvariantCulture, "LEAST({0},{1})", operands[0], operands[1]);
				case FunctionOperatorType.Rnd:
					return "RAND()";
				case FunctionOperatorType.Round:
					return FnRound(operands);
				case FunctionOperatorType.AddYears:
					return string.Format(CultureInfo.InvariantCulture, "ADD_YEARS({0},{1})", operands[0], operands[1]);
				case FunctionOperatorType.AddMonths:
					return string.Format(CultureInfo.InvariantCulture, "ADD_MONTHS({0},{1})", operands[0], operands[1]);
				case FunctionOperatorType.AddDays:
					return string.Format(CultureInfo.InvariantCulture, "ADD_NANO100({0},TO_DECIMAL({1},37,8)*864000000000)", operands[0], operands[1]);
				case FunctionOperatorType.AddHours:
					return string.Format(CultureInfo.InvariantCulture, "ADD_NANO100({0},TO_DECIMAL({1},37,8)*36000000000)", operands[0], operands[1]);
				case FunctionOperatorType.AddMinutes:
					return string.Format(CultureInfo.InvariantCulture, "ADD_NANO100({0},TO_DECIMAL({1},37,8)*600000000)", operands[0], operands[1]);
				case FunctionOperatorType.AddSeconds:
				case FunctionOperatorType.AddTimeSpan:
					return string.Format(CultureInfo.InvariantCulture, "ADD_NANO100({0},TO_DECIMAL({1},37,8)*10000000)", operands[0], operands[1]);
				case FunctionOperatorType.AddMilliSeconds:
					return string.Format(CultureInfo.InvariantCulture, "ADD_NANO100({0},TO_DECIMAL({1},37,8)*10000)", operands[0], operands[1]);
				case FunctionOperatorType.AddTicks:
					return string.Format(CultureInfo.InvariantCulture, "ADD_NANO100({0},{1})", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffYear:
					return string.Format(CultureInfo.InvariantCulture, "(YEARS_BETWEEN({0},{1})+1)", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffMonth:
					return string.Format(CultureInfo.InvariantCulture, "(MONTHS_BETWEEN({0},{1})+1)", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffDay:
					return string.Format(CultureInfo.InvariantCulture, "(DAYS_BETWEEN({0},{1})+1)", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffHour:
					return string.Format(CultureInfo.InvariantCulture, "(FLOOR(SECONDS_BETWEEN({0},{1})/3600)+1)", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffMinute:
					return string.Format(CultureInfo.InvariantCulture, "(FLOOR(SECONDS_BETWEEN({0},{1})/60)+1)", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffSecond:
					return string.Format(CultureInfo.InvariantCulture, "(SECONDS_BETWEEN({0},{1})+1)", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffMilliSecond:
					return string.Format(CultureInfo.InvariantCulture, "(FLOOR(NANO100_BETWEEN({0},{1})/10000))", operands[0], operands[1]);
				case FunctionOperatorType.DateDiffTick:
					return string.Format(CultureInfo.InvariantCulture, "NANO100_BETWEEN({0},{1})", operands[0], operands[1]);
				case FunctionOperatorType.GetTimeOfDay:
					return string.Format(CultureInfo.InvariantCulture, "(SELECT (HOUR(T.V)*3600+MINUTE(T.V)*60+TO_DECIMAL(SECOND(T.V),37,8))*10000000 X FROM (SELECT ({0}) V FROM DUMMY) T)", operands[0]);
				case FunctionOperatorType.GetDate:
					return string.Format(CultureInfo.InvariantCulture, "TO_TIMESTAMP(TO_DATE({0}))", operands[0]);
				case FunctionOperatorType.GetDayOfWeek:
					return string.Format(CultureInfo.InvariantCulture, "(WEEKDAY({0})+1)", operands[0]);
				case FunctionOperatorType.GetDayOfYear:
					return string.Format(CultureInfo.InvariantCulture, "DAYOFYEAR({0})", operands[0]);
				case FunctionOperatorType.GetYear:
					return string.Format(CultureInfo.InvariantCulture, "EXTRACT(YEAR FROM {0})", operands[0]);
				case FunctionOperatorType.GetMonth:
					return string.Format(CultureInfo.InvariantCulture, "EXTRACT(MONTH FROM {0})", operands[0]);
				case FunctionOperatorType.GetDay:
					return string.Format(CultureInfo.InvariantCulture, "EXTRACT(DAY FROM {0})", operands[0]);
				case FunctionOperatorType.GetHour:
					return string.Format(CultureInfo.InvariantCulture, "EXTRACT(HOUR FROM {0})", operands[0]);
				case FunctionOperatorType.GetMinute:
					return string.Format(CultureInfo.InvariantCulture, "EXTRACT(MINUTE FROM {0})", operands[0]);
				case FunctionOperatorType.GetSecond:
					return string.Format(CultureInfo.InvariantCulture, "FLOOR(SECOND({0}))", operands[0]);
				case FunctionOperatorType.GetMilliSecond:
					return string.Format(CultureInfo.InvariantCulture, "(SELECT (T.V-FLOOR(T.V))*1000 X FROM (SELECT SECOND({0}) V FROM DUMMY) T)", operands[0]);
				case FunctionOperatorType.Today:
					return string.Format(CultureInfo.InvariantCulture, "TO_TIMESTAMP(TO_DATE(CURRENT_TIMESTAMP))");
				case FunctionOperatorType.Now:
					return string.Format(CultureInfo.InvariantCulture, "CURRENT_TIMESTAMP");
				case FunctionOperatorType.UtcNow:
					return string.Format(CultureInfo.InvariantCulture, "CURRENT_UTCTIMESTAMP");
				case FunctionOperatorType.CharIndex:
					return FnCharIndex(operands);
				case FunctionOperatorType.Contains:
					return string.Format(CultureInfo.InvariantCulture, "(LOCATE({0},{1})<>0)", operands[0], operands[1]);
				case FunctionOperatorType.EndsWith:
					return string.Format(CultureInfo.InvariantCulture, "(0=(SELECT (LOCATE(T.V0,T.V1,-1)-(LENGTH(T.V0)-LENGTH(T.V1)+1)) X FROM (SELECT ({0}) V0, ({1}) V1 FROM DUMMY) T))", operands[0], operands[1]);
				case FunctionOperatorType.StartsWith:
					return string.Format(CultureInfo.InvariantCulture, "(LOCATE({0},{1})=1)", operands[0], operands[1]);
				case FunctionOperatorType.Substring:
					return FnSubstring(operands);
				case FunctionOperatorType.Insert:
					return string.Format(CultureInfo.InvariantCulture, "(SELECT CONCAT(CONCAT(SUBSTRING(T.V0,1,T.V1),T.V2),SUBSTRING(T.V0,T.V1+1)) X FROM (SELECT ({0}) V0, ({1}) V1, ({2}) V2 FROM DUMMY) T)", operands[0], operands[1], operands[2]);
				case FunctionOperatorType.Remove:
					return FnRemove(operands);
				case FunctionOperatorType.PadLeft:
					return FnPadLeft(operands);
				case FunctionOperatorType.PadRight:
					return FnPadRight(operands);
				case FunctionOperatorType.Len:
					return string.Format(CultureInfo.InvariantCulture, "LENGTH({0})", operands[0]);
				case FunctionOperatorType.IsNullOrEmpty:
					return string.Format(CultureInfo.InvariantCulture, "(LENGTH(IFNULL({0},''))=0)", operands[0]);
				case FunctionOperatorType.ToStr:
					return string.Format(CultureInfo.InvariantCulture, "TO_NVARCHAR({0})", operands[0]);
				case FunctionOperatorType.ToDecimal:
					return string.Format(CultureInfo.InvariantCulture, "TO_DECIMAL({0},37,8)", operands[0]);
				case FunctionOperatorType.ToDouble:
					return string.Format(CultureInfo.InvariantCulture, "TO_DOUBLE({0})", operands[0]);
				case FunctionOperatorType.ToFloat:
					return string.Format(CultureInfo.InvariantCulture, "TO_REAL({0})", operands[0]);
				case FunctionOperatorType.ToInt:
					return string.Format(CultureInfo.InvariantCulture, "TO_INT({0})", operands[0]);
				case FunctionOperatorType.ToLong:
					return string.Format(CultureInfo.InvariantCulture, "TO_BIGINT({0})", operands[0]);
				case FunctionOperatorType.IsNull:
					return FnIsNull(operands);
			}
			return base.FormatFunction(operatorType, operands);
		}
		string FnIsNull(string[] operands) {
			if(operands.Length == 1) {
				return string.Format(CultureInfo.InvariantCulture, "({0} IS NULL)", operands[0]);
			}
			if(operands.Length == 2) {
				return string.Format(CultureInfo.InvariantCulture, "IFNULL({1},{0})", operands[0], operands[1]);
			}
			throw new NotSupportedException();
		}
		string FnLog(string[] operands) {
			if(operands.Length == 1) {
				return string.Format(CultureInfo.InvariantCulture, "LN({0})", operands[0]);
			}
			if(operands.Length == 2) {
				return string.Format(CultureInfo.InvariantCulture, "LOG({1},{0})", operands[0], operands[1]);
			}
			throw new NotSupportedException();
		}
		string FnRound(string[] operands) {
			if(operands.Length == 1) {
				return string.Format(CultureInfo.InvariantCulture, "ROUND({0})", operands[0]);
			}
			if(operands.Length == 2) {
				return string.Format(CultureInfo.InvariantCulture, "ROUND({0},{1})", operands[0], operands[1]);
			}
			throw new NotSupportedException();
		}
		string FnPadLeft(string[] operands) {
			if(operands.Length == 2) {
				return string.Format(CultureInfo.InvariantCulture, "(SELECT LPAD(T.V,GREATEST({1},LENGTH(T.V))) X FROM (SELECT ({0}) V FROM DUMMY) T)", operands[0], operands[1]);
			}
			if(operands.Length == 3) {
				return string.Format(CultureInfo.InvariantCulture, "(SELECT LPAD(T.V,GREATEST({1},LENGTH(T.V)),{2}) X FROM (SELECT ({0}) V FROM DUMMY) T)", operands[0], operands[1], operands[2]);
			}
			throw new NotSupportedException();
		}
		string FnPadRight(string[] operands) {
			if(operands.Length == 2) {
				return string.Format(CultureInfo.InvariantCulture, "(SELECT RPAD(T.V,GREATEST({1},LENGTH(T.V))) X FROM (SELECT ({0}) V FROM DUMMY) T)", operands[0], operands[1]);
			}
			if(operands.Length == 3) {
				return string.Format(CultureInfo.InvariantCulture, "(SELECT RPAD(T.V,GREATEST({1},LENGTH(T.V)),{2}) X FROM (SELECT ({0}) V FROM DUMMY) T)", operands[0], operands[1], operands[2]);
			}
			throw new NotSupportedException();
		}
		string FnSubstring(string[] operands) {
			if(operands.Length == 2) {
				return string.Format(CultureInfo.InvariantCulture, "SUBSTRING({0},({1})+1)", operands[0], operands[1]);
			}
			if(operands.Length == 3) {
				return string.Format(CultureInfo.InvariantCulture, "SUBSTRING({0},({1})+1,{2})", operands[0], operands[1], operands[2]);
			}
			throw new NotSupportedException();
		}
		string FnRemove(string[] operands) {
			if(operands.Length == 2) {
				return string.Format(CultureInfo.InvariantCulture, "LEFT({0},{1})", operands[0], operands[1]);
			}
			if(operands.Length == 3) {
				return string.Format(CultureInfo.InvariantCulture, "(SELECT CONCAT(LEFT(T.V0,T.V1),SUBSTRING(T.V0,T.V1+T.V2+1)) X FROM (SELECT ({0}) V0, ({1}) V1, ({2}) V2 FROM DUMMY) T)", operands[0], operands[1], operands[2]);
			}
			throw new NotSupportedException();
		}
		string FnCharIndex(string[] operands) {
			switch(operands.Length) {
				case 2:
					return string.Format(CultureInfo.InvariantCulture, "(LOCATE({1},{0}) - 1)", operands[0], operands[1]);
				case 3:
					return string.Format(CultureInfo.InvariantCulture, "(LOCATE({1},{0},({2}) + 1) - 1)", operands[0], operands[1], operands[2]);
				case 4:
					return string.Format(CultureInfo.InvariantCulture, "(LOCATE(SUBSTRING({0},1,{3}),{1},({2}) + 1) - 1)", operands[0], operands[1], operands[2], operands[3]);
				default:
					throw new NotSupportedException();
			}
		}
		#endregion
		#region temporary stubs
		public override bool NativeSkipTakeSupported {
			get { return true; }
		}
		#endregion
		public override void CreateColumn(DBTable table, DBColumn column) {
			ExecuteSqlSchemaUpdate("Column", column.Name, table.Name, String.Format(CultureInfo.InvariantCulture,
	"alter table {0} add ({1} {2})",
	FormatTableSafe(table), FormatColumnSafe(column.Name), GetSqlCreateColumnFullAttributes(table, column, false)));
		}
		public override string GetParameterName(OperandValue parameter, int index, ref bool createParameter) {
			createParameter = true;
			return string.Format(CultureInfo.InvariantCulture, ":p{0}", index);
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
			if(column.IsIdentity) {
				result += " GENERATED BY DEFAULT AS IDENTITY";
			}
			else {
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
			if(table.PrimaryKey != null) {
				if(table.PrimaryKey.Columns.Count == 1 && table.PrimaryKey.Columns[0] == column.Name) {
					result += " PRIMARY KEY";
				}
			}
			return result;
		}
		public override string[] GetStorageTablesList(bool includeViews) {
			return GetStorageTablesList(includeViews, true).Keys.ToArray();
		}
		protected Dictionary<string, bool> GetStorageTablesList(bool includeViews, bool stripKnownSchema) {
			Dictionary<string, bool> result = new Dictionary<string, bool>();
			foreach(var name in GetStorageTablesList("SELECT TABLE_NAME, SCHEMA_NAME FROM SYS.TABLES where IS_PUBLIC = 'TRUE' and not (SCHEMA_NAME = 'SYS' or SCHEMA_NAME like '_SYS_%')", stripKnownSchema)) {
				result.Add(name, false);
			}
			if(includeViews) {
				foreach(var name in GetStorageTablesList("SELECT VIEW_NAME, SCHEMA_NAME FROM SYS.VIEWS where not (SCHEMA_NAME = 'SYS' or SCHEMA_NAME = 'SYS_DATABASES' or SCHEMA_NAME like '_SYS_%')", stripKnownSchema)) {
					result.Add(name, true);
				}
			}
			return result;
		}
		IEnumerable<string> GetStorageTablesList(string sql, bool stripKnownSchema) {
			Query query = new Query(sql);
			SelectStatementResult tables = SelectData(query);
			foreach(SelectStatementResultRow row in tables.Rows) {
				string objectName = (string)row.Values[0];
				string schemaName = (string)row.Values[1];
				if(stripKnownSchema && (string.IsNullOrEmpty(ObjectsOwner) || (ObjectsOwner.ToUpperInvariant() == schemaName))) {
					yield return objectName;
				}
				else {
					yield return string.Concat(schemaName, ".", objectName);
				}
			}
		}
		protected override object ReformatReadValue(object value, ReformatReadValueArgs args) {
			if(args.TargetTypeCode == TypeCode.Boolean && args.DbTypeCode == TypeCode.String)
				return ((string)value) != "0";
			return base.ReformatReadValue(value, args);
		}
		protected override object ConvertToDbParameter(object clientValue, TypeCode clientValueTypeCode) {
			switch(clientValueTypeCode) {
				case TypeCode.Object:
					if(clientValue is Guid) {
						return clientValue.ToString();
					}
					if(clientValue is TimeSpan) {
						return ((TimeSpan)clientValue).TotalSeconds;
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
		protected ParameterValue CreateParameterForSystemQuery(int tag, string value) {
			return new ParameterValue(tag) {
				Value = value
			};
		}
		Nullable<T> GetValueOrNull<T>(object value) where T : struct {
			return Equals(DBNull.Value, value) ? null : new Nullable<T>((T)value);
		}
		protected DBColumnType GetTypeFromString(string typeName, int? size, int? scale, out string fullTypeName) {
			fullTypeName = typeName;
			switch(typeName.ToLowerInvariant()) {
				case "boolean":
					return DBColumnType.Boolean;
				case "tinyint":
					return DBColumnType.Byte;
				case "smallint":
					return DBColumnType.Int16;
				case "integer":
					return DBColumnType.Int32;
				case "bigint":
					return DBColumnType.Int64;
				case "real":
					return DBColumnType.Single;
				case "double":
					return DBColumnType.Double;
				case "varchar":
				case "nvarchar":
				case "alphanum":
				case "shorttext":
					if(size.HasValue) {
						fullTypeName = string.Format(CultureInfo.InvariantCulture, "{0}({1})", typeName, size.Value);
					}
					return DBColumnType.String;
				case "clob":
				case "nclob":
				case "text":
				case "bintext":
					return DBColumnType.String;
				case "timestamp":
				case "date":
				case "seconddate":
					return DBColumnType.DateTime;
				case "varbinary":
				case "blob":
					return DBColumnType.ByteArray;
				case "decimal":
				case "smalldecimal":
					if(size.HasValue) {
						fullTypeName = string.Format(CultureInfo.InvariantCulture, "{0}({1},{2})", typeName, size.Value, scale.HasValue ? scale.Value : 0);
						return DBColumnType.Decimal;
					}
					return DBColumnType.Double;
			}
			return DBColumnType.Unknown;
		}
		protected struct ObjectName {
			public string SchemaName;
			public string TableName;
			public ObjectName(string schema, string table) {
				this.SchemaName = schema;
				this.TableName = table;
			}
		}
		protected virtual ObjectName ComposeSafeObjectName(string name) {
			string schemaName = ComposeSafeSchemaName(name);
			if(schemaName == string.Empty)
				schemaName = ObjectsOwner;
			if(!string.IsNullOrEmpty(schemaName))
				schemaName = schemaName.ToUpperInvariant();
			string tableName = ComposeSafeTableName(name);
			return new ObjectName(schemaName, tableName);
		}
		void GetColumns(DBTable table) {
			var objectName = ComposeSafeObjectName(table.Name);
			string sql;
			if(!table.IsView) {
				sql = @"select COLUMN_NAME, DATA_TYPE_NAME, LENGTH, SCALE, IS_NULLABLE, DEFAULT_VALUE 
from SYS.TABLE_COLUMNS 
where SCHEMA_NAME = :p0 and TABLE_NAME = :p1 order by POSITION";
			}
			else {
				sql = @"select COLUMN_NAME, DATA_TYPE_NAME, LENGTH, SCALE, IS_NULLABLE, DEFAULT_VALUE 
from SYS.VIEW_COLUMNS 
where SCHEMA_NAME = :p0 and VIEW_NAME = :p1 order by POSITION";
			}
			Query query = new Query(sql,
				new QueryParameterCollection(CreateParameterForSystemQuery(0, objectName.SchemaName), CreateParameterForSystemQuery(1, objectName.TableName)), new string[] { "p0", "p1" });
			foreach(SelectStatementResultRow row in SelectData(query).Rows) {
				string columnName = (string)row.Values[0];
				string dataTypeName = (string)row.Values[1];
				int? length = GetValueOrNull<int>(row.Values[2]);
				int? scale = GetValueOrNull<int>(row.Values[3]);
				bool isNullable = Convert.ToBoolean(row.Values[4]);
				string dbDefaultValue = (string)(Equals(DBNull.Value, row.Values[5]) ? null : row.Values[5]);
				string fullTypeName;
				DBColumnType type = GetTypeFromString(dataTypeName, length, scale, out fullTypeName);
				object defaultValue = null;
				if(dbDefaultValue != null) {
					string dataTypeStyring = dataTypeName;
					if(type == DBColumnType.String) {
						dataTypeStyring = fullTypeName;
					}
					try {
						string scalarQuery = string.Format("SELECT CAST({0} AS {1}) X FROM DUMMY", FormatString(dbDefaultValue), dataTypeStyring);
						defaultValue = FixDBNullScalar(GetScalar(new Query(scalarQuery)));
					}
					catch { }
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
				DBColumn dBColumn = new DBColumn(columnName, false, fullTypeName, type == DBColumnType.String ? (length.HasValue ? length.Value : 1) : 0, type, isNullable, defaultValue);
				if(dbDefaultValue != null && (type == DBColumnType.String || type == DBColumnType.DateTime)) {
					dBColumn.DbDefaultValue = FormatString(dbDefaultValue);
				}
				else {
					dBColumn.DbDefaultValue = dbDefaultValue;
				}
				table.AddColumn(dBColumn);
			}
		}
		void GetPrimaryKey(DBTable table) {
			var objectName = ComposeSafeObjectName(table.Name);
			Query query = new Query(@"select COLUMN_NAME, CONSTRAINT_NAME from SYS.CONSTRAINTS
where SCHEMA_NAME = :p0 and TABLE_NAME = :p1 and IS_PRIMARY_KEY = 'TRUE'
order by POSITION",
				new QueryParameterCollection(CreateParameterForSystemQuery(0, objectName.SchemaName), CreateParameterForSystemQuery(1, objectName.TableName)), new string[] { "p0", "p1" });
			SelectStatementResult data = SelectData(query);
			if(data.Rows.Length > 0) {
				StringCollection cols = new StringCollection();
				string constraintName = "PK";
				foreach(SelectStatementResultRow row in data.Rows) {
					string columnName = (string)row.Values[0];
					constraintName = (string)row.Values[1];
					DBColumn column = table.GetColumn(columnName);
					if(column != null) {
						column.IsKey = true;
					}
					cols.Add(columnName);
				}
				table.PrimaryKey = new DBPrimaryKey(constraintName, cols);
			}
		}
		void GetIndexes(DBTable table) {
			var objectName = ComposeSafeObjectName(table.Name);
			Query query = new Query(@"select COLUMN_NAME, INDEX_NAME, POSITION, CONSTRAINT from SYS.INDEX_COLUMNS
where SCHEMA_NAME = :p0 and TABLE_NAME = :p1
order by INDEX_NAME, POSITION",
				new QueryParameterCollection(CreateParameterForSystemQuery(0, objectName.SchemaName), CreateParameterForSystemQuery(1, objectName.TableName)), new string[] { "p0", "p1" });
			SelectStatementResult data = SelectData(query);
			DBIndex index = null;
			foreach(SelectStatementResultRow row in data.Rows) {
				int position = Convert.ToInt32(row.Values[2]);
				if(position == 1) {
					StringCollection list = new StringCollection();
					list.Add((string)row.Values[0]);
					string constraint = (string)FixDBNullScalar(row.Values[3]);
					bool isUnique = constraint == "UNIQUE" || constraint == "NOT_NULL_UNIQUE" || constraint == "PRIMARY_KEY";
					index = new DBIndex((string)row.Values[1], list, isUnique);
					table.Indexes.Add(index);
				}
				else
					index.Columns.Add((string)row.Values[0]);
			}
		}
		void GetForeignKeys(DBTable table) {
			var objectName = ComposeSafeObjectName(table.Name);
			Query query = new Query(@"select COLUMN_NAME, CONSTRAINT_NAME, POSITION, REFERENCED_SCHEMA_NAME, REFERENCED_TABLE_NAME, REFERENCED_COLUMN_NAME, REFERENCED_CONSTRAINT_NAME from SYS.REFERENTIAL_CONSTRAINTS
where SCHEMA_NAME = :p0 and TABLE_NAME = :p1
order by CONSTRAINT_NAME, POSITION",
				new QueryParameterCollection(CreateParameterForSystemQuery(0, objectName.SchemaName), CreateParameterForSystemQuery(1, objectName.TableName)), new string[] { "p0", "p1" });
			SelectStatementResult data = SelectData(query);
			DBForeignKey fk = null;
			foreach(SelectStatementResultRow row in data.Rows) {
				int position = Convert.ToInt32(row.Values[2]);
				if(position == 1) {
					StringCollection pkc = new StringCollection();
					StringCollection fkc = new StringCollection();
					pkc.Add((string)row.Values[5]);
					fkc.Add((string)row.Values[0]);
					string fkTableName = (string)row.Values[4];
					string fkSchemeName = (string)row.Values[3];
					string fkFullName = (string.IsNullOrEmpty(ObjectsOwner) || fkSchemeName == ObjectsOwner.ToUpperInvariant()) ? fkTableName : string.Concat(fkSchemeName, ".", fkTableName);
					fk = new DBForeignKey(fkc, fkFullName, pkc);
					fk.Name = (string)row.Values[6];
					table.ForeignKeys.Add(fk);
				}
				else {
					fk.Columns.Add((string)row.Values[0]);
					fk.PrimaryKeyTableKeyColumns.Add((string)row.Values[5]);
				}
			}
		}
		public override void GetTableSchema(DBTable table, bool checkIndexes, bool checkForeignKeys) {
			var objectName = ComposeSafeObjectName(table.Name);
			Query query = new Query(@"select OBJECT_TYPE from SYS.OBJECTS where SCHEMA_NAME = :p0 and OBJECT_NAME = :p1",
				new QueryParameterCollection(CreateParameterForSystemQuery(0, objectName.SchemaName), CreateParameterForSystemQuery(1, objectName.TableName)), new string[] { "p0", "p1" });
			SelectStatementResult data = SelectData(query);
			foreach(SelectStatementResultRow row in data.Rows) {
				string otype = row.Values[0] as string;
				table.IsView = (otype == "VIEW");
			}
			GetColumns(table);
			GetPrimaryKey(table);
			if(checkIndexes)
				GetIndexes(table);
			if(checkForeignKeys)
				GetForeignKeys(table);
		}
		protected override void CommandBuilderDeriveParameters(IDbCommand command) {
			throw new NotImplementedException();
		}
		protected override int GetSafeNameTableMaxLength() {
			return 127;
		}
		protected override int GetObjectNameEffectiveLength(string objectName) {
			return Encoding.UTF8.GetByteCount(objectName);
		}
		public override bool SupportNamedParameters {
			get { return false; }
		}
		protected override Exception WrapException(Exception e, IDbCommand query) {
			object o;
			if(ConnectionHelper.TryGetExceptionProperty(e, "Errors", out o) && ((ICollection)o).Count > 0) {
				object error = ReflectConnectionHelper.GetCollectionFirstItem((ICollection)o);
				int code = (int)ReflectConnectionHelper.GetPropertyValue(error, "NativeError");
				if(code == 260 || code == 259)
					return new SchemaCorrectionNeededException(e);
				if(code == 462 || code == 301)
					return new ConstraintViolationException(query.CommandText, GetParametersString(query), e);
			}
			return base.WrapException(e, query);
		}
		#region GetSqlCreateColumnTypeFor
		protected override string GetSqlCreateColumnTypeForBoolean(DBTable table, DBColumn column) {
			return "BOOLEAN";
		}
		protected override string GetSqlCreateColumnTypeForByte(DBTable table, DBColumn column) {
			return "TINYINT";
		}
		protected override string GetSqlCreateColumnTypeForByteArray(DBTable table, DBColumn column) {
			return "BLOB";
		}
		protected override string GetSqlCreateColumnTypeForChar(DBTable table, DBColumn column) {
			return "NVARCHAR(1)";
		}
		protected override string GetSqlCreateColumnTypeForDateTime(DBTable table, DBColumn column) {
			return "TIMESTAMP";
		}
		protected override string GetSqlCreateColumnTypeForDecimal(DBTable table, DBColumn column) {
			return "DECIMAL(37,8)";
		}
		protected override string GetSqlCreateColumnTypeForDouble(DBTable table, DBColumn column) {
			return "DOUBLE";
		}
		protected override string GetSqlCreateColumnTypeForGuid(DBTable table, DBColumn column) {
			return "VARCHAR(36)";
		}
		protected override string GetSqlCreateColumnTypeForInt16(DBTable table, DBColumn column) {
			return "SMALLINT";
		}
		protected override string GetSqlCreateColumnTypeForInt32(DBTable table, DBColumn column) {
			return "INTEGER";
		}
		protected override string GetSqlCreateColumnTypeForInt64(DBTable table, DBColumn column) {
			return "BIGINT";
		}
		protected override string GetSqlCreateColumnTypeForSByte(DBTable table, DBColumn column) {
			return "DECIMAL(3,0)";
		}
		protected override string GetSqlCreateColumnTypeForSingle(DBTable table, DBColumn column) {
			return "REAL";
		}
		protected override string GetSqlCreateColumnTypeForString(DBTable table, DBColumn column) {
			if(column.Size > 0 && column.Size <= 5000)
				return "NVARCHAR(" + column.Size.ToString(CultureInfo.InvariantCulture) + ')';
			else
				return "NCLOB";
		}
		protected override string GetSqlCreateColumnTypeForUInt16(DBTable table, DBColumn column) {
			return "DECIMAL(5,0)";
		}
		protected override string GetSqlCreateColumnTypeForUInt32(DBTable table, DBColumn column) {
			return "DECIMAL(10,0)";
		}
		protected override string GetSqlCreateColumnTypeForUInt64(DBTable table, DBColumn column) {
			return "DECIMAL(20,0)";
		}
		#endregion
		#region Registration
		public const string XpoProviderTypeString = "Hana";
		public static string GetConnectionString(string server, int port, string userId, string password, string database) {
			string s = string.Format("Server={0}:{1};UserID={2};Password={3}", EscapeConnectionStringArgument(server), port.ToString(CultureInfo.InvariantCulture), EscapeConnectionStringArgument(userId), EscapeConnectionStringArgument(password));
			if(!string.IsNullOrWhiteSpace(database)) {
				s = string.Concat(s, string.Format(";Database={0}", EscapeConnectionStringArgument(database)));
			}
			return string.Concat(string.Format("{0}={1};", DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString), s);
		}
		public static string GetConnectionString(string server, int port, string userId, string password) {
			return GetConnectionString(server, port, userId, password, null);
		}
#if !NET
		const string HanaAssemblyName = "Sap.Data.Hana.v4.5";
#else
		const string HanaAssemblyName = "Sap.Data.Hana.Core.v2.1";
#endif
		public static IDbConnection CreateConnection(string connectionString) {
			return ReflectConnectionHelper.GetConnection(HanaAssemblyName, "Sap.Data.Hana.HanaConnection", connectionString);
		}
		public static IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			IDbConnection connection = CreateConnection(connectionString);
			objectsToDisposeOnDisconnect = new IDisposable[] { connection };
			return CreateProviderFromConnection(connection, autoCreateOption);
		}
		public static IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return new HanaConnectionProvider(connection, autoCreateOption);
		}
		static HanaConnectionProvider() {
			RegisterDataStoreProvider(XpoProviderTypeString, new DataStoreCreationFromStringDelegate(CreateProviderFromString));
			RegisterDataStoreProvider("Sap.Data.Hana.HanaConnection", new DataStoreCreationFromConnectionDelegate(CreateProviderFromConnection));
			RegisterFactory(new HanaProviderFactory());
		}
		public static void Register() { }
		#endregion
	}
	public class HanaProviderFactory : ProviderFactory {
		public override IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return HanaConnectionProvider.CreateProviderFromConnection(connection, autoCreateOption);
		}
		public override IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			return HanaConnectionProvider.CreateProviderFromString(connectionString, autoCreateOption, out objectsToDisposeOnDisconnect);
		}
		public override string GetConnectionString(Dictionary<string, string> parameters) {
			if(!parameters.ContainsKey(ServerParamID) || !parameters.ContainsKey(DatabaseParamID) ||
				!parameters.ContainsKey(UserIDParamID) || !parameters.ContainsKey(PasswordParamID)) {
				return null;
			}
			string portString;
			int port = 39017;
			if(parameters.TryGetValue(PortParamID, out portString)) {
				port = Convert.ToInt32(portString, CultureInfo.InvariantCulture);
			}
			return HanaConnectionProvider.GetConnectionString(parameters[ServerParamID], port, parameters[UserIDParamID], parameters[PasswordParamID], parameters[DatabaseParamID]);
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
		public override bool IsServerbased { get { return true; } }
		public override bool IsFilebased { get { return false; } }
		public override bool HasMultipleDatabases { get { return true; } }
		public override string ProviderKey { get { return HanaConnectionProvider.XpoProviderTypeString; } }
		public override string FileFilter { get { return null; } }
		public override bool MeanSchemaGeneration { get { return true; } }
		public override bool HasPort { get { return true; } }
		public override string[] GetDatabases(string server, string userId, string password) {
			return this.GetDatabases(server, 39017, userId, password);
		}
		public override string[] GetDatabases(string server, int port, string userId, string password) {
			return Array.Empty<string>(); 
		}
	}
#pragma warning restore DX0024
}
