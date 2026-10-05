"""Matrix, reset, and self-check tests. No model and no Unity process."""

from __future__ import annotations

import io
import json
import tempfile
import unittest
from pathlib import Path

from lab.adapters.mcp_server import serve
from lab.adapters.unity_cli import UnityCliBackend, parse_unity_stdout
from lab.candidates.baselines import smoke_hidden_gateway, smoke_official_subset
from lab.catalog.extract import load_catalog
from lab.fixtures.observe import _compilation_failed, extract_cs, observe_state, pristine_match
from lab.fixtures.selfcheck import check_task
from lab.runners.matrix import run_smoke_matrix
from lab.search.feasibility import classify_candidate
from lab.surfaces.generate import generate_surface
from lab.telemetry.calls import summarize_calls
from lab.telemetry.store import RunStore

ROOT = Path(__file__).resolve().parents[1]
CATALOG = load_catalog(ROOT / "catalog" / "snapshots" / "pipeline-0.8.0-exp.1.json")


class UnityStdout(unittest.TestCase):
    def test_two_json_bodies_keep_a_result(self):
        text = '{"success": false}\n{"success": true, "data": {"status": "ready"}}'
        parsed = parse_unity_stdout(text)
        self.assertTrue(parsed["success"])
        self.assertEqual(parsed["data"]["status"], "ready")
        self.assertGreaterEqual(parsed["_body_count"], 2)

    def test_serialized_field_descriptor_unwraps_the_value(self):
        from lab.fixtures.reference import _value

        payload = {
            "type": "LabMarker",
            "fields": [{"name": "intensity", "propertyType": "Float", "value": 1.25}],
        }
        self.assertEqual(_value(payload, "intensity"), 1.25)
        self.assertEqual(_value({"value": 4.25}, "intensity"), 4.25)

    def test_string_field_value_is_sent_as_json(self):
        backend = UnityCliBackend("/tmp/unity-cli-beta12/unity", "/tmp/nexus-arch-lab", 7811)
        quoted = backend.argv(
            "set_serialized_field",
            {"target": "LabReadTarget", "component": "LabMarker", "field": "markerName", "value": "holdout-ok"},
        )
        self.assertIn("holdout-ok", quoted)
        self.assertNotIn('"holdout-ok"', quoted)
        number = backend.argv(
            "set_serialized_field",
            {"target": "LabReadTarget", "component": "LabMarker", "field": "intensity", "value": 4.25},
        )
        self.assertIn("4.25", number)
        self.assertNotIn('"4.25"', number)

    def test_console_flag_comes_from_ground_truth(self):
        result = {
            "ok": True,
            "payload": {"success": True, "data": {"counts": {"error": 75}, "groundTruth": {"compilationFailed": False}}},
        }
        self.assertIs(_compilation_failed(result), False)
        self.assertIsNone(_compilation_failed({"ok": True, "payload": {"success": True, "data": {"counts": {"error": 1}}}}))

    def test_write_read_does_not_reopen_the_scene(self):
        calls = []

        def backend(command, arguments):
            calls.append(command)
            if command == "find_gameobjects":
                return {"ok": True, "payload": {"success": True, "data": {"gameObjects": [{"hierarchyPath": "LabReadTarget"}]}}}
            if command == "get_serialized_fields":
                return {
                    "ok": True,
                    "payload": {"success": True, "data": {"fields": [{"name": arguments["field"], "value": 4.25}]}},
                }
            if command == "editor_status":
                return {
                    "ok": True,
                    "payload": {"success": True, "data": {"status": "ready", "compiling": False, "playMode": "stopped"}},
                }
            return {"ok": True, "payload": {"success": True, "data": {}}}

        observed = observe_state(backend, reload_scene=False)
        self.assertNotIn("open_scene", calls)
        self.assertEqual(observed["intensity"], 4.25)

    def test_extracts_compiler_code_without_inventing_one(self):
        self.assertEqual(extract_cs({"data": {"errors": [{"message": "Assets/A.cs(1,1): error CS0103"}]}}), "CS0103")
        self.assertIsNone(extract_cs({"data": {"status": "idle"}}))


class FakeUnity:
    def __init__(self):
        self.intensity = 1.25
        self.revision = 7
        self.marker_name = "readable"
        self.calls = []

    def __call__(self, command, arguments):
        self.calls.append(command)
        if command == "create_scene":
            self.intensity = 1.25
            self.revision = 7
            self.marker_name = "readable"
        if command == "set_serialized_field":
            field = arguments["field"]
            if field == "intensity":
                self.intensity = float(arguments["value"])
            elif field == "revision":
                self.revision = int(arguments["value"])
            elif field == "markerName":
                self.marker_name = arguments["value"]
        if command == "recompile":
            return {"ok": False, "payload": {"success": False, "data": {"errors": [{"message": "error CS0103: missing"}]}}}
        if command == "recompile_status":
            return {"ok": True, "payload": {"success": True, "data": {"status": "idle"}}}
        if command == "get_serialized_fields":
            values = {"intensity": self.intensity, "revision": self.revision, "markerName": self.marker_name}
            return {"ok": True, "payload": {"success": True, "data": {"value": values[arguments["field"]]}}}
        if command == "find_gameobjects":
            return {
                "ok": True,
                "payload": {"success": True, "data": {"gameObjects": [{"hierarchyPath": "LabReadTarget", "type": "LabMarker"}]}},
            }
        if command == "editor_status":
            return {
                "ok": True,
                "payload": {
                    "success": True,
                    "data": {"status": "ready", "compiling": False, "playMode": "stopped", "projectPath": "/tmp/nexus-arch-lab"},
                },
            }
        return {"ok": True, "payload": {"success": True, "data": {}}}


class ForeignHooks(unittest.TestCase):
    def test_yard_open_is_parked_and_lab_reset_stays(self):
        from lab.runners.scratch import neutralize_foreign_hooks

        with tempfile.TemporaryDirectory() as tmp:
            editor = Path(tmp) / "Assets" / "Editor"
            editor.mkdir(parents=True)
            (editor / "YardOpen.cs").write_text("hook\n", encoding="utf-8")
            (editor / "YardOpen.cs.meta").write_text("meta\n", encoding="utf-8")
            (editor / "LabFixtureReset.cs").write_text("keep\n", encoding="utf-8")
            removed = neutralize_foreign_hooks(Path(tmp))
            self.assertEqual(removed, ["YardOpen.cs"])
            self.assertFalse((editor / "YardOpen.cs").exists())
            self.assertTrue((editor / "YardOpen.cs.off").is_file())
            self.assertTrue((editor / "LabFixtureReset.cs").is_file())
            self.assertEqual(neutralize_foreign_hooks(Path(tmp)), [])


class SelfCheck(unittest.TestCase):
    def test_write_reference_round_trip_and_visual_stays_invalid(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            pristine = root / "pristine"
            dest = root / "Assets" / "LabFixture"
            project = root
            pristine.mkdir()
            (pristine / "marker.txt").write_text("lab\n", encoding="utf-8")
            backend = FakeUnity()
            task = json.loads((ROOT / "tasks" / "search" / "write_intensity.json").read_text(encoding="utf-8"))
            result = check_task(task, backend, project=project, pristine=pristine, destination=dest, pause=lambda _s: None)
            self.assertEqual(result["status"], "VALID", result)
            self.assertEqual(backend.intensity, 1.25)
            self.assertEqual(pristine_match({
                "object_name": "LabReadTarget",
                "intensity": 1.25,
                "revision": 7,
                "marker_name": "readable",
                "play_mode": "stopped",
                "editor_status": "ready",
                "compiling": False,
            }), [])
            visual = json.loads((ROOT / "tasks" / "search" / "visual_game_view.json").read_text(encoding="utf-8"))
            self.assertEqual(check_task(visual, backend, project=project, pristine=pristine, destination=dest)["status"], "INVALID")

    def test_diagnose_reference_sees_the_compiler_code(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            pristine = root / "pristine"
            dest = root / "Assets" / "LabFixture"
            pristine.mkdir()
            (pristine / "marker.txt").write_text("lab\n", encoding="utf-8")
            backend = FakeUnity()
            task = json.loads((ROOT / "tasks" / "search" / "diagnose_compile.json").read_text(encoding="utf-8"))
            result = check_task(task, backend, project=root, pristine=pristine, destination=dest, pause=lambda _s: None)
            self.assertEqual(result["status"], "VALID", result)
            self.assertFalse((dest / "LabBrokenMarker.cs").exists())


class _Stream:
    def __init__(self, data: bytes = b""):
        self.buffer = io.BytesIO(data)


class Matrix(unittest.TestCase):
    def test_void_does_not_start_the_model_and_a_real_run_is_smoke_only(self):
        started = {"n": 0}

        def probe():
            return {
                "port": 7811,
                "listener_pid": 999,
                "project_path": "/tmp/nexus-arch-lab",
                "pipeline_version": "0.8.0-exp.1",
                "cli_version": "1.0.0-beta.12",
                "codex_version": "0.160.0",
                "unity_version": "6000.3.16f1",
                "fixture_hash": "abc",
                "candidate_hash": "ignored",
                "model": "gpt-6-luna",
                "compiling": False,
                "play_mode": False,
                "ready": True,
                "agent_cwd": "/tmp/empty",
            }

        def prepare(task):
            return {"ok": True, "fingerprint": "abc"}

        def codex_run(surface, task, run_id):
            started["n"] += 1
            name = surface["model_visible_names"][0]
            return {
                "status": "completed",
                "final_message": json.dumps({"object_name": "LabReadTarget", "status": "done"}),
                "events": [{"type": "turn.completed", "usage": {"input_tokens": 20, "output_tokens": 5, "cached_input_tokens": 0}}],
                "tool_calls": [{"exposed_name": "find_gameobjects" if "find_gameobjects" in surface["model_visible_names"] else name, "arguments": {}, "ok": True}],
                "backend_telemetry": [{"backend_command": "find_gameobjects", "ok": True}],
                "surface_bypass": False,
                "usage": {"status": "MEASURED", "uncached_input_tokens": 20, "cached_input_tokens": 0, "output_tokens": 5, "total_tokens": 25, "reasoning_tokens": None},
            }

        def observe():
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

        tasks = [json.loads((ROOT / "tasks" / "search" / "read_object_name.json").read_text(encoding="utf-8"))]
        candidates = [smoke_official_subset(CATALOG), smoke_hidden_gateway(CATALOG)]
        with tempfile.TemporaryDirectory() as tmp:
            store = RunStore(Path(tmp) / "runs.sqlite")
            bad = dict(probe())
            bad["port"] = 7800
            result = run_smoke_matrix(
                catalog=CATALOG,
                tasks=tasks,
                candidates=candidates,
                store=store,
                expected_probe=probe(),
                probe=lambda: bad,
                prepare_task=prepare,
                codex_run=codex_run,
                observe=observe,
                price_profile={"profile_id": "luna6-unpriced", "uncached_input_per_million": None, "cached_input_per_million": None, "output_per_million": None},
                seed=1,
                experiment_id="smoke-test",
                identity={"label": "SMOKE_ONLY"},
            )
            self.assertEqual(started["n"], 0)
            self.assertFalse(result["acceptance"]["ok"])
            self.assertTrue(all(row["status"] == "void" for row in result["runs"]))

            result = run_smoke_matrix(
                catalog=CATALOG,
                tasks=tasks,
                candidates=candidates,
                store=store,
                expected_probe=probe(),
                probe=probe,
                prepare_task=prepare,
                codex_run=codex_run,
                observe=observe,
                price_profile={"profile_id": "luna6-unpriced", "uncached_input_per_million": None, "cached_input_per_million": None, "output_per_million": None},
                seed=1,
                experiment_id="smoke-ok",
                identity={"label": "SMOKE_ONLY"},
            )
            self.assertEqual(started["n"], 2)
            self.assertTrue(result["acceptance"]["ok"], result["acceptance"])
            self.assertIn("SMOKE_ONLY", result["report"])
            self.assertNotIn("is better", result["report"])
            self.assertEqual(store.event_count(result["runs"][-1]["run_id"]), 1)
            self.assertTrue(result["runs"][-1]["unity_calls"] >= 1)
            store.close()

    def test_voided_task_stays_in_the_denominator(self):
        calls = {"n": 0}

        def probe():
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

        def prepare(task):
            if task["task_id"].endswith("write_intensity"):
                return {"ok": False, "fingerprint": "abc", "reason": "editor compiling"}
            return {"ok": True, "fingerprint": "abc"}

        def codex_run(surface, task, run_id):
            calls["n"] += 1
            return {
                "status": "completed",
                "final_message": json.dumps({"object_name": "LabReadTarget"}),
                "events": [{"usage": {"input_tokens": 10, "output_tokens": 2, "cached_input_tokens": 0}}],
                "tool_calls": [{"exposed_name": "find_gameobjects", "arguments": {}, "ok": True}],
                "backend_telemetry": [{"ok": True}],
                "usage": {"status": "MEASURED", "uncached_input_tokens": 10, "cached_input_tokens": 0, "output_tokens": 2, "total_tokens": 12},
            }

        def observe():
            return {"open_ok": True, "object_name": "LabReadTarget", "intensity": 1.25, "revision": 7, "marker_name": "readable", "play_mode": "stopped", "editor_status": "ready", "compiling": False}

        tasks = [
            json.loads((ROOT / "tasks" / "search" / name).read_text(encoding="utf-8"))
            for name in ("read_object_name.json", "write_intensity.json")
        ]
        with tempfile.TemporaryDirectory() as tmp:
            store = RunStore(Path(tmp) / "runs.sqlite")
            result = run_smoke_matrix(
                catalog=CATALOG,
                tasks=tasks,
                candidates=[smoke_official_subset(CATALOG)],
                store=store,
                expected_probe=probe(),
                probe=probe,
                prepare_task=prepare,
                codex_run=codex_run,
                observe=observe,
                price_profile={"uncached_input_per_million": None, "cached_input_per_million": None, "output_per_million": None},
                seed=1,
                experiment_id="void-denom",
                identity={"label": "SMOKE_ONLY"},
            )
            store.close()
        self.assertEqual(calls["n"], 1)
        self.assertFalse(result["rows"][0]["feasible"])
        self.assertLess(result["rows"][0]["semantic_success_rate"], 1)

    def test_surface_bypass_cannot_be_feasible(self):
        verdict = classify_candidate(
            {
                "coverage": 1.0,
                "attempted": 1,
                "oracle_successes": 1,
                "category_rates": {"read": 1.0},
                "usage_status": "MEASURED",
                "surface_bypasses": 1,
            },
            {"min_coverage": 1, "min_semantic_success": 1, "category_floors": {"read": 1}, "require_measured_usage": True},
        )
        self.assertFalse(verdict["feasible"])
        self.assertIn("surface_bypass", verdict["reasons"])

    def test_lab_invoke_counts_as_a_tool_call(self):
        surface = generate_surface(smoke_hidden_gateway(CATALOG), CATALOG)
        summary = summarize_calls(
            surface,
            [{"exposed_name": "lab_invoke", "arguments": {"name": "nexus_set_field"}, "backend_reached": True, "backend_command": "set_serialized_field"}],
            {"set_serialized_field"},
        )
        self.assertEqual(summary["tool_calls"], 1)
        self.assertEqual(summary["discovery_calls"], 1)
        self.assertEqual(summary["wrong_tool_calls"], 0)

    def test_mcp_content_length_lists_only_the_surface(self):
        surface = {
            "tools": [{"name": "nexus_set_field", "description": "set", "inputSchema": {"type": "object"}}],
            "lazy": [],
            "resolution": {},
        }
        message = {"jsonrpc": "2.0", "id": 1, "method": "tools/list"}
        body = json.dumps(message).encode("utf-8")
        raw = f"Content-Length: {len(body)}\r\n\r\n".encode("ascii") + body
        incoming = _Stream(raw)
        outgoing = _Stream()
        serve(surface, CATALOG, lambda command, arguments: {"ok": True}, incoming, outgoing)
        data = outgoing.buffer.getvalue()
        self.assertIn(b"nexus_set_field", data)
        self.assertNotIn(b"set_serialized_field", data)
