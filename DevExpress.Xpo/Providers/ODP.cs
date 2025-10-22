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
using System.Data;
using System.Globalization;
using System.Reflection;
using System.Threading;
#pragma warning disable DX0024
namespace DevExpress.Xpo.DB {
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using DevExpress.Data.Filtering;
	using DevExpress.Data.Helpers;
	using DevExpress.Xpo.DB.Exceptions;
	using DevExpress.Xpo.DB.Helpers;
	public abstract class BaseODPConnectionProvider : BaseOracleConnectionProvider {
		protected abstract string OracleAssemblyName { get; }
		protected abstract string OracleDbTypeName { get; }
		protected abstract string OracleDecimanlTypeName { get; }
		protected abstract string OracleCommandTypeName { get; }
		protected abstract string OracleExceptionTypeName { get; }
		protected abstract string OracleParameterTypeName { get; }
		protected abstract string OracleDataReaderTypeName { get; }
		protected abstract string OracleCommandBuilderTypeName { get; }
		protected abstract string OracleGlobalizationTypeName { get; }
		ReflectConnectionHelper helper;
		ReflectConnectionHelper ConnectionHelper {
			get {
				if(helper == null)
					helper = new ReflectConnectionHelper(Connection, OracleExceptionTypeName);
				return helper;
			}
		}
		SetPropertyValueDelegate setOracleCommandBindByName;
		SetPropertyValueDelegate setOracleInitialLONGFetchSize;
		protected abstract bool IsODPv10 { get; }
		public BaseODPConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption)
			: base(connection, autoCreateOption) {
		}
		protected BaseODPConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption, bool openConnection)
			: base(connection, autoCreateOption, openConnection) {
		}
		protected override void PrepareDelegates() {
			Type oracleParameterType = ConnectionHelper.GetType(OracleParameterTypeName);
			Type oracleCommandType = ConnectionHelper.GetType(OracleCommandTypeName);
			setOracleCommandBindByName = ReflectConnectionHelper.CreateSetPropertyDelegate(oracleCommandType, "BindByName");
			setOracleInitialLONGFetchSize = ReflectConnectionHelper.CreateSetPropertyDelegate(oracleCommandType, "InitialLONGFetchSize");
		}
		public static bool VarcharParameterFixEnabled = true;
		protected override IDataParameter CreateParameter(IDbCommand command, object value, string name, DBColumnType dbType, string dbTypeName, int size) {
			IDbDataParameter param = (IDbDataParameter)CreateParameter(command);
			bool isParameterTypeChanged = false;
			if(value is Enum) param.Value = 0;
			if(value is Guid) {
				string guid = value.ToString();
				param.Value = guid;
				((IDbTypeMapperBaseOracle)DbTypeMapper).SetOracleDbTypeChar(param, guid.Length);
				isParameterTypeChanged = true;
			}
			else {
				param.Value = value;
			}
			param.ParameterName = name;
			QueryParameterMode parameterMode = GetQueryParameterMode();
			if(!isParameterTypeChanged && parameterMode != QueryParameterMode.Legacy) {
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
			if(VarcharParameterFixEnabled) {
				((IDbTypeMapperBaseOracle)DbTypeMapper).FixVarcharParameterType(param);
			}
			if(parameterMode == QueryParameterMode.SetTypeAndSize) {
				ValidateParameterSize(command, param);
			}
			return param;
		}
		int GetValueSize(object value) {
			string stringValue = value as string;
			if(stringValue != null) {
				return stringValue.Length;
			}
			else {
				var arrayValue = value as Array;
				if(arrayValue != null) {
					return arrayValue.Length;
				}
			}
			return -1;
		}
		protected override object ConvertToDbParameter(object clientValue, TypeCode clientValueTypeCode) {
			switch(clientValueTypeCode) {
				case TypeCode.Boolean:
					return (bool)clientValue ? 1 : 0;
				case TypeCode.Byte:
					return (short)(byte)clientValue;
				case TypeCode.UInt16:
					return (int)(UInt16)clientValue;
				case TypeCode.SByte:
					return (int)(sbyte)clientValue;
				case TypeCode.UInt32:
					return (long)(UInt32)clientValue;
				case TypeCode.UInt64:
					return (decimal)(UInt64)clientValue;
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
		protected override bool IsConnectionBroken(Exception e) {
			object numberObject;
			if(ConnectionHelper.TryGetExceptionProperty(e, "Number", out numberObject)) {
				int number = (int)numberObject;
				if(number == 0x311b || number == 3114 || number == 12152 || number == 3135) {
					Connection.Close();
					return true;
				}
			}
			return base.IsConnectionBroken(e);
		}
		protected override Exception WrapException(Exception e, IDbCommand query) {
			object numberObject;
			if(ConnectionHelper.TryGetExceptionProperty(e, "Number", out numberObject)) {
				int number = (int)numberObject;
				if(number == 0x388 || number == 0x3ae || number == 0x1996)
					return new SchemaCorrectionNeededException(e);
				if(number == 0x8f4 || number == 1)
					return new ConstraintViolationException(query.CommandText, GetParametersString(query), e);
			}
			return base.WrapException(e, query);
		}
		protected override IDbConnection CreateConnection() {
			return ConnectionHelper.GetConnection(ConnectionString);
		}
		public override IDbCommand CreateCommand() {
			IDbCommand cmd = base.CreateCommand();
			setOracleCommandBindByName(cmd, true);
			setOracleInitialLONGFetchSize(cmd, MaximumStringSize);
			return cmd;
		}
		protected override bool IsFieldTypesNeeded { get { return true; } }
		ReflectionGetValuesHelperBase getValuesHelper;
		private ReflectionGetValuesHelperBase GetValuesHelper {
			get {
				if(getValuesHelper == null) {
					Type oracleDataReaderType = ConnectionHelper.GetType(OracleDataReaderTypeName);
					Type oracleDecimalType = ConnectionHelper.GetType(OracleDecimanlTypeName);
					Type oracleGlobalizationType = Data.Internal.SafeTypeResolver.GetKnownType(oracleDecimalType.Assembly, OracleGlobalizationTypeName, false);
					if(oracleGlobalizationType == null) {
						oracleGlobalizationType = typeof(object);
					}
					getValuesHelper = (ReflectionGetValuesHelperBase)Activator.CreateInstance(typeof(ReflectionGetValuesHelper<,,>).MakeGenericType(oracleDataReaderType, oracleDecimalType, oracleGlobalizationType));
				}
				return getValuesHelper;
			}
		}
		DbTypeMapperBase dbTypeMapper;
		protected override DbTypeMapperBase DbTypeMapper {
			get {
				if(dbTypeMapper == null) {
					Type oracleParameterType = ConnectionHelper.GetType(OracleParameterTypeName);
					Type oracleDbTypeType = ConnectionHelper.GetType(OracleDbTypeName);
					dbTypeMapper = (DbTypeMapperBase)Activator.CreateInstance(typeof(DbTypeMapperODP<,>).MakeGenericType(oracleDbTypeType, oracleParameterType));
				}
				return dbTypeMapper;
			}
		}
		protected override void GetValues(IDataReader reader, Type[] fieldTypes, object[] values) {
			if(GetValuesHelper.GetValues(reader, fieldTypes, values))
				return;
			base.GetValues(reader, fieldTypes, values);
		}
		ExecMethodDelegate commandBuilderDeriveParametersHandler;
		protected override void CommandBuilderDeriveParameters(IDbCommand command) {
			if(IsODPv10) {
				if(commandBuilderDeriveParametersHandler == null) {
					commandBuilderDeriveParametersHandler = ReflectConnectionHelper.GetCommandBuilderDeriveParametersDelegate(OracleAssemblyName, OracleCommandBuilderTypeName);
				}
				commandBuilderDeriveParametersHandler(command);
			}
			else
				throw new NotSupportedException();
		}
		protected override SelectedData ExecuteSprocParametrized(string sprocName, params OperandValue[] parameters) {
			if(IsODPv10) {
				return base.ExecuteSprocParametrized(sprocName, parameters);
			}
			using(IDbCommand command = CreateCommand()) {
				PrepareCommandForSprocCallParametrized(command, sprocName, parameters);
				List<SelectStatementResult> selectStatementResults = GetSelectedStatementResults(command);
				return new SelectedData(selectStatementResults.ToArray());
			}
		}
		protected async override Task<SelectedData> ExecuteSprocParametrizedAsync(AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken, string sprocName, params OperandValue[] parameters) {
			if(IsODPv10) {
				return await base.ExecuteSprocParametrizedAsync(asyncOperationId, cancellationToken, sprocName, parameters).ConfigureAwait(false);
			}
			using(IDbCommand command = CreateCommand()) {
				PrepareCommandForSprocCallParametrized(command, sprocName, parameters);
				List<SelectStatementResult> selectStatementResults = await GetSelectedStatementResultsAsync(command, asyncOperationId, cancellationToken).ConfigureAwait(false);
				return new SelectedData(selectStatementResults.ToArray());
			}
		}
		protected override SelectedData ExecuteSproc(string sprocName, params OperandValue[] parameters) {
			if(IsODPv10) {
				return base.ExecuteSproc(sprocName, parameters);
			}
			using(IDbCommand command = CreateCommand()) {
				PrepareCommandForSprocCall(command, sprocName, parameters);
				List<SelectStatementResult> selectStatementResults = GetSelectedStatementResults(command);
				return new SelectedData(selectStatementResults.ToArray());
			}
		}
		protected override async Task<SelectedData> ExecuteSprocAsync(AsyncOperationIdentifier asyncOperationId, CancellationToken cancellationToken, string sprocName, params OperandValue[] parameters) {
			if(IsODPv10) {
				return await base.ExecuteSprocAsync(asyncOperationId, cancellationToken, sprocName, parameters).ConfigureAwait(false);
			}
			using(IDbCommand command = CreateCommand()) {
				PrepareCommandForSprocCall(command, sprocName, parameters);
				List<SelectStatementResult> selectStatementResults = await GetSelectedStatementResultsAsync(command, asyncOperationId, cancellationToken).ConfigureAwait(false);
				return new SelectedData(selectStatementResults.ToArray());
			}
		}
		void PrepareCommandForSprocCall(IDbCommand command, string sprocName, OperandValue[] parameters) {
			string[] listParams = new string[parameters.Length];
			for(int i = 0; i < parameters.Length; i++) {
				bool createParam = true;
				listParams[i] = GetParameterName(parameters[i], i, ref createParam);
				var parameter = parameters[i] as ParameterValue;
				if(!ReferenceEquals(parameter, null)) {
					command.Parameters.Add(CreateParameter(command, parameters[i].Value, listParams[i], parameter.DBType, parameter.DBTypeName, parameter.Size));
				}
				else {
					command.Parameters.Add(CreateParameter(command, parameters[i].Value, listParams[i], DBColumnType.Unknown, null, 0));
				}
			}
			command.CommandText = FormatSprocCall(sprocName, listParams);
		}
		void PrepareCommandForSprocCallParametrized(IDbCommand command, string sprocName, OperandValue[] parameters) {
			string[] listParams = new string[parameters.Length];
			for(int i = 0; i < parameters.Length; i++) {
				SprocParameter sprocParameter = (SprocParameter)parameters[i];
				if(sprocParameter.Direction != SprocParameterDirection.Input && sprocParameter.Direction != SprocParameterDirection.InputOutput)
					continue;
				listParams[i] = sprocParameter.ParameterName;
				IDataParameter parameter;
				if(sprocParameter.DbType.HasValue) {
					parameter = CreateParameter(command, sprocParameter.Value, listParams[i], sprocParameter.DbType.Value, null, sprocParameter.Size.HasValue ? sprocParameter.Size.Value : 0);
					if(sprocParameter.Precision.HasValue) {
						((IDbDataParameter)parameter).Precision = sprocParameter.Precision.Value;
					}
					if(sprocParameter.Scale.HasValue) {
						((IDbDataParameter)parameter).Scale = sprocParameter.Scale.Value;
					}
				}
				else {
					parameter = CreateParameter(command, parameters[i].Value, listParams[i], DBColumnType.Unknown, null, 0);
				}
				command.Parameters.Add(parameter);
			}
			command.CommandText = FormatSprocCall(sprocName, listParams);
		}
		string FormatSprocCall(string sprocName, string[] listParams) {
			string sprocNameSafe = FormatTable(ComposeSafeSchemaName(sprocName), ComposeSafeTableName(sprocName));
			return string.Format("call {0}({1})", sprocNameSafe, string.Join(", ", listParams));
		}
	}
	public class ODPConnectionProvider : BaseODPConnectionProvider {
		public const string XpoProviderTypeString = "ODP";
		public static string GetConnectionString(string server, string userId, string password) {
			return String.Format("{3}={4};Data Source={0};user id={1}; password={2};", EscapeConnectionStringArgument(server), EscapeConnectionStringArgument(userId), EscapeConnectionStringArgument(password), DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString);
		}
		public static IDbConnection CreateConnection(string connectionString) {
			return ReflectConnectionHelper.GetConnection("Oracle.DataAccess", "Oracle.DataAccess.Client.OracleConnection", connectionString);
		}
		public static IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			IDbConnection connection = CreateConnection(connectionString);
			objectsToDisposeOnDisconnect = new IDisposable[] { connection };
			return CreateProviderFromConnection(connection, autoCreateOption);
		}
		public static IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return new ODPConnectionProvider(connection, autoCreateOption);
		}
		static ODPConnectionProvider() {
			RegisterDataStoreProvider(XpoProviderTypeString, new DataStoreCreationFromStringDelegate(CreateProviderFromString));
			RegisterDataStoreProvider("Oracle.DataAccess.Client.OracleConnection", new DataStoreCreationFromConnectionDelegate(CreateProviderFromConnection));
		}
		public static void Register() { }
		protected override string OracleAssemblyName { get { return "Oracle.DataAccess"; } }
		protected override string OracleDbTypeName { get { return "Oracle.DataAccess.Client.OracleDbType"; } }
		protected override string OracleDecimanlTypeName { get { return "Oracle.DataAccess.Types.OracleDecimal"; } }
		protected override string OracleCommandTypeName { get { return "Oracle.DataAccess.Client.OracleCommand"; } }
		protected override string OracleExceptionTypeName { get { return "Oracle.DataAccess.Client.OracleException"; } }
		protected override string OracleParameterTypeName { get { return "Oracle.DataAccess.Client.OracleParameter"; } }
		protected override string OracleDataReaderTypeName { get { return "Oracle.DataAccess.Client.OracleDataReader"; } }
		protected override string OracleCommandBuilderTypeName { get { return "Oracle.DataAccess.Client.OracleCommandBuilder"; } }
		protected override string OracleGlobalizationTypeName { get { return "Oracle.DataAccess.Client.OracleGlobalization"; } }
		bool isODP10;
		protected override bool IsODPv10 { get { return isODP10; } }
		public ODPConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption)
			: this(connection, autoCreateOption, false) {
		}
		protected ODPConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption, bool openConnection)
			: base(connection, autoCreateOption) {
			Type odpConnectionType = connection.GetType();
			AssemblyName name = odpConnectionType.Assembly.GetName();
			isODP10 = name.Version.Major > 9 || ((name.Version.Major == 2 || name.Version.Major == 4) && name.Version.Minor >= 100);
		}
	}
	public class ODPManagedProviderFactory : ProviderFactory {
		public override IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return ODPManagedConnectionProvider.CreateProviderFromConnection(connection, autoCreateOption);
		}
		public override IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			return ODPManagedConnectionProvider.CreateProviderFromString(connectionString, autoCreateOption, out objectsToDisposeOnDisconnect);
		}
		public override string GetConnectionString(Dictionary<string, string> parameters) {
			if(!parameters.ContainsKey(ServerParamID) || !parameters.ContainsKey(UserIDParamID) || !parameters.ContainsKey(PasswordParamID)) { return null; }
			return ODPManagedConnectionProvider.GetConnectionString(parameters[ServerParamID], parameters[UserIDParamID], parameters[PasswordParamID]);
		}
		public override IDataStore CreateProvider(Dictionary<string, string> parameters, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			string connectionString = this.GetConnectionString(parameters);
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
		public override bool IsServerbased { get { return true; } }
		public override bool IsFilebased { get { return false; } }
		public override string ProviderKey { get { return ODPManagedConnectionProvider.XpoProviderTypeString; } }
		public override string[] GetDatabases(string server, string userId, string password) {
			return new string[1] { server };
		}
		public override string FileFilter { get { return null; } }
		public override bool MeanSchemaGeneration { get { return true; } }
		public override bool SupportStoredProcedures { get { return true; } }
	}
	public class ODPManagedConnectionProvider : BaseODPConnectionProvider {
		public const string XpoProviderTypeString = "ODPManaged";
		public static string GetConnectionString(string server, string userId, string password) {
			return String.Format("{3}={4};Data Source={0};user id={1}; password={2};", EscapeConnectionStringArgument(server), EscapeConnectionStringArgument(userId), EscapeConnectionStringArgument(password), DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString);
		}
		public static IDbConnection CreateConnection(string connectionString) {
			return ReflectConnectionHelper.GetConnection("Oracle.ManagedDataAccess", "Oracle.ManagedDataAccess.Client.OracleConnection", connectionString);
		}
		public static IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			IDbConnection connection = CreateConnection(connectionString);
			objectsToDisposeOnDisconnect = new IDisposable[] { connection };
			return CreateProviderFromConnection(connection, autoCreateOption);
		}
		public static IDataStore CreateProviderFromConnection(IDbConnection connection, AutoCreateOption autoCreateOption) {
			return new ODPManagedConnectionProvider(connection, autoCreateOption);
		}
		static ODPManagedConnectionProvider() {
			RegisterDataStoreProvider(XpoProviderTypeString, new DataStoreCreationFromStringDelegate(CreateProviderFromString));
			RegisterDataStoreProvider("Oracle.ManagedDataAccess.Client.OracleConnection", new DataStoreCreationFromConnectionDelegate(CreateProviderFromConnection));
			RegisterFactory(new ODPManagedProviderFactory());
		}
		public static void Register() { }
		protected override string OracleAssemblyName { get { return "Oracle.ManagedDataAccess"; } }
		protected override string OracleDbTypeName { get { return "Oracle.ManagedDataAccess.Client.OracleDbType"; } }
		protected override string OracleDecimanlTypeName { get { return "Oracle.ManagedDataAccess.Types.OracleDecimal"; } }
		protected override string OracleCommandTypeName { get { return "Oracle.ManagedDataAccess.Client.OracleCommand"; } }
		protected override string OracleExceptionTypeName { get { return "Oracle.ManagedDataAccess.Client.OracleException"; } }
		protected override string OracleParameterTypeName { get { return "Oracle.ManagedDataAccess.Client.OracleParameter"; } }
		protected override string OracleDataReaderTypeName { get { return "Oracle.ManagedDataAccess.Client.OracleDataReader"; } }
		protected override string OracleCommandBuilderTypeName { get { return "Oracle.ManagedDataAccess.Client.OracleCommandBuilder"; } }
		protected override string OracleGlobalizationTypeName { get { return "Oracle.ManagedDataAccess.Client.OracleGlobalization"; } }
		protected override bool IsODPv10 { get { return true; } }
		public ODPManagedConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption)
			: base(connection, autoCreateOption) {
		}
		protected ODPManagedConnectionProvider(IDbConnection connection, AutoCreateOption autoCreateOption, bool openConnection)
			: base(connection, autoCreateOption, openConnection) {
		}
	}
}
#pragma warning restore DX0024
namespace DevExpress.Xpo.DB.Helpers {
	class ReflectionGetValuesHelperBase {
		public virtual bool GetValues(IDataReader reader, Type[] fieldTypes, object[] values) {
			return false;
		}
	}
	class ReflectionGetValuesHelper<R, OD, OG> : ReflectionGetValuesHelperBase where R : IDataReader, IDataRecord {
		static readonly OD oracleDecimalMax;
		static readonly OD oracleDecimalMin;
		static readonly OD oracleDecimalZero;
		static readonly OracleDataReaderGetOracleDecimalDelegate getOracleDecimal;
		static readonly OracleDecimalComparisonDelegate oDGreaterThan;
		static readonly OracleDecimalComparisonDelegate oDEquals;
		static readonly OracleDecimalToDouble oDToDouble;
		static readonly OracleDecimalToDecimal oDToDecimal;
		static readonly OracleDecimalOperationWithInt oDSetPrecision;
		static readonly bool BugWithSetPrecisionExistsInODP;
		static readonly OracleGlobalizationGetThreadInfoDelegate oracleGlobalizationGetThreadInfo;
		static readonly OracleGlobalizationGetNumericCharactersDelegate oracleGlobalizationGetNumericCharacters;
		static ReflectionGetValuesHelper() {
			Type oracleDecimalType = typeof(OD);
			oracleDecimalMax = (OD)Activator.CreateInstance(oracleDecimalType, Decimal.MaxValue);
			oracleDecimalMin = (OD)Activator.CreateInstance(oracleDecimalType, Decimal.MinValue);
			oracleDecimalZero = (OD)Activator.CreateInstance(oracleDecimalType, Decimal.Zero);
			MethodInfo mi = typeof(R).GetMethod("GetOracleDecimal", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(int) }, null);
			getOracleDecimal = (OracleDataReaderGetOracleDecimalDelegate)Delegate.CreateDelegate(typeof(OracleDataReaderGetOracleDecimalDelegate), null, mi);
			mi = oracleDecimalType.GetMethod("GreaterThan", BindingFlags.Public | BindingFlags.Static, null, new Type[] { oracleDecimalType, oracleDecimalType }, null);
			oDGreaterThan = (OracleDecimalComparisonDelegate)Delegate.CreateDelegate(typeof(OracleDecimalComparisonDelegate), mi);
			mi = oracleDecimalType.GetMethod("Equals", BindingFlags.Public | BindingFlags.Static, null, new Type[] { oracleDecimalType, oracleDecimalType }, null);
			oDEquals = (OracleDecimalComparisonDelegate)Delegate.CreateDelegate(typeof(OracleDecimalComparisonDelegate), mi);
			mi = oracleDecimalType.GetMethod("SetPrecision", BindingFlags.Public | BindingFlags.Static, null, new Type[] { oracleDecimalType, typeof(int) }, null);
			oDSetPrecision = (OracleDecimalOperationWithInt)Delegate.CreateDelegate(typeof(OracleDecimalOperationWithInt), mi);
			MethodInfo[] miList = oracleDecimalType.GetMethods(BindingFlags.Public | BindingFlags.Static);
			for(int i = 0; i < miList.Length; i++) {
				MethodInfo currentMi = miList[i];
				if(currentMi.Name == "op_Explicit") {
					if(currentMi.ReturnType == typeof(double)) {
						oDToDouble = (OracleDecimalToDouble)Delegate.CreateDelegate(typeof(OracleDecimalToDouble), currentMi);
					}
					else if(currentMi.ReturnType == typeof(decimal)) {
						oDToDecimal = (OracleDecimalToDecimal)Delegate.CreateDelegate(typeof(OracleDecimalToDecimal), currentMi);
					}
				}
			}
			if(oDToDecimal == null || oDToDouble == null)
				throw new InvalidOperationException("Methods 'ToDecimal' or 'ToDouble' not found.");
			mi = typeof(OG).GetMethod("GetThreadInfo", Array.Empty<Type>());
			if(mi != null) {
				PropertyInfo pi = typeof(OG).GetProperty("NumericCharacters");
				if(pi != null) {
					oracleGlobalizationGetThreadInfo = (OracleGlobalizationGetThreadInfoDelegate)Delegate.CreateDelegate(typeof(OracleGlobalizationGetThreadInfoDelegate), mi);
					oracleGlobalizationGetNumericCharacters = (OracleGlobalizationGetNumericCharactersDelegate)Delegate.CreateDelegate(typeof(OracleGlobalizationGetNumericCharactersDelegate), pi.GetMethod);
				}
			}
			OD testOD = (OD)Activator.CreateInstance(oracleDecimalType, 10304927536231884057971014491M);
			OD truncatedTestOD = oDSetPrecision(testOD, 28);
			BugWithSetPrecisionExistsInODP = oDToDecimal(truncatedTestOD).ToString() != truncatedTestOD.ToString();
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
		readonly ThreadLocal<char> currentOracleDecimalSeparator = new ThreadLocal<char>(GetOracleDecimalSeparator);
		static char GetOracleDecimalSeparator() {
			if(oracleGlobalizationGetNumericCharacters != null) {
				OG threadGlobalization = oracleGlobalizationGetThreadInfo();
				string numChars = oracleGlobalizationGetNumericCharacters(threadGlobalization);
				if(!string.IsNullOrEmpty(numChars)) {
					return numChars[0];
				}
			}
			return '.';
		}
		object GetDecimal(R dataReader, int index) {
			OD od = getOracleDecimal(dataReader, index);
			if(oDGreaterThan(od, oracleDecimalMax) || oDGreaterThan(oracleDecimalMin, od))
				return oDToDouble(od);
			else {
				if(oDEquals(od, oracleDecimalZero)) {
					return 0M;
				}
				else {
					if(BugWithSetPrecisionExistsInODP) {
						string decimalString = od.ToString();
						if(decimalString.Length >= 27) {
							OD truncatedOD = oDSetPrecision(od, 28);
							decimalString = truncatedOD.ToString();
							if(currentOracleDecimalSeparator.Value != '.') {
								decimalString = decimalString.Replace(currentOracleDecimalSeparator.Value, '.');
							}
							return decimal.Parse(decimalString, CultureInfo.InvariantCulture);
						}
						return oDToDecimal(od);
					}
					else {
						OD truncatedOD = oDSetPrecision(od, 28);
						return oDToDecimal(truncatedOD);
					}
				}
			}
		}
		delegate OD OracleDataReaderGetOracleDecimalDelegate(R reader, int i);
		delegate bool OracleDecimalComparisonDelegate(OD left, OD right);
		delegate OD OracleDecimalOperationWithInt(OD od, int i);
		delegate double OracleDecimalToDouble(OD od);
		delegate decimal OracleDecimalToDecimal(OD od);
		delegate OG OracleGlobalizationGetThreadInfoDelegate();
		delegate string OracleGlobalizationGetNumericCharactersDelegate(OG og);
	}
	class DbTypeMapperODP<TSqlDbTypeEnum, TSqlParameter> : DbTypeMapperBaseOracle<TSqlDbTypeEnum, TSqlParameter>, IDbTypeMapperBaseOracle
		where TSqlDbTypeEnum : struct
		where TSqlParameter : IDbDataParameter {
		static readonly TSqlDbTypeEnum OracleDbTypeVarchar2;
		static readonly TSqlDbTypeEnum OracleDbTypeNVarchar2;
		protected override string ParameterDbTypePropertyName { get { return "OracleDbType"; } }
		static DbTypeMapperODP() {
			OracleDbTypeVarchar2 = (TSqlDbTypeEnum)Enum.Parse(typeof(TSqlDbTypeEnum), "Varchar2");
			OracleDbTypeNVarchar2 = (TSqlDbTypeEnum)Enum.Parse(typeof(TSqlDbTypeEnum), "NVarchar2");
		}
		protected override string GetParameterTypeNameForBoolean(out int? size, out byte? precision, out byte? scale) {
			size = null;
			precision = 1;
			scale = 0;
			return "Decimal";
		}
		protected override string GetParameterTypeNameForByte(out byte? precision, out byte? scale) {
			precision = 3;
			scale = 0;
			return "Decimal";
		}
		protected override string GetParameterTypeNameForDecimal(out byte? precision, out byte? scale) {
			precision = 19;
			scale = 5;
			return "Decimal";
		}
		protected override string GetParameterTypeNameForInt16(out byte? precision, out byte? scale) {
			precision = 5;
			scale = 0;
			return "Decimal";
		}
		protected override string GetParameterTypeNameForInt64(out byte? precision, out byte? scale) {
			precision = 20;
			scale = 0;
			return "Decimal";
		}
		protected override string GetParameterTypeNameForSByte(out byte? precision, out byte? scale) {
			precision = 3;
			scale = 0;
			return "Decimal";
		}
		protected override string GetParameterTypeNameForSingle(out byte? precision, out byte? scale) {
			precision = scale = null;
			return "Double";
		}
		protected override string GetParameterTypeNameForUInt16(out byte? precision, out byte? scale) {
			precision = 5;
			scale = 0;
			return "Decimal";
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
		protected override string GetParameterTypeNameForString(out int? size) {
			size = null;
			return "NVarchar2";
		}
		protected override string GetParameterTypeNameForDateTime() {
			return "Date";
		}
		protected override string GetParameterTypeNameForDateOnly(out int? size) {
			size = null;
			return "Date";
		}
		protected override string GetParameterTypeNameForTimeOnly(out int? size) {
			size = null;
			return "Date";
		}
		protected override string ConvertSqlTypeToParameterType(string sqlType) {
			switch(sqlType.ToUpperInvariant()) {
				case "NVARCHAR":
				case "NVARCHAR2":
					return "NVarchar2";
				case "VARCHAR":
				case "VARCHAR2":
					return "Varchar2";
				case "LONG":
					return "Long";
				case "NUMBER":
				case "NUMERIC":
				case "DEC":
				case "DECIMAL":
					return "Decimal";
				case "REAL":
				case "FLOAT":
					return "Decimal";
				case "DOUBLE PRECISION":
					return "Double";
				case "DATE":
					return "Date";
				case "TIMESTAMP":
					return "TimeStamp";
				case "TIMESTAMP WITH LOCAL TIME ZONE":
					return "TimeStampLTZ";
				case "TIMESTAMP WITH TIME ZONE":
					return "TimeStampTZ";
				case "INTERVAL YEAR":
					return "IntervalYM";
				case "INTERVAL DAY":
					return "IntervalDS";
				case "ROWID":
				case "UROWID":
					return "Varchar2";
				case "XMLTYPE":
					return "XmlType";
				case "BINARY_FLOAT":
					return "BinaryFloat";
				case "BINARY_DOUBLE":
					return "BinaryDouble";
				default:
					return base.ConvertSqlTypeToParameterType(sqlType);
			}
		}
		public void SetOracleDbTypeChar(IDbDataParameter parameter, int size) {
			SetSqlDbTypeHandler((TSqlParameter)parameter, OracleDbTypeChar);
			parameter.Size = size;
		}
		public void FixVarcharParameterType(IDbDataParameter parameter) {
			if(object.Equals(GetSqlDbTypeHandler((TSqlParameter)parameter), OracleDbTypeVarchar2)) { 
				SetSqlDbTypeHandler((TSqlParameter)parameter, OracleDbTypeNVarchar2);
			}
		}
	}
}
