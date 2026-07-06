using UnityEditor;
using UnityEngine;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System;

namespace UnityMCP.Editor
{
    public static partial class MCPCliInstaller
    {
        /// <summary>
        /// Links the current Unity project to Codex by deploying the bridge script, running the Codex MCP command, or editing Codex TOML fallback config.
        /// </summary>
        /// <remarks>
        /// This editor action copies bridge files into the project root, resolves <c>python3</c> and <c>codex</c>, may launch local
        /// CLI processes, and can create or update the user's <c>~/.codex/config.toml</c> when the CLI registration path is unavailable.
        /// </remarks>
        public static void LinkToCodex()
        {
            if (DeployBridgeScript(out string destinationPath))
            {
                string pythonPath = ResolvePythonPath();
                ExecuteCodexLinkSequence(destinationPath, pythonPath);
            }
        }

        private static void ExecuteCodexLinkSequence(string scriptPath, string pythonPath)
        {
            string codexPath = ResolveExecutablePath("codex");

            // If codex CLI is available, try it (cleanest/official way)
            if (!string.IsNullOrEmpty(codexPath) && codexPath != "codex")
            {
                // 1. Remove existing to ensure clean slate (silent)
                string removeCommand = "\"" + codexPath + "\" mcp remove nexus-unity";
                RunInstallerProcess(CreateProcessStartInfo(removeCommand), codexPath, false, "Codex");

                // 2. Add new with command/args (silent, because we have a fallback)
                string addCommand = "\"" + codexPath + "\" mcp add nexus-unity --env " + MCPServer.AuthTokenEnvironmentVariable + "=" + MCPServer.AuthToken + " -- \"" + pythonPath + "\" \"" + scriptPath + "\"";

                // If it succeeds, we are done
                if (RunInstallerProcess(CreateProcessStartInfo(addCommand), codexPath, false, "Codex"))
                {
                    NexusEditorLog.Log(NexusLogCategory.Integrations, "[MCP] Successfully linked Nexus Unity to Codex CLI via '" + codexPath + "'", true);
                    EditorUtility.DisplayDialog("MCP Success", "Successfully linked Nexus Unity to your system Codex CLI!", "OK");
                    return;
                }

                // If it failed (likely version issue), we proceed to fallback
                NexusEditorLog.Warning(NexusLogCategory.Integrations, "[MCP] Codex CLI command failed at '" + codexPath + "'. Falling back to manual TOML configuration.");
            }

            // Fallback: Manual TOML edit
            try
            {
                string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                var codex = NexusMcpConfigGenerator.BuildAll(scriptPath, pythonPath, projectRoot, homeDir)
                    .First(client => client.Kind == NexusMcpClientKind.Codex);
                var result = NexusMcpConfigGenerator.WriteConfig(codex);
                if (!result.Success) throw new Exception(result.Message);
                NexusEditorLog.Log(NexusLogCategory.Integrations, "[MCP] Successfully linked Nexus Unity to Codex CLI via manual TOML update: " + result.ConfigPath, true);
                EditorUtility.DisplayDialog("MCP Success", "Successfully linked Nexus Unity to your system Codex CLI!", "OK");
            }
            catch (Exception e)
            {
                NexusEditorLog.Error(NexusLogCategory.Integrations, "[MCP] Failed to link to Codex CLI via fallback: " + e.Message);
                EditorUtility.DisplayDialog("MCP Error", "Failed to update Codex configuration.\n\n" + e.Message, "OK");
            }
        }
    }
}
