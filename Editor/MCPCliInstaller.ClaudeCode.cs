using UnityEditor;
using UnityEngine;
using System.IO;
using System;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    public static partial class MCPCliInstaller
    {
        /// <summary>
        /// Links the current Unity project to Claude Code by deploying the bridge script and registering the
        /// <c>nexus-unity</c> MCP server in the project-scoped <c>.mcp.json</c> file at the project root.
        /// </summary>
        /// <remarks>
        /// Prefers the official <c>claude</c> CLI (<c>claude mcp add --scope project</c>, which itself writes
        /// <c>.mcp.json</c>). When the CLI is unavailable or fails, it falls back to writing <c>.mcp.json</c> directly.
        /// Either path produces the same project-root <c>.mcp.json</c> that the Integrations tab tracks. This is distinct
        /// from the Claude Desktop app config, which is handled by the generic JSON-config codepath in
        /// <see cref="NexusMcpConfigGenerator"/> instead of a dedicated CLI installer.
        /// </remarks>
        public static void LinkToClaudeCode()
        {
            if (DeployBridgeScript(out string destinationPath))
            {
                string pythonPath = ResolvePythonPath();
                ExecuteClaudeCodeLinkSequence(destinationPath, pythonPath);
            }
        }

        private static void ExecuteClaudeCodeLinkSequence(string scriptPath, string pythonPath)
        {
            string claudePath = ResolveExecutablePath("claude");

            // Preferred path: the official claude CLI writes the project-scoped .mcp.json for us.
            if (!string.IsNullOrEmpty(claudePath) && claudePath != "claude")
            {
                // 1. Remove any stale registration so re-running is idempotent (silent).
                string removeCommand = "\"" + claudePath + "\" mcp remove nexus-unity -s project";
                RunInstallerProcess(CreateProcessStartInfo(removeCommand), claudePath, false, "Claude Code");

                // 2. Add the server at project scope (silent, because we have a file fallback).
                string addCommand = "\"" + claudePath + "\" mcp add nexus-unity -s project -- \"" + pythonPath + "\" \"" + scriptPath + "\"";
                if (RunInstallerProcess(CreateProcessStartInfo(addCommand), claudePath, false, "Claude Code"))
                {
                    NexusEditorLog.Log(NexusLogCategory.Integrations, "[MCP] Successfully linked Nexus Unity to Claude Code via '" + claudePath + "'.", true);
                    EditorUtility.DisplayDialog("MCP Success", "Successfully linked Nexus Unity to Claude Code.\n\nRun /mcp inside Claude Code (or restart it) to load the server.", "OK");
                    return;
                }

                NexusEditorLog.Warning(NexusLogCategory.Integrations, "[MCP] Claude Code CLI command failed at '" + claudePath + "'. Falling back to direct .mcp.json edit.");
            }

            // Fallback: write the project-root .mcp.json directly.
            try
            {
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                string configPath = Path.Combine(projectRoot, ".mcp.json");

                JObject config;
                if (File.Exists(configPath))
                {
                    try { config = JObject.Parse(File.ReadAllText(configPath)); }
                    catch { config = new JObject(); }
                }
                else
                {
                    config = new JObject();
                }

                if (config["mcpServers"] == null) config["mcpServers"] = new JObject();
                JObject servers = (JObject)config["mcpServers"];

                servers["nexus-unity"] = new JObject
                {
                    ["command"] = pythonPath.Replace("\\", "/"),
                    ["args"] = new JArray { scriptPath.Replace("\\", "/") }
                };

                NexusMcpConfigGenerator.BackupFileIfExists(configPath);
                File.WriteAllText(configPath, config.ToString(Newtonsoft.Json.Formatting.Indented));

                NexusEditorLog.Log(NexusLogCategory.Integrations, "[MCP] Successfully linked Nexus Unity to Claude Code via .mcp.json: " + configPath, true);
                EditorUtility.DisplayDialog("MCP Success", "Wrote .mcp.json in your project root for Claude Code.\n\nRun /mcp inside Claude Code (or restart it) to load the server.", "OK");
            }
            catch (Exception e)
            {
                NexusEditorLog.Error(NexusLogCategory.Integrations, "[MCP] Failed to link Claude Code: " + e.Message);
                EditorUtility.DisplayDialog("MCP Error", "Failed to write .mcp.json for Claude Code.\n\n" + e.Message, "OK");
            }
        }
    }
}
