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

            string root = System.IO.Path.GetFullPath(".");
            string relativePath = fullPath.Substring(root.Length).TrimStart(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar).Replace("\\", "/");

            if (!isScript)
            {
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

        private static EditorApplication.CallbackFunction _pendingRefreshCallback;

        static MCPServerMethods()
        {
            AssemblyReloadEvents.beforeAssemblyReload += CleanupPendingRefresh;
            EditorApplication.quitting += CleanupPendingRefresh;
        }

        private static void CleanupPendingRefresh()
        {
            if (_pendingRefreshCallback != null)
            {
                EditorApplication.update -= _pendingRefreshCallback;
                _pendingRefreshCallback = null;
            }
        }

        private static void TriggerSafeAssetRefresh()
        {
            CleanupPendingRefresh();

            _scriptRefreshBusyUntilUtc = DateTime.UtcNow.AddSeconds(8);

            #if UNITY_EDITOR_OSX
            AppNapBypass.ScheduleActivation();
            #endif

            double startTime = EditorApplication.timeSinceStartup;
            double timeoutSeconds = 15.0;

            _pendingRefreshCallback = () => {
                var cb = _pendingRefreshCallback;
                if (cb == null) return;
                EditorApplication.update -= cb;
                _pendingRefreshCallback = null;

                if (EditorApplication.timeSinceStartup - startTime > timeoutSeconds)
                {
                    NexusEditorLog.Warning(NexusLogCategory.Api, "[MCP] TriggerSafeAssetRefresh timed out waiting for OS focus. Refresh aborted.");
                    return;
                }
                
                AssetDatabase.Refresh();
            };
            
            EditorApplication.update += _pendingRefreshCallback;
        }
    }
}
