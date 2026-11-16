using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Capture
{
    /// <summary>
    /// Crop and blit math for Capture V2. A full-frame crop uses the same Y flip as Game View Overlay capture.
    /// </summary>
    internal static class CaptureFraming
    {
        /// <summary>Crops smaller than this on either edge are treated as not visible.</summary>
        internal const int MinimumCropPixels = 4;

        /// <summary>
        /// Scale and offset for a bottom-left source crop. A full-frame crop is (1, -1) and (0, 1), the Overlay flip.
        /// </summary>
        internal static void CropScale(RectInt crop, int sourceWidth, int sourceHeight, out Vector2 scale, out Vector2 offset)
        {
            float su = crop.width / (float)sourceWidth;
            float sv = crop.height / (float)sourceHeight;
            float u0 = crop.x / (float)sourceWidth;
            float v0 = crop.y / (float)sourceHeight;
            scale = new Vector2(su, -sv);
            offset = new Vector2(u0, v0 + sv);
        }

        /// <summary>Intersects the bottom-left pixel rect spanned by two corners with the render texture.</summary>
        internal static RectInt IntersectPixels(Vector2 cornerA, Vector2 cornerB, int rtWidth, int rtHeight)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(cornerA.x, cornerB.x)), 0, rtWidth);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(cornerA.y, cornerB.y)), 0, rtHeight);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(cornerA.x, cornerB.x)), 0, rtWidth);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(cornerA.y, cornerB.y)), 0, rtHeight);
            return new RectInt(x0, y0, Mathf.Max(0, x1 - x0), Mathf.Max(0, y1 - y0));
        }

        /// <summary>Rejects an empty or tiny crop so a missed target is not returned as an image.</summary>
        internal static RectInt RequireVisible(RectInt rect)
        {
            if (rect.width < MinimumCropPixels || rect.height < MinimumCropPixels)
            {
                throw new CaptureException(
                    CaptureErrorCode.TargetNotVisible,
                    "The target is not visible enough in the Game View to crop.");
            }

            return rect;
        }

        /// <summary>World bounds of enabled renderers, otherwise UI corners, otherwise a 1m box at the transform.</summary>
        internal static Bounds WorldBounds(GameObject target)
        {
            bool any = false;
            Bounds bounds = new Bounds(target.transform.position, Vector3.one);
            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
            {
                if (renderer == null || !renderer.enabled) continue;
                if (!any) bounds = renderer.bounds;
                else bounds.Encapsulate(renderer.bounds);
                any = true;
            }

            if (any) return bounds;
            foreach (RectTransform rect in target.GetComponentsInChildren<RectTransform>())
            {
                if (rect == null) continue;
                Vector3[] corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                if (!any) bounds = new Bounds(corners[0], Vector3.zero);
                for (int i = 0; i < 4; i++) bounds.Encapsulate(corners[i]);
                any = true;
            }

            return bounds;
        }

        /// <summary>
        /// Projects a target into Game View pixels with a bottom-left origin. Reads transforms and does not modify the scene.
        /// </summary>
        internal static RectInt ProjectToRenderTexture(GameObject target, int rtWidth, int rtHeight)
        {
            Canvas canvas = target.GetComponentInParent<Canvas>();
            RectTransform rect = target.GetComponent<RectTransform>();
            if (rect != null && canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return RequireVisible(OverlayRect(rect, rtWidth, rtHeight));

            Camera camera = ActiveCamera();
            if (camera == null)
            {
                throw new CaptureException(
                    CaptureErrorCode.TargetNotVisible,
                    "No camera can see the target.");
            }

            return RequireVisible(RendererRect(target, camera, rtWidth, rtHeight));
        }

        private static RectInt OverlayRect(RectTransform rect, int rtWidth, int rtHeight)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float screenW = Screen.width > 0 ? Screen.width : rtWidth;
            float screenH = Screen.height > 0 ? Screen.height : rtHeight;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                var pixel = new Vector2(corners[i].x / screenW * rtWidth, corners[i].y / screenH * rtHeight);
                min = Vector2.Min(min, pixel);
                max = Vector2.Max(max, pixel);
            }

            return IntersectPixels(min, max, rtWidth, rtHeight);
        }

        private static RectInt RendererRect(GameObject target, Camera camera, int rtWidth, int rtHeight)
        {
            Bounds bounds = WorldBounds(target);
            Vector3 ext = bounds.extents;
            Vector3 center = bounds.center;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            bool any = false;
            for (int i = 0; i < 8; i++)
            {
                Vector3 world = center + new Vector3(
                    (i & 1) == 0 ? -ext.x : ext.x,
                    (i & 2) == 0 ? -ext.y : ext.y,
                    (i & 4) == 0 ? -ext.z : ext.z);
                Vector3 screen = camera.WorldToScreenPoint(world);
                if (screen.z <= 0f) continue;
                var pixel = new Vector2(
                    screen.x / Mathf.Max(1, camera.pixelWidth) * rtWidth,
                    screen.y / Mathf.Max(1, camera.pixelHeight) * rtHeight);
                min = Vector2.Min(min, pixel);
                max = Vector2.Max(max, pixel);
                any = true;
            }

            if (!any) return new RectInt(0, 0, 0, 0);
            return IntersectPixels(min, max, rtWidth, rtHeight);
        }

        private static Camera ActiveCamera()
        {
            if (Camera.main != null) return Camera.main;
            Camera[] cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].isActiveAndEnabled) return cameras[i];
            }

            return null;
        }
    }

    /// <summary>
    /// Resolves a capture target from an instance id or a hierarchy path. Reads the loaded scenes and does not modify them.
    /// </summary>
    internal static class CaptureTargetResolver
    {
        /// <summary>Returns the one matching scene object, or throws a stable capture error.</summary>
        internal static GameObject Resolve(int instanceId, string path)
        {
            GameObject target = instanceId != 0 ? ById(instanceId) : ByPath(path);
            if (!target.activeInHierarchy)
            {
                throw new CaptureException(CaptureErrorCode.TargetDisabled, "The target object is disabled.");
            }

            return target;
        }

        private static GameObject ById(int instanceId)
        {
            var target = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if (target == null)
            {
                throw new CaptureException(CaptureErrorCode.TargetNotFound, "The target object was not found.");
            }

            return target;
        }

        private static GameObject ByPath(string path)
        {
            bool hierarchy = !string.IsNullOrEmpty(path) && path.IndexOf('/') >= 0;
            var matches = new List<GameObject>();
            foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (candidate == null || EditorUtility.IsPersistent(candidate) || !candidate.scene.IsValid()) continue;
                bool match = hierarchy ? HierarchyPath(candidate) == path : candidate.name == path;
                if (match) matches.Add(candidate);
            }

            if (matches.Count == 0)
                throw new CaptureException(CaptureErrorCode.TargetNotFound, "The target object was not found.");
            if (matches.Count > 1)
                throw new CaptureException(CaptureErrorCode.TargetNotFound, "The target name matches more than one object.");
            return matches[0];
        }

        private static string HierarchyPath(GameObject target)
        {
            var names = new List<string>();
            Transform cursor = target.transform;
            while (cursor != null)
            {
                names.Add(cursor.name);
                cursor = cursor.parent;
            }

            names.Reverse();
            return string.Join("/", names.ToArray());
        }
    }
}
