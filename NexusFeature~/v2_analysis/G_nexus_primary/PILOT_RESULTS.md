# P0-16 pilot results — S4 vs S5 vs S6 (Codex, 2026-10-03)

Pre-registration: `PREREG_PILOT.md` (v1 + amendments A1–A5, all committed before the runs they govern; A3–A5 fix
infrastructure/scoring defects and were decided from logs, not outcomes). Raw data: `bench/facade_mock/pilot_harness/results/`
(`*.result.json` per run, `analysis.json`, `raw_runs.tgz` with every Codex event stream, MCP proxy log and the voided runs).
Analysis code: `pilot_harness/analyze.py` (committed before results were read, `fd8d754`).

**Verdict for every comparison: INSUFFICIENT EVIDENCE (N=12 per main arm, N=6 for curated/router, one client, one model).**
Labels below are the pre-registered directional labels; their only consequence is research priority (A1.6). **The final
independent review (FINAL_REVIEW.md) showed both directional labels are fragile — they flip to neutral under small, defensible
variations (§6). Read every label as "neutral within noise".**

## 1. What was run
- Client Codex CLI 0.160.0, model gpt-6-luna, reasoning medium, code-mode tool exposure. Fixture: 1,376-object irregular
  Harbor scene + MainMenu + Docks_Test (TASKS_v1 subset T1, T2, T3, T5, T7, T9). 48 scored fixture runs + 3 third-party runs.
- Arms: **S4** current (`unity mcp` 154 tools + current Nexus bridge 14 managers; shell CLI present but non-functional in the
  Codex sandbox, F27), **S5** = S4 + 5 normalised Nexus reads, **S6-domain** (13 catalog-generated managers + `nexus_get`,
  Unity hidden), **S6-curated** (3 typed tools + describe/invoke), **S6-router** (list/describe/run; control).
- Infrastructure incidents (all disclosed, see A3–A5): dead Nexus bridge in the first 7 S4/S5 runs (re-run); Unity CLI deleted the
  Pipeline port file from inside the Codex sandbox (re-run, F27); a harness port-file lock voided 2 T2 runs (re-run); T3
  parser rejected trailing punctuation (re-scored for all arms).

## 2. Results per task (primary success = production policy for S6, i.e. a run that needed `eval`/`run_script`/`batch` through Nexus counts as failure; observed success in parentheses; mean total tokens incl. cached)

| Task | S4 | S5 | S6-domain | S6-curated | S6-router |
|---|---|---|---|---|---|
| T1 UI fix (W) | 2/2 (2) 224k | 2/2 (2) 180k | 1/2 (2) 381k | 1/1 (1) 114k | 1/1 (1) 147k |
| T2 compile fix (W) | 2/2 (2) 430k | 2/2 (2) 364k | 2/2 (2) 233k | 1/1 (1) 347k | 1/1 (1) 189k |
| T3 references (RO) | 1/2 (1) 246k | 2/2 (2) 265k | 0/2 (1) 580k | 0/1 (1) 200k | 1/1 (1) 237k |
| T5 console triage (RO) | 2/2 (2) 162k | 2/2 (2) 61k | 2/2 (2) 160k | 1/1 (1) 44k | 1/1 (1) 72k |
| T7 scene write (W) | 2/2 (2) 443k | 0/2 (0) 446k | 0/2 (2) 509k | 1/1 (1) 421k | 0/1 (0) 528k |
| T9 deep hierarchy (RO) | 0/2 (0) 318k | 1/2 (1) 156k | 1/2 (1) 198k | 0/1 (0) 95k | 0/1 (0) 246k |
| **Total** | **9/12 (9)** | **9/12 (9)** | **6/12 (10)** | **4/6 (5)** | **4/6 (4)** |
| False success | 3 | 3 | 2 | 1 | 2 |

Third-party (owner's Puzzle copy, 9,478-object asset-store demo scene, T9★, N=1): **all three arms (S4, S5, S6-domain) found
the exact target object and all three gave a different-but-true reason** (the RawImage's alpha is 1/255) instead of the
pre-registered one (inactive ancestors) → all scored FAIL. The task is ambiguous by construction (the mechanical selection
rule did not check for other causes); S4/S5 were additionally flagged "mutated" because Unity rewrote
`Packages/packages-lock.json` (harness artifact, not an agent write). No arm difference; tokens S4 193k, S5 114k, S6-domain
184k; tool output S4 363k (Nexus `unity_dump_scene_graph`), S5 0.3k, S6 0.5k. T3★ was infeasible (no qualifying prefab, A5).

## 3. Pre-registered labels (A1.6: cost per success = Σ tokens over all runs ÷ successes; geometric mean over tasks)

| Comparison | Label | Successes | Cost/success ratio (gm) | Sensitivity (excl. T5, T9) |
|---|---|---|---|---|
| S6-domain vs S4 | **against-S6-domain** | 6 vs 9 | 1.39 | against (2.33) — stable |
| S6-domain vs S5 | **against-S6-domain** | 6 vs 9 | 2.02 | against (2.17) — stable |
| S5 vs S4 | **toward-S5** | 9 vs 9 | 0.72 | neutral (1.10) → **contamination-sensitive** |

Secondary (observed success, `eval` through Nexus allowed): S6-domain vs S4 **neutral** (10 vs 9, 0.92); S6-domain vs S5
neutral (10 vs 9, 1.29); S6-curated vs S4 against (5/6 vs 9/12 on fewer tasks; cost 0.53); S6-router vs S4 against (4/6, 0.76).

By task class (primary): **read/diagnose** (T3, T5, T9): S5 vs S4 toward-S5 (5 vs 3, 0.37); S6-domain vs S4 neutral (3 vs 3,
1.00); S6-domain vs S5 against (3 vs 5, 2.37). **Writes** (T1, T2, T7): S6-domain vs S4 against (3 vs 6, 1.95) but observed
neutral (6 vs 6, 1.02); S5 vs S4 against (4 vs 6, 1.40 — S5 lost both T7 runs).

Pre-registered consequence (A1.6): **against S6-domain → the full P0-16 runs S5 vs S4 first, and S6 only with the C1 control
and composite variants.** The S5 label is contamination-sensitive, so S5 gets no promotion beyond "first in the full spike".
**Superseded after the final review:** because no label is robust (§6), the plan runs S5 and a production-shaped S6 in the same
wave instead (MASTER_PLAN_V2 G15, §17.3) — a disclosed deviation from this pre-registered consequence.

## 4. Mechanism-level observations (descriptive; these explain the labels and are the most useful pilot output)

| Per run (mean) | S4 | S5 | S6-domain | S6-curated | S6-router |
|---|---|---|---|---|---|
| Total tokens (incl. cached) | 304k | 245k | 343k | 203k | 236k |
| Uncached input tokens | 43.9k | 32.8k | 36.4k | 27.7k | 28.0k |
| Tool output tokens (as emitted) | 283k | 13.9k | 4.4k | 2.9k | 7.7k |
| Calls (MCP + shell + edits) | 9.2 | 12.0 | 16.5 | 19.3 | 16.2 |
| MCP errors / invalid-argument | 1.2 / 0.7 | 2.7 / 1.8 | 5.2 / 4.4 | 3.2 / 2.2 | 1.2 / 1.0 |
| Discovery calls (`describe`/`list`) | 0 | 0 | 0 | 3.3 | 5.2 |
| Wall time (s) | 103 | 88 | 69 | 62 | 54 |

1. **Normalisation works on output volume, not automatically on total cost.** Tool output fell 20–100× (S4 283k → S5 13.9k →
   S6 4.4k); wall time did **not** clearly fall (medians: S4 56 s, S5 54 s, S6-domain 64 s; the mean drop came from two slow
   S4/S5 outliers), and total tokens did not fall proportionally: Codex replays context every turn (cached
   input dominates totals), and S6 arms make more calls. Uncached input is the better cost signal: S5 −25%, S6-domain −17%
   vs S4.
2. **The S4 output explosion comes from the current Nexus, not Unity.** In S4, the 12 tool results over 40k characters are all
   Nexus's own `unity_dump_scene_graph`; agents chose it for scene reads. The current Nexus already competes with Unity *and* is
   the noisiest read. (Supports S5-style bounded reads replacing it.)
3. **S6-domain's most frequent *error* is the façade's own schema** (it cost calls and tokens; it did not by itself decide
   outcomes — see §6). 36 of its 63 errors are one defect: the flat
   union schema merges parameters across actions, and agents passed `component` to `get_component_properties`, which needs
   `type`. In T7, S6-domain never found `set_serialized_field` (present in its `nexus_gameobject` manager) and fell back to `eval`
   — **discoverability loss inside a large manager, not capability loss.** S4 solved T7 with that exact official command.
4. **Production policy matters.** S6-domain needed `eval` in 4/12 runs (eval share 9.3% of calls; below the 25%
   "eval-mediated" threshold), turning observed parity (10 vs 9) into a loss (6 vs 9). S4/S5 never used `eval`.
5. **Smaller surfaces trade success for cost.** Curated/router were the cheapest per run but succeeded less, with more
   discovery calls (3–5 per run) — consistent with "fewer tools ≠ better" and with Agent A's retrieval data.
6. **Shell-first Unity CLI is unusable from Codex's default sandbox** (F27): it cannot reach the Editor and deletes the
   Pipeline port file. In practice S4/S5 agents used only MCP. This is a real argument against "Unity CLI via shell" as the
   Codex path, independent of Nexus.
7. **Bypass**: 0 S6 runs invoked Unity outside Nexus (PATH-hidden). Answer-leak checks: 0 voided runs.
8. **Capability coverage (S6)**: compact surface only 3/12 (domain), 2/6 (curated), 1/6 (router); escape hatch (`describe`,
   `invoke`, `detail:"full"`) 5/12, 3/6, 4/6; needed `eval` 4/12, 1/6, 1/6. No task required a capability S6 lacked; every
   escape was a discoverability or argument-shape issue.

## 5. What this pilot does and does not show
- **Does not show** that Nexus-primary is bad: observed success was at parity; 4 of S6-domain's 6 production-policy losses are
  `eval` penalties, and the rest of the gap sits on T3, which S4/S5 solved by grepping prefab GUIDs in the shell (no Unity tool
  calls) while S6-domain tried Unity tools; the mock was a **flat, lossy union** (not the per-action typed design the owner
  described), exposed `eval`, and had no composites.
- **Does not show** that S5 is better: its advantage sits in T5/T9 (where the lead's normalisers are contamination-exposed) and
  it lost T7 twice.
- **Does show** (descriptively): output normalisation cuts emitted tool output by 1–2 orders of magnitude and does not clearly change wall time; total-token savings are smaller and client-dependent; a catalog-generated flat manager surface causes argument errors
  and in-manager discoverability loss; the current Nexus legacy scene dump is the largest single output source; Unity's
  shell CLI is not a viable Codex path under the default sandbox.
- **Not measured**: Claude Code, Gemini CLI, Cursor; retrieval correctness inside Codex code mode (not exposed in the event
  stream); C1, S1e and S6-bypass controls; composites; typed per-action (non-flat) S6 managers.

## 6. Robustness checks requested by the final review (computed with the committed `analyze.py`; `results/` holds every input)

| Variation | S6-domain vs S4 | S5 vs S4 | S6-domain vs S5 |
|---|---|---|---|
| Main (as pre-registered) | against (6 vs 9; 1.39) | toward (9 vs 9; 0.72) | against (6 vs 9; 2.02) |
| Without T3 (grep-solvable; no Unity tools needed) | **neutral** (6 vs 8; 1.13) | **neutral** (0.76) | against (1.71) |
| Without T3, observed success | neutral (9 vs 8; 0.76) | neutral | neutral (1.02) |
| Original dead-bridge S4/S5 runs kept instead of the A3 re-runs | **neutral** (1.13) | **neutral** (1.00) | **neutral** (1.14) |
| T3 scored as before A5 | against (1.39) | **neutral** (0.81) | against |
| Tasks with 0 successes excluded instead of the 0.25/4.0 floor/cap (the floor/cap is in code, not in A1.6 text) | against (1.22) | toward (0.61) | against (1.73) |

Conclusion: **no label is robust**. Additional corrections from the review:
- **Timeline honesty.** A3 (dead bridge) was written after all 7 affected S4/S5 runs had scored 0/7, so it was not outcome-blind,
  although the trigger (100% connection-refused) was an infrastructure fact; two of the re-run T9 S5 cells never called the dead
  bridge. A5 (T3 parser) was triggered by reading a scored failure; its "≈23:15" stamp is wrong — the commit is 22:55. The A4 voids
  were clean. Pre-fix T3 results are in `results/*.pre_A5`; dead-bridge originals in `results/deadbridge/`.
- **Capability equivalence.** S4's T7 successes used the legacy Nexus `unity_dump_scene_graph` to find the `m_Shadows` field path,
  so PREREG's "no task needs a legacy-only capability" is not true in practice; S6 had `eval`, S4/S5 had the legacy managers.
- **Discovery.** "0 discovery calls" for S4/S5/S6-domain means Codex code mode does not expose discovery in its event stream, not
  that none happened. Retrieval cost/correctness remain unmeasured here.
- **Third-party requirement unmet.** T3★ was infeasible and T9★ was ambiguous (all arms found the object, gave a true but
  unregistered reason). The full spike needs third-party tasks with uniqueness checks on the *reason*, not only on the object.
- **Post-hoc metric.** "Uncached input tokens" as the better cost signal is a post-hoc observation, not a pre-registered metric.
