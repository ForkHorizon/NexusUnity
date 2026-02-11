using System;
using System.IO;
using System.Threading;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Contains methods for processing and executing MCP JSON-RPC requests.
    /// Uses a high-performance Dictionary for method dispatching.
    /// </summary>
    public static partial class MCPServerMethods
    {
        private static readonly Dictionary<string, Func<JToken, JToken>> _methods = new Dictionary<string, Func<JToken, JToken>>();

        static MCPServerMethods()
        {
            RegisterCoreMethods();
            RegisterSceneMethods();
            RegisterDiscoveryMethods();
            RegisterEditorMethods();
            RegisterAssetMethods();
            RegisterHierarchyMethods();
            RegisterUIMethods();
        }

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

        /// <summary>
        /// Processes a JSON-RPC request from a TextReader to minimize allocations.
        /// </summary>
        public static string ProcessJsonRpc(TextReader reader)
        {
            try
            {
                using (var jsonReader = new JsonTextReader(reader))
                {
                    jsonReader.CloseInput = false;
                    JObject request = JObject.Load(jsonReader);
                    JToken id = request["id"];
                    if (request["method"] == null) return CreateErrorResponse(id, -32600, "Method missing");
                    return ExecuteOnMainThread(request["method"].ToString(), request["params"], id);
                }
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
            if (_methods.TryGetValue(method, out var func))
            {
                return func(p);
            }

            throw new Exception($"Method not found: {method}");
        }
    }
}
