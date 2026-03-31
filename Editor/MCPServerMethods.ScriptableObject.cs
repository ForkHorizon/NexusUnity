using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        private static void RegisterScriptableObjectMethods()
        {
            _methods["read_scriptable_object"] = ReadScriptableObject;
            _methods["update_scriptable_object"] = UpdateScriptableObject;
            _methods["create_scriptable_object_asset"] = CreateScriptableObjectAsset;
            _methods["duplicate_scriptable_object_asset"] = DuplicateScriptableObjectAsset;
            _methods["list_fields_for_type"] = ListFieldsForType;
        }

        private static ScriptableObject GetScriptableObject(JToken p)
        {
            if (p["path"] != null)
            {
                string path = p["path"].ToString();
                var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so == null) throw new Exception($"ScriptableObject not found at path: {path}");
                return so;
            }
            
            EntityId id = ExtractId(p);
            if (!id.Equals(default(EntityId)))
            {
                var obj = IdToObject(id) as ScriptableObject;
                if (obj == null) throw new Exception($"Object with ID {id} is not a ScriptableObject or not found");
                return obj;
            }

            throw new Exception("Either 'path' or 'instance_id' must be provided");
        }

        private static JToken ReadScriptableObject(JToken p)
        {
            var so = GetScriptableObject(p);
            var serializedObj = new SerializedObject(so);
            JObject result = new JObject();
            SerializedProperty prop = serializedObj.GetIterator();
            bool enterChildren = true;
            
            while (prop.Next(enterChildren))
            {
                enterChildren = false;
                try { result[prop.name] = SerializeProperty(prop); } catch { }
            }
            
            result["status"] = "Success";
            result["type"] = so.GetType().Name;
            result["name"] = so.name;
            return result;
        }

        private static JToken UpdateScriptableObject(JToken p)
        {
            var so = GetScriptableObject(p);
            JObject data = p["properties"] as JObject;
            if (data == null) throw new Exception("'properties' object required");

            var serializedObj = new SerializedObject(so);
            Undo.RecordObject(so, "Update ScriptableObject");

            JArray errors = new JArray();
            int updatedCount = UpdateComponentProperties(serializedObj, data, errors);

            serializedObj.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            return new JObject 
            { 
                ["status"] = errors.Count > 0 ? "Partial Success" : "Success",
                ["updated_count"] = updatedCount,
                ["errors"] = errors
            };
        }

        private static JToken CreateScriptableObjectAsset(JToken p)
        {
            if (p["type"] == null || p["path"] == null) throw new Exception("'type' and 'path' required");
            string typeName = p["type"].ToString();
            string path = p["path"].ToString();

            Type type = FindType(typeName);
            if (type == null) throw new Exception($"Type '{typeName}' not found");
            if (!typeof(ScriptableObject).IsAssignableFrom(type)) throw new Exception($"Type '{typeName}' is not a ScriptableObject");

            var instance = ScriptableObject.CreateInstance(type);
            AssetDatabase.CreateAsset(instance, path);
            AssetDatabase.SaveAssets();

            return new JObject 
            { 
                ["status"] = "Success", 
                ["path"] = path, 
                ["instance_id"] = instance.GetRawId() 
            };
        }

        private static JToken DuplicateScriptableObjectAsset(JToken p)
        {
            if (p["source_path"] == null || p["dest_path"] == null) throw new Exception("'source_path' and 'dest_path' required");
            string source = p["source_path"].ToString();
            string dest = p["dest_path"].ToString();

            if (!AssetDatabase.CopyAsset(source, dest)) throw new Exception("Failed to copy asset");
            AssetDatabase.SaveAssets();

            var newAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(dest);
            return new JObject 
            { 
                ["status"] = "Success", 
                ["path"] = dest, 
                ["instance_id"] = newAsset != null ? newAsset.GetRawId() : 0 
            };
        }

        private static JToken ListFieldsForType(JToken p)
        {
            if (p["type"] == null) throw new Exception("'type' required");
            string typeName = p["type"].ToString();

            Type type = FindType(typeName);
            if (type == null) throw new Exception($"Type '{typeName}' not found");

            JArray result = new JArray();
            
            // For ScriptableObjects and Components, we use SerializedObject to get serializable fields
            if (typeof(ScriptableObject).IsAssignableFrom(type))
            {
                var temp = ScriptableObject.CreateInstance(type);
                var so = new SerializedObject(temp);
                var prop = so.GetIterator();
                bool enterChildren = true;
                while (prop.Next(enterChildren))
                {
                    enterChildren = false;
                    result.Add(new JObject 
                    { 
                        ["name"] = prop.name, 
                        ["type"] = prop.propertyType.ToString(), 
                        ["displayName"] = prop.displayName,
                        ["tooltip"] = prop.tooltip
                    });
                }
                UnityEngine.Object.DestroyImmediate(temp);
            }
            else
            {
                // Generic reflection for non-Unity objects if needed, 
                // but usually users want this for Unity serializable types.
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var f in fields)
                {
                    if (f.IsPublic || f.GetCustomAttribute<SerializeField>() != null)
                    {
                        result.Add(new JObject { ["name"] = f.Name, ["type"] = f.FieldType.Name });
                    }
                }
            }

            return new JObject { ["type"] = typeName, ["fields"] = result };
        }
    }
}
