using System;
using UnityEngine;

namespace UnityMCP.Editor
{
    [Serializable]
    public class LogEntry
    {
        public string message;
        public string stackTrace;
        public string type;
        public string timestamp;
        public int count;

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
