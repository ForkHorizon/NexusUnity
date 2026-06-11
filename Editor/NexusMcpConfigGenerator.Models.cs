namespace UnityMCP.Editor
{
    internal enum NexusMcpClientKind
    {
        Codex,
        ClaudeDesktop,
        ClaudeCode,
        Gemini,
        Antigravity,
        Cursor,
        VsCodeClineRoo,
        Windsurf,
        GenericJson
    }

    internal enum NexusMcpClientStatus
    {
        Detected,
        NotFound,
        Configured,
        Outdated,
        NeedsRestart,
        Error
    }

    internal enum NexusMcpConfigFormat
    {
        CodexToml,
        JsonMcpServers,
        JsonServers,
        ManualOnly,
        CliManaged
    }

    internal sealed class NexusMcpClientInfo
    {
        internal NexusMcpClientKind Kind { get; set; }
        internal NexusMcpClientStatus Status { get; set; }
        internal NexusMcpConfigFormat Format { get; set; }
        internal string ElementKey { get; set; }
        internal string DisplayName { get; set; }
        internal string Instruction { get; set; }
        internal string StatusDetail { get; set; }
        internal string ConfigPath { get; set; }
        internal string ConfigText { get; set; }
        internal string BridgePath { get; set; }
        internal string SourceBridgePath { get; set; }
        internal string SourceBridgeVersion { get; set; }
        internal string DeployedBridgeVersion { get; set; }
        internal string ConfiguredBridgePath { get; set; }
        internal string PythonPath { get; set; }
        internal bool SupportsAutoSetup { get; set; }
        internal string AutoSetupDisabledReason { get; set; }
        internal string RootKey { get; set; }
    }

    internal sealed class NexusMcpSetupResult
    {
        internal bool Success { get; set; }
        internal string Message { get; set; }
        internal string ConfigPath { get; set; }
        internal string BackupPath { get; set; }
    }
}
