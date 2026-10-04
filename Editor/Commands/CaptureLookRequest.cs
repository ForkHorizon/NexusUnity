using Newtonsoft.Json.Linq;
using UnityMCP.Editor.Capture;

namespace UnityMCP.Editor.Commands
{
    /// <summary>
    /// Parsed canonical capture arguments. Rejected arguments do not capture and do not write a file.
    /// </summary>
    internal sealed class CaptureLookCall
    {
        /// <summary>Gets or sets the capture request. Format is filled by the command after the jpg default.</summary>
        internal CaptureRequest Request { get; set; }
        /// <summary>Gets or sets the raw format string, or null when omitted.</summary>
        internal string Format { get; set; }
        /// <summary>Gets or sets the caller id echoed in meta.</summary>
        internal string RequestId { get; set; }
        /// <summary>Gets or sets whether the one encoded image is written under Temp instead of Base64.</summary>
        internal bool WriteFile { get; set; }
        /// <summary>Gets or sets the argument failure envelope.</summary>
        internal NexusCommandEnvelope Rejected { get; set; }
    }

    /// <summary>
    /// Reads canonical look arguments. A bad argument does not capture and does not modify the project.
    /// </summary>
    internal static class CaptureLookRequest
    {
        /// <summary>Parses look arguments. Omitted max edge becomes 768. Explicit 0 stays unconstrained.</summary>
        internal static bool TryParse(JToken parameters, long milliseconds, out CaptureLookCall call)
        {
            call = new CaptureLookCall();
            if (!NexusCommandArguments.TryReadRequestId(parameters, milliseconds, out string requestId, out NexusCommandEnvelope rejected))
                return Fail(call, rejected);
            if (!NexusCommandArguments.TryReadOptionalInt(parameters?["width"], "width", requestId, milliseconds, out int? width, out rejected))
                return Fail(call, rejected);
            if (!NexusCommandArguments.TryReadOptionalInt(parameters?["height"], "height", requestId, milliseconds, out int? height, out rejected))
                return Fail(call, rejected);
            if (!NexusCommandArguments.TryReadOptionalInt(parameters?["quality"], "quality", requestId, milliseconds, out int? quality, out rejected))
                return Fail(call, rejected);
            if (!NexusCommandArguments.TryReadOptionalString(parameters?["format"], "format", requestId, milliseconds, out string format, out rejected))
                return Fail(call, rejected);
            if (!TryReadEdge(parameters, requestId, milliseconds, out int maxEdge, out rejected))
                return Fail(call, rejected);
            if (!TryReadView(parameters, requestId, milliseconds, out CaptureView view, out string target, out int instanceId, out bool writeFile, out rejected))
                return Fail(call, rejected);

            call.RequestId = requestId;
            call.Format = format;
            call.WriteFile = writeFile;
            call.Request = new CaptureRequest
            {
                JpegQuality = quality ?? 85,
                RequestedWidth = width ?? 0,
                RequestedHeight = height ?? 0,
                MaxLongEdge = maxEdge,
                IncludeTelemetry = true,
                View = view,
                TargetInstanceId = instanceId,
                TargetPath = target
            };
            return true;
        }

        private static bool TryReadEdge(JToken parameters, string requestId, long milliseconds, out int maxEdge, out NexusCommandEnvelope rejected)
        {
            JToken edge = parameters?["max_dimension"];
            string edgeName = "max_dimension";
            if (edge == null || edge.Type == JTokenType.Null)
            {
                edge = parameters?["max_long_edge"];
                edgeName = "max_long_edge";
            }

            if (!NexusCommandArguments.TryReadOptionalInt(edge, edgeName, requestId, milliseconds, out int? parsed, out rejected))
            {
                maxEdge = 0;
                return false;
            }

            maxEdge = parsed ?? CaptureGameViewCommand.DefaultMaxLongEdge;
            return true;
        }

        private static bool TryReadView(
            JToken parameters,
            string requestId,
            long milliseconds,
            out CaptureView view,
            out string target,
            out int instanceId,
            out bool writeFile,
            out NexusCommandEnvelope rejected)
        {
            view = CaptureView.Game;
            target = null;
            instanceId = 0;
            writeFile = false;
            if (!NexusCommandArguments.TryReadOptionalString(parameters?["view"], "view", requestId, milliseconds, out string viewText, out rejected))
                return false;
            if (!NexusCommandArguments.TryReadOptionalString(parameters?["target"], "target", requestId, milliseconds, out target, out rejected))
                return false;
            if (!NexusCommandArguments.TryReadOptionalInt(parameters?["instance_id"], "instance_id", requestId, milliseconds, out int? id, out rejected))
                return false;
            if (!NexusCommandArguments.TryReadOptionalString(parameters?["delivery"], "delivery", requestId, milliseconds, out string delivery, out rejected))
                return false;

            viewText = string.IsNullOrEmpty(viewText) ? "game" : viewText;
            delivery = string.IsNullOrEmpty(delivery) ? "base64" : delivery;
            if (!TryViewToken(viewText, out view))
                return Reject(requestId, milliseconds, "view must be game or scene.", out rejected);
            if (delivery != "base64" && delivery != "file")
                return Reject(requestId, milliseconds, "delivery must be base64 or file.", out rejected);
            if (target != null && target.Trim().Length == 0)
                return Reject(requestId, milliseconds, "target must be a non-empty name or hierarchy path.", out rejected);
            instanceId = id ?? 0;
            if (instanceId != 0 && !string.IsNullOrEmpty(target))
                return Reject(requestId, milliseconds, "Pass instance_id or target, not both.", out rejected);
            if (view == CaptureView.Scene && instanceId == 0 && string.IsNullOrEmpty(target))
                return Reject(requestId, milliseconds, "Scene View capture requires a target.", out rejected);

            writeFile = delivery == "file";
            return true;
        }

        private static bool TryViewToken(string viewText, out CaptureView view)
        {
            if (viewText == "game")
            {
                view = CaptureView.Game;
                return true;
            }

            if (viewText == "scene")
            {
                view = CaptureView.Scene;
                return true;
            }

            view = CaptureView.Game;
            return false;
        }

        private static bool Reject(string requestId, long milliseconds, string message, out NexusCommandEnvelope rejected)
        {
            rejected = NexusCommandEnvelope.Failure(
                NexusCommandCodes.InvalidArgument,
                message,
                false,
                NexusCommandCodes.ChangeArgumentsHint,
                null,
                requestId,
                milliseconds);
            return false;
        }

        private static bool Fail(CaptureLookCall call, NexusCommandEnvelope rejected)
        {
            call.Rejected = rejected;
            return false;
        }
    }
}
