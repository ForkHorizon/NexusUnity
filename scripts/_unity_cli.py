"""Shared `unity command` helpers for the Pipeline stabilize scripts.

Pipeline 1.0.0+ groups Nexus commands (`unity command nexus capture_game_view`) and
recompile (`script recompile`); older Pipelines use the global `nexus_*` / `recompile`
names. Flags are kebab-case on every version.
"""

from __future__ import annotations

import json
import os
import subprocess
import sys
from functools import cache
from pathlib import Path

PROJ = Path(__file__).resolve().parents[3]  # Unity project root from Assets/NexusUnity/scripts
if not (PROJ / "Assets").exists():
    sys.exit("Run from Assets/NexusUnity/scripts inside a Unity project (Assets/ not found at " + str(PROJ) + ").")


def run_cmd(args: list[str], timeout: int = 60) -> dict:
    """Run one `unity command` and return its JSON result, or a failure dict."""
    env = os.environ.copy()
    env["UNITY_NO_BANNER"] = "1"
    env["UNITY_NON_INTERACTIVE"] = "1"
    proc = subprocess.run(
        [
            "unity",
            "command",
            "--project-path",
            str(PROJ),
            "--json",
            "--result-only",
            "--timeout",
            str(timeout),
            *args,
        ],
        capture_output=True,
        text=True,
        env=env,
        timeout=timeout + 20,
        check=False,
    )
    raw = (proc.stdout or proc.stderr or "").strip()
    try:
        return json.loads(raw)
    except json.JSONDecodeError:
        return {
            "success": False,
            "parse_error": raw[:800],
            "returncode": proc.returncode,
        }


@cache
def grouped() -> bool:
    """True when the Editor serves the Pipeline 1.0.0+ grouped `nexus` commands."""
    return "ok" in run_cmd(["nexus", "project_map"], timeout=30)


def nexus(name: str, *flags: str, timeout: int = 60) -> dict:
    """Run a Nexus command and flatten its envelope: `data` fields plus `success` from `ok`."""
    command = ["nexus", name] if grouped() else ["nexus_" + name]
    envelope = run_cmd([*command, *flags], timeout=timeout)
    data = dict(envelope.get("data") or {})
    data["success"] = bool(envelope.get("ok"))
    data["error"] = envelope.get("error") or envelope.get("parse_error")
    return data


def recompile(timeout: int = 180) -> dict:
    """Request a script recompile on any Pipeline version."""
    return run_cmd(["script", "recompile"] if grouped() else ["recompile"], timeout=timeout)
