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
        private bool _isCompiling;
        private bool _isLinkedToCli;
        private string _cliStatusMessage = "Checking link...";

        private int _selectedTab = 0;
        private readonly string[] _tabs = { "Server", "Tools", "Verification" };
        private double _lastCopyTime = -1;

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
            CompilationPipeline.compilationStarted += (o) => _isCompiling = true;
            CompilationPipeline.compilationFinished += (o) => _isCompiling = false;
            Application.logMessageReceivedThreaded += OnLogMessageReceived;
            if (SessionState.GetBool("MCP_Server_Running", false)) StartServer();
            CheckCliLinkStatus();
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
            DrawServerStatusBar();

            EditorGUILayout.Space();

            if (!_isRunning)
            {
<<<<<<< HEAD
                if (GUILayout.Button(new GUIContent("START SERVER", "Starts the MCP server on the configured port."), GUILayout.Height(40))) StartServer();
            }
            else
            {
                if (GUILayout.Button(new GUIContent("STOP SERVER", "Stops the running MCP server."), GUILayout.Height(40))) StopServer();
=======
<<<<<<< HEAD
                if (GUILayout.Button(new GUIContent("START SERVER", "Start the MCP server on the configured port"), GUILayout.Height(40))) StartServer();
            }
            else
            {
                if (GUILayout.Button(new GUIContent("STOP SERVER", "Stop the running MCP server"), GUILayout.Height(40))) StopServer();
=======
<<<<<<< HEAD
                if (GUILayout.Button(new GUIContent("START SERVER", "Starts the MCP server to allow AI connections via port " + _port), GUILayout.Height(40))) StartServer();
            }
            else
            {
                if (GUILayout.Button(new GUIContent("STOP SERVER", "Stops the currently running MCP server"), GUILayout.Height(40))) StopServer();
=======
<<<<<<< HEAD
                if (GUILayout.Button(new GUIContent("START SERVER", "Start the MCP server to allow external tools to connect"), GUILayout.Height(40))) StartServer();
            }
            else
            {
                if (GUILayout.Button(new GUIContent("STOP SERVER", "Stop the MCP server and close the port"), GUILayout.Height(40))) StopServer();
=======
                if (GUILayout.Button(new GUIContent("START SERVER", "Starts the MCP server on port " + _port), GUILayout.Height(40))) StartServer();
            }
            else
            {
                if (GUILayout.Button(new GUIContent("STOP SERVER", "Stops the currently running MCP server"), GUILayout.Height(40))) StopServer();
>>>>>>> origin/main
>>>>>>> origin/main
>>>>>>> origin/main
>>>>>>> origin/main
            }

            EditorGUILayout.Space();
            DrawCliIntegration();
        }

        private void DrawServerStatusBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                string status = _isRunning ? "RUNNING" : "STOPPED";
                GUI.color = _isRunning ? Color.green : Color.red;
                GUILayout.Label(new GUIContent($"● {status}", _isRunning ? "Server is running and listening for connections." : "Server is currently stopped."), EditorStyles.boldLabel);
                GUI.color = Color.white;
                GUILayout.FlexibleSpace();
                GUILayout.Label(new GUIContent($"Port: {_port}", "The port the server is listening on. Can be changed in Project Settings."));
                GUILayout.Space(10);

<<<<<<< HEAD
                bool justCopied = EditorApplication.timeSinceStartup - _lastUrlCopyTime < 2.0;
                string btnText = justCopied ? "Copied! ✔" : "Copy URL";
                if (GUILayout.Button(new GUIContent(btnText, "Copy the server URL to clipboard"), EditorStyles.miniButton))
=======
                bool recentlyCopied = EditorApplication.timeSinceStartup - _lastCopyTime < 2.0;
                string copyText = recentlyCopied ? "Copied!" : "Copy URL";
                string tooltip = recentlyCopied ? "URL is in your clipboard" : "Copy the server URL to clipboard";

                if (GUILayout.Button(new GUIContent(copyText, tooltip), EditorStyles.miniButton))
>>>>>>> origin/main
                {
                    EditorGUIUtility.systemCopyBuffer = $"http://localhost:{_port}";
                    _lastUrlCopyTime = EditorApplication.timeSinceStartup;
                    Debug.Log($"[MCP] Server URL copied to clipboard: http://localhost:{_port}");
<<<<<<< HEAD
                    ShowNotification(new GUIContent("URL Copied!"));
=======
                    _lastCopyTime = EditorApplication.timeSinceStartup;
>>>>>>> origin/main
                }

                if (recentlyCopied) Repaint();
            }
        }

        private void DrawCliIntegration()
        {
            GUILayout.Label("Gemini CLI Integration", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label($"Status: {_cliStatusMessage}");
<<<<<<< HEAD
                if (GUILayout.Button(new GUIContent("Refresh", "Refresh the CLI link status"), GUILayout.Width(60))) CheckCliLinkStatus();
            }

<<<<<<< HEAD
            if (GUILayout.Button(new GUIContent("Link to Gemini CLI", "Registers this server instance with the Gemini CLI tool."), GUILayout.Height(30)))
=======
            if (GUILayout.Button(new GUIContent("Link to Gemini CLI", "Configure Gemini CLI to use this Unity instance"), GUILayout.Height(30)))
=======
<<<<<<< HEAD
                if (GUILayout.Button(new GUIContent("Refresh", "Check the current link status with Gemini CLI."), GUILayout.Width(60))) CheckCliLinkStatus();
            }

            if (GUILayout.Button(new GUIContent("Link to Gemini CLI", "Registers this Unity project with your local Gemini CLI installation."), GUILayout.Height(30)))
=======
                if (GUILayout.Button(new GUIContent("Refresh", "Check the status of the Gemini CLI integration"), GUILayout.Width(60))) CheckCliLinkStatus();
            }

            if (GUILayout.Button(new GUIContent("Link to Gemini CLI", "Configure the Gemini CLI to communicate with this Unity project"), GUILayout.Height(30)))
>>>>>>> origin/main
>>>>>>> origin/main
>>>>>>> origin/main
            {
                MCPCliInstaller.LinkToGemini();
                CheckCliLinkStatus();
            }
        }

        private void DrawToolsTab()
        {
            GUILayout.Label("Developer Tools", EditorStyles.boldLabel);
<<<<<<< HEAD
            if (GUILayout.Button(new GUIContent("Open Test Window", "Opens a separate window to manually test MCP API commands."))) MCPTestWindow.ShowWindow();
            if (GUILayout.Button(new GUIContent("Clear All Logs", "Clears the internal buffer of captured console logs exposed to the MCP client."))) ClearLogs();
=======
<<<<<<< HEAD
            if (GUILayout.Button(new GUIContent("Open Test Window", "Open the manual testing window"))) MCPTestWindow.ShowWindow();
            if (GUILayout.Button(new GUIContent("Clear All Logs", "Clear the internal log buffer"))) ClearLogs();
=======
            if (GUILayout.Button(new GUIContent("Open Test Window", "Open the internal test window for debugging MCP commands"))) MCPTestWindow.ShowWindow();
            if (GUILayout.Button(new GUIContent("Clear All Logs", "Clear all logs from the Unity Console"))) ClearLogs();
>>>>>>> origin/main
>>>>>>> origin/main
        }

        private void DrawVerificationTab()
        {
            GUILayout.Label("API Verification", EditorStyles.boldLabel);
<<<<<<< HEAD
            if (GUILayout.Button(new GUIContent("Run Full API Verification", "Opens the verification window to run a suite of tests against the API.")))
=======
            if (GUILayout.Button(new GUIContent("Run Full API Verification", "Execute a comprehensive test suite for all MCP API methods")))
>>>>>>> origin/main
            {
                // We'll call the method from MCPVerificationWindow directly if possible, or just open it
                GetWindow<MCPVerificationWindow>().Show();
            }
<<<<<<< HEAD
            if (GUILayout.Button(new GUIContent("Verify UI Instruments", "Runs a quick verification of the UI Automation tools."))) UIVerification.Verify();
            if (GUILayout.Button(new GUIContent("Verify MCP Logs", "Runs a quick verification of the Log Capture system."))) LogVerification.Verify();
=======
            if (GUILayout.Button(new GUIContent("Verify UI Instruments", "Test the UI automation and inspection capabilities"))) UIVerification.Verify();
            if (GUILayout.Button(new GUIContent("Verify MCP Logs", "Verify that the log capture system is working correctly"))) LogVerification.Verify();
>>>>>>> origin/main
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