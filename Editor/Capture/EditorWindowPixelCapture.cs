using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace UnityMCP.Editor.Capture
{
    /// <summary>
    /// Small ReadPixels / screen-pixel capture for Inspector and other Editor windows.
    /// This is not Game View Capture V2 and not a DriverOwnedReadback path.
    /// </summary>
    internal static class EditorWindowPixelCapture
    {
        /// <summary>
        /// Encodes an Editor-window RenderTexture to PNG via ReadPixels. Resolves MSAA first.
        /// </summary>
        internal static byte[] EncodeRenderTextureToPng(RenderTexture rt)
        {
            if (rt == null || rt.width <= 0 || rt.height <= 0) return null;

            RenderTexture resolveRt = null;
            var prev = RenderTexture.active;
            try
            {
                RenderTexture source = rt;
                if (rt.antiAliasing > 1)
                {
                    resolveRt = RenderTexture.GetTemporary(rt.width, rt.height, 0, rt.format);
                    Graphics.Blit(rt, resolveRt);
                    source = resolveRt;
                }

                RenderTexture.active = source;
                var tex = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
                try
                {
                    tex.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                    tex.Apply();
                    byte[] png = tex.EncodeToPNG();
                    return png != null && png.Length >= 8 && IsPng(png) ? png : null;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                }
            }
            finally
            {
                RenderTexture.active = prev;
                if (resolveRt != null) RenderTexture.ReleaseTemporary(resolveRt);
            }
        }

        /// <summary>
        /// Reads OS-composited window pixels. Skips when Unity is not the active application.
        /// </summary>
        internal static byte[] TryReadSurfacePixels(Vector2 screenPosition, Vector2Int size, string windowName, int attempt)
        {
            if (size.x <= 0 || size.y <= 0) return null;
            if (!InternalEditorUtility.isApplicationActive)
            {
                NexusEditorLog.Warning(NexusLogCategory.UiAutomation,
                    $"[MCP_SCREENSHOT] {windowName} surface capture skipped: Unity editor is not the active application.");
                return null;
            }

            try
            {
                Color[] pixels = InternalEditorUtility.ReadScreenPixel(screenPosition, size.x, size.y);
                if (pixels == null || pixels.Length != size.x * size.y) return null;

                var texture = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                try
                {
                    texture.SetPixels(pixels);
                    texture.Apply();
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
                    $"[MCP_SCREENSHOT] {windowName} surface capture attempt {attempt + 1} failed: {e.Message}");
                return null;
            }
        }

        private static bool IsPng(byte[] bytes)
        {
            return bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4e &&
                bytes[3] == 0x47 && bytes[4] == 0x0d && bytes[5] == 0x0a && bytes[6] == 0x1a && bytes[7] == 0x0a;
        }
    }
}
