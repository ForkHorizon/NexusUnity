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
                new NexusCommandParameter
                {
                    Name = "max_logs",
                    JsonType = "integer",
                    Description = MaxLogsDescription
                }
            }
        };

        /// <summary>
        /// Groups compiler errors from Unity console logs and returns JSON for Legacy HTTP/MCP.
        /// </summary>
        /// <param name="parameters">Optional <c>max_logs</c> integer.</param>
        /// <param name="cancellationToken">Unused; the command is synchronous.</param>
        /// <returns>Structured compiler-error groups.</returns>
        public Task<JToken> ExecuteAsync(JToken parameters, CancellationToken cancellationToken)
        {
            int maxLogs = parameters?["max_logs"]?.Value<int>() ?? 50;
            return Task.FromResult(JToken.FromObject(Execute(maxLogs)));
        }

        /// <summary>
        /// Groups recent CSxxxx compiler errors by originating file.
        /// </summary>
        /// <param name="maxLogs">Maximum recent console entries to inspect.</param>
        /// <returns>Grouped compiler diagnostics.</returns>
        public static NexusCompileErrorsResult Execute(int maxLogs = 50)
        {
            var sw = Stopwatch.StartNew();
            var logs = MCPServer.GetLogs(maxLogs, "Error", string.Empty);
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
