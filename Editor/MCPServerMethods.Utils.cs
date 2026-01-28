using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Partial implementation of MCPServerMethods providing internal utilities.
    /// </summary>
    public static partial class MCPServerMethods
    {
        private static EditorWindow FindWindow(string title)
        {
            return Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w => w.titleContent.text == title);
        }

        private static VisualElement FindElementByName(VisualElement root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            foreach (var child in root.Children())
            {
                var found = FindElementByName(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static JToken SerializeVisualElement(VisualElement el)
        {
            var obj = new JObject
            {
                ["name"] = el.name,
                ["type"] = el.GetType().Name,
                ["visible"] = el.resolvedStyle.display != DisplayStyle.None
            };
            var children = new JArray();
            foreach (var child in el.Children()) children.Add(SerializeVisualElement(child));
            if (children.Count > 0) obj["children"] = children;
            return obj;
        }
    }
}
