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
## 2026-04-05 - [Denial of Service via Unbounded Stream Reading]
**Vulnerability:** Reading an HTTP request body using `reader.ReadToEnd()` without enforcing a maximum size limit allows an attacker to send an arbitrarily large payload (e.g., 10GB), causing memory exhaustion (OOM) and crashing the application (DoS).
**Learning:** Relying on the client to send reasonably sized requests is unsafe. Unbounded reads load the entire stream into memory at once.
**Prevention:** Enforce a strict limit on the request body size by checking `context.Request.ContentLength64` and incrementally reading the stream while validating the byte/character count, returning `HTTP 413 (RequestEntityTooLarge)` if exceeded.
