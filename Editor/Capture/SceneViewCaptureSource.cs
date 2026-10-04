using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace UnityMCP.Editor.Capture
{
    /// <summary>
    /// Frames the open Scene View on a target, renders that camera, and copies it with the Capture V2 flip.
    /// Restores the editor camera pose. The render is synchronous so a test runner update cannot deadlock it.
    /// </summary>
    internal static class SceneViewCaptureSource
    {
        /// <summary>
        /// Frames the target, renders the Scene camera, and returns a Nexus-owned texture. Restores the editor camera pose.
        /// </summary>
        internal static RenderTexture Capture(GameObject target, CaptureRequest request)
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null || view.camera == null)
            {
                throw new CaptureException(CaptureErrorCode.SceneViewUnavailable, "No active Scene View is open.");
            }

            if (DriverOwnedReadback.IsReloading)
            {
                throw new CaptureException(CaptureErrorCode.DomainReloadInterrupted, "Capture rejected during domain reload.");
            }

            PoseSnapshot pose = PoseSnapshot.Save(view);
            RenderTexture raw = null;
            try
            {
                view.Frame(CaptureFraming.WorldBounds(target), true);
                Aim(view);
                raw = Render(view.camera, request);
                return GameViewCaptureSource.CopyToOwned(raw, raw.width, raw.height);
            }
            finally
            {
                if (raw != null) RenderTexture.ReleaseTemporary(raw);
                pose.Restore(view);
            }
        }

        private static void Aim(SceneView view)
        {
            Camera camera = view.camera;
            float fov = Mathf.Max(1f, camera.fieldOfView);
            float distance = view.orthographic
                ? view.size
                : view.size / Mathf.Sin(fov * 0.5f * Mathf.Deg2Rad);
            camera.orthographic = view.orthographic;
            camera.orthographicSize = view.size;
            camera.transform.rotation = view.rotation;
            camera.transform.position = view.pivot - view.rotation * Vector3.forward * distance;
        }

        private static RenderTexture Render(Camera camera, CaptureRequest request)
        {
            int sourceWidth = Mathf.Max(1, camera.pixelWidth);
            int sourceHeight = Mathf.Max(1, camera.pixelHeight);
            Vector2Int size = GameViewCaptureSource.ResolveOutputSize(sourceWidth, sourceHeight, request);
            RenderTexture texture = RenderTexture.GetTemporary(size.x, size.y, 24, GraphicsFormat.R8G8B8A8_SRGB);
            RenderTexture previousTarget = camera.targetTexture;
            float previousAspect = camera.aspect;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = texture;
                camera.aspect = size.x / (float)size.y;
                camera.Render();
                return texture;
            }
            catch
            {
                RenderTexture.ReleaseTemporary(texture);
                throw;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.aspect = previousAspect;
                RenderTexture.active = previousActive;
            }
        }

        private struct PoseSnapshot
        {
            public Vector3 Pivot;
            public Quaternion Rotation;
            public float Size;
            public bool Orthographic;
            public bool In2D;

            internal static PoseSnapshot Save(SceneView view)
            {
                return new PoseSnapshot
                {
                    Pivot = view.pivot,
                    Rotation = view.rotation,
                    Size = view.size,
                    Orthographic = view.orthographic,
                    In2D = view.in2DMode
                };
            }

            internal void Restore(SceneView view)
            {
                view.pivot = Pivot;
                view.rotation = Rotation;
                view.size = Size;
                view.orthographic = Orthographic;
                view.in2DMode = In2D;
                view.Repaint();
            }
        }
    }
}
