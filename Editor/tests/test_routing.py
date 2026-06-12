from __future__ import annotations

import os
import sys
import unittest
from typing import Any
from unittest.mock import call, patch

EDITOR_DIR: str = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if EDITOR_DIR not in sys.path:
    sys.path.insert(0, EDITOR_DIR)

from nexus_bridge import routing


class RouteToolDispatchTests(unittest.TestCase):
    def test_route_tool_dispatches_to_expected_unity_methods(self) -> None:
        test_cases: list[tuple[str, dict[str, Any], str, tuple[Any, ...]]] = [
            ("scene_manager", {"action": "open", "path": "Assets/TestScene.unity"}, "open_scene", ({"path": "Assets/TestScene.unity"},)),
            ("hierarchy_manager", {"action": "destroy", "instance_id": 42}, "destroy_game_object", ({"instance_id": 42},)),
            ("hierarchy_manager", {"action": "set_sibling_index", "instance_id": 42, "index": 2}, "set_sibling_index", ({"instance_id": 42, "index": 2},)),
            ("component_manager", {"action": "add", "instance_id": 42, "component_name": "BoxCollider"}, "add_component", ({"instance_id": 42, "component_name": "BoxCollider"},)),
            ("search_manager", {"strategy": "path", "query": "/Canvas/Button"}, "find_by_path", ({"path": "/Canvas/Button"},)),
            ("asset_manager", {"action": "refresh"}, "refresh_asset_database", tuple()),
            ("editor_controller", {"action": "get_state"}, "get_editor_state", tuple()),
            ("ui_automation", {"action": "list_windows"}, "ui_list_windows", tuple()),
            ("playerprefs_manager", {"action": "list"}, "list_player_prefs", tuple()),
        ]

        for route_name, route_args, unity_method, unity_params in test_cases:
            with self.subTest(route_name=route_name):
                with patch("nexus_bridge.routing.call_unity", return_value={"result": {"ok": True}}) as mock_call_unity:
                    response: dict[str, Any] = routing.route_tool(route_name, route_args)

                self.assertEqual({"result": {"ok": True}}, response)
                mock_call_unity.assert_called_once_with(unity_method, *unity_params)

    def test_apply_created_transform_accepts_zero_instance_id(self) -> None:
        response: dict[str, Any] = {"result": {"data": {"instance_id": 0}}}
        args: dict[str, Any] = {"position": {"x": 1, "y": 2, "z": 3}}

        with patch("nexus_bridge.routing.call_unity", return_value={"result": {"ok": True}}) as mock_call_unity:
            returned = routing._apply_created_transform(response, args)

        self.assertIs(returned, response)
        mock_call_unity.assert_called_once_with(
            "set_transform",
            {"instance_id": 0, "position": {"x": 1, "y": 2, "z": 3}},
        )


class RunTestsWaitTests(unittest.TestCase):
    def test_run_tests_wait_returns_success_when_new_results_appear(self) -> None:
        call_results: list[dict[str, Any]] = [
            {"result": {"status": "Success", "timestamp_utc": "2026-06-11T00:00:00Z"}},
            {"result": {"result_path": "/tmp/TestResults.xml"}},
            {"result": {"status": "Success", "timestamp_utc": "2026-06-11T00:00:03Z"}},
        ]

        with patch("nexus_bridge.routing.call_unity", side_effect=call_results) as mock_call_unity:
            with patch("nexus_bridge.routing.time.time", side_effect=[100.0, 100.0, 103.25]):
                with patch("nexus_bridge.routing.time.sleep") as mock_sleep:
                    response: dict[str, Any] = routing._run_tests_wait({})

        self.assertEqual("Success", response["result"]["status"])
        self.assertEqual(3.25, response["result"]["time_waited_seconds"])
        self.assertEqual("2026-06-11T00:00:03Z", response["result"]["timestamp_utc"])
        mock_sleep.assert_not_called()
        mock_call_unity.assert_has_calls(
            [
                call("get_test_results"),
                call("run_tests", {"mode": "EditMode"}),
                call("get_test_results", {"result_path": "/tmp/TestResults.xml"}),
            ]
        )

    def test_run_tests_wait_returns_timeout_when_results_do_not_change(self) -> None:
        call_results: list[dict[str, Any]] = [
            {"result": {"status": "Success", "timestamp_utc": "2026-06-11T00:00:00Z"}},
            {"result": {"result_path": "/tmp/TestResults.xml"}},
            {"result": {"status": "Success", "timestamp_utc": "2026-06-11T00:00:00Z"}},
        ]

        with patch("nexus_bridge.routing.call_unity", side_effect=call_results):
            with patch("nexus_bridge.routing.time.time", side_effect=[0.0, 0.0, 5.1, 5.1]):
                with patch("nexus_bridge.routing.time.sleep") as mock_sleep:
                    response: dict[str, Any] = routing._run_tests_wait({"timeout_seconds": 5.0})

        self.assertEqual("Timeout", response["result"]["status"])
        self.assertEqual("/tmp/TestResults.xml", response["result"]["result_path"])
        self.assertEqual(5.1, response["result"]["time_waited_seconds"])
        self.assertEqual("Timed out waiting for a new Unity TestResults XML file.", response["result"]["message"])
        mock_sleep.assert_called_once_with(1.0)


class WaitForCompilationTests(unittest.TestCase):
    def test_wait_for_compilation_returns_ready_when_editor_finishes_compiling(self) -> None:
        call_results: list[dict[str, Any]] = [
            {"error": {"code": -32000, "message": "reload in progress"}},
            {"result": {}},
            {"result": {"is_compiling": False, "is_updating": False}},
        ]

        with patch("nexus_bridge.routing.call_unity", side_effect=call_results) as mock_call_unity:
            with patch("nexus_bridge.routing.time.time", side_effect=[100.0, 100.0, 101.0, 103.0]):
                with patch("nexus_bridge.routing.time.sleep") as mock_sleep:
                    response: dict[str, Any] = routing._wait_for_compilation(timeout=30.0)

        self.assertEqual({"result": {"status": "Ready", "time_waited_seconds": 3.0}}, response)
        self.assertEqual(
            [
                call("initialize"),
                call("initialize"),
                call("get_editor_state"),
            ],
            mock_call_unity.call_args_list,
        )
        mock_sleep.assert_has_calls([call(2.0)])

    def test_wait_for_compilation_returns_timeout_after_refresh(self) -> None:
        call_results: list[dict[str, Any]] = [
            {"result": {}},
            {"result": {}},
        ]

        with patch("nexus_bridge.routing.call_unity", side_effect=call_results) as mock_call_unity:
            with patch("nexus_bridge.routing.time.time", side_effect=[0.0, 0.0, 21.0, 21.0, 22.0]):
                with patch("nexus_bridge.routing.time.sleep") as mock_sleep:
                    response: dict[str, Any] = routing._wait_for_compilation(timeout=10.0)

        self.assertEqual({"result": {"status": "Timeout", "time_waited_seconds": 22.0}}, response)
        self.assertEqual(
            [
                call("initialize"),
                call("refresh_asset_database"),
            ],
            mock_call_unity.call_args_list,
        )
        mock_sleep.assert_called_once_with(0.5)

    def test_wait_route_delegates_compilation_condition_to_helper(self) -> None:
        expected_response: dict[str, Any] = {"result": {"status": "Ready", "time_waited_seconds": 4.5}}

        with patch("nexus_bridge.routing._wait_for_compilation", return_value=expected_response) as mock_wait_for_compilation:
            with patch("nexus_bridge.routing.time.time", return_value=55.0):
                response: dict[str, Any] = routing.route_tool("wait", {"condition": "compilation", "timeout_seconds": 12.0})

        self.assertEqual(expected_response, response)
        mock_wait_for_compilation.assert_called_once_with(timeout=12.0, start_time=55.0)


if __name__ == "__main__":
    unittest.main()
