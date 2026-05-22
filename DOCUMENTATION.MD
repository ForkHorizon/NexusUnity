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

Open `Window > Nexus Unity` and click `Start Server`. The default port is `8081` and can be changed in `Edit > Project Settings > Nexus Unity`.

The server:

- Binds to loopback use only.
- Restarts after Unity domain reloads when the previous session was running.
- Tracks session id, session generation, last heartbeat, play mode, compile/import state, and last error.
- Provides `get_server_status`, `ping_main_thread`, `wait_for_asset_import_idle`, and `wait_for_editor_idle` for robust automation loops.

## Unity Editor Menu

Nexus Unity exposes one Unity menu entry: `Window > Nexus Unity`.

- `Server`: main server lifecycle, documentation, and CLI linking UI.
- `Tools`: UI automation test window, Codex link test, and log management.
- `Verification`: project audit, API verification, UI verification, and log verification.

The main server control panel is a compact UI Toolkit window with `Server`, `Tools`, and `Verification` tabs. CLI integration, resource, and verification action rows wrap responsively so the panel remains usable at narrow editor widths.

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
- Runtime schemas intentionally include compatibility fields where needed; for example `update_component` accepts both `properties` and the legacy `json_data` payload, and `create_material` accepts an optional explicit asset `path`.

MCP bridge:

- Uses `unity_` prefixed manager tools, for example `unity_scene_manager`, `unity_asset_manager`, and `unity_write_and_compile`.
- Intended for AI clients and day-to-day automation.
- Reduces schema size while preserving access to the supported raw capabilities.

## Current Compatibility Decisions

The current development line preserves backward compatibility while correcting public contracts found during API stress validation:

- `unity_write_and_compile` is the supported public MCP macro for write, wait, and compiler-feedback workflows. Any old documentation wording that says `unity_apply_code_change` should be treated as stale.
- `update_component` should use a `properties` object for new callers; `json_data` remains accepted for existing callers.
- `create_material` accepts an optional explicit `path` so automation can isolate generated materials in a known project folder.
- `invoke_method.arguments` is documented and exposed as a JSON array schema.
- `click_object_in_game` accepts either `instance_id` or hierarchy `path`.

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

## Contributor Validation

Nexus Unity uses GitHub Actions as the contributor enforcement gate. The `Validate package` workflow runs static package checks on every push and pull request, and includes an optional Unity EditMode job when the repository has a `UNITY_LICENSE` secret configured.

Contributor pull requests target `development`. The `main` branch is release-only and should receive changes only when maintainers prepare or publish a release.

Direct pushes to `main` and `development` are blocked for everyone, including maintainers. Trusted maintainers merge pull requests through GitHub; release changes reach `main` only through a release pull request from `development` or `release/*`.

For local feedback, contributors can run `bash scripts/install-git-hooks.sh` once per checkout. This configures Git to use the tracked `.githooks/pre-push` hook, which runs `scripts/prepush-validate.sh --quick` before push. The quick hook always performs static package validation and adds a short read-only live smoke check when the Nexus Unity server is already reachable.

Full local integration validation is explicit:

```bash
bash scripts/prepush-validate.sh --integration
```

Integration validation requires the Unity project to be open and the Nexus Unity server to be running on `http://127.0.0.1:8081/`.

Before release, maintainers should run a public API stress audit that compares raw `list_tools` output with the MCP bridge catalog, exercises read-only and mutating tool groups in a disposable namespace, and verifies cleanup of generated assets and PlayerPrefs keys.

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

## Development Versioning Policy

Nexus Unity follows semantic versioning for public releases, but the development branch should not bump the package version for every merged fix. Keep `package.json` and visible docs at the latest shipped public version until a release is being prepared.

During normal development:

- Add all user-visible API, behavior, docs, and validation changes to `[Unreleased]` in `CHANGELOG.md`.
- Do not change `package.json` from `1.0.0` unless the change is part of a release-preparation commit.
- Prefer compatibility fixes over breaking changes; if a breaking change is unavoidable, document the migration path before release.

During release preparation:

- Choose the next semantic version based on accumulated changes.
- Move `[Unreleased]` entries into the new dated release section.
- Update `package.json`, README badges/install examples, `DOCUMENTATION.MD`, and `API_REFERENCE.MD`.
- Tag the release with the same version, for example `v1.1.0`.
