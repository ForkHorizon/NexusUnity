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


def _reload_transport_module(environment: dict[str, str], argv: list[str]) -> Any:
    with patch.dict(os.environ, environment, clear=True):
        with patch.object(sys, "argv", argv):
            return importlib.reload(transport_module)


class CallUnityTests(unittest.TestCase):
    def test_call_unity_returns_error_dict_on_http_failure(self) -> None:
        transport: Any = _reload_transport_module({}, ["nexus_unity_bridge.py"])

        with patch("nexus_bridge._transport.urllib.request.urlopen", side_effect=RuntimeError("boom")):
            response: dict[str, Any] = transport.call_unity("get_state")

        self.assertEqual(
            {
                "error": {
                    "code": -32000,
                    "message": "Unity Server unreachable. Error: boom",
                }
            },
            response,
        )

    def test_call_unity_sends_expected_json_rpc_payload(self) -> None:
        transport: Any = _reload_transport_module({}, ["nexus_unity_bridge.py"])
        fake_response: _FakeHttpResponse = _FakeHttpResponse(b'{"result": {"ok": true}}')

        with patch("nexus_bridge._transport.urllib.request.urlopen", return_value=fake_response) as mock_urlopen:
            response: dict[str, Any] = transport.call_unity("scene_manager", {"action": "list"})

        request: Any = mock_urlopen.call_args.args[0]
        timeout: float = mock_urlopen.call_args.kwargs["timeout"]
        payload: dict[str, Any] = json.loads(request.data.decode("utf-8"))
        headers: dict[str, str] = {key.lower(): value for key, value in request.header_items()}

        self.assertEqual({"result": {"ok": True}}, response)
        self.assertEqual(transport.UNITY_URL, request.full_url)
        self.assertEqual("application/json", headers["content-type"])
        self.assertEqual(transport.UNITY_TIMEOUT_SECONDS, timeout)
        self.assertEqual(
            {
                "jsonrpc": "2.0",
                "method": "scene_manager",
                "params": {"action": "list"},
                "id": 1,
            },
            payload,
        )

    def test_call_unity_sends_auth_token_header_when_configured(self) -> None:
        transport: Any = _reload_transport_module({"NEXUS_UNITY_AUTH_TOKEN": "secret-token"}, ["nexus_unity_bridge.py"])
        fake_response: _FakeHttpResponse = _FakeHttpResponse(b'{"result": {"ok": true}}')

        with patch("nexus_bridge._transport.urllib.request.urlopen", return_value=fake_response) as mock_urlopen:
            transport.call_unity("get_state")

        request: Any = mock_urlopen.call_args.args[0]
        headers: dict[str, str] = {key.lower(): value for key, value in request.header_items()}
        self.assertEqual("secret-token", headers["x-nexus-unity-token"])

    def test_call_unity_retries_once_with_reloaded_token_on_401(self) -> None:
        # Unity rotates the auth token on every domain reload; a cached token
        # then 401s. call_unity must re-read the token file and retry once,
        # rather than surfacing the 401 as a failure.
        transport: Any = _reload_transport_module({"NEXUS_UNITY_AUTH_TOKEN": "stale-token"}, ["nexus_unity_bridge.py"])
        import urllib.error

        unauthorized = urllib.error.HTTPError(transport.UNITY_URL, 401, "Unauthorized", {}, None)
        success = _FakeHttpResponse(b'{"result": {"ok": true}}')

        with patch.object(transport, "_read_token_file", return_value="fresh-token"):
            with patch(
                "nexus_bridge._transport.urllib.request.urlopen",
                side_effect=[unauthorized, success],
            ) as mock_urlopen:
                response: dict[str, Any] = transport.call_unity("get_state")

        self.assertEqual({"result": {"ok": True}}, response)
        self.assertEqual(2, mock_urlopen.call_count)
        retry_request: Any = mock_urlopen.call_args_list[1].args[0]
        retry_headers: dict[str, str] = {k.lower(): v for k, v in retry_request.header_items()}
        self.assertEqual("fresh-token", retry_headers["x-nexus-unity-token"])

    def test_call_unity_reports_401_as_auth_failure_not_unreachable(self) -> None:
        transport: Any = _reload_transport_module({"NEXUS_UNITY_AUTH_TOKEN": "stale-token"}, ["nexus_unity_bridge.py"])
        import urllib.error

        unauthorized = urllib.error.HTTPError(transport.UNITY_URL, 401, "Unauthorized", {}, None)

        # Both attempts 401 (token file also stale): the error must name the 401,
        # not claim the server is unreachable.
        with patch.object(transport, "_read_token_file", return_value="still-stale"):
            with patch(
                "nexus_bridge._transport.urllib.request.urlopen",
                side_effect=[unauthorized, unauthorized],
            ):
                response: dict[str, Any] = transport.call_unity("get_state")

        self.assertIn("error", response)
        self.assertIn("401", response["error"]["message"])


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
