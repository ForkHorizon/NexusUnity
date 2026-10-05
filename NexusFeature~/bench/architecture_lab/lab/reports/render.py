"""Markdown rendered from structured rows. It does not decide a winner."""

from __future__ import annotations

from typing import Any

FORBIDDEN_CLAIM_PHRASES = (
    "is better",
    "winner",
    "fewer tokens than",
    "confirmed architecture",
)


def render_comparison(rows: list[dict[str, Any]], *, label: str) -> str:
    lines = [
        f"# Architecture lab report",
        "",
        f"Label: `{label}`",
        "",
    ]
    if label == "SMOKE_ONLY":
        lines.append("This is a plumbing smoke run. It is not an architecture result.")
        lines.append("These rows are not ranked. Search, validation, and holdout were not used.")
        lines.append("")
    lines.append("| Candidate | Hash | Success | False success | Uncached/success | Calls | Wrong tools | Feasible |")
    lines.append("| --- | --- | --- | --- | --- | --- | --- | --- |")
    for row in rows:
        lines.append(
            "| {candidate_id} | `{candidate_hash}` | {success} | {false_success} | {uncached} | {calls} | {wrong} | {feasible} |".format(
                candidate_id=row.get("candidate_id"),
                candidate_hash=str(row.get("candidate_hash") or "")[:12],
                success=_fmt(row.get("semantic_success_rate")),
                false_success=row.get("false_successes"),
                uncached=_fmt(row.get("uncached_tokens_per_success")),
                calls=_fmt(row.get("tool_calls_per_success")),
                wrong=row.get("wrong_tool_calls"),
                feasible=row.get("feasible"),
            )
        )
    lines.append("")
    lines.append("Raw token counts stay in the SQLite run database. Null costs are unpriced, not zero.")
    text = "\n".join(lines) + "\n"
    _reject_claims(text, label)
    return text


def render_lineage(children: list[dict[str, Any]]) -> str:
    lines = ["# Mutation plan", "", "This plan was not executed against a model.", ""]
    for child in children:
        lines.append(
            f"- `{child['candidate_id']}` parent `{str(child.get('parent_hash') or '')[:12]}` — {child.get('why')}"
        )
    return "\n".join(lines) + "\n"


def _fmt(value: Any) -> str:
    if value is None:
        return "null"
    if isinstance(value, float):
        return f"{value:.4g}"
    return str(value)


def _reject_claims(text: str, label: str) -> None:
    if label != "SMOKE_ONLY":
        return
    lowered = text.lower()
    for phrase in FORBIDDEN_CLAIM_PHRASES:
        if phrase in lowered:
            raise RuntimeError(f"smoke report used claim language: {phrase}")
