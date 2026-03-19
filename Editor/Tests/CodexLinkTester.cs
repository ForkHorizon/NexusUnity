using UnityEditor;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor {
    public static class CodexLinkTester {
        [MenuItem("Nexus/Test Codex Link")]
        public static void TestLink() {
            Debug.Log("[Test] Starting Codex link test...");
            MCPCliInstaller.LinkToCodex();
            Debug.Log("[Test] Codex link test finished.");
        }
    }
}
