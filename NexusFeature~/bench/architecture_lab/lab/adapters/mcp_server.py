"""Minimal stdio MCP server for one generated surface.

Codex 0.160 does not reliably grow its tool list mid-run. This server
advertises a fixed list. Lazy tools are reached through describe/invoke
tools that were on the list from the start. tools.listChanged is not claimed.
"""

from __future__ import annotations

import json
import sys
import time
from datetime import datetime, timezone
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
        params = message.get("params") or {}
        version = params.get("protocolVersion") if isinstance(params, dict) else None
        if not isinstance(version, str) or not version.strip():
            version = PROTOCOL
        return _result(
            msg_id,
            {
                "protocolVersion": version,
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


class CallLog:
    """One JSONL line per MCP tools/call. The file is truncated when the server starts.

    A composite capability calls the Unity backend once per step. Writing one
    line per step makes the next tool call inherit the previous step's timing.
    A call that never reaches Unity still writes one line, with
    backend_reached false. Omitting that line makes the next call's result
    look like it belonged to the miss. A later process for the same path
    truncates, so a transport retry cannot prepend the failed attempt's lines.
    """

    def __init__(self, path) -> None:
        from pathlib import Path

        self.path = Path(path)
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self.path.write_text("", encoding="utf-8")
        self._steps: list[dict[str, Any]] = []

    def wrap(self, backend):
        def logged(command: str, arguments: dict[str, Any]) -> dict[str, Any]:
            started = time.perf_counter()
            result = backend(command, arguments)
            duration_ms = int((time.perf_counter() - started) * 1000)
            payload = result.get("payload")
            result_bytes = len(json.dumps(payload, default=str).encode("utf-8")) if payload is not None else 0
            self._steps.append(
                {
                    "ts": datetime.now(timezone.utc).isoformat(),
                    "backend_command": command,
                    "arguments": arguments,
                    "ok": result.get("ok"),
                    "exit_code": result.get("exit_code"),
                    "duration_ms": duration_ms,
                    "result_bytes": result_bytes,
                }
            )
            return result

        return logged

    def flush_call(self) -> None:
        steps = self._steps
        self._steps = []
        if steps:
            line = {
                "ts": steps[0]["ts"],
                "backend_command": ",".join(str(step["backend_command"]) for step in steps),
                "arguments": steps[0]["arguments"] if len(steps) == 1 else {"steps": [step["arguments"] for step in steps]},
                "ok": all(step.get("ok") for step in steps),
                "exit_code": steps[-1].get("exit_code"),
                "duration_ms": sum(int(step["duration_ms"]) for step in steps),
                "result_bytes": sum(int(step["result_bytes"]) for step in steps),
                "step_count": len(steps),
                "backend_reached": True,
            }
        else:
            line = {
                "ts": datetime.now(timezone.utc).isoformat(),
                "backend_command": None,
                "arguments": {},
                "ok": False,
                "exit_code": None,
                "duration_ms": 0,
                "result_bytes": 0,
                "step_count": 0,
                "backend_reached": False,
            }
        with self.path.open("a", encoding="utf-8") as handle:
            handle.write(json.dumps(redact(line)) + "\n")


def serve(surface: dict[str, Any], catalog: dict[str, Any], backend, input_stream, output_stream, call_log: CallLog | None = None) -> None:
    """Speak whichever framing the client used.

    Codex 0.160's rmcp client writes one JSON object per line. A
    Content-Length reply is not a JSON line, so the handshake waits until
    the startup timeout. LSP-style clients still get Content-Length replies.
    """
    pending = b""
    while True:
        chunk = _read1(input_stream)
        if not chunk:
            return
        pending += chunk
        while True:
            pending = pending.lstrip(b"\r\n \t")
            if not pending:
                break
            if pending[:1] == b"{":
                if b"\n" not in pending:
                    break
                line, _, pending = pending.partition(b"\n")
                message = json.loads(line.decode("utf-8"))
                _dispatch_message(surface, catalog, message, backend, output_stream, "ndjson", call_log)
                continue
            separator = b"\r\n\r\n" if b"\r\n\r\n" in pending else b"\n\n" if b"\n\n" in pending else None
            if separator is None:
                break
            header, _, rest = pending.partition(separator)
            length = _content_length(header)
            if len(rest) < length:
                pending = header + separator + rest
                break
            body, pending = rest[:length], rest[length:]
            message = json.loads(body.decode("utf-8"))
            _dispatch_message(surface, catalog, message, backend, output_stream, "content-length", call_log)


def _dispatch_message(surface, catalog, message, backend, output_stream, framing: str, call_log: CallLog | None) -> None:
    try:
        _write(output_stream, handle(surface, catalog, message, backend), framing)
    finally:
        if call_log is not None and message.get("method") == "tools/call":
            call_log.flush_call()


def _content_length(header: bytes) -> int:
    for line in header.decode("utf-8", errors="replace").split("\r\n"):
        if line.lower().startswith("content-length:"):
            return int(line.split(":", 1)[1].strip())
    raise ValueError("missing Content-Length")


def _read1(input_stream) -> bytes:
    if hasattr(input_stream, "buffer"):
        return input_stream.buffer.read(1)
    data = input_stream.read(1)
    if isinstance(data, str):
        return data.encode("utf-8")
    return data or b""


def _write(output_stream, message: dict[str, Any] | None, framing: str) -> None:
    if message is None:
        return
    body = json.dumps(message, separators=(",", ":")).encode("utf-8")
    if framing == "ndjson":
        frame = body + b"\n"
    else:
        frame = f"Content-Length: {len(body)}\r\n\r\n".encode("ascii") + body
    output_stream.buffer.write(frame)
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
    call_log = CallLog(args.telemetry)
    serve(surface, catalog, call_log.wrap(backend.call), sys.stdin, sys.stdout, call_log)


if __name__ == "__main__":
    main()
