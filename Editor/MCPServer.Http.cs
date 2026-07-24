using System;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UnityMCP.Editor
{
    // HTTP/WebSocket request handling and authorization for the loopback server.
    // Split out of MCPServer.Networking.cs to keep each file under the readability
    // line budget; both are the same partial MCPServer class.
    public static partial class MCPServer
    {
        private const long MaxPayloadSize = 10 * 1024 * 1024; // 10MB, prevents memory exhaustion

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
            try
            {
                if (!IsValidOrigin(context))
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

                if (!TryReadRequestBody(context, out string requestJson)) return;

                bool isProbeMethod = !string.IsNullOrEmpty(requestJson) && (requestJson.Contains("\"get_server_status\"") || requestJson.Contains("\"shutdown_server\""));
                if (!IsAuthorized(context) && !isProbeMethod)
                {
                    RejectUnauthorized(context);
                    return;
                }

                WriteJsonResponse(context, MCPServerMethods.ProcessJsonRpc(requestJson));
            }
            catch (ObjectDisposedException) { }
            catch (System.Net.HttpListenerException) { }
            catch (ThreadAbortException) { }
            catch (Exception e)
            {
                NexusEditorLog.Error(NexusLogCategory.Server, $"[MCP] Error handling HTTP request: {e.Message}");
            }
        }

        // Reads the request body with a hard size cap. Returns false (and closes
        // the response with 413) when the payload is too large.
        private static bool TryReadRequestBody(HttpListenerContext context, out string requestJson)
        {
            requestJson = null;

            if (context.Request.ContentLength64 > MaxPayloadSize)
            {
                context.Response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
                context.Response.Close();
                return false;
            }

            using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding ?? Encoding.UTF8);
            var sb = new StringBuilder();
            char[] buffer = new char[4096];
            int charsRead;
            long totalChars = 0;
            while ((charsRead = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                totalChars += charsRead;
                if (totalChars > MaxPayloadSize)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
                    context.Response.Close();
                    return false;
                }
                sb.Append(buffer, 0, charsRead);
            }

            requestJson = sb.ToString();
            return true;
        }

        private static void WriteJsonResponse(HttpListenerContext context, string response)
        {
            byte[] responseBuffer = Encoding.UTF8.GetBytes(response);
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = responseBuffer.Length;
            context.Response.OutputStream.Write(responseBuffer, 0, responseBuffer.Length);
            context.Response.Close();
        }

        private static bool IsAuthorized(HttpListenerContext context)
        {
            string requestToken = context.Request.Headers[AuthTokenHeaderName];
            if (string.IsNullOrEmpty(requestToken))
            {
                requestToken = context.Request.Headers["X-Nexus-Auth-Token"];
            }
            return IsAuthorizedToken(requestToken);
        }

        internal static bool IsAuthorizedToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            string activeToken = AuthToken?.Trim();
            if (string.Equals(token.Trim(), activeToken, StringComparison.Ordinal)) return true;

            string fileToken = ReadTokenFile();
            if (!string.IsNullOrEmpty(fileToken) && string.Equals(token.Trim(), fileToken.Trim(), StringComparison.Ordinal))
            {
                _authToken = fileToken.Trim();
                return true;
            }
            return false;
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

                    if (ms.Length + result.Count > MaxPayloadSize)
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
                    using var reader = new StreamReader(ms, Encoding.UTF8, false, 1024, leaveOpen: true);
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
