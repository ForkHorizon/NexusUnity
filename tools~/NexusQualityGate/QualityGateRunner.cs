namespace NexusQualityGate;

public static class QualityGateRunner
{
    public static async Task<QualityGateResult> RunAsync(QualityGateOptions options)
    {
        var scanner = new RoslynQualityScanner(options);
        QualityGateResult result = scanner.Scan();

        if (options.Ai != AiMode.Off && result.DocumentationCandidates.Count > 0)
        {
            var reviewer = new OllamaDocumentationReviewer(options);
            result.AddRange(await reviewer.ReviewAsync(result.DocumentationCandidates));
        }

        return result;
    }
}
