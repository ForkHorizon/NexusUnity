namespace UnityMCP.Editor.Runtime
{
    /// <summary>
    /// Immutable Pipeline capability snapshot for one Editor session generation.
    /// </summary>
    public sealed class RuntimeCapabilitySnapshot
    {
        /// <summary>Gets the Editor session generation this snapshot was captured for.</summary>
        public int SessionGeneration { get; set; }

        /// <summary>Gets whether the <c>com.unity.pipeline</c> package is registered.</summary>
        public bool PipelinePackagePresent { get; set; }

        /// <summary>Gets whether a Pipeline session/port file was found for this project.</summary>
        public bool PipelineSessionFilePresent { get; set; }

        /// <summary>Gets the Pipeline loopback port when known.</summary>
        public int? PipelinePort { get; set; }

        /// <summary>Gets the Pipeline editor process id when known.</summary>
        public int? PipelinePid { get; set; }

        /// <summary>Gets whether the async health probe succeeded.</summary>
        public bool PipelineHealthy { get; set; }

        /// <summary>Gets whether health has not been proven yet.</summary>
        public bool HealthUnknown { get; set; } = true;

        /// <summary>Gets whether the running Unity version is in the supported 6000.x line.</summary>
        public bool UnityVersionSupported { get; set; }

        /// <summary>Gets whether the three canonical Nexus commands are registered for Pipeline projection.</summary>
        public bool NexusCommandsRegistered { get; set; }

        /// <summary>Gets the installed <c>com.unity.pipeline</c> version when known.</summary>
        public string PipelinePackageVersion { get; set; }

        /// <summary>Gets whether the installed Pipeline package is experimental or preview.</summary>
        public bool PipelineExperimental { get; set; }

        /// <summary>Gets a short diagnostic detail string.</summary>
        public string Detail { get; set; }
    }
}
