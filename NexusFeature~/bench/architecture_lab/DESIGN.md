# Architecture lab design

The lab compares model-visible tool surfaces. Unity commands stay the execution backend. Production Nexus is not modified by a candidate.

```text
catalog.json
    |
    v
candidate manifest ----hash----> architecture_hash
    |
    v
surface generator ----fixed tool list----> Codex CLI (gpt-6-luna)
    |                                            |
    |                                            v
    +---- resolution table ---------------> lab MCP (stdio)
                                                 |
                                                 v
                                          Unity CLI beta.12
                                                 |
                                                 v
                                          Editor :7811  /tmp/nexus-arch-lab
                                                 |
                                                 v
                                          oracle reads Unity state
                                                 |
                                                 v
                                          SQLite + raw Codex events
```

Search, validation, and holdout are different runners. The search loader cannot open the other two splits.

## Catalog

`lab/catalog/extract.py` reads Pipeline `CliCommand` attributes and the three Nexus command descriptors. The checked-in snapshot is `catalog/snapshots/pipeline-0.8.0-exp.1.json` with `schema_status: parsed_from_source`. `load_catalog` recomputes the hash and refuses a mismatch.

Each capability has an id, backend name, domain suggestion, description, parameters, tags, risk, and version. Nexus entries add exposure and backend-mode fields when a candidate is built. Some C# defaults in the snapshot are raw tokens (for example `open_scene.additive` stored as `"false)"`). The surface generator does not copy those defaults into JSON schema. Reference calls pass explicit arguments.

`class_map` says which capability ids satisfy a task class (`read`, `write`, `diagnose`, `visual`, `project`, `rare`, `out_of_default`, `asset_read`). Coverage uses the classes of the tasks being scored, not all 165 commands. A class is covered when at least one of its ids is reachable. `DISABLED` and `HIDDEN_INTERNAL` are not reachable. `LAZY` is reachable only when the discovery mechanism can name it.

## Candidate

A manifest lists every catalog id. Omitted ids normalize to `DISABLED`. Canonical JSON (sorted keys, no insignificant whitespace) is hashed. `candidate_id`, parent, generation, and notes are outside the hash. `catalog_hash` is stored beside it.

Backend modes:

| Mode | Meaning in v1 |
| --- | --- |
| `PASSTHROUGH` | The official command, unchanged. |
| `ENHANCED` | The same command plus a named normalizer (`raw`, `truncate_4k`, `drop_base64`, `scalar_value`). Not a new algorithm. |
| `COMPOSITE` | A fixed step list from the catalog. Only `lab_compile_status` has steps. Mutations cannot invent steps. |

Exposure: `ALWAYS_VISIBLE`, `LAZY`, `HIDDEN_INTERNAL`, `DISABLED`.

Grouping is data on the manifest (`group`, `wrapper`), not a fixed domain list. Small managers hold at most four capabilities and use `oneOf`. The action constant is the wrapper name when one exists, so the official id is not leaked. Flat parameter unions are rejected.

## Surface generator

Strategies: `atomic`, `small_managers`, `hybrid`, `hidden_gateway`, `unity_tags`. Discovery: `none`, `describe_invoke`, `unity_tags`, `domain_discovery`. Unsupported client mechanisms raise `UnsupportedMechanism`.

The returned document has `tools` (what Codex lists), `resolution` (exposed name to backend command), and `lazy` (schemas that are not listed up front). `substituted` is false. A hidden-gateway name such as `nexus_set_field` resolves to `set_serialized_field`. Calling the official name is an error on that surface.

## Client adapter

`CodexCliAdapter` runs `codex exec` with `--ignore-user-config`, `--json`, `--ephemeral`, `--sandbox read-only`, model `gpt-6-luna`, and one MCP server: `bin/lab-mcp`. The workspace is an empty directory, not the package repo. Shell, local-shell, function, and web-search items mark the run `surface_bypass`. Those runs stay in the denominator.

The MCP server advertises the fixed tool list. It does not claim `tools.listChanged`. Discovery tools answer from the surface. Other tools call `UnityCliBackend`, which shells the official CLI and checks the response project path and port.

## Unity backend

One backend implementation serves every candidate. The lab does not reimplement Unity commands. Telemetry records the backend command that actually ran. The scratch editor is batchmode on port 7811. Reset creates `Assets/LabReset/Scratch.unity`, replaces `Assets/LabFixture` from the pristine snapshot, then opens `Lab.unity`. `YardOpen` from the copied discovery harness opens a different scene on every domain reload. The lab parks that script before a run. The `LabReset` directory stays on disk. Deleting it while the AssetDatabase still has it imported makes the next `create_scene` fail with "Parent directory must exist". Disk hash and live fields (`LabReadTarget`, intensity 1.25, revision 7, marker `readable`, play mode stopped, editor ready) must both match. A mismatch voids the next run.

`open_scene` on this Pipeline reloads the scene file and drops unsaved component edits. Write oracles read the already-open scene. They do not reopen it first. `set_serialized_field` receives `value` as text the CLI encodes. A string is sent bare (`holdout-ok`). A number is sent as a JSON number (`4.25`). Pre-quoting the string stores the quote characters. `console_status` reports `compilationFailed` under `groundTruth`, not on the root object. A missing flag stays unknown. It is not guessed from the error count. That flag is often false on a healthy editor, so the rare-console task can be guessed; it still stays in the set.

## Tasks, oracles, splits

Each task file has id, split, category, setup, prompt, environment, capability class, oracle, cleanup, and timeout. Read prompts must not contain the live answer. Write prompts contain the value to set. The oracle reads the live Unity scene afterward and does not reopen it.

| Oracle | Decision |
| --- | --- |
| `model_json_equals_unity` | A JSON field equals the lab's own Unity read. |
| `unity_field_equals` | Unity state equals `expected`. The model's "done" is ignored. |

`false_success` is a claim of done without oracle success. There is no LLM judge in v1.

Splits:

| Split | Who may read it |
| --- | --- |
| `search` | Mutation fitness. |
| `validation` | Promotion only, through the confirmation flag. |
| `holdout` | Frozen confirmation only. |

Invalid tasks are dropped before scoring. They are not given a softer oracle.

## Telemetry and cost

SQLite tables: `experiments`, `candidates`, `runs`, `tool_calls`, `events`. Reports are rendered from rows. Usage prefers Codex fields `input_tokens`, `cached_input_tokens` / `cache_read_input_tokens`, `uncached_input_tokens`, `output_tokens`, `reasoning_output_tokens`, `total_tokens`. A missing cache split does not become a cached zero. `MEASURED` requires uncached and output counts.

Per-success ratios are null when the numerator is missing or the success count is zero. Infeasible candidates are removed from the Pareto set. They are not given a penalty that a low token count can offset. Category floors block a candidate that only solves reads.

Pareto minimizes uncached tokens per success, total tokens per success, wrong-tool calls, tool calls per success, and wall time per success. A row with a null key does not dominate. `selection_order` is only for choosing parents.

Co-occurrence counts capabilities on successful non-smoke runs. It does not edit production grouping.

## Search and promotion

`plan_generation` mutates a population, drops invalid and duplicate hashes, and returns a plan with `executed: false`. Mutations change exposure, grouping, discovery, normalizer, and core size. They do not retarget `backend_name` or rewrite Unity code.

Promotion states: `GENERATED`, `PREFLIGHT_FAILED`, `SCREENING`, `SCREENING_REJECTED`, `SCREENING_PASSED`, `VALIDATING`, `VALIDATION_REJECTED`, `FINALIST`, `HOLDOUT_READY`, `CONFIRMED`.

`SCREENING_PASSED` requires `feasible`. `FINALIST` requires source `validation`. `CONFIRMED` requires source `holdout` from `HOLDOUT_READY`. Freeze stores `frozen_hash`. A change after freeze is a new candidate.

`evolve` requires `--confirm-search` and explicit run, generation, and uncached-token budgets. The hard run cap is 20000. This build returns the budget and does not call a model.

## Confirmation

A later confirmation run must use a frozen hash, a fresh editor, new seeds, the full valid task list, the holdout split, real Codex, and real Unity. Mocked tool results are not confirmation. If the hash, catalog, fixture, Unity version, pipeline, CLI, Codex, model, or reasoning changes, the experiment id changes. A mismatch with `frozen_hash` refuses the run.

Smoke output is labelled `SMOKE_ONLY` and is not that confirmation.
