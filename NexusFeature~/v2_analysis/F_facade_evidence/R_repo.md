# R_repo — Nexus manager-tool consolidation, token cost, reliability, façade cost

Repo: /Users/daliys/Daliys/UnityProjects/UnityTestForNexus/Assets/NexusUnity (branch docs/nexus-feature-notes).
Tokenizer: tiktoken `cl100k_base`, used as a PROXY for Claude's tokenizer (Claude's counts will differ; use the count_tokens API for final numbers).
Serialization: `json.dumps` with default separators, which is what the bridge writes (`Editor/nexus_unity_bridge.py:206`).
Scratch artifacts (all in this scratchpad dir): `current_tools_list.json`, `atomic_tools_list.json`, `cs_list_tools.json`, `parse_cs.py`, `measure.py`, `bridge_before.py` / `bridge_after.py`, `tag_*` exports.

## 1. When the consolidation happened (git history)

- CITED: commit `c6fcb1b` (2026-05-16) "feat: consolidate tools and sync documentation for open source release". `Editor/nexus_unity_bridge.py` went from 1214 to 463 lines.
- CITED: its CHANGELOG hunk says "Groups 64+ granular tools into **14 core entry points**", "Strict JSON Schemas: Implemented `oneOf` logic for all Managers", and "Removed 50+ redundant micro-tools from the public schema to minimize LLM token overhead and analysis paralysis".
- CITED: no token number was recorded at the time; the changelog states the motivation only.
- CITED: follow-ups:
  - `ee02934` / `dbe4584` (2026-05-16): NUnit suite for the 14 managers (`Tests/Editor/ConsolidatedManagersTests.cs`, 588 lines).
  - `f76f718` (2026-05-27): "Fix manager scene building flow" (routing.py +111).
  - CHANGELOG 1.0.0:241 "Consolidated MCP bridge API with 14 `unity_` manager and macro tools".
- MEASURED: tool counts before and after:
  - Before (`c6fcb1b^`), the bridge `STATIC_TOOLS` fallback had 111 tools, all with empty `properties`: 4,023 tokens, about 36 per tool.
  - Before, the bridge preferred Unity's live `list_tools`. Reconstructing that from the old C# gives 111 tools, about 7,149 tokens (prefixed), about 64 per tool. This is approximate because the helper schemas were taken from current code.
  - After (`c6fcb1b`): 14 tools, 2,657 tokens (tools array).
- MEASURED: growth of the 14-tool array (tools array only) at each tag:

| Tag | Date | Tokens |
|---|---|---|
| v1.0.0 | 2026-05-18 | 2,655 |
| v1.2.0 | 2026-05-31 | 3,163 |
| v1.4.2 | 2026-06-13 | 4,668 |
| v1.5.0 | 2026-07-12 | 5,256 |
| v1.6.0 | 2026-08-23 | 5,316 |

  The surface roughly doubled through per-action `oneOf` expansion and aliases.
- CITED: 36 commits have touched `Editor/nexus_bridge/` or the bridge script since 2026-05-16.

## 2. Token measurements

### (a) Current bridge tools/list (MEASURED)

`STATIC_TOOLS` was imported from `Editor/nexus_bridge/schemas.py`. Unity was not running: port 8081 refused, so live `list_tools` was unavailable.

- 14 tools:
  - tools array: 5,316 tokens
  - full JSON-RPC response: **5,337 tokens**, about 381 per tool
  - compact separators: 3,909 tokens
  - characters: 19,655
- Per tool: hierarchy_manager 1,668 (15 actions); editor_controller 693 (16); ui_automation 607 (8); asset_manager 521 (9); component_manager 491 (7); playerprefs 272; search 241; wait 237; scene 213; write_and_compile 133; invoke_method 88; dump_scene_graph 70; lint 40; get_scene_dependencies 39.
- The 14 tools expose 71 discriminated action branches in total.

### (b) Hypothetical atomic exposure

- MEASURED: I parsed every `CreateTool(...)` in `Editor/MCPServerMethods*.cs` (Unity's own `list_tools`, `MCPServerMethods.Tools.cs:23`) with a C# initializer parser: 117 tools, 0 parse failures. I added the 3 canonical commands from `Commands/*Command.cs` descriptors, for **120 tools**.
  - That gives **8,284 tokens** unprefixed and 8,412 with the `unity_` prefix.
  - Average 69 per tool, median 55.5.
  - 29 tools have no params and 52 have descriptions under 25 characters, so this is a terse baseline.
  - Cross-check: projectmem `events.jsonl:198` records "120 visible list_tools names … 29,160 UTF-8 bytes". My reconstruction is 27,999 bytes compact and 30,059 bytes spaced, which is consistent.
- MEASURED: there are 121 registered raw methods (`_methods["..."]`). Four are not listed: `initialize`, `is_asset_import_idle`, `is_editor_idle`, `list_tools`.
- MEASURED: an apples-to-apples alternative splits each of the 71 manager actions into its own tool with the same descriptions and params. That gives **5,566 tokens**, about 78 per tool. That is only about 4% more than the 14-manager form (5,337). The manager form saves almost nothing per covered action; its saving comes from hiding the other ~50 methods.

### (c) Competitor captures (MEASURED; tools array as `json.dumps`)

| Capture | Tools | Tokens | Per tool | File (raw) | Compact |
|---|---|---|---|---|---|
| funplay_tools_list.json | 34 | 6,551 | 193 | 7,684 | 5,755 |
| ivan_tools_list.json | 38 | 36,736 | 967 | 43,774 | 31,942 |

## 3. The "~4.9k (14 managers)" claim

- CITED: `NexusFeature~/MASTER_PLAN_V2.md:516` reads "Default tools/list tokens | ~4.9k (14 managers) | ≤2.5k". `MASTER_PLAN.md:109` reads "14 tools / 4.9k tok".
- CITED: `C_agent_efficiency.md:171` reads "Nexus's 19.7k chars (about 5–6k tokens)". `BENCHMARK_PROTOCOL.md:47` specifies the chars/4 method.
- MEASURED: 19,655 characters / 4 = 4,914, so the 4.9k figure is a chars/4 estimate.
- MEASURED: with cl100k the figure is 5,316–5,337, about 8% higher. The 4.9k figure **roughly agrees but understates**; "5–6k" in C §2 is the better statement.
- MEASURED: C's "Ivan about 36k" matches my 36.7k tokens.
- The ≤2.5k target for `core` is about the size of the original v1.0.0 manager set (2,655).

## 4. Reliability problems attributed to managers

- CITED: action-name confusion was handled by adding aliases and better errors:
  - CHANGELOG 1.2.0:142: "MCP manager aliases for common raw-style action names such as `list_scenes`, `create_scene`, `create_gameobject`, `rename`".
  - CHANGELOG 1.2.0:153: "Manager invalid-action errors now include valid action names to make AI recovery deterministic". See `routes_base.py:35`.
  - CHANGELOG 1.4.2:90: hierarchy schema "expanded into per-action shapes with clearer required parameters".
  - CHANGELOG 1.5.0:80: "`unity_scene_manager` schema aliases now preserve action-specific required parameters".
  - This points to agents sending raw method names as actions or wrong arguments, but no incident log with counts exists.
- CITED: `.projectmem/events.jsonl:144`, issue 0015: "unity_search_manager regex query searches GameObject names while component type search is a separate undocumented type filter". This is a manager-contract ambiguity.
- CITED: `AUDIT_FINDINGS.md:130-131`: routing `.get(key, default)` does not guard against explicit `null`, a latent TypeError across most `_route_*` handlers. `AUDIT_FINDINGS.md:58-59` covers raw NREs on a missing `component_name`.
- CITED: the AI Chat reports (1–4.md) contain no wrong-action or invalid-argument incidents. Nexus was barely used:
  - 2.md:13-19 used only `editor_controller refresh_assets` and `wait compilation`, which took about 19 s (that is issue 0012, a wait problem rather than a schema problem).
  - 4.md used computer-use instead of Nexus.

## 5. Bridge routing: façade or logic? (MEASURED)

- Line counts: bridge Python is 1,933 lines in total.
  - `routing.py`: 69 lines, a `_HANDLERS` dict of 14 names that falls back to `call_unity(name)` for any unknown name. This means raw methods are still callable through `tools/call` even though they are not listed.
  - `routes_managers.py`: 253; `routes_editor.py`: 272; `routes_ui.py`: 107; `routes_base.py`: 78.
  - `schemas*.py`: 789 in total (schemas 50, components 190, diagnostics 94, editor 181, managers 180, ui 94).
  - `nexus_unity_bridge.py`: 213.
  - Bridge tests: 859 lines in `Editor/tests/`.
- Most routing is a **hand-written if/elif façade**: action → raw method, with per-action argument renaming and `_compact`. Examples:
  - `set_name` maps to `set_property m_Name`.
  - The alias dicts are duplicated inside the valid-action lists.
- Some actions contain **real logic or composites**:
  - `write_and_compile` (`routes_editor.py:108-148`): clear_logs, then write_file for each file, then poll compilation, then read_logs(200), then filter errors.
  - `_wait_for_compilation` and `_wait_for_condition_loop`: sleep polling.
  - `_run_tests_wait` / `_poll_test_results`.
  - `create_empty`: creates the object, then applies the transform as a second call.
- Coverage: 63 of the 121 registered raw methods are reachable from manager routes.
- Schemas exist twice: the Python `oneOf` schemas are maintained separately from the C# `CreateTool` schemas, which creates a drift risk. `OpenSourceApiContractTests.cs` exists to catch drift.
- Not a generated dispatch table: every new action needs edits to both the schema and the route.
