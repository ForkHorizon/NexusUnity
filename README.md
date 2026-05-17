# Nexus Unity

Nexus Unity is an open source Unity Editor automation package. It runs a local JSON-RPC server inside the Unity Editor and exposes editor, scene, asset, log, test, and inspection tools to local developer workflows.

Package id: `com.forkhorizon.nexus.unity`

License: GPLv3

## Requirements

- Unity `6000.0` or newer.
- Local machine access to the Unity Editor.
- **Python 3** (required if you want to use AI agents like Claude Code or Cursor).

## Install

1. Open your Unity project.
2. Go to `Window > Package Manager`.
3. Click the `+` icon in the top left and select **"Add package from git URL..."**.
4. Enter the repository URL:
   ```text
   https://github.com/<owner>/<repo>.git
   ```
   *(Note: The actual package code resides in `Assets/NexusUnity` if you are browsing this repository locally).*

## Start The Server

1. Open a Unity project with Nexus Unity installed.
2. Open `Window > Nexus Unity`.
3. Click `START SERVER`.
4. Confirm the server is listening, usually on:

   ```text
   http://localhost:8081/
   ```

The server only binds to local loopback access. Use it for local editor automation, diagnostics, and trusted local AI tooling.

## Direct JSON-RPC

Nexus Unity accepts JSON-RPC 2.0 requests over HTTP:

```bash
curl -s http://127.0.0.1:8081/ \
  -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","method":"get_server_status","params":{},"id":1}'
```

Tool names are unprefixed at the HTTP JSON-RPC layer, for example `get_server_status`, `list_tools`, and `read_logs`.

## Python MCP Bridge & AI Clients

The package includes a Python script that translates AI tool calls into Unity JSON-RPC calls. This allows tools like **Claude Code** or **Cursor** to directly control your Unity Editor.

To use the bridge, your AI client needs to run it as an MCP (Model Context Protocol) server.

### Connecting Claude Code or Cursor

You need to configure your AI tool to run the Python bridge script. Since Unity installs packages into the `Packages` directory or your `Assets` folder, you must locate the script first.

When installed via Git, the script is typically at:
```bash
python3 Packages/com.forkhorizon.nexus.unity/Editor/nexus_unity_bridge.py
```

*If you copied the package manually into your Assets folder, it will be at `Assets/NexusUnity/Editor/nexus_unity_bridge.py`.*

#### Claude Code Setup
Add an MCP server configuration pointing to your Python executable and the bridge script:
```json
{
  "mcpServers": {
    "nexus-unity": {
      "command": "python3",
      "args": ["Packages/com.forkhorizon.nexus.unity/Editor/nexus_unity_bridge.py"]
    }
  }
}
```

### Troubleshooting: Port Conflicts

If the Unity server fails to start, port `8081` might be in use by another application.
1. In Unity, go to **Edit > Project Settings > Nexus Unity**.
2. Change the **Port** from `8081` to an available port (e.g., `8082`).
3. Restart the server via **Window > Nexus Unity**.

## Features

- **Consolidated Tooling:** Unified managers for Scene, Hierarchy, Components, and Wait conditions to reduce token overhead.
- **Batch Operations:** `unity_apply_code_change` macro for optimized write-compile-verify cycles.
- **Server Health:** Readiness checks and self-healing server loop.
- **Discovery:** Semantic search, dependency mapping, and hierarchy snapshots.
- **Play Mode:** Runtime mouse and touch simulation.
- **UI Toolkit:** Inspector and Editor Window interaction helpers.
- **Test Runner:** NUnit test triggering support.

## Security Model

- Requests are limited to local loopback hosts.
- Origin and host checks protect against browser-origin attacks.
- File operations are constrained to the Unity project root.
- Payload size is capped to reduce memory exhaustion risk.
- Unity API work is dispatched onto the main thread where required.

## Documentation

| Document | Purpose |
|---|---|
| `DOCUMENTATION.MD` | Technical guide, architecture, setup, and troubleshooting |
| `API_REFERENCE.MD` | Public tool catalog |
| `CHANGELOG.md` | Version history |
| `LICENSE.md` | GPLv3 license notice |

## Release Notes

The first public release ships the Python bridge as the supported MCP bridge. Previous experimental Mojo bridge artifacts are not included in the open source package.
