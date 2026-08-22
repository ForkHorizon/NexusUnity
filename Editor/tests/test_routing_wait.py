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


class WaitForCompilationTests(unittest.TestCase):
    def test_wait_for_compilation_returns_ready_when_editor_finishes_compiling(self) -> None:
        call_results: list[dict[str, Any]] = [
            {"error": {"code": -32000, "message": "reload in progress"}},
            {"result": {}},
            {"result": {"is_compiling": False, "is_updating": False}},
        ]

        with patch("nexus_bridge._transport.call_unity", side_effect=call_results) as mock_call_unity:
            with patch("nexus_bridge.routes_editor.time.time", side_effect=[100.0, 100.0, 101.0, 103.0]):
                with patch("nexus_bridge.routes_editor.time.sleep") as mock_sleep:
                    response: dict[str, Any] = routing._wait_for_compilation(timeout=30.0)

        self.assertEqual({"result": {"status": "Ready", "time_waited_seconds": 3.0}}, response)
        self.assertEqual(
            [call("initialize"), call("initialize"), call("get_editor_state")],
            mock_call_unity.call_args_list,
        )
        mock_sleep.assert_has_calls([call(2.0)])

    def test_wait_for_compilation_returns_timeout_after_refresh(self) -> None:
        call_results: list[dict[str, Any]] = [{"result": {}}, {"result": {}}]

        with patch("nexus_bridge._transport.call_unity", side_effect=call_results) as mock_call_unity:
            with patch("nexus_bridge.routes_editor.time.time", side_effect=[0.0, 0.0, 21.0, 21.0, 22.0]):
                with patch("nexus_bridge.routes_editor.time.sleep") as mock_sleep:
                    response: dict[str, Any] = routing._wait_for_compilation(timeout=10.0)

        self.assertEqual({"result": {"status": "Timeout", "time_waited_seconds": 22.0}}, response)
        self.assertEqual(
            [call("initialize"), call("refresh_asset_database")],
            mock_call_unity.call_args_list,
        )
        mock_sleep.assert_called_once_with(0.5)

    def test_wait_for_compilation_respects_timeout_smaller_than_reload_probe_window(self) -> None:
        call_results: list[dict[str, Any]] = [{"result": {}}, {"result": {}}]

        with patch("nexus_bridge._transport.call_unity", side_effect=call_results) as mock_call_unity:
            with patch("nexus_bridge.routes_editor.time.time", side_effect=[100.0, 100.0, 105.1, 105.1, 106.1]):
                with patch("nexus_bridge.routes_editor.time.sleep") as mock_sleep:
                    response: dict[str, Any] = routing._wait_for_compilation(timeout=5.0)

        self.assertEqual({"result": {"status": "Timeout", "time_waited_seconds": 6.1}}, response)
        self.assertEqual(
            [call("initialize"), call("refresh_asset_database")],
            mock_call_unity.call_args_list,
        )
        mock_sleep.assert_called_once_with(0.5)

    def test_wait_route_delegates_compilation_condition_to_helper(self) -> None:
        expected_response: dict[str, Any] = {"result": {"status": "Ready", "time_waited_seconds": 4.5}}

        with patch(
            "nexus_bridge.routes_editor._wait_for_compilation", return_value=expected_response
        ) as mock_wait_for_compilation:
            with patch("nexus_bridge.routes_editor.time.time", return_value=55.0):
                response: dict[str, Any] = routing.route_tool(
                    "wait", {"condition": "compilation", "timeout_seconds": 12.0}
                )

        self.assertEqual(expected_response, response)
        mock_wait_for_compilation.assert_called_once_with(timeout=12.0, start_time=55.0)


class UiAutomationRoutingTests(unittest.TestCase):
    def test_ui_automation_routes_hierarchy_traversal_caps(self) -> None:
        with patch("nexus_bridge._transport.call_unity", return_value={"result": {"ok": True}}) as mock_call_unity:
            routing.route_tool(
                "ui_automation",
                {
                    "action": "get_hierarchy",
                    "window_title": "Nexus Unity",
                    "deep": True,
                    "max_depth": 5,
                    "max_elements": 50,
                },
            )
            mock_call_unity.assert_called_with(
                "ui_get_hierarchy",
                {"window_title": "Nexus Unity", "deep": True, "max_depth": 5, "max_elements": 50},
            )

    def test_ui_automation_routes_query_traversal_caps(self) -> None:
        with patch("nexus_bridge._transport.call_unity", return_value={"result": {"ok": True}}) as mock_call_unity:
            routing.route_tool(
                "ui_automation",
                {"action": "query", "window_title": "Nexus Unity", "name": "Button", "max_depth": 4, "max_results": 10},
            )
            mock_call_unity.assert_called_with(
                "ui_query_elements",
                {"window_title": "Nexus Unity", "name": "Button", "max_depth": 4, "max_results": 10},
            )

    def test_ui_automation_routes_snapshot_traversal_caps(self) -> None:
        with patch("nexus_bridge._transport.call_unity", return_value={"result": {"ok": True}}) as mock_call_unity:
            routing.route_tool(
                "ui_automation",
                {
                    "action": "capture_window_snapshot",
                    "window_title": "Nexus Unity",
                    "include_hierarchy": True,
                    "max_depth": 3,
                    "max_elements": 20,
                },
            )
            mock_call_unity.assert_called_with(
                "ui_capture_window_snapshot",
                {"window_title": "Nexus Unity", "include_hierarchy": True, "max_depth": 3, "max_elements": 20},
            )

    def test_ui_automation_preserves_zero_caps(self) -> None:
        with patch("nexus_bridge._transport.call_unity", return_value={"result": {"ok": True}}) as mock_call_unity:
            routing.route_tool(
                "ui_automation",
                {"action": "get_hierarchy", "window_title": "Nexus Unity", "max_depth": 0, "max_elements": 0},
            )
            mock_call_unity.assert_called_with(
                "ui_get_hierarchy",
                {"window_title": "Nexus Unity", "max_depth": 0, "max_elements": 0},
            )


if __name__ == "__main__":
    unittest.main()
