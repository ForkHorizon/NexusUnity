using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        private static void RegisterPlayerPrefsMethods()
        {
            _methods["get_player_pref"] = GetPlayerPref;
            _methods["set_player_pref"] = SetPlayerPref;
            _methods["delete_player_pref"] = DeletePlayerPref;
            _methods["list_player_prefs"] = ListPlayerPrefs;
        }

        private static JToken GetPlayerPref(JToken p)
        {
            string key = p["key"].ToString();
            string type = p["type"]?.ToString() ?? "string";

            if (type == "int") return PlayerPrefs.GetInt(key, p["default"]?.Value<int>() ?? 0);
            if (type == "float") return PlayerPrefs.GetFloat(key, p["default"]?.Value<float>() ?? 0f);
            return PlayerPrefs.GetString(key, p["default"]?.ToString() ?? "");
        }

        private static JToken SetPlayerPref(JToken p)
        {
            string key = p["key"].ToString();
            string type = p["type"]?.ToString() ?? "string";
            JToken value = p["value"];

            if (type == "int") PlayerPrefs.SetInt(key, value.Value<int>());
            else if (type == "float") PlayerPrefs.SetFloat(key, value.Value<float>());
            else PlayerPrefs.SetString(key, value.ToString());

            PlayerPrefs.Save();
            return "Success";
        }

        private static JToken DeletePlayerPref(JToken p)
        {
            string key = p?["key"]?.ToString();
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("key is required");
            }

            if (key == "all")
            {
                if (p?["confirm"]?.Value<bool>() != true)
                {
                    throw new ArgumentException("Deleting all PlayerPrefs requires key: \"all\" and confirm: true because it is not undoable.");
                }

                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
                return new JObject { ["status"] = "Success", ["message"] = "Deleted all PlayerPrefs. This operation is not undoable." };
            }

            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            return "Success";
        }

        private static List<string> CollectPlayerPrefKeys()
        {
            try
            {
#if UNITY_EDITOR_OSX
                return CollectMacOsPlayerPrefKeys();
#elif UNITY_EDITOR_WIN
                return CollectWindowsPlayerPrefKeys();
#elif UNITY_EDITOR_LINUX
                return CollectLinuxPlayerPrefKeys();
#else
                return new List<string>();
#endif
            }
            catch (Exception ex)
            {
                NexusEditorLog.Warning(NexusLogCategory.Api, $"[MCP] Failed to list PlayerPrefs: {ex.Message}");
                return new List<string>();
            }
        }

        private static string GetSafeCompanyName()
        {
            string company = PlayerSettings.companyName;
            return string.IsNullOrEmpty(company) ? "DefaultCompany" : company;
        }

        private static string GetSafeProductName()
        {
            string product = PlayerSettings.productName;
            return string.IsNullOrEmpty(product) ? "DefaultProduct" : product;
        }

#if UNITY_EDITOR_OSX
        private static List<string> CollectMacOsPlayerPrefKeys()
        {
            var keys = new List<string>();
            string company = GetSafeCompanyName();
            string product = GetSafeProductName();
            string bundleId = PlayerSettings.applicationIdentifier;
            if (string.IsNullOrEmpty(bundleId)) bundleId = $"com.{company}.{product}";
            string editorPlist = $"unity.{company}.{product}";

            var domainsToCheck = new List<string> { editorPlist, bundleId };
            foreach (var domain in domainsToCheck)
            {
                keys.AddRange(ReadMacOsPlistKeys(domain));
            }
            return keys;
        }

        private static List<string> ReadMacOsPlistKeys(string domain)
        {
            var keys = new List<string>();
            ProcessStartInfo psi = new ProcessStartInfo("defaults");
            psi.ArgumentList.Add("read");
            psi.ArgumentList.Add(domain);
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = false;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;

            try
            {
                using (Process process = Process.Start(psi))
                {
                    if (process == null) return keys;
                    string output = process.StandardOutput.ReadToEnd();
                    if (!process.WaitForExit(5000))
                    {
                        try { process.Kill(); } catch { }
                        NexusEditorLog.Warning(NexusLogCategory.Api, $"[MCP] 'defaults read {domain}' timed out after 5000ms.");
                        return keys;
                    }

                    if (process.ExitCode == 0)
                    {
                        keys.AddRange(ParseMacOsDefaultsOutput(output));
                    }
                }
            }
            catch (Exception ex)
            {
                NexusEditorLog.Warning(NexusLogCategory.Api, $"[MCP] Failed reading macOS plist for domain '{domain}': {ex.Message}");
            }
            return keys;
        }
#elif UNITY_EDITOR_WIN
        private static List<string> CollectWindowsPlayerPrefKeys()
        {
            var keys = new List<string>();
            string company = GetSafeCompanyName();
            string product = GetSafeProductName();
            string registryPath = $@"Software\Unity\UnityEditor\{company}\{product}";
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(registryPath))
            {
                if (key != null)
                {
                    foreach (var valueName in key.GetValueNames())
                    {
                        keys.Add(UnescapeWindowsRegistryKeyName(valueName));
                    }
                }
            }
            return keys;
        }
#elif UNITY_EDITOR_LINUX
        private static List<string> CollectLinuxPlayerPrefKeys()
        {
            var keys = new List<string>();
            string company = GetSafeCompanyName();
            string product = GetSafeProductName();
            string configDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string prefsPath = Path.Combine(configDir, ".config", "unity3d", company, product, "prefs");

            if (File.Exists(prefsPath))
            {
                try
                {
                    var doc = System.Xml.Linq.XDocument.Load(prefsPath);
                    foreach (var elem in doc.Descendants("pref"))
                    {
                        var nameAttr = elem.Attribute("name");
                        if (nameAttr != null && !string.IsNullOrEmpty(nameAttr.Value))
                        {
                            keys.Add(nameAttr.Value);
                        }
                    }
                }
                catch (Exception ex)
                {
                    NexusEditorLog.Warning(NexusLogCategory.Api, $"[MCP] Failed to parse Linux PlayerPrefs XML: {ex.Message}");
                }
            }
            return keys;
        }
#endif

        internal static List<string> ParseMacOsDefaultsOutput(string output)
        {
            var keys = new List<string>();
            if (string.IsNullOrEmpty(output)) return keys;

            var lineRegex = new Regex(@"^\s*(?:""((?:[^""\\]|\\.)+)""|([^\s=]+))\s*=", RegexOptions.Multiline);
            var matches = lineRegex.Matches(output);
            foreach (Match match in matches)
            {
                string rawKey = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                if (!string.IsNullOrEmpty(rawKey))
                {
                    string unescaped = rawKey.Replace("\\\"", "\"").Replace("\\\\", "\\");
                    keys.Add(unescaped);
                }
            }
            return keys;
        }

        internal static string UnescapeWindowsRegistryKeyName(string valueName)
        {
            if (string.IsNullOrEmpty(valueName)) return valueName;
            int lastUnderscore = valueName.LastIndexOf('_');
            if (lastUnderscore > 0 && lastUnderscore < valueName.Length - 1 && valueName[lastUnderscore + 1] == 'h')
            {
                string hashPart = valueName.Substring(lastUnderscore + 2);
                if (hashPart.Length > 0 && hashPart.All(char.IsDigit))
                {
                    return valueName.Substring(0, lastUnderscore);
                }
            }
            return valueName;
        }

        internal static JToken ReadPlayerPrefValue(string key)
        {
            const string def1 = "__NEXUS_PLPREF_DEF_1__";
            const string def2 = "__NEXUS_PLPREF_DEF_2__";

            string sVal1 = PlayerPrefs.GetString(key, def1);
            string sVal2 = PlayerPrefs.GetString(key, def2);
            if (sVal1 == sVal2 && sVal1 != def1)
            {
                return sVal1;
            }

            int iVal1 = PlayerPrefs.GetInt(key, 0);
            int iVal2 = PlayerPrefs.GetInt(key, 1);
            if (iVal1 == iVal2) return iVal1;

            float fVal1 = PlayerPrefs.GetFloat(key, 0f);
            float fVal2 = PlayerPrefs.GetFloat(key, 1f);
            if (Mathf.Approximately(fVal1, fVal2)) return fVal1;

            return "[Unknown Type]";
        }

        private static JToken ListPlayerPrefs(JToken p)
        {
            var result = new JObject();
            var keys = CollectPlayerPrefKeys();

            foreach (var key in keys.Distinct())
            {
                if (PlayerPrefs.HasKey(key))
                {
                    result[key] = ReadPlayerPrefValue(key);
                }
            }

            return new JObject {
                ["prefs"] = result,
                ["bundleIdentifier"] = PlayerSettings.applicationIdentifier
            };
        }
    }
}
