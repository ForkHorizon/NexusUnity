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
    elif label == "EVOLUTION_INTEGRATION_ONLY":
        lines.append("This is a closed-loop integration run. It is not an architecture result.")
        lines.append("These rows are not ranked.")
        lines.append("")
    elif label == "CONFIRM_PIPELINE_INTEGRATION_ONLY":
        lines.append("This exercises the confirmation pipeline. It is not an architecture result.")
        lines.append("These rows are not ranked.")
        lines.append("")
    else:
        lines.append("Rows are measurements from this run. They are not an architecture ranking.")
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


_CLAIM_LABELS = {"SMOKE_ONLY", "EVOLUTION_INTEGRATION_ONLY", "CONFIRM_PIPELINE_INTEGRATION_ONLY"}


def _reject_claims(text: str, label: str) -> None:
    if label not in _CLAIM_LABELS:
        return
    lowered = text.lower()
    for phrase in FORBIDDEN_CLAIM_PHRASES:
        if phrase in lowered:
            raise RuntimeError(f"{label} report used claim language: {phrase}")
