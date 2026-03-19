using UnityEditor;
using UnityEngine;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Handles the integration of the Unity MCP server with external CLIs like Gemini and Codex.
    /// </summary>
    public static class MCPCliInstaller
    {
        /// <summary>
        /// Attempts to link the current Unity project to the local Gemini CLI instance.
        /// </summary>
        public static void LinkToGemini()
        {
            if (DeployBridgeScript(out string destinationPath))
            {
                ExecuteGeminiLinkSequence(destinationPath);
            }
        }

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

        private static bool DeployBridgeScript(out string destinationPath)
        {
            destinationPath = null;
            string sourcePath = FindBridgeScript();

            if (string.IsNullOrEmpty(sourcePath))
            {
                UnityEngine.Debug.LogError("[MCP] Could not find 'nexus_unity_bridge.py' in the project.");
                EditorUtility.DisplayDialog("MCP Error", "Could not find 'nexus_unity_bridge.py'.\n\nEnsure the library is correctly imported.", "OK");
                return false;
            }

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            destinationPath = Path.Combine(projectRoot, "nexus_unity_bridge.py");

            try
            {
                File.Copy(sourcePath, destinationPath, true);
                UnityEngine.Debug.Log("[MCP] Bridge script deployed to stable location: " + destinationPath);
                DeployDocumentationPointer(projectRoot, sourcePath);
                return true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[MCP] Failed to deploy bridge or docs: " + e.Message);
                EditorUtility.DisplayDialog("MCP Error", "Failed to deploy integration files to project root.\n\n" + e.Message, "OK");
                return false;
            }
        }

        private static string FindLibraryRoot(string sourcePath)
        {
            string dir = Path.GetDirectoryName(sourcePath);
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, "package.json")))
                {
                    return dir;
                }
                dir = Path.GetDirectoryName(dir);
            }
            return Path.GetDirectoryName(sourcePath);
        }

        private static void DeployDocumentationPointer(string projectRoot, string sourcePath)
        {
            string libraryRoot = FindLibraryRoot(sourcePath);
            string[] docFiles = { "API_REFERENCE.MD", "DOCUMENTATION.MD" };

            foreach (string file in docFiles)
            {
                string src = Path.Combine(libraryRoot, file);
                if (File.Exists(src))
                {
                    try
                    {
                        string dst = Path.Combine(projectRoot, file);
                        File.Copy(src, dst, true);
                        UnityEngine.Debug.Log("[MCP] Copied documentation to root: " + dst);
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogWarning("[MCP] Failed to copy " + file + " to root: " + e.Message);
                    }
                }
            }

            string docPointerPath = Path.Combine(projectRoot, "NEXUS_UNITY_DOCS.md");
            string docContent = "# Nexus Unity - AI Context\n\n" +
                               "This project uses **Nexus Unity** for AI Editor automation.\n\n" +
                               "## 📚 Documentation Access\n" +
                               "- **Full Tool Reference**: [API_REFERENCE.MD](API_REFERENCE.MD)\n" +
                               "- **Technical Guide**: [DOCUMENTATION.MD](DOCUMENTATION.MD)\n\n" +
                               "## 🤖 AI Instructions\n" +
                               "Before performing any Unity tasks, ALWAYS read `API_REFERENCE.MD` to understand the available tools, their parameters, and the surgical editing patterns required for this project.";

            File.WriteAllText(docPointerPath, docContent);
            UnityEngine.Debug.Log("[MCP] Documentation pointer deployed: " + docPointerPath);
        }

        private static void ExecuteGeminiLinkSequence(string scriptPath)
        {
            string geminiPath = ResolveExecutablePath("gemini");
            string pythonPath = ResolveExecutablePath("python3");

            // 1. Ensure clean slate by removing existing registration
            string removeCommand = "\"" + geminiPath + "\" mcp remove nexus-unity";
            RunInstallerProcess(CreateProcessStartInfo(removeCommand), geminiPath, false, "Gemini");

            // 2. Add new registration with stable path
            string addCommand = "\"" + geminiPath + "\" mcp add nexus-unity --trust \"" + pythonPath + "\" \"" + scriptPath + "\"";
            RunInstallerProcess(CreateProcessStartInfo(addCommand), geminiPath, true, "Gemini");
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

        private static string FindBridgeScript()
        {
            string[] guids = AssetDatabase.FindAssets("MCPCliInstaller t:Script");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("NexusUnity")) continue;
                
                string dir = Path.GetDirectoryName(path);
                string potentialBridge = Path.Combine(dir, "nexus_unity_bridge.py");
                if (File.Exists(Path.GetFullPath(potentialBridge))) return Path.GetFullPath(potentialBridge);
            }

            foreach (var path in AssetDatabase.GetAllAssetPaths())
            {
                if (path.EndsWith("nexus_unity_bridge.py")) return Path.GetFullPath(path);
            }
            return null;
        }

        private static string ResolveExecutablePath(string name)
        {
            if (Application.platform == RuntimePlatform.WindowsEditor) return name;

            // 1. Explicit check for NVM/Node paths (High priority for Codex)
            if (name == "codex")
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string nvmBase = Path.Combine(home, ".nvm/versions/node");
                if (Directory.Exists(nvmBase))
                {
                    foreach (var versionDir in Directory.GetDirectories(nvmBase))
                    {
                        string potential = Path.Combine(versionDir, "bin/codex");
                        if (File.Exists(potential)) return potential;
                    }
                }
            }

            // 2. Try 'which' (respects shell PATH)
            string pathFromWhich = GetPathFromWhich(name);
            if (!string.IsNullOrEmpty(pathFromWhich) && pathFromWhich != name && File.Exists(pathFromWhich))
            {
                // Only return if it's not the older homebrew version of codex (if we can tell)
                if (name == "codex" && pathFromWhich.Contains("homebrew")) {
                    // Continue to fallback if we prefer NVM
                } else {
                    return pathFromWhich;
                }
            }

            // 3. Common system paths
            string[] searchPaths = { 
                "/usr/local/bin/" + name, 
                "/opt/homebrew/bin/" + name, 
                "/usr/bin/" + name, 
                "/bin/" + name 
            };

            foreach (string path in searchPaths)
            {
                if (File.Exists(path)) return path;
            }

            return name;
        }

        private static string GetPathFromWhich(string name)
        {
            ProcessStartInfo psi = new ProcessStartInfo 
            { 
                FileName = "/usr/bin/which", 
                Arguments = name, 
                RedirectStandardOutput = true, 
                UseShellExecute = false, 
                CreateNoWindow = true 
            };

            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                string pathEnv = "";
                try { pathEnv = Environment.GetEnvironmentVariable("PATH"); } catch(Exception) {}
                // Don't inject Homebrew here, let it find what's in the actual PATH
                psi.EnvironmentVariables["PATH"] = pathEnv;
            }

            try
            {
                using (Process p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit();
                    if (p.ExitCode == 0 && !string.IsNullOrEmpty(output)) return output;
                }
            }
            catch (Exception) { }
            return name;
        }

        private static ProcessStartInfo CreateProcessStartInfo(string command)
        {
            bool isWindows = Application.platform == RuntimePlatform.WindowsEditor;
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = isWindows ? "cmd.exe" : "/bin/bash",
                Arguments = isWindows ? ("/c \"" + command + "\"") : ("-c \"" + command + "\""),
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (!isWindows)
            {
                string pathEnv = "";
                try { pathEnv = Environment.GetEnvironmentVariable("PATH"); } catch(Exception) {}
                
                // Add common locations but keep existing PATH to support NVM, etc.
                psi.EnvironmentVariables["PATH"] = pathEnv + ":/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin";
            }
            return psi;
        }

        private static bool RunInstallerProcess(ProcessStartInfo psi, string cliPath, bool showSuccessDialog, string cliName)
        {
            try
            {
                using (Process p = Process.Start(psi))
                {
                    string error = p.StandardError.ReadToEnd();
                    p.WaitForExit();

                    if (p.ExitCode == 0)
                    {
                        return true;
                    }
                    else
                    {
                        string msg = "CLI command failed at " + cliPath + ".\n\nExit Code: " + p.ExitCode + "\nError: " + error;
                        UnityEngine.Debug.LogWarning("[MCP] " + msg);
                        
                        if (showSuccessDialog) // If this was supposed to be the final step
                        {
                            EditorUtility.DisplayDialog("MCP Error", "Failed to link to " + cliName + " CLI.\n\n" + error, "OK");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[MCP] Process start failed: " + e.Message);
            }
            return false;
        }
    }
}