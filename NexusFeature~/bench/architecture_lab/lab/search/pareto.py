"""Pareto frontier over feasible candidates only."""

from __future__ import annotations

from typing import Any


# Uncached tokens stay on the report. They are not a Pareto key: a shared
# Codex prompt cache makes a later arm look cheaper without doing less work.
# Wall time is recorded too, and it is not a dominance or parent-selection
# key. Arms share one Codex home and run in list order, so a later arm is
# faster because that cache is already warm.
COST_KEYS = (
    "total_tokens_per_success",
    "wrong_tool_calls",
    "tool_calls_per_success",
)


def pareto_frontier(candidates: list[dict[str, Any]]) -> list[dict[str, Any]]:
    """Minimize every cost key. Infeasible rows are excluded, not penalized."""
    feasible = [row for row in candidates if row.get("feasible") is True]
    frontier = []
    for row in feasible:
        if any(_dominates(other, row) for other in feasible if other is not row):
            continue
        frontier.append(row)
    return sorted(frontier, key=lambda row: row.get("candidate_hash") or row.get("candidate_id") or "")


def _dominates(left: dict[str, Any], right: dict[str, Any]) -> bool:
    better = False
    for key in COST_KEYS:
        lv = left.get(key)
        rv = right.get(key)
        if lv is None or rv is None:
            return False
        if lv > rv:
            return False
        if lv < rv:
            better = True
    return better


def selection_order(frontier: list[dict[str, Any]]) -> list[dict[str, Any]]:
    """Lexicographic order used only to pick the next parent. Not a public claim."""
    return sorted(
        frontier,
        key=lambda row: (
            row.get("wrong_tool_calls") if row.get("wrong_tool_calls") is not None else 10**12,
            row.get("total_tokens_per_success") if row.get("total_tokens_per_success") is not None else 10**12,
            row.get("candidate_hash") or "",
        ),
    )
