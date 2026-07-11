using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Registers core Nexus Unity JSON-RPC methods for initialization, tool listing, log access, server status, batching, and primitive creation.
    /// </summary>
    /// <remarks>
    /// Core methods can query or reset server usage state, clear the Nexus log queue, create Unity primitives with Undo support,
    /// attach scripts, shut down the local server, and dispatch nested batch requests through the same editor method table.
    /// </remarks>
    public static partial class MCPServerMethods
    {
        private const int MaxBatchExecuteRequests = 50;
        private static int _batchExecuteDepth;

        private static void RegisterCoreMethods()
        {
            _methods["initialize"] = Initialize;
            _methods["read_logs"] = ReadLogs;
            _methods["read_logs_since_cursor"] = ReadLogsSinceCursor;
            _methods["clear_logs"] = ClearLogs;
            _methods["list_tools"] = ListTools;
            _methods["wait_for_ready"] = WaitForReady;
            _methods["create_primitive"] = CreatePrimitive;
            _methods["attach_script"] = AttachScript;
            _methods["get_server_status"] = GetServerStatus;
            _methods["get_tool_usage_stats"] = GetToolUsageStats;
            _methods["reset_tool_usage_stats"] = ResetToolUsageStats;
            _methods["attach_existing_session"] = AttachExistingSession;
            _methods["ping_main_thread"] = PingMainThread;
            _methods["shutdown_server"] = ShutdownServer;
            _methods["batch_execute"] = BatchExecute;
        }

        private static JToken BatchExecute(JToken p)
        {
            if (p == null || p["requests"] == null) throw new Exception("requests array is required");
            var requests = p["requests"] as JArray;
            if (requests == null) throw new Exception("requests must be a JSON array");
            if (requests.Count > MaxBatchExecuteRequests) throw new Exception($"batch_execute supports at most {MaxBatchExecuteRequests} requests");
            if (_batchExecuteDepth > 0) throw new Exception("Recursive batch_execute is not allowed");

            var results = new JArray();
            _batchExecuteDepth++;
            try
            {
                foreach (var req in requests)
                {
                    string method = req["method"]?.ToString();
                    if (method == "batch_execute")
                    {
                        results.Add(new JObject { ["status"] = "Error", ["message"] = "Recursive batch_execute is not allowed" });
                        continue;
                    }

                    JToken par = req["params"];
                    try { results.Add(ExecuteMethod(method, par)); }
                    catch (Exception e) { results.Add(new JObject { ["status"] = "Error", ["message"] = e.Message }); }
                }
            }
            finally
            {
                _batchExecuteDepth--;
            }
            return new JObject { ["results"] = results };
        }

        private static JToken CreatePrimitive(JToken p)
        {
            if (p == null || p["primitive_type"] == null) throw new Exception("primitive_type is required");
            if (!Enum.TryParse(typeof(PrimitiveType), p["primitive_type"].ToString(), true, out var type)) throw new Exception("Invalid primitive");

            string name = p["name"]?.ToString();
            Transform parentTransform = null;
            if (p["parent_id"] != null)
            {
                var parent = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p, "parent_id")) as GameObject;
                if (parent == null) throw new Exception("parent_id does not resolve to a GameObject");
                parentTransform = parent.transform;
            }

            Vector3? position = p["position"] != null ? ParseVector3(p["position"]) : null;
            JToken rotationToken = p["rotation"] ?? p["eulerAngles"];
            Vector3? rotation = rotationToken != null ? ParseVector3(rotationToken) : null;
            JToken scaleToken = p["scale"] ?? p["localScale"];
            Vector3? scale = scaleToken != null ? ParseVector3(scaleToken) : null;

            Material material = null;
            string materialPath = p["material_path"]?.ToString();
            if (!string.IsNullOrWhiteSpace(materialPath))
            {
                materialPath = ValidateAssetPath(materialPath);
                material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null) throw new Exception($"Material not found at {materialPath}");
            }

            GameObject go = null;
            try
            {
                go = GameObject.CreatePrimitive((PrimitiveType)type);
                Undo.RegisterCreatedObjectUndo(go, "Create Primitive");

                if (!string.IsNullOrWhiteSpace(name)) go.name = name;

                if (parentTransform != null) go.transform.SetParent(parentTransform, false);

                if (position.HasValue) go.transform.position = position.Value;
                if (rotation.HasValue) go.transform.eulerAngles = rotation.Value;
                if (scale.HasValue) go.transform.localScale = scale.Value;

                var renderer = go.GetComponent<Renderer>();
                if (renderer != null && material != null) renderer.sharedMaterial = material;

                Selection.activeGameObject = go;
                return new JObject { ["status"] = "Success", ["data"] = SerializeGameObject(go) };
            }
            catch
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
                throw;
            }
        }

        private static JToken AttachScript(JToken p)
        {
            RequireScriptWriteConfirmation(p);
            string name = SanitizeScriptName(p["script_name"].ToString());
            string content = p["script_content"]?.ToString().Replace("\n", "\n") ?? GetDefaultScript(name);
            File.WriteAllText(Path.Combine("Assets", $"{name}.cs"), content);
            if (Selection.activeGameObject != null)
            {
                SessionState.SetString("MCP_PendingAttach_Script", name);
                SessionState.SetInt("MCP_PendingAttach_GO", Selection.activeGameObject.GetRawId());
            }
            AssetDatabase.Refresh();
            return new JObject { ["status"] = "Success", ["message"] = "Script created and compilation triggered" };
        }

        private static JToken ReadLogs(JToken p) {
            var logs = MCPServer.GetLogs((int)(p?["count"] ?? 50), p?["filter_type"]?.ToString(), p?["search_text"]?.ToString());
            bool structured = p?["structured"]?.Value<bool>() ?? false;

            if (structured)
            {
                logs = CollapseLogs(logs);
            }

            return new JObject { ["logs"] = JArray.FromObject(logs) };
        }

        private static JToken ReadLogsSinceCursor(JToken p)
        {
            long cursor = p?["cursor"]?.Value<long>() ?? 0;
            string[] severities = p?["severities"]?.ToObject<string[]>();
            string searchText = p?["search_text"]?.ToString();
            bool structured = p?["structured"]?.Value<bool>() ?? false;

            var logs = MCPServer.GetLogsSince(cursor, severities, searchText);
            
            if (structured)
            {
                logs = CollapseLogs(logs);
            }

            long newCursor = logs.Count > 0 ? logs.Max(l => l.Id) : cursor;

            return new JObject
            {
                ["logs"] = JArray.FromObject(logs),
                ["new_cursor"] = newCursor
            };
        }

        private static JToken ClearLogs(JToken p) { MCPServer.ClearLogs(); return new JObject { ["status"] = "Success", ["message"] = "Logs cleared" }; }
        private static JToken TestCoroutine(JToken p) { 
            NexusEditorLog.Log(NexusLogCategory.Diagnostics, "[MCP_EXECUTE] test_coroutine");
            EditorApplication.delayCall += () => NexusEditorLog.Log(NexusLogCategory.Diagnostics, "[MCP] Delay call complete");
            return new JObject { ["status"] = "Success", ["message"] = "Started" }; 
        }

        // Cache the tool definitions since they are static and do not change during the session.
    }
}
