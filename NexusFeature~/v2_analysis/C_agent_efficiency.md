# Agent C: Agent efficiency, adoption, and honest measurement

Author: Agent C (independent analyst). Date: 2026-09-30. Scope: speed, quality, and token cost as the agent experiences them; why agents don't call Nexus; how to measure all of it honestly. Read-only review of the repo.

---

## 0. New evidence found in the raw data (additions to the shared findings)

1. **The bridge "fast" run ran with the Editor in Play Mode.** The cold `get_server_status` response in `results/bench_nexus_bridge_fast.json` contains `"isPlaying": true`. The raw runs (`bench_nexus_raw*.json`) show `"isPlaying": false`. In Play Mode the player loop keeps `EditorApplication.update` ticking. In Edit Mode an unfocused Editor throttles it. So the bridge's 2.6 ms (C3) and 25 ms (C6) against raw 100–300 ms is not a bridge advantage. It confirms the wake-up/throttle diagnosis: the same server answers main-thread work in about 2 ms when the loop is hot. It also means C3/C5/C6 "bridge vs raw" compared different Editor states, so they are invalid comparisons as well as different code paths.
2. **`get_server_status` answers in about 0.5 ms because it never touches the main thread.** Every main-thread call costs about 100 ms × the number of ticks it needs. This is the whole "speed tax".
3. **Both Nexus harnesses define `ok` as "no JSON-RPC `error` key".** The bridge wraps every Unity result, including `Timeout`, `PartialSuccess` and failures, into `result.content[0].text`, and never sets `isError`. As a result, C10 (status `Timeout`) and C8 (status `PartialSuccess`, "screencapture failed") were both scored 100%. The agent sees the same thing: MCP clients treat these as successful tool calls.
4. **The bridge has no way to return an image.** Every result is `{"type":"text","text": json.dumps(...)}`. A screenshot would reach the model as base64 inside a text block. The model can't see it, and it costs tens of thousands of tokens. The bridge also never exposes Capture V2; C8 went through `capture_window_snapshot`, which uses OS `screencapture`.
5. **The bridge `initialize` is out of date.** It uses `protocolVersion: 2024-11-05`, sends no `instructions`, and declares a `prompts` capability without implementing `prompts/list`, so the call returns -32601. Two resources exist (API reference and setup guide), but they are human docs.
6. **The 14 tool names and descriptions are generic nouns**, for example "Unified scene management (create, open, save, list)". None says when to use it or what it does that shell cannot. The only directive, on `unity_write_and_compile`, is "Use for ALL code changes". Agents with native edit tools will ignore it, and should.

---

## 1. Adoption diagnosis: why the agents in the 4 reports never used Nexus

### 1.1 What the reports show

| Report | Nexus calls | What the agent used instead | What it actually needed from Unity |
|---|---|---|---|
| 1 (reward/Addressables) | 0 | rg/sed, apply_patch, `dotnet build`, graphify | Real Unity compile, console |
| 2 | 2 (`refresh_assets`, `wait compilation`, 19 s, "worked well") | rg/sed, `dotnet build`, graphify | Compile truth after `dotnet build` |
| 3 (Forest/Canvas bug) | 0 | YAML grep, git history; **the user sent screenshots by hand** | Live hierarchy, inspector values, Game View image |
| 4 (texture packing) | 0 | **Computer-use (`cua_repl`) clicking the Unity UI**, which failed with wrong folders and timeouts, then reimplemented in PIL | Run an existing menu item with arguments, wait for import, verify |

The pattern is clear:

- Shell was the right choice for source-level work.
- Nexus should have won in reports 3 and 4. It lost there to "ask the human for a screenshot" and to "drive the GUI with computer use". Both are far slower and less reliable than an Editor API.
- The agent reached for pixel-clicking before an installed Editor API. That rules out capability as the cause. The problem is discoverability and framing.

### 1.2 Causes, ranked

1. **Deferred tool loading hides Nexus.** Claude Code defers MCP tools by default: the model sees only tool names and must call a search tool to load schemas. Codex behaves similarly as tool lists grow. A model scanning names like `unity_hierarchy_manager` or `unity_asset_manager` gets no hint that one of them can "show me the Game View" or "prove the fix works".
2. **No server `instructions`.** Claude Code injects each MCP server's `instructions` into the system prompt under "MCP Server Instructions"; this very session shows that block for other servers. Nexus sends nothing, so the model gets zero guidance on when to use it.
3. **Shell-first habit plus repo instructions.** Agents already have rg, sed and a build command, and repo AGENTS.md files tell them how to build and test. Nothing in the user's project AGENTS.md or CLAUDE.md said "for live Unity state or visual proof, use Nexus".
4. **The value proposition overlaps shell where shell is cheaper.** Reading code, grepping YAML and editing files are all better in shell. Nexus's 14 managers mostly advertise CRUD over scenes and components, which the agent can often do by editing YAML (report 3 did exactly that).
5. **Unity itself now tells agents to prefer `unity command` over MCP.** Unity's 31 official skills plus the `unity-cli` skill will steer agents to `unity command`, and Nexus doesn't appear in that universe except for 3 `[CliCommand]` wrappers.
6. **Setup friction and a trust deficit.** The agent can't tell whether the server is running. There is no cheap "is Nexus up?" check in its toolset, and a failed first call teaches it to avoid the tool.

### 1.3 Fixes

**A. Initialize `instructions` (highest leverage, about 1 day).** The bridge should return about 120–200 words in `initialize.result.instructions`, for example:

> Nexus reads and drives the LIVE Unity Editor. Files on disk can be stale; Nexus reports what Unity actually compiled, loaded, and rendered. Use it when you need: (1) Unity's real compile errors (not `dotnet build`) → `unity_compile_errors`; (2) proof a change works: play, click UI, capture Game View, assert → `unity_verify`; (3) live scene/inspector state → `unity_scene_snapshot`; (4) what references an asset → `unity_find_references`; (5) run a menu item or test and wait for it → `unity_run`. Use shell for reading and editing source. If a tool returns `status != ok`, read `hint` before retrying.

**B. Task-named tools.** Under deferred loading the name is the ad, so each name should say the job, not the object.

- Rules:
  - Use verb_object names that match what users say.
  - Start each description with "Use when …".
  - Name the shell alternative it beats, and why.
  - State its cost, e.g. "returns ≤2 KB unless detail=full".
- Suggested renames or new composite tools:

| Tool | Replaces or composes |
|---|---|
| `unity_compile_errors` | refresh + wait + group_compile_errors |
| `unity_verify` | play → input → wait → capture → assert → logs → stop |
| `unity_scene_snapshot` | compact_scene_snapshot with a budget |
| `unity_find_references` | Project-wide reverse dependency lookup |
| `unity_capture_game_view` | Capture V2 |
| `unity_run` | Menu item or tests, then wait |
| `unity_status` | Editor state |

Keep the old managers under a `full` profile.

**C. Profiles.** The default model-visible profile should hold at most 8 tools and stay under about 2.5k schema tokens. `NexusToolCatalog` already has `core/visual/scene/compat` for the HTTP side; mirror that in the bridge (`NEXUS_PROFILE=agent|full`). A small default is still right even though clients defer schemas: every tool loaded through search stays in context for the rest of the session.

**D. CLI-first exposure.** Register each composite as a `[CliCommand]` with a `nexus_` prefix and `Tags={"nexus", ...}` so it appears in `unity command` listings next to Unity's ~151 commands. This is where Unity-trained agents will look. Ship the same set via the bridge's CLI mode for non-Pipeline users (`nexus verify ...`).

**E. A Nexus skill (the "brain"),** installable into `.claude/skills/nexus-unity/SKILL.md`, the Codex equivalent, and alongside `unity skill install`. The frontmatter description should be written for triggering, for example:

> "Use when a Unity task needs live Editor truth: real compile errors, proving a UI/gameplay fix works, screenshots, scene/inspector state, asset reference lookups. Not for reading/editing C# source."

Body outline, about 150 lines:

1. **Decision table:** task → shell, `unity command` or `nexus_*`, with one-line reasons.
2. **Five recipes, each a copy-paste command sequence with expected output shape:**
   - Fix and prove a UI bug.
   - Why does compile fail.
   - Who references this asset.
   - Did my scene edit land.
   - Run tests and read failures.
3. **Budget rules:**
   - Request `detail=summary` first.
   - Screenshots at `max_dimension=1024` unless reading text.
   - Never dump the full hierarchy for scenes with more than 200 objects.
4. **State rules:**
   - Always check `unity_status` before Play Mode actions.
   - After a domain reload wait for `ready_epoch`.
   - Treat `status: stale` as not done.
5. **Error table:** error code → what to do.
6. **When not to use Nexus.**

**F. AGENTS.md/CLAUDE.md snippet.** The Nexus installer should offer to append it to the user's project (marked with a comment block):

```md
## Unity Editor access (Nexus)
- The Unity Editor for this project is live and reachable via Nexus (MCP server `nexus_unity`, or `unity command nexus_*`).
- Before claiming a Unity change works, prove it: `nexus_compile_errors` (not `dotnet build`), then `nexus_verify` for UI/gameplay behaviour.
- For live scene/inspector state use `nexus_scene_snapshot` instead of parsing .unity YAML; do not ask the user for screenshots — use `nexus_capture_game_view`.
- Use shell for reading/editing source files.
```

**G. MCP resources and prompts done right.**
- Implement `prompts/list`, or stop declaring the capability.
- Offer 3 prompts, e.g. `/nexus-verify-ui`, `/nexus-why-compile-fails`, `/nexus-who-references`. They appear as slash commands in Claude Code, which is a discovery path.
- Resources should be live and cheap, e.g. `unity://status` and `unity://compile-errors`, instead of the static docs.

---

## 2. Token economics

### 2.1 Assumptions

- JSON text costs about 1 token per 3.2–3.8 chars. Prose is about 4 chars per token. Base64 is about 1 token per 1.3–2 chars, so it is expensive.
- Images sent as real image blocks cost about `w×h/750` tokens. The cap is 1568 px on the long edge (about 1.6k tokens) for most models, and 2576 px (about 4.8k tokens) for Opus 4.7+ class models. Images are padded to multiples of 28 px.
- Each tool call adds about 100–200 tokens of call and reasoning scaffolding.
- Every result stays in context for all later turns. Prompt caching makes re-reads cheap in dollars (about 0.1×) but not in context budget, so I report context tokens added.
- Scene: a realistic mid-size UI scene with about 150 objects. v1's tiny test scene understated dump sizes (C4 was 1.5 KB for Nexus and 8.8 KB for Ivan).
- These are estimates to be replaced by measured numbers from the agent-level benchmark in §3.

### 2.2 Per-task estimates (context tokens added, excluding the task prompt)

**T1 "Button should toggle cube color; it doesn't. Fix it and prove it."**

| Step | (i) Raw Unity CLI + skill | (ii) Nexus MCP today | (iii) Nexus composites |
|---|---|---|---|
| Discovery | skill load ~2.5k + `unity command` list filtered ~1.5k | tool-search + load 2–4 manager schemas ~3–5k | instructions ~0.3k + 2 schemas ~0.8k |
| Locate button/handler | get_scene_hierarchy ~4–8k, get_component_properties ~1.5k | dump_scene_graph ~6–15k (all components) | scene_snapshot(filter="Button") ~0.8k |
| Edit code | native ~2k | native ~2k | native ~2k |
| Compile | recompile + status polls ~0.6k + logs ~1–3k | write_and_compile ~0.2k (good) | compile_errors ~0.2k |
| Prove | play, then `eval` invoking onClick ~1k (**skips raycast/EventSystem, weak proof**), screenshot PNG to file + read ~1.9k at 1600×900 | UI click targets EditorWindow, not game UI (v1 C12 failed); OS-capture snapshot or base64-in-text **~60–70k if it returns an image** | `verify(click="Canvas/ToggleBtn", assert="Cube.MeshRenderer.material.color != before", capture=1024)` ~0.4k JSON + ~0.8k image |
| **Total** | **~15–22k, 12–16 calls, weak proof** | **~20–30k, 10–15 calls, proof fails or ~90k+** | **~5–7k, 4–6 calls, strong proof** |

**T2 "Why does compile fail?"**

| Approach | Estimate | Notes |
|---|---|---|
| Shell `dotnet build` | 2–10k | Can disagree with Unity: stale csproj, defines, asmdef, package code |
| Unity CLI recompile + get_console_logs | 3–8k | Raw logs with stack traces and duplicates |
| Nexus today | 1–2k | wait + group_compile_errors, grouped by file, if the agent finds it |
| Composite `compile_errors` | 0.6–1.5k | Adds ±3 lines of source context per error and a `compile_epoch`, so the agent knows the result is fresh |

**T3 "What references this prefab?"**

| Approach | Estimate | Notes |
|---|---|---|
| Shell | 1–3k, 2–3 calls | Read .meta guid, then `rg -l guid Assets`. Misses nested/variant semantics, Addressables and Resources paths; unreadable if serialization is binary. |
| Unity CLI `eval` with AssetDatabase reverse scan | ~1.5k | Plus about 1k of code the agent must write. Slow on large projects. |
| Nexus today | 1–2k | `find_references` is scene-scoped |
| Composite `find_references` | 0.3–0.8k | Project-wide cached reverse index; paths grouped by scene, prefab, SO or addressable |

### 2.3 Biggest token sinks and fixes

1. **Images sent as base64 text.** A 1600×900 JPEG at quality 85 is about 150–250 KB, or 200–330 KB as base64: roughly 100k+ tokens, and the model can't see it.
   - Return MCP `image` content blocks.
   - Default `max_dimension=1024` (1024×576 ≈ 790 tokens). Use 768 for "did anything render" (about 440 tokens). Use a 1568 cap only when reading small text.
   - Offer `crop=<rect or UI path>` so the agent sends only the relevant region; this gives the best ratio.
   - For the CLI path, write to a file and return the path plus dimensions, so the agent reads it only if needed.
   - Never return both PNG and JPEG.
2. **Full hierarchy and component dumps.**
   - Default to a summary: name, path, and component type names only.
   - Add a `filter`, a `max_nodes` budget, and a `truncated: true` with a continuation cursor.
   - Return values only for requested components.
   - Omit default-valued fields and zero vectors. v1 C3 returned full Transform twice: at the object level and in components.
3. **Tool schemas.** Nexus's 19.7k chars (about 5–6k tokens) is small next to Coplay (about 29k) and Ivan (about 36k). Under deferred loading only the loaded schemas count, so keep each composite schema under 400 tokens, with enums and without `oneOf` fan-outs; the current managers use large `oneOf` per action.
4. **Console logs.** Deduplicate, collapse stack traces to the top user frame, and cap at N with counts.
5. **Polling loops.** Each status poll is about 150 tokens of call overhead. Use blocking waits with a timeout (Nexus already does this) and return a final state rather than intermediate states.
6. **Unity `unity command` full listing.** At about 151 commands it may be 5–10k tokens. The Nexus skill should use `unity command --tag nexus` or name the commands directly.

---

## 3. Benchmark v2 specification

### 3.1 Fixture project (`NexusBench`, versioned, Unity 6000.x LTS, committed to a separate repo)

- `Scenes/UIToggle.unity`:
  - Canvas (Screen Space Overlay) + EventSystem + Button `ToggleBtn`.
  - A `ColorToggler` script that toggles the Cube's material color red↔blue.
  - A second scene variant with a planted bug: onClick is not wired, or the raycast is blocked by a transparent Image.
- `Scenes/Big.unity`: about 1,500 generated objects, for payload and scaling tests.
- `Tests/EditMode`: 12 tests. 11 pass, 1 fails with a known message. 1 PlayMode test.
- Compile scenario: `Scripts/BreakMe.cs.txt` gets swapped in to produce 3 errors in 2 files. That covers CS0103, CS1002, and a cross-asmdef reference error that `dotnet build` on stale csproj misses.
- Prefab graph:
  - `Enemy.prefab` is referenced by 2 scenes, 1 prefab variant, 1 nested prefab, 1 ScriptableObject list and 1 Addressables entry.
  - 1 decoy with the same name in another folder.
- Menu item `Tools/Bench/PackAtlas(folder)` with argument validation, mirroring report 4.
- `bench_reset.sh`: git clean of `Assets/` plus a known Editor state (Edit Mode, UIToggle open, console cleared).

### 3.2 Protocol-level scenarios (each with a semantic check)

| ID | Scenario | Success = |
|---|---|---|
| P1 | Connect + list | Tool/command count and schema bytes recorded; first real call succeeds |
| P2 | Status | Returned `isPlaying/isCompiling` equal ground truth set by harness |
| P3 | Read object | Returned Cube material color equals the value in scene YAML |
| P4 | Snapshot Big.unity | Node count equals 1,500 (or truncated flag + cursor); bytes recorded |
| P5 | Create+destroy | Object exists after create, not after destroy (checked by an independent read) |
| P6 | Compile | Errors returned equal the 3 planted errors; `ready` reported only after a new compile epoch; **time-to-ready ≥ measured domain reload** (kills false-ready #0013) |
| P7 | Capture | Image decodes, dimensions as requested, **Overlay canvas pixels present** (sample the button's color at a known coordinate) |
| P8 | Tests | 12 discovered, 11 passed, 1 failed with the known message |
| P9 | UI QA loop | Play → click ToggleBtn **through the EventSystem** → Cube color changed (read) → capture shows the new color → logs contain the toggle message → Edit Mode restored |
| P10 | References | Returns exactly the 6 true referrers, not the decoy |
| P11 | Reload survival | Trigger a domain reload mid-session; next call succeeds or returns a documented retryable error |

"ok%" is split into three numbers:

- **transport_ok**: no RPC error.
- **tool_status_ok**: the tool itself reported success.
- **semantic_ok**: the independent check passed.

Only semantic_ok is headline.

### 3.3 Controls that fix v1's errors

- **Editor tick state.** Run every latency scenario under 3 conditions:
  - (a) Editor focused.
  - (b) Unfocused, default throttling.
  - (c) Unfocused with the product's own wake mechanism, e.g. Pipeline `set_autotick`, Nexus wake fix, or Unity's "Interaction Mode: No Throttling" preference.
  Record `isPlaying` per sample, and never compare samples taken in different play states.
- **Harness validation step.** Before the timed run, each scenario runs once in "assert mode" and the full response is saved and eyeballed. Mismaps are caught when the check fails, e.g. clicking "Main Camera" as a UI element, or using OS screencapture as a Game View capture.
- **Quantization check.** Report a histogram. If samples cluster at 100 ms multiples, flag "tick-bound".
- **N.** Use 30 warm + 1 cold per scenario for reads, and 10 for compile/tests/QA loops. Report p50, p95, and a bootstrap 95% CI. Restart the Editor for each cold sample (3 cold restarts).
- **Interleaving.**
  - Put one project clone per product, all open concurrently (multi-editor isolation is already validated).
  - Run in rounds: each round executes the scenario once per product in randomized order.
  - This removes drift from thermal state, Spotlight, etc.
  - If ports conflict, alternate A-B-B-A blocks.
- **Platforms.** Use macOS (Apple Silicon) and Windows 11 (x64, NVIDIA or AMD). Windows focus/throttle and GPU readback behave differently. Linux is optional.
- **Competitors:**
  - Unity CLI in two modes: persistent `unity mcp` and cold `unity command` per call (process spawn included, because that is what shell agents pay).
  - Unity `eval` for C11-type tasks.
  - Coplay, Funplay, Ivan at pinned versions.
  - Nexus v1.6 and v1.7.
  - A "shell only" baseline where applicable (dotnet build, rg).
- **Hygiene.**
  - Clean `__pycache__`.
  - Pin package hashes.
  - Record Unity version, GPU, display scale, and Game View resolution.
  - Keep raw JSON per sample, including the full response for the first sample.

### 3.4 Metrics table (per product × scenario × platform × tick condition)

`cold_ms | p50 | p95 | CI95 | semantic_ok% | tool_status_ok% | transport_ok% | payload_B p50 | est_tokens p50 (text + image separately) | calls | tick_bound(y/n) | reload_recovered(y/n)`

Static per product:
- tool count at default profile;
- schema tokens (measured with the Anthropic count_tokens API, not chars/4);
- setup steps to first successful call, timed by a fresh human.

### 3.5 Agent-level benchmark (the one that matters)

- **Tasks:** 8 tasks given as natural user prompts that don't name any tool.
  1. Fix and prove the UI toggle bug.
  2. Why does it not compile; fix it.
  3. What references Enemy.prefab.
  4. Make test X pass.
  5. Change the button color to #3366FF and show me.
  6. Run the atlas packer on folder Y and confirm the output size.
  7. Why is the Forest drawn over the UI? (sorting/canvas reproduction of report 3)
  8. A source-only refactor, as a control where Nexus should not be used.
- **Toolsets:**
  - S0: shell only.
  - S1: Unity CLI + official Unity skills.
  - S2: `unity mcp`.
  - S3: Nexus MCP current.
  - S4: Nexus v2 (instructions + composites + skill + CLI).
  - S5–S7: Coplay, Funplay, Ivan.
  - S1+S4 combined: the realistic future setup.
- **Agents:** Claude Code (`claude -p --output-format stream-json`) and Codex CLI (`codex exec --json`). Pin model versions and use default settings, including deferred tool loading.
- **Prompt conditions:**
  - Neutral: measures adoption.
  - Hinted, with the AGENTS.md snippet: measures capability given discovery.
- **N:** 5 runs per task × toolset × agent × condition as a minimum; 10 for T1/T2.
- **Metrics:**
  - Semantic success, checked by a grader script on the project state plus artifacts, never by the agent's claim.
  - **False-success rate**: the agent claims done but the check fails.
  - Wall time.
  - Input, output and cache-read tokens from transcript usage fields, and $ cost.
  - Number of tool calls by category (shell, Unity CLI, Nexus, other MCP).
  - **Chose-tool**: whether the first Unity-state action went through the product.
  - **Nexus share**: the fraction of Unity-state operations that went through it.
  - Number of human-required steps (e.g. "please send a screenshot"), counted as failures in headless mode.
  - Errors-then-recovered.
- **Reset:** `bench_reset.sh` between runs. Keep `Library/` warm and record which runs were cold.
- **Report:** a per-task Pareto view of success vs tokens vs time. The headline claim should read like: "Nexus v2 + CLI: X% success at Y tokens on T1 vs Z% for S1".

---

## 4. Quality and reliability metrics users feel, and how Nexus wins them

| Metric | Definition | Nexus move |
|---|---|---|
| Truthful status | P(tool says success ∧ semantic check fails) → target 0 | One status enum `ok / failed / timeout / partial / stale` mapped to MCP `isError` for everything except `ok`. Never return `Success` for a submitted-but-unfinished job. |
| No false-ready | `ready` only after compile epoch increments and domain reload completes | Expose `compile_epoch` and `reload_epoch` in every response. `wait` returns the epoch it observed. |
| Determinism | Same input and state → same output, e.g. stable ordering and ids by path, not instance_id | Sort outputs. Address objects by scene path + sibling index. Include `state_hash` so the agent can tell if anything changed. |
| Recoverable errors | Each error carries a `code`, `hint`, and `retryable` flag | e.g. `EDITOR_THROTTLED → hint: call unity_status(wake=true)`, `NO_EVENTSYSTEM → hint: add EventSystem or use invoke=true`, `PLAYMODE_REQUIRED` |
| Reload survival | % of calls across a domain reload that succeed or fail retryably | v1 raw C7 lost the listener. Bridge-side retry with backoff on `reload_epoch` change. |
| Latency floor | p50 main-thread call unfocused ≤ 20 ms | Wake the Editor loop on request arrival (`QueuePlayerLoopUpdate` / `RepaintAllViews`, or Pipeline autotick). This is the single cheapest speed win. |
| Proof quality | Verification exercises the real input path (EventSystem raycast), not reflection | `verify` reports `input_path: "eventsystem"` versus `"invoke"` explicitly. |
| Cost predictability | Every response under a declared budget, with a `truncated` flag | Default budgets per tool; show `approx_tokens` in the response. |

The strategic point: Unity owns the "hands", and raw speed will converge. Nexus can own trust, in the form of "when Nexus says it works, it works", with an evidence bundle (image + assertion + logs) the agent can show the user. The false-success rate is the metric to publish.

---

## 5. Top 10 prioritized actions

| # | Action | Effort | Expected impact |
|---|---|---|---|
| 1 | Fix main-thread wake-up (no 100 ms tick dependence), and re-measure focused/unfocused | S | 5–50× lower latency on every main-thread call; removes the "speed tax" story |
| 2 | Truthful status: map non-ok to `isError`, add `code/hint/retryable`, epochs; fix false-ready #0013 | S–M | Kills false-success; makes all benchmarks and agent loops trustworthy |
| 3 | Add `initialize.instructions`, update protocol to 2025-06-18 (structured output, image blocks), implement or remove `prompts` | S | Largest adoption lever per hour of work |
| 4 | Composite, task-named tools (`verify`, `compile_errors`, `scene_snapshot`, `find_references`, `capture_game_view`, `run`, `status`) as a ≤8-tool default profile; old managers under `full` | M | 3–5× fewer tokens and calls on T1-type tasks; better names under deferred loading |
| 5 | Project the same composites as `[CliCommand]` `nexus_*` (Pipeline) plus bridge CLI mode | S–M | Nexus shows up where Unity-skilled agents look; rides Unity's distribution |
| 6 | Nexus SKILL.md + AGENTS.md/CLAUDE.md snippet via installer (opt-in) | S | Directly addresses the 0-call pattern in the reports |
| 7 | Image hygiene: real image blocks, default 1024 px JPEG, crop-to-UI-element, file-path mode | S | Screenshot cost from ~100k-token base64 down to ~0.4–0.8k; the agent can actually see |
| 8 | `unity_verify` with a real EventSystem input path + assertions + evidence bundle (the flagship) | M–L | Wins the task class where agents currently ask humans for screenshots or use computer-use |
| 9 | Benchmark v2 fixture + protocol harness with semantic checks, tick controls, interleaving, macOS + Windows | M | Credible public numbers; stops self-deception |
| 10 | Agent-level benchmark (Claude Code + Codex, neutral vs hinted prompts, S0–S7), published with false-success rate | M | The only evidence that proves adoption and value; drives future priorities |

Suggested order: 1–3 in week 1; 6–7 next; then 4–5; 8 together with 9; 10 once 4–8 land. Run a neutral-prompt pilot of T1/T2 before and after items 3 and 6 to measure the adoption change directly.

---

### Sources (web)
- Claude vision token cost and resolution limits: https://platform.claude.com/docs/en/build-with-claude/vision
- Claude Code tool search / deferred MCP loading: https://oldeucryptoboi.substack.com/p/tool-search-deep-dive , https://azukiazusa.dev/en/blog/enable-claude-code-tool-search-to-reduce-mcp-token-usage
