using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    public class MCPServerWindow : EditorWindow
    {
        private HttpListener _httpListener;
        private Thread _serverThread;
        private bool _isRunning = false;
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

            // Check if we should auto-restart
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
                // Clear state
                SessionState.EraseString("MCP_PendingAttach_Script");
                SessionState.EraseInt("MCP_PendingAttach_GO");

                var go = EditorUtility.InstanceIDToObject(pendingGoId) as GameObject;
                if (go != null)
                {
                     // Search all assemblies for the type
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
                _httpListener.Stop();
                _httpListener.Close();
                _httpListener = null;
            }

            // _serverThread will exit on its own

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
                catch (HttpListenerException) { }
                catch (ObjectDisposedException) { }
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
                responseString = $"{{\"jsonrpc\": \"2.0\", \"error\": {{\"code\": -32603, \"message\": \"{e.Message}\"}}, \"id\": null}}";
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
                Debug.LogError($"Error sending response: {e.Message}");
            }
        }

        private string ProcessJsonRpc(string json)
        {
            JsonRpcRequest request = null;
            try
            {
                request = JsonUtility.FromJson<JsonRpcRequest>(json);
            }
            catch
            {
                return $"{{\"jsonrpc\": \"2.0\", \"error\": {{\"code\": -32700, \"message\": \"Parse error\"}}, \"id\": null}}";
            }

            if (request == null || string.IsNullOrEmpty(request.method))
            {
                return $"{{\"jsonrpc\": \"2.0\", \"error\": {{\"code\": -32600, \"message\": \"Invalid Request\"}}, \"id\": null}}";
            }

            string result = "null";
            string error = null;

            // Dispatch to main thread and wait
            ManualResetEvent signal = new ManualResetEvent(false);

            Enqueue(() => {
                try
                {
                    result = ExecuteMethod(request);
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

            signal.WaitOne();

            if (error != null)
            {
                return $"{{\"jsonrpc\": \"2.0\", \"error\": {{\"code\": -32000, \"message\": \"{error}\"}}, \"id\": {request.id}}}";
            }

            return $"{{\"jsonrpc\": \"2.0\", \"result\": {result}, \"id\": {request.id}}}";
        }

        private string ExecuteMethod(JsonRpcRequest request)
        {
            switch (request.method)
            {
                case "create_primitive":
                    return CreatePrimitive(request.@params);
                case "attach_script":
                    return AttachScript(request.@params);
                default:
                    throw new Exception($"Method not found: {request.method}");
            }
        }

        private string CreatePrimitive(JsonRpcParams p)
        {
            if (p == null || string.IsNullOrEmpty(p.primitive_type)) throw new Exception("primitive_type is required");

            PrimitiveType type;
            try
            {
                type = (PrimitiveType)Enum.Parse(typeof(PrimitiveType), p.primitive_type, true);
            }
            catch
            {
                throw new Exception($"Invalid primitive type: {p.primitive_type}");
            }

            var go = GameObject.CreatePrimitive(type);
            go.name = "MCP_" + p.primitive_type;

            Selection.activeGameObject = go;

            return $"\"Created {go.name}\"";
        }

        private string AttachScript(JsonRpcParams p)
        {
            if (p == null || string.IsNullOrEmpty(p.script_name)) throw new Exception("script_name is required");

            string scriptName = p.script_name;
            // Sanitize script name
            scriptName = scriptName.Replace(" ", "_");

            string content = !string.IsNullOrEmpty(p.script_content) ? p.script_content :
                $"using UnityEngine;\npublic class {scriptName} : MonoBehaviour {{ void Start() {{ Debug.Log(\"Hello from {scriptName}\"); }} }}";

            string fileName = $"{scriptName}.cs";
            string path = Path.Combine("Assets", fileName);

            // Just write the file
            File.WriteAllText(path, content);

            // Store pending attach info
            if (Selection.activeGameObject != null)
            {
                SessionState.SetString("MCP_PendingAttach_Script", scriptName);
                SessionState.SetInt("MCP_PendingAttach_GO", Selection.activeGameObject.GetInstanceID());
            }

            // Refresh to trigger compilation
            AssetDatabase.Refresh();

            return $"\"Script {scriptName} created at {path}. Compilation triggered. It will be attached to {Selection.activeGameObject?.name} automatically after reload.\"";
        }

        private void HandleMainThreadQueue()
        {
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

    [Serializable]
    public class JsonRpcRequest
    {
        public string jsonrpc;
        public string method;
        public JsonRpcParams @params;
        public int id;
    }

    [Serializable]
    public class JsonRpcParams
    {
        public string primitive_type;
        public string script_name;
        public string script_content;
    }
}
