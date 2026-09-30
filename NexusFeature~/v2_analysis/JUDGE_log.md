# JUDGE LOG — MASTER_PLAN_V2 v2.0 → v2.1 (2026-09-30)

## Summary
- 52 distinct objections judged (CH1 technical 32, CH2 strategy 20): **ACCEPT 37 · PARTIAL 13 · REJECT 2**.
- Verification done: repo code (aliases `nexus_*`, D-2 disposed event at MCPServerMethods.cs:215-224, D-8 at MCPServer.Networking.cs:105-113, D-10 at GroupCompileErrorsCommand.cs:61-80, `confirm:true` at MCPServerMethods.Utils.cs:287-291, Pipeline 3-tick cadence in STABILIZATION_ACCEPTANCE_REPORT.md §E), AI reports 3/4, Socials screenshots, GitHub (hatayama/unity-cli-loop v3.0.1, akiojin/unity-cli), WebSearch (Unity CLI launch demo: agent play-tests and catches a bug).
- Key plan changes: flagship moved to "Live truth" (`nexus_snapshot`, `nexus_run`, `nexus_compile`, `nexus_look`); `nexus_verify` conditional on Gate A2 (P0-10b competitor spike) and Gate B vs the best competitor, moved to week 9; the hands/eyes metaphor dropped; reload-as-normal-path (resumable job store L-02 + V-02b); P0-01 split into spike + implementation and removed from the 1.7.0 dependencies; underscore public names; `Submitted` is not an error; `request_id` idempotency; Input System only; single `EditorUnityPrimitives`; no code generator; pre-registered gates with absolute thresholds, N=8–10, frozen external prompts, third-party tasks and external users; new tasks P0-08b agent harness, P0-08c harness lock, P0-10b, P0-12 outreach, S-08 testers; Windows before Gate B; OpenUPM/skill listing at weeks 2–3; security rules extended; persona §2.1; COMPARISON with 6 competitors; G8/G9/G10/G11.
- Rejected: CH2-6 `eval` forwarder (same Unity floor as Pipeline, adds surface, blurs the security boundary; documented instead) and CH2-20 (the plan never claimed profiles are novel). Partial rejections: native dialog interception (not feasible; detection only), live-truth demo at week 3 (nothing built yet), ≥5 external users by week 4 (moved to Gate B), a full harness farm (lock + 2nd Editor instead), in-product stats export (backlog), dropping the ≤2.5k tools/list budget (kept, since not every client defers schemas).
- Placeholder §14b left for the Unity roadmap research step.

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
