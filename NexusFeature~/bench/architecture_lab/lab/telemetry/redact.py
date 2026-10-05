"""Strip secrets before anything is stored or printed."""

from __future__ import annotations

from typing import Any

_SECRET_PARTS = ("secret", "authorization", "password", "bearer", "evaltoken", "api_key")


def _secret_key(key: str) -> bool:
    """Credential fields only. `input_tokens` is a measurement, not a secret."""
    lowered = str(key).lower()
    if lowered == "tokens" or lowered.endswith("_tokens") or "token_usage" in lowered:
        return False
    if any(part in lowered for part in _SECRET_PARTS):
        return True
    return lowered == "token" or lowered.endswith("_token") or lowered.startswith("token_")


def redact(value: Any) -> Any:
    if isinstance(value, dict):
        cleaned = {}
        for key, item in value.items():
            if _secret_key(key):
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
