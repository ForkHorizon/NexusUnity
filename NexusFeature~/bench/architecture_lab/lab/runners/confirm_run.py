"""Frozen-candidate confirmation.

This module is not imported by the search engine. Holdout tasks are loaded
only after validation passes, and the optimizer view omits holdout rows.
"""

from __future__ import annotations

from typing import Any, Callable

from lab.canonical import sha256_json
from lab.candidates.manifest import normalize_candidate
from lab.errors import LabError
from lab.search.promote import transition

PASS = "PASS"
ELIGIBLE_STATES = {"FINALIST", "HOLDOUT_READY"}


def assert_frozen(candidate: dict[str, Any], expected_hash: str) -> None:
    """Refuse anything other than the requested architecture."""
    actual = candidate.get("architecture_hash")
    if actual != expected_hash:
        raise LabError(f"refusing to confirm {actual}; requested {expected_hash}")
    if candidate.get("mutated_during_confirm"):
        raise LabError("confirmation must not mutate the candidate")


def assert_canonical(candidate: dict[str, Any], expected_hash: str, catalog: dict[str, Any]) -> None:
    """Refuse when the stored manifest would hash to a different architecture now."""
    assert_frozen(candidate, expected_hash)
    architecture = candidate.get("architecture") or {}
    if sha256_json(architecture) != expected_hash:
        raise LabError("canonical architecture content does not match the requested hash")
    raw = {
        "candidate_id": candidate.get("candidate_id"),
        "parent_hash": candidate.get("parent_hash"),
        "generation": candidate.get("generation") or 0,
        "why": candidate.get("why") or "",
        "surface_strategy": architecture.get("surface_strategy"),
        "discovery_mechanism": architecture.get("discovery_mechanism"),
        "output_normalization": architecture.get("output_normalization"),
        "domains": architecture.get("domains") or {},
        "core_always_visible": list(architecture.get("core_always_visible") or []),
        "capabilities": architecture.get("capabilities") or {},
    }
    rebuilt = normalize_candidate(raw, catalog)
    if rebuilt["architecture_hash"] != expected_hash:
        raise LabError("re-normalized architecture hash differs; refusing a changed candidate")


def promotion_for(status: str, *, integration: bool, state: str | None) -> str:
    """Integration never promotes a candidate to CONFIRMED."""
    if integration:
        return "SCREENING"
    if status == "CONFIRMATION_PASSED" and state == "HOLDOUT_READY":
        return transition(state, "CONFIRMED", source="holdout")
    return status


def assert_eligible(candidate: dict[str, Any], *, integration: bool) -> None:
    """A real confirmation requires a frozen finalist. Integration does not promote one."""
    if integration:
        return
    if candidate.get("state") not in ELIGIBLE_STATES and not candidate.get("frozen"):
        raise LabError("candidate is not an eligible frozen finalist")


def run_confirmation(
    *,
    candidate: dict[str, Any],
    expected_hash: str,
    validation_tasks: list[dict[str, Any]],
    holdout_loader: Callable[[], list[dict[str, Any]]],
    run_batch: Callable[[list[dict[str, Any]], str], dict[str, Any]],
    include_holdout: bool,
    holdout_block: Callable[[], str | None] | None = None,
) -> dict[str, Any]:
    """Run validation, then holdout only when validation is feasible.

    `holdout_loader` is called only on that path. The returned `optimizer`
    dict is the only object a later search step may see, and it has no
    holdout task rows.
    """
    assert_frozen(candidate, expected_hash)
    if not validation_tasks:
        return _result("CONFIRMATION_VOID", False, expected_hash, validation_rows=[])
    validation = run_batch(validation_tasks, "validation")
    if validation.get("stopped"):
        # A partial batch can look feasible. That is not a pass, and it must
        # not open the holdout loader.
        return _result(
            "CONFIRMATION_VOID",
            False,
            expected_hash,
            validation_rows=validation.get("rows") or [],
            reason=validation["stopped"],
        )
    status = _batch_status(validation.get("rows") or [])
    if status != PASS:
        return _result(status, False, expected_hash, validation_rows=validation.get("rows") or [])
    if not include_holdout:
        return _result("CONFIRMATION_FAILED", False, expected_hash, validation_rows=validation.get("rows") or [], reason="holdout was not requested")
    if holdout_block is not None:
        blocked = holdout_block()
        if blocked:
            return _result(
                "CONFIRMATION_VOID",
                False,
                expected_hash,
                validation_rows=validation.get("rows") or [],
                reason=blocked,
            )
    holdout_tasks = holdout_loader()
    if not holdout_tasks:
        return _result("CONFIRMATION_VOID", False, expected_hash, validation_rows=validation.get("rows") or [], reason="holdout split was empty")
    holdout = run_batch(holdout_tasks, "holdout")
    holdout_rows = holdout.get("rows") or []
    if not any(row.get("model_started_any") for row in holdout_rows):
        return _result(
            "CONFIRMATION_VOID",
            False,
            expected_hash,
            validation_rows=validation.get("rows") or [],
            holdout_rows=holdout_rows,
            reason=holdout.get("stopped") or "holdout model did not start",
        )
    final = _batch_status(holdout_rows)
    if final == PASS:
        final = "CONFIRMATION_PASSED"
    return _result(
        final,
        True,
        expected_hash,
        validation_rows=validation.get("rows") or [],
        holdout_rows=holdout.get("rows") or [],
    )


def _batch_status(rows: list[dict[str, Any]]) -> str:
    if not rows:
        return "CONFIRMATION_VOID"
    if all(not row.get("model_started_any") and row.get("feasible") is not True for row in rows):
        return "CONFIRMATION_VOID"
    if all(row.get("feasible") is True for row in rows):
        return PASS
    return "CONFIRMATION_FAILED"


def _result(status: str, holdout_executed: bool, candidate_hash: str, **extra: Any) -> dict[str, Any]:
    optimizer = {"status": status, "candidate_hash": candidate_hash}
    return {
        "status": status,
        "holdout_executed": holdout_executed,
        "optimizer": optimizer,
        **extra,
    }
