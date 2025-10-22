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
using System.ComponentModel;
using DevExpress.Xpo.DB.Helpers;
#if !NET
using System.ServiceModel.Activation;
#endif
using DevExpress.Xpo.Helpers;
namespace DevExpress.Xpo.DB {
	#region SerializableObjectLayerService
	[ServiceKnownType(typeof(IdList))]
	[ServiceContract, XmlSerializerFormat]
	public interface ISerializableObjectLayerService {
		[OperationContract(Action = "http://tempuri.org/ISerializableObjectLayerService/GetCanLoadCollectionObjects"), XmlSerializerFormat]
		OperationResult<bool> GetCanLoadCollectionObjects();
		[OperationContract(Action = "http://tempuri.org/ISerializableObjectLayerService/CommitObjects"), XmlSerializerFormat]
		OperationResult<CommitObjectStubsResult[]> CommitObjects(XPDictionaryStub dictionary, XPObjectStubCollection objectsForDelete, XPObjectStubCollection objectsForSave, LockingOption lockingOption);
		[OperationContract(Action = "http://tempuri.org/ISerializableObjectLayerService/CreateObjectType"), XmlSerializerFormat]
		OperationResult<object> CreateObjectType(string assemblyName, string typeName);
		[OperationContract(Action = "http://tempuri.org/ISerializableObjectLayerService/GetObjectsByKey"), XmlSerializerFormat]
		OperationResult<SerializableObjectLayerResult<XPObjectStubCollection[]>> GetObjectsByKey(XPDictionaryStub dictionary, GetObjectStubsByKeyQuery[] queries);
		[OperationContract(Action = "http://tempuri.org/ISerializableObjectLayerService/LoadCollectionObjects"), XmlSerializerFormat]
		OperationResult<SerializableObjectLayerResult<XPObjectStubCollection>> LoadCollectionObjects(XPDictionaryStub dictionary, string refPropertyName, XPObjectStub ownerObject);
		[OperationContract(Action = "http://tempuri.org/ISerializableObjectLayerService/LoadObjects"), XmlSerializerFormat]
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
		[ServiceKnownType(typeof(ConstantValue))]
		[ServiceKnownType(typeof(XPStubOperandValue))]
		[ServiceKnownType(typeof(ParameterValue))]
		[ServiceKnownType(typeof(QueryOperand))]
		[ServiceKnownType(typeof(UnaryOperator))]
		[ServiceKnownType(typeof(JoinOperand))]
		[ServiceKnownType(typeof(OperandParameter))]
		[ServiceKnownType(typeof(QuerySubQueryContainer))]
		[ServiceKnownType(typeof(XPObjectStub))]
		OperationResult<SerializableObjectLayerResult<XPObjectStubCollection[]>> LoadObjects(XPDictionaryStub dictionary, ObjectStubsQuery[] queries);
		[OperationContract(Action = "http://tempuri.org/ISerializableObjectLayerService/Purge"), XmlSerializerFormat]
		OperationResult<PurgeResult> Purge();
		[OperationContract(Action = "http://tempuri.org/ISerializableObjectLayerService/SelectData"), XmlSerializerFormat]
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
		[ServiceKnownType(typeof(ConstantValue))]
		[ServiceKnownType(typeof(XPStubOperandValue))]
		[ServiceKnownType(typeof(ParameterValue))]
		[ServiceKnownType(typeof(QueryOperand))]
		[ServiceKnownType(typeof(UnaryOperator))]
		[ServiceKnownType(typeof(JoinOperand))]
		[ServiceKnownType(typeof(OperandParameter))]
		[ServiceKnownType(typeof(QuerySubQueryContainer))]
		OperationResult<object[][]> SelectData(XPDictionaryStub dictionary, ObjectStubsQuery query, CriteriaOperatorCollection properties, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria);
		[OperationContract(Action = "http://tempuri.org/ISerializableObjectLayerService/GetParentObjectsToDelete"), XmlSerializerFormat]
		OperationResult<SerializableObjectLayerResult<XPObjectStubCollection>> GetParentObjectsToDelete();
		[OperationContract(Action = "http://tempuri.org/ISerializableObjectLayerService/GetParentObjectsToSave"), XmlSerializerFormat]
		OperationResult<SerializableObjectLayerResult<XPObjectStubCollection>> GetParentObjectsToSave();
		[OperationContract(Action = "http://tempuri.org/ISerializableObjectLayerService/GetParentTouchedClassInfos"), XmlSerializerFormat]
		OperationResult<string[]> GetParentTouchedClassInfos();
		[OperationContract(Action = "http://tempuri.org/ISerializableObjectLayerService/IsParentObjectToDelete"), XmlSerializerFormat]
		OperationResult<bool> IsParentObjectToDelete(XPDictionaryStub dictionary, XPObjectStub theObject);
		[OperationContract(Action = "http://tempuri.org/ISerializableObjectLayerService/IsParentObjectToSave"), XmlSerializerFormat]
		OperationResult<bool> IsParentObjectToSave(XPDictionaryStub dictionary, XPObjectStub theObject);
		[OperationContract(Action = "http://tempuri.org/ISerializableObjectLayerService/LoadDelayedProperties"), XmlSerializerFormat]
		OperationResult<SerializableObjectLayerResult<object[]>> LoadDelayedProperties(XPDictionaryStub dictionary, XPObjectStub theObject, string[] props);
		[OperationContract(Action = "http://tempuri.org/ISerializableObjectLayerService/LoadDelayedProperty"), XmlSerializerFormat]
		OperationResult<SerializableObjectLayerResult<object[]>> LoadDelayedProperty(XPDictionaryStub dictionary, XPObjectStubCollection objects, string property);
	}
	public class SerializableObjectLayerService : ServiceBase, ISerializableObjectLayerService {
		ISerializableObjectLayer serializableObjectLayer;
		[Description("Returns a serializable object layer used to initialize the current SerializableObjectLayerService instance.")]
		public ISerializableObjectLayer SerializableObjectLayer {
			get { return serializableObjectLayer; }
		}
		public SerializableObjectLayerService(ISerializableObjectLayer serializableObjectLayer) {
			this.serializableObjectLayer = serializableObjectLayer;
		}
		public virtual OperationResult<bool> GetCanLoadCollectionObjects() {
			return Execute<bool>(delegate() { return serializableObjectLayer.CanLoadCollectionObjects; });
		}
		public virtual OperationResult<CommitObjectStubsResult[]> CommitObjects(XPDictionaryStub dictionary, XPObjectStubCollection objectsForDelete, XPObjectStubCollection objectsForSave, LockingOption lockingOption) {
			return Execute<CommitObjectStubsResult[]>(delegate() { return serializableObjectLayer.CommitObjects(dictionary, objectsForDelete, objectsForSave, lockingOption); });
		}
		public virtual OperationResult<object> CreateObjectType(string assemblyName, string typeName) {
			return Execute<object>(delegate() { serializableObjectLayer.CreateObjectType(assemblyName, typeName); return null; });
		}
		public virtual OperationResult<SerializableObjectLayerResult<XPObjectStubCollection[]>> GetObjectsByKey(XPDictionaryStub dictionary, GetObjectStubsByKeyQuery[] queries) {
			return Execute<SerializableObjectLayerResult<XPObjectStubCollection[]>>(delegate() { return serializableObjectLayer.GetObjectsByKey(dictionary, queries); });
		}
		public virtual OperationResult<SerializableObjectLayerResult<XPObjectStubCollection>> LoadCollectionObjects(XPDictionaryStub dictionary, string refPropertyName, XPObjectStub ownerObject) {
			return Execute <SerializableObjectLayerResult<XPObjectStubCollection>>(delegate() { return serializableObjectLayer.LoadCollectionObjects(dictionary, refPropertyName, ownerObject); });
		}
		public virtual OperationResult<SerializableObjectLayerResult<XPObjectStubCollection[]>> LoadObjects(XPDictionaryStub dictionary, ObjectStubsQuery[] queries) {
			return Execute <SerializableObjectLayerResult<XPObjectStubCollection[]>>(delegate() { return serializableObjectLayer.LoadObjects(dictionary, queries); });
		}
		public virtual OperationResult<PurgeResult> Purge() {
			return Execute<PurgeResult>(delegate() { return serializableObjectLayer.Purge(); });
		}
		public virtual OperationResult<object[][]> SelectData(XPDictionaryStub dictionary, ObjectStubsQuery query, CriteriaOperatorCollection properties, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria) {
			return Execute<object[][]>(delegate() { return serializableObjectLayer.SelectData(dictionary, query, properties, groupProperties, groupCriteria); });
		}
		public virtual OperationResult<SerializableObjectLayerResult<XPObjectStubCollection>> GetParentObjectsToDelete() {
			return Execute<SerializableObjectLayerResult<XPObjectStubCollection>>(delegate() { return ((ISerializableObjectLayerEx)serializableObjectLayer).GetParentObjectsToDelete(); });
		}
		public virtual OperationResult<SerializableObjectLayerResult<XPObjectStubCollection>> GetParentObjectsToSave() {
			return Execute<SerializableObjectLayerResult<XPObjectStubCollection>>(delegate() { return ((ISerializableObjectLayerEx)serializableObjectLayer).GetParentObjectsToSave(); });
		}
		public virtual OperationResult<string[]> GetParentTouchedClassInfos() {
			return Execute<string[]>(delegate() { return ((ISerializableObjectLayerEx)serializableObjectLayer).GetParentTouchedClassInfos(); });
		}
		public virtual OperationResult<bool> IsParentObjectToDelete(XPDictionaryStub dictionary, XPObjectStub theObject) {
			return Execute<bool>(delegate() { return ((ISerializableObjectLayerEx)serializableObjectLayer).IsParentObjectToDelete(dictionary, theObject); });
		}
		public virtual OperationResult<bool> IsParentObjectToSave(XPDictionaryStub dictionary, XPObjectStub theObject) {
			return Execute<bool>(delegate() { return ((ISerializableObjectLayerEx)serializableObjectLayer).IsParentObjectToSave(dictionary, theObject); });
		}
		public virtual OperationResult<SerializableObjectLayerResult<object[]>> LoadDelayedProperties(XPDictionaryStub dictionary, XPObjectStub theObject, string[] props) {
			return Execute<SerializableObjectLayerResult<object[]>>(delegate() { return ((ISerializableObjectLayerEx)serializableObjectLayer).LoadDelayedProperties(dictionary, theObject, props); });
		}
		public virtual OperationResult<SerializableObjectLayerResult<object[]>> LoadDelayedProperty(XPDictionaryStub dictionary, XPObjectStubCollection objects, string property) {
			return Execute<SerializableObjectLayerResult<object[]>>(delegate() { return ((ISerializableObjectLayerEx)serializableObjectLayer).LoadDelayedProperties(dictionary, objects, property); });
		}
	}
#if !NET
	[ServiceBehaviorAttribute(InstanceContextMode = InstanceContextMode.Single)]
	public class SerializableObjectLayerSingletonService : SerializableObjectLayerService {
		public SerializableObjectLayerSingletonService(ISerializableObjectLayer serializableObjectLayer) : base(serializableObjectLayer) { }
	}
#endif
	public class SerializableObjectLayerServiceClient: SerializableObjectLayerServiceClientBase<ISerializableObjectLayerService>
	{
#if !NET
		public SerializableObjectLayerServiceClient(string configName) : base(configName) { }
#endif
		public SerializableObjectLayerServiceClient(System.ServiceModel.Channels.Binding binding, EndpointAddress remoteAddress) : base(binding, remoteAddress) { }
#if NET
		readonly bool needCustomChannel = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX);
		protected override ISerializableObjectLayerService CreateChannel() {
			return needCustomChannel ? new SerializableObjectLayerServiceClientChannel(this) : base.CreateChannel();
		}
		class SerializableObjectLayerServiceClientChannel : ChannelBase<ISerializableObjectLayerService>, ISerializableObjectLayerService {
			public SerializableObjectLayerServiceClientChannel(SerializableObjectLayerServiceClient client)
				: base(client) {
			}
			object Invoke(object[] args, [System.Runtime.CompilerServices.CallerMemberName]string methodName = null) {
				return DataStoreServiceInvokeHelper.SafeInvoke(BeginInvoke, EndInvoke, methodName, args);
			}
			public OperationResult<CommitObjectStubsResult[]> CommitObjects(XPDictionaryStub dictionary, XPObjectStubCollection objectsForDelete, XPObjectStubCollection objectsForSave, LockingOption lockingOption) {
				return (OperationResult<CommitObjectStubsResult[]>)Invoke(new object[] { dictionary, objectsForDelete, objectsForSave, lockingOption });
			}
			public OperationResult<object> CreateObjectType(string assemblyName, string typeName) {
				return (OperationResult<object>)Invoke(new object[] { assemblyName, typeName });
			}
			public OperationResult<bool> GetCanLoadCollectionObjects() {
				return (OperationResult<bool>)Invoke(Array.Empty<object>());
			}
			public OperationResult<SerializableObjectLayerResult<XPObjectStubCollection[]>> GetObjectsByKey(XPDictionaryStub dictionary, GetObjectStubsByKeyQuery[] queries) {
				return (OperationResult<SerializableObjectLayerResult<XPObjectStubCollection[]>>)Invoke(new object[] { dictionary, queries });
			}
			public OperationResult<SerializableObjectLayerResult<XPObjectStubCollection>> GetParentObjectsToDelete() {
				return (OperationResult<SerializableObjectLayerResult<XPObjectStubCollection>>)Invoke(Array.Empty<object>());
			}
			public OperationResult<SerializableObjectLayerResult<XPObjectStubCollection>> GetParentObjectsToSave() {
				return (OperationResult<SerializableObjectLayerResult<XPObjectStubCollection>>)Invoke(Array.Empty<object>());
			}
			public OperationResult<string[]> GetParentTouchedClassInfos() {
				return (OperationResult<string[]>)Invoke(Array.Empty<object>());
			}
			public OperationResult<bool> IsParentObjectToDelete(XPDictionaryStub dictionary, XPObjectStub theObject) {
				return (OperationResult<bool>)Invoke(new object[] { dictionary, theObject });
			}
			public OperationResult<bool> IsParentObjectToSave(XPDictionaryStub dictionary, XPObjectStub theObject) {
				return (OperationResult<bool>)Invoke(new object[] { dictionary, theObject });
			}
			public OperationResult<SerializableObjectLayerResult<XPObjectStubCollection>> LoadCollectionObjects(XPDictionaryStub dictionary, string refPropertyName, XPObjectStub ownerObject) {
				return (OperationResult<SerializableObjectLayerResult<XPObjectStubCollection>>)Invoke(new object[] { dictionary, refPropertyName, ownerObject });
			}
			public OperationResult<SerializableObjectLayerResult<object[]>> LoadDelayedProperties(XPDictionaryStub dictionary, XPObjectStub theObject, string[] props) {
				return (OperationResult<SerializableObjectLayerResult<object[]>>)Invoke(new object[] { dictionary, theObject, props });
			}
			public OperationResult<SerializableObjectLayerResult<object[]>> LoadDelayedProperty(XPDictionaryStub dictionary, XPObjectStubCollection objects, string property) {
				return (OperationResult<SerializableObjectLayerResult<object[]>>)Invoke(new object[] { dictionary, objects, property });
			}
			public OperationResult<SerializableObjectLayerResult<XPObjectStubCollection[]>> LoadObjects(XPDictionaryStub dictionary, ObjectStubsQuery[] queries) {
				return (OperationResult<SerializableObjectLayerResult<XPObjectStubCollection[]>>)Invoke(new object[] { dictionary, queries });
			}
			public OperationResult<PurgeResult> Purge() {
				return (OperationResult<PurgeResult>)Invoke(Array.Empty<object>());
			}
			public OperationResult<object[][]> SelectData(XPDictionaryStub dictionary, ObjectStubsQuery query, CriteriaOperatorCollection properties, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria) {
				return (OperationResult<object[][]>)Invoke(new object[] { dictionary, query, properties, groupProperties, groupCriteria });
			}
		}
#endif
	}
	public class SerializableObjectLayerServiceClientBase<IContractType> : ClientBase<IContractType>, ISerializableObjectLayer, ISerializableObjectLayerEx where IContractType : class, ISerializableObjectLayerService
	{
#if !NET
		public SerializableObjectLayerServiceClientBase(string configName) : base(configName) { }
#endif
		public SerializableObjectLayerServiceClientBase(System.ServiceModel.Channels.Binding binding, EndpointAddress remoteAddress) : base(binding, remoteAddress) { }
		public event ClientChannelCreatedHandler ClientChannelCreated;
		public static event ClientChannelCreatedHandler GlobalObjectClientChannelCreated;
		protected virtual void OnClientChannelCreated(object channel) {
			if(ClientChannelCreated != null) {
				ClientChannelCreated(this, new ClientChannelCreatedEventArgs(channel));
			}
			if(GlobalObjectClientChannelCreated != null) {
				GlobalObjectClientChannelCreated(this, new ClientChannelCreatedEventArgs(channel));
			}
		}
		IContractType channel;
		protected new IContractType Channel {
			get {
				IContractType currentChannel = channel;
				if(currentChannel == null) {
					currentChannel = CreateChannel();
					channel = currentChannel;
					OnClientChannelCreated(currentChannel);
				}
				return currentChannel;
			}
		}
		public bool CanLoadCollectionObjects {
			get { return ExecuteClient<bool>(delegate() { return Channel.GetCanLoadCollectionObjects(); }, null).HandleError(); }
		}
		public CommitObjectStubsResult[] CommitObjects(XPDictionaryStub dictionary, XPObjectStubCollection objectsForDelete, XPObjectStubCollection objectsForSave, LockingOption lockingOption) {
			return ExecuteClient<CommitObjectStubsResult[]>(delegate() { return Channel.CommitObjects(dictionary, objectsForDelete, objectsForSave, lockingOption); }, HandleModifyDataException).HandleError();
		}
		public void CreateObjectType(string assemblyName, string typeName) {
			ExecuteClient<object>(delegate() { return Channel.CreateObjectType(assemblyName, typeName); }, HandleModifyDataException).HandleError();
		}
		public SerializableObjectLayerResult<XPObjectStubCollection[]> GetObjectsByKey(XPDictionaryStub dictionary, GetObjectStubsByKeyQuery[] queries) {
			return ExecuteClient<SerializableObjectLayerResult<XPObjectStubCollection[]>>(delegate() { return Channel.GetObjectsByKey(dictionary, queries); }, HandleSelectDataException).HandleError();
		}
		public SerializableObjectLayerResult<XPObjectStubCollection> LoadCollectionObjects(XPDictionaryStub dictionary, string refPropertyName, XPObjectStub ownerObject) {
			return ExecuteClient<SerializableObjectLayerResult<XPObjectStubCollection>>(delegate() { return Channel.LoadCollectionObjects(dictionary, refPropertyName, ownerObject); }, HandleSelectDataException).HandleError();
		}
		public SerializableObjectLayerResult<XPObjectStubCollection[]> LoadObjects(XPDictionaryStub dictionary, ObjectStubsQuery[] queries) {
			return ExecuteClient<SerializableObjectLayerResult<XPObjectStubCollection[]>>(delegate() { return Channel.LoadObjects(dictionary, queries); }, HandleSelectDataException).HandleError();
		}
		public PurgeResult Purge() {
			return ExecuteClient<PurgeResult>(delegate() { return Channel.Purge(); }, HandleModifyDataException).HandleError();
		}
		public object[][] SelectData(XPDictionaryStub dictionary, ObjectStubsQuery query, CriteriaOperatorCollection properties, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria) {
			return ExecuteClient<object[][]>(delegate() { return Channel.SelectData(dictionary, query, properties, groupProperties, groupCriteria); }, HandleSelectDataException).HandleError();
		}
		[Description("")]
		public ISerializableObjectLayer ObjectLayer {
			get { return this; }
		}
		public SerializableObjectLayerResult<XPObjectStubCollection> GetParentObjectsToDelete() {
			return ExecuteClient<SerializableObjectLayerResult<XPObjectStubCollection>>(delegate() { return Channel.GetParentObjectsToDelete(); }, HandleSelectDataException).HandleError();
		}
		public SerializableObjectLayerResult<XPObjectStubCollection> GetParentObjectsToSave() {
			return ExecuteClient<SerializableObjectLayerResult<XPObjectStubCollection>>(delegate() { return Channel.GetParentObjectsToSave(); }, HandleSelectDataException).HandleError();
		}
		public string[] GetParentTouchedClassInfos() {
			return ExecuteClient<string[]>(delegate() { return Channel.GetParentTouchedClassInfos(); }, HandleSelectDataException).HandleError();
		}
		public bool IsParentObjectToDelete(XPDictionaryStub dictionary, XPObjectStub theObject) {
			return ExecuteClient<bool>(delegate() { return Channel.IsParentObjectToDelete(dictionary, theObject); }, HandleSelectDataException).HandleError();
		}
		public bool IsParentObjectToSave(XPDictionaryStub dictionary, XPObjectStub theObject) {
			return ExecuteClient<bool>(delegate() { return Channel.IsParentObjectToSave(dictionary, theObject); }, HandleSelectDataException).HandleError();
		}
		public SerializableObjectLayerResult<object[]> LoadDelayedProperties(XPDictionaryStub dictionary, XPObjectStubCollection objects, string property) {
			return ExecuteClient<SerializableObjectLayerResult<object[]>>(delegate() { return Channel.LoadDelayedProperty(dictionary, objects, property); }, HandleSelectDataException).HandleError();
		}
		public SerializableObjectLayerResult<object[]> LoadDelayedProperties(XPDictionaryStub dictionary, XPObjectStub theObject, string[] props) {
			return ExecuteClient<SerializableObjectLayerResult<object[]>>(delegate() { return Channel.LoadDelayedProperties(dictionary, theObject, props); }, HandleSelectDataException).HandleError();
		}
		public OperationResult<R> ExecuteClient<R>(OperationResultChannelPredicate<R> predicate) {
			return ExecuteClient(predicate, null);
		}
		public OperationResult<R> ExecuteClient<R>(OperationResultChannelPredicate<R> predicate, Func<Exception, bool> exceptionHandler) {
			return OperationResult.ExecuteClient<R, IContractType>(predicate, exceptionHandler, ref channel);
		}
		protected virtual bool HandleModifyDataException(Exception ex) {
			return !(ex is TimeoutException);
		}
		protected virtual bool HandleSelectDataException(Exception ex) {
			return true;
		}
	}
#endregion
	public class SerializableObjectLayerMarshalByRefObject : MarshalByRefObject, ISerializableObjectLayer, ISerializableObjectLayerEx {
		public static ISerializableObjectLayer SerializableObjectLayer;
		public bool CanLoadCollectionObjects {
			get { return SerializableObjectLayer.CanLoadCollectionObjects; }
		}
		public CommitObjectStubsResult[] CommitObjects(XPDictionaryStub dictionary, XPObjectStubCollection objectsForDelete, XPObjectStubCollection objectsForSave, LockingOption lockingOption) {
			return SerializableObjectLayer.CommitObjects(dictionary, objectsForDelete, objectsForSave, lockingOption);
		}
		public void CreateObjectType(string assemblyName, string typeName) {
			SerializableObjectLayer.CreateObjectType(assemblyName, typeName);
		}
		public SerializableObjectLayerResult<XPObjectStubCollection[]> GetObjectsByKey(XPDictionaryStub dictionary, GetObjectStubsByKeyQuery[] queries) {
			return SerializableObjectLayer.GetObjectsByKey(dictionary, queries);
		}
		public SerializableObjectLayerResult<XPObjectStubCollection> LoadCollectionObjects(XPDictionaryStub dictionary, string refPropertyName, XPObjectStub ownerObject) {
			return SerializableObjectLayer.LoadCollectionObjects(dictionary, refPropertyName, ownerObject);
		}
		public SerializableObjectLayerResult<XPObjectStubCollection[]> LoadObjects(XPDictionaryStub dictionary, ObjectStubsQuery[] queries) {
			return SerializableObjectLayer.LoadObjects(dictionary, queries);
		}
		public PurgeResult Purge() {
			return SerializableObjectLayer.Purge();
		}
		public object[][] SelectData(XPDictionaryStub dictionary, ObjectStubsQuery query, CriteriaOperatorCollection properties, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria) {
			return SerializableObjectLayer.SelectData(dictionary, query, properties, groupProperties, groupCriteria);
		}
		public ISerializableObjectLayer ObjectLayer {
			get { return this; }
		}
		public SerializableObjectLayerResult<XPObjectStubCollection> GetParentObjectsToDelete() {
			return ((ISerializableObjectLayerEx)SerializableObjectLayer).GetParentObjectsToDelete();
		}
		public SerializableObjectLayerResult<XPObjectStubCollection> GetParentObjectsToSave() {
			return ((ISerializableObjectLayerEx)SerializableObjectLayer).GetParentObjectsToSave();
		}
		public string[] GetParentTouchedClassInfos() {
			return ((ISerializableObjectLayerEx)SerializableObjectLayer).GetParentTouchedClassInfos();
		}
		public bool IsParentObjectToDelete(XPDictionaryStub dictionary, XPObjectStub theObject) {
			return ((ISerializableObjectLayerEx)SerializableObjectLayer).IsParentObjectToDelete(dictionary, theObject);
		}
		public bool IsParentObjectToSave(XPDictionaryStub dictionary, XPObjectStub theObject) {
			return ((ISerializableObjectLayerEx)SerializableObjectLayer).IsParentObjectToSave(dictionary, theObject);
		}
		public SerializableObjectLayerResult<object[]> LoadDelayedProperties(XPDictionaryStub dictionary, XPObjectStubCollection objects, string property) {
			return ((ISerializableObjectLayerEx)SerializableObjectLayer).LoadDelayedProperties(dictionary, objects, property);
		}
		public SerializableObjectLayerResult<object[]> LoadDelayedProperties(XPDictionaryStub dictionary, XPObjectStub theObject, string[] props) {
			return ((ISerializableObjectLayerEx)SerializableObjectLayer).LoadDelayedProperties(dictionary, theObject, props);
		}
	}
	public abstract class SerializableObjectLayerProxyBase : ISerializableObjectLayer, ISerializableObjectLayerProvider, ISerializableObjectLayerEx, ICommandChannel {
		protected abstract SerializableObjectLayer GetObjectLayer();
		[Description("Obtains the value from the SerializableObjectLayer.CanLoadCollectionObjects property of an object layer instance returned by the SerializableObjectLayerProxyBase.GetObjectLayer method implementation.")]
		[Browsable(false)]
		public virtual bool CanLoadCollectionObjects {
			get { return GetObjectLayer().CanLoadCollectionObjects; }
		}
		public virtual CommitObjectStubsResult[] CommitObjects(XPDictionaryStub dictionary, XPObjectStubCollection objectsForDelete, XPObjectStubCollection objectsForSave, LockingOption lockingOption) {
			return GetObjectLayer().CommitObjects(dictionary, objectsForDelete, objectsForSave, lockingOption);
		}
		public virtual void CreateObjectType(string assemblyName, string typeName) {
			GetObjectLayer().CreateObjectType(assemblyName, typeName);
		}
		public virtual SerializableObjectLayerResult<XPObjectStubCollection[]> GetObjectsByKey(XPDictionaryStub dictionary, GetObjectStubsByKeyQuery[] queries) {
			return GetObjectLayer().GetObjectsByKey(dictionary, queries);
		}
		public virtual SerializableObjectLayerResult<XPObjectStubCollection> LoadCollectionObjects(XPDictionaryStub dictionary, string refPropertyName, XPObjectStub ownerObject) {
			return GetObjectLayer().LoadCollectionObjects(dictionary, refPropertyName, ownerObject);
		}
		public virtual SerializableObjectLayerResult<XPObjectStubCollection[]> LoadObjects(XPDictionaryStub dictionary, ObjectStubsQuery[] queries) {
			return GetObjectLayer().LoadObjects(dictionary, queries);
		}
		public virtual PurgeResult Purge() {
			return GetObjectLayer().Purge();
		}
		public virtual object[][] SelectData(XPDictionaryStub dictionary, ObjectStubsQuery query, CriteriaOperatorCollection properties, CriteriaOperatorCollection groupProperties, CriteriaOperator groupCriteria) {
			return GetObjectLayer().SelectData(dictionary, query, properties, groupProperties, groupCriteria);
		}
		[Description("Obtains the value from the SerializableObjectLayer.ObjectLayer property of an object layer instance returned by the SerializableObjectLayerProxyBase.GetObjectLayer method implementation.")]
		[Browsable(false)]
		public virtual ISerializableObjectLayer ObjectLayer {
			get { return GetObjectLayer().ObjectLayer; }
		}
		public virtual SerializableObjectLayerResult<XPObjectStubCollection> GetParentObjectsToDelete() {
			return GetObjectLayer().GetParentObjectsToDelete();
		}
		public virtual SerializableObjectLayerResult<XPObjectStubCollection> GetParentObjectsToSave() {
			return GetObjectLayer().GetParentObjectsToSave();
		}
		public virtual string[] GetParentTouchedClassInfos() {
			return GetObjectLayer().GetParentTouchedClassInfos();
		}
		public virtual bool IsParentObjectToDelete(XPDictionaryStub dictionary, XPObjectStub theObject) {
			return GetObjectLayer().IsParentObjectToDelete(dictionary, theObject);
		}
		public virtual bool IsParentObjectToSave(XPDictionaryStub dictionary, XPObjectStub theObject) {
			return GetObjectLayer().IsParentObjectToSave(dictionary, theObject);
		}
		public virtual SerializableObjectLayerResult<object[]> LoadDelayedProperties(XPDictionaryStub dictionary, XPObjectStubCollection objects, string property) {
			return GetObjectLayer().LoadDelayedProperties(dictionary, objects, property);
		}
		public virtual SerializableObjectLayerResult<object[]> LoadDelayedProperties(XPDictionaryStub dictionary, XPObjectStub theObject, string[] props) {
			return GetObjectLayer().LoadDelayedProperties(dictionary, theObject, props);
		}
		public virtual object Do(string command, object args) {
			return ((ICommandChannel)GetObjectLayer()).Do(command, args);
		}
	}
}
