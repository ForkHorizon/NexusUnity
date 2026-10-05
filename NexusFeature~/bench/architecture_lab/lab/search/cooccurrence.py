"""Capability co-occurrence from successful scored runs. Does not change production."""

from __future__ import annotations

from collections import Counter
from typing import Any


def cooccurrence(runs: list[dict[str, Any]]) -> dict[str, Any]:
    """runs items need oracle_success and capability_ids (list)."""
    pair_counts: Counter[tuple[str, str]] = Counter()
    single: Counter[str] = Counter()
    used_runs = 0
    for run in runs:
        if not run.get("oracle_success"):
            continue
        if run.get("label") == "SMOKE_ONLY":
            # Smoke plumbing is not evidence for a grouping proposal.
            continue
        ids = sorted(set(run.get("capability_ids") or []))
        if not ids:
            continue
        used_runs += 1
        for item in ids:
            single[item] += 1
        for i, left in enumerate(ids):
            for right in ids[i + 1 :]:
                pair_counts[(left, right)] += 1
    pairs = [
        {"left": left, "right": right, "count": count}
        for (left, right), count in sorted(pair_counts.items())
    ]
    return {"successful_runs": used_runs, "counts": dict(single), "pairs": pairs, "applied_to_production": False}
