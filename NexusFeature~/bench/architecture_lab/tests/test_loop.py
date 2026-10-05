"""Closed-loop tests. The model and Unity are fakes. No Luna budget is spent."""

from __future__ import annotations

import ast
import json
import tempfile
import unittest
from pathlib import Path

from lab.candidates.baselines import smoke_hidden_gateway, smoke_official_subset
from lab.catalog.extract import load_catalog
from lab.cli import main
from lab.errors import LabError
from lab.runners.confirm_run import assert_canonical, assert_eligible, promotion_for, run_confirmation
from lab.runners.matrix import _annotate_calls, run_smoke_matrix
from lab.runners.study import run_evolution
from lab.search.engine import plan_generation
from lab.search.mutate import mutate
from lab.search.pareto import COST_KEYS, pareto_frontier
from lab.surfaces.generate import generate_surface, surface_hash, surface_problem
from lab.telemetry.store import RunStore

ROOT = Path(__file__).resolve().parents[1]
CATALOG = load_catalog(ROOT / "catalog" / "snapshots" / "pipeline-0.8.0-exp.1.json")
PRICE = {
    "profile_id": "luna6-unpriced",
    "uncached_input_per_million": None,
    "cached_input_per_million": None,
    "output_per_million": None,
}


def _probe():
    return {
        "port": 7811,
        "listener_pid": 999,
        "project_path": "/tmp/nexus-arch-lab",
        "pipeline_version": "0.8.0-exp.1",
        "cli_version": "1.0.0-beta.12",
        "codex_version": "0.160.0",
        "unity_version": "6000.3.16f1",
        "fixture_hash": "abc",
        "model": "gpt-6-luna",
        "compiling": False,
        "play_mode": False,
        "ready": True,
        "agent_cwd": "/tmp/empty",
    }


def _observe(task):
    del task
    return {
        "open_ok": True,
        "object_name": "LabReadTarget",
        "intensity": 1.25,
        "revision": 7,
        "marker_name": "readable",
        "play_mode": "stopped",
        "editor_status": "ready",
        "compiling": False,
    }


def _read_task():
    return json.loads((ROOT / "tasks" / "search" / "read_object_name.json").read_text(encoding="utf-8"))


def _usage(uncached=20):
    return {
        "status": "MEASURED",
        "uncached_input_tokens": uncached,
        "cached_input_tokens": 0,
        "output_tokens": 5,
        "total_tokens": uncached + 5,
        "reasoning_tokens": None,
    }


def _codex(surface, task, run_id, calls, uncached=20, wrong=False):
    del task
    if wrong and "find_gameobjects" in surface["model_visible_names"]:
        name = "editor_status"
        backend = "editor_status"
    elif "find_gameobjects" in surface["model_visible_names"]:
        name = "find_gameobjects"
        backend = "find_gameobjects"
    else:
        name = "nexus_find_gameobjects"
        backend = "find_gameobjects"
    calls.append({"run_id": run_id, "candidate_hash": surface["candidate_hash"], "generation": surface["candidate"].get("generation")})
    return {
        "status": "completed",
        "final_message": json.dumps({"object_name": "LabReadTarget"}),
        "events": [{"type": "turn.completed"}],
        "tool_calls": [{"exposed_name": name, "arguments": {}, "ok": True}],
        "backend_telemetry": [{"backend_command": backend, "ok": True, "ts": "2026-10-05T00:00:00+00:00", "duration_ms": 12, "result_bytes": 40}],
        "surface_bypass": False,
        "usage": _usage(uncached),
    }


def _kwargs(codex):
    return {
        "expected_probe": _probe(),
        "probe": _probe,
        "prepare_task": lambda task: {"ok": True, "fingerprint": "abc"},
        "cleanup_task": lambda task: {"ok": True, "fingerprint": "abc"},
        "codex_run": codex,
        "observe": _observe,
        "price_profile": PRICE,
    }


class EvolveLoop(unittest.TestCase):
    def test_runner_advances_a_generation_and_skips_duplicates(self):
        official = smoke_official_subset(CATALOG)
        hidden = smoke_hidden_gateway(CATALOG)
        calls = []

        def codex(surface, task, run_id):
            return _codex(surface, task, run_id, calls)

        with tempfile.TemporaryDirectory() as tmp:
            store = RunStore(Path(tmp) / "runs.sqlite")
            result = run_evolution(
                catalog=CATALOG,
                tasks=[_read_task()],
                population=[official, hidden],
                store=store,
                seed=17,
                generations=2,
                max_runs=20,
                max_uncached_tokens=100000,
                experiment_id="evo",
                label="EVOLUTION_INTEGRATION_ONLY",
                population_limit=2,
                runner_kwargs=_kwargs(codex),
                identity={"lab_git": "abc123", "model": "gpt-6-luna", "reasoning": "low", "catalog_hash": CATALOG["catalog_hash"]},
            )
            self.assertTrue(result["executed"])
            self.assertEqual(result["stop_reason"], "STOP_GENERATION_LIMIT")
            generations = {item["generation"] for item in result["lineage"]}
            self.assertIn(0, generations)
            self.assertIn(1, generations)
            self.assertEqual(len(result["lineage"]), 2)
            gen0_rows = next(item["rows"] for item in result["lineage"] if item["generation"] == 0)
            self.assertEqual(gen0_rows[0]["semantic_success_rate"], 1.0)
            self.assertGreaterEqual(gen0_rows[0]["oracle_successes"], 1)
            self.assertEqual(gen0_rows[0]["usage_status"], "MEASURED")
            gen0 = {official["architecture_hash"], hidden["architecture_hash"]}
            children = []
            for entry in result["lineage"]:
                children.extend(entry.get("children") or [])
            self.assertTrue(children)
            self.assertTrue(all(child["parent_hash"] in gen0 for child in children))
            evaluated = {item["candidate_hash"] for item in calls}
            self.assertTrue(gen0 <= evaluated)
            self.assertTrue(any(item not in gen0 for item in evaluated))
            first = store.run_identities(calls[0]["run_id"])
            self.assertEqual(first[0]["phase"], "before_model")
            self.assertEqual(first[0]["identity"]["lab_git"], "abc123")
            self.assertEqual(first[0]["identity"]["fixture_hash"], "abc")
            self.assertTrue(first[0]["identity"]["surface_hash"])
            self.assertEqual(first[0]["identity"]["candidate_hash"], calls[0]["candidate_hash"])
            self.assertEqual(first[0]["identity"]["unity_version"], "6000.3.16f1")
            stored = store.runs()
            self.assertGreaterEqual(len(stored), 3)
            store.close()

    def test_duplicate_child_is_not_sent_to_the_model(self):
        parent = smoke_official_subset(CATALOG)
        child = mutate(parent, CATALOG, 19)
        self.assertIsNotNone(child)
        task = _read_task()
        calls = []

        def codex(surface, task, run_id):
            return _codex(surface, task, run_id, calls)

        with tempfile.TemporaryDirectory() as tmp:
            store = RunStore(Path(tmp) / "runs.sqlite")
            store.add_run(
                {
                    "run_id": f"dup-{child['candidate_id']}-{task['task_id']}-s17",
                    "experiment_id": "dup",
                    "candidate_hash": child["architecture_hash"],
                    "candidate_id": child["candidate_id"],
                    "task_id": task["task_id"],
                    "split": "search",
                    "seed": 17,
                    "status": "completed",
                    "oracle_success": True,
                    "model_claimed_success": True,
                    "false_success": False,
                    "usage": _usage(),
                    "wall_ms": 1,
                    "void_reason": None,
                    "label": "EVOLUTION_INTEGRATION_ONLY",
                    "model_started": True,
                    "events": [],
                    "tool_calls": [],
                    "scored": True,
                    "call_summary": {},
                    "candidate": child,
                }
            )
            store.save_study_state(
                "dup",
                {
                    "generation": 0,
                    "phase": "evaluating",
                    "pending_manifests": [parent],
                    "lineage": [],
                    "skipped": [],
                    "stop_reason": None,
                    "resumable": True,
                    "seed": 17,
                    "population_limit": 1,
                    "label": "EVOLUTION_INTEGRATION_ONLY",
                },
            )
            result = run_evolution(
                catalog=CATALOG,
                tasks=[task],
                population=[parent],
                store=store,
                seed=17,
                generations=2,
                max_runs=20,
                max_uncached_tokens=100000,
                experiment_id="dup",
                label="EVOLUTION_INTEGRATION_ONLY",
                population_limit=1,
                runner_kwargs=_kwargs(codex),
                identity={"model": "gpt-6-luna"},
                resume=True,
            )
            hashes = {item["candidate_hash"] for item in calls}
            self.assertNotIn(child["architecture_hash"], hashes)
            self.assertTrue(any(item.get("reason") == "DUPLICATE_CANDIDATE_SKIPPED" for item in result["skipped"]))
            store.close()

    def test_max_runs_stops_before_the_next_model_call(self):
        calls = []

        def codex(surface, task, run_id):
            return _codex(surface, task, run_id, calls)

        with tempfile.TemporaryDirectory() as tmp:
            store = RunStore(Path(tmp) / "runs.sqlite")
            result = run_evolution(
                catalog=CATALOG,
                tasks=[_read_task()],
                population=[smoke_official_subset(CATALOG), smoke_hidden_gateway(CATALOG)],
                store=store,
                seed=17,
                generations=2,
                max_runs=1,
                max_uncached_tokens=100000,
                experiment_id="budget-runs",
                label="EVOLUTION_INTEGRATION_ONLY",
                population_limit=2,
                runner_kwargs=_kwargs(codex),
                identity={"model": "gpt-6-luna"},
            )
            self.assertEqual(result["stop_reason"], "STOP_MAX_RUNS")
            self.assertEqual(len(calls), 1)
            self.assertEqual(result["runs_used"], 1)
            store.close()

    def test_token_budget_stops_before_the_next_model_call(self):
        calls = []

        def codex(surface, task, run_id):
            return _codex(surface, task, run_id, calls, uncached=10)

        with tempfile.TemporaryDirectory() as tmp:
            store = RunStore(Path(tmp) / "runs.sqlite")
            result = run_evolution(
                catalog=CATALOG,
                tasks=[_read_task()],
                population=[smoke_official_subset(CATALOG), smoke_hidden_gateway(CATALOG)],
                store=store,
                seed=17,
                generations=2,
                max_runs=20,
                max_uncached_tokens=10,
                experiment_id="budget-tokens",
                label="EVOLUTION_INTEGRATION_ONLY",
                population_limit=2,
                runner_kwargs=_kwargs(codex),
                identity={"model": "gpt-6-luna"},
            )
            self.assertEqual(result["stop_reason"], "STOP_TOKEN_BUDGET")
            self.assertEqual(len(calls), 1)
            store.close()

    def test_last_run_of_a_generation_cannot_cross_the_token_cap_and_plan_the_next(self):
        calls = []

        def codex(surface, task, run_id):
            return _codex(surface, task, run_id, calls, uncached=11)

        with tempfile.TemporaryDirectory() as tmp:
            store = RunStore(Path(tmp) / "runs.sqlite")
            result = run_evolution(
                catalog=CATALOG,
                tasks=[_read_task()],
                population=[smoke_official_subset(CATALOG)],
                store=store,
                seed=17,
                generations=2,
                max_runs=20,
                max_uncached_tokens=10,
                experiment_id="budget-last",
                label="EVOLUTION_INTEGRATION_ONLY",
                population_limit=1,
                runner_kwargs=_kwargs(codex),
                identity={"model": "gpt-6-luna"},
            )
            self.assertEqual(result["stop_reason"], "STOP_TOKEN_BUDGET")
            self.assertEqual(len(calls), 1)
            self.assertFalse(any(entry.get("children") for entry in result["lineage"]))
            store.close()

    def test_infeasible_parent_is_not_mutated(self):
        calls = []

        def codex(surface, task, run_id):
            wrong = "find_gameobjects" in surface["model_visible_names"]
            return _codex(surface, task, run_id, calls, wrong=wrong)

        official = smoke_official_subset(CATALOG)
        hidden = smoke_hidden_gateway(CATALOG)
        with tempfile.TemporaryDirectory() as tmp:
            store = RunStore(Path(tmp) / "runs.sqlite")
            result = run_evolution(
                catalog=CATALOG,
                tasks=[_read_task()],
                population=[official, hidden],
                store=store,
                seed=17,
                generations=2,
                max_runs=20,
                max_uncached_tokens=100000,
                experiment_id="parents",
                label="EVOLUTION_INTEGRATION_ONLY",
                population_limit=2,
                runner_kwargs=_kwargs(codex),
                identity={"model": "gpt-6-luna"},
            )
            children = [child for entry in result["lineage"] for child in entry.get("children") or []]
            self.assertTrue(children)
            self.assertTrue(all(child["parent_hash"] == hidden["architecture_hash"] for child in children))
            self.assertNotIn(official["architecture_hash"], {child["parent_hash"] for child in children})
            store.close()

    def test_resume_does_not_repeat_a_started_run(self):
        calls = []

        def codex(surface, task, run_id):
            return _codex(surface, task, run_id, calls)

        population = [smoke_official_subset(CATALOG), smoke_hidden_gateway(CATALOG)]
        with tempfile.TemporaryDirectory() as tmp:
            store = RunStore(Path(tmp) / "runs.sqlite")
            common = dict(
                catalog=CATALOG,
                tasks=[_read_task()],
                population=population,
                store=store,
                seed=3,
                generations=1,
                max_uncached_tokens=100000,
                experiment_id="resume",
                label="EVOLUTION_INTEGRATION_ONLY",
                population_limit=2,
                runner_kwargs=_kwargs(codex),
                identity={"model": "gpt-6-luna"},
            )
            first = run_evolution(max_runs=1, resume=False, **common)
            self.assertEqual(first["stop_reason"], "STOP_MAX_RUNS")
            self.assertEqual(len(calls), 1)
            second = run_evolution(max_runs=5, resume=True, **common)
            self.assertEqual(len(calls), 2)
            self.assertNotEqual(calls[0]["run_id"], calls[1]["run_id"])
            self.assertEqual(second["runs_used"], 2)
            self.assertEqual([entry["generation"] for entry in second["lineage"]], [0])
            store.close()

    def test_duplicate_initial_candidate_is_not_run_twice(self):
        official = smoke_official_subset(CATALOG)
        copy = json.loads(json.dumps(official))
        calls = []

        def codex(surface, task, run_id):
            return _codex(surface, task, run_id, calls)

        with tempfile.TemporaryDirectory() as tmp:
            store = RunStore(Path(tmp) / "runs.sqlite")
            result = run_evolution(
                catalog=CATALOG,
                tasks=[_read_task()],
                population=[official, copy],
                store=store,
                seed=17,
                generations=1,
                max_runs=10,
                max_uncached_tokens=100000,
                experiment_id="dup-init",
                label="EVOLUTION_INTEGRATION_ONLY",
                population_limit=2,
                runner_kwargs=_kwargs(codex),
                identity={"model": "gpt-6-luna"},
            )
            self.assertEqual(len(calls), 1)
            self.assertTrue(any(item.get("reason") == "DUPLICATE_CANDIDATE_SKIPPED" for item in result["skipped"]))
            store.close()

    def test_validation_task_never_enters_the_search_loop(self):
        started = {"n": 0}

        def runner(**kwargs):
            started["n"] += 1
            raise AssertionError("runner must not be called")

        with tempfile.TemporaryDirectory() as tmp:
            store = RunStore(Path(tmp) / "runs.sqlite")
            task = _read_task()
            task["split"] = "validation"
            task["task_id"] = "validation.read_object_name"
            with self.assertRaises(LabError):
                run_evolution(
                    catalog=CATALOG,
                    tasks=[task],
                    population=[smoke_official_subset(CATALOG)],
                    store=store,
                    seed=1,
                    generations=1,
                    max_runs=2,
                    max_uncached_tokens=10,
                    experiment_id="leak",
                    label="SEARCH",
                    population_limit=1,
                    runner=runner,
                    runner_kwargs={},
                )
            self.assertEqual(started["n"], 0)
            store.close()

    def test_study_source_does_not_import_confirmation(self):
        source = (ROOT / "lab" / "runners" / "study.py").read_text(encoding="utf-8")
        tree = ast.parse(source)
        imports = []
        for node in ast.walk(tree):
            if isinstance(node, ast.ImportFrom) and node.module:
                imports.append(node.module)
            if isinstance(node, ast.Import):
                imports.extend(alias.name for alias in node.names)
        self.assertNotIn("lab.validation", imports)
        self.assertNotIn("lab.validation.confirm", imports)
        self.assertNotIn("lab.search.holdout", imports)
        self.assertNotIn("lab.runners.live", imports)

    def test_zero_success_does_not_outrank_a_feasible_row(self):
        rows = [
            {
                "candidate_hash": "cheap-zero",
                "feasible": False,
                "wrong_tool_calls": 0,
                "total_tokens_per_success": 0,
                "tool_calls_per_success": 0,
            },
            {
                "candidate_hash": "useful",
                "feasible": True,
                "wrong_tool_calls": 2,
                "total_tokens_per_success": 500,
                "tool_calls_per_success": 4,
            },
        ]
        self.assertEqual([row["candidate_hash"] for row in pareto_frontier(rows)], ["useful"])
        self.assertNotIn("wall_ms_per_success", COST_KEYS)
        self.assertNotIn("uncached_tokens_per_success", COST_KEYS)


class ConfirmLoop(unittest.TestCase):
    def test_exact_hash_is_required_and_a_mutated_candidate_is_rejected(self):
        candidate = smoke_official_subset(CATALOG)
        assert_canonical(candidate, candidate["architecture_hash"], CATALOG)
        mutated = dict(candidate)
        mutated["mutated_during_confirm"] = True
        with self.assertRaises(LabError):
            assert_canonical(mutated, candidate["architecture_hash"], CATALOG)
        changed = json.loads(json.dumps(candidate))
        changed["architecture_hash"] = "f" * 64
        with self.assertRaises(LabError):
            assert_canonical(changed, changed["architecture_hash"], CATALOG)
        with self.assertRaises(LabError):
            assert_eligible(candidate, integration=False)
        assert_eligible(candidate, integration=True)

    def test_validation_failure_does_not_load_holdout(self):
        candidate = smoke_official_subset(CATALOG)
        loaded = {"n": 0}

        def holdout_loader():
            loaded["n"] += 1
            return [{"task_id": "confirm_holdout.write_intensity", "split": "confirm_holdout"}]

        def run_batch(tasks, split):
            self.assertEqual(split, "validation")
            return {"rows": [{"feasible": False, "model_started_any": True, "candidate_hash": candidate["architecture_hash"]}]}

        result = run_confirmation(
            candidate=candidate,
            expected_hash=candidate["architecture_hash"],
            validation_tasks=[{"task_id": "confirm_validation.read_object_name", "split": "confirm_validation"}],
            holdout_loader=holdout_loader,
            run_batch=run_batch,
            include_holdout=True,
        )
        self.assertEqual(loaded["n"], 0)
        self.assertEqual(result["status"], "CONFIRMATION_FAILED")
        self.assertFalse(result["holdout_executed"])
        self.assertEqual(set(result["optimizer"]), {"status", "candidate_hash"})
        self.assertNotIn("holdout", json.dumps(result["optimizer"]))

    def test_stopped_validation_does_not_load_holdout_even_if_rows_look_feasible(self):
        candidate = smoke_official_subset(CATALOG)
        loaded = {"n": 0}

        def holdout_loader():
            loaded["n"] += 1
            return [{"task_id": "confirm_holdout.write_intensity"}]

        def run_batch(tasks, split):
            self.assertEqual(split, "validation")
            return {
                "stopped": "STOP_TOKEN_UNKNOWN",
                "rows": [{"feasible": True, "model_started_any": True, "candidate_hash": candidate["architecture_hash"]}],
            }

        result = run_confirmation(
            candidate=candidate,
            expected_hash=candidate["architecture_hash"],
            validation_tasks=[{"task_id": "confirm_validation.read_object_name"}],
            holdout_loader=holdout_loader,
            run_batch=run_batch,
            include_holdout=True,
        )
        self.assertEqual(loaded["n"], 0)
        self.assertEqual(result["status"], "CONFIRMATION_VOID")
        self.assertFalse(result["holdout_executed"])
        self.assertEqual(result["reason"], "STOP_TOKEN_UNKNOWN")
        self.assertEqual(set(result["optimizer"]), {"status", "candidate_hash"})

    def test_budget_block_does_not_load_holdout_after_a_pass(self):
        candidate = smoke_official_subset(CATALOG)
        loaded = {"n": 0}

        def holdout_loader():
            loaded["n"] += 1
            return [{"task_id": "confirm_holdout.write_intensity"}]

        result = run_confirmation(
            candidate=candidate,
            expected_hash=candidate["architecture_hash"],
            validation_tasks=[{"task_id": "v"}],
            holdout_loader=holdout_loader,
            run_batch=lambda tasks, split: {"rows": [{"feasible": True, "model_started_any": True}]},
            include_holdout=True,
            holdout_block=lambda: "STOP_MAX_RUNS",
        )
        self.assertEqual(loaded["n"], 0)
        self.assertEqual(result["status"], "CONFIRMATION_VOID")
        self.assertFalse(result["holdout_executed"])
        self.assertEqual(result["reason"], "STOP_MAX_RUNS")
        self.assertNotIn("holdout", json.dumps(result["optimizer"]))

    def test_passed_confirmation_keeps_holdout_out_of_the_optimizer(self):
        candidate = smoke_hidden_gateway(CATALOG)

        def run_batch(tasks, split):
            return {"rows": [{"feasible": True, "model_started_any": True}]}

        result = run_confirmation(
            candidate=candidate,
            expected_hash=candidate["architecture_hash"],
            validation_tasks=[{"task_id": "v"}],
            holdout_loader=lambda: [{"task_id": "h"}],
            run_batch=run_batch,
            include_holdout=True,
        )
        self.assertEqual(result["status"], "CONFIRMATION_PASSED")
        self.assertTrue(result["holdout_executed"])
        self.assertNotIn("holdout_rows", result["optimizer"])
        self.assertEqual(promotion_for("CONFIRMATION_PASSED", integration=True, state="SCREENING"), "SCREENING")
        self.assertEqual(promotion_for("CONFIRMATION_PASSED", integration=False, state="HOLDOUT_READY"), "CONFIRMED")

    def test_plan_records_duplicate_skips(self):
        parent = smoke_official_subset(CATALOG)
        child = mutate(parent, CATALOG, 19)
        plan = plan_generation([parent], CATALOG, seed=18, known_hashes={child["architecture_hash"]})
        self.assertEqual(plan["children"], [])
        self.assertEqual(plan["skipped"][0]["reason"], "DUPLICATE_CANDIDATE_SKIPPED")
        self.assertFalse(plan["executed"])


class Measurement(unittest.TestCase):
    def test_before_model_identity_is_not_replaced(self):
        with tempfile.TemporaryDirectory() as tmp:
            store = RunStore(Path(tmp) / "runs.sqlite")
            store.note_identity("run-1", "before_model", {"unity_version": "6000.3.16f1", "phase": "before_model"})
            store.note_identity("run-1", "before_model", {"unity_version": "later-probe", "phase": "before_model"})
            rows = store.run_identities("run-1")
            self.assertEqual(len(rows), 1)
            self.assertEqual(rows[0]["identity"]["unity_version"], "6000.3.16f1")
            store.close()

    def test_unmeasured_fields_stay_null_and_name_the_reason(self):
        surface = generate_surface(smoke_official_subset(CATALOG), CATALOG)
        task = _read_task()
        calls, _summary = _annotate_calls(
            surface,
            {
                "tool_calls": [{"exposed_name": "find_gameobjects", "arguments": {}}],
                "backend_telemetry": [{"backend_command": "find_gameobjects", "ok": True, "backend_reached": True}],
            },
            task,
        )
        from lab.runners.matrix import _mark_unmeasured

        marked = _mark_unmeasured(calls[0])
        self.assertIsNone(marked["ts"])
        self.assertIsNone(marked["duration_ms"])
        self.assertIsNone(marked["result_bytes"])
        self.assertNotEqual(marked["result_bytes"], 0)
        self.assertTrue(marked["error"].startswith("unmeasured:"))

    def test_a_miss_does_not_inherit_the_next_backend_call(self):
        surface = generate_surface(smoke_official_subset(CATALOG), CATALOG)
        task = _read_task()
        calls, _summary = _annotate_calls(
            surface,
            {
                "tool_calls": [
                    {"exposed_name": "editor_status", "arguments": {}, "server": "architecture_lab"},
                    {"exposed_name": "find_gameobjects", "arguments": {}, "server": "architecture_lab"},
                ],
                "backend_telemetry": [
                    {"backend_command": None, "ok": False, "backend_reached": False, "step_count": 0, "duration_ms": 0, "result_bytes": 0},
                    {"backend_command": "find_gameobjects", "ok": True, "backend_reached": True, "duration_ms": 8, "result_bytes": 20},
                ],
            },
            task,
        )
        self.assertFalse(calls[0]["backend_reached"])
        self.assertNotEqual(calls[0]["backend_command"], "find_gameobjects")
        self.assertTrue(calls[1]["backend_reached"])
        self.assertEqual(calls[1]["capability_id"], "find_gameobjects")

    def test_a_lying_architecture_hash_does_not_start_the_model(self):
        started = {"n": 0}
        candidate = json.loads(json.dumps(smoke_official_subset(CATALOG)))
        candidate["architecture_hash"] = "ab" * 32
        with tempfile.TemporaryDirectory() as tmp:
            store = RunStore(Path(tmp) / "runs.sqlite")
            result = run_smoke_matrix(
                catalog=CATALOG,
                tasks=[_read_task()],
                candidates=[candidate],
                store=store,
                expected_probe=_probe(),
                probe=_probe,
                prepare_task=lambda task: {"ok": True, "fingerprint": "abc"},
                codex_run=lambda surface, task, run_id: started.__setitem__("n", started["n"] + 1),
                observe=_observe,
                price_profile=PRICE,
                seed=1,
                experiment_id="bad-hash",
                identity={"label": "SMOKE_ONLY"},
            )
            self.assertEqual(started["n"], 0)
            self.assertIn("canonical content", result["runs"][0]["void_reason"])
            store.close()

    def test_surface_mismatch_does_not_start_the_model(self):
        import lab.runners.matrix as matrix

        started = {"n": 0}
        original = matrix.surface_problem
        matrix.surface_problem = lambda surface, candidate: "mechanism leak"
        try:
            with tempfile.TemporaryDirectory() as tmp:
                store = RunStore(Path(tmp) / "runs.sqlite")
                result = run_smoke_matrix(
                    catalog=CATALOG,
                    tasks=[_read_task()],
                    candidates=[smoke_official_subset(CATALOG)],
                    store=store,
                    expected_probe=_probe(),
                    probe=_probe,
                    prepare_task=lambda task: {"ok": True, "fingerprint": "abc"},
                    codex_run=lambda surface, task, run_id: started.__setitem__("n", started["n"] + 1),
                    observe=_observe,
                    price_profile=PRICE,
                    seed=1,
                    experiment_id="void-surface",
                    identity={"label": "SMOKE_ONLY", "lab_git": "abc"},
                )
                self.assertEqual(started["n"], 0)
                self.assertEqual(result["runs"][0]["status"], "void")
                self.assertIn("mechanism leak", result["runs"][0]["void_reason"])
                store.close()
        finally:
            matrix.surface_problem = original

    def test_hidden_gateway_leak_is_a_surface_problem(self):
        hidden = smoke_hidden_gateway(CATALOG)
        surface = generate_surface(hidden, CATALOG)
        self.assertIsNone(surface_problem(surface, hidden))
        leaked = dict(surface)
        leaked["model_visible_names"] = ["find_gameobjects", *surface["model_visible_names"]]
        leaked["surface_hash"] = surface_hash(leaked)
        self.assertIn("find_gameobjects", surface_problem(leaked, hidden))

    def test_bypass_stays_in_the_scored_rows(self):
        def codex(surface, task, run_id):
            return {
                "status": "completed",
                "final_message": json.dumps({"object_name": "LabReadTarget"}),
                "events": [{"type": "turn.completed"}],
                "tool_calls": [{"exposed_name": "find_gameobjects", "arguments": {}}],
                "backend_telemetry": [{"backend_command": "find_gameobjects", "ok": True, "backend_reached": True, "ts": "t", "duration_ms": 1, "result_bytes": 2}],
                "surface_bypass": True,
                "usage": _usage(),
            }

        with tempfile.TemporaryDirectory() as tmp:
            store = RunStore(Path(tmp) / "runs.sqlite")
            result = run_smoke_matrix(
                catalog=CATALOG,
                tasks=[_read_task()],
                candidates=[smoke_official_subset(CATALOG), smoke_hidden_gateway(CATALOG)],
                store=store,
                **_kwargs(codex),
                seed=1,
                experiment_id="bypass",
                identity={"label": "SMOKE_ONLY"},
            )
            self.assertTrue(all(row["scored"] for row in result["runs"]))
            self.assertTrue(all(row["oracle_success"] is False for row in result["runs"]))
            self.assertTrue(all(row["feasible"] is False for row in result["rows"]))
            store.close()

    def test_cleanup_failure_taints_the_experiment(self):
        calls = []

        def codex(surface, task, run_id):
            return _codex(surface, task, run_id, calls)

        kwargs = _kwargs(codex)
        kwargs["cleanup_task"] = lambda task: {"ok": False, "fingerprint": "different"}
        with tempfile.TemporaryDirectory() as tmp:
            store = RunStore(Path(tmp) / "runs.sqlite")
            result = run_smoke_matrix(
                catalog=CATALOG,
                tasks=[_read_task()],
                candidates=[smoke_official_subset(CATALOG), smoke_hidden_gateway(CATALOG)],
                store=store,
                seed=1,
                experiment_id="cleanup",
                identity={"label": "SMOKE_ONLY"},
                **kwargs,
            )
            self.assertTrue(all("cleanup_taint" in row["reasons"] for row in result["rows"]))
            self.assertTrue(all(row["feasible"] is False for row in result["rows"]))
            store.close()

    def test_cli_refuses_without_the_confirm_flags(self):
        self.assertEqual(main(["evolve", "--seed", "1"]), 2)
        self.assertEqual(main(["confirm", "--candidate", "abc"]), 2)


if __name__ == "__main__":
    unittest.main()
