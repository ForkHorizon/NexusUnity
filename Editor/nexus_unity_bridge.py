#!/usr/bin/env python3
import sys
import json
import os
import time
import threading

# Ensure the Editor directory is in sys.path so we can import the module locally
CURRENT_DIR = os.path.dirname(os.path.abspath(__file__))
if CURRENT_DIR not in sys.path:
    sys.path.insert(0, CURRENT_DIR)

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
    # --- DUAL MODE: CLI vs MCP ---
    if len(sys.argv) > 1:
        arg1 = sys.argv[1]
        try:
            int(arg1)
        except ValueError:
            method_name = arg1.replace("unity_", "")
            params = {}
            for arg in sys.argv[2:]:
                if "=" in arg:
                    k, v = arg.split("=", 1)
                    try: params[k] = json.loads(v)
                    except: params[k] = v

            log(f"CLI Mode: Calling {method_name} with {params}")
            res = route_tool(method_name, params)
            if "error" in res:
                print(json.dumps(res["error"], indent=2))
                sys.exit(1)
            else:
                final_res = res.get("result", res)
                print(json.dumps(final_res, indent=2))
                sys.exit(0)

    log(f"NexusUnity Bridge started (Parent PID: {PARENT_PID})")
    
    monitor_thread = threading.Thread(target=orphan_monitor, daemon=True)
    monitor_thread.start()

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
                    "serverInfo": {"name": "NexusUnity-Bridge", "version": "2.8.0"}
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
