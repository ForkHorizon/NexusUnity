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
            string path = ValidateAssetPath(p["path"].ToString());
            PrefabUtility.SaveAsPrefabAsset(go, path);
            AssetDatabase.SaveAssets();
            return new JObject { ["status"] = "Success", ["path"] = path };
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

            string path = ValidateAssetPath(p["path"].ToString());
            JObject data = p["properties"] as JObject;
            string compName = p["component_name"]?.ToString();

            var go = PrefabUtility.LoadPrefabContents(path);
            if (go == null) throw new Exception($"Could not load prefab at {path}");

            try
            {
                UnityEngine.Object target = ResolvePrefabEditTarget(go, compName);
                SerializedObject so = new SerializedObject(target);
                JArray errors = new JArray();
                int updatedCount = ApplyPrefabPropertyEdits(so, data, errors);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(go, path);
                return CreatePrefabEditResult(errors, updatedCount);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(go);
            }
        }

        private static UnityEngine.Object ResolvePrefabEditTarget(GameObject prefab, string componentName)
        {
            if (string.IsNullOrEmpty(componentName)) return prefab;
            var component = prefab.GetComponent(componentName);
            if (component == null) throw new Exception($"Component {componentName} not found on prefab");
            return component;
        }

        private static int ApplyPrefabPropertyEdits(SerializedObject serializedObject, JObject data, JArray errors)
        {
            int updatedCount = 0;
            foreach (var propPair in data)
            {
                var property = FindPrefabProperty(serializedObject, propPair.Key);
                if (property == null)
                {
                    errors.Add(new JObject { ["field"] = propPair.Key, ["error"] = "Property not found" });
                    continue;
                }

                try
                {
                    ApplySimpleJTokenValue(property, propPair.Value, "Value type not supported for surgical asset edit yet");
                    updatedCount++;
                }
                catch (Exception e)
                {
                    errors.Add(new JObject { ["field"] = propPair.Key, ["error"] = e.Message });
                }
            }
            return updatedCount;
        }

        private static SerializedProperty FindPrefabProperty(SerializedObject serializedObject, string propertyName)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property != null || propertyName.StartsWith("m_")) return property;
            string cleanName = "m_" + char.ToUpper(propertyName[0]) + propertyName.Substring(1);
            return serializedObject.FindProperty(cleanName);
        }

        private static JObject CreatePrefabEditResult(JArray errors, int updatedCount)
        {
            return new JObject
            {
                ["status"] = errors.Count == 0 ? "Success" : (updatedCount > 0 ? "Partial" : "Failed"),
                ["updated_count"] = updatedCount,
                ["errors"] = errors,
                ["message"] = "Prefab asset updated directly"
            };
        }

    }
}
