"""Environment gate. A failure voids the run before the model starts."""

from __future__ import annotations

from typing import Any

FORBIDDEN_PORTS = {7800}
PROTECTED_PIDS = {4465}


def preflight(expected: dict[str, Any], probe: dict[str, Any]) -> dict[str, Any]:
    failures = []
    port = probe.get("port")
    if port in FORBIDDEN_PORTS:
        failures.append(f"port {port} is reserved for the harness")
    if probe.get("listener_pid") in PROTECTED_PIDS:
        failures.append("listener is protected harness pid 4465")
    if probe.get("project_path") != expected.get("project_path"):
        failures.append("project path mismatch")
    project = str(probe.get("project_path") or "")
    if "UnityTestForNexus" in project:
        failures.append("project is the harness")
    for key in (
        "pipeline_version",
        "cli_version",
        "codex_version",
        "unity_version",
        "fixture_hash",
        "model",
    ):
        if probe.get(key) != expected.get(key):
            failures.append(f"{key} mismatch")
    # The editor does not know the candidate. Compare hashes only when the
    # probe actually loaded one, so two candidates can share one editor probe.
    if probe.get("candidate_hash") and expected.get("candidate_hash") and probe.get("candidate_hash") != expected.get("candidate_hash"):
        failures.append("candidate hash mismatch")
    if probe.get("compiling"):
        failures.append("editor is compiling")
    if probe.get("play_mode") and not expected.get("allow_play_mode"):
        failures.append("editor is in play mode")
    if not probe.get("ready"):
        failures.append("editor is not ready")
    if probe.get("agent_cwd") == expected.get("repo_root"):
        failures.append("agent workspace is the package repository")
    return {"ok": not failures, "failures": failures, "void": bool(failures)}
