using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    public partial class MCPServerWindow
    {
        private void DrawServerTab()
        {
            DrawServerControl();
            DrawCliIntegration();
            DrawResources();
            DrawSessionSummary();
        }

        private void DrawServerControl()
        {
            var section = NexusEditorUi.Section("Server Control", "Local HTTP bridge status and lifecycle controls.", "NexusServerSection");
            var panel = NexusEditorUi.Panel("NexusServerControl");

            var statusRow = NexusEditorUi.Row(true, "NexusServerStatusRow");
            _stateLabel = NexusEditorUi.Label("State: Unknown", 12, true, null, "NexusServerStateLabel");
            _stateLabel.style.marginRight = 12;
            _stateLabel.style.marginBottom = 4;
            statusRow.Add(_stateLabel);

            _sessionLabel = NexusEditorUi.Label("Session: -", 11, false, NexusEditorUi.Muted, "NexusSessionLabel");
            _sessionLabel.style.marginRight = 12;
            _sessionLabel.style.marginBottom = 4;
            statusRow.Add(_sessionLabel);

            _editorStateLabel = NexusEditorUi.Label("Editor: Idle", 11, false, NexusEditorUi.Muted, "NexusEditorStateLabel");
            _editorStateLabel.style.marginBottom = 4;
            statusRow.Add(_editorStateLabel);
            panel.Add(statusRow);

            _errorLabel = NexusEditorUi.Label(string.Empty, 11, true, new Color(1f, 0.45f, 0.45f), "NexusErrorLabel");
            _errorLabel.style.marginTop = 4;
            _errorLabel.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(_errorLabel);

            var actions = NexusEditorUi.Row(true, "NexusServerActions");
            _startButton = NexusEditorUi.Button("Start Server", () =>
            {
                MCPServer.Start();
                UpdateDynamicState();
            }, $"Start the local server on port {MCPServer.Port}", true, "NexusStartButton");
            actions.Add(_startButton);

            _stopButton = NexusEditorUi.Button("Stop / Reset", () =>
            {
                MCPServer.Stop();
                UpdateDynamicState();
            }, "Stop the running server and reset the active server state", false, "NexusStopButton");
            actions.Add(_stopButton);
            panel.Add(actions);

            section.Add(panel);
            _content.Add(section);
        }

        private void DrawCliIntegration()
        {
            var section = NexusEditorUi.Section("CLI Integrations", "Link external coding assistants to this Unity project.", "NexusCliSection");
            var status = NexusEditorUi.Panel("NexusCliStatusPanel");
            var statusRow = NexusEditorUi.Row(true, "NexusCliStatusRow");
            _cliStatusLabel = NexusEditorUi.Label($"Status: {_cliStatusMessage}", 12, false, null, "NexusCliStatusLabel");
            _cliStatusLabel.style.flexGrow = 1;
            _cliStatusLabel.style.minWidth = 180;
            statusRow.Add(_cliStatusLabel);
            statusRow.Add(NexusEditorUi.Button("Refresh", () =>
            {
                CheckCliLinkStatus();
                UpdateDynamicState();
            }, "Check current CLI installation and link status", false, "NexusRefreshCliButton"));
            status.Add(statusRow);
            section.Add(status);

            var actions = NexusEditorUi.Row(true, "NexusCliActions");
            actions.Add(NexusEditorUi.Button("Link to Gemini CLI", () =>
            {
                MCPCliInstaller.LinkToGemini();
                CheckCliLinkStatus();
                UpdateDynamicState();
            }, "Install and link Gemini CLI tools to this Unity project", false, "NexusLinkGeminiButton"));

            actions.Add(NexusEditorUi.Button("Link to Codex CLI", () =>
            {
                MCPCliInstaller.LinkToCodex();
                CheckCliLinkStatus();
                UpdateDynamicState();
            }, "Install and link Codex CLI tools to this Unity project", false, "NexusLinkCodexButton"));

            actions.Add(NexusEditorUi.Button("Link to Anthropic Claude", () =>
            {
                MCPCliInstaller.LinkToAnthropic();
                CheckCliLinkStatus();
                UpdateDynamicState();
            }, "Install and link Anthropic Claude Desktop to this Unity project", false, "NexusLinkClaudeButton"));

            actions.Add(NexusEditorUi.Button("Link to Antigravity CLI", () =>
            {
                MCPCliInstaller.LinkToAntigravity();
                CheckCliLinkStatus();
                UpdateDynamicState();
            }, "Install and link Antigravity CLI to this Unity project", false, "NexusLinkAntigravityButton"));
            section.Add(actions);
            _content.Add(section);
        }

        private void DrawResources()
        {
            var section = NexusEditorUi.Section("Resources", "Package documentation for users and contributors.", "NexusResourcesSection");
            var actions = NexusEditorUi.Row(true, "NexusResources");
            actions.Add(NexusEditorUi.Button("Documentation", () => OpenDocumentation("DOCUMENTATION.MD"), "Open project documentation", false, "NexusDocumentationButton"));
            actions.Add(NexusEditorUi.Button("API Reference", () => OpenDocumentation("API_REFERENCE.MD"), "Open API reference documentation", false, "NexusApiReferenceButton"));
            section.Add(actions);
            _content.Add(section);
        }

        private void DrawSessionSummary()
        {
            var section = NexusEditorUi.Section("Status", null, "NexusStatusSummary");
            var panel = NexusEditorUi.Panel("NexusStatusSummaryPanel");
            panel.Add(NexusEditorUi.Label("Use the Tools tab for diagnostics and verification helpers.", 11, false, NexusEditorUi.Muted, "NexusSummaryText"));
            section.Add(panel);
            _content.Add(section);
        }

        private void DrawToolsTab()
        {
            var section = NexusEditorUi.Section("Developer Tools", "Compact diagnostics and package utilities.", "NexusToolsSection");
            var actions = NexusEditorUi.Row(true, "NexusToolsActions");
            actions.Add(NexusEditorUi.Button("Open Test Window", () =>
            {
                MCPTestWindow.ShowWindow();
            }, "Open UI automation test window", true, "NexusOpenTestWindowButton"));
            actions.Add(NexusEditorUi.Button("Test Codex Link", CodexLinkTester.TestLink, "Run the Codex CLI link test from the main Nexus Unity window", false, "NexusTestCodexLinkButton"));
            actions.Add(NexusEditorUi.Button("Clear All Logs", () =>
            {
                MCPServer.ClearLogs();
                _content.Clear();
                DrawToolsTab();
            }, "Clear the MCP server log history", false, "NexusClearLogsButton"));
            section.Add(actions);

            var logsPanel = NexusEditorUi.Panel("NexusLogSummary");
            var logs = MCPServer.GetLogs(6, null, null);
            logsPanel.Add(NexusEditorUi.Label($"Captured logs: {logs.Count}", 12, true, null, "NexusLogCountLabel"));
            if (logs.Count == 0)
            {
                logsPanel.Add(NexusEditorUi.Label("No captured logs.", 11, false, NexusEditorUi.Muted));
            }
            else
            {
                foreach (var log in logs)
                {
                    string message = string.IsNullOrEmpty(log.Message) ? "(empty)" : log.Message.Replace('\n', ' ');
                    if (message.Length > 120) message = message.Substring(0, 117) + "...";
                    var line = NexusEditorUi.Label($"{log.Timestamp} {log.Type}: {message}", 10, false, NexusEditorUi.Muted);
                    line.style.whiteSpace = WhiteSpace.Normal;
                    logsPanel.Add(line);
                }
            }
            section.Add(logsPanel);
            _content.Add(section);
        }

        private void DrawVerificationTab()
        {
            var section = NexusEditorUi.Section("API Verification", "Run audit and verification tools without leaving the Nexus panel.", "NexusVerificationSection");
            var actions = NexusEditorUi.Row(true, "NexusVerificationActions");
            actions.Add(NexusEditorUi.Button("Run Full Project Audit", ProjectAuditorWrapper.RunAuditMenu, "Scan the current project for health and structure issues", false, "NexusRunProjectAuditButton"));
            actions.Add(NexusEditorUi.Button("Run API Verification", () =>
            {
                MCPVerificationWindow.ShowWindow();
            }, "Open the API verification window", true, "NexusOpenApiVerificationButton"));
            actions.Add(NexusEditorUi.Button("Verify UI", UIVerification.Verify, "Run UI Toolkit interaction verification", false, "NexusVerifyUiButton"));
            actions.Add(NexusEditorUi.Button("Verify Logs", LogVerification.Verify, "Run log capture and parsing verification", false, "NexusVerifyLogsButton"));
            section.Add(actions);

            var panel = NexusEditorUi.Panel("NexusVerificationHint");
            panel.Add(NexusEditorUi.Label("Verification runs may mutate temporary editor state. Keep long stress runs outside quick contributor hooks.", 11, false, NexusEditorUi.Muted));
            section.Add(panel);
            _content.Add(section);
        }

        private void CopyServerUrl()
        {
            EditorGUIUtility.systemCopyBuffer = $"http://localhost:{MCPServer.Port}";
            ShowNotification(new GUIContent("URL copied"));
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

        private void UpdateDynamicState()
        {
            var color = GetStateColor(MCPServer.State);
            var status = MCPServer.State == ServerState.Stopped ? "STOPPED" : MCPServer.State.ToString().ToUpperInvariant();
            NexusEditorUi.SetPill(_statusPill, status, color);

            if (_portLabel != null) _portLabel.text = $"Port: {MCPServer.Port}";
            if (_footerLabel != null) _footerLabel.text = $"v{MCPServer.Version}";
            if (_stateLabel != null) _stateLabel.text = $"State: {MCPServer.State}";
            if (_sessionLabel != null) _sessionLabel.text = $"Session: {GetShortSessionId()}";
            if (_editorStateLabel != null) _editorStateLabel.text = $"Editor: {GetEditorStateText()}";
            if (_cliStatusLabel != null) _cliStatusLabel.text = $"Status: {_cliStatusMessage}";

            bool hasError = MCPServer.State == ServerState.Error && !string.IsNullOrEmpty(MCPServer.LastError);
            if (_errorLabel != null)
            {
                _errorLabel.text = hasError ? MCPServer.LastError : string.Empty;
                _errorLabel.style.display = hasError ? DisplayStyle.Flex : DisplayStyle.None;
            }

            bool canStart = MCPServer.State == ServerState.Stopped || MCPServer.State == ServerState.Error || MCPServer.State == ServerState.Attached;
            bool canStop = MCPServer.State == ServerState.Running || MCPServer.State == ServerState.Starting || MCPServer.State == ServerState.Attached || MCPServer.State == ServerState.Error;
            _startButton?.SetEnabled(canStart);
            _stopButton?.SetEnabled(canStop);
        }

        private static Color GetStateColor(ServerState state)
        {
            switch (state)
            {
                case ServerState.Running: return new Color(0.12f, 0.62f, 0.22f);
                case ServerState.Starting: return new Color(0.92f, 0.66f, 0.12f);
                case ServerState.Attached: return new Color(0.12f, 0.50f, 0.72f);
                case ServerState.Error: return new Color(0.78f, 0.18f, 0.18f);
                default: return new Color(0.42f, 0.42f, 0.42f);
            }
        }

        private static string GetShortSessionId()
        {
            var session = MCPServer.SessionId;
            if (string.IsNullOrEmpty(session)) return "-";
            return session.Length <= 8 ? session : session.Substring(0, 8);
        }

        private static string GetEditorStateText()
        {
            if (EditorApplication.isCompiling) return "Compiling";
            if (EditorApplication.isUpdating) return "Updating";
            if (EditorApplication.isPlayingOrWillChangePlaymode) return EditorApplication.isPlaying ? "Playing" : "Entering Play Mode";
            return "Idle";
        }

        private void CheckCliLinkStatus()
        {
            _cliStatusMessage = "Ready to Link";
        }
    }
}
