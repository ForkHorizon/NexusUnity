"""Fixed output normalizers. Mutations may select a name, not new code."""

from __future__ import annotations

import json
from typing import Any

from lab.telemetry.redact import redact


def apply_normalizer(name: str, payload: Any, limit: int = 4000) -> str:
    cleaned = redact(payload)
    if name == "raw":
        text = json.dumps(cleaned, sort_keys=True)
    elif name == "truncate_4k":
        text = json.dumps(cleaned, sort_keys=True)[:limit]
    elif name == "drop_base64":
        text = json.dumps(_drop_base64(cleaned), sort_keys=True)
    elif name == "scalar_value":
        text = json.dumps(_scalar(cleaned), sort_keys=True)
    else:
        raise ValueError(f"unknown normalizer {name}")
    return text


def _drop_base64(value: Any) -> Any:
    if isinstance(value, dict):
        return {
            key: ("<binary omitted>" if _binary_key(key) else _drop_base64(item))
            for key, item in value.items()
        }
    if isinstance(value, list):
        return [_drop_base64(item) for item in value]
    if isinstance(value, str) and len(value) > 500 and _maybe_base64(value):
        return f"<omitted {len(value)} chars>"
    return value


def _binary_key(key: str) -> bool:
    lowered = key.lower()
    return "base64" in lowered or lowered in {"image", "bytes", "png", "jpg"}


def _maybe_base64(value: str) -> bool:
    alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/="
    sample = value[:200]
    return all(ch in alphabet for ch in sample)


def _scalar(value: Any) -> Any:
    if isinstance(value, dict):
        if "value" in value and not isinstance(value["value"], (dict, list)):
            return {"value": value["value"]}
        data = value.get("data")
        if isinstance(data, dict) and "value" in data:
            return {"value": data["value"]}
    return {"summary": "non-scalar result", "keys": sorted(value)[:12] if isinstance(value, dict) else []}
