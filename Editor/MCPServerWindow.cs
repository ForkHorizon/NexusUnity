using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    public class MCPServerWindow : EditorWindow
    {
        private HttpListener _httpListener;
        private Thread _serverThread;
        private volatile bool _isRunning = false;
        private const int Port = 8080;
        private static readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

        [MenuItem("Tools/MCP Server")]
        public static void ShowWindow()
        {
            GetWindow<MCPServerWindow>("MCP Server");
        }

        private void OnEnable()
        {
            EditorApplication.update += HandleMainThreadQueue;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;

            if (SessionState.GetBool("MCP_Server_Running", false))
            {
                StartServer();
            }
        }

        private void OnDisable()
        {
            EditorApplication.update -= HandleMainThreadQueue;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            StopServer();
        }

        private void OnBeforeAssemblyReload()
        {
            if (_isRunning)
            {
                SessionState.SetBool("MCP_Server_Running", true);
                StopServer();
            }
            else
            {
                SessionState.SetBool("MCP_Server_Running", false);
            }
        }

        private void OnAfterAssemblyReload()
        {
            if (SessionState.GetBool("MCP_Server_Running", false))
            {
                StartServer();
            }

            CheckPendingAttachments();
        }

        private void CheckPendingAttachments()
        {
            string pendingScript = SessionState.GetString("MCP_PendingAttach_Script", "");
            int pendingGoId = SessionState.GetInt("MCP_PendingAttach_GO", 0);

            if (!string.IsNullOrEmpty(pendingScript) && pendingGoId != 0)
            {
                SessionState.EraseString("MCP_PendingAttach_Script");
                SessionState.EraseInt("MCP_PendingAttach_GO");

                var go = EditorUtility.InstanceIDToObject(pendingGoId) as GameObject;
                if (go != null)
                {
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

        private void OnGUI()
        {
            GUILayout.Label("Unity MCP Server", EditorStyles.boldLabel);
            GUILayout.Label($"Status: {(_isRunning ? "Running" : "Stopped")}");

            if (!_isRunning)
            {
                if (GUILayout.Button("Start Server"))
                {
                    StartServer();
                    SessionState.SetBool("MCP_Server_Running", true);
                }
            }
            else
            {
                if (GUILayout.Button("Stop Server"))
                {
                    StopServer();
                    SessionState.SetBool("MCP_Server_Running", false);
                }
            }
        }

        private void StartServer()
        {
            if (_isRunning) return;

            try
            {
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add($"http://localhost:{Port}/");
                _httpListener.Start();

                _isRunning = true;
                _serverThread = new Thread(ServerLoop);
                _serverThread.Start();

                Debug.Log($"MCP Server started on port {Port}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to start MCP server: {e.Message}");
                StopServer();
            }
        }

        private void StopServer()
        {
            _isRunning = false;

            if (_httpListener != null)
            {
                try
                {
                    _httpListener.Stop();
                    _httpListener.Close();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Error stopping HttpListener: {e.Message}");
                }
                finally
                {
                    _httpListener = null;
                }
            }

            Debug.Log("MCP Server stopped");
        }

        private void ServerLoop()
        {
            while (_isRunning && _httpListener != null && _httpListener.IsListening)
            {
                try
                {
                    var context = _httpListener.GetContext();
                    ThreadPool.QueueUserWorkItem((_) => HandleRequest(context));
                }
                catch (HttpListenerException) { } // Expected on stop
                catch (ObjectDisposedException) { } // Expected on stop
                catch (Exception e)
                {
                    Debug.LogError($"Server loop error: {e.Message}");
                }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            string responseString = "";
            int statusCode = 200;

            try
            {
                string requestBody;
                using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                {
                    requestBody = reader.ReadToEnd();
                }

                responseString = ProcessJsonRpc(requestBody);
            }
            catch (Exception e)
            {
                statusCode = 500;
                var errObj = new JObject
                {
                    ["jsonrpc"] = "2.0",
                    ["error"] = new JObject { ["code"] = -32603, ["message"] = e.Message },
                    ["id"] = null
                };
                responseString = errObj.ToString();
                Debug.LogError($"Error handling request: {e.Message}");
            }

            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error sending response: {e.Message}");
            }
        }

        private string ProcessJsonRpc(string json)
        {
            JObject request;
            try
            {
                request = JObject.Parse(json);
            }
            catch
            {
                return new JObject
                {
                    ["jsonrpc"] = "2.0",
                    ["error"] = new JObject { ["code"] = -32700, ["message"] = "Parse error" },
                    ["id"] = null
                }.ToString();
            }

            JToken id = request["id"]; // Can be null for notifications
            if (request["method"] == null)
            {
                return new JObject
                {
                    ["jsonrpc"] = "2.0",
                    ["error"] = new JObject { ["code"] = -32600, ["message"] = "Invalid Request: method missing" },
                    ["id"] = id
                }.ToString();
            }

            string method = request["method"].ToString();
            JToken requestParams = request["params"];

            JToken result = null;
            string error = null;

            // Use ManualResetEventSlim for lighter weight
            using (var signal = new ManualResetEventSlim(false))
            {
                Enqueue(() => {
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

                // Wait with timeout (e.g. 10 seconds) to prevent permanent hang
                if (!signal.Wait(10000))
                {
                    error = "Request timed out waiting for Main Thread";
                }
            }

            JObject response = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id
            };

            if (error != null)
            {
                response["error"] = new JObject { ["code"] = -32000, ["message"] = error };
            }
            else
            {
                response["result"] = result;
            }

            return response.ToString();
        }

        private JToken ExecuteMethod(string method, JToken p)
        {
            switch (method)
            {
                case "initialize":
                    return Initialize(p);
                case "create_primitive":
                    return CreatePrimitive(p);
                case "attach_script":
                    return AttachScript(p);
                default:
                    throw new Exception($"Method not found: {method}");
            }
        }

        private JToken Initialize(JToken p)
        {
             // p can be null or empty, that's fine for initialize
             return new JObject
             {
                 ["protocolVersion"] = "2024-11-05",
                 ["capabilities"] = new JObject(),
                 ["serverInfo"] = new JObject
                 {
                     ["name"] = "Unity MCP Server",
                     ["version"] = "0.0.1"
                 }
             };
        }

        private JToken CreatePrimitive(JToken p)
        {
            if (p == null || p["primitive_type"] == null) throw new Exception("primitive_type is required");

            string typeStr = p["primitive_type"].ToString();
            PrimitiveType type;
            try
            {
                type = (PrimitiveType)Enum.Parse(typeof(PrimitiveType), typeStr, true);
            }
            catch
            {
                throw new Exception($"Invalid primitive type: {typeStr}");
            }

            var go = GameObject.CreatePrimitive(type);
            go.name = "MCP_" + typeStr;

            Selection.activeGameObject = go;

            return $"Created {go.name}";
        }

        private JToken AttachScript(JToken p)
        {
            if (p == null || p["script_name"] == null) throw new Exception("script_name is required");

            string scriptName = p["script_name"].ToString();
            // Basic sanitization
            scriptName = System.Text.RegularExpressions.Regex.Replace(scriptName, @"[^a-zA-Z0-9_]", "_");

            if (char.IsDigit(scriptName[0])) scriptName = "_" + scriptName;

            string content = (p["script_content"] != null) ? p["script_content"].ToString() :
                $"using UnityEngine;\npublic class {scriptName} : MonoBehaviour {{ void Start() {{ Debug.Log(\"Hello from {scriptName}\"); }} }}";

            string fileName = $"{scriptName}.cs";
            string path = Path.Combine("Assets", fileName);

            File.WriteAllText(path, content);

            if (Selection.activeGameObject != null)
            {
                SessionState.SetString("MCP_PendingAttach_Script", scriptName);
                SessionState.SetInt("MCP_PendingAttach_GO", Selection.activeGameObject.GetInstanceID());
            }

            AssetDatabase.Refresh();

            return $"Script {scriptName} created at {path}. Compilation triggered. It will be attached to {Selection.activeGameObject?.name} automatically after reload.";
        }

        private void HandleMainThreadQueue()
        {
            // Process all pending actions
            while (_mainThreadQueue.TryDequeue(out var action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error executing on main thread: {e.Message}");
                }
            }
        }

        public static void Enqueue(Action action)
        {
            _mainThreadQueue.Enqueue(action);
        }
    }
}
