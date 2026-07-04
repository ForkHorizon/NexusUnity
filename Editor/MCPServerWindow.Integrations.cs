using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    public partial class MCPServerWindow
    {
        private void DrawIntegrationsTab()
        {
            var section = NexusEditorUi.Section("Integrations", "Connect Nexus Unity to MCP clients.", "NexusIntegrationsSection");
            var toolbar = NexusEditorUi.Panel("NexusIntegrationsToolbar");
            var toolbarRow = NexusEditorUi.Row(true, "NexusIntegrationsToolbarRow");
            _integrationStatusLabel = NexusEditorUi.Label("Choose a client card to set up MCP access.", 11, false, NexusEditorUi.Muted, "NexusIntegrationStatusLabel");
            _integrationStatusLabel.style.flexGrow = 1;
            _integrationStatusLabel.style.minWidth = 180;
            toolbarRow.Add(_integrationStatusLabel);
            toolbarRow.Add(NexusEditorUi.Button("Refresh", DrawIntegrationsTabRefresh, "Refresh integration status.", false, "NexusRefreshIntegrationsButton"));
            toolbar.Add(toolbarRow);
            section.Add(toolbar);

            var cards = NexusEditorUi.Row(true, "NexusIntegrationCards");
            cards.style.alignItems = Align.Stretch;
            foreach (var client in NexusMcpConfigGenerator.BuildAllForCurrentProject())
            {
                cards.Add(BuildIntegrationCard(client));
            }

            section.Add(cards);
            _content.Add(section);
        }

        private void DrawIntegrationsTabRefresh()
        {
            _content.Clear();
            DrawIntegrationsTab();
            UpdateDynamicState();
        }

        private VisualElement BuildIntegrationCard(NexusMcpClientInfo client)
        {
            var card = NexusEditorUi.Panel("NexusIntegrationCard" + client.ElementKey);
            card.style.flexBasis = 260;
            card.style.flexGrow = 1;
            card.style.flexShrink = 1;
            card.style.minWidth = 240;
            card.style.marginRight = 6;

            var titleRow = NexusEditorUi.Row(true, "NexusIntegrationTitle" + client.ElementKey);
            var title = NexusEditorUi.Label(client.DisplayName, 13, true, null, "NexusIntegrationName" + client.ElementKey);
            title.style.flexGrow = 1;
            title.style.minWidth = 130;
            titleRow.Add(title);
            titleRow.Add(NexusEditorUi.Pill(GetIntegrationStatusText(client.Status), GetIntegrationStatusColor(client.Status), "NexusIntegrationStatus" + client.ElementKey));
            card.Add(titleRow);

            var detail = NexusEditorUi.Label(client.StatusDetail, 10, false, NexusEditorUi.Muted, "NexusIntegrationDetail" + client.ElementKey);
            detail.style.marginTop = 4;
            card.Add(detail);

            var instruction = NexusEditorUi.Label(client.Instruction, 10, false, null, "NexusIntegrationInstruction" + client.ElementKey);
            instruction.style.marginTop = 4;
            card.Add(instruction);

            var path = NexusEditorUi.Label("Config: " + client.ConfigPath, 10, false, NexusEditorUi.Muted, "NexusIntegrationConfigPath" + client.ElementKey);
            path.style.marginTop = 4;
            card.Add(path);

            var actions = NexusEditorUi.Row(true, "NexusIntegrationActions" + client.ElementKey);
            var autoButton = NexusEditorUi.Button("Auto Setup", () => AutoSetupIntegration(client),
                client.SupportsAutoSetup ? "Write the Nexus Unity MCP config for this client." : client.AutoSetupDisabledReason,
                true, "NexusIntegrationAuto" + client.ElementKey);
            StyleIntegrationButton(autoButton);
            autoButton.SetEnabled(client.SupportsAutoSetup);
            actions.Add(autoButton);

            var copyButton = NexusEditorUi.Button("Copy Config", () => CopyIntegrationConfig(client),
                "Copy the MCP config snippet for this client.", false, "NexusIntegrationCopy" + client.ElementKey);
            StyleIntegrationButton(copyButton);
            actions.Add(copyButton);

            var openButton = NexusEditorUi.Button("Open Config", () => OpenIntegrationConfigLocation(client),
                "Open the folder that contains this client's config.", false, "NexusIntegrationOpen" + client.ElementKey);
            StyleIntegrationButton(openButton);
            bool canOpen = IsFileBackedConfig(client);
            openButton.SetEnabled(canOpen);
            if (!canOpen) openButton.tooltip = "This integration is managed by CLI or manual copy/paste.";
            actions.Add(openButton);

            card.Add(actions);
            return card;
        }

        private void AutoSetupIntegration(NexusMcpClientInfo client)
        {
            if (!client.SupportsAutoSetup)
            {
                SetIntegrationMessage(client.AutoSetupDisabledReason);
                return;
            }

            if (!MCPCliInstaller.DeployBridgeForUi(out string bridgePath))
            {
                SetIntegrationMessage("Bridge deploy failed. Check Console logs.");
                return;
            }

            var refreshed = NexusMcpConfigGenerator.BuildAllForCurrentProject().First(item => item.Kind == client.Kind);
            refreshed.BridgePath = bridgePath.Replace("\\", "/");

            if (refreshed.CustomAutoSetup != null)
            {
                refreshed.CustomAutoSetup();
                DrawIntegrationsTabRefresh();
                SetIntegrationMessage(refreshed.DisplayName + " setup finished. Restart " + refreshed.DisplayName + " sessions to use the redeployed bridge.");
                return;
            }

            var result = NexusMcpConfigGenerator.WriteConfig(refreshed);
            string message = result.Success ? "Config updated: " + result.ConfigPath : "Setup failed: " + result.Message;
            if (result.Success && !string.IsNullOrEmpty(result.BackupPath))
            {
                message += " Backup: " + result.BackupPath;
            }
            if (result.Success)
            {
                message += " Restart " + refreshed.DisplayName + " sessions to use the redeployed bridge.";
            }

            ShowNotification(new GUIContent(result.Success ? "Integration updated" : "Integration failed"));
            DrawIntegrationsTabRefresh();
            SetIntegrationMessage(message);
        }

        private void CopyIntegrationConfig(NexusMcpClientInfo client)
        {
            EditorGUIUtility.systemCopyBuffer = client.ConfigText;
            SetIntegrationMessage("Copied " + client.DisplayName + " config.");
            ShowNotification(new GUIContent("Config copied"));
        }

        private void OpenIntegrationConfigLocation(NexusMcpClientInfo client)
        {
            if (!IsFileBackedConfig(client))
            {
                SetIntegrationMessage("No config file location is available for " + client.DisplayName + ".");
                return;
            }

            string folder = File.Exists(client.ConfigPath) ? Path.GetDirectoryName(client.ConfigPath) : Path.GetDirectoryName(client.ConfigPath);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                folder = MCPCliInstaller.GetProjectRootForUi();
            }

            EditorUtility.RevealInFinder(folder);
            SetIntegrationMessage("Opened config location for " + client.DisplayName + ".");
        }

        private void SetIntegrationMessage(string message)
        {
            if (_integrationStatusLabel != null) _integrationStatusLabel.text = message;
            NexusEditorLog.Log(NexusLogCategory.Integrations, "[Nexus Unity] " + message, true);
        }

        private static bool IsFileBackedConfig(NexusMcpClientInfo client)
        {
            return client.Format == NexusMcpConfigFormat.CodexToml ||
                   client.Format == NexusMcpConfigFormat.JsonMcpServers ||
                   client.Format == NexusMcpConfigFormat.JsonServers;
        }

        private static void StyleIntegrationButton(Button button)
        {
            button.style.flexGrow = 1;
            button.style.flexShrink = 1;
            button.style.minWidth = 88;
            button.style.height = 28;
        }

        private static Color GetIntegrationStatusColor(NexusMcpClientStatus status)
        {
            switch (status)
            {
                case NexusMcpClientStatus.Configured: return new Color(0.12f, 0.62f, 0.22f);
                case NexusMcpClientStatus.Detected: return new Color(0.12f, 0.50f, 0.72f);
                case NexusMcpClientStatus.Outdated: return new Color(0.92f, 0.45f, 0.12f);
                case NexusMcpClientStatus.NeedsRestart: return new Color(0.92f, 0.66f, 0.12f);
                case NexusMcpClientStatus.Error: return new Color(0.78f, 0.18f, 0.18f);
                default: return new Color(0.46f, 0.46f, 0.46f);
            }
        }

        private static string GetIntegrationStatusText(NexusMcpClientStatus status)
        {
            switch (status)
            {
                case NexusMcpClientStatus.NotFound: return "Not found";
                case NexusMcpClientStatus.Outdated: return "Outdated";
                case NexusMcpClientStatus.NeedsRestart: return "Needs restart";
                default: return status.ToString();
            }
        }
    }
}
