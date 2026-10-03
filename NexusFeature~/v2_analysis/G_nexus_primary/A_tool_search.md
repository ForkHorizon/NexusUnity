# A — How clients retrieve deferred tools, and what a big tool surface costs in retrieval and routing

Agent A, 2026-10-03. Labels: **MEASURED** (this bench), **REPO** (I read the client source or bundle; file and line given), **DOC** (URL), **REPORTED**, **HYPOTHESIS**, **UNKNOWN**.
Bench code and data are in `NexusFeature~/bench/facade_mock/retrieval/`:
- `intents.json`: 40 intents with labels, committed in 40db1db **before** any ranking was run.
- `rank.py`: the offline rankers.
- `bm25_ranker/`: the Rust crate Codex uses.
- `results.json`: per-intent top-K lists.
- `stub_mcp.py`, `run_codex*.sh`, `extract_codex_runs.py`, `validate_bm25.py`: the live Codex runs.
- `codex_runs/`: captured streams and `summary.json`.

## 0. Headline corrections to R_clients.md

1. **Current Codex models do not use `tool_search` at all.** In `~/.codex/models_cache.json`, every gpt-6.x and gpt-5.6.x model has `tool_mode = code_mode_only`. Only `gpt-5.5` has `tool_mode = null`, which means Direct mode, which means `tool_search`.
   - In code mode the model gets an `exec` (JavaScript) tool. Deferred MCP tools are not described in the prompt. The model finds them by writing JS that filters `ALL_TOOLS` (`{name, description}`) and then calls `tools.mcp__<server>__<tool>(…)`.
     - Source: REPO `codex-rs/code-mode-protocol/src/description.rs:16-17` ("filter `ALL_TOOLS` by `name` and `description`") and `:43`.
     - Mode selection: `core/src/tools/mod.rs:76-97`. `ToolSearch` specs are skipped inside code mode: `core/src/tools/spec_plan.rs` around line 857.
   - MEASURED in 7 of 7 runs with the default model `gpt-6-luna`. BM25 ranking is only what Codex does for Direct-mode models.
2. **BM25 in Codex indexes much less than "name, description and params".**
   - It indexes name tokens, server name, title, description, server `instructions`, and **top-level** `inputSchema.properties` keys only.
   - It does not index `oneOf` branches, enums, or parameter descriptions.
   - Nexus managers are pure `oneOf`, so BM25 sees only their roughly 40-character descriptions.

## 1. Mechanics by client

### Codex CLI 0.160.0
Source: tag `rust-v0.160.0`, a956835. All of this is REPO unless marked otherwise.

| Question | Answer | Where |
|---|---|---|
| Algorithm | `bm25` crate **2.3.2**, `SearchEngineBuilder::<usize>::with_documents(Language::English, docs)`, so k1=1.2, b=0.75, avgdl fitted to the corpus. The idf is `ln(1+(N-n+.5)/(n+.5))`. Query term weights are ignored; it uses idf times the document's term weight. | `core/src/tools/handlers/tool_search.rs:153-160,246-258`; crate `embedder.rs:164-191`, `scorer.rs` idf/score_ |
| Tokenizer | `deunicode`, lowercase, UAX#29 `unicode_words`, NLTK stopwords, Snowball English stemmer. **UAX#29 keeps `get_scene_hierarchy`, `unity_hierarchy_manager` and `Enemy.prefab` as single tokens**, while `Canvas/HUD` and `nexus-unity` split. Verified by running the crate. | crate `default_tokenizer.rs` |
| Indexed text | `"mcp__srv__tool tool tool srv [title] [description] [server instructions] <sorted top-level property keys>"` | `core/src/tools/handlers/mcp.rs:541-585`; test `mcp_search_tests.rs:8-15` |
| Default top-k | **8** (`TOOL_SEARCH_DEFAULT_LIMIT`). The model may pass `limit`; gpt-5.5 passed 10 in 3 of 3 runs (MEASURED). | `tools/src/tool_discovery.rs:7` |
| Ties | Scores are sorted from a `HashSet`, so the order of tied tools is not stable across processes. | crate `scorer.rs::matches` |
| What the model sees before searching | The `tool_search` description lists the **sources only**: `- <server>: <server instructions>`. It shows **no tool names and no tool count**. A model can still call a deferred tool directly if it guesses the exact name. | `core/src/tools/handlers/tool_search_spec.rs:34-95`; `router.rs:177-209` |
| Namespacing | Each MCP server is one namespace, `mcp__<server>__`. Results are **coalesced per namespace**, so the order the model sees is not the score order. In run6, the 6th-ranked Nexus hit was shown 9th. | `tool_search.rs:261-268`; MEASURED |
| Result injection | A `tool_search_output` item carries full function specs with `defer_loading:true`. Tools are callable from the next model call. | `tool_search_spec.rs:94` ("exposes matching tools for the next model call") |
| Persistence | The output item stays in history, so loaded tools persist across turns. Remote compaction **empties** `tools` in old `tool_search_output` items, so the model has to search again after compaction. | `core/src/compact_remote_history.rs:158-171` |
| When deferral applies | When `model_info.supports_search_tool` is true and the provider supports namespaces. There is **no tool-count threshold**: every MCP tool is deferred. | `spec_plan.rs:246-254,653-666` |
| `omit_tools_from` | A per-server list over {`direct`, `deferred`, `code_mode`}. `["deferred"]` loads that server's tools directly, which is Codex's equivalent of `alwaysLoad`. `code_mode.direct_only_tool_namespaces` does the same per namespace. | `protocol/src/config_types.rs:396-407`; `spec_plan.rs:207-254` |
| `enabled_tools` / `disabled_tools` | Filtered before exposure, at `ToolFilter`. | `codex-mcp/src/tools.rs:63-70` |
| Schema size | MCP input schemas over **5,000 bytes** are compacted (`tool_input_schema_max_bytes`). Nexus `unity_hierarchy_manager` is 5,457 bytes. | `config/src/mcp_types.rs:264` |
| Code mode (default models) | `ALL_TOOLS` descriptions carry the full TypeScript declaration (`augment_tool_definition`), so a broad filter dumps whole schemas. `exec` output is capped at **10,000 tokens** by default and is truncated beyond that. | `description.rs:22,393-397` |

### Claude Code 2.1.286
The ranking algorithm comes from the bundled CLI (`…/claude-code/2.1.286/…/MacOS/claude`, functions `VWn`, `$T` and the ToolSearch `call`). It is REPO-level evidence from a minified bundle.
- **Start of session:** bare deferred tool names, `mcp__server__tool`, plus server instructions (DOC https://code.claude.com/docs/en/mcp). We also observed this in the current session.
- **Query forms:**
  - `select:a,b` loads exact names.
  - An exact full name returns that tool.
  - An `mcp__…` prefix does a prefix match.
  - Anything else is a **keyword search with a fixed score, not BM25 and not regex**.
- **Keyword search details:**
  - Terms come from `query.lower().split()`, with no stemming and no stopword removal. `+term` marks a required term.
  - For MCP tools, `parts` is server and tool split on `[\s_.]+`.
  - Score per term:
    - +12 if the term exactly matches a part, else +6 if it is a substring of one.
    - +12 if it matches the full server or tool name exactly, else +4 if it is a substring.
    - +3 if it is a substring of the joined name and the score is still 0.
    - +4 for a match in `_meta["anthropic/searchHint"]` (word boundary).
    - **+2 for a match in the description** (word boundary).
  - Results have score > 0, use a stable sort, and return **5 by default** (`max_results`). Results go back as `tool_reference` blocks.
- **Implications:**
  - The name dominates the score: one name hit is worth 12–24 points, a description hit 2.
  - Hyphenated server names are not split. Because the term `unity` is a substring of `nexus-unity`, **every** Nexus tool gets +4 or +6 from it.
  - Stopwords in natural-language queries ("the", "on") add +2 to any description that contains them.
- **`alwaysLoad`:** per server, plus `_meta["anthropic/alwaysLoad"]` per tool, keeps tools out of deferral (REPO bundle string, and DOC).
- **Persistence:** loaded schemas are `tool_reference` blocks in history, so they persist. Behaviour after compaction is **UNKNOWN**.
- **Anthropic API variants:** `tool_search_tool_regex_20251119` and `…_bm25_20251119` are API-side only (DOC). Claude Code does not use them for MCP; it uses the client-side scorer above (REPO).

### Gemini CLI 0.60.0
- **No deferral** (REPO, installed bundle). `ToolRegistry.getFunctionDeclarations()` sends the full schema of every active tool on every request (`bundle/chunk-M6NSK26M.js:312184`).
- The only narrowing available is an allow list (`getMainAgentTools`) plus `includeTools`/`excludeTools`.
- The bundle contains no `tool_search`, `defer_loading` or lazy-tools code.
- No client-side tool-count cap was found. The API limit of 512 function declarations is REPORTED (R_clients §3).

### Cursor
DOC https://cursor.com/blog/dynamic-context-discovery
- Static context holds "names of the tools". Descriptions are synced to "one folder per server".
- The agent looks tools up with ordinary file tools: "grep", `rg`, `jq`.
- So retrieval is model-authored text search, like Codex code mode. Ranking is whatever the model's grep returns.
- Reported effect: −46.9% total tokens on MCP runs.
- Internals beyond this are **UNKNOWN**.

## 2. Offline retrieval benchmark (MEASURED)

**Setup:**
- 40 intents from a Unity programmer working on an existing project, each in two forms:
  - `raw`: natural language.
  - `kw`: a terse query in the model's style. These were written by someone who had seen the tool list, so they are **optimistic for Codex**, where the model sees no names. They are realistic for Claude Code, where it does.
- Acceptable tools are labelled per server.
- Rankers:
  - Codex: the real `bm25` 2.3.2 crate, run through a Rust shim.
  - Claude Code: a Python port of `VWn`.
- **Fidelity check:** the 3 real `tool_search` calls captured from Codex (§3) returned exactly the same top-10 sets as the offline ranker, **30/30 tools** (`validate_bm25.py`).
- K = 8 for Codex and 5 for Claude Code.
- For Codex I report the pessimistic rank, where tied tools are counted ahead. The optimistic rank differs by ≤0.03 on R@1.
- "Dup" means the top-K holds acceptable tools from **both** servers, i.e. the model faces a Unity-vs-Nexus duplicate.

| Ranker / query | Surface | n | R@1 | R@3 | R@8 (CC: R@5) | MRR | dup in top-K | 0-match queries | schema tokens per search (top-K) |
|---|---|---|---|---|---|---|---|---|---|
| Codex raw | **S4** Unity 154 + Nexus 14 | 40 | .53 | .72 | .85 | .64 | .28 | 2 | 1.2k |
| Codex raw | Unity only, 154 | 39 | .46 | .67 | .82 | .59 | – | 2 | 1.1k |
| Codex raw | Nexus only, 14 | 33 | .45 | .55 | .55 | .49 | – | 8 | 0.4k |
| Codex raw | S6-domain oneOf, 13 | 39 | .56 | .69 | .69 | .62 | – | 7 | 4.7k |
| Codex raw | S6-domain flat, 13 | 39 | .62 | .87 | .92 | .75 | – | 2 | 6.5k |
| Codex raw | S6-curated, 3 + describe/invoke | 39 | .38 | .49 | .49 | .44 | – | 18 | 1.6k |
| Codex kw | **S4** | 40 | .72 | .85 | .95 | .80 | **.45** | 0 | 1.4k |
| Codex kw | Unity only | 39 | .69 | .82 | .92 | .77 | – | 0 | 1.2k |
| Codex kw | Nexus only | 33 | .61 | .76 | .79 | .68 | – | 3 | 0.7k |
| Codex kw | S6-domain oneOf | 39 | .79 | .87 | .87 | .83 | – | 2 | 5.7k |
| Codex kw | S6-domain flat | 39 | .79 | .95 | .97 | .88 | – | 0 | 7.3k |
| Codex kw | S6-curated | 39 | .67 | .77 | .77 | .71 | – | 6 | 2.4k |
| CC raw | **S4** | 40 | .55 | .70 | .75 | .64 | .03 | 0 | 1.0k |
| CC raw | S6-domain flat | 39 | .59 | .79 | .90 | .73 | – | 0 | 6.0k |
| CC kw | **S4** | 40 | .88 | .90 | .90 | .89 | .15 | 0 | 0.8k |
| CC kw | Unity only | 39 | .87 | .87 | .90 | .88 | – | 0 | 0.8k |
| CC kw | S6-domain flat | 39 | .77 | .95 | .97 | .85 | – | 0 | 5.1k |
| CC kw | S6-curated | 39 | .36 | .41 | .41 | .38 | – | 15 | 1.3k |

The full grid is in `results.json`, including the hypothetical "Nexus actions listed in the description" variants.

**Routing decision on S4** (Codex kw; 32 intents where both a Unity and a Nexus tool are acceptable):
- Top-1 is a Unity tool in **28/32** and a Nexus tool in 4/32.
- Raw queries: 27 Unity, 3 Nexus.
- Claude Code kw: 29 Unity, 3 Nexus.

Retrieval therefore routes to Unity by default. The reason is index richness, not quality: Nexus `oneOf` managers expose about 40 characters of text.
- HYPOTHETICAL fix: appending each manager's action list to its description.
  - Nexus-only R@8 rises from .55 to .88 (raw), and from .79 to .97 (kw).
  - S4 routing shifts to 19 Unity / 13 Nexus.
  - Duplicate exposure rises from .45 to .65. **Better Nexus retrieval inside S4 increases duplicate competition.**

**Failure patterns** (from `results.json`):
- **Zero-match natural queries in Codex BM25.** "what's under Canvas/HUD" and "make Weapon a child of RightHand" match nothing. Tool names are single tokens, and words like "under" and "child" appear in no description.
- **Siblings outrank the target:**
  - "run tests editmode" puts `list_tests` (contains "EditMode") above `run_tests`.
  - "open scene" ranks `open_scene` 4th behind `set_active_scene`, `save_scene` and `save_all`.
  - "add package" ranks `package_add` 5th.
- "undo" has no tool on the Unity surface (only `menu`), and BM25 does not reach Nexus `editor_controller` either.
- **S6 trade-off:**
  - S6-domain flat has the best recall at rank 3 and beyond (.87–.97).
  - But every hit loads a 0.6–2k-token manager, so each search injects **4–7k schema tokens**, against about 1.2k for S4.
  - The `oneOf` variant of the domain surface loses 7 raw queries entirely, because Codex indexes no branch text.
  - **S6-curated is weakest:** 15–18 zero-match queries under either ranker. Its typed tools describe domains in about 12 words, and the escape tools (describe/invoke) only match generic words.

## 3. Live Codex 0.160.0 runs (MEASURED, 10 runs)

Setup: stub stdio MCP servers (`stub_mcp.py`) with no Unity Editor; tools/call returns a canned error. Other MCP servers, plugins, apps and hooks were disabled through `-c`/`--disable`; `~/.codex/config.toml` was not touched.

| Run | Model / surface | Intent | Discovery | First discovery output | Tool called | Turn input tokens |
|---|---|---|---|---|---|---|
| 1 | luna / S4 | Canvas/HUD | exec `/unity\|hierarchy\|scene\|gameobject/` over ALL_TOOLS | **26.4k → truncated to 11.3k** | nexus `unity_dump_scene_graph` | 67.5k |
| 2 | luna / S4 | refs Enemy.prefab | 4 exec filters | 29.1k → 11.4k, then 6.8k, 0.2k, 0.6k | unity `search` `ref:Enemy.prefab` | **174k** |
| 3 | luna / S4 | compile errors | 2 filters | 25.7k → 11.4k, then 2.7k | unity `nexus_group_compile_errors` | 99k |
| 4 | luna / S4 | Image color | 2 filters | 29.3k → 11.4k, then 1.2k | unity `get_scene_hierarchy`; final text claimed `unity_component_manager` | 96k |
| 5 | luna / S4 | EditMode tests | 3 filters | 25.7k → 11.4k, then 0.3k, 0.3k | unity `run_tests` | 122k |
| 6 | gpt-5.5 / S4 | Canvas/HUD | `tool_search` "Unity scene hierarchy get children GameObject path Canvas/HUD open scene", limit 10 | 2.9k | unity `get_scene_hierarchy` | 42.9k |
| 7 | gpt-5.5 / S4 | refs Enemy.prefab | `tool_search`, limit 10 | 2.0k | nexus `unity_search_manager` with strategy=semantic | 42.9k |
| 8 | gpt-5.5 / S4 | Image color | `tool_search`, limit 10 | 1.7k | unity `set_serialized_field` | 42.3k |
| 9 | luna / **S6-domain flat only** | Canvas/HUD | 1 filter | **11.9k → 10.6k** | `unity_scene` action=get_scene_hierarchy | 66.2k |
| 10 | luna / S6-domain flat only | Image color | 2 filters | 11.3k → 10.6k, then 3.1k | `unity_gameobject` action=set_serialized_field | 98.2k |

**Findings:**
- In code mode, the model's first filter always included `unity`, which matches the whole Unity-related surface.
  - On S4 that first dump was **25.7–29.3k tokens**, always clipped by the 10k cap. A clipped dump means the model sees only part of the surface. It then issued narrower follow-up filters: 1–4 extra exec calls.
  - On S6-domain flat, the same filter dumped 11.3–11.9k tokens, less than half. End-to-end turn input stayed about the same (66k vs 67.5k; 98k vs 96k, n=2). The fixed per-step prompt (about 15k, mostly cached) dominates.
- Routing on S4:
  - 6 of 8 S4 runs called a Unity tool and 2 called Nexus.
  - In run7 gpt-5.5 picked Nexus `search_manager` with `semantic` instead of `references`. The Unity `search` tool was never retrieved for that query.
- Run4 shows the model **misreporting** which tool it called after an error. Answer text alone is not reliable for routing analysis; use the call stream.
- These are 10 single runs: directional evidence, not rates.

## 4. What this means for S4/S5/S6 (HYPOTHESIS, grounded in the numbers above)

- **Deferral does not make a large surface free.** The cost moves from schema tokens to:
  - (a) retrieval misses: Codex BM25 raw R@1 is about .5, and 2 of 40 natural queries find nothing;
  - (b) duplicate competition: on S4, 28–45% of top-K lists hold both a Unity and a Nexus candidate;
  - (c) on today's default Codex models, model-written filters that dump 25–29k tokens on S4.
- **Current Nexus managers are close to invisible to BM25,** because `oneOf` branch text is not indexed. Any S4/S5 design that keeps managers must put action vocabulary in the top-level description or top-level properties. Claude Code also offers `_meta["anthropic/searchHint"]`.
- **A compact S6 surface roughly halves code-mode discovery dumps** and improves recall at rank 3 and beyond. But each retrieved manager costs 0.6–2k tokens, so per-search schema load is 4–7× S4's.
- **S6-curated with describe/invoke retrieves poorly** through every client search tested. It only works if it is always loaded: `omit_tools_from=["deferred"]` in Codex, `alwaysLoad` in Claude Code, and Gemini loads everything anyway.
- **UNKNOWN:** downstream task success. These are retrieval and routing proxies only. The live runs called stubs, so argument correctness was not scored.
