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
    /// Owns Nexus Unity JSON-RPC method registration and dispatch for Unity editor automation.
    /// </summary>
    /// <remarks>
    /// Initialization must occur on the Unity Editor main thread before registering scene, asset, component, UI, input,
    /// logging, and diagnostic methods. Dispatched requests can read or mutate editor state, access validated project files,
    /// run editor APIs, and return JSON-RPC error responses when a method fails.
    /// </remarks>
    public static partial class MCPServerMethods
    {
        private static int _mainThreadId = -1;

        private static bool _isMainThread => Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        private static readonly Dictionary<string, Func<JToken, JToken>> _methods = new Dictionary<string, Func<JToken, JToken>>();

        internal static void Init()
        {
            if (_mainThreadId == -1)
            {
                _mainThreadId = Thread.CurrentThread.ManagedThreadId;
                NexusEditorLog.Log(NexusLogCategory.Api, $"[MCP] Main Thread ID captured: {_mainThreadId}");
            }

            if (!_isMainThread)
            {
                NexusEditorLog.Warning(NexusLogCategory.Api, "[MCP] MCPServerMethods.Init called from non-main thread. Registration skipped to avoid state corruption.");
                return;
            }

            MCPServer.RefreshMainThreadCachedState();
            CacheEnvironmentPaths();
            NexusEditorLog.Log(NexusLogCategory.Api, "[MCP] MCPServerMethods.Init starting...");
            _methods.Clear();
            ClearCache();
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
            RegisterPlayerPrefsMethods();
            RegisterScriptableObjectMethods();
            RegisterReflectionMethods();
            RegisterSyncMethods();
            RegisterInputMethods();
            RegisterSnapshotMethods();
            RegisterTimelineMethods();
            RegisterContextMethods();
            RegisterDeltaMethods();
            NexusEditorLog.Log(NexusLogCategory.Api, $"[MCP] MCPServerMethods.Init completed. Registered {_methods.Count} methods.");        }

        /// <summary>
        /// Parses and processes a JSON-RPC request string for the local Nexus Unity server.
        /// </summary>
        /// <remarks>
        /// Request execution may call editor automation methods with side effects such as scene changes, asset I/O,
        /// selection updates, UI manipulation, or server control. Invalid JSON is caught and converted into a JSON-RPC parse error.
        /// </remarks>
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
        /// Processes a JSON-RPC request from a <see cref="TextReader"/> without taking ownership of the reader.
        /// </summary>
        /// <remarks>
        /// The underlying <see cref="JsonTextReader"/> uses <c>CloseInput = false</c>, so callers remain responsible for
        /// stream lifetime. Parse and execution exceptions are returned as JSON-RPC error response strings.
        /// </remarks>
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
            string method = request["method"]?.ToString();
            if (method == null) return CreateErrorResponse(id, -32600, "Method missing");

            // Fast-path health checks run on the listener/current thread.
            // Handlers here must only read cached or thread-safe process state.
            if (method == "get_server_status" || method == "attach_existing_session" || method == "wait_for_asset_import_idle" || method == "wait_for_editor_idle") {
                try {
                    JToken result = ExecuteMethod(method, request["params"], false);
                    return CreateJsonResponse(id, result);
                } catch(Exception e) { return CreateExceptionResponse(id, e); }
            }

            return ExecuteOnMainThread(method, request["params"], id);
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
                    return CreateJsonResponse(id, syncResult);
                }
                catch (ArgumentException e)
                {
                    return CreateErrorResponse(id, -32602, e.Message);
                }
                catch (Exception e)
                {
                    return CreateExceptionResponse(id, e);
                }
            }

            JToken result = null;
            Exception caughtException = null;
            bool timeout = false;
            using (var signal = new ManualResetEventSlim(false))
            {
                MCPServer.Enqueue(() => {
                    try {
                        result = ExecuteMethod(method, requestParams);
                    }
                    catch (Exception e) { caughtException = e; }
                    finally { signal.Set(); }
                });
                if (!signal.Wait(60000)) timeout = true;
            }

            if (timeout) return CreateErrorResponse(id, -32000, "Timeout waiting for Main Thread");
            if (caughtException != null) return CreateExceptionResponse(id, caughtException);
            return CreateJsonResponse(id, result);
        }

        private static string CreateJsonResponse(JToken id, JToken result)
        {
            JObject response = new JObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result };
            return response.ToString(Formatting.None);
        }

        /// <summary>
        /// Creates a compact JSON-RPC error response string for Nexus Unity server failures.
        /// </summary>
        /// <remarks>
        /// When a stack trace is provided, it is placed in the error data payload for editor/network diagnostics.
        /// The response is serialized without indentation because it is sent over the local JSON-RPC protocol.
        /// </remarks>
        public static string CreateErrorResponse(JToken id, int code, string message, string stackTrace = null)
        {
            JObject errorObj = new JObject { ["code"] = code, ["message"] = message };
            if (!string.IsNullOrEmpty(stackTrace))
            {
                errorObj["data"] = new JObject { ["stackTrace"] = stackTrace };
            }
            return new JObject { ["jsonrpc"] = "2.0", ["error"] = errorObj, ["id"] = id }.ToString(Formatting.None);
        }

        /// <summary>
        /// Unwraps common wrapper exceptions and creates a JSON-RPC error response with a highly readable stack trace.
        /// </summary>
        private static string CreateExceptionResponse(JToken id, Exception e)
        {
            Exception actualException = e;
            while (actualException is System.Reflection.TargetInvocationException || actualException is AggregateException)
            {
                if (actualException is System.Reflection.TargetInvocationException tie && tie.InnerException != null)
                {
                    actualException = tie.InnerException;
                }
                else if (actualException is AggregateException ae && ae.InnerExceptions.Count == 1)
                {
                    actualException = ae.InnerExceptions[0];
                }
                else
                {
                    break;
                }
            }

            string stackTrace = actualException.StackTrace;
            return CreateErrorResponse(id, -32000, actualException.Message, stackTrace);
        }

        private static JToken ExecuteMethod(string method, JToken p, bool logExecution = true)
        {
            if (logExecution) NexusEditorLog.Log(NexusLogCategory.Api, $"[MCP_EXECUTE] {method}");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            Exception failure = null;
            try
            {
                if (_methods.TryGetValue(method, out var func))
                {
                    return func(p);
                }

                throw new Exception($"Method not found: {method}");
            }
            catch (Exception e)
            {
                failure = e;
                throw;
            }
            finally
            {
                stopwatch.Stop();
                if (method != "reset_tool_usage_stats")
                    RecordToolUsage(method, stopwatch.Elapsed.TotalMilliseconds, failure);
            }
        }

    }
}
