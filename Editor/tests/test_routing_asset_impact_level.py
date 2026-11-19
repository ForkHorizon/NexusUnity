"""The asset impact `level` (static/editor/auto) reaches nexus.asset_impact from every manager entry point."""

from __future__ import annotations

import unittest
from unittest.mock import patch

from nexus_bridge import routing


class AssetImpactLevelRoutingTests(unittest.TestCase):
    def test_level_is_forwarded(self) -> None:
        cases = [
            (
                "search_manager",
                {
                    "strategy": "references",
                    "type": "Game.Health",
                    "name": "hp",
                    "level": "editor",
                },
            ),
            (
                "search_manager",
                {
                    "strategy": "impact",
                    "change": "rename_field",
                    "type": "Game.Health",
                    "level": "auto",
                },
            ),
            ("lint_project", {"scope": "assets", "level": "editor"}),
        ]
        for name, args in cases:
            with patch(
                "nexus_bridge._transport.call_unity",
                return_value={"result": {"ok": True}},
            ) as call:
                routing.route_tool(name, args)
            self.assertEqual("nexus.asset_impact", call.call_args.args[0])
            self.assertEqual(args["level"], call.call_args.args[1]["level"])


if __name__ == "__main__":
    unittest.main()
