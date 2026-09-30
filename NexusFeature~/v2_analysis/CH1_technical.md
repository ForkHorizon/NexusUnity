# CH1 — Technical challenge of MASTER_PLAN_V2 (Challenger 1 of 2)

Scope: technical correctness and feasibility. Repo checked at `46a00d7` (the plan cites `ef13f8e`; the cited lines still match). Read-only review.

## 1. Fact and defect verification

| Item | Verdict | Evidence / note |
|---|---|---|
| F1–F3 (Unity CLI / Pipeline) | UNVERIFIABLE here (docs.unity.com is blocked), partly CONFIRMED | The `unity-cli` SKILL.md (GitHub) confirms `unity command eval`, `[CliCommand]`, and `MainThreadRequired` (default true). `set_autotick` is confirmed only by the community list ("if the Editor is unfocused/minimized it may stop ticking"). `--detach`/`unity job` appear in neither SKILL.md nor `integration-advanced.md`; the only source is a forum post. |
| F4 | UNVERIFIABLE, but the conclusion is at risk | Official material has no input simulation. See F5. |
| F5 | CONFIRMED and **understated** | github.com/hatayama/unity-cli-loop v3.0.1 ships `simulate-keyboard`, `simulate-mouse-input`, `simulate-mouse-ui` (UI element clicks), `replay-input`, and `screenshot`, over its own socket/named-pipe IPC. It is not built on Pipeline. That covers most of V-03 and V-04 today. |
| F6 | OVERSTATED as a "fact" | The four reports are agents' self-written retrospectives, not transcripts. `3.md:31` says: "Whether Nexus Unity was installed or available in the Unity project cannot be confirmed." `4.md` ran in a computer-use client. Treat this as a hypothesis with N=4. |
| F7 | CONFIRMED | `bench_nexus_bridge_c10.json` has `Submitted`/`Timeout` ×11. `bench_nexus_bridge_c8.json` has `PartialSuccess`, "screencapture failed". Funplay C12 has "hit no target". C7 raw p50 is 98.9 ms with n=4, and the listener died on reload (`BENCH_RESULTS.md:117`). |
| F8 | CONFIRMED, with a missing half | `MCPServer.cs:145` subscribes `HandleMainThreadQueue` to `EditorApplication.update`. `bench_nexus_bridge_fast.json` has `isPlaying: true` ×11, and the raw runs have false. What the plan leaves out: `STABILIZATION_ACCEPTANCE_REPORT.md:141-144` shows **persistent Pipeline has the same 3 ticks × ~100 ms**, so Pipeline mode does not escape the problem. `AppNapBypass.cs` already disables macOS App Nap, so the remaining ~100 ms is Unity's own background throttling. |
| F9 | CONFIRMED | `Editor/Pipeline/NexusPipelineCommands.cs` has 3 wrappers. **They are registered as `nexus_project_map` etc., with underscores** (`Commands/*Command.cs: Alias = "nexus_*"`), not `nexus.*`. |
| F10 | CONFIRMED | `GameViewCaptureSource.cs:14-16` uses reflection on `m_RenderTexture`. `:52-55` has an unconditional `Blit(..., (1,-1), (0,1))` flip, so D3D will likely be upside down. |
| D-1 | CONFIRMED but OVERSTATED | `MCPServer.Http.cs:23` (empty Origin allowed) and `:132` (shutdown is a probe method) are real. However, any same-user local process can read `Library/NexusUnityAuthToken.txt` (`MCPServer.Identity.cs:122`). Browsers are blocked by the Origin and Host loopback checks. The real impact is nuisance-level. Keep the fix, but make it minor. |
| D-2 | CONFIRMED, and worse than stated | `MCPServerMethods.cs:215-227`: on timeout, the lambda still runs later. It then calls `signal.Set()` on a `ManualResetEventSlim` already disposed by `using`, which throws `ObjectDisposedException` inside `finally` on the main thread. A cancel flag only fixes calls that are still *queued*. A call already *running* past 60 s (for example a large import) still applies, and the client sees "Timeout". |
| D-3 | CONFIRMED | `MCPServerMethods.Sync.cs:76` checks only `IsCompilingCached`/`IsUpdatingCached`. Those flags refresh only on a main-thread tick (`MCPServer.cs:79`, called from `HandleMainThreadQueue`), so they are also *stale* while throttled. `Status.cs:22` already has a richer predicate (play transition, `_scriptRefreshBusyUntilUtc`), so there are two inconsistent readiness predicates today. |
| D-4 | CONFIRMED | `routes_editor.py:73-97`. |
| D-5 | CONFIRMED | `UnityMCP.Editor.Pipeline.asmdef:22`. |
| D-6 | CONFIRMED | `nexus_unity_bridge.py:164-166`. `prompts/list` falls through to -32601. Results are text-only (`:150-153`). |
| D-7 | CONFIRMED | There is no `isError` anywhere in the bridge. Also note that the bridge treats `Submitted` as success (`routes_editor.py:54`). |
| D-8 | OVERSTATED / imprecise | The legacy bind is skipped only when the port **is owned by another project** and Pipeline is eligible (`MCPServer.Networking.cs:105-113`). The selector comment says legacy is "still bound opportunistically". The fix is port discovery, not "bind when bridge configured". |
| D-9 | CONFIRMED | `CreateExceptionResponse` always attaches `StackTrace` (`MCPServerMethods.cs` around line 255). |
| D-10 | PARTLY WRONG | `GroupCompileErrorsCommand.cs:65-80`: line and column are kept inside the raw message strings, just not structured. The real defects are the 50-entry window, reliance on Nexus's own log ring buffer (it misses errors logged before server init or reload), and the substring file key. |

## 2. Critical and major issues

### C1 (critical) — Domain reload is treated as a failure mode. For verify and compile it is the normal path. (§3 P3, §4 job model, V-02, V-06, S-05)
Entering Play Mode with domain reload enabled tears down every C# static: the legacy HTTP listener (`beforeAssemblyReload += Cleanup`), any in-flight `[CliCommand]` Task, and the scenario interpreter. A successful compile does the same. v1 C7 shows the listener dying mid-run. V-02's acceptance ("reports `interrupted`") would make **every verify on a default-settings project end as interrupted**. Principle 3 ("one call … never agent-driven polling") cannot hold for any operation that crosses a reload.
**Change:** rewrite P3 as "one logical call: the Editor persists a resumable job; the client shim (bridge/`nexus` CLI/skill) reconnects and long-polls `job_wait` once." Add a task **V-02b "Resumable scenario runner"** (step cursor in SessionState plus `[InitializeOnLoad]` resume, and an idempotent step contract) before V-06. V-02's E should be: "verify survives the play-entry reload and completes 20/20". Keep `interrupted` only for Editor restart or crash.

### C2 (critical) — P0-01 is a research spike priced as a 2–3 day fix, and its E criteria contradict each other. (P0-01, §11)
There is no public thread-safe way to wake the Editor loop. `EditorApplication.QueuePlayerLoopUpdate` is a main-thread API, which is the chicken-and-egg problem. Posting to `UnitySynchronizationContext` only enqueues work that runs on the same throttled tick. The acceptance report shows Pipeline suffers the same cadence. The only known levers are Pipeline's `set_autotick` (mechanism undocumented) and changing interaction or throttle preferences, which are machine-wide EditorPrefs and raise idle CPU. That conflicts with "idle CPU unchanged".
**Change:** split P0-01 into (a) a 1-day spike that measures `set_autotick`, the Interaction Mode settings, and App Nap state with unfocused, minimized, and other-Space variants, and finds out what `set_autotick` actually calls; and (b) an implementation task sized from the spike. Change the E to "p50 <20 ms *while an agent session is active*; idle CPU unchanged *when no session is active*". **Remove P0-01 from P0-11's dependencies**, because the release must not block on research.

### C3 (critical) — The name `unity command nexus.*` is unverified, and the shipped code already avoids dots. (§0, §3 P6, §5, S-01, S-02)
The existing Pipeline commands are `nexus_project_map`, `nexus_group_compile_errors`, and `nexus_capture_game_view`. The dotted string is only the internal `Id`. Every Pipeline built-in uses snake_case. The analyses themselves use `nexus_verify` and `unity_verify` (A, C §1.3, D line 76). Tool-name rules in major LLM APIs (Anthropic, OpenAI) are commonly `[a-zA-Z0-9_-]`. Dotted MCP names projected through `unity mcp` or the bridge risk rejection or mangling (not verified here).
**Change:** add a P0-10 check: "register `nexus.verify` via `[CliCommand]`, then call it from `unity command`, `unity mcp` in Claude Code, and Codex". Until that passes, all public names in skills, docs and headlines use `nexus_verify`, and dotted ids stay internal.

### M1 (major) — P0-04 maps `Submitted` to `isError:true`. (P0-04)
`Submitted` is a legitimate asynchronous accept, for example a test run that has been triggered (`MCPServerMethods.Editor.cs:52`, `routes_editor.py:54`). Flagging it as an error teaches agents to retry, which means duplicate test runs and duplicate writes. That is the exact D-2 class of bug.
**Change:** `Submitted` → `isError:false` with `{state:"pending", job, next:"job_wait"}`. `Timeout` is `isError:true` with `retryable:false` and `outcome:"unknown"` for mutations.

### M2 (major) — P0-05's cancel flag does not stop double-apply. (P0-05)
**Change:** add a request idempotency key (`request_id`) that the server remembers for about 5 minutes. A mutation that times out *while executing* returns `outcome_unknown` plus `request_id`. A retry with the same key returns the stored result. Also fix the disposed-event bug. E: "timed-out mutation not applied" is only testable for the queued case, so add "retry of a running mutation is not applied twice".

### M3 (major) — V-03 "legacy Input Manager fallback" is not implementable. (V-03)
`UnityEngine.Input.GetKey` reads native state, and there is no public injection API. Input System injection (virtual devices plus `QueueStateEvent`) works. However, the Input System's `backgroundBehavior` and Game View focus settings can disable devices **when the Editor is unfocused, which is the normal agent condition**. Changing those settings writes `ProjectSettings`, which conflicts with security invariant #8.
**Change:** scope V-03 to the Input System only, and return `LEGACY_INPUT_UNSUPPORTED`. Add a 0.5-day spike in P0-10: "inject a key into an unfocused Editor in Play Mode, with and without Game View focus". Note that uLoopMCP already has keyboard support, so the value must come from assertions and reports, not from input (Principle 2).

### M4 (major) — V-04 and V-06 are each 2–3 tasks. (V-04, V-06)
V-04 covers UGUI via EventSystem, UI Toolkit runtime panels (`panel.Pick` plus synthesized pointer events, which route differently under the Input System), both input backends, and 10/10 on P9. That does not fit in 3 days. V-06 covers a DSL, per-frame predicates driven by a throttled Editor loop (depends on C2), capture-on-fail, a report, reload resumption (C1), and scenario files.
**Change:** split V-04 into V-04a (UGUI) and V-04b (UI Toolkit, optional before Gate B). Split V-06 into V-06a (runner and DSL core with 5 step types) and V-06b (reporting, capture-on-fail, saved scenarios). Reserve the 3 recipes for Gate B. The critical path V-01 → V-02 → V-02b → V-04a/V-05 → V-06a is about 15 agent-days plus review, so Gate B at the end of week 7 is unrealistic unless Phase 1 slips nothing. Move Gate B to week 8, or define Gate B on the V-06a prototype only.

### M5 (major) — `IUnityPrimitives` in-process Pipeline adapter: a contradiction plus private-API risk. (§4, V-07)
§4 says `PipelineUnityPrimitives` calls Unity "in-process (no HTTP to itself)". V-07 says "via `unity command --detach` + `unity job wait`", which from inside the Editor *is* a call to itself. Pipeline's built-in implementations are not a documented C# API, so calling them in-process means reflection into an experimental package, which is a second `m_RenderTexture`. Meanwhile play/stop, recompile, refresh, tests and console all have **public Editor APIs** (`EditorApplication.EnterPlaymode`, `CompilationPipeline.RequestScriptCompilation`, `AssetDatabase.Refresh`, `TestRunnerApi`, `Application.logMessageReceivedThreaded`). Both adapters would call the same thing.
**Change:** use one `EditorUnityPrimitives` built on public APIs. Pipeline is only a *projection* (L1), not a primitive provider. Delete V-07, or reduce it to "verify reachable via `unity command` with detach, parity table". This saves about 3 days and removes an API-churn risk.

### M6 (major) — S-02 generator design is infeasible as written. (S-02)
"Descriptor parameters by reflection → `scripts/gen-pipeline-commands.py`": Python cannot reflect C#, so it would have to regex-parse C#, which is fragile. Running Unity batchmode to generate adds a Unity-in-CI dependency. That exists only on the self-hosted macOS runner.
**Change:** hand-write the thin `[CliCommand]` wrappers (about 10 lines each) and add an EditMode **drift test** that reflects over the `CliArg` attributes and compares them to descriptors. That gives the same guarantee without a code generator. Make "zero Python edits" (S-03) depend on `list_tools` metadata, not generation.

### M7 (major) — Deferred loading via `tools/list_changed` is unreliable, and partly moot. (§4, S-03)
Cursor ignores `tools/list_changed` mid-session (forum.cursor.com thread 161459). Reports exist of the same problem in Claude Code. Claude Code already defers MCP schemas by default (the plan's own P0-09 assumes this), so a ≤2.5k `tools/list` budget buys little there. The binding constraint is **names, descriptions and `instructions`**.
**Change:** make profiles a *config/startup* choice (an env var or argument to the bridge), with `load_profile` plus `list_changed` as a best-effort extra. Make the S-03 E a per-client matrix (Claude Code, Codex, Cursor) instead of a token count alone.

### M8 (major) — Measurement: the gates are underpowered and definitionally weak. (P0-09, Gate A, Gate B, §11)
- Gate A says "≥2× the S3 baseline". If the baseline is about 0, the ratio is meaningless. With N=3 per cell and about 3 "relevant" tasks, each arm has about 18 relevant runs, which gives a 95% CI of about ±23 pp. With N=3, "no increase in false-success" is undetectable.
- **Change:** pre-register absolute thresholds, for example "Nexus called for the Unity-state step in ≥60% of relevant runs, and the lower Wilson 90% bound is above S3's upper bound". Use N=8–10 on the 2–3 relevant tasks rather than N=3 on 5. Define "relevant" per task before running.
- Contamination: S3 "Nexus MCP today" must be pinned to 1.6.0 and run **before** P0-04 and P0-07 merge. P0-09 depends on the P0-08 fixture, but no dependency is declared.
- Fairness: arm S4 adds an AGENTS.md snippet, which is a prompt intervention. Give S1 an equivalent "use Unity CLI skills" snippet, or the comparison measures instruction-following, not tool value. Gate B tasks are authored by the Nexus team, which is home-field bias. Freeze the task list and oracles before V-06 starts, and include at least one task uLoopMCP is designed for.
- Missing task: **agent-run harness** (`claude -p`/`codex exec` runner, fixture reset, transcript token parsing, automated oracle). P0-08 is only protocol-level, and P0-09 cannot build that harness *and* run about 60 sessions in 3 days. Estimate: 90 runs (3 arms × 2 agents × 5 tasks × 3) at 10–25 min each is 15–37 h of serial Editor time. The money cost is modest (roughly $100–400 at API rates, or subscription rate limits). Time and harness contention dominate.

### M9 (major) — Single harness Editor versus "≤4 PRs in flight, agent-executed". (§6 rule 14, §7 capacity)
All Unity-facing E criteria run on one harness project and Editor on one Mac (AGENTS.md). Concurrent agents cause domain reloads, port and token collisions, and play-mode interference.
**Change:** add a task "harness farm": N worktree copies of the fixture, each with its own Editor, port and Library, plus a lock or schedule. Otherwise the 4-PR parallelism is fictional.

### M10 (major) — Security regressions from composites are not covered. (§3, §6 rule 8, S-05, V-06, S-06, C-01)
1. **`[CliCommand]` projection bypasses the Nexus token.** Anything write-capable projected to Pipeline (for example `nexus.compile` with writes) inherits Pipeline's auth model, not Nexus's. State this, or keep write parameters legacy-only until Pipeline's model is documented.
2. **`nexus.compile` writes:** any `.cs` write is Editor code execution (`[InitializeOnLoad]`). Also block `csc.rsp`, `*.rsp`, `.dll`, `.asmdef` or `.asmref` with `precompiledReferences`, and `Packages/` paths. "Confirm gate" means `confirm:true`, which the agent sets itself (`MCPServerMethods.Utils.cs:287-291`). Stop calling it a gate in SECURITY.md.
3. **Scenario files** in `Assets/NexusScenarios/` are repo content executed by agents. That creates a supply-chain and prompt-injection path (malicious repo → verify runs `invoke`/menu steps). Put them in a non-imported folder (`NexusScenarios~/` or the project root) to avoid `.meta` churn and import. Restrict the DSL to a closed step set with no reflection setters or menu execution unless the `full` profile is active.
4. **Handles:** `cap://`, `log://`, `snap://`, and job ids must be server-issued opaque ids (`[a-z0-9]{8,}`), never paths. Add a size and retention cap on `Library/Nexus/jobs` to prevent disk fill.
5. **`nexus.context`** returns project text to the model. Honour `.gitignore` plus a deny-list (`*.env`, keystores, `StreamingAssets/**/secrets`).
6. **Untrusted text:** console logs and UI strings returned by verify or diagnose are third-party content. Mark them as data in the skill.
7. **V-08** toggles `EditorSettings.enterPlayModeOptions`, which is a ProjectSettings write that conflicts with invariant #8. A static-state lint cannot be complete (third-party DLLs, static events), so it risks false-pass verification. Keep V-08 optional, restore the setting after the run, and label the results "no-reload mode".

## 3. Minor issues
- **S-07 / §11 image cost:** Claude charges about w×h/750 tokens, so 1024×576 ≈ 790 and 1024×1024 ≈ 1.4k. The 0.4–0.8k target only holds for 16:9 at ≤1024 wide or ≤768 px. Default `max_dimension` to 768, or restate the target.
- **P0-02:** epochs must persist in SessionState, because static counters reset on reload. The wait cannot span a reload in one HTTP request. Say that the bridge re-polls.
- **P0-06:** testing "faked out-of-range" needs a second project, since a package version cannot be faked in EditMode. Unity prerelease range semantics (`-exp`) need an explicit test.
- **Shell-first `nexus` shim = the Python bridge CLI.** That requires Python on Windows, where it is often missing, which conflicts with Principle 6. Note this in W-01, or make the shim `unity command` only.
- **S-05** "no_change <50 ms" is impossible until C2 is solved, because it needs a main-thread tick.
- **C-02:** a reverse index over YAML fails on Binary serialization mode. `AssetDatabase.GetDependencies` is forward-only and slow on 100k-asset projects. Scope it to Force Text and declare that limit.
- **C-01:** relevance ranking from a task string is a retrieval research problem. The "≥30% or drop" rule is right, but budget it as a spike.
- **Schema budget:** the verify DSL schema alone may take about 1k of the 2.5k. Measure it before promising ≤8 tools within ≤2.5k.

## 4. Missing tasks (add to §7)
1. Naming spike (C3), in P0-10.
2. Unfocused-Editor wake-up spike (C2a) and Input System focus spike (M3), both in P0-10.
3. Agent-run harness (M8), a separate 3-day task before P0-09.
4. Harness farm (M9).
5. V-02b resumable runner (C1).
6. Idempotency keys (M2).

## 5. What the plan got right (keep)
- Measure first and void v1 numbers. Keep `semantic_ok` as the headline and record tick and `isPlaying` per sample. The F7 and F8 diagnoses are correct.
- The cheap, real fixes: D-4 (fixed sleeps), D-7 (`isError`), D-9 (stack traces behind `NEXUS_DEBUG`), D-5 (version pinning), D-6 (`instructions` and image blocks).
- The kill list: no own `execute_code`, transport, broker, or 10-client installers. Freeze list plus deprecation policy (hide → deprecate → remove only in a major).
- Hard gates with a legitimate fallback (§9). Honest security repositioning around `unity eval`.
- Scope caps, the stop rule, and "you don't accept your own work".
- The W-03 capture self-test and fallback, and the Windows run before any claims (the unconditional Blit flip makes this necessary).
