#pragma warning disable 0618
#pragma warning disable 0618
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
    /// Partial implementation of MCPServerMethods handling UI interaction.
    /// </summary>
    public static partial class MCPServerMethods
    {
        private static void RegisterUIMethods()
        {
            _methods["ui_list_windows"] = UIListWindows;
            _methods["ui_get_hierarchy"] = UIGetHierarchy;
            _methods["ui_click"] = UIClick;
            _methods["ui_input_text"] = UIInputText;
            _methods["ui_query_elements"] = UIQueryElements;
        }

        private static JToken UIListWindows(JToken p)
        {
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            var arr = new JArray(windows.Where(w => !string.IsNullOrEmpty(w.titleContent.text)).Select(w => w.titleContent.text).Distinct());
            return new JObject { ["windows"] = arr };
        }

        private static JToken UIGetHierarchy(JToken p)
        {
            if (p == null || p["window_title"] == null) throw new Exception("window_title is required");
            var w = FindWindow(p["window_title"].ToString());
            if (w == null) throw new Exception("Window not found");
            bool deep = p["deep"] != null && (bool)p["deep"];
            return SerializeVisualElement(w.rootVisualElement, deep);
        }

        private static JToken UIQueryElements(JToken p)
        {
            if (p == null || p["window_title"] == null) throw new Exception("window_title is required");
            var w = FindWindow(p["window_title"].ToString());
            if (w == null) throw new Exception("Window not found");

            string textMatch = p["text"]?.ToString();
            string nameMatch = p["name"]?.ToString();
            string classMatch = p["class_name"]?.ToString();

            var results = new JArray();
            QueryVisualElementRecursive(w.rootVisualElement, textMatch, nameMatch, classMatch, results);
            return results;
        }

        private static void QueryVisualElementRecursive(VisualElement el, string textMatch, string nameMatch, string classMatch, JArray results)
        {
            if (el == null) return;

            bool matches = true;

            if (!string.IsNullOrEmpty(nameMatch) && el.name != nameMatch)
                matches = false;

            if (matches && !string.IsNullOrEmpty(classMatch) && !el.ClassListContains(classMatch))
                matches = false;

            if (matches && !string.IsNullOrEmpty(textMatch))
            {
                if (el is TextElement te)
                {
                    if (string.IsNullOrEmpty(te.text) || !te.text.Contains(textMatch, StringComparison.OrdinalIgnoreCase))
                        matches = false;
                }
                else
                {
                    matches = false; // Not a text element
                }
            }

            // We only add to results if at least one query parameter was provided AND it matched
            if (matches && (!string.IsNullOrEmpty(textMatch) || !string.IsNullOrEmpty(nameMatch) || !string.IsNullOrEmpty(classMatch)))
            {
                // We serialize this element deeply but without its full children tree to avoid massive payloads
                var obj = SerializeVisualElement(el, true);
                if (obj is JObject jobj && jobj.ContainsKey("children"))
                {
                    jobj.Remove("children"); // Remove deep children for the query result, we just want the matched node
                }
                results.Add(obj);
            }

            foreach (var child in el.Children())
            {
                QueryVisualElementRecursive(child, textMatch, nameMatch, classMatch, results);
            }
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