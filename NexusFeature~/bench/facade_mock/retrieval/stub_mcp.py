"""Minimal stdio MCP stub: serves a fixed tools/list, returns a canned error on tools/call, logs calls.
usage: stub_mcp.py unity|nexus|file:<tools_list.json> <logfile>"""
import json, sys
from pathlib import Path
HERE = Path(__file__).resolve().parent
which, log = sys.argv[1], sys.argv[2]
if which == "unity":
    lines = [json.loads(l) for l in open(HERE.parents[2] / "v2_analysis/F_facade_evidence/unity_mcp_tools_list.jsonl")]
    TOOLS = next(l for l in lines if l.get("id") == 2)["result"]["tools"]
    INFO = {"name": "unity-mcp", "version": "1.0.0-beta.10"}
elif which.startswith("file:"):  # any tools/list-shaped JSON, e.g. ../catalog/domain_flat.json
    TOOLS = json.load(open(which[5:]))["result"]["tools"]
    INFO = {"name": "NexusUnity-Bridge", "version": "stub-catalog"}
else:
    TOOLS = json.load(open(HERE / "data/nexus_tools.json"))
    INFO = {"name": "NexusUnity-Bridge", "version": "stub"}
def send(o):
    sys.stdout.write(json.dumps(o) + "\n"); sys.stdout.flush()
for line in sys.stdin:
    try:
        m = json.loads(line)
    except Exception:
        continue
    meth, mid = m.get("method"), m.get("id")
    with open(log, "a") as f:
        f.write(json.dumps({"server": which, "method": meth, "params": m.get("params") if meth == "tools/call" else None}) + "\n")
    if mid is None:
        continue
    if meth == "initialize":
        send({"jsonrpc": "2.0", "id": mid, "result": {"protocolVersion": m["params"].get("protocolVersion", "2025-06-18"),
              "capabilities": {"tools": {"listChanged": False}}, "serverInfo": INFO}})
    elif meth == "tools/list":
        send({"jsonrpc": "2.0", "id": mid, "result": {"tools": TOOLS}})
    elif meth == "tools/call":
        send({"jsonrpc": "2.0", "id": mid, "result": {"isError": True, "content": [{"type": "text",
              "text": "STUB: Unity Editor not connected (benchmark stub). Do not retry; report which tool and arguments you chose."}]}})
    elif meth == "ping":
        send({"jsonrpc": "2.0", "id": mid, "result": {}})
    else:
        send({"jsonrpc": "2.0", "id": mid, "error": {"code": -32601, "message": "not supported"}})
