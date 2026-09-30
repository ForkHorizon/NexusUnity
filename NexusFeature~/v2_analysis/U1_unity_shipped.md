# U1 — What Unity has SHIPPED for agents & editor automation (as of 2026-09-30)

Author: Unity Research 1 of 2. Read-only research; nothing in the repo was modified.

## Source key (used as tags below)

| Tag | Source | Notes |
|---|---|---|
| **[SK]** | https://github.com/Unity-Technologies/skills — `skills/unity-cli/SKILL.md`, `CHANGELOG.md`, `references/integration-advanced.md`, `references/build-run-test.md` (HEAD 36e1a6a, 2026-09-29) | Primary, Unity-authored |
| **[PL]** | https://github.com/Unity-Technologies/unity-agent-plugin (HEAD 9c01e8d, 2026-09-29; manifest 0.1.6-beta) | Primary |
| **[PV7]** | `com.unity.pipeline` **0.7.0-exp.1** full source + `Documentation~` + `CHANGELOG.md`, vendored verbatim in https://github.com/bigkaka111-oss/ReactorCrystal/tree/main/Packages/com.unity.pipeline (package.json `repository.revision 41bb021d…`, internal Unity git URL) | Primary Unity code, third-party mirror. Unity's docs site is blocked here, so this is the best available ground truth |
| **[PV6]** | 0.6.0-exp.1 package.json in https://github.com/AChen1111/TCGGameDem0 (GitHub code search) | Same kind of mirror |
| **[F6] [F7] [F8]** | Forum announcements for Pipeline 0.6 / 0.7 / 0.8: https://discussions.unity.com/t/…/1735626 , /1736536 , /1737832 | Search snippets only |
| **[B11]** | https://discussions.unity.com/t/unity-cli-1-0-0-beta-11-is-rolling-out/1737353 | Search snippet |
| **[MCPD]** | https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli | Search snippet |
| **[P08]** | https://github.com/JamesVeug/UnityBuildUploader/pull/113 | Real 0.8 migration PR |

---

## 1. Unity CLI (`unity` binary)

**Status:** public beta, latest **1.0.0-beta.11** (announced ~2026-09-22) [B11]. The [SK] skill is still pinned to beta.10 (2026-09-14). It runs on macOS, Linux and Windows. Install with `curl -fsSL https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.sh | UNITY_CLI_CHANNEL=beta bash` or the matching `install.ps1`. There are also `.deb`/`.rpm` repos and an AppImage, and the Hub now installs the CLI automatically [SK][PL]. The binary is a .NET app with Sentry crash reporting and an always-on anonymous usage ping [SK]. No source says it needs a Unity AI entitlement. The docs put it next to AI but never gate it; treat "free" as true in practice but UNVERIFIED against a formal statement.

**Version history.** Dates come from the [SK] CHANGELOG unless marked otherwise.
| Version | Date | What it added |
|---|---|---|
| 0.1.0-beta.6 | ≤2026-06 | cloud, proxy, analytics, templates, `status`, build versioning |
| 0.1.0-beta.7 | 06-17 | `license`, `hub install`, **`unity test`** (EditMode/PlayMode), Android signing on `build`, `projects clone/link` |
| 0.1.0-beta.8 | 06-25 | **`unity mcp`** stdio server and `mcp configure` for 16 clients. **`pipeline`/`command`/`status` promoted to production**, and Pipeline now resolves from the UPM registry |
| 1.0.0-beta.1/2 | 07-21 | re-baseline to 1.0. `unity shell` REPL, `unity list`, `pipeline upgrade/list-versions/--package-version`. `--instance` removed; Editors are auto-discovered through the lockfile and token |
| beta.3 | 07-24 | `unity run --command <name>` (one-shot `[CliCommand]`), `editors running`, **`shell --protocol ndjson`**, `--json`, docs for authoring `[CliCommand]`, Safe-Mode recovery |
| beta.4 | 08-06 | **`unity command --detach` + `unity job wait/status/cancel`**, live progress, `test --report-format junit`/`--coverage`, `build --profile`, `projects exec` |
| beta.5 | 08-13 | fixes only. Skill docs added `skill install/refresh`, `editors prune/verify`, `projects clean`, `templates pack`, `unity command --query/--tag/--detail/--group_by/--limit` |
| beta.6 | 08-19 | build `--timeout` + heartbeat, `doctor --ci`, `cache key`, `--format github`, `test --shard`, `collaboration` |
| beta.7 (withdrawn) / beta.8 | 08-25 / 09-01 | `vcs` verb family, `plugin install/remove/upgrade`, `skill install --local` mirrors the package's `unity-pipeline` skill |
| beta.9 | 09-08 | `skill show`, `--color`, `auth consumers/revoke`, `config get/set`, `test --affected` |
| beta.10 | 09-14 | `assets inspect`, `build --list-targets/--create-profile`, `build run`, `open --wait`, `ProjectSettings/UnityCliConfig.json`, **`unity command --result-only`**, `unity context`, **`unity watch test`**, `unity commands`, `vcs blame`. MCP now falls back to a desktop screenshot when the main thread is blocked, and announces `tools/list_changed` |
| **beta.11** | ~09-22 | quick compile check that skips the full build, `.unitypackage` export/import, version-matched docs lookup, `pipeline cloud-build` / `pipeline automation` (read-only), `run --log-file`. Installers default to beta [B11] |

**Command groups** [SK]: `auth`, `license`, `cloud` · `editors`/`install`/`modules`/`install-modules` · `projects` (list/create/clone/open/link/upgrade/export/import/size/clean/exec), `releases`, `templates`, `assets inspect` · `config`, `context`, `hub install` · **`run`, `test`, `build` (+`build run`), `watch test`** · `logs`, `doctor`, `env`, `version`, `cache`, `ci init`, `bug`, `self-update`, `diagnose proxy|accelerator` · **`mcp` (+configure), `skill install|refresh|show`, `plugin`, `pipeline`, `command`, `commands`, `status`, `list`, `shell`, `job`** · `vcs …` (git, UVCS, affected, blame…) · `collaboration`.

Details that matter for Nexus:

- **`unity command`** forwards a call to a live Editor's Pipeline server. The default timeout is 30 s (`--timeout`). Targeting uses `--project-path`; a Player is targeted with `--runtime <name>` or `--runtime-path`. If several Editors match, it fails with `AMBIGUOUS_EDITOR` and returns `data.candidates`. A round trip against a warm Editor takes about **200–600 ms**, with no recompile and no domain reload [SK integration-advanced].
- **`unity eval`**: Unity tells shell-capable agents to use `unity command`/`unity eval` rather than MCP, because they are "faster, fewer tokens" [MCPD]. The skill itself documents `unity command eval '<C#>'` and `eval_file` [SK].
- **`unity shell --protocol ndjson`** keeps one CLI process warm. Clients send `{id, argv}` and get back `{id, exitCode, envelope}`. Unity marks it "trusted input only" [SK].
- **`unity mcp`** is a stdio server. It exposes whatever commands the connected Editor registers, notifies clients with `tools/list_changed`, and falls back to an OS desktop screenshot for `capture_*_view` when the Editor is blocked by a modal [SK].
- **`unity test`** exit codes: 8 means tests failed, 6 means infrastructure failure. It also supports `--retries` with flake reporting, `--rerun-failed`, `--affected --since <ref>` (a lower-bound approximation) and `--shard`. `unity watch test` is a local loop over `--affected` [SK build-run-test].
- **`unity skill install <client>`** covers claude-code, claude-desktop, grok, cursor, windsurf, vscode, cline and codex. With `--local` it also mirrors the package's own `unity-pipeline` skill from `Library/PackageCache` [SK].
- **Safe Mode:** when there are compile errors, the Pipeline package does not load, so `command`/`status`/`mcp` cannot connect. `unity pipeline list` detects this state [SK].
- **Known blind spot:** a sandboxed agent shell can hide a running Editor. The CLI does not tell this apart from "no Editor" [SK].

## 2. `com.unity.pipeline` (experimental)

**Versions:** 0.1.0-exp.1 (06-09), 0.2.0-exp.2 (06-24, first published), 0.3.0-exp.1 (07-13, bulk command set), 0.4.0-exp.1 (07-23), 0.5.0-exp.1 (08-10), 0.6.0-exp.1 (08-31), **0.7.0-exp.1 (09-10)** [PV7 CHANGELOG], **0.8.0-exp.1 (announced 09-28)** [F8]. From 0.6 on it requires Unity **6000.0** (package.json in [PV6]/[PV7]); 0.3 declared 2022.3.

### 2.1 Built-in command catalogue (0.7.0-exp.1, extracted from source)

There are **162 shipped `[CliCommand]`s**, plus 10 test-only commands under `Tests/`. `log_editor`, which the skill uses as an example, is test-only [PV7]. Grouped by tag:

- **Editor lifecycle:** `editor_play`, `editor_stop`, `editor_pause`, `editor_status` (status, compiling, domainReloadInProgress, playMode), `editor_focus`, `menu` (`path`, or list when omitted), `quit`\*.
- **Ticking:** **`set_autotick`** (`enable`=true, `interval_ms`=16, `persist`=true). It forces `EditorApplication.SignalTick` while the Editor is unfocused, and its state survives reloads through SessionState.
- **Console:** **`console`** (`tail`=100, `level`, `since` cursor, `since_session`). Entries carry seq, ts, level, logType, message, stackTrace and seeded, and the response adds `counts` and `groundTruth` {compilationFailed, compiling, console counts}. Also `console_status` and `clear_console`. `get_console_logs` was **removed in 0.7**. There is **no structured file/line/col** for compile errors.
- **Compile / build / tests:** `recompile` → `recompile_status` (idle, triggered, compiling, completed, up_to_date, plus compilationFailed); `build` (target, outputPath, profileName, options, scenes, `confirm`, `dry_run`) → `build_status` (a full BuildReport summary); `switch_build_target(_status)`, `list_build_targets`, `list_build_profiles`, `get/set_build_settings`; `list_tests`, `run_tests` (mode all/editor/playmode, filter, filter_type, include_explicit, `async_tests`, timeout=300) → `test_status`, `cancel_tests`.
- **Capture:** `screenshot` (view game/scene, output, w/h, returns a path); **`capture_game_view`** (width 1280, height 720, camera, `save_path`, `include_inline_image`, `max_resolution`, and `source` = `camera` | **`screen`**). `source=screen` includes **Screen Space Overlay UI but only in Play Mode**; `camera` misses overlay canvases. Also `capture_scene_view`, and `capture_editor_element` / `capture_runtime_element`\* (UI Toolkit selector → PNG, **Unity 6000.7+ only**). Output is **PNG only**; no JPEG parameter exists [PV7 capture.md].
- **Waiting:** **`wait_for`** takes a `condition` {member, target|findType, op equals/notEquals/greaterThan/lessThan/contains/changed, value}, plus timeout_s ≤600, poll_interval_ms ≥16, `on_met` {capture, pause} executed in the same frame, `return_history`, `tolerate_missing` and `async`. Companions are `wait_status` and `wait_cancel`. Async waits do **not** survive a domain reload [PV7 wait.md].
- **Scenes / GameObjects / components / prefabs / assets:** about 60 CRUD commands (create/open/save scene, hierarchy, find, transform, parent, active, tag, layer, add/remove component, get/set component properties, serialized fields, prefab create/variant/instantiate/apply/revert/unpack, asset create/import/move/copy/rename/delete/find, read/write text file, import settings, selection, **`search`** (a Unity Search query, limit ≤200), authoring root).
- **Settings:** get/set for audio, graphics, input, physics, player, quality, time, tags_layers, lighting, navmesh and runtime-pipeline settings. All setters are gated by `confirm`/`dry_run`.
- **Content:** materials/shaders (4), animation/animator (10), timeline (4).
- **Bakes:** lighting, navmesh (+surfaces) and occlusion, each with its own status, cancel and clear commands.
- **Packages:** `package_add/remove/resolve/list/search/status`.
- **Code execution:** `eval` / `eval_file` (Roslyn; default timeout 5 s, cap 24 h; work in both Editor and Player); **`run_script`** (compiles one `.cs` in memory and calls an entry point with no domain reload; supports ephemeral/hotpatch and dry_run); `batch` (≤200 ops, transactional with a single Undo, `dry_run`, job); `report_evals` (local eval-usage telemetry that suggests which typed commands are missing).
- **Code reload** (formerly "hot reload"): `reload_file`, `reload_file_editor_interpreter`, `reload_file_player_interpreter` (pushes IL to IL2CPP dev players), `codereload_status`\*, `cleanup_codereload`\*.
- **Observability:** **`get_performance_stats`**, which returns draw calls, batches, setpass, tris/verts, allocated/reserved/Mono memory and CPU/GPU/main-thread frame ms in a single snapshot. It has **no Profiler capture, markers or recording** [PV7 PerformanceCommands.cs]. Also `audit`/`audit_status` (Project Auditor → CSV; needs `com.unity.project-auditor-rules`).
- **Runtime (dev Player)\*:** `runtime_status`, `set_timescale`, `set_target_framerate`, `log`, and **`simulate_key`** (`key` as an Input System Key name, `action` down/up/press) plus **`simulate_pointer`** (x, y with a bottom-left origin, `action` move/down/up/click, `button`). Both inject through `InputSystem.QueueEvent` and therefore reach uGUI and UI Toolkit through the Input System UI module. The legacy Input Manager is **not supported** [PV7 RuntimeInputCommand.cs]. These two commands are **absent from every CHANGELOG entry**, so they shipped quietly.

\* = `RuntimeOnly`: served by the runtime server and hidden from the Editor listing. The runtime driver also starts **when entering Editor Play Mode** if `enableInBuilds` is on [PV7 runtime-setup.md]. So simulated input *may* work inside Editor Play Mode through `--runtime`. **UNVERIFIED — this is the #1 thing for P0-10 to test.**

What the catalogue lacks: UI element query or click-by-name (only raw screen coordinates), key hold/text entry/sequences, asserts or scenario runner, find-references/reverse index, scene diff, structured compile diagnostics, JPEG/crop capture, and profiler capture.

### 2.2 `[CliCommand]` / `[CliArg]` API

- Namespace `Unity.Pipeline.Commands`. The attribute lives in assembly `Unity.Pipeline` up to 0.7, and moved to **`Unity.Pipeline.Attributes` in 0.8.0**. That is a breaking change: an asmdef must add that reference, and without it you get CS0246 [F8][P08]. **This directly affects Nexus's `Editor/Pipeline/*.asmdef` and P0-06.**
- Signature: `[CliCommand(name, description, MainThreadRequired = true, RuntimeOnly = false, Tags = new[]{"a/b"})]` on a **static** method of any accessibility [PV7 CliCommandAttribute.cs].
- `[CliArg(name, description, Required, DefaultValue)]` can go on parameters, fields and properties, so a DTO can be annotated too. A parameter with no C# default counts as required. **Declaration order and required-ness are wire API**, because positional binding follows them. Enums are validated by name [PV7 creating-commands.md].
- **Naming:** the registry only rejects null, empty or whitespace names and duplicates, which throw. There is **no charset check, so dots are accepted by the server** [PV7 CommandRegistry.cs:120-126]. Built-ins all use `snake_case` and tags are lowercase `/`-paths. Whether the CLI parser and MCP tool naming handle `nexus.status` is **UNVERIFIED**. Note that many MCP clients restrict tool names to `[a-zA-Z0-9_-]`, so `nexus_status` is the safer choice.
- **Async:** a handler may return `Task` or `Task<T>`; the server awaits and unwraps it [PV7 BasePipelineServer.cs:2563]. Long work should use the job model (`"job":true`, `/api/job`, `PipelineCancellation`, `CliProgress.Report`) [PV7 0.5 changelog].
- **Dynamic registration:** from 0.7 there is `CommandRegistry.RegisterCommand/UnregisterCommand`, which supports instance delegates and is dropped on reload or Play Mode exit [PV7].
- **Mutation convention:** `confirm` + `dry_run` arguments, and `AuthoringUndoScope` for scene edits [PV7 safety-and-mutations.md].
- **Wire protocol:** `POST /api/exec` with `command` + `parameters`, or `commandLine`, or `argv`. Other endpoints are `/api/commands` (compact or full, tags, package), `/api/status` (with a `settling` flag), `/api/progress`, `/api/job`, and a modal-dialog endpoint. By default a response is a lean `{"success":true,"result":…}` [PV7 0.6 changelog].

### 2.3 Domain reload, security, runtime

- **Reload:** the token persists across reloads through SessionState (since 0.4). `recompile` cannot keep its request open across the reload. Async waits and dynamic registrations die on reload. `set_autotick` persists. During cold-start import the server answers **503** with `settling`. Main-thread commands are rejected while a modal dialog is open [PV7].
- **Execution model:** HTTP is concurrent but **command execution is strictly serialized** (0.5). Detached jobs are capped at 100 [PV7].
- **Auth and security:** a 256-bit bearer token (`evalToken`) sits in `Library/Pipeline/.unity-pipeline-port`, readable only by the owning user and re-advertised on every heartbeat. Comparison is constant-time, and any request with an `Origin` header is refused. Binding is **127.0.0.1 only** from 0.8 [F8] (0.7 source still used `http://+:port`). Editor ports sit in the 78xx range and runtime ports in 7900–7949. **There is no per-command permission policy**: the input commands' source carries "TODO(CAT-2509) safety policy not implemented" [PV7].
- **Runtime:** runs in development builds only; since 0.7, release builds need `ENABLE_RUNTIME_PIPELINE`. Configure it at Project Settings › Pipeline › Runtime. Code reload works on Mono and, through the interpreter, on IL2CPP [PV7][F6].
- **0.8 additions:** declaring commands no longer needs the full package, development players carry less of it, the IL interpreter covers more C#, `/api/commands` gains a tag summary, samples move to `com.unity.pipeline.samples`, and there are **six breaking changes** [F8].

## 3. Unity AI (Assistant / Gateway / old MCP)

- **In-Editor MCP server (inside `com.unity.ai.assistant`):** **deprecated**, and `unity mcp` replaces it. Third-party MCP packages are explicitly unaffected [MCPD]. Assistant 2.7.0 added connection caps, and reports mention a "Connected Clients limit 0" (https://discussions.unity.com/t/unity-ai-assistant-2-7-0-mcp-server-capacity-limit/1718606).
- **Unity AI** (Assistant agent, Generators, **AI Gateway**) remains. It opened as a beta for Unity 6 around 2026-05 (https://app.cinevva.com/news/2026-05-04-unity-ai-open-beta). The AI Gateway connects Claude, Codex or Gemini to the Editor using the user's own provider subscription.
- **Pricing (secondary sources, UNVERIFIED on unity.com):** Personal pays $10/mo for 1,000 credits; Pro includes 2,000 credits and 3 MCP/Gateway connections; Enterprise includes 3,000 credits and 5 connections (https://www.aicentralresources.com/tool/unity-ai-tools).
- **Package:** since 2.0.0-pre.1 (2026-03-03) it has absorbed ai.toolkit and ai.generators and made its MCP tools public. Docs exist up to `@2.20` (search index for docs.unity3d.com/Packages/com.unity.ai.assistant@2.20). The needle-mirror copy is stale (last update 1.0.0-pre.12, 2025).
- **For Nexus:** the paid, capped path is the Assistant; the free path is CLI + Pipeline. Nexus should build on the free path.

## 4. Official skills / agent plugin

The plugin [PL] currently ships **33 skills**, not 31. The standalone repo [SK] has **34**, adding `project-auditor-fixes` (v1.0.0, 2026-09-28). Install with `/plugin marketplace add Unity-Technologies/unity-agent-plugin` then `/plugin install unity@unity-agent-plugin`; Codex has an equivalent. The plugin is licensed under the Unity Companion License and requires Unity 6+.

| Skill | Covers |
|---|---|
| unity-cli | the entire CLI plus driving a live Editor, `[CliCommand]`, Safe Mode, MCP **(testing: `unity test`)** |
| new-unity-project | guided bootstrap, including a **visual baseline `unity command screenshot`** step |
| unity-package-management | UPM from outside the Editor |
| **project-auditor-fixes** | `audit` → CSV → fix loop **(QA/perf)**; its gotchas say to run `set_autotick` first |
| ui | routes to ui-uitk / ui-ugui / ui-imgui |
| ui-uitk / ui-ugui / ui-imgui | authoring UXML/USS, Canvas/RectTransform and IMGUI (**authoring only, no UI testing**) |
| migrate-birp-to-urp | migration workflow; `capturing-the-editor.md` does **camera renders via `eval`** and states "there is no capture command in the Pipeline catalog", which is outdated |
| urp-postprocessing / validate-urp-render-graph-renderer-feature / shader-graph-create-custom-node | rendering |
| optimize-web / optimize-audio / optimize-text-mesh-pro | **performance** (build size, audio memory/CPU, TMP) |
| physics-3d-collision | collision debugging, includes `CollisionDebugger.cs` |
| 2d-pixel-perfect, sprite-editor, sprite-segment-3x3grid, manage-sprite-atlas, tilemap-palette-create, tilemap-ruletile-createempty, tilemap-ruletile-createfromsegment | 2D |
| initialize-ai-navigation | NavMesh |
| audio-setup-mixers, localization, asset-transformer-toolkit, build-gtk, generate-editor-search-query | content and tools |
| implement-in-app-purchases, levelplay-unity-integration, build-live-game, setup-multiplayer-services, setup-vivox-voice-chat | monetization, live ops, multiplayer |

No skill covers play-testing, input simulation, UI QA or profiler capture. The package-embedded `unity-pipeline` skill (mirrored with `skill install --local`) is not public outside the package; its content is UNVERIFIED beyond what `project-auditor-fixes` quotes.

## 5. Adjacent Unity packages for automation/QA

- **Unity Test Framework** (core): driven by `unity test` and `run_tests` [SK][PV7].
- **UI Test Framework** `com.unity.ui.test-framework`: a core package fixed to the Editor version. It provides UI Toolkit panel simulators (context/popup menu simulators, world-space UI) for EditMode and PlayMode tests (https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.ui.test-framework.html; changelog @6.5). It is test-authoring only; nothing in the CLI exposes it.
- **Input System `InputTestFixture`:** device and event simulation inside NUnit tests (https://docs.unity3d.com/Packages/com.unity.inputsystem@1.20/manual/Testing.html). It covers the same mechanism `simulate_key` uses.
- **Automated QA** `com.unity.automated-testing` 0.8: development **on hold since 2021-12-06**, unsupported (https://docs.unity3d.com/Packages/com.unity.automated-testing@0.8/manual/index.html). Unity has no current record/replay product.
- **Project Auditor:** built into the Editor from 6.4, with rules in `com.unity.project-auditor-rules`, and wrapped by Pipeline `audit` (https://docs.unity3d.com/Manual/com.unity.project-auditor-rules.html) [PV7].
- **Profiler / ProfilerRecorder:** engine APIs only. Pipeline exposes a snapshot through `get_performance_stats` [PV7]; there is no capture, marker or worst-frame command.
- **Recorder** (`com.unity.recorder`): not integrated with the CLI or Pipeline (UNVERIFIED; nothing found).

## 6. Mapping: Nexus MASTER_PLAN_V2 §5.1 / §7 vs Unity

Legend. Status: **S** = shipped, **P** = partial, **N** = none. Recommendation: **USE** Unity as is; **WRAP** Unity (call its primitive and add value); **BUILD** (no Unity equivalent); **AVOID** (don't build it).

| Nexus item | Unity status (source) | Rec. |
|---|---|---|
| `nexus.status` / `wait_ready` | **P**: `editor_status`, `/api/status` settling, `recompile_status`+compilationFailed, `console_status` groundTruth [PV7]. No reload epochs | WRAP (add epochs) |
| `nexus.compile` | **P**: `recompile`/`recompile_status`; `console` gives messages but **no file/line/col**; CLI beta.11 adds a "quick compile check" [B11] | BUILD the diagnostics layer on top of `recompile` |
| `nexus.verify` | **N** as a composite. Primitives exist: `editor_play`, `wait_for` + `on_met.capture`, `batch`, `run_tests`, runtime `simulate_key/pointer` [PV7] | BUILD, but WRAP these primitives |
| `nexus.look` | **P**: `capture_game_view` (camera, or screen with Overlay in Play Mode only), `max_resolution`, path-only mode; PNG only; no crop or marks [PV7] | WRAP/BUILD (JPEG, crop, marks, Edit-Mode Overlay) |
| `nexus.context` | **N** | BUILD |
| `nexus.diagnose` | **P**: `console`, `console_status`, `audit` [PV7] | BUILD composite |
| `nexus.find_references` | **P**: `search` (Unity Search `ref:`-type queries, ≤200) [PV7]; no cached reverse index | BUILD (benchmark against `search` first) |
| `nexus.get` handles | **N** (Unity has save_path and path-only results) | BUILD (small) |
| qa profile: input | **P**: `simulate_key`/`simulate_pointer`, runtime-only, Input System only, press/click only [PV7] | WRAP for players; BUILD hold/text/legacy input/Editor Play Mode if P0-10 confirms the gap |
| qa: ui_query / ui_act | **N** (only `capture_*_element` 6000.7+) | BUILD |
| qa: step_frame | **P**: `editor_pause`, `set_timescale`\* | BUILD (small) |
| scene: scene_diff / snapshot | **N** (`get_scene_hierarchy` only) | BUILD |
| perf profile | **P**: `get_performance_stats` snapshot | BUILD later (ProfilerRecorder) |
| P0-01 main-thread wake-up | **S**: `set_autotick` [PV7] | USE in pipeline mode; BUILD only for legacy mode |
| P0-02 truthful readiness | **P**: settling/503, compilationFailed, groundTruth [PV7] | WRAP |
| P0-03 remove bridge sleeps | N/A (Nexus internal) | BUILD |
| P0-04 truthful status (isError) | Unity CLI envelope `success`/`errors[0].code` [SK] | BUILD (copy Unity's envelope) |
| P0-05 security fixes | Unity is a reference model: token, Origin refusal, loopback [PV7] | BUILD (match it) |
| P0-06 pin Pipeline range | **urgent**: 0.8 moved attributes to `Unity.Pipeline.Attributes` [F8][P08] | BUILD now |
| P0-07 MCP handshake | `unity mcp` implements `tools/list_changed` [SK] | BUILD |
| P0-08 benchmark v2 | CLI: warm `shell --protocol ndjson`, `--result-only`, `unity mcp` [SK] | BUILD (bench against these) |
| P0-09 adoption pilot | arm S1 = the plugin's 33 skills + CLI [PL] | BUILD |
| P0-10 ground truth | the list is now known (162 commands, 0.7) — see §2.1 | still RUN it on 0.8: test `simulate_*` in Editor Play Mode, `source=screen`, `wait_for` |
| P0-11 release | — | BUILD |
| S-01 skill pack | Unity ships `unity-cli` + `unity-pipeline` skills and `skill install` [SK] | BUILD, but position it next to Unity's skills and defer to them |
| S-02 generated `[CliCommand]` projection | **S** API; dynamic `RegisterCommand` in 0.7 [PV7] | WRAP (consider `RegisterCommand` instead of codegen) |
| S-03 bridge profiles/deferred loading | `unity command --tag/--query/--detail compact` [SK] | BUILD (mirror tags) |
| S-04 error envelope | Unity: `errorCode`, `argProblems`, `warnings` [PV7] | BUILD, aligned to Unity's shape |
| S-05 `nexus.compile` | see above | BUILD |
| S-06 diagnose + handles | see above | BUILD |
| S-07 look hygiene | Unity `max_resolution` + path-only [PV7] | WRAP |
| V-01 seams | — | BUILD |
| V-02 job store | **S**: `/api/job`, `--detach`, `unity job` [PV7][SK] (jobs don't survive reload) | USE Unity jobs in pipeline mode; BUILD reload-surviving jobs only if needed |
| V-03 keyboard `nexus.input` | **P**: `simulate_key` press/down/up, runtime-only | WRAP + BUILD (hold/text/legacy/Editor) |
| V-04 UI-aware targeting | **N** (coordinates only) | BUILD (key differentiator) |
| V-05 assertions | **P**: `wait_for` ops equals/greaterThan/contains/changed on members [PV7] | WRAP `wait_for`; BUILD log/UI/pixel asserts |
| V-06 verify composite | **N**; `batch` is Editor-mutation oriented | BUILD |
| V-07 pipeline adapter | **S** primitives (`--detach`, `unity job wait`) | WRAP |
| V-08 fast Enter Play Mode | Unity has `EnterPlayModeOptions`; nothing in CLI | BUILD (optional) |
| V-09 release | — | BUILD |
| C-01 `nexus.context` | **N** | BUILD |
| C-02 find_references | **P** via `search` | BUILD (only if it beats `search`) |
| C-03 scene_diff/snapshot | **N** | BUILD |
| C-04 detail/max_tokens | Unity: `--detail compact`, lean responses, `omitNulls` [PV7][SK] | BUILD (same vocabulary) |
| C-05 ui_check | **N** | BUILD (optional) |
| W-01 Windows validation | Unity supports Windows; Pipeline capture uses its own path | BUILD |
| W-02 Windows fixes | — | BUILD |
| W-03 capture fallback | Unity `source=screen` covers Overlay (Play Mode) + desktop fallback in `unity mcp` [PV7][SK] | WRAP: fall back to Unity `source=screen` rather than bespoke code |
| A-01 N/N-1 CI job | justified by 0.6→0.8 breaking changes every ~2 weeks | BUILD |
| A-02 deprecate duplicates | ~60 CRUD duplicates confirmed | AVOID extending; hide |
| R-02 plugin packaging | Unity's plugin uses the Claude/Codex marketplace format [PL] | BUILD, same format |
| backlog `nexus.perf` | **P** (snapshot only) | BUILD later |
| backlog `player_check` | runtime server + `--runtime` + `simulate_*` **S** | WRAP |
| backlog `nexus.mutate` | **S**: `batch` is transactional with Undo and dry_run [PV7] | **AVOID** (use `batch`) |
| (execute_code / own Roslyn) | **S**: `eval`, `eval_file`, `run_script` | **AVOID** |

## 7. Corrections to SHARED_CONTEXT / plan facts

1. **F4 is wrong as written.** Pipeline 0.7 does include keyboard and pointer simulation (`simulate_key`, `simulate_pointer`), but only on the runtime server and only through the Input System, with no text input or hold.
2. Overlay UI **is** capturable by Unity (`capture_game_view --source screen`, since 0.4), but only in Play Mode.
3. `get_console_logs` no longer exists (removed in 0.7); the command is now `console`.
4. The plugin has 33 skills and the skills repo has 34, not 31.
5. The Pipeline version is now 0.8.0-exp.1, and it contains a breaking attribute-assembly move.
6. `set_autotick` is exactly the fix that F8/P0-01 needs in pipeline mode.
