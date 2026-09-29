#!/usr/bin/env python3
"""Pending-capture / domain-reload recovery: N recompile cycles + capture.

Does not call Nexus HTTP. Requires a running Editor with Pipeline.
"""
from __future__ import annotations

import json
import os
import subprocess
import sys
import time
from pathlib import Path

PROJ = Path(__file__).resolve().parents[3]
if not (PROJ / "Assets").exists():
    sys.exit("Run from Assets/NexusUnity/scripts inside a Unity project (Assets/ not found at " + str(PROJ) + ").")

CYCLES = 20


def run_cmd(args: list[str], timeout: int = 180) -> dict:
    env = os.environ.copy()
    env["UNITY_NO_BANNER"] = "1"
    env["UNITY_NON_INTERACTIVE"] = "1"
    proc = subprocess.run(
        ["unity", "command", "--project-path", str(PROJ), "--json", "--result-only",
         "--timeout", str(timeout), *args],
        capture_output=True,
        text=True,
        env=env,
        timeout=timeout + 20,
    )
    raw = (proc.stdout or proc.stderr or "").strip()
    try:
        return json.loads(raw)
    except json.JSONDecodeError:
        return {"success": False, "parse_error": raw[:800], "returncode": proc.returncode}


def wait_ready(timeout: int = 180) -> bool:
    deadline = time.time() + timeout
    while time.time() < deadline:
        data = run_cmd(["nexus_project_map"], timeout=30)
        if data.get("success") and data.get("project_path"):
            return True
        time.sleep(2)
    return False


def main() -> int:
    recoveries = 0
    hangs = 0
    pending_failures = 0
    cycles = []
    for i in range(CYCLES):
        started = time.perf_counter()
        pending = run_cmd(["nexus_capture_game_view"], timeout=60)
        pending_ok = bool(pending.get("success"))
        reload = run_cmd(["recompile"], timeout=180)
        time.sleep(3)
        ready = wait_ready(180)
        captured = run_cmd(["nexus_capture_game_view"], timeout=60) if ready else {}
        recovered = bool(captured.get("success")) and captured.get("width", 0) > 0
        elapsed = (time.perf_counter() - started) * 1000.0
        if recovered:
            recoveries += 1
        if elapsed > 120_000:
            hangs += 1
        if pending_ok:
            pending_failures += 0
        else:
            pending_failures += 1
        cycles.append({
            "i": i,
            "pending_ok": pending_ok,
            "reload_ok": reload.get("success", True),
            "ready": ready,
            "recovered": recovered,
            "elapsed_ms": elapsed,
        })
        if not ready:
            break

    report = {
        "cycles_requested": CYCLES,
        "cycles_ran": len(cycles),
        "recoveries": recoveries,
        "pending_failures": pending_failures,
        "hangs": hangs,
        "success": recoveries == CYCLES and hangs == 0,
        "cycles": cycles,
    }
    print(json.dumps(report, indent=2))
    Path("stabilize-domain-reload-results.json").write_text(json.dumps(report, indent=2))
    return 0 if report["success"] else 1


if __name__ == "__main__":
    sys.exit(main())
