using System;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Contains methods for processing and executing MCP JSON-RPC requests.
    /// </summary>
    public static class MCPServerMethods
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

                if (request["method"] == null)
                {
                    return CreateErrorResponse(id, -32600, "Invalid Request: method missing");
                }

                string method = request["method"].ToString();
                JToken requestParams = request["params"];
                return ExecuteOnMainThread(method, requestParams, id);
            }
            catch
            {
                return CreateErrorResponse(null, -32700, "Parse error");
            }
        }

        private static string ExecuteOnMainThread(string method, JToken requestParams, JToken id)
        {
            JToken result = null;
            string error = null;

            using (var signal = new ManualResetEventSlim(false))
            {
                MCPServerWindow.Enqueue(() => {
                    try
                    {
                        result = ExecuteMethod(method, requestParams);
                    }
                    catch (Exception e)
                    {
                        error = e.Message;
                    }
                    finally
                    {
                        signal.Set();
                    }
                });

                if (!signal.Wait(10000)) error = "Request timed out waiting for Main Thread";
            }

            return CreateJsonResponse(id, result, error);
        }

        private static string CreateJsonResponse(JToken id, JToken result, string error)
        {
            JObject response = new JObject { ["jsonrpc"] = "2.0", ["id"] = id };
            if (error != null)
                response["error"] = new JObject { ["code"] = -32000, ["message"] = error };
            else
                response["result"] = result;
            return response.ToString();
        }

        /// <summary>
        /// Creates a JSON-RPC error response string.
        /// </summary>
        public static string CreateErrorResponse(JToken id, int code, string message)
        {
            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["error"] = new JObject { ["code"] = code, ["message"] = message },
                ["id"] = id
            }.ToString();
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
                default: throw new Exception($"Method not found: {method}");
            }
        }

        private static JToken ReadLogs(JToken p)
        {
            int count = 50;
            string filterType = null;
            string searchText = null;

            if (p != null)
            {
                if (p["count"] != null) count = (int)p["count"];
                if (p["filter_type"] != null) filterType = p["filter_type"].ToString();
                if (p["search_text"] != null) searchText = p["search_text"].ToString();
            }

            var logs = MCPServerWindow.GetLogs(count, filterType, searchText);
            return JArray.FromObject(logs);
        }

        private static JToken ClearLogs(JToken p)
        {
            MCPServerWindow.ClearLogs();
            return "Logs cleared";
        }

        private static JToken Initialize(JToken p)
        {
            return new JObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new JObject(),
                ["serverInfo"] = new JObject { ["name"] = "Unity MCP Server", ["version"] = "0.0.1" }
            };
        }

        private static JToken CreatePrimitive(JToken p)
        {
            if (p == null || p["primitive_type"] == null) throw new Exception("primitive_type is required");
            string typeStr = p["primitive_type"].ToString();
            if (!Enum.TryParse(typeof(PrimitiveType), typeStr, true, out var type))
                throw new Exception($"Invalid primitive type: {typeStr}");

            var go = GameObject.CreatePrimitive((PrimitiveType)type);
            go.name = "MCP_" + typeStr;
            Selection.activeGameObject = go;
            return $"Created {go.name}";
        }

        private static JToken AttachScript(JToken p)
        {
            if (p == null || p["script_name"] == null) throw new Exception("script_name is required");
            string scriptName = SanitizeScriptName(p["script_name"].ToString());
            string content = (p["script_content"] != null) ? p["script_content"].ToString() :
                $"using UnityEngine;\npublic class {scriptName} : MonoBehaviour {{ void Start() {{ Debug.Log(\"Hello from {scriptName}\"); }} }}";

            string path = Path.Combine("Assets", $"{scriptName}.cs");
            File.WriteAllText(path, content);

            if (Selection.activeGameObject == null)
            {
                AssetDatabase.Refresh();
                return $"Script {scriptName} created at {path}. No target GameObject selected.";
            }

            SessionState.SetString("MCP_PendingAttach_Script", scriptName);
            SessionState.SetInt("MCP_PendingAttach_GO", Selection.activeGameObject.GetInstanceID());

            AssetDatabase.Refresh();
            return $"Script {scriptName} created at {path}. Compilation triggered.";
        }

        private static string SanitizeScriptName(string name)
        {
            string sanitized = System.Text.RegularExpressions.Regex.Replace(name, @"[^a-zA-Z0-9_]", "_");
            if (char.IsDigit(sanitized[0])) sanitized = "_" + sanitized;
            return sanitized;
        }

        /// <summary>
        /// Checks for scripts that were pending attachment before an assembly reload.
        /// </summary>
        public static void CheckPendingAttachments()
        {
            string pendingScript = SessionState.GetString("MCP_PendingAttach_Script", "");
            int pendingGoId = SessionState.GetInt("MCP_PendingAttach_GO", 0);

            if (string.IsNullOrEmpty(pendingScript) || pendingGoId == 0) return;

            SessionState.EraseString("MCP_PendingAttach_Script");
            SessionState.EraseInt("MCP_PendingAttach_GO");

            var go = EditorUtility.InstanceIDToObject(pendingGoId) as GameObject;
            if (go == null) return;

            Type type = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(pendingScript);
                if (type != null) break;
            }

            if (type != null)
            {
                go.AddComponent(type);
                Debug.Log($"[MCP] Successfully attached {pendingScript} to {go.name}");
            }
            else
            {
                Debug.LogError($"[MCP] Could not find type {pendingScript} to attach.");
            }
        }
    }
}
