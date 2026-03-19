using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    public partial class MCPServerWindow : EditorWindow
    {
        private double _lastUrlCopyTime = -10.0;
        private string _cliStatusMessage = "Checking link...";
        private string _version = "2.5.0";
        private int _selectedTab = 0;
        private string[] _tabs;

        [MenuItem("Window/Nexus Unity/Server Control Panel")]
        public static void ShowWindow() => GetWindow<MCPServerWindow>("Nexus Unity");

        private void OnEnable()
        {
            _tabs = new[] { "Server", "Tools", "Verification" };
            EditorApplication.update += UpdateCopyFeedback;
            titleContent = new GUIContent($"Nexus Unity v{MCPServer.Version}");
            CheckCliLinkStatus();
        }

        private void OnDisable() => EditorApplication.update -= UpdateCopyFeedback;

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
                bool recentlyCopied = EditorApplication.timeSinceStartup - _lastUrlCopyTime < 2.0;
                if (GUILayout.Button(new GUIContent(recentlyCopied ? "Copied!" : "Copy URL"), EditorStyles.miniButton))
                {
                    EditorGUIUtility.systemCopyBuffer = $"http://localhost:{MCPServer.Port}";
                    _lastUrlCopyTime = EditorApplication.timeSinceStartup;
                }
            }
        }

        private void DrawCliIntegration()
        {
            GUILayout.Label("CLI Integrations", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label($"Status: {_cliStatusMessage}");
                if (GUILayout.Button("Refresh", GUILayout.Width(60))) CheckCliLinkStatus();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Link to Gemini CLI", GUILayout.Height(30)))
                {
                    MCPCliInstaller.LinkToGemini();
                    CheckCliLinkStatus();
                }

                if (GUILayout.Button("Link to Codex CLI", GUILayout.Height(30)))
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
                if (GUILayout.Button("Documentation", EditorStyles.miniButton)) OpenDocumentation("DOCUMENTATION.MD");
                if (GUILayout.Button("API Reference", EditorStyles.miniButton)) OpenDocumentation("API_REFERENCE.MD");
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
            if (GUILayout.Button("Open Test Window")) MCPTestWindow.ShowWindow();
            if (GUILayout.Button("Clear All Logs")) MCPServer.ClearLogs();
        }

        private void DrawVerificationTab()
        {
            GUILayout.Label("API Verification", EditorStyles.boldLabel);
            if (GUILayout.Button("Run Full Project Audit")) ProjectAuditorWrapper.RunAuditMenu();
            if (GUILayout.Button("Run Full API Verification")) GetWindow<MCPVerificationWindow>().Show();
            if (GUILayout.Button("Verify UI")) UIVerification.Verify();
            if (GUILayout.Button("Verify Logs")) LogVerification.Verify();
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
        private void UpdateCopyFeedback() { if (EditorApplication.timeSinceStartup - _lastUrlCopyTime < 2.0) Repaint(); }
    }
}
