# Plan review v2.6 — B (methodology: Stage A / Stage B / Gate F / §18)

Reviewer: independent methodology review, document-only. No experiments, benchmarks, Unity or agent sessions were run.
Only offline Monte-Carlo checks of the decision rules were done (scripts in `/private/tmp/revB/`, not in the repo).
Read: `MASTER_PLAN_V2.md` §0 (v2.6 RU summary), §7 P0-16c, P0-17, Gate F, §17.5, §18; `PILOT_RESULTS.md` (§4, §6),
`FINAL_REVIEW.md`, `C_design_review.md` (H6–H8), `PREREG_PILOT.md`; pilot `*.result.json` (read-only, to get variance inputs).

## 0. Simulation inputs (from the pilot; descriptive)

- Within-cell (same task × arm) run-to-run token variation: median CV ≈ 0.25; SD of log(total tokens) ≈ 0.25 (median) to
  0.38 (RMS). Uncached tokens CV ≈ 0.17; calls CV ≈ 0.25. The sims use SD = 0.30 on the log scale, with calls correlated
  ρ = 0.7 with tokens.
- Per-task success for the comparator: {0.50, 0.60, 0.75, 0.85, 0.95, 1.00}. Six tasks × N = 8 per arm (48 runs per arm).
- Cost per success = Σ tokens ÷ successes per task, with the pilot floor of 0.25 successes. The ratio is the geometric mean
  over tasks. Success difference = pooled difference in success rate.
- "Robust to drop-one-task" = the verdict holds on the full set **and** on all six leave-one-out sets.
- 20,000 replications per scenario. Runs are treated as independent given the task, because Codex exposes no seed (see #4).
- Scheduling time: pilot wall time plus reset averaged **1.7 min per scored run** (median ≈1.4 min). The range was 0.5–9.2 min,
  with two runs of 7.7 and 8.9 min.

Key derived fact: at 6 × 8 per arm, the SD of the pooled success difference is **≈7.7 pp**. The −5 pp survive guard sits 0.65 SD
from zero. The −15 pp falsify threshold sits 1.9 SD from zero.

## 1. Operating characteristics of the Stage A rules (as written, 6 tasks × N = 8)

| True state (challenger vs comparator) | P(FALSIFIED) | P(SURVIVED) | P(INCONCLUSIVE) |
|---|---|---|---|
| Equal arms | **0.013** | **0.047** | 0.94 |
| Equal on average, task×arm heterogeneity (SD 10 pp success, 0.2 log cost) | 0.043 | 0.056 | 0.90 |
| +10 pp success, 0.8× cost and calls (the brief's "moderate good") | 0.000 | **0.76** | 0.24 |
| Equal success, 0.8× cost, calls unchanged | 0.005 | **0.37** | 0.63 |
| Equal success, 0.67× cost and calls | 0.005 | **0.49** | 0.51 |
| −15 pp success | **0.38** | 0.001 | 0.62 |
| −25 pp success | 0.79 | 0.000 | 0.21 |
| Equal success, 1.25× cost | **0.22** | 0.001 | 0.78 |
| Equal success, 1.5× cost | **0.32** | 0.000 | 0.68 |

The same model with N = 12 and N = 16 changes little. False calls fall a little, but the low power for pure-cost or
pure-success effects barely moves, because the limits come from rule structure, not from N.

## Objections

**1 · HIGH · §18 Stage A rule framework — the rules are safe against false calls but cannot detect the large effects they target.**
False falsification (≈1%) and false survival (≈5%) under equal arms are fine. Three structural defects make most real effects
come out INCONCLUSIVE:
- (a) **The thresholds sit exactly at the effect sizes they are meant to catch.** A true −15 pp is falsified only ≈38% of the
  time. A true 1.25× is falsified ≈22% of the time.
- (b) **"Success not higher" is a coin flip at equal success.** P(observed d ≤ 0) ≈ 0.55. This gate caps the cost-falsify
  branch, so even a true **1.5×** cost blow-up is falsified only ≈32% of the time.
- (c) **The −5 pp survive guard is 0.65 SD.** About 25–40% of challengers that are truly equal on success but cheaper fail it
  by noise. A true 0.67× cost arm survives only ≈49% of the time.
- (d) **Drop-one-task re-applies the full thresholds to every leave-one-out subset.** That roughly halves power (no-drop:
  −15 pp → 60%, 0.8× → 63%).

The likely result is "no theory FALSIFIED or SURVIVED", which is exactly the R27 trigger.

Fix (cheap, no extra runs). Simulated at N = 8:
- FALSIFIED = d ≤ −15 pp, **or** (cost ratio ≥ 1.25 **and** d ≤ **+5 pp**).
- SURVIVED = d ≥ **−10 pp** and cost ratio ≤ 0.85.
- Robustness: every leave-one-out estimate stays on the same side of a relaxed band (falsify: d ≤ −8 pp or ratio ≥ 1.12;
  survive: d ≥ −15 pp and ratio ≤ 0.92). Do not re-apply the full thresholds to each subset.

Results with this rule:
- Equal arms: false-falsify 5.6%, false-survive 9.5% (11% with heterogeneity).
- −15 pp: 57% falsified. −25 pp: 90%. 1.25×: 46%. 1.5×: 68%.
- 0.8× cost: 61% survived. 0.67×: 85%. +10 pp with 0.8×: 88%.

Going from N = 8 to N = 12 adds about 5 points of power and costs +50% runs, so it is not worth it. Spend runs on more
tasks (#4) instead.

If the plan wants "large effects only" semantics, it should say plainly that the rule is calibrated to detect effects of
**≥25 pp or ≥1.5× / ≤0.67×**. It should not cite −15 pp / 1.25 / 0.85 as detection sizes.

**2 · HIGH · §18 SURVIVED rule — "cost **or** calls ≤ 0.85" is a two-shot test, and calls are not a cost-neutral unit.**
Under code mode, the number of calls depends on the counting unit (C_design_review B3). The pilot also showed the cost/calls
trade-off running the wrong way: S6 arms made more calls and used less output. The OR doubles the chance of a noise pass on
whichever metric happens to look good.

Fix: make SURVIVED depend on **cost per success only**, with uncached tokens as the frozen primary (see #9). Report calls
descriptively. If calls must stay in the rule, require both metrics, or apply Holm to the pair.

**3 · HIGH · H1 rule (§18) and P0-17c/d staging — C1 and the Nexus arms are not in the same paired block.**
C1 runs in A1 (weeks 3–5). S6-P and M run in A2 (weeks 6–8). A2 re-baselines only S4. H1 compares C1 directly with "the best
Nexus arm", which is a cross-stage, unpaired comparison exposed to client, model and fixture drift. The same applies to H4,
which compares S4+ with the best challenger.

Fix: re-run C1 in A2 (+48 runs). Alternatively, define H1 and H4 only through ratios to the S4 run in the same stage, and
accept the extra variance (about √2 on the log ratio). Freeze the client and model versions for the whole of Stage A.

**4 · MED · §18 "paired by task and seed" — there is no seed to pair on.**
Codex does not expose a sampling seed. "Paired by seed" therefore means only run-order blocking. Within-task pairing gives
almost no variance reduction, and the effective unit is the task. The pilot showed large task×arm interactions: S5 scored 0/2
on T7 where S4 scored 2/2, and S6 failed on T3 by grep. With 6 tasks, the between-task component cannot shrink.

Fix:
- Say "blocked by task and interleaved run order" instead of "paired by seed".
- Analyse with a task-blocked permutation test or a cluster bootstrap. Note that a cluster bootstrap over 6 clusters is
  unstable, so prefer an exact task-level sign or permutation test, or a mixed model with task random effects.
- Prefer **8–10 tasks × N = 5–6** over 6 × 8 at the same run count.

**5 · MED · H1 rule — the overlap with SURVIVED is undefined, "best Nexus arm" is undefined, and the rule can fire vacuously.**
- (a) FALSIFIED (C1 within −5 pp and C1/Nexus cost ≤ 1.10) and SURVIVED (Nexus beats C1 through the *calls* branch ≤ 0.85) can
  both hold at once. No precedence is stated.
- (b) "Best Nexus arm" has no stated criterion (cost? success? which metric?). Picking the minimum of 3 noisy arms biases
  toward H1 surviving, a winner's-curse effect.
- (c) If no Nexus arm beats S4, "C1 ≈ Nexus" falsifies H1 vacuously. In the simulation with all arms equal, H1 is FALSIFIED in
  **46%** of replicates. When one Nexus arm is truly 0.8× and C1 = S4, H1 is falsely FALSIFIED 17% of the time and SURVIVES
  only 41% of the time.
- (d) Unlike the generic rules, it has no drop-one robustness rule.

Fix:
- Pre-declare the Nexus arm for H1 as the arm with the best S4-relative cost ratio, chosen by the frozen metric.
- Make H1 **not applicable** (INCONCLUSIVE) unless that arm SURVIVED vs S4.
- Give FALSIFIED precedence over SURVIVED, or drop the calls branch (#2).
- Apply the same robustness rule as the generic rules.

**6 · MED · H4 rule — "≥70% of the best challenger's improvement" is ill-conditioned.**
It is a ratio of two noisy improvements. When the best challenger's improvement is near zero or negative, the 70% condition is
trivially met or undefined, and "best" is selected on noise. H4 is cost-only, while the generic survive rule is cost-or-calls,
so the two are inconsistent. H4 SURVIVED also does not require S4+ to pass the generic survive rule vs S4, and has no
drop-one rule.

Fix:
- H4 is evaluable only if some challenger SURVIVED vs S4.
- SURVIVED: S4+/S4 ≤ 1 − 0.7·(1 − r_best), success ≥ best − 10 pp, and the same robustness rule.
- FALSIFIED: S4+/S4 ≥ 0.95, **and** the challenger's ratio is below S4+'s by ≥ 0.15 on the paired comparison.
- Otherwise INCONCLUSIVE.

**7 · MED · P0-17f finalist rule — multiplicity, an inverted C1 condition, and only one free slot.**
- (a) **Multiplicity.** With 5 challengers vs one shared S4 and all arms truly equal, P(≥1 spurious SURVIVED) ≈ **15%**
  (plan rule), or about 25–30% with the fix in #1. That is acceptable for a screen, provided Stage B is confirmatory and
  independent of Stage A (fresh runs; Stage A data are not pooled into Gate F). Say so explicitly. Under the fixed rule, also
  cap SURVIVED at the top-2 by pre-declared metric when more than 2 arms survive.
- (b) **The C1 condition is backwards.** "C1 if H1 is not falsified" drops C1 exactly when H1 is FALSIFIED, which is when C1 is
  as good as the best Nexus arm, the E candidate. Gate F's C1 attribution rule also needs C1 whenever F or M is a finalist.
  Fix: **C1 is always a Stage B arm whenever a Nexus-primary (S6-P/M) finalist exists, and it is a finalist in its own right if
  H1 is FALSIFIED.**
- (c) **Only one free slot.** S4 + C1 + one other means S4+ (H4/E) and the best Nexus arm can never both enter. Either allow
  4 arms with C1 counted as a control rather than a finalist, or state the tie-break in advance.
- (d) **"Fewer than two theories survive" is ambiguous.** It does not say whether S4 and C1 count as theories. Define it.

**8 · HIGH · Gate F D3 — the 0.8 cut-off is a point threshold, not a margin.**
D3 requires the geometric-mean ratio ≤ 0.8 **and** a CI upper bound < 1.0. At a true ratio of 0.8, the point condition holds
about 50% of the time **at any N**, so D3's power is capped at ≈50%. The I rule ("interval contains 0.8 and 1.0") then
dominates.

Fix: either test "upper 90% bound ≤ 0.9" (a true margin) with the point estimate reported, or state that D3 is designed to
detect **true ≤ 0.7**. Analytically this has ≈80% power at about 30–40 runs per arm per task-class, given the pilot CV.

**9 · MED · Gate F metrics (3) and (4), and D3 — two primary cost metrics with no designated decider.**
Total and uncached tokens per success are both listed as primary, and D3 says "both reported". The pilot's switch to uncached
was post hoc (PILOT_RESULTS §6), and the two diverge under Codex replay. Fix: freeze one as the decision metric in
PREREG_P0-17 and PREREG_GATE_F (uncached is the lower-variance choice: CV ≈ 0.17 vs 0.25). Report the other as secondary.
Also freeze the zero-success handling (floor/cap vs exclusion). In the pilot, this choice alone moved S5/S4 from 0.72 to 0.61
and S6/S4 from 1.39 to 1.22.

**10 · HIGH · P0-16c / Gate F — the 15–40 h budget is coherent only under optimistic assumptions. State the power honestly.**
D1 non-inferiority at −10 pp with a 90% one-sided bound, p ≈ 0.75, true difference 0: 80% power needs SE ≤ 4 pp.
Required runs per arm:
- ≈ **235 per arm** with no task×arm heterogeneity;
- ≈ **290** with heterogeneity SD 5 pp across 8 tasks;
- ≈ **1,000+** with SD 10 pp across 8 tasks.

With few tasks, the heterogeneity term τ²/K alone uses up most of the SE budget.

Runs per finalist set:
- 2 arms (current + 1 challenger) ≈ 470 runs ≈ 13–31 Editor-hours at 1.7–4 min.
- 3 arms ≈ 700 runs ≈ 20–47 h.
- Before alpha-spending inflation (O'Brien–Fleming, +2–5% maximum N).
- Before the per-class analyses that C and M require (each class has about half the runs; to be powered they roughly double
  the total).
- Before D10's second client.

So 15–40 h buys **≈80% D1 power for 2–3 arms on one task class with homogeneous tasks**. Everything else (per-class verdicts,
heterogeneous tasks, D10) will most likely end in **I**.

Fix:
- Put this table in PREREG_GATE_F.
- Raise the task count to ≥10 (it shrinks τ²/K).
- Pre-specify a **futility** look at about 50% of maximum N (it saves budget when I is likely).
- Make the per-class verdicts for C and M explicitly exploratory unless separately powered.
- Budget D10 as a fixed small replication (for example ≈60 runs, point-estimate rule as in C_design_review H6.4).

**11 · MED · §7 budget note (P0-17) — the run count and time ranges are slightly off.**
- (a) **Run count.** A1 is 4 arms × 48 = 192 runs. A2 is S6-P + M + an S4 re-baseline = 144 runs, plus C1 re-run (#3)
  = 48. The total is **336–384 runs, not ≈290**.
- (b) **Time range.** At the pilot's measured 1.7 min/run mean, 336–384 runs is ≈10–11 h of pure run time. The pilot also
  had ≈16% voided or re-run runs (dead bridge, port-file lock, parser), two 7.7–8.9 min runs, and S4/S5 outliers of 3 min.
  Composite arms are unmeasured. A realistic range is therefore **≈12–25 Editor-hours** plus incident debugging, not 8–20.
  The "1.5–4 min" range is fine for agent time but understates the low end once resets and re-runs are included.
- (c) **RU summary inconsistency.** The §0 RU summary says "≈5–15 прогонов на руку" ("≈5–15 runs per arm"); the plan
  specifies ≈48 per arm.

Fix: restate the count as ~340–380 runs, add a 20% contingency for re-runs, and correct the RU line to "≈6 задач × ≈8
прогонов на руку" ("≈6 tasks × ≈8 runs per arm").

**12 · MED · §0 RU summary, §10 G15, §18 H3 — "M is the leading hypothesis / probably the most plausible" reads as evidence-based. It is the owner's prior.**
Nothing in the pilot tested M, S6-P, composites or C1. The pilot's observed parity was for a flat S6-domain. Fix: label it
"owner's prior / design intent, no empirical support yet (H3 UNTESTED)" in all three places, and keep the arms symmetric in
the prereg. M must not be the default finalist.

**13 · LOW · §18 H4 status "PILOT SIGNAL (recall@8 .55 → .88)".**
This is an offline BM25 retrieval result on a small intent set (A_tool_search). It is not an in-client measurement:
PILOT_RESULTS §6 states that Codex code mode does not expose discovery. Fix: "OFFLINE SIGNAL (retrieval only; in-client
discovery unmeasured)". Similarly, H6's "−20–100×" is emitted volume on contamination-exposed normalisers. It is already
labelled, but say "on 3 pilot tasks".

**14 · LOW · §18 H5 SURVIVED "20/20 reload recoveries".**
20/20 bounds the failure rate only at ≲14% (95% upper bound). That is fine as a screen, but call it a screen.
H7 "≤ 1 day and ≤ 200 LOC" on one or two version steps is likewise N = 1–2. State it as descriptive.

**15 · LOW · Stage A ↔ Gate F independence.**
State that Stage A runs are **not** pooled into Stage B / Gate F estimates, and that Stage A tasks may be reused only if the
oracles stay frozen and the prompts are unseen by arm builders between stages. Otherwise selection on Stage A noise inflates
Gate F (a winner's curse on finalists).

## Answers to the brief, in short
1. Equal arms: false-falsify ≈1%, false-survive ≈5%. For +10 pp with 0.8× cost: survive ≈76%. For pure 0.8× cost: ≈37%.
   For −15 pp: falsify ≈38%. For 1.25×: ≈22%. Usable for "large effects only" only in the success+cost-together direction.
   Fixes are in #1, #2 and #4.
2. Yes. P(≥1 spurious survivor) is ≈15% (≈25–30% under the fixed rule). Acceptable only if Stage B is independent. The C1
   finalist condition is inverted (#7).
3. Neither rule is well-defined or consistent with the generic rules (#5, #6).
4. Coherent only for 2–3 arms, one class, homogeneous tasks, ≈235 runs per arm. D3 at 0.8 is power-capped at 50% (#8, #10).
5. Close but low. ~340–380 runs, ≈12–25 h realistic (#11).
6. "M leading", H4's offline signal, and the 1.5 min low end (#11–#13).
