using UnityEngine;

namespace UnityMCP.Editor {
    public static class CodexLinkTester {
        public static void TestLink() {
            NexusEditorLog.Log(NexusLogCategory.Diagnostics, "[Test] Starting Codex link test...", true);
            MCPCliInstaller.LinkToCodex();
            NexusEditorLog.Log(NexusLogCategory.Diagnostics, "[Test] Codex link test finished.", true);
        }
    }
}
