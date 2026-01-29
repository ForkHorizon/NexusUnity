#!/usr/bin/env python3
import sys
import json
import urllib.request

# Port can be overridden via command line arg if needed
PORT = 8081
if len(sys.argv) > 1:
    try: PORT = int(sys.argv[1])
    except: pass

UNITY_URL = f"http://localhost:{PORT}/"

def log(msg):
    sys.stderr.write(f"DEBUG: {msg}\n")
    sys.stderr.flush()

def call_unity(method, params=None):
    payload = {"jsonrpc": "2.0", "method": method, "params": params or {}, "id": 1}
    data = json.dumps(payload).encode('utf-8')
    req = urllib.request.Request(UNITY_URL, data=data, headers={'Content-Type': 'application/json'})
    try:
        with urllib.request.urlopen(req, timeout=5) as f:
            return json.loads(f.read().decode('utf-8'))
    except Exception as e:
        log(f"Unity call failed: {str(e)}")
        return {"error": {"code": -32000, "message": f"Unity Server unreachable or error: {str(e)}"}}

def main():
    log("NexusUnity Bridge started")
    while True:
        line = sys.stdin.readline()
        if not line: break
        try:
            request = json.loads(line)
            method = request.get("method")
            req_id = request.get("id")
            
            log(f"Received request: {method} (id: {req_id})")

            # Standard MCP lifecycle methods
            if method == "initialize":
                res = {
                    "protocolVersion": "2024-11-05", 
                    "capabilities": {
                        "tools": {},
                        "resources": {},
                        "prompts": {}
                    }, 
                    "serverInfo": {"name": "NexusUnity-Bridge", "version": "1.7.9"}
                }
                response = {"jsonrpc": "2.0", "id": req_id, "result": res}
            elif method == "notifications/initialized":
                log("Received initialized notification")
                continue 
            elif method in ["tools/list", "listTools"]:
                unity_res = call_unity("list_tools")
                if "error" in unity_res:
                    log(f"Error fetching tools: {unity_res['error']}")
                    response = {"jsonrpc": "2.0", "id": req_id, "error": unity_res["error"]}
                else:
                    tools = unity_res.get("result", [])
                    log(f"Found {len(tools)} tools")
                    response = {"jsonrpc": "2.0", "id": req_id, "result": {"tools": tools}}
            elif method in ["resources/list", "listResources"]:
                response = {"jsonrpc": "2.0", "id": req_id, "result": {"resources": []}}
            elif method in ["prompts/list", "listPrompts"]:
                response = {"jsonrpc": "2.0", "id": req_id, "result": {"prompts": []}}
            elif method == "tools/call" or method == "callTool":
                # Handle both standard and legacy naming
                params = request.get("params", {})
                tool_name = params.get("name", "").replace("unity_", "")
                tool_args = params.get("arguments", {})
                
                log(f"Calling Unity tool: {tool_name}")
                unity_res = call_unity(tool_name, tool_args)
                
                if "error" in unity_res:
                    response = {"jsonrpc": "2.0", "id": req_id, "error": unity_res["error"]}
                else:
                    response = {
                        "jsonrpc": "2.0", 
                        "id": req_id, 
                        "result": {"content": [{"type": "text", "text": json.dumps(unity_res["result"]) }]}}
            elif req_id is not None:
                response = {"jsonrpc": "2.0", "id": req_id, "error": {"code": -32601, "message": f"Method not found: {method}"}}
            else:
                log(f"Ignoring notification or unknown method without ID: {method}")
                continue

            sys.stdout.write(json.dumps(response) + "\n")
            sys.stdout.flush()
        except Exception as e:
            log(f"Error in bridge loop: {str(e)}")

if __name__ == "__main__":
    main()