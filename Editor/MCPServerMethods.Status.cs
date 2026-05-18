using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
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

        private static JToken Initialize(JToken p) => new JObject { ["protocolVersion"] = "2024-11-05", ["serverInfo"] = new JObject { ["name"] = "Unity MCP Server", ["version"] = MCPServer.Version } };

        private static JToken GetServerStatus(JToken p)
        {
            bool isCompiling = MCPServer.IsCompilingCached;
            bool isUpdating = MCPServer.IsUpdatingCached;
            bool isPlaying = MCPServer.IsPlayingCached;
            bool isPaused = MCPServer.IsPausedCached;
            bool isPlayModeTransition = MCPServer.IsPlayModeTransitionCached;
            bool isMainThreadResponsive = (DateTime.UtcNow - MCPServer.LastMainThreadTickUtc).TotalSeconds < 5;

            string busyReason = "idle";
            if (isCompiling) busyReason = "compiling";
            else if (isUpdating) busyReason = "importing";
            else if (isPlayModeTransition && !isPlaying) busyReason = "play_mode_transition";

            return new JObject {
                ["serverAlive"] = MCPServer.IsRunning,
                ["state"] = MCPServer.State.ToString(),
                ["busyReason"] = busyReason,
                ["lastError"] = MCPServer.LastError,
                ["port"] = MCPServer.Port,
                ["sessionId"] = MCPServer.SessionId,
                ["processId"] = System.Diagnostics.Process.GetCurrentProcess().Id,
                ["projectPath"] = Directory.GetCurrentDirectory().Replace("\\", "/"),
                ["unityVersion"] = Application.unityVersion,
                ["editorConnected"] = true,
                ["mainThreadResponsive"] = isMainThreadResponsive,
                ["editorState"] = new JObject {
                    ["isPlaying"] = isPlaying,
                    ["isCompiling"] = isCompiling,
                    ["isImporting"] = isUpdating,
                    ["isPaused"] = isPaused,
                    ["isPlayModeTransition"] = isPlayModeTransition
                },
                ["commandState"] = new JObject {
                    ["acceptsReadCommands"] = true,
                    ["acceptsWriteCommands"] = !isCompiling && !isUpdating,
                    ["busyReason"] = busyReason
                },
                ["lastHeartbeatUtc"] = MCPServer.LastMainThreadTickUtc.ToString("o"),
                ["sessionGeneration"] = MCPServer.SessionGeneration
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
            return !EditorApplication.isCompiling && !EditorApplication.isUpdating;
        }
    }
}
