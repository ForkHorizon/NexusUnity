namespace UnityMCP.Editor.Commands
{
    /// <summary>
    /// One canonical description of a Nexus-owned command, independent of HTTP or Pipeline transport.
    /// </summary>
    public sealed class NexusCommandDescriptor
    {
        /// <summary>Gets or sets the canonical command id, such as <c>nexus.project_map</c>.</summary>
        public string Id { get; set; }

        /// <summary>Gets or sets transport aliases such as <c>nexus_project_map</c> for Unity CLI / existing HTTP clients.</summary>
        public string[] Aliases { get; set; }

        /// <summary>Gets or sets a short title.</summary>
        public string Title { get; set; }

        /// <summary>Gets or sets the human-readable command description shared by every projection.</summary>
        public string Description { get; set; }

        /// <summary>Gets or sets profile tags such as <c>core</c> or <c>visual</c>.</summary>
        public string[] Profiles { get; set; }

        /// <summary>Gets or sets declared parameters for tool/schema projection.</summary>
        public NexusCommandParameter[] Parameters { get; set; }

        /// <summary>
        /// Gets or sets whether Legacy HTTP should dispatch this command as an async method.
        /// Synchronous commands are marshaled onto the Unity main thread.
        /// </summary>
        public bool DispatchAsync { get; set; }
    }

    /// <summary>
    /// One parameter on a canonical Nexus command.
    /// </summary>
    public sealed class NexusCommandParameter
    {
        /// <summary>Gets or sets the parameter name as it appears on the wire.</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets the JSON schema type, such as <c>integer</c> or <c>string</c>.</summary>
        public string JsonType { get; set; }

        /// <summary>Gets or sets the parameter description.</summary>
        public string Description { get; set; }
    }
}
