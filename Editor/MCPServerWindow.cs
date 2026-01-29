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
    public partial class MCPServerWindow : EditorWindow
    {
        private static ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();
        private int _port;
        private bool _isRunning;
        private HttpListener _listener;
        private WebSocket _webSocket;
        private CancellationTokenSource _cts;
        private int? _cliPortOverride;
        private bool _isCompiling;

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
        }

        private void OnDisable()
        {
            EditorApplication.update -= HandleMainThreadQueue;
            Application.logMessageReceivedThreaded -= OnLogMessageReceived;
            StopServer();
        }

        private void ParseCommandLineArgs()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "--mcp-port" && int.TryParse(args[i + 1], out int p)) _cliPortOverride = p;
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
            GUILayout.Label("MCP Server Status", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(_isRunning ? $"Running on port {_port}" : "Server Stopped", _isRunning ? MessageType.Info : MessageType.Warning);
            
            if (!_isRunning && GUILayout.Button("Start Server", GUILayout.Height(30))) StartServer();
            if (_isRunning && GUILayout.Button("Stop Server", GUILayout.Height(30))) StopServer();

            EditorGUILayout.Space();
            if (GUILayout.Button("Link to Gemini CLI")) MCPCliInstaller.LinkToGemini();
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

        private void StopServer()
        {
            _cts?.Cancel();
            _listener?.Stop();
            _isRunning = false;
            SessionState.SetBool("MCP_Server_Running", false);
        }

        private void HandleMainThreadQueue()
        {
            while (_mainThreadQueue.TryDequeue(out var action)) action?.Invoke();
        }

        /// <summary>Enqueues an action to be executed on the main thread.</summary>
        public static void Enqueue(Action action) => _mainThreadQueue.Enqueue(action);
    }
}
