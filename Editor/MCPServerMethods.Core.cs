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
    /// Partial implementation of MCPServerMethods handling Core system and tool listing.
    /// </summary>
    public static partial class MCPServerMethods
    {
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

            var results = new JArray();
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
            return new JObject { ["results"] = results };
        }

                private static JToken CreatePrimitive(JToken p)
        {
            if (!Enum.TryParse(typeof(PrimitiveType), p["primitive_type"].ToString(), true, out var type)) throw new Exception("Invalid primitive");
            var go = GameObject.CreatePrimitive((PrimitiveType)type);
            Undo.RegisterCreatedObjectUndo(go, "Create Primitive");
            Selection.activeGameObject = go;
            return new JObject { ["status"] = "Success", ["data"] = SerializeGameObject(go) };
        }

        private static JToken AttachScript(JToken p)
        {
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
            UnityEngine.Debug.Log("[MCP_EXECUTE] test_coroutine");
            EditorApplication.delayCall += () => Debug.Log("[MCP] Delay call complete"); 
            return new JObject { ["status"] = "Success", ["message"] = "Started" }; 
        }

        // Cache the tool definitions since they are static and do not change during the session.
    }
}
