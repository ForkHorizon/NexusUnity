# U2 — What Unity is likely to ship next (6–12 months) for agents, CLI, QA and runtime

Analyst: Unity Research 2 of 2 · Date: 2026-09-30 · Scope: Unity CLI / `com.unity.pipeline` / Unity AI / official skills and plugin, for Nexus Unity planning.

**How the evidence is labelled**
- **CONFIRMED**: Unity shipped it or officially stated it, with a source.
- **STRONG SIGNAL**: engineering activity from Unity staff (commits, draft PRs, internal repository references) or a staff forum reply that is not a formal commitment.
- **SPECULATION**: my own inference.

**Source limits:** unity.com, docs.unity.com, docs.unity3d.com and discussions.unity.com were only reachable through search snippets. GitHub was cloned or fetched directly: `Unity-Technologies/skills` had 124 commits, and I read the full `git log` plus the contents of PR #80. I also read `Unity-Technologies/unity-agent-plugin` and draft PRs #72 and #51.

---

## 1. Timeline of Unity's agent and CLI moves in 2026

| Date | Event | Source |
|---|---|---|
| 2026-02 | CEO says an upcoming Unity AI beta will let people "prompt full casual games into existence". | https://www.gamingonlinux.com/2026/02/unity-ceo-says-an-upcoming-beta-will-allow-people-to-prompt-full-casual-games-into-existence/ |
| 2026-04-04 | First public Unity CLI beta (0.1.0-beta.1). | https://docs.unity.com/en-us/unity-cli/release-notes (search snippet) |
| 2026-05-04 | Unity AI open beta ships with Ask/Plan/Agent modes, an in-Editor MCP server and an AI Gateway (BYO Claude/GPT keys, no Unity credits consumed). Personal plan: $10/month per 1,000 credits. | https://app.cinevva.com/news/2026-05-04-unity-ai-open-beta · https://discussions.unity.com/t/unity-ai-s-open-beta-now-live-for-unity-6/1718560 |
| 2026-05-05 | `Unity-Technologies/skills` repository created, starting with the `unity-cli` skill. | git log of github.com/Unity-Technologies/skills (commit #1) |
| 2026-07-20/21 | Unite Seoul: Unity 7 roadmap announced ("creators, teams and coding agents"). The CLI and Pipeline are publicly announced, along with a "free-to-use MCP" and a public API. Unity 7 early beta is set for **December 2026** and GA for **Q1 2027**. | https://www.businesswire.com/news/home/20260720250213/en/Unity-7-Roadmap-Revealed-At-Unite-Seoul · https://unity.com/blog/unite-seoul-keynote-2026-recap · https://www.gamedeveloper.com/programming/unity-unveils-unity-7-roadmap-with-update-path-that-won-t-break-your-build |
| 2026-08-06 | Official `unity-agent-plugin` (Claude Code + Codex) published. It contains skills only: no MCP server, no hooks. | github.com/Unity-Technologies/unity-agent-plugin (git log, `.claude-plugin/plugin.json`) |
| 2026-08-06 | Q2 earnings: "best quarter" ($546.5M). Unity 7 is framed as "rebuilt for… coding agents" with "free MCP, CLI, and API built in". | https://www.businesswire.com/news/home/20260806640439/en/ · https://www.fool.com/earnings/call-transcripts/2026/08/13/unity-u-q2-2026-earnings-call-transcript/ |
| 2026-08-12/13 | CLI 1.0.0-beta.4 and beta.5 released. Player builds no longer need project C#. | https://discussions.unity.com/t/unity-cli-1-0-0-beta-4-is-rolling-out/1733720 |
| 2026-08-14 | The plugin withdraws its `setup-game-inputs` skill (PR #24). | plugin git log |
| ~2026-08 | The AI Assistant in-Editor MCP server is **deprecated**; `unity mcp` replaces it. Unity recommends `unity command`/`unity eval` directly because they are "faster and use fewer tokens". | https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli |
| 2026-08-19 | CLI beta.6 released: `test --shard`, `doctor --ci`, `--format github`. Pipeline 0.5.0-exp.1 released around the same time. | skills CHANGELOG; https://discussions.unity.com/t/unity-pipeline-package-version-0-5-0-exp-1-is-rolling-out/1734288 |
| 2026-08-25 → 09-01 | CLI beta.7 is pulled and replaced by beta.8. Beta.8 adds a `unity vcs` family (conflicts/explain/resolve/summarize/**affected**), `unity plugin`, and `skill install --local`, which mirrors the skill that ships inside the project's Pipeline package. | skills `unity-cli/CHANGELOG.md` |
| ~2026-09-03 | Pipeline 0.6.0-exp.1: `run_script` (in-memory .cs, no domain reload), hot reload on IL2CPP via an IL interpreter, command batching. | https://discussions.unity.com/t/unity-pipeline-package-0-6-0-exp-1-is-available-now/1735626 |
| 2026-09-08 | CLI beta.9: `unity test --affected`, `skill show`, and an auth broker. | skills CHANGELOG |
| 2026-09-14 | CLI beta.10: `unity watch test`, `unity commands --format json` (machine manifest for agents), `unity job` (detach/wait), `build run`, and a **desktop-screenshot fallback** for `capture_game_view`/`capture_scene_view`. | https://discussions.unity.com/t/unity-cli-1-0-0-beta-10-is-rolling-out/1736729 · skills CHANGELOG |
| ~2026-09-15 | Pipeline 0.7.0-exp.1: runtime `CommandRegistry.RegisterCommand`, `console_status` with a compile-state "groundTruth", server-side waiting, and Pipeline excluded from release builds. | https://discussions.unity.com/t/unity-pipeline-package-0-7-0-exp-1-is-available-now/1736536 |
| 2026-09-15 | **Draft PRs by Unity staff: `polyspatial-playtest` skill + `recording-analyst` sub-agent.** The skill records Play sessions, drives the game with simulated input, and queries per-frame scene state. | https://github.com/Unity-Technologies/skills/pull/72 · https://github.com/Unity-Technologies/unity-agent-plugin/pull/51 |
| 2026-09-21 | The `new-unity-project` skill gains a "Visual baseline" step that checks a `unity command screenshot`. | skills commit 2ac0232 (#76) |
| 2026-09-22 | CLI 1.0.0-beta.11: `unity recompile` (quick compile check, errors with file and line, exit 6), `unity docs <topic>` matched to the project's version, `.unitypackage` export/import, and `unity mcp configure --server issue-tracker`. | https://discussions.unity.com/t/unity-cli-1-0-0-beta-11-is-rolling-out/1737353 · PR #80 diff |
| ~2026-09-26/29 | Pipeline 0.8.0-exp.1: loopback-only server, `Unity.Pipeline.Attributes` split out (commands can be declared without the full package), public `CommandRegistry`, and a license change to Unity ToS. Six breaking changes. | https://discussions.unity.com/t/unity-pipeline-package-0-8-0-exp-1-is-available-now/1737832 |
| 2026-09-28/29 | Skills are now auto-published from an internal `unity/skills` repository, and the public repository states that PRs are not accepted (#96). A `project-auditor-fixes` skill is added that uses the `unity command audit` / `audit_status` commands. | skills git log and PR list |

**Cadence (CONFIRMED):** a CLI beta about every 7–10 days and a Pipeline minor about every 2 weeks, each with breaking changes. The skill text still says "until GA ships", so there is no GA date yet (PR #80 diff). **SPECULATION:** CLI 1.0 GA and a non-experimental Pipeline will ship with or just before the Unity 7 early beta (December 2026) or Unity 7 GA (Q1 2027). I estimate 60% that CLI GA lands by 2027-03 and 35% that Pipeline leaves `-exp` by then.

---

## 2. Capability-by-capability assessment

### 2.1 Input simulation (keyboard/mouse/touch in Play Mode or Player)
- **Evidence:**
  - STRONG SIGNAL: Unity staff draft PR #72/#51, "drives the game with simulated input". It depends on `com.unity.polyspatial.annotation` and PolySpatial recording commands, and falls back to `editor_play` + `capture_game_view`. The scope is PolySpatial/XR-flavoured and still a draft.
  - The plugin withdrew `setup-game-inputs` (#24, 2026-08-14), which suggests input is not yet a polished area internally.
  - Not verified: one search-engine summary claimed RuntimeOnly `simulate_pointer`/`simulate_key` Pipeline commands exist. A targeted search found no documentation, so treat this as unconfirmed.
  - No input command appears in the skill's command lists or the Pipeline release summaries (0.5–0.8).
  - Community demand is visible: Coplay issue #1408 (2026-09-21), and uLoopMCP already ships `simulate-keyboard`/`simulate-mouse-ui`/`replay-input` (https://github.com/hatayama/unity-cli-loop).
- **Likelihood Unity ships generic input commands:** 6 months **medium** (40%), 12 months **medium-high** (60%). The PolySpatial work shows the capability exists internally, but it has not been generalized. Input System `InputTestFixture` makes the command cheap for Unity to add.
- **Recommendation: WRAP NOW, design for replacement.** Ship `nexus.input` (V-03) behind an adapter that can later call a Unity `simulate_*` command. Value lives in UI-aware targeting and assertions, not raw key injection.

### 2.2 UI automation (UGUI / UI Toolkit)
- **Evidence:**
  - CONFIRMED: the UI Toolkit Test Framework has been a core package since 6.3 (https://discussions.unity.com/t/ui-toolkit-test-framework-is-available-in-6-3/1698228). It is a test-author API, not an agent command.
  - Official `ui`, `ui-ugui`, `ui-uitk` and `ui-imgui` skills (2026-08-07) cover *authoring* guidance only. The `ui` skill asks the user to "describe or screenshot the screen", so it has no introspection.
  - No Pipeline UI query/click command appears in the evidence I found.
- **Likelihood:** 6 months **low** (20%), 12 months **medium** (35%). **SPECULATION:** if it appears, it will likely be a thin `ui_query` over UITK Test Framework fixtures rather than UGUI EventSystem raycasting.
- **Recommendation: BUILD.** UI-aware targeting across UGUI and UITK, with explicit `NO_EVENTSYSTEM` errors, is low-overlap and high-value. Keep it a composable primitive under `nexus.verify`.

### 2.3 Game-view / overlay capture
- **Evidence:**
  - CONFIRMED: `screenshot` (PNG to file, game/scene, width/height) and `capture_game_view`/`capture_scene_view` exist. Beta.10 added a whole-desktop OS fallback when the main thread is blocked (skills `integration-advanced.md`).
  - Unity is actively iterating here, including handling blocked main threads. Staff also say "further work on the horizon to allow the package to control the blocking dialogs, but it requires an Editor change" (https://discussions.unity.com/t/announcing-the-unity-cli-a-new-way-to-connect-your-tools-and-agents/1731104?page=2).
  - Screen Space Overlay being missing from Unity's capture is Nexus's own finding; no Unity statement addresses it.
- **Likelihood Unity fixes overlay and adds JPEG/downscale/crop:** 6 months **medium** (45%), 12 months **high** (70%). Capture quality is cheap, visible and already on their bug path.
- **Recommendation: INTEGRATE WHEN SHIPPED plus a short-lived WRAP NOW.** Keep Capture V2 as the default only while it measurably beats Unity (overlay, JPEG, stall). Add the W-03 self-test that compares against Unity's capture. Do not invest in new raw-capture features. Invest in *crop to object/element, numbered marks, and token budget*, which Unity has not signalled.

### 2.4 Visual verification / asserts
- **Evidence:**
  - STRONG SIGNAL (skill-level): `new-unity-project` Step 6 "Visual baseline" asks the agent to check that `unity command screenshot --output baseline.png` "looks lit and tonemapped". This is LLM eyeballing, not pixel asserts.
  - PR #72/#51 pushes a different philosophy: answer "from recorded per-frame scene state *instead of a screenshot*" via `eval_file` SceneStateQuery.
  - No assert or diff command has been announced.
- **Likelihood of a pixel/region assert command:** 6 months **low** (15%), 12 months **low-medium** (25%). A *state-recording* analysis path (PolySpatial-style) is more likely: 12 months **medium** (40%).
- **Recommendation: BUILD.** State-based asserts (property, log, UI visible/text) come first; pixel region with tolerance comes second. Borrow Unity's idea of asserting on recorded state rather than screenshots. Keep assertion output tiny (≤400 tokens).

### 2.5 QA scenario runner (play → input → wait → assert → report)
- **Evidence:**
  - CONFIRMED building blocks: `unity test` with `--affected`/`--shard`/`--rerun-failed`/junit, `unity watch test`, `unity command --detach` + `unity job wait`, `editor_play/stop`, `set_timescale`/`runtime_status`/`log` in dev Players (project-auditor skill), and `run_script`.
  - Unity's direction is "tests as the scenario format" (Unity Test Framework) plus CI ergonomics.
  - The polyspatial-playtest draft is the closest thing to an official agent playtest loop. It is a skill plus a sub-agent composed from primitives, not a runner command.
  - AltTester launched an MCP server, CLI and AI Skills (https://alttester.com/alttester-at-gamescom-2026-ai-assisted-testing-for-game-dev-and-whats-next/), so the third-party QA space is also moving.
- **Likelihood Unity ships a declarative scenario runner:** 6 months **low** (15%), 12 months **low-medium** (30%). Unity's pattern is primitives plus skills (see §3).
- **Recommendation: BUILD, as the flagship, but emit NUnit/JUnit** so results plug into `unity test` reports and CI. Also offer an "export scenario as a PlayMode test" path, so Nexus rides Unity's test infrastructure rather than competing with it.

### 2.6 Profiler / performance triage
- **Evidence:**
  - STRONG SIGNAL: in the CLI announcement thread, a staff reply is summarized in search snippets as "planning on adding integrations with the profiler and frame debugger before the CLI goes GA" (https://discussions.unity.com/t/announcing-the-unity-cli-a-new-way-to-connect-your-tools-and-agents/1731104; exact wording not directly verified).
  - CONFIRMED: the AI Assistant already has "Ask Assistant" on Profiler samples (https://docs.unity3d.com/Packages/com.unity.ai.assistant@1.6/manual/use-profiler.html), and Unity 7 includes a new 2D profiler (keynote recap).
  - The shared context lists a `profiler` command in Pipeline 0.7; its depth is unknown.
- **Likelihood:** 6 months **medium-high** (55%, tied to GA), 12 months **high** (75%).
- **Recommendation: AVOID raw profiler capture; INTEGRATE WHEN SHIPPED.** Keep `nexus.perf` in the backlog, as the plan already does. The only thing worth considering later is a perf *assert* inside `nexus.verify` (e.g. "p95 frame < 16.6 ms during the scenario") built on `ProfilerRecorder`.

### 2.7 Context / project understanding
- **Evidence:**
  - CONFIRMED: `unity vcs affected` (GUID impact graph: asset → prefabs → scenes → asmdefs → tests), `vcs summarize`/`explain`, `unity docs <topic>` matched to the project's version, `unity commands --format json` manifest, `console_status` with a groundTruth compile state, `find_assets`, `get_scene_hierarchy`, and the `generate-editor-search-query` skill (Search API queries).
  - `unity mcp configure --server <id>` with an `issue-tracker` server shows Unity is building a *family* of MCP servers.
- **Likelihood of deeper context tools (reverse references, task-scoped packs):** 6 months **medium** (40%). A reverse-reference index is almost free for them because `vcs affected` already computes a GUID graph. 12 months **medium-high** (60%).
- **Recommendation: split.**
  - `nexus.find_references` → **INTEGRATE/WRAP**: reuse `unity vcs affected --json` where possible and add only the semantic layer (serialized field names, Addressables, component-to-script mapping).
  - `nexus.context` token-budgeted packs → **BUILD**. Unity has shown no interest in token budgeting or task scoping.

### 2.8 Compile diagnostics
- **Evidence:** CONFIRMED in beta.11: `unity recompile` triggers, polls and lists each error with file and line, with exit codes 0/6/7 and `--strict`. Pipeline 0.7 `console_status` carries a groundTruth compile state. Skills note that Safe Mode kills Pipeline connectivity, which is still an open gap.
- **Likelihood of further improvement:** already shipped. Grouping and "did you mean" hints: 12 months **medium** (40%).
- **Recommendation: AVOID competing on basic compile.** Make `nexus.compile` a thin wrapper that calls `unity recompile` when available. Add only (a) error grouping and hints and (b) **Safe-Mode-resilient diagnostics**, since Nexus can read `Editor.log` and asmdefs without the Pipeline. That is a real, documented Unity gap.

### 2.9 Runtime / player-build control
- **Evidence:** CONFIRMED:
  - `--runtime`/`--runtime-path` targeting of dev Players.
  - Hot reload, including IL2CPP via the IL interpreter (0.6), and runtime `RegisterCommand` (0.7/0.8).
  - `unity build run`, Build Profiles, and a provenance manifest.
  - `RuntimeOnly` commands, and Pipeline stripped from release builds unless `ENABLE_RUNTIME_PIPELINE` is set.
  - Unity 7 "Live Code Reload" (https://www.invenglobal.com/articles/24003/unity-engine-7-changing-the-development-paradigm-and-the-roadmap-ahead).
- **Likelihood of further expansion:** 6 months **high** (80%). This is Unity's fastest-moving area.
- **Recommendation: AVOID own player transport; INTEGRATE.** `nexus.player_check` should become "run the same Nexus scenario over `unity command --runtime`" by registering Nexus's verify steps as `RuntimeOnly` `[CliCommand]`s through the new `Unity.Pipeline.Attributes` assembly.

### 2.10 Skills / agent plugin
- **Evidence:** CONFIRMED:
  - Official plugin 0.1.6-beta. It is **skills only**, with no MCP or hooks, and is published from the internal `unity/skills` repository; the public repository now refuses PRs (#96).
  - More than 33 skills, mostly domain skills written by product teams (URP, 2D, audio, IAP, LevelPlay, Vivox, localization).
  - `unity skill install --local` mirrors the skill **shipped inside the project's Pipeline package** (beta.8).
  - `--caller`/`--skill` analytics labels "exist so an integration can identify itself" (PR #80).
- **Likelihood Unity adds QA/verification skills:** 6 months **high** (70%). The playtest skill is already in draft. A third-party skill registry via `unity plugin` is possible: 12 months **medium** (40%).
- **Recommendation: INTEGRATE.** Distribute Nexus as a skill plus a Claude Code/Codex plugin that *composes* with Unity's `unity-cli` skill rather than duplicating it. Never publish a Nexus copy of Unity's domain skills.

### 2.11 Security / policy
- **Evidence:** CONFIRMED:
  - Pipeline 0.8 binds to loopback only, with per-instance auth tokens.
  - `unity mcp` dropped `--instance host:port` for that reason.
  - A hardware-sealed auth broker, peer code-signature checks, and Pipeline removed from release builds.
  - Skills must pass an internal "skills-gate SEC_POWER_CAP check" that requires an "Accepted risks" section in `SECURITY.md` for `eval` usage (commit 2ac0232).
  - Guidance that sandboxed agents may falsely see "no Editor", and must never be told to disable the sandbox.
  - The AI Assistant permission prompts in Agent mode.
- **Likelihood of policy features (per-command allow/deny, audit):** 6 months **medium** (45%), 12 months **medium-high** (60%).
- **Recommendation: INTEGRATE / align.** Keep Nexus's confirm gates and path sandbox, but express them in Unity's vocabulary: a `SECURITY.md` with accepted risks per skill, `RuntimeOnly`, and dev-only. Do not build a competing auth system.

---

## 3. Pattern: how Unity is shipping

**CONFIRMED pattern:** Unity ships **atomic primitives** (commands) plus **CI ergonomics** (exit codes, JSON/NDJSON, jobs, shards, affected tests) plus **domain skills from product teams**. Composition (multi-step agent workflows) is left to skills and sub-agents (the playtest draft; the project-auditor "trigger, poll, read" recipe).

**SPECULATION:** Unity will not ship an opinionated, token-budgeted "verify/diagnose/context" composite layer within 12 months. Its incentives are breadth, stability and GA before Unity 7. That leaves a window for Nexus as a composition and verification layer. It is also why every Nexus composite must be *portable onto Unity primitives*, so it does not break when Unity ships a primitive Nexus already has.

**Absorption risk from community tools:**
- uLoopMCP's input/replay/record-video set is the most likely feature bundle for Unity to absorb, matching the PolySpatial recording direction.
- akiojin/unity-cli is copying Unity's capture fallback (https://github.com/akiojin/unity-cli/issues/369), so the ecosystem is converging on Unity's primitives.
- Coplay was acquired by Ramen/Aura (https://coplay.dev/blog/whats-next-for-coplay), so the largest open-source MCP is now tied to a commercial assistant.

---

## 4. Explicit lists

### Do NOT build (Unity will likely ship or already has)
1. Raw transport and CRUD over GameObjects/components/assets/scenes. Pipeline has ~150–164 built-ins (CONFIRMED).
2. Basic compile check and error listing: `unity recompile` (CONFIRMED beta.11).
3. Test running, affected-test selection, sharding, watch mode, JUnit output (CONFIRMED).
4. Player build, run, hot reload and runtime connection (CONFIRMED; the fastest-moving area).
5. Profiler/frame-debugger capture commands (STRONG SIGNAL: "before GA").
6. MCP client installers/configurators: `unity mcp configure` supports 16 clients (CONFIRMED). Keep Nexus installers minimal or delegate to Unity.
7. Generic Roslyn execute_code: `eval`/`eval_file`/`run_script` (CONFIRMED).
8. Domain skills (URP, UI authoring, 2D, audio, IAP). Unity product teams own these (CONFIRMED).
9. New raw-capture features beyond what Capture V2 already does (MEDIUM-HIGH risk; see 2.3).
10. A GUID-dependency / impact graph: `unity vcs affected` (CONFIRMED).

### Safe to build (low Unity overlap within 12 months)
1. The `nexus.verify` declarative scenario runner with assertions, capture-on-fail and a ≤400-token report. Emit NUnit/JUnit and support "export as PlayMode test".
2. UI-aware targeting and querying across UGUI and UI Toolkit (EventSystem raycast, visibility, text, clickability), including a multi-resolution `ui_check`.
3. State and pixel assertions (property/log/UI/region color with tolerance), plus perf-budget assertions inside a scenario.
4. Token-budgeted, task-scoped context packs (`nexus.context`) and handle-based payloads (`nexus.get`, crop/range).
5. Safe-Mode-resilient diagnosis (`nexus.diagnose` reading `Editor.log`/asmdefs when Pipeline cannot load). Unity documents this gap itself.
6. Truthful readiness with compile/reload epochs, where Nexus's false-ready fix is a differentiator. Also wrap Unity's `console_status` groundTruth.
7. Semantic find and scene diffs (`scene_delta`, `semantic_find`, serialized-reference semantics on top of `vcs affected`).
8. Keyboard/mouse input as a **thin, swappable** layer (medium risk; accept that it may be replaced).

### Integration hooks to prepare now
1. **Declare Nexus commands via `Unity.Pipeline.Attributes`** (Pipeline 0.8) so Nexus needs no hard dependency on the full package. Register state-dependent commands with the public `CommandRegistry.RegisterCommand` (0.7/0.8).
2. **Mark verify step executors `RuntimeOnly`** where they apply to dev Players, so the same scenario runs with `unity command --runtime <player>` (future `nexus.player_check`).
3. **Long operations via `--detach` + `unity job wait`** (V-07). Map the Nexus job store onto Unity job ids.
4. **Pass `--caller nexus --skill <name>` on every CLI call** Nexus makes, since this is the documented way for "an integration to identify itself" (PR #80). It costs nothing and makes Nexus visible in Unity's telemetry.
5. **Ship the Nexus skill inside the package**, mirroring Unity's `skill install --local` pattern, and add a `SECURITY.md` with an "Accepted risks" section matching Unity's skills-gate convention.
6. **Capability probing, not version pinning:** at startup, read `unity commands --format json` / `/api/commands?detail=tags`, and route to Unity's `simulate_*`, `profiler`, `ui_*` or `capture_*` the moment they appear. Keep a parity table in CI (A-01) against Pipeline N/N-1, because each minor has breaking changes.
7. **Consume `unity vcs affected --json`** for reference and impact queries, and `unity test --affected` results for regression selection inside verify.
8. **Watch list, checked monthly:**
   - PolySpatial playtest PRs (#72/#51) being merged or generalized.
   - Pipeline changelogs for `simulate_`, `ui_`, `profiler`, `frame_debugger` and overlay-capture fixes.
   - CLI GA announcement; Unity 7 beta in December 2026.
   - Changes to `unity mcp configure --server` (a possible third-party server registry).

---

## 5. Bottom line for planning

Unity is running a weekly-to-biweekly release train aimed at CLI GA and the Unity 7 beta in December 2026. Its investment is in transport, CI, runtime/hot-reload, compile and test ergonomics, security, and product-team domain skills.

Areas Unity is close to shipping:
- **Profiler integration:** a staff reply says it is planned "before GA".
- **Better capture:** Unity is already iterating on capture.
- **Input simulation:** a staff draft exists, currently scoped to PolySpatial.

No composed QA/verification layer, UI automation or token-budgeting work appeared in anything I found. Nexus's "eyes and brain" direction (verify, UI targeting, asserts, lean context, Safe-Mode diagnosis) is the right bet for the next 6–12 months, provided it is built as a portable layer over Pipeline primitives with capability probing. Expect input and capture to be commoditized within about 12 months.
