using System;
using System.Net;
using UnityEditor;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Partial implementation of MCPServerWindow handling networking and server lifecycle.
    /// </summary>
    public partial class MCPServerWindow
    {
        internal async void StartServer()
        {
            if (_isRunning) return;

            if (await IsPortOccupiedByAnotherServer())
            {
                Debug.LogError($"[MCP] Cannot start server. Port {{_port}} is already being used by another Unity MCP instance.");
                SessionState.SetBool("MCP_Server_Running", false);
                return;
            }
            
            BindAndStartListener();
        }

        private async void BindAndStartListener()
        {
            int retries = 3;
            while (retries > 0)
            {
                try
                {
                    _isRunning = true;
                    _cts = new CancellationTokenSource();
                    _listener = new HttpListener();
                    _listener.Prefixes.Add($"http://*:{_port}/");
                    _listener.Start();
                    SessionState.SetBool("MCP_Server_Running", true);
                    Task.Run(() => ServerLoop(_cts.Token));
                    Debug.Log($"[MCP] Server started on port {{_port}}");
                    return;
                }
                catch (Exception e)
                {
                    CleanupServer();
                    retries--;
                    if (retries > 0)
                    {
                        Debug.LogWarning($"[MCP] Server bind failed ({{e.Message}}). Retrying in 1s...");
                        await Task.Delay(1000);
                    }
                    else Debug.LogError($"[MCP] Server failed to start after multiple attempts: {{e.Message}}");
                }
            }
        }

        private async Task<bool> IsPortOccupiedByAnotherServer()
        {
            using var client = new System.Net.Http.HttpClient();
            client.Timeout = TimeSpan.FromMilliseconds(500); 
            try
            {
                var content = new System.Net.Http.StringContent("{\"jsonrpc\":\"2.0\",\"method\":\"initialize\",\"params\":{},\"id\":1}", Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"http://localhost:{_port}/", content);
                if (response.IsSuccessStatusCode)
                {
                    string body = await response.Content.ReadAsStringAsync();
                    return body.Contains("Unity MCP Server");
                }
            }
            catch { }
            return false;
        }

        internal void StopServer()
        {
            SessionState.SetBool("MCP_Server_Running", false); // Clear intent
            CleanupServer();
            Debug.Log("[MCP] Server stopped manually");
        }

        private void CleanupServer()
        {
            _cts?.Cancel();
            if (_listener != null)
            {
                try { if (_listener.IsListening) _listener.Stop(); } catch { }
                try { _listener.Close(); } catch { }
            }
            _isRunning = false;
        }

        private async Task ServerLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && _listener != null && _listener.IsListening)
                {
                    try
                    {
                        var context = await _listener.GetContextAsync();
                        if (context.Request.IsWebSocketRequest) await ProcessWebSocket(context);
                        else HandleHttpRequest(context);
                    }
                    catch (HttpListenerException e) when (e.ErrorCode == 995) 
                    {
                        break;
                    }
                    catch (Exception e)
                    {
                        if (!token.IsCancellationRequested)
                            Debug.LogWarning($"[MCP] Error processing request: {{e.Message}}");
                    }
                }
            }
            catch (Exception e)
            {
                if (!token.IsCancellationRequested)
                    Debug.LogError($"[MCP] Fatal server loop error: {{e.Message}}");
            }
            finally
            {
                _isRunning = false;
                if (!token.IsCancellationRequested && SessionState.GetBool("MCP_Server_Running", false))
                {
                    Debug.Log("[MCP] Server loop ended unexpectedly. Attempting restart...");
                    EditorApplication.delayCall += () => StartServer();
                }
            }
        }

        private async Task ProcessWebSocket(HttpListenerContext context)
        {
            if (!context.Request.Url.IsLoopback)
            {
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                context.Response.Close();
                return;
            }

            string origin = context.Request.Headers["Origin"];
            if (!string.IsNullOrEmpty(origin) && Uri.TryCreate(origin, UriKind.Absolute, out Uri originUri))
            {
                if ((originUri.Scheme == Uri.UriSchemeHttp || originUri.Scheme == Uri.UriSchemeHttps) && !originUri.IsLoopback)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    context.Response.Close();
                    return;
                }
            }

            var wsContext = await context.AcceptWebSocketAsync(null);
            _webSocket = wsContext.WebSocket;
            await ReceiveWebsocketLoop(_cts.Token);
        }

        private void HandleHttpRequest(HttpListenerContext context)
        {
            if (!context.Request.Url.IsLoopback)
            {
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                context.Response.Close();
                return;
            }

            if (context.Request.HttpMethod != "POST")
            {
                context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                context.Response.Close();
                return;
            }

            if (context.Request.ContentType == null || !context.Request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = (int)HttpStatusCode.UnsupportedMediaType;
                context.Response.Close();
                return;
            }

            using var reader = new System.IO.StreamReader(context.Request.InputStream);
            string response = MCPServerMethods.ProcessJsonRpc(reader);
            byte[] buffer = Encoding.UTF8.GetBytes(response);
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.Close();
        }

        private async Task ReceiveWebsocketLoop(CancellationToken token)
        {
            var buffer = new byte[4096];
            using var ms = new System.IO.MemoryStream();

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

                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage && !token.IsCancellationRequested);

                if (ms.Length > 0 && result.MessageType == WebSocketMessageType.Text)
                {
                    ms.Position = 0;
                    using (var reader = new System.IO.StreamReader(ms, Encoding.UTF8, false, 1024, leaveOpen: true))
                    {
                        string response = MCPServerMethods.ProcessJsonRpc(reader);
                        await SendResponse(response);
                    }
                }
            }
        }

        private async Task SendResponse(string response)
        {
            if (_webSocket?.State == WebSocketState.Open)
            {
                var buffer = Encoding.UTF8.GetBytes(response);
                await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }
}