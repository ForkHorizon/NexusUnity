using UnityEditor;
using UnityEngine;
using UnityMCP.Editor;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Runs editor UI automation smoke checks by creating the MCP test window and exercising it through Nexus JSON-RPC methods.
    /// </summary>
    /// <remarks>
    /// Verification shows and resets <see cref="MCPTestWindow"/>, queries window hierarchy, sends text input and click events,
    /// logs diagnostics, and throws exceptions when a UI automation step fails.
    /// </remarks>
    public static class UIVerification
    {
        /// <summary>
        /// Creates or focuses the MCP test window, resets its UI state, runs list/hierarchy/input/click checks, and logs the result.
        /// </summary>
        public static void Verify()
        {
            NexusEditorLog.Log(NexusLogCategory.Diagnostics, "Starting UI Verification...", true);
            
            var wnd = MCPTestWindow.ShowWindow();
            wnd.ResetState();
            
            TestListAndHierarchy();
            TestInputAndClick(wnd);
            TestScreenshots();
            NexusEditorLog.Log(NexusLogCategory.Diagnostics, "VERIFICATION SUCCESS", true);
        }

        private static void TestListAndHierarchy()
        {
            string resp = Call("ui_list_windows", null);
            if (!resp.Contains(MCPTestWindow.WindowTitle)) throw new System.Exception("List failed");
            string hier = Call("ui_get_hierarchy", new JObject { ["window_title"] = MCPTestWindow.WindowTitle });
            if (!hier.Contains("TestButton")) throw new System.Exception("Hierarchy failed");
        }

        private static void TestInputAndClick(MCPTestWindow wnd)
        {
            string txt = "Test";
            Call("ui_input_text", new JObject { ["window_title"] = MCPTestWindow.WindowTitle, ["element_name"] = "TestInput", ["text"] = txt });
            if (wnd.LastInputValue != txt) throw new System.Exception($"Input failed: expected '{txt}' but got '{wnd.LastInputValue}'");
            Call("ui_click", new JObject { ["window_title"] = MCPTestWindow.WindowTitle, ["element_name"] = "TestButton" });
            if (!wnd.ButtonClicked) throw new System.Exception("Click failed");
        }

        private static void TestScreenshots()
        {
            Call("execute_menu_item", new JObject { ["item_path"] = "Window/General/Game" });
            AssertScreenshot(Call("capture_game_view_screenshot", null), "Game View");

            Call("execute_menu_item", new JObject { ["item_path"] = "Window/General/Inspector" });
            AssertScreenshot(Call("capture_inspector_screenshot", null), "Inspector");
        }

        private static void AssertScreenshot(string response, string windowName)
        {
            JObject result = JObject.Parse(response);
            if (result["success"]?.Value<bool>() != true)
                throw new System.Exception($"{windowName} screenshot failed: {result.ToString(Newtonsoft.Json.Formatting.None)}");

            JObject data = (JObject)result["data"];
            byte[] image = System.Convert.FromBase64String(data?["image_base64"]?.ToString() ?? string.Empty);
            byte[] signature = { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a };
            if (image.Length <= 5 * 1024 || image.Length < signature.Length || !image.Take(signature.Length).SequenceEqual(signature))
                throw new System.Exception($"{windowName} screenshot is not a non-trivial PNG.");
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
