// Maps Capture V2 failures onto canonical envelope error codes and hints.
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
            NexusCommandError error = Describe(capture.Code, capture.Message);
            bool internalFailure = error.Code == NexusCommandCodes.Internal;
            return NexusCommandEnvelope.Failure(error, null, requestId, milliseconds, internalFailure ? capture : null);
        }

        private static NexusCommandError Describe(CaptureErrorCode code, string message)
        {
            switch (code)
            {
                case CaptureErrorCode.UnsupportedFormat:
                case CaptureErrorCode.TargetNotFound:
                    return NexusCommandError.Create(NexusCommandCodes.InvalidArgument, message, false, NexusCommandCodes.ChangeArgumentsHint);
                case CaptureErrorCode.GameViewUnavailable:
                case CaptureErrorCode.CaptureSourceEmpty:
                    return Retry(NexusCommandCodes.Unavailable, message, "Open a Game View that has a visible area, then call again.");
                case CaptureErrorCode.SceneViewUnavailable:
                    return Retry(NexusCommandCodes.Unavailable, message, "Open a Scene View, then call again.");
                case CaptureErrorCode.TargetDisabled:
                    return Retry(NexusCommandCodes.Unavailable, message, "Enable the object, then call again.");
                case CaptureErrorCode.TargetNotVisible:
                    return Retry(NexusCommandCodes.Unavailable, message, "Move the target into the Game View, or capture the Scene View, then call again.");
                case CaptureErrorCode.CaptureBusy:
                    return Retry(NexusCommandCodes.Busy, message, "Wait for the in-flight capture to finish, then call again.");
                case CaptureErrorCode.DomainReloadInterrupted:
                    return Retry(NexusCommandCodes.EditorNotReady, message, "Wait until the editor finishes reloading, then call again.");
                case CaptureErrorCode.ReadbackFailed when IsTimeout(message):
                    return Retry(NexusCommandCodes.Timeout, message, "The read timed out before an image was produced. It is safe to call again.");
                case CaptureErrorCode.ReadbackFailed:
                    return Retry(NexusCommandCodes.CaptureFailed, message, "GPU readback failed before an image was produced. It is safe to call again.");
                default:
                    return NexusCommandError.Create(NexusCommandCodes.Internal, message, false, NexusCommandCodes.InternalHint);
            }
        }

        private static NexusCommandError Retry(string code, string message, string hint)
        {
            return NexusCommandError.Create(code, message, true, hint);
        }

        private static bool IsTimeout(string message)
        {
            return message != null && message.IndexOf("timed out", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
