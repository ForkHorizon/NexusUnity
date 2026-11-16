"""Tests for the bridge JSON-RPC method table in nexus_unity_bridge.py."""

from __future__ import annotations

import importlib
import sys
import unittest
from typing import Any
from unittest.mock import patch

with patch.object(sys, "argv", ["nexus_unity_bridge.py"]):
    bridge: Any = importlib.import_module("nexus_unity_bridge")


class HandleRpcRequestTests(unittest.TestCase):
    def test_initialize_reports_server_info(self) -> None:
        response = bridge._handle_rpc_request({"method": "initialize", "id": 1})

        self.assertEqual(1, response["id"])
        self.assertEqual("NexusUnity-Bridge", response["result"]["serverInfo"]["name"])
        self.assertEqual("2024-11-05", response["result"]["protocolVersion"])

    def test_list_aliases_return_the_same_static_catalogs(self) -> None:
        for method in ("tools/list", "listTools", "list_tools"):
            with self.subTest(method=method):
                response = bridge._handle_rpc_request({"method": method, "id": 2})
                self.assertIs(bridge.STATIC_TOOLS, response["result"]["tools"])
        for method in ("resources/list", "listResources"):
            with self.subTest(method=method):
                response = bridge._handle_rpc_request({"method": method, "id": 3})
                self.assertIs(bridge.STATIC_RESOURCES, response["result"]["resources"])

    def test_read_resource_merges_the_resource_payload(self) -> None:
        with patch.object(bridge, "read_resource", return_value={"result": {"contents": []}}) as read:
            response = bridge._handle_rpc_request({"method": "readResource", "id": 4, "params": {"uri": "u"}})

        read.assert_called_once_with("u")
        self.assertEqual({"jsonrpc": "2.0", "id": 4, "result": {"contents": []}}, response)

    def test_tool_call_aliases_delegate_to_the_tool_handler(self) -> None:
        for method in ("tools/call", "callTool"):
            request = {"method": method, "id": 5}
            with self.subTest(method=method), patch.object(bridge, "_handle_tool_call", return_value={"ok": 1}) as call:
                self.assertEqual({"ok": 1}, bridge._handle_rpc_request(request))
                call.assert_called_once_with(request, 5)

    def test_unknown_method_errors_only_when_an_id_is_present(self) -> None:
        response = bridge._handle_rpc_request({"method": "nope", "id": 6})

        self.assertEqual(-32601, response["error"]["code"])
        self.assertIsNone(bridge._handle_rpc_request({"method": "notifications/initialized"}))
        self.assertIsNone(bridge._handle_rpc_request({"method": ["not", "hashable"]}))


if __name__ == "__main__":
    unittest.main()
