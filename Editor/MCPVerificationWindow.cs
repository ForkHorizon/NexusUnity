using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using System.IO;

namespace UnityMCP.Editor
{
    public class MCPVerificationWindow : EditorWindow
    {
        [MenuItem("Tools/MCP Verification")]
        public static void ShowWindow()
        {
            GetWindow<MCPVerificationWindow>("MCP Verification");
        }

        private void OnGUI()
        {
            if (GUILayout.Button("Run API Verification"))
            {
                RunVerification();
            }
        }

        private void RunVerification()
        {
            Debug.Log("Starting Verification...");

            // 1. Create Scene
            string sceneRes = Call("create_scene", new JObject { ["name"] = "VerificationScene" });
            Debug.Log($"Create Scene: {sceneRes}");

            // 2. Create Material
            string matRes = Call("create_material", new JObject { ["name"] = "VerifyMat", ["shader"] = "Standard" });
            Debug.Log($"Create Material: {matRes}");

            // 2.5 Refresh Assets
            string refreshRes = Call("refresh_asset_database", null);
            Debug.Log($"Refresh Assets: {refreshRes}");

            // 3. Create GameObject
            string goRes = Call("create_game_object", new JObject { ["name"] = "VerifyGO" });
            Debug.Log($"Create GO: {goRes}");
            var goJson = JObject.Parse(ExtractResult(goRes));
            int goId = (int)goJson["instance_id"];

            // 4. Add Component (BoxCollider)
            string compRes = Call("add_component", new JObject { ["instance_id"] = goId, ["component_name"] = "UnityEngine.BoxCollider" });
            Debug.Log($"Add Component: {compRes}");

            // 5. Update Component
            string updateRes = Call("update_component", new JObject {
                ["instance_id"] = goId,
                ["component_name"] = "UnityEngine.BoxCollider",
                ["json_data"] = "{\"isTrigger\":true}"
            });
            Debug.Log($"Update Component: {updateRes}");

            // 6. Inspect Component
            string inspectRes = Call("inspect_component", new JObject { ["instance_id"] = goId, ["component_name"] = "UnityEngine.BoxCollider" });
            Debug.Log($"Inspect Component: {inspectRes}");

            // 7. Set Transform
            string transRes = Call("set_transform", new JObject {
                ["instance_id"] = goId,
                ["position"] = new JObject { ["x"] = 10, ["y"] = 0, ["z"] = 0 }
            });
            Debug.Log($"Set Transform: {transRes}");

            Debug.Log("Verification Complete. Check Console for details.");
        }

        private string Call(string method, JObject parameters)
        {
            var req = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = method,
                ["params"] = parameters,
                ["id"] = 1
            };
            return MCPServerMethods.ProcessJsonRpc(req.ToString());
        }

        private string ExtractResult(string jsonResponse)
        {
            var obj = JObject.Parse(jsonResponse);
            if (obj["error"] != null)
            {
                Debug.LogError($"RPC Error: {obj["error"]["message"]}");
                return "{}";
            }
            return obj["result"].ToString();
        }
    }
}
