using UnityEngine;

namespace UnityMCP.Editor {
    /// <summary>
    /// Provides a maintainer-only diagnostic action that runs the Codex CLI linking flow from the Nexus Unity resources panel.
    /// </summary>
    /// <remarks>
    /// The action can modify the local Codex configuration on the developer machine by delegating to
    /// <see cref="MCPCliInstaller.LinkToCodex"/>, and writes progress to the Nexus diagnostics log category.
    /// </remarks>
    public static class CodexLinkTester {
        /// <summary>
        /// Runs the Codex integration link diagnostic and records start and finish messages in the Unity Console.
        /// </summary>
        /// <remarks>
        /// This method is intended for manual troubleshooting from the advanced diagnostics UI and may update local MCP client
        /// configuration files through the Codex installer helper.
        /// </remarks>
        public static void TestLink() {
            NexusEditorLog.Log(NexusLogCategory.Diagnostics, "[Test] Starting Codex link test...", true);
            MCPCliInstaller.LinkToCodex();
            NexusEditorLog.Log(NexusLogCategory.Diagnostics, "[Test] Codex link test finished.", true);
        }
    }
}
