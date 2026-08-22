using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
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
                Type type = FindComponentType(c.ToString());
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
                    Component comp = FindMatchingComponent(componentsList, compPair.Key);
                    if (comp == null) continue;

                    var data = compPair.Value as JObject;
                    if (data == null) continue;

                    SerializedObject so = new SerializedObject(comp);
                    foreach (var propPair in data)
                    {
                        SerializedProperty sp = so.FindProperty(propPair.Key);
                        if (sp != null) ApplyValueToProperty(sp, propPair.Value);
                    }
                    so.ApplyModifiedProperties();
                }
            }
            finally
            {
                UnityEngine.Pool.ListPool<Component>.Release(componentsList);
            }
        }

        private static Component FindMatchingComponent(System.Collections.Generic.List<Component> components, string targetName)
        {
            foreach (var c in components)
            {
                if (c == null) continue;
                var type = c.GetType();
                string typeName = GetTypeName(type);
                if (typeName == targetName || type.FullName == targetName)
                {
                    return c;
                }
            }
            return null;
        }

        private static void AddChildrenToHierarchyObject(GameObject go, JArray children)
        {
            if (children == null) return;
            foreach (var childNode in children)
            {
                CreateHierarchyRecursive(childNode, go.transform);
            }
        }
    }
}
