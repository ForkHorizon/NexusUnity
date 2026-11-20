"""The asset impact `results` shape (auto/summary/full) reaches nexus.asset_impact from every manager entry point."""

from __future__ import annotations

import unittest
from unittest.mock import patch

from nexus_bridge import routing


class AssetImpactResultsRoutingTests(unittest.TestCase):
    def test_results_is_forwarded(self) -> None:
        cases = [
            (
                "search_manager",
                {
                    "strategy": "references",
                    "type": "Health",
                    "name": "hp",
                    "results": "full",
                },
            ),
            (
                "search_manager",
                {
                    "strategy": "impact",
                    "change": "rename_field",
                    "type": "Health",
                    "results": "summary",
                },
            ),
            ("lint_project", {"scope": "assets", "results": "full"}),
        ]
        for name, args in cases:
            with patch(
                "nexus_bridge._transport.call_unity",
                return_value={"result": {"ok": True}},
            ) as call:
                routing.route_tool(name, args)
            self.assertEqual("nexus.asset_impact", call.call_args.args[0])
            self.assertEqual(args["results"], call.call_args.args[1]["results"])


if __name__ == "__main__":
    unittest.main()
