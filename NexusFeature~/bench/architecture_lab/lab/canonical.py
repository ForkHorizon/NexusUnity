"""Canonical JSON and stable hashes.

Two manifests that describe the same architecture hash the same.
Identity fields (ids, parents, notes, generation) are not part of the hash.
"""

from __future__ import annotations

import hashlib
import json
from typing import Any


def canonical_dumps(value: Any) -> str:
    """Serialize JSON with sorted keys and no insignificant whitespace."""
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False)


def sha256_text(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def sha256_json(value: Any) -> str:
    return sha256_text(canonical_dumps(value))


def sort_strings(values: list[str] | tuple[str, ...]) -> list[str]:
    return sorted(values)
