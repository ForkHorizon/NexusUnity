using UnityEditor;
using UnityEngine;
using UnityMCP.Editor;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Utility class for verifying UI state in tests.
    /// </summary>
    public static class UIVerification
    {
        /// <summary>Performs UI state verification.</summary>
        [MenuItem("Window/Unity MCP/Verify UI Instruments")]
        public static void Verify()
        {
            Debug.Log("Starting UI Verification...");
            MCPTestWindow.ShowWindow();
            TestListAndHierarchy();
            TestInputAndClick();
            Debug.Log("VERIFICATION SUCCESS");
        }

        private static void TestListAndHierarchy()
        {
            string resp = Call("ui_list_windows", null);
            if (!resp.Contains("MCPTestWindow")) throw new System.Exception("List failed");
            string hier = Call("ui_get_hierarchy", new JObject { ["window_title"] = "MCPTestWindow" });
            if (!hier.Contains("TestButton")) throw new System.Exception("Hierarchy failed");
        }

        private static void TestInputAndClick()
        {
            string txt = "Test";
            Call("ui_input_text", new JObject { ["window_title"] = "MCPTestWindow", ["element_name"] = "TestInput", ["text"] = txt });
            if (MCPTestWindow.LastInputValue != txt) throw new System.Exception("Input failed");
            Call("ui_click", new JObject { ["window_title"] = "MCPTestWindow", ["element_name"] = "TestButton" });
            if (!MCPTestWindow.ButtonClicked) throw new System.Exception("Click failed");
        }

        private static string Call(string m, JObject p) => MCPServerMethods.ProcessJsonRpc(new JObject { ["jsonrpc"] = "2.0", ["method"] = m, ["params"] = p, ["id"] = 1 }.ToString());
    }
}
