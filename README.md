# Nexus Unity

Nexus Unity is an open source Unity Editor automation package. It runs a local JSON-RPC server inside the Unity Editor and exposes scene, asset, code, log, test, inspection, and UI automation tools to trusted local developer workflows.

- Package id: `com.forkhorizon.nexus.unity`
- Version: `1.0.0`
- License: `GPL-3.0-only`
- Public repository: `https://github.com/ForkHorizon/NexusUnity.git`

## Requirements

- Unity `6000.0` or newer.
- Local machine access to the Unity Editor.
- Python 3 for MCP bridge integrations.

## Install

1. Open your Unity project.
2. Go to `Window > Package Manager`.
3. Click `+` and select `Add package from git URL...`.
4. Enter:

```text
https://github.com/ForkHorizon/NexusUnity.git
```

## Start The Server

1. Open `Window > Nexus Unity`.
2. Click `START SERVER`.
3. Confirm the server is listening on the configured loopback port, usually:

```text
http://127.0.0.1:8081/
```

The server is intended for trusted local automation only. It validates loopback hosts and browser origins, constrains file operations to the Unity project root, and limits request payload size.

## Public APIs

Nexus Unity supports two public surfaces:

- Raw HTTP JSON-RPC tools: unprefixed Unity method names returned by `list_tools`.
- MCP bridge tools: consolidated `unity_` manager tools optimized for AI clients.

Direct JSON-RPC example:

```bash
curl -s http://127.0.0.1:8081/ \
  -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","method":"get_server_status","params":{},"id":1}'
```

MCP clients should use the Python bridge:

```bash
python3 Packages/com.forkhorizon.nexus.unity/Editor/nexus_unity_bridge.py
```

The Unity window can also deploy the bridge to the project root for CLIs that prefer stable local paths. The deployment copies both `nexus_unity_bridge.py` and the required `nexus_bridge/` module.

## AI Client Setup

For Claude Code, Cursor, Codex, Gemini, Antigravity, or compatible MCP clients, configure a command server that runs Python with the bridge script.

Package path:

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

Root-deployed path:

```json
{
  "mcpServers": {
    "nexus-unity": {
      "command": "python3",
      "args": ["nexus_unity_bridge.py"]
    }
  }
}
```

## Key Tools

- `unity_write_and_compile`: write files, wait for Unity reload, and return compiler errors.
- `unity_scene_manager`: create, open, save, and list scenes.
- `unity_hierarchy_manager`: create, destroy, duplicate, activate, and parent GameObjects.
- `unity_component_manager`: add, inspect, update, and remove components.
- `unity_asset_manager`: search, import, refresh, and manage prefab assets.
- `unity_editor_controller`: play mode, menus, undo/redo, logs, and editor state.
- `unity_ui_automation`: query and operate Unity Editor UI Toolkit windows.

See `API_REFERENCE.MD` for the complete raw and MCP tool catalogs.

## Documentation

| Document | Purpose |
|---|---|
| `DOCUMENTATION.MD` | Architecture, setup, security, bridge deployment, and troubleshooting |
| `API_REFERENCE.MD` | Raw JSON-RPC and MCP bridge tool catalogs |
| `SECURITY.md` | Supported versions, vulnerability reporting, and local-only security policy |
| `CONTRIBUTING.md` | Contribution rules and validation expectations |
| `RELEASE.md` | Public release checklist and smoke test |
| `CHANGELOG.md` | Public release history |
| `LICENSE.md` | GPL-3.0-only license text |

## Release Notes

The `1.0.0` open source release ships the Python bridge as the supported MCP bridge. Experimental native bridge artifacts are not included.
