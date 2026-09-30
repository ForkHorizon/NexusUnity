# Agent D — Target Architecture, 12-Week Roadmap, Go-to-Market

Date: 2026-09-30 · Scope: `ForkHorizon/NexusUnity` @ 1.7.0-dev · Read-only review.
Angle: what a 2-person team that ships mostly through AI coding agents can actually build and sell in 12 weeks.

---

## 0. Position in one paragraph

Unity has made transport and atomic Editor primitives a commodity. `unity command` / `unity mcp` / `unity eval`, about 151 built-ins, `--detach` jobs and 31 official skills all ship free. Competing on "more tools" or "faster raw calls" is a losing race. Nexus already has what the others lack: a canonical command registry that projects to both HTTP and `[CliCommand]`, Capture V2 that includes Screen Space Overlay UI, input simulation, UI Toolkit automation, scene_delta/semantic_find/timeline, and real security depth. **Unity supplies the agent's hands. Nexus supplies its eyes and its judgment:** Nexus verifies that a change actually works (play → act → see → assert → report) and hands the agent the smallest correct context. I agree with the lead reviewer's direction, with two amendments:
(a) Phase 0 has to show with real task-success numbers that agents *call* Nexus when a skill tells them to. In 3 of 4 real sessions they never did. If they still don't, no architecture work matters.
(b) Nexus's main entry point is the **shell** (`unity command nexus.*`, plus a `nexus` shim when Pipeline is absent), not MCP. MCP becomes a thin compatibility projection.

---

## 1. Target architecture

### 1.1 Layers

```
┌──────────────────────────────────────────────────────────────────────┐
│ L4  Skills & distribution                                            │
│     skills/nexus-verify, nexus-context, nexus-unity (SKILL.md +      │
│     recipes); installers (reuse MCPCliInstaller.*); OpenUPM; plugins │
├──────────────────────────────────────────────────────────────────────┤
│ L3  Nexus Workflows (composite, long-running, job-based)             │
│     nexus.verify, nexus.prepare_context, nexus.analyze_logs,         │
│     nexus.after_edit (write→compile→group errors)                    │
│     depend on IUnityPrimitives (play/stop/tests/compile/refresh)     │
├──────────────────────────────────────────────────────────────────────┤
│ L2  Nexus Core (canonical commands + domain services)                │
│     Editor/Commands (INexusCommand, descriptor, registry)            │
│     Editor/Capture (ICaptureGateway, DriverOwnedReadback)            │
│     Input, UI automation, scene intelligence, readiness, logs        │
├──────────────────────────────────────────────────────────────────────┤
│ L1  Projections / transports (no business logic)                     │
│     Pipeline [CliCommand] (generated) │ Legacy HTTP JSON-RPC │ Bridge│
├──────────────────────────────────────────────────────────────────────┤
│ L0  Unity: Pipeline server, unity CLI/mcp/eval, jobs, built-ins      │
└──────────────────────────────────────────────────────────────────────┘
```

Most of this already exists, so the job is to reuse and finish it, not rewrite it:
- The L2 contract is `Editor/Commands/INexusCommand.cs` (`Task<JToken> ExecuteAsync(JToken, CancellationToken)`) plus `NexusCommandDescriptor.cs` (id, aliases, profiles, parameters, `DispatchAsync`) and `NexusCommandRegistry.cs`. Only 3 commands are registered today: project_map, group_compile_errors and capture_game_view.
- The L1 legacy projection is `NexusLegacyCommandProjection.cs`, and it is clean. The L1 Pipeline projection is `Editor/Pipeline/NexusPipelineCommands.cs` in an optional asmdef gated by `versionDefines com.unity.pipeline → NEXUS_HAS_PIPELINE`. That is the right isolation mechanism.
- Runtime selection lives in `Editor/Runtime/NexusRuntimeSelector.cs` and `NexusRuntimeCapabilities.cs`. The async probe, the PID-liveness check and the last-healthy seed across reloads all work.
- Capture lives in `Editor/Capture/*` and is transport-independent (`ICaptureGateway`).

### 1.2 Gaps in the "one definition" design (found in the code)

1. **The Pipeline wrappers are hand-written, so parameters are defined twice.** `NexusPipelineCommands.CaptureGameView` repeats `width/height/format/quality/max_dimension` and their defaults as `[CliArg]`, while the descriptor lists its own `Parameters`. Only the description strings are shared, as `const` values. At 3 commands this is fine. At 25 commands it will drift.
   **Fix:** use typed argument records plus a generator. Each command declares `sealed class XArgs { [NexusArg("width", "...")] public int Width = 0; ... }`. Then:
   - The descriptor derives `Parameters` from `XArgs` by reflection at registry build. This keeps runtime cost trivial and needs no Roslyn source generator. Source generators are fragile across Unity versions, and AI agents struggle to debug them.
   - A **checked-in, script-generated** `NexusPipelineCommands.g.cs` emits `[CliCommand]` methods from the same records. The script is `scripts/gen-pipeline-commands.py`, which reads a JSON dump produced by an EditMode test.
   - A **contract test** (extend `Tests/Editor/NexusCommandRegistryTests.cs`) fails if `g.cs` is stale, if a descriptor has no Pipeline projection, or if `[CliArg]` names and defaults ≠ the record.
   This keeps the Pipeline assembly free of logic, as `NexusPipelineCommands` remarks already require.
2. **Legacy sync projection blocks the thread.** `NexusLegacyCommandProjection` calls `.GetAwaiter().GetResult()` for sync commands. Rule: every new canonical command is `DispatchAsync = true`, or it is truly synchronous and returns `Task.FromResult`. Add an analyzer rule to `tools~/NexusQualityGate` that forbids `.GetResult()` in `Editor/Commands`.
3. **Core is still coupled to the legacy server.** `GroupCompileErrorsCommand` calls `MCPServer.GetLogs`, and `CaptureGateway` calls `MCPServer.Enqueue`/`MainThreadId`. Introduce two seams, `IMainThreadDispatcher` and `IConsoleLogSource`, with the legacy server as the default implementation. This must happen before any asmdef split.
4. **The profile catalog is a hard-coded list.** `NexusToolCatalog` uses hard-coded HashSets. Profiles should come from `descriptor.Profiles`. Keep the HashSets only for legacy raw methods that have no descriptor.

### 1.3 How one canonical command projects

| Projection | Mechanism | Name | Status |
|---|---|---|---|
| Pipeline `[CliCommand]` | generated `*.g.cs` in `UnityMCP.Editor.Pipeline` | `nexus.<verb>` (keep `nexus_<verb>` alias while the CLI requires `_`) | 3/3 exist, hand-written |
| `unity mcp` | free: Unity exposes every `[CliCommand]` | same | automatic |
| Legacy HTTP JSON-RPC | `NexusLegacyCommandProjection` | id + aliases | done |
| Python bridge (MCP stdio) | **dynamic**: the bridge calls `list_tools{profile}` and exposes canonical descriptors as tools; the 14 managers stay frozen | `nexus_<verb>` | to do |
| Skills | SKILL.md recipes that call the shell form | `unity command nexus.verify …` | to do |

### 1.4 Python bridge: keep it, freeze it, make it thin

`Editor/nexus_bridge/` is about 1.7k LOC with hand-written schemas for 14 managers. Recommendation:
- **Freeze the 14 managers.** They get no new actions and bug fixes only.
- **Add a dynamic canonical-command pass-through** so new Nexus commands reach MCP-only clients (Claude Desktop, Cursor, and so on) with zero Python edits.
- **Add deferred loading** via a `nexus_load_profile(profile)` meta-tool that emits `notifications/tools/list_changed`. The default visible set is at most 6 tools and about 1.5k tokens. This answers the LinkedIn token question directly.
- **Reuse the existing bridge CLI mode** (argparse, `key=value`, see CHANGELOG 1.4.0) as the `nexus` shell shim for users without Pipeline, so skills work in both modes.
- **Deprecation trigger:** mark the bridge "compat-only" once Legacy removal Gate 1+2 pass. Do not remove it before 2.0.

### 1.5 Legacy HTTP fallback policy

Keep the current semantics from `NexusRuntimeSelector.Effective`: explicit `pipeline` never silently falls back, `auto` uses Pipeline only when eligible, and `legacy` is always available. Add three rules:
1. **Legacy HTTP is the only path for the frozen raw surface (about 115 methods).** Pipeline mode does not re-project it.
2. **New features are Pipeline-first but must run in legacy mode too** through the same handler, because the Legacy projection is nearly free. Unity 6000.0 users without the Pipeline package must still get `nexus.verify` via the bridge or shim.
3. **No removal before the Gate 1–11 ledger** in `NexusLegacyDeprecation.cs` / ImageCapturePlan §10 passes. Earliest realistic removal is 2.0 in the second half of 2027.

### 1.6 Assembly layout (incremental, no big move)

| asmdef | Contents | When |
|---|---|---|
| `UnityMCP.Editor` (exists) | Legacy server, `MCPServerMethods.*` partials, windows, installers | stays; partial classes cannot span assemblies |
| `Nexus.Core.Editor` (new) | `Commands/`, `Capture/`, readiness, input, jobs, seams | week 8–9, after the seams exist; `UnityMCP.Editor` references it |
| `Nexus.Workflows.Editor` (new) | verify, context packs; references Core only | born separate in week 4 (it can reference `UnityMCP.Editor` first, then flip) |
| `UnityMCP.Editor.Pipeline` (exists) | generated `[CliCommand]` + `PipelineUnityPrimitives` adapter | extend |
| `UnityMCP.Editor.Pipeline.V0_8` etc. | only if a Pipeline API break forces it, selected by `versionDefines` ranges | on demand |

### 1.7 Composite workflows and Unity built-ins

Define `IUnityPrimitives` in Core: `EnterPlayAsync`, `ExitPlayAsync`, `RecompileAsync`, `RunTestsAsync`, `RefreshAsync`, `ReadConsoleAsync`, `EvalAsync?`.
- **`LegacyUnityPrimitives`** (Core) wraps existing Nexus code: `toggle_play_mode` logic from `MCPServerMethods.Editor.cs`, `run_tests`/`TestResults.cs`, and `TriggerSafeAssetRefresh`.
- **`PipelineUnityPrimitives`** (Pipeline asmdef) calls Unity's **in-process** command implementations or public Editor APIs directly. It does not make HTTP calls to itself: in-process is faster and has no auth or port issues. Only call the Pipeline command registry where it adds semantics that Nexus lacks, such as build status and the test runner job.
- Selection: `NexusRuntimeHost.EffectiveMode`. Workflows never see transports. This follows ImageCapturePlan §4: "adapters are outside domain logic".
- **Rule:** a workflow may only call primitives and Core services, never `MCPServerMethods.*` JObject handlers. This prevents the "workflow calls stringly-typed legacy RPC" trap.

### 1.8 Error model

Today there are three shapes: JSON-RPC `-32602`/`-32000` + stackTrace (`MCPServerMethods.cs` ~L230), `{success:false,message}` bodies, and `NexusCaptureResult.Success`. Canonical commands standardize on one shape:

```json
{ "ok": false,
  "error": { "code": "EDITOR_BUSY|NOT_FOUND|INVALID_ARG|DOMAIN_RELOAD_INTERRUPTED|TIMEOUT|UNSUPPORTED_IN_MODE|CONFIRM_REQUIRED|INTERNAL",
             "message": "...", "retryable": true, "hint": "call nexus.wait_ready then retry" },
  "data": {...partial...}, "meta": { "ms": 12, "mode": "pipeline", "gen": 42 } }
```

- Stable string codes form a public contract and are documented in API_REFERENCE.
- `retryable` and `hint` are written for the agent, not for humans.
- Stack traces only appear when `NEXUS_DEBUG` is set, which saves tokens.
- The legacy raw surface keeps its current shapes, because they are already public.
- `DomainReloadInterrupted` already exists in Capture. Promote it to a code.

### 1.9 Job model for long operations

Do not invent a parallel job system in Pipeline mode. Unity CLI already ships `--detach` → job id and `unity job wait|status|cancel`.
- Core provides `NexusJobStore`: `SessionState` + a `Library/Nexus/jobs/<id>.json` artifact directory. It survives domain reload because each state transition is written to disk. Jobs hold state (queued/running/succeeded/failed/interrupted), step log, artifact paths and domain generation.
- **Pipeline mode:** `nexus.verify` is a normal async `[CliCommand]`. Users run it with `--detach` and poll it with `unity job`. The command writes its report to `Library/Nexus/jobs/<id>/` either way, so a reload that kills the task still leaves an inspectable partial report marked `interrupted`.
- **Legacy mode:** `nexus.job_status` / `nexus.job_wait` / `nexus.job_cancel`, following the same pattern as `run_tests`→`run_tests_wait`.

### 1.10 Versioning and compatibility with experimental Pipeline

- Keep `unity: 6000.0` as the floor.
- The Pipeline asmdef **must set version ranges**, for example `"[0.7.0-exp,0.9.0)"`. Today it is the open-ended `0.0.0-exp`, so an API-breaking 0.9 would break user compiles. That is the single biggest compat risk in the current code.
- Out-of-range versions → the assembly is excluded → `NexusCommandsRegistered=false` → `auto` falls back to legacy. This fallback already exists.
- **Contract tests in CI:** a matrix job that installs pinned Pipeline versions (N and N-1) into the harness project and runs `unity command` listing plus 3 smoke commands. On the first Pipeline pre-release each month, a routine opens an issue if the tests fail.
- Descriptors get a `SinceVersion` and an optional `RequiresPipeline >= x` capability.
- **Surface versioning:** `list_tools` reports `schema_version`. Canonical commands follow SemVer: adding a param is minor, and removing or renaming one needs an alias for 2 minors.

---

## 2. Migration plan for the ~121 raw methods

These are grouped by the `MCPServerMethods.*.cs` domain files. Categories:
- **C:** becomes a canonical command, available in Pipeline, MCP and skills.
- **F:** frozen legacy-only, dispatchable but hidden from default profiles.
- **D:** deprecated.

| Domain (file) | C (canonical, in priority order) | F (frozen, Unity has an equivalent) | D |
|---|---|---|---|
| HighValue / Capture | `capture_game_view` ✔; `capture_editor_window` (merges `capture_inspector_screenshot` + `ui_capture_window_snapshot`); `semantic_find`; `generate_mermaid_diagram` → folded into `prepare_context` | legacy `capture_game_view_screenshot` (alias) | — |
| Delta / Snapshot / Timeline | `scene_delta`, `scene_summary` (= `compact_scene_snapshot`, bounded, #0016), `editor_timeline` | `dump_scene_graph` (unbounded) | — |
| Context / References / Reflection | `prepare_context` (new; composes project_map + symbol_index + selection context + missing refs); `find_references`; `missing_references` | `invoke_method` (confirm-gated; Pipeline users should use `unity eval`) | `symbol_index` as a standalone tool (folded into prepare_context) |
| Logs / Core | `group_compile_errors` ✔; `analyze_logs` (dedupe/group + cursor, built on `read_logs_since_cursor`) | `read_logs`, `clear_logs`, `batch_execute`, `initialize`, `list_tools`, `get_server_status` | `ping_main_thread`, `attach_existing_session` (after bridge rework), `reset_tool_usage_stats` (hide) |
| Sync / readiness | `wait_ready` (fixed #0013, generation-aware) | `is_editor_idle`, `is_asset_import_idle` | `wait_for_asset_import_idle` (merge into wait_ready) |
| Input / UI Toolkit | `input` (mouse/touch/**keyboard**, one command, action list), `ui_query`, `ui_act` (click/input_text) | `ui_get_hierarchy`, `ui_list_windows`, window-rect methods | `click_object_in_game` (superseded by `input` target resolution) |
| Editor / Play / Tests | `verify` (new flagship) | `toggle_play_mode`, `pause_play_mode`, `step_frame`, `run_tests`, `get_test_results`, `execute_menu_item`, undo/redo, `set_selection`, `list_scenes`, `get_project_info`, `get_tags_and_layers`, `lint_project` | `focus_scene_view` |
| Scene / Hierarchy / Component / Search | none (Unity owns CRUD) | all ~35 CRUD methods (`create_game_object`, `set_transform`, `add_component`, `find_objects`, ...) | duplicate `instantiate_prefab` registration (Component + Scene) |
| Asset / Prefab / SO / PlayerPrefs | `diff_scriptable_objects` (C, later: unique) | the rest | — |
| File I/O | — | `read_file`, `write_file`, `write_files_batch` (confirm-gated) | hide from default profiles: agents have a shell |

The result is about **15 canonical commands** by week 12, against ~121 raw methods today. The default model-visible surface is **core = 5**: `wait_ready`, `prepare_context`, `group_compile_errors`, `verify`, and `load_profile`.

**Deprecation policy:**
1. Hide the method from default profiles immediately. This is not a break.
2. Mark it `deprecated:true` plus `replacement` in `list_tools`, and add `meta.deprecation` to responses.
3. Keep it dispatchable for **at least 2 minor releases and at least 90 days**, with a CHANGELOG "Deprecated" section and DOCUMENTATION.MD update in the same PR.
4. Remove it only in a major release.
5. Nothing security-gated (confirm, allowlist) is relaxed during migration.

---

## 3. 12-week roadmap (weeks 1–12 from 2026-10-05)

Rules follow MASTER_PLAN v1 GLOBAL RULES 1–12: one task means one agent and one PR from `development`, TDD, honest evidence, and the security invariants are untouchable. Each task is at most 3 days. **E:** marks acceptance evidence that must be attached to the PR.

### Phase 0 — Honest measurement + cheap fixes (weeks 1–2)

**P0-01 · Benchmark v2 with task-success oracles** (3d)
- Deps: none. Files: `NexusFeature~/bench/*`, new `bench/oracles.py`, `BENCHMARK_PROTOCOL.md`.
- Requirements:
  - Separate transport-ok from task-success. Each scenario gets an oracle: C8 checks image decode plus overlay marker pixels; C10 needs a fixture project with 5 tests and asserts 5 results; C12 needs an EventSystem fixture and asserts a UI state change.
  - Record focused vs. unfocused Editor.
  - Add a token column: tokens of tools/list plus tokens of the response.
- E: raw JSON; re-run of Nexus plus one competitor; invalid legacy numbers marked void in BENCH_RESULTS.
- Out of scope: new competitor runs beyond one.

**P0-02 · Main-thread wake-up fix** (2–3d)
- Deps: P0-01 harness. Files: `MCPServer.cs` (~L145), `MCPServer.Logs.cs` `Enqueue`, `MCPSettings.cs`.
- Requirements:
  - After `Enqueue`, wake the Editor loop: post to the captured `UnitySynchronizationContext`, and/or call `EditorApplication.QueuePlayerLoopUpdate` from main-thread hooks.
  - Add an opt-in "agent session: no throttling" setting that temporarily sets the Editor interaction mode / idle time while requests are active, and restores it when they stop.
  - In Pipeline mode, document and use `set_autotick`.
- E: unfocused p50 for object read below 20 ms (today 100–300 ms), shown as a histogram before and after on macOS; no CPU regression while idle (Activity Monitor numbers).
- Out of scope: broker process.

**P0-03 · False-ready fix #0013 → generation-aware readiness** (2–3d)
- Files: `MCPServerMethods.Sync.cs` (today `is_idle = !isCompiling && !isUpdating`), `MCPServerMethods.Hierarchy.cs` pending refresh.
- Requirements:
  - A domain `generation` counter driven by `CompilationPipeline.compilationStarted/Finished` and `AssemblyReloadEvents`.
  - A "refresh pending" and "script write pending" latch, plus a settle window of N ticks.
  - `wait_ready` returns `{ready, gen, reason}`.
- E: the C7 write→compile run reports ready only after reload, 20/20 runs; the reported compile time matches Editor.log (about 7.5–8.5 s, not 99 ms).

**P0-04 · Adoption experiment ("will agents call us?")** (3d) [DECISION input]
- Replay 4 tasks from `AI Chat reports Before all changes/` with Claude Code and Codex in 3 arms: no Nexus, Nexus MCP only, and Nexus skill + shell.
- Measure: number of Nexus calls, task success, total tokens, wall time.
- E: a table in `NexusFeature~/results/ADOPTION_V1.md`.
- This is the kill/continue input for the flagship (Gate G4).

**P0-05 · Pin Pipeline asmdef version range + compat contract test** (1d)
- Files: `Editor/Pipeline/UnityMCP.Editor.Pipeline.asmdef`, `Tests/Editor/NexusRuntimeSelectorTests.cs`.
- E: the harness compiles with Pipeline absent, with 0.7.x, and with a faked out-of-range version.

**P0-06 · Release 1.7.0** (1d)
- Deps: P0-02, P0-03, P0-05.
- Ships the Unreleased CHANGELOG plus the fixes; no new features.
- E: tag, GitHub release, and a clean-install smoke on macOS.

### Phase 1 — Discoverability: skills first (weeks 2–4)

**S-01 · Nexus skill pack v1** (3d)
- Files: new `skills~/nexus-unity/SKILL.md`, `nexus-verify/`, `nexus-context/`, installed via `MCPCliInstaller.*`.
- Requirements:
  - Trigger-oriented descriptions such as "after editing C# in a Unity project…" and "before claiming a UI change works…".
  - Commands are shown in both shell forms (`unity command nexus.*` and `nexus …`), plus a token-budget note.
- E: the P0-04 arm-3 re-run shows ≥2× Nexus calls versus MCP-only.

**S-02 · Typed args + generated Pipeline projection + drift test** (3d)
- Files: `Editor/Commands/*`, `Editor/Pipeline/NexusPipelineCommands.g.cs`, `scripts/gen-pipeline-commands.py`, `NexusCommandRegistryTests.cs`.
- E: deleting a param from a record fails the test; the 3 existing commands are regenerated with identical `unity command` help output.

**S-03 · Bridge: dynamic canonical tools + `nexus_load_profile` deferred loading** (3d)
- Files: `nexus_bridge/routing.py`, `routes_base.py`, `schemas.py`, `NexusToolCatalog.cs` (derive profiles from descriptors).
- E: the default tools/list is at most 1.5k tokens (measured); a new C# command appears in MCP with no Python change.

**S-04 · `nexus` shell shim from existing bridge CLI mode** (2d)
- Files: `nexus_unity_bridge.py`, installers.
- E: `nexus project_map` works with Pipeline absent; the skills run unchanged in both modes.

**S-05 · Unified error envelope for canonical commands** (2d)
- Files: `Commands/NexusCommandResults.cs`, projections, API_REFERENCE.MD.
- E: contract tests for 6 codes; legacy raw shapes unchanged, shown by `OpenSourceApiContractTests`.

### Phase 2 — Flagship: `nexus.verify` (weeks 4–8)

**V-01 · Seams: `IMainThreadDispatcher`, `IConsoleLogSource`, `IUnityPrimitives` (legacy impl)** (3d)
- Files: `Capture/CaptureGateway.cs`, `Commands/GroupCompileErrorsCommand.cs`, new `Core/`.
- E: Core files have no `MCPServer.` references (enforced by a grep test).

**V-02 · Job store + legacy job commands** (3d). See §1.9.
- E: a job survives a forced `RequestScriptReload` and reports `interrupted` plus partial artifacts, 20/20 runs (reuse the harness pattern from `NexusCaptureReloadProbe`).

**V-03 · Keyboard input + unified `nexus.input`** (3d)
- Files: `MCPServerMethods.Input.cs`, using the existing `com.unity.inputsystem` dependency.
- Requirements: key down/up/press/text and hold duration; action lists.
- E: PlayMode fixture: WASD moves a player; a text field receives a string.

**V-04 · UI-aware target resolution** (3d)
- Adds EventSystem raycast, UGUI and UI Toolkit runtime panels, and a clear `NO_EVENTSYSTEM` error. This fixes the C12 "Main Camera" misclick.
- E: the C12 oracle passes 10/10 on the fixture scene.

**V-05 · Assertion library** (3d)
- Asserts: `log_absent/present(pattern)`, `object_state(path, prop, op, value)`, `ui_element(query, visible/text)`, `pixel_region(rect, color±tol)`, `no_new_errors`.
- E: unit tests; each assert returns a structured fail reason.

**V-06 · `nexus.verify` composite** (3d)
- Deps: V-02..V-05.
- Input: steps (play/act/wait/capture/assert). Output: pass/fail, per-step timings, JPEG thumbnails (with a `max_long_edge` default), a markdown report in the job dir, and a token-bounded summary under 400 tokens.
- E: 3 recipe scenarios pass 10/10; report samples committed under `docs/`.

**V-07 · Pipeline primitives adapter** (3d)
- File: `Editor/Pipeline/PipelineUnityPrimitives.cs`. Verify runs in `pipeline` mode with `--detach`.
- E: the same 3 scenarios pass through `unity command --detach` + `unity job wait`; parity table vs. legacy.

**V-08 · Release 1.8.0-preview "verify"** (1d). Includes docs, CHANGELOG, a demo GIF, and README comparison section v1.

### Phase 3 — Token-lean context tools (weeks 7–10)

**C-01 · `nexus.prepare_context`** (3d)
- Input: a task string and a `max_tokens` budget.
- Output: project_map, relevant scripts via `symbol_index` + `semantic_find`, a bounded scene summary, selection context, and missing references.
- E: on the P0-04 tasks, agent tokens-to-first-correct-edit drop ≥30% vs. shell-only, or the task is killed.

**C-02 · `nexus.analyze_logs`** (2d). Dedupe, group by signature, cursor-based "since last call", compile vs. runtime split.

**C-03 · Canonicalize `scene_delta` + `scene_summary`** (bounded, #0016) (3d).

**C-04 · Canonicalize `find_references` + `missing_references` + `capture_editor_window`** (3d).

**C-05 · `max_tokens`/`detail` convention across canonical commands** (2d)
- E: each command's response token count is measured in the bench token column.

### Phase 4 — Hardening and platform (weeks 9–11)

**W-01 · Windows validation pass** (3d)
- Scope: install, token file, ports, capture (D3D11/12), input, installers, bridge on Windows Python.
- E: a Windows run of the P0-01 suite; issues filed.

**W-02 · Windows fixes from W-01** (≤3d, may split).

**A-01 · Extract `Nexus.Core.Editor` asmdef** (3d)
- Deps: V-01.
- E: a clean clone compiles with Pipeline absent and present; 152+ tests green.

**A-02 · Pipeline N/N-1 CI contract job + monthly routine** (2d).

**A-03 · Deprecation sweep 1** (2d). Hide and mark the D items from §2 in docs, CHANGELOG and list_tools.

### Phase 5 — Release and distribution (weeks 10–12)

**R-01 · OpenUPM listing + release automation** (2d). Covers `RELEASE.md` and the tag→OpenUPM flow.

**R-02 · Claude Code plugin / Codex marketplace packaging** (2d). The skills plus a `.mcp.json`-optional plugin manifest; submit to skills.sh and marketplaces.

**R-03 · Honest comparison page + benchmark v2 publication** (2d). Include the cases where Nexus loses.

**R-04 · Demo: "Agent says it's fixed. Nexus proves it."** (2d). A 90 s scripted video plus a GIF.

**R-05 · Release 1.9.0** (1d). Verify becomes GA, context tools ship, Windows is supported.

**Capacity check:** about 32 tasks × 2.5 d ≈ 80 agent-days. With 2 humans reviewing and 3–4 agents running in parallel, that fits 12 weeks. **Human review is the bottleneck, not agents.** Budget 1 review day per 4 PRs, and cap work in progress at 4 PRs.

---

## 4. Go-to-market

**Positioning statement.** For Unity developers who let AI agents edit their projects, Nexus is the verification and context layer that turns "I think it works" into evidence. It plays the game, drives input, sees the real Game View including Overlay UI, asserts, and reports, while spending as few tokens as possible. Unity CLI gives agents hands. Nexus gives them eyes. It is built on top of Unity's official CLI, not against it.

**Naming.** Keep the package id. Brand as **"Nexus"** with the descriptor "agent QA & context for Unity". Avoid "Nexus for Unity CLI" as the name, for two reasons: it ties the brand to a beta product, and "Unity" in product names has trademark risk. Use "Works with Unity CLI" as a badge line instead. The flagship verb goes in every headline: **`nexus verify`**.

**Channels, in priority order:**
1. **GitHub.** README rewritten around verify, a 30-second quickstart, and the honest comparison.
2. **OpenUPM.** This is the standard path for UPM packages.
3. **Skills distribution.** Claude Code plugin marketplace, Codex, and skills.sh, where Unity's own `unity-cli` skill is listed. Skills are the adoption lever (P0-04).
4. **Unity Discussions.** Post in the Unity CLI / AI threads, framed as complementary.
5. **LinkedIn/Telegram/r/Unity3D.** Short videos.
6. **Unity Asset Store: skip for now.** It is slow to review, poorly suited to Python-bridge tooling, and brings low-intent traffic. Revisit only with a paid tier.

**Demo strategy.** One repeatable before/after. The agent "fixes" a UI bug and claims success. `nexus verify` plays the game, clicks, and captures a frame showing the overlay still broken. The agent fixes it again, and verify passes with a report. Then show the token counter.
- Publish the demo script as a bench scenario so the demo can be reproduced.
- Monthly "verified by Nexus" clips on community sample projects.

**Community answers.** People asked about tokens and comparisons. Answer with a pinned **TOKENS.md**: tools/list tokens per profile, per-command response sizes, and deferred-loading design. Publish **COMPARISON.md** with scenario oracles, including where Unity CLI alone is enough ("use `unity command` for CRUD; add Nexus when you need proof"). Honesty is the differentiator against inflated claims elsewhere.

**Monetization (realistic).**
- Keep the core MIT. Expect no meaningful revenue in 12 weeks.
- Low-cost options: GitHub Sponsors, and paid setup/consulting for studios. This path is already credible because of the security depth.
- A later, plausible **"Nexus CI" (studio tier)**: headless `nexus verify` in CI on development Player builds, with report history, flaky-step detection and an artifact dashboard. It is closed-source or hosted, starts at 3+ months, and comes only after verify has users.
- Do not paywall any Editor feature. That kills adoption against free Unity CLI and MIT competitors.

---

## 5. Decision gates for the owners

| # | Decision | When | Recommendation |
|---|---|---|---|
| G1 | Pipeline-first vs. permanent dual transport | now | **Pipeline-first for new features, Legacy frozen as compat** under the existing Gate 1–11 ledger. No new raw methods. |
| G2 | Python bridge fate | week 2 | **Freeze the managers, add a dynamic canonical pass-through and deferred loading, reuse its CLI as the `nexus` shim.** No rewrite into another language (D1 in MASTER_PLAN is moot). |
| G3 | Own Roslyn `execute_code` (MASTER_PLAN T21) | now | **No.** Point skills at `unity eval` and keep `invoke_method` confirm-gated. Unity maintains Roslyn now, and owning it carries security liability. |
| G4 | Flagship = `nexus verify`? | end of week 2 (P0-04 data) | **Yes, with kill criteria.** If skill-armed agents still call Nexus less than 2× per task, pivot effort to context tools plus Unity skill contributions. |
| G5 | Tool surface size (MASTER_PLAN target 32–40) | now | **Reverse it.** Core profile of 5 tools, about 15 canonical commands in total, and everything else behind `load_profile`. Tokens are a selling point. |
| G6 | Unity floor | now | **Stay at 6000.0.** Pipeline features are capability-gated, not version-gated. |
| G7 | Player-build (runtime) verify within 12 weeks | week 8 | **Spike only** (1 task). Editor verify first; runtime is the Nexus-CI tier's foundation. |
| G8 | Brand name | week 10 | **"Nexus": agent QA & context for Unity**, with a "works with Unity CLI" badge. |
| G9 | Monetization | week 12 | **None yet.** Revisit with usage data; Nexus CI is the candidate. |
| G10 | Legacy HTTP removal | 2027 | **Not before 2.0 and all gates**, with one stable Pipeline-primary release cycle first (Gate 11). |
| G11 | Upstream vs. differentiate | ongoing | Contribute generic fixes (such as tick throttling findings) to Unity forums. Keep Capture V2, input, verify and context as Nexus-owned. Re-check each Pipeline release in case Unity ships input sim; if it does, **wrap it, don't fight it**, the same as with atomics. |

**Main risks:**
1. Unity ships input and UI automation in the Pipeline. Mitigation: verify's value is the assert/report/orchestration layer, and primitives are swappable through `IUnityPrimitives`.
2. Pipeline API breaks. Mitigation: pinned version ranges plus the N/N-1 contract job.
3. Agents ignore Nexus anyway. Mitigation: the P0-04 gate, measured before building.
4. Review bandwidth for 4 parallel agents. Mitigation: the WIP cap and tasks of at most 3 days.

Sources: [Unity Pipeline & CLI walkthrough](https://unity.com/resources/unity-pipeline-cli-technical-walkthrough), [Meet the Unity CLI](https://unity.com/blog/meet-the-unity-cli), [Unity CLI 1.0.0-beta.4 notes (`--detach`, `unity job`)](https://discussions.unity.com/t/unity-cli-1-0-0-beta-4-is-rolling-out/1733720), [unity-cli skill on skills.sh](https://www.skills.sh/unity-technologies/skills/unity-cli).
