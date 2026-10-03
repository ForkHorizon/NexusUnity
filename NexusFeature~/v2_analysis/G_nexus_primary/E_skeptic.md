# E — Skeptic / anti-façade challenger: the case against S6 (Nexus-primary, Unity-hidden)

Date: 2026-10-03 · Agent E · Research only, no production code · Input to MASTER_PLAN_V2 v2.5
Labels: **MEASURED** (run on this machine / in `F_facade_evidence/`), **REPO** (file:line or commit),
**DOC** (official vendor docs/source, URL), **REPORTED** (blog/forum/third-party/search snippet),
**HYPOTHESIS**, **UNKNOWN**. Where a source cuts *against* this position it is marked **[COUNTER]**.

Scope: S6 = agent sees only a compact Nexus surface; Unity's 154 commands are reached only through Nexus
(variants S6-domain / S6-curated / S6-router). This note argues S6 is unnecessary or harmful, lists the
tasks a benchmark must contain so S6 cannot win by construction, and states what would prove me wrong.

---

## 1. Strongest arguments, ranked (strongest first)

### A1. "Unity-hidden" is not achievable in the target environment without restricting the user — so S6 measures a configuration users will not run
- The target persona uses Claude Code / Codex on a real project (MASTER_PLAN §2.1, REPO). Both give the
  agent a shell; `unity` is on PATH after install (MEASURED: `/Users/daliys/.unity/bin/unity`).
- Unity's official plugin installs with two commands into Claude Code at user or project scope and adds
  a `unity-cli` skill that tells the agent to run `unity command` / `unity command eval` (DOC:
  https://docs.unity.com/en-us/ai/unity-plugin/claude-code ;
  https://github.com/Unity-Technologies/skills/blob/main/skills/unity-cli/SKILL.md). Unity ships 31–33
  such skills (MASTER_PLAN §14b.2, DOC).
- Unity recommends shell over MCP for shell-capable agents (DOC:
  https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli).
- Field evidence: in 3 of 4 real sessions agents never called Nexus; they used shell, asked for screenshots
  or used computer-use (REPO, MASTER_PLAN F6, N=4, HYPOTHESIS-grade).
- Consequence: "hidden" requires a permission deny on `Bash(unity:*)` and not installing Unity's plugin.
  Without that, S6 in practice = S4 with a different Nexus. An S6 arm run without shell/plugin wins
  partly **by construction** (the competing path is removed, not out-competed). (HYPOTHESIS, testable as
  "leakage rate", §2 T12.)

### A2. S6 must either forward writes/`eval` (breaking the security invariant) or remove the agent's most familiar escape hatch
- The v2.4 façade design keeps "writes, `eval`, `run_script` and `batch` Unity-direct and never forwarded
  through Nexus" (REPO, `F_facade_hypothesis.md` §2/§3; G3). S6 by definition hides Unity, so it must
  forward them (contradicting G3 and D-11: `confirm:true` is agent-set, not a human gate) or drop them.
- Dropping them removes the path agents fall back to when a surface lacks a parameter: F22 records that a
  truncated hierarchy pushes the agent to "re-query or fall back to `eval`" (REPO, MASTER_PLAN F22).
- Unity's own launch demo is an agent inspecting runtime state with `unity command eval` (REPORTED, F11).
- Anthropic and Cloudflare argue agents do better writing code against familiar APIs than calling many
  bespoke tools (DOC: https://www.anthropic.com/engineering/code-execution-with-mcp — 150k→2k tokens;
  https://blog.cloudflare.com/code-mode/ — no metrics). The UnityEditor C# API has far more training
  presence than either Unity CLI command names (beta since 2026) or a Nexus vocabulary (HYPOTHESIS).

### A3. The benefit the literature measures ("small visible tool set") is already delivered by clients for the *official* surface
- Claude Code defers MCP schemas by default (names only), Codex defers behind `tool_search`, Cursor keeps
  names only (DOC, `F_facade_evidence/R_clients.md` §1, §2, §4).
- Anthropic's own numbers: tool search raises MCP-eval accuracy Opus 4 49%→74%, Opus 4.5 79.5%→88.1%
  (DOC: https://www.anthropic.com/engineering/advanced-tool-use). RAG-MCP 13.6%→43.1% and "How many tools"
  (~7 shown ≈ 50 shown) show the gain comes from *retrieval at decision time* (DOC: arXiv 2505.03275,
  2605.24660). None of these studies required renaming or re-wrapping the underlying API.
- Static subsetting of Unity's own tools needs zero code: Codex `enabled_tools`, Gemini CLI `includeTools`
  (DOC, R_clients §2–3). So "fewer tools" and "Nexus tools" are separable; an experiment that does not
  separate them credits Nexus for an effect clients already provide.
- Nexus history: the 2026-05 consolidation's saving came from **hiding ~50 methods**, not from the
  manager format (+4% only; MEASURED, `R_repo.md` §2b). Hiding is exactly what deferral/allowlists do.

### A4. Curated action vocabularies have a measured history of mismatching agent priors in *this* repo
- Nexus had to add aliases "for common raw-style action names such as `list_scenes`, `create_scene`,
  `create_gameobject`, `rename`", make invalid-action errors list valid names "to make AI recovery
  deterministic", and expand schemas into per-action shapes (REPO, `CHANGELOG.md:142,153,90,80`).
  This is direct evidence that agents sent underlying/raw names to the curated surface. No counts were
  logged (REPO, `R_repo.md` §4) — evidence of the phenomenon, not its rate.
- Schema growth: 14-manager array 2,655 → 5,316 tokens in 3 months (MEASURED, `R_repo.md` §1), i.e. the
  compact surface stopped being compact as it covered more cases.
- Issue 0015: manager-contract ambiguity (regex on names vs separate type filter) (REPO,
  `.projectmem/events.jsonl:144`).
- **[COUNTER]** Zechner's 120-run study: a custom tool with good docs and no training exposure reached 100%
  success, same as tmux (REPORTED: https://mariozechner.at/posts/2025-08-15-mcp-vs-cli/). So unfamiliar
  names are survivable with good docs; this argument is about cost/fragility, not impossibility.

### A5. Maintenance: S6 sits on the fastest-moving surface and must track it completely
- Pipeline breaks its API roughly every minor (0.6→0.7→0.8 in weeks; F14 breaking attribute move)
  (REPO, MASTER_PLAN §14b.1, F14).
- Unity's own skill warns: "an outdated CLI fails in ways that read like the docs being wrong" and newer
  releases "regularly add commands and output fields" (DOC, unity-cli SKILL.md). A façade pinned to a
  catalog version reproduces exactly that failure for every command it hides.
- The previous Nexus façade covered 63 of 121 raw methods (52%) and still needed a fall-through
  `call_unity(name)` for unknown names; schemas were duplicated in Python and C# (REPO, `R_repo.md` §5).
- GitHub removed its dynamic-toolsets layer as tech debt (complexity: extra flag, parallel registration,
  meta-tools) (DOC: https://github.com/github/github-mcp-server/pull/2512; no usage data given).
- API-evolution studies: LLMs use deprecated APIs in 25–38% of cases across 8 libraries (REPORTED via
  abstract: https://arxiv.org/abs/2604.09515). Relevance cuts both ways: it hurts direct Unity use too,
  but a façade adds a second version axis (Nexus×Pipeline) (HYPOTHESIS).

### A6. Unity is closing the output-volume gap upstream — the one argument for S6 that does not depend on deferral
- Unity 0.7 already ships context-economy knobs (MEASURED from the live catalog `unity_list.json`):
  `batch.result_fields` ("per-op result projection … (context economy)"), `get_serialized_fields
  format=value` ("a token-efficient read for scalars"), `search limit` (cap 200), `console level/tail/since`
  cursor. Whether `result_fields` can project `get_scene_hierarchy` inside a `batch` is **UNKNOWN**
  (Editor not running today: `STATUS_NO_INSTANCES`, MEASURED) — must be tested before crediting S6.
- The real gap that remains is narrow: hierarchy has no depth/limit; `find_gameobjects` has no limit
  (Button_0 → 120 hits, MEASURED `raw_samples/find.json`). That justifies S5/E for 2–3 reads, not
  hiding 154 commands.

### A7. Error semantics are degraded by re-wrapping (repo precedent)
- Unity returns `success:false` + `errors[{code,message}]` with actionable messages (DOC SKILL.md;
  MEASURED: `STATUS_NO_INSTANCES` "…run unity pipeline install"). Unity's skill keys troubleshooting
  (Safe Mode, sandbox false negatives) to these signals (DOC).
- Nexus's own wrapper never set MCP `isError` (Timeout/PartialSuccess looked like success, D-7) and leaked
  full stack traces (D-9) (REPO, MASTER_PLAN §1). Anthropic: errors must be "specific and actionable …
  rather than opaque error codes or tracebacks" (DOC:
  https://www.anthropic.com/engineering/writing-tools-for-agents).
- Same source: "tools that merely wrap existing software functionality or API endpoints" is a common
  error (DOC). S6-router is a near-pure wrapper.

### A8. Vendor precedent for "compact surface" keeps the *official* vocabulary
- AWS's managed MCP server (GA 2026-05-06) collapses 15k APIs into `call_aws` (takes AWS CLI commands)
  + `suggest_aws_commands` + skills + sandboxed scripts (DOC:
  https://aws.amazon.com/about-aws/whats-new/2026/05/aws-mcp-server/; tool details REPORTED via
  https://hub.docker.com/r/mcp/aws-api-mcp-server). Compact, but the agent still writes official CLI
  syntax. Playwright's README says coding agents "increasingly favor CLI–based workflows exposed as
  SKILLs" (DOC, R_clients §7). Neither renames the vendor surface.
- **[COUNTER]** Smithery (756 runs): agents do best with well-described APIs exposed through the
  harness's native tool interface; on GitHub's 826-tool API, MCP 100% vs CLI+descriptions 87.5%
  (REPORTED: https://smithery.ai/blog/mcp-vs-cli-is-the-wrong-fight, via search snippet). Supports good
  descriptions/native exposure — which `unity mcp` already gives — not Nexus specifically.
- **[COUNTER]** Scalekit (75 runs, Sonnet 4): `gh` CLI 100% vs GitHub MCP 72%, but MCP failures were TCP
  timeouts, and the CLI win is attributed to training-data familiarity with `gh` (REPORTED:
  https://scalekit.com/blog/mcp-vs-cli-use). Unity CLI has little such familiarity (beta 2026), so this
  favours `eval`/C# priors more than `unity command` names (HYPOTHESIS).

### A9 (weak, kept for honesty). Latency and client portability
- Latency: warm Unity call ≈100 ms, normalisation ≈1 ms (MEASURED, F22). An in-Editor `[CliCommand]`
  façade adds ~nothing; a Python hop adds little. **Not a strong argument against S6.**
- **[COUNTER] Gemini CLI loads all schemas** (no deferral; DOC/REPORTED R_clients §3). 154 tools ≈20.8k
  tokens up front (MEASURED F20). S6 genuinely helps here — but so does `includeTools` over Unity's own
  tools (DOC), which must be the comparator.

---

## 2. Failure cases the benchmark MUST include (so S6 cannot win by construction)

Design controls (non-negotiable):
- **C1 Same-subset control (S4-allow):** Unity's own tools restricted, via client allowlist
  (`enabled_tools`/`includeTools`) or a `call_unity(command, params)` router using **official names**
  (AWS pattern), to the same capability subset S6 covers. Separates "fewer tools" from "Nexus vocabulary".
- **C2 Equal guidance budget:** S4 gets the same number of instruction/skill tokens as S6 and the E recipes
  (`format=value`, `level=error tail=N`, `search limit`, `result_fields`, eval snippet). Count Nexus
  server instructions + skill tokens in S6's total.
- **C3 Realistic environment cell:** Bash available + Unity's official plugin installed (project scope).
  Report **leakage** = % runs where the agent calls `unity …`/Unity tools directly despite S6.
- **C4 Task authorship:** tasks frozen before the S6 surface is designed, by someone who has not seen it;
  ≥50% from the 154-command catalog sampled uniformly by *group of purpose*, plus reports 1–4 and a
  third-party project (MASTER_PLAN §2.1). End-state scoring, never agent self-report.
- **C5 Version-shift cell:** run each arm once on Pipeline 0.7 and once on 0.8.0-exp.1 (F14) with the façade
  built for 0.7, no fixes in between; record failures and the LOC needed to repair.

Concrete Unity tasks:
| # | Task (fixture or third-party scene) | What it probes |
|---|---|---|
| T1 | Add an Animator transition Idle→Run with a `Speed>0.1` float condition | long-tail authoring (`add_animator_parameter/state/transition`) outside a curated set |
| T2 | Set texture `Assets/UI/icon.png` to Sprite (2D/UI), no mipmaps, reimport | `get/set_import_settings` long tail |
| T3 | Add layer "Enemy", put all `Enemy*` objects on it, exclude it from the main camera culling mask | `set_tags_layers` + `set_layer` + component write, multi-step |
| T4 | Bake NavMesh surfaces and report status | async job commands (`bake_navmesh_surfaces`, `navmesh_bake_status`) |
| T5 | Create 30 objects under a parent atomically; on any error leave the scene unchanged (`dry_run` first) | `batch` transactional + `$ref` + rollback; S6 must keep atomicity or fail |
| T6 | "Which of the 120 `Button_0` objects is under `Panel_3/Row_2` and is inactive?" | duplicate names, inactive objects, handle fidelity (instanceId vs globalId) — normaliser info loss |
| T7 | Count objects with missing scripts across all open scenes; list their paths | natural `eval` task; S6 without eval must show an equivalent path |
| T8 | Read `m_OnClick` listeners on a Button | known Unity loss `<unsupported:Generic>` (MEASURED F_facade §1); façade cannot recover it |
| T9 | Fix a compile error that puts Pipeline into Safe Mode, then verify | error path where Unity is *unreachable*; the official skill has a recovery recipe |
| T10 | Run tests for an edited script (`unity test --affected`), summarise the one failure | async tests, official CLI-only subcommand |
| T11 | User pastes a Unity doc line: "run `unity command bake_occlusion_culling`" | vocabulary mismatch: S6 agent must map official name → Nexus |
| T12 | Any of T1–T11 in cell C3 | leakage / bypass |
| T13 | Hierarchy question on a 1.1k-object third-party scene ("where is the player's weapon socket?") | the case S6/S5 should win; included so wins are measured where real |
| T14 | Console: "why does Play Mode log errors?" with 2 distinct errors repeated 500× | grouping (S5/E strength); S4 given `level=error` recipe |
| T15 | Repeat T1/T5 on a weaker model (Haiku-class) and one non-Claude client (Codex) | prompt dependence of learned Nexus vocabulary |
| T16 | Gemini CLI cell: S4-full vs S4-allow (`includeTools`) vs S6 | isolates no-deferral benefit from Nexus |
| T17 | Editor reload mid-call / Editor not running | wrapped vs raw error recovery steps |

Metrics: success; tokens per *successful* task (incl. instructions); steps to recovery on T9/T17;
leakage; first-call routing errors; LOC + hours to keep S6 working across C5.

---

## 3. What results would falsify my position

I accept S6 (or a variant) is justified if, pre-registered, ≈50 pooled runs per arm:
1. S6 success is non-inferior (−15 pp margin, per Gate F) to **both** S4+E and C1 (S4-allow) on the
   long-tail/error tasks T1–T11, T17 — not only on T13–T14.
2. S6 tokens per success ≤0.7× of the **better** of S4+E and C1, with CI excluding 1.0.
3. Leakage in C3 ≤10% *or* S6 still wins (1)+(2) with leakage included.
4. In C5 the 0.7-built façade either keeps working on 0.8 or is repaired with ≤1 day / ≤200 LOC, and
   S4's own breakage is comparable.
5. Error recovery (T9, T17) needs no more steps than raw Unity errors.
6. A measured S4 mis-routing rate ≥20% that C1 does not fix (existing Gate F trigger).
Partial falsification: if S6 wins only on T13–T14, the evidence supports **S5** (selective reads), not S6.

---

## 4. Strongest alternatives and what evidence makes each win

| Alternative | Wins if | Evidence for it today |
|---|---|---|
| **E** — skill recipes + `unity eval` snippets + upstream depth/limit request | S4+E reaches ≤1.1× S5 tokens/success with equal success on T13–T14; or `batch.result_fields` can already project hierarchy; or Unity ships depth/limit | Unity already ships context-economy params (MEASURED catalog); zero shipped code; Anthropic code-execution pattern (DOC) |
| **S5** — selective normalised reads (hierarchy/console/find) inside `nexus_snapshot`/`nexus_diagnose`, Unity direct for everything else | S5 beats S4+E by ≥30% tokens/success on large third-party scenes without success loss on T6/T8; agents don't reliably use eval snippets | Measured ~86k-token hierarchy vs 25k cap (F22); no hierarchy depth/limit, no find limit (MEASURED) |
| **Contribute to Unity** (feature requests: hierarchy depth/subtree/limit, `find_gameobjects limit`, console grouping) | Unity accepts within a release or two; its trajectory (`result_fields`, `format=value`) suggests receptiveness | Public skills repo no longer takes PRs (MASTER_PLAN §14b.2) — so requests via Discussions/feedback, not PRs (REPORTED) |
| **C1 used as product** (document allowlists / official-name router) | C1 ≈ S6 on success and tokens | Client allowlists exist (DOC); AWS `call_aws` precedent (DOC) |

Net position: S6 is unnecessary in deferring clients (A3), unachievable as "hidden" in the persona's
environment (A1), in tension with G3 (A2), and maintenance-heavy on a surface that breaks every minor (A5).
The measured gap supports S5/E for a handful of unbounded reads (A6). The burden is on S6 to pass §3.
