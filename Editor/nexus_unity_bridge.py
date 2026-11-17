#!/usr/bin/env python3
"""Entry point for the NexusUnity MCP/CLI bridge.

Supports two modes:

* **CLI mode** — invoked as ``nexus_unity_bridge.py <tool_name> [key=value …]``;
  calls the tool and prints the JSON result to stdout.
* **MCP mode** — reads newline-delimited JSON-RPC 2.0 requests from stdin and
  writes responses to stdout, acting as a stdio MCP server.
"""

import argparse
import importlib
import json
import logging
import os
import sys
import threading
import time
from typing import Any

# Set before the package modules below are imported so no __pycache__ lands inside the Unity package.
sys.dont_write_bytecode = True

# Ensure the Editor directory is in sys.path so we can import the module locally
CURRENT_DIR = os.path.dirname(os.path.abspath(__file__))
if CURRENT_DIR not in sys.path:
    sys.path.insert(0, CURRENT_DIR)


def _read_package_version() -> str:
    try:
        package_json = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "package.json")
        if os.path.isfile(package_json):
            with open(package_json, encoding="utf-8") as f:
                data = json.load(f)
                ver = data.get("version")
                if ver:
                    return ver
    except (OSError, ValueError):
        return "1.6.1"
    return "1.6.1"


BRIDGE_VERSION = _read_package_version()


def consume_positional_port_arg() -> None:
    if len(sys.argv) <= 1:
        return

    try:
        port = int(sys.argv[1])
    except ValueError:
        return

    os.environ.setdefault("NEXUS_UNITY_PORT", str(port))
    del sys.argv[1]


consume_positional_port_arg()

_log_level = os.environ.get("NEXUS_UNITY_LOG_LEVEL", "DEBUG").upper()
logging.basicConfig(
    stream=sys.stderr,
    level=getattr(logging, _log_level, logging.DEBUG),
    format="%(asctime)s %(name)s %(levelname)s: %(message)s",
)

# nexus_bridge reads NEXUS_UNITY_PORT and argv when it is imported, and must not write bytecode into the
# Unity package, so it is imported only after the setup above.
_schemas = importlib.import_module("nexus_bridge.schemas")
_routing = importlib.import_module("nexus_bridge.routing")
# tools/list advertises flat schemas: see nexus_bridge.schemas.MCP_TOOLS.
STATIC_TOOLS = _schemas.MCP_TOOLS
STATIC_RESOURCES = _schemas.STATIC_RESOURCES
route_tool = _routing.route_tool

logger = logging.getLogger("nexus_bridge")

PARENT_PID = os.getppid()
RESOURCE_FILES = {
    "unity://docs/api-reference": "API_REFERENCE.MD",
    "unity://docs/setup": "README.md",
}


def read_resource(uri: str) -> dict[str, Any]:
    filename = RESOURCE_FILES.get(uri)
    if filename is None:
        return {"error": {"code": -32602, "message": f"Unknown resource: {uri}"}}

    for root in [CURRENT_DIR, os.path.dirname(CURRENT_DIR)]:
        path = os.path.join(root, filename)
        if os.path.isfile(path):
            with open(path, encoding="utf-8") as handle:
                return {"result": {"contents": [{"uri": uri, "mimeType": "text/markdown", "text": handle.read()}]}}

    return {"error": {"code": -32000, "message": f"Resource file not found: {filename}"}}


def orphan_monitor() -> None:
    """Monitor if the parent process (AI CLI) is still alive."""
    while True:
        try:
            # os.getppid() returns 1 if the parent has died (on Unix)
            if os.getppid() != PARENT_PID or os.getppid() == 1:
                logger.info("Parent process died. Shutting down bridge.")
                os._exit(0)
        except OSError:
            os._exit(0)
        time.sleep(5)


def _json_or_string(value: str) -> Any:
    try:
        return json.loads(value)
    except (json.JSONDecodeError, ValueError):
        return value


def _parse_cli_args(argv: list[str]) -> tuple[str, dict[str, Any]]:
    parser = argparse.ArgumentParser(prog="nexus_unity_bridge.py")
    parser.add_argument("tool_name", help="Tool or method name to call (without unity_ prefix)")
    parser.add_argument("arguments", nargs="*", help="Space-separated key=value arguments for the tool")
    ns: argparse.Namespace = parser.parse_args(argv)

    args: dict[str, Any] = {}
    for raw in ns.arguments:
        if "=" not in raw:
            parser.error(f"expected key=value argument, got: {raw}")
        key, value = raw.split("=", 1)
        args[key] = _json_or_string(value)

    return ns.tool_name, args


def _run_cli_mode() -> None:
    """Direct command execution: `nexus_unity_bridge.py <tool> key=value ...`."""
    method, args = _parse_cli_args(sys.argv[1:])
    logger.debug("CLI Mode: Calling %s with %s", method, args)
    print(json.dumps(route_tool(method, args), indent=2))


def _handle_tool_call(request: dict, req_id) -> dict:
    params = request.get("params", {})
    name = params.get("name", "").replace("unity_", "")
    args = params.get("arguments", {})

    unity_res = route_tool(name, args)
    if "error" in unity_res:
        return {"jsonrpc": "2.0", "id": req_id, "error": unity_res["error"]}

    result_content = unity_res.get("result", unity_res)
    if isinstance(result_content, dict) and "content" in result_content:
        return {"jsonrpc": "2.0", "id": req_id, "result": result_content}
    return {
        "jsonrpc": "2.0",
        "id": req_id,
        "result": {"content": [{"type": "text", "text": json.dumps(result_content)}]},
    }


def _rpc_initialize(_request: dict, req_id: Any) -> dict:
    res = {
        "protocolVersion": "2024-11-05",
        "capabilities": {"tools": {}, "resources": {}, "prompts": {}},
        "serverInfo": {"name": "NexusUnity-Bridge", "version": BRIDGE_VERSION},
    }
    return {"jsonrpc": "2.0", "id": req_id, "result": res}


def _rpc_list_tools(_request: dict, req_id: Any) -> dict:
    return {"jsonrpc": "2.0", "id": req_id, "result": {"tools": STATIC_TOOLS}}


def _rpc_list_resources(_request: dict, req_id: Any) -> dict:
    return {"jsonrpc": "2.0", "id": req_id, "result": {"resources": STATIC_RESOURCES}}


def _rpc_read_resource(request: dict, req_id: Any) -> dict:
    resource_response = read_resource(request.get("params", {}).get("uri", ""))
    return {"jsonrpc": "2.0", "id": req_id, **resource_response}


def _rpc_call_tool(request: dict, req_id: Any) -> dict:
    return _handle_tool_call(request, req_id)


_RPC_METHODS = {
    "initialize": _rpc_initialize,
    "tools/list": _rpc_list_tools,
    "listTools": _rpc_list_tools,
    "list_tools": _rpc_list_tools,
    "resources/list": _rpc_list_resources,
    "listResources": _rpc_list_resources,
    "resources/read": _rpc_read_resource,
    "readResource": _rpc_read_resource,
    "tools/call": _rpc_call_tool,
    "callTool": _rpc_call_tool,
}


def _handle_rpc_request(request: dict):
    """Build a JSON-RPC response for one request, or None when there is nothing
    to send back (a notification with no id)."""
    method = request.get("method")
    req_id = request.get("id")
    handler = _RPC_METHODS.get(method) if isinstance(method, str) else None
    if handler is not None:
        return handler(request, req_id)
    if req_id is not None:
        return {"jsonrpc": "2.0", "id": req_id, "error": {"code": -32601, "message": "Method not found"}}
    return None


def main() -> None:
    # Start orphan monitor thread
    threading.Thread(target=orphan_monitor, daemon=True).start()

    # CLI Mode: Support direct command execution
    if len(sys.argv) > 1 and not sys.argv[1].startswith("{"):
        _run_cli_mode()
        return

    # MCP Mode: JSON-RPC 2.0 over Stdin/Stdout
    logger.info("NexusUnity Bridge started (Parent PID: %s)", PARENT_PID)

    while True:
        line = sys.stdin.readline()
        if not line:
            logger.info("Stdin closed. Shutting down bridge.")
            break

        try:
            response = _handle_rpc_request(json.loads(line))
            if response is None:
                continue
            sys.stdout.write(json.dumps(response) + "\n")
            sys.stdout.flush()
        except Exception:
            logger.exception("Error in bridge loop")


if __name__ == "__main__":
    main()
