## 2024-05-22 - Unity Editor HTTP Server CSRF
**Vulnerability:** Local HTTP servers in Unity Editor (using `HttpListener`) are vulnerable to CSRF attacks from malicious websites via the browser.
**Learning:** `HttpListener` binds to localhost but does not validate the `Origin` header by default, allowing cross-origin requests from browsers.
**Prevention:** Always validate the `Origin` header in `HttpListener` handlers to ensure it matches `localhost`, `127.0.0.1`, or is empty (for non-browser clients).

## 2024-05-23 - HttpListener Host Validation
**Vulnerability:** Relying on `HttpListener` binding to `localhost` is insufficient for preventing DNS Rebinding if the Host header isn't validated.
**Learning:** `context.Request.Url.IsLoopback` provides a robust, built-in check for ensuring the request targets the local machine, handling IPv4, IPv6, and localhost names correctly.
**Prevention:** Use `!context.Request.Url.IsLoopback` to reject requests not targeting the loopback interface.
