"""Routing of the read-only nexus.asset_impact command through the MCP managers."""

from __future__ import annotations

import unittest
from typing import Any
from unittest.mock import patch

from nexus_bridge import routing


class AssetImpactRoutingTests(unittest.TestCase):
    def _route(self, name: str, args: dict[str, Any]) -> tuple[str, Any]:
        with patch("nexus_bridge._transport.call_unity", return_value={"result": {"ok": True}}) as call:
            routing.route_tool(name, args)
        call.assert_called_once()
        return call.call_args.args[0], call.call_args.args[1] if len(call.call_args.args) > 1 else None

    def test_references_with_symbol_object_flattens_into_asset_impact(self) -> None:
        method, params = self._route(
            "search_manager",
            {
                "strategy": "references",
                "symbol": {"kind": "field", "type": "Game.Health", "name": "maxHealth"},
                "max_results": 50,
            },
        )
        self.assertEqual("nexus.asset_impact", method)
        self.assertEqual(
            {"op": "find_references", "kind": "field", "type": "Game.Health", "name": "maxHealth", "max_results": 50},
            params,
        )

    def test_references_without_symbol_keeps_legacy_target_mode(self) -> None:
        method, params = self._route("search_manager", {"strategy": "references", "target_guid": "abc"})
        self.assertEqual("find_references", method)
        self.assertEqual({"target_id": None, "target_guid": "abc"}, params)

    def test_impact_strategy_routes_change_analysis(self) -> None:
        method, params = self._route(
            "search_manager",
            {"strategy": "impact", "change": "rename_field", "type": "Health", "name": "hp", "new_name": "maxHp"},
        )
        self.assertEqual("nexus.asset_impact", method)
        self.assertEqual(
            {
                "op": "analyze_change_impact",
                "change": "rename_field",
                "type": "Health",
                "name": "hp",
                "new_name": "maxHp",
            },
            params,
        )

    def test_lint_assets_scope_routes_integrity_check(self) -> None:
        method, params = self._route("lint_project", {"scope": "assets", "changed_scripts": "Assets/A.cs"})
        self.assertEqual("nexus.asset_impact", method)
        self.assertEqual({"op": "validate_asset_integrity", "changed_scripts": "Assets/A.cs"}, params)

    def test_lint_default_scope_keeps_roslyn_audit(self) -> None:
        method, params = self._route("lint_project", {})
        self.assertEqual("lint_project", method)
        self.assertEqual({}, params)


if __name__ == "__main__":
    unittest.main()
