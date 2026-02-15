using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

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
            _methods["execute_menu_item"] = ExecuteMenuItem;
            _methods["get_tags_and_layers"] = GetTagsAndLayers;
            _methods["set_property"] = SetProperty;
            _methods["set_transform"] = SetTransform;
            _methods["set_parent"] = SetParent;
            _methods["add_component"] = AddComponent;
            _methods["inspect_component"] = InspectComponent;
            _methods["update_component"] = UpdateComponent;
            _methods["pause_play_mode"] = PausePlayMode;
            _methods["step_frame"] = StepFrame;
            _methods["attach_script"] = AttachScript;
            _methods["create_primitive"] = CreatePrimitive;
            _methods["unity_lint_project"] = UnityLintProject;
        }

        private static JToken UnityLintProject(JToken p)
        {
            // The auditor runs synchronously on the main thread
            string report = ProjectAuditor.RunAudit(silent: true);
            return new JObject
            {
                ["report"] = report,
                ["violation_count"] = report.Split('\n').Length
            };
        }

        private static JToken UndoMethod(JToken p)
        {
            Undo.PerformUndo();
            return "Undo performed";
        }

        private static JToken RedoMethod(JToken p)
        {
            Undo.PerformRedo();
            return "Redo performed";
        }

        private static JToken TogglePlayMode(JToken p)
        {
            bool? value = p?["value"]?.ToObject<bool>();
            if (value.HasValue) EditorApplication.isPlaying = value.Value;
            else EditorApplication.isPlaying = !EditorApplication.isPlaying;
            return $"Play mode: {EditorApplication.isPlaying}";
        }

        private static JToken ExecuteMenuItem(JToken p)
        {
            if (p == null || p["item_path"] == null) throw new System.Exception("item_path is required (e.g., 'Edit/Select All')");
            EditorApplication.ExecuteMenuItem(p["item_path"].ToString());
            return "Executed";
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

            var obj = EditorUtility.InstanceIDToObject((int)p["instance_id"]);
            if (obj == null) throw new System.Exception("Object not found");

            SerializedObject so = new SerializedObject(obj);
            SerializedProperty prop = so.FindProperty(p["property_name"].ToString());
            
            if (prop == null) throw new System.Exception($"Property '{p["property_name"]}' not found on {obj.name}");

            Undo.RecordObject(obj, $"Set {p["property_name"]}");
            ApplyValueToSerializedProperty(prop, p["value"]);

            so.ApplyModifiedProperties();
            return "Property updated";
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
    }
}