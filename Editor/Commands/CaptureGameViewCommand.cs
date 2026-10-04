using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityMCP.Editor.Capture;

namespace UnityMCP.Editor.Commands
{
    /// <summary>
    /// Canonical look capture. Uses Capture V2 for Game View Overlay and a Scene View framed on a target.
    /// File delivery writes one image under Temp and does not modify assets.
    /// </summary>
    public sealed class CaptureGameViewCommand : INexusCommand
    {
        /// <summary>Canonical command id.</summary>
        public const string Id = "nexus.capture_game_view";
        /// <summary>HTTP / Unity CLI alias used by the hybrid proof of concept.</summary>
        public const string Alias = "nexus_capture_game_view";
        /// <summary>Unlisted agent alias for the same look capture.</summary>
        public const string LookAlias = "nexus_look";
        /// <summary>Dotted unlisted alias for the same look capture.</summary>
        public const string LookDotAlias = "nexus.look";
        /// <summary>Default longest edge when max_dimension is omitted. Explicit 0 stays unconstrained.</summary>
        public const int DefaultMaxLongEdge = 768;
        /// <summary>Shared command description for every projection.</summary>
        public const string Description =
            "Captures Game View Overlay or a Scene View framed on a target. JPEG by default, longest edge 768 unless raised.";
        /// <summary>Shared width parameter description.</summary>
        public const string WidthDescription = "Target width in pixels (0 for native Game View width).";
        /// <summary>Shared height parameter description.</summary>
        public const string HeightDescription = "Target height in pixels (0 for native Game View height).";
        /// <summary>Shared format parameter description.</summary>
        public const string FormatDescription = "Image encoding format: 'jpg' (default) or 'png'. Only one encoding is returned.";
        /// <summary>Shared quality parameter description.</summary>
        public const string QualityDescription = "JPEG compression quality (1-100, default 85).";
        /// <summary>Shared max-dimension parameter description.</summary>
        public const string MaxDimensionDescription =
            "Longest edge in pixels. Defaults to 768. Pass 1024 to opt in. 0 keeps the source size up to 8192.";
        /// <summary>Shared view parameter description.</summary>
        public const string ViewDescription = "game (default) or scene. Scene View is framed on the target.";
        /// <summary>Shared target parameter description.</summary>
        public const string TargetDescription = "Hierarchy path or unique object name to crop or frame.";
        /// <summary>Shared instance id parameter description.</summary>
        public const string InstanceIdDescription = "Unity instance id to crop or frame, including a negative scene-object id. Zero means none. Do not also pass target.";
        /// <summary>Shared delivery parameter description.</summary>
        public const string DeliveryDescription = "base64 (default) or file. File writes one image under Temp and omits base64.";

        /// <summary>Gets the canonical Game View capture descriptor.</summary>
        public NexusCommandDescriptor Descriptor { get; } = CreateDescriptor();

        /// <summary>
        /// Captures Game View or a framed Scene View and returns the canonical envelope.
        /// File delivery writes one image under Temp. It does not modify assets.
        /// </summary>
        /// <param name="parameters">Size, format, view, target, delivery, and request_id.</param>
        /// <param name="cancellationToken">Token used to abort GPU readback.</param>
        /// <returns>Envelope JSON. Domain failures stay in <c>result</c>, not a JSON-RPC error.</returns>
        public async Task<JToken> ExecuteAsync(JToken parameters, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            if (!CaptureLookRequest.TryParse(parameters, sw.ElapsedMilliseconds, out CaptureLookCall call))
                return call.Rejected.ToJson();

            NexusCommandEnvelope envelope = await ExecuteEnveloped(call, cancellationToken).ConfigureAwait(false);
            return envelope.ToJson();
        }

        /// <summary>
        /// Pipeline entry. File delivery writes one image under Temp and does not modify assets.
        /// </summary>
        /// <param name="width">Requested width, or 0 for the native size.</param>
        /// <param name="height">Requested height, or 0 for the native size.</param>
        /// <param name="format"><c>jpg</c> by default, or <c>png</c>.</param>
        /// <param name="quality">JPEG quality. Values outside 1–100 are clamped.</param>
        /// <param name="maxDimension">Longest edge. 768 is the default. 0 keeps the source size.</param>
        /// <param name="view"><c>game</c> or <c>scene</c>.</param>
        /// <param name="target">Object name or hierarchy path.</param>
        /// <param name="instanceId">Object instance id. Zero means none.</param>
        /// <param name="delivery"><c>base64</c> or <c>file</c>.</param>
        /// <param name="requestId">Caller id echoed in meta, or null.</param>
        /// <param name="cancellationToken">Token used to abort the capture.</param>
        /// <returns>The canonical envelope. Argument failures do not capture.</returns>
        public static Task<NexusCommandEnvelope> ExecuteFromCli(
            int width,
            int height,
            string format,
            int quality,
            int maxDimension,
            string view,
            string target,
            int instanceId,
            string delivery,
            string requestId,
            CancellationToken cancellationToken)
        {
            var parameters = new JObject
            {
                ["width"] = width,
                ["height"] = height,
                ["format"] = format ?? "jpg",
                ["quality"] = quality,
                ["max_dimension"] = maxDimension,
                ["view"] = string.IsNullOrEmpty(view) ? "game" : view,
                ["delivery"] = string.IsNullOrEmpty(delivery) ? "base64" : delivery
            };
            if (!string.IsNullOrEmpty(target)) parameters["target"] = target;
            if (instanceId != 0) parameters["instance_id"] = instanceId;
            if (!string.IsNullOrEmpty(requestId)) parameters["request_id"] = requestId;
            if (!CaptureLookRequest.TryParse(parameters, 0, out CaptureLookCall call))
                return Task.FromResult(call.Rejected);

            return ExecuteEnveloped(call, cancellationToken);
        }

        /// <summary>
        /// Captures and returns the canonical envelope. Domain failures are envelopes, not thrown.
        /// File delivery writes one image under Temp and does not modify assets.
        /// </summary>
        /// <param name="call">Parsed look arguments, including the jpg default already applied by the caller.</param>
        /// <param name="cancellationToken">Token used to abort the capture.</param>
        /// <returns>Envelope whose <c>data</c> is a <see cref="NexusCaptureResult"/> on success.</returns>
        internal static async Task<NexusCommandEnvelope> ExecuteEnveloped(CaptureLookCall call, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                call.Request.Format = ParseFormat(call.Format ?? "jpg");
                NexusCaptureResult result = await ExecuteAsync(call, cancellationToken).ConfigureAwait(false);
                return NexusCommandEnvelope.Success(result, call.RequestId, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                return NexusCommandEnvelope.FromException(ex, call.RequestId, sw.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// Captures through Capture V2. Returns Base64 or one Temp file path, never both encodings.
        /// </summary>
        /// <param name="call">Parsed look arguments.</param>
        /// <param name="cancellationToken">Token used to abort the capture.</param>
        /// <returns>Transport-facing capture DTO.</returns>
        internal static async Task<NexusCaptureResult> ExecuteAsync(CaptureLookCall call, CancellationToken cancellationToken)
        {
            var swBase64 = Stopwatch.StartNew();
            CaptureResult captured = await CaptureGateway.Shared
                .CaptureGameView(call.Request, cancellationToken)
                .ConfigureAwait(false);
            string path = null;
            string base64 = null;
            if (call.WriteFile)
                path = CaptureFileDelivery.Write(captured.CompressedBytes, captured.Format);
            else
                base64 = Convert.ToBase64String(captured.CompressedBytes ?? Array.Empty<byte>());
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
                FilePath = path,
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
                Aliases = new[] { Alias, LookAlias, LookDotAlias },
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
                    Param("max_dimension", "integer", MaxDimensionDescription),
                    Param("view", "string", ViewDescription),
                    Param("target", "string", TargetDescription),
                    Param("instance_id", "integer", InstanceIdDescription),
                    Param("delivery", "string", DeliveryDescription),
                    Param("request_id", "string", NexusCommandCodes.RequestIdDescription)
                }
            };
        }

        private static NexusCommandParameter Param(string name, string jsonType, string description)
        {
            return new NexusCommandParameter { Name = name, JsonType = jsonType, Description = description };
        }
    }
}
