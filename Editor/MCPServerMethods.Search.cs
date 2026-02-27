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
        }

        private static JToken FindByPath(JToken p)
        {
            if (p == null || p["path"] == null) throw new System.Exception("path required");
            string path = p["path"].ToString();
            var go = GameObject.Find(path);
            if (go == null) throw new System.Exception($"Object at path '{path}' not found");
            return SerializeGameObject(go);
        }

        private static JToken FindObjects(JToken p)
        {
            string name = p?["name"]?.ToString();
            string tag = p?["tag"]?.ToString();
            string typeName = p?["type"]?.ToString();
            
            var results = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(go => go.hideFlags == HideFlags.None); // Filter out internal objects

            if (!string.IsNullOrEmpty(name))
                results = results.Where(go => System.Text.RegularExpressions.Regex.IsMatch(go.name, name, System.Text.RegularExpressions.RegexOptions.IgnoreCase));

            if (!string.IsNullOrEmpty(tag))
                results = results.Where(go => go.CompareTag(tag));

            if (!string.IsNullOrEmpty(typeName))
                results = results.Where(go => go.GetComponent(typeName) != null);

            return new JArray(results.Take(50).Select(SerializeGameObject));
        }

        private static JToken GetObjectPath(JToken p)
        {
            if (p == null || p["instance_id"] == null) throw new System.Exception("instance_id is required");
            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
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
            return string.Join("/", pathParts);
        }

        private static JToken PingObject(JToken p)
        {
            if (p == null || p["instance_id"] == null) throw new System.Exception("instance_id is required");
            var obj = EditorUtility.InstanceIDToObject((int)p["instance_id"]);
            if (obj == null) throw new System.Exception("Object not found");
            EditorGUIUtility.PingObject(obj);
            return "Pinged";
        }
    }
}