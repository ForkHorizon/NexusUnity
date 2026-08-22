using System;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Groups Nexus Unity service logs by subsystem for custom Unity Console filtering.
    /// </summary>
    /// <remarks>
    /// Categories correspond to server lifecycle, CLI integrations, JSON-RPC API calls, UI automation, diagnostics,
    /// project audit output, and runtime log bridging. The FlagsAttribute allows multiple subsystems to
    /// be enabled together in MCPSettings.EnabledLogCategories.
    /// </remarks>
    [Flags]
    public enum NexusLogCategory
    {
        None = 0,
        Server = 1 << 0,
        Integrations = 1 << 1,
        Api = 1 << 2,
        UiAutomation = 1 << 3,
        Diagnostics = 1 << 4,
        Audit = 1 << 5,
        Runtime = 1 << 6,
        All = Server | Integrations | Api | UiAutomation | Diagnostics | Audit | Runtime
    }
}
