# FINAL REVIEW — P0-16 pilot and Master Plan v2.5 (independent reviewer, 2026-10-03)

Scope: BRIEF, PILOT_RESULTS, PREREG_PILOT (v1 + A1–A5), MASTER_PLAN_V2 working tree (diff vs `25f9cb0`, `8571bcb`, `4ba4983`,
`fd8d754`), A–F/TASKS_v1, `pilot_harness/results/*.result.json`, `analysis.json`, `raw_runs.tgz` (extracted and read: event
streams, MCP proxy logs, `deadbridge/`, `harness_invalid/`), `facade_mcp.py`, `analyze.py`, `metrics.py`, `pilot.py`, `puzzle_run.py`.
I re-ran `analyze.py` unchanged on modified copies of the result set to test label sensitivity (copies under /private/tmp only).

Bottom line: the pilot numbers are reported accurately. Both directional labels are much more fragile than PILOT_RESULTS and
the plan say. Each one flips to *neutral* when a single defensible choice changes. The S6 mock was also weaker than the owner's
hypothesis in ways that were known before run 1.

---------------------------------------------------------------------
## A. Objections (most severe first)

### O1 — HIGH — "toward-S5" depends on three post-hoc or arbitrary choices, not only on contamination
- **Location:** PILOT_RESULTS §3 row "S5 vs S4"; plan §0 RU ("S5 «за» по стоимости (0.72×)"), G15, §17.3.
- **Evidence (re-computed with the committed `analyze.py`):**
  - (a) **A5 re-score.** With pre-A5 scoring (T3_S5_2 failing on `LanternSet.asset.`), the label is **neutral**: gm 0.806, successes 8 vs 9.
  - (b) **A3 re-run of runs the incident did not touch.** `deadbridge/T9_S5_1` and `T9_S5_2` made **zero** `nexus-unity` calls (proxy log: `{'nexus': 5}` and `{'nexus': 2}`), so the dead bridge did not affect them. They were still re-run, and the re-run T9_S5_2 is S5's only T9 success. With the original T9_S5 pair the label is **neutral**: gm 0.887, 8 vs 9.
  - (c) **Comparator floor.** `analyze.py` `ratio()` returns 0.25 when the comparator has 0 successes. A1.6 text only defines the 4.0 cap. T9 (S4 0/2 vs S5 1/2) therefore enters as the most favourable possible ratio, decided by one run.
  - (d) T5 contamination, which is already disclosed.
- **Fix:** In PILOT_RESULTS §3, call the S5 label "fragile (A5-, A3-rerun-, floor- and contamination-sensitive)". Add the four sensitivity rows. Remove "toward" from §0, G15 and §17.3, or qualify it the same way. Write the 0.25 floor into the prereg text for the full spike.

### O2 — HIGH — "against-S6-domain vs S4" rests on T3, a task S4/S5 solved by grepping YAML, plus the eval penalty
- **Location:** PILOT_RESULTS §3 and §4.3; plan §0, §17.3 ("S6 losses trace mainly to the mock's flat union schemas").
- **Evidence:**
  - Every successful S4/S5 T3 run, and S6-router's, solved T3 with shell `rg` on the prefab GUID (`m_SourcePrefab … guid: d0e6ab…`), with no Unity tool at all. T3_S4_1 and T3_S5_1 have `mcp_calls_by_server = {}`.
  - TASKS_v1 T3 set traps against *name* search, but none against GUID grep, so the task does not exercise the surface.
  - Under production policy, dropping T3 makes S6-domain vs S4 **neutral** (6 vs 8 successes, gm < 1.3).
  - Under observed success S6-domain vs S4 is neutral (0.92), and the gm is 0.76 without T3.
  - Of S6-domain's 6 production-policy losses, 4 are eval-policy:
    - T1_1: eval was call #4, before any argument error.
    - T3_2: eval was call #1.
    - T7_1 and T7_2: eval came after `set_component_properties` hit Unity's own `m_Shadows` "unsupported type 'Generic'" error.
  - The other 2 losses are a wrong count (T3_1) and the T9 decoy (T9_1). The 36 flat-union `'type' is required` errors (verified 36/63) cost tokens and calls, but no S6-domain loss is caused by them.
- **Fix:**
  - Re-attribute the losses in PILOT §4.3/§5, §0 and §17.3: eval habit plus a grep-solvable task, with the flat union as a *cost* defect.
  - For the full spike, make T3 non-greppable (variants or nested instances whose YAML GUIDs mislead), or score shell-only runs separately.
  - Add a production-shaped S6 cell with **no eval exposed** (see O4).

### O3 — HIGH — The primary S6 arm was not the owner's S6, and the main defect was known before run 1
- **Location:** PREREG §2 ("13 typed domain managers"); `facade_mcp.py:domain_tools()`; C_design_review M1/M5.
- **Evidence:**
  - `domain_tools()` builds a **flat union**: the first-seen parameter name wins (`if p["name"] in props: continue`), action descriptions are cut to 90 characters, and `additionalProperties: True`. That is B_catalog's *lossy flat* variant, not the typed one.
  - The critic flagged it before run 1 (C_design_review l.96, l.299). PREREG §2 still says "typed".
  - The arm also exposes **all 154 actions** under Unity's own names, so it tests regrouping (representation compression), not a compact task-shaped domain surface.
  - It has no composites (A1.9/M1) and exposes `eval` inside `nexus_code`.
- **Fix:** Correct PREREG §2 and PILOT §1 to "flat lossy union (domain_flat)". State in G15 and §17.3 that the pilot tested "Unity re-skinned (flat) + normalised reads" and says nothing about a typed, composite-bearing S6 (critic M1's "negative label does not refute" sentence).

### O4 — HIGH — Production-policy scoring mixes a security rule into the architecture verdict
- **Location:** A1.4; Gate F D1 (production-policy success for S6); PILOT §2 headline; plan §14b CRUD row ("use `batch`").
- **Evidence:**
  - S6 arms were *given* `eval`/`run_script`/`batch` and then scored as failures for using them. S4/S5 had Unity's `eval` too and used it 0/24 times, so availability was not the difference.
  - What production S6 would do without eval was never observed. The headline label converts this unknown into a failure.
  - `batch` is counted as eval-like, yet §14b recommends Unity's `batch` for writes.
  - A realistic "S6 for reads, Unity-direct for eval" (the plan's own Mixed outcome) would score these runs differently.
- **Fix:**
  - In the full spike, run S6 in its production shape (eval and agent `batch` not exposed) and measure real success. Keep the "eval exposed" cell as a sensitivity cell.
  - Take `batch` with `dry_run` out of the eval-like set, or justify keeping it.
  - In PILOT §2/§3, report production and observed labels side by side with equal weight.

### O5 — HIGH — Integrity claims about amendments are partly inaccurate, and promised artifacts are missing
- **Location:** PILOT §0 line 3 ("A3–A5 … decided from logs, not outcomes"); PREREG A3, A5; `results/`.
- **Evidence:**
  - **A5** was written *after the fixture pilot completed* and was triggered by a scored failure (T3_S5_2). That is outcome-informed, even if fair on the merits.
  - A5's text says "≈23:15", but the commit `ef73066` is at 22:55:02.
  - **A3** (commit 20:29:06) came after all 7 S4/S5 runs were scored 0/7 (result files 17:31–18:57). Its blanket re-run included runs the dead bridge did not affect (O1b).
  - A3 promised a "deadbridge sensitivity set" report. PILOT_RESULTS has none.
  - A5 promised `*.pre_A5` files. None exist in `results/`, in `raw_runs.tgz`, or anywhere in the repo.
  - `puzzle_run.py` (the third-party runner and T9★ oracle) is **untracked**, and its mtime 22:59:14 is *after* the three T9P runs (22:56–22:58). No committed pre-run oracle exists, only the A5 prose.
  - Gate F says "fixed … before any S6 result existed". The Gate F edit in `4ba4983` (17:28:52) came after the S6 smoke run T9_S6domain_0 (17:27:25).
  - `analyze.py` (`fd8d754`, 17:31:55) was committed after the first scored result, T9_S6domain_1 (17:30:07).
- **Fix:** Reword PILOT §0 to say "A3/A5 were decided after outcomes were visible; rules applied to all arms". Add the deadbridge and pre-A5 sensitivity table. Commit `puzzle_run.py` and the `*.pre_A5` files, or say they do not exist. Correct the A5 timestamp and the Gate F "before any S6 result" sentence ("before any scored S6 result").

### O6 — MED — S4's T7 success used a legacy-only Nexus capability
- **Location:** PREREG §2 ("No selected task requires a legacy-only capability"); PILOT §4.3 ("S4 solved T7 with that exact official command").
- **Evidence:**
  - Both S4 T7 runs learned the nested path `m_Shadows.m_Type` from the legacy `unity_dump_scene_graph include_all_properties` output (event streams).
  - The official `get_component_properties`, as passed through by S5's `nexus_inspect`, shows `"<unsupported:Generic>"`.
  - Both S5 T7 failures and S6-domain's eval fallback hit exactly this wall.
  - The official *write* worked. The *discovery* came from legacy Nexus.
- **Fix:** Disclose this as an S4/S5 advantage. Report legacy-helper use per run, as critic M1 required. Note that S5's T7 loss comes from the normalised read *displacing* the legacy dump, not from hiding data that Unity had returned.

### O7 — MED — The wall-time claim is a mean artifact
- **Location:** PILOT §4 table and §5 ("wall time by 15–47%").
- **Evidence:**
  - Medians: S4 59.2 s, S5 55.3 s, S6-domain **64.2 s** (slower than S4), curated 63.8 s, router 54.8 s.
  - The means are driven by T9_S4_2 at 510.9 s and T1_S5_2 at 443.6 s.
  - Gate F D8 uses medians.
- **Fix:** Report medians. Remove "wall time fell 15–47%".

### O8 — MED — Plan sections still contradict S6 and each other
- **§4 "Delegated façade" bullet:** "Coverage means every Unity command stays reachable **directly through Unity**". **§14b CRUD row:** "Writes stay Unity-direct even under a façade". Both define S6 away, while Principle 1 and P0-16d treat delegated writes as an open question.
- **§5.1 budget vs S6 schemas:** §5.1 sets ≤8 commands, ≤2.5k tokens, and each manager ≤600 tokens (also R22's trigger). S6-domain managers are 0.3–2k tokens each (58.9k chars total), and Gate F has no schema-budget dimension. If F passes, nothing resolves the clash.
- **§11 vs Gate F:** §11 says "Gate F D3 … or the candidate is dropped". Gate F says a failed or undecided D3 is "INSUFFICIENT EVIDENCE … stays open".
- **R23 vs D1:** R23's trigger "≥3/10 runs below S4" brings back the per-task veto that D1 removed.
- **Effort figures:** three different numbers — §8 capacity (≈25–60 Editor-hours), P0-16c (≈40–90), Gate F (≈130–320).
- **Fix:** Reconcile each item. In §4 and §14b, mark those sentences as S5/production policy only. Add a schema-budget row or an explicit §5.1 exception for F. Use one effort number with a budget decision.

### O9 — MED — The Gate F outcome rules are asymmetric between S5 and S6
- **Location:** Gate F outcomes F vs C.
- **Evidence:**
  - F needs D1, D2, D6, D9, two of D3/D4/D5, plus D7, D8 and D10, against both S5 and S4.
  - C needs only D1, D2, D9 and D3 on reads. S5 also ships hand-written normalisers (B §5 d: 200–350 lines) and also depends on client behaviour, but D7, D8 and D10 do not apply to it.
  - D6 (≥90% of tasks without leaving the compact surface) counts `nexus_describe`/`invoke` as escapes (A1.7). For S6-curated those *are* the designed surface, so the variant cannot pass D6 by construction.
- **Fix:** Apply D7, D8 and D10 to C as well, or justify the exemption in writing. Define D6's escapes per variant (for curated, only direct-Unity or eval counts).
- **Timing:** Changing these now is a loosening for F and a tightening for C. Under the plan's tighten-only rule this needs an owner-dated amendment, done before any full-P0-16c run.

### O10 — MED — Sequencing risks S6 never being tested at power
- **Location:** G15, §17.3, A1.6 consequence.
- **Evidence:** "S5 vs S4 first, then S6" combined with the 130–320 Editor-hour estimate and the expected INSUFFICIENT EVIDENCE outcome (Gate F text). In practice S6 gets whatever budget is left. Per O1–O4, the labels that trigger this ordering are not robust.
- **Fix:** Reserve a fixed share of the full-spike budget for S6 (typed per-action, production-shaped, with composites, plus C1) in the same wave as S5. The pre-registered consequence only orders runs; it does not say S6 is optional.

### O11 — MED — The third-party requirement is unmet, and T9★ carries no information
- **Location:** A1.8, A2, A5; PILOT §2 paragraph 2.
- **Evidence:** T3★ was dropped (the rule found no qualifying scene). T9★ was ambiguous by construction: all three arms gave a true alternative cause, alpha = 1/255. S4/S5 were flagged "mutated" by `packages-lock.json`, so their reasons were never scored. Plan P0-16b requires ≥2 third-party tasks.
- **Fix:** For the full spike, select third-party targets with a check for alternative causes, plus a human sanity read of ground truth before freezing it. Exclude `Packages/packages-lock.json` from the read-only diff. Accept any reason that names the inactive ancestors, alongside other true reasons.

### O12 — LOW — "Discovery calls 0" for S4/S5/S6-domain means unobservable, not zero
- **Location:** PILOT §4 table.
- **Evidence:** Code-mode `ALL_TOOLS` filtering is not exposed in the event stream (A1.7). F24's 25–29k-token dumps come from 10 stub-tool runs (A_tool_search §3, which marks task success UNKNOWN).
- **Fix:** Show "n/o" instead of 0, and footnote it.

### O13 — LOW — Leftover inconsistencies inside PREREG
- **Evidence:**
  - §2 table still says S6 runs with "sandbox network off". A1.5 and `pilot.py:206` say network is on for all arms.
  - "Uncached input is the better cost signal" (PILOT §4.1) is a post-hoc metric choice. The pre-registered metric is total tokens including cached.
  - A1.6 loosened v1 §5's "toward" rule from "success ≥ comparator" to "≥ comparator − 1". That happened before scored runs but after the smoke runs, and is not called out as a loosening.
- **Fix:** Fix the §2 table. Label the uncached metric as post-hoc. Call out the A1.6 change as a loosening.

---------------------------------------------------------------------
## B. Answers to the 10 questions

**1. Was Nexus-primary (S6) tested, or only selective reads? — CONCERN.**
- Tested: the "Unity hidden" property. S6 arms had no `unity` MCP or CLI, and the bypass count was 0. All three S6 shapes ran.
- Not tested: the owner's "compact domain façade + composites".
  - S6-domain is a flat lossy union of all 154 Unity actions under Unity's own names, with eval exposed and no composites (O3).
  - S6-curated (42 typed + describe/invoke) comes closest to "compact", but N=6 and it is descriptive only.
- Missing: per-action typed managers; task-shaped verbs (`nexus_snapshot`, `nexus_diagnose`, `nexus_compile`); production shape without eval; Claude Code and Gemini; the M4 sufficiency check.

**2. Are S4/S5/S6 capability-equivalent enough? — CONCERN.** Concrete differences:
- **Legacy Nexus (S4/S5 only):** 14 legacy managers. `unity_dump_scene_graph` produced 12 tool outputs over 40k tokens and supplied S4's T7 field path (O6).
- **Unity schemas:** S4/S5 saw Unity's full typed schemas (102k chars). S6-domain saw a flat 58.9k-char union with 90-character action blurbs.
- **eval (S6 only):** forwarded and then penalised (O4).
- **Shell CLI:**
  - In S4/S5, `unity` on PATH is the A4 shim, which always fails. In S6 it is absent.
  - In valid runs, no agent in any arm invoked `unity` in the shell (I scanned all event streams), so F27, A3 and A4 had **no behavioural effect** on scored runs.
  - Plain shell (`rg`, `cat`) was available to every arm and decided T3 (O2).
- **Network:** on for all arms (A1.5). PREREG §2 still says off for S6.
- **Instructions:** the S4/S5 AGENTS.md line advertises "also `unity command <name>` in the shell". S6's line says "it runs the official Unity commands for you", which plausibly steers S6 away from shell file reads.
- **Codex code-mode compaction:** schemas >5 KB are compacted, which affects the 0.3–2k-token S6 managers and Unity's tools differently.
- **Façade result clipping:** the façade clips results at 24k chars and returns a handle. S4 raw outputs go through code-mode JS post-processing instead.

**3. Is any arm advantaged by prompts, tooling or incidents, and were the re-run decisions outcome-blind? — PROBLEM.**
- **Advantaged:**
  - S4/S5 by legacy helpers (O6).
  - S4/S5 by a prompt that mentions the shell (T3; O2).
  - S5 by the A3 blanket re-run (O1b).
  - S5 and S6 equally by the A5 parser fix (it flipped T3_S5_2 to pass and T3_S6domain_2 to an observed pass).
- **Disadvantaged:**
  - S4 by not having the official Unity skill (disclosed in A1.9).
  - S6 by the known flat-union defect and the exposed-then-penalised eval (O3, O4).
- **A4 voids:**
  - The two voided runs (T2_S6curated_1, T2_S4_1) had "Cannot connect" oracle errors. The rest of T2 had not yet run (raw mtimes: every scored T2 run is 22:32–22:48, after A4). This one is clean.
- **A3 and A5:** not outcome-blind as claimed (O5).
- **Rule symmetry:** the rules were applied to all arms. The trigger timing is what makes them outcome-informed.

**4. Are the thresholds justified, pre-registered before results, and not loosened afterwards? — CONCERN.**
- A1.6 formulas: commit `4ba4983` at 17:28:52. The first scored run finished at 17:30:07, so the timing is fine.
- The 0.25 floor exists only in code (`fd8d754`, 17:31:55, after one scored result).
- The Gate F v2.5 matrix was committed in `8571bcb` (17:05) and edited in `4ba4983`:
  - Loosened: D1's per-task veto removed. The removal is justified by the false-positive rate (critic H6), but it contradicts "tighten, never loosen".
  - Tightened: D3 lost "or D4/D5 instead"; C now needs "D3 itself"; D1 now uses production policy for S6.
- Neither edit happened after scored results, but both came after S6 smoke results existed (O5).
- No Gate F text changed after the pilot. `git diff fd8d754` touches only §0, F27, P0-16c, G15, §17.3 and A4 wording.
- The full-spike thresholds (D1 −10 pp at 90%, D3 ≤0.8) are reasonable but underpowered, which the plan admits.

**5. Does the plan confuse schema savings with capability reduction? — CONCERN.**
- The plan's wording now separates the two (Principle 1 bullet; F23 vs F24).
- The main S6 arm is the confusion in practice: S6-domain exposes all 154 capabilities. Its smaller list (58.9k vs 121k chars) comes from the *lossy flat* representation (B §6.1: "flat variants that save 35–49% do so by discarding per-action parameter docs").
- The pilot's S6-domain result therefore speaks to lossy compression, not to capability-surface reduction. Only curated and router reduce the visible surface, and they still reach all 154 capabilities through invoke/run.
- PILOT §4.5 ("Smaller surfaces trade success for cost") rests on N=6.

**6. Are retrieval and discovery costs measured or inferred? — CONCERN.**
- **Measured:** explicit `nexus_describe`/`list` counts (curated 3.3 per run, router 5.2 per run); F24's dump sizes (10 live runs against stub tools); offline BM25 recall@1 (an offline proxy).
- **Inferred or unobservable:**
  - Code-mode `ALL_TOOLS` filtering cost and correctness in every pilot run (A1.7); shown as "0" in PILOT §4 (O12).
  - All Claude Code ToolSearch and Gemini numbers.
  - Retrieval correctness in top-k.
  - Discovery tokens separated from total tokens.
- **Still inferred in the plan:** §0 "25–29k токенов списка тулов на один запрос" is presented as a general fact. F21's Claude Code "spill to file" is "observed in this session" (anecdotal).

**7. Are we overfitting to the synthetic scene? — CONCERN.**
- **Contamination:** the lead wrote the normalisers after reading the tasks. T5 is solved by one `nexus_console` call (176 output tokens); its ratio of 0.376 drives S5.
- **Fixture regularity:** the generated suffixes (` (n)`, `_nn`) match the sibling collapse, and the M4 sufficiency check was not run.
- **T9:** sensitive to a single run per arm (S5 1/2, S6-domain 1/2, S4 0/2), and the 0.25 floor amplifies that.
- **T3:** grep-solvable (O2).
- **Third-party T9★:** ambiguous, gives zero discrimination (O11).
- PILOT already flags T5/T9 sensitivity; T3 and the floor are not flagged.

**8. Is the maintenance estimate (F25, D7) realistic? — CONCERN.**
- F25's ≈720–1,130 lines is the total. D7's ≤600 counts only normalisers and adapters. B §5 puts (c)+(d) at 360–590 and §6 at "≈400–600", so D7 sits at the top of the estimate before tests, composites and per-action typed generation (which P0-16c now requires).
- All production numbers are HYPOTHESIS, extrapolated from a 68-line prototype.
- Output envelopes changed in 3 of 4 minors (0.4/0.6/0.7), and every normaliser is hand-written against unschematised output. Expect D7 to be decided on an estimate, since a spike produces no production code.
- S5's normalisers carry the same churn risk but are exempt from D7 (O9).
- **Fix:** Make D7 a measured line count of the spike prototype plus a stated multiplier. Apply it to S5 too.

**9. Are security constraints being confused with architecture? — PROBLEM.**
- The plan text now separates "production policy" from the "research question" (Principle 1, P0-16d).
- The pilot's *primary* metric re-merges them: eval use under S6 counts as an architecture failure (O4).
- §4 and §14b still state production constraints ("reachable directly through Unity", "writes stay Unity-direct") as architecture facts (O8).
- D9 is sound as a gate. It should not also be a scoring rule inside D1.

**10. Is the plan deciding more strongly than the evidence supports? — CONCERN.**
- **Wording that is fine:** §0 ("S6 не отклонён, не подтверждён и не запланирован"), the header and §17.3's "Decision: no architecture change, nothing scheduled".
- **Wording that overstates:**
  - §0, G15 and §17.3 repeat "S5 toward" and "S6-domain against" with only "contamination-sensitive" attached, although both labels flip to neutral under single-point changes (O1, O2).
  - "S6 losses … mainly flat union schemas" is not supported by the run logs (O2).
  - The S5-first ordering plus the budget gap can turn an "order" consequence into a de facto demotion of S6 (O10).
- **Nothing pushes toward implementing S5.** No production work is scheduled, and C still needs Gate F.

---------------------------------------------------------------------
## C. Spot-check of PILOT_RESULTS numbers (all against result files / analysis.json)

| Claim | Source value | Verdict |
|---|---|---|
| S6-domain prod 6/12, observed 10/12 | analysis.json arm_totals | OK |
| S5 vs S4 gm 0.72, S6d vs S4 1.39, S6d vs S5 2.02 | 0.718 / 1.391 / 2.024 | OK |
| Sensitivity: 2.33 / 2.17 / 1.10 | 2.329 / 2.172 / 1.099 | OK |
| Mean tokens: S4 304k, S6-domain 343k | 303,994 / 343,486 | OK |
| Uncached: S4 43.9k, S5 32.8k, S6d 36.4k | 43.9 / 32.8 / 36.4 | OK |
| Tool output: S4 283k | 283.5k | OK |
| Errors / invalid: S6d 5.2 / 4.4 | 5.25 / 4.42 | OK |
| "36 errors are one defect" | 36 of 63 S6-domain errors are `'type' is required` | OK |
| eval share 9.3% | 17/182 MCP calls = 9.3% (8.6% of all calls) | OK (denominator = MCP calls; say so) |
| T1 S6-domain 381k; T3 S6-domain 580k; T7 S6-domain 509k | 380.5k / 579.5k / 509k | OK |
| Third-party tokens 193k / 114k / 184k | 192.8k / 114.0k / 183.8k | OK |
| Wall 103 / 88 / 69 | means correct; medians 59 / 55 / **64** | Misleading (O7) |
| "Wall time fell 15–47%" | median: S6-domain +9% vs S4 | **Wrong as stated** |
| "S4 solved T7 with that exact official command" | write yes; field path from legacy dump | Incomplete (O6) |
| "A3–A5 decided from logs, not outcomes" | A5 triggered by a scored failure; A3 after 0/7 scored | **Inaccurate** (O5) |

---------------------------------------------------------------------
## D. Owner requirements still missing
- S6 "+ composites" (BRIEF, S6-domain definition).
- Option E arm (S1e).
- C1 and S6-bypass controls.
- Claude Code and Gemini cells.
- ≥2 third-party tasks (achieved 0 informative).
- The A3 deadbridge sensitivity report.
- The A5 `*.pre_A5` files.
- A committed third-party runner.
- Per-run legacy-helper reporting (critic M1 fix).
- The M4 sufficiency check.

All except the last five are acknowledged as "not run" in the plan. The last five are promised but absent.

---------------------------------------------------------------------
## E. Overall verdict (5 lines)
1. The data is reported accurately at the number level. The labels' robustness and the integrity claims are not.
2. "toward-S5" and "against-S6-domain vs S4" each flip to neutral under one defensible change: A5, the A3 re-run, the 0.25 floor, removing T3, or observed scoring.
3. The S6 that ran was a flat, eval-exposed, composite-free re-skin of all 154 Unity commands. It cannot speak for the owner's compact Nexus-primary hypothesis.
4. The plan's "nothing scheduled / S6 open" decision is right. Its wording should drop both directional labels from §0, G15 and §17.3, or mark them fragile.
5. Before the full spike: reconcile §4, §14b, §5.1, §11, R23 and the budget; fix T3 and the third-party selection; run S6 production-shaped, typed per action and with composites in the same wave as S5.
