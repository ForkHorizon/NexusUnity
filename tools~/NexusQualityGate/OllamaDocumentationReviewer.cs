using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NexusQualityGate;

public sealed class OllamaDocumentationReviewer
{
    private readonly QualityGateOptions _options;
    private readonly HttpClient _client = new();
    private readonly Dictionary<string, CachedVerdict> _cache;
    private readonly string _cachePath;
    private bool _contactedOllama;

    public OllamaDocumentationReviewer(QualityGateOptions options)
    {
        _options = options;
        _client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.AiTimeoutSeconds));
        Directory.CreateDirectory(options.CacheDirectory);
        _cachePath = Path.Combine(options.CacheDirectory, "ollama-verdicts.json");
        _cache = LoadCache(_cachePath);
    }

    public async Task<IReadOnlyList<QualityGateIssue>> ReviewAsync(IReadOnlyList<DocumentationCandidate> candidates)
    {
        if (string.IsNullOrWhiteSpace(_options.Model))
        {
            return _options.Ai == AiMode.Required
                ? new[] { DocumentationRules.Error("NQG200", "AI documentation review requires NEXUS_DOC_AI_MODEL or --model.", "NexusQualityGate", 1) }
                : Array.Empty<QualityGateIssue>();
        }

        List<QualityGateIssue> issues = new();
        using SemaphoreSlim semaphore = new(Math.Max(1, _options.MaxAiParallelism));
        List<Task> tasks = new();

        foreach (DocumentationCandidate candidate in candidates)
        {
            await semaphore.WaitAsync();
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    QualityGateIssue? issue = await ReviewCandidateAsync(candidate);
                    if (issue is not null)
                    {
                        lock (issues)
                            issues.Add(issue);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);
        SaveCache(_cachePath, _cache);
        return issues.OrderBy(issue => issue.File, StringComparer.Ordinal).ThenBy(issue => issue.Line).ToList();
    }

    private async Task<QualityGateIssue?> ReviewCandidateAsync(DocumentationCandidate candidate)
    {
        string cacheKey = BuildCacheKey(candidate);
        lock (_cache)
        {
            if (_cache.TryGetValue(cacheKey, out CachedVerdict? cached))
                return ToIssue(candidate, cached);
        }

        try
        {
            CachedVerdict verdict = await RequestVerdictAsync(candidate);
            lock (_cache)
                _cache[cacheKey] = verdict;
            return ToIssue(candidate, verdict);
        }
        catch (Exception ex) when (_options.Ai != AiMode.Required)
        {
            return DocumentationRules.Warning(
                "NQG201",
                "AI documentation review was unavailable: " + ex.Message,
                candidate.File,
                candidate.Line,
                candidate.Symbol);
        }
        catch (Exception ex)
        {
            return DocumentationRules.Error(
                "NQG201",
                "AI documentation review failed in required mode: " + ex.Message,
                candidate.File,
                candidate.Line,
                candidate.Symbol);
        }
    }

    private async Task<CachedVerdict> RequestVerdictAsync(DocumentationCandidate candidate)
    {
        var payload = new
        {
            model = _options.Model,
            stream = false,
            keep_alive = _options.AiKeepAlive,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "You review C# XML documentation for Nexus Unity. Return only strict JSON with fields pass:boolean, severity:'warning'|'error', reason:string, suggested_doc:string. Fail docs that are misleading, generic, too vague, or omit important Unity/editor/filesystem/network side effects visible in code."
                },
                new
                {
                    role = "user",
                    content = BuildPrompt(candidate),
                },
            },
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        _contactedOllama = true;
        using HttpResponseMessage response = await _client.PostAsync(BuildOllamaEndpoint(), content);
        string responseBody = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        using JsonDocument document = JsonDocument.Parse(responseBody);
        string modelContent = document.RootElement.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        return ParseModelVerdict(modelContent);
    }

    public async Task<string?> TryUnloadModelAsync()
    {
        if (!_contactedOllama || !_options.UnloadAiModelOnExit || string.IsNullOrWhiteSpace(_options.Model))
            return null;

        try
        {
            var payload = new
            {
                model = _options.Model,
                prompt = string.Empty,
                stream = false,
                keep_alive = 0,
            };

            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await _client.PostAsync(BuildOllamaGenerateEndpoint(), content);
            string responseBody = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                return $"{(int)response.StatusCode} {response.ReasonPhrase}: {TrimForLog(responseBody)}";

            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private Uri BuildOllamaEndpoint()
    {
        string baseUrl = _options.OllamaUrl.TrimEnd('/');
        return new Uri(baseUrl + "/api/chat");
    }

    private Uri BuildOllamaGenerateEndpoint()
    {
        string baseUrl = _options.OllamaUrl.TrimEnd('/');
        return new Uri(baseUrl + "/api/generate");
    }

    private static string BuildPrompt(DocumentationCandidate candidate)
    {
        return $"""
        File: {candidate.File}:{candidate.Line}
        Symbol: {candidate.Symbol}
        Kind: {candidate.Kind}
        Signature:
        {candidate.Signature}

        XML documentation:
        {candidate.Documentation}

        Code excerpt:
        {candidate.CodeExcerpt}
        """;
    }

    private CachedVerdict ParseModelVerdict(string content)
    {
        string json = ExtractJsonObject(content);
        AiVerdict verdict = JsonSerializer.Deserialize<AiVerdict>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidOperationException("Model returned empty verdict.");

        string severity = string.IsNullOrWhiteSpace(verdict.Severity) ? "error" : verdict.Severity.Trim().ToLowerInvariant();
        bool pass = verdict.Pass;
        string reason = string.IsNullOrWhiteSpace(verdict.Reason) ? "AI model rejected the documentation without a reason." : verdict.Reason.Trim();
        string suggestedDoc = verdict.SuggestedDoc?.Trim() ?? string.Empty;

        return new CachedVerdict(pass, severity, reason, suggestedDoc);
    }

    private static string ExtractJsonObject(string content)
    {
        int start = content.IndexOf('{', StringComparison.Ordinal);
        int end = content.LastIndexOf('}');
        if (start < 0 || end <= start)
            throw new InvalidOperationException("Model did not return a JSON object.");

        return content[start..(end + 1)];
    }

    private QualityGateIssue? ToIssue(DocumentationCandidate candidate, CachedVerdict verdict)
    {
        if (verdict.Pass)
            return null;

        QualityGateSeverity severity = verdict.Severity == "warning" && _options.Ai != AiMode.Required
            ? QualityGateSeverity.Warning
            : QualityGateSeverity.Error;

        string message = verdict.Reason;
        if (!string.IsNullOrWhiteSpace(verdict.SuggestedDoc))
            message += " Suggested doc: " + verdict.SuggestedDoc;

        return new QualityGateIssue(severity, "NQG210", message, candidate.File, candidate.Line, candidate.Symbol);
    }

    private string BuildCacheKey(DocumentationCandidate candidate)
    {
        string input = _options.Model + "\n" + candidate.CacheInput;
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash);
    }

    private static Dictionary<string, CachedVerdict> LoadCache(string path)
    {
        if (!File.Exists(path))
            return new Dictionary<string, CachedVerdict>(StringComparer.Ordinal);

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, CachedVerdict>>(File.ReadAllText(path))
                ?? new Dictionary<string, CachedVerdict>(StringComparer.Ordinal);
        }
        catch
        {
            return new Dictionary<string, CachedVerdict>(StringComparer.Ordinal);
        }
    }

    private static void SaveCache(string path, Dictionary<string, CachedVerdict> cache)
    {
        string tempPath = path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tempPath, path, true);
    }

    private static string TrimForLog(string value)
    {
        const int maxLength = 500;
        value = value.ReplaceLineEndings(" ").Trim();
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }

    private sealed record CachedVerdict(bool Pass, string Severity, string Reason, string SuggestedDoc);

    private sealed class AiVerdict
    {
        public bool Pass { get; set; }
        public string? Severity { get; set; }
        public string? Reason { get; set; }
        [JsonPropertyName("suggested_doc")]
        public string? SuggestedDoc { get; set; }
    }
}
