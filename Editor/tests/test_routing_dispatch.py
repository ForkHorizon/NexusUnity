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
            with patch("nexus_bridge._transport.call_unity") as mock_call_unity:
                response: dict[str, Any] = routing.route_tool("synthetic", {"value": 7})

        self.assertEqual({"result": {"handled": True}}, response)
        handler.assert_called_once_with({"value": 7})
        mock_call_unity.assert_not_called()

    def test_route_tool_falls_back_to_call_unity_for_unknown_route(self) -> None:
        with patch("nexus_bridge._transport.call_unity", return_value={"result": {"ok": True}}) as mock_call_unity:
            response: dict[str, Any] = routing.route_tool("invoke_method", {"instance_id": 42})

        self.assertEqual({"result": {"ok": True}}, response)
        mock_call_unity.assert_called_once_with("invoke_method", {"instance_id": 42})

    def test_route_tool_dispatches_to_expected_unity_methods(self) -> None:
        test_cases: list[tuple[str, dict[str, Any], str, tuple[Any, ...]]] = [
            (
                "scene_manager",
                {"action": "open", "path": "Assets/TestScene.unity"},
                "open_scene",
                ({"path": "Assets/TestScene.unity"},),
            ),
            (
                "hierarchy_manager",
                {"action": "destroy", "instance_id": 42},
                "destroy_game_object",
                ({"instance_id": 42},),
            ),
            (
                "hierarchy_manager",
                {"action": "set_sibling_index", "instance_id": 42, "index": 2},
                "set_sibling_index",
                ({"instance_id": 42, "index": 2},),
            ),
            (
                "component_manager",
                {"action": "add", "instance_id": 42, "component_name": "BoxCollider"},
                "add_component",
                ({"instance_id": 42, "component_name": "BoxCollider"},),
            ),
            (
                "search_manager",
                {"strategy": "path", "query": "/Canvas/Button"},
                "find_by_path",
                ({"path": "/Canvas/Button"},),
            ),
            ("asset_manager", {"action": "refresh"}, "refresh_asset_database", tuple()),
            ("editor_controller", {"action": "get_state"}, "get_editor_state", tuple()),
            ("ui_automation", {"action": "list_windows"}, "ui_list_windows", tuple()),
            ("playerprefs_manager", {"action": "list"}, "list_player_prefs", tuple()),
        ]

        for route_name, route_args, unity_method, unity_params in test_cases:
            with self.subTest(route_name=route_name):
                with patch(
                    "nexus_bridge._transport.call_unity", return_value={"result": {"ok": True}}
                ) as mock_call_unity:
                    response: dict[str, Any] = routing.route_tool(route_name, route_args)

                self.assertEqual({"result": {"ok": True}}, response)
                mock_call_unity.assert_called_once_with(unity_method, *unity_params)

    def test_apply_created_transform_accepts_zero_instance_id(self) -> None:
        response: dict[str, Any] = {"result": {"data": {"instance_id": 0}}}
        args: dict[str, Any] = {"position": {"x": 1, "y": 2, "z": 3}}

        with patch("nexus_bridge._transport.call_unity", return_value={"result": {"ok": True}}) as mock_call_unity:
            returned = routing._apply_created_transform(response, args)

        self.assertIs(returned, response)
        mock_call_unity.assert_called_once_with(
            "set_transform",
            {"instance_id": 0, "position": {"x": 1, "y": 2, "z": 3}},
        )


class RouteHandlerTests(unittest.TestCase):
    def test_scene_manager_create_alias_routes_to_create_scene(self) -> None:
        with patch("nexus_bridge._transport.call_unity", return_value={"result": {"ok": True}}) as mock_call_unity:
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

        with patch("nexus_bridge.routes_editor._run_tests_wait", return_value=expected_response) as mock_run_tests_wait:
            response: dict[str, Any] = routing._route_editor_controller(
                {"action": "run_tests_wait", "timeout_seconds": 9}
            )

        self.assertEqual(expected_response, response)
        mock_run_tests_wait.assert_called_once_with({"action": "run_tests_wait", "timeout_seconds": 9})

    def test_playerprefs_delete_forwards_confirm(self) -> None:
        with patch("nexus_bridge._transport.call_unity", return_value={"result": {"ok": True}}) as mock_call_unity:
            response: dict[str, Any] = routing._route_playerprefs_manager(
                {"action": "delete", "key": "all", "confirm": True}
            )

        self.assertEqual({"result": {"ok": True}}, response)
        mock_call_unity.assert_called_once_with("delete_player_pref", {"key": "all", "confirm": True})

    def test_hierarchy_manager_create_applies_transform_after_create(self) -> None:
        create_response: dict[str, Any] = {"result": {"data": {"instance_id": 12}}}
        transform_response: dict[str, Any] = {"result": {"ok": True}}

        with patch(
            "nexus_bridge._transport.call_unity", side_effect=[create_response, transform_response]
        ) as mock_call_unity:
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


if __name__ == "__main__":
    unittest.main()
