using System;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Controls how much Nexus Unity service logging is written to the Unity Console.
    /// </summary>
    public enum NexusConsoleLogMode
    {
        Important = 0,
        All = 1,
        Custom = 2
    }

    /// <summary>
    /// Groups Nexus Unity service logs so custom Console filtering can include selected subsystems.
    /// </summary>
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
