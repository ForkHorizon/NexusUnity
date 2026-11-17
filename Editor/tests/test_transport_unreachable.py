"""Tests how the bridge finds a Unity server: the auth token lookup and the refused-connection error."""

from __future__ import annotations

import importlib
import os
import tempfile
import sys
import unittest
import urllib.error
from typing import Any
from unittest.mock import patch

from nexus_bridge import _transport as transport_module


class UnreachableServerTests(unittest.TestCase):
    def _call_with(self, error: Exception) -> str:
        with patch.dict(os.environ, {}, clear=True), patch.object(sys, "argv", ["nexus_unity_bridge.py"]):
            transport: Any = importlib.reload(transport_module)
        with patch("nexus_bridge._transport.urllib.request.urlopen", side_effect=error):
            return transport.call_unity("get_state")["error"]["message"]

    def test_refused_connection_says_how_to_start_the_server(self) -> None:
        message = self._call_with(urllib.error.URLError(ConnectionRefusedError(61, "Connection refused")))

        self.assertTrue(message.startswith("Unity Server unreachable."))
        self.assertIn("http://127.0.0.1:8081/", message)
        self.assertIn("Start Server", message)

    def test_other_failures_keep_the_plain_message(self) -> None:
        self.assertEqual("Unity Server unreachable. Error: boom", self._call_with(RuntimeError("boom")))

    def test_network_errors_log_one_line_without_a_traceback(self) -> None:
        refused = urllib.error.URLError(ConnectionRefusedError(61, "Connection refused"))
        with self.assertLogs("nexus_bridge._transport", level="WARNING") as logs:
            self._call_with(refused)

        self.assertFalse(logs.records[0].exc_info)
        self.assertIn("Connection refused", logs.output[0])

    def test_unexpected_errors_keep_the_traceback(self) -> None:
        with self.assertLogs("nexus_bridge._transport", level="WARNING") as logs:
            self._call_with(RuntimeError("boom"))

        self.assertTrue(logs.records[0].exc_info)


class TokenLookupTests(unittest.TestCase):
    def test_token_is_found_above_the_bridge_from_any_working_directory(self) -> None:
        with tempfile.TemporaryDirectory() as project, tempfile.TemporaryDirectory() as elsewhere:
            os.makedirs(os.path.join(project, "Library"))
            with open(os.path.join(project, "Library", "NexusUnityAuthToken.txt"), "w", encoding="utf-8") as handle:
                handle.write("project-token\n")
            bridge_file = os.path.join(project, "Assets", "NexusUnity", "Editor", "nexus_bridge", "_transport.py")
            with patch.object(transport_module, "__file__", bridge_file), patch("os.getcwd", return_value=elsewhere):
                self.assertEqual("project-token", transport_module._read_token_file())


if __name__ == "__main__":
    unittest.main()
