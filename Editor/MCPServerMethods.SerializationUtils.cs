using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        internal static JObject SerializeVector3(Vector3 v)
        {
            return new JObject { ["x"] = v.x, ["y"] = v.y, ["z"] = v.z };
        }

        internal static string SerializeColor(Color c)
        {
            return "#" + ColorUtility.ToHtmlStringRGBA(c);
        }

        private static EditorWindow FindWindow(string title)
        {
            return Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w => w.titleContent.text == title);
        }

        private static VisualElement FindElementByName(VisualElement root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            return root.Q(name);
        }

        private static JToken SerializeVisualElement(VisualElement el, bool deep = false)
        {
            var obj = new JObject
            {
                ["name"] = el.name,
                ["type"] = el.GetType().Name,
                ["visible"] = el.resolvedStyle.display != DisplayStyle.None
            };

            if (el is TextElement te && !string.IsNullOrEmpty(te.text))
                obj["text"] = te.text;

            var classes = el.GetClasses().ToList();
            if (classes.Count > 0)
                obj["classes"] = new JArray(classes);

            if (deep)
            {
                var rect = el.layout;
                obj["layout"] = new JObject { ["x"] = rect.x, ["y"] = rect.y, ["width"] = rect.width, ["height"] = rect.height };

                var style = new JObject();
                style["display"] = el.resolvedStyle.display.ToString();
                style["visibility"] = el.resolvedStyle.visibility.ToString();
                style["opacity"] = el.resolvedStyle.opacity;
                style["color"] = el.resolvedStyle.color.ToString();
                style["backgroundColor"] = el.resolvedStyle.backgroundColor.ToString();
                obj["computed_style"] = style;
            }

            var children = new JArray();
            foreach (var child in el.Children()) children.Add(SerializeVisualElement(child, deep));
            if (children.Count > 0) obj["children"] = children;
            return obj;
        }
    }
}
