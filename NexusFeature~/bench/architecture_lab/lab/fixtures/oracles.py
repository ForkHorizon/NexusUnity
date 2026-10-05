"""Deterministic oracles. The model's claim is recorded and then ignored."""

from __future__ import annotations

import json
import math
import re
from typing import Any

_JSON_OBJECT = re.compile(r"\{.*\}", re.S)


def extract_json_object(text: str) -> dict[str, Any] | None:
    if not text:
        return None
    match = _JSON_OBJECT.search(text)
    if not match:
        return None
    try:
        value = json.loads(match.group(0))
    except json.JSONDecodeError:
        return None
    return value if isinstance(value, dict) else None


def model_claimed_success(text: str) -> bool:
    if not text:
        return False
    lowered = text.lower()
    if "unable" in lowered or "cannot" in lowered or "failed" in lowered:
        # A JSON status can still say done inside a longer apology. Prefer the JSON.
        payload = extract_json_object(text)
        if payload and str(payload.get("status", "")).lower() == "done":
            return True
        return False
    payload = extract_json_object(text)
    if payload and str(payload.get("status", "")).lower() in {"done", "ok", "success"}:
        return True
    return "done" in lowered


def evaluate(task: dict[str, Any], model_text: str, observation: dict[str, Any]) -> dict[str, Any]:
    """observation is what the lab read from Unity, not what the model said."""
    oracle = task["oracle"]
    kind = oracle["type"]
    claimed = model_claimed_success(model_text)
    if kind == "model_json_equals_unity":
        payload = extract_json_object(model_text)
        field = oracle["field"]
        expected = observation.get(oracle["unity_field"])
        ok = payload is not None and _same(payload.get(field), expected) and expected is not None
    elif kind == "unity_field_equals":
        expected = oracle["expected"]
        ok = _same(observation.get(oracle["unity_field"]), expected)
    else:
        raise ValueError(f"unknown oracle {kind}")
    return {
        "oracle_success": bool(ok),
        "model_claimed_success": claimed,
        "false_success": bool(claimed and not ok),
        "judge": "deterministic",
    }


def _same(actual: Any, expected: Any) -> bool:
    """Exact compare, with a fixed tolerance for two real numbers.

    Booleans are not numbers here. A string never matches a number.
    """
    if isinstance(actual, bool) or isinstance(expected, bool):
        return isinstance(actual, bool) and isinstance(expected, bool) and actual is expected
    if isinstance(actual, (int, float)) and isinstance(expected, (int, float)):
        return math.isclose(float(actual), float(expected), rel_tol=0.0, abs_tol=1e-3)
    return actual == expected
