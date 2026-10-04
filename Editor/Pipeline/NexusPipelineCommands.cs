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
        /// Pipeline projection of <see cref="ProjectMapCommand"/>: returns the canonical envelope around a curated project map.
        /// Read-only; it reads editor project state and does not modify the project.
        /// </summary>
        [CliCommand(ProjectMapCommand.Alias, ProjectMapCommand.Description, Tags = new[] { "nexus", "context" })]
        public static NexusCommandEnvelope ProjectMap(
            [CliArg("request_id", NexusCommandCodes.RequestIdDescription)] string requestId = null)
        {
            return ProjectMapCommand.ExecuteEnveloped(requestId);
        }

        /// <summary>
        /// Pipeline projection of <see cref="GroupCompileErrorsCommand"/>: returns the canonical envelope around grouped compiler errors.
        /// Read-only; it reads the Unity console and does not modify the project.
        /// </summary>
        [CliCommand(GroupCompileErrorsCommand.Alias, GroupCompileErrorsCommand.Description, Tags = new[] { "nexus", "diagnostics" })]
        public static NexusCommandEnvelope GroupCompileErrors(
            [CliArg("max_logs", GroupCompileErrorsCommand.MaxLogsDescription)] int maxLogs = 50,
            [CliArg("request_id", NexusCommandCodes.RequestIdDescription)] string requestId = null)
        {
            return GroupCompileErrorsCommand.ExecuteEnveloped(maxLogs, requestId);
        }

        /// <summary>
        /// Pipeline projection of <see cref="CaptureGameViewCommand"/>.
        /// Returns a task so Pipeline <c>UnwrapResult</c> awaits GPU completion on a worker thread
        /// while Unity's update loop remains free to finish AsyncGPUReadback. The task result is the canonical envelope.
        /// </summary>
        [CliCommand(CaptureGameViewCommand.Alias, CaptureGameViewCommand.Description, MainThreadRequired = true, Tags = new[] { "nexus", "capture" })]
        public static Task<NexusCommandEnvelope> CaptureGameView(
            [CliArg("width", CaptureGameViewCommand.WidthDescription)] int width = 0,
            [CliArg("height", CaptureGameViewCommand.HeightDescription)] int height = 0,
            [CliArg("format", CaptureGameViewCommand.FormatDescription)] string format = "jpg",
            [CliArg("quality", CaptureGameViewCommand.QualityDescription)] int quality = 85,
            [CliArg("max_dimension", CaptureGameViewCommand.MaxDimensionDescription)] int maxDimension = CaptureGameViewCommand.DefaultMaxLongEdge,
            [CliArg("view", CaptureGameViewCommand.ViewDescription)] string view = "game",
            [CliArg("target", CaptureGameViewCommand.TargetDescription)] string target = null,
            [CliArg("instance_id", CaptureGameViewCommand.InstanceIdDescription)] int instanceId = 0,
            [CliArg("delivery", CaptureGameViewCommand.DeliveryDescription)] string delivery = "base64",
            [CliArg("request_id", NexusCommandCodes.RequestIdDescription)] string requestId = null)
        {
            return CaptureGameViewCommand.ExecuteFromCli(
                width, height, format, quality, maxDimension, view, target, instanceId, delivery, requestId,
                CancellationToken.None);
        }
    }
}
