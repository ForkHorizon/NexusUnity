#!/usr/bin/env python3
import sys
import json
import urllib.request
import os
import time
import threading

# Port can be overridden via command line arg if needed
PORT = 8081
if len(sys.argv) > 1:
    try: PORT = int(sys.argv[1])
    except: pass

UNITY_URL = f"http://127.0.0.1:{PORT}/"
PARENT_PID = os.getppid()

def log(msg):
    sys.stderr.write(f"DEBUG: {msg}\n")
    sys.stderr.flush()

def call_unity(method, params=None):
    payload = {"jsonrpc": "2.0", "method": method, "params": params or {}, "id": 1}
    data = json.dumps(payload).encode('utf-8')
    req = urllib.request.Request(UNITY_URL, data=data, headers={'Content-Type': 'application/json'})
    try:
        # Use a short timeout to prevent the bridge from hanging if Unity is unresponsive
        with urllib.request.urlopen(req, timeout=2) as f:
            return json.loads(f.read().decode('utf-8'))
    except Exception as e:
        return {"error": {"code": -32000, "message": f"Unity Server unreachable: {str(e)}"}}

def orphan_monitor():
    """Monitor if the parent process (Gemini CLI) is still alive."""
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
    log(f"NexusUnity Bridge started (Parent PID: {PARENT_PID})")
    
    # Start the orphan monitor in a background thread
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
            
            # Standard MCP lifecycle methods
            if method == "initialize":
                res = {
                    "protocolVersion": "2024-11-05", 
                    "capabilities": {"tools": {}, "resources": {}, "prompts": {}}, 
                    "serverInfo": {"name": "NexusUnity-Bridge", "version": "1.9.4"}
                }
                response = {"jsonrpc": "2.0", "id": req_id, "result": res}
            elif method == "notifications/initialized":
                continue 
            elif method in ["tools/list", "listTools"]:
                unity_res = call_unity("list_tools")
                tools = unity_res.get("result", [])
                response = {"jsonrpc": "2.0", "id": req_id, "result": {"tools": tools}}
            elif method in ["resources/list", "listResources"]:
                resources = [
                    {
                        "uri": "unity://docs/api-reference",
                        "name": "Nexus Unity API Reference",
                        "mimeType": "text/markdown",
                        "description": "Full reference for all 60+ tools."
                    },
                    {
                        "uri": "unity://docs/setup",
                        "name": "Nexus Unity Setup Guide",
                        "mimeType": "text/markdown",
                        "description": "General architecture and configuration."
                    }
                ]
                response = {"jsonrpc": "2.0", "id": req_id, "result": {"resources": resources}}
            elif method in ["resources/read", "readResource"]:
                uri = request.get("params", {}).get("uri")
                # Attempt to find the library folder relative to this bridge script
                # We'll look for where the documentation might be
                doc_path = ""
                if uri == "unity://docs/api-reference": filename = "API_REFERENCE.MD"
                elif uri == "unity://docs/setup": filename = "DOCUMENTATION.MD"
                else: filename = None

                content = "Documentation file not found. Please check Assets/NexusUnity/ or Packages/."
                if filename:
                    # Strategy: Bridge is usually in project root. Docs are in Assets/NexusUnity or Packages/com.custom...
                    search_paths = [
                        f"Assets/NexusUnity/{filename}",
                        f"Packages/com.custom.unity.mcp/{filename}",
                        filename # If it's in the same folder as bridge
                    ]
                    for p in search_paths:
                        if os.path.exists(p):
                            with open(p, 'r') as f:
                                content = f.read()
                                break
                
                response = {
                    "jsonrpc": "2.0", 
                    "id": req_id, 
                    "result": {"contents": [{"uri": uri, "mimeType": "text/markdown", "text": content}]}}
            elif method in ["prompts/list", "listPrompts"]:
                response = {"jsonrpc": "2.0", "id": req_id, "result": {"prompts": []}}
            elif method in ["tools/call", "callTool"]:
                params = request.get("params", {})
                name = params.get("name", "").replace("unity_", "")
                args = params.get("arguments", {})
                unity_res = call_unity(name, args)
                if "error" in unity_res:
                    response = {"jsonrpc": "2.0", "id": req_id, "error": unity_res["error"]}
                else:
                    response = {
                        "jsonrpc": "2.0", 
                        "id": req_id, 
                        "result": {"content": [{"type": "text", "text": json.dumps(unity_res["result"]) }]}}
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
