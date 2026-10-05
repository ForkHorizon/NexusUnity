"""Codex CLI adapter. The first client is GPT-6-Luna. Others can be added later."""

from __future__ import annotations

import json
import os
import subprocess
from pathlib import Path
from typing import Any

from lab.telemetry.redact import redact
from lab.telemetry.tokens import add_usage, usage_from_events

SHELL_ITEM_TYPES = {
    "command_execution",
    "local_shell_call",
    "web_search_call",
    "function_call",
    "custom_tool_call",
    "image_generation_call",
}
SHELL_TOOL_NAMES = {"shell", "local_shell", "exec", "bash", "functions.exec", "functions.shell"}
BENIGN_ITEM_TYPES = {"mcp_tool_call", "agent_message", "message", "reasoning", "error"}
LAB_MCP_SERVER = "architecture_lab"


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
        # A private CODEX_HOME keeps the user's nexus and unityMCP servers out.
        # --ignore-user-config also drops -c MCP servers on Codex 0.160, so the
        # lab writes its own config instead of using that flag.
        home = _isolated_codex_home(wrapper, mcp_args, request.get("reasoning") or "low")
        command = [
            self.binary,
            "exec",
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
            "-c",
            "mcp_servers.architecture_lab.startup_timeout_sec=30",
            # Non-interactive exec uses approval policy never, which rejects an
            # MCP call that still needs a prompt. Approve only this lab server.
            "-c",
            'mcp_servers.architecture_lab.default_tools_approval_mode="approve"',
            "--",
            request["prompt"],
        ]
        env = os.environ.copy()
        env["CODEX_HOME"] = str(home)
        # Do not forward a caller-supplied token into the child environment log.
        outcome = self._exec(command, request, env)
        if outcome["status"] == "crashed" and _transport_failure(outcome):
            # One transport retry. The failed attempt stays attached, and its
            # token bill is added. Keeping only the second usage undercounts.
            second = self._exec(command, request, env)
            second["transport_retries"] = 1
            second["prior_events"] = outcome.get("events") or []
            second["usage"] = add_usage(outcome.get("usage"), second.get("usage"))
            return second
        return outcome

    def _exec(self, command: list[str], request: dict[str, Any], env: dict[str, str]) -> dict[str, Any]:
        try:
            proc = subprocess.run(
                command,
                capture_output=True,
                text=True,
                timeout=int(request["timeout_s"]),
                check=False,
                env=env,
                cwd=str(request["workspace"]),
                # A piped stdin makes Codex append it to the prompt and can stall.
                stdin=subprocess.DEVNULL,
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
        client = inspect_client_events(events)
        bypass = client["surface_bypass"]
        mismatch = client["mechanism_mismatch"]
        status = "completed"
        if timed_out:
            status = "timeout"
        elif proc.returncode != 0 and not message and not tool_calls:
            status = "crashed"
        if bypass:
            status = "surface_bypass"
        elif mismatch:
            # The client served a different exposure mechanism than the candidate
            # asked for. Scoring it would measure that substitution.
            status = "mechanism_mismatch"
        return redact(
            {
                "status": status,
                "exit_code": None if timed_out else proc.returncode,
                "events": events,
                "usage": usage,
                "final_message": message,
                "tool_calls": tool_calls,
                "surface_bypass": bypass,
                "mechanism_mismatch": mismatch,
                "stderr_tail": (stderr or "")[-500:],
                "model": request["model"],
                "reasoning": request["reasoning"],
                "command_preview": [part for part in command if "token" not in part.lower()],
                "mechanism_actually_used": request.get("mechanism"),
            }
        )


CODEX_HOME = Path("/tmp/nexus-arch-lab-codex")


def _isolated_codex_home(command: str, args: list[str], reasoning: str = "low") -> Path:
    home = CODEX_HOME
    home.mkdir(parents=True, exist_ok=True)
    auth = home / "auth.json"
    source = Path.home() / ".codex" / "auth.json"
    if source.is_file() and not auth.exists():
        auth.symlink_to(source)
    quoted = "[" + ",".join(_toml(item) for item in args) + "]"
    # Apps tools are client-injected and are not part of any candidate surface.
    # Codex 0.160 accepts features.apps=false. Other client tools stay at their
    # defaults so every candidate sees the same remainder.
    config = "\n".join(
        [
            'model = "gpt-6-luna"',
            f"model_reasoning_effort = {_toml(reasoning)}",
            "",
            "[features]",
            "apps = false",
            "",
            "[mcp_servers.architecture_lab]",
            f"command = {_toml(command)}",
            f"args = {quoted}",
            "startup_timeout_sec = 30",
            'default_tools_approval_mode = "approve"',
            "enabled = true",
            "",
        ]
    )
    (home / "config.toml").write_text(config, encoding="utf-8")
    return home


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
                "visible_bytes": _visible_bytes(item.get("result")),
            }
        )
    return calls


def _visible_bytes(result: Any) -> int | None:
    if result is None:
        return None
    if isinstance(result, str):
        return len(result.encode("utf-8"))
    return len(json.dumps(result).encode("utf-8"))


def _transport_failure(outcome: dict[str, Any]) -> bool:
    blob = json.dumps({"events": outcome.get("events") or [], "stderr": outcome.get("stderr_tail") or ""})
    return "workspace routing discovery failed" in blob


def inspect_client_events(events: list[dict[str, Any]]) -> dict[str, bool]:
    """Separate a foreign tool from a client mechanism substitution.

    A shell, image, custom tool, or MCP server other than the lab server is a
    surface bypass: that candidate left the surface. `tool_search` is not a
    supported exposure mode on Codex 0.160. Treating it as the requested
    surface would score a substitution. Unknown item types fail closed.
    """
    bypass = False
    mismatch = False
    for event in events:
        item = event.get("item") if isinstance(event.get("item"), dict) else {}
        item_type = str(item.get("type") or "")
        name = str(item.get("tool") or item.get("name") or "")
        event_type = str(event.get("type") or "")
        if item_type == "tool_search_call" or event_type == "tool_search_call":
            mismatch = True
            continue
        if item_type in SHELL_ITEM_TYPES or name in SHELL_TOOL_NAMES:
            bypass = True
            continue
        if item_type == "mcp_tool_call":
            server = item.get("server")
            if server not in (None, "", LAB_MCP_SERVER):
                bypass = True
            continue
        if item_type and item_type not in BENIGN_ITEM_TYPES:
            bypass = True
    return {"surface_bypass": bypass, "mechanism_mismatch": mismatch}
