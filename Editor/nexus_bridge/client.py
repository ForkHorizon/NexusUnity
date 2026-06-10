"""HTTP transport and logging utilities for the NexusUnity Python bridge.

Provides :func:`call_unity` for sending JSON-RPC requests to the local Unity
editor server, and a pre-configured :data:`logger` that callers can use instead
of ad-hoc ``print``/``stderr`` writes.
"""
from __future__ import annotations

import json
import logging
import os
import sys
import urllib.request
from typing import Any

DEFAULT_PORT = 8081

logger: logging.Logger = logging.getLogger("nexus_bridge")


def _normalize_url(url: str) -> str:
    return url if url.endswith("/") else url + "/"


def _read_port() -> int:
    raw_port = os.environ.get("NEXUS_UNITY_PORT")
    if raw_port:
        return int(raw_port)

    if len(sys.argv) > 1:
        try:
            return int(sys.argv[1])
        except ValueError:
            pass

    return DEFAULT_PORT


def _read_timeout() -> float:
    raw_timeout = os.environ.get("NEXUS_UNITY_TIMEOUT_SECONDS")
    if not raw_timeout:
        return 120

    return max(1.0, float(raw_timeout))


UNITY_URL: str = (
    _normalize_url(os.environ["NEXUS_UNITY_URL"])
    if os.environ.get("NEXUS_UNITY_URL")
    else f"http://127.0.0.1:{_read_port()}/"
)
UNITY_TIMEOUT_SECONDS: float = _read_timeout()


def call_unity(method: str, params: dict[str, Any] | None = None) -> dict[str, Any]:
    """Send a JSON-RPC request to the Unity editor server and return the response.

    Returns a dict with an ``"error"`` key if the server is unreachable or
    returns an HTTP error, matching the JSON-RPC error object shape.
    """
    payload = {"jsonrpc": "2.0", "method": method, "params": params or {}, "id": 1}
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(UNITY_URL, data=data, headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=UNITY_TIMEOUT_SECONDS) as f:
            return json.loads(f.read().decode("utf-8"))  # type: ignore[no-any-return]
    except Exception as e:
        return {"error": {"code": -32000, "message": f"Unity Server unreachable. Error: {e}"}}
