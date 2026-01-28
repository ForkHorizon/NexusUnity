using System;
using System.IO;
using System.Threading;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.EditorCoroutines.Editor;
using UnityEngine;
using UnityEngine.UIElements;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Contains methods for processing and executing MCP JSON-RPC requests.
    /// </summary>
    public static class MCPServerMethods
    {
        /// <summary>
        /// Processes a JSON-RPC request string and returns the response string.
        /// </summary>
        public static string ProcessJsonRpc(string json)
        {
            try
            {
                JObject request = JObject.Parse(json);
                JToken id = request["id"];

                if (request["method"] == null)
                {
                    return CreateErrorResponse(id, -32600, "Invalid Request: method missing");
                }

                string method = request["method"].ToString();
                JToken requestParams = request["params"];
                return ExecuteOnMainThread(method, requestParams, id);
            }
            catch
            {
                return CreateErrorResponse(null, -32700, "Parse error");
            }
        }

        private static string ExecuteOnMainThread(string method, JToken requestParams, JToken id)
        {
            JToken result = null;
            string error = null;

            using (var signal = new ManualResetEventSlim(false))
            {
                MCPServerWindow.Enqueue(() => {
                    try
                    {
                        result = ExecuteMethod(method, requestParams);
                    }
                    catch (Exception e)
                    {
                        error = e.Message;
                    }
                    finally
                    {
                        signal.Set();
                    }
                });

                if (!signal.Wait(10000)) error = "Request timed out waiting for Main Thread";
            }

            return CreateJsonResponse(id, result, error);
        }

        private static string CreateJsonResponse(JToken id, JToken result, string error)
        {
            JObject response = new JObject { ["jsonrpc"] = "2.0", ["id"] = id };
            if (error != null)
                response["error"] = new JObject { ["code"] = -32000, ["message"] = error };
            else
                response["result"] = result;
            return response.ToString();
        }

        /// <summary>
        /// Creates a JSON-RPC error response string.
        /// </summary>
        public static string CreateErrorResponse(JToken id, int code, string message)
        {
            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["error"] = new JObject { ["code"] = code, ["message"] = message },
                ["id"] = id
            }.ToString();
        }

        private static JToken ExecuteMethod(string method, JToken p)
        {
            switch (method)
            {
                case "initialize": return Initialize(p);
                case "create_primitive": return CreatePrimitive(p);
                case "attach_script": return AttachScript(p);
                case "read_logs": return ReadLogs(p);
                case "clear_logs": return ClearLogs(p);
                case "ui_list_windows": return UIListWindows(p);
                case "ui_get_hierarchy": return UIGetHierarchy(p);
                case "ui_click": return UIClick(p);
                case "ui_input_text": return UIInputText(p);
                case "list_assets": return ListAssets(p);
                case "create_material": return CreateMaterial(p);
                case "refresh_asset_database": return RefreshAssetDatabase(p);
                case "import_asset": return ImportAsset(p);
                case "open_scene": return OpenScene(p);
                case "create_scene": return CreateScene(p);
                case "save_scene": return SaveScene(p);
                case "get_game_object": return GetGameObject(p);
                case "create_game_object": return CreateGameObject(p);
                case "destroy_game_object": return DestroyGameObject(p);
                case "set_transform": return SetTransform(p);
                case "set_parent": return SetParent(p);
                case "add_component": return AddComponent(p);
                case "inspect_component": return InspectComponent(p);
                case "update_component": return UpdateComponent(p);
                case "instantiate_prefab": return InstantiatePrefab(p);
                case "get_root_game_objects": return GetRootGameObjects(p);
                case "get_active_game_object": return GetActiveGameObject(p);
                case "test_coroutine": return TestCoroutine(p);
                default: throw new Exception($"Method not found: {method}");
            }
        }

        #region Component & Prefab

        private static JToken AddComponent(JToken p)
        {
            if (p == null || p["instance_id"] == null || p["component_name"] == null)
                throw new Exception("instance_id and component_name required");

            int id = (int)p["instance_id"];
            string componentName = p["component_name"].ToString();

            var go = EditorUtility.InstanceIDToObject(id) as GameObject;
            if (go == null) throw new Exception($"GameObject {id} not found");

            Type type = FindType(componentName);
            if (type == null) throw new Exception($"Type '{componentName}' not found");

            var component = Undo.AddComponent(go, type);
            return $"Added {componentName} to {go.name}";
        }

        private static JToken InspectComponent(JToken p)
        {
            if (p == null || p["instance_id"] == null || p["component_name"] == null)
                throw new Exception("instance_id and component_name required");

            int id = (int)p["instance_id"];
            string componentName = p["component_name"].ToString();

            var go = EditorUtility.InstanceIDToObject(id) as GameObject;
            if (go == null) throw new Exception($"GameObject {id} not found");

            var component = go.GetComponent(componentName);
            if (component == null) throw new Exception($"Component '{componentName}' not found on {go.name}");

            return JObject.Parse(JsonUtility.ToJson(component, true));
        }

        private static JToken UpdateComponent(JToken p)
        {
            if (p == null || p["instance_id"] == null || p["component_name"] == null || p["json_data"] == null)
                throw new Exception("instance_id, component_name, and json_data required");

            int id = (int)p["instance_id"];
            string componentName = p["component_name"].ToString();
            string jsonData = p["json_data"].ToString(); // Expecting raw JSON string or object?
            // If json_data is a JObject, ToString() returns formatted JSON which FromJsonOverwrite accepts.

            var go = EditorUtility.InstanceIDToObject(id) as GameObject;
            if (go == null) throw new Exception($"GameObject {id} not found");

            var component = go.GetComponent(componentName);
            if (component == null) throw new Exception($"Component '{componentName}' not found on {go.name}");

            Undo.RecordObject(component, "Update Component");
            JsonUtility.FromJsonOverwrite(jsonData, component);

            return $"Updated {componentName} on {go.name}";
        }

        private static JToken InstantiatePrefab(JToken p)
        {
            if (p == null || p["path"] == null) throw new Exception("path is required");
            string path = p["path"].ToString();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new Exception($"Prefab not found at {path}");

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            Undo.RegisterCreatedObjectUndo(instance, "Instantiate Prefab");

            if (p["parent_id"] != null)
            {
                int parentId = (int)p["parent_id"];
                var parent = EditorUtility.InstanceIDToObject(parentId) as GameObject;
                if (parent != null) instance.transform.SetParent(parent.transform);
            }

            if (p["position"] != null) instance.transform.position = ParseVector3(p["position"], instance.transform.position);
            if (p["rotation"] != null) instance.transform.eulerAngles = ParseVector3(p["rotation"], instance.transform.eulerAngles);

            Selection.activeGameObject = instance;
            return SerializeGameObject(instance);
        }

        private static Type FindType(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(name);
                if (type != null) return type;
                // Try ignoring namespace?
                type = assembly.GetTypes().FirstOrDefault(t => t.Name == name);
                if (type != null) return type;
            }
            return null;
        }

        #endregion

        #region GameObject Control

        private static JToken GetGameObject(JToken p)
        {
            if (p == null) throw new Exception("params required");

            GameObject go = null;

            if (p["instance_id"] != null)
            {
                int id = (int)p["instance_id"];
                go = EditorUtility.InstanceIDToObject(id) as GameObject;
            }
            else if (p["name"] != null)
            {
                // Simple search by name (inefficient for large scenes but simple)
                string name = p["name"].ToString();
                go = GameObject.Find(name);
            }

            if (go == null) throw new Exception("GameObject not found");

            return SerializeGameObject(go);
        }

        private static JToken CreateGameObject(JToken p)
        {
            if (p == null || p["name"] == null) throw new Exception("name is required");
            string name = p["name"].ToString();

            GameObject go = new GameObject(name);

            if (p["parent_id"] != null)
            {
                int parentId = (int)p["parent_id"];
                var parent = EditorUtility.InstanceIDToObject(parentId) as GameObject;
                if (parent != null) go.transform.SetParent(parent.transform);
            }

            Undo.RegisterCreatedObjectUndo(go, "Create Object");
            Selection.activeGameObject = go;

            return SerializeGameObject(go);
        }

        private static JToken DestroyGameObject(JToken p)
        {
            if (p == null || p["instance_id"] == null) throw new Exception("instance_id is required");
            int id = (int)p["instance_id"];

            var go = EditorUtility.InstanceIDToObject(id) as GameObject;
            if (go == null) throw new Exception($"GameObject with ID {id} not found");

            Undo.DestroyObjectImmediate(go);
            return $"Destroyed GameObject {id}";
        }

        private static JToken GetRootGameObjects(JToken p)
        {
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            JArray list = new JArray();
            foreach (var go in roots)
            {
                list.Add(SerializeGameObject(go));
            }
            return list;
        }

        private static JToken GetActiveGameObject(JToken p)
        {
            if (Selection.activeGameObject == null) return JValue.CreateNull();
            return SerializeGameObject(Selection.activeGameObject);
        }

        private static JToken SetTransform(JToken p)
        {
            if (p == null || p["instance_id"] == null) throw new Exception("instance_id is required");
            int id = (int)p["instance_id"];

            var go = EditorUtility.InstanceIDToObject(id) as GameObject;
            if (go == null) throw new Exception($"GameObject with ID {id} not found");

            Undo.RecordObject(go.transform, "Set Transform");

            if (p["position"] != null) go.transform.position = ParseVector3(p["position"], go.transform.position);
            if (p["rotation"] != null) go.transform.eulerAngles = ParseVector3(p["rotation"], go.transform.eulerAngles); // Input as Euler
            if (p["scale"] != null) go.transform.localScale = ParseVector3(p["scale"], go.transform.localScale);

            return SerializeGameObject(go);
        }

        private static JToken SetParent(JToken p)
        {
            if (p == null || p["instance_id"] == null) throw new Exception("instance_id is required");
            int id = (int)p["instance_id"];

            var go = EditorUtility.InstanceIDToObject(id) as GameObject;
            if (go == null) throw new Exception($"GameObject with ID {id} not found");

            Transform newParent = null;
            if (p["parent_id"] != null)
            {
                int parentId = (int)p["parent_id"];
                var parentGo = EditorUtility.InstanceIDToObject(parentId) as GameObject;
                if (parentGo != null) newParent = parentGo.transform;
            }

            Undo.SetTransformParent(go.transform, newParent, "Set Parent");
            return $"Set parent of {go.name} to {(newParent != null ? newParent.name : "root")}";
        }

        private static JObject SerializeGameObject(GameObject go)
        {
            return new JObject
            {
                ["name"] = go.name,
                ["instance_id"] = go.GetInstanceID(),
                ["position"] = new JObject { ["x"] = go.transform.position.x, ["y"] = go.transform.position.y, ["z"] = go.transform.position.z },
                ["rotation"] = new JObject { ["x"] = go.transform.eulerAngles.x, ["y"] = go.transform.eulerAngles.y, ["z"] = go.transform.eulerAngles.z },
                ["scale"] = new JObject { ["x"] = go.transform.localScale.x, ["y"] = go.transform.localScale.y, ["z"] = go.transform.localScale.z }
            };
        }

        private static Vector3 ParseVector3(JToken token, Vector3 defaultValue)
        {
            if (token == null) return defaultValue;

            float x = token["x"] != null ? (float)token["x"] : defaultValue.x;
            float y = token["y"] != null ? (float)token["y"] : defaultValue.y;
            float z = token["z"] != null ? (float)token["z"] : defaultValue.z;

            return new Vector3(x, y, z);
        }

        #endregion

        #region Asset Management

        private static JToken ListAssets(JToken p)
        {
            string filter = "t:Object";
            string[] searchInFolders = null;

            if (p != null)
            {
                if (p["filter"] != null) filter = p["filter"].ToString();
                if (p["folders"] != null) searchInFolders = p["folders"].ToObject<string[]>();
            }

            string[] guids = AssetDatabase.FindAssets(filter, searchInFolders);
            JArray list = new JArray();

            // Limit results to avoid massive payloads
            int limit = 100;
            foreach (string guid in guids.Take(limit))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                list.Add(new JObject
                {
                    ["path"] = path,
                    ["guid"] = guid,
                    ["type"] = AssetDatabase.GetMainAssetTypeAtPath(path).Name
                });
            }

            return list;
        }

        private static JToken CreateMaterial(JToken p)
        {
            if (p == null || p["name"] == null) throw new Exception("name is required");
            string name = p["name"].ToString();
            string shaderName = p["shader"] != null ? p["shader"].ToString() : "Standard";

            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new Exception($"Shader '{shaderName}' not found");

            Material mat = new Material(shader);
            string path = Path.Combine("Assets", $"{name}.mat");
            path = AssetDatabase.GenerateUniqueAssetPath(path);

            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();

            return $"Material created at {path}";
        }

        private static JToken RefreshAssetDatabase(JToken p)
        {
            AssetDatabase.Refresh();
            return "AssetDatabase refreshed";
        }

        private static JToken ImportAsset(JToken p)
        {
            if (p == null || p["path"] == null) throw new Exception("path is required");
            string path = p["path"].ToString();
            AssetDatabase.ImportAsset(path);
            return $"Imported asset at {path}";
        }

        #endregion

        #region Scene Management

        private static JToken OpenScene(JToken p)
        {
            if (p == null || p["path"] == null) throw new Exception("path is required");
            string path = p["path"].ToString();

            if (!File.Exists(path)) throw new Exception($"Scene file not found: {path}");

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(path);
                return $"Opened scene: {path}";
            }
            return "Open scene cancelled by user";
        }

        private static JToken CreateScene(JToken p)
        {
            string name = "New Scene";
            if (p != null && p["name"] != null) name = p["name"].ToString();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // We can't set the name until we save, but we can return the path if we saved it?
            // Usually new scenes are untitled.
            return "Created new scene";
        }

        private static JToken SaveScene(JToken p)
        {
            if (p == null || p["path"] == null) throw new Exception("path is required");
            string path = p["path"].ToString();

            if (!path.EndsWith(".unity")) path += ".unity";

            var scene = EditorSceneManager.GetActiveScene();
            bool success = EditorSceneManager.SaveScene(scene, path);

            return success ? $"Saved scene to {path}" : "Failed to save scene";
        }

        #endregion

        #region UI Instruments

        private static JToken UIListWindows(JToken p)
        {
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            JArray list = new JArray();
            foreach (var w in windows)
            {
                if (w.rootVisualElement != null && w.rootVisualElement.visible)
                {
                    list.Add(new JObject {
                        ["title"] = w.titleContent.text,
                        ["type"] = w.GetType().FullName,
                        ["instanceId"] = w.GetInstanceID()
                    });
                }
            }
            return list;
        }

        private static JToken UIGetHierarchy(JToken p)
        {
            if (p == null || p["window_title"] == null) throw new Exception("window_title is required");
            string title = p["window_title"].ToString();

            var w = FindWindow(title);
            if (w == null) throw new Exception($"Window '{title}' not found");

            return SerializeVisualElement(w.rootVisualElement);
        }

        private static JToken UIClick(JToken p)
        {
            if (p == null || p["window_title"] == null || p["element_name"] == null)
                throw new Exception("window_title and element_name are required");

            string title = p["window_title"].ToString();
            string elName = p["element_name"].ToString();

            var w = FindWindow(title);
            if (w == null) throw new Exception($"Window '{title}' not found");

            var el = FindElementByName(w.rootVisualElement, elName);
            if (el == null) throw new Exception($"Element '{elName}' not found in '{title}'");

            using (var evt = ClickEvent.GetPooled())
            {
                evt.target = el;
                el.SendEvent(evt);
            }

            // Also support Button specific .clicked event which might not be triggered by generic SendEvent depending on implementation
            if (el is Button btn)
            {
                // UI Toolkit buttons usually fire via ClickEvent, but we can double check or rely on event.
                // Standard Button.Clicked() is internal or handled via event.
                // Creating a navigation submit event often helps too.
                // But SendEvent(ClickEvent) is the standard way.
            }

            return $"Clicked {elName} in {title}";
        }

        private static JToken UIInputText(JToken p)
        {
            if (p == null || p["window_title"] == null || p["element_name"] == null || p["text"] == null)
                throw new Exception("window_title, element_name, and text are required");

            string title = p["window_title"].ToString();
            string elName = p["element_name"].ToString();
            string text = p["text"].ToString();

            var w = FindWindow(title);
            if (w == null) throw new Exception($"Window '{title}' not found");

            var el = FindElementByName(w.rootVisualElement, elName);
            if (el == null) throw new Exception($"Element '{elName}' not found in '{title}'");

            if (el is TextField tf)
            {
                tf.value = text;
            }
            else if (el is Label lbl)
            {
                lbl.text = text;
            }
            else
            {
                // Generic attempt via reflection for "value" or "text" property?
                // For now, stick to strong types.
                throw new Exception($"Element '{elName}' (Type: {el.GetType().Name}) is not a TextField or Label");
            }

            return $"Set text of {elName} to '{text}'";
        }

        private static EditorWindow FindWindow(string title)
        {
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            // Match exact title or Type name
            return windows.FirstOrDefault(w => w.titleContent.text == title || w.GetType().Name == title);
        }

        private static VisualElement FindElementByName(VisualElement root, string name)
        {
            if (root == null) return null;
            // Depth-first search
            if (root.name == name) return root;

            foreach (var child in root.Children())
            {
                var found = FindElementByName(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static JObject SerializeVisualElement(VisualElement ve)
        {
            var obj = new JObject
            {
                ["name"] = ve.name,
                ["type"] = ve.GetType().Name,
                ["visible"] = ve.visible,
                ["enabled"] = ve.enabledSelf
            };

            if (ve is TextElement te) obj["text"] = te.text;
            if (ve is TextField tf) obj["value"] = tf.value;
            // Add other specific properties as needed

            if (ve.childCount > 0)
            {
                var children = new JArray();
                foreach (var child in ve.Children())
                {
                    children.Add(SerializeVisualElement(child));
                }
                obj["children"] = children;
            }

            return obj;
        }

        #endregion

        private static JToken ReadLogs(JToken p)
        {
            int count = 50;
            string filterType = null;
            string searchText = null;

            if (p != null)
            {
                if (p["count"] != null) count = (int)p["count"];
                if (p["filter_type"] != null) filterType = p["filter_type"].ToString();
                if (p["search_text"] != null) searchText = p["search_text"].ToString();
            }

            var logs = MCPServerWindow.GetLogs(count, filterType, searchText);
            return JArray.FromObject(logs);
        }

        private static JToken ClearLogs(JToken p)
        {
            MCPServerWindow.ClearLogs();
            return "Logs cleared";
        }

        private static JToken Initialize(JToken p)
        {
            return new JObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new JObject(),
                ["serverInfo"] = new JObject { ["name"] = "Unity MCP Server", ["version"] = "0.0.1" }
            };
        }

        private static JToken CreatePrimitive(JToken p)
        {
            if (p == null || p["primitive_type"] == null) throw new Exception("primitive_type is required");
            string typeStr = p["primitive_type"].ToString();
            if (!Enum.TryParse(typeof(PrimitiveType), typeStr, true, out var type))
                throw new Exception($"Invalid primitive type: {typeStr}");

            var go = GameObject.CreatePrimitive((PrimitiveType)type);
            Undo.RegisterCreatedObjectUndo(go, "Create Primitive");
            go.name = "MCP_" + typeStr;
            Selection.activeGameObject = go;
            return $"Created {go.name}";
        }

        private static JToken AttachScript(JToken p)
        {
            if (p == null || p["script_name"] == null) throw new Exception("script_name is required");
            string scriptName = SanitizeScriptName(p["script_name"].ToString());
            string content = (p["script_content"] != null) ? p["script_content"].ToString().Replace("\\n", "\n") :
                GetDefaultScript(scriptName);

            string path = Path.Combine("Assets", $"{scriptName}.cs");
            File.WriteAllText(path, content);

            if (Selection.activeGameObject == null)
            {
                AssetDatabase.Refresh();
                return $"Script {scriptName} created at {path}. No target GameObject selected.";
            }

            SessionState.SetString("MCP_PendingAttach_Script", scriptName);
            SessionState.SetInt("MCP_PendingAttach_GO", Selection.activeGameObject.GetInstanceID());

            AssetDatabase.Refresh();
            return $"Script {scriptName} created at {path}. Compilation triggered.";
        }

        private static string SanitizeScriptName(string name)
        {
            string sanitized = System.Text.RegularExpressions.Regex.Replace(name, @"[^a-zA-Z0-9_]", "_");
            if (string.IsNullOrEmpty(sanitized)) return "NewScript";
            if (char.IsDigit(sanitized[0])) sanitized = "_" + sanitized;
            return sanitized;
        }

        private static string GetDefaultScript(string name)
        {
            return "using UnityEngine;\n\n" +
                   "public class " + name + " : MonoBehaviour\n" +
                   "{\n" +
                   "    void Start()\n" +
                   "    {\n" +
                   "        Debug.Log(\"Hello from " + name + "!\");\n" +
                   "    }\n" +
                   "}";
        }

        private static JToken TestCoroutine(JToken p)
        {
            MCPServerWindow.StartCoroutine(WaitAndLog());
            return "Coroutine started (logs in 2 seconds)";
        }

        private static System.Collections.IEnumerator WaitAndLog()
        {
            Debug.Log("[MCP] Coroutine started");
            yield return new EditorWaitForSeconds(2.0f);
            Debug.Log("[MCP] Coroutine finished after 2 seconds");
        }

        /// <summary>
        /// Checks for scripts that were pending attachment before an assembly reload.
        /// </summary>
        public static void CheckPendingAttachments()
        {
            string pendingScript = SessionState.GetString("MCP_PendingAttach_Script", "");
            int pendingGoId = SessionState.GetInt("MCP_PendingAttach_GO", 0);

            if (string.IsNullOrEmpty(pendingScript) || pendingGoId == 0) return;

            SessionState.EraseString("MCP_PendingAttach_Script");
            SessionState.EraseInt("MCP_PendingAttach_GO");

            var go = EditorUtility.InstanceIDToObject(pendingGoId) as GameObject;
            if (go == null) return;

            Type type = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(pendingScript);
                if (type != null) break;
            }

            if (type != null)
            {
                go.AddComponent(type);
                Debug.Log($"[MCP] Successfully attached {pendingScript} to {go.name}");
            }
            else
            {
                Debug.LogError($"[MCP] Could not find type {pendingScript} to attach.");
            }
        }
    }
}
