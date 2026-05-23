using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    public partial class MCPServerWindow
    {
        private void DrawResourcesTab()
        {
            DrawResourceLinks();
            DrawAdvancedDiagnostics();
        }

        private void DrawResourceLinks()
        {
            var section = NexusEditorUi.Section("Resources", "Documentation and package files.", "NexusResourcesSection");
            var panel = NexusEditorUi.Panel("NexusResourcesPanel");

            _resourceStatusLabel = NexusEditorUi.Label("Open package documentation or inspect the installed package folder.", 11, false, NexusEditorUi.Muted, "NexusResourceStatusLabel");
            panel.Add(_resourceStatusLabel);

            var actions = NexusEditorUi.Row(true, "NexusResources");
            actions.Add(NexusEditorUi.Button("Documentation", () => OpenDocumentation("DOCUMENTATION.MD"), "Open the technical guide.", false, "NexusDocumentationButton"));
            actions.Add(NexusEditorUi.Button("API Reference", () => OpenDocumentation("API_REFERENCE.MD"), "Open the public API reference.", false, "NexusApiReferenceButton"));
            actions.Add(NexusEditorUi.Button("Changelog", () => OpenDocumentation("CHANGELOG.md"), "Open the package changelog.", false, "NexusChangelogButton"));
            actions.Add(NexusEditorUi.Button("Open Package Folder", OpenPackageFolder, "Reveal the Nexus Unity package folder.", false, "NexusOpenPackageFolderButton"));
            panel.Add(actions);

            section.Add(panel);
            _content.Add(section);
        }

        private void DrawAdvancedDiagnostics()
        {
            var section = NexusEditorUi.Section("Advanced / Diagnostics", "Internal verification tools for maintainers.", "NexusAdvancedSection");
            var foldout = new Foldout
            {
                name = "NexusAdvancedDiagnostics",
                text = "Show diagnostics",
                value = false
            };
            foldout.tooltip = "Open test and verification tools. These are hidden from the default setup workflow.";

            var actions = NexusEditorUi.Row(true, "NexusDiagnosticActions");
            actions.Add(NexusEditorUi.Button("Open Test Window", () => MCPTestWindow.ShowWindow(), "Open the UI automation test window.", false, "NexusOpenTestWindowButton"));
            actions.Add(NexusEditorUi.Button("API Verification", MCPVerificationWindow.ShowWindow, "Open the API verification window.", true, "NexusOpenApiVerificationButton"));
            actions.Add(NexusEditorUi.Button("Run Project Audit", ProjectAuditorWrapper.RunAuditMenu, "Scan the current project for health issues.", false, "NexusRunProjectAuditButton"));
            actions.Add(NexusEditorUi.Button("Verify UI", UIVerification.Verify, "Run UI Toolkit interaction verification.", false, "NexusVerifyUiButton"));
            actions.Add(NexusEditorUi.Button("Verify Logs", LogVerification.Verify, "Run log capture and parsing verification.", false, "NexusVerifyLogsButton"));
            actions.Add(NexusEditorUi.Button("Test Codex Link", CodexLinkTester.TestLink, "Run the Codex CLI link diagnostic.", false, "NexusTestCodexLinkButton"));
            actions.Add(NexusEditorUi.Button("Clear Logs", ClearServerLogsAndRefresh, "Clear the MCP server log history.", false, "NexusClearLogsButton"));
            foldout.Add(actions);

            foldout.Add(BuildLogSummary());
            section.Add(foldout);
            _content.Add(section);
        }

        private VisualElement BuildLogSummary()
        {
            var logsPanel = NexusEditorUi.Panel("NexusLogSummary");
            var logs = MCPServer.GetLogs(6, null, null);
            logsPanel.Add(NexusEditorUi.Label("Captured logs: " + logs.Count, 12, true, null, "NexusLogCountLabel"));
            if (logs.Count == 0)
            {
                logsPanel.Add(NexusEditorUi.Label("No captured logs.", 11, false, NexusEditorUi.Muted, "NexusNoLogsLabel"));
                return logsPanel;
            }

            foreach (var log in logs)
            {
                string message = string.IsNullOrEmpty(log.Message) ? "(empty)" : log.Message.Replace('\n', ' ');
                if (message.Length > 120) message = message.Substring(0, 117) + "...";
                var line = NexusEditorUi.Label(log.Timestamp + " " + log.Type + ": " + message, 10, false, NexusEditorUi.Muted);
                line.style.whiteSpace = WhiteSpace.Normal;
                logsPanel.Add(line);
            }

            return logsPanel;
        }

        private void ClearServerLogsAndRefresh()
        {
            MCPServer.ClearLogs();
            if (_resourceStatusLabel != null) _resourceStatusLabel.text = "Logs cleared.";
            _content.Clear();
            DrawResourcesTab();
        }

        private void OpenPackageFolder()
        {
            var script = MonoScript.FromScriptableObject(this);
            var path = AssetDatabase.GetAssetPath(script);
            if (string.IsNullOrEmpty(path)) return;
            var packageRoot = Path.GetDirectoryName(Path.GetDirectoryName(path));
            EditorUtility.RevealInFinder(packageRoot);
            if (_resourceStatusLabel != null) _resourceStatusLabel.text = "Opened package folder.";
        }
    }
}
