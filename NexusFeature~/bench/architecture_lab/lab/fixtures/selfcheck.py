"""Reference-path task check. A failure marks the task INVALID. It is not scored.

This module reads task files for the builder. The search engine must not import it.
"""

from __future__ import annotations

import json
import time
from pathlib import Path
from typing import Any

from lab.fixtures.observe import extract_cs, observe_state, pristine_match
from lab.fixtures.oracles import evaluate
from lab.fixtures.reference import editor_status, find_marker, open_lab, write_field
from lab.fixtures.reset import fingerprint, reload_lab_scene

WRITE_FIELDS = {
    "intensity": "intensity",
    "revision": "revision",
    "marker_name": "markerName",
}

BROKEN_SOURCE = "public class LabBrokenMarker { void Boom() { int x = missingName; } }\n"


def reference_text(task: dict[str, Any], observation: dict[str, Any]) -> str | None:
    """The text a correct agent would return after the reference path ran."""
    oracle = task["oracle"]
    if oracle["type"] == "unity_field_equals":
        return json.dumps({"status": "done"})
    if oracle["type"] != "model_json_equals_unity":
        return None
    value = observation.get(oracle["unity_field"])
    if value is None:
        return None
    return json.dumps({oracle["field"]: value})


def plant_compile_error(project: Path) -> Path:
    path = project / "Assets" / "LabFixture" / "LabBrokenMarker.cs"
    path.write_text(BROKEN_SOURCE, encoding="utf-8")
    return path


def collect_error_code(backend, *, pause=time.sleep, attempts: int = 8) -> str | None:
    first = backend("recompile", {"focus": False})
    code = extract_cs(first)
    if code:
        return code
    for _ in range(attempts):
        pause(1)
        status = backend("recompile_status", {})
        code = extract_cs(status)
        if code:
            return code
    return None


def check_task(
    task: dict[str, Any],
    backend,
    *,
    project: Path,
    pristine: Path,
    destination: Path,
    pause=time.sleep,
) -> dict[str, Any]:
    """Run the reference path, the oracle, and a reset. Do not lower the bar."""
    if task["category"] == "visual":
        return _invalid(task, "no deterministic pixel fixture in v1")
    try:
        reloaded = reload_lab_scene(backend, project, pristine, destination)
    except Exception as exc:
        return _invalid(task, f"reset failed before the reference path: {exc}")
    before = observe_state(backend, include_assets=False)
    problems = pristine_match(before)
    if problems:
        return _invalid(task, "pristine live state mismatch: " + ",".join(problems), observation=before)
    expected_fp = reloaded["fingerprint"]
    observation = dict(before)
    if "plant_compile_error" in (task.get("setup") or []):
        plant_compile_error(project)
        observation["error_code"] = collect_error_code(backend, pause=pause)
    elif task["oracle"]["type"] == "unity_field_equals":
        target = (find_marker(backend) or {}).get("object_name")
        field = WRITE_FIELDS.get(task["oracle"]["unity_field"])
        if not target or not field:
            return _invalid(task, "write reference could not resolve the target")
        write_field(backend, target, field, task["oracle"]["expected"])
        # Do not reopen. open_scene reloads the scene file and drops this edit.
        observation = observe_state(backend, include_assets=False, reload_scene=False)
    elif task["required_capability_class"] in {"rare", "out_of_default", "asset_read", "diagnose"}:
        observation = observe_state(backend, include_assets=True)
    text = reference_text(task, observation)
    if text is None:
        return _invalid(task, "reference path produced no oracle value", observation=observation)
    verdict = evaluate(task, text, observation)
    planted = "plant_compile_error" in (task.get("setup") or [])
    try:
        restored = reload_lab_scene(backend, project, pristine, destination)
    except Exception as exc:
        return _invalid(task, f"reset failed after the reference path: {exc}", verdict=verdict)
    if planted:
        backend("recompile", {"focus": False})
        # The broken script takes the whole domain down. Wait until it is
        # compiling cleanly, then open the restored scene again.
        if not _wait_until_settled(backend, pause):
            return _invalid(task, "editor stayed compiling after the diagnose reset", verdict=verdict)
        open_lab(backend)
        if not _wait_until_settled(backend, pause):
            return _invalid(task, "editor stayed compiling after reopening the restored scene", verdict=verdict)
    after = observe_state(backend, include_assets=False)
    after_problems = pristine_match(after)
    if planted and extract_cs(backend("recompile_status", {})):
        after_problems.append("compile_error_remains")
    fp_ok = restored["fingerprint"] == expected_fp == fingerprint(destination)
    valid = bool(verdict["oracle_success"]) and fp_ok and not after_problems
    return {
        "task_id": task["task_id"],
        "split": task["split"],
        "status": "VALID" if valid else "INVALID",
        "reason": None if valid else _reason(verdict, fp_ok, after_problems),
        "oracle_success": verdict["oracle_success"],
        "fingerprint": expected_fp,
        "observation_keys": sorted(key for key, value in observation.items() if value is not None),
    }


def _wait_until_settled(backend, pause, attempts: int = 40) -> bool:
    for _ in range(attempts):
        status = editor_status(backend)
        if status.get("status") == "ready" and not status.get("compiling"):
            return True
        pause(1)
    return False


def _reason(verdict: dict[str, Any], fp_ok: bool, problems: list[str]) -> str:
    parts = []
    if not verdict["oracle_success"]:
        parts.append("oracle rejected the reference path")
    if not fp_ok:
        parts.append("fingerprint changed across reset")
    if problems:
        parts.append("live state after reset: " + ",".join(problems))
    return "; ".join(parts) or "invalid"


def _invalid(task: dict[str, Any], reason: str, **extra: Any) -> dict[str, Any]:
    return {
        "task_id": task["task_id"],
        "split": task["split"],
        "status": "INVALID",
        "reason": reason,
        "oracle_success": False,
        **extra,
    }
