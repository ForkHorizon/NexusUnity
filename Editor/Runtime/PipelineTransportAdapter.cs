namespace UnityMCP.Editor.Runtime
{
    /// <summary>
    /// Unity Pipeline adapter. Detects optional Pipeline without referencing Unity.Pipeline types.
    /// </summary>
    public sealed class PipelineTransportAdapter
    {
        /// <summary>Gets the adapter name <c>pipeline</c>.</summary>
        public string Name => "pipeline";

        /// <summary>Gets whether the optional Pipeline package/assembly is present.</summary>
        public bool IsAvailable => NexusRuntimeCapabilities.Shared.Current.PipelinePackagePresent;

        /// <summary>Starts an async Pipeline health probe. Does not bind HTTP.</summary>
        public void Start()
        {
            NexusRuntimeCapabilities.Shared.BeginHealthProbe();
        }

        /// <summary>Currently a no-op: the adapter holds no resources, and Pipeline CliCommands unload with the Editor domain.</summary>
        public void Stop()
        {
        }
    }
}
