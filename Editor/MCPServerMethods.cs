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
            if (method.StartsWith("ui_")) return ExecuteUIMethod(method, p);
            if (method.StartsWith("get_") || method == "find_objects" || method == "list_scenes" || method == "ping_object") return ExecuteDiscoveryMethod(method, p);
            if (method.StartsWith("set_") || method == "undo" || method == "redo" || method == "toggle_play_mode" || method == "execute_menu_item" || method == "focus_scene_view") return ExecuteEditorMethod(method, p);
            
            switch (method)
            {
                case "initialize": return Initialize(p);
                case "create_primitive": return CreatePrimitive(p);
                case "attach_script": return AttachScript(p);
                case "read_logs": return ReadLogs(p);
                case "clear_logs": return ClearLogs(p);
                case "list_assets": return ListAssets(p);
                case "create_material": return CreateMaterial(p);
                case "refresh_asset_database": return RefreshAssetDatabase(p);
                case "import_asset": return ImportAsset(p);
                case "open_scene": return OpenScene(p);
                case "create_scene": return CreateScene(p);
                case "save_scene": return SaveScene(p);
                case "create_game_object": return CreateGameObject(p);
                case "destroy_game_object": return DestroyGameObject(p);
                case "instantiate_prefab": return InstantiatePrefab(p);
                case "create_prefab": return CreatePrefab(p);
                case "apply_prefab_overrides": return ApplyPrefabOverrides(p);
                case "revert_prefab_overrides": return RevertPrefabOverrides(p);
                case "test_coroutine": return TestCoroutine(p);
                default: throw new Exception($"Method not found: {method}");
            }
        }

        private static JToken ExecuteUIMethod(string method, JToken p)
        {
            switch (method)
            {
                case "ui_list_windows": return UIListWindows(p);
                case "ui_get_hierarchy": return UIGetHierarchy(p);
                case "ui_click": return UIClick(p);
                case "ui_input_text": return UIInputText(p);
                default: throw new Exception($"UI Method not found: {method}");
            }
        }

        private static JToken ExecuteDiscoveryMethod(string method, JToken p)
        {
            switch (method)
            {
                case "get_game_object": return GetGameObject(p);
                case "get_active_game_object": return GetActiveGameObject(p);
                case "get_root_game_objects": return GetRootGameObjects(p);
                case "get_object_path": return GetObjectPath(p);
                case "find_objects": return FindObjects(p);
                case "list_scenes": return ListScenes(p);
                case "get_tags_and_layers": return GetTagsAndLayers(p);
                case "ping_object": return PingObject(p);
                default: throw new Exception($"Discovery Method not found: {method}");
            }
        }

        private static JToken ExecuteEditorMethod(string method, JToken p)
        {
            switch (method)
            {
                case "set_transform": return SetTransform(p);
                case "set_parent": return SetParent(p);
                case "add_component": return AddComponent(p);
                case "inspect_component": return InspectComponent(p);
                case "update_component": return UpdateComponent(p);
                case "set_property": return SetProperty(p);
                case "undo": return UndoMethod(p);
                case "redo": return RedoMethod(p);
                case "toggle_play_mode": return TogglePlayMode(p);
                case "execute_menu_item": return ExecuteMenuItem(p);
                case "set_selection": return SetSelection(p);
                case "focus_scene_view": return FocusSceneView(p);
                default: throw new Exception($"Editor Method not found: {method}");
            }
        }
    }
}
