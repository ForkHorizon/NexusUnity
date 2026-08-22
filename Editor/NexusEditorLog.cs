using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class NexusEditorLog
    {
        internal static bool ShouldWriteToConsole(NexusLogCategory category, bool important, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert || type == LogType.Warning)
            {
                return true;
            }

            switch (MCPSettings.ConsoleLogMode)
            {
                case NexusConsoleLogMode.All:
                    return true;
                case NexusConsoleLogMode.Custom:
                    return (MCPSettings.EnabledLogCategories & category) != 0;
                default:
                    return important;
            }
        }

        internal static void Log(NexusLogCategory category, string message, bool important = false)
        {
            if (ShouldWriteToConsole(category, important, LogType.Log))
            {
                Debug.Log(message);
            }
        }

        internal static void Warning(NexusLogCategory category, string message)
        {
            if (ShouldWriteToConsole(category, true, LogType.Warning))
            {
                Debug.LogWarning(message);
            }
        }

        internal static void Error(NexusLogCategory category, string message)
        {
            if (ShouldWriteToConsole(category, true, LogType.Error))
            {
                Debug.LogError(message);
            }
        }
    }
}
