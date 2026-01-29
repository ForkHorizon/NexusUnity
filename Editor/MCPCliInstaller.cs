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
            string scriptPath = Path.GetFullPath("Assets/NexusUnity/Editor/nexus_unity_bridge.py");
            if (!File.Exists(scriptPath))
            {
                UnityEngine.Debug.LogError($"[MCP] Could not find bridge script at: {scriptPath}");
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