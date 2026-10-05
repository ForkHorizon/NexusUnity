"""Minimal stdio MCP server for one generated surface.

Codex 0.160 does not reliably grow its tool list mid-run. This server
advertises a fixed list. Lazy tools are reached through describe/invoke
tools that were on the list from the start. tools.listChanged is not claimed.
"""

from __future__ import annotations

import json
import sys
from typing import Any

from lab.adapters.dispatch import dispatch
from lab.telemetry.redact import redact

PROTOCOL = "2024-11-05"


def handle(surface: dict[str, Any], catalog: dict[str, Any], message: dict[str, Any], backend) -> dict[str, Any] | None:
    method = message.get("method")
    msg_id = message.get("id")
    if method == "notifications/initialized" or method and method.startswith("notifications/"):
        return None
    if method == "initialize":
        return _result(
            msg_id,
            {
                "protocolVersion": PROTOCOL,
                "capabilities": {"tools": {}},
                "serverInfo": {"name": "nexus-architecture-lab", "version": "0.1.0"},
            },
        )
    if method == "tools/list":
        return _result(msg_id, {"tools": surface["tools"]})
    if method == "tools/call":
        params = message.get("params") or {}
        name = params.get("name") or ""
        arguments = params.get("arguments") or {}
        if isinstance(arguments, str):
            try:
                arguments = json.loads(arguments)
            except json.JSONDecodeError:
                arguments = {}
        if not isinstance(arguments, dict):
            arguments = {}
        outcome = dispatch(surface, catalog, name, arguments, backend)
        return _result(
            msg_id,
            {
                "content": [{"type": "text", "text": outcome["text"]}],
                "isError": not outcome["ok"],
            },
        )
    if msg_id is None:
        return None
    return {"jsonrpc": "2.0", "id": msg_id, "error": {"code": -32601, "message": f"unknown method {method}"}}


def _result(msg_id: Any, result: dict[str, Any]) -> dict[str, Any]:
    return {"jsonrpc": "2.0", "id": msg_id, "result": redact(result)}


def serve(surface: dict[str, Any], catalog: dict[str, Any], backend, input_stream, output_stream) -> None:
    pending = b""
    while True:
        chunk = input_stream.buffer.read(1) if hasattr(input_stream, "buffer") else None
        if chunk is None:
            data = input_stream.readline()
            if not data:
                return
            message = json.loads(data)
            _write(output_stream, handle(surface, catalog, message, backend))
            continue
        if not chunk:
            return
        pending += chunk
        if pending.startswith(b"{"):
            line = pending + input_stream.buffer.readline()
            pending = b""
            message = json.loads(line.decode("utf-8"))
            _write(output_stream, handle(surface, catalog, message, backend))
            continue
        if b"\r\n\r\n" in pending:
            header, rest = pending.split(b"\r\n\r\n", 1)
            length = _content_length(header)
            while len(rest) < length:
                more = input_stream.buffer.read(length - len(rest))
                if not more:
                    return
                rest += more
            body, pending = rest[:length], rest[length:]
            message = json.loads(body.decode("utf-8"))
            _write(output_stream, handle(surface, catalog, message, backend))


def _content_length(header: bytes) -> int:
    for line in header.decode("utf-8", errors="replace").split("\r\n"):
        if line.lower().startswith("content-length:"):
            return int(line.split(":", 1)[1].strip())
    raise ValueError("missing Content-Length")


def _write(output_stream, message: dict[str, Any] | None) -> None:
    if message is None:
        return
    body = json.dumps(message).encode("utf-8")
    output_stream.buffer.write(f"Content-Length: {len(body)}\r\n\r\n".encode("ascii") + body)
    output_stream.buffer.flush()


def main(argv: list[str] | None = None) -> None:
    import argparse
    from pathlib import Path

    from lab.adapters.unity_cli import UnityCliBackend
    from lab.catalog.extract import load_catalog

    parser = argparse.ArgumentParser()
    parser.add_argument("--surface", required=True)
    parser.add_argument("--catalog", required=True)
    parser.add_argument("--unity-binary", required=True)
    parser.add_argument("--project", required=True)
    parser.add_argument("--port", required=True, type=int)
    parser.add_argument("--telemetry", required=True)
    args = parser.parse_args(argv)
    surface = json.loads(Path(args.surface).read_text(encoding="utf-8"))
    catalog = load_catalog(Path(args.catalog))
    backend = UnityCliBackend(args.unity_binary, args.project, args.port)
    telemetry = Path(args.telemetry)
    telemetry.parent.mkdir(parents=True, exist_ok=True)

    def logged_backend(command: str, arguments: dict[str, Any]) -> dict[str, Any]:
        result = backend.call(command, arguments)
        with telemetry.open("a", encoding="utf-8") as handle:
            handle.write(
                json.dumps(
                    redact(
                        {
                            "backend_command": command,
                            "arguments": arguments,
                            "ok": result.get("ok"),
                            "exit_code": result.get("exit_code"),
                        }
                    )
                )
                + "\n"
            )
        return result

    serve(surface, catalog, logged_backend, sys.stdin, sys.stdout)


if __name__ == "__main__":
    main()
