#pragma warning disable 0618 // Suppress obsolete InstanceIDToObject/GetInstanceID warnings for stability in 2021.3+
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
            _typeCache.Clear();
            _methods["add_component"] = AddComponent;
            _methods["inspect_component"] = InspectComponent;
            _methods["update_component"] = UpdateComponent;
            _methods["get_component_schema"] = GetComponentSchema;
            _methods["set_transform"] = SetTransform;
            _methods["set_parent"] = SetParent;
            _methods["invoke_method"] = InvokeMethod;
        }

        private static JToken AddComponent(JToken p)
        {
            if (p == null || p["instance_id"] == null || p["component_name"] == null) throw new Exception("instance_id and component_name required");
            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
            Type type = FindType(p["component_name"].ToString());
            if (type == null) throw new Exception($"Type '{p["component_name"]}' not found");
            return new JObject { ["status"] = "Success", ["message"] = $"Added {p["component_name"]} to {Undo.AddComponent(go, type).name}" };
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
            while (prop.Next(enterChildren))
            {
                enterChildren = false; // Only enter children for the root
                try { result[prop.name] = SerializeProperty(prop); } catch { }
            }
            result["status"] = "Success";
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
            while (prop.Next(enterChildren))
            {
                enterChildren = false;
                result.Add(new JObject { ["name"] = prop.name, ["type"] = prop.propertyType.ToString(), ["displayName"] = prop.displayName });
            }
            return new JObject { ["properties"] = result };
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
                    return new JObject { ["instance_id"] = obj.GetInstanceID(), ["name"] = obj.name, ["type"] = obj.GetType().Name };
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

        private static JToken UpdateComponent(JToken p)
        {
            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
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

        private static SerializedProperty FindPropertyFuzzy(SerializedObject so, string name)
        {
            // 1. Exact match
            var prop = so.FindProperty(name);
            if (prop != null) return prop;

            // 2. Try with 'm_' prefix and capitalized first letter
            if (name.Length > 0)
            {
                string mName = "m_" + char.ToUpper(name[0]) + name.Substring(1);
                prop = so.FindProperty(mName);
                if (prop != null) return prop;
                
                // 3. Try with '_' prefix and lower case
                string underscoreName = "_" + char.ToLower(name[0]) + name.Substring(1);
                prop = so.FindProperty(underscoreName);
                if (prop != null) return prop;
            }

            // 4. Iterate all properties and do a case-insensitive match ignoring 'm_' prefix
            var iterator = so.GetIterator();
            bool enterChildren = true;
            while (iterator.Next(enterChildren))
            {
                enterChildren = false;
                string propName = iterator.name;
                string cleanPropName = propName.StartsWith("m_") ? propName.Substring(2) : propName;
                cleanPropName = cleanPropName.StartsWith("_") ? cleanPropName.Substring(1) : cleanPropName;

                if (string.Equals(cleanPropName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return so.FindProperty(propName); // Get fresh copy
                }
            }

            return null;
        }

        private static int UpdateComponentProperties(SerializedObject so, JObject data, JArray errors)
        {
            int updatedCount = 0;
            foreach (var propPair in data)
            {
                SerializedProperty prop = FindPropertyFuzzy(so, propPair.Key);
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
            if (prop.isArray && prop.propertyType != SerializedPropertyType.String)
            {
                if (value.Type == JTokenType.Array)
                {
                    JArray arr = (JArray)value;
                    prop.arraySize = arr.Count;
                    for (int i = 0; i < arr.Count; i++)
                    {
                        ApplyValueToProperty(prop.GetArrayElementAtIndex(i), arr[i]);
                    }
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
                    if (dict.TryGetValue(childProp.name, out JToken childValue))
                    {
                        ApplyValueToProperty(childProp, childValue);
                    }
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
            else if (value.Type == JTokenType.Object)
            {
                if (value["instance_id"] != null)
                {
                    prop.objectReferenceValue = EditorUtility.InstanceIDToObject((int)value["instance_id"]);
                }
                else if (value["guid"] != null && value["file_id"] != null)
                {
                    string guid = value["guid"].ToString();
                    long fileId = (long)value["file_id"];
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(path))
                    {
                        var all = AssetDatabase.LoadAllAssetsAtPath(path);
                        foreach (var asset in all)
                        {
                            if (asset == null) continue;
                            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out _, out long id);
                            if (id == fileId)
                            {
                                prop.objectReferenceValue = asset;
                                break;
                            }
                        }
                    }
                }
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
            return new JObject { ["status"] = "Success", ["data"] = SerializeGameObject(go) };
        }

        private static Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();

        private static Type FindType(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_typeCache.TryGetValue(name, out var cachedType)) return cachedType;

            // Priority 1: Check Assembly-CSharp (where most user scripts live)
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var mainAsm = assemblies.FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (mainAsm != null)
            {
                var t = mainAsm.GetType(name) ?? mainAsm.GetTypes().FirstOrDefault(x => x.Name == name);
                if (t != null) return _typeCache[name] = t;
            }

            // Priority 2: Check all other assemblies
            foreach (var a in assemblies)
            {
                try 
                {
                    var t = a.GetType(name) ?? a.GetTypes().FirstOrDefault(x => x.Name == name);
                    if (t != null) return _typeCache[name] = t;
                }
                catch { /* Ignore assemblies that fail to load types */ }
            }

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
            return new JObject { ["objects"] = result };
        }

        private static JToken GetActiveGameObject(JToken p)
        {
            var go = Selection.activeGameObject;
            return new JObject { ["status"] = "Success", ["data"] = go != null ? SerializeGameObject(go) : JValue.CreateNull() };
        }

        private static JToken SetTransform(JToken p)
        {
            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
            Undo.RecordObject(go.transform, "Set Transform");
            if (p["position"] != null) go.transform.position = ParseVector3(p["position"], go.transform.position);
            return new JObject { ["status"] = "Success", ["message"] = "Transform updated" };
        }

        private static JToken SetParent(JToken p)
        {
            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
            var parent = EditorUtility.InstanceIDToObject((int)p["parent_id"]) as GameObject;
            Undo.SetTransformParent(go.transform, parent?.transform, "Set Parent");
            return new JObject { ["status"] = "Success", ["message"] = "Parent set" };
        }

        private static JToken InvokeMethod(JToken p)
        {
            if (p == null || p["instance_id"] == null || p["method_name"] == null) 
                throw new Exception("instance_id and method_name required");

            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
            if (go == null) throw new Exception("GameObject not found");

            object target = go;
            string compName = p["component_name"]?.ToString();
            if (!string.IsNullOrEmpty(compName))
            {
                var comp = go.GetComponent(compName);
                if (comp == null) throw new Exception($"Component '{compName}' not found on object");
                target = comp;
            }

            string methodName = p["method_name"].ToString();
            var argsArray = p["arguments"] as JArray;
            int argCount = argsArray != null ? argsArray.Count : 0;

            var methods = target.GetType().GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Where(m => m.Name == methodName && m.GetParameters().Length == argCount)
                .ToList();

            if (methods.Count == 0)
                throw new Exception($"Method '{methodName}' with {argCount} parameters not found on '{target.GetType().Name}'");

            if (methods.Count > 1)
                throw new Exception($"Ambiguous method call. Found multiple '{methodName}' with {argCount} parameters.");

            var method = methods[0];
            var parameters = method.GetParameters();
            object[] invokeArgs = new object[argCount];

            for (int i = 0; i < argCount; i++)
            {
                var expectedType = parameters[i].ParameterType;
                var jsonValue = argsArray[i];

                try
                {
                    if (jsonValue.Type == JTokenType.Null)
                    {
                        invokeArgs[i] = null;
                    }
                    else if (expectedType == typeof(int)) invokeArgs[i] = (int)jsonValue;
                    else if (expectedType == typeof(float)) invokeArgs[i] = (float)jsonValue;
                    else if (expectedType == typeof(double)) invokeArgs[i] = (double)jsonValue;
                    else if (expectedType == typeof(bool)) invokeArgs[i] = (bool)jsonValue;
                    else if (expectedType == typeof(string)) invokeArgs[i] = (string)jsonValue;
                    else
                    {
                        // Fallback using JSON deserialization
                        invokeArgs[i] = jsonValue.ToObject(expectedType);
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to convert argument {i} to type '{expectedType.Name}': {ex.Message}");
                }
            }

            object result = null;
            try
            {
                result = method.Invoke(target, invokeArgs);
            }
            catch (Exception ex)
            {
                throw new Exception($"Method invocation failed: {ex.InnerException?.Message ?? ex.Message}");
            }

            if (method.ReturnType == typeof(void))
                return new JObject { ["status"] = "Success" };

            if (result == null) return new JObject { ["status"] = "Success", ["result"] = JValue.CreateNull() };

            JToken resultToken;
            // Try basic serialization for the result
            if (result is int || result is float || result is bool || result is string || result is double)
                resultToken = JToken.FromObject(result);
            else if (result is Vector3 v3) resultToken = new JObject { ["x"] = v3.x, ["y"] = v3.y, ["z"] = v3.z };
            else if (result is Vector2 v2) resultToken = new JObject { ["x"] = v2.x, ["y"] = v2.y };
            else
            {
                try
                {
                    resultToken = JToken.FromObject(result);
                }
                catch
                {
                    resultToken = result.ToString();
                }
            }

            if (resultToken is JObject jo)
            {
                jo["status"] = "Success";
                return jo;
            }

            return new JObject { ["status"] = "Success", ["result"] = resultToken };
        }    }
}
