using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Window for verifying MCP functionality in the editor.
    /// </summary>
    public class MCPVerificationWindow : EditorWindow
    {
        /// <summary>Shows the MCP Verification window.</summary>
        public static void ShowWindow() => GetWindow<MCPVerificationWindow>("MCP Verification");

        private void OnGUI()
        {
            if (GUILayout.Button("Run API Verification")) RunVerification();
        }

        private void RunVerification()
        {
            Debug.Log("Starting Verification...");
            VerifyScenesAndMaterials();
            VerifyGameObjectsAndComponents();
            VerifyAssetsAndLogs();
            Debug.Log("Verification Complete.");
        }

        private void VerifyScenesAndMaterials()
        {
            Call("create_scene", new JObject { ["name"] = "VerifyScene" });
            Call("create_material", new JObject { ["name"] = "VerifyMat" });
            Call("refresh_asset_database", null);
        }

        private void VerifyGameObjectsAndComponents()
        {
            string resp = Call("create_game_object", new JObject { ["name"] = "VerifyGO" });
            string resultJson = ExtractResult(resp);
            
            if (string.IsNullOrEmpty(resultJson) || resultJson == "{}")
            {
                Debug.LogError("Failed to create GameObject for verification.");
                return;
            }

            var goRes = JObject.Parse(resultJson);
            if (goRes["instance_id"] == null)
            {
                Debug.LogError("GameObject creation failed: missing instance_id");
                return;
            }

            int id = (int)goRes["instance_id"];
            Call("add_component", new JObject { ["instance_id"] = id, ["component_name"] = "BoxCollider" });
            Call("update_component", new JObject { ["instance_id"] = id, ["component_name"] = "BoxCollider", ["json_data"] = "{\"isTrigger\":true}" });
            Call("set_transform", new JObject { ["instance_id"] = id, ["position"] = new JObject { ["x"] = 5 } });
        }

        private void VerifyAssetsAndLogs()
        {
            Call("list_assets", new JObject { ["filter"] = "t:Material" });
            Call("read_logs", new JObject { ["count"] = 5 });
        }

        private string Call(string method, JObject parameters) => MCPServerMethods.ProcessJsonRpc(new JObject { ["jsonrpc"] = "2.0", ["method"] = method, ["params"] = parameters, ["id"] = 1 }.ToString());

        private string ExtractResult(string resp)
        {
            try
            {
                var obj = JObject.Parse(resp);
                if (obj["error"] != null)
                {
                    Debug.LogError($"RPC Error: {obj["error"]["message"]}");
                    return "{}";
                }
                return obj["result"]?.ToString() ?? "{}";
            }
            catch (Exception e)
            {
                Debug.LogError($"JSON Parse Error in ExtractResult: {e.Message}");
                return "{}";
            }
        }
    }
}
