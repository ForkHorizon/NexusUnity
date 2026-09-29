# NEXUS UNITY — MASTER REWORK PLAN
Version 1.0 · 2026-09-01 · Owner: Kiryl / ForkHorizon
Horizon: 2-3 months · Goal: beat funplay/coplay/ivan + official Unity Pipeline on
speed, functionality, and out-of-the-box UX.

Source evidence:
- /Users/daliys/Daliys/NexusFeature/results/BENCH_RESULTS.md (benchmark 2026-09-01)
- /Users/daliys/Daliys/NexusFeature/BENCHMARK_PROTOCOL.md
- Package source: /Users/daliys/Daliys/UnityProjects/UnityTestForNexus/Assets/NexusUnity
- Project issue tracker: .projectmem/issues/ (open: #0005, #0007-#0018) + GitHub #56 #78 #99
- Official Pipeline docs: https://docs.unity3d.com/Packages/com.unity.pipeline@0.3/manual/index.html

=====================================================================
HOW TO USE THIS FILE (для Kiryl)
=====================================================================
- Задачи идут ПО ПОРЯДКУ КРИТИЧНОСТИ: Phase 0 — критические баги, Phase 1 —
  скорость, Phase 2 — архитектура, Phase 3 — фичи, Phase 4 — UX/дистрибуция,
  Phase 5 — мелочи. Внутри фаз тоже по убыванию критичности.
- ОДНА ЗАДАЧА = ОДИН АГЕНТ = ОДИН PR. Не давать агенту больше одной задачи за раз.
- Задачи с тегом [DECISION] — агент готовит эксперимент/прототип/отчёт с
  рекомендацией, но ФИНАЛЬНОЕ РЕШЕНИЕ принимаем мы вдвоём после его отчёта.
  Такие задачи не заканчиваются PR в main — только прототипом/документом.
- Перед передачей задачи агенту скопируй ему: (1) GLOBAL RULES целиком,
  (2) саму задачу. Больше ничего не нужно — задача самодостаточна.
- Зависимости указаны в поле Deps. Не запускай задачу раньше её зависимостей.

=====================================================================
GLOBAL RULES (вставлять каждому агенту целиком)
=====================================================================
1. Repo: https://github.com/ForkHorizon/NexusUnity (MIT). Work on a feature branch
   `rework/<task-id>` from latest main. One PR per task. NEVER merge your own PR.
2. Process: strict TDD (RED → GREEN → REFACTOR). Write failing tests first for every
   behavior change. Tests live in Tests~/Editor/ (Unity EditMode) and bridge tests
   alongside the Python bridge. All tests must pass before opening the PR.
3. Honesty: if something cannot be verified locally (no Unity license, no live
   editor), say so in the PR. Never claim unverified results. Failed benchmarks
   are recorded, never smoothed.
4. Security invariants — MUST be preserved in every task, they are the product's
   differentiator. If your task conflicts with one, STOP and report instead:
   - Loopback-only binding; per-session auth token (X-Nexus-Unity-Token);
     token file Library/NexusUnityAuthToken.txt never logged, never committed.
   - Path sandbox: all file ops confined to project root; symlink resolution
     before boundary checks (see MCPServerMethods.Utils.cs ValidatePath).
   - confirm:true gates for: .cs writes (attach_script/write_file/write_files_batch/
     unity_write_and_compile), delete_asset, PlayerPrefs "all".
   - Type allowlists for component/ScriptableObject creation (no editor/internal
     system assemblies).
   - batch_execute cap (50) and no nesting. ProjectSettings/ + Packages/ paths
     are write-protected.
5. Public surface compatibility: the 121 raw JSON-RPC methods are a documented
   public API. Renames/removals require a deprecation shim for one minor version
   + CHANGELOG entry + DOCUMENTATION.MD update. New tools require docs + tests.
6. Unity floor is 6000.0 unless task D4 changes it. Code must compile on 6000.0.
   If you need a newer API, gate it with #if UNITY_X_OR_NEWER version defines.
7. Style: follow existing code conventions (partial classes MCPServerMethods.*,
   JObject-based JSON, Editor coroutines for main-thread dispatch). Run the repo
   linter (`dotnet run --project tools~/NexusQualityGate` or .code-linter.json
   config) before opening PR. Zero new linter errors.
8. PR description must contain: What/Why (2-3 sentences), How (key decisions),
   Test evidence (test names + counts + how run), Benchmark evidence IF the task
   has perf acceptance (use bench/ harnesses from the metrics project), Breaking
   changes (or "none"), Checklist: docs updated / changelog updated / tests green.
9. Do not touch: .projectmem/, .github/workflows/ (unless the task says so),
   CI secrets, LICENSE.
10. If you are blocked >2 hours on one approach, write down what you tried in the
    PR/issue and stop — do not silently change the task scope.
11. Each new Task has to be a new branch, before creating a new branch do pull from developer branch.
12. DO NOT Merge your branch to the developer (The other human will do it)

=====================================================================
CURRENT STATE — FACTS (from benchmark 2026-09-01, N=10, warm p50)
=====================================================================
Nexus raw:  cheap read 0.6ms · object read 100-199ms · search 199ms ·
  create+destroy 300ms · screenshot 0% OK (!) · run_tests TIMEOUT (!) ·
  QA cycle FAIL (!) · C7 raw run killed by domain reload (!)
Nexus bridge: C7 compile 8.5s · C10 20.5s · C12 fails at UI click
Ivan (fastest MCP): reads 2.2-2.3ms · create+destroy 16ms
Funplay: reads 125ms · native ops via execute_code 4.6-4.7s · C12 QA full 1.4s
Coplay: reads 501-799ms · disk-script compile 13.4s · C10 20.1s · C12 partial
Token cost of tools/list: Nexus 4,913 (14 tools) · funplay 7,067 (34) ·
  Coplay 28,955 (48) · Ivan 36,035 (38)
Known open defects (live-confirmed or tracked): Game View screenshot broken on
  macOS (#0007, GH #56/#78), listener dies on domain reload (#0011), run_tests
  timeouts (#0002/#0003 legacy), bridge 401 after restart in some projects
  (#0010), wait(compilation) 20s stall (#0012), editor_idle false-ready
  (#0013), dump_scene_graph id mismatch with invoke_method (#0014),
  search_manager regex confusion (#0015), scene graph unbounded (#0016),
  log API gaps (#0017), refresh_assets side effects (#0018).

Official Unity Pipeline (com.unity.pipeline 0.3.1-exp.1): HTTP server in Editor,
  bearer auth, commands stable DURING compiles, Roslyn in-memory hot reload,
  runtime player connection. Lacks: input simulation, UI automation, QA loop,
  multi-agent coordination, semantic tools, our security depth. Exp-only,
  newest-Unity-only. STRATEGY: integrate as optional execution backend (D2/T22),
  do not compete head-on, do not depend on it.

=====================================================================
TARGET METRICS (definition of "we won"; all warm p50 unless noted)
=====================================================================
| Metric | Now | Target | Beats |
|---|---|---|---|
| Cheap read (status/state) | 0.6ms | ≤1ms | everyone |
| Object read / search | 100-199ms | ≤15ms | Ivan parity, beats all |
| Scene graph depth 3 | 150-195ms | ≤40ms | all |
| create+destroy | 300ms | ≤50ms | all |
| Screenshot Game View | 0% ok | ≥99% ok, ≤250ms | funplay(499) coplay(387) |
| QA cycle C12 | FAIL | 100% ok, ≤1.5s | funplay(1.4s) parity |
| run_tests small suite | timeout | accepted ≤500ms, result ≤ suite time | coplay(20s) |
| tools/list | 14 tools / 4.9k tok | 32-40 tools / ≤10k tok | all (sweet spot) |
| Fresh install → first successful agent call | ~30min human | ≤5min, ≤3 clicks | everyone |
| Editor restart survival | 401 bug (#0010) | 100% reconnect | all |
| Domain reload survival (C7 raw) | run dies | run survives, ≤1 retry | coplay/funplay bridge |

=====================================================================
PHASE 0 — STOP THE BLEEDING (critical defects, weeks 1-2)
=====================================================================

---------------------------------------------------------------------
T01 · Fix Game View screenshot capture (P0, est 1-2 days)
---------------------------------------------------------------------
Deps: none · Files: Editor/MCPServerMethods.HighValue.Screenshots.cs,
Editor/UIVerification.cs, scripts/agent-tooling-smoke.py
Context: benchmark C8 = 0% success (10/10 failures). Competitors: 124-499ms.
This single defect blocks the entire QA-loop positioning. Issues #0007, GH #56/#78:
capture returns PartialSuccess without image; screencapture exits 1 on macOS.
Goal: capture_game_view_screenshot and capture_inspector_screenshot return a
valid PNG (base64) ≥99% of runs on macOS (Windows/Linux paths compile-safe).
Requirements:
1. Reproduce first: write an EditMode test + a live smoke that captures and
   validates PNG magic bytes and non-trivial size (>5KB for a default scene).
2. Root-cause the screencapture failure (permissions? window focus? path?
   timing before frame rendered?). Fix the Unity-side capture path
   (ScreenCapture / Camera.Render + ReadPixels is preferred over shelling out
   to `screencapture`; keep OS capture only as fallback).
3. Add retry-once-with-frame-wait logic; wait for end-of-frame before read.
4. Return structured {success, message, data:{width,height,format,image_base64}}
   and include capture duration ms.
Acceptance: new tests green; live smoke: 20 consecutive captures, ≥19 valid;
benchmark C8 rerun attached to PR (cold, p50, ok%).
Out of scope: Scene View screenshots (T24), video capture, image diffing.

---------------------------------------------------------------------
T02 · Survive domain reload: raw listener + bridge reconnect (P0, est 3-5 days)
---------------------------------------------------------------------
Deps: none · Files: Editor/MCPServer.cs, MCPServer.Networking.cs, MCPServer.Http.cs,
MCPServer.Port.cs, Editor/nexus_bridge/_transport.py, routes_editor.py
Context: benchmark C7 raw run DIED after 4 samples (listener lost on compile).
Bridge survives but shows 8.5s stall; #0011 documents transient Connection
refused during reload; #0012 wait(compilation) 20s stall; #0013 false-ready.
Goal: an in-flight benchmark survives a scripted .cs write + compile with zero
harness retries; reconnect after reload ≤2s.
Requirements:
1. Server socket + port + token must persist across domain reloads (static
   serialized state or Library/ file; port descriptor file like Pipeline does).
2. Bridge: exponential backoff reconnect (100ms→2s), explicit RELOADING state
   surfaced in get_server_status, queued requests replay after recovery.
3. wait_for_editor_idle / is_editor_idle must not report ready while compile
   or import in progress (fix #0013 false-ready; registry from #0013 note).
4. unity_wait(compilation) must not trigger redundant refresh when initialize
   already succeeded (#0012).
Acceptance: EditMode tests for state persistence; live test script: start
harness → write .cs via bridge → poll status through reload → resume calls,
all logged in PR. Benchmark C7 reruns without harness-side retry.
Out of scope: broker process (that's D3/T15 territory — do NOT build a daemon here).

---------------------------------------------------------------------
T03 · Fix run_tests + test results pipeline (P0, est 2-3 days)
---------------------------------------------------------------------
Deps: none · Files: Editor/MCPServerMethods.Editor.cs, TestResults.cs,
nexus_bridge/routing.py
Context: benchmark C10 raw = TIMEOUT (never got result); bridge got result in
20.5s. Coplay does the same job in 20s, funplay core lacks it entirely — this
is a winnable surface. Legacy #0002/#0003 fixed the Submitted contract; the
timeout is in result retrieval.
Goal: run_tests_wait on a 5-test EditMode suite: accepted ≤500ms, final result
≤ suite execution time + 2s.
Requirements:
1. Root-cause the raw timeout (TestRunnerApi callback wiring? polling gap?
   thread marshalling?). Fix with tests (fake result provider).
2. Bridge run_tests_wait polls both legacy and current ack shapes (keep #0003 fix).
3. Structured result: total/passed/failed/skipped + per-failure message truncation.
Acceptance: tests green; live benchmark C10 rerun attached (both raw and bridge).

---------------------------------------------------------------------
T04 · Green QA cycle C12 end-to-end (P0, est 2-4 days)
---------------------------------------------------------------------
Deps: T01 (screenshots), T02 (reload survive) · Files:
Editor/MCPServerMethods.Editor.cs, Input.cs, HighValue.Screenshots.cs, logs
Context: C12 failed at UI-click step; funplay completes in 1.4s — our target niche.
Goal: scripted sequence toggle_play_mode → simulate_mouse/click_object_in_game →
capture_game_view_screenshot → read_logs → toggle_play_mode completes 10/10.
Requirements:
1. Fix whatever failed at click step in the benchmark (rerun bench_nexus_bridge
   C12 from bench/ harness first, capture the exact error).
2. play mode transitions keep readiness probes busy (1.5.0 behavior — verify
   still true after T02 changes).
3. Add a composite raw method `run_qa_cycle` (enter→act→capture→logs→exit,
   params: actions[], maxDurationSec) returning per-step timings + artifacts.
   This becomes the flagship tool later (T25).
Acceptance: new EditMode tests for run_qa_cycle orchestration; live C12 bench
10/10 with per-step timings in PR.

---------------------------------------------------------------------
T05 · Lock in the benchmark as CI regression (P0, est 1-2 days)
---------------------------------------------------------------------
Deps: T01-T04 merged (or guarded by allowlisted failures) · Files: new
scripts/perf_gate.py, .github/workflows/perf.yml (you MAY create this one),
bench harnesses copied from metrics project
Context: perf regressions caused this mess (0.6ms fast-path vs 300ms writes on
the same server). We need numbers on every PR forever.
Goal: CI job runs a headless-editor benchmark (or self-hosted runner) on a
fixed micro-scene, compares against budgets table above, fails if exceeded >25%.
Requirements:
1. Port bench_nexus.py + a C12 mini scenario into scripts/perf_gate.py
   (token via env, Unity batch mode acceptable if reliable).
2. Emit JSON artifact + markdown summary comment on PR.
3. Budgets encoded in one place with the table above.
Acceptance: workflow runs green on a no-op branch; intentionally slows one
method in a scratch branch → workflow fails (prove it in PR description).

---------------------------------------------------------------------
T06 · Kill the 401-after-restart bug class (P0, est 1-2 days)
---------------------------------------------------------------------
Deps: none · Files: Editor/MCPServer.Identity.cs, MCPServer.Http.cs,
nexus_bridge/_transport.py, NexusMcpConfigGenerator.cs
Context: #0010 (Puzzle project 401), #166 fixed persistence but live bridge
still hit 401 in the metrics project until re-deploy. Token lifecycle is fragile.
Goal: token survives editor restarts AND bridge restarts; zero 401s in a
restart-matrix test.
Requirements:
1. Restart matrix test: editor restart / bridge restart / both / token file
   deleted / port changed → each ends with successful authenticated call.
2. Config generator writes the CURRENT token on every deploy; bridge reads
   token lazily (not cached at import time).
3. get_server_status gains `auth: ok|regenerated` field for debugging.
Acceptance: matrix test in Tests~/Editor (mock fs) + live run documented.

=====================================================================
PHASE 1 — SPEED (weeks 2-4, parallelizable with Phase 0 tail)
=====================================================================

---------------------------------------------------------------------
T07 · Main-thread dispatch: find and remove the 100-300ms tax (P1, est 3-5 days)
---------------------------------------------------------------------
Deps: none (but lands after T02 to avoid conflicts) · Files:
Editor/MCPServer.cs, MCPServer.Networking.cs, MCPServerMethods.Core.cs,
MCPServerMethods.Utils.cs
Context: fast-path get_server_status = 0.6ms but find_objects = 199ms,
create_primitive = 300ms, while Ivan does the same class of work in 2-16ms.
Something in the dispatch path (polling interval? EditorApplication.update
scheduling? lock contention? per-call setup like type resolution or path
validation re-scanning?) adds ~100-300ms.
Goal: object read/search ≤15ms, create+destroy ≤50ms warm (see budgets).
Requirements:
1. Instrument first: add opt-in per-call timing breakdown (queue wait,
   dispatch, execution, serialization) reported in get_tool_usage_stats.
2. Profile a real session (Unity Profiler on the harness run); identify the
   top 3 cost centers; fix them (likely candidates: update-loop polling
   interval instead of event-driven signalling; ValidatePath doing filesystem
   walks per call; regex construction; JObject deep copies).
3. Event-driven dispatch: main-thread pump signaled by AutoResetEvent, not
   time-sliced polling.
4. Cache hot validators (project root path resolved once; symlink targets
   cached with invalidation on AssetDatabase refresh).
Acceptance: timing breakdown tool + before/after benchmark table in PR
(methods: find_objects, get_game_object, create_primitive, dump_scene_graph,
batch_execute 10). Security invariants untouched (path sandbox still correct —
add a regression test that symlink escape is still caught after caching).

---------------------------------------------------------------------
T08 · Response/payload diet (P1, est 1-2 days)
---------------------------------------------------------------------
Deps: none · Files: MCPServerMethods.Serialization*.cs, Snapshot.cs
Context: Nexus payloads are compact already, but schema-per-tool is fat
(351 tokens/tool avg vs funplay 208). C9 batch payload 16.6KB for 10 reads.
Goal: -30% median payload on heavy reads; schemas ≤250 tokens/tool avg.
Requirements:
1. dump_scene_graph/compact_scene_snapshot: field inclusion levels
   (minimal/standard/full), truncate long strings, cap arrays with markers.
2. Batch response compaction: shared dedup of repeated object shells.
3. Schema audit: remove redundant param descriptions, collapse enums.
Acceptance: payload size tests (golden JSON fixtures) + token count table
before/after; agent-visible behavior documented in DOCUMENTATION.MD.

---------------------------------------------------------------------
T09 · Bridge transport efficiency (P1, est 2-3 days)
---------------------------------------------------------------------
Deps: T02 · Files: nexus_bridge/_transport.py, client.py, routing.py
Context: bridge adds reconnect stalls and C7 8.5s; bridge/raw paths diverged
(benchmark §bridge comparison showed materially different code paths — that's
a correctness smell too).
Requirements:
1. Single shared request path: bridge routes map 1:1 to documented raw methods;
   no bridge-side reimplementation of server logic (routes_* should be thin).
2. Persistent HTTP connection (keep-alive/session), request pipelining where
   safe, no per-call token file re-read (T06 dependency).
3. Bridge adds ≤5ms overhead per call vs raw (measure and document).
Acceptance: overhead table (raw vs bridge for 6 operations) in PR; contract
tests that every bridge tool's params/returns match the raw method schema.

=====================================================================
PHASE 2 — ARCHITECTURE (weeks 3-6; contains the big DECISIONS)
=====================================================================

---------------------------------------------------------------------
D1 · [DECISION] Bridge language & process model (est 3-5 days)
---------------------------------------------------------------------
Deps: none (do early — gates T10-T12, T15) · Deliverable: REPORT + PROTOTYPE, no main PR
Question: should the MCP bridge stay Python, move to a dotnet single-file
executable, or be embedded in the Editor process speaking MCP directly?
Evidence to gather:
1. What do the top MCP clients actually require for stdio servers? (spawn
   command, env, cwd; Claude Code /mcp, Cursor, Codex config formats — we
   have installer code for 10 clients already, inventory their constraints).
2. Prototype A: minimal in-Editor MCP-over-stdio adapter (Editor writes to a
   spawned child's stdin? measure feasibility: can Unity host a stdio loop
   without blocking? funplay does in-Editor HTTP MCP — copy that pattern).
3. Prototype B: dotnet bridge (publish single-file, no Python) — effort
   estimate, install UX (does user need dotnet runtime? self-contained size?).
4. Failure modes today: Python 3 version issues (1.4.x changelog), uv/uvx
   friction (coplay), PATH problems — list every support-class issue Python
   has caused us from changelog audit.
Output: docs/adr/ADR-001-bridge-process-model.md with recommendation,
migration cost, risk table. WE decide.

---------------------------------------------------------------------
D2 · [DECISION] execute_code: own Roslyn vs Pipeline eval vs hybrid (est 3-4 days)
---------------------------------------------------------------------
Deps: none (gates T21) · Deliverable: REPORT + spike, no main PR
Question: implement in-memory C# execution ourselves (funplay-style Roslyn
compile, ~4.6s/call) or delegate to com.unity.pipeline eval when present?
Evidence to gather:
1. Spike: Roslyn compile+run in-memory via Unity's bundled compiler (funplay
   does "csc first, in-memory flow") — measure OUR cold/warm latency; can we
   beat 4.6s with assembly caching (compile once, invoke many)?
2. Spike: is com.unity.pipeline 0.3.1-exp.1 installable side-by-side with
   Nexus in a 6000.4 project? Does its eval survive OUR security needs (we
   must wrap it in confirm-gates + audit logging anyway)?
3. Security design either way: allowlist namespaces/types (extend existing
   FindType allowlist), confirm:true for state-mutating snippets, structured
   {logs, created, modified, destroyed, returnValue} return (funplay parity),
   undo registration.
Output: docs/adr/ADR-002-execute-code.md with latency table + recommendation.
---------------------------------------------------------------------
D3 · [DECISION] External broker process for reload survival (est 2-3 days)
---------------------------------------------------------------------
Deps: T02 (which fixes in-process survival first) · Deliverable: REPORT
Question: after T02, is anything still lost across domain reloads that
justifies a tiny always-on broker (funplay "Experimental Broker Mode": local
process keeps the port, proxies to Editor, reconnects after reload)?
Evidence: post-T02 benchmark C7/C12 with and without heavy scripts importing;
measure if any client-visible disconnect remains. If T02 reaches zero-loss,
recommend NO broker (complexity not justified) and close this.
Output: docs/adr/ADR-003-broker.md.
---------------------------------------------------------------------
D4 · [DECISION] Unity version floor (est 0.5 day)
---------------------------------------------------------------------
Deliverable: REPORT. Today 6000.0. Pipeline needs 6000.5-ish; Unity AI needs
6000.3. Survey: what do 6000.0/6000.1/6000.2 users lose if we raise to 6000.2?
Check Asset Store/OpenUPM norms for "current minus 2". Recommend floor.
Output: docs/adr/ADR-004-unity-floor.md.
---------------------------------------------------------------------
D5 · [DECISION] Repo/package layout (est 1 day)
---------------------------------------------------------------------
Question: stay Assets/NexusUnity-style dev repo, or restructure to a proper
UPM package layout (package at repo root, Samples~, Tests~, Documentation~)
so OpenUPM + git-URL + embedded dev all work cleanly and .meta hygiene is
machine-checkable? Audit current pain (missing folder metas happened: 1.4.1).
Output: docs/adr/ADR-005-package-layout.md + migration checklist (not executed).

---------------------------------------------------------------------
T10 · Tool surface redesign: 14 → 32-40 MCP tools (P1, est 4-6 days)
---------------------------------------------------------------------
Deps: D1 (bridge model), T08 (schema diet) · Files: nexus_bridge/schemas_*.py,
routes_*.py, docs
Context: 121 raw methods but MCP sees 14 managers — the #1 discovery failure
(friend's audit + benchmark token table). funplay core=34/7k tokens is the
proven sweet spot; Coplay/Ivan prove 29-36k tokens is poison.
Goal: 32-40 bridge tools, total tools/list ≤10k tokens, every tool mapped to
exactly one raw method family, per-action required params.
Requirements:
1. Inventory all 121 raw methods; group into tools by verb+domain (target
   list: ~36 — draft in the PR, e.g. split unity_scene_manager into
   scene_open/scene_save/scene_list; expose screenshot, input, play mode,
   logs (already), tests, prefs, search as first-class).
2. Per-action JSON schemas with required[] and tight descriptions; add 3-6
   usage examples into resources/read static docs (bridge already supports
   resources — expose a tool-catalog resource the agent can read on demand).
3. Naming: verb_noun, no abbreviations, stable across versions (deprecate old
   manager names for one minor version, keep aliases).
4. Tool descriptions must include WHEN to use (agents choose tools by
   description — this is prompt engineering, spend real effort here).
5. Optional lazy mode: env/config flag exposing core 12 + a `list_all_tools`
   resource pattern for token-starved clients (document; default full).
Acceptance: token count test (≤10k chars×4 target on tools/list); contract
tests per new tool; migration table old→new in CHANGELOG; DOCUMENTATION.MD
rewritten tool catalog section.
---------------------------------------------------------------------
T11 · Response contract unification + instanceId chaining (P1, est 2-3 days)
---------------------------------------------------------------------
Deps: T10 · Files: all MCPServerMethods.*, bridge routes
Context: funplay's {success,message,data} + stable instanceId chaining beats
us on agent ergonomics; our shapes vary per method (benchmark noted payload
path divergence).
Requirements:
1. Every tool returns {success, message, data} + optional {warnings[], timings_ms}.
2. Every object-bearing response includes stable `instance_id` (we already emit
   them; make them accepted EVERYWHERE as input: fix #0014 where dump ids
   aren't invoke_method-compatible).
3. Deprecation shims where shapes changed.
Acceptance: contract test suite (open-source API contract tests extended);
docs table of response envelope.
---------------------------------------------------------------------
T12 · Zero-config out-of-box experience (P2, est 4-6 days) — FLAGSHIP UX
---------------------------------------------------------------------
Deps: D1, T06, T02 · Files: MCPServer.Port.cs, MCPCliInstaller*.cs,
NexusMcpConfigGenerator*.cs, MCPServerWindow*.cs
Context: benchmark setup notes: funplay = "clearest setup path"; Coplay =
port collision + confirmations + uvx; Ivan = registry repair + cloud→local +
binary download. User demands: "подключил и работает с коробки".
Goal: fresh project + any of 10 clients → first successful tool call in
≤5 minutes and ≤3 human clicks.
Requirements:
1. Auto port: derive free port per project (hash of project path, collision
   fallback scan), persist pin in Library descriptor (T02 groundwork).
2. First-run wizard in the Nexus window: detect installed clients → one
   "Connect everything" button → writes/updates all configs with correct
   token/URL → shows per-client verification ping results inline.
3. Status page: server state, port, token health, connected clients last
   seen, quick copy buttons. All in the existing Window (no new windows).
4. Failure self-diagnosis: common breakages (stale config, editor restarted,
   port taken, Python missing if D1 keeps Python) → plain-language fix card.
5. Editor restart → configs stay valid (T06), server auto-starts if it was
   running (session flag).
Acceptance: scripted fresh-clone test (CI or documented manual): install →
wizard → Claude Code /mcp shows green → call a tool; timing screenshots;
updated README quickstart ≤10 lines.
---------------------------------------------------------------------
T13 · Deterministic readiness & lifecycle API (P2, est 2 days)
---------------------------------------------------------------------
Deps: T02, T07 · Files: MCPServerMethods.Sync.cs, Status.cs, bridge
Requirements:
1. get_editor_state returns a single readiness enum (booting/ready/
   compiling/importing/playmode_transition/reloading) + blocking operation
   name; editor_idle waits on the enum, not on ad-hoc flags (#0013).
2. Every long op returns a job id + poll method where >1s (bake, tests,
   playmode transitions) — pattern already half-exists (test jobs).
3. Document time budgets per operation class.
Acceptance: tests for enum transitions; docs table.

=====================================================================
PHASE 3 — FEATURES (weeks 5-9; order = customer value)
=====================================================================

---------------------------------------------------------------------
T21 · execute_code (P2, est 5-8 days) — THE flagship feature
---------------------------------------------------------------------
Deps: D2 decision, T11 (contract) · Files: new Editor/MCPServerMethods.Eval.cs
(or Pipeline adapter), Security: Utils allowlist extension
Goal: safe in-memory C# execution: agent sends snippet → structured result,
no .cs on disk, no domain reload, warm repeat calls ≤1s (beat funplay 4.6s).
Requirements (own-Roslyn path; adjust per D2):
1. Compile with Unity-bundled Roslyn; cache compiled assembly per
   (normalized snippet hash) — warm invoke must skip compile.
2. Template support: IFunplay-style context object (ours: NexusEvalContext
   with RegisterCreation/Modification/Destroy → Undo grouping, Log* →
   structured logs, ReturnValue).
3. Security: namespace/type allowlist reusing FindType policy + explicit
   deny of File.IO/Process/Networking unless confirm:true AND a settings
   toggle; snippet audit log (hash + result status) to Library/; timeout
   guard; response truncation caps.
4. Return {logs, created, modified, destroyed, returnValue, timings_ms}.
5. Bridge tool eval_execute + docs page with 5 recipes (spawn 30 cubes,
   read all materials, tweak physics, run menu item, custom validation).
Acceptance: unit tests (compile fail, runtime fail, timeout, allowlist deny,
undo integration, cache hit path); live benchmark: cold ≤4.5s, warm ≤1s,
20-call mixed suite; security tests red-team style (path escape attempt,
Process spawn attempt).
---------------------------------------------------------------------
T22 · Pipeline adapter (optional backend) (P2, est 3-4 days)
---------------------------------------------------------------------
Deps: D2, D4 · Files: new Editor/PipelineAdapter.cs, bridge routes
Goal: if com.unity.pipeline present, Nexus can delegate selected commands
(commands stable during compiles = our reload pain disappears for those) and
expose pipeline_status tool; graceful absence otherwise.
Requirements:
1. Detect package via PackageManager API; feature-flag in settings (off by
   default until Pipeline ≥1.0? — flag default decided by D2 report).
2. Delegation map doc: which Nexus ops route to Pipeline (script writes,
   bake, asset import) vs stay native (input, UI, QA, screenshots).
3. Pass through our auth surface; never bypass confirm gates.
Acceptance: integration test with Pipeline installed in scratch project (or
documented manual test if license-blocked); delegation map in docs.
---------------------------------------------------------------------
T23 · Input simulation: keyboard + UI raycast (P2, est 3-4 days)
---------------------------------------------------------------------
Deps: T04 · Files: Editor/MCPServerMethods.Input.cs (+ new Keyboard part),
package.json (inputsystem dependency exists)
Context: mouse/touch exist; keyboard missing (friend's gap list); UAX adds
simulate_input to Pipeline proving demand; QA loop needs it.
Requirements:
1. simulate_keyboard: key down/up, text typing (InputSystem + legacy
   UnityEngine.Input paths via version defines), modifiers.
2. UI raycast tool: ScreenPointToRay hit info (object, component, uv, distance)
   for Game View; works with our click_object_in_game.
3. Composite helpers: press_hotkey, type_into_focused.
Acceptance: playmode tests with EventSystem dummy scene; docs recipes.
---------------------------------------------------------------------
T24 · Scene View & Inspector screenshots + capture hardening (P2, est 2-3 days)
---------------------------------------------------------------------
Deps: T01 · Files: HighValue.Screenshots.cs
Requirements:
1. capture_scene_view (Camera.SceneView callback + ReadPixels), focus control
   (focus_scene_view exists — chain it), optional grid/gizmos toggles.
2. capture_inspector(object) — InspectorWindow reflection repaint + capture
   (existing partial impl; make reliable).
3. Multi-monitor/DPI correctness on macOS Retina.
Acceptance: tests + benchmark-style 20-run stability matrix.
---------------------------------------------------------------------
T25 · Productized QA workflow tools (P2, est 3-5 days)
---------------------------------------------------------------------
Deps: T04, T23, T24, T11 · Files: new MCPServerMethods.QA.cs
Goal: turn the C12 sequence into first-class tools — the niche nobody closes.
Requirements:
1. run_qa_cycle (from T04) extended: scripted action list (wait, key, mouse,
   click_object, screenshot, assert_log_contains, assert_object_exists,
   set_timescale), returns step-by-step report + artifacts bundle.
2. qa_diff_screenshots: pixel-diff two captures (tolerance, changed-region
   bbox) — enables "did the button visually react".
3. qa_report: markdown summary the agent can paste to the user.
4. Artifacts stored under Library/NexusQA/ with rotation cap.
Acceptance: integration test scenario (button toggles cube color: agent-style
sequence passes; breaking the app fails the assert step); docs with 3 recipes.
---------------------------------------------------------------------
T26 · Basic profiler & performance reads (P3, est 2-3 days)
---------------------------------------------------------------------
Deps: T11 · Files: new MCPServerMethods.Profiler.cs
Requirements:
1. profiler_capture(durationSec): ProfilerEnabled + frame data summary (ms by
   category: render/script/physics/gc), top markers by self-time (via
   ProfilerDriver/ProfilerSample API), memory snapshot (total/mono/gfx).
2. Cheap live stats tool: current fps, frame time p50/p95 over window.
3. No external packages; degrade gracefully where API differs across versions.
Acceptance: tests with mocked profiler data + one live capture attached to PR.
---------------------------------------------------------------------
T27 · Agent skills & self-describing surface (P3, est 2-3 days)
---------------------------------------------------------------------
Deps: T10 · Files: new Skills~/, installer extension
Context: Ivan generates skills; UAX ships a SKILL.md teaching CLI grammar —
agents that know our grammar waste fewer tokens. Cheap, high leverage.
Requirements:
1. Author nexus-unity/SKILL.md: tool selection guide, common pitfalls
   (confirm gates, readiness enum, instance_id chaining), 10 recipes.
2. Installer copies to .claude/skills/ + .codex/skills/ + AGENTS.md block
   (UAX-style markers, byte-preserving outside markers) — reuse their
   pattern legally (MIT, attribute).
3. resources/read already serves docs — cross-link.
Acceptance: installer tests (idempotent, marker preservation); manual: fresh
Claude Code session completes a recipe using only the skill.
---------------------------------------------------------------------
T28 · Close audit backlog #0014-#0018 (P3, est 3-4 days)
---------------------------------------------------------------------
Deps: T11 (#0014 needs id unification) · Files: per issue
One PR per issue, strict TDD, in this order: #0014 (id mismatch), #0016
(scene graph bounds — partially in T08), #0015 (search contract docs),
#0013-remainder (operation registry — if not fully done in T13), #0017 (log
correlation/cursors), #0018 (refresh_assets affected-asset reporting).
Acceptance: per-issue tests + projectmem record_fix.

=====================================================================
PHASE 4 — DISTRIBUTION & TRUST (weeks 8-12)
=====================================================================

---------------------------------------------------------------------
T31 · Documentation overhaul (P2, est 3-4 days)
Deps: T10, T11, T21
Requirements:
1. DOCUMENTATION.MD restructure: 5-minute quickstart, tool catalog (auto-
   generated from schemas — write the generator), security model page,
   comparison table (Nexus vs funplay vs Coplay vs Ivan vs Pipeline:
   speed/coverage/tokens/security/setup), recipes (10+), troubleshooting
   (from T12 diagnosis cards).
2. docs/ generated on CI or pre-commit; never hand-edit generated parts.
Acceptance: generator test; every tool has doc entry (test); dead links zero.
---------------------------------------------------------------------
T32 · OpenUPM + release engineering (P2, est 2 days)
Deps: D5 (layout), T31
Requirements:
1. OpenUPM registration (com.forkhorizon.nexus.unity), scoped registry docs.
2. Release checklist doc: version bump, changelog, tag, git-URL pin note,
   migration notes for renamed tools (T10).
3. CHANGELOG hygiene pass (Unreleased section discipline).
Acceptance: openupm add works in scratch project (manual verify, document).
---------------------------------------------------------------------
T33 · Comparison tables & positioning refresh (P2, est 1 day)
Deps: benchmark re-run after Phase 3
Re-run the full benchmark suite vs funplay/coplay/ivan (same protocol),
publish honest tables in README (link raw results). Positioning: "fast,
secure, QA-complete agent layer for Unity; Pipeline-compatible".
Acceptance: tables generated from results JSON (no hand-typed numbers).
---------------------------------------------------------------------
T34 · Onboarding telemetry-free smoke (P3, est 1-2 days)
Deps: T12
Scripted fresh-machine-simulation (clean user, clean project): git clone →
open → wizard → client connect → first tool call, timed; publish as
"5-minute setup" guarantee with the script in repo (.github or scripts/) so
anyone can verify. Include a 60-second asciinema/GIF for README.
---------------------------------------------------------------------
T35 · Multi-project / multi-editor support (P3, est 2-3 days)
Deps: T12 (per-project ports)
Requirements: two editors side-by-side (funplay's documented scenario):
port derivation, client config generation picks the RIGHT project (project
path embedded in window title/status), tools list project label.
Acceptance: scripted two-project test.
---------------------------------------------------------------------
T36 · Plugin API for custom tools (P3, est 3-5 days)
Deps: T10, T13
Attribute-based discovery ([NexusTool]) so users add their own commands
(funplay parity, our twist: they flow through the SAME security gates and
docs generator). Includes: registration API, schema-from-attributes, example
plugin package, docs.
Acceptance: example plugin test; docs; security gates enforced test.

=====================================================================
PHASE 5 — POLISH BACKLOG (fill-ins, order freely)
=====================================================================
T41 get_tool_usage_stats: per-tool p50/p95 from server side (feeds T33 tables).
T42 PlayerPrefs: typed read API parity audit (Windows registry/Linux XML paths
    from 1.6.0 — add live tests where OS available).
T43 semantic_find: promote from raw to bridge tool with docs (unique feature —
    benchmark didn't even test it; add C5b semantic scenario to bench).
T44 generate_mermaid_diagram + scene_delta: bridge exposure + recipes (unique
    features, zero marketing so far).
T45 Editor time control: step_frame batching (step_n), timescale set/get in
    playmode (QA needs).
T46 Console log streaming: subscribe resource (bridge push on new error while
    agent works — polling today).
T47 Localization pass on window UI labels (RU/EN) — low priority.
T48 Demo asset refresh: record new 98s demo AFTER Phase 3 (scripted via
    run_qa_cycle itself — dogfooding proof).
T49 SECURITY.md expand: threat model for eval (post-T21), responsible
    disclosure contact, auth design rationale.
T50 Changelog/dead-code sweep: remove deprecated manager aliases from T10
    (the one-minor-version deadline elapsing).

=====================================================================
DEPENDENCY GRAPH (edges: prerequisite → dependent)
=====================================================================
T01 → T04, T24
T02 → T04(gently), T09, T12, T13, D3
T03 → (none; feeds T25 test helper)
T05 → guards everything perf-related
T06 → T12
T07 → T13
T08 → T10
T09 → T10
D1 → T10, T12 (bridge model), T21 (eval transport choice)
D2 → T21, T22
D3 → optional T15-broker (not scheduled unless D3 says yes)
D4 → T22, T32
D5 → T32
T10 → T11, T27, T31, T36
T11 → T21, T25, T26, T28
T12 → T34, T35
T13 → T36
T21 → T31 (docs), T25 (eval in recipes)
T23 → T25
T24 → T25
T25 → T33 (QA benchmark), T48
T28 → (independent per-issue)

SUGGESTED EXECUTION ORDER (one agent at a time, ~2.5 months):
W1: T01 ∥ T03 ∥ T06 (small, independent) → T02
W2: T02 cont. ∥ T05, T07 start; D1, D2, D4, D5 reports (parallel, cheap)
W3: T07 finish, T08, T09; decide D1-D5 WITH me
W4: T10 (surface) — biggest single-piece risk, give it a full week
W5: T11, T12 start, T13
W6: T12 finish; T21 start (flagship)
W7: T21 finish; T23, T24
W8: T25, T22, T26
W9: T27, T28, re-benchmark (T33 inputs)
W10: T31, T32, T33
W11: T34, T35, T36
W12: Phase 5 fill-ins + buffer (realistically 20% of tasks slip; the buffer is real)

=====================================================================
RISK REGISTER
=====================================================================
R1 T07 dispatch fix conflicts with T02 rewrite → land T02 first, rebase discipline.
R2 T10 surface rename fragments users → aliases + deprecation window + docs.
R3 T21 eval is a security hole → red-team tests are acceptance criteria, gates
   default-on, SECURITY.md updated; NEVER ship eval without them.
R4 Pipeline goes 1.0 mid-plan and shifts ground → T22 stays optional backend;
   D4 quarterly re-check (calendar note).
R5 One-agent-per-task context loss → every task lists exact files + evidence
   docs; GLOBAL RULES travel with each handoff.
R6 Motivation/energy over 3 months → Phase 0 wins are visible in week 1
   (screenshot fix alone unblocks demos); celebrate benchmarks in issues.
R7 Benchmark noise (cold/warm, JIT) → all targets warm p50, N=10, fixed scene,
   CI gate T5 enforces.

=====================================================================
APPENDIX A — FILE MAP (orientation for agents)
=====================================================================
Editor/MCPServer*.cs — HTTP/WS listener, identity/auth, ports, discovery, logs
Editor/MCPServerMethods.*.cs — 121 raw methods by domain (Asset, Component,
  Context, Delta, Editor, Hierarchy, HighValue(.Screenshots/.Semantic), Input,
  ObjectIds, PlayerPrefs, Prefab, Reflection, Scene, Search, Serialization,
  Snapshot, Status, Sync, TestResults, Timeline, ToolUsage, Tools(.Extended),
  TypeResolution, UI, UISnapshot, Utils)
Editor/nexus_bridge/ — Python MCP bridge (transport, routing, routes_*,
  schemas_*)
Editor/MCPCliInstaller*.cs — 10 client integrations (Claude Code/Desktop,
  Codex, Cursor, Gemini, Antigravity, VS Code/Cline/Roo, Windsurf, generic)
Editor/NexusMcpConfigGenerator*.cs — config writing, bridge health
Editor/MCPServerWindow*.cs — the Unity window (Server/Integrations/Resources/
  Settings tabs)
Editor/ProjectAuditorFinal*.cs — optional auditor integration
Runtime/ — MCPRuntimeLogger, ForceDefaultAttribute (runtime relay)
Tests~/Editor/ — 16 test files (path security, contracts, installers...)
tools~/NexusQualityGate/ — Roslyn lint + Ollama reviewers (keep!)
bench+results (metrics project) — perf evidence, ported by T05
APPENDIX B — BENCHMARK SNAPSHOT (2026-09-01, warm p50 ms / ok%)
                 Nexus   funplay  coplay  ivan
obj read         100-199   125     799     2.3
scene read       150-195   125     501     2.2
create+destroy     300    4739*   1299     16
compile (C7)     ~99/8511  4624*  13403    n/a
screenshot (C8)   0%        499     387    n/a
tests (C10)      timeout    n/a   20120   84(0%)
QA cycle (C12)    FAIL     1409   3606**   n/a
tools/list tokens 4913     7067   28955   36035
* via execute_code  ** partial (no mouse)
APPENDIX C — UNIQUE NEXUS ASSETS TO PROTECT & PROMOTE
Security model (auth, sandbox, gates, allowlists) · NexusQualityGate + Ollama
reviewers · semantic_find · scene_delta · generate_mermaid_diagram ·
symbol_index · get_editor_timeline · UI Toolkit automation suite ·
step_frame · 10-client installer breadth · MIT + zero-cloud.
