using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    public enum ServerState { Stopped, Starting, Running, Attached, Error }

    public static partial class MCPServer
    {
        private static string _version = "3.0.0";
        private static long _logCounter = 0;
        public static string Version => _version;

        public static string SessionId { get; private set; }
        public static int SessionGeneration { get; private set; }
        public static DateTime LastMainThreadTickUtc { get; private set; }
        public static bool IsCompilingCached { get; private set; }
        public static bool IsUpdatingCached { get; private set; }
        public static bool IsPlayingCached { get; private set; }
        public static bool IsPausedCached { get; private set; }
        public static bool IsPlayModeTransitionCached { get; private set; }

        private static ConcurrentQueue<Action> _mainThreadQueue;
        private static ConcurrentQueue<LogEntry> _logs;
        private static int _port;
        private static ServerState _state = ServerState.Stopped;
        public static ServerState State => _state;
        public static string LastError { get; private set; }

        private static HttpListener _listener;
        private static WebSocket _webSocket;
        private static CancellationTokenSource _cts;
        private static int? _cliPortOverride;
        private const int _MAX_LOGS = 5000;
        
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

        private static readonly object _startLock = new object();

        static MCPServer()
        {
            _mainThreadQueue = new ConcurrentQueue<Action>();
            _logs = new ConcurrentQueue<LogEntry>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RuntimeInit() => Init();

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
            MCPServerMethods.Init();
            InitTimeline();
            
            EditorApplication.update -= HandleMainThreadQueue;
            EditorApplication.update += HandleMainThreadQueue;
            Application.logMessageReceivedThreaded -= OnLogMessageReceived;
            Application.logMessageReceivedThreaded += OnLogMessageReceived;

            #if UNITY_2019_1_OR_NEWER
            Application.logMessageReceived -= OnLogMessageReceived;
            Application.logMessageReceived += OnLogMessageReceived;
            #endif

            UnityMCP.Runtime.MCPRuntimeLogger.OnLogReceived -= OnLogMessageReceived;
            UnityMCP.Runtime.MCPRuntimeLogger.OnLogReceived += OnLogMessageReceived;            
            AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;

            ParseCommandLineArgs();
            _port = _cliPortOverride ?? MCPSettings.Port;
            
            if (EditorPrefs.GetBool(StablePrefsKey, false))
            {
                int framesToWait = 60;
                EditorApplication.CallbackFunction autoStart = null;
                autoStart = () => {
                    if (framesToWait-- <= 0) {
                        EditorApplication.update -= autoStart;
                        if (_state != ServerState.Running) Start();
                    }
                };
                EditorApplication.update += autoStart;
            }

            #if UNITY_EDITOR_OSX
            _postCompileFramesToWait = 15;
            EditorApplication.update += HandlePostCompileFocusReturn;
            #endif
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

        public static int Port => _port;
        public static bool IsRunning => _state == ServerState.Running;

        public static void Start()
        {
            if (_mainThreadId != -1 && Thread.CurrentThread.ManagedThreadId != _mainThreadId)
            {
                EditorApplication.delayCall += Start;
                return;
            }

            lock (_startLock)
            {
                if (_state == ServerState.Running || _state == ServerState.Starting) return;
                _state = ServerState.Starting;
                LastError = null;
                _cts = new CancellationTokenSource();
            }
            
            MCPServerMethods.Init();
            if (EditorApplication.isPlaying) Application.runInBackground = true;

            if (_port <= 0) {
                ParseCommandLineArgs();
                _port = _cliPortOverride ?? MCPSettings.Port;
            }

            if (_port == 0)
            {
                try {
                    var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
                    l.Start();
                    _port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
                    l.Stop();
                    System.Threading.Thread.Sleep(100);
                } catch { }
            }
            
            EditorPrefs.SetBool(StablePrefsKey, true);
            Debug.Log($"[MCP] Attempting to start server on port {_port}...");

            var token = _cts.Token;
            Task.Run(async () => {
                try {
                    if (IsPortBusy(_port))
                    {
                        if (await IsAnotherMcpInstanceRunning()) {
                            _state = ServerState.Attached;
                            return;
                        }
                        
                        string owner = GetPortOwner(_port);
                        if (owner != "Unknown Process")
                        {
                            _state = ServerState.Error;
                            LastError = $"Port {_port} is being used by another application: {owner}.";
                            Debug.LogError($"[MCP] {LastError}");
                            return;
                        }
                        Debug.LogWarning($"[MCP] Port {_port} reported busy by Unknown Process. Proceeding with force-bind attempt...");
                    }

                    if (token.IsCancellationRequested) return;
                    #if UNITY_EDITOR_OSX
                    AppNapBypass.Enable();
                    #endif
                    BindAndStartListener();
                } catch (Exception e) {
                    _state = ServerState.Error;
                    LastError = e.Message;
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
        }

        internal static void Cleanup()
        {
            lock (_startLock)
            {
                _cts?.Cancel();
                if (_listener != null)
                {
                    try { if (_listener.IsListening) _listener.Stop(); } catch { }
                    try { _listener.Close(); } catch { }
                    _listener = null;
                }
                _state = ServerState.Stopped;
            }
        }

        private static bool IsPortBusy(int port)
        {
            try {
                var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
                var tcpListeners = ipProperties.GetActiveTcpListeners();
                if (tcpListeners != null && tcpListeners.Any(l => l.Port == port)) return true;
            } catch { }
            
            try
            {
                using (var tcp = new System.Net.Sockets.TcpClient()) {
                    var result = tcp.BeginConnect("127.0.0.1", port, null, null);
                    if (result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(200))) {
                        tcp.EndConnect(result);
                        return true;
                    }
                }
            } catch { }
            return false;
        }

        private static string GetPortOwner(int port)
        {
            try
            {
                var p = new System.Diagnostics.Process();
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.CreateNoWindow = true;

                if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.LinuxEditor)
                {
                    p.StartInfo.FileName = "/bin/bash";
                    p.StartInfo.Arguments = $"-c \"lsof -i TCP:{port} -s TCP:LISTEN -P -n || /usr/sbin/lsof -i TCP:{port} -s TCP:LISTEN -P -n\"";
                    p.Start();
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();

                    string[] lines = output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length > 1)
                    {
                        string[] parts = lines[1].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2) return $"{parts[0]} (PID: {parts[1]})";
                    }
                }
                else if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    p.StartInfo.FileName = "cmd.exe";
                    p.StartInfo.Arguments = $"/c netstat -a -n -o | findstr :{port}";
                    p.Start();
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();

                    string[] lines = output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (line.Contains("LISTENING"))
                        {
                            string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 0)
                            {
                                string pid = parts[parts.Length - 1];
                                try
                                {
                                    var proc = System.Diagnostics.Process.GetProcessById(int.Parse(pid));
                                    return $"{proc.ProcessName} (PID: {pid})";
                                }
                                catch { return $"PID: {pid}"; }
                            }
                        }
                    }
                }
            }
            catch { }
            return "Unknown Process";
        }

        private static void ParseCommandLineArgs()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "--mcp-port" && int.TryParse(args[i + 1], out int p)) _cliPortOverride = p;
        }
    }
}
