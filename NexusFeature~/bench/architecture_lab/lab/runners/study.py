"""Closed-loop search.

The CLI calls this module. It does not own a second copy of mutation,
scoring, or the Unity runner. It also does not import validation or holdout.
"""

from __future__ import annotations

from typing import Any

from lab.errors import LabError
from lab.runners.matrix import run_smoke_matrix
from lab.search.budget import (
    STOP_GENERATION_LIMIT,
    STOP_NO_FEASIBLE_PARENTS,
    STOP_NO_NEW_CANDIDATES,
    STOP_TOKEN_UNKNOWN,
    TERMINAL_STOPS,
    next_run_allowed,
    spend_so_far,
)
from lab.search.engine import assert_optimizer_view, plan_generation
from lab.search.pareto import pareto_frontier, selection_order

EVOLUTION_LABEL = "EVOLUTION_INTEGRATION_ONLY"


def run_evolution(
    *,
    catalog: dict[str, Any],
    tasks: list[dict[str, Any]],
    population: list[dict[str, Any]],
    store,
    seed: int,
    generations: int,
    max_runs: int,
    max_uncached_tokens: int,
    experiment_id: str,
    label: str,
    population_limit: int,
    runner=run_smoke_matrix,
    runner_kwargs: dict[str, Any] | None = None,
    identity: dict[str, Any] | None = None,
    resume: bool = False,
) -> dict[str, Any]:
    """Evaluate search candidates, select feasible parents, mutate, and repeat.

    `known_hashes` is the archive of architectures already fully measured.
    A duplicate child is skipped and is not sent to the model.
    """
    for task in tasks:
        if task.get("split") != "search":
            raise LabError(f"evolve refuses non-search task {task.get('task_id')}")
    if generations < 1 or max_runs < 1 or max_uncached_tokens < 1:
        raise LabError("evolve requires positive generations, max_runs, and max_uncached_tokens")
    runner_kwargs = dict(runner_kwargs or {})
    identity = dict(identity or {})
    identity["label"] = label
    state = store.study_state(experiment_id) if resume else None
    if resume and not state:
        raise LabError(f"nothing to resume for {experiment_id}")
    if state and state.get("stop_reason") in TERMINAL_STOPS:
        return _done(
            executed=False,
            stop_reason=state["stop_reason"],
            resumable=False,
            generation=state.get("generation") or 0,
            lineage=state.get("lineage") or [],
            label=label,
            experiment_id=experiment_id,
            runs_used=_spent(store, experiment_id)[0],
            uncached_used=_spent(store, experiment_id)[1] or 0,
            population=state.get("pending_manifests") or [],
            skipped=state.get("skipped") or [],
        )
    existing = store.started_run_ids(experiment_id)
    if existing and not resume:
        raise LabError(f"{experiment_id} already has {len(existing)} started runs; pass resume to continue")
    if state:
        population = list(state.get("pending_manifests") or [])
        generation = int(state.get("generation") or 0)
        lineage = list(state.get("lineage") or [])
        skipped = list(state.get("skipped") or [])
    else:
        generation = 0
        lineage = []
        skipped = []
        population = _dedupe(list(population), skipped)[:population_limit]
    task_ids = [task["task_id"] for task in tasks]
    while True:
        population = _dedupe(population, skipped)
        if not population:
            return _finish(store, experiment_id, STOP_NO_NEW_CANDIDATES, False, generation, lineage, label, population, skipped, tasks)
        if generation >= generations:
            return _finish(store, experiment_id, STOP_GENERATION_LIMIT, False, generation, lineage, label, population, skipped, tasks)
        store.save_study_state(
            experiment_id,
            _state(generation, "evaluating", population, lineage, skipped, None, True, seed, population_limit, label),
        )
        runs_used, uncached_used = _spent(store, experiment_id)
        if uncached_used is None:
            return _finish(store, experiment_id, STOP_TOKEN_UNKNOWN, True, generation, lineage, label, population, skipped, tasks)
        stop = next_run_allowed(
            runs_used=runs_used,
            max_runs=max_runs,
            uncached_used=uncached_used,
            max_uncached_tokens=max_uncached_tokens,
        )
        if stop:
            return _finish(store, experiment_id, stop, True, generation, lineage, label, population, skipped, tasks)
        batch = runner(
            catalog=catalog,
            tasks=tasks,
            candidates=population,
            store=store,
            seed=seed,
            experiment_id=experiment_id,
            identity=identity,
            label=label,
            budget={"max_runs": max_runs, "max_uncached_tokens": max_uncached_tokens},
            skip_ids=store.started_run_ids(experiment_id),
            generation=generation,
            order_seed=seed,
            runs_used=runs_used,
            uncached_used=uncached_used,
            **runner_kwargs,
        )
        entry = {
            "generation": generation,
            "candidate_hashes": [item["architecture_hash"] for item in population],
            "rows": [_safe_row(row) for row in batch.get("rows") or []],
            "execution_order": batch.get("execution_order") or [],
            "stopped": batch.get("stopped"),
        }
        if lineage and int(lineage[-1].get("generation") or 0) == generation:
            # Resume recomputes this generation from stored runs. Keep one entry.
            lineage.pop()
        lineage.append(entry)
        if batch.get("stopped"):
            resumable = batch["stopped"] not in TERMINAL_STOPS
            return _finish(
                store,
                experiment_id,
                batch["stopped"],
                resumable,
                generation,
                lineage,
                label,
                population,
                skipped,
                tasks,
            )
        if generation + 1 >= generations:
            return _finish(store, experiment_id, STOP_GENERATION_LIMIT, False, generation, lineage, label, population, skipped, tasks)
        known = store.completed_architecture_hashes(experiment_id, task_ids, seed)
        parents = _parents(batch.get("rows") or [], population, population_limit)
        if not parents:
            return _finish(store, experiment_id, STOP_NO_FEASIBLE_PARENTS, False, generation, lineage, label, population, skipped, tasks)
        plan = plan_generation(parents, catalog, seed=seed + generation + 1, known_hashes=known)
        skipped.extend(plan.get("skipped") or [])
        children = [item["manifest"] for item in plan["children"]][:population_limit]
        entry["parent_hashes"] = [item["architecture_hash"] for item in parents]
        entry["children"] = [
            {
                "candidate_hash": item["architecture_hash"],
                "parent_hash": item.get("parent_hash"),
                "candidate_id": item.get("candidate_id"),
                "generation": item.get("generation"),
            }
            for item in children
        ]
        entry["duplicates"] = plan.get("skipped") or []
        if not children:
            return _finish(store, experiment_id, STOP_NO_NEW_CANDIDATES, False, generation, lineage, label, population, skipped, tasks)
        generation += 1
        population = children
        store.save_study_state(
            experiment_id,
            _state(generation, "ready_generation", population, lineage, skipped, None, True, seed, population_limit, label),
        )


def _parents(rows: list[dict[str, Any]], population: list[dict[str, Any]], limit: int) -> list[dict[str, Any]]:
    """Feasible Pareto frontier only. Wall time and uncached tokens are not keys."""
    by_hash = {item["architecture_hash"]: item for item in population}
    chosen = []
    for row in selection_order(pareto_frontier(rows)):
        if row.get("feasible") is not True:
            continue
        manifest = by_hash.get(row.get("candidate_hash"))
        if manifest is None:
            continue
        chosen.append(manifest)
        if len(chosen) >= limit:
            break
    return chosen


def _safe_row(row: dict[str, Any]) -> dict[str, Any]:
    allowed = {key: row.get(key) for key in row if key in {
        "candidate_hash",
        "oracle_successes",
        "attempted",
        "false_successes",
        "coverage",
        "uncached_tokens_per_success",
        "total_tokens_per_success",
        "wrong_tool_calls",
        "tool_calls_per_success",
        "wall_ms_per_success",
        "category_rates",
        "usage_status",
        "feasible",
        "semantic_success_rate",
    }}
    safe = assert_optimizer_view(allowed)
    safe["candidate_id"] = row.get("candidate_id")
    return safe


def _spent(store, experiment_id: str) -> tuple[int, int | None]:
    return spend_so_far(store, experiment_id)


def _dedupe(population: list[dict[str, Any]], skipped: list[dict[str, Any]]) -> list[dict[str, Any]]:
    """Drop a repeated architecture before it can spend another model run."""
    kept = []
    seen: set[str] = set()
    ids: set[str] = set()
    for candidate in population:
        digest = candidate["architecture_hash"]
        if digest in seen:
            skipped.append(
                {
                    "candidate_hash": digest,
                    "parent_hash": candidate.get("parent_hash"),
                    "reason": "DUPLICATE_CANDIDATE_SKIPPED",
                }
            )
            continue
        candidate_id = candidate["candidate_id"]
        if candidate_id in ids:
            raise LabError(f"candidate_id {candidate_id} is used by two architectures")
        seen.add(digest)
        ids.add(candidate_id)
        kept.append(candidate)
    return kept


def _state(generation, phase, population, lineage, skipped, stop_reason, resumable, seed, population_limit, label) -> dict[str, Any]:
    return {
        "generation": generation,
        "phase": phase,
        "pending_manifests": population,
        "known_hashes": [item["architecture_hash"] for item in population],
        "lineage": lineage,
        "skipped": skipped,
        "stop_reason": stop_reason,
        "resumable": resumable,
        "seed": seed,
        "population_limit": population_limit,
        "label": label,
    }


def _finish(store, experiment_id, stop_reason, resumable, generation, lineage, label, population, skipped, tasks) -> dict[str, Any]:
    runs_used, uncached_used = _spent(store, experiment_id)
    if uncached_used is None and stop_reason not in TERMINAL_STOPS:
        stop_reason = STOP_TOKEN_UNKNOWN
        resumable = True
    store.save_study_state(
        experiment_id,
        _state(
            generation,
            "stopped",
            population,
            lineage,
            skipped,
            stop_reason,
            resumable,
            0,
            len(population),
            label,
        ),
    )
    return _done(
        executed=True,
        stop_reason=stop_reason,
        resumable=resumable,
        generation=generation,
        lineage=lineage,
        label=label,
        experiment_id=experiment_id,
        runs_used=runs_used,
        uncached_used=0 if uncached_used is None else uncached_used,
        population=population,
        skipped=skipped,
    )


def _done(**payload: Any) -> dict[str, Any]:
    payload["search_tasks_only"] = True
    return payload
