using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor.Commands
{
    /// <summary>
    /// Reads canonical command arguments before the handler runs.
    /// A rejected argument does not call the handler and does not modify the project.
    /// </summary>
    internal sealed class NexusCommandArguments
    {
        private readonly JToken _parameters;
        private readonly long _milliseconds;

        private NexusCommandArguments(JToken parameters, long milliseconds)
        {
            _parameters = parameters;
            _milliseconds = milliseconds;
        }

        /// <summary>Gets the caller id echoed in meta, or null when omitted.</summary>
        internal string RequestId { get; private set; }

        /// <summary>Gets the first argument failure, or null while every argument read so far is valid.</summary>
        internal NexusCommandEnvelope Rejected { get; private set; }

        /// <summary>Starts reading and validates the optional string <c>request_id</c>.</summary>
        /// <returns><c>false</c> with <see cref="Rejected"/> set when <c>request_id</c> is not a string.</returns>
        internal static bool TryOpen(JToken parameters, long milliseconds, out NexusCommandArguments arguments)
        {
            arguments = new NexusCommandArguments(parameters, milliseconds);
            JToken token = parameters?["request_id"];
            if (IsOmitted(token)) return true;
            if (token.Type != JTokenType.String) return arguments.Reject("request_id must be a string.");
            arguments.RequestId = token.Value<string>();
            return true;
        }

        /// <summary>Returns whether the caller sent a non-null value for <paramref name="name"/>.</summary>
        internal bool Has(string name)
        {
            return !IsOmitted(_parameters?[name]);
        }

        /// <summary>Reads an optional integer. Omitted or null leaves <paramref name="value"/> null.</summary>
        internal bool TryReadInt(string name, out int? value)
        {
            value = null;
            JToken token = _parameters?[name];
            if (IsOmitted(token)) return true;
            if (token.Type != JTokenType.Integer) return Reject(name + " must be an integer.");
            try
            {
                value = token.Value<int>();
                return true;
            }
            catch (System.Exception)
            {
                return Reject(name + " must be an integer.");
            }
        }

        /// <summary>Reads an optional string. Omitted or null leaves <paramref name="value"/> null.</summary>
        internal bool TryReadString(string name, out string value)
        {
            value = null;
            JToken token = _parameters?[name];
            if (IsOmitted(token)) return true;
            if (token.Type != JTokenType.String) return Reject(name + " must be a string.");
            value = token.Value<string>();
            return true;
        }

        /// <summary>Records an <c>invalid_argument</c> failure and returns <c>false</c>.</summary>
        internal bool Reject(string message)
        {
            Rejected = NexusCommandEnvelope.InvalidArgument(message, RequestId, _milliseconds);
            return false;
        }

        private static bool IsOmitted(JToken token)
        {
            return token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined;
        }
    }
}
