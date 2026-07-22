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

            results = FilterByName(results, name);

            if (!string.IsNullOrEmpty(tag))
                results = results.Where(go => go.CompareTag(tag));

            return new JObject { ["objects"] = new JArray(results.Take(50).Select(SerializeGameObject)) };
        }

        private static IEnumerable<GameObject> FilterByName(IEnumerable<GameObject> results, string name)
        {
            if (string.IsNullOrEmpty(name)) return results;

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
                return results.Where(go =>
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

            return results.Where(go => go.name.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0);
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
    }
}
