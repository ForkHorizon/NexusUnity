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
        public static void Verify()
        {
            NexusEditorLog.Log(NexusLogCategory.Diagnostics, "Starting UI Verification...", true);
            
            var wnd = MCPTestWindow.ShowWindow();
            wnd.ResetState();
            
            TestListAndHierarchy();
            TestInputAndClick();
            NexusEditorLog.Log(NexusLogCategory.Diagnostics, "VERIFICATION SUCCESS", true);
        }

        private static void TestListAndHierarchy()
        {
            string resp = Call("ui_list_windows", null);
            if (!resp.Contains(MCPTestWindow.WindowTitle)) throw new System.Exception("List failed");
            string hier = Call("ui_get_hierarchy", new JObject { ["window_title"] = MCPTestWindow.WindowTitle });
            if (!hier.Contains("TestButton")) throw new System.Exception("Hierarchy failed");
        }

        private static void TestInputAndClick()
        {
            string txt = "Test";
            Call("ui_input_text", new JObject { ["window_title"] = MCPTestWindow.WindowTitle, ["element_name"] = "TestInput", ["text"] = txt });
            if (MCPTestWindow.LastInputValue != txt) throw new System.Exception($"Input failed: expected '{txt}' but got '{MCPTestWindow.LastInputValue}'");
            Call("ui_click", new JObject { ["window_title"] = MCPTestWindow.WindowTitle, ["element_name"] = "TestButton" });
            if (!MCPTestWindow.ButtonClicked) throw new System.Exception("Click failed");
        }

        private static string Call(string m, JObject p)
        {
            string resp = MCPServerMethods.ProcessJsonRpc(new JObject { ["jsonrpc"] = "2.0", ["method"] = m, ["params"] = p, ["id"] = 1 }.ToString());
            JObject json = JObject.Parse(resp);
            if (json["error"] != null)
            {
                throw new System.Exception($"MCP Error in {m}: {json["error"]["message"]}");
            }
            return json["result"]?.ToString() ?? "";
        }
    }
}
