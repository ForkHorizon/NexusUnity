// Output shape for nexus.asset_impact: full lists for small results, a per-asset summary for large ones.
using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor.Commands
{
    /// <summary>
    /// <c>results</c>: <c>full</c> keeps every entry; <c>summary</c> keeps the first <see cref="SampleSize"/> entries per list and adds
    /// <c>&lt;list&gt;_by_asset</c> with every asset, its entry count, evidence lines and distinct values; unique_values keep 3 locations each and the one-line summary is cut at 300 characters; <c>auto</c> (default) is full
    /// up to <see cref="AutoFullLimit"/> entries and summary above.
    /// </summary>
    internal static class AssetImpactResults
    {
        internal const int AutoFullLimit = 50;
        internal const int SampleSize = 10;
        private static readonly string[] Lists = { "values", "bindings", "direct", "via_prefab_instance", "findings" };

        /// <summary>Throws <see cref="ArgumentException"/> for an unknown mode.</summary>
        internal static void Validate(string mode)
        {
            if (mode != "auto" && mode != "summary" && mode != "full") throw new ArgumentException("results must be auto, summary or full.");
        }

        /// <summary>Reshapes <paramref name="target"/> in place and records the shape as <c>results</c>.</summary>
        internal static void Apply(JObject target, string mode)
        {
            int total = Lists.Sum(l => (target[l] as JArray)?.Count ?? 0);
            bool summary = mode == "summary" || (mode == "auto" && total > AutoFullLimit);
            target["results"] = summary ? "summary" : "full";
            if (!summary) return;
            foreach (string list in Lists)
            {
                if (!(target[list] is JArray entries)) continue;
                target[list + "_by_asset"] = new JArray(entries.GroupBy(e => (string)e["asset"]).Select(ByAsset));
                target[list] = new JArray(entries.Take(SampleSize));
            }
            if (target["unique_values"] is JArray unique)
                foreach (JToken u in unique)
                    if (u["at"] is JArray at && at.Count > 3) u["at"] = new JArray(at.Take(3).Concat(new JToken[] { "+" + (at.Count - 3) + " more" }));
            if (target["summary"] is JValue line && line.Type == JTokenType.String && ((string)line).Length > 300)
                target["summary"] = ((string)line).Substring(0, 300) + "… (see values_by_asset)";
            target["hint"] = "results=summary: each list holds its first " + SampleSize + " entries and <list>_by_asset covers every asset with its lines. " +
                             "Pass results=full (narrow it with paths) to get every entry.";
        }

        private static JObject ByAsset(IGrouping<string, JToken> g)
        {
            var row = new JObject { ["asset"] = g.Key, ["count"] = g.Count() };
            var lines = g.Select(e => e["evidence"]?["line"]).Where(l => l != null).ToList();
            if (lines.Count > 0) row["lines"] = new JArray(lines);
            var values = g.Select(e => e["value"]).Where(v => v != null && v.Type != JTokenType.Null).Select(v => v.ToString()).Distinct().ToList();
            if (values.Count > 0) row["values"] = new JArray(values);
            var severities = g.Select(e => (string)e["severity"]).Where(s => s != null).Distinct().ToList();
            if (severities.Count > 0) row["severity"] = new JArray(severities);
            return row;
        }
    }
}
