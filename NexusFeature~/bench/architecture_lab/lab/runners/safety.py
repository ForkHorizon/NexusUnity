"""Process guards. PID 4465 and the harness project are never targets."""

from __future__ import annotations

from lab.errors import SafetyError

PROTECTED_PIDS = {4465}
HARNESS_MARKERS = ("UnityTestForNexus",)


def assert_safe_to_stop(pid: int, project_marker: str, processes: dict[int, dict]) -> None:
    """Refuse a kill unless the process is the lab Editor and not the harness."""
    if pid in PROTECTED_PIDS:
        raise SafetyError(f"refusing to signal protected pid {pid}")
    proc = processes.get(pid)
    if proc is None:
        raise SafetyError(f"pid {pid} is not in the process table")
    command = proc.get("command") or ""
    if "Unity.app" not in command:
        raise SafetyError("refusing to signal a process that is not Unity.app")
    if any(marker in command for marker in HARNESS_MARKERS):
        raise SafetyError("refusing to signal the harness editor")
    if project_marker not in command:
        raise SafetyError("process command does not contain the lab project path")
    if "pgrep" in command:
        raise SafetyError("refusing a pgrep match")
