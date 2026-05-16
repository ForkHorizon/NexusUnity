# Nexus Unity

Nexus Unity is an open source Unity Editor automation package. It runs a local JSON-RPC server inside the Unity Editor and exposes editor, scene, asset, log, test, and inspection tools to local developer workflows.

Package id: `com.forkhorizon.nexus.unity`

License: GPLv3

## Requirements

- Unity `6000.0` or newer.
- Local machine access to the Unity Editor.
- Python 3 for the optional MCP stdio bridge.

## Install

Use Unity Package Manager and add the package from a Git URL:

```text
https://github.com/<owner>/<repo>.git
```

When using the validation project in this repository, the package source is under:

```text
Assets/NexusUnity
```

The public package repository should be cut from that folder, not from the full validation project.

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

## Python MCP Bridge

The package includes a Python stdio bridge for MCP clients:

```bash
python3 Assets/NexusUnity/Editor/nexus_unity_bridge.py
```

The bridge exposes the same public tools as Unity `list_tools`, with a `unity_` prefix for MCP clients. For direct one-off calls:

```bash
python3 Assets/NexusUnity/Editor/nexus_unity_bridge.py unity_get_server_status
python3 Assets/NexusUnity/Editor/nexus_unity_bridge.py unity_read_logs count=20
```

## Features

- Server health and readiness checks.
- Scene, hierarchy, component, and prefab automation.
- Asset database and project file operations constrained to the project root.
- Console log reading with cursor-based incremental polling.
- ScriptableObject read, diff, duplicate, create, and update tools.
- PlayerPrefs utilities.
- Runtime mouse and touch simulation for Play Mode workflows.
- UI Toolkit window inspection and interaction helpers.
- Compact scene snapshots, dependency mapping, and editor timeline tools.
- NUnit test triggering through Unity's Test Runner API.

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
