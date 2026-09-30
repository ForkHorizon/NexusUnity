# NEXUS UNITY — MASTER PLAN v2

Version 2.0 · 2026-09-30 · Owners: Kiryl / Daliys (ForkHorizon)
Horizon: 12 weeks (target start 2026-10-05) with two hard decision gates (week 4, week 7).
**Status: ACTIVE. Supersedes `MASTER_PLAN.md` (v1, 2026-09-01) and the forward-looking parts of
`ImageCapturePlan.md` (M0–M4 are done; M5 is replaced by §9 here).** Only one plan is active at a time.

Inputs: review of all `NexusFeature~` material, the repo at `ef13f8e`, web research on Unity CLI /
Pipeline (Sep 2026), and four independent analyses kept in `NexusFeature~/v2_analysis/`:
- `A_build_on_unity.md` — what to build on top of Unity CLI (composites, speed, tokens)
- `B_skeptic.md` — what not to do, risks, kill list, process guardrails
- `C_agent_efficiency.md` — adoption, token economics, benchmark v2 spec
- `D_architecture_roadmap.md` — target architecture, method migration, roadmap, go-to-market

---------------------------------------------------------------------
## 0. КРАТКО ДЛЯ ВЛАДЕЛЬЦЕВ (RU)
---------------------------------------------------------------------

**Суть.** Unity бесплатно отдала «руки» агента: транспорт, подключение клиентов, базовые команды,
`eval`, тесты, билды, 31 официальный skill. Соревноваться там бессмысленно. Nexus становится
**слоем «глаз и головы» поверх Unity CLI**: доказать, что изменение работает (`nexus.verify`),
дать агенту минимальный правильный контекст (`nexus.context`, `nexus.compile`, `nexus.diagnose`),
и делать это быстрее и дешевле по токенам, чем сырые команды.

**Главная проблема — не скорость и не функции, а то, что агенты не вызывают Nexus** (в 3 из 4
реальных сессий — ни одного вызова). Поэтому первые 4 недели — честные замеры, дешёвые фиксы
и доступность (skills, `unity command nexus.*`, инструкции в MCP), а не новые фичи.

**Что делаем:** фиксы Phase 0 (≈2 недели) → skills + малый набор составных команд → флагман
`nexus.verify` → контекст-инструменты → Windows → релиз и дистрибуция.

**Чего НЕ делаем:** гонку за количеством тулов (14→40), свой `execute_code`, свой транспорт,
брокер, установщики под 10 клиентов, миллисекундные сравнения как главный аргумент.

**Два решения-«ворот» с правом остановиться:**
- **Неделя 4 (Gate A):** если с установленным skill агенты всё равно почти не вызывают Nexus —
  прекращаем фичи, разбираемся с доступностью или сужаемся.
- **Неделя 7 (Gate B):** если прототип `nexus.verify` не лучше, чем «Unity CLI + официальные skills»
  (и конкурент uLoopMCP) по успешности задач или токенам — уходим в запасной план (Capture V2
  как отдельная библиотека + skills-пакет) или в режим поддержки.

**Решения, которые нужны от вас сейчас** — см. §10 (G1–G8). Рекомендации даны.

---------------------------------------------------------------------
## 1. FACTS THIS PLAN RELIES ON (each with evidence and expiry)
---------------------------------------------------------------------

Rule (new): a "fact" needs an evidence link, a date and an expiry. Anything else is a hypothesis.
No "DO NOT REOPEN" sections. Facts expire on a Unity / Pipeline version bump or after 30 days.

| # | Fact | Evidence | Expires |
|---|---|---|---|
| F1 | Unity CLI 1.0.0-beta.11 (2026-09-22) + `com.unity.pipeline` (experimental, 0.x-exp), free, Unity 6.0+ | docs.unity.com/en-us/unity-cli/release-notes (via search) | next CLI release |
| F2 | Unity deprecated its in-Editor AI Assistant MCP server; `unity mcp` replaces it; Unity recommends `unity command`/`unity eval` over MCP for shell-capable agents | docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli | next CLI release |
| F3 | Pipeline built-ins include status, console, hierarchy, component get/set, play/stop, screenshot/capture_game_view, recompile, build, tests, packages, bakes, eval, menu, `set_autotick`; `[CliCommand]` auto-discovery; `unity shell --protocol ndjson` warm process; `--detach` + `unity job` | Unity-Technologies/skills `unity-cli` SKILL.md + references; community command list | next Pipeline release |
| F4 | No keyboard/mouse Play Mode input or UI automation found in official Unity CLI material | A §5 (official skill + references fetched) | **must be re-checked in P0-10 against live `unity command` listing** |
| F5 | Third party `hatayama/unity-cli-loop` (uLoopMCP) already ships Play Mode input record/replay skills | skills.sh/hatayama/unity-cli-loop | P0-10 |
| F6 | In 3 of 4 real AI sessions Nexus was never called; in the 4th, 2 calls. Agents used shell, asked the user for screenshots, or drove the GUI with computer-use | `AI Chat reports Before all changes/1-4.md` | re-measured in P0-09 |
| F7 | v1 benchmark "ok%" = no JSON-RPC error; C10 invalid for all tools (fixture has 0 tests; bridge result was `Timeout`); C12 invalid (no EventSystem; Funplay click hit nothing; Coplay screenshot `success:false`); C8 bridge used OS screencapture; C7 raw 99 ms = false-ready | raw JSON in `NexusFeature~/results/` | permanent (v1 numbers are void) |
| F8 | Main-thread latency steps (~100 ms) = Editor update throttling in Edit Mode while unfocused. Bridge "fast" run was in Play Mode (`isPlaying:true`), raw runs in Edit Mode. Nexus queue drains only on `EditorApplication.update`, nothing wakes it | `bench_nexus_bridge_fast.json`; `MCPServer.cs:145`, `MCPServerMethods.cs:215`; `STABILIZATION_ACCEPTANCE_REPORT.md` §E | until P0-01 re-measures |
| F9 | Hybrid M1–M4 is implemented but only 3 commands reach Pipeline (`project_map`, `group_compile_errors`, `capture_game_view`) | `Editor/Pipeline/NexusPipelineCommands.cs` | — |
| F10 | Capture V2 includes Screen Space Overlay UI; orientation proven on Metal only; depends on private `GameView.m_RenderTexture` | Acceptance report §F; `Editor/Capture/GameViewCaptureSource.cs:14-16,52-55` | Windows run (W-01) |

Verified code defects used below (file:line checked on `ef13f8e`):
- D-1 `shutdown_server` needs no token and requests without `Origin` are accepted → any local process can stop the server (`MCPServer.Http.cs:23,132`).
- D-2 A main-thread call that times out after 60 s still runs later → a client retry can apply a write twice (`MCPServerMethods.cs:215-227`).
- D-3 `wait_for_editor_idle` checks only `isCompiling/isUpdating`, ignores pending refresh / play transition (#0013) (`MCPServerMethods.Sync.cs`).
- D-4 Bridge compile wait uses fixed sleeps (0.5 s / 2.0 s / 1.0 s) and can wait ~20 s for a reload that never comes (`nexus_bridge/routes_editor.py:73-97`).
- D-5 Pipeline asmdef `versionDefines` is open-ended `0.0.0-exp` → a breaking Pipeline release breaks user compiles (`Editor/Pipeline/UnityMCP.Editor.Pipeline.asmdef:22`).
- D-6 Bridge `initialize` sends protocol `2024-11-05`, no `instructions`, declares `prompts` (`nexus_unity_bridge.py:165-166`); results are text-only (images arrive as base64 text).
- D-7 Bridge never sets MCP `isError`; `Timeout`/`PartialSuccess` look like successful calls to agents.
- D-8 In `auto` mode the legacy HTTP port may stay unbound while the Python bridge is HTTP-only → ~117 methods unreachable for that project (`NexusRuntimeSelector.cs:64-67`, `_transport.py:13`).
- D-9 Full stack traces returned in JSON-RPC error `data` (token waste, info leak) (`MCPServerMethods.cs` error helpers).
- D-10 `group_compile_errors` scrapes console text; loses line/column and misses errors outside the log window.

---------------------------------------------------------------------
## 2. POSITIONING
---------------------------------------------------------------------

> **Nexus — agent QA & context for Unity.** Unity CLI gives agents hands; Nexus gives them eyes
> and judgment. Nexus proves a change works (play → act → see → assert → report, including
> Overlay UI) and hands the agent the smallest correct context — in fewer calls and fewer tokens.
> Works with Unity CLI; also runs standalone.

- Brand "Nexus" + badge line "Works with Unity CLI". Do not name the product after Unity CLI (beta dependency, trademark risk).
- Headline demo: **"The agent says it's fixed. Nexus proves it."**
- Security is repositioned honestly: *Nexus's own commands are sandboxed and gated; Unity `eval` is outside Nexus's control.* It is no longer a headline differentiator.

---------------------------------------------------------------------
## 3. PRINCIPLES (apply to every task)
---------------------------------------------------------------------

1. **Don't duplicate Unity.** If Pipeline has an atomic command, Nexus calls it (pipeline mode) or keeps its frozen legacy equivalent (legacy mode). No new atomic CRUD.
2. **Every new capability must either replace ≥3 agent calls, or do something Unity cannot.** Otherwise it is not built.
3. **One call, Editor-side.** Composite workflows run inside the Editor with event-driven waits (compile/reload/play-mode/test callbacks), never agent-driven polling or fixed sleeps.
4. **Small answers by default.** Verdict + counts + top-k + handles; `detail: summary|normal|full`; deltas via generation/cursor; images only when useful, cropped and downscaled.
5. **Truthful status.** Never report success for submitted/partial/timeout. Ready only after a new compile/reload epoch.
6. **Shell-first distribution.** Primary entry is `unity command nexus.*` (and a `nexus` shim without Pipeline) + skills. MCP is a thin projection.
7. **Measure what users feel.** Task success, false-success rate, tokens per task, agent chose-tool rate. Milliseconds only with tick state controlled.
8. **Freeze what we don't differentiate on.** Legacy HTTP, 14 bridge managers, 10-client installers, runtime selector: security and crash fixes only.

---------------------------------------------------------------------
## 4. TARGET ARCHITECTURE (reuse what exists — no rewrite)
---------------------------------------------------------------------

```
L4 Skills & distribution   skills~/nexus-unity, nexus-verify, nexus-context (+ AGENTS.md snippet)
L3 Nexus Workflows         nexus.verify · nexus.compile · nexus.context · nexus.diagnose · ...
                           depend only on IUnityPrimitives + Core services
L2 Nexus Core              Editor/Commands (registry, descriptors) · Editor/Capture (Capture V2)
                           input · UI automation · readiness/epochs · job store · scene intelligence
L1 Projections             Pipeline [CliCommand] (generated) · Legacy HTTP JSON-RPC · MCP bridge (thin)
L0 Unity                   Pipeline server · unity command / shell / mcp / eval · jobs · built-ins
```

Key rules (details in `v2_analysis/D_architecture_roadmap.md` §1):
- **One canonical definition per command**: typed argument record → descriptor parameters by reflection → checked-in generated `NexusPipelineCommands.g.cs` → contract test fails on drift. Legacy projection already exists (`NexusLegacyCommandProjection`).
- **`IUnityPrimitives`** (play/stop/recompile/tests/refresh/console): `LegacyUnityPrimitives` wraps existing Nexus code; `PipelineUnityPrimitives` calls Unity in-process (no HTTP to itself). Workflows never call `MCPServerMethods.*` JObject handlers.
- **Seams before any assembly split**: `IMainThreadDispatcher`, `IConsoleLogSource` (Capture and GroupCompileErrors currently call `MCPServer.*` directly).
- **Error envelope for canonical commands**: `{ok, data, error:{code, message, retryable, hint}, meta:{ms, mode, gen}}`; stable string codes; stack traces only with `NEXUS_DEBUG`. Legacy raw shapes unchanged.
- **Job model**: Pipeline mode uses Unity `--detach` + `unity job`; Nexus `NexusJobStore` (SessionState + `Library/Nexus/jobs/<id>/`) survives domain reload and leaves an `interrupted` partial report. Legacy mode gets `nexus.job_status/wait/cancel`.
- **Pipeline compatibility**: pin `versionDefines` to a tested range; out-of-range → assembly excluded → `auto` falls back to legacy; CI contract job for Pipeline N and N-1.
- **Python bridge**: freeze the 14 managers; add dynamic pass-through of canonical commands (new C# command appears in MCP with zero Python edits); deferred loading via profiles; reuse its CLI mode as the `nexus` shim.

---------------------------------------------------------------------
## 5. THE PRODUCT SURFACE
---------------------------------------------------------------------

### 5.1 Default profile `core` (≤8 commands, schema budget ≤2.5k tokens)
| Command | What it does | Replaces |
|---|---|---|
| `nexus.status` / `nexus.wait_ready` | Truthful state + compile/reload epochs; wait returns the epoch it observed | status polls, false-ready |
| `nexus.compile` | (optional writes) → refresh → compile → grouped diagnostics with file/line/col + "did you mean" hints; `no_change` in <50 ms | 6–15 calls + log dumps |
| `nexus.verify` | Declarative scenario executed Editor-side: play → input → frame waits → asserts → capture-on-fail → report; scenarios saved as files = regression tests | 10–20 calls + 1–3 images |
| `nexus.look` | Capture V2: JPEG, default max 1024 px, crop to object/UI element, optional numbered UI marks | full-frame PNG / human screenshots |
| `nexus.context` | Task-scoped context pack with token budget: which scripts are attached where, serialized refs, build scenes, missing refs | 10–30 rg/sed calls at session start |
| `nexus.diagnose` | "Why is it broken now": state, compile, grouped console errors, missing refs, last test failures | 5–7 calls |
| `nexus.find_references` | Project-wide cached reverse index (scenes, prefabs, variants, SOs, Addressables) | guid grep that misses semantics |
| `nexus.get` | Handle resolver (`cap://`, `log://`, `snap://`) with range/crop | large default payloads |

Other profiles, loaded on demand: `qa` (input, ui_query/ui_act, step_frame, ui_check), `scene` (scene_diff, snapshot), `perf` (later), `full` (legacy managers + raw escape hatch `nexus.call` / `nexus.list`).

### 5.2 Method migration (from ~121 raw methods)
Summary (details: A §1, D §2):
- **~64 duplicate Pipeline built-ins** (scene/GameObject/component/asset CRUD, play/undo, prefs, menu): **frozen, legacy-only**, hidden from default profiles, still dispatchable.
- **~40 unique** (Capture V2, input, UI Toolkit automation, semantic_find, scene_delta, symbol_index, missing refs, diffs, timeline, lint, step_frame): become or feed canonical commands.
- **~10 rebuilt as composites** (waits, write+compile, tests-wait, batch).
- **7 transport methods**: Pipeline owns them in pipeline mode; legacy keeps them.
- Deprecation policy: hide first (not a break) → `deprecated:true` + `replacement` in `list_tools` → dispatchable for ≥2 minors and ≥90 days → remove only in a major. No security gate is relaxed during migration.

---------------------------------------------------------------------
## 6. GLOBAL RULES v2 (paste to every agent, together with the task)
---------------------------------------------------------------------

1. Repo `ForkHorizon/NexusUnity` (MIT). Branch `rework2/<task-id>` from latest `development`. One task = one agent = one PR. Never merge your own PR. Do not push to `development`/`main`.
2. **Scope cap:** ≤15 files and ≤800 changed lines per PR unless the task says otherwise. If you would exceed it, STOP and report.
3. **Stop rule:** if effort passes 2× the estimate, or you are blocked >2 h on one approach, stop and write what you learned. Never silently widen scope.
4. **Definition of done** = user-visible behavior + raw evidence artifact committed under `Research~/evidence/<task-id>/` (JSON/log/PNG) + exact reproduction command + environment (OS, GPU API, Unity version, Pipeline version, Editor focus state).
5. **You don't accept your own work.** Never write "ACCEPTED"/"IMPLEMENTED/ACCEPTED". A different agent or a human verifies from the artifact on a clean checkout.
6. **Honest results:** failures are recorded, never smoothed. Unverifiable items are marked UNVERIFIED in the PR.
7. **TDD** for behavior changes (EditMode tests in `Tests/Editor`, Python tests next to the bridge). `PYTHONDONTWRITEBYTECODE=1 scripts/prepush-validate.sh --static-only` must pass.
8. **Security invariants** (loopback, token, path sandbox + symlink resolution, confirm gates for `.cs` writes / delete_asset / PlayerPrefs all, type allowlists, batch cap 50, ProjectSettings/Packages write-protected) are untouchable. If the task conflicts, STOP.
9. **Frozen areas** (legacy HTTP methods, 14 bridge managers, installers, runtime selector): only security/crash fixes unless the task explicitly names them.
10. **Public API**: raw JSON-RPC methods keep their shapes. New canonical commands follow SemVer (add param = minor; remove/rename = alias for 2 minors). Keep `CHANGELOG.md`, `README.md`, `DOCUMENTATION.MD`, `API_REFERENCE.MD` aligned.
11. Unity floor 6000.0. Newer APIs behind `#if UNITY_X_OR_NEWER`. Pipeline code only in the Pipeline asmdef.
12. Benchmarks: success = semantic check (task oracle), never "no JSON-RPC error". Record Editor focus/tick state and `isPlaying` per sample.
13. No public performance claims without a benchmark v2 artifact for that platform.
14. Human review is the bottleneck: at most 4 open PRs at once.

---------------------------------------------------------------------
## 7. ROADMAP (weeks from 2026-10-05; each task ≤3 days)
---------------------------------------------------------------------

Legend: **E:** = evidence required in the PR.

### PHASE 0 — Honest measurement + cheap fixes (weeks 1–2)

**P0-01 · Main-thread wake-up** (2–3d) · Files: `MCPServer.cs`, `MCPServer.Logs.cs` (`Enqueue`), settings
- Wake the Editor loop when a request is enqueued (e.g. post to captured `UnitySynchronizationContext`; main-thread-safe `QueuePlayerLoopUpdate`); opt-in "agent session: no throttling" that restores the Editor interaction mode after idle; document `set_autotick` for pipeline mode. Do not call main-thread-only APIs from the listener thread.
- E: histogram before/after, Edit Mode, Editor unfocused, macOS: object read p50 <20 ms (from 100–300 ms); idle CPU unchanged.

**P0-02 · Truthful readiness (#0013) + epochs** (2–3d) · Files: `MCPServerMethods.Sync.cs`, `.Status.cs`, `.Hierarchy.cs`
- `compile_epoch`/`reload_epoch` from `CompilationPipeline` + `AssemblyReloadEvents`; pending-refresh / script-write / play-transition latches; one readiness predicate shared by status and waits; wait returns `{ready, epoch, reason}`; waits move off 100 ms sleeps.
- E: write→compile 20/20 runs report ready only after reload; time matches Editor.log (≈7.5–8.5 s on the harness), not 99 ms.

**P0-03 · Remove fixed sleeps from the bridge** (1–2d) · Files: `nexus_bridge/routes_editor.py`
- Replace 0.5/2.0/1.0 s sleeps and the 20 s "wait for reload" with epoch-based waiting (P0-02). `no_change` returns immediately.
- E: "refresh + wait compilation" with nothing to compile returns in <1 s (report 2 took 19 s).

**P0-04 · Truthful status to agents** (2d) · Files: bridge result wrapping, `MCPServerMethods.cs` error helpers
- Map any non-ok status (`Timeout`, `PartialSuccess`, `Error`, `Submitted`) to MCP `isError:true` with `code/hint/retryable`; stack traces only with `NEXUS_DEBUG`.
- E: contract tests for each status; v1 C8/C10 responses reproduced and now flagged as errors.

**P0-05 · Security quick fixes** (1d) · Files: `MCPServer.Http.cs`, `MCPServerMethods.cs`
- `shutdown_server` requires the token (zombie cleanup reads the token file); cancel flag so a timed-out main-thread call never executes later.
- E: tests: unauthenticated shutdown rejected; timed-out mutation not applied.

**P0-06 · Pin Pipeline version range** (1d) · Files: `Editor/Pipeline/UnityMCP.Editor.Pipeline.asmdef`, tests
- Replace `0.0.0-exp` with the tested range; out-of-range → legacy fallback.
- E: compiles with Pipeline absent, in range, and faked out-of-range.

**P0-07 · MCP handshake that sells Nexus** (1–2d) · Files: `nexus_unity_bridge.py`, bridge schemas
- `initialize.instructions` (≈150 words: when to use Nexus vs shell); current MCP protocol version; image content blocks instead of base64 text; implement `prompts/list` with 3 prompts or stop declaring `prompts`; tool descriptions start with "Use when …".
- E: `initialize` transcript; screenshot arrives as an image block; token count of the default tools/list.

**P0-08 · Benchmark v2: fixture + protocol harness** (3d) · Files: `NexusFeature~/bench/`, new fixture project (separate repo or `Research~/bench-fixture`)
- Fixture and scenarios P1–P11 per `v2_analysis/C_agent_efficiency.md` §3 (Canvas+EventSystem+toggle button + planted-bug variant; 12 EditMode tests with 1 known failure; 3-error compile case incl. asmdef; prefab reference graph with decoy; 1,500-object scene).
- Three scores per sample: transport_ok / tool_status_ok / **semantic_ok (headline)**; focus/tick condition and `isPlaying` recorded; assert-mode dry run before timing; histogram with 100 ms clustering flag; N=30 warm reads / 10 for long ops; interleaved rounds.
- Competitors: Unity CLI (persistent `unity mcp` / `unity shell`, and cold `unity command`), Nexus 1.6 vs 1.7. Others optional in this task.
- E: raw JSON + `BENCH_RESULTS_V2.md`; `BENCH_RESULTS.md` v1 marked void where invalid.

**P0-09 · Adoption baseline (agent-level pilot)** (3d) [DECISION input]
- 4–5 tasks modelled on the real reports + the fixture (fix-and-prove UI toggle, why doesn't it compile, who references Enemy.prefab, run packer menu item and confirm, source-only control). Arms: S1 Unity CLI + official skills; S3 Nexus MCP today. Agents: Claude Code + Codex, default settings (deferred tools on). Neutral prompts. N≥3 per cell for the pilot.
- Metrics: semantic success, **false-success rate**, tokens, wall time, calls by category, **chose-Nexus**.
- E: `NexusFeature~/results/ADOPTION_V1.md` with transcripts.

**P0-10 · Ground truth on Unity & competitors** (1d) [spike, doc only]
- On the harness: dump live `unity command` list for the installed Pipeline; test: Play Mode input? Overlay UI in `capture_game_view`? profiler depth? player-build command registration? Install uLoopMCP in a scratch copy and try its input record/replay on the fixture.
- E: `Research~/evidence/P0-10/unity_capabilities.md` with command list and screenshots. Updates F4/F5.

**P0-11 · Release 1.7.0** (1d) · Deps: P0-01..P0-07
- Ships Unreleased CHANGELOG + fixes; no new features. E: tag, release notes, clean-install smoke on macOS.

### PHASE 1 — Discoverability + small composite surface (weeks 2–4)

**S-01 · Skill pack v1 + AGENTS.md snippet** (3d) · Files: new `skills~/nexus-unity/`, installers (`MCPCliInstaller.*`)
- Trigger-oriented descriptions ("after editing C# in a Unity project…", "before claiming a UI change works…"); decision table (shell vs `unity command` vs `nexus.*`); 5 recipes; budget and state rules; error table; "when NOT to use Nexus". Both invocation forms shown. Opt-in AGENTS.md/CLAUDE.md block (marker-delimited, byte-preserving).
- E: installer tests (idempotent, markers preserved); P0-09 re-run for arm "Nexus + skill".

**S-02 · Typed args + generated `[CliCommand]` projection + drift test** (3d) · Files: `Editor/Commands/*`, `Editor/Pipeline/NexusPipelineCommands.g.cs`, `scripts/gen-pipeline-commands.py`, `Tests/Editor/NexusCommandRegistryTests.cs`
- E: removing a param fails the test; the 3 existing commands regenerate with identical `unity command` help.

**S-03 · Bridge: dynamic canonical pass-through + profiles + deferred loading** (3d) · Files: `nexus_bridge/routing.py`, `routes_base.py`, `schemas.py`, `NexusToolCatalog.cs`
- Canonical commands exposed from `list_tools{profile}`; `nexus.load_profile` emits `tools/list_changed`; default `core` ≤8 tools and ≤2.5k tokens; 14 managers only in `full`. Fix D-8: bridge must not silently lose the Editor in `auto` mode (bind legacy HTTP when the bridge is configured, or route through Pipeline).
- E: measured tools/list tokens per profile; new C# command appears in MCP with no Python change.

**S-04 · Unified error envelope for canonical commands** (2d) · Files: `Commands/NexusCommandResults.cs`, projections, `API_REFERENCE.MD`
- E: contract tests for ≥6 codes; legacy raw shapes unchanged (`OpenSourceApiContractTests`).

**S-05 · `nexus.compile`** (3d) · Deps: P0-02 · Files: new command, replaces scraping in `GroupCompileErrorsCommand`
- `CompilationPipeline.assemblyCompilationFinished` messages (file/line/col/id), persisted in SessionState across reload; optional sandboxed writes (confirm gate kept); "did you mean" via `symbol_index` for CS0246/CS1061; ±3 lines of source context in `normal` detail.
- E: fixture 3-error case returns exactly 3 errors with positions; `no_change` <50 ms; token size of summary response.

**S-06 · `nexus.diagnose` + `nexus.get` handles** (3d)
- E: fixture broken state → ≤15-line prioritized report; handle fetch with range/crop.

**S-07 · `nexus.look` image hygiene** (2d) · Files: Capture command/projections
- JPEG default, max 1024 px, crop to object/UI element; file-path mode for CLI; never both PNG and JPEG.
- E: token cost per capture measured (target ≈0.4–0.8k); Overlay marker test still passes.

**▶ GATE A (end of week 4):** re-run P0-09 with S4 = Nexus (instructions + skill + core profile + CLI). Continue feature work only if agents choose Nexus for Unity-state actions in a clear majority of relevant runs (target: ≥2× the S3 baseline) and no increase in false-success. Otherwise: stop Phase 2, spend 2 weeks on discoverability, re-run; if still failing → fallback (§9).

### PHASE 2 — Flagship `nexus.verify` (weeks 4–8)

**V-01 · Seams** (3d): `IMainThreadDispatcher`, `IConsoleLogSource`, `IUnityPrimitives` + `LegacyUnityPrimitives`. E: grep test — Core files contain no `MCPServer.` references.
**V-02 · Job store + legacy job commands** (3d). E: job survives forced `RequestScriptReload`, reports `interrupted` + partial artifacts, 20/20.
**V-03 · Keyboard + unified `nexus.input`** (3d): key down/up/press/text/hold, action lists; Input System + legacy Input Manager fallback. E: PlayMode fixture — WASD moves player, text field receives string.
**V-04 · UI-aware targeting** (3d): EventSystem raycast, UGUI + UI Toolkit runtime panels, `NO_EVENTSYSTEM` error, `input_path: eventsystem|invoke` reported. E: fixture P9 passes 10/10.
**V-05 · Assertions** (3d): log present/absent, object property op value, UI element visible/text, pixel region color±tol, no new errors; structured fail reasons. E: unit tests per assert.
**V-06 · `nexus.verify` composite** (3d) · Deps V-02..V-05: step DSL (A §2.2), per-frame predicates, capture-on-fail, report in job dir, summary ≤400 tokens, scenarios saved under `Assets/NexusScenarios/`. E: 3 recipe scenarios 10/10; planted bug is caught.
**V-07 · Pipeline primitives adapter** (3d): verify runs in pipeline mode via `unity command --detach` + `unity job wait`. E: parity table legacy vs pipeline.
**V-08 · Enter Play Mode fast path** (2d, optional): use `EnterPlayModeOptions` (no domain reload) only when a static-state lint passes. E: play entry time before/after on fixture; lint catches a planted static.
**V-09 · Release 1.8.0-preview "verify"** (1d): docs, CHANGELOG, GIF.

**▶ GATE B (end of week 7):** agent-level run on the verify tasks: Nexus v2 + Unity CLI vs Unity CLI + official skills alone vs uLoopMCP (if P0-10 shows it covers the case). Continue if Nexus wins on semantic success or tokens-per-success without higher false-success. Otherwise → fallback (§9).

### PHASE 3 — Token-lean context (weeks 7–10)

**C-01 · `nexus.context`** (3d): task string + `max_tokens`; cached index in `Library/Nexus/` invalidated by postprocessor + compile events; `fingerprint` in responses. E: tokens-to-first-correct-edit ≥30% lower than shell-only on the benchmark tasks, or the task is dropped.
**C-02 · `nexus.find_references`** (3d): project-wide cached reverse index. E: fixture P10 returns exactly the 6 referrers, not the decoy.
**C-03 · `nexus.scene_diff` + bounded `scene_snapshot`** (3d): `ObjectChangeEvents` + YAML→hierarchy path resolution; closes #0016.
**C-04 · `detail` / `max_tokens` convention** across canonical commands (2d). E: bench token column per command.
**C-05 · `nexus.ui_check`** (3d, optional): multi-resolution offscreen / overflow / unclickable checks, captures only on issues.

### PHASE 4 — Hardening & platform (weeks 9–11)

**W-01 · Windows validation** (3d): install, token, ports, Capture V2 on D3D11/D3D12/Vulkan (orientation via `SystemInfo.graphicsUVStartsAtTop`), input, bridge, skills. E: benchmark v2 protocol run on Windows.
**W-02 · Windows fixes** (≤3d each).
**W-03 · Capture fallback + self-test** (2d): startup check that `GameView.m_RenderTexture` still resolves; fallback to Unity capture with an "Overlay may be missing" warning.
**A-01 · Pipeline N/N-1 CI contract job** (2d) + monthly check on Pipeline pre-releases.
**A-02 · Hide/deprecate sweep 1** (2d): per §5.2 policy; docs + CHANGELOG + `list_tools` flags.
**A-03 · Extract `Nexus.Core.Editor` asmdef** (3d, optional; only after V-01).

### PHASE 5 — Release & distribution (weeks 10–12)

**R-01 · OpenUPM + release automation** (2d).
**R-02 · Plugin packaging** (2d): Claude Code plugin / Codex / skills.sh listing next to Unity's `unity-cli` skill.
**R-03 · Public TOKENS.md + COMPARISON.md** (2d): generated from benchmark v2 JSON only; include where Unity CLI alone is enough.
**R-04 · Demo "Agent says it's fixed. Nexus proves it."** (2d): recorded with `nexus.verify` itself; demo script is a bench scenario.
**R-05 · Release 1.9.0** (1d): verify GA, context tools, Windows supported.

**Later / backlog (not scheduled):** `nexus.perf` (ProfilerRecorder + worst-frame markers; only if P0-10 shows Unity's profiler command is shallow) · `nexus.player_check` (same scenario against a development build; spike first) · `nexus.mutate` transactional batch with undo group + rollback · Nexus CI tier (headless verify on player builds, report history).

Capacity: ~40 tasks × ~2.5 d ≈ 100 agent-days; with ≤4 PRs in flight and 2 reviewers this fills 12 weeks only if the gates pass. Optional tasks are the buffer.

---------------------------------------------------------------------
## 8. v1 → v2 MAPPING (what happened to every v1 task)
---------------------------------------------------------------------

| v1 | v2 verdict | Where it went / why |
|---|---|---|
| T01 screenshot | DONE (acceptance numbers missing) | Numbers come from P0-08; Windows in W-01 |
| T02 reload survival | SHRINK | P0-02 epochs + V-02 job store; Pipeline owns lifecycle |
| T03 run_tests | SHRINK | Failure summarizer inside `nexus.diagnose`/verify; Unity has `run_tests` |
| T04 QA cycle C12 | KEEP → V-03..V-06 | Needs real fixture (P0-08) |
| T05 perf CI gate | DROP | Tick noise; replaced by semantic + agent-level benchmarks |
| T06 401 matrix | SHRINK | Only if it reappears; P0-05 covers auth hole |
| T07 dispatch tax | SHRINK → P0-01 | It is a wake-up problem |
| T08 payload diet | KEEP → C-04, S-07 | Tokens are a core axis |
| T09 bridge efficiency | DROP | Bridge is frozen/thin |
| T10 14→32–40 tools | **REVERSED** | ≤8 default + profiles + skills (§5.1) |
| T11 response contract | SHRINK → S-04 | New canonical commands only |
| T12 zero-config wizard | DROP | `unity mcp configure` / skills; keep existing installer frozen |
| T13 readiness enum | KEEP → P0-02 | |
| D1 bridge language | DROP | Transport is Unity's |
| D2 / T21 execute_code | DROP | Use `unity eval` |
| D3 broker | DROP | |
| D4 Unity floor | DECIDED: 6000.0 | |
| D5 layout | DONE | |
| T22 Pipeline adapter | DONE | Extend only via S-02 / V-07 |
| T23 keyboard + raycast | KEEP → V-03, V-04 | After P0-10 check |
| T24 scene/inspector capture | SHRINK | `nexus.look` crop; editor-window capture kept frozen |
| T25 QA tools | KEEP → V-05, V-06 | Flagship |
| T26 profiler | BACKLOG | Only if Unity's is shallow |
| T27 skills | **TOP PRIORITY → S-01** | |
| T28 audit backlog | SHRINK | Only issues new commands depend on (#0013 in P0-02, #0016 in C-03) |
| T31 docs overhaul | SHRINK | Docs for the new small surface |
| T32 OpenUPM | KEEP → R-01 | |
| T33 comparison tables | SHRINK → R-03 | Task-success data only |
| T34 onboarding smoke | DROP | |
| T35 multi-editor | DONE | |
| T36 plugin API | DROP | `[CliCommand]` is the plugin API |
| T41–T47, T50 | DROP | Except T43 semantic_find (feeds C-01), T44 scene_delta (C-03), T45 step_frame (qa profile) |
| T48 demo | KEEP → R-04 | |
| T49 SECURITY.md | KEEP (in P0-05 PR) | Must state `unity eval` is outside Nexus gates |
| ImageCapturePlan M5 + 11 gates | REPLACED | §9 rule |

---------------------------------------------------------------------
## 9. LEGACY POLICY AND FALLBACK PLAN
---------------------------------------------------------------------

**Legacy HTTP + Python bridge:** frozen (security/crash fixes only), kept as the path for users without Pipeline. Removal only in a major release, after Pipeline leaves experimental and one stable Nexus release has shipped with Pipeline-first `auto`. Everything new is written once in Core and reaches legacy through the existing projection for free.

**Fallback if Gate A or Gate B fails:**
1. Publish Capture V2 as a small standalone package (`[CliCommand]` + library) that other tools can depend on.
2. Publish the Nexus skill pack as verification recipes on top of `unity eval` + the few Nexus commands.
3. Put the rest in maintenance mode with a clear README note. This is a legitimate outcome, not a failure.

---------------------------------------------------------------------
## 10. DECISIONS FOR THE OWNERS (recommendation in bold)
---------------------------------------------------------------------

| # | Decision | When | Recommendation |
|---|---|---|---|
| G1 | Pipeline-first for new features; legacy frozen | now | **Yes** |
| G2 | Python bridge: freeze + thin pass-through, no rewrite | now | **Yes** |
| G3 | Own `execute_code` | now | **No — use `unity eval`** |
| G4 | Flagship = `nexus.verify` with Gate B kill criterion | now | **Yes** |
| G5 | Tool surface: ≤8 default, profiles, skills | now | **Yes (reverses v1 T10)** |
| G6 | Timebox: B's 6-week hard bet vs D's 12 weeks | now | **12-week plan with Gate A (wk 4) and Gate B (wk 7) as hard stops — combines both** |
| G7 | Player-build verify in these 12 weeks | week 8 | **Spike only** |
| G8 | Monetization | week 12 | **None yet; keep core MIT; revisit "Nexus CI" tier with usage data** |

---------------------------------------------------------------------
## 11. SUCCESS METRICS (what "we're winning" means now)
---------------------------------------------------------------------

| Metric | Baseline (v1 evidence) | Target by week 12 |
|---|---|---|
| Agent chooses Nexus for Unity-state actions (neutral prompts) | ~0 of 4 sessions | majority of relevant runs |
| Semantic success on "fix and prove UI bug" | not measurable (no fixture) | > Unity CLI + official skills alone |
| False-success rate (agent says done, check fails) | unmeasured | lowest among measured setups |
| Tokens per successful task (T1 fix-and-prove) | est. 20–30k (Nexus today) | ≤7k (estimate to be replaced by P0-09) |
| Main-thread call p50, Edit Mode, unfocused | 100–300 ms | <20 ms |
| Default tools/list tokens | ~4.9k (14 managers) | ≤2.5k |
| Screenshot cost to the model | base64 text (unusable) | ≈0.4–0.8k tokens, real image |
| Platforms with a recorded benchmark run | macOS only | macOS + Windows |

---------------------------------------------------------------------
## 12. RISK REGISTER
---------------------------------------------------------------------

| # | Risk | Mitigation | Early-warning trigger |
|---|---|---|---|
| R1 | Agents still don't choose Nexus | Skills, instructions, CLI exposure; Gate A | Gate A numbers |
| R2 | Unity ships input/UI automation/Overlay capture | Keep value in scenario + assertions + report; primitives swappable via `IUnityPrimitives` — wrap, don't fight | Pipeline changelog mentions input/simulate/overlay |
| R3 | uLoopMCP or others own the verify niche | P0-10 head-to-head; specialise (Overlay capture, semantic asserts, reports) or integrate | Their skill tops skills.sh / forum mentions |
| R4 | Pipeline API churn | Pinned range (P0-06), N/N-1 CI (A-01), thin generated projection | Compile break on Pipeline bump |
| R5 | `unity eval` bypasses Nexus gates | Honest SECURITY.md; stop selling security as headline | Any doc claiming full protection in pipeline mode |
| R6 | 3 transports, 2 people | Freeze list; new code only in Core | >20% of monthly commits touch frozen areas |
| R7 | Windows breaks capture/input | W-01 before any public claim | No Windows run before 1.9 |
| R8 | Private `m_RenderTexture` disappears | W-03 self-test + fallback | Field resolves null on a new 6000.x |
| R9 | AI agents widen scope / self-accept | Rules 2–5 in §6 | PR over cap; "ACCEPTED" written by implementer |
| R10 | Energy / focus | Gates give visible checkpoints every 3–4 weeks | Two weeks without user-visible change |

---------------------------------------------------------------------
## 13. WHERE THE FOUR ANALYSES DISAGREED (and how v2 resolves it)
---------------------------------------------------------------------

- **Timebox.** B: hard 6-week bet with exit. D: 12 weeks. → 12 weeks with hard Gate A (wk 4) and Gate B (wk 7).
- **Is input a Unity "hand"?** Lead reviewer assumed possibly; A found nothing official. → Treat input as Nexus-owned (V-03/V-04) but verify first (P0-10).
- **Verify shape.** A: declarative scenario run Editor-side in one call. D: composite command with steps. → Same thing; v2 adopts A's DSL executed Editor-side, with D's job model.
- **Profiler.** B: drop (Unity has it). A: marker-level triage could differentiate. → Backlog, decided by P0-10.
- **Multi-editor / runtime selector.** B: freeze and consider dropping `auto` default. D: keep semantics. → Keep, but fix D-8 in S-03 so `auto` never silently disconnects bridge users.
- **Default surface size.** C: ≤8; D: 5; A: ~12 + escape hatch. → ≤8 in `core`, the rest via profiles + `nexus.call/list`.

---------------------------------------------------------------------
## 14. SOURCES
---------------------------------------------------------------------

- Unity CLI release notes — https://docs.unity.com/en-us/unity-cli/release-notes
- Unity CLI as the replacement for the in-Editor MCP server — https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli
- Unity-Technologies/skills, `unity-cli` skill — https://github.com/Unity-Technologies/skills/blob/main/skills/unity-cli/SKILL.md
- Pipeline command list (community) — https://github.com/menstood/unity-pipeline-commands-skill/blob/main/SKILL.md
- Unity CLI beta.4 (`--detach`, `unity job`) — https://discussions.unity.com/t/unity-cli-1-0-0-beta-4-is-rolling-out/1733720
- uLoopMCP record/replay input — https://www.skills.sh/hatayama/unity-cli-loop/uloop-record-input
- "Adopt the Brain, Defer the Bridge" — https://dev.to/furic/adopt-the-brain-defer-the-bridge-unitys-agent-plugin-and-free-cli-three-weeks-on-1kei
- Claude vision token cost — https://platform.claude.com/docs/en/build-with-claude/vision
