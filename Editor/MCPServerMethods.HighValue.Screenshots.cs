using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        private const int ScreenshotAttempts = 2;

        /// <summary>
        /// Captures the Inspector Editor window. Uses UI Toolkit capture when possible, otherwise
        /// <see cref="Capture.EditorWindowPixelCapture"/> (not Game View Capture V2).
        /// </summary>
        private static JToken CaptureInspectorScreenshot(JToken p)
        {
            SelectInspectorTarget(p);
            var inspector = Resources.FindObjectsOfTypeAll<EditorWindow>()
                .FirstOrDefault(window => window != null && (window.GetType().Name == "InspectorWindow" || window.titleContent?.text == "Inspector"));
            if (inspector == null) throw new Exception("Inspector window not found or not open.");

            inspector.Focus();
            if (p?["instance_id"] != null)
            {
                ActiveEditorTracker.sharedTracker?.ForceRebuild();
            }
            inspector.Repaint();
            InternalEditorUtility.RepaintAllViews();

            var stopwatch = Stopwatch.StartNew();
            var layout = SerializeVisualElement(inspector.rootVisualElement, true);

            var size = new Vector2Int(Mathf.RoundToInt(inspector.position.width), Mathf.RoundToInt(inspector.position.height));
            if (size.x <= 0 || size.y <= 0)
            {
                stopwatch.Stop();
                return CreateScreenshotResult(false, "Inspector window has no capturable area.",
                    new ScreenshotPayload(null, new Vector2Int(Mathf.Max(0, size.x), Mathf.Max(0, size.y)), layout),
                    stopwatch.Elapsed.TotalMilliseconds);
            }

            // 1. In-engine UI Toolkit VisualElement capture
            byte[] visualElementPng = TryCaptureVisualElement(inspector.rootVisualElement, out var veSize);
            if (visualElementPng != null)
            {
                stopwatch.Stop();
                return CreateScreenshotResult(true, "Inspector screenshot captured.",
                    new ScreenshotPayload(visualElementPng, veSize, layout), stopwatch.Elapsed.TotalMilliseconds);
            }

            // 2. Fallback to surface pixel read
            for (int attempt = 0; attempt < ScreenshotAttempts; attempt++)
            {
                if (attempt > 0) WaitForCaptureFrame();

                byte[] png = TryReadSurfacePixels(inspector.position.position, size, "Inspector", attempt);
                if (png == null) continue;

                stopwatch.Stop();
                return CreateScreenshotResult(true, "Inspector screenshot captured.",
                    new ScreenshotPayload(png, size, layout), stopwatch.Elapsed.TotalMilliseconds);
            }

            stopwatch.Stop();
            return CreateScreenshotResult(false, "Inspector screenshot could not be read from the editor surface.",
                new ScreenshotPayload(null, size, layout), stopwatch.Elapsed.TotalMilliseconds);
        }

        private static byte[] TryCaptureVisualElement(VisualElement element, out Vector2Int size)
        {
            size = Vector2Int.zero;
            if (element == null) return null;

            try
            {
                foreach (Type extType in GetVisualElementCaptureTypes())
                {
                    if (extType == null) continue;
                    byte[] png = CaptureToRenderTexture(extType, element, out size)
                        ?? TryCaptureIntoRenderTexture(extType, element, out size);
                    if (png != null) return png;
                }
            }
            catch (Exception e)
            {
                NexusEditorLog.Warning(NexusLogCategory.UiAutomation,
                    $"[MCP_SCREENSHOT] VisualElement capture failed: {e.Message}");
            }

            size = Vector2Int.zero;
            return null;
        }

        // Strategy 1: CaptureToRenderTexture(VisualElement) returns a texture this method owns and releases.
        private static byte[] CaptureToRenderTexture(Type extType, VisualElement element, out Vector2Int size)
        {
            size = Vector2Int.zero;
            MethodInfo captureMethod = extType.GetMethod("CaptureToRenderTexture",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(VisualElement) }, null);
            if (!(captureMethod?.Invoke(null, new object[] { element }) is RenderTexture rt)) return null;

            try
            {
                size = new Vector2Int(rt.width, rt.height);
                return EncodeRenderTextureToPng(rt);
            }
            finally
            {
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        // Strategy 2: TryCaptureIntoRenderTexture(VisualElement, RenderTexture) fills a texture sized from the layout.
        private static byte[] TryCaptureIntoRenderTexture(Type extType, VisualElement element, out Vector2Int size)
        {
            size = Vector2Int.zero;
            MethodInfo tryCaptureMethod = extType.GetMethod("TryCaptureIntoRenderTexture",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(VisualElement), typeof(RenderTexture) }, null);
            if (tryCaptureMethod == null) return null;

            Vector2Int pixels = VisualElementPixelSize(element);
            var tempRt = new RenderTexture(pixels.x, pixels.y, 24, RenderTextureFormat.ARGB32);
            tempRt.Create();
            try
            {
                if (!(bool)tryCaptureMethod.Invoke(null, new object[] { element, tempRt })) return null;
                size = pixels;
                return EncodeRenderTextureToPng(tempRt);
            }
            finally
            {
                tempRt.Release();
                UnityEngine.Object.DestroyImmediate(tempRt);
            }
        }

        private static Vector2Int VisualElementPixelSize(VisualElement element)
        {
            float ppp = EditorGUIUtility.pixelsPerPoint;
            float rawWidth = float.IsNaN(element.layout.width) || element.layout.width <= 0
                ? (float.IsNaN(element.worldBound.width) ? 0 : element.worldBound.width)
                : element.layout.width;
            float rawHeight = float.IsNaN(element.layout.height) || element.layout.height <= 0
                ? (float.IsNaN(element.worldBound.height) ? 0 : element.worldBound.height)
                : element.layout.height;
            return new Vector2Int(
                Mathf.Max(1, Mathf.RoundToInt(rawWidth * ppp)),
                Mathf.Max(1, Mathf.RoundToInt(rawHeight * ppp)));
        }

        private static Type[] _visualElementCaptureTypes;

        private static Type[] GetVisualElementCaptureTypes()
        {
            if (_visualElementCaptureTypes != null) return _visualElementCaptureTypes;
            var types = new List<Type>();
            void AddType(Type t) { if (t != null && !types.Contains(t)) types.Add(t); }

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type t = asm.GetType("UnityEngine.UIElements.VisualElementCaptureExtensions")
                        ?? asm.GetType("UnityEditor.UIElements.VisualElementCaptureEditorExtensions");
                    if (t != null) AddType(t);
                }
                catch { }
            }

            return _visualElementCaptureTypes = types.ToArray();
        }

        private static byte[] EncodeRenderTextureToPng(RenderTexture rt)
        {
            return Capture.EditorWindowPixelCapture.EncodeRenderTextureToPng(rt);
        }

        private static byte[] TryReadSurfacePixels(Vector2 screenPosition, Vector2Int size, string windowName, int attempt)
        {
            return Capture.EditorWindowPixelCapture.TryReadSurfacePixels(screenPosition, size, windowName, attempt);
        }

        private static void WaitForCaptureFrame()
        {
            EditorApplication.QueuePlayerLoopUpdate();
            InternalEditorUtility.RepaintAllViews();
        }

        private static void SelectInspectorTarget(JToken p)
        {
            if (p?["instance_id"] == null) return;
            var target = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p));
            if (target != null) Selection.activeObject = target;
        }
    }
}
