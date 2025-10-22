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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using DevExpress.Utils;
using DevExpress.Xpo.DB;
namespace DevExpress.Xpo.Helpers {
	public static class ConnectionParameter {
		const string GenerateConnectionHelperParameterName = "generateConnectionHelper";
#if !NET
		const string SettingsLocation = "DevExpress\\XpoWizard\\";
		static readonly string SettingsFilename = string.Concat("settings", AssemblyInfo.VirtDirSuffix, ".xml");
#endif
		public static Dictionary<string, string> GetParamsDict(IConnectionPage connPage) {
			Dictionary<string, string> parameters = new Dictionary<string, string>();
			if(connPage.IsServerbased) {
				parameters.Add(ProviderFactory.ServerParamID, connPage.ServerName);
			}
			IConnectionPageEx connPageEx = connPage as IConnectionPageEx;
			if(connPageEx != null && connPage.IsServerbased && connPage.Factory.HasPort) {
				object port = connPageEx.GetParameterValue(ProviderFactory.PortParamID);
				if(port != null) {
					parameters.Add(ProviderFactory.PortParamID, ((int)port).ToString(CultureInfo.InvariantCulture));
				}
			}
			if(connPageEx != null && connPage.Factory.ProviderKey == MSSqlConnectionProvider.XpoProviderTypeString) {
				object encrypt = connPageEx.GetParameterValue(ProviderFactory.EncryptParamID);
				if(encrypt != null) {
					bool isEncrypt = (bool)encrypt;
					parameters.Add(ProviderFactory.EncryptParamID, isEncrypt ? "true" : "false");
				}
				object trustServerCertificate = connPageEx.GetParameterValue("IsTrustServerCertificateChecked");
				if(trustServerCertificate != null) {
					bool isTrustServerCertificate = (bool)trustServerCertificate;
					parameters.Add("IsTrustServerCertificateChecked", isTrustServerCertificate ? "true" : "false");
				}
			}
			if(connPage.IsServerbased && connPage.Factory.HasMultipleDatabases) {
				parameters.Add(ProviderFactory.DatabaseParamID, connPage.DatabaseName);
			}
			else {
				parameters.Add(ProviderFactory.DatabaseParamID, connPage.FileName);
			}
			if(connPage.Factory.HasIntegratedSecurity) {
				parameters.Add(ProviderFactory.UseIntegratedSecurityParamID, connPage.AuthType.ToString());
			}
			if(connPage.Factory.HasUserName) {
				parameters.Add(ProviderFactory.UserIDParamID, connPage.UserName);
			}
			if(connPage.Factory.HasPassword) {
				parameters.Add(ProviderFactory.PasswordParamID, connPage.Password);
			}
			parameters.Add(ProviderFactory.ReadOnlyParamID, "1");
			parameters.Add(GenerateConnectionHelperParameterName, connPage.GenerateConnectionHelper ? "true" : "false");
			return parameters;
		}
		public static void SetParamsDict(string providerKey, Dictionary<string, string> paramDict, IConnectionPage connPage) {
			if(providerKey == connPage.CustomConStrTag) {
				connPage.LastConStr = paramDict[connPage.CustomConStrTag];
				connPage.SetProvider(providerKey);
				return;
			}
			connPage.SetProvider(providerKey);
			if(connPage.Factory == null)
				return;
			if(connPage.IsServerbased) {
				connPage.ServerName = paramDict[ProviderFactory.ServerParamID];
			}
			IConnectionPageEx connPageEx = connPage as IConnectionPageEx;
			if(connPage.IsServerbased && connPage.Factory.HasPort && connPageEx != null) {
				string portString;
				if(paramDict.TryGetValue(ProviderFactory.PortParamID, out portString)) {
					connPageEx.SetParameterValue(ProviderFactory.PortParamID, Convert.ToInt32(portString, CultureInfo.InvariantCulture));
				}
				else {
					connPageEx.SetParameterValue(ProviderFactory.PortParamID, null);
				}
			}
			if(connPageEx != null && providerKey == MSSqlConnectionProvider.XpoProviderTypeString) {
				string encrypt;
				if(paramDict.TryGetValue(ProviderFactory.EncryptParamID, out encrypt)) {
					connPageEx.SetParameterValue(ProviderFactory.EncryptParamID, encrypt == "true");
				}
				string trustServerCertificate;
				if(paramDict.TryGetValue("IsTrustServerCertificateChecked", out trustServerCertificate)) {
					connPageEx.SetParameterValue("IsTrustServerCertificateChecked", trustServerCertificate == "true");
				}
			}
			if(connPage.Factory.HasUserName) {
				connPage.UserName = paramDict[ProviderFactory.UserIDParamID];
			}
			if(connPage.Factory.HasPassword) {
				connPage.Password = paramDict[ProviderFactory.PasswordParamID];
			}
			if(connPage.Factory.HasIntegratedSecurity) {
				connPage.AuthType = bool.Parse(paramDict[ProviderFactory.UseIntegratedSecurityParamID]);
			}
			if(connPage.IsServerbased && connPage.Factory.HasMultipleDatabases) {
				connPage.DatabaseName = paramDict[ProviderFactory.DatabaseParamID];
			}
			else {
				connPage.FileName = paramDict[ProviderFactory.DatabaseParamID];
			}
			if(paramDict.ContainsKey(GenerateConnectionHelperParameterName)) {
				connPage.CeConnectionHelper = paramDict[GenerateConnectionHelperParameterName] == "true";
			}
		}
		static ConnectionPageSettings GetSettings(IConnectionPage connPage) {
			Dictionary<string, string> paramDict;
			if(connPage.Factory == null) {
				paramDict = new Dictionary<string, string>();
				paramDict.Add(connPage.CustomConStrTag, connPage.CustomConStr);
				return new ConnectionPageSettings(connPage.CustomConStrTag, paramDict);
			}
			return new ConnectionPageSettings(connPage.Factory.ProviderKey, GetParamsDict(connPage));
		}
		public static void SetSettings(ConnectionPageSettings settings, IConnectionPage connPage) {
			SetParamsDict(settings.ProviderKey, settings.ParamDict, connPage);
		}
		public static void TryToLoadSettingsFromBase64(IConnectionPage connPage, string base64String) {
			byte[] data = Convert.FromBase64String(base64String);
			using(MemoryStream ms = new MemoryStream(data))
				TryToLoadSettings(connPage, ms);
		}
		public static void TryToLoadSettings(IConnectionPage connPage, Stream stream) {
			try {
				var settings = SafeXml.Deserialize<ConnectionPageSettings>(stream);
				if(settings != null)
					SetSettings(settings, connPage);
			}
			catch(Exception) { }
		}
#if !NET
		public static void TryToLoadSettings(IConnectionPage connPage) {
			string settingsFolderPath = Path.Combine(DevExpress.Data.Utils.SafeEnvironment.LocalApplicationData, SettingsLocation);
			string settingPath = Path.Combine(settingsFolderPath, SettingsFilename);
			if(!File.Exists(settingPath))
				return;
			try {
				using(FileStream file = new FileStream(settingPath, FileMode.Open, FileAccess.Read)) 
					TryToLoadSettings(connPage, file);
			}
			catch(Exception) { }
		}
#endif
		public static string SaveSettingsToBase64(IConnectionPage connPage) {
			using(MemoryStream ms = new MemoryStream()) {
				SaveSettings(connPage, ms);
				ms.Flush();
				return Convert.ToBase64String(ms.ToArray());
			}
		}
		public static void SaveSettings(IConnectionPage connPage, Stream stream) {
			try {
				ConnectionPageSettings settings = GetSettings(connPage);
				XmlWriterSettings xmlSettings = new XmlWriterSettings();
				xmlSettings.Indent = true;
				using(XmlWriter xmlWriter = XmlWriter.Create(stream, xmlSettings))
					SafeXml.Serialize<ConnectionPageSettings>(xmlWriter, settings);
			}
			catch(Exception) { }
		}
#if !NET
		public static void SaveSettings(IConnectionPage connPage) {
			try {
				string settingsFolderPath = Path.Combine(DevExpress.Data.Utils.SafeEnvironment.LocalApplicationData, SettingsLocation);
				string settingPath = Path.Combine(settingsFolderPath, SettingsFilename);
				if(!Directory.Exists(settingsFolderPath))
					Directory.CreateDirectory(settingsFolderPath);
				using(FileStream file = new FileStream(settingPath, FileMode.Create, FileAccess.Write))
					SaveSettings(connPage, file);
			}
			catch(Exception) { }
		}
#endif
	}
	[XmlRoot("DevExpressXpoGeneratorSettings")]
	public class ConnectionPageSettings {
		string providerKey;
		[XmlAttribute("providerKey")]
		public string ProviderKey {
			get { return providerKey; }
			set { providerKey = value; }
		}
		Dictionary<string, string> paramDict;
		[XmlIgnore]
		public Dictionary<string, string> ParamDict {
			get { return paramDict; }
		}
		[XmlArray("parameter")]
		public ConnectionPageSettingsItem[] Parameters {
			get {
				ConnectionPageSettingsItem[] settings = new ConnectionPageSettingsItem[paramDict.Count];
				int parameterIndex = 0;
				foreach(KeyValuePair<string, string> parameter in paramDict) {
					settings[parameterIndex] = new ConnectionPageSettingsItem(parameter.Key, parameter.Value);
					parameterIndex++;
				}
				return settings;
			}
			set {
				if(paramDict == null)
					paramDict = new Dictionary<string, string>();
				else
					paramDict.Clear();
				if(value == null || value.Length == 0)
					return;
				foreach(ConnectionPageSettingsItem item in value) {
					paramDict[item.Name] = item.Value;
				}
			}
		}
		public ConnectionPageSettings() { }
		public ConnectionPageSettings(string providerKey, Dictionary<string, string> paramDict) {
			this.providerKey = providerKey;
			this.paramDict = paramDict;
		}
		public class ConnectionPageSettingsItem {
			[XmlAttribute("name")]
			public string Name;
			[XmlAttribute("value")]
			public string Value;
			public ConnectionPageSettingsItem() { }
			public ConnectionPageSettingsItem(string name, string value) {
				Name = name;
				Value = value;
			}
		}
	}
}
