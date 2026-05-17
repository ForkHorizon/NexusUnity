## 2024-05-22 - Unity Editor HTTP Server CSRF
**Vulnerability:** Local HTTP servers in Unity Editor (using `HttpListener`) are vulnerable to CSRF attacks from malicious websites via the browser.
**Learning:** `HttpListener` binds to localhost but does not validate the `Origin` header by default, allowing cross-origin requests from browsers.
**Prevention:** Always validate the `Origin` header in `HttpListener` handlers to ensure it matches `localhost`, `127.0.0.1`, or is empty (for non-browser clients).

## 2024-05-23 - HttpListener Host Validation
**Vulnerability:** Relying on `HttpListener` binding to `localhost` is insufficient for preventing DNS Rebinding if the Host header isn't validated.
**Learning:** `context.Request.Url.IsLoopback` provides a robust, built-in check for ensuring the request targets the local machine, handling IPv4, IPv6, and localhost names correctly.
**Prevention:** Use `!context.Request.Url.IsLoopback` to reject requests not targeting the loopback interface.

## 2024-05-24 - Unity MCP Path Traversal
**Vulnerability:** File operations like `read_file` and `write_file` exposed by an MCP server allowed reading/writing arbitrary files outside the project via `..` paths.
**Learning:** `System.IO` methods like `File.ReadAllText` accept paths outside the project root without restriction. `Path.GetFullPath` is essential to resolve `..` before validation.
**Prevention:** Implement strict path validation that resolves the full path and checks if it starts with the project root directory before performing any file I/O.

## 2024-11-26 - WebSocket CSWSH & DNS Rebinding
**Vulnerability:** The MCP server accepted WebSocket connections without validating the `Origin` header or Host (via `IsLoopback` check) in `ProcessWebSocket`, allowing arbitrary websites to connect to localhost (CSWSH) and execute commands.
**Learning:** `HttpListener.AcceptWebSocketAsync` does not automatically perform `Origin` validation. Explicit checks are needed for both Host (DNS Rebinding) and Origin (CSWSH) for WebSockets, just like HTTP requests.
**Prevention:** In `ProcessWebSocket`, verify `context.Request.Url.IsLoopback` (Host) and check `context.Request.Headers["Origin"]` to ensure it is either empty/safe or coming from a loopback address.

## 2025-02-20 - Partial Path Traversal via Prefix Matching
**Vulnerability:** Path validation using `StartsWith` allowed access to sibling directories with matching prefixes (e.g., `/ProjectSecret` matched `/Project`).
**Learning:** Checking `path.StartsWith(root)` is insufficient because it doesn't respect directory boundaries.
**Prevention:** Ensure path validation checks for an exact match OR that the path starts with `root + DirectorySeparatorChar` (e.g., `/Project/`).

## 2025-02-28 - HTTP CSRF & DNS Rebinding
**Vulnerability:** The MCP server accepted HTTP requests without validating the `Origin` header, allowing arbitrary websites to connect to localhost (CSRF) and execute commands.
**Learning:** The previous fix only applied `Origin` validation to WebSocket connections, leaving HTTP requests vulnerable. Both endpoints need `Origin` checks.
**Prevention:** In `HandleHttpRequest`, verify `context.Request.Headers["Origin"]` to ensure it is either empty/safe or coming from a loopback address.
## 2024-04-26 - [High] DoS vulnerability via memory exhaustion in Unity Networking

**Vulnerability:** Unbounded use of `StreamReader.ReadToEnd()` for HTTP requests and unbounded accumulation via `MemoryStream.Write()` in WebSocket loop.
**Learning:** Unity networking logic that reads directly from standard input streams into memory (strings/byte arrays) without checking stream limits can cause total memory exhaustion and Denial of Service if the server handles massive or chunked payloads.
**Prevention:** Always enforce a maximum payload limit (e.g. 10MB) via `ContentLength64` or string length limits for HTTP requests, and accumulate check chunk sizes (`ms.Length`) inside the WebSocket `ReceiveAsync` loop before executing writes.

## 2025-03-01 - HTTP Chunked Request DoS
**Vulnerability:** The MCP HTTP server allowed requests with missing `Content-Length` (or chunked transfer encoding where `ContentLength64` is `-1`) to bypass the maximum payload size limit.
**Learning:** Checking `ContentLength64 > maxPayloadSize` is insufficient because `ContentLength64` returns `-1` if the header is missing, allowing unbounded memory allocation via JSON parsing.
**Prevention:** Always check if `ContentLength64 < 0` and return `411 Length Required` to ensure stream limits are strictly enforced.
