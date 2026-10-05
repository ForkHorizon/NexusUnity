"""Holdout isolation.

Search code in this module cannot open the holdout split. Confirmation
lives in lab.validation.confirm and is not imported here.
"""

from __future__ import annotations

import json
from pathlib import Path

from lab.errors import HoldoutLeak

SEARCH_SPLITS = {"search"}
VALIDATION_SPLIT = "validation"
HOLDOUT_SPLIT = "holdout"


def assert_search_path(path: Path) -> None:
    parts = set(path.parts)
    if HOLDOUT_SPLIT in parts or "holdout_state.json" in path.name:
        raise HoldoutLeak(f"search path refused: {path}")


def load_task_split(root: Path, split: str, *, confirmation: bool = False) -> list[dict]:
    """Load task JSON for one split.

    The default confirmation flag is false. Search callers must leave it false.
    """
    if split == HOLDOUT_SPLIT and not confirmation:
        raise HoldoutLeak("search loader cannot read holdout tasks")
    if split == VALIDATION_SPLIT and not confirmation:
        raise HoldoutLeak("search loader cannot read validation tasks")
    if split not in SEARCH_SPLITS | {VALIDATION_SPLIT, HOLDOUT_SPLIT}:
        raise HoldoutLeak(f"unknown split {split}")
    directory = root / split
    assert_search_path(directory) if not confirmation else None
    if split == HOLDOUT_SPLIT:
        # Confirmation is the only reader. The assertion above already ran
        # only for non-confirmation. A confirmation reader may proceed.
        pass
    documents = []
    if not directory.is_dir():
        return documents
    for path in sorted(directory.glob("*.json")):
        if not confirmation:
            assert_search_path(path)
        documents.append(json.loads(path.read_text(encoding="utf-8")))
    return documents


def load_search_tasks(root: Path) -> list[dict]:
    """The only task loader the mutation engine is allowed to call."""
    return load_task_split(root, "search", confirmation=False)


def search_metric_keys() -> set[str]:
    """Fields the optimizer may see. Holdout columns are absent on purpose."""
    return {
        "candidate_hash",
        "oracle_successes",
        "attempted",
        "false_successes",
        "coverage",
        "uncached_tokens_per_success",
        "total_tokens_per_success",
        "wrong_tool_calls",
        "tool_calls_per_success",
        "wall_ms_per_success",
        "category_rates",
        "usage_status",
        "feasible",
    }
