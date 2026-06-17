using UnityEngine;

namespace UnityMCP.Editor {
    /// <summary>
    /// Provides a maintainer-only diagnostic action that runs the Codex CLI linking flow from the Nexus Unity resources panel.
    /// </summary>
    /// <remarks>
    /// This is a thin troubleshooting wrapper: it logs start and finish messages to the Unity Console and delegates the actual
    /// linking work — including any local Codex/MCP client configuration changes — to <see cref="MCPCliInstaller.LinkToCodex"/>.
    /// </remarks>
    public static class CodexLinkTester {
        /// <summary>
        /// Runs the Codex integration link diagnostic, logging start and finish messages to the Unity Console.
        /// </summary>
        /// <remarks>
        /// Intended for manual troubleshooting from the advanced diagnostics UI. The actual linking and any local MCP client
        /// configuration changes are performed by <see cref="MCPCliInstaller.LinkToCodex"/>; this method itself only logs and delegates.
        /// </remarks>
        public static void TestLink() {
            NexusEditorLog.Log(NexusLogCategory.Diagnostics, "[Test] Starting Codex link test...", true);
            MCPCliInstaller.LinkToCodex();
            NexusEditorLog.Log(NexusLogCategory.Diagnostics, "[Test] Codex link test finished.", true);
        }
    }
}
