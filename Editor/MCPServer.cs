using System;
using System.Collections.Concurrent;
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
    public static partial class MCPServer
    {
        private static string _version = "2.7.0";
        private static long _logCounter = 0;
        public static string Version => _version;

        public static string SessionId { get; private set; }
        public static int SessionGeneration { get; private set; }
        public static DateTime LastMainThreadTickUtc { get; private set; }
        public static bool IsCompilingCached { get; private set; }
        public static bool IsUpdatingCached { get; private set; }
        public static bool IsPlayingCached { get; private set; }
        public static bool IsPausedCached { get; private set; }

        private static ConcurrentQueue<Action> _mainThreadQueue;
        private static ConcurrentQueue<LogEntry> _logs;
        private static int _port;
        private static bool _isRunning;
        private static HttpListener _listener;
        private static WebSocket _webSocket;
        private static CancellationTokenSource _cts;
        private static int? _cliPortOverride;
        private const int _MAX_LOGS = 1000;
        
        // Use a deterministic hash or just the sanitized path
        private static string GetDeterministicProjectKey()
        {
            int hash = 17;
            string path = Application.dataPath;
            foreach (char c in path) hash = hash * 31 + c;
            return $"NexusUnity_ServerRunning_{hash}";
        }
        private static string _prefsKeyCached;
        private static string StablePrefsKey => _prefsKeyCached ?? (_prefsKeyCached = GetDeterministicProjectKey());
        
        private static int _mainThreadId = -1;
        public static int MainThreadId => _mainThreadId;

        static MCPServer()
        {
            _mainThreadQueue = new ConcurrentQueue<Action>();
            _logs = new ConcurrentQueue<LogEntry>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RuntimeInit()
        {
            Init();
        }

        [InitializeOnLoadMethod]
        internal static void Init()
        {
            if (_mainThreadId == -1) _mainThreadId = Thread.CurrentThread.ManagedThreadId;

            _mainThreadQueue = _mainThreadQueue ?? new ConcurrentQueue<Action>();
            _logs = _logs ?? new ConcurrentQueue<LogEntry>();

            if (Thread.CurrentThread.ManagedThreadId == _mainThreadId)
            {
                SessionId = SessionState.GetString("MCP_SessionId", Guid.NewGuid().ToString("N"));
                SessionState.SetString("MCP_SessionId", SessionId);
                
                SessionGeneration = SessionState.GetInt("MCP_SessionGen", 0) + 1;
                SessionState.SetInt("MCP_SessionGen", SessionGeneration);
            }
            
            LastMainThreadTickUtc = DateTime.UtcNow;

            #if UNITY_EDITOR_OSX
            AppNapBypass.CacheApplicationPath();
            #endif
            UpdateVersion();
            MCPServerMethods.Init();
            
            EditorApplication.update -= HandleMainThreadQueue;
            EditorApplication.update += HandleMainThreadQueue;

            Application.logMessageReceivedThreaded -= OnLogMessageReceived;
            Application.logMessageReceivedThreaded += OnLogMessageReceived;

            #if UNITY_2019_1_OR_NEWER
            Application.logMessageReceived -= OnLogMessageReceived;
            Application.logMessageReceived += OnLogMessageReceived;
            #endif

            // Bridge from Runtime assembly
            UnityMCP.Runtime.MCPRuntimeLogger.OnLogReceived -= OnLogMessageReceived;
            UnityMCP.Runtime.MCPRuntimeLogger.OnLogReceived += OnLogMessageReceived;            
            AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;

            ParseCommandLineArgs();
            _port = _cliPortOverride ?? MCPSettings.Port;
            
            if (EditorPrefs.GetBool(StablePrefsKey, false))
            {
                // Delayed auto-start to ensure Unity is ready
                EditorApplication.delayCall += () => {
                    Task.Delay(1500).ContinueWith(_ => {
                        EditorApplication.delayCall += Start;
                    });
                };
                
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
            } catch { _version = "2.7.0"; }
        }

        public static int Port => _port;
        public static bool IsRunning => _isRunning;

        public static void Start()
        {
            // Thread safety check
            if (_mainThreadId != -1 && Thread.CurrentThread.ManagedThreadId != _mainThreadId)
            {
                EditorApplication.delayCall += Start;
                return;
            }

            if (_isRunning) return;
            
            MCPServerMethods.Init();

            if (_port <= 0) {
                ParseCommandLineArgs();
                _port = _cliPortOverride ?? MCPSettings.Port;
            }
            
            // Re-subscribe to logs to be absolutely sure
            Application.logMessageReceivedThreaded -= OnLogMessageReceived;
            Application.logMessageReceivedThreaded += OnLogMessageReceived;

            // Persist auto-start immediately while we're still on the main thread.
            // Doing this via the queue after listener startup is vulnerable to domain reloads
            // that can discard the queued action before it runs.
            EditorPrefs.SetBool(StablePrefsKey, true);
            
            Debug.Log($"[MCP] Attempting to start server on port {_port}...");

            Task.Run(async () => {
                try {
                    if (IsPortBusy(_port))
                    {
                        if (await IsAnotherMcpInstanceRunning()) {
                            _isRunning = true; // Mark as running so we don't try to start again
                            return;
                        }
                        Debug.LogError($"[MCP] Port {_port} is being used by another application.");
                        return;
                    }

                    _isRunning = true;
                    // Note: AppNapBypass.Enable is now background-thread safe
                    #if UNITY_EDITOR_OSX
                    AppNapBypass.Enable();
                    #endif

                    BindAndStartListener();
                } catch (Exception e) {
                    _isRunning = false;
                    Debug.LogError($"[MCP] Server start error: {e.Message}");
                }
            });
        }

        public static void Stop()
        {
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
            {
                MCPServer.Enqueue(Stop);
                return;
            }

            EditorPrefs.SetBool(StablePrefsKey, false);
            #if UNITY_EDITOR_OSX
            AppNapBypass.Disable();
            #endif
            Cleanup();
            Debug.Log("[MCP] Server stopped manually");
        }

        internal static void Cleanup()
        {
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
            try {
                using (var tcp = new System.Net.Sockets.TcpClient()) {
                    var result = tcp.BeginConnect("127.0.0.1", port, null, null);
                    bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(100));
                    if (!success) return false;
                    tcp.EndConnect(result);
                    return true;
                }
            } catch { return false; }
        }

        private static void ParseCommandLineArgs()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "--mcp-port" && int.TryParse(args[i + 1], out int p)) _cliPortOverride = p;
        }
    }
}
