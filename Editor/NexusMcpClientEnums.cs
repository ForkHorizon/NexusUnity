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
        VsCode,
        Cline,
        RooCode,
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
}
