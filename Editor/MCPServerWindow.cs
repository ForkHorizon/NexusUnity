using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using UnityEditor;
using UnityEditor.Compilation;
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
        private static int _port = 8081;
        private static int? _cliPortOverride = null;
        private volatile bool _isCompiling = false;
        private static readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

        // WebSocket Client Fields
        private ClientWebSocket _webSocket;
        private CancellationTokenSource _wsCts;
        private string _wsUrl = "ws://localhost:8080";
        private bool _wsConnected = false;

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
            ParseCommandLineArgs();
            _port = _cliPortOverride ?? MCPSettings.Port;

            EditorApplication.update += HandleMainThreadQueue;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
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
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            Application.logMessageReceivedThreaded -= OnLogMessageReceived;
            StopServer();
            DisconnectWebSocket();
        }

        private void ParseCommandLineArgs()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--mcp-port" && i + 1 < args.Length)
                {
                    if (int.TryParse(args[i + 1], out int p))
                    {
                        _cliPortOverride = p;
                        Debug.Log($"[MCP] Port overridden by CLI: {_cliPortOverride}");
                    }
                }
            }
        }

        private void OnCompilationStarted(object obj)
        {
            _isCompiling = true;
        }

        private void OnCompilationFinished(object obj)
        {
            _isCompiling = false;
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

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Status: {(_isRunning ? "Running" : "Stopped")}");
            var rect = GUILayoutUtility.GetRect(20, 20);
            EditorGUI.DrawRect(rect, _isRunning ? Color.green : Color.red);
            GUILayout.EndHorizontal();

            if (_isCompiling)
            {
                EditorGUILayout.HelpBox("Compiling...", MessageType.Info);
            }

            GUILayout.Label($"Port: {(_cliPortOverride ?? MCPSettings.Port)} {(_cliPortOverride.HasValue ? "(CLI)" : "")}");

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

            GUILayout.Space(20);
            GUILayout.Label("WebSocket Client Bridge", EditorStyles.boldLabel);
            _wsUrl = EditorGUILayout.TextField("Bridge URL", _wsUrl);

            if (!_wsConnected)
            {
                if (GUILayout.Button("Connect to Bridge"))
                {
                    ConnectWebSocket();
                }
            }
            else
            {
                GUILayout.Label("Status: Connected");
                if (GUILayout.Button("Disconnect"))
                {
                    DisconnectWebSocket();
                }
            }
        }

        private void StartServer()
        {
            if (_isRunning) return;

            _port = _cliPortOverride ?? MCPSettings.Port;

            try
            {
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add($"http://localhost:{_port}/");
                _httpListener.Start();

                _isRunning = true;
                _serverThread = new Thread(ServerLoop);
                _serverThread.Start();

                Debug.Log($"MCP Server started on port {_port}");
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
            if (_isCompiling)
            {
                string errorJson = "{\"jsonrpc\": \"2.0\", \"error\": {\"code\": -32000, \"message\": \"Server is busy compiling\"}, \"id\": null}";
                SendResponse(context, errorJson, 503);
                return;
            }

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

        /// <summary>
        /// Starts an Editor Coroutine.
        /// </summary>
        public static void StartCoroutine(System.Collections.IEnumerator routine)
        {
             Unity.EditorCoroutines.Editor.EditorCoroutineUtility.StartCoroutineOwnerless(routine);
        }

        private async void ConnectWebSocket()
        {
            if (_webSocket != null) return;

            try
            {
                _webSocket = new ClientWebSocket();
                _wsCts = new CancellationTokenSource();
                await _webSocket.ConnectAsync(new Uri(_wsUrl), _wsCts.Token);
                _wsConnected = true;
                Debug.Log($"Connected to MCP Bridge at {_wsUrl}");

                // Fire and forget receive loop on background thread.
                // Critical: Must run on background thread to avoid deadlock with Main Thread Dispatcher.
                _ = Task.Run(ReceiveWebsocketLoop);
            }
            catch (Exception e)
            {
                Debug.LogError($"WebSocket connection failed: {e.Message}");
                DisconnectWebSocket();
            }
        }

        private void DisconnectWebSocket()
        {
            if (_wsCts != null)
            {
                _wsCts.Cancel();
                _wsCts.Dispose();
                _wsCts = null;
            }

            if (_webSocket != null)
            {
                try
                {
                    // We can't wait too long on main thread or GUI thread
                    _webSocket.Dispose();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Error disposing websocket: {e.Message}");
                }
                finally {
                    _webSocket = null;
                }
            }

            _wsConnected = false;
            // Debug.Log("WebSocket disconnected");
        }

        private async Task ReceiveWebsocketLoop()
        {
            var buffer = new byte[32768]; // 32KB buffer
            try
            {
                while (_webSocket != null && _webSocket.State == WebSocketState.Open && !_wsCts.IsCancellationRequested)
                {
                    WebSocketReceiveResult result;
                    using (var ms = new MemoryStream())
                    {
                        do
                        {
                            result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _wsCts.Token).ConfigureAwait(false);
                            if (result.MessageType == WebSocketMessageType.Close) break;
                            ms.Write(buffer, 0, result.Count);
                        } while (!result.EndOfMessage);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None).ConfigureAwait(false);
                            _wsConnected = false;
                            break;
                        }

                        if (result.MessageType == WebSocketMessageType.Text)
                        {
                            string msg = Encoding.UTF8.GetString(ms.ToArray());

                            // Process Request
                            string response = MCPServerMethods.ProcessJsonRpc(msg);

                            // Send Response
                            if (!string.IsNullOrEmpty(response) && _webSocket != null && _webSocket.State == WebSocketState.Open)
                            {
                                byte[] respBytes = Encoding.UTF8.GetBytes(response);
                                await _webSocket.SendAsync(new ArraySegment<byte>(respBytes), WebSocketMessageType.Text, true, _wsCts.Token).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (_wsCts != null && !_wsCts.IsCancellationRequested)
                {
                    Debug.LogError($"WebSocket receive error: {e.Message}");
                }
                _wsConnected = false;
            }
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
