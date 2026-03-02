using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Editor window that runs a local MCP server to interact with Unity.
    /// </summary>
    [InitializeOnLoad]
    public partial class MCPServerWindow : EditorWindow
    {
        static MCPServerWindow()
        {
            // Just ensure OnEnable runs on an instance
            EditorApplication.delayCall += () => {
                if (SessionState.GetBool("MCP_Server_Running", false))
                {
                    var windows = Resources.FindObjectsOfTypeAll<MCPServerWindow>();
                    if (windows.Length == 0) StartServerStandalone();
                }
            };
        }

        private static void StartServerStandalone()
        {
            var window = CreateInstance<MCPServerWindow>();
            window.ParseCommandLineArgs();
            window._port = window._cliPortOverride ?? MCPSettings.Port;
            window.StartServer();
        }
        private static ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();
        private double _lastUrlCopyTime = -10.0;
        private int _port;
        private bool _isRunning;
        private HttpListener _listener;
        private WebSocket _webSocket;
        private CancellationTokenSource _cts;
        private int? _cliPortOverride;
        private string _cliStatusMessage = "Checking link...";
        private string _version = "0.0.0";

        private int _selectedTab = 0;
        private readonly string[] _tabs = { "Server", "Tools", "Verification" };

        /// <summary>
        /// Shows the main Nexus Unity window and initializes it.
        /// </summary>
        [MenuItem("Window/Nexus Unity")]
        public static void ShowWindow() => GetWindow<MCPServerWindow>("Nexus Unity");

        private void OnEnable()
        {
            ParseCommandLineArgs();
            _port = _cliPortOverride ?? MCPSettings.Port;
            EditorApplication.update += HandleMainThreadQueue;
            EditorApplication.update += UpdateCopyFeedback;
            Application.logMessageReceivedThreaded += OnLogMessageReceived;
            if (SessionState.GetBool("MCP_Server_Running", false)) StartServer();
            CheckCliLinkStatus();
            LoadVersion();
        }

        private void LoadVersion()
        {
            try
            {
                var script = MonoScript.FromScriptableObject(this);
                string path = AssetDatabase.GetAssetPath(script);
                if (string.IsNullOrEmpty(path)) return;

                string dir = Path.GetDirectoryName(path);
                while (!string.IsNullOrEmpty(dir))
                {
                    string pkgPath = Path.Combine(dir, "package.json");
                    if (File.Exists(pkgPath))
                    {
                        string json = File.ReadAllText(pkgPath);
                        var data = Newtonsoft.Json.Linq.JObject.Parse(json);
                        _version = data["version"]?.ToString() ?? "0.0.0";
                        titleContent = new GUIContent($"Nexus Unity v{_version}");
                        return;
                    }
                    dir = Path.GetDirectoryName(dir);
                }
            }
            catch (Exception) { _version = "unknown"; }
        }

        private void OnDisable()
        {
            EditorApplication.update -= HandleMainThreadQueue;
            EditorApplication.update -= UpdateCopyFeedback;
            Application.logMessageReceivedThreaded -= OnLogMessageReceived;
            
            // Only cancel background tasks, don't clear the persistent "running" intent
            CleanupServer();
        }

        private void ParseCommandLineArgs()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "--mcp-port" && int.TryParse(args[i + 1], out int p)) _cliPortOverride = p;
        }

        private void CheckCliLinkStatus()
        {
            // Simple check: run gemini mcp list and see if we are in it
            System.Threading.Tasks.Task.Run(() => {
                try 
                {
                    // We can't easily parse the whole list, but we can check if the link command might be needed
                    // For now, we'll just assume it's checking and provide a way to refresh
                    _cliStatusMessage = "Ready to Link"; 
                }
                catch { _cliStatusMessage = "CLI Not Found"; }
            });
        }

        private void HandleMainThreadQueue()
        {
            while (_mainThreadQueue.TryDequeue(out var action)) action?.Invoke();
        }

        private void UpdateCopyFeedback()
        {
            if (EditorApplication.timeSinceStartup - _lastUrlCopyTime < 2.0) Repaint();
        }

        /// <summary>Enqueues an action to be executed on the main thread.</summary>
        public static void Enqueue(Action action) => _mainThreadQueue.Enqueue(action);
    }
}
