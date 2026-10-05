"""Quality gates. Cheap failures are INFEASIBLE, not bargain winners."""

from __future__ import annotations

from typing import Any


def classify_candidate(metrics: dict[str, Any], floors: dict[str, Any]) -> dict[str, Any]:
    """Return FEASIBLE or INFEASIBLE. There is no numeric penalty that cost can offset."""
    reasons = []
    coverage = metrics.get("coverage")
    min_coverage = floors.get("min_coverage", 1.0)
    if coverage is None or coverage < min_coverage:
        reasons.append("capability_coverage")

    attempted = int(metrics.get("attempted") or 0)
    successes = int(metrics.get("oracle_successes") or 0)
    # Callers must count every scheduled task in `attempted`, including a
    # pre-start void. Dropping the void would turn a missed write into a
    # perfect read-only rate. Surface bypasses stay in this denominator too.
    rate = (successes / attempted) if attempted else 0.0
    min_success = floors.get("min_semantic_success", 1.0)
    if attempted == 0 or rate < min_success:
        reasons.append("semantic_success")

    category_floors = floors.get("category_floors") or {}
    default_floor = floors.get("default_category_floor", 0.0)
    for category, observed in sorted((metrics.get("category_rates") or {}).items()):
        floor = category_floors.get(category, default_floor)
        if observed < floor:
            reasons.append(f"category:{category}")

    if floors.get("require_measured_usage", True) and metrics.get("usage_status") != "MEASURED":
        reasons.append("usage_not_measured")

    if int(metrics.get("surface_bypasses") or 0) > 0:
        reasons.append("surface_bypass")

    status = "INFEASIBLE" if reasons else "FEASIBLE"
    return {
        "status": status,
        "reasons": reasons,
        "semantic_success_rate": rate,
        "feasible": status == "FEASIBLE",
    }


def per_success(total: float | None, successes: int) -> float | None:
    if total is None or successes <= 0:
        return None
    return total / successes
