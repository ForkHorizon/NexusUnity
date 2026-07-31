using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
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
            var keys = new List<string>();
            try
            {
#if UNITY_EDITOR_OSX
                CollectMacOsPlayerPrefKeys(keys);
#elif UNITY_EDITOR_WIN
                CollectWindowsPlayerPrefKeys(keys);
#endif
            }
            catch (Exception ex)
            {
                NexusEditorLog.Warning(NexusLogCategory.Api, $"[MCP] Failed to list PlayerPrefs: {ex.Message}");
            }
            return keys;
        }

#if UNITY_EDITOR_OSX
        private static void CollectMacOsPlayerPrefKeys(List<string> keys)
        {
            string bundleId = PlayerSettings.applicationIdentifier;
            if (string.IsNullOrEmpty(bundleId)) bundleId = $"com.{PlayerSettings.companyName}.{PlayerSettings.productName}";
            string editorPlist = $"unity.{PlayerSettings.companyName}.{PlayerSettings.productName}";

            var domainsToCheck = new List<string> { editorPlist, bundleId };
            foreach (var domain in domainsToCheck)
            {
                ReadMacOsPlistKeys(domain, keys);
            }
        }

        private static void ReadMacOsPlistKeys(string domain, List<string> keys)
        {
            ProcessStartInfo psi = new ProcessStartInfo("defaults");
            psi.ArgumentList.Add("read");
            psi.ArgumentList.Add(domain);
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;

            using (Process process = Process.Start(psi))
            {
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    ParseMacOsDefaultsOutput(output, keys);
                }
            }
        }

        private static void ParseMacOsDefaultsOutput(string output, List<string> keys)
        {
            var lines = output.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("{") || trimmed.EndsWith("}") || string.IsNullOrEmpty(trimmed) || trimmed.EndsWith("(")) continue;

                int equalIndex = trimmed.IndexOf('=');
                if (equalIndex > 0)
                {
                    string key = trimmed.Substring(0, equalIndex).Trim().Trim('"');
                    keys.Add(key);
                }
            }
        }
#elif UNITY_EDITOR_WIN
        private static void CollectWindowsPlayerPrefKeys(List<string> keys)
        {
            string registryPath = $@"Software\Unity\UnityEditor\{PlayerSettings.companyName}\{PlayerSettings.productName}";
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(registryPath))
            {
                if (key != null)
                {
                    foreach (var valueName in key.GetValueNames())
                    {
                        int lastUnderscore = valueName.LastIndexOf('_');
                        if (lastUnderscore > 0)
                            keys.Add(valueName.Substring(0, lastUnderscore));
                        else
                            keys.Add(valueName);
                    }
                }
            }
        }
#endif

        private static JToken ReadPlayerPrefValue(string key)
        {
            string sVal = PlayerPrefs.GetString(key, null);
            if (sVal != null) return sVal;

            int iVal = PlayerPrefs.GetInt(key, int.MinValue);
            if (iVal != int.MinValue) return iVal;

            float fVal = PlayerPrefs.GetFloat(key, float.NaN);
            if (!float.IsNaN(fVal)) return fVal;

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
