# Nexus Unity Technical Documentation

Nexus Unity is a Unity Editor automation package built around a local JSON-RPC server. It is intended for trusted local developer workflows and local AI tooling.

## Architecture

```text
Local tool or MCP client
  -> optional Python stdio bridge
  -> Nexus Unity HTTP JSON-RPC server
  -> Unity Editor main thread
```

The server runs inside the Unity Editor. Network handling happens off the main thread, while Unity API calls are queued back onto the editor main thread.

## Server Lifecycle

- Default port: `8081`.
- Dynamic port mode is supported by setting the configured port to `0`.
- Session id and session generation are stored through Unity editor session state.
- Domain reloads increment the session generation and rebuild the method registry.
- Health checks such as `get_server_status` run without blocking on the Unity main thread.

The recommended startup path is:

1. Install the package.
2. Open `Window > Nexus Unity`.
3. Click `START SERVER`.
4. Call `get_server_status`.
5. Call `list_tools` to discover the public tool catalog.

## JSON-RPC Protocol

Requests use JSON-RPC 2.0:

```json
{
  "jsonrpc": "2.0",
  "method": "get_server_status",
  "params": {},
  "id": 1
}
```

Successful responses return a `result`. Failures return a JSON-RPC `error` with a message suitable for diagnostics.

## MCP Bridge

`Editor/nexus_unity_bridge.py` adapts MCP stdio clients to the Unity HTTP server.

- Unity HTTP method names are unprefixed.
- MCP bridge tool names are prefixed with `unity_`.
- When Unity is online, the bridge reads the live `list_tools` catalog from Unity.
- When Unity is offline, the bridge returns a static fallback catalog generated from the same public tool list.

## Threading

Unity APIs are not thread-safe. Nexus Unity separates work into two paths:

- Listener-thread operations for health and wait tools that only read cached editor state.
- Main-thread operations for scene, asset, editor, and serialized object changes.

Long-running write workflows should prefer batch or sync tools instead of many small calls.

## Security

Nexus Unity is a local automation server, not a public network service.

- Bind to local loopback only.
- Reject non-loopback host and origin headers.
- Limit incoming request payload size.
- Keep file operations inside the Unity project root.
- Use Unity Undo for scene and asset operations where practical.

Do not expose the server port to untrusted networks.

## Package Layout

| Path | Purpose |
|---|---|
| `Editor/MCPServer.cs` | Server lifecycle, state, and main-thread queue |
| `Editor/MCPServer.Networking.cs` | HTTP listener and request handling |
| `Editor/MCPServerMethods*.cs` | JSON-RPC tool implementations |
| `Editor/MCPServerWindow*.cs` | Unity Editor dashboard |
| `Editor/nexus_unity_bridge.py` | Python MCP stdio bridge |
| `Editor/Tests/` | Editor tests |
| `Runtime/` | Runtime support code |
| `package.json` | Unity package manifest |

## Packaging Rules

The public package repository should be cut from `Assets/NexusUnity`.

Do not publish validation project files, local agent configs, Graphify caches, Unity generated folders, or binary bridge artifacts without corresponding source and build instructions.

## Troubleshooting

### Server Does Not Start

- Check that another process is not using the configured port.
- Use port `0` for dynamic port assignment.
- Open `Window > Nexus Unity` and inspect the displayed server state.

### Requests Timeout

- Call `get_server_status` first.
- Check `busyReason`, `isCompiling`, `isImporting`, and `mainThreadResponsive`.
- Wait for `wait_for_editor_idle` before sending write commands.

### File Operations Fail

- Use paths inside the Unity project root.
- Prefer `Assets/...` project paths for assets.
- Path traversal and sibling project access are intentionally rejected.

### Tests Do Not Run

- Confirm Unity Test Framework is installed.
- Use `run_tests` with `mode` set to `EditMode` or `PlayMode`.

## Development Checks

Before release:

- Run Editor tests.
- Confirm `list_tools`, dispatch registration, and bridge fallback match.
- Confirm no public docs mention private or future internal products.
- Confirm no generated cache or binary-only bridge artifact is tracked.
