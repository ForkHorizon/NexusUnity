"""HTTP transport and config helpers for the NexusUnity bridge."""
from __future__ import annotations

import json
import os
import sys
import urllib.request
from typing import Any

DEFAULT_PORT: int = 8081


def _normalize_url(url: str) -> str:
    return url if url.endswith("/") else url + "/"


def _read_port() -> int:
    raw_port: str | None = os.environ.get("NEXUS_UNITY_PORT")
    if raw_port:
        try:
            return int(raw_port)
        except ValueError:
            pass
    if len(sys.argv) > 1:
        try:
            return int(sys.argv[1])
        except ValueError:
            pass
    return DEFAULT_PORT


def _read_timeout() -> float:
    raw_timeout: str | None = os.environ.get("NEXUS_UNITY_TIMEOUT_SECONDS")
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
    payload: dict[str, Any] = {"jsonrpc": "2.0", "method": method, "params": params or {}, "id": 1}
    data: bytes = json.dumps(payload).encode("utf-8")
    req: urllib.request.Request = urllib.request.Request(
        UNITY_URL,
        data=data,
        headers={"Content-Type": "application/json"},
    )
    try:
        with urllib.request.urlopen(req, timeout=UNITY_TIMEOUT_SECONDS) as response:
            return json.loads(response.read().decode("utf-8"))
    except Exception as error:
        return {"error": {"code": -32000, "message": f"Unity Server unreachable. Error: {error}"}}
