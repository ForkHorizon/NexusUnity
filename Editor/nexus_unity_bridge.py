#!/usr/bin/env python3
import sys
sys.dont_write_bytecode = True

import json
import os
import time
import threading

# Ensure the Editor directory is in sys.path so we can import the module locally
CURRENT_DIR = os.path.dirname(os.path.abspath(__file__))
if CURRENT_DIR not in sys.path:
    sys.path.insert(0, CURRENT_DIR)

def consume_positional_port_arg():
    if len(sys.argv) <= 1:
        return

    try:
        port = int(sys.argv[1])
    except ValueError:
        return

    os.environ.setdefault("NEXUS_UNITY_PORT", str(port))
    del sys.argv[1]

consume_positional_port_arg()

from nexus_bridge.schemas import STATIC_TOOLS
from nexus_bridge.routing import route_tool
from nexus_bridge.client import log

PARENT_PID = os.getppid()

def orphan_monitor():
    """Monitor if the parent process (AI CLI) is still alive."""
    while True:
        try:
            # os.getppid() returns 1 if the parent has died (on Unix)
            if os.getppid() != PARENT_PID or os.getppid() == 1:
                log("Parent process died. Shutting down bridge.")
                os._exit(0)
        except:
            os._exit(0)
        time.sleep(5)

def main():
    # Start orphan monitor thread
    threading.Thread(target=orphan_monitor, daemon=True).start()

    # CLI Mode: Support direct command execution
    if len(sys.argv) > 1 and not sys.argv[1].startswith("{"):
        method = sys.argv[1]
        args = {}
        for arg in sys.argv[2:]:
            if "=" in arg:
                key, val = arg.split("=", 1)
                # Try to parse as JSON if it looks like it, otherwise keep as string
                try: args[key] = json.loads(val)
                except: args[key] = val
        
        log(f"CLI Mode: Calling {method} with {args}")
        print(json.dumps(route_tool(method, args), indent=2))
        return

    # MCP Mode: JSON-RPC 2.0 over Stdin/Stdout
    log(f"NexusUnity Bridge started (Parent PID: {PARENT_PID})")
    
    while True:
        line = sys.stdin.readline()
        if not line:
            log("Stdin closed. Shutting down bridge.")
            break

        try:
            request = json.loads(line)
            method = request.get("method")
            req_id = request.get("id")
            
            if method == "initialize":
                res = {
                    "protocolVersion": "2024-11-05", 
                    "capabilities": {"tools": {}, "resources": {}, "prompts": {}}, 
                    "serverInfo": {"name": "NexusUnity-Bridge", "version": "1.1.1"}
                }
                response = {"jsonrpc": "2.0", "id": req_id, "result": res}
            elif method in ["tools/list", "listTools", "list_tools"]:
                response = {"jsonrpc": "2.0", "id": req_id, "result": {"tools": STATIC_TOOLS}}
            elif method in ["resources/list", "listResources"]:
                resources = [
                    {"uri": "unity://docs/api-reference", "name": "API Reference", "mimeType": "text/markdown"},
                    {"uri": "unity://docs/setup", "name": "Setup Guide", "mimeType": "text/markdown"}
                ]
                response = {"jsonrpc": "2.0", "id": req_id, "result": {"resources": resources}}
            elif method in ["tools/call", "callTool"]:
                params = request.get("params", {})
                name = params.get("name", "").replace("unity_", "")
                args = params.get("arguments", {})

                unity_res = route_tool(name, args)

                if "error" in unity_res:
                    response = {"jsonrpc": "2.0", "id": req_id, "error": unity_res["error"]}
                else:
                    result_content = unity_res.get("result", unity_res)
                    if isinstance(result_content, dict) and "content" in result_content:
                        response = {"jsonrpc": "2.0", "id": req_id, "result": result_content}
                    else:
                        response = {
                            "jsonrpc": "2.0", 
                            "id": req_id, 
                            "result": {"content": [{"type": "text", "text": json.dumps(result_content) }]}}
            elif req_id is not None:
                response = {"jsonrpc": "2.0", "id": req_id, "error": {"code": -32601, "message": "Method not found"}}
            else:
                continue

            sys.stdout.write(json.dumps(response) + "\n")
            sys.stdout.flush()
        except Exception as e:
            log(f"Error in bridge loop: {str(e)}")

if __name__ == "__main__":
    main()
