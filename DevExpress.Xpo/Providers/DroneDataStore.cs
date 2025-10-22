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
using DevExpress.Xpo.DB.Helpers;
namespace DevExpress.Xpo.DB {
	public class DroneDataStore : IDataStore {
		public const string XpoProviderTypeString = "DroneDataStore";
		public static string GetConnectionString() {
			return String.Format("{0}={1}", DataStoreBase.XpoProviderTypeParameterName, XpoProviderTypeString);
		}
		public static IDataStore CreateProviderFromString(string connectionString, AutoCreateOption autoCreateOption, out IDisposable[] objectsToDisposeOnDisconnect) {
			objectsToDisposeOnDisconnect = Array.Empty<IDisposable>();
			return new DroneDataStore();
		}
		static DroneDataStore() {
			DataStoreBase.RegisterDataStoreProvider(XpoProviderTypeString, CreateProviderFromString);
		}
		public static void Register() { }
		public AutoCreateOption AutoCreateOption {
			get { return AutoCreateOption.SchemaAlreadyExists; }
		}
		public ModificationResult ModifyData(params ModificationStatement[] dmlStatements) {
			throw new NotSupportedException(Res.GetString(Res.DroneDataStore_CommitNotSupported));
		}
		public SelectedData SelectData(params SelectStatement[] selects) {
			if(selects == null)
				return null;
			return new SelectedData(selects.Select(s => new SelectStatementResult()).ToArray());
		}
		public UpdateSchemaResult UpdateSchema(bool doNotCreateIfFirstTableNotExist, params DBTable[] tables) {
			return UpdateSchemaResult.SchemaExists;
		}
	}
}
