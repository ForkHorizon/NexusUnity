using System;
using System.IO;
using System.Threading;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Contains methods for processing and executing MCP JSON-RPC requests.
    /// Uses a high-performance Dictionary for method dispatching.
    /// </summary>
    public static partial class MCPServerMethods
    {
        private static int _mainThreadId;

        private static bool _isMainThread => Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        private static readonly Dictionary<string, Func<JToken, JToken>> _methods = new Dictionary<string, Func<JToken, JToken>>();

        internal static void Init()
        {
            if (_methods.Count > 0) return;
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            RegisterCoreMethods();
            RegisterSceneMethods();
            RegisterDiscoveryMethods();
            RegisterEditorMethods();
            RegisterAssetMethods();
            RegisterHierarchyMethods();
            RegisterComponentMethods();
            RegisterSerializationMethods();
            RegisterUIMethods();
            RegisterHighValueMethods();
        }

        /// <summary>
        /// Processes a JSON-RPC request string and returns the response string.
        /// </summary>
        public static string ProcessJsonRpc(string json)
        {
            try
            {
                JObject request = JObject.Parse(json);
                return ProcessJsonRequest(request);
            }
            catch (Exception e)
            {
                return CreateErrorResponse(null, -32700, $"Parse error: {e.Message}");
            }
        }



        /// <summary>
        /// Processes a JSON-RPC request from a TextReader and returns the response string.
        /// Optimized to reduce memory allocations for large payloads.
        /// </summary>
        public static string ProcessJsonRpc(TextReader reader)
        {
            try
            {
                using (var jsonReader = new JsonTextReader(reader))
                {
                    jsonReader.CloseInput = false;
                    JObject request = JObject.Load(jsonReader);
                    return ProcessJsonRequest(request);
                }
            }
            catch (Exception e) { return CreateErrorResponse(null, -32700, $"Parse error (Reader): {e.Message}"); }
        }

        private static string ProcessJsonRequest(JObject request)
        {
            JToken id = request["id"];
            if (request["method"] == null) return CreateErrorResponse(id, -32600, "Method missing");
            return ExecuteOnMainThread(request["method"].ToString(), request["params"], id);
        }

        private static string ExecuteOnMainThread(string method, JToken requestParams, JToken id)
        {
            int currentThreadId = Thread.CurrentThread.ManagedThreadId;
            // Deadlock prevention: If we're already on the main thread, execute immediately.
            // This happens when calling ProcessJsonRpc from an Editor window or menu item.
            if (currentThreadId == _mainThreadId)
            {
                try
                {
                    JToken syncResult = ExecuteMethod(method, requestParams);
                    return CreateJsonResponse(id, syncResult, null);
                }
                catch (Exception e)
                {
                    return CreateJsonResponse(id, null, e.Message);
                }
            }

            JToken result = null;
            string error = null;
            using (var signal = new ManualResetEventSlim(false))
            {
                MCPServer.Enqueue(() => {
                    try { 
                        result = ExecuteMethod(method, requestParams); 
                    }
                    catch (Exception e) { error = e.Message; }
                    finally { signal.Set(); }
                });
                if (!signal.Wait(60000)) error = "Timeout waiting for Main Thread";
            }
            return CreateJsonResponse(id, result, error);
        }

        private static string CreateJsonResponse(JToken id, JToken result, string error)
        {
            JObject response = new JObject { ["jsonrpc"] = "2.0", ["id"] = id };
            if (error != null) response["error"] = new JObject { ["code"] = -32000, ["message"] = error };
            else response["result"] = result;
            return response.ToString(Formatting.None);
        }

        /// <summary>
        /// Creates a JSON-RPC error response string.
        /// </summary>
        public static string CreateErrorResponse(JToken id, int code, string message)
        {
            return new JObject { ["jsonrpc"] = "2.0", ["error"] = new JObject { ["code"] = code, ["message"] = message }, ["id"] = id }.ToString(Formatting.None);
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
