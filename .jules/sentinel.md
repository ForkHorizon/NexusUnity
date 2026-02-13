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

## 2024-11-27 - Partial Path Traversal in ValidatePath
**Vulnerability:** The `ValidatePath` method relied on `StartsWith` to check if a file path was inside the project root, but failed to enforce a trailing directory separator on the root path. This allowed access to sibling directories with the same prefix (e.g., `/ProjectSecrets` matching `/Project`).
**Learning:** String prefix checks (`StartsWith`) are insufficient for path validation unless the prefix is guaranteed to end with a directory separator to denote a full folder boundary.
**Prevention:** Always append a trailing directory separator (e.g., `/`) to the authorized root path before checking if a target path starts with it, or check for exact directory equality.
