using System;
using System.IO;
using System.Threading;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Contains methods for processing and executing MCP JSON-RPC requests.
    /// </summary>
    public static partial class MCPServerMethods
    {
        /// <summary>
        /// Processes a JSON-RPC request string and returns the response string.
        /// </summary>
        public static string ProcessJsonRpc(string json)
        {
            try
            {
                JObject request = JObject.Parse(json);
                JToken id = request["id"];
                if (request["method"] == null) return CreateErrorResponse(id, -32600, "Method missing");
                return ExecuteOnMainThread(request["method"].ToString(), request["params"], id);
            }
            catch { return CreateErrorResponse(null, -32700, "Parse error"); }
        }

        private static string ExecuteOnMainThread(string method, JToken requestParams, JToken id)
        {
            JToken result = null;
            string error = null;
            using (var signal = new ManualResetEventSlim(false))
            {
                MCPServerWindow.Enqueue(() => {
                    try { result = ExecuteMethod(method, requestParams); }
                    catch (Exception e) { error = e.Message; }
                    finally { signal.Set(); }
                });
                if (!signal.Wait(10000)) error = "Timeout waiting for Main Thread";
            }
            return CreateJsonResponse(id, result, error);
        }

        private static string CreateJsonResponse(JToken id, JToken result, string error)
        {
            JObject response = new JObject { ["jsonrpc"] = "2.0", ["id"] = id };
            if (error != null) response["error"] = new JObject { ["code"] = -32000, ["message"] = error };
            else response["result"] = result;
            return response.ToString();
        }

        /// <summary>
        /// Creates a JSON-RPC error response string.
        /// </summary>
        public static string CreateErrorResponse(JToken id, int code, string message)
        {
            return new JObject { ["jsonrpc"] = "2.0", ["error"] = new JObject { ["code"] = code, ["message"] = message }, ["id"] = id }.ToString();
        }

        private static JToken ExecuteMethod(string method, JToken p)
        {
            switch (method)
            {
                case "initialize": return Initialize(p);
                case "create_primitive": return CreatePrimitive(p);
                case "attach_script": return AttachScript(p);
                case "read_logs": return ReadLogs(p);
                case "clear_logs": return ClearLogs(p);
                case "ui_list_windows": return UIListWindows(p);
                case "ui_get_hierarchy": return UIGetHierarchy(p);
                case "ui_click": return UIClick(p);
                case "ui_input_text": return UIInputText(p);
                case "list_assets": return ListAssets(p);
                case "create_material": return CreateMaterial(p);
                case "refresh_asset_database": return RefreshAssetDatabase(p);
                case "import_asset": return ImportAsset(p);
                case "open_scene": return OpenScene(p);
                case "create_scene": return CreateScene(p);
                case "save_scene": return SaveScene(p);
                case "get_game_object": return GetGameObject(p);
                case "create_game_object": return CreateGameObject(p);
                case "destroy_game_object": return DestroyGameObject(p);
                case "set_transform": return SetTransform(p);
                case "set_parent": return SetParent(p);
                case "add_component": return AddComponent(p);
                case "inspect_component": return InspectComponent(p);
                case "update_component": return UpdateComponent(p);
                case "instantiate_prefab": return InstantiatePrefab(p);
                case "get_root_game_objects": return GetRootGameObjects(p);
                case "get_active_game_object": return GetActiveGameObject(p);
                case "test_coroutine": return TestCoroutine(p);
                default: throw new Exception($"Method not found: {method}");
            }
        }
    }
}
