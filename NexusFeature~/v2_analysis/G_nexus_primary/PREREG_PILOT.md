# PREREG — P0-16 Nexus-primary pilot (v1, written before any S4/S5/S6 task run)

Date: 2026-10-03. Author: lead agent. Status: **DRAFT → frozen after critic review (Agent C), before run 1.**
Any change after the first scored run goes to an amendment section with a timestamp and a reason, and is reported.

## 0. What this pilot can and cannot decide
- It is a **pilot**: one client (Codex CLI), one model, N=1–2 per cell, ~54 runs. It validates the harness and gives
  **direction** for the full P0-16 run. It **cannot** pass or fail Gate F and cannot select an architecture.
- Expected headline verdict for every comparison: **INSUFFICIENT EVIDENCE** (statistically), plus a directional label
  (§5). The plan may change *research priorities* from it, never schedule implementation from it.
- Lead-agent contamination, disclosed: the lead wrote the mock façade and the normalisers **after** reading TASKS_v1.md.
  Mitigation: normaliser features are frozen to the v2.4 pre-task spec (bounded hierarchy root/depth/name/component
  filter/limit + sibling collapse; grouped console; bounded find; null/id stripping on component reads; handles).
  No property-value queries, no task-specific logic. The tasks themselves were written blind by Agent G.

## 1. Client and environment (fixed)
- Codex CLI 0.160.0, `codex exec --json`, model `gpt-6-luna`, `model_reasoning_effort="medium"` (user default is high;
  medium for run budget, same for all arms), default tool exposure for this model (code-mode: the model filters an
  `ALL_TOOLS` list in JS; per Agent A, BM25 `tool_search` is not used by this model).
- Isolated `CODEX_HOME` per arm (auth symlink only; no user config, no user AGENTS.md, no user MCP servers),
  `approval_policy="never"`, `sandbox_mode="workspace-write"`, `allow_login_shell=false`, MCP servers of the arm with
  `default_tools_approval_mode="approve"`.
- Unity 6000.3.16f1, Pipeline 0.7.0-exp.1, Unity CLI 1.0.0-beta.10, macOS arm64, Editor unfocused; fixture = scratch copy
  of UnityTestForNexus + `Assets/BenchFixture/` built from TASKS_v1 §1 (subset, §3), git tag `fixture-v1`.

## 2. Arms (fixed)
| Arm | Model-visible Unity/Nexus surface | Shell → Unity | Notes |
|---|---|---|---|
| **S4** current | `unity mcp` (154 official tools) + current Nexus bridge (14 managers, legacy HTTP) | yes (`unity` on PATH, sandbox network on) | baseline |
| **S5** selective | S4 + mock `nexus` server mode s5: `nexus_hierarchy`, `nexus_console`, `nexus_find`, `nexus_inspect`, `nexus_get` (normalised, delegating to official commands) | yes | tests selective normalisation |
| **S6-domain** | mock `nexus` server only: 13 typed domain managers generated from Unity's catalog (all 154 commands, action = official name) + `nexus_get`; large reads normalised by default, `detail:"full"` = raw | **no** (`unity` not on PATH, sandbox network off → loopback blocked) | primary Nexus-primary arm |
| **S6-curated** | mock `nexus` only: `nexus_object`, `nexus_project`, `nexus_editor` (42 typed actions) + `nexus_describe`/`nexus_invoke` (other 112) + `nexus_get` | no | very small curated façade |
| **S6-router** | mock `nexus` only: `nexus_list`/`nexus_describe`/`nexus_run` + `nexus_get` | no | minimal-schema control, not representative |

Capability equivalence (honest list): all arms can reach all 154 official Unity commands. S4/S5 additionally have the 14
legacy Nexus managers (Capture V2, UI automation, write_and_compile, etc.) and the shell CLI; S6* do not. No selected
task requires a legacy-only capability. S6* forward writes and `eval` to Unity **for research only** (production policy
is read-only, see D_security.md); this favours S6 relative to a production S6 and is reported as a limitation.
Not run in the pilot (budget): option E arm (S1e), skeptic control C1 (Unity allowlisted to the curated subset),
realistic bypass cell (Unity skill + shell present in S6), Claude Code and Gemini cells. They are required in the full P0-16.

Arm instruction file (`AGENTS.md` in the fixture root; identical first paragraph, one arm-specific sentence):
- common: "This is a Unity 6 project. A Unity Editor is running with this project open; use the available Unity tools to
  inspect and change Editor state instead of editing scene/prefab YAML by hand. Save scenes you change."
- S4: "Unity Editor tools: the `unity` MCP server (official Unity CLI commands; also `unity command <name>` in the shell)
  and the `nexus-unity` MCP server (Nexus)."
- S5: S4 sentence + "The `nexus` server adds compact reads (nexus_hierarchy, nexus_console, nexus_find, nexus_inspect)."
- S6*: "Unity Editor tools: the `nexus` MCP server (Nexus; it runs the official Unity commands for you)."

## 3. Tasks (fixed selection rule, applied before any run)
Budget allows 6 fixture tasks. Rule: one task per requested category, preferring tasks whose fixture plants were
specified completely in TASKS_v1 and that need no capability absent from all arms:
T1 (UI why-wrong + fix, W), T2 (edit C# + verify compile, W), T3★ (find references, RO), T5 (console triage, RO),
T7 (scene change via primitive + verify, W), T9★ (deep irregular hierarchy, RO).
Excluded: T4 (overlaps T3/T9 tool needs), T6 (needs the icon-atlas tool; not built in the pilot fixture), T8 (import;
lowest priority), T10 (overlaps T2). Prompts, oracles, false-success definitions: verbatim from TASKS_v1.
Pilot oracle simplifications (disclosed): T1 uses the static oracle only (dynamic Play-mode raycast oracle deferred);
T2 uses compile + type/file checks + the TideMath/TideClock tests via the official `run_tests`.
Third-party (user's own production project copy, per owner decision): T3★ and T9★ templates on a copy of `Puzzle`,
arms S4/S5/S6-domain, N=1, ground truth fixed by the self-check snippets **before** any run there.

Run plan: fixture 6 tasks × (S4×2, S5×2, S6-domain×2, S6-curated×1, S6-router×1) = 48; third-party 2 × 3 = 6. Total 54.
Order: per task, arms interleaved in a seeded random order (seed 6000316); reset before every run.

## 4. Metrics (per run, from the Codex JSON stream + façade log + oracle)
semantic_ok (oracle), claimed_ok, false_success; input tokens (total and cached), output + reasoning tokens (from
`turn.completed`); tool-output tokens (sum of MCP result text + command output, cl100k); calls by kind (MCP per server,
shell, file edits); discovery calls (`nexus_list/describe`, `unity list`/`unity command` listing, code-mode tool filtering
items if present); MCP errors split into unknown-tool/action, invalid-argument (Unity validation), other; retries
(same tool re-called within 2 calls after an error); wrong-capability calls (a call to a tool that cannot serve the
step, judged by a fixed list per task written before the runs — §4.1); wall time; Unity/façade time (façade log, S5/S6
only); human intervention (always 0 in exec mode; recorded). Read-only integrity per TASKS_v1 §0.3.

4.1 Wrong-capability list (fixed now): T5 – any read of scene hierarchy; T9 – console reads; T3 – console reads; T2 –
scene hierarchy reads; T1/T7 – console reads. (Coarse; reported, not decisive.)

## 5. Directional labels (pilot) — fixed now
For comparisons S6-domain vs S4, S6-domain vs S5, S5 vs S4 (pooled over the 6 fixture tasks, N=12 per arm):
- **toward-X** if X has success ≥ comparator (point) AND (tokens per successful run ≤0.75× OR calls per successful run
  ≤0.75×) AND false-success not higher by >1 run.
- **against-X** if X has ≥3 fewer successes out of 12, OR tokens/success ≥1.3× with success not higher.
- otherwise **neutral**. Every label is reported together with "INSUFFICIENT EVIDENCE (N=12)".
Per-task-class split (RO: T3/T5/T9; W: T1/T2/T7) reported to inform the mixed-boundary hypothesis.
Granularity (S6-domain vs S6-curated vs S6-router): reported descriptively only (N=6 each).

## 6. Full P0-16 decision matrix (pre-registered now, before any S6 result; replaces v2.4 Gate F text)
See MASTER_PLAN_V2 Gate F (v2.5). Written into the plan before pilot results are read. Thresholds there may only be
tightened after this point.

---------------------------------------------------------------------
## Amendment A1 (2026-10-03, after critic review C_design_review.md, before any scored run)
Only two **unscored smoke runs** exist (T5/S4, T9/S6-domain, run ids `*_0`); they validated the harness and are excluded
from all analysis. Changes (each maps to a critic item):

**A1.1 Freeze (B1).** Façade, proxy, catalog surfaces, harness (`bench/facade_mock/pilot_harness/`), fixture generator and
T2 variant are committed before run 1; sha256 list in `pilot_harness/FREEZE.sha256`. Deviations of the mock normaliser from
the pre-task spec (F_facade_hypothesis §2.3, mtime 2026-10-02):
| Feature | Pre-task spec | Mock (frozen) | Note |
|---|---|---|---|
| hierarchy depth/collapse/default components/handle | yes | yes | + `root`, `limit`, name regex (plan P0-12 "depth/root/limit/filter") |
| derived `activeInHierarchy`, component filter | no | **removed** (H1) | only Unity's raw `activeSelf` is returned |
| console grouping, counts, cursor, errors first | yes (compact.py sorts errors first) | yes | top-10 instead of top-5 |
| console origin | first `(at X:N)` match (compact.py) | same rule | Assets/Packages-only variant removed (H1) |
| `compilationFailed` passthrough | no | yes | raw Unity field |
| unknown arguments | n/a | explicit `invalid_arguments` error (B4) | was silently dropped in a pre-freeze draft |

**A1.2 Answer hygiene (B2).** The fixture contains no NexusFeature~/task files (verified). Any agent shell command or MCP
argument matching `NexusFeature|TASKS_v1|G_nexus_primary|/harness/|Editor.log|eval-usage` marks the run **void**
(reported, rerun once). Editor.log reads are allowed for real users but void here because the shared log carries prior runs.

**A1.3 Symmetric call logging (B3).** Every MCP server of every arm runs behind the same logging proxy. Call unit = one
MCP `tools/call` or one shell command or one file change. In code mode, calls are counted from the proxy, not from items.

**A1.4 eval/production policy (B5).** Primary success for S6 arms = **production-policy success**: a run that called
`eval`/`eval_file`/`run_script`/`batch` through Nexus counts as *not achievable on production S6*. Observed success is
secondary. Such runs are tagged "needed direct Unity" for coverage. If >25% of S6-domain task calls are eval-like, the S6-domain
result is labelled "eval-mediated". S4/S5 eval use is reported, not penalised.

**A1.5 Run protocol (B6).** Infrastructure failure (decided automatically, before reading output): reset assertion failed
(console not seeded; T2 not failing at start), Codex produced 0 tokens, Editor unresponsive, void per A1.2 → rerun once,
all reruns reported. Cap: 30 min wall per run, same for all arms; capped = failure. `claimed_ok`: RO = an ANSWER line exists;
W = fixed positive-claim regex on the final message and no negation in its last 600 chars (`metrics.py`), applied
blind to arm. Network access is **on for all arms** (H4); S6 hides Unity's CLI by PATH only, and any shell command invoking
`unity command|cmd|list|mcp|eval`, `.unity/bin`, `:7800` or the Pipeline port file is recorded as **bypass**. Codex web
search disabled in all arms.

**A1.6 Formulas and labels (B6, H8).** Cost per success (per task, per arm) = Σ(input incl. cached + output incl.
reasoning) over **all** runs ÷ successes (production policy for S6); 0 successes → ratio capped at 4.0. Pooled = geometric
mean of per-task ratios. Calls per success analogous (reported only).
- **toward-X**: X's successes ≥ comparator − 1 **and** pooled cost-per-success ratio ≤0.75 **and** ratio ≤1.0 on ≥4 of 6
  tasks **and** false-success not higher by >1.
- **against-X**: ≥3 fewer successes **or** pooled ratio ≥1.3. Otherwise **neutral**. Always reported with "INSUFFICIENT
  EVIDENCE (N=12)".
- **Sensitivity (H1)**: labels are recomputed on the 4 tasks excluding T5 and T9; if the label changes, it is reported as
  "contamination-sensitive".
- **Consequences** (fixed now): toward S6-domain → the full P0-16c runs S6-domain first and keeps S6-curated only if it
  was not worse; against S6-domain → full spike runs S5 vs S4 first and S6 only with the C1 control and composite variants;
  neutral → no reprioritisation. Granularity (S6-domain/curated/router) is descriptive only.

**A1.7 Coverage and discovery (H9).** Per run: coverage class (compact surface only / escape hatch `nexus_describe`,
`nexus_invoke`, `detail:"full"` / needed direct Unity (eval-like) / bypass), discovery calls (`nexus_list`/`describe`,
`unity command` listings), first-call routing in S4/S5 (unity vs nexus-unity vs nexus vs shell), MCP output truncation.
Codex code-mode retrieval items are recorded if the JSON stream exposes them (in smoke runs it did not; MCP calls appeared
as plain `mcp_tool_call` items) — so retrieval correctness is **not** measured in this pilot and stays a P0-16a item.

**A1.8 Third-party targets (H2), mechanical rule fixed now.** Project: copy of the owner's `Puzzle`. Scene = the scene file
under `Assets/` with the most GameObjects (ties: path order). T3★ prefab = among prefabs with ≥5 outermost instances in that
scene and ≥1 other asset depending on it, the one with the most instances (ties: path order). T9★ = the first entry of
(AudioSource, Light, Collider, Animator, ParticleSystem, Camera, Canvas, MonoBehaviour-derived components in name order)
for which exactly one object at depth ≥6 with that component has an inactive ancestor; prompt = T9 template with the
component named. Ground truth recorded before any run there. Arms S4, S5, S6-domain, N=1.

**A1.9 Disclosures.** S4 has no official Unity skill (not installed here; the plan's S4 includes it) and only the one-line
routing text — S4 is weaker than the plan's S4 (H3). Codex code mode can post-process raw outputs in JS and compacts tool
schemas >5 KB (S6-domain managers are 0.3–2k tokens each, above the plan's 600-token rule) (H5). S6 mock has no
composites (`nexus_snapshot/compile/diagnose`), so it tests "Unity re-shaped + normalised reads", not the full Nexus-primary
product (M1). Sibling collapse may hide target branches; the offline sufficiency check (M4) was **not** run before the pilot.
T2 at start shows only CS0117 (CS0246 appears after the first fix because Saltmarsh.Editor depends on Saltmarsh.Runtime).
Oracle tolerance (M7): leading `/` and `.unity` suffixes accepted in T3/T9 parsers (applied to all arms). The v2.4
"≥20% mis-routing" prerequisite was removed at the owner's explicit request (2026-10-03 message, item 8), after Agent A's
offline routing data existed; mis-routing stays a reported metric (M6).

## Amendment A2 (2026-10-03, before any third-party run; fixture pilot running, results not read)
Applying A1.8 to the Puzzle copy (YAML GameObject-document count as the "most GameObjects" measure): the top scene is
`Assets/Libs/Layer Lab/GUI PRO Kit - Simple Casual/Scene/DemoScene.unity` (9,478 GameObjects, third-party asset-store
content) — it has no prefab instances, so T3★ is unsatisfiable there. Fallback rule: T3★ uses the next scene by GameObject
count that has a qualifying prefab (`Assets/Scenes/Game.unity` is next, 146 GOs + 7 prefab instances; if it has none, the next
one, and so on). T9★ uses the top scene. Prompt scoping (to keep the third-party tasks tractable and deterministic): T3★ asks
for the count in the named scene only plus referencing assets; T9★ names the component and the scene, and says "deep in the
hierarchy (6 or more levels)". Ground truth is computed by the harness before any agent run and stored in
`harness/key/puzzle_key.json`.
