using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

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
        /// Either path produces the same project-root <c>.mcp.json</c> that the Integrations tab tracks.
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
            if (!string.IsNullOrEmpty(claudePath) && claudePath != "claude")
            {
                if (TryLinkViaClaudeCli(claudePath, scriptPath, pythonPath))
                {
                    return;
                }
            }

            WriteClaudeCodeDirectJson(scriptPath, pythonPath);
        }

        private static bool TryLinkViaClaudeCli(string claudePath, string scriptPath, string pythonPath)
        {
            bool removedStale = RunInstallerProcess(CreateProcessStartInfo(claudePath, "mcp", "remove", "--scope", "project", "nexus-unity"), claudePath, false, "Claude Code", out string removeError, false);
            bool registrationAbsent = !removedStale && IsClaudeCodeRegistrationAbsent(removeError);

            if ((removedStale || registrationAbsent) && RunInstallerProcess(CreateProcessStartInfo(claudePath, "mcp", "add", "--transport", "stdio", "--scope", "project", "--env", MCPServer.AuthTokenEnvironmentVariable + "=" + MCPServer.AuthToken, "nexus-unity", "--", pythonPath, scriptPath), claudePath, false, "Claude Code"))
            {
                NexusEditorLog.Log(NexusLogCategory.Integrations, "[MCP] Successfully linked Nexus Unity to Claude Code via '" + claudePath + "'.", true);
                EditorUtility.DisplayDialog("MCP Success", "Successfully linked Nexus Unity to Claude Code.\n\nRun /mcp inside Claude Code (or restart it) to load the server.", "OK");
                return true;
            }

            NexusEditorLog.Warning(NexusLogCategory.Integrations, (removedStale || registrationAbsent)
                ? "[MCP] Claude Code CLI add command failed at '" + claudePath + "'. Falling back to direct .mcp.json edit."
                : "[MCP] Claude Code CLI could not remove the existing registration at '" + claudePath + "': " + removeError + ". Skipping CLI add and falling back to direct .mcp.json edit.");
            return false;
        }

        private static void WriteClaudeCodeDirectJson(string scriptPath, string pythonPath)
        {
            try
            {
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                string configPath = Path.Combine(projectRoot, ".mcp.json");

                JObject config = LoadOrCreateJsonObject(configPath);
                if (config["mcpServers"] == null) config["mcpServers"] = new JObject();
                JObject servers = (JObject)config["mcpServers"];

                servers["nexus-unity"] = new JObject
                {
                    ["command"] = pythonPath.Replace("\\", "/"),
                    ["args"] = new JArray { scriptPath.Replace("\\", "/") },
                    ["env"] = new JObject { [MCPServer.AuthTokenEnvironmentVariable] = MCPServer.AuthToken }
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

        private static JObject LoadOrCreateJsonObject(string path)
        {
            if (File.Exists(path))
            {
                try { return JObject.Parse(File.ReadAllText(path)); }
                catch { return new JObject(); }
            }
            return new JObject();
        }

        internal static bool IsClaudeCodeRegistrationAbsent(string error)
        {
            return !string.IsNullOrEmpty(error)
                && error.IndexOf("nexus-unity", StringComparison.OrdinalIgnoreCase) >= 0
                && error.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
