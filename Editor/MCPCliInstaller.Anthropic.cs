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
        /// Links the current Unity project to Anthropic Claude Desktop by deploying the bridge script and rewriting the local Claude MCP config.
        /// </summary>
        /// <remarks>
        /// This editor action copies bridge files into the project root, resolves a Python executable, backs up any existing
        /// Claude Desktop config, writes the <c>nexus-unity</c> server entry, logs progress, and shows success/error dialogs.
        /// </remarks>
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
                    NexusEditorLog.Error(NexusLogCategory.Integrations, "[MCP] Unsupported platform for automatic Anthropic config linkage.");
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

                NexusEditorLog.Log(NexusLogCategory.Integrations, "[MCP] Successfully linked Unity project to Anthropic Claude Desktop.", true);
                EditorUtility.DisplayDialog("Success", "Successfully configured Claude Desktop to use the Unity MCP Server.\n\nPlease restart Claude Desktop for the changes to take effect.", "OK");
            }
            catch (Exception e)
            {
                NexusEditorLog.Error(NexusLogCategory.Integrations, $"[MCP] Failed to link Anthropic: {e.Message}");
                EditorUtility.DisplayDialog("Error", $"Failed to link to Anthropic Claude Desktop:\n{e.Message}", "OK");
            }
        }
    }
}
