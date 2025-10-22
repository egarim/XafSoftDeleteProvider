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
using DevExpress.Xpo.DB.Helpers;
using DevExpress.Xpo.Helpers;
using System.Threading;
using System.Threading.Tasks;
namespace DevExpress.Xpo.DB {
	[ServiceContract, XmlSerializerFormat]
	public interface ICachedDataStoreService: IDataStoreService {
		[OperationContract(Action = "http://tempuri.org/ICachedDataStoreService/ModifyDataCached", ReplyAction = "http://tempuri.org/ICachedDataStoreService/ModifyDataCachedResponse"), XmlSerializerFormat]
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
		OperationResult<DataCacheModificationResult> ModifyDataCached(DataCacheCookie cookie, ModificationStatement[] dmlStatements);
		[OperationContract(Action = "http://tempuri.org/ICachedDataStoreService/NotifyDirtyTables", ReplyAction = "http://tempuri.org/ICachedDataStoreService/NotifyDirtyTablesResponse"), XmlSerializerFormat]
		OperationResult<DataCacheResult> NotifyDirtyTables(DataCacheCookie cookie, params string[] dirtyTablesNames);
		[OperationContract(Action = "http://tempuri.org/ICachedDataStoreService/ProcessCookie", ReplyAction = "http://tempuri.org/ICachedDataStoreService/ProcessCookieResponse"), XmlSerializerFormat]
		OperationResult<DataCacheResult> ProcessCookie(DataCacheCookie cookie);
		[OperationContract(Action = "http://tempuri.org/ICachedDataStoreService/SelectDataCached", ReplyAction = "http://tempuri.org/ICachedDataStoreService/SelectDataCachedResponse"), XmlSerializerFormat]
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
		OperationResult<DataCacheSelectDataResult> SelectDataCached(DataCacheCookie cookie, SelectStatement[] selects);
		[OperationContract(Action = "http://tempuri.org/ICachedDataStoreService/UpdateSchemaCached", ReplyAction = "http://tempuri.org/ICachedDataStoreService/UpdateSchemaCachedResponse"), XmlSerializerFormat]
		OperationResult<DataCacheUpdateSchemaResult> UpdateSchemaCached(DataCacheCookie cookie, DBTable[] tables, bool doNotCreateIfFirstTableNotExist);
	}
	[ServiceContract, XmlSerializerFormat]
	public interface ICachedDataStoreServiceAsync : ICachedDataStoreService, IDataStoreServiceAsync {
		[OperationContract(Action = "http://tempuri.org/ICachedDataStoreService/ModifyDataCached", ReplyAction = "http://tempuri.org/ICachedDataStoreService/ModifyDataCachedResponse"), XmlSerializerFormat]
		Task<OperationResult<DataCacheModificationResult>> ModifyDataCachedAsync(DataCacheCookie cookie, ModificationStatement[] dmlStatements);
		[OperationContract(Action = "http://tempuri.org/ICachedDataStoreService/NotifyDirtyTables", ReplyAction = "http://tempuri.org/ICachedDataStoreService/NotifyDirtyTablesResponse"), XmlSerializerFormat]
		Task<OperationResult<DataCacheResult>> NotifyDirtyTablesAsync(DataCacheCookie cookie, params string[] dirtyTablesNames);
		[OperationContract(Action = "http://tempuri.org/ICachedDataStoreService/ProcessCookie", ReplyAction = "http://tempuri.org/ICachedDataStoreService/ProcessCookieResponse"), XmlSerializerFormat]
		Task<OperationResult<DataCacheResult>> ProcessCookieAsync(DataCacheCookie cookie);
		[OperationContract(Action = "http://tempuri.org/ICachedDataStoreService/SelectDataCached", ReplyAction = "http://tempuri.org/ICachedDataStoreService/SelectDataCachedResponse"), XmlSerializerFormat]
		Task<OperationResult<DataCacheSelectDataResult>> SelectDataCachedAsync(DataCacheCookie cookie, SelectStatement[] selects);
		[OperationContract(Action = "http://tempuri.org/ICachedDataStoreService/UpdateSchemaCached", ReplyAction = "http://tempuri.org/ICachedDataStoreService/UpdateSchemaCachedResponse"), XmlSerializerFormat]
		Task<OperationResult<DataCacheUpdateSchemaResult>> UpdateSchemaCachedAsync(DataCacheCookie cookie, DBTable[] tables, bool doNotCreateIfFirstTableNotExist);
	}
	[ServiceContract, XmlSerializerFormat]
	public interface ICachedDataStoreWarpService : ICachedDataStoreService, IDataStoreWarpService {
		[OperationContract(Action = "http://tempuri.org/ICachedDataStoreService/WarpSelectDataCached", ReplyAction = "http://tempuri.org/ICachedDataStoreService/WarpSelectDataCachedResponse"), XmlSerializerFormat]
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
		OperationResult<DataCacheWarpSelectDataResult> WarpSelectDataCached(DataCacheCookie cookie, SelectStatement[] selects);
	}
	[ServiceContract, XmlSerializerFormat]
	public interface ICachedDataStoreWarpServiceAsync : ICachedDataStoreServiceAsync, IDataStoreWarpServiceAsync {
		[OperationContract(Action = "http://tempuri.org/ICachedDataStoreService/WarpSelectDataCached", ReplyAction = "http://tempuri.org/ICachedDataStoreService/WarpSelectDataCachedResponse"), XmlSerializerFormat]
		Task<OperationResult<DataCacheWarpSelectDataResult>> WarpSelectDataCachedAsync(DataCacheCookie cookie, SelectStatement[] selects);
	}
	public class DataCacheWarpSelectDataResult : DataCacheResult {
		public byte[] SelectResult;
		public DataCacheCookie SelectingCookie;
		public DataCacheWarpSelectDataResult() : base() { }
		public DataCacheWarpSelectDataResult(DataCacheSelectDataResult selectedData)
			: this() {
			SelectResult = WcfUsedAsDumbPipeHelper.Warp(selectedData.SelectedData == null ? null : selectedData.SelectedData.ResultSet);
			SelectingCookie = selectedData.SelectingCookie;
			CacheConfig = selectedData.CacheConfig;
			Cookie = selectedData.Cookie;
			UpdatedTableAges = selectedData.UpdatedTableAges;
		}
		public DataCacheSelectDataResult GetResult() {
			DataCacheSelectDataResult result = new DataCacheSelectDataResult();
			result.SelectedData = new SelectedData(WcfUsedAsDumbPipeHelper.Unwarp(SelectResult));
			result.SelectingCookie = SelectingCookie;
			result.CacheConfig = CacheConfig;
			result.Cookie = Cookie;
			result.UpdatedTableAges = UpdatedTableAges;
			return result;
		}
	}
	public class CachedDataStoreService : DataStoreService, ICachedDataStoreWarpService {
		ICachedDataStore cachedProvider;
		public CachedDataStoreService(ICachedDataStore provider)
			: base(provider) {
				cachedProvider = provider;
		}
		public virtual OperationResult<DataCacheModificationResult> ModifyDataCached(DataCacheCookie cookie, ModificationStatement[] dmlStatements) {
			return Execute<DataCacheModificationResult>(delegate() { return cachedProvider.ModifyData(cookie, dmlStatements); });
		}
		public virtual OperationResult<DataCacheResult> NotifyDirtyTables(DataCacheCookie cookie, params string[] dirtyTablesNames) {
			return Execute<DataCacheResult>(delegate() { return cachedProvider.NotifyDirtyTables(cookie, dirtyTablesNames); });
		}
		public virtual OperationResult<DataCacheResult> ProcessCookie(DataCacheCookie cookie) {
			return Execute<DataCacheResult>(delegate() { return cachedProvider.ProcessCookie(cookie); });
		}
		public virtual OperationResult<DataCacheSelectDataResult> SelectDataCached(DataCacheCookie cookie, SelectStatement[] selects) {
			return Execute<DataCacheSelectDataResult>(delegate() { return cachedProvider.SelectData(cookie, selects); });
		}
		public virtual OperationResult<DataCacheUpdateSchemaResult> UpdateSchemaCached(DataCacheCookie cookie, DBTable[] tables, bool doNotCreateIfFirstTableNotExist) {
			return Execute<DataCacheUpdateSchemaResult>(delegate() { return cachedProvider.UpdateSchema(cookie, tables, doNotCreateIfFirstTableNotExist); });
		}
		public virtual OperationResult<DataCacheWarpSelectDataResult> WarpSelectDataCached(DataCacheCookie cookie, SelectStatement[] selects) {
			return Execute<DataCacheWarpSelectDataResult>(delegate() {  return new DataCacheWarpSelectDataResult(cachedProvider.SelectData(cookie, selects)); });
		}
	}
	public class CachedDataStoreClient : CachedDataStoreClientBase<ICachedDataStoreWarpService>
	{
#if !NET
		public CachedDataStoreClient(string configName) : base(configName) { }
#endif
		public CachedDataStoreClient(System.ServiceModel.Channels.Binding binding, EndpointAddress remoteAddress) : base(binding, remoteAddress) { }
#if NET
		readonly bool needCustomChannel = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX);
		protected override ICachedDataStoreWarpService CreateChannel() {
			return needCustomChannel ? new CachedDataStoreClientChannel(this) : base.CreateChannel();
		}
		class CachedDataStoreClientChannel : ChannelBase<ICachedDataStoreWarpService>, ICachedDataStoreWarpService {
			public CachedDataStoreClientChannel(CachedDataStoreClient client)
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
			public OperationResult<DataCacheModificationResult> ModifyDataCached(DataCacheCookie cookie, ModificationStatement[] dmlStatements) {
				return (OperationResult<DataCacheModificationResult>)Invoke(new object[] { cookie, dmlStatements });
			}
			public OperationResult<DataCacheResult> NotifyDirtyTables(DataCacheCookie cookie, params string[] dirtyTablesNames) {
				return (OperationResult<DataCacheResult>)Invoke(new object[] { cookie, dirtyTablesNames });
			}
			public OperationResult<DataCacheResult> ProcessCookie(DataCacheCookie cookie) {
				return (OperationResult<DataCacheResult>)Invoke(new object[] { cookie });
			}
			public OperationResult<SelectedData> SelectData(SelectStatement[] selects) {
				return (OperationResult<SelectedData>)Invoke(new object[] { selects });
			}
			public OperationResult<DataCacheSelectDataResult> SelectDataCached(DataCacheCookie cookie, SelectStatement[] selects) {
				return (OperationResult<DataCacheSelectDataResult>)Invoke(new object[] { cookie, selects });
			}
			public OperationResult<UpdateSchemaResult> UpdateSchema(bool doNotCreateIfFirstTableNotExist, DBTable[] tables) {
				return (OperationResult<UpdateSchemaResult>)Invoke(new object[] { doNotCreateIfFirstTableNotExist, tables });
			}
			public OperationResult<DataCacheUpdateSchemaResult> UpdateSchemaCached(DataCacheCookie cookie, DBTable[] tables, bool doNotCreateIfFirstTableNotExist) {
				return (OperationResult<DataCacheUpdateSchemaResult>)Invoke(new object[] { cookie, tables, doNotCreateIfFirstTableNotExist });
			}
			public OperationResult<byte[]> WarpSelectData(SelectStatement[] selects) {
				return (OperationResult<byte[]>)Invoke(new object[] { selects });
			}
			public OperationResult<DataCacheWarpSelectDataResult> WarpSelectDataCached(DataCacheCookie cookie, SelectStatement[] selects) {
				return (OperationResult<DataCacheWarpSelectDataResult>)Invoke(new object[] { cookie, selects });
			}
		}
#endif
	}
	public class CachedDataStoreClientAsync : CachedDataStoreClientAsyncBase<ICachedDataStoreWarpServiceAsync> {
#if !NET
		public CachedDataStoreClientAsync(string configName) : base(configName) { }
#endif
		public CachedDataStoreClientAsync(System.ServiceModel.Channels.Binding binding, EndpointAddress remoteAddress) : base(binding, remoteAddress) { }
#if NET
		readonly bool needCustomChannel = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX)
			|| (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux) && System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription.StartsWith("Mono "));
		protected override ICachedDataStoreWarpServiceAsync CreateChannel() {
			return needCustomChannel ? new CachedDataStoreClientChannelAsync(this) : base.CreateChannel();
		}
		class CachedDataStoreClientChannelAsync : ChannelBase<ICachedDataStoreWarpServiceAsync>, ICachedDataStoreWarpServiceAsync {
			public CachedDataStoreClientChannelAsync(CachedDataStoreClientAsync client)
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
			public OperationResult<DataCacheModificationResult> ModifyDataCached(DataCacheCookie cookie, ModificationStatement[] dmlStatements) {
				return (OperationResult<DataCacheModificationResult>)Invoke(new object[] { cookie, dmlStatements });
			}
			public OperationResult<DataCacheResult> NotifyDirtyTables(DataCacheCookie cookie, params string[] dirtyTablesNames) {
				return (OperationResult<DataCacheResult>)Invoke(new object[] { cookie, dirtyTablesNames });
			}
			public OperationResult<DataCacheResult> ProcessCookie(DataCacheCookie cookie) {
				return (OperationResult<DataCacheResult>)Invoke(new object[] { cookie });
			}
			public OperationResult<SelectedData> SelectData(SelectStatement[] selects) {
				return (OperationResult<SelectedData>)Invoke(new object[] { selects });
			}
			public OperationResult<DataCacheSelectDataResult> SelectDataCached(DataCacheCookie cookie, SelectStatement[] selects) {
				return (OperationResult<DataCacheSelectDataResult>)Invoke(new object[] { cookie, selects });
			}
			public OperationResult<UpdateSchemaResult> UpdateSchema(bool doNotCreateIfFirstTableNotExist, DBTable[] tables) {
				return (OperationResult<UpdateSchemaResult>)Invoke(new object[] { doNotCreateIfFirstTableNotExist, tables });
			}
			public OperationResult<DataCacheUpdateSchemaResult> UpdateSchemaCached(DataCacheCookie cookie, DBTable[] tables, bool doNotCreateIfFirstTableNotExist) {
				return (OperationResult<DataCacheUpdateSchemaResult>)Invoke(new object[] { cookie, tables, doNotCreateIfFirstTableNotExist });
			}
			public OperationResult<byte[]> WarpSelectData(SelectStatement[] selects) {
				return (OperationResult<byte[]>)Invoke(new object[] { selects });
			}
			public OperationResult<DataCacheWarpSelectDataResult> WarpSelectDataCached(DataCacheCookie cookie, SelectStatement[] selects) {
				return (OperationResult<DataCacheWarpSelectDataResult>)Invoke(new object[] { cookie, selects });
			}
			public async Task<OperationResult<DataCacheWarpSelectDataResult>> WarpSelectDataCachedAsync(DataCacheCookie cookie, SelectStatement[] selects) {
				return (OperationResult<DataCacheWarpSelectDataResult>)(await InvokeAsync(new object[] { cookie, selects }, "WarpSelectDataCached").ConfigureAwait(false));
			}
			public async Task<OperationResult<DataCacheModificationResult>> ModifyDataCachedAsync(DataCacheCookie cookie, ModificationStatement[] dmlStatements) {
				return (OperationResult<DataCacheModificationResult>)(await InvokeAsync(new object[] { cookie, dmlStatements }, "ModifyDataCached").ConfigureAwait(false));
			}
			public async Task<OperationResult<DataCacheResult>> NotifyDirtyTablesAsync(DataCacheCookie cookie, params string[] dirtyTablesNames) {
				return (OperationResult<DataCacheResult>)(await InvokeAsync(new object[] { cookie, dirtyTablesNames }, "NotifyDirtyTables").ConfigureAwait(false));
			}
			public async Task<OperationResult<DataCacheResult>> ProcessCookieAsync(DataCacheCookie cookie) {
				return (OperationResult<DataCacheResult>)(await InvokeAsync(new object[] { cookie }, "ProcessCookie").ConfigureAwait(false));
			}
			public async Task<OperationResult<DataCacheSelectDataResult>> SelectDataCachedAsync(DataCacheCookie cookie, SelectStatement[] selects) {
				return (OperationResult<DataCacheSelectDataResult>)(await InvokeAsync(new object[] { cookie, selects }, "SelectDataCached").ConfigureAwait(false));
			}
			public async Task<OperationResult<DataCacheUpdateSchemaResult>> UpdateSchemaCachedAsync(DataCacheCookie cookie, DBTable[] tables, bool doNotCreateIfFirstTableNotExist) {
				return (OperationResult<DataCacheUpdateSchemaResult>)(await InvokeAsync(new object[] { cookie, tables, doNotCreateIfFirstTableNotExist }, "UpdateSchemaCached").ConfigureAwait(false));
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
	public class CachedDataStoreClientBase<TContractType> : DataStoreClientBase<TContractType>, ICachedDataStore, ICommandChannel where TContractType: class, ICachedDataStoreWarpService
   {
#if !NET
		public CachedDataStoreClientBase(string configName) : base(configName) { }
#endif
		public CachedDataStoreClientBase(System.ServiceModel.Channels.Binding binding, EndpointAddress remoteAddress) : base(binding, remoteAddress) { }
		bool selectDataRawFound;
		bool selectDataRawNotFound;
		DataCacheModificationResult ICacheToCacheCommunicationCore.ModifyData(DataCacheCookie cookie, ModificationStatement[] dmlStatements) {
			return ExecuteClient<DataCacheModificationResult>(delegate() { return Channel.ModifyDataCached(cookie, dmlStatements); }, HandleModifyDataException).HandleError();
		}
		DataCacheResult ICacheToCacheCommunicationCore.NotifyDirtyTables(DataCacheCookie cookie, params string[] dirtyTablesNames) {
			return ExecuteClient<DataCacheResult>(delegate() { return Channel.NotifyDirtyTables(cookie, dirtyTablesNames); }, HandleCacheToCacheCommunicationException).HandleError();
		}
		DataCacheResult ICacheToCacheCommunicationCore.ProcessCookie(DataCacheCookie cookie) {
			return ExecuteClient<DataCacheResult>(delegate() { return Channel.ProcessCookie(cookie); }, HandleCacheToCacheCommunicationException).HandleError();
		}
		DataCacheSelectDataResult ICacheToCacheCommunicationCore.SelectData(DataCacheCookie cookie, SelectStatement[] selects) {
			if(!selectDataRawNotFound) {
				try {
					DataCacheSelectDataResult selecteData = ExecuteClient<DataCacheWarpSelectDataResult>(delegate() { 
						return ((ICachedDataStoreWarpService)Channel).WarpSelectDataCached(cookie, selects); 
					}, HandleSelectDataException).HandleError().GetResult();
					selectDataRawFound = true;
					return selecteData;
				} catch(Exception) {
					if(selectDataRawFound) throw;
					selectDataRawNotFound = true;
				}
			}
			return ExecuteClient<DataCacheSelectDataResult>(delegate() { return Channel.SelectDataCached(cookie, selects); }, HandleSelectDataException).HandleError();
		}
		DataCacheUpdateSchemaResult ICacheToCacheCommunicationCore.UpdateSchema(DataCacheCookie cookie, DBTable[] tables, bool doNotCreateIfFirstTableNotExist) {
			return ExecuteClient<DataCacheUpdateSchemaResult>(delegate() { 
				return Channel.UpdateSchemaCached(cookie, tables, doNotCreateIfFirstTableNotExist); 
			}, HandleUpdateSchemaException).HandleError();
		}
		protected virtual bool HandleCacheToCacheCommunicationException(Exception ex) {
			return true;
		}
	}
	public class CachedDataStoreClientAsyncBase<TContractType> : DataStoreClientAsyncBase<TContractType>, ICachedDataStoreAsync, ICommandChannel, ICommandChannelAsync where TContractType : class, ICachedDataStoreWarpServiceAsync {
#if !NET
		public CachedDataStoreClientAsyncBase(string configName) : base(configName) { }
#endif
		public CachedDataStoreClientAsyncBase(System.ServiceModel.Channels.Binding binding, EndpointAddress remoteAddress) : base(binding, remoteAddress) { }
		bool selectDataRawFound;
		bool selectDataRawNotFound;
		DataCacheModificationResult ICacheToCacheCommunicationCore.ModifyData(DataCacheCookie cookie, ModificationStatement[] dmlStatements) {
			return ExecuteClient<DataCacheModificationResult>(delegate () { return Channel.ModifyDataCached(cookie, dmlStatements); }, HandleModifyDataException).HandleError();
		}
		DataCacheResult ICacheToCacheCommunicationCore.NotifyDirtyTables(DataCacheCookie cookie, params string[] dirtyTablesNames) {
			return ExecuteClient<DataCacheResult>(delegate () { return Channel.NotifyDirtyTables(cookie, dirtyTablesNames); }, HandleCacheToCacheCommunicationException).HandleError();
		}
		DataCacheResult ICacheToCacheCommunicationCore.ProcessCookie(DataCacheCookie cookie) {
			return ExecuteClient<DataCacheResult>(delegate () { return Channel.ProcessCookie(cookie); }, HandleCacheToCacheCommunicationException).HandleError();
		}
		DataCacheSelectDataResult ICacheToCacheCommunicationCore.SelectData(DataCacheCookie cookie, SelectStatement[] selects) {
			if(!selectDataRawNotFound) {
				try {
					DataCacheSelectDataResult selecteData = ExecuteClient<DataCacheWarpSelectDataResult>(delegate () { 
						return ((ICachedDataStoreWarpService)Channel).WarpSelectDataCached(cookie, selects); 
					}, HandleSelectDataException).HandleError().GetResult();
					selectDataRawFound = true;
					return selecteData;
				} catch(Exception) {
					if(selectDataRawFound) throw;
					selectDataRawNotFound = true;
				}
			}
			return ExecuteClient<DataCacheSelectDataResult>(delegate () { return Channel.SelectDataCached(cookie, selects); }, HandleSelectDataException).HandleError();
		}
		DataCacheUpdateSchemaResult ICacheToCacheCommunicationCore.UpdateSchema(DataCacheCookie cookie, DBTable[] tables, bool doNotCreateIfFirstTableNotExist) {
			return ExecuteClient<DataCacheUpdateSchemaResult>(delegate () { return Channel.UpdateSchemaCached(cookie, tables, doNotCreateIfFirstTableNotExist); }, HandleUpdateSchemaException).HandleError();
		}
		async Task<DataCacheModificationResult> ICacheToCacheCommunicationCoreAsync.ModifyDataAsync(CancellationToken cancellationToken, DataCacheCookie cookie, ModificationStatement[] dmlStatements) {
			var opResult = await ExecuteClientAsync<DataCacheModificationResult>(delegate () { return Channel.ModifyDataCachedAsync(cookie, dmlStatements); }, HandleModifyDataException).ConfigureAwait(false);
			return opResult.HandleError();
		}
		async Task<DataCacheResult> ICacheToCacheCommunicationCoreAsync.NotifyDirtyTablesAsync(CancellationToken cancellationToken, DataCacheCookie cookie, params string[] dirtyTablesNames) {
			var opResult = await ExecuteClientAsync<DataCacheResult>(delegate () { return Channel.NotifyDirtyTablesAsync(cookie, dirtyTablesNames); }, HandleCacheToCacheCommunicationException).ConfigureAwait(false);
			return opResult.HandleError();
		}
		async Task<DataCacheResult> ICacheToCacheCommunicationCoreAsync.ProcessCookieAsync(CancellationToken cancellationToken, DataCacheCookie cookie) {
			var opResult = await ExecuteClientAsync<DataCacheResult>(delegate () { return Channel.ProcessCookieAsync(cookie); }, HandleCacheToCacheCommunicationException).ConfigureAwait(false);
			return opResult.HandleError();
		}
		async Task<DataCacheSelectDataResult> ICacheToCacheCommunicationCoreAsync.SelectDataAsync(CancellationToken cancellationToken, DataCacheCookie cookie, SelectStatement[] selects) {
			if(!selectDataRawNotFound) {
				try {
					var warpSelectResult = await ExecuteClientAsync<DataCacheWarpSelectDataResult>(delegate () { 
						return ((ICachedDataStoreWarpServiceAsync)Channel).WarpSelectDataCachedAsync(cookie, selects); 
					}, HandleSelectDataException).ConfigureAwait(false);
					DataCacheSelectDataResult selecteData = warpSelectResult.HandleError().GetResult();
					selectDataRawFound = true;
					return selecteData;
				} catch(Exception) {
					if(selectDataRawFound) throw;
					selectDataRawNotFound = true;
				}
			}
			var opResult = await ExecuteClientAsync<DataCacheSelectDataResult>(delegate () { return Channel.SelectDataCachedAsync(cookie, selects); }, HandleSelectDataException).ConfigureAwait(false);
			return opResult.HandleError();
		}
		async Task<DataCacheUpdateSchemaResult> ICacheToCacheCommunicationCoreAsync.UpdateSchemaAsync(CancellationToken cancellationToken, DataCacheCookie cookie, DBTable[] tables, bool doNotCreateIfFirstTableNotExist) {
			var opResult = await ExecuteClientAsync<DataCacheUpdateSchemaResult>(delegate () { 
				return Channel.UpdateSchemaCachedAsync(cookie, tables, doNotCreateIfFirstTableNotExist); 
			}, HandleUpdateSchemaException).ConfigureAwait(false);
			return opResult.HandleError();
		}
		protected virtual bool HandleCacheToCacheCommunicationException(Exception ex) {
			return true;
		}
	}
}
namespace DevExpress.Xpo.Helpers {
	using System.Collections.Generic;
	using System.IO;
	using System.IO.Compression;
	using System.Runtime.Serialization.Formatters.Binary;
	public static class WcfUsedAsDumbPipeHelper {
		const string UnpackedSignature = "Xpo.SSRs.0.Un:";
		const string GzPackedSignature = "Xpo.SSRs.0.Gz:";
		public static byte[] Warp(SelectStatementResult[] resultSet) {
			if(GzPackedSignature.Length != UnpackedSignature.Length)
				throw new ArgumentException("GzPackedSignature.Length != UnpackedSignature.Length");
			if(resultSet == null) {
				return null;
			}
			byte[] unpackedBytesWithASig;
			using(MemoryStream ms = new MemoryStream()) {
				foreach(char ch in UnpackedSignature)
					ms.WriteByte((byte)ch);
				SelectResultsToStream(ms, resultSet);
				unpackedBytesWithASig = ms.ToArray();
			}
			if(CanTryPackWhole(unpackedBytesWithASig)) {
				using(MemoryStream packed = new MemoryStream()) {
					foreach(char ch in GzPackedSignature)
						packed.WriteByte((byte)ch);
					using(GZipStream ps = new GZipStream(packed, CompressionMode.Compress, true)) {
						ps.Write(unpackedBytesWithASig, UnpackedSignature.Length, unpackedBytesWithASig.Length - UnpackedSignature.Length);
						ps.Flush();
					}
					if(packed.Length < unpackedBytesWithASig.Length * 0.8)
						return packed.ToArray();
				}
			}
			return unpackedBytesWithASig;
		}
		public static SelectStatementResult[] Unwarp(byte[] warped) {
			if(GzPackedSignature.Length != UnpackedSignature.Length)
				throw new ArgumentException("GzPackedSignature.Length != UnpackedSignature.Length");
			if(warped == null) {
				return null;
			}
			using(MemoryStream ms = new MemoryStream(warped)) {
				System.Text.StringBuilder sigBuilder = new System.Text.StringBuilder();
				for(int i = 0; i < UnpackedSignature.Length; ++i) {
					sigBuilder.Append((char)ms.ReadByte());
				}
				string signature = sigBuilder.ToString();
				switch(signature) {
					case UnpackedSignature:
						return SelectResultsFromStream(ms);
					case GzPackedSignature:
						using(GZipStream ps = new GZipStream(ms, CompressionMode.Decompress, true)) {
							return SelectResultsFromStream(ps);
						}
					default:
						throw new InvalidOperationException("Unexpected Signature: " + signature);
				}
			}
		}
		static bool CanTryPackWhole(byte[] load) {
			if(load == null)
				throw new ArgumentNullException(nameof(load));
			if(load.Length < 4096)
				return false;
			if(load.Length < 1024 * 1024)
				return true;
			using(MemoryStream sample = new MemoryStream()) {
				using(GZipStream ps = new GZipStream(sample, CompressionMode.Compress, true)) {
					ps.Write(load, load.Length / 2 - 64 * 1024, 128 * 1024);
					ps.Flush();
				}
				const int Threshold = 128 * 1024 * 70 / 100;
				return sample.Length < Threshold;
			}
		}
		static void SelectResultsToStream(Stream stream, SelectStatementResult[] resultSet) {
			BinaryWriter wr = new BinaryWriter(stream);
			wr.Write((Int32)resultSet.Length);
			foreach(SelectStatementResult result in resultSet) {
				wr.Write((Int32)result.Rows.Length);
				foreach(SelectStatementResultRow row in result.Rows) {
					wr.Write((Int32)row.Values.Length);
					foreach(object value in row.Values) {
						WriteObject(wr, value);
					}
				}
			}
		}
		static SelectStatementResult[] SelectResultsFromStream(Stream stream) {
			BinaryReader rd = new BinaryReader(stream);
			int resultsCount = rd.ReadInt32();
			SelectStatementResult[] resultSet = new SelectStatementResult[resultsCount];
			for(int i = 0; i < resultsCount; ++i) {
				int rowsCount = rd.ReadInt32();
				SelectStatementResultRow[] rows = new SelectStatementResultRow[rowsCount];
				for(int r = 0; r < rowsCount; ++r) {
					int valuesCount = rd.ReadInt32();
					object[] values = new object[valuesCount];
					for(int v = 0; v < valuesCount; ++v) {
						values[v] = ReadObject(rd);
					}
					rows[r] = new SelectStatementResultRow(values);
				}
				resultSet[i] = new SelectStatementResult(rows);
			}
			return resultSet;
		}
		static void WriteObject(BinaryWriter wr, object value) {
			if(value == null) {
				wr.Write((byte)TypeCode.Empty);
			} else {
				Type t = value.GetType();
				sbyte code = (sbyte)Type.GetTypeCode(t);
				switch(code) {
					case (sbyte)TypeCode.Boolean:
						wr.Write(code);
						wr.Write((Boolean)value);
						break;
					case (sbyte)TypeCode.Char:
						wr.Write(code);
						wr.Write((Char)value);
						break;
					case (sbyte)TypeCode.SByte:
						wr.Write(code);
						wr.Write((SByte)value);
						break;
					case (sbyte)TypeCode.Byte:
						wr.Write(code);
						wr.Write((Byte)value);
						break;
					case (sbyte)TypeCode.Int16:
						wr.Write(code);
						wr.Write((Int16)value);
						break;
					case (sbyte)TypeCode.UInt16:
						wr.Write(code);
						wr.Write((UInt16)value);
						break;
					case (sbyte)TypeCode.Int32:
						wr.Write(code);
						wr.Write((Int32)value);
						break;
					case (sbyte)TypeCode.UInt32:
						wr.Write(code);
						wr.Write((UInt32)value);
						break;
					case (sbyte)TypeCode.Int64:
						wr.Write(code);
						wr.Write((Int64)value);
						break;
					case (sbyte)TypeCode.UInt64:
						wr.Write(code);
						wr.Write((UInt64)value);
						break;
					case (sbyte)TypeCode.Single:
						wr.Write(code);
						wr.Write((Single)value);
						break;
					case (sbyte)TypeCode.Double:
						wr.Write(code);
						wr.Write((Double)value);
						break;
					case (sbyte)TypeCode.Decimal:
						wr.Write(code);
						wr.Write((Decimal)value);
						break;
					case (sbyte)TypeCode.DateTime:
						wr.Write(code);
						wr.Write((Int64)((DateTime)value).ToBinary());
						break;
					case (sbyte)TypeCode.String:
						wr.Write(code);
						wr.Write((String)value);
						break;
					default:
						if(t == typeof(byte[])) {
							wr.Write((sbyte)-1);
							byte[] array = (byte[])value;
							wr.Write((Int32)array.Length);
							wr.Write(array);
						} else if(t == typeof(Guid)) {
							wr.Write((sbyte)-2);
							Guid guid = (Guid)value;
							wr.Write(guid.ToByteArray(), 0, 16);
						} else if(t == typeof(TimeSpan)) {
							wr.Write((sbyte)-3);
							TimeSpan ts = (TimeSpan)value;
							wr.Write((Int64)ts.Ticks);
						} else {
							wr.Write((sbyte)TypeCode.Object);
							Utils.SafeBinaryFormatter.Serialize(wr.BaseStream, value);
						}
						break;
				}
			}
		}
		static object ReadObject(BinaryReader rd) {
			SByte code = rd.ReadSByte();
			switch(code) {
				case (sbyte)TypeCode.Empty:
					return null;
				case (sbyte)TypeCode.Boolean:
					return rd.ReadBoolean();
				case (sbyte)TypeCode.Char:
					return rd.ReadChar();
				case (sbyte)TypeCode.SByte:
					return rd.ReadSByte();
				case (sbyte)TypeCode.Byte:
					return rd.ReadByte();
				case (sbyte)TypeCode.Int16:
					return rd.ReadInt16();
				case (sbyte)TypeCode.UInt16:
					return rd.ReadUInt16();
				case (sbyte)TypeCode.Int32:
					return rd.ReadInt32();
				case (sbyte)TypeCode.UInt32:
					return rd.ReadUInt32();
				case (sbyte)TypeCode.Int64:
					return rd.ReadInt64();
				case (sbyte)TypeCode.UInt64:
					return rd.ReadUInt64();
				case (sbyte)TypeCode.Single:
					return rd.ReadSingle();
				case (sbyte)TypeCode.Double:
					return rd.ReadDouble();
				case (sbyte)TypeCode.Decimal:
					return rd.ReadDecimal();
				case (sbyte)TypeCode.DateTime:
					return DateTime.FromBinary(rd.ReadInt64());
				case (sbyte)TypeCode.String:
					return rd.ReadString();
				case (sbyte)TypeCode.Object:
					return Utils.SafeBinaryFormatter.Deserialize(rd.BaseStream);
				case (sbyte)-1:
					return rd.ReadBytes(rd.ReadInt32());
				case (sbyte)-2:
					return new Guid(rd.ReadBytes(16));
				case (sbyte)-3:
					return TimeSpan.FromTicks(rd.ReadInt64());
				default:
					throw new InvalidOperationException("Unknown type code: " + code.ToString());
			}
		}
	}
}
