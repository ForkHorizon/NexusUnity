using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Registers JSON-RPC methods for Unity scene lifecycle operations and GameObject creation/destruction.
    /// </summary>
    /// <remarks>
    /// Scene operations can save dirty scenes, open or create scenes, write scene assets, call <see cref="AssetDatabase.SaveAssets"/>,
    /// dirty editor state, and register GameObject changes with Unity Undo. Invalid asset paths are rejected before scene I/O.
    /// </remarks>
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
            string path = ValidateAssetPath(p["path"].ToString());
            
            EnsureCurrentSceneSaved();
            
            var scene = EditorSceneManager.OpenScene(path);
            return new JObject { ["status"] = "Success", ["message"] = $"Opened scene {scene.name}" };
        }

        private static JToken CreateScene(JToken p)
        {
            string path = p?["path"]?.ToString();
            if (!string.IsNullOrWhiteSpace(path))
            {
                path = ValidateAssetPath(path);
            }

            if (!string.IsNullOrWhiteSpace(path) && p?["open_if_exists"]?.Value<bool>() == true)
            {
                string fullPath = ValidatePath(path);
                if (File.Exists(fullPath))
                {
                    EnsureCurrentSceneSaved();
                    var existing = EditorSceneManager.OpenScene(path);
                    return new JObject { ["status"] = "Success", ["message"] = $"Opened existing scene {existing.name}", ["path"] = path };
                }
            }

            string name = p?["name"]?.ToString();
            if (string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(path))
            {
                name = Path.GetFileNameWithoutExtension(path);
            }
            if (string.IsNullOrWhiteSpace(name)) name = "New Scene";

            EnsureCurrentSceneSaved();
            
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);
            scene.name = name;
            if (!string.IsNullOrWhiteSpace(path))
            {
                var saved = InternalSaveScene(scene, path) as JObject;
                saved["scene_name"] = name;
                return saved;
            }

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
                NexusEditorLog.Log(NexusLogCategory.Api, $"[MCP] Saving new scene to default path: {path}", true);
            }

            path = ValidateAssetPath(path);
            string fullPath = ValidatePath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            bool result = EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.SaveAssets();
            return new JObject { ["status"] = result ? "Success" : "Failed", ["message"] = result ? $"Saved scene to {path}" : "Failed to save scene" };
        }

        private static JToken GetGameObject(JToken p)
        {
            if (p == null || p["instance_id"] == null) throw new Exception("instance_id is required");
            var id = MCPServerMethods.ExtractId(p);
            var go = MCPServerMethods.IdToObject(id) as GameObject;
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
                var parent = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p, "parent_id")) as GameObject;
                if (parent != null) go.transform.SetParent(parent.transform);
            }
            Undo.RegisterCreatedObjectUndo(go, "Create Object");
            Selection.activeGameObject = go;
            return new JObject { ["status"] = "Success", ["data"] = SerializeGameObject(go) };
        }

        private static JToken DestroyGameObject(JToken p)
        {
            if (p == null || p["instance_id"] == null) throw new Exception("instance_id is required");
            var go = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p)) as GameObject;
            if (go == null) throw new Exception("GameObject not found");
            Undo.DestroyObjectImmediate(go);
            return new JObject { ["status"] = "Success", ["message"] = "Destroyed" };
        }
    }
}
