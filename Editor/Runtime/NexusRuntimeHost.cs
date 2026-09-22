using System.Threading;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor.Runtime
{
    /// <summary>
    /// Coordinates transport adapters and runtime selection for the current Editor generation.
    /// Main thread publishes an immutable status snapshot; listener threads only read that snapshot.
    /// </summary>
    public static class NexusRuntimeHost
    {
        private static readonly object PublishLock = new object();
        private static JObject _publishedStatus;

        /// <summary>Legacy HTTP/MCP adapter instance.</summary>
        public static LegacyTransportAdapter Legacy { get; } = new LegacyTransportAdapter();

        /// <summary>Optional Pipeline adapter instance.</summary>
        public static PipelineTransportAdapter Pipeline { get; } =
            new PipelineTransportAdapter();

        /// <summary>
        /// Starts capability probing without blocking Unity initialization and publishes runtime status.
        /// </summary>
        public static void OnEditorInit()
        {
            Pipeline.Start();
            PublishSnapshot();
        }

        /// <summary>
        /// Rebuilds the runtime status object on the Unity main thread for all consumers, including the HTTP listener.
        /// </summary>
        public static void PublishSnapshot()
        {
            JObject json = BuildStatusJson();
            lock (PublishLock) _publishedStatus = json;
        }

        /// <summary>Gets the user-requested runtime mode.</summary>
        public static NexusRuntimeMode RequestedMode => MCPSettings.RuntimeMode;

        /// <summary>Gets the effective runtime after M4 selection policy.</summary>
        public static NexusRuntimeMode EffectiveMode =>
            NexusRuntimeSelector.Effective(RequestedMode, NexusRuntimeCapabilities.Shared.Current);

        /// <summary>
        /// True when a busy Legacy HTTP port should not fail Nexus because Pipeline is the selected runtime.
        /// </summary>
        public static bool CanSkipLegacyHttpBind()
        {
            return NexusRuntimeSelector.CanSkipLegacyHttpBind(
                RequestedMode, NexusRuntimeCapabilities.Shared.Current);
        }

        /// <summary>
        /// Returns the last main-thread runtime snapshot. Rebuilds immediately when called on the Unity main thread.
        /// </summary>
        public static JObject ToStatusJson()
        {
            if (IsMainThread()) PublishSnapshot();
            lock (PublishLock)
            {
                if (_publishedStatus != null)
                {
                    JObject snapshot = (JObject)_publishedStatus.DeepClone();
                    snapshot["legacy_http_bound"] = MCPServer.IsRunning;
                    snapshot["legacy_unavailable_reason"] = MCPServer.ForeignProjectOwnsLegacyPort
                        ? "foreign_project"
                        : (string)null;
                    return snapshot;
                }
            }

            return BuildStatusJson();
        }

        private static bool IsMainThread()
        {
            return MCPServer.MainThreadId != -1 &&
                Thread.CurrentThread.ManagedThreadId == MCPServer.MainThreadId;
        }

        private static JObject BuildStatusJson()
        {
            RuntimeCapabilitySnapshot snap = NexusRuntimeCapabilities.Shared.Current;
            NexusRuntimeMode requested = RequestedMode;
            NexusRuntimeMode effective = NexusRuntimeSelector.Effective(requested, snap);
            JObject legacy = NexusLegacyDeprecation.ToStatusJson(snap);
            return new JObject
            {
                ["requested"] = NexusRuntimeSelector.ToWireName(requested),
                ["effective"] = NexusRuntimeSelector.ToWireName(effective),
                ["eligible"] = NexusRuntimeSelector.IsEligible(snap),
                ["legacy_http_bound"] = MCPServer.IsRunning,
                ["legacy_fallback"] = requested != NexusRuntimeMode.Pipeline,
                ["legacy_unavailable_reason"] = MCPServer.ForeignProjectOwnsLegacyPort
                    ? "foreign_project"
                    : (string)null,
                ["legacy"] = legacy,
                ["unity_cli"] = legacy["unity_cli"],
                ["pipeline"] = new JObject
                {
                    ["detected"] = snap.PipelinePackagePresent && snap.PipelineSessionFilePresent,
                    ["version"] = snap.PipelinePackageVersion,
                    ["experimental"] = snap.PipelineExperimental,
                    ["supported"] = snap.UnityVersionSupported && snap.PipelinePackagePresent &&
                        snap.NexusCommandsRegistered,
                    ["package_present"] = snap.PipelinePackagePresent,
                    ["package_version"] = snap.PipelinePackageVersion,
                    ["session_file_present"] = snap.PipelineSessionFilePresent,
                    ["port"] = snap.PipelinePort,
                    ["pid"] = snap.PipelinePid,
                    ["healthy"] = snap.PipelineHealthy,
                    ["health_unknown"] = snap.HealthUnknown,
                    ["unity_version_supported"] = snap.UnityVersionSupported,
                    ["commands_registered"] = snap.NexusCommandsRegistered,
                    ["detail"] = snap.Detail,
                    ["adapter_available"] = Pipeline.IsAvailable
                }
            };
        }
    }
}
