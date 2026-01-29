using UnityEditor;
using UnityEngine;
using System.IO;
using System.Diagnostics;

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

        private static void ExecuteLinkCommand(string scriptPath)
        {
            string command = $"gemini mcp add nexus-unity python3 \"{scriptPath}\"";
            UnityEngine.Debug.Log($"[MCP] Executing: {command}");

            bool isWindows = Application.platform == RuntimePlatform.WindowsEditor;
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = isWindows ? "cmd.exe" : "/bin/bash",
                Arguments = isWindows ? $"/c \"{command}\"" : $"-c \"{command}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process p = Process.Start(psi))
            {
                string error = p.StandardError.ReadToEnd();
                p.WaitForExit();

                if (p.ExitCode == 0)
                {
                    EditorUtility.DisplayDialog("MCP Success", "Successfully linked NexusUnity to Gemini CLI!", "OK");
                }
                else
                {
                    string msg = $"Failed to link to Gemini CLI.\n\nExit Code: {p.ExitCode}\nError: {error}\n\nCommand: {command}\n\nEnsure 'gemini' is installed and accessible in your system path.";
                    UnityEngine.Debug.LogError($"[MCP] {msg}");
                    EditorUtility.DisplayDialog("MCP Error", msg, "OK");
                }
            }
        }
    }
}
