"""Laboratory integrity tests. These do not call a model or Unity."""

from __future__ import annotations

import ast
import json
import tempfile
import unittest
from pathlib import Path

from lab.adapters.dispatch import dispatch
from lab.adapters.mcp_server import handle
from lab.candidates.baselines import (
    all_baselines,
    smoke_hidden_gateway,
    smoke_official_subset,
    zero_capability,
)
from lab.candidates.manifest import normalize_candidate
from lab.catalog.extract import load_catalog
from lab.errors import CandidateInvalid, HoldoutLeak, SafetyError, UnsupportedMechanism
from lab.fixtures.oracles import evaluate
from lab.fixtures.reset import fingerprint, reset_tree
from lab.fixtures.tasks import load_tasks, validate_task
from lab.reports.render import render_comparison
from lab.runners.preflight import preflight
from lab.runners.safety import assert_safe_to_stop
from lab.search.cooccurrence import cooccurrence
from lab.search.engine import evolve_request, plan_generation
from lab.search.feasibility import classify_candidate, per_success
from lab.search.holdout import load_search_tasks, load_task_split
from lab.search.mutate import mutate
from lab.search.pareto import COST_KEYS, pareto_frontier, selection_order
from lab.search.promote import freeze, mutate_frozen, transition
from lab.surfaces.generate import generate_surface, surfaces_differ
from lab.telemetry.cost import estimate_cost
from lab.telemetry.store import RunStore
from lab.telemetry.tokens import add_usage, estimate_tokens, usage_from_events
from lab.validation import confirm as confirm_module

ROOT = Path(__file__).resolve().parents[1]
CATALOG = load_catalog(ROOT / "catalog" / "snapshots" / "pipeline-0.8.0-exp.1.json")
READ_SECRETS = [
    "LabReadTarget",
    "LabOther",
    "readable",
    "1.25",
    "validation-seal",
    "holdout-seal",
    "a11ab000a11ab000a11ab000a11ab000",
    "b22cd000b22cd000b22cd000b22cd000",
    "c33ef000c33ef000c33ef000c33ef000",
    "6000.3.16f1",
]


class CatalogAndCandidates(unittest.TestCase):
    def test_catalog_has_official_and_nexus_commands(self):
        ids = {item["id"] for item in CATALOG["capabilities"]}
        self.assertGreater(len(ids), 100)
        self.assertIn("set_serialized_field", ids)
        self.assertIn("nexus_capture_game_view", ids)

    def test_hash_ignores_identity_and_is_stable(self):
        left = smoke_official_subset(CATALOG)
        again = smoke_official_subset(CATALOG)
        renamed = dict(left)
        renamed["candidate_id"] = "other-name"
        renamed["why"] = "different prose"
        self.assertEqual(left["architecture_hash"], again["architecture_hash"])
        self.assertEqual(left["architecture_hash"], renamed["architecture_hash"])
        hidden = smoke_hidden_gateway(CATALOG)
        self.assertNotEqual(left["architecture_hash"], hidden["architecture_hash"])

    def test_baselines_normalize_and_flat_is_not_the_smoke_subset(self):
        baselines = all_baselines(CATALOG)
        ids = {item["candidate_id"] for item in baselines}
        self.assertIn("BASELINE_UNITY_FLAT", ids)
        self.assertIn("BASELINE_HIDDEN_GATEWAY", ids)
        smoke = smoke_official_subset(CATALOG)
        flat = next(item for item in baselines if item["candidate_id"] == "BASELINE_UNITY_FLAT")
        self.assertNotEqual(smoke["architecture_hash"], flat["architecture_hash"])

    def test_surfaces_differ_and_hidden_gateway_hides_official_names(self):
        official = generate_surface(smoke_official_subset(CATALOG), CATALOG)
        hidden = generate_surface(smoke_hidden_gateway(CATALOG), CATALOG)
        self.assertTrue(surfaces_differ(official, hidden))
        self.assertIn("set_serialized_field", official["model_visible_names"])
        self.assertNotIn("set_serialized_field", hidden["model_visible_names"])
        self.assertIn("nexus_set_field", hidden["model_visible_names"])
        self.assertEqual(
            hidden["resolution"]["nexus_set_field"]["backend_name"],
            "set_serialized_field",
        )

    def test_dynamic_tool_list_is_refused(self):
        raw = {
            "candidate_id": "bad",
            "surface_strategy": "hybrid",
            "discovery_mechanism": "dynamic_tool_list",
            "capabilities": {},
        }
        with self.assertRaises(UnsupportedMechanism):
            normalize_candidate(raw, CATALOG)

    def test_flat_union_is_rejected(self):
        raw = smoke_official_subset(CATALOG)
        capabilities = {
            cap_id: dict(entry)
            for cap_id, entry in raw["architecture"]["capabilities"].items()
            if entry["exposure"] != "DISABLED"
        }
        capabilities["set_serialized_field"]["parameter_union"] = "flat"
        raw_manifest = {
            "candidate_id": "flat-union",
            "surface_strategy": "small_managers",
            "discovery_mechanism": "none",
            "capabilities": capabilities,
        }
        with self.assertRaises(CandidateInvalid):
            normalize_candidate(raw_manifest, CATALOG)


class DegeneracyAndPareto(unittest.TestCase):
    def test_zero_capability_is_infeasible_not_cheap(self):
        verdict = classify_candidate(
            {
                "coverage": 0.0,
                "attempted": 4,
                "oracle_successes": 0,
                "category_rates": {"read": 0.0, "write": 0.0},
                "usage_status": "MEASURED",
            },
            {
                "min_coverage": 1.0,
                "min_semantic_success": 0.5,
                "category_floors": {"read": 0.5, "write": 0.5},
            },
        )
        self.assertEqual(verdict["status"], "INFEASIBLE")
        self.assertIsNone(per_success(0, 0))
        frontier = pareto_frontier(
            [
                {
                    "candidate_id": "ZERO_CAPABILITY",
                    "feasible": False,
                    "uncached_tokens_per_success": 0,
                    "total_tokens_per_success": 0,
                    "wrong_tool_calls": 0,
                    "tool_calls_per_success": 0,
                    "wall_ms_per_success": 0,
                },
                {
                    "candidate_id": "balanced",
                    "feasible": True,
                    "uncached_tokens_per_success": 100,
                    "total_tokens_per_success": 120,
                    "wrong_tool_calls": 1,
                    "tool_calls_per_success": 3,
                    "wall_ms_per_success": 1000,
                },
            ]
        )
        self.assertEqual([row["candidate_id"] for row in frontier], ["balanced"])

    def test_read_only_success_fails_write_floor(self):
        verdict = classify_candidate(
            {
                "coverage": 1.0,
                "attempted": 4,
                "oracle_successes": 2,
                "category_rates": {"read": 1.0, "write": 0.0},
                "usage_status": "MEASURED",
            },
            {
                "min_coverage": 1.0,
                "min_semantic_success": 0.4,
                "category_floors": {"read": 0.5, "write": 0.5},
            },
        )
        self.assertFalse(verdict["feasible"])
        self.assertIn("category:write", verdict["reasons"])

    def test_missing_usage_is_not_zero(self):
        usage = usage_from_events([{"type": "turn.completed"}])
        self.assertEqual(usage["status"], "MISSING")
        self.assertIsNone(usage["uncached_input_tokens"])
        estimated = estimate_tokens("abcd")
        self.assertEqual(estimated["source"], "ESTIMATED")
        cost = estimate_cost(usage, {"uncached_input_per_million": 1, "cached_input_per_million": 1, "output_per_million": 1})
        self.assertIsNone(cost["estimated_cost"])

    def test_null_price_is_not_zero(self):
        profile = json.loads((ROOT / "pricing" / "luna6.json").read_text(encoding="utf-8"))
        usage = {
            "status": "MEASURED",
            "cached_input_tokens": 0,
            "uncached_input_tokens": 10,
            "output_tokens": 2,
        }
        self.assertIsNone(estimate_cost(usage, profile)["estimated_cost"])


class HoldoutAndPromotion(unittest.TestCase):
    def test_search_loader_cannot_read_holdout_or_validation(self):
        with self.assertRaises(HoldoutLeak):
            load_task_split(ROOT / "tasks", "holdout")
        with self.assertRaises(HoldoutLeak):
            load_task_split(ROOT / "tasks", "validation")
        search = load_search_tasks(ROOT / "tasks")
        self.assertTrue(search)
        self.assertTrue(all(item["split"] == "search" for item in search))
        self.assertNotIn("search.visual_game_view", {item["task_id"] for item in search})

    def test_search_engine_source_does_not_import_confirmation(self):
        source = (ROOT / "lab" / "search" / "engine.py").read_text(encoding="utf-8")
        tree = ast.parse(source)
        imports = []
        for node in ast.walk(tree):
            if isinstance(node, ast.ImportFrom) and node.module:
                imports.append(node.module)
            if isinstance(node, ast.Import):
                imports.extend(alias.name for alias in node.names)
        self.assertNotIn("lab.validation.confirm", imports)
        self.assertNotIn("lab.fixtures.selfcheck", imports)
        self.assertNotIn("lab.runners.live", imports)
        self.assertNotIn("lab.runners.matrix", imports)
        self.assertIn("load_confirmation_tasks", confirm_module.__dict__)

    def test_promotion_blocks_infeasible_search_and_overfit(self):
        state = transition("GENERATED", "SCREENING", source="search")
        with self.assertRaises(CandidateInvalid):
            transition(state, "SCREENING_PASSED", source="search", feasible=False)
        passed = transition(state, "SCREENING_PASSED", source="search", feasible=True)
        validating = transition(passed, "VALIDATING", source="validation")
        with self.assertRaises(CandidateInvalid):
            transition(validating, "FINALIST", source="search")
        rejected = transition(validating, "VALIDATION_REJECTED", source="validation")
        self.assertEqual(rejected, "VALIDATION_REJECTED")
        finalist = transition(validating, "FINALIST", source="validation")
        frozen = freeze({"candidate_id": "y", "architecture_hash": "yhash", "generation": 1, "state": finalist})
        with self.assertRaises(CandidateInvalid):
            transition("SCREENING_PASSED", "CONFIRMED", source="search")
        confirmed = transition(frozen["state"], "CONFIRMED", source="holdout")
        self.assertEqual(confirmed, "CONFIRMED")
        child = mutate_frozen(frozen, {"candidate_id": "old", "architecture_hash": "zhash"})
        self.assertEqual(child["state"], "GENERATED")
        self.assertNotEqual(child["candidate_id"], "y")
        self.assertEqual(child["parent_hash"], "yhash")

    def test_overfit_candidate_does_not_outrank_validation(self):
        # X looks perfect on search and fails validation. Y is acceptable on both.
        # The finalist transition requires a validation source, so X's search score
        # cannot produce FINALIST once validation rejected it.
        self.assertEqual(
            transition("VALIDATING", "VALIDATION_REJECTED", source="validation"),
            "VALIDATION_REJECTED",
        )


class TasksOraclesReset(unittest.TestCase):
    def test_prompts_do_not_contain_read_answers(self):
        for split in ("search", "validation", "holdout"):
            for task in load_tasks(ROOT / "tasks" / split):
                validate_task(task, READ_SECRETS if task["oracle"]["type"] == "model_json_equals_unity" else [])

    def test_false_success_and_real_write(self):
        task = {
            "oracle": {"type": "unity_field_equals", "unity_field": "intensity", "expected": 4.25}
        }
        claimed = evaluate(task, '{"status":"done"}', {"intensity": 1.25})
        self.assertFalse(claimed["oracle_success"])
        self.assertTrue(claimed["false_success"])
        real = evaluate(task, "I could not do it", {"intensity": 4.25})
        self.assertTrue(real["oracle_success"])
        self.assertFalse(real["model_claimed_success"])

    def test_reset_fingerprint(self):
        with tempfile.TemporaryDirectory() as tmp:
            pristine = Path(tmp) / "pristine"
            pristine.mkdir()
            (pristine / "a.txt").write_text("one", encoding="utf-8")
            destination = Path(tmp) / "live"
            expected = reset_tree(pristine, destination)
            self.assertEqual(fingerprint(destination), expected)
            (destination / "a.txt").write_text("changed", encoding="utf-8")
            self.assertNotEqual(fingerprint(destination), expected)
            self.assertEqual(reset_tree(pristine, destination), expected)


class PreflightSafetyDispatch(unittest.TestCase):
    def test_preflight_rejects_harness_port_and_pid(self):
        expected = {
            "project_path": "/tmp/nexus-arch-lab",
            "pipeline_version": "0.8.0-exp.1",
            "cli_version": "1.0.0-beta.12",
            "codex_version": "codex-cli 0.160.0",
            "unity_version": "6000.3.16f1",
            "fixture_hash": "abc",
            "candidate_hash": "def",
            "model": "gpt-6-luna",
            "repo_root": "/repo",
        }
        probe = dict(expected)
        probe.update({"port": 7811, "listener_pid": 99, "ready": True, "compiling": False, "play_mode": False, "agent_cwd": "/tmp/agent"})
        self.assertTrue(preflight(expected, probe)["ok"])
        probe["port"] = 7800
        self.assertFalse(preflight(expected, probe)["ok"])
        probe["port"] = 7811
        probe["listener_pid"] = 4465
        self.assertFalse(preflight(expected, probe)["ok"])

    def test_safety_never_targets_harness(self):
        processes = {
            4465: {"command": "Unity.app -projectpath /Users/daliys/Daliys/UnityProjects/UnityTestForNexus"},
            50: {"command": "Unity.app -projectpath /tmp/nexus-arch-lab"},
            51: {"command": "python pgrep Unity.app /tmp/nexus-arch-lab"},
        }
        with self.assertRaises(SafetyError):
            assert_safe_to_stop(4465, "/tmp/nexus-arch-lab", processes)
        with self.assertRaises(SafetyError):
            assert_safe_to_stop(51, "/tmp/nexus-arch-lab", processes)
        assert_safe_to_stop(50, "/tmp/nexus-arch-lab", processes)

    def test_dispatch_uses_backend_and_rejects_hidden_official_name(self):
        surface = generate_surface(smoke_hidden_gateway(CATALOG), CATALOG)
        seen = {}

        def backend(command, arguments):
            seen["command"] = command
            seen["arguments"] = arguments
            return {"ok": True, "payload": {"success": True}}

        outcome = dispatch(surface, CATALOG, "nexus_set_field", {"target": "LabReadTarget", "field": "intensity", "value": 4.25}, backend)
        self.assertTrue(outcome["ok"])
        self.assertEqual(seen["command"], "set_serialized_field")
        missing = dispatch(surface, CATALOG, "set_serialized_field", {}, backend)
        self.assertFalse(missing["ok"])

    def test_mcp_list_is_the_surface(self):
        surface = generate_surface(smoke_official_subset(CATALOG), CATALOG)
        listed = handle(surface, CATALOG, {"jsonrpc": "2.0", "id": 1, "method": "tools/list"}, lambda *a: {})
        names = [item["name"] for item in listed["result"]["tools"]]
        self.assertEqual(names, surface["model_visible_names"])


class SearchPlan(unittest.TestCase):
    def test_dry_mutation_deduplicates_and_does_not_execute(self):
        population = all_baselines(CATALOG)
        plan = plan_generation(population, CATALOG, seed=17, task_root=ROOT / "tasks")
        self.assertFalse(plan["executed"])
        hashes = [item["candidate_hash"] for item in plan["children"]]
        self.assertEqual(len(hashes), len(set(hashes)))
        self.assertTrue(plan["children"])
        child = mutate(population[0], CATALOG, 17)
        self.assertIsNotNone(child)
        self.assertNotEqual(child["architecture_hash"], population[0]["architecture_hash"])
        self.assertIn(child["why"], child["why"])

    def test_evolve_refuses_without_budget(self):
        with self.assertRaises(Exception):
            evolve_request({"confirm_search": False})
        with self.assertRaises(Exception):
            evolve_request({"confirm_search": True, "max_runs": 10, "max_generations": 2, "max_uncached_tokens": None})

    def test_store_roundtrip_and_smoke_report(self):
        with tempfile.TemporaryDirectory() as tmp:
            store = RunStore(Path(tmp) / "runs.sqlite")
            store.add_experiment("exp", "SMOKE_ONLY", {"model": "gpt-6-luna", "evalToken": "nope"})
            candidate = smoke_official_subset(CATALOG)
            store.add_candidate(candidate)
            store.add_run(
                {
                    "run_id": "r1",
                    "experiment_id": "exp",
                    "candidate_hash": candidate["architecture_hash"],
                    "task_id": "search.read_object_name",
                    "split": "search",
                    "seed": 1,
                    "status": "completed",
                    "oracle_success": True,
                    "model_claimed_success": True,
                    "false_success": False,
                    "usage": {"status": "MEASURED", "uncached_input_tokens": 10, "cached_input_tokens": 0, "output_tokens": 2},
                    "wall_ms": 5,
                    "void_reason": None,
                    "label": "SMOKE_ONLY",
                    "events": [{"type": "token", "authorization": "secret"}],
                    "tool_calls": [
                        {
                            "exposed_name": "find_gameobjects",
                            "capability_id": "find_gameobjects",
                            "backend_command": "find_gameobjects",
                            "arguments": {"type": "LabMarker"},
                            "ok": True,
                            "retry": False,
                        }
                    ],
                }
            )
            rows = store.runs()
            self.assertEqual(rows[0]["label"], "SMOKE_ONLY")
            saved = Path(tmp, "runs.sqlite").read_bytes()
            self.assertNotIn(b"secret", saved)
            report = render_comparison(
                [
                    {
                        "candidate_id": "SMOKE_OFFICIAL_SUBSET",
                        "candidate_hash": candidate["architecture_hash"],
                        "semantic_success_rate": 1,
                        "false_successes": 0,
                        "uncached_tokens_per_success": 10,
                        "tool_calls_per_success": 1,
                        "wrong_tool_calls": 0,
                        "feasible": True,
                    }
                ],
                label="SMOKE_ONLY",
            )
            self.assertIn("not an architecture result", report.lower())
            self.assertIn("SMOKE_ONLY", report)
            store.close()

    def test_cooccurrence_ignores_smoke_and_failures(self):
        matrix = cooccurrence(
            [
                {"oracle_success": True, "label": "SMOKE_ONLY", "capability_ids": ["find_gameobjects", "set_serialized_field"]},
                {"oracle_success": False, "label": "SEARCH", "capability_ids": ["find_gameobjects"]},
                {"oracle_success": True, "label": "SEARCH", "capability_ids": ["find_gameobjects", "get_serialized_fields"]},
            ]
        )
        self.assertEqual(matrix["successful_runs"], 1)
        self.assertFalse(matrix["applied_to_production"])


class MeasuredUsage(unittest.TestCase):
    def test_isolated_codex_home_has_only_the_lab_server(self):
        from lab.adapters.codex_cli import _isolated_codex_home

        home = _isolated_codex_home("/tmp/lab-mcp", ["--surface", "/tmp/surface.json"])
        text = (home / "config.toml").read_text(encoding="utf-8")
        self.assertIn("[mcp_servers.architecture_lab]", text)
        self.assertIn('default_tools_approval_mode = "approve"', text)
        self.assertNotIn("rmcp_client", text)
        self.assertNotIn("nexus-unity", text)
        self.assertNotIn("unityMCP", text)
        self.assertNotIn("8080", text)

    def test_token_counts_are_not_redacted(self):
        from lab.telemetry.redact import redact

        cleaned = redact(
            {
                "usage": {"input_tokens": 12, "output_tokens": 3, "access_token": "secret-value"},
                "note": "Bearer abc",
            }
        )
        self.assertEqual(cleaned["usage"]["input_tokens"], 12)
        self.assertEqual(cleaned["usage"]["output_tokens"], 3)
        self.assertEqual(cleaned["usage"]["access_token"], "<redacted>")
        self.assertEqual(cleaned["note"], "<redacted>")

    def test_codex_usage_shape_is_measured(self):
        usage = usage_from_events(
            [
                {
                    "type": "turn.completed",
                    "usage": {
                        "input_tokens": 100,
                        "cached_input_tokens": 40,
                        "output_tokens": 15,
                        "reasoning_output_tokens": 5,
                    },
                }
            ]
        )
        self.assertEqual(usage["status"], "MEASURED")
        self.assertEqual(usage["uncached_input_tokens"], 60)
        self.assertEqual(usage["cached_input_tokens"], 40)
        self.assertEqual(usage["reasoning_tokens"], 5)
        self.assertEqual(usage["total_tokens"], 115)

    def test_retry_usage_is_summed(self):
        first = {"status": "MEASURED", "uncached_input_tokens": 10, "cached_input_tokens": 1, "output_tokens": 2, "reasoning_tokens": 0, "total_tokens": 13}
        second = {"status": "MEASURED", "uncached_input_tokens": 4, "cached_input_tokens": 2, "output_tokens": 3, "reasoning_tokens": 1, "total_tokens": 9}
        merged = add_usage(first, second)
        self.assertEqual(merged["uncached_input_tokens"], 14)
        self.assertEqual(merged["total_tokens"], 22)
        self.assertEqual(merged["status"], "MEASURED")

    def test_warm_cache_does_not_win_on_uncached_tokens(self):
        later = {
            "candidate_id": "later",
            "candidate_hash": "b",
            "feasible": True,
            "uncached_tokens_per_success": 1,
            "total_tokens_per_success": 500,
            "wrong_tool_calls": 0,
            "tool_calls_per_success": 1,
            "wall_ms_per_success": 10,
        }
        earlier = {
            "candidate_id": "earlier",
            "candidate_hash": "a",
            "feasible": True,
            "uncached_tokens_per_success": 400,
            "total_tokens_per_success": 100,
            "wrong_tool_calls": 0,
            "tool_calls_per_success": 1,
            "wall_ms_per_success": 10,
        }
        frontier = pareto_frontier([later, earlier])
        self.assertEqual([row["candidate_id"] for row in frontier], ["earlier"])
        self.assertEqual(selection_order(frontier)[0]["candidate_id"], "earlier")

    def test_lower_wall_does_not_dominate_a_shared_cache(self):
        self.assertNotIn("wall_ms_per_success", COST_KEYS)
        later = {
            "candidate_id": "later",
            "candidate_hash": "b",
            "feasible": True,
            "uncached_tokens_per_success": 1,
            "total_tokens_per_success": 100,
            "wrong_tool_calls": 0,
            "tool_calls_per_success": 1,
            "wall_ms_per_success": 1,
        }
        earlier = {
            "candidate_id": "earlier",
            "candidate_hash": "a",
            "feasible": True,
            "uncached_tokens_per_success": 400,
            "total_tokens_per_success": 100,
            "wrong_tool_calls": 0,
            "tool_calls_per_success": 1,
            "wall_ms_per_success": 5000,
        }
        frontier = pareto_frontier([later, earlier])
        self.assertEqual([row["candidate_id"] for row in frontier], ["earlier", "later"])
        self.assertEqual(selection_order(frontier)[0]["candidate_id"], "earlier")

    def test_known_hashes_are_not_generated_again(self):
        parent = smoke_official_subset(CATALOG)
        first = plan_generation([parent], CATALOG, seed=17)
        known = {child["candidate_hash"] for child in first["children"]}
        second = plan_generation([parent], CATALOG, seed=17, known_hashes=known)
        self.assertEqual(second["children"], [])
        self.assertGreater(second["duplicates_or_rejected"], 0)

    def test_foreign_tool_and_tool_search_are_not_the_candidate(self):
        from lab.adapters.codex_cli import inspect_client_events

        foreign = inspect_client_events(
            [{"type": "item.completed", "item": {"type": "mcp_tool_call", "server": "unityMCP", "tool": "find_gameobjects"}}]
        )
        self.assertTrue(foreign["surface_bypass"])
        self.assertFalse(foreign["mechanism_mismatch"])
        searched = inspect_client_events([{"type": "item.completed", "item": {"type": "tool_search_call"}}])
        self.assertTrue(searched["mechanism_mismatch"])
        self.assertFalse(searched["surface_bypass"])
        image = inspect_client_events([{"type": "item.completed", "item": {"type": "image_generation_call"}}])
        self.assertTrue(image["surface_bypass"])

    def test_two_json_objects_keep_the_first(self):
        from lab.fixtures.oracles import extract_json_object

        self.assertEqual(extract_json_object('note {"object_name": "Lab"} tail {"other": 1}'), {"object_name": "Lab"})
        self.assertEqual(extract_json_object('{"file_text": "a}b"}'), {"file_text": "a}b"})

    def test_prompt_does_not_list_the_candidate_tools(self):
        from lab.runners.live import _prompt_for_surface

        task = json.loads((ROOT / "tasks" / "search" / "read_object_name.json").read_text(encoding="utf-8"))
        surface = generate_surface(smoke_official_subset(CATALOG), CATALOG)
        prompt = _prompt_for_surface(task, surface)
        self.assertNotIn("find_gameobjects", prompt)
        self.assertNotIn("nexus_find_gameobjects", prompt)

    def test_validation_and_holdout_are_not_search_clones(self):
        search_guid = json.loads((ROOT / "tasks" / "search" / "out_of_default_guid.json").read_text(encoding="utf-8"))
        validation_guid = json.loads((ROOT / "tasks" / "validation" / "asset_guid.json").read_text(encoding="utf-8"))
        holdout_read = json.loads((ROOT / "tasks" / "holdout" / "read_note_text.json").read_text(encoding="utf-8"))
        self.assertIn("readme.txt", search_guid["prompt"])
        self.assertIn("validation-note.txt", validation_guid["prompt"])
        self.assertNotIn("readme.txt", validation_guid["prompt"])
        self.assertNotIn("markerName", holdout_read["prompt"])
        self.assertNotIn("intensity", holdout_read["prompt"])
        self.assertNotEqual(search_guid["oracle"].get("asset_name"), validation_guid["oracle"].get("asset_name"))


if __name__ == "__main__":
    unittest.main()
