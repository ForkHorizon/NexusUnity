# F — Façade hypothesis: Unity CLI as a hidden backend behind a compact Nexus API?

Date: 2026-10-02 · Input to MASTER_PLAN_V2 v2.4 · Research and planning only (no production code)

> **v2.5 correction (2026-10-03):** this analysis tested an adjacent design (S4 + selective reads = S5) and its "option B
rejected" conclusion is **withdrawn**: Nexus-primary (S6) was not tested here. See `G_nexus_primary/` and MASTER_PLAN_V2 §17.3.
The "truncated at 25k" statement about Claude Code is also wrong (large outputs spill to a file; F21).
Evidence folder: `v2_analysis/F_facade_evidence/` (scripts, raw captures, two research notes:
`R_clients.md` = client tool-loading research with a URL per claim; `R_repo.md` = Nexus manager-tool history + measurements).

Evidence labels: **MEASURED** (run by us on this machine, reproducible from the evidence folder) ·
**REPO** (cited file/commit) · **DOC** (official external docs/source, URL in `R_clients.md`) ·
**REPORTED** (forum/blog/issue) · **HYPOTHESIS** · **UNKNOWN**.
Token counts use tiktoken `cl100k_base` as a proxy for Claude's tokenizer (expect ±10–20%); the spike
(P0-16) must re-measure with the Anthropic `count_tokens` API, as the benchmark protocol requires.

Environment for MEASURED rows: macOS (arm64), Unity 6000.3.16f1, Unity CLI 1.0.0-beta.10,
`com.unity.pipeline` 0.7.0-exp.1, project `UnityTestForNexus`, Editor **unfocused, Edit Mode**.

---

## 1. Assessment

**Previously assumed (v2.3).** The agent sees Unity CLI / official skills **and** a small Nexus core.
Routing between them is solved by skills, instructions and hooks. Nexus wraps Unity only inside its own
composites (`nexus_compile` over `unity recompile`). Fallback A4 ("token-lean proxy") was kept only for
the case where raw `unity command` output turns out ≥3× larger than Nexus equivalents.

**What the hypothesis changes.** It moves the agent-facing boundary: primitives stay Unity's, but the
model reaches them through a compact, stable Nexus API that routes to Unity, normalises and budgets the
output. That adds a third category between "duplicate implementation" (bad, already avoided) and
"Nexus composite" (the current product): the **delegated façade**.

**Strongest arguments for.**
1. *Output volume is a real, measured gap.* Unity's `get_scene_hierarchy` has **no depth/subtree/limit
   parameter** (its `path` is a scene file) and returns ~86k tokens for a ~1.1k-object scene; `console`
   has no grouping. Claude Code caps MCP output at 25k tokens by default, so in practice the agent gets a
   **truncated** answer and re-queries or falls back to `eval`. A prototype normaliser cuts the raw size by
   two orders of magnitude (§2.3), but that is an upper bound; the real saving per successful task is
   unknown. This is the one argument that does not depend on client deferral.
2. *Unity's surface churns* (Pipeline broke its API on every minor, F14). A stable Nexus name and output
   shape helps skills and recipes **only for the normalised reads**; a generated pass-through passes churn
   straight through, and hand-written normalisers that parse Unity output break first. Weak argument; it
   is counted as a maintenance cost (F5), not a benefit.
3. *Vendor practice and accuracy studies favour fewer, workflow-shaped tools* (Anthropic tool guidance,
   GitHub MCP read/write consolidation saving ~23k tokens on one toolset, Block's 30→2 tools; RAG-MCP
   13.6%→43.1% selection accuracy when the visible set shrinks) — DOC, `R_clients.md` §5, §7.
4. *Adoption.* One entry point for "Unity state" removes a per-step Unity-vs-Nexus choice. Plausible,
   **unmeasured**.

**Strongest arguments against.**
1. *The schema-context problem is mostly already solved by clients.* Claude Code defers MCP schemas by
   default (names only at start; observed directly in this session), Codex defers MCP tools behind
   `tool_search`, Cursor keeps names only since Jan 2026 (DOC). The 154-tool `unity mcp` surface costs
   ~20.8k tokens **only** in clients without deferral (Gemini CLI, tool search disabled, third-party
   base URLs). Shell-first use (`unity command`, which Unity itself recommends over MCP for
   shell-capable agents, DOC) costs ~0 schema tokens up front: discovery is on demand.
2. *The manager format itself saves almost nothing on schemas.* Re-splitting Nexus's 14 managers into
   71 atomic tools costs only ~4% more tokens (MEASURED, `R_repo.md` §2). The old saving came from
   **hiding** ~50 methods, not from the `action` discriminator. Manager schemas also bloat over time
   (2.7k → 5.3k tokens from v1.0 to v1.6, MEASURED) and needed aliases, per-action required params and
   "valid actions" error lists to stay usable (REPO, CHANGELOG 1.2.0–1.5.0).
3. *Agents already prefer Unity's path.* Unity ships 31–33 official skills that teach `unity command`;
   an agent with those skills installed will still see Unity's surface. A façade cannot hide what the
   user installed; it can only be a better choice.
4. *Maintenance.* A hand-written façade over ~154 moving commands is the old duplication problem in a
   new place (schemas instead of implementations). The current bridge is 1.9k Python lines of mostly
   hand-written if/elif routing with schemas duplicated in Python and C# (REPO).
5. *Normalisation can delete the needed fact.* Compression is only a win if the task still succeeds;
   Unity also loses information we cannot recover by wrapping (e.g. `get_component_properties` returns
   `"<unsupported:Generic>"` for `m_OnClick`, `m_Colors` — MEASURED); fixing that needs Nexus Core, not a
   façade.

6. *Cheaper alternatives exist* (option E): ask Unity for `depth`/`limit`/grouping upstream, and ship
   skill recipes with a `unity eval` snippet for a bounded hierarchy / grouped console, at zero shipped code.

**Unknown (needs the spike).** Whether agents pick the right manager/action as reliably as a
named Unity command; total tokens per *successful* task (not per call) under each design; whether a
targeted agent (filters, `--level error`, `find_gameobjects`) already avoids most raw volume; adoption
effect; real maintenance size of a catalog-driven adapter.

**Verdict on materiality.** It does **not** overturn the plan's core (Unity owns primitives, Live
truth, composites, gates). It **does** change: (a) Principle 1 needs the three-way distinction;
(b) fallback A4 becomes a scheduled, pre-registered decision instead of a conditional footnote;
(c) Gate A needs extra, separately reported arms and a separate Gate F; (d) §14b gets a row for unbounded
reads ("AVOID implementing; normalise; decided by Gate F"); (e) "duplicate" is defined by capability, not
code path.

---

## 2. Research findings

### 2.1 Confirmed / measured facts

| # | Fact | Label | Evidence |
|---|---|---|---|
| M1 | `unity mcp` (beta.10, Pipeline 0.7) exposes **154 tools, one per Pipeline command** (incl. 3 `nexus_*`), `listChanged:true`; with no Editor connected `tools/list` is **empty** | MEASURED | `unity_mcp_tools_list.jsonl`, `probe.py` |
| M2 | Full 154-tool schema ≈ **20.8k tokens**; names+descriptions ≈ 5.4k; names only ≈ 0.45k; median tool ≈ 106 tokens; largest `wait_for` 959, `batch` 614, `run_script` 583 | MEASURED | `tok.py` |
| M3 | Nexus bridge today: 14 managers ≈ **5.3k tokens** (plan says ~4.9k = chars/4); same 71 actions as atomic tools ≈ 5.6k (+4%); 120 atomic raw methods ≈ 8.3k | MEASURED | `R_repo.md` §2 |
| M4 | Nexus consolidation (commit `c6fcb1b`, 2026-05-16): 111 tools → 14 managers; at the time 2.66k tokens; no token figure recorded then | REPO/MEASURED | `R_repo.md` §1 |
| M5 | Competitor tools/list: Funplay 34 tools ≈ 6.6k; IvanMurzak 38 tools ≈ 36.7k | MEASURED | `R_repo.md` §2c |
| M6 | Raw Unity outputs on a ~1.1k-object scene (UI canvas 20×6×4 buttons, 30 enemies, 47 log entries): hierarchy 86.3k tokens, console tail 29.4k, `find_gameobjects name=Button_0` (120 hits) 10.8k, console `level=error` (6 identical errors) 4.1k, `get_component_properties` 0.35k, `get_serialized_fields` 1.0k. `--format human` is the same JSON pretty-printed (no smaller) | MEASURED | `raw_samples/`, `compact.py` |
| M7 | `get_scene_hierarchy` has only a `path` (scene file) parameter — no depth, subtree, filter or limit | MEASURED | `unity_list.json` |
| M8 | Warm `unity mcp` call ≈ **100 ms** median (83–146) for both `editor_status` and a 540 KB hierarchy; cold `unity command` ≈ **1.2–2.2 s**; parsing 560 KB JSON ≈ 1 ms | MEASURED | `warm.py` (n=20/5, unfocused Edit Mode) |
| C1 | Claude Code defers MCP tool schemas by default (names + server instructions at start), `alwaysLoad` opt-out, honours `list_changed` | DOC + observed in this session | `R_clients.md` §1 |
| C2 | Codex defers MCP tools behind `tool_search` when the model supports it; per-server `enabled_tools`/`disabled_tools`; `list_changed` not honoured | DOC (source) / REPORTED | `R_clients.md` §2 |
| C3 | Gemini CLI: no lazy loading; `includeTools`/`excludeTools`; honours `list_changed`; users report a 512-declaration API cap | DOC / REPORTED | `R_clients.md` §3 |
| C4 | Cursor: names-only context since Jan 2026 (−46.9% agent tokens in their A/B); historic 40-tool cap, current cap unknown; ignores `list_changed` | DOC / REPORTED | `R_clients.md` §4 |
| C5 | Unity docs: "use `unity command`/`unity eval` if your agent can run shell commands" (faster, fewer tokens than MCP); the `unity-cli` skill discovers commands on demand via shell | DOC | `R_clients.md` §6 |
| C6 | Accuracy vs number of visible tools: Anthropic docs say selection degrades past 30–50 tools; tool search raised Opus 4 49%→74%, Opus 4.5 79.5%→88.1%; RAG-MCP 13.6%→43.1% | DOC/paper | `R_clients.md` §5 |
| C7 | GitHub MCP consolidated to read/write tools with a `method` discriminator (−23k tokens / −50% on Projects) and **removed dynamic toolsets** (May 2026) | DOC | `R_clients.md` §7 |

### 2.2 What the "Unity tool-context problem" really is, per client

| Client / mode | Persistent cost of exposing Unity's 154 commands | Per-use cost |
|---|---|---|
| Claude Code, MCP (`unity mcp`), default | names only (~0.45k, MEASURED proxy) | each loaded schema stays for the session (~0.1–1k each) |
| Codex, MCP, model with tool search | names/namespace only (DOC, not measured) | top-8 search results |
| Cursor, MCP | names only (DOC) | description file read on demand |
| Gemini CLI, MCP | **~20.8k every turn** (all schemas; DOC + M2) | — |
| Any client with tool search off / third-party base URL | **~20.8k** | — |
| Shell-first (`unity command` + `unity-cli` skill) | skill text only (official skill ≈2.5k, C §5.2 estimate) | `unity command` listing on demand (human list ≈5k tokens, filterable) |

So "154 commands" ≠ "20k tokens per request". The persistent schema saving of a façade is **large only for
Gemini CLI and non-deferring setups**, and small (~0.5k) in the plan's primary clients (Claude Code, Codex).
The plan's primary persona uses Claude Code / Codex (§2.1).

### 2.3 Output compression (prototype normaliser, MEASURED, upper bound)

| Output | Raw (tokens) | Normalised (tokens) | How |
|---|---|---|---|
| hierarchy, ~1.1k objects (client delivers ≤25k, truncated) | 86,263 | 140 | depth 2, collapse same-shape siblings (`Panel_# ×20, 4 inactive`), drop default components, handle for the rest |
| console tail 100 (47 entries; truncated at 25k) | 29,405 | 236 | group by first line + level, counts, top-5, cursor |
| console errors (6 identical) | 4,144 | 37 | dedupe + `file:line` |
| find 120 objects | 10,759 | 151 | count + top-10 paths + cursor |
| component properties | 347 | 118 | drop null/ids |
| serialized fields | 983 | 468 | drop null/ids |

Caveats (why this is an **upper bound**, not a result): (1) the comparison is against *untargeted* calls; a
careful agent filters (`--level error`, `find_gameobjects` instead of hierarchy, `eval` with a LINQ query) —
except for hierarchy, which has no filter (M7); (2) sufficiency was not tested — the depth-2 view cannot
answer "what is under Row_3 of Panel_7" without a second call; (3) the scene is synthetic and very regular,
which flatters sibling collapse; (4) the normaliser mis-attributed a warning's `file:line` to the eval
compiler frame — heuristics need tests. The spike must measure tokens **per successful task**.

### 2.4 Latency
A façade adds a hop but the Editor tick dominates: warm Pipeline ≈100 ms per call regardless of payload (M8,
the throttled-tick cadence of F8); normalising 560 KB is ~1 ms. **A façade must never shell out to cold
`unity command` per call** (+1.2–2 s). Viable transports: a warm `unity mcp`/`unity shell --protocol ndjson`
child process from the bridge/shim, or the Pipeline HTTP endpoint directly (auth model to be confirmed in
P0-10). In-Editor in-process dispatch into Pipeline's `CommandRegistry` would conflict with §4 ("Pipeline is
a projection only, no reflection into the experimental package") unless P0-10 finds a public API.

### 2.5 Maintenance
`unity list --format json` returns a typed catalog for every command (name, group, description, typed
parameters with required/default) (MEASURED, `unity_list.json`). A **catalog-driven** adapter can therefore
derive domain-manager actions from Unity's own metadata at runtime (prefix/group → domain; parameters →
action schema) with zero hand code for pass-through, and keep hand-written code only for normalisers of the
few high-volume reads (hierarchy, console, find, search, component/serialized reads, test results,
compile diagnostics — 7–10 normalisers). Unity's own group field is currently uninformative (all 154 are
`built-in`), so domain mapping needs a small hand-kept prefix table. **HYPOTHESIS** until the spike sizes it.

### 2.6 Manager granularity
- **Atomic** (154 Unity tools): best per-call precision; fine under deferral; ~20.8k where nothing defers.
- **Domain managers** (~6–8 tools, `action` discriminator + per-action typed params): GitHub/Block pattern;
  Nexus history shows this works but schemas grow and need aliases + valid-action errors; under deferral
  a manager schema is loaded whole (Nexus `hierarchy_manager` = 1.7k alone), so it can cost **more**
  per task than 2 atomic schemas. Keep each manager ≤ ~600 tokens by putting rarely used actions behind
  the escape hatch.
- **One generic router** (`nexus_call{command,args}`): smallest schema, weak typing; needs a discovery
  call first. Suitable only as the escape hatch (already in the plan as `nexus_call`/`nexus_list` in `full`).

Recommended shape to test: **normalised read commands merged into existing Nexus composites**
(`nexus_snapshot` for bounded hierarchy/find, `nexus_diagnose` for grouped console/test/compile output) and
implemented as Core `[CliCommand]`s, so they work over `unity command` without Python (Principle 6). An L1
client-side adapter over a warm `unity mcp` child is the alternative. Read-only allowlist; writes, `eval`,
`run_script` and `batch` stay Unity-direct and are never forwarded through Nexus (security invariants,
G3). The generic router is only measured (S5r), not recommended.

---

## 3. Architecture decision proposal

| Option | Verdict |
|---|---|
| A. Keep direct Unity + Nexus | Safe default; leaves the measured output-volume gap (M6/M7) to the agent |
| E. Upstream request + skill recipes (`unity eval` snippets, filtered calls) | Cheapest; zero shipped code; **tested first** (P0-12 request, S1e arm) — wins Gate F ties |
| B. Full Nexus façade | **Not supported by evidence.** Schema savings are small in deferring clients (§2.2); manager format saves ~4% (M3); hiding Unity's surface is impossible when official skills are installed; maintenance risk is real |
| C. Selective façade | **Most promising product option**: normalised read-only commands for high-volume / unbounded reads (hierarchy, console, find/search, component reads, test/compile results) with budgets and handles; everything else Unity-direct |
| D. Defer until a spike | **Chosen, with C as the hypothesis the spike tests** |

**Recommendation: D → (E, then C).** Do not build a façade now. File the upstream request (P0-12), test
option E and option C against S4 in a pre-registered spike (P0-16a–c) and decide at Gate F (week 5, does not
block Gate A). B stays rejected unless all Gate F criteria pass **and** a measured mis-routing problem
(≥20% of relevant runs) appears in S4 or in the no-deferral cell.

---

## 4. Proposed spike (added to the plan as P0-16a/b/c and Gate F)

See MASTER_PLAN_V2 §7 (P0-16a–c, Gate F). Summary:
- **P0-16a schema + discovery matrix (1d):** per client (Claude Code with and without deferral, Codex,
  Gemini CLI; Cursor if cheap) for arms U-MCP, U-shell, N, U+N, F, F-router; `count_tokens` API; output
  truncation recorded.
- **P0-16b corpus (2d):** raw (and as delivered after truncation) vs option E vs normalised tokens for ≥7
  output classes on the fixture **and** a real third-party scene; sufficiency questions frozen **before**
  normalisers by an author who has not seen them; façade sizing (hand-written normalisers, N/N-1 contract
  tests); overhead.
- **P0-16c arms:** S5 = S4 + façade (single-variable difference); S1e = S1 + option E; S5r router on one task;
  one no-deferral cell. ≈150 extra runs, ≈25–60 Editor-hours on both fixture Editors.
- **Gate F:** comparator S4; non-inferiority margins sized to ≈50 pooled runs per arm (±15 pp);
  F1 success, F2 tokens per success ≤0.7× with CI, F3 routing, F4 overhead, F5 maintenance, F6 sufficiency,
  F7 security; option E wins ties. Full text in the plan.

## 5. What remains unchanged
Unity owns primitives; no duplicate implementations; Live truth; `nexus_snapshot/compile/look/run/
diagnose`; truthful readiness; resumable jobs; budgets/handles/cursors; hooks; benchmark v2; Gate A/B
philosophy and pre-registration; legacy freeze; Pipeline compatibility; Windows validation; external testers.

---

## 6. Independent review (adversarial challenger, 2026-10-02)

A separate agent that did not write this analysis reviewed it and the plan diff: 13 objections
(6 HIGH, 4 MED, 3 LOW), verdict "partially agree" (defer + reject B correct; Gate F as first drafted was
underpowered, had an unfair "better of S1/S4 on every task" comparator, a contaminated task design,
a façade location contradicting Principle 6, and an `nexus_call` "100% coverage" rule that would forward
writes/`eval`). The full log with verdicts is MASTER_PLAN_V2 §17.2. All HIGH objections were accepted
and are reflected above and in the plan.
