namespace UnityMCP.Editor.Runtime
{
    /// <summary>
    /// Requested Nexus transport runtime. <see cref="Auto"/> prefers Pipeline on eligible installs and Legacy otherwise.
    /// </summary>
    public enum NexusRuntimeMode
    {
        /// <summary>Use the Legacy HTTP/MCP loopback server.</summary>
        Legacy = 0,
        /// <summary>Prefer Unity Pipeline / unity mcp when capability checks pass.</summary>
        Pipeline = 1,
        /// <summary>Automatic selection. Prefers Pipeline when the install is eligible.</summary>
        Auto = 2
    }
}
