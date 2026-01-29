using UnityEditor;
using UnityEngine;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Handles the integration of the Unity MCP server with the Gemini CLI.
    /// </summary>
    public static class MCPCliInstaller
    {
        /// <summary>
        /// Attempts to link the current Unity project to the local Gemini CLI instance.
        /// </summary>
        public static void LinkToGemini()
        {
            string scriptPath = FindBridgeScript();

            if (string.IsNullOrEmpty(scriptPath))
            {
                UnityEngine.Debug.LogError("[MCP] Could not find 'nexus_unity_bridge.py' in the project.");
                EditorUtility.DisplayDialog("MCP Error", "Could not find 'nexus_unity_bridge.py'.\n\nEnsure the library is correctly imported.", "OK");
                return;
            }

            ExecuteLinkCommand(scriptPath);
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

            string[] searchPaths = { $"/opt/homebrew/bin/{name}", $"/usr/local/bin/{name}", $"/usr/bin/{name}", $"/bin/{name}" };
            foreach (string path in searchPaths)
            {
                if (File.Exists(path)) return path;
            }

            return GetPathFromWhich(name);
        }

        private static string GetPathFromWhich(string name)
        {
            ProcessStartInfo psi = new ProcessStartInfo { FileName = "/usr/bin/which", Arguments = name, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            try
            {
                using (Process p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit();
                    if (p.ExitCode == 0 && !string.IsNullOrEmpty(output)) return output;
                }
            }
            catch {{ }}
            return name;
        }

        private static void ExecuteLinkCommand(string scriptPath)
        {
            string geminiPath = ResolveExecutablePath("gemini");
            string pythonPath = ResolveExecutablePath("python3");
            // Add --trust flag to mark the local server as trusted, bypassing OAuth/Confirmation
            string command = $"\"{geminiPath}\" mcp add nexus-unity --trust \"{pythonPath}\" \"{scriptPath}\"";
            
            ProcessStartInfo psi = CreateProcessStartInfo(command);
            RunInstallerProcess(psi, geminiPath);
        }

        private static ProcessStartInfo CreateProcessStartInfo(string command)
        {
            bool isWindows = Application.platform == RuntimePlatform.WindowsEditor;
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = isWindows ? "cmd.exe" : "/bin/bash",
                Arguments = isWindows ? $"/c \"{command}\"" : $"-c \"{command}\"",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (!isWindows)
            {
                string currentPath = psi.EnvironmentVariables["PATH"] ?? "";
                psi.EnvironmentVariables["PATH"] = $"/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:{currentPath}";
            }
            return psi;
        }

        private static void RunInstallerProcess(ProcessStartInfo psi, string geminiPath)
        {
            using (Process p = Process.Start(psi))
            {
                string error = p.StandardError.ReadToEnd();
                p.WaitForExit();

                if (p.ExitCode == 0)
                {
                    EditorUtility.DisplayDialog("MCP Success", "Successfully linked Nexus Unity to your system Gemini CLI!", "OK");
                }
                else
                {
                    string msg = $"Failed to link to Gemini CLI.\n\nExit Code: {p.ExitCode}\nError: {error}\n\nPath used: {geminiPath}";
                    UnityEngine.Debug.LogError($"[MCP] {msg}");
                    EditorUtility.DisplayDialog("MCP Error", msg, "OK");
                }
            }
        }
    }
}