using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Partial implementation of MCPServerMethods handling Component and Transform manipulation.
    /// </summary>
    public static partial class MCPServerMethods
    {
        private static void RegisterComponentMethods()
        {
            _methods["add_component"] = AddComponent;
            _methods["inspect_component"] = InspectComponent;
            _methods["update_component"] = UpdateComponent;
            _methods["get_component_schema"] = GetComponentSchema;
            _methods["set_transform"] = SetTransform;
            _methods["set_parent"] = SetParent;
        }

        private static JToken AddComponent(JToken p)
        {
            if (p == null || p["instance_id"] == null || p["component_name"] == null) throw new Exception("instance_id and component_name required");
            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
            Type type = FindType(p["component_name"].ToString());
            if (type == null) throw new Exception($"Type '{p["component_name"]}' not found");
            return $"Added {p["component_name"]} to {Undo.AddComponent(go, type).name}";
        }

        private static JToken InspectComponent(JToken p)
        {
            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
            var comp = go?.GetComponent(p["component_name"].ToString());
            if (comp == null) throw new Exception("Component not found");

            SerializedObject so = new SerializedObject(comp);
            JObject result = new JObject();
            SerializedProperty prop = so.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false; // Only enter children for the root
                try { result[prop.name] = SerializeProperty(prop); } catch { }
            }
            return result;
        }

        private static JToken GetComponentSchema(JToken p)
        {
            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
            var comp = go?.GetComponent(p["component_name"].ToString());
            if (comp == null) throw new Exception("Component not found");

            SerializedObject so = new SerializedObject(comp);
            JArray result = new JArray();
            SerializedProperty prop = so.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                result.Add(new JObject { ["name"] = prop.name, ["type"] = prop.propertyType.ToString(), ["displayName"] = prop.displayName });
            }
            return result;
        }

        private static JToken SerializeProperty(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue;
                case SerializedPropertyType.Boolean: return prop.boolValue;
                case SerializedPropertyType.Float: return prop.floatValue;
                case SerializedPropertyType.String: return prop.stringValue;
                case SerializedPropertyType.Color: return prop.colorValue.ToString();
                case SerializedPropertyType.Enum: return prop.enumDisplayNames[prop.enumValueIndex];
                default: return SerializeComplexProperty(prop);
            }
        }

        private static JToken SerializeComplexProperty(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Vector3:
                    return new JObject { ["x"] = prop.vector3Value.x, ["y"] = prop.vector3Value.y, ["z"] = prop.vector3Value.z };
                case SerializedPropertyType.ObjectReference:
                    var obj = prop.objectReferenceValue;
                    if (obj == null) return JValue.CreateNull();
                    return new JObject { ["instance_id"] = obj.GetInstanceID(), ["name"] = obj.name, ["type"] = obj.GetType().Name };
                default:
                    return prop.propertyType.ToString();
            }
        }

        private static JToken UpdateComponent(JToken p)
        {
            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
            var comp = go?.GetComponent(p["component_name"].ToString());
            if (comp == null) throw new Exception("Component not found");

            string jsonData = p["json_data"]?.ToString();
            if (string.IsNullOrEmpty(jsonData)) return "No data provided";

            JObject data = JObject.Parse(jsonData);
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

        private static int UpdateComponentProperties(SerializedObject so, JObject data, JArray errors)
        {
            int updatedCount = 0;
            foreach (var propPair in data)
            {
                SerializedProperty prop = so.FindProperty(propPair.Key);
                if (prop != null)
                {
                    try
                    {
                        ApplyValueToProperty(prop, propPair.Value);
                        updatedCount++;
                    }
                    catch (Exception e)
                    {
                        errors.Add(new JObject { ["field"] = propPair.Key, ["error"] = e.Message });
                    }
                }
                else
                {
                    errors.Add(new JObject { ["field"] = propPair.Key, ["error"] = "Property not found" });
                }
            }
            return updatedCount;
        }

        private static void ApplyValueToProperty(SerializedProperty prop, JToken value)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: prop.intValue = (int)value; break;
                case SerializedPropertyType.Boolean: prop.boolValue = (bool)value; break;
                case SerializedPropertyType.Float: prop.floatValue = (float)value; break;
                case SerializedPropertyType.String: prop.stringValue = value.ToString(); break;
                default: ApplyComplexValueToProperty(prop, value); break;
            }
        }

        private static void ApplyComplexValueToProperty(SerializedProperty prop, JToken value)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Vector3: prop.vector3Value = ParseVector3(value, prop.vector3Value); break;
                case SerializedPropertyType.Color:
                    if (ColorUtility.TryParseHtmlString(value.ToString(), out Color color)) prop.colorValue = color;
                    break;
                case SerializedPropertyType.Enum:
                    ApplyEnumValue(prop, value);
                    break;
                case SerializedPropertyType.ObjectReference:
                    ApplyObjectReferenceValue(prop, value);
                    break;
            }
        }

        private static void ApplyObjectReferenceValue(SerializedProperty prop, JToken value)
        {
            if (value.Type == JTokenType.Null)
            {
                prop.objectReferenceValue = null;
                return;
            }

            if (value.Type == JTokenType.Integer)
            {
                prop.objectReferenceValue = EditorUtility.InstanceIDToObject((int)value);
            }
            else if (value.Type == JTokenType.String)
            {
                prop.objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(value.ToString());
            }
            else if (value.Type == JTokenType.Object && value["instance_id"] != null)
            {
                prop.objectReferenceValue = EditorUtility.InstanceIDToObject((int)value["instance_id"]);
            }
        }

        private static void ApplyEnumValue(SerializedProperty prop, JToken value)
        {
            if (value.Type == JTokenType.Integer) prop.enumValueIndex = (int)value;
            else
            {
                int index = Array.IndexOf(prop.enumDisplayNames, value.ToString());
                if (index >= 0) prop.enumValueIndex = index;
            }
        }

        private static JToken InstantiatePrefab(JToken p)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p["path"].ToString());
            var go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            Undo.RegisterCreatedObjectUndo(go, "Instantiate Prefab");
            return SerializeGameObject(go);
        }

        private static Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();

        private static Type FindType(string name)
        {
            // Bolt Optimization: Check cache first to avoid O(A * T) iteration
            if (_typeCache.TryGetValue(name, out var cachedType)) return cachedType;

            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = a.GetType(name) ?? a.GetTypes().FirstOrDefault(x => x.Name == name);
                if (t != null)
                {
                    _typeCache[name] = t;
                    return t;
                }
            }

            // Negative cache: store null to prevent repeated expensive searches for missing types
            _typeCache[name] = null;
            return null;
        }

        private static JToken SerializeGameObject(GameObject go)
        {
            return new JObject { ["name"] = go.name, ["instance_id"] = go.GetInstanceID() };
        }

        private static Vector3 ParseVector3(JToken t, Vector3 _defaultValue = default)
        {
            return new Vector3((float)(t["x"] ?? _defaultValue.x), (float)(t["y"] ?? _defaultValue.y), (float)(t["z"] ?? _defaultValue.z));
        }

        private static List<GameObject> _rootGameObjectsCache = new List<GameObject>();

        private static JToken GetRootGameObjects(JToken p)
        {
            // Bolt Optimization: Reusing a static list to avoid array allocations from GetRootGameObjects()
            // and avoiding LINQ allocations.
            _rootGameObjectsCache.Clear();
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects(_rootGameObjectsCache);

            JArray result = new JArray();
            foreach (var go in _rootGameObjectsCache)
            {
                result.Add(SerializeGameObject(go));
            }
            return result;
        }

        private static JToken GetActiveGameObject(JToken p) => Selection.activeGameObject != null ? SerializeGameObject(Selection.activeGameObject) : JValue.CreateNull();

        private static JToken SetTransform(JToken p)
        {
            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
            Undo.RecordObject(go.transform, "Set Transform");
            if (p["position"] != null) go.transform.position = ParseVector3(p["position"], go.transform.position);
            return SerializeGameObject(go);
        }

        private static JToken SetParent(JToken p)
        {
            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
            var parent = EditorUtility.InstanceIDToObject((int)p["parent_id"]) as GameObject;
            Undo.SetTransformParent(go.transform, parent?.transform, "Set Parent");
            return "Parent set";
        }
    }
}
