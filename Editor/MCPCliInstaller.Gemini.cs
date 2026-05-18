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
        /// Attempts to link the current Unity project to the local Gemini CLI instance.
        /// </summary>
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
