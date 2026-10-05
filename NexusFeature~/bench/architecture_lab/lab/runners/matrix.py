"""Smoke matrix.

A pre-run gate failure is a void: the model is not started and the run is not
scored. Once the model has started, the run stays in the store.
"""

from __future__ import annotations

import random
import time
from typing import Any

from lab.canonical import sha256_json
from lab.candidates.manifest import coverage_report
from lab.fixtures.observe import pristine_match
from lab.fixtures.oracles import evaluate
from lab.reports.render import render_comparison
from lab.runners.preflight import preflight
from lab.search.budget import STOP_TOKEN_UNKNOWN, next_run_allowed
from lab.search.feasibility import classify_candidate, per_success
from lab.surfaces.generate import generate_surface, surface_problem, surfaces_differ
from lab.telemetry.calls import classify_call, summarize_calls
from lab.telemetry.cost import estimate_cost

SMOKE_LABEL = "SMOKE_ONLY"
_UNMEASURED = ("ts", "duration_ms", "result_bytes", "visible_bytes")


def run_smoke_matrix(
    *,
    catalog: dict[str, Any],
    tasks: list[dict[str, Any]],
    candidates: list[dict[str, Any]],
    store,
    expected_probe: dict[str, Any],
    probe,
    prepare_task,
    codex_run,
    observe,
    price_profile: dict[str, Any],
    seed: int,
    experiment_id: str,
    identity: dict[str, Any],
    cleanup_task=None,
    label: str | None = None,
    budget: dict[str, Any] | None = None,
    skip_ids: set[str] | None = None,
    generation: int = 0,
    order_seed: int | None = None,
    runs_used: int = 0,
    uncached_used: int = 0,
) -> dict[str, Any]:
    label = label or SMOKE_LABEL
    cleanup_task = cleanup_task or prepare_task
    skip_ids = set(skip_ids or ())
    surfaces = []
    for candidate in candidates:
        surface = generate_surface(candidate, catalog)
        surface["candidate"] = candidate
        surface["substituted"] = False
        surface["surface_problem"] = _hash_problem(candidate) or surface_problem(surface, candidate)
        surfaces.append(surface)
        store.add_candidate(candidate, state=str(candidate.get("state") or "SCREENING"))
    identity = dict(identity or {})
    identity["label"] = label
    identity["recorded_phase"] = "before_model"
    identity["task_set_hash"] = identity.get("task_set_hash") or _task_set_hash(tasks)
    identity["surface_hashes"] = {item["candidate_hash"]: item.get("surface_hash") for item in surfaces}
    identity["candidate_hashes"] = [item["candidate_hash"] for item in surfaces]
    identity["generation"] = generation
    store.add_experiment(experiment_id, label, identity)
    pairs = [(surface, task) for surface in surfaces for task in tasks]
    if order_seed is not None:
        random.Random(order_seed + generation * 1009).shuffle(pairs)
    records = []
    execution_order = []
    stopped = None
    for surface, task in pairs:
        candidate = surface["candidate"]
        run_id = f"{experiment_id}-{candidate['candidate_id']}-{task['task_id']}-s{seed}"
        if run_id in skip_ids:
            previous = store.load_record(run_id)
            if previous and previous.get("model_started"):
                records.append(previous)
                execution_order.append(
                    {
                        "order": len(execution_order),
                        "candidate_hash": candidate["architecture_hash"],
                        "task_id": task["task_id"],
                        "generation": generation,
                        "skipped": True,
                    }
                )
                continue
        if budget is not None:
            stopped = next_run_allowed(
                runs_used=runs_used,
                max_runs=int(budget["max_runs"]),
                uncached_used=uncached_used,
                max_uncached_tokens=int(budget["max_uncached_tokens"]),
            )
            if stopped:
                break
        record = _one_run(
            candidate=candidate,
            surface=surface,
            task=task,
            store=store,
            expected_probe=expected_probe,
            probe=probe,
            prepare_task=prepare_task,
            cleanup_task=cleanup_task,
            codex_run=codex_run,
            observe=observe,
            price_profile=price_profile,
            seed=seed,
            experiment_id=experiment_id,
            label=label,
            identity=identity,
            generation=generation,
            order_index=len(execution_order),
        )
        records.append(record)
        execution_order.append(
            {
                "order": len(execution_order),
                "candidate_hash": candidate["architecture_hash"],
                "task_id": task["task_id"],
                "generation": generation,
                "skipped": False,
                "model_started": bool(record.get("model_started")),
            }
        )
        if record.get("model_started"):
            runs_used += 1
            spent = _uncached(record.get("usage") or {})
            if spent is None:
                stopped = STOP_TOKEN_UNKNOWN
                break
            uncached_used += spent
            if budget is not None:
                # The run that crosses the cap is kept. No later run is planned.
                stopped = next_run_allowed(
                    runs_used=runs_used,
                    max_runs=int(budget["max_runs"]),
                    uncached_used=uncached_used,
                    max_uncached_tokens=int(budget["max_uncached_tokens"]),
                )
                if stopped:
                    break
    rows = _rows(records, tasks, catalog)
    report = render_comparison(rows, label=label)
    acceptance = smoke_acceptance(records, surfaces, report, store) if label == SMOKE_LABEL else {"ok": None, "failures": [], "skipped": True}
    return {
        "label": label,
        "surfaces": surfaces,
        "runs": records,
        "rows": rows,
        "report": report,
        "acceptance": acceptance,
        "executed_search": False,
        "stopped": stopped,
        "execution_order": execution_order,
        "runs_used": runs_used,
        "uncached_used": uncached_used,
        "generation": generation,
    }


_IDENTITY_KEYS = (
    "lab_git",
    "nexus_base_commit",
    "candidate_hash",
    "surface_hash",
    "fixture_hash",
    "task_set_hash",
    "unity_version",
    "pipeline_version",
    "cli_version",
    "codex_version",
    "model",
    "reasoning",
    "port",
    "unity_project",
    "mechanism_actually_used",
)


def smoke_acceptance(records: list[dict[str, Any]], surfaces: list[dict[str, Any]], report: str, store=None) -> dict[str, Any]:
    """Plumbing checks. Passing them does not rank a candidate."""
    failures = []
    if len(surfaces) < 2 or not surfaces_differ(surfaces[0], surfaces[1]):
        failures.append("surfaces do not differ")
    if len({item["candidate_hash"] for item in surfaces}) != len(surfaces):
        failures.append("candidate hashes collided")
    if SMOKE_LABEL not in report:
        failures.append("report is not labelled SMOKE_ONLY")
    started = [row for row in records if row["model_started"]]
    if not started:
        failures.append("model never started")
    for row in started:
        if row["usage"].get("status") != "MEASURED":
            failures.append(f"{row['run_id']} usage is not MEASURED")
        if not row["events_saved"]:
            failures.append(f"{row['run_id']} has no raw events")
        if row["model"] != "gpt-6-luna":
            failures.append(f"{row['run_id']} model is {row['model']}")
        if row["status"] in {"surface_bypass", "mechanism_mismatch"}:
            failures.append(f"{row['run_id']} was not the candidate surface ({row['status']})")
        if not row["oracle_evaluated"]:
            failures.append(f"{row['run_id']} oracle did not see Unity state")
    by_candidate: dict[str, list[dict[str, Any]]] = {}
    for row in started:
        by_candidate.setdefault(row["candidate_id"], []).append(row)
    for candidate_id, rows in by_candidate.items():
        if not any(row["unity_calls"] for row in rows):
            failures.append(f"{candidate_id} never reached Unity")
    if any(not row["cleanup_ok"] for row in started):
        failures.append("fixture did not reset after a started run")
    fingerprints = [row["fixture_hash"] for row in records if row.get("fixture_hash")]
    if fingerprints and len(set(fingerprints)) != 1:
        failures.append("fixture hash changed between runs")
    if any(row["label"] != SMOKE_LABEL for row in records):
        failures.append("a run escaped the SMOKE_ONLY label")
    for phrase in ("is better", "winner", "fewer tokens than", "confirmed architecture"):
        if phrase in report:
            failures.append(f"report contains {phrase}")
    if store is not None:
        for row in started:
            snapshots = [item for item in store.run_identities(row["run_id"]) if item["phase"] == "before_model"]
            if not snapshots:
                failures.append(f"{row['run_id']} has no before_model identity")
                continue
            identity = snapshots[0]["identity"]
            for key in _IDENTITY_KEYS:
                if identity.get(key) in (None, "", []):
                    failures.append(f"{row['run_id']} identity missing {key}")
            names = identity.get("visible_tool_names")
            if not names:
                failures.append(f"{row['run_id']} identity has no visible tools")
            for call in row.get("tool_calls") or []:
                missing = [key for key in _UNMEASURED if call.get(key) is None]
                if missing and not str(call.get("error") or "").startswith("unmeasured:"):
                    failures.append(f"{row['run_id']} left {','.join(missing)} null without a reason")
    return {"ok": not failures, "failures": failures}


def _one_run(*, candidate, surface, task, store, expected_probe, probe, prepare_task, cleanup_task, codex_run, observe, price_profile, seed, experiment_id, label=SMOKE_LABEL, identity=None, generation=0, order_index=0) -> dict[str, Any]:
    run_id = f"{experiment_id}-{candidate['candidate_id']}-{task['task_id']}-s{seed}"
    identity = dict(identity or {})
    base = {
        "run_id": run_id,
        "experiment_id": experiment_id,
        "candidate_id": candidate["candidate_id"],
        "candidate_hash": candidate["architecture_hash"],
        "surface_hash": surface.get("surface_hash"),
        "task_id": task["task_id"],
        "split": task["split"],
        "seed": seed,
        "generation": generation,
        "label": label,
        "model": identity.get("model") or expected_probe.get("model"),
        "fixture_hash": identity.get("fixture_hash"),
        "model_started": False,
        "events_saved": False,
        "unity_calls": 0,
        "oracle_evaluated": False,
        "cleanup_ok": False,
        "usage": {"status": "MISSING"},
        "call_summary": {},
        "candidate": candidate,
    }
    problem = surface.get("surface_problem")
    if problem:
        row = _void(base, problem)
        _note(store, run_id, "void", identity, candidate, surface, task, generation, order_index, problem)
        store.add_run(row)
        return row
    live = probe()
    gate = preflight(expected_probe, live)
    prepared = {"ok": False, "fingerprint": None, "reason": "not prepared"}
    if gate["ok"]:
        prepared = prepare_task(task)
    base["fixture_hash"] = prepared.get("fingerprint") or identity.get("fixture_hash")
    if not gate["ok"] or not prepared.get("ok"):
        reason = "; ".join(gate["failures"]) if not gate["ok"] else prepared.get("reason")
        row = _void(base, reason)
        _note(store, run_id, "void", identity, candidate, surface, task, generation, order_index, reason, live)
        store.add_run(row)
        return row

    _note(store, run_id, "before_model", identity, candidate, surface, task, generation, order_index, None, live, prepared.get("fingerprint"))
    started = time.monotonic()
    outcome = codex_run(surface, task, run_id)
    wall_ms = int((time.monotonic() - started) * 1000)
    observation = observe(task)
    verdict = evaluate(task, outcome.get("final_message") or "", observation)
    calls, summary = _annotate_calls(surface, outcome, task)
    calls = [_mark_unmeasured(call) for call in calls]
    # A matching sentence without an acceptable Unity call is a guess.
    # A foreign tool or a client mechanism substitution is not that candidate.
    if outcome.get("surface_bypass") or outcome.get("mechanism_mismatch") or not _acceptable_backend_call(calls, task):
        verdict = {
            **verdict,
            "oracle_success": False,
            "false_success": bool(verdict.get("model_claimed_success")),
        }
    usage = dict(outcome.get("usage") or {"status": "MISSING"})
    usage["cost"] = estimate_cost(usage, price_profile)
    if outcome.get("surface_bypass"):
        status = "surface_bypass"
    elif outcome.get("mechanism_mismatch"):
        status = "mechanism_mismatch"
    else:
        status = outcome.get("status") or "completed"
    cleanup = cleanup_task(task)
    after = observe(task)
    cleanup_ok = bool(cleanup.get("ok")) and not pristine_match(after) and cleanup.get("fingerprint") == prepared.get("fingerprint")
    if not cleanup_ok:
        # The arm may have edited Unity and then failed to restore it.
        # Keeping the success would rank the arm that dirtied the fixture.
        verdict = {
            **verdict,
            "oracle_success": False,
            "false_success": bool(verdict.get("model_claimed_success")),
        }
    unity_field = task["oracle"]["unity_field"]
    row = {
        **base,
        "status": status,
        "oracle_success": verdict["oracle_success"],
        "model_claimed_success": verdict["model_claimed_success"],
        "false_success": verdict["false_success"],
        "void_reason": None if cleanup_ok else "cleanup fingerprint or live state mismatch",
        "wall_ms": wall_ms,
        "events": outcome.get("events") or [],
        "tool_calls": calls,
        "usage": usage,
        "model_started": True,
        "events_saved": bool(outcome.get("events")),
        "unity_calls": sum(1 for call in calls if call.get("backend_reached")),
        "oracle_evaluated": unity_field in observation and observation.get("open_ok") is True,
        "cleanup_ok": cleanup_ok,
        "call_summary": summary,
        "scored": True,
    }
    store.add_run(row)
    return row


_LAB_SERVER = "architecture_lab"


def _acceptable_backend_call(calls: list[dict[str, Any]], task: dict[str, Any]) -> bool:
    acceptable = set(task.get("acceptable_capabilities") or [])
    if not acceptable:
        return False
    return any(bool(call.get("backend_reached")) and call.get("capability_id") in acceptable for call in calls)


def _lab_tool_call(raw: dict[str, Any]) -> bool:
    server = raw.get("server")
    return server in (None, "", _LAB_SERVER)


def _telemetry_reached(line: dict[str, Any]) -> bool:
    """A paired line counts only when that call itself ran Unity.

    Older lines omitted the flag and were written only after a backend call.
    A zero-step line sets the flag false so it cannot donate the next call's
    success to a miss.
    """
    if "backend_reached" in line:
        return bool(line["backend_reached"])
    steps = line.get("step_count")
    if steps is not None:
        return int(steps) > 0
    return True


def _call_field(raw: dict[str, Any], reached: dict[str, Any] | None, key: str) -> Any:
    if raw.get(key) is not None:
        return raw.get(key)
    if reached is not None:
        return reached.get(key)
    return None


def _annotate_calls(surface: dict[str, Any], outcome: dict[str, Any], task: dict[str, Any]) -> tuple[list[dict[str, Any]], dict[str, int]]:
    telemetry = list(outcome.get("backend_telemetry") or [])
    cursor = 0
    calls = []
    for raw in outcome.get("tool_calls") or []:
        classified = classify_call(surface, raw)
        reached = None
        # Every lab MCP call writes one line, including a miss. Consuming a
        # line only for calls that look like tools lets a miss steal the next
        # call's Unity result.
        if _lab_tool_call(raw) and cursor < len(telemetry):
            reached = telemetry[cursor]
            cursor += 1
        backend_reached = _telemetry_reached(reached) if reached is not None else False
        calls.append(
            {
                "ts": _call_field(raw, reached, "ts"),
                "exposed_name": classified["exposed_name"],
                "capability_id": classified["capability_id"],
                "backend_command": classified["backend_command"],
                "arguments": raw.get("arguments") or {},
                "ok": bool(reached.get("ok")) if reached else False,
                "backend_reached": backend_reached,
                "retry": bool(raw.get("retry")),
                "error": None if backend_reached and reached and reached.get("ok") else ("backend telemetry missing" if classified["kind"] == "tool" and not backend_reached else None),
                "result_bytes": _call_field(raw, reached, "result_bytes"),
                "visible_bytes": raw.get("visible_bytes"),
                "duration_ms": _call_field(raw, reached, "duration_ms"),
            }
        )
    return calls, summarize_calls(surface, calls, set(task.get("acceptable_capabilities") or []))


def _rows(records: list[dict[str, Any]], tasks: list[dict[str, Any]], catalog: dict[str, Any]) -> list[dict[str, Any]]:
    task_by_id = {task["task_id"]: task for task in tasks}
    tainted = _experiment_taint(records)
    grouped: dict[str, list[dict[str, Any]]] = {}
    for record in records:
        grouped.setdefault(record["candidate_hash"], []).append(record)
    rows = []
    for candidate_hash, group in grouped.items():
        scored = [row for row in group if row.get("scored")]
        # Voids stay in the denominator. Dropping them would make a missing
        # write task look like a perfect read-only candidate.
        successes = sum(1 for row in group if row.get("scored") and row.get("oracle_success"))
        uncached = _sum_usage(scored, "uncached_input_tokens")
        total = _sum_usage(scored, "total_tokens")
        wrong = sum(int(row["call_summary"].get("wrong_tool_calls") or 0) for row in scored)
        tool_calls = sum(int(row["call_summary"].get("tool_calls") or 0) for row in scored)
        wall = _sum_optional_int(scored, "wall_ms")
        false_successes = sum(1 for row in group if row.get("false_success"))
        bypasses = sum(1 for row in group if row.get("status") == "surface_bypass")
        mismatches = sum(1 for row in group if row.get("status") == "mechanism_mismatch")
        rates: dict[str, float] = {}
        buckets: dict[str, list[bool]] = {}
        for row in group:
            category = task_by_id[row["task_id"]]["category"]
            buckets.setdefault(category, []).append(bool(row.get("scored") and row.get("oracle_success")))
        for category, values in buckets.items():
            rates[category] = sum(values) / len(values)
        candidate = group[0].get("candidate")
        scheduled_tasks = [task_by_id[row["task_id"]] for row in group]
        coverage = coverage_report(candidate, scheduled_tasks, catalog) if candidate else {"coverage": None}
        usage_status = "MEASURED" if group and all(row["usage"].get("status") == "MEASURED" for row in scored) and len(scored) == len(group) else "MISSING"
        verdict = classify_candidate(
            {
                "coverage": coverage.get("coverage"),
                "attempted": len(group),
                "oracle_successes": successes,
                "category_rates": rates,
                "usage_status": usage_status,
                "surface_bypasses": bypasses,
                "mechanism_mismatches": mismatches,
            },
            {
                "min_coverage": 1.0,
                "min_semantic_success": 1.0,
                "category_floors": {category: 1.0 for category in rates},
                "require_measured_usage": True,
            },
        ) if scored else {"feasible": False, "semantic_success_rate": 0.0, "reasons": ["no scored runs"]}
        reasons = list(verdict.get("reasons") or [])
        if tainted:
            # A dirty fixture or a substituted mechanism poisons every arm.
            # The clean arm must not become the winner by surviving the mess.
            verdict["feasible"] = False
            for reason in tainted:
                if reason not in reasons:
                    reasons.append(reason)
        rows.append(
            {
                "candidate_id": group[0]["candidate_id"],
                "candidate_hash": candidate_hash,
                "semantic_success_rate": verdict["semantic_success_rate"],
                "false_successes": false_successes,
                "uncached_tokens_per_success": per_success(uncached, successes),
                "total_tokens_per_success": per_success(total, successes),
                "wrong_tool_calls": wrong,
                "tool_calls_per_success": per_success(tool_calls, successes),
                "wall_ms_per_success": per_success(wall, successes),
                "feasible": verdict["feasible"],
                "reasons": reasons,
                "model_started_any": any(row.get("model_started") for row in group),
                "oracle_successes": successes,
                "attempted": len(group),
                "coverage": coverage.get("coverage"),
                "category_rates": rates,
                "usage_status": usage_status,
            }
        )
    return rows


def _task_set_hash(tasks: list[dict[str, Any]]) -> str:
    payload = [
        {
            "task_id": task.get("task_id"),
            "split": task.get("split"),
            "prompt": task.get("prompt"),
            "oracle": task.get("oracle"),
        }
        for task in tasks
    ]
    return sha256_json(payload)


def _hash_problem(candidate: dict[str, Any]) -> str | None:
    """A stored hash that does not match the architecture body is not that candidate."""
    architecture = candidate.get("architecture")
    if not isinstance(architecture, dict):
        return "candidate has no architecture body"
    if sha256_json(architecture) != candidate.get("architecture_hash"):
        return "architecture hash does not match canonical content"
    return None


def _sum_optional_int(rows: list[dict[str, Any]], key: str) -> int | None:
    if not rows:
        return None
    values = [row.get(key) for row in rows]
    if any(isinstance(value, bool) or not isinstance(value, int) for value in values):
        return None
    return sum(values)


def _uncached(usage: dict[str, Any]) -> int | None:
    value = usage.get("uncached_input_tokens")
    if isinstance(value, bool) or not isinstance(value, int):
        return None
    return value


def _void(base: dict[str, Any], reason: str) -> dict[str, Any]:
    return {
        **base,
        "status": "void",
        "oracle_success": None,
        "model_claimed_success": None,
        "false_success": None,
        "void_reason": reason,
        "wall_ms": None,
        "events": [],
        "tool_calls": [],
        "scored": False,
    }


def _note(store, run_id, phase, identity, candidate, surface, task, generation, order_index, reason, live=None, fixture_hash=None) -> None:
    live = live or {}
    snapshot = {
        "phase": phase,
        "recorded_phase": phase,
        "lab_git": identity.get("lab_git"),
        "nexus_git": identity.get("nexus_git"),
        "nexus_base_commit": identity.get("nexus_base_commit"),
        "candidate_hash": candidate["architecture_hash"],
        "surface_hash": surface.get("surface_hash"),
        "fixture_hash": fixture_hash or identity.get("fixture_hash"),
        "task_set_hash": identity.get("task_set_hash"),
        "task_id": task["task_id"],
        "unity_version": live.get("unity_version") or identity.get("unity_version"),
        "pipeline_version": live.get("pipeline_version") or identity.get("pipeline_version"),
        "cli_version": live.get("cli_version") or identity.get("cli_version"),
        "codex_version": live.get("codex_version") or identity.get("codex_version"),
        "model": identity.get("model") or live.get("model"),
        "reasoning": identity.get("reasoning"),
        "port": live.get("port") if live.get("port") is not None else identity.get("port"),
        "unity_project": live.get("project_path") or identity.get("unity_project"),
        "visible_tool_names": list(surface.get("model_visible_names") or []),
        "mechanism_actually_used": surface.get("mechanism_actually_used"),
        "codex_home": identity.get("codex_home"),
        "generation": generation,
        "execution_order": order_index,
        "catalog_hash": identity.get("catalog_hash"),
        "void_reason": reason,
    }
    store.note_identity(run_id, phase, snapshot)


def _mark_unmeasured(call: dict[str, Any]) -> dict[str, Any]:
    """Null means the field was not measured. It is not a zero."""
    missing = [key for key in _UNMEASURED if call.get(key) is None]
    if missing and not call.get("error"):
        call = dict(call)
        call["error"] = "unmeasured:" + ",".join(missing)
    return call


def _experiment_taint(records: list[dict[str, Any]]) -> list[str]:
    reasons = []
    if any(row.get("status") == "mechanism_mismatch" for row in records):
        reasons.append("mechanism_mismatch")
    if any(row.get("model_started") and not row.get("cleanup_ok") for row in records):
        reasons.append("cleanup_taint")
    if reasons:
        reasons.append("experiment_tainted")
    return reasons


def _sum_usage(rows: list[dict[str, Any]], key: str) -> int | None:
    if not rows:
        return None
    values = [row["usage"].get(key) for row in rows]
    if any(not isinstance(value, int) or isinstance(value, bool) for value in values):
        return None
    return sum(values)
