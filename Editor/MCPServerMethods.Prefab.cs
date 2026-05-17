using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        private static JToken CreatePrefab(JToken p)
        {
            if (p == null || p["instance_id"] == null || p["path"] == null) throw new Exception("instance_id and path required");
            var go = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p)) as GameObject;
            PrefabUtility.SaveAsPrefabAsset(go, p["path"].ToString());
            AssetDatabase.SaveAssets();
            return new JObject { ["status"] = "Success", ["path"] = p["path"].ToString() };
        }


        private static JToken ApplyPrefabOverrides(JToken p)
        {
            if (p == null || p["instance_id"] == null) throw new Exception("instance_id required");
            var go = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p)) as GameObject;
            if (!PrefabUtility.IsPartOfPrefabInstance(go)) throw new Exception("GameObject is not a prefab instance");

            var overrides = GetPrefabOverrides(p);
            PrefabUtility.ApplyPrefabInstance(go, InteractionMode.UserAction);
            AssetDatabase.SaveAssets();

            var result = overrides as JObject;
            result["status"] = "Success";
            result["message"] = "Overrides applied to prefab asset";
            return result;
        }


        private static JToken RevertPrefabOverrides(JToken p)
        {
            if (p == null || p["instance_id"] == null) throw new Exception("instance_id required");
            var go = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p)) as GameObject;
            if (!PrefabUtility.IsPartOfPrefabInstance(go)) throw new Exception("GameObject is not a prefab instance");

            var overrides = GetPrefabOverrides(p);
            PrefabUtility.RevertPrefabInstance(go, InteractionMode.AutomatedAction);

            var result = overrides as JObject;
            result["status"] = "Success";
            result["message"] = "Overrides reverted to prefab asset defaults";
            return result;
        }


        private static JToken GetPrefabOverrides(JToken p)
        {
            if (p == null || p["instance_id"] == null) throw new Exception("instance_id required");
            var go = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p)) as GameObject;
            if (!PrefabUtility.IsPartOfPrefabInstance(go)) throw new Exception("GameObject is not a prefab instance");

            JArray propertyMods = new JArray();
            var mods = PrefabUtility.GetPropertyModifications(go);
            if (mods != null)
            {
                foreach (var mod in mods)
                {
                    propertyMods.Add(new JObject {
                        ["target_type"] = mod.target?.GetType().Name,
                        ["property_path"] = mod.propertyPath,
                        ["value"] = mod.value
                    });
                }
            }

            JArray addedComps = new JArray();
            var added = PrefabUtility.GetAddedComponents(go);
            if (added != null)
            {
                foreach (var comp in added) addedComps.Add(comp.instanceComponent.GetType().Name);
            }

            JArray removedComps = new JArray();
            var removed = PrefabUtility.GetRemovedComponents(go);
            if (removed != null)
            {
                foreach (var comp in removed) removedComps.Add(comp.assetComponent.GetType().Name);
            }

            return new JObject {
                ["status"] = "Success",
                ["has_overrides"] = PrefabUtility.HasPrefabInstanceAnyOverrides(go, true),
                ["property_modifications"] = propertyMods,
                ["added_components"] = addedComps,
                ["removed_components"] = removedComps
            };
        }


        private static JToken EditPrefabAsset(JToken p)
        {
            if (p == null || p["path"] == null || p["properties"] == null)
                throw new Exception("path and properties are required");

            string path = p["path"].ToString();
            JObject data = p["properties"] as JObject;
            string compName = p["component_name"]?.ToString();

            var go = PrefabUtility.LoadPrefabContents(path);
            if (go == null) throw new Exception($"Could not load prefab at {path}");

            try
            {
                UnityEngine.Object target = go;
                if (!string.IsNullOrEmpty(compName))
                {
                    target = go.GetComponent(compName);
                    if (target == null) throw new Exception($"Component {compName} not found on prefab");
                }

                SerializedObject so = new SerializedObject(target);
                JArray errors = new JArray();
                int updatedCount = 0;

                foreach (var propPair in data)
                {
                    SerializedProperty prop = so.FindProperty(propPair.Key);
                    if (prop == null)
                    {
                        string cleanName = propPair.Key.StartsWith("m_") ? propPair.Key : "m_" + char.ToUpper(propPair.Key[0]) + propPair.Key.Substring(1);
                        prop = so.FindProperty(cleanName);
                    }

                    if (prop != null)
                    {
                        try { ApplyValueToSerializedPropertyAsset(prop, propPair.Value); updatedCount++; }
                        catch (Exception e) { errors.Add(new JObject { ["field"] = propPair.Key, ["error"] = e.Message }); }
                    }
                    else { errors.Add(new JObject { ["field"] = propPair.Key, ["error"] = "Property not found" }); }
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(go, path);

                return new JObject
                {
                    ["status"] = errors.Count == 0 ? "Success" : (updatedCount > 0 ? "Partial" : "Failed"),
                    ["updated_count"] = updatedCount,
                    ["errors"] = errors,
                    ["message"] = "Prefab asset updated directly"
                };
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(go);
            }
        }


        private static void ApplyValueToSerializedPropertyAsset(SerializedProperty prop, JToken val)
        {
            if (val.Type == JTokenType.Boolean) prop.boolValue = val.Value<bool>();
            else if (val.Type == JTokenType.Float) prop.floatValue = val.Value<float>();
            else if (val.Type == JTokenType.Integer) prop.intValue = val.Value<int>();
            else if (val.Type == JTokenType.String) prop.stringValue = val.Value<string>();
            else if (val.Type == JTokenType.Object && val["x"] != null)
            {
                prop.vector3Value = new Vector3(val["x"].Value<float>(), val["y"].Value<float>(), val["z"].Value<float>());
            }
            else throw new Exception("Value type not supported for surgical asset edit yet");
        }


    }
}
