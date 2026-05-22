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
            DrawBridgeSummary();
        }

        private void DrawServerControl()
        {
            var section = NexusEditorUi.Section("Server", "Local HTTP bridge status and lifecycle controls.", "NexusServerSection");
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

            _serverHealthLabel = NexusEditorUi.Label("Health: Waiting for refresh", 11, false, NexusEditorUi.Muted, "NexusServerHealthLabel");
            _serverHealthLabel.style.marginTop = 2;
            panel.Add(_serverHealthLabel);

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

            _restartButton = NexusEditorUi.Button("Restart", () =>
            {
                MCPServer.Stop();
                MCPServer.Start();
                UpdateDynamicState();
            }, "Restart the local server and refresh the active session", false, "NexusRestartButton");
            actions.Add(_restartButton);

            _stopButton = NexusEditorUi.Button("Stop", () =>
            {
                MCPServer.Stop();
                UpdateDynamicState();
            }, "Stop the local server", false, "NexusStopButton");
            actions.Add(_stopButton);

            actions.Add(NexusEditorUi.Button("Copy URL", CopyServerUrl, "Copy the local server URL to clipboard", false, "NexusServerCopyUrlButton"));
            StretchActionButtons(actions, 84);
            panel.Add(actions);

            section.Add(panel);
            _content.Add(section);
        }

        private void DrawBridgeSummary()
        {
            var section = NexusEditorUi.Section("Bridge", "Stable files used by external MCP clients.", "NexusBridgeSection");
            var panel = NexusEditorUi.Panel("NexusBridgePanel");

            _bridgePathLabel = NexusEditorUi.Label("Bridge path: " + MCPCliInstaller.GetDefaultBridgePathForUi(), 11, false, null, "NexusBridgePathLabel");
            _bridgePathLabel.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(_bridgePathLabel);

            var actions = NexusEditorUi.Row(true, "NexusBridgeActions");
            actions.Add(NexusEditorUi.Button("Deploy Bridge", () =>
            {
                if (MCPCliInstaller.DeployBridgeForUi(out string path))
                {
                    ShowNotification(new GUIContent("Bridge deployed"));
                    _bridgePathLabel.text = "Bridge path: " + path;
                }
                UpdateDynamicState();
            }, "Copy the MCP bridge script and support module to the project root", false, "NexusDeployBridgeButton"));

            actions.Add(NexusEditorUi.Button("Open Bridge Folder", () =>
            {
                string path = MCPCliInstaller.GetDefaultBridgePathForUi();
                string folder = File.Exists(path) ? Path.GetDirectoryName(path) : MCPCliInstaller.GetProjectRootForUi();
                EditorUtility.RevealInFinder(folder);
            }, "Open the folder that contains the deployed bridge files", false, "NexusOpenBridgeFolderButton"));
            StretchActionButtons(actions, 140);
            panel.Add(actions);

            section.Add(panel);
            _content.Add(section);
        }

        private static void StretchActionButtons(VisualElement row, int minWidth)
        {
            foreach (var child in row.Children())
            {
                if (!(child is Button button)) continue;
                button.style.flexBasis = 0;
                button.style.flexGrow = 1;
                button.style.flexShrink = 1;
                button.style.minWidth = minWidth;
            }
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
            if (_serverHealthLabel != null) _serverHealthLabel.text = GetServerHealthText();
            if (_bridgePathLabel != null) _bridgePathLabel.text = "Bridge path: " + MCPCliInstaller.GetDefaultBridgePathForUi();

            bool hasError = MCPServer.State == ServerState.Error && !string.IsNullOrEmpty(MCPServer.LastError);
            if (_errorLabel != null)
            {
                _errorLabel.text = hasError ? MCPServer.LastError : string.Empty;
                _errorLabel.style.display = hasError ? DisplayStyle.Flex : DisplayStyle.None;
            }

            bool canStart = MCPServer.State == ServerState.Stopped || MCPServer.State == ServerState.Error || MCPServer.State == ServerState.Attached;
            bool canStop = MCPServer.State == ServerState.Running || MCPServer.State == ServerState.Starting || MCPServer.State == ServerState.Attached || MCPServer.State == ServerState.Error;
            _startButton?.SetEnabled(canStart);
            _restartButton?.SetEnabled(canStop);
            _stopButton?.SetEnabled(canStop);
        }

        private static string GetServerHealthText()
        {
            if (MCPServer.State == ServerState.Running) return "Health: Ready for local MCP clients.";
            if (MCPServer.State == ServerState.Starting) return "Health: Starting listener.";
            if (MCPServer.State == ServerState.Error) return "Health: Error. Check Console logs.";
            return "Health: Stopped. Start the server before connecting a client.";
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
    }
}
