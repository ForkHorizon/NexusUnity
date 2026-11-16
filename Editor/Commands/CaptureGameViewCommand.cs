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
        /// <summary>JPEG quality used when quality is omitted. Values outside 1–100 are clamped later.</summary>
        public const int DefaultJpegQuality = 85;
        /// <summary>Image encoding used when format is omitted. The other encoding is png.</summary>
        public const string DefaultFormat = "jpg";
        /// <summary>View used when view is omitted. Scene is the other accepted value.</summary>
        public const string DefaultView = "game";
        /// <summary>Delivery used when delivery is omitted. File writes one image under Temp.</summary>
        public const string DefaultDelivery = "base64";
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
        /// Last step before pixels are read. Tests replace this to see the request HTTP and CLI actually capture.
        /// </summary>
        internal static Func<CaptureRequest, CancellationToken, Task<CaptureResult>> Capture =
            (request, token) => CaptureGateway.Shared.CaptureGameView(request, token);

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
            if (!TryPrepareLook(parameters, sw.ElapsedMilliseconds, out CaptureLookCall call))
                return call.Rejected.ToJson();

            NexusCommandEnvelope envelope = await ExecuteEnveloped(call, cancellationToken).ConfigureAwait(false);
            return envelope.ToJson();
        }

        /// <summary>
        /// Pipeline entry. File delivery writes one image under Temp and does not modify assets.
        /// </summary>
        /// <param name="arguments">CLI flag values. A null format stays omitted until the jpg default.</param>
        /// <param name="cancellationToken">Token used to abort the capture.</param>
        /// <returns>The canonical envelope. Argument failures do not capture.</returns>
        public static Task<NexusCommandEnvelope> ExecuteFromCli(CaptureCliArguments arguments, CancellationToken cancellationToken)
        {
            if (!TryPrepareLook(BuildCliArguments(arguments), 0, out CaptureLookCall call))
                return Task.FromResult(call.Rejected);

            return ExecuteEnveloped(call, cancellationToken);
        }

        /// <summary>
        /// Builds the CLI argument object without applying the jpg default. Null format stays omitted.
        /// </summary>
        internal static JObject BuildCliArguments(CaptureCliArguments arguments)
        {
            var parameters = new JObject
            {
                ["width"] = arguments.Width,
                ["height"] = arguments.Height,
                ["quality"] = arguments.Quality,
                ["max_dimension"] = arguments.MaxDimension,
                ["view"] = string.IsNullOrEmpty(arguments.View) ? DefaultView : arguments.View,
                ["delivery"] = string.IsNullOrEmpty(arguments.Delivery) ? DefaultDelivery : arguments.Delivery
            };
            if (!string.IsNullOrEmpty(arguments.Format)) parameters["format"] = arguments.Format;
            if (!string.IsNullOrEmpty(arguments.Target)) parameters["target"] = arguments.Target;
            if (arguments.InstanceId != 0) parameters["instance_id"] = arguments.InstanceId;
            if (!string.IsNullOrEmpty(arguments.RequestId)) parameters["request_id"] = arguments.RequestId;
            return parameters;
        }

        /// <summary>
        /// Parses HTTP or CLI arguments and applies the omitted jpg default onto <see cref="CaptureRequest.Format"/>.
        /// An unsupported format is an <c>invalid_argument</c> rejection. Does not capture.
        /// </summary>
        internal static bool TryPrepareLook(JToken parameters, long milliseconds, out CaptureLookCall call)
        {
            if (!CaptureLookRequest.TryParse(parameters, milliseconds, out call))
                return false;

            if (!TryParseFormat(FormatOrDefault(call.Format), out CaptureFormat format))
            {
                call.Rejected = NexusCommandEnvelope.InvalidArgument("Unsupported capture format.", call.RequestId, milliseconds);
                return false;
            }

            call.Request.Format = format;
            return true;
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
            CaptureResult captured = await Capture(call.Request, cancellationToken).ConfigureAwait(false);
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

        /// <summary>Uses jpg when the caller omits format.</summary>
        internal static string FormatOrDefault(string format)
        {
            return format ?? DefaultFormat;
        }

        private static bool TryParseFormat(string format, out CaptureFormat parsed)
        {
            parsed = CaptureFormat.Jpeg;
            if (string.IsNullOrEmpty(format) ||
                format.Equals("jpg", StringComparison.OrdinalIgnoreCase) ||
                format.Equals("jpeg", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            parsed = CaptureFormat.Png;
            return format.Equals("png", StringComparison.OrdinalIgnoreCase);
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
                    NexusCommandParameter.Optional("width", "integer", WidthDescription, 0),
                    NexusCommandParameter.Optional("height", "integer", HeightDescription, 0),
                    NexusCommandParameter.Optional("format", "string", FormatDescription, DefaultFormat),
                    NexusCommandParameter.Optional("quality", "integer", QualityDescription, DefaultJpegQuality),
                    NexusCommandParameter.Optional("max_dimension", "integer", MaxDimensionDescription, DefaultMaxLongEdge),
                    NexusCommandParameter.Optional("view", "string", ViewDescription, DefaultView),
                    NexusCommandParameter.Optional("target", "string", TargetDescription, null),
                    NexusCommandParameter.Optional("instance_id", "integer", InstanceIdDescription, 0),
                    NexusCommandParameter.Optional("delivery", "string", DeliveryDescription, DefaultDelivery),
                    NexusCommandParameter.Optional("request_id", "string", NexusCommandCodes.RequestIdDescription, null)
                }
            };
        }
    }

    /// <summary>
    /// Flag values from the Pipeline <c>[CliCommand]</c> projection of <see cref="CaptureGameViewCommand"/>.
    /// Zero and null mean the flag was not set.
    /// </summary>
    public sealed class CaptureCliArguments
    {
        /// <summary>Gets or sets the requested width, or 0 for the native size.</summary>
        public int Width { get; set; }
        /// <summary>Gets or sets the requested height, or 0 for the native size.</summary>
        public int Height { get; set; }
        /// <summary>Gets or sets <c>jpg</c> or <c>png</c>, or null to use the jpg default.</summary>
        public string Format { get; set; }
        /// <summary>Gets or sets JPEG quality. Values outside 1–100 are clamped.</summary>
        public int Quality { get; set; } = CaptureGameViewCommand.DefaultJpegQuality;
        /// <summary>Gets or sets the longest edge. 0 keeps the source size.</summary>
        public int MaxDimension { get; set; } = CaptureGameViewCommand.DefaultMaxLongEdge;
        /// <summary>Gets or sets <c>game</c> or <c>scene</c>, or null for game.</summary>
        public string View { get; set; }
        /// <summary>Gets or sets the object name or hierarchy path, or null.</summary>
        public string Target { get; set; }
        /// <summary>Gets or sets the object instance id. Zero means none.</summary>
        public int InstanceId { get; set; }
        /// <summary>Gets or sets <c>base64</c> or <c>file</c>, or null for base64.</summary>
        public string Delivery { get; set; }
        /// <summary>Gets or sets the caller id echoed in meta, or null.</summary>
        public string RequestId { get; set; }
    }
}
