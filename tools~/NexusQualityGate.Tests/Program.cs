using NexusQualityGate;

static async Task Run(string name, Func<Task> test)
{
    try
    {
        await test();
        Console.WriteLine("PASS " + name);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("FAIL " + name + ": " + ex.Message);
        Environment.ExitCode = 1;
    }
}

await Run("missing XML docs fail", async () =>
{
    using TempPackage package = TempPackage.Create("""
        namespace Demo;
        public class MissingDocs
        {
            public void Run() { }
        }
        """);

    QualityGateResult result = await QualityGateRunner.RunAsync(package.Options);
    AssertHasError(result, "NQG100");
});

await Run("useful XML docs pass", async () =>
{
    using TempPackage package = TempPackage.Create("""
        namespace Demo;
        /// <summary>
        /// Coordinates a documented editor operation for validation tests.
        /// </summary>
        public class Documented
        {
            /// <summary>
            /// Runs the documented validation command without external side effects.
            /// </summary>
            public void Run() { }
        }
        """);

    QualityGateResult result = await QualityGateRunner.RunAsync(package.Options);
    AssertNoErrors(result);
});

await Run("generic XML docs fail", async () =>
{
    using TempPackage package = TempPackage.Create("""
        namespace Demo;
        /// <summary>
        /// This class does stuff.
        /// </summary>
        public class GenericDocs
        {
        }
        """);

    QualityGateResult result = await QualityGateRunner.RunAsync(package.Options);
    AssertHasError(result, "NQG103");
});

await Run("large method fails", async () =>
{
    string body = string.Join(Environment.NewLine, Enumerable.Range(0, 151).Select(index => $"            int value{index} = {index};"));
    using TempPackage package = TempPackage.Create($$"""
        namespace Demo;
        /// <summary>
        /// Hosts a method that is intentionally too long for validation.
        /// </summary>
        public class LargeMethod
        {
            /// <summary>
            /// Runs a long operation used by the quality gate tests.
            /// </summary>
            public void Run()
            {
        {{body}}
            }
        }
        """);

    QualityGateResult result = await QualityGateRunner.RunAsync(package.Options);
    AssertHasError(result, "NQG011");
});

static void AssertHasError(QualityGateResult result, string code)
{
    if (!result.Issues.Any(issue => issue.IsError && issue.Code == code))
        throw new InvalidOperationException($"Expected error {code}, got: {string.Join(", ", result.Issues.Select(issue => issue.Code))}");
}

static void AssertNoErrors(QualityGateResult result)
{
    if (result.Issues.Any(issue => issue.IsError))
        throw new InvalidOperationException("Unexpected errors: " + string.Join(", ", result.Issues.Where(issue => issue.IsError).Select(issue => issue.Code + " " + issue.Message)));
}

internal sealed class TempPackage : IDisposable
{
    private TempPackage(string root)
    {
        Root = root;
        Options = new QualityGateOptions { Root = root, Format = OutputFormat.None, Ai = AiMode.Off };
    }

    public string Root { get; }
    public QualityGateOptions Options { get; }

    public static TempPackage Create(string csharp)
    {
        string root = Path.Combine(Path.GetTempPath(), "nexus-quality-gate-tests", Guid.NewGuid().ToString("N"));
        string editor = Path.Combine(root, "Editor");
        Directory.CreateDirectory(editor);
        File.WriteAllText(Path.Combine(editor, "Sample.cs"), csharp);
        return new TempPackage(root);
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
            Directory.Delete(Root, true);
    }
}
