using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace UnityMCP.Editor.Commands
{
    /// <summary>
    /// Canonical handler that reads Unity console logs and groups C# compiler errors by source file.
    /// </summary>
    public sealed class GroupCompileErrorsCommand : INexusCommand
    {
        /// <summary>Canonical command id.</summary>
        public const string Id = "nexus.group_compile_errors";
        /// <summary>HTTP / Unity CLI alias used by the hybrid proof of concept.</summary>
        public const string Alias = "nexus_group_compile_errors";
        /// <summary>Shared command description for every projection.</summary>
        public const string Description =
            "Analyzes recent console logs, extracts C# compiler errors (CSxxxx), and groups them by root cause file.";
        /// <summary>Shared parameter description for <c>max_logs</c>.</summary>
        public const string MaxLogsDescription = "Maximum number of recent console log entries to inspect.";
        /// <summary>Console entries inspected when max_logs is omitted.</summary>
        public const int DefaultMaxLogs = 50;

        /// <summary>
        /// Reads recent error logs. Tests replace this to observe the integer <see cref="Execute"/> actually requests.
        /// </summary>
        internal static Func<int, List<UnityMCP.Editor.LogEntry>> ReadErrorLogs =
            count => MCPServer.GetLogs(count, "Error", string.Empty);

        /// <summary>Gets the canonical compile-error descriptor.</summary>
        public NexusCommandDescriptor Descriptor { get; } = new NexusCommandDescriptor
        {
            Id = Id,
            Aliases = new[] { Alias },
            Title = "Group compile errors",
            Description = Description,
            Profiles = new[] { "core" },
            Parameters = new[]
            {
                NexusCommandParameter.Optional("max_logs", "integer", MaxLogsDescription, DefaultMaxLogs),
                NexusCommandParameter.Optional("request_id", "string", NexusCommandCodes.RequestIdDescription, null)
            }
        };

        /// <summary>
        /// Groups compiler errors and returns the canonical envelope for Legacy HTTP/MCP.
        /// Reads the Unity console and does not modify the project.
        /// </summary>
        /// <param name="parameters">Optional <c>max_logs</c> integer and string <c>request_id</c>.</param>
        /// <param name="cancellationToken">Unused; the command is synchronous.</param>
        /// <returns>Envelope JSON. A list taken while compiling uses <c>partial</c>.</returns>
        public Task<JToken> ExecuteAsync(JToken parameters, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            if (!TryReadCompileArguments(parameters, sw.ElapsedMilliseconds, out int maxLogs, out string requestId, out NexusCommandEnvelope rejected))
                return Task.FromResult<JToken>(rejected.ToJson());

            return Task.FromResult<JToken>(ExecuteEnveloped(maxLogs, requestId).ToJson());
        }

        /// <summary>
        /// Resolves an omitted <c>max_logs</c> to <see cref="DefaultMaxLogs"/>. Does not read the console.
        /// </summary>
        /// <param name="parameters">Optional <c>max_logs</c> integer and string <c>request_id</c>.</param>
        /// <param name="milliseconds">Elapsed time copied into a rejection envelope.</param>
        /// <param name="maxLogs">Resolved window. Zero is a real window, not an omit.</param>
        /// <param name="requestId">Caller id, or null.</param>
        /// <param name="rejected">Argument failure, or null when <paramref name="maxLogs"/> is set.</param>
        /// <returns><c>false</c> when the arguments are rejected.</returns>
        internal static bool TryReadCompileArguments(
            JToken parameters,
            long milliseconds,
            out int maxLogs,
            out string requestId,
            out NexusCommandEnvelope rejected)
        {
            maxLogs = 0;
            int? parsed = null;
            bool valid = NexusCommandArguments.TryOpen(parameters, milliseconds, out NexusCommandArguments arguments)
                && arguments.TryReadInt("max_logs", out parsed)
                && (!parsed.HasValue || parsed.Value >= 0 || arguments.Reject("max_logs must be zero or greater."));
            requestId = arguments.RequestId;
            rejected = arguments.Rejected;
            if (!valid) return false;

            maxLogs = parsed ?? DefaultMaxLogs;
            return true;
        }

        /// <summary>
        /// Builds the canonical compile-error envelope. Reads the Unity console and does not modify the project.
        /// </summary>
        /// <param name="maxLogs">Maximum recent console entries. Negative values are <c>invalid_argument</c>.</param>
        /// <param name="requestId">Caller id echoed in <c>meta.request_id</c>, or null.</param>
        /// <returns>Success, <c>partial</c> while compiling, or <c>invalid_argument</c>.</returns>
        public static NexusCommandEnvelope ExecuteEnveloped(int maxLogs, string requestId)
        {
            var sw = Stopwatch.StartNew();
            if (maxLogs < 0)
            {
                return NexusCommandEnvelope.InvalidArgument("max_logs must be zero or greater.", requestId, sw.ElapsedMilliseconds);
            }

            try
            {
                return NexusCommandEnvelope.ProjectCompile(Execute(maxLogs), requestId, sw.ElapsedMilliseconds);
            }
            catch (System.Exception ex)
            {
                return NexusCommandEnvelope.FromException(ex, requestId, sw.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// Groups recent CSxxxx compiler errors by originating file.
        /// </summary>
        /// <param name="maxLogs">Maximum recent console entries to inspect.</param>
        /// <returns>Grouped compiler diagnostics.</returns>
        public static NexusCompileErrorsResult Execute(int maxLogs = DefaultMaxLogs)
        {
            var sw = Stopwatch.StartNew();
            var logs = ReadErrorLogs(maxLogs);
            var groups = new Dictionary<string, List<string>>();
            int totalErrors = 0;
            foreach (var log in logs)
            {
                if (log.Message == null || !log.Message.Contains("error CS")) continue;
                totalErrors++;
                string fileKey = "Unknown";
                int parenIdx = log.Message.IndexOf('(');
                if (parenIdx > 0) fileKey = log.Message.Substring(0, parenIdx).Trim();
                if (!groups.TryGetValue(fileKey, out var list))
                {
                    list = new List<string>();
                    groups[fileKey] = list;
                }
                list.Add(log.Message.Trim());
            }

            var groupResults = groups.Select(g => new NexusCompileErrorGroup
            {
                File = g.Key,
                Count = g.Value.Count,
                Errors = g.Value
            }).ToList();
            sw.Stop();
            return new NexusCompileErrorsResult
            {
                Success = true,
                TotalErrors = totalErrors,
                GroupCount = groupResults.Count,
                IsCompiling = EditorApplication.isCompiling,
                Groups = groupResults,
                ExecutionDurationMs = sw.Elapsed.TotalMilliseconds
            };
        }
    }
}
