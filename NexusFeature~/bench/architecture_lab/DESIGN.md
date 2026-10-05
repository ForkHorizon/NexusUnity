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

`class_map` says which capability ids satisfy a task class (`read`, `write`, `diagnose`, `visual`, `project`, `rare`, `out_of_default`, `asset_read`). Coverage uses each scored task's `acceptable_capabilities`. A task is covered only when one of those ids is reachable. A sibling in the class map does not cover it. `DISABLED` and `HIDDEN_INTERNAL` are not reachable. `LAZY` is reachable only when the discovery mechanism can name it.

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

`CodexCliAdapter` runs `codex exec` with `--json`, `--ephemeral`, `--sandbox read-only`, model `gpt-6-luna`, and one MCP server: `bin/lab-mcp`. The workspace is an empty directory, not the package repo. The prompt does not list the candidate's tool names. Shell, local-shell, function, web-search, image, custom-tool, and any MCP server other than `architecture_lab` mark the run `surface_bypass`. `tool_search` is `mechanism_mismatch`: this CLI does not support deferred retrieval, and scoring it would treat a substitution as the requested surface. A mismatch taints the whole experiment, so the other arm cannot win because the client deferred only the large list. A bypass stays on that candidate and in the denominator. Unknown item types fail closed.

The MCP server advertises the fixed tool list. It does not claim `tools.listChanged`. Discovery tools answer from the surface. Other tools call `UnityCliBackend`, which shells the official CLI and checks the response project path and port.

## Unity backend

One backend implementation serves every candidate. The lab does not reimplement Unity commands. Telemetry records the backend command that actually ran. Codex runs with an isolated `CODEX_HOME` that contains only the lab MCP server. That keeps the user's other Unity MCP servers out of the tool list. `--ignore-user-config` on Codex 0.160 also drops `-c` MCP settings, so the lab does not use that flag. This binary ignores `features.rmcp_client`, so the lab config does not set it. Non-interactive exec rejects an MCP call that still needs approval, so the lab server alone is set to `default_tools_approval_mode = "approve"`. The shell sandbox stays read-only. The lab MCP server answers newline-delimited JSON when the client writes newline-delimited JSON. Codex Apps tools are still injected by the client and are not part of the candidate surface. The scratch editor is batchmode on port 7811. Reset creates `Assets/LabReset/Scratch.unity`, replaces `Assets/LabFixture` from the pristine snapshot, then opens `Lab.unity`. `YardOpen` from the copied discovery harness opens a different scene on every domain reload. The lab replaces that script with a stub and keeps the original beside it. Deleting the path leaves CS2001 in the compiler list, which then fails diagnose resets. The `LabReset` directory stays on disk. Deleting it while the AssetDatabase still has it imported makes the next `create_scene` fail with "Parent directory must exist". Disk hash and live fields (`LabReadTarget`, intensity 1.25, revision 7, marker `readable`, play mode stopped, editor ready) must both match. A mismatch voids the next run.

`open_scene` on this Pipeline reloads the scene file and drops unsaved component edits. Write oracles read the already-open scene. They do not reopen it first. `set_serialized_field` receives `value` as text the CLI encodes. A string is sent bare (`holdout-ok`). A number is sent as a JSON number (`4.25`). Pre-quoting the string stores the quote characters. `console_status` reports `compilationFailed` under `groundTruth`, not on the root object. A missing flag stays unknown. It is not guessed from the error count. That flag is often false on a healthy editor, so the rare-console task can be guessed; it still stays in the set.

## Tasks, oracles, splits

Each task file has id, split, category, setup, prompt, environment, capability class, oracle, cleanup, and timeout. Read prompts must not contain the live answer. Write prompts contain the value to set. The oracle reads the live Unity scene afterward and does not reopen it.

| Oracle | Decision |
| --- | --- |
| `model_json_equals_unity` | A JSON field equals the lab's own Unity read. |
| `unity_field_equals` | Unity state equals `expected`. The model's "done" is ignored. |

`false_success` is a claim of done without oracle success. There is no LLM judge in v1. Oracle success also requires an acceptable capability that reached the Unity backend. A reply that matches the state without that call is a guess. A cleanup failure clears that success and taints every candidate in the experiment, so the arm that left the fixture dirty is not the winner.

Search, validation, and holdout ask different questions. Search reads the marker object and the readme GUID. Validation names the other root object and the validation-note GUID. Holdout reads holdout-note text and the editor version string. Writes still share `LabMarker`, with different fields and values. The search loader cannot read the other splits.

Splits:

| Split | Who may read it |
| --- | --- |
| `search` | Mutation fitness. |
| `validation` | Promotion only, through the confirmation flag. |
| `holdout` | Frozen confirmation only. |

Invalid tasks are dropped before scoring. They are not given a softer oracle.

## Telemetry and cost

SQLite tables: `experiments`, `candidates`, `runs`, `tool_calls`, `events`. Reports are rendered from rows. Usage prefers Codex fields `input_tokens`, `cached_input_tokens` / `cache_read_input_tokens`, `uncached_input_tokens`, `output_tokens`, `reasoning_output_tokens`, `total_tokens`. A missing cache split does not become a cached zero. `MEASURED` requires uncached and output counts. A synthesized total is uncached plus cached plus output. Reasoning is stored separately and is not added again. A transport retry adds the two usage objects. The backend log is truncated when the MCP server starts and writes one line per MCP call, including a composite and a call that never reaches Unity.

Per-success ratios are null when the numerator is missing or the success count is zero. Infeasible candidates are removed from the Pareto set. They are not given a penalty that a low token count can offset. Category floors block a candidate that only solves reads.

Pareto minimizes total tokens per success, wrong-tool calls, and tool calls per success. Uncached tokens and wall time stay on the report and are not selection keys. Arms share one Codex home and run in list order, so a later arm has a warmer prompt cache and a lower wall time without doing less work. A row with a null key does not dominate. `selection_order` is only for choosing parents and breaks remaining ties by candidate hash. `plan_generation` accepts `known_hashes` so a later generation does not measure the same architecture again.

An acceptable capability has to be one that can observe the oracle field. `search.project_playmode` accepts `editor_status` only. `nexus_project_map` returns the project map and Unity version, not play mode, so a call to it plus the fixture's default `stopped` is not success. The backend log writes one line for every MCP call. A call that never reaches Unity has `backend_reached` false, and that line is not reused as proof that the next call ran.

Co-occurrence counts capabilities on successful non-smoke runs. It does not edit production grouping.

## Search and promotion

`plan_generation` mutates a population, drops invalid and duplicate hashes, and returns a plan with `executed: false`. Mutations change exposure, grouping, discovery, normalizer, and core size. They do not retarget `backend_name` or rewrite Unity code.

Promotion states: `GENERATED`, `PREFLIGHT_FAILED`, `SCREENING`, `SCREENING_REJECTED`, `SCREENING_PASSED`, `VALIDATING`, `VALIDATION_REJECTED`, `FINALIST`, `HOLDOUT_READY`, `CONFIRMED`.

`SCREENING_PASSED` requires `feasible`. `FINALIST` requires source `validation`. `CONFIRMED` requires source `holdout` from `HOLDOUT_READY`. Freeze stores `frozen_hash`. A change after freeze is a new candidate.

`evolve` requires `--confirm-search` and explicit run, generation, and uncached-token budgets. The hard run cap is 20000. This build returns the budget and does not call a model.

## Confirmation

A later confirmation run must use a frozen hash, a fresh editor, new seeds, the full valid task list, the holdout split, real Codex, and real Unity. Mocked tool results are not confirmation. If the hash, catalog, fixture, Unity version, pipeline, CLI, Codex, model, or reasoning changes, the experiment id changes. A mismatch with `frozen_hash` refuses the run.

Smoke output is labelled `SMOKE_ONLY` and is not that confirmation.
