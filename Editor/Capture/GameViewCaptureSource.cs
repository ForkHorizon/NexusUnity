using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace UnityMCP.Editor.Capture
{
    /// <summary>
    /// Acquires presented Game View pixels and copies them immediately into a Nexus-owned render texture.
    /// </summary>
    internal static class GameViewCaptureSource
    {
        private static readonly FieldInfo GameViewRtField = typeof(EditorWindow).Assembly
            .GetType("UnityEditor.GameView")
            ?.GetField("m_RenderTexture", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static EditorWindow FindOpenGameView()
        {
            return Resources.FindObjectsOfTypeAll<EditorWindow>()
                .FirstOrDefault(window => window != null && window.GetType().Name == "GameView");
        }

        internal static RenderTexture TryGetPresentedRt(EditorWindow gameView)
        {
            if (gameView == null || GameViewRtField == null) return null;
            var rt = GameViewRtField.GetValue(gameView) as RenderTexture;
            if (rt == null || !rt.IsCreated() || rt.width <= 0 || rt.height <= 0) return null;
            return rt;
        }

        internal static Vector2Int ResolveOutputSize(int sourceWidth, int sourceHeight, CaptureRequest request)
        {
            int width = request.RequestedWidth > 0 ? request.RequestedWidth : sourceWidth;
            int height = request.RequestedHeight > 0 ? request.RequestedHeight : sourceHeight;
            int maxEdge = request.MaxLongEdge;
            int longest = Mathf.Max(width, height);
            if (maxEdge > 0 && longest > maxEdge)
            {
                float scale = (float)maxEdge / longest;
                width = Mathf.Max(1, Mathf.RoundToInt(width * scale));
                height = Mathf.Max(1, Mathf.RoundToInt(height * scale));
            }

            return new Vector2Int(Mathf.Max(1, width), Mathf.Max(1, height));
        }

        internal static RenderTexture CopyToOwned(RenderTexture source, int width, int height)
        {
            RenderTexture owned = RenderTexture.GetTemporary(width, height, 0, GraphicsFormat.R8G8B8A8_SRGB);
            // GPU readback row 0 is the bottom of the RT; PNG/JPEG row 0 is the top.
            Graphics.Blit(source, owned, new Vector2(1f, -1f), new Vector2(0f, 1f));
            return owned;
        }
    }
}
