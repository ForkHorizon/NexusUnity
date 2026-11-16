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
            call = new CaptureLookCall { Request = new CaptureRequest { IncludeTelemetry = true } };
            bool parsed = NexusCommandArguments.TryOpen(parameters, milliseconds, out NexusCommandArguments arguments)
                && TryReadImage(arguments, call)
                && TryReadView(arguments, call);
            call.RequestId = arguments.RequestId;
            call.Rejected = arguments.Rejected;
            return parsed;
        }

        private static bool TryReadImage(NexusCommandArguments arguments, CaptureLookCall call)
        {
            string edgeName = arguments.Has("max_dimension") ? "max_dimension" : "max_long_edge";
            if (!arguments.TryReadInt("width", out int? width)
                || !arguments.TryReadInt("height", out int? height)
                || !arguments.TryReadInt("quality", out int? quality)
                || !arguments.TryReadString("format", out string format)
                || !arguments.TryReadInt(edgeName, out int? maxEdge))
            {
                return false;
            }

            CaptureRequest request = call.Request;
            request.RequestedWidth = width ?? 0;
            request.RequestedHeight = height ?? 0;
            request.JpegQuality = quality ?? CaptureGameViewCommand.DefaultJpegQuality;
            request.MaxLongEdge = maxEdge ?? CaptureGameViewCommand.DefaultMaxLongEdge;
            call.Format = format;
            return true;
        }

        private static bool TryReadView(NexusCommandArguments arguments, CaptureLookCall call)
        {
            if (!arguments.TryReadString("view", out string viewText)
                || !arguments.TryReadString("target", out string target)
                || !arguments.TryReadInt("instance_id", out int? id)
                || !arguments.TryReadString("delivery", out string delivery))
            {
                return false;
            }

            viewText = string.IsNullOrEmpty(viewText) ? CaptureGameViewCommand.DefaultView : viewText;
            delivery = string.IsNullOrEmpty(delivery) ? CaptureGameViewCommand.DefaultDelivery : delivery;
            int instanceId = id ?? 0;
            if (!TryViewToken(viewText, out CaptureView view))
                return arguments.Reject("view must be game or scene.");
            if (delivery != CaptureGameViewCommand.DefaultDelivery && delivery != "file")
                return arguments.Reject("delivery must be base64 or file.");
            if (target != null && target.Trim().Length == 0)
                return arguments.Reject("target must be a non-empty name or hierarchy path.");
            if (instanceId != 0 && !string.IsNullOrEmpty(target))
                return arguments.Reject("Pass instance_id or target, not both.");
            if (view == CaptureView.Scene && instanceId == 0 && string.IsNullOrEmpty(target))
                return arguments.Reject("Scene View capture requires a target.");

            call.Request.View = view;
            call.Request.TargetInstanceId = instanceId;
            call.Request.TargetPath = target;
            call.WriteFile = delivery == "file";
            return true;
        }

        private static bool TryViewToken(string viewText, out CaptureView view)
        {
            if (viewText == CaptureGameViewCommand.DefaultView)
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
    }
}
