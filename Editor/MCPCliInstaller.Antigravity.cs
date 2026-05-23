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
        /// Links the current Unity project to the local Antigravity CLI by deploying the bridge script and executing MCP remove/add commands.
        /// </summary>
        /// <remarks>
        /// This editor action modifies project-root bridge files, resolves <c>python3</c> and Antigravity executables,
        /// launches local CLI processes, and reports failures through Nexus editor logs or dialogs.
        /// </remarks>
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
