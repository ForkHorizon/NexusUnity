# Changelog - Nexus Unity

All notable public changes to Nexus Unity are documented here.

## [Unreleased]

### Added
- `nexus.asset_impact` (alias `nexus_asset_impact`, CLI `unity command nexus asset_impact`, read-only): static C# <-> Unity YAML impact over every text prefab, Prefab Variant, scene, ScriptableObject and AnimationClip, loaded or not. `find_references` lists each stored value of a field per asset and object (component values, variant / nested prefab / scene-instance overrides, curve bindings) against the C# default; `analyze_change_impact` reports what a field rename or type change, method rename, script delete or type move breaks, with `[FormerlySerializedAs]` hints; `validate_asset_integrity` finds Missing Scripts, dangling guids and broken UnityEvent bindings that `unity vcs hooks` does not. Binary and unparsable assets are listed as `skipped` / `unknown`; `Error` is only reported with `exact` confidence.
- `nexus.asset_impact` `level: "editor"` (CLI `--level editor`) adds an `editor` section with what Unity actually loads (Layer B): effective field values per prefab, Prefab Variant and nested instance tagged `component`, `prefab_modification` or `inherited`; UnityEvent bindings checked against the target's real type; AnimationClip curve bindings; and Editor-side integrity findings (`missing_script`, `broken_unityevent`, `serialize_reference_missing_type`, `missing_animated_property`). It never opens a scene or marks anything dirty: scenes that are not loaded are listed under `editor.unknown` as `scene_not_loaded_for_editor_validation`, and while the Editor compiles or reloads `editor.available` is `false`. `level: "auto"` runs that check only for assets the static scan left `unknown` or matched without `exact` confidence. The default `level: "static"` is unchanged. MCP: `unity_search_manager` (`references`, `impact`) and `unity_lint_project` (`scope: "assets"`) forward `level`.
- `nexus.asset_impact` takes `results` (CLI `--results`): `auto` (default), `summary` or `full`. Results with more than 50 entries now come back as a summary by default: each list keeps its first 10 entries, and a new `<list>_by_asset` lists every asset with its entry count, evidence lines and values. `unique_values` keep 3 locations each and the one-line `summary` stops at 300 characters. On a 300-asset project this cuts `analyze_change_impact rename_field` from 107 KB to 28 KB and `find_references` from 83 KB to 36 KB; pass `results: "full"` for every entry. Results with 50 entries or fewer are unchanged apart from the new `results: "full"` marker. MCP: `unity_search_manager` and `unity_lint_project` forward `results`.
- `nexus.asset_impact` field queries follow `[FormerlySerializedAs]`: after a rename, `find_references` on the new name returns values still stored under the old name, marked `serialized_as`, and `csharp_default` resolves when attributes sit on the declaration line.
- Raw `find_references` accepts `symbol: {kind, type, name}` and then returns the `nexus.asset_impact` envelope. The target mode is unchanged.
- MCP bridge: `unity_search_manager` `references` accepts `symbol` (or `kind`/`type`/`name`) for the static scan, and the new `impact` strategy runs `analyze_change_impact`; `unity_lint_project` takes `scope: "assets"` for the asset integrity check.

### Changed
- **Breaking (Unity CLI):** with `com.unity.pipeline` 1.0.0 or newer, the Nexus Pipeline commands move into the `nexus` group: `unity command nexus project_map`, `unity command nexus group_compile_errors`, `unity command nexus capture_game_view`. The global `nexus_*` names remain on older Pipeline versions. HTTP and MCP names are unchanged.
- **Breaking (Unity CLI):** Pipeline CLI flags are kebab-case on every Pipeline version: `--max-logs`, `--max-dimension`, `--instance-id`, `--request-id`. HTTP/MCP keys stay snake_case.
- `nexus.capture_game_view` takes the image encoding as `encoding` (`jpg` or `png`). HTTP still accepts the deprecated `format` key when `encoding` is absent, and `max_long_edge` when `max_dimension` is absent. `list_tools` advertises `encoding`.
- On Pipeline 1.0.0 or newer, the Nexus commands declare `CommandEffect.ReadOnly`, so `unity command` and MCP clients see them as read-only.
- `get_server_status.runtime.legacy` no longer reports the stale `1.0.0-beta.10` Unity CLI observation. The `unity_cli_stable` gate passes with `not_probed_in_editor (1.0.x GA released upstream)`, `runtime.unity_cli.prerelease` and `stable_1_0_or_newer` are `null` (the Editor never runs the `unity` executable), and `requires` lists only the remaining prerequisites. Legacy HTTP stays supported and `deprecated` stays `false`.
- **Breaking for large scenes:** `dump_scene_graph` and `compact_scene_snapshot` return at most 200 nodes by default (depth-first). Results add `node_count` and `truncated`, and a `hint` when truncated. Pass `max_nodes: 0` for the previous unlimited output.

### Deprecated
- `unity_dump_scene_graph` is deprecated. Its MCP schema now lists the existing `compact` flag and the new `max_nodes`. It stays available until a bounded `nexus_snapshot` ships.
- The `Integrations` client installers (`MCPCliInstaller`) are frozen and maintenance-only. With `com.unity.pipeline` 1.0.0+, use `unity setup <client>` and `unity mcp configure <client>`.

### Fixed
- `unity_editor_controller` `run_tests_wait` no longer fails with `Unity Server unreachable` partway through a run. The package EditMode tests stop and restart the server, so polling briefly lost the connection and returned that error. The bridge now keeps polling until results appear or `timeout_seconds` ends; a timeout reports the last connection error as `last_error`.
- `com.unity.pipeline` versions with any SemVer pre-release suffix (for example `1.0.0-pre.1`) now count as pre-release. Before, only `exp`, `preview`, `alpha`, and `beta` did.
- Removed the `CS0618` warning for `EditorUtility.InstanceIDToObject` in capture target resolution on Unity 6000.3+.
- `nexus.capture_game_view` rejects an unsupported encoding with `Unsupported capture encoding. Use jpg or png.` instead of `Unsupported capture format.`
- `scripts/stabilize-pipeline-capture.py` and `scripts/stabilize-domain-reload.py` work again: they read the `{ok, data, error, meta}` envelope, use `--encoding` and other named flags, and call the grouped `nexus` / `script recompile` commands on Pipeline 1.0.0+ (shared helper `scripts/_unity_cli.py`).
- Package EditMode `ServerPortTests` failed when another Unity project's Nexus server owned the default port and Pipeline 1.0.0 was the selected runtime (the server correctly stays stopped then). The tests now start the server on their own free loopback port. `Start_WithPort0_AssignsDynamicPort` is renamed `Start_OnFreeLoopbackPort_BindsThatPort`, because the port setting never resolves to 0.
- `unity command ... --encoding png` now switches the capture to PNG. The old `--format png` flag never reached Nexus, because Pipeline 1.0.0 reserves `--format` for its own output format.

## [1.6.2] - 2026-11-17

Hotfix for Claude clients. No API or behavior changes beyond the fix below.

### Fixed
- Claude Code and other Claude clients saw the nine manager tools (`unity_scene_manager`, `unity_hierarchy_manager`, `unity_component_manager`, `unity_search_manager`, `unity_asset_manager`, `unity_editor_controller`, `unity_ui_automation`, `unity_wait`, `unity_playerprefs_manager`) with no parameters, so every call failed with `Invalid action: None`. Their input schemas used `oneOf` at the top level, which Claude's tool API does not accept. `tools/list` now advertises one flat object schema per tool: all parameters, the action key as a required `enum`, and each action's required parameters in its description. Routing and per-action validation are unchanged.

## [1.6.1] - 2026-11-17

This release ships the work previously tracked as the `1.7.0` development line: canonical `nexus.*` commands with one response envelope, Capture V2, optional Unity Pipeline support, reliable Editor readiness, and fixes that keep AI clients connected to the server.

### Upgrade notes
- After updating the package, run `Auto Setup` again for each AI client in `Window > Nexus Unity > Integrations`, then reload the client (`/mcp` in Claude Code). This redeploys the project-root bridge, which older setups copied once and never updated.
- Remove any global `nexus-unity` MCP entry (for example in `claude_desktop_config.json`) that points to another project's bridge. Prefer the project-scoped config that Auto Setup writes.
- `nexus.capture_game_view` returns JPEG at a 768 px longest edge by default. Pass `format: "png"` or `max_dimension: 0` for the previous output. Legacy `capture_game_view_screenshot` is unchanged.
- C# callers of `NexusCommandEnvelope.Failure` must build the error with `NexusCommandError.Create`. The JSON on the wire is unchanged.
- Package EditMode tests moved from `Tests~/Editor` to `Tests/Editor`. Add the package to `testables` in `Packages/manifest.json` to run them.

### Known issues
- With `com.unity.pipeline` 1.0.0, the Pipeline CLI rejects `--format` for `nexus_capture_game_view` before Nexus receives it. Omit the flag (JPEG is the default) or call the command through the MCP bridge or HTTP.
- Stopping the server from `Window > Nexus Unity` also turns off auto-start for later editor sessions. Click `Start Server` again to turn it back on.

### Added
- Canonical Nexus commands `nexus.project_map`, `nexus.group_compile_errors`, and `nexus.capture_game_view` (HTTP aliases `nexus_*`) share one handler and description each. When `com.unity.pipeline` is installed, optional `[CliCommand]` wrappers in `UnityMCP.Editor.Pipeline` project the same handlers; Pipeline is not a package dependency.
- Runtime setting `legacy` / `pipeline` / `auto` (default `auto`). `auto` prefers Unity Pipeline on eligible installs (Unity 6000, Pipeline package, registered Nexus commands, healthy session) and Legacy otherwise. Explicit `legacy` forces HTTP immediately. HTTP remains the fallback and is not removed.
- Published a Legacy HTTP sunset timeline: Legacy remains fully supported. Removal is not scheduled until Unity CLI 1.0 stable, non-experimental `com.unity.pipeline`, and one Nexus stable release after Pipeline-primary `auto`. `get_server_status.runtime.legacy` reports that ledger.

### Changed
- CI moved fully to CI Scope: the repository has no CI workflow, `.ci-scope.json`, `.code-linter.json` or `.ruff.toml`; rules, limits and exceptions live on the CI Scope website and the pinned gates in `ci-gates`. Removed the old in-repo gate machinery: `tools~/NexusQualityGate*` (Roslyn and Ollama review), `.unity-quality-gate.json`, the `.githooks/pre-push` hook, the `Validate package`, `Unity quality gate` and `Approve external PR` workflows, and the `--quality-ai` / `--checklist-ai` modes of `scripts/prepush-validate.sh`. Python uses the ci-gates strict ruff config. Unity compile and EditMode tests are not in the CI check; run them locally.
- C# envelope API: `NexusCommandEnvelope.Failure(NexusCommandError, data, requestId, ms, stackSource)` takes an error built with `NexusCommandError.Create(code, message, retryable, hint, outcome)` instead of nine loose arguments, and `NexusCommandEnvelope.InvalidArgument(message, requestId, ms)` builds the common non-retryable rejection. `CaptureGameViewCommand.ExecuteFromCli` takes one `CaptureCliArguments` object. Wire JSON is unchanged.
- Quality gates check the package code without blanket waivers: Python bridge code passes the ci-gates strict ruff config (blind `except Exception` handlers narrowed or logged), and `Tests/` and `Editor/tests/` are scanned too.
- Python bridge tests run with `unittest discover -s Editor/tests -t Editor` and no longer edit `sys.path` per module.
- `nexus.capture_game_view` (aliases `nexus_look` and `nexus.look`) defaults to JPEG and a 768 px longest edge. Pass `max_dimension` 1024 to opt in, or 0 for the previous unconstrained size. `view:"scene"` frames a target in the Scene View and restores the camera pose. `target` or `instance_id` crops a Game View capture to that object. `delivery:"file"` writes one image under the project `Temp/NexusCapture` directory and omits Base64. The response never includes both PNG and JPEG. Legacy `capture_game_view_screenshot` stays PNG and full-size.
- Pipeline `[CliCommand]` parameters for `nexus_project_map`, `nexus_group_compile_errors`, and `nexus_capture_game_view` are locked to the canonical descriptor: same name, description, JSON type, order, and default. An EditMode test fails if one side adds, removes, or retargets a parameter. `list_tools` schemas are unchanged and still omit CLI default fields. `nexus_look` stays an unlisted alias, not a fourth command.
- Canonical commands `nexus.project_map`, `nexus.group_compile_errors`, and `nexus.capture_game_view` (and their `nexus_*` aliases) return one envelope: `{ok, data, error, meta}`. `meta.request_id` echoes a string `request_id`. `meta.gen` is the editor session generation. `meta.mode` is the effective runtime (`legacy` or `pipeline`). Domain failures use stable codes (`invalid_argument`, `unavailable`, `busy`, `timeout`, `capture_failed`, `partial`, `editor_not_ready`, `internal`) inside the JSON-RPC `result`. A caller-supplied unknown mutation outcome is never retryable. Stack traces are included only when `NEXUS_DEBUG` is set. Pipeline `[CliCommand]`s return that same envelope. `nexus.capture_game_view` still returns `Task` so Pipeline can await GPU completion. Legacy raw responses, including `capture_game_view_screenshot`, are unchanged.
- Package EditMode tests live under a single `Tests/Editor` suite (guarded by `UNITY_INCLUDE_TESTS`, so consumers only compile it when the test framework is installed and the package is listed in their `Packages/manifest.json` `testables`). Historical `Tests~/Editor` was merged into that suite and removed. The test asmdef uses Unity's `overrideReferences` + `Newtonsoft.Json.dll` / `nunit.framework.dll`.
- Runtime `get_server_status` now returns a main-thread-published snapshot. Requested mode is cached so the HTTP listener does not read EditorPrefs off-thread.
- Inspector/window screenshots use `EditorWindowPixelCapture` (ReadPixels). Game View remains Capture V2 DriverOwnedReadback only.
- Extracted Game View capture into a transport-independent Capture V2 gateway (`ICaptureGateway`) using `AsyncGPUReadback.Request` plus `GetData<byte>()` (DriverOwnedReadback). Legacy `capture_game_view_screenshot` keeps its structured PNG JSON schema by default and now accepts optional `format`, `quality` / `jpeg_quality`, `width`, `height`, `max_long_edge`, and `include_telemetry` parameters. Canonical `nexus.capture_game_view` uses that same gateway with a JPEG default matching the hybrid proof of concept.
- Pipeline `[CliCommand]` Game View capture now returns `Task<NexusCaptureResult>` so Unity Pipeline can await GPU completion off the main thread. Production capture never calls `WaitForCompletion` or `.GetResult()` on the Unity main thread.
- `list_tools` can filter by profile (`core`, `visual`, `scene`, `compat`). Canonical ids are advertised once; transport aliases remain dispatchable.
- Pipeline health probing validates session PID liveness and Unity-like process identity, caches last-healthy state across domain reload, and reports `runtime.unity_cli` / `runtime.pipeline.detected|experimental|supported` without shelling out to the Unity CLI.

### Fixed
- Running the package EditMode tests no longer leaves the editor without a Nexus server. `ServerPortTests` stops the live server, and `MCPServer.Stop()` also turns off auto-start, so after a test run nothing listened on 8081 until someone clicked `Start Server`. A test setup fixture now restarts the server if it was running before the tests.
- The bridge finds `Library/NexusUnityAuthToken.txt` from any working directory by also searching the folders above the bridge script, so a client that starts the bridge outside the project root no longer gets HTTP 401 with a stale configured token.
- The bridge no longer writes a full traceback to stderr for every failed call. Readiness polling during a domain reload hits network errors twice a second, and the traceback flood filled the stderr pipe of a client that does not read it, which blocked the bridge inside a call (`apply_code_change` hung for 35 minutes in the live harness). Network errors now log one line; unexpected errors keep the traceback.
- When nothing listens on the server port, the bridge error now names the URL and says to open the project and click `Start Server` in `Window > Nexus Unity`, instead of only `Unity Server unreachable`.
- `unity_component_manager` `set_property` sent `property_name: "m_Name"` for every call, renaming the object instead of setting the requested property. It now forwards `property_name` and `value`.
- `nexus.capture_game_view` with an unsupported `format` (for example `bmp`) returned a JSON-RPC `-32000` error. It now returns an `invalid_argument` envelope and does not capture.
- Optional Pipeline commands compile on `com.unity.pipeline` 0.7 and 0.8. 0.8 moved `CliCommandAttribute` and `CliArgAttribute` into assembly `Unity.Pipeline.Attributes` without a type forwarder. `UnityMCP.Editor.Pipeline` references that assembly beside `Unity.Pipeline`, which still contains the attributes on 0.7.
- A script write that arrives while another compile is in flight stays not-ready through that older reload, then refreshes again. Play Mode transition frames do not count as quiet ticks. On macOS, script refresh waits until the Editor is focused or 15 seconds have passed. Failed-compile detection reads `scriptCompilationFailed` on the following editor tick.
- Editor readiness now uses one predicate for `get_server_status`, `initialize`, `wait_for_ready`, and idle waits. `compile_epoch` and `reload_epoch` persist in `SessionState` across domain reload. A script write that compiles successfully stays not-ready until `reload_epoch` advances, including the gap after compilation finishes and before the domain reload. A failed compile settles only after the editor is quiet. Pending asset refresh and Play Mode transitions stay not-ready until that cycle finishes. `wait_for_editor_idle` returns `ready`, `epoch`, and `reason`, and returns `Reloading` instead of blocking through a domain reload. `unity_wait` compilation re-polls `is_editor_idle` after a dropped connection and does not treat an early idle flag, or a missing epoch, as success. Legacy `status` and `is_idle` fields remain. `wait_for_ready` still returns a boolean.
- Security: unauthenticated HTTP requests could bypass auth by containing `get_server_status` or `shutdown_server` anywhere in the body; only the actual JSON-RPC `method` is exempt now.
- Capture: readback timeouts no longer busy-loop or poll forever, encode failures no longer wedge the capture gate, the Game View wait resumes on the main thread, and capture size is capped at 8192 per edge.
- Runtime: Pipeline health probe retries and re-runs when the runtime mode changes; the Unity version gate accepts 6000 and newer.
- `batch_execute` reports async capture methods with a clear error; `find_objects` also matches the literal name; `list_tools` accepts array params.
- Game View, Inspector, and UI window screenshots use Unity in-engine render textures and VisualElement captures instead of macOS `screencapture`. Targeted Inspector captures (`instance_id`) rebuild the editor tracker first, MSAA render textures are resolved, and the screen-pixel fallback is skipped while Unity is not the active application, so the desktop behind it is never read.

## [1.6.0] - 2026-08-23
### Security
- Sanitize and redact sensitive exception messages in tool usage stats (`get_tool_usage_stats` and `RecordToolUsage`), replacing absolute filesystem paths, user directories, and multiline frames with generic placeholders and safe truncated summaries, and exposing `last_error_type` (#142).
- Require explicit confirmation (`confirm: true`) for `delete_asset`, enforce `AssetDatabase.MoveAssetToTrash` for OS trash recovery, and block creation, modification, moving, or deletion of `ProjectSettings/` and `Packages/` paths across all asset tools (#140).
- Hardened external PR replay workflow (`approve-external-pr.yml`) with an in-workflow actor authorization gate and a PR content security pre-scan to prevent unauthorized execution and flag critical CI modifications on self-hosted runners (#144).
- The content pre-scan now blocks the replay when a PR touches `.github/workflows/*`, `.githooks/*`, or `scripts/*`, requiring the maintainer to re-run with `acknowledge_critical_files: true` after reviewing the diff, instead of only logging a warning (#144).

### Changed
- Consolidated duplicate internal Ollama-review and serialized-property write helpers.

### Fixed
- Add traversal depth and element/result bounds to UI Toolkit hierarchy serialization and element queries (`ui_get_hierarchy`, `ui_query_elements`, and `ui_capture_window_snapshot`), preventing editor stalls and payload bloat on complex UI windows, with `children_truncated` and `truncated` markers on partial hierarchy responses (#126).
- Dispatch WebSocket request handling asynchronously in `Task.Run`, ensuring `ServerLoop` remains non-blocking for concurrent HTTP JSON-RPC requests and new connections (#129).
- `EditorApplication.update` subscription for `HandlePostCompileFocusReturn` is now idempotent (unsubscribes before subscribing), preventing stacked duplicate frame handlers on repeated initialization or domain reloads (#138).
- `ListPlayerPrefs` now uses `ProcessStartInfo.ArgumentList` and timeout guards for macOS `defaults read` execution, preventing process hangs, alongside regex unhashing for Windows registry keys, Linux XML prefs support, and double-default type verification (#143).
- Persist authentication tokens across Unity Editor process restarts in Library token file, preventing persistent HTTP 401 Unauthorized errors for external MCP CLI integrations after restarting Unity (#166).
- `get_scene_dependencies` now uses `enterChildren = false` after the initial property iteration step, preventing deep recursive traversal into child properties and eliminating duplicated dependency references (#71).
- `find_objects` now safely constructs name search regexes with a 100ms match timeout and falls back gracefully to literal substring search on invalid regex patterns or match timeouts (#119).
- Restrict type resolution (`FindType`) to user project assemblies and standard public `UnityEngine` assemblies, disallow internal system/editor assemblies and namespaces, and enforce strict type allowlist constraints for component and ScriptableObject creation/inspection tools (#139).
- Resolve symlinks and directory junctions to their real filesystem targets in `ValidatePath` before checking project boundaries to prevent path traversal.
- Reject abstract ScriptableObject types before calling `CreateInstance` in `create_scriptable_object_asset` and `list_fields_for_type`.
- `TriggerSafeAssetRefresh` callback is now tracked to prevent callback leaks, duplicate refresh hooks, and memory leaks across assembly reload/shutdown boundaries, with a fail-safe 15-second timeout guard.
- `run_tests` now reports `Submitted` rather than `Success` after Unity accepts its asynchronous request, and `run_tests_wait` now returns rejected submissions immediately instead of polling until timeout.
- Auto Setup now reports a clear refresh-and-retry message if a client is unavailable after the integration list is regenerated, instead of throwing an exception.

## [1.5.0] - 2026-07-12

### Changed
- Relicensed Nexus Unity from `GPL-3.0-only` to the `MIT` license to remove copyleft friction for commercial Unity studios. All prior contributors consented to the relicense.

### Fixed
- Updated Claude Code and Gemini CLI setup commands for their current option ordering and explicit project-scoped stdio registration; Gemini setup no longer bypasses tool approvals with `--trust`.
- Antigravity setup now writes its workspace configuration to `.agents/mcp_config.json` instead of the legacy global Gemini config path.
- Cline setup now writes its shared MCP settings to `~/.cline/data/settings/cline_mcp_settings.json` instead of the retired VS Code extension storage path.
- Claude Code setup now skips the CLI add command when removal of a stale project registration fails, logs the actionable failure, and falls back directly to `.mcp.json`.
- The local HTTP/WebSocket control plane now requires a per-session auth token before JSON-RPC dispatch; generated MCP configs pass the token through the Python bridge.
- `add_component` now returns a clear `GameObject not found` error for stale instance IDs instead of throwing a raw Unity null reference.
- `batch_execute` now caps batches at 50 requests and rejects nested batch execution.
- `create_primitive` now validates parent, transform, and material inputs before creating the GameObject, and Vector3 inputs accept `[x, y, z]` arrays as well as `{x, y, z}` objects.
- `get_game_object` now returns transform state and a compact component list so agents can verify basic write operations with the cheap read-back call.
- Script writes now keep readiness probes in a busy/importing state while the scheduled asset refresh is pending, avoiding premature follow-up write calls during Unity domain reload.
- Script-writing RPC methods now require `confirm: true` before creating or overwriting `.cs` files and triggering Unity compilation.
- MCP bridge static resources can now be read through `resources/read` after discovery through `resources/list`.
- Play mode transitions now keep readiness probes busy so agents do not issue follow-up writes while Unity is still entering or exiting Play Mode.
- `delete_player_pref` now rejects missing or empty keys, and only clears all PlayerPrefs when called with `key: "all"` and `confirm: true`.
- Invalid `NEXUS_UNITY_TIMEOUT_SECONDS` values now fall back to the default bridge timeout instead of crashing the Python bridge at import time.
- Python bridge type helpers no longer require Python 3.11-only typing features.
- Path security tests now exercise JSON-RPC traversal rejection for representative file and asset tools.
- `get_test_results` now reads messages only from scoped NUnit result nodes and falls back to the test result instead of unrelated nested messages.
- `unity_scene_manager` schema aliases now preserve action-specific required parameters.
- Fast-path health JSON-RPC methods now use cached editor state instead of direct Unity API reads on the listener thread.
- MCP CLI installers now pass executable paths and project paths as process arguments instead of shell command strings.

## [1.4.2] - 2026-06-13

### Added
- Added Python unit coverage for MCP bridge schema shape and type expectations.

### Changed
- Expanded the `unity_hierarchy_manager` MCP schema into per-action shapes with clearer required parameters and aliases.
- Refined Python MCP bridge routing with shared JSON-RPC payload types and more structured handler code.
- Added a repository mailmap entry for `air17` so local Git contributor reports resolve historical `air17@github.com` commits to the GitHub account's canonical no-reply identity.

### Fixed
- Removed direct Unity Project Auditor package dependencies and made Unity Project Auditor execution optional, so clean installs avoid duplicate immutable `.meta` GUID warnings and `Could not find any registered modules` Console spam.
- Fixed bridge-side compilation wait timeout handling after asset refresh and tightened test-result polling payload handling.

## [1.4.1] - 2026-06-12

### Fixed
- Added the missing folder `.meta` files for `docs/`, `docs/assets/`, and `Editor/tests/`. These folders were published without their folder-level metas, so a fresh package install logged "no meta file ... will be ignored" warnings and skipped the folders.
- Hardened the pre-push `.meta` pairing check to validate the git-tracked tree instead of the on-disk working directory (Unity auto-generates metas locally, which masked the missing committed metas) and to require a tracked `.meta` for every imported folder, not just files.

## [1.4.0] - 2026-06-11

### Added
- Claude Code integration card that registers `nexus-unity` in a project-root `.mcp.json`, using the `claude` CLI at project scope when available and falling back to writing the file directly. This is separate from the existing Claude Desktop card.
- Dedicated Python bridge unit tests for routing, compilation/test-result polling helpers, transport config, and JSON-RPC payload construction.
- `Python unit tests` GitHub Actions job that runs the bridge unit test suite after static validation.
- Maintainer-dispatched external PR replay workflow that can copy a reviewed fork PR to a trusted `trusted/pr-N` branch for CI reruns.

### Changed
- Python MCP bridge internals now split logging and HTTP transport helpers into `nexus_bridge/_logging.py` and `nexus_bridge/_transport.py`, with stronger type hints, module documentation, and reusable transport configuration.
- Bridge CLI mode now uses `argparse`, validates `key=value` arguments, and parses JSON-looking values before routing direct tool calls.
- MCP bridge tool schemas now include clearer per-action descriptions and static resource metadata for `resources/list`.
- `Validate package` now warns for external fork pull requests and continues the CI pipeline instead of blocking before checkout; maintainers can still use the trusted-branch replay workflow when they want an internal CI branch.
- Local static/pre-push validation now runs the Python bridge unit test suite after compiling the bridge.
- The bridge log level is configurable with `NEXUS_UNITY_LOG_LEVEL`.

### Fixed
- Claude Desktop auto setup on macOS now writes `~/Library/Application Support/Claude/claude_desktop_config.json` instead of the XDG `~/.config/Claude` path, which the macOS app does not read (`SpecialFolder.ApplicationData` resolves to `~/.config` under Mono).
- CLI client detection (Codex, Gemini, Antigravity, Claude Code) now resolves executables with `where` on Windows instead of always failing, which previously left Gemini and Antigravity `Auto Setup` disabled on Windows even when installed.
- Generated MCP configs resolve the Python interpreter as `python3`, then `python`, then the Windows `py` launcher, instead of hardcoding `python3`, so the bridge launches on Windows hosts without a `python3` alias.
- Invalid `NEXUS_UNITY_PORT` values are ignored cleanly so the bridge can fall back to a positional port argument or the default port.

## [1.3.0] - 2026-06-05

### Added
- Integration cards now detect stale project-root MCP bridge deployments and client configs that point at a different Unity project.

### Changed
- The Server tab bridge summary now shows the deployed bridge version and flags package/deployed bridge mismatches.
- Integration setup success messages now explicitly tell users to restart the affected MCP client session after redeploying the bridge.

### Fixed
- Unity 6.3 editor compilation now works again by guarding Unity 6.4-only `EntityId` hierarchy event and serialized object-reference APIs behind 6.4+ fallbacks.

## [1.2.0] - 2026-05-31

### Added
- GitHub funding metadata now configures the repository Sponsor button for `Daliys`.
- MCP manager aliases for common raw-style action names such as `list_scenes`, `create_scene`, `create_gameobject`, `rename`, and tool-usage diagnostics.
- Scene-building manager support for primitive name, parent, transform, material path, object rename, transform updates, and `create_hierarchy` passthrough.
- Material creation support for visible base color and emission color fields.
- Local-Ollama pull request checklist review through `NexusQualityGate --checklist-ai`, with one AI verdict per checklist item and explicit next-action output for failures.

### Changed
- `Validate package` now runs entirely on the maintainer self-hosted Mac runner with the `nexus-unity-ci` label instead of GitHub-hosted runners.
- CI now uses workflow-level `concurrency.queue: max` so trusted runs queue on the local runner instead of being canceled or running in parallel.
- Public contribution docs now describe the trusted-branch replay policy for external fork pull requests before full local CI can execute candidate code.
- Unity package validation now performs a local package smoke import with Unity `6000.4.3f1` instead of the previous optional GitHub-hosted EditMode test job.
- Unity package smoke now generates a dedicated EditMode smoke test assembly in the temporary project so package resolution and editor bridge compilation are verified during CI.
- Manager invalid-action errors now include valid action names to make AI recovery deterministic.
- `create_scene` can create or reopen a scene directly at a provided asset path.
- `set_transform` now updates rotation and scale as well as position.
- The required `Documentation quality AI` CI job now validates both XML documentation quality and pull request checklist evidence while preserving the existing required status-check name.

### Fixed
- `write_file` and `write_files_batch` now create missing parent directories after path validation.

## [1.1.2] - 2026-05-23

### Fixed
- Fixed clean Unity Package Manager installs by moving package-internal Unity test sources into `Tests~/` so user projects do not compile NUnit-dependent test files during normal package import.
- Added the required `com.unity.inputsystem` package dependency for input simulation tools that compile against `UnityEngine.InputSystem`.

### Changed
- Packaging docs now treat all Unity-ignored `~` folders as no-`.meta` areas, including `tools~/` and `Tests~/`.
- Optional agent tooling smoke now validates live UI/tool routing directly instead of depending on package-internal Unity test sources.
- README release notes now direct users from `v1.1.0` and `v1.1.1` to the superseding `#v1.1.2` install pin.

## [1.1.1] - 2026-05-23

### Fixed
- Fixed Unity Package Manager installs by adding an auto-referenced `UnityMCP.Runtime` assembly definition for runtime APIs used by editor code.
- Removed Unity `.meta` files from the ignored `tools~/` validation tooling folder so immutable PackageCache imports no longer report orphan `tools~.meta` assets.

### Changed
- Package validation now ignores Unity-ignored `~` folders for `.meta` pairing and fails if `.meta` files under those folders are tracked again.
- README release notes now tell `v1.1.0` users to update pinned installs to `#v1.1.1` and clear the stale PackageCache entry if Unity keeps old import errors.

## [1.1.0] - 2026-05-23

### Added
- Roslyn-based `NexusQualityGate` for contributor CI, covering XML documentation quality, file size limits, method size warnings/failures, and local test coverage.
- Required self-hosted Ollama documentation review workflow using the `nexus-doc-ai` runner label and serialized GitHub Actions concurrency.
- GitHub issue templates, pull request template, static validation workflow, and code of conduct for public community maintenance.
- README badges and reproducible install URL pinned to `v1.1.0`.
- A single `Window > Nexus Unity` menu entry that opens the main Nexus Unity window.
- Tracked optional fast pre-push hook and installer for contributor local validation.
- Development versioning policy: keep unreleased development on the latest public package version and record user-visible work under `[Unreleased]` until release preparation.
- Public API stress-audit documentation covering raw `list_tools` validation, MCP bridge catalog validation, disposable mutation namespaces, and cleanup expectations.
- Contributor branch policy documentation: public pull requests target `development`; `main` is release-only.
- Pull request target policy workflow for enforcing contributor PRs to `development` and release PRs to `main`.
- UI Toolkit regression tests for the Nexus Unity editor windows.
- MCP integration config generator for Codex TOML, JSON-style MCP clients, VS Code-compatible `servers` config, Cursor, Windsurf, Claude Desktop, and generic copy/paste setup.
- Editor tests for integration config generation and responsive integration hub controls.
- Raw agent diagnostics: `get_test_results`, `get_tool_usage_stats`, `ui_get_window_rect`, and `ui_set_window_rect`.
- Bridge actions for `unity_editor_controller` test polling/server state/asset refresh and `unity_ui_automation` window rect/layout QA.
- Window snapshot diagnostics through raw `ui_capture_window_snapshot` and `unity_ui_automation` `capture_window_snapshot`.
- Scoped tool usage reset via raw `reset_tool_usage_stats`.
- Optional `scripts/agent-tooling-smoke.py` for focused local agent tooling verification.
- Console logging settings for Nexus Unity service messages, available in the main `Settings` tab and `Edit > Project Settings > Nexus Unity`.

### Changed
- Public XML documentation now describes the Unity Editor, filesystem, process, serialization, and UI side effects that the AI review gate expects on editor-facing APIs.
- The AI documentation review now uses Ollama JSON mode and a pragmatic rubric cache version so stale or over-strict model verdicts are not reused after rubric updates.
- AI documentation validation now passes a short Ollama keep-alive and explicitly unloads the local model after each review to avoid leaving large models resident between CI jobs.
- Static package validation now runs the deterministic Nexus quality gate and its local test harness.
- Contributor docs now define useful XML documentation expectations and local commands for deterministic and AI-backed documentation review.
- Consolidated API verification, project audit, test window, and Codex link test actions into the main Nexus Unity window instead of exposing separate Unity submenu entries.
- The package validation workflow now reuses the local static validator and includes an optional Unity EditMode test job when a Unity license secret is configured.
- The local pre-push hook now runs a sub-minute quick gate by default; full Python integration validation is opt-in with `scripts/prepush-validate.sh --integration`.
- Validation now runs on both `main` and `development` and documents that direct pushes to protected branches are blocked for everyone.
- Rebuilt Nexus Unity editor windows with compact UI Toolkit layouts, responsive wrapping, minimum usable bounds, and named automation elements.
- Reworked the main Nexus Unity window around user-facing `Server`, `Integrations`, and `Resources` tabs; test/internal actions now live under collapsed `Advanced / Diagnostics`.
- Integration setup is now card-based with status, auto setup, copy config, and config-location actions instead of a row of ambiguous CLI buttons.
- Wide editor layouts now keep server and bridge blocks stacked while stretching their internal action rows cleanly.
- Nexus Unity internal logs now default to important Console messages only, with `All` and `Custom` modes for verbose diagnostics.
- The quick pre-push validator now supports `NEXUS_UNITY_HOOK_LIVE=auto|required|off`, retries transient live-smoke failures, and prints validation timing.
- `unity_editor_controller` can run tests and wait for results through bridge-side polling instead of blocking the Unity main thread.
- `unity_ui_automation` now forwards `deep` hierarchy reads and `class_name` queries through the MCP bridge.
- The Python MCP bridge now supports `NEXUS_UNITY_URL`, `NEXUS_UNITY_PORT`, and `NEXUS_UNITY_TIMEOUT_SECONDS`, and avoids writing Python bytecode caches.
- Raw API schemas now document `update_component`'s preferred `properties` payload, keep its legacy `json_data` payload compatible, and expose `invoke_method.arguments` as a valid array schema.
- `create_material` now accepts an optional explicit asset `path`, allowing stress tests and automation to keep generated materials isolated.
- Public docs now identify `unity_write_and_compile` as the supported code-edit macro and treat old `unity_apply_code_change` wording as stale.

### Fixed
- `click_object_in_game` now honors `instance_id` as documented instead of only accepting hierarchy paths.
- Existing Codex and Claude Desktop config files are backed up before Nexus Unity updates their MCP server entries.

### Removed
- Removed orphan `Runtime/Tests.meta` file from the public package.
- Removed the default `runtime_trace.txt` diagnostic write from runtime log capture.

## [1.0.0] - 2026-05-18

### Added
- First public open source release for `com.forkhorizon.nexus.unity`.
- Raw HTTP JSON-RPC API with 111 Unity Editor tools discoverable through `list_tools`.
- Consolidated MCP bridge API with 14 `unity_` manager and macro tools for AI clients.
- Modular Python bridge package under `nexus_bridge/`.
- `unity_write_and_compile` macro for write, wait, and compiler-feedback workflows.
- Editor tests for path security, port behavior, manager routing, raw registry consistency, and bridge contract consistency.
- Release-readiness docs: `SECURITY.md`, `CONTRIBUTING.md`, and `RELEASE.md`.
- `GPL-3.0-only` license.

### Changed
- Public repository target is `https://github.com/ForkHorizon/NexusUnity.git`.
- Public documentation now treats Nexus Unity as a standalone open source package.
- The Unity installer deploys both `nexus_unity_bridge.py` and the required `nexus_bridge/` module to the project root.
- Package version, server version, bridge version, and public docs now use `1.0.0`.
- Package metadata now includes public repository, documentation, changelog, and license URLs.

### Removed
- Removed generated Graphify cache files from the package repo.
- Removed tracked `.jules/`, `.DS_Store`, Python bytecode, and local validation artifacts from public package contents.
- Experimental native bridge artifacts are not included in the public release.

## Pre-public History

Earlier internal builds used `2.x` and `3.x` version numbers while the package was being validated. Public semantic versioning starts at `1.0.0`.
