using UnityEditor;
using UnityEngine;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System;

namespace UnityMCP.Editor
{
    public static partial class MCPCliInstaller
    {
        /// <summary>
        /// Attempts to link the current Unity project to the local Codex CLI instance.
        /// </summary>
        public static void LinkToCodex()
        {
            if (DeployBridgeScript(out string destinationPath))
            {
                string pythonPath = ResolveExecutablePath("python3");
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
                string addCommand = "\"" + codexPath + "\" mcp add nexus-unity -- \"" + pythonPath + "\" \"" + scriptPath + "\"";

                // If it succeeds, we are done
                if (RunInstallerProcess(CreateProcessStartInfo(addCommand), codexPath, false, "Codex"))
                {
                    UnityEngine.Debug.Log("[MCP] Successfully linked Nexus Unity to Codex CLI via '" + codexPath + "'");
                    EditorUtility.DisplayDialog("MCP Success", "Successfully linked Nexus Unity to your system Codex CLI!", "OK");
                    return;
                }

                // If it failed (likely version issue), we proceed to fallback
                UnityEngine.Debug.LogWarning("[MCP] Codex CLI command failed at '" + codexPath + "'. Falling back to manual TOML configuration.");
            }

            // Fallback: Manual TOML edit
            try
            {
                string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string codexDir = Path.Combine(homeDir, ".codex");
                string configPath = Path.Combine(codexDir, "config.toml");

                if (!Directory.Exists(codexDir))
                {
                    Directory.CreateDirectory(codexDir);
                }

                List<string> lines = new List<string>();
                if (File.Exists(configPath))
                {
                    lines.AddRange(File.ReadAllLines(configPath));
                }

                string safeScriptPath = scriptPath.Replace("\\", "/");
                string safePythonPath = pythonPath.Replace("\\", "/");

                // Check if [mcp_servers.nexus-unity] already exists
                int existingIndex = -1;
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].Trim() == "[mcp_servers.nexus-unity]")
                    {
                        existingIndex = i;
                        break;
                    }
                }

                if (existingIndex != -1)
                {
                    // Update existing
                    bool foundCommand = false;
                    bool foundArgs = false;
                    for (int i = existingIndex + 1; i < lines.Count; i++)
                    {
                        string trimmed = lines[i].Trim();
                        if (trimmed.StartsWith("[")) break; // Next section

                        if (trimmed.StartsWith("command"))
                        {
                            lines[i] = "command = \"" + safePythonPath + "\"";
                            foundCommand = true;
                        }
                        else if (trimmed.StartsWith("args"))
                        {
                            lines[i] = "args = [ \"" + safeScriptPath + "\" ]";
                            foundArgs = true;
                        }
                    }

                    if (!foundCommand) lines.Insert(existingIndex + 1, "command = \"" + safePythonPath + "\"");
                    if (!foundArgs) lines.Insert(existingIndex + (foundCommand ? 2 : 1), "args = [ \"" + safeScriptPath + "\" ]");
                }
                else
                {
                    // Append new
                    if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
                    {
                        lines.Add(""); // Add a blank line for spacing
                    }
                    lines.Add("[mcp_servers.nexus-unity]");
                    lines.Add("command = \"" + safePythonPath + "\"");
                    lines.Add("args = [ \"" + safeScriptPath + "\" ]");
                }

                File.WriteAllLines(configPath, lines);
                UnityEngine.Debug.Log("[MCP] Successfully linked Nexus Unity to Codex CLI via manual TOML update: " + configPath);
                EditorUtility.DisplayDialog("MCP Success", "Successfully linked Nexus Unity to your system Codex CLI!", "OK");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[MCP] Failed to link to Codex CLI via fallback: " + e.Message);
                EditorUtility.DisplayDialog("MCP Error", "Failed to update Codex configuration.\n\n" + e.Message, "OK");
            }
        }
    }
}
