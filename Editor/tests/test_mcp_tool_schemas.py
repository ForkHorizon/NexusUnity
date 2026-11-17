"""Tests that tools/list advertises flat schemas that Claude's tool API accepts."""

from __future__ import annotations

import importlib
import sys
import unittest
from typing import Any
from unittest.mock import patch

from nexus_bridge.schemas import MCP_TOOLS, STATIC_TOOLS, flatten_tool

with patch.object(sys, "argv", ["nexus_unity_bridge.py"]):
    bridge: Any = importlib.import_module("nexus_unity_bridge")


class McpToolSchemaTests(unittest.TestCase):
    def test_no_tool_has_a_top_level_combinator(self) -> None:
        for tool in MCP_TOOLS:
            with self.subTest(tool=tool["name"]):
                schema = tool["inputSchema"]
                self.assertEqual("object", schema["type"])
                self.assertFalse({"oneOf", "anyOf", "allOf"} & set(schema))

    def test_flat_schema_keeps_every_action_and_parameter(self) -> None:
        for source, flat in zip(STATIC_TOOLS, MCP_TOOLS, strict=True):
            variants = source["inputSchema"].get("oneOf", [])
            if not variants:
                self.assertIs(source, flat)
                continue
            with self.subTest(tool=source["name"]):
                key = flat["inputSchema"]["required"][0]
                properties = flat["inputSchema"]["properties"]
                for variant in variants:
                    action = variant["properties"][key]
                    for value in [action["const"]] if "const" in action else action["enum"]:
                        self.assertIn(value, properties[key]["enum"])
                        self.assertIn(f"`{value}`", properties[key]["description"])
                    self.assertLessEqual(set(variant["properties"]), set(properties))

    def test_per_action_requirements_are_described(self) -> None:
        wait = next(tool for tool in MCP_TOOLS if tool["name"] == "unity_wait")
        condition = wait["inputSchema"]["properties"]["condition"]

        self.assertEqual(["condition"], wait["inputSchema"]["required"])
        self.assertIn("`play_mode`", condition["description"])
        self.assertIn("(needs state)", condition["description"])

    def test_flatten_keeps_a_tool_without_variants(self) -> None:
        tool = {"name": "plain", "inputSchema": {"type": "object", "properties": {}}}
        self.assertIs(tool, flatten_tool(tool))

    def test_tools_list_serves_the_flat_catalog(self) -> None:
        response = bridge._handle_rpc_request({"method": "tools/list", "id": 1})
        self.assertIs(MCP_TOOLS, response["result"]["tools"])


if __name__ == "__main__":
    unittest.main()
