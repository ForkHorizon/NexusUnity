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


class HierarchyManagerSchemaTests(unittest.TestCase):
    def test_hierarchy_manager_uses_action_specific_variants(self) -> None:
        hierarchy_manager = _get_tool("unity_hierarchy_manager")
        input_schema = hierarchy_manager["inputSchema"]

        self.assertNotIn("required", input_schema)
        self.assertIn("oneOf", input_schema)
        self.assertGreaterEqual(len(input_schema["oneOf"]), 1)

    def test_rename_variant_requires_instance_id_and_name_or_new_name(self) -> None:
        hierarchy_manager = _get_tool("unity_hierarchy_manager")
        rename_variant = next(
            variant
            for variant in hierarchy_manager["inputSchema"]["oneOf"]
            if "rename" in _action_values(variant)
        )

        self.assertCountEqual(["action", "instance_id"], rename_variant["required"])
        self.assertEqual(
            [{"required": ["name"]}, {"required": ["new_name"]}],
            rename_variant["anyOf"],
        )

    def test_transform_alias_is_advertised(self) -> None:
        hierarchy_manager = _get_tool("unity_hierarchy_manager")
        transform_variant = next(
            variant
            for variant in hierarchy_manager["inputSchema"]["oneOf"]
            if "set_transform" in _action_values(variant)
        )

        self.assertIn("transform", _action_values(transform_variant))

    def test_set_sibling_index_accepts_integer_or_edge_keywords(self) -> None:
        hierarchy_manager = _get_tool("unity_hierarchy_manager")
        sibling_variant = next(
            variant
            for variant in hierarchy_manager["inputSchema"]["oneOf"]
            if "set_sibling_index" in _action_values(variant)
        )

        self.assertCountEqual(["action", "instance_id", "index"], sibling_variant["required"])
        index_schema = sibling_variant["properties"]["index"]
        self.assertEqual(
            [
                {"type": "integer"},
                {"type": "string", "enum": ["first", "last"]},
            ],
            index_schema["oneOf"],
        )


if __name__ == "__main__":
    unittest.main()
