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
using System.Collections.Generic;
using System.Text;
using DevExpress.Data.Filtering;
using DevExpress.Data.Filtering.Exceptions;
using DevExpress.Data.Filtering.Helpers;
using DevExpress.Utils;
using DevExpress.Xpo.Exceptions;
using DevExpress.Xpo.Infrastructure;
using DevExpress.Xpo.Metadata;
using DevExpress.Xpo.Metadata.Helpers;
#if !NET
using DevExpress.Data.NetCompatibility.Extensions;
#endif
namespace DevExpress.Xpo.Helpers {
	public abstract class StronglyTypedCriteriaVisitorBase<T> : IClientCriteriaVisitor<T> {
		protected readonly XPDictionary Dictionary;
		protected readonly XPClassInfo[] UpLevelsClassInfos;
		protected StronglyTypedCriteriaVisitorBase(XPClassInfo[] upLevelsClassInfos) {
			if(upLevelsClassInfos == null)
				throw new ArgumentNullException(nameof(upLevelsClassInfos));
			this.UpLevelsClassInfos = upLevelsClassInfos;
			if(this.UpLevelsClassInfos.Length > 0) this.Dictionary = this.UpLevelsClassInfos[0].Dictionary;
		}
		protected static XPClassInfo[] GetUpLevels(XPClassInfo[] upLevels, EvaluatorProperty prop) {
			List<XPClassInfo> result = new List<XPClassInfo>();
			for(int i = upLevels.Length - 1; i >= prop.UpDepth; --i) {
				result.Insert(0, upLevels[i]);
			}
			if(EvaluatorProperty.GetIsThisProperty(prop.PropertyPath) && result.Count > 0) return result.ToArray();
			XPClassInfo currentBase = upLevels[prop.UpDepth];
			MemberInfoCollection col = currentBase.ParsePath(prop.PropertyPath);
			foreach(XPMemberInfo mi in col) {
				if(mi.ReferenceType != null && mi.IsPersistent) {
					currentBase = mi.ReferenceType;
				}
				else if(mi.IsAssociationList) {
					currentBase = mi.CollectionElementType;
				}
				else {
					throw new ArgumentException(Res.GetString(Res.PersistentAliasExpander_ReferenceOrCollectionExpectedInTheMiddleOfThePath, mi.Name));
				}
				result.Insert(0, currentBase);
			}
			return result.ToArray();
		}
		protected XPClassInfo[] GetUpLevels(OperandProperty collection) {
			if(ReferenceEquals(collection, null) || collection.PropertyName == null || collection.PropertyName.Length == 0)
				return UpLevelsClassInfos;
			return GetUpLevels(UpLevelsClassInfos, EvaluatorProperty.Create(collection));
		}
		protected static XPClassInfo[] GetUpLevels(XPClassInfo[] upLevels, string className) {
			XPClassInfo addClassInfo;
			if(!MemberInfoCollection.TryResolveTypeAlsoByShortName(className, upLevels[0], out addClassInfo)) {
				throw new CannotResolveClassInfoException(string.Empty, className);
			}
			XPClassInfo[] result = new XPClassInfo[upLevels.Length + 1];
			Array.Copy(upLevels, 0, result, 1, upLevels.Length);
			result[0] = addClassInfo;
			return result;
		}
		protected XPClassInfo[] GetUpLevels(string className) {
			return GetUpLevels(UpLevelsClassInfos, className);
		}
		protected abstract T Visit(OperandProperty theOperand);
		protected abstract T Visit(AggregateOperand theOperand);
		protected abstract T Visit(JoinOperand theOperand);
		protected abstract T Visit(FunctionOperator theOperator);
		protected abstract T Visit(OperandValue theOperand);
		protected abstract T Visit(GroupOperator theOperator);
		protected abstract T Visit(InOperator theOperator);
		protected abstract T Visit(UnaryOperator theOperator);
		protected abstract T Visit(BinaryOperator theOperator);
		protected abstract T Visit(BetweenOperator theOperator);
		#region IBlahBlahVisitors implementation
		T IClientCriteriaVisitor<T>.Visit(OperandProperty theOperand) {
			return Visit(theOperand);
		}
		T IClientCriteriaVisitor<T>.Visit(AggregateOperand theOperand) {
			return Visit(theOperand);
		}
		T IClientCriteriaVisitor<T>.Visit(JoinOperand theOperand) {
			return Visit(theOperand);
		}
		T ICriteriaVisitor<T>.Visit(FunctionOperator theOperator) {
			return Visit(theOperator);
		}
		T ICriteriaVisitor<T>.Visit(OperandValue theOperand) {
			return Visit(theOperand);
		}
		T ICriteriaVisitor<T>.Visit(GroupOperator theOperator) {
			return Visit(theOperator);
		}
		T ICriteriaVisitor<T>.Visit(InOperator theOperator) {
			return Visit(theOperator);
		}
		T ICriteriaVisitor<T>.Visit(UnaryOperator theOperator) {
			return Visit(theOperator);
		}
		T ICriteriaVisitor<T>.Visit(BinaryOperator theOperator) {
			return Visit(theOperator);
		}
		T ICriteriaVisitor<T>.Visit(BetweenOperator theOperator) {
			return Visit(theOperator);
		}
		#endregion
	}
	public abstract class UnknownCriteriaEleminatorBase : StronglyTypedCriteriaVisitorBase<ExpandedCriteriaHolder> {
		bool underNot = false;
		readonly bool caseSensitive;
		protected UnknownCriteriaEleminatorBase(XPClassInfo[] upLevelsClassInfo, bool caseSensitive)
			: base(upLevelsClassInfo) {
			this.caseSensitive = caseSensitive;
		}
		protected override ExpandedCriteriaHolder Visit(AggregateOperand theOperand) {
			ExpandedCriteriaHolder processedCollectionPropertyHolder = Process(theOperand.CollectionProperty);
			if(processedCollectionPropertyHolder.RequiresPostProcessing)
				return ExpandedCriteriaHolder.Indeterminate(processedCollectionPropertyHolder.PostProcessingCause);
			OperandProperty processedCollectionProperty = (OperandProperty)processedCollectionPropertyHolder.ExpandedCriteria;
			XPClassInfo[] nestedUpLevels = GetUpLevels(processedCollectionProperty);
			ExpandedCriteriaHolder processedConditionHolder = ProcessInContext(nestedUpLevels, theOperand.Condition);
			if(processedConditionHolder.RequiresPostProcessing)
				return ExpandedCriteriaHolder.Indeterminate(processedConditionHolder.PostProcessingCause);
			if(processedConditionHolder.IsFalse)
				return GetFalseConditionAggregate(theOperand.AggregateType, theOperand.CustomAggregateName);
			if(theOperand.AggregateType != Aggregate.Custom) {
				ExpandedCriteriaHolder processedAggregatedExpressionHolder = ProcessInContext(nestedUpLevels, theOperand.AggregatedExpression);
				if(processedAggregatedExpressionHolder.RequiresPostProcessing)
					return ExpandedCriteriaHolder.Indeterminate(processedAggregatedExpressionHolder.PostProcessingCause);
				CriteriaOperator persistentOperator = new AggregateOperand(processedCollectionProperty,
					ReferenceEquals(null, processedAggregatedExpressionHolder.ExpandedCriteria) ? null : ExpandedCriteriaHolder.IfNeededConvertToBoolOperator(processedAggregatedExpressionHolder.ExpandedCriteria),
					theOperand.AggregateType,
					ExpandedCriteriaHolder.IfNeededConvertToLogicalOperator(processedConditionHolder.ExpandedCriteria));
				return new ExpandedCriteriaHolder(persistentOperator);
			}
			else {
				ExpandedCriteriaHolder[] processedAggrExprHolders = new ExpandedCriteriaHolder[theOperand.CustomAggregateOperands.Count];
				CriteriaOperator[] processedAggrExprs = new CriteriaOperator[theOperand.CustomAggregateOperands.Count];
				for(int i = 0; i < theOperand.CustomAggregateOperands.Count; i++) {
					ExpandedCriteriaHolder processedAggregatedExpressionHolder = ProcessInContext(nestedUpLevels, theOperand.CustomAggregateOperands[i]);
					if(processedAggregatedExpressionHolder.RequiresPostProcessing)
						return ExpandedCriteriaHolder.Indeterminate(processedAggregatedExpressionHolder.PostProcessingCause);
					processedAggrExprHolders[i] = processedAggregatedExpressionHolder;
					processedAggrExprs[i] = ReferenceEquals(null, processedAggregatedExpressionHolder.ExpandedCriteria) ? null : ExpandedCriteriaHolder.IfNeededConvertToBoolOperator(processedAggregatedExpressionHolder.ExpandedCriteria);
				}
				CriteriaOperator persistentOperator = new AggregateOperand(processedCollectionProperty, processedAggrExprs, theOperand.CustomAggregateName,
					ExpandedCriteriaHolder.IfNeededConvertToLogicalOperator(processedConditionHolder.ExpandedCriteria));
				return new ExpandedCriteriaHolder(persistentOperator);
			}
		}
		public static ExpandedCriteriaHolder GetFalseConditionAggregate(Aggregate aggregate, string customAggregateName) {
			if(aggregate == Aggregate.Custom && string.IsNullOrEmpty(customAggregateName)) {
				throw new ArgumentNullException(nameof(customAggregateName));
			}
			switch(aggregate) {
				case Aggregate.Exists:
					return ExpandedCriteriaHolder.False;
				case Aggregate.Count:
					return new ExpandedCriteriaHolder(new OperandValue(0), true);
				default:
					return new ExpandedCriteriaHolder(new OperandValue(null), true);
			}
		}
		public static ExpandedCriteriaHolder GetFalseConditionAggregate(Aggregate aggregate) {
			return GetFalseConditionAggregate(aggregate, null);
		}
		protected override ExpandedCriteriaHolder Visit(JoinOperand theOperand) {
			XPClassInfo[] nestedUpLevels = GetUpLevels(theOperand.JoinTypeName);
			ExpandedCriteriaHolder processedConditionHolder = ProcessInContext(nestedUpLevels, theOperand.Condition);
			if(processedConditionHolder.RequiresPostProcessing)
				return ExpandedCriteriaHolder.Indeterminate(processedConditionHolder.PostProcessingCause);
			if(processedConditionHolder.IsFalse)
				return GetFalseConditionAggregate(theOperand.AggregateType, theOperand.CustomAggregateName);
			CriteriaOperator persistentOperator;
			if(theOperand.AggregateType != Aggregate.Custom) {
				ExpandedCriteriaHolder processedAggregatedExpressionHolder = ProcessInContext(nestedUpLevels, theOperand.AggregatedExpression);
				if(processedAggregatedExpressionHolder.RequiresPostProcessing)
					return ExpandedCriteriaHolder.Indeterminate(processedAggregatedExpressionHolder.PostProcessingCause);
				persistentOperator = new JoinOperand(nestedUpLevels[0].FullName,
					ExpandedCriteriaHolder.IfNeededConvertToLogicalOperator(processedConditionHolder.ExpandedCriteria),
					theOperand.AggregateType,
					ReferenceEquals(null, processedAggregatedExpressionHolder.ExpandedCriteria) ? null : ExpandedCriteriaHolder.IfNeededConvertToBoolOperator(processedAggregatedExpressionHolder.ExpandedCriteria));
			}
			else {
				CriteriaOperator[] aggrExprs = new CriteriaOperator[theOperand.CustomAggregateOperands.Count];
				for(int i = 0; i < theOperand.CustomAggregateOperands.Count; i++) {
					ExpandedCriteriaHolder processedAggregatedExpressionHolder = ProcessInContext(nestedUpLevels, theOperand.CustomAggregateOperands[i]);
					if(processedAggregatedExpressionHolder.RequiresPostProcessing)
						return ExpandedCriteriaHolder.Indeterminate(processedAggregatedExpressionHolder.PostProcessingCause);
					aggrExprs[i] = ReferenceEquals(null, processedAggregatedExpressionHolder.ExpandedCriteria) ? null : ExpandedCriteriaHolder.IfNeededConvertToBoolOperator(processedAggregatedExpressionHolder.ExpandedCriteria);
				}
				persistentOperator = new JoinOperand(nestedUpLevels[0].FullName,
					ExpandedCriteriaHolder.IfNeededConvertToLogicalOperator(processedConditionHolder.ExpandedCriteria),
					theOperand.CustomAggregateName, aggrExprs);
			}
			return new ExpandedCriteriaHolder(persistentOperator);
		}
		protected override ExpandedCriteriaHolder Visit(FunctionOperator theOperator) {
			if(theOperator.OperatorType == FunctionOperatorType.Iif) {
				List<Lazy<CriteriaOperator>> lazyResultOperandsList = new List<Lazy<CriteriaOperator>>();
				List<Lazy<ExpandedCriteriaHolder>> lazyHolderList = new List<Lazy<ExpandedCriteriaHolder>>(theOperator.Operands.Count);
				for(int i = 0; i < theOperator.Operands.Count; i++) {
					CriteriaOperator criteriaToProcess = theOperator.Operands[i];
					Lazy<ExpandedCriteriaHolder> lazyProcessedOp = new Lazy<ExpandedCriteriaHolder>(() => Process(criteriaToProcess), false);
					lazyHolderList.Add(lazyProcessedOp);
					lazyResultOperandsList.Add(new Lazy<CriteriaOperator>(() => lazyProcessedOp.Value.ExpandedCriteria, false));
				}
				if(lazyHolderList.Count < 3 || (lazyHolderList.Count % 2) == 0) throw new ArgumentException(Res.GetString(Res.Filtering_TheIifFunctionOperatorRequiresThree));
				if(lazyHolderList.Count == 3) {
					if(lazyHolderList[0].Value.IsConstant) {
						ExpandedCriteriaHolder firstHolder = Process(ExpandedCriteriaHolder.IfNeededConvertToLogicalOperator(lazyResultOperandsList[0].Value));
						if(firstHolder.IsTrue) return lazyHolderList[1].Value;
						if(firstHolder.IsFalse) return lazyHolderList[2].Value;
					}
					else {
						if(lazyHolderList[0].Value.IsTrue) return lazyHolderList[1].Value;
						if(lazyHolderList[0].Value.IsFalse) return lazyHolderList[2].Value;
					}
					lazyResultOperandsList[0] = ExpandedCriteriaHolder.IfNeededConvertToLogicalOperator(lazyResultOperandsList[0]);
					lazyResultOperandsList[1] = ExpandedCriteriaHolder.IfNeededConvertToBoolOperator(lazyResultOperandsList[1]);
					lazyResultOperandsList[2] = ExpandedCriteriaHolder.IfNeededConvertToBoolOperator(lazyResultOperandsList[2]);
				}
				else {
					int index = -2;
					int resultShift = 0;
					bool allAreConstants = true;
					bool cutTail = false;
					ExpandedCriteriaHolder firstHolder = null;
					do {
						if(firstHolder != null) {
							if(firstHolder.IsFalse) {
								lazyResultOperandsList.RemoveRange(index + resultShift, 2);
								resultShift -= 2;
							}
							firstHolder = null;
						}
						index += 2;
						if(lazyHolderList[index].Value.IsConstant) {
							firstHolder = Process(ExpandedCriteriaHolder.IfNeededConvertToLogicalOperator(lazyResultOperandsList[index + resultShift].Value));
							if(firstHolder.IsTrue) {
								if(allAreConstants) return lazyHolderList[index + 1].Value;
								if(lazyResultOperandsList.Count == 3) {
									return lazyHolderList[index + 1].Value;
								}
								else {
									lazyResultOperandsList[index + resultShift] = ExpandedCriteriaHolder.IfNeededConvertToBoolOperator(lazyResultOperandsList[index + resultShift + 1]);
									lazyResultOperandsList.RemoveRange(index + resultShift + 1, lazyResultOperandsList.Count - (index + resultShift + 1));
									cutTail = true;
								}
								break;
							}
						}
						else {
							allAreConstants = false;
							if(lazyHolderList[index].Value.IsTrue) {
								if(lazyResultOperandsList.Count == 3 || index + resultShift == 0) {
									return lazyHolderList[index + 1].Value;
								}
								else {
									lazyResultOperandsList[index + resultShift] = ExpandedCriteriaHolder.IfNeededConvertToBoolOperator(lazyResultOperandsList[index + resultShift + 1]);
									lazyResultOperandsList.RemoveRange(index + resultShift + 1, lazyResultOperandsList.Count - (index + resultShift + 1));
									cutTail = true;
								}
								break;
							}
							lazyResultOperandsList[index + resultShift] = ExpandedCriteriaHolder.IfNeededConvertToLogicalOperator(lazyResultOperandsList[index + resultShift]);
							lazyResultOperandsList[index + resultShift + 1] = ExpandedCriteriaHolder.IfNeededConvertToBoolOperator(lazyResultOperandsList[index + resultShift + 1]);
						}
					} while((index + 3) < lazyHolderList.Count);
					if(!cutTail) {
						if(firstHolder != null && firstHolder.IsFalse) {
							if(allAreConstants) return lazyHolderList[index + 2].Value;
							lazyResultOperandsList.RemoveRange(index + resultShift, 2);
							resultShift -= 2;
						}
						lazyResultOperandsList[index + resultShift + 2] = ExpandedCriteriaHolder.IfNeededConvertToBoolOperator(lazyResultOperandsList[index + resultShift + 2]);
					}
				}
				FunctionOperator processedResult = new FunctionOperator(theOperator.OperatorType);
				foreach(var lazyOp in lazyResultOperandsList) {
					processedResult.Operands.Add(lazyOp.Value);
				}
				return new ExpandedCriteriaHolder(processedResult);
			}
			FunctionOperator result = new FunctionOperator(theOperator.OperatorType);
			bool allConstants = true;
			List<ExpandedCriteriaHolder> holderList = new List<ExpandedCriteriaHolder>();
			for(int i = 0; i < theOperator.Operands.Count; i++) {
				CriteriaOperator criteriaToProcess = theOperator.Operands[i];
				ExpandedCriteriaHolder processedOp = Process(criteriaToProcess);
				if(!processedOp.IsConstant) allConstants = false;
				if(processedOp.RequiresPostProcessing)
					return ExpandedCriteriaHolder.Indeterminate(processedOp.PostProcessingCause);
				holderList.Add(processedOp);
				result.Operands.Add(processedOp.ExpandedCriteria);
			}
			CriteriaOperator op;
			switch(result.OperatorType) {
				case FunctionOperatorType.UtcNow:
				case FunctionOperatorType.Now:
				case FunctionOperatorType.Today:
				case FunctionOperatorType.Rnd:
				case FunctionOperatorType.CustomNonDeterministic:
					break;
				case FunctionOperatorType.IsNull:
					if(allConstants) {
						op = Evaluate(result);
						return ExpandedCriteriaHolder.TryConvertToLogicalConstant(op, true);
					}
					else {
						for(int i = 0; i < result.Operands.Count; i++) {
							result.Operands[i] = ExpandedCriteriaHolder.IfNeededConvertToBoolOperator(result.Operands[i]);
						}
					}
					break;
				case FunctionOperatorType.Custom:
					if(allConstants) {
						op = Evaluate(result);
						if(!ReferenceEquals(op, null)) return new ExpandedCriteriaHolder(op, true);
					}
					else if(holderList.Count == 3) {
						string functionName = null;
						bool exactType = false;
						bool instanceOfType = false;
						OperandValue functionNameOperand = holderList[0].ExpandedCriteria as OperandValue;
						OperandValue typeNameOperand = holderList[2].ExpandedCriteria as OperandValue;
						if(!ReferenceEquals(functionNameOperand, null) && !ReferenceEquals(typeNameOperand, null)) {
							functionName = functionNameOperand.Value as string;
							exactType = (functionName == IsExactTypeFunction.FunctionName);
							instanceOfType = (functionName == IsInstanceOfTypeFunction.FunctionName);
						}
						if(exactType || instanceOfType) {
							string typeName;
							if(!holderList[2].IsConstant || string.IsNullOrEmpty(typeName = typeNameOperand.Value as string)) throw new ArgumentException(Res.GetString(Res.Filtering_TheTypeNameArgumentOfTheX0FunctionIsNotFound, functionName));
							CriteriaTypeResolverResult objectTypeResult = CriteriaTypeResolver.ResolveTypeResult(UpLevelsClassInfos, Dictionary, holderList[1].ExpandedCriteria, CriteriaTypeResolveKeyBehavior.AlwaysReference);
							XPClassInfo propertyClassInfo = objectTypeResult.Tag as XPClassInfo;
							if(propertyClassInfo == null) {
								if(objectTypeResult.Type == typeof(object)) {
									return ExpandedCriteriaHolder.Indeterminate(exactType ? IsExactTypeFunction.FunctionName : IsInstanceOfTypeFunction.FunctionName);
								}
								if(exactType) {
									return IsInstanceOfTypeFunctionHelper.EqualsType(typeName, objectTypeResult.Type.FullName) ? ExpandedCriteriaHolder.True : ExpandedCriteriaHolder.False;
								}
								return IsInstanceOfTypeFunctionHelper.IsInstanceOfType(typeName, objectTypeResult.Type) ? ExpandedCriteriaHolder.True : ExpandedCriteriaHolder.False;
							}
							if(!propertyClassInfo.IsTypedObject) {
								if(exactType) {
									return IsInstanceOfTypeFunctionHelper.EqualsType(typeName, propertyClassInfo.FullName) ? ExpandedCriteriaHolder.True : ExpandedCriteriaHolder.False;
								}
								return IsInstanceOfTypeFunctionXpoHelper.IsInstanceOfType(typeName, propertyClassInfo) ? ExpandedCriteriaHolder.True : ExpandedCriteriaHolder.False;
							}
							XPClassInfo searchedClassInfo;
							if(!MemberInfoCollection.TryResolveTypeAlsoByShortName(typeName, propertyClassInfo, out searchedClassInfo) || searchedClassInfo == null)
								throw new InvalidOperationException(Res.GetString(Res.Metadata_TypeNotFound, typeName));
							if(!searchedClassInfo.IsPersistent || !searchedClassInfo.IsTypedObject) return ExpandedCriteriaHolder.False;
							CriteriaOperator propertyArgument = holderList[1].ExpandedCriteria;
							JoinOperand joinOperand = propertyArgument as JoinOperand;
							AggregateOperand aggregateOperand = propertyArgument as AggregateOperand;
							if(!ReferenceEquals(joinOperand, null) && joinOperand.AggregateType == Aggregate.Single) {
								FunctionOperator aggregatedExpression = new FunctionOperator(result.OperatorType, result.Operands);
								aggregatedExpression.Operands[1] = joinOperand.AggregatedExpression;
								joinOperand.AggregatedExpression = aggregatedExpression;
								return new ExpandedCriteriaHolder(joinOperand);
							}
							else if(!ReferenceEquals(aggregateOperand, null) && aggregateOperand.AggregateType == Aggregate.Single) {
								FunctionOperator aggregatedExpression = new FunctionOperator(result.OperatorType, result.Operands);
								aggregatedExpression.Operands[1] = aggregateOperand.AggregatedExpression;
								aggregateOperand.AggregatedExpression = aggregatedExpression;
								return new ExpandedCriteriaHolder(aggregateOperand);
							}
						}
					}
					break;
				default:
					if(allConstants) {
						op = Evaluate(result);
						return new ExpandedCriteriaHolder(op, true);
					}
					else {
						for(int i = 0; i < result.Operands.Count; i++) {
							result.Operands[i] = ExpandedCriteriaHolder.IfNeededConvertToBoolOperator(result.Operands[i]);
						}
					}
					break;
			}
			return new ExpandedCriteriaHolder(result);
		}
		protected sealed override ExpandedCriteriaHolder Visit(GroupOperator theOperator) {
			bool falseFound = false;
			bool trueFound = false;
			int nonFalses = 0;
			int nonTrues = 0;
			CriteriaOperator result = null;
			string reqPostproc = null;
			foreach(CriteriaOperator op in theOperator.Operands) {
				if(ReferenceEquals(op, null))
					continue;
				ExpandedCriteriaHolder processed = Process(op);
				if(processed.RequiresPostProcessing) {
					reqPostproc = processed.PostProcessingCause;
				}
				if(processed.IsConstant) {
					processed = Process(ExpandedCriteriaHolder.IfNeededConvertToLogicalOperator(processed.ExpandedCriteria));
				}
				if(processed.IsFalse) {
					falseFound = true;
					++nonTrues;
					if(theOperator.OperatorType == GroupOperatorType.And)
						return ExpandedCriteriaHolder.False;
				}
				else {
					++nonFalses;
					if(processed.IsTrue) {
						trueFound = true;
						if(!processed.RequiresPostProcessing && theOperator.OperatorType == GroupOperatorType.Or)
							return ExpandedCriteriaHolder.True;
					}
					else {
						++nonTrues;
						result = GroupOperator.Combine(theOperator.OperatorType, result, ExpandedCriteriaHolder.IfNeededConvertToLogicalOperator(processed.ExpandedCriteria));
					}
				}
			}
			if(falseFound && nonFalses == 0) {
				return ExpandedCriteriaHolder.False;
			}
			if(trueFound && nonTrues == 0 && reqPostproc == null) {
				return ExpandedCriteriaHolder.True;
			}
			if(nonFalses > 1 && reqPostproc != null && theOperator.OperatorType == (underNot ? GroupOperatorType.And : GroupOperatorType.Or)) {
				return ExpandedCriteriaHolder.Indeterminate(reqPostproc);
			}
			return new ExpandedCriteriaHolder(result, reqPostproc);
		}
		protected sealed override ExpandedCriteriaHolder Visit(InOperator theOperator) {
			ExpandedCriteriaHolder leftProcessed = Process(theOperator.LeftOperand);
			if(leftProcessed.RequiresPostProcessing)
				return ExpandedCriteriaHolder.Indeterminate(leftProcessed.PostProcessingCause);
			InOperator result = new InOperator(ExpandedCriteriaHolder.IfNeededConvertToBoolOperator(leftProcessed.ExpandedCriteria));
			CriteriaOperatorCollection operands = theOperator.Operands;
			result.Operands.Capacity = operands.Count;
			string req = null;
			bool allConstants = true;
			foreach(CriteriaOperator op in operands) {
				ExpandedCriteriaHolder processedOp = Process(op);
				if(processedOp.RequiresPostProcessing) {
					req = processedOp.PostProcessingCause;
					if(!underNot)
						return ExpandedCriteriaHolder.Indeterminate(req);
				}
				if(!processedOp.IsConstant) allConstants = false;
				result.Operands.Add(processedOp.ExpandedCriteria);
			}
			if(result.Operands.Count == 0) {
				if(req != null) return ExpandedCriteriaHolder.Indeterminate(req);
				else return ExpandedCriteriaHolder.False;
			}
			if(leftProcessed.IsConstant && allConstants) {
				return ExpandedCriteriaHolder.TryConvertToLogicalConstant(Evaluate(result), true);
			}
			else {
				for(int i = 0; i < result.Operands.Count; i++) {
					result.Operands[i] = ExpandedCriteriaHolder.IfNeededConvertToBoolOperator(result.Operands[i]);
				}
			}
			return new ExpandedCriteriaHolder(result, req);
		}
		protected sealed override ExpandedCriteriaHolder Visit(UnaryOperator theOperator) {
			if(theOperator.OperatorType == UnaryOperatorType.Not) {
				underNot = !underNot;
				try {
					ExpandedCriteriaHolder processedOp = Process(theOperator.Operand);
					if(processedOp.IsTrue) {
						if(processedOp.RequiresPostProcessing)
							return ExpandedCriteriaHolder.Indeterminate(processedOp.PostProcessingCause);
						else
							return ExpandedCriteriaHolder.False;
					}
					else if(processedOp.IsFalse) {
						return ExpandedCriteriaHolder.True;
					}
					else {
						return new ExpandedCriteriaHolder(
							new UnaryOperator(UnaryOperatorType.Not, ExpandedCriteriaHolder.IfNeededConvertToLogicalOperator(processedOp.ExpandedCriteria)),
							processedOp.PostProcessingCause);
					}
				}
				finally {
					underNot = !underNot;
				}
			}
			else {
				ExpandedCriteriaHolder processedOp = Process(theOperator.Operand);
				if(processedOp.RequiresPostProcessing)
					return ExpandedCriteriaHolder.Indeterminate(processedOp.PostProcessingCause);
				UnaryOperator result = new UnaryOperator(theOperator.OperatorType, processedOp.ExpandedCriteria);
				if(processedOp.IsConstant) {
					CriteriaOperator op = Evaluate(result);
					if(theOperator.OperatorType == UnaryOperatorType.IsNull)
						return ExpandedCriteriaHolder.TryConvertToLogicalConstant(op, true);
					return new ExpandedCriteriaHolder(op, true);
				}
				result.Operand = ExpandedCriteriaHolder.IfNeededConvertToBoolOperator(result.Operand);
				return new ExpandedCriteriaHolder(result);
			}
		}
		protected override ExpandedCriteriaHolder Visit(BinaryOperator theOperator) {
			ExpandedCriteriaHolder leftProcessed = Process(theOperator.LeftOperand);
			if(leftProcessed.RequiresPostProcessing)
				return ExpandedCriteriaHolder.Indeterminate(leftProcessed.PostProcessingCause);
			ExpandedCriteriaHolder rightProcessed = Process(theOperator.RightOperand);
			if(rightProcessed.RequiresPostProcessing)
				return ExpandedCriteriaHolder.Indeterminate(rightProcessed.PostProcessingCause);
			if(rightProcessed.IsNullValue || leftProcessed.IsNullValue) {
				if(theOperator.OperatorType == BinaryOperatorType.NotEqual)
					return ExpandedCriteriaHolder.True;
				else
					return ExpandedCriteriaHolder.False;
			}
			BinaryOperator result = new BinaryOperator(leftProcessed.ExpandedCriteria, rightProcessed.ExpandedCriteria, theOperator.OperatorType);
#pragma warning disable CA2208
			if(ReferenceEquals(result.LeftOperand, null))
				throw new ArgumentNullException("result.LeftOperand", Res.GetString(Xpo.Res.Generator_OneOfBinaryOperatorsOperandsIsNull));
			if(ReferenceEquals(result.RightOperand, null))
				throw new ArgumentNullException("result.RightOperand", Xpo.Res.GetString(Xpo.Res.Generator_OneOfBinaryOperatorsOperandsIsNull));
#pragma warning restore CA2208
			if(leftProcessed.IsConstant && rightProcessed.IsConstant) {
				CriteriaOperator op = Evaluate(result);
				switch(theOperator.OperatorType) {
					case BinaryOperatorType.Equal:
					case BinaryOperatorType.Greater:
					case BinaryOperatorType.GreaterOrEqual:
					case BinaryOperatorType.Less:
					case BinaryOperatorType.LessOrEqual:
#pragma warning disable 618
					case BinaryOperatorType.Like:
#pragma warning restore 618
					case BinaryOperatorType.NotEqual:
						return ExpandedCriteriaHolder.TryConvertToLogicalConstant(op, true);
				}
				return new ExpandedCriteriaHolder(op);
			}
			result.LeftOperand = ExpandedCriteriaHolder.IfNeededConvertToBoolOperator(result.LeftOperand);
			result.RightOperand = ExpandedCriteriaHolder.IfNeededConvertToBoolOperator(result.RightOperand);
			return new ExpandedCriteriaHolder(result);
		}
		protected sealed override ExpandedCriteriaHolder Visit(BetweenOperator theOperator) {
			ExpandedCriteriaHolder testProcessed = Process(theOperator.TestExpression);
			if(testProcessed.RequiresPostProcessing)
				return ExpandedCriteriaHolder.Indeterminate(testProcessed.PostProcessingCause);
			ExpandedCriteriaHolder beginProcessed = Process(theOperator.BeginExpression);
			if(beginProcessed.RequiresPostProcessing)
				return ExpandedCriteriaHolder.Indeterminate(beginProcessed.PostProcessingCause);
			ExpandedCriteriaHolder endProcessed = Process(theOperator.EndExpression);
			if(endProcessed.RequiresPostProcessing)
				return ExpandedCriteriaHolder.Indeterminate(endProcessed.PostProcessingCause);
			BetweenOperator result = new BetweenOperator(testProcessed.ExpandedCriteria, beginProcessed.ExpandedCriteria, endProcessed.ExpandedCriteria);
			if(testProcessed.IsConstant && beginProcessed.IsConstant && endProcessed.IsConstant) {
				return ExpandedCriteriaHolder.TryConvertToLogicalConstant(Evaluate(result), true);
			}
			result.BeginExpression = ExpandedCriteriaHolder.IfNeededConvertToBoolOperator(result.BeginExpression);
			result.EndExpression = ExpandedCriteriaHolder.IfNeededConvertToBoolOperator(result.EndExpression);
			result.TestExpression = ExpandedCriteriaHolder.IfNeededConvertToBoolOperator(result.TestExpression);
			return new ExpandedCriteriaHolder(result);
		}
		protected ExpandedCriteriaHolder Process(CriteriaOperator operand) {
			if(ReferenceEquals(operand, null))
				return ExpandedCriteriaHolder.True;
			return operand.Accept(this);
		}
		protected abstract ExpandedCriteriaHolder ProcessInContext(XPClassInfo[] upLevels, CriteriaOperator operand);
		OperandValue Evaluate(CriteriaOperator theOperator) {
			ExpressionEvaluator eval = new ExpressionEvaluator(ConstantDescriptor.Instance, theOperator, caseSensitive, Dictionary.CustomFunctionOperators, Dictionary.CustomAggregates);
			return new OperandValue(eval.Evaluate(theOperator));
		}
	}
	public class ConstantDescriptor : EvaluatorContextDescriptor {
		public override object GetPropertyValue(object source, EvaluatorProperty propertyPath) {
			throw new Exception(Res.GetString(Res.Common_MethodOrOperationNotImplemented));
		}
		public override EvaluatorContext GetNestedContext(object source, string propertyPath) {
			throw new Exception(Res.GetString(Res.Common_MethodOrOperationNotImplemented));
		}
		public override IEnumerable GetCollectionContexts(object source, string collectionName) {
			throw new Exception(Res.GetString(Res.Common_MethodOrOperationNotImplemented));
		}
		public override IEnumerable GetQueryContexts(object source, string queryTypeName, CriteriaOperator condition, int top) {
			throw new Exception(Res.GetString(Res.Common_MethodOrOperationNotImplemented));
		}
		static public EvaluatorContextDescriptor Instance = new ConstantDescriptor();
	}
	public class ExpandedCriteriaHolder {
		public static readonly CriteriaOperator AlwaysFalseCriteria = new ConstantValue(1) == new ConstantValue(2);
		public readonly CriteriaOperator ExpandedCriteria;
		public readonly string PostProcessingCause;
		public readonly bool IsConstant;
		public bool RequiresPostProcessing { get { return PostProcessingCause != null; } }
		public ExpandedCriteriaHolder(CriteriaOperator expandedCriteria) : this(expandedCriteria, null) { }
		public ExpandedCriteriaHolder(CriteriaOperator expandedCriteria, string postProcessingCause) : this(expandedCriteria, postProcessingCause, false) { }
		public ExpandedCriteriaHolder(CriteriaOperator expandedCriteria, bool isConstant) : this(expandedCriteria, null, isConstant) { }
		public ExpandedCriteriaHolder(CriteriaOperator expandedCriteria, string postProcessingCause, bool isConstant) {
			this.ExpandedCriteria = expandedCriteria;
			this.PostProcessingCause = postProcessingCause;
			this.IsConstant = isConstant;
		}
		public bool IsTrue { get { return ReferenceEquals(ExpandedCriteria, null); } }
		public bool IsFalse { get { return Equals(ExpandedCriteria, AlwaysFalseCriteria); } }
		static readonly CriteriaOperator NullOperandValue = new OperandValue(null);
		public bool IsNullValue { get { return Equals(ExpandedCriteria, NullOperandValue); } }
		public static ExpandedCriteriaHolder Indeterminate(string causeProperty) {
			System.Diagnostics.Debug.Assert(causeProperty != null);
			return new ExpandedCriteriaHolder(null, causeProperty);
		}
		public static ExpandedCriteriaHolder Indeterminate(OperandProperty indeterminateProperty) {
			string propName = indeterminateProperty.PropertyName;
			if(propName == null)
				propName = string.Empty;
			return Indeterminate(propName);
		}
		public static ExpandedCriteriaHolder TryConvertToLogicalConstant(CriteriaOperator operand, bool isConstantIfFail) {
			if((operand is OperandValue) && (((OperandValue)operand).Value is bool)) {
				return ((bool)((OperandValue)operand).Value) ? True : False;
			}
			return new ExpandedCriteriaHolder(operand, isConstantIfFail);
		}
		public static readonly ExpandedCriteriaHolder True = new ExpandedCriteriaHolder(null, true);
		public static readonly ExpandedCriteriaHolder False = new ExpandedCriteriaHolder(AlwaysFalseCriteria, true);
		public static CriteriaOperator IfNeededConvertToLogicalOperator(CriteriaOperator operand) {
			if(IsLogicalCriteriaChecker.GetBooleanState(operand) == BooleanCriteriaState.Value) {
				return new BinaryOperator(operand, new ConstantValue(true), BinaryOperatorType.Equal);
			}
			else return operand;
		}
		public static Lazy<CriteriaOperator> IfNeededConvertToLogicalOperator(Lazy<CriteriaOperator> operand) {
			return new Lazy<CriteriaOperator>(() => IfNeededConvertToLogicalOperator(operand.Value));
		}
		public static Lazy<CriteriaOperator> IfNeededConvertToBoolOperator(Lazy<CriteriaOperator> operand) {
			return new Lazy<CriteriaOperator>(() => IfNeededConvertToBoolOperator(operand.Value));
		}
		public static CriteriaOperator IfNeededConvertToBoolOperator(CriteriaOperator operand) {
			if(IsLogicalCriteriaChecker.GetBooleanState(operand) == BooleanCriteriaState.Logical) {
				if(ReferenceEquals(operand, null)) return new ConstantValue(true);
				return new FunctionOperator(FunctionOperatorType.Iif, operand, new ConstantValue(true), new ConstantValue(false));
			}
			else return operand;
		}
		public static ExpandedCriteriaHolder IfNeededConvertToLogicalHolder(ExpandedCriteriaHolder holder) {
			if(IsLogicalCriteriaChecker.GetBooleanState(holder.ExpandedCriteria) == BooleanCriteriaState.Value) return new ExpandedCriteriaHolder(new BinaryOperator(holder.ExpandedCriteria, new ConstantValue(true), BinaryOperatorType.Equal), holder.PostProcessingCause, holder.IsConstant);
			else return holder;
		}
		public static ExpandedCriteriaHolder IfNeededConvertToBoolHolder(ExpandedCriteriaHolder holder) {
			if(IsLogicalCriteriaChecker.GetBooleanState(holder.ExpandedCriteria) == BooleanCriteriaState.Logical) {
				if(holder.IsTrue) return new ExpandedCriteriaHolder(new ConstantValue(true), holder.PostProcessingCause, holder.IsConstant);
				return new ExpandedCriteriaHolder(new FunctionOperator(FunctionOperatorType.Iif, holder.ExpandedCriteria, new ConstantValue(true), new ConstantValue(false)), holder.PostProcessingCause, holder.IsConstant);
			}
			else return holder;
		}
	}
	public enum PersistentCriterionExpanderRequiresPostProcessingAction {
		None, ThrowException, WriteToLog
	}
	public interface IPersistentValueExtractor {
		object ExtractPersistentValue(object criterionValue);
		bool CaseSensitive { get; }
	}
	public class PersistentCriterionExpander : UnknownCriteriaEleminatorBase {
		public static PersistentCriterionExpanderRequiresPostProcessingAction PersistentCriterionExpanderRequiresPostProcessingAction = PersistentCriterionExpanderRequiresPostProcessingAction.None;
		readonly bool DoDetectPostProcessing = true;
		readonly IPersistentValueExtractor PersistentValuesSource;
		readonly Lazy<IFunctionOperatorPatcher> externalFunctionOperatorPatcher;
		int AliasDepthWatchDog;
		protected PersistentCriterionExpander(XPClassInfo[] upLevels, IPersistentValueExtractor persistentValuesSource, int aliasDepthWatchDog, bool doDetectPostProcessing)
			: base(upLevels, persistentValuesSource == null ? XpoDefault.DefaultCaseSensitive : persistentValuesSource.CaseSensitive) {
			this.PersistentValuesSource = persistentValuesSource;
			this.AliasDepthWatchDog = aliasDepthWatchDog;
			this.DoDetectPostProcessing = doDetectPostProcessing;
			externalFunctionOperatorPatcher = new Lazy<IFunctionOperatorPatcher>(FindExternalFunctionOperatorPatcher);
		}
		protected PersistentCriterionExpander(XPClassInfo[] upLevels, IPersistentValueExtractor persistentValuesSource, int aliasDepthWatchDog)
			: this(upLevels, persistentValuesSource, aliasDepthWatchDog, true) {
		}
		protected override ExpandedCriteriaHolder Visit(BinaryOperator theOperator) {
			Type leftType = CriteriaTypeResolver.ResolveType(UpLevelsClassInfos, Dictionary, theOperator.LeftOperand, CriteriaTypeResolveKeyBehavior.AsIs);
			Type rightType = CriteriaTypeResolver.ResolveType(UpLevelsClassInfos, Dictionary, theOperator.RightOperand, CriteriaTypeResolveKeyBehavior.AsIs);
			if(leftType != null && rightType != null && (theOperator.OperatorType == BinaryOperatorType.Equal || theOperator.OperatorType == BinaryOperatorType.NotEqual)) {
				object interfaceObject;
				if(CheckInterfaceEquality(leftType, rightType, theOperator.RightOperand, out interfaceObject)) {
					return base.Visit(new BinaryOperator(theOperator.LeftOperand, new OperandValue(interfaceObject), theOperator.OperatorType));
				}
				if(CheckInterfaceEquality(rightType, leftType, theOperator.LeftOperand, out interfaceObject)) {
					return base.Visit(new BinaryOperator(new OperandValue(interfaceObject), theOperator.RightOperand, theOperator.OperatorType));
				}
			}
			else if(theOperator.OperatorType == BinaryOperatorType.Plus && leftType == typeof(string) && rightType == typeof(string)) {
				return Visit(new FunctionOperator(FunctionOperatorType.Concat, theOperator.LeftOperand, theOperator.RightOperand));
			}
			return base.Visit(theOperator);
		}
		bool CheckInterfaceEquality(Type interfaceType, Type objectType, CriteriaOperator objectCriteria, out object interfaceObject) {
			interfaceObject = null;
			if(objectType.IsInterface || !(objectCriteria is OperandValue)) return false;
			object classObject = ((OperandValue)objectCriteria).Value;
			XPClassInfo interfaceClassInfo = Dictionary.QueryClassInfo(interfaceType);
			XPClassInfo rightClassInfo = Dictionary.QueryClassInfo(classObject);
			if(interfaceClassInfo == null || rightClassInfo == null
				|| interfaceClassInfo.ClassType == null || rightClassInfo.ClassType == null) return false;
			Type interfaceTypeNew = interfaceClassInfo.ClassType;
			Type typePI = null;
			Type typePID = null;
			if(!interfaceTypeNew.IsInterface) {
				Type[] interfaces = interfaceTypeNew.GetInterfaces();
				for(int i = interfaces.Length - 1; i >= 0; i--) {
					Type intrf = interfaces[i];
					if(intrf.IsGenericType && intrf.GetGenericTypeDefinition() == typeof(IPersistentInterfaceData<>)) {
						interfaceTypeNew = intrf.GetGenericArguments()[0];
						typePI = typeof(IPersistentInterface<>).MakeGenericType(interfaceTypeNew);
						typePID = intrf;
						break;
					}
				}
				if(typePI == null) return false;
			}
			else {
				if(interfaceTypeNew.IsGenericType && interfaceTypeNew.GetGenericTypeDefinition() == typeof(IPersistentInterfaceData<>)) {
					interfaceTypeNew = interfaceTypeNew.GetGenericArguments()[0];
				}
				typePI = typeof(IPersistentInterface<>).MakeGenericType(interfaceTypeNew);
				typePID = typeof(IPersistentInterfaceData<>).MakeGenericType(interfaceTypeNew);
			}
			Type[] rightInterfaces = rightClassInfo.ClassType.GetInterfaces();
			for(int i = rightInterfaces.Length - 1; i >= 0; i--) {
				Type intrf = rightInterfaces[i];
				if(intrf == typePID) return false;
				if(intrf == typePI) {
					interfaceObject = PersistentInterfaceTypedHelper.ExtractDataObject(interfaceTypeNew, classObject);
					return true;
				}
			}
			return false;
		}
		static bool IsNullConstant(CriteriaOperator criteria) {
			OperandValue operandValue = criteria as OperandValue;
			return !ReferenceEquals(operandValue, null) && operandValue.Value == null;
		}
		static bool IsSingle(CriteriaOperator criteria) {
			return (criteria is JoinOperand) && ((JoinOperand)criteria).AggregateType == Aggregate.Single || (criteria is AggregateOperand) && ((AggregateOperand)criteria).AggregateType == Aggregate.Single;
		}
		static bool IsIif(CriteriaOperator criteria) {
			return criteria is FunctionOperator && ((FunctionOperator)criteria).OperatorType == FunctionOperatorType.Iif;
		}
		static bool IsTwoArgumentIsNull(CriteriaOperator criteria) {
			FunctionOperator func = criteria as FunctionOperator;
			return !ReferenceEquals(func, null) && func.OperatorType == FunctionOperatorType.IsNull && func.Operands.Count == 2;
		}
		public ExpandedCriteriaHolder ProcessSingleAlias(XPClassInfo[] upLevelsWithReference, CriteriaOperator alias, MemberInfoCollection path, int start) {
			JoinOperand joinOperand = alias as JoinOperand;
			AggregateOperand aggregateOperand = alias as AggregateOperand;
			bool isAggregateOperand = !ReferenceEquals(aggregateOperand, null);
			bool isJoinOperand = !ReferenceEquals(joinOperand, null);
			if((!isAggregateOperand && !isJoinOperand)
				|| (isAggregateOperand && aggregateOperand.AggregateType != Aggregate.Single)
				|| (isJoinOperand && joinOperand.AggregateType != Aggregate.Single)) throw new ArgumentException(null, nameof(alias));
			XPMemberInfo mi = path[start];
			CriteriaOperator expression;
			XPClassInfo[] upLevels = null;
			if(isJoinOperand) {
				upLevels = GetUpLevels(upLevelsWithReference, joinOperand.JoinTypeName);
				expression = joinOperand.AggregatedExpression;
			}
			else {
				upLevels = GetUpLevels(upLevelsWithReference, EvaluatorProperty.Create(aggregateOperand.CollectionProperty));
				expression = aggregateOperand.AggregatedExpression;
			}
			ExpandedCriteriaHolder expanded = MergeAlias(upLevels, path, start, null, expression);
			if(expanded.RequiresPostProcessing)
				return ExpandedCriteriaHolder.Indeterminate(expanded.PostProcessingCause);
			if(isJoinOperand) {
				return new ExpandedCriteriaHolder(new JoinOperand(joinOperand.JoinTypeName, joinOperand.Condition, Aggregate.Single, expanded.ExpandedCriteria));
			}
			return new ExpandedCriteriaHolder(new AggregateOperand(aggregateOperand.CollectionProperty, expanded.ExpandedCriteria, Aggregate.Single, aggregateOperand.Condition));
		}
		public ExpandedCriteriaHolder ProcessIifAlias(XPClassInfo[] upLevelsWithReference, CriteriaOperator expression, MemberInfoCollection path, int start) {
			FunctionOperator func = expression as FunctionOperator;
			if(ReferenceEquals(func, null) || func.OperatorType != FunctionOperatorType.Iif)
				throw new ArgumentException("alias");
			XPMemberInfo mi = path[start];
			if(func.Operands.Count == 3) {
				CriteriaOperator leftOperand = func.Operands[1];
				CriteriaOperator rightOperand = func.Operands[2];
				ExpandedCriteriaHolder leftExpanded = MergeAlias(upLevelsWithReference, path, start, mi, leftOperand);
				if(leftExpanded.RequiresPostProcessing)
					return ExpandedCriteriaHolder.Indeterminate(leftExpanded.PostProcessingCause);
				ExpandedCriteriaHolder rightExpanded = MergeAlias(upLevelsWithReference, path, start, mi, rightOperand);
				if(leftExpanded.RequiresPostProcessing)
					return ExpandedCriteriaHolder.Indeterminate(leftExpanded.PostProcessingCause);
				return new ExpandedCriteriaHolder(new FunctionOperator(FunctionOperatorType.Iif, func.Operands[0], leftExpanded.ExpandedCriteria, rightExpanded.ExpandedCriteria));
			}
			CriteriaOperatorCollection operands = new CriteriaOperatorCollection();
			int operandCount = func.Operands.Count;
			for(int i = 0; i < operandCount; i++) {
				CriteriaOperator operand = func.Operands[i];
				if((i % 2) == 1 || i == (operandCount - 1)) {
					ExpandedCriteriaHolder expanded = MergeAlias(upLevelsWithReference, path, start, mi, operand);
					if(expanded.RequiresPostProcessing)
						return ExpandedCriteriaHolder.Indeterminate(expanded.PostProcessingCause);
					operands.Add(expanded.ExpandedCriteria);
					continue;
				}
				operands.Add(operand);
			}
			return new ExpandedCriteriaHolder(new FunctionOperator(FunctionOperatorType.Iif, operands));
		}
		public ExpandedCriteriaHolder ProcessTwoArgumentIsNullAlias(XPClassInfo[] upLevelsWithReference, CriteriaOperator expression, MemberInfoCollection path, int start) {
			FunctionOperator func = expression as FunctionOperator;
			if(ReferenceEquals(func, null) || func.OperatorType != FunctionOperatorType.IsNull || func.Operands.Count != 2)
				throw new ArgumentException("alias");
			XPMemberInfo mi = path[start];
			CriteriaOperator leftOperand = func.Operands[0];
			CriteriaOperator rightOperand = func.Operands[1];
			ExpandedCriteriaHolder leftExpanded = MergeAlias(upLevelsWithReference, path, start, mi, leftOperand);
			if(leftExpanded.RequiresPostProcessing)
				return ExpandedCriteriaHolder.Indeterminate(leftExpanded.PostProcessingCause);
			ExpandedCriteriaHolder rightExpanded = MergeAlias(upLevelsWithReference, path, start, mi, rightOperand);
			if(leftExpanded.RequiresPostProcessing)
				return ExpandedCriteriaHolder.Indeterminate(leftExpanded.PostProcessingCause);
			return new ExpandedCriteriaHolder(new FunctionOperator(FunctionOperatorType.IsNull, leftExpanded.ExpandedCriteria, rightExpanded.ExpandedCriteria));
		}
		ExpandedCriteriaHolder MergeAlias(XPClassInfo[] upLevelsWithReference, MemberInfoCollection path, int start, XPMemberInfo mi, CriteriaOperator expression) {
			OperandProperty aliasAsProperty = expression as OperandProperty;
			if(start < path.Count - 1 || !ReferenceEquals(aliasAsProperty, null)) {
				if(IsSingle(expression)) {
					ExpandedCriteriaHolder expanded = ProcessSingleAlias(upLevelsWithReference, expression, path, start);
					if(expanded.RequiresPostProcessing)
						return ExpandedCriteriaHolder.Indeterminate(expanded.PostProcessingCause);
					expression = expanded.ExpandedCriteria;
				}
				else if(IsIif(expression)) {
					ExpandedCriteriaHolder expanded = ProcessIifAlias(upLevelsWithReference, expression, path, start);
					if(expanded.RequiresPostProcessing)
						return ExpandedCriteriaHolder.Indeterminate(expanded.PostProcessingCause);
					expression = expanded.ExpandedCriteria;
				}
				else if(IsTwoArgumentIsNull(expression)) {
					ExpandedCriteriaHolder expanded = ProcessTwoArgumentIsNullAlias(upLevelsWithReference, expression, path, start);
					if(expanded.RequiresPostProcessing)
						return ExpandedCriteriaHolder.Indeterminate(expanded.PostProcessingCause);
					expression = expanded.ExpandedCriteria;
				}
				else if(IsNullConstant(expression)) {
					return new ExpandedCriteriaHolder(expression, true);
				}
				else {
					if(ReferenceEquals(aliasAsProperty, null))
						throw new ArgumentException(Res.GetString(Res.PersistentAliasExpander_ReferenceOrCollectionExpectedInTheMiddleOfThePath, mi.Name));
					MemberInfoCollection aliasedProperty = new MemberInfoCollection(upLevelsWithReference[0]);
					aliasedProperty.AddRange((mi == null ? upLevelsWithReference[0] : mi.Owner).ParsePath(aliasAsProperty.PropertyName));
					for(int j = start + 1; j < path.Count; ++j) {
						XPClassInfo cci = null;
						int lastPropIndex = aliasedProperty.Count - 1;
						XPMemberInfo prop = aliasedProperty[lastPropIndex];
						while(EvaluatorProperty.GetIsThisProperty(prop.Name)) {
							aliasedProperty.RemoveAt(lastPropIndex);
							if(lastPropIndex == 0) {
								cci = upLevelsWithReference[0];
								break;
							}
							if(lastPropIndex >= aliasedProperty.Count)
								lastPropIndex = aliasedProperty.Count - 1;
							prop = aliasedProperty[lastPropIndex];
						}
						if(cci == null) cci = prop.ReferenceType;
						XPMemberInfo pathj = path[j];
						if(pathj.Owner.IsAssignableTo(cci) || cci.IsAssignableTo(pathj.Owner)) {
							aliasedProperty.Add(path[j]);
						}
						else {
							aliasedProperty.Add(cci.GetMember(pathj.Name));
						}
					}
					expression = new OperandProperty(aliasedProperty.ToString());
				}
			}
			return new ExpandedCriteriaHolder(expression);
		}
		protected override ExpandedCriteriaHolder Visit(OperandProperty theOriginalOperand) {
			bool addKeyTail;
			OperandProperty theOperand = FixPropertyExclamation(theOriginalOperand, out addKeyTail);
			EvaluatorProperty prop = EvaluatorProperty.Create(theOperand);
			if(DoDetectPostProcessing && EvaluatorProperty.GetIsThisProperty(prop.PropertyPath)) {
				string patchedPropertyName = string.Empty;
				for(int i = 0; i < prop.UpDepth; ++i) {
					patchedPropertyName += "^.";
				}
				UpLevelsClassInfos[prop.UpDepth].CheckAbstractReference();
				patchedPropertyName += UpLevelsClassInfos[prop.UpDepth].KeyProperty.Name;
				prop = EvaluatorProperty.Create(new OperandProperty(patchedPropertyName));
			}
			MemberInfoCollection res = new MemberInfoCollection(UpLevelsClassInfos[prop.UpDepth]);
			for(int i = 0; i < prop.UpDepth; ++i) {
				res.Add(null);
			}
			XPClassInfo currentClassInfo = UpLevelsClassInfos[prop.UpDepth];
			MemberInfoCollection path = currentClassInfo.ParsePath(prop.PropertyPath);
			if(addKeyTail && path.Count > 0 && path[0].ReferenceType != null) {
				path = currentClassInfo.ParsePath(string.Concat(prop.PropertyPath, ".", path[0].ReferenceType.KeyProperty.Name));
			}
			List<XPClassInfo> upLevelList = new List<XPClassInfo>(UpLevelsClassInfos.Length - prop.UpDepth);
			for(int i = prop.UpDepth; i < UpLevelsClassInfos.Length; i++) {
				upLevelList.Add(UpLevelsClassInfos[i]);
			}
			for(int i = 0; i < path.Count; ++i) {
				XPMemberInfo mi = path[i];
				if(mi.IsAliased) {
					CriteriaOperator alias = ((PersistentAliasAttribute)mi.GetAttributeInfo(typeof(PersistentAliasAttribute))).Criteria;
					if(AliasDepthWatchDog >= 128)   
						throw new InvalidOperationException(Res.GetString(Res.Metadata_PersistentAliasCircular, mi.Owner.FullName, mi.Name, alias));
					ExpandedCriteriaHolder expanded = MergeAlias(upLevelList.ToArray(), path, i, mi, alias);
					if(expanded.RequiresPostProcessing)
						return ExpandedCriteriaHolder.Indeterminate(expanded.PostProcessingCause);
					alias = expanded.ExpandedCriteria;
					XPClassInfo[] upLevels = GetUpLevels(new OperandProperty(res.ToString()));
					if(!upLevels[0].IsAssignableTo(mi.Owner) && mi.Owner.IsAssignableTo(upLevels[0])) {
						string upCastPrefix = string.Concat('<', mi.Owner.FullName, '>');
						alias = TopLevelPropertiesPrefixer.PatchAliasPrefix(upCastPrefix, alias);
					}
					expanded = ProcessInContext(upLevels, alias);
					if(expanded.RequiresPostProcessing)
						return ExpandedCriteriaHolder.Indeterminate(expanded.PostProcessingCause);
					CriteriaOperator prefixed;
					if(res.Count > 0)
						prefixed = TopLevelPropertiesPrefixer.PatchAliasPrefix(res.ToString() + '.', expanded.ExpandedCriteria);
					else
						prefixed = expanded.ExpandedCriteria;
					return new ExpandedCriteriaHolder(prefixed, expanded.PostProcessingCause);
				}
				else if(IsValidForPersistentCriterion(mi)) {
					res.Add(mi);
					currentClassInfo = mi.ReferenceType;
					upLevelList.Insert(0, currentClassInfo);
				}
				else if(mi.IsAssociationList && i == path.Count - 1) {
					res.Add(mi);
					currentClassInfo = null;
				}
				else if(i == path.Count - 1 && mi.HasAttribute(typeof(ManyToManyAliasAttribute))) {
					return new ExpandedCriteriaHolder(new ManyToManyProperty(res, mi));
				}
				else {
					if(DoDetectPostProcessing)
						return ExpandedCriteriaHolder.Indeterminate(theOriginalOperand);
					else {
						res.Add(mi);
						if(mi.Name != "This") {
							currentClassInfo = mi.ReferenceType;
						}
					}
				}
			}
			return new ExpandedCriteriaHolder(new OperandProperty(res.ToString()));
		}
		internal static OperandProperty FixPropertyExclamation(OperandProperty theOriginalOperand, out bool addKeyTail) {
			OperandProperty theOperand = theOriginalOperand;
			addKeyTail = false;
			if(theOperand.PropertyName.EndsWith(XPPropertyDescriptor.ReferenceAsKeyTail, StringComparison.Ordinal)) {
				theOperand = new OperandProperty(theOperand.PropertyName.Substring(0, theOperand.PropertyName.Length - XPPropertyDescriptor.ReferenceAsKeyTail.Length));
				addKeyTail = true;
			}
			else if(theOperand.PropertyName.EndsWith(XPPropertyDescriptor.ReferenceAsObjectTail, StringComparison.Ordinal)) {
				theOperand = new OperandProperty(theOperand.PropertyName.Substring(0, theOperand.PropertyName.Length - XPPropertyDescriptor.ReferenceAsObjectTail.Length));
			}
			return theOperand;
		}
		class ManyToManyProperty : CriteriaOperator {
			public readonly MemberInfoCollection PathToProperty;
			public readonly XPMemberInfo ManyToManyAlias;
			public ManyToManyProperty(MemberInfoCollection pathToProperty, XPMemberInfo manyToManyAlias) {
				this.PathToProperty = pathToProperty;
				this.ManyToManyAlias = manyToManyAlias;
			}
			public override void Accept(ICriteriaVisitor visitor) {
				throw new NotSupportedException();
			}
			public override T Accept<T>(ICriteriaVisitor<T> visitor) {
				throw new NotSupportedException();
			}
			protected override CriteriaOperator CloneCommon() {
				throw new NotSupportedException();
			}
		}
		protected override ExpandedCriteriaHolder Visit(AggregateOperand theOperand) {
			ExpandedCriteriaHolder collectionResult = Process(theOperand.CollectionProperty);
			ManyToManyProperty mmp = collectionResult.ExpandedCriteria as ManyToManyProperty;
			if(!ReferenceEquals(mmp, null)) {
				ManyToManyAliasAttribute mma = (ManyToManyAliasAttribute)mmp.ManyToManyAlias.GetAttributeInfo(typeof(ManyToManyAliasAttribute));
				XPMemberInfo oneToManyCollection = mmp.ManyToManyAlias.Owner.GetMember(mma.OneToManyCollectionName);
				if(!oneToManyCollection.IsAssociationList
					|| oneToManyCollection.IsManyToMany
					) {
					throw new PropertyMissingException(mmp.ManyToManyAlias.Owner.FullName, mma.OneToManyCollectionName);
				}
				mmp.PathToProperty.Add(oneToManyCollection);
				XPMemberInfo intermediateReferenceMember = oneToManyCollection.CollectionElementType.GetMember(mma.ReferenceInTheIntermediateTableName);
				if(intermediateReferenceMember.ReferenceType == null) {
					throw new PropertyMissingException(oneToManyCollection.CollectionElementType.FullName, mma.ReferenceInTheIntermediateTableName);
				}
				OperandProperty processedCollectionProperty = new OperandProperty(mmp.PathToProperty.ToString());
				XPClassInfo[] nestedUpLevels = GetUpLevels(processedCollectionProperty);
				nestedUpLevels[0] = intermediateReferenceMember.ReferenceType;
				ExpandedCriteriaHolder processedConditionHolder = ProcessInContext(nestedUpLevels, theOperand.Condition);
				if(processedConditionHolder.RequiresPostProcessing)
					return ExpandedCriteriaHolder.Indeterminate(processedConditionHolder.PostProcessingCause);
				if(processedConditionHolder.IsFalse)
					return GetFalseConditionAggregate(theOperand.AggregateType, theOperand.CustomAggregateName);
				CriteriaOperator processedCondition = ExpandedCriteriaHolder.IfNeededConvertToLogicalOperator(ManyToManyPrefixer.Alias(mma.ReferenceInTheIntermediateTableName, processedConditionHolder.ExpandedCriteria));
				CriteriaOperator persistentOperator;
				if(theOperand.AggregateType != Aggregate.Custom) {
					ExpandedCriteriaHolder processedAggregatedExpressionHolder = ProcessInContext(nestedUpLevels, theOperand.AggregatedExpression);
					if(processedAggregatedExpressionHolder.RequiresPostProcessing)
						return ExpandedCriteriaHolder.Indeterminate(processedAggregatedExpressionHolder.PostProcessingCause);
					CriteriaOperator processedAggregatedExpression = ManyToManyPrefixer.Alias(mma.ReferenceInTheIntermediateTableName, processedAggregatedExpressionHolder.ExpandedCriteria);
					persistentOperator = new AggregateOperand(processedCollectionProperty,
						ReferenceEquals(null, processedAggregatedExpression) ? null : ExpandedCriteriaHolder.IfNeededConvertToBoolOperator(processedAggregatedExpression),
						theOperand.AggregateType, processedCondition);
				}
				else {
					CriteriaOperator[] processedAggrExprs = new CriteriaOperator[theOperand.CustomAggregateOperands.Count];
					for(int i = 0; i < theOperand.CustomAggregateOperands.Count; i++) {
						ExpandedCriteriaHolder processedAggregatedExpressionHolder = ProcessInContext(nestedUpLevels, theOperand.CustomAggregateOperands[i]);
						if(processedAggregatedExpressionHolder.RequiresPostProcessing)
							return ExpandedCriteriaHolder.Indeterminate(processedAggregatedExpressionHolder.PostProcessingCause);
						CriteriaOperator processedAggregatedExpression = ManyToManyPrefixer.Alias(mma.ReferenceInTheIntermediateTableName, processedAggregatedExpressionHolder.ExpandedCriteria);
						processedAggrExprs[i] = ReferenceEquals(null, processedAggregatedExpression) ? null : ExpandedCriteriaHolder.IfNeededConvertToBoolOperator(processedAggregatedExpression);
					}
					persistentOperator = new AggregateOperand(processedCollectionProperty, processedAggrExprs, theOperand.CustomAggregateName, processedCondition);
				}
				return new ExpandedCriteriaHolder(persistentOperator);
			}
			return base.Visit(theOperand);
		}
		class ManyToManyPrefixer : ClientCriteriaLazyPatcherBase {
			protected readonly string Prefix;
			protected readonly int Depth;
			protected readonly int JoinDepth;
			public ManyToManyPrefixer(string prefix, int depth, int joinDepth) {
				this.Prefix = prefix;
				this.Depth = depth;
				this.JoinDepth = joinDepth;
			}
			string ProcessPropertyNameCore(string propertyName, int depth) {
				if(propertyName.StartsWith("^.")) {
					if(depth > 0) {
						string[] path = MemberInfoCollection.SplitPath(propertyName);
						int cnt = 0;
						while(cnt < path.Length && path[cnt] == "^")
							++cnt;
						if(cnt > depth)
							return "^." + propertyName;
					}
				}
				else {
					if(depth == 0) {
						if(Prefix.EndsWith('>'))
							return Prefix + propertyName;
						return string.Concat(Prefix, '.', propertyName);
					}
				}
				return propertyName;
			}
			public override CriteriaOperator Visit(OperandProperty theOperand) {
				if(theOperand.PropertyName != null && theOperand.PropertyName.Length > 0) {
					string propertyName = theOperand.PropertyName;
					string joinPrefix = null;
					int joinCnt = 0;
					if(JoinDepth > 0) {
						while(joinCnt < JoinDepth && propertyName.StartsWith("^.")) {
							joinPrefix = string.IsNullOrEmpty(joinPrefix) ? "^." : joinPrefix + "^.";
							propertyName = propertyName.Substring(2);
							joinCnt++;
						}
					}
					string processedPropertyName = ProcessPropertyNameCore(propertyName, Depth - joinCnt);
					if(propertyName != processedPropertyName || !string.IsNullOrEmpty(joinPrefix)) {
						return new OperandProperty(string.IsNullOrEmpty(joinPrefix) ? processedPropertyName : joinPrefix + processedPropertyName);
					}
				}
				return theOperand;
			}
			public override CriteriaOperator Visit(AggregateOperand theOperand) {
				if(theOperand.IsTopLevel) {
					if(theOperand.AggregateType != Aggregate.Custom) {
						return NewIfDifferent(theOperand, (OperandProperty)Process(theOperand.CollectionProperty), Process(theOperand.AggregatedExpression), Process(theOperand.Condition));
					}
					return NewIfDifferent(theOperand, (OperandProperty)Process(theOperand.CollectionProperty), Process(theOperand.CustomAggregateOperands), Process(theOperand.Condition));
				}
				else {
					string[] path = MemberInfoCollection.SplitPath(theOperand.CollectionProperty.PropertyName);
					int cnt = 0;
					while(cnt < path.Length && path[cnt] == "^")
						++cnt;
					OperandProperty processedColectionProperty = (OperandProperty)Process(theOperand.CollectionProperty);
					if(cnt > Depth) {
						if(theOperand.AggregateType != Aggregate.Custom) {
							return NewIfDifferent(theOperand, processedColectionProperty, theOperand.AggregatedExpression, theOperand.Condition);
						}
						return NewIfDifferent(theOperand, processedColectionProperty, theOperand.CustomAggregateOperands, theOperand.Condition);
					}
					else {
						ManyToManyPrefixer nestedPrefixer = new ManyToManyPrefixer(Prefix, Depth - cnt + (path.Length - cnt), JoinDepth);
						CriteriaOperator prefixedCond = nestedPrefixer.Process(theOperand.Condition);
						if(theOperand.AggregateType != Aggregate.Custom) {
							CriteriaOperator prefixedAgg = nestedPrefixer.Process(theOperand.AggregatedExpression);
							return NewIfDifferent(theOperand, processedColectionProperty, prefixedAgg, prefixedCond);
						}
						else {
							CriteriaOperator[] prefixedArgs = new CriteriaOperator[theOperand.CustomAggregateOperands.Count];
							for(int i = 0; i < prefixedArgs.Length; i++) {
								prefixedArgs[i] = nestedPrefixer.Process(theOperand.CustomAggregateOperands[i]);
							}
							return NewIfDifferent(theOperand, processedColectionProperty, prefixedArgs, prefixedCond);
						}
					}
				}
			}
			public override CriteriaOperator Visit(JoinOperand theOperand) {
				ManyToManyPrefixer nestedPrefixer = new ManyToManyPrefixer(Prefix, Depth + 1, JoinDepth + 1);
				CriteriaOperator prefixedCond = nestedPrefixer.Process(theOperand.Condition);
				if(theOperand.AggregateType != Aggregate.Custom) {
					CriteriaOperator prefixedAgg = nestedPrefixer.Process(theOperand.AggregatedExpression);
					return NewIfDifferent(theOperand, prefixedCond, prefixedAgg);
				}
				else {
					CriteriaOperator[] prefixedArgs = new CriteriaOperator[theOperand.CustomAggregateOperands.Count];
					for(int i = 0; i < prefixedArgs.Length; i++) {
						prefixedArgs[i] = nestedPrefixer.Process(theOperand.CustomAggregateOperands[i]);
					}
					return NewIfDifferent(theOperand, prefixedCond, prefixedArgs);
				}
			}
			public static CriteriaOperator Alias(string p, CriteriaOperator criteriaOperator) {
				return new ManyToManyPrefixer(p, 0, 0).Process(criteriaOperator);
			}
		}
		public static CriteriaOperator Prefix(string p, CriteriaOperator criteriaOperator) {
			return ManyToManyPrefixer.Alias(p, criteriaOperator);
		}
		protected virtual bool IsValidForPersistentCriterion(XPMemberInfo mi) {
			return mi.IsPersistent || mi.IsAssociationList;
		}
		protected override ExpandedCriteriaHolder Visit(OperandValue theOperand) {
			object extracted = DoDetectPostProcessing ? PersistentValuesSource.ExtractPersistentValue(theOperand.Value) : theOperand.Value;
			OperandValue value;
			if(extracted == theOperand.Value)
				value = theOperand;
			else
				value = theOperand is ConstantValue ? new ConstantValue(extracted) : new OperandValue(extracted);
			return new ExpandedCriteriaHolder(value, true);
		}
		protected override ExpandedCriteriaHolder ProcessInContext(XPClassInfo[] upLevels, CriteriaOperator operand) {
			return Expand(upLevels, PersistentValuesSource, operand, AliasDepthWatchDog + 1, DoDetectPostProcessing);
		}
		protected static ExpandedCriteriaHolder Expand(XPClassInfo[] upLevels, IPersistentValueExtractor persistentValuesSource, CriteriaOperator op, int aliasDepthWatchDog, bool doDetectPostProcessing) {
			return new PersistentCriterionExpander(upLevels, persistentValuesSource, aliasDepthWatchDog, doDetectPostProcessing).Process(op);
		}
		protected static ExpandedCriteriaHolder Expand(XPClassInfo[] upLevels, IPersistentValueExtractor persistentValuesSource, CriteriaOperator op, bool doDetectPostProcessing) {
			ExpandedCriteriaHolder result = Expand(upLevels, persistentValuesSource, op, 0, doDetectPostProcessing);
			if(result.RequiresPostProcessing && PersistentCriterionExpanderRequiresPostProcessingAction != PersistentCriterionExpanderRequiresPostProcessingAction.None) {
				string classInfos = string.Empty;
				foreach(XPClassInfo ci in upLevels) {
					if(classInfos.Length > 0)
						classInfos += ",";
					classInfos += ci.FullName;
				}
				string message = string.Format(null, "Expanding '{0}' criterion for '{1}' classInfo(s) postprocessing was requested because it uses a nonpersistent (and/or modified for NestedUnitOfWork) property '{2}'", op, classInfos, result.PostProcessingCause);
				switch(PersistentCriterionExpanderRequiresPostProcessingAction) {
					case PersistentCriterionExpanderRequiresPostProcessingAction.ThrowException:
						throw new ArgumentException(message);
					case PersistentCriterionExpanderRequiresPostProcessingAction.WriteToLog:
						System.Diagnostics.Trace.WriteLineIf(new System.Diagnostics.TraceSwitch(null, "XPO", "").TraceInfo, message);
						break;
				}
				return result;
			}
			return new ExpandedCriteriaHolder(JoinOperandExpander.Expand(upLevels, result.ExpandedCriteria), result.PostProcessingCause, result.IsConstant);
		}
		public static ExpandedCriteriaHolder ExpandToLogical(XPClassInfo ci, IPersistentValueExtractor persistentValuesSource, CriteriaOperator op, bool doDetectPostProcessing) {
			return ExpandedCriteriaHolder.IfNeededConvertToLogicalHolder(Expand(ci, persistentValuesSource, op, doDetectPostProcessing));
		}
		public static ExpandedCriteriaHolder ExpandToLogical(IPersistentValueExtractor persistentValuesSource, XPClassInfo ci, CriteriaOperator op, bool doDetectPostProcessing) {
			return ExpandedCriteriaHolder.IfNeededConvertToLogicalHolder(Expand(persistentValuesSource, ci, op, doDetectPostProcessing));
		}
		public static ExpandedCriteriaHolder ExpandToValue(XPClassInfo ci, IPersistentValueExtractor persistentValuesSource, CriteriaOperator op, bool doDetectPostProcessing) {
			return ExpandedCriteriaHolder.IfNeededConvertToBoolHolder(Expand(ci, persistentValuesSource, op, doDetectPostProcessing));
		}
		public static ExpandedCriteriaHolder ExpandToValue(IPersistentValueExtractor persistentValuesSource, XPClassInfo ci, CriteriaOperator op, bool doDetectPostProcessing) {
			return ExpandedCriteriaHolder.IfNeededConvertToBoolHolder(Expand(persistentValuesSource, ci, op, doDetectPostProcessing));
		}
		public static ExpandedCriteriaHolder Expand(XPClassInfo ci, IPersistentValueExtractor persistentValuesSource, CriteriaOperator op) {
			return Expand(ci, persistentValuesSource, op, true);
		}
		public static ExpandedCriteriaHolder Expand(XPClassInfo ci, IPersistentValueExtractor persistentValuesSource, CriteriaOperator op, bool doDetectPostProcessing) {
			Guard.ArgumentNotNull(ci, nameof(ci));
			return Expand(new XPClassInfo[] { ci }, persistentValuesSource, op, doDetectPostProcessing);
		}
		public static ExpandedCriteriaHolder Expand(IPersistentValueExtractor persistentValuesSource, XPClassInfo ci, CriteriaOperator op) {
			return Expand(persistentValuesSource, ci, op, true);
		}
		public static ExpandedCriteriaHolder Expand(IPersistentValueExtractor persistentValuesSource, XPClassInfo ci, CriteriaOperator op, bool doDetectPostProcessing) {
			return Expand(ci, persistentValuesSource, op, doDetectPostProcessing);
		}
		protected override ExpandedCriteriaHolder Visit(FunctionOperator theOperator) {
			if(EvalHelpers.IsLocalDateTime(theOperator.OperatorType)) {
				return Process(new OperandValue(EvalHelpers.EvaluateLocalDateTime(theOperator.OperatorType)));
			}
			if(EvalHelpers.IsExpandableLogicalIntrinsicFunction(theOperator.OperatorType)) {
				return Process(EvalHelpers.ExpandLogicalIntrinsicFunction(theOperator));
			}
			if(theOperator.OperatorType == FunctionOperatorType.Custom || theOperator.OperatorType == FunctionOperatorType.CustomNonDeterministic) {
				if(externalFunctionOperatorPatcher.Value != null) {
					var processedOperands = new CriteriaOperator[theOperator.Operands.Count];
					string postProcessingClause = null;
					bool isOperandsChanged = false;
					for(int i = 0; i < theOperator.Operands.Count; i++) {
						CriteriaOperator operand = theOperator.Operands[i];
						ExpandedCriteriaHolder processed = Process(operand);
						processedOperands[i] = processed.ExpandedCriteria;
						isOperandsChanged |= !ReferenceEquals(processed.ExpandedCriteria, operand);
						if(processed.PostProcessingCause != null) {
							postProcessingClause = processed.PostProcessingCause;
						}
					}
					var processedOperator = !isOperandsChanged ? theOperator : new FunctionOperator(theOperator.OperatorType, processedOperands);
					var expression = externalFunctionOperatorPatcher.Value.Patch(processedOperator);
					if(!ReferenceEquals(expression, null) && !ReferenceEquals(expression, theOperator)) {
						if(expression is FunctionOperator) {
							return base.Visit((FunctionOperator)expression);
						}
						else {
							bool isConstant = expression is OperandValue;
							return new ExpandedCriteriaHolder(expression, postProcessingClause, isConstant);
						}
					}
				}
			}
			return base.Visit(theOperator);
		}
		IFunctionOperatorPatcher FindExternalFunctionOperatorPatcher() {
			var internalServiceProvider = PersistentValuesSource as IInfrastructure<IServiceProvider>;
			if(internalServiceProvider != null) {
				return internalServiceProvider.Instance?.GetService(typeof(IFunctionOperatorPatcher)) as IFunctionOperatorPatcher;
			}
			return null;
		}
	}
	class TopLevelPropertiesPrefixer : ClientCriteriaVisitorBase {
		protected readonly string Prefix;
		int inAggregateOperand = 0;
		int patchedLevels = 0;
		protected override CriteriaOperator Visit(OperandProperty theOperand) {
			bool prefixIsRef = !string.IsNullOrEmpty(Prefix) && Prefix[Prefix.Length - 1] == '.';
			if(inAggregateOperand == 0) {
				if(prefixIsRef && EvaluatorProperty.GetIsThisProperty(theOperand.PropertyName)) {
					return new OperandProperty(Prefix.Remove(Prefix.Length - 1));
				}
				return new OperandProperty(Prefix + theOperand.PropertyName);
			}
			int upLevelsPrefixCount = 0;
			string propertyName = theOperand.PropertyName;
			while(propertyName.StartsWith("^.")) {
				upLevelsPrefixCount++;
				propertyName = propertyName.Substring(2);
			}
			var patchedUpLevelsPrefixCount = upLevelsPrefixCount + patchedLevels;
			if(inAggregateOperand == patchedUpLevelsPrefixCount) {
				StringBuilder sb = new StringBuilder();
				for(int i = 0; i < patchedUpLevelsPrefixCount; i++) {
					sb.Append("^.");
				}
				if(prefixIsRef && EvaluatorProperty.GetIsThisProperty(propertyName)) {
					sb.Append(Prefix, 0, Prefix.Length - 1);
				}
				else {
					sb.Append(Prefix);
					sb.Append(propertyName);
				}
				return new OperandProperty(sb.ToString());
			}
			return theOperand;
		}
		protected override CriteriaOperator Visit(AggregateOperand theOperand, bool processCollectionProperty) {
			OperandProperty collectionProperty = (OperandProperty)Process(theOperand.CollectionProperty);
			int levelsCountBeforePatch = MemberInfoCollection.GetSplitPartsCount(theOperand.CollectionProperty.PropertyName);
			int levelsCount = MemberInfoCollection.GetSplitPartsCount(collectionProperty.PropertyName);
			CriteriaOperator condition;
			inAggregateOperand += levelsCount;
			patchedLevels += levelsCount - levelsCountBeforePatch;
			if(theOperand.AggregateType != Aggregate.Custom) {
				CriteriaOperator expression;
				try {
					condition = Process(theOperand.Condition);
					expression = Process(theOperand.AggregatedExpression);
				}
				finally {
					patchedLevels -= levelsCount - levelsCountBeforePatch;
					inAggregateOperand -= levelsCount;
				}
				if(ReferenceEquals(collectionProperty, theOperand.CollectionProperty)
					&& ReferenceEquals(condition, theOperand.Condition)
					&& ReferenceEquals(expression, theOperand.AggregatedExpression)) {
					return theOperand;
				}
				return new AggregateOperand(collectionProperty, expression, theOperand.AggregateType, condition);
			}
			else {
				bool modified;
				CriteriaOperatorCollection aggrExprs;
				try {
					condition = Process(theOperand.Condition);
					aggrExprs = ProcessCollection(theOperand.CustomAggregateOperands, out modified);
				}
				finally {
					patchedLevels -= levelsCount - levelsCountBeforePatch;
					inAggregateOperand -= levelsCount;
				}
				if(ReferenceEquals(collectionProperty, theOperand.CollectionProperty)
					&& ReferenceEquals(condition, theOperand.Condition)
					&& !modified) {
					return theOperand;
				}
				return new AggregateOperand(collectionProperty, aggrExprs, theOperand.CustomAggregateName, condition);
			}
		}
		protected override CriteriaOperator Visit(JoinOperand theOperand) {
			inAggregateOperand++;
			try {
				return base.Visit(theOperand);
			}
			finally {
				inAggregateOperand--;
			}
		}
		TopLevelPropertiesPrefixer(string prefix) {
			this.Prefix = prefix;
		}
		static CriteriaOperator GetPrefixed(string prefix, CriteriaOperator criteriaOperator) {
			return new TopLevelPropertiesPrefixer(prefix).Process(criteriaOperator);
		}
		public static CriteriaOperator PatchAliasPrefix(string persistentPrefix, CriteriaOperator criteriaOperator) {
			return GetPrefixed(persistentPrefix, criteriaOperator);
		}
	}
	public abstract class ContextClientCriteriaVisitorBase<T> : IClientCriteriaVisitor<T> {
		ContextState currentState;
		bool hasJoinOperand;
		int hasJoinOperandOnStack;
		Stack<ContextState> stateStack = new Stack<ContextState>();
		protected XPClassInfo CurrentClassInfo {
			get { return currentState.ClassInfo; }
		}
		protected int InAtomOperation {
			get { return currentState.InAtomOperator; }
		}
		protected bool IsInJoinOperand {
			get { return currentState.IsInJoinOperand; }
		}
		protected bool HasJoinOperandOnStack {
			get { return hasJoinOperandOnStack > 0; }
		}
		public bool HasJoinOperand {
			get { return hasJoinOperand; }
		}
		public ContextClientCriteriaVisitorBase(XPClassInfo currentClassInfo) {
			this.currentState = new ContextState(currentClassInfo);
		}
		public ContextClientCriteriaVisitorBase(XPClassInfo[] upLevelsClassInfo) {
			this.currentState = new ContextState(upLevelsClassInfo[0]);
			for(int i = upLevelsClassInfo.Length - 1; i >= 1; i--) {
				stateStack.Push(new ContextState(upLevelsClassInfo[i]));
			}
		}
		public abstract T Process(CriteriaOperator criteria);
		public List<T> ProcessCollection(CriteriaOperatorCollection operands) {
			List<T> result = new List<T>();
			foreach(CriteriaOperator operand in operands) {
				result.Add(Process(operand));
			}
			return result;
		}
		public abstract T VisitInternalJoinOperand(T conditionResult, T agregatedResult, Aggregate aggregateType);
		public virtual T VisitInternalJoinOperand(T conditionResult, IEnumerable<T> agregatedResult, string customAggregateName) {
			throw new NotImplementedException();
		}
		public abstract T VisitInternalFunction(FunctionOperator theOperator);
		public abstract T VisitInternalProperty(string propertyName);
		public abstract T VisitInternalAggregate(T collectionPropertyResult, T aggregateResult, Aggregate aggregateType, T conditionResult);
		public virtual T VisitInternalAggregate(T collectionPropertyResult, IEnumerable<T> aggregateResult, string customAggregateName, T conditionResult) {
			throw new NotImplementedException();
		}
		public abstract T VisitInternalInOperator(InOperator theOperator);
		public abstract T VisitInternalUnary(UnaryOperator theOperator);
		public abstract T VisitInternalBetween(BetweenOperator theOperator);
		public abstract T VisitInternalGroup(GroupOperatorType operatorType, List<T> results);
		public abstract T VisitInternalBinary(T left, T right, BinaryOperatorType operatorType);
		public abstract T VisitInternalOperand(object value);
		public T Visit(JoinOperand theOperand) {
			hasJoinOperand = true;
			XPClassInfo joinedCi = null;
			if(!MemberInfoCollection.TryResolveTypeAlsoByShortName(theOperand.JoinTypeName, currentState.ClassInfo, out joinedCi)) {
				throw new CannotResolveClassInfoException(string.Empty, theOperand.JoinTypeName);
			}
			ContextState newState = new ContextState(joinedCi, true);
			hasJoinOperandOnStack++;
			stateStack.Push(currentState);
			currentState = newState;
			try {
				T conditionResult = Process(theOperand.Condition);
				currentState.InAtomOperator++;
				try {
					if(theOperand.AggregateType != Aggregate.Custom) {
						T agregatedResult = Process(theOperand.AggregatedExpression);
						return VisitInternalJoinOperand(conditionResult, agregatedResult, theOperand.AggregateType);
					}
					else {
						var operands = ProcessCollection(theOperand.CustomAggregateOperands);
						return VisitInternalJoinOperand(conditionResult, operands, theOperand.CustomAggregateName);
					}
				}
				finally {
					currentState.InAtomOperator--;
				}
			}
			finally {
				currentState = stateStack.Pop();
				hasJoinOperandOnStack--;
			}
		}
		public T Visit(OperandValue theOperand) {
			return VisitInternalOperand(theOperand.Value);
		}
		public T Visit(OperandProperty theOperand) {
			Stack<ContextState> localStateStack = null;
			string propertyName = theOperand.PropertyName;
			while(propertyName.StartsWith("^.")) {
				if(localStateStack == null) {
					localStateStack = new Stack<ContextState>();
				}
				if(stateStack.Count == 0) throw new InvalidOperationException("^.");
				propertyName = propertyName.Substring(2);
				localStateStack.Push(currentState);
				currentState = stateStack.Pop();
			}
			try {
				return VisitInternalProperty(propertyName);
			}
			finally {
				if(localStateStack != null) {
					while(localStateStack.Count > 0) {
						stateStack.Push(currentState);
						currentState = localStateStack.Pop();
					}
				}
			}
		}
		public T Visit(AggregateOperand theOperand) {
			if(theOperand.IsTopLevel) {
				if(theOperand.AggregateType != Aggregate.Custom) {
					return VisitInternalAggregate(default(T), Process(theOperand.AggregatedExpression), theOperand.AggregateType, Process(theOperand.Condition));
				}
				return VisitInternalAggregate(default(T), ProcessCollection(theOperand.CustomAggregateOperands), theOperand.CustomAggregateName, Process(theOperand.Condition));
			}
			T propertyResult = Process(theOperand.CollectionProperty);
			Stack<ContextState> localStateStack = null;
			string propertyName = theOperand.CollectionProperty.PropertyName;
			while(propertyName.StartsWith("^.")) {
				if(localStateStack == null) {
					localStateStack = new Stack<ContextState>();
				}
				if(stateStack.Count == 0) throw new InvalidOperationException("^.");
				propertyName = propertyName.Substring(2);
				localStateStack.Push(currentState);
				currentState = stateStack.Pop();
			}
			try {
				MemberInfoCollection mic = currentState.ClassInfo.ParsePersistentPath(propertyName);
				if(mic.Count == 0) throw new InvalidPropertyPathException(Res.GetString(Res.MetaData_IncorrectPath, currentState.ClassInfo.FullName, propertyName));
				for(int i = 0; i < (mic.Count - 1); i++) {
					XPMemberInfo mi = mic[i];
					stateStack.Push(currentState);
					currentState = new ContextState(mi.ReferenceType == null ? mi.CollectionElementType : mi.ReferenceType);
				}
				XPMemberInfo collectionMi = mic[mic.Count - 1];
				if(!(collectionMi.IsAssociationList || collectionMi.IsManyToManyAlias)) throw new InvalidPropertyPathException(Res.GetString(Res.MetaData_IncorrectPath, currentState.ClassInfo.FullName, propertyName));
				stateStack.Push(currentState);
				currentState = new ContextState(collectionMi.CollectionElementType);
				try {
					if(theOperand.AggregateType != Aggregate.Custom) {
						return VisitInternalAggregate(propertyResult, Process(theOperand.AggregatedExpression), theOperand.AggregateType, Process(theOperand.Condition));
					}
					return VisitInternalAggregate(propertyResult, ProcessCollection(theOperand.CustomAggregateOperands), theOperand.CustomAggregateName, Process(theOperand.Condition));
				}
				finally {
					for(int i = 0; i < mic.Count && stateStack.Count > 0; i++)
						currentState = stateStack.Pop();
				}
			}
			finally {
				if(localStateStack != null) {
					while(localStateStack.Count > 0) {
						stateStack.Push(currentState);
						currentState = localStateStack.Pop();
					}
				}
			}
		}
		public T Visit(FunctionOperator theOperator) {
			currentState.InAtomOperator++;
			try {
				return VisitInternalFunction(theOperator);
			}
			finally {
				currentState.InAtomOperator--;
			}
		}
		public T Visit(GroupOperator theOperator) {
			if(theOperator.OperatorType == GroupOperatorType.Or) {
				currentState.InAtomOperator++;
				try {
					return VisitInternalGroup(theOperator.OperatorType, ProcessCollection(theOperator.Operands));
				}
				finally {
					currentState.InAtomOperator--;
				}
			}
			return VisitInternalGroup(theOperator.OperatorType, ProcessCollection(theOperator.Operands));
		}
		public T Visit(InOperator theOperator) {
			currentState.InAtomOperator++;
			try {
				return VisitInternalInOperator(theOperator);
			}
			finally {
				currentState.InAtomOperator--;
			}
		}
		public T Visit(UnaryOperator theOperator) {
			currentState.InAtomOperator++;
			try {
				return VisitInternalUnary(theOperator);
			}
			finally {
				currentState.InAtomOperator--;
			}
		}
		public T Visit(BinaryOperator theOperator) {
			bool inAtomOperatorSet = false;
			if(theOperator.OperatorType == BinaryOperatorType.Equal) {
				if(ReferenceEquals(currentState.EqualsBinaryOperator, null)) {
					currentState.EqualsBinaryOperator = theOperator;
				}
				else {
					inAtomOperatorSet = true;
					currentState.InAtomOperator++;
				}
			}
			else {
				currentState.EqualsBinaryOperator = null;
				return VisitInternalBinary(Process(theOperator.LeftOperand), Process(theOperator.RightOperand), theOperator.OperatorType);
			}
			try {
				return VisitInternalBinary(Process(theOperator.LeftOperand), Process(theOperator.RightOperand), theOperator.OperatorType);
			}
			finally {
				currentState.EqualsBinaryOperator = null;
				if(inAtomOperatorSet) currentState.InAtomOperator--;
			}
		}
		public T Visit(BetweenOperator theOperator) {
			currentState.InAtomOperator++;
			try {
				return VisitInternalBetween(theOperator);
			}
			finally {
				currentState.InAtomOperator--;
			}
		}
		class ContextState {
			bool isInJoinOperand;
			int inAtomOperator;
			XPClassInfo classInfo;
			BinaryOperator equalsBinaryOperator;
			public bool IsInJoinOperand { get { return isInJoinOperand; } set { isInJoinOperand = value; } }
			public int InAtomOperator { get { return inAtomOperator; } set { inAtomOperator = value; } }
			public XPClassInfo ClassInfo { get { return classInfo; } }
			public BinaryOperator EqualsBinaryOperator { get { return equalsBinaryOperator; } set { equalsBinaryOperator = value; } }
			public ContextState(XPClassInfo classInfo) {
				this.classInfo = classInfo;
			}
			public ContextState(XPClassInfo classInfo, bool isInJoinOperand)
				: this(classInfo) {
				this.isInJoinOperand = isInJoinOperand;
			}
		}
	}
	public class JoinOperandExpander : ClientCriteriaVisitorBase {
		JoinOperandExpanderState currentState;
		Stack<JoinOperandExpanderState> stateStack = new Stack<JoinOperandExpanderState>();
		public JoinOperandExpander(XPClassInfo currentClassInfo) {
			this.currentState = new JoinOperandExpanderState(currentClassInfo);
		}
		public JoinOperandExpander(XPClassInfo[] upLevelsClassInfo) {
			this.currentState = new JoinOperandExpanderState(upLevelsClassInfo[0]);
			for(int i = upLevelsClassInfo.Length - 1; i >= 1; i--) {
				stateStack.Push(new JoinOperandExpanderState(upLevelsClassInfo[i]));
			}
		}
		public static CriteriaOperator Expand(XPClassInfo currentClassInfo, CriteriaOperator criteria) {
			return new JoinOperandExpander(currentClassInfo).Process(criteria);
		}
		public static CriteriaOperator Expand(XPClassInfo[] upLevelsClassInfo, CriteriaOperator criteria) {
			return new JoinOperandExpander(upLevelsClassInfo).Process(criteria);
		}
		protected override CriteriaOperator Visit(JoinOperand theOperand) {
			XPClassInfo joinedCi = null;
			if(!MemberInfoCollection.TryResolveTypeAlsoByShortName(theOperand.JoinTypeName, currentState.ClassInfo, out joinedCi)) {
				throw new CannotResolveClassInfoException(string.Empty, theOperand.JoinTypeName);
			}
			JoinOperandExpanderState newState = new JoinOperandExpanderState(joinedCi, true);
			stateStack.Push(currentState);
			currentState = newState;
			try {
				CriteriaOperator resultCondition = Process(theOperand.Condition);
				if(!ReferenceEquals(currentState.FoundAssociationProperty, null)) {
					currentState.InAtomOperator++;
					try {
						if(theOperand.AggregateType != Aggregate.Custom) {
							return new AggregateOperand(currentState.FoundAssociationProperty, Process(theOperand.AggregatedExpression), theOperand.AggregateType, resultCondition);
						}
						bool modified;
						CriteriaOperatorCollection processedOperands = ProcessCollection(theOperand.CustomAggregateOperands, out modified);
						return new AggregateOperand(currentState.FoundAssociationProperty, processedOperands, theOperand.CustomAggregateName, resultCondition);
					}
					finally {
						currentState.InAtomOperator--;
					}
				}
				currentState.InAtomOperator++;
				try {
					CriteriaOperator agregatedExpression = Process(theOperand.AggregatedExpression);
					if(ReferenceEquals(resultCondition, theOperand.Condition) && ReferenceEquals(agregatedExpression, theOperand.AggregatedExpression)) {
						return theOperand;
					}
					if(theOperand.AggregateType != Aggregate.Custom) {
						return new JoinOperand(theOperand.JoinTypeName, resultCondition, theOperand.AggregateType, agregatedExpression);
					}
					bool modified;
					CriteriaOperatorCollection processedOperands = ProcessCollection(theOperand.CustomAggregateOperands, out modified);
					return new JoinOperand(theOperand.JoinTypeName, resultCondition, theOperand.CustomAggregateName, processedOperands);
				}
				finally {
					currentState.InAtomOperator--;
				}
			}
			finally {
				currentState = stateStack.Pop();
			}
		}
		protected override CriteriaOperator Visit(OperandProperty theOperand) {
			if(currentState.IsInJoinOperand && currentState.InAtomOperator == 0 && !ReferenceEquals(currentState.EqualsBinaryOperator, null) && stateStack.Count > 0) {
				XPClassInfo parentClassInfo = stateStack.Peek().ClassInfo;
				string propertyName = theOperand.PropertyName.TrimEnd('!');
				if(propertyName.StartsWith("^.")) {
					propertyName = propertyName.Substring(2);
					if(parentClassInfo.KeyProperty.Name == propertyName) {
						currentState.ParentKeyMemberInfo = parentClassInfo.KeyProperty;
					}
				}
				else {
					int pointLastIndex = MemberInfoCollection.LastIndexOfSplittingDotInPath(propertyName);
					XPMemberInfo referenceMember = currentState.ClassInfo.FindMember(pointLastIndex >= 0 ? propertyName.Substring(0, pointLastIndex) : propertyName);
					if(referenceMember != null && referenceMember.ReferenceType == parentClassInfo && referenceMember.IsAssociation
						&& ((pointLastIndex < 0) || ((pointLastIndex >= 0) && (parentClassInfo.KeyProperty.Name == propertyName.Substring(pointLastIndex + 1))))) {
						currentState.JoinReferenceMemberInfo = referenceMember;
					}
				}
			}
			return theOperand;
		}
		protected override CriteriaOperator Visit(AggregateOperand theOperand, bool processCollectionProperty) {
			if(theOperand.IsTopLevel) {
				if(theOperand.AggregateType != Aggregate.Custom) {
					return new AggregateOperand(null, Process(theOperand.AggregatedExpression), theOperand.AggregateType, Process(theOperand.Condition));
				}
				else {
					bool modified;
					CriteriaOperatorCollection aggregatedExpr = ProcessCollection(theOperand.CustomAggregateOperands, out modified);
					return new AggregateOperand(null, aggregatedExpr, theOperand.CustomAggregateName, Process(theOperand.Condition));
				}
			}
			Stack<JoinOperandExpanderState> localStateStack = null;
			string propertyName = theOperand.CollectionProperty.PropertyName;
			while(propertyName.StartsWith("^.")) {
				if(localStateStack == null) {
					localStateStack = new Stack<JoinOperandExpanderState>();
				}
				if(stateStack.Count == 0) throw new InvalidOperationException("^.");
				propertyName = propertyName.Substring(2);
				localStateStack.Push(currentState);
				currentState = stateStack.Pop();
			}
			try {
				MemberInfoCollection mic = currentState.ClassInfo.ParsePersistentPath(propertyName);
				if(mic.Count == 0) throw new InvalidPropertyPathException(Res.GetString(Res.MetaData_IncorrectPath, currentState.ClassInfo.FullName, propertyName));
				for(int i = 0; i < (mic.Count - 1); i++) {
					XPMemberInfo mi = mic[i];
					stateStack.Push(currentState);
					currentState = new JoinOperandExpanderState(mi.ReferenceType == null ? mi.CollectionElementType : mi.ReferenceType);
				}
				XPMemberInfo collectionMi = mic[mic.Count - 1];
				if(!collectionMi.IsAssociationList) throw new InvalidPropertyPathException(Res.GetString(Res.MetaData_IncorrectPath, currentState.ClassInfo.FullName, propertyName));
				stateStack.Push(currentState);
				currentState = new JoinOperandExpanderState(collectionMi.CollectionElementType);
				try {
					return base.Visit(theOperand, false);
				}
				finally {
					for(int i = 0; i < mic.Count && stateStack.Count > 0; i++)
						currentState = stateStack.Pop();
				}
			}
			finally {
				if(localStateStack != null) {
					while(localStateStack.Count > 0) {
						stateStack.Push(currentState);
						currentState = localStateStack.Pop();
					}
				}
			}
		}
		protected override CriteriaOperator Visit(FunctionOperator theOperator) {
			currentState.InAtomOperator++;
			try {
				return base.Visit(theOperator);
			}
			finally {
				currentState.InAtomOperator--;
			}
		}
		protected override CriteriaOperator Visit(GroupOperator theOperator) {
			if(theOperator.OperatorType == GroupOperatorType.Or) {
				currentState.InAtomOperator++;
				try {
					return base.Visit(theOperator);
				}
				finally {
					currentState.InAtomOperator--;
				}
			}
			bool modified;
			CriteriaOperatorCollection resultOperands = ProcessCollection(theOperator.Operands, out modified);
			if(!ReferenceEquals(currentState.FoundAssociationProperty, null)) {
				for(int i = resultOperands.Count - 1; i >= 0; i--) {
					if(ReferenceEquals(resultOperands[i], null)) {
						resultOperands.RemoveAt(i);
						modified = true;
					}
				}
			}
			return modified ? new GroupOperator(theOperator.OperatorType, resultOperands) : theOperator;
		}
		protected override CriteriaOperator Visit(InOperator theOperator) {
			currentState.InAtomOperator++;
			try {
				return base.Visit(theOperator);
			}
			finally {
				currentState.InAtomOperator--;
			}
		}
		protected override CriteriaOperator Visit(UnaryOperator theOperator) {
			currentState.InAtomOperator++;
			try {
				return base.Visit(theOperator);
			}
			finally {
				currentState.InAtomOperator--;
			}
		}
		static readonly object objectZero = 0;
		protected override CriteriaOperator Visit(BinaryOperator theOperatorArg) {
			BinaryOperator theOperator = theOperatorArg;
			if(theOperator.RightOperand is OperandValue && object.Equals(((OperandValue)theOperator.RightOperand).Value, objectZero)
				 && theOperator.LeftOperand is AggregateOperand && ((AggregateOperand)theOperator.LeftOperand).AggregateType == Aggregate.Count) {
				AggregateOperand aggregate = (AggregateOperand)theOperator.LeftOperand;
				if(!aggregate.IsTopLevel) {
					switch(theOperator.OperatorType) {
						case BinaryOperatorType.Equal:
							return Process(new AggregateOperand(aggregate.CollectionProperty, aggregate.AggregatedExpression, Aggregate.Exists, aggregate.Condition).Not());
						case BinaryOperatorType.Greater:
							return Process(new AggregateOperand(aggregate.CollectionProperty, aggregate.AggregatedExpression, Aggregate.Exists, aggregate.Condition));
					}
				}
			}
			bool inAtomOperatorSet = false;
			if(theOperator.OperatorType == BinaryOperatorType.Equal && ReferenceEquals(currentState.FoundAssociationProperty, null)) {
				if(ReferenceEquals(currentState.EqualsBinaryOperator, null)) {
					currentState.EqualsBinaryOperator = theOperator;
				}
				else {
					inAtomOperatorSet = true;
					currentState.InAtomOperator++;
				}
			}
			else {
				currentState.EqualsBinaryOperator = null;
				CriteriaOperator right = Process(theOperator.RightOperand);
				CriteriaOperator left = Process(theOperator.LeftOperand);
				return new BinaryOperator(left, right, theOperator.OperatorType);
			}
			try {
				CriteriaOperator left = Process(theOperator.LeftOperand);
				CriteriaOperator right = Process(theOperator.RightOperand);
				if(currentState.JoinReferenceMemberInfo != null && currentState.ParentKeyMemberInfo != null) {
					if(currentState.JoinReferenceMemberInfo.IsAssociation) {
						XPMemberInfo collectionMember = currentState.JoinReferenceMemberInfo.GetAssociatedMember();
						if(collectionMember.IsAssociationList && collectionMember.CollectionElementType == currentState.ClassInfo) {
							currentState.FoundAssociationProperty = new OperandProperty(collectionMember.Name);
							return null;
						}
					}
				}
				currentState.JoinReferenceMemberInfo = null;
				currentState.ParentKeyMemberInfo = null;
				return new BinaryOperator(left, right, theOperator.OperatorType);
			}
			finally {
				currentState.EqualsBinaryOperator = null;
				if(inAtomOperatorSet) currentState.InAtomOperator--;
			}
		}
		protected override CriteriaOperator Visit(BetweenOperator theOperator) {
			currentState.InAtomOperator++;
			try {
				return base.Visit(theOperator);
			}
			finally {
				currentState.InAtomOperator--;
			}
		}
		class JoinOperandExpanderState {
			bool isInJoinOperand;
			int inAtomOperator;
			XPClassInfo classInfo;
			XPMemberInfo joinReferenceMemberInfo;
			XPMemberInfo parentKeyMemberInfo;
			BinaryOperator equalsBinaryOperator;
			OperandProperty foundAssociationProperty;
			public bool IsInJoinOperand { get { return isInJoinOperand; } set { isInJoinOperand = value; } }
			public int InAtomOperator { get { return inAtomOperator; } set { inAtomOperator = value; } }
			public XPClassInfo ClassInfo { get { return classInfo; } }
			public XPMemberInfo JoinReferenceMemberInfo { get { return joinReferenceMemberInfo; } set { joinReferenceMemberInfo = value; } }
			public XPMemberInfo ParentKeyMemberInfo { get { return parentKeyMemberInfo; } set { parentKeyMemberInfo = value; } }
			public BinaryOperator EqualsBinaryOperator { get { return equalsBinaryOperator; } set { equalsBinaryOperator = value; } }
			public OperandProperty FoundAssociationProperty { get { return foundAssociationProperty; } set { foundAssociationProperty = value; } }
			public JoinOperandExpanderState(XPClassInfo classInfo) {
				this.classInfo = classInfo;
			}
			public JoinOperandExpanderState(XPClassInfo classInfo, bool isInJoinOperand)
				: this(classInfo) {
				this.isInJoinOperand = isInJoinOperand;
			}
		}
	}
	class XpoExpressionEvaluator : ExpressionEvaluator {
		public XpoExpressionEvaluator(Session session, EvaluatorContextDescriptor descriptor, CriteriaOperator criteria, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions, ICollection<ICustomAggregate> customAggregates)
			: base(descriptor, PatchCriteriaVisitor.Process(session.ServiceProvider, criteria), caseSensitive, customFunctions, customAggregates) { }
		class PatchCriteriaVisitor : ClientCriteriaVisitorBase {
			readonly IFunctionOperatorPatcher functionOperatorPatcher;
			public PatchCriteriaVisitor(IFunctionOperatorPatcher functionOperatorPatcher) {
				Guard.ArgumentNotNull(functionOperatorPatcher, nameof(functionOperatorPatcher));
				this.functionOperatorPatcher = functionOperatorPatcher;
			}
			protected override CriteriaOperator Visit(FunctionOperator theOperator) {
				if(theOperator.OperatorType == FunctionOperatorType.Custom || theOperator.OperatorType == FunctionOperatorType.CustomNonDeterministic) {
					var expression = functionOperatorPatcher.Patch(theOperator);
					if(!ReferenceEquals(expression, null) && !ReferenceEquals(expression, theOperator)) {
						return expression;
					}
				}
				return base.Visit(theOperator);
			}
			public static CriteriaOperator Process(IServiceProvider serviceProvider, CriteriaOperator criteria) {
				if(serviceProvider != null && !ReferenceEquals(criteria, null)) {
					var functionOperatorPatcher = serviceProvider.GetService(typeof(IFunctionOperatorPatcher)) as IFunctionOperatorPatcher;
					if(functionOperatorPatcher != null) {
						var patcher = new PatchCriteriaVisitor(functionOperatorPatcher);
						return patcher.Process(criteria);
					}
				}
				return criteria;
			}
		}
	}
}
