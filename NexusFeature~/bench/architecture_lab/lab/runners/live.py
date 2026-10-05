"""Real Unity and Codex entry points.

Search code does not import this module. Self-check may read every task split
because it is a fixture check, not an optimizer.
"""

from __future__ import annotations

import hashlib
import json
import shutil
import subprocess
import time
from pathlib import Path
from typing import Any

from lab.adapters.codex_cli import CodexCliAdapter
from lab.adapters.unity_cli import UnityCliBackend, command_body
from lab.candidates.baselines import smoke_hidden_gateway, smoke_official_subset
from lab.catalog.extract import load_catalog
from lab.errors import LabError, SafetyError
from lab.fixtures.observe import observe_state, pristine_match
from lab.fixtures.reference import open_lab
from lab.fixtures.reset import fingerprint, reload_lab_scene
from lab.fixtures.selfcheck import _wait_until_settled, check_task, plant_compile_error
from lab.fixtures.tasks import load_tasks
from lab.runners.matrix import SMOKE_LABEL, run_smoke_matrix
from lab.runners.scratch import DEFAULT_PORT, DEFAULT_PROJECT, LAB_ROOT, PACKAGE_ROOT, neutralize_foreign_hooks
from lab.telemetry.cost import load_profile
from lab.telemetry.store import RunStore

UNITY_CLI = Path("/tmp/unity-cli-beta12/unity")
CATALOG_PATH = LAB_ROOT / "catalog" / "snapshots" / "pipeline-0.8.0-exp.1.json"
EVIDENCE = LAB_ROOT / "evidence"
SMOKE_TASKS = ("search.read_object_name", "search.write_intensity")
WORKSPACE = Path("/tmp/nexus-arch-lab-agent")


def _prompt_for_surface(task: dict[str, Any], surface: dict[str, Any]) -> str:
    """Keep the instruction identical for every candidate.

    Listing model-visible names here dumps a large flat surface into the prompt
    and coaches a small gateway. The tool list is the surface under test.
    `surface` is accepted so callers stay stable and is not read.
    """
    del surface
    return (
        task["prompt"].rstrip()
        + "\nUse only the tools in this session's tool list. "
        + "Do not guess a value a tool can read. Do not use the shell.\n"
    )


def _drop_foreign_hooks(project: Path, backend) -> None:
    removed = neutralize_foreign_hooks(project)
    if removed and not _wait_until_settled(backend.call, time.sleep):
        raise LabError("editor stayed compiling after parking " + ",".join(removed))


def execute_self_check(project: Path = DEFAULT_PROJECT, port: int = DEFAULT_PORT, unity_binary: Path = UNITY_CLI) -> dict[str, Any]:
    backend = _backend(unity_binary, project, port)
    _drop_foreign_hooks(project, backend)
    pristine = _ensure_pristine(project)
    destination = project / "Assets" / "LabFixture"
    rows = []
    for split in ("search", "validation", "holdout"):
        for task in load_tasks(LAB_ROOT / "tasks" / split):
            rows.append(
                check_task(
                    task,
                    backend.call,
                    project=project,
                    pristine=pristine,
                    destination=destination,
                )
            )
    payload = {
        "label": "SELF_CHECK",
        "valid": sum(1 for row in rows if row["status"] == "VALID"),
        "invalid": sum(1 for row in rows if row["status"] == "INVALID"),
        "tasks": rows,
    }
    EVIDENCE.mkdir(parents=True, exist_ok=True)
    (EVIDENCE / "self-check.json").write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    return payload


def execute_smoke(project: Path = DEFAULT_PROJECT, port: int = DEFAULT_PORT, unity_binary: Path = UNITY_CLI, seed: int = 1) -> dict[str, Any]:
    status_path = EVIDENCE / "self-check.json"
    if not status_path.exists():
        raise LabError("self-check has not been written")
    status = json.loads(status_path.read_text(encoding="utf-8"))
    valid = {row["task_id"] for row in status["tasks"] if row["status"] == "VALID"}
    missing = [task_id for task_id in SMOKE_TASKS if task_id not in valid]
    if missing:
        raise LabError(f"smoke refuses tasks that are not VALID: {missing}")
    backend = _backend(unity_binary, project, port)
    _drop_foreign_hooks(project, backend)
    catalog = load_catalog(CATALOG_PATH)
    tasks = [task for task in load_tasks(LAB_ROOT / "tasks" / "search") if task["task_id"] in SMOKE_TASKS]
    candidates = [smoke_official_subset(catalog), smoke_hidden_gateway(catalog)]
    pristine = _ensure_pristine(project)
    destination = project / "Assets" / "LabFixture"
    first = reload_lab_scene(backend.call, project, pristine, destination)
    problems = pristine_match(observe_state(backend.call))
    if problems:
        raise LabError("smoke refused to start; live fixture is " + ",".join(problems))
    expected_fp = first["fingerprint"]
    probe = _environment_probe(backend, project, port, unity_binary, expected_fp)
    if not probe["ready"]:
        raise LabError(f"editor is not ready: {probe}")

    def prepare(task):
        reloaded = reload_lab_scene(backend.call, project, pristine, destination)
        observed = observe_state(backend.call, reload_scene=False)
        issues = pristine_match(observed)
        if reloaded["fingerprint"] != expected_fp or issues:
            return {"ok": False, "fingerprint": reloaded["fingerprint"], "reason": "fixture drift: " + ",".join(issues)}
        if "plant_compile_error" in (task.get("setup") or []):
            plant_compile_error(project)
            if not _wait_until_settled(backend.call, time.sleep):
                return {"ok": False, "fingerprint": expected_fp, "reason": "compile error did not settle"}
        return {"ok": True, "fingerprint": expected_fp}

    def cleanup(task):
        reloaded = reload_lab_scene(backend.call, project, pristine, destination)
        if "plant_compile_error" in (task.get("setup") or []):
            backend.call("recompile", {"focus": False})
            if not _wait_until_settled(backend.call, time.sleep):
                return {"ok": False, "fingerprint": reloaded["fingerprint"], "reason": "cleanup compile did not settle"}
            open_lab(backend.call)
            if not _wait_until_settled(backend.call, time.sleep):
                return {"ok": False, "fingerprint": reloaded["fingerprint"], "reason": "cleanup reopen did not settle"}
        observed = observe_state(backend.call, reload_scene=False)
        issues = pristine_match(observed)
        if reloaded["fingerprint"] != expected_fp or issues:
            return {"ok": False, "fingerprint": reloaded["fingerprint"], "reason": "cleanup drift: " + ",".join(issues)}
        return {"ok": True, "fingerprint": expected_fp}

    evidence = EVIDENCE / "smoke"
    evidence.mkdir(parents=True, exist_ok=True)
    adapter = CodexCliAdapter()

    def codex_run(surface, task, run_id):
        surface_path = evidence / f"{run_id}-surface.json"
        telemetry_path = evidence / f"{run_id}-backend.jsonl"
        surface_path.write_text(json.dumps(surface, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        if surface["candidate_hash"] != surface["candidate"]["architecture_hash"]:
            raise SafetyError("surface hash does not match the candidate")
        WORKSPACE.mkdir(parents=True, exist_ok=True)
        outcome = adapter.run(
            {
                "workspace": str(WORKSPACE),
                "repo_root": str(PACKAGE_ROOT),
                "surface_path": str(surface_path),
                "catalog_path": str(CATALOG_PATH),
                "telemetry_path": str(telemetry_path),
                "mcp_wrapper": str(LAB_ROOT / "bin" / "lab-mcp"),
                "unity_binary": str(unity_binary),
                "project_path": str(project),
                "port": port,
                "model": "gpt-6-luna",
                "reasoning": "low",
                "mechanism": surface["mechanism_actually_used"],
                "timeout_s": int(task["timeout_s"]),
                "prompt": _prompt_for_surface(task, surface),
            }
        )
        # The server truncates this file on start. Read it after the process exits.
        outcome["backend_telemetry"] = _telemetry(telemetry_path)
        (evidence / f"{run_id}-events.json").write_text(json.dumps(outcome.get("events") or [], indent=2) + "\n", encoding="utf-8")
        if outcome.get("prior_events"):
            (evidence / f"{run_id}-prior-events.json").write_text(
                json.dumps(outcome["prior_events"], indent=2) + "\n",
                encoding="utf-8",
            )
        stderr_tail = outcome.get("stderr_tail") or ""
        if stderr_tail:
            (evidence / f"{run_id}-stderr.txt").write_text(stderr_tail, encoding="utf-8")
        return outcome

    store = RunStore(evidence / "runs.sqlite")
    try:
        result = run_smoke_matrix(
            catalog=catalog,
            tasks=tasks,
            candidates=candidates,
            store=store,
            expected_probe=probe,
            probe=lambda: _environment_probe(backend, project, port, unity_binary, expected_fp),
            prepare_task=prepare,
            cleanup_task=cleanup,
            codex_run=codex_run,
            # A write is still unsaved. Reopening here would reload the file and
            # hide a real mutation from the oracle. prepare_task opens the scene.
            observe=lambda task: _observe_task(backend.call, task),
            price_profile=load_profile(LAB_ROOT / "pricing" / "luna6.json"),
            seed=seed,
            experiment_id=f"smoke-s{seed}",
            identity=_identity(project, port, unity_binary, expected_fp, catalog, candidates, probe),
        )
    finally:
        store.close()
    (evidence / "report.md").write_text(result["report"], encoding="utf-8")
    (evidence / "acceptance.json").write_text(json.dumps(result["acceptance"], indent=2) + "\n", encoding="utf-8")
    return {"acceptance": result["acceptance"], "report": result["report"], "rows": result["rows"]}


def _backend(binary: Path, project: Path, port: int) -> UnityCliBackend:
    if not binary.is_file():
        raise SafetyError(f"unity cli missing: {binary}")
    return UnityCliBackend(str(binary), str(project), port, timeout_s=90)


def _ensure_pristine(project: Path) -> Path:
    """Snapshot Assets/LabFixture after the scene exists. Editor scripts stay out."""
    stored = LAB_ROOT / "fixtures" / "pristine"
    scene = project / "Assets" / "LabFixture" / "Scenes" / "Lab.unity"
    if scene.exists() and not (stored / "Scenes" / "Lab.unity").exists():
        if stored.exists():
            shutil.rmtree(stored)
        shutil.copytree(project / "Assets" / "LabFixture", stored)
    if (stored / "Scenes" / "Lab.unity").exists():
        return stored
    source = LAB_ROOT / "fixtures" / "unity"
    if not (source / "LabMarker.cs").exists():
        raise LabError("fixture source is missing LabMarker.cs")
    return source


def _environment_probe(backend: UnityCliBackend, project: Path, port: int, unity_binary: Path, fixture_hash: str) -> dict[str, Any]:
    result = backend.call("editor_status", {})
    body = command_body(result.get("payload")) or {}
    if not isinstance(body, dict):
        body = {}
    reported = _norm(str(body.get("projectPath") or ""))
    expected = _norm(str(project))
    listening = _listener_pid(port)
    ready = (
        result.get("ok")
        and body.get("status") == "ready"
        and not body.get("compiling")
        and reported.endswith(expected)
        and listening not in (None, 4465)
    )
    return {
        "port": port,
        "listener_pid": listening,
        "project_path": expected,
        "pipeline_version": _pipeline_version(project),
        "cli_version": _first_line([str(unity_binary), "--version"]),
        "codex_version": _first_line(["codex", "--version"]),
        "unity_version": body.get("unityVersion"),
        "fixture_hash": fixture_hash,
        "model": "gpt-6-luna",
        "compiling": bool(body.get("compiling")),
        "play_mode": body.get("playMode") == "playing",
        "ready": bool(ready),
        "agent_cwd": str(WORKSPACE),
    }


def _identity(project, port, unity_binary, fixture_hash, catalog, candidates, probe: dict[str, Any] | None = None) -> dict[str, Any]:
    probe = probe or {}
    return {
        "label": SMOKE_LABEL,
        "lab_git": _git_head(),
        "nexus_git": _git_head(),
        "unity_project": str(project),
        "port": port,
        "pipeline_version": _pipeline_version(project),
        "cli_binary": str(unity_binary),
        "cli_version": probe.get("cli_version"),
        "codex_binary": "codex",
        "codex_version": probe.get("codex_version"),
        "unity_version": probe.get("unity_version"),
        "listener_pid": probe.get("listener_pid"),
        "worktree_dirty": _worktree_dirty(),
        "lab_content_hash": _lab_content_hash(),
        "model": "gpt-6-luna",
        "reasoning": "low",
        "fixture_hash": fixture_hash,
        "catalog_hash": catalog.get("catalog_hash"),
        "candidate_hashes": [item["architecture_hash"] for item in candidates],
        "task_ids": list(SMOKE_TASKS),
        "claim": "plumbing only",
    }


def _telemetry(path: Path) -> list[dict[str, Any]]:
    if not path.exists():
        return []
    rows = []
    for line in path.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        try:
            rows.append(json.loads(line))
        except json.JSONDecodeError:
            continue
    return rows


def _pipeline_version(project: Path) -> str:
    manifest = json.loads((project / "Packages" / "manifest.json").read_text(encoding="utf-8"))
    return manifest["dependencies"]["com.unity.pipeline"]


def _listener_pid(port: int) -> int | None:
    proc = subprocess.run(["lsof", "-nP", f"-iTCP:{port}", "-sTCP:LISTEN", "-t"], capture_output=True, text=True, check=False)
    pids = [int(line) for line in proc.stdout.split() if line.isdigit()]
    return pids[0] if pids else None


def _first_line(command: list[str]) -> str:
    proc = subprocess.run(command, capture_output=True, text=True, check=False)
    text = (proc.stdout or proc.stderr or "").strip()
    return text.splitlines()[0] if text else ""


def _observe_task(backend, task: dict[str, Any]) -> dict[str, Any]:
    oracle = task.get("oracle") or {}
    unity_field = oracle.get("unity_field")
    return observe_state(
        backend,
        include_assets=unity_field in {"guid", "error_code", "compilation_failed", "file_text"}
        or bool(oracle.get("asset_name") or oracle.get("text_path")),
        reload_scene=False,
        asset_name=oracle.get("asset_name"),
        text_path=oracle.get("text_path"),
        want_other=unity_field == "other_object_name",
    )


def _git_head() -> str:
    proc = subprocess.run(["git", "-C", str(PACKAGE_ROOT), "rev-parse", "HEAD"], capture_output=True, text=True, check=False)
    return (proc.stdout or "").strip()


def _worktree_dirty() -> bool:
    proc = subprocess.run(
        ["git", "-C", str(PACKAGE_ROOT), "status", "--porcelain"],
        capture_output=True,
        text=True,
        check=False,
    )
    for line in (proc.stdout or "").splitlines():
        if "architecture_lab/evidence/" in line:
            continue
        return True
    return False


def _lab_content_hash() -> str:
    """Hash the lab tree except evidence, so a dirty run is not just HEAD."""
    digest = hashlib.sha256()
    files = [path for path in LAB_ROOT.rglob("*") if path.is_file()]
    for path in sorted(files, key=lambda item: item.relative_to(LAB_ROOT).as_posix()):
        relative = path.relative_to(LAB_ROOT).as_posix()
        if relative.startswith("evidence/") or "__pycache__" in relative or relative.endswith(".pyc"):
            continue
        digest.update(relative.encode("utf-8"))
        digest.update(b"\0")
        digest.update(path.read_bytes())
        digest.update(b"\0")
    return digest.hexdigest()


def _norm(path: str) -> str:
    return path.replace("/private/tmp/", "/tmp/").rstrip("/")


def fingerprint_now(project: Path) -> str:
    return fingerprint(project / "Assets" / "LabFixture")
