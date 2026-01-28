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

            string path = go.name;
            Transform t = go.transform.parent;
            while (t != null)
            {
                path = t.name + "/" + path;
                t = t.parent;
            }
            return path;
        }

        private static JToken ListScenes(JToken p)
        {
            var guids = AssetDatabase.FindAssets("t:Scene");
            return new JArray(guids.Select(AssetDatabase.GUIDToAssetPath));
        }

        private static JToken SetSelection(JToken p)
        {
            if (p == null || p["instance_ids"] == null) throw new System.Exception("instance_ids (array) is required");
            var ids = p["instance_ids"].ToObject<int[]>();
            var objects = ids.Select(id => EditorUtility.InstanceIDToObject(id)).Where(o => o != null).ToArray();
            Selection.objects = objects;
            return $"Selected {objects.Length} objects";
        }

        private static JToken PingObject(JToken p)
        {
            if (p == null || p["instance_id"] == null) throw new System.Exception("instance_id is required");
            var obj = EditorUtility.InstanceIDToObject((int)p["instance_id"]);
            if (obj == null) throw new System.Exception("Object not found");
            EditorGUIUtility.PingObject(obj);
            return "Pinged";
        }

        private static JToken FocusSceneView(JToken p)
        {
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
                return "Focused";
            }
            return "No active Scene View found";
        }
    }
}
