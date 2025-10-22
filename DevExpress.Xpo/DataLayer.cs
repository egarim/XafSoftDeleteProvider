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
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.Metadata;
using System.Collections.Generic;
using DevExpress.Xpo.Exceptions;
using System.ComponentModel;
using System.Threading.Tasks;
using DevExpress.Xpo.DB.Helpers;
namespace DevExpress.Xpo.Helpers {
	public interface IDataLayerForTests : IDataLayer {
		void ClearDatabase();
	}
	public interface IDataLayerProvider : DevExpress.Xpo.Metadata.Helpers.IXPDictionaryProvider {
		IDataLayer DataLayer { get;}
	}
	public abstract class BaseDataLayer : IDataLayer, IDataLayerAsync, ICommandChannel, ICommandChannelAsync {
		protected readonly Dictionary<XPClassInfo, XPClassInfo> EnsuredTypes = new Dictionary<XPClassInfo, XPClassInfo>();
		readonly XPDictionary dictionary;
		readonly IDataStore provider;
		readonly ICommandChannel nestedCommandChannel;
		readonly ICommandChannelAsync nestedCommandChannelAsync;
		AutoCreateOption? autoCreateOption;
		protected BaseDataLayer(XPDictionary dictionary, IDataStore provider, Action<XPDictionary> dictionaryInit) {
			if(dictionary == null)
				dictionary = XpoDefault.GetDictionary();
			this.dictionary = dictionary;
			if(dictionaryInit != null)
				dictionaryInit(dictionary);
			this.provider = provider;
			this.nestedCommandChannel = provider as ICommandChannel;
			this.nestedCommandChannelAsync = provider as ICommandChannelAsync;
			BeforeClassInfoSubscribe();
			this.dictionary.ClassInfoChanged += new ClassInfoEventHandler(OnClassInfoChanged);
		}
		protected virtual void BeforeClassInfoSubscribe() { }
		public void Dispose() {
			this.Dispose(true);
		}
		~BaseDataLayer() {
			this.Dispose(false);
		}
		protected virtual void Dispose(bool disposing) {
			if(disposing) {
				Dictionary.ClassInfoChanged -= new ClassInfoEventHandler(OnClassInfoChanged);
			}
		}
		[Description("")]
		[Browsable(false)]
		public IDataStore ConnectionProvider { get { return provider; } }
		[Description("")]
		[Browsable(false)]
		public XPDictionary Dictionary { get { return dictionary; } }
		protected abstract void OnClassInfoChanged(object sender, ClassInfoEventArgs e);
		protected void RegisterEnsuredTypes(ICollection<XPClassInfo> justEnsuredTypes) {
			foreach(XPClassInfo type in justEnsuredTypes) {
				EnsuredTypes.Add(type, type);
			}
		}
		protected void RaiseSchemaInit(ICollection<XPClassInfo> justEnsuredTypes) {
			if(SchemaInit != null) {
				foreach(XPClassInfo type in justEnsuredTypes) {
					using(IDbCommand command = CreateCommand()) {
						SchemaInit(this, new SchemaInitEventArgs(type, command));
					}
				}
			}
		}
		public abstract UpdateSchemaResult UpdateSchema(bool doNotCreate, params XPClassInfo[] types);
		public abstract SelectedData SelectData(params SelectStatement[] selects);
		public abstract ModificationResult ModifyData(params ModificationStatement[] dmlStatements);
		public abstract Task<SelectedData> SelectDataAsync(CancellationToken cancellationToken, params SelectStatement[] selects);
		public abstract Task<ModificationResult> ModifyDataAsync(CancellationToken cancellationToken, params ModificationStatement[] dmlStatements);
		public abstract Task<UpdateSchemaResult> UpdateSchemaAsync(CancellationToken cancellationToken, bool doNotCreate, params XPClassInfo[] types);
		[Description("")]
		[Browsable(false)]
		public virtual AutoCreateOption AutoCreateOption {
			get {
				if(!autoCreateOption.HasValue)
					autoCreateOption = provider.AutoCreateOption;
				return autoCreateOption.Value;
			}
		}
		public event SchemaInitEventHandler SchemaInit;
		public abstract IDbConnection Connection { get; }
		public abstract IDbCommand CreateCommand();
		static int seq = 0;
		int seqNum = Interlocked.Increment(ref seq);
		public override string ToString() {
			return base.ToString() + '(' + seqNum.ToString() + ')';
		}
		IDataLayer IDataLayerProvider.DataLayer {
			get { return this; }
		}
		readonly Dictionary<object, object> staticData = new Dictionary<object, object>();
		protected void ClearStaticData() {
			staticData.Clear();
		}
		public void SetDataLayerWideData(object key, object data) {
			staticData[key] = data;
		}
		public object GetDataLayerWideData(object key) {
			object res;
			return staticData.TryGetValue(key, out res) ? res : null;
		}
		readonly static object loadedTypesKey = new object();
		public static void SetDataLayerWideObjectTypes(IDataLayer layer, Dictionary<XPClassInfo, XPObjectType> loadedTypes) {
			layer.SetDataLayerWideData(loadedTypesKey, loadedTypes);
		}
		public static Dictionary<XPClassInfo, XPObjectType> GetDataLayerWideObjectTypes(IDataLayer layer) {
			return (Dictionary<XPClassInfo, XPObjectType>)layer.GetDataLayerWideData(loadedTypesKey);
		}
		readonly static object staticTypesKey = new object();
		static Dictionary<XPClassInfo, XPClassInfo> GetStaticTypesDictionary(IDataLayer layer) {
			Dictionary<XPClassInfo, XPClassInfo> rv = (Dictionary<XPClassInfo, XPClassInfo>)layer.GetDataLayerWideData(staticTypesKey);
			if(rv == null) {
				rv = new Dictionary<XPClassInfo, XPClassInfo>();
				layer.SetDataLayerWideData(staticTypesKey, rv);
			}
			return rv;
		}
		public static void RegisterStaticTypes(IDataLayer layer, params XPClassInfo[] types) {
			Dictionary<XPClassInfo, XPClassInfo> staticTypes = GetStaticTypesDictionary(layer);
			foreach(XPClassInfo ci in types) {
				staticTypes[ci] = ci;
			}
		}
		public static bool IsStaticType(IDataLayer layer, XPClassInfo type) {
			return GetStaticTypesDictionary(layer).ContainsKey(type);
		}
		readonly static object staticCacheKey = new object();
		public static IObjectMap GetStaticCache(IDataLayer layer, XPClassInfo info) {
			Dictionary<XPClassInfo, IObjectMap> staticCache = (Dictionary<XPClassInfo, IObjectMap>)layer.GetDataLayerWideData(staticCacheKey);
			if(staticCache == null) {
				staticCache = new Dictionary<XPClassInfo, IObjectMap>();
				layer.SetDataLayerWideData(staticCacheKey, staticCache);
			}
			IObjectMap cache;
			if(!staticCache.TryGetValue(info, out cache)) {
				cache = new StrongObjectMap();
				staticCache.Add(info, cache);
			}
			return cache;
		}
		sealed class StrongObjectMap : Dictionary<object, object>, IObjectMap {
			void IObjectMap.Add(object theObject, object id) {
				if(!ContainsKey(id)) {
					base.Add(id, theObject);
				} else {
					throw new ObjectCacheException(id, theObject, this[id]);
				}
			}
			void IObjectMap.Remove(object id) {
				Remove(id);
			}
			public object Get(object id) {
				object res;
				return TryGetValue(id, out res) ? res : null;
			}
			public int CompactCache() {
				return 0;
			}
			public void ClearCache() {
			}
		}
		object ICommandChannel.Do(string command, object args) {
			return Do(command, args);
		}
		protected virtual object Do(string command, object args) {
			if(nestedCommandChannel == null) {
				if(provider == null) {
					throw new NotSupportedException(string.Format(CommandChannelHelper.Message_CommandIsNotSupported, command));
				} else {
					throw new NotSupportedException(string.Format(CommandChannelHelper.Message_CommandIsNotSupportedEx, command, provider.GetType().FullName));
				}
			}
			return nestedCommandChannel.Do(command, args);
		}
		Task<object> ICommandChannelAsync.DoAsync(string command, object args, CancellationToken cancellationToken) {
			return DoAsync(command, args, cancellationToken);
		}
		protected virtual Task<object> DoAsync(string command, object args, CancellationToken cancellationToken = default(CancellationToken)) {
			return DataStoreAsyncFallbackHelper.CommandChannelDoAsyncWithSmartFallback(nestedCommandChannel, command, args, cancellationToken);
		}
	}
}
namespace DevExpress.Xpo {
	using DevExpress.Data.Helpers;
	using DevExpress.Xpo.DB.Helpers;
	using DevExpress.Xpo.Helpers;
	using System.Threading.Tasks;
	public interface IDataLayer: IDisposable, IDataLayerProvider {
		UpdateSchemaResult UpdateSchema(bool doNotCreateIfFirstTableNotExist, params XPClassInfo[] types);
		SelectedData SelectData(params SelectStatement[] selects);
		ModificationResult ModifyData(params ModificationStatement[] dmlStatements);
		event SchemaInitEventHandler SchemaInit;
		IDbConnection Connection { get; }
		IDbCommand CreateCommand();
		AutoCreateOption AutoCreateOption { get; }
		void SetDataLayerWideData(object key, object data);
		object GetDataLayerWideData(object key);
	}
	public interface IDataLayerAsync: IDataLayer {
		Task<UpdateSchemaResult> UpdateSchemaAsync(CancellationToken cancellationToken, bool doNotCreateIfFirstTableNotExist, params XPClassInfo[] types);
		Task<SelectedData> SelectDataAsync(CancellationToken cancellationToken, params SelectStatement[] selects);
		Task<ModificationResult> ModifyDataAsync(CancellationToken cancellationToken, params ModificationStatement[] dmlStatements);
	}
	public class SimpleDataLayer: BaseDataLayer, IDataLayerForTests, IDataLayerAsync {
		public SimpleDataLayer(IDataStore provider) : this(null, provider) { }
		public SimpleDataLayer(XPDictionary dictionary, IDataStore provider) : base(dictionary, provider, null) { }
		protected override void OnClassInfoChanged(object sender, ClassInfoEventArgs e) {
			ReentrancyAndThreadSafetyChecked(() => {
				EnsuredTypes.Remove(e.ClassInfo);
				return (object)null;
			});
		}
		public override UpdateSchemaResult UpdateSchema(bool doNotCreate, params XPClassInfo[] types) {
			ICollection<XPClassInfo> typesNeedEnsure = UpdateSchemaBegin(types);
			if(typesNeedEnsure == null) {
				return UpdateSchemaResult.SchemaExists;
			}
			DBTable[] tables = XPDictionary.CollectTables(typesNeedEnsure);
			var rv = ReentrancyAndThreadSafetyChecked(() => {
				if(ConnectionProvider.UpdateSchema(doNotCreate, tables) == UpdateSchemaResult.FirstTableNotExists)
					return UpdateSchemaResult.FirstTableNotExists;
				RegisterEnsuredTypes(typesNeedEnsure);
				return UpdateSchemaResult.SchemaExists;
			});
			if(rv == UpdateSchemaResult.SchemaExists)
				RaiseSchemaInit(typesNeedEnsure);
			return rv;
		}
		public override async Task<UpdateSchemaResult> UpdateSchemaAsync(CancellationToken cancellationToken, bool doNotCreate, params XPClassInfo[] types) {
			ICollection<XPClassInfo> typesNeedEnsure = UpdateSchemaBegin(types);
			if(typesNeedEnsure == null) {
				return UpdateSchemaResult.SchemaExists;
			}
			DBTable[] tables = XPDictionary.CollectTables(typesNeedEnsure);
			var rv = await ReentrancyAndThreadSafetyCheckedAsync(async () => {
				if(await DataStoreAsyncFallbackHelper.UpdateSchemaAsyncWithSmartFallback(ConnectionProvider, cancellationToken, doNotCreate, tables) == UpdateSchemaResult.FirstTableNotExists) {
					return UpdateSchemaResult.FirstTableNotExists;
				}
				RegisterEnsuredTypes(typesNeedEnsure);
				return UpdateSchemaResult.SchemaExists;
			});
			if(rv == UpdateSchemaResult.SchemaExists)
				RaiseSchemaInit(typesNeedEnsure);
			return rv;
		}
		ICollection<XPClassInfo> UpdateSchemaBegin(XPClassInfo[] types) {
			if(AutoCreateOption == AutoCreateOption.SchemaAlreadyExists) {
				foreach(XPClassInfo t in types)
					t.CheckAbstractReference();
				return null;
			}
			ICollection<XPClassInfo> typesNeedEnsure = Dictionary.ExpandTypesToEnsure(types, EnsuredTypes);
			if(typesNeedEnsure.Count == 0)
				return null;
			return typesNeedEnsure;
		}
		public override SelectedData SelectData(params SelectStatement[] selects) {
			return ReentrancyAndThreadSafetyChecked(() => {
				return ConnectionProvider.SelectData(selects);
			}
			);
		}
		public override Task<SelectedData> SelectDataAsync(CancellationToken cancellationToken, params SelectStatement[] selects) {
			return ReentrancyAndThreadSafetyCheckedAsync(() => {
				return DataStoreAsyncFallbackHelper.SelectDataAsyncWithSmartFallback(ConnectionProvider, cancellationToken, selects);
			});
		}
		public override ModificationResult ModifyData(params ModificationStatement[] dmlStatements) {
			return ReentrancyAndThreadSafetyChecked(() => {
				return ConnectionProvider.ModifyData(dmlStatements);
			});
		}
		public override Task<ModificationResult> ModifyDataAsync(CancellationToken cancellationToken, params ModificationStatement[] dmlStatements) {
			return ReentrancyAndThreadSafetyCheckedAsync(() => {
				return DataStoreAsyncFallbackHelper.ModifyDataAsyncWithSmartFallback(ConnectionProvider, cancellationToken, dmlStatements);
			});
		}
		public void ClearDatabase() {
			ReentrancyAndThreadSafetyChecked(() => {
				EnsuredTypes.Clear();
				ClearStaticData();
				((IDataStoreForTests)ConnectionProvider).ClearDatabase();
				return (object)null;
			});
		}
		[Description("A IDbConnection object that specifies the connection to the data store if it allows commands to be created.")]
		[Browsable(false)]
		public override IDbConnection Connection {
			get {
				ISqlDataStore connSource = ConnectionProvider as ISqlDataStore;
				if(connSource != null)
					try {
						return connSource.Connection;
					} catch { }
				return null;
			}
		}
		public override IDbCommand CreateCommand() {
			return ReentrancyAndThreadSafetyChecked(() => {
				ISqlDataStore commandSource = ConnectionProvider as ISqlDataStore;
				if(commandSource != null)
					try {
						return commandSource.CreateCommand();
					} catch { }
				return null;
			});
		}
		int roadBlock;
		const string ReentrancyOrCrossThreadFailureMessage = "Reentrancy or cross thread operation detected. Use ThreadSafeDataLayer or find the cause of the cross thread access to the SimpleDataLayer instance from your code as described in https://www.devexpress.com/kb=T419520. To suppress this exception, set DevExpress.Xpo.SimpleDataLayer.SuppressReentrancyAndThreadSafetyCheck = true";
		static bool _SuppressReentrancyAndThreadSafetyCheck;
		[Obsolete("Reentrancy and thread safety check suppressed")]
		public static bool SuppressReentrancyAndThreadSafetyCheck {
			get { return _SuppressReentrancyAndThreadSafetyCheck; }
			set {
				_SuppressReentrancyAndThreadSafetyCheck = value;
			}
		}
		T ReentrancyAndThreadSafetyChecked<T>(Func<T> action) {
			if(_SuppressReentrancyAndThreadSafetyCheck)
				return action();
			if(Interlocked.Increment(ref roadBlock) != 1)
				throw new InvalidOperationException(ReentrancyOrCrossThreadFailureMessage);
			bool throwExceptionOnLeave = true;
			try {
				return action();
			} catch {
				throwExceptionOnLeave = false;
				throw;
			} finally {
				if(Interlocked.Decrement(ref roadBlock) != 0 && throwExceptionOnLeave)
					throw new InvalidOperationException(ReentrancyOrCrossThreadFailureMessage);
			}
		}
		async Task<T> ReentrancyAndThreadSafetyCheckedAsync<T>(Func<Task<T>> action) {
			if(_SuppressReentrancyAndThreadSafetyCheck)
				return await action().ConfigureAwait(false);
			if(Interlocked.Increment(ref roadBlock) != 1)
				throw new InvalidOperationException(ReentrancyOrCrossThreadFailureMessage);
			bool throwExceptionOnLeave = true;
			try {
				return await action().ConfigureAwait(false);
			} catch {
				throwExceptionOnLeave = false;
				throw;
			} finally {
				if(Interlocked.Decrement(ref roadBlock) != 0 && throwExceptionOnLeave)
					throw new InvalidOperationException(ReentrancyOrCrossThreadFailureMessage);
			}
		}
	}
	public class ThreadSafeDataLayer: BaseDataLayer, ICommandChannel, IDataLayerAsync, ICommandChannelAsync {
		readonly AsyncReaderWriterLock ensuredTypesRwl = new AsyncReaderWriterLock();
		public ThreadSafeDataLayer(XPDictionary dictionary, IDataStore provider, Action<XPDictionary> dictionaryInit)
			: base(dictionary, provider, dictionaryInit) {
		}
		public ThreadSafeDataLayer(XPDictionary dictionary, IDataStore provider)
			: this(dictionary, provider, (Action<XPDictionary>)null) {
		}
		public ThreadSafeDataLayer(XPDictionary dictionary, IDataStore provider, params Assembly[] assemblies)
			: this(dictionary, provider, d => InitializeDictionary(d, assemblies)) { }
		public ThreadSafeDataLayer(XPDictionary dictionary, IDataStore provider, IEnumerable<Assembly> assemblies)
			: this(dictionary, provider, d => InitializeDictionary(d, assemblies)) { }
		public ThreadSafeDataLayer(XPDictionary dictionary, IDataStore provider, params Type[] types)
			: this(dictionary, provider, d => InitializeDictionary(d, types)) { }
		public ThreadSafeDataLayer(XPDictionary dictionary, IDataStore provider, IEnumerable<Type> types)
			: this(dictionary, provider, d => InitializeDictionary(d, types)) { }
		public ThreadSafeDataLayer(IDataStore provider, params Assembly[] assemblies)
			: this(null, provider, d => InitializeDictionary(d, assemblies)) { }
		public ThreadSafeDataLayer(IDataStore provider, IEnumerable<Assembly> assemblies)
			: this(null, provider, d => InitializeDictionary(d, assemblies)) { }
		public ThreadSafeDataLayer(IDataStore provider, params Type[] types)
			: this(null, provider, d => InitializeDictionary(d, types)) { }
		public ThreadSafeDataLayer(IDataStore provider, IEnumerable<Type> types)
			: this(null, provider, d => InitializeDictionary(d, types)) { }
		public ThreadSafeDataLayer(IDataStore provider)
			: this(null, provider, d => InitializeDictionary(d)) { }
		protected override void OnClassInfoChanged(object sender, ClassInfoEventArgs e) {
			throw new InvalidOperationException(Res.GetString(Res.ThreadSafeDataLayer_DictionaryModified, e.ClassInfo.FullName));
		}
		public override UpdateSchemaResult UpdateSchema(bool doNotCreate, params XPClassInfo[] types) {
			if(AutoCreateOption == AutoCreateOption.SchemaAlreadyExists) {
				foreach(XPClassInfo t in types)
					t.CheckAbstractReference();
				return UpdateSchemaResult.SchemaExists;
			}
			using(var readLock = ensuredTypesRwl.EnterUpgradeableReadLock()) {
				int ensuredTypesCount = EnsuredTypes.Count;
				ICollection<XPClassInfo> typesNeedEnsure = Dictionary.ExpandTypesToEnsure(types, EnsuredTypes);
				if(typesNeedEnsure.Count == 0)
					return UpdateSchemaResult.SchemaExists;
				using(var writeLock = ensuredTypesRwl.EnterWriteLock()) {
					if(EnsuredTypes.Count != ensuredTypesCount) {
						typesNeedEnsure = Dictionary.ExpandTypesToEnsure(types, EnsuredTypes);
						if(typesNeedEnsure.Count == 0)
							return UpdateSchemaResult.SchemaExists;
					}
					DBTable[] tables = XPDictionary.CollectTables(typesNeedEnsure);
					if(ConnectionProvider.UpdateSchema(doNotCreate, tables) == UpdateSchemaResult.FirstTableNotExists)
						return UpdateSchemaResult.FirstTableNotExists;
					RegisterEnsuredTypes(typesNeedEnsure);
					RaiseSchemaInit(typesNeedEnsure);
					return UpdateSchemaResult.SchemaExists;
				}
			}
		}
		public override async Task<UpdateSchemaResult> UpdateSchemaAsync(CancellationToken cancellationToken, bool doNotCreate, params XPClassInfo[] types) {
			if(AutoCreateOption == AutoCreateOption.SchemaAlreadyExists) {
				foreach(XPClassInfo t in types)
					t.CheckAbstractReference();
				return UpdateSchemaResult.SchemaExists;
			}
			var asyncOperationId = AsyncOperationIdentifier.New();
			using(var readLock = await ensuredTypesRwl.EnterUpgradeableReadLockAsync(asyncOperationId, cancellationToken)) {
				int ensuredTypesCount = EnsuredTypes.Count;
				ICollection<XPClassInfo> typesNeedEnsure = Dictionary.ExpandTypesToEnsure(types, EnsuredTypes);
				if(typesNeedEnsure.Count == 0)
					return UpdateSchemaResult.SchemaExists;
				using(var writeLock = await ensuredTypesRwl.EnterWriteLockAsync(asyncOperationId, cancellationToken)) {
					if(EnsuredTypes.Count != ensuredTypesCount) {
						typesNeedEnsure = Dictionary.ExpandTypesToEnsure(types, EnsuredTypes);
						if(typesNeedEnsure.Count == 0)
							return UpdateSchemaResult.SchemaExists;
					}
					DBTable[] tables = XPDictionary.CollectTables(typesNeedEnsure);
					if(await DataStoreAsyncFallbackHelper.UpdateSchemaAsyncWithSmartFallback(ConnectionProvider, cancellationToken, doNotCreate, tables) == UpdateSchemaResult.FirstTableNotExists)
						return UpdateSchemaResult.FirstTableNotExists;
					RegisterEnsuredTypes(typesNeedEnsure);
					RaiseSchemaInit(typesNeedEnsure);
					return UpdateSchemaResult.SchemaExists;
				}
			}
		}
		public override SelectedData SelectData(params SelectStatement[] selects) {
			using(var readLock = ensuredTypesRwl.EnterReadLock()) {
				return ConnectionProvider.SelectData(selects);
			}
		}
		public override async Task<SelectedData> SelectDataAsync(CancellationToken cancellationToken, params SelectStatement[] selects) {
			using(var readLock = await ensuredTypesRwl.EnterReadLockAsync(AsyncOperationIdentifier.New(), cancellationToken).ConfigureAwait(false)) {
				return await DataStoreAsyncFallbackHelper.SelectDataAsyncWithSmartFallback(ConnectionProvider, cancellationToken, selects).ConfigureAwait(false);
			}
		}
		public override ModificationResult ModifyData(params ModificationStatement[] dmlStatements) {
			using(var readLock = ensuredTypesRwl.EnterReadLock()) {
				return ConnectionProvider.ModifyData(dmlStatements);
			}
		}
		public override async Task<ModificationResult> ModifyDataAsync(CancellationToken cancellationToken, params ModificationStatement[] dmlStatements) {
			using(var readLock = await ensuredTypesRwl.EnterReadLockAsync(AsyncOperationIdentifier.New(), cancellationToken).ConfigureAwait(false)) {
				return await DataStoreAsyncFallbackHelper.ModifyDataAsyncWithSmartFallback(ConnectionProvider, cancellationToken, dmlStatements).ConfigureAwait(false);
			}
		}
		[Description("This property always returns  null (Nothing in Visual Basic).")]
		[Browsable(false)]
		public override IDbConnection Connection { get { return null; } }
		public override IDbCommand CreateCommand() { return null; }
		protected override void BeforeClassInfoSubscribe() {
			base.BeforeClassInfoSubscribe();
			using(Session touchAllTypesSession = new StrongSession(SimpleObjectLayer.FromDataLayer(this))) {
				touchAllTypesSession.TypesManager.EnsureIsTypedObjectValid();
				Dictionary<XPClassInfo, XPClassInfo> touchedHolder = new Dictionary<XPClassInfo, XPClassInfo>();
				foreach(XPClassInfo ci in new ArrayList(Dictionary.Classes))
					ci.TouchRecursive(touchedHolder);
			}
		}
		protected override object Do(string command, object args) {
			switch(command) {
				case CommandChannelHelper.Command_ExplicitBeginTransaction:
				case CommandChannelHelper.Command_ExplicitCommitTransaction:
				case CommandChannelHelper.Command_ExplicitRollbackTransaction:
					throw new NotSupportedException(string.Format(CommandChannelHelper.Message_CommandIsNotSupportedEx, command, "ThreadSafeDataLayer"));
				default:
					return base.Do(command, args);
			}
		}
		protected override Task<object> DoAsync(string command, object args, CancellationToken cancellationToken = default(CancellationToken)) {
			switch(command) {
				case CommandChannelHelper.Command_ExplicitBeginTransaction:
				case CommandChannelHelper.Command_ExplicitCommitTransaction:
				case CommandChannelHelper.Command_ExplicitRollbackTransaction:
					throw new NotSupportedException(string.Format(CommandChannelHelper.Message_CommandIsNotSupportedEx, command, "ThreadSafeDataLayer"));
				default:
					return base.DoAsync(command, args, cancellationToken);
			}
		}
		static void InitializeDictionary(XPDictionary dictionary, ICollection<XPClassInfo> types) {
			if(dictionary == null)
				throw new ArgumentNullException(nameof(dictionary));
			var allTypes = dictionary.ExpandTypesToEnsure(types);
			XPDictionary.CollectTables(allTypes);
		}
		public static void InitializeDictionary(XPDictionary dictionary, params Type[] types) {
			if(dictionary == null)
				throw new ArgumentNullException(nameof(dictionary));
			InitializeDictionary(dictionary, dictionary.CollectClassInfos(types));
		}
		public static void InitializeDictionary(XPDictionary dictionary, IEnumerable<Type> types) {
			if(dictionary == null)
				throw new ArgumentNullException(nameof(dictionary));
			InitializeDictionary(dictionary, dictionary.CollectClassInfos(types));
		}
		public static void InitializeDictionary(XPDictionary dictionary, params Assembly[] assemblies) {
			if(dictionary == null)
				throw new ArgumentNullException(nameof(dictionary));
			InitializeDictionary(dictionary, dictionary.CollectClassInfos(assemblies));
		}
		public static void InitializeDictionary(XPDictionary dictionary, IEnumerable<Assembly> assemblies) {
			if(dictionary == null)
				throw new ArgumentNullException(nameof(dictionary));
			InitializeDictionary(dictionary, dictionary.CollectClassInfos(assemblies));
		}
		public static void InitializeDictionary(XPDictionary dictionary) {
			if(dictionary == null)
				throw new ArgumentNullException(nameof(dictionary));
			InitializeDictionary(dictionary, dictionary.CollectClassInfos(Session.GetNonXpoAssemblies()));
		}
		public static void PrepareDataStore(IDataStore dataStore, XPDictionary dictionary, params XPClassInfo[] types) {
			if(dataStore == null)
				throw new ArgumentNullException(nameof(dataStore));
			using(var tmpDL = new SimpleDataLayer(dictionary, dataStore)) {
				using(var session = new Session(tmpDL) { IdentityMapBehavior = IdentityMapBehavior.Strong }) {
					session.CreateObjectTypeRecords(false, types);
				}
			}
		}
		public static void PrepareDataStore(IDataStore dataStore, XPDictionary dictionary, IEnumerable<XPClassInfo> types) {
			if(types == null)
				throw new ArgumentNullException(nameof(types));
			PrepareDataStore(dataStore, dictionary, types.ToArray());
		}
		public static void PrepareDataStore(IDataStore dataStore, XPDictionary dictionary, params Type[] types) {
			if(dictionary == null)
				dictionary = XpoDefault.GetDictionary();
			PrepareDataStore(dataStore, dictionary, dictionary.CollectClassInfos(types));
		}
		public static void PrepareDataStore(IDataStore dataStore, XPDictionary dictionary, IEnumerable<Type> types) {
			if(dictionary == null)
				dictionary = XpoDefault.GetDictionary();
			PrepareDataStore(dataStore, dictionary, dictionary.CollectClassInfos(types));
		}
		public static void PrepareDataStore(IDataStore dataStore, XPDictionary dictionary, params Assembly[] assemblies) {
			if(dictionary == null)
				dictionary = XpoDefault.GetDictionary();
			PrepareDataStore(dataStore, dictionary, dictionary.CollectClassInfos(assemblies));
		}
		public static void PrepareDataStore(IDataStore dataStore, XPDictionary dictionary, IEnumerable<Assembly> assemblies) {
			if(dictionary == null)
				dictionary = XpoDefault.GetDictionary();
			PrepareDataStore(dataStore, dictionary, dictionary.CollectClassInfos(assemblies));
		}
		public static void PrepareDataStore(IDataStore dataStore, XPDictionary dictionary) {
			if(dictionary == null)
				dictionary = XpoDefault.GetDictionary();
			PrepareDataStore(dataStore, dictionary, dictionary.CollectClassInfos(Session.GetNonXpoAssemblies()));
		}
		public static void PrepareDataStore(IDataStore dataStore, params Type[] types) {
			PrepareDataStore(dataStore, null, types);
		}
		public static void PrepareDataStore(IDataStore dataStore, IEnumerable<Type> types) {
			PrepareDataStore(dataStore, null, types);
		}
		public static void PrepareDataStore(IDataStore dataStore, params Assembly[] assemblies) {
			PrepareDataStore(dataStore, null, assemblies);
		}
		public static void PrepareDataStore(IDataStore dataStore, IEnumerable<Assembly> assemblies) {
			PrepareDataStore(dataStore, null, assemblies);
		}
		public static void PrepareDataStore(IDataStore dataStore) {
			PrepareDataStore(dataStore, (XPDictionary)null);
		}
	}
}
