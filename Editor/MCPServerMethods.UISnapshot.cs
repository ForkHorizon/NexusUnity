using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        private static JToken UICaptureWindowSnapshot(JToken p)
        {
            if (p == null || p["window_title"] == null) throw new Exception("window_title is required");
            bool includeImage = p["include_image"]?.Value<bool>() ?? true;
            bool includeHierarchy = p["include_hierarchy"]?.Value<bool>() ?? true;

            var window = FindWindow(p["window_title"].ToString());
            if (window == null) throw new Exception("Window not found");

            window.Focus();
            window.Repaint();

            var result = new JObject
            {
                ["status"] = "Success",
                ["window_title"] = window.titleContent.text,
                ["rect"] = SerializeWindowRect(window)
            };

            if (includeHierarchy)
            {
                int maxDepth = p["max_depth"] != null ? Mathf.Max(0, p["max_depth"].Value<int>()) : DefaultMaxHierarchyDepth;
                int maxElements = p["max_elements"] != null ? Mathf.Max(1, p["max_elements"].Value<int>()) :
                    (p["max_results"] != null ? Mathf.Max(1, p["max_results"].Value<int>()) : DefaultMaxHierarchyElements);
                result["ui_hierarchy"] = SerializeVisualElement(window.rootVisualElement, true, maxDepth, maxElements);
            }

            if (includeImage)
                AddWindowImage(window, result);

            return result;
        }

        private static void AddWindowImage(EditorWindow window, JObject result)
        {
            if (window == null) return;

            string windowName = window.titleContent?.text ?? window.GetType().Name;

            // 1. In-engine UI Toolkit VisualElement capture
            byte[] inEnginePng = TryCaptureVisualElement(window.rootVisualElement, out var veSize);
            if (inEnginePng != null)
            {
                result["image_base64"] = Convert.ToBase64String(inEnginePng);
                result["format"] = "png";
                return;
            }

            // 2. Fallback to surface pixel read
            var size = new Vector2Int(Mathf.RoundToInt(window.position.width), Mathf.RoundToInt(window.position.height));
            if (size.x > 0 && size.y > 0)
            {
                byte[] surfacePng = TryReadSurfacePixels(window.position.position, size, windowName, 0);
                if (surfacePng != null)
                {
                    result["image_base64"] = Convert.ToBase64String(surfacePng);
                    result["format"] = "png";
                    return;
                }
            }

            bool hasHierarchy = result["ui_hierarchy"] != null && result["ui_hierarchy"].Type != JTokenType.Null;
            result["status"] = hasHierarchy ? "PartialSuccess" : "Failed";
            result["message"] = "Window image capture could not be read from the editor surface.";
        }
    }
}
