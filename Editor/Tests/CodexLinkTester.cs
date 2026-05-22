using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor {
    public static class CodexLinkTester {
        [MenuItem("Window/Nexus Unity/Test Codex Link")]
        public static void TestLink() {
            Debug.Log("[Test] Starting Codex link test...");
            MCPCliInstaller.LinkToCodex();
            Debug.Log("[Test] Codex link test finished.");
        }
    }
}
