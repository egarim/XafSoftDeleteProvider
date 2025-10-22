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
using DevExpress.Data.Filtering;
using System.Globalization;
namespace DevExpress.Xpo.Logger {
	class LogHelpers {
		public static string QueryToString(ObjectsQuery query) {
			StringBuilder sb = new StringBuilder();
			sb.AppendFormat(CultureInfo.InvariantCulture, "ClassInfoFullName:{0}; ", query.ClassInfo.FullName);
			sb.AppendFormat(CultureInfo.InvariantCulture, "Criteria:{0}; ", ReferenceEquals(query.Criteria, null) ? "null" : query.Criteria);
			sb.AppendFormat(CultureInfo.InvariantCulture, "Force:{0}; ", query.Force);
			sb.AppendFormat(CultureInfo.InvariantCulture, "SkipSelectedRecords:{0}; ", query.SkipSelectedRecords);
			sb.AppendFormat(CultureInfo.InvariantCulture, "TopSelectedRecords:{0}; ", query.TopSelectedRecords);
			sb.Append(SortingCollectionToString(query.Sorting));
			return sb.ToString();
		}
		public static string ObjectsByKeyQueryToString(ObjectsByKeyQuery query) {
			StringBuilder sb = new StringBuilder();
			sb.AppendFormat(CultureInfo.InvariantCulture, "ClassInfoFullName:{0}; ", query.ClassInfo.FullName);
			sb.AppendFormat(CultureInfo.InvariantCulture, "ObjectsCount:{0}", query.IdCollection.Count);
			return sb.ToString();
		}
		public static string SortingCollectionToString(SortingCollection collection) {
			if (collection == null) return string.Empty;
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < collection.Count; i++) {
				sb.AppendFormat(CultureInfo.InvariantCulture, "{0}:{1}; ", collection[i].PropertyName, collection[i].Direction);
			}
			return sb.ToString();
		}
	}
}
