using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using UnityEditor;
using UnityEditor.Compilation;
#if HAS_EDITOR_COROUTINES
using Unity.EditorCoroutines.Editor;
#endif
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

        /// <summary>
        /// Shows the MCP Server window and initializes it.
        /// </summary>
        [MenuItem("Window/Unity MCP/Server")]
        public static void ShowWindow() => GetWindow<MCPServerWindow>("MCP Server");

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
            GUILayout.Label("MCP Server Status", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(_isRunning ? $"Running on port {_port}" : "Server Stopped", _isRunning ? MessageType.Info : MessageType.Warning);
            if (!_isRunning && GUILayout.Button("Start Server")) StartServer();
            if (_isRunning && GUILayout.Button("Stop Server")) StopServer();
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
        
        /// <summary>Starts an Editor Coroutine.</summary>
        public static void StartCoroutine(System.Collections.IEnumerator r) 
        {
#if HAS_EDITOR_COROUTINES
            EditorCoroutineUtility.StartCoroutineOwnerless(r);
#else
            Debug.LogWarning("[MCP] Editor Coroutines package is missing. Install 'com.unity.editorcoroutines' to use this feature.");
#endif
        }
    }
}
