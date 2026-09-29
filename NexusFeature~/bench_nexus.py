#!/usr/bin/env python3
"""Nexus Unity latency benchmark harness.

Measures per-method latency of the raw JSON-RPC surface (bypasses the Python
MCP bridge on purpose: first find the floor, then measure the bridge on top).

Usage:
  python3 bench_nexus.py [project_root]

Requires: Unity Editor open, Nexus server started (Window > Nexus Unity).
Token is read from <project_root>/Library/NexusUnityAuthToken.txt
(falls back to the NEXUS_UNITY_AUTH_TOKEN env var).
"""
import json
import os
import socket
import statistics
import sys
import time
import urllib.request

PORT = 8081
URL = f"http://127.0.0.1:{PORT}/"
N = int(os.environ.get("BENCH_N", "10"))  # repetitions per method

PROJECT = sys.argv[1] if len(sys.argv) > 1 else os.path.expanduser(
    "~/Daliys/UnityProjects/UnityTestForNexus"
)
TOKEN_PATH = os.path.join(PROJECT, "Library", "NexusUnityAuthToken.txt")


def load_token():
    if os.environ.get("NEXUS_UNITY_AUTH_TOKEN"):
        return os.environ["NEXUS_UNITY_AUTH_TOKEN"].strip()
    try:
        return open(TOKEN_PATH).read().strip()
    except FileNotFoundError:
        print(f"[!] Token not found at {TOKEN_PATH} and NEXUS_UNITY_AUTH_TOKEN unset")
        sys.exit(1)


def call(method, params, timeout=120):
    payload = json.dumps({"jsonrpc": "2.0", "method": method, "params": params, "id": 1}).encode()
    req = urllib.request.Request(URL, data=payload, headers={
        "Content-Type": "application/json",
        "X-Nexus-Unity-Token": TOKEN,
    })
    t0 = time.perf_counter()
    with urllib.request.urlopen(req, timeout=timeout) as r:
        body = json.loads(r.read().decode())
    dt = (time.perf_counter() - t0) * 1000.0
    ok = "error" not in body
    return dt, ok, body


# (label, method, params, note)
SCENARIOS = [
    ("health_status",   "get_server_status",        {},                  "fast-path, cached"),
    ("editor_idle",     "wait_for_editor_idle",     {"timeoutSeconds": 5}, "readiness probe"),
    ("read_gameobject", "get_root_game_objects",    {},                  "cheap read"),
    ("scene_graph",     "dump_scene_graph",          {"maxDepth": 3},     "heavy read"),
    ("search_objects",  "find_objects",              {"search": "Camera"}, "search"),
    ("list_assets",     "list_assets",               {"path": "Assets"},  "asset DB read"),
    ("read_logs",       "read_logs",                 {"count": 20},       "log tail"),
    ("create_primitive","create_primitive",          {"primitive": "Sphere", "name": "BenchSphere", "position": [0, 0, 0]}, "light write (undoable)"),
    ("destroy_cleanup", "destroy_game_object",       {"name": "BenchSphere"}, "write cleanup"),
    ("batch_10",        "batch_execute",             {"requests": [
        {"method": "get_root_game_objects", "params": {}} for _ in range(10)
    ]}, "batch overhead"),
]

TOKEN = load_token()

# warmup + connectivity check
try:
    dt, ok, body = call("get_server_status", {})
    print(f"[i] server reachable, get_server_status = {dt:.1f} ms, ok={ok}")
except Exception as e:
    print(f"[!] Cannot reach {URL}: {e}")
    sys.exit(1)

results = {}
for label, method, params, note in SCENARIOS:
    samples, ok_all = [], True
    for i in range(N):
        try:
            dt, ok, _ = call(method, params)
            samples.append(dt)
            ok_all = ok_all and ok
        except Exception as e:
            ok_all = False
            print(f"    [{label}] run {i} failed: {e}")
            break
    if not samples:
        continue
    samples.sort()
    p50 = statistics.median(samples)
    p95 = samples[max(0, int(len(samples) * 0.95) - 1)]
    results[label] = {"method": method, "note": note, "n": len(samples),
                      "min_ms": round(samples[0], 1), "p50_ms": round(p50, 1),
                      "p95_ms": round(p95, 1), "max_ms": round(samples[-1], 1),
                      "all_ok": ok_all}
    print(f"{label:<18} {method:<26} n={len(samples):<3} p50={p50:8.1f}ms  "
          f"p95={p95:8.1f}ms  max={samples[-1]:8.1f}ms  ok={ok_all}")

# TCP connect timing (transport floor)
t0 = time.perf_counter()
try:
    s = socket.create_connection(("127.0.0.1", PORT), timeout=5)
    s.close()
    print(f"\ntcp_connect_ms={((time.perf_counter()-t0)*1000):.2f}")
except Exception as e:
    print("tcp connect failed:", e)

out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "bench_results_nexus.json")
with open(out, "w") as f:
    json.dump({"ts": time.strftime("%Y-%m-%dT%H:%M:%S"), "n": N, "results": results}, f, indent=1)
print(f"\n[saved] {out}")
