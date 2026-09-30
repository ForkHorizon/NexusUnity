# NEXUS UNITY — MASTER PLAN v2

Version 2.1 · 2026-09-30 · Owners: Kiryl / Daliys (ForkHorizon)
Revised after an adversarial review (technical + strategy challengers, independent judge); see §15.
Horizon: 12 weeks (target start 2026-10-05) with two hard decision gates (week 4, week 9).
**Status: ACTIVE. Supersedes `MASTER_PLAN.md` (v1, 2026-09-01) and the forward-looking parts of
`ImageCapturePlan.md` (M0–M4 are done; M5 is replaced by §9 here).** Only one plan is active at a time.

Inputs: review of all `NexusFeature~` material, the repo at `ef13f8e` (re-checked at `46a00d7`/`f4d728f`; cited lines
still match), web research on Unity CLI / Pipeline (Sep 2026), four independent analyses kept in `NexusFeature~/v2_analysis/`:
- `A_build_on_unity.md` — what to build on top of Unity CLI (composites, speed, tokens)
- `B_skeptic.md` — what not to do, risks, kill list, process guardrails
- `C_agent_efficiency.md` — adoption, token economics, benchmark v2 spec
- `D_architecture_roadmap.md` — target architecture, method migration, roadmap, go-to-market

and two adversarial challenges of v2.0 (technical, strategy), judged in §15.

---------------------------------------------------------------------
## 0. КРАТКО ДЛЯ ВЛАДЕЛЬЦЕВ (RU)
---------------------------------------------------------------------

**Суть.** Unity бесплатно отдала агентам базовый доступ к Editor: транспорт, подключение клиентов,
базовые команды (включая скриншоты, иерархию, свойства компонентов), `eval`, тесты, билды, 31
официальный skill. Соревноваться там бессмысленно, и метафору «Unity — руки, Nexus — глаза» мы
убираем: у Unity тоже есть «глаза». Nexus позиционируется на проверяемом утверждении:
**«Nexus возвращает то, что Editor реально показывает и реально скомпилировал, — иерархию, значения
инспектора, состояние импорта, картинку с Overlay UI, ошибки компиляции — одним вызовом и до 1k
токенов, и доводит до конца запуск ваших editor-инструментов».**

**Главная проблема — агенты не вызывают Nexus** (в 4 ретроспективах реальных сессий: в 3 — ни
одного вызова, в 4-й — 2; это гипотеза на N=4, а не доказанный факт). Поэтому первые 4 недели —
честные замеры, дешёвые фиксы, доступность (skills с публикацией уже на 2–3 неделе, инструкции в MCP,
`unity command nexus_*`) и первые внешние тестеры.

**Флагман изменён.** Реальные сессии просили не «play → input → assert», а «живую правду Editor»:
снимок иерархии/инспектора/импорта с картинкой (отчёт 3 — пользователь вручную слал скриншоты),
правду компиляции (отчёты 1–2) и запуск существующего editor-инструмента с аргументами до
завершения импорта (отчёт 4). Поэтому:
- **Шаг 1 (недели 4–6) — «Live truth»:** `nexus_snapshot`, `nexus_run`, плюс `nexus_compile` /
  `nexus_look` из Phase 1. Демо: «агент перестал просить у вас скриншоты».
- **Шаг 2 (недели 6–9) — `nexus_verify`, только если есть место.** Ниша занята: uLoopMCP (v3.0.1:
  клавиатура/мышь/клик по UI/replay), akiojin/unity-cli (skill playmode-testing), Funplay, а демо
  самой Unity показывает, как агент тестирует в Play Mode и ловит баг. Если в спайке P0-10b ≥2
  конкурента находят наш подложенный баг — verify не строим как флагман, строим только недостающее.

**Технические поправки.** Domain reload при входе в Play Mode и после компиляции — это норма, а не
сбой: длинные операции делаем возобновляемыми (job store + resume). Ускорение main thread — сначала
1-дневный спайк (Pipeline страдает от того же троттлинга), релиз 1.7.0 его не ждёт. Публичные имена —
`nexus_*` с подчёркиванием (точки в именах тулов не проверены и уже не используются в коде). Ввод —
только Input System. Один `EditorUnityPrimitives` на публичных API вместо двух адаптеров. Без
генератора кода: ручные обёртки + тест на расхождение. Идемпотентность запросов (`request_id`).

**Чего НЕ делаем:** гонку за количеством тулов, свой `execute_code` (и его «прокладку» — используем
`unity eval`, это документируем), свой транспорт, брокер, установщики под 10 клиентов, перехват
нативных диалогов, телеметрию, миллисекундные сравнения как главный аргумент.

**Два решения-«ворот» с правом остановиться (критерии регистрируются заранее, промпты пишет не автор
skill, N=8–10 на ячейку, ≥2 задачи из стороннего open-source проекта):**
- **Неделя 4 (Gate A):** (A1) Nexus вызывается на шаге, где нужна правда Unity, в ≥60% релевантных
  прогонов, и нижняя граница 90% CI выше верхней границы базовой линии; (A2) решение по verify по
  результатам спайка конкурентов. Провал A1 → 2 недели на доступность и повтор, затем запасной план.
- **Неделя 9 (Gate B):** verify против **лучшего** конкурента (не только против голой Unity CLI):
  успешность ≥ конкурента **И** ложные «готово» ≤ конкурента, плюс заранее заданный эффект; прогон и
  на Windows; ≥5 внешних пользователей попробовали, ≥3 подтвердили использование.

**Параллельно:** в неделю 1 — предложить Capture V2 (Overlay) / readiness апстриму (Unity, Ivan,
uLoop); публикация на OpenUPM вместе с 1.7.0; Windows-проверка до Gate B; COMPARISON с Ivan,
Coplay, Funplay, akiojin, uLoop.

**Решения, которые нужны от вас сейчас** — см. §10 (G1–G11). Рекомендации даны.

---------------------------------------------------------------------
## 1. FACTS THIS PLAN RELIES ON (each with evidence and expiry)
---------------------------------------------------------------------

Rule (new): a "fact" needs an evidence link, a date and an expiry. Anything else is a hypothesis.
No "DO NOT REOPEN" sections. Facts expire on a Unity / Pipeline version bump or after 30 days.

| # | Fact | Evidence | Expires |
|---|---|---|---|
| F1 | Unity CLI 1.0.0-beta.11 (2026-09-22) + `com.unity.pipeline` (experimental, 0.x-exp), free, Unity 6.0+ | docs.unity.com/en-us/unity-cli/release-notes (via search only; docs.unity.com is not fetchable from the review container) | next CLI release |
| F2 | Unity deprecated its in-Editor AI Assistant MCP server; `unity mcp` replaces it; Unity recommends `unity command`/`unity eval` over MCP for shell-capable agents | docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli (via search) | next CLI release |
| F3 | Pipeline built-ins include status, console, hierarchy, component get/set, play/stop, screenshot/capture_game_view, recompile, build, tests, packages, bakes, eval, menu, `set_autotick`; `[CliCommand]` auto-discovery with `MainThreadRequired` (default true); `unity shell --protocol ndjson` warm process. **`--detach` + `unity job` is sourced only from a forum post** (not in the official SKILL.md) → hypothesis until P0-10 | Unity-Technologies/skills `unity-cli` SKILL.md; community command list; beta.4 forum post | next Pipeline release / P0-10 |
| F4 | No keyboard/mouse Play Mode input or UI automation found in official Unity CLI material | A §5 (official skill + references fetched) | **must be re-checked in P0-10 against live `unity command` listing** |
| F5 | Third parties already ship Play Mode input + UI clicks + screenshots: `hatayama/unity-cli-loop` (uLoopMCP) v3.0.1 has `simulate-keyboard`, `simulate-mouse-input`, `simulate-mouse-ui`, `replay-input`, `screenshot`, `record-video` over its own socket/named-pipe IPC (not Pipeline); `akiojin/unity-cli` ships `unity-playmode-testing`, `unity-input-system`, `unity-ui-automation` skills; Funplay ships a core profile with input sim + screenshots | github.com/hatayama/unity-cli-loop; github.com/akiojin/unity-cli; skills.sh listings | P0-10b |
| F6 | **Hypothesis (N=4, self-written agent retrospectives, not transcripts):** in 3 of 4 sessions Nexus was never called; in the 4th, 2 calls. Agents used shell, asked the user for screenshots, or drove the GUI with computer-use. Report 3 cannot confirm Nexus was even installed (`3.md:31`); report 4 ran in a computer-use client | `AI Chat reports Before all changes/1-4.md` | re-measured in P0-09 |
| F7 | v1 benchmark "ok%" = no JSON-RPC error; C10 invalid for all tools (fixture has 0 tests; bridge result was `Submitted`/`Timeout`); C12 invalid (no EventSystem; Funplay click hit nothing; Coplay screenshot `success:false`); C8 bridge used OS screencapture; C7 raw 99 ms = false-ready (n=4; listener died on reload) | raw JSON in `NexusFeature~/results/`; `BENCH_RESULTS.md:117` | permanent (v1 numbers are void) |
| F8 | Main-thread latency steps (~100 ms) = Editor update throttling in Edit Mode while unfocused. Bridge "fast" run was in Play Mode (`isPlaying:true`), raw runs in Edit Mode. Nexus queue drains only on `EditorApplication.update`, nothing wakes it. **Persistent Pipeline shows the same 3 ticks × ~100 ms**, so pipeline mode does not escape it; macOS App Nap is already disabled (`AppNapBypass.cs`), so the remainder is Unity's own background throttling | `bench_nexus_bridge_fast.json`; `MCPServer.cs:145`, `MCPServerMethods.cs:215`; `STABILIZATION_ACCEPTANCE_REPORT.md` §E (lines ~135-144) | until P0-01a re-measures |
| F9 | Hybrid M1–M4 is implemented but only 3 commands reach Pipeline; they are registered as **`nexus_project_map`, `nexus_group_compile_errors`, `nexus_capture_game_view` (underscores)**; the dotted `nexus.*` string is only the internal `Id` | `Editor/Pipeline/NexusPipelineCommands.cs:21,31,43`; `Editor/Commands/*Command.cs` `Alias` | — |
| F10 | Capture V2 includes Screen Space Overlay UI; orientation proven on Metal only; depends on private `GameView.m_RenderTexture`; applies an **unconditional** vertical-flip Blit, so D3D output is likely upside down | Acceptance report §F; `Editor/Capture/GameViewCaptureSource.cs:14-16,52-55` | Windows run (L-05) |
| F11 | Unity's own launch material shows an agent (Pi harness + Unity CLI) entering Play Mode, inspecting runtime state with `unity command eval`, fixing and re-verifying a bug — "agent verifies its own fix" is the category's default demo, not a Nexus-unique claim | gamedev.net/news/5423; runtimewire.com (Unity CLI article); unity.com Pipeline walkthrough (via search) | next Unity CLI release |
| F12 | Claude image input costs ≈ w×h/750 tokens: 768×432 ≈ 0.44k, 1024×576 ≈ 0.79k, 1024×1024 ≈ 1.4k | platform.claude.com vision docs | model change |
| F13 | Tool names in major LLM APIs are restricted to `[a-zA-Z0-9_-]` (Anthropic `^[a-zA-Z0-9_-]{1,64}$`); dotted MCP tool names may be rejected or mangled by clients (not yet tested for `unity mcp`) | Anthropic tool-use docs; P0-10 naming check | P0-10 |

Verified code defects used below (file:line checked on `ef13f8e`, re-checked on `f4d728f`):
- D-1 (minor) `shutdown_server` needs no token and requests without `Origin` are accepted → any local process can stop the server (`MCPServer.Http.cs:23,132`). Impact is nuisance-level: a same-user process can read the token file anyway (`MCPServer.Identity.cs:122`), and browsers are blocked by Origin/Host loopback checks. Fix stays, severity lowered.
- D-2 A main-thread call that times out after 60 s still runs later → a client retry can apply a write twice; after timeout the lambda also calls `signal.Set()` on a `ManualResetEventSlim` already disposed by `using` → `ObjectDisposedException` on the main thread. A cancel flag only helps calls still *queued*; a call already *running* past 60 s still applies (`MCPServerMethods.cs:215-227`).
- D-3 `wait_for_editor_idle` checks only `IsCompilingCached/IsUpdatingCached` (`MCPServerMethods.Sync.cs:76`), which refresh only on a main-thread tick (stale while throttled) and ignore pending refresh / play transition (#0013). `Status.cs:22` has a richer predicate → two inconsistent readiness predicates today.
- D-4 Bridge compile wait uses fixed sleeps (0.5 s / 2.0 s / 1.0 s) and can wait ~20 s for a reload that never comes (`nexus_bridge/routes_editor.py:73-97`).
- D-5 Pipeline asmdef `versionDefines` is open-ended `0.0.0-exp` → a breaking Pipeline release breaks user compiles (`Editor/Pipeline/UnityMCP.Editor.Pipeline.asmdef:22`).
- D-6 Bridge `initialize` sends protocol `2024-11-05`, no `instructions`, declares `prompts` but `prompts/list` returns -32601 (`nexus_unity_bridge.py:164-166`); results are text-only (images arrive as base64 text, `:150-153`).
- D-7 Bridge never sets MCP `isError`; `Timeout`/`PartialSuccess` look like successful calls to agents.
- D-8 Legacy HTTP bind is skipped when the port **is owned by another Unity project** and Pipeline is the effective runtime (`MCPServer.Networking.cs:105-113`, `NexusRuntimeSelector.cs:60-68`); the HTTP-only Python bridge then talks to the wrong project or nothing → ~117 methods unreachable for that project. Fix = per-project port discovery for the bridge, not "always bind".
- D-9 Full stack traces returned in JSON-RPC error `data` (token waste, info leak) (`MCPServerMethods.cs` `CreateExceptionResponse`/`CreateErrorResponse`).
- D-10 `group_compile_errors` scrapes Nexus's own 50-entry console ring buffer: line/column stay only inside raw message strings (unstructured), errors logged before server init or across reload are missed, file key is a substring heuristic (`GroupCompileErrorsCommand.cs:61-80`).
- D-11 The "confirm gate" for `.cs` writes is `confirm:true`, which the agent sets itself (`MCPServerMethods.Utils.cs:287-291`). It is an intent flag, not a human gate; docs and SECURITY.md must not call it a gate.

---------------------------------------------------------------------
## 2. POSITIONING
---------------------------------------------------------------------

> **Nexus — live Editor truth for Unity agents.** Nexus returns what the Editor actually shows and
> actually compiled — hierarchy, inspector values, import state, Overlay-correct images, structured
> compile diagnostics — in one call and under 1k tokens, and runs your existing editor tools to
> completion. Works with Unity CLI; also runs standalone.

- The v2.0 metaphor "Unity gives hands, Nexus gives eyes and judgment" is **dropped**: Unity already ships screenshots, hierarchy and component reads (F3), so the metaphor invites "Unity already has that". Claims must be concrete and checkable against a benchmark artifact.
- Brand "Nexus" + badge line "Works with Unity CLI". Do not name the product after Unity CLI (beta dependency, trademark risk). The **skill id and listing use a descriptive, searchable name** (working name `unity-live-editor-truth`, title "Nexus: live Editor truth for Unity agents"); acceptance test in S-01.
- Headline demo: **"Your agent stops asking you for screenshots."** — a replay of real report 3 (and report 4: run the packer tool to completion) side by side with Nexus.
- "The agent says it's fixed. Nexus proves it." becomes the **secondary** line and is used publicly only if Gate B passes (F11: it is the category's default demo).
- Security is repositioned honestly: *Nexus's own commands are sandboxed; Unity `eval` is outside Nexus's control; `confirm:true` is an agent-set intent flag, not a human gate (D-11).* It is not a headline differentiator.

### 2.1 Target user
- **Primary:** a Unity programmer using Claude Code / Codex on an existing production project (not greenfield scene-building), on Windows or macOS — the user who produced reports 1–4.
- **Secondary:** tools programmers who need agents to run existing editor tooling (report 4).
- **Not targeted:** QA teams testing player builds (AltTester, GameDriver own this), no-code creators (Unity AI).
- Every benchmark task set must include tasks derived from this persona's real sessions (reports 1–4) and ≥2 tasks from a third-party open-source Unity project, not the synthetic fixture alone.

---------------------------------------------------------------------
## 3. PRINCIPLES (apply to every task)
---------------------------------------------------------------------

1. **Don't duplicate Unity.** If Pipeline has an atomic command, Nexus calls it (pipeline mode) or keeps its frozen legacy equivalent (legacy mode). No new atomic CRUD.
2. **Every new capability must either replace ≥3 agent calls, or do something Unity and the shipping competitors cannot.** Otherwise it is not built.
3. **One logical call, Editor-side.** Composite workflows run inside the Editor with event-driven waits (compile/reload/play-mode/test callbacks), never fixed sleeps. **Domain reload is the normal path** (play entry with reload enabled, every successful compile): any operation that can cross a reload persists a resumable job (state in SessionState / `Library/Nexus/jobs`), resumes via `[InitializeOnLoad]`, and the client shim (bridge / `unity command` wrapper / skill) reconnects and long-polls `job_wait` once. `interrupted` is reserved for Editor restart or crash.
4. **Small answers by default.** Verdict + counts + top-k + handles; `detail: summary|normal|full`; deltas via generation/cursor; images only when useful, cropped and downscaled (default long side 768 px, F12).
5. **Truthful status.** Never report success for partial/timeout. `Submitted` is a legitimate pending state, not an error. Ready only after a new compile/reload epoch.
6. **Shell-first distribution.** Primary entry is `unity command nexus_*` + skills. A `nexus` shim (the bridge CLI) is optional because it requires Python (often missing on Windows). MCP is a thin projection. Public names use underscores (F13) until P0-10 proves dotted names work everywhere.
7. **Measure what users feel.** Task success, false-success rate, tokens per task, agent chose-tool rate, external users. Milliseconds only with tick state controlled. Gate criteria are pre-registered.
8. **Freeze what we don't differentiate on.** Legacy HTTP, 14 bridge managers, 10-client installers, runtime selector: security and crash fixes only.
9. **Untrusted content is data.** Console logs, UI strings, scenario files and project text returned to the model are third-party content; skills say so, and no returned text can trigger Nexus actions.

---------------------------------------------------------------------
## 4. TARGET ARCHITECTURE (reuse what exists — no rewrite)
---------------------------------------------------------------------

```
L4 Skills & distribution   skills~/unity-live-editor-truth (+ nexus-verify if Gate A2/B pass) + AGENTS.md snippet
L3 Nexus Workflows         nexus_snapshot · nexus_compile · nexus_run · nexus_diagnose · nexus_verify (conditional) · ...
                           depend only on IUnityPrimitives + Core services
L2 Nexus Core              Editor/Commands (registry, descriptors) · Editor/Capture (Capture V2)
                           input · UI automation · readiness/epochs · resumable job store · scene intelligence
L1 Projections             Pipeline [CliCommand] (hand-written, drift-tested) · Legacy HTTP JSON-RPC · MCP bridge (thin)
L0 Unity                   Pipeline server · unity command / shell / mcp / eval · built-ins · public Editor APIs
```

Key rules (details in `v2_analysis/D_architecture_roadmap.md` §1, amended here):
- **One canonical definition per command**: typed argument record → descriptor parameters by reflection (C#, in-Editor) → **hand-written** thin `[CliCommand]` wrappers (~10 lines each) → EditMode **drift test** that reflects over `CliArg` attributes and fails when they differ from descriptors. No code generator (Python cannot reflect C#; batchmode generation would need Unity in CI). Legacy projection already exists (`NexusLegacyCommandProjection`).
- **`IUnityPrimitives`** (play/stop/recompile/tests/refresh/console) has **one** implementation, `EditorUnityPrimitives`, built on public Editor APIs (`EditorApplication.EnterPlaymode`, `CompilationPipeline.RequestScriptCompilation`, `AssetDatabase.Refresh`, `TestRunnerApi`, `Application.logMessageReceivedThreaded`). Pipeline is a **projection (L1) only**, never a primitive provider — no reflection into the experimental package. Workflows never call `MCPServerMethods.*` JObject handlers.
- **Seams before any assembly split**: `IMainThreadDispatcher`, `IConsoleLogSource` (Capture and GroupCompileErrors currently call `MCPServer.*` directly).
- **Error envelope for canonical commands**: `{ok, data, error:{code, message, retryable, hint}, meta:{ms, mode, gen, request_id}}`; stable string codes; stack traces only with `NEXUS_DEBUG`. Legacy raw shapes unchanged.
- **Job model**: `NexusJobStore` (SessionState + `Library/Nexus/jobs/<id>/`, size and retention capped) with step cursor + `[InitializeOnLoad]` resume, so a job survives play-entry and compile reloads and completes; `interrupted` + partial report only on restart/crash. Legacy and MCP get `nexus_job_status/wait/cancel`; pipeline mode uses Unity `--detach` + `unity job` only if P0-10 confirms they exist.
- **Idempotency**: mutating calls accept `request_id`; the server remembers results ~5 min; a retry with the same id returns the stored result; a mutation that times out while executing returns `outcome:"unknown"` + `request_id`.
- **Handles and job ids** (`cap://`, `log://`, `snap://`, jobs) are server-issued opaque ids `[a-z0-9]{8,}`, never paths.
- **Pipeline compatibility**: pin `versionDefines` to a tested range; out-of-range → assembly excluded → `auto` falls back to legacy; CI contract job for Pipeline N and N-1. Write-capable parameters are **not** projected to Pipeline until Pipeline's auth model is documented (Pipeline does not check the Nexus token).
- **Python bridge**: freeze the 14 managers; add dynamic pass-through of canonical commands driven by `list_tools` metadata (new C# command appears in MCP with zero Python edits); profiles chosen at startup (env var / argument), `load_profile` + `tools/list_changed` best-effort only; per-project port discovery (D-8); reuse its CLI mode as the optional `nexus` shim.

---------------------------------------------------------------------
## 5. THE PRODUCT SURFACE
---------------------------------------------------------------------

### 5.1 Default profile `core` (≤8 commands, schema budget ≤2.5k tokens, measured)
| Command | What it does | Replaces |
|---|---|---|
| `nexus_status` | Truthful state + compile/reload epochs; `wait` mode returns the epoch it observed; `detail:"capabilities"` returns a ≈300-token map of all jobs/profiles (answers "only 14 of 120 visible"); `detail:"doctor"` = which transport, port, profile, skill installed | status polls, false-ready, first-run confusion |
| `nexus_compile` | (optional sandboxed writes) → refresh → compile → grouped diagnostics with file/line/col + "did you mean" hints; reports Hot Reload / FastScriptReload presence | 6–15 calls + log dumps |
| `nexus_snapshot` | Live truth in one call: selection/path → hierarchy path, active state, RectTransform, component summary, serialized refs, import state, optional cropped Game/Scene View image | report 3: user-sent screenshots + YAML reading |
| `nexus_look` | Capture V2: JPEG, default long side 768 px, crop to object/UI element, Game View (incl. Overlay) or Scene View framed on object, optional numbered UI marks | full-frame PNG / human screenshots |
| `nexus_run` | Run a menu item or an allowlisted editor method with arguments, then wait event-driven for import/compile/reload; reports a probable modal dialog blocking the main thread | report 4: computer-use on folder dialogs |
| `nexus_diagnose` | "Why is it broken now": state, compile, grouped console errors, missing refs, last test failures | 5–7 calls |
| `nexus_find_references` | Project-wide cached reverse index (scenes, prefabs, variants, SOs, Addressables); Force Text serialization only | guid grep that misses semantics |
| `nexus_get` | Handle resolver (`cap://`, `log://`, `snap://`) with range/crop | large default payloads |

Other profiles, chosen at startup (or loaded on demand where the client honours `list_changed`): `qa` (`nexus_verify`, input, ui_query/ui_act, step_frame — only if Gate A2/B pass), `context` (`nexus_context`, promoted to core only if C-01 passes its ≥30% bar), `scene` (scene_diff, snapshot — backlog), `perf` (later), `full` (legacy managers + raw escape hatch `nexus_call` / `nexus_list`). Before promising the budget, measure the verify DSL schema (may cost ~1k alone).

In-memory C#: Nexus does not ship `execute_code` or an `eval` forwarder. Skills, `nexus_status detail:"capabilities"` and COMPARISON.md state: "in-memory C# = `unity eval` (requires Pipeline, same Unity 6.0 floor as Nexus)".

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
8. **Security invariants** (loopback, token, path sandbox + symlink resolution, `confirm:true` intent flag for `.cs` writes / delete_asset / PlayerPrefs all, type allowlists, batch cap 50, ProjectSettings/Packages write-protected) are untouchable. Additionally for new commands: compile writes also block `*.rsp`, `.dll`, `.asmdef`/`.asmref` with `precompiledReferences`, and `Packages/`; handles/job ids are opaque server ids; job storage is size- and retention-capped; `nexus_context` honours `.gitignore` + a deny-list (`*.env`, keystores, `**/secrets*`); scenario files live in a non-imported folder (`NexusScenarios~/`) and use a closed step set (no reflection setters, no menu steps outside `full`); any temporary ProjectSettings/EditorSettings change is restored after the run and labelled in the report; write parameters are not projected to Pipeline. If the task conflicts, STOP.
9. **Frozen areas** (legacy HTTP methods, 14 bridge managers, installers, runtime selector): only security/crash fixes unless the task explicitly names them.
10. **Public API**: raw JSON-RPC methods keep their shapes. New canonical commands follow SemVer (add param = minor; remove/rename = alias for 2 minors). Public names use underscores (`nexus_*`). Keep `CHANGELOG.md`, `README.md`, `DOCUMENTATION.MD`, `API_REFERENCE.MD` aligned.
11. Unity floor 6000.0. Newer APIs behind `#if UNITY_X_OR_NEWER`. Pipeline code only in the Pipeline asmdef.
12. Benchmarks: success = semantic check (task oracle), never "no JSON-RPC error". Record Editor focus/tick state and `isPlaying` per sample. Gate runs follow the pre-registration file; no post-hoc changes to tasks, relevance or thresholds.
13. No public performance claims without a benchmark v2 artifact for that platform.
14. Human review is the bottleneck: at most 4 open PRs at once. Unity-facing evidence runs take the harness lock (P0-08c); never run two agents against one Editor.

---------------------------------------------------------------------
## 7. ROADMAP (weeks from 2026-10-05; each task ≤3 days)
---------------------------------------------------------------------

Legend: **E:** = evidence required in the PR.

### PHASE 0 — Honest measurement + cheap fixes (weeks 1–2)

**P0-01a · Main-thread wake-up spike** (1d) [spike, doc only]
- Measure Edit Mode read latency with Editor focused / unfocused / minimized / on another Space, with and without Pipeline `set_autotick`, Interaction Mode settings, App Nap state; find out what `set_autotick` actually calls. Note: `QueuePlayerLoopUpdate` is main-thread-only and posting to `UnitySynchronizationContext` runs on the same throttled tick — do not assume either wakes the loop.
- E: `Research~/evidence/P0-01a/wakeup.md` with histograms and a sized proposal for P0-01b.

**P0-01b · Main-thread wake-up implementation** (≤3d, sized by P0-01a) · Files: `MCPServer.cs`, `MCPServer.Logs.cs` (`Enqueue`), settings
- Agent-session-scoped: no throttling only while an agent session is active; restore the user's interaction mode after idle. Do not call main-thread-only APIs from the listener thread. If the spike finds no safe lever, record it and drop the task (P0-02/S-05 must not depend on it).
- E: p50 object read <20 ms *while an agent session is active* (Edit Mode, unfocused, macOS); idle CPU unchanged *when no session is active*.

**P0-02 · Truthful readiness (#0013) + epochs** (2–3d) · Files: `MCPServerMethods.Sync.cs`, `.Status.cs`, `.Hierarchy.cs`
- `compile_epoch`/`reload_epoch` from `CompilationPipeline` + `AssemblyReloadEvents`, **persisted in SessionState** (statics reset on reload); pending-refresh / script-write / play-transition latches; **one** readiness predicate shared by status and waits (replaces the two in D-3); wait returns `{ready, epoch, reason}`; a wait cannot span a reload in one HTTP request, so the bridge/shim re-polls after reconnect.
- E: write→compile 20/20 runs report ready only after reload; time matches Editor.log (≈7.5–8.5 s on the harness), not 99 ms.

**P0-03 · Remove fixed sleeps from the bridge** (1–2d) · Files: `nexus_bridge/routes_editor.py`
- Replace 0.5/2.0/1.0 s sleeps and the 20 s "wait for reload" with epoch-based waiting (P0-02). `no_change` returns immediately.
- E: "refresh + wait compilation" with nothing to compile returns in <1 s (report 2 took 19 s).

**P0-04 · Truthful status to agents** (2d) · Files: bridge result wrapping, `MCPServerMethods.cs` error helpers
- `Timeout`, `PartialSuccess`, `Error` → MCP `isError:true` with `code/hint/retryable`; a mutation `Timeout` → `retryable:false`, `outcome:"unknown"`, `request_id`. **`Submitted` → `isError:false` with `{state:"pending", job, next:"job_wait"}`** (flagging it as an error teaches agents to retry → duplicate test runs/writes). Stack traces only with `NEXUS_DEBUG` (D-9).
- E: contract tests for each status; v1 C8/C10 responses reproduced and now flagged correctly.

**P0-05 · Security + timeout safety** (2d) · Files: `MCPServer.Http.cs`, `MCPServerMethods.cs`
- `shutdown_server` requires the token (zombie cleanup reads the token file) — minor (D-1). Cancel flag so a timed-out *queued* call never executes later; fix the disposed-`ManualResetEventSlim` bug (D-2); `request_id` idempotency cache (~5 min) for mutating methods; SECURITY.md states `unity eval` is outside Nexus gates and `confirm:true` is an intent flag (D-11).
- E: tests — unauthenticated shutdown rejected; timed-out queued mutation not applied; retry of a *running* mutation with the same `request_id` is not applied twice; no `ObjectDisposedException` after timeout.

**P0-06 · Pin Pipeline version range** (1d) · Files: `Editor/Pipeline/UnityMCP.Editor.Pipeline.asmdef`, tests
- Replace `0.0.0-exp` with the tested range, with an explicit test of Unity prerelease (`-exp`) range semantics; out-of-range → legacy fallback.
- E: compiles with Pipeline absent and in range; out-of-range verified in a **second scratch project** with a different Pipeline version (a package version cannot be faked in EditMode).

**P0-07 · MCP handshake that sells Nexus** (1–2d) · Files: `nexus_unity_bridge.py`, bridge schemas
- `initialize.instructions` (≈150 words: when to use Nexus vs shell, lists the profiles and `nexus_status detail:"capabilities"`); current MCP protocol version; image content blocks instead of base64 text; implement `prompts/list` with 3 prompts or stop declaring `prompts`; tool descriptions start with "Use when …".
- E: `initialize` transcript; screenshot arrives as an image block; token count of the default tools/list.

**P0-08 · Benchmark v2: fixture + protocol harness** (3d) · Files: `NexusFeature~/bench/`, new fixture project (separate repo or `Research~/bench-fixture`)
- Fixture and scenarios P1–P11 per `v2_analysis/C_agent_efficiency.md` §3 (Canvas+EventSystem+toggle button + planted-bug variant incl. an Overlay-only visual bug; 12 EditMode tests with 1 known failure; 3-error compile case incl. asmdef; prefab reference graph with decoy; 1,500-object scene) + a report-3 replay scene (Canvas/Image mis-setup) and a report-4 replay (menu tool with folder arguments + import).
- Three scores per sample: transport_ok / tool_status_ok / **semantic_ok (headline)**; focus/tick condition and `isPlaying` recorded; assert-mode dry run before timing; histogram with 100 ms clustering flag; N=30 warm reads / 10 for long ops; interleaved rounds; **token column for raw `unity command` outputs** (hierarchy, console, component) — decides fallback A4.
- Competitors: Unity CLI (persistent `unity mcp` / `unity shell`, and cold `unity command`), Nexus 1.6 vs 1.7. Others in P0-10b.
- E: raw JSON + `BENCH_RESULTS_V2.md`; `BENCH_RESULTS.md` v1 marked void where invalid.

**P0-08b · Agent-run harness** (3d) · Deps: P0-08
- `claude -p` / `codex exec` runner, per-run fixture reset, transcript token parsing, automated oracles per task, chose-tool classification of calls, result JSON per run. Cost is modest (~$100–400 API or subscription limits); serial Editor time (15–37 h for ~90 runs) dominates, hence P0-08c.
- E: 3 dry runs per task end-to-end, oracles agree with a human check.

**P0-08c · Harness lock + second fixture Editor** (2d)
- 2 worktree copies of the fixture, each with its own Editor, port, token and `Library`; a lock/schedule script that every Unity-facing E run and benchmark run must acquire. (Not a full farm; 2 Editors match 2 reviewers.)
- E: two concurrent agent runs on separate copies without port/token/reload interference.

**P0-09 · Adoption baseline (agent-level pilot)** (3d) [DECISION input] · Deps: P0-08, P0-08b
- **Pre-registration first** (`NexusFeature~/results/PREREG_GATE_A.md`, committed before any run): tasks, per-task definition of the "Unity-state step" that counts as relevant, oracles, thresholds, N. Prompts are written by the partner or a separate agent that has not seen the skill, then frozen.
- Tasks: 4–5 modelled on reports 1–4 + fixture (live-truth Canvas question, why doesn't it compile, who references Enemy.prefab, run packer menu item and confirm import, source-only control) + **≥2 tasks from a third-party open-source Unity project**. Arms: S1 Unity CLI + official skills + an equivalent "use the Unity CLI skills" AGENTS.md snippet; S3 Nexus MCP **pinned to the 1.6.0 release** (so P0-04/P0-07 merges cannot contaminate it). Agents: Claude Code + Codex, default settings (deferred tools on). **N=8–10 per cell on the 2–3 relevant tasks**, N=3 on the controls.
- Metrics: semantic success, **false-success rate**, tokens, wall time, calls by category, **chose-Nexus** (with Wilson 90% intervals).
- E: `NexusFeature~/results/ADOPTION_V1.md` with transcripts.

**P0-10 · Ground truth on Unity** (2d) [spike, doc only]
- On the harness: dump live `unity command` list for the installed Pipeline; confirm or refute `--detach` / `unity job` (F3); Play Mode input? Overlay UI in `capture_game_view`? profiler depth? player-build command registration? Pipeline pre-dispatch hook (could a policy layer gate `eval`? — input to G8).
- **Naming check:** register a test command as `nexus.verify_probe` and `nexus_verify_probe` via `[CliCommand]`; call both from `unity command`, `unity mcp` in Claude Code, and Codex. Until the dotted form passes everywhere, all public names are `nexus_*`.
- **Input focus check** (0.5d): inject a key via Input System into an unfocused Editor in Play Mode, with and without Game View focus; note which `backgroundBehavior`/focus settings are required and whether they need a ProjectSettings write.
- E: `Research~/evidence/P0-10/unity_capabilities.md` with command list and screenshots. Updates F3/F4/F13.

**P0-10b · Competitor verify spike** (2d) [spike, DECISION input for Gate A2]
- Install in scratch copies of the fixture: uLoopMCP (unity-cli-loop), akiojin/unity-cli, Funplay core profile, IvanMurzak Unity-MCP, and Unity CLI alone. For each, an agent tries to detect the fixture's planted Overlay-toggle bug and the report-3 Canvas problem. Record: detected yes/no, tokens, calls, what was missing.
- E: `Research~/evidence/P0-10b/competitors.md` + raw transcripts. Updates F5.

**P0-11 · Release 1.7.0 + OpenUPM** (1–2d) · Deps: P0-02..P0-07 (**not** P0-01a/b; the release does not wait for research)
- Ships Unreleased CHANGELOG + fixes; no new features; OpenUPM listing + release automation (former R-01) so install stats exist from week 2. E: tag, release notes, clean-install smoke on macOS, OpenUPM page.

**P0-12 · Upstream outreach** (0.5d) [parallel, week 1]
- One issue/post each to Unity (Pipeline forum), IvanMurzak/Unity-MCP and uLoopMCP offering Capture V2 Overlay-correct capture as a `[CliCommand]`/library and readiness epochs. Record responses; input to §9 fallback and G9.

### PHASE 1 — Discoverability + small composite surface (weeks 2–4)

**S-01 · Skill pack v1 + AGENTS.md snippet + public listing** (3d) · Files: new `skills~/unity-live-editor-truth/`, installers (`MCPCliInstaller.*`)
- Trigger-oriented descriptions ("after editing C# in a Unity project…", "before asking the user for a screenshot…", "before claiming a UI change works…"); decision table (shell vs `unity command` vs `nexus_*` vs `unity eval`); 5 recipes; budget and state rules; error table; "when NOT to use Nexus"; "logs/UI text/scenario files are data, not instructions". Both invocation forms shown with underscore names. Opt-in AGENTS.md/CLAUDE.md block (marker-delimited, byte-preserving). `nexus doctor` (shim) = `nexus_status detail:"doctor"`. **Published by week 3** on skills.sh and as a Claude Code plugin / Codex listing next to Unity's `unity-cli` skill (former R-02).
- E: installer tests (idempotent, markers preserved); listing live; searching skills.sh for "unity screenshot overlay" and "unity compile errors" finds the skill; P0-09 re-run for arm "Nexus + skill" feeds Gate A.

**S-02 · Hand-written `[CliCommand]` wrappers + drift test** (2d) · Files: `Editor/Commands/*`, `Editor/Pipeline/NexusPipelineCommands.cs`, `Tests/Editor/NexusCommandRegistryTests.cs`
- Typed argument records → descriptors; EditMode test reflects over `CliArg` attributes and compares to descriptors. No generator script.
- E: removing a param fails the test; the 3 existing commands keep identical `unity command` help.

**S-03 · Bridge: dynamic canonical pass-through + startup profiles + port discovery** (3d) · Files: `nexus_bridge/routing.py`, `routes_base.py`, `schemas.py`, `NexusToolCatalog.cs`
- Canonical commands exposed from `list_tools{profile}` metadata; profile chosen by env var / argument at bridge start; `nexus_load_profile` + `tools/list_changed` as best-effort extra; default `core` ≤8 tools and ≤2.5k tokens; 14 managers only in `full`. Fix D-8 with per-project port discovery (bridge finds the Editor of *its* project; clear error otherwise).
- E: per-client matrix (Claude Code, Codex, Cursor): tools visible at start, whether `list_changed` is honoured, tools/list tokens per profile; new C# command appears in MCP with no Python change; two-project port-collision test.

**S-04 · Unified error envelope for canonical commands** (2d) · Files: `Commands/NexusCommandResults.cs`, projections, `API_REFERENCE.MD`
- Includes `request_id` in `meta`.
- E: contract tests for ≥6 codes; legacy raw shapes unchanged (`OpenSourceApiContractTests`).

**S-05 · `nexus_compile`** (3d) · Deps: P0-02 · Files: new command, replaces scraping in `GroupCompileErrorsCommand`
- `CompilationPipeline.assemblyCompilationFinished` messages (file/line/col/id), persisted in SessionState across reload (fixes D-10); optional sandboxed writes with the extended deny-list (rule §6.8), write params legacy-only in the Pipeline projection; "did you mean" via `symbol_index` for CS0246/CS1061; ±3 lines of source context in `normal` detail; detect Hot Reload for Unity / FastScriptReload and warn that the verdict reflects Unity's compile, not the hot-patched state.
- E: fixture 3-error case returns exactly 3 errors with positions; token size of summary response; `no_change` <50 ms **only if P0-01b landed**, otherwise report measured time.

**S-06 · `nexus_diagnose` + `nexus_get` handles + capabilities map** (3d)
- Opaque server-issued handles; retention cap; `nexus_status detail:"capabilities"` (≈300 tokens, grouped by job with profile names).
- E: fixture broken state → ≤15-line prioritized report; handle fetch with range/crop; path-like handle rejected.

**S-07 · `nexus_look` image hygiene + Scene View** (3d) · Files: Capture command/projections
- JPEG default, **default long side 768 px** (≤0.8k tokens for any aspect, F12), 1024 opt-in; crop to object/UI element; Scene View capture framed on an object; file-path mode for CLI; never both PNG and JPEG.
- E: token cost per capture measured for 16:9 and 1:1; Overlay marker test still passes; Scene View framing on fixture object.

**S-08 · External testers + usage counters** (0.5d, week 3)
- Call for 5–10 testers via the LinkedIn/Telegram contacts who already engaged; a short feedback form. Report OpenUPM downloads, GitHub traffic, skills.sh installs at each gate. (No in-product telemetry.)
- E: `NexusFeature~/results/USAGE.md` updated at Gate A and Gate B.

**▶ GATE A (end of week 4), pre-registered in `PREREG_GATE_A.md`:**
- **A1 adoption:** re-run P0-09 with S4 = Nexus 1.7 (instructions + skill + core profile + CLI). Pass if Nexus is called for the Unity-state step in **≥60% of pre-registered relevant runs** AND the lower Wilson 90% bound for S4 is above the upper bound for S3 AND false-success is not higher than S1 (reported with interval). External-user feedback (S-08) is reported, not a hard criterion yet. Fail → stop feature work, 2 weeks on discoverability, re-run; if still failing → fallback (§9).
- **A2 verify room (desk decision from P0-10b):** if **≥2 competitors detect the planted bug**, `nexus_verify` is not built as a flagship; Phase 3 shrinks to "verify-lite" (V-05 assertions usable by `nexus_snapshot`, Overlay capture as a `[CliCommand]` others can call, skill recipes that combine competitor/Unity input with Nexus assertions). Otherwise Phase 3 proceeds.

### PHASE 2 — Flagship "Live truth" (weeks 4–6)

**L-01 · Seams + `EditorUnityPrimitives`** (3d): `IMainThreadDispatcher`, `IConsoleLogSource`, `IUnityPrimitives` with one public-API implementation. E: grep test — Core files contain no `MCPServer.` references.
**L-02 · Resumable job store** (3d): `NexusJobStore` with step cursor in SessionState + `[InitializeOnLoad]` resume, idempotent step contract, size/retention cap, `nexus_job_status/wait/cancel`. E: a job that crosses a forced `RequestScriptReload` **and** a play-entry reload completes 20/20; `interrupted` + partial artifacts only after a simulated Editor restart.
**L-03 · `nexus_snapshot`** (3d) · Deps: L-01, S-07: selection or path → hierarchy path, active state, RectTransform, component summary, serialized refs (missing refs flagged), import state (last import, pending refresh), optional cropped Game/Scene View image; ≤1k tokens in `summary`. E: report-3 replay — an agent resolves the Canvas problem with zero user screenshots; tokens per success vs S1.
**L-04 · `nexus_run`** (3d) · Deps: L-02: run a menu item or a method marked `[NexusRunnable]` (allowlist; arbitrary code stays `unity eval`) with arguments, then wait event-driven for import/compile/reload via L-02; if the main thread stops ticking during the run, return `PROBABLE_MODAL_DIALOG` with elapsed time (best-effort detection; native folder panels cannot be intercepted and this is not promised). E: report-4 replay (packer tool with folder arguments) completes and reports the import finished; a blocking dialog is reported, not hung.
**L-05 · Windows validation** (3d, moved before Gate B): install, token, ports, Capture V2 on D3D11/D3D12/Vulkan (orientation via `SystemInfo.graphicsUVStartsAtTop`; F10 unconditional flip), input focus, bridge + Python presence, skills. E: benchmark v2 protocol run on Windows.
**L-06 · Windows fixes** (≤3d each).
**L-07 · Release 1.8.0-preview "live truth" + demo** (2d): docs, CHANGELOG, report-3/report-4 replay GIF ("Your agent stops asking you for screenshots"); second call to external testers.

### PHASE 3 — `nexus_verify` (conditional on Gate A2; weeks 6–9)

Task list and oracles for Gate B are frozen in `PREREG_GATE_B.md` **before V-06a starts**, including ≥1 task uLoopMCP / akiojin are designed for.

**V-03 · Keyboard + unified `nexus_input`** (3d): key down/up/press/text/hold, action lists; **Input System only** (legacy `UnityEngine.Input` has no injection API → `LEGACY_INPUT_UNSUPPORTED`); focus handling per P0-10 input check; no ProjectSettings writes, or restore after run. E: PlayMode fixture — WASD moves player, text field receives string, with Editor unfocused.
**V-04a · UGUI targeting** (3d): EventSystem raycast, `NO_EVENTSYSTEM` error, `input_path: eventsystem|invoke` reported. E: fixture P9 (UGUI) passes 10/10.
**V-04b · UI Toolkit runtime panels** (3d, optional before Gate B): `panel.Pick` + synthesized pointer events under the Input System. E: UI Toolkit variant of P9 10/10.
**V-05 · Assertions** (3d): log present/absent, object property op value, UI element visible/text, pixel region color±tol, no new errors; structured fail reasons. Also used by `nexus_snapshot` (verify-lite). E: unit tests per assert.
**V-02b · Resumable scenario runner** (3d) · Deps: L-02: scenario step cursor survives play-entry reload; idempotent step contract. E: verify survives the play-entry reload with domain reload enabled and completes 20/20.
**V-06a · Runner + DSL core** (3d) · Deps: V-02b, V-03, V-04a, V-05: closed step set (5 step types: play, input, wait-frames/until, assert, capture), per-frame predicates, summary ≤400 tokens. E: planted bug is caught; DSL schema tokens measured. **Gate B runs on V-06a.**
**V-06b · Reporting + saved scenarios** (3d): capture-on-fail, report in job dir, scenarios saved under `NexusScenarios~/` (non-imported) as regression tests. E: 3 recipe scenarios 10/10.
**V-07 · Verify via `unity command`** (1d, only if P0-10 confirms `--detach`/`unity job`): parity table legacy vs pipeline projection.
**V-08 · Enter Play Mode fast path** (2d, optional): `EnterPlayModeOptions` (no domain reload) only when a static-state lint passes; setting restored after the run; results labelled "no-reload mode" (lint cannot be complete: third-party DLLs, static events). E: play entry time before/after; lint catches a planted static.
**V-09 · Release 1.9.0-preview "verify"** (1d): docs, CHANGELOG, GIF.

**▶ GATE B (end of week 9), pre-registered in `PREREG_GATE_B.md`:** agent-level run on the frozen verify tasks, macOS **and** Windows: Nexus + Unity CLI vs Unity CLI + official skills (with equivalent snippet) vs **the best verify competitor from P0-10b**. N≥10 per task per arm. Continue only if Nexus's semantic success ≥ best competitor **AND** false-success ≤ best competitor **AND** (≥15 pp higher success OR ≥30% fewer tokens-per-success); tokens alone never pass the gate. External: ≥5 non-team users tried Nexus on their own project and ≥3 report it was used (S-08). Otherwise → verify-lite + fallback options (§9); Live truth continues.

### PHASE 4 — Token-lean context (weeks 8–11, reduced)

**C-01 · `nexus_context` spike** (3d) [research]: task string + `max_tokens`; cached index in `Library/Nexus/` invalidated by postprocessor + compile events; `fingerprint` in responses; `.gitignore` + deny-list. E: tokens-to-first-correct-edit ≥30% lower than shell-only on the benchmark tasks, or the task is dropped (stays out of `core`).
**C-02 · `nexus_find_references`** (3d): project-wide cached reverse index over YAML; **Force Text serialization only** (declared limit; Binary mode → `UNSUPPORTED_SERIALIZATION`). E: fixture P10 returns exactly the 6 referrers, not the decoy.
**C-04 · `detail` / `max_tokens` convention** across canonical commands (2d). E: bench token column per command.

### PHASE 5 — Hardening, release & distribution (weeks 10–12)

**W-03 · Capture fallback + self-test** (2d): startup check that `GameView.m_RenderTexture` still resolves; fallback to Unity capture with an "Overlay may be missing" warning.
**A-01 · Pipeline N/N-1 CI contract job** (2d) + monthly check on Pipeline pre-releases.
**A-02 · Hide/deprecate sweep 1** (2d): per §5.2 policy; docs + CHANGELOG + `list_tools` flags.
**R-03 · Public TOKENS.md + COMPARISON.md** (2d): generated from benchmark v2 JSON only; compares Unity CLI alone, IvanMurzak Unity-MCP, CoplayDev, Funplay core, akiojin/unity-cli, uLoopMCP with scenario oracles; states honestly where each wins (e.g. Funplay for breadth/execute_code, uLoop for input) and where Unity CLI alone is enough.
**R-04 · Demo** (2d): replays of the real failures (report 3 screenshots, report 4 folder dialog) side by side with Nexus; if Gate B passed, add "Agent says it's fixed. Nexus proves it." recorded with `nexus_verify` itself; demo scripts are bench scenarios.
**R-05 · Release 1.9.0** (1d): live truth GA, context tools, Windows supported, verify GA only if Gate B passed.

**Later / backlog (not scheduled):** `nexus_scene_diff` + bounded `scene_snapshot` (former C-03, closes #0016) · `nexus_ui_check` (former C-05) · extract `Nexus.Core.Editor` asmdef (former A-03, only after L-01) · `nexus_perf` (ProfilerRecorder + worst-frame markers; only if P0-10 shows Unity's profiler command is shallow) · `nexus_player_check` (same scenario against a development build; spike first) · `nexus_mutate` transactional batch with undo group + rollback · opt-in local usage export · studio policy/audit layer over Pipeline (G8, only if P0-10 finds a pre-dispatch hook).

Capacity: ~51 tasks × ~2.2 d ≈ 110 agent-days (spikes P0-01a/P0-10/P0-10b/C-01 included); with ≤4 PRs in flight, 2 fixture Editors and 2 reviewers this fills 12 weeks only if the gates pass. If Gate A2 says "verify-lite", Phase 3 drops to ~4 tasks and frees ~2 weeks for Live truth polish and context. Optional tasks (V-04b, V-07, V-08) are the buffer.

---------------------------------------------------------------------
## 8. v1 → v2 MAPPING (what happened to every v1 task)
---------------------------------------------------------------------

| v1 | v2 verdict | Where it went / why |
|---|---|---|
| T01 screenshot | DONE (acceptance numbers missing) | Numbers come from P0-08; Windows in L-05 |
| T02 reload survival | SHRINK | P0-02 epochs + L-02 resumable job store (reload is the normal path) |
| T03 run_tests | SHRINK | Failure summarizer inside `nexus_diagnose`/verify; Unity has `run_tests` |
| T04 QA cycle C12 | CONDITIONAL → V-03..V-06b | Only if Gate A2 says verify has room; needs real fixture (P0-08) |
| T05 perf CI gate | DROP | Tick noise; replaced by semantic + agent-level benchmarks |
| T06 401 matrix | SHRINK | Only if it reappears; P0-05 covers auth hole |
| T07 dispatch tax | SHRINK → P0-01a/b | It is a wake-up problem, and Pipeline has it too; spike first |
| T08 payload diet | KEEP → C-04, S-07 | Tokens are a core axis |
| T09 bridge efficiency | DROP | Bridge is frozen/thin |
| T10 14→32–40 tools | **REVERSED** | ≤8 default + profiles + skills + capabilities map (§5.1) |
| T11 response contract | SHRINK → S-04 | New canonical commands only |
| T12 zero-config wizard | DROP | `unity mcp configure` / skills; installer frozen; `nexus doctor` in S-01 |
| T13 readiness enum | KEEP → P0-02 | |
| D1 bridge language | DROP | Transport is Unity's |
| D2 / T21 execute_code | DROP | Use `unity eval`; documented in skills/COMPARISON; no forwarder |
| D3 broker | DROP | |
| D4 Unity floor | DECIDED: 6000.0 | |
| D5 layout | DONE | |
| T22 Pipeline adapter | DONE | Extend only via S-02 / V-07 (projection only) |
| T23 keyboard + raycast | CONDITIONAL → V-03 (Input System only), V-04a | After P0-10 input check and Gate A2 |
| T24 scene/inspector capture | SHRINK → S-07, L-03 | `nexus_look` crop + Scene View framed on object; `nexus_snapshot` for inspector values |
| T25 QA tools | CONDITIONAL → V-05 (always), V-06a/b (if Gate A2) | Assertions reused by snapshot |
| T26 profiler | BACKLOG | Only if Unity's is shallow |
| T27 skills | **TOP PRIORITY → S-01** | Published by week 3 |
| T28 audit backlog | SHRINK | Only issues new commands depend on (#0013 in P0-02; #0016 moves to backlog with scene_diff) |
| T31 docs overhaul | SHRINK | Docs for the new small surface |
| T32 OpenUPM | KEEP → P0-11 | Moved to week 2 for install stats |
| T33 comparison tables | SHRINK → R-03 | Task-success data only; 6 competitors |
| T34 onboarding smoke | DROP | |
| T35 multi-editor | DONE | D-8 port discovery in S-03 |
| T36 plugin API | DROP | `[CliCommand]` is the plugin API |
| T41–T47, T50 | DROP | Except T43 semantic_find (feeds C-01), T44 scene_delta (backlog), T45 step_frame (qa profile) |
| T48 demo | KEEP → L-07, R-04 | Real-failure replays |
| T49 SECURITY.md | KEEP (in P0-05 PR) | States `unity eval` is outside Nexus gates; `confirm:true` is an intent flag |
| ImageCapturePlan M5 + 11 gates | REPLACED | §9 rule |

---------------------------------------------------------------------
## 9. LEGACY POLICY AND FALLBACK PLAN
---------------------------------------------------------------------

**Legacy HTTP + Python bridge:** frozen (security/crash fixes only), kept as the path for users without Pipeline. Removal only in a major release, after Pipeline leaves experimental and one stable Nexus release has shipped with Pipeline-first `auto`. Everything new is written once in Core and reaches legacy through the existing projection for free.

**Verify-lite (Gate A2 or Gate B fails, Live truth still healthy):** keep V-05 assertions inside `nexus_snapshot`; publish Overlay capture as a `[CliCommand]` other tools can call; ship skill recipes that pair Unity/competitor input with Nexus assertions and captures.

**Fallback destinations if Gate A fails twice (pick by evidence, not by default):**
1. **Contribute (A1):** publish Capture V2 as a small standalone package (`[CliCommand]` + library) and upstream it / readiness epochs to whichever maintainer answered P0-12.
2. **Skills product (A2):** publish the skill pack as verification and debugging recipes on top of `unity command` / `unity eval` + the few Nexus commands.
3. **Editor-tool automation (A3):** if the replays show agents mostly need to run existing editor tooling, narrow to `nexus_run` + `nexus_compile` for tools programmers.
4. **Token-lean proxy (A4):** only if the P0-08 token column shows raw `unity command` outputs ≥3× larger than Nexus equivalents on hierarchy/console.
5. Put the rest in maintenance mode with a clear README note. This is a legitimate outcome, not a failure.

---------------------------------------------------------------------
## 10. DECISIONS FOR THE OWNERS (recommendation in bold)
---------------------------------------------------------------------

| # | Decision | When | Recommendation |
|---|---|---|---|
| G1 | Pipeline-first for new features (as projection); legacy frozen | now | **Yes** |
| G2 | Python bridge: freeze + thin pass-through, no rewrite | now | **Yes** |
| G3 | Own `execute_code` or an `eval` forwarder | now | **No — use `unity eval`; document it in skills and COMPARISON** |
| G4 | Flagship = "Live truth" (`nexus_snapshot`, `nexus_compile`, `nexus_look`, `nexus_run`); `nexus_verify` conditional on Gate A2 and Gate B | now | **Yes (changed in v2.1)** |
| G5 | Tool surface: ≤8 default, startup profiles, skills, capabilities map | now | **Yes (reverses v1 T10)** |
| G6 | Timebox: B's 6-week hard bet vs D's 12 weeks | now | **12-week plan with pre-registered Gate A (wk 4) and Gate B (wk 9) as hard stops** |
| G7 | Player-build verify in these 12 weeks | week 10 | **Spike only, and only if Gate B passed** |
| G8 | Monetization | week 12 | **None yet; keep core MIT. "Nexus CI" is no longer the default (AltTester/GameDriver own it). Candidates: studio policy/audit layer over Pipeline incl. `eval` (only if P0-10 finds a pre-dispatch hook; validate with 5 studio conversations) and consulting** |
| G9 | Upstream outreach in week 1, in parallel (P0-12) | now | **Yes** |
| G10 | Target user = Unity programmer on an existing production project (§2.1) | now | **Yes** |
| G11 | Public names `nexus_*` (underscores) until the P0-10 naming check passes | now | **Yes** |

---------------------------------------------------------------------
## 11. SUCCESS METRICS (what "we're winning" means now)
---------------------------------------------------------------------

| Metric | Baseline (v1 evidence) | Target by week 12 |
|---|---|---|
| Agent chooses Nexus for the Unity-state step (neutral, pre-registered prompts) | ~0 of 4 sessions (hypothesis, F6) | ≥60% of relevant runs, CI above baseline |
| User screenshot requests per session on the report-3 replay | user sent screenshots manually | 0 |
| Report-4 replay (run editor tool with args, confirm import) | failed via computer-use | completes, semantic_ok |
| Semantic success on "fix and prove UI bug" (if verify built) | not measurable (no fixture) | ≥ best competitor, false-success ≤ it |
| False-success rate (agent says done, check fails) | unmeasured | not higher than any measured setup |
| Tokens per successful task (report-3 replay) | est. 20–30k (Nexus today) | ≤7k (estimate to be replaced by P0-09) |
| Main-thread call p50, Edit Mode, unfocused, agent session active | 100–300 ms | <20 ms if P0-01a finds a safe lever |
| Default tools/list tokens | ~4.9k (14 managers) | ≤2.5k |
| Screenshot cost to the model | base64 text (unusable) | ≤0.8k tokens, real image (768 px default) |
| Platforms with a recorded benchmark run | macOS only | macOS + Windows (before Gate B) |
| External users who tried it on their own project / report use | 0 | ≥5 / ≥3 by Gate B |
| Install signals (OpenUPM, skills.sh, GitHub traffic) | unmeasured | reported at each gate |

---------------------------------------------------------------------
## 12. RISK REGISTER
---------------------------------------------------------------------

| # | Risk | Mitigation | Early-warning trigger |
|---|---|---|---|
| R1 | Agents still don't choose Nexus | Skills published by week 3, instructions, CLI exposure; pre-registered Gate A | Gate A numbers |
| R2 | Unity ships input/UI automation/Overlay capture | Keep value in live truth + assertions + report; primitives swappable via `IUnityPrimitives` — wrap, don't fight | Pipeline changelog mentions input/simulate/overlay |
| R3 | Verify niche already occupied (uLoopMCP, akiojin, Funplay, AltTester, Unity demo F11) | P0-10b head-to-head; Gate A2 desk decision; Gate B vs best competitor; verify-lite fallback | ≥2 competitors detect the planted bug |
| R4 | Pipeline API churn | Pinned range (P0-06), N/N-1 CI (A-01), hand-written thin projection + drift test | Compile break on Pipeline bump |
| R5 | `unity eval` bypasses Nexus gates; `confirm:true` mistaken for a human gate | Honest SECURITY.md; stop selling security as headline; no write params projected to Pipeline | Any doc claiming full protection in pipeline mode |
| R6 | 3 transports, 2 people | Freeze list; new code only in Core | >20% of monthly commits touch frozen areas |
| R7 | Windows breaks capture/input (unconditional flip, F10; Python missing for shim) | L-05 before Gate B and before any public claim | No Windows run by week 6 |
| R8 | Private `m_RenderTexture` disappears | W-03 self-test + fallback | Field resolves null on a new 6000.x |
| R9 | AI agents widen scope / self-accept | Rules 2–5 in §6 | PR over cap; "ACCEPTED" written by implementer |
| R10 | Energy / focus | Gates and public releases (wk 2, 6, 9, 12) give visible checkpoints | Two weeks without user-visible change |
| R11 | Domain reload kills long operations (play entry, compile) | Principle 3; L-02 resumable job store; V-02b | Any verify/run ending `interrupted` on default settings |
| R12 | Gates self-graded or underpowered | Pre-registration, frozen external prompts, N=8–10, Wilson intervals, third-party tasks, external users | Gate criteria edited after runs |
| R13 | Single harness contention invalidates evidence | P0-08c lock + second Editor; rule §6.14 | Evidence runs overlapping in logs |
| R14 | Dotted tool names rejected/mangled by clients | Underscore public names; P0-10 naming check | Any client error on `nexus.` names |
| R15 | Prompt injection / secret leakage via scenario files, logs, context packs | Rule §6.8 (non-imported closed-step scenarios, deny-list, opaque handles); Principle 9 | Scenario or log text changing agent actions in tests |
| R16 | Wake-up has no safe public lever | P0-01a spike first; release not blocked; metrics conditional | Spike finds only machine-wide pref changes |

---------------------------------------------------------------------
## 13. WHERE THE ANALYSES DISAGREED (and how v2.1 resolves it)
---------------------------------------------------------------------

- **Timebox.** B: hard 6-week bet with exit. D: 12 weeks. → 12 weeks with hard, pre-registered Gate A (wk 4) and Gate B (wk 9; moved from wk 7 because the verify critical path is ~15 agent-days and Live truth now comes first).
- **Is input a Unity "hand"?** Lead reviewer assumed possibly; A found nothing official. Challenger 1 showed uLoopMCP already ships input and UI clicks. → Input is not a Nexus differentiator; built only if Gate A2 shows verify has room, Input System only.
- **Flagship.** v2.0: `nexus.verify`. Challenger 2: the evidence (reports 3–4, socials) asked for live Editor truth and editor-tool runs, and verify is crowded. → v2.1: Live truth first (weeks 4–6); verify conditional (Gate A2, Gate B vs best competitor). Rejected from the challenge: intercepting native dialogs (not feasible without patching; detection only) and a week-3 demo (nothing to show yet; demo at week 6).
- **Verify shape.** A: declarative scenario run Editor-side in one call. D: composite command with steps. Challenger 1: must be resumable across the play-entry reload. → A's DSL (closed step set) executed Editor-side on D's job model with a resumable runner (V-02b).
- **Pipeline as primitive provider.** v2.0: two adapters (legacy + in-process Pipeline). Challenger 1: in-process Pipeline means reflection into an experimental package; public Editor APIs suffice. → one `EditorUnityPrimitives`; Pipeline is projection only.
- **Profiler.** B: drop (Unity has it). A: marker-level triage could differentiate. → Backlog, decided by P0-10.
- **Multi-editor / runtime selector.** B: freeze and consider dropping `auto` default. D: keep semantics. → Keep; fix D-8 with port discovery in S-03.
- **Default surface size.** C: ≤8; D: 5; A: ~12 + escape hatch. Challenger 2: "only 14 of 120 visible" must not get worse. → ≤8 in `core` + `nexus_status detail:"capabilities"` + profiles + `nexus_call/list`.
- **execute_code.** Challenger 2: ship a thin `eval` forwarder. → Rejected (same Unity floor as Pipeline, adds surface, blurs the R5 boundary); documented instead.

---------------------------------------------------------------------
## 14. SOURCES
---------------------------------------------------------------------

- Unity CLI release notes — https://docs.unity.com/en-us/unity-cli/release-notes
- Unity CLI as the replacement for the in-Editor MCP server — https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli
- Unity-Technologies/skills, `unity-cli` skill — https://github.com/Unity-Technologies/skills/blob/main/skills/unity-cli/SKILL.md
- Pipeline command list (community) — https://github.com/menstood/unity-pipeline-commands-skill/blob/main/SKILL.md
- Unity CLI beta.4 (`--detach`, `unity job`; only source) — https://discussions.unity.com/t/unity-cli-1-0-0-beta-4-is-rolling-out/1733720
- Unity CLI launch coverage (agent play-tests and catches a bug) — https://gamedev.net/news/5423/ · https://runtimewire.com/article/unity-ships-a-cli-that-lets-ai-agents-operate-running-game-projects · https://unity.com/resources/unity-pipeline-cli-technical-walkthrough
- uLoopMCP / unity-cli-loop (v3.0.1: simulate-keyboard/mouse/ui, replay-input, screenshot; own IPC) — https://github.com/hatayama/unity-cli-loop · https://www.skills.sh/hatayama/unity-cli-loop/uloop-record-input
- akiojin/unity-cli (playmode-testing, input-system, ui-automation skills) — https://github.com/akiojin/unity-cli · https://www.skills.sh/akiojin/unity-cli/unity-playmode-testing
- Funplay Unity MCP (core profile) — https://glama.ai/mcp/servers/FunplayAI/funplay-unity-mcp
- AltTester CLI / AI skills — https://alttester.com/alttester-cli-built-for-the-way-ai-assisted-testing-actually-works/ · GameDriver — https://www2.gamedriver.io/blog/gamedriver-test-assistant
- "Adopt the Brain, Defer the Bridge" — https://dev.to/furic/adopt-the-brain-defer-the-bridge-unitys-agent-plugin-and-free-cli-three-weeks-on-1kei
- Claude vision token cost — https://platform.claude.com/docs/en/build-with-claude/vision
- Claude tool-name constraints — Anthropic tool-use docs on platform.claude.com (tool `name` pattern `^[a-zA-Z0-9_-]{1,64}$`)
- Cursor ignoring `tools/list_changed` mid-session (reported by Challenger 1, not independently verified) — forum.cursor.com thread 161459
- Adversarial reviews (session scratchpad): `CH1_technical.md`, `CH2_strategy.md`; judge log `JUDGE_log.md`

---------------------------------------------------------------------
## 14b. UNITY ROADMAP ALIGNMENT (to be filled after Unity research)
---------------------------------------------------------------------

Pending: Unity roadmap research.

---------------------------------------------------------------------
## 15. ADVERSARIAL REVIEW LOG (v2.0 → v2.1)
---------------------------------------------------------------------

Totals: 52 objections (CH1 technical: 32, CH2 strategy: 20). **ACCEPT 37 · PARTIAL 13 · REJECT 2.**

| id | objection (short) | verdict | reason | change applied |
|---|---|---|---|---|
| CH1-1 | F1–F3 partly unverifiable; `--detach`/`unity job` only in a forum post | PARTIAL | docs.unity.com is not fetchable here; SKILL.md confirms `[CliCommand]`/`MainThreadRequired`/eval but not `--detach` | F1/F3 marked "via search"; `--detach` a hypothesis checked in P0-10; V-07 conditional on it |
| CH1-2 | F5 understated: uLoopMCP already covers most of V-03/V-04 | ACCEPT | github.com/hatayama/unity-cli-loop v3.0.1 lists simulate-keyboard/mouse/ui, replay-input, screenshot over own IPC | F5 rewritten (+akiojin, Funplay); R3; input no longer a differentiator |
| CH1-3 | F6 is a hypothesis (retrospectives, N=4; install unconfirmed) | ACCEPT | `3.md:31` "cannot be confirmed"; report 4 used `mcp__cua_repl` | F6 relabelled hypothesis; §0 and §11 wording |
| CH1-4 | F8 missing half: Pipeline has the same 3×100 ms ticks | ACCEPT | `STABILIZATION_ACCEPTANCE_REPORT.md` §E table: Pipeline and legacy both 3 ticks / ~297 ms | F8 amended; R16 |
| CH1-5 | F9: shipped Pipeline names use underscores | ACCEPT | `Alias = "nexus_*"` in `Editor/Commands/*Command.cs:18-23`; `NexusPipelineCommands.cs:21,31,43` | F9; all public names → `nexus_*` |
| CH1-6 | F10: unconditional flip Blit → D3D likely upside down | ACCEPT | `GameViewCaptureSource.cs:52-55` | F10; L-05 note; R7 |
| CH1-7 | D-1 overstated (token file readable by same user) | ACCEPT | `MCPServer.Identity.cs:122`; Origin/Host checks block browsers | D-1 marked minor; fix kept in P0-05 |
| CH1-8 | D-2 worse: disposed event; running calls still apply | ACCEPT | `MCPServerMethods.cs:215-224`: `signal.Set()` after `using` disposal | D-2 rewritten; P0-05 adds disposal fix + E for running mutation |
| CH1-9 | D-3: cached flags stale while throttled; two readiness predicates | ACCEPT | `Sync.cs:76` vs `Status.cs:22` | D-3 amended; P0-02 "one predicate" |
| CH1-10 | D-8 imprecise; fix is port discovery | ACCEPT | `MCPServer.Networking.cs:105-113` skips bind only for foreign-owned port; selector comment "bound opportunistically" | D-8 rewritten; S-03 port discovery + 2-project test |
| CH1-11 | D-10 partly wrong (line/col are in message strings) | ACCEPT | `GroupCompileErrorsCommand.cs:61-80`: 50-entry ring buffer, substring key | D-10 rewritten |
| CH1-12 | Domain reload is the normal path for verify/compile | ACCEPT | Default Enter Play Mode reloads the domain; v1 C7 listener died on reload (`BENCH_RESULTS.md:117`) | Principle 3 rewritten; job model; L-02 resumable store; new V-02b; R11 |
| CH1-13 | P0-01 is a research spike; E criteria conflict; don't block release | ACCEPT | No public thread-safe wake API; Pipeline shows same cadence (F8) | P0-01 split into P0-01a spike + P0-01b; E scoped to active session; removed from P0-11 deps; §11 conditional |
| CH1-14 | `unity command nexus.*` naming unverified | ACCEPT | F9 + LLM tool-name charset (F13) | P0-10 naming check; G11; rule §6.10; R14 |
| CH1-15 | P0-04 maps `Submitted` to error → retries/duplicates | ACCEPT | `routes_editor.py:54` treats Submitted as legitimate test trigger | P0-04: Submitted → pending, `isError:false`; Principle 5 |
| CH1-16 | Cancel flag doesn't stop double-apply; need idempotency | ACCEPT | Follows from D-2 | `request_id` in §4, P0-04, P0-05, S-04 |
| CH1-17 | Legacy Input Manager fallback not implementable; focus issues | ACCEPT | `UnityEngine.Input` has no injection API; Input System background behaviour is a ProjectSettings concern | V-03 Input System only + `LEGACY_INPUT_UNSUPPORTED`; P0-10 input focus check |
| CH1-18 | V-04/V-06 are 2–3 tasks each; Gate B wk 7 unrealistic | ACCEPT | Critical path ≈15 agent-days | V-04a/b, V-06a/b; Gate B on V-06a at week 9 |
| CH1-19 | Two primitive adapters contradictory; in-process Pipeline = private API | ACCEPT | Public Editor APIs cover play/compile/refresh/tests/console | One `EditorUnityPrimitives`; Pipeline projection only; V-07 reduced to 1d conditional |
| CH1-20 | Python generator cannot reflect C# | ACCEPT | Batchmode generation needs Unity in CI | S-02: hand-written wrappers + EditMode drift test (2d) |
| CH1-21 | `tools/list_changed` unreliable; deferral makes token budget moot | PARTIAL | Startup profiles are safer (accepted); Cursor thread not independently verified, and not every client defers schemas, so the ≤2.5k budget stays | S-03: startup profiles, best-effort `list_changed`, per-client matrix E |
| CH1-22 | Gates underpowered, contaminated, unfair; no agent-run harness | ACCEPT | ≈18 runs/arm → ±23 pp CI; ratio vs ~0 baseline meaningless | Pre-registration, ≥60% absolute + Wilson bounds, N=8–10, S3 pinned to 1.6.0, S1 equivalent snippet, frozen Gate B tasks incl. uLoop-native task; new P0-08b harness; P0-09 deps |
| CH1-23 | Single harness Editor vs 4 PRs in flight | PARTIAL | Contention is real; a full farm is over-scoped for 2 reviewers | P0-08c: lock + 2nd fixture Editor; rule §6.14; R13 |
| CH1-24 | Security regressions from composites (7 points) | ACCEPT | `confirm:true` is agent-set (`Utils.cs:287-291`); scenario/handle/context/ProjectSettings risks are real | D-11; rule §6.8 extended; §4 opaque handles, no write params to Pipeline; `NexusScenarios~/`; V-08 restore+label; Principle 9; R15 |
| CH1-25 | Image cost target wrong for 1024×1024 | ACCEPT | w×h/750 (F12) | Default long side 768 px; S-07 E for 16:9 and 1:1; §11 |
| CH1-26 | Epochs must persist in SessionState; wait can't span reload | ACCEPT | Statics reset on reload | P0-02 text |
| CH1-27 | Out-of-range Pipeline can't be faked in EditMode | ACCEPT | Package version is resolved by UPM | P0-06 E uses a second scratch project + prerelease semantics test |
| CH1-28 | `nexus` shim needs Python (Windows) | PARTIAL | True, but shim is already optional | Principle 6: `unity command` primary, shim optional; L-05 checks Python |
| CH1-29 | S-05 `no_change` <50 ms impossible before wake-up | ACCEPT | Needs a main-thread tick | S-05 E conditional on P0-01b |
| CH1-30 | Reverse index fails on Binary serialization | ACCEPT | YAML parsing requires Force Text | C-02 scoped to Force Text + error code |
| CH1-31 | `nexus_context` is a retrieval research problem | ACCEPT | Ranking from a task string is unproven | C-01 labelled spike; stays out of `core` unless ≥30% |
| CH1-32 | Verify DSL schema may eat the budget | ACCEPT | Unmeasured | §5.1 "measure before promising"; V-06a E measures schema |
| CH2-1 | Flagship is not what evidence asked; "Live truth" wedge first | PARTIAL | Reports 3 (`3.md:130,208-231`), 4 (`4.md:48-65`) and socials support live truth + editor-tool runs; verify kept but conditional. Native dialog interception rejected (not possible without patching) | New Phase 2 Live truth (L-03 `nexus_snapshot`, L-04 `nexus_run` with modal detection); verify → Phase 3 conditional; G4; §0, §2, §5.1 |
| CH2-2 | Drop "hands vs eyes" metaphor | ACCEPT | Unity ships screenshot/hierarchy/component reads (F3); F11 | §2 concrete claim |
| CH2-3 | Alternatives A1–A6; outreach week 1; named gate-failure destinations | PARTIAL | Outreach is cheap and parallel; A1 as main strategy not supported by evidence | P0-12, G9; §9 destinations A1–A4 |
| CH2-4 | No target persona | ACCEPT | Plan never named a user | §2.1, G10; P0-09 tasks from real sessions + third-party project |
| CH2-5 | Add `nexus.capabilities` | PARTIAL | Need is real ("14 of 120"); a separate tool costs a core slot | `nexus_status detail:"capabilities"`; profiles in `instructions` |
| CH2-6 | Ship a thin `nexus.eval` forwarder | REJECT | Nexus floor (6000.0) = Pipeline floor, so any user can add Pipeline; a forwarder adds surface and blurs the R5 boundary | Only documentation (§5.1, G3, R-03) |
| CH2-7 | Scene View capture in `nexus_look` | ACCEPT | Friend's gap list; cheap on Capture V2 | S-07 (3d) Scene View framed on object |
| CH2-8 | Hot-reload awareness in `nexus_compile` | PARTIAL | Verdict can mislead in hot-reload setups; full integration out of scope | S-05: detect + warn only |
| CH2-9 | Verify niche occupied; widen competitor spike; ≥2 detect → not flagship | ACCEPT | Verified uLoop v3.0.1 and akiojin skills on GitHub; F11 Unity demo | P0-10b; Gate A2; R3; verify-lite in §9 |
| CH2-10 | Gate B compares against a strawman | ACCEPT | Official skills lack input (F4) | Gate B arm = best competitor from P0-10b |
| CH2-11 | Gate A cannot fail cleanly; external prompts/tasks/users | PARTIAL | Pre-registration, thresholds, frozen external prompts, third-party tasks accepted; ≥5 external users by week 4 is unrealistic (skill published week 3) | Gate A1 rewrite; external users reported at A, hard criterion at Gate B |
| CH2-12 | Gate B "OR" makes it advisory | ACCEPT | A token win alone must not pass a verify product | Gate B: AND + effect sizes, N≥10 |
| CH2-13 | Compress public loop; Windows before Gate B; drop C-03/C-05/A-03 | PARTIAL | Accepted: OpenUPM wk 2, skill listing wk 3, testers wk 3, Windows wk 5–6, drops to backlog. Rejected: live-truth demo at week 3 (not built until wk 6) | P0-11, S-01, S-08, L-05, L-07; backlog |
| CH2-14 | Keep `nexus doctor` | ACCEPT | First-run friction (C §1.2) | S-01 + `nexus_status detail:"doctor"` |
| CH2-15 | Name "Nexus" is unsearchable | PARTIAL | Brand kept (package id, history); discoverability fixed at skill level | §2 descriptive skill id; S-01 search test |
| CH2-16 | COMPARISON must include Ivan, Coplay, Funplay, akiojin | ACCEPT | These were the questions asked (Socials: James, Abdullah) | R-03 six competitors |
| CH2-17 | No usage measurement | PARTIAL | Public counters accepted; in-product stats export deferred (scope, privacy) | S-08, §11; export in backlog |
| CH2-18 | Monetization: studio policy layer, not "Nexus CI" | PARTIAL | "Nexus CI" crowded (AltTester/GameDriver); policy layer depends on a Pipeline hook nobody has confirmed | G8 rewritten; P0-10 checks hook; backlog item |
| CH2-19 | Demo should replay real failures | ACCEPT | Reports 3–4 are real and more credible than a planted toggle | §2 headline; L-07, R-04 |
| CH2-20 | Funplay already did a small core profile (plan presents it as novel) | REJECT | The plan makes no novelty claim for profiles; they are chosen for tokens, not differentiation | none |
