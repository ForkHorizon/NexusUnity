using System.Threading;
using System.Threading.Tasks;
using Unity.Pipeline.Commands;
using UnityMCP.Editor.Commands;

namespace UnityMCP.Editor.Pipeline
{
    /// <summary>
    /// Optional Unity Pipeline <c>[CliCommand]</c> wrappers. Each method calls the canonical Nexus handler.
    /// </summary>
    /// <remarks>
    /// This assembly is excluded when <c>com.unity.pipeline</c> is not installed. It must not contain Nexus business logic.
    /// Capture returns <see cref="Task"/> so Pipeline can await GPU completion off the Unity main thread.
    /// </remarks>
    public static class NexusPipelineCommands
    {
        /// <summary>
        /// Pipeline projection of <see cref="ProjectMapCommand"/>: returns a curated project intelligence map (build scenes,
        /// color space, render pipeline) as a structured result. Read-only; it does not modify the project.
        /// </summary>
        [CliCommand(ProjectMapCommand.Alias, ProjectMapCommand.Description, Tags = new[] { "nexus", "context" })]
        public static NexusProjectMapResult ProjectMap()
        {
            return ProjectMapCommand.Execute();
        }

        /// <summary>
        /// Pipeline projection of <see cref="GroupCompileErrorsCommand"/>: extracts C# compiler errors (CSxxxx) from recent console logs
        /// and groups them by originating file. Read-only; <c>max_logs</c> caps how many console entries are inspected.
        /// </summary>
        [CliCommand(GroupCompileErrorsCommand.Alias, GroupCompileErrorsCommand.Description, Tags = new[] { "nexus", "diagnostics" })]
        public static NexusCompileErrorsResult GroupCompileErrors(
            [CliArg("max_logs", GroupCompileErrorsCommand.MaxLogsDescription)] int maxLogs = 50)
        {
            return GroupCompileErrorsCommand.Execute(maxLogs);
        }

        /// <summary>
        /// Pipeline projection of <see cref="CaptureGameViewCommand"/>.
        /// Returns a task so Pipeline <c>UnwrapResult</c> awaits GPU completion on a worker thread
        /// while Unity's update loop remains free to finish AsyncGPUReadback.
        /// </summary>
        [CliCommand(CaptureGameViewCommand.Alias, CaptureGameViewCommand.Description, MainThreadRequired = true, Tags = new[] { "nexus", "capture" })]
        public static Task<NexusCaptureResult> CaptureGameView(
            [CliArg("width", CaptureGameViewCommand.WidthDescription)] int width = 0,
            [CliArg("height", CaptureGameViewCommand.HeightDescription)] int height = 0,
            [CliArg("format", CaptureGameViewCommand.FormatDescription)] string format = "jpg",
            [CliArg("quality", CaptureGameViewCommand.QualityDescription)] int quality = 85,
            [CliArg("max_dimension", CaptureGameViewCommand.MaxDimensionDescription)] int maxDimension = 0)
        {
            return CaptureGameViewCommand.ExecuteAsync(
                width, height, format, quality, maxDimension, CancellationToken.None);
        }
    }
}
