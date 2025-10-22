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
using System.Linq;
using System.Text;
using DevExpress.Data.Filtering;
using DevExpress.Data.Filtering.Exceptions;
using DevExpress.Data.Filtering.Helpers;
using DevExpress.Utils;
using DevExpress.Xpo.Exceptions;
using DevExpress.Xpo.Helpers;
using DevExpress.Xpo.Metadata;
using DevExpress.Xpo.Metadata.Helpers;
namespace DevExpress.Xpo {
	public interface ISecurityRule {
		XPClassInfo[] SupportedObjectTypes { get; }
		bool GetSelectFilterCriteria(SecurityContext context, XPClassInfo classInfo, out CriteriaOperator criteria);
		bool GetSelectMemberExpression(SecurityContext context, XPClassInfo classInfo, XPMemberInfo memberInfo, out CriteriaOperator expression);
		bool ValidateObjectOnSelect(SecurityContext context, XPClassInfo classInfo, object realObjectOnLoad);
		bool ValidateObjectOnSave(SecurityContext context, XPClassInfo classInfo, object theObject, object realObjectOnLoad);
		bool ValidateObjectOnDelete(SecurityContext context, XPClassInfo classInfo, object theObject, object realObjectOnLoad);
		ValidateMemberOnSaveResult ValidateMemberOnSave(SecurityContext context, XPMemberInfo memberInfo, object theObject, object realObjectOnLoad, object value, object valueOnLoad, object realValueOnLoad);
	}
	public interface IGenericSecurityRule {
		bool ValidateObjectsOnCommit(SecurityContext context, SecurityContextValidateItem[] objectsToSave, SecurityContextValidateItem[] objectsToDelete);
	}
	public interface ISecurityRuleProvider {
		ISecurityRule GetRule(XPClassInfo classInfo);
	}
	public interface ISecurityRuleProvider2 {
		bool Enabled { get; }
		ISecurityRule2 GetRule(XPClassInfo classInfo);
	}
	public interface ISecurityRule2 {
		bool ValidateObjectOnSave(SecurityContext context, object targetObject);
		bool ValidateMemberOnSave(SecurityContext context, XPMemberInfo memberInfo, object targetObject);
		bool IsDoNotSaveMember(SecurityContext securityContext, object realObject, XPMemberInfo mi, object value, object valueOnLoad);
		bool ValidateObjectOnDelete(SecurityContext context, XPClassInfo classInfo, object realObjectOnLoad);
		bool ValidateObjectOnSelect(SecurityContext context, XPClassInfo classInfo, object realObjectOnLoad);
		bool GetSelectMemberExpression(SecurityContext context, XPClassInfo classInfo, XPMemberInfo memberInfo, out CriteriaOperator expression);
		bool GetSelectFilterCriteria(SecurityContext context, XPClassInfo classInfo, out CriteriaOperator criteria);
	}
	public interface IMemberValueProvider {
		object GetMemberValue(SecurityContext context, XPClassInfo classInfo, XPMemberInfo memberInfo, object targetObject);
	}
	public enum ValidateMemberOnSaveResult {
		DoSaveMember,
		DoNotSaveMember,
		DoRaiseException
	}
	public struct SecurityContextValidateItem {
		public readonly XPClassInfo ClassInfo;
		public readonly object TheObject;
		public readonly object RealObject;
		internal SecurityContextValidateItem(XPClassInfo classInfo, object theObject, object realObject) {
			this.ClassInfo = classInfo;
			this.TheObject = theObject;
			this.RealObject = realObject;
		}
	}
	public interface ISecuredPropertyAccessor {
		void SetPropertyValue<T>(object obj, string propertyName, T value);
		void EnterObjectSaving(IXPObject obj);
		void ExitObjectSaving(IXPObject obj);
		void ClearChangedSecuredProperties(object obj);
		void ClearAllChangedSecuredProperties();
		IEnumerable<string> GetChangedSecuredProperties(object obj);
	}
	public class SecurityContext {
		readonly object customContext;
		readonly IGenericSecurityRule genericSecurityRule;
		readonly ISecurityRuleProvider securityRuleProvider;
		readonly Session nestedSession;
		readonly SessionObjectLayer parentObjectLayer;
		readonly Dictionary<ExpressionEvaluatorCacheItem, ExpressionEvaluator> evaluatorDictionary = new Dictionary<ExpressionEvaluatorCacheItem, ExpressionEvaluator>(ExpressionEvaluatorCacheItemComparer.Instance);
		readonly Dictionary<SelectMemberExpressionCacheItem, SelectMemberExpressionCacheItemResult> selectMemberExpressionDictionary = new Dictionary<SelectMemberExpressionCacheItem, SelectMemberExpressionCacheItemResult>(SelectMemberExpressionCacheItemComparer.Instance);
		XPDictionary Dictionary { get { return parentObjectLayer.Dictionary; } }
		public object CustomContext { get { return customContext; } }
		public IGenericSecurityRule GenericSecurityRule { get { return genericSecurityRule; } }
		public ISecurityRuleProvider SecurityRuleProvider { get { return securityRuleProvider; } }
		public Session ParentSession { get { return parentObjectLayer.ParentSession; } }
		public SessionObjectLayer ParentObjectLayer { get { return parentObjectLayer; } }
		public SecurityContext(SessionObjectLayer parentObjectLayer, IGenericSecurityRule genericSecurityRule, ISecurityRuleProvider securityRuleProvide, object customContext) {
			Guard.ArgumentNotNull(securityRuleProvide, nameof(securityRuleProvide));
			this.securityRuleProvider = securityRuleProvide;
			this.customContext = customContext;
			this.parentObjectLayer = parentObjectLayer;
			this.genericSecurityRule = genericSecurityRule;
		}
		public SecurityContext(SessionObjectLayer parentObjectLayer, IGenericSecurityRule genericSecurityRule, ISecurityRuleProvider securityRuleProvide, object customContext, Session nestedSession)
			: this(parentObjectLayer, genericSecurityRule, securityRuleProvide, customContext) {
			this.nestedSession = nestedSession;
		}
		public ExpressionEvaluator GetEvaluator(XPClassInfo classInfo, CriteriaOperator criteria) {
			ExpressionEvaluatorCacheItem cacheItem = new ExpressionEvaluatorCacheItem(classInfo, criteria);
			ExpressionEvaluator evaluator;
			if(evaluatorDictionary.TryGetValue(cacheItem, out evaluator))
				return evaluator;
			evaluator = new ExpressionEvaluator(classInfo.GetEvaluatorContextDescriptor(), criteria, nestedSession.CaseSensitive, Dictionary.CustomFunctionOperators, Dictionary.CustomAggregates);
			evaluatorDictionary.Add(cacheItem, evaluator);
			return evaluator;
		}
		public CriteriaOperator ParseCriteria(string expressionString, out OperandValue[] criteriaParameterList) {
			return nestedSession.ParseCriteria(expressionString, out criteriaParameterList);
		}
		public CriteriaOperator ParseCriteria(string expressionString, params object[] parameters) {
			return nestedSession.ParseCriteria(expressionString, parameters);
		}
		public CriteriaOperator ParseCriteriaOnParentSession(string expressionString, out OperandValue[] criteriaParameterList) {
			return parentObjectLayer.ParentSession.ParseCriteria(expressionString, out criteriaParameterList);
		}
		public CriteriaOperator ParseCriteriaOnParentSession(string expressionString, params object[] parameters) {
			return parentObjectLayer.ParentSession.ParseCriteria(expressionString, parameters);
		}
		public object Evaluate(XPClassInfo classInfo, CriteriaOperator expression, object theObject) {
			return GetEvaluator(classInfo, expression).Evaluate(theObject);
		}
		public bool Fit(XPClassInfo classInfo, CriteriaOperator criteria, object theObject) {
			return GetEvaluator(classInfo, criteria).Fit(theObject);
		}
		public virtual bool Fit(XPClassInfo classInfo, XPMemberInfo memberInfo, CriteriaOperator memberCriteria, string memberCriteriaString, object theObject) {
			return GetEvaluator(classInfo, memberCriteria).Fit(theObject);
		}
		public object EvaluateOnParentSession(XPClassInfo classInfo, CriteriaOperator expression, CriteriaOperator criteria) {
			return parentObjectLayer.ParentSession.Evaluate(classInfo, expression, criteria);
		}
		public CriteriaOperator ExpandToLogical(XPClassInfo classInfo, CriteriaOperator op) {
			return PersistentCriterionExpander.ExpandToLogical(nestedSession == null ? parentObjectLayer.ParentSession : nestedSession, classInfo, op, false).ExpandedCriteria;
		}
		public CriteriaOperator ExpandToValue(XPClassInfo classInfo, CriteriaOperator op) {
			return PersistentCriterionExpander.ExpandToValue(nestedSession == null ? parentObjectLayer.ParentSession : nestedSession, classInfo, op, false).ExpandedCriteria;
		}
		public CriteriaOperator Expand(XPClassInfo classInfo, CriteriaOperator op) {
			return PersistentCriterionExpander.Expand(nestedSession == null ? parentObjectLayer.ParentSession : nestedSession, classInfo, op, false).ExpandedCriteria;
		}
		public XPClassInfo GetClassInfo(object theObject) {
			return Dictionary.QueryClassInfo(theObject);
		}
		public virtual SecurityContext Clone(Session nestedSession) {
			return new SecurityContext(parentObjectLayer, genericSecurityRule, securityRuleProvider, customContext, nestedSession);
		}
		public bool IsObjectMarkedDeleted(object theObject) {
			return nestedSession.IsObjectMarkedDeleted(theObject);
		}
		public static bool IsSystemProperty(XPMemberInfo mi) {
			return mi != null && (mi.IsKey || mi is ServiceField);
		}
		public static bool IsSystemClass(XPClassInfo ci) {
			return ci != null && typeof(IXPOServiceClass).IsAssignableFrom(ci.ClassType);
		}
		public bool GetSelectMemberExpression(ISecurityRule rule, XPClassInfo ci, XPMemberInfo mi, out CriteriaOperator memberExpression) {
			if(rule == null || ci == null || mi == null) {
				memberExpression = null;
				return false;
			}
			SelectMemberExpressionCacheItem item = new SelectMemberExpressionCacheItem(rule, ci, mi);
			SelectMemberExpressionCacheItemResult resultItem;
			if(!selectMemberExpressionDictionary.TryGetValue(item, out resultItem)) {
				CriteriaOperator expression;
				resultItem = new SelectMemberExpressionCacheItemResult(rule.GetSelectMemberExpression(this, ci, mi, out expression), expression);
				selectMemberExpressionDictionary.Add(item, resultItem);
			}
			memberExpression = resultItem.Expression;
			return resultItem.Result;
		}
		public bool GetSelectMemberExpression(ISecurityRule2 rule, XPClassInfo ci, XPMemberInfo mi, out CriteriaOperator memberExpression) {
			if(rule == null || ci == null || mi == null) {
				memberExpression = null;
				return false;
			}
			SelectMemberExpressionCacheItem item = new SelectMemberExpressionCacheItem(rule, ci, mi);
			SelectMemberExpressionCacheItemResult resultItem;
			if(!selectMemberExpressionDictionary.TryGetValue(item, out resultItem)) {
				CriteriaOperator expression;
				resultItem = new SelectMemberExpressionCacheItemResult(rule.GetSelectMemberExpression(this, ci, mi, out expression), expression);
				selectMemberExpressionDictionary.Add(item, resultItem);
			}
			memberExpression = resultItem.Expression;
			return resultItem.Result;
		}
		public bool GetSelectMemberExpression(XPClassInfo ci, XPMemberInfo mi, out CriteriaOperator memberExpression) {
			if(IsSystemClass(ci) || IsSystemProperty(mi)) {
				memberExpression = null;
				return false;
			}
			else {
				ISecurityRule securityRule = null;
				ISecurityRule2 securityRule2 = null;
				ISecurityRuleProvider2 securityRuleProvider2 = SecurityRuleProvider as ISecurityRuleProvider2;
				if(securityRuleProvider2 != null && securityRuleProvider2.Enabled) {
					securityRule2 = securityRuleProvider2.GetRule(ci);
				}
				else {
					securityRule = SecurityRuleProvider.GetRule(ci);
				}
				if(securityRule2 != null) {
					return GetSelectMemberExpression(securityRule2, ci, mi, out memberExpression);
				}
				else if(securityRule != null) {
					return GetSelectMemberExpression(securityRule, ci, mi, out memberExpression);
				}
				else {
					memberExpression = null;
					return false;
				}
			}
		}
		public object GetValueBySecurityRule(object source, XPMemberInfo mi) {
			XPClassInfo ci = nestedSession.GetClassInfo(source);
			if(IsSystemClass(ci) || IsSystemProperty(mi)) {
				return mi.GetValue(source);
			}
			else {
				CriteriaOperator memberExpression;
				ISecurityRule2 rule2 = null;
				ISecurityRuleProvider2 securityRuleProvider2 = SecurityRuleProvider as ISecurityRuleProvider2;
				if(securityRuleProvider2 != null && securityRuleProvider2.Enabled) {
					rule2 = securityRuleProvider2.GetRule(ci);
				}
				if(rule2 is IMemberValueProvider) {
					return ((IMemberValueProvider)rule2).GetMemberValue(this, ci, mi, source);
				}
				else if(GetSelectMemberExpression(ci, mi, out memberExpression)) {
					return Evaluate(AnalyzeCriteriaCreator.GetUpClass(ci, mi.Owner), memberExpression, source);
				}
				else {
					return mi.GetValue(source);
				}
			}
		}
		public static string[] FindDelayedProperties(XPClassInfo classInfo, CriteriaOperator expression, out bool hasJoinOperand) {
			return DelayedPropertiesFinder.Find(classInfo, expression, out hasJoinOperand);
		}
		public virtual bool IsPrefetchPreferred(XPClassInfo classInfo, string[] memberPaths) {
			return true;
		}
		public virtual void NotifyMemberCriteriaHasDelayedProperties(XPMemberInfo memberInfo, string[] delayedPaths) { }
		public virtual void NotifyMemberCriteriaHasFreeJoin(XPMemberInfo memberInfo) { }
		struct ExpressionEvaluatorCacheItem {
			public readonly XPClassInfo ClassInfo;
			public readonly CriteriaOperator Criteria;
			public ExpressionEvaluatorCacheItem(XPClassInfo classInfo, CriteriaOperator criteria) {
				if(classInfo == null)
					throw new ArgumentNullException(nameof(classInfo));
				this.ClassInfo = classInfo;
				this.Criteria = criteria;
			}
		}
		class ExpressionEvaluatorCacheItemComparer : IEqualityComparer<ExpressionEvaluatorCacheItem> {
			static ExpressionEvaluatorCacheItemComparer instance = new ExpressionEvaluatorCacheItemComparer();
			public static ExpressionEvaluatorCacheItemComparer Instance {
				get { return instance; }
			}
			ExpressionEvaluatorCacheItemComparer() { }
			bool IEqualityComparer<ExpressionEvaluatorCacheItem>.Equals(ExpressionEvaluatorCacheItem x, ExpressionEvaluatorCacheItem y) {
				return (x.ClassInfo == y.ClassInfo) && CriteriaOperator.CriterionEquals(x.Criteria, y.Criteria);
			}
			int IEqualityComparer<ExpressionEvaluatorCacheItem>.GetHashCode(ExpressionEvaluatorCacheItem obj) {
				return HashCodeHelper.CalculateGeneric(obj.ClassInfo, obj.Criteria);
			}
		}
		struct SelectMemberExpressionCacheItem {
			public readonly ISecurityRule Rule;
			public readonly ISecurityRule2 Rule2;
			public readonly XPClassInfo ClassInfo;
			public readonly XPMemberInfo MemberInfo;
			public SelectMemberExpressionCacheItem(ISecurityRule rule, XPClassInfo ci, XPMemberInfo mi) {
				Rule = rule;
				Rule2 = null;
				ClassInfo = ci;
				MemberInfo = mi;
			}
			public SelectMemberExpressionCacheItem(ISecurityRule2 rule, XPClassInfo ci, XPMemberInfo mi) {
				Rule = null;
				Rule2 = rule;
				ClassInfo = ci;
				MemberInfo = mi;
			}
		}
		struct SelectMemberExpressionCacheItemResult {
			public readonly bool Result;
			public readonly CriteriaOperator Expression;
			public SelectMemberExpressionCacheItemResult(bool result, CriteriaOperator expression) {
				Result = result;
				Expression = expression;
			}
		}
		class SelectMemberExpressionCacheItemComparer : IEqualityComparer<SelectMemberExpressionCacheItem> {
			static SelectMemberExpressionCacheItemComparer instance = new SelectMemberExpressionCacheItemComparer();
			public static SelectMemberExpressionCacheItemComparer Instance {
				get { return instance; }
			}
			SelectMemberExpressionCacheItemComparer() { }
			bool IEqualityComparer<SelectMemberExpressionCacheItem>.Equals(SelectMemberExpressionCacheItem x, SelectMemberExpressionCacheItem y) {
				bool result = false;
				if(x.Rule != null) {
					result = x.Rule == y.Rule && x.ClassInfo == y.ClassInfo && x.MemberInfo == y.MemberInfo;
				}
				else {
					result = x.Rule2 == y.Rule2 && x.ClassInfo == y.ClassInfo && x.MemberInfo == y.MemberInfo;
				}
				return result;
			}
			int IEqualityComparer<SelectMemberExpressionCacheItem>.GetHashCode(SelectMemberExpressionCacheItem obj) {
				if(obj.Rule != null) {
					return HashCodeHelper.CalculateGeneric(obj.Rule, obj.ClassInfo, obj.MemberInfo);
				}
				else {
					return HashCodeHelper.CalculateGeneric(obj.Rule2, obj.ClassInfo, obj.MemberInfo);
				}
			}
		}
		class DelayedPropertiesFinder : IClientCriteriaVisitor<string[]> {
			public static string[] Find(XPClassInfo classInfo, CriteriaOperator expression, out bool hasJoinOperand ) {
				DelayedPropertiesFinder finder = new DelayedPropertiesFinder(classInfo);
				finder.Process(expression);
				hasJoinOperand = finder.hasJoinOperand;
				return finder.result == null ? Array.Empty<string>() : finder.result.ToArray();
			}
			static void UpdatePaths(string[] paths, string root) {
				for(int i = 0; i < paths.Length; i++) {
					paths[i] = root + "." + paths[i];
				}
			}
			static string[] Union(params string[][] arrays) {
				HashSet<string> result = null;
				foreach(string[] arr in arrays) {
					if(arr == null) continue;
					if(result == null) {
						result = new HashSet<string>();
					}
					foreach(string item in arr) {
						result.Add(item);
					}
				}
				return result == null ? null : result.ToArray();
			}
			bool hasJoinOperand = false;
			XPClassInfo currentClassInfo;
			DelayedPropertiesFinder(XPClassInfo topLevelClassInfo) {
				this.currentClassInfo = topLevelClassInfo;
			}
			readonly Stack<XPClassInfo> stack = new Stack<XPClassInfo>();
			protected bool IsTopLevel() {
				return stack.Count == 0;
			}
			HashSet<string> result = null;
			protected void AddResults(params string[] paths) {
				if(result == null) {
					result = new HashSet<string>();
				}
				foreach(string path in paths) {
					result.Add(path);
				}
			}
			protected string[] Process(CriteriaOperator expression) {
				return expression.ReferenceEqualsNull() ? null : expression.Accept(this);
			}
			protected string[] Process(IList<CriteriaOperator> expressions) {
				HashSet<string> result = null;
				foreach(var expression in expressions) {
					string[] paths = Process(expression);
					if(paths == null) continue;
					if(result == null) {
						result = new HashSet<string>();
					}
					foreach(string path in paths) {
						result.Add(path);
					}
				}
				return result == null ? null : result.ToArray();
			}
			public string[] Visit(AggregateOperand theOperand) {
				Stack<XPClassInfo> localStack = null;
				string propertyName = null;
				if(!theOperand.IsTopLevel) {
					propertyName = theOperand.CollectionProperty.PropertyName;
					localStack = BeginProcessParentRelatingOperator(ref propertyName);
				}
				try {
					MemberInfoCollection path = null;
					if(!theOperand.IsTopLevel) {
						path = currentClassInfo.ParsePath(propertyName);
						if(path.Count == 0) {
							throw new InvalidPropertyPathException(Res.GetString(Res.MetaData_IncorrectPath, currentClassInfo.FullName, propertyName));
						}
						XPClassInfo currentPathClassInfo = currentClassInfo;
						for(int i = 0; i < path.Count; i++) {
							if(!currentPathClassInfo.IsAssignableTo(path[i].Owner)) {
								return null;
							}
							if(path[i].ReferenceType != null) {
								currentPathClassInfo = path[i].ReferenceType;
							}
							else {
								currentPathClassInfo = path[i].Owner;
							}
						}
						foreach(XPMemberInfo mi in path) {
							stack.Push(currentClassInfo);
							if(mi.ReferenceType == null) {
								if(!(mi.IsAssociationList || mi.IsManyToManyAlias)) {
									throw new InvalidPropertyPathException(Res.GetString(Res.MetaData_IncorrectPath, currentClassInfo.FullName, propertyName));
								}
								currentClassInfo = mi.CollectionElementType;
							}
							else {
								currentClassInfo = mi.ReferenceType;
							}
						}
					}
					string[] conditionPaths = null;
					string[] aggregatedExpressionPaths = null;
					string[] customAggregateOperandsPaths = null;
					try {
						conditionPaths = Process(theOperand.Condition);
						aggregatedExpressionPaths = Process(theOperand.AggregatedExpression);
						customAggregateOperandsPaths = Process(theOperand.CustomAggregateOperands);
					}
					finally {
						if(path != null) {
							for(int i = 0; i < path.Count && stack.Count > 0; i++) {
								currentClassInfo = stack.Pop();
							}
						}
					}
					string[] result = Union(conditionPaths, aggregatedExpressionPaths, customAggregateOperandsPaths);
					if(result != null) {
						UpdatePaths(result, propertyName);
					}
					else {
						result = new[] { string.Join(".", MemberInfoCollection.SplitPath(propertyName)) };
					}
					return PrepareResult(result);
				}
				finally {
					EndProcessParentRelatingOperator(localStack);
				}
			}
			public string[] Visit(JoinOperand theOperand) {
				hasJoinOperand = true;
				return null;
			}
			public string[] Visit(OperandProperty theOperand) {
				string propertyName = theOperand.PropertyName;
				Stack<XPClassInfo> localStack = BeginProcessParentRelatingOperator(ref propertyName);
				try {
					string[] result = null;
					var path = MemberInfoCollection.ParsePath(currentClassInfo, propertyName);
					if(!IsTopLevel() || currentClassInfo.IsAssignableTo(path[0].Owner)) {
						for(int i = path.Count - 1; i >= 0; i--) {
							XPMemberInfo mi = path[i];
							if(mi.IsExpandableToPersistent && NeedsPreFetch(mi)) {
								result = new[] { string.Join(".", MemberInfoCollection.SplitPath(propertyName), 0, i + 1) };
								break;
							}
						}
					}
					return PrepareResult(result);
				}
				finally {
					EndProcessParentRelatingOperator(localStack);
				}
			}
			public string[] Visit(BetweenOperator theOperator) {
				return PrepareResult(Union(Process(theOperator.BeginExpression), Process(theOperator.EndExpression), Process(theOperator.TestExpression)));
			}
			public string[] Visit(BinaryOperator theOperator) {
				return PrepareResult(Union(Process(theOperator.LeftOperand), Process(theOperator.RightOperand)));
			}
			public string[] Visit(UnaryOperator theOperator) {
				return PrepareResult(Process(theOperator.Operand));
			}
			public string[] Visit(InOperator theOperator) {
				return PrepareResult(Union(Process(theOperator.LeftOperand), Process(theOperator.Operands)));
			}
			public string[] Visit(GroupOperator theOperator) {
				return PrepareResult(Process(theOperator.Operands));
			}
			public string[] Visit(OperandValue theOperand) {
				return null;
			}
			public string[] Visit(FunctionOperator theOperator) {
				return PrepareResult(Process(theOperator.Operands));
			}
			static bool NeedsPreFetch(XPMemberInfo memberInfo) {
				return memberInfo.IsDelayed
					|| memberInfo.IsCollection
					|| memberInfo.IsManyToManyAlias
					|| memberInfo.IsAssociationList;
			}
			protected Stack<XPClassInfo> BeginProcessParentRelatingOperator(ref string propertyName) {
				Stack<XPClassInfo> result = null;
				while(propertyName.StartsWith("^.")) {
					if(result == null) {
						result = new Stack<XPClassInfo>();
					}
					if(stack.Count == 0) {
						throw new InvalidOperationException("^.");
					}
					propertyName = propertyName.Substring(2);
					result.Push(currentClassInfo);
					currentClassInfo = stack.Pop();
				}
				return result;
			}
			protected void EndProcessParentRelatingOperator(Stack<XPClassInfo> localStack) {
				if(localStack != null) {
					while(localStack.Count > 0) {
						stack.Push(currentClassInfo);
						currentClassInfo = localStack.Pop();
					}
				}
			}
			protected string[] PrepareResult(string[] paths) {
				if(paths == null) return null;
				if(IsTopLevel()) {
					AddResults(paths);
					return null;
				}
				else {
					return paths;
				}
			}
		}
	}
	public class SecurityRuleDictionary : CustomMultiKeyDictionaryCollection<XPClassInfo, ISecurityRule>, ISecurityRuleProvider {
		public SecurityRuleDictionary() : base() { }
		public SecurityRuleDictionary(IEqualityComparer<XPClassInfo> comparer) : base(comparer) { }
		protected override XPClassInfo[] GetKey(ISecurityRule item) {
			return item.SupportedObjectTypes;
		}
		public ISecurityRule GetRule(XPClassInfo classInfo) {
			return GetItem(classInfo);
		}
	}
	public class SecurityOneRuleProvider : ISecurityRuleProvider {
		ISecurityRule rule;
		public SecurityOneRuleProvider(ISecurityRule rule) {
			this.rule = rule;
		}
		public ISecurityRule GetRule(XPClassInfo classInfo) {
			return rule;
		}
	}
	public class SecurityExpressionCleaner : ClientCriteriaVisitorBase {
		readonly CriteriaOperator baseFilterCriteria;
		public SecurityExpressionCleaner(CriteriaOperator baseFilterCriteria) {
			this.baseFilterCriteria = baseFilterCriteria;
		}
		public static CriteriaOperator Clean(CriteriaOperator baseFilterCriteria, CriteriaOperator securityExpression) {
			return new SecurityExpressionCleaner(baseFilterCriteria).Process(securityExpression);
		}
		public CriteriaOperator Clean(CriteriaOperator securityExpression) {
			return Process(securityExpression);
		}
		protected override CriteriaOperator Visit(FunctionOperator theOperator) {
			if(theOperator.OperatorType == FunctionOperatorType.Iif) {
				if(theOperator.Operands.Count > 2 && object.Equals(theOperator.Operands[0], baseFilterCriteria)) {
					return theOperator.Operands[1];
				}
			}
			return base.Visit(theOperator);
		}
	}
	public interface ISecurityCriteriaPatcher {
		CriteriaOperator Process(CriteriaOperator input);
	}
	public class SecurityCriteriaPatcher : ClientCriteriaVisitorBase, ISecurityCriteriaPatcher {
		XPClassInfo currentState;
		readonly SecurityContext securityContext;
		readonly XPClassInfo classInfo;
		readonly Stack<XPClassInfo> stateStack = new Stack<XPClassInfo>();
		readonly SecurityExpressionCleaner securityExpressionCleaner;
		public SecurityCriteriaPatcher(XPClassInfo currentClassInfo, SecurityContext securityContext) {
			classInfo = currentClassInfo;
			this.securityContext = securityContext;
			currentState = classInfo;
			securityExpressionCleaner = GetSecurityExpressionCleaner();
		}
		private SecurityExpressionCleaner GetSecurityExpressionCleaner() {
			SecurityExpressionCleaner securityExpressionCleaner = null;
			if(!SecurityContext.IsSystemClass(classInfo)) {
				ISecurityRule rule = null;
				ISecurityRule2 rule2 = null;
				CriteriaOperator baseFilterCriteria;
				if(securityContext != null) {
					ISecurityRuleProvider2 securityRuleProvider2 = securityContext.SecurityRuleProvider as ISecurityRuleProvider2;
					if(securityRuleProvider2 != null && securityRuleProvider2.Enabled == true) {
						rule2 = securityRuleProvider2.GetRule(classInfo);
					}
					else {
						rule = securityContext.SecurityRuleProvider.GetRule(classInfo);
					}
				}
				if(rule != null && rule.GetSelectFilterCriteria(securityContext, classInfo, out baseFilterCriteria)) {
					securityExpressionCleaner = new SecurityExpressionCleaner(baseFilterCriteria);
				}
				if(rule2 != null && rule2.GetSelectFilterCriteria(securityContext, classInfo, out baseFilterCriteria)) {
					securityExpressionCleaner = new SecurityExpressionCleaner(baseFilterCriteria);
				}
			}
			return securityExpressionCleaner;
		}
		public static CriteriaOperator Patch(XPClassInfo currentClassInfo, SecurityContext securityContext, CriteriaOperator criteria) {
			return new SecurityCriteriaPatcher(currentClassInfo, securityContext).Process(criteria);
		}
		CriteriaOperator ISecurityCriteriaPatcher.Process(CriteriaOperator input) {
			currentState = classInfo;
			return Process(input);
		}
		protected override CriteriaOperator Visit(JoinOperand theOperand) {
			XPClassInfo joinedCi = null;
			if(!MemberInfoCollection.TryResolveTypeAlsoByShortName(theOperand.JoinTypeName, currentState, out joinedCi)) {
				throw new CannotResolveClassInfoException(string.Empty, theOperand.JoinTypeName);
			}
			XPClassInfo newState = joinedCi;
			stateStack.Push(currentState);
			currentState = newState;
			try {
				CriteriaOperator condition = Process(theOperand.Condition);
				if(theOperand.AggregateType != Aggregate.Custom) {
					CriteriaOperator aggregatedExpression = Process(theOperand.AggregatedExpression);
					return ReferenceEquals(aggregatedExpression, theOperand.AggregatedExpression)
						&& ReferenceEquals(condition, theOperand.Condition) ? theOperand : new JoinOperand(theOperand.JoinTypeName, condition, theOperand.AggregateType, aggregatedExpression);
				}
				else {
					bool modified;
					CriteriaOperatorCollection operands = ProcessCollection(theOperand.CustomAggregateOperands, out modified);
					return !modified && ReferenceEquals(condition, theOperand.Condition)
						? theOperand : new JoinOperand(theOperand.JoinTypeName, condition, theOperand.CustomAggregateName, operands);
				}
			}
			finally {
				currentState = stateStack.Pop();
			}
		}
		protected override CriteriaOperator Visit(OperandProperty theOperand) {
			Stack<XPClassInfo> localStateStack = null;
			string propertyName = theOperand.PropertyName;
			while(propertyName.StartsWith("^.")) {
				if(localStateStack == null) {
					localStateStack = new Stack<XPClassInfo>();
				}
				if(stateStack.Count == 0)
					throw new InvalidOperationException("^.");
				propertyName = propertyName.Substring(2);
				localStateStack.Push(currentState);
				currentState = stateStack.Pop();
			}
			try {
				MemberInfoCollection mic = currentState.ParsePath(propertyName);
				if(mic == null)
					return theOperand;
				if(mic.Count > 0) {
					XPClassInfo currentClassInfo = currentState;
					for(int i = 0; i < (mic.Count - 1); i++) {
						currentClassInfo = mic[i].ReferenceType;
						if(currentClassInfo == null)
							throw new InvalidOperationException(); 
					}
					XPMemberInfo mi = mic[mic.Count - 1];
					if(!SecurityContext.IsSystemClass(currentClassInfo) && !SecurityContext.IsSystemProperty(mi)) {
						CriteriaOperator expression;
						if(securityContext.GetSelectMemberExpression(currentClassInfo, mi, out expression)) {
							if(securityExpressionCleaner != null && stateStack.Count == 0) {
								expression = securityExpressionCleaner.Clean(expression);
							}
							CriteriaOperator expanded = securityContext.ExpandToValue(currentClassInfo, expression);
							StringBuilder prefix = null;
							if(localStateStack != null) {
								prefix = new StringBuilder();
								for(int i = 0; i < localStateStack.Count; i++) {
									prefix.Append("^.");
								}
							}
							if(mic.Count > 1) {
								MemberInfoCollection prefixMic = new MemberInfoCollection(currentState, mic.GetRange(0, mic.Count - 1).ToArray());
								string prefixMicString = prefixMic.ToString();
								if(prefix == null)
									prefix = new StringBuilder(prefixMicString.Length + 1);
								prefix.Append(prefixMicString);
								prefix.Append('.');
							}
							if(prefix != null) {
								return TopLevelPropertiesPrefixer.PatchAliasPrefix(prefix.ToString(), expanded);
							}
							return expanded;
						}
					}
				}
				return theOperand;
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
		protected override CriteriaOperator Visit(AggregateOperand theOperand, bool processCollectionProperty) {
			if(theOperand.IsTopLevel) {
				CriteriaOperator condition = Process(theOperand.Condition);
				if(theOperand.AggregateType != Aggregate.Custom) {
					CriteriaOperator aggregatedExpression = Process(theOperand.AggregatedExpression);
					return ReferenceEquals(aggregatedExpression, theOperand.AggregatedExpression)
						&& ReferenceEquals(condition, theOperand.Condition) ? theOperand : new AggregateOperand(null, aggregatedExpression, theOperand.AggregateType, condition);
				}
				else {
					bool modified;
					CriteriaOperatorCollection aggrExprs = ProcessCollection(theOperand.CustomAggregateOperands, out modified);
					return !modified && ReferenceEquals(condition, theOperand.Condition)
						? theOperand
						: new AggregateOperand(null, aggrExprs, theOperand.CustomAggregateName, condition);
				}
			}
			Stack<XPClassInfo> localStateStack = null;
			string propertyName = theOperand.CollectionProperty.PropertyName;
			while(propertyName.StartsWith("^.")) {
				if(localStateStack == null) {
					localStateStack = new Stack<XPClassInfo>();
				}
				if(stateStack.Count == 0)
					throw new InvalidOperationException("^.");
				propertyName = propertyName.Substring(2);
				localStateStack.Push(currentState);
				currentState = stateStack.Pop();
			}
			try {
				MemberInfoCollection mic = currentState.ParsePath(propertyName);
				if(mic.Count == 0)
					throw new InvalidPropertyPathException(Res.GetString(Res.MetaData_IncorrectPath, currentState.FullName, propertyName));
				for(int i = 0; i < (mic.Count - 1); i++) {
					XPMemberInfo mi = mic[i];
					stateStack.Push(currentState);
					currentState = mi.ReferenceType == null ? mi.CollectionElementType : mi.ReferenceType;
				}
				XPMemberInfo collectionMi = mic[mic.Count - 1];
				if(!collectionMi.IsAssociationList && !collectionMi.IsManyToManyAlias)
					throw new InvalidPropertyPathException(Res.GetString(Res.MetaData_IncorrectPath, currentState.FullName, propertyName));
				stateStack.Push(currentState);
				currentState = collectionMi.CollectionElementType;
				try {
					CriteriaOperator condition = Process(theOperand.Condition);
					if(theOperand.AggregateType != Aggregate.Custom) {
						CriteriaOperator aggregatedExpression = Process(theOperand.AggregatedExpression);
						return ReferenceEquals(aggregatedExpression, theOperand.AggregatedExpression)
							&& ReferenceEquals(condition, theOperand.Condition) ? theOperand : new AggregateOperand(theOperand.CollectionProperty, aggregatedExpression, theOperand.AggregateType, condition);
					}
					else {
						bool modified;
						CriteriaOperatorCollection aggrExprs = ProcessCollection(theOperand.CustomAggregateOperands, out modified);
						return !modified && ReferenceEquals(condition, theOperand.Condition)
							? theOperand
							: new AggregateOperand(theOperand.CollectionProperty, aggrExprs, theOperand.CustomAggregateName, condition);
					}
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
	}
	public class SecurityCriteriaBuilder : ClientCriteriaVisitorBase, ISecurityCriteriaPatcher {
		XPClassInfo currentState;
		readonly SecurityContext securityContext;
		readonly Stack<XPClassInfo> stateStack = new Stack<XPClassInfo>();
		readonly SecurityExpressionCleaner securityExpressionCleaner;
		private bool memberGranted;
		public SecurityCriteriaBuilder(XPClassInfo currentClassInfo, SecurityContext securityContext) {
			this.currentState = currentClassInfo;
			this.securityContext = securityContext;
			if(!SecurityContext.IsSystemClass(currentClassInfo)) {
				ISecurityRule rule = null;
				ISecurityRule2 rule2 = null;
				CriteriaOperator baseFilterCriteria;
				if(securityContext != null) {
					ISecurityRuleProvider2 securityRuleProvider2 = securityContext.SecurityRuleProvider as ISecurityRuleProvider2;
					if(securityRuleProvider2 != null && securityRuleProvider2.Enabled == true) {
						rule2 = securityRuleProvider2.GetRule(currentClassInfo);
					}
					else {
						rule = securityContext.SecurityRuleProvider.GetRule(currentClassInfo);
					}
				}
				if(rule != null && rule.GetSelectFilterCriteria(securityContext, currentClassInfo, out baseFilterCriteria)) {
					securityExpressionCleaner = new SecurityExpressionCleaner(baseFilterCriteria);
				}
				if(rule2 != null && rule2.GetSelectFilterCriteria(securityContext, currentClassInfo, out baseFilterCriteria)) {
					securityExpressionCleaner = new SecurityExpressionCleaner(baseFilterCriteria);
				}
			}
		}
		public static CriteriaOperator Patch(XPClassInfo currentClassInfo, SecurityContext securityContext, CriteriaOperator criteria) {
			return new SecurityCriteriaBuilder(currentClassInfo, securityContext).Process(criteria);
		}
		protected new CriteriaOperator Process(CriteriaOperator input) {
			memberGranted = false;
			CriteriaOperator resultCriteriaOperator = TryAccept(input);
			if(memberGranted) {
				resultCriteriaOperator = input;
			}
			else {
				resultCriteriaOperator = input & resultCriteriaOperator;
			}
			return resultCriteriaOperator;
		}
		protected override CriteriaOperator Visit(OperandValue theOperand) {
			return null;
		}
		private CriteriaOperator TryAccept(CriteriaOperator criteriaOperator) {
			CriteriaOperator resultCriteriaOperator = null;
			if(!ReferenceEquals(criteriaOperator, null)) {
				resultCriteriaOperator = criteriaOperator.Accept(this);
			}
			return resultCriteriaOperator;
		}
		protected override CriteriaOperator Visit(BinaryOperator theOperator) {
			CriteriaOperator resultOperator = null;
			CriteriaOperator leftOperand = TryAccept(theOperator.LeftOperand);
			if(!ReferenceEquals(leftOperand, theOperator.LeftOperand)) {
				resultOperator |= leftOperand;
			}
			CriteriaOperator rightOperand = TryAccept(theOperator.RightOperand);
			if(!ReferenceEquals(rightOperand, theOperator.RightOperand)) {
				resultOperator |= rightOperand;
			}
			return resultOperator;
		}
		protected override CriteriaOperator Visit(UnaryOperator theOperator) {
			CriteriaOperator resultOperator = null;
			CriteriaOperator operand = TryAccept(theOperator.Operand);
			if(!ReferenceEquals(operand, theOperator.Operand)) {
				resultOperator = operand;
			}
			return operand;
		}
		protected override CriteriaOperator Visit(JoinOperand theOperand) {
			XPClassInfo joinedCi = null;
			if(!MemberInfoCollection.TryResolveTypeAlsoByShortName(theOperand.JoinTypeName, currentState, out joinedCi)) {
				throw new CannotResolveClassInfoException(string.Empty, theOperand.JoinTypeName);
			}
			XPClassInfo newState = joinedCi;
			stateStack.Push(currentState);
			currentState = newState;
			try {
				CriteriaOperator resultCriteriaOperator = null;
				if(theOperand.AggregateType != Aggregate.Custom) {
					CriteriaOperator aggregatedExpression = TryAccept(theOperand.AggregatedExpression);
					CriteriaOperator condition = TryAccept(theOperand.Condition);
					if(ReferenceEquals(aggregatedExpression, null) && ReferenceEquals(condition, null)) {
						resultCriteriaOperator = null;
					}
					else {
						resultCriteriaOperator = ReferenceEquals(aggregatedExpression, theOperand.AggregatedExpression)
							&& ReferenceEquals(condition, theOperand.Condition) ? theOperand : new JoinOperand(theOperand.JoinTypeName, condition, theOperand.AggregateType, aggregatedExpression);
					}
				}
				else {
					var aggregatedExpressions = theOperand.CustomAggregateOperands.Select(t => TryAccept(t)).ToList();
					CriteriaOperator condition = TryAccept(theOperand.Condition);
					if(aggregatedExpressions.Count == 0 && ReferenceEquals(condition, null)) {
						resultCriteriaOperator = null;
					}
					else {
						resultCriteriaOperator = theOperand.CustomAggregateOperands.Equals(aggregatedExpressions)
							&& ReferenceEquals(condition, theOperand.Condition) ? theOperand : new JoinOperand(theOperand.JoinTypeName, condition, theOperand.CustomAggregateName, aggregatedExpressions);
					}
				}
				return resultCriteriaOperator;
			}
			finally {
				currentState = stateStack.Pop();
			}
		}
		protected override CriteriaOperator Visit(BetweenOperator theOperator) {
			CriteriaOperator resultOperator = null;
			CriteriaOperator test = TryAccept(theOperator.TestExpression);
			if(!ReferenceEquals(test, theOperator.TestExpression)) {
				resultOperator |= test;
			}
			CriteriaOperator begin = TryAccept(theOperator.BeginExpression);
			if(!ReferenceEquals(begin, theOperator.BeginExpression)) {
				resultOperator |= begin;
			}
			CriteriaOperator end = TryAccept(theOperator.EndExpression);
			if(!ReferenceEquals(end, theOperator.EndExpression)) {
				resultOperator |= end;
			}
			return resultOperator;
		}
		protected override CriteriaOperator Visit(FunctionOperator theOperator) {
			CriteriaOperator resultOperator = null;
			foreach(CriteriaOperator criteriaOperator in theOperator.Operands) {
				CriteriaOperator processCriteria = TryAccept(criteriaOperator);
				if(!ReferenceEquals(processCriteria, criteriaOperator)) {
					resultOperator |= processCriteria;
				}
			}
			return resultOperator;
		}
		protected override CriteriaOperator Visit(InOperator theOperator) {
			CriteriaOperator resultOperator = null;
			CriteriaOperator leftOperand = TryAccept(theOperator.LeftOperand);
			if(!ReferenceEquals(leftOperand, theOperator.LeftOperand)) {
				resultOperator |= leftOperand;
			}
			foreach(CriteriaOperator criteriaOperator in theOperator.Operands) {
				CriteriaOperator processCriteria = TryAccept(criteriaOperator);
				if(!ReferenceEquals(processCriteria, criteriaOperator)) {
					resultOperator |= processCriteria;
				}
			}
			return resultOperator;
		}
		protected override CriteriaOperator Visit(GroupOperator theOperator) {
			CriteriaOperator resultOperator = null;
			foreach(CriteriaOperator criteriaOperator in theOperator.Operands) {
				CriteriaOperator processCriteria = TryAccept(criteriaOperator);
				if(!ReferenceEquals(processCriteria, criteriaOperator)) {
					resultOperator |= processCriteria;
				}
			}
			return resultOperator;
		}
		protected override CriteriaOperator Visit(OperandProperty theOperand) {
			Stack<XPClassInfo> localStateStack = null;
			string propertyName = theOperand.PropertyName;
			while(propertyName.StartsWith("^.")) {
				if(localStateStack == null) {
					localStateStack = new Stack<XPClassInfo>();
				}
				if(stateStack.Count == 0)
					throw new InvalidOperationException("^.");
				propertyName = propertyName.Substring(2);
				localStateStack.Push(currentState);
				currentState = stateStack.Pop();
			}
			try {
				MemberInfoCollection mic = currentState.ParsePath(propertyName);
				if(mic == null)
					return theOperand;
				if(mic.Count > 0) {
					XPClassInfo currentClassInfo = currentState;
					for(int i = 0; i < (mic.Count - 1); i++) {
						currentClassInfo = mic[i].ReferenceType;
						if(currentClassInfo == null)
							throw new InvalidOperationException(); 
					}
					XPMemberInfo mi = mic[mic.Count - 1];
					if(!SecurityContext.IsSystemClass(currentClassInfo) && !SecurityContext.IsSystemProperty(mi)) {
						CriteriaOperator expression;
						if(securityContext.GetSelectMemberExpression(currentClassInfo, mi, out expression)) { 
							FunctionOperator functionOperator = expression as FunctionOperator;
							if(!ReferenceEquals(functionOperator, null) && functionOperator.OperatorType == FunctionOperatorType.Iif) {
								expression = functionOperator.Operands[0];
							}
							else {
								return CriteriaOperator.Parse("1=0");
							}
							StringBuilder prefix = null;
							if(localStateStack != null) {
								prefix = new StringBuilder();
								for(int i = 0; i < localStateStack.Count; i++) {
									prefix.Append("^.");
								}
							}
							if(mic.Count > 1) {
								MemberInfoCollection prefixMic = new MemberInfoCollection(currentState, mic.GetRange(0, mic.Count - 1).ToArray());
								string prefixMicString = prefixMic.ToString();
								if(prefix == null)
									prefix = new StringBuilder(prefixMicString.Length + 1);
								prefix.Append(prefixMicString);
								prefix.Append('.');
							}
							if(prefix != null) {
								expression = TopLevelPropertiesPrefixer.PatchAliasPrefix(prefix.ToString(), expression);
							}
							return expression;
						}
					}
				}
				memberGranted = true;
				return CriteriaOperator.Parse("0=0");
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
		protected override CriteriaOperator Visit(AggregateOperand theOperand, bool processCollectionProperty) {
			if(theOperand.IsTopLevel) {
				CriteriaOperator aggregatedExpression = TryAccept(theOperand.AggregatedExpression);
				CriteriaOperator condition = TryAccept(theOperand.Condition);
				return condition | aggregatedExpression;
			}
			Stack<XPClassInfo> localStateStack = null;
			string propertyName = theOperand.CollectionProperty.PropertyName;
			while(propertyName.StartsWith("^.")) {
				if(localStateStack == null) {
					localStateStack = new Stack<XPClassInfo>();
				}
				if(stateStack.Count == 0)
					throw new InvalidOperationException("^.");
				propertyName = propertyName.Substring(2);
				localStateStack.Push(currentState);
				currentState = stateStack.Pop();
			}
			try {
				MemberInfoCollection mic = currentState.ParsePath(propertyName);
				if(mic.Count == 0)
					throw new InvalidPropertyPathException(Res.GetString(Res.MetaData_IncorrectPath, currentState.FullName, propertyName));
				for(int i = 0; i < (mic.Count - 1); i++) {
					XPMemberInfo mi = mic[i];
					stateStack.Push(currentState);
					currentState = mi.ReferenceType == null ? mi.CollectionElementType : mi.ReferenceType;
				}
				XPMemberInfo collectionMi = mic[mic.Count - 1];
				stateStack.Push(currentState);
				currentState = collectionMi.CollectionElementType;
				try {
					CriteriaOperator condition = TryAccept(theOperand.Condition);
					CriteriaOperator resultCriteriaOperator;
					if(theOperand.AggregateType != Aggregate.Custom) {
						CriteriaOperator aggregatedExpression = TryAccept(theOperand.AggregatedExpression);
						if(ReferenceEquals(aggregatedExpression, null) && ReferenceEquals(condition, null)) {
							resultCriteriaOperator = null;
						}
						else {
							resultCriteriaOperator = ReferenceEquals(aggregatedExpression, theOperand.AggregatedExpression)
								&& ReferenceEquals(condition, theOperand.Condition) ? theOperand : new AggregateOperand(theOperand.CollectionProperty, aggregatedExpression, theOperand.AggregateType, condition);
						}
					}
					else {
						bool modified = false;
						bool hasNotNull = false;
						CriteriaOperator[] aggrExprs = new CriteriaOperator[theOperand.CustomAggregateOperands.Count];
						for(int i = 0; i < theOperand.CustomAggregateOperands.Count; i++) {
							CriteriaOperator oldOp = theOperand.CustomAggregateOperands[i];
							CriteriaOperator newOp = TryAccept(oldOp);
							aggrExprs[i] = newOp;
							modified = modified || !ReferenceEquals(oldOp, newOp);
							hasNotNull = hasNotNull || !ReferenceEquals(newOp, null);
						}
						if(hasNotNull && aggrExprs.Length > 0 && ReferenceEquals(condition, null)) {
							resultCriteriaOperator = null;
						}
						else {
							resultCriteriaOperator = modified && ReferenceEquals(condition, theOperand.Condition)
								? theOperand
								: new AggregateOperand(theOperand.CollectionProperty, aggrExprs, theOperand.CustomAggregateName, condition);
						}
					}
					return resultCriteriaOperator;
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
		#region ISecurityCriteriaBuilder Members
		CriteriaOperator ISecurityCriteriaPatcher.Process(CriteriaOperator input) {
			return this.Process(input);
		}
		#endregion
	}
}
