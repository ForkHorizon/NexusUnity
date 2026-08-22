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

        with patch("nexus_bridge._transport.call_unity", side_effect=call_results) as mock_call_unity:
            with patch("nexus_bridge.routes_editor._wait_for_compilation") as mock_wait_for_compilation:
                response: dict[str, Any] = routing._route_write_and_compile({"files": files, "confirm": True})

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
                call("write_file", {"path": "Assets/One.cs", "content": "one", "confirm": True}),
                call("write_file", {"path": "Assets/Two.cs", "content": "two", "confirm": True}),
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

        with patch("nexus_bridge.routes_editor.time.time", return_value=10.0):
            with patch(
                "nexus_bridge._transport.call_unity", side_effect=[None, {"result": {"ok": True}}, log_response]
            ) as mock_call_unity:
                with patch(
                    "nexus_bridge.routes_editor._wait_for_compilation", return_value=wait_response
                ) as mock_wait_for_compilation:
                    response: dict[str, Any] = routing._route_write_and_compile({"files": files, "confirm": True})

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
                call("write_file", {"path": "Assets/Test.cs", "content": "class Test {}", "confirm": True}),
                call("read_logs", {"count": 200}),
            ],
            mock_call_unity.call_args_list,
        )


class RunTestsWaitTests(unittest.TestCase):
    def test_run_tests_wait_returns_success_when_new_results_appear(self) -> None:
        call_results: list[dict[str, Any]] = [
            {"result": {"status": "Success", "timestamp_utc": "2026-06-11T00:00:00Z"}},
            {"result": {"status": "Submitted", "result_path": "/tmp/TestResults.xml"}},
            {"result": {"status": "Success", "timestamp_utc": "2026-06-11T00:00:03Z"}},
        ]

        with patch("nexus_bridge._transport.call_unity", side_effect=call_results) as mock_call_unity:
            with patch("nexus_bridge.routes_editor.time.time", side_effect=[100.0, 100.0, 103.25]):
                with patch("nexus_bridge.routes_editor.time.sleep") as mock_sleep:
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

    def test_run_tests_wait_accepts_legacy_success_trigger(self) -> None:
        call_results: list[dict[str, Any]] = [
            {"result": {"status": "Success", "timestamp_utc": "2026-06-11T00:00:00Z"}},
            {"result": {"status": "Success", "result_path": "/tmp/TestResults.xml"}},
            {"result": {"status": "Success", "timestamp_utc": "2026-06-11T00:00:03Z"}},
        ]

        with patch("nexus_bridge._transport.call_unity", side_effect=call_results) as mock_call_unity:
            with patch("nexus_bridge.routes_editor.time.time", side_effect=[100.0, 100.0, 103.25]):
                response: dict[str, Any] = routing._run_tests_wait({})

        self.assertEqual("Success", response["result"]["status"])
        self.assertEqual(3, mock_call_unity.call_count)

    def test_run_tests_wait_returns_timeout_when_results_do_not_change(self) -> None:
        call_results: list[dict[str, Any]] = [
            {"result": {"status": "Success", "timestamp_utc": "2026-06-11T00:00:00Z"}},
            {"result": {"status": "Submitted", "result_path": "/tmp/TestResults.xml"}},
            {"result": {"status": "Success", "timestamp_utc": "2026-06-11T00:00:00Z"}},
        ]

        with patch("nexus_bridge._transport.call_unity", side_effect=call_results):
            with patch("nexus_bridge.routes_editor.time.time", side_effect=[0.0, 0.0, 5.1, 5.1]):
                with patch("nexus_bridge.routes_editor.time.sleep") as mock_sleep:
                    response: dict[str, Any] = routing._run_tests_wait({"timeout_seconds": 5.0})

        self.assertEqual("Timeout", response["result"]["status"])
        self.assertEqual("/tmp/TestResults.xml", response["result"]["result_path"])
        self.assertEqual(5.1, response["result"]["time_waited_seconds"])
        self.assertEqual("Timed out waiting for a new Unity TestResults XML file.", response["result"]["message"])
        mock_sleep.assert_called_once_with(1.0)

    def test_run_tests_wait_returns_non_submitted_trigger_without_polling(self) -> None:
        call_results: list[dict[str, Any]] = [
            {"result": {"status": "Success", "timestamp_utc": "2026-06-11T00:00:00Z"}},
            {"result": {"status": "Error", "message": "A test run is already active."}},
        ]

        with patch("nexus_bridge._transport.call_unity", side_effect=call_results) as mock_call_unity:
            response: dict[str, Any] = routing._run_tests_wait({})

        self.assertEqual("Error", response["result"]["status"])
        self.assertEqual("A test run is already active.", response["result"]["message"])
        mock_call_unity.assert_has_calls(
            [
                call("get_test_results"),
                call("run_tests", {"mode": "EditMode"}),
            ]
        )
        self.assertEqual(2, mock_call_unity.call_count)


if __name__ == "__main__":
    unittest.main()
