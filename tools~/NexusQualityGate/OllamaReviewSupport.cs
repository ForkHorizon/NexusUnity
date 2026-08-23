using System.Text.Json;

namespace NexusQualityGate;

internal static class OllamaReviewSupport
{
    public static HttpClient CreateClient(QualityGateOptions options) => new()
    {
        Timeout = TimeSpan.FromSeconds(Math.Max(1, options.AiTimeoutSeconds)),
    };

    public static string GetCachePath(QualityGateOptions options, string fileName)
    {
        Directory.CreateDirectory(options.CacheDirectory);
        return Path.Combine(options.CacheDirectory, fileName);
    }

    public static Uri BuildChatEndpoint(QualityGateOptions options) => new(options.OllamaUrl.TrimEnd('/') + "/api/chat");

    public static Uri BuildGenerateEndpoint(QualityGateOptions options) => new(options.OllamaUrl.TrimEnd('/') + "/api/generate");

    public static string ExtractJsonObject(string content)
    {
        int start = content.IndexOf('{', StringComparison.Ordinal);
        int end = content.LastIndexOf('}');
        if (start < 0 || end <= start)
            throw new InvalidOperationException("Model did not return a JSON object.");

        return content[start..(end + 1)];
    }

    public static Dictionary<string, T> LoadCache<T>(string path)
    {
        if (!File.Exists(path))
            return new Dictionary<string, T>(StringComparer.Ordinal);

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, T>>(File.ReadAllText(path))
                ?? new Dictionary<string, T>(StringComparer.Ordinal);
        }
        catch
        {
            return new Dictionary<string, T>(StringComparer.Ordinal);
        }
    }

    public static void SaveCache<T>(string path, Dictionary<string, T> cache)
    {
        string tempPath = path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tempPath, path, true);
    }

    public static string TrimForLog(string value)
    {
        const int maxLength = 500;
        value = value.ReplaceLineEndings(" ").Trim();
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}
