#pragma warning disable 0618 // Suppress obsolete InstanceIDToObject/GetInstanceID warnings for stability in 2021.3+
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
        private static void RegisterSceneMethods()
        {
            _methods["open_scene"] = OpenScene;
            _methods["create_scene"] = CreateScene;
            _methods["save_scene"] = SaveScene;
            _methods["get_game_object"] = GetGameObject;
            _methods["create_game_object"] = CreateGameObject;
            _methods["destroy_game_object"] = DestroyGameObject;
            _methods["instantiate_prefab"] = InstantiatePrefab;
        }

        private static JToken OpenScene(JToken p)
        {
            if (p == null || p["path"] == null) throw new Exception("path is required");
            string path = p["path"].ToString();
            
            EnsureCurrentSceneSaved();
            
            var scene = EditorSceneManager.OpenScene(path);
            return new JObject { ["status"] = "Success", ["message"] = $"Opened scene {scene.name}" };
        }

        private static JToken CreateScene(JToken p)
        {
            string name = p?["name"]?.ToString() ?? "New Scene";
            
            EnsureCurrentSceneSaved();
            
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);
            scene.name = name;
            return new JObject { ["status"] = "Success", ["message"] = $"Created scene {name}" };
        }

        private static void EnsureCurrentSceneSaved()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.isDirty)
            {
                InternalSaveScene(scene, null);
            }
        }

        private static JToken SaveScene(JToken p)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            string path = p?["path"]?.ToString();
            return InternalSaveScene(scene, path);
        }

        private static JToken InternalSaveScene(UnityEngine.SceneManagement.Scene scene, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                path = scene.path;
            }

            if (string.IsNullOrEmpty(path))
            {
                path = "Assets/AutoSavedScene.unity";
                Debug.Log($"[MCP] Saving new scene to default path: {path}");
            }

            bool result = EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.SaveAssets();
            return new JObject { ["status"] = result ? "Success" : "Failed", ["message"] = result ? $"Saved scene to {path}" : "Failed to save scene" };
        }

        private static JToken GetGameObject(JToken p)
        {
            if (p == null || p["instance_id"] == null) throw new Exception("instance_id is required");
            int id = (int)p["instance_id"];
            var go = IdToObject(id) as GameObject;
            if (go == null) throw new Exception($"GameObject {id} not found");
            return new JObject { ["status"] = "Success", ["data"] = SerializeGameObject(go) };
        }

        private static JToken CreateGameObject(JToken p)
        {
            if (p == null || p["name"] == null) throw new Exception("name is required");
            string name = p["name"].ToString();
            GameObject go = new GameObject(name);
            if (p["parent_id"] != null)
            {
                var parent = IdToObject((int)p["parent_id"]) as GameObject;
                if (parent != null) go.transform.SetParent(parent.transform);
            }
            Undo.RegisterCreatedObjectUndo(go, "Create Object");
            Selection.activeGameObject = go;
            return new JObject { ["status"] = "Success", ["data"] = SerializeGameObject(go) };
        }

        private static JToken DestroyGameObject(JToken p)
        {
            if (p == null || p["instance_id"] == null) throw new Exception("instance_id is required");
            var go = IdToObject((int)p["instance_id"]) as GameObject;
            if (go == null) throw new Exception("GameObject not found");
            Undo.DestroyObjectImmediate(go);
            return new JObject { ["status"] = "Success", ["message"] = "Destroyed" };
        }
    }
}