from __future__ import annotations

import os
import sys
import unittest
from typing import Any

EDITOR_DIR: str = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if EDITOR_DIR not in sys.path:
    sys.path.insert(0, EDITOR_DIR)

from nexus_bridge.schemas import STATIC_TOOLS


def _get_tool(name: str) -> dict[str, Any]:
    for tool in STATIC_TOOLS:
        if tool["name"] == name:
            return tool
    raise AssertionError(f"Could not find tool {name}.")


def _action_values(variant: dict[str, Any]) -> set[str]:
    action_schema = variant["properties"]["action"]
    if "const" in action_schema:
        return {action_schema["const"]}
    return set(action_schema["enum"])


class UiAutomationSchemaTests(unittest.TestCase):
    def test_ui_automation_advertises_traversal_caps(self) -> None:
        tool = _get_tool("unity_ui_automation")
        variants = tool["inputSchema"]["oneOf"]

        get_hierarchy_variant = next(variant for variant in variants if "get_hierarchy" in _action_values(variant))
        self.assertEqual("integer", get_hierarchy_variant["properties"]["max_depth"]["type"])
        self.assertEqual("integer", get_hierarchy_variant["properties"]["max_elements"]["type"])

        query_variant = next(variant for variant in variants if "query" in _action_values(variant))
        self.assertEqual("integer", query_variant["properties"]["max_depth"]["type"])
        self.assertEqual("integer", query_variant["properties"]["max_results"]["type"])

        snapshot_variant = next(variant for variant in variants if "capture_window_snapshot" in _action_values(variant))
        self.assertEqual("integer", snapshot_variant["properties"]["max_depth"]["type"])
        self.assertEqual("integer", snapshot_variant["properties"]["max_elements"]["type"])


if __name__ == "__main__":
    unittest.main()
