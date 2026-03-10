using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;
using UnityMCP.Runtime;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Partial implementation handling complex SerializedProperty serialization and updates.
    /// </summary>
    public static partial class MCPServerMethods
    {
        private static void RegisterSerializationMethods()
        {
            _methods["enforce_forced_defaults"] = EnforceForcedDefaults;
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
                case SerializedPropertyType.Enum: return prop.enumNames.Length > 0 && prop.enumValueIndex >= 0 && prop.enumValueIndex < prop.enumNames.Length ? prop.enumNames[prop.enumValueIndex] : prop.enumValueIndex.ToString();
                case SerializedPropertyType.Vector2: return new JObject { ["x"] = prop.vector2Value.x, ["y"] = prop.vector2Value.y };
                case SerializedPropertyType.Vector3: return new JObject { ["x"] = prop.vector3Value.x, ["y"] = prop.vector3Value.y, ["z"] = prop.vector3Value.z };
                case SerializedPropertyType.Vector4: return new JObject { ["x"] = prop.vector4Value.x, ["y"] = prop.vector4Value.y, ["z"] = prop.vector4Value.z, ["w"] = prop.vector4Value.w };
                case SerializedPropertyType.Rect: return new JObject { ["x"] = prop.rectValue.x, ["y"] = prop.rectValue.y, ["width"] = prop.rectValue.width, ["height"] = prop.rectValue.height };
                case SerializedPropertyType.Bounds: return new JObject { ["x"] = prop.boundsValue.center.x, ["y"] = prop.boundsValue.center.y, ["z"] = prop.boundsValue.center.z };
                case SerializedPropertyType.ObjectReference:
                    var obj = prop.objectReferenceValue;
                    if (obj == null) return JValue.CreateNull();
                    return new JObject { ["instance_id"] = obj.GetRawId(), ["name"] = obj.name, ["type"] = obj.GetType().Name };
                default: return SerializeComplexProperty(prop);
            }
        }

        private static JToken SerializeComplexProperty(SerializedProperty prop)
        {
            if (prop.isArray && prop.propertyType != SerializedPropertyType.String)
            {
                JArray arr = new JArray();
                for (int i = 0; i < prop.arraySize; i++)
                {
                    arr.Add(SerializeProperty(prop.GetArrayElementAtIndex(i)));
                }
                return arr;
            }

            if (prop.propertyType == SerializedPropertyType.Generic)
            {
                JObject dict = new JObject();
                SerializedProperty childProp = prop.Copy();
                SerializedProperty endProp = childProp.GetEndProperty();
                bool enter = true;
                while (childProp.Next(enter) && !SerializedProperty.EqualContents(childProp, endProp))
                {
                    enter = false;
                    dict[childProp.name] = SerializeProperty(childProp);
                }
                return dict;
            }

            return prop.propertyType.ToString();
        }

        private static int UpdateComponentProperties(SerializedObject so, JObject data, JArray errors)
        {
            int updatedCount = 0;
            foreach (var propPair in data)
            {
                SerializedProperty prop = FindPropertyFuzzy(so, propPair.Key);
                if (prop != null)
                {
                    try { ApplyValueToProperty(prop, propPair.Value); updatedCount++; }
                    catch (Exception e) { errors.Add(new JObject { ["field"] = propPair.Key, ["error"] = e.Message }); }
                }
                else { errors.Add(new JObject { ["field"] = propPair.Key, ["error"] = "Property not found" }); }
            }
            return updatedCount;
        }

        private static SerializedProperty FindPropertyFuzzy(SerializedObject so, string name)
        {
            var prop = so.FindProperty(name);
            if (prop != null) return prop;

            if (name.Length > 0)
            {
                string mName = "m_" + char.ToUpper(name[0]) + name.Substring(1);
                prop = so.FindProperty(mName);
                if (prop != null) return prop;
                string underscoreName = "_" + char.ToLower(name[0]) + name.Substring(1);
                prop = so.FindProperty(underscoreName);
                if (prop != null) return prop;
            }

            var iterator = so.GetIterator();
            bool enterChildren = true;
            while (iterator.Next(enterChildren))
            {
                enterChildren = false;
                string propName = iterator.name;
                string cleanPropName = propName.StartsWith("m_") ? propName.Substring(2) : propName;
                cleanPropName = cleanPropName.StartsWith("_") ? cleanPropName.Substring(1) : cleanPropName;
                if (string.Equals(cleanPropName, name, StringComparison.OrdinalIgnoreCase)) return so.FindProperty(propName);
            }
            return null;
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
            if (prop.isArray && prop.propertyType != SerializedPropertyType.String)
            {
                if (value.Type == JTokenType.Array)
                {
                    JArray arr = (JArray)value;
                    prop.arraySize = arr.Count;
                    for (int i = 0; i < arr.Count; i++) ApplyValueToProperty(prop.GetArrayElementAtIndex(i), arr[i]);
                }
                return;
            }

            if (prop.propertyType == SerializedPropertyType.Generic && value.Type == JTokenType.Object)
            {
                JObject dict = (JObject)value;
                SerializedProperty childProp = prop.Copy();
                SerializedProperty endProp = childProp.GetEndProperty();
                bool enter = true;
                while (childProp.Next(enter) && !SerializedProperty.EqualContents(childProp, endProp))
                {
                    enter = false;
                    if (dict.TryGetValue(childProp.name, out JToken childValue)) ApplyValueToProperty(childProp, childValue);
                }
                return;
            }

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Vector2: prop.vector2Value = new Vector2((float)(value["x"] ?? prop.vector2Value.x), (float)(value["y"] ?? prop.vector2Value.y)); break;
                case SerializedPropertyType.Vector3: prop.vector3Value = ParseVector3(value, prop.vector3Value); break;
                case SerializedPropertyType.Vector4: prop.vector4Value = new Vector4((float)(value["x"] ?? prop.vector4Value.x), (float)(value["y"] ?? prop.vector4Value.y), (float)(value["z"] ?? prop.vector4Value.z), (float)(value["w"] ?? prop.vector4Value.w)); break;
                case SerializedPropertyType.Rect: prop.rectValue = new Rect((float)(value["x"] ?? prop.rectValue.x), (float)(value["y"] ?? prop.rectValue.y), (float)(value["width"] ?? prop.rectValue.width), (float)(value["height"] ?? prop.rectValue.height)); break;
                case SerializedPropertyType.Bounds: prop.boundsValue = new Bounds(new Vector3((float)(value["x"] ?? prop.boundsValue.center.x), (float)(value["y"] ?? prop.boundsValue.center.y), (float)(value["z"] ?? prop.boundsValue.center.z)), prop.boundsValue.size); break;
                case SerializedPropertyType.Color: if (ColorUtility.TryParseHtmlString(value.ToString(), out Color color)) prop.colorValue = color; break;
                case SerializedPropertyType.Enum: ApplyEnumValue(prop, value); break;
                case SerializedPropertyType.ObjectReference: ApplyObjectReferenceValue(prop, value); break;
            }
        }

        private static void ApplyObjectReferenceValue(SerializedProperty prop, JToken value)
        {
            if (value.Type == JTokenType.Null) { prop.objectReferenceValue = null; return; }
            if (value.Type == JTokenType.Integer) prop.objectReferenceValue = MCPServerMethods.IdToObject(MCPServerMethods.ExtractIdFromToken(value));
            else if (value.Type == JTokenType.String) prop.objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(value.ToString());
            else if (value.Type == JTokenType.Object)
            {
                if (value["instance_id"] != null) prop.objectReferenceValue = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(value));
                else if (value["guid"] != null && value["file_id"] != null)
                {
                    string path = AssetDatabase.GUIDToAssetPath(value["guid"].ToString());
                    if (!string.IsNullOrEmpty(path))
                    {
                        long fileId = (long)value["file_id"];
                        var all = AssetDatabase.LoadAllAssetsAtPath(path);
                        foreach (var asset in all)
                        {
                            if (asset == null) continue;
                            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out _, out long id);
                            if (id == fileId) { prop.objectReferenceValue = asset; break; }
                        }
                    }
                }
            }
        }

        private static void ApplyEnumValue(SerializedProperty prop, JToken value)
        {
            if (value.Type == JTokenType.Integer) prop.enumValueIndex = (int)value;
            else { int index = Array.IndexOf(prop.enumDisplayNames, value.ToString()); if (index >= 0) prop.enumValueIndex = index; }
        }

        private static JToken EnforceForcedDefaults(JToken p)
        {
            if (p == null || p["instance_id"] == null) throw new Exception("instance_id required");
            var go = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p)) as GameObject;
            if (go == null) throw new Exception("Object not found");

            int count = 0;
            foreach (var comp in go.GetComponents<MonoBehaviour>())
            {
                if (comp == null) continue;
                count += EnforceOnComponent(comp);
            }
            return $"Enforced {count} default values on {go.name}";
        }

        private static int EnforceOnComponent(MonoBehaviour comp)
        {
            int enforcedCount = 0;
            var fields = comp.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            SerializedObject so = new SerializedObject(comp);
            foreach (var field in fields)
            {
                var attr = field.GetCustomAttribute<ForceDefaultAttribute>();
                if (attr == null) continue;
                var prop = so.FindProperty(field.Name);
                if (prop == null) continue;
                Undo.RecordObject(comp, "Enforce Default");
                if (attr.DefaultValue != null) { ApplyValueToProperty(prop, attr.DefaultValue); enforcedCount++; }
            }
            so.ApplyModifiedProperties();
            return enforcedCount;
        }

        private static void ApplyValueToProperty(SerializedProperty prop, object value)
        {
            if (value is bool b) prop.boolValue = b;
            else if (value is int i) prop.intValue = i;
            else if (value is float f) prop.floatValue = f;
            else if (value is string s) prop.stringValue = s;
            else if (value is Vector3 v) prop.vector3Value = v;
        }
    }
}