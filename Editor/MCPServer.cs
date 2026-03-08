using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Static service that runs the local MCP server autonomously.
    /// </summary>
    [InitializeOnLoad]
    public static class MCPServer
    {
        private static string _version = "0.0.0";
        public static string Version => _version;

        private static ConcurrentQueue<Action> _mainThreadQueue;
        private static ConcurrentQueue<LogEntry> _logs;
        private static int _port;
        private static bool _isRunning;
        private static HttpListener _listener;
        private static WebSocket _webSocket;
        private static CancellationTokenSource _cts;
        private static int? _cliPortOverride;
        private const int _MAX_LOGS = 1000;
        private static string PrefsKey => $"NexusUnity_ServerRunning_{Application.dataPath.GetHashCode()}";
        
        private static int _backgroundTaskId = -1;

        static MCPServer()
        {
            _mainThreadQueue = new ConcurrentQueue<Action>();
            _logs = new ConcurrentQueue<LogEntry>();
            EditorApplication.delayCall += Init;
        }

        internal static async void Init()
        {
            UpdateVersion();
            MCPServerMethods.Init();
            
            EditorApplication.update -= HandleMainThreadQueue;
            EditorApplication.update += HandleMainThreadQueue;
            
            Application.logMessageReceivedThreaded -= OnLogMessageReceived;
            Application.logMessageReceivedThreaded += OnLogMessageReceived;
            
            AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;

            ParseCommandLineArgs();
            _port = _cliPortOverride ?? MCPSettings.Port;
            
            if (EditorPrefs.GetBool(PrefsKey, false))
            {
                int retries = 5;
                while (retries > 0 && IsPortBusy(_port))
                {
                    await Task.Delay(500);
                    retries--;
                }
                Start();
            }
        }

        private static void StartHeartbeat()
        {
            StopHeartbeat();
            
            // Create a Progress task to signal to Unity/macOS that work is happening.
            // We use Progress.Options.None for compatibility.
            _backgroundTaskId = Progress.Start("Nexus MCP Server", "Active", Progress.Options.None);
            
            ScheduleNextHeartbeat();
        }

        private static void ScheduleNextHeartbeat()
        {
            if (!_isRunning) return;

            // delayCall ALWAYS runs on the Main Thread.
            EditorApplication.delayCall += () => 
            {
                if (!_isRunning) return;
                
                // On the Main Thread, these calls are safe.
                try {
                    EditorApplication.QueuePlayerLoopUpdate();
                    if (_backgroundTaskId != -1) 
                        Progress.Report(_backgroundTaskId, (float)(DateTime.Now.Millisecond / 1000.0), "Nexus Background Link Active");
                } catch { }

                // Schedule next pulse in 100ms. 
                // We use a simple Task.Delay then add back to delayCall to keep the loop going
                // without pegging the CPU at 100% in a tight loop.
                Task.Delay(100).ContinueWith(_ => {
                    EditorApplication.delayCall += ScheduleNextHeartbeat;
                });
            };
        }

        private static void StopHeartbeat()
        {
            if (_backgroundTaskId != -1)
            {
                Progress.Remove(_backgroundTaskId);
                _backgroundTaskId = -1;
            }
        }

        private static void UpdateVersion()
        {
            try {
                var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(MCPServer).Assembly);
                if (pkg != null) {
                    _version = pkg.version;
                    return;
                }

                // Fallback: manual search
                string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(ScriptableObject.CreateInstance<MCPServerWindow>()));
                string dir = Path.GetDirectoryName(scriptPath);
                while (!string.IsNullOrEmpty(dir)) {
                    string pkgPath = Path.Combine(dir, "package.json");
                    if (File.Exists(pkgPath)) {
                        var data = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(pkgPath));
                        _version = data["version"]?.ToString() ?? "0.0.0";
                        return;
                    }
                    dir = Path.GetDirectoryName(dir);
                }
            } catch { _version = "2.4.0"; }
        }

        public static int Port => _port;
        public static bool IsRunning => _isRunning;

        public static async void Start()
        {
            if (_isRunning) return;
            Debug.Log($"[MCP] Attempting to start server on port {_port}...");

            if (IsPortBusy(_port))
            {
                if (await IsAnotherMcpInstanceRunning())
                {
                    Debug.LogWarning($"[MCP] Port {_port} is already in use by another Unity MCP instance.");
                }
                else
                {
                    Debug.LogError($"[MCP] Port {_port} is being used by another application. Please change the port in Project Settings.");
                }
                return;
            }
            
            _isRunning = true;
            StartHeartbeat();
            BindAndStartListener();
        }

        internal static void Stop()
        {
            EditorPrefs.SetBool(PrefsKey, false);
            Cleanup();
            Debug.Log("[MCP] Server stopped manually");
        }

        internal static void Cleanup()
        {
            StopHeartbeat();
            _cts?.Cancel();
            if (_listener != null)
            {
                try { if (_listener.IsListening) _listener.Stop(); } catch { }
                try { _listener.Close(); } catch { }
            }
            _isRunning = false;
        }

        private static bool IsPortBusy(int port)
        {
            try
            {
                var properties = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties();
                return properties.GetActiveTcpListeners().Any(l => l.Port == port);
            }
            catch { return false; }
        }

        private static async Task<bool> IsAnotherMcpInstanceRunning()
        {
            using var client = new System.Net.Http.HttpClient();
            client.Timeout = TimeSpan.FromMilliseconds(300); 
            try
            {
                var content = new System.Net.Http.StringContent("{\"jsonrpc\":\"2.0\",\"method\":\"initialize\",\"params\":{},\"id\":1}", Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"http://127.0.0.1:{_port}/", content);
                string body = await response.Content.ReadAsStringAsync();
                return body.Contains("Unity MCP Server");
            }
            catch { return false; }
        }

        private static void BindAndStartListener()
        {
            try
            {
                _cts = new CancellationTokenSource();
                _listener = new HttpListener();
                // Add prefixes for both 127.0.0.1 and localhost for maximum compatibility
                _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
                _listener.Prefixes.Add($"http://localhost:{_port}/");
                _listener.Start();
                EditorPrefs.SetBool(PrefsKey, true);
                _ = Task.Run(() => ServerLoop(_cts.Token));
                Debug.Log($"[MCP] Server started on port {_port}");
            }
            catch (Exception e)
            {
                Cleanup();
                Debug.LogError($"[MCP] Server failed to start: {e.Message}");
            }
        }

        private static async Task ServerLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && _listener != null && _listener.IsListening)
                {
                    var context = await _listener.GetContextAsync();
                    if (context.Request.IsWebSocketRequest) await ProcessWebSocket(context);
                    else HandleHttpRequest(context);
                }
            }
            catch (Exception e)
            {
                if (!token.IsCancellationRequested)
                    Debug.LogError($"[MCP] Fatal server loop error: {e.Message}");
            }
            finally { _isRunning = false; }
        }

        private static async Task ProcessWebSocket(HttpListenerContext context)
        {
            var wsContext = await context.AcceptWebSocketAsync(null);
            _webSocket = wsContext.WebSocket;
            await ReceiveWebsocketLoop(_cts.Token);
        }

        private static void HandleHttpRequest(HttpListenerContext context)
        {
            try {
                if (context.Request.HttpMethod != "POST") {
                    context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    context.Response.Close();
                    return;
                }

                using (var reader = new System.IO.StreamReader(context.Request.InputStream, context.Request.ContentEncoding ?? Encoding.UTF8))
                {
                    string json = reader.ReadToEnd();
                    string response = MCPServerMethods.ProcessJsonRpc(json);
                    byte[] buffer = Encoding.UTF8.GetBytes(response);
                    context.Response.ContentType = "application/json";
                    context.Response.ContentLength64 = buffer.Length;
                    context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                    context.Response.Close();
                }
            } catch (Exception e) {
                Debug.LogError($"[MCP] Error handling HTTP request: {e.Message}");
            }
        }

        private static async Task ReceiveWebsocketLoop(CancellationToken token)
        {
            var buffer = new byte[4096];
            using var ms = new MemoryStream();

            while (_webSocket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                ms.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                        return;
                    }
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage && !token.IsCancellationRequested);

                if (ms.Length > 0 && result.MessageType == WebSocketMessageType.Text)
                {
                    ms.Position = 0;
                    using (var reader = new System.IO.StreamReader(ms, Encoding.UTF8, false, 1024, leaveOpen: true))
                    {
                        string response = MCPServerMethods.ProcessJsonRpc(reader);
                        if (_webSocket.State == WebSocketState.Open)
                        {
                            var respBuffer = Encoding.UTF8.GetBytes(response);
                            await _webSocket.SendAsync(new ArraySegment<byte>(respBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                    }
                }
            }
        }

        internal static void AddLog(LogEntry log)
        {
            _logs.Enqueue(log);
            while (_logs.Count > _MAX_LOGS) _logs.TryDequeue(out _);
        }

        public static List<LogEntry> GetLogs(int count, string filterType, string searchText)
        {
            var query = _logs.AsEnumerable();
            if (!string.IsNullOrEmpty(filterType)) query = query.Where(l => l.Type.Equals(filterType, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(searchText)) query = query.Where(l => l.Message.Contains(searchText) || l.StackTrace.Contains(searchText));
            return query.Reverse().Take(count).ToList();
        }

        public static void ClearLogs() { while (_logs.TryDequeue(out _)) { } }

        internal static void HandleMainThreadQueue()
        {
            if (_mainThreadQueue == null || _mainThreadQueue.IsEmpty) return;

            while (_mainThreadQueue.TryDequeue(out var action))
            {
                try { action?.Invoke(); }
                catch (Exception e) { Debug.LogError($"[MCP] Error executing enqueued action: {e.Message}"); }
            }

            // Force a repaint in a safe phase
            EditorApplication.delayCall += () => {
                try { UnityEditorInternal.InternalEditorUtility.RepaintAllViews(); } catch { }
            };
        }

        private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            AddLog(new LogEntry(condition, stackTrace, type));
        }

        public static void Enqueue(Action action)
        {
            _mainThreadQueue.Enqueue(action);
            // DO NOT call QueuePlayerLoopUpdate here, it is not thread-safe.
            // The heartbeat loop running on the main thread will pick it up.
        }

        private static void ParseCommandLineArgs()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "--mcp-port" && int.TryParse(args[i + 1], out int p)) _cliPortOverride = p;
        }
    }
}