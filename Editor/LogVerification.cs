using UnityEngine;
using UnityEditor;
using UnityMCP.Editor;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Reflection;

/// <summary>
/// Runs an editor-only smoke check for Nexus Unity log capture by creating a temporary server window,
/// invoking its private lifecycle hook through reflection, writing Unity Console messages, and querying them through JSON-RPC.
/// </summary>
public static class LogVerification
{
    /// <summary>
    /// Executes the log verification sequence, including editor log output, temporary window setup and teardown,
    /// Unity Console writes, JSON-RPC log retrieval, and response validation for duplicate/error capture.
    /// </summary>
    public static void Verify()
    {
        NexusEditorLog.Log(NexusLogCategory.Diagnostics, "Starting Verification...", true);
        var window = SetupVerificationWindow();
        string dupMsg = GenerateTestLogs();
        
        string json = "{\"jsonrpc\": \"2.0\", \"method\": \"read_logs\", \"params\": {\"count\": 10}, \"id\": 1}";
        string response = MCPServerMethods.ProcessJsonRpc(json);
        NexusEditorLog.Log(NexusLogCategory.Diagnostics, "MCP Response: " + response);

        ValidateResponse(response, dupMsg);
        CleanupVerificationWindow(window);
    }

    /// <summary>
    /// Creates and enables a temporary MCPServerWindow instance.
    /// </summary>
    private static MCPServerWindow SetupVerificationWindow()
    {
        var window = ScriptableObject.CreateInstance<MCPServerWindow>();
        var onEnable = typeof(MCPServerWindow).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance);
        onEnable.Invoke(window, null);
        return window;
    }

    /// <summary>
    /// Generates various types of test logs to verify capture logic.
    /// </summary>
    private static string GenerateTestLogs()
    {
        // Intentionally writes direct Unity Console entries; this diagnostic verifies log capture itself.
        Debug.Log("Test Log Unique " + System.Guid.NewGuid());
        Debug.LogError("Test Error Unique " + System.Guid.NewGuid());
        string dupMsg = "Test Duplicate " + System.Guid.NewGuid();
        Debug.Log(dupMsg);
        Debug.Log(dupMsg);
        return dupMsg;
    }

    /// <summary>
    /// Parses the MCP JSON response and validates that logs were correctly captured.
    /// </summary>
    private static void ValidateResponse(string response, string dupMsg)
    {
        JObject resp = JObject.Parse(response);
        if (resp["error"] != null)
        {
            NexusEditorLog.Error(NexusLogCategory.Diagnostics, "MCP Error: " + resp["error"]);
            return;
        }

        JArray result = (JArray)resp["result"];
        bool foundError = result.Any(x => x["type"].ToString().Contains("Error"));
        bool foundDup = result.Any(x => x["message"].ToString() == dupMsg && (int)x["count"] >= 2);

        if (foundError && foundDup)
            NexusEditorLog.Log(NexusLogCategory.Diagnostics, "VERIFICATION SUCCESS: Logs retrieved, types correct, deduplication working.", true);
        else
            NexusEditorLog.Error(NexusLogCategory.Diagnostics, $"VERIFICATION FAILED: ErrorFound={foundError}, DupFound={foundDup}");
    }

    /// <summary>
    /// Disables and destroys the temporary MCPServerWindow instance.
    /// </summary>
    private static void CleanupVerificationWindow(MCPServerWindow window)
    {
        var onDisable = typeof(MCPServerWindow).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance);
        onDisable.Invoke(window, null);
        Object.DestroyImmediate(window);
    }
}
