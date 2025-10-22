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

using DevExpress.Xpo.Logger.Transport;
using System.Collections.Generic;
using System.Threading;
namespace DevExpress.Xpo.Logger {
	public class LoggerBase: ILogger, ILogSource {
		public int Count {
			get {
				long currentHeadTail = Interlocked.Read(ref headtail);
				int head;
				int tail;
				GetHeadAndTail(currentHeadTail, out head, out tail);
				return (tail > head) ? tail - head : capacity - head + tail;
			}
		}
		int capacity;
		LogMessage[] messageArray;
		long headtail = 0;
		public int Capacity {
			get { return capacity; }
		}
		int lostMessageCount = 0;
		public int LostMessageCount {
			get { return lostMessageCount; }
		}
		bool enabled = true;
		public bool Enabled {
			get { return enabled; }
			set { enabled = value; }
		}
		public LoggerBase(int capacity) {
			this.capacity = capacity;
			this.messageArray = new LogMessage[capacity];
		}
		static void GetHeadAndTail(long headTail, out int head, out int tail) {
			head = (int)(headTail & 0x7FFFFFFF);
			tail = (int)((headTail & 0x7FFFFFFF00000000) >> 32);
		}
		static long GetHeadTail(int head, int tail) {
			return (long)(((((ulong)tail & 0x7FFFFFFF)) << 32) | ((((ulong)head) & 0x7FFFFFFF)));
		}
		public void Log(LogMessage message) {
			if (!enabled || capacity == 0) return;
			long oldHeadTail, newHeadTail;
			LogMessage oldMessage;
			int oldTail, oldHead;
			int newTail, newHead;
			bool lostMessage = false;
			do {
				oldHeadTail = headtail;
				GetHeadAndTail(oldHeadTail, out oldHead, out oldTail);
				newHead = oldHead + 1;
				newTail = oldTail;
				if(newHead >= capacity) {
					newHead = 0;
				}
				if((newHead >= newTail && oldHead < oldTail) || newHead == newTail) {
					newTail = newHead + 1;
					lostMessage = true;
				}
				if(newTail >= capacity) {
					newTail = 0;
				}
				newHeadTail = GetHeadTail(newHead, newTail);
				oldMessage = messageArray[oldHead];
			} while(oldHeadTail != Interlocked.CompareExchange(ref headtail, newHeadTail, oldHeadTail));
			if(Interlocked.CompareExchange<LogMessage>(ref messageArray[oldHead], message, oldMessage) != oldMessage) {
				Interlocked.Increment(ref lostMessageCount);
			}
			if(lostMessage){
				Interlocked.Increment(ref lostMessageCount);
			}
		}
		public void Log(LogMessage[] messages) {
			if(!enabled || capacity == 0) return;
			foreach(LogMessage message in messages) {
				Log(message);
			}
		}
		public LogMessage GetMessage() {
			if(!enabled) return null;
			long oldHeadTail, newHeadTail;
			int oldTail, oldHead;
			int newTail;
			LogMessage result;
			do {
				oldHeadTail = headtail;
				GetHeadAndTail(oldHeadTail, out oldHead, out oldTail);
				newTail = oldTail + 1;
				if (newTail >= capacity) {
					newTail = 0;
				}
				if(oldHead == oldTail) {
					return null;
				}
				newHeadTail = GetHeadTail(oldHead, newTail);
				result = messageArray[oldTail];
			} while(oldHeadTail != Interlocked.CompareExchange(ref headtail, newHeadTail, oldHeadTail) && result != messageArray[oldTail]);
			return result;
		}
		public LogMessage[] GetMessages(int messagesAmount) {
			if(!enabled) return null;
			List<LogMessage> result = new List<LogMessage>(messagesAmount);
			for(int i = 0; i < messagesAmount; i++) {
				LogMessage message = GetMessage();
				if(message == null) break;
				result.Add(message);
			}
			return result.ToArray();
		}
		public LogMessage[] GetCompleteLog() {
			if(!enabled) return null;
			List<LogMessage> result = new List<LogMessage>(Count);
			LogMessage message;
			while((message = GetMessage()) != null) {
				result.Add(message);
			}
			return result.ToArray();
		}
		public void ClearLog() {
			if(!enabled || capacity == 0) return;
			long oldHeadTail, newHeadTail;
			int oldTail, oldHead;
			int newTail, newHead;
			do {
				oldHeadTail = headtail;
				GetHeadAndTail(oldHeadTail, out oldTail, out oldHead);
				newHead = 0;
				newTail = 0;
				newHeadTail = GetHeadTail(newHead, newTail);
			} while(oldHeadTail != Interlocked.CompareExchange(ref headtail, newHeadTail, oldHeadTail));
		}
		public bool IsServerActive {
			get { return true; }
		}
	}
	namespace Transport {
		using System.ServiceModel;
		[ServiceContract]
		public interface ILogSource {
			[OperationContract]
			LogMessage GetMessage();
			[OperationContract]
			LogMessage[] GetMessages(int messageAmount);
			[OperationContract]
			LogMessage[] GetCompleteLog();
		}
	}
}
