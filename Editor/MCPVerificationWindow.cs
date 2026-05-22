using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Window for verifying MCP functionality in the editor.
    /// </summary>
    public class MCPVerificationWindow : EditorWindow
    {
        private Button _runButton;
        private Label _statusLabel;
        private Label _resultLabel;

        /// <summary>Shows the MCP Verification window.</summary>
        [MenuItem("Window/Nexus Unity/API Verification")]
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
                    if (_resultLabel != null) _resultLabel.text = "Verification complete. Check the Console for detailed RPC output.";
                }
                catch (Exception e)
                {
                    Debug.LogError($"Verification failed: {e.Message}");
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
            Debug.Log("Starting Verification...");
            VerifyScenesAndMaterials();
            VerifyGameObjectsAndComponents();
            VerifyAssetsAndLogs();
            Debug.Log("Verification Complete.");
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
                Debug.LogError("Failed to create GameObject for verification.");
                return;
            }

            var goRes = JObject.Parse(resultJson);
            if (goRes["instance_id"] == null)
            {
                Debug.LogError("GameObject creation failed: missing instance_id");
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
                    Debug.LogError($"RPC Error: {obj["error"]["message"]}");
                    return "{}";
                }
                return obj["result"]?.ToString() ?? "{}";
            }
            catch (Exception e)
            {
                Debug.LogError($"JSON Parse Error in ExtractResult: {e.Message}");
                return "{}";
            }
        }
    }
}
