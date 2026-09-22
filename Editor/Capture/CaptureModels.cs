using System;

namespace UnityMCP.Editor.Capture
{
    /// <summary>
    /// Compressed image format produced by Nexus Capture V2.
    /// </summary>
    public enum CaptureFormat
    {
        /// <summary>Lossless PNG. Default for the Legacy screenshot contract.</summary>
        Png = 0,
        /// <summary>JPEG with configurable quality.</summary>
        Jpeg = 1
    }

    /// <summary>
    /// Domain error codes for capture failures. Transport adapters serialize these.
    /// </summary>
    public enum CaptureErrorCode
    {
        /// <summary>Game View window is missing or closed.</summary>
        GameViewUnavailable = 1,
        /// <summary>Presented Game View pixels could not be acquired.</summary>
        CaptureSourceEmpty = 2,
        /// <summary>Another GPU screenshot is already in flight.</summary>
        CaptureBusy = 3,
        /// <summary>Async GPU readback failed or timed out.</summary>
        ReadbackFailed = 4,
        /// <summary>Requested encoding format is not supported.</summary>
        UnsupportedFormat = 5,
        /// <summary>Capture was cancelled or interrupted by domain reload.</summary>
        DomainReloadInterrupted = 6
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

        /// <summary>Gets or sets a max longest-edge constraint. Zero leaves the size unconstrained.</summary>
        public int MaxLongEdge { get; set; }

        /// <summary>Gets or sets whether stage timings are populated on the result.</summary>
        public bool IncludeTelemetry { get; set; }
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

    /// <summary>
    /// Transport-independent capture failure.
    /// </summary>
    public sealed class CaptureException : Exception
    {
        /// <summary>
        /// Creates a capture exception with a stable domain code.
        /// </summary>
        /// <param name="code">Machine-readable failure code.</param>
        /// <param name="message">Human-readable failure message.</param>
        public CaptureException(CaptureErrorCode code, string message)
            : base(message)
        {
            Code = code;
        }

        /// <summary>Gets the domain error code.</summary>
        public CaptureErrorCode Code { get; }
    }
}
