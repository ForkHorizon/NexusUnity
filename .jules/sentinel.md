## 2024-05-22 - Unity Editor HTTP Server CSRF
**Vulnerability:** Local HTTP servers in Unity Editor (using `HttpListener`) are vulnerable to CSRF attacks from malicious websites via the browser.
**Learning:** `HttpListener` binds to localhost but does not validate the `Origin` header by default, allowing cross-origin requests from browsers.
**Prevention:** Always validate the `Origin` header in `HttpListener` handlers to ensure it matches `localhost`, `127.0.0.1`, or is empty (for non-browser clients).
