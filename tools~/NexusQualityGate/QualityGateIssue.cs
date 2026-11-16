namespace NexusQualityGate;

public enum QualityGateSeverity
{
    Warning,
    Error,
}

public sealed record QualityGateIssue
{
    public required QualityGateSeverity Severity { get; init; }
    public required string Code { get; init; }
    public required string Message { get; init; }
    public required string File { get; init; }
    public required int Line { get; init; }
    public string? Symbol { get; init; }

    public bool IsError => Severity == QualityGateSeverity.Error;
}

public sealed record DocumentationCandidate
{
    public required string File { get; init; }
    public required int Line { get; init; }
    public required string Symbol { get; init; }
    public required string Kind { get; init; }
    public required string Signature { get; init; }
    public required string Documentation { get; init; }
    public required string CodeExcerpt { get; init; }

    public string CacheInput => string.Join("\n---\n", File, Line.ToString(), Symbol, Kind, Signature, Documentation, CodeExcerpt);
}

public sealed record ChecklistItem(
    string File,
    int Line,
    string Text)
{
    public string Symbol => "Checklist: " + Text;
}

public sealed class QualityGateResult
{
    public List<QualityGateIssue> Issues { get; } = new();
    public List<DocumentationCandidate> DocumentationCandidates { get; } = new();
    public bool HasFailures => Issues.Any(issue => issue.IsError);

    public void Add(QualityGateIssue issue) => Issues.Add(issue);

    public void AddRange(IEnumerable<QualityGateIssue> issues) => Issues.AddRange(issues);
}
