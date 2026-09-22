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
                return CreateScreenshotResult(
                    true,
                    "Game View screenshot captured.",
                    captured.CompressedBytes,
                    new Vector2Int(captured.Width, captured.Height),
                    stopwatch.Elapsed.TotalMilliseconds,
                    format: CaptureEncoder.ToWireFormat(captured.Format));
            }
            catch (CaptureException ex) when (ex.Code == CaptureErrorCode.GameViewUnavailable)
            {
                throw new Exception(ex.Message);
            }
            catch (CaptureException ex)
            {
                stopwatch.Stop();
                return CreateScreenshotResult(
                    false,
                    ex.Message,
                    null,
                    Vector2Int.zero,
                    stopwatch.Elapsed.TotalMilliseconds,
                    format: CaptureEncoder.ToWireFormat(request.Format));
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

        internal static JObject CreateScreenshotResult(bool success, string message, byte[] imageBytes, Vector2Int size,
            double durationMs, JToken layout = null, string format = "png")
        {
            bool hasPartialData = layout != null && layout.Type != JTokenType.Null;
            string imageBase64 = imageBytes == null ? string.Empty : Convert.ToBase64String(imageBytes);
            var data = new JObject
            {
                ["width"] = size.x,
                ["height"] = size.y,
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
