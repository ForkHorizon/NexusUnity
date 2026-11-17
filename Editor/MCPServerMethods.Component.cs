using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Registers JSON-RPC methods for Unity component inspection, component updates, transform changes, parenting, and prefab instantiation.
    /// </summary>
    /// <remarks>
    /// Operations use editor APIs such as <see cref="Undo.AddComponent"/>, <see cref="SerializedObject"/>, and transform parenting.
    /// They may modify scene objects, serialized properties, object references, the Undo stack, and hierarchy relationships.
    /// </remarks>
    public static partial class MCPServerMethods
    {
        private static void RegisterComponentMethods()
        {
            _typeCache.Clear();
            _methods["add_component"] = AddComponent;
            _methods["inspect_component"] = InspectComponent;
            _methods["component_values"] = ComponentValues;
            _methods["update_component"] = UpdateComponent;
            _methods["get_component_schema"] = GetComponentSchema;
            _methods["set_transform"] = SetTransform;
            _methods["set_parent"] = SetParent;
            _methods["instantiate_prefab"] = InstantiatePrefab;
        }

        private static JToken AddComponent(JToken p)
        {
            if (p == null || p["instance_id"] == null || p["component_name"] == null) throw new Exception("instance_id and component_name required");
            var go = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p)) as GameObject;
            if (go == null) throw new Exception("GameObject not found");
            string compName = p["component_name"].ToString();
            Type type = FindComponentType(compName);
            if (type == null) throw new Exception($"Type '{compName}' not found or is not a valid attachable Component");
            return new JObject { ["status"] = "Success", ["message"] = $"Added {compName} to {Undo.AddComponent(go, type).name}" };
        }

        private static JToken InspectComponent(JToken p)
        {
            var go = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p)) as GameObject;
            var comp = go?.GetComponent(p["component_name"].ToString());
            if (comp == null) throw new Exception("Component not found");

            bool detailed = p["detailed"]?.Value<bool>() ?? false;
            JArray fieldsFilter = p["fields"] as JArray;
            
            SerializedObject so = new SerializedObject(comp);
            JObject result = new JObject();

            if (fieldsFilter != null && fieldsFilter.Count > 0)
            {
                foreach (var field in fieldsFilter)
                {
                    string fieldName = field.ToString();
                    var prop = FindPropertyFuzzy(so, fieldName);
                    if (prop != null)
                    {
                        try { result[fieldName] = SerializeProperty(prop, detailed); } 
                        catch (Exception e) { NexusEditorLog.Warning(NexusLogCategory.Api, $"[MCP] Serialization error for {fieldName}: {e.Message}"); }
                    }
                }
            }
            else
            {
                SerializedProperty prop = so.GetIterator();
                bool enterChildren = true;
                while (prop.Next(enterChildren))
                {
                    enterChildren = false; // Only enter children for the root
                    try { result[prop.name] = SerializeProperty(prop, detailed); } 
                    catch (Exception e) { NexusEditorLog.Warning(NexusLogCategory.Api, $"[MCP] Serialization error for {prop.name}: {e.Message}"); }
                }
            }
            result["status"] = "Success";
            return result;
        }

        private static JToken ComponentValues(JToken p)
        {
            var go = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p)) as GameObject;
            var comp = go?.GetComponent(p["component_name"].ToString());
            if (comp == null) throw new Exception("Component not found");

            JArray fields = p["fields"] as JArray;
            if (fields == null || fields.Count == 0) throw new Exception("fields array is required");

            SerializedObject so = new SerializedObject(comp);
            JObject result = new JObject();
            foreach (var field in fields)
            {
                string fieldName = field.ToString();
                var prop = FindPropertyFuzzy(so, fieldName);
                if (prop != null)
                {
                    try { result[fieldName] = SerializeProperty(prop, false); } 
                    catch (Exception e) { NexusEditorLog.Warning(NexusLogCategory.Api, $"[MCP] Serialization error for {fieldName}: {e.Message}"); }
                }
                else
                {
                    result[fieldName] = null;
                }
            }
            return result;
        }

        private static JToken GetComponentSchema(JToken p)
        {
            var go = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p)) as GameObject;
            var comp = go?.GetComponent(p["component_name"].ToString());
            if (comp == null) throw new Exception("Component not found");

            SerializedObject so = new SerializedObject(comp);
            JArray result = new JArray();
            SerializedProperty prop = so.GetIterator();
            bool enterChildren = true;
            while (prop.Next(enterChildren))
            {
                enterChildren = false;
                result.Add(new JObject { ["name"] = prop.name, ["type"] = prop.propertyType.ToString(), ["displayName"] = prop.displayName });
            }
            return new JObject { ["properties"] = result };
        }

        private static JToken UpdateComponent(JToken p)
        {
            var go = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p)) as GameObject;
            var comp = go?.GetComponent(p["component_name"].ToString());
            if (comp == null) throw new Exception("Component not found");

            JObject data = null;
            if (p["properties"] != null)
            {
                data = p["properties"] as JObject;
            }
            else if (p["json_data"] != null)
            {
                string jsonData = p["json_data"].ToString();
                if (!string.IsNullOrEmpty(jsonData))
                    data = JObject.Parse(jsonData);
            }

            if (data == null) return "No data provided";

            SerializedObject so = new SerializedObject(comp);
            Undo.RecordObject(comp, "Update Component");

            JArray errors = new JArray();
            int updatedCount = UpdateComponentProperties(so, data, errors);

            so.ApplyModifiedProperties();
            
            return new JObject 
            { 
                ["status"] = errors.Count == 0 ? "Success" : (updatedCount > 0 ? "Partial" : "Failed"),
                ["updated_count"] = updatedCount,
                ["errors"] = errors
            };
        }

        private static JToken SetTransform(JToken p)
        {
            if (p == null || p["instance_id"] == null) throw new Exception("instance_id is required");
            var go = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p)) as GameObject;
            if (go == null) throw new Exception("GameObject not found");

            Undo.RecordObject(go.transform, "Set Transform");
            if (p["position"] != null) go.transform.position = ParseVector3(p["position"], go.transform.position);

            JToken rotation = p["rotation"] ?? p["eulerAngles"];
            if (rotation != null) go.transform.eulerAngles = ParseVector3(rotation, go.transform.eulerAngles);

            JToken scale = p["scale"] ?? p["localScale"];
            if (scale != null) go.transform.localScale = ParseVector3(scale, go.transform.localScale);

            return new JObject
            {
                ["status"] = "Success",
                ["message"] = "Transform updated",
                ["position"] = SerializeVector3(go.transform.position),
                ["rotation"] = SerializeVector3(go.transform.eulerAngles),
                ["scale"] = SerializeVector3(go.transform.localScale)
            };
        }

        private static JToken SetParent(JToken p)
        {
            var go = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p)) as GameObject;
            var parent = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p, "parent_id")) as GameObject;
            Undo.SetTransformParent(go.transform, parent?.transform, "Set Parent");
            return new JObject { ["status"] = "Success", ["message"] = "Parent set" };
        }

        private static JToken InstantiatePrefab(JToken p)
        {
            string path = ValidateAssetPath(p["path"].ToString());
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            Undo.RegisterCreatedObjectUndo(go, "Instantiate Prefab");
            return new JObject { ["status"] = "Success", ["data"] = SerializeGameObject(go) };
        }
    }
}
