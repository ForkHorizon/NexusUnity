using UnityEditor;
using UnityEngine;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    public static partial class MCPCliInstaller
    {
        /// <summary>
        /// Attempts to link the current Unity project to the Anthropic Claude Desktop app.
        /// </summary>
        public static void LinkToAnthropic()
        {
            if (DeployBridgeScript(out string destinationPath))
            {
                string pythonPath = ResolveExecutablePath("python3");
                ExecuteAnthropicLinkSequence(destinationPath, pythonPath);
            }
        }

        private static void ExecuteAnthropicLinkSequence(string scriptPath, string pythonPath)
        {
            try
            {
                string configPath = "";
#if UNITY_EDITOR_OSX
                configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude", "claude_desktop_config.json");
#elif UNITY_EDITOR_WIN
                configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude", "claude_desktop_config.json");
#endif

                if (string.IsNullOrEmpty(configPath)) {
                    UnityEngine.Debug.LogError("[MCP] Unsupported platform for automatic Anthropic config linkage.");
                    return;
                }

                JObject config;
                if (File.Exists(configPath)) {
                    string content = File.ReadAllText(configPath);
                    try { config = JObject.Parse(content); }
                    catch { config = new JObject(); }
                } else {
                    config = new JObject();
                    string dir = Path.GetDirectoryName(configPath);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                }

                if (config["mcpServers"] == null) config["mcpServers"] = new JObject();
                JObject servers = (JObject)config["mcpServers"];

                servers["nexus-unity"] = new JObject {
                    ["command"] = pythonPath,
                    ["args"] = new JArray { scriptPath }
                };

                NexusMcpConfigGenerator.BackupFileIfExists(configPath);
                File.WriteAllText(configPath, config.ToString(Newtonsoft.Json.Formatting.Indented));

                UnityEngine.Debug.Log("[MCP] Successfully linked Unity project to Anthropic Claude Desktop.");
                EditorUtility.DisplayDialog("Success", "Successfully configured Claude Desktop to use the Unity MCP Server.\n\nPlease restart Claude Desktop for the changes to take effect.", "OK");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[MCP] Failed to link Anthropic: {e.Message}");
                EditorUtility.DisplayDialog("Error", $"Failed to link to Anthropic Claude Desktop:\n{e.Message}", "OK");
            }
        }
    }
}
