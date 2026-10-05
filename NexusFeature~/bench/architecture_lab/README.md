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

Codex 0.160 does not update its tool list mid-run, and deferred schema retrieval is off. `dynamic_tool_list` and `deferred_retrieval` raise. Lazy exposure in this version is `describe_invoke`, `unity_tags`, or `domain_discovery`. The surface records `mechanism_actually_used`. Nothing is silently swapped.

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

Self-check opens the lab scene through the official CLI, solves each task on that path, runs the oracle, resets the files, and checks the live fields. A task that fails is `INVALID` and is not scored. The visual task has no deterministic pixels in v1, so it stays invalid. That is not a reason to weaken the oracle.

```bash
bin/architecture-lab self-check
```

Results: `evidence/self-check.json`.

## Smoke

Smoke is four real runs: two surfaces, two self-checked tasks, one seed. It is labelled `SMOKE_ONLY`. It does not rank candidates.

```bash
bin/architecture-lab smoke --seed 1
```

The editor must already be ready, and both smoke tasks must be `VALID`.

## Later screening

Not implemented as a model loop in this version. The pieces are:

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

`load_search_tasks` is the only loader the mutation engine may call. Asking it for `validation` or `holdout` raises `HoldoutLeak`. `lab/validation/confirm.py` is the confirmation reader. `lab/search/engine.py` does not import it.

A search-set result cannot become `FINALIST` or `CONFIRMED`. Freeze requires `FINALIST`. Mutating a frozen manifest returns a new id and `GENERATED`.

## Results

SQLite (`evidence/smoke/runs.sqlite`) is the raw record: experiments, candidates, runs, tool calls, events. Markdown is rendered from those rows. Client usage is `MEASURED`. A missing token stays null. A character-count estimate is `ESTIMATED`. Money uses `pricing/luna6.json`. While those prices are null, cost is `UNPRICED`, not zero.

## Stop and resume

- `editor-stop` stops the scratch editor.
- `evolve` without `--confirm-search`, `--max-runs`, `--generations`, and `--max-uncached-tokens` exits 2 and prints a dry plan.
- `--max-runs` above 20000 is refused.
- This build's `evolve` still does not start model runs after the budget is accepted. Do not treat a zero exit as a finished study.
- `confirm` always refuses in this build, including with `--confirm-final`.

## Future commands

Do not run these as a study until a later sprint connects the model loop. They are the interfaces:

```bash
bin/architecture-lab evolve \
  --model gpt-6-luna \
  --reasoning low \
  --population 8 \
  --generations 4 \
  --max-runs 200 \
  --max-uncached-tokens 2000000 \
  --seed 17 \
  --confirm-search
```

```bash
bin/architecture-lab confirm \
  --candidate <frozen-architecture-hash> \
  --include-holdout \
  --confirm-final
```

Search and confirmation stay separate commands.
