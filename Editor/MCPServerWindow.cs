using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private const int PORT = 8081;
        private static readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

        private static readonly List<LogEntry> _logEntries = new List<LogEntry>();
        private static readonly object _logLock = new object();
        private const int MAX_LOG_ENTRIES = 1000;

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
            Application.logMessageReceivedThreaded += OnLogMessageReceived;

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
            Application.logMessageReceivedThreaded -= OnLogMessageReceived;
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
            // Security Checks

            // 1. Host Header Validation (DNS Rebinding Protection)
            // Use Url.Host to reliably parse the host (handling ports and IPv6)
            string host = context.Request.Url.Host;
            if (!host.Equals("localhost", StringComparison.OrdinalIgnoreCase) &&
                !host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
            {
                SendResponse(context, "Invalid Host Header", 403);
                return;
            }

            // 2. Method Validation
            if (context.Request.HttpMethod != "POST")
            {
                context.Response.AddHeader("Allow", "POST");
                SendResponse(context, "Method Not Allowed", 405);
                return;
            }

            // 3. Content-Type Validation (CSRF Protection)
            if (context.Request.ContentType == null ||
                !context.Request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            {
                SendResponse(context, "Unsupported Media Type: Content-Type must be application/json", 415);
                return;
            }

            try
            {
                // Security: CSRF Protection
                string origin = context.Request.Headers["Origin"];
                if (!string.IsNullOrEmpty(origin) && origin != "null")
                {
                    bool allowed = false;
                    try
                    {
                        Uri uri = new Uri(origin);
                        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
                        {
                            allowed = true;
                        }
                    }
                    catch { }

                    if (!allowed)
                    {
                        string errorJson = "{\"jsonrpc\": \"2.0\", \"error\": {\"code\": -32000, \"message\": \"Forbidden: Invalid Origin\"}, \"id\": null}";
                        SendResponse(context, errorJson, 403);
                        return;
                    }
                }

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

        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            lock (_logLock)
            {
                if (_logEntries.Count > 0)
                {
                    var last = _logEntries[_logEntries.Count - 1];
                    if (last.message == condition && last.stackTrace == stackTrace && last.type == type.ToString())
                    {
                        last.count++;
                        return;
                    }
                }

                if (_logEntries.Count >= MAX_LOG_ENTRIES)
                {
                    _logEntries.RemoveAt(0);
                }

                _logEntries.Add(new LogEntry(condition, stackTrace, type));
            }
        }

        /// <summary>
        /// Retrieves the captured logs with optional filtering.
        /// </summary>
        /// <param name="count">Max number of logs to return.</param>
        /// <param name="filterType">Optional filter for log type (e.g. "Error").</param>
        /// <param name="searchText">Optional search text to filter messages.</param>
        /// <returns>A list of matching log entries.</returns>
        public static List<LogEntry> GetLogs(int count, string filterType, string searchText)
        {
            lock (_logLock)
            {
                var query = _logEntries.AsEnumerable();

                if (!string.IsNullOrEmpty(filterType))
                {
                    query = query.Where(l => l.type.Equals(filterType, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrEmpty(searchText))
                {
                    query = query.Where(l => l.message.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0);
                }

                var list = query.ToList();
                return list.Skip(Math.Max(0, list.Count - count)).ToList();
            }
        }

        /// <summary>
        /// Clears all captured log entries.
        /// </summary>
        public static void ClearLogs()
        {
            lock (_logLock)
            {
                _logEntries.Clear();
            }
        }
    }
}
