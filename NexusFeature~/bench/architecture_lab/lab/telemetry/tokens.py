"""Token accounting.

Client-reported usage is MEASURED. A missing field stays null. It is never
coerced to zero. String-length estimates are a separate ESTIMATED path.
"""

from __future__ import annotations

from typing import Any


def usage_from_events(events: list[dict[str, Any]]) -> dict[str, Any]:
    """Use the last complete usage object in a Codex JSONL stream."""
    found = None
    for event in events:
        parsed = _from_event(event)
        if parsed is not None:
            found = parsed
    if found is None:
        return _empty("MISSING")
    found["status"] = "MEASURED" if _complete(found) else "PARTIAL"
    found["source"] = "MEASURED"
    return found


def estimate_tokens(text: str) -> dict[str, Any]:
    """Labelled estimate. Roughly 4 characters per token. Not a measurement."""
    count = max(1, len(text) // 4) if text else 0
    return {
        "status": "ESTIMATED",
        "source": "ESTIMATED",
        "cached_input_tokens": None,
        "uncached_input_tokens": count,
        "output_tokens": None,
        "reasoning_tokens": None,
        "total_tokens": None,
    }


def _complete(usage: dict[str, Any]) -> bool:
    return usage.get("uncached_input_tokens") is not None and usage.get("output_tokens") is not None


def _empty(status: str) -> dict[str, Any]:
    return {
        "status": status,
        "source": status,
        "cached_input_tokens": None,
        "uncached_input_tokens": None,
        "output_tokens": None,
        "reasoning_tokens": None,
        "total_tokens": None,
    }


def _from_event(event: dict[str, Any]) -> dict[str, Any] | None:
    payload = event.get("usage")
    if isinstance(payload, dict) and _looks_like_usage(payload):
        return _normalize(payload)
    info = (event.get("payload") or {}).get("info") if isinstance(event.get("payload"), dict) else None
    if isinstance(info, dict):
        for key in ("total_token_usage", "last_token_usage"):
            if isinstance(info.get(key), dict) and _looks_like_usage(info[key]):
                return _normalize(info[key])
    return None


def _looks_like_usage(payload: dict[str, Any]) -> bool:
    return any(
        key in payload
        for key in ("input_tokens", "output_tokens", "total_tokens", "uncached_input_tokens")
    )


def _normalize(payload: dict[str, Any]) -> dict[str, Any]:
    cached = _optional_int(payload, "cached_input_tokens")
    if cached is None:
        cached = _optional_int(payload, "cache_read_input_tokens")
    explicit_uncached = _optional_int(payload, "uncached_input_tokens")
    input_tokens = _optional_int(payload, "input_tokens")
    if explicit_uncached is not None:
        uncached = explicit_uncached
    elif input_tokens is not None and cached is not None:
        uncached = max(0, input_tokens - cached)
    elif input_tokens is not None and "cached_input_tokens" not in payload and "cache_read_input_tokens" not in payload:
        # The client did not report a cache split. Do not invent a cached zero.
        uncached = input_tokens
        cached = None
    else:
        uncached = None
    output_tokens = _optional_int(payload, "output_tokens")
    reasoning = _optional_int(payload, "reasoning_output_tokens")
    if reasoning is None:
        reasoning = _optional_int(payload, "reasoning_tokens")
    total = _optional_int(payload, "total_tokens")
    if total is None and uncached is not None and output_tokens is not None:
        total = uncached + (cached or 0) + output_tokens + (reasoning or 0)
    return {
        "cached_input_tokens": cached,
        "uncached_input_tokens": uncached,
        "output_tokens": output_tokens,
        "reasoning_tokens": reasoning,
        "total_tokens": total,
    }


def _optional_int(payload: dict[str, Any], key: str) -> int | None:
    if key not in payload or payload[key] is None:
        return None
    return int(payload[key])
