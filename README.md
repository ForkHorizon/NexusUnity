# Nexus Unity

[![Tag](https://img.shields.io/github/v/tag/ForkHorizon/NexusUnity?sort=semver&label=release)](https://github.com/ForkHorizon/NexusUnity/releases/tag/v1.0.0)
[![License: GPL-3.0-only](https://img.shields.io/badge/license-GPL--3.0--only-blue.svg)](LICENSE.md)
[![Unity](https://img.shields.io/badge/Unity-6000.0%2B-black?logo=unity)](package.json)
[![Validate package](https://github.com/ForkHorizon/NexusUnity/actions/workflows/validate.yml/badge.svg)](https://github.com/ForkHorizon/NexusUnity/actions/workflows/validate.yml)

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

For reproducible installs, pin the public release tag:

```text
https://github.com/ForkHorizon/NexusUnity.git#v1.0.0
```

## Start The Server

1. Open `Window > Nexus Unity`.
2. Click `Start Server`.
3. Confirm the server is listening on the configured loopback port, usually:

```text
http://127.0.0.1:8081/
```

The server is intended for trusted local automation only. It validates loopback hosts and browser origins, constrains file operations to the Unity project root, and limits request payload size.

## Editor Menu

Nexus Unity uses a single Unity menu entry: `Window > Nexus Unity`.

- `Server`: start, stop, restart, copy the local URL, and deploy the MCP bridge to the project root.
- `Integrations`: configure Codex, Claude Desktop, Gemini, Antigravity, Cursor, VS Code/Cline/Roo, Windsurf, or a generic MCP JSON client.
- `Resources`: open docs, API reference, changelog, and the package folder.
- `Settings`: choose how much Nexus Unity service logging is written to the Unity Console.

Diagnostic actions such as the UI test window, log verification, Codex link test, project audit, and API verification are available from the collapsed `Advanced / Diagnostics` block in `Resources`. The default tabs show only user-facing setup and server actions. Server and bridge blocks stay stacked vertically, while their action rows and the integration cards stretch and wrap cleanly across narrow and wide editor windows.

Console logging defaults to `Important`, which shows key lifecycle/setup messages plus warnings and errors. Use `Settings` or `Edit > Project Settings > Nexus Unity` to switch to `All` for full diagnostics or `Custom` to show info logs only for selected Nexus Unity categories.

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

For Codex, Claude Desktop, Gemini, Antigravity, Cursor, VS Code/Cline/Roo, Windsurf, or compatible MCP clients, open `Window > Nexus Unity` and use the `Integrations` tab.

Each integration card shows `Detected`, `Not found`, `Configured`, or `Error` status and provides:

- `Auto Setup` when Nexus Unity can safely write or invoke the client configuration.
- `Copy Config` for manual setup.
- `Open Config` when the client uses a known config file path.

Nexus Unity generates configs from the current `python3` path and deployed `nexus_unity_bridge.py` path. User/global config writes create a timestamped backup before modifying an existing file.

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
- `unity_editor_controller`: play mode, menus, undo/redo, logs, editor state, asset refresh, and test-result polling.
- `unity_ui_automation`: query and operate Unity Editor UI Toolkit windows, including window rects for resize QA.

See `API_REFERENCE.MD` for the complete raw and MCP tool catalogs.

## Compatibility Notes

Current development keeps the public API backward-compatible while tightening schemas and documentation:

- Use `unity_write_and_compile` for code-edit workflows. Older references to `unity_apply_code_change` are outdated and should not be used for the public MCP bridge.
- `update_component` accepts the preferred `properties` object and the legacy `json_data` string form.
- `create_material` accepts an optional `path` so generated materials can be created inside a chosen project folder instead of the project root `Assets/` folder.
- `invoke_method.arguments` is an optional positional JSON array.
- `click_object_in_game` accepts a documented `instance_id` target and still supports hierarchy `path` lookup.
- `get_test_results` reads Unity `TestResults*.xml` summaries from the project root or Unity persistent data path; `unity_editor_controller` exposes `run_tests_wait` as a bridge-side polling workflow.
- `get_tool_usage_stats` reports in-memory raw tool counts, durations, and errors since Unity domain load without storing request payloads; `reset_tool_usage_stats` clears that state for scoped diagnostics.
- `ui_get_window_rect`, `ui_set_window_rect`, and `ui_capture_window_snapshot` support automated layout checks for editor windows.

## Documentation

| Document | Purpose |
|---|---|
| `DOCUMENTATION.MD` | Architecture, setup, security, bridge deployment, and troubleshooting |
| `API_REFERENCE.MD` | Raw JSON-RPC and MCP bridge tool catalogs |
| `SECURITY.md` | Supported versions, vulnerability reporting, and local-only security policy |
| `CONTRIBUTING.md` | Contribution rules and validation expectations |
| `CODE_OF_CONDUCT.md` | Community behavior expectations |
| `RELEASE.md` | Public release checklist and smoke test |
| `CHANGELOG.md` | Public release history |
| `LICENSE.md` | GPL-3.0-only license text |

## Contributor Validation

GitHub Actions is the required validation gate for public contributions. Maintainers should configure the `Validate package` workflow as a required status check before merge.

Contributor pull requests should target `development`. The `main` branch is release-only and should be updated only by maintainers during release preparation.

Direct pushes to `main` and `development` are blocked for everyone. Trusted maintainers merge pull requests in GitHub; external contributors can contribute through fork or feature-branch pull requests without direct protected-branch access.

Contributors can also install the optional local pre-push hook for faster feedback:

```bash
bash scripts/install-git-hooks.sh
```

The hook runs `scripts/prepush-validate.sh --quick`, which is designed to finish in under a minute on normal contributor machines. It always runs static package validation and uses `NEXUS_UNITY_HOOK_LIVE=auto` by default: when the Unity server is reachable, the hook adds a short read-only live smoke check for the public raw tool catalog and key schemas; when the server is temporarily unreachable, it prints `NOTICE` and lets the push continue.

Live smoke behavior can be made explicit:

```bash
NEXUS_UNITY_HOOK_LIVE=off bash scripts/prepush-validate.sh --quick
NEXUS_UNITY_HOOK_LIVE=required bash scripts/prepush-validate.sh --quick
```

Full local integration validation is opt-in:

```bash
bash scripts/prepush-validate.sh --integration
```

Maintainers and agents can also run the optional focused tooling smoke:

```bash
python3 scripts/agent-tooling-smoke.py
```

For integration tests, open the Unity project, start the Nexus Unity server from `Window > Nexus Unity`, and set `NEXUS_UNITY_PROJECT_ROOT` if the package is not checked out under a Unity project.

## Development Versioning

Do not bump `package.json` for every change while development is unreleased. Keep the package at the latest public release version, currently `1.0.0`, and record user-visible work under `[Unreleased]` in `CHANGELOG.md`.

When maintainers prepare a release, move the accumulated `[Unreleased]` entries to the new version section, update `package.json` and the visible version strings in `README.md`, `DOCUMENTATION.MD`, and `API_REFERENCE.MD`, then tag the release. Use semantic versioning: patch for compatible fixes, minor for backward-compatible API additions or behavior improvements, and major only for breaking public contracts.

## Community

Please use GitHub Issues for reproducible bugs and focused feature requests. Security reports should follow `SECURITY.md` and use GitHub Security Advisories rather than public issues.

## Release Notes

The `1.0.0` open source release ships the Python bridge as the supported MCP bridge. Experimental native bridge artifacts are not included.
