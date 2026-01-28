using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Partial implementation of MCPServerMethods handling Scene and GameObject lifecycle.
    /// </summary>
    public static partial class MCPServerMethods
    {
        private static JToken OpenScene(JToken p)
        {
            if (p == null || p["path"] == null) throw new Exception("path is required");
            string path = p["path"].ToString();
            var scene = EditorSceneManager.OpenScene(path);
            return $"Opened scene {scene.name}";
        }

        private static JToken CreateScene(JToken p)
        {
            string name = p?["name"]?.ToString() ?? "New Scene";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);
            scene.name = name;
            return $"Created scene {name}";
        }

        private static JToken SaveScene(JToken p)
        {
            var scene = EditorSceneManager.GetActiveScene();
            bool result = EditorSceneManager.SaveScene(scene);
            return result ? $"Saved scene {scene.name}" : "Failed to save scene";
        }

        private static JToken GetGameObject(JToken p)
        {
            if (p == null || p["instance_id"] == null) throw new Exception("instance_id is required");
            int id = (int)p["instance_id"];
            var go = EditorUtility.InstanceIDToObject(id) as GameObject;
            if (go == null) throw new Exception($"GameObject {id} not found");
            return SerializeGameObject(go);
        }

        private static JToken CreateGameObject(JToken p)
        {
            if (p == null || p["name"] == null) throw new Exception("name is required");
            string name = p["name"].ToString();
            GameObject go = new GameObject(name);
            if (p["parent_id"] != null)
            {
                var parent = EditorUtility.InstanceIDToObject((int)p["parent_id"]) as GameObject;
                if (parent != null) go.transform.SetParent(parent.transform);
            }
            Undo.RegisterCreatedObjectUndo(go, "Create Object");
            Selection.activeGameObject = go;
            return SerializeGameObject(go);
        }

        private static JToken DestroyGameObject(JToken p)
        {
            if (p == null || p["instance_id"] == null) throw new Exception("instance_id is required");
            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
            if (go == null) throw new Exception("GameObject not found");
            Undo.DestroyObjectImmediate(go);
            return "Destroyed";
        }
    }
}
