# B — Catalog-driven adapter: can Nexus generate its S6 surface from Unity's catalog?

Agent B, v2.5 corrective pass, 2026-10-03. This is research only; no production code was written.

- **Prototype:** `NexusFeature~/bench/facade_mock/catalog/`
  - `gen.py` generates the surfaces and the metrics.
  - `churn.py` computes the version diff.
  - `tags_0.7.0-exp.1.json` holds the official tags.
  - Outputs: `catalog_groups.json`, `domain.json`, `domain_flat.json`, `curated.json`, `router.json`, `metrics.json`, `churn_0.3.1_to_0.7.0.json`.
- **Rebuild:** `python3 gen.py && python3 churn.py <path to menstood/unity-pipeline-commands-skill/references>`.
- **Token method:** tiktoken `cl100k_base`, used as a **proxy** for Claude's tokenizer. Counts are taken on the `tools` array, serialized with `json.dumps` default separators, which matches R_repo. Every number is MEASURED unless it is labelled otherwise.

## 0. A finding that changes the classification question

- **REPO:** every one of the 151 Unity commands carries an official hierarchical `Tags` value on its `[CliCommand]`, for example `baking/lighting`, `gameobjects/components` or `settings/player`. The tags are in `Library/PackageCache/com.unity.pipeline@ab05e9cd7f74/**`.
  - `/api/commands?detail=compact` serves `name`, `description`, `tags` and `package`. It also supports `?tag=` filtering and `group_by=tag`. Sources: `Documentation~/connectivity.md:277-300` and `BasePipelineServer.cs:1008-1050`.
  - The `unity list` capture we hold flattens all of this to `"group": "built-in"`, and `unity mcp` drops it entirely.
- **What this means:** domain classification is mostly given to us by Unity. The only Nexus-owned input is a mapping from tag prefix to manager. The three untagged commands are `nexus_*`, which are our own.
- **Caveat (UNKNOWN):** Unity was not running, so tag availability on the live endpoint was taken from source and docs, not from a call.

## 1. Classification

I measured two methods.

| Method | Hand-written input | Auto-classified | Unclassified | Notes |
|---|---|---|---|---|
| A. Name heuristics (works on the `unity list` capture) | 13 ordered regex rules + **2** exception entries (`get/set_import_settings` → asset) | **151/154 (98.1%)** by rules alone; 154/154 with the table | 0 | The rules were written while looking at 0.7, so this is an optimistic fit; see §4. |
| B. Official tags | 20 tag-prefix → manager entries + 3 `nexus_*` | 151/154 | 3 (`nexus_*`) | Disagrees with A on 4 commands, and both answers are defensible: `add/remove_scene_to_build` (scene vs build) and `get/set_serialized_field(s)` (code vs gameobject). |

- **Ambiguous commands (fit 2 or more domains): 13.**
  - `*_scene_*_build` (2)
  - `get/set_build_settings` (build vs settings)
  - `get/set_lighting_settings` and `get/set_navmesh_settings` (bake vs settings)
  - `get/set_serialized_field(s)`
  - `nexus_group_compile_errors` and `report_evals` (code vs diagnostics)
  - `package_search`: the "search" rule hits it, which is a pure name-collision false positive.
- **Domain choice: 13 managers**, actions per manager in brackets:
  - gameobject 16, scene 7, asset 16, prefab 7, animation 14, bake 17, build 10, settings 18, package 6, code 11, test 4, editor 15, diagnostics 13.
- **Why this grouping:** it follows Unity's own top-level tags almost 1:1, with a few merges:
  - materials/shaders → asset
  - navigation/authoring/wait → editor
  - capture/observability → diagnostics
  - batch → code

  Prefab stays separate from asset to keep each manager under 20 actions. Settings stays one manager because the 18 commands are 9 get/set pairs with one shared shape.
- **Action naming:** each action name is the Unity command name, with no renaming and no aliases.
  - Nexus history shows agents send raw method names as actions anyway (R_repo §4, 1.2.0 aliases).
  - Identity naming removes that error class and makes version churn mechanical.

## 2. Surfaces: schema tokens (cl100k proxy)

Baseline: Unity's own `unity mcp` exposes 154 atomic tools in **24,634** tokens. The median tool is 127 tokens and the largest is 1,082.

| Surface | Tools | Total | Largest manager | Lossless? |
|---|---|---|---|---|
| Domain, `oneOf` per action, full descriptions (`domain.json`) | 13 | **25,659 (+4.2%)** | asset 3,241 | yes |
| Domain, `oneOf`, first-sentence descriptions (≤80 chars) | 13 | 23,953 (−2.8%) | asset 3,030 | descriptions trimmed |
| Domain, flat param union, `if/then` required | 13 | 16,064 (−35%) | code 2,271 | **no** |
| Domain, flat, required lists in the description (`domain_flat.json`) | 13 | 14,233 (−42%) | code 2,054 | **no** |
| Domain, flat, required in description + short descriptions | 13 | 12,481 (−49%) | editor 1,816 | **no** |
| Curated (`curated.json`): 3 typed managers + `unity_describe` + `unity_invoke` | 5 | **6,945** | project 2,913 | 42 typed commands (27%); the other 112 via describe/invoke |
| Router (`router.json`): `unity_list` / `unity_describe` / `unity_run` | 3 | **213** | — | all schema content moves to call results |

- **`oneOf` saves nothing.**
  - Each manager costs about the same as the sum of its atomic tools, for example asset 3,241 vs 3,161.
  - Across all 13 managers it is +4%, which confirms R_repo's "~4%" result at full catalog scale.
  - Representation compression is not a lever.
- **The flat union's savings are mostly information loss.**
  - Of the 63 parameter names shared within a manager, 49 have different per-action descriptions, and the union drops them (replaced with "See action.").
  - The 9 typed nested `settings` schemas collapse into one untyped `settings` object.
  - Only the dedup of `target`, `confirm` and `dry_run` is a genuine saving.
- **Per-manager size, which matters for deferred clients:** with `oneOf`, loading one manager costs 454–3,241 tokens. Loading one atomic tool through ToolSearch costs a median of 127. Deferred clients therefore pay more per first touch with managers than with atomic tools. Managers only win on clients that do not defer, such as Gemini CLI.
- **Size of describe/list results (the curated/router path):**

| Result | Tokens |
|---|---|
| `describe(command)` median | 109 |
| `describe(command)` max (`wait_for`) | 1,064 |
| Compact index of one domain, max | 835 |
| Full compact index (name + description + tags) | 7,170 |
| Names only | 449 |

  Router cost per task ≈ 449 + k×109 + 213, where k is the number of distinct commands used. This is the cheapest surface, but every first use costs a discovery round-trip.
- **HYPOTHESIS:** the curated subset of 42 commands is my own pick of common production needs. No usage data backs it.

## 3. Is Unity's metadata rich enough to generate from? Mostly, for inputs; no, for outputs

- **Inputs are good:**
  - 359 parameters across 154 commands.
  - 103 are marked required, and 178 carry defaults.
  - Every parameter has a type and a description, and 42 commands take no parameters.
  - The `unity list` type is **richer than the MCP schema** for JSON-shaped parameters.
- **Parameters that don't map cleanly:**

| Count | Unity type | Problem | Examples |
|---|---|---|---|
| 46 | `objectref` | Exposed as a plain string handle: an untyped union of path / guid / globalId / instanceId / hierarchyPath. Only the description says so. | — |
| 14 | `jobject` (6), `jtoken` (4), `jarray` (4) | `unity mcp` declares them as `"type":"string"` (JSON-in-string). | `set_component_properties.properties`, `set_serialized_field.value` (polymorphic), `add_animator_state.layer` (int or name), `set_animation_curve.keys`, `run_script.args` |

  A generator needs a 5-line typemap override for the 14 JSON-shaped parameters: `jobject→object`, `jarray→array`, `jtoken→any`.
- **Smaller oddities:**
  - `single[][]` (3, in `create_gameobjects`) becomes array-of-arrays with no length constraints.
  - `list\`1` (`batch.operations`) is fine in MCP, which gives a nested item schema.
  - `nullable\`1` and `objectid[]` each appear once.
  - The 12 structured `*input` settings types map to typed nested objects. This is the good case.
- **Enums:**
  - 0 top-level string parameters carry a schema `enum`.
  - At least 11 carry enums only in prose, for example `blendingMode` ("Override | Additive"), `primitive`, `trackType`, `console.level` and `run_tests.mode`.
  - Only 2 `enum`s exist at all, both nested in player settings.
  - Generated schemas are therefore no stricter than Unity's. Extracting enums from prose is possible but fragile, and would be Nexus-owned.
- **No output schemas exist in the catalog.**
  - Normalisers (hierarchy, console, find compaction) and adapters must be hand-written against observed responses.
  - They are blind to output churn; §4 shows that output churn is the larger kind.

### Commands that need a command-specific adapter (beyond generic pass-through)

- **Async with status polling: 12 commands.**
  - `build`, `bake_lighting`, `bake_navmesh`, `bake_occlusion_culling`, `audit`, `run_tests`, `recompile`, `switch_build_target`, `package_add`, `package_remove`, `package_resolve`, `wait_for` (async mode).
  - 6 of the start→status pairs follow the `X_status` / `*_bake_status` naming. The other 6 need hand mapping: `bake_occlusion_culling`, `run_tests`, the 3 package commands, and `wait_for`.
  - One generic poll adapter driven by the pair table should cover all 12 (HYPOTHESIS). Alternatively it can use Pipeline's own detached `job:true` + `/api/job` (REPO, 0.5.0 CHANGELOG).
- **Base64 image results: 4 commands.** `capture_game_view`, `capture_scene_view`, `screenshot`, `nexus_capture_game_view`.
- **Confirmation gates:** 30 commands take `confirm`, and 12 are destructive.
  - The destructive ones are `delete_*`, `clear_*`, `remove_*`, `unpack_prefab`, `switch_build_target` and `package_remove`.
  - This needs a policy pass-through, not per-command code.
- **Code execution: 8 commands.** `eval`, `eval_file`, `run_script`, `batch`, `menu`, and `reload_file` ×3. These need a policy/gating decision; `batch` also nests other command names inside it.

## 4. Version churn: 0.3.1-exp.1 → 0.7.0-exp.1

**Sources:**
- Only one Pipeline version exists locally (0.7.0-exp.1 in UnityTestForNexus; no other project has it).
- The older catalog was reconstructed from github.com/menstood/unity-pipeline-commands-skill (`references/commands-*.md`, which targets **0.3.1-exp.1**), by parsing its markdown tables. Parameters are approximate.
- The package CHANGELOG (0.3→0.7, REPO) was used to cross-check.

**Input-side diff (MEASURED, approximate):**
- The 0.3.1 doc lists 151 commands; the 0.7.0 editor catalog has 151 Unity commands; 138 are common.
- **13 added:**
  - `audit`, `audit_status`, `batch`, `console_status`
  - `get/set_runtime_pipeline_settings`
  - `reload_file_editor_interpreter`, `reload_file_player_interpreter`
  - `report_evals`, `run_script`
  - `wait_for`, `wait_status`, `wait_cancel`
- **Removed from the editor surface: 2.**
  - `get_console_logs` was removed in 0.7.
  - `reload_file_override` was removed in 0.6.
- **Renamed: 2.** `hotreload_status` → `codereload_status` and `cleanup_hotreload` → `cleanup_codereload`, both in 0.6 and both runtime-only.
- The 9 other doc-only commands are RuntimeOnly or version-gated (`capture_editor_element` exists in 0.7 source but is not listed live).
- **Parameter additions:** 7 commands gained 11 optional parameters.
  - `capture_game_view` +3, `capture_scene_view` +2, `console.since_session`, `get_serialized_fields.format`, `recompile.focus`, `set_autotick.persist`, `set_import_settings.platform`.
  - No required-parameter additions were found.

**Effect on a generated façade:**
- All 13 additions appear as typed actions automatically, and the 2 removals disappear automatically.
- **Placement:**
  - With tag mapping frozen at 0.3.1, 9 of 13 land in the right manager. 4 are unmapped (`batch`, `wait_*`) and fall back to describe/invoke, so they are still reachable.
  - With name rules frozen at 0.3.1, 6 land correctly, 1 lands defensibly but differently (`report_evals` → code), and 6 fall back (`audit`×2, `batch`, `wait_*`×3).
  - So **about 2–3 hand entries per 4 minor versions**.
- **What breaks in hand-written pieces:**
  - A curated list or adapter that referenced `get_console_logs` breaks (1 to 2 places).
  - The status-pair table needs `wait_for` → `wait_status` (1 entry).
  - A hand-written schema façade, like today's bridge, would instead need about 13 new schema branches and about 11 parameter edits by hand.

**Output-side churn (REPO, CHANGELOG) is larger, and generation cannot absorb it:**
- 0.4 changed capture results: a `save_path` now gives a path-only result.
- 0.6 made a lean `/api/exec` envelope the default for **every** command, and changed the `reload_file` result contract.
- 0.7 changed `console` to add `session`, `counts`, `groundTruth`, `logType` and `seeded`, with cursor-reset semantics. `recompile` up_to_date semantics also changed.

These changes hit normalisers and adapters (the image, console and compile adapters), which is exactly the hand-written part.

## 5. Maintenance estimate (lines of hand-written code)

| Piece | Prototype (MEASURED) | Production estimate (HYPOTHESIS) |
|---|---|---|
| (a) Generator: fetch `/api/commands`, classify, build `oneOf` or flat schemas, typemap, validate, listChanged | about 180 logic lines in `gen.py` (out of 300 non-blank, the rest being metrics and data) | 300–450 |
| (b) Hand data: 13 domain descriptions + 20 tag prefixes (or 13 regexes + 2 exceptions) + 6 status pairs + 42-name curated list | about 60 lines | 60–90, growing about 3 entries per minor version |
| (c) Command-specific adapters: generic async-poll (12 commands), image (4), confirm/destructive/code-exec policy (50) | — | 160–240 |
| (d) Normalisers: hierarchy, console, find compaction (F_facade `compact.py` = 68-line prototype) | 68 | 200–350, with an output-churn risk at every version |
| **Total** | | **about 720–1,130** |

**Comparison with today's bridge:** 1,933 Python lines, of which 789 are hand-maintained schemas and about 700 are routes, plus 859 test lines (R_repo §5).
- The generated approach deletes the duplicated schema layer, which is the bridge's main drift source.
- It still needs about 40–60% of today's line count, concentrated in adapters and normalisers. Those are also the parts exposed to Unity's output churn.
- Tests are not included in either number.
- Composites such as `write_and_compile` would come on top.

## 6. Verdict for the central question (for the S6 arms)

1. **Generating S6-domain from the catalog is feasible and cheap.**
   - Placement is near-total: 151/154 commands by Unity's own tags, or 98% by rules alone, with 2–3 hand entries per version.
   - Inputs are typed well enough, given a 5-line typemap fix.
   - **It does not reduce tokens.** Lossless S6-domain costs +4% over Unity's own 154 tools. The flat variants that save 35–49% do so by discarding per-action parameter docs and nested types.
   - On deferred clients, per-domain first-touch cost goes **up**: 454–3,241 tokens vs 127 for a median atomic tool.
2. **The only real token lever is capability-surface reduction.**
   - S6-curated: 6,945 tokens schema, about 28% of baseline.
   - S6-router: 213 tokens schema, plus about 109 tokens per describe at call time.
   - Whether agents stay reliable through describe/invoke is UNTESTED here. That is the experiment the mock façade should run.
3. **Where the boundary sits:**
   - The input side is generable and churn-tolerant.
   - The output side (normalisers, async, images) is hand-written, has no catalog schema, and absorbed the larger share of 0.3→0.7 churn.
   - A Nexus-primary façade therefore owns a permanent hand-maintained output layer of about 400–600 lines. That layer is the honest cost of S6, independent of how the input schemas are produced.

## 7. `catalog_groups.json` contract (for the mock-façade agent)

```
{ "version": "catalog_groups/1.1 (pipeline 0.7.0-exp.1, generated by gen.py)",
  "action_naming": "action == unity command name (no renaming, no aliases)",
  "managers": { "<unity_domain>": { "description": str,
      "actions": { "<unity_command>": { "unity_command": str, "description": str,
          "params": [ {"name","unity_type","schema"(MCP JSON schema),"required","default","description"} ],
          "required": [str], "flags": ["async_status:<status_cmd>"|"image_base64"|"confirm_gate"|"destructive"|"code_exec"|"dry_run"],
          "tags": [str] } } } } }
```

- 13 managers cover all 154 commands, and each command belongs to exactly one manager.
- The version string bumps if the schema shape changes.
