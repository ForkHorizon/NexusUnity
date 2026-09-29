#!/usr/bin/env python3
"""Raw Nexus Unity JSON-RPC benchmark for protocol scenarios C1-C12."""
import json
import os
import statistics
import sys
import time
import urllib.request

PORT = int(os.environ.get("NEXUS_PORT", "8081"))
N = int(os.environ.get("BENCH_N", "10"))
PROJECT = sys.argv[1] if len(sys.argv) > 1 else os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TOKEN_PATH = os.path.join(PROJECT, "Library", "NexusUnityAuthToken.txt")
OUT = os.environ.get("BENCH_OUT", os.path.join(PROJECT, "results", "bench_nexus_raw.json"))


def load_token():
    value = os.environ.get("NEXUS_UNITY_AUTH_TOKEN")
    if value:
        return value.strip()
    with open(TOKEN_PATH, encoding="utf-8") as handle:
        return handle.read().strip()


TOKEN = load_token()


def call(method, params=None, timeout=120):
    request_body = {"jsonrpc": "2.0", "method": method, "params": params or {}, "id": int(time.time_ns() % 2**31)}
    payload = json.dumps(request_body).encode()
    request = urllib.request.Request(
        f"http://127.0.0.1:{PORT}/",
        data=payload,
        headers={"Content-Type": "application/json", "X-Nexus-Unity-Token": TOKEN},
    )
    started = time.perf_counter()
    with urllib.request.urlopen(request, timeout=timeout) as response:
        raw = response.read()
    elapsed = (time.perf_counter() - started) * 1000
    body = json.loads(raw.decode())
    return {"method": method, "params": params or {}, "ms": round(elapsed, 3),
            "ok": "error" not in body, "payload_B": len(raw), "response": body}


def call_sequence(calls):
    started = time.perf_counter()
    items = [call(method, params, timeout) for method, params, timeout in calls]
    return {"ms": round((time.perf_counter() - started) * 1000, 3), "ok": all(x["ok"] for x in items),
            "payload_B": sum(x["payload_B"] for x in items), "calls": items}


def scenario_c1():
    return call_sequence([("initialize", {} , 30), ("list_tools", {}, 30)])


def scenario_c6():
    created = call("create_primitive", {"primitive_type": "Cube", "name": "BenchCube", "position": [0, 0, 0]})
    object_id = created.get("response", {}).get("result", {}).get("data", {}).get("instance_id")
    if object_id is None:
        return {"ms": created["ms"], "ok": False, "payload_B": created["payload_B"], "calls": [created]}
    destroyed = call("destroy_game_object", {"instance_id": object_id})
    return {"ms": round(created["ms"] + destroyed["ms"], 3), "ok": created["ok"] and destroyed["ok"],
            "payload_B": created["payload_B"] + destroyed["payload_B"], "calls": [created, destroyed]}


def scenario_c7():
    source = "using UnityEngine; public class NexusBenchScript : MonoBehaviour {}\n"
    started = time.perf_counter()
    accepted = call("write_file", {"path": "Assets/NexusBenchScript.cs", "content": source, "confirm": True})
    accepted_ms = accepted["ms"]
    calls = [accepted]
    compiled = False
    if accepted["ok"]:
        deadline = time.perf_counter() + 120
        while time.perf_counter() < deadline:
            probe = call("wait_for_editor_idle", {"timeout_seconds": 5}, 30)
            calls.append(probe)
            if probe["ok"] and probe.get("response", {}).get("result", {}).get("status") == "Ready":
                compiled = True
                break
            time.sleep(0.1)
    compile_ms = round((time.perf_counter() - started) * 1000, 3) if compiled else None
    cleanup = call("delete_asset", {"path": "Assets/NexusBenchScript.cs", "confirm": True})
    calls.append(cleanup)
    return {"ms": compile_ms or round((time.perf_counter() - started) * 1000, 3), "ok": accepted["ok"] and compiled and cleanup["ok"],
            "payload_B": sum(x["payload_B"] for x in calls), "accepted_ms": accepted_ms, "compiled": compiled,
            "compile_ms": compile_ms, "calls": calls}


def scenario_c10():
    run = call("run_tests", {"mode": "EditMode"}, 30)
    calls = [run]
    result = None
    if run["ok"]:
        deadline = time.perf_counter() + 120
        result_path = run.get("response", {}).get("result", {}).get("result_path")
        while time.perf_counter() < deadline:
            result_call = call("get_test_results", {"result_path": result_path} if result_path else {}, 30)
            calls.append(result_call)
            result = result_call.get("response", {}).get("result", {})
            if result.get("status") in ("Success", "Error"):
                break
            time.sleep(0.25)
    return {"ms": round(sum(x["ms"] for x in calls), 3), "ok": run["ok"] and bool(result and result.get("status") == "Success"),
            "payload_B": sum(x["payload_B"] for x in calls), "calls": calls}


def scenario_c12():
    calls = []
    for method, params in [
        ("toggle_play_mode", {"value": True}),
        ("simulate_mouse", {"action": "click", "x": 0.5, "y": 0.5, "normalized": True, "button": 0}),
        ("capture_game_view_screenshot", {}),
        ("read_logs", {"count": 20}),
        ("toggle_play_mode", {"value": False}),
    ]:
        try:
            calls.append(call(method, params, 120))
        except Exception as exc:
            calls.append({"method": method, "params": params, "ms": None, "ok": False, "payload_B": 0, "error": str(exc)})
            break
    return {"ms": round(sum(x["ms"] or 0 for x in calls), 3), "ok": len(calls) == 5 and all(x["ok"] for x in calls),
            "payload_B": sum(x["payload_B"] for x in calls), "calls": calls}


def scenario_c9():
    return call("batch_execute", {"requests": [{"method": "get_root_game_objects", "params": {}} for _ in range(10)]})


SCENARIOS = {
    "C1": scenario_c1,
    "C2": lambda: call("get_server_status"),
    "C3": lambda: call("find_by_path", {"path": "Main Camera"}),
    "C4": lambda: call("dump_scene_graph", {"max_depth": 3}),
    "C5": lambda: call("find_objects", {"name": "Camera"}),
    "C6": scenario_c6,
    "C7": scenario_c7,
    "C8": lambda: call("capture_game_view_screenshot"),
    "C9": scenario_c9,
    "C10": scenario_c10,
    "C12": scenario_c12,
}


def stats(samples):
    valid = [x["ms"] for x in samples if x.get("ms") is not None]
    if not valid:
        return {"n": len(samples), "ok_percent": 0}
    ordered = sorted(valid)
    return {"n": len(samples), "min_ms": round(ordered[0], 3), "p50_ms": round(statistics.median(ordered), 3),
            "p95_ms": round(ordered[max(0, int(len(ordered) * 0.95) - 1)], 3), "max_ms": round(ordered[-1], 3),
            "ok_percent": round(100 * sum(x.get("ok", False) for x in samples) / len(samples), 1),
            "payload_B_p50": round(statistics.median([x.get("payload_B", 0) for x in samples]), 1)}


def main():
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    call("get_server_status")
    results = {"tool": "nexus_raw", "protocol": "C1-C12", "n": N, "scenarios": {},
               "unsupported": {"C11": "Nexus raw JSON-RPC has no execute_code-equivalent per protocol."},
               "harness_fixes": [
                   "dump_scene_graph uses max_depth, not maxDepth",
                   "find_objects uses name, not search",
                   "create_primitive uses primitive_type, not primitive",
                   "wait_for_editor_idle uses timeout_seconds, not timeoutSeconds",
                   "destroy_game_object requires instance_id, not name",
               ]}
    selected = os.environ.get("BENCH_SCENARIOS")
    scenario_map = SCENARIOS if not selected else {name: SCENARIOS[name] for name in selected.split(",") if name in SCENARIOS}
    for scenario, runner in scenario_map.items():
        print(f"{scenario}: cold + {N - 1} warm")
        samples = []
        try:
            samples.append(runner())
            for _ in range(N - 1):
                samples.append(runner())
        except Exception as exc:
            samples.append({"ms": None, "ok": False, "payload_B": 0, "error": repr(exc)})
        results["scenarios"][scenario] = {"cold": samples[0], "stats": stats(samples), "samples": samples}
    with open(OUT, "w", encoding="utf-8") as handle:
        json.dump(results, handle, indent=2, ensure_ascii=False)
    print(f"saved {OUT}")


if __name__ == "__main__":
    main()
