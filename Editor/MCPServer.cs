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
        private static readonly System.Collections.Generic.HashSet<WebSocket> _activeWebSockets = new System.Collections.Generic.HashSet<WebSocket>();
        private static readonly object _webSocketLock = new object();
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
            // isPlayingOrWillChangePlaymode stays true for the WHOLE play session,
            // so using it directly reported a permanent "play_mode_transition":
            // acceptsWriteCommands stayed false and Initialize threw "editor is
            // busy" for as long as the game ran. A transition is in progress only
            // while the two flags disagree (entering: will-change but not yet
            // playing; exiting: still playing but no longer will-change).
            IsPlayModeTransitionCached =
                EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying;
            UnityVersionCached = Application.unityVersion;
        }

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
                EnsureAuthToken();
            }
            
            RefreshMainThreadCachedState();
            Application.runInBackground = true;
            #if UNITY_EDITOR_OSX
            AppNapBypass.CacheApplicationPath();
            AppNapBypass.Enable();
            #endif
            MCPServerMethods.Init();
            InitTimeline();

            SubscribeEditorEvents();

            ParseCommandLineArgs();
            _port = _cliPortOverride ?? MCPSettings.Port;

            if (EditorPrefs.GetBool(StablePrefsKey, false)) ScheduleAutoStart();

            #if UNITY_EDITOR_OSX
            _postCompileFramesToWait = 15;
            EditorApplication.update -= HandlePostCompileFocusReturn;
            EditorApplication.update += HandlePostCompileFocusReturn;
            #endif
        }

        // Idempotent (unsubscribe-then-subscribe) so it is safe to call on every
        // domain reload without stacking duplicate handlers.
        private static void SubscribeEditorEvents()
        {
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
        }

        // Restart intent was persisted in EditorPrefs, so start the server a few
        // frames after the reload settles rather than during Init itself.
        private static void ScheduleAutoStart()
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
    }
}
