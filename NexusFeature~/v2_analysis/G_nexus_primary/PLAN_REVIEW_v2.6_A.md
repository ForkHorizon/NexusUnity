# PLAN REVIEW — MASTER_PLAN_V2 v2.6 (reviewer A, document review only)

Date: 2026-10-04 · Scope: uncommitted v2.6 diff of `NexusFeature~/MASTER_PLAN_V2.md` vs `a5fe4d3`, read against
`G_nexus_primary/{PILOT_RESULTS, FINAL_REVIEW, PREREG_PILOT, A–F}.md`. No experiments, code or other files touched.

Overall: v2.6 does what the owner asked in most places — the expensive matrix is gone, Stage A/Stage B are separated,
M and C1 exist, architecture-independent work continues. But three things break the logic as written: (1) the
finalist rule can drop C1 from Stage B and caps the real challengers at one; (2) Gate F thresholds frozen in v2.5
were loosened without disclosure; (3) neither S6-P nor M has a defined route for ordinary scene writes, which §4
keeps Unity-direct. There is also a lot of stale v2.5 text left in §0, P0-16 and §12.

---------------------------------------------------------------------
## Part 1 — The ten questions

| # | Question | Verdict | Evidence |
|---|---|---|---|
| 1 | Avoids prematurely selecting S5 or S6? | **OK** | G15: "M (mixed Nexus-primary — current leading hypothesis, not approved)"; "Nothing architecture-dependent is scheduled". §0: "Это **гипотеза, не одобренная архитектура**". S5 is no longer "first". Minor point: M is called the "leading hypothesis" in §0, G15 and §14b. That is a prior, and the independent reviewer in P0-17f has to guard against it (obj. 14). |
| 2 | Capability reduction vs manager grouping distinguished? | **OK** | §0: "«13 менеджеров со всеми 154 командами» — не цель (≈4% экономии); цель — **сокращение видимых возможностей**"; §17.5: "manager grouping ≈4% (F23) is representation compression, distinct from capability-surface reduction (H1, H2)"; §3 P1 bullet. |
| 3 | C1 a required control? | **PROBLEM** | It is required in Stage A (P0-17b: "**C1** … (H1 control)"). It is *not* guaranteed in Stage B: P0-17f says "name 2–3 finalists (always including the current design …, and C1 **if H1 is not falsified**)". But the Gate F outcomes F and M both require "that C1 does not match it", so C1 has to be in Stage B no matter what. The condition is also backwards: if H1 *is* falsified (C1 ≈ Nexus), C1 is the E candidate and must go forward. The C1 definition also differs between sections: P0-16 says "same command subset as S6-curated", P0-17b says "same task-relevant subset". See obj. 1 and 9. |
| 4 | S6-P defined correctly? | **CONCERN** | H2 is close to the owner's intent: "compact typed Nexus surface (`nexus_status/snapshot/diagnose/compile/look/run/get` + ≤1 bounded query; real composites; no `eval` through Nexus; Unity hidden …)". Gaps: (a) the frozen task set includes "a simple scene change through an official primitive" (P0-16 task freeze), but S6-P has no write path, and §4 makes bounded writes "later research only". Coverage of write tasks is undefined, which affects D6. (b) "rare capabilities only via a typed discovery path if needed" is S6-curated's `describe/invoke` under another name, with no limit on scope. (c) The P0-16 lead text still defines S6 as "every primitive is delegated" with S6-domain/curated/router variants, which contradicts H2. See obj. 3 and 6. |
| 5 | Mixed a first-class candidate? | **CONCERN** | M is in the outcomes ("**M — mixed Nexus-primary**"), has a theory (H3) and is in G15. But P0-17b says M is "run only if operationally distinct from S6-P". If it is skipped, M can never become a finalist. And M's declared escape list (H3 / P0-17b) leaves out ordinary and destructive writes, which §4 keeps Unity-direct. Any write task would then count as an *undeclared* escape and push M under its "≥90% declared-category" rule. See obj. 3 and 4. |
| 6 | Avoids immediately spending 130–320 Editor-hours? | **OK** | P0-16c: "the earlier ≈130–320 h estimate described the full all-arms × all-clients matrix, which is **no longer planned**"; budget note "Stage A ≈ … 8–20 Editor-hours". Caveats: the stale §0 v2.5 paragraph ("Полный спайк P0-16 (Claude Code + Codex + Gemini, контроли C1/S1e/S6-bypass …)") and the stale P0-16 arm list still read like the full wave (obj. 6). |
| 7 | Cheap falsification and confirmatory testing clearly separated? | **CONCERN** | The separation is clear in structure: P0-17 "kills weak theories … it cannot confirm an architecture"; Gate F evaluates "only the 2–3 finalists". Missing: (a) a rule that Stage B uses fresh seeds and/or tasks and never pools Stage A runs (winner's curse / selection on the same data); (b) Stage A's H1/H2 comparisons span A1 (weeks 3–5) and A2 (weeks 6–8), so C1 and S5 are not run alongside S6-P/M. See obj. 5 and 8. |
| 8 | Architecture-independent tasks allowed to continue? | **OK** | §7 "Work classification (v2.6). *Architecture-independent — continues on schedule:* … P0-02 … L-02 … S-04 … S-07 … S-10 … S-11 … L-03 … S-06 … S-05 … L-04 …"; R30 "architecture-independent work proceeds". |
| 9 | Security policy separate from architecture evaluation? | **OK (with minor concerns)** | §4 "Security boundary (v2.6, policy — separate from architecture evaluation)"; §18 "security is never scored inside these rules"; Gate F "the architecture is judged on observed success …; the production-policy view decides deployability (security is evaluated separately, D9)". Concerns: D1 no longer says which success view it uses. The §4 phrase "never combined … *where that would weaken* client permission granularity" is a loophole. M's escape list contradicts §4 on writes (obj. 4, 12). |
| 10 | Any accidental implementation instruction? | **CONCERN (minor)** | Nothing says "build the façade now"; "Bench-only prototypes … never ship" and R29 are good. Risks: (a) P0-17b/d depend on "bench-quality" L-03/S-05/S-06/L-04. It does not say whether these are early versions of the production tasks or mocks in `bench/`. Either production tasks get shaped and rushed for S6-P, or bench mocks drift toward shipping (obj. 10). (b) S-11 "also builds the H4 arm" and S-10 change the shipped default surface. If they land before Stage A, the S4 baseline silently becomes S4+ (obj. 7). (c) Principle 6 promotes MCP from "thin projection" to "a first-class entry". That is a distribution decision made without evidence, while the bridge stays frozen (§4) (obj. 15). |

---------------------------------------------------------------------
## Part 2 — Numbered objections

### HIGH

**1. HIGH — The finalist rule can drop C1 and caps challengers at one.**
Location: §7 P0-17f; §11 "≤3 finalists enter Stage B"; §18 header "2–3 finalists"; Gate F outcomes F/M ("that C1 does not match it").
Problem: "name 2–3 finalists (always including the current design … and C1 if H1 is not falsified)". (a) Gate F's F and M rules cannot be evaluated without C1 in Stage B. (b) The condition is inverted: a falsified H1 makes C1 the E candidate. (c) S4 + C1 already fill 2 of the 3 slots, so at most one of S5/S6-P/M/S4+ can be confirmed. That contradicts "2–3 finalists" as candidates.
Fix: "Stage B always includes the comparators S4 and C1 (not counted as finalists) plus 1–3 challenger finalists (≤2 by default)." Remove "if H1 is not falsified". Size Stage B for 3–5 arms.

**2. HIGH — Gate F thresholds frozen in v2.5 were loosened without disclosure.**
Location: Gate F D1, D6, D7, D10; PREREG_PILOT §6 ("Thresholds there may only be tightened after this point"); Gate F "the prereg author may tighten, never loosen".
Problem: compared with v2.5, v2.6 (written after the pilot results were read):
- D7: drops the "hand-written normalisers/adapters ≤ 600 lines total" cap. There is now no numeric criterion at all ("measured on Pipeline N/N-1").
- D10: weakens "advantage holds in ≥2 of the 3 clients and it is not worse on D1 in any client" to "re-checked on ≥1 other primary client".
- D1: drops the explicit production-policy definition for S6.
- D6: changes how escapes count ("typed `describe`/`invoke` … count as discovery" is removed).
Some changes tighten (F/M must beat C1, paired design). These may all be defensible for a staged design, but §17.5 lists only "Gate F rewritten" and never states that thresholds were relaxed. The integrity norm that PREREG_PILOT and FINAL_REVIEW O5 enforced is now broken.
Fix: add to §17.5 a v2.5 → v2.6 Gate F diff table (tightened / loosened / re-defined, with rationale). Either restore a numeric D7 (e.g. reuse H7: ≤1 day and ≤200 LOC per version step, plus a total normaliser LOC cap) and a D10 that requires "not worse on D1" on the second client, or explicitly mark the loosening as owner-approved and made after the pilot.

**3. HIGH — No defined route for ordinary scene/object writes in S6-P or M.**
Location: §4 Security boundary ("bounded object writes → later research only (P0-16d); destructive writes → Unity-direct now"); H2; H3 escape list; P0-17b M ("declared Unity-direct escape for `eval`/`run_script`/free-form `batch`/packages/builds/risky settings"); P0-16 task freeze ("a simple scene change through an official primitive"); Gate F D6 / M "escape usage ≥90% within the declared categories".
Problem: production policy keeps writes Unity-direct, but M's declared escapes leave out writes and destructive writes, and S6-P has no write tool. A write task therefore fails coverage for S6-P and counts as an *undeclared* escape for M. M's score then depends on task mix, not on architecture.
Fix: add "bounded object writes and destructive writes (until P0-16d)" to M's declared Unity-direct categories in H3, P0-17b and Gate F M. For S6-P, state either that write tasks are out of its declared scope (judged on the read/diagnose class only, with coverage reported) or that a bench-only delegated bounded write exists for observation only and is scored under the production-policy view.

### MED

**4. MED — The "operationally distinct" condition can silently drop M.**
Location: P0-17b "M … run only if operationally distinct from S6-P"; budget "6 arms".
Problem: M is the stated leading hypothesis, yet it is optional. By H2's own definition S6-P has no Unity escape and M adds one, so they are distinct by construction.
Fix: define S6-P as "no Unity-direct surface" and M as "S6-P + declared Unity-direct escape (installed skill/CLI)". Make M mandatory in A2.

**5. MED — Stage B could reuse Stage A data (winner's curse).**
Location: P0-16c, §18 pipeline.
Problem: finalists are chosen on the Stage A tasks and seeds. If Stage B reuses those tasks or pools Stage A runs, the confirmatory estimate is biased upward.
Fix: "Stage A runs are never pooled into Gate F analyses. Stage B uses new seeds; ≥50% of Stage B tasks (incl. the ≥2 third-party ones) were not used in Stage A, or all tasks are re-run fresh." Freeze this in `PREREG_GATE_F.md`.

**6. MED — Stale v2.5 text contradicts v2.6.**
Locations and fixes:
- §0 v2.5 paragraph: "Решение: **ничего не строим.** Полный спайк P0-16 (Claude Code + Codex + Gemini, контроли C1/S1e/S6-bypass, S6 с типизированными … схемами и композитами) решит по новой матрице Gate F". Add "(заменено v2.6, см. ниже)" or delete the spike sentence.
- P0-16 lead: "It tests **three different architectures** …" with "Variants: **S6-domain** …, **S6-curated** …, **S6-router**" and "Controls: **S1e** …, **C1** (… same command subset as S6-curated …), **S6-bypass**". Mark these as v2.5 history and point to the P0-17b arm list and H2 as authoritative.
- Gate A: "S5/S5r/S1e runs … do not count toward A1". Update to the v2.6 arms (S4+, C1, S5, S6-P, M).
- R24 mitigation "S6-bypass cell measures bypass": that cell is now scheduled nowhere. R25 trigger ">25% of S6 task calls eval-like": S6-P has no eval. Update both (see obj. 11).
- Phase 0 load note still says "v2.4 adds ≈6 agent-days (P0-16a–c) plus ≈25–60 Editor-hours". Strike or restate.
- §17.4 dispositions O3/O4/O8 ("full spike: production-shaped S6 without eval **and** observed-with-eval variant; both reported"; "budgets unified (≈130–320 h)"). Add a v2.6 note: the observed-with-eval variant is dropped or exploratory only, and budgets are superseded.

**7. MED — S-10/S-11 change the S4 baseline.**
Location: S-10, S-11 ("also builds the H4 arm"), G17 "now", P0-17b S4 vs S4+ ("legacy dumps hidden" is part of S4+).
Problem: if S-10/S-11 ship in Phase 1 (weeks 2–4) before Stage A (weeks 3–5), "S4 current" already contains the H4 changes. H4 then becomes untestable, and S4 is no longer comparable with the pilot's S4.
Fix: freeze S4 as a pinned commit (pre-S-10/S-11) in `PREREG_P0-17.md`. S4+ = S4 + S-10 + S-11. State that Gate A A1's "Nexus 1.7" is a separate baseline.

**8. MED — Stage A comparisons are not run in the same window.**
Location: P0-17c (A1: S4, S4+, C1, S5, weeks 3–5) vs P0-17d (A2: S6-P, M, S4 re-baseline, weeks 6–8); H1 "C1 vs S6-P/M vs S4"; H2 "vs S5".
Problem: H1 and H2 compare arms run weeks apart, across client/model updates and possibly a changed S4. The pairing "by task and seed" is then only nominal.
Fix: run C1 and S5 again in A2 (or run all H1–H3-relevant arms only in A2), and decide H1/H2/H3 only on concurrent cells. Update the budget (≈+96 runs).

**9. MED — C1 definitions disagree; a task-tailored allowlist is oracle-informed.**
Location: P0-16 Controls ("same command subset as S6-curated") vs P0-17b / H1 ("same task-relevant subset").
Problem: a subset chosen from the frozen tasks gives C1 knowledge of the tasks that S6-P's general surface does not have. That biases H1 toward FALSIFIED (→ E).
Fix: define C1 once. Use Unity commands covering the same *capability classes* as S6-P/M's surface, chosen by someone blind to the tasks and frozen before the tasks. Optionally report a task-tailored C1 as an exploratory upper bound.

**10. MED — "Bench-quality" composites are ambiguous.**
Location: P0-17b ("S6-P and M need bench-quality `nexus_snapshot` / `nexus_diagnose` / `nexus_compile` / `nexus_run` (L-03, S-06, S-05, L-04)"); P0-17d deps.
Problem: it is unclear whether these are early states of the production tasks (risk: shaped or rushed for S6-P) or separate mocks in `bench/` (risk: duplicated code that later "gets shipped").
Fix: "Stage A uses the production L-03/S-05/S-06/L-04 branches at their current state, with no S6-P-specific API changes; any bench-only adapter stays in `bench/facade_mock/` and is deleted after Gate F."

**11. MED — Bypass risk (R24) and option E (S1e) are no longer tested anywhere.**
Location: R24, §17.2 F-10 ("option E arm S1e, wins ties"), P0-17b arm list.
Problem: "Unity hidden" is central to S6-P and M, but nothing measures agents bypassing the façade when the official skill/CLI is installed. Option-E recipes have also vanished; outcome E is now tested only via C1/S4+.
Fix: add an S6-P-bypass (or M-realistic-install) exploratory cell in Stage B for any Nexus-primary finalist. Say explicitly that S1e is retired and E is represented by C1 + S4+.

**12. MED — Gate F D1 does not name the success view; D6 vs M rule is ambiguous.**
Location: Gate F D1 ("lower 90% bound of the paired difference"); "Every success figure is reported twice"; D6 "without leaving the candidate's preferred surface"; M "escape usage ≥90% within the declared categories".
Fix: "D1 uses observed success under the arm's declared capability set; the production-policy D1 is reported and must also pass for F/M to be *deployable*." For D6, state whether a declared M escape counts as "leaving the preferred surface". It probably should not count against D6, but it should be reported.

**13. MED — Budget, timing and capacity contradictions.**
- §0: Stage A "≈5–15 прогонов на руку" vs P0-17 "≈6 tasks × N≈8" = 48 runs per arm. Fix: say "≈5–15 на ячейку задача×рука" or "≈48 на руку".
- Budget note "≈ 290 runs" vs A1 (4 arms × 48 = 192) + A2 (3 arms × 48 = 144) = 336, since S4 runs twice. Re-add (and add obj. 8's re-runs).
- Timing: "P0-17a/b/c … weeks 3–5 … after the Gate A runs", but Gate A is "end of week 4", so A1 is effectively week 5 only. Say "week 5 (after Gate A)".
- Capacity: "~51 tasks" is unchanged although S-10 (2d) and S-11 (1.5d) were added, and P0-17 (+10d) was added to a plan that already "fills 12 weeks only if the gates pass". Say what is cut or postponed.
- Phase placement: P0-17 sits under "PHASE 0 (weeks 1–2)" but runs weeks 3–8. Move it, or note it.
- Stage B "expected ≈15–40 Editor-hours" is stated before the power calculation it claims to depend on. With 4–5 arms (obj. 1), the low end is <60 runs per arm, below the v2.5 power figure (≈46% power at 100 runs per arm). Replace with "sized by power calc; floor ≥100 runs/arm or the expected outcome is I".
- Stage B (weeks 9–11) overlaps Gate B (week 9, macOS + Windows) on the two fixture Editors. Add a scheduling note.

### LOW

**14. LOW — Register vocabulary is inconsistent; primary/secondary labels are missing; S5 has no theory.**
- Allowed states are "UNTESTED · PILOT SIGNAL · FALSIFIED · SURVIVED CHEAP TEST · CONFIRMATORY CANDIDATE · CONFIRMED · INSUFFICIENT EVIDENCE", yet P0-17f and §11 use "INCONCLUSIVE". Stage A "INCONCLUSIVE" maps onto the same INSUFFICIENT EVIDENCE label as a Gate F result. Fix: add INCONCLUSIVE (Stage A) as an allowed state, or keep INSUFFICIENT EVIDENCE but tag the stage.
- The owner asked for four primary and three secondary theories. The register does not label H1–H4 as primary and H5–H7 as secondary (it is only implicit in P0-17a). Add a "Tier" column.
- The selective façade (S5 / outcome C) has no H-ID and no survive/falsify rule ("S5 = selective façade (outcome C; H6 at task level)"), so the rules give no way to make it a finalist. Add H2b/H8 or state that S5 is evaluated under H6 + the generic Stage A rule vs S4.
- §11 "Theory register hygiene … every H1–H4 theory" leaves out H5–H7.
- The Stage A rules use point estimates on ≈48 runs per arm. Say so explicitly (−15 pp ≈ 1.5–2 SE); drop-one-task is a good guard.

**15. LOW — Principle 6 makes a distribution decision ahead of the evidence.**
"MCP is a first-class entry" (was "thin projection"), while §4 keeps the "Python bridge: freeze the 14 managers". Fix: phrase it as "MCP and `unity command` are both supported entries; which is primary is decided with H5/G16".

**16. LOW — Loophole in the approval rule.**
§4 "Reads, writes and destructive writes are never combined in one approval-scoped tool *where that would weaken client permission granularity*." Drop the qualifier, or define it, e.g. "destructive actions always live in a separate tool from reads".

**17. LOW — Duplicate heading.**
§7: "### PHASE 2 — Flagship### PHASE 2 — Flagship "Live truth" (weeks 4–6)". Fix the typo.

**18. LOW — The Gate F prereg file move loses provenance.**
v2.5's Gate F lived in `PREREG_GATE_A.md` §F, with a history note that it was fixed after one smoke run. v2.6 points to `PREREG_GATE_F.md` (good: no stale `PREREG_GATE_A §F` reference remains in the plan). But the "fixed on 2026-10-03 …" provenance sentence was deleted. Keep a one-line history note so obj. 2's diff is traceable.

**19. LOW — Two different headline metric lists.**
P0-16c "Headline metrics per arm: semantic success, false-success, total tokens per successful task, calls per successful task" vs Gate F's 10 "Primary metrics". Point P0-16c to the Gate F list.

---------------------------------------------------------------------
## Part 3 — Owner checklist coverage

| Item | Present? | Where / gap |
|---|---|---|
| Four primary theories | Partial | H1–H4 exist; not labelled primary (obj. 14) |
| Three secondary theories | Partial | H5–H7 exist; not labelled secondary |
| Register fields | Yes | §18 columns ID / Hypothesis / Why / Cheapest falsification / Required evidence / Primary metrics / Pass-fail-inconclusive / Status / Next action |
| Allowed states | Yes, inconsistent | §18 list vs "INCONCLUSIVE" in P0-17f / §11 (obj. 14) |
| Third-party task checklist | Yes | §7 "Third-party task quality checklist" (6 items) |
| Primary metrics, raw compression not headline | Yes | Gate F "Primary metrics …"; "Raw compression ratio is never a headline metric"; §11 |
| Outcomes A/C/F/M/E/I | Yes | Gate F outcomes; §9 A4; G15 |
| Legacy `unity_dump_scene_graph` cleanup | Yes | S-10, G17, §5.1 note, §11 |
| Shell-first not universal / transport independent | Yes | §3 P6, §4 Transport bullet, H5, G16 |
| Never mix reads/writes/destructive in one approval tool | Yes, with loophole | §4 Security boundary (obj. 16) |
| C1 required control | Stage A only | obj. 1 |
| S6-P replaces flat S6-domain | Mostly | H2 / P0-16c "never a flat regrouping"; stale P0-16 lead (obj. 6) |
| Architecture-dependent work blocked | Yes | Work classification, R29, façade backlog |
| Security separate from architecture evaluation | Yes | §4, §18, Gate F D9 (obj. 12) |
