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
        private static string _version = "0.0.0";
        private static long _logCounter = 0;
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RuntimeInit()
        {
            Init();
        }

        [InitializeOnLoadMethod]
        internal static void Init()
        {
            try { File.AppendAllText("mcp_server_trace.txt", $"[SERVER] Init at {DateTime.Now} in {Directory.GetCurrentDirectory()}\n"); } catch {}
            _mainThreadQueue = _mainThreadQueue ?? new ConcurrentQueue<Action>();
            _logs = _logs ?? new ConcurrentQueue<LogEntry>();

            AppNapBypass.CacheApplicationPath();
            UpdateVersion();
            MCPServerMethods.Init();
            
            EditorApplication.update -= HandleMainThreadQueue;
            EditorApplication.update += HandleMainThreadQueue;

            Application.logMessageReceivedThreaded -= OnLogMessageReceived;
            Application.logMessageReceivedThreaded += OnLogMessageReceived;
            
            // Use absolute path for sanity check
            try {
                string path = Path.Combine(Directory.GetCurrentDirectory(), "mcp_log_capture.txt");
                File.AppendAllText(path, $"[{DateTime.Now}] Init Subscribed at {path}\n");
            } catch {}

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
            
            if (EditorPrefs.GetBool(PrefsKey, false))
            {
                Task.Run(async () => {
                    int retries = 5;
                    while (retries > 0 && IsPortBusy(_port))
                    {
                        await Task.Delay(500);
                        retries--;
                    }
                    Start();
                });
                
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

                // Dynamic interval: If compiling or updating, wait longer to reduce CPU/UI contention.
                int delay = (EditorApplication.isCompiling || EditorApplication.isUpdating) ? 1000 : 100;

                Task.Delay(delay).ContinueWith(_ => {
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
            } catch { _version = "2.6.0"; }
        }

        public static int Port => _port;
        public static bool IsRunning => _isRunning;

        public static async void Start()
        {
            System.IO.File.AppendAllText("mcp_log_capture.txt", $"[{DateTime.Now}] Server Start Called\n");
            if (_isRunning) return;
            MCPServerMethods.Init();
            
            // Re-subscribe to logs to be absolutely sure
            Application.logMessageReceivedThreaded -= OnLogMessageReceived;
            Application.logMessageReceivedThreaded += OnLogMessageReceived;
            
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

        public static void Stop()
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

        private static void ParseCommandLineArgs()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "--mcp-port" && int.TryParse(args[i + 1], out int p)) _cliPortOverride = p;
        }
    }
}
