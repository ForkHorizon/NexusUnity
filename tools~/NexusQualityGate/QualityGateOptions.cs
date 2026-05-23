namespace NexusQualityGate;

public enum AiMode
{
    Off,
    Advisory,
    Required,
}

public enum OutputFormat
{
    Text,
    Github,
    Json,
    None,
}

public sealed class QualityGateOptions
{
    public const string Usage =
        "Usage: NexusQualityGate --root <package-root> [--ai off|advisory|required] [--format text|github|json|none] [--ai-keep-alive <duration>] [--no-ai-unload]";

    public string Root { get; set; } = Directory.GetCurrentDirectory();
    public AiMode Ai { get; set; } = AiMode.Off;
    public OutputFormat Format { get; set; } = OutputFormat.Text;
    public string OllamaUrl { get; set; } = Environment.GetEnvironmentVariable("NEXUS_OLLAMA_URL") ?? "http://127.0.0.1:11434";
    public string? Model { get; set; } = Environment.GetEnvironmentVariable("NEXUS_DOC_AI_MODEL");
    public string AiKeepAlive { get; set; } = Environment.GetEnvironmentVariable("NEXUS_DOC_AI_KEEP_ALIVE") ?? "30s";
    public bool UnloadAiModelOnExit { get; set; } = ParseBool(Environment.GetEnvironmentVariable("NEXUS_DOC_AI_UNLOAD_ON_EXIT"), true);
    public string CacheDirectory { get; set; } = Environment.GetEnvironmentVariable("NEXUS_DOC_AI_CACHE")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "nexus-unity-quality-gate");
    public int MaxAiParallelism { get; set; } = ParsePositiveInt(Environment.GetEnvironmentVariable("NEXUS_DOC_AI_MAX_PARALLEL"), 1);
    public int AiTimeoutSeconds { get; set; } = ParsePositiveInt(Environment.GetEnvironmentVariable("NEXUS_DOC_AI_TIMEOUT_SECONDS"), 120);
    public int FileWarningLines { get; set; } = 300;
    public int FileFailureLines { get; set; } = 450;
    public int MethodWarningLines { get; set; } = 50;
    public int MethodFailureLines { get; set; } = 150;

    public static QualityGateOptions Parse(string[] args)
    {
        var options = new QualityGateOptions();

        for (var index = 0; index < args.Length; index++)
        {
            string arg = args[index];
            string Next()
            {
                if (index + 1 >= args.Length)
                    throw new ArgumentException($"Missing value for {arg}.");
                return args[++index];
            }

            switch (arg)
            {
                case "--root":
                    options.Root = Path.GetFullPath(Next());
                    break;
                case "--ai":
                    options.Ai = ParseAiMode(Next());
                    break;
                case "--format":
                    options.Format = ParseOutputFormat(Next());
                    break;
                case "--ollama-url":
                    options.OllamaUrl = Next();
                    break;
                case "--model":
                    options.Model = Next();
                    break;
                case "--ai-keep-alive":
                    options.AiKeepAlive = Next();
                    break;
                case "--no-ai-unload":
                    options.UnloadAiModelOnExit = false;
                    break;
                case "--cache-dir":
                    options.CacheDirectory = Next();
                    break;
                case "--ai-timeout-seconds":
                    options.AiTimeoutSeconds = ParsePositiveInt(Next(), options.AiTimeoutSeconds);
                    break;
                case "--help":
                case "-h":
                    throw new ArgumentException("Help requested.");
                default:
                    throw new ArgumentException($"Unknown argument: {arg}.");
            }
        }

        if (!Directory.Exists(options.Root))
            throw new ArgumentException($"Package root does not exist: {options.Root}");

        if (options.Ai == AiMode.Required && string.IsNullOrWhiteSpace(options.Model))
            throw new ArgumentException("NEXUS_DOC_AI_MODEL or --model is required when --ai required is used.");

        return options;
    }

    private static AiMode ParseAiMode(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "off" => AiMode.Off,
            "advisory" => AiMode.Advisory,
            "required" => AiMode.Required,
            _ => throw new ArgumentException($"Invalid AI mode: {value}. Use off, advisory, or required."),
        };
    }

    private static OutputFormat ParseOutputFormat(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "text" => OutputFormat.Text,
            "github" => OutputFormat.Github,
            "json" => OutputFormat.Json,
            "none" => OutputFormat.None,
            _ => throw new ArgumentException($"Invalid output format: {value}. Use text, github, json, or none."),
        };
    }

    private static int ParsePositiveInt(string? value, int fallback)
    {
        return int.TryParse(value, out int parsed) && parsed > 0 ? parsed : fallback;
    }

    private static bool ParseBool(string? value, bool fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return value.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "on" => true,
            "0" or "false" or "no" or "off" => false,
            _ => fallback,
        };
    }
}
