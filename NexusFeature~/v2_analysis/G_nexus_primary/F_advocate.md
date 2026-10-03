# F_advocate: the case for a Nexus-primary architecture (S6) over S4

Agent F (pro-façade challenger) · 2026-10-03 · research only, no production code.

This is a deliberately one-sided document. Its job is to state the strongest case for S6 that the evidence
supports, and then to write down predictions and falsifiers before any P0-16 runs exist. Concessions are
stated explicitly (§0.2) so that a judge can see where the case is thin.

**Labels.**
- **MEASURED**: run on this machine. The evidence folder is `v2_analysis/F_facade_evidence/`, or the measurement is described here.
- **REPO**: a cited file in this repository.
- **DOC**: an official external document or source (URL given).
- **PAPER**: a peer-reviewed or arXiv paper (URL given).
- **REPORTED**: a blog, forum post or one of the AI Chat reports 1–4.
- **OBSERVED**: seen first-hand in this agent session.
- **HYPOTHESIS**: an inference that has not been measured.

Abbreviations:
- **S4**: Unity CLI / `unity mcp` (154 tools) plus Nexus plus routing instructions.
- **S5**: S4 plus a selective normalised read façade.
- **S6**: compact Nexus surface only. Nexus routes to official Unity commands and normalises the output.

---

## 0. Exact claim and concessions

### 0.1 Claim
For the target persona (Claude Code or Codex on a production Unity project, plan §2.1), S6 beats S4 by a
material margin. Material here means at least 0.7× tokens per successful task, with success equal or
better. The win is expected on four classes of tasks:

- (a) unbounded reads;
- (b) multi-step async loops;
- (c) intents where Unity exposes 3 or more overlapping commands with different semantics;
- (d) any client or setup that does not defer schemas.

On single targeted reads and writes, S6 should be neutral. The claim is **not** that S6 wins everywhere,
and it is **not** that the advantage comes from schema size in deferring clients.

### 0.2 Concessions (evidence against S6)
- **C1. Schema size is mostly solved in the primary clients.** Claude Code defers MCP schemas, so the
  model sees names only. Codex defers when the model supports tool search. Cursor keeps names only. With
  deferral, the persistent saving from hiding 154 tools is about 0.5–1.4k tokens. DOC/MEASURED:
  `R_clients.md` §1–4; `F_facade_hypothesis.md` §2.2.
- **C2. A large toolset does not always hurt strong agentic models.** In MCPVerse, Claude-4-Sonnet scored
  62.3% in Oracle mode (minimal tools) and 62.4% in Standard mode (about 220 tools, about 44k tokens). It
  degraded only in Max-Scale mode (about 550 tools): 44.2%. The authors attribute the Standard-mode result
  to the model finding alternative solution paths. PAPER: https://arxiv.org/html/2508.16260v2. At about
  150–220 visible tools, a strong Claude model may therefore be neutral.
- **C3. Consolidating syntax alone does not help.** Nexus's 14-manager form saves only about 4% versus the
  same 71 actions exposed as atomic tools (MEASURED, `R_repo.md` §2b). Block reports the same: its first
  Linear consolidation into category-parameter tools "reduced the number of tools but each still needed to
  be called individually". The win came only when calls collapsed. DOC:
  https://engineering.block.xyz/blog/blocks-playbook-for-designing-mcp-servers. Any S6 advantage must
  therefore come from fewer calls and smaller outputs, not from fewer names.
- **C4. Code mode is a competing remedy that already ships in Unity.** Anthropic reports that filtering in
  code before returning cuts usage from 150k to 2k tokens (DOC:
  https://www.anthropic.com/engineering/code-execution-with-mcp). Unity already ships `eval` and
  `run_script` (MEASURED, `unity_list.json`). Block's final Linear design was a query language (GraphQL),
  not a curated façade (DOC, same URL as C3). S4 combined with skill recipes for `eval` (option E / S1e) is
  therefore the real rival.
- **C5. Agents in the field avoided all Unity surfaces, not only Nexus.**
  - Report 1: `dotnet build` and `rg`.
  - Report 2: `dotnet build` plus 2 Nexus calls.
  - Report 3: YAML edits plus screenshots sent by the user.
  - Report 4: computer-use.
  - Source: REPORTED, `AI Chat reports Before all changes/1–4.md`.
  - None of the four sessions used Unity CLI either. S6 does not by itself fix the choice between shell
    and Unity.
- **C6. Unity itself recommends shell for shell-capable agents.** DOC:
  https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli.

---

## 1. Strongest arguments, ranked

### A1. Unity's high-volume reads are unbounded, and S6 can bound them at the source (strongest)

**The gap is measured.**
- `get_scene_hierarchy` takes only `path`. It has no depth, root, filter or limit.
- `find_gameobjects` has no `limit`.
- `console` defaults to `tail=100, level=log`.
- Source: MEASURED, parameters from `unity_list.json`.

Measured output sizes on a scene of about 1.1k objects (`F_facade_hypothesis.md` M6):

| Read | Tokens |
|---|---|
| Hierarchy | 86.3k |
| Default console | 29.4k |
| `find name=Button_0` (120 hits) | 10.8k |

**Correction to the plan.** The plan says Claude Code truncates oversized output at 25k. The current docs
say otherwise. When MCP output exceeds `MAX_MCP_OUTPUT_TOKENS` (default 25k; warning at 10k), Claude Code
"saves it to a file and replaces it in the conversation with a message that names the file path, so Claude
reads the file when it needs the content" (DOC: https://code.claude.com/docs/en/mcp). The cost of an
oversized output is therefore **extra Read or grep calls on a file**, not silent loss.

**This happened twice in this session (OBSERVED):**
- `get_summary` (94,200 characters) was persisted to a file.
- A 34.1 KB Bash `cat` was persisted to a file. Both needed extra calls to read.

**What follows for S4.**
- The shell path (`unity command`) is persisted the same way for Bash output (OBSERVED).
- The hierarchy JSON is pretty-printed at about 37 bytes per line: 20,000 bytes = 536 lines (MEASURED,
  `raw_samples/hier.HEAD20k.json`).
- The full 540 KB hierarchy is therefore about 14.6k lines (HYPOTHESIS, extrapolated).
- At the Read tool's 2,000-line default, that is at least 7 reads to see it, or grep calls that lose the
  tree structure (HYPOTHESIS).

**What S6 changes.**
- A prototype normaliser produces 140 tokens for the hierarchy (depth 2, sibling collapse), 236 for the
  console tail, and 151 for find. This is MEASURED, but it is an upper bound on a synthetic regular scene
  (`F_facade_hypothesis.md` §2.3).
- Vendor precedent for bounding output:
  - Playwright MCP exposes a `depth` parameter on snapshots and `--snapshot-mode` (DOC:
    https://github.com/microsoft/playwright-mcp).
  - Anthropic recommends a concise/detailed `response_format`; their example is 206 vs 72 tokens (DOC:
    https://www.anthropic.com/engineering/writing-tools-for-agents).
  - Anthropic also says "tool truncation and error responses can steer agents towards more
    token-efficient tool-use behaviors" (same URL).

**Why S6 and not only S5.** In S4 and S5 the unbounded raw command remains visible and is often the most
literally named match ("scene hierarchy"). In S6 it cannot be called by mistake (HYPOTHESIS; tested in
prediction P8).

### A2. Async Unity work forces multi-call poll loops; S6 composites collapse them

**The loops are measured.**
- Unity exposes **12 `*_status` tools** (MEASURED, `unity_list.json`): audit, build, console, editor,
  lighting/navmesh/occlusion bake, package, recompile, switch_build_target, test and wait.
- `recompile` says "Poll recompile_status for completion".
- `wait_for` warns that sync waits "hold the exec queue… set async=true and poll wait_status".
- A correct S4 compile check is at least 3–5 calls: `recompile` → poll `recompile_status` →
  `console --level error` (or `console_status`) → optionally `nexus_group_compile_errors`.

**External evidence that collapsing calls helps.**
- Block: questions needing "4-6 tool calls" became one call after redesign (DOC, URL in C3).
- Anthropic programmatic tool calling: average usage fell from 43,588 to 27,297 tokens (−37%). Accuracy
  rose from 25.6% to 28.5% on internal retrieval and from 46.5% to 51.2% on GIA (DOC:
  https://www.anthropic.com/engineering/advanced-tool-use).
- OpenAI: "combine functions that are always called in sequence" (DOC: `R_clients.md` §7).

**Field demand.**
- Report 2 asked for "refresh_assets_and_wait_for_compilation".
- Report 1 asked for "change → wait compile → read Console".
- Report 4 asked for "pack → wait for import → validate".
- Source: REPORTED, 1.md, 2.md, 4.md.

**The S4 failure mode in the field.** Agents substitute `dotnet build` for a Unity compile (reports 1 and
2, REPORTED). That does not exercise Unity's own compilation pipeline: Unity-only defines, asmdef and
package resolution, editor-only assemblies (HYPOTHESIS for the false-success effect).

**Caveat.** Composites are already in the plan (`nexus_compile`/`nexus_diagnose`). The S6-specific gain is
that the composite is the *only* compile path the agent sees.

### A3. Overlapping Unity commands with different semantics are a measured trap

**The overlaps.** Intent clusters in the 154-command catalog (commands MEASURED from `unity_list.json`;
grouping is my classification):

| Intent | Unity candidates | Semantic trap |
|---|---|---|
| Locate or inspect scene objects | `get_scene_hierarchy`, `find_gameobjects`, `search`, `get_selection`, `eval`, `eval_file`, `run_script` (7) | Only `search` has a limit. Hierarchy is unbounded. |
| Capture what the user sees | `capture_game_view`, `capture_scene_view`, `screenshot`, `nexus_capture_game_view` (4) | `capture_game_view` default `source=camera` "misses Screen Space - Overlay UI"; `source=screen` is "Play Mode only"; `screenshot` returns a path only. |
| Compile and diagnose | `recompile`, `recompile_status`, `console`, `console_status`, `nexus_group_compile_errors`, `wait_for`, `editor_status`, `reload_file` (8) | Polling plus the exec-queue footgun. |
| Read component data | `get_component_properties`, `get_serialized_fields`, `eval` (3) | `get_component_properties` returns `<unsupported:Generic>` for `m_OnClick` (MEASURED, F-hyp §1 against #5). |

**External evidence that overlap hurts selection.**
- Anthropic: "When tools overlap in function or have a vague purpose, agents can get confused about which
  ones to use" (DOC, writing-tools URL).
- Anthropic: "the most common failures are wrong tool selection and incorrect parameters, especially when
  tools have similar names" (DOC, advanced-tool-use URL).
- ToolScope: merging semantically redundant tools raised correct-selection rate by 34.6% on Seal-Tools,
  38.6% on UltraTool and 8.8% on BFCL, across GPT-4o, Llama-3.3-70B and Command-R (PAPER:
  https://arxiv.org/html/2510.20036v1).

**How S6 resolves it.** S6 maps one intent to one action, so "show me what the player sees" cannot land on
a camera capture that misses Overlay UI.

**Persona link.** This is the plan's headline persona problem: in report 3 the user sent screenshots by
hand (REPORTED, 3.md).

### A4. Visible-set size, where it is not deferred or where discovery happens by listing

**Evidence from studies.**
- OSWorld-MCP has 158 tools, close to Unity's 154.
  - Exposing all tools instead of a RAG-filtered subset lowered Gemini-2.5-Pro accuracy from 20.5% to
    15.5%.
  - Tool invocation rate on tool-beneficial tasks fell from 20.4% to 12.4%.
  - "When the number of available tools grew, both Acc and TIR dropped sharply, and ACS gradually rose."
  - Source: PAPER, https://arxiv.org/html/2510.24563, §4.2 and Table 2.
- MCP-Universe added unrelated servers to reach 94 tools. Claude-4.0-Sonnet location navigation fell from
  22.22% to 11.11%. GPT-4.1 browser automation fell from 23.08% to 15.38%. PAPER:
  https://arxiv.org/pdf/2508.14704.
- RAG-MCP: selection accuracy was 13.62% with the full set and 43.13% with retrieval. PAPER:
  https://arxiv.org/abs/2505.03275.
- Anthropic: selection "degrades once you exceed 30–50 available tools" (DOC:
  https://platform.claude.com/docs/en/agents-and-tools/tool-search-tool).
- OpenAI: "aim for fewer than 20 functions" and fewer than 10 per namespace (DOC: `R_clients.md` §5).
- Cloudflare exposes 2,500 endpoints through 2 tools in about 1,069 tokens, against about 1.17M tokens
  for an equivalent MCP server (DOC: https://blog.cloudflare.com/code-mode-mcp/).

**Where this applies.**
- Gemini CLI, which does not defer.
- `ENABLE_TOOL_SEARCH=false`, non-first-party base URLs, and Vertex models older than 4.5 (DOC:
  `R_clients.md` §1, §3).
- Shell discovery: the `unity-cli` skill tells the agent to run bare `unity command` to list all commands.
  That listing is about 5k tokens (`F_facade_hypothesis.md` §2.2).

**Why it ranks fourth.** It is weakened by C1 and C2 in the primary clients.

### A5. Adoption: S6 removes a per-step routing decision that agents demonstrably get wrong

**Field and benchmark evidence.**
- In reports 1–4, Nexus was called in 1 of 4 sessions, twice (REPORTED).
- In report 4 the agent drove Unity through computer-use GUI automation instead of any tool. It picked the
  wrong folder twice ("Missing assets.json or house_data.json"), hit timeouts, and gave up on the Unity
  path (REPORTED, 4.md).
- This matches OSWorld-MCP: even the best model used tools in only 36.3% of cases and defaulted to the GUI
  when tool necessity was ambiguous (PAPER, URL above).

**What S6 changes.** S6 cannot stop shell or file bypass (C5). It does reduce the Unity-side choice from
three options (Nexus, `unity command`, `unity mcp`) to one (HYPOTHESIS).

**Measurability.** Plan §11 targets an adoption rate of at least 60%. In S6 that metric becomes trivially
satisfied, so for S6 adoption must be measured as **"used any Unity surface when Unity truth was needed"**
against S4. That is the honest metric.

### A6. A stable contract over a churning backend (weak)

**Churn is real.** Pipeline broke its API on minor releases. 0.8.0-exp.1 moved its attributes to a new
assembly (REPO, `MASTER_PLAN_V2.md` §0 and §14b).

**What a façade gives.** Skills and recipes written against S6 names survive a rename of Unity's commands
(HYPOTHESIS).

**Why it is weak.** It holds only for normalised actions; pass-through passes churn straight through.
Hand-written normalisers break first (`F_facade_hypothesis.md` §1, against #2).

### A7. BFF / API-gateway analogy (supporting only)

**What the pattern doc says.** Microsoft's BFF pattern exists to "optimize the backend for the
requirements of specific client interfaces" and to aggregate calls. It also lists the costs: an extra hop,
code duplication and maintenance. It also notes that GraphQL "eliminates the need for a separate BFF
layer" (DOC: https://learn.microsoft.com/en-us/azure/architecture/patterns/backends-for-frontends).

**How it maps here.** The LLM is a client with a hard bandwidth constraint (its context). The analogy
favours S6 on payload shaping, and C4 (`eval` as GraphQL) is its own stated counter-case. HYPOTHESIS for
the transfer.

**Latency cost is negligible.** A warm Unity call takes about 100 ms and normalisation about 1 ms
(MEASURED, M8).

---

## 2. Pre-registered predictions (written before any P0-16 run)

**Conditions.**
- Claude Code with deferral on, unless the row says otherwise.
- Fixture scene of about 1.1k objects, plus 1 third-party scene.
- N=10 per cell.
- S6 = S6-domain or S6-curated with a read-only escape. Writes go to Unity commands routed by Nexus.
- S4 = plan configuration.

**Metrics.**
- TPS: median tokens per successful run, S6 divided by S4.
- Calls: median number of tool calls, S6 divided by S4.
- Success Δ: success-rate difference in percentage points.
- Wrong-tool: share of runs that call a semantically wrong tool at least once.

### 2.1 Where S6 should win

| ID | Task | Mechanism | TPS (S6/S4) | Calls | Success Δ (pp) | Other |
|---|---|---|---|---|---|---|
| P1 | Large-scene structural question ("which Row under Panel_7 has inactive buttons / where is the missing-script object") | A1, A3 | **≤0.40** (point 0.3) | ≤0.6× | **+10 to +30** | S4: ≥40% of runs pull an output >25k tokens (persisted to file) |
| P2 | Console triage (flood: 47 entries, 6 identical errors; "root cause + file:line") | A1 | **≤0.45** | ≤0.7× | 0 to +15 | S4: ≥30% of runs call `console` without `level` first |
| P3 | Compile → diagnose → fix → confirm clean (seeded CS error) | A2, A3 | ≤0.65 | **≤0.6×** | 0 to +15 | False-success rate (S6) ≤ ½ of S4. S4: ≥30% of runs use `dotnet build` or skip the Unity recompile confirmation |
| P4 | "Does the Overlay HUD show X?" in Edit Mode (Screen Space Overlay canvas) | A3 | ≤0.8 | ≤0.8× | **+20 or more** | S4 wrong-tool: ≥30% of runs call `capture_game_view` with default `source=camera` or `screenshot` first |
| P5 | Find all objects with component T (≥100 hits); report count + 3 examples | A1 | ≤0.5 | ≤0.8× | 0 to +10 | |
| P6 | Long async op (run tests, or package add then compile) to a final verdict | A2 | ≤0.7 | **≤0.5×** | 0 to +10 | |
| P7 | **No-deferral cell** (`ENABLE_TOOL_SEARCH=false`: U-MCP 154 tools vs S6), P1-type task | A4 | **≤0.5** (persistent schema ≈21k vs ≤2.5k per turn) | ≤0.9× | **+10 or more** | Wrong-tool rate halved |
| P8 | **S6 vs S5** on P1, P2 and P4 (isolates "hiding" from "normalising") | A1, A3 | 0.8–1.0 | ~1× | 0 to +10 | In S5, ≥25% of runs still call a raw unbounded or wrong-semantics Unity read at least once despite the Nexus version |

**Pooled high-volume tasks (P1, P2, P5).**
- TPS ≤0.45, against the Gate F F2 threshold of ≤0.7.
- Success non-inferior, with a point estimate of +10 pp.

**Across the full Gate A task mix, including neutral tasks.**
- TPS 0.75–0.9.
- Success +0 to +10 pp.

### 2.2 Where S6 should be neutral (|ΔTPS| ≤15%, |Δ success| ≤10 pp)

- Single targeted write: rename, `set_transform`, `set_active`, `add_component`.
- Single component or serialized-field read on a known object.
- Small scenes (<100 objects).
- Code-only tasks: C# edits with no Unity-state step.
- Settings reads.

### 2.3 Where S6 should lose (written down so the loss is not explained away later)

- **Long-tail capability outside the curated surface** (animator, timeline, bakes, settings writes):
  - S6-curated needs discovery plus an escape, which adds 1–2 calls.
  - Predicted TPS 1.1–1.3, success −0 to −10 pp.
  - If no escape exists, success falls to 0.
- **Tasks needing a fact the normaliser drops** (exact full serialized dump, or anything behind a
  collapsed sibling group): at least one extra handle fetch, TPS 0.9–1.2.
- **Agents with Unity's official skills installed:** S6 cannot hide `unity command`. Predicted to behave
  like S5 (C5/C6).

---

## 3. What would falsify this position

Each item names the result that would show the argument wrong, independently of the others.

1. **A1 is falsified** if, in S4, at most 10% of P1/P2/P5 runs receive an output larger than 25k tokens
   (agents use `find`, `--level error` or `eval` unprompted), **or** pooled TPS on P1/P2/P5 is above 0.7.
2. **A1 is falsified (the persist-to-file path is good enough)** if S4 runs that hit a persisted file
   recover with at most 1.5 extra calls and equal success.
3. **Option E beats S6** if S1e (skill recipes plus `eval` snippets) reaches TPS ≤1.1× S6 with equal
   success on P1/P2/P5. Then S6 is unnecessary (C4).
4. **S5 is enough** if S5 achieves at least 90% of S6's token reduction **and** S5's wrong-tool rate is
   within 5 pp of S6's (P8 fails). Hiding Unity then adds nothing.
5. **A3 is falsified** if S4 picks the Overlay-correct capture first in at least 80% of P4 runs, and the
   S6 success gain on P4 is below 10 pp.
6. **A4 is falsified** if the no-deferral cell (P7) shows a success Δ below 5 pp (the MCPVerse-like
   outcome, C2).
7. **A2 is falsified** if the S4 call-count ratio on P3/P6 is at most 1.25× S6's, or S4's false-success
   rate is no worse than S6's.
8. **Manager risk materialises** if S6's invalid-action or invalid-argument rate exceeds S4's
   named-command rate by 5 pp or more. Nexus already needed aliases and valid-action errors for this
   (REPO, `R_repo.md` §4).
9. **Sufficiency fails** if 20% or more of S6 runs need more than one handle fetch, or S6 success falls
   15 pp or more below S4 on any high-volume task (plan R23).
10. **Adoption is falsified** if, in S6, the share of runs that bypass Unity entirely (file, `dotnet`, GUI)
    when Unity truth is needed is within 10 pp of S4's. That would mean the field problem (C5) is about
    shell versus Unity, not about Nexus versus Unity.
11. **Maintenance is falsified** if the façade needs more than 10 hand-written normalisers, or more than 2
    contract breaks per Pipeline minor release (N/N-1).

If items 1, 3 and 4 all hold, the Nexus-primary case collapses to "ship recipes and a selective read
façade at most". I would accept that outcome.
