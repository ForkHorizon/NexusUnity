using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    public partial class MCPServerWindow : EditorWindow
    {
        private string _cliStatusMessage = "Checking link...";
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
                GUILayout.Label(new GUIContent($"v{MCPServer.Version}", "Current Server Version"), EditorStyles.miniLabel);
                GUI.enabled = true;
            }
        }

        private void DrawServerTab()
        {
            GUILayout.Label(new GUIContent("Server Control", "Controls for starting and stopping the local server"), EditorStyles.boldLabel);
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
            GUILayout.Label(new GUIContent("CLI Integrations", "Setup CLI tools for external systems"), EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(new GUIContent($"Status: {_cliStatusMessage}", "Current linking status of CLI tools"));
                if (GUILayout.Button(new GUIContent("Refresh", "Check the current CLI installation and link status"), GUILayout.Width(60))) CheckCliLinkStatus();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Link to Gemini CLI", "Install and link the Gemini CLI tools to this Unity project"), GUILayout.Height(30)))
                {
                    MCPCliInstaller.LinkToGemini();
                    CheckCliLinkStatus();
                }

                if (GUILayout.Button(new GUIContent("Link to Codex CLI", "Install and link the Codex CLI tools to this Unity project"), GUILayout.Height(30)))
                {
                    MCPCliInstaller.LinkToCodex();
                    CheckCliLinkStatus();
                }

                if (GUILayout.Button(new GUIContent("Link to Anthropic Claude", "Install and link Anthropic Claude Desktop to this Unity project"), GUILayout.Height(30)))
                {
                    MCPCliInstaller.LinkToAnthropic();
                }

                if (GUILayout.Button(new GUIContent("Link to Antigravity CLI", "Install and link the Antigravity CLI to this Unity project"), GUILayout.Height(30)))
                {
                    MCPCliInstaller.LinkToAntigravity();
                }
            }
        }

        private void DrawResources()
        {
            EditorGUILayout.Space();
            GUILayout.Label(new GUIContent("Resources", "Helpful project resources and documentation"), EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUIContent docContent = EditorGUIUtility.IconContent("_Help");
                docContent.text = " Documentation";
                docContent.tooltip = "Open project documentation";

                GUIContent apiContent = EditorGUIUtility.IconContent("TextAsset Icon");
                apiContent.text = " API Reference";
                apiContent.tooltip = "Open API reference documentation";

                if (GUILayout.Button(docContent, EditorStyles.miniButton)) OpenDocumentation("DOCUMENTATION.MD");
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
            GUILayout.Label(new GUIContent("Developer Tools", "Additional utilities for server management and testing"), EditorStyles.boldLabel);
            if (GUILayout.Button(new GUIContent("Open Test Window", "Open a test window with UI elements for verification testing"))) MCPTestWindow.ShowWindow();
            if (GUILayout.Button(new GUIContent("Clear All Logs", "Clear the MCP server log history"))) MCPServer.ClearLogs();
        }

        private void DrawVerificationTab()
        {
            GUILayout.Label(new GUIContent("API Verification", "Run comprehensive API tests"), EditorStyles.boldLabel);
            if (GUILayout.Button(new GUIContent("Run Full Project Audit", "Scan the current scene for project health and structure issues"))) ProjectAuditorWrapper.RunAuditMenu();
            if (GUILayout.Button(new GUIContent("Run Full API Verification", "Open the comprehensive API verification test suite window"))) MCPVerificationWindow.ShowWindow();
            if (GUILayout.Button(new GUIContent("Verify UI", "Run automated UI toolkit interaction tests"))) UIVerification.Verify();
            if (GUILayout.Button(new GUIContent("Verify Logs", "Run automated log capture and parsing tests"))) LogVerification.Verify();
        }

        private void CheckCliLinkStatus() { _cliStatusMessage = "Ready to Link"; }
    }
}
