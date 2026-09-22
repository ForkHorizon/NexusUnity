using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace UnityMCP.Editor.Capture
{
    /// <summary>
    /// Encodes GPU readback pixels on the Unity main thread using native JPEG/PNG encoders.
    /// </summary>
    internal static class CaptureEncoder
    {
        internal static int ClampJpegQuality(int quality)
        {
            if (quality < 1) return 1;
            if (quality > 100) return 100;
            return quality;
        }

        internal static byte[] Encode(
            NativeArray<byte> pixels,
            int width,
            int height,
            CaptureFormat format,
            int jpegQuality)
        {
            NativeArray<byte> encoded = format == CaptureFormat.Png
                ? ImageConversion.EncodeNativeArrayToPNG(
                    pixels, GraphicsFormat.R8G8B8A8_SRGB, (uint)width, (uint)height)
                : ImageConversion.EncodeNativeArrayToJPG(
                    pixels, GraphicsFormat.R8G8B8A8_SRGB, (uint)width, (uint)height, 0,
                    ClampJpegQuality(jpegQuality));

            try
            {
                return encoded.ToArray();
            }
            finally
            {
                if (encoded.IsCreated) encoded.Dispose();
            }
        }

        internal static string ToWireFormat(CaptureFormat format)
        {
            return format == CaptureFormat.Jpeg ? "jpg" : "png";
        }
    }
}
