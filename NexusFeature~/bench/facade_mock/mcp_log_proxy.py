#!/usr/bin/env python3
"""Research-only stdio MCP logging proxy (P0-16 pilot): identical call logging for every arm's MCP servers.
Usage: mcp_log_proxy.py <logfile> <server-label> -- <command> [args...]"""
import json, subprocess, sys, threading, time
log, label = sys.argv[1], sys.argv[2]
cmd = sys.argv[sys.argv.index("--") + 1:]
p = subprocess.Popen(cmd, stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True, bufsize=1)
pending = {}; lock = threading.Lock()
def w(rec):
    with lock, open(log, "a") as f: f.write(json.dumps(rec) + "\n")
def up():
    for line in p.stdout:
        try:
            m = json.loads(line); mid = m.get("id")
            if mid in pending:
                t0, name, args = pending.pop(mid); r = m.get("result") or {}
                text = "".join(c.get("text", "") for c in r.get("content", []) if c.get("type") == "text")
                w({"server": label, "tool": name, "args": args, "ms": round((time.time() - t0) * 1000), "isError": bool(r.get("isError") or m.get("error")),
                   "chars": len(text), "nontext": sum(1 for c in r.get("content", []) if c.get("type") != "text"), "head": text[:400]})
            elif m.get("result", {}).get("tools") is not None:
                w({"server": label, "tools_list": len(m["result"]["tools"]), "chars": len(json.dumps(m["result"]["tools"]))})
        except Exception: pass
        sys.stdout.write(line); sys.stdout.flush()
threading.Thread(target=up, daemon=True).start()
for line in sys.stdin:
    try:
        m = json.loads(line)
        if m.get("method") == "tools/call": pending[m.get("id")] = (time.time(), m["params"]["name"], m["params"].get("arguments"))
    except Exception: pass
    p.stdin.write(line); p.stdin.flush()
p.stdin.close(); p.wait()
