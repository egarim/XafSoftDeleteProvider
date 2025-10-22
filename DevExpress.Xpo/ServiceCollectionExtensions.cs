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

#if NET
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using DevExpress.Utils;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.Helpers;
using DevExpress.Xpo.Metadata;
namespace Microsoft.Extensions.DependencyInjection {
	[CLSCompliant(false)]
	public static class XpoServiceCollectionExtensions {
		readonly static ConcurrentDictionary<Guid, IDataLayer> dataLayerSingletonCache = new ConcurrentDictionary<Guid, IDataLayer>();
		readonly static ConcurrentDictionary<Guid, IObjectLayer> objectLayerSingletonCache = new ConcurrentDictionary<Guid, IObjectLayer>();
		public static IServiceCollection AddXpoDefaultDataLayer(this IServiceCollection serviceCollection, ServiceLifetime lifetime, Action<DataLayerOptionsBuilder> dataLayerOptionsBuildAction) {
			Func<IServiceProvider, IDataLayer> constructor = (sp) => {
				DataLayerOptionsBuilder optionsBuilder = new DataLayerOptionsBuilder();
				dataLayerOptionsBuildAction(optionsBuilder);
				var dataLayerFactory = DataLayerFactory.GetDataLayerFactory(optionsBuilder.DataLayerOptions);
				return dataLayerFactory();
			};
			switch(lifetime) {
				case ServiceLifetime.Scoped:
					serviceCollection.AddScoped<IDataLayer>(constructor);
					break;
				case ServiceLifetime.Transient:
					serviceCollection.AddTransient<IDataLayer>(constructor);
					break;
				case ServiceLifetime.Singleton:
					serviceCollection.AddSingleton<IDataLayer>(constructor);
					break;
			}
			return serviceCollection;
		}
		public static IServiceCollection AddXpoDefaultDataLayer(this IServiceCollection serviceCollection, ServiceLifetime lifetime, Func<IServiceProvider, IDataLayer> dataLayerFactory) {
			Func<IServiceProvider, IDataLayer> constructor = (sp) => {
				return dataLayerFactory(sp);
			};
			switch(lifetime) {
				case ServiceLifetime.Scoped:
					serviceCollection.AddScoped<IDataLayer>(constructor);
					break;
				case ServiceLifetime.Transient:
					serviceCollection.AddTransient<IDataLayer>(constructor);
					break;
				case ServiceLifetime.Singleton:
					serviceCollection.AddSingleton<IDataLayer>(constructor);
					break;
			}
			return serviceCollection;
		}
		public static IServiceCollection AddXpoDefaultObjectLayer(this IServiceCollection serviceCollection, ServiceLifetime lifetime, Func<IServiceProvider, IObjectLayer> objectLayerFactory) {
			Func<IServiceProvider, IObjectLayer> constructor = objectLayerFactory;
			switch(lifetime) {
				case ServiceLifetime.Scoped:
					serviceCollection.AddScoped<IObjectLayer>(constructor);
					break;
				case ServiceLifetime.Transient:
					serviceCollection.AddTransient<IObjectLayer>(constructor);
					break;
				case ServiceLifetime.Singleton:
					serviceCollection.AddSingleton<IObjectLayer>(constructor);
					break;
			}
			return serviceCollection;
		}
		public static IServiceCollection AddXpoDefaultUnitOfWork(this IServiceCollection serviceCollection) {
			return AddXpoCustomSession<UnitOfWork>(serviceCollection);
		}
		public static IServiceCollection AddXpoDefaultUnitOfWork(this IServiceCollection serviceCollection, bool useDataLayerAsSingleton, Action<DataLayerOptionsBuilder> dataLayerOptionsBuildAction) {
			return AddXpoCustomSession<UnitOfWork>(serviceCollection, useDataLayerAsSingleton, dataLayerOptionsBuildAction);
		}
		public static IServiceCollection AddXpoDefaultUnitOfWork(this IServiceCollection serviceCollection, bool useDataLayerAsSingleton, Func<IServiceProvider, IDataLayer> dataLayerFactory) {
			return AddXpoCustomSession<UnitOfWork>(serviceCollection, useDataLayerAsSingleton, dataLayerFactory);
		}
		public static IServiceCollection AddXpoDefaultUnitOfWork(this IServiceCollection serviceCollection, bool useObjectLayerAsSingleton, Func<IServiceProvider, IObjectLayer> objectLayerFactory) {
			return AddXpoCustomSession<UnitOfWork>(serviceCollection, useObjectLayerAsSingleton, objectLayerFactory);
		}
		public static IServiceCollection AddXpoDefaultSession(this IServiceCollection serviceCollection) {
			return AddXpoCustomSession<Session>(serviceCollection);
		}
		public static IServiceCollection AddXpoDefaultSession(this IServiceCollection serviceCollection, bool useDataLayerAsSingleton, Action<DataLayerOptionsBuilder> dataLayerOptionsBuildAction) {
			return AddXpoCustomSession<Session>(serviceCollection, useDataLayerAsSingleton, dataLayerOptionsBuildAction);
		}
		public static IServiceCollection AddXpoDefaultSession(this IServiceCollection serviceCollection, bool useDataLayerAsSingleton, Func<IServiceProvider, IDataLayer> dataLayerFactory) {
			return AddXpoCustomSession<Session>(serviceCollection, useDataLayerAsSingleton, dataLayerFactory);
		}
		public static IServiceCollection AddXpoDefaultSession(this IServiceCollection serviceCollection, bool useObjectLayerAsSingleton, Func<IServiceProvider, IObjectLayer> objectLayerFactory) {
			return AddXpoCustomSession<Session>(serviceCollection, useObjectLayerAsSingleton, objectLayerFactory);
		}
		public static IServiceCollection AddXpoCustomSession<TSession>(this IServiceCollection serviceCollection)
			where TSession : Session, new() {
			Func<IServiceProvider, TSession> constructor = (sp) => {
				IObjectLayer objectLayer = sp.GetService<IObjectLayer>();
				if(objectLayer != null) {
					return (TSession)SessionFactory.CreateSessionInstance(sp, typeof(TSession), objectLayer);
				} else {
					IDataLayer dataLayer = sp.GetService<IDataLayer>();
					if(dataLayer != null) {
						return (TSession)SessionFactory.CreateSessionInstance(sp, typeof(TSession), dataLayer);
					} else {
						return (TSession)SessionFactory.CreateSessionInstance<TSession>(sp);
					}
				}
			};
			return serviceCollection.AddScoped<TSession>(constructor);
		}
		public static IServiceCollection AddXpoCustomSession<TSession>(this IServiceCollection serviceCollection, bool useDataLayerAsSingleton, Action<DataLayerOptionsBuilder> dataLayerOptionsBuildAction)
			where TSession : Session {
			DataLayerOptionsBuilder optionsBuilder = new DataLayerOptionsBuilder();
			dataLayerOptionsBuildAction(optionsBuilder);
			Func<IDataLayer> dataLayerFactory = DataLayerFactory.GetDataLayerFactory(optionsBuilder.DataLayerOptions);
			Func<IServiceProvider, TSession> constructor;
			if(useDataLayerAsSingleton) {
				Guid dataLayerKey = Guid.NewGuid();
				constructor = (sp) => {
					IDataLayer dataLayer = dataLayerSingletonCache.GetOrAdd(dataLayerKey, (key) => dataLayerFactory());
					return (TSession)SessionFactory.CreateSessionInstance(sp, typeof(TSession), dataLayer);
				};
			} else {
				constructor = (sp) => {
					IDataLayer dataLayer = dataLayerFactory();
					return (TSession)SessionFactory.CreateSessionInstance(sp, typeof(TSession), dataLayer, new IDisposable[] { dataLayer });
				};
			}
			return serviceCollection.AddScoped<TSession>(constructor);
		}
		public static IServiceCollection AddXpoCustomSession<TSession>(this IServiceCollection serviceCollection, bool useDataLayerAsSingleton, Func<IServiceProvider, IDataLayer> dataLayerFactory)
			where TSession : Session {
			Func<IServiceProvider, TSession> constructor;
			if(useDataLayerAsSingleton) {
				Guid dataLayerKey = Guid.NewGuid();
				constructor = (sp) => {
					IDataLayer dataLayer = dataLayerSingletonCache.GetOrAdd(dataLayerKey, (key) => dataLayerFactory(sp));
					return (TSession)SessionFactory.CreateSessionInstance(sp, typeof(TSession), dataLayer);
				};
			} else {
				constructor = (sp) => {
					IDataLayer dataLayer = dataLayerFactory(sp);
					return (TSession)SessionFactory.CreateSessionInstance(sp, typeof(TSession), dataLayer, new IDisposable[] { dataLayer });
				};
			}
			return serviceCollection.AddScoped<TSession>(constructor);
		}
		public static IServiceCollection AddXpoCustomSession<TSession>(this IServiceCollection serviceCollection, bool useObjectLayerAsSingleton, Func<IServiceProvider, IObjectLayer> objectLayerFactory)
			where TSession : Session {
			Func<IServiceProvider, TSession> constructor;
			if(useObjectLayerAsSingleton) {
				Guid objectLayerKey = Guid.NewGuid();
				constructor = (sp) => {
					IObjectLayer objectLayer = objectLayerSingletonCache.GetOrAdd(objectLayerKey, (key) => objectLayerFactory(sp));
					return (TSession)SessionFactory.CreateSessionInstance(sp, typeof(TSession), objectLayer);
				};
			} else {
				constructor = (sp) => {
					IObjectLayer objectLayer = objectLayerFactory(sp);
					return (TSession)SessionFactory.CreateSessionInstance(sp, typeof(TSession), objectLayer);
				};
			}
			return serviceCollection.AddScoped<TSession>(constructor);
		}
	}
	public class XpoDataStoreResult {
		public IDataStore DataStore { get; set; }
		public IDisposable[] ObjectsToDisposeOnDisconnect { get; set; }
		public XpoDataStoreResult() {
			ObjectsToDisposeOnDisconnect = Array.Empty<IDisposable>();
		}
		public XpoDataStoreResult(IDataStore dataStore, IDisposable[] objectsToDisposeOnDisconnect) {
			DataStore = dataStore;
			ObjectsToDisposeOnDisconnect = objectsToDisposeOnDisconnect;
		}
	}
	internal static class SessionFactory {
		readonly static ConcurrentDictionary<Type, Func<IServiceProvider, IDataLayer, IDisposable[], object>> sessionCompiledConstructorWithDatalayerCacheWithDisposableAndServiceProvider = new ConcurrentDictionary<Type, Func<IServiceProvider, IDataLayer, IDisposable[], object>>();
		readonly static ConcurrentDictionary<Type, Func<IServiceProvider, IObjectLayer, IDisposable[], object>> sessionCompiledConstructorsWithObjectlayerCacheWithDisposableAndServiceProvider = new ConcurrentDictionary<Type, Func<IServiceProvider, IObjectLayer, IDisposable[], object>>();
		readonly static ConcurrentDictionary<Type, Func<IServiceProvider, IDataLayer, object>> sessionCompiledConstructorWithDatalayerCacheAndServiceProvider = new ConcurrentDictionary<Type, Func<IServiceProvider, IDataLayer, object>>();
		readonly static ConcurrentDictionary<Type, Func<IServiceProvider, IObjectLayer, object>> sessionCompiledConstructorsWithObjectlayerCacheAndServiceProvider = new ConcurrentDictionary<Type, Func<IServiceProvider, IObjectLayer, object>>();
		readonly static ConcurrentDictionary<Type, Func<IDataLayer, IDisposable[], object>> sessionCompiledConstructorWithDatalayerCacheWithDisposable = new ConcurrentDictionary<Type, Func<IDataLayer, IDisposable[], object>>();
		readonly static ConcurrentDictionary<Type, Func<IObjectLayer, IDisposable[], object>> sessionCompiledConstructorsWithObjectlayerCacheWithDisposable = new ConcurrentDictionary<Type, Func<IObjectLayer, IDisposable[], object>>();
		readonly static ConcurrentDictionary<Type, Func<IDataLayer, object>> sessionCompiledConstructorWithDatalayerCache = new ConcurrentDictionary<Type, Func<IDataLayer, object>>();
		readonly static ConcurrentDictionary<Type, Func<IObjectLayer, object>> sessionCompiledConstructorsWithObjectlayerCache = new ConcurrentDictionary<Type, Func<IObjectLayer, object>>();
		readonly static ConcurrentDictionary<Type, Func<IServiceProvider, object>> sessionCompiledConstructorsWithIServiceProviderCache = new ConcurrentDictionary<Type, Func<IServiceProvider, object>>();
		public static object CreateSessionInstance(IServiceProvider serviceProvider, Type sessionType, IDataLayer dataLayer, params IDisposable[] disposeOnDisconnect) {
			Func<IServiceProvider, IDataLayer, IDisposable[], object> constructor = GetCompiledSessionConstructorWithServiceProviderAndDataLayerAndDisposable(sessionType);
			if(constructor != null) {
				return constructor(serviceProvider, dataLayer, disposeOnDisconnect);
			} else {
				Func<IDataLayer, IDisposable[], object> constructor2 = GetCompiledSessionConstructorWithDataLayerAndDisposable(sessionType);
				if(constructor2 != null) {
					return constructor2(dataLayer, disposeOnDisconnect);
				} else {
					throw new InvalidOperationException(Res.GetString(Res.ServiceCollectionExtensions_NoConstructor, sessionType.Name, "(IServiceProvider, IDataLayer, IDisposable[]) or (IDataLayer, IDisposable[])"));
				}
			}
		}
		public static object CreateSessionInstance(IServiceProvider serviceProvider, Type sessionType, IDataLayer dataLayer) {
			Func<IServiceProvider, IDataLayer, IDisposable[], object> constructor = GetCompiledSessionConstructorWithServiceProviderAndDataLayerAndDisposable(sessionType);
			if(constructor != null) {
				return constructor(serviceProvider, dataLayer, null);
			} else {
				Func<IServiceProvider, IDataLayer, object> constructor2 = GetCompiledSessionConstructorWithServiceProviderAndDataLayer(sessionType);
				if(constructor2 != null) {
					return constructor2(serviceProvider, dataLayer);
				} else {
					Func<IDataLayer, IDisposable[], object> constructor3 = GetCompiledSessionConstructorWithDataLayerAndDisposable(sessionType);
					if(constructor3 != null) {
						return constructor3(dataLayer, null);
					} else {
						Func<IDataLayer, object> constructor4 = GetCompiledSessionConstructorWithDataLayer(sessionType);
						if(constructor4 != null) {
							return constructor4(dataLayer);
						} else {
							throw new InvalidOperationException(Res.GetString(Res.ServiceCollectionExtensions_NoConstructor, sessionType.Name, "(IServiceProvider, IDataLayer) or (IServiceProvider, IDataLayer, IDisposable[]) or (IDataLayer) or (IDataLayer, IDisposable[])"));
						}
					}
				}
			}
		}
		public static object CreateSessionInstance(IServiceProvider serviceProvider, Type sessionType, IObjectLayer objectLayer, params IDisposable[] disposeOnDisconnect) {
			Func<IServiceProvider, IObjectLayer, IDisposable[], object> constructor = GetCompiledSessionConstructorWithServiceProviderAndObjectLayerAndDisposable(sessionType);
			if(constructor != null) {
				return constructor(serviceProvider, objectLayer, disposeOnDisconnect);
			} else {
				Func<IObjectLayer, IDisposable[], object> constructor2 = GetCompiledSessionConstructorWithObjectLayerAndDisposable(sessionType);
				if(constructor2 != null) {
					return constructor2(objectLayer, disposeOnDisconnect);
				} else {
					throw new InvalidOperationException(Res.GetString(Res.ServiceCollectionExtensions_NoConstructor, sessionType.Name, "(IServiceProvider, IObjectLayer, IDisposable[]) or (IObjectLayer, IDisposable[])"));
				}
			}
		}
		public static object CreateSessionInstance(IServiceProvider serviceProvider, Type sessionType, IObjectLayer objectLayer) {
			Func<IServiceProvider, IObjectLayer, IDisposable[], object> constructor = GetCompiledSessionConstructorWithServiceProviderAndObjectLayerAndDisposable(sessionType);
			if(constructor != null) {
				return constructor(serviceProvider, objectLayer, null);
			} else {
				Func<IServiceProvider, IObjectLayer, object> constructor1 = GetCompiledSessionConstructorWithServiceProviderAndObjectLayer(sessionType);
				if(constructor1 != null) {
					return constructor1(serviceProvider, objectLayer);
				} else {
					Func<IObjectLayer, IDisposable[], object> constructor2 = GetCompiledSessionConstructorWithObjectLayerAndDisposable(sessionType);
					if(constructor2 != null) {
						return constructor2(objectLayer, null);
					} else {
						Func<IObjectLayer, object> constructor4 = GetCompiledSessionConstructorWithObjectLayer(sessionType);
						if(constructor4 != null) {
							return constructor4(objectLayer);
						} else {
							throw new InvalidOperationException(Res.GetString(Res.ServiceCollectionExtensions_NoConstructor, sessionType.Name, "(IServiceProvider, IObjectLayer) or (IServiceProvider, IObjectLayer, IDisposable[]) or (IObjectLayer) or (IObjectLayer, IDisposable[])"));
						}
					}
				}
			}
		}
		public static object CreateSessionInstance<TSession>(IServiceProvider serviceProvider) where TSession : Session, new () {
			Func<IServiceProvider, object> constructor = GetCompiledSessionConstructorWithServiceProvider(typeof(TSession));
			if(constructor != null) {
				return constructor(serviceProvider);
			} else {
				return new TSession();
			}
		}
		static Func<IServiceProvider, object> GetCompiledSessionConstructorWithServiceProvider(Type sessionType)
		{
			return sessionCompiledConstructorsWithIServiceProviderCache.GetOrAdd(sessionType, (type) => CreateCompiledConstructor<IServiceProvider>(type));
		}
		static Func<IServiceProvider, IDataLayer, IDisposable[], object> GetCompiledSessionConstructorWithServiceProviderAndDataLayerAndDisposable(Type sessionType) {
			return sessionCompiledConstructorWithDatalayerCacheWithDisposableAndServiceProvider.GetOrAdd(sessionType, (type) => CreateCompiledConstructor<IServiceProvider, IDataLayer, IDisposable[]>(type));
		}
		static Func<IDataLayer, IDisposable[], object> GetCompiledSessionConstructorWithDataLayerAndDisposable(Type sessionType) {
			return sessionCompiledConstructorWithDatalayerCacheWithDisposable.GetOrAdd(sessionType, (type) => CreateCompiledConstructor<IDataLayer, IDisposable[]>(type));
		}
		static Func<IServiceProvider, IObjectLayer, IDisposable[], object> GetCompiledSessionConstructorWithServiceProviderAndObjectLayerAndDisposable(Type sessionType) {
			return sessionCompiledConstructorsWithObjectlayerCacheWithDisposableAndServiceProvider.GetOrAdd(sessionType, (type) => CreateCompiledConstructor<IServiceProvider, IObjectLayer, IDisposable[]>(type));
		}
		static Func<IObjectLayer, IDisposable[], object> GetCompiledSessionConstructorWithObjectLayerAndDisposable(Type sessionType) {
			return sessionCompiledConstructorsWithObjectlayerCacheWithDisposable.GetOrAdd(sessionType, (type) => CreateCompiledConstructor<IObjectLayer, IDisposable[]>(type));
		}
		static Func<IServiceProvider, IDataLayer, object> GetCompiledSessionConstructorWithServiceProviderAndDataLayer(Type sessionType) {
			return sessionCompiledConstructorWithDatalayerCacheAndServiceProvider.GetOrAdd(sessionType, (type) => CreateCompiledConstructor<IServiceProvider, IDataLayer>(type));
		}
		static Func<IDataLayer, object> GetCompiledSessionConstructorWithDataLayer(Type sessionType) {
			return sessionCompiledConstructorWithDatalayerCache.GetOrAdd(sessionType, (type) => CreateCompiledConstructor<IDataLayer>(type));
		}
		static Func<IServiceProvider, IObjectLayer, object> GetCompiledSessionConstructorWithServiceProviderAndObjectLayer(Type sessionType) {
			return sessionCompiledConstructorsWithObjectlayerCacheAndServiceProvider.GetOrAdd(sessionType, (type) => CreateCompiledConstructor<IServiceProvider, IObjectLayer>(type));
		}
		static Func<IObjectLayer, object> GetCompiledSessionConstructorWithObjectLayer(Type sessionType) {
			return sessionCompiledConstructorsWithObjectlayerCache.GetOrAdd(sessionType, (type) => CreateCompiledConstructor<IObjectLayer>(type));
		}
		static Func<TArg1, object> CreateCompiledConstructor<TArg1>(Type type) {
			ConstructorInfo ctorInfo = type.GetConstructor(new Type[] { typeof(TArg1) });
			if(ctorInfo == null) {
				return null;
			}
			ParameterExpression arg1 = Expression.Parameter(typeof(TArg1));
			Expression exprNew = Expression.New(ctorInfo, arg1);
			var expr = Expression.Lambda<Func<TArg1, object>>(exprNew, arg1);
			return expr.Compile();
		}
		static Func<TArg1, TArg2, object> CreateCompiledConstructor<TArg1, TArg2>(Type type) {
			ConstructorInfo ctorInfo = type.GetConstructor(new Type[] { typeof(TArg1), typeof(TArg2) });
			if(ctorInfo == null) {
				return null;
			}
			ParameterExpression arg1 = Expression.Parameter(typeof(TArg1));
			ParameterExpression arg2 = Expression.Parameter(typeof(TArg2));
			Expression exprNew = Expression.New(ctorInfo, arg1, arg2);
			var expr = Expression.Lambda<Func<TArg1, TArg2, object>>(exprNew, arg1, arg2);
			return expr.Compile();
		}
		static Func<TArg1, TArg2, TArg3, object> CreateCompiledConstructor<TArg1, TArg2, TArg3>(Type type) {
			ConstructorInfo ctorInfo = type.GetConstructor(new Type[] { typeof(TArg1), typeof(TArg2), typeof(TArg3) });
			if(ctorInfo == null) {
				return null;
			}
			ParameterExpression arg1 = Expression.Parameter(typeof(TArg1));
			ParameterExpression arg2 = Expression.Parameter(typeof(TArg2));
			ParameterExpression arg3 = Expression.Parameter(typeof(TArg3));
			Expression exprNew = Expression.New(ctorInfo, arg1, arg2, arg3);
			var expr = Expression.Lambda<Func<TArg1, TArg2, TArg3, object>>(exprNew, arg1, arg2, arg3);
			return expr.Compile();
		}
	}
	internal static class DataLayerFactory {
		public static Func<IDataLayer> GetDataLayerFactory(DataLayerOptions options) {
			return dataLayerFactoryFuncCache.GetOrAdd(options, (opt) => PrepareDataLayerFactoryFunc(opt));
		}
		readonly static ConcurrentDictionary<DataLayerOptions, Func<IDataLayer>> dataLayerFactoryFuncCache = new ConcurrentDictionary<DataLayerOptions, Func<IDataLayer>>();
		class Context {
			public XPDictionary Dictionary;
			public IDataStore DataStore;
			public IDataLayer DataLayer;
			public DataLayerOptions Options;
			public string ConnectionString;
			public readonly List<IDisposable> ObjectsToDispose = new List<IDisposable>();
		}
		static Func<IDataLayer> PrepareDataLayerFactoryFunc(DataLayerOptions options) {
			List<Action<Context>> actionList = new List<Action<Context>>();
			if(options.CustomDictionaryFactory != null) {
				AddAction(actionList, ctx => {
					ctx.Dictionary = options.CustomDictionaryFactory();
				});
			} else {
				if(options.EntityTypes != null && options.EntityTypes.Length > 0) {
					AddAction(actionList, ctx => {
						ctx.Dictionary = new ReflectionDictionary();
						ctx.Dictionary.NullableBehavior = ctx.Options.NullableBehavior;
						XPClassInfo[] classInfos = ctx.Dictionary.CollectClassInfos(ctx.Options.EntityTypes);
						var persistentClasses = classInfos.Where(t => t.IsPersistent).ToArray();
						if(persistentClasses.Length > 0) {
							ctx.Dictionary.GetDataStoreSchema(persistentClasses);
						}
					});
				}
			}
			if(options.DictionaryInitializedHandler != null) {
				AddAction(actionList, ctx => {
					if(ctx.Dictionary != null) {
						ctx.Options.DictionaryInitializedHandler(ctx.Dictionary);
					}
				});
			}
			if(options.UseThreadSafeDataLayer) {
				AddAction(actionList, ctx => {
					if(ctx.Dictionary == null) {
						throw new InvalidOperationException(Res.GetString(Res.ServiceCollectionExtensions_EntityTypesEmpty));
					};
				});
				if(options.CustomDataStoreFactory != null) {
					AddAction(actionList, ctx => {
						XpoDataStoreResult result = ctx.Options.CustomDataStoreFactory();
						if(result != null) {
							ctx.DataStore = result.DataStore;
							if(result.ObjectsToDisposeOnDisconnect != null) {
								ctx.ObjectsToDispose.AddRange(result.ObjectsToDisposeOnDisconnect);
							}
						}
					});
				} else {
					if(!options.UseInMemoryDataStore) {
						if(string.IsNullOrEmpty(options.ConnectionString)) {
							throw new InvalidOperationException(Res.GetString(Res.ServiceCollectionExtensions_ConnectionStringEmpty));
						}
						if(options.UseConnectionPool) {
							if(options.ConnectionPoolSize != null && options.ConnectionPoolMaxConnections != null) {
								AddAction(actionList, ctx => {
									ctx.ConnectionString = XpoDefault.GetConnectionPoolString(ctx.Options.ConnectionString, options.ConnectionPoolSize.Value, options.ConnectionPoolMaxConnections.Value);
								});
							} else if(options.ConnectionPoolSize != null) {
								AddAction(actionList, ctx => {
									ctx.ConnectionString = XpoDefault.GetConnectionPoolString(ctx.Options.ConnectionString, options.ConnectionPoolSize.Value);
								});
							} else {
								AddAction(actionList, ctx => {
									ctx.ConnectionString = XpoDefault.GetConnectionPoolString(ctx.Options.ConnectionString);
								});
							}
						} else {
							AddAction(actionList, ctx => {
								ctx.ConnectionString = ctx.Options.ConnectionString;
							});
						}
						AddAction(actionList, ctx => {
							IDisposable[] objectsToDispose;
							ctx.DataStore = XpoDefault.GetConnectionProvider(ctx.ConnectionString, ctx.Options.AutoCreateOption, out objectsToDispose);
							ctx.ObjectsToDispose.AddRange(objectsToDispose);
						});
					} else {
						AddAction(actionList, ctx => {
							ctx.DataStore = new InMemoryDataStore(ctx.Options.AutoCreateOption);
						});
					}
				}
				if(options.DataStoreInitializedHandler != null) {
					AddAction(actionList, ctx => {
						ctx.Options.DataStoreInitializedHandler(ctx.DataStore);
					});
				}
				if(options.UseThreadSafeDataLayerSchemaInitialization) {
					AddAction(actionList, ctx => {
						using(var updateDataLayer = new SimpleDataLayer(ctx.Dictionary, ctx.DataStore)) {
							updateDataLayer.UpdateSchema(false, ctx.Dictionary.Classes.Cast<XPClassInfo>().Where(t => t.IsPersistent).ToArray());
						}
					});
				}
				AddAction(actionList, ctx => {
					ctx.DataLayer = new ThreadSafeDataLayer(ctx.Dictionary, ctx.DataStore);
				});
			} else {
				if(options.CustomDataStoreFactory != null) {
					AddAction(actionList, ctx => {
						XpoDataStoreResult result = ctx.Options.CustomDataStoreFactory();
						if(result != null) {
							ctx.DataStore = result.DataStore;
							if(result.ObjectsToDisposeOnDisconnect != null) {
								ctx.ObjectsToDispose.AddRange(result.ObjectsToDisposeOnDisconnect);
							}
						}
					});
				} else {
					if(options.UseConnectionPool) {
						throw new InvalidOperationException(Res.GetString(Res.ServiceCollectionExtensions_ConnectionPoolCannotBeUsed));
					};
					if(!options.UseInMemoryDataStore) {
						if(string.IsNullOrEmpty(options.ConnectionString)) {
							throw new InvalidOperationException(Res.GetString(Res.ServiceCollectionExtensions_ConnectionStringEmpty));
						}
						AddAction(actionList, ctx => {
							IDisposable[] objectsToDispose;
							ctx.DataStore = XpoDefault.GetConnectionProvider(ctx.Options.ConnectionString, ctx.Options.AutoCreateOption, out objectsToDispose);
							ctx.ObjectsToDispose.AddRange(objectsToDispose);
						});
					} else {
						AddAction(actionList, ctx => {
							ctx.DataStore = new InMemoryDataStore(ctx.Options.AutoCreateOption);
						});
					}
				}
				if(options.DataStoreInitializedHandler != null) {
					AddAction(actionList, ctx => {
						ctx.Options.DataStoreInitializedHandler(ctx.DataStore);
					});
				}
				AddAction(actionList, ctx => {
					ctx.DataLayer = ctx.Dictionary == null ? new SimpleDataLayer(ctx.DataStore) : new SimpleDataLayer(ctx.Dictionary, ctx.DataStore);
				});
			}
			if(options.DataLayerInitializedHandler != null) {
				AddAction(actionList, ctx => {
					ctx.Options.DataLayerInitializedHandler(ctx.DataLayer);
				});
			}
			ParameterExpression varContextExpr = Expression.Parameter(typeof(Context), "ctx");
			IEnumerable<InvocationExpression> invokeActions = actionList.Select(action => Expression.Invoke(Expression.Constant(action), varContextExpr));
			Action<Context> invokeAllActionsCompiledFunc = Expression.Lambda<Action<Context>>(Expression.Block(invokeActions), varContextExpr).Compile();
			return new Func<IDataLayer>(() => {
				Context context = new Context() { Options = options };
				invokeAllActionsCompiledFunc(context);
				context.ObjectsToDispose.Add(context.DataLayer);
				return new DataLayerWrapperS18452(context.DataLayer, context.ObjectsToDispose.ToArray());
			});
		}
		static void AddAction(List<Action<Context>> actionList, Action<Context> action) {
			actionList.Add(action);
		}
	}
	public class DataLayerOptions : ICloneable {
		public string ConnectionString { get; internal set; }
		public Type[] EntityTypes { get; internal set; }
		public AutoCreateOption AutoCreateOption { get; internal set; }
		public NullableBehavior NullableBehavior { get; set; }
		public bool UseThreadSafeDataLayer { get; internal set; }
		public bool UseInMemoryDataStore { get; internal set; }
		public bool UseThreadSafeDataLayerSchemaInitialization { get; internal set; }
		public bool UseConnectionPool { get; internal set; }
		public int? ConnectionPoolSize { get; internal set; }
		public int? ConnectionPoolMaxConnections { get; internal set; }
		public Func<XPDictionary> CustomDictionaryFactory { get; internal set; }
		public Func<XpoDataStoreResult> CustomDataStoreFactory { get; internal set; }
		public Action<XPDictionary> DictionaryInitializedHandler { get; internal set; }
		public Action<IDataStore> DataStoreInitializedHandler { get; internal set; }
		public Action<IDataLayer> DataLayerInitializedHandler { get; internal set; }
		public object Clone() {
			DataLayerOptions copy = (DataLayerOptions)this.MemberwiseClone();
			copy.EntityTypes = (EntityTypes != null) ? (Type[])EntityTypes : null;
			copy.CustomDictionaryFactory = (CustomDictionaryFactory != null) ? (Func<XPDictionary>)CustomDictionaryFactory : null;
			copy.CustomDataStoreFactory = (CustomDataStoreFactory != null) ? (Func<XpoDataStoreResult>)CustomDataStoreFactory : null;
			copy.DictionaryInitializedHandler = (DictionaryInitializedHandler != null) ? (Action<XPDictionary>)DictionaryInitializedHandler : null;
			copy.DataStoreInitializedHandler = (DataStoreInitializedHandler != null) ? (Action<IDataStore>)DataStoreInitializedHandler : null;
			copy.DataLayerInitializedHandler = (DataLayerInitializedHandler != null) ? (Action<IDataLayer>)DataLayerInitializedHandler : null;
			return copy;
		}
		public override int GetHashCode() {
			int hash = HashCodeHelper.CalculateGeneric(ConnectionString, CustomDictionaryFactory, CustomDataStoreFactory,
				AutoCreateOption, UseThreadSafeDataLayer, UseInMemoryDataStore,
				UseThreadSafeDataLayerSchemaInitialization, UseConnectionPool, ConnectionPoolMaxConnections, ConnectionPoolSize,
				NullableBehavior, DictionaryInitializedHandler, DataStoreInitializedHandler, DataLayerInitializedHandler);
			if(EntityTypes != null) {
				hash = HashCodeHelper.CombineGenericList(hash, EntityTypes);
			}
			return hash;
		}
		public override bool Equals(object obj) {
			DataLayerOptions another = (obj as DataLayerOptions);
			if(another == null) {
				return false;
			}
			if(EntityTypes != another.EntityTypes && (EntityTypes == null || another.EntityTypes == null)) {
				return false;
			}
			if(EntityTypes != null) {
				if(EntityTypes.Length != another.EntityTypes.Length) {
					return false;
				}
				if(!Enumerable.SequenceEqual(EntityTypes, another.EntityTypes)) {
					return false;
				}
			}
			return ConnectionString == another.ConnectionString
				&& CustomDictionaryFactory == another.CustomDictionaryFactory
				&& CustomDataStoreFactory == another.CustomDataStoreFactory
				&& AutoCreateOption == another.AutoCreateOption
				&& UseThreadSafeDataLayer == another.UseThreadSafeDataLayer
				&& UseInMemoryDataStore == another.UseInMemoryDataStore
				&& UseThreadSafeDataLayerSchemaInitialization == another.UseThreadSafeDataLayerSchemaInitialization
				&& UseConnectionPool == another.UseConnectionPool
				&& ConnectionPoolSize == another.ConnectionPoolSize
				&& ConnectionPoolMaxConnections == another.ConnectionPoolMaxConnections
				&& NullableBehavior == another.NullableBehavior
				&& DictionaryInitializedHandler == another.DictionaryInitializedHandler
				&& DataStoreInitializedHandler == another.DataStoreInitializedHandler
				&& DataLayerInitializedHandler == another.DataLayerInitializedHandler;
		}
	}
	public class DataLayerOptionsBuilder {
		public DataLayerOptions DataLayerOptions { get; private set; }
		public DataLayerOptionsBuilder() {
			DataLayerOptions = new DataLayerOptions() {
				EntityTypes = Array.Empty<Type>(),
				AutoCreateOption = AutoCreateOption.SchemaAlreadyExists,
				UseThreadSafeDataLayer = true,
				UseConnectionPool = true,
				NullableBehavior = NullableBehavior.Default
			};
		}
		public DataLayerOptionsBuilder UseConnectionString(string connectionString) {
			DataLayerOptions = (DataLayerOptions)DataLayerOptions.Clone();
			DataLayerOptions.ConnectionString = connectionString;
			return this;
		}
		public DataLayerOptionsBuilder UseEntityTypes(params Type[] entityTypes) {
			if(entityTypes == null) {
				throw new ArgumentNullException(nameof(entityTypes));
			}
			DataLayerOptions = (DataLayerOptions)DataLayerOptions.Clone();
			Type[] newEntityTypes = new Type[DataLayerOptions.EntityTypes.Length + entityTypes.Length];
			Array.Copy(DataLayerOptions.EntityTypes, newEntityTypes, DataLayerOptions.EntityTypes.Length);
			Array.Copy(entityTypes, 0, newEntityTypes, DataLayerOptions.EntityTypes.Length, entityTypes.Length);
			DataLayerOptions.EntityTypes = newEntityTypes;
			return this;
		}
		public DataLayerOptionsBuilder UseAutoCreationOption(AutoCreateOption autoCreateOption) {
			DataLayerOptions = (DataLayerOptions)DataLayerOptions.Clone();
			DataLayerOptions.AutoCreateOption = autoCreateOption;
			return this;
		}
		public DataLayerOptionsBuilder UseThreadSafeDataLayer(bool isThreadSafe) {
			DataLayerOptions = (DataLayerOptions)DataLayerOptions.Clone();
			DataLayerOptions.UseThreadSafeDataLayer = isThreadSafe;
			return this;
		}
		public DataLayerOptionsBuilder UseInMemoryDataStore(bool useInMemoryDataStore) {
			DataLayerOptions = (DataLayerOptions)DataLayerOptions.Clone();
			DataLayerOptions.UseInMemoryDataStore = useInMemoryDataStore;
			return this;
		}
		public DataLayerOptionsBuilder UseThreadSafeDataLayerSchemaInitialization(bool useThreadSafeDataLayerSchemaInitialization) {
			DataLayerOptions = (DataLayerOptions)DataLayerOptions.Clone();
			DataLayerOptions.UseThreadSafeDataLayerSchemaInitialization = useThreadSafeDataLayerSchemaInitialization;
			return this;
		}
		public DataLayerOptionsBuilder UseConnectionPool(bool useConnectionPool) {
			DataLayerOptions = (DataLayerOptions)DataLayerOptions.Clone();
			DataLayerOptions.UseConnectionPool = useConnectionPool;
			return this;
		}
		public DataLayerOptionsBuilder UseCustomDictionaryFactory(Func<XPDictionary> factory) {
			DataLayerOptions = (DataLayerOptions)DataLayerOptions.Clone();
			DataLayerOptions.CustomDictionaryFactory = factory;
			return this;
		}
		public DataLayerOptionsBuilder UseCustomDataStoreFactory(Func<XpoDataStoreResult> factory) {
			DataLayerOptions = (DataLayerOptions)DataLayerOptions.Clone();
			DataLayerOptions.CustomDataStoreFactory = factory;
			return this;
		}
		public DataLayerOptionsBuilder UseConnectionPoolSize(int size) {
			DataLayerOptions = (DataLayerOptions)DataLayerOptions.Clone();
			DataLayerOptions.ConnectionPoolSize = size;
			return this;
		}
		public DataLayerOptionsBuilder UseConnectionPoolSizeAndMaxConnections(int size, int maxConnections) {
			DataLayerOptions = (DataLayerOptions)DataLayerOptions.Clone();
			DataLayerOptions.ConnectionPoolSize = size;
			DataLayerOptions.ConnectionPoolMaxConnections = maxConnections;
			return this;
		}
		public DataLayerOptionsBuilder UseNullableBehavior(NullableBehavior nullableBehavior) {
			DataLayerOptions = (DataLayerOptions)DataLayerOptions.Clone();
			DataLayerOptions.NullableBehavior = nullableBehavior;
			return this;
		}
		public DataLayerOptionsBuilder UseDictionaryInitializedHandler(Action<XPDictionary> action) {
			DataLayerOptions = (DataLayerOptions)DataLayerOptions.Clone();
			DataLayerOptions.DictionaryInitializedHandler = action;
			return this;
		}
		public DataLayerOptionsBuilder UseDataStoreInitializedHandler(Action<IDataStore> action) {
			DataLayerOptions = (DataLayerOptions)DataLayerOptions.Clone();
			DataLayerOptions.DataStoreInitializedHandler = action;
			return this;
		}
		public DataLayerOptionsBuilder UseDataLayerInitializedHandler(Action<IDataLayer> action) {
			DataLayerOptions = (DataLayerOptions)DataLayerOptions.Clone();
			DataLayerOptions.DataLayerInitializedHandler = action;
			return this;
		}
	}
}
#endif
