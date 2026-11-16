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
    /// Pipeline binds one CLI flag per method parameter and the ten capture flags are locked to the HTTP descriptor,
    /// so this file is listed in <c>.code-linter.json</c> ignore. Keep it a thin binding with no logic.
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
            [CliArg("max_logs", GroupCompileErrorsCommand.MaxLogsDescription)] int maxLogs = GroupCompileErrorsCommand.DefaultMaxLogs,
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
            [CliArg("format", CaptureGameViewCommand.FormatDescription)] string format = CaptureGameViewCommand.DefaultFormat,
            [CliArg("quality", CaptureGameViewCommand.QualityDescription)] int quality = CaptureGameViewCommand.DefaultJpegQuality,
            [CliArg("max_dimension", CaptureGameViewCommand.MaxDimensionDescription)] int maxDimension = CaptureGameViewCommand.DefaultMaxLongEdge,
            [CliArg("view", CaptureGameViewCommand.ViewDescription)] string view = CaptureGameViewCommand.DefaultView,
            [CliArg("target", CaptureGameViewCommand.TargetDescription)] string target = null,
            [CliArg("instance_id", CaptureGameViewCommand.InstanceIdDescription)] int instanceId = 0,
            [CliArg("delivery", CaptureGameViewCommand.DeliveryDescription)] string delivery = CaptureGameViewCommand.DefaultDelivery,
            [CliArg("request_id", NexusCommandCodes.RequestIdDescription)] string requestId = null)
        {
            var arguments = new CaptureCliArguments
            {
                Width = width,
                Height = height,
                Format = format,
                Quality = quality,
                MaxDimension = maxDimension,
                View = view,
                Target = target,
                InstanceId = instanceId,
                Delivery = delivery,
                RequestId = requestId
            };
            return CaptureGameViewCommand.ExecuteFromCli(arguments, CancellationToken.None);
        }
    }
}
