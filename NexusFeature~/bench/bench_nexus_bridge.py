#!/usr/bin/env python3
"""Measure the Nexus stdio MCP bridge over the same raw scenarios."""
import json
import os
import statistics
import subprocess
import sys
import time
import urllib.request

PROJECT = sys.argv[1] if len(sys.argv) > 1 else os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BRIDGE = os.path.join(PROJECT, "Library/PackageCache/com.forkhorizon.nexus.unity@b7f1c369188f/Editor/nexus_unity_bridge.py")
N = int(os.environ.get("BENCH_N", "10"))
OUT = os.environ.get("BENCH_OUT", os.path.join(PROJECT, "results", "bench_nexus_bridge.json"))


class Bridge:
    def __init__(self):
        self.process = subprocess.Popen([sys.executable, BRIDGE], cwd=PROJECT, stdin=subprocess.PIPE,
                                        stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, bufsize=1,
                                        env={**os.environ, "NEXUS_UNITY_PORT": os.environ.get("NEXUS_PORT", "8081"),
                                             "NEXUS_UNITY_LOG_LEVEL": "WARNING"})
        self.next_id = 1

    def call(self, method, params=None, timeout=180):
        request = {"jsonrpc": "2.0", "id": self.next_id, "method": method, "params": params or {}}
        self.next_id += 1
        started = time.perf_counter()
        self.process.stdin.write(json.dumps(request) + "\n")
        self.process.stdin.flush()
        while True:
            line = self.process.stdout.readline()
            if not line:
                raise RuntimeError("bridge exited without a response")
            response = json.loads(line)
            if response.get("id") == request["id"]:
                break
            if time.perf_counter() - started > timeout:
                raise TimeoutError(method)
        return {"method": method, "params": params or {}, "ms": round((time.perf_counter() - started) * 1000, 3),
                "ok": "error" not in response, "payload_B": len(json.dumps(response).encode()), "response": response}

    def close(self):
        self.process.terminate()
        try:
            self.process.wait(timeout=3)
        except subprocess.TimeoutExpired:
            self.process.kill()


def raw_delete():
    token = open(os.path.join(PROJECT, "Library/NexusUnityAuthToken.txt"), encoding="utf-8").read().strip()
    body = json.dumps({"jsonrpc": "2.0", "method": "delete_asset", "params": {"path": "Assets/NexusBridgeBenchScript.cs", "confirm": True}, "id": 1}).encode()
    req = urllib.request.Request("http://127.0.0.1:8081/", data=body, headers={"Content-Type": "application/json", "X-Nexus-Unity-Token": token})
    try:
        with urllib.request.urlopen(req, timeout=30) as response:
            return json.loads(response.read())
    except Exception:
        return None


def sequence(bridge, calls):
    started = time.perf_counter()
    items = [bridge.call(method, params, timeout) for method, params, timeout in calls]
    return {"ms": round((time.perf_counter() - started) * 1000, 3), "ok": all(x["ok"] for x in items),
            "payload_B": sum(x["payload_B"] for x in items), "calls": items}


def c1(b): return sequence(b, [("initialize", {}, 30), ("tools/list", {}, 30)])
def c2(b): return b.call("tools/call", {"name": "unity_editor_controller", "arguments": {"action": "get_server_status"}})
def c3(b): return b.call("tools/call", {"name": "unity_search_manager", "arguments": {"strategy": "path", "query": "Main Camera"}})
def c5(b): return b.call("tools/call", {"name": "unity_search_manager", "arguments": {"strategy": "regex", "query": "Camera"}})
def c6(b):
    made = b.call("tools/call", {"name": "unity_hierarchy_manager", "arguments": {"action": "create_primitive", "primitive_type": "Cube", "name": "NexusBridgeBenchCube"}})
    result = made.get("response", {}).get("result", {})
    text = result.get("content", [{}])[0].get("text", "{}") if isinstance(result, dict) else "{}"
    try: object_id = json.loads(text).get("data", {}).get("instance_id")
    except json.JSONDecodeError: object_id = None
    if object_id is None: return {"ms": made["ms"], "ok": False, "payload_B": made["payload_B"], "calls": [made]}
    destroyed = b.call("tools/call", {"name": "unity_hierarchy_manager", "arguments": {"action": "destroy", "instance_id": object_id}})
    return {"ms": round(made["ms"] + destroyed["ms"], 3), "ok": made["ok"] and destroyed["ok"], "payload_B": made["payload_B"] + destroyed["payload_B"], "calls": [made, destroyed]}
def c7(b):
    source = "using UnityEngine; public class NexusBridgeBenchScript : MonoBehaviour {}\n"
    item = b.call("tools/call", {"name": "unity_write_and_compile", "arguments": {"files": [{"path": "Assets/NexusBridgeBenchScript.cs", "content": source}], "confirm": True}}, 120)
    raw_delete()
    return item
def c8(b): return b.call("tools/call", {"name": "unity_ui_automation", "arguments": {"action": "capture_window_snapshot", "window_title": "Game", "include_image": True, "include_hierarchy": False}})
def c10(b): return b.call("tools/call", {"name": "unity_editor_controller", "arguments": {"action": "run_tests_wait", "mode": "EditMode", "timeout_seconds": 20, "poll_interval_seconds": 1}}, 30)
def c12(b):
    return sequence(b, [
        ("tools/call", {"name": "unity_editor_controller", "arguments": {"action": "play", "state": True}}, 60),
        ("tools/call", {"name": "unity_ui_automation", "arguments": {"action": "click", "window_title": "Game", "element_name": "Main Camera"}}, 60),
        ("tools/call", {"name": "unity_ui_automation", "arguments": {"action": "capture_window_snapshot", "window_title": "Game", "include_image": True, "include_hierarchy": False}}, 60),
        ("tools/call", {"name": "unity_editor_controller", "arguments": {"action": "read_logs", "count": 20}}, 60),
        ("tools/call", {"name": "unity_editor_controller", "arguments": {"action": "play", "state": False}}, 60),
    ])


SCENARIOS = {"C1": c1, "C2": c2, "C3": c3, "C5": c5, "C6": c6, "C7": c7, "C8": c8, "C10": c10, "C12": c12}


def summarize(samples):
    valid = [s["ms"] for s in samples if s.get("ms") is not None]
    if not valid: return {"n": len(samples), "ok_percent": 0}
    ordered = sorted(valid)
    return {"n": len(samples), "min_ms": round(ordered[0], 3), "p50_ms": round(statistics.median(ordered), 3),
            "p95_ms": round(ordered[max(0, int(len(ordered) * .95) - 1)], 3), "max_ms": round(ordered[-1], 3),
            "ok_percent": round(100 * sum(s.get("ok", False) for s in samples) / len(samples), 1),
            "payload_B_p50": round(statistics.median([s.get("payload_B", 0) for s in samples]), 1)}


def main():
    b = Bridge()
    try:
        first = b.call("tools/list", {}, 30)
        tools = first.get("response", {}).get("result", {}).get("tools", [])
        output = {"tool": "nexus_bridge", "n": N, "tools_list": {"count": len(tools), "schema_chars": len(json.dumps(tools)), "approx_tokens": len(json.dumps(tools)) // 4},
                  "mapping": {"C1": "initialize + tools/list", "C2": "unity_editor_controller(action=get_server_status)", "C3": "unity_search_manager(strategy=path)", "C4": "not supported by static bridge surface", "C5": "unity_search_manager(strategy=regex)", "C6": "unity_hierarchy_manager", "C7": "unity_write_and_compile", "C8": "unity_ui_automation(capture_window_snapshot)", "C9": "not supported by static bridge surface", "C10": "unity_editor_controller(action=run_tests_wait)", "C11": "not supported", "C12": "editor_controller + ui_automation closest equivalents"}, "scenarios": {}, "unsupported": {"C4": "No scene graph route in bridge", "C9": "No batch route in bridge", "C11": "No execute_code route in bridge"}}
        selected = os.environ.get("BENCH_SCENARIOS")
        scenarios = SCENARIOS if not selected else {k: SCENARIOS[k] for k in selected.split(",") if k in SCENARIOS}
        for name, runner in scenarios.items():
            print(f"{name}: cold + {N - 1} warm")
            samples = []
            for _ in range(N):
                try: samples.append(runner(b))
                except Exception as exc: samples.append({"ms": None, "ok": False, "payload_B": 0, "error": repr(exc)})
            output["scenarios"][name] = {"cold": samples[0], "stats": summarize(samples), "samples": samples}
    finally:
        b.close()
    with open(OUT, "w", encoding="utf-8") as handle: json.dump(output, handle, indent=2, ensure_ascii=False)
    print(f"saved {OUT}")


if __name__ == "__main__": main()
