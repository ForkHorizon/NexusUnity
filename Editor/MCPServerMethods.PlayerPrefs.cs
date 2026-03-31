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
            string key = p["key"]?.ToString();
            if (string.IsNullOrEmpty(key) || key == "all")
            {
                PlayerPrefs.DeleteAll();
            }
            else
            {
                PlayerPrefs.DeleteKey(key);
            }
            PlayerPrefs.Save();
            return "Success";
        }

        private static JToken ListPlayerPrefs(JToken p)
        {
            var result = new JObject();
            var keys = new List<string>();

            // Note: Unity doesn't provide a native way to list all PlayerPrefs keys.
            // We use platform-specific logic to find where they are stored.
            
            try
            {
#if UNITY_EDITOR_OSX
                string bundleId = PlayerSettings.applicationIdentifier;
                if (string.IsNullOrEmpty(bundleId)) bundleId = $"com.{PlayerSettings.companyName}.{PlayerSettings.productName}";
                
                // On macOS, Unity Editor stores PlayerPrefs in unity.<Company>.<Product>.plist
                string editorPlist = $"unity.{PlayerSettings.companyName}.{PlayerSettings.productName}";
                
                var domainsToCheck = new List<string> { editorPlist, bundleId };
                
                foreach (var domain in domainsToCheck)
                {
                    ProcessStartInfo psi = new ProcessStartInfo("defaults", $"read \"{domain}\"");
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
                    }
                }
#elif UNITY_EDITOR_WIN
                string registryPath = $@"Software\Unity\UnityEditor\{PlayerSettings.companyName}\{PlayerSettings.productName}";
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(registryPath))
                {
                    if (key != null)
                    {
                        foreach (var valueName in key.GetValueNames())
                        {
                            // Unity appends a hash to the end of the key name in the registry
                            // e.g. "MyKey_h123456789"
                            int lastUnderscore = valueName.LastIndexOf('_');
                            if (lastUnderscore > 0)
                            {
                                keys.Add(valueName.Substring(0, lastUnderscore));
                            }
                            else
                            {
                                keys.Add(valueName);
                            }
                        }
                    }
                }
#endif
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[MCP] Failed to list PlayerPrefs: {ex.Message}");
            }

            // Now populate the result with values using the standard Unity API
            foreach (var key in keys.Distinct())
            {
                if (PlayerPrefs.HasKey(key))
                {
                    // Best effort to get the value.
                    // If it's a string, GetString works.
                    // If it's an int, GetString might return empty, but GetInt will work.
                    // Since we can't be sure, we'll try to get it as string.
                    // If it's empty, we try int and float.
                    string sVal = PlayerPrefs.GetString(key, null);
                    if (sVal != null) 
                    {
                        result[key] = sVal;
                    }
                    else
                    {
                        // Try int
                        int iVal = PlayerPrefs.GetInt(key, int.MinValue);
                        if (iVal != int.MinValue) result[key] = iVal;
                        else
                        {
                            float fVal = PlayerPrefs.GetFloat(key, float.NaN);
                            if (!float.IsNaN(fVal)) result[key] = fVal;
                            else result[key] = "[Unknown Type]";
                        }
                    }
                }
            }

            return new JObject { 
                ["prefs"] = result,
                ["bundleIdentifier"] = PlayerSettings.applicationIdentifier
            };
        }
    }
}
