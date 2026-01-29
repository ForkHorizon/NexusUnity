using UnityEngine;
using UnityEditor;
using UnityMCP.Editor;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Reflection;

/// <summary>
/// Utility class to verify the MCP log capturing and retrieval system.
/// </summary>
public static class LogVerification
{
    /// <summary>
    /// Executes a verification sequence: hooks logs, generates test logs, queries via MCP, and validates results.
    /// </summary>
    public static void Verify()
    {
        Debug.Log("Starting Verification...");
        var window = SetupVerificationWindow();
        string dupMsg = GenerateTestLogs();
        
        string json = "{\"jsonrpc\": \"2.0\", \"method\": \"read_logs\", \"params\": {\"count\": 10}, \"id\": 1}";
        string response = MCPServerMethods.ProcessJsonRpc(json);
        Debug.Log("MCP Response: " + response);

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
            Debug.LogError("MCP Error: " + resp["error"]);
            return;
        }

        JArray result = (JArray)resp["result"];
        bool foundError = result.Any(x => x["type"].ToString().Contains("Error"));
        bool foundDup = result.Any(x => x["message"].ToString() == dupMsg && (int)x["count"] >= 2);

        if (foundError && foundDup)
            Debug.Log("VERIFICATION SUCCESS: Logs retrieved, types correct, deduplication working.");
        else
            Debug.LogError($"VERIFICATION FAILED: ErrorFound={foundError}, DupFound={foundDup}");
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
