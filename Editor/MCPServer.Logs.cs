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

        public static List<LogEntry> GetLogs(int count, string filterType, string searchText)
        {
            try { System.IO.File.AppendAllText("sync_debug.txt", $"[GET_LOGS] Count: {count}\n"); } catch { }
            SyncWithUnityConsole();
            var query = _logs.AsEnumerable();
            if (!string.IsNullOrEmpty(filterType)) query = query.Where(l => l.Type.Equals(filterType, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(searchText)) query = query.Where(l => l.Message.Contains(searchText) || l.StackTrace.Contains(searchText));
            return query.Reverse().Take(count).ToList();
        }

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
                System.IO.File.AppendAllText("sync_debug.txt", $"[SYNC] Unity Console Count: {count}\n");
                
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

        public static void ClearLogs() { while (_logs.TryDequeue(out _)) { } }

        internal static void HandleMainThreadQueue()
        {
            try { System.IO.File.AppendAllText("update_trace.txt", $"[UPDATE] IsPlaying: {EditorApplication.isPlaying}, QueueEmpty: {(_mainThreadQueue?.IsEmpty ?? true)}\n"); } catch {}
            if (_mainThreadQueue == null || _mainThreadQueue.IsEmpty) return;
            Debug.Log("[MCP] MainThreadQueue Processing Heartbeat");

            while (_mainThreadQueue.TryDequeue(out var action))
            {
                try { action?.Invoke(); }
                catch (Exception e) { Debug.LogError($"[MCP] Error executing enqueued action: {e.Message}"); }
            }
        }
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
private static void RuntimeInitLogs()
{
    try { System.IO.File.AppendAllText("mcp_log_capture.txt", $"[{DateTime.Now}] RuntimeInitLogs starting\n"); } catch {}
    Application.logMessageReceivedThreaded -= OnLogMessageReceived;
    Application.logMessageReceivedThreaded += OnLogMessageReceived;

    UnityMCP.Runtime.MCPRuntimeLogger.OnLogReceived -= OnLogMessageReceived;
    UnityMCP.Runtime.MCPRuntimeLogger.OnLogReceived += OnLogMessageReceived;
}

        private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            System.IO.File.AppendAllText("mcp_log_capture.txt", $"[{DateTime.Now}] {type}: {condition}\n");
            long id = Interlocked.Increment(ref _logCounter);
            AddLog(new LogEntry(id, condition, stackTrace, type));
        }

        public static void Enqueue(Action action)
        {
            _mainThreadQueue.Enqueue(action);
        }
    }
}
