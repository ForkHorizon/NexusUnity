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
            string pythonPath = ResolveExecutablePath("python3");

            // 1. Ensure clean slate by removing existing registration
            string removeCommand = "\"" + geminiPath + "\" mcp remove nexus-unity";
            RunInstallerProcess(CreateProcessStartInfo(removeCommand), geminiPath, false, "Gemini");

            // 2. Add new registration with stable path
            string addCommand = "\"" + geminiPath + "\" mcp add nexus-unity --trust \"" + pythonPath + "\" \"" + scriptPath + "\"";
            RunInstallerProcess(CreateProcessStartInfo(addCommand), geminiPath, true, "Gemini");
        }
    }
}
