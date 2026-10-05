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

from lab.adapters.codex_cli import CODEX_HOME, CodexCliAdapter
from lab.adapters.unity_cli import UnityCliBackend, command_body
from lab.candidates.baselines import all_baselines, smoke_hidden_gateway, smoke_official_subset
from lab.catalog.extract import load_catalog
from lab.errors import LabError, SafetyError
from lab.fixtures.observe import observe_state, pristine_match
from lab.fixtures.reference import open_lab
from lab.fixtures.reset import fingerprint, reload_lab_scene
from lab.fixtures.selfcheck import _wait_until_settled, check_task, plant_compile_error
from lab.fixtures.tasks import load_tasks
from lab.reports.render import render_comparison
from lab.runners.confirm_run import assert_canonical, assert_eligible, promotion_for, run_confirmation
from lab.runners.matrix import SMOKE_LABEL, run_smoke_matrix
from lab.runners.scratch import DEFAULT_PORT, DEFAULT_PROJECT, LAB_ROOT, PACKAGE_ROOT, neutralize_foreign_hooks
from lab.runners.study import run_evolution
from lab.search.budget import STOP_TOKEN_UNKNOWN, next_run_allowed, spend_so_far
from lab.search.holdout import load_search_tasks, load_task_split
from lab.search.promote import freeze
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
    for split in ("search", "validation", "holdout", "confirm_validation", "confirm_holdout"):
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
    """Fresh smoke. The historical database under evidence/smoke is left as it was."""
    tasks = _require_valid(SMOKE_TASKS)
    session = _open_session(project, port, unity_binary, EVIDENCE / "smoke-final", model="gpt-6-luna", reasoning="low")
    candidates = [smoke_official_subset(session["catalog"]), smoke_hidden_gateway(session["catalog"])]
    store = RunStore(session["evidence"] / "runs.sqlite")
    try:
        result = run_smoke_matrix(
            catalog=session["catalog"],
            tasks=tasks,
            candidates=candidates,
            store=store,
            expected_probe=session["probe0"],
            probe=session["probe"],
            prepare_task=session["prepare"],
            cleanup_task=session["cleanup"],
            codex_run=session["codex_run"],
            observe=session["observe"],
            price_profile=load_profile(LAB_ROOT / "pricing" / "luna6.json"),
            seed=seed,
            experiment_id=f"smoke-final-s{seed}",
            identity=_identity(
                project,
                port,
                unity_binary,
                session["expected_fp"],
                session["catalog"],
                candidates,
                session["probe0"],
                task_ids=tuple(task["task_id"] for task in tasks),
            ),
            order_seed=seed,
        )
    finally:
        store.close()
    (session["evidence"] / "report.md").write_text(result["report"], encoding="utf-8")
    (session["evidence"] / "acceptance.json").write_text(json.dumps(result["acceptance"], indent=2) + "\n", encoding="utf-8")
    (session["evidence"] / "execution-order.json").write_text(
        json.dumps(result.get("execution_order") or [], indent=2) + "\n",
        encoding="utf-8",
    )
    return {
        "acceptance": result["acceptance"],
        "report": result["report"],
        "rows": result["rows"],
        "experiment_id": f"smoke-final-s{seed}",
        "evidence": str(session["evidence"]),
    }


def execute_evolution(
    *,
    population_limit: int,
    generations: int,
    max_runs: int,
    max_uncached_tokens: int,
    seed: int,
    label: str,
    candidates_name: str,
    tasks_name: str,
    resume: bool,
    experiment_id: str,
    model: str = "gpt-6-luna",
    reasoning: str = "low",
    project: Path = DEFAULT_PROJECT,
    port: int = DEFAULT_PORT,
    unity_binary: Path = UNITY_CLI,
) -> dict[str, Any]:
    """One real search loop. Tasks come from the search split only."""
    catalog = load_catalog(CATALOG_PATH)
    if tasks_name == "smoke":
        tasks = _require_valid(SMOKE_TASKS)
    elif tasks_name == "search":
        tasks = _require_valid(tuple(task["task_id"] for task in load_search_tasks(LAB_ROOT / "tasks")))
    else:
        raise LabError(f"unknown task set {tasks_name}")
    if candidates_name == "smoke":
        population = [smoke_official_subset(catalog), smoke_hidden_gateway(catalog)]
    elif candidates_name == "baselines":
        population = all_baselines(catalog)
    else:
        raise LabError(f"unknown candidate set {candidates_name}")
    population = population[:population_limit]
    evidence = EVIDENCE / "evolution" / experiment_id
    session = _open_session(project, port, unity_binary, evidence, model=model, reasoning=reasoning)
    identity = _identity(
        project,
        port,
        unity_binary,
        session["expected_fp"],
        catalog,
        population,
        session["probe0"],
        label=label,
        task_ids=[task["task_id"] for task in tasks],
        model=model,
        reasoning=reasoning,
    )
    store = RunStore(evidence / "runs.sqlite")
    try:
        result = run_evolution(
            catalog=catalog,
            tasks=tasks,
            population=population,
            store=store,
            seed=seed,
            generations=generations,
            max_runs=max_runs,
            max_uncached_tokens=max_uncached_tokens,
            experiment_id=experiment_id,
            label=label,
            population_limit=population_limit,
            runner_kwargs={
                "expected_probe": session["probe0"],
                "probe": session["probe"],
                "prepare_task": session["prepare"],
                "cleanup_task": session["cleanup"],
                "codex_run": session["codex_run"],
                "observe": session["observe"],
                "price_profile": load_profile(LAB_ROOT / "pricing" / "luna6.json"),
            },
            identity=identity,
            resume=resume,
        )
        rows = []
        for entry in result.get("lineage") or []:
            for row in entry.get("rows") or []:
                rows.append(row)
        report = render_comparison(rows, label=label)
        report += _lineage_text(result)
    finally:
        store.close()
    (evidence / "report.md").write_text(report, encoding="utf-8")
    (evidence / "result.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    return {"result": result, "report": report, "evidence": str(evidence)}


def execute_confirmation(
    *,
    candidate_hash: str,
    include_holdout: bool,
    integration: bool,
    seed: int,
    max_runs: int,
    max_uncached_tokens: int,
    model: str = "gpt-6-luna",
    reasoning: str = "low",
    project: Path = DEFAULT_PROJECT,
    port: int = DEFAULT_PORT,
    unity_binary: Path = UNITY_CLI,
) -> dict[str, Any]:
    """Fresh validation, then holdout only when validation passes.

    Integration uses dedicated tasks and does not open the research holdout.
    """
    catalog = load_catalog(CATALOG_PATH)
    candidate = _load_frozen_candidate(candidate_hash, catalog)
    assert_canonical(candidate, candidate_hash, catalog)
    assert_eligible(candidate, integration=integration)
    if not integration and candidate.get("state") == "FINALIST":
        candidate = freeze(candidate)
    label = "CONFIRM_PIPELINE_INTEGRATION_ONLY" if integration else "CONFIRMATION"
    if integration:
        validation_tasks = _require_valid(("confirm_validation.read_object_name",))

        def holdout_loader():
            # Read only after validation has passed and another run is still allowed.
            return _require_valid(("confirm_holdout.write_intensity",))
    else:
        validation_tasks = _require_valid(
            tuple(task["task_id"] for task in load_task_split(LAB_ROOT / "tasks", "validation", confirmation=True))
        )

        def holdout_loader():
            return _require_valid(
                tuple(task["task_id"] for task in load_task_split(LAB_ROOT / "tasks", "holdout", confirmation=True))
            )

    evidence = EVIDENCE / ("confirm-integration" if integration else "confirm") / candidate_hash[:12]
    experiment_id = f"{'confirm-integration' if integration else 'confirm'}-{candidate_hash[:12]}"
    session = _open_session(project, port, unity_binary, evidence, model=model, reasoning=reasoning)
    identity = _identity(
        project,
        port,
        unity_binary,
        session["expected_fp"],
        catalog,
        [candidate],
        session["probe0"],
        label=label,
        task_ids=[task["task_id"] for task in validation_tasks],
        model=model,
        reasoning=reasoning,
    )
    def run_batch(batch_tasks, split):
        del split
        store = RunStore(evidence / "runs.sqlite")
        try:
            runs_used, uncached_used = spend_so_far(store, experiment_id)
            # Unknown spend is not zero. Passing the cap makes the runner
            # refuse the next model call, then the stop reason is corrected.
            tokens_unknown = uncached_used is None
            batch = run_smoke_matrix(
                catalog=catalog,
                tasks=batch_tasks,
                candidates=[candidate],
                store=store,
                expected_probe=session["probe0"],
                probe=session["probe"],
                prepare_task=session["prepare"],
                cleanup_task=session["cleanup"],
                codex_run=session["codex_run"],
                observe=session["observe"],
                price_profile=load_profile(LAB_ROOT / "pricing" / "luna6.json"),
                seed=seed,
                experiment_id=experiment_id,
                identity=identity,
                label=label,
                budget={"max_runs": max_runs, "max_uncached_tokens": max_uncached_tokens},
                skip_ids=store.started_run_ids(experiment_id),
                generation=0,
                order_seed=seed,
                runs_used=runs_used,
                uncached_used=max_uncached_tokens if tokens_unknown else uncached_used,
            )
            if tokens_unknown:
                batch["stopped"] = STOP_TOKEN_UNKNOWN
        finally:
            store.close()
        return batch

    def holdout_block() -> str | None:
        """Decide from stored spend. Do not open holdout files to make this decision."""
        store = RunStore(evidence / "runs.sqlite")
        try:
            runs_used, uncached_used = spend_so_far(store, experiment_id)
        finally:
            store.close()
        if uncached_used is None:
            return STOP_TOKEN_UNKNOWN
        return next_run_allowed(
            runs_used=runs_used,
            max_runs=max_runs,
            uncached_used=uncached_used,
            max_uncached_tokens=max_uncached_tokens,
        )

    result = run_confirmation(
        candidate=candidate,
        expected_hash=candidate_hash,
        validation_tasks=validation_tasks,
        holdout_loader=holdout_loader,
        run_batch=run_batch,
        include_holdout=include_holdout,
        holdout_block=holdout_block,
    )
    result["promotion"] = promotion_for(
        result["status"],
        integration=integration,
        state=candidate.get("state"),
    )
    result["label"] = label
    rows = list(result.get("validation_rows") or [])
    report = render_comparison(rows, label=label)
    report += f"\nStatus: `{result['status']}`\nHoldout executed: {result['holdout_executed']}\n"
    (evidence / "report.md").write_text(report, encoding="utf-8")
    public = {key: value for key, value in result.items() if key not in {"validation_rows", "holdout_rows"}}
    (evidence / "result.json").write_text(json.dumps(public, indent=2) + "\n", encoding="utf-8")
    return {"result": result, "report": report, "evidence": str(evidence)}


def _open_session(project: Path, port: int, unity_binary: Path, evidence: Path, *, model: str, reasoning: str) -> dict[str, Any]:
    backend = _backend(unity_binary, project, port)
    _drop_foreign_hooks(project, backend)
    catalog = load_catalog(CATALOG_PATH)
    pristine = _ensure_pristine(project)
    destination = project / "Assets" / "LabFixture"
    first = reload_lab_scene(backend.call, project, pristine, destination)
    problems = pristine_match(observe_state(backend.call))
    if problems:
        raise LabError("live fixture is " + ",".join(problems))
    expected_fp = first["fingerprint"]
    probe0 = _environment_probe(backend, project, port, unity_binary, expected_fp, model=model)
    if not probe0["ready"]:
        raise LabError(f"editor is not ready: {probe0}")
    evidence.mkdir(parents=True, exist_ok=True)
    adapter = CodexCliAdapter()

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

    def codex_run(surface, task, run_id):
        surface_path = evidence / f"{run_id}-surface.json"
        telemetry_path = evidence / f"{run_id}-backend.jsonl"
        surface_path.write_text(json.dumps({key: value for key, value in surface.items() if key != "candidate"}, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        if surface["candidate_hash"] != surface["candidate"]["architecture_hash"]:
            raise SafetyError("surface hash does not match the candidate")
        if surface.get("surface_problem"):
            raise SafetyError(surface["surface_problem"])
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
                "model": model,
                "reasoning": reasoning,
                "mechanism": surface["mechanism_actually_used"],
                "timeout_s": int(task["timeout_s"]),
                "prompt": _prompt_for_surface(task, surface),
            }
        )
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

    return {
        "catalog": catalog,
        "evidence": evidence,
        "expected_fp": expected_fp,
        "probe0": probe0,
        "probe": lambda: _environment_probe(backend, project, port, unity_binary, expected_fp, model=model),
        "prepare": prepare,
        "cleanup": cleanup,
        "observe": lambda task: _observe_task(backend.call, task),
        "codex_run": codex_run,
    }


def _require_valid(task_ids: tuple[str, ...] | list[str]) -> list[dict[str, Any]]:
    status_path = EVIDENCE / "self-check.json"
    if not status_path.exists():
        raise LabError("self-check has not been written")
    status = json.loads(status_path.read_text(encoding="utf-8"))
    valid = {row["task_id"]: row for row in status["tasks"] if row["status"] == "VALID"}
    missing = [task_id for task_id in task_ids if task_id not in valid]
    if missing:
        raise LabError(f"refusing tasks that are not VALID: {missing}")
    by_id = {}
    splits = set()
    for task_id in task_ids:
        prefix = task_id.split(".", 1)[0]
        splits.add(prefix)
    for split in splits:
        directory = LAB_ROOT / "tasks" / split
        if not directory.is_dir():
            raise LabError(f"no task directory for {split}")
        for task in load_tasks(directory):
            by_id[task["task_id"]] = task
    return [by_id[task_id] for task_id in task_ids]


def _load_frozen_candidate(candidate_hash: str, catalog: dict[str, Any]) -> dict[str, Any]:
    """Load the architecture that hashes to the request. Do not invent a replacement."""
    from lab.canonical import sha256_json

    for candidate in (smoke_official_subset(catalog), smoke_hidden_gateway(catalog), *all_baselines(catalog)):
        if candidate["architecture_hash"] == candidate_hash:
            return candidate
    if EVIDENCE.exists():
        for path in sorted(EVIDENCE.rglob("runs.sqlite")):
            store = RunStore(path)
            try:
                stored = store.candidate_manifest(candidate_hash)
            finally:
                store.close()
            if stored and sha256_json(stored.get("architecture") or {}) == candidate_hash:
                return stored
    raise LabError(f"no frozen candidate for {candidate_hash}")


def _lineage_text(result: dict[str, Any]) -> str:
    lines = ["", f"Stop: `{result.get('stop_reason')}`", "", "## Lineage", ""]
    for entry in result.get("lineage") or []:
        lines.append(f"### Generation {entry.get('generation')}")
        for digest in entry.get("candidate_hashes") or []:
            lines.append(f"- candidate `{digest}`")
        for child in entry.get("children") or []:
            lines.append(f"- child `{child.get('candidate_hash')}` parent `{child.get('parent_hash')}`")
        for skipped in entry.get("duplicates") or []:
            lines.append(f"- DUPLICATE_CANDIDATE_SKIPPED `{skipped.get('candidate_hash')}`")
        lines.append("")
    return "\n".join(lines) + "\n"


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


def _environment_probe(backend: UnityCliBackend, project: Path, port: int, unity_binary: Path, fixture_hash: str, model: str = "gpt-6-luna") -> dict[str, Any]:
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
        "model": model,
        "compiling": bool(body.get("compiling")),
        "play_mode": body.get("playMode") == "playing",
        "ready": bool(ready),
        "agent_cwd": str(WORKSPACE),
    }


def _identity(project, port, unity_binary, fixture_hash, catalog, candidates, probe: dict[str, Any] | None = None, *, label: str = SMOKE_LABEL, task_ids: tuple[str, ...] | list[str] | None = None, model: str = "gpt-6-luna", reasoning: str = "low") -> dict[str, Any]:
    probe = probe or {}
    return {
        "label": label,
        "lab_git": _git_head(),
        "nexus_git": _git_head(),
        "nexus_base_commit": _merge_base("rework2/S-02"),
        "unity_project": str(project),
        "port": port,
        "pipeline_version": _pipeline_version(project),
        "cli_binary": str(unity_binary),
        "cli_version": probe.get("cli_version"),
        "codex_binary": "codex",
        "codex_version": probe.get("codex_version"),
        "codex_home": str(CODEX_HOME),
        "unity_version": probe.get("unity_version"),
        "listener_pid": probe.get("listener_pid"),
        "worktree_dirty": _worktree_dirty(),
        "lab_content_hash": _lab_content_hash(),
        "model": model,
        "reasoning": reasoning,
        "fixture_hash": fixture_hash,
        "catalog_hash": catalog.get("catalog_hash"),
        "candidate_hashes": [item["architecture_hash"] for item in candidates],
        "task_ids": list(task_ids if task_ids is not None else SMOKE_TASKS),
        "claim": "plumbing only" if label == SMOKE_LABEL else "closed loop measurement",
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


def _merge_base(branch: str) -> str:
    proc = subprocess.run(
        ["git", "-C", str(PACKAGE_ROOT), "merge-base", "HEAD", branch],
        capture_output=True,
        text=True,
        check=False,
    )
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
