namespace NexusQualityGate;

public enum QualityGateSeverity
{
    Warning,
    Error,
}

public sealed record QualityGateIssue(
    QualityGateSeverity Severity,
    string Code,
    string Message,
    string File,
    int Line,
    string? Symbol = null)
{
    public bool IsError => Severity == QualityGateSeverity.Error;
}

public sealed record DocumentationCandidate(
    string File,
    int Line,
    string Symbol,
    string Kind,
    string Signature,
    string Documentation,
    string CodeExcerpt)
{
    public string CacheInput => string.Join("\n---\n", File, Line.ToString(), Symbol, Kind, Signature, Documentation, CodeExcerpt);
}

public sealed class QualityGateResult
{
    public List<QualityGateIssue> Issues { get; } = new();
    public List<DocumentationCandidate> DocumentationCandidates { get; } = new();
    public bool HasFailures => Issues.Any(issue => issue.IsError);

    public void Add(QualityGateIssue issue) => Issues.Add(issue);

    public void AddRange(IEnumerable<QualityGateIssue> issues) => Issues.AddRange(issues);
}
