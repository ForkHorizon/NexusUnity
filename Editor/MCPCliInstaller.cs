using UnityEditor;
using UnityEngine;
using System.IO;
using System.Diagnostics;

namespace UnityMCP.Editor
{
    public static class MCPCliInstaller
    {
        [MenuItem("Window/Unity MCP/Link to Gemini CLI")]
        public static void LinkToGemini()
        {
            // Find the directory where this script is located
            string[] guids = AssetDatabase.FindAssets("MCPCliInstaller t:Script");
            string scriptPath = "";
            
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("NexusUnity")) // Ensure we are in our lib, not a name collision
                {
                    string dir = Path.GetDirectoryName(path);
                    string potentialBridge = Path.Combine(dir, "nexus_unity_bridge.py");
                    if (File.Exists(Path.GetFullPath(potentialBridge)))
                    {
                        scriptPath = Path.GetFullPath(potentialBridge);
                        break;
                    }
                }
            }

            // Fallback: search all assets for the filename
            if (string.IsNullOrEmpty(scriptPath))
            {
                string[] allPaths = AssetDatabase.GetAllAssetPaths();
                foreach (var path in allPaths)
                {
                    if (path.EndsWith("nexus_unity_bridge.py"))
                    {
                        scriptPath = Path.GetFullPath(path);
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
            {
                UnityEngine.Debug.LogError("[MCP] Could not find 'nexus_unity_bridge.py' in the project.");
                EditorUtility.DisplayDialog("MCP Error", "Could not find 'nexus_unity_bridge.py'.\n\nEnsure the library is correctly imported and that the .py file has not been deleted.", "OK");
                return;
            }

            string command = $"gemini mcp add nexus-unity python3 \"{scriptPath}\"";
            UnityEngine.Debug.Log($"[MCP] Executing: {command}");

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process p = Process.Start(psi))
            {
                string output = p.StandardOutput.ReadToEnd();
                string error = p.StandardError.ReadToEnd();
                p.WaitForExit();

                if (p.ExitCode == 0)
                {
                    EditorUtility.DisplayDialog("MCP Success", "Successfully linked NexusUnity to Gemini CLI!\n\nYou can now use Unity commands in your terminal.", "OK");
                }
                else
                {
                    UnityEngine.Debug.LogError($"[MCP] Failed to link: {error}");
                    EditorUtility.DisplayDialog("MCP Error", $"Failed to link to Gemini CLI.\n\nError: {error}\n\nMake sure 'gemini' command is in your PATH.", "OK");
                }
            }
        }
    }
}