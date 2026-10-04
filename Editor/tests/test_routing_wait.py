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


def _idle(reason: str, compile_epoch: int, reload_epoch: int, ready: bool = True) -> dict[str, Any]:
    return {
        "result": {
            "ready": ready,
            "reason": reason,
            "reconnect": reason == "domain_reload",
            "epoch": {"compile": compile_epoch, "reload": reload_epoch},
            "is_idle": ready,
        }
    }


class _Clock:
    def __init__(self) -> None:
        self.now = 0.0

    def time(self) -> float:
        return self.now

    def sleep(self, seconds: float) -> None:
        self.now += seconds


class WaitForCompilationTests(unittest.TestCase):
    def _run(self, responses: list[dict[str, Any] | None], timeout: float = 30.0) -> tuple[dict[str, Any], Any]:
        clock = _Clock()
        with patch("nexus_bridge._transport.call_unity", side_effect=responses) as mock_call_unity:
            with patch("nexus_bridge.routes_editor.time.time", clock.time):
                with patch("nexus_bridge.routes_editor.time.sleep", clock.sleep):
                    response: dict[str, Any] = routing._wait_for_compilation(timeout=timeout)
        return response, mock_call_unity

    def test_already_ready_returns_without_refresh(self) -> None:
        response, mock_call_unity = self._run([_idle("already_ready", 3, 4)])
        self.assertEqual("Ready", response["result"]["status"])
        self.assertTrue(response["result"]["ready"])
        self.assertEqual("already_ready", response["result"]["reason"])
        self.assertEqual({"compile": 3, "reload": 4}, response["result"]["epoch"])
        mock_call_unity.assert_called_once_with("is_editor_idle")

    def test_script_write_does_not_accept_ready_until_epoch_advances(self) -> None:
        response, mock_call_unity = self._run(
            [
                _idle("script_write", 3, 4, ready=False),
                _idle("ready", 3, 4),
                None,
                _idle("ready", 4, 5),
            ]
        )
        self.assertEqual("Ready", response["result"]["status"])
        self.assertEqual({"compile": 4, "reload": 5}, response["result"]["epoch"])
        self.assertEqual(4, mock_call_unity.call_count)
        self.assertNotIn(call("refresh_asset_database"), mock_call_unity.call_args_list)
        self.assertNotIn(call("initialize"), mock_call_unity.call_args_list)

    def test_noop_already_ready_is_accepted_after_script_write(self) -> None:
        response, _mock_call_unity = self._run(
            [
                _idle("script_write", 1, 1, ready=False),
                _idle("already_ready", 1, 1),
            ]
        )
        self.assertEqual("Ready", response["result"]["status"])
        self.assertEqual("already_ready", response["result"]["reason"])

    def test_connection_loss_is_not_success(self) -> None:
        response, mock_call_unity = self._run([None, None], timeout=0.3)
        self.assertEqual("Timeout", response["result"]["status"])
        self.assertFalse(response["result"]["ready"])
        self.assertEqual("domain_reload", response["result"]["reason"])
        self.assertGreaterEqual(mock_call_unity.call_count, 2)
        self.assertNotIn(call("refresh_asset_database"), mock_call_unity.call_args_list)

    def test_legacy_idle_payload_without_epoch_is_not_success(self) -> None:
        legacy = {"result": {"is_idle": True, "is_compiling": False, "is_updating": False}}
        response, _mock_call_unity = self._run([legacy, legacy, legacy], timeout=0.3)
        self.assertEqual("Timeout", response["result"]["status"])
        self.assertFalse(response["result"]["ready"])

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
