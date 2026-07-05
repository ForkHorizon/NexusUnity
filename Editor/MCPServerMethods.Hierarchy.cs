using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Registers JSON-RPC methods for Unity hierarchy editing and scoped file reads/writes inside the project.
    /// </summary>
    /// <remarks>
    /// Hierarchy operations can duplicate objects, create nested GameObjects, add/remove components, set active/enabled states,
    /// reorder siblings, and register changes with Unity Undo. File operations are constrained by Nexus path validation before
    /// reading or writing project files.
    /// </remarks>
    public static partial class MCPServerMethods
    {
        private static DateTime _scriptRefreshBusyUntilUtc;

        private static void RegisterHierarchyMethods()
        {
            _methods["duplicate_object"] = DuplicateObject;
            _methods["get_children"] = GetChildren;
            _methods["set_active"] = SetActive;
            _methods["set_enabled"] = SetEnabled;
            _methods["set_sibling_index"] = SetSiblingIndex;
            _methods["create_hierarchy"] = CreateHierarchy;
            _methods["remove_component"] = RemoveComponent;
            _methods["read_file"] = ReadFile;
            _methods["write_file"] = WriteFile;
            _methods["write_files_batch"] = WriteFilesBatch;
        }

        /// <summary>Creates a full hierarchy of objects and components in one call.</summary>
        private static JToken CreateHierarchy(JToken p)
        {
            var parent = p["parent_id"] != null ? MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p, "parent_id")) as GameObject : null;
            var tree = p["tree"];
            if (tree == null) throw new System.Exception("tree required");

            GameObject root = CreateHierarchyRecursive(tree, parent?.transform);
            return new JObject { ["status"] = "Success", ["data"] = SerializeGameObject(root) };
        }

        private static GameObject CreateHierarchyRecursive(JToken node, Transform parent)
        {
            string name = node["name"]?.ToString() ?? "New GameObject";
            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Hierarchy");
            if (parent != null) go.transform.SetParent(parent, false);

            AddComponentsToHierarchyObject(go, node["components"] as JArray);
            ApplyPropertiesToHierarchyObject(go, node["properties"] as JObject);
            AddChildrenToHierarchyObject(go, node["children"] as JArray);

            return go;
        }

        private static void AddComponentsToHierarchyObject(GameObject go, JArray components)
        {
            if (components == null) return;
            foreach (var c in components)
            {
                Type type = FindType(c.ToString());
                if (type != null) Undo.AddComponent(go, type);
            }
        }

        private static void ApplyPropertiesToHierarchyObject(GameObject go, JObject properties)
        {
            if (properties == null) return;

            var componentsList = UnityEngine.Pool.ListPool<Component>.Get();
            try
            {
                go.GetComponents(componentsList);

                foreach (var compPair in properties)
                {
                    Component comp = null;
                    foreach (var c in componentsList)
                    {
                        if (c == null) continue;
                        var type = c.GetType();
                        string typeName = GetTypeName(type);
                        if (typeName == compPair.Key || type.FullName == compPair.Key)
                        {
                            comp = c;
                            break;
                        }
                    }

                    if (comp == null) continue;

                    SerializedObject so = new SerializedObject(comp);
                    var data = compPair.Value as JObject;
                    if (data != null)
                    {
                        foreach (var propPair in data)
                        {
                            SerializedProperty prop = so.FindProperty(propPair.Key);
                            if (prop != null) ApplyValueToProperty(prop, propPair.Value);
                        }
                    }
                    so.ApplyModifiedProperties();
                }
            }
            finally
            {
                UnityEngine.Pool.ListPool<Component>.Release(componentsList);
            }
        }

        private static void AddChildrenToHierarchyObject(GameObject go, JArray children)
        {
            if (children == null) return;
            foreach (var child in children)
            {
                CreateHierarchyRecursive(child, go.transform);
            }
        }

        /// <summary>Returns all direct children of a GameObject.</summary>
        private static JToken GetChildren(JToken p)
        {
            if (p?["instance_id"] == null) throw new System.Exception("instance_id required");
            var go = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p)) as GameObject;
            if (go == null) throw new System.Exception("Object not found");
            var children = new JArray();
            for (int i = 0; i < go.transform.childCount; i++)
            {
                var child = go.transform.GetChild(i).gameObject;
                children.Add(new JObject { ["name"] = child.name, ["instance_id"] = child.GetRawId(), ["active"] = child.activeSelf });
            }
            return new JObject { ["children"] = children };
        }

        /// <summary>Duplicates a GameObject (Undo-supported).</summary>
        private static JToken DuplicateObject(JToken p)
        {
            if (p?["instance_id"] == null) throw new System.Exception("instance_id required");
            var go = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p)) as GameObject;
            if (go == null) throw new System.Exception("Object not found");
            var copy = UnityEngine.Object.Instantiate(go, go.transform.parent);
            copy.name = go.name; // Remove "(Clone)" suffix
            Undo.RegisterCreatedObjectUndo(copy, "Duplicate Object");
            Selection.activeGameObject = copy;
            return new JObject { ["name"] = copy.name, ["instance_id"] = copy.GetRawId() };
        }

        /// <summary>Enables or disables a GameObject explicitly.</summary>
        private static JToken SetActive(JToken p)
        {
            if (p?["instance_id"] == null || p["active"] == null) throw new System.Exception("instance_id and active required");
            var go = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p)) as GameObject;
            if (go == null) throw new System.Exception("Object not found");
            Undo.RecordObject(go, "Set Active");
            go.SetActive(p["active"].Value<bool>());
            return new JObject { ["status"] = "Success", ["message"] = "OK" };
        }

        /// <summary>Enables or disables a specific Component.</summary>
        private static JToken SetEnabled(JToken p)
        {
            if (p == null || p.Type != JTokenType.Object)
                throw new System.ArgumentException("Parameters must be a JSON object");

            var pObj = (JObject)p;
            if (pObj.Count != 3 || p["instance_id"] == null || p["component_name"] == null || p["enabled"] == null)
                throw new System.ArgumentException("Strict schema validation failed: exactly instance_id, component_name, and enabled are required, with no extra properties.");

            var go = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p)) as GameObject;
            var comp = go?.GetComponent(p["component_name"].ToString()) as Behaviour;
            if (comp == null) throw new System.Exception("Component not found or not a Behaviour");
            Undo.RecordObject(comp, "Set Enabled");
            comp.enabled = p["enabled"].Value<bool>();
            return new JObject { ["status"] = "Success", ["message"] = "OK" };
        }

        /// <summary>Removes a component from an object (Undo-supported).</summary>
        private static JToken RemoveComponent(JToken p)
        {
            if (p?["instance_id"] == null || p["component_name"] == null) throw new System.Exception("instance_id and component_name required");
            var go = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p)) as GameObject;
            var comp = go?.GetComponent(p["component_name"].ToString());
            if (comp == null) throw new System.Exception("Component not found");
            Undo.DestroyObjectImmediate(comp);
            return new JObject { ["status"] = "Success", ["message"] = "OK" };
        }

        /// <summary>Changes object order in hierarchy.</summary>
        private static JToken SetSiblingIndex(JToken p)
        {
            if (p?["instance_id"] == null || p["index"] == null) throw new System.Exception("instance_id and index required");
            var go = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p)) as GameObject;
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
            return new JObject { ["status"] = "Success", ["message"] = "OK" };
        }

        /// <summary>Reads text content from a project file.</summary>
        private static JToken ReadFile(JToken p)
        {
            if (p?["path"] == null) throw new System.Exception("path required");
            string path = ValidatePath(p["path"].ToString());
            if (!System.IO.File.Exists(path)) throw new System.Exception("File not found");
            string content = System.IO.File.ReadAllText(path);
            return new JObject { ["content"] = content, ["length"] = content.Length };
        }

        /// <summary>Writes text content to a project file (triggers import).</summary>
        private static JToken WriteFile(JToken p)
        {
            if (p?["path"] == null || p["content"] == null) throw new System.Exception("path and content required");
            string fullPath = ValidatePath(p["path"].ToString());
            bool isScript = IsCSharpScriptPath(fullPath);
            if (isScript) RequireScriptWriteConfirmation(p);

            EnsureParentDirectory(fullPath);
            System.IO.File.WriteAllText(fullPath, p["content"].ToString());

            // Convert to relative path for AssetDatabase
            string root = System.IO.Path.GetFullPath(".");
            string relativePath = fullPath.Substring(root.Length).TrimStart(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar).Replace("\\", "/");

            if (!isScript)
            {
                // Non-scripts don't trigger domain reload, safe to run synchronously
                AssetDatabase.ImportAsset(relativePath);
            }
            else
            {
                TriggerSafeAssetRefresh();
            }

            return new JObject { ["status"] = "Success", ["message"] = "OK" };
        }

        /// <summary>Writes multiple files in a single pass to save AI context.</summary>
        private static JToken WriteFilesBatch(JToken p)
        {
            var files = p?["files"] as JArray;
            if (files == null) throw new System.Exception("files array required");

            bool hasScripts = false;
            string root = System.IO.Path.GetFullPath(".");

            foreach (var f in files)
            {
                if (f["path"] == null || f["content"] == null) continue;
                if (IsCSharpScriptPath(ValidatePath(f["path"].ToString())))
                {
                    hasScripts = true;
                    break;
                }
            }

            if (hasScripts) RequireScriptWriteConfirmation(p);

            foreach (var f in files)
            {
                if (f["path"] == null || f["content"] == null) continue;
                string fullPath = ValidatePath(f["path"].ToString());
                EnsureParentDirectory(fullPath);
                System.IO.File.WriteAllText(fullPath, f["content"].ToString());

                string relativePath = fullPath.Substring(root.Length).TrimStart(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar).Replace("\\", "/");
                
                if (IsCSharpScriptPath(fullPath))
                {
                    hasScripts = true;
                }
                else
                {
                    AssetDatabase.ImportAsset(relativePath);
                }
            }

            if (hasScripts)
            {
                TriggerSafeAssetRefresh();
            }

            return new JObject { ["status"] = "Success", ["message"] = $"Wrote {files.Count} files." };
        }

        private static void EnsureParentDirectory(string fullPath)
        {
            string directory = System.IO.Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) System.IO.Directory.CreateDirectory(directory);
        }

        private static void TriggerSafeAssetRefresh()
        {
            _scriptRefreshBusyUntilUtc = DateTime.UtcNow.AddSeconds(8);

            // Scripts trigger domain reload which blocks the HTTP response.
            // Unity strictly aborts Domain Reloads if the Editor window is in the background 
            // to protect external IDE development. We use LaunchServices (open -a) to explicitly
            // foreground the Editor, and wait for true OS focus before triggering the Refresh.
            
            #if UNITY_EDITOR_OSX
            AppNapBypass.ScheduleActivation();
            #endif

            EditorApplication.CallbackFunction waitForFocus = null;
            waitForFocus = () => {
                EditorApplication.update -= waitForFocus;
                
                // Trigger refresh immediately. With App Nap bypassed, this is reliable 
                // even if the focus-switch (open -a) is still in flight.
                AssetDatabase.Refresh();
            };
            
            EditorApplication.update += waitForFocus;
        }
    }
}
