using System;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityMCP.Editor.Capture;
using UnityMCP.Editor.Runtime;

namespace UnityMCP.Editor.Commands
{
    /// <summary>Stable codes for the canonical Nexus command envelope.</summary>
    public static class NexusCommandCodes
    {
        /// <summary>Arguments must change before another call. Not retryable.</summary>
        public const string InvalidArgument = "invalid_argument";
        /// <summary>A required editor surface such as Game View is missing. Retryable.</summary>
        public const string Unavailable = "unavailable";
        /// <summary>Another capture already holds the gate. Retryable. Not a job.</summary>
        public const string Busy = "busy";
        /// <summary>A read timed out before producing a result. Retryable for reads.</summary>
        public const string Timeout = "timeout";
        /// <summary>GPU readback failed without timing out. Retryable.</summary>
        public const string CaptureFailed = "capture_failed";
        /// <summary>A read finished with an incomplete payload. Retryable.</summary>
        public const string Partial = "partial";
        /// <summary>The editor is reloading assemblies. Retryable after it is idle.</summary>
        public const string EditorNotReady = "editor_not_ready";
        /// <summary>An unexpected failure. Not retryable.</summary>
        public const string Internal = "internal";
        /// <summary>Mutation outcome is not known. Forces retryable to false.</summary>
        public const string UnknownOutcome = "unknown";

        /// <summary>Hint for a permanent argument failure.</summary>
        public const string ChangeArgumentsHint = "Change the arguments. Do not send the same arguments again.";
        /// <summary>Hint used when a mutation's outcome cannot be known.</summary>
        public const string UnknownOutcomeHint =
            "The outcome is unknown. Do not retry this call. Inspect the editor before doing anything else.";
        /// <summary>Shared description for the optional caller id copied into meta.</summary>
        public const string RequestIdDescription =
            "Optional string copied into meta.request_id. It is not stored.";
    }

    /// <summary>
    /// Machine-readable failure for one canonical command. Serialization omits outcome and stack unless set.
    /// </summary>
    public sealed class NexusCommandError
    {
        /// <summary>Gets or sets the stable code.</summary>
        [JsonProperty("code")] public string Code { get; set; }
        /// <summary>Gets or sets the human-readable failure text.</summary>
        [JsonProperty("message")] public string Message { get; set; }
        /// <summary>Gets or sets whether the same call is safe to send again.</summary>
        [JsonProperty("retryable")] public bool Retryable { get; set; }
        /// <summary>Gets or sets the agent-facing next step.</summary>
        [JsonProperty("hint")] public string Hint { get; set; }
        /// <summary>Gets or sets <c>unknown</c> when a mutation's outcome cannot be known.</summary>
        [JsonProperty("outcome", NullValueHandling = NullValueHandling.Ignore)] public string Outcome { get; set; }
        /// <summary>Gets or sets a stack trace when <c>NEXUS_DEBUG</c> is set.</summary>
        [JsonProperty("stack_trace", NullValueHandling = NullValueHandling.Ignore)] public string StackTrace { get; set; }
    }

    /// <summary>
    /// Correlation for one canonical command. <c>gen</c> is the editor session generation, not a compile epoch.
    /// </summary>
    public sealed class NexusCommandMeta
    {
        /// <summary>Gets or sets elapsed handler time in milliseconds.</summary>
        [JsonProperty("ms")] public long Ms { get; set; }
        /// <summary>Gets or sets the effective runtime wire name, <c>legacy</c> or <c>pipeline</c>.</summary>
        [JsonProperty("mode")] public string Mode { get; set; }
        /// <summary>Gets or sets <see cref="MCPServer.SessionGeneration"/>.</summary>
        [JsonProperty("gen")] public int Gen { get; set; }
        /// <summary>Gets or sets the caller string id, or null when the caller did not send one.</summary>
        [JsonProperty("request_id")] public string RequestId { get; set; }
    }

    /// <summary>
    /// Canonical result for <c>nexus.project_map</c>, <c>nexus.group_compile_errors</c>, and <c>nexus.capture_game_view</c>.
    /// Building meta reads the editor session generation and the cached runtime mode. It does not modify the project.
    /// </summary>
    public sealed class NexusCommandEnvelope
    {
        /// <summary>Gets or sets whether the payload is complete.</summary>
        [JsonProperty("ok")] public bool Ok { get; set; }
        /// <summary>Gets or sets the command payload, or null when there is nothing to return.</summary>
        [JsonProperty("data")] public JToken Data { get; set; }
        /// <summary>Gets or sets the failure, or null on success.</summary>
        [JsonProperty("error")] public NexusCommandError Error { get; set; }
        /// <summary>Gets or sets timing and correlation.</summary>
        [JsonProperty("meta")] public NexusCommandMeta Meta { get; set; }

        /// <summary>
        /// Wraps a finished payload. Reads the editor session generation and does not modify the project.
        /// </summary>
        /// <param name="data">Command payload. May be null.</param>
        /// <param name="requestId">Caller id to echo, or null.</param>
        /// <param name="milliseconds">Elapsed handler time.</param>
        /// <returns>An envelope with <c>ok</c> true and <c>error</c> null.</returns>
        public static NexusCommandEnvelope Success(object data, string requestId, long milliseconds)
        {
            return new NexusCommandEnvelope
            {
                Ok = true,
                Data = Box(data),
                Error = null,
                Meta = CreateMeta(requestId, milliseconds)
            };
        }

        /// <summary>
        /// Builds a failure. An <paramref name="outcome"/> of <c>unknown</c> forces <c>retryable</c> false.
        /// </summary>
        /// <param name="code">Stable code.</param>
        /// <param name="message">Human-readable failure text.</param>
        /// <param name="retryable">Whether the same call is safe to send again.</param>
        /// <param name="hint">Agent-facing next step.</param>
        /// <param name="data">Partial payload, or null.</param>
        /// <param name="requestId">Caller id to echo, or null.</param>
        /// <param name="milliseconds">Elapsed handler time.</param>
        /// <param name="outcome">Set to <c>unknown</c> only when a mutation's outcome cannot be known.</param>
        /// <param name="stackSource">Exception whose stack is included only when <c>NEXUS_DEBUG</c> is set.</param>
        /// <returns>An envelope with <c>ok</c> false.</returns>
        public static NexusCommandEnvelope Failure(
            string code,
            string message,
            bool retryable,
            string hint,
            object data,
            string requestId,
            long milliseconds,
            string outcome = null,
            Exception stackSource = null)
        {
            if (string.Equals(outcome, NexusCommandCodes.UnknownOutcome, StringComparison.Ordinal))
            {
                retryable = false;
                hint = NexusCommandCodes.UnknownOutcomeHint;
            }

            string stack = null;
            if (stackSource != null && IsDebugEnabled())
                stack = stackSource.StackTrace;

            return new NexusCommandEnvelope
            {
                Ok = false,
                Data = Box(data),
                Error = new NexusCommandError
                {
                    Code = code,
                    Message = string.IsNullOrEmpty(message) ? "The command failed." : message,
                    Retryable = retryable,
                    Hint = string.IsNullOrEmpty(hint)
                        ? (retryable
                            ? "The same call can be sent again."
                            : "Do not send the same call again until the cause is understood.")
                        : hint,
                    Outcome = outcome,
                    StackTrace = string.IsNullOrEmpty(stack) ? null : stack
                },
                Meta = CreateMeta(requestId, milliseconds)
            };
        }

        /// <summary>
        /// Maps a thrown exception to the envelope. Read timeouts stay retryable and do not set an unknown outcome.
        /// </summary>
        /// <param name="exception">Failure from a canonical handler. Wrapper exceptions are unwrapped.</param>
        /// <param name="requestId">Caller id to echo, or null.</param>
        /// <param name="milliseconds">Elapsed handler time.</param>
        /// <returns>An envelope with <c>ok</c> false and a stable code.</returns>
        public static NexusCommandEnvelope FromException(Exception exception, string requestId, long milliseconds)
        {
            Exception ex = Unwrap(exception) ?? exception;
            if (ex is CaptureException capture) return NexusCaptureErrors.FromCapture(capture, requestId, milliseconds);
            if (ex is ArgumentException || ex is FormatException || ex is OverflowException || ex is JsonReaderException)
            {
                return Failure(
                    NexusCommandCodes.InvalidArgument,
                    ex.Message,
                    false,
                    NexusCommandCodes.ChangeArgumentsHint,
                    null,
                    requestId,
                    milliseconds);
            }

            return Failure(
                NexusCommandCodes.Internal,
                ex == null || string.IsNullOrEmpty(ex.Message) ? "The command failed." : ex.Message,
                false,
                "The command failed before it could finish. Do not retry it blindly.",
                null,
                requestId,
                milliseconds,
                stackSource: ex);
        }

        /// <summary>
        /// Projects grouped compiler errors. A list taken while the editor is compiling is <c>partial</c>, not success.
        /// </summary>
        /// <param name="data">Handler payload. <c>data.success</c> is not treated as <c>ok</c>.</param>
        /// <param name="requestId">Caller id to echo, or null.</param>
        /// <param name="milliseconds">Elapsed handler time.</param>
        /// <returns>Success when the editor is idle, otherwise a partial envelope that keeps the groups.</returns>
        public static NexusCommandEnvelope ProjectCompile(NexusCompileErrorsResult data, string requestId, long milliseconds)
        {
            if (data != null && data.IsCompiling)
            {
                return Failure(
                    NexusCommandCodes.Partial,
                    "The editor is compiling, so this error list may be incomplete.",
                    true,
                    "Wait until compilation finishes, then call again. Do not treat this list as complete.",
                    data,
                    requestId,
                    milliseconds);
            }

            return Success(data, requestId, milliseconds);
        }

        /// <summary>Serializes the envelope with null <c>error</c> or <c>data</c> preserved.</summary>
        /// <returns>JSON object for a JSON-RPC result.</returns>
        public JObject ToJson()
        {
            return JObject.FromObject(this);
        }

        private static NexusCommandMeta CreateMeta(string requestId, long milliseconds)
        {
            return new NexusCommandMeta
            {
                Ms = milliseconds < 0 ? 0 : milliseconds,
                Mode = NexusRuntimeSelector.ToWireName(NexusRuntimeHost.EffectiveMode),
                Gen = MCPServer.SessionGeneration,
                RequestId = requestId
            };
        }

        private static JToken Box(object data)
        {
            if (data == null) return null;
            if (data is JToken token) return token;
            return JToken.FromObject(data);
        }

        private static Exception Unwrap(Exception exception)
        {
            while (exception is TargetInvocationException invoked && invoked.InnerException != null)
                exception = invoked.InnerException;
            while (exception is AggregateException aggregate && aggregate.InnerExceptions.Count == 1)
                exception = aggregate.InnerExceptions[0];
            return exception;
        }

        private static bool IsDebugEnabled()
        {
            string value = Environment.GetEnvironmentVariable("NEXUS_DEBUG");
            if (string.IsNullOrWhiteSpace(value)) return false;
            return !value.Equals("0", StringComparison.OrdinalIgnoreCase)
                && !value.Equals("false", StringComparison.OrdinalIgnoreCase);
        }
    }
}
