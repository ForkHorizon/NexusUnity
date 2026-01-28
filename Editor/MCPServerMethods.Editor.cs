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
            
            // Basic type support
            var val = p["value"];
            if (val.Type == JTokenType.Boolean) prop.boolValue = val.Value<bool>();
            else if (val.Type == JTokenType.Float) prop.floatValue = val.Value<float>();
            else if (val.Type == JTokenType.Integer) prop.intValue = val.Value<int>();
            else if (val.Type == JTokenType.String) prop.stringValue = val.Value<string>();
            else if (val.Type == JTokenType.Object && val["x"] != null) prop.vector3Value = new Vector3(val["x"].Value<float>(), val["y"].Value<float>(), val["z"].Value<float>());
            else throw new System.Exception("Value type not supported for surgical edit yet");

            so.ApplyModifiedProperties();
            return "Property updated";
        }
    }
}
