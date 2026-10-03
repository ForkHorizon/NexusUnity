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
