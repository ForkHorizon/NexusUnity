using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Partial implementation for v1.5.0 file and hierarchy operations.
    /// </summary>
    public static partial class MCPServerMethods
    {
        /// <summary>Returns all direct children of a GameObject.</summary>
        private static JToken GetChildren(JToken p)
        {
            if (p?["instance_id"] == null) throw new System.Exception("instance_id required");
            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
            if (go == null) throw new System.Exception("Object not found");
            var children = new JArray();
            for (int i = 0; i < go.transform.childCount; i++)
            {
                var child = go.transform.GetChild(i).gameObject;
                children.Add(new JObject { ["name"] = child.name, ["instance_id"] = child.GetInstanceID(), ["active"] = child.activeSelf });
            }
            return children;
        }

        /// <summary>Duplicates a GameObject (Undo-supported).</summary>
        private static JToken DuplicateObject(JToken p)
        {
            if (p?["instance_id"] == null) throw new System.Exception("instance_id required");
            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
            if (go == null) throw new System.Exception("Object not found");
            var copy = Object.Instantiate(go, go.transform.parent);
            copy.name = go.name; // Remove "(Clone)" suffix
            Undo.RegisterCreatedObjectUndo(copy, "Duplicate Object");
            Selection.activeGameObject = copy;
            return new JObject { ["name"] = copy.name, ["instance_id"] = copy.GetInstanceID() };
        }

        /// <summary>Enables or disables a GameObject explicitly.</summary>
        private static JToken SetActive(JToken p)
        {
            if (p?["instance_id"] == null || p["active"] == null) throw new System.Exception("instance_id and active required");
            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
            if (go == null) throw new System.Exception("Object not found");
            Undo.RecordObject(go, "Set Active");
            go.SetActive(p["active"].Value<bool>());
            return "OK";
        }

        /// <summary>Enables or disables a specific Component.</summary>
        private static JToken SetEnabled(JToken p)
        {
            if (p?["instance_id"] == null || p["component_name"] == null || p["enabled"] == null) throw new System.Exception("instance_id, component_name, and enabled required");
            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
            var comp = go?.GetComponent(p["component_name"].ToString()) as Behaviour;
            if (comp == null) throw new System.Exception("Component not found or not a Behaviour");
            Undo.RecordObject(comp, "Set Enabled");
            comp.enabled = p["enabled"].Value<bool>();
            return "OK";
        }

        /// <summary>Removes a component from an object (Undo-supported).</summary>
        private static JToken RemoveComponent(JToken p)
        {
            if (p?["instance_id"] == null || p["component_name"] == null) throw new System.Exception("instance_id and component_name required");
            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
            var comp = go?.GetComponent(p["component_name"].ToString());
            if (comp == null) throw new System.Exception("Component not found");
            Undo.DestroyObjectImmediate(comp);
            return "OK";
        }

        /// <summary>Changes object order in hierarchy.</summary>
        private static JToken SetSiblingIndex(JToken p)
        {
            if (p?["instance_id"] == null || p["index"] == null) throw new System.Exception("instance_id and index required");
            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
            if (go == null) throw new System.Exception("Object not found");
            Undo.RecordObject(go.transform, "Set Sibling Index");
            var indexVal = p["index"];
            if (indexVal.Type == JTokenType.String)
            {
                string s = indexVal.ToString().ToLower();
                if (s == "first") go.transform.SetAsFirstSibling();
                else if (s == "last") go.transform.SetAsLastSibling();
                else throw new System.Exception("index must be int, 'first', or 'last'");
            }
            else go.transform.SetSiblingIndex(indexVal.Value<int>());
            return "OK";
        }

        /// <summary>Reads text content from a project file.</summary>
        private static JToken ReadFile(JToken p)
        {
            if (p?["path"] == null) throw new System.Exception("path required");
            string fullPath = ValidatePath(p["path"].ToString());
            if (!System.IO.File.Exists(fullPath)) throw new System.Exception("File not found");
            string content = System.IO.File.ReadAllText(fullPath);
            return new JObject { ["content"] = content, ["length"] = content.Length };
        }

        /// <summary>Writes text content to a project file (triggers import).</summary>
        private static JToken WriteFile(JToken p)
        {
            if (p?["path"] == null || p["content"] == null) throw new System.Exception("path and content required");
            string fullPath = ValidatePath(p["path"].ToString());
            System.IO.File.WriteAllText(fullPath, p["content"].ToString());

            // Calculate relative path for AssetDatabase
            string projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');
            string relativePath = fullPath.Length > projectRoot.Length ? fullPath.Substring(projectRoot.Length + 1) : fullPath;

            AssetDatabase.ImportAsset(relativePath);
            return "OK";
        }

        /// <summary>Returns current editor state flags.</summary>
        private static JToken GetEditorState(JToken p)
        {
            return new JObject
            {
                ["is_playing"] = EditorApplication.isPlaying,
                ["is_paused"] = EditorApplication.isPaused,
                ["is_compiling"] = EditorApplication.isCompiling,
                ["active_scene"] = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path,
                ["platform"] = EditorUserBuildSettings.activeBuildTarget.ToString()
            };
        }

        /// <summary>Pauses or unpauses Play Mode.</summary>
        private static JToken PausePlayMode(JToken p)
        {
            if (p?["value"] != null) EditorApplication.isPaused = p["value"].Value<bool>();
            else EditorApplication.isPaused = !EditorApplication.isPaused;
            return new JObject { ["is_paused"] = EditorApplication.isPaused };
        }

        /// <summary>Advances one frame while paused.</summary>
        private static JToken StepFrame(JToken p)
        {
            EditorApplication.Step();
            return "OK";
        }

        /// <summary>Returns basic project metadata.</summary>
        private static JToken GetProjectInfo(JToken p)
        {
            return new JObject
            {
                ["project_path"] = Application.dataPath.Replace("/Assets", ""),
                ["unity_version"] = Application.unityVersion,
                ["platform"] = EditorUserBuildSettings.activeBuildTarget.ToString(),
                ["product_name"] = Application.productName,
                ["company_name"] = Application.companyName
            };
        }
    }
}
