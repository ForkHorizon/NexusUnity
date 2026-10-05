"""Confirmation loader. Search code must not import this module."""

from __future__ import annotations

from pathlib import Path

from lab.errors import HoldoutLeak, LabError
from lab.search.holdout import load_task_split


def load_confirmation_tasks(root: Path, *, include_holdout: bool, confirm_final: bool) -> dict[str, list]:
    if include_holdout and not confirm_final:
        raise HoldoutLeak("holdout tasks require --confirm-final")
    if not confirm_final:
        raise LabError("confirmation refuses to run without --confirm-final")
    payload = {
        "validation": load_task_split(root, "validation", confirmation=True),
    }
    if include_holdout:
        payload["holdout"] = load_task_split(root, "holdout", confirmation=True)
    return payload
