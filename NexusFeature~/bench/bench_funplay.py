#!/usr/bin/env python3
"""Funplay HTTP MCP benchmark using direct JSON-RPC calls only."""
import json
import os
import statistics
import sys
import time
import urllib.request

PROJECT = sys.argv[1] if len(sys.argv) > 1 else os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
URL = os.environ.get("FUNPLAY_URL", "http://127.0.0.1:27015/")
N = int(os.environ.get("BENCH_N", "10"))
OUT = os.environ.get("BENCH_OUT", os.path.join(PROJECT, "results", "bench_funplay.json"))


class Client:
    def __init__(self): self.request_id = 1

    def call(self, method, params=None, timeout=120):
        request = {"jsonrpc": "2.0", "id": self.request_id, "method": method, "params": params or {}}
        self.request_id += 1
        payload = json.dumps(request).encode()
        req = urllib.request.Request(URL, data=payload, headers={"Content-Type": "application/json"})
        started = time.perf_counter()
        with urllib.request.urlopen(req, timeout=timeout) as response: raw = response.read()
        body = json.loads(raw.decode())
        return {"method": method, "params": params or {}, "ms": round((time.perf_counter() - started) * 1000, 3),
                "ok": "error" not in body, "payload_B": len(raw), "response": body}


def tool(client, name, arguments=None, timeout=120):
    return client.call("tools/call", {"name": name, "arguments": arguments or {}}, timeout)


def sequence(client, calls):
    started = time.perf_counter(); items = []
    for name, args, timeout in calls:
        try: items.append(tool(client, name, args, timeout))
        except Exception as exc: items.append({"method": "tools/call", "params": {"name": name, "arguments": args}, "ms": None, "ok": False, "payload_B": 0, "error": repr(exc)})
    return {"ms": round((time.perf_counter() - started) * 1000, 3), "ok": all(x.get("ok", False) for x in items),
            "payload_B": sum(x.get("payload_B", 0) for x in items), "calls": items}


def c1(c):
    init = c.call("initialize", {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "benchmark", "version": "1"}}, 30)
    try: c.call("notifications/initialized", {}, 10)
    except Exception: pass
    listing = c.call("tools/list", {}, 30)
    return {"ms": round(init["ms"] + listing["ms"], 3), "ok": init["ok"] and listing["ok"], "payload_B": init["payload_B"] + listing["payload_B"], "calls": [init, listing]}


def execute(c, code, safety_checks=False):
    return tool(c, "execute_code", {"code": code, "safety_checks": safety_checks}, 120)


def c6(c):
    code = "using UnityEngine; public static class FunplayBenchC6 { public static string Run() { var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = \"FunplayBenchCube\"; Object.DestroyImmediate(go); return \"created_and_destroyed\"; } }"
    return execute(c, code, False)


def c7(c):
    # Funplay's closest C7 equivalent is its in-memory compiler: it deliberately writes no .cs file.
    return execute(c, "using UnityEngine; public static class FunplayBenchC7 { public static string Run() { return Application.unityVersion; } }", True)


def c11(c):
    return execute(c, "using UnityEngine; public static class FunplayBenchC11 { public static string Run() { return \"execute_code_ok:\" + Application.unityVersion; } }", True)


def c12(c):
    calls = []
    try: calls.append(tool(c, "enter_play_mode", {}, 120))
    except Exception as exc: calls.append({"method": "tools/call", "params": {"name": "enter_play_mode", "arguments": {}}, "ms": None, "ok": False, "payload_B": 0, "error": repr(exc)})
    # Funplay documents a brief HTTP drop during Play Mode domain reload.
    for _ in range(15):
        try:
            recovery = tool(c, "get_reload_recovery_status", {}, 30)
            calls.append(recovery)
            if recovery.get("ok", False): break
        except Exception:
            time.sleep(1)
    for name, args in [("simulate_mouse_click", {"x": 858, "y": 337, "button": "left"}),
                       ("capture_game_view", {"width": 320, "height": 240}),
                       ("get_console_logs", {"count": 20, "group_duplicates": True})]:
        try: calls.append(tool(c, name, args, 120))
        except Exception as exc: calls.append({"method": "tools/call", "params": {"name": name, "arguments": args}, "ms": None, "ok": False, "payload_B": 0, "error": repr(exc)})
    try: calls.append(tool(c, "exit_play_mode", {}, 120))
    except Exception as exc: calls.append({"method": "tools/call", "params": {"name": "exit_play_mode", "arguments": {}}, "ms": None, "ok": False, "payload_B": 0, "error": repr(exc)})
    measured = [x for x in calls if x.get("params", {}).get("name") != "get_reload_recovery_status"]
    return {"ms": round(sum(x.get("ms") or 0 for x in calls), 3), "ok": len(measured) == 5 and all(x.get("ok", False) for x in measured),
            "payload_B": sum(x.get("payload_B", 0) for x in calls), "calls": calls}


SCENARIOS = {
    "C1": c1,
    "C2": lambda c: tool(c, "get_editor_state"),
    "C3": lambda c: tool(c, "get_hierarchy", {"root_name": "Main Camera", "depth": 1, "include_components": True}),
    "C4": lambda c: tool(c, "get_hierarchy", {"depth": 3, "include_components": True}),
    "C5": lambda c: tool(c, "find_game_objects", {"query": "Camera", "find_method": "by_name"}),
    "C6": c6,
    "C7": c7,
    "C8": lambda c: tool(c, "capture_game_view", {"width": 320, "height": 240}),
    "C10": None,
    "C11": c11,
    "C12": c12,
}


def summary(samples):
    valid = [s["ms"] for s in samples if s.get("ms") is not None]
    if not valid: return {"n": len(samples), "ok_percent": 0}
    ordered = sorted(valid)
    return {"n": len(samples), "min_ms": round(ordered[0], 3), "p50_ms": round(statistics.median(ordered), 3),
            "p95_ms": round(ordered[max(0, int(len(ordered) * .95) - 1)], 3), "max_ms": round(ordered[-1], 3),
            "ok_percent": round(100 * sum(s.get("ok", False) for s in samples) / len(samples), 1),
            "payload_B_p50": round(statistics.median([s.get("payload_B", 0) for s in samples]), 1)}


def main():
    c = Client(); listing = c.call("tools/list", {}, 30)
    tools = listing.get("response", {}).get("result", {}).get("tools", [])
    output = {"tool": "funplay", "url": URL, "n": N,
              "tools_list": {"count": len(tools), "schema_chars": len(json.dumps(tools)), "approx_tokens": len(json.dumps(tools)) // 4},
              "mapping": {"C1": "initialize + tools/list", "C2": "get_editor_state", "C3": "get_hierarchy(root_name=Main Camera)", "C4": "get_hierarchy(depth=3)", "C5": "find_game_objects", "C6": "execute_code (create + destroy)", "C7": "execute_code (in-memory compile; no disk file)", "C8": "capture_game_view", "C9": "not supported by HTTP MCP surface", "C10": "not supported in core tool exposure", "C11": "execute_code", "C12": "enter_play_mode + simulate_mouse_click + capture_game_view + get_console_logs + exit_play_mode"},
              "unsupported": {"C9": "No batch JSON-RPC operation exposed by Funplay HTTP MCP.", "C10": "core tools/list exposes no run_tests/get_test_job tool."}, "scenarios": {}}
    selected = os.environ.get("BENCH_SCENARIOS")
    scenarios = SCENARIOS if not selected else {k: SCENARIOS[k] for k in selected.split(",") if k in SCENARIOS}
    for name, runner in scenarios.items():
        if runner is None: continue
        print(f"{name}: cold + {N - 1} warm"); samples = []
        for _ in range(N):
            try: samples.append(runner(c))
            except Exception as exc: samples.append({"ms": None, "ok": False, "payload_B": 0, "error": repr(exc)})
        output["scenarios"][name] = {"cold": samples[0], "stats": summary(samples), "samples": samples}
    with open(OUT, "w", encoding="utf-8") as handle: json.dump(output, handle, indent=2, ensure_ascii=False)
    print(f"saved {OUT}")


if __name__ == "__main__": main()
