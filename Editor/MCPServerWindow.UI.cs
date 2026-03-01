using UnityEditor;
using UnityEngine;
using System.IO;
using System;

namespace UnityMCP.Editor
{
    public partial class MCPServerWindow
    {
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

            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUI.enabled = false;
                GUILayout.Label($"v{_version}", EditorStyles.miniLabel);
                GUI.enabled = true;
            }
        }

        private void DrawServerTab()
        {
            GUILayout.Label("Server Control", EditorStyles.boldLabel);
            DrawServerStatusBar();

            EditorGUILayout.Space();

            if (!_isRunning)
            {
                if (GUILayout.Button(new GUIContent("START SERVER", $"Start the local MCP server on port {_port}"), GUILayout.Height(40))) StartServer();
            }
            else
            {
                if (GUILayout.Button(new GUIContent("STOP SERVER", "Stop the running MCP server"), GUILayout.Height(40))) StopServer();
            }

            EditorGUILayout.Space();
            DrawCliIntegration();

            DrawResources();
        }

        private void DrawResources()
        {
            EditorGUILayout.Space();
            GUILayout.Label("Resources", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                if (GUILayout.Button(new GUIContent("Documentation", "Open the comprehensive documentation (DOCUMENTATION.MD)"), EditorStyles.miniButton))
                    OpenDocumentation("DOCUMENTATION.MD");
                if (GUILayout.Button(new GUIContent("API Reference", "Open the API reference guide (API_REFERENCE.MD)"), EditorStyles.miniButton))
                    OpenDocumentation("API_REFERENCE.MD");
            }
        }

        private void OpenDocumentation(string filename)
        {
            var script = MonoScript.FromScriptableObject(this);
            var path = AssetDatabase.GetAssetPath(script);
            if (string.IsNullOrEmpty(path)) return;

            var root = Path.GetDirectoryName(Path.GetDirectoryName(path));
            var docPath = Path.Combine(root, filename).Replace("", "/");
            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(docPath);

            if (obj != null) AssetDatabase.OpenAsset(obj);
            else Debug.LogError($"[MCP] Could not find documentation at {docPath}");
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

                bool recentlyCopied = EditorApplication.timeSinceStartup - _lastUrlCopyTime < 2.0;
                string copyText = recentlyCopied ? "Copied!" : "Copy URL";
                string tooltip = recentlyCopied ? "URL is in your clipboard" : "Copy the server URL to clipboard";

                if (GUILayout.Button(new GUIContent(copyText, tooltip), EditorStyles.miniButton))
                {
                    EditorGUIUtility.systemCopyBuffer = $"http://localhost:{_port}";
                    ShowNotification(new GUIContent("Server URL copied to clipboard"));
                    _lastUrlCopyTime = EditorApplication.timeSinceStartup;
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
                if (GUILayout.Button(new GUIContent("Refresh", "Refresh the CLI link status"), GUILayout.Width(60)))
                {
                    CheckCliLinkStatus();
                    ShowNotification(new GUIContent("Refreshing CLI status..."));
                }
            }

            if (GUILayout.Button(new GUIContent("Link to Gemini CLI", "Connects your terminal to Unity via the Gemini CLI"), GUILayout.Height(30)))
            {
                MCPCliInstaller.LinkToGemini();
                CheckCliLinkStatus();
                ShowNotification(new GUIContent("Linking to Gemini CLI..."));
            }
        }

        private void DrawToolsTab()
        {
            GUILayout.Label("Developer Tools", EditorStyles.boldLabel);
            if (GUILayout.Button(new GUIContent("Open Test Window", "Open the manual verification and testing window")))
            {
                MCPTestWindow.ShowWindow();
                ShowNotification(new GUIContent("Opening Test Window..."));
            }
            if (GUILayout.Button(new GUIContent("Clear All Logs", "Clear the internal log history captured by the server")))
            {
                ClearLogs();
                ShowNotification(new GUIContent("Logs cleared"));
            }
        }

        private void DrawVerificationTab()
        {
            GUILayout.Label("API Verification", EditorStyles.boldLabel);
            if (GUILayout.Button(new GUIContent("Run Linter (whole proj)", "Run a deterministic C# audit using Roslyn to enforce high code quality standards")))
            {
                ShowNotification(new GUIContent("Running Linter..."));
                ProjectAuditor.RunAuditMenu();
            }
            
            EditorGUILayout.Space();
            if (GUILayout.Button(new GUIContent("Run Full API Verification", "Open the API Verification window to run tests manually")))
            {
                // We'll call the method from MCPVerificationWindow directly if possible, or just open it
                GetWindow<MCPVerificationWindow>().Show();
                ShowNotification(new GUIContent("Opening Verification Window..."));
            }
            if (GUILayout.Button(new GUIContent("Verify UI Instruments", "Run UI verification tests")))
            {
                UIVerification.Verify();
                ShowNotification(new GUIContent("Running UI Verification..."));
            }
            if (GUILayout.Button(new GUIContent("Verify MCP Logs", "Run log verification tests")))
            {
                LogVerification.Verify();
                ShowNotification(new GUIContent("Running Log Verification..."));
            }
        }
    }
}
