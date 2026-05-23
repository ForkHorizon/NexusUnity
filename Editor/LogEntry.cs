using System;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Represents one Unity Console entry captured by Nexus Unity for editor diagnostics and JSON-RPC log queries.
    /// </summary>
    /// <remarks>
    /// Entries are populated from Unity editor and runtime log callbacks, then serialized through Newtonsoft.Json for MCP clients.
    /// Message and StackTrace mirror Unity's console payloads and may be empty or null depending on the callback source.
    /// </remarks>
    [Serializable]
    public class LogEntry
    {
        /// <summary>Unique identifier for the log entry.</summary>
        public long Id;
        /// <summary>The message content of the log.</summary>
        public string Message;
        /// <summary>The stack trace associated with the log.</summary>
        public string StackTrace;
        /// <summary>The type of log (Log, Warning, Error, etc.).</summary>
        public string Type;
        /// <summary>The time the log was captured.</summary>
        public string Timestamp;
        /// <summary>The number of times this specific log has occurred consecutively.</summary>
        public int Count;

        /// <summary>
        /// Initializes a captured Unity log entry, stamps it with the current editor-local time, and starts its duplicate count at one.
        /// </summary>
        /// <param name="id">Monotonic Nexus Unity log identifier assigned by the editor log queue.</param>
        /// <param name="message">Unity log condition/message captured from the Console callback.</param>
        /// <param name="stackTrace">Unity stack trace associated with the message; this can be null or empty.</param>
        /// <param name="type">Unity severity reported by the Console callback.</param>
        public LogEntry(long id, string message, string stackTrace, LogType type)
        {
            this.Id = id;
            this.Message = message;
            this.StackTrace = stackTrace;
            this.Type = type.ToString();
            this.Timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            this.Count = 1;
        }

        /// <summary>
        /// Initializes an empty log entry for Unity and JSON serialization paths that populate public fields after construction.
        /// </summary>
        public LogEntry() { }

        /// <summary>
        /// Copies an existing captured log entry without creating a new timestamp or changing the duplicate count.
        /// </summary>
        /// <param name="other">The captured Unity log entry whose serialized fields should be cloned.</param>
        public LogEntry(LogEntry other)
        {
            this.Id = other.Id;
            this.Message = other.Message;
            this.StackTrace = other.StackTrace;
            this.Type = other.Type;
            this.Timestamp = other.Timestamp;
            this.Count = other.Count;
        }
    }
}
