using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityMCP.Editor
{
    public static partial class MCPServer
    {
        internal static void AddLog(LogEntry log)
        {
            _logs.Enqueue(log);
            while (_logs.Count > _MAX_LOGS) _logs.TryDequeue(out _);
        }

        public static List<LogEntry> GetLogs(int count, string filterType, string searchText)
        {
            var query = _logs.AsEnumerable();
            if (!string.IsNullOrEmpty(filterType)) query = query.Where(l => l.Type.Equals(filterType, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(searchText)) query = query.Where(l => l.Message.Contains(searchText) || l.StackTrace.Contains(searchText));
            return query.Reverse().Take(count).ToList();
        }

        public static void ClearLogs() { while (_logs.TryDequeue(out _)) { } }

        internal static void HandleMainThreadQueue()
        {
            if (_mainThreadQueue == null || _mainThreadQueue.IsEmpty) return;

            while (_mainThreadQueue.TryDequeue(out var action))
            {
                try { action?.Invoke(); }
                catch (Exception e) { Debug.LogError($"[MCP] Error executing enqueued action: {e.Message}"); }
            }
        }

        private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            AddLog(new LogEntry(condition, stackTrace, type));
        }

        public static void Enqueue(Action action)
        {
            _mainThreadQueue.Enqueue(action);
        }
    }
}