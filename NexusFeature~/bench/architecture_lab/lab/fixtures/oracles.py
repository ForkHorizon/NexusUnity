"""Deterministic oracles. The model's claim is recorded and then ignored."""

from __future__ import annotations

import json
import math
from typing import Any

def extract_json_object(text: str) -> dict[str, Any] | None:
    """Return the first JSON object in text.

    A greedy brace match joins two objects into one invalid span and rejects a
    correct answer. Strings are skipped so a brace inside a value is not a closer.
    """
    if not text:
        return None
    start = 0
    while True:
        index = text.find("{", start)
        if index < 0:
            return None
        blob = _balanced_object(text, index)
        if blob is None:
            return None
        try:
            value = json.loads(blob)
        except json.JSONDecodeError:
            start = index + 1
            continue
        if isinstance(value, dict):
            return value
        start = index + len(blob)


def _balanced_object(text: str, start: int) -> str | None:
    depth = 0
    in_string = False
    escaped = False
    for index in range(start, len(text)):
        char = text[index]
        if in_string:
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == '"':
                in_string = False
            continue
        if char == '"':
            in_string = True
        elif char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return text[start : index + 1]
    return None


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
