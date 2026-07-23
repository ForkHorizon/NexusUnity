"""HTTP transport and config helpers for the NexusUnity bridge."""
from __future__ import annotations

import json
import math
import os
import sys
from typing import Any
import urllib.request

DEFAULT_PORT: int = 8081
JsonObject = dict[str, Any]
JsonRpcResponse = dict[str, Any]


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
    try:
        timeout = float(raw_timeout)
    except ValueError:
        return 120
    return max(1.0, timeout) if math.isfinite(timeout) else 120


UNITY_URL: str = (
    _normalize_url(os.environ["NEXUS_UNITY_URL"])
    if os.environ.get("NEXUS_UNITY_URL")
    else f"http://127.0.0.1:{_read_port()}/"
)
UNITY_TIMEOUT_SECONDS: float = _read_timeout()
def _resolve_auth_token() -> str | None:
    token = os.environ.get("NEXUS_UNITY_AUTH_TOKEN")
    if token:
        return token
    for candidate in (
        os.path.join(os.getcwd(), "Library", "NexusUnityAuthToken.txt"),
        os.path.join(os.getcwd(), "..", "..", "Library", "NexusUnityAuthToken.txt"),
    ):
        if os.path.isfile(candidate):
            try:
                with open(candidate, "r", encoding="utf-8") as handle:
                    val = handle.read().strip()
                    if val:
                        return val
            except Exception:
                pass
    return None


AUTH_TOKEN: str | None = _resolve_auth_token()


def call_unity(method: str, params: JsonObject | None = None) -> JsonRpcResponse:
    payload = {"jsonrpc": "2.0", "method": method, "params": params or {}, "id": 1}
    data: bytes = json.dumps(payload).encode("utf-8")
    headers: dict[str, str] = {"Content-Type": "application/json"}
    token = AUTH_TOKEN or _resolve_auth_token()
    if token:
        headers["X-Nexus-Unity-Token"] = token
    req: urllib.request.Request = urllib.request.Request(
        UNITY_URL,
        data=data,
        headers=headers,
    )
    try:
        with urllib.request.urlopen(req, timeout=UNITY_TIMEOUT_SECONDS) as response:
            return json.loads(response.read().decode("utf-8"))
    except Exception as error:
        error_payload = {
            "code": -32000,
            "message": f"Unity Server unreachable. Error: {error}",
        }
        return {"error": error_payload}
