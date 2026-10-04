"""Canonical envelopes pass through the bridge transport without a second wrap."""

from __future__ import annotations

import importlib
import json
import os
import sys
import unittest
from typing import Any
from unittest.mock import patch

EDITOR_DIR: str = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if EDITOR_DIR not in sys.path:
    sys.path.insert(0, EDITOR_DIR)

from nexus_bridge import _transport as transport_module


class _FakeHttpResponse:
    def __init__(self, payload: bytes) -> None:
        self._payload: bytes = payload

    def __enter__(self) -> _FakeHttpResponse:
        return self

    def __exit__(
        self,
        exc_type: type[BaseException] | None,
        exc_value: BaseException | None,
        traceback: Any,
    ) -> None:
        return None

    def read(self) -> bytes:
        return self._payload


def _reload_transport() -> Any:
    with patch.dict(os.environ, {}, clear=True):
        with patch.object(sys, "argv", ["nexus_unity_bridge.py"]):
            return importlib.reload(transport_module)


class CanonicalEnvelopePassthroughTests(unittest.TestCase):
    def test_call_unity_returns_canonical_envelope_unchanged(self) -> None:
        transport: Any = _reload_transport()
        body: dict[str, Any] = {
            "jsonrpc": "2.0",
            "id": 1,
            "result": {
                "ok": False,
                "data": None,
                "error": {
                    "code": "timeout",
                    "message": "GPU readback timed out.",
                    "retryable": True,
                    "hint": "The read timed out before an image was produced. It is safe to call again.",
                },
                "meta": {"ms": 12, "mode": "legacy", "gen": 4, "request_id": "req-1"},
            },
        }
        fake: _FakeHttpResponse = _FakeHttpResponse(json.dumps(body).encode("utf-8"))

        with patch("nexus_bridge._transport.urllib.request.urlopen", return_value=fake):
            response: dict[str, Any] = transport.call_unity("nexus.capture_game_view", {"request_id": "req-1"})

        self.assertEqual(body, response)
        self.assertIs(response["result"]["ok"], False)
        self.assertNotIn("isError", response)
        self.assertNotIn("isError", response["result"])
        self.assertEqual("timeout", response["result"]["error"]["code"])

    def test_call_unity_does_not_wrap_a_legacy_result(self) -> None:
        transport: Any = _reload_transport()
        body: dict[str, Any] = {
            "jsonrpc": "2.0",
            "id": 1,
            "result": {
                "status": "Success",
                "success": True,
                "message": "Game View screenshot captured.",
                "format": "png",
                "image_base64": "aGk=",
            },
        }
        fake: _FakeHttpResponse = _FakeHttpResponse(json.dumps(body).encode("utf-8"))

        with patch("nexus_bridge._transport.urllib.request.urlopen", return_value=fake):
            response: dict[str, Any] = transport.call_unity("capture_game_view_screenshot")

        self.assertEqual(body, response)
        self.assertNotIn("ok", response["result"])
        self.assertNotIn("meta", response["result"])
        self.assertNotIn("isError", response)
