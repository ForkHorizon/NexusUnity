STATUS: FROZEN — 2026-10-05. No arm has been run. This file contains no scores, no token counts, and no hypothesis label earned by data.

# Discovery preregistration — cheap paired falsification (not Gate F)

Replaces the unfrozen draft at `H_2026-10-validation/10_DISCOVERY_PREREG.md` for the later scratch-editor run. It does not replace `PREREG_P0-17.md` or `PREREG_GATE_F.md`. It does not schedule S5, full S6-P, Mixed, a façade, write delegation, `eval` / `run_script` forwarding, or an architecture router.

The October 4 draft was not executed (`11_DISCOVERY_RESULTS.md`). This revision is not a rerun report. Contract captures under `evidence/contracts08/` (tag index, recompile JSON, capture pixels) are environment facts. They are not agent results and must not be copied into an agent prompt.

---------------------------------------------------------------------
## 0. What a later runner is allowed to claim

One label per hypothesis, from this closed set: **strong negative**, **strong positive**, **neutral**, **insufficient**. No architecture is confirmed. Do not write SURVIVED, FALSIFIED, CONFIRMED, or a Gate F margin. Do not recommend building, shipping, or rejecting a façade, S6-P, Mixed, S5, or "Unity is enough".

N is 14 scored runs per arm that is actually started (7 tasks × 2 seeds). That can contradict a large, task-stable gap. It cannot see a 15 pp gap. The plan's own power note (about 235–290 paired runs) is why Gate F is out of scope. Insufficient is an allowed result. Do not add seeds, tasks, or metrics after any agent output is visible.

### 0.1 Large-effect rules (frozen)

Paired win: same task and same seed, `semantic_success` 1 against 0. Ties are not wins. Voids are not scored and are not imputed.

- **Large success:** net paired wins ≥ 5 out of 14, the loser has ≤ 1 win, and every leave-one-task-out cut (both seeds of one task removed) still has net ≥ 3 in the same direction.
- **Large false-success:** the counts differ by ≥ 4 out of 14, and every leave-one-task-out cut still differs by ≥ 3 in the same direction.
- **Large cost:** geometric mean, across tasks, of (arm A `tokens_per_success` ÷ arm B `tokens_per_success`) is ≥ 1.50 or ≤ 0.67, on at least 5 tasks where both arms have ≥ 1 semantic success, and the uncached-token ratio is on the same side of 1.0. If uncached tokens are null, there is no cost win.
- **Match band:** success net ≤ 2 and false-success counts differ by ≤ 1, and both still hold after every leave-one-task-out cut.
- **Output-length only:** success and false-success stay inside the match band, but the large-cost rule fires. Record it. It is not a semantic win.

### 0.2 Hypotheses H1–H5

Wording of the plan register (`MASTER_PLAN_V2.md` §18) is the hypothesis. The contrast this protocol can actually score is narrower. A label applies only to that contrast.

| Id | Plan claim | Contrast this file scores | What the label must not say |
|---|---|---|---|
| **H1** | A compact Nexus benefit is not explained just by exposing fewer official Unity tools. | **C1 vs S4.** C1 is the critical control. | Not a product decision. Not "Unity is enough". |
| **H2** | A compact typed Nexus-primary surface beats the current design without losing success. | **S6-P-mini vs S4** only. | Not plan S6-P. Not the pilot's 154 actions behind domain managers. Unbuilt `nexus_status` / `snapshot` / `diagnose` / `compile` / `run` / `get` are absent on purpose. A bad mini does not falsify those composites. A good mini does not confirm them. |
| **H3** | Mixed boundary: Nexus for common workflows, Unity-direct only for rare capabilities. | **No M arm.** T-OUT records escape share. Plan H3 stays **insufficient**. | Do not relabel U-TAGS as Mixed. |
| **H4** | Cheap discovery metadata closes most of the gap without a larger façade. | **S4+ vs S4**, with **E-SKILL vs C1** as the control. | Not option-E recipes, not hidden dumps, not a rewrite. |
| **H5** | One warm transport is lower-overhead and must not be confounded with the façade. | **No agent contrast.** | A wrong port or wrong CLI voids H1–H4. It is not an H5 result. |

**H1 label**

- **Strong negative** if the match band holds for C1 versus S4, or C1 beats S4 on the large success rule or the large false-success rule. Mandatory sentence: a match kills only a large gap on this set. It does not confirm an architecture and it does not bound a smaller gap.
- **Strong positive** if S4 beats C1 on the large success rule or the large false-success rule, the gap is not output-length only, `production_policy_success` moves the same way, and the cut still holds after dropping runs with `solved_without_editor` = 1 (at least 8 paired runs must remain). S6-P-mini is not this contrast. If only the mini beats C1, H1 is not strong positive.
- **Neutral** if both arms are fully scored and neither bullet above fires, including output-length only.
- **Insufficient** if either arm lacks 14 non-void runs, a headline metric is missing, or any §9 invalidity hits the pair.

**H2 label**

- **Strong positive** if S6-P-mini beats S4 on the large success or false-success rule, not output-length only, `production_policy_success` agrees, and in-surface tasks (everything except T-OUT) are not carried by `bench_escape`. Every sentence that quotes the label says **NOT AN S6-P TEST**.
- **Strong negative** if S4 beats S6-P-mini on the large success or false-success rule. A mini that was never built, or that was built as managers, does not count.
- **Neutral** if both are fully scored and neither large rule fires, including a match and including output-length only.
- **Insufficient** otherwise, and always if §3.6 was violated.

**H3 label**

Always **insufficient** for the plan hypothesis. Still report, as a table and not as a label: on S6-P-mini, the fraction of runs that called `bench_escape`, split by T-OUT versus the other six tasks. U-TAGS versus S4 may be described with the same four words only under the heading **tag observation, not H3**. That observation uses the H1 numeric rules with U-TAGS in the role of S4. It does not choose an architecture.

**H4 label**

- **Strong positive** only if S4+ beats S4 on the large success or false-success rule, not output-length only, **and** E-SKILL does not beat C1 on that same rule.
- **Strong negative** if E-SKILL beats C1 on the large rule and S4+ does not beat S4, or if S4 beats S4+ on the large rule.
- **Neutral** if S4+ beats S4 and E-SKILL also beats C1 (write `SKILL_TEXT_ALSO_LARGE`), or if no large rule fires, or if the only gap is output length.
- **Insufficient** if E-SKILL, S4, or S4+ is not fully scored. S4+ beating S4 without the E-SKILL control is insufficient, not strong positive.

**H5 label**

Always **insufficient**. Do not infer it from wall time. Transport checks in §2 are validity gates.

---------------------------------------------------------------------
## 1. Freeze block

Filled from the compatibility sprint's measured environment. Model, client, and the fixture-key hash are the only cells a runner may write, and only before run 1, into a log line. Writing them is not a license to change an arm, a task, a threshold, or a port.

| Field | Frozen value |
|---|---|
| Date frozen | 2026-10-05 |
| Status | FROZEN. Zero agent runs. |
| Editor | Unity `6000.3.16f1` at `/Applications/Unity/Hub/Editor/6000.3.16f1`, scratch project only |
| Pipeline | `0.8.0-exp.1` on `/tmp/nexus-p08-harness`. Not the harness `0.7.0-exp.1`. |
| CLI | `/tmp/unity-cli-beta12/unity` `1.0.0-beta.12`, sha256 `7f21746fe7b2cf1010a7f293763ad9590715166147dcc0401a7b52de0d95f107` |
| CLI forbidden | `~/.unity/bin/unity` and any other binary. That path is `1.0.0-beta.10`. |
| Project path | `/tmp/nexus-p08-harness` on every CLI invocation |
| Port | Scratch `ProjectSettings/Packages/com.unity.pipeline/EditorPipelineConfig.json` field `m_Port` = **7810**, set before launch. Expected bind `127.0.0.1`. |
| Port forbidden | `7800`. The harness Editor PID 4465 listens on `*:7800`. The stopped 0.8 editor had bound `127.0.0.1:7800`. |
| Package pin | Descriptions and the agent-visible Nexus command set match git `bfdd0f7e71c851fb4615956f3c420940a9623220`. An asmdef reference to `Unity.Pipeline.Attributes` may be present so 0.8 compiles. It must not add a command or a description line. |
| Channel | CLI only, absolute beta.12 binary. `unity mcp` is not an arm and not a fallback. |
| Seeds | `17` and `43`. They permute arm order only. |
| Model | blank until the pre-run addendum |
| Client and reasoning | blank until the pre-run addendum. One id, one reasoning level, one context window, one tool-search mode, all arms. |
| Fixture key sha256 | blank until the builder hashes the key, before run 1 |

Harness PID 4465 stays up and is not a target. No `Unity -runTests`. No second editor on 7800. No call to port 7800 from this study.

---------------------------------------------------------------------
## 2. Pre-run gates

Any failed gate means do not start. Do not "fix" a gate by pointing the CLI at the harness.

1. `m_Port` is 7810 in the scratch config before the scratch editor process exists. The harness `ProjectSettings` are not edited.
2. After launch, the first CLI JSON `target.port` is 7810 and `target.projectPath` is `/tmp/nexus-p08-harness` (or `/private/tmp/nexus-p08-harness`). A 7800 or a harness path stops the study.
3. `/tmp/unity-cli-beta12/unity` is the binary in every call, including oracles. `--project-path /tmp/nexus-p08-harness` is present. A PATH lookup of `unity` is not used.
4. Bare `unity command` on that pair returns a `tags` array (progressive discovery), not the flat schema dump. The sample `evidence/contracts08/tags.stdout.txt` (50 tags, taken while the scratch editor was on port 7800) may be used only to check that the new process still returns tags. It is not shown to the agent and it is not a result.
5. S4 description strings for `nexus_project_map`, `nexus_group_compile_errors`, and `nexus_capture_game_view` match `bfdd0f7` byte for byte. Any added "Use when", action list, or `_meta` hint means S4 is invalid.
6. The agent process cannot read this file, `TASKS_v1.md`, `07_DISCOVERY_RESULTS.md`, the answer key, or `evidence/contracts08/`. Root the agent at the scratch project. An unrestricted shell that can open the package worktree makes the run invalid.
7. `Assets/P08ProbeErrors/` is not in the scratch project the agent sees. Those files are the recompile-contract probe, already written up, and are not a task.
8. Model, client, reasoning level, and key hash are written in the addendum. The same values are used for every arm.

---------------------------------------------------------------------
## 3. Arms

Shared preamble, byte-identical, first paragraph of the only instruction file:

> This is a Unity 6 project. A Unity Editor is running with this project open. Inspect and change Editor state with the tools this session actually exposes. Do not edit scene or prefab YAML by hand. Save scenes you change. Do not claim the project compiles because a shell or `dotnet` build succeeded.

One extra sentence per arm. It does not name another arm. No arm is told which capture source sees overlay UI, which port is correct, or that a warning may be missing from `recompile_status`.

Arm order inside a task is a rotation of this list by `seed mod 6`, starting at that index: `C1`, `E-SKILL`, `S4`, `S4+`, `S6-P-mini`, `U-TAGS`. Seed 17 starts at index 5. Seed 43 starts at index 1.

### 3.1 U-TAGS

Official progressive discovery on the frozen pair. Bare `unity command` is the tag index. The agent may pass `--tag`, `--query`, `--limit`, and `--detail compact`. The runner must not pre-expand tags and must not put `--detail full` in the preamble. If the agent itself asks for the full catalog, count that as a discovery call; do not restart the arm to stop it.

No Nexus server, no `nexus_*` tool, no Nexus skill, no rewritten descriptions. This is not a hand-trimmed list (that would be C1).

Instruction sentence: "Editor commands are the official Unity CLI. List tags before listing every command."

### 3.2 C1 — critical control for H1

Official commands only, official names, official description strings, official defaults. No `nexus_*`. No skill document. No rewritten descriptions.

The visible set is the allowlist and nothing else. Discovery is the frozen tag mode filtered to tags that still contain at least one allowlisted command. Drilling a tag shows only allowlisted commands. Do not invent a third discovery UI.

Excluded on purpose: `eval`, `eval_file`, `run_script`, agent `batch`, and the Pipeline `batch` command. Reaching one of them is an allowlist escape, not a C1 capability.

| Need | Locked official names |
|---|---|
| Scene read | `get_scene_hierarchy`, `find_gameobjects`, `search`, `get_selection` |
| Component read/write | `get_component_properties`, `get_serialized_fields`, `set_serialized_field` |
| Save | `save_scene` |
| Capture, official defaults | `capture_game_view`, `capture_scene_view`, `screenshot` |
| Compile / console | `recompile`, `recompile_status`, `console`, `console_status`, `editor_status`, `wait_for`, `reload_file` |
| Refresh | `menu` |
| Tests | `run_tests` |

`unity recompile` is the CLI wrapper of `recompile`, not an extra tool. `menu` counts as escape-path usage unless the path is `Assets/Refresh`. Before run 1, delete a name the live tag drill shows is absent. Do not add a substitute. If that deletion removes the last command in a row, the task gets harder. No second edit after run 1.

Instruction sentence: "Editor commands are the official Unity CLI names exposed in this session and no others."

### 3.3 E-SKILL — control for H4

Same callable allowlist as C1, same official names and official descriptions, same exclusions. The only added factor is skill text.

Before run 1, capture stdout of `/tmp/unity-cli-beta12/unity skill show` once, store its sha256 in the addendum, and append that exact text to the E-SKILL instruction file. If `skill show` fails, E-SKILL is **not run**. Do not paste a web copy, a Nexus skill, a domain skill, or option-E recipes. Do not `unity skill install` into a client that other arms also load. `--caller` and `--skill` are not part of the arm.

Instruction sentence: "Editor commands are the official Unity CLI names exposed in this session. Follow the official skill text included below."

### 3.4 S4 — frozen current baseline

Official Unity CLI in the default tag mode of §3.1, plus Nexus as `bfdd0f7` exposes it.

- Canonical commands, these three only: `nexus_project_map`, `nexus_group_compile_errors`, `nexus_capture_game_view`. Real schemas. Real results.
- `nexus_capture_game_view` keeps the S-07 defaults already on that tip (JPEG, default long side 768). Do not retune them. `nexus_look` is not a command name.
- `nexus_group_compile_errors` groups logs. It does not refresh, recompile, or own the compile pipeline. It is not `nexus_compile`.
- `nexus_project_map` is not a scene snapshot.
- The legacy Nexus surface stays visible, including dumps such as `unity_dump_scene_graph`. Hiding dumps would be S-10. It is out of this arm.
- Descriptions are the `bfdd0f7` bytes. **No new discovery metadata.** A proxy that rewrites them voids S4.
- Runtime mode stays the commit default (`auto`).

Instruction sentence: "Unity tools are the official Unity CLI. Nexus tools are the installed `nexus-unity` commands, including `nexus_project_map`, `nexus_group_compile_errors`, and `nexus_capture_game_view`."

### 3.5 S4+ — description overlay only

S4 binaries plus bench-only description text. Not a build. Not committed. A proxy under `NexusFeature~/bench/` may forward calls and must return the unmodified tool result. If a forwarded body differs from S4 for the same arguments, that S4+ run is void.

Allowed text changes, and no others:

1. For each legacy Nexus manager whose schema is a `oneOf`, append one line to the top-level description: `Actions:` and the action names that manager already has. Do not add actions. Do not change parameters.
2. Prepend exactly one sentence, and do not change the rest of the description:
   - `nexus_project_map`: "Use when you need the project map this command already returns."
   - `nexus_group_compile_errors`: "Use when you need grouped compile and console errors. This command does not recompile."
   - `nexus_capture_game_view`: "Use when you need the Game View image this command already captures."
3. If the client reads `_meta` search hints, those hints may repeat the words added above. If it ignores hints, log `hints unused`. Do not add another prose channel.
4. Do not rename tools, hide dumps, add recipes, shorten outputs, or change JPEG size, limits, or envelopes.

This S4+ is not plan S4+. Plan S4+ also hid legacy dumps and added option-E recipes. Those are not factors here.

Instruction sentence: the same words as S4.

### 3.6 S6-P-mini — small semantic surface, not 13 managers

Bench only. Never committed as package code. Not plan S6-P.

The agent sees a closed list:

| Exposed name | What it is | Catalog policy |
|---|---|---|
| `nexus_project_map` | Real `bfdd0f7` command, S4 description, not the S4+ sentence | Shipped composite |
| `nexus_group_compile_errors` | Real command. Grouping only. Not compile. | Shipped composite |
| `nexus_capture_game_view` | Real command, S-07 defaults | Shipped composite |
| The §3.2 allowlist, official names and official descriptions | Official Unity backend. Calls run the real CLI command. Bodies are not rewritten. | Backend, not a Nexus semantic |
| `bench_escape` | One official command **not** on the allowlist and not a `nexus_*` name. Arguments unchanged. Body unmodified. Refuses `eval`, `eval_file`, `run_script`, and `batch`. Does not list the catalog. | Rare escape. Counted. Not a compact-surface success |

Not exposed, and an invalid arm if any of these appear:

- A domain manager, a `oneOf` over the official catalog, or any schema that is the 154-command list under another name.
- The legacy ~120 Nexus tools, including `unity_dump_scene_graph`.
- Mock tools that return `not_built` or invented Editor state. The October 4 mock table is withdrawn. Those mocks are not this arm.
- A search tool whose result is the full command list.

`bench_escape`'s description is one sentence: "Run one official Editor command that is not otherwise exposed. Pass its name and arguments. This does not list commands." No examples that name the T-OUT command.

Instruction sentence: "Editor tools are the commands listed for this session. Prefer those. One escape command can run a single official command that is not listed."

A success on T-COMP, T-SCENE, T-VIS, T-RARE, T-AMB, or T-BULK that depended on `bench_escape` can still be `semantic_success` and is not `production_policy_success`. T-OUT may use `bench_escape`. A T-OUT success that did not need the escape because the command was already on the closed list means the arm was built wrong and **every** S6-P-mini run is invalid.

---------------------------------------------------------------------
## 4. Design the runner must not relax

- Paired: same task, same seed, every arm that this study started, before the next task. Reset the fixture between runs.
- Prompts in §5 are byte-identical across arms.
- One scratch editor on 7810. The harness is not an arm, an oracle host, or a fallback.
- Blind semantic score: the oracle sees the final message and the scratch editor. It does not see the arm id. Join the arm id after PASS/FAIL is stored.
- Per-run oracles may run immediately so a broken fixture is caught. Cross-arm ratios and labels are computed only after every planned run has a score or a void.
- Pilot normalisers are not reused.
- One JSON object per run. Every headline key in §6 is present. Use `null` and a `reason` string when a value is unobservable. A missing headline key invalidates the analysis.
- Void, re-run once, then leave void: editor crash, port not 7810, wrong project path, wrong CLI, transcript missing, oracle process failed, arm saw the wrong tool list, S4 description was not the frozen string, S4+ body differed from S4, agent saw this file or an answer key, S6-P-mini matched §3.6's invalid list. A wrong answer is a failure, not a void.

Stop at 100 attempted runs, including the one allowed re-run of a void. Do not raise the cap after looking.

Minimum that can speak to H1: C1 and S4, 28 runs, on the one scratch editor. If that pair cannot be finished, do not start. Optional order, only with budget already reserved: E-SKILL, S4+, S6-P-mini, U-TAGS. Do not steal runs from C1 or S4 to start an optional arm.

---------------------------------------------------------------------
## 5. Tasks

Seven tasks. Prompts do not name a tool, a server, a product, a port, or a capture source. The builder creates `Assets/DiscFixture/` in the scratch project before run 1, outside the agent instruction set. Names inside the fixture must not contain bench, planted, decoy, bug, oracle, task, answer, or trap.

The answer key lives outside the scratch project and outside the agent root. Its sha256 is logged before run 1. Editing the key after any agent output voids the study.

Self-checks below are harness checks, run with the beta.12 binary against port 7810, before run 1. A failed self-check drops that task for the whole study. Do not replace a task after an agent result. If fewer than 5 tasks remain, abort. The only spare is §5.8, and only before run 1.

Reset between runs: restore `Assets/DiscFixture` and `ProjectSettings` from the pre-run snapshot, clear the console, leave Edit mode if the agent entered Play mode, and confirm the editor is not compiling. Record a Play-mode exit. Do not start another editor to do it.

Read-only integrity for every task except where a write is requested (none of the seven request a write): `git status --porcelain` on the scratch `Assets`, `ProjectSettings`, and `Packages` shows no agent change, and the open scene is not dirty. Violation → semantic FAIL, category `mutated`. Console clearing is not a mutation.

### 5.1 T-COMP — compile diagnosis

**Prompt:**

> The project does not compile in the Unity Editor. List each distinct compiler error once, with file, line, and compiler code. Do not list warnings. Do not change anything.
> Finish your reply with one line in exactly this form:
> `ANSWER: count=<number>; errors=<file>:<line>:<code> | <file>:<line>:<code> | ...`

**Plant, before run 1:** three compiler errors across two new files under `Assets/DiscFixture/Scripts/`, plus one `#warning` whose text is in the editor log. Do not reuse `Assets/P08ProbeErrors`, `One.cs`, `Three.cs`, `Warn.cs`, or the string `missingName`. The harness runs `unity recompile` and stores the structured `file`, `line`, and `code` of the three errors in the key.

**Self-check:** that call returns exactly three structured errors, and `recompile_status.errors` does not contain the warning. Otherwise drop T-COMP.

**Oracle:** parse the last `ANSWER:` line. `count` is 3. Each key token `file:line:code` is present. Directory prefixes are allowed. Order is free. Any warning token, any extra error token, or a missing line is FAIL. Then read-only integrity.

**False success (still FAIL):** a warning reported as an error; a claim that the project compiles; treating a dropped-connection exit 6 with an empty error list as if it were the error list; a `dotnet` or `msbuild` success.

A single official recompile that lists the three errors is a PASS. Grouping is not required. That is intentional for H1.

**Wrong-tool:** hierarchy, capture, scene writes, `dotnet`, `msbuild`, `csc`. **Acceptable:** `recompile`, `recompile_status`, `console`, `console_status`, `editor_status`, `wait_for`, `reload_file`, `menu` path `Assets/Refresh`, `nexus_group_compile_errors`.

### 5.2 T-SCENE — scene state

**Prompt:**

> In the open scene, one referenced asset does not resolve. Say which object, which component, which field, and what is wrong. Do not change anything.
> Finish your reply with one line in exactly this form:
> `ANSWER: object=<hierarchy path>; component=<component type>; field=<field name>; problem=<short description>`

**Plant:** scene `Assets/DiscFixture/Scenes/Yard.unity` open. Object path ending in `Market/Board`. Component `NoticeBoard`. Field `noticeTemplate` holds a guid for which `AssetDatabase.GUIDToAssetPath` is empty. Field `headerIcon` is present and is not the missing guid. No other missing script reference on that object.

**Self-check:** the empty-path guid check above. Otherwise drop T-SCENE.

**Oracle:** normalise `object` by trimming, collapsing spaces around `/`, and stripping an optional `Yard.unity:` prefix. PASS only if the path ends with `Market/Board`, `component` is `NoticeBoard` (case-insensitive), `field` matches `notice template` with the space optional, and `problem` matches `missing`, `deleted`, `no longer exist`, `does not exist`, `broken`, or `nonexist`. Naming `headerIcon` as the problem is FAIL. Then read-only integrity.

**False success:** the other object, `headerIcon`, or "null" without a missing-asset phrase.

**Wrong-tool:** console, capture, any write. **Acceptable:** `get_scene_hierarchy`, `find_gameobjects`, `search`, `get_component_properties`, `get_serialized_fields`.

**Grep rule:** zero Editor-tool calls and an oracle PASS sets `solved_without_editor` true. §0.2 still drops those runs before a strong-positive H1 label.

### 5.3 T-VIS — visual state

**Prompt:**

> Look at the Game view. What is drawn over the scene at the top left, and what color is the scene marker along the top edge? Do not change anything.
> Finish your reply with one line in exactly this form:
> `ANSWER: overlay=<object name>; top_edge=<color>`

**Plant:** a Screen Space Overlay object named `ShadeBand`, enabled, alpha ≥ 0.3, on a canvas sorted above the world, with a raycaster. A world quad `RedMarker` occupies the top edge of the Game view. A Game view render target exists before any agent run. Do not reuse the contract-capture scene's measured pixel fractions as the key.

**Self-check, harness, before run 1:** omitted `capture_game_view` reports `source=screen` and includes `ShadeBand`; `source=camera` shows `RedMarker` and does not include `ShadeBand`. This matches the 0.8 contract capture's direction (screen includes overlay, camera does not) but must be re-checked on port 7810. If the self-check fails, drop T-VIS and take §5.8. Do not tell the agent the source split.

**Oracle:** `overlay` is `ShadeBand` and `top_edge` is `red` (case-insensitive). Anything else is FAIL. Then read-only integrity.

**False success:** `overlay=RedMarker`; `top_edge` any color but red; a claim that nothing is drawn over the scene.

**Wrong-tool:** console, recompile, `dotnet`. **Acceptable:** `capture_game_view`, `capture_scene_view`, `screenshot`, `get_scene_hierarchy`, `find_gameobjects`, `get_component_properties`, and on S4/S4+ `nexus_capture_game_view`. Calling official `capture_game_view` is not wrong-tool. Set `capture_default_risk` true when the logged source is `camera`. Omitted source and `source=screen` set it false. This field does not change PASS/FAIL.

Overlay success is not a Nexus-only semantic. On this Pipeline, official `source=screen` already includes overlay in Edit Mode when a Game view target exists (`04_CAPTURE_08.md` measured that on the previous scratch process; the self-check must show it again on 7810). A T-VIS gap can still enter the pooled counts. The write-up must say the overlay contrast was not Nexus-only. Scene-target framing is not an oracle. It was not established by the contract captures.

### 5.4 T-RARE — rare capability

**Prompt template, filled before run 1, command name omitted:**

> Report the current Editor value described here, and nothing else: `<one sentence taken from that command's official description, with the command name and flag names removed>`.
> Finish your reply with one line in exactly this form:
> `ANSWER: value=<short value>`

**Selection rule, applied once to the port-7810 tag index, before run 1:** among tags with `count` ≤ 3, take commands that are not on the C1 allowlist, not `nexus_*`, and whose names do not contain `eval`, `run_script`, or `batch`. Sort by tag, then by command name. Pick the first command that has no required write argument. The harness calls it, stores the value in the key, and writes the prompt sentence from its official description without the command name.

**Self-check:** the harness call returns a scalar or a short string the oracle can compare exactly, and the name is absent from the C1 allowlist. Otherwise skip to the next name in that same order, at most three skips, all before run 1. If none qualify, drop T-RARE.

**Oracle:** the last `ANSWER:` line's `value` equals the key after trim and case-fold. Then read-only integrity.

**Wrong-tool:** capture, scene writes, `dotnet`. **Acceptable:** that selected command, and a tag or query listing that was used only to find it.

### 5.5 T-AMB — tool ambiguity

**Prompt:**

> The Unity console is noisy, but the scripts compile. How many of the current console entries are compiler errors, as opposed to warnings, exceptions, or ordinary logs? Name the file and line of each compiler error. Do not change anything.
> Finish your reply with one line in exactly this form:
> `ANSWER: count=<number>; errors=<file>:<line> | <file>:<line> | ...`

**Plant:** compile is up to date. An editor script under `Assets/DiscFixture/` logs two exceptions and one warning. Each message cites a `.cs` file and a line. None of those messages contain `error CS`. The script runs without a domain reload initiated by the agent.

**Self-check:** `unity recompile` is `up_to_date` with zero errors, and the console contains the three decoys. Otherwise drop T-AMB.

**Oracle:** `count` is 0 and `errors` is `none` or empty. Any file:line token is FAIL. Then read-only integrity.

**False success:** an exception or the warning reported as a compiler error; a non-zero count.

**Wrong-tool:** hierarchy, capture, scene writes. **Acceptable:** `recompile`, `recompile_status`, `console`, `console_status`, `nexus_group_compile_errors`. Using both an official console or recompile command and `nexus_group_compile_errors` is not wrong-tool. It increments `duplicate_unity_nexus` when both names appear.

`solved_without_editor` applies. A guessed zero with no Editor call is not semantic evidence.

### 5.6 T-BULK — large-output avoidance

**Prompt:**

> In the open scene, what is the Intensity value on the light named PierLamp? Do not change anything.
> Finish your reply with one line in exactly this form:
> `ANSWER: intensity=<number>`

**Plant:** `Yard.unity` contains at least 1,000 transforms. Exactly one light is named `PierLamp`. Its intensity is the key, a value other than the engine default 1. `get_scene_hierarchy` is left unbounded. The 0.8 manual still documents only a scene `path` for that command. Do not add a depth parameter for the study.

**Self-check:** the transform count is ≥ 1,000 and a component read of `PierLamp` returns the key. Otherwise drop T-BULK.

**Oracle:** `intensity` parses as a number within 0.01 of the key. Then read-only integrity.

**Wrong-tool:** console, recompile, capture, `dotnet`. **Acceptable:** `find_gameobjects`, `search`, `get_scene_hierarchy`, `get_component_properties`, `get_serialized_fields`, and on S4/S4+ `unity_dump_scene_graph`. A full hierarchy dump is not wrong-tool. Set `large_output_used` true when any single tool result exceeds 50,000 bytes. That flag does not change PASS/FAIL.

T-BULK cannot by itself produce a strong-positive success label. A token gap with tied success is output-length only or neutral.

### 5.7 T-OUT — outside the compact surface

Same prompt template and oracle shape as T-RARE. The selection rule starts at the next eligible command after T-RARE's choice and must use a different top-level tag. The chosen command is not on the C1 allowlist, not one of the three Nexus composites, and not the T-RARE command.

**Arm rules for this task only:**

- C1 and E-SKILL do not expose the command. A PASS that called it anyway is `escape_path_usage` = 1 and `production_policy_success` = 0. A PASS with no Editor call is `solved_without_editor`.
- U-TAGS may drill to it. That is ordinary discovery for this arm, not an allowlist escape. Set `capability_coverage` to `official_outside_c1`.
- S4 and S4+ may reach it through the official tag index they already mount. That is not Nexus coverage.
- S6-P-mini PASS requires `bench_escape` for `production_policy_success`. If the command was on the closed list, the arm is invalid (§3.6).

**Wrong-tool:** the C1 allowlist is not wrong-tool when used as a failed attempt. Capture and `dotnet` are wrong-tool. **Acceptable:** the selected command, tag or query listings, and `bench_escape` when it forwards that command.

### 5.8 Spare, before run 1 only

Use only if T-VIS fails its overlay self-check. Do not use it to replace a task the agent has already seen.

**T-SPARE prompt:**

> The imported sprite `Assets/DiscFixture/Art/mark.png` looks soft in the Editor. Report its imported max size on the active Standalone target, and why that size is in effect.
> Finish your reply with one line in exactly this form:
> `ANSWER: imported_size=<width>x<height>; cause=<short description>`

**Oracle:** `imported_size` is exactly the key (the builder sets the import so the active Standalone max size is not the source texture's pixel size), and `cause` matches both a max-size phrase and one of `standalone`, `platform`, `override`, `pc`, `mac`, `windows`. Read-only integrity. Self-check: the importer reports that size. Wrong-tool: writes and `dotnet`. `solved_without_editor` applies.

No second spare. Do not revive TASKS_v1 T3, T6, T9, or T10 inside this protocol.

---------------------------------------------------------------------
## 6. Headline metrics

Every key below is mandatory. Omitting one, or publishing schema-token counts or a compression ratio in its place, invalidates the analysis. Those extras may be stored as `schema_tokens_appendix` and cannot decide §0.2.

**Per success:** sum of the measure over all runs of that task on that arm, failures included, divided by the number of `semantic_success` runs. Zero successes → `undefined`. The geometric mean skips undefined tasks and does not drop them from the success table. Also report the pooled sums.

| Key | Definition |
|---|---|
| `semantic_success` | 1 if the §5 oracle returned PASS, else 0. |
| `false_success` | 1 if `claimed_ok` is 1 and `semantic_success` is 0. A silent failure is not a false success. |
| `tokens_per_success` | The per-success denominator applied to client-reported total tokens (cached input, output, and reasoning if the client splits them). Field names go in `token_fields`. |
| `uncached_tokens` | Client-reported uncached input tokens, plus `uncached_tokens_per_success`. If the client does not split cache, null and `reason=client_has_no_cache_split`. Do not estimate. A total-token win with uncached null is not a cost win. |
| `calls` | MCP or CLI tool calls, shell commands, and file edits. Also `calls_per_success`. |
| `discovery_calls` | Catalog or tag listings, `unity command` listings, `--tag` / `--query` / `--detail` listings, client tool search, and `bench_escape` used only to list. Ordinary work calls are not discovery calls. |
| `wrong_tool` | Calls on that task's wrong-tool list, or calls that are outside its acceptable list and cannot perform the step. A Unity command and a Nexus command that can both perform the step are not wrong-tool. |
| `invalid_args` | Tool responses that are schema, unknown-parameter, or validation errors. Do not score these from the agent's apology. |
| `retries` | A call of the same tool name within the next two calls after an error from that tool. |
| `wall_time_s` | Seconds from agent start to final message. Also the median across the arm's scored runs. |

Definition of `claimed_ok`, recorded because false success depends on it: read-only tasks, 1 if the final message has a line matching `^\s*ANSWER:\s*\S`. There are no write tasks in this protocol.

Also recorded, not a substitute for a headline key:

- `production_policy_success`: 1 only if `semantic_success` is 1 and the run did not call `eval`, `eval_file`, `run_script`, or `batch`, and did not call a tool outside that arm's exposed list. On S6-P-mini, `bench_escape` is allowed only on T-OUT. If this disagrees with `semantic_success`, report both.
- `escape_path_usage`: 1 if the run called `bench_escape`, a command outside the exposed list, or `menu` with a path other than `Assets/Refresh`.
- `solved_without_editor`: 1 if a read-only oracle PASSes with zero Editor tool calls.
- `duplicate_unity_nexus`: on S4 and S4+ only, 1 if the run called at least one official name and at least one Nexus name from the same cluster. Capture cluster: `capture_game_view`, `capture_scene_view`, `screenshot`, `nexus_capture_game_view`. Compile cluster: `recompile`, `recompile_status`, `console`, `console_status`, `wait_for`, `editor_status`, `reload_file`, `nexus_group_compile_errors`. Hierarchy cluster: `get_scene_hierarchy`, `find_gameobjects`, `search`, `unity_dump_scene_graph`. Other arms: null, `reason=not_applicable`.
- `capture_default_risk` and `large_output_used` as defined in §5.3 and §5.6.
- `capability_coverage`: one of `closed_list`, `official_outside_c1`, `used_escape`, `solved_without_editor`.

---------------------------------------------------------------------
## 7. Contamination

- S4 descriptions stay the `bfdd0f7` bytes. S4+ text never lands in the package, the skill files, or S4's process.
- S6-P-mini never lands in package code, product `list_tools`, or the changelog.
- Agents do not see §5 oracles, the key, other arms' transcripts, this file, or `evidence/contracts08/`.
- The shared preamble does not mention overlays, grouping, tag counts, or which capture source is safe.
- Do not tune the C1 allowlist after a failure.
- Do not pool these runs with the 2026-10-03 pilot, with Stage A, or with Stage B.

---------------------------------------------------------------------
## 8. What makes a run invalid

Any one of these voids the affected runs. If it can have changed the editor the other arms talked to, void the study. Do not impute. Do not relabel a void as failure or as a hypothesis result.

1. **Wrong port.** Scratch editor not on 7810, any CLI `target.port` of 7800, or any dial of the harness on `*:7800`. The 0.8 editor was stopped because it bound `127.0.0.1:7800` while PID 4465 still listened on `*:7800`. Repeating that is invalid, not a retry.
2. **Wrong CLI.** Any binary other than `/tmp/unity-cli-beta12/unity` `1.0.0-beta.12`, including `~/.unity/bin/unity` (`1.0.0-beta.10`). A call that omits `--project-path /tmp/nexus-p08-harness` is the same class of invalid.
3. **The agent sees the oracle.** This file, the answer key, TASKS_v1, another arm's transcript, or the contract-capture JSON was readable by the agent.
4. **S4 receives new discovery metadata.** Description bytes differ from `bfdd0f7`, including "Use when" lines, `Actions:` lines, search hints, renamed tools, or hidden dumps. Those belong only to S4+, and only as specified in §3.5.
5. **S6-P-mini hides a full catalog behind manager names.** Domain managers, a `oneOf` or router over the official catalog, the legacy dump surface, or a tool list that is the flat catalog under shorter names. That is the pilot shape this arm exists not to repeat. Invalid, not a strong negative and not a strong positive.
6. Also invalid: a second scratch editor, `Unity -runTests`, S4+ result bodies that differ from S4, a mock payload, model or client not identical across arms, more than one re-run used to fill a cell, a fixture self-check waived, or labels computed before the last planned run was scored or voided.

H5 does not absorb these. They are not evidence that a transport is better. They are evidence the comparison did not happen.

---------------------------------------------------------------------
## 9. Results

None. Do not fill this section from `tags.stdout.txt`, from the recompile contract, from the capture pixel table, or from the 0.7 harness. Those files are not this experiment.
