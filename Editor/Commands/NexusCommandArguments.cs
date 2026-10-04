using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor.Commands
{
    /// <summary>
    /// Reads canonical command arguments before the handler runs.
    /// A rejected argument does not call the handler and does not modify the project.
    /// </summary>
    internal static class NexusCommandArguments
    {
        internal static bool TryReadRequestId(
            JToken parameters,
            long milliseconds,
            out string requestId,
            out NexusCommandEnvelope rejected)
        {
            requestId = null;
            rejected = null;
            JToken token = parameters?["request_id"];
            if (token == null || token.Type == JTokenType.Null) return true;
            if (token.Type != JTokenType.String)
            {
                rejected = Reject("request_id must be a string.", null, milliseconds);
                return false;
            }

            requestId = token.Value<string>();
            return true;
        }

        internal static bool TryReadOptionalInt(
            JToken token,
            string name,
            string requestId,
            long milliseconds,
            out int? value,
            out NexusCommandEnvelope rejected)
        {
            value = null;
            rejected = null;
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
                return true;
            if (token.Type != JTokenType.Integer)
            {
                rejected = Reject(name + " must be an integer.", requestId, milliseconds);
                return false;
            }

            try
            {
                value = token.Value<int>();
                return true;
            }
            catch (System.Exception)
            {
                rejected = Reject(name + " must be an integer.", requestId, milliseconds);
                return false;
            }
        }

        internal static bool TryReadOptionalString(
            JToken token,
            string name,
            string requestId,
            long milliseconds,
            out string value,
            out NexusCommandEnvelope rejected)
        {
            value = null;
            rejected = null;
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
                return true;
            if (token.Type != JTokenType.String)
            {
                rejected = Reject(name + " must be a string.", requestId, milliseconds);
                return false;
            }

            value = token.Value<string>();
            return true;
        }

        private static NexusCommandEnvelope Reject(string message, string requestId, long milliseconds)
        {
            return NexusCommandEnvelope.Failure(
                NexusCommandCodes.InvalidArgument,
                message,
                false,
                NexusCommandCodes.ChangeArgumentsHint,
                null,
                requestId,
                milliseconds);
        }
    }
}
