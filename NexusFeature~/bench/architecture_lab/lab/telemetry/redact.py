"""Strip secrets before anything is stored or printed."""

from __future__ import annotations

from typing import Any

_SECRET_KEYS = ("token", "secret", "authorization", "password", "bearer", "evaltoken", "api_key")


def redact(value: Any) -> Any:
    if isinstance(value, dict):
        cleaned = {}
        for key, item in value.items():
            if any(part in str(key).lower() for part in _SECRET_KEYS):
                cleaned[key] = "<redacted>"
            else:
                cleaned[key] = redact(item)
        return cleaned
    if isinstance(value, list):
        return [redact(item) for item in value]
    if isinstance(value, str):
        if "Bearer " in value or "evalToken" in value:
            return "<redacted>"
        return value
    return value
