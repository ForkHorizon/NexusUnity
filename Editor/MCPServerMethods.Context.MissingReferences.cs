using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        private static JToken ShowUnresolvedMissingReferences(JToken p)
        {
            var missingReferences = new JArray();
            var activeScene = SceneManager.GetActiveScene();
            var sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(go => IsSceneObject(go, activeScene));

            using (UnityEngine.Pool.ListPool<Component>.Get(out var components))
            {
                foreach (var go in sceneObjects)
                {
                    var issues = FindMissingReferenceIssues(go, components);
                    if (issues.Count > 0)
                        missingReferences.Add(CreateMissingReferenceObject(go, issues));
                }
            }

            return new JObject
            {
                ["status"] = "Success",
                ["missing_references"] = missingReferences
            };
        }

        private static bool IsSceneObject(GameObject go, Scene activeScene)
        {
            return go.scene == activeScene &&
                (go.hideFlags == HideFlags.None || go.hideFlags == HideFlags.NotEditable);
        }

        private static JArray FindMissingReferenceIssues(GameObject go, List<Component> components)
        {
            var issues = new JArray();
            go.GetComponents(components);
            foreach (var component in components)
                AddComponentReferenceIssues(issues, component);
            return issues;
        }

        private static void AddComponentReferenceIssues(JArray issues, Component component)
        {
            if (component == null)
            {
                issues.Add(new JObject { ["type"] = "MissingScript" });
                return;
            }

            using (var serializedObject = new SerializedObject(component))
            {
                var property = serializedObject.GetIterator();
                var enterChildren = true;
                while (property.Next(enterChildren))
                {
                    enterChildren = true;
                    AddPropertyReferenceIssue(issues, component, property);
                }
            }
        }

        private static void AddPropertyReferenceIssue(JArray issues, Component component, SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference ||
                GetObjectReferenceId(property) == default || property.objectReferenceValue != null)
                return;

            issues.Add(new JObject
            {
                ["type"] = "MissingReference",
                ["component"] = GetTypeName(component.GetType()),
                ["field"] = property.propertyPath
            });
        }

        private static JObject CreateMissingReferenceObject(GameObject go, JArray issues)
        {
            return new JObject
            {
                ["name"] = go.name,
                ["instance_id"] = go.GetRawId(),
                ["issues"] = issues
            };
        }
    }
}
