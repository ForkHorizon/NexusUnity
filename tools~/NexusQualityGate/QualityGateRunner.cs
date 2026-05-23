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
            try
            {
                result.AddRange(await reviewer.ReviewAsync(result.DocumentationCandidates));
            }
            finally
            {
                string? unloadError = await reviewer.TryUnloadModelAsync();
                if (!string.IsNullOrWhiteSpace(unloadError))
                    Console.Error.WriteLine("WARNING: Failed to unload Ollama model after documentation review: " + unloadError);
            }
        }

        return result;
    }
}
