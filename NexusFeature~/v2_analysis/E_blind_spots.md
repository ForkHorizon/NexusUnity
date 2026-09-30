# E — Blind-spot review of MASTER_PLAN_V2 (v2.2)

Reviewer: independent analyst (agent E) · Date: 2026-09-30 · Read-only review. No repo file was changed.
Scope: things that the earlier passes (A, B, C, D, CH1, CH2, JUDGE, U1, U2) did not raise, or raised only in passing.
Evidence tags: **CONFIRMED** means a primary source or repo file:line. **SEARCH** means search-engine snippets only, because the site was blocked for fetch. **SPEC** means speculation.

---

## 1. Top 10 blind spots, ranked by impact

### #1 — Unity is removing the domain-reload model the plan is designed around (HIGH)
**What was missed.** Principle 3, L-02 (resumable job store), V-02b, V-08 and the P0-02 epoch design all assume that "domain reload is the normal path". The JUDGE accepted CH1-12 on the grounds that "Default Enter Play Mode reloads the domain" (`MASTER_PLAN_V2.md:622`). Unity's timeline now says otherwise:
- Unity 6.6 (released 2026-08-31) turns **Fast Enter Play Mode on by default for new projects**: entering Play Mode no longer reloads the domain. Unity's 6.6 manual says to keep domain reload off "to prepare for… CoreCLR, which doesn't have the domain reload concept" (SEARCH: docs.unity3d.com/6000.6/Documentation/Manual/configurable-enter-play-mode.html; openupm.com/blog/unity-66-fast-enter-play-mode-package-readiness/).
- 6.7 LTS is the last Mono release. **6.8 (late 2026) removes Mono**, and the Editor moves to CoreCLR/.NET 10. Traditional domain reload goes away and is replaced by partial reloads of only the assemblies that changed (SEARCH: discussions.unity.com "CoreCLR, Scripting, and Serialization Update – June 2026"; oceanviewgames.co.uk/blog/posts/unity-path-to-coreclr).
- The Unity 7 early beta starts in **December 2026**, during the plan's weeks 10–12 (U2).
- Unity 6.0 LTS reaches **end of support on 2026-10-16**, which is week 2 of the plan (SEARCH: unity.com/releases/unity-6/support). Rule §6.11 still says "floor 6000.0", and the harness runs 6000.4.3f1 (`results/BENCH_RESULTS.md:10`), which is neither the floor nor the current LTS (6.3).

**Why it matters.**
- (a) Epochs keyed on `AssemblyReloadEvents` do not fire when play entry has no reload. On CoreCLR, "reload" becomes per-assembly, so the meaning of `reload_epoch` changes.
- (b) The job store's reason for existing (surviving the play-entry reload) weakens on 6.6+ new projects, but it still applies on 6.0–6.5 and on migrated projects. The plan needs a two-mode design, not one mode.
- (c) V-08 ("optionally disable domain reload when a lint passes") is now the reverse of Unity's default. Its lint becomes more valuable in a new role: **stale static state is a new source of false success for agents.** A verify or play run without domain reload can pass or fail because of leftover statics.
- (d) Nexus itself keeps static state (`MCPServer` queues and statics, `MCPServer.cs:91-135`). It has not been audited for no-reload play entry or for CoreCLR.

**Recommendation.** Add task **P0-13 "Unity version matrix + CoreCLR readiness spike" (2d)**:
- Run the harness on 6.3 LTS, on 6.6 with Fast Enter Play Mode, and on the latest 6.8 alpha or Unity 7 beta when it is available.
- Audit Nexus's own statics.
- Design epochs on `CompilationPipeline` and play-state events, not on reload events only.

Also:
- Rewrite Principle 3 as "reload may or may not happen; never depend on either".
- Turn V-08 into a `nexus_diagnose` check for stale static state.
- Move the floor decision (6000.0 vs 6000.3) into §10.

### #2 — Agent hooks can make Nexus run without the agent choosing it (HIGH)
**What was missed.** The plan treats adoption as a discovery problem: skills, instructions, descriptions, and Gate A1 "agent chooses Nexus ≥60%". Claude Code, Codex CLI (10 hook events incl. PreToolUse and PostToolUse; SEARCH: codex.danielvaughan.com 2026-05-17) and Gemini CLI (SEARCH: thenewstack.io) all ship **lifecycle hooks**. A Claude Code plugin can bundle hooks. Unity's official plugin is skills-only, with "no MCP server, no hooks" (U2 line 23). Nobody proposed hooks.

**Why it matters.** Two hooks attack the #1 risk (R1) and the false-success metric directly:
- A **PostToolUse hook on `*.cs` edits** runs `nexus_compile` in summary mode (about 100 tokens) and injects the result into context.
- A **Stop hook** runs `nexus_diagnose` and blocks "done" when new compile errors or missing references appeared.

The agent no longer has to *remember* Nexus. It is also a free differentiator next to Unity's hook-less plugin.

**Recommendation.** Add **S-09 "Hook pack" (2–3d, Phase 1)** for Claude Code, Codex and Gemini: opt-in, token-capped, and fail-open when the Editor is absent. Add an arm "Nexus + hooks" to the P0-09 re-run.
- Redefine A1 as "a Nexus result is present in context at the Unity-state step, whether the agent called it or a hook injected it". Report calls and hook injections separately.
- Hooks run shell commands on every edit, so treat them as a security surface: no network, and the Editor token is read from a file.

### #3 — The MCP spec changed under P0-07 (HIGH for P0-07/S-03, MED overall)
**What was missed.** The MCP **2026-07-28** revision (CONFIRMED: `modelcontextprotocol/modelcontextprotocol` `docs/specification/2026-07-28/changelog.mdx`) changes several things the plan depends on:
- It **removes the `initialize` handshake and sessions**. `instructions` now live in the new, mandatory **`server/discover`** RPC (`server/discover.mdx:100`).
- It replaces `list_changed` notifications with an opt-in `subscriptions/listen` stream.
- It moves **tasks** into an official extension with polling via `tasks/get`.
- It adds **Multi Round-Trip Requests** (`input_required`), which replace server-initiated elicitation.
- It requires `ttlMs`/`cacheScope` on list results and a deterministic `tools/list` order (the order helps prompt caching).
- It deprecates Roots, Sampling and Logging.
- New official extensions: **Skills over MCP** (SEP-2640, Final) and **MCP Apps** (inline HTML UI; supported by Claude, Cursor, VS Code Copilot and ChatGPT according to `docs/extensions/client-matrix.mdx`).

P0-07 says "`initialize.instructions`… current MCP protocol version" (`MASTER_PLAN_V2.md:266`). That text is ambiguous, and it is wrong for 2026-07-28 clients.

**Why it matters and what it opens.**
- (a) The bridge must serve **both** the 2025-11-25 handshake and 2026-07-28 `server/discover`.
- (b) The **tasks extension** is the standard form of `nexus_job_status/wait` (L-02). Use it, not a private protocol.
- (c) **MRTR/elicitation gives Nexus a real human confirmation gate** in clients that support it. That fixes D-11 ("`confirm:true` is agent-set") properly and turns security back into a real feature.
- (d) **Skills over MCP** lets the bridge ship its own SKILL.md to MCP-only users.
- (e) Dynamic profiles through `list_changed` (S-03) now depend on per-client support for `subscriptions/listen`.

**Recommendation.**
- P0-07: "implement `server/discover` + legacy `initialize`; instructions in both; deterministic tool order; `ttlMs`".
- S-03: add a column "2026-07-28 support" to the per-client matrix.
- L-02: "expose jobs via the `io.modelcontextprotocol/tasks` extension where the client supports it".
- New backlog item: "human confirm via MRTR `input_required` for `.cs` writes, deletes and `nexus_run`" (only where the client supports it; otherwise keep the intent flag).

### #4 — Cloud and headless agents cannot reach a local Editor (MED-HIGH)
**What was missed.**
- The plan assumes an interactive Editor on the user's Mac or PC.
- A growing share of agent work runs in **cloud containers with no Editor, GPU or Unity license**: Claude Code on the web, Codex cloud, Copilot coding agent, Cursor background agents (SEARCH: morphllm.com Codex vs Claude Code 2026). This review itself runs in such a container.
- Unity Personal licenses are hard to activate in Docker, and command-line activation is documented as Pro-only (SEARCH: issuetracker.unity3d.com "cannot activate license within a docker container"; docs.unity.cn ManagingYourUnityLicense).
- Nexus has no batchmode handling at all (`grep isBatchMode Editor/` returns no hits).

**Why it matters.** In those runs, "live Editor truth" is worth zero, so the plan's persona shrinks to local interactive sessions.

**The opportunity is an Editor-less "file truth" mode.** Several planned features need no running Editor:
- the Safe-Mode diagnosis (`Editor.log` + asmdefs), which is already in S-06
- the asmdef graph
- YAML reverse references (C-02, Force Text only)
- `project_map`

These could run as a plain CLI in any container. A second option is a batchmode `-executeMethod` entry point, so CI (GameCI) can check an agent's PR for compile errors, missing references and prefab breakage.

**Recommendation.** Add a spike **P0-14 "Headless/cloud reach" (1d)**. It answers three questions:
- Does `unity command` reach a `-batchmode` Editor?
- Can Nexus commands run under `-batchmode -nographics`? Capture needs a GPU, so it will not.
- Which S-06/C-02 pieces can be packaged as a Python or .NET CLI that does not need an Editor?

If the answers are positive, add a Phase-4 task "file-truth CLI for cloud agents".

### #5 — Unity's ToS now restricts agentic access (MED-HIGH, legal)
**What was missed.**
- Unity's ToS updated **2026-06-30**. §17.2 (ff)/(gg) prohibit interacting with any "Offering" through "any AI agent… LLM, command line interface, or MCP client or server unless such access is via Authorized Agentic Access" (SEARCH: unity.com/legal/terms-of-service; roboin.io 2026-07-02; discussions.unity.com thread 1724661).
- Unity's answer came from support on Reddit and the forum: the terms apply to Unity's cloud, online services, Asset Store and public APIs, and **local Editor MCP integrations are fine**.
- As far as the search showed, the **ToS text itself was not changed**.
- Separately, Pipeline 0.8 moved to the Unity ToS license (U2).

None of the reviews mention any of this.

**Why it matters.**
- A studio's legal team reviewing the plan's target persona (production projects) will read the text, not the Reddit reply. "Use the Unity-designated framework (Pipeline/CLI)" becomes a *legal* argument for Pipeline-first, and a question mark over the legacy HTTP path.
- Any Nexus feature that touches Unity online services would be squarely in scope. Examples: package registry search, Asset Store, cloud build, Unity docs lookup.

**Recommendation.**
- Add a short "Unity ToS and agent access" section to `SECURITY.md`/README: local-only, no Unity online APIs, citation of Unity's clarification.
- Add a rule to §6.8: "Nexus never calls Unity online services".
- Add a monthly watch-list item for a ToS amendment.
- Add risk **R17**.

### #6 — Privacy and NDA data flow is unstated (MED)
**What was missed.**
- `nexus_snapshot`, `nexus_look`, console logs and `nexus_context` send unreleased art, UI and code from a *production project* to third-party model providers.
- The plan's security section covers attackers (injection, sandbox), not **data leaving the studio**.
- Principle 9 is about injection, not disclosure.

**Why it matters.**
- NDA-bound studios, including work-for-hire and platform-holder NDAs, often forbid sending screenshots to cloud LLMs.
- This quietly narrows the primary persona.
- It is also a positioning chance: "text-first truth, images opt-in, works with local models".

**Recommendation.**
- Add `PRIVACY.md`, a one-page data-flow statement: what each command returns, and that Nexus itself sends nothing anywhere.
- Add a project setting `images: off|opt-in|on` and a path/regex **redaction deny-list** that applies to snapshot, logs and context. Put it in §6.8 and S-07.
- In P0-09, record the share of tasks solved **without** any image. That also tests the "Overlay-correct image" claim; see opportunity O6.

### #7 — The package footprint in user projects (MED)
**What was missed.**
- `package.json` hard-depends on `com.unity.inputsystem: 1.19.0` (`package.json:23-26`). The dependency exists only for `MCPServerMethods.Input.cs`.
- Adding Input System to a legacy-input production project brings the backend-switch prompt and a possible *Active Input Handling* change. That is a ProjectSettings change, which the plan elsewhere forbids (§6.8). This part is SPEC: the exact prompt behaviour on 6.x has not been re-checked.
- `Runtime/UnityMCP.Runtime.asmdef` covers all platforms and is auto-referenced, so `MCPRuntimeLogger` (a `logMessageReceivedThreaded` subscriber, `Runtime/MCPRuntimeLogger.cs:18-22`) ships in **release player builds**. Unity's Pipeline, by contrast, is stripped from release builds (U2).

**Why it matters.** Production studios audit what an AI-tool package adds to builds and settings. Both issues are cheap to fix, and both are real blockers in a studio review.

**Recommendation.** Add P0-15 (1–2d):
- Make Input System optional through `versionDefines` (`NEXUS_INPUT_SYSTEM`), with a `LEGACY_INPUT_UNSUPPORTED`-style error when it is absent.
- Constrain the Runtime asmdef to `UNITY_EDITOR || DEVELOPMENT_BUILD`, or move it into the Editor assembly.
- Add a CHANGELOG note.

### #8 — Publishing the benchmark as a public "Unity agent eval" (MED-HIGH opportunity)
**What was missed.**
- P0-08/08b/08c are about 8 agent-days spent building a fixture, oracles and an agent-run harness, used only as an internal gate instrument.
- There is no neutral, reproducible benchmark for Unity agent tooling. Unity, Coplay/Aura, Ivan, uLoop and Funplay all make unverifiable claims.

**Why it matters.**
- Owning the yardstick brings authority, inbound links and conversations with competitors.
- It survives every fallback in §9, including "maintenance mode".
- R-03 COMPARISON.md becomes a credible leaderboard, not self-promotion.

**Recommendation.**
- Add to R-03: "publish `unity-agent-bench` (fixture + oracles + harness) as a separate MIT repo with a results table and an invitation for competitors to submit their configs".
- Keep the gate tasks private until Gate B so they are not contaminated, and publish them afterwards.

### #9 — The measurement blind spot, and an ironic leak (MED)
**What was missed.**
- The plan forbids telemetry and relies on OpenUPM, GitHub traffic, skills.sh and 5–10 testers.
- Meanwhile, §14b.3 item 4 tells Nexus and its skills to pass `--caller nexus` on every Unity CLI call. U1 says the Unity CLI has an **always-on anonymous usage ping**.
- So **Unity would get Nexus usage data and Nexus would not.** This is not a privacy problem for users (the data is anonymous), but it is a strategic asymmetry nobody noted.

**Recommendation.**
- Keep `--caller`: it is good citizenship and may earn partner goodwill. Disclose it in PRIVACY.md.
- Ask Unity for aggregate caller counts in the P0-12 outreach.
- Add privacy-respecting signals:
  - an opt-in `nexus doctor --report` that writes a redacted environment and usage summary for the user to paste into a GitHub Discussion
  - a local-only usage counter shown in `nexus_status detail:"doctor"`
  - "used-by" reports through an issue template
- Move "opt-in local usage export" from backlog into S-08.

### #10 — Bus factor, community and funding (MED)
**What was missed.**
- Evidence: `git shortlog` shows about 240 of 257 commits from the owner (several email variants) and 7 from the partner (`air17`). The GitHub API shows **6 stars, 1 fork, 71 open issues**.
- The planned gates depend on ≥5 external users by week 9 (early December). Growing from a 6-star base in the pre-holiday window is hard.
- Anthropic's Claude for Open Source requires 5k+ stars, so Nexus is **ineligible** (SEARCH: letsdatascience.com, 2026-07). OpenAI's Codex for OSS / Codex Open Source Fund offers up to $25k in API credits with looser criteria (SEARCH: openai.com/form/codex-open-source-fund; developers.openai.com/codex/codex-for-oss).
- The largest open-source rival, Coplay's unity-mcp (about 7k stars), now belongs to **Aura (Ramen), a paid cross-engine Unity + Unreal assistant** (SEARCH: coplay.dev/blog/ramen-acquires-coplay; pulse2.com).

**Recommendation.**
- Apply to the Codex OSS fund. The agent-bench harness (#8) is a natural grant use.
- Enable GitHub Sponsors.
- Triage the 71 issues down to fewer than 20 (close stale ones) before the public push in week 2.
- Add a CONTRIBUTING recipe "add a `[CliCommand]` in 30 lines". It is the cheapest contributor funnel.
- Add R18 "bus factor 1" with the mitigation "partner owns review of ≥50% of PRs; architecture notes kept in `PROJECT_MAP.md`".

---

## 2. Opportunities

- **O1 Hook pack** for Claude Code, Codex and Gemini (see #2). Nexus becomes automatic instead of optional.
- **O2 Real human confirmation gate** through MCP MRTR/elicitation (see #3). Security becomes a feature again.
- **O3 Editor-less "file truth" CLI** for cloud agents and CI (see #4). Safe-Mode diagnosis, asmdef graph and YAML references reach the growing headless market.
- **O4 Public Unity agent benchmark** (see #8).
- **O5 Stale-static-state diagnosis** (see #1). It becomes more relevant with every 6.6+ project and every package author migrating (the OpenUPM readiness post shows this is an active concern).
- **O6 Text-first positioning and local models.** Vision models are weak on small UI text and precise layout at 768 px. Local and small models often have no vision at all. Structured UI trees (RectTransform, raycast, text) are more reliable than pixels. SPEC: pitch "works without sending images". Add one local-model arm (for example Qwen-class via Ollama) to one P0-09 task, N=3, as a probe.
- **O7 MCP Apps.** A `nexus_snapshot` inspector panel rendered inline for the *human* (Claude, Cursor, VS Code Copilot). Low effort later; not in the 12 weeks.
- **O8 Skills over MCP.** Ship SKILL.md through the bridge itself (SEP-2640 Final; client support partial). Monitor.
- **O9 Unity AI Assistant tool registry.** `com.unity.ai.assistant` documents `[McpTool]`/`McpToolRegistry` and an Assistant API (SEARCH: docs.unity3d.com/Packages/com.unity.ai.assistant@2.x). Registering Nexus composites there could reach in-Editor users such as technical artists and designers who never open a terminal. SPEC: the in-Editor MCP server is deprecated (F2), so check whether the Assistant's *agent* tool API is separate from it.
- **O10 Adjacent persona: Asset Store and package publishers.** They need "does my package compile and behave on 6.0/6.3/6.6/CoreCLR, and does it survive Fast Enter Play Mode?". `nexus_compile` + static-state lint + batchmode is a natural fit (see #1 and #4). Cheap to test with 2–3 publisher conversations.
- **O11 Chinese ecosystem.** Unity 6 is not distributed in mainland China. Tuanjie 2.0 (July 2026) ships its own agent **Codely** plus a Codely Bridge MCP (SEARCH: ithome.com; 36kr). Whether Pipeline/CLI exist there is unknown (SPEC: likely not), and there Nexus's legacy transport would be the *only* one. First check that Nexus installs on Tuanjie at all (floor 6000.0 vs Tuanjie's base version). Low priority; one-hour check.

## 3. Threats

- **T1** CoreCLR / Unity 7 changes reload, statics and threading. Nexus reflects on a private field (`GameView.m_RenderTexture`) and has not been tested on it. The Unity 7 beta lands in Phase 5 (see #1).
- **T2** Stateless MCP clients break the bridge's handshake-dependent features (instructions, profiles) (see #3).
- **T3** The ToS text makes studio legal teams nervous about third-party agent tools (see #5).
- **T4** Cross-engine consolidation:
  - Aura (with Coplay) sells one Unity + Unreal assistant.
  - Epic shipped an **official MCP plugin in UE 5.8**, is "core infrastructure into UE6" (SEARCH: pugetsystems.com 2026-07-09; strayspark.studio).
  - Godot has community MCPs with about 180 typed tools and safety classes (SEARCH: godotengine.org asset library 5434).

  The "live truth / verify" layer is becoming table stakes in every engine. A small team cannot go cross-engine. Nexus should stay Unity-deep, but should design the benchmark (#8) and the verify DSL to be engine-neutral, so the ideas travel even if the code does not.
- **T5** Agent work moves to cloud and headless runs where Nexus is absent (see #4).
- **T6** Schedule collision: Gate B (about 2026-12-04), Phase 5 releases (around 2026-12-21) and the Unity 7 early beta all fall in December, together with the holiday attention dip. External-user recruitment for Gate B lands in the worst month.
- **T7** Tuanjie's Codely and Unity AI's paid agent grow the "no-terminal" segment, which Nexus does not target. That is fine, but it caps the total market to terminal-agent programmers on local machines.

---

## 4. Internal inconsistencies and sequencing risks in v2.2

1. **F3 vs §14b.2 "Jobs".** F3 says `--detach` + `unity job` is "a hypothesis until P0-10" (`MASTER_PLAN_V2.md:94`). §14b.2 says "CONFIRMED `--detach`, `unity job`" (line ~571). U2 dates it to CLI beta.10. Update F3; V-07's condition is probably already met.
2. **Wake-up.** §14b.2 says "USE `set_autotick` in pipeline mode; P0-01a/b only for legacy". F8 says "persistent Pipeline shows the same 3 ticks × ~100 ms, so pipeline mode does not escape it". Until P0-01a measures `set_autotick`, §14b.2 must say "candidate", not "USE".
3. **CH1-12 premise** ("Default Enter Play Mode reloads the domain") is false for new 6.6+ projects (#1). **V-08** as written contradicts Unity's default.
4. **§5.1 core contents.** Core lists `nexus_find_references`, which is Phase 4 (C-02) and conditional on beating `search`/`vcs affected` (§14b.2). With 8 of 8 slots taken, `nexus_context` promotion (C-01) has no slot. State the swap rule.
5. **P0-07 protocol version** is unspecified and targets a handshake the current spec removed (#3).
6. **Rule §6.11 floor 6000.0** conflicts with 6.0 end of support on 2026-10-16 and with a harness on 6000.4. Pick the floor explicitly, test on the floor and on the current LTS, and add it to §10.
7. **Phase 0 load.** P0-01a…P0-12 plus P0-08b/c sum to about 36 agent-days in weeks 1–2, under a limit of ≤4 open PRs and 2 human reviewers. P0-09 (the baseline) depends on P0-08 and P0-08b and must finish before S-01 publishes in week 3. That path is serial and has no slack. Name which tasks slip first (suggested: P0-01b, P0-10b→week 3).
8. **Gate A1 metric vs hooks.** If hooks (#2) are added, "agent chose Nexus" undercounts value. Pre-register the metric before P0-09 runs.
9. **Pipeline license.** Pipeline 0.8 is under the Unity ToS (U2). The Nexus asmdef only references it, which is fine for MIT, but the plan's fallback A1 ("publish Capture V2 as a `[CliCommand]` package") must not vendor any Pipeline code.

---

## 5. Proposed plan edits (text snippets)

- **§1, new F16:** "Unity 6.6 (2026-08-31) enables Fast Enter Play Mode by default for new projects; 6.7 LTS is the last Mono release; 6.8 removes Mono and traditional domain reload (CoreCLR/.NET 10); Unity 6.0 LTS support ends 2026-10-16. Evidence: Unity 6.6 manual (configurable-enter-play-mode), CoreCLR update June 2026 (search). Expires: 6.8 beta."
- **§1, new F17:** "MCP 2026-07-28 removes `initialize`/sessions; `instructions` move to `server/discover`; tasks become an extension; MRTR replaces server-initiated elicitation. Evidence: modelcontextprotocol spec changelog 2026-07-28."
- **§1, new F18:** "Unity ToS (2026-06-30) §17.2 (ff)/(gg) restrict agentic access to Offerings to 'Authorized Agentic Access'; Unity support states local Editor integrations are unaffected (informal). Evidence: unity.com/legal/terms-of-service; discussions.unity.com/t/…/1724661 (search)."
- **§3 Principle 3 (replace first sentence):** "A reload may or may not happen (Fast Enter Play Mode is default on 6.6+; CoreCLR reloads per assembly). Readiness never depends on a reload event alone; workflows are resumable where reloads happen and state-reset-aware where they don't."
- **§7 Phase 0, add:** "**P0-13 Version matrix + CoreCLR spike (2d)**: 6.3 LTS, 6.6 FEPM, 6.8/7 alpha-beta when available; audit Nexus statics. **P0-14 Headless reach spike (1d)**: batchmode/`-nographics`, `unity command` to a batchmode Editor, Editor-less CLI candidates. **P0-15 Footprint (1–2d)**: optional Input System via `versionDefines`; Runtime asmdef constrained to `UNITY_EDITOR || DEVELOPMENT_BUILD`."
- **§7 P0-07, replace:** "`server/discover` (2026-07-28) and legacy `initialize` (2025-11-25), `instructions` in both; deterministic `tools/list` order + `ttlMs`; image content blocks…"
- **§7 Phase 1, add:** "**S-09 Hook pack (3d)**: opt-in PostToolUse (`*.cs` → `nexus_compile` summary) and Stop (`nexus_diagnose` guard) hooks for Claude Code / Codex / Gemini; fail-open without an Editor; token cap 200. E: P0-09 arm 'Nexus + hooks'."
- **§7 V-08, replace with:** "**V-08 Static-state check (2d)**: `nexus_diagnose` flags mutable statics / static events that survive play entry without domain reload."
- **§7 R-03, append:** "Publish `unity-agent-bench` (fixture, oracles, harness) as a separate MIT repo after Gate B; invite competitor configs."
- **§6.8, append:** "Nexus never calls Unity online services (ToS §17.2). Image capture respects the `images` setting and redaction deny-list."
- **§10, add G12:** "Unity floor: 6000.0 vs 6000.3 LTS (6.0 support ends 2026-10-16). Recommendation: **keep 6000.0 installable but test and support 6000.3+**." **G13:** "Hooks as a first-class entry point. **Yes, opt-in.**" **G14:** "Apply to Codex for OSS / enable Sponsors. **Yes.**"
- **§12, add:** "R17 ToS/legal perception (trigger: ToS amendment or studio feedback). R18 Bus factor 1 (trigger: partner reviews <30% of PRs). R19 CoreCLR/Unity 7 break (trigger: any failure on the 6.8/7 matrix). R20 December collision (trigger: Gate B recruitment <3 users by week 8)."
- **§11, add metric:** "Share of relevant tasks solved with zero images sent."

## 6. Open questions for the owners

1. Which Unity versions do your real users (the LinkedIn/Telegram testers) run: 6.0, 6.3 LTS or 6.6? Do they use Fast Enter Play Mode?
2. How many of your own agent sessions now run in cloud or background agents (Codex cloud, Claude Code web) rather than a local terminal?
3. Would you accept Nexus acting through **hooks** (automatic, opt-in) rather than only through agent choice? It changes Gate A's definition.
4. Is any tester under an NDA that forbids sending screenshots to cloud models? How many would need `images: off`?
5. Where are you legally domiciled, and can you receive GitHub Sponsors or OpenAI grant payouts? This affects the funding options.
6. Does Nexus need to be installable on Tuanjie for Chinese users, or is that out of scope?
7. Should the benchmark be released publicly even if Nexus loses on it?
8. Is December acceptable for Gate B and the 1.9.0 release, or should the schedule avoid the Unity 7 beta and holiday window (for example, freeze in week 11)?

## Sources
- MCP 2026-07-28 changelog and extensions: https://github.com/modelcontextprotocol/modelcontextprotocol (docs/specification/2026-07-28/changelog.mdx, server/discover.mdx, docs/extensions/*)
- Unity 6.6 Fast Enter Play Mode default: https://docs.unity3d.com/6000.6/Documentation/Manual/configurable-enter-play-mode.html · https://openupm.com/blog/unity-66-fast-enter-play-mode-package-readiness/
- CoreCLR / 6.8 / Unity 7: https://discussions.unity.com/t/coreclr-scripting-and-serialization-update-june-2026/1723299 · https://oceanviewgames.co.uk/blog/posts/unity-path-to-coreclr · https://automaton-media.com/en/news/next-gen-game-engine-unity-7-announced-for-the-first-quarter-of-2027-promising-seamless-transition-from-unity-6
- Unity 6 support dates: https://unity.com/releases/unity-6/support
- Unity ToS 2026-06-30 and reaction: https://unity.com/legal/terms-of-service · https://roboin.io/article/en/2026/07/02/unity-denies-concerns-over-ban-on-third-party-ai/ · https://discussions.unity.com/t/new-terms-of-service-is-unity-restricting-local-ai-tools-and-ai-training/1724661
- Hooks: https://codex.danielvaughan.com/2026/05/17/codex-cli-hooks-lifecycle-pretooluse-posttooluse-governance-enforcement/ · https://thenewstack.io/gemini-cli-gets-its-hooks-into-the-agentic-development-loop/ · https://lilting.ch/en/articles/gemini-cli-hooks-research
- Cloud agents: https://www.morphllm.com/comparisons/openai-codex-vs-claude-code
- Unity license in Docker: https://issuetracker.unity3d.com/issues/cannot-activate-license-within-a-docker-container
- Unreal 5.8 MCP: https://www.pugetsystems.com/blog/2026/07/09/unreal-engine-mcp-hands-on-testing-ai-inside-the-editor/ · https://www.strayspark.studio/blog/epic-official-mcp-plugin-ue5-8-vs-third-party
- Godot MCP: https://www.godotengine.org/asset-library/asset/5434
- Tuanjie 2.0 / Codely: https://www.ithome.com/0/982/616.htm · https://eu.36kr.com/de/p/3934480632462729
- Aura/Coplay: https://coplay.dev/blog/ramen-acquires-coplay · https://pulse2.com/ramen-acquires-coplay-to-expand-cross-engine-assistant
- Unity AI credits and Assistant tool registry: https://unity.com/legal/unity-ai-credits-terms · https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.2/manual/unity-mcp-tool-registration.html
- OSS programs: https://www.letsdatascience.com/news/anthropic-grants-claude-max-access-to-maintainers-35f128fa · https://openai.com/form/codex-open-source-fund/ · https://developers.openai.com/codex/codex-for-oss
- Repo facts: `package.json:23-26`, `Runtime/UnityMCP.Runtime.asmdef`, `Runtime/MCPRuntimeLogger.cs:18-22`, `Editor/MCPServer.cs:91-135`, `NexusFeature~/results/BENCH_RESULTS.md:10`, `git shortlog -sne`, GitHub API (6 stars, 71 open issues).
