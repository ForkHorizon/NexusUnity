using UnityEngine;
using System;

namespace UnityMCP.Runtime
{
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
            try { System.IO.File.AppendAllText("runtime_trace.txt", $"[RUNTIME_LOG] {type}: {condition}\n"); } catch {}
            OnLogReceived?.Invoke(condition, stackTrace, type);
        }
    }
}
