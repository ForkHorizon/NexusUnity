# CH2 — Strategy / Product / Market challenge to MASTER_PLAN_V2

Reviewer: Challenger 2 of 2 (adversarial, strategy angle). Date 2026-09-30. Read-only review.
Inputs: `NexusFeature~/MASTER_PLAN_V2.md`, SHARED_CONTEXT, `v2_analysis/A–D`, `AI Chat reports 1–4`, `Socials/*.png`, README, plus web research (sources inline).

## 0. Verdict in one paragraph

The plan's process discipline is excellent. Its strategic bet is weaker than it looks. `nexus.verify` (play, input, assert, report) is chosen as the flagship, but **none of the four real sessions and none of the six feedback screenshots asked for it**. The observed demand was different: live Editor truth (hierarchy, inspector values, Game View, "did it reimport"), compile truth, and "run my existing editor tool with arguments". The only ask that touches verify (Funplay's input sim) came bundled with execute_code, Scene View screenshots, and "MCP sees only 14 of 120". The plan drops or hides those. The verify niche is also more crowded than the plan's single-competitor check (uLoopMCP) assumes. At least four shipping tools already do agent-driven Play Mode input plus screenshot plus verdict in Unity, and Unity's own CLI launch demo is literally "the agent tests in Play Mode and catches a real bug". The gates as written cannot fail cleanly. The plan also spends 8–10 weeks before the new positioning meets a single external user.

---

## 1. Positioning and flagship choice

### 1.1 [CRITICAL] The flagship is not what the evidence asked for (§0, §2, G4)
Evidence from the repo:
- Report 3 (Canvas/Forest bug), §9: *"I needed runtime confirmation of: selected object hierarchy; live Inspector values; Canvas/Image settings; Game View rendering; whether Unity had reimported the changed asset."* Its suggestion was a **"Runtime Unity snapshot"** operation. The user sent screenshots by hand. That is a *look/snapshot* need, not a play→input→assert need.
- Report 4: the agent needed to **run an existing menu item that opens a folder picker**, then wait for the import. Computer-use failed on the dialog. That is *editor-tool automation* (menu + dialog arguments + completion wait). No roadmap task covers it.
- Reports 1–2: compile truth after `dotnet build`, and a 19 s refresh wait. Phase 0 and `nexus.compile` cover these. Good.
- Socials: token cost (Abdullah), deferred loading (James), "how is it better than Ivan + CLI + hot reload" (James), "needs another MCP?" confusion (Christian), and the friend's gap list: *in-memory C#, keyboard, UI raycast, Scene View screenshots, full runtime QA, profiler, only 14 of 120 visible*.

Only keyboard, UI raycast and runtime QA map to verify. The plan picked the part of the evidence that is most contested (see §3) and least observed.

**Proposed change (§2 / G4):** Replace the single-flagship framing with a **two-step wedge**. Step 1, flagship for weeks 2–5, is **"Live truth"**: `nexus.snapshot` (selection/path → hierarchy, RectTransform, components, serialized refs, active state, import state, cropped Game/Scene View image in one call, the report-3 spec) + `nexus.compile` + `nexus.run` (menu item/editor method with arguments, dialog interception via `EditorUtility.DisplayDialog`/folder-panel overrides, and waiting for import/compile). Step 2 is `nexus.verify`, built only if Gate B′ (below) shows it has room. The headline demo becomes "the agent stopped asking you for screenshots". That claim is reproducible against report 3.

### 1.2 [MAJOR] "Unity does the hands, Nexus the eyes and brain" is not a defensible split
Unity already ships eyes: `screenshot`, `capture_game_view`, `get_scene_hierarchy` and `get_component_properties` (F3). Its launch material shows agents "entering Play Mode, inspecting runtime results, capturing screenshots" and a Pi harness demo where the agent "tests it in Play Mode … and catches a real bug" ([gamedev.net Unity CLI news](https://gamedev.net/news/5423/), [runtimewire](https://runtimewire.com/article/unity-ships-a-cli-that-lets-ai-agents-operate-running-game-projects), [Unity walkthrough](https://unity.com/resources/unity-pipeline-cli-technical-walkthrough)). "Brain" belongs to the model and to skills, and Unity has 31 of those. What Nexus provably has that Unity lacks is narrow: Overlay-correct capture, UI Toolkit/UGUI automation, epoch-correct readiness and semantic indexes (find_references, scene_delta).
**Change (§2):** Drop the metaphor. Position on a concrete, checkable claim: *"Nexus returns what the Editor actually shows and compiled, in one call and under 1k tokens, including Overlay UI."* The metaphor invites the reply "Unity already has screenshots".

### 1.3 Steelmanned alternative strategies

| # | Alternative | Why it could beat the plan | Evidence that would decide |
|---|---|---|---|
| A1 | **Merge/contribute**: offer Capture V2 (Overlay-correct) + readiness epochs upstream to Unity Pipeline and as a module to IvanMurzak/Unity-MCP (~3k stars, Apache-2.0, "any C# method becomes a tool" ([enterprisedna](https://enterprisedna.co/directories/mcp/ivanmurzak-unity-mcp))) or to uLoopMCP | Two people cannot out-distribute Ivan, Coplay or Unity. Contribution gives instant reach and reputation (hireability, consulting) at near-zero maintenance. The plan's own fallback (§9) is half of this, but only after failing. | One email/issue to each maintainer in week 1. Measure acceptance and interest. Compare their weekly installs with Nexus's (currently unknown; see 5.3). |
| A2 | **Skills-and-recipes product**: the best Unity agent skill pack (verification, debugging and context recipes over `unity command`/`eval`), with ≤5 tiny `[CliCommand]`s | It tests the adoption hypothesis in days, not weeks. It rides the "adopt the brain, defer the bridge" mood. Skills are where agents actually choose tools now. | skills.sh install counts and agent chose-rate after 2 weeks vs. akiojin/uLoop skills. |
| A3 | **Editor-tool automation for tools programmers**: run menu items/editor methods with args, intercept modal dialogs and file pickers, drive EditorWindows/UI Toolkit editor panels, wait on import/bake | This is exactly report 4's failure, which computer-use cannot do reliably. No competitor sells it as the primary job (they sell Play Mode). Nexus already has UI Toolkit automation. The owner is himself a tools programmer. | Count, across 10 more real sessions, how often the agent needed to run existing editor tooling vs. play-test. |
| A4 | **Token-lean proxy over `unity mcp`/Pipeline**: compaction, handles, deltas and deferred profiles in front of Unity's ~151 commands | Tokens were the #1 public question (LinkedIn), and Unity AI users report burning a month of credits in a weekend. The value is measurable (tokens/task) and the product is small. | Benchmark v2 token column: if raw `unity command` output is already small, A4 is dead. If it is 3–5× larger on hierarchy/console, A4 is a real wedge. |
| A5 | **Studio policy/audit layer**: allowlist, audit log and confirm gates *in front of Pipeline commands including `eval`* (a Pipeline-side hook, not Nexus's own server) | This turns the "security depth" asset into something studios pay for, and it is the only credible monetization path that does not compete with free tools. | Five studio conversations. Whether Pipeline exposes a pre-dispatch hook (P0-10 should check). If it cannot gate `eval`, A5 is dead. |
| A6 | Runtime/player-build QA in CI | Enterprise budgets exist. | AltTester (CLI + AI skills + MCP, on builds, paid plans) and GameDriver (MCP Test Assistant) already own this ([AltTester CLI](https://alttester.com/alttester-cli-built-for-the-way-ai-assisted-testing-actually-works/), [GameDriver](https://www2.gamedriver.io/blog/gamedriver-test-assistant)). Not a fit for 2 people. Keep it as backlog, as the plan does. |

**Change (§9, §10):** Add G9, *"Week-1 outreach for A1 (upstream Capture/readiness) runs in parallel, not as a fallback."* Add A3 and A4 as explicit Gate-A alternatives, so a failed gate has a better destination than "maintenance".

---

## 2. User value: who is this for?

### 2.1 [MAJOR] No target persona (whole plan)
The plan never names a user. The feedback comes from three different people. (a) A solo/contract gameplay programmer with a working stack (Ivan MCP + Unity CLI + hot reload + Claude Code) who asks "why switch?". (b) A technical artist asking about speed and tokens vs. Coplay. (c) A lead programmer confused about what the product even is ("isn't an MCP but needs another MCP?"). The actual evidence-producing user is **the owner on his own mid-size production project** (reports 1–4: Addressables, rewards, canvas, texture packing).
**Change (new §2.1):** *"Primary user: a Unity programmer using Claude Code/Codex on an existing production project (not greenfield scene-building), on Windows or macOS. Secondary: tools programmers. Not targeted: QA teams (AltTester/GameDriver), no-code creators (Unity AI)."* Every benchmark task must come from this persona's real sessions, not a synthetic fixture alone.

### 2.2 [MAJOR] Explicit asks dropped or hidden (§5.1, §8)
- **"Only 14 of 120 visible"**: the plan answers by making the default surface *smaller* (≤8) and hiding about 64 methods. That is right for tokens, but it repeats the complaint unless the capability map is discoverable. **Change:** add `nexus.capabilities` (one call, about 300 tokens, grouped by job with profile names) to `core`, and list the profiles in `initialize.instructions`.
- **In-memory C# (execute_code)**: G3 "use `unity eval`" is correct *only when Pipeline is installed*. Pipeline is experimental and Unity 6.0+ only, and Nexus keeps a legacy path precisely for users without it. The friend's #1 ask is then unmet for exactly those users. **Change G3:** no own Roslyn, but ship a thin `nexus.eval` that forwards to Pipeline eval when present and returns a clear `PIPELINE_REQUIRED` + install hint otherwise. Document this in the comparison table so "no execute_code" is not read as a missing feature.
- **Scene View screenshots**: T24 was shrunk, but the ask is cheap, and report 3 needed it. **Change:** include a Scene View framed-on-object capture in `nexus.look` (S-07).
- **Profiler**: the drop is justified. A LinkedIn commenter already says Unity CLI "can also profile". Keep it dropped.
- **Hot reload**: users pair agents with hot reload. `nexus.compile` should detect Hot Reload/FastScriptReload and report which one applied. Otherwise its compile verdict can be wrong in these users' setups. Add this to S-05.

---

## 3. Competitive realism: does `nexus.verify` have room?

### 3.1 [CRITICAL] The verify niche is already occupied by at least five players; the plan checks one (F5, P0-10, R3, Gate B)
- **uLoopMCP / unity-cli-loop**: CLI record/replay of keyboard/mouse via Input System, `execute-dynamic-code`, compile/log tools, on OpenUPM and skills.sh ([skills.sh record](https://www.skills.sh/hatayama/unity-cli-loop/uloop-record-input), [replay](https://www.skills.sh/hatayama/unity-cli-loop/uloop-replay-input), [OpenUPM](https://openupm.com/packages/io.github.hatayama.uloopmcp/)).
- **akiojin/unity-cli**: a `unity-playmode-testing` skill: "simulate keyboard, mouse, gamepad, or touch input, capture a screenshot or video", UI automation, visual verification, plus `simulate-mouse-ui` raycast PointerDown/Up/Click ([skills.sh](https://www.skills.sh/akiojin/unity-cli/unity-playmode-testing)). This already covers V-03, V-04 and most of V-06.
- **Funplay**: a 34-tool **core profile** centred on execute_code, play control, input sim, screenshots and perf ([glama](https://glama.ai/mcp/servers/FunplayAI/funplay-unity-mcp)). Funplay already did the "small default profile" idea the plan presents as novel.
- **AltTester CLI + AI Skills + MCP** on builds, including Unreal ([AltTester](https://alttester.com/alttester-at-gamescom-2026-ai-assisted-testing-for-game-dev-and-whats-next/)).
- **Unity's own launch demo**: an agent play-tests and catches a bug with Unity CLI alone ([gamedev.net](https://gamedev.net/news/5423/)).
- Other engines have converged on the same idea: Ziva's Godot playtest agent gives a "pass or fail verdict plus a saved video" ([ziva.sh](https://ziva.sh/blogs/ai-agent-playtest-godot-game)), and Open Godot MCP/Beckett do the same ([Godot forum](https://forum.godotengine.org/t/open-godot-mcp-ai-plays-tests-your-game-not-just-edits-files/141986)). nunu.ai (YC) covers the autonomous-agent QA end ([YC](https://www.ycombinator.com/launches/Jzd-nunu-ai-ai-agents-to-play-and-test-games)).

"The agent says it's fixed. Nexus proves it." is therefore not a unique headline. It is the category's default demo. The remaining defensible slice is narrow: **(i) Overlay-correct capture, (ii) scenario files that persist as regression tests, (iii) truthful readiness, so there is no false-pass after reload.** Only (i) is proven, and only on Metal.
**Change (P0-10):** widen the spike to uLoopMCP, akiojin/unity-cli, Funplay core, Ivan and Unity CLI alone, each run on the fixture's Overlay-toggle bug. Record which of them *can* detect the planted bug. **If two or more detect it, `nexus.verify` must not be the flagship.** Build only the missing piece (Overlay capture as a `[CliCommand]` others can call, per A1).

### 3.2 [MAJOR] Gate B compares against a strawman
"Nexus v2 + Unity CLI vs Unity CLI + official skills alone" is an unfair test. Official skills have no input simulation (F4), so Nexus wins input tasks by construction. **Change:** Gate B must include the **best available verify competitor** from P0-10 (probably akiojin or uLoop). "wins" must hold against that competitor, not only against Unity alone.

---

## 4. The gates and timeline

### 4.1 [CRITICAL] Gate A cannot fail cleanly (§7 Gate A, §11)
- "≥2× the S3 baseline": the baseline is about 0 (0 calls in 3 of 4 sessions). Two times zero is met by a single call.
- "clear majority of relevant runs": the threshold is undefined, and "relevant" is decided after the fact.
- N≥3 per cell, 4–5 tasks and 2 agents: a 2/3 vs 1/3 difference is noise.
- The team writes the tasks, the fixture, the skill *and* the prompts. That is self-grading, which contradicts rule §6.5. Prompts can drift toward words that trigger the skill.
- There are no external users anywhere in the gate.

**Change:** pre-register in the repo before week 1: absolute threshold (Nexus chosen in ≥60% of runs where Unity state is needed), N≥10 per cell, prompts frozen and written by someone else (the partner or a different agent), and at least 2 tasks taken from a *third-party* open-source Unity project. Add an external gate criterion: **≥5 non-team users ran the skill on their own project and ≥3 reported it used**, gathered via the LinkedIn/Telegram contacts who already engaged (James, Abdullah, the friend).

### 4.2 [MAJOR] Gate B's "OR" and fallback make the gate advisory
"semantic success **or** tokens-per-success" lets a 5% token win pass a verify product that finds no more bugs. **Change:** require semantic success ≥ best competitor *and* false-success ≤ it; tokens act as a tiebreaker only. State N (≥10 per task) and the effect size (≥15 pp success or ≥30% tokens) in advance.

### 4.3 [MAJOR] 12 weeks is too long before market contact, and too long for the capacity
About 100 agent-days of work reviewed by 2 humans, one of whom does the partner's day job. The previous "1–2 day" T01 became +12.4k lines (B §0). Meanwhile Unity CLI went from beta.4 to beta.11 in a few weeks, so the ground shifts monthly. The new positioning first reaches the public at week 8 (1.8.0-preview), and the demo, skills listing and comparison arrive only in weeks 10–12.
**Change:** keep the gates, compress the public loop. Week 2: release 1.7.0 + skill on skills.sh/Claude plugin (move R-02 into S-01). Week 3: short demo of *live truth* (report-3 replay). Week 5: public call for 5 testers. Drop Phase 3 C-03/C-05 and Phase 4 A-03 from the 12-week scope. Put W-01 Windows *before* Gate B, because most of the audience is on Windows and the verify evidence is Metal-only (F10).

### 4.4 [MINOR] Drops that hurt adoption
Installers (T12) are fine to freeze, but first-run friction killed trust (C §1.2 #6). Keep a single `nexus doctor` command in S-01 ("is it up, which transport, which profile"). Tool breadth (T10) was rightly reversed.

---

## 5. Go-to-market

### 5.1 [MAJOR] The name is invisible
"Nexus" collides with Nexus Mods, Sonatype Nexus, Google Nexus and many "Nexus MCP" projects, so search and skills.sh discovery will be poor. Christian's comment shows the product is not understood even when seen. **Change (§2):** keep the package id, but give the skill and CLI a descriptive, searchable name (e.g. `unity-live-truth` / "Nexus: Live Editor Truth for Unity agents"). Test it by searching skills.sh for "unity screenshot overlay" and "unity verify": Nexus must appear.

### 5.2 [MAJOR] Answer the competitor question people actually asked
James asked for a comparison with "Ivan MCP + Unity CLI + hot reload", and Abdullah asked for one with Coplay. The plan's comparison set is Unity CLI and uLoop; Ivan and Coplay are absent from every gate and from R-03. **Change:** R-03 COMPARISON.md must include Ivan, Coplay, Funplay core and akiojin, with scenario oracles. It must also state honestly where each wins (e.g. Funplay for breadth and execute_code).

### 5.3 [MAJOR] No usage measurement at all
With no install data, "adoption" is only the team's own agent runs. **Change:** add OpenUPM download stats (R-01 moves to week 2), GitHub traffic, skills.sh counts, and an opt-in, local-only `nexus.stats` export that users can paste. Report these at each gate.

### 5.4 [MINOR] Monetization
"Nexus CI" would compete with AltTester and GameDriver, which have paid plans, SDKs and sales teams. If revenue matters, A5 (studio policy/audit over Pipeline incl. `eval`) and consulting are the only non-crowded paths. **Change G8:** name A5 as the monetization spike candidate, validated by 5 studio conversations. Remove "Nexus CI" as the default.

### 5.5 [MINOR] Demo
The demo should replay a *real* failure (report 3: the user had to send screenshots; report 4: computer-use failed on a folder dialog) side by side with Nexus. That is more credible than a planted toggle bug that every competitor's demo also shows.

---

## 6. What the plan got right
- It diagnoses the adoption problem correctly: agents do not call Nexus, and discoverability (instructions, skills, task names) comes before features. P0-07 and S-01 are the highest-value tasks in the plan.
- Evidence discipline: facts with links and expiry, no self-acceptance, scope caps, stop rules, void v1 benchmarks, semantic success as the headline metric.
- It reverses the tool-count race, freezes the transports, pins Pipeline versions, and does not rebuild `execute_code`.
- It repositions security honestly given `unity eval` (R5).
- It treats the fallback as a legitimate outcome (§9), and Capture-as-a-library is a real asset.
- Phase 0 fixes (wake-up, truthful readiness, `isError`, D-8) are necessary under every alternative strategy above, so they are no-regret.

## 7. Summary of proposed edits
1. §2/G4: flagship becomes "Live truth" (snapshot + compile + run-with-dialogs), and verify becomes conditional.
2. P0-10: 5-competitor bug-detection spike. Verify is not the flagship if ≥2 detect the planted bug.
3. Gate A/B: pre-registered absolute thresholds, N≥10, externally written prompts, third-party tasks, external-user criterion, best competitor in the comparison, AND instead of OR.
4. New §2.1 persona. Add `nexus.capabilities`, `nexus.eval` forwarder, Scene View capture and hot-reload awareness.
5. Week 1 upstream/merge outreach (A1) in parallel. A3/A4 become named gate-failure destinations.
6. GTM: skills listing and 1.7.0 by week 2, demo by week 3, Windows before Gate B, searchable descriptor name, Ivan/Coplay in COMPARISON, usage stats, A5 as monetization spike.
