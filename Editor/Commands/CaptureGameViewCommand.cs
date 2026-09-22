using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityMCP.Editor.Capture;

namespace UnityMCP.Editor.Commands
{
    /// <summary>
    /// Canonical Game View capture command. Uses <see cref="ICaptureGateway"/>; Base64 is applied only on the result DTO.
    /// </summary>
    public sealed class CaptureGameViewCommand : INexusCommand
    {
        /// <summary>Canonical command id.</summary>
        public const string Id = "nexus.capture_game_view";
        /// <summary>HTTP / Unity CLI alias used by the hybrid proof of concept.</summary>
        public const string Alias = "nexus_capture_game_view";
        /// <summary>Shared command description for every projection.</summary>
        public const string Description =
            "Captures presented Game View pixels including Overlay UI, with JPEG/PNG and optional downscale.";
        /// <summary>Shared width parameter description.</summary>
        public const string WidthDescription = "Target width in pixels (0 for native Game View width).";
        /// <summary>Shared height parameter description.</summary>
        public const string HeightDescription = "Target height in pixels (0 for native Game View height).";
        /// <summary>Shared format parameter description.</summary>
        public const string FormatDescription = "Image encoding format: 'jpg' (default) or 'png'.";
        /// <summary>Shared quality parameter description.</summary>
        public const string QualityDescription = "JPEG compression quality (1-100, default 85).";
        /// <summary>Shared max-dimension parameter description.</summary>
        public const string MaxDimensionDescription =
            "Maximum allowed longest edge in pixels (0 for unconstrained).";

        /// <summary>Gets the canonical Game View capture descriptor.</summary>
        public NexusCommandDescriptor Descriptor { get; } = CreateDescriptor();

        /// <summary>
        /// Captures the presented Game View through Capture V2 and returns JSON for Legacy HTTP/MCP.
        /// </summary>
        /// <param name="parameters">Optional width, height, format, quality, and max_dimension.</param>
        /// <param name="cancellationToken">Token used to abort GPU readback.</param>
        /// <returns>Structured capture JSON including Base64 bytes.</returns>
        public async Task<JToken> ExecuteAsync(JToken parameters, CancellationToken cancellationToken)
        {
            int width = parameters?["width"]?.Value<int>() ?? 0;
            int height = parameters?["height"]?.Value<int>() ?? 0;
            string format = parameters?["format"]?.ToString() ?? "jpg";
            int quality = parameters?["quality"]?.Value<int>() ?? 85;
            int maxDimension = parameters?["max_dimension"]?.Value<int>()
                ?? parameters?["max_long_edge"]?.Value<int>() ?? 0;
            NexusCaptureResult result = await ExecuteAsync(
                width, height, format, quality, maxDimension, cancellationToken).ConfigureAwait(false);
            return JToken.FromObject(result);
        }

        /// <summary>
        /// Captures Game View through Capture V2 and returns the hybrid command DTO.
        /// </summary>
        /// <param name="width">Requested width, or 0 for the native Game View width.</param>
        /// <param name="height">Requested height, or 0 for the native Game View height.</param>
        /// <param name="format"><c>jpg</c>/<c>jpeg</c> or <c>png</c>.</param>
        /// <param name="quality">JPEG quality from 1 to 100.</param>
        /// <param name="maxDimension">Optional max longest edge, or 0 for unconstrained.</param>
        /// <param name="cancellationToken">Token used to abort the capture.</param>
        /// <returns>Transport-facing capture DTO including Base64 bytes.</returns>
        public static async Task<NexusCaptureResult> ExecuteAsync(
            int width,
            int height,
            string format,
            int quality,
            int maxDimension,
            CancellationToken cancellationToken)
        {
            var request = new CaptureRequest
            {
                Format = ParseFormat(format),
                JpegQuality = quality,
                RequestedWidth = width,
                RequestedHeight = height,
                MaxLongEdge = maxDimension,
                IncludeTelemetry = true
            };

            var swBase64 = Stopwatch.StartNew();
            CaptureResult captured = await CaptureGateway.Shared
                .CaptureGameView(request, cancellationToken)
                .ConfigureAwait(false);
            string base64 = Convert.ToBase64String(captured.CompressedBytes ?? Array.Empty<byte>());
            swBase64.Stop();

            CaptureTimings timings = captured.Timings ?? new CaptureTimings();
            return new NexusCaptureResult
            {
                Success = true,
                Width = captured.Width,
                Height = captured.Height,
                Encoding = CaptureEncoder.ToWireFormat(captured.Format),
                Bytes = captured.CompressedBytes?.Length ?? 0,
                Base64 = base64,
                Source = captured.Source,
                AcquisitionMs = timings.SourceAcquireMs,
                SubmitMs = timings.ReadbackSubmitMs,
                WaitMs = timings.SubmitToDoneMs,
                GetDataMs = 0,
                EncodeMs = timings.EncodeMs,
                Base64Ms = swBase64.Elapsed.TotalMilliseconds,
                MainThreadStallMs = timings.MainThreadStallMs,
                TotalMs = timings.TotalInternalMs + swBase64.Elapsed.TotalMilliseconds,
                EditorTicksSubmitToDone = timings.EditorTicksSubmitToDone
            };
        }

        private static CaptureFormat ParseFormat(string format)
        {
            if (string.IsNullOrEmpty(format) ||
                format.Equals("jpg", StringComparison.OrdinalIgnoreCase) ||
                format.Equals("jpeg", StringComparison.OrdinalIgnoreCase))
            {
                return CaptureFormat.Jpeg;
            }

            if (format.Equals("png", StringComparison.OrdinalIgnoreCase)) return CaptureFormat.Png;
            throw new CaptureException(CaptureErrorCode.UnsupportedFormat, "Unsupported capture format.");
        }

        private static NexusCommandDescriptor CreateDescriptor()
        {
            return new NexusCommandDescriptor
            {
                Id = Id,
                Aliases = new[] { Alias },
                Title = "Capture Game View",
                Description = Description,
                Profiles = new[] { "visual" },
                DispatchAsync = true,
                Parameters = new[]
                {
                    Param("width", "integer", WidthDescription),
                    Param("height", "integer", HeightDescription),
                    Param("format", "string", FormatDescription),
                    Param("quality", "integer", QualityDescription),
                    Param("max_dimension", "integer", MaxDimensionDescription)
                }
            };
        }

        private static NexusCommandParameter Param(string name, string jsonType, string description)
        {
            return new NexusCommandParameter { Name = name, JsonType = jsonType, Description = description };
        }
    }
}
