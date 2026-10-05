# Nexus Architecture Evolution Lab

This directory is a bench. It generates, runs, measures, and compares agent-facing tool surfaces. It does not change the production Nexus tools, and it does not decide which surface is better.

A candidate is eligible only when it does the Unity work. Token cost is compared after that. Search metrics are not a public claim. Validation checks generalization. Holdout confirms a frozen finalist. Only a later confirmation run may support an architecture claim.

## What a run is

```text
candidate manifest
    -> surface generator
    -> Codex (GPT-6-Luna) sees only that tool list
    -> lab MCP server
    -> official Unity CLI
    -> deterministic oracle reads Unity
    -> SQLite + raw event JSON
```

The physical command set and the model-visible list are different objects. Two candidates can call the same Unity command under different names.

Codex 0.160 does not update its tool list mid-run, and deferred schema retrieval is off. `dynamic_tool_list` and `deferred_retrieval` raise. Lazy exposure in this version is `describe_invoke`, `unity_tags`, or `domain_discovery`. The surface records `mechanism_actually_used`. Nothing is silently swapped. A `tool_search` event is a mechanism mismatch and taints the experiment. A shell, image, custom tool, or foreign MCP server is a surface bypass. The runner does not paste tool names into the prompt.

## Layout

| Path | Role |
| --- | --- |
| `catalog/snapshots/` | Generated capability catalog. Do not hand-copy schemas. |
| `lab/candidates/` | Manifests, baselines, canonical hash. |
| `lab/surfaces/` | Model-visible tool list. |
| `lab/adapters/` | Unity CLI, Codex CLI, MCP server. |
| `lab/fixtures/` | Tasks, oracles, reset, reference self-check. |
| `lab/runners/` | Preflight, scratch project, smoke matrix. |
| `lab/search/` | Mutation, Pareto, promotion. No model calls. |
| `lab/validation/` | Frozen confirmation reader. Search cannot import it. |
| `tasks/search/` | Mutation fitness tasks. |
| `tasks/validation/` | Generalization tasks. Hidden from the search loader. |
| `tasks/holdout/` | Final confirmation only. |
| `pricing/luna6.json` | Prices are null until a profile is filled in. Null is not zero. |
| `evidence/` | Self-check and smoke output. |

`bin/architecture-lab` is the CLI. Run it from anywhere; it sets `PYTHONPATH` itself.

## Create a candidate

Build a manifest with `exposure` (`ALWAYS_VISIBLE`, `LAZY`, `HIDDEN_INTERNAL`, `DISABLED`) and `backend_mode` (`PASSTHROUGH`, `ENHANCED`, `COMPOSITE`). `normalize_candidate` canonicalizes order and hashes the architecture. The same architecture produces the same hash. Identity fields (`candidate_id`, notes, generation) are not part of the hash.

Reference manifests, none of which is assumed to win:

- `BASELINE_UNITY_FLAT`
- `BASELINE_UNITY_TAGS`
- `BASELINE_CURATED`
- `BASELINE_CURRENT_NEXUS_PLUS_UNITY`
- `BASELINE_HIDDEN_GATEWAY`

`SMOKE_OFFICIAL_SUBSET` and `SMOKE_HIDDEN_GATEWAY` are plumbing fixtures. They are not the flat baseline, and they must not be confirmed.

```bash
bin/architecture-lab baselines
bin/architecture-lab surfaces
```

## Preflight

Every model run checks the project path, port, pipeline version, CLI version, Codex version, Unity version, fixture hash, model id, editor readiness, and that the agent workspace is not this package. Port 7800 and PID 4465 are refused. A failure is `void`: the model is not started and the run is not scored.

The scratch project is `/tmp/nexus-arch-lab` on port **7811**. The lab never writes the harness project.

```bash
bin/architecture-lab prepare-project
bin/architecture-lab editor-build
bin/architecture-lab editor-start
bin/architecture-lab editor-stop
```

`editor-stop` signals only the PID recorded for this scratch editor, and only when `ps -p` shows `Unity.app` and `nexus-arch-lab`.

## Fixture self-check

Self-check opens the lab scene through the official CLI, solves each task on that path, runs the oracle, resets the files, and checks the live fields. A task that fails is `INVALID` and is not scored. The visual task has no deterministic pixels in v1, so it stays invalid and `scoreable: false`. The search loader omits it. That is not a reason to weaken the oracle. A later result does not measure visual skill.

```bash
bin/architecture-lab self-check
```

Results: `evidence/self-check.json`. On commit `1d8dbd0` the scratch editor reported 15 `VALID` tasks and 1 `INVALID` task, `search.visual_game_view`.

## Smoke

Smoke is four real runs: two surfaces, two self-checked tasks, one seed. It is labelled `SMOKE_ONLY`. It does not rank candidates. New smoke writes `evidence/smoke-final/`. The earlier record in `evidence/smoke/` is left in place.

```bash
bin/architecture-lab smoke --seed 1
```

The editor must already be ready, and both smoke tasks must be `VALID`. Commit `1d8dbd0` ran this command into `evidence/smoke-final/`. Acceptance passed. The label is `SMOKE_ONLY`.

## Closed loop

`evolve --confirm-search` is the model loop. It loads only the requested search tasks, scores them, keeps the feasible Pareto parents, mutates those parents, and evaluates the children. `confirm --confirm-final` loads one frozen hash. `--integration` uses `tasks/confirm_validation` and `tasks/confirm_holdout` and stays at `SCREENING`.

These commands were run on commit `1d8dbd0` against the scratch editor. They are integration evidence, not an architecture result:

```bash
bin/architecture-lab evolve \
  --model gpt-6-luna \
  --reasoning low \
  --candidates smoke \
  --tasks smoke \
  --population 2 \
  --generations 2 \
  --max-runs 16 \
  --max-uncached-tokens 400000 \
  --seed 17 \
  --label EVOLUTION_INTEGRATION_ONLY \
  --experiment evolution-integration-s17 \
  --confirm-search
```

```bash
bin/architecture-lab confirm \
  --candidate 585a8eea710edb1c5bdba2496354d2b7fbed1a2709c393bb29200981dd672c65 \
  --integration \
  --include-holdout \
  --confirm-final \
  --seed 101 \
  --max-runs 4 \
  --max-uncached-tokens 200000 \
  --model gpt-6-luna \
  --reasoning low
```

Evidence is `evidence/evolution/evolution-integration-s17/` and `evidence/confirm-integration/585a8eea710e/`. The research splits `tasks/validation` and `tasks/holdout` were not used. The large search command below was not executed.

Per-run `run_identity` is the identity snapshot. The experiment row is replaced when the next generation starts, so it names the latest generation. `study_state.seed` is stored as 0 after a stop; resume uses the seed on the command. The integration confirm tasks repeat the smoke read and write prompts under new ids. They do not open the research holdout. `search.visual_game_view` stays unscored. Writes share `LabMarker`.

## Later screening

The pieces are:

1. Screening on the search split only (`lab/search/engine.py` refuses holdout and validation).
2. Validation for candidates that passed screening.
3. Freeze a finalist. Any later edit is a new id.
4. Confirmation on a fresh editor, new seeds, and the holdout split.

```bash
bin/architecture-lab mutate-dry --seed 17
bin/architecture-lab promote-dry
bin/architecture-lab degeneracy
```

`degeneracy` shows that zero tools and zero successes are `INFEASIBLE`, including when the token count is zero.

## Validation and holdout

`load_search_tasks` is the only loader the mutation engine may call. Asking it for `validation` or `holdout` raises `HoldoutLeak`. `lab/validation/confirm.py` is the confirmation reader. `lab/search/engine.py` does not import it. Validation and holdout tasks are different problems from the search tasks: another root object, another note GUID, holdout note text, and the editor version. A search success does not contain those answers.

A search-set result cannot become `FINALIST` or `CONFIRMED`. Freeze requires `FINALIST`. Mutating a frozen manifest returns a new id and `GENERATED`.

## Results

SQLite (`evidence/smoke/runs.sqlite`) is the raw record: experiments, candidates, runs, tool calls, events. Markdown is rendered from those rows. Client usage is `MEASURED`. A missing token stays null. A character-count estimate is `ESTIMATED`. Money uses `pricing/luna6.json`. While those prices are null, cost is `UNPRICED`, not zero.

## Stop and resume

- `editor-stop` stops the scratch editor.
- `evolve` without `--confirm-search`, `--max-runs`, `--generations`, and `--max-uncached-tokens` exits 2 and prints a dry plan.
- `--max-runs` above 20000 is refused.
- With those flags, `evolve` loads a search-split population, runs it, keeps feasible Pareto parents, mutates, and repeats until a stop reason is stored. Stop reasons include `STOP_MAX_RUNS`, `STOP_TOKEN_BUDGET`, `STOP_TOKEN_UNKNOWN`, `STOP_GENERATION_LIMIT`, `STOP_NO_NEW_CANDIDATES`, and `STOP_NO_FEASIBLE_PARENTS`.
- A duplicate architecture hash is stored as `DUPLICATE_CANDIDATE_SKIPPED` and is not sent to the model.
- `--resume` continues an experiment whose stop was a run or token budget. Completed model runs are not started again. A terminal stop is not resumed.
- `confirm --confirm-final` loads one architecture hash, runs validation, and runs holdout only when every validation row is feasible and another model run still fits the budget. A stopped validation batch does not open the holdout files. `--integration` uses `tasks/confirm_validation` and `tasks/confirm_holdout` and does not open the research holdout or promote a candidate to `CONFIRMED`.
- Wall time and uncached tokens are recorded. They are not parent-selection keys.

Unit tests cover the loop with a fake model. The Luna runs named above are the integration evidence for this commit. They do not rank a candidate.

## Commands

Defaults for `evolve` are `--candidates baselines`, `--tasks search`, and `--label SEARCH`. The smoke pair is `--candidates smoke --tasks smoke`.

```bash
bin/architecture-lab evolve \
  --model gpt-6-luna \
  --reasoning low \
  --candidates baselines \
  --tasks search \
  --population 8 \
  --generations 4 \
  --max-runs 200 \
  --max-uncached-tokens 2000000 \
  --seed 17 \
  --label SEARCH \
  --confirm-search
```

```bash
bin/architecture-lab confirm \
  --candidate <frozen-architecture-hash> \
  --include-holdout \
  --confirm-final
```

`confirm` defaults are `--seed 101`, `--max-runs 40`, and `--max-uncached-tokens 2000000`. Search and confirmation stay separate commands. Resume an interrupted search with the same `--experiment` and `--resume`.
