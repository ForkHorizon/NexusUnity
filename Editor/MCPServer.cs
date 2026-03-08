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
    /// Handles the background heartbeat and OS-level App Nap bypass.
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
        

        static MCPServer()
        {
            _mainThreadQueue = new ConcurrentQueue<Action>();
            _logs = new ConcurrentQueue<LogEntry>();
        }

        [InitializeOnLoadMethod]
        internal static async void Init()
        {
            AppNapBypass.CacheApplicationPath();
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
                
                #if UNITY_EDITOR_OSX
                // After domain reload, wait for the editor to fully settle before 
                // returning focus to the previous app.
                _postCompileFramesToWait = 15;
                EditorApplication.update += HandlePostCompileFocusReturn;
                #endif
            }
        }

        #if UNITY_EDITOR_OSX
        private static int _postCompileFramesToWait = 15;
        private static void HandlePostCompileFocusReturn()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

            _postCompileFramesToWait--;
            if (_postCompileFramesToWait <= 0)
            {
                EditorApplication.update -= HandlePostCompileFocusReturn;
                AppNapBypass.ReturnToPreviousApp();
            }
        }
        #endif

        private static void StartHeartbeat()
        {
            StopHeartbeat();
            ScheduleNextHeartbeat();
        }

        private static void ScheduleNextHeartbeat()
        {
            if (!_isRunning) return;

            EditorApplication.delayCall += () => 
            {
                if (!_isRunning) return;
                
                try {
                    // Safe Main Thread Kicks - No RepaintAllViews here to avoid thread violations.
                    EditorApplication.QueuePlayerLoopUpdate();
                    
                    #if UNITY_EDITOR_OSX
                    AppNapBypass.WakeMainLoop();
                    #endif

                } catch { }

                Task.Delay(100).ContinueWith(_ => {
                    EditorApplication.delayCall += ScheduleNextHeartbeat;
                });
            };
        }

        private static void StopHeartbeat()
        {
            // Heartbeat stopped via _isRunning = false; in Stop()
        }

        private static void UpdateVersion()
        {
            try {
                var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(MCPServer).Assembly);
                if (pkg != null) {
                    _version = pkg.version;
                    return;
                }

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
                if (await IsAnotherMcpInstanceRunning()) return;
                Debug.LogError($"[MCP] Port {_port} is being used by another application.");
                return;
            }
            
            _isRunning = true;
            #if UNITY_EDITOR_OSX
            AppNapBypass.Enable();
            #endif
            StartHeartbeat();
            BindAndStartListener();
        }

        internal static void Stop()
        {
            EditorPrefs.SetBool(PrefsKey, false);
            #if UNITY_EDITOR_OSX
            AppNapBypass.Disable();
            #endif
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
            }
            catch (ObjectDisposedException) { }
            catch (System.Net.HttpListenerException) { }
            catch (Exception e)
            {
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
        }

        private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            AddLog(new LogEntry(condition, stackTrace, type));
        }

        public static void Enqueue(Action action)
        {
            _mainThreadQueue.Enqueue(action);
        }

        private static void ParseCommandLineArgs()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "--mcp-port" && int.TryParse(args[i + 1], out int p)) _cliPortOverride = p;
        }
    }
}