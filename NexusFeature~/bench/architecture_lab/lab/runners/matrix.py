"""Smoke matrix.

A pre-run gate failure is a void: the model is not started and the run is not
scored. Once the model has started, the run stays in the store.
"""

from __future__ import annotations

import time
from typing import Any

from lab.candidates.manifest import coverage_report
from lab.fixtures.observe import pristine_match
from lab.fixtures.oracles import evaluate
from lab.reports.render import render_comparison
from lab.runners.preflight import preflight
from lab.search.feasibility import classify_candidate, per_success
from lab.surfaces.generate import generate_surface, surfaces_differ
from lab.telemetry.calls import classify_call, summarize_calls
from lab.telemetry.cost import estimate_cost

SMOKE_LABEL = "SMOKE_ONLY"


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
) -> dict[str, Any]:
    store.add_experiment(experiment_id, SMOKE_LABEL, identity)
    surfaces = []
    records = []
    for candidate in candidates:
        surface = generate_surface(candidate, catalog)
        surface["candidate"] = candidate
        surface["substituted"] = False
        surfaces.append(surface)
        store.add_candidate(candidate, state="SCREENING")
        for task in tasks:
            records.append(
                _one_run(
                    candidate=candidate,
                    surface=surface,
                    task=task,
                    store=store,
                    expected_probe=expected_probe,
                    probe=probe,
                    prepare_task=prepare_task,
                    codex_run=codex_run,
                    observe=observe,
                    price_profile=price_profile,
                    seed=seed,
                    experiment_id=experiment_id,
                )
            )
    rows = _rows(records, tasks, catalog)
    report = render_comparison(rows, label=SMOKE_LABEL)
    return {
        "label": SMOKE_LABEL,
        "surfaces": surfaces,
        "runs": records,
        "rows": rows,
        "report": report,
        "acceptance": smoke_acceptance(records, surfaces, report),
        "executed_search": False,
    }


def smoke_acceptance(records: list[dict[str, Any]], surfaces: list[dict[str, Any]], report: str) -> dict[str, Any]:
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
        if row["status"] == "surface_bypass":
            failures.append(f"{row['run_id']} used a tool outside the candidate surface")
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
    return {"ok": not failures, "failures": failures}


def _one_run(*, candidate, surface, task, store, expected_probe, probe, prepare_task, codex_run, observe, price_profile, seed, experiment_id) -> dict[str, Any]:
    run_id = f"{experiment_id}-{candidate['candidate_id']}-{task['task_id']}-s{seed}"
    gate = preflight(expected_probe, probe())
    prepared = {"ok": False, "fingerprint": None, "reason": "not prepared"}
    if gate["ok"]:
        prepared = prepare_task(task)
    base = {
        "run_id": run_id,
        "experiment_id": experiment_id,
        "candidate_id": candidate["candidate_id"],
        "candidate_hash": candidate["architecture_hash"],
        "task_id": task["task_id"],
        "split": task["split"],
        "seed": seed,
        "label": SMOKE_LABEL,
        "model": expected_probe.get("model"),
        "fixture_hash": prepared.get("fingerprint"),
        "model_started": False,
        "events_saved": False,
        "unity_calls": 0,
        "oracle_evaluated": False,
        "cleanup_ok": False,
        "usage": {"status": "MISSING"},
        "call_summary": {},
        "candidate": candidate,
    }
    if not gate["ok"] or not prepared.get("ok"):
        reason = "; ".join(gate["failures"]) if not gate["ok"] else prepared.get("reason")
        row = {
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
        store.add_run(row)
        return row

    started = time.monotonic()
    outcome = codex_run(surface, task, run_id)
    wall_ms = int((time.monotonic() - started) * 1000)
    observation = observe()
    verdict = evaluate(task, outcome.get("final_message") or "", observation)
    if outcome.get("surface_bypass"):
        # A side path can change Unity and still be an invalid measurement.
        verdict = {
            **verdict,
            "oracle_success": False,
            "false_success": bool(verdict.get("model_claimed_success")),
        }
    calls, summary = _annotate_calls(surface, outcome, task)
    usage = dict(outcome.get("usage") or {"status": "MISSING"})
    usage["cost"] = estimate_cost(usage, price_profile)
    status = "surface_bypass" if outcome.get("surface_bypass") else (outcome.get("status") or "completed")
    cleanup = prepare_task(task)
    after = observe()
    cleanup_ok = bool(cleanup.get("ok")) and not pristine_match(after) and cleanup.get("fingerprint") == prepared.get("fingerprint")
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


def _annotate_calls(surface: dict[str, Any], outcome: dict[str, Any], task: dict[str, Any]) -> tuple[list[dict[str, Any]], dict[str, int]]:
    telemetry = list(outcome.get("backend_telemetry") or [])
    cursor = 0
    calls = []
    for raw in outcome.get("tool_calls") or []:
        classified = classify_call(surface, raw)
        reached = None
        hits_backend = classified["kind"] == "tool" or classified["exposed_name"] == "lab_invoke"
        if hits_backend and cursor < len(telemetry):
            reached = telemetry[cursor]
            cursor += 1
        calls.append(
            {
                "ts": raw.get("ts"),
                "exposed_name": classified["exposed_name"],
                "capability_id": classified["capability_id"],
                "backend_command": classified["backend_command"],
                "arguments": raw.get("arguments") or {},
                "ok": bool(reached.get("ok")) if reached else False,
                "backend_reached": reached is not None,
                "retry": bool(raw.get("retry")),
                "error": None if reached and reached.get("ok") else ("backend telemetry missing" if classified["kind"] == "tool" else None),
                "result_bytes": raw.get("result_bytes"),
                "visible_bytes": raw.get("visible_bytes"),
                "duration_ms": raw.get("duration_ms"),
            }
        )
    return calls, summarize_calls(surface, calls, set(task.get("acceptable_capabilities") or []))


def _rows(records: list[dict[str, Any]], tasks: list[dict[str, Any]], catalog: dict[str, Any]) -> list[dict[str, Any]]:
    task_by_id = {task["task_id"]: task for task in tasks}
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
        wall = sum(int(row["wall_ms"] or 0) for row in scored)
        false_successes = sum(1 for row in group if row.get("false_success"))
        bypasses = sum(1 for row in group if row.get("status") == "surface_bypass")
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
            },
            {
                "min_coverage": 1.0,
                "min_semantic_success": 1.0,
                "category_floors": {category: 1.0 for category in rates},
                "require_measured_usage": True,
            },
        ) if scored else {"feasible": False, "semantic_success_rate": 0.0, "reasons": ["no scored runs"]}
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
                "reasons": verdict.get("reasons"),
            }
        )
    return rows


def _sum_usage(rows: list[dict[str, Any]], key: str) -> int | None:
    if not rows:
        return None
    values = [row["usage"].get(key) for row in rows]
    if any(value is None for value in values):
        return None
    return sum(values)
