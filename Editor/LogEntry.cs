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
        public string message;
        /// <summary>The stack trace associated with the log.</summary>
        public string stackTrace;
        /// <summary>The type of log (Log, Warning, Error, etc.).</summary>
        public string type;
        /// <summary>The time the log was captured.</summary>
        public string timestamp;
        /// <summary>The number of times this specific log has occurred consecutively.</summary>
        public int count;

        /// <summary>
        /// Initializes a new instance of the <see cref="LogEntry"/> class.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="stackTrace">The stack trace.</param>
        /// <param name="type">The type of log.</param>
        public LogEntry(string message, string stackTrace, LogType type)
        {
            this.message = message;
            this.stackTrace = stackTrace;
            this.type = type.ToString();
            this.timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            this.count = 1;
        }
    }
}
