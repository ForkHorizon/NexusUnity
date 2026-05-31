using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NexusQualityGate;

public sealed class OllamaChecklistReviewer
{
    private const string RubricVersion = "2026-05-27-pr-checklist-review-v1";

    private readonly QualityGateOptions _options;
    private readonly HttpClient _client = new();
    private readonly Dictionary<string, CachedChecklistVerdict> _cache;
    private readonly string _cachePath;
    private bool _contactedOllama;

    public OllamaChecklistReviewer(QualityGateOptions options)
    {
        _options = options;
        _client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.AiTimeoutSeconds));
        Directory.CreateDirectory(options.CacheDirectory);
        _cachePath = Path.Combine(options.CacheDirectory, "ollama-checklist-verdicts.json");
        _cache = LoadCache(_cachePath);
    }

    public async Task<IReadOnlyList<QualityGateIssue>> ReviewAsync()
    {
        if (string.IsNullOrWhiteSpace(_options.Model))
        {
            return _options.ChecklistAi == AiMode.Required
                ? new[] { DocumentationRules.Error("NQG300", "AI checklist review requires NEXUS_DOC_AI_MODEL or --model.", "NexusQualityGate", 1) }
                : Array.Empty<QualityGateIssue>();
        }

        IReadOnlyList<ChecklistItem> items = ReadChecklistItems(_options.Root);
        if (items.Count == 0)
        {
            return _options.ChecklistAi == AiMode.Required
                ? new[] { DocumentationRules.Error("NQG301", "AI checklist review could not find checkbox items in .github/pull_request_template.md.", ".github/pull_request_template.md", 1) }
                : Array.Empty<QualityGateIssue>();
        }

        List<QualityGateIssue> issues = new();
        using SemaphoreSlim semaphore = new(Math.Max(1, _options.MaxAiParallelism));
        List<Task> tasks = new();

        foreach (ChecklistItem item in items)
        {
            string evidence = BuildRepositoryEvidence(_options.Root, item);
            await semaphore.WaitAsync();
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    QualityGateIssue? issue = await ReviewItemAsync(item, evidence);
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

    public static IReadOnlyList<ChecklistItem> ReadChecklistItems(string root)
    {
        string path = Path.Combine(root, ".github", "pull_request_template.md");
        if (!File.Exists(path))
            return Array.Empty<ChecklistItem>();

        string[] lines = File.ReadAllLines(path);
        List<ChecklistItem> items = new();
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index].Trim();
            if (!line.StartsWith("- [ ] ", StringComparison.Ordinal) && !line.StartsWith("- [x] ", StringComparison.OrdinalIgnoreCase))
                continue;

            string text = line[6..].Trim();
            if (text.Length > 0)
                items.Add(new ChecklistItem(".github/pull_request_template.md", index + 1, text));
        }

        return items;
    }

    private async Task<QualityGateIssue?> ReviewItemAsync(ChecklistItem item, string evidence)
    {
        string cacheKey = BuildCacheKey(item, evidence);
        lock (_cache)
        {
            if (_cache.TryGetValue(cacheKey, out CachedChecklistVerdict? cached))
                return ToIssue(item, cached);
        }

        try
        {
            CachedChecklistVerdict verdict = await RequestVerdictAsync(item, evidence);
            lock (_cache)
                _cache[cacheKey] = verdict;
            return ToIssue(item, verdict);
        }
        catch (Exception ex) when (_options.ChecklistAi != AiMode.Required)
        {
            return DocumentationRules.Warning("NQG302", "AI checklist review was unavailable: " + ex.Message, item.File, item.Line, item.Symbol);
        }
        catch (Exception ex)
        {
            return DocumentationRules.Error("NQG302", "AI checklist review failed in required mode: " + ex.Message, item.File, item.Line, item.Symbol);
        }
    }

    private async Task<CachedChecklistVerdict> RequestVerdictAsync(ChecklistItem item, string evidence)
    {
        var payload = new
        {
            model = _options.Model,
            stream = false,
            format = "json",
            keep_alive = _options.AiKeepAlive,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "You are a local CI reviewer for Nexus Unity pull request checklist items. Return one strict JSON object only, with fields pass:boolean, severity:'warning'|'error', reason:string, next_action:string. Do not include markdown or text outside JSON. Review exactly the provided checklist item against the repository evidence. PASS when deterministic CI, local scripts, docs, or tracked files provide reasonable evidence that the item is covered, or when there is no clear contradiction in the candidate package. FAIL only for clear missing coverage, clear unsafe changes, tracked generated/private artifacts, missing public documentation/changelog requirements, or an obvious contradiction. Do not fail merely because runtime CI output is not embedded in the repository; separate CI jobs can supply runtime evidence."
                },
                new
                {
                    role = "user",
                    content = BuildPrompt(item, evidence),
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
            return response.IsSuccessStatusCode ? null : $"{(int)response.StatusCode} {response.ReasonPhrase}: {TrimForLog(responseBody)}";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private static string BuildPrompt(ChecklistItem item, string evidence)
    {
        return $"""
        Checklist item:
        {item.Text}

        Item source:
        {item.File}:{item.Line}

        Repository evidence:
        {evidence}
        """;
    }

    private static string BuildRepositoryEvidence(string root, ChecklistItem item)
    {
        string trackedFiles = RunGit(root, "ls-files");
        string suspicious = BuildSuspiciousTrackedFileSummary(trackedFiles);
        string fileSummary = BuildTrackedFileSummary(trackedFiles);

        var builder = new StringBuilder();
        builder.AppendLine("Tracked file summary:");
        builder.AppendLine(fileSummary);
        builder.AppendLine();
        builder.AppendLine("Suspicious tracked files:");
        builder.AppendLine(suspicious);

        foreach (string path in SelectEvidenceFiles(item.Text))
        {
            builder.AppendLine();
            builder.AppendLine($"--- {path} excerpt ---");
            builder.AppendLine(ReadTargetedExcerpt(Path.Combine(root, path), item.Text, 3500));
        }

        return builder.ToString();
    }

    private static IEnumerable<string> SelectEvidenceFiles(string itemText)
    {
        string text = itemText.ToLowerInvariant();
        var paths = new List<string> { ".github/pull_request_template.md" };

        if (text.Contains("compile", StringComparison.Ordinal) || text.Contains("test", StringComparison.Ordinal) || text.Contains("quality", StringComparison.Ordinal) || text.Contains("python", StringComparison.Ordinal))
        {
            paths.Add(".github/workflows/validate.yml");
            paths.Add("scripts/prepush-validate.sh");
            paths.Add("package.json");
        }

        if (text.Contains("docs", StringComparison.Ordinal) || text.Contains("api", StringComparison.Ordinal))
        {
            paths.Add("README.md");
            paths.Add("DOCUMENTATION.MD");
            paths.Add("API_REFERENCE.MD");
        }

        if (text.Contains("changelog", StringComparison.Ordinal))
            paths.Add("CHANGELOG.md");

        if (text.Contains("loopback", StringComparison.Ordinal) || text.Contains("local-only", StringComparison.Ordinal) || text.Contains("project root", StringComparison.Ordinal) || text.Contains("secret", StringComparison.Ordinal) || text.Contains("credential", StringComparison.Ordinal) || text.Contains("generated", StringComparison.Ordinal))
        {
            paths.Add("SECURITY.md");
            paths.Add("CONTRIBUTING.md");
            paths.Add("scripts/prepush-validate.sh");
            paths.Add("Editor/MCPServer.Networking.cs");
            paths.Add("Editor/MCPServerMethods.Utils.cs");
        }

        if (text.Contains("xml documentation", StringComparison.Ordinal) || text.Contains("public/protected", StringComparison.Ordinal))
        {
            paths.Add("tools~/NexusQualityGate/RoslynQualityScanner.cs");
            paths.Add("tools~/NexusQualityGate/DocumentationRules.cs");
        }

        if (text.Contains("editor tests", StringComparison.Ordinal))
        {
            paths.Add(".github/workflows/validate.yml");
            paths.Add("Tests~/Editor/ConsolidatedManagersTests.cs");
            paths.Add("Tests~/Editor/OpenSourceApiContractTests.cs");
        }

        return paths.Distinct(StringComparer.Ordinal).Take(6);
    }

    private static string BuildTrackedFileSummary(string trackedFiles)
    {
        string[] files = trackedFiles.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var groups = files
            .Select(file => file.Split('/')[0])
            .GroupBy(part => part, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key}: {group.Count()}");

        return $"Total tracked files: {files.Length}\n" + string.Join("\n", groups);
    }

    private static string BuildSuspiciousTrackedFileSummary(string trackedFiles)
    {
        string[] patterns =
        {
            "__pycache__",
            ".pyc",
            ".pyo",
            ".DS_Store",
            "/Library/",
            "/Temp/",
            "/graphify-out/",
            "/.soma/",
            "runtime_trace",
            "input_trace",
            ".env",
            "secret",
            "credential",
            "private",
        };

        string[] suspicious = trackedFiles.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(file => patterns.Any(pattern => file.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            .Take(80)
            .ToArray();

        return suspicious.Length == 0 ? "None detected in git-tracked files." : string.Join("\n", suspicious);
    }

    private static string RunGit(string root, string arguments)
    {
        try
        {
            using var process = new Process();
            process.StartInfo.FileName = "git";
            process.StartInfo.Arguments = arguments;
            process.StartInfo.WorkingDirectory = root;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(5000))
            {
                try { process.Kill(entireProcessTree: true); }
                catch { }
                return string.Empty;
            }
            return process.ExitCode == 0 ? output : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ReadExcerpt(string path, int maxChars)
    {
        if (!File.Exists(path))
            return "File not found.";

        string text = File.ReadAllText(path);
        return text.Length <= maxChars ? text : text[..maxChars] + "\n[truncated]";
    }

    private static string ReadTargetedExcerpt(string path, string itemText, int maxChars)
    {
        if (!File.Exists(path))
            return "File not found.";

        string text = File.ReadAllText(path);
        string[] lines = text.Split('\n');
        string[] terms = BuildSearchTerms(itemText);
        SortedSet<int> selected = new();

        for (int index = 0; index < lines.Length; index++)
        {
            if (!terms.Any(term => lines[index].Contains(term, StringComparison.OrdinalIgnoreCase)))
                continue;

            int start = Math.Max(0, index - 4);
            int end = Math.Min(lines.Length - 1, index + 8);
            for (int selectedIndex = start; selectedIndex <= end; selectedIndex++)
                selected.Add(selectedIndex);
        }

        if (selected.Count == 0)
            return ReadExcerpt(path, maxChars);

        var builder = new StringBuilder();
        int previous = -2;
        foreach (int index in selected)
        {
            if (builder.Length > maxChars)
                break;
            if (previous >= 0 && index > previous + 1)
                builder.AppendLine("[...]");
            builder.AppendLine($"{index + 1}: {lines[index]}");
            previous = index;
        }

        string excerpt = builder.ToString();
        return excerpt.Length <= maxChars ? excerpt : excerpt[..maxChars] + "\n[truncated]";
    }

    private static string[] BuildSearchTerms(string itemText)
    {
        string text = itemText.ToLowerInvariant();
        List<string> terms = new() { "NexusQualityGate", "prepush", "Validate package" };

        if (text.Contains("compile", StringComparison.Ordinal))
            terms.AddRange(new[] { "Unity package smoke", "Import and compile candidate package", "UNITY_EDITOR_PATH", "Package Manager error" });
        if (text.Contains("editor tests", StringComparison.Ordinal) || text.Contains("test", StringComparison.Ordinal))
            terms.AddRange(new[] { "runTests", "EditMode", "Tests~", "NexusUnityPackageTests", "TestResults" });
        if (text.Contains("python", StringComparison.Ordinal))
            terms.AddRange(new[] { "py_compile", "Python bridge", "nexus_bridge" });
        if (text.Contains("docs", StringComparison.Ordinal) || text.Contains("documentation", StringComparison.Ordinal) || text.Contains("public/protected", StringComparison.Ordinal))
            terms.AddRange(new[] { "XML documentation", "DocumentationCandidates", "public/protected", "NQG100", "NQG103" });
        if (text.Contains("changelog", StringComparison.Ordinal))
            terms.Add("CHANGELOG");
        if (text.Contains("loopback", StringComparison.Ordinal) || text.Contains("local-only", StringComparison.Ordinal))
            terms.AddRange(new[] { "loopback", "127.0.0.1", "localhost", "local-only" });
        if (text.Contains("project root", StringComparison.Ordinal))
            terms.AddRange(new[] { "ValidatePath", "project root", "Access denied" });
        if (text.Contains("generated", StringComparison.Ordinal) || text.Contains("cache", StringComparison.Ordinal))
            terms.AddRange(new[] { "__pycache__", ".pyc", ".DS_Store", "generated/local" });
        if (text.Contains("secret", StringComparison.Ordinal) || text.Contains("credential", StringComparison.Ordinal) || text.Contains("private", StringComparison.Ordinal))
            terms.AddRange(new[] { "secret", "credential", "private", "proprietary" });

        return terms.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private Uri BuildOllamaEndpoint() => new(_options.OllamaUrl.TrimEnd('/') + "/api/chat");

    private Uri BuildOllamaGenerateEndpoint() => new(_options.OllamaUrl.TrimEnd('/') + "/api/generate");

    private CachedChecklistVerdict ParseModelVerdict(string content)
    {
        string json = ExtractJsonObject(content);
        AiChecklistVerdict verdict = JsonSerializer.Deserialize<AiChecklistVerdict>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidOperationException("Model returned empty checklist verdict.");

        return new CachedChecklistVerdict(
            verdict.Pass,
            string.IsNullOrWhiteSpace(verdict.Severity) ? "error" : verdict.Severity.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(verdict.Reason) ? "AI model rejected the checklist item without a reason." : verdict.Reason.Trim(),
            verdict.NextAction?.Trim() ?? string.Empty);
    }

    private static string ExtractJsonObject(string content)
    {
        int start = content.IndexOf('{', StringComparison.Ordinal);
        int end = content.LastIndexOf('}');
        if (start < 0 || end <= start)
            throw new InvalidOperationException("Model did not return a JSON object.");

        return content[start..(end + 1)];
    }

    private QualityGateIssue? ToIssue(ChecklistItem item, CachedChecklistVerdict verdict)
    {
        if (verdict.Pass)
            return null;

        QualityGateSeverity severity = verdict.Severity == "warning" && _options.ChecklistAi != AiMode.Required
            ? QualityGateSeverity.Warning
            : QualityGateSeverity.Error;

        string message = verdict.Reason;
        if (!string.IsNullOrWhiteSpace(verdict.NextAction))
            message += " Next action: " + verdict.NextAction;

        return new QualityGateIssue(severity, "NQG310", message, item.File, item.Line, item.Symbol);
    }

    private string BuildCacheKey(ChecklistItem item, string evidence)
    {
        string input = RubricVersion + "\n" + _options.Model + "\n" + item.File + "\n" + item.Line + "\n" + item.Text + "\n" + evidence;
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash);
    }

    private static Dictionary<string, CachedChecklistVerdict> LoadCache(string path)
    {
        if (!File.Exists(path))
            return new Dictionary<string, CachedChecklistVerdict>(StringComparer.Ordinal);

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, CachedChecklistVerdict>>(File.ReadAllText(path))
                ?? new Dictionary<string, CachedChecklistVerdict>(StringComparer.Ordinal);
        }
        catch
        {
            return new Dictionary<string, CachedChecklistVerdict>(StringComparer.Ordinal);
        }
    }

    private static void SaveCache(string path, Dictionary<string, CachedChecklistVerdict> cache)
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

    private sealed record CachedChecklistVerdict(bool Pass, string Severity, string Reason, string NextAction);

    private sealed class AiChecklistVerdict
    {
        public bool Pass { get; set; }
        public string? Severity { get; set; }
        public string? Reason { get; set; }
        [JsonPropertyName("next_action")]
        public string? NextAction { get; set; }
    }
}
