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
        /// Links the current Unity project to Gemini CLI by deploying the bridge script and replacing the local <c>nexus-unity</c> MCP registration.
        /// </summary>
        /// <remarks>
        /// This editor action modifies project-root bridge files, resolves local Gemini and Python executables, launches Gemini MCP
        /// remove/add commands, and reports setup failures through Nexus editor logging or Unity dialogs.
        /// </remarks>
        public static void LinkToGemini()
        {
            if (DeployBridgeScript(out string destinationPath))
            {
                ExecuteGeminiLinkSequence(destinationPath);
            }
        }

        private static void ExecuteGeminiLinkSequence(string scriptPath)
        {
            string geminiPath = ResolveExecutablePath("gemini");
            string pythonPath = ResolvePythonPath();

            // 1. Ensure clean slate by removing existing registration
            RunInstallerProcess(CreateProcessStartInfo(geminiPath, "mcp", "remove", "nexus-unity"), geminiPath, false, "Gemini");

            // 2. Add new registration with stable path
            RunInstallerProcess(CreateProcessStartInfo(geminiPath, "mcp", "add", "nexus-unity", "--trust", "-e", MCPServer.AuthTokenEnvironmentVariable + "=" + MCPServer.AuthToken, pythonPath, scriptPath), geminiPath, true, "Gemini");
        }
    }
}
