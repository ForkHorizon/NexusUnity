import sys
import json
import urllib.request
import os

# Port can be overridden via command line arg if needed
PORT = 8081
if len(sys.argv) > 1:
    try: PORT = int(sys.argv[1])
    except: pass

UNITY_URL = f"http://127.0.0.1:{PORT}/"

def log(msg):
    sys.stderr.write(f"DEBUG: {msg}\n")
    sys.stderr.flush()

def call_unity(method, params=None):
    payload = {"jsonrpc": "2.0", "method": method, "params": params or {}, "id": 1}
    data = json.dumps(payload).encode('utf-8')
    req = urllib.request.Request(UNITY_URL, data=data, headers={'Content-Type': 'application/json'})
    try:
        # Increased timeout to 120s to support heavy operations like project-wide linting.
        with urllib.request.urlopen(req, timeout=120) as f:
            return json.loads(f.read().decode('utf-8'))
    except Exception as e:
        return {"error": {"code": -32000, "message": f"Unity Server unreachable. Error: {str(e)}"}}
