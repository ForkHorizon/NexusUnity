using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor.Runtime;

namespace UnityMCP.Editor
{
    public partial class MCPServerWindow
    {
        private Label _settingsSummaryLabel;
        private VisualElement _settingsCategoryRows;

        private void DrawSettingsTab()
        {
            var section = NexusEditorUi.Section("Settings", "Local Nexus Unity editor preferences.", "NexusSettingsSection");
            DrawRuntimeSettings(section);
            DrawConsoleLoggingSettings(section);
            _content.Add(section);
        }

        private void DrawRuntimeSettings(VisualElement section)
        {
            var panel = NexusEditorUi.Panel("NexusRuntimePanel");
            panel.Add(NexusEditorUi.Label("Runtime", 13, true, null, "NexusRuntimeTitle"));
            panel.Add(NexusEditorUi.Label(
                "auto prefers Pipeline on eligible installs and Legacy otherwise. Set Mode to Legacy to force HTTP immediately. Legacy HTTP remains fully supported; a sunset timeline is published, and removal is not scheduled.",
                11, false, NexusEditorUi.Muted, "NexusRuntimeDescription"));

            var modeField = new EnumField("Mode", MCPSettings.RuntimeMode)
            {
                name = "NexusRuntimeModeField",
                tooltip = "Requested transport runtime. Default is auto."
            };
            modeField.style.marginTop = 8;
            modeField.RegisterValueChangedCallback(evt =>
            {
                MCPSettings.RuntimeMode = (NexusRuntimeMode)evt.newValue;
            });
            panel.Add(modeField);
            panel.Add(NexusEditorUi.Label(
                "Effective: " + NexusRuntimeSelector.ToWireName(NexusRuntimeHost.EffectiveMode),
                11, false, NexusEditorUi.Muted, "NexusRuntimeEffective"));
            section.Add(panel);
        }

        private void DrawConsoleLoggingSettings(VisualElement section)
        {
            var panel = NexusEditorUi.Panel("NexusConsoleLoggingPanel");
            panel.Add(NexusEditorUi.Label("Console Logging", 13, true, null, "NexusConsoleLoggingTitle"));
            panel.Add(NexusEditorUi.Label("Controls which Nexus Unity service messages are written to the Unity Console. Warnings and errors are always shown.", 11, false, NexusEditorUi.Muted, "NexusConsoleLoggingDescription"));

            var modeField = new EnumField("Mode", MCPSettings.ConsoleLogMode)
            {
                name = "NexusConsoleLogModeField",
                tooltip = "Important keeps the Console quieter. All shows every Nexus Unity diagnostic. Custom filters info logs by category."
            };
            modeField.style.marginTop = 8;
            modeField.RegisterValueChangedCallback(evt =>
            {
                MCPSettings.ConsoleLogMode = (NexusConsoleLogMode)evt.newValue;
                RefreshSettingsSummary();
                RefreshSettingsCategoryRows();
            });
            panel.Add(modeField);

            _settingsCategoryRows = NexusEditorUi.Panel("NexusConsoleLogCategories");
            _settingsCategoryRows.style.marginTop = 8;
            panel.Add(_settingsCategoryRows);

            _settingsSummaryLabel = NexusEditorUi.Label(MCPSettings.GetConsoleLoggingSummary(), 11, false, NexusEditorUi.Muted, "NexusConsoleLoggingSummary");
            _settingsSummaryLabel.style.marginTop = 4;
            panel.Add(_settingsSummaryLabel);

            var actions = NexusEditorUi.Row(true, "NexusSettingsActions");
            var reset = NexusEditorUi.Button("Reset to Defaults", () =>
            {
                MCPSettings.ResetConsoleLoggingDefaults();
                DrawSelectedTab();
            }, "Restore Important logging and all custom categories.", false, "NexusResetConsoleLoggingButton");
            actions.Add(reset);
            StretchActionButtons(actions, 140);
            panel.Add(actions);

            RefreshSettingsCategoryRows();
            section.Add(panel);
        }

        private void RefreshSettingsSummary()
        {
            if (_settingsSummaryLabel != null)
            {
                _settingsSummaryLabel.text = MCPSettings.GetConsoleLoggingSummary();
            }
        }

        private void RefreshSettingsCategoryRows()
        {
            if (_settingsCategoryRows == null) return;

            _settingsCategoryRows.Clear();
            bool customMode = MCPSettings.ConsoleLogMode == NexusConsoleLogMode.Custom;
            var heading = NexusEditorUi.Label("Custom Categories", 12, true, null, "NexusConsoleLogCategoriesTitle");
            heading.tooltip = "These toggles affect info-level Nexus Unity logs only. Warnings and errors are always visible.";
            _settingsCategoryRows.Add(heading);

            foreach (var category in MCPSettings.LogCategoryOptions)
            {
                var toggle = new Toggle(MCPSettings.GetLogCategoryLabel(category))
                {
                    name = "NexusLogCategory" + category + "Toggle",
                    value = (MCPSettings.EnabledLogCategories & category) != 0,
                    tooltip = "Show info-level Nexus Unity logs for " + MCPSettings.GetLogCategoryLabel(category) + " when mode is Custom."
                };
                toggle.style.marginTop = 3;
                toggle.SetEnabled(customMode);
                toggle.RegisterValueChangedCallback(evt =>
                {
                    MCPSettings.SetLogCategoryEnabled(category, evt.newValue);
                    RefreshSettingsSummary();
                });
                _settingsCategoryRows.Add(toggle);
            }

            if (!customMode)
            {
                _settingsCategoryRows.Add(NexusEditorUi.Label("Switch mode to Custom to edit category filters.", 10, false, NexusEditorUi.Muted, "NexusConsoleLogCategoriesDisabledHint"));
            }
        }
    }
}
