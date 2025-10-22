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
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.ServiceModel;
using System.Diagnostics;
using DevExpress.Xpo.Logger;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Xml;
using System.Security;
using System.ServiceModel.Description;
namespace DevExpress.Xpo.Logger.Transport {
	public interface ILogClient: ILogSource, IDisposable {
		bool Start();
		bool Stop();
	}
	public class LogClient : ILogClient {
		ChannelFactory<ILogSource> myChannelFactory;
		ChannelFactory<ILogActivationServer> activationChannelFactory;
		ILogSource xpoLogClient;
		ILogActivationServer activationChannelClient;
		readonly string auxPath;
		readonly string activationPath;
		readonly bool useRemoteProxy;
		readonly int port;
		readonly NetBinding binding;
		Mutex mutex;
		bool active;
		public bool Active {
			get { return active; }
		}
		public string ChannelState {
			get { return myChannelFactory == null ? string.Empty : myChannelFactory.State.ToString(); }
		}
		public LogClient(string host, int port)
			: this(new Host(host, port)) {
		}
		[SecuritySafeCritical]
		public LogClient(Host host) {
			binding = host.Binding;
			useRemoteProxy = !IsLocalhost(host.HostAddress) && binding == NetBinding.NetTcpBinding;
			if (binding == NetBinding.NetTcpBinding) {
				this.port = host.Port;
				activationPath = string.Format(CultureInfo.InvariantCulture, LogParams.UriString, host.HostAddress, LogParams.ServicePort);
				auxPath = string.Format(CultureInfo.InvariantCulture, LogParams.UriString, host.HostAddress, host.Port);
				if (!useRemoteProxy) {
					mutex = new Mutex(false, LogParams.MutexName + port.ToString(CultureInfo.InvariantCulture));
				}
			} else {
				if (host.Port == -1) auxPath = string.Format(CultureInfo.InvariantCulture, LogParams.UriHttpStringPortless, host.HostAddress, host.ServiceName);
				else auxPath = string.Format(CultureInfo.InvariantCulture, LogParams.UriHttpString, host.HostAddress, host.Port, host.ServiceName);
			}
		}
		public static bool IsLocalhost(string hostOrIP) {
			IPAddress address = null;
			if (!IPAddress.TryParse(hostOrIP, out address)) {
				address = null;
				try {
					IPHostEntry entry = Dns.GetHostEntry(hostOrIP);
					foreach (IPAddress currentAddress in entry.AddressList) {
						if (currentAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) {
							address = currentAddress;
							break;
						}
					}
				} catch (Exception) {
					return false;
				}
			}
			if (address == null) return false;
			return IPAddress.IsLoopback(address);
		}
		void InitializeChannel() {
			if (myChannelFactory == null) {
				myChannelFactory = new ChannelFactory<ILogSource>(LogParams.GetBinding(binding), new EndpointAddress(new Uri(auxPath), new SpnEndpointIdentity(String.Empty)));
				xpoLogClient = myChannelFactory.CreateChannel();
			}
		}
		void InitializeActivationChannel() {
			if (activationChannelFactory == null) {
				activationChannelFactory = new ChannelFactory<ILogActivationServer>(LogParams.GetBinding(), new EndpointAddress(new Uri(activationPath), new SpnEndpointIdentity(String.Empty)));
				activationChannelClient = activationChannelFactory.CreateChannel();
			}
		}
		public void Dispose() {
			if (Active) {
				Stop();
			}
			try {
				if(myChannelFactory != null) {
					try {
						myChannelFactory.Close();
					} catch(Exception) {
						myChannelFactory.Abort();
					}
				}
				if(activationChannelFactory != null) {
					try {
						activationChannelFactory.Close();
					} catch(Exception) {
						activationChannelFactory.Abort();
					}
				}
			} catch(Exception) { }
		}
		public LogMessage GetMessage() {
			LogMessage result = new LogMessage();
			try {
				InitializeChannel();
				result = xpoLogClient.GetMessage();
			} catch (Exception) {
				myChannelFactory = null;
				result = null;
			}
			return result;
		}
		public LogMessage[] GetMessages(int messagesAmount) {
			LogMessage[] result = Array.Empty<LogMessage>();
			try {
				InitializeChannel();
				result = xpoLogClient.GetMessages(messagesAmount);
			} catch (Exception) {
				myChannelFactory = null;
				result = null;
			}
			return result;
		}
		public LogMessage[] GetCompleteLog() {
			LogMessage[] result = Array.Empty<LogMessage>();
			try {
				InitializeChannel();
				result = xpoLogClient.GetCompleteLog();
			} catch(Exception e) {
				if(e.InnerException != null && e.InnerException.InnerException != null && e.InnerException.InnerException is XmlException)
					result = new LogMessage[] { new LogMessage(LogMessageType.LoggingEvent, "The maximum string content length quota has been exceeded") };
				else
					result = null;
				myChannelFactory = null;
			}
			return result;
		}
		public bool Start() {
			return (active = StartInternal());
		}
		bool StartInternal() {
			if (binding == NetBinding.NetTcpBinding) {
				if (useRemoteProxy) {
					try {
						InitializeActivationChannel();
						activationChannelClient.Open(port);
						return true;
					} catch (Exception) {
						return false;
					}
				}
				return mutex.WaitOne();
			}
			return true;
		}
		public bool Stop() {
			try {
				bool result = true;
				if (useRemoteProxy) {
					try {
						InitializeActivationChannel();
						result = activationChannelClient.Close(port);
					} catch (Exception) {
						activationChannelFactory = null;
						result = false;
					}
				}
				if (mutex != null) {
					mutex.ReleaseMutex();
					mutex.Close();
				}
				return result;
			} finally {
				active = false;
			}
		}
	}
}
