"""Task manifest checks. Prompts must not contain read-oracle answers."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from lab.errors import CandidateInvalid

REQUIRED = {
    "task_id",
    "split",
    "category",
    "initial_state",
    "setup",
    "prompt",
    "allowed_environment",
    "required_capability_class",
    "oracle",
    "cleanup",
    "timeout_s",
}
SPLITS = {"search", "validation", "holdout"}
CATEGORIES = {"read", "diagnose", "write", "visual", "project", "rare", "out_of_default", "asset_read"}


def validate_task(task: dict[str, Any], forbidden_literals: list[str] | None = None) -> None:
    missing = sorted(REQUIRED - set(task))
    if missing:
        raise CandidateInvalid(f"{task.get('task_id')} missing {missing}")
    if task["split"] not in SPLITS:
        raise CandidateInvalid(f"bad split on {task['task_id']}")
    if task["category"] not in CATEGORIES:
        raise CandidateInvalid(f"bad category on {task['task_id']}")
    if task["category"] != task["required_capability_class"] and task["required_capability_class"] not in CATEGORIES:
        raise CandidateInvalid(f"bad class on {task['task_id']}")
    oracle = task["oracle"]
    if oracle.get("type") not in {"model_json_equals_unity", "unity_field_equals"}:
        raise CandidateInvalid(f"bad oracle on {task['task_id']}")
    if task["allowed_environment"] != "lab_scratch_only":
        raise CandidateInvalid("tasks may only touch the lab scratch project")
    for literal in forbidden_literals or []:
        if literal and literal in task["prompt"]:
            raise CandidateInvalid(f"{task['task_id']} prompt leaks {literal!r}")


def load_tasks(directory: Path) -> list[dict[str, Any]]:
    return [json.loads(path.read_text(encoding="utf-8")) for path in sorted(directory.glob("*.json"))]
