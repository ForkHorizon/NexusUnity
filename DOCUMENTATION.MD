# Nexus Unity Technical Documentation

Version: `1.6.2`

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
- Restarts after Unity domain reloads and editor restarts when it was running. `Stop` turns that off until `Start Server` is clicked again.
- Tracks session id, session generation, last heartbeat, play mode, compile/import state, and last error.
- Provides `get_server_status`, `ping_main_thread`, `wait_for_asset_import_idle`, `wait_for_editor_idle`, `get_test_results`, `get_tool_usage_stats`, and `reset_tool_usage_stats` for robust automation loops.
- Serves fast-path health and wait calls from one published readiness snapshot when they run on the listener thread. Waits do not span a domain reload; clients re-poll after reconnect. `epoch.compile` and `epoch.reload` survive the reload.

## Unity Editor Menu

Nexus Unity exposes one Unity menu entry: `Window > Nexus Unity`.

- `Server`: local server lifecycle, URL copy, server health, and bridge deployment status.
- `Integrations`: MCP setup cards for Codex, Claude Desktop, Claude Code, Gemini, Antigravity, Cursor, VS Code/Cline/Roo, Windsurf, and generic JSON clients.
- `Resources`: documentation, API reference, changelog, package folder access, and collapsed maintainer diagnostics.
- `Settings`: Nexus Unity editor preferences, including Console logging verbosity.

The main control panel is a compact UI Toolkit window with `Server`, `Integrations`, `Resources`, and `Settings` tabs. Test-only actions are hidden by default under `Resources > Advanced / Diagnostics`, while user-facing setup actions remain visible. Server and bridge blocks remain stacked vertically; their internal action rows stretch and wrap responsively, and integration cards expose status, auto setup, copyable config, and config-location actions where supported.

Console logging defaults to `Important`, which keeps the Unity Console focused on server lifecycle, setup results, warnings, and errors. `All` shows verbose Nexus Unity diagnostics, and `Custom` shows info logs only for selected categories: Server, Integrations, API, UI Automation, Diagnostics, Audit, and Runtime. These settings affect Nexus Unity service messages only; user project logs and Unity errors are not suppressed.

## MCP Client Configuration

With `com.unity.pipeline` 1.0.0+ installed, with `com.unity.pipeline` 1.0.0+ installed, prefer Unity's own setup: `unity setup <client>` (or `unity setup all`) installs the Unity plugin and skills, and `unity mcp configure <client>` writes the `unity mcp` server entry, which also exposes the `nexus` command group. The Nexus `Integrations` installers are frozen (maintenance-only): they still configure the Legacy `nexus-unity` bridge, but get no new clients.

The `Integrations` tab builds all snippets from the resolved Python interpreter and the project-root bridge path `nexus_unity_bridge.py`. Auto setup deploys the bridge first. JSON-style clients use `mcpServers.nexus-unity`, VS Code-compatible clients use `.vscode/mcp.json` with `servers.nexus-unity`, Claude Code uses a project-root `.mcp.json` with `mcpServers.nexus-unity`, and Codex uses `[mcp_servers.nexus-unity]` in TOML.

Claude Code and Claude Desktop are distinct products with distinct config locations. The Claude Code card registers `nexus-unity` in `<projectRoot>/.mcp.json` (via the `claude` CLI when it is on `PATH`, otherwise by writing the file directly). If the CLI cannot remove a stale registration, setup skips its add command and writes `.mcp.json` directly instead; the CLI error is logged for diagnosis. The Claude Desktop card writes the desktop app config (`~/Library/Application Support/Claude/claude_desktop_config.json` on macOS, `%APPDATA%\Claude\claude_desktop_config.json` on Windows). Claude Code does not read the desktop config.

Gemini CLI uses a project-scoped stdio registration in `.gemini/settings.json`; Auto Setup keeps Gemini's normal tool-approval prompts enabled. Antigravity uses the workspace-local `.agents/mcp_config.json` with the standard `mcpServers` object, not the older shared Gemini configuration path. Cline uses `~/.cline/data/settings/cline_mcp_settings.json` for shared MCP settings.

### Platform notes

- The Python interpreter is resolved in the order `python3`, then `python`, then the Windows `py` launcher. On Windows, where `python3` is frequently absent from `PATH`, the generated config falls back to a discovered `python.exe` or `py`. Ensure a Python 3 interpreter is installed and on `PATH`.
- CLI-managed clients (Codex, Gemini, and the Claude Code CLI path) are discovered with `which` on macOS/Linux and `where` on Windows. The matching CLI must be on the `PATH` of the environment that launched the Unity Editor for `Auto Setup` to detect it; otherwise use `Copy Config` or the file-based card.

Integration status checks compare the package bridge with the project-root deployed bridge and the client config path. A card reports `Outdated` when the config points at another Unity project, the deployed bridge is missing, or the deployed bridge version differs from the package bridge version. Running `Auto Setup` redeploys `nexus_unity_bridge.py` plus `nexus_bridge/`, rewrites the client entry, and requires restarting the external MCP client session.

When Nexus Unity writes an existing user or project config, it preserves unrelated server entries and creates a timestamped backup next to the original file.

## Security Model

Nexus Unity is a local developer tool and should be used only with trusted local clients.

- HTTP and WebSocket requests must target loopback hosts.
- HTTP and WebSocket requests require the per-session `X-Nexus-Unity-Token` header; generated MCP configs pass it to the Python bridge as `NEXUS_UNITY_AUTH_TOKEN`. The token persists in `Library/NexusUnityAuthToken.txt` across editor restarts; when the configured token is missing or rejected, the bridge rereads that file, searching from its working directory and from the folders above the bridge script. Only `get_server_status` and `shutdown_server` are exempt, matched on the JSON-RPC `method` field.
- WebSocket and HTTP requests are processed asynchronously on background worker threads, ensuring the server accept loop remains non-blocking for concurrent clients.
- Browser origins are validated to reduce CSRF and DNS rebinding exposure; non-loopback origins are rejected.
- File operations resolve paths and enforce the Unity project root boundary.
- C# script writes require `confirm: true` before creating or overwriting `.cs` files and triggering Unity compilation.
- Editor tests cover traversal rejection at the JSON-RPC boundary for representative file and asset methods.
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

Bridge connection settings can be overridden with `NEXUS_UNITY_URL`, `NEXUS_UNITY_PORT`, `NEXUS_UNITY_TIMEOUT_SECONDS`, and `NEXUS_UNITY_AUTH_TOKEN`. A positional port argument is still supported for existing MCP client configs, and invalid port values fall back to the positional port or default port. Bridge logging defaults to `DEBUG` and can be changed with `NEXUS_UNITY_LOG_LEVEL`. Network errors log one line without a traceback, so clients that do not read the bridge's stderr are not blocked by a full pipe.

The bridge is modular:

- `nexus_unity_bridge.py`: MCP loop and CLI entry point.
- `nexus_bridge/_transport.py`: Unity JSON-RPC URL, port, timeout, and HTTP transport helpers.
- `nexus_bridge/client.py`: HTTP JSON-RPC client.
- `nexus_bridge/routing.py` with `routes_*.py`: manager routing and macro behavior.
- `nexus_bridge/schemas.py` with `schemas_*.py`: public MCP tool schemas and static read-only MCP resources served through `resources/list` and `resources/read`.

When deploying from the Unity UI, Nexus Unity copies both `nexus_unity_bridge.py` and the full `nexus_bridge/` package to the project root so the bridge can run from a stable path.

## Public API Surfaces

Raw HTTP JSON-RPC:

- Use unprefixed method names, for example `get_server_status`, `list_tools`, `read_logs`, and `write_file`.
- Intended for diagnostics, custom integrations, and direct automation.
- The complete raw catalog is returned by `list_tools`.
- Runtime schemas intentionally include compatibility fields where needed; for example `update_component` accepts both `properties` and the legacy `json_data` payload, `create_scene` accepts `path` / `open_if_exists`, `create_primitive` accepts transform/material fields, and `create_material` accepts an optional explicit asset `path` plus visible color fields.
- Agent diagnostics include `get_test_results` for Unity `TestResults*.xml` summaries with scoped non-passing test messages, `get_tool_usage_stats` / `reset_tool_usage_stats` for scoped in-memory call counters and timing, and `ui_get_window_rect` / `ui_set_window_rect` / `ui_capture_window_snapshot` for resize-based editor UI QA. `capture_game_view_screenshot` and `capture_inspector_screenshot` return structured image data with dimensions, base64 image bytes, and capture duration while preserving legacy successful fields. Game View defaults to PNG and optionally accepts JPEG, quality, and downscale parameters.
- Experimental canonical commands `nexus.project_map`, `nexus.group_compile_errors`, and `nexus.capture_game_view` are available over Legacy HTTP and return one envelope (`ok`, `data`, `error`, `meta`). Capture defaults to JPEG at a 768 px longest edge, can crop to an object, and can frame a Scene View on a target. `nexus_look` is an unlisted alias. If `com.unity.pipeline` is installed, the same handlers are also registered as optional `[CliCommand]`s for `unity mcp` / `unity command`, with the same parameters and defaults as kebab-case flags (`--max-logs`, `--encoding`), and return that same envelope. On Pipeline 1.0.0+ they are grouped (`unity command nexus capture_game_view`) and read-only; capture uses `--encoding` because Pipeline reserves `--format`. Unlike Pipeline 1.0.0 `capture game_view`, Nexus never opens a Game View window: with none open, `nexus.capture_game_view` returns a retryable `unavailable` error so the user's window layout is not changed. Pipeline remains optional; Nexus compiles and runs without it. Legacy raw tools keep their existing JSON.
- **Support matrix:** Unity 6000.0+. With a healthy `com.unity.pipeline` session, runtime `auto` prefers Pipeline / persistent `unity mcp`. Without Pipeline, Legacy HTTP/MCP on loopback is the compatibility backend. `com.unity.pipeline` is not a package dependency. Runtime mode (`legacy` / `pipeline` / `auto`) is in Project Settings > Nexus Unity. Set `legacy` to force HTTP. Explicit `pipeline` does not fall back to HTTP. Legacy HTTP remains fully supported until Unity CLI 1.0 stable, non-experimental Pipeline, and one Nexus stable release.
- Legacy HTTP sunset: Legacy remains fully supported. It is not fully deprecated while Unity CLI is pre-release and `com.unity.pipeline` is experimental. Removal is not scheduled until Unity CLI 1.0 stable, non-experimental Pipeline, and one Nexus stable release after Pipeline-primary `auto`.
- `batch_execute` runs requests serially, accepts at most 50 requests, and rejects nested `batch_execute` calls. Asynchronous capture methods must be called directly, not inside a batch.

MCP bridge:

- Uses `unity_` prefixed manager tools, for example `unity_scene_manager`, `unity_asset_manager`, and `unity_write_and_compile`.
- Intended for AI clients and day-to-day automation.
- Reduces schema size while preserving access to the supported raw capabilities.
- Manager tools accept common raw-action aliases where agents naturally try them, for example `unity_scene_manager action=list_scenes` and `unity_hierarchy_manager action=create_gameobject`.
- Invalid manager actions include the valid action names in the error message.
- `unity_hierarchy_manager` can create primitives with name, parent, transform, and material path; it can also rename, set transforms, and pass through `create_hierarchy`.
- Raw `run_tests` returns `Submitted` when Unity accepts the asynchronous request; it does not confirm that execution has begun or completed. `unity_editor_controller` `run_tests_wait` waits in the Python bridge by polling raw `get_test_results` instead of blocking the Unity main thread. Polling rides out short connection drops while tests restart the server, and a timeout reports the last connection error as `last_error`.
- `unity_editor_controller` also exposes `get_tool_usage_stats` and `reset_tool_usage_stats` so diagnostics are not split between raw and manager workflows.
- `unity_ui_automation` passes through `deep`, `max_depth`, and `max_elements` for UI hierarchy reads, `class_name`, `max_depth`, and `max_results` for queries, and window rect/snapshot actions for layout checks.

## Current Compatibility Decisions

Version 1.6.2 preserves backward compatibility while correcting public contracts found during API stress validation:

- `unity_write_and_compile` is the supported public MCP macro for write, wait, and compiler-feedback workflows. Any old documentation wording that says `unity_apply_code_change` should be treated as stale.
- `update_component` should use a `properties` object for new callers; `json_data` remains accepted for existing callers.
- `unity_scene_manager` aliases such as `create_scene`, `open_scene`, `save_scene`, and `list_scenes` preserve action-specific required parameters.
- `create_scene` accepts optional `path` and `open_if_exists` for deterministic scene assets.
- `create_primitive` accepts optional `name`, `parent_id`, `position`, `rotation`, `scale`, and `material_path`.
- Vector3 fields accept either `{ "x": 0, "y": 1, "z": 0 }` or `[0, 1, 0]`.
- `set_transform` accepts `position`, `rotation` / `eulerAngles`, and `scale` / `localScale`.
- `get_game_object` includes transform state and a compact component list for cheap write verification; use `inspect_component` for full component properties.
- Script writes require `confirm: true`, then readiness probes remain busy while the scheduled asset refresh is pending so agents do not race Unity domain reload. A write during an in-flight compile stays busy until a later compile reloads. On macOS that refresh waits for Editor focus, or 15 seconds.
- Play Mode transitions keep readiness probes busy until Unity finishes entering or exiting Play Mode.
- `create_material` accepts an optional explicit `path` plus `base_color` / `color` and `emission_color`, so automation can isolate generated materials in a known project folder and make them visibly distinct.
- `write_file` and `write_files_batch` create missing parent directories after path validation; `.cs` writes require `confirm: true`.
- `invoke_method.arguments` is documented and exposed as a JSON array schema.
- `click_object_in_game` accepts either `instance_id` or hierarchy `path`.
- Unity 6.x compatibility is maintained across editor object identity API changes. External JSON payloads continue to use integer-shaped `instance_id` values, while Nexus maps them to `EntityId` internally and falls back to Unity 6.3-compatible hierarchy event and object-reference instance ID APIs when Unity 6.4+ members are unavailable.
- `get_test_results`, scoped tool usage stats, and editor-window rect/snapshot automation are public raw diagnostics for agent-driven development.

## Recommended AI Workflow

Use `unity_write_and_compile` with `confirm: true` for code edits. It writes one or more files, waits for Unity reload/import work, and returns compiler errors from the Unity Console. Use `unity_wait` when a workflow needs to block until compilation, import, play mode, or editor idle state is reached.

For scene work, prefer manager tools:

- `unity_scene_manager`
- `unity_hierarchy_manager`
- `unity_component_manager`
- `unity_search_manager`
- `unity_asset_manager`
- `unity_editor_controller`
- `unity_ui_automation`

## Contributor Validation

Pull requests are checked by one GitHub check, `CI Scope`, posted by the CI Scope GitHub App; the checks run on a maintainer-owned Mac. Rules, limits and exceptions live on the CI Scope website (ci.forkhorizon.com) and the pinned gates in [ForkHorizon/ci-gates](https://github.com/ForkHorizon/ci-gates); this repository carries no CI workflow, gate config or linter settings. Unity compile and EditMode tests are not part of this check: run `scripts/prepush-validate.sh --integration` and the Unity EditMode suite locally before merging.

Contributor pull requests target `development`. The `main` branch is release-only and should receive changes only when maintainers prepare or publish a release.

Commit authors should use an email address verified on their GitHub account, preferably the GitHub no-reply address shown in account email settings. GitHub contributor statistics depend on account-linked commit identities, so commits made with placeholder or unverified emails may not be attributed even when the pull request is merged. The repository `.mailmap` records known historical identity mappings for local Git reporting; future commits should still be made with the correct verified email.

Full local integration validation is explicit:

```bash
bash scripts/prepush-validate.sh --integration
```

Integration validation requires the Unity project to be open and the Nexus Unity server to be running on `http://127.0.0.1:8081/`.

For a focused agent tooling check, run `python3 scripts/agent-tooling-smoke.py`. It exercises server status, raw catalog discovery, scoped usage stats, Nexus Unity window UI automation, snapshot capture, 20 consecutive Game View and Inspector PNG captures, and bridge-side `run_tests_wait` without writing repo-tracked files.

Before release, maintainers should run a public API stress audit that compares raw `list_tools` output with the MCP bridge catalog, exercises read-only and mutating tool groups in a disposable namespace, and verifies cleanup of generated assets and PlayerPrefs keys.

PlayerPrefs cleanup should use specific disposable keys. Bulk cleanup requires `key: "all"` plus `confirm: true` because Unity does not provide an Undo path for `PlayerPrefs.DeleteAll()`.

## Troubleshooting

- Server does not start: change the port in project settings and restart the server window.
- Client reports `Unity Server unreachable ... Nothing is listening`: the Nexus server is stopped. Open `Window > Nexus Unity` and click `Start Server`. Stopping the server, from the window or from code, also turns off auto-start for later editor sessions.
- Client gets HTTP 401 or talks to the wrong project: remove any global `nexus-unity` entry (for example in the Claude Desktop config) that points to another project's bridge or holds an old token. Use the project-scoped `.mcp.json` from Auto Setup; the bridge reads the current token from the project `Library` folder.
- MCP client cannot import `nexus_bridge`: redeploy the bridge from `Window > Nexus Unity > Server`, or point the client directly at the package path.
- Tool returns `Method not found`: compare the raw Unity catalog from `list_tools` with the MCP catalog in `API_REFERENCE.MD`.
- File write denied: ensure the target path resolves inside the Unity project root.
- Compile loop times out: call `get_server_status`, `read_logs`, and `unity_wait` with `editor_idle`.
- Auto Setup reports success but the client does not connect: confirm you used the card for the client you actually run (Claude Code reads `.mcp.json`, not the Claude Desktop config), then reload the client (`/mcp` in Claude Code) after starting the Nexus server.
- Bridge fails to launch on Windows: verify a Python 3 interpreter is on `PATH` (`python --version` or `py --version`); the generated config uses `python3`, `python`, or `py` in that order.
- Gemini card shows `Not found` or disabled Auto Setup: the CLI is not on the Editor's `PATH`. Install it and ensure `where gemini` (Windows) or `which gemini` (macOS/Linux) resolves, or use `Copy Config`.

## Packaging Rules

- Public repo: `https://github.com/ForkHorizon/NexusUnity.git`.
- Package id: `com.forkhorizon.nexus.unity`.
- Latest public release: `1.6.2`.
- License: `MIT`.
- Required release docs: `SECURITY.md`, `CONTRIBUTING.md`, and `RELEASE.md`.
- Repository funding metadata lives in `.github/FUNDING.yml` and configures the GitHub Sponsor button for `Daliys`.
- Do not ship generated caches, local agent folders, `.jules/`, `.DS_Store`, Python bytecode, or native bridge binaries without corresponding source and build instructions.
- Package EditMode tests live in `Tests/Editor` with an asmdef guarded by `UNITY_INCLUDE_TESTS`, so consumers compile them only when the package is listed in `testables`. Keep any other contributor tooling under Unity-ignored `~` folders without `.meta` files; Unity does not import those folders, and root `~.meta` entries break immutable PackageCache installs.
- Do not declare Unity Project Auditor packages as Nexus dependencies. Nexus audit support uses reflection when a host project explicitly installs compatible Project Auditor rules; direct dependencies can add duplicate immutable `.meta` GUID warnings or no-module Console spam during clean installs.

## Development Versioning Policy

Nexus Unity follows semantic versioning for public releases, but the development branch should not bump the package version for every merged fix. Keep `package.json` and visible docs at the latest shipped public version until a release is being prepared.

Unity Package Manager requires `MAJOR.MINOR.PATCH` values in `package.json`, and GitHub release tags and titles use the same semantic version. The latest released tag is `v1.6.2`.

During normal development:

- Add all user-visible API, behavior, docs, and validation changes to `[Unreleased]` in `CHANGELOG.md`.
- Keep `package.json` and visible docs on the latest shipped version until a release is prepared.
- Prefer compatibility fixes over breaking changes; if a breaking change is unavoidable, document the migration path before release.

During release preparation:

- Choose the next semantic version based on accumulated changes.
- Move `[Unreleased]` entries into the new dated release section.
- Update `package.json`, README badges/install examples, `DOCUMENTATION.MD`, and `API_REFERENCE.MD`.
- Tag the release with the matching semantic GitHub version, for example `v1.6.2` for package version `1.6.2`.
