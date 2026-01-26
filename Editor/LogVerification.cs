using UnityEngine;
using UnityEditor;
using UnityMCP.Editor;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Reflection;

public static class LogVerification
{
    [MenuItem("Tools/Verify MCP Logs")]
    public static void Verify()
    {
        Debug.Log("Starting Verification...");

        // 1. Force enable the window logic to hook events
        var window = ScriptableObject.CreateInstance<MCPServerWindow>();
        var onEnable = typeof(MCPServerWindow).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance);
        onEnable.Invoke(window, null);

        // 2. Generate Logs
        Debug.Log("Test Log Unique " + System.Guid.NewGuid());
        Debug.LogError("Test Error Unique " + System.Guid.NewGuid());

        string dupMsg = "Test Duplicate " + System.Guid.NewGuid();
        Debug.Log(dupMsg);
        Debug.Log(dupMsg); // Should increment count

        // Allow main thread to process (in case threaded logs need time)
        // In Editor, direct Debug.Log usually fires immediately on main thread.

        // 3. Query
        string json = "{\"jsonrpc\": \"2.0\", \"method\": \"read_logs\", \"params\": {\"count\": 10}, \"id\": 1}";
        string response = MCPServerMethods.ProcessJsonRpc(json);

        Debug.Log("MCP Response: " + response);

        // 4. Validate
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
        {
            Debug.Log("VERIFICATION SUCCESS: Logs retrieved, types correct, deduplication working.");
        }
        else
        {
            Debug.LogError($"VERIFICATION FAILED: ErrorFound={foundError}, DupFound={foundDup}");
        }

        // Cleanup
        var onDisable = typeof(MCPServerWindow).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance);
        onDisable.Invoke(window, null);
        Object.DestroyImmediate(window);
    }
}
