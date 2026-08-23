from __future__ import annotations

import importlib
import os
import sys
import unittest
from typing import Any
from unittest.mock import patch

EDITOR_DIR: str = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if EDITOR_DIR not in sys.path:
    sys.path.insert(0, EDITOR_DIR)

from nexus_bridge import _transport as transport_module


def _reload_transport_module(environment: dict[str, str], argv: list[str]) -> Any:
    with patch.dict(os.environ, environment, clear=True):
        with patch.object(sys, "argv", argv):
            return importlib.reload(transport_module)


class ReadPortTests(unittest.TestCase):
    def test_read_port_prefers_environment_variable(self) -> None:
        transport: Any = _reload_transport_module({"NEXUS_UNITY_PORT": "8123"}, ["nexus_unity_bridge.py", "9000"])
        with patch.dict(os.environ, {"NEXUS_UNITY_PORT": "8123"}, clear=True):
            with patch.object(sys, "argv", ["nexus_unity_bridge.py", "9000"]):
                self.assertEqual(8123, transport._read_port())

    def test_read_port_uses_positional_argv_value(self) -> None:
        transport: Any = _reload_transport_module({}, ["nexus_unity_bridge.py", "8124"])
        with patch.dict(os.environ, {}, clear=True):
            with patch.object(sys, "argv", ["nexus_unity_bridge.py", "8124"]):
                self.assertEqual(8124, transport._read_port())

    def test_read_port_falls_back_to_default(self) -> None:
        transport: Any = _reload_transport_module({}, ["nexus_unity_bridge.py", "scene_manager"])
        with patch.dict(os.environ, {}, clear=True):
            with patch.object(sys, "argv", ["nexus_unity_bridge.py", "scene_manager"]):
                self.assertEqual(transport.DEFAULT_PORT, transport._read_port())

    def test_read_port_ignores_invalid_environment_variable(self) -> None:
        transport: Any = _reload_transport_module(
            {"NEXUS_UNITY_PORT": "scene_manager"},
            ["nexus_unity_bridge.py", "8124"],
        )
        with patch.dict(os.environ, {"NEXUS_UNITY_PORT": "scene_manager"}, clear=True):
            with patch.object(sys, "argv", ["nexus_unity_bridge.py", "8124"]):
                self.assertEqual(8124, transport._read_port())


class ModuleConfigTests(unittest.TestCase):
    def test_unity_url_uses_environment_variable_and_normalizes_trailing_slash(self) -> None:
        transport: Any = _reload_transport_module(
            {"NEXUS_UNITY_URL": "http://unity-host:9001"}, ["nexus_unity_bridge.py"]
        )
        self.assertEqual("http://unity-host:9001/", transport.UNITY_URL)

    def test_timeout_is_clamped_to_minimum_of_one_second(self) -> None:
        transport: Any = _reload_transport_module({"NEXUS_UNITY_TIMEOUT_SECONDS": "0.2"}, ["nexus_unity_bridge.py"])
        self.assertEqual(1.0, transport.UNITY_TIMEOUT_SECONDS)

    def test_timeout_ignores_invalid_environment_variable(self) -> None:
        transport: Any = _reload_transport_module({"NEXUS_UNITY_TIMEOUT_SECONDS": "oops"}, ["nexus_unity_bridge.py"])
        self.assertEqual(120, transport.UNITY_TIMEOUT_SECONDS)


if __name__ == "__main__":
    unittest.main()
