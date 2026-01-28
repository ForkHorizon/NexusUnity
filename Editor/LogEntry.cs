using System;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Represents a single log entry captured from the Unity console.
    /// </summary>
    [Serializable]
    public class LogEntry
    {
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
        /// Initializes a new instance of the <see cref="LogEntry"/> class.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="stackTrace">The stack trace.</param>
        /// <param name="type">The type of log.</param>
        public LogEntry(string message, string stackTrace, LogType type)
        {
            this.Message = message;
            this.StackTrace = stackTrace;
            this.Type = type.ToString();
            this.Timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            this.Count = 1;
        }
    }
}
