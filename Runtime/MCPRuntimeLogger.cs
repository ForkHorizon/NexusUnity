using UnityEngine;
using System;

namespace UnityMCP.Runtime
{
    /// <summary>
    /// Bridges runtime Unity log messages back to the editor-side Nexus Unity log collector before the first scene loads.
    /// </summary>
    /// <remarks>
    /// Initialization uses <see cref="RuntimeInitializeOnLoadMethodAttribute"/> and subscribes to
    /// <see cref="Application.logMessageReceivedThreaded"/>, so callbacks can arrive from Unity's threaded logging path
    /// and are forwarded through <see cref="OnLogReceived"/> for editor-side collection.
    /// </remarks>
    public static class MCPRuntimeLogger
    {
        public static Action<string, string, LogType> OnLogReceived;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            Application.logMessageReceivedThreaded -= HandleLog;
            Application.logMessageReceivedThreaded += HandleLog;
        }

        private static void HandleLog(string condition, string stackTrace, LogType type)
        {
            OnLogReceived?.Invoke(condition, stackTrace, type);
        }
    }
}
