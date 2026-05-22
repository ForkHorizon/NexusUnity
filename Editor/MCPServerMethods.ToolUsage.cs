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
        private static readonly DateTime _toolUsageStartedUtc = DateTime.UtcNow;

        private sealed class ToolUsageStat
        {
            public int Count;
            public int ErrorCount;
            public double TotalDurationMs;
            public double LastDurationMs;
            public DateTime LastCallUtc;
            public DateTime? LastSuccessUtc;
            public DateTime? LastErrorUtc;
            public string LastError;
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
                stat.LastError = failure.Message;
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
    }
}
