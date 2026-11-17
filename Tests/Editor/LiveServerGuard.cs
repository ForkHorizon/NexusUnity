// Restores the live Nexus server after EditMode tests that stop or restart it.
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    /// <summary>
    /// Some tests stop the running server, and <see cref="MCPServer.Stop"/> also clears the saved auto-start setting.
    /// Without this guard a test run leaves the editor with no server until someone clicks Start Server again.
    /// </summary>
    [SetUpFixture]
    public sealed class LiveServerGuard
    {
        private bool _wasRunning;
        private int _port;

        [OneTimeSetUp]
        public void RememberServer()
        {
            _wasRunning = MCPServer.IsRunning;
            _port = MCPSettings.Port;
        }

        [OneTimeTearDown]
        public void RestoreServer()
        {
            MCPSettings.Port = _port;
            if (_wasRunning && !MCPServer.IsRunning) MCPServer.Start();
        }
    }
}
