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
            // Dynamically find the script path to support both Assets and Packages folders
            string[] guids = AssetDatabase.FindAssets("nexus_unity_bridge t:DefaultAsset");
            string scriptPath = "";
            
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("nexus_unity_bridge.py"))
                {
                    scriptPath = Path.GetFullPath(path);
                    break;
                }
            }

            if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
            {
                UnityEngine.Debug.LogError("[MCP] Could not find 'nexus_unity_bridge.py' in the project.");
                EditorUtility.DisplayDialog("MCP Error", "Could not find 'nexus_unity_bridge.py'.\n\nEnsure the library is correctly imported.", "OK");
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