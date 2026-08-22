using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        private static readonly object _toolUsageLock = new object();
        private static readonly Dictionary<string, ToolUsageStat> _toolUsageStats = new Dictionary<string, ToolUsageStat>();
        private static DateTime _toolUsageStartedUtc = DateTime.UtcNow;

        private static readonly System.Text.RegularExpressions.Regex _windowsPathRegex =
            new System.Text.RegularExpressions.Regex(@"[a-zA-Z]:[\\/][^\s""'<>]+", System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex _unixPathRegex =
            new System.Text.RegularExpressions.Regex(@"/(?:Users|home|root|var|tmp|etc|private|Applications|Volumes)/[^\s""'<>]+", System.Text.RegularExpressions.RegexOptions.Compiled);

        private sealed class ToolUsageStat
        {
            public int Count;
            public int ErrorCount;
            public double TotalDurationMs;
            public double LastDurationMs;
            public DateTime LastCallUtc;
            public DateTime? LastSuccessUtc;
            public DateTime? LastErrorUtc;
            public string LastErrorType;
            public string LastError;
        }

        /// <summary>
        /// Sanitizes raw exception messages before storing them in in-memory tool usage metrics.
        /// </summary>
        /// <param name="failure">The caught exception, or null.</param>
        /// <param name="errorType">Outputs the exception type name.</param>
        /// <returns>A sanitized single-line summary with sensitive paths redacted.</returns>
        internal static string SanitizeErrorMessage(Exception failure, out string errorType)
        {
            if (failure == null)
            {
                errorType = null;
                return null;
            }

            errorType = failure.GetType().Name;
            string raw = failure.Message ?? string.Empty;

            string firstLine = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            firstLine = firstLine.Trim();

            if (string.IsNullOrEmpty(firstLine))
            {
                return errorType;
            }

            try
            {
                string projectPath = UnityEngine.Application.dataPath;
                if (!string.IsNullOrEmpty(projectPath))
                {
                    string parent = System.IO.Directory.GetParent(projectPath)?.FullName;
                    if (!string.IsNullOrEmpty(parent))
                    {
                        firstLine = firstLine.Replace(parent, "[project]");
                        firstLine = firstLine.Replace(parent.Replace('\\', '/'), "[project]");
                        firstLine = firstLine.Replace(parent.Replace('/', '\\'), "[project]");
                    }
                    firstLine = firstLine.Replace(projectPath, "[project]/Assets");
                    firstLine = firstLine.Replace(projectPath.Replace('\\', '/'), "[project]/Assets");
                    firstLine = firstLine.Replace(projectPath.Replace('/', '\\'), "[project]/Assets");
                }
            }
            catch
            {
                // Ignore environment resolution errors in headless or unit test contexts
            }

            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(userProfile) && userProfile.Length > 1)
                {
                    firstLine = firstLine.Replace(userProfile, "[user]");
                    firstLine = firstLine.Replace(userProfile.Replace('\\', '/'), "[user]");
                    firstLine = firstLine.Replace(userProfile.Replace('/', '\\'), "[user]");
                }
            }
            catch
            {
                // Ignore environment resolution errors
            }

            firstLine = _windowsPathRegex.Replace(firstLine, "[path]");
            firstLine = _unixPathRegex.Replace(firstLine, "[path]");

            const int maxLen = 160;
            if (firstLine.Length > maxLen)
            {
                firstLine = firstLine.Substring(0, maxLen - 3) + "...";
            }

            return firstLine;
        }

        private static void RecordToolUsage(string method, double durationMs, Exception failure)
        {
            if (string.IsNullOrEmpty(method)) return;

            lock (_toolUsageLock)
            {
                if (!_toolUsageStats.TryGetValue(method, out ToolUsageStat stat))
                {
                    stat = new ToolUsageStat();
                    _toolUsageStats[method] = stat;
                }

                DateTime now = DateTime.UtcNow;
                stat.Count++;
                stat.TotalDurationMs += durationMs;
                stat.LastDurationMs = durationMs;
                stat.LastCallUtc = now;

                if (failure == null)
                {
                    stat.LastSuccessUtc = now;
                    return;
                }

                stat.ErrorCount++;
                stat.LastErrorUtc = now;
                stat.LastError = SanitizeErrorMessage(failure, out string errorType);
                stat.LastErrorType = errorType;
            }
        }

        private static JToken GetToolUsageStats(JToken p)
        {
            var stats = new JArray();

            lock (_toolUsageLock)
            {
                foreach (var pair in _toolUsageStats.OrderBy(x => x.Key))
                {
                    ToolUsageStat stat = pair.Value;
                    var item = new JObject
                    {
                        ["method"] = pair.Key,
                        ["count"] = stat.Count,
                        ["error_count"] = stat.ErrorCount,
                        ["total_duration_ms"] = Math.Round(stat.TotalDurationMs, 3),
                        ["average_duration_ms"] = stat.Count == 0 ? 0 : Math.Round(stat.TotalDurationMs / stat.Count, 3),
                        ["last_duration_ms"] = Math.Round(stat.LastDurationMs, 3),
                        ["last_call_utc"] = stat.LastCallUtc.ToString("o")
                    };

                    if (stat.LastSuccessUtc.HasValue)
                        item["last_success_utc"] = stat.LastSuccessUtc.Value.ToString("o");
                    if (stat.LastErrorUtc.HasValue)
                        item["last_error_utc"] = stat.LastErrorUtc.Value.ToString("o");
                    if (!string.IsNullOrEmpty(stat.LastErrorType))
                        item["last_error_type"] = stat.LastErrorType;
                    if (!string.IsNullOrEmpty(stat.LastError))
                        item["last_error"] = stat.LastError;

                    stats.Add(item);
                }
            }

            return new JObject
            {
                ["since_utc"] = _toolUsageStartedUtc.ToString("o"),
                ["tools"] = stats
            };
        }

        private static JToken ResetToolUsageStats(JToken p)
        {
            lock (_toolUsageLock)
            {
                _toolUsageStats.Clear();
                _toolUsageStartedUtc = DateTime.UtcNow;
            }

            return new JObject
            {
                ["status"] = "Success",
                ["since_utc"] = _toolUsageStartedUtc.ToString("o")
            };
        }
    }
}
