namespace UnityMCP.Editor.Runtime
{
    /// <summary>
    /// Chooses the effective transport (Legacy HTTP or Pipeline) from the requested mode and the current capability snapshot.
    /// Explicit Legacy and Pipeline are honoured as is; Auto uses Pipeline only when the install is eligible, otherwise Legacy.
    /// </summary>
    public static class NexusRuntimeSelector
    {
        /// <summary>
        /// Returns the wire name for a runtime mode.
        /// </summary>
        public static string ToWireName(NexusRuntimeMode mode)
        {
            switch (mode)
            {
                case NexusRuntimeMode.Pipeline: return "pipeline";
                case NexusRuntimeMode.Auto: return "auto";
                default: return "legacy";
            }
        }

        /// <summary>
        /// Parses a runtime mode string. Unknown values become Legacy.
        /// </summary>
        public static NexusRuntimeMode Parse(string value)
        {
            if (string.IsNullOrEmpty(value)) return NexusRuntimeMode.Legacy;
            if (value.Equals("pipeline", System.StringComparison.OrdinalIgnoreCase)) return NexusRuntimeMode.Pipeline;
            if (value.Equals("auto", System.StringComparison.OrdinalIgnoreCase)) return NexusRuntimeMode.Auto;
            return NexusRuntimeMode.Legacy;
        }

        /// <summary>
        /// True when this Editor can use Pipeline as primary: Unity 6000, package present,
        /// Nexus commands registered, session port file, and a healthy loopback probe.
        /// </summary>
        public static bool IsEligible(RuntimeCapabilitySnapshot snapshot)
        {
            if (snapshot == null || snapshot.HealthUnknown) return false;
            return snapshot.UnityVersionSupported
                && snapshot.PipelinePackagePresent
                && snapshot.NexusCommandsRegistered
                && snapshot.PipelineSessionFilePresent
                && snapshot.PipelineHealthy;
        }

        /// <summary>
        /// Resolves the effective runtime. Explicit Legacy always wins.
        /// Auto uses Pipeline only when eligible, otherwise Legacy.
        /// Explicit Pipeline stays Pipeline even when unhealthy so callers cannot silently fall back to HTTP.
        /// </summary>
        public static NexusRuntimeMode Effective(NexusRuntimeMode requested, RuntimeCapabilitySnapshot snapshot)
        {
            if (requested == NexusRuntimeMode.Legacy) return NexusRuntimeMode.Legacy;
            if (requested == NexusRuntimeMode.Pipeline) return NexusRuntimeMode.Pipeline;
            if (IsEligible(snapshot)) return NexusRuntimeMode.Pipeline;
            return NexusRuntimeMode.Legacy;
        }

        /// <summary>
        /// True when Pipeline is the selected runtime (explicit pipeline, or auto with a healthy Pipeline).
        /// Legacy HTTP is still bound opportunistically; this only means a busy/foreign-owned port is not a startup error.
        /// </summary>
        public static bool CanSkipLegacyHttpBind(NexusRuntimeMode requested, RuntimeCapabilitySnapshot snapshot)
        {
            if (requested == NexusRuntimeMode.Pipeline) return true;
            return Effective(requested, snapshot) == NexusRuntimeMode.Pipeline;
        }
    }
}
