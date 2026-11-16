using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        private static JToken ShutdownServer(JToken p)
        {
            // Note: We bypass the main thread check for shutdown to ensure it
            // works for zombie listeners from old domains.
            MCPServer.Cleanup();
            return new JObject { ["status"] = "Shutting down..." };
        }

        private static JToken Initialize(JToken p)
        {
            EditorReadiness.PublishFromEditor();
            EditorReadiness.Snapshot readiness = EditorReadiness.Published;
            if (!readiness.Ready)
                throw new Exception("Unity editor is not ready (" + readiness.Reason + ").");
            return new JObject { ["protocolVersion"] = "2024-11-05", ["serverInfo"] = new JObject { ["name"] = "Unity MCP Server", ["version"] = MCPServer.Version } };
        }

        private static JToken GetServerStatus(JToken p)
        {
            EditorReadiness.Snapshot readiness = EditorReadiness.Published;
            string busyReason = EditorReadiness.BusyReason(readiness);
            bool isMainThreadResponsive = (DateTime.UtcNow - MCPServer.LastMainThreadTickUtc).TotalSeconds < 5;

            return new JObject {
                ["serverAlive"] = MCPServer.IsRunning,
                ["state"] = MCPServer.State.ToString(),
                ["busyReason"] = busyReason,
                ["readiness"] = EditorReadiness.ToJson(readiness),
                ["lastError"] = MCPServer.LastError,
                ["port"] = MCPServer.Port,
                ["sessionId"] = MCPServer.SessionId,
                ["processId"] = System.Diagnostics.Process.GetCurrentProcess().Id,
                ["projectPath"] = Directory.GetCurrentDirectory().Replace("\\", "/"),
                ["unityVersion"] = MCPServer.UnityVersionCached,
                ["editorConnected"] = true,
                ["mainThreadResponsive"] = isMainThreadResponsive,
                ["editorState"] = new JObject {
                    ["isPlaying"] = MCPServer.IsPlayingCached,
                    ["isCompiling"] = readiness.IsCompiling,
                    ["isImporting"] = readiness.IsUpdating || busyReason == "importing",
                    ["isPaused"] = MCPServer.IsPausedCached,
                    ["isPlayModeTransition"] = readiness.IsPlayTransition
                },
                ["commandState"] = new JObject {
                    ["acceptsReadCommands"] = true,
                    ["acceptsWriteCommands"] = readiness.Ready,
                    ["busyReason"] = busyReason
                },
                ["lastHeartbeatUtc"] = MCPServer.LastMainThreadTickUtc.ToString("o"),
                ["sessionGeneration"] = MCPServer.SessionGeneration,
                ["runtime"] = Runtime.NexusRuntimeHost.ToStatusJson()
            };
        }

        private static JToken AttachExistingSession(JToken p)
        {
            return new JObject { ["status"] = "attached", ["sessionId"] = MCPServer.SessionId };
        }

        private static JToken PingMainThread(JToken p)
        {
            return new JObject { ["status"] = "pong", ["timestamp"] = DateTime.UtcNow.ToString("o") };
        }

        private static JToken WaitForReady(JToken p)
        {
            return EditorReadiness.Published.Ready;
        }
    }
}
