// Optional Unity Pipeline [CliCommand] wrappers that call the canonical Nexus handlers.
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
    /// so this file is an approved exception to the parameter limit, kept as a CI Scope ignore on the website. Keep it a thin binding with no logic.
    /// Capture returns <see cref="Task"/> so Pipeline can await GPU completion off the Unity main thread.
    /// On Pipeline 1.0.0+ (<c>NEXUS_PIPELINE_1_0_OR_NEWER</c>) commands live in the <c>nexus</c> group
    /// (<c>unity command nexus capture_game_view</c>) and declare <c>CommandEffect.ReadOnly</c>; older Pipelines have
    /// neither, so they keep the global <c>nexus_*</c> names. Flags are kebab-case wire names (<c>max_logs</c> becomes
    /// <c>--max-logs</c>), and capture takes <c>--encoding</c> because Pipeline reserves <c>--format</c>.
    /// </remarks>
    public static class NexusPipelineCommands
    {
        /// <summary>Pipeline 1.0.0+ command group: <c>unity command nexus &lt;name&gt;</c>.</summary>
        public const string Group = "nexus";

        /// <summary>
        /// Pipeline projection of <see cref="ProjectMapCommand"/>: returns the canonical envelope around a curated project map.
        /// Read-only; it reads editor project state and does not modify the project.
        /// </summary>
#if NEXUS_PIPELINE_1_0_OR_NEWER
        [CliCommand(Group, "project_map", ProjectMapCommand.Description, Effect = CommandEffect.ReadOnly, Tags = new[] { "nexus", "context" })]
#else
        [CliCommand(ProjectMapCommand.Alias, ProjectMapCommand.Description, Tags = new[] { "nexus", "context" })]
#endif
        public static NexusCommandEnvelope ProjectMap(
            [CliArg("request-id", NexusCommandCodes.RequestIdDescription)] string requestId = null)
        {
            return ProjectMapCommand.ExecuteEnveloped(requestId);
        }

        /// <summary>
        /// Pipeline projection of <see cref="GroupCompileErrorsCommand"/>: returns the canonical envelope around grouped compiler errors.
        /// Read-only; it reads the Unity console and does not modify the project.
        /// </summary>
#if NEXUS_PIPELINE_1_0_OR_NEWER
        [CliCommand(Group, "group_compile_errors", GroupCompileErrorsCommand.Description, Effect = CommandEffect.ReadOnly, Tags = new[] { "nexus", "diagnostics" })]
#else
        [CliCommand(GroupCompileErrorsCommand.Alias, GroupCompileErrorsCommand.Description, Tags = new[] { "nexus", "diagnostics" })]
#endif
        public static NexusCommandEnvelope GroupCompileErrors(
            [CliArg("max-logs", GroupCompileErrorsCommand.MaxLogsDescription)] int maxLogs = GroupCompileErrorsCommand.DefaultMaxLogs,
            [CliArg("request-id", NexusCommandCodes.RequestIdDescription)] string requestId = null)
        {
            return GroupCompileErrorsCommand.ExecuteEnveloped(maxLogs, requestId);
        }

        /// <summary>
        /// Pipeline projection of <see cref="CaptureGameViewCommand"/>.
        /// Returns a task so Pipeline <c>UnwrapResult</c> awaits GPU completion on a worker thread
        /// while Unity's update loop remains free to finish AsyncGPUReadback. The task result is the canonical envelope.
        /// </summary>
#if NEXUS_PIPELINE_1_0_OR_NEWER
        [CliCommand(Group, "capture_game_view", CaptureGameViewCommand.Description, MainThreadRequired = true, Effect = CommandEffect.ReadOnly, Tags = new[] { "nexus", "capture" })]
#else
        [CliCommand(CaptureGameViewCommand.Alias, CaptureGameViewCommand.Description, MainThreadRequired = true, Tags = new[] { "nexus", "capture" })]
#endif
        public static Task<NexusCommandEnvelope> CaptureGameView(
            [CliArg("width", CaptureGameViewCommand.WidthDescription)] int width = 0,
            [CliArg("height", CaptureGameViewCommand.HeightDescription)] int height = 0,
            [CliArg("encoding", CaptureGameViewCommand.EncodingDescription)] string encoding = CaptureGameViewCommand.DefaultFormat,
            [CliArg("quality", CaptureGameViewCommand.QualityDescription)] int quality = CaptureGameViewCommand.DefaultJpegQuality,
            [CliArg("max-dimension", CaptureGameViewCommand.MaxDimensionDescription)] int maxDimension = CaptureGameViewCommand.DefaultMaxLongEdge,
            [CliArg("view", CaptureGameViewCommand.ViewDescription)] string view = CaptureGameViewCommand.DefaultView,
            [CliArg("target", CaptureGameViewCommand.TargetDescription)] string target = null,
            [CliArg("instance-id", CaptureGameViewCommand.InstanceIdDescription)] int instanceId = 0,
            [CliArg("delivery", CaptureGameViewCommand.DeliveryDescription)] string delivery = CaptureGameViewCommand.DefaultDelivery,
            [CliArg("request-id", NexusCommandCodes.RequestIdDescription)] string requestId = null)
        {
            var arguments = new CaptureCliArguments
            {
                Width = width,
                Height = height,
                Encoding = encoding,
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
