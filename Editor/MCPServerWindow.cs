using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    public partial class MCPServerWindow : EditorWindow
    {
        private string _cliStatusMessage = "Checking link...";
        private string _version = "2.6.0";
        private int _selectedTab = 0;
        private GUIContent[] _tabs;

        [MenuItem("Window/Nexus Unity/Server Control Panel")]
        public static void ShowWindow() => GetWindow<MCPServerWindow>("Nexus Unity");

        private void OnEnable()
        {
            _tabs = new[] {
                new GUIContent("Server", "Server Control Panel"),
                new GUIContent("Tools", "Developer Tools"),
                new GUIContent("Verification", "API Verification")
            };
            titleContent = new GUIContent($"Nexus Unity v{MCPServer.Version}");
            CheckCliLinkStatus();
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
            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUI.enabled = false;
                GUILayout.Label($"v{MCPServer.Version}", EditorStyles.miniLabel);
                GUI.enabled = true;
            }
        }

        private void DrawServerTab()
        {
            GUILayout.Label("Server Control", EditorStyles.boldLabel);
            DrawServerStatusBar();
            EditorGUILayout.Space();
            
            bool canStart = MCPServer.State == ServerState.Stopped || MCPServer.State == ServerState.Error || MCPServer.State == ServerState.Attached;
            bool canStop = MCPServer.State == ServerState.Running || MCPServer.State == ServerState.Starting || MCPServer.State == ServerState.Attached || MCPServer.State == ServerState.Error;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (canStart)
                {
                    string label = MCPServer.State == ServerState.Error ? "RETRY START" : "START SERVER";
                    if (GUILayout.Button(new GUIContent(label, $"Start the local MCP server on port {MCPServer.Port}"), GUILayout.Height(40))) MCPServer.Start();
                }

                if (canStop)
                {
                    if (GUILayout.Button(new GUIContent("STOP / RESET", "Stop the running server or reset the state"), GUILayout.Height(40))) MCPServer.Stop();
                }
            }
            EditorGUILayout.Space();
            DrawCliIntegration();
            DrawResources();
        }

        private void DrawServerStatusBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                string status = MCPServer.State.ToString().ToUpper();
                Color statusColor = Color.white;
                switch (MCPServer.State)
                {
                    case ServerState.Running: statusColor = Color.green; break;
                    case ServerState.Starting: statusColor = Color.yellow; break;
                    case ServerState.Attached: statusColor = Color.cyan; break;
                    case ServerState.Error: statusColor = Color.red; break;
                    default: statusColor = Color.gray; status = "STOPPED"; break;
                }

                GUI.color = statusColor;
                GUILayout.Label(new GUIContent($"● {status}"), EditorStyles.boldLabel);
                GUI.color = Color.white;
                GUILayout.FlexibleSpace();
                GUILayout.Label(new GUIContent($"Port: {MCPServer.Port}"));
                GUILayout.Space(10);
                if (GUILayout.Button(new GUIContent("Copy URL", "Copy Server URL to clipboard"), EditorStyles.miniButton))
                {
                    EditorGUIUtility.systemCopyBuffer = $"http://localhost:{MCPServer.Port}";
                    ShowNotification(new GUIContent("URL Copied to Clipboard"));
                }
            }

            if (MCPServer.State == ServerState.Error && !string.IsNullOrEmpty(MCPServer.LastError))
            {
                EditorGUILayout.HelpBox(MCPServer.LastError, MessageType.Error);
            }
            else if (MCPServer.State == ServerState.Attached)
            {
                EditorGUILayout.HelpBox("This instance is attached to an existing session from another Unity editor instance. Remote server is active.", MessageType.Info);
            }
        }

        private void DrawCliIntegration()
        {
            GUILayout.Label("CLI Integrations", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label($"Status: {_cliStatusMessage}");
                if (GUILayout.Button(new GUIContent("Refresh", "Check the link status to external CLIs"), GUILayout.Width(60))) CheckCliLinkStatus();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Link to Gemini CLI", "Setup integration with the Gemini CLI"), GUILayout.Height(30)))
                {
                    MCPCliInstaller.LinkToGemini();
                    CheckCliLinkStatus();
                }

                if (GUILayout.Button(new GUIContent("Link to Codex CLI", "Setup integration with the Codex CLI"), GUILayout.Height(30)))
                {
                    MCPCliInstaller.LinkToCodex();
                    CheckCliLinkStatus();
                }
            }
        }

        private void DrawResources()
        {
            EditorGUILayout.Space();
            GUILayout.Label("Resources", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUIContent docContent = new GUIContent("Documentation", EditorGUIUtility.IconContent("_Help").image, "Open the general documentation");
                if (GUILayout.Button(docContent, EditorStyles.miniButton)) OpenDocumentation("DOCUMENTATION.MD");

                GUIContent apiContent = new GUIContent("API Reference", EditorGUIUtility.IconContent("TextAsset Icon").image, "Open the API reference documentation");
                if (GUILayout.Button(apiContent, EditorStyles.miniButton)) OpenDocumentation("API_REFERENCE.MD");
            }
        }

        private void OpenDocumentation(string filename)
        {
            var script = MonoScript.FromScriptableObject(this);
            var path = AssetDatabase.GetAssetPath(script);
            if (string.IsNullOrEmpty(path)) return;
            var root = Path.GetDirectoryName(Path.GetDirectoryName(path));
            var docPath = Path.Combine(root, filename);
            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(docPath);
            if (obj != null) AssetDatabase.OpenAsset(obj);
        }

        private void DrawToolsTab()
        {
            GUILayout.Label("Developer Tools", EditorStyles.boldLabel);
            if (GUILayout.Button(new GUIContent("Open Test Window", "Open a test window to verify UI interaction"))) MCPTestWindow.ShowWindow();
            if (GUILayout.Button(new GUIContent("Clear All Logs", "Clear the MCP server logs"))) MCPServer.ClearLogs();
        }

        private void DrawVerificationTab()
        {
            GUILayout.Label("API Verification", EditorStyles.boldLabel);
            if (GUILayout.Button(new GUIContent("Run Full Project Audit", "Audit the project for issues and health"))) ProjectAuditorWrapper.RunAuditMenu();
            if (GUILayout.Button(new GUIContent("Run Full API Verification", "Run all automated API verification tests"))) GetWindow<MCPVerificationWindow>().Show();
            if (GUILayout.Button(new GUIContent("Verify UI", "Run verification tests for UI components"))) UIVerification.Verify();
            if (GUILayout.Button(new GUIContent("Verify Logs", "Run verification tests for log entries"))) LogVerification.Verify();
        }

        private void LoadVersion()
        {
            try {
                var script = MonoScript.FromScriptableObject(this);
                string path = AssetDatabase.GetAssetPath(script);
                string dir = Path.GetDirectoryName(path);
                while (!string.IsNullOrEmpty(dir)) {
                    string pkgPath = Path.Combine(dir, "package.json");
                    if (File.Exists(pkgPath)) {
                        string json = File.ReadAllText(pkgPath);
                        var data = Newtonsoft.Json.Linq.JObject.Parse(json);
                        _version = data["version"]?.ToString() ?? "0.0.0";
                        titleContent = new GUIContent($"Nexus Unity v{_version}");
                        return;
                    }
                    dir = Path.GetDirectoryName(dir);
                }
            } catch { _version = "unknown"; }
        }

        private void CheckCliLinkStatus() { _cliStatusMessage = "Ready to Link"; }
    }
}
