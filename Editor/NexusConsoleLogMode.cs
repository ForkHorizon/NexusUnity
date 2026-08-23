namespace UnityMCP.Editor
{
    /// <summary>
    /// Controls which Nexus Unity service messages are forwarded to Unity's Console through UnityEngine.Debug.
    /// </summary>
    /// <remarks>
    /// Important writes warnings/errors and explicitly important messages; All writes every Nexus service message;
    /// Custom writes warnings/errors plus info messages whose categories are enabled in MCPSettings.EnabledLogCategories.
    /// </remarks>
    public enum NexusConsoleLogMode
    {
        Important = 0,
        All = 1,
        Custom = 2
    }
}
