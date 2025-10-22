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
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using DevExpress.Data.Filtering;
using DevExpress.Data.Helpers;
using DevExpress.Data.Utils;
using DevExpress.Utils;
using DevExpress.Xpo.DB.Helpers;
namespace DevExpress.Xpo.DB {
	public class WebApiDataStoreClient : IDataStore, IDataStoreAsync, ICacheToCacheCommunicationCore, ICacheToCacheCommunicationCoreAsync {
		public const string XpoProviderTypeString = "WebApi";
		const string UriPartName = "uri";
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool IgnoreIllegalXmlCharacters = false;
		public static string GetConnectionString(string url) {
			return String.Format("{0}={1};{3}={2}", DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString, url, UriPartName);
		}
#pragma warning disable DX0006
		public static IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			ConnectionStringParser helper = new ConnectionStringParser(connectionString);
			string address = helper.GetPartByName(UriPartName);
			var uri = new Uri(address);
			var client = new HttpClient();
			client.BaseAddress = uri;
			objectsToDisposeOnDisconnect = new IDisposable[] { client };
			return new WebApiDataStoreClient(client, autoCreateOption);
		}
		public WebApiDataStoreClient(HttpClient httpClient, AutoCreateOption autoCreateOption) {
			MediaTypeWithQualityHeaderValue acceptHeader = new MediaTypeWithQualityHeaderValue("application/xml");
			httpClient.DefaultRequestHeaders.Accept.Add(acceptHeader);
			this.httpClient = httpClient;
			this.autoCreateOption = autoCreateOption;
		}
		static WebApiDataStoreClient() {
			DataStoreBase.RegisterDataStoreProvider(XpoProviderTypeString, new DataStoreCreationFromStringDelegate(CreateProviderFromString));
		}
		public static void Register() { }
		AutoCreateOption autoCreateOption;
		AutoCreateOption IDataStore.AutoCreateOption {
			get { return autoCreateOption; }
		}
		readonly HttpClient httpClient;
		public HttpClient HttpClient {
			get { return httpClient; }
		}
		IWebApiDataFormatter fFormatter;
		protected IWebApiDataFormatter Formatter {
			get { 
				if(fFormatter == null) {
					fFormatter = CreateDataFormatter();
				}
				return fFormatter;
			}
		}
		HttpClientHelper httpHelper;
		protected HttpClientHelper HttpHelper {
			get {
				if(httpHelper == null) {
					httpHelper = new HttpClientHelper(HttpClient, Formatter);
				}
				return httpHelper;
			}
		}
#pragma warning restore DX0006
		protected virtual IWebApiDataFormatter CreateDataFormatter() {
			return new WebApiXmlDataFormatter();
		} 
		UpdateSchemaResult IDataStore.UpdateSchema(bool doNotCreateIfFirstTableNotExist, params DBTable[] tables) {
			return HttpHelper.Post<DBTable[], OperationResult<UpdateSchemaResult>>(HttpClientHelper.BuildQueryString("updateschema", "donotcreateiffirsttablenotexist", doNotCreateIfFirstTableNotExist), tables).HandleError();
		}
		SelectedData IDataStore.SelectData(params SelectStatement[] selects) {
			return HttpHelper.Post<SelectStatement[], OperationResult<SelectedData>>("selectdata", selects).HandleError();
		}
		ModificationResult IDataStore.ModifyData(params ModificationStatement[] dmlStatements) {
			return HttpHelper.Post<ModificationStatement[], OperationResult<ModificationResult>>("modifydata", dmlStatements).HandleError();
		}
		async Task<SelectedData> IDataStoreAsync.SelectDataAsync(CancellationToken cancellationToken, params SelectStatement[] selects) {
			var result = await HttpHelper.PostAsync<SelectStatement[], OperationResult<SelectedData>>("selectdata", selects, cancellationToken).ConfigureAwait(false);
			return result.HandleError();
		}
		async Task<ModificationResult> IDataStoreAsync.ModifyDataAsync(CancellationToken cancellationToken, params ModificationStatement[] dmlStatements) {
			var result = await HttpHelper.PostAsync<ModificationStatement[], OperationResult<ModificationResult>>("modifydata", dmlStatements, cancellationToken).ConfigureAwait(false);
			return result.HandleError();
		}
		async Task<UpdateSchemaResult> IDataStoreAsync.UpdateSchemaAsync(CancellationToken cancellationToken, bool doNotCreateIfFirstTableNotExist, params DBTable[] tables) {
			var result = await HttpHelper.PostAsync<DBTable[], OperationResult<UpdateSchemaResult>>(HttpClientHelper.BuildQueryString("updateschema", "donotcreateiffirsttablenotexist", doNotCreateIfFirstTableNotExist), tables, cancellationToken).ConfigureAwait(false);
			return result.HandleError();
		}
		public DataCacheUpdateSchemaResult UpdateSchema(DataCacheCookie cookie, DBTable[] tables, bool doNotCreateIfFirstTableNotExist) {
			return HttpHelper.Post<WebApiDataContainer<DBTable[]>, OperationResult<DataCacheUpdateSchemaResult>>(HttpClientHelper.BuildQueryString("updateschemawithcookie", "donotcreateiffirsttablenotexist", doNotCreateIfFirstTableNotExist), new WebApiDataContainer<DBTable[]>(cookie, tables)).HandleError();
		}
		public DataCacheSelectDataResult SelectData(DataCacheCookie cookie, SelectStatement[] selects) {
			return HttpHelper.Post<WebApiDataContainer<SelectStatement[]>, OperationResult<DataCacheSelectDataResult>>("selectdatawithcookie", new WebApiDataContainer<SelectStatement[]>(cookie, selects)).HandleError();
		}
		public DataCacheModificationResult ModifyData(DataCacheCookie cookie, ModificationStatement[] dmlStatements) {
			return HttpHelper.Post<WebApiDataContainer<ModificationStatement[]>, OperationResult<DataCacheModificationResult>>("modifydatawithcookie", new WebApiDataContainer<ModificationStatement[]>(cookie, dmlStatements)).HandleError();
		}
		public DataCacheResult ProcessCookie(DataCacheCookie cookie) {
			return HttpHelper.Post<DataCacheCookie, OperationResult<DataCacheResult>>("processcookie", cookie).HandleError();
		}
		public DataCacheResult NotifyDirtyTables(DataCacheCookie cookie, params string[] dirtyTablesNames) {
			return HttpHelper.Post<WebApiDataContainer<string[]>, OperationResult<DataCacheResult>>("notifydirtytables", new WebApiDataContainer<string[]>(cookie, dirtyTablesNames)).HandleError();
		}
		public async Task<DataCacheUpdateSchemaResult> UpdateSchemaAsync(CancellationToken cancellationToken, DataCacheCookie cookie, DBTable[] tables, bool doNotCreateIfFirstTableNotExist) {
			var result = await HttpHelper.PostAsync<WebApiDataContainer<DBTable[]>, OperationResult<DataCacheUpdateSchemaResult>>(HttpClientHelper.BuildQueryString("updateschemawithcookie", "donotcreateiffirsttablenotexist", doNotCreateIfFirstTableNotExist), new WebApiDataContainer<DBTable[]>(cookie, tables), cancellationToken).ConfigureAwait(false);
			return result.HandleError();
		}
		public async Task<DataCacheSelectDataResult> SelectDataAsync(CancellationToken cancellationToken, DataCacheCookie cookie, SelectStatement[] selects) {
			var result = await HttpHelper.PostAsync<WebApiDataContainer<SelectStatement[]>, OperationResult<DataCacheSelectDataResult>>("selectdatawithcookie", new WebApiDataContainer<SelectStatement[]>(cookie, selects), cancellationToken).ConfigureAwait(false);
			return result.HandleError();
		}
		public async Task<DataCacheModificationResult> ModifyDataAsync(CancellationToken cancellationToken, DataCacheCookie cookie, ModificationStatement[] dmlStatements) {
			var result = await HttpHelper.PostAsync<WebApiDataContainer<ModificationStatement[]>, OperationResult<DataCacheModificationResult>>("modifydatawithcookie", new WebApiDataContainer<ModificationStatement[]>(cookie, dmlStatements), cancellationToken).ConfigureAwait(false);
			return result.HandleError();
		}
		public async Task<DataCacheResult> ProcessCookieAsync(CancellationToken cancellationToken, DataCacheCookie cookie) {
			var result = await HttpHelper.PostAsync<DataCacheCookie, OperationResult<DataCacheResult>>("processcookie", cookie, cancellationToken).ConfigureAwait(false);
			return result.HandleError();
		}
		public async Task<DataCacheResult> NotifyDirtyTablesAsync(CancellationToken cancellationToken, DataCacheCookie cookie, params string[] dirtyTablesNames) {
			var result = await HttpHelper.PostAsync<WebApiDataContainer<string[]>, OperationResult<DataCacheResult>>("notifydirtytables", new WebApiDataContainer<string[]>(cookie, dirtyTablesNames), cancellationToken).ConfigureAwait(false);
			return result.HandleError();
		}
	}
	public class WebApiDataStoreService : ServiceBase {
		readonly IDataStore Inner;
		public WebApiDataStoreService(IDataStore inner) {
			this.Inner = inner;
		}
		private ICacheToCacheCommunicationCore GetDataCacheRoot() {
			var innerDataStore = this.Inner as ICacheToCacheCommunicationCore;
			if(innerDataStore == null) {
				string msg = Res.GetString(CultureInfo.CurrentUICulture, Res.WebApi_ICacheToCacheCommunicationCore_NotImplemented, Inner.GetType());
				throw new NotSupportedException(msg);
			}
			return innerDataStore;
		}
		public OperationResult<ModificationResult> ModifyData(ModificationStatement[] dmlStatements) {
			return Execute(() => Inner.ModifyData(dmlStatements));
		}
		public Task<OperationResult<ModificationResult>> ModifyDataAsync(ModificationStatement[] dmlStatements, CancellationToken cancellationToken = default(CancellationToken)) {
			Task<ModificationResult> resultAwaiter = DataStoreAsyncFallbackHelper.ModifyDataAsyncWithSmartFallback(Inner, cancellationToken, dmlStatements);
			return ExecuteAsync(resultAwaiter);
		}
		public OperationResult<SelectedData> SelectData(SelectStatement[] selects) {
			return Execute(() => Inner.SelectData(selects));
		}
		public Task<OperationResult<SelectedData>> SelectDataAsync(SelectStatement[] selects, CancellationToken cancellationToken = default(CancellationToken)) {
			Task<SelectedData> resultAwaiter = DataStoreAsyncFallbackHelper.SelectDataAsyncWithSmartFallback(Inner, cancellationToken, selects);
			return ExecuteAsync(resultAwaiter);
		}
		public OperationResult<UpdateSchemaResult> UpdateSchema(bool doNotCreateIfFirstTableNotExist, DBTable[] tables) {
			return Execute(() => Inner.UpdateSchema(doNotCreateIfFirstTableNotExist, tables));
		}
		public Task<OperationResult<UpdateSchemaResult>> UpdateSchemaAsync(bool doNotCreateIfFirstTableNotExist, DBTable[] tables, CancellationToken cancellationToken = default(CancellationToken)) {
			try {
				Task<UpdateSchemaResult> resultAwaiter = DataStoreAsyncFallbackHelper.UpdateSchemaAsyncWithSmartFallback(Inner, cancellationToken, doNotCreateIfFirstTableNotExist, tables);
				return ExecuteAsync(resultAwaiter);
			} catch(Exception ex) {
				return Task.FromResult(WrapException<UpdateSchemaResult>(ex));
			}
		}
		public OperationResult<DataCacheModificationResult> ModifyDataWithCookie(WebApiDataContainer<ModificationStatement[]> data) {
			return Execute(() => GetDataCacheRoot().ModifyData(data.DataCacheCookie, data.DataCacheData));
		}
		public OperationResult<DataCacheSelectDataResult> SelectDataWithCookie(WebApiDataContainer<SelectStatement[]> data) {
			return Execute(() => GetDataCacheRoot().SelectData(data.DataCacheCookie, data.DataCacheData));
		}
		public OperationResult<DataCacheUpdateSchemaResult> UpdateSchemaWithCookie(bool doNotCreateIfFirstTableNotExist, WebApiDataContainer<DBTable[]> data) {
			return Execute(() => GetDataCacheRoot().UpdateSchema(data.DataCacheCookie, data.DataCacheData, doNotCreateIfFirstTableNotExist ));
		}
		public OperationResult<DataCacheResult> ProcessCookie(DataCacheCookie data) {
			return Execute(() => GetDataCacheRoot().ProcessCookie(data));
		}
		public OperationResult<DataCacheResult> NotifyDirtyTables(WebApiDataContainer<string[]> data) {
			return Execute(() => GetDataCacheRoot().NotifyDirtyTables(data.DataCacheCookie, data.DataCacheData));
		}
	}	
}
namespace DevExpress.Xpo.DB.Helpers {
	public class WebApiDataContainer<T> {
		public DataCacheCookie DataCacheCookie { get; set; }
		public T DataCacheData { get; set; }
		private WebApiDataContainer() {
		}
		public WebApiDataContainer(DataCacheCookie cookie, T data) {
			DataCacheCookie = cookie;
			DataCacheData = data;
		}
	}
	public class HttpClientHelper {
#pragma warning disable DX0006
		readonly HttpClient http;
		readonly IWebApiDataFormatter formatter;
		readonly bool staSafe;
		public HttpClientHelper(HttpClient http, IWebApiDataFormatter formatter) : this(http, formatter, false) { }
		public HttpClientHelper(HttpClient http, IWebApiDataFormatter formatter, bool staSafe) {
			this.http = http;
			this.formatter = formatter;
			this.staSafe = staSafe;
		}
		Encoding outputEncoding = Encoding.Unicode;
		public Encoding OutputEncoding {
			get { return outputEncoding; }
			set { outputEncoding = value; }
		}
		AsyncDownloader<HttpResult<TResult>>.LifeTime CreateLifetime<TResult>() {
			return AsyncDownloader<HttpResult<TResult>>.CreateLifeTime(http);
		}
#pragma warning restore DX0006
		public async Task<TResult> GetAsync<TResult>(string action, CancellationToken cancellationToken) {
			Encoding responseEncoding = Encoding.UTF8;
			var lifetime = CreateLifetime<TResult>();
			var options = new AsyncDownloader<HttpResult<TResult>>.SendMethodOptions(HttpMethod.Get, null,
				(response) => {
					response.EnsureSuccessStatusCode();
					responseEncoding = Encoding.GetEncoding(response.Content.Headers.ContentType.CharSet);
					return true;
				}, lifetime);
			var uri = new Uri(action, UriKind.RelativeOrAbsolute);
			var result = await AsyncDownloader<HttpResult<TResult>>.LoadAsync(uri,
				(ex, stream) => new HttpResult<TResult>(ex, stream, responseEncoding, formatter),
				cancellationToken, options).ConfigureAwait(false);
			return result.Value;
		}
		TResult ExecuteSynchronously<TResult>(Func<Task<TResult>> asyncAction) {
			if (staSafe) {
				return StaSafeHelper.Invoke(() => asyncAction().ConfigureAwait(false).GetAwaiter().GetResult());
			}
			else {
				return asyncAction().GetAwaiter().GetResult();
			}
		}
		void ExecuteSynchronously(Func<Task> asyncAction) {
			if (staSafe) {
				StaSafeHelper.Invoke(() => asyncAction().ConfigureAwait(false).GetAwaiter().GetResult());
			}
			else {
				asyncAction().GetAwaiter().GetResult();
			}
		}
		public TResult Get<TResult>(string action) {
			return ExecuteSynchronously(() => GetAsync<TResult>(action, CancellationToken.None));
		}
		public async Task InvokeAsync(string action, CancellationToken cancellationToken) {
			Encoding responseEncoding = Encoding.UTF8;
			var lifetime = CreateLifetime<string>();
			var options = new AsyncDownloader<HttpResult<string>>.PostMethodOptions(
				() => null,
				(response) => {
					response.EnsureSuccessStatusCode();
					return true;
				}, lifetime);
			var uri = new Uri(action, UriKind.RelativeOrAbsolute);
			await AsyncDownloader<HttpResult<string>>.LoadAsync(uri,
				(ex, stream) => { if (ex?.SourceException != null) ex.Throw(); return null; },
				cancellationToken, options).ConfigureAwait(false);
		}
		public void Invoke(string action) {
			ExecuteSynchronously(() => InvokeAsync(action, CancellationToken.None));
		}
		public async Task<TResult> InvokeAsync<TResult>(string action, CancellationToken cancellationToken) {
			Encoding responseEncoding = Encoding.UTF8;
			var lifetime = CreateLifetime<TResult>();
			var options = new AsyncDownloader<HttpResult<TResult>>.PostMethodOptions(
				() => null,
				(response) => {
					response.EnsureSuccessStatusCode();
					responseEncoding = Encoding.GetEncoding(response.Content.Headers.ContentType.CharSet);
					return true;
				}, lifetime);
			var uri = new Uri(action, UriKind.RelativeOrAbsolute);
			var result = await AsyncDownloader<HttpResult<TResult>>.LoadAsync(uri,
				(ex, stream) => new HttpResult<TResult>(ex, stream, responseEncoding, formatter),
				cancellationToken, options).ConfigureAwait(false);
			return result.Value;
		}
		public TResult Invoke<TResult>(string action) {
			return ExecuteSynchronously(() => InvokeAsync<TResult>(action, CancellationToken.None));
		}
		public async Task UploadAsync<TData>(string action, TData data, CancellationToken cancellationToken) {
			Encoding responseEncoding = Encoding.UTF8;
			var lifetime = CreateLifetime<string>();
			var options = new AsyncDownloader<HttpResult<string>>.PostMethodOptions(
				() => CreateContent(data),
				(response) => {
					response.EnsureSuccessStatusCode();
					responseEncoding = Encoding.GetEncoding(response.Content.Headers.ContentType.CharSet);
					return true;
				}, lifetime);
			var uri = new Uri(action, UriKind.RelativeOrAbsolute);
			await AsyncDownloader<HttpResult<string>>.LoadAsync(uri, (ex, stream) => null, cancellationToken, options).ConfigureAwait(false);
		}
		public void Upload<TData>(string action, TData data) {
			ExecuteSynchronously(() => UploadAsync(action, data, CancellationToken.None));
		}
		public async Task<TResult> PostAsync<TData, TResult>(string action, TData data, CancellationToken cancellationToken) {
			Encoding responseEncoding = Encoding.UTF8;
			var lifetime = CreateLifetime<TResult>();
			var options = new AsyncDownloader<HttpResult<TResult>>.PostMethodOptions(
				() => CreateContent(data),
				(response) => {
					response.EnsureSuccessStatusCode();
					responseEncoding = Encoding.GetEncoding(response.Content.Headers.ContentType.CharSet);
					return true;
				}, lifetime);
			var uri = new Uri(action, UriKind.RelativeOrAbsolute);
			var result = await AsyncDownloader<HttpResult<TResult>>.LoadAsync(uri,
				(ex, stream) => new HttpResult<TResult>(ex, stream, responseEncoding, formatter),
				cancellationToken, options).ConfigureAwait(false);
			return result.Value;
		}
		public TResult Post<TData, TResult>(string action, TData data) {
			return ExecuteSynchronously(() => PostAsync<TData, TResult>(action, data, CancellationToken.None));
		}
		HttpContent CreateContent<TData>(TData data) {
			HttpContent content = new StreamContent(formatter.Serialize(data, OutputEncoding));
			content.Headers.ContentType = new MediaTypeHeaderValue("application/xml") { CharSet = Encoding.Unicode.WebName };
			return content;
		}
		public static string BuildQueryString(string key, object value) {
			if(value == null || string.IsNullOrEmpty(value.ToString())) { return key; }
			IEnumerable values = value as IEnumerable;
			if(values == null || value is string) {
				return string.Concat(key, "=", Uri.EscapeDataString(value.ToString()));
			}
			return string.Join("&", values.Cast<object>().Select(x => BuildQueryString(key, x)).ToArray());
		}
		public static string BuildQueryString(IDictionary<string, object> parameters) {
			if(parameters == null || parameters.Count == 0) { return null; }
			return string.Join("&", parameters.Select(x => BuildQueryString(x.Key, x.Value)));
		}
		public static string BuildQueryString(string path, string key, object value) {
			return string.Concat(path, "?", BuildQueryString(key, value));
		}
		public static string BuildQueryString(string path, IDictionary<string, object> parameters) {
			return string.Concat(path, "?", BuildQueryString(parameters));
		}
		class HttpResult<T> {
			public T Value { get; set; }
			public readonly System.Runtime.ExceptionServices.ExceptionDispatchInfo ExceptionInfo;
			public HttpResult(System.Runtime.ExceptionServices.ExceptionDispatchInfo exceptionInfo, Stream stream, Encoding encoding, IWebApiDataFormatter formatter) {
				this.ExceptionInfo = exceptionInfo;
				if (!HasException && stream != null) {
					Value = formatter.Deserialize<T>(stream, encoding);
				} else {
					ExceptionInfo?.Throw();
				}
			}
			public bool HasException {
				get { return (ExceptionInfo != null) && (ExceptionInfo.SourceException != null); }
			}
		}
	}
	public interface IWebApiDataFormatter {
		Stream Serialize<T>(T value, Encoding encoding);
		T Deserialize<T>(Stream stream, Encoding encoding);
	}
	public abstract class WebApiXmlDataFormatterBase : IWebApiDataFormatter {
		const int DefaultBufferSize = 1024;
		protected abstract T Deserialize<T>(Stream stream, Encoding encoding);
		protected abstract Stream Serialize<T>(T value, Encoding encoding);
		T IWebApiDataFormatter.Deserialize<T>(Stream stream, Encoding encoding) {
			return Deserialize<T>(stream, encoding);
		}
		Stream IWebApiDataFormatter.Serialize<T>(T value, Encoding encoding) {
			return Serialize(value, encoding);
		}
		protected StreamReader CreateStreamReader(Stream stream, Encoding encoding) {
			return new StreamReader(stream, encoding, true, DefaultBufferSize, true);
		}
		protected XmlWriter CreateXmlWriter(Stream stream, Encoding encoding) {
			var settings = new XmlWriterSettings();
			settings.Encoding = encoding;
			settings.CloseOutput = false;
			settings.CheckCharacters = false;
			return XmlWriter.Create(stream, settings);
		}
	}
	public sealed class WebApiXmlDataFormatter : WebApiXmlDataFormatterBase {
		protected override T Deserialize<T>(Stream stream, Encoding encoding) {
			using(StreamReader reader = CreateStreamReader(stream, encoding)) {
				try {
					return SafeXml.Cached.Deserialize<T>(reader, settings => settings.CheckCharacters = false);
				}
				finally { stream.Seek(0, SeekOrigin.Begin); }
			}
		}
		protected override Stream Serialize<T>(T value, Encoding encoding) {
			Stream stream = new MemoryStream();
			using(XmlWriter writer = CreateXmlWriter(stream, encoding)) {
				try {
					SafeXml.Cached.Serialize<T>(writer, value);
					writer.Flush();
					return stream;
				}
				finally { stream.Seek(0, SeekOrigin.Begin); }
			}
		}
	}
	public sealed class WebApiDataContractXmlDataFormatter : WebApiXmlDataFormatterBase {
		static readonly ConcurrentDictionary<Type, DataContractSerializer> Serializers = new ConcurrentDictionary<Type, DataContractSerializer>();
		static readonly Type[] DefaultKnownTypes = new[] {
			typeof(InsertStatement),
			typeof(DeleteStatement),
			typeof(UpdateStatement),
			typeof(ParameterValue),
			typeof(QueryOperand),
			typeof(QuerySubQueryContainer),
			typeof(ContainsOperator),
			typeof(BetweenOperator),
			typeof(BinaryOperator),
			typeof(UnaryOperator),
			typeof(InOperator),
			typeof(GroupOperator),
			typeof(OperandValue),
			typeof(ConstantValue),
			typeof(OperandProperty),
			typeof(AggregateOperand),
			typeof(JoinOperand),
			typeof(FunctionOperator),
			typeof(NotOperator),
			typeof(NullOperator),
			typeof(QueryOperand)
		};
		public static readonly IWebApiDataFormatter Default = new WebApiDataContractXmlDataFormatter(DefaultKnownTypes);
		readonly Type[] knownTypes;
		public WebApiDataContractXmlDataFormatter(Type[] knownTypes) {
			this.knownTypes = knownTypes;
		}
		public static DataContractSerializer GetSerializer(Type objectType, Type[] knownTypes) {
			return Serializers.GetOrAdd(objectType, t => new DataContractSerializer(t, knownTypes));
		}
		protected override T Deserialize<T>(Stream stream, Encoding encoding) {
			using(StreamReader streamReader = CreateStreamReader(stream, encoding)) {
				var xmlReader = SafeXml.CreateReader(streamReader, settings => settings.CheckCharacters = false);
				using(var reader = XmlDictionaryReader.CreateDictionaryReader(xmlReader)) {
					try {
#pragma warning disable DX0011 // safe usage for known types list
						DataContractSerializer serializer = GetSerializer(typeof(T), knownTypes);
						return (T)serializer.ReadObject(reader);
#pragma warning restore DX0011
					}
					finally { stream.Seek(0, SeekOrigin.Begin); }
				}
			}
		}
		protected override Stream Serialize<T>(T value, Encoding encoding) {
			Stream stream = new MemoryStream();
			using(var writer = XmlDictionaryWriter.CreateDictionaryWriter(CreateXmlWriter(stream, encoding))) {
				try {
					DataContractSerializer serializer = GetSerializer(typeof(T), knownTypes);
					serializer.WriteObject(writer, value);
					writer.Flush(); 
					return stream;
				}
				finally { stream.Seek(0, SeekOrigin.Begin); }
			}
		}
	}
}
