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
            new System.Text.RegularExpressions.Regex(@"(?:[a-zA-Z]:[\\/]|\\\\[a-zA-Z0-9_.\-]+[\\/])[^\s""'<>]+", System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex _unixPathRegex =
            new System.Text.RegularExpressions.Regex(@"/(?:Users|home|root|var|tmp|etc|private|Applications|Volumes|usr|opt|mnt|media|srv)/[^\s""'<>]+", System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly string _cachedUserProfile = GetUserProfilePath();
        private static string _cachedDataPath;
        private static string _cachedProjectPath;

        private static string GetUserProfilePath()
        {
            try
            {
                string path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return string.IsNullOrEmpty(path) || path.Length <= 1 ? null : path;
            }
            catch
            {
                return null;
            }
        }

        internal static void CacheEnvironmentPaths()
        {
            try
            {
                string dataPath = UnityEngine.Application.dataPath;
                if (!string.IsNullOrEmpty(dataPath))
                {
                    _cachedDataPath = dataPath;
                    _cachedProjectPath = System.IO.Directory.GetParent(dataPath)?.FullName;
                }
            }
            catch
            {
                // Ignore when executed outside Unity runtime/main-thread
            }
        }

        private static string GetDataPath()
        {
            if (!string.IsNullOrEmpty(_cachedDataPath)) return _cachedDataPath;
            if (_isMainThread)
            {
                CacheEnvironmentPaths();
                if (!string.IsNullOrEmpty(_cachedDataPath)) return _cachedDataPath;
            }
            return null;
        }

        private static string GetProjectPath()
        {
            if (!string.IsNullOrEmpty(_cachedProjectPath)) return _cachedProjectPath;
            if (_isMainThread)
            {
                CacheEnvironmentPaths();
                if (!string.IsNullOrEmpty(_cachedProjectPath)) return _cachedProjectPath;
            }
            try
            {
                return System.IO.Directory.GetCurrentDirectory();
            }
            catch
            {
                return null;
            }
        }

        private static Exception UnwrapException(Exception ex)
        {
            Exception current = ex;
            while (current != null)
            {
                if (current is System.Reflection.TargetInvocationException tie && tie.InnerException != null)
                {
                    current = tie.InnerException;
                }
                else if (current is AggregateException ae && ae.InnerExceptions.Count == 1)
                {
                    current = ae.InnerExceptions[0];
                }
                else
                {
                    break;
                }
            }
            return current;
        }

        private static string ReplacePathVariations(string text, string pathToRedact, string replacement)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pathToRedact)) return text;
            text = text.Replace(pathToRedact, replacement);
            string slashNormalized = pathToRedact.Replace('\\', '/');
            if (slashNormalized != pathToRedact)
                text = text.Replace(slashNormalized, replacement);
            string backslashNormalized = pathToRedact.Replace('/', '\\');
            if (backslashNormalized != pathToRedact)
                text = text.Replace(backslashNormalized, replacement);
            return text;
        }

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
        /// <param name="errorType">Outputs the unwrapped exception type name.</param>
        /// <returns>A sanitized single-line summary with sensitive paths redacted.</returns>
        internal static string SanitizeErrorMessage(Exception failure, out string errorType)
        {
            if (failure == null)
            {
                errorType = null;
                return null;
            }

            Exception actual = UnwrapException(failure) ?? failure;
            errorType = actual.GetType().Name;
            string raw = actual.Message ?? string.Empty;

            int newlineIdx = raw.IndexOfAny(new[] { '\r', '\n' });
            string firstLine = newlineIdx >= 0 ? raw.Substring(0, newlineIdx) : raw;
            firstLine = firstLine.Trim();

            if (string.IsNullOrEmpty(firstLine))
            {
                return errorType;
            }

            string projectPath = GetProjectPath();
            if (!string.IsNullOrEmpty(projectPath) && projectPath.Length > 1)
            {
                firstLine = ReplacePathVariations(firstLine, projectPath, "[project]");
            }

            string dataPath = GetDataPath();
            if (!string.IsNullOrEmpty(dataPath) && dataPath.Length > 1)
            {
                firstLine = ReplacePathVariations(firstLine, dataPath, "[project]/Assets");
            }

            if (!string.IsNullOrEmpty(_cachedUserProfile))
            {
                firstLine = ReplacePathVariations(firstLine, _cachedUserProfile, "[user]");
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

            string sanitizedError = null;
            string errorType = null;
            if (failure != null)
            {
                sanitizedError = SanitizeErrorMessage(failure, out errorType);
            }

            DateTime now = DateTime.UtcNow;

            lock (_toolUsageLock)
            {
                if (!_toolUsageStats.TryGetValue(method, out ToolUsageStat stat))
                {
                    stat = new ToolUsageStat();
                    _toolUsageStats[method] = stat;
                }

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
                stat.LastError = sanitizedError;
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
