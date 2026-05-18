# Nexus Unity Technical Documentation

Version: `1.0.0`

Nexus Unity is a Unity Editor automation package with two public interfaces:

- A local HTTP JSON-RPC server running inside the Unity Editor.
- A Python MCP bridge that exposes consolidated `unity_` tools for AI clients.

## Architecture

- `MCPServer` owns server lifecycle, loopback networking, session recovery, request limits, log capture, and main-thread dispatch.
- `MCPServerMethods` owns raw JSON-RPC dispatch. Raw method names are unprefixed and discoverable through `list_tools`.
- `nexus_unity_bridge.py` is the MCP entry point. It imports `nexus_bridge/`, exposes the consolidated MCP schema, and routes MCP tool calls to raw Unity JSON-RPC methods.
- Unity API calls execute on the Unity main thread unless they are explicit health or idle checks that can safely run on the listener thread.

## Server Lifecycle

Open `Window > Nexus Unity` and click `START SERVER`. The default port is `8081` and can be changed in `Edit > Project Settings > Nexus Unity`.

The server:

- Binds to loopback use only.
- Restarts after Unity domain reloads when the previous session was running.
- Tracks session id, session generation, last heartbeat, play mode, compile/import state, and last error.
- Provides `get_server_status`, `ping_main_thread`, `wait_for_asset_import_idle`, and `wait_for_editor_idle` for robust automation loops.

## Security Model

Nexus Unity is a local developer tool and should be used only with trusted local clients.

- HTTP and WebSocket requests must target loopback hosts.
- Browser origins are validated to reduce CSRF and DNS rebinding exposure; non-loopback origins are rejected.
- File operations resolve paths and enforce the Unity project root boundary.
- Request payload size is capped to reduce memory exhaustion risk.
- Raw write operations are intentionally powerful; prefer `unity_write_and_compile` through the MCP bridge for code edits.

## Python MCP Bridge

Package path:

```bash
python3 Packages/com.forkhorizon.nexus.unity/Editor/nexus_unity_bridge.py
```

Manual or root-deployed path:

```bash
python3 nexus_unity_bridge.py
```

The bridge is modular:

- `nexus_unity_bridge.py`: MCP loop and CLI entry point.
- `nexus_bridge/client.py`: HTTP JSON-RPC client.
- `nexus_bridge/routing.py`: manager routing and macro behavior.
- `nexus_bridge/schemas.py`: public MCP tool schemas.

When deploying from the Unity UI, Nexus Unity copies both `nexus_unity_bridge.py` and the full `nexus_bridge/` package to the project root so the bridge can run from a stable path.

## Public API Surfaces

Raw HTTP JSON-RPC:

- Use unprefixed method names, for example `get_server_status`, `list_tools`, `read_logs`, and `write_file`.
- Intended for diagnostics, custom integrations, and direct automation.
- The complete raw catalog is returned by `list_tools`.

MCP bridge:

- Uses `unity_` prefixed manager tools, for example `unity_scene_manager`, `unity_asset_manager`, and `unity_write_and_compile`.
- Intended for AI clients and day-to-day automation.
- Reduces schema size while preserving access to the supported raw capabilities.

## Recommended AI Workflow

Use `unity_write_and_compile` for code edits. It writes one or more files, waits for Unity reload/import work, and returns compiler errors from the Unity Console. Use `unity_wait` when a workflow needs to block until compilation, import, play mode, or editor idle state is reached.

For scene work, prefer manager tools:

- `unity_scene_manager`
- `unity_hierarchy_manager`
- `unity_component_manager`
- `unity_search_manager`
- `unity_asset_manager`
- `unity_editor_controller`
- `unity_ui_automation`

## Troubleshooting

- Server does not start: change the port in project settings and restart the server window.
- MCP client cannot import `nexus_bridge`: redeploy the bridge from `Window > Nexus Unity`, or point the client directly at the package path.
- Tool returns `Method not found`: compare the raw Unity catalog from `list_tools` with the MCP catalog in `API_REFERENCE.MD`.
- File write denied: ensure the target path resolves inside the Unity project root.
- Compile loop times out: call `get_server_status`, `read_logs`, and `unity_wait` with `editor_idle`.

## Packaging Rules

- Public repo: `https://github.com/ForkHorizon/NexusUnity.git`.
- Package id: `com.forkhorizon.nexus.unity`.
- Public release version: `1.0.0`.
- License: `GPL-3.0-only`.
- Required release docs: `SECURITY.md`, `CONTRIBUTING.md`, and `RELEASE.md`.
- Do not ship generated caches, local agent folders, `.jules/`, `.DS_Store`, Python bytecode, or native bridge binaries without corresponding source and build instructions.
