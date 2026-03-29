using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    public partial class MCPServerWindow : EditorWindow
    {
        private string _cliStatusMessage = "Checking link...";
        private string _version = "2.2.0";
        private int _selectedTab = 0;
        private string[] _tabs;

        [MenuItem("Window/Nexus Unity/Server Control Panel")]
        public static void ShowWindow() => GetWindow<MCPServerWindow>("Nexus Unity");

        private void OnEnable()
        {
            _tabs = new[] { "Server", "Tools", "Verification" };
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
            if (!MCPServer.IsRunning)
            {
                if (GUILayout.Button(new GUIContent("START SERVER", $"Start the local MCP server on port {MCPServer.Port}"), GUILayout.Height(40))) MCPServer.Start();
            }
            else
            {
                if (GUILayout.Button(new GUIContent("STOP SERVER", "Stop the running MCP server"), GUILayout.Height(40))) MCPServer.Stop();
            }
            EditorGUILayout.Space();
            DrawCliIntegration();
            DrawResources();
        }

        private void DrawServerStatusBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                string status = MCPServer.IsRunning ? "RUNNING" : "STOPPED";
                GUI.color = MCPServer.IsRunning ? Color.green : Color.red;
                GUILayout.Label(new GUIContent($"● {status}"), EditorStyles.boldLabel);
                GUI.color = Color.white;
                GUILayout.FlexibleSpace();
                GUILayout.Label(new GUIContent($"Port: {MCPServer.Port}"));
                GUILayout.Space(10);
                if (GUILayout.Button(new GUIContent("Copy URL", "Copy the server URL to the clipboard"), EditorStyles.miniButton))
                {
                    EditorGUIUtility.systemCopyBuffer = $"http://localhost:{MCPServer.Port}";
                    ShowNotification(new GUIContent("Copied to clipboard!"));
                }
            }
        }

        private void DrawCliIntegration()
        {
            GUILayout.Label("Gemini CLI Integration", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label($"Status: {_cliStatusMessage}");
                if (GUILayout.Button(new GUIContent("Refresh", "Check the link status with the Gemini CLI"), GUILayout.Width(60))) CheckCliLinkStatus();
            }
            if (GUILayout.Button(new GUIContent("Link to Gemini CLI", "Install and configure the Gemini CLI integration"), GUILayout.Height(30)))
            {
                MCPCliInstaller.LinkToGemini();
                CheckCliLinkStatus();
            }
        }

        private void DrawResources()
        {
            EditorGUILayout.Space();
            GUILayout.Label("Resources", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                if (GUILayout.Button(new GUIContent("Documentation", "Open the Nexus Unity technical documentation"), EditorStyles.miniButton)) OpenDocumentation("DOCUMENTATION.MD");
                if (GUILayout.Button(new GUIContent("API Reference", "Open the Nexus Unity API reference guide"), EditorStyles.miniButton)) OpenDocumentation("API_REFERENCE.MD");
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
            if (GUILayout.Button(new GUIContent("Open Test Window", "Open the UI testing window"))) MCPTestWindow.ShowWindow();
            if (GUILayout.Button(new GUIContent("Clear All Logs", "Clear the server logs"))) MCPServer.ClearLogs();
        }

        private void DrawVerificationTab()
        {
            GUILayout.Label("API Verification", EditorStyles.boldLabel);
            if (GUILayout.Button(new GUIContent("Run Linter", "Run the project auditor linter"))) ProjectAuditor.RunAuditMenu();
            if (GUILayout.Button(new GUIContent("Run Full API Verification", "Open the API verification window"))) GetWindow<MCPVerificationWindow>().Show();
            if (GUILayout.Button(new GUIContent("Verify UI", "Run UI verification tests"))) UIVerification.Verify();
            if (GUILayout.Button(new GUIContent("Verify Logs", "Run log verification tests"))) LogVerification.Verify();
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
