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
using System.Runtime.Serialization;
using System.ServiceModel;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using DevExpress.Data.Filtering;
using System.Configuration;
using System.Xml.Serialization;
using DevExpress.Xpo.DB.Exceptions;
using System.ServiceModel.Channels;
#if !NET
using System.ServiceModel.Activation;
#endif
using DevExpress.Xpo.Helpers;
using DevExpress.Xpo.Exceptions;
using System.Threading.Tasks;
using System.Threading;
using DevExpress.Xpo.DB.Helpers;
namespace DevExpress.Xpo.DB {
	[ServiceContract, XmlSerializerFormat]
	public interface IDataStoreService {
		[OperationContract(Action = "http://tempuri.org/IDataStoreService/ModifyData", ReplyAction = "http://tempuri.org/IDataStoreService/ModifyDataResponse"), XmlSerializerFormat]
		[ServiceKnownType(typeof(DeleteStatement))]
		[ServiceKnownType(typeof(InsertStatement))]
		[ServiceKnownType(typeof(UpdateStatement))]
		[ServiceKnownType(typeof(AggregateOperand))]
		[ServiceKnownType(typeof(BetweenOperator))]
		[ServiceKnownType(typeof(BinaryOperator))]
		[ServiceKnownType(typeof(ContainsOperator))]
		[ServiceKnownType(typeof(FunctionOperator))]
		[ServiceKnownType(typeof(GroupOperator))]
		[ServiceKnownType(typeof(InOperator))]
		[ServiceKnownType(typeof(NotOperator))]
		[ServiceKnownType(typeof(NullOperator))]
		[ServiceKnownType(typeof(OperandProperty))]
		[ServiceKnownType(typeof(OperandValue))]
		[ServiceKnownType(typeof(ParameterValue))]
		[ServiceKnownType(typeof(QueryOperand))]
		[ServiceKnownType(typeof(UnaryOperator))]
		[ServiceKnownType(typeof(JoinOperand))]
		[ServiceKnownType(typeof(OperandParameter))]
		[ServiceKnownType(typeof(QuerySubQueryContainer))]
		OperationResult<ModificationResult> ModifyData(ModificationStatement[] dmlStatements);
		[OperationContract(Action = "http://tempuri.org/IDataStoreService/SelectData", ReplyAction = "http://tempuri.org/IDataStoreService/SelectDataResponse"), XmlSerializerFormat]
		[ServiceKnownType(typeof(AggregateOperand))]
		[ServiceKnownType(typeof(BetweenOperator))]
		[ServiceKnownType(typeof(BinaryOperator))]
		[ServiceKnownType(typeof(ContainsOperator))]
		[ServiceKnownType(typeof(FunctionOperator))]
		[ServiceKnownType(typeof(GroupOperator))]
		[ServiceKnownType(typeof(InOperator))]
		[ServiceKnownType(typeof(NotOperator))]
		[ServiceKnownType(typeof(NullOperator))]
		[ServiceKnownType(typeof(OperandProperty))]
		[ServiceKnownType(typeof(OperandValue))]
		[ServiceKnownType(typeof(ParameterValue))]
		[ServiceKnownType(typeof(QueryOperand))]
		[ServiceKnownType(typeof(UnaryOperator))]
		[ServiceKnownType(typeof(JoinOperand))]
		[ServiceKnownType(typeof(OperandParameter))]
		[ServiceKnownType(typeof(QuerySubQueryContainer))]
		OperationResult<SelectedData> SelectData(SelectStatement[] selects);
		[OperationContract(Action = "http://tempuri.org/IDataStoreService/UpdateSchema", ReplyAction = "http://tempuri.org/IDataStoreService/UpdateSchemaResponse"), XmlSerializerFormat]
		OperationResult<UpdateSchemaResult> UpdateSchema(bool doNotCreateIfFirstTableNotExist, DBTable[] tables);
		[OperationContract(Action = "http://tempuri.org/IDataStoreService/GetAutoCreateOption", ReplyAction = "http://tempuri.org/IDataStoreService/GetAutoCreateOptionResponse"), XmlSerializerFormat]
		OperationResult<AutoCreateOption> GetAutoCreateOption();
		[ServiceKnownType(typeof(CommandChannelHelper.SprocQuery))]
		[ServiceKnownType(typeof(CommandChannelHelper.SqlQuery))]
		[OperationContract(Action = "http://tempuri.org/IDataStoreService/Do", ReplyAction = "http://tempuri.org/IDataStoreService/DoResponse"), XmlSerializerFormat]
		OperationResult<object> Do(string command, object args);
	}
	[ServiceContract, XmlSerializerFormat]
	public interface IDataStoreServiceAsync : IDataStoreService {
		[OperationContract(Action = "http://tempuri.org/IDataStoreService/ModifyData", ReplyAction = "http://tempuri.org/IDataStoreService/ModifyDataResponse"), XmlSerializerFormat]
		Task<OperationResult<ModificationResult>> ModifyDataAsync(ModificationStatement[] dmlStatements);
		[OperationContract(Action = "http://tempuri.org/IDataStoreService/SelectData", ReplyAction = "http://tempuri.org/IDataStoreService/SelectDataResponse"), XmlSerializerFormat]
		Task<OperationResult<SelectedData>> SelectDataAsync(SelectStatement[] selects);
		[OperationContract(Action = "http://tempuri.org/IDataStoreService/UpdateSchema", ReplyAction = "http://tempuri.org/IDataStoreService/UpdateSchemaResponse"), XmlSerializerFormat]
		Task<OperationResult<UpdateSchemaResult>> UpdateSchemaAsync(bool doNotCreateIfFirstTableNotExist, DBTable[] tables);
		[OperationContract(Action = "http://tempuri.org/IDataStoreService/GetAutoCreateOption", ReplyAction = "http://tempuri.org/IDataStoreService/GetAutoCreateOptionResponse"), XmlSerializerFormat]
		Task<OperationResult<AutoCreateOption>> GetAutoCreateOptionAsync();
		[OperationContract(Action = "http://tempuri.org/IDataStoreService/Do", ReplyAction = "http://tempuri.org/IDataStoreService/DoResponse"), XmlSerializerFormat]
		Task<OperationResult<object>> DoAsync(string command, object args);
	}
	[ServiceContract, XmlSerializerFormat]
	public interface IDataStoreWarpService : IDataStoreService {
		[OperationContract(Action = "http://tempuri.org/IDataStoreService/WarpSelectData", ReplyAction = "http://tempuri.org/IDataStoreService/WarpSelectDataResponse"), XmlSerializerFormat]
		OperationResult<byte[]> WarpSelectData(SelectStatement[] selects);
	}
	[ServiceContract, XmlSerializerFormat]
	public interface IDataStoreWarpServiceAsync: IDataStoreServiceAsync, IDataStoreWarpService {
		[OperationContract(Action = "http://tempuri.org/IDataStoreService/WarpSelectData", ReplyAction = "http://tempuri.org/IDataStoreService/WarpSelectDataResponse"), XmlSerializerFormat]
		Task<OperationResult<byte[]>> WarpSelectDataAsync(SelectStatement[] selects);
	}
	public class ServiceBase {
		public static event ServiceExceptionHandler GlobalServiceExceptionThrown;
		public event ServiceExceptionHandler ServiceExceptionThrown;
		protected virtual void OnServiceExceptionThrown(Exception ex) {
			ServiceExceptionEventArgs args = new ServiceExceptionEventArgs(ex);
			ServiceExceptionHandler handler = ServiceExceptionThrown;
			if(handler != null) {
				handler(this, args);
			}
			handler = GlobalServiceExceptionThrown;
			if(handler != null) {
				handler(this, args);
			}
		}
		protected OperationResult<T> Execute<T>(OperationResultPredicate<T> predicate) {
			try {
				return new OperationResult<T>(predicate());
			} catch(Exception ex) {
				return WrapException<T>(ex);
			}
		}
		protected async Task<OperationResult<T>> ExecuteAsync<T>(Task<T> resultAwaiter) {
			try {
				T result = await resultAwaiter.ConfigureAwait(false);
				return new OperationResult<T>(result);
			} catch(Exception ex) {
				return WrapException<T>(ex);
			}
		}
		protected OperationResult<T> WrapException<T>(Exception ex) {
			OnServiceExceptionThrown(ex);
			if(ex is NotSupportedException)
				return new OperationResult<T>(ServiceException.NotSupported, ex.Message);
			else if(ex is SchemaCorrectionNeededException)
				return new OperationResult<T>(ServiceException.Schema, ex.Message);
			else if(ex is LockingException)
				return new OperationResult<T>(ServiceException.Locking, string.Empty);
			else if(ex is ObjectLayerSecurityException)
				return new OperationResult<T>(ServiceException.ObjectLayerSecurity, OperationResult.SerializeSecurityException((ObjectLayerSecurityException)ex));
			else
				return new OperationResult<T>(ServiceException.Unknown, ex.Message);
		}
	}
	public class DataStoreService : ServiceBase, IDataStoreWarpService {
		protected readonly IDataStore provider;
		protected readonly ICommandChannel commandChannel;
		public DataStoreService(IDataStore provider) {
			this.provider = provider;
			this.commandChannel = provider as ICommandChannel;
		}
		public virtual OperationResult<ModificationResult> ModifyData(ModificationStatement[] dmlStatements) {
			return Execute<ModificationResult>(delegate() { return provider.ModifyData(dmlStatements); });
		}
		public virtual OperationResult<SelectedData> SelectData(SelectStatement[] selects) {
			return Execute<SelectedData>(delegate() { return provider.SelectData(selects); });
		}
		public virtual OperationResult<byte[]> WarpSelectData(SelectStatement[] selects) {
			return Execute<byte[]>(delegate() {
				SelectedData selectedData = provider.SelectData(selects);
				return WcfUsedAsDumbPipeHelper.Warp(selectedData == null ? null : selectedData.ResultSet);
			});
		}
		public virtual OperationResult<UpdateSchemaResult> UpdateSchema(bool doNotCreateIfFirstTableNotExist, DBTable[] tables) {
			return Execute<UpdateSchemaResult>(delegate() { return provider.UpdateSchema(doNotCreateIfFirstTableNotExist, tables); });
		}
		public virtual OperationResult<AutoCreateOption> GetAutoCreateOption() {
			return Execute<AutoCreateOption>(delegate() { return provider.AutoCreateOption; });
		}
		public virtual OperationResult<object> Do(string command, object args) {
			if(commandChannel == null) {
				if(provider == null) {
					return new OperationResult<object>(ServiceException.NotSupported, string.Format(CommandChannelHelper.Message_CommandIsNotSupported, command));
				} else {
					return new OperationResult<object>(ServiceException.NotSupported, string.Format(CommandChannelHelper.Message_CommandIsNotSupportedEx, command, provider.GetType().FullName));
				}
			}
			return Execute<object>(delegate() { return commandChannel.Do(command, args); });
		}
	}
	public class DataStoreClient : DataStoreClientBase<IDataStoreWarpService> {
#if !NET
		public DataStoreClient(string configName) : base(configName) { }
#endif
		public DataStoreClient(System.ServiceModel.Channels.Binding binding, EndpointAddress remoteAddress) : base(binding, remoteAddress) { }
#if NET
		readonly bool needCustomChannel = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX);
		protected override IDataStoreWarpService CreateChannel() {
			return needCustomChannel ? new DataStoreClientChannel(this) : base.CreateChannel();
		}
		class DataStoreClientChannel : ChannelBase<IDataStoreWarpService>, IDataStoreWarpService {
			public DataStoreClientChannel(DataStoreClient client)
				: base(client) {
			}
			object Invoke(object[] args, [System.Runtime.CompilerServices.CallerMemberName]string methodName = null) {
				return DataStoreServiceInvokeHelper.SafeInvoke(BeginInvoke, EndInvoke, methodName, args);
			}
			public OperationResult<object> Do(string command, object args) {
				return (OperationResult<object>)Invoke(new object[] { command, args });
			}
			public OperationResult<AutoCreateOption> GetAutoCreateOption() {
				return (OperationResult<AutoCreateOption>)Invoke(Array.Empty<object>());
			}
			public OperationResult<ModificationResult> ModifyData(ModificationStatement[] dmlStatements) {
				return (OperationResult<ModificationResult>)Invoke(new object[] { dmlStatements });
			}
			public OperationResult<SelectedData> SelectData(SelectStatement[] selects) {
				return (OperationResult<SelectedData>)Invoke(new object[] { selects });
			}
			public OperationResult<UpdateSchemaResult> UpdateSchema(bool doNotCreateIfFirstTableNotExist, DBTable[] tables) {
				return (OperationResult<UpdateSchemaResult>)Invoke(new object[] { doNotCreateIfFirstTableNotExist, tables });
			}
			public OperationResult<byte[]> WarpSelectData(SelectStatement[] selects) {
				return (OperationResult<byte[]>)Invoke(new object[] { selects });
			}
		}
#endif
	}
	public class DataStoreClientAsync : DataStoreClientAsyncBase<IDataStoreWarpServiceAsync> {
#if !NET
		public DataStoreClientAsync(string configName) : base(configName) { }
#endif
		public DataStoreClientAsync(System.ServiceModel.Channels.Binding binding, EndpointAddress remoteAddress) : base(binding, remoteAddress) { }
#if NET
		readonly bool needCustomChannel = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX)
			|| (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux) && System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription.StartsWith("Mono "));
		protected override IDataStoreWarpServiceAsync CreateChannel() {
			return needCustomChannel ? new DataStoreClientAsyncChannel(this) : base.CreateChannel();
		}
		class DataStoreClientAsyncChannel : ChannelBase<IDataStoreWarpServiceAsync>, IDataStoreWarpServiceAsync {
			public DataStoreClientAsyncChannel(DataStoreClientAsync client)
				: base(client) {
			}
			object Invoke(object[] args, [System.Runtime.CompilerServices.CallerMemberName]string methodName = null) {
				return DataStoreServiceInvokeHelper.SafeInvoke(BeginInvoke, EndInvoke, methodName, args);
			}
			Task<object> InvokeAsync(object[] args, [System.Runtime.CompilerServices.CallerMemberName]string methodName = null) {
				return DataStoreServiceInvokeHelper.SafeInvokeAsync(BeginInvoke, EndInvoke, methodName, args);
			}
			public OperationResult<object> Do(string command, object args) {
				return (OperationResult<object>)Invoke(new object[] { command, args });
			}
			public OperationResult<AutoCreateOption> GetAutoCreateOption() {
				return (OperationResult<AutoCreateOption>)Invoke(Array.Empty<object>());
			}
			public OperationResult<ModificationResult> ModifyData(ModificationStatement[] dmlStatements) {
				return (OperationResult<ModificationResult>)Invoke(new object[] { dmlStatements });
			}
			public OperationResult<SelectedData> SelectData(SelectStatement[] selects) {
				return (OperationResult<SelectedData>)Invoke(new object[] { selects });
			}
			public OperationResult<UpdateSchemaResult> UpdateSchema(bool doNotCreateIfFirstTableNotExist, DBTable[] tables) {
				return (OperationResult<UpdateSchemaResult>)Invoke(new object[] { doNotCreateIfFirstTableNotExist, tables });
			}
			public OperationResult<byte[]> WarpSelectData(SelectStatement[] selects) {
				return (OperationResult<byte[]>)Invoke(new object[] { selects });
			}
			public async Task<OperationResult<object>> DoAsync(string command, object args) {
				return (OperationResult<object>)(await InvokeAsync(new object[] { command, args }, "Do").ConfigureAwait(false));
			}
			public async Task<OperationResult<AutoCreateOption>> GetAutoCreateOptionAsync() {
				return (OperationResult<AutoCreateOption>)(await InvokeAsync(Array.Empty<object>(), "GetAutoCreateOption").ConfigureAwait(false));
			}
			public async Task<OperationResult<ModificationResult>> ModifyDataAsync(ModificationStatement[] dmlStatements) {
				return (OperationResult<ModificationResult>)(await InvokeAsync(new object[] { dmlStatements }, "ModifyData").ConfigureAwait(false));
			}
			public async Task<OperationResult<SelectedData>> SelectDataAsync(SelectStatement[] selects) {
				return (OperationResult<SelectedData>)(await InvokeAsync(new object[] { selects }, "SelectData").ConfigureAwait(false));
			}
			public async Task<OperationResult<UpdateSchemaResult>> UpdateSchemaAsync(bool doNotCreateIfFirstTableNotExist, DBTable[] tables) {
				return (OperationResult<UpdateSchemaResult>)(await InvokeAsync(new object[] { doNotCreateIfFirstTableNotExist, tables }, "UpdateSchema").ConfigureAwait(false));
			}
			public async Task<OperationResult<byte[]>> WarpSelectDataAsync(SelectStatement[] selects) {
				return (OperationResult<byte[]>)(await InvokeAsync(new object[] { selects }, "WarpSelectData").ConfigureAwait(false));
			}
		}
#endif
	}
	public class DataStoreClientBase<TContractType> : ClientBase<TContractType>, IDataStore, ICommandChannel where TContractType : class, IDataStoreWarpService {
#if !NET
		public DataStoreClientBase(string configName) : base(configName) { }
#endif
		public DataStoreClientBase(System.ServiceModel.Channels.Binding binding, EndpointAddress remoteAddress) : base(binding, remoteAddress) { }
		public event ClientChannelCreatedHandler ClientChannelCreated;
		public static event ClientChannelCreatedHandler GlobalDataClientChannelCreated;
		protected virtual void OnClientChannelCreated(object channel) {
			if(ClientChannelCreated != null) {
				ClientChannelCreated(this, new ClientChannelCreatedEventArgs(channel));
			}
			if(GlobalDataClientChannelCreated != null) {
				GlobalDataClientChannelCreated(this, new ClientChannelCreatedEventArgs(channel));
			}
		}
		bool selectDataRawFound;
		bool selectDataRawNotFound;
		TContractType channel;
		protected new TContractType Channel {
			get {
				TContractType currentChannel = channel;
				if(currentChannel == null) {
					currentChannel = CreateChannel();
					channel = currentChannel;
					OnClientChannelCreated(currentChannel);
				}
				return currentChannel;
			}
		}
		ModificationResult IDataStore.ModifyData(ModificationStatement[] dmlStatements) {
			return ExecuteClient<ModificationResult>(delegate() { return Channel.ModifyData(dmlStatements); }, HandleModifyDataException).HandleError();
		}
		SelectedData IDataStore.SelectData(SelectStatement[] selects) {
			if(!selectDataRawNotFound) {
				try {
					SelectedData selectedData = new SelectedData(WcfUsedAsDumbPipeHelper.Unwarp(ExecuteClient<byte[]>(delegate() {
						return ((IDataStoreWarpService)Channel).WarpSelectData(selects);
					}, HandleSelectDataException).HandleError()));
					selectDataRawFound = true;
					return selectedData;
				} catch(Exception) {
					if(selectDataRawFound) throw;
					selectDataRawNotFound = true;
				}
			}
			return ExecuteClient<SelectedData>(delegate() { return Channel.SelectData(selects); }, HandleSelectDataException).HandleError();
		}
		UpdateSchemaResult IDataStore.UpdateSchema(bool doNotCreateIfFirstTableNotExist, params DBTable[] tables) {
			return ExecuteClient<UpdateSchemaResult>(delegate() { return Channel.UpdateSchema(doNotCreateIfFirstTableNotExist, tables); }, HandleUpdateSchemaException).HandleError();
		}
		AutoCreateOption IDataStore.AutoCreateOption {
			get { return ExecuteClient<AutoCreateOption>(delegate () { return Channel.GetAutoCreateOption(); }, HandleSelectDataException).HandleError(); }
		}
		object ICommandChannel.Do(string command, object args) {
			return ExecuteClient<object>(delegate() { return Channel.Do(command, args); }, HandleCommandChannelException).HandleError();
		}
		public OperationResult<R> ExecuteClient<R>(OperationResultChannelPredicate<R> predicate) {
			return ExecuteClient(predicate, null);
		}
		public OperationResult<R> ExecuteClient<R>(OperationResultChannelPredicate<R> predicate, Func<Exception, bool> exceptionHandler) {
			return OperationResult.ExecuteClient<R, TContractType>(predicate, exceptionHandler, ref channel);
		}
		protected Task<OperationResult<R>> ExecuteClientAsync<R>(OperationResultChannelPredicateAsync<R> predicate) {
			return ExecuteClientAsync(predicate, null);
		}
		protected Task<OperationResult<R>> ExecuteClientAsync<R>(OperationResultChannelPredicateAsync<R> predicate, Func<Exception, bool> exceptionHandler) {
			return OperationResult.ExecuteClientAsync<R>(predicate, exceptionHandler, () => channel = default(TContractType));
		}
		protected virtual bool HandleModifyDataException(Exception ex) {
			return !(ex is TimeoutException);
		}
		protected virtual bool HandleSelectDataException(Exception ex) {
			return true;
		}
		protected virtual bool HandleUpdateSchemaException(Exception ex) {
			return true;
		}
		protected virtual bool HandleCommandChannelException(Exception ex) {
			return true;
		}
	}
	public class DataStoreClientAsyncBase<TContractType> : DataStoreClientBase<TContractType>, IDataStoreAsync, ICommandChannelAsync where TContractType : class, IDataStoreWarpServiceAsync {
#if !NET
		public DataStoreClientAsyncBase(string configName) : base(configName) { }
#endif
		public DataStoreClientAsyncBase(System.ServiceModel.Channels.Binding binding, EndpointAddress remoteAddress) : base(binding, remoteAddress) { }
		bool selectDataRawFound;
		bool selectDataRawNotFound;
		public async Task<SelectedData> SelectDataAsync(CancellationToken cancellationToken, params SelectStatement[] selects) {
			if(!selectDataRawNotFound) {
				try {
					var warpSelectResult = await ExecuteClientAsync<byte[]>(delegate () {
						return ((IDataStoreWarpServiceAsync)Channel).WarpSelectDataAsync(selects);
					}, HandleSelectDataException).ConfigureAwait(false);
					SelectedData selectedData = new SelectedData(WcfUsedAsDumbPipeHelper.Unwarp(warpSelectResult.HandleError()));
					selectDataRawFound = true;
					return selectedData;
				} catch(Exception) {
					if(selectDataRawFound) throw;
					selectDataRawNotFound = true;
				}
			}
			var selectResult = await ExecuteClientAsync<SelectedData>(delegate () { return Channel.SelectDataAsync(selects); }, HandleSelectDataException).ConfigureAwait(false);
			return selectResult.HandleError();
		}
		public async Task<ModificationResult> ModifyDataAsync(CancellationToken cancellationToken, params ModificationStatement[] dmlStatements) {
			var opResult = await ExecuteClientAsync<ModificationResult>(delegate () { return Channel.ModifyDataAsync(dmlStatements); }, HandleModifyDataException).ConfigureAwait(false);
			return opResult.HandleError();
		}
		public async Task<UpdateSchemaResult> UpdateSchemaAsync(CancellationToken cancellationToken, bool doNotCreateIfFirstTableNotExist, params DBTable[] tables) {
			var opResult = await ExecuteClientAsync<UpdateSchemaResult>(delegate () { return Channel.UpdateSchemaAsync(doNotCreateIfFirstTableNotExist, tables); }, HandleUpdateSchemaException).ConfigureAwait(false);
			return opResult.HandleError();
		}
		public async Task<object> DoAsync(string command, object args, CancellationToken cancellationToken = default(CancellationToken)) {
			var opResult = await ExecuteClientAsync<object>(delegate () { return Channel.DoAsync(command, args); }, HandleCommandChannelException).ConfigureAwait(false);
			return opResult.HandleError();
		}
	}
	public class OperationResult {
		const string SerializeSeparator = "#|O_o|#";
		public ServiceException ErrorType;
		public string Error;
		public OperationResult() { }
		public OperationResult(ServiceException errorType, string error) {
			ErrorType = errorType;
			Error = error;
		}
		public static int ExecuteClientMaxRetryCount = 3;
		public static string SerializeSecurityException(ObjectLayerSecurityException securityException) {
			return string.Join(SerializeSeparator, new string[] { securityException.TypeName, securityException.PropertyName, securityException.IsDeletion ? "True" : "False" });
		}
		public static ObjectLayerSecurityException DeserializeSecurityException(string data) {
			if(string.IsNullOrEmpty(data)) return null;
			string[] splittedData = data.Split(new string[] { SerializeSeparator }, StringSplitOptions.None);
			if(splittedData.Length != 3) return null;
			if(string.IsNullOrEmpty(splittedData[0])) return new ObjectLayerSecurityException();
			if(!string.IsNullOrEmpty(splittedData[1])) return new ObjectLayerSecurityException(splittedData[0], splittedData[1]);
			return new ObjectLayerSecurityException(splittedData[0], splittedData[2] == "True");
		}
		public static OperationResult<T> ExecuteClient<T, N>(OperationResultChannelPredicate<T> predicate, Func<Exception, bool> exceptionHandler, ref N channel) {
			int count = ExecuteClientMaxRetryCount;
			for(; ; ) {
				try {
					return predicate();
				} catch(Exception ex) {
					channel = default(N);
					if(count-- > 0 && (exceptionHandler == null || exceptionHandler(ex))) {
						continue;
					}
					throw;
				}
			}
		}
		public static OperationResult<T> ExecuteClient<T, N>(OperationResultChannelPredicate<T> predicate, ref N channel) {
			return ExecuteClient(predicate, null, ref channel);
		}
		public static async Task<OperationResult<R>> ExecuteClientAsync<R>(OperationResultChannelPredicateAsync<R> predicate, Func<Exception, bool> exceptionHandler, Action resetChannelAction) {
			int count = OperationResult.ExecuteClientMaxRetryCount;
			for(; ; ) {
				try {
					return await predicate().ConfigureAwait(false);
				} catch(Exception ex) {
					if(resetChannelAction != null) {
						resetChannelAction();
					}
					if(count-- > 0 && (exceptionHandler == null || exceptionHandler(ex))) {
						continue;
					}
					throw;
				}
			}
		}
		public static Task<OperationResult<R>> ExecuteClientAsync<R>(OperationResultChannelPredicateAsync<R> predicate, Action resetChannelAction) {
			return ExecuteClientAsync(predicate, null, resetChannelAction);
		}
	}
	public class OperationResult<T> : OperationResult {
		public T Result;
		public OperationResult() : base() { }
		public OperationResult(ServiceException errorType, string error)
			: base(errorType, error) {
		}
		public OperationResult(T result)
			: this(ServiceException.None, null) {
			Result = result;
		}
		public T HandleError() {
			switch(ErrorType) {
				case ServiceException.Unknown:
					throw new Exception(Error);
				case ServiceException.Schema:
					throw new SchemaCorrectionNeededException(Error);
				case ServiceException.NotSupported:
					throw new NotSupportedException(Error);
				case ServiceException.ObjectLayerSecurity: {
						ObjectLayerSecurityException ex = DeserializeSecurityException(Error);
						if(ex == null) ex = new ObjectLayerSecurityException();
						throw ex;
					}
				case ServiceException.Locking:
					throw new LockingException();
			}
			return Result;
		}
	}
	public class ClientChannelCreatedEventArgs : EventArgs {
		object channel;
		public object Channel { get { return channel; } }
		public ClientChannelCreatedEventArgs(object channel) {
			this.channel = channel;
		}
	}
	public delegate void ClientChannelCreatedHandler(object sender, ClientChannelCreatedEventArgs e);
	public delegate T OperationResultPredicate<T>();
	public delegate OperationResult<T> OperationResultChannelPredicate<T>();
	public delegate Task<OperationResult<T>> OperationResultChannelPredicateAsync<T>();
	public enum ServiceException { None, Unknown, Schema, NotSupported, Locking, ObjectLayerSecurity }
	public delegate void ServiceExceptionHandler(object sender, ServiceExceptionEventArgs e);
	public class ServiceExceptionEventArgs : EventArgs {
		Exception exception;
		public Exception Exception { get { return exception; } }
		public ServiceExceptionEventArgs(Exception exception) {
			this.exception = exception;
		}
	}
}
#if NET
namespace DevExpress.Xpo.DB.Helpers {
	public static class DataStoreServiceInvokeHelper {
		public static object SafeInvoke(Func<string, object[], AsyncCallback, object, IAsyncResult> beginInvoke, Func<string, object[], IAsyncResult, object> endInvoke, string methodName, object[] args) {
			var asyncResult = beginInvoke(methodName, args, null, null);
			for(int i = 0; !asyncResult.IsCompleted && i < 1000; i++) {
				Thread.Sleep(0);
			}
			for(int i = 0; !asyncResult.IsCompleted && i < 1000; i++) {
				Thread.Sleep(10);
			}
			while(!asyncResult.IsCompleted) {
				Thread.Sleep(100);
			}
			return endInvoke(methodName, args, asyncResult);
		}
		public static Task<object> SafeInvokeAsync(Func<string, object[], AsyncCallback, object, IAsyncResult> beginInvoke, Func<string, object[], IAsyncResult, object> endInvoke, string methodName, object[] args) {
			var asyncResult = beginInvoke(methodName, args, null, null);
			return Task.Factory.FromAsync<object>(asyncResult, ar => endInvoke(methodName, args, ar));
		}
	}
}
#endif
