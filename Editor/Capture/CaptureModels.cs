// Capture V2 request, result, format and timing models.
namespace UnityMCP.Editor.Capture
{
    /// <summary>
    /// Image encodings supported by Nexus Capture V2: <see cref="CaptureFormat.Png"/> is lossless, <see cref="CaptureFormat.Jpeg"/> is lossy and smaller.
    /// </summary>
    public enum CaptureFormat
    {
        /// <summary>Lossless PNG. Default for the Legacy screenshot contract.</summary>
        Png = 0,
        /// <summary>JPEG with configurable quality.</summary>
        Jpeg = 1
    }

    /// <summary>Editor surface captured by the canonical look command.</summary>
    public enum CaptureView
    {
        /// <summary>Presented Game View, including Overlay UI composited by Unity.</summary>
        Game = 0,
        /// <summary>Scene View framed on a target object.</summary>
        Scene = 1
    }

    /// <summary>
    /// Semantic Game View capture options. Transport concerns such as Base64 do not belong here.
    /// </summary>
    public sealed class CaptureRequest
    {
        /// <summary>Gets or sets the compressed output format. Defaults to PNG.</summary>
        public CaptureFormat Format { get; set; } = CaptureFormat.Png;

        /// <summary>Gets or sets JPEG quality in the range 1–100. Ignored for PNG.</summary>
        public int JpegQuality { get; set; } = 85;

        /// <summary>Gets or sets an explicit output width. Zero keeps the source width.</summary>
        public int RequestedWidth { get; set; }

        /// <summary>Gets or sets an explicit output height. Zero keeps the source height.</summary>
        public int RequestedHeight { get; set; }

        /// <summary>Gets or sets a max longest-edge constraint. Zero leaves the size unconstrained up to the encoder cap.</summary>
        public int MaxLongEdge { get; set; }

        /// <summary>Gets or sets whether stage timings are populated on the result.</summary>
        public bool IncludeTelemetry { get; set; }

        /// <summary>Gets or sets Game View or a Scene View framed on the target.</summary>
        public CaptureView View { get; set; } = CaptureView.Game;

        /// <summary>Gets or sets an instance id to crop or frame. Zero means no id was supplied.</summary>
        public int TargetInstanceId { get; set; }

        /// <summary>Gets or sets a hierarchy path or unique object name. Null captures the full Game View.</summary>
        public string TargetPath { get; set; }
    }

    /// <summary>
    /// Compressed capture output plus metadata. Bytes are raw encoded image data, not Base64.
    /// </summary>
    public sealed class CaptureResult
    {
        /// <summary>Gets or sets the compressed image bytes.</summary>
        public byte[] CompressedBytes { get; set; }

        /// <summary>Gets or sets the encoded format of <see cref="CompressedBytes"/>.</summary>
        public CaptureFormat Format { get; set; }

        /// <summary>Gets or sets the encoded image width in pixels.</summary>
        public int Width { get; set; }

        /// <summary>Gets or sets the encoded image height in pixels.</summary>
        public int Height { get; set; }

        /// <summary>Gets or sets a semantic source label such as <c>game_view</c>.</summary>
        public string Source { get; set; }

        /// <summary>Gets or sets optional stage timings when telemetry was requested.</summary>
        public CaptureTimings Timings { get; set; }
    }

    /// <summary>
    /// Lightweight capture stage timings in milliseconds.
    /// </summary>
    public sealed class CaptureTimings
    {
        /// <summary>Time to acquire the presented source and copy into a Nexus-owned RT.</summary>
        public double SourceAcquireMs { get; set; }

        /// <summary>Time to submit <c>AsyncGPUReadback.Request</c>.</summary>
        public double ReadbackSubmitMs { get; set; }

        /// <summary>Time from submit until the readback first reports done.</summary>
        public double SubmitToDoneMs { get; set; }

        /// <summary>Time to encode compressed bytes on the main thread.</summary>
        public double EncodeMs { get; set; }

        /// <summary>Observed main-thread stall for acquire, submit, get-data, and encode.</summary>
        public double MainThreadStallMs { get; set; }

        /// <summary>End-to-end internal capture time excluding transport serialization.</summary>
        public double TotalInternalMs { get; set; }

        /// <summary>EditorApplication.update ticks from readback submit until request.done.</summary>
        public int EditorTicksSubmitToDone { get; set; }
    }
}
