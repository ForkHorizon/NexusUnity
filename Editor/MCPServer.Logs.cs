using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using System.Threading;

namespace UnityMCP.Editor
{
    public static partial class MCPServer
    {
        internal static void AddLog(LogEntry log)
        {
            _logs.Enqueue(log);
            while (_logs.Count > _MAX_LOGS) _logs.TryDequeue(out _);
        }

        /// <summary>
        /// Synchronizes with the Unity Console and returns the newest captured entries after optional severity and text filtering.
        /// </summary>
        /// <remarks>
        /// This method reflects Unity's internal Console buffer on the editor thread, may inspect recent stack trace strings,
        /// and returns results newest-first from the Nexus Unity in-memory log queue.
        /// </remarks>
        public static List<LogEntry> GetLogs(int count, string filterType, string searchText)
        {
            SyncWithUnityConsole();
            var query = _logs.AsEnumerable();
            if (!string.IsNullOrEmpty(filterType)) query = query.Where(l => l.Type.Equals(filterType, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(searchText)) query = query.Where(l => l.Message.Contains(searchText) || l.StackTrace.Contains(searchText));
            return query.Reverse().Take(count).ToList();
        }

        /// <summary>
        /// Synchronizes with the Unity Console and returns captured entries with Nexus log IDs newer than the supplied cursor.
        /// </summary>
        /// <remarks>
        /// Search text is matched against both messages and stack traces. The cursor is scoped to the current Nexus Unity
        /// in-memory queue, so stale cursors from previous editor sessions may not map to retained entries.
        /// </remarks>
        public static List<LogEntry> GetLogsSince(long cursor, string[] severities, string searchText)
        {
            SyncWithUnityConsole();
            var query = _logs.AsEnumerable().Where(l => l.Id > cursor);

            if (severities != null && severities.Length > 0)
            {
                query = query.Where(l => severities.Any(s => l.Type.Equals(s, StringComparison.OrdinalIgnoreCase)));
            }

            if (!string.IsNullOrEmpty(searchText))
            {
                query = query.Where(l => l.Message.Contains(searchText) || l.StackTrace.Contains(searchText));
            }

            return query.ToList();
        }

        private static void SyncWithUnityConsole()
        {
            try
            {
                var type = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.LogEntries");
                if (type == null) return;

                var getCountMethod = type.GetMethod("GetCount");
                var getEntryMethod = type.GetMethod("GetEntryInternal");

                if (getCountMethod == null || getEntryMethod == null) return;

                int count = (int)getCountMethod.Invoke(null, null);

                var logEntryType = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.LogEntry");
                var entry = Activator.CreateInstance(logEntryType);
                var messageField = logEntryType.GetField("condition");
                var stackField = logEntryType.GetField("stacktrace");
                var typeField = logEntryType.GetField("mode");

                int start = Mathf.Max(0, count - 50);
                for (int i = start; i < count; i++)
                {
                    getEntryMethod.Invoke(null, new object[] { i, entry });
                    string msg = (string)messageField.GetValue(entry);
                    string stack = (string)stackField.GetValue(entry);
                    int mode = (int)typeField.GetValue(entry);

                    LogType logType = LogType.Log;
                    if ((mode & 1) != 0) logType = LogType.Error;
                    else if ((mode & 2) != 0) logType = LogType.Warning;

                    if (!_logs.Any(l => l.Message == msg && l.StackTrace == stack))
                    {
                        long id = Interlocked.Increment(ref _logCounter);
                        AddLog(new LogEntry(id, msg, stack, logType));
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Clears every entry from the Nexus Unity in-memory log queue without clearing Unity's Console window.
        /// </summary>
        /// <remarks>
        /// Downstream MCP log readers lose access to previously queued entries, while the original Unity Console output remains visible.
        /// </remarks>
        public static void ClearLogs() { while (_logs.TryDequeue(out _)) { } }

        internal static void HandleMainThreadQueue()
        {
            RefreshMainThreadCachedState();

            if (_mainThreadQueue == null || _mainThreadQueue.IsEmpty) return;

            while (_mainThreadQueue.TryDequeue(out var action))
            {
                try { action?.Invoke(); }
                catch (Exception e) { NexusEditorLog.Error(NexusLogCategory.Api, $"[MCP] Error executing enqueued action: {e.Message}"); }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RuntimeInitLogs()
        {
            Application.logMessageReceivedThreaded -= OnLogMessageReceived;
            Application.logMessageReceivedThreaded += OnLogMessageReceived;
        }

        private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            long id = Interlocked.Increment(ref _logCounter);
            AddLog(new LogEntry(id, condition, stackTrace, type));
        }

        /// <summary>
        /// Queues editor work for execution by Nexus Unity on the Unity Editor main thread during a later update tick.
        /// </summary>
        /// <remarks>
        /// Use this only for editor operations that must run on Unity's main thread. Background callers should avoid waiting
        /// synchronously on queued work in ways that could deadlock the editor update loop.
        /// </remarks>
        public static void Enqueue(Action action)
        {
            _mainThreadQueue.Enqueue(action);
        }
    }
}
