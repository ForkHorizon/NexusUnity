using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Linq;
using UnityEditor.SceneManagement;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Partial implementation of MCPServerMethods handling editor state and control.
    /// </summary>
    public static partial class MCPServerMethods
    {
        private static void RegisterEditorMethods()
        {
            _methods["undo"] = UndoMethod;
            _methods["redo"] = RedoMethod;
            _methods["toggle_play_mode"] = TogglePlayMode;
            _methods["pause_play_mode"] = PausePlayMode;
            _methods["step_frame"] = StepFrame;
            _methods["execute_menu_item"] = ExecuteMenuItem;
            _methods["lint_project"] = UnityLintProject;
            _methods["set_selection"] = SetSelection;
            _methods["focus_scene_view"] = FocusSceneView;
            _methods["list_scenes"] = ListScenes;
            _methods["get_tags_and_layers"] = GetTagsAndLayers;
            _methods["get_editor_state"] = GetEditorState;
            _methods["get_project_info"] = GetProjectInfo;
            _methods["set_property"] = SetProperty;
            _methods["open_prefab_stage"] = OpenPrefabStage;
            _methods["close_prefab_stage"] = ClosePrefabStage;
        }

        private static JToken OpenPrefabStage(JToken p)
        {
            if (p == null || p["path"] == null) throw new System.Exception("path is required");
            string path = p["path"].ToString();
            
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefabAsset == null) throw new System.Exception($"Prefab not found at {path}");

            AssetDatabase.OpenAsset(prefabAsset);
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            
            if (stage != null)
            {
                return new JObject { 
                    ["status"] = "Success", 
                    ["message"] = $"Opened Prefab Stage for {prefabAsset.name}",
                    ["stage_path"] = stage.assetPath
                };
            }
            return new JObject { ["status"] = "Failed", ["message"] = "Failed to open Prefab Stage" };
        }

        private static JToken ClosePrefabStage(JToken p)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null) return new JObject { ["status"] = "Failed", ["message"] = "No Prefab Stage is currently open" };
            
            StageUtility.GoToMainStage();
            return new JObject { ["status"] = "Success", ["message"] = "Returned to Main Stage" };
        }

        private static JToken ListScenes(JToken p)
        {
            var guids = AssetDatabase.FindAssets("t:Scene");
            return new JObject { ["scenes"] = new JArray(guids.Select(AssetDatabase.GUIDToAssetPath)) };
        }

        private static JToken FocusSceneView(JToken p)
        {
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
                return new JObject { ["status"] = "Success", ["message"] = "Focused" };
            }
            return new JObject { ["status"] = "Failed", ["message"] = "No active Scene View found" };
        }

        private static JToken UnityLintProject(JToken p)
        {
            // The auditor runs synchronously on the main thread
            string report = ProjectAuditorWrapper.RunAudit(silent: true);
            return JToken.Parse(report);
        }

        private static JToken SetSelection(JToken p)
        {
            if (p == null || p["instance_ids"] == null) throw new System.Exception("instance_ids (array) is required");
            var ids = p["instance_ids"] as JArray;
            if (ids == null) throw new System.Exception("instance_ids must be an array");
            
            var objects = ids.Select(token => MCPServerMethods.IdToObject(MCPServerMethods.ExtractIdFromToken(token)))
                             .Where(o => o != null)
                             .ToArray();
            
            Selection.objects = objects;
            return new JObject { ["status"] = "Success", ["message"] = $"Selected {objects.Length} objects" };
        }

        private static JToken UndoMethod(JToken p)
        {
            Undo.PerformUndo();
            return new JObject { ["status"] = "Success", ["message"] = "Undo performed" };
        }

        private static JToken RedoMethod(JToken p)
        {
            Undo.PerformRedo();
            return new JObject { ["status"] = "Success", ["message"] = "Redo performed" };
        }

        private static JToken TogglePlayMode(JToken p)
        {
            #if UNITY_EDITOR_OSX
            AppNapBypass.ScheduleActivation();
            #endif

            bool? value = p?["value"]?.ToObject<bool>();
            if (value.HasValue) EditorApplication.isPlaying = value.Value;
            else EditorApplication.isPlaying = !EditorApplication.isPlaying;
            return new JObject { ["status"] = "Success", ["is_playing"] = EditorApplication.isPlaying };
        }

        private static JToken ExecuteMenuItem(JToken p)
        {
            if (p == null || p["item_path"] == null) throw new System.Exception("item_path is required (e.g., 'Edit/Select All')");
            EditorApplication.ExecuteMenuItem(p["item_path"].ToString());
            return new JObject { ["status"] = "Success", ["message"] = "Executed" };
        }

        private static JToken GetTagsAndLayers(JToken p)
        {
            return new JObject
            {
                ["tags"] = new JArray(UnityEditorInternal.InternalEditorUtility.tags),
                ["layers"] = new JArray(UnityEditorInternal.InternalEditorUtility.layers)
            };
        }

        private static JToken SetProperty(JToken p)
        {
            if (p == null || p["instance_id"] == null || p["property_name"] == null || p["value"] == null)
                throw new System.Exception("instance_id, property_name, and value are required");

            var obj = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p));
            if (obj == null) throw new System.Exception("Object not found");

            SerializedObject so = new SerializedObject(obj);
            SerializedProperty prop = so.FindProperty(p["property_name"].ToString());
            
            if (prop == null) throw new System.Exception($"Property '{p["property_name"]}' not found on {obj.name}");

            Undo.RecordObject(obj, $"Set {p["property_name"]}");
            ApplyValueToSerializedProperty(prop, p["value"]);

            so.ApplyModifiedProperties();
            return new JObject { ["status"] = "Success", ["message"] = "Property updated" };
        }

        private static void ApplyValueToSerializedProperty(SerializedProperty prop, JToken val)
        {
            if (val.Type == JTokenType.Boolean) prop.boolValue = val.Value<bool>();
            else if (val.Type == JTokenType.Float) prop.floatValue = val.Value<float>();
            else if (val.Type == JTokenType.Integer) prop.intValue = val.Value<int>();
            else if (val.Type == JTokenType.String) prop.stringValue = val.Value<string>();
            else if (val.Type == JTokenType.Object && val["x"] != null)
            {
                prop.vector3Value = new Vector3(val["x"].Value<float>(), val["y"].Value<float>(), val["z"].Value<float>());
            }
            else throw new System.Exception("Value type not supported for surgical edit yet");
        }

        /// <summary>Returns current editor state flags.</summary>
        private static JToken GetEditorState(JToken p)
        {
            return new JObject
            {
                ["is_playing"] = EditorApplication.isPlaying,
                ["is_paused"] = EditorApplication.isPaused,
                ["is_compiling"] = EditorApplication.isCompiling,
                ["is_updating"] = EditorApplication.isUpdating,
                ["active_scene"] = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path,
                ["platform"] = EditorUserBuildSettings.activeBuildTarget.ToString()
            };
        }

        /// <summary>Pauses or unpauses Play Mode.</summary>
        private static JToken PausePlayMode(JToken p)
        {
            if (p?["value"] != null) EditorApplication.isPaused = p["value"].Value<bool>();
            else EditorApplication.isPaused = !EditorApplication.isPaused;
            return new JObject { ["is_paused"] = EditorApplication.isPaused };
        }

        /// <summary>Advances one frame while paused.</summary>
        private static JToken StepFrame(JToken p)
        {
            EditorApplication.Step();
            return new JObject { ["status"] = "Success", ["message"] = "OK" };
        }

        /// <summary>Returns basic project metadata.</summary>
        private static JToken GetProjectInfo(JToken p)
        {
            return new JObject
            {
                ["project_path"] = Application.dataPath.Replace("/Assets", ""),
                ["unity_version"] = Application.unityVersion,
                ["platform"] = EditorUserBuildSettings.activeBuildTarget.ToString(),
                ["product_name"] = Application.productName,
                ["company_name"] = Application.companyName
            };
        }
    }
}