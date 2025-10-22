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
using System.Resources;
using System.Globalization;
using DevExpress.Utils;
using System.ComponentModel;
namespace DevExpress.Xpo {
	[EditorBrowsable(EditorBrowsableState.Never)]
	public sealed class Res {
		ResourceManager manager;
		static Res res;
		Res() {
			manager = new ResourceManager("DevExpress.Xpo.Messages", GetType().Assembly);
		}
		static Res GetLoader() {
			if(res == null) {
				lock(typeof(Res)) {
					if(res == null) {
						res = new Res();
					}
				}
			}
			return res;
		}
		public static string GetString(CultureInfo culture, string name) {
			Res r = Res.GetLoader();
			if(r == null)
				return null;
			return r.manager.GetString(name, culture);
		}
		public static string GetString(CultureInfo culture, string name, params object[] args) {
			Res r = Res.GetLoader();
			if(r == null)
				return null;
			string str = r.manager.GetString(name, culture);
			if(args != null && args.Length > 0)
				return String.Format(str, args);
			return str;
		}
		public static string GetString(string name) {
			return GetString(null, name);
		}
		public static string GetString(string name, params object[] args) {
			return GetString(null, name, args);
		}
		public const string Common_MethodOrOperationNotImplemented = "Common_MethodOrOperationNotImplemented";
		public const string Async_OperationCannotBePerformedBecauseNoSyncContext = "Async_OperationCannotBePerformedBecauseNoSyncContext";
		public const string Async_CanChangeProperties = "Async_CanChangeProperties";
		public const string Async_CanChangeViewProperties = "Async_CanChangeViewProperties";
		public const string Async_InternalErrorAsyncActionIsAlreadyDisposed = "Async_InternalErrorAsyncActionIsAlreadyDisposed";
		public const string Async_ObjectLayerDoesNotImplementIObjectLayerAsync = "Async_ObjectLayerDoesNotImplementIObjectLayerAsync";
		public const string Async_DataLayerDoesNotImplementIDataLayerAsync = "Async_DataLayerDoesNotImplementIDataLayerAsync";
		public const string Async_CommandChannelDoesNotImplementICommandChannelAsync = "Async_CommandChannelDoesNotImplementICommandChannelAsync";
		public const string Async_ConnectionProviderDoesNotImplementIDataStoreAsync = "Async_ConnectionProviderDoesNotImplementIDataStoreAsync";
		public const string Filtering_TheIifFunctionOperatorRequiresThree = "Filtering_TheIifFunctionOperatorRequiresThree";
		public const string Filtering_TheTypeNameArgumentOfTheX0FunctionIsNotFound = "Filtering_TheTypeNameArgumentOfTheX0FunctionIsNotFound";
		public const string Collections_InvalidCastOnAdd = "Collections_InvalidCastOnAdd";
		public const string Collections_CannotAssignProperty = "Collections_CannotAssignProperty";
		public const string Collections_CannotResolvePropertyTypeComparer = "Collections_CannotResolvePropertyTypeComparer";
		public const string Collections_CriteriaEvaluationBehaviorIsNotSupported = "Collections_CriteriaEvaluationBehaviorIsNotSupported";
		public const string Collections_RecurringObjectAdd = "Collections_RecurringObjectAdd";
		public const string Collections_NotCollectionProperty = "Collections_NotCollectionProperty";
		public const string Collections_WantNotDeleteFilteredAggregateCollection = "Collections_WantNotDeleteFilteredAggregateCollection";
		public const string Collections_GeneralPurposeCollectionInsteadOfRefCollection = "Collections_GeneralPurposeCollectionInsteadOfRefCollection";
		public const string LinqToXpo_DuplicateJoinOperatorFound = "LinqToXpo_DuplicateJoinOperatorFound";
		public const string LinqToXpo_WithDeletedOptionNotSupported = "LinqToXpo_WithDeletedOptionNotSupported";
		public const string ConnectionProvider_UnableToFillRefType = "ConnectionProvider_UnableToFillRefType";
		public const string ConnectionProvider_KeysAutogenerationNonSupportedTypeException = "ConnectionProvider_KeysAutogenerationNonSupportedTypeException";
		public const string ConnectionProvider_TheAutoIncrementedKeyWithX0TypeIsNotSupport = "ConnectionProvider_TheAutoIncrementedKeyWithX0TypeIsNotSupport";
		public const string CriteriaAnalyzer_ClassesAreNotAssignable = "CriteriaAnalyzer_ClassesAreNotAssignable";
		public const string CriteriaAnalyzer_PathNotFound = "CriteriaAnalyzer_PathNotFound";
		public const string CriteriaAnalyzer_NullTransitionMemberInfo = "CriteriaAnalyzer_NullTransitionMemberInfo";
		public const string CriteriaAnalyzer_EmptyJoinOperandId = "CriteriaAnalyzer_EmptyJoinOperandId";
		public const string CriteriaAnalyzer_NullJoinClassInfo = "CriteriaAnalyzer_NullJoinClassInfo";
		public const string CriteriaAnalyzer_TopLevelAggregateNotSupported = "CriteriaAnalyzer_TopLevelAggregateNotSupported";
		public const string DirectSQL_WrongColumnCount = "DirectSQL_WrongColumnCount";
		public const string DataView_ReferenceMembersWithCompoundKeyAreNotSupported = "DataView_ReferenceMembersWithCompoundKeyAreNotSupported";
		public const string DirectSQL_CollectionMembersAreNotSupported = "DirectSQL_CollectionMembersAreNotSupported";
		public const string Generator_OneOfBinaryOperatorsOperandsIsNull = "Generator_OneOfBinaryOperatorsOperandsIsNull";
		public const string Generator_TheUseOfNestedSingleAggregatesIsProhibited = "Generator_TheUseOfNestedSingleAggregatesIsProhibited";
		public const string Generator_TheUseOfATopLevelSingleAggregateIsProhibit = "Generator_TheUseOfATopLevelSingleAggregateIsProhibit";
		public const string Helpers_DifferentObjectsKeys = "Helpers_DifferentObjectsKeys";
		public const string Helpers_SameDictionaryExpected = "Helpers_SameDictionaryExpected";
		public const string Helpers_CloningObjectModified = "Helpers_CloningObjectModified";
		public const string Helpers_ResolveDataForCollection = "Helpers_ResolveDataForCollection";
		public const string InMemory_SingleRowExpected = "InMemory_SingleRowExpected";
		public const string InMemory_NotACollection = "InMemory_NotACollection";
		public const string InMemory_CannotFindParentRelationForNode = "InMemory_CannotFindParentRelationForNode";
		public const string InMemory_MalformedAggregate = "InMemory_MalformedAggregate";
		public const string InMemory_DataSetUncommitted = "InMemory_DataSetUncommitted";
		public const string InMemory_IsReadOnly = "InMemory_IsReadOnly";
		public const string InMemoryFull_DifferentColumnListLengths = "InMemoryFull_DifferentColumnListLengths";
		public const string InMemoryFull_CannotPrepareQueryPlan = "InMemoryFull_CannotPrepareQueryPlan";
		public const string InMemoryFull_CannotPrepareQueryPlanX0 = "InMemoryFull_CannotPrepareQueryPlanX0";
		public const string InMemoryFull_TableNotFound = "InMemoryFull_TableNotFound";
		public const string InMemoryFull_WrongIndexInfo = "InMemoryFull_WrongIndexInfo";
		public const string InMemoryFull_UseDataSetDataStoreOrCtor = "InMemoryFull_UseDataSetDataStoreOrCtor";
		public const string InMemorySet_AddRelation = "InMemorySet_AddRelation";
		public const string InMemorySet_InvalidTypeString = "InMemorySet_InvalidTypeString";
		public const string InMemorySet_XMLException_SchemaNodeNotFound = "InMemorySet_XMLException_SchemaNodeNotFound";
		public const string InMemorySet_XMLException_ElementNodeNotFound = "InMemorySet_XMLException_ElementNodeNotFound";
		public const string InMemorySet_XMLException_NotDataSetNode = "InMemorySet_XMLException_NotDataSetNode";
		public const string InMemorySet_XMLException_ComplexTypeNodeNotFound = "InMemorySet_XMLException_ComplexTypeNodeNotFound";
		public const string InMemorySet_XMLException_choiceNodeNotFound = "InMemorySet_XMLException_choiceNodeNotFound";
		public const string InMemorySet_XMLException_TableNameIsNotSpecified = "InMemorySet_XMLException_TableNameIsNotSpecified";
		public const string InMemorySet_XMLException_XScomplexTypeExpected = "InMemorySet_XMLException_XScomplexTypeExpected";
		public const string InMemorySet_XMLException_XSsequenceExpected = "InMemorySet_XMLException_XSsequenceExpected";
		public const string InMemorySet_XMLException_ColumnNameIsNotSpecified = "InMemorySet_XMLException_ColumnNameIsNotSpecified";
		public const string InMemorySet_XMLException_XSsimpleTypeExpected = "InMemorySet_XMLException_XSsimpleTypeExpected";
		public const string InMemorySet_XMLException_XSrestrictionExpected = "InMemorySet_XMLException_XSrestrictionExpected";
		public const string InMemorySet_XMLException_InvalidBaseAttributeValue = "InMemorySet_XMLException_InvalidBaseAttributeValue";
		public const string InMemorySet_XMLException_InvalidValueAttributeValue = "InMemorySet_XMLException_InvalidValueAttributeValue";
		public const string InMemorySet_XMLException_XSlengthExpected = "InMemorySet_XMLException_XSlengthExpected";
		public const string InMemorySet_XMLException_InvalidColumnTypeDeclaration = "InMemorySet_XMLException_InvalidColumnTypeDeclaration";
		public const string InMemorySet_XMLException_DataTypeAttributeNotFound = "InMemorySet_XMLException_DataTypeAttributeNotFound";
		public const string InMemorySet_XMLException_CantGetColumnType = "InMemorySet_XMLException_CantGetColumnType";
		public const string InMemorySet_XMLException_NameAttributeExpected = "InMemorySet_XMLException_NameAttributeExpected";
		public const string InMemorySet_XMLException_XSselectorNodeExpected = "InMemorySet_XMLException_XSselectorNodeExpected";
		public const string InMemorySet_XMLException_XpathAttributeExpected = "InMemorySet_XMLException_XpathAttributeExpected";
		public const string InMemorySet_XMLException_XpathAttributeWrongFormat = "InMemorySet_XMLException_XpathAttributeWrongFormat";
		public const string InMemorySet_XMLException_TableNotDeclared = "InMemorySet_XMLException_TableNotDeclared";
		public const string InMemorySet_XMLException_ReferAttributeExpected = "InMemorySet_XMLException_ReferAttributeExpected";
		public const string InMemorySet_XMLException_ReferNotFound = "InMemorySet_XMLException_ReferNotFound";
		public const string InMemorySet_XMLException_NullPrimaryColumns = "InMemorySet_XMLException_NullPrimaryColumns";
		public const string InMemorySet_XMLException_InconsistentForeignKeyCount = "InMemorySet_XMLException_InconsistentForeignKeyCount";
		public const string InMemorySet_XMLException_CantRestoreObjectFromXML = "InMemorySet_XMLException_CantRestoreObjectFromXML";
		public const string InMemorySet_CantUpdateRelationColumn = "InMemorySet_CantUpdateRelationColumn";
		public const string InMemorySet_WrongInitData = "InMemorySet_WrongInitData";
		public const string InMemorySet_InsertingDataIntoAutoincrementColumn = "InMemorySet_InsertingDataIntoAutoincrementColumn";
		public const string InMemorySet_InEditMode = "InMemorySet_InEditMode";
		public const string InMemorySet_ColumnNotFound = "InMemorySet_ColumnNotFound";
		public const string InMemorySet_WrongCommitInfo = "InMemorySet_WrongCommitInfo";
		public const string InMemorySet_WrongRollbackInfo = "InMemorySet_WrongRollbackInfo";
		public const string InMemorySet_NullDefaultValueNotAllowed = "InMemorySet_NullDefaultValueNotAllowed";
		public const string InMemorySet_NotLoadingMode = "InMemorySet_NotLoadingMode";
		public const string InMemorySet_UniqueIndex = "InMemorySet_UniqueIndex";
		public const string InMemorySet_WrongDictionaryIndex = "InMemorySet_WrongDictionaryIndex";
		public const string InMemorySet_DifferentTypes = "InMemorySet_DifferentTypes";
		public const string InMemorySet_ConstraintConflict = "InMemorySet_ConstraintConflict";
		public const string InMemorySet_DifferentComplexSet = "InMemorySet_DifferentComplexSet";
		public const string InMemorySet_AliasNotFound = "InMemorySet_AliasNotFound";
		public const string InMemorySet_OperationNotAllowed = "InMemorySet_OperationNotAllowed";
		public const string InTransactionLoader_NotInSelectDataMode = "InTransactionLoader_NotInSelectDataMode";
		public const string InTransactionLoader_ProcessReturnResultError = "InTransactionLoader_ProcessReturnResultError";
		public const string Loader_ReloadError = "Loader_ReloadError";
		public const string Loader_InternalErrorOrUnsupportedReferenceStructure = "Loader_InternalErrorOrUnsupportedReferenceStructure";
		public const string MetaData_IncorrectPath = "MetaData_IncorrectPath";
		public const string MetaData_IncorrectPathMemberNotExists = "MetaData_IncorrectPathMemberNotExists";
		public const string MetaData_IncorrectPathNonPersistentMember = "MetaData_IncorrectPathNonPersistentMember";
		public const string MetaData_IncorrectPathNonReferenceMember = "MetaData_IncorrectPathNonReferenceMember";
		public const string MetaData_KeyPropertyAbsent = "MetaData_KeyPropertyAbsent";
		public const string MetaData_DuplicateKeyProperty = "MetaData_DuplicateKeyProperty";
		public const string MetaData_CannotResolveClassInfo = "MetaData_CannotResolveClassInfo";
		public const string MetaData_XMLLoadError = "MetaData_XMLLoadError";
		public const string MetaData_XMLLoadErrorCannotFindClassinfoType = "MetaData_XMLLoadErrorCannotFindClassinfoType";
		public const string MetaData_XMLLoadErrorCannotFindConstructor = "MetaData_XMLLoadErrorCannotFindConstructor";
		public const string MetaData_XMLLoadErrorModelTagAbsent = "MetaData_XMLLoadErrorModelTagAbsent";
		public const string MetaData_XMLLoadErrorCannotResolveClassinfoInstanceType = "MetaData_XMLLoadErrorCannotResolveClassinfoInstanceType";
		public const string MetaData_XMLLoadErrorCannotLoadMember = "MetaData_XMLLoadErrorCannotLoadMember";
		public const string MetaData_XMLLoadErrorUnknownAttribute = "MetaData_XMLLoadErrorUnknownAttribute";
		public const string MetaData_PropertyTypeMismatch = "MetaData_PropertyTypeMismatch";
		public const string MetaData_PropertyMissing = "MetaData_PropertyMissing";
		public const string MetaData_AssociationElementTypeMissing = "MetaData_AssociationElementTypeMissing";
		public const string MetaData_RequiredAttributeMissing = "MetaData_RequiredAttributeMissing";
		public const string MetaData_NonPersistentReferenceFound = "MetaData_NonPersistentReferenceFound";
		public const string MetaData_PersistentReferenceFound = "MetaData_PersistentReferenceFound";
		public const string MetaData_SessionCtorAbsent = "MetaData_SessionCtorAbsent";
		public const string MetaData_ReferenceTooComplex = "MetaData_ReferenceTooComplex";
		public const string Metadata_SameTableName = "Metadata_SameTableName";
		public const string Metadata_CustomProperties_PersistentAndNonStorableType = "Metadata_CustomProperties_PersistentAndNonStorableType";
		public const string Metadata_CustomProperties_ReferenceOrCollectionInSessionStore = "Metadata_CustomProperties_ReferenceOrCollectionInSessionStore";
		public const string Metadata_CantPersistGenericType = "Metadata_CantPersistGenericType";
		public const string Metadata_DictionaryMixing = "Metadata_DictionaryMixing";
		public const string Metadata_SeveralClassesWithSameName = "Metadata_SeveralClassesWithSameName";
		public const string Metadata_PersistentAliasCircular = "Metadata_PersistentAliasCircular";
		public const string Metadata_AssociationInvalid_AssociationAttributeOnlyForListOrReference = "Metadata_AssociationInvalid_AssociationAttributeOnlyForListOrReference";
		public const string Metadata_AssociationInvalid_MoreThenOneAssociatedMemberFound = "Metadata_AssociationInvalid_MoreThenOneAssociatedMemberFound";
		public const string Metadata_AssociationInvalid_NoAssociationListInAssociation = "Metadata_AssociationInvalid_NoAssociationListInAssociation";
		public const string Metadata_AssociationInvalid_TwoAssociationListsInAssociation = "Metadata_AssociationInvalid_TwoAssociationListsInAssociation";
		public const string Metadata_AssociationInvalid_UseAssociationNameAsIntermediateTableNameMismatch = "Metadata_AssociationInvalid_UseAssociationNameAsIntermediateTableNameMismatch";
		public const string Metadata_AssociationInvalid_PropertyTypeMismatch = "Metadata_AssociationInvalid_PropertyTypeMismatch";
		public const string Metadata_AssociationInvalid_NotFound = "Metadata_AssociationInvalid_NotFound";
		public const string Metadata_AssociationInvalid_NonPersistentClassInTheAssociation = "Metadata_AssociationInvalid_NonPersistentClassInTheAssociation";
		public const string Metadata_NullSessionProvider = "Metadata_NullSessionProvider";
		public const string Metadata_DuplicateMappingField = "Metadata_DuplicateMappingField";
		public const string Metadata_NonPersistentKey = "Metadata_NonPersistentKey";
		public const string Metadata_ConverterOnKeyOrReference = "Metadata_ConverterOnKeyOrReference";
		public const string Metadata_NotCollection = "Metadata_NotCollection";
		public const string Metadata_ClassAttributeExclusive = "Metadata_ClassAttributeExclusive";
		public const string Metadata_MemberAttributeExclusive = "Metadata_MemberAttributeExclusive";
		public const string Metadata_AssociationListExpected = "Metadata_AssociationListExpected";
		public const string Metadata_TypeNotFound = "Metadata_TypeNotFound";
		public const string Metadata_SuppressSuspiciousMemberInheritanceCheckError = "Metadata_SuppressSuspiciousMemberInheritanceCheckError";
		public const string Metadata_WrongObjectType = "Metadata_WrongObjectType";
		public const string MetaData_PropertyIsDuplicatedInIndexDeclaration = "MetaData_PropertyIsDuplicatedInIndexDeclaration";
		public const string Metadata_AmbiguousClassName = "Metadata_AmbiguousClassName";
		public const string Metadata_YouCannotApplyTheMapInheritanceParentTable = "Metadata_YouCannotApplyTheMapInheritanceParentTable";
		public const string Metadata_NullableAttributeNotApplicable = "Metadata_NullableAttributeNotApplicable";
		public const string Metadata_FetchOnlyAttributeNotApplicable = "Metadata_FetchOnlyAttributeNotApplicable";
		public const string Metadata_FetchOnlyAttributeNotApplicableToReference = "Metadata_FetchOnlyAttributeNotApplicableToReference";
		public const string MsSql_RootIsNotInsertStatement = "MsSql_RootIsNotInsertStatement";
		public const string NestedSession_NotCloneable = "NestedSession_NotCloneable";
		public const string Object_ReferencePropertyViaCollectionAccessor = "Object_ReferencePropertyViaCollectionAccessor";
		public const string Object_CorrectPIinUOW = "Object_CorrectPIinUOW";
		public const string Object_PersistentOrThis = "Object_PersistentOrThis";
		public const string Object_DelayedPropertyContainerCannotBeAssigned = "Object_DelayedPropertyContainerCannotBeAssigned";
		public const string Object_EmptyMemberInstance = "Object_EmptyMemberInstance";
		public const string Object_NullInstance = "Object_NullInstance";
		public const string Object_DelayedPropertyDoesNotContainProperObject = "Object_DelayedPropertyDoesNotContainProperObject";
		public const string ObjectLayer_MemberNotFound = "ObjectLayer_MemberNotFound";
		public const string ObjectLayer_XPClassInfoNotFound = "ObjectLayer_XPClassInfoNotFound";
		public const string Paging_PageSizeShouldBeGreaterThanZero = "Paging_PageSizeShouldBeGreaterThanZero";
		public const string Paging_CurrentPageShouldBeGreaterOrEqualZero = "Paging_CurrentPageShouldBeGreaterOrEqualZero";
		public const string Paging_CurrentPageShouldBeLessThanPageCount = "Paging_CurrentPageShouldBeLessThanPageCount";
		public const string Paging_EnumeratorPositioning = "Paging_EnumeratorPositioning";
		public const string Paging_EnumeratorObjectModifiedOrDeleted = "Paging_EnumeratorObjectModifiedOrDeleted";
		public const string PersistentAliasExpander_ReferenceOrCollectionExpectedInTheMiddleOfThePath = "PersistentAliasExpander_ReferenceOrCollectionExpectedInTheMiddleOfThePath";
		public const string PersistentAliasExpander_NonPersistentCriteria = "PersistentAliasExpander_NonPersistentCriteria";
		public const string PersistentAliasExpander_NonPersistentCriteriaThisValueMember = "PersistentAliasExpander_NonPersistentCriteriaThisValueMember";
		public const string Security_TheCommitOperationWasProhibitedByTheRules = "Security_TheCommitOperationWasProhibitedByTheRules";
		public const string Security_SavingAnObjectWasProhibitedByTheRulesX0 = "Security_SavingAnObjectWasProhibitedByTheRulesOfVal";
		public const string Security_SavingThePropertyWasProhibitedByTheRulesX0X1 = "Security_SavingAnObjectWasProhibitedByTheRulesWProp";
		public const string Security_DeletingAnObjectWasProhibitedByTheRulesX0 = "Security_DeletingAnObjectWasProhibitedByTheRules";
		public const string Security_ICommandChannel_TransferringRequestsIsProhibited = "Security_ICommandChannel_TransferringRequestsIsProhibited";
		public const string SerializableObjectLayer_OptimisticLockFieldNotExists = "SerializableObjectLayer_OptimisticLockFieldNotExists";
		public const string SerializableObjectLayer_OptimisticLockFieldInDLNotExists = "SerializableObjectLayer_OptimisticLockFieldInDLNotExists";
		public const string SerializableObjectLayer_OLDoesNotImplementISerializableObjectLayerEx = "SerializableObjectLayer_OLDoesNotImplementISerializableObjectLayerEx";
		public const string SerOLHelpers_NestedParentMapIsNull = "SerOLHelpers_NestedParentMapIsNull";
		public const string ServerModeGridSource_WrongTopLevelAggregate = "ServerModeGridSource_WrongTopLevelAggregate";
		public const string ServerModeGridSource_SummaryItemTypeNotSupported = "ServerModeGridSource_SummaryItemTypeNotSupported";
		public const string ServerModeGridSource_GroupAndAddOrRemoveIsNotAllowed = "ServerModeGridSource_GroupAndAddOrRemoveIsNotAllowed";
		public const string Session_TypeNotFound = "Session_TypeNotFound";
		public const string Session_TypeFieldIsEmpty = "Session_TypeFieldIsEmpty";
		public const string Session_ObjectCannotBePurged = "Session_ObjectCannotBePurged";
		public const string Session_WrongConnectionString = "Session_WrongConnectionString";
		public const string Session_CannotReloadPurgedObject = "Session_CannotReloadPurgedObject";
		public const string Session_SessionMixing = "Session_SessionMixing";
		public const string Session_CannotAddObjectToObjectCache = "Session_CannotAddObjectToObjectCache";
		public const string Session_TranSequenceBegin = "Session_TranSequenceBegin";
		public const string Session_TranSequenceCommit = "Session_TranSequenceCommit";
		public const string Session_TranSequenceRollback = "Session_TranSequenceRollback";
		public const string Session_CannotLoadInvalidType = "Session_CannotLoadInvalidType";
		public const string Session_CannotChangePropertyWhenSessionIsConnected = "Session_CannotChangePropertyWhenSessionIsConnected";
		public const string Session_DifferentObjectsWithSameKey = "Session_DifferentObjectsWithSameKey";
		public const string Session_IncompatibleIdType = "Session_IncompatibleIdType";
		public const string Session_AlreadyConnected = "Session_AlreadyConnected";
		public const string Session_AssociationCollectionWithDisabledLoading = "Session_AssociationCollectionWithDisabledLoading";
		public const string Session_LengthsOfTheCollectionsAreDifferentInArgs = "Session_LengthsOfTheCollectionsAreDifferentInArgs";
		public const string Session_InternalXPOError = "Session_InternalXPOError";
		public const string Session_UnexpectedState = "Session_UnexpectedState";
		public const string Session_UnexpectedPersistentCriteriaEvaluationBehavior = "Session_UnexpectedPersistentCriteriaEvaluationBehavior";
		public const string Session_ObjectModificationIsNotAllowed = "Session_ObjectModificationIsNotAllowed";
		public const string Session_AssociationListExpected = "Session_AssociationListExpected";
		public const string Session_NotClassMember = "Session_NotClassMember";
		public const string Session_EnteringTheX0StateFromTheX1StateIsProhibit = "Session_EnteringTheX0StateFromTheX1StateIsProhibit";
		public const string Session_MostProbablyYouAreTryingToInitiateAnObject = "Session_MostProbablyYouAreTryingToInitiateAnObject";
		public const string Session_MostProbablyYouAreTryingToInitiateAnObjectEx = "Session_MostProbablyYouAreTryingToInitiateAnObjectEx";
		public const string Session_CrossThreadFailureDetected = "Session_CrossThreadFailureDetected";
		public const string Session_DictConstructor = "Session_DictConstructor";
		public const string ThreadSafeDataLayer_DictionaryModified = "ThreadSafeDataLayer_DictionaryModified";
		public const string View_AtLeastOneFetchPropertyShouldBeDefined = "View_AtLeastOneFetchPropertyShouldBeDefined";
		public const string View_PropertyNotFetched = "View_PropertyNotFetched";
		public const string View_View_IsInLoading = "View_IsInLoading";
		public const string XpoDefault_CannotAssignPropertyWhileAnotherIsNotNull = "XpoDefault_CannotAssignPropertyWhileAnotherIsNotNull";
		public const string XPWeakReference_SavedObjectExpected = "XPWeakReference_SavedObjectExpected";
		public const string DroneDataStore_CommitNotSupported = "DroneDataStore_CommitNotSupported";
		public const string LinqToXpo_X0OperatorAfterSkipOrTakeIsNotSupported = "LinqToXpo_X0OperatorAfterSkipOrTakeIsNotSupported";
		public const string LinqToXpo_X0OverGroupingIsNotSupported = "LinqToXpo_X0OverGroupingIsNotSupported";
		public const string LinqToXpo_X0WithSoManyParametersIsNotSupported = "LinqToXpo_X0WithSoManyParametersIsNotSupported";
		public const string LinqToXpo_SkipOperationIsNotSupportedWithoutSorting = "LinqToXpo_SkipOperationIsNotSupportedWithoutSorting";
		public const string LinqToXpo_ElemAtOperationIsNotSupportedWithoutSorting = "LinqToXpo_ElemAtOperationIsNotSupportedWithoutSorting";
		public const string LinqToXpo_SequenceContainsNoMatchingElement = "LinqToXpo_SequenceContainsNoMatchingElement";
		public const string LinqToXpo_SequenceContainsMoreThanOneElement = "LinqToXpo_SequenceContainsMoreThanOneElement";
		public const string LinqToXpo_ComplexDataSelectionIsNotSupportedPerhapsY = "LinqToXpo_ComplexDataSelectionIsNotSupportedPerhapsY";
		public const string LinqToXpo_X0WithSkipOrTakeOrGroupingIsNotSupported = "LinqToXpo_X0WithSkipOrTakeOrGroupingIsNotSupported";
		public const string LinqToXpo_IncorrectDeclaringTypeX0InTheMethodCallQue = "LinqToXpo_IncorrectDeclaringTypeX0InTheMethodCallQue";
		public const string LinqToXpo_ExpressionX0IsNotSupportedInX1 = "LinqToXpo_ExpressionX0IsNotSupportedInX1";
		public const string LinqToXpo_ExpressionX0IsNotSupported = "LinqToXpo_ExpressionX0IsNotSupported";
		public const string LinqToXpo_MethodX0ForX1IsNotSupported = "LinqToXpo_MethodX0ForX1IsNotSupported";
		public const string LinqToXpo_SpecifiedJoinKeySelectorsNotCompatibleX0X1 = "LinqToXpo_SpecifiedJoinKeySelectorsNotCompatibleX0X1";
		public const string LinqToXpo_TheJoinWithManyTablesSimultaneouslyInASing = "LinqToXpo_TheJoinWithManyTablesSimultaneouslyInASing";
		public const string LinqToXpo_DoesNotSupportNestedJoinsWithLevel = "LinqToXpo_DoesNotSupportNestedJoinsWithLevel";
		public const string LinqToXpo_TheDeclaringTypeAssemblyNamePropertyIsEmpty = "LinqToXpo_TheDeclaringTypeAssemblyNamePropertyIsEmpty";
		public const string LinqToXpo_TheDeclaringTypeNamePropertyIsEmpty = "LinqToXpo_TheDeclaringTypeNamePropertyIsEmpty";
		public const string LinqToXpo_SessionIsNull = "LinqToXpo_SessionIsNull";
		public const string LinqToXpo_QueryWithAppliedTheSkipOrTheTakeOperations = "LinqToXpo_QueryWithAppliedTheSkipOrTheTakeOperations";
		public const string LinqToXpo_SortingIsNotSupportedForSelectManyOperation = "LinqToXpo_SortingIsNotSupportedForSelectManyOperation";
		public const string LinqToXpo_CurrentExpressionWithX0IsNotSupported = "LinqToXpo_CurrentExpressionWithX0IsNotSupported";
		public const string LinqToXpo_GroupingWithACustomComparerIsNotSupported = "LinqToXpo_GroupingWithACustomComparerIsNotSupported";
		public const string LinqToXpo_TheLambdaExpressionWithSuchParametersIsNot = "LinqToXpo_TheLambdaExpressionWithSuchParametersIsNot";
		public const string LinqToXpo_LambdaExpressionIsExpectedX0 = "LinqToXpo_LambdaExpressionIsExpectedX0";
		public const string LinqToXpo_TheCallExpressionIsExpectedX0 = "LinqToXpo_TheCallExpressionIsExpectedX0";
		public const string LinqToXpo_TheCallExpressionReturnTypeX0 = "LinqToXpo_TheCallExpressionReturnTypeX0";
		public const string LinqToXpo_TheCallExpressionGenericReturnTypeX0 = "LinqToXpo_TheCallExpressionGenericReturnTypeX0";
		public const string LinqToXpo_TheCallExpressionXPQueryX0 = "LinqToXpo_TheCallExpressionXPQueryX0";
		public const string LinqToXpo_TheX0MethodIsNotSupported = "LinqToXpo_TheX0MethodIsNotSupported";
		public const string LinqToXpo_CachedExpressionIsIncompatible = "LinqToXpo_CachedExpressionIsIncompatible";
		public const string VistaDB_UpdatingSchemaIsForbiddenWhileExplicitTran = "VistaDB_UpdatingSchemaIsForbiddenWhileExplicitTran";
		public const string SqlConnectionProvider_CannotCreateAColumnForTheX0FieldWithTheX1D = "SqlConnectionProvider_CannotCreateAColumnForTheX0FieldWithTheX1D";
		public const string ASE_CommandNotAllowedWithinMultiStatementTransaction = "ASE_CommandNotAllowedWithinMultiStatementTransaction";
		public const string ServiceCollectionExtensions_ConnectionPoolCannotBeUsed = "ServiceCollectionExtensions_ConnectionPoolCannotBeUsed";
		public const string ServiceCollectionExtensions_ConnectionStringEmpty = "ServiceCollectionExtensions_ConnectionStringEmpty";
		public const string ServiceCollectionExtensions_EntityTypesEmpty = "ServiceCollectionExtensions_EntityTypesEmpty";
		public const string ServiceCollectionExtensions_NoConstructor = "ServiceCollectionExtensions_NoConstructor";
		internal const string PostgreSQL_LegacyGuidDetected = "PostgreSQL_LegacyGuidDetected";
		internal const string PostgreSQL_CommandPoolNotSupported = "PostgreSQL_CommandPoolNotSupported";
		public const string XPBindingSource_BadClassInfo_DataSource = "XPBindingSource_BadClassInfo_DataSource";
		public const string XPBindingSource_BadClassInfo_DataSourceDictionary = "XPBindingSource_BadClassInfo_DataSourceDictionary";
		public const string XPBindingSource_BadClassInfo_Dictionary = "XPBindingSource_BadClassInfo_Dictionary";
		public const string XPBindingSource_BadObjectType_DataSource = "XPBindingSource_BadObjectType_DataSource";
		public const string XpoDefault_CustomGuidGenerationHandlerCannotBeNull = "XpoDefault_CustomGuidGenerationHandlerCannotBeNull";
		public const string CustomAggregate_NotFound = "CustomAggregate_NotFound";
		public const string CustomAggregate_DoesNotImplementInterface = "CustomAggregate_DoesNotImplementInterface";
		public const string WebApi_ICacheToCacheCommunicationCore_NotImplemented = "WebApi_ICacheToCacheCommunicationCore_NotImplemented";
		public const string ImageValueConverter_NotPresent = "ImageValueConverter_NotPresent";
		public const string XpoServerMode_NonPersistentIsNotSupported = "XpoServerMode_NonPersistentIsNotSupported";
		public const string JsonConverter_UnexpectedToken = "JsonConverter_UnexpectedToken";
		public const string JsonConverter_CouldNotResolvePropertyType = "JsonConverter_CouldNotResolvePropertyType";
		public const string JsonConverter_CouldNotAssignPropertyValue = "JsonConverter_CouldNotAssignPropertyValue";
		public const string JsonConverter_CouldNotFindAppropriateConstructor = "JsonConverter_CouldNotFindAppropriateConstructor";
		public const string JsonConverter_ODataType_Mismatch = "JsonConverter_ODataType_Mismatch";
		public const string CreateCollection_InvalidCastException = "CreateCollection_InvalidCastException";
		public const string CollectClassInfos_CouldNotLoadAssembly = "CollectClassInfos_CouldNotLoadAssembly";
		public const string InitOperator_DeserializationProhibitedDueSecurityIssue = "InitOperator_DeserializationProhibitedDueSecurityIssue";
	}
}
