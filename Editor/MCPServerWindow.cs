using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
            EditorApplication.delayCall += () => {
                if (SessionState.GetBool("MCP_Server_Running", false))
                {
                    var windows = Resources.FindObjectsOfTypeAll<MCPServerWindow>();
                    if (windows.Length > 0) windows[0].StartServer();
                    else StartServerStandalone();
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
        private int _port;
        private bool _isRunning;
        private HttpListener _listener;
        private WebSocket _webSocket;
        private CancellationTokenSource _cts;
        private int? _cliPortOverride;
        private bool _isCompiling;
        private bool _isLinkedToCli;
        private string _cliStatusMessage = "Checking link...";

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
            CompilationPipeline.compilationStarted += (o) => _isCompiling = true;
            CompilationPipeline.compilationFinished += (o) => _isCompiling = false;
            Application.logMessageReceivedThreaded += OnLogMessageReceived;
            if (SessionState.GetBool("MCP_Server_Running", false)) StartServer();
            CheckCliLinkStatus();
        }

        private void OnDisable()
        {
            EditorApplication.update -= HandleMainThreadQueue;
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

        private void OnGUI()
        {
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabs);
            EditorGUILayout.Space();

            switch (_selectedTab)
            {
                case 0: DrawServerTab(); break;
                case 1: DrawToolsTab(); break;
                case 2: DrawVerificationTab(); break;
            }
        }

        private void DrawServerTab()
        {
            GUILayout.Label("Server Control", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                string status = _isRunning ? "RUNNING" : "STOPPED";
                GUI.color = _isRunning ? Color.green : Color.red;
                GUILayout.Label($"● {status}", EditorStyles.boldLabel);
                GUI.color = Color.white;
                GUILayout.FlexibleSpace();
                GUILayout.Label($"Port: {_port}");
            }

            EditorGUILayout.Space();

            if (!_isRunning)
            {
                if (GUILayout.Button("START SERVER", GUILayout.Height(40))) StartServer();
            }
            else
            {
                if (GUILayout.Button("STOP SERVER", GUILayout.Height(40))) StopServer();
            }

            EditorGUILayout.Space();
            GUILayout.Label("Gemini CLI Integration", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label($"Status: {_cliStatusMessage}");
                if (GUILayout.Button("Refresh", GUILayout.Width(60))) CheckCliLinkStatus();
            }

            if (GUILayout.Button("Link to Gemini CLI", GUILayout.Height(30))) 
            {
                MCPCliInstaller.LinkToGemini();
                CheckCliLinkStatus();
            }
        }

        private void DrawToolsTab()
        {
            GUILayout.Label("Developer Tools", EditorStyles.boldLabel);
            if (GUILayout.Button("Open Test Window")) MCPTestWindow.ShowWindow();
            if (GUILayout.Button("Clear All Logs")) ClearLogs();
        }

        private void DrawVerificationTab()
        {
            GUILayout.Label("API Verification", EditorStyles.boldLabel);
            if (GUILayout.Button("Run Full API Verification")) 
            {
                // We'll call the method from MCPVerificationWindow directly if possible, or just open it
                GetWindow<MCPVerificationWindow>().Show();
            }
            if (GUILayout.Button("Verify UI Instruments")) UIVerification.Verify();
            if (GUILayout.Button("Verify MCP Logs")) LogVerification.Verify();
        }

        private void HandleMainThreadQueue()
        {
            while (_mainThreadQueue.TryDequeue(out var action)) action?.Invoke();
        }


        /// <summary>Enqueues an action to be executed on the main thread.</summary>
        public static void Enqueue(Action action) => _mainThreadQueue.Enqueue(action);
    }
}
