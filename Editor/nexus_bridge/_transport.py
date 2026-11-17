"""HTTP transport and config helpers for the NexusUnity bridge."""

from __future__ import annotations

import json
import logging
import math
import os
import sys
from http import HTTPStatus
from typing import Any
import urllib.error
import urllib.request

DEFAULT_PORT: int = 8081
JsonObject = dict[str, Any]
JsonRpcResponse = dict[str, Any]


logger = logging.getLogger(__name__)


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


def _try_read_token(candidate: str) -> str | None:
    if not os.path.isfile(candidate):
        return None
    try:
        with open(candidate, encoding="utf-8") as handle:
            val = handle.read().strip()
            return val or None
    except (OSError, UnicodeDecodeError):
        return None


def _token_file_candidates() -> list[str]:
    candidates = [
        os.path.join(os.getcwd(), "Library", "NexusUnityAuthToken.txt"),
        os.path.join(os.getcwd(), "..", "..", "Library", "NexusUnityAuthToken.txt"),
    ]
    # MCP clients often start the bridge outside the project (a global config, any working
    # directory), so also look in every folder above the bridge itself. That finds the project
    # Library whether the bridge was deployed to the project root or runs from the package.
    folder = os.path.dirname(os.path.abspath(__file__))
    while True:
        candidates.append(os.path.join(folder, "Library", "NexusUnityAuthToken.txt"))
        parent = os.path.dirname(folder)
        if parent == folder:
            return candidates
        folder = parent


def _read_token_file() -> str | None:
    for candidate in _token_file_candidates():
        token = _try_read_token(candidate)
        if token:
            return token
    return None


def _resolve_auth_token(force_reload: bool = False) -> str | None:
    """Resolve the Unity auth token.

    Unity rewrites Library/NexusUnityAuthToken.txt on every domain reload, so a
    token resolved once at import goes stale as soon as a script compiles. Every
    authenticated method then returns HTTP 401 for the rest of the session while
    get_server_status keeps succeeding (it is exempt from auth), which reads to a
    caller as the editor never coming back. Pass force_reload=True after a 401 to
    pick up the rotated token.
    """
    if not force_reload:
        token = os.environ.get("NEXUS_UNITY_AUTH_TOKEN")
        if token:
            return token
    token = _read_token_file()
    if token:
        os.environ["NEXUS_UNITY_AUTH_TOKEN"] = token
    return token


AUTH_TOKEN: str | None = _resolve_auth_token()


def _unreachable_message(error: Exception) -> str:
    message = f"Unity Server unreachable. Error: {error}"
    reason = getattr(error, "reason", error)
    if isinstance(reason, ConnectionRefusedError):
        # Nothing listens on the port: the editor is closed, or the server was stopped and
        # Unity remembers that across reloads and restarts.
        message += (
            f" Nothing is listening on {UNITY_URL}. Open the Unity project, then click"
            " 'Start Server' in Window > Nexus Unity, or set NEXUS_UNITY_PORT if the server uses another port."
        )
    return message


def call_unity(method: str, params: JsonObject | None = None) -> JsonRpcResponse:
    payload = {"jsonrpc": "2.0", "method": method, "params": params or {}, "id": 1}
    data: bytes = json.dumps(payload).encode("utf-8")

    # Retry once on 401 with a freshly read token — see _resolve_auth_token.
    # First attempt uses the token resolved at import (AUTH_TOKEN); only the
    # retry re-reads the file, so normal calls keep the import-time token.
    for attempt in range(2):
        headers: dict[str, str] = {"Content-Type": "application/json"}
        token = _resolve_auth_token(force_reload=True) if attempt == 1 else (AUTH_TOKEN or _resolve_auth_token())
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
        except urllib.error.HTTPError as error:
            if error.code == HTTPStatus.UNAUTHORIZED and attempt == 0:
                continue
            # Distinguish "the server answered and refused" from "unreachable";
            # reporting a 401 as unreachable sends debugging the wrong way.
            return {
                "error": {
                    "code": -32000,
                    "message": f"Unity Server returned HTTP {error.code} {error.reason}.",
                }
            }
        except Exception as error:
            # Readiness polling hits network errors twice a second during a domain reload. A traceback
            # for each one floods stderr and blocks a client that does not read it, so only unexpected
            # errors carry one.
            logger.warning("Unity call %s failed: %s", method, error, exc_info=not isinstance(error, OSError))
            return {"error": {"code": -32000, "message": _unreachable_message(error)}}
    return {"error": {"code": -32000, "message": "Unity Server authorization failed."}}
