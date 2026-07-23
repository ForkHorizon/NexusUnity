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
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Describes the current lifecycle state of the local Nexus Unity server.
    /// </summary>
    /// <remarks>
    /// <c>Stopped</c> means no local listener is active; <c>Starting</c> means startup work has been queued or is binding a port;
    /// <c>Running</c> means the loopback HTTP/WebSocket server is accepting requests; <c>Attached</c> means another Nexus Unity
    /// instance already owns the configured port; <c>Error</c> means startup failed and <see cref="MCPServer.LastError"/> contains details.
    /// </remarks>
    public enum ServerState { Stopped, Starting, Running, Attached, Error }

    public static partial class MCPServer
    {
        private static string _version;
        private static long _logCounter = 0;
        public static string Version => _version ?? (_version = ReadPackageVersion());

        private static string ReadPackageVersion()
        {
            try
            {
                var pkgInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(MCPServer).Assembly);
                if (pkgInfo != null && !string.IsNullOrEmpty(pkgInfo.version))
                {
                    return pkgInfo.version;
                }
            }
            catch { }

            try
            {
                string packageJsonPath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "NexusUnity", "package.json"));
                if (!File.Exists(packageJsonPath))
                {
                    packageJsonPath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "package.json"));
                }
                if (File.Exists(packageJsonPath))
                {
                    var json = JObject.Parse(File.ReadAllText(packageJsonPath));
                    string ver = json["version"]?.ToString();
                    if (!string.IsNullOrEmpty(ver)) return ver;
                }
            }
            catch { }

            return "1.5.0";
        }

        public static string SessionId { get; private set; }
        public static int SessionGeneration { get; private set; }
        internal const string AuthTokenEnvironmentVariable = "NEXUS_UNITY_AUTH_TOKEN";
        internal const string AuthTokenHeaderName = "X-Nexus-Unity-Token";
        private static string _authToken;
        public static DateTime LastMainThreadTickUtc { get; private set; }
        public static bool IsCompilingCached { get; private set; }
        public static bool IsUpdatingCached { get; private set; }
        public static bool IsPlayingCached { get; private set; }
        public static bool IsPausedCached { get; private set; }
        public static bool IsPlayModeTransitionCached { get; private set; }
        public static string UnityVersionCached { get; private set; }

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
        private static string AuthSessionStateKey => StablePrefsKey + "_AuthToken";
        
        private static int _mainThreadId = -1;
        public static int MainThreadId => _mainThreadId;

        private static readonly object _startLock = new object();

        internal static void RefreshMainThreadCachedState()
        {
            LastMainThreadTickUtc = DateTime.UtcNow;
            IsCompilingCached = EditorApplication.isCompiling;
            IsUpdatingCached = EditorApplication.isUpdating;
            IsPlayingCached = EditorApplication.isPlaying;
            IsPausedCached = EditorApplication.isPaused;
            IsPlayModeTransitionCached = EditorApplication.isPlayingOrWillChangePlaymode;
            UnityVersionCached = Application.unityVersion;
        }

        static MCPServer()
        {
            _mainThreadQueue = new ConcurrentQueue<Action>();
            _logs = new ConcurrentQueue<LogEntry>();
        }

        internal static string AuthToken => EnsureAuthToken();

        private static string EnsureAuthToken()
        {
            if (!string.IsNullOrEmpty(_authToken)) return _authToken;

            if (_mainThreadId != -1 && Thread.CurrentThread.ManagedThreadId != _mainThreadId)
            {
                return _authToken;
            }

            try
            {
                _authToken = SessionState.GetString(AuthSessionStateKey, string.Empty);
                if (string.IsNullOrEmpty(_authToken))
                {
                    _authToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                    SessionState.SetString(AuthSessionStateKey, _authToken);
                }

                WriteTokenFile(_authToken);
            }
            catch { }

            return _authToken;
        }

        private static void WriteTokenFile(string token)
        {
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
                string libraryDir = Path.Combine(projectRoot, "Library");
                if (Directory.Exists(libraryDir))
                {
                    File.WriteAllText(Path.Combine(libraryDir, "NexusUnityAuthToken.txt"), token);
                }
            }
            catch { }
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
                EnsureAuthToken();
            }
            
            RefreshMainThreadCachedState();
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

        /// <summary>
        /// Starts the loopback-only Nexus Unity HTTP/WebSocket server and persists restart intent in Unity <see cref="EditorPrefs"/>.
        /// </summary>
        /// <remarks>
        /// Startup must run from the Unity Editor main thread; background calls are marshaled through <see cref="EditorApplication.delayCall"/>.
        /// The method initializes tool dispatch, resolves or allocates the configured port, records restart intent, may attach to an
        /// existing Nexus Unity instance on the same port, and enables the macOS App Nap bypass before binding the listener.
        /// </remarks>
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
            EnsureAuthToken();
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
            NexusEditorLog.Log(NexusLogCategory.Server, $"[MCP] Attempting to start server on port {_port}...", true);

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
                            NexusEditorLog.Error(NexusLogCategory.Server, $"[MCP] {LastError}");
                            return;
                        }
                        NexusEditorLog.Warning(NexusLogCategory.Server, $"[MCP] Port {_port} reported busy by Unknown Process. Proceeding with force-bind attempt...");
                    }

                    if (token.IsCancellationRequested) return;
                    #if UNITY_EDITOR_OSX
                    AppNapBypass.Enable();
                    #endif
                    BindAndStartListener();
                } catch (Exception e) {
                    _state = ServerState.Error;
                    LastError = e.Message;
                    NexusEditorLog.Error(NexusLogCategory.Server, $"[MCP] Server start error: {e.Message}");
                }
            });
        }

        /// <summary>
        /// Stops the Nexus Unity server on the editor main thread, clears restart intent, disables macOS App Nap bypass, and closes listeners.
        /// </summary>
        /// <remarks>
        /// Calls from background threads are marshaled through <see cref="Enqueue"/> before mutating Unity editor state.
        /// Cleanup cancels pending server work and closes the HTTP listener/WebSocket state used by the local automation server.
        /// </remarks>
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



    }
}
