# Coplay / MCP for Unity teardown — 2026-10-04

Evidence labels: **CONFIRMED** (read in the v10.3.0 tree or a live GitHub API field), **DOCUMENTED CLAIM** (their README, docs site, skill, or release note, not re-measured), **INFERRED** (follows from the code but not an explicit comment), **UNKNOWN** (not in the snapshot).

This is a source teardown of **MCP for Unity**, not of Coplay's paid in-editor agent and not of Ivan Murzak's Unity-MCP. Comparisons to Nexus use only the current baseline: three canonical semantic commands plus a large legacy MCP surface; an envelope with retryable/hint; readiness epochs; Overlay-correct JPEG capture; Pipeline optional. Architecture is unresolved. This note does not recommend a production façade.

## Identity — three different things

| Thing | What it is | Evidence |
|---|---|---|
| **MCP for Unity** | Open-source Editor bridge. GitHub `CoplayDev/unity-mcp`. UPM `com.coplaydev.unity-mcp`. Python package `mcpforunityserver`. MIT. | **CONFIRMED** `MCPForUnity/package.json`, `Server/pyproject.toml`, `LICENSE` |
| **Coplay (product)** | Paid in-editor Unity agent at https://coplay.dev. Docs https://docs.coplay.dev. Marketing says Unity 2022+, natural-language tasks, approve-or-autonomous, models of your choice. Same Discord invite as the OSS repo (`discord.gg/y4p8KfzrN4`). Separate repo `CoplayDev/coplay-unity-plugin`. | **DOCUMENTED CLAIM** (coplay.dev HTML fetched 2026-10-04; GitHub org page lists the plugin repo). Source of the paid plugin was **not** inspected. |
| **Aura** | Beta README says the OSS project is "sponsored and maintained by Aura" (https://www.tryaura.dev/) and points at a sibling Godot project. `package.json` author is still Coplay / support@coplay.dev / coplay.dev. Telemetry still posts to `api-prod.coplay.dev`. | **DOCUMENTED CLAIM** for the sponsorship line. Corporate relationship beyond that string is **UNKNOWN**. |
| **IvanMurzak/Unity-MCP** | Different project. See the short section at the end. Do not merge its issues, stars, or transport into this teardown. | **CONFIRMED** different repo, package id, and transport. |

Install docs and the README still tell users to add `.../MCPForUnity#main`. Contributing says branch off `beta`. On 2026-10-04 those are not the same commit.

## Snapshot

| | |
|---|---|
| Released tag | `v10.3.0`, annotated tag `ba688193`, commit `aa5fc638` (2026-10-04 02:52 UTC, "Merge pull request #1425"). **CONFIRMED** `git ls-remote` and the clone. |
| `main` | `aa5fc638` — same commit as the tag. **CONFIRMED** |
| `beta` HEAD | `9a67cb4` (2026-10-04 16:16 UTC), package version string `10.3.1-beta.2`. **CONFIRMED** log only. Two non-chore fixes after the tag: invariant-culture numeric params (#1416) and matching raw `m_` serialized field names (#1419). Those diffs were **not** read line by line. |
| Tree read | Shallow clone of the **tag**, `/tmp/coplay-unity-mcp`. All file citations below are that tree unless noted. |
| Repo API (2026-10-04) | Created 2025-03-18. Language C#. Stars **14689**, forks **1532**, open issues **84**, watchers **81**. Default branch `beta`. Homepage `https://coplaydev.github.io/unity-mcp/`. **CONFIRMED** GitHub API. |
| Docs site drift | Live homepage fetched the same day still says `STATUS OPERATIONAL · v10.0.0` and "49 tools". README on the tag says **48** tools and lists updates only through v10.0.0. In-repo release notes (`website/docs/releases.md`) also stop at v10.0.0. The generator comment says that file is API-synced; it is stale in this tag. |

## Architecture

Three processes, two transports, one Editor plugin. **CONFIRMED** `website/docs/architecture/transports.md`, `python-layers.md`, and the trees they name.

```
MCP client  --stdio or HTTP /mcp-->  Python FastMCP (mcpforunityserver)
                                         |
                    HTTP: WebSocket /hub/plugin     stdio: TCP ~6400 (legacy)
                                         |
                              Unity Editor plugin (MCPForUnity/Editor)
                              [InitializeOnLoad] bridge + [McpForUnityTool] handlers
```

Python is not generated from C#. The docs say the three surfaces are hand-kept in sync:

- MCP tools: `Server/src/services/tools/` via `@mcp_for_unity_tool`
- CLI: `Server/src/cli/commands/` via Click, HTTP `run_command` into the same C# handlers
- Resources: `Server/src/services/resources/` via `@mcp_for_unity_resource` (read-only)

C# handlers live under `MCPForUnity/Editor/Tools/` and are discovered with Unity `TypeCache` (`ToolDiscoveryService`), not a full assembly walk. That scan change landed in v10.2.0 / was tightened again in v10.3.0. **CONFIRMED** release notes and `ToolDiscoveryService.cs`.

There is no separate runtime player process in this design. The plugin is an Editor assembly. Play Mode is `EditorApplication.isPlaying` inside that Editor.

## Transport

**CONFIRMED** `website/docs/architecture/transports.md`, `Server/src/core/config.py`, `HttpEndpointUtility.cs`, `PortManager.cs`, `plugin_hub.py`.

| | HTTP (docs call this the multi-agent default) | stdio (Claude Desktop is forced onto this) |
|---|---|---|
| Client ↔ Python | `http://127.0.0.1:8080/mcp` (`DefaultLocalBaseUrl`) | Client spawns `uvx --from mcpforunityserver mcp-for-unity --transport stdio` |
| Python ↔ Editor | WebSocket hub `/hub/plugin`. One Python process, many Editor sessions, many MCP clients. | Legacy TCP. Editor default port **6400**, then 6401… if busy. `ServerConfig.unity_port = 6400`. `ServerConfig.mcp_port = 6500` is a different field and is not the documented HTTP port. |
| Session state | Per MCP `client_id`: active instance, tool-group visibility. | Process-local. A new client replaces the previous one. |
| Instance id | `Name@hash` only. Port shorthand refused. | Port shorthand allowed (`set_active_instance(instance="6401")`). |
| Remote | Supported, but auth is a separate mode (`http_remote_hosted`). | Not hostable. |

`ServerConfig.transport_mode` defaults to `"stdio"`. The Editor UI picks HTTP vs stdio and rewrites detected client configs. Claude Desktop is always written as stdio because that client has no HTTP. **DOCUMENTED CLAIM** in the transport doc; configurator special-case was not re-read line by line.

Local HTTP binds loopback unless **Allow LAN Bind** is opted in. Remote URLs must be `https://` unless **Allow Insecure Remote HTTP** is opted in. **CONFIRMED** `SECURITY.md` and the transport doc. Local HTTP and the stdio TCP bridge do **not** require an API key. The key check in `PluginHub.on_connect` runs only when `http_remote_hosted` is true.

Multi-instance routing is real: `mcpforunity://instances` plus `set_active_instance`. HTTP can hold different active instances per MCP client against one Editor pool. **DOCUMENTED** in the transport doc; middleware is `unity_instance_middleware.py`.

## Tool count

**CONFIRMED** by AST over every `@mcp_for_unity_tool` in `Server/src/services/tools/` at v10.3.0: **49** functions.

README "48 focused MCP tool entrypoints" is a **stale DOCUMENTED CLAIM**. The docs homepage "49 tools" matches the source count but is paired with a stale v10.0.0 banner.

The 49th relative to a mental model of "48 manage_* tools" is the always-on meta set, not one extra domain tool. Count by `group=`:

| Group | Default | Count | Tools |
|---|---|---|---|
| *(none, always listed)* | always | 5 | `manage_tools`, `set_active_instance`, `execute_custom_tool`, `manage_script_capabilities`, `debug_request_context` |
| `core` | on | 25 | `batch_execute`, `execute_menu_item`, `find_gameobjects`, `find_in_file`, `manage_asset`, `manage_build`, `manage_camera`, `manage_components`, `manage_editor`, `manage_gameobject`, `manage_graphics`, `manage_material`, `manage_packages`, `manage_physics`, `manage_prefabs`, `manage_scene`, `apply_text_edits`, `create_script`, `delete_script`, `validate_script`, `manage_script`, `get_sha`, `read_console`, `refresh_unity`, `script_apply_edits` |
| `asset_gen` | off | 6 | `generate_image`, `generate_model`, `generate_audio`, `import_model`, `import_model_file`, `blender_bridge` |
| `vfx` | off | 3 | `manage_shader`, `manage_texture`, `manage_vfx` |
| `scripting_ext` | off | 2 | `execute_code`, `manage_scriptable_object` |
| `testing` | off | 2 | `run_tests`, `get_test_job` |
| `docs` | off | 2 | `unity_docs`, `unity_reflect` |
| `animation` | off | 1 | `manage_animation` |
| `ui` | off | 1 | `manage_ui` |
| `probuilder` | off | 1 | `manage_probuilder` |
| `profiling` | off | 1 | `manage_profiler` |

`find_in_file`, the script editors, and `get_sha` use `unity_target="manage_script"` so they follow the `manage_script` toggle. `unity_docs` follows `unity_reflect`. **CONFIRMED** decorator args.

These are fat routers. String `case "..."` labels in the C# files (includes nested switches, so not a clean "action API" count):

| Router | Case labels in v10.3.0 | Note |
|---|---|---|
| `manage_physics` | 21 | Matches the skill text. |
| `manage_graphics` | 40 | Skill says 33. Skill is stale. |
| `manage_vfx` | 62 | Particles, trails, lines, VFX Graph. |
| `manage_ui` | 59 | UI Toolkit, not uGUI. uGUI is documented as "use batch_execute + components". |
| `manage_animation` | 28 | |
| `manage_camera` | 18 | Tier 2 is Cinemachine and no-ops without the package. |
| `manage_scene` | 18 | Includes scene-template names, not all top-level actions. |
| `manage_profiler` | 14 | |
| `manage_editor` | 12 live | `set_resolution` / `set_quality` are commented out; a regex still sees them. |
| `manage_asset` | 11 | |
| `manage_gameobject` | 6 | `create`, `modify`, `delete`, `duplicate`, `move_relative`, `look_at`. |
| `manage_build` | 8 | `build`, `status`, `platform`, `settings`, `scenes`, `profiles`, `batch`, `cancel`. |

C# `[McpForUnityTool]` attributes are fewer than 49 because several Python tools are aliases onto one Unity command (`manage_script` and friends). **CONFIRMED** attribute grep: on the order of 30 handler classes, almost all `AutoRegister = false` because the Python server already registered them.

## Default visible surface vs "full"

There is no profile literally named `full`. **CONFIRMED** absence in `TOOL_GROUPS` / `manage_tools`.

What "default" actually means:

1. Decorator default group is `core`. `DEFAULT_ENABLED_GROUPS = {"core"}`.
2. **HTTP startup:** non-core groups are `mcp.disable`'d so a new session sees **core (25) + always-on (5) = 30** until something re-enables groups. **CONFIRMED** `register_all_tools`.
3. **stdio startup:** all groups start **enabled**, then the server pulls Unity's per-tool toggles (`get_tool_states`) and hides what the Editor has off. The comment says a disabled group would otherwise stay invisible forever because stdio has no `register_tools` push. **CONFIRMED** same function.
4. Unity Editor toggles (`Window > MCP for Unity > Tools`) persist in **EditorPrefs** (machine-global, not per-project). First-seen default is `AutoRegister || IsBuiltIn`. Built-in types in `MCPForUnity.Editor` count as built-in, so the packaged tools default **on**, including non-core groups, unless the user toggled them off. **CONFIRMED** `ToolDiscoveryService` + `StringCaseUtility.IsBuiltInMcpType`.
5. On HTTP, Unity's `register_tools` push calls `PluginHub._sync_server_tool_visibility`, which can turn those Editor toggles back **on** at the server after the lean startup. So "HTTP starts at 30" is the cold server state, not necessarily the state after the Editor connects. **CONFIRMED** `plugin_hub.py`.
6. `manage_tools` then layers **per MCP session** visibility on top: `activate`, `deactivate`, `list_groups`, `sync` (pull Editor toggles), `reset` (back to core only). **CONFIRMED** `website/docs/guides/tool-groups.md` and the decorator.

Practical default the model sees:

- Fresh HTTP session before Unity connects: 30 tools. **CONFIRMED**
- After a normal Editor connect, with stock toggles: closer to all built-in tools whose EditorPrefs are still at the default ON, including testing, vfx, animation, ui, docs, profiling, asset_gen, probuilder. **INFERRED** from the default-on rule. Not measured against a live Editor on this machine.
- `probuilder` still fails at call time without `com.unity.probuilder`. **DOCUMENTED CLAIM**

The skill (`unity-mcp-skill/SKILL.md`) tells the agent: if a tool is missing, call `manage_tools(action="list_groups")` before debugging the connection. That text matches the code.

## Dynamic exposure and custom tools

User C# can add `[McpForUnityTool]` (AutoRegister defaults **true** for non-package types). Parameters come from `[ToolParameter]`. **CONFIRMED** `McpForUnityToolAttribute.cs`.

Two exposure modes, **CONFIRMED** in the skill and `register_all_tools` / `CustomToolService`:

- Project-scoped tools **on** (HTTP default path, and stdio when the Unity payload includes `is_built_in`): custom tools are **not** registered under their own names. The model lists `mcpforunity://custom-tools` and calls `execute_custom_tool(tool_name, parameters)`.
- Project-scoped tools **off**: they are registered as ordinary MCP tools.

Long-running custom tools set `RequiresPolling`, `PollAction` (default `"status"`), `MaxPollSeconds`. Python polls until a terminal status. Built-in asset-gen and package tools use the same attribute (`generate_*` 300s or 600s for audio; `manage_packages` polls `status`). **CONFIRMED** attributes.

`execute_custom_tool` is skipped entirely when the server is started with project-scoped tools disabled. **CONFIRMED** `register_all_tools`.

## Job system

Not one job framework. Four SessionState-or-memory trackers:

| Job | Where | Survives domain reload? |
|---|---|---|
| Test runs | `TestJobManager` + `get_test_job` | Yes, SessionState key `MCPForUnity.CurrentTestJobIdV1` / jobs blob. v10.3.0 re-registers Test Runner callbacks in `RestoreRunningJobCallbacks`. **CONFIRMED** |
| Package add/remove | `PackageJobManager`, max 10 jobs, 120s domain-reload timeout | SessionState `MCPForUnity.PackageJobsV1`. **CONFIRMED** |
| Player builds | `BuildJob` / `BatchJob` in memory: Pending, Building, Succeeded, Failed, Cancelled, Skipped. Status returns size, errors, warnings. | No SessionState on the job class itself. **INFERRED** lost on reload. |
| Asset gen | `AssetGenJobManager` plus provider poll (`fal`, Meshy, Tripo, Sketchfab, OpenRouter, local image). BYO API key. | Not fully read. Polling attribute says the Python side keeps asking `status`. |

Test polling is the one they operate at user scale. `run_tests` returns a `job_id`; `get_test_job` waits and can set `stuck_suspected`. Focus nudges are capped at `_MAX_JOB_NUDGES = 3` per job and can be disabled with `UNITY_MCP_DISABLE_FOCUS_NUDGE`. **CONFIRMED** `run_tests.py`. That cap is new relative to the still-open report filed against 10.1/10.2 (see recovery).

## Recovery after domain reload

This is the part that shows real users, not a design doc.

Both transports have an `[InitializeOnLoad]` resume handler with the same retry schedule: 0s, 1s, 3s, 5s, 10s, 30s. **CONFIRMED** `HttpBridgeReloadHandler.cs`, `StdioBridgeReloadHandler.cs`.

HTTP (`HttpBridgeReloadHandler`):

- Before reload, if HTTP is running, set SessionState `MCPForUnity.ResumeHttpAfterReload` and `ForceStop` synchronously so the socket is not left half-closed.
- The flag used to be EditorPrefs. Comment cites **#1229**: EditorPrefs is per-user and machine-global, so a second Editor could consume the flag, and a crash mid-compile resurrected the bridge on next launch. Moved to SessionState (survives reload, dies with the Editor process). A one-shot migration deletes the old EditorPrefs key.
- The flag is **not** cleared just because the bridge is down during a multi-pass compile. Clearing it there used to lose resume permanently.
- After reload, wait until `EditorStateCache.GetActualIsCompiling()` is false. Raw `EditorApplication.isCompiling` stays true for a whole play session under "Recompile After Finished Playing" (**#549**). Deferred reload (**#1276**) is the same class of bug.
- Do not restart if some other path already connected.
- On exhaustion, erase the flag and warn. A reload mid-retry kills the task and leaves the flag set, so the next domain retries again.

stdio (`StdioBridgeReloadHandler`):

- Resume flag is still **EditorPrefs** (`ResumeStdioAfterReload`), unlike HTTP. **CONFIRMED** asymmetry.
- Writes a heartbeat file `reloading` before teardown and `stopped` if resume gives up, so clients are not stuck on a zombie "reloading" status.
- Port policy in `StdioBridgeHost.Start`: if 6400 is `AddressAlreadyInUse` right after reload, **do not** silently move to another port. The Python client is pinned to the old port and would talk to an orphan forever (**#1173**). Retry the same port; only abandon it after a busy window.

Python HTTP side (`PluginHub._resolve_session_id`):

- On disconnect, in-flight `command_id` futures fail immediately with `PluginDisconnectedError` instead of waiting out `COMMAND_TIMEOUT`.
- If no session is up, wait up to **20s** (env `UNITY_MCP_SESSION_RESOLVE_MAX_WAIT_S`, ceiling 120s after **#1207**) polling every 50–250ms. The comment still says this is a **blind wait**, with an open TODO to detect Unity's real reload state. It cites **issue #657**. That TODO is still in v10.3.0 source. **CONFIRMED**
- Fast-path commands `read_console`, `get_editor_state`, `ping` fail in **2s** with `hint="retry"` so a compiling Editor does not eat Cursor's ~30s tool timeout. **CONFIRMED**

stdio command retry: `async_send_command_with_retry`. v10.3.0 also refreshes port discovery before reconnect backoff (PR #1345). Broker resend suppression (PR #1361): if the same payload arrives from a **different** connection while the original is queued, it is treated as the broker reconnecting and resending, and the work is not run twice. Same connection + same payload is still a real second command. **CONFIRMED** `StdioBridgeHost.IsBrokerResend`.

Editor readiness is a snapshot, not an epoch the client must echo:

- `EditorStateCache` increments `_sequence` inside the domain and **resets it to 0** in the static constructor, so the sequence dies on reload. **CONFIRMED**
- What does survive in SessionState: compile started/finished unix ms, a compile **count**, domain-reload before/after unix ms, external-change scanner state on the Python side. **CONFIRMED** field names and `CompileCountKey`.
- Python `editor_state` resource adds `advice.ready_for_tools`, `blocking_reasons` (`compiling`, `domain_reload`, `running_tests`, `asset_refresh`, `stale_status`), `recommended_retry_after_ms` (0 or 500), `recommended_next_action` (`none` or `retry_later`). Stale means the snapshot is **>2s** old, explicitly to cover unfocused-editor throttling. **CONFIRMED** `editor_state.py`.
- `preflight()` runs inside tools. It can wait a bounded 30s on compile, fail fast with `error="busy"`, `hint="retry"`, and optionally `refresh_unity` if external files are dirty. If state cannot be read, it **proceeds** rather than blocking. **CONFIRMED**

`refresh_unity` will not wait for "ready" across a Unity 6 domain reload when `compile=request`. The comment says that wait caused infinite compilation loops. It instead records whether compilation **started**, using the SessionState compile count so a short compile that begins and ends inside the reload is still observable (PR #1347, in this tag). Callers are told to poll `mcpforunity://editor/state` until `advice.ready_for_tools`. **CONFIRMED** `RefreshUnity.cs`.

## HTTP / socket recovery, summarized

| Failure | What they do now | Still open? |
|---|---|---|
| Domain reload drops the WebSocket | Editor resumes on a 6-step backoff; Python waits up to 20s for the new session | The wait is still timed, not state-driven (#657 TODO remains in source). |
| Domain reload drops stdio TCP | Same backoff; heartbeat file; retry **same** port (#1173) | Resume flag still EditorPrefs. Cross-editor clobber that #1229 fixed for HTTP is **not** fixed here. **INFERRED** from the key type. |
| Broker resends the in-flight command after reconnect | Suppress if payload and connection identity say "resend" (#1361) | No durable idempotency key. A resend that arrives after the first command **finished** can run again. **INFERRED** |
| Editor unfocused, tests look stalled | OS focus nudge, then restore. Cap 3 per job. Opt out via env. Windows skips if the project path cannot be resolved. | #1407 (filed on 10.1/10.2) is **still open**. v10.3.0 source matches several of its suggested fixes. Whether the reporter's Windows multi-editor loop is gone was **not** retested here. |
| PlayMode `run_tests` vs reload | SessionState job + callback rebind in 10.3.0 | #1394 still **open**, updated 2026-10-04 15:56 UTC (after the tag). Body is a 10.2.0 Windows repro: false failure while `TestResults.xml` passed, jobs stuck at `completed: 0`, one full "ping not answered" lockup. #1390 still **open**: Linux/Vulkan PlayMode tests hang the Editor (`futex_do_wait`, SIGKILL). EditMode and manual Play Mode were fine in that report. |
| Scene save/reload modal blocks the agent | No handler found in this pass | #1411 still **open** (10.2.0, stdio, Windows, Unity 6). |

Other scale fixes already in the tag, from release notes (behavior **CONFIRMED** only where the source was opened): console window filters no longer hide `read_console` entries (#1342); screenshot downscale matches source sRGB vs linear so Linear projects are not washed out (#1328, see capture); `execute_code` CodeDom reference leaks and duplicate refs patched across 10.1–10.2; Windows Store Python alias (#1324); Claude Code duplicate config keys and git worktrees (#1280).

## Async operations

- WebSocket commands carry `id` (UUID), `name`, `params`, `timeout`. Result message type is `command_result`. Default command timeout 30s. Caller `timeout_seconds` is clamped to 1s–1h, with +5s server cushion. **CONFIRMED** `plugin_hub.py`.
- stdio socket timeout default is **300s** per command (`UNITY_MCP_CONNECTION_TIMEOUT`), total across retries **600s**. The comment says the old 30s cut off imports and tests, forced a reconnect, and made the bridge restart. **CONFIRMED** `config.py`.
- Tests, packages, asset gen, and builds return a job id and expect a later status call. Asset gen and packages also have server-side poll loops. Tests are polled by the model via `get_test_job`, with the server optionally nudging focus while it waits.
- `batch_execute` takes up to 25 commands by default, Editor-configurable to 100. The skill's `parallel=True` is documented in the skill itself as a **hint**; Unity may still run them sequentially. **DOCUMENTED CLAIM** plus the skill's own caveat.
- Screenshot default path can be async (`ScreenCapture` writes a file, import scheduled when the file appears, 30s). Inline `include_image` paths are synchronous. **CONFIRMED** `ManageScene.CaptureScreenshot`.

## Command receipts

There is no persisted command receipt log and no client-supplied idempotency token on the wire.

Closest equivalents, all **CONFIRMED** unless noted:

- In-flight map `command_id -> future` inside the Python process. Lost on server restart.
- Response shape `MCPResponse`: `success`, `message`, `error`, `data`, optional `hint`. The only documented hint value is `"retry"`. Busy payload also carries `retry_after_ms`. This is a hint, not a structured `retryable: bool` plus a reason code the client must obey.
- `editor_state.advice` is the readiness hint (see above). It is recomputed per read. It is not an epoch the next command must present.
- Job ids for tests and packages are the durable "receipts", stored in SessionState, and they are exactly where the open bugs are.
- SHA-256 precondition on `apply_text_edits` / `get_sha` is an edit receipt for scripts (`stale_file` if the file moved). **CONFIRMED** tool description and the skill's error table.
- Stdio resend suppression is a queue-time duplicate filter, not a receipt.

## Skills, prompts, resources

**Resources: 25.** **CONFIRMED** AST of `@mcp_for_unity_resource`. The docs homepage "25 resources" matches. URIs:

`editor/state`, `editor/selection`, `editor/windows`, `editor/active-tool`, `editor/prefab-stage`, `project/info`, `project/layers`, `project/tags`, `instances`, `menu-items`, `tests`, `tests/{mode}`, `tool-groups`, `custom-tools`, `scene/cameras`, `scene/volumes`, `scene/gameobject/{id}`, `.../components`, `.../component/{name}`, `scene/gameobject-api`, `prefab/{path}`, `prefab/{path}/hierarchy`, `prefab-api`, `pipeline/renderer-features`, `rendering/stats`.

C# resource handlers are 19 names; a few Python resources are server-only (`tool-groups`, `custom-tools`, `instances`, API doc resources). **CONFIRMED**

**MCP prompts:** none. No `@mcp.prompt` (or similar) registration under `Server/`. **CONFIRMED** by search.

**Skills:** markdown, not a protocol feature. `unity-mcp-skill/SKILL.md` is an operator guide (resource-first workflow, screenshot recipes, batching, pagination, multi-instance, error table). Copies also live under `.claude/skills/`. There is a Blender skill. `.claude/prompts/` holds three natural-language eval suites (`nl-unity-suite-*`, `nl-gameobject-suite`), not user-facing MCP prompts.

The skill is ahead of the README and behind some routers (graphics "33 actions" vs 40 case labels). It tells agents not to call `refresh_unity` after `create_script` / `script_apply_edits` because those already request compilation. That matches `RefreshUnity.cs` comments about redundant refresh loops on Unity 6.

## Capture and image handling

Screenshots are a `manage_camera` action that delegates to `ManageScene` / `ScreenshotUtility`. **CONFIRMED**

| Mode | Behavior |
|---|---|
| Default `capture_source=game_view` | File under the project (default PNG name `screenshot-yyyyMMdd-HHmmss.png`). jpg/jpeg extensions are allowed in `BuildFileName`, but the encoder used on the paths read is `EncodeToPNG`. |
| `include_image=true` | Base64 **PNG** returned as MCP `ImageContent` (`mimeType="image/png"`). Longest edge defaults to **640** (`max_resolution`, never upscales). Batch surround/orbit returns several images. **CONFIRMED** `extract_screenshot_images`. |
| Specific `camera=` | `Camera` render into a texture. Does not include UI Toolkit overlays. |
| `include_image` while **playing**, no specific camera | `ScreenCapture.CaptureScreenshotAsTexture` after `WaitForEndOfFrame`, pumped with `EditorApplication.Step` (max 5 steps). Comment: an immediate read happens before UITK composites. `Step()` pauses play mode; they restore the previous pause bit. This is the composited path (PR #1132, #1040). |
| `include_image` in **edit mode** | Falls back to a camera render. UITK overlays are not in that image. **CONFIRMED** branch in `CaptureScreenshot`. |
| `capture_source=scene_view` | Editor viewport, gizmos. No `super_size`, no batch, no `view_position`. Batch mode (Unity `-batchmode`) rejects scene view. |
| `batch=surround` / `orbit` | Several angles, one call. |
| Color | Downscale blit uses the source texture's sRGB vs linear flag. A Linear project used to wash out because a default sRGB RT encoded on store. Fixed for #1328. **CONFIRMED** `DownscaleTexture`. |
| Path safety | Folder must resolve inside the project root or the call throws. **CONFIRMED** `ResolveFolderAbsolute`. |

Not JPEG. Not Overlay-correct in edit mode. Play-mode compositing is a 5-step `EditorApplication.Step` spin, which is a different mechanism from waiting on the real player loop, and it has a pause side effect they paper over.

`generate_image` is a different feature: a paid provider call (OpenRouter / fal) that imports a texture. It is not a scene capture.

## Compile

- `create_script`, `delete_script`, `apply_text_edits`, `script_apply_edits`, `validate_script`, `manage_script`, `get_sha` are separate MCP tools sharing the `manage_script` Unity target.
- Text edits are exact line/column ranges, 1-indexed, optional `precondition_sha256`. Structured method/class edits are `script_apply_edits` with anchors. The tool description tells the model to prefer anchors. **CONFIRMED**
- `validate_script` and the Roslyn optional path (`CustomTools/RoslynRuntimeCompilation`, `USE_ROSLYN`) exist because CodeDom/`mcs` has been a stream of Windows and reference bugs (BOM phantom error, duplicate refs, missing `Unsafe`, severity culture). v10.3.0 includes "upper-case Roslyn severity with the invariant culture" and "drop Workspaces-layer formatting so USE_ROSLYN compiles with the installer's DLLs".
- `refresh_unity` modes include `compile=none|request` and `wait_for_ready`. Unity 6 refusal to block across reload is above.
- `read_console` is the compile-error channel. v10.3.0 stops the Console window's own filters from hiding rows.

## Execution

`execute_code` is **off** until the `scripting_ext` group is activated (and until the Editor toggle allows it). **CONFIRMED** group assignment.

It compiles a wrapper class `MCPDynamicCode.Execute` with CodeDom or Roslyn (`compiler=auto`). Limits: 50_000 characters. Actions: `execute`, `get_history`, `clear_history`, `replay`. History is in memory only (50 entries, 500-char preview) and dies on domain reload.

Safety is a **case-insensitive substring denylist** (`File.Delete`, `Directory.Delete`, `FileUtil.DeleteFileOrDirectory`, `AssetDatabase.DeleteAsset`, `AssetDatabase.MoveAssetToTrash`, `EditorApplication.Exit`, `Process.Start`, `Process.Kill`, `while(true)`, `for(;;)`). The caller passes `safety_checks` and the default true can be set false. **CONFIRMED** `ExecuteCode.cs`. This is not a sandbox. Concatenation, reflection, and `System.IO.File` methods not in the list are not covered. **INFERRED** from the list being exact substrings.

Mono cannot unload the dynamic assembly. They cache 64 compiled snippets keyed by source so identical calls do not leak an assembly per call (#1351). Cache clears on domain reload. **CONFIRMED**

`execute_menu_item` runs arbitrary Editor menu paths. That is the other open execution door. **CONFIRMED** tool exists; the menu catalog is the `menu-items` resource.

## Context and project inspection

Resource-first is the intended loop: `editor/state` → `project/info` → `find_gameobjects` (ids only) → `scene/gameobject/{id}` and component resources (paginated) → mutate → `read_console` / screenshot.

`project/info` is described as mostly static (root, Unity version, platform) and is also how the server learns the project root for the external-change scanner. The scanner marks `assets.external_changes_dirty` when files change outside Unity. **CONFIRMED** `editor_state.py`.

`find_gameobjects` plus paged `manage_scene(action="get_hierarchy")` is the inspection API. `unity_reflect` searches live loaded types; `unity_docs` fetches docs.unity3d.com and does not need a live Editor. The skill's trust order is reflection > project assets > docs. **DOCUMENTED CLAIM** in the skill; both tools exist in source.

`manage_components` resolves property names with a fuzzy match (Levenshtein). Beta after this tag further fixes native `m_` field names. On v10.3.0 that fix is **not** in the tree. **CONFIRMED** by the beta log, not by reading the patch.

Selection, open windows, active editor tool, prefab stage, tags, layers, cameras, volumes, renderer features, and rendering stats are resources, not tools.

## Input

No input-injection tool. Search of `MCPForUnity/Editor` for `Input.`, `SendEvent`, keyboard/mouse simulation found nothing that drives the Input System or the old Input manager. **CONFIRMED** absence in this tree.

What exists instead:

- `manage_editor` `play` / `pause` / `stop` (in-editor Play Mode only).
- `manage_physics` `simulate_step` (edit-mode `Physics.Simulate` / `Physics2D.Simulate`) and `apply_force`.
- Animator `play` / parameter sets. Triggers are documented as runtime-only no-ops in edit mode.
- UI Toolkit `modify_visual_element`. Not a click.

Driving a game, sending keys, or attaching to a built player is **not** in this product.

## Build

`manage_build` is in `core`. Actions above. `HandleBuild` / `BuildRunner` call the Editor build pipeline. v10.2.0 notes: save assets and scenes before `BuildPlayer`, but skip never-saved scenes instead of opening the save modal (#1340, #1356). Jobs are in-memory. Batch builds exist. Output is a player build on disk, not a connected player.

`manage_editor` `deploy_package` / `restore_package` is a separate "copy this package into the project" path (`PackageDeploymentService`), not a player deploy.

## Runtime / player

Editor-only bridge. **CONFIRMED** by `[InitializeOnLoad]`, `Editor/` asmdef placement, and the absence of a player connection type.

Play Mode side effects they have already hit:

- Composited screenshots only work correctly while playing, and the capture steps the player loop.
- PlayMode tests enter play mode, which itself domain-reloads, which drops the bridge. That is the core of #1390 and #1394.
- `TestRunnerNoThrottle` forces the Editor interaction mode so an unfocused Editor does not throttle tests, and restores it afterwards. It re-applies across domain reload via SessionState. **CONFIRMED** file header and the test-job restore path.
- Profiler and Frame Debugger actions exist under `manage_profiler` (group off by default in the decorator, on by default in the Editor toggle unless the user disabled it).

## Installation

**DOCUMENTED** in `website/docs/getting-started/install.md`, versions **CONFIRMED** in `package.json` (`unity: 2021.3`, version `10.3.0`).

1. Git URL `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main` (or `#beta`, or pin `#v10.3.0`). Needs git on PATH. v10.3.0 adds a setup-window explanation when the Git URL fails.
2. OpenUPM `openupm add com.coplaydev.unity-mcp`.
3. Asset Store listing id 329908, "MCP for Unity". **DOCUMENTED CLAIM** in the install page. The store page was not opened. Release asset is also a Claude desktop `.mcpb` (v10.3.0 file is 40865 bytes, **52** downloads on the release day; v10.2.0 had **1614**; v10.1.2 **1289**; v10.1.0 **1101**). Those are GitHub asset download counts, not unique editors. **CONFIRMED** GitHub Releases API.

Wizard: Window → MCP for Unity. It checks Python 3.10+ and `uv`, can install `uv`, then "Configure Selected" / "Configure All Detected Clients". Server lifecycle (start, stop, HTTP vs stdio) is in that window. Python is an external process the Editor launches (`ServerManagementService`, pid files, process kill). Headless HTTP launch was patched in the 9.7 line.

Supported Unity: package says 2021.3. Release history through 2026 is full of 2021.3 / 2022.3 / Unity 6.0–6.5 / 6000.6 compile fixes (`InstanceID`, VisionOS enums, disabled built-in modules). They are actively paying the cost of that range. **CONFIRMED** as release-note titles; not every compat `#if` was read.

## Supported clients

**24** configurator classes. **CONFIRMED** by directory listing. The homepage "24 clients" matches.

Antigravity, Antigravity IDE, Cherry Studio, Claude Code, Claude Desktop, Cline, CodeBuddy CLI, Codex, Copilot CLI, Cursor, Gemini CLI, Kilo Code, Kimi Code, Kiro, OpenClaw, OpenCode, Pi (added in v10.3.0), Qwen Code, Rider, Trae, Trae CN (added in v10.3.0), VS Code, VS Code Insiders, Windsurf.

Codex HTTP was broken and restored across 10.1.x (#1193). Antigravity's config path moved in a client 2.x migration and they patched the configurator (v9.7.1). This is ongoing client-churn work, not a one-time list.

## Performance claims

| Claim | Label |
|---|---|
| `batch_execute` is "10–100x faster than sequential calls" | **DOCUMENTED CLAIM** in the skill. No benchmark in the tag was opened. The same skill says `parallel=True` may still run sequentially. |
| Skipping unchanged editor-state rebuilds avoids "28ms GC spikes" | **DOCUMENTED** as a code comment in `EditorStateCache`. Not re-measured. |
| TypeCache discovery is cheaper than scanning every loaded assembly | **CONFIRMED** as the v10.2/v10.3 change. No before/after numbers in the notes that were read. |
| Hiding non-core tools saves tokens and "the wrong-tool rate drops measurably" | **DOCUMENTED CLAIM** in the tool-groups guide. No measurement is in that page. |
| Fast-fail 2s on ping / console / state | **CONFIRMED** constant. It is a timeout policy, not a latency SLA. |
| 16MB TCP buffer | **CONFIRMED** config. |

Nothing in the inspected tree is a published latency or throughput suite for the bridge itself. `tools/stress_editor_state.py` exists; it was not run.

## Security

**CONFIRMED** unless noted.

- Vulnerability reports go to security@coplay.dev, not GitHub issues. They claim 3 business days to acknowledge, 10 to assess. Only latest `main` and latest `beta` are supported. **DOCUMENTED CLAIM** (`SECURITY.md`). No advisory list was pulled.
- Local HTTP: loopback, no API key. Anyone on the machine who can open `127.0.0.1:8080` can drive the Editor, including `execute_menu_item` and, if the group is on, `execute_code`.
- LAN bind and plaintext remote HTTP are explicit opt-ins. Remote mode fail-closes if the auth service is down (WebSocket close 4401 / 4403 / 1013). Validation is an external HTTP POST with an in-memory cache (TTL 300s) keyed by the raw API key. **CONFIRMED** `remote-auth.md` and `plugin_hub.py`.
- `execute_code` denylist is bypassable by construction, and `safety_checks=false` turns it off. See Execution.
- Screenshot and asset-gen zip extract (`SafeZipExtractor.cs` exists) show they have started thinking about path escape. The zip extractor was not audited line by line.
- API keys for asset gen go through `SecureKeyStore` (Windows / macOS / Linux secret-tool backends). Not audited.
- Telemetry is **on by default**. Opt out with `DISABLE_TELEMETRY`, `UNITY_MCP_DISABLE_TELEMETRY`, `MCP_DISABLE_TELEMETRY`, or EditorPrefs `MCPForUnity.TelemetryDisabled`. The homepage component says "No account, no telemetry by default". That sentence is **false relative to the source**. `website/docs/architecture/telemetry.md` tells the truth (opt-out) but still says the Editor toggle is "Coming Soon", while the key is only listed in a raw EditorPrefs window. Events POST to `https://api-prod.coplay.dev/telemetry/events`. They say they send tool name, timing, success/failure, Unity version, platform, an anonymous UUID, and truncated errors, and not script contents or paths. The "we don't collect paths" line was **not** verified against every event field. Connection to Coplay's cloud is the default, not an opt-in.

## Adoption evidence

| Signal | Value | Label |
|---|---|---|
| GitHub stars / forks / open issues | 14689 / 1532 / 84 on 2026-10-04 | **CONFIRMED** API |
| Watchers | 81 | **CONFIRMED** API. Not a user count. |
| Age | Public since 2025-03-18. Tag history runs at least v6 (Oct 2025) through v10.3.0 (Oct 2026). | **CONFIRMED** API created_at; tags page and releases API for the 9.6–10.3 range. |
| `.mcpb` downloads | See installation. Tens to low thousands per recent tag. | **CONFIRMED** as download counters only. |
| Discord | Invite linked from README and coplay.dev. Member count | **UNKNOWN** |
| Asset Store installs | Listing is documented. Install count | **UNKNOWN** |
| OpenUPM installs | Package name is real. Download count | **UNKNOWN** |
| In-product DAU | Their own analytics doc says DAU/WAU exists in the telemetry backend and is **not** exposed on the public dashboard yet. | **DOCUMENTED CLAIM** that they collect it. The number is **UNKNOWN**. |
| ACM citation | README cites Wu & Barnett, "MCP-Unity: Protocol-Driven Framework for Interactive 3D Authoring", SA Technical Communications '25, DOI `10.1145/3757376.3771417`. | **DOCUMENTED CLAIM**. Paper not opened. It is not adoption. |
| Issue quality | Long, instrumented reports (#1394 is a 23-call matrix with `TestResults.xml` cross-check; #1407 quotes the PowerShell bug). Maintainers ship patch releases that name those failure modes. | **CONFIRMED** that this volume of field bugs exists. Not a user census. |

Commercial Coplay testimonials on coplay.dev (named engineers, studios) are about the **paid plugin**, not about this MCP server. Do not cite them as MCP-for-Unity adoption.

## What changed because people actually used it

The useful pattern in their history is not the tool catalog. It is a year of bridge failures that only show up with multiple Editors, unfocused windows, Unity 6 domain reloads, and clients that time out at 30s:

- Global EditorPrefs is the wrong place for "resume after this reload" (#1229). SessionState is the right scope. They fixed HTTP and left stdio on EditorPrefs.
- After reload the old socket still owns the port for a few hundred milliseconds. Switching ports "to be helpful" strands the client (#1173).
- A reconnecting client resends the command it thinks timed out. Without a duplicate check the Editor does the work twice (#1361).
- `EditorApplication.isCompiling` lies for the whole play session under a common preference, so any gate that trusts it wedges the bridge until play mode exits (#549, #1276).
- Waiting inside the tool call that *caused* the domain reload deadlocks Unity 6 compile (# refresh_unity comment, #1347). The call must return, and a later read must observe a **SessionState** compile counter because in-memory flags are gone.
- Test jobs and package jobs have to be rehydrated and their native callbacks re-bound, or the model sees a false failure while the real run passed (#1394, still open as a user report).
- "Nudge the Editor to the foreground so the player loop runs" becomes a desktop-stealing loop when more than one Editor is open (#1407). They added a cap and an opt-out after the report; the report is still open.
- PlayMode tests on Linux/Vulkan can lock the Editor hard enough to need SIGKILL (#1390, still open). EditMode is fine.
- Screenshot color space and "capture before UITK composites" are the kind of bugs you only get from agents looking at the image, not from unit tests of the file path.
- Client config paths rot (Antigravity 2.x, Codex HTTP, Claude Code worktrees, Trae CN). A 24-client matrix is a maintenance job, not a feature checkbox.
- Marketing numbers drift (48 vs 49, homepage stuck on v10.0.0, "no telemetry by default"). At this star count the README is not the source of truth.

## What Nexus is objectively better at

Grounded only in the Nexus baseline stated for this sprint, against v10.3.0 source.

- **Readiness epochs vs a resetting sequence.** Coplay's `sequence` is an in-domain counter that goes back to 0 on every reload. The Python advice block is recomputed from a snapshot and treats ">2 seconds old" as stale. Session resolve is still a blind 20s poll with a TODO pointing at #657. Nexus epochs are the thing their own comment says they still need. They persisted compile timestamps because in-memory state lies; an epoch is that idea carried through to the client.
- **Retry envelope.** Coplay's `hint="retry"` plus `retry_after_ms` is a string convention on some paths (`busy`, disconnect, fast-fail) and a different shape on `editor_state.advice`. It is not one envelope every command returns. Nexus already has retryable/hint as a single contract.
- **Capture.** Coplay's inline image is PNG, default longest edge 640, and UI Toolkit compositing only happens in Play Mode via `WaitForEndOfFrame` plus `EditorApplication.Step` (which pauses play mode as a side effect). Edit-mode `include_image` is a camera render and misses overlays. They already shipped a Linear-colorspace washout bug. Overlay-correct JPEG is ahead of this.
- **Not shipping a default-on phone-home.** Coplay telemetry posts to `api-prod.coplay.dev` unless the user opts out, while the marketing homepage says there is no telemetry by default.
- **A smaller semantic surface as the thing the model is supposed to call.** Coplay's "30 core tools" are still action routers (`manage_graphics` alone has 40 case labels). That is the problem their own tool-groups page admits. Nexus's three canonical commands are the opposite bet. This is not a claim that the legacy Nexus catalog is small.

## What Nexus is objectively worse at

- **Breadth that is already wired and field-patched.** One Editor plugin covers scenes, GameObjects, components, script edits with SHA preconditions, assets, prefabs, materials, physics (3D and 2D), UI Toolkit, animation, VFX, profiler, packages, builds, cameras including Cinemachine, ProBuilder, docs/reflection, and a 25-resource read side. Nexus does not have that catalog on the canonical surface.
- **Multi-client and multi-Editor routing that people use.** HTTP hub, `Name@hash`, per-session active instance, 24 client configurators, an install wizard that writes their configs. The configurators are how they absorbed Codex, Antigravity, and Trae breaking changes.
- **Adoption and the bug list that comes with it.** ~14.7k stars, a year of patch releases, Asset Store and OpenUPM distribution, and user reports that include log excerpts and NUnit XML. Nexus does not have that field sample. Several of their worst bugs are still open; the advantage is that the failure modes are public.
- **Async jobs for tests, packages, and builds**, with ids the model can poll, instead of holding one MCP call across a domain reload.
- **An agent-facing skill** that says "read state, then act, then screenshot" and tells the model what to do when a tool is hidden. Nexus has no equivalent operator skill in this comparison.
- **Edit-mode physics step, light baking, Frame Debugger, memory snapshots.** Out of scope for three semantic commands; still capability Coplay ships and Nexus's canonical surface does not.

## What Nexus could copy cheaply

These do not require picking the unresolved architecture and they are not a façade.

- **SessionState, not EditorPrefs, for anything that means "this Editor process, after the next reload."** They learned this the hard way (#1229) and only applied it to HTTP.
- **Do not change the listen port on the first `EADDRINUSE` after a reload.** Retry the same port (#1173).
- **Treat "same payload, different connection, command still queued" as a resend**, and do not run it twice (#1361). Pair it with the epoch so a resend after completion is a no-op rather than a second execution.
- **Return from the call that triggers compilation.** Do not block that call across the reload (Unity 6 infinite compile). Put the "did compile start" bit in SessionState because statics will be gone (#1347).
- **A compile counter / domain-reload timestamp beside the epoch**, so a reload that finishes before the client polls is still visible. Coplay has the timestamps and then throws the sequence away.
- **Fast-fail the status read** (their 2s ping/state/console) so a compiling Editor does not sit in a client-side 30s tool timeout. The response should be the existing retryable envelope, not a new protocol.
- **SHA precondition on script text edits** (`get_sha` / `precondition_sha256`). Small, and it stops the stale-edit class their skill already documents.
- **Cap and disable any focus stealing.** If Nexus ever nudges the Editor, copy `_MAX_JOB_NUDGES` and `UNITY_MCP_DISABLE_FOCUS_NUDGE`, not the original unbounded nudge.
- **Screenshot path confinement** to the project root, and the sRGB-vs-linear blit flag. The JPEG/overlay path stays Nexus's; the path check and the color flag are still worth having.
- **One operator note** that says which calls are hidden and how to ask for them, written against the real registry. Their skill does this; their README does not. Do not invent a second command router to make the note true.

## What would require architecture change

- Replacing or wrapping the canonical commands with Coplay's 49 entrypoints and `manage_*` action strings. That is a façade, and it is out of scope while the architecture is unresolved.
- A Python FastMCP process plus a WebSocket plugin hub plus a legacy TCP bridge. Nexus is not that shape.
- Per-MCP-session tool visibility (`manage_tools` groups) on top of a shared Editor. Useful at their client count; it is a server feature, not a tweak.
- Push registration of arbitrary user C# tools (`execute_custom_tool` / `[McpForUnityTool]`), including server-side polling of `status`.
- In-Editor arbitrary C# execution (`execute_code`) and arbitrary menu execution as first-class tools.
- Multi-tenant remote mode with an external API-key service.
- BYO-key asset generation (fal, Meshy, Tripo, OpenRouter) and the Blender socket bridge.
- A 24-client config writer. Each client is a small class, but the matrix is a product commitment (they are still patching paths in 10.3.0).
- PlayMode test running that survives the domain reload it causes. They have most of the mechanism and the bug is still open. Copying it means owning #1390 and #1394.

## What should NOT be copied

- **The homepage lie and the README count.** "No telemetry by default" is false. "48 tools" is stale. Do not publish a number the registry does not match.
- **Default-on telemetry to a vendor cloud**, especially while the settings doc still says the toggle is "Coming Soon".
- **`execute_code`'s denylist**, and the `safety_checks=false` switch. A substring block list that the caller can disable is not a security boundary. It will be what a later incident report quotes.
- **Blind 20s session polling** as the recovery design. They left a TODO on it. Nexus epochs exist so this does not need to be reinvented as a sleep.
- **Resetting the readiness counter on domain reload** and then calling the snapshot "the" state. That is the bug their timestamps are compensating for.
- **EditorPrefs for per-Editor resume flags.** They already know this.
- **Unbounded or even "helpful" focus nudges** as a way to unstick the player loop. It steals the wrong window (#1407). Prefer `runInBackground` / test no-throttle inside the Editor.
- **Holding a tool call across play-mode entry** on Linux/Vulkan. #1390 is a hard hang. A job id plus a poll is necessary but not sufficient; the poll is what #1394 shows failing.
- **Fat `manage_*` routers as the way to grow coverage.** Their own groups page says 48 tools dilute routing, and the groups are an admission, not a fix: the Editor toggle defaults the packaged tools **on**, and HTTP re-syncs those toggles after the lean startup. The model often still sees the fat catalog.
- **stdio and HTTP recovery implemented twice and only half-fixed.** HTTP uses SessionState; stdio still uses EditorPrefs. Two bridges means every reload bug lands twice.
- **A commercial assistant and an open bridge that share a Discord, a telemetry host, and a README sponsor line without saying which product a testimonial is about.** Fine for them; it makes adoption numbers unusable. Keep Nexus claims on the bridge that was actually measured.

## Not the same project — IvanMurzak/Unity-MCP

Included only so later notes do not merge the two "Unity MCP" repos.

| | CoplayDev/unity-mcp | IvanMurzak/Unity-MCP |
|---|---|---|
| Stars (pages fetched 2026-10-04) | 14.7k | 4.4k |
| Package | `com.coplaydev.unity-mcp` | `com.ivanmurzak.unity.mcp` |
| Unity window | Window → MCP for Unity | Window → AI Game Developer |
| Server | Python `mcpforunityserver`, user-installed via `uv` | C# binary auto-downloaded to `Library/mcp-server/`, also NuGet and Docker image `ivanmurzakdev/unity-mcp-server` |
| Editor ↔ server | WebSocket (HTTP mode) or TCP (stdio mode) | **SignalR** |
| Client transport | stdio or HTTP | stdio or streamable HTTP |
| Min Unity | 2021.3 | Wiki says **2022.3**, server version **0.76.1** as of the wiki edit dated 2026-05-29 |
| Install | git path `/MCPForUnity`, OpenUPM, Asset Store | unitypackage, OpenUPM, or git path `/Unity-MCP-Plugin/Assets/root` |

**DOCUMENTED** from that repo's wiki pages (Installation, Server Setup, Troubleshooting) fetched with this teardown. Its source was not cloned. Nothing above about Coplay's tools, jobs, or open issues applies to it.
