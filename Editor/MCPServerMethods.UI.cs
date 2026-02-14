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
        }

        private static JToken UIListWindows(JToken p)
        {
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            return new JArray(windows.Where(w => !string.IsNullOrEmpty(w.titleContent.text)).Select(w => w.titleContent.text).Distinct());
        }

        private static JToken UIGetHierarchy(JToken p)
        {
            if (p == null || p["window_title"] == null) throw new Exception("window_title is required");
            var w = FindWindow(p["window_title"].ToString());
            if (w == null) throw new Exception("Window not found");
            return SerializeVisualElement(w.rootVisualElement);
        }

        private static JToken UIClick(JToken p)
        {
            if (p == null || p["window_title"] == null || p["element_name"] == null) throw new Exception("window_title and element_name required");
            var w = FindWindow(p["window_title"].ToString());
            var el = FindElementByName(w?.rootVisualElement, p["element_name"].ToString());
            if (el == null) throw new Exception("Element not found");
            using (var evt = ClickEvent.GetPooled()) { evt.target = el; el.SendEvent(evt); }
            return "Clicked";
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
            return "Updated";
        }
    }
}