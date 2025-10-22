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
using System.ComponentModel;
using DevExpress.Data.Filtering;
using DevExpress.Data.Filtering.Helpers;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
namespace DevExpress.Xpo.Helpers {
using DevExpress.Xpo.DB.Helpers;
	public abstract class QuereableEvaluatorContextDescriptor : EvaluatorContextDescriptor {
		public abstract object GetQueryResult(DevExpress.Xpo.DB.JoinNode root);
		public abstract object GetOperandValue(object source, DevExpress.Xpo.DB.QueryOperand theOperand);
		public abstract void PushNestedSource(object source);
		public abstract void PopNestedSource();
	}
	class QueryableEvaluatorContext : EvaluatorContext {
		public QueryableEvaluatorContext(EvaluatorContextDescriptor descriptor, object source) : base(descriptor, source) { }
		public object GetOperandValue(DevExpress.Xpo.DB.QueryOperand theOperand) {
			return ((QuereableEvaluatorContextDescriptor)Descriptor).GetOperandValue(Source, theOperand);
		}
	}
	class QueryableExpressionEvaluatorCore : ExpressionEvaluatorCoreBase, IQueryCriteriaVisitor<object> {
		QueryableEvaluatorContext defaultContext;
		protected override bool Is3ValuedLogic { get { return true; } }
		public QueryableExpressionEvaluatorCore(bool caseSensitive) : base(caseSensitive) { }
		public QueryableExpressionEvaluatorCore(bool caseSensitive, EvaluateCustomFunctionHandler evaluateCustomFunction) : base(caseSensitive, evaluateCustomFunction) { }
		public QueryableExpressionEvaluatorCore(bool caseSensitive, EvaluateCustomFunctionHandler evaluateCustomFunction, CustomAggregateResolveHandler customAggregateResolveHandler) 
			: base(caseSensitive, evaluateCustomFunction, customAggregateResolveHandler) { }
		public virtual object Visit(DevExpress.Xpo.DB.QuerySubQueryContainer theOperand) {
			QuereableEvaluatorContextDescriptor descriptor = (QuereableEvaluatorContextDescriptor)defaultContext.Descriptor;
			object source = defaultContext.Source;
			descriptor.PushNestedSource(source);
			try {
				if(!ReferenceEquals(theOperand.Node, null)) source = descriptor.GetQueryResult(theOperand.Node);
				List<QueryableEvaluatorContext> currentContexts = new List<QueryableEvaluatorContext>();
				IEnumerable rows = source as IEnumerable;
				if(rows == null) {
					currentContexts.Add(new QueryableEvaluatorContext(descriptor, source));
				} else {
					foreach(object row in rows) {
						currentContexts.Add(new QueryableEvaluatorContext(descriptor, row));
					}
				}
				QueryableEvaluatorContext saveContext = defaultContext;
				try {
					if(theOperand.AggregateType != Aggregate.Custom) {
						return DoAggregate(theOperand.AggregateType, currentContexts, null, theOperand.AggregateProperty);
					}
					return DoCustomAggregate(theOperand.CustomAggregateName, currentContexts, null, theOperand.CustomAggregateOperands);
				} finally {
					defaultContext = saveContext;
				}
			} finally {
				descriptor.PopNestedSource();
			}
		}
		class CriteriaOperatorReferenceComparer : IEqualityComparer<CriteriaOperator> {
			readonly static CriteriaOperatorReferenceComparer instance = new CriteriaOperatorReferenceComparer();
			public static CriteriaOperatorReferenceComparer Instance { get { return instance; } }
			bool IEqualityComparer<CriteriaOperator>.Equals(CriteriaOperator x, CriteriaOperator y) {
				return ReferenceEquals(x, y);
			}
			int IEqualityComparer<CriteriaOperator>.GetHashCode(CriteriaOperator obj) {
				return obj.GetHashCode();
			}
		}
		class InOperatorCacheItem {
			public readonly Dictionary<object, bool> Dictionary;
			public readonly bool HasNullValue;
			public readonly bool IsNonDeterministic;
			public InOperatorCacheItem(Dictionary<object, bool> dictionary, bool hasNullValue){
				Dictionary = dictionary;
				HasNullValue = hasNullValue;
			}
			public InOperatorCacheItem() {
				IsNonDeterministic = true;
			}
			public bool Contains(object value) {
				if(value == null)
					return HasNullValue;
				return Dictionary.ContainsKey(value);
			}
		}
		class InOperatorCacheItemComparer : IEqualityComparer<object> {
			public readonly bool CaseSensitive;
			public readonly IComparer CustomComparer;
			public readonly bool IsEqualityComparer;
			public InOperatorCacheItemComparer(bool isEqulityComparer, bool caseSensitive, IComparer customComparer) {
				IsEqualityComparer = isEqulityComparer;
				CaseSensitive = caseSensitive;
				CustomComparer = customComparer;
			}
			bool IEqualityComparer<object>.Equals(object x, object y) {
				return EvalHelpers.CompareObjects(x, y, IsEqualityComparer, CaseSensitive, CustomComparer) == 0;
			}
			int IEqualityComparer<object>.GetHashCode(object obj) {
				return obj.GetHashCode();
			}
		}
		Dictionary<CriteriaOperator, InOperatorCacheItem> inOperatorOptimizer;
		public override object Visit(InOperator theOperator) {
			object val = Process(theOperator.LeftOperand);
			if(theOperator.Operands.Count > 3) {
				if(inOperatorOptimizer == null) inOperatorOptimizer = new Dictionary<CriteriaOperator, InOperatorCacheItem>(CriteriaOperatorReferenceComparer.Instance);
				InOperatorCacheItem cacheItem;
				if(!inOperatorOptimizer.TryGetValue(theOperator, out cacheItem)) {
					bool hasNullValue = false;
					bool isNonDeterministic = false;
					Dictionary<object, bool> dictionary = new Dictionary<object, bool>(new InOperatorCacheItemComparer(true, CaseSensitive, CustomComparer));
					foreach(CriteriaOperator op in theOperator.Operands) {
						if(!(op is OperandValue)) {
							isNonDeterministic = true;
							break;
						}
						object operand = Process(op);
						if(operand == null)
							hasNullValue = true;
						else
							dictionary[operand] = true;
					}
					cacheItem = isNonDeterministic ? new InOperatorCacheItem() : new InOperatorCacheItem(dictionary, hasNullValue);
					inOperatorOptimizer.Add(theOperator, cacheItem);
				}
				if(!cacheItem.IsNonDeterministic)
					return BoxBool(cacheItem.Contains(val));
			}
			foreach(CriteriaOperator op in theOperator.Operands)
				if(EvalHelpers.CompareObjects(val, Process(op), true, CaseSensitive, CustomComparer) == 0)
					return BoxBool(true);
			return BoxBool(false);
		}
		public virtual object Visit(DevExpress.Xpo.DB.QueryOperand theOperand) {
			return defaultContext.GetOperandValue(theOperand);
		}
		protected sealed override void SetContext(EvaluatorContext context) {
			defaultContext = (QueryableEvaluatorContext)context;
		}
		protected sealed override EvaluatorContext GetContext(int upDepth) {
			return defaultContext;
		}
		protected sealed override EvaluatorContext GetContext() {
			return defaultContext;
		}
		protected sealed override void ClearContext() {
			defaultContext = null;
		}
		protected sealed override bool HasContext {
			get { return defaultContext != null; }
		}
	}
	class QueryableExpressionEvaluator : ExpressionEvaluator {
		readonly ExpressionEvaluatorCoreBase evaluatorCore;
		protected override ExpressionEvaluatorCoreBase EvaluatorCore { get { return evaluatorCore; } }
		public QueryableExpressionEvaluator(EvaluatorContextDescriptor descriptor, CriteriaOperator criteria, bool caseSensitive)
			: base(descriptor, criteria, caseSensitive, false) {
			this.evaluatorCore = new QueryableExpressionEvaluatorCore(caseSensitive, new EvaluateCustomFunctionHandler(EvaluateCustomFunction), new CustomAggregateResolveHandler(ResolveCustomAggregate));
		}
		public QueryableExpressionEvaluator(EvaluatorContextDescriptor descriptor, CriteriaOperator criteria) : this(descriptor, criteria, true) { }
		public QueryableExpressionEvaluator(EvaluatorContextDescriptor descriptor, CriteriaOperator criteria, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions)
			: base(descriptor, criteria, caseSensitive, false, customFunctions) {
			this.evaluatorCore = new QueryableExpressionEvaluatorCore(caseSensitive, new EvaluateCustomFunctionHandler(EvaluateCustomFunction), new CustomAggregateResolveHandler(ResolveCustomAggregate));
		}
		public QueryableExpressionEvaluator(EvaluatorContextDescriptor descriptor, CriteriaOperator criteria, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions, ICollection<ICustomAggregate> customAggregates)
			: base(descriptor, criteria, caseSensitive, false, customFunctions, customAggregates) {
			this.evaluatorCore = new QueryableExpressionEvaluatorCore(caseSensitive, new EvaluateCustomFunctionHandler(EvaluateCustomFunction), new CustomAggregateResolveHandler(ResolveCustomAggregate));
		}
		public QueryableExpressionEvaluator(EvaluatorContextDescriptor descriptor, CriteriaOperator criteria, ICollection<ICustomFunctionOperator> customFunctions)
			: this(descriptor, criteria, true, customFunctions) { }
		public QueryableExpressionEvaluator(EvaluatorContextDescriptor descriptor, CriteriaOperator criteria, ICollection<ICustomFunctionOperator> customFunctions, ICollection<ICustomAggregate> customAggregates)
			: this(descriptor, criteria, true, customFunctions, customAggregates) { }
		public QueryableExpressionEvaluator(PropertyDescriptorCollection properties, CriteriaOperator criteria, bool caseSensitive)
			: base(properties, criteria, caseSensitive, false) {
			this.evaluatorCore = new QueryableExpressionEvaluatorCore(caseSensitive, new EvaluateCustomFunctionHandler(EvaluateCustomFunction), new CustomAggregateResolveHandler(ResolveCustomAggregate));
		}
		public QueryableExpressionEvaluator(PropertyDescriptorCollection properties, CriteriaOperator criteria) : this(properties, criteria, true) { }
		public QueryableExpressionEvaluator(PropertyDescriptorCollection properties, string criteria, bool caseSensitive) : this(properties, CriteriaOperator.Parse(criteria), caseSensitive) { }
		public QueryableExpressionEvaluator(PropertyDescriptorCollection properties, string criteria) : this(properties, criteria, true) { }
		public QueryableExpressionEvaluator(PropertyDescriptorCollection properties, CriteriaOperator criteria, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions)
			: base(properties, criteria, caseSensitive, false ,customFunctions) {
			this.evaluatorCore = new QueryableExpressionEvaluatorCore(caseSensitive, new EvaluateCustomFunctionHandler(EvaluateCustomFunction), new CustomAggregateResolveHandler(ResolveCustomAggregate));
		}
		public QueryableExpressionEvaluator(PropertyDescriptorCollection properties, CriteriaOperator criteria, ICollection<ICustomFunctionOperator> customFunctions)
			: this(properties, criteria, true, customFunctions) { }
		public QueryableExpressionEvaluator(PropertyDescriptorCollection properties, string criteria, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions)
			: this(properties, CriteriaOperator.Parse(criteria), caseSensitive, customFunctions) { }
		public QueryableExpressionEvaluator(PropertyDescriptorCollection properties, string criteria, ICollection<ICustomFunctionOperator> customFunctions)
			: this(properties, CriteriaOperator.Parse(criteria), true, customFunctions) { }
		public QueryableExpressionEvaluator(PropertyDescriptorCollection properties, CriteriaOperator criteria, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions, ICollection<ICustomAggregate> customAggregates)
			: base(properties, criteria, caseSensitive, false, customFunctions, customAggregates) {
			this.evaluatorCore = new QueryableExpressionEvaluatorCore(caseSensitive, new EvaluateCustomFunctionHandler(EvaluateCustomFunction), new CustomAggregateResolveHandler(ResolveCustomAggregate));
		}
		public QueryableExpressionEvaluator(PropertyDescriptorCollection properties, CriteriaOperator criteria, ICollection<ICustomFunctionOperator> customFunctions, ICollection<ICustomAggregate> customAggregates)
			: this(properties, criteria, true, customFunctions, customAggregates) { }
		public QueryableExpressionEvaluator(PropertyDescriptorCollection properties, string criteria, bool caseSensitive, ICollection<ICustomFunctionOperator> customFunctions, ICollection<ICustomAggregate> customAggregates)
			: this(properties, CriteriaOperator.Parse(criteria), caseSensitive, customFunctions, customAggregates) { }
		public QueryableExpressionEvaluator(PropertyDescriptorCollection properties, string criteria, ICollection<ICustomFunctionOperator> customFunctions, ICollection<ICustomAggregate> customAggregates)
			: this(properties, CriteriaOperator.Parse(criteria), true, customFunctions, customAggregates) { }
		protected override EvaluatorContext PrepareContext(object valuesSource) {
			return new QueryableEvaluatorContext((QuereableEvaluatorContextDescriptor)DefaultDescriptor, valuesSource);
		}
	}
}
