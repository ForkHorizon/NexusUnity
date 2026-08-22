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


class PlayerPrefsManagerSchemaTests(unittest.TestCase):
    def test_delete_variant_advertises_optional_confirm(self) -> None:
        playerprefs_manager = _get_tool("unity_playerprefs_manager")
        delete_variant = next(
            variant for variant in playerprefs_manager["inputSchema"]["oneOf"] if "delete" in _action_values(variant)
        )

        self.assertCountEqual(["action", "key"], delete_variant["required"])
        self.assertEqual({"type": "boolean"}, delete_variant["properties"]["confirm"])


class WriteAndCompileSchemaTests(unittest.TestCase):
    def test_write_and_compile_advertises_confirm(self) -> None:
        tool = _get_tool("unity_write_and_compile")
        properties = tool["inputSchema"]["properties"]

        self.assertEqual("boolean", properties["confirm"]["type"])


if __name__ == "__main__":
    unittest.main()
