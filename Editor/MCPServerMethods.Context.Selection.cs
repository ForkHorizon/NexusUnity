using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        private static JToken GetSelectedObjectFullContext(JToken p)
        {
            var go = Selection.activeGameObject;
            if (go == null) throw new Exception("No GameObject is currently selected.");

            var result = new JObject { ["status"] = "Success" };
            AddSelectedObjectDetails(result, go);
            result["path"] = BuildTransformPath(go.transform);
            AddPrefabDetails(result, go);
            result["components"] = SerializeSelectedComponents(go);
            AddHierarchyDetails(result, go);
            return result;
        }

        private static void AddSelectedObjectDetails(JObject result, GameObject go)
        {
            result["name"] = go.name;
            result["instance_id"] = go.GetRawId();
            result["active"] = go.activeSelf;
            result["tag"] = go.tag;
            result["layer"] = LayerMask.LayerToName(go.layer);
        }

        private static string BuildTransformPath(Transform transform)
        {
            var pathParts = new Stack<string>();
            for (var current = transform; current != null; current = current.parent)
                pathParts.Push(current.name);
            return string.Join("/", pathParts);
        }

        private static void AddPrefabDetails(JObject result, GameObject go)
        {
            result["is_prefab_instance"] = PrefabUtility.IsPartOfPrefabInstance(go);
            result["prefab_asset_type"] = PrefabUtility.GetPrefabAssetType(go).ToString();
            result["prefab_instance_status"] = PrefabUtility.GetPrefabInstanceStatus(go).ToString();
        }

        private static JArray SerializeSelectedComponents(GameObject go)
        {
            var components = new JArray();
            foreach (var component in go.GetComponents<Component>())
                components.Add(component == null ? CreateMissingScriptSnapshot() : SerializeComponentSnapshot(component, true));
            return components;
        }

        private static JObject CreateMissingScriptSnapshot()
        {
            return new JObject { ["type"] = "MissingScript", ["status"] = "Broken" };
        }

        private static void AddHierarchyDetails(JObject result, GameObject go)
        {
            if (go.transform.parent != null)
                result["parent"] = CreateObjectReference(go.transform.parent.gameObject);

            var children = new JArray();
            foreach (Transform child in go.transform)
                children.Add(CreateObjectReference(child.gameObject));
            if (children.Count > 0) result["children"] = children;
        }

        private static JObject CreateObjectReference(GameObject go)
        {
            return new JObject
            {
                ["name"] = go.name,
                ["instance_id"] = go.GetRawId()
            };
        }
    }
}
