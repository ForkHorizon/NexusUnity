using UnityEngine;

namespace UnityMCP.Editor {
    public static class CodexLinkTester {
        public static void TestLink() {
            Debug.Log("[Test] Starting Codex link test...");
            MCPCliInstaller.LinkToCodex();
            Debug.Log("[Test] Codex link test finished.");
        }
    }
}
