# C — Independent design review of the S4/S5/S6 pilot and Gate F v2.5

Agent C, 2026-10-03. I did not design the façade, the normalisers or the success criteria.
Read: BRIEF.md, PREREG_PILOT.md, MASTER_PLAN_V2.md (P0-16, Gate F v2.5, plus §9 A4, KPI table and R23 that
reference it), TASKS_v1.md, bench/facade_mock/facade_mcp.py, bench/facade_mock/catalog/*, A_tool_search.md,
E_skeptic.md, F_advocate.md, F_facade_hypothesis.md §2.3–2.6, F_facade_evidence/raw_samples, git history.
Labels: **REPO** (file:line or commit), **MEASURED** (I ran it here), **HYPOTHESIS**, **UNKNOWN**.

Severity: **BLOCKER** = fix before pilot run 1 · **HIGH** · **MED** · **LOW**.
**[CHEAP]** = can be applied today before the pilot starts (minutes to ~1 h, no new arm, no extra runs).

---

## Summary table

| # | Sev | Topic | Cheap today |
|---|---|---|---|
| B1 | BLOCKER | Nothing that defines the treatment is frozen or hashed. The "pre-task" normaliser spec cannot be shown to predate the tasks from git | yes |
| B2 | BLOCKER | Answers can leak through the filesystem and through Editor state across runs | yes |
| B3 | BLOCKER | Call-level metrics are logged only for the façade, not for S4's `unity`/`nexus-unity` servers | yes |
| B4 | BLOCKER | Façade bugs: a silently ignored scene `path`, silently dropped arguments, a wrong `activeInHierarchy` | yes |
| B5 | BLOCKER | S6 forwards `eval`/`run_script`/`batch`, which D9 forbids, and there is no pre-registered scoring for that | yes |
| B6 | BLOCKER | Run protocol and label formulas are under-specified (reruns, caps, token formula, call unit, claimed_ok) | yes |
| H1 | HIGH | Normaliser features that look task-shaped (T9, T5, T2) and go beyond the Oct-2 spec | partly |
| H2 | HIGH | The contaminated lead picks the third-party T3★/T9★ targets | yes |
| H3 | HIGH | The pilot S4 is weaker than the plan's S4 (no official skill or routing text), and the guidance budget is unequal | yes (decide + record) |
| H4 | HIGH | Network-off vs network-on sandbox changes more than Unity loopback access | yes (canaries) |
| H5 | HIGH | Codex code-mode confounds: JS post-processing of results and >5 KB schema compaction | yes (record + scope) |
| H6 | HIGH | Gate F is statistically near-unreachable as written (per-task veto, D1 power, budget, D10) | no (plan edit) |
| H7 | HIGH | Decision-matrix logic: D3 escape clause, A vs INSUFFICIENT precedence, undefined "fail region" | wording only |
| H8 | HIGH | Pilot directional labels are noisy and can be reinterpreted | yes |
| H9 | HIGH | Owner-required capability coverage and measured discovery are missing from the pilot metrics | yes |
| M1 | MED | S6 mock has no Nexus composites, while S4/S5 keep legacy Nexus helpers | disclose |
| M2 | MED | Wrong-capability list is coarse and misses each task's known traps | yes |
| M3 | MED | Unity package/Editor parity across arms is not stated | yes |
| M4 | MED | Sibling collapse hides the target branches (T9 Segment_C (2), T7 Pier_03). This is a sufficiency risk and is untested | yes (offline SQ run) |
| M5 | MED | S6-domain is not a god-tool by count, but `nexus_code` carries universal `eval`, and managers exceed the 600-token rule | yes (report) |
| M6 | MED | 20% mis-routing gate removed after A's retrieval data existed; loosening vs "tighten only" | disclose |
| M7 | MED | T9 oracle rejects Unity's own leading-`/` path format | yes |
| M8 | MED | C1 cannot attribute S6-domain's gain; "matches" is undefined; the E product table is unscored | wording only |
| L1 | LOW | Stale cross-references (F1/F2/F6, ≤0.7×) and a missing `PREREG_GATE_A.md` | yes |
| L2 | LOW | Minor façade defects (unusable cursor, `nexus_inspect` args, "handle" name collision) | yes |
| L3 | LOW | Token proxy (cl100k), cached/uncached split, third-party N=1 | yes |

---

## BLOCKERS

### B1 — The treatment is not frozen; the "frozen to the v2.4 pre-task spec" claim is not verifiable [CHEAP]
- **Location:** PREREG §0 (mitigation), §1, §2. TASKS_v1 §0.4.
- **Evidence (REPO):**
  - `bench/facade_mock/facade_mcp.py`, `catalog/*`, `F_facade_hypothesis.md` and the per-arm Codex configs and AGENTS.md files are all **untracked** (`git status`). The façade's mtime is 16:50, after TASKS_v1 (16:25).
  - The v2.4 plan text that PREREG cites as the pre-task spec was never committed on its own. It went in with v2.5, PREREG and TASKS_v1 in the single commit 8571bcb. The last committed plan (25f9cb0, v2.3) has no normaliser spec.
  - The only pre-task evidence is `F_facade_hypothesis.md` (mtime Oct 2, untracked). Its §2.3 lists: depth-2, same-shape sibling collapse, drop default components, handle; console group by first line + level, counts, **top-5**, cursor, dedupe + file:line; find count + top-10; null/id stripping. It does **not** list `activeInHierarchy`, a `component` filter, errors-first ordering, an Assets/Packages-only origin, or `compilationFailed` (see H1).
  - TASKS_v1 §0.4 still has `<sha>`, `<KEY_TOTAL>`, `<KEY_DEEP>` and `<...>` placeholders, so the tasks are not formally frozen either.
- **Fix (today):**
  1. Commit the façade, catalog, `retrieval/`, the harness, the oracle file, the answer key and the per-arm `CODEX_HOME/config.toml` + AGENTS.md before run 1.
  2. Put their sha256 in PREREG §1.
  3. Fill TASKS §0.4.
  4. Add a "deviations from F_facade_hypothesis §2.3" table to PREREG §0 (content in H1).
  5. State plainly that the pre-task spec's date rests on a file mtime, not git.

### B2 — Answers can leak to the agent through the filesystem and Editor state [CHEAP to check]
- **Location:** PREREG §1 ("scratch copy of UnityTestForNexus"); TASKS §0.2 and §5.
- **Evidence:**
  - Nexus lives in `UnityTestForNexus/Assets/NexusUnity/`. A scratch copy of that project carries `Assets/NexusUnity/NexusFeature~/` unless it is explicitly excluded. That folder holds TASKS_v1.md (answers, paths, line numbers), PREREG and the façade. It also carries `.projectmem/` and `CLAUDE.md` (REPO).
  - Unity ignores `~` folders, but the agent's shell does not.
  - Codex `workspace-write` restricts writes, not reads. The agent can read anything under `$HOME`, including the original repo and `~/Library/Logs/Unity/Editor.log`.
  - That log accumulates **earlier runs'** agent activity and any oracle or self-check output, because the Editor restarts only every 20 runs.
  - Pipeline ships `report_evals` (catalog, REPO), "eval usage report". If the harness runs oracles or self-checks through Pipeline `eval`, their code (containing `buoy.r12`, `Segment_C (2)` and so on) may be retrievable by the next agent. **UNKNOWN**: whether it persists across console clear and reload.
- **Fix (today):**
  1. Build the fixture without `Assets/NexusUnity/NexusFeature~`, `.projectmem`, `CLAUDE.md`, `Research~`, and any `*.md` from Nexus. Install the Nexus package from a clean tarball or path instead.
  2. Run `grep -rIl -e "buoy.r12" -e "ManifestValidator.cs:47" -e "Segment_C (2)" -e "fixture-v1" <fixture> ~/.codex* <CODEX_HOME>` and require zero hits outside scene and script assets.
  3. Run oracles through a channel that does not use Pipeline `eval`, or clear the eval history in the reset. Check `report_evals` output after a dry run.
  4. Launch the Editor with `-logFile <harness-dir>/editor_<run>.log` outside the fixture. Rotate per run, or at least per task.
  5. Pre-register a **void rule**: any run whose transcript reads a path under the harness dir, the original repo, `NexusFeature~`, `Editor.log`, or calls `report_evals` is voided, reported, and not re-run.

### B3 — Call-level metrics are asymmetric: only the façade logs calls [CHEAP]
- **Location:** PREREG §4 ("from the Codex JSON stream + façade log").
- **Evidence:**
  - In S4 the `unity mcp` and `nexus-unity` servers have no call log. In S5/S6 the façade logs every call (`facade_mcp.py` `log()`).
  - On `gpt-6-luna`, MCP tools are called **from inside JS `exec`** (A_tool_search §0, REPO). One exec can loop over dozens of `tools.mcp__…` calls, and it is UNKNOWN whether the Codex JSON stream itemises nested calls.
  - So calls/success, wrong-capability, invalid-argument and retries (all headline or D5 metrics) would be measured precisely for S6 and approximately for S4.
- **Fix (today):**
  - Wrap **every** MCP server in every arm (`unity mcp`, `nexus-unity`, `nexus`) in the same transparent stdio logging proxy, about 30 lines; the façade's MCP loop already does this.
  - Define call units in PREREG §4:
    - MCP `tools/call` (from proxy logs) = the call metric;
    - shell commands are counted separately;
    - `exec` invocations are counted separately;
    - discovery = exec code that touches `ALL_TOOLS`, plus `nexus_list`/`nexus_describe`, plus `unity list`/`--help`.

### B4 — Façade defects that silently bias S6 (both directions) [CHEAP]
- **Location:** `facade_mcp.py`.
- **Defects:**
  - **(a) The official `path` parameter is ignored.** `norm_hierarchy` reads `args.get("scene")` (line ~92). The S6-domain manager exposes Unity's own parameter name `path` for `get_scene_hierarchy` (it is merged from the catalog). An S6 agent that asks for `path:"…/Docks_Test.unity"` silently gets the **active** scene (Harbor). On T3 this can yield a confident wrong per-scene count (a false-success path specific to S6). Map `path` to scene, and error if the scene is not open.
  - **(b) Unknown arguments are silently dropped.** `delegate()` removes unknown arguments and still succeeds, appending `_nexus_ignored_args`. S6-domain schemas are a flat union with `additionalProperties: true`. In S4 the same mistake is a Unity validation error that is counted as invalid-argument (D5; advocate falsifier #8). The façade converts errors into silent partial execution. This favours S6 on D5 and can hide wrong behaviour. Either return an error, as Unity would, or count every `_nexus_ignored_args` event as an invalid-argument call. Pre-register which.
  - **(c) `activeInHierarchy` is wrong under `root`.** In the filtered listing, the stack starts with `act=True` at the subtree root. If `root` is below an inactive ancestor, every hit reports `activeInHierarchy:true`. On T9 that would point an S5/S6 agent at a wrong reason. Walk the ancestors of `root` first.
  - **(d) Verified non-issue.** Stripping `globalId`/`instanceId` does not break find→inspect chaining. Unity's `get_component_properties` accepts a `hierarchyPath` target (raw_samples/comp.json, MEASURED).
- **Fix:** patch (a) to (c), add a 5-call smoke test (Docks_Test hierarchy by `path`; an unknown arg; `root` under `[Debug]`), commit, then freeze (B1).

### B5 — S6's forwarding of eval, writes and batch has no pre-registered scoring [CHEAP]
- **Location:** PREREG §2 ("S6* forward writes and `eval` … for research only"); Gate F D6 and D9.
- **Evidence:**
  - D9 says `eval`/`run_script`/agent `batch` are **never** delegated.
  - The S6-domain `nexus_code` manager exposes `eval`, `eval_file`, `run_script` and `batch` (catalog_groups.json, REPO).
  - T9 (find the AudioSource with playOnAwake among about 1.2k objects) and T3 (prefab-source identity) are natural `eval` tasks. An S6 win that rests on forwarded `eval` is a capability a production S6 cannot have.
  - D6 ("without leaving the compact surface") is trivially met in a hidden S6, where there is nowhere to leave to, unless "escape" is defined.
- **Fix (today), pre-register:**
  1. Tag every S6 run that called `eval`/`eval_file`/`run_script`/`batch` (from the façade log) as **"needed direct Unity"** for D6.
  2. Report S6 success twice: as observed, and under production policy, where such runs count as not achievable on S6. The production-policy number is **primary** for the directional label.
  3. Report `nexus_describe`/`nexus_invoke` use as "escape hatch".
  4. Report shell or file bypass (YAML grep, editing scene files, writing Editor scripts) as "bypass".

### B6 — Run protocol and metric formulas leave room for post-hoc choice [CHEAP]
- **Location:** PREREG §3–§5.
- **Missing and needed before run 1:**
  1. **Infrastructure-failure and rerun rule.** Define: Editor crash, Codex API or 429 errors, harness assert failure in reset steps 3/5/6/8. Say who decides, decided **before** looking at task output, with a maximum of 1 rerun and all reruns reported.
  2. **Per-run caps**, identical for all arms: wall time (for example 20 min), and a maximum of turns or tokens. A capped run counts as a failure.
  3. **claimed_ok method.** Use a fixed regex or rubric on the final message, scored blind to arm, or by two raters.
  4. **Token formula.**
     - Primary: Σ(input incl. cached + output incl. reasoning) over **all** runs of the arm on the task ÷ number of successes on that task. This is the cost per success, with no survivorship.
     - Per-task ratio first; pool as the **geometric mean of per-task ratios**, so T9/T3 do not dominate.
     - Report uncached input separately.
     - Define a task with 0 successes in an arm as ratio = ∞ for that arm.
  5. **Call unit:** as in B3.
- **Fix:** add PREREG §4.2 "Run protocol" and §5.1 "Formulas" with exactly the text above.

---

## HIGH

### H1 — Normaliser features beyond the pre-task spec that line up with task answers [partly CHEAP]
- **Location:** `facade_mcp.py` `norm_hierarchy`, `norm_console`, `NORM_PROPS`/tool descriptions; PREREG §0.
- **Evidence:**
  - **T9.** The filtered hierarchy returns a derived `activeInHierarchy` per hit, with full paths, filtered by `component`.
    - Raw Unity returns only `activeSelf` (raw_samples/hier.HEAD20k.json, MEASURED).
    - T9's answer is exactly "AudioSource, path, inactive ancestor". `component=AudioSource` returns the 18 bell plus 6 gull sources. The 12 `BuoyField_East/*` hits all show `activeInHierarchy:false`, which nearly answers the "why".
    - The S5 tool description even advertises "activeInHierarchy".
    - None of this is in F_facade_hypothesis §2.3. P0-12's generic "filter" covers a name filter at most.
  - **T5.** The pre-task spec has "dedupe + file:line, top-5". The mock adds:
    - errors-first sorting;
    - `top=10`;
    - an origin regex that picks the first `(at Assets/…|Packages/…)` frame across message + stackTrace.
    - For `LogException`, that first frame is the throw site, `HarborAmbience.cs:58`. This is exactly T5's discriminator: false success = "reporting only the logging site of E3".
    - The default `nexus_console{level:"error"}` output *is* the T5 ANSWER line. The Assets-only filter does fix the eval-frame caveat in §2.3(4), so there is a generic rationale, but the lead chose it knowing the trap.
  - **T2.** `compilationFailed` is passed through from `groundTruth`. Low concern, since it is a Unity field.
- **Fix:**
  - (today) Add the deviations table to PREREG §0.
  - (today) Drop `activeInHierarchy` from the filtered output. Return `activeSelf` only, as the spec and Unity do, or move it behind `detail`. If kept, label T9 "contamination-exposed".
  - (today) Pre-register a **sensitivity analysis**: every directional label is computed on all 6 tasks *and* on the 4 tasks excluding T5 and T9. If the label changes, the reported label is "contamination-sensitive", not toward/against.
  - (full spike) Have the normaliser feature list re-derived by an agent blind to TASKS, and run P0-16b sufficiency questions on normaliser output before agent runs.

### H2 — Third-party target selection by the contaminated lead [CHEAP]
- **Location:** PREREG §3 ("T3★ and T9★ templates on a copy of `Puzzle` … ground truth fixed by the self-check snippets before any run there").
- **Evidence:** TASKS §3 leaves the choice of prefab, condition and object open ("pick a prefab…", "pick a component+property condition"). The lead now knows which conditions the normaliser exposes cheaply, such as an inactive ancestor plus a component filter. Choosing after building the normaliser is a selection degree of freedom.
- **Fix (today):** either Agent G (blind) picks the Puzzle targets, or a mechanical rule is pre-registered:
  - for T3★, the alphabetically first prefab under `Assets/` meeting the T3★ criteria;
  - for T9★, the first (component, bool property) pair, in a fixed list order, that satisfies the uniqueness filter.
  Record the rule output before any run.

### H3 — The pilot S4 is weaker than the plan's S4; the guidance budget is unequal [CHEAP: decide and record]
- **Location:** PREREG §2 (S4 row, AGENTS.md sentences); plan P0-16 S4 definition ("Unity CLI / `unity mcp` / **official skills** + current Nexus + **routing instructions**"); E_skeptic C2.
- **Evidence:**
  - The pilot S4 gets one sentence: no official `unity-cli` skill and no Nexus routing text.
  - S5 gets explicit tool names for the normalised reads.
  - S5/S6 also receive the façade's MCP `instructions` field ("large reads come back normalised"). S4's servers get whatever Unity and legacy Nexus send. These were not counted.
  - S4 has internet: Codex web search, if enabled, plus curl/pip.
- **Fix (today):**
  - Explicitly disable Codex `web_search` in every arm's config.
  - Record each arm's instruction tokens: AGENTS.md + every server's `instructions` + skills.
  - Either install the official Unity skill in S4/S5 (plan-faithful) or rename the arm "S4-noskill" in PREREG. Choose before run 1, and do not add it later.

### H4 — Network-off vs network-on changes more than Unity loopback [CHEAP: canaries]
- **Location:** PREREG §1–2 (S6: "sandbox network off → loopback blocked"; S4/S5: network on).
- **Evidence and HYPOTHESES to check:**
  - (i) S4/S5 can reach the internet (docs, package registries, `dotnet restore`); S6 cannot.
    - For T2, `dotnet build` on a stale csproj is the documented false-success path (TASKS §1.8; advocate A2).
    - In S6 it may simply fail on restore, which lowers S6's false-success rate for a reason unrelated to architecture.
  - (ii) The S4 shell `unity` CLI may itself be broken under `workspace-write`, which blocks writes outside the workspace (for example `~/.unity` locks and logs). UNKNOWN. If it is broken, S4's advertised shell path is fake.
  - (iii) Whether code-mode JS `exec` has network access, and whether Codex sandboxes MCP server processes, is UNKNOWN. The S6 façade must reach Unity, and the S6 agent must not.
  - (iv) A file-system bypass (writing an Editor script into `Assets/` plus `execute_menu_item`) is open to all arms. That is fine, but it must be classified (B5).
- **Fix (today):** run three scripted canaries per arm, before run 1, from the agent's sandbox, and record them in PREREG:
  - `unity command console_status`: S4 must succeed, S6 must fail;
  - `curl -m3 https://example.com`;
  - `dotnet build` on the fixture.
  Then pre-register a T2 analysis split by "used a non-Unity compile check".

### H5 — Codex code-mode is a special case; the pilot cannot generalise beyond it [CHEAP: record and scope]
- **Location:** PREREG §0–1; A_tool_search §0, §1.
- **Evidence:**
  - (i) In code mode the agent can call `tools.mcp__unity__get_scene_hierarchy()` inside JS and filter the result before printing it. S4 already has a programmable normaliser, and Claude Code and Gemini do not. Normalisation value is therefore likely *under*-estimated here.
  - (ii) Codex compacts MCP input schemas over **5,000 bytes** (A §1).
    - S6-curated tools are 6.1–12.1 KB (curated.json, MEASURED).
    - S6-domain flat managers are up to about 2k tokens (metrics.json `domain_flat_reqdesc`), so several exceed 5 KB.
    - Unity's atomic tools mostly do not.
    - S6 may therefore be shown degraded schemas. This also violates the plan's own "each manager ≤ ~600 tokens" rule, so the mock S6-domain is not the S6 the plan would ship.
- **Fix (today):**
  - Log, per tool, whether compaction applied (from the code-mode declaration the model sees).
  - Add to PREREG §0: "labels describe Codex code-mode only; no transfer to deferral clients is claimed".

### H6 — Gate F is statistically near-unreachable as written [plan edit, not cheap]
- **Location:** Gate F v2.5, D1, D3, D10, the power statement; plan P0-16c budget (≈40–90 Editor-hours).
- **Evidence (MEASURED by simulation, binomial, equal true success):**
  - **D1 per-task veto.** "No task ≥3/10 runs worse" fires by chance in **68%** of comparisons at p=0.7 with 10 tasks (50% with 6 tasks; 44% even at p=0.85). F requires D1 against *both* S5 and S4, so a truly equivalent S6 fails D1 most of the time.
  - **D1 interval.** With 100 runs per arm, the 90% lower bound is about ±10.7 pp. At a true difference of 0, P(pass) ≈ **46%**. 80% power needs about 260 per arm.
  - **Budget.** The full spike has about 8 arms (S4, S5, S6×3, S1e, C1, S6-bypass) × 2–3 clients × 100 runs ≈ 1,600–2,400 runs. At 5–8 Editor-minutes per run including reset, that is ≈130–320 Editor-hours, against 40–90 budgeted.
  - **D10.** D10 adds per-client conditions ("not worse on D1 in any client") on about 33 runs per client, with no definition of "worse".
  - Net effect: the gate defaults to INSUFFICIENT or A regardless of truth. That is safe, but it means the spike cannot answer the question it is funded for.
- **Fix:**
  1. Use a paired design: same task and seed order, analysed per task, with mixed-effects or cluster bootstrap over tasks.
  2. Limit the **confirmatory** comparisons to S6-domain vs S4 and S5 vs S4. Everything else is exploratory.
  3. Change the per-task veto to "≥3/10 worse **and** one-sided Fisher p<0.10", or require ≥4/10.
  4. Define D10 "advantage holds" as a per-client point estimate ≤0.9, and "not worse on D1" as a per-client point estimate ≥ −10 pp.
  5. Restate the honest budget, or the expected verdict, in the plan.
  6. Adding a sequential look (for example after 50 per arm, with an O'Brien-Fleming boundary) is a tightening and is allowed.

### H7 — Decision-matrix logic gaps [wording, CHEAP]
- **Location:** Gate F v2.5 table and outcome list.
- **Gaps and fixes:**
  - **(a) D3's escape clause.** D3 "passes" if "D4/D5 passes instead". Then:
    - F's "≥2 of D3/D4/D5" can be met by D4+D5 counted twice;
    - C ("D3 passes on the read/diagnose class") can pass with **no token gain**, through D5 alone.
    - Fix: delete the parenthesis; C requires D3 proper.
  - **(b) Undefined outcomes.** "INSUFFICIENT if the D1 or D3 interval spans both pass and fail regions": D1 is a one-sided non-inferiority test, and no fail region is defined. Fix:
    - D1 fail = upper 90% bound < −10 pp;
    - D3 fail = lower CI bound ≥ 1.0;
    - **precedence: INSUFFICIENT before A.** A means "evidence of no benefit"; INSUFFICIENT means "unknown".
  - **(c) Mixed.** Mixed needs per-class power, and each class has about half the runs. Pre-state that Mixed requires the class-level D1/D3 intervals to resolve, otherwise the outcome is INSUFFICIENT for that class.
  - **(d) F vs C.** "C … and F does not pass" means a passing F suppresses C even when S6 ≈ S5. Since F already requires S6 to beat S5 by D3/D4 margins, this is consistent. Add a sentence saying so.
  - **(e) Gaming risk.** D5 "failed-discovery calls" is undefined, and B4(b)'s silent argument-dropping would let any façade win D5. Fix with B3 and B4.

### H8 — Pilot directional labels are noisy and can be reinterpreted [CHEAP]
- **Location:** PREREG §5.
- **Evidence (MEASURED by simulation, N=12 vs 12, equal p):**
  - "against-X if ≥3 fewer successes" fires by chance in **13–15%** of comparisons, about 35% across the 3 comparisons. Its power to detect a true −20 pp is about 49%.
  - "success ≥ comparator (point)" holds by chance in 58% of comparisons.
  - Combined with "tokens ≤0.75× **OR** calls ≤0.75×", "toward" has two shots.
  - In code mode, "calls" depends entirely on the unit chosen (B3).
  - Labels have no stated consequence ("may change research priorities"), so any label can be read as anything afterwards.
- **Fix (today):**
  1. Use the B6 formulas.
  2. Make tokens the single primary metric and report calls only.
  3. **toward-X:** production-policy success (B5) ≥ comparator − 1 run, **and** geometric-mean per-task cost-per-success ratio ≤0.75, **and** that ratio ≤1.0 on ≥4 of 6 tasks, **and** false-success not higher by >1.
  4. **against-X:** ≥3 fewer successes, **or** ratio ≥1.3.
  5. Apply the H1 sensitivity rule.
  6. Add a consequence table, for example:

     | Label | Consequence |
     |---|---|
     | toward S6-domain | S6-domain gets the first full-spike slot; S6-curated is dropped |
     | against S6-domain | full spike runs S5 vs S4 first; S6 only with the C1 and composite variants |
     | neutral | no reprioritisation |

### H9 — Owner-required metrics are missing from the pilot [CHEAP]
- **Location:** PREREG §4 vs plan P0-16a/c.
- **Evidence:**
  - Capability coverage (completed via the compact surface / escape hatch / direct Unity / bypass; capability vs discoverability loss) is absent.
  - Discovery is "code-mode tool filtering items if present". Retrieval correctness ("is the right capability in what discovery returned") is not measured, though P0-16a says "measured … never inferred".
- **Fix (today):** add to §4:
  - per-run coverage class, using B5's tags;
  - per exec that touches `ALL_TOOLS`: the filter expression, output tokens, whether the output was truncated at 10k, and whether the capability eventually used appeared in the **first** discovery output;
  - first-call routing for S4 (Unity vs Nexus vs shell).

---

## MED

### M1 — The S6 mock is "Unity re-skinned + normalised reads", not Nexus-primary with composites; S4/S5 keep legacy helpers [disclose]
- The plan's S6-domain includes "+ composites", and the advocate's A2 and A4 mechanisms rely on a compile→wait→diagnose composite and an Overlay-correct capture. The mock has neither.
- S4/S5 keep 14 legacy Nexus managers. Several of them directly help pilot tasks: `unity_get_scene_dependencies` (T3), `unity_ui_automation` (T1 verification), `unity_write_and_compile` (T2), `unity_dump_scene_graph` (T9).
- "No selected task *requires* a legacy-only capability" is true, but these tools *help*.
- In S6, `detail:"full"` is still clipped at 24k chars behind a handle, so S6 has no truly raw path.
- **Fix:** add to PREREG §2: "a negative S6 label does not refute a composite-bearing S6. Legacy helper use in S4/S5 is reported per run."

### M2 — The wrong-capability list is coarse and misses each task's traps [CHEAP]
- §4.1 flags only cross-domain reads (console during T9, and so on).
- It omits the traps TASKS defines:
  - T3: name-only `find_gameobjects`/search without a prefab-source check;
  - T7: any write to `Prefabs/Props/Lantern_Post.prefab`;
  - T2: `dotnet build`/msbuild as the compile check;
  - T5: reading warnings-only output as the final source.
- Judge by the **underlying Unity command** (façade log `unity_command`, proxy log for S4), not by tool name.
- **Fix:** extend §4.1 now. It stays "reported, not decisive".

### M3 — Unity-side parity across arms is not stated [CHEAP]
- TASKS §5 installs "the arm's own package/config" per arm. If the Unity packages differ (for example Nexus removed in S6), then domain-reload time, console lines and compile behaviour differ.
- Reset then forces re-resolution on every arm switch, and arms are interleaved.
- **Fix:** state that the Unity package set is **identical** in all arms, with Nexus installed everywhere and only client-side config differing. Assert identical console counts after reset.

### M4 — Sibling collapse hides the target branches; sufficiency is untested [CHEAP offline]
- `_shape` strips ` (n)`/`_nn` suffixes and collapses siblings with equal components and child count, expanding only `g[0]`.
- So `Segment_C`, `(1)`, `(2)` and `(3)` (one `Markers` child each) show only `Segment_C`'s subtree, and `Pier_01`–`Pier_04` likely collapse the same way.
- This is lossy against S6 (R23), not contamination. It is still untested.
- **Fix:** before run 1, answer SQ3.1/3.2, SQ5.1/5.2 and SQ9.1/9.2 from normaliser output only (no agent; about 10 min) and record handle-fetch counts. This is the P0-16b check in miniature.

### M5 — The god-tool check [CHEAP: report]
- 13 managers with 4–18 actions each is not a god-tool by count.
- However, `nexus_code` holds universal `eval`/`run_script`/`batch`.
- Schemas are a flat union with `additionalProperties:true`.
- Sizes are 0.3–2k tokens, above the plan's ≤600-token rule.
- **Fix:** report the per-action share of S6 calls. If >25% of S6-domain task-relevant calls go to `eval`/`run_script`/`batch`, label the S6-domain result "eval-mediated" (ties to B5).

### M6 — The mis-routing threshold was removed after retrieval data existed [disclose]
- The v2.4 "20% mis-routing" prerequisite (cited in E_skeptic §3.6) was removed in 8571bcb (17:05). That was after A_tool_search's routing data (16:36) was available.
- "No S6 result existed" is true, but this was a loosening relative to v2.4, made with partial routing evidence in hand. The plan rule is "tighten, never loosen".
- **Fix:** add one line recording that the owner approved the removal, with the timestamp, and keep mis-routing as a reported secondary metric.

### M7 — The T9 oracle rejects Unity's native path format [CHEAP]
- Unity returns `hierarchyPath` with a leading `/` (`"/Main Camera"`, `"/Canvas/Panel_0/…"`; raw_samples, MEASURED).
- The T9 normalisation strips whitespace and an optional `Harbor.unity:` prefix only. An agent that copies Unity's path verbatim fails in every arm.
- T3 `ANSWER` scene keys may come as `Harbor.unity`.
- **Fix:** strip leading `/` and accept `.unity` suffixes in both parsers before freeze. This is an oracle-tolerance change, applied to all arms, before any run.

### M8 — C1 attribution and the E rule [wording]
- C1 is "allowlisted to the same subset as S6-curated". There is no control for S6-domain, whose subset is all 154, i.e. Unity-only without the legacy Nexus tools.
- "C1 matches S6" is undefined; failing to find a difference is not equivalence.
- The E rule makes the unscored "product table" the tie-breaker. It is filled in after the results, so it can be steered.
- **Fix:**
  - Add "C1-full" (Unity's 154 only, no Nexus) as the S6-domain comparator.
  - Define "matches" as an equivalence test with the D1/D3 margins: the 90% CI of the difference lies within ±10 pp, and the ratio CI lies within [0.8, 1.25].
  - Pre-register the product-table rows, a −1/0/+1 scale, the evidence source per row, and a scorer who did not build the façade.
  - Tie rule: no architecture change. This is neither E nor the façade, which honours "E does not auto-win ties".

---

## LOW

### L1 — Stale references and a missing file [CHEAP]
- Gate F says it is "pre-registered in `PREREG_GATE_A.md` §F", but that file does not exist (`find`, MEASURED). PREREG_PILOT §6 points to the plan instead.
- §9 A4 still says "Gate F's F1, F2 and F6".
- The KPI table says "Gate F: ≤0.7×", while D3 says ≤0.8.
- R23 cites "F1"/"F6".
- **Fix:** name one authoritative location and update the references. Keep 0.8 as the threshold, since 0.7 was never in v2.5.

### L2 — Minor façade defects [CHEAP]
- `nexus_console` returns a `cursor` but accepts no `since`, so the cursor is unusable.
- `nexus_inspect` in properties mode forwards `component`/`field` to Unity, which then errors.
- Nexus "handle" (`hXXXX`) vs Unity "Handle" (globalId) in descriptions invites passing one where the other is expected.
- **Fix:** rename to `nexus_get{ref}` or document the difference.

### L3 — Measurement details [CHEAP]
- Tool-output tokens use cl100k, which is a proxy for a non-cl100k model. Label it as a proxy, or use o200k.
- Report cached and uncached input separately. Interleaving arms makes cache warmth noise, not bias. Say so.
- The third-party cells (N=1 × 3 arms) are descriptive only. Exclude them from labels explicitly.

---

## Cheap fixes to apply before the pilot today, in order
1. Patch B4 (a)–(c) and drop `activeInHierarchy` (H1).
2. Add the logging proxy for all MCP servers (B3).
3. Disable Codex web search in every arm (H3).
4. Build the fixture without `NexusFeature~`/`.projectmem`/`CLAUDE.md`, run the leak grep, set a per-run `-logFile`, check `report_evals` (B2).
5. Run the canaries (H4) and the offline SQ check (M4).
6. Relax the oracle path format (M7) and fill in TASKS §0.4.
7. Amend PREREG with:
   - deviations table (H1);
   - void rule (B2);
   - eval/coverage scoring (B5, H9, M5);
   - run protocol and formulas (B6);
   - new label rules, sensitivity rule and consequence table (H8);
   - wrong-capability additions (M2);
   - S4 skill decision (H3);
   - parity statement (M3);
   - scope line (H5);
   - M1 disclosure;
   - third-party selection rule (H2).
8. Commit everything and record the hashes in PREREG §1 (B1). Only then run 1.

Plan-level (not blocking the pilot, but before any full P0-16c run): H6, H7, M6, M8, L1.
