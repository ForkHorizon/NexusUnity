using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    /// <summary>
    /// EditorWindow for running local Nexus Unity API smoke checks against scenes, materials, GameObjects, components, assets, and logs.
    /// </summary>
    /// <remarks>
    /// The window builds UI Toolkit controls and, when executed, sends JSON-RPC calls that create or modify editor objects and assets.
    /// Verification runs can dirty scenes/assets and write diagnostic messages to the Unity Console.
    /// </remarks>
    public class MCPVerificationWindow : EditorWindow
    {
        private Button _runButton;
        private Label _statusLabel;
        private Label _resultLabel;

        /// <summary>
        /// Creates or focuses the MCP Verification editor window, assigns its title, and shows it in the Unity Editor.
        /// </summary>
        public static void ShowWindow()
        {
            var window = GetWindow<MCPVerificationWindow>();
            window.titleContent = new GUIContent("Nexus Unity Verification");
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Nexus Unity Verification");
            minSize = new Vector2(480, 280);
        }

        /// <summary>
        /// Builds UI Toolkit status/result labels and a run button for the local API verification smoke test.
        /// </summary>
        /// <remarks>
        /// This method mutates the window visual tree, applies Nexus editor UI styling, and wires a button that schedules
        /// verification work on the Unity editor loop.
        /// </remarks>
        public void CreateGUI()
        {
            NexusEditorUi.SetupRoot(rootVisualElement);
            rootVisualElement.name = "NexusVerificationRoot";

            var header = NexusEditorUi.Panel("VerificationHeader");
            header.Add(NexusEditorUi.Label("API Verification", 18, true));
            header.Add(NexusEditorUi.Label("Run a compact smoke verification against scenes, materials, GameObjects, components, assets, and logs.", 11, false, NexusEditorUi.Muted));
            rootVisualElement.Add(header);

            var panel = NexusEditorUi.Panel("VerificationActions");
            _statusLabel = NexusEditorUi.Label("Status: Ready", 12, true, null, "VerificationStatusLabel");
            _resultLabel = NexusEditorUi.Label("No verification run yet.", 11, false, NexusEditorUi.Muted, "VerificationResultLabel");
            _resultLabel.style.marginTop = 4;

            var row = NexusEditorUi.Row(true);
            _runButton = NexusEditorUi.Button("Run API Verification", RunVerificationFromUi, "Start API verification", true, "VerificationRunButton");
            _runButton.style.flexGrow = 1;
            row.Add(_runButton);

            panel.Add(_statusLabel);
            panel.Add(_resultLabel);
            panel.Add(row);
            rootVisualElement.Add(panel);
        }

        private void RunVerificationFromUi()
        {
            if (_runButton != null) _runButton.SetEnabled(false);
            if (_statusLabel != null) _statusLabel.text = "Status: Running";
            if (_resultLabel != null) _resultLabel.text = "Verification scheduled on the Unity Editor loop.";

            EditorApplication.delayCall += () =>
            {
                try
                {
                    RunVerification();
                    if (_statusLabel != null) _statusLabel.text = "Status: Complete";
                    if (_resultLabel != null) _resultLabel.text = "Verification complete.";
                }
                catch (Exception e)
                {
                    NexusEditorLog.Error(NexusLogCategory.Diagnostics, $"Verification failed: {e.Message}");
                    if (_statusLabel != null) _statusLabel.text = "Status: Failed";
                    if (_resultLabel != null) _resultLabel.text = e.Message;
                }
                finally
                {
                    if (_runButton != null) _runButton.SetEnabled(true);
                }
            };
        }

        private void RunVerification()
        {
            NexusEditorLog.Log(NexusLogCategory.Diagnostics, "Starting Verification...", true);
            VerifyScenesAndMaterials();
            VerifyGameObjectsAndComponents();
            VerifyAssetsAndLogs();
            NexusEditorLog.Log(NexusLogCategory.Diagnostics, "Verification Complete.", true);
        }

        private void VerifyScenesAndMaterials()
        {
            Call("create_scene", new JObject { ["name"] = "VerifyScene" });
            Call("create_material", new JObject { ["name"] = "VerifyMat" });
            Call("refresh_asset_database", null);
        }

        private void VerifyGameObjectsAndComponents()
        {
            string resp = Call("create_game_object", new JObject { ["name"] = "VerifyGO" });
            string resultJson = ExtractResult(resp);

            if (string.IsNullOrEmpty(resultJson) || resultJson == "{}")
            {
                NexusEditorLog.Error(NexusLogCategory.Diagnostics, "Failed to create GameObject for verification.");
                return;
            }

            var goRes = JObject.Parse(resultJson);
            if (goRes["instance_id"] == null)
            {
                NexusEditorLog.Error(NexusLogCategory.Diagnostics, "GameObject creation failed: missing instance_id");
                return;
            }

            int id = (int)goRes["instance_id"];
            Call("add_component", new JObject { ["instance_id"] = id, ["component_name"] = "BoxCollider" });
            Call("update_component", new JObject { ["instance_id"] = id, ["component_name"] = "BoxCollider", ["properties"] = new JObject { ["isTrigger"] = true } });
            Call("set_transform", new JObject { ["instance_id"] = id, ["position"] = new JObject { ["x"] = 5 } });
        }

        private void VerifyAssetsAndLogs()
        {
            Call("list_assets", new JObject { ["filter"] = "t:Material" });
            Call("read_logs", new JObject { ["count"] = 5 });
        }

        private string Call(string method, JObject parameters)
        {
            return MCPServerMethods.ProcessJsonRpc(new JObject { ["jsonrpc"] = "2.0", ["method"] = method, ["params"] = parameters, ["id"] = 1 }.ToString());
        }

        private string ExtractResult(string resp)
        {
            try
            {
                var obj = JObject.Parse(resp);
                if (obj["error"] != null)
                {
                    NexusEditorLog.Error(NexusLogCategory.Diagnostics, $"RPC Error: {obj["error"]["message"]}");
                    return "{}";
                }
                return obj["result"]?.ToString() ?? "{}";
            }
            catch (Exception e)
            {
                NexusEditorLog.Error(NexusLogCategory.Diagnostics, $"JSON Parse Error in ExtractResult: {e.Message}");
                return "{}";
            }
        }
    }
}
