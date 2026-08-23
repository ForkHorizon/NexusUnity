using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    // Probing existing MCP server instances, legacy server detection, and port discovery.
    // Split out of MCPServer.Networking.cs to keep files modular and well under readability limits.
    public static partial class MCPServer
    {
        // Kept on its own line (no surrounding code braces): the "//" inside the
        // URL confuses simple readability linters that strip comments before
        // strings, so we never place a brace on the same line as this literal.
        private static string LoopbackUrl => $"http://127.0.0.1:{_port}/";

        private static System.Net.Http.StringContent JsonRpcContent(string method)
        {
            string payload = "{\"jsonrpc\":\"2.0\",\"method\":\"" + method + "\",\"params\":{},\"id\":1}";
            return new System.Net.Http.StringContent(payload, Encoding.UTF8, "application/json");
        }

        private static async Task<bool> IsAnotherMcpInstanceRunning()
        {
            using var client = new System.Net.Http.HttpClient();
            client.Timeout = TimeSpan.FromMilliseconds(500);
            client.DefaultRequestHeaders.Add(AuthTokenHeaderName, AuthToken);
            try
            {
                var response = await client.PostAsync(LoopbackUrl, JsonRpcContent("get_server_status"));
                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    return await HandleUnauthorizedProbe();
                }

                string body = await response.Content.ReadAsStringAsync();
                if (body.Contains("serverAlive"))
                {
                    bool? verdict = await EvaluateAliveServer(client, body);
                    if (verdict.HasValue) return verdict.Value;
                }

                return await ProbeLegacyServer(client);
            }
            catch { return false; }
        }

        // A 401/403 means either a genuine other instance, or our own stale
        // listener from a previous domain reload. Returns true only for the
        // former; cleans up and returns false for the latter.
        private static async Task<bool> HandleUnauthorizedProbe()
        {
            string owner = GetPortOwner(_port);
            int localPid = System.Diagnostics.Process.GetCurrentProcess().Id;
            if (!string.IsNullOrEmpty(owner) && owner.Contains(localPid.ToString()))
            {
                NexusEditorLog.Warning(NexusLogCategory.Server, $"[MCP] Detected unauthorized listener from previous domain (PID: {localPid}). Cleaning up stale listener...");
                Cleanup();
                await Task.Delay(200);
                return false;
            }
            return true;
        }

        // Returns a conclusive verdict from a live server's identity, or null to
        // fall through to the legacy-server probe.
        private static async Task<bool?> EvaluateAliveServer(System.Net.Http.HttpClient client, string body)
        {
            var json = JObject.Parse(body);
            string remoteProjectPath = json["result"]?["projectPath"]?.ToString();
            if (string.IsNullOrEmpty(remoteProjectPath)) return null;

            string localProjectPath = Directory.GetCurrentDirectory().Replace("\\", "/");
            int remotePid = json["result"]?["processId"]?.Value<int>() ?? -1;
            int localPid = System.Diagnostics.Process.GetCurrentProcess().Id;

            if (remoteProjectPath != localProjectPath)
            {
                NexusEditorLog.Error(NexusLogCategory.Server, $"[MCP] Existing MCP server found on port {_port}. Project does NOT match current workspace. Action required: choose another port or stop the other session. Remote project: {remoteProjectPath} (PID: {remotePid})");
                return true;
            }

            if (remotePid == localPid)
            {
                NexusEditorLog.Warning(NexusLogCategory.Server, $"[MCP] Detected zombie listener from previous domain (PID: {localPid}). Disconnecting stale entity...");
                try { await client.PostAsync(LoopbackUrl, JsonRpcContent("shutdown_server")); } catch { }
                await Task.Delay(200); // Give it time to close
                return false; // Allow Start() to try binding its own listener
            }

            NexusEditorLog.Log(NexusLogCategory.Server, $"[MCP] Existing MCP server found on port {_port}. Project matches current workspace. Action taken: attached to existing session.", true);
            return true;
        }

        // Fallback for older servers that predate get_server_status.
        private static async Task<bool> ProbeLegacyServer(System.Net.Http.HttpClient client)
        {
            var initResponse = await client.PostAsync(LoopbackUrl, JsonRpcContent("initialize"));
            if (initResponse.StatusCode == HttpStatusCode.Unauthorized || initResponse.StatusCode == HttpStatusCode.Forbidden) return true;

            string initBody = await initResponse.Content.ReadAsStringAsync();
            if (initBody.Contains("Unity MCP Server"))
            {
                NexusEditorLog.Log(NexusLogCategory.Server, $"[MCP] Connected to an older existing session on port {_port}", true);
                return true;
            }
            return false;
        }

        // Resolves the configured port, allocating a free ephemeral port when it is left at 0.
        private static void ResolvePort()
        {
            if (_port <= 0)
            {
                ParseCommandLineArgs();
                _port = _cliPortOverride ?? MCPSettings.Port;
            }

            if (_port != 0) return;

            try
            {
                var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
                l.Start();
                _port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
                l.Stop();
                System.Threading.Thread.Sleep(100);
            }
            catch { }
        }

        // Returns true if we may proceed to bind the busy port ourselves; false
        // if another instance/app owns it (state is set accordingly).
        private static async Task<bool> TryClaimBusyPort()
        {
            if (await IsAnotherMcpInstanceRunning())
            {
                _state = ServerState.Attached;
                return false;
            }

            string owner = GetPortOwner(_port);
            if (owner != "Unknown Process")
            {
                _state = ServerState.Error;
                LastError = $"Port {_port} is being used by another application: {owner}.";
                NexusEditorLog.Error(NexusLogCategory.Server, $"[MCP] {LastError}");
                return false;
            }

            NexusEditorLog.Warning(NexusLogCategory.Server, $"[MCP] Port {_port} reported busy by Unknown Process. Proceeding with force-bind attempt...");
            return true;
        }
    }
}
