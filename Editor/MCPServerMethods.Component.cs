using System;
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
        private static JToken AddComponent(JToken p)
        {
            if (p == null || p["instance_id"] == null || p["component_name"] == null) throw new Exception("instance_id and component_name required");
            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
            Type type = FindType(p["component_name"].ToString());
            if (type == null) throw new Exception($"Type '{p["component_name"]}' not found");
            return $"Added {p["component_name"]} to {Undo.AddComponent(go, type).name}";
        }

        private static JToken InspectComponent(JToken p)
        {
            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
            var comp = go?.GetComponent(p["component_name"].ToString());
            if (comp == null) throw new Exception("Component not found");
            return JToken.FromObject(comp, new Newtonsoft.Json.JsonSerializer { ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore });
        }

        private static JToken UpdateComponent(JToken p)
        {
            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
            var comp = go?.GetComponent(p["component_name"].ToString());
            if (comp == null) throw new Exception("Component not found");
            Undo.RecordObject(comp, "Update Component");
            JsonUtility.FromJsonOverwrite(p["json_data"].ToString(), comp);
            return "Updated";
        }

        private static JToken InstantiatePrefab(JToken p)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p["path"].ToString());
            var go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            Undo.RegisterCreatedObjectUndo(go, "Instantiate Prefab");
            return SerializeGameObject(go);
        }

        private static Type FindType(string name)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = a.GetType(name) ?? a.GetTypes().FirstOrDefault(x => x.Name == name);
                if (t != null) return t;
            }
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

        private static JToken GetRootGameObjects(JToken p) => new JArray(UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Select(SerializeGameObject));
        private static JToken GetActiveGameObject(JToken p) => Selection.activeGameObject != null ? SerializeGameObject(Selection.activeGameObject) : JValue.CreateNull();

        private static JToken SetTransform(JToken p)
        {
            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
            Undo.RecordObject(go.transform, "Set Transform");
            if (p["position"] != null) go.transform.position = ParseVector3(p["position"], go.transform.position);
            return SerializeGameObject(go);
        }

        private static JToken SetParent(JToken p)
        {
            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
            var parent = EditorUtility.InstanceIDToObject((int)p["parent_id"]) as GameObject;
            Undo.SetTransformParent(go.transform, parent?.transform, "Set Parent");
            return "Parent set";
        }
    }
}
