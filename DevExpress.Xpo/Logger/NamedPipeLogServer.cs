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
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
namespace DevExpress.Xpo.Logger.Transport {
	public class NamedPipeLogger : ILogger, IDisposable {
		const int defaultCapacity = 10000;
		readonly LoggerBase logger;
		readonly NamedPipeLogServer logServer;
		public NamedPipeLogger(string pipeName) {
			logger = new LoggerBase(defaultCapacity);
			logServer = new NamedPipeLogServer(pipeName, logger);
		}
		public int Count {
			get {
				return logger.Count;
			}
		}
		public int LostMessageCount {
			get {
				return logger.LostMessageCount;
			}
		}
		public bool IsServerActive {
			get {
				return logger.IsServerActive;
			}
		}
		public bool Enabled {
			get {
				return logger.Enabled;
			}
			set {
				logger.Enabled = value;
			}
		}
		public int Capacity {
			get {
				return logger.Capacity;
			}
		}
		public void ClearLog() {
			logger.ClearLog();
		}
		public void Log(LogMessage message) {
			logger.Log(message);
		}
		public void Log(LogMessage[] messages) {
			logger.Log(messages);
		}
		public void Dispose() {
			logServer.Dispose();
		}
	}
	class NamedPipeLogServer : IDisposable {
		NamedPipeServerStream namedPipeServer;
		readonly string pipeName;
		readonly ILogSource logger;
		public NamedPipeLogServer(string pipeName, ILogSource logSource) {
			this.logger = logSource;
			this.pipeName = pipeName;
			CreateNamedPipe();
		}
		void CreateNamedPipe() {
			namedPipeServer = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
			namedPipeServer.BeginWaitForConnection(NamedPipeClientConnectedHandler, null);
		}
		void DisposeNamedPipe() {
			if(namedPipeServer != null) {
				namedPipeServer.Dispose();
				namedPipeServer = null;
			}
		}
		void RecreateNamedPipe() {
			if(!isDisposed) {
				DisposeNamedPipe();
				CreateNamedPipe();
			}
		}
		void NamedPipeClientConnectedHandler(IAsyncResult ar) {
			if(namedPipeServer == null) {
				return;
			}
			namedPipeServer.EndWaitForConnection(ar);
			byte[] readBuffer = new byte[4];
			try {
				while(true) {
					int bytesRead = namedPipeServer.Read(readBuffer, 0, 1);
					if(bytesRead != 1) {
						RecreateNamedPipe();
						return;
					}
					LogMessage[] data;
					switch(readBuffer[0]) {
						case 1:
							data = logger.GetCompleteLog();
							break;
						case 2:
							LogMessage message = logger.GetMessage();
							data = message != null ? new LogMessage[] { message } : Array.Empty<LogMessage>();
							break;
						case 3:
							bytesRead = namedPipeServer.Read(readBuffer, 0, 4);
							if(bytesRead != 4) {
								RecreateNamedPipe();
								return;
							}
							int messagesAmount = BitConverter.ToInt32(readBuffer, 0);
							if(messagesAmount <= 0) {
								RecreateNamedPipe();
								return;
							}
							data = logger.GetMessages(messagesAmount);
							break;
						default:
							RecreateNamedPipe();
							return;
					}
					WriteResponseToNamedPipe(data);
				}
			} catch {
				RecreateNamedPipe();
			}
		}
		void WriteResponseToNamedPipe(LogMessage[] data) {
			var serializer = new DataContractSerializer(typeof(LogMessage[]));
			using(var stream = new MemoryStream()) {
				serializer.WriteObject(stream, data);
				byte[] buf = stream.GetBuffer();
				int dataLength = (int)stream.Length;
				namedPipeServer.Write(BitConverter.GetBytes(dataLength), 0, 4);
				namedPipeServer.Write(buf, 0, dataLength);
			}
		}
		bool isDisposed = false;
		public void Dispose() {
			isDisposed = true;
			DisposeNamedPipe();
		}
	}
}
