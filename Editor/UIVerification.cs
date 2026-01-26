using UnityEditor;
using UnityEngine;
using UnityMCP.Editor;
using Newtonsoft.Json.Linq;
using System.Linq;

public static class UIVerification
{
    [MenuItem("Tools/Verify UI Instruments")]
    public static void Verify()
    {
        Debug.Log("Starting UI Verification...");

        // 1. Open Test Window
        MCPTestWindow.ShowWindow();

        // Allow UI to refresh
        // In a real integration test we would wait, but here we assume immediate availability for VisualElement creation
        // However, CreateGUI runs when window is added.

        // 2. Test ui_list_windows
        string jsonList = "{\"jsonrpc\": \"2.0\", \"method\": \"ui_list_windows\", \"id\": 1}";
        string responseList = MCPServerMethods.ProcessJsonRpc(jsonList);
        Debug.Log("ui_list_windows response: " + responseList);

        if (!responseList.Contains("MCPTestWindow"))
        {
            Debug.LogError("FAILED: MCPTestWindow not found in ui_list_windows");
            return;
        }

        // 3. Test ui_get_hierarchy
        string jsonHier = "{\"jsonrpc\": \"2.0\", \"method\": \"ui_get_hierarchy\", \"params\": {\"window_title\": \"MCPTestWindow\"}, \"id\": 2}";
        string responseHier = MCPServerMethods.ProcessJsonRpc(jsonHier);
        // Debug.Log("ui_get_hierarchy response: " + responseHier); // Verbose

        if (!responseHier.Contains("TestButton") || !responseHier.Contains("TestInput"))
        {
            Debug.LogError("FAILED: TestButton or TestInput not found in hierarchy");
            return;
        }

        // 4. Test ui_input_text
        string testText = "Hello MCP " + System.Guid.NewGuid();
        string jsonInput = "{\"jsonrpc\": \"2.0\", \"method\": \"ui_input_text\", \"params\": {\"window_title\": \"MCPTestWindow\", \"element_name\": \"TestInput\", \"text\": \"" + testText + "\"}, \"id\": 3}";
        string responseInput = MCPServerMethods.ProcessJsonRpc(jsonInput);
        Debug.Log("ui_input_text response: " + responseInput);

        if (MCPTestWindow.LastInputValue != testText)
        {
            Debug.LogError($"FAILED: Input text mismatch. Expected '{testText}', got '{MCPTestWindow.LastInputValue}'");
            return;
        }

        // 5. Test ui_click
        MCPTestWindow.ButtonClicked = false;
        string jsonClick = "{\"jsonrpc\": \"2.0\", \"method\": \"ui_click\", \"params\": {\"window_title\": \"MCPTestWindow\", \"element_name\": \"TestButton\"}, \"id\": 4}";
        string responseClick = MCPServerMethods.ProcessJsonRpc(jsonClick);
        Debug.Log("ui_click response: " + responseClick);

        if (!MCPTestWindow.ButtonClicked)
        {
            Debug.LogError("FAILED: ButtonClicked flag was not set after ui_click");
            return;
        }

        Debug.Log("VERIFICATION SUCCESS: All UI tests passed!");

        // Cleanup
        var wnd = EditorWindow.GetWindow<MCPTestWindow>();
        if (wnd) wnd.Close();
    }
}
