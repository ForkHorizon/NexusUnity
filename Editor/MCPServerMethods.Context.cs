using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        private static void RegisterContextMethods()
        {
            _methods["get_selected_object_full_context"] = GetSelectedObjectFullContext;
            _methods["show_unresolved_missing_references"] = ShowUnresolvedMissingReferences;
        }

        private static JToken GetSelectedObjectFullContext(JToken p)
        {
            var go = Selection.activeGameObject;
            if (go == null) throw new Exception("No GameObject is currently selected.");

            JObject result = new JObject
            {
                ["status"] = "Success"
            };

            // Basic Details
            result["name"] = go.name;
            result["instance_id"] = go.GetRawId();
            result["active"] = go.activeSelf;
            result["tag"] = go.tag;
            result["layer"] = LayerMask.LayerToName(go.layer);

            // Path
            var pathParts = new Stack<string>();
            pathParts.Push(go.name);
            Transform t = go.transform.parent;
            while (t != null)
            {
                pathParts.Push(t.name);
                t = t.parent;
            }
            result["path"] = string.Join("/", pathParts);

            // Prefab Status
            result["is_prefab_instance"] = PrefabUtility.IsPartOfPrefabInstance(go);
            result["prefab_asset_type"] = PrefabUtility.GetPrefabAssetType(go).ToString();
            result["prefab_instance_status"] = PrefabUtility.GetPrefabInstanceStatus(go).ToString();

            // Components (Full deep dump)
            JArray components = new JArray();
            var comps = go.GetComponents<Component>();
            foreach (var comp in comps)
            {
                if (comp == null)
                {
                    components.Add(new JObject { ["type"] = "MissingScript", ["status"] = "Broken" });
                    continue;
                }
                
                // Using the Snapshot serializer
                components.Add(SerializeComponentSnapshot(comp, true));
            }
            result["components"] = components;

            // Hierarchy relationships
            if (go.transform.parent != null)
            {
                result["parent"] = new JObject
                {
                    ["name"] = go.transform.parent.name,
                    ["instance_id"] = go.transform.parent.gameObject.GetRawId()
                };
            }

            JArray children = new JArray();
            foreach (Transform child in go.transform)
            {
                children.Add(new JObject
                {
                    ["name"] = child.name,
                    ["instance_id"] = child.gameObject.GetRawId()
                });
            }
            if (children.Count > 0) result["children"] = children;

            return result;
        }

        private static JToken ShowUnresolvedMissingReferences(JToken p)
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var allGOs = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(go => go.scene == activeScene && (go.hideFlags == HideFlags.None || go.hideFlags == HideFlags.NotEditable));

            JArray missingRefs = new JArray();

            using (UnityEngine.Pool.ListPool<Component>.Get(out var components))
            {
                foreach (var go in allGOs)
                {
                    go.GetComponents(components);
                    JArray goIssues = new JArray();

                    foreach (var comp in components)
                    {
                        if (comp == null)
                        {
                            goIssues.Add(new JObject { ["type"] = "MissingScript" });
                            continue;
                        }

                        using (var so = new SerializedObject(comp))
                        {
                            var prop = so.GetIterator();
                            bool enterChildren = true;
                            while (prop.Next(enterChildren))
                            {
                                enterChildren = true;
                                if (prop.propertyType == SerializedPropertyType.ObjectReference)
                                {
                                    // If instance ID is non-zero, it means a reference WAS here.
                                    // If the objectReferenceValue is null, the target is missing/destroyed.
                                    if (prop.objectReferenceEntityIdValue != default && prop.objectReferenceValue == null)
                                    {
                                        goIssues.Add(new JObject 
                                        { 
                                            ["type"] = "MissingReference",
                                            ["component"] = comp.GetType().Name,
                                            ["field"] = prop.propertyPath
                                        });
                                    }
                                }
                            }
                        }
                    }

                    if (goIssues.Count > 0)
                    {
                        missingRefs.Add(new JObject
                        {
                            ["name"] = go.name,
                            ["instance_id"] = go.GetRawId(),
                            ["issues"] = goIssues
                        });
                    }
                }
            }

            return new JObject 
            { 
                ["status"] = "Success",
                ["missing_references"] = missingRefs 
            };
        }
    }
}