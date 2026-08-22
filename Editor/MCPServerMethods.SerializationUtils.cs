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

        private sealed class VisualElementSerializationState
        {
            internal int VisitedCount;
            internal int CurrentDepth;
            internal readonly int MaxElements;
            internal readonly int MaxDepth;
            internal readonly bool Deep;
            internal bool IsTruncated;

            internal VisualElementSerializationState(int maxDepth, int maxElements, bool deep = false, int currentDepth = 0)
            {
                MaxDepth = maxDepth < 0 ? 0 : maxDepth;
                MaxElements = maxElements <= 0 ? 1 : maxElements;
                Deep = deep;
                CurrentDepth = currentDepth;
            }
        }

        internal static JObject SerializeVisualElementNode(VisualElement el, bool deep = false)
        {
            if (el == null) return null;

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

            return obj;
        }

        private static JToken SerializeVisualElement(VisualElement el, VisualElementSerializationState state)
        {
            if (el == null) return JValue.CreateNull();

            state.VisitedCount++;
            var obj = SerializeVisualElementNode(el, state.Deep);
            if (obj == null) return JValue.CreateNull();

            int childCount = el.childCount;
            if (childCount > 0)
            {
                if (state.CurrentDepth >= state.MaxDepth)
                {
                    obj["children_truncated"] = true;
                    state.IsTruncated = true;
                }
                else
                {
                    var children = new JArray();
                    state.CurrentDepth++;
                    for (int i = 0; i < childCount; i++)
                    {
                        if (state.VisitedCount >= state.MaxElements)
                        {
                            obj["children_truncated"] = true;
                            state.IsTruncated = true;
                            break;
                        }
                        children.Add(SerializeVisualElement(el[i], state));
                    }
                    state.CurrentDepth--;
                    if (children.Count > 0) obj["children"] = children;
                }
            }

            if (state.CurrentDepth == 0 && state.IsTruncated)
            {
                obj["truncated"] = true;
            }

            return obj;
        }

        private static JToken SerializeVisualElement(VisualElement el, bool deep = false, int maxDepth = 30, int maxElements = 1000)
        {
            if (el == null) return JValue.CreateNull();
            var state = new VisualElementSerializationState(maxDepth, maxElements, deep, 0);
            return SerializeVisualElement(el, state);
        }
    }
}
