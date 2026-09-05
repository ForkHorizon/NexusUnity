using System;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        private const int ScreenshotAttempts = 2;

        private static JToken CaptureGameViewScreenshot(JToken p)
        {
            var gameView = Resources.FindObjectsOfTypeAll<EditorWindow>()
                .FirstOrDefault(window => window.GetType().Name == "GameView");
            if (gameView == null) throw new Exception("Game View window not found or not open.");

            return CaptureEditorWindowScreenshot(gameView, "Game View");
        }

        private static JToken CaptureInspectorScreenshot(JToken p)
        {
            SelectInspectorTarget(p);
            var inspector = Resources.FindObjectsOfTypeAll<EditorWindow>()
                .FirstOrDefault(window => window.titleContent.text == "Inspector");
            if (inspector == null) throw new Exception("Inspector window not found or not open.");

            return CaptureEditorWindowScreenshot(inspector, "Inspector", SerializeVisualElement(inspector.rootVisualElement, true));
        }

        private static JObject CaptureEditorWindowScreenshot(EditorWindow window, string windowName, JToken layout = null)
        {
            var stopwatch = Stopwatch.StartNew();
            window.Focus();
            window.Repaint();
            InternalEditorUtility.RepaintAllViews();

            var size = new Vector2Int(Mathf.RoundToInt(window.position.width), Mathf.RoundToInt(window.position.height));
            if (size.x <= 0 || size.y <= 0)
            {
                stopwatch.Stop();
                var fail = CreateScreenshotResult(false, windowName + " window has no capturable area.", null,
                    new Vector2Int(Mathf.Max(0, size.x), Mathf.Max(0, size.y)), stopwatch.Elapsed.TotalMilliseconds);
                if (layout != null) fail["ui_layout"] = layout;
                return fail;
            }

            for (int attempt = 0; attempt < ScreenshotAttempts; attempt++)
            {
                if (attempt > 0) WaitForCaptureFrame();

                byte[] png = TryReadSurfacePixels(window.position.position, size, windowName, attempt);
                if (png == null) continue;

                stopwatch.Stop();
                var success = CreateScreenshotResult(true, windowName + " screenshot captured.", png, size,
                    stopwatch.Elapsed.TotalMilliseconds);
                if (layout != null) success["ui_layout"] = layout;
                return success;
            }

            stopwatch.Stop();
            var result = CreateScreenshotResult(false, windowName + " screenshot could not be read from the editor surface.",
                null, size, stopwatch.Elapsed.TotalMilliseconds);
            if (layout != null) result["ui_layout"] = layout;
            return result;
        }

        private static byte[] TryReadSurfacePixels(Vector2 screenPosition, Vector2Int size, string windowName, int attempt)
        {
            try
            {
                Color[] pixels = InternalEditorUtility.ReadScreenPixel(screenPosition, size.x, size.y);
                if (pixels == null || pixels.Length != size.x * size.y) return null;

                var texture = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
                try
                {
                    texture.SetPixels(pixels);
                    byte[] png = texture.EncodeToPNG();
                    return png != null && png.Length >= 8 && IsPng(png) ? png : null;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
            catch (Exception e)
            {
                NexusEditorLog.Warning(NexusLogCategory.UiAutomation,
                    $"[MCP_SCREENSHOT] {windowName} capture attempt {attempt + 1} failed: {e.Message}");
                return null;
            }
        }

        private static void WaitForCaptureFrame()
        {
            EditorApplication.QueuePlayerLoopUpdate();
            InternalEditorUtility.RepaintAllViews();
            System.Threading.Thread.Sleep(16);
        }

        private static JObject CreateScreenshotResult(bool success, string message, byte[] png, Vector2Int size,
            double durationMs)
        {
            string imageBase64 = png == null ? string.Empty : Convert.ToBase64String(png);
            var data = new JObject
            {
                ["width"] = size.x,
                ["height"] = size.y,
                ["format"] = "png",
                ["image_base64"] = imageBase64
            };
            var result = new JObject
            {
                ["status"] = success ? "Success" : "PartialSuccess",
                ["success"] = success,
                ["message"] = message,
                ["duration_ms"] = Math.Round(durationMs, 3),
                ["data"] = data
            };

            // Keep the original top-level fields for existing raw JSON-RPC clients.
            if (success)
            {
                result["image_base64"] = imageBase64;
                result["format"] = "png";
            }
            return result;
        }

        private static bool IsPng(byte[] bytes)
        {
            return bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4e &&
                bytes[3] == 0x47 && bytes[4] == 0x0d && bytes[5] == 0x0a && bytes[6] == 0x1a && bytes[7] == 0x0a;
        }

        private static void SelectInspectorTarget(JToken p)
        {
            if (p?["instance_id"] == null) return;
            var target = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p));
            if (target != null) Selection.activeObject = target;
        }
    }
}
