using System.Text.Json;

namespace NexusQualityGate;

public static class QualityGateReporter
{
    public static void Write(QualityGateResult result, QualityGateOptions options)
    {
        switch (options.Format)
        {
            case OutputFormat.None:
                return;
            case OutputFormat.Json:
                WriteJson(result);
                return;
            case OutputFormat.Github:
                WriteGithub(result);
                return;
            default:
                WriteText(result);
                return;
        }
    }

    private static void WriteText(QualityGateResult result)
    {
        foreach (QualityGateIssue issue in SortIssues(result))
        {
            string severity = issue.IsError ? "ERROR" : "WARN";
            Console.WriteLine($"{severity} {issue.Code} {issue.File}:{issue.Line} {issue.Symbol} - {issue.Message}");
        }

        Console.WriteLine($"Nexus quality gate: {result.Issues.Count(issue => issue.IsError)} error(s), {result.Issues.Count(issue => !issue.IsError)} warning(s).");
    }

    private static void WriteGithub(QualityGateResult result)
    {
        foreach (QualityGateIssue issue in SortIssues(result))
        {
            string command = issue.IsError ? "error" : "warning";
            string title = EscapeGithub($"{issue.Code}{(issue.Symbol is null ? string.Empty : " " + issue.Symbol)}");
            string message = EscapeGithub(issue.Message);
            Console.WriteLine($"::{command} file={issue.File},line={issue.Line},title={title}::{message}");
        }

        Console.WriteLine($"Nexus quality gate: {result.Issues.Count(issue => issue.IsError)} error(s), {result.Issues.Count(issue => !issue.IsError)} warning(s).");
    }

    private static void WriteJson(QualityGateResult result)
    {
        Console.WriteLine(JsonSerializer.Serialize(result.Issues, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static IEnumerable<QualityGateIssue> SortIssues(QualityGateResult result)
    {
        return result.Issues
            .OrderBy(issue => issue.File, StringComparer.Ordinal)
            .ThenBy(issue => issue.Line)
            .ThenBy(issue => issue.Code, StringComparer.Ordinal);
    }

    private static string EscapeGithub(string value)
    {
        return value
            .Replace("%", "%25", StringComparison.Ordinal)
            .Replace("\r", "%0D", StringComparison.Ordinal)
            .Replace("\n", "%0A", StringComparison.Ordinal)
            .Replace(":", "%3A", StringComparison.Ordinal)
            .Replace(",", "%2C", StringComparison.Ordinal);
    }
}
