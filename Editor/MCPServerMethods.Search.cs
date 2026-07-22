using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Registers JSON-RPC discovery methods that inspect Unity scenes, selection state, object paths, and object references.
    /// </summary>
    /// <remarks>
    /// Discovery methods read <see cref="UnityEngine.SceneManagement.SceneManager"/> active scene data, <see cref="Selection"/>,
    /// <see cref="Resources.FindObjectsOfTypeAll{T}"/>, and hierarchy paths, then serialize matching GameObjects for MCP clients.
    /// Calls can ping objects in the editor and may traverse serialized references during reference searches.
    /// </remarks>
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

        private static List<GameObject> _rootGameObjectsCache = new List<GameObject>();

        private static JToken GetRootGameObjects(JToken p)
        {
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

            if (!string.IsNullOrEmpty(typeName))
            {
                var type = FindType(typeName);
                if (type == null) return new JObject { ["objects"] = new JArray() };

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

            if (!string.IsNullOrEmpty(name))
            {
                System.Text.RegularExpressions.Regex regex = null;
                try
                {
                    regex = new System.Text.RegularExpressions.Regex(name, System.Text.RegularExpressions.RegexOptions.IgnoreCase, System.TimeSpan.FromMilliseconds(100));
                }
                catch (System.ArgumentException)
                {
                    // Invalid regex pattern (e.g. "Player (1)"), fallback to literal substring search
                }

                if (regex != null)
                {
                    results = results.Where(go =>
                    {
                        try
                        {
                            return regex.IsMatch(go.name);
                        }
                        catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
                        {
                            return go.name.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0;
                        }
                    });
                }
                else
                {
                    results = results.Where(go => go.name.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0);
                }
            }

            if (!string.IsNullOrEmpty(tag))
                results = results.Where(go => go.CompareTag(tag));

            return new JObject { ["objects"] = new JArray(results.Take(50).Select(SerializeGameObject)) };
        }

        private static Stack<string> _pathStackCache = new Stack<string>();

        private static JToken GetObjectPath(JToken p)
        {
            if (p == null || p["instance_id"] == null) throw new System.Exception("instance_id is required");
            var go = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p)) as GameObject;
            if (go == null) throw new System.Exception("Object not found");

            _pathStackCache.Clear();
            _pathStackCache.Push(go.name);
            Transform t = go.transform.parent;
            while (t != null)
            {
                _pathStackCache.Push(t.name);
                t = t.parent;
            }
            return new JObject { ["status"] = "Success", ["path"] = string.Join("/", _pathStackCache) };
        }

        private static JToken PingObject(JToken p)
        {
            if (p == null || p["instance_id"] == null) throw new System.Exception("instance_id is required");
            var obj = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p));
            if (obj == null) throw new System.Exception("Object not found");
            EditorGUIUtility.PingObject(obj);
            return new JObject { ["status"] = "Success", ["message"] = "Pinged" };
        }

        private static JToken FindReferences(JToken p)
        {
            string targetGuid = p?["target_guid"]?.ToString();
            EntityId targetId = MCPServerMethods.ExtractId(p, "target_id");

            if (string.IsNullOrEmpty(targetGuid) && targetId == default)
                throw new System.Exception("Either target_guid or target_id is required.");

            Object targetObject = null;
            if (!string.IsNullOrEmpty(targetGuid))
            {
                string path = AssetDatabase.GUIDToAssetPath(targetGuid);
                if (!string.IsNullOrEmpty(path))
                    targetObject = AssetDatabase.LoadAssetAtPath<Object>(path);
            }
            else if (targetId != default)
            {
                targetObject = MCPServerMethods.IdToObject(targetId);
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

            using (UnityEngine.Pool.ListPool<Component>.Get(out var components))
            {
                foreach (var go in allGameObjects)
                {
                    if (go.scene == null || !go.scene.isLoaded) continue;

                    go.GetComponents(components);
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

                                    if (directMatch || indirectMatch)
                                    {
                                        var matchDetail = new JObject();
                                        matchDetail["component"] = GetTypeName(comp.GetType());
                                        matchDetail["field"] = prop.propertyPath;
                                        goMatches.Add(matchDetail);
                                    }
                                }
                            }
                        }
                    }

                    if (goMatches.Count > 0)
                    {
                        var goData = new JObject();
                        goData["name"] = go.name;
                        goData["instance_id"] = go.GetRawId();
                        goData["references"] = goMatches;
                        sceneRefs.Add(goData);
                    }
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
