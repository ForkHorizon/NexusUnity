namespace NexusQualityGate;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = QualityGateOptions.Parse(args);
            var result = await QualityGateRunner.RunAsync(options);
            QualityGateReporter.Write(result, options);
            return result.HasFailures ? 1 : 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine("ERROR: " + ex.Message);
            Console.Error.WriteLine(QualityGateOptions.Usage);
            return 2;
        }
    }
}
