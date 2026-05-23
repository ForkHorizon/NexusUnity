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
                result["ui_hierarchy"] = SerializeVisualElement(window.rootVisualElement, true);

            if (includeImage)
                AddWindowImage(window, result);

            return result;
        }

        private static void AddWindowImage(EditorWindow window, JObject result)
        {
#if UNITY_EDITOR_OSX
            string tempPath = Path.Combine(Path.GetTempPath(), $"nexus_window_{DateTime.UtcNow.Ticks}.png");
            try
            {
                Rect rect = window.position;
                int x = Mathf.RoundToInt(rect.x);
                int y = Mathf.RoundToInt(rect.y);
                int width = Mathf.RoundToInt(rect.width);
                int height = Mathf.RoundToInt(rect.height);

                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "screencapture",
                    Arguments = $"-x -R{x},{y},{width},{height} {tempPath}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    process.WaitForExit();
                    string error = process.StandardError.ReadToEnd();
                    if (process.ExitCode != 0)
                    {
                        result["status"] = "PartialSuccess";
                        result["message"] = $"screencapture failed with exit code {process.ExitCode}: {error}";
                        return;
                    }
                }

                if (!File.Exists(tempPath))
                {
                    result["status"] = "PartialSuccess";
                    result["message"] = "Window screenshot failed or was blocked by OS permissions.";
                    return;
                }

                result["image_base64"] = Convert.ToBase64String(File.ReadAllBytes(tempPath));
                result["format"] = "png";
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
#else
            result["status"] = "PartialSuccess";
            result["message"] = "Window image capture is currently supported on macOS only.";
#endif
        }
    }
}
