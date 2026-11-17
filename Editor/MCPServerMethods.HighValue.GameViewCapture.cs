// Legacy capture_game_view_screenshot method built on the Capture V2 gateway.
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityMCP.Editor.Capture;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        private static async Task<JToken> CaptureGameViewScreenshotAsync(JToken p)
        {
            var stopwatch = Stopwatch.StartNew();
            CaptureRequest request = new CaptureRequest();
            try
            {
                request = ParseGameViewCaptureRequest(p);
                CaptureResult captured = await CaptureGateway.Shared
                    .CaptureGameView(request, CancellationToken.None)
                    .ConfigureAwait(false);
                stopwatch.Stop();
                var image = new ScreenshotPayload(
                    captured.CompressedBytes,
                    new Vector2Int(captured.Width, captured.Height),
                    format: CaptureEncoder.ToWireFormat(captured.Format));
                return CreateScreenshotResult(true, "Game View screenshot captured.", image, stopwatch.Elapsed.TotalMilliseconds);
            }
            catch (CaptureException ex) when (ex.Code == CaptureErrorCode.GameViewUnavailable)
            {
                throw new Exception(ex.Message);
            }
            catch (CaptureException ex)
            {
                stopwatch.Stop();
                var empty = new ScreenshotPayload(null, Vector2Int.zero, format: CaptureEncoder.ToWireFormat(request.Format));
                return CreateScreenshotResult(false, ex.Message, empty, stopwatch.Elapsed.TotalMilliseconds);
            }
        }

        internal static CaptureRequest ParseGameViewCaptureRequest(JToken p)
        {
            var request = new CaptureRequest();
            string format = p?["format"]?.ToString();
            if (!string.IsNullOrEmpty(format)) request.Format = ParseCaptureFormat(format);
            if (p?["quality"] != null) request.JpegQuality = p["quality"].Value<int>();
            else if (p?["jpeg_quality"] != null) request.JpegQuality = p["jpeg_quality"].Value<int>();
            request.RequestedWidth = p?["width"]?.Value<int>() ?? 0;
            request.RequestedHeight = p?["height"]?.Value<int>() ?? 0;
            request.MaxLongEdge = p?["max_long_edge"]?.Value<int>() ?? p?["max_dimension"]?.Value<int>() ?? 0;
            request.IncludeTelemetry = p?["include_telemetry"]?.Value<bool>() ?? false;
            return request;
        }

        private static CaptureFormat ParseCaptureFormat(string format)
        {
            if (format.Equals("png", StringComparison.OrdinalIgnoreCase)) return CaptureFormat.Png;
            if (format.Equals("jpg", StringComparison.OrdinalIgnoreCase) ||
                format.Equals("jpeg", StringComparison.OrdinalIgnoreCase))
            {
                return CaptureFormat.Jpeg;
            }

            throw new CaptureException(CaptureErrorCode.UnsupportedFormat, "Unsupported capture format.");
        }

        /// <summary>Encoded screenshot pixels with their size, wire format, and optional UI layout.</summary>
        internal sealed class ScreenshotPayload
        {
            internal ScreenshotPayload(byte[] bytes, Vector2Int size, JToken layout = null, string format = "png")
            {
                Bytes = bytes;
                Size = size;
                Layout = layout;
                Format = format;
            }

            internal byte[] Bytes { get; }
            internal Vector2Int Size { get; }
            internal JToken Layout { get; }
            internal string Format { get; }
        }

        internal static JObject CreateScreenshotResult(bool success, string message, ScreenshotPayload image, double durationMs)
        {
            JToken layout = image.Layout;
            string format = image.Format;
            bool hasPartialData = layout != null && layout.Type != JTokenType.Null;
            string imageBase64 = image.Bytes == null ? string.Empty : Convert.ToBase64String(image.Bytes);
            var data = new JObject
            {
                ["width"] = image.Size.x,
                ["height"] = image.Size.y,
                ["format"] = format,
                ["image_base64"] = imageBase64
            };
            if (hasPartialData) data["ui_layout"] = layout;

            var result = new JObject
            {
                ["status"] = success ? "Success" : (hasPartialData ? "PartialSuccess" : "Failed"),
                ["success"] = success,
                ["message"] = message,
                ["duration_ms"] = Math.Round(durationMs, 3),
                ["data"] = data
            };

            if (success)
            {
                result["image_base64"] = imageBase64;
                result["format"] = format;
            }
            if (hasPartialData) result["ui_layout"] = layout;
            return result;
        }
    }
}
