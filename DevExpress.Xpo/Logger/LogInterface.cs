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
using System.ServiceModel;
using System.ServiceModel.Description;
using DevExpress.Xpo.Logger;
using System.ServiceModel.Channels;
using System.Globalization;
using System.Configuration;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
#if !NET
using System.ServiceModel.Configuration;
#endif
using DevExpress.Utils;
namespace DevExpress.Xpo.Logger.Transport {
	[ServiceContract]
	public interface ILogActivationServer {
		[OperationContract]
		bool Open(int port);
		[OperationContract]
		bool Close(int port);
		[OperationContract]
		bool IsOpened(int port);
	}
	public static class LogParams {
		public static readonly int LogLength = 5000;
		public static readonly string MutexName = "Global\\XPOLogServiceMutex:{8F7CD142-2BC3-48d7-92AA-54BD12A2FFB3}:";
		public static readonly int LogLengthLimit = 10000;
		public static readonly string LocalUriString = "net.tcp://localhost:{0}/WCFLogService";
		public static readonly string UriString = "net.tcp://{0}:{1}/WCFLogService";
		public static readonly string UriHttpString = "http://{0}:{1}/{2}";
		public static readonly string UriHttpStringPortless = "http://{0}/{1}";
		public static readonly int ServicePort = 52927;
		public static NetTcpBinding GetBinding() {
			NetTcpBinding tcpBinding = new NetTcpBinding();
			tcpBinding.Security.Mode = SecurityMode.Transport;
			tcpBinding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Windows;
#if !NET
			tcpBinding.Security.Transport.ProtectionLevel = System.Net.Security.ProtectionLevel.EncryptAndSign;
			tcpBinding.MaxConnections = 100;
#endif
			tcpBinding.MaxReceivedMessageSize = 1024 * 1024 * 1024;
			tcpBinding.ReceiveTimeout = new TimeSpan(0, 10, 0);
			tcpBinding.SendTimeout = new TimeSpan(0, 10, 0);
			tcpBinding.ReaderQuotas.MaxArrayLength = int.MaxValue;
			tcpBinding.ReaderQuotas.MaxBytesPerRead = int.MaxValue;
			tcpBinding.ReaderQuotas.MaxDepth = int.MaxValue;
			tcpBinding.ReaderQuotas.MaxNameTableCharCount = int.MaxValue;
			tcpBinding.ReaderQuotas.MaxStringContentLength = int.MaxValue;
			return tcpBinding;
		}
		public static Binding GetBinding(NetBinding binding) {
			switch(binding) {
				case NetBinding.NetTcpBinding:
					return GetBinding();
#if !NET
				case NetBinding.WSHttpBinding: {
						WSHttpBinding wsHttpBinding = new WSHttpBinding();
						wsHttpBinding.MaxReceivedMessageSize = 1024 * 1024 * 1024;
						wsHttpBinding.ReceiveTimeout = new TimeSpan(0, 10, 0);
						wsHttpBinding.SendTimeout = new TimeSpan(0, 10, 0);
						wsHttpBinding.ReaderQuotas.MaxArrayLength = int.MaxValue;
						wsHttpBinding.ReaderQuotas.MaxBytesPerRead = int.MaxValue;
						wsHttpBinding.ReaderQuotas.MaxDepth = int.MaxValue;
						wsHttpBinding.ReaderQuotas.MaxNameTableCharCount = int.MaxValue;
						wsHttpBinding.ReaderQuotas.MaxStringContentLength = int.MaxValue;
						return wsHttpBinding;
					}
#endif
				case NetBinding.BasicHttpBinding: {
						BasicHttpBinding basicHttpBinding = new BasicHttpBinding();
						basicHttpBinding.MaxReceivedMessageSize = 1024 * 1024 * 1024;
						basicHttpBinding.ReceiveTimeout = new TimeSpan(0, 10, 0);
						basicHttpBinding.SendTimeout = new TimeSpan(0, 10, 0);
						basicHttpBinding.ReaderQuotas.MaxArrayLength = int.MaxValue;
						basicHttpBinding.ReaderQuotas.MaxBytesPerRead = int.MaxValue;
						basicHttpBinding.ReaderQuotas.MaxDepth = int.MaxValue;
						basicHttpBinding.ReaderQuotas.MaxNameTableCharCount = int.MaxValue;
						basicHttpBinding.ReaderQuotas.MaxStringContentLength = int.MaxValue;
						return basicHttpBinding;
					}
				case NetBinding.WebApi:
					throw new NotSupportedException(binding.ToString());
				default:
					throw new InvalidOperationException();
			}
		}
		public static ServiceEndpoint CreateEndpoint(EndpointAddress address, Type contractType) {
			ServiceEndpoint point = new ServiceEndpoint(ContractDescription.GetContract(contractType), LogParams.GetBinding(), address);
			foreach (OperationDescription op in point.Contract.Operations) {
				DataContractSerializerOperationBehavior dataContractBehavior = op.Behaviors.Find<DataContractSerializerOperationBehavior>();
				if (dataContractBehavior != null)
					dataContractBehavior.MaxItemsInObjectGraph = 1024 * 1024 * 1024;
			}
			return point;
		}
	}
	public class Host {
		public string HostAddress = string.Empty;
		public int Port;
		public NetBinding Binding;
		public string ServiceName = string.Empty;
		public string VisibleName {
			get {
				if (Binding == NetBinding.NamedPipes) return string.Format(CultureInfo.InvariantCulture, "\\\\{0}\\{1}\\", HostAddress, ServiceName);
				if (Binding == NetBinding.NetTcpBinding) return string.Format(CultureInfo.InvariantCulture, "net.tcp://{0}:{1}", HostAddress, Port);
				string hostWithProtocol;
				if(HostAddress != null && !HostAddress.StartsWithInvariantCultureIgnoreCase("http://") && !HostAddress.StartsWithInvariantCultureIgnoreCase("https://")) {
					hostWithProtocol = "http://" + HostAddress;
				} else {
					hostWithProtocol = HostAddress;
				}
				if (Port == -1) return string.Format(CultureInfo.InvariantCulture, "{0}/{1}", hostWithProtocol, ServiceName);
				return string.Format(CultureInfo.InvariantCulture, "{0}:{1}/{2}", hostWithProtocol, Port, ServiceName);
			}
		}
		public Host() { }
		public Host(string host, int port)
			: this() {
			this.HostAddress = host;
			this.Port = port;
		}
		public Host(string host, int port, NetBinding binding, string serviceName)
			: this(host, port) {
			Binding = binding;
			ServiceName = serviceName;
		}
		public override bool Equals(object obj) {
			if (obj.GetType() != typeof(Host))
				return false;
			Host anotherObj = obj as Host;
			return Binding == anotherObj.Binding && HostAddress == anotherObj.HostAddress && Port == anotherObj.Port && ServiceName == anotherObj.ServiceName;
		}
		public override int GetHashCode() {
			return HashCodeHelper.CalculateGeneric(HostAddress, Port, Binding, ServiceName);
		}
		public static bool operator ==(Host host1, Host host2) {
			return host1.Equals(host2);
		}
		public static bool operator !=(Host host1, Host host2) {
			return !host1.Equals(host2);
		}
	}
	public enum NetBinding {
		NetTcpBinding,
#if !NET
		WSHttpBinding,
#endif
		BasicHttpBinding,
		WebApi,
		NamedPipes
	}
}
