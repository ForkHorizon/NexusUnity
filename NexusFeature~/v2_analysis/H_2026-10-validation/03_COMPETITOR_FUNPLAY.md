# Funplay core profile — source teardown

Date: 2026-10-04. Inspector: Agent C. No Unity Editor was started. No port 7800 traffic. No Nexus source was modified.

## Which product

**Chosen:** [FunplayAI/funplay-unity-mcp](https://github.com/FunplayAI/funplay-unity-mcp) — UPM package `com.gamebooom.unity.mcp`, display name "Funplay MCP for Unity".

| Pin | Value |
| --- | --- |
| Tag | `v0.6.10` (annotated tag points at the commit below) |
| Commit | `6fbbc213329c8216ed2610c3076c04292196edec` |
| Commit time | 2026-10-04 12:05:28 +0800, message "Release v0.6.10: fix Broker notification handshake and refine UI skills" |
| Clone | `/tmp/funplay-unity-mcp` (depth 1 of default branch, which is that tag) |
| Package | `package.json`: Unity `2022.3`, MIT, deps `com.unity.nuget.newtonsoft-json` 3.2.1 and `com.unity.inputsystem` 1.7.0 |
| Docs in repo | `README.md`, `README_CN.md`, `CHANGELOG.md`, `Documentation~/index.md`, `Documentation~/multi-project-setup.md`, `Documentation~/ui-workflows.md`, `PROFILER_TOOLS.md`, `CONTRIBUTING.md` |
| Org | [github.com/FunplayAI](https://github.com/FunplayAI), verified domain `funplay.top`. Marketing site [funplay.dev](https://funplay.dev/) points Unity editor control at this repo. |

**Why this one.** Nexus planning names "Funplay core profile" (`NexusFeature~/MASTER_PLAN_V2.md`, `BENCHMARK_PROTOCOL.md`, `PROMPT_FOR_AI.md`). That phrase is this package's `core` / `full` tool-exposure profiles (`Editor/MCP/Server/MCPToolExportProfile.cs`), not a generic agent brand. The high-surface Unity editor integration is this repo.

**Same vendor, not substituted in.**

- [FunplayAI/Funplay](https://github.com/FunplayAI/Funplay) is a separate Electron workbench that *consumes* engine MCPs. It is not the editor tool server.
- Sibling MCPs exist for Cocos, Godot, and Unreal. They have their own core/full counts and were not used.
- [FunplayAI/funplay-skill](https://github.com/FunplayAI/funplay-skill) is an external skills library. This package also embeds two skills of its own (below).
- NuGet `funplay.unity.mcp` is a **stdio wrapper** published from a private workspace (`scripts/release.sh`: `MCP_WRAPPER_ROOT`, project `FunseaAI.Unity.Mcp.csproj`). The wrapper source is **not** in the public clone. `.github/workflows/publish-nuget.yml` and `publish-mcp-registry.yml` only download/push `funplay.unity.mcp.<version>.nupkg` and describe it as "stdio bridge for the local Unity Editor MCP server". Changelog v0.6.10 claims that packaged bridge completed init, notifications, discovery, and one tool call. **DOCUMENTED CLAIM** for the live bridge test. **UNKNOWN** for wrapper internals.

**Rejected, not used as stand-ins:** CoplayDev/unity-mcp, IvanMurzak/Unity-MCP, akiojin/unity-cli, hatayama/uLoopMCP. Funplay's own README compares itself to Coplay and to Unity AI Assistant; those columns were not re-audited here.

Label key: **CONFIRMED** = this clone's source (or a count produced from it). **DOCUMENTED CLAIM** = README/changelog/docs only. **INFERRED** = follows from the code but not executed. **UNKNOWN** = not in the inspected tree and not run.

## Architecture

**CONFIRMED.** Editor-only assembly `Editor/Funplay.Editor.asmdef` (`includePlatforms: ["Editor"]`). There is no Runtime assembly. README "Editor-only / does not add runtime components to a built player" matches the asmdef.

In-process path (`README` architecture block matches the types):

`HTTP JSON-RPC` → `HttpMCPTransport` → `MCPRequestHandler` → `MCPExecutionBridge` → `FunctionInvokerController` (reflection) → `[ToolProvider]` static methods.

Tools are discovered by scanning loaded assemblies for `[ToolProvider]`, then every **public static** method (`Editor/Tools/ToolRegistry.cs`). `BindingFlags` are `Public | Static` without `FlattenHierarchy`, so `object.Equals` / `ReferenceEquals` are not pulled in. Names are snake_case of the method. A duplicate name logs a warning and keeps the first. `evaluate_expression` is hard-blocked. Manual `ToolRegistry.Register` exists for external plugins.

Calls are marshalled onto the editor thread (`IEditorThreadHelper`). Non-`get_task` calls save a pending-function record in `SessionState` before invoke and clear it on success (`MCPExecutionBridge`).

Optional **Broker Mode** is a second process. `keepalive-broker.cs.txt` is compiled with Unity's bundled Mono and left running outside the reloadable AppDomain (`MCPBrokerProcessManager`). The broker owns the client-facing loopback port. Unity attaches on `/_funplay/broker/*`. Default is off (`DefaultMCPBrokerModeEnabled = false`).

The package can also be an MCP **client** (`Editor/MCP/Client/MCPHost.cs`, stdio) to call external servers. That is a side path, not the editor tool host.

Server cold-start: `RootScopeServices` calls `StartAsync` only when `MCPServerEnabled` is true and a post-reload restart is not already pending. Default `enabled` is **false**. Batch mode does not start services. The user starts it from **Funplay → MCP Server**. After that, domain reload restarts it (below).

## Transport

**CONFIRMED.**

- Direct mode: `TcpListener(IPAddress.Loopback, port)`, `NoDelay`, hand-rolled HTTP/1.1. Not `HttpListener`. Logged URL `http://127.0.0.1:{port}/`.
- Methods: POST (JSON-RPC body) and OPTIONS (204). Other methods get 405.
- Responses: `Connection: close`. CORS `Access-Control-Allow-Origin: *`, methods POST/OPTIONS, header `Content-Type`.
- JSON-RPC 2.0 required. `initialize` advertises `protocolVersion` **`2024-11-05`**, not the 2025-03-26 streamable-HTTP revision.
- Capabilities: `tools.listChanged = true`. `resources` and `prompts` are empty objects (no `listChanged`, no subscribe).
- "Streamable HTTP" in this code is a **single response** whose body is SSE when the request `Accept` contains `text/event-stream`, used to piggyback `notifications/tools/list_changed` ahead of the JSON-RPC result (`HttpMCPTransport.SendSseResponseAsync`). It is not a long-lived GET SSE session.
- Notifications (`notifications/initialized`, `notifications/cancelled`, other `notifications/*`) return a null handler result. Broker protocol **v4** (this release's fix for issue #58) turns an empty push into **HTTP 202 with no body**, so strict clients (changelog names Codex) do not see `id: null`. Direct mode uses the same `SendAcceptedAsync` path.
- Header cap 64 KiB. Broker body cap **32 MiB** (`keepalive-broker.cs.txt`). Broker holds a client request up to **300000 ms** while Unity is detached; pull timeout 25000 ms. If the backend is gone it returns JSON-RPC `-32001` "Unity MCP backend is reloading or reconnecting."
- There is **no MCP-client auth token**, no Host/Origin allowlist, and no DNS-rebinding check on the public HTTP parser. Loopback bind is the only network control found. The broker's `X-Funplay-Broker-Token` is a `Guid` shared by the Unity editor and the broker process (`MCPBrokerProcessManager`), not by the AI client.

**Ports. CONFIRMED.**

- Constant `DefaultPort = 8765` is the fallback for a missing/out-of-range pin (`NormalizePort`), not the port new projects bind.
- New projects (no settings file) are unpinned. `FunplayProjectIdentity` hashes the project path into **20000–29999**.
- Settings written before `portConfigured` existed are migrated to **pinned** (`settingsVersion < 1`), so upgrades keep the old port.
- If the requested port stays busy, the transport scans a nearby free port. The fallback is `SessionState` only, never written as the pin. A fallback remembered this session is retried first after reload so clients are not chased onto a new port. Known-foreign ports retry ~3×250 ms instead of ~40×250 ms (~10 s).
- One-click client config is blocked while a fallback bind is active (changelog 0.6.0, still the control-panel behavior described in the current README).
- Client entry key is `funplay-<project-directory>` (ASCII), with a hash suffix on collision. Claude Code entries go under `projects["<git root>"]` in `~/.claude.json`, not the global mcpServers object.

## Tool count, default surface, full profile, dynamic exposure

**CONFIRMED counts from source, not from the README sentence alone.**

A brace-depth parse of `Editor/Tools/Builtins/*.cs` (strings and comments skipped) found **180** `public static` methods on classes marked `[ToolProvider]`, across **42** distinct provider category strings. That matches changelog 0.6.9 ("180 tools across 42 modules"), README 0.6.10, and `Documentation~/index.md`. v0.6.10's changelog does not add or remove tools. Nested helpers (`ClickOutcome.Miss`, `SnapshotAccessor.Load`) are not on a `[ToolProvider]` type and are not tools. `ExecuteCodeSafetyPolicy` has no `[ToolProvider]`.

Default profile constant: `"core"` (`SettingsController.DefaultToolExportProfile`). `MCPToolExportPolicy.CoreTools` is a fixed set of **40** names. `IsToolAllowed` for `full` exposes every discovered tool unless the user saved a custom full list. A custom core list **replaces** the built-in 40; it is not a delta. Empty/unknown profile strings parse as core.

**Default visible surface = those 40, after the user has started the server.** Changelog v0.6.10 says a real MCP TypeScript SDK 1.32.0 client, against Broker, discovered 40 tools. **DOCUMENTED CLAIM** (not re-run here). **CONFIRMED** that the source policy yields 40 when `coreToolsCustom` is false.

Core set (`MCPToolExportProfile.cs`), in source order:

`execute_code`, `simulate_key_press`, `simulate_key_combo`, `simulate_mouse_click`, `simulate_mouse_drag`, `get_scene_info`, `get_hierarchy`, `get_console_logs`, `capture_scene_view`, `capture_game_view`, `record_game_view`, `extract_recording_frames`, `get_recording_frame`, `simulate_ui_scroll`, `raycast_at_point`, `get_compilation_errors`, `prepare_editor`, `get_task`, `cancel_editor_operation`, `audit_ui`, `cancel_ui_audit`, `find_project_types`, `inspect_ui_sprites`, `get_tool_capabilities`, `get_visual_coordinates`, `get_object_screen_bounds`, `get_ui_defaults`, `create_project_ui`, `get_editor_state`, `get_selection`, `set_selection`, `get_prefab_stage`, `find_game_objects`, `list_components`, `get_component_properties`, `set_component_property`, `set_component_properties`, `set_prefab_property`, `set_prefab_properties`, `execute_menu_item`.

`execute_code` is sorted last and prefixed `[fallback]` in the description. Other core tools are prefixed `[core] `. Rank puts `execute_code` after the rest of core so it is not the model's first schema.

**Not in core, still in the 180 / full catalog** (README table matches the method names): legacy play/compile (`enter_play_mode`, `exit_play_mode`, `wait_for_compilation`, `request_recompile`, `get_reload_recovery_status`), scene file ops, GameObject create/destroy, `create_script` / `edit_script` / `patch_script`, asset and file CRUD, prefab stage, UI primitives, `configure_ui_defaults`, preview session tools, profiler and memory snapshots, Test Runner, undo, packages, lighting, animation, physics queries, `focus_on_object`, `capture_simulator_view`, `capture_multiview`, `capture_editor_window`, `get_build_settings`, `get_project_settings`, execute_code history/replay. `get_tool_capabilities` (core) reports implemented vs exposed vs disabled and does not enable anything.

**Dynamic exposure. CONFIRMED.**

- Window: **Funplay → Tool Exposure** (`FunplayToolExposureWindow`). Saving restarts the running server.
- `MCPToolListChangeNotifier` hashes the exposed set in `SessionState` and sets a pending flag. The next SSE-accepting response carries `notifications/tools/list_changed`. A failed send restores the pending flag.
- `tools/call` checks the same allowlist as `tools/list` and returns `TOOL_NOT_EXPOSED` or `TOOL_DISABLED` or `UNKNOWN_TOOL`. Calling a full-only tool on the default profile fails closed.
- `get_task` also refuses a kind whose underlying tools are not exposed (`TASK_KIND_NOT_EXPOSED`). It does not bypass a custom allowlist.
- Runtime `ToolRegistry.SetEnabled` exists. The user-facing control inspected is the exposure window plus the safety toggles, not a separate per-call approval UI.

**Schema size. DOCUMENTED CLAIM, not re-measured.** Changelog 0.6.9: compact default-tool definitions went from 44,938 to **36,105 characters** versus an intermediate 57-tool set (~20%). It explicitly says this is schema size, not billed tokens. v0.6.10 does not republish the figure. Tool methods did not change in that release, so the 36,105 figure is **INFERRED** still current. No latency table exists in the 0.6.10 README. Older Nexus bench milliseconds are **not** reused here.

## Jobs, receipts, domain reload, recovery

**Jobs. CONFIRMED.** `TaskStatusService.Kinds`: `editor`, `ui_audit`, `ui_preview`, `recording`, `recording_frames`, `tests`. `get_task` is read-only. It can wait up to `wait_seconds` 0..30, or until `after_revision` changes. Revisions are 64-hex. Unchanged phases tell the client `poll_after_ms`. An interrupted HTTP wait must not restart, cancel, or clean the task (changelog 0.6.9; waiter is `MCPTaskWaiter`, and `get_task` is excluded from the pending-function slot). `wait_complete` means the wait ended, not that the job succeeded (`MCPServerInstructions`).

`prepare_editor` (`EditorOperationFunctions`) is the core compile/play entry. Target `edit` or `play`, optional asset refresh, deadline 5..900 s (default 120), caller `request_key` (≤128 chars) for idempotency. Same key plus different target/refresh/timeout throws `IDEMPOTENCY_CONFLICT`. Overlap throws `EDITOR_OPERATION_IN_PROGRESS`. Coordinator comment: persist before returning an action; after reload, inspect the effect; **never replay a user code snippet**. Journal path: `Library/FunplayMcp/Operations/editor-operations.json`, max **32** receipts. Invalid journal fail-closed: diagnostics still work, no actions replayed (`OPERATION_JOURNAL_UNAVAILABLE`). A `ready` receipt is historical; `current_editor.ready` is separate. Session change marks running ops `interrupted` / `EDITOR_RESTARTED` and does not replay. Cancel stops future steps only; in-flight import, compile, or play transition is not rolled back. Timeout does not cancel native compilation.

Other durable work: `audit_ui` (bounded, interrupted on domain reload), UI preview sessions (restore saved clean scenes / selection / Game View; dirty user state protected; business side effects not rolled back), `record_game_view`, frame extraction, `run_tests` (returns `job_id`; status/cancel are full-profile aliases, core uses `get_task`).

**Domain reload. CONFIRMED.**

- `MCPServerDomainReloadHandler`: if the server was running, `SessionState` records that fact and a **5 minute** restart deadline. `beforeAssemblyReload` calls `StopSync` on the listener (comment: awaiting was why port 8765 stayed bound, issue #1). `afterAssemblyReload` restarts when the editor is not compiling. It does not write the live port back into settings, so a derived port is not accidentally pinned.
- Direct HTTP clients see the socket drop until bind succeeds. Broker mode keeps the client port and redelivery-flags the in-flight `tools/call`.
- `TryCreateBrokerRedeliveryResponse` does **not** re-execute the tool. Within 10 minutes it returns the stored recovery summary; otherwise it returns an error text telling the client to call `get_reload_recovery_status` and retry only if needed. `get_task` and `get_reload_recovery_status` are exempt from that synthetic reply so status reads still run.

**Pending-call recovery. CONFIRMED, narrower than "the call resumes".** `DomainReloadHandler` stores the in-flight function name and arguments, allows at most **5** auto-resumes per 120 seconds, then pauses. `CheckForInterruptedExecution` publishes a recovery summary. The only continuation result it appends is `TempScriptRunner.ConsumeResult()`. `TempScriptRunner.ScheduleExecution` has **no callers** elsewhere under `Editor/`. Current `execute_code` is in-memory (`ScriptCompilerPipeline`), not `Assets/unity-mcp/Editor/Temp`. **INFERRED:** an interrupted arbitrary tool, including `execute_code`, is reported, not replayed. Disk temp-script resume is leftover.

`get_reload_recovery_status` is **full profile**, not core. Core recovery path is `get_task` / `prepare_editor` plus the initialize instructions.

## Skills, prompts, resources

**Prompts. CONFIRMED.** Five built-ins in `MCPPromptProvider`: `edit_prefab_safely`, `verify_compilation`, `enter_play_and_recover`, `wire_serialized_references`, `create_playable_prototype`. Project prompts: `<project>/mcp-prompts/*.md`, max 100 files, 256 KiB each, names `[a-z][a-z0-9_-]{0,63}`. Cached at server start; restart or domain reload picks up edits (README). `prompts/get` rejects unknown and non-string arguments.

**Resources. CONFIRMED.** Fixed URIs:

- `unity://project/context`, `unity://project/summary`
- `unity://scene/active`, `unity://scene/current` (alias)
- `unity://selection/current`
- `unity://errors/compilation`, `unity://errors/console`
- `unity://mcp/interactions`

Templates: `unity://scene/object/{name}`, `unity://asset/path/{asset_path}`, `unity://component/gameobject/{name}/component/{type}`.

`unity://project/context` is built by `EditorContextBuilder` (scene, selection, console, compile caches, refreshed on hierarchy/selection/play-mode/scene events). This is a text resource, not a Nexus-style project-map command.

**Skills. CONFIRMED.** `ProjectSkillsManager.SupportedPlatforms`: `codex`, `claude`, `cursor`, `opencode`, `dsh`, `antigravity`. Both built-ins are not optional: `unity-mcp-workflow` (changelog 0.6.9: v1.0.5; 0.6.10 does not bump it) and `unity-ui-composition` **v1.0.7** (0.6.10: reference-resolution priority, saved page prefabs over runtime hierarchy, no proactive safe-area scripts). They are generated text installed into client skill dirs and managed blocks (`CLAUDE.md`, `AGENTS.md`, Cursor rules, `.opencode/skills`, `.dsh/skills`, `.agents/skills`). Guidance says: use MCP for Unity operations; desktop computer-use only for a confirmed gap or an explicit user request; do not bypass custom allowlists; do not replay uncertain mutations. The same routing is in `MCPServerInstructions.Text`, returned on every `initialize`.

**One-click MCP config targets. CONFIRMED** in `FunplayMCPClientConfigPanel.CreateTargets`: Claude Code, Cursor, Kimi, LM Studio, VS Code, Trae, Kiro, Codex, OpenCode, DeepSeek Harness, Antigravity. Configure+Skills is only the six skill platforms. Windsurf is a README manual JSON note ("same structure as Cursor"), not a target in `CreateTargets`. README's "VS Code Copilot / any MCP client" is **DOCUMENTED CLAIM** beyond the coded writers.

## Capture

**CONFIRMED.** All stills are **PNG**, not JPEG. `EncodeToPNG`. Inline transport is a `data:image/png;base64,` prefix, which `MCPRequestHandler` splits into an MCP image block (`mimeType: image/png`) plus text metadata.

`MaxInlineScreenshotBytes = 512 * 1024` raw PNG bytes. Larger captures are written to disk and the tool returns a path (`fell_back_to_file`). `save_to_file` forces that. Instructions tell the model to prefer a file when the client can read local files.

`capture_game_view` (core):

1. Repaint Game View and wait frames (`RefreshGameViewBeforeCaptureAsync`) so `m_RenderTexture` is not a stale frame (changelog 0.6.2).
2. Read PlayModeView's internal `m_RenderTexture` via reflection. Comment: this avoids `camera.Render()`, which can bypass SRP and return black frames in URP/HDRP.
3. `ShouldFlipPlayModeViewRenderTexture()` is **unconditionally true**. The RT is described as vertically inverted on ReadPixels.
4. That RT is the already composited Game View, so Screen Space Overlay is whatever the Game View already drew. This path does not retarget canvases.
5. If the RT cannot be read, it falls back to `Camera.main` / first camera and `CaptureWithUIPngBytes`. That path **temporarily** sets active Screen Space Overlay canvases to Screen Space Camera, renders, then restores render mode, camera, and plane distance in `finally`. Geometry is tagged `camera_fallback` and is **not** a valid click map.

`capture_scene_view` (core): renders the Scene View camera. `include_ui` defaults **true** and uses the same temporary overlay-canvas switch (issue #54, changelog 0.6.4). Restores canvas state after capture, including exceptional paths. No "frame this object" parameter. `focus_on_object` exists but is **full only**.

`get_visual_coordinates` / `get_object_screen_bounds` (core): captures retained for **32** images in the domain; interaction requires the same render geometry/mode and age **≤ 30 seconds**. Resized screenshot pixels map into render pixels. Stale, reloaded, or changed geometry is rejected.

`capture_simulator_view` (full): Device Simulator window texture, optional device name, optional safe-area outline, vertical flip. Not a player build.

`record_game_view` (core): silent MP4 via `UnityEngine.Media.MediaEncoder`, no Recorder package. macOS and Windows, graphics-enabled Editor, **Play Mode**, Game tab must stay visible and at a constant resolution. Includes overlay UI because it encodes the Game View. No audio. Duration 1–120 s, fps 1–60, max dimension 128–1920. Bytes are **not** in the MCP response; path is `Library/FunplayMcp/Recordings/`. Play-mode exit or script reload finalizes and keeps an `interrupted` receipt. Markers from click/drag/scroll and `mark_recording` (full). Frame extract is async (`extract_recording_frames`, core) and domain reload interrupts the decoder.

## Compile, execution, context, project inspection

**Compile. CONFIRMED.** `CompilationService` subscribes to `CompilationPipeline` and keeps the latest `CompilerMessage` list. `get_compilation_errors` (core) returns a **plain string**, not `{success,message,data}`: either "No compilation errors detected." / "Currently compiling..." or a flat bullet list `- [Error|Warning] message (file:line)`, capped by `max_entries` (default 50). **No grouping by error code, file, or assembly.** Warnings off by default. `unity://errors/compilation` is a separate resource with "source context" from `EditorContextBuilder` (not re-quoted here).

`prepare_editor` is the supported wait: refresh, compile, reach edit or play, survive reload via the journal. `wait_for_compilation` and `request_recompile` remain full-only.

**execute_code. CONFIRMED.** Core, but description-ranked as fallback. Roslyn `csc` first, then CodeDom (`ScriptCompilerPipeline`). In-memory assembly. README claim "no `.cs` under Assets and no domain reload from the compile itself" matches the pipeline class. The call **does** `AssetDatabase` refresh and waits for ready unless `skip_refresh=true`. The tool description states that default refresh can import and domain-reload from pending changes and wipe Play Mode. Snippet runs on the editor thread.

Templates: `IFunplayCommand.Execute(ExecutionContext)` (Undo registration, logs, `ReturnValue`, changelog `{logs, created, modified, destroyed, returnValue}`) or legacy `public static string Run()`. History: 50 entries in `SessionState` (survives reload, not editor restart). `get_execute_code_history` / `replay_execute_code` / `clear_execute_code_history` are full-only. Replay re-executes.

**Context / inspection. CONFIRMED.**

- Live resources above, plus `get_editor_state`, `get_scene_info`, `get_hierarchy`, `get_selection`, `get_prefab_stage`, `find_game_objects`, component property get/set, `find_project_types`, `inspect_ui_sprites`, `get_console_logs`.
- Console tool supports `filter_text` and `group_duplicates` (collapse identical lines with a count). That grouping is logs, not compiler errors.
- Instructions tell the model to chain `instanceId` with `find_method=by_id` and not to hand-edit `.unity` / `.prefab` / `.asset` while the Editor is open.
- Many newer tools return `Response.Success/Error` → `{success, message, data}` or `{success:false, code, error, data}`. Many older tools still return raw strings (`get_compilation_errors`, several scene/file/screenshot helpers before `FinishCapture`). The initialize text says non-image results are JSON envelopes. **That is not true of every tool.** Mixed envelopes are **CONFIRMED**.

## Input, build, player / runtime

**Input. CONFIRMED.** All five simulation tools are core and require Play Mode (`PLAY_MODE_REQUIRED`).

- `simulate_mouse_click`: bottom-left pixels by default; also `image_pixels` (needs fresh `capture_id`) and `normalized`. UI raycast via `EventSystem` / `ExecuteEvents`, then physics. Structured hit/miss. Does not depend on the Input System for the UI path. Description: "works without the Input System".
- `simulate_ui_scroll`: top `IScrollHandler` only, no click-through. Requires an EventSystem.
- `simulate_mouse_drag`, `simulate_key_press`, `simulate_key_combo`: `UnityEngine.InputSystem` queue (`InputSystem.QueueEvent` / `Update`). Package hard-depends on Input System 1.7.0. Keys are device events, not legacy `Input`.
- Click/drag/scroll write recording markers when a recording is active.

**Build. CONFIRMED absence.** No `BuildPipeline` / `BuildPlayer` references under `Editor/`. `get_build_settings` (full) reads enabled scenes, active build target, scripting backend. `get_project_settings` (full) reads a guarded subset of `PlayerSettings` (identifier, product name, company, Android SDK, scripting backend). There is no "build the player" tool.

**Player / runtime. CONFIRMED.** Editor and Editor Play Mode only. Device Simulator capture is an Editor window. Recording is Editor Play Mode on macOS/Windows. No player connection, no standalone log tail, no device deploy. README states the package does not ship in the built game. **INFERRED** from asmdef: player builds do not include this assembly even if someone copied the package in.

## Installation and supported clients

**CONFIRMED / DOCUMENTED CLAIM as marked.**

- Git UPM: `https://github.com/FunplayAI/funplay-unity-mcp.git` (README).
- OpenUPM: scope `com.gamebooom`, package `com.gamebooom.unity.mcp` version `0.6.10` (README pins that version).
- Menu start, then one-click config. Settings file: `UserSettings/FunplayMcpSettings.json`.
- **Funplay → Check for Updates** can refresh a Git install or import a unitypackage (README). Release helper `scripts/release.sh` can export `Assets/unity-mcp` as a filtered unitypackage. **DOCUMENTED CLAIM** that the update menu works; the menu class exists (`FunplayMCPUpdateChecker`).
- Clients: the eleven coded config writers, plus Windsurf as docs-only, plus any client that can POST MCP HTTP to loopback. Skills for six of them.
- Changelog validation for 0.6.10: **661** EditMode tests passed on graphics-enabled Unity **2022.3**, macOS, 0 failures/skips. Windows and other Unity versions "are not claimed as live-tested." **DOCUMENTED CLAIM.** This teardown did not run Unity. The test tree has 46 `*Tests.cs` files; attribute count is not the same as NUnit case count, so it is not used as a contradiction.
- Broker + stdio-bridge smoke in that same changelog entry is a **DOCUMENTED CLAIM**.

## Performance claims

No current-version latency SLO was found in source.

- README "Most Advanced", "one sentence builds a snake game", "<1 min" on funplay.dev: **DOCUMENTED CLAIM / marketing**. Not treated as measurements.
- Schema character count 36,105: **DOCUMENTED CLAIM** (0.6.9), **INFERRED** unchanged at 0.6.10.
- Recording text says capture overhead means it is evidence, not frame-accurate profiling. **CONFIRMED** as a documented limitation inside the tool description and README.
- `execute_code` pre-refresh can dominate a call and can force a domain reload. **CONFIRMED** by the tool's own parameter text. No millisecond figure in source.

## Security model

**CONFIRMED.** Trust model is "localhost process that can already drive the Editor."

- Bind is loopback only (direct and broker). Any local process that can open `127.0.0.1:<port>` can call every exposed tool. Port is in the MCP window, `UserSettings/FunplayMcpSettings.json`, and the client config files. No bearer token, no constant-time compare, no per-call user approval.
- README: "All exposed MCP tools run directly. There is no extra approval toggle." No approval gate was found in the request path.
- `execute_code` safety is a **regex blocklist** (`ExecuteCodeSafetyPolicy`), on by default, plus a strict filesystem regex set (also default on). Blocked examples: `File.Delete`, `Directory.Delete`, `Process.Start`, `Environment.Exit`, `Application.Quit`, `AssetDatabase.DeleteAsset`, `while(true)`, `for(;;)`. Strict mode adds a list of `File`/`Directory` write APIs, `FileStream`/`StreamWriter`/`StreamReader` constructors, and some absolute/home/traversal string literals. The settings UI says this is "a defensive guard, not a complete sandbox." The call argument `safety_checks=false` **bypasses** it. Concatenation, reflection, `UnityEditor` APIs, and `AssetDatabase` paths other than `DeleteAsset` are not covered. Project-namespace injection into snippets is **off** by default.
- `write_file` (full, not core) `ResolveProjectPath`: rooted paths are used as-is. A full-profile session can `File.WriteAllText` outside the project. Core does not expose `write_file`, but core `execute_code` can reach the same APIs if the regex misses.
- Scene-view overlay capture mutates canvas render mode for the duration of the capture and restores it. Game View RT readback does not.
- Broker token protects only the editor↔broker channel.
- Multi-project work is about not leaking the wrong editor's tools into another client's config (Claude project scope, per-project server names). It is not an authentication boundary between projects if both ports are reachable locally.

## What Nexus is objectively better at

Baseline used: Nexus is a UPM package with dual Legacy HTTP and optional Unity Pipeline, three canonical commands (project map, group compile errors, capture/look), one result envelope, JPEG Game View capture that includes Overlay via a vertical flip, Scene View framing, and truthful readiness epochs. Façade-vs-primary is unresolved and is not a recommendation.

- **Default surface.** Funplay's default, once the server is started, is **40 tools / ~36k characters of schema** (0.6.9 measurement). Nexus's agent-facing contract is **three commands**. Funplay itself added `get_tool_capabilities`, `[fallback]` on `execute_code`, and a cut from an intermediate 57-tool core down to 40 because selection cost was the problem. Nexus already sits on the small side of that trade.
- **One result envelope.** Funplay initialize text claims JSON `{success, code, data}` but `get_compilation_errors` and other early tools still return prose strings. Nexus's envelope is a single contract. Funplay clients must branch on both shapes.
- **Compile errors as a grouped command.** Funplay's core compile read is a flat, truncated `CompilerMessage` bullet list (file + line, no group key). Grouping exists only for **console** duplicates. A project-map command also has no Funplay equivalent; `unity://project/context` is a cached prose summary, and `find_project_types` is a type search.
- **Capture weight and framing.** Funplay stills are PNG, inlined until 512 KiB raw, then a file path. Nexus look defaults are JPEG. Funplay core `capture_scene_view` does not frame a subject; framing is a separate full-only `focus_on_object` plus a raw camera grab. Nexus Scene View framing is part of the look command.
- **Readiness as the product, not a 180-tool side effect.** Funplay's best honesty feature (`current_editor.ready` separate from a historical `ready` receipt, no replay) is real, but it sits under `prepare_editor` + `get_task` plus a 32-deep journal and six job kinds. Nexus epochs are the smaller, explicit version of "are you actually ready."
- **Official Pipeline as an optional transport.** Funplay is a private HTTP dialect (`protocolVersion` 2024-11-05, custom broker, `Connection: close`) plus a **private** stdio wrapper. Nexus can speak Legacy HTTP or Unity Pipeline without that second process.

## What Nexus is objectively worse at

- **Breadth inside the Editor.** Full catalog is 180 tools / 42 modules: hierarchy and SerializedObject edits, prefab property writes without Prefab Mode, UI authoring, undo, menus, packages, profiler, memory snapshots, Test Runner, lighting, animation, physics queries. Core already includes play-mode input, Game View and Scene View capture, MP4 recording, UI audit, and `execute_code`. Nexus's three commands do not cover that.
- **In-memory C# with Undo changelog.** `execute_code` compiles through bundled Roslyn, runs on the editor thread, and can return `{logs, created, modified, destroyed, returnValue}` without writing a project script. Nexus has no equivalent in the baseline. (Do not treat this as something to copy; see below.)
- **Long-running work that survives reload.** `prepare_editor` idempotency keys, on-disk journal, `get_task` bounded wait, `poll_after_ms`, and optional broker hold (up to 300 s) are implemented. Direct mode still drops the socket, but the listener is restarted for 5 minutes and the job is not replayed. Nexus's baseline does not claim an out-of-AppDomain hold or a multi-kind job receipt.
- **Client install surface.** Eleven coded config writers and six skill targets, with project-scoped Claude entries and per-project server names so two Editors do not share one MCP key. Derived ports in 20000–29999. Nexus baseline does not include that installer.
- **Click-from-screenshot contract.** Core click/scroll takes `capture_id`, rejects geometry changes and images older than 30 seconds, and returns which object was hit. Recording markers attach to those events. Nexus look does not drive input.
- **Prompts, resources, and initialize instructions** that every client receives, including "do not hand-edit open scenes" and "do not replay a mutation whose outcome you did not read back."

## What Nexus could copy cheaply

These fit the current three-command shape. They are not a tool catalog and not a façade.

- **Bounded wait + `poll_after_ms` on readiness** for the three commands, instead of the client inventing a poll interval. Funplay caps this at 30 seconds and tells the model the backoff. Copy the hint, not `get_task`'s six kinds.
- **Say "unknown, do not replay"** when a call dies mid-reload, and keep historical success distinct from current readiness. Funplay already learned that a `ready` receipt is not `current_editor.ready`. Nexus epochs are the right place to phrase that in one envelope field if it is not already explicit to the model.
- **Idempotency key on the compile/prepare path only.** One string, conflict if the same key is reused with different arguments, no second operation while one is active. Funplay's journal format does not need to come with it.
- **Screenshot budget.** Funplay refuses to inline a PNG over 512 KiB and returns a path. If a JPEG look still blows the context window, the same spill-to-file rule is a small gateway change.
- **Short `initialize` instructions** stating the envelope, "branch on code", and "do not hand-edit `.unity/.prefab/.asset` while the Editor is open." Funplay's `MCPServerInstructions` is static text. No new tools.
- **Console duplicate collapsing** is optional and not the compile command. Only worth it if `group_compile_errors` is being asked to also clean log spam. Do not merge those two outputs; Funplay's split is the useful part (and their compile side is the weak one).

Do not copy Scene View overlay by toggling canvas render mode. Nexus already reads Game View Overlay off the framebuffer with a flip. Funplay's Game View path does the same; its Scene View path mutates canvases for the shot. Prefer the framebuffer read.

## What would require architecture change

- An `execute_code` / Roslyn host, snippet Undo context, and history/replay.
- A reflected 100-tool catalog, core/full profiles, or `tools/list_changed` driven by an exposure editor. That is the façade decision, which stays unresolved. **Do not add a façade to imitate this.**
- Broker process outside the AppDomain so the client TCP port survives reload.
- Play Mode input injection, capture-id click maps, MP4 recording, UI audit/preview sessions, Test Runner jobs.
- MCP resources, prompts, and a six-client skill installer.
- Prefab/SerializedObject write tools and `instanceId` chaining across dozens of mutators.
- Multi-editor derived ports and per-client config rewriters. Useful, but it is a product surface, not a patch on three commands.

## What should NOT be copied

- **The 40-tool "small" core as a target.** It is still an order of magnitude above three commands, and Funplay's own changelog is the story of cutting it down. Matching 180, or even 40, loses the surface Nexus just fixed.
- **`execute_code` as the primary tool**, with a regex sandbox and `safety_checks=false`. The blocklist does not constrain UnityEditor APIs. The settings panel and the tool text both call it not-a-sandbox. Replay (`replay_execute_code`) will re-run that snippet. Nexus should not grow a hidden eval to "catch up."
- **No approval gate.** "All exposed tools run directly" plus loopback with **no client token** and CORS `*` means any local process that learns the port has the Editor. Copying the transport without an auth check would be a regression if Nexus already has a shared secret. Copying the regex instead of a real boundary would be worse.
- **`write_file` accepting absolute paths** (full profile). Easy to miss because it is not in core, and `execute_code` can do it anyway.
- **Temporary canvas render-mode switches** to get Overlay into a camera render. They restore, but they are a mutation in the capture path. The Game View RT + vertical flip is the pattern both codebases already use for the game image.
- **Mixed string and JSON results**, and initialize text that over-claims a uniform envelope.
- **Private stdio wrapper** (`FunseaAI.Unity.Mcp.csproj` outside the public repo) as the compatibility story. The public server is HTTP; the bridge source cannot be reviewed from the canonical repo.
- **Holding the HTTP request for up to five minutes** across reload without a ready/not-ready epoch the client can branch on. Funplay's broker does this so clients do not disconnect; the failure mode is a long silent call. Nexus epochs should stay honest rather than look busy.
- **Auto-refresh inside every execution call.** `execute_code`'s default refresh can domain-reload and destroy Play Mode. A look/compile command must not import the project as a side effect of a read.
- **Marketing parity:** snake-game-from-one-sentence, "most advanced," profiler-as-validation, or a tool-count row in a comparison table. Funplay's useful ideas are the recovery journal rules and the exposure cut, not the catalog size.

## Source index

| Claim anchor | Path in the v0.6.10 clone |
| --- | --- |
| 40-name core set | `Editor/MCP/Server/MCPToolExportProfile.cs` |
| 180 methods / 42 `[ToolProvider]` categories | `Editor/Tools/Builtins/*.cs` (parsed) |
| Allowlist on call | `Editor/MCP/Server/MCPExecutionBridge.cs` |
| HTTP + SSE piggyback | `Editor/MCP/Server/HttpMCPTransport.cs` |
| Protocol 2024-11-05, instructions | `Editor/MCP/Server/MCPRequestHandler.cs`, `MCPServerInstructions.cs` |
| Reload restart | `Editor/MCP/Server/MCPServerDomainReloadHandler.cs` |
| No replay on broker redelivery | `Editor/MCP/Server/MCPServerService.cs` (`TryCreateBrokerRedeliveryResponse`) |
| Broker v4, loopback, 300 s hold | `Editor/MCP/Server/Broker/keepalive-broker.cs.txt`, `MCPBrokerProtocol.cs` |
| prepare_editor journal | `Editor/State/EditorOperation.cs`, `EditorOperationService.cs` |
| Shared receipts | `Editor/State/TaskStatusService.cs` |
| PNG, flip, overlay, 512 KiB | `Editor/Tools/Builtins/ScreenshotFunctions.cs` |
| Flat compile list | `Editor/Services/CompilationService.cs`, `CompilationFunctions.cs` |
| execute_code + regex | `ScriptExecutionFunctions.cs`, `ExecuteCodeSafetyPolicy.cs`, `ScriptCompilerPipeline.cs` |
| Skills / clients | `ProjectSkillsManager.cs`, `FunplayMCPClientConfigPanel.cs` |
| Private NuGet wrapper | `scripts/release.sh`, `.github/workflows/publish-nuget.yml` |
| Defaults (core, broker off, server off, safety on) | `Editor/Settings/SettingsController.cs` |
