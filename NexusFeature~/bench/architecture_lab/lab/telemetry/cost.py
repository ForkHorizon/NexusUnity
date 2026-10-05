"""Optional money estimates. Null prices stay null and never become zero."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any


def load_profile(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def estimate_cost(usage: dict[str, Any], profile: dict[str, Any]) -> dict[str, Any]:
    prices = (
        profile.get("uncached_input_per_million"),
        profile.get("cached_input_per_million"),
        profile.get("output_per_million"),
    )
    if usage.get("status") != "MEASURED" or any(price is None for price in prices):
        return {"estimated_cost": None, "source": "UNPRICED", "profile_id": profile.get("profile_id")}
    if usage.get("cached_input_tokens") is None or usage.get("uncached_input_tokens") is None or usage.get("output_tokens") is None:
        return {"estimated_cost": None, "source": "UNPRICED", "profile_id": profile.get("profile_id")}
    uncached = usage["uncached_input_tokens"]
    cached = usage["cached_input_tokens"]
    output = usage["output_tokens"]
    cost = (
        uncached * prices[0] + cached * prices[1] + output * prices[2]
    ) / 1_000_000
    return {
        "estimated_cost": cost,
        "source": "PRICED_FROM_MEASURED_TOKENS",
        "profile_id": profile.get("profile_id"),
    }
