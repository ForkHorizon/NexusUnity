STATUS: DRAFT — NOT FROZEN — DO NOT RUN until the lead fills the Unity discovery model from 02_UNITY_CLI.md and signs the freeze block.

# Discovery preregistration — cheap paired study (not Gate F, not P0-17)

Author: Agent I. Date of this draft: 2026-10-04. Status: **DRAFT**. No arm has been built for this file. No agent run, Unity call, or fixture build was done to write it.

This file is the protocol a later runner must follow if, and only if, the lead freezes it. It is not `PREREG_P0-17.md`, not `PREREG_GATE_F.md`, and not permission to schedule S5, S6-P, Mixed, C1 as a product, a façade, write delegation, or `eval` / `run_script` forwarding.

**Proposed N = 12 scored runs per runnable arm (6 tasks × 2 seeds).** That is the minimum that can falsify a *large, consistent* effect under the rules in §8. It cannot confirm an architecture.

---------------------------------------------------------------------
## 0. What this study is allowed to mean

### 0.1 Central hypothesis

> Is Nexus valuable because of its semantics and workflows, or are most observed benefits reproduced simply by exposing fewer official Unity capabilities?

Operational comparison: **C1** (official Unity commands only, task-relevant subset, official names and official descriptions) against the **current production Nexus surface** at commit `bfdd0f7` (**S4**), with **S4+** as a description-only bench overlay. If C1 matches S4 and S4+ on the rules in §8, a façade is not justified *by these runs*. If S4 or S4+ beats C1 on semantic success or false-success, and that gap is not just a shorter payload, the semantic layer has *large-effect evidence on this set only*.

### 0.2 This is not Gate F and not P0-17 Stage A

| | This draft | Plan Gate F / P0-17 (do not run those here) |
|---|---|---|
| Purpose | Falsify a large "fewer tools" vs "semantics" gap, or declare it unreadable | Confirm or kill architectures |
| N | 12 scored runs per arm | Stage A ≈6×8 tasks; Stage B hundreds of paired runs per arm. Plan text: D1 at 80% power needs ≈235–290 paired runs per arm under mild heterogeneity |
| Arms | U-FLAT, U-TAGS (conditional), C1, S4, S4+, S6-P-mini | S4 (frozen 1.7.0 release), S4+ (S-10/S-11 + recipes), C1, S5, S6-P, M |
| Data | Never pooled into the 2026-10-03 pilot, Stage A, or Stage B | Fresh runs only; pilot already judged INSUFFICIENT EVIDENCE |

Plan names are **not** these arms:

- Plan S4 is a frozen Nexus 1.7.0 *release* plus `unity mcp`, and it does not absorb S-10/S-11. **This S4** is the git tip `bfdd0f7` (stacked P0-02, S-04 envelope, S-07 JPEG look defaults, S-02 CLI drift lock, plus the legacy surface that tip already contains). It also does not receive S-11.
- Plan S4+ adds legacy-dump removal, skill routing, and option-E recipes. **This S4+** is description text only (§3.5).
- Plan S6-P needs merged `nexus_status` / `snapshot` / `diagnose` / `compile` / `look` / `run` / `get`. **S6-P-mini** is a bench stub. It is not an H2 test (§3.6).

### 0.3 What N=12 can and cannot do

A *large* effect, frozen now:

- **Success:** on 12 paired runs, one arm has at least 5 paired wins and the other has at most 1 (net ≥4/12, about 33 percentage points), **and** the same direction still has net ≥3 after deleting either seed of any one task.
- **False-success:** the counts differ by ≥3 runs out of 12, and the direction survives that same drop-one-task cut.
- **Cost:** geometric mean, across tasks, of (arm A total-tokens-per-success ÷ arm B total-tokens-per-success) is ≥1.50 or ≤0.67, on at least 4 tasks where both arms have ≥1 semantic success, **and** the uncached-token ratio is on the same side of 1.0.

What this N can do: fire those rules when the gap is large *and repeated across tasks*. A one-task sweep fails drop-one-task on purpose.

What this N cannot prove, and must not be written up as proving:

- Gate F margins (non-inferiority −10 pp, cost ratio 0.8, call ratio 0.8). At this N the expected label is **INSUFFICIENT**. That is an acceptable result.
- A 15 pp success gap. The plan's own power note says that needs on the order of 235–290 paired runs.
- Client portability, maintenance cost, security, or transport (H5/H7).
- That S6-P, a façade, Mixed, or "keep today's architecture" is the right product. See §8. No architecture is selected here.
- Rates to within ±15 pp. With 12 binary runs the sampling noise is the result.

Do not add seeds or tasks after any agent result is visible. Do not drop a metric because the table looks cleaner without it.

---------------------------------------------------------------------
## 1. Freeze block

The lead fills every cell **before** the first agent run, by hand, from the machine that will run. Desk notes in `00_UPSTREAM_PRIMARY_SOURCES.md` are not a freeze. Empty means empty.

| Field | Value |
|---|---|
| Date frozen | |
| Unity version | |
| Pipeline version | |
| CLI version | |
| Model | |
| Reasoning level | |
| Client | |
| Commit | |
| Seed list | |
| Lead signature | |

Model, reasoning level, and client are **to be fixed at freeze**. This draft does not choose them. The 2026-10-03 pilot's client and model are history, not a default.

Hard checks before signature. Any failure means do not start.

1. `02_UNITY_CLI.md` exists and §2 of this file has been filled from it. Conditional arms are marked RUN or NOT RUN there.
2. The signed commit is `bfdd0f7e71c851fb4615956f3c420940a9623220`. Any other sha is a different study.
3. One Unity channel for every arm (§4). Shell `unity` stays off unless §2 records that the frozen client can reach the Editor **and** does not delete the Pipeline port file. If that sentence is missing, shell `unity` is off.
4. The same model id, the same reasoning level, the same client build, the same context window, and the same deferral / tool-search mode on every arm. A larger catalog must not silently flip reasoning or discovery mode.
5. Seed list is exactly two positive integers. They permute arm order inside a task. They do not change prompts, fixtures, or temperature. If the client has no seed parameter, the integers still fix the arm order and that fact is written next to the list.
6. No second Editor. No `Unity -runTests`. Oracles that need EditMode tests run inside the single already-open Editor through the harness channel, or that task is not started (§5).

---------------------------------------------------------------------
## 2. Unity discovery model (lead fills at freeze from 02_UNITY_CLI.md)

Do not upgrade Pipeline or the CLI to manufacture an arm. The study uses the versions in §1.

### 2.1 Facts the lead must copy (leave blank in this draft)

| Question | Answer from 02_UNITY_CLI.md (blank until freeze) |
|---|---|
| Default listing when the agent asks for commands (full list, or tag index with counts) | |
| Does a full/flat catalog still exist on this pair, and the exact flag or API | |
| Does official tag / progressive discovery exist on this pair, and the exact flag or API (`--tag`, `/api/commands?detail=tags`, client tool list, other) | |
| Command count actually returned in the flat listing | |
| Tag count actually returned, if the mode exists | |
| Official `capture_game_view` default `source`, and whether `source=screen` works in Edit Mode on this Pipeline | |
| Whether shell `unity` from the frozen client deletes the port file or fails to reach the Editor | |
| Client mechanism that will be used for all arms (MCP, CLI, other) — one only | |

A desk note already on disk (`00_UPSTREAM_PRIMARY_SOURCES.md`, 2026-10-04) says installed CLI `1.0.0-beta.10` + Pipeline `0.7.0-exp.1` still return a full listing, and that a tag index is documented for CLI beta.12 + Pipeline 0.8. That note is **not** §2.1. The lead confirms or replaces it from `02_UNITY_CLI.md`.

### 2.2 How the answers turn arms on

- **U-FLAT is RUN** only if §2.1 names a real full-catalog mode on the frozen CLI+Pipeline. If the full listing has been removed, U-FLAT is **NOT RUN**. NOT RUN is not a failure and is not a zero.
- **U-TAGS is RUN** only if §2.1 names a real progressive/tag mode on that same frozen pair. If this pair only returns the flat list, U-TAGS is **NOT RUN**. Do not install 0.8 to create it.
- **S4, S4+, and C1** use the **default** discovery mode of the frozen pair, not a private flat dump and not a private tag index. C1 then intersects that mode with the §3.4 allowlist.
- If both U arms are NOT RUN, the tag-confusion metric is **NOT RUN** (§6.16). C1 vs S4 can still be scored.

---------------------------------------------------------------------
## 3. Arms

Shared agent preamble, byte-identical, first paragraph of the only instruction file:

> This is a Unity 6 project. A Unity Editor is running with this project open. Inspect and change Editor state with the tools this session actually exposes. Do not edit scene or prefab YAML by hand. Save scenes you change. Do not claim the project compiles because a shell or `dotnet` build succeeded.

One extra sentence per arm, and nothing else. No arm's sentence names another arm. S4+ "Use when" text lives in tool descriptions (§3.5), not in this paragraph.

### 3.1 U-FLAT — conditional

Official Unity catalog in the full/flat mode §2.1 names. No Nexus server, no `nexus_*` tool, no Nexus skill, no Nexus description line. **NOT RUN** until §2.2 says RUN. If RUN, the exposure must be the real flat mode, not a hand-trimmed list (that would be C1).

### 3.2 U-TAGS — conditional

Official Unity progressive/tag discovery exactly as §2.1 names it. No Nexus. The agent may drill into tags the product already supports. The runner must not pre-expand every tag into the prompt. **NOT RUN** until §2.2 says RUN.

### 3.3 C1 — control ("fewer tools, no Nexus semantics")

Official Unity commands only, official names, official description strings, official defaults. No `nexus_*`. No rewritten descriptions. No skill text about Nexus workflows, grouping, or overlay capture.

The visible set is the allowlist in §3.4 and nothing else. If the frozen default is tags, C1 exposes only tags that still contain at least one allowlisted command, and drilling in shows only allowlisted commands. If the frozen default is flat, C1 exposes only the allowlist. Do not invent a third discovery UI.

Excluded on purpose, so "fewer tools" is not "arbitrary C#": `eval`, `eval_file`, `run_script`, agent `batch`. A run that reaches them is an allowlist escape (§6.13), not a C1 capability.

### 3.4 C1 allowlist (provisional names — confirm at freeze, then lock)

These names were used in the 2026-10-03 catalog notes. At freeze, **delete** any name `02_UNITY_CLI.md` says is absent. **Do not add** a substitute, a Nexus alias, or a command outside this list. If the deletion removes the last command in a row, the tasks that needed that row are not given a new tool; they become harder, which is the point of the control.

| Task need | Provisional official names |
|---|---|
| Scene read | `get_scene_hierarchy`, `find_gameobjects`, `search`, `get_selection` |
| Component read/write | `get_component_properties`, `get_serialized_fields`, `set_serialized_field` |
| Save | `save_scene` |
| Official capture (unchanged defaults) | `capture_game_view`, `capture_scene_view`, `screenshot` |
| Compile / console | `recompile`, `recompile_status`, `console`, `console_status`, `editor_status`, `wait_for`, `reload_file` |
| Refresh entry point | `menu` |
| Tests | `run_tests` |

`menu` is included only because this Editor's compile path has needed `Assets/Refresh` before new scripts are visible. Any `menu` call whose path is not `Assets/Refresh` counts as escape-path usage even on C1. If §2.1 names a narrower refresh command, the lead swaps `menu` for that one name **before** run 1 and deletes `menu`. No second swap later.

### 3.5 S4 — frozen current surface at `bfdd0f7`

Direct Unity, in the **default** mode §2.2 assigns, **plus** Nexus as that commit actually is:

- Canonical commands, and only these three: `nexus_project_map`, `nexus_group_compile_errors`, `nexus_capture_game_view`.
- `nexus_capture_game_view` already has the S-07 look defaults shipped on this tip (JPEG, default long side 768). Do not retune them for the study. `nexus_look` is not a separate exposed command.
- `nexus_group_compile_errors` groups logs. It is **not** `nexus_compile`. It does not refresh, recompile, or own the compile pipeline.
- `nexus_project_map` is the project/context command that exists. It is **not** `nexus_snapshot`.
- Legacy surface stays as `bfdd0f7` exposes it (the three canonical commands plus the remaining raw tools; the sprint handoff describes this as the ~120-tool catalog). Do not hide `unity_dump_scene_graph` or other legacy dumps. That would be S-10, and it is **out of this arm**.
- Descriptions are the `bfdd0f7` strings, byte for byte. **No S-11 edits.** A proxy that rewrites them voids S4.
- Runtime mode stays the commit's default (`auto`). Do not force a private pipeline/legacy switch the other arms do not get.

Instruction sentence: "Unity tools are the official Unity server. Nexus tools are the `nexus-unity` server as installed, including `nexus_project_map`, `nexus_group_compile_errors`, and `nexus_capture_game_view`."

### 3.6 S4+ — same binaries, bench-only description overlay

S4+ is S4 plus **discovery-description text**. It is not a build. It must not be committed. Apply it with a bench proxy or a copied catalog under `NexusFeature~/bench/` that forwards every call to the real `bfdd0f7` tools and returns the **unmodified** tool result.

Allowed text changes, and no others:

1. For each legacy Nexus manager whose schema is a `oneOf`, append one line to the **top-level** description: `Actions:` followed by the action names that manager already has. Do not add actions. Do not change parameters, enums, or result JSON.
2. Prepend exactly one sentence to each canonical command, and do not change the rest of its description:
   - `nexus_project_map`: "Use when you need the project map this command already returns."
   - `nexus_group_compile_errors`: "Use when you need grouped compile and console errors. This command does not recompile."
   - `nexus_capture_game_view`: "Use when you need the Game View image this command already captures."
3. If, and only if, the frozen client reads `_meta` search hints, set those hints to the same words already added in (1) and (2). If it ignores hints, write "hints unused" in the run log. Do not add a second prose channel to compensate.
4. Do not rename tools, do not hide dumps, do not add recipes, do not add option-E snippets, do not shorten outputs, do not change JPEG size, limits, or envelopes.

If a forwarded result differs from S4's result for the same arguments, the S4+ run is void. Description text is the only experimental factor. The offline retrieval note (action lists moved Nexus-only recall@8 from about .55 to about .88, and also raised duplicate Unity/Nexus hits) is why this arm exists. It is not a prediction the runner may "confirm" by dropping metrics.

Instruction sentence: same words as S4. No extra routing paragraph.

### 3.7 S6-P-mini — bench only, not S6-P, must not ship

Not a regrouping of the catalog into domain managers. Not production. If a tree for it is created later, it stays under `NexusFeature~/bench/` and is never committed as package code. This draft does not create that tree.

The agent-facing names and what a scorer is allowed to believe:

| Slot | Exposed? | Status | What a call may do | Counts as production capability? |
|---|---|---|---|---|
| status | Yes, name `bench_status` | **MOCK** | There is no `nexus_status`. P0-02 readiness exists inside the Editor, but this slot is not that API. Return `not_built` and no body. Do not write a status paragraph. | No |
| snapshot | Yes, name `bench_snapshot` | **MOCK** | **L-03 is not built. Snapshot-like state does not exist in production.** Return `not_built` and no scene data. A payload that contains hierarchy, components, or images voids the entire arm. | No |
| compile | Yes, name `bench_compile` | **MOCK** | **S-05 `nexus_compile` is not built.** Return `not_built`. Do not call `recompile` under this name. | No |
| diagnose | Yes, name `bench_diagnose` | **MOCK** | **Not built.** Return `not_built`. Do not merge console, tests, and compile into a fake report. | No |
| look | Yes, name `nexus_capture_game_view` | **REAL** | The real `bfdd0f7` command, real schema, S4 description (not the S4+ sentence). | **Yes** |
| group errors | Yes, name `nexus_group_compile_errors` | **REAL** | The real command. It is not compile. | **Yes**, as grouping only |
| project map | Yes, name `nexus_project_map` | **REAL** | The real command. It is not a scene snapshot. | **Yes**, as that command only |
| run | Yes, name `bench_run` | **MOCK** | **L-04 is not built.** Return `not_built`. Do not invoke menu items here. | No |
| get / handle | Yes, name `bench_get` | **MOCK** | **Not built.** No handle store. Return `not_built`. Do not crop or replay a previous payload. | No |
| search | Yes, name `bench_bounded_search` | **BENCH PASS-THROUGH** | One official command named in §2.1 as the search/find command, `limit` ≤ 50, response body unmodified. No Nexus index. No task-specific filter. | No. Count as pass-through, not Nexus coverage |
| escape | Yes, name `bench_escape` | **ESCAPE** | Forwards one call to an official name on the locked §3.4 allowlist, arguments unchanged, body unmodified. Any other name is an invalid-argument error and still counts as an escape attempt. No `eval`. | No |

Unity's own server is **not** mounted. `unity` is not on PATH. File edits by the agent are allowed on write tasks, on every arm, because the tasks ask for a code or scene fix and the Editor tools do the Editor part.

Do not put the word MOCK in the agent prompt. The scorer's table above is the mark. The agent instruction sentence is only: "Editor tools are the single bench server in this session. Prefer them. There is no separate Unity tool list."

A success that used any MOCK call as the call that produced the answer or the edit is **not** production-policy success (§6.2). A MOCK that returns invented Editor state voids every S6-P-mini run. Under-performance because `bench_compile` / `bench_snapshot` / `bench_run` return `not_built` does **not** falsify plan H2. Those composites do not exist. Over-performance that depends on a MOCK payload is void, not a Nexus win.

---------------------------------------------------------------------
## 4. Design that the runner must not relax

- **Paired:** same task, same seed, every runnable arm, before the next task. Arm order inside a task is the permutation given by that seed. Reset the fixture between runs (§5.0).
- **Identical model configuration** across arms (§1). "To be fixed at freeze" means the cells start blank. It does not mean each arm may pick its own default.
- **Prompts** are the strings in §5, byte-identical across arms.
- **Channel** is the one channel in §2.1. No arm gets shell `unity` if another does not.
- **One Editor.** Do not start a second one. Do not launch `Unity -runTests`.
- **Blind semantic score:** the oracle sees the final message and the Editor. It does not see the arm id. Join the arm id after PASS/FAIL is stored.
- **No peeking:** per-run oracles may be executed immediately so a broken fixture is caught. Cross-arm tables, ratios, and labels are computed only after every planned run has a score or a void.
- **Pilot normalisers are not reused.** They were written after the tasks and are contamination-exposed.
- **Logs:** one JSON object per run. Every key in §6 is present. Use `null` plus a `reason` string when a value is unobservable. A missing key makes the whole analysis invalid.

Void, re-run once, then leave void (do not impute, do not score as failure): Editor crash; port file deleted; transcript missing; oracle process failed; arm saw the wrong tool list or an S4 description that is not the frozen string; S4+ result body differed from S4; any MOCK returned fabricated state; agent saw this prereg, TASKS_v1, or an answer key. A wrong answer is a failure, not a void.

---------------------------------------------------------------------
## 5. Tasks

Six tasks. Prompts do not name a tool, server, or product. Read-only prompts include the `ANSWER:` line as part of the prompt.

**Fixture precondition (not optional, not improvised).** `Assets/BenchFixture/` is not in this harness, and TASKS_v1 §0.4 constants are still placeholders. The fixture is TASKS_v1 §1, built outside the agent workspace, with hidden-answer hygiene from TASKS_v1 §0.2. Oracles prepend TASKS_v1 §0.5 unchanged. `<KEY_*>` values come from that builder's answer key. If the key or the fixture is missing, **do not invent constants and do not run**. If a self-check below fails, replace that task *before any agent run* from the spare list in §5.8, in order. If fewer than 4 tasks remain runnable, abort the study.

Reset between runs: TASKS_v1 §5, plus restore any S4+ proxy so it cannot leak into the next arm. Console seed for DCON is the live Editor seed in TASKS_v1 §1.4, not a file the agent is told to open.

Read-only integrity (DCAP, DCON, DBOARD, and spare T8): `git status --porcelain` on `Assets`, `ProjectSettings`, `Packages` shows no change outside the arm's recorded package whitelist, and the open scene is not dirty. Violation → semantic FAIL, category `mutated`.

Harness waits, before every oracle, until the Editor is not compiling and not updating. If it is in Play mode, exit Play mode and record that. Do not start a second Editor to do it.

### 5.1 DCAP — capture / orientation (RO). New. Uses the MainMenu plant from TASKS_v1 T1.

**Prompt:**

> On the main menu, something is drawn over the buttons. Tell me what is in front of the Play button in the Game view, and whether that object blocks clicks. Don't change anything.
> Finish your reply with one line in exactly this form:
> `ANSWER: top=<object name>; blocks_clicks=<yes|no>; play_button_path=<hierarchy path>`

**Self-check (must PASS before this task is frozen):** open `Assets/BenchFixture/Scenes/MainMenu.unity` from disk. `FxCanvas/VignetteOverlay` exists, is active, has an enabled `Image` with alpha ≥ 0.3, `FxCanvas` sorting order is greater than `MenuCanvas`, the overlay `Image.raycastTarget` is true, parent `CanvasGroup`s block raycasts, and `FxCanvas` has an enabled `GraphicRaycaster`. Otherwise drop DCAP.

**Oracle:** parse the last `ANSWER:` line. Normalise `play_button_path` by trimming, collapsing spaces around `/`, and stripping an optional `MainMenu.unity:` prefix. PASS only if `top` is `VignetteOverlay`, `blocks_clicks` is `yes`, and the path ends with `MenuCanvas/MainPanel/Buttons/PlayButton`. Anything else, including a missing line, is FAIL. Then the read-only integrity check.

**False-success examples (still FAIL):** `top=PlayButton`; `blocks_clicks=no`; blaming `CreditsButton`; saying the menu is fine. A camera capture that misses Overlay UI is a known way to produce those answers on Pipeline 0.7. It is not a separate pass rule.

**Acceptable commands for top-K and wrong-tool (§6):** official `capture_game_view`, `capture_scene_view`, `screenshot`, `get_scene_hierarchy`, `find_gameobjects`, `get_component_properties`, `get_serialized_fields`; on S4/S4+ also `nexus_capture_game_view`. Hierarchy reads are acceptable. Console, recompile, test, and `dotnet`/`msbuild` calls are wrong-tool. Calling official `capture_game_view` is **not** wrong-tool; if the logged arguments use the camera/default source that §2.1 says misses Overlay, set `capture_default_risk` true (§6.17).

### 5.2 DCOMP — compile truth (W). TASKS_v1 T2, still valid.

**Prompt (frozen in TASKS_v1):**

> I just merged a branch and Unity now shows compile errors. Fix them so the project compiles in Unity again. Keep the new code from the merge and don't change behaviour.

**Why it is still valid:** the oracle is Unity's compile result, not a file the agent can grep, and not `dotnet build`. `nexus_compile` does not exist; both Unity `recompile` and `nexus_group_compile_errors` may appear. Grouping is not a compile.

**Self-check:** the T2 variant from TASKS_v1 §1.8 is applied, `EditorUtility.scriptCompilationFailed` is true, and `HarborLightBaker` still exists on disk. Otherwise drop DCOMP.

**Oracle, both parts required.** Part A: harness runs `AssetDatabase.Refresh()`, then `CompilationPipeline.RequestScriptCompilation()`, waits until compile and domain reload finish (timeout 180 s), then runs this body (TASKS_v1 T2):

```csharp
if (EditorUtility.scriptCompilationFailed) return "FAIL:Unity compile still failing";
if (Type.GetType("Saltmarsh.Weather.WeatherProfile, Saltmarsh.Weather") == null) return "FAIL:WeatherProfile moved/removed";
var baker = Type.GetType("Saltmarsh.EditorTools.HarborLightBaker, Saltmarsh.Editor");
if (baker == null || baker.GetMethod("Bake", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static) == null) return "FAIL:merged HarborLightBaker removed";
var src = File.ReadAllLines(FX+"Scripts/Editor/Lighting/HarborLightBaker.cs").Where(l => !l.TrimStart().StartsWith("//"));
if (!src.Any(l => l.Contains("WeatherProfile")) || File.ReadAllText(FX+"Scripts/Editor/Lighting/HarborLightBaker.cs").Contains("#if false")) return "FAIL:merged code disabled";
return "PASS";
```

Part B, same Editor, not `Unity -runTests`: TestRunnerApi EditMode assembly `Saltmarsh.Tests.EditMode`. Require TideMathTests 3/3 and TideClockTests 1/1, and the overall result equal to the fixture baseline (9 pass, 1 fail = `BulkDiscount_StartsAtHundredUnits`, 1 ignored). SHA-256 of the test files unchanged. If part B cannot be run without a second Editor, **do not start DCOMP**; take the next spare. Do not score part A alone as PASS.

**False success:** the agent says it compiles because a shell or `dotnet` build passed, or because old assemblies are loaded, while part A fails. Also FAIL: deleting or `#if false`-wrapping `HarborLightBaker`, moving `WeatherProfile`, editing tests.

**Wrong-tool:** scene-hierarchy dumps, capture calls, prefab edits, `dotnet` / `msbuild` / `csc`. Acceptable: edits under the fixture script paths, `recompile`, `recompile_status`, `console`, `console_status`, `editor_status`, `wait_for`, `reload_file`, `menu` path `Assets/Refresh`, `run_tests`, and `nexus_group_compile_errors`. `bench_compile` is a MOCK call, not an acceptable production command.

### 5.3 DCON — console triage (RO). TASKS_v1 T5, still valid.

**Prompt (frozen in TASKS_v1):**

> The Unity console is flooded since I pulled. Which of these are actual errors I need to fix? Give me each distinct error once, with where it comes from in our code. Don't change anything.
> Finish your reply with one line in exactly this form:
> `ANSWER: count=<number of distinct errors>; errors=<file>:<line> | <file>:<line> | ...`

**Oracle:** parse `ANSWER:`. `count` must be 3. The three tokens `ManifestValidator.cs:47`, `LocalizationCheck.cs:31`, and `HarborAmbience.cs:58` must be present (file name + line; directory prefix allowed; order free). No token may name `AudioBankCheck`, `SaltmarshStartupChecks`, Tides, or `[Net]` as an error. `AmbienceWarmup.cs:<KEY_AMBIENCE_WARMUP_LINE>` is accepted only when `HarborAmbience.cs:58` is also present. Then read-only integrity. Console clearing is not a mutation.

**False success:** warnings counted as errors; seven undeduplicated errors; the exception omitted; only the logging site of the third error.

**Wrong-tool:** hierarchy, capture, asset import. Acceptable: `console`, `console_status`, `nexus_group_compile_errors`.

This task was easy in the pilot (often 2/2). A tied success is informative only together with false-success, wrong-tool, and tokens. A token-only gap is not semantic evidence (§8).

### 5.4 DUI — menu fix (W). TASKS_v1 T1, still valid. Static oracle only.

**Prompt (frozen in TASKS_v1):**

> On the main menu scene none of the buttons react when I click them in Play mode — not even Play. Figure out why and fix it. Keep the vignette look on the menu as it is, and save the scene when you're done.

**Oracle:** TASKS_v1 T1 **static** body only, on `MainMenu.unity` opened from disk:

```csharp
var s = OpenFromDisk(FX+"Scenes/MainMenu.unity", out var dirty);
var ov = F(s, "FxCanvas/VignetteOverlay"); if (ov == null || !ov.activeInHierarchy) return "FAIL:overlay removed/inactive";
var img = ov.GetComponent<Image>(); if (img == null || !img.enabled || img.color.a < 0.3f) return "FAIL:vignette look changed";
var fx = F(s, "FxCanvas").GetComponent<Canvas>(); var menu = F(s, "MenuCanvas").GetComponent<Canvas>();
if (fx.sortingOrder <= menu.sortingOrder) return "FAIL:vignette no longer drawn over menu";
bool groupsBlock = ov.GetComponentsInParent<CanvasGroup>(true).All(g => g.blocksRaycasts);
var gr = fx.GetComponent<GraphicRaycaster>();
bool stillBlocks = img.raycastTarget && groupsBlock && gr != null && gr.enabled;
if (stillBlocks) return "FAIL:overlay still blocks raycasts";
var play = F(s, "MenuCanvas/MainPanel/Buttons/PlayButton").GetComponent<Button>();
if (!play.interactable || play.onClick.GetPersistentEventCount() != 1 || play.onClick.GetPersistentMethodName(0) != "OnPlayPressed") return "FAIL:play button wiring changed";
if (F(s, "MenuCanvas/MainPanel/Buttons/CreditsButton").GetComponent<Button>().interactable) return "FAIL:credits button changed";
return "PASS";
```

The dynamic Play-mode raycast oracle from TASKS_v1 is **not** a pass condition here (one Editor, no extra Play session). Do not add it after a surprising result. False success remains: scene not saved, overlay deleted or made transparent, Credits button made interactable, onClick rewired, a second EventSystem, or an unrelated input-module edit. Those are FAIL if the static body fails.

**Wrong-tool:** console, recompile, `eval`. Acceptable: hierarchy, component get/set, `save_scene`, official capture, `nexus_capture_game_view`.

DCAP and DUI share a plant. DCAP is say-what-is-there. DUI is change-the-raycast-block without destroying the vignette. Keep both. Drop-one-task exists so one plant cannot decide the study.

### 5.5 DBOARD — missing reference (RO). TASKS_v1 T4. Not in the pilot set. Grep risk is scored, not ignored.

**Prompt (frozen in TASKS_v1):**

> In the Harbor scene the quest board on the market square doesn't show any notices when I play. Look at how it's set up and tell me what's wrong. Don't fix it yet.
> Finish your reply with one line in exactly this form:
> `ANSWER: object=<hierarchy path>; component=<component type>; field=<field name>; problem=<short description>`

**Oracle:** parse `ANSWER:`. Normalise `object` (trim, collapse spaces around `/`, strip an optional `Harbor.unity:` prefix). PASS only if the path ends with `QuestBoard_Stand/Board`, `component` is `QuestBoard` (case-insensitive), `field` matches `notice template` with optional space, and `problem` matches `missing`, `deleted`, `no longer exist`, `does not exist`, `broken`, or `nonexist`. Naming `headerIcon` as *the* problem is FAIL. Then read-only integrity.

**Self-check:** Harbor contains `guid: <KEY_DELETED_GUID>` and `AssetDatabase.GUIDToAssetPath` of that guid is empty. Otherwise drop DBOARD.

**False success:** the tavern board, `headerIcon`, `maxNotices`, or "null / unassigned" without a missing-asset phrase.

**Wrong-tool:** console, capture, any write. Acceptable: hierarchy, `get_serialized_fields`, `get_component_properties`, `search`, `find_gameobjects`.

**Grep rule:** if the run has zero Editor-tool calls, set `solved_without_editor` true. The oracle may still PASS. §8 recomputes the headline contrast without those runs.

### 5.6 DSCENE — scene write (W). TASKS_v1 T7, still valid.

**Prompt (frozen in TASKS_v1):**

> The lantern posts on Pier 3 in the Harbor scene cast shadows and it's too expensive. Turn shadows off for the lanterns on Pier 3 only and set their light intensity to 1.5. Don't change the lantern prefab itself or any other lanterns. Save the scene.

**Oracle:** TASKS_v1 T7 body, unchanged, including the four Pier_03 paths, WorkLight unchanged, prefab asset unchanged, every other `Lamp` light unchanged, and `All(s).Count() == <KEY_TOTAL>`. `<KEY_TOTAL>` is the builder's measured count (band 1,100–1,400 is the build acceptance, not a tolerance on the agent's scene). Scene is opened from disk. Missing key → task not runnable.

**False success:** prefab asset edited, only objects whose names start with `Lantern_Post` changed, WorkLight changed, scene not saved, instances unpacked or replaced.

**Wrong-tool:** console, capture, `eval`. Acceptable: `find_gameobjects`, `get_scene_hierarchy`, `search`, `get_serialized_fields`, `set_serialized_field`, `get_component_properties`, `save_scene`. `unity_dump_scene_graph` is not wrong-tool (it can serve a read) and it **is** a duplicate candidate next to `get_scene_hierarchy` (§6.15).

### 5.7 Explicitly not selected

- **T3** (references): pilot successes were shell greps; the plan's checklist calls that out. Do not put it back.
- **T6** (icon atlas): the pilot fixture never included the tool.
- **T9** (deep hierarchy): fixture form is a single planted cause; the third-party form failed because another true cause existed. Left out so a hard unique plant cannot dominate N=12. Not a spare.
- **T10** overlaps compile. Spare only.

### 5.8 Spares (before run 1 only, in this order)

**T8**, then **T10**. Same prompts and oracles as TASKS_v1. Do not rewrite them.

T8 prompt and pass rule: `ANSWER: cause=<short description>; imported_size=<width>x<height>` with `imported_size` exactly `256x128`, and `cause` matching both a max-size phrase and a platform phrase (`standalone`, `platform`, `override`, `pc`, `mac`, or `windows`). Read-only integrity. Self-check: imported `harbor_map.png` is 256×128 on the active Standalone target. Wrong-tool: writes and `dotnet`. `solved_without_editor` applies.

T10 prompt: "One of our edit-mode tests is failing. Find out which one and fix the code under test so it passes. Don't change the tests." PASS: compile not failed; every file under `Tests/EditMode/` matches `<SHA_TESTS_*>`; TestRunnerApi on `Saltmarsh.Tests.EditMode` is 11 total, 10 passed (the original 9 plus `BulkDiscount_StartsAtHundredUnits`), 0 failed, 1 ignored (`Parse_HandlesUnicodeSuffix`); `PriceTable.cs` differs from pristine. Same single-Editor rule as DCOMP part B. If that rule blocks T10, skip it. Do not invent a third spare.

---------------------------------------------------------------------
## 6. Metrics

Every key below is mandatory in the per-run JSON and in the final table. Omitting one, merging two, or publishing only schema tokens / compression **invalidates** the analysis. Raw schema-token counts and raw compression ratios may be stored as `schema_tokens_appendix` and `compression_ratio_appendix`. They are **not** headline metrics and cannot decide §8.

Denominators:

- **Per successful task** means: sum of that measure over **all** runs of that task on that arm (failures included) divided by the number of `semantic_success` runs. Zero successes → the cell is `undefined`, the task is counted in `undefined_tasks`, and the geometric mean skips it. Do not drop the task from the success table.
- Report the pooled 12-run sums as well as the per-task values.

| # | Key | Definition |
|---|---|---|
| 1 | `semantic_success` | 1 if the §5 oracle returned PASS, else 0. Path does not matter. |
| 2 | `production_policy_success` | 1 only if `semantic_success` is 1 **and** the run did not call `eval`, `eval_file`, `run_script`, or agent `batch` **and**, on S6-P-mini, did not call any tool marked MOCK or `bench_escape` / `bench_bounded_search`. Otherwise 0. Headline success for C1 vs S4/S4+ is `semantic_success`. This key is still mandatory. If the two disagree, report both. Do not collapse them. |
| 3 | `claimed_ok` | Read-only tasks: 1 if the final message has a line matching `^\s*ANSWER:\s*\S`. Write tasks: 1 if the final message matches `fixed`, `compiles`, `no longer failing`, `saved the scene`, `turned shadows off`, or `buttons react` (case-insensitive) **and** does not match `unable to fix`, `could not fix`, `still failing`, or `not fixed`. |
| 4 | `false_success` | 1 iff `claimed_ok` is 1 and `semantic_success` is 0. A silent failure is not a false success. |
| 5 | `total_tokens` | Client-reported total tokens for the run, including cached input, output, and reasoning if the client splits them. Record the client's own field names in `token_fields`. |
| 6 | `total_tokens_per_successful_task` | §6 denominator applied to `total_tokens`. |
| 7 | `uncached_tokens` | Client-reported uncached input tokens. If the client does not split cache, set null and `reason=client_has_no_cache_split`. Do not estimate. A total-token win with uncached null **cannot** be a cost win. |
| 8 | `uncached_tokens_per_successful_task` | Same denominator on `uncached_tokens`. |
| 9 | `calls` | Count of MCP/tool calls, shell commands, and file edits. |
| 10 | `calls_per_successful_task` | Same denominator on `calls`. |
| 11 | `discovery_calls` | Count of: catalog or tag listings, `unity command` listings, client `tool_search` / ToolSearch, code-mode `ALL_TOOLS` filter execs, `nexus_list`, `nexus_describe`, `bench_escape` used only to list. Tag drills count. Ordinary work calls do not. |
| 12 | `tool_search_calls` | Subset of `discovery_calls` that are the client's retrieval API (`tool_search`, ToolSearch, BM25). Code-mode filters and tag picks stay in `discovery_calls` only. If the frozen client has no such API, store 0 and `reason=client_has_no_tool_search`. Do not omit the key. |
| 13 | `wrong_tool_selection` | Count of calls whose tool is on that task's wrong-tool list (§5) or is absent from its acceptable list **and** cannot perform the step. Duplicate Unity vs Nexus tools that can both perform the step are **not** wrong-tool; they increment §6.15 instead. |
| 14 | `invalid_argument_calls` | Tool responses that are schema, unknown-parameter, or validation errors. Do not count them from the agent's apology. |
| 15 | `retries` | A call of the same tool name within the next two calls after an error from that tool. Count each such call. |
| 16 | `wall_time_s` | Seconds from agent start to final message. Also emit `wall_time_per_successful_task` with the §6 denominator, plus the median wall time across the 12 runs. |
| 17 | `unity_time_s` | Sum of tool-side timings if the log has them. If it does not, null and `reason=not_logged`. Do not estimate. |
| 18 | `capability_coverage` | Exactly one of: `compact_or_allowlist_only`, `used_escape`, `used_mock`, `used_pass_through`, `solved_without_editor`, `capability_absent`. S6-P-mini: MOCK or escape or pass-through is never `compact_or_allowlist_only`. A REAL `nexus_capture_game_view` / `nexus_group_compile_errors` / `nexus_project_map` call does not by itself force `used_mock`. |
| 19 | `escape_path_usage` | 1 if the run called `bench_escape`, a Unity command outside the exposed list, or `menu` with a path other than `Assets/Refresh`. Else 0. Arms with no escape tool still score shell or hidden-server calls. 0 is legal only when the log was read. |
| 20 | `topk_correct` | 1 if the **first** retrieval list (tool_search results, or the first code-mode filter dump if that is the client's discovery) contains at least one command in that task's acceptable set (§5). K = **8**. Also record `client_retrieval_limit` as the client actually used it. If the transcript does not contain the list, null and `reason=unobservable`. Do not infer this bit from the tool the agent eventually called. That later call is `first_actionable_call_correct` (1 if the first non-discovery call is in the acceptable set), which is extra and does not replace `topk_correct`. |
| 21 | `duplicate_unity_nexus` | On S4 and S4+ only: 1 if the first top-K list contains ≥1 official Unity name and ≥1 Nexus name from the same cluster below. Else 0. On arms that do not mount both surfaces: null and `reason=not_applicable`. Never store 0 for "Nexus was absent". Clusters: **capture** = `capture_game_view`, `capture_scene_view`, `screenshot`, `nexus_capture_game_view`; **compile** = `recompile`, `recompile_status`, `console`, `console_status`, `wait_for`, `editor_status`, `reload_file`, `nexus_group_compile_errors`; **hierarchy** = `get_scene_hierarchy`, `find_gameobjects`, `search`, `unity_dump_scene_graph`, `unity_hierarchy_manager`. |
| 22 | `tag_discovery_reduced_confusion` | Arm-level, not per run. Computed only if **both** U-FLAT and U-TAGS are RUN. Value `yes` only if U-TAGS has a lower `wrong_tool_selection` rate, a lower `invalid_argument_calls` rate, and `topk_correct` not lower, all versus U-FLAT, on the same tasks. Otherwise `no`. If either arm is NOT RUN: `not_run`. This key does not decide §8.1 or §8.2. |
| 23 | `solved_without_editor` | 1 if a read-only oracle PASSes and the run made zero Editor tool calls. |
| 24 | `capture_default_risk` | DCAP and DUI only. 1 if a call to official `capture_game_view` used the source §2.1 says misses Overlay UI (on the documented 0.7 behaviour, `source=camera` or a default that is camera). Else 0. Null if §2.1 says official Edit Mode screen capture already includes Overlay. |
| 25 | `mock_calls` | Count of calls to tools marked MOCK in §3.7. On other arms, 0. |

`production_policy_success` is how a MOCK is kept out of "production capability". Do not relabel a MOCK call as `semantic_success` by itself. Semantic success stays the oracle.

---------------------------------------------------------------------
## 7. Contamination and budget

- S4 tool descriptions stay the `bfdd0f7` bytes. S4+ text never lands in the package, the skill files, or S4's process.
- S6-P-mini never lands in package code, `list_tools` of the product, or the changelog.
- Agents do not see §5 oracles, answer keys, other arms' transcripts, or this file.
- The shared preamble does not mention overlays, grouping, or which capture source is safe.
- Do not tune §3.4 after a failure.
- Stop at 90 **attempted** runs including the one allowed re-run of a void. That cap covers 6 arms × 12 runs plus voids on the order of the pilot's ~16%. Do not raise it after looking.
- Reserve budget **before** run 1. Minimum that can speak to the hypothesis: **C1 and S4, 24 runs**. If that pair cannot be finished on one Editor, do not start. Optional order, only with budget already reserved: S4+, then S6-P-mini, then whichever of U-FLAT and U-TAGS §2 marks RUN. Do not start an optional arm by stealing runs from the pair.
- Rough time, not a bid: the pilot was about 1.7 minutes per run plus reset, with occasional 8–9 minute runs. 24 runs is a few Editor-hours. 72 runs is the full set. This is not the 900-run Gate F.

---------------------------------------------------------------------
## 8. How to interpret the numbers

Compute three labels. Do not add a fourth. Do not recommend an architecture, a roadmap change, or a product surface. The sentence after the label is the whole recommendation.

### 8.1 Label `C1_MATCHES_CURRENT_NEXUS`

Fire only if **all** of these hold for C1 versus S4 **and** for C1 versus S4+:

- Semantic-success net difference ≤ 2 runs out of 12 (neither side ahead by ≥3), and the same is true after drop-one-task.
- False-success counts differ by ≤ 1 run.
- Either the total-token ratio and the uncached ratio both lie in [0.85, 1.15], **or** tokens differ but `semantic_success`, `false_success`, `wrong_tool_selection` rate, and `topk_correct` do not each move by the large-effect rule. The second case is written `OUTPUT_LENGTH_ONLY` and still fires this label.

`OUTPUT_LENGTH_ONLY` means a shorter payload reproduced the benefit. It is evidence **for** "fewer or shorter official outputs", not for a semantic layer, and not for shipping a curated Unity config either.

S6-P-mini is ignored for this label.

**Read it as:** on this 12-run set, a smaller official Unity list matched the current Nexus baseline. A façade is not justified *by these runs*. Do not change the architecture, the theory-register states, or the plan.

### 8.2 Label `SEMANTIC_GAP_LARGE`

Fire only if **all** of these hold for at least one of S4 or S4+ against C1:

- The large success rule **or** the large false-success rule (§0.3) favors that Nexus arm.
- Drop-one-task keeps the direction.
- The gap is not `OUTPUT_LENGTH_ONLY`.
- `production_policy_success` moves in the same direction. A gap that exists only on `semantic_success` because of `eval` or a MOCK does not fire.
- Recomputing without runs where `solved_without_editor` is 1 still leaves at least 8 paired runs and still meets the large-effect cut. If fewer than 8 paired runs remain, this label does not fire.

If the winner is S4+ and S4 does **not** beat C1, the label is rewritten `DESCRIPTION_TEXT_ONLY`. That says the bench sentences in §3.5 mattered. It does not say a workflow layer or a façade mattered.

If the winner is S4, the gap is about the **current** surface (three canonical commands plus legacy), not about unbuilt `nexus_compile` / `nexus_snapshot` / S6-P.

Capture-specific guard: if §2.1 says official Edit Mode capture already shows Overlay UI, DCAP and DUI success gaps are **not** evidence of a Nexus-only capture semantic. They can still count toward the pooled rule, but the write-up must say the overlay contrast was not available on the frozen Pipeline.

**Read it as:** a large gap remained after catalog size was equalized by C1, and it was not just shorter JSON. That keeps the semantic question open for a *later* preregistration with a real N. It does not authorize S6-P, S5, Mixed, a router, or Gate F.

### 8.3 Label `INSUFFICIENT`

Everything else, including:

- Any mandatory metric missing, null without a reason, or replaced by schema tokens or a compression ratio.
- C1 or S4 not both fully scored (12 non-void runs each).
- Model, reasoning level, client, or Unity channel not identical.
- S4 descriptions not the `bfdd0f7` strings.
- The only arm that beat C1 was S6-P-mini.
- A fixture self-check was waived.
- More than one re-run was used to fill a cell.
- Either side of a comparison was NOT RUN (that comparison only).

**Read it as:** do not update the theory register, do not cite this file plus the 2026-10-03 pilot as two confirmations, and do not schedule architecture work. Insufficient is the result the sample size was built to allow.

### 8.4 S6-P-mini and the Unity discovery arms

- S6-P-mini tables are reported. They are marked **NOT AN S6-P TEST** in every sentence that quotes them. MOCK `not_built` errors are expected. They do not falsify H2.
- U-TAGS vs U-FLAT answers only §6.22. `yes` means tag discovery reduced the pre-registered confusion counts on this set. It does not mean tags replace Nexus, and it does not mean they failed to, unless the label is `no`. `not_run` is silence.

### 8.5 Forbidden sentences

The runner's report must not contain a recommendation to build, ship, or reject a façade, S6-P, Mixed, S5, or "Unity is enough" as a product decision. The three labels above are the only closing lines. Tightening or loosening §0.3 or §8 after run 1 is forbidden. Infrastructure voids (port file, crashed Editor) may be noted. They do not move a threshold.
