using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Partial implementation of MCPServerWindow handling log capturing.
    /// </summary>
    public partial class MCPServerWindow
    {
        private static ConcurrentQueue<LogEntry> _logs = new ConcurrentQueue<LogEntry>();
        private const int _MAX_LOGS = 1000;

        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            var log = new LogEntry(condition, stackTrace, type);
            _logs.Enqueue(log);
            while (_logs.Count > _MAX_LOGS) _logs.TryDequeue(out _);
        }

        /// <summary>
        /// Retrieves the captured logs with optional filtering.
        /// </summary>
        public static List<LogEntry> GetLogs(int count, string filterType, string searchText)
        {
            var query = _logs.AsEnumerable();
            if (!string.IsNullOrEmpty(filterType)) query = query.Where(l => l.Type.Equals(filterType, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(searchText)) query = query.Where(l => l.Message.Contains(searchText) || l.StackTrace.Contains(searchText));
            return query.Reverse().Take(count).ToList();
        }

        /// <summary>
        /// Clears all captured log entries.
        /// </summary>
        public static void ClearLogs()
        {
            while (_logs.TryDequeue(out _)) { }
        }
    }
}
