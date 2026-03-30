using System;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    public static partial class MCPServer
    {
        private static async Task<bool> IsAnotherMcpInstanceRunning()
        {
            using var client = new System.Net.Http.HttpClient();
            client.Timeout = TimeSpan.FromMilliseconds(300); 
            try
            {
                var content = new System.Net.Http.StringContent("{\"jsonrpc\":\"2.0\",\"method\":\"initialize\",\"params\":{},\"id\":1}", Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"http://127.0.0.1:{_port}/", content);
                string body = await response.Content.ReadAsStringAsync();
                return body.Contains("Unity MCP Server");
            }
            catch { return false; }
        }

        private static void BindAndStartListener()
        {
            try
            {
                _cts = new CancellationTokenSource();
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
                _listener.Prefixes.Add($"http://localhost:{_port}/");
                _listener.Start();
                EditorPrefs.SetBool(PrefsKey, true);
                _ = Task.Run(() => ServerLoop(_cts.Token));
                Debug.Log($"[MCP] Server started on port {_port}");
            }
            catch (Exception e)
            {
                Cleanup();
                Debug.LogError($"[MCP] Server failed to start: {e.Message}");
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
                    else HandleHttpRequest(context);
                }
            }
            catch (Exception e)
            {
                if (!token.IsCancellationRequested)
                    Debug.LogError($"[MCP] Fatal server loop error: {e.Message}");
            }
            finally { _isRunning = false; }
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

                if (context.Request.HttpMethod != "POST") {
                    context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    context.Response.Close();
                    return;
                }

                using (var reader = new System.IO.StreamReader(context.Request.InputStream, context.Request.ContentEncoding ?? Encoding.UTF8))
                {
                    string json = reader.ReadToEnd();
                    string response = MCPServerMethods.ProcessJsonRpc(json);
                    byte[] buffer = Encoding.UTF8.GetBytes(response);
                    context.Response.ContentType = "application/json";
                    context.Response.ContentLength64 = buffer.Length;
                    context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                    context.Response.Close();
                }
            }
            catch (ObjectDisposedException) { }
            catch (System.Net.HttpListenerException) { }
            catch (Exception e)
            {
                Debug.LogError($"[MCP] Error handling HTTP request: {e.Message}");
            }
        }

        private static async Task ReceiveWebsocketLoop(CancellationToken token)
        {
            var buffer = new byte[4096];
            using var ms = new MemoryStream();

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