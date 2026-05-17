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
        /// Attempts to link the current Unity project to the Antigravity CLI.
        /// </summary>
        public static void LinkToAntigravity()
        {
            if (DeployBridgeScript(out string destinationPath))
            {
                string pythonPath = ResolveExecutablePath("python3");
                ExecuteAntigravityLinkSequence(destinationPath, pythonPath);
            }
        }

        private static void ExecuteAntigravityLinkSequence(string scriptPath, string pythonPath)
        {
            string agPath = ResolveExecutablePath("ag");
            if (string.IsNullOrEmpty(agPath)) agPath = ResolveExecutablePath("antigravity");

            // 1. Ensure clean slate
            string removeCommand = "\"" + agPath + "\" mcp remove nexus-unity";
            RunInstallerProcess(CreateProcessStartInfo(removeCommand), agPath, false, "Antigravity");

            // 2. Add new registration
            string addCommand = "\"" + agPath + "\" mcp add nexus-unity --command \"" + pythonPath + "\" --args \"" + scriptPath + "\"";
            RunInstallerProcess(CreateProcessStartInfo(addCommand), agPath, true, "Antigravity");
        }
    }
}
