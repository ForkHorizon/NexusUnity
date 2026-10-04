using UnityMCP.Editor.Capture;

namespace UnityMCP.Editor.Commands
{
    /// <summary>
    /// Maps capture failures onto the canonical envelope. Read timeouts stay retryable and do not set an unknown outcome.
    /// </summary>
    internal static class NexusCaptureErrors
    {
        /// <summary>Returns the envelope for a capture failure. Does not modify the project.</summary>
        internal static NexusCommandEnvelope FromCapture(CaptureException capture, string requestId, long milliseconds)
        {
            string message = capture.Message;
            switch (capture.Code)
            {
                case CaptureErrorCode.UnsupportedFormat:
                    return Fail(NexusCommandCodes.InvalidArgument, message, false, NexusCommandCodes.ChangeArgumentsHint, requestId, milliseconds);
                case CaptureErrorCode.TargetNotFound:
                    return Fail(NexusCommandCodes.InvalidArgument, message, false, NexusCommandCodes.ChangeArgumentsHint, requestId, milliseconds);
                case CaptureErrorCode.GameViewUnavailable:
                case CaptureErrorCode.CaptureSourceEmpty:
                    return Fail(NexusCommandCodes.Unavailable, message, true, "Open a Game View that has a visible area, then call again.", requestId, milliseconds);
                case CaptureErrorCode.SceneViewUnavailable:
                    return Fail(NexusCommandCodes.Unavailable, message, true, "Open a Scene View, then call again.", requestId, milliseconds);
                case CaptureErrorCode.TargetDisabled:
                    return Fail(NexusCommandCodes.Unavailable, message, true, "Enable the object, then call again.", requestId, milliseconds);
                case CaptureErrorCode.TargetNotVisible:
                    return Fail(NexusCommandCodes.Unavailable, message, true, "Move the target into the Game View, or capture the Scene View, then call again.", requestId, milliseconds);
                case CaptureErrorCode.CaptureBusy:
                    return Fail(NexusCommandCodes.Busy, message, true, "Wait for the in-flight capture to finish, then call again.", requestId, milliseconds);
                case CaptureErrorCode.DomainReloadInterrupted:
                    return Fail(NexusCommandCodes.EditorNotReady, message, true, "Wait until the editor finishes reloading, then call again.", requestId, milliseconds);
                case CaptureErrorCode.ReadbackFailed when message != null &&
                    message.IndexOf("timed out", System.StringComparison.OrdinalIgnoreCase) >= 0:
                    return Fail(NexusCommandCodes.Timeout, message, true, "The read timed out before an image was produced. It is safe to call again.", requestId, milliseconds);
                case CaptureErrorCode.ReadbackFailed:
                    return Fail(NexusCommandCodes.CaptureFailed, message, true, "GPU readback failed before an image was produced. It is safe to call again.", requestId, milliseconds);
                default:
                    return NexusCommandEnvelope.Failure(
                        NexusCommandCodes.Internal,
                        message,
                        false,
                        "The command failed before it could finish. Do not retry it blindly.",
                        null,
                        requestId,
                        milliseconds,
                        stackSource: capture);
            }
        }

        private static NexusCommandEnvelope Fail(
            string code,
            string message,
            bool retryable,
            string hint,
            string requestId,
            long milliseconds)
        {
            return NexusCommandEnvelope.Failure(code, message, retryable, hint, null, requestId, milliseconds);
        }
    }
}
