using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        private static JToken FindReferences(JToken p)
        {
            string targetGuid = p?["target_guid"]?.ToString();
            EntityId targetId = MCPServerMethods.ExtractId(p, "target_id");

            if (string.IsNullOrEmpty(targetGuid) && targetId == default)
                throw new System.Exception("Either target_guid or target_id is required.");

            Object targetObject = ResolveTargetObject(targetGuid, targetId);
            if (targetObject == null)
                throw new System.Exception("Could not find the target object.");

            string searchGuid = GetSearchGuid(targetGuid, targetObject);
            JArray assetRefs = FindAssetReferences(searchGuid);

            HashSet<EntityId> targetInstanceIds = BuildTargetInstanceIds(targetObject, searchGuid);
            JArray sceneRefs = FindSceneReferences(targetInstanceIds);

            var result = new JObject();
            result["status"] = "Success";
            result["asset_references"] = assetRefs;
            result["scene_references"] = sceneRefs;
            return result;
        }

        private static Object ResolveTargetObject(string targetGuid, EntityId targetId)
        {
            if (!string.IsNullOrEmpty(targetGuid))
            {
                string path = AssetDatabase.GUIDToAssetPath(targetGuid);
                if (!string.IsNullOrEmpty(path))
                    return AssetDatabase.LoadAssetAtPath<Object>(path);
            }
            else if (targetId != default)
            {
                return MCPServerMethods.IdToObject(targetId);
            }
            return null;
        }

        private static string GetSearchGuid(string targetGuid, Object targetObject)
        {
            if (!string.IsNullOrEmpty(targetGuid)) return targetGuid;
            string path = AssetDatabase.GetAssetPath(targetObject);
            return !string.IsNullOrEmpty(path) ? AssetDatabase.AssetPathToGUID(path) : null;
        }

        private static JArray FindAssetReferences(string searchGuid)
        {
            var assetRefs = new JArray();
            if (!string.IsNullOrEmpty(searchGuid))
            {
                string[] dependencies = AssetDatabase.FindAssets("ref:" + searchGuid);
                foreach (var guid in dependencies)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(path))
                        assetRefs.Add(path);
                }
            }
            return assetRefs;
        }

        private static HashSet<EntityId> BuildTargetInstanceIds(Object targetObject, string searchGuid)
        {
            var targetInstanceIds = new HashSet<EntityId>();
            if (targetObject != null)
                targetInstanceIds.Add(targetObject.GetId());

            if (!string.IsNullOrEmpty(searchGuid))
            {
                string path = AssetDatabase.GUIDToAssetPath(searchGuid);
                if (!string.IsNullOrEmpty(path))
                {
                    var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                    foreach (var a in allAssets)
                    {
                        if (a != null)
                            targetInstanceIds.Add(a.GetId());
                    }
                }
            }
            return targetInstanceIds;
        }

        private static JArray FindSceneReferences(HashSet<EntityId> targetInstanceIds)
        {
            var sceneRefs = new JArray();
            var allGameObjects = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(go => go.hideFlags == HideFlags.None || go.hideFlags == HideFlags.NotEditable);

            using (UnityEngine.Pool.ListPool<Component>.Get(out var components))
            {
                foreach (var go in allGameObjects)
                {
                    if (go.scene == null || !go.scene.isLoaded) continue;

                    go.GetComponents(components);
                    var goMatches = InspectGameObjectReferences(components, targetInstanceIds);
                    if (goMatches.Count > 0)
                    {
                        var goData = new JObject
                        {
                            ["name"] = go.name,
                            ["instance_id"] = go.GetRawId(),
                            ["references"] = goMatches
                        };
                        sceneRefs.Add(goData);
                    }
                }
            }
            return sceneRefs;
        }

        private static JArray InspectGameObjectReferences(List<Component> components, HashSet<EntityId> targetInstanceIds)
        {
            var goMatches = new JArray();
            foreach (var comp in components)
            {
                if (comp == null) continue;

                using (var so = new SerializedObject(comp))
                {
                    var prop = so.GetIterator();
                    bool enterChildren = true;
                    while (prop.Next(enterChildren))
                    {
                        enterChildren = false;
                        if (prop.propertyType == SerializedPropertyType.ObjectReference)
                        {
                            var propId = MCPServerMethods.GetObjectReferenceId(prop);
                            var objRef = prop.objectReferenceValue;

                            bool directMatch = targetInstanceIds.Contains(propId);
                            bool indirectMatch = objRef != null && targetInstanceIds.Contains(objRef.GetId());

                            AddReferenceMatch(goMatches, directMatch || indirectMatch, comp, prop);
                        }
                    }
                }
            }
            return goMatches;
        }

        private static void AddReferenceMatch(JArray matches, bool isMatch, Component component, SerializedProperty property)
        {
            if (!isMatch) return;
            matches.Add(new JObject
            {
                ["component"] = GetTypeName(component.GetType()),
                ["field"] = property.propertyPath
            });
        }
    }
}
