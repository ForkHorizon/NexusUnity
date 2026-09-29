namespace UnityMCP.Editor.Runtime
{
    /// <summary>
    /// Legacy HTTP/MCP adapter. Owns loopback HttpListener lifecycle without containing command logic.
    /// </summary>
    public sealed class LegacyTransportAdapter
    {
        /// <summary>Gets the adapter name <c>legacy</c>.</summary>
        public string Name => "legacy";

        /// <summary>Gets whether Legacy HTTP/MCP can be used. Always true.</summary>
        public bool IsAvailable => true;

        /// <summary>Starts the Legacy loopback HTTP/WebSocket listener.</summary>
        public void Start()
        {
            MCPServer.Start();
        }

        /// <summary>Stops the Legacy loopback HTTP/WebSocket listener.</summary>
        public void Stop()
        {
            MCPServer.Stop();
        }
    }
}
