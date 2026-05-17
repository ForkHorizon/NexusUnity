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
        private static void RegisterSnapshotMethods()
        {
            _methods["dump_scene_graph"] = DumpSceneGraph;
            _methods["compact_scene_snapshot"] = CompactSceneSnapshot;
            _methods["get_scene_dependencies"] = GetSceneDependencies;
        }

        private static JToken CompactSceneSnapshot(JToken p)
        {
            var jo = p?.DeepClone() as JObject ?? new JObject();
            jo["compact"] = true;
            return DumpSceneGraph(jo);
        }

        private static JToken DumpSceneGraph(JToken p)
        {
            var rootId = ExtractId(p, "root_id");
            int maxDepth = p?["max_depth"]?.Value<int>() ?? 5;
            bool includeAllProperties = p?["include_all_properties"]?.Value<bool>() ?? false;
            bool compact = p?["compact"]?.Value<bool>() ?? false;

            JArray roots = new JArray();
            if (rootId != default)
            {
                var rootGo = IdToObject(rootId) as GameObject;
                if (rootGo != null)
                {
                    roots.Add(SerializeSceneNode(rootGo, 0, maxDepth, includeAllProperties, compact));
                }
            }
            else
            {
                var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

                // Use ListPool to avoid GC allocation from array creation
                using (UnityEngine.Pool.ListPool<GameObject>.Get(out var rootGOs))
                {
                    activeScene.GetRootGameObjects(rootGOs);
                    foreach (var go in rootGOs)
                    {
                        roots.Add(SerializeSceneNode(go, 0, maxDepth, includeAllProperties, compact));
                    }
                }
            }

            return new JObject 
            { 
                ["status"] = "Success",
                ["scene_name"] = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                ["hierarchy"] = roots 
            };
        }

        internal static JObject SerializeSceneNode(GameObject go, int currentDepth, int maxDepth, bool includeAllProperties, bool compact = false)
        {
            JObject node = new JObject
            {
                ["name"] = go.name,
                ["instance_id"] = go.GetRawId()
            };

            if (compact)
            {
                JArray components = new JArray();
                using (UnityEngine.Pool.ListPool<Component>.Get(out var comps))
                {
                    go.GetComponents(comps);
                    foreach (var comp in comps)
                    {
                        if (comp != null) components.Add(comp.GetType().Name);
                    }
                }
                node["components"] = components;
            }
            else
            {
                node["active"] = go.activeSelf;
                node["tag"] = go.tag;
                node["layer"] = LayerMask.LayerToName(go.layer);

                JArray componentNodes = new JArray();
                using (UnityEngine.Pool.ListPool<Component>.Get(out var comps))
                {
                    go.GetComponents(comps);
                    foreach (var comp in comps)
                    {
                        if (comp != null) componentNodes.Add(SerializeComponentSnapshot(comp, includeAllProperties));
                    }
                }
                node["components"] = componentNodes;
            }

            if (currentDepth < maxDepth)
            {
                JArray children = new JArray();
                foreach (Transform child in go.transform)
                {
                    children.Add(SerializeSceneNode(child.gameObject, currentDepth + 1, maxDepth, includeAllProperties, compact));
                }
                if (children.Count > 0) node["children"] = children;
            }

            return node;
        }

        private static JObject SerializeComponentSnapshot(Component comp, bool includeAllProperties)
        {
            var type = comp.GetType();
            JObject obj = new JObject
            {
                ["type"] = type.Name,
                ["instance_id"] = comp.GetRawId()
            };

            if (comp is Behaviour b) obj["enabled"] = b.enabled;

            if (includeAllProperties)
            {
                var so = new SerializedObject(comp);
                var prop = so.GetIterator();
                bool enterChildren = true;
                while (prop.Next(enterChildren))
                {
                    enterChildren = false;
                    obj[prop.name] = SerializeProperty(prop, false);
                }
            }
            else
            {
                obj["properties"] = GetKeyProperties(comp);
            }

            return obj;
        }

        private static JObject GetKeyProperties(Component comp)
        {
            JObject props = new JObject();
            if (comp is Transform t)
            {
                props["position"] = SerializeVector3(t.localPosition);
                props["rotation"] = SerializeVector3(t.localEulerAngles);
                props["scale"] = SerializeVector3(t.localScale);
            }
            else if (comp is Camera cam)
            {
                props["fov"] = cam.fieldOfView;
                props["orthographic"] = cam.orthographic;
                props["nearClip"] = cam.nearClipPlane;
                props["farClip"] = cam.farClipPlane;
            }
            else if (comp is Light l)
            {
                props["type"] = l.type.ToString();
                props["color"] = SerializeColor(l.color);
                props["intensity"] = l.intensity;
                props["range"] = l.range;
            }
            else if (comp is MeshFilter mf)
            {
                props["mesh"] = mf.sharedMesh != null ? mf.sharedMesh.name : "None";
            }
            else if (comp is MeshRenderer mr)
            {
                JArray mats = new JArray();
                foreach (var m in mr.sharedMaterials) if (m != null) mats.Add(m.name);
                props["materials"] = mats;
            }
            return props;
        }

        private static JToken GetSceneDependencies(JToken p)
        {
            var allGOs = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(go => go.hideFlags == HideFlags.None || go.hideFlags == HideFlags.NotEditable);

            var sceneRefs = new JArray();
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            using (UnityEngine.Pool.ListPool<Component>.Get(out var components))
            {
                foreach (var go in allGOs)
                {
                    if (go.scene != activeScene) continue;

                    go.GetComponents(components);
                    foreach (var comp in components)
                    {
                        if (comp == null) continue;

                        using (var so = new SerializedObject(comp))
                        {
                            var prop = so.GetIterator();
                            bool enterChildren = true;
                            while (prop.Next(enterChildren))
                            {
                                enterChildren = true;
                                if (prop.propertyType == SerializedPropertyType.ObjectReference)
                                {
                                    var target = prop.objectReferenceValue;
                                    if (target != null && target is GameObject targetGO && targetGO.scene == activeScene)
                                    {
                                        var dep = new JObject();
                                        dep["from_go"] = go.name;
                                        dep["from_id"] = go.GetRawId();
                                        dep["component"] = comp.GetType().Name;
                                        dep["field"] = prop.name;
                                        dep["to_go"] = targetGO.name;
                                        dep["to_id"] = targetGO.GetRawId();
                                        sceneRefs.Add(dep);
                                    }
                                    else if (target != null && target is Component targetComp && targetComp.gameObject.scene == activeScene)
                                    {
                                        var dep = new JObject();
                                        dep["from_go"] = go.name;
                                        dep["from_id"] = go.GetRawId();
                                        dep["component"] = comp.GetType().Name;
                                        dep["field"] = prop.name;
                                        dep["to_go"] = targetComp.gameObject.name;
                                        dep["to_comp"] = targetComp.GetType().Name;
                                        dep["to_id"] = targetComp.gameObject.GetRawId();
                                        sceneRefs.Add(dep);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return new JObject 
            { 
                ["status"] = "Success",
                ["dependencies"] = sceneRefs 
            };
        }
    }
}