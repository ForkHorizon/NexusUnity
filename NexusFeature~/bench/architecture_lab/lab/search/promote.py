"""Promotion states. Search metrics cannot confirm a candidate."""

from __future__ import annotations

from typing import Any

from lab.errors import CandidateInvalid

STATES = {
    "GENERATED",
    "PREFLIGHT_FAILED",
    "SCREENING",
    "SCREENING_REJECTED",
    "SCREENING_PASSED",
    "VALIDATING",
    "VALIDATION_REJECTED",
    "FINALIST",
    "HOLDOUT_READY",
    "CONFIRMED",
}

_TRANSITIONS = {
    "GENERATED": {"PREFLIGHT_FAILED", "SCREENING"},
    "SCREENING": {"SCREENING_REJECTED", "SCREENING_PASSED"},
    "SCREENING_PASSED": {"VALIDATING"},
    "VALIDATING": {"VALIDATION_REJECTED", "FINALIST"},
    "FINALIST": {"HOLDOUT_READY"},
    "HOLDOUT_READY": {"CONFIRMED", "VALIDATION_REJECTED"},
}


def transition(state: str, new_state: str, *, source: str, feasible: bool | None = None) -> str:
    if state not in STATES or new_state not in STATES:
        raise CandidateInvalid(f"unknown state {state} -> {new_state}")
    if new_state not in _TRANSITIONS.get(state, set()):
        raise CandidateInvalid(f"illegal promotion {state} -> {new_state}")
    if new_state == "SCREENING_PASSED" and feasible is not True:
        raise CandidateInvalid("infeasible candidate cannot pass screening")
    if new_state == "FINALIST" and source != "validation":
        raise CandidateInvalid("finalist requires a validation result")
    if new_state == "CONFIRMED" and source != "holdout":
        raise CandidateInvalid("CONFIRMED requires a holdout result, not a search result")
    return new_state


def freeze(candidate: dict[str, Any]) -> dict[str, Any]:
    """Lock a finalist. Further edits must go through mutate_frozen."""
    if candidate.get("state") != "FINALIST":
        raise CandidateInvalid("only a FINALIST can be frozen")
    frozen = dict(candidate)
    frozen["state"] = "HOLDOUT_READY"
    frozen["frozen_hash"] = candidate["architecture_hash"]
    frozen["frozen"] = True
    return frozen


def mutate_frozen(candidate: dict[str, Any], mutated: dict[str, Any]) -> dict[str, Any]:
    """A change after freeze is a new candidate. The frozen hash stays put."""
    if not candidate.get("frozen"):
        raise CandidateInvalid("mutate_frozen requires a frozen candidate")
    if mutated["architecture_hash"] == candidate["frozen_hash"]:
        raise CandidateInvalid("frozen candidate did not change")
    child = dict(mutated)
    child["candidate_id"] = f"{candidate['candidate_id']}__mut_{mutated['architecture_hash'][:8]}"
    child["parent_hash"] = candidate["frozen_hash"]
    child["generation"] = int(candidate.get("generation") or 0) + 1
    child["state"] = "GENERATED"
    child["frozen"] = False
    child["frozen_hash"] = None
    return child
