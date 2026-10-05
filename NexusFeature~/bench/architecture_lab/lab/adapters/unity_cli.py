"""Official Unity CLI backend.

Every lab surface calls this same adapter. The adapter does not reimplement
Unity commands.
"""

from __future__ import annotations

import json
import subprocess
from typing import Any

from lab.errors import SafetyError
from lab.telemetry.redact import redact


def parse_unity_stdout(text: str) -> dict[str, Any]:
    """Parse one CLI response. Beta.12 sometimes prints more than one JSON body."""
    raw = (text or "").strip()
    if not raw:
        return {"success": False, "errors": [{"message": "empty"}]}
    try:
        parsed = json.loads(raw)
        if isinstance(parsed, dict):
            return parsed
    except json.JSONDecodeError:
        parsed = None
    decoder = json.JSONDecoder()
    bodies: list[dict[str, Any]] = []
    index = 0
    while index < len(raw):
        while index < len(raw) and raw[index].isspace():
            index += 1
        if index >= len(raw):
            break
        try:
            obj, end = decoder.raw_decode(raw, index)
        except json.JSONDecodeError:
            break
        if isinstance(obj, dict):
            bodies.append(obj)
        index = end
    if not bodies:
        return {"success": False, "errors": [{"message": "non-json"}]}
    chosen = bodies[-1]
    for body in bodies:
        if body.get("success") is True or "data" in body or "result" in body:
            chosen = body
            break
    chosen = dict(chosen)
    chosen["_body_count"] = len(bodies)
    return chosen


def _prepare_arguments(command: str, arguments: dict[str, Any]) -> dict[str, Any]:
    """Numbers become JSON tokens. Strings stay bare.

    The CLI JSON-encodes each argument itself. Pre-quoting a string stores
    the quote characters in the field (`"holdout-ok"` instead of holdout-ok).
    """
    if command != "set_serialized_field" or "value" not in arguments:
        return arguments
    prepared = dict(arguments)
    prepared["value"] = _json_token(prepared["value"])
    return prepared


def _json_token(value: Any) -> str:
    if isinstance(value, str):
        return value
    return json.dumps(value)


def command_body(payload: Any) -> Any:
    """Return the command result object from either a CLI envelope or a bare body."""
    if not isinstance(payload, dict):
        return payload
    for key in ("data", "result"):
        inner = payload.get(key)
        if isinstance(inner, dict) and inner:
            nested = inner.get("result")
            if isinstance(nested, dict) and "success" not in inner:
                return nested
            return inner
    return payload


class UnityCliBackend:
    def __init__(self, binary: str, project_path: str, expected_port: int, timeout_s: int = 60):
        if expected_port == 7800:
            raise SafetyError("lab backend refuses port 7800")
        if "UnityTestForNexus" in project_path:
            raise SafetyError("lab backend refuses the harness project")
        self.binary = binary
        self.project_path = project_path
        self.expected_port = expected_port
        self.timeout_s = timeout_s

    def argv(self, command: str, arguments: dict[str, Any]) -> list[str]:
        arguments = _prepare_arguments(command, arguments)
        argv = [
            self.binary,
            "--format",
            "json",
            "--non-interactive",
            "command",
            "--timeout",
            str(self.timeout_s),
            "--project-path",
            self.project_path,
            "--",
            command,
        ]
        for key in sorted(arguments):
            value = arguments[key]
            if value is None:
                continue
            if isinstance(value, bool):
                argv.extend([f"--{key}", "true" if value else "false"])
            elif isinstance(value, (dict, list)):
                argv.extend([f"--{key}", json.dumps(value, separators=(",", ":"))])
            else:
                argv.extend([f"--{key}", str(value)])
        return argv

    def call(self, command: str, arguments: dict[str, Any]) -> dict[str, Any]:
        proc = subprocess.run(
            self.argv(command, arguments),
            capture_output=True,
            text=True,
            timeout=self.timeout_s + 20,
            check=False,
        )
        payload = redact(parse_unity_stdout(proc.stdout))
        self._check_target(payload)
        return {
            "exit_code": proc.returncode,
            "payload": payload,
            "backend_command": command,
            "ok": bool(payload.get("success")) and proc.returncode == 0,
        }

    def _check_target(self, payload: dict[str, Any]) -> None:
        target = ((payload.get("data") or {}) if isinstance(payload.get("data"), dict) else {}).get("target") or {}
        if not isinstance(target, dict) or not target:
            return
        path = str(target.get("projectPath") or "")
        port = target.get("port")
        if path and self.project_path not in path and not path.endswith(self.project_path.rstrip("/")):
            # Unity reports /private/tmp when the project lives at /tmp.
            normalized = path.replace("/private/tmp/", "/tmp/")
            expected = self.project_path.replace("/private/tmp/", "/tmp/")
            if expected not in normalized and not normalized.endswith(expected):
                raise SafetyError("unity response projectPath does not match the lab project")
        if port is not None and int(port) != int(self.expected_port):
            raise SafetyError(f"unity response port {port} != lab port {self.expected_port}")
