#pragma warning disable 0618 // Suppress obsolete InstanceIDToObject/GetInstanceID warnings for stability in 2021.3+
using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Partial implementation of MCPServerMethods handling searching and discovery.
    /// </summary>
    public static partial class MCPServerMethods
    {
        private static void RegisterDiscoveryMethods()
        {
            _methods["get_active_game_object"] = GetActiveGameObject;
            _methods["get_root_game_objects"] = GetRootGameObjects;
            _methods["get_object_path"] = GetObjectPath;
            _methods["find_objects"] = FindObjects;
            _methods["find_by_path"] = FindByPath;
            _methods["ping_object"] = PingObject;
            _methods["find_references"] = FindReferences;
        }

        private static JToken FindByPath(JToken p)
        {
            if (p == null || p["path"] == null) throw new System.Exception("path required");
            string path = p["path"].ToString();
            var go = GameObject.Find(path);
            if (go == null) throw new System.Exception($"Object at path '{path}' not found");
            return new JObject { ["status"] = "Success", ["data"] = SerializeGameObject(go) };
        }

        private static JToken FindObjects(JToken p)
        {
            string name = p?["name"]?.ToString();
            string tag = p?["tag"]?.ToString();
            string typeName = p?["type"]?.ToString();
            
            IEnumerable<GameObject> results;

            // Bolt Optimization: Instead of getting all GameObjects and performing N+1 GetComponents,
            // we start with GameObjects that definitely have the component (if typeName is provided).
            if (!string.IsNullOrEmpty(typeName))
            {
                var type = FindType(typeName);
                if (type == null) return new JObject { ["objects"] = new JArray() }; // Short-circuit if type doesn't exist

                results = Resources.FindObjectsOfTypeAll(type)
                    .OfType<Component>()
                    .Select(c => c.gameObject)
                    .Distinct()
                    .Where(go => go.hideFlags == HideFlags.None);
            }
            else
            {
                results = Resources.FindObjectsOfTypeAll<GameObject>()
                    .Where(go => go.hideFlags == HideFlags.None);
            }

            // Bolt Optimization: Pre-instantiate Regex once outside the loop to avoid cache lookups and options parsing during iteration.
            // Avoid RegexOptions.Compiled for short-lived regex objects as it incurs high IL generation overhead.
            if (!string.IsNullOrEmpty(name))
            {
                var regex = new System.Text.RegularExpressions.Regex(name, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                results = results.Where(go => regex.IsMatch(go.name));
            }

            if (!string.IsNullOrEmpty(tag))
                results = results.Where(go => go.CompareTag(tag));

            return new JObject { ["objects"] = new JArray(results.Take(50).Select(SerializeGameObject)) };
        }

        private static JToken GetObjectPath(JToken p)
        {
            if (p == null || p["instance_id"] == null) throw new System.Exception("instance_id is required");
            var go = IdToObject((int)p["instance_id"]) as GameObject;
            if (go == null) throw new System.Exception("Object not found");

            // Optimization: Use Stack<string> to avoid O(N^2) string allocations in deep hierarchies.
            var pathParts = new Stack<string>();
            pathParts.Push(go.name);
            Transform t = go.transform.parent;
            while (t != null)
            {
                pathParts.Push(t.name);
                t = t.parent;
            }
            return new JObject { ["status"] = "Success", ["path"] = string.Join("/", pathParts) };
        }

        private static JToken PingObject(JToken p)
        {
            if (p == null || p["instance_id"] == null) throw new System.Exception("instance_id is required");
            var obj = IdToObject((int)p["instance_id"]);
            if (obj == null) throw new System.Exception("Object not found");
            EditorGUIUtility.PingObject(obj);
            return new JObject { ["status"] = "Success", ["message"] = "Pinged" };
        }

        private static JToken FindReferences(JToken p)
        {
            string targetGuid = p?["target_guid"]?.ToString();
            int? targetId = p?["target_id"]?.ToObject<int?>();

            if (string.IsNullOrEmpty(targetGuid) && !targetId.HasValue)
                throw new System.Exception("Either target_guid or target_id is required.");

            Object targetObject = null;
            if (!string.IsNullOrEmpty(targetGuid))
            {
                string path = AssetDatabase.GUIDToAssetPath(targetGuid);
                if (!string.IsNullOrEmpty(path))
                    targetObject = AssetDatabase.LoadAssetAtPath<Object>(path);
            }
            else if (targetId.HasValue)
            {
                targetObject = IdToObject(targetId.Value);
            }

            if (targetObject == null)
                throw new System.Exception("Could not find the target object.");

            var assetRefs = new JArray();
            string searchGuid = targetGuid;

            if (string.IsNullOrEmpty(searchGuid))
            {
                string path = AssetDatabase.GetAssetPath(targetObject);
                if (!string.IsNullOrEmpty(path))
                    searchGuid = AssetDatabase.AssetPathToGUID(path);
            }

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

            var sceneRefs = new JArray();
            var allGameObjects = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(go => go.hideFlags == HideFlags.None || go.hideFlags == HideFlags.NotEditable);

            var targetInstanceIds = new HashSet<int>();
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

            foreach (var go in allGameObjects)
            {
                if (go.scene == null || !go.scene.isLoaded) continue;

                var components = go.GetComponents<Component>();
                bool matches = false;
                List<string> matchingComponents = new List<string>();

                foreach (var comp in components)
                {
                    if (comp == null) continue;

                    using (var so = new SerializedObject(comp))
                    {
                        var prop = so.GetIterator();
                        bool enterChildren = true;
                        while (prop.Next(enterChildren))
                        {
                            enterChildren = true;
                            if (prop.propertyType == SerializedPropertyType.ObjectReference)
                            {
                                if (targetInstanceIds.Contains(prop.objectReferenceInstanceIDValue) || (prop.objectReferenceValue != null && targetInstanceIds.Contains(prop.objectReferenceValue.GetId())))
                                {
                                    matches = true;
                                    matchingComponents.Add(comp.GetType().Name);
                                    break;
                                }
                            }
                        }
                    }
                }

                if (matches)
                {
                    var goData = new JObject();
                    goData["name"] = go.name;
                    goData["instance_id"] = go.GetId();
                    goData["components"] = new JArray(matchingComponents.Distinct());
                    sceneRefs.Add(goData);
                }
            }

            var result = new JObject();
            result["status"] = "Success";
            result["asset_references"] = assetRefs;
            result["scene_references"] = sceneRefs;
            return result;
        }
    }
}