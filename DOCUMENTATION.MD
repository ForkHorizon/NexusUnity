# Nexus Unity Technical Documentation

Version: `1.1.1`

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
- Provides `get_server_status`, `ping_main_thread`, `wait_for_asset_import_idle`, `wait_for_editor_idle`, `get_test_results`, `get_tool_usage_stats`, and `reset_tool_usage_stats` for robust automation loops.

## Unity Editor Menu

Nexus Unity exposes one Unity menu entry: `Window > Nexus Unity`.

- `Server`: local server lifecycle, URL copy, server health, and bridge deployment status.
- `Integrations`: MCP setup cards for Codex, Claude Desktop, Gemini, Antigravity, Cursor, VS Code/Cline/Roo, Windsurf, and generic JSON clients.
- `Resources`: documentation, API reference, changelog, package folder access, and collapsed maintainer diagnostics.
- `Settings`: Nexus Unity editor preferences, including Console logging verbosity.

The main control panel is a compact UI Toolkit window with `Server`, `Integrations`, `Resources`, and `Settings` tabs. Test-only actions are hidden by default under `Resources > Advanced / Diagnostics`, while user-facing setup actions remain visible. Server and bridge blocks remain stacked vertically; their internal action rows stretch and wrap responsively, and integration cards expose status, auto setup, copyable config, and config-location actions where supported.

Console logging defaults to `Important`, which keeps the Unity Console focused on server lifecycle, setup results, warnings, and errors. `All` shows verbose Nexus Unity diagnostics, and `Custom` shows info logs only for selected categories: Server, Integrations, API, UI Automation, Diagnostics, Audit, and Runtime. These settings affect Nexus Unity service messages only; user project logs and Unity errors are not suppressed.

## MCP Client Configuration

The `Integrations` tab builds all snippets from the active `python3` executable and the project-root bridge path `nexus_unity_bridge.py`. Auto setup deploys the bridge first. JSON-style clients use `mcpServers.nexus-unity`, VS Code-compatible clients use `.vscode/mcp.json` with `servers.nexus-unity`, and Codex uses `[mcp_servers.nexus-unity]` in TOML.

When Nexus Unity writes an existing user or project config, it preserves unrelated server entries and creates a timestamped backup next to the original file.

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

Bridge connection settings can be overridden with `NEXUS_UNITY_URL`, `NEXUS_UNITY_PORT`, and `NEXUS_UNITY_TIMEOUT_SECONDS`. A positional port argument is still supported for existing MCP client configs.

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
- Agent diagnostics include `get_test_results` for Unity `TestResults*.xml` summaries, `get_tool_usage_stats` / `reset_tool_usage_stats` for scoped in-memory call counters and timing, and `ui_get_window_rect` / `ui_set_window_rect` / `ui_capture_window_snapshot` for resize-based editor UI QA.

MCP bridge:

- Uses `unity_` prefixed manager tools, for example `unity_scene_manager`, `unity_asset_manager`, and `unity_write_and_compile`.
- Intended for AI clients and day-to-day automation.
- Reduces schema size while preserving access to the supported raw capabilities.
- `unity_editor_controller` includes `run_tests_wait`, which waits in the Python bridge by polling raw `get_test_results` instead of blocking the Unity main thread.
- `unity_ui_automation` passes through `deep` for UI hierarchy reads, `class_name` for queries, and window rect/snapshot actions for layout checks.

## Current Compatibility Decisions

The current development line preserves backward compatibility while correcting public contracts found during API stress validation:

- `unity_write_and_compile` is the supported public MCP macro for write, wait, and compiler-feedback workflows. Any old documentation wording that says `unity_apply_code_change` should be treated as stale.
- `update_component` should use a `properties` object for new callers; `json_data` remains accepted for existing callers.
- `create_material` accepts an optional explicit `path` so automation can isolate generated materials in a known project folder.
- `invoke_method.arguments` is documented and exposed as a JSON array schema.
- `click_object_in_game` accepts either `instance_id` or hierarchy `path`.
- `get_test_results`, scoped tool usage stats, and editor-window rect/snapshot automation are public raw diagnostics for agent-driven development.

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

Nexus Unity uses GitHub Actions as the contributor enforcement gate. The `Validate package` workflow runs static package checks on every push and pull request, a required self-hosted AI documentation review, and an optional Unity EditMode job when the repository has a `UNITY_LICENSE` secret configured.

Contributor pull requests target `development`. The `main` branch is release-only and should receive changes only when maintainers prepare or publish a release.

Direct pushes to `main` and `development` are blocked for everyone, including maintainers. Trusted maintainers merge pull requests through GitHub; release changes reach `main` only through a release pull request from `development` or `release/*`.

For local feedback, contributors can run `bash scripts/install-git-hooks.sh` once per checkout. This configures Git to use the tracked `.githooks/pre-push` hook, which runs `scripts/prepush-validate.sh --quick` before push. The quick hook always performs package metadata validation, Python bridge compilation, Unity `.meta` pairing checks, `NexusQualityGate` tests, and the deterministic Roslyn quality gate. It uses `NEXUS_UNITY_HOOK_LIVE=auto` by default: reachable Unity servers run the read-only live smoke check, while temporary server timeouts print `NOTICE` and do not block the push. Use `NEXUS_UNITY_HOOK_LIVE=required` when a live server must be enforced, or `NEXUS_UNITY_HOOK_LIVE=off` for static-only local feedback.

`NexusQualityGate` lives under `tools~/` so Unity ignores the .NET/Roslyn source while GitHub Actions and local scripts can build it. The deterministic gate scans production C# source under `Editor/` and `Runtime/`, fails public/protected types and methods without meaningful XML documentation, fails files over 450 lines, and fails methods over 150 lines. It warns for files over 300 lines and methods over 50 lines.

The required AI documentation review runs on a maintainer-owned self-hosted GitHub Actions runner with the label `nexus-doc-ai`. The job uses GitHub Actions concurrency group `nexus-doc-ai` with `cancel-in-progress: false`, so multiple pull requests queue instead of running Ollama in parallel. The runner must expose Ollama locally through `http://127.0.0.1:11434`; the workflow defaults to the local `qwen3-coder:30b-a3b-q4_K_M` model. The AI rubric is pragmatic: it fails misleading, filler, or materially incomplete docs, but it does not require every private helper, minor edge case, or implementation detail. The reviewer sends `NEXUS_DOC_AI_KEEP_ALIVE` to Ollama on each request, defaults it to `30s`, and unloads the model at the end of the review. The workflow also has an `always()` cleanup step that unloads the same model even when validation fails. For pull requests, the job executes the quality tool from the trusted base branch and treats the pull request checkout as input data. While bootstrapping the first quality-gate pull request, it falls back to the candidate tool when the trusted base branch does not contain `NexusQualityGate` yet.

Full local integration validation is explicit:

```bash
bash scripts/prepush-validate.sh --integration
```

Integration validation requires the Unity project to be open and the Nexus Unity server to be running on `http://127.0.0.1:8081/`.

For a focused agent tooling check, run `python3 scripts/agent-tooling-smoke.py`. It exercises server status, raw catalog discovery, scoped usage stats, Nexus Unity window UI automation, snapshot capture, and bridge-side `run_tests_wait` without writing repo-tracked files.

Before release, maintainers should run a public API stress audit that compares raw `list_tools` output with the MCP bridge catalog, exercises read-only and mutating tool groups in a disposable namespace, and verifies cleanup of generated assets and PlayerPrefs keys.

## Troubleshooting

- Server does not start: change the port in project settings and restart the server window.
- MCP client cannot import `nexus_bridge`: redeploy the bridge from `Window > Nexus Unity > Server`, or point the client directly at the package path.
- Tool returns `Method not found`: compare the raw Unity catalog from `list_tools` with the MCP catalog in `API_REFERENCE.MD`.
- File write denied: ensure the target path resolves inside the Unity project root.
- Compile loop times out: call `get_server_status`, `read_logs`, and `unity_wait` with `editor_idle`.

## Packaging Rules

- Public repo: `https://github.com/ForkHorizon/NexusUnity.git`.
- Package id: `com.forkhorizon.nexus.unity`.
- Public release version: `1.1.1`.
- License: `GPL-3.0-only`.
- Required release docs: `SECURITY.md`, `CONTRIBUTING.md`, and `RELEASE.md`.
- Do not ship generated caches, local agent folders, `.jules/`, `.DS_Store`, Python bytecode, or native bridge binaries without corresponding source and build instructions.
- Keep contributor tooling under Unity-ignored `tools~/` without `.meta` files; Unity will not import that folder, and root `tools~.meta` entries break immutable PackageCache installs.

## Development Versioning Policy

Nexus Unity follows semantic versioning for public releases, but the development branch should not bump the package version for every merged fix. Keep `package.json` and visible docs at the latest shipped public version until a release is being prepared.

Unity Package Manager requires `MAJOR.MINOR.PATCH` values in `package.json`, so the technical package version and git tag use forms like `1.1.0` and `v1.1.0`. Release titles and user-facing copy can use the shorter `1.1`, `1.2`, `1.3` naming style when the patch component is zero.

During normal development:

- Add all user-visible API, behavior, docs, and validation changes to `[Unreleased]` in `CHANGELOG.md`.
- Do not change `package.json` from `1.1.1` unless the change is part of a release-preparation commit.
- Prefer compatibility fixes over breaking changes; if a breaking change is unavoidable, document the migration path before release.

During release preparation:

- Choose the next semantic version based on accumulated changes.
- Move `[Unreleased]` entries into the new dated release section.
- Update `package.json`, README badges/install examples, `DOCUMENTATION.MD`, and `API_REFERENCE.MD`.
- Tag the release with the same version, for example `v1.1.0`.
