# Agent B: Skeptic Report. What to cut, what to avoid, what could kill Nexus

Date: 2026-09-30. Read-only review of `/home/user/NexusUnity` (branch `docs/nexus-feature-notes`, HEAD `ef13f8e`). Inputs: SHARED_CONTEXT, MASTER_PLAN v1, ImageCapturePlan, STABILIZATION_ACCEPTANCE_REPORT, CHANGELOG, the 4 AI chat reports, a code spot-check and two web searches.

## 0. Bottom line

The most important fact in the repo isn't in the code. It's in `AI Chat reports Before all changes/1-4.md`: in four real sessions where Nexus was installed, agents made **2 Nexus calls in total**. They used `rg`/`sed`/`apply_patch`/`dotnet build`, and in report 4 a computer-use tool (`mcp__cua_repl`) to drive the Unity UI. Nexus was configured and working, and agents still went around it. Speed, tool count, transports and security depth only matter once an agent actually picks the tool, and none of the recent work was measured against that.

Second fact: the "hybrid" migration (M1–M4) landed as PR #201 "rework/T01". `git diff 329990e^1 329990e` shows **164 files, +12,439 / −328**. The original T01 was "fix screenshot, est 1–2 days". What reaches Unity Pipeline today is **3 commands** (`Editor/Pipeline/NexusPipelineCommands.cs:21,31,43`). Most of the migration effort went into the transport layer, and the part users would see is small.

Third fact: Unity now gives away the "hands": `eval`, play/stop, tests, build, console, hierarchy, screenshots, packages and `unity mcp`, plus 31 official skills. Third parties are also going after the verification niche. `hatayama/unity-cli-loop` (uLoopMCP) already ships CLI skills for PlayMode input **record/replay** and simulated mouse/keyboard, distributed through skills.sh. The "nexus verify" niche is contested, not open.

## 1. Kill list

Verdicts: KEEP (do as scoped), SHRINK (much smaller version), DROP (don't do), DONE (already delivered or made moot).

### MASTER_PLAN v1

| Task | Verdict | One-line reason |
|---|---|---|
| T01 Game View capture | DONE | Capture V2 works, Overlay proven on Metal. Stop touching it except to validate Windows. |
| T02 Domain-reload survival | SHRINK | Pipeline owns lifecycle. Keep only correct readiness reporting (see T13). No listener persistence work. |
| T03 run_tests pipeline | SHRINK | Unity CLI has `run_tests`/`test_status`. Keep only a compact failure summarizer on top. |
| T04 Green C12 QA cycle | KEEP | This is the seed of the only differentiated workflow, but rebuild the benchmark scene first: it has no EventSystem and C12 was invalid for everyone. |
| T05 Perf CI gate | DROP | A headless or unfocused editor gives ~100 ms quantized noise, so a 25% budget gate will flap. Replace with a task-success eval (see §5). |
| T06 401-after-restart | SHRINK | #166 fixed most of it. One restart-matrix test, no new features. |
| T07 Dispatch 100–300 ms tax | SHRINK | This is a wake-up/throttling problem, not an architecture one (see §6.3). Half a day of experiments, not 3–5 days. |
| T08 Payload diet | KEEP | Tokens are one of the owner's three axes and cheap to win. Field levels and truncation on the few context tools. |
| T09 Bridge transport efficiency | DROP | Optimizing a Python bridge that should shrink. Bridge overhead is not the bottleneck. |
| T10 14→32–40 tools | DROP | This is the tool-count race (§2). Unity already exposes ~151 commands. Ship 5–8 intent-level commands plus a skill instead. |
| T11 Response envelope + id chaining | SHRINK | Apply only to new canonical commands. Don't retrofit 117 legacy methods. |
| T12 Zero-config wizard for 10 clients | DROP | `unity skill install <client>` and `unity mcp` now own onboarding. Support Claude Code + Codex via skills only. |
| T13 Readiness enum | KEEP | Readiness is a verification primitive, and a false-ready path still exists (§6.4). |
| D1 Bridge language | DROP | Decided by events: Unity CLI/`unity mcp` is the transport. Don't build a dotnet bridge. |
| D2 Own Roslyn vs Pipeline eval | DROP | Decided: `unity eval` is free and official. |
| D3 Broker process | DROP | Pipeline solved multi-editor and reload. A broker is pure maintenance. |
| D4 Unity floor | SHRINK | A one-hour decision: new work targets Unity 6000 + Pipeline, legacy frozen for older versions. |
| D5 Package layout | DONE | The package already lives at repo root with `Tests/`, `Research~`. |
| T21 execute_code | DROP | Competing with Unity's own `eval` and Funplay. Zero differentiation, maximum security liability. |
| T22 Pipeline adapter | DONE | M2/M3 delivered it. Freeze it. |
| T23 Keyboard input + UI raycast | KEEP | This is a core verification primitive, but check whether uLoopMCP or Unity close it first. Build it as `[CliCommand]`, not HTTP. |
| T24 Scene/Inspector screenshots | SHRINK | Scene View capture only if cheap. Drop Inspector capture hardening. |
| T25 Productized QA tools | KEEP | This is the flagship candidate, but narrow it to one scripted loop + assertion report. No pixel-diff research. |
| T26 Profiler | DROP | Unity CLI has profiler commands. |
| T27 Skills | KEEP (top priority) | Skills are how agents choose tools now. It's cheap and targets the non-adoption finding directly. |
| T28 Audit backlog #0014–#0018 | SHRINK | Fix only what new commands depend on (#0014 id mismatch maybe). Freeze the rest with legacy. |
| T31 Doc overhaul + generator | SHRINK | Docs only for the new small surface. No doc generator. |
| T32 OpenUPM | KEEP | Cheap distribution win. |
| T33 Comparison tables | SHRINK | Publish task-success data only. Latency tables vs competitors are invalid (§2) and invite disputes. |
| T34 Onboarding smoke | DROP | Onboarding is now Unity's job. |
| T35 Multi-editor | DONE | Pipeline handles it (Acceptance Report §C). |
| T36 Plugin API `[NexusTool]` | DROP | `[CliCommand]` *is* the plugin API. |
| T41 Tool usage stats | DROP | Already exists and isn't a product. |
| T42 PlayerPrefs parity | DROP | Nobody chooses a tool for PlayerPrefs. |
| T43 semantic_find exposure | SHRINK | Expose as one `[CliCommand]`, then measure whether agents call it. Delete it if they don't. |
| T44 mermaid + scene_delta | SHRINK | Keep scene_delta, which fits a "what changed" verification step. Drop mermaid. |
| T45 step_frame / timescale | KEEP (small) | Deterministic stepping supports reliable verification. |
| T46 Log streaming resource | DROP | MCP resource push is barely supported by clients. Agents poll. |
| T47 Localization | DROP | No value. |
| T48 Demo refresh | KEEP later | Only after the verify loop works. It's the adoption asset. |
| T49 SECURITY threat model | KEEP | It must now say plainly that Unity `eval` sits outside Nexus's gates (§3). |
| T50 Alias sweep | DROP | Moot if T10 is dropped. |

### ImageCapturePlan milestones

| Milestone | Verdict | Reason |
|---|---|---|
| M0 Capture V2 baseline | DONE | Accepted. |
| M1 Extract Nexus.Capture | DONE | Accepted, 152/152 EditMode tests. |
| M2 Canonical commands + dual registration | DONE | Only 3 commands, and that's fine. The next commands go straight to `[CliCommand]`. |
| M3 Runtime abstraction | DONE, freeze | A ~600-line selector/capabilities layer (`Editor/Runtime/*.cs`). Do not extend. |
| M4 Hybrid default (`auto`) | DONE, reconsider default | `auto` can leave the legacy HTTP port unbound, so the Python bridge loses the Editor (§6.6). |
| M5 Legacy deprecation ledger + Gates 1–11 | DROP (as a process) | Replace 11 gates with one rule: legacy is frozen, security fixes only, removal decided when Pipeline leaves experimental. |

## 2. Traps: work that feels productive but won't matter

1. **Tool-count race (T10, 156-tool Funplay envy).** Agents don't pick tools by catalog size. In the chat reports they never opened the catalog. More tools cost more tokens and don't bring more use. Unity already exposes ~151 commands and Nexus can't win on coverage. The winning unit is a *skill that names a workflow* ("after editing gameplay code, run `nexus verify`").
2. **Owning a transport.** Three transports (legacy HTTP/WS, Python stdio bridge, Pipeline) for two people. Every hour spent on reload survival, ports, tokens, brokers or client installers duplicates something Unity maintains for free with a larger team. The M4 selector/deprecation ledger/Gates 1–11 is transport bureaucracy.
3. **Own eval (T21/D2).** It matches `unity eval` feature for feature and doubles the attack surface. Nexus's "security depth" story becomes false the moment eval exists anywhere in the Editor (§3).
4. **Microsecond and millisecond benchmarks.** The existing numbers measure no-JSON-RPC-error (not success), unfocused-editor tick quantization (~100 ms), and mismapped scenarios (C8 OS screencapture, C12 click on "Main Camera", C7 false-ready 99 ms). Chasing "≤15 ms object read" optimizes noise. Agents spend seconds thinking between calls, so 100 ms vs 2 ms is not what decides task success.
5. **10-client installers.** Each client changes config paths yearly (CHANGELOG 1.5.0 has 4 installer path fixes). Unity ships `unity skill install`, and Claude Code/Codex read skills and AGENTS.md. Supporting Antigravity/Windsurf/Cline/Roo installers is pure maintenance tax.
6. **Broad AI-agent refactors without acceptance evidence.** PR #201 is the case study: "fix screenshot" became a +12.4k-line migration. It was accepted on internal tests, while T01's own acceptance numbers (20 captures, C8 rerun) were never recorded in the plan's format.
7. **Capture micro-optimization.** Stall 1.6 ms vs 10 ms doesn't matter to a user. Overlay-UI correctness and Windows correctness do.
8. **Comparison tables and positioning refreshes.** They invite disputes and go stale in weeks. A 60-second demo of an agent catching a real bug does more.
9. **Plugin API, multi-editor, broker, log streaming.** These look like architecture work, but no user has asked for them.
10. **Deprecation ledgers for a package with unknown user count.** Without install telemetry or OpenUPM stats, a formal deprecation program serves nobody.

## 3. Existential risks

| # | Risk | Likelihood | Impact | Mitigation | Early-warning trigger |
|---|---|---|---|---|---|
| R1 | **Agents don't choose Nexus** (already observed 4/4) | Already happening | Fatal | Skills-first distribution. One flagship command named in AGENTS.md/skill. Measure selection rate in scripted agent runs. | The next 5 recorded sessions show fewer than 3 with a Nexus call even with a skill installed. Then stop building features. |
| R2 | **Unity adds input sim, Overlay capture, a QA loop** | Medium-high within 6–12 mo (they already have `capture_game_view`, `screenshot`, `eval`, player connection, 31 skills) | Removes the verify niche | Build as thin `[CliCommand]`s on top of Unity so a Unity feature replaces a Nexus internal, not the product. Keep the value in assertions and reports, not primitives. | Pipeline changelog mentions input/simulate/overlay/UI automation, or official skill "unity-testing/qa" appears. |
| R3 | **Third-party CLI loops take the niche** (uLoopMCP has record/replay input and PlayMode verification skills) | Already happening | High | Do a head-to-head evaluation before building T23/T25. If they're good, integrate or specialize (Overlay capture + semantic assertions), don't duplicate. | They top skills.sh or Unity forum mentions for "verify/QA". |
| R4 | **Pipeline API churn (0.x-exp)** | High | Medium: breaks the 3 wrappers and runtime probing | Keep the Pipeline surface in one small asmdef (it already is: `Editor/Pipeline/*.asmdef` with `NEXUS_HAS_PIPELINE`). Pin tested versions in a matrix, fail closed to legacy. | Any Pipeline minor bump that breaks compile. `[CliCommand]` signature changes. |
| R5 | **Security gates bypassable via `unity eval`** | Certain when Pipeline is installed | Security marketing becomes false. A user trusts confirm gates that don't cover the Editor. | Reposition security as "Nexus's own commands are sandboxed; Unity eval is outside Nexus's control". Document it in SECURITY.md. Don't sell security as a differentiator any more. | Any doc or README claiming Nexus protects the project while `auto` prefers Pipeline. |
| R6 | **3 transports for 2 people** | Certain | Slow death by maintenance, and bugs land in the least-tested path | Freeze legacy HTTP and the Python bridge (security fixes only). All new capability goes to `[CliCommand]` only. | More than 20% of commits per month touch `MCPServer*.cs`, `nexus_bridge/`, or installers. |
| R7 | **macOS-only validation (most Unity devs on Windows)** | High | Capture orientation or format or reflection may fail on D3D11/12/Vulkan. First Windows users churn. | A Windows validation box (even a cheap cloud VM with GPU) is the single best infra investment. Pixel-marker overlay test on D3D11 + Vulkan. | The first Windows capture issue, or no Windows run recorded before the 1.7 release. |
| R8 | **Private `GameView.m_RenderTexture` reflection** (`Editor/Capture/GameViewCaptureSource.cs:14-16`) | Medium per Unity minor | The core asset silently returns null and capture fails | Keep a supported fallback (Unity `capture_game_view`/`ScreenCapture` with a warning that Overlay may be missing). Add a startup self-test that logs if the field is gone. | CI or smoke on a new Unity 6000.x beta returns `GameViewRtField == null`. |
| R9 | **AI-written plans with "DO NOT REOPEN" facts that were wrong** (177× claim retracted; "benchmarks show 100-300 ms tax" was mostly throttling; T01 "est 1-2 days") | Already happened | Wrong decisions get locked in and agents are told not to question them | "Facts" require a linked raw artifact + reproduction command + date + environment. Anything else is a hypothesis. Expire facts after 30 days or on a Unity/Pipeline version bump. | Any plan section with "DO NOT REOPEN" lacking an evidence link. |
| R10 | **Team energy and focus** (MASTER_PLAN R6) | High | Project stalls halfway through a migration | A 6-week scope cap with a kill decision at the end (§4). | Two consecutive weeks with no user-visible change. |

## 4. Scenario analysis

**(a) Full MCP competitor (MASTER_PLAN v1).**
- Pros: uses existing assets (120 methods, installers, security story).
- Cons: fights Unity (free and official), Coplay (community), Funplay (156 tools), and Ivan (speed) all at once, with 2 people. Unity deprecated its own in-Editor MCP in favour of CLI+skills, which is a strong sign the MCP-server market is shrinking.
- Right choice only if: Nexus has measurable organic users who pick it over alternatives. No evidence of that exists. **Reject.**

**(b) Add-on layer over Unity CLI ("Unity does the hands").**
- Pros: small surface (`[CliCommand]`s + skills), inherits Unity's lifecycle, multi-editor and auth. The team already proved `[CliCommand]` works. Low maintenance.
- Cons: depends on an experimental package. Differentiation is thin unless the commands are clearly better than `eval` one-liners an agent could write itself.
- Right choice if: agents with the Nexus skill installed call Nexus commands and succeed more often or with fewer tokens than with plain `unity eval`.

**(c) Narrow vertical: verification/QA ("nexus verify").**
- Pros: the one place where Nexus has real assets (Overlay-correct capture, UI Toolkit automation, step_frame, scene_delta, mouse/touch sim). Agents in reports 2–4 *asked* for "combined refresh/compile verification", "runtime smoke test", "post-change verification". That's the only unprompted demand signal in the repo.
- Cons: uLoopMCP is already there. Unity may absorb it. It needs Windows validation and deterministic scenes.
- Right choice if: a 2-week prototype (compile-wait → play → input → capture → assert → report) catches a planted bug in 3 real projects, driven by an agent without human prompting to use it.

**(d) Maintenance mode / sunset.**
- Pros: honest. Frees both people. The capture code and research could be donated to Coplay or Unity or published as a standalone package.
- Cons: sunk cost, and the loss of a nice product people like.
- Right choice if: (b)/(c) prototypes fail the adoption test above within 6 weeks.

**(e) Other options:**
- **(e1) Capture-as-a-library:** publish Capture V2 (Overlay-correct, low-stall, JPEG control) as a tiny standalone `[CliCommand]` package other MCPs/skills can depend on. Minimal maintenance, real value, good reputation.
- **(e2) Skills-only product:** a curated Unity agent skill pack (verification recipes on top of `unity eval` + a few Nexus commands). Near-zero code, and it tests the adoption hypothesis fastest.

**Recommendation:** (b)+(c) as a **time-boxed 6-week bet**, run skills-first (e2) and with (e1) as the fallback, and a hard gate to (d).
- Week 0–1: freeze legacy and the bridge. Write the skill. Build a 10-task agent eval.
- Week 2–4: `nexus verify` as ≤5 `[CliCommand]`s.
- Week 5–6: run the eval.

If Nexus doesn't beat "Unity CLI + official skills alone" on task success or tokens, go to (e1)/(d).

## 5. Process critique and v2 guardrails

What went wrong:
- **"One task = one PR" broke:** T01 became a 164-file migration. A second plan (ImageCapturePlan) silently replaced the first without a written decision.
- **Acceptance criteria not met or not recorded:** T01 demanded a 20-capture smoke + C8 rerun in the PR. The acceptance report ended as "READY FOR INTERNAL MERGE ONLY" on internal tests. Cross-platform wasn't checked.
- **Benchmark validity:** "ok%" counted transport success. Scenarios were mismapped. The editor was unfocused. Nobody asked "did the task succeed?".
- **AI plans as authority:** "DO NOT REOPEN" headers froze claims (177×) that were later retracted. Agents were told not to question the premise, and they didn't.
- **Agents scored their own work:** "M4: IMPLEMENTED / ACCEPTED" was written by the implementing agent.

v2 guardrails:
1. **Definition of done** = user-visible behavior + raw evidence artifact (JSON/log/PNG committed under `Research~/evidence/<task>/`) + reproduction command + environment (OS, GPU API, Unity, Pipeline version). No artifact means not done.
2. **Scope cap:** a PR may touch ≤15 files / ≤800 changed lines unless a human pre-approves a written exception. An agent that exceeds the cap must stop and report.
3. **Stop rule:** when elapsed effort passes 2× the estimate, stop, write down what was learned, and a human re-scopes. (The existing "blocked >2h" rule doesn't catch *expansion*.)
4. **Separate implementer and verifier:** a different agent or human runs acceptance from the artifact alone, on a clean checkout. The implementer may not write "ACCEPTED".
5. **Benchmark rules:** success means a task-level assertion (object exists, test count > 0, image contains marker), never "no JSON-RPC error". Focused editor or documented throttling setting. Report failures. Per-scenario validity check before comparing tools.
6. **Adoption eval as the primary metric:** 10 scripted real tasks run by Claude Code and Codex with (i) Unity CLI + official skills, (ii) + Nexus skill. Measure selection rate, task success, tokens and wall time. Run it before and after every flagship change.
7. **"Facts" hygiene:** every plan fact carries a link to evidence + expiry. Plans are dated, superseded explicitly, and only one is active.
8. **Platform gate:** no release claims for a platform without a recorded run on it (Windows D3D11 minimum).
9. **Freeze list:** legacy HTTP, the Python bridge, installers and the runtime selector take security or crash fixes only. PRs that touch them need a written justification.

## 6. Verified weak spots in the code

1. **Unauthenticated `shutdown_server`:** `Editor/MCPServer.Http.cs:126-133` exempts `get_server_status` *and* `shutdown_server` from auth. `IsValidOrigin` accepts requests with no Origin header (`MCPServer.Http.cs:22-23`), so any local non-browser process can stop the server without the token. The only internal caller is the zombie cleanup (`MCPServer.Discovery.cs:95`), which runs in the same project and could read the token file. The exemption is a needless DoS vector.
2. **Timed-out main-thread calls still execute:** `Editor/MCPServerMethods.cs:215-227` enqueues the action, waits 60 s, then returns "Timeout waiting for Main Thread". The queued lambda is never cancelled and runs later. A client that retries a mutating call (create/write/delete) can apply it twice. It needs a cancelled flag checked inside the lambda.
3. **Wake-up, not dispatch cost:** `MCPServer.Enqueue` (`Editor/MCPServer.Logs.cs:142-145`) only enqueues. The queue drains in `HandleMainThreadQueue` on `EditorApplication.update` (`MCPServer.cs:144-145`, `MCPServer.Logs.cs:109-120`). Nothing wakes the editor, so an unfocused or throttled editor gives the ~100 ms steps seen in the benchmarks. The capture path does call `EditorApplication.QueuePlayerLoopUpdate()` (`Capture/DriverOwnedReadback.cs:154,195`), but the generic dispatch path doesn't. Test Unity's Interaction Mode "No Throttling" preference and a wake mechanism before any T07-scale work.
4. **`wait_for_editor_idle` can report Ready while status says busy:** `Editor/MCPServerMethods.Sync.cs:65-78` checks only cached `IsCompiling`/`IsUpdating`. `Initialize` and `GetServerStatus` also treat `_scriptRefreshBusyUntilUtc` and play-mode transitions as busy (`MCPServerMethods.Status.cs:22, 34-40`). Right after a script write, before Unity flips `isCompiling`, `wait_for_editor_idle` returns Ready. This is the same false-ready class as #0013 and C7's "99 ms compile". It also sleeps in 100 ms steps (`Sync.cs:58,85`) on the listener thread, because it's on the fast-path list (`MCPServerMethods.cs:180`).
5. **Stack traces returned to clients:** `MCPServerMethods.cs:245-251, 277-278` put the full exception stack trace into JSON-RPC error `data`. That's inconsistent with 1.6.0's path redaction for tool-usage stats. It's minor, but it's token waste too.
6. **The "hybrid" only reaches 3 commands:** the Pipeline projection exposes only `project_map`, `group_compile_errors` and `capture_game_view` (`Editor/Pipeline/NexusPipelineCommands.cs:21,31,43`). `NexusRuntimeSelector.CanSkipLegacyHttpBind` (`Editor/Runtime/NexusRuntimeSelector.cs:64-67`) lets `auto` skip binding legacy HTTP. The Python bridge is HTTP-only (`Editor/nexus_bridge/_transport.py:13` `DEFAULT_PORT = 8081`; no bridge file references Pipeline except one schema string). In Acceptance Report case B (second editor, `legacy_http_bound=false`), the 14 manager tools and ~117 raw methods are unreachable for that project. Users in `auto` can silently lose most of Nexus.
7. **Capture depends on private reflection, and orientation is proven only on Metal:** `Editor/Capture/GameViewCaptureSource.cs:14-16` reflects `GameView.m_RenderTexture`. `:52-55` applies a fixed y-flip blit whose correctness was proven only by the Metal marker test (Acceptance Report §F). There's no recorded D3D11/D3D12/Vulkan run, and no `SystemInfo.graphicsUVStartsAtTop` handling is visible in this file.
8. **Process evidence:** PR #201 (`rework/T01`, merge `329990e`) changed 164 files (+12,439/−328), covering 75 Editor files and the Runtime/Commands/Capture/Pipeline trees, under a task budgeted at 1–2 days.

Sources (web): [unity-cli-loop (uLoopMCP) record-input skill](https://www.skills.sh/hatayama/unity-cli-loop/uloop-record-input), [uloop replay-input](https://www.skills.sh/hatayama/unity-cli-loop/uloop-replay-input), [Unity CLI coverage, runtimewire](https://runtimewire.com/article/unity-ships-a-cli-that-lets-ai-agents-operate-running-game-projects), [gamedev.net Unity CLI news](https://gamedev.net/news/202607-unity-cli-pi-r5423/).
