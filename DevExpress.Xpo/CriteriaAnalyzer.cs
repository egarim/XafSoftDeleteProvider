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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using DevExpress.Compatibility.System;
using DevExpress.Data.Filtering;
using DevExpress.Data.Filtering.Exceptions;
using DevExpress.Data.Filtering.Helpers;
using DevExpress.Data.Helpers;
using DevExpress.Utils;
using DevExpress.Xpo;
using DevExpress.Xpo.DB.Helpers;
using DevExpress.Xpo.Exceptions;
using DevExpress.Xpo.Metadata;
using DevExpress.Xpo.Metadata.Helpers;
namespace DevExpress.Xpo.Helpers {
	public interface IAnalyzeCriteriaVisitor<T> : IClientCriteriaVisitor<T> {
		T Visit(AnalyzeOperator criteria);
	}
	public class AnalyzeTransitionInfo {
		public readonly Guid JoinOperandId;
		public readonly XPClassInfo JoinClassInfo;
		public readonly XPMemberInfo MemberInfo;
		public readonly bool UpCasting;
		public readonly bool CantWrapCollection;
		public bool IsMemberInfo { get { return MemberInfo != null; } }
		public AnalyzeTransitionInfo(XPMemberInfo memberInfo, bool cantWrapCollection)
			: this(memberInfo, null, false, cantWrapCollection) {
		}
		public AnalyzeTransitionInfo(XPMemberInfo memberInfo, XPClassInfo classInfo, bool upCasting, bool cantWrapCollection) {
			if(memberInfo == null) throw new ArgumentNullException(Res.GetString(Res.CriteriaAnalyzer_NullTransitionMemberInfo));
			this.MemberInfo = memberInfo;
			this.JoinClassInfo = classInfo;
			this.UpCasting = upCasting;
			this.CantWrapCollection = cantWrapCollection;
		}
		public AnalyzeTransitionInfo(Guid joinOperandId, XPClassInfo joinClassInfo, bool cantWrapCollection) {
			if(joinOperandId == Guid.Empty) throw new ArgumentException(Res.GetString(Res.CriteriaAnalyzer_EmptyJoinOperandId));
			if(joinClassInfo == null) throw new ArgumentNullException(Res.GetString(Res.CriteriaAnalyzer_NullJoinClassInfo));
			this.JoinOperandId = joinOperandId;
			this.JoinClassInfo = joinClassInfo;
			this.CantWrapCollection = cantWrapCollection;
		}
		public override string ToString() {
			if(IsMemberInfo) {
				return MemberInfo.ToString();
			}
			return string.Format("[<{0}>]", JoinClassInfo.FullName);
		}
		public override bool Equals(object obj) {
			AnalyzeTransitionInfo other = obj as AnalyzeTransitionInfo;
			if(other == null) return false;
			if(CantWrapCollection != other.CantWrapCollection) {
				return false;
			}
			bool isMemberInfo = IsMemberInfo;
			bool otherIsMemberInfo = other.IsMemberInfo;
			if(isMemberInfo && otherIsMemberInfo) {
				if(MemberInfo.Owner.IsPersistent) {
					return MemberInfo.Equals(other.MemberInfo);
				}
				return MemberInfo.Equals(other.MemberInfo) && object.Equals(JoinClassInfo, other.JoinClassInfo);
			}
			else if(!isMemberInfo && !otherIsMemberInfo) {
				return JoinOperandId.Equals(other.JoinOperandId) && JoinClassInfo.Equals(other.JoinClassInfo);
			}
			return false;
		}
		public override int GetHashCode() {
			if(IsMemberInfo) {
				return HashCodeHelper.CalculateGeneric(MemberInfo, CantWrapCollection);
			}
			else {
				return HashCodeHelper.CalculateGeneric(JoinOperandId, JoinClassInfo, CantWrapCollection);
			}
		}
	}
	public class AnalyzeCriteriaCreator : IClientCriteriaVisitor<AnalyzeOperator> {
		static public readonly AnalyzeInfoKey NodeNameKey = new AnalyzeInfoKey("NodeName");
		static public readonly AnalyzeInfoKey ClassInfoKey = new AnalyzeInfoKey("ClassInfo");
		static public readonly AnalyzeInfoKey TransitionInfoKey = new AnalyzeInfoKey("TransitionInfo");
		static public readonly AnalyzeInfoKey OperatorIdKey = new AnalyzeInfoKey("OperatorId");
		string currentNodeName;
		XPClassInfo currentClassInfo;
		readonly Stack<XPClassInfo> classInfoStack = new Stack<XPClassInfo>(5);
		readonly Stack<string> nodeNameStack = new Stack<string>(5);
		readonly Dictionary<string, Dictionary<AnalyzeTransitionInfo, string>> nodeToNodeMoveDict = new Dictionary<string, Dictionary<AnalyzeTransitionInfo, string>>();
		readonly Dictionary<string, XPClassInfo> nodeClassInfoDict = new Dictionary<string, XPClassInfo>();
		int nodeNameCounter = 1;
		bool topLevelAggregateDetected;
		public bool TopLevelAggregateDetected {
			get { return topLevelAggregateDetected; }
		}
		public AnalyzeCriteriaCreator(XPClassInfo classInfo) {
			this.currentClassInfo = classInfo;
			this.currentNodeName = "N0";
			this.nodeClassInfoDict.Add(currentNodeName, currentClassInfo);
		}
		public void RaiseIfTopLevelAggregate() {
			if(TopLevelAggregateDetected) {
				throw new InvalidOperationException(Res.GetString(Res.CriteriaAnalyzer_TopLevelAggregateNotSupported));
			}
		}
		static public AnalyzeResult Process(XPClassInfo classInfo, CriteriaOperator co) {
			return new AnalyzeCriteriaCreator(classInfo).Process(co);
		}
		AnalyzeResult Process(CriteriaOperator co) {
			AnalyzeOperator ao = ProcessInternal(co);
			return new AnalyzeResult(ao, nodeToNodeMoveDict, nodeClassInfoDict, topLevelAggregateDetected);
		}
		AnalyzeOperator ProcessInternal(CriteriaOperator co) {
			if(ReferenceEquals(co, null)) return null;
			return co.Accept(this);
		}
		string GetNextNodeName(AnalyzeTransitionInfo ati, XPClassInfo nextNodeClassInfo) {
			string nextNodeName;
			if(ati != null) {
				Dictionary<AnalyzeTransitionInfo, string> currentNodeDict = GetNodeDict(currentNodeName);
				string nodeName;
				if(!currentNodeDict.TryGetValue(ati, out nodeName)) {
					nodeName = string.Format(CultureInfo.InvariantCulture, "N{0}", nodeNameCounter++);
					currentNodeDict.Add(ati, nodeName);
				}
				nextNodeName = nodeName;
			}
			else {
				nextNodeName = string.Format(CultureInfo.InvariantCulture, "N{0}", nodeNameCounter++);
			}
			if(!nodeClassInfoDict.ContainsKey(nextNodeName)) {
				nodeClassInfoDict.Add(nextNodeName, nextNodeClassInfo);
			}
			return nextNodeName;
		}
		Dictionary<AnalyzeTransitionInfo, string> GetNodeDict(string nodeName) {
			Dictionary<AnalyzeTransitionInfo, string> nodeDict;
			if(!nodeToNodeMoveDict.TryGetValue(nodeName, out nodeDict)) {
				nodeDict = new Dictionary<AnalyzeTransitionInfo, string>();
				nodeToNodeMoveDict.Add(nodeName, nodeDict);
			}
			return nodeDict;
		}
		internal static Dictionary<AnalyzeInfoKey, object> CreateAnalyzeInfo() {
			Dictionary<AnalyzeInfoKey, object> result = new Dictionary<AnalyzeInfoKey, object>();
			result[OperatorIdKey] = Guid.NewGuid();
			return result;
		}
		internal static Dictionary<AnalyzeInfoKey, object> CreateAnalyzeInfo(XPClassInfo classInfo, string nodeName) {
			Dictionary<AnalyzeInfoKey, object> result = CreateAnalyzeInfo();
			result[ClassInfoKey] = classInfo;
			result[NodeNameKey] = nodeName;
			return result;
		}
		internal static Dictionary<AnalyzeInfoKey, object> CreateAnalyzeInfo(Guid operatorId, XPClassInfo classInfo, string nodeName) {
			Dictionary<AnalyzeInfoKey, object> result = new Dictionary<AnalyzeInfoKey, object>();
			result[OperatorIdKey] = operatorId;
			result[ClassInfoKey] = classInfo;
			result[NodeNameKey] = nodeName;
			return result;
		}
		internal static Dictionary<AnalyzeInfoKey, object> CreateAnalyzeInfo(XPClassInfo classInfo, string nodeName, AnalyzeTransitionInfo transitionInfo) {
			Dictionary<AnalyzeInfoKey, object> result = CreateAnalyzeInfo(classInfo, nodeName);
			result[TransitionInfoKey] = transitionInfo;
			return result;
		}
		public AnalyzeOperator Visit(JoinOperand theOperand) {
			XPClassInfo joinedCi = null;
			if(!MemberInfoCollection.TryResolveTypeAlsoByShortName(theOperand.JoinTypeName, currentClassInfo, out joinedCi)) {
				throw new CannotResolveClassInfoException(string.Empty, theOperand.JoinTypeName);
			}
			classInfoStack.Push(currentClassInfo);
			nodeNameStack.Push(currentNodeName);
			Guid operatorId = Guid.NewGuid();
			AnalyzeTransitionInfo transitionInfo = new AnalyzeTransitionInfo(operatorId, joinedCi, theOperand.AggregateType != Aggregate.Exists);
			currentClassInfo = joinedCi;
			currentNodeName = GetNextNodeName(transitionInfo, currentClassInfo);
			try {
				JoinOperand processedAggr;
				if(theOperand.AggregateType != Aggregate.Custom) {
					processedAggr = new JoinOperand(theOperand.JoinTypeName, ProcessInternal(theOperand.Condition), theOperand.AggregateType, ProcessInternal(theOperand.AggregatedExpression));
				}
				else {
					processedAggr = new JoinOperand(theOperand.JoinTypeName, ProcessInternal(theOperand.Condition), theOperand.CustomAggregateName, theOperand.CustomAggregateOperands.Select(t => ProcessInternal(t)));
				}
				return new AnalyzeOperator<JoinOperand>(processedAggr, CreateAnalyzeInfo(currentClassInfo, currentNodeName, transitionInfo));
			}
			finally {
				currentClassInfo = classInfoStack.Pop();
				currentNodeName = nodeNameStack.Pop();
			}
		}
		public AnalyzeOperator Visit(OperandProperty theOperand) {
			Stack<XPClassInfo> localClassInfoStack = null;
			Stack<string> localNodeNameStack = null;
			string propertyName = theOperand.PropertyName;
			while(propertyName.StartsWith("^.")) {
				if(localClassInfoStack == null) {
					localClassInfoStack = new Stack<XPClassInfo>();
					localNodeNameStack = new Stack<string>();
				}
				if(classInfoStack.Count == 0) throw new InvalidOperationException("^.");
				propertyName = propertyName.Substring(2);
				localClassInfoStack.Push(currentClassInfo);
				currentClassInfo = classInfoStack.Pop();
				localNodeNameStack.Push(currentNodeName);
				currentNodeName = nodeNameStack.Pop();
			}
			try {
				MemberInfoCollection mic = currentClassInfo.ParsePath(propertyName);
				for(int i = 1; i < mic.Count; i++) {
					bool upCasting = IsUpCasting(currentClassInfo, mic[i - 1]);
					classInfoStack.Push(currentClassInfo);
					nodeNameStack.Push(currentNodeName);
					currentClassInfo = GetUpClass(mic[i - 1].ReferenceType, mic[i].Owner);
					currentNodeName = GetNextNodeName(new AnalyzeTransitionInfo(mic[i - 1], currentClassInfo, upCasting, false), currentClassInfo);
				}
				try {
					if(mic.Count == 0) throw new InvalidPropertyPathException(Res.GetString(Res.MetaData_IncorrectPath, currentClassInfo.FullName, propertyName));
					XPMemberInfo mi = mic[mic.Count - 1];
					return new AnalyzeOperator<OperandProperty>(new OperandProperty(theOperand.PropertyName), CreateAnalyzeInfo(currentClassInfo, currentNodeName, new AnalyzeTransitionInfo(mi, currentClassInfo, IsUpCasting(currentClassInfo, mi), false)));
				}
				finally {
					for(int i = 1; i < mic.Count; i++) {
						currentClassInfo = classInfoStack.Pop();
						currentNodeName = nodeNameStack.Pop();
					}
				}
			}
			finally {
				if(localClassInfoStack != null) {
					while(localClassInfoStack.Count > 0) {
						classInfoStack.Push(currentClassInfo);
						nodeNameStack.Push(currentNodeName);
						currentClassInfo = localClassInfoStack.Pop();
						currentNodeName = localNodeNameStack.Pop();
					}
				}
			}
		}
		public static bool IsUpCasting(XPClassInfo currentClassInfo, XPMemberInfo memberInfo) {
			XPClassInfo memberOwner = memberInfo.Owner;
			if(currentClassInfo == memberOwner || currentClassInfo == null || memberOwner == null || !memberOwner.IsPersistent) {
				return false;
			}
			return memberOwner.IsAssignableTo(currentClassInfo);
		}
		public static XPClassInfo GetUpClass(XPClassInfo left, XPClassInfo right) {
			if(left == right) return left;
			if(left == null) return right;
			if(right == null) return left;
			if(left.IsAssignableTo(right)) return left;
			if(right.IsAssignableTo(left)) return right;
			throw new ArgumentException(Res.GetString(Res.CriteriaAnalyzer_ClassesAreNotAssignable, left.FullName, right.FullName));
		}
		public static XPClassInfo GetDownClass(XPClassInfo left, XPClassInfo right) {
			if(left == right) return left;
			if(left == null) return right;
			if(right == null) return left;
			if(left.IsAssignableTo(right)) return right;
			if(right.IsAssignableTo(left)) return left;
			throw new ArgumentException(Res.GetString(Res.CriteriaAnalyzer_ClassesAreNotAssignable, left.FullName, right.FullName));
		}
		public AnalyzeOperator Visit(AggregateOperand theOperand) {
			if(theOperand.IsTopLevel) {
				topLevelAggregateDetected = true;
				AggregateOperand processedAggr;
				if(theOperand.AggregateType != Aggregate.Custom) {
					processedAggr = AggregateOperand.TopLevel(theOperand.AggregateType, ProcessInternal(theOperand.AggregatedExpression));
				}
				else {
					processedAggr = AggregateOperand.TopLevel(theOperand.CustomAggregateName, ProcessOperands(theOperand.CustomAggregateOperands));
				}
				return new AnalyzeOperator<AggregateOperand>(processedAggr, CreateAnalyzeInfo(currentClassInfo, currentNodeName));
			}
			Stack<XPClassInfo> localClassInfoStack = null;
			Stack<string> localNodeNameStack = null;
			string propertyName = theOperand.CollectionProperty.PropertyName;
			while(propertyName.StartsWith("^.")) {
				if(localClassInfoStack == null) {
					localClassInfoStack = new Stack<XPClassInfo>();
					localNodeNameStack = new Stack<string>();
				}
				if(classInfoStack.Count == 0) throw new InvalidOperationException("^.");
				propertyName = propertyName.Substring(2);
				localClassInfoStack.Push(currentClassInfo);
				localNodeNameStack.Push(currentNodeName);
				currentClassInfo = classInfoStack.Pop();
				currentNodeName = nodeNameStack.Pop();
			}
			try {
				MemberInfoCollection mic = currentClassInfo.ParsePersistentPath(propertyName);
				for(int i = 0; i < mic.Count; i++) {
					bool upCasting = IsUpCasting(currentClassInfo, mic[i]);
					classInfoStack.Push(currentClassInfo);
					nodeNameStack.Push(currentNodeName);
					if(mic[i].IsAssociationList) {
						currentClassInfo = mic[i].CollectionElementType;
						currentNodeName = GetNextNodeName(new AnalyzeTransitionInfo(mic[i], null, upCasting, theOperand.AggregateType != Aggregate.Exists), currentClassInfo);
					}
					else {
						currentClassInfo = mic[i].ReferenceType;
						currentNodeName = GetNextNodeName(new AnalyzeTransitionInfo(mic[i], null, upCasting, false), currentClassInfo);
					}
				}
				try {
					if(mic.Count == 0) throw new InvalidPropertyPathException(Res.GetString(Res.MetaData_IncorrectPath, currentClassInfo.FullName, theOperand.CollectionProperty.PropertyName));
					XPMemberInfo mi = mic[mic.Count - 1];
					AggregateOperand processedAggregate;
					if(theOperand.AggregateType != Aggregate.Custom) {
						processedAggregate = new AggregateOperand(theOperand.CollectionProperty, ProcessInternal(theOperand.AggregatedExpression), theOperand.AggregateType, ProcessInternal(theOperand.Condition));
					}
					else {
						processedAggregate = new AggregateOperand(theOperand.CollectionProperty, theOperand.CustomAggregateOperands.Select(t => ProcessInternal(t)), theOperand.CustomAggregateName, ProcessInternal(theOperand.Condition));
					}
					return new AnalyzeOperator<AggregateOperand>(processedAggregate, CreateAnalyzeInfo(currentClassInfo, currentNodeName, new AnalyzeTransitionInfo(mi, null, IsUpCasting(currentClassInfo, mi), theOperand.AggregateType != Aggregate.Exists)));
				}
				finally {
					for(int i = 0; i < mic.Count; i++) {
						currentClassInfo = classInfoStack.Pop();
						currentNodeName = nodeNameStack.Pop();
					}
				}
			}
			finally {
				if(localClassInfoStack != null) {
					while(localClassInfoStack.Count > 0) {
						classInfoStack.Push(currentClassInfo);
						currentClassInfo = localClassInfoStack.Pop();
						nodeNameStack.Push(currentNodeName);
						currentNodeName = localNodeNameStack.Pop();
					}
				}
			}
		}
		public AnalyzeOperator Visit(FunctionOperator theOperator) {
			return new AnalyzeOperator<FunctionOperator>(new FunctionOperator(theOperator.OperatorType, ProcessOperands(theOperator.Operands)), CreateAnalyzeInfo(currentClassInfo, currentNodeName));
		}
		public AnalyzeOperator Visit(OperandValue theOperand) {
			return new AnalyzeOperator<OperandValue>(theOperand is ConstantValue ? new ConstantValue(theOperand.Value) : new OperandValue(theOperand.Value), CreateAnalyzeInfo());
		}
		public AnalyzeOperator Visit(GroupOperator theOperator) {
			return new AnalyzeOperator<GroupOperator>(new GroupOperator(theOperator.OperatorType, ProcessOperands(theOperator.Operands)), CreateAnalyzeInfo(currentClassInfo, currentNodeName));
		}
		public AnalyzeOperator Visit(InOperator theOperator) {
			return new AnalyzeOperator<InOperator>(new InOperator(ProcessInternal(theOperator.LeftOperand), ProcessOperands(theOperator.Operands)), CreateAnalyzeInfo(currentClassInfo, currentNodeName));
		}
		public AnalyzeOperator Visit(UnaryOperator theOperator) {
			return new AnalyzeOperator<UnaryOperator>(new UnaryOperator(theOperator.OperatorType, ProcessInternal(theOperator.Operand)), CreateAnalyzeInfo(currentClassInfo, currentNodeName));
		}
		public AnalyzeOperator Visit(BinaryOperator theOperator) {
			return new AnalyzeOperator<BinaryOperator>(new BinaryOperator(ProcessInternal(theOperator.LeftOperand), ProcessInternal(theOperator.RightOperand), theOperator.OperatorType), CreateAnalyzeInfo(currentClassInfo, currentNodeName));
		}
		public AnalyzeOperator Visit(BetweenOperator theOperator) {
			return new AnalyzeOperator<BetweenOperator>(new BetweenOperator(ProcessInternal(theOperator.TestExpression), ProcessInternal(theOperator.BeginExpression), ProcessInternal(theOperator.EndExpression)), CreateAnalyzeInfo(currentClassInfo, currentNodeName));
		}
		CriteriaOperatorCollection ProcessOperands(CriteriaOperatorCollection collection) {
			CriteriaOperatorCollection result = new CriteriaOperatorCollection();
			for(int i = 0; i < collection.Count; i++) {
				result.Add(ProcessInternal(collection[i]));
			}
			return result;
		}
	}
	public class NodePathCriteriaAnalyzer : IAnalyzeCriteriaVisitor<List<Guid>> {
		string nodeName;
		public NodePathCriteriaAnalyzer(string nodeName) {
			this.nodeName = nodeName;
		}
		public static List<Guid> Process(string nodeName, CriteriaOperator criteria) {
			return new NodePathCriteriaAnalyzer(nodeName).Process(criteria);
		}
		public List<Guid> Process(CriteriaOperator criteria) {
			if(ReferenceEquals(criteria, null)) return null;
			return criteria.Accept(this);
		}
		public List<Guid> Visit(AnalyzeOperator criteria) {
			object savedNodeName;
			List<Guid> path;
			if(criteria.Original is OperandProperty && criteria.AnalyzeInfo.TryGetValue(AnalyzeCriteriaCreator.NodeNameKey, out savedNodeName) && savedNodeName.ToString() == nodeName) {
				path = new List<Guid>();
				path.Add((Guid)criteria.AnalyzeInfo[AnalyzeCriteriaCreator.OperatorIdKey]);
				return path;
			}
			path = Process(criteria.Original);
			if(path == null) return null;
			path.Insert(0, (Guid)criteria.AnalyzeInfo[AnalyzeCriteriaCreator.OperatorIdKey]);
			return path;
		}
		public List<Guid> Visit(JoinOperand theOperand) {
			List<Guid> path;
			if((path = Process(theOperand.AggregatedExpression)) != null) return path;
			if((path = Process(theOperand.Condition)) != null) return path;
			foreach(CriteriaOperator op in theOperand.CustomAggregateOperands) {
				if((path = Process(op)) != null) return path;
			}
			return null;
		}
		public List<Guid> Visit(OperandProperty theOperand) {
			return null;
		}
		public List<Guid> Visit(AggregateOperand theOperand) {
			List<Guid> path;
			if((path = Process(theOperand.AggregatedExpression)) != null) return path;
			if((path = Process(theOperand.Condition)) != null) return path;
			foreach(CriteriaOperator op in theOperand.CustomAggregateOperands) {
				if((path = Process(op)) != null) return path;
			}
			return null;
		}
		public List<Guid> Visit(FunctionOperator theOperator) {
			return ProcessOperands(theOperator.Operands);
		}
		public List<Guid> Visit(OperandValue theOperand) {
			return null;
		}
		public List<Guid> Visit(GroupOperator theOperator) {
			return ProcessOperands(theOperator.Operands);
		}
		public List<Guid> Visit(InOperator theOperator) {
			List<Guid> path = Process(theOperator.LeftOperand);
			if(path != null) return path;
			return ProcessOperands(theOperator.Operands);
		}
		public List<Guid> Visit(UnaryOperator theOperator) {
			return Process(theOperator.Operand);
		}
		public List<Guid> Visit(BinaryOperator theOperator) {
			List<Guid> path;
			if((path = Process(theOperator.LeftOperand)) != null) return path;
			if((path = Process(theOperator.RightOperand)) != null) return path;
			return null;
		}
		public List<Guid> Visit(BetweenOperator theOperator) {
			List<Guid> path;
			if((path = Process(theOperator.TestExpression)) != null) return path;
			if((path = Process(theOperator.BeginExpression)) != null) return path;
			if((path = Process(theOperator.EndExpression)) != null) return path;
			return null;
		}
		List<Guid> ProcessOperands(CriteriaOperatorCollection operands) {
			foreach(CriteriaOperator criteria in operands) {
				List<Guid> path = Process(criteria);
				if(path != null) return path;
			}
			return null;
		}
	}
	class PropertyNameFixCriteriaAnalyzer : ClientCriteriaVisitorBase, IAnalyzeCriteriaVisitor<CriteriaOperator> {
		Guid currentNodeId;
		AnalyzeNodePathItem[] currentAnalyzePathItems;
		Stack<AnalyzeNodePathItem[]> pathsStack = new Stack<AnalyzeNodePathItem[]>();
		readonly HashSet<string> DetectedInvalidPropertyPaths = new HashSet<string>();
		public static CriteriaOperator Fix(CriteriaOperator op, out ICollection<string> detectedInvalidPropertyPaths) {
			var analyzer = new PropertyNameFixCriteriaAnalyzer();
			CriteriaOperator result = analyzer.Process(op);
			detectedInvalidPropertyPaths = analyzer.DetectedInvalidPropertyPaths;
			return result;
		}
		CriteriaOperator IAnalyzeCriteriaVisitor<CriteriaOperator>.Visit(AnalyzeOperator criteria) {
			object o;
			if(criteria.AnalyzeInfo.TryGetValue(ReverseCriteriaAnalyzer.NodePathItemsKey, out o)) {
				currentAnalyzePathItems = (AnalyzeNodePathItem[])o;
			}
			else currentAnalyzePathItems = null;
			currentNodeId = (Guid)criteria.AnalyzeInfo[AnalyzeCriteriaCreator.OperatorIdKey];
			CriteriaOperator result = Process(criteria.Original);
			if(ReferenceEquals(result, criteria.Original)) {
				return criteria;
			}
			return new AnalyzeOperator(result, criteria.AnalyzeInfo);
		}
		protected override CriteriaOperator Visit(JoinOperand theOperand) {
			pathsStack.Push(currentAnalyzePathItems);
			try {
				CriteriaOperator condition = Process(theOperand.Condition);
				if(theOperand.AggregateType != Aggregate.Custom) {
					CriteriaOperator aggregatedExpression = Process(theOperand.AggregatedExpression);
					if(ReferenceEquals(condition, theOperand.Condition) && ReferenceEquals(aggregatedExpression, theOperand.AggregatedExpression)) {
						return theOperand;
					}
					return new JoinOperand(theOperand.JoinTypeName, condition, theOperand.AggregateType, aggregatedExpression);
				}
				else {
					bool modified;
					CriteriaOperatorCollection operands = ProcessCollection(theOperand.CustomAggregateOperands, out modified);
					if(!modified && ReferenceEquals(condition, theOperand.Condition)) {
						return theOperand;
					}
					return new JoinOperand(theOperand.JoinTypeName, condition, theOperand.CustomAggregateName, operands);
				}
			}
			finally {
				pathsStack.Pop();
			}
		}
		protected override CriteriaOperator Visit(OperandProperty theOperand) {
			AnalyzeNodePathItem[][] paths = pathsStack.ToArray();
			int foundPathIndex = -1;
			int startItemIndex = -1;
			int startPathItemIndex = -1;
			int forkIndex = -1;
			for(int pathIndex = 0; pathIndex < paths.Length; pathIndex++) {
				AnalyzeNodePathItem[] path = paths[pathIndex];
				bool doContinue = true;
				if(currentAnalyzePathItems != null) {
					startItemIndex = -1;
					startPathItemIndex = -1;
					forkIndex = -1;
					for(int itemIndex = 0; itemIndex < currentAnalyzePathItems.Length; itemIndex++) {
						for(int pathItemIndex = 0; pathItemIndex < path.Length; pathItemIndex++) {
							if(path[itemIndex].Node == currentAnalyzePathItems[itemIndex].Node) {
								if(startItemIndex < 0) {
									startItemIndex = itemIndex;
									startPathItemIndex = pathItemIndex;
								}
								break;
							}
						}
						if(startItemIndex >= 0) {
							break;
						}
					}
					if(startItemIndex >= 0) {
						int minLength = Math.Min(currentAnalyzePathItems.Length - startItemIndex, path.Length - startPathItemIndex);
						for(int index = 0; index < minLength; index++) {
							var currentItem = currentAnalyzePathItems[index + startItemIndex];
							var pathItem = path[index + startPathItemIndex];
							if(currentItem.TransitionInfo != pathItem.TransitionInfo) {
								forkIndex = index;
							}
						}
						doContinue = false;
					}
				}
				if(doContinue) {
					continue;
				}
				foundPathIndex = pathIndex;
				break;
			}
			if(foundPathIndex < 0 && paths.Length > 0) {
				foundPathIndex = 0;
			}
			string resultPropertyName;
			if(foundPathIndex < 0) {
				DetectInvalidPropertyPath(currentAnalyzePathItems, theOperand.PropertyName);
				resultPropertyName = (GetPathString(currentAnalyzePathItems) + theOperand.PropertyName).TrimEnd('.');
			}
			else {
				if(startItemIndex >= 0) {
					int currentLength = currentAnalyzePathItems.Length - startItemIndex;
					int pathLength = paths[foundPathIndex].Length - startPathItemIndex;
					if(forkIndex >= 0) {
						DetectInvalidPropertyPath(currentAnalyzePathItems, startItemIndex + forkIndex, currentLength - forkIndex, theOperand.PropertyName);
						resultPropertyName = (GetPrefix(pathLength - forkIndex) + GetPathString(currentAnalyzePathItems, startItemIndex + forkIndex, currentLength - forkIndex) + theOperand.PropertyName).TrimEnd('.');
					}
					else if(pathLength > currentLength) {
						resultPropertyName = (GetPrefix(pathLength - currentLength) + theOperand.PropertyName).TrimEnd('.');
					}
					else {
						DetectInvalidPropertyPath(currentAnalyzePathItems, startItemIndex + pathLength, currentLength - pathLength, theOperand.PropertyName);
						resultPropertyName = (GetPathString(currentAnalyzePathItems, startItemIndex + pathLength, currentLength - pathLength) + theOperand.PropertyName).TrimEnd('.');
					}
				}
				else {
					DetectInvalidPropertyPath(currentAnalyzePathItems, theOperand.PropertyName);
					resultPropertyName = (GetPrefix(paths[foundPathIndex].Length) + GetPathString(currentAnalyzePathItems) + theOperand.PropertyName).TrimEnd('.');
				}
			}
			if(string.Equals(resultPropertyName, theOperand.PropertyName)) {
				return theOperand;
			}
			return new OperandProperty(resultPropertyName);
		}
		static ConcurrentDictionary<int, string> prefixDictionary = new ConcurrentDictionary<int, string>();
		static string GetPrefix(int prefixCount) {
			if(prefixCount < 0) {
				return string.Empty;
			}
			return prefixDictionary.GetOrAdd(prefixCount, (pxCount) => {
				StringBuilder prefix = new StringBuilder();
				for(int i = 0; i < pxCount; i++) {
					prefix.Append("^.");
				}
				return prefix.ToString();
			});
		}
		protected override CriteriaOperator Visit(AggregateOperand theOperand, bool processCollectionProperty) {
			if(currentAnalyzePathItems == null) {
				return null;
			}
			OperandProperty op = (OperandProperty)Process(new OperandProperty(string.Empty));
			pathsStack.Push(currentAnalyzePathItems);
			try {
				CriteriaOperator condition = Process(theOperand.Condition);
				if(theOperand.AggregateType != Aggregate.Custom) {
					CriteriaOperator aggregatedExpression = Process(theOperand.AggregatedExpression);
					if(ReferenceEquals(aggregatedExpression, theOperand.AggregatedExpression) && ReferenceEquals(condition, theOperand.Condition)
							&& ReferenceEquals(op, theOperand.CollectionProperty) && string.Equals(op.PropertyName, theOperand.CollectionProperty.PropertyName)) {
						return theOperand;
					}
					return new AggregateOperand(op, aggregatedExpression, theOperand.AggregateType, condition);
				}
				else {
					bool modified;
					CriteriaOperatorCollection aggrExprs = ProcessCollection(theOperand.CustomAggregateOperands, out modified);
					if(!modified && ReferenceEquals(condition, theOperand.Condition)
							&& ReferenceEquals(op, theOperand.CollectionProperty) && string.Equals(op.PropertyName, theOperand.CollectionProperty.PropertyName)) {
						return theOperand;
					}
					return new AggregateOperand(op, aggrExprs, theOperand.CustomAggregateName, condition);
				}
			}
			finally {
				pathsStack.Pop();
			}
		}
		string GetPathString(AnalyzeNodePathItem[] path) {
			if(path == null) return string.Empty;
			return GetPathString(path, 0, path.Length);
		}
		string GetPathString(AnalyzeNodePathItem[] path, int start, int count) {
			if(path == null) return string.Empty;
			StringBuilder sb = new StringBuilder();
			for(int i = start; i < start + count; i++) {
				var mi = path[i].TransitionInfo.MemberInfo;
				if(mi != null) {
					if(path[i].TransitionInfo.UpCasting) {
						sb.Append('<');
						sb.Append(mi.Owner.FullName);
						sb.Append('>');
					}
					sb.Append(mi.Name);
				}
				else {
					throw new InvalidOperationException("JoinOperand in property path.");
				}
				sb.Append('.');
			}
			return sb.ToString();
		}
		void DetectInvalidPropertyPath(AnalyzeNodePathItem[] path, string propertyName) {
			if(path == null) return;
			DetectInvalidPropertyPath(path, path.Length - 1, 1, propertyName);
		}
		void DetectInvalidPropertyPath(AnalyzeNodePathItem[] path, int start, int count, string propertyName) {
			int index = start + count - 1;
			if(string.IsNullOrEmpty(propertyName) || path == null || count == 0 || index < 0 || path.Length - 1 < index) return;
			var pathItem = path[index];
			if(pathItem == null || pathItem.TransitionInfo.MemberInfo == null) return;
			if(pathItem.TransitionInfo.MemberInfo.IsCollection || pathItem.TransitionInfo.MemberInfo.IsAssociationList) {
				string propertyPath = GetPathString(path, start, count) + propertyName;
				DetectedInvalidPropertyPaths.Add(propertyPath);
			}
		}
	}
	class NodeCriteriaPatcherItem {
		public readonly Guid PatchOperatorId;
		public readonly CriteriaOperator OrPatch;
		public readonly CriteriaOperator AndPatch;
		public NodeCriteriaPatcherItem(Guid patchOperatorId, CriteriaOperator orPatch, CriteriaOperator andPatch) {
			this.PatchOperatorId = patchOperatorId;
			this.OrPatch = orPatch;
			this.AndPatch = andPatch;
		}
	}
	class NodeCriteriaPatcher : ClientCriteriaVisitorBase, IAnalyzeCriteriaVisitor<CriteriaOperator> {
		Guid currentNodeId;
		AnalyzeNodePathItem[] currentAnalyzePathItems;
		Dictionary<Guid, NodeCriteriaPatcherItem> patchItemDict = new Dictionary<Guid, NodeCriteriaPatcherItem>();
		public NodeCriteriaPatcher(Guid patchOperatorId, CriteriaOperator orPatch, CriteriaOperator andPatch) {
			patchItemDict.Add(patchOperatorId, new NodeCriteriaPatcherItem(patchOperatorId, orPatch, andPatch));
		}
		public NodeCriteriaPatcher(IEnumerable patchItems) {
			if(patchItems == null) return;
			foreach(NodeCriteriaPatcherItem item in patchItems) {
				patchItemDict.Add(item.PatchOperatorId, item);
			}
		}
		public NodeCriteriaPatcher(IEnumerable<NodeCriteriaPatcherItem> patchItems) {
			if(patchItems == null) return;
			foreach(NodeCriteriaPatcherItem item in patchItems) {
				patchItemDict.Add(item.PatchOperatorId, item);
			}
		}
		public static CriteriaOperator Patch(CriteriaOperator op, Guid patchOperatorId, CriteriaOperator orPatch, CriteriaOperator andPatch) {
			return new NodeCriteriaPatcher(patchOperatorId, orPatch, andPatch).Process(op);
		}
		CriteriaOperator IAnalyzeCriteriaVisitor<CriteriaOperator>.Visit(AnalyzeOperator criteria) {
			object o;
			if(criteria.AnalyzeInfo.TryGetValue(ReverseCriteriaAnalyzer.NodePathItemsKey, out o)) {
				currentAnalyzePathItems = (AnalyzeNodePathItem[])o;
			}
			else currentAnalyzePathItems = null;
			currentNodeId = (Guid)criteria.AnalyzeInfo[AnalyzeCriteriaCreator.OperatorIdKey];
			CriteriaOperator result = Process(criteria.Original);
			NodeCriteriaPatcherItem patchItem;
			if(patchItemDict.TryGetValue(currentNodeId, out patchItem)) {
				result = GroupOperator.Or(GroupOperator.And(result, patchItem.AndPatch), patchItem.OrPatch);
			}
			else if(ReferenceEquals(result, criteria.Original)) {
				return criteria;
			}
			return new AnalyzeOperator(result, criteria.AnalyzeInfo);
		}
	}
	public class AnalyzeCriteriaCleaner : ClientCriteriaVisitorBase, IAnalyzeCriteriaVisitor<CriteriaOperator> {
		static AnalyzeCriteriaCleaner instance;
		static AnalyzeCriteriaCleaner Instance {
			get {
				if(AnalyzeCriteriaCleaner.instance == null)
					AnalyzeCriteriaCleaner.instance = new AnalyzeCriteriaCleaner();
				return AnalyzeCriteriaCleaner.instance;
			}
		}
		public static CriteriaOperator Clean(CriteriaOperator op) {
			return Instance.Process(op);
		}
		CriteriaOperator IAnalyzeCriteriaVisitor<CriteriaOperator>.Visit(AnalyzeOperator criteria) {
			return Process(criteria.Original);
		}
	}
	public class FailedToReverseOperator : CriteriaOperator {
		Exception ex;
		public Exception Ex { get { return ex; } }
		public FailedToReverseOperator(Exception ex)
			: base() {
			this.ex = ex;
		}
		public override void Accept(ICriteriaVisitor visitor) {
			throw new NotSupportedException();
		}
		public override T Accept<T>(ICriteriaVisitor<T> visitor) {
			throw new NotSupportedException();
		}
		protected override CriteriaOperator CloneCommon() {
			return new FailedToReverseOperator(ex);
		}
	}
	public class ReverseCriteriaAnalyzer : IAnalyzeCriteriaVisitor<CriteriaOperator> {
		readonly bool removeAnotherModified;
		readonly Session session;
		static public readonly AnalyzeInfoKey NodePathItemsKey = new AnalyzeInfoKey("NodePathItems");
		readonly string baseNodeName;
		readonly AnalyzeResult analyzeResult;
		readonly HashSet<string> modifiedNodesDict;
		string currentNode;
		XPClassInfo originalCriteriaRootClassInfo;
		Guid currentNodeId;
		AnalyzeTransitionInfo currentNodeTransitionInfo;
		XPClassInfo currentNodeClassInfo;
		AnalyzeNodePathItem[] currentAnalyzePathItems;
		bool removeSign;
		bool existsGroupingSign;
		public ReverseCriteriaAnalyzer(string baseNodeName, AnalyzeResult analyzeResult) {
			this.baseNodeName = baseNodeName;
			this.analyzeResult = analyzeResult;
		}
		public ReverseCriteriaAnalyzer(string baseNodeName, AnalyzeResult analyzeResult, Session session, bool removeAnotherModified)
			: this(baseNodeName, analyzeResult) {
			this.session = session;
			this.removeAnotherModified = removeAnotherModified;
			this.modifiedNodesDict = new HashSet<string>();
			string[] modifiedNodes = analyzeResult.GetModifiedNodes(session);
			for(int i = 0; i < modifiedNodes.Length; i++) {
				modifiedNodesDict.Add(modifiedNodes[i]);
			}
			originalCriteriaRootClassInfo = analyzeResult.NodeClassInfoDict["N0"];
		}
		public ReverseCriteriaAnalyzer(string baseNodeName, AnalyzeResult analyzeResult, string[] modifiedNodes, bool removeAnotherModified)
			: this(baseNodeName, analyzeResult) {
			this.removeAnotherModified = removeAnotherModified;
			this.modifiedNodesDict = new HashSet<string>();
			for(int i = 0; i < modifiedNodes.Length; i++) {
				modifiedNodesDict.Add(modifiedNodes[i]);
			}
			originalCriteriaRootClassInfo = analyzeResult.NodeClassInfoDict["N0"];
		}
		public static CriteriaOperator ReverseAndRemoveModified(string baseNodeName, AnalyzeResult analyzeResult, Session session) {
			ICollection<string> invalidProperties;
			CriteriaOperator result = new ReverseCriteriaAnalyzer(baseNodeName, analyzeResult, session, true).Process();
			result = PropertyNameFixCriteriaAnalyzer.Fix(result, out invalidProperties);
			if(invalidProperties.Count > 0) {
				throw new InvalidPropertyPathException(invalidProperties.First());
			}
			return result;
		}
		public static CriteriaOperator ReverseAndRemoveModified(string baseNodeName, AnalyzeResult analyzeResult, string[] modifiedNodes) {
			ICollection<string> invalidProperties;
			CriteriaOperator result = new ReverseCriteriaAnalyzer(baseNodeName, analyzeResult, modifiedNodes, true).Process();
			result = PropertyNameFixCriteriaAnalyzer.Fix(result, out invalidProperties);
			if(invalidProperties.Count > 0) {
				throw new InvalidPropertyPathException(invalidProperties.First());
			}
			return result;
		}
		public static CriteriaOperator Reverse(string baseNodeName, AnalyzeResult analyzeResult) {
			ICollection<string> invalidProperties;
			CriteriaOperator result = new ReverseCriteriaAnalyzer(baseNodeName, analyzeResult).Process();
			result = PropertyNameFixCriteriaAnalyzer.Fix(result, out invalidProperties);
			if(invalidProperties.Count > 0) {
				throw new InvalidPropertyPathException(invalidProperties.First());
			}
			return result;
		}
		public static bool ReverseAndRemoveModifiedNoException(string baseNodeName, AnalyzeResult analyzeResult, Session session, out CriteriaOperator result) {
			ICollection<string> invalidProperties;
			if(!new ReverseCriteriaAnalyzer(baseNodeName, analyzeResult, session, true).ProcessNoException(out result)) return false;
			result = PropertyNameFixCriteriaAnalyzer.Fix(result, out invalidProperties);
			return (invalidProperties.Count == 0);
		}
		public static bool ReverseAndRemoveModifiedNoException(string baseNodeName, AnalyzeResult analyzeResult, string[] modifiedNodes, out CriteriaOperator result) {
			ICollection<string> invalidProperties;
			if(!new ReverseCriteriaAnalyzer(baseNodeName, analyzeResult, modifiedNodes, true).ProcessNoException(out result)) return false;
			result = PropertyNameFixCriteriaAnalyzer.Fix(result, out invalidProperties);
			return (invalidProperties.Count == 0);
		}
		public static bool ReverseNoException(string baseNodeName, AnalyzeResult analyzeResult, out CriteriaOperator result) {
			ICollection<string> invalidProperties;
			if(!new ReverseCriteriaAnalyzer(baseNodeName, analyzeResult).ProcessNoException(out result)) return false;
			result = PropertyNameFixCriteriaAnalyzer.Fix(result, out invalidProperties);
			return (invalidProperties.Count == 0);
		}
		public CriteriaOperator Process() {
			CriteriaOperator result = Process(analyzeResult.ResultOperator);
			if(result is FailedToReverseOperator) throw ((FailedToReverseOperator)result).Ex;
			if(currentAnalyzePathItems != null) {
				result = WrapCollection(currentAnalyzePathItems, result);
				if(result is FailedToReverseOperator) throw ((FailedToReverseOperator)result).Ex;
			}
			return result;
		}
		public bool ProcessNoException(out CriteriaOperator result) {
			result = Process(analyzeResult.ResultOperator);
			if(result is FailedToReverseOperator) {
				return false;
			}
			if(currentAnalyzePathItems != null) {
				result = WrapCollection(currentAnalyzePathItems, result);
				if(result is FailedToReverseOperator) {
					return false;
				}
			}
			return true;
		}
		CriteriaOperator Process(CriteriaOperator criteria) {
			if(ReferenceEquals(criteria, null)) return null;
			return criteria.Accept(this);
		}
		CriteriaOperator IAnalyzeCriteriaVisitor<CriteriaOperator>.Visit(AnalyzeOperator criteria) {
			Guid nodeId = (Guid)criteria.AnalyzeInfo[AnalyzeCriteriaCreator.OperatorIdKey];
			currentNodeId = nodeId;
			object o;
			if(criteria.AnalyzeInfo.TryGetValue(AnalyzeCriteriaCreator.NodeNameKey, out o)) {
				currentNode = (string)o;
			}
			else currentNode = null;
			if(criteria.AnalyzeInfo.TryGetValue(AnalyzeCriteriaCreator.TransitionInfoKey, out o)) {
				currentNodeTransitionInfo = (AnalyzeTransitionInfo)o;
			}
			if(criteria.AnalyzeInfo.TryGetValue(AnalyzeCriteriaCreator.ClassInfoKey, out o)) {
				currentNodeClassInfo = (XPClassInfo)o;
			}
			CriteriaOperator result = Process(criteria.Original);
			if(result is FailedToReverseOperator) return result;
			if(ReferenceEquals(result, null)) return null;
			AnalyzeOperator ao = new AnalyzeOperator(result, criteria.AnalyzeInfo);
			ao.AnalyzeInfo[NodePathItemsKey] = currentAnalyzePathItems;
			return ao;
		}
		CriteriaOperator IClientCriteriaVisitor<CriteriaOperator>.Visit(OperandProperty theOperand) {
			int lastIndex;
			string upcastingClass = string.Empty;
			string propertyName = theOperand.PropertyName;
			if((lastIndex = MemberInfoCollection.LastIndexOfSplittingDotInPath(propertyName)) >= 0) {
				propertyName = propertyName.Substring(lastIndex + 1);
			}
			if((lastIndex = propertyName.LastIndexOf('>')) >= 0) {
				upcastingClass = propertyName.Remove(lastIndex + 1);
				propertyName = propertyName.Substring(lastIndex + 1);
			}
			if(removeAnotherModified && currentNode != baseNodeName && modifiedNodesDict.Contains(currentNode)) {
				XPMemberInfo mi = currentNodeClassInfo.FindMember(propertyName);
				if(mi == null || !mi.IsKey) {
					removeSign = true;
					return null;
				}
			}
			AnalyzeNodePathItem[] pathItems = analyzeResult.GetNodePath(baseNodeName, currentNode);
			if(pathItems == null) {
				return new FailedToReverseOperator(new InvalidOperationException(Res.GetString(Res.CriteriaAnalyzer_PathNotFound, baseNodeName, currentNode)));
			}
			if(removeAnotherModified) {
				foreach(AnalyzeNodePathItem pathItem in pathItems) {
					if(pathItem.Node != baseNodeName && modifiedNodesDict.Contains(pathItem.Node)) {
						removeSign = true;
						return null;
					}
				}
			}
			if(pathItems.Length == 0) {
				currentAnalyzePathItems = null;
			}
			else {
				currentAnalyzePathItems = pathItems;
			}
			if(string.IsNullOrEmpty(upcastingClass)) {
				return new OperandProperty(currentNodeTransitionInfo.MemberInfo.Name);
			}
			return new OperandProperty(string.Concat(upcastingClass, currentNodeTransitionInfo.MemberInfo.Name));
		}
		CriteriaOperator ICriteriaVisitor<CriteriaOperator>.Visit(OperandValue theOperand) {
			currentAnalyzePathItems = null;
			return theOperand is ConstantValue ? new ConstantValue(theOperand.Value) : new OperandValue(theOperand.Value);
		}
		CriteriaOperator IClientCriteriaVisitor<CriteriaOperator>.Visit(JoinOperand theOperand) {
			XPClassInfo savedCurrentNodeClassInfo = currentNodeClassInfo;
			if(theOperand.AggregateType == Aggregate.Exists) {
				if(inNot && currentNode == baseNodeName) return new FailedToReverseOperator(new ArithmeticException());
				CriteriaOperator existsResult = Process(theOperand.Condition);
				existsGroupingSign = true;
				return existsResult;
			}
			if(removeAnotherModified && currentNode != baseNodeName && modifiedNodesDict.Contains(currentNode)) {
				removeSign = true;
				return null;
			}
			AnalyzeNodePathItem[] currentPath = analyzeResult.GetNodePath(baseNodeName, currentNode);
			if(currentPath == null || currentPath.Length == 0) return new FailedToReverseOperator(new ArithmeticException());
			AnalyzeNodePathItem lastPathItem = currentPath[currentPath.Length - 1];
			if(lastPathItem.TransitionInfo.IsMemberInfo
				|| savedCurrentNodeClassInfo != lastPathItem.TransitionInfo.JoinClassInfo) return new FailedToReverseOperator(new ArithmeticException());
			CriteriaOperator condition = Process(theOperand.Condition);
			if(condition is FailedToReverseOperator) return condition;
			JoinOperand resultJoinOperand;
			if(theOperand.AggregateType != Aggregate.Custom) {
				CriteriaOperator aggregatedExpression = Process(theOperand.AggregatedExpression);
				if(removeSign && ReferenceEquals(aggregatedExpression, null)) return null;
				if(aggregatedExpression is FailedToReverseOperator) return aggregatedExpression;
				resultJoinOperand = new JoinOperand(savedCurrentNodeClassInfo.FullName, condition, theOperand.AggregateType, aggregatedExpression);
			}
			else {
				CriteriaOperator[] aggrExprs = new CriteriaOperator[theOperand.CustomAggregateOperands.Count];
				bool hasNotNull = false;
				for(int i = 0; i < theOperand.CustomAggregateOperands.Count; i++) {
					CriteriaOperator aggregatedExpression = Process(theOperand.CustomAggregateOperands[i]);
					if(aggregatedExpression is FailedToReverseOperator) return aggregatedExpression;
					hasNotNull = hasNotNull || !ReferenceEquals(aggregatedExpression, null);
				}
				if(removeSign && aggrExprs.Length > 0 && !hasNotNull) {
					return null;
				}
				resultJoinOperand = new JoinOperand(savedCurrentNodeClassInfo.FullName, condition, theOperand.CustomAggregateName, aggrExprs);
			}
			AnalyzeOperator result = new AnalyzeOperator(resultJoinOperand);
			result.AnalyzeInfo[NodePathItemsKey] = currentPath;
			result.AnalyzeInfo[AnalyzeCriteriaCreator.OperatorIdKey] = Guid.NewGuid();
			currentAnalyzePathItems = null;
			return result;
		}
		CriteriaOperator IClientCriteriaVisitor<CriteriaOperator>.Visit(AggregateOperand theOperand) {
			if(theOperand.AggregateType == Aggregate.Exists) {
				if(inNot && currentNode == baseNodeName) return new FailedToReverseOperator(new ArithmeticException());
				CriteriaOperator existsResult = Process(theOperand.Condition);
				existsGroupingSign = true;
				return existsResult;
			}
			CriteriaOperator propertyResult = Process(theOperand.CollectionProperty);
			if(propertyResult is FailedToReverseOperator) return propertyResult;
			OperandProperty collectionProperty = (OperandProperty)propertyResult;
			if(removeSign && ReferenceEquals(collectionProperty, null)) return null;
			if(currentAnalyzePathItems == null || currentAnalyzePathItems.Length < 1) return new FailedToReverseOperator(new ArithmeticException());
			AnalyzeNodePathItem lastPathItem = currentAnalyzePathItems[currentAnalyzePathItems.Length - 1];
			if(!lastPathItem.TransitionInfo.IsMemberInfo
				|| !lastPathItem.TransitionInfo.MemberInfo.IsAssociationList
					|| lastPathItem.Type != AnalyzeNodePathItemType.Direct) return new FailedToReverseOperator(new ArithmeticException());
			CriteriaOperator condition = Process(theOperand.Condition);
			if(condition is FailedToReverseOperator) return condition;
			AggregateOperand processedAggr;
			if(theOperand.AggregateType != Aggregate.Custom) {
				CriteriaOperator aggregatedExpression = Process(theOperand.AggregatedExpression);
				if(removeSign && ReferenceEquals(aggregatedExpression, null)) return null;
				if(aggregatedExpression is FailedToReverseOperator) return aggregatedExpression;
				processedAggr = new AggregateOperand(collectionProperty, aggregatedExpression, theOperand.AggregateType, condition);
			}
			else {
				CriteriaOperatorCollection operands = new CriteriaOperatorCollection();
				foreach(CriteriaOperator op in theOperand.CustomAggregateOperands) {
					CriteriaOperator aggregatedExpression = Process(op);
					if(removeSign && ReferenceEquals(aggregatedExpression, null)) return null;
					if(aggregatedExpression is FailedToReverseOperator) return op;
					operands.Add(op);
				}
				processedAggr = new AggregateOperand(collectionProperty, operands, theOperand.CustomAggregateName, condition);
			}
			AnalyzeOperator result = new AnalyzeOperator(processedAggr);
			result.AnalyzeInfo[NodePathItemsKey] = currentAnalyzePathItems;
			result.AnalyzeInfo[AnalyzeCriteriaCreator.OperatorIdKey] = Guid.NewGuid();
			currentAnalyzePathItems = null;
			return result;
		}
		CriteriaOperator ICriteriaVisitor<CriteriaOperator>.Visit(GroupOperator theOperator) {
			CriteriaOperatorCollection resultOperands = new CriteriaOperatorCollection();
			List<AnalyzeNodePathItem[]> analyzePathList = new List<AnalyzeNodePathItem[]>();
			int notNullCount = 0;
			int notNullLastIndex = -1;
			bool allRemoveSign = true;
			bool hasExistsSign = false;
			for(int i = 0; i < theOperator.Operands.Count; i++) {
				existsGroupingSign = false;
				CriteriaOperator currentResult = Process(theOperator.Operands[i]);
				if(currentResult is FailedToReverseOperator) return currentResult;
				if(ReferenceEquals(currentResult, null) && (removeSign || theOperator.OperatorType == GroupOperatorType.And)) {
					if(theOperator.OperatorType == GroupOperatorType.Or) {
						return null;
					}
					removeSign = false;
					continue;
				}
				if(existsGroupingSign) {
					existsGroupingSign = false;
					if(hasExistsSign) continue;
					if(theOperator.OperatorType == GroupOperatorType.And) hasExistsSign = true;
				}
				allRemoveSign = false;
				resultOperands.Add(currentResult);
				analyzePathList.Add(currentAnalyzePathItems);
				if(currentAnalyzePathItems != null) {
					notNullLastIndex = i;
					notNullCount++;
				}
			}
			if(notNullCount == 0) {
				currentAnalyzePathItems = null;
				if(resultOperands.Count == 0) {
					if(allRemoveSign) {
						removeSign = true;
					}
					return null;
				}
				if(resultOperands.Count == 1) {
					return resultOperands[0];
				}
				return FillAnalyzeInfoForGroup(resultOperands, new GroupOperator(theOperator.OperatorType, resultOperands));
			}
			return GroupLevelProcess(theOperator.OperatorType, resultOperands, analyzePathList, Array.Empty<AnalyzeNodePathItem>(), false);
		}
		CriteriaOperator GroupLevelProcess(GroupOperatorType operatorType, CriteriaOperatorCollection resultOperands, List<AnalyzeNodePathItem[]> analyzePathList, AnalyzeNodePathItem[] additionalPath, bool forceCollections) {
			int collectionExistsCount = 0;
			string collectionExistsLastGroup = string.Empty;
			Dictionary<string, List<int>> firstCollectionPathGroups = new Dictionary<string, List<int>>();
			List<string> firstCollectionPathList = new List<string>();
			List<int> firstCollectionPosList = new List<int>();
			for(int pathIndex = 0; pathIndex < analyzePathList.Count; pathIndex++) {
				AnalyzeNodePathItem[] path = analyzePathList[pathIndex];
				bool firstCollectionFound = false;
				int firstCollectionPos = 0;
				StringBuilder firstCollectionPath = new StringBuilder();
				if(path != null) {
					for(int itemIndex = 0; itemIndex < path.Length; itemIndex++) {
						if(!firstCollectionFound) {
							firstCollectionPath.Append(path[itemIndex].ToString());
						}
						if(!path[itemIndex].TransitionInfo.IsMemberInfo
							|| ((path[itemIndex].TransitionInfo.MemberInfo.IsAssociationList && path[itemIndex].Type == AnalyzeNodePathItemType.Direct) ||
								(!path[itemIndex].TransitionInfo.MemberInfo.IsAssociationList && path[itemIndex].Type == AnalyzeNodePathItemType.Reverse))) {
							if(!firstCollectionFound) {
								firstCollectionPos = itemIndex;
								firstCollectionFound = true;
							}
						}
					}
				}
				string firstCollectionPathString = firstCollectionFound ? firstCollectionPath.ToString() : string.Empty;
				List<int> groupIndexList;
				if(!firstCollectionPathGroups.TryGetValue(firstCollectionPathString, out groupIndexList)) {
					groupIndexList = new List<int>();
					firstCollectionPathGroups.Add(firstCollectionPathString, groupIndexList);
					if(firstCollectionFound) {
						collectionExistsCount++;
						collectionExistsLastGroup = firstCollectionPathString;
					}
				}
				groupIndexList.Add(pathIndex);
				firstCollectionPathList.Add(firstCollectionPathString);
				firstCollectionPosList.Add(firstCollectionPos);
			}
			currentAnalyzePathItems = null;
			if(collectionExistsCount == 0) {
				if(resultOperands.Count == 1) {
					return resultOperands[0];
				}
				return FillAnalyzeInfoForGroup(resultOperands, new GroupOperator(operatorType, resultOperands));
			}
			CriteriaOperatorCollection additionalyGroppedResult = new CriteriaOperatorCollection();
			bool currentForceCollection = forceCollections || (firstCollectionPathGroups.Count > 1);
			foreach(KeyValuePair<string, List<int>> pair in firstCollectionPathGroups) {
				List<int> group = pair.Value;
				if(string.IsNullOrEmpty(pair.Key)) {
					for(int i = 0; i < group.Count; i++) {
						additionalyGroppedResult.Add(resultOperands[group[i]]);
					}
					continue;
				}
				CriteriaOperator groupPrepareResult = GroupLevelProcessPrepare(group, operatorType, resultOperands, analyzePathList, firstCollectionPosList, additionalPath, currentForceCollection);
				if(groupPrepareResult is FailedToReverseOperator) return groupPrepareResult;
				additionalyGroppedResult.Add(groupPrepareResult);
			}
			if(additionalyGroppedResult.Count == 1) {
				return additionalyGroppedResult[0];
			}
			currentAnalyzePathItems = null;
			return FillAnalyzeInfoForGroup(additionalyGroppedResult, new GroupOperator(operatorType, additionalyGroppedResult));
		}
		static CriteriaOperator FillAnalyzeInfoForGroup(CriteriaOperatorCollection resultOperands, GroupOperator result) {
			string nodeDetected = null;
			XPClassInfo nodeClassInfoDetected = null;
			foreach(CriteriaOperator criteria in resultOperands) {
				AnalyzeOperator analyzeOperator = criteria as AnalyzeOperator;
				if(ReferenceEquals(analyzeOperator, null)) continue;
				object analyzeInfo;
				if(analyzeOperator.AnalyzeInfo.TryGetValue(AnalyzeCriteriaCreator.NodeNameKey, out analyzeInfo)) {
					if(analyzeInfo == null || !(analyzeInfo is string)) continue;
					if(nodeDetected == null) {
						nodeDetected = (string)analyzeInfo;
						if(analyzeOperator.AnalyzeInfo.TryGetValue(AnalyzeCriteriaCreator.ClassInfoKey, out analyzeInfo) && analyzeInfo != null && analyzeInfo is XPClassInfo) {
							nodeClassInfoDetected = (XPClassInfo)analyzeInfo;
						}
						continue;
					}
					if(nodeDetected != (string)analyzeInfo) {
						nodeDetected = null;
						break;
					}
				}
			}
			if(nodeClassInfoDetected == null) {
				return result;
			}
			return new AnalyzeOperator<GroupOperator>(result, AnalyzeCriteriaCreator.CreateAnalyzeInfo(nodeClassInfoDetected, nodeDetected));
		}
		CriteriaOperator GroupLevelProcessPrepare(List<int> group, GroupOperatorType operatorType, CriteriaOperatorCollection allOperands, List<AnalyzeNodePathItem[]> allAnalyzePathList, List<int> firstCollectionPosList, AnalyzeNodePathItem[] additionalPath, bool forceCollections) {
			CriteriaOperatorCollection operands = new CriteriaOperatorCollection(group.Count);
			List<AnalyzeNodePathItem[]> analyzePathList = new List<AnalyzeNodePathItem[]>(group.Count);
			List<int> firstCollectionShiftList = new List<int>(group.Count);
			for(int i = 0; i < group.Count; i++) {
				int pathIndex = group[i];
				AnalyzeNodePathItem[] path = allAnalyzePathList[pathIndex];
				int firstCollectionPos = firstCollectionPosList[pathIndex];
				var firstCollection = firstCollectionPos < (path.Length - 1) ? path[firstCollectionPos + 1] : null;
				int firstCollectionLoopShift = firstCollectionPos + (firstCollection != null && firstCollection.TransitionInfo.CantWrapCollection ? 2 : 1);
				AnalyzeNodePathItem[] newPath = new AnalyzeNodePathItem[path.Length - firstCollectionLoopShift];
				for(int itemIndex = firstCollectionLoopShift; itemIndex < path.Length; itemIndex++) {
					newPath[itemIndex - firstCollectionLoopShift] = path[itemIndex];
				}
				operands.Add(allOperands[pathIndex]);
				analyzePathList.Add(newPath);
				firstCollectionShiftList.Add(firstCollectionLoopShift);
			}
			AnalyzeNodePathItem[] collectionPath = SlicePathHard(allAnalyzePathList[group[0]], firstCollectionShiftList[0]);
			AnalyzeNodePathItem[] newAdditionalPath = MergePaths(additionalPath, collectionPath);
			CriteriaOperator result = GroupLevelProcess(operatorType, operands, analyzePathList, newAdditionalPath, true);
			if(!(result is FailedToReverseOperator)) {
				currentAnalyzePathItems = newAdditionalPath;
				if(forceCollections) {
					return WrapCollection(newAdditionalPath, result);
				}
			}
			return result;
		}
		static CriteriaOperator WrapCollection(AnalyzeNodePathItem[] collectionPath, CriteriaOperator criteria) {
			for(int collectionIndex = collectionPath.Length - 1; collectionIndex >= 0; collectionIndex--) {
				AnalyzeNodePathItem collectionItem = collectionPath[collectionIndex];
				CriteriaOperator result;
				if(collectionItem.TransitionInfo.IsMemberInfo) {
					XPMemberInfo mi = collectionItem.TransitionInfo.MemberInfo;
					if(mi.IsAssociationList && collectionItem.Type == AnalyzeNodePathItemType.Direct) {
						result = new AggregateOperand(new OperandProperty(mi.Name), null, Aggregate.Exists, criteria);
					}
					else if(!mi.IsAssociationList && collectionItem.Type == AnalyzeNodePathItemType.Reverse) {
						if(mi.Owner.KeyProperty == null) return new FailedToReverseOperator(new ArithmeticException());
						XPClassInfo owner = mi.Owner;
						AnalyzeOperator aoMember;
						AnalyzeOperator aoRefKey;
						if(mi.ReferenceType == null) {
							if(mi.Name == "This") {
								continue;
							}
							else
								return new FailedToReverseOperator(new ArithmeticException());
						}
						else {
							using(LohPooled.OrdinaryList<string> fallbackList = new LohPooled.OrdinaryList<string>(collectionPath.Length)) {
								if(collectionIndex > 0) {
									int currentCollectionIndex = collectionIndex - 1;
									while(currentCollectionIndex >= 0) {
										var currentCollectionItem = collectionPath[currentCollectionIndex];
										var currentTransitionInfo = currentCollectionItem.TransitionInfo;
										if(!currentTransitionInfo.IsMemberInfo || currentTransitionInfo.MemberInfo.ReferenceType == null || currentCollectionItem.Type == AnalyzeNodePathItemType.Reverse) {
											break;
										}
										fallbackList.Add(currentTransitionInfo.UpCasting ? string.Concat("<", currentTransitionInfo.MemberInfo.Owner.FullName, ">", currentTransitionInfo.MemberInfo.Name) : currentTransitionInfo.MemberInfo.Name);
										currentCollectionIndex--;
									}
								}
								aoMember = new AnalyzeOperator(new OperandProperty(string.Concat(mi.Name, ".", mi.ReferenceType.KeyProperty.Name)));
								if(fallbackList.Count == 0) {
									aoRefKey = new AnalyzeOperator(new OperandProperty(mi.ReferenceType.KeyProperty.Name));
								}
								else {
									fallbackList.Add(mi.ReferenceType.KeyProperty.Name);
									aoRefKey = new AnalyzeOperator(new OperandProperty(string.Join(".", fallbackList)));
								}
							}
						}
						aoRefKey.AnalyzeInfo[NodePathItemsKey] = SlicePathHard(collectionPath, collectionPath.Length - 1);
						aoRefKey.AnalyzeInfo[AnalyzeCriteriaCreator.OperatorIdKey] = Guid.NewGuid();
						aoMember.AnalyzeInfo[NodePathItemsKey] = collectionPath;
						aoMember.AnalyzeInfo[AnalyzeCriteriaCreator.OperatorIdKey] = Guid.NewGuid();
						AnalyzeOperator analyzedCriteria = criteria as AnalyzeOperator;
						string joinTypeString;
						object analyzedCriteriaClassInfoObject;
						if(!ReferenceEquals(analyzedCriteria, null) && analyzedCriteria.AnalyzeInfo.TryGetValue(AnalyzeCriteriaCreator.ClassInfoKey, out analyzedCriteriaClassInfoObject)
							&& (analyzedCriteriaClassInfoObject != null) && (analyzedCriteriaClassInfoObject is XPClassInfo) && (((XPClassInfo)analyzedCriteriaClassInfoObject).FindMember(mi.Name) != null)) {
							joinTypeString = ((XPClassInfo)analyzedCriteriaClassInfoObject).FullName;
						}
						else {
							joinTypeString = owner.FullName;
						}
						result = new JoinOperand(joinTypeString,
							GroupOperator.And(criteria, new BinaryOperator(aoRefKey, aoMember, BinaryOperatorType.Equal)), Aggregate.Exists, null);
					}
					else if(mi.ReferenceType != null && collectionItem.Type == AnalyzeNodePathItemType.Direct) {
						continue;
					}
					else return new FailedToReverseOperator(new ArithmeticException());
					AnalyzeOperator aoResult = new AnalyzeOperator(result);
					aoResult.AnalyzeInfo[NodePathItemsKey] = SlicePathHard(collectionPath, collectionIndex + 1);
					aoResult.AnalyzeInfo[AnalyzeCriteriaCreator.OperatorIdKey] = Guid.NewGuid();
					return aoResult;
				}
				else {
					result = new JoinOperand(collectionItem.TransitionInfo.JoinClassInfo.FullName, criteria, Aggregate.Exists, null);
					AnalyzeOperator aoResult = new AnalyzeOperator(result);
					aoResult.AnalyzeInfo[NodePathItemsKey] = SlicePathHard(collectionPath, collectionIndex + 1);
					aoResult.AnalyzeInfo[AnalyzeCriteriaCreator.OperatorIdKey] = collectionItem.TransitionInfo.JoinOperandId;
					return aoResult;
				}
			}
			return criteria;
		}
		CriteriaOperator ICriteriaVisitor<CriteriaOperator>.Visit(InOperator theOperator) {
			CriteriaOperatorCollection operands = new CriteriaOperatorCollection();
			operands.Add(theOperator.LeftOperand);
			operands.AddRange(theOperator.Operands);
			FailedToReverseOperator failedOperator;
			CriteriaOperatorCollection resultOperands = ProcessPathSimple(operands, out failedOperator);
			if(!ReferenceEquals(failedOperator, null)) return failedOperator;
			if(resultOperands == null) return null;
			return new InOperator(resultOperands[0], resultOperands.GetRange(1, resultOperands.Count - 1));
		}
		bool inNot = false;
		CriteriaOperator ICriteriaVisitor<CriteriaOperator>.Visit(UnaryOperator theOperator) {
			inNot = theOperator.OperatorType == UnaryOperatorType.Not ? !inNot : inNot;
			try {
				CriteriaOperator result = Process(theOperator.Operand);
				if(result is FailedToReverseOperator) return result;
				if(ReferenceEquals(result, null)) {
					return null;
				}
				return new UnaryOperator(theOperator.OperatorType, result);
			}
			finally {
				inNot = theOperator.OperatorType == UnaryOperatorType.Not ? !inNot : inNot;
			}
		}
		CriteriaOperator ICriteriaVisitor<CriteriaOperator>.Visit(FunctionOperator theOperator) {
			FailedToReverseOperator failedOperator;
			CriteriaOperatorCollection resultOperands = ProcessPathSimple(theOperator.Operands, out failedOperator);
			if(!ReferenceEquals(failedOperator, null)) return failedOperator;
			if(resultOperands == null) return null;
			return new FunctionOperator(theOperator.OperatorType, resultOperands);
		}
		CriteriaOperatorCollection ProcessPathSimple(CriteriaOperatorCollection operands, out FailedToReverseOperator failedOperator) {
			failedOperator = null;
			CriteriaOperatorCollection resultOperands = new CriteriaOperatorCollection();
			List<AnalyzeNodePathItem[]> analyzePathList = new List<AnalyzeNodePathItem[]>();
			int notNullCount = 0;
			int notNullLastIndex = -1;
			int minAnalyzePathLength = -1;
			int maxAnalyzePathLength = -1;
			for(int i = 0; i < operands.Count; i++) {
				CriteriaOperator currentResult = Process(operands[i]);
				if(currentResult is FailedToReverseOperator) {
					failedOperator = (FailedToReverseOperator)currentResult;
					return null;
				}
				if(removeSign && ReferenceEquals(currentResult, null)) {
					return null;
				}
				resultOperands.Add(currentResult);
				analyzePathList.Add(currentAnalyzePathItems);
				if(currentAnalyzePathItems != null) {
					notNullLastIndex = i;
					notNullCount++;
					if(minAnalyzePathLength < 0 || currentAnalyzePathItems.Length < minAnalyzePathLength) {
						minAnalyzePathLength = currentAnalyzePathItems.Length;
					}
					if(maxAnalyzePathLength < 0 || currentAnalyzePathItems.Length > maxAnalyzePathLength) {
						maxAnalyzePathLength = currentAnalyzePathItems.Length;
					}
				}
			}
			if(notNullCount == 0) {
				currentAnalyzePathItems = null;
				return resultOperands;
			}
			if(notNullCount == 1) {
				currentAnalyzePathItems = analyzePathList[notNullLastIndex]; 
				return resultOperands;
			}
			int lastEquals = -1;
			for(int itemIndex = 0; itemIndex < minAnalyzePathLength; itemIndex++) {
				bool allEquals = true;
				for(int pathIndex = 1; pathIndex < analyzePathList.Count; pathIndex++) {
					var current = analyzePathList[pathIndex];
					var previous = analyzePathList[pathIndex - 1];
					if(current != previous && (current == null || previous == null || !object.Equals(current[itemIndex].TransitionInfo, previous[itemIndex].TransitionInfo))) {
						allEquals = false;
						break;
					}
				}
				if(allEquals) {
					lastEquals = itemIndex;
					continue;
				}
				break;
			}
			if((lastEquals == minAnalyzePathLength - 1) && (lastEquals == maxAnalyzePathLength - 1)) {
				currentAnalyzePathItems = analyzePathList[0];
				return resultOperands;
			}
			int collectionExistsCount = 0;
			int collectionExistsLastIndex = -1;
			for(int pathIndex = 0; pathIndex < analyzePathList.Count; pathIndex++) {
				var path = analyzePathList[pathIndex];
				int pathLength = path == null ? 0 : path.Length;
				for(int itemIndex = lastEquals + 1; itemIndex < pathLength; itemIndex++) {
					if(!path[itemIndex].TransitionInfo.IsMemberInfo
						|| ((path[itemIndex].TransitionInfo.MemberInfo.IsAssociationList && path[itemIndex].Type == AnalyzeNodePathItemType.Direct) ||
							(!path[itemIndex].TransitionInfo.MemberInfo.IsAssociationList && path[itemIndex].Type == AnalyzeNodePathItemType.Reverse))) {
						if(collectionExistsCount > 0) {
							failedOperator = new FailedToReverseOperator(new ArithmeticException());
							return null;
						}
						collectionExistsCount++;
						collectionExistsLastIndex = pathIndex;
					}
				}
			}
			if(collectionExistsCount == 0) {
				currentAnalyzePathItems = SlicePath(analyzePathList[0]);
				return resultOperands;
			}
			currentAnalyzePathItems = SlicePath(analyzePathList[collectionExistsLastIndex]); 
			return resultOperands;
		}
		CriteriaOperator ICriteriaVisitor<CriteriaOperator>.Visit(BinaryOperator theOperator) {
			CriteriaOperatorCollection operands = new CriteriaOperatorCollection();
			operands.Add(theOperator.LeftOperand);
			operands.Add(theOperator.RightOperand);
			FailedToReverseOperator failedOperator;
			CriteriaOperatorCollection resultOperands = ProcessPathSimple(operands, out failedOperator);
			if(!ReferenceEquals(failedOperator, null)) return failedOperator;
			if(resultOperands == null) return null;
			return new BinaryOperator(resultOperands[0], resultOperands[1], theOperator.OperatorType);
		}
		static AnalyzeNodePathItem[] SlicePathHard(AnalyzeNodePathItem[] pathItems, int sliceCount) {
			if(sliceCount < 0 || sliceCount > pathItems.Length)
				throw new ArgumentException(null, nameof(pathItems));
			AnalyzeNodePathItem[] newPath = new AnalyzeNodePathItem[sliceCount];
			for(int i = 0; i < sliceCount; i++) {
				newPath[i] = pathItems[i];
			}
			return newPath;
		}
		static AnalyzeNodePathItem[] SlicePath(AnalyzeNodePathItem[] pathItems) {
			List<AnalyzeNodePathItem> collectionSlice = new List<AnalyzeNodePathItem>();
			for(int i = pathItems.Length - 1; i >= 0; i--) {
				AnalyzeNodePathItem currentPathItem = pathItems[i];
				if(!currentPathItem.TransitionInfo.IsMemberInfo
					|| ((currentPathItem.TransitionInfo.MemberInfo.IsAssociationList && currentPathItem.Type == AnalyzeNodePathItemType.Direct) ||
						(!currentPathItem.TransitionInfo.MemberInfo.IsAssociationList && currentPathItem.Type == AnalyzeNodePathItemType.Reverse))) {
					for(int j = 0; j <= i; j++) {
						collectionSlice.Add(pathItems[j]);
					}
					break;
				}
			}
			return collectionSlice.Count > 0 ? collectionSlice.ToArray() : null;
		}
		static AnalyzeNodePathItem[] MergePaths(AnalyzeNodePathItem[] left, AnalyzeNodePathItem[] right) {
			int leftLength = left == null ? 0 : left.Length;
			int rightLength = right == null ? 0 : right.Length;
			int resultLength = leftLength + rightLength;
			AnalyzeNodePathItem[] result = new AnalyzeNodePathItem[resultLength];
			if(resultLength == 0) return result;
			if(left != null && leftLength > 0) {
				Array.Copy(left, 0, result, 0, leftLength);
			}
			if(right != null && rightLength > 0) {
				Array.Copy(right, 0, result, leftLength, rightLength);
			}
			return result;
		}
		CriteriaOperator ICriteriaVisitor<CriteriaOperator>.Visit(BetweenOperator theOperator) {
			CriteriaOperatorCollection operands = new CriteriaOperatorCollection();
			operands.Add(theOperator.BeginExpression);
			operands.Add(theOperator.TestExpression);
			operands.Add(theOperator.EndExpression);
			FailedToReverseOperator failedOperator;
			CriteriaOperatorCollection resultOperands = ProcessPathSimple(operands, out failedOperator);
			if(!ReferenceEquals(failedOperator, null)) return failedOperator;
			if(resultOperands == null) return null;
			return new BetweenOperator(resultOperands[1], resultOperands[0], resultOperands[2]);
		}
	}
	public class ComplexityAnalyzer : ContextClientCriteriaVisitorBase<int> {
		public override int Process(CriteriaOperator criteria) {
			if(ReferenceEquals(criteria, null)) return 0;
			return criteria.Accept(this);
		}
		public int ProcessFixProperty(CriteriaOperator criteria) {
			if(ReferenceEquals(criteria, null)) return 0;
			int result = (int)criteria.Accept(this);
			if(result < 0) return 2;
			return result;
		}
		public ComplexityAnalyzer(XPClassInfo classInfo)
			: base(classInfo) {
		}
		public ComplexityAnalyzer(XPClassInfo[] upLevels)
			: base(upLevels) {
		}
		public static int Analyze(XPClassInfo classInfo, CriteriaOperator criteria) {
			return new ComplexityAnalyzer(classInfo).Process(criteria);
		}
		public static int Analyze(XPClassInfo[] upLevels, CriteriaOperator criteria) {
			return new ComplexityAnalyzer(upLevels).Process(criteria);
		}
		public static int Analyze(AnalyzeNodePathItem[] path) {
			if(path == null) return -1;
			int accumulator = 1;
			int reversWorth = 4;
			for(int i = 0; i < path.Length; i++) {
				AnalyzeNodePathItem pathItem = path[i];
				if(pathItem.TransitionInfo.IsMemberInfo) {
					if(pathItem.TransitionInfo.MemberInfo.IsAssociationList) {
						if(pathItem.TransitionInfo.MemberInfo.IsManyToManyAlias
							|| pathItem.TransitionInfo.MemberInfo.IsManyToMany
							) return -1;
						if(pathItem.Type != AnalyzeNodePathItemType.Direct) {
							accumulator += reversWorth;
							if(reversWorth > 1) reversWorth = reversWorth / 2;
							else reversWorth = 1;
						}
					}
					else {
						if(pathItem.Type != AnalyzeNodePathItemType.Direct) {
							accumulator += reversWorth;
							if(reversWorth > 1) reversWorth = reversWorth / 2;
							else reversWorth = 1;
						}
					}
				}
				else {
					return -1;
				}
			}
			return accumulator;
		}
		public override int VisitInternalJoinOperand(int conditionResult, int agregatedResult, Aggregate aggregateType) {
			int accumulator = 0;
			accumulator += 1;
			accumulator += conditionResult;
			accumulator += agregatedResult;
			return accumulator;
		}
		public override int VisitInternalJoinOperand(int conditionResult, IEnumerable<int> agregatedResult, string customAggregateName) {
			return VisitInternalJoinOperand(conditionResult, agregatedResult.Sum(), Aggregate.Custom);
		}
		public override int VisitInternalAggregate(int collectionPropertyResult, int aggregateResult, Aggregate aggregateType, int conditionResult) {
			int accumulator = 1;
			accumulator += aggregateResult;
			accumulator += conditionResult;
			return accumulator;
		}
		public override int VisitInternalAggregate(int collectionPropertyResult, IEnumerable<int> aggregateResult, string customAggregateName, int conditionResult) {
			return VisitInternalAggregate(collectionPropertyResult, aggregateResult.Sum(), Aggregate.Custom, conditionResult);
		}
		public override int VisitInternalFunction(FunctionOperator theOperator) {
			int accumulator = 2;
			accumulator += ProcessOperands(theOperator.Operands);
			return accumulator;
		}
		public override int VisitInternalUnary(UnaryOperator theOperator) {
			return 2 + ProcessFixProperty(theOperator.Operand);
		}
		public override int VisitInternalBetween(BetweenOperator theOperator) {
			return 2 + ProcessFixProperty(theOperator.BeginExpression) + ProcessFixProperty(theOperator.TestExpression) + ProcessFixProperty(theOperator.EndExpression);
		}
		public override int VisitInternalInOperator(InOperator theOperator) {
			int accumulator = 2;
			int leftResult = (int)Process(theOperator.LeftOperand);
			accumulator += leftResult < 0 ? -leftResult : leftResult;
			accumulator += ProcessOperands(theOperator.Operands);
			return accumulator;
		}
		public override int VisitInternalBinary(int leftResult, int rightResult, BinaryOperatorType operatorType) {
			if(operatorType == BinaryOperatorType.Equal) {
				if(leftResult < 0) leftResult = -leftResult;
				if(rightResult < 0) rightResult = -rightResult;
			}
			else {
				if(leftResult < 0) leftResult = 3;
				if(rightResult < 0) rightResult = 3;
			}
			return 2 + Math.Max(leftResult, rightResult);
		}
		XPClassInfo xpObjectTypeClassInfo;
		XPClassInfo XPObjectTypeClassInfo {
			get {
				if(xpObjectTypeClassInfo == null) {
					xpObjectTypeClassInfo = CurrentClassInfo.Dictionary.QueryClassInfo(typeof(XPObjectType));
				}
				return xpObjectTypeClassInfo;
			}
		}
		public override int VisitInternalProperty(string propertyName) {
			MemberInfoCollection mic = CurrentClassInfo.ParsePath(propertyName);
			if(mic.Count >= 1) {
				XPMemberInfo mi = mic[mic.Count - 1];
				if(mi.ReferenceType != null && mi.ReferenceType != XPObjectTypeClassInfo) {
					return HasJoinOperandOnStack ? -15 : -200;
				}
				if(mi.IsKey) {
					if(mic.Count == 1) {
						return HasJoinOperandOnStack ? -30 : -400;
					}
					else {
						if(mic[mic.Count - 2].ReferenceType != XPObjectTypeClassInfo) {
							return HasJoinOperandOnStack ? -15 : -200;
						}
					}
				}
			}
			return 2;
		}
		public override int VisitInternalOperand(object value) {
			return 1;
		}
		public override int VisitInternalGroup(GroupOperatorType operatorType, List<int> results) {
			if(operatorType == GroupOperatorType.Or) {
				int min = Int32.MaxValue;
				foreach(int result in results) {
					if(result < 0) return 3;
					if(result < min) min = result;
				}
				return min;
			}
			int accumulator = 0;
			foreach(int result in results) {
				if(result < 0) accumulator += 2;
				accumulator += result;
			}
			return accumulator;
		}
		public int ProcessOperands(CriteriaOperatorCollection operands) {
			int accumulator = 0;
			foreach(int result in ProcessCollection(operands)) {
				if(result < 0) accumulator += 3;
				accumulator += result;
			}
			return accumulator;
		}
	}
	public enum AnalyzeNodePathItemType {
		Direct,
		Reverse
	}
	public class AnalyzeNodePathItem {
		public readonly AnalyzeTransitionInfo TransitionInfo;
		public readonly AnalyzeNodePathItemType Type;
		public readonly string Node;
		public AnalyzeNodePathItem(AnalyzeTransitionInfo transitionInfo, string node)
			: this(transitionInfo, node, AnalyzeNodePathItemType.Direct) {
		}
		public AnalyzeNodePathItem(AnalyzeTransitionInfo transitionInfo, string node, AnalyzeNodePathItemType type) {
			TransitionInfo = transitionInfo;
			Type = type;
			Node = node;
		}
		public override string ToString() {
			return Node + (Type == AnalyzeNodePathItemType.Direct ? "->" : "-<") + TransitionInfo.ToString();
		}
		public override bool Equals(object obj) {
			AnalyzeNodePathItem item = obj as AnalyzeNodePathItem;
			if(item == null) return false;
			return TransitionInfo.Equals(item.TransitionInfo) && Type == item.Type;
		}
		public override int GetHashCode() {
			return HashCodeHelper.CalculateGeneric(TransitionInfo, Type);
		}
	}
	public class AnalyzeResult {
		public readonly AnalyzeOperator ResultOperator;
		public readonly Dictionary<string, Dictionary<AnalyzeTransitionInfo, string>> NodeToNodeMoveDictByMember;
		public readonly Dictionary<string, Dictionary<string, AnalyzeNodePathItem>> NodeToNodeMoveDict;
		public readonly Dictionary<string, XPClassInfo> NodeClassInfoDict;
		public readonly Dictionary<XPClassInfo, List<string>> ClassInfoNodeDict = new Dictionary<XPClassInfo, List<string>>();
		public readonly Dictionary<XPClassInfo, List<string>> AffectedNodeDict;
		public readonly bool TopLevelAggregateDetected;
		public AnalyzeResult(AnalyzeOperator resultOperator, Dictionary<string, Dictionary<AnalyzeTransitionInfo, string>> nodeToNodeMoveDictByMember, Dictionary<string, XPClassInfo> nodeClassInfoDict, bool topLevelAggregateDetected) {
			this.ResultOperator = resultOperator;
			this.NodeClassInfoDict = nodeClassInfoDict;
			this.NodeToNodeMoveDictByMember = nodeToNodeMoveDictByMember;
			this.NodeToNodeMoveDict = new Dictionary<string, Dictionary<string, AnalyzeNodePathItem>>();
			this.TopLevelAggregateDetected = topLevelAggregateDetected;
			foreach(KeyValuePair<string, Dictionary<AnalyzeTransitionInfo, string>> nodeToNodePair in NodeToNodeMoveDictByMember) {
				Dictionary<string, AnalyzeNodePathItem> nodeDict;
				string srcNodeName = nodeToNodePair.Key;
				if(!NodeToNodeMoveDict.TryGetValue(srcNodeName, out nodeDict)) {
					nodeDict = new Dictionary<string, AnalyzeNodePathItem>();
					NodeToNodeMoveDict.Add(srcNodeName, nodeDict);
				}
				foreach(KeyValuePair<AnalyzeTransitionInfo, string> nodePair in nodeToNodePair.Value) {
					var destNodeName = nodePair.Value;
					var destNodeTransitionInfo = nodePair.Key;
					nodeDict.Add(destNodeName, new AnalyzeNodePathItem(destNodeTransitionInfo, srcNodeName));
					if(destNodeTransitionInfo.IsMemberInfo) {
						XPMemberInfo associatedMember = null;
						if(destNodeTransitionInfo.MemberInfo.IsAssociation && (associatedMember = destNodeTransitionInfo.MemberInfo.GetAssociatedMember()) != null && (((associatedMember.IsAssociationList && associatedMember.CollectionElementType == NodeClassInfoDict[srcNodeName])
								|| (associatedMember.ReferenceType != null && associatedMember.ReferenceType == NodeClassInfoDict[srcNodeName])))) {
							if(destNodeTransitionInfo.CantWrapCollection) {
								continue;
							}
							if(destNodeTransitionInfo.MemberInfo.IsManyToMany) {
								var intermediateClassInfo = destNodeTransitionInfo.MemberInfo.IntermediateClass;
								if(AffectedNodeDict == null) {
									AffectedNodeDict = new Dictionary<XPClassInfo, List<string>>();
								}
								List<string> affectedNodes;
								if(!AffectedNodeDict.TryGetValue(intermediateClassInfo, out affectedNodes)) {
									affectedNodes = new List<string>();
									AffectedNodeDict.Add(intermediateClassInfo, affectedNodes);
								}
								affectedNodes.Add(destNodeName);
								affectedNodes.Add(srcNodeName);
							}
							Dictionary<string, AnalyzeNodePathItem> reverseNodeDict;
							if(!NodeToNodeMoveDict.TryGetValue(destNodeName, out reverseNodeDict)) {
								reverseNodeDict = new Dictionary<string, AnalyzeNodePathItem>();
								NodeToNodeMoveDict.Add(destNodeName, reverseNodeDict);
							}
							reverseNodeDict.Add(srcNodeName, new AnalyzeNodePathItem(new AnalyzeTransitionInfo(associatedMember, false), destNodeName));
						}
						else {
							if(destNodeTransitionInfo.MemberInfo.ReferenceType != null || destNodeTransitionInfo.MemberInfo.Name == "This") {
								Dictionary<string, AnalyzeNodePathItem> reverseNodeDict;
								if(!NodeToNodeMoveDict.TryGetValue(destNodeName, out reverseNodeDict)) {
									reverseNodeDict = new Dictionary<string, AnalyzeNodePathItem>();
									NodeToNodeMoveDict.Add(destNodeName, reverseNodeDict);
								}
								reverseNodeDict.Add(srcNodeName, new AnalyzeNodePathItem(destNodeTransitionInfo, srcNodeName, AnalyzeNodePathItemType.Reverse));
							}
						}
					}
					else {
						Dictionary<string, AnalyzeNodePathItem> reverseNodeDict;
						if(!NodeToNodeMoveDict.TryGetValue(destNodeName, out reverseNodeDict)) {
							reverseNodeDict = new Dictionary<string, AnalyzeNodePathItem>();
							NodeToNodeMoveDict.Add(destNodeName, reverseNodeDict);
						}
						XPClassInfo newClassInfo = NodeClassInfoDict[srcNodeName];
						if(destNodeTransitionInfo.CantWrapCollection) {
							continue;
						}
						reverseNodeDict.Add(srcNodeName, new AnalyzeNodePathItem(new AnalyzeTransitionInfo(Guid.NewGuid(), newClassInfo, false), destNodeName));
					}
				}
			}
			foreach(KeyValuePair<string, XPClassInfo> nodePair in NodeClassInfoDict) {
				List<string> nodes;
				if(!ClassInfoNodeDict.TryGetValue(nodePair.Value, out nodes)) {
					nodes = new List<string>();
					ClassInfoNodeDict.Add(nodePair.Value, nodes);
				}
				nodes.Add(nodePair.Key);
			}
		}
		public void RaiseIfTopLevelAggregate() {
			if(TopLevelAggregateDetected) {
				throw new InvalidOperationException(Res.GetString(Res.CriteriaAnalyzer_TopLevelAggregateNotSupported));
			}
		}
		public AnalyzeNodePathItem[] GetNodePath(string startNode, string endNode) {
			Dictionary<string, bool> usedNodes = new Dictionary<string, bool>();
			usedNodes.Add(startNode, true);
			List<AnalyzeNodePathItem> result = FindNodePath(startNode, endNode, usedNodes);
			return result == null ? null : result.ToArray();
		}
		public static string NodePathToString(AnalyzeNodePathItem[] nodePath) {
			StringBuilder sb = new StringBuilder();
			for(int i = 0; i < nodePath.Length; i++) {
				sb.Append(nodePath[i].ToString());
			}
			return sb.ToString();
		}
		List<AnalyzeNodePathItem> FindNodePath(string node, string endNode, Dictionary<string, bool> usedNodes) {
			if(node == endNode) return new List<AnalyzeNodePathItem>();
			Dictionary<string, AnalyzeNodePathItem> nodeDict;
			if(!NodeToNodeMoveDict.TryGetValue(node, out nodeDict)) return null;
			foreach(KeyValuePair<string, AnalyzeNodePathItem> nodePair in nodeDict) {
				if(usedNodes.ContainsKey(nodePair.Key)) continue;
				usedNodes.Add(nodePair.Key, true);
				List<AnalyzeNodePathItem> result = FindNodePath(nodePair.Key, endNode, usedNodes);
				usedNodes.Remove(nodePair.Key);
				if(result == null) continue;
				result.Insert(0, nodePair.Value);
				return result;
			}
			return null;
		}
		public class ModifiedNodesInfo {
			public readonly string Node;
			public readonly object[] ModifiedObjects;
			public readonly object[] DeletedObjects;
			public ModifiedNodesInfo(string node, object[] modifiedObjects, object[] deletedObjects) {
				this.Node = node;
				this.ModifiedObjects = modifiedObjects;
				this.DeletedObjects = deletedObjects;
			}
		}
		public string[] GetModifiedNodes(Session session) {
			ICollection objectsToSave = session.GetObjectsToSave(true);
			ICollection objectsToDelete = session.GetObjectsToDelete(true);
			return GetModifiedNodes(session, objectsToSave, objectsToDelete);
		}
		public static HashSet<XPClassInfo> GetAllModifiedClassInfo(Session session, ICollection objectsToSave, ICollection objectsToDelete) {
			HashSet<XPClassInfo> modifiedSet = new HashSet<XPClassInfo>();
			foreach(object obj in objectsToSave) {
				XPClassInfo ci = session.GetClassInfo(obj);
				if(ci == null) continue;
				modifiedSet.Add(ci);
			}
			foreach(object obj in objectsToDelete) {
				XPClassInfo ci = session.GetClassInfo(obj);
				if(ci == null) continue;
				modifiedSet.Add(ci);
			}
			return modifiedSet;
		}
		public string[] GetModifiedNodes(Session session, ICollection objectsToSave, ICollection objectsToDelete) {
			var modifiedNodes = new HashSet<string>();
			foreach(object obj in objectsToSave) {
				XPClassInfo ci = session.GetClassInfo(obj);
				if(ci == null) continue;
				List<string> nodes;
				do {
					if(ClassInfoNodeDict.TryGetValue(ci, out nodes) || (AffectedNodeDict != null && AffectedNodeDict.TryGetValue(ci, out nodes))) {
						break;
					}
				} while((ci = ci.BaseClass) != null);
				if(nodes == null) continue;
				for(int i = 0; i < nodes.Count; i++) {
					modifiedNodes.Add(nodes[i]);
				}
			}
			foreach(object obj in objectsToDelete) {
				XPClassInfo ci = session.GetClassInfo(obj);
				if(ci == null) continue;
				List<string> nodes;
				do {
					if(ClassInfoNodeDict.TryGetValue(ci, out nodes) || (AffectedNodeDict != null && AffectedNodeDict.TryGetValue(ci, out nodes))) {
						break;
					}
				} while((ci = ci.BaseClass) != null);
				if(nodes == null) continue;
				for(int i = 0; i < nodes.Count; i++) {
					modifiedNodes.Add(nodes[i]);
				}
			}
			return modifiedNodes.ToArray();
		}
		public ModifiedNodesInfo[] GetModifiedNodesInfo(Session session) {
			return GetModifiedNodesInfo(session, session.GetObjectsToSave(), session.GetObjectsToDelete());
		}
		public ModifiedNodesInfo[] GetModifiedNodesInfo(Session session, ICollection objectsToSave, ICollection objectsToDelete) {
			Dictionary<string, List<object>> modifiedNodesDict = new Dictionary<string, List<object>>();
			Dictionary<string, List<object>> deletedNodesDict = new Dictionary<string, List<object>>();
			foreach(object obj in objectsToSave) {
				XPClassInfo ci = session.GetClassInfo(obj);
				if(ci == null) continue;
				List<string> nodes;
				if(!ClassInfoNodeDict.TryGetValue(ci, out nodes)) continue;
				for(int i = 0; i < nodes.Count; i++) {
					List<object> objectList;
					if(!modifiedNodesDict.TryGetValue(nodes[i], out objectList)) {
						objectList = new List<object>();
						modifiedNodesDict.Add(nodes[i], objectList);
					}
					objectList.Add(obj);
				}
			}
			foreach(object obj in objectsToDelete) {
				XPClassInfo ci = session.GetClassInfo(obj);
				if(ci == null) continue;
				List<string> nodes;
				if(!ClassInfoNodeDict.TryGetValue(ci, out nodes)) continue;
				for(int i = 0; i < nodes.Count; i++) {
					List<object> objectList;
					if(!deletedNodesDict.TryGetValue(nodes[i], out objectList)) {
						objectList = new List<object>();
						deletedNodesDict.Add(nodes[i], objectList);
					}
					objectList.Add(obj);
				}
			}
			List<ModifiedNodesInfo> result = new List<ModifiedNodesInfo>();
			foreach(KeyValuePair<string, List<object>> pair in modifiedNodesDict) {
				object[] modifiedObjects = pair.Value.Count > 0 ? pair.Value.ToArray() : null;
				List<object> deletedObjectList;
				if(deletedNodesDict.TryGetValue(pair.Key, out deletedObjectList)) {
					deletedNodesDict.Remove(pair.Key);
				}
				else {
					deletedObjectList = null;
				}
				result.Add(new ModifiedNodesInfo(pair.Key, modifiedObjects,
					(deletedObjectList == null) || (deletedObjectList.Count == 0) ? null : deletedObjectList.ToArray()));
			}
			foreach(KeyValuePair<string, List<object>> pair in deletedNodesDict) {
				object[] deletedObjects = pair.Value.Count > 0 ? pair.Value.ToArray() : null;
				result.Add(new ModifiedNodesInfo(pair.Key, null, deletedObjects));
			}
			return result.ToArray();
		}
		public override string ToString() {
			return ResultOperator.ToString();
		}
	}
	public class AnalyzeOperator : CriteriaOperator {
		CriteriaOperator original;
		Dictionary<AnalyzeInfoKey, object> analyzeInfo;
		public CriteriaOperator Original { get { return original; } }
		public Dictionary<AnalyzeInfoKey, object> AnalyzeInfo { get { return analyzeInfo; } }
		public AnalyzeOperator(CriteriaOperator original)
			: this(original, new Dictionary<AnalyzeInfoKey, object>()) {
		}
		public AnalyzeOperator(CriteriaOperator original, Dictionary<AnalyzeInfoKey, object> analyzeInfo)
			: base() {
			this.original = original;
			this.analyzeInfo = analyzeInfo;
		}
		public override void Accept(ICriteriaVisitor visitor) {
			original.Accept(visitor);
		}
		public override T Accept<T>(ICriteriaVisitor<T> visitor) {
			var analyzeVisitor = visitor as IAnalyzeCriteriaVisitor<T>;
			if(analyzeVisitor != null) {
				return analyzeVisitor.Visit(this);
			}
			return original.Accept(visitor);
		}
		public AnalyzeOperator CloneCriteria() {
			return new AnalyzeOperator((CriteriaOperator)(((ICloneable)original).Clone()), new Dictionary<AnalyzeInfoKey, object>(analyzeInfo));
		}
		protected override CriteriaOperator CloneCommon() {
			return CloneCriteria();
		}
		public override string ToString() {
			object nodeName;
			if(analyzeInfo.TryGetValue(AnalyzeCriteriaCreator.NodeNameKey, out nodeName)) {
				return original.ToString() + nodeName.ToString();
			}
			return original.ToString();
		}
		public override bool Equals(object obj) {
			AnalyzeOperator analyzeObj = obj as AnalyzeOperator;
			if(!ReferenceEquals(analyzeObj, null)) {
				return original.Equals(analyzeObj.Original);
			}
			return original.Equals(obj);
		}
		public override int GetHashCode() {
			return original.GetHashCode();
		}
	}
	public class AnalyzeOperator<T> : AnalyzeOperator where T : CriteriaOperator {
		public AnalyzeOperator(T original, Dictionary<AnalyzeInfoKey, object> analyzeInfo) : base(original, analyzeInfo) { }
		public new T Original { get { return (T)base.Original; } }
		public AnalyzeOperator<T> Clone() {
			return new AnalyzeOperator<T>((T)(((ICloneable)base.Original).Clone()), AnalyzeInfo);
		}
		protected override CriteriaOperator CloneCommon() {
			return Clone();
		}
	}
	public class AnalyzeInfoKey {
		string showInfo;
		public AnalyzeInfoKey(string showInfo) {
			this.showInfo = showInfo;
		}
		public override string ToString() {
			return showInfo;
		}
	}
	public class AnalyzeCriteriaToBasicStyleParameterlessProcessor : CriteriaToBasicStyleParameterlessProcessor, IAnalyzeCriteriaVisitor<CriteriaToStringVisitResult> {
		public CriteriaToStringVisitResult Visit(AnalyzeOperator criteria) {
			if(ReferenceEquals(criteria, null)) {
				return CriteriaToStringVisitResult.Null;
			}
			object nodeName;
			if(criteria.AnalyzeInfo.TryGetValue(AnalyzeCriteriaCreator.NodeNameKey, out nodeName)) {
				return new CriteriaToStringVisitResult("( " + nodeName.ToString() + ((CriteriaToStringVisitResult)criteria.Original.Accept(this)).Result + " )");
			}
			return criteria.Original.Accept(this);
		}
		public static string ToAnalyzeString(CriteriaOperator criteria) {
			return new AnalyzeCriteriaToBasicStyleParameterlessProcessor().Process(criteria).Result;
		}
	}
}
