using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Registers JSON-RPC methods for inspecting and manipulating Unity Editor windows and UI Toolkit visual elements.
    /// </summary>
    /// <remarks>
    /// UI automation methods enumerate <see cref="EditorWindow"/> instances, traverse visual trees, click or input text into
    /// named controls, move/resize editor windows, capture snapshots, and repaint affected windows for automation diagnostics.
    /// </remarks>
    public static partial class MCPServerMethods
    {
        private static void RegisterUIMethods()
        {
            _methods["ui_list_windows"] = UIListWindows;
            _methods["ui_get_hierarchy"] = UIGetHierarchy;
            _methods["ui_click"] = UIClick;
            _methods["ui_input_text"] = UIInputText;
            _methods["ui_query_elements"] = UIQueryElements;
            _methods["ui_get_window_rect"] = UIGetWindowRect;
            _methods["ui_set_window_rect"] = UISetWindowRect;
            _methods["ui_capture_window_snapshot"] = UICaptureWindowSnapshot;
        }

        private static JToken UIListWindows(JToken p)
        {
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            var arr = new JArray(windows.Where(w => !string.IsNullOrEmpty(w.titleContent.text)).Select(w => w.titleContent.text).Distinct());
            return new JObject { ["windows"] = arr };
        }

        private const int DefaultMaxHierarchyDepth = 30;
        private const int DefaultMaxHierarchyElements = 1000;
        private const int DefaultMaxQueryResults = 100;
        private const int DefaultMaxQueryTraversedElements = 2000;

        private static JToken UIGetHierarchy(JToken p)
        {
            if (p == null || p["window_title"] == null) throw new Exception("window_title is required");
            var w = FindWindow(p["window_title"].ToString());
            if (w == null) throw new Exception("Window not found");
            bool deep = p["deep"] != null && (bool)p["deep"];
            int maxDepth = p["max_depth"] != null ? Mathf.Max(0, p["max_depth"].Value<int>()) : DefaultMaxHierarchyDepth;
            int maxElements = p["max_elements"] != null ? Mathf.Max(1, p["max_elements"].Value<int>()) :
                (p["max_results"] != null ? Mathf.Max(1, p["max_results"].Value<int>()) : DefaultMaxHierarchyElements);
            return SerializeVisualElement(w.rootVisualElement, deep, maxDepth, maxElements);
        }

        private sealed class UIElementQueryContext
        {
            internal string TextMatch;
            internal string NameMatch;
            internal string ClassMatch;
            internal int MaxResults;
            internal int MaxTraversed;
            internal int MaxDepth;
            internal JArray Results;
            internal int VisitedCount;

            internal bool ShouldStop(int currentDepth) =>
                Results.Count >= MaxResults || VisitedCount >= MaxTraversed || currentDepth > MaxDepth;
        }

        private static JToken UIQueryElements(JToken p)
        {
            if (p == null || p["window_title"] == null) throw new Exception("window_title is required");
            var w = FindWindow(p["window_title"].ToString());
            if (w == null) throw new Exception("Window not found");

            int maxDepth = p["max_depth"] != null ? Mathf.Max(0, p["max_depth"].Value<int>()) : DefaultMaxHierarchyDepth;
            int maxResults = p["max_results"] != null ? Mathf.Max(1, p["max_results"].Value<int>()) : DefaultMaxQueryResults;
            int maxTraversed = p["max_elements"] != null ? Mathf.Max(1, p["max_elements"].Value<int>()) :
                (p["max_traversed"] != null ? Mathf.Max(1, p["max_traversed"].Value<int>()) : DefaultMaxQueryTraversedElements);

            var ctx = new UIElementQueryContext
            {
                TextMatch = p["text"]?.ToString(),
                NameMatch = p["name"]?.ToString(),
                ClassMatch = p["class_name"]?.ToString(),
                MaxDepth = maxDepth,
                MaxResults = maxResults,
                MaxTraversed = maxTraversed,
                Results = new JArray()
            };

            QueryVisualElementRecursive(w.rootVisualElement, ctx, 0);
            return ctx.Results;
        }

        private static JToken UIGetWindowRect(JToken p)
        {
            if (p == null || p["window_title"] == null) throw new Exception("window_title is required");
            var w = FindWindow(p["window_title"].ToString());
            if (w == null) throw new Exception("Window not found");

            return new JObject
            {
                ["status"] = "Success",
                ["window_title"] = w.titleContent.text,
                ["rect"] = SerializeWindowRect(w)
            };
        }

        private static JToken UISetWindowRect(JToken p)
        {
            if (p == null || p["window_title"] == null) throw new Exception("window_title is required");
            var w = FindWindow(p["window_title"].ToString());
            if (w == null) throw new Exception("Window not found");

            Rect rect = w.position;
            if (HasNonNullParam(p, "x")) rect.x = p["x"].Value<float>();
            if (HasNonNullParam(p, "y")) rect.y = p["y"].Value<float>();
            if (HasNonNullParam(p, "width")) rect.width = Mathf.Max(p["width"].Value<float>(), w.minSize.x);
            if (HasNonNullParam(p, "height")) rect.height = Mathf.Max(p["height"].Value<float>(), w.minSize.y);

            w.position = rect;
            w.Repaint();

            return new JObject
            {
                ["status"] = "Success",
                ["window_title"] = w.titleContent.text,
                ["rect"] = SerializeWindowRect(w)
            };
        }

        private static JObject SerializeWindowRect(EditorWindow window)
        {
            Rect rect = window.position;
            return new JObject
            {
                ["x"] = rect.x,
                ["y"] = rect.y,
                ["width"] = rect.width,
                ["height"] = rect.height,
                ["min_width"] = window.minSize.x,
                ["min_height"] = window.minSize.y,
                ["max_width"] = window.maxSize.x,
                ["max_height"] = window.maxSize.y
            };
        }

        private static bool HasNonNullParam(JToken p, string name)
        {
            return p?[name] != null && p[name].Type != JTokenType.Null;
        }

        private static void QueryVisualElementRecursive(VisualElement el, UIElementQueryContext ctx, int currentDepth)
        {
            if (el == null || ctx.ShouldStop(currentDepth)) return;

            ctx.VisitedCount++;

            if (MatchesQuery(el, ctx))
            {
                var obj = SerializeVisualElementNode(el, deep: true);
                if (obj != null) ctx.Results.Add(obj);
                if (ctx.Results.Count >= ctx.MaxResults) return;
            }

            if (currentDepth < ctx.MaxDepth)
            {
                int childCount = el.childCount;
                for (int i = 0; i < childCount; i++)
                {
                    if (ctx.Results.Count >= ctx.MaxResults || ctx.VisitedCount >= ctx.MaxTraversed) break;
                    QueryVisualElementRecursive(el[i], ctx, currentDepth + 1);
                }
            }
        }

        private static bool MatchesQuery(VisualElement el, UIElementQueryContext ctx)
        {
            if (!string.IsNullOrEmpty(ctx.NameMatch) && el.name != ctx.NameMatch)
                return false;

            if (!string.IsNullOrEmpty(ctx.ClassMatch) && !el.ClassListContains(ctx.ClassMatch))
                return false;

            if (!string.IsNullOrEmpty(ctx.TextMatch))
            {
                if (el is TextElement te)
                {
                    if (string.IsNullOrEmpty(te.text) || !te.text.Contains(ctx.TextMatch, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                else
                {
                    return false;
                }
            }

            return !string.IsNullOrEmpty(ctx.TextMatch) || !string.IsNullOrEmpty(ctx.NameMatch) || !string.IsNullOrEmpty(ctx.ClassMatch);
        }

        private static JToken UIClick(JToken p)
        {
            if (p == null || p["window_title"] == null || p["element_name"] == null) throw new Exception("window_title and element_name required");
            var w = FindWindow(p["window_title"].ToString());
            var el = FindElementByName(w?.rootVisualElement, p["element_name"].ToString());
            if (el == null) throw new Exception("Element not found");
            using (var evt = ClickEvent.GetPooled()) { evt.target = el; el.SendEvent(evt); }
            return new JObject { ["status"] = "Success", ["message"] = "Clicked" };
        }

        private static JToken UIInputText(JToken p)
        {
            if (p == null || p["window_title"] == null || p["element_name"] == null || p["text"] == null) throw new Exception("Missing params");
            var w = FindWindow(p["window_title"].ToString());
            if (w == null) throw new Exception($"Window not found: {p["window_title"]}");
            var el = FindElementByName(w.rootVisualElement, p["element_name"].ToString());
            if (el == null) throw new Exception($"Element not found: {p["element_name"]}");
            
            if (el is TextField tf) tf.value = p["text"].ToString();
            else if (el is Label lbl) lbl.text = p["text"].ToString();
            else throw new Exception($"Element '{p["element_name"]}' is not a TextField or Label (it is a {el.GetType().Name})");
            return new JObject { ["status"] = "Success", ["message"] = "Updated" };
        }
    }
}
