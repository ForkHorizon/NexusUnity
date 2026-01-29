using System;
using System.Net;
using UnityEditor;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Partial implementation of MCPServerWindow handling networking and server lifecycle.
    /// </summary>
    public partial class MCPServerWindow
    {
        private async void StartServer()
        {
            if (_isRunning) return;
            _isRunning = true;
            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://*:{_port}/");
            _listener.Start();
            SessionState.SetBool("MCP_Server_Running", true);
            Task.Run(() => ServerLoop(_cts.Token));
            Debug.Log($"[MCP] Server started on port {_port}");
        }

        private async Task ServerLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var context = await _listener.GetContextAsync();
                if (context.Request.IsWebSocketRequest) await ProcessWebSocket(context);
                else HandleHttpRequest(context);
            }
        }

        private async Task ProcessWebSocket(HttpListenerContext context)
        {
            var wsContext = await context.AcceptWebSocketAsync(null);
            _webSocket = wsContext.WebSocket;
            await ReceiveWebsocketLoop(_cts.Token);
        }

        private void HandleHttpRequest(HttpListenerContext context)
        {
            // Security: Host Header Validation (DNS Rebinding protection)
            // Ensure the request is intended for localhost
            if (!context.Request.Url.IsLoopback)
            {
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                context.Response.Close();
                return;
            }

            // Security: Method Validation
            if (context.Request.HttpMethod != "POST")
            {
                context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                context.Response.Close();
                return;
            }

            // Security: Content-Type Validation (CSRF protection)
            // Enforce application/json to prevent simple form POSTs from browsers
            if (context.Request.ContentType == null || !context.Request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = (int)HttpStatusCode.UnsupportedMediaType;
                context.Response.Close();
                return;
            }

            using var reader = new System.IO.StreamReader(context.Request.InputStream);
            string requestBody = reader.ReadToEnd();
            string response = MCPServerMethods.ProcessJsonRpc(requestBody);
            byte[] buffer = Encoding.UTF8.GetBytes(response);
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.Close();
        }

        private async Task ReceiveWebsocketLoop(CancellationToken token)
        {
            var buffer = new byte[4096];
            while (_webSocket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    string response = MCPServerMethods.ProcessJsonRpc(msg);
                    await SendResponse(response);
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
