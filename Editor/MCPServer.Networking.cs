using System;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    public static partial class MCPServer
    {
        private static async Task<bool> IsAnotherMcpInstanceRunning()
        {
            using var client = new System.Net.Http.HttpClient();
            client.Timeout = TimeSpan.FromMilliseconds(500); 
            client.DefaultRequestHeaders.Add(AuthTokenHeaderName, AuthToken);
            try
            {
                var content = new System.Net.Http.StringContent("{\"jsonrpc\":\"2.0\",\"method\":\"get_server_status\",\"params\":{},\"id\":1}", Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"http://127.0.0.1:{_port}/", content);
                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden) return true;
                string body = await response.Content.ReadAsStringAsync();
                
                if (body.Contains("serverAlive"))
                {
                    var json = JObject.Parse(body);
                    string remoteProjectPath = json["result"]?["projectPath"]?.ToString();
                    string localProjectPath = Directory.GetCurrentDirectory().Replace("\\", "/");
                    int remotePid = json["result"]?["processId"]?.Value<int>() ?? -1;
                    int localPid = System.Diagnostics.Process.GetCurrentProcess().Id;

                    if (!string.IsNullOrEmpty(remoteProjectPath))
                    {
                        if (remoteProjectPath == localProjectPath)
                        {
                            if (remotePid == localPid)
                            {
                                NexusEditorLog.Warning(NexusLogCategory.Server, $"[MCP] Detected zombie listener from previous domain (PID: {localPid}). Disconnecting stale entity...");
                                var shutdownContent = new System.Net.Http.StringContent("{\"jsonrpc\":\"2.0\",\"method\":\"shutdown_server\",\"params\":{},\"id\":1}", Encoding.UTF8, "application/json");
                                try { await client.PostAsync($"http://127.0.0.1:{_port}/", shutdownContent); } catch { }
                                await Task.Delay(200); // Give it time to close
                                return false; // Allow Start() to try binding its own listener
                            }

                            NexusEditorLog.Log(NexusLogCategory.Server, $"[MCP] Existing MCP server found on port {_port}. Project matches current workspace. Action taken: attached to existing session.", true);
                            return true;
                        }
                        else
                        {
                            NexusEditorLog.Error(NexusLogCategory.Server, $"[MCP] Existing MCP server found on port {_port}. Project does NOT match current workspace. Action required: choose another port or stop the other session. Remote project: {remoteProjectPath} (PID: {remotePid})");
                            return true; // We consider it "running" to prevent attempting to start our own and throwing a cryptic error
                        }
                    }
                }
                
                // Fallback to old initialize method just in case it's an older server
                var initContent = new System.Net.Http.StringContent("{\"jsonrpc\":\"2.0\",\"method\":\"initialize\",\"params\":{},\"id\":1}", Encoding.UTF8, "application/json");
                var initResponse = await client.PostAsync($"http://127.0.0.1:{_port}/", initContent);
                if (initResponse.StatusCode == HttpStatusCode.Unauthorized || initResponse.StatusCode == HttpStatusCode.Forbidden) return true;
                string initBody = await initResponse.Content.ReadAsStringAsync();
                if (initBody.Contains("Unity MCP Server")) {
                    NexusEditorLog.Log(NexusLogCategory.Server, $"[MCP] Connected to an older existing session on port {_port}", true);
                    return true;
                }

                return false;
            }
            catch { return false; }
        }

        private static void BindAndStartListener()
        {
            try
            {
                if (_cts == null || _cts.IsCancellationRequested) return;

                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
                _listener.Prefixes.Add($"http://localhost:{_port}/");
                _listener.Start();
                _state = ServerState.Running;
                _ = Task.Run(() => ServerLoop(_cts.Token));
                NexusEditorLog.Log(NexusLogCategory.Server, $"[MCP] Server started on port {_port}", true);
            }
            catch (Exception e)
            {
                Cleanup();
                _state = ServerState.Error;
                string owner = GetPortOwner(_port);
                LastError = $"{e.Message} (Port {_port} owner: {owner})";
                NexusEditorLog.Error(NexusLogCategory.Server, $"[MCP] Server failed to start: {LastError}");
            }
        }

        private static async Task ServerLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && _listener != null && _listener.IsListening)
                {
                    var context = await _listener.GetContextAsync();
                    if (context.Request.IsWebSocketRequest) await ProcessWebSocket(context);
                    else _ = Task.Run(() => HandleHttpRequest(context));
                }
            }
            catch (Exception e)
            {
                if (!token.IsCancellationRequested)
                    NexusEditorLog.Error(NexusLogCategory.Server, $"[MCP] Fatal server loop error: {e.Message}");
            }
            finally { if (_state == ServerState.Running) _state = ServerState.Stopped; }
        }

        private static bool IsValidOrigin(HttpListenerContext context)
        {
            if (!context.Request.Url.IsLoopback) return false;

            string origin = context.Request.Headers["Origin"];
            if (string.IsNullOrEmpty(origin)) return true;

            try
            {
                Uri originUri = new Uri(origin);
                return originUri.IsLoopback && (originUri.Scheme == Uri.UriSchemeHttp || originUri.Scheme == Uri.UriSchemeHttps);
            }
            catch
            {
                return false;
            }
        }

        private static async Task ProcessWebSocket(HttpListenerContext context)
        {
            if (!IsValidOrigin(context))
            {
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                context.Response.Close();
                return;
            }

            if (!IsAuthorized(context))
            {
                RejectUnauthorized(context);
                return;
            }

            var wsContext = await context.AcceptWebSocketAsync(null);
            _webSocket = wsContext.WebSocket;
            await ReceiveWebsocketLoop(_cts.Token);
        }

        private static void HandleHttpRequest(HttpListenerContext context)
        {
            try {
                if (!IsValidOrigin(context))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    context.Response.Close();
                    return;
                }

                if (!IsAuthorized(context))
                {
                    RejectUnauthorized(context);
                    return;
                }

                if (context.Request.HttpMethod != "POST") {
                    context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    context.Response.Close();
                    return;
                }

                const long maxPayloadSize = 10 * 1024 * 1024; // 10MB limit to prevent memory exhaustion
                if (context.Request.ContentLength64 > maxPayloadSize)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
                    context.Response.Close();
                    return;
                }

                string requestJson;
                using (var reader = new System.IO.StreamReader(context.Request.InputStream, context.Request.ContentEncoding ?? Encoding.UTF8))
                {
                    var sb = new System.Text.StringBuilder();
                    char[] buffer = new char[4096];
                    int charsRead;
                    int totalChars = 0;
                    while ((charsRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        totalChars += charsRead;
                        if (totalChars > maxPayloadSize)
                        {
                            context.Response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
                            context.Response.Close();
                            return;
                        }
                        sb.Append(buffer, 0, charsRead);
                    }
                    requestJson = sb.ToString();
                }

                string response = MCPServerMethods.ProcessJsonRpc(requestJson);
                byte[] responseBuffer = Encoding.UTF8.GetBytes(response);
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = responseBuffer.Length;
                context.Response.OutputStream.Write(responseBuffer, 0, responseBuffer.Length);
                context.Response.Close();
            }
            catch (ObjectDisposedException) { }
            catch (System.Net.HttpListenerException) { }
            catch (ThreadAbortException) { }
            catch (Exception e)
            {
                NexusEditorLog.Error(NexusLogCategory.Server, $"[MCP] Error handling HTTP request: {e.Message}");
            }
        }

        private static bool IsAuthorized(HttpListenerContext context)
        {
            return IsAuthorizedToken(context.Request.Headers[AuthTokenHeaderName]);
        }

        internal static bool IsAuthorizedToken(string token)
        {
            return !string.IsNullOrEmpty(token) && string.Equals(token, _authToken, StringComparison.Ordinal);
        }

        private static void RejectUnauthorized(HttpListenerContext context)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.Close();
        }

        private static async Task ReceiveWebsocketLoop(CancellationToken token)
        {
            var buffer = new byte[4096];
            using var ms = new MemoryStream();
            const long maxPayloadSize = 10 * 1024 * 1024; // 10MB limit

            while (_webSocket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                ms.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                        return;
                    }

                    if (ms.Length + result.Count > maxPayloadSize)
                    {
                        NexusEditorLog.Error(NexusLogCategory.Server, "[MCP] WebSocket payload exceeded maximum size. Disconnecting.");
                        await _webSocket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Payload too large", CancellationToken.None);
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage && !token.IsCancellationRequested);

                if (ms.Length > 0 && result.MessageType == WebSocketMessageType.Text)
                {
                    ms.Position = 0;
                    using (var reader = new System.IO.StreamReader(ms, Encoding.UTF8, false, 1024, leaveOpen: true))
                    {
                        string response = MCPServerMethods.ProcessJsonRpc(reader);
                        if (_webSocket.State == WebSocketState.Open)
                        {
                            var respBuffer = Encoding.UTF8.GetBytes(response);
                            await _webSocket.SendAsync(new ArraySegment<byte>(respBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                    }
                }
            }
        }
    }
}
