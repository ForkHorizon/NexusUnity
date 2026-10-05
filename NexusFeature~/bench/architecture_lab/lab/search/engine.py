"""Search planning. This module does not call a model and cannot read holdout."""

from __future__ import annotations

from typing import Any

from lab.errors import LabError
from lab.search.holdout import load_search_tasks, search_metric_keys
from lab.search.mutate import mutate

# A forgotten flag must not schedule an unbounded study.
HARD_RUN_CAP = 20000


def assert_optimizer_view(metrics: dict[str, Any]) -> dict[str, Any]:
    """Drop anything outside the search metric allow-list."""
    forbidden = [key for key in metrics if "holdout" in key.lower()]
    if forbidden:
        raise LabError(f"optimizer payload named holdout fields: {forbidden}")
    return {key: metrics[key] for key in metrics if key in search_metric_keys()}


def plan_generation(
    population: list[dict[str, Any]],
    catalog: dict[str, Any],
    *,
    seed: int,
    task_root=None,
    known_hashes: set[str] | None = None,
) -> dict[str, Any]:
    """Build the next generation on paper. No model calls.

    `known_hashes` is every architecture already measured. Deduping only inside
    this population would run the same candidate again against a warm cache.
    """
    if task_root is not None:
        # Touching tasks here is limited to the search split loader.
        load_search_tasks(task_root)
    seen = set(known_hashes or ())
    seen.update(item["architecture_hash"] for item in population)
    children = []
    skipped = []
    rejected = 0
    for offset, parent in enumerate(population):
        child = mutate(parent, catalog, seed + offset + 1)
        if child is None:
            rejected += 1
            continue
        if child["architecture_hash"] in seen:
            rejected += 1
            skipped.append(
                {
                    "candidate_hash": child["architecture_hash"],
                    "parent_hash": parent["architecture_hash"],
                    "reason": "DUPLICATE_CANDIDATE_SKIPPED",
                }
            )
            continue
        seen.add(child["architecture_hash"])
        children.append(
            {
                "candidate_id": child["candidate_id"],
                "candidate_hash": child["architecture_hash"],
                "parent_hash": child["parent_hash"],
                "generation": child["generation"],
                "why": child["why"],
                "manifest": child,
            }
        )
    return {
        "seed": seed,
        "parents": len(population),
        "children": children,
        "skipped": skipped,
        "duplicates_or_rejected": rejected,
        "executed": False,
    }


def evolve_request(budget: dict[str, Any]) -> dict[str, Any]:
    """Refuse a real search unless the caller set an explicit budget and the confirm flag."""
    max_runs = budget.get("max_runs")
    max_generations = budget.get("max_generations")
    if not budget.get("confirm_search"):
        raise LabError(
            "evolve refuses to start a model search without --confirm-search. "
            "Use plan_generation for a dry run."
        )
    if not isinstance(max_runs, int) or max_runs < 1:
        raise LabError("evolve requires an explicit positive --max-runs budget")
    if max_runs > HARD_RUN_CAP:
        raise LabError(f"max_runs {max_runs} exceeds hard cap {HARD_RUN_CAP}")
    if not isinstance(max_generations, int) or max_generations < 1:
        raise LabError("evolve requires an explicit positive --generations budget")
    if budget.get("max_uncached_tokens") is None:
        raise LabError("evolve requires --max-uncached-tokens")
    return {
        "accepted": True,
        "max_runs": max_runs,
        "max_generations": max_generations,
        "max_uncached_tokens": budget["max_uncached_tokens"],
        "model": budget.get("model"),
        "executed": False,
    }
