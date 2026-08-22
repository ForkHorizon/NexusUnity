using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    public static partial class ProjectAuditorWrapper
    {
        private static readonly List<Renderer> _rendererCache = new List<Renderer>();
        private static readonly List<Material> _materialCache = new List<Material>();
        private static readonly Stack<string> _pathStackCache = new Stack<string>();

        private static void ScanSceneHealth(JArray issues)
        {
            var allGOs = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(go => (go.hideFlags & (HideFlags.HideInInspector | HideFlags.HideAndDontSave)) == 0);

            foreach (var go in allGOs)
            {
                if (!go.scene.IsValid() || !go.scene.isLoaded) continue;
                AddMissingScriptIssues(issues, go);
                AddRendererIssues(issues, go);
                AddBrokenPrefabIssue(issues, go);
            }
        }

        private static void AddMissingScriptIssues(JArray issues, GameObject go)
        {
            int missingScripts = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            for (int i = 0; i < missingScripts; i++)
            {
                issues.Add(new JObject {
                    ["type"] = "MissingScript",
                    ["object"] = go.name,
                    ["path"] = GetGameObjectPath(go),
                    ["description"] = "GameObject has a missing script reference."
                });
            }
        }

        private static void AddRendererIssues(JArray issues, GameObject go)
        {
            go.GetComponents<Renderer>(_rendererCache);
            foreach (var renderer in _rendererCache)
            {
                renderer.GetSharedMaterials(_materialCache);
                foreach (var material in _materialCache)
                    AddMaterialIssue(issues, go, material);
            }
        }

        private static void AddMaterialIssue(JArray issues, GameObject go, Material material)
        {
            if (material == null)
            {
                issues.Add(new JObject {
                    ["type"] = "MissingMaterial",
                    ["object"] = go.name,
                    ["path"] = GetGameObjectPath(go),
                    ["description"] = "Renderer has a null material entry."
                });
                return;
            }

            if (material.shader != null && material.shader.name == "Hidden/InternalErrorShader")
            {
                issues.Add(new JObject {
                    ["type"] = "PinkMaterial",
                    ["object"] = go.name,
                    ["path"] = GetGameObjectPath(go),
                    ["description"] = "Material is using the error shader (Pink)."
                });
            }
        }

        private static void AddBrokenPrefabIssue(JArray issues, GameObject go)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(go) ||
                PrefabUtility.GetPrefabInstanceStatus(go) != PrefabInstanceStatus.MissingAsset)
                return;

            issues.Add(new JObject {
                ["type"] = "BrokenPrefab",
                ["object"] = go.name,
                ["path"] = GetGameObjectPath(go),
                ["description"] = "Prefab instance is missing its source asset."
            });
        }

        private static string GetGameObjectPath(GameObject obj)
        {
            _pathStackCache.Clear();
            _pathStackCache.Push(obj.name);
            var current = obj.transform;
            while (current.parent != null)
            {
                current = current.parent;
                _pathStackCache.Push(current.name);
            }
            return string.Join("/", _pathStackCache);
        }
    }
}
