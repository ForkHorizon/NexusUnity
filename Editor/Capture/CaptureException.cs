using System;

namespace UnityMCP.Editor.Capture
{
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
        DomainReloadInterrupted = 6,
        /// <summary>No Scene View is open, so a framed capture cannot run.</summary>
        SceneViewUnavailable = 7,
        /// <summary>The selector did not resolve to one scene object.</summary>
        TargetNotFound = 8,
        /// <summary>The target is disabled and has no visible pixels.</summary>
        TargetDisabled = 9,
        /// <summary>The target is outside the Game View or too small to crop.</summary>
        TargetNotVisible = 10
    }

    /// <summary>
    /// Capture failure carrying a stable <see cref="CaptureErrorCode"/> and a human-readable message.
    /// Thrown by the capture gateway and translated by each transport (HTTP, Pipeline) without leaking transport details.
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
