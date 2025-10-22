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

#if !NET
using System;
using System.Collections.Generic;
using System.Text;
using System.ServiceModel;
using System.Timers;
using System.Threading;
using System.ServiceModel.Description;
using DevExpress.Xpo.Logger;
using System.Globalization;
using System.Security;
namespace DevExpress.Xpo.Logger.Transport {
	public class LogServer : ILogger, IDisposable {
		public const int DefaultCapacity = 10000;
		int port;
		string mutexName;
		readonly object syncRoot = new object();
		ServiceHost serviceHost;
		LogMessageTimer timer = null;
		public bool Enabled {
			get { return logger.Enabled; }
			set { logger.Enabled = value; }
		}
		public int Count {
			get { return logger.Count; }
		}
		public int Capacity {
			get { return logger.Capacity; }
		}
		public int LostMessageCount {
			get { return logger.LostMessageCount; }
		}
		LoggerBase logger = new LoggerBase(DefaultCapacity);
		public bool IsServerActive {
			get {
				CheckIsActive();
				return Enabled && serviceHost != null;
			}
		}
		public int Port {
			get { return port; }
		}
		public LogServer(int port) {
			this.port = port;
			mutexName = LogParams.MutexName + port.ToString(CultureInfo.InvariantCulture);
		}
		void InitializeHost() {
			serviceHost = new ServiceHost(new LogService(logger));
			serviceHost.Description.Endpoints.Add(LogParams.CreateEndpoint(new EndpointAddress(new Uri(string.Format(LogParams.LocalUriString, port))), typeof(ILogSource)));
			serviceHost.Open();
		}
		public void Log(LogMessage message) {
			if(message == null) return;
			logger.Log(message);
		}
		public void Log(LogMessage[] messages) {
			if(messages == null || messages.Length == 0) return;
			logger.Log(messages);
		}
		int CheckDuration = 1;
		[SecuritySafeCritical]
		void CheckIsActive() {
			if(!logger.Enabled) {
				if(serviceHost == null) return;
				lock(syncRoot) {
					if(serviceHost == null) return;
					CheckDuration = 1;
					serviceHost.Close();
					serviceHost = null;
				}
				return;
			}
			if(serviceHost == null) {
				if(timer != null && timer.Elapsed.Seconds <= CheckDuration)
					return;
				lock(syncRoot) {
					if(timer != null && timer.Elapsed.Seconds <= CheckDuration)
						return;
					using(Mutex mutex = new Mutex(false, mutexName)) {
						if(timer == null) timer = new LogMessageTimer();
						timer.Restart();
						try {
							if(mutex.WaitOne(0)) {
								mutex.ReleaseMutex();
								if(CheckDuration < 5) CheckDuration++;
								return;
							}
							CheckDuration = 1;
							InitializeHost();
							return;
						} catch(AbandonedMutexException) { }
					}
				}
			} else {
				if(timer != null && timer.Elapsed.Seconds <= CheckDuration) {
					return;
				}
				lock(syncRoot) {
					if(timer != null && timer.Elapsed.Seconds <= CheckDuration) {
						return;
					}
					using(Mutex mutex = new Mutex(false, mutexName)) {
						if(timer == null) timer = new LogMessageTimer();
						timer.Restart();
						try {
							if(!mutex.WaitOne(0)) {
								if(CheckDuration < 5) CheckDuration++;
								return;
							}
						} catch(AbandonedMutexException) { }
						CheckDuration = 1;
						serviceHost.Close();
						serviceHost = null;
						mutex.ReleaseMutex();
						return;
					}
				}
			}
		}
		public void ClearLog() {
			logger.ClearLog();
		}
		public void Dispose() {
			ServiceHost sh = serviceHost;
			if(sh != null) {
				sh.Close();
			}
		}
	}
	[ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
	public class LogService : ILogSource {
		ILogSource logger;
		public ILogger LogServer {
			get { return logger as ILogger; }
		}
		public LogService(ILogSource logger) {
			this.logger = logger;
		}
		public LogMessage GetMessage() {
			return logger.GetMessage();
		}
		public LogMessage[] GetMessages(int messagesAmount) {
			return logger.GetMessages(messagesAmount);
		}
		public LogMessage[] GetCompleteLog() {
			return logger.GetCompleteLog();
		}
	}
	public class LogActivationServer : IDisposable {
		ServiceHost serviceHost;
		LogActivationService service;
		public LogActivationServer() {
			service = new LogActivationService();
			serviceHost = new ServiceHost(service);
			serviceHost.Description.Endpoints.Add(LogParams.CreateEndpoint(new EndpointAddress(new Uri(string.Format(LogParams.LocalUriString, LogParams.ServicePort))), typeof(ILogActivationServer)));
			serviceHost.Open();
		}
		public void Dispose() {
			if (serviceHost != null) {
				serviceHost.Close();
				serviceHost = null;
			}
			if (service != null) {
				service.CloseAll();
				service = null;
			}
		}
		[ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
		class LogActivationService : ILogActivationServer, IDisposable {
			bool disposed;
			readonly Thread mutexThread;
			readonly AutoResetEvent hasCommandEvent = new AutoResetEvent(false);
			readonly List<LogActivationServerCommand> commandList = new List<LogActivationServerCommand>();
			readonly Dictionary<int, Mutex> mutexDictionary = new Dictionary<int, Mutex>();
			public LogActivationService() {
				mutexThread = new Thread(new ThreadStart(MutexThreadBody));
				mutexThread.Name = "mutexThread";
				mutexThread.IsBackground = true;
				mutexThread.Start();
			}
			public bool Open(int port) {
				return (bool)ExecCommand(LogActivationServerCommandType.Open, port);
			}
			public bool Close(int port) {
				return (bool)ExecCommand(LogActivationServerCommandType.Close, port);
			}
			public bool IsOpened(int port) {
				return (bool)ExecCommand(LogActivationServerCommandType.Exists, port);
			}
			public object ExecCommand(LogActivationServerCommandType type, int port) {
				LogActivationServerCommand command = new LogActivationServerCommand(type, port);
				lock (commandList) {
					commandList.Add(command);
				}
				hasCommandEvent.Set();
				command.ReadyEvent.WaitOne();
				return command.Result;
			}
			public void MutexThreadBody() {
				while (true) {
					try {
						hasCommandEvent.WaitOne();
						lock (commandList) {
							lock (mutexDictionary) {
								foreach (LogActivationServerCommand command in commandList) {
									switch (command.Type) {
										case LogActivationServerCommandType.Open: {
												try {
													Mutex currentMutex;
													if (!disposed && !mutexDictionary.TryGetValue(command.Port, out currentMutex)) {
														currentMutex = new Mutex(true, LogParams.MutexName + command.Port.ToString(CultureInfo.InvariantCulture));
														mutexDictionary.Add(command.Port, currentMutex);
														command.Result = true;
													} else {
														command.Result = false;
													}
												} catch (Exception) {
													command.Result = false;
												}
												command.ReadyEvent.Set();
											}
											break;
										case LogActivationServerCommandType.Close: {
												try {
													Mutex currentMutex;
													if (!disposed && mutexDictionary.TryGetValue(command.Port, out currentMutex)) {
														currentMutex.ReleaseMutex();
														currentMutex.Close();
														mutexDictionary.Remove(command.Port);
														command.Result = true;
													} else {
														command.Result = false;
													}
												} catch (Exception) {
													command.Result = false;
												}
												command.ReadyEvent.Set();
											}
											break;
										case LogActivationServerCommandType.Exists: {
												command.Result = !disposed && mutexDictionary.ContainsKey(command.Port);
												command.ReadyEvent.Set();
											}
											break;
									}
								}
							}
							commandList.Clear();
						}
					} catch (Exception) { Thread.Sleep(1000); }
				}
			}
			public class LogActivationServerCommand {
				private LogActivationServerCommandType type;
				public LogActivationServerCommandType Type {
					get { return type; }
					set { type = value; }
				}
				readonly ManualResetEvent readyEvent;
				public ManualResetEvent ReadyEvent {
					get { return readyEvent; }
				}
				private int port;
				public int Port {
					get { return port; }
					set { port = value; }
				}
				object result;
				public object Result {
					get { return result; }
					set { result = value; }
				}
				public LogActivationServerCommand(LogActivationServerCommandType type, int port) {
					this.type = type;
					this.port = port;
					readyEvent = new ManualResetEvent(false);
				}
			}
			public enum LogActivationServerCommandType {
				Open,
				Close,
				Exists
			}
			public void CloseAll() {
				CloseAllInternal();
			}
			void IDisposable.Dispose() {
				CloseAllInternal();
			}
			void CloseAllInternal() {
				lock (mutexDictionary) {
					if (disposed) return;
					foreach (Mutex mutex in mutexDictionary.Values) {
						try {
							mutex.ReleaseMutex();
							mutex.Close();
						} catch (Exception) { }
					}
					disposed = true;
				}
			}
		}
	}
}
#endif
