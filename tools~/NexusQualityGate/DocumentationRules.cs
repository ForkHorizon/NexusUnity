using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NexusQualityGate;

public static class DocumentationRules
{
    private static readonly Regex SummaryRegex = new(
        @"<summary>\s*(.*?)\s*</summary>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly string[] WeakPhrases =
    {
        "does the thing",
        "does stuff",
        "handles the logic",
        "main method",
        "helper method",
        "utility method",
        "this method",
        "this class",
        "todo",
        "tbd",
        "fixme",
    };

    private static readonly string[] DangerousCodeMarkers =
    {
        "File.",
        "Directory.",
        "AssetDatabase.",
        "EditorSceneManager.",
        "Process.Start",
        "HttpListener",
        "TcpListener",
        "SessionState.",
        "EditorPrefs.",
        "PlayerPrefs.",
        "CreateAsset",
        "DeleteAsset",
        "ImportAsset",
        "Refresh(",
        "Start()",
        "Stop()",
    };

    private static readonly string[] SideEffectWords =
    {
        "asset",
        "disk",
        "editor",
        "file",
        "http",
        "network",
        "port",
        "process",
        "refresh",
        "scene",
        "server",
        "settings",
        "state",
        "unity",
    };

    public static string ExtractXmlDocumentation(MemberDeclarationSyntax declaration)
    {
        return string.Concat(declaration.GetLeadingTrivia()
            .Where(trivia => (SyntaxKind)trivia.RawKind == SyntaxKind.SingleLineDocumentationCommentTrivia
                || (SyntaxKind)trivia.RawKind == SyntaxKind.MultiLineDocumentationCommentTrivia)
            .Select(trivia => trivia.ToFullString()));
    }

    public static string ExtractSummary(string xml)
    {
        Match match = SummaryRegex.Match(xml);
        if (!match.Success)
            return string.Empty;

        return Regex.Replace(match.Groups[1].Value, @"\s+", " ")
            .Replace("///", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    public static QualityGateIssue? ValidateSummary(string file, int line, string symbol, string xml)
    {
        string summary = ExtractSummary(xml);
        if (string.IsNullOrWhiteSpace(summary))
        {
            return Error("NQG101", "XML documentation must include a <summary> block.", file, line, symbol);
        }

        if (summary.Length < 18)
        {
            return Error("NQG102", "XML documentation summary is too short to explain the contract.", file, line, symbol);
        }

        string lower = summary.ToLowerInvariant();
        if (WeakPhrases.Any(phrase => lower.Contains(phrase, StringComparison.Ordinal)))
        {
            return Error("NQG103", "XML documentation summary is generic filler rather than useful contract documentation.", file, line, symbol);
        }

        string symbolWords = Regex.Replace(symbol, @"[^A-Za-z0-9]+", " ").Trim().ToLowerInvariant();
        string summaryWords = Regex.Replace(lower, @"[^a-z0-9]+", " ").Trim();
        if (summaryWords == symbolWords)
        {
            return Error("NQG104", "XML documentation summary only repeats the symbol name.", file, line, symbol);
        }

        return null;
    }

    public static QualityGateIssue? ValidateSideEffectDocumentation(string file, int line, string symbol, string xml, string code)
    {
        if (!DangerousCodeMarkers.Any(marker => code.Contains(marker, StringComparison.Ordinal)))
            return null;

        string lowerDoc = xml.ToLowerInvariant();
        if (SideEffectWords.Any(word => lowerDoc.Contains(word, StringComparison.Ordinal)))
            return null;

        return Warning(
            "NQG120",
            "Documentation should mention Unity, filesystem, process, network, or editor-state side effects visible in the implementation.",
            file,
            line,
            symbol);
    }

    public static QualityGateIssue Error(string code, string message, string file, int line, string? symbol = null)
    {
        return new QualityGateIssue(QualityGateSeverity.Error, code, message, file, line, symbol);
    }

    public static QualityGateIssue Warning(string code, string message, string file, int line, string? symbol = null)
    {
        return new QualityGateIssue(QualityGateSeverity.Warning, code, message, file, line, symbol);
    }
}
