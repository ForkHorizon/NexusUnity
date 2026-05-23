using UnityEngine;
using System;

namespace UnityMCP.Runtime
{
    /// <summary>
    /// Bridges runtime Unity log messages back to the Editor-side Nexus Unity log collector.
    /// </summary>
    public static class MCPRuntimeLogger
    {
        public static Action<string, string, LogType> OnLogReceived;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            Application.logMessageReceivedThreaded += HandleLog;
        }

        private static void HandleLog(string condition, string stackTrace, LogType type)
        {
            OnLogReceived?.Invoke(condition, stackTrace, type);
        }
    }
}
