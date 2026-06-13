from __future__ import annotations

import os
import sys
import unittest
from typing import Any
from unittest.mock import Mock, call, patch

EDITOR_DIR: str = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if EDITOR_DIR not in sys.path:
    sys.path.insert(0, EDITOR_DIR)

from nexus_bridge import routing


class RouteToolDispatchTests(unittest.TestCase):
    def test_route_tool_uses_registered_handler(self) -> None:
        handler = Mock(return_value={"result": {"handled": True}})

        with patch.dict(routing._HANDLERS, {"synthetic": handler}, clear=True):
            with patch("nexus_bridge.routing.call_unity") as mock_call_unity:
                response: dict[str, Any] = routing.route_tool("synthetic", {"value": 7})

        self.assertEqual({"result": {"handled": True}}, response)
        handler.assert_called_once_with({"value": 7})
        mock_call_unity.assert_not_called()

    def test_route_tool_falls_back_to_call_unity_for_unknown_route(self) -> None:
        with patch("nexus_bridge.routing.call_unity", return_value={"result": {"ok": True}}) as mock_call_unity:
            response: dict[str, Any] = routing.route_tool("invoke_method", {"instance_id": 42})

        self.assertEqual({"result": {"ok": True}}, response)
        mock_call_unity.assert_called_once_with("invoke_method", {"instance_id": 42})

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


class RouteHandlerTests(unittest.TestCase):
    def test_scene_manager_create_alias_routes_to_create_scene(self) -> None:
        with patch("nexus_bridge.routing.call_unity", return_value={"result": {"ok": True}}) as mock_call_unity:
            response: dict[str, Any] = routing._route_scene_manager(
                {"action": "create_scene", "name": "Arena", "open_if_exists": True}
            )

        self.assertEqual({"result": {"ok": True}}, response)
        mock_call_unity.assert_called_once_with(
            "create_scene",
            {"name": "Arena", "open_if_exists": True},
        )

    def test_scene_manager_invalid_action_returns_error(self) -> None:
        response: dict[str, Any] = routing._route_scene_manager({"action": "delete"})

        self.assertEqual(-32602, response["error"]["code"])
        self.assertIn("Invalid action: delete", response["error"]["message"])

    def test_editor_controller_run_tests_wait_delegates_to_helper(self) -> None:
        expected_response: dict[str, Any] = {"result": {"status": "Success"}}

        with patch("nexus_bridge.routing._run_tests_wait", return_value=expected_response) as mock_run_tests_wait:
            response: dict[str, Any] = routing._route_editor_controller({"action": "run_tests_wait", "timeout_seconds": 9})

        self.assertEqual(expected_response, response)
        mock_run_tests_wait.assert_called_once_with({"action": "run_tests_wait", "timeout_seconds": 9})

    def test_hierarchy_manager_create_applies_transform_after_create(self) -> None:
        create_response: dict[str, Any] = {"result": {"data": {"instance_id": 12}}}
        transform_response: dict[str, Any] = {"result": {"ok": True}}

        with patch("nexus_bridge.routing.call_unity", side_effect=[create_response, transform_response]) as mock_call_unity:
            response: dict[str, Any] = routing._route_hierarchy_manager(
                {"action": "create", "name": "Cube", "position": {"x": 1, "y": 2, "z": 3}}
            )

        self.assertEqual(create_response, response)
        self.assertEqual(
            [
                call("create_game_object", {"name": "Cube"}),
                call("set_transform", {"instance_id": 12, "position": {"x": 1, "y": 2, "z": 3}}),
            ],
            mock_call_unity.call_args_list,
        )


class WriteAndCompileTests(unittest.TestCase):
    def test_write_and_compile_returns_write_errors_without_waiting(self) -> None:
        files = [
            {"path": "Assets/One.cs", "content": "one"},
            {"path": "Assets/Two.cs", "content": "two"},
        ]
        call_results: list[dict[str, Any] | None] = [
            None,
            {"error": {"code": -32000, "message": "disk full"}},
            {"result": {"ok": True}},
        ]

        with patch("nexus_bridge.routing.call_unity", side_effect=call_results) as mock_call_unity:
            with patch("nexus_bridge.routing._wait_for_compilation") as mock_wait_for_compilation:
                response: dict[str, Any] = routing._route_write_and_compile({"files": files})

        self.assertEqual("Failed", response["result"]["status"])
        self.assertEqual("Failed to write some files", response["result"]["message"])
        self.assertEqual(
            [{"path": "Assets/One.cs", "error": {"code": -32000, "message": "disk full"}}],
            response["result"]["errors"],
        )
        mock_wait_for_compilation.assert_not_called()
        self.assertEqual(
            [
                call("clear_logs"),
                call("write_file", {"path": "Assets/One.cs", "content": "one"}),
                call("write_file", {"path": "Assets/Two.cs", "content": "two"}),
            ],
            mock_call_unity.call_args_list,
        )

    def test_write_and_compile_waits_and_filters_compiler_errors(self) -> None:
        files = [{"path": "Assets/Test.cs", "content": "class Test {}"}]
        wait_response: dict[str, Any] = {"result": {"status": "Ready", "time_waited_seconds": 1.5}}
        log_response: dict[str, Any] = {
            "result": {
                "logs": [
                    {"Type": "Error", "Message": "compile failed"},
                    {"Type": "Assert", "Message": "assertion"},
                    {"Type": "Log", "Message": "ignore"},
                ]
            }
        }

        with patch("nexus_bridge.routing.time.time", return_value=10.0):
            with patch("nexus_bridge.routing.call_unity", side_effect=[None, {"result": {"ok": True}}, log_response]) as mock_call_unity:
                with patch("nexus_bridge.routing._wait_for_compilation", return_value=wait_response) as mock_wait_for_compilation:
                    response: dict[str, Any] = routing._route_write_and_compile({"files": files})

        self.assertEqual("Failed", response["result"]["status"])
        self.assertEqual(1.5, response["result"]["time_waited_seconds"])
        self.assertEqual(
            [
                {"Type": "Error", "Message": "compile failed"},
                {"Type": "Assert", "Message": "assertion"},
            ],
            response["result"]["compiler_errors"],
        )
        mock_wait_for_compilation.assert_called_once_with(timeout=90, start_time=10.0)
        self.assertEqual(
            [
                call("clear_logs"),
                call("write_file", {"path": "Assets/Test.cs", "content": "class Test {}"}),
                call("read_logs", {"count": 200}),
            ],
            mock_call_unity.call_args_list,
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

    def test_wait_for_compilation_respects_timeout_smaller_than_reload_probe_window(self) -> None:
        call_results: list[dict[str, Any]] = [
            {"result": {}},
            {"result": {}},
        ]

        with patch("nexus_bridge.routing.call_unity", side_effect=call_results) as mock_call_unity:
            with patch("nexus_bridge.routing.time.time", side_effect=[100.0, 100.0, 105.1, 105.1, 106.1]):
                with patch("nexus_bridge.routing.time.sleep") as mock_sleep:
                    response: dict[str, Any] = routing._wait_for_compilation(timeout=5.0)

        self.assertEqual({"result": {"status": "Timeout", "time_waited_seconds": 6.1}}, response)
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
