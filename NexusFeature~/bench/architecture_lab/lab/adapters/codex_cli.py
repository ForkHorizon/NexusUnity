"""Codex CLI adapter. The first client is GPT-6-Luna. Others can be added later."""

from __future__ import annotations

import json
import os
import subprocess
from pathlib import Path
from typing import Any

from lab.telemetry.redact import redact
from lab.telemetry.tokens import usage_from_events

SHELL_ITEM_TYPES = {
    "command_execution",
    "local_shell_call",
    "web_search_call",
    "function_call",
}
ALLOWED_ITEM_TYPES = {"mcp_tool_call", "agent_message", "reasoning", "error"}


def codex_version(binary: str = "codex") -> str:
    proc = subprocess.run([binary, "--version"], capture_output=True, text=True, check=False)
    return (proc.stdout or proc.stderr or "").strip()


class CodexCliAdapter:
    """One non-interactive Codex exec. The workspace must not be the package repo."""

    def __init__(self, binary: str = "codex", python: str = "python3"):
        self.binary = binary
        self.python = python

    def run(self, request: dict[str, Any]) -> dict[str, Any]:
        workspace = Path(request["workspace"])
        workspace.mkdir(parents=True, exist_ok=True)
        if Path(request["repo_root"]).resolve() == workspace.resolve():
            raise RuntimeError("refusing to point Codex at the package repository")
        surface_path = Path(request["surface_path"])
        catalog_path = Path(request["catalog_path"])
        telemetry_path = Path(request["telemetry_path"])
        wrapper = str(Path(request["mcp_wrapper"]))
        mcp_args = [
            "--surface",
            str(surface_path),
            "--catalog",
            str(catalog_path),
            "--unity-binary",
            request["unity_binary"],
            "--project",
            request["project_path"],
            "--port",
            str(request["port"]),
            "--telemetry",
            str(telemetry_path),
        ]
        command = [
            self.binary,
            "exec",
            "--ignore-user-config",
            "--json",
            "--ephemeral",
            "--skip-git-repo-check",
            "--sandbox",
            "read-only",
            "-m",
            request["model"],
            "-C",
            str(workspace),
            "-c",
            f'model_reasoning_effort={_toml(request["reasoning"])}',
            "-c",
            f"mcp_servers.architecture_lab.command={_toml(wrapper)}",
            "-c",
            "mcp_servers.architecture_lab.args=" + _toml_array(mcp_args),
            "--",
            request["prompt"],
        ]
        env = os.environ.copy()
        # Do not forward a caller-supplied token into the child environment log.
        try:
            proc = subprocess.run(
                command,
                capture_output=True,
                text=True,
                timeout=int(request["timeout_s"]),
                check=False,
                env=env,
                cwd=str(workspace),
            )
            timed_out = False
        except subprocess.TimeoutExpired as exc:
            proc = exc
            timed_out = True
        stdout = "" if timed_out else proc.stdout
        stderr = "" if timed_out else proc.stderr
        events = _parse_jsonl(stdout)
        usage = usage_from_events(events)
        message = _final_message(events)
        tool_calls = _tool_calls(events)
        bypass = _shell_bypass(events)
        status = "completed"
        if timed_out:
            status = "timeout"
        elif proc.returncode != 0 and not message and not tool_calls:
            status = "crashed"
        if bypass:
            status = "surface_bypass"
        return redact(
            {
                "status": status,
                "exit_code": None if timed_out else proc.returncode,
                "events": events,
                "usage": usage,
                "final_message": message,
                "tool_calls": tool_calls,
                "surface_bypass": bypass,
                "stderr_tail": (stderr or "")[-500:],
                "model": request["model"],
                "reasoning": request["reasoning"],
                "command_preview": [part for part in command if "token" not in part.lower()],
                "mechanism_actually_used": request.get("mechanism"),
            }
        )


def _toml(value: str) -> str:
    escaped = value.replace("\\", "\\\\").replace('"', '\\"')
    return f'"{escaped}"'


def _toml_array(values: list[str]) -> str:
    return "[" + ",".join(_toml(item) for item in values) + "]"


def _parse_jsonl(stdout: str) -> list[dict[str, Any]]:
    events = []
    for line in stdout.splitlines():
        line = line.strip()
        if not line or not line.startswith("{"):
            continue
        try:
            event = json.loads(line)
        except json.JSONDecodeError:
            continue
        if isinstance(event, dict):
            events.append(event)
    return events


def _final_message(events: list[dict[str, Any]]) -> str:
    text = ""
    for event in events:
        item = event.get("item") if isinstance(event.get("item"), dict) else None
        if item and item.get("type") in {"agent_message", "message"}:
            text = item.get("text") or item.get("message") or text
        if event.get("type") == "item.completed" and isinstance(event.get("item"), dict):
            if event["item"].get("type") == "agent_message":
                text = event["item"].get("text") or text
    return text


def _tool_calls(events: list[dict[str, Any]]) -> list[dict[str, Any]]:
    calls = []
    for event in events:
        item = event.get("item") if isinstance(event.get("item"), dict) else None
        if not item or item.get("type") != "mcp_tool_call":
            continue
        if event.get("type") not in {None, "item.completed", "item.started"} and not str(event.get("type", "")).endswith("completed"):
            # Keep completed calls. A started event without a result is still a call.
            pass
        if event.get("type") == "item.started":
            continue
        calls.append(
            {
                "exposed_name": item.get("tool") or item.get("name"),
                "arguments": item.get("arguments") or {},
                "ok": item.get("error") in (None, ""),
                "server": item.get("server"),
            }
        )
    return calls


def _shell_bypass(events: list[dict[str, Any]]) -> bool:
    for event in events:
        item = event.get("item") if isinstance(event.get("item"), dict) else {}
        if item.get("type") in SHELL_ITEM_TYPES:
            return True
        name = str(item.get("tool") or item.get("name") or "")
        if name in {"shell", "local_shell", "exec", "bash"}:
            return True
    return False
