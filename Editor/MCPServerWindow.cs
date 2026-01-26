using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Editor window that runs a local MCP (Model Context Protocol) server to interact with Unity from external tools.
    /// </summary>
    public class MCPServerWindow : EditorWindow
    {
        private HttpListener _httpListener;
        private Thread _serverThread;
        private volatile bool _isRunning = false;
        private const int PORT = 8080;
        private static readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

        /// <summary>
        /// Shows the MCP Server window.
        /// </summary>
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

            MCPServerMethods.CheckPendingAttachments();
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
                _httpListener.Prefixes.Add($"http://localhost:{PORT}/");
                _httpListener.Start();

                _isRunning = true;
                _serverThread = new Thread(ServerLoop);
                _serverThread.Start();

                Debug.Log($"MCP Server started on port {PORT}");
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
            try
            {
                string requestBody;
                using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                {
                    requestBody = reader.ReadToEnd();
                }

                string responseString = MCPServerMethods.ProcessJsonRpc(requestBody);
                SendResponse(context, responseString, 200);
            }
            catch (Exception e)
            {
                var errObj = MCPServerMethods.CreateErrorResponse(null, -32603, e.Message);
                SendResponse(context, errObj, 500);
                Debug.LogError($"Error handling request: {e.Message}");
            }
        }

        private void SendResponse(HttpListenerContext context, string responseString, int statusCode)
        {
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

        private void HandleMainThreadQueue()
        {
            while (_mainThreadQueue.TryDequeue(out var action))
            {
                try { action?.Invoke(); }
                catch (Exception e) { Debug.LogError($"Error executing on main thread: {e.Message}"); }
            }
        }

        /// <summary>
        /// Enqueues an action to be executed on the Unity main thread.
        /// </summary>
        public static void Enqueue(Action action)
        {
            _mainThreadQueue.Enqueue(action);
        }
    }
}
