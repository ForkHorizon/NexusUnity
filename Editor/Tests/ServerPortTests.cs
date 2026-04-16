using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine.TestTools;

namespace UnityMCP.Editor.Tests
{
    /// <summary>
    /// Tests for the MCPServer port management and state transitions.
    /// </summary>
    public class ServerPortTests
    {
        private const int TEST_PORT = 9999;

        [TearDown]
        public void Cleanup()
        {
            // Reset state if we touched it, but only if it's not the default port server
            // to avoid stopping the main session.
            if (MCPServer.State != ServerState.Stopped && MCPServer.Port != 8081)
            {
                MCPServer.Stop();
            }
        }

        [Test]
        public void IsPortBusy_DetectsLocalListener()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, TEST_PORT);
            try
            {
                listener.Start();
                
                // The new logic should catch it via TcpClient connect.
                bool isBusy = (bool)InvokePrivateMethod("IsPortBusy", TEST_PORT);
                Assert.IsTrue(isBusy, "IsPortBusy should detect the active TcpListener on Loopback");
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public void GetPortOwner_IdentifiesProcess()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, TEST_PORT);
            try
            {
                listener.Start();
                string owner = (string)InvokePrivateMethod("GetPortOwner", TEST_PORT);
                
                // Should contain "Unity" on Mac/Windows if running from Editor
                Assert.IsFalse(string.IsNullOrEmpty(owner), "GetPortOwner should not return empty string");
                Assert.AreNotEqual("Unknown Process", owner, "Should identify the process");
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public async Task Start_WithPort0_AssignsDynamicPort()
        {
            // We expect some background noise from Init() auto-starts in some environments
            LogAssert.ignoreFailingMessages = true;

            try
            {
                // Ensure server is stopped
                if (MCPServer.State != ServerState.Stopped)
                {
                    MCPServer.Stop();
                    await Task.Delay(200);
                }

                // Save current port setting
                int originalPort = MCPSettings.Port;
                MCPSettings.Port = 0;

                try
                {
                    MCPServer.Start();
                    
                    // Wait for it to start (it happens async)
                    int timeout = 50; // 5 seconds max
                    while (MCPServer.State == ServerState.Starting && timeout-- > 0)
                    {
                        await Task.Delay(100);
                    }

                    Assert.AreEqual(ServerState.Running, MCPServer.State, "Server should be running");
                    Assert.Greater(MCPServer.Port, 0, "Server should have a dynamic port > 0");
                }
                finally
                {
                    MCPServer.Stop();
                    MCPSettings.Port = originalPort;
                }
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [Test]
        public async Task ServerState_Lifecycle()
        {
            // Wait for any background activity to settle
            await Task.Delay(500);
            
            // This is a loose test because background auto-start might be happening
            Assert.That(MCPServer.State, Is.Not.EqualTo(ServerState.Starting), "Should not be stuck in Starting");
        }

        private object InvokePrivateMethod(string methodName, params object[] args)
        {
            var method = typeof(MCPServer).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null) throw new Exception($"Method {methodName} not found in MCPServer");
            return method.Invoke(null, args);
        }
    }
}
