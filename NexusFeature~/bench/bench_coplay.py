#!/usr/bin/env python3
"""Coplay MCP for Unity benchmark over its documented local HTTP server."""
import json
import os
import statistics
import sys
import time
import urllib.request

PROJECT = sys.argv[1] if len(sys.argv) > 1 else os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
URL = os.environ.get("COPLAY_URL", "http://127.0.0.1:8090/mcp")
N = int(os.environ.get("BENCH_N", "10"))
OUT = os.environ.get("BENCH_OUT", os.path.join(PROJECT, "results", "bench_coplay.json"))


def parse_sse(raw, expected_id):
    candidates = []
    for line in raw.decode("utf-8", "replace").splitlines():
        if not line.startswith("data: "):
            continue
        try:
            candidates.append(json.loads(line[6:]))
        except json.JSONDecodeError:
            pass
    for candidate in candidates:
        if candidate.get("id") == expected_id:
            return candidate
    if candidates:
        return candidates[-1]
    raise ValueError("No JSON-RPC response in SSE body")


class Client:
    def __init__(self):
        self.request_id = 1
        self.session_id = None

    def call(self, method, params=None, timeout=180):
        request = {"jsonrpc": "2.0", "id": self.request_id, "method": method, "params": params or {}}
        self.request_id += 1
        headers = {"Content-Type": "application/json", "Accept": "application/json, text/event-stream"}
        if self.session_id:
            headers["Mcp-Session-Id"] = self.session_id
        req = urllib.request.Request(URL, data=json.dumps(request).encode(), headers=headers)
        started = time.perf_counter()
        with urllib.request.urlopen(req, timeout=timeout) as response:
            raw = response.read()
            self.session_id = response.headers.get("mcp-session-id") or self.session_id
        body = parse_sse(raw, request["id"])
        result = body.get("result", {})
        return {"method": method, "params": params or {}, "ms": round((time.perf_counter() - started) * 1000, 3),
                "ok": "error" not in body and not result.get("isError", False), "payload_B": len(raw), "response": body}


def new_session():
    c = Client()
    c.call("initialize", {"protocolVersion": "2024-11-05", "capabilities": {},
                           "clientInfo": {"name": "nexus-benchmark", "version": "1"}}, 30)
    try:
        c.call("notifications/initialized", {}, 10)
    except Exception:
        pass
    return c


def tool(c, name, arguments=None, timeout=180):
    return c.call("tools/call", {"name": name, "arguments": arguments or {}}, timeout)


def sequence(calls):
    started = time.perf_counter()
    items = []
    for fn in calls:
        try:
            items.append(fn())
        except Exception as exc:
            items.append({"ms": None, "ok": False, "payload_B": 0, "error": repr(exc)})
    return {"ms": round((time.perf_counter() - started) * 1000, 3),
            "ok": bool(items) and all(item.get("ok", False) for item in items),
            "payload_B": sum(item.get("payload_B", 0) for item in items), "calls": items}


def text_of(item):
    try:
        content = item["response"]["result"].get("content", [])
        return "\n".join(block.get("text", "") for block in content if isinstance(block, dict))
    except Exception:
        return ""


def find_value(value, key):
    if isinstance(value, dict):
        if key in value:
            return value[key]
        for child in value.values():
            found = find_value(child, key)
            if found is not None:
                return found
    elif isinstance(value, list):
        for child in value:
            found = find_value(child, key)
            if found is not None:
                return found
    return None


def parsed_tool_result(item):
    raw = text_of(item)
    try:
        return json.loads(raw)
    except Exception:
        return {}


def c1(_):
    c = new_session()
    started = time.perf_counter()
    listing = c.call("tools/list", {}, 30)
    return {"ms": round((time.perf_counter() - started) * 1000, 3), "ok": listing["ok"],
            "payload_B": listing["payload_B"], "calls": [listing]}


def c2(c):
    return tool(c, "read_console", {"action": "get", "count": 1, "types": ["all"], "format": "plain"})


def c3(c):
    found = tool(c, "find_gameobjects", {"search_term": "Main Camera", "search_method": "by_name", "page_size": 10})
    ids = (find_value(parsed_tool_result(found), "instance_ids") or
           find_value(parsed_tool_result(found), "instanceIDs") or
           find_value(parsed_tool_result(found), "ids"))
    if ids is None:
        ids = find_value(parsed_tool_result(found), "instance_id") or find_value(parsed_tool_result(found), "instanceID")
    object_id = ids[0] if isinstance(ids, list) and ids else ids
    if object_id is None:
        return {"ms": found["ms"], "ok": False, "payload_B": found["payload_B"], "calls": [found], "error": "Main Camera id not returned"}
    detail = c.call("resources/read", {"uri": "mcpforunity://scene/gameobject/" + str(object_id)}, 60)
    return {"ms": round(found["ms"] + detail["ms"], 3), "ok": found["ok"] and detail["ok"],
            "payload_B": found["payload_B"] + detail["payload_B"], "calls": [found, detail]}


def c4(c):
    return tool(c, "manage_scene", {"action": "get_hierarchy", "max_depth": 3, "include_transform": True,
                                     "page_size": 100}, 60)


def c5(c):
    return tool(c, "find_gameobjects", {"search_term": "Camera", "search_method": "by_name", "page_size": 50})


def c6(c):
    made = tool(c, "manage_gameobject", {"action": "create", "name": "CoplayBenchCube", "primitive_type": "Cube"}, 60)
    object_id = (find_value(parsed_tool_result(made), "instance_id") or
                 find_value(parsed_tool_result(made), "instanceID") or
                 find_value(parsed_tool_result(made), "id"))
    if object_id is None:
        return {"ms": made["ms"], "ok": False, "payload_B": made["payload_B"], "calls": [made], "error": "create did not return an id"}
    deleted = tool(c, "manage_gameobject", {"action": "delete", "target": str(object_id), "search_method": "by_id"}, 60)
    return {"ms": round(made["ms"] + deleted["ms"], 3), "ok": made["ok"] and deleted["ok"],
            "payload_B": made["payload_B"] + deleted["payload_B"], "calls": [made, deleted]}


def c7(c):
    source = "using UnityEngine; public class CoplayBenchScript : MonoBehaviour {}\n"
    created = tool(c, "create_script", {"path": "Assets/CoplayBenchScript.cs", "contents": source}, 60)
    refreshed = tool(c, "refresh_unity", {"mode": "if_dirty", "scope": "scripts", "compile": "request", "wait_for_ready": True}, 120)
    deleted = tool(c, "delete_script", {"uri": "Assets/CoplayBenchScript.cs"}, 60)
    return {"ms": round(created["ms"] + refreshed["ms"] + deleted["ms"], 3),
            "ok": created["ok"] and refreshed["ok"] and deleted["ok"],
            "payload_B": created["payload_B"] + refreshed["payload_B"] + deleted["payload_B"],
            "calls": [created, refreshed, deleted]}


def c8(c):
    return tool(c, "manage_camera", {"action": "screenshot", "include_image": True, "max_resolution": 640}, 120)


def c9(c):
    commands = [{"tool": "find_gameobjects", "params": {"search_term": "Camera", "search_method": "by_name", "page_size": 10}}
                for _ in range(10)]
    return tool(c, "batch_execute", {"commands": commands, "parallel": True, "fail_fast": False, "max_parallelism": 10}, 120)


def c10(c):
    started = tool(c, "run_tests", {"mode": "EditMode", "include_failed_tests": True}, 60)
    job_id = find_value(parsed_tool_result(started), "job_id")
    if job_id is None:
        return started
    result = tool(c, "get_test_job", {"job_id": job_id, "wait_timeout": 30, "include_failed_tests": True}, 60)
    return {"ms": round(started["ms"] + result["ms"], 3), "ok": started["ok"] and result["ok"],
            "payload_B": started["payload_B"] + result["payload_B"], "calls": [started, result]}


def c12(c):
    # Coplay exposes play/screenshot/log/stop but no input automation tool; record partial capability.
    return sequence([lambda: tool(c, "manage_editor", {"action": "play"}, 120),
                     lambda: tool(c, "manage_camera", {"action": "screenshot", "include_image": True, "max_resolution": 640}, 120),
                     lambda: tool(c, "read_console", {"action": "get", "count": 20, "types": ["all"], "format": "plain"}, 60),
                     lambda: tool(c, "manage_editor", {"action": "stop"}, 120)])


SCENARIOS = {"C1": c1, "C2": c2, "C3": c3, "C4": c4, "C5": c5, "C6": c6,
             "C7": c7, "C8": c8, "C9": c9, "C10": c10, "C12": c12}


def summary(samples):
    valid = [sample["ms"] for sample in samples if sample.get("ms") is not None]
    if not valid:
        return {"n": len(samples), "ok_percent": 0}
    ordered = sorted(valid)
    return {"n": len(samples), "min_ms": round(ordered[0], 3), "p50_ms": round(statistics.median(ordered), 3),
            "p95_ms": round(ordered[max(0, int(len(ordered) * .95) - 1)], 3), "max_ms": round(ordered[-1], 3),
            "ok_percent": round(100 * sum(sample.get("ok", False) for sample in samples) / len(samples), 1),
            "payload_B_p50": round(statistics.median([sample.get("payload_B", 0) for sample in samples]), 1)}


def main():
    discovery = new_session()
    listing = discovery.call("tools/list", {}, 30)
    tools = listing.get("response", {}).get("result", {}).get("tools", [])
    output = {"tool": "coplay", "url": URL, "n": N,
              "tools_list": {"count": len(tools), "schema_chars": len(json.dumps(tools)), "approx_tokens": len(json.dumps(tools)) // 4},
              "mapping": {"C1": "initialize + tools/list", "C2": "read_console", "C3": "find_gameobjects + resources/read gameobject", "C4": "manage_scene(get_hierarchy)",
                          "C5": "find_gameobjects", "C6": "manage_gameobject(create + delete)", "C7": "create_script + refresh_unity + delete_script",
                          "C8": "manage_camera(screenshot)", "C9": "batch_execute(10 find_gameobjects)", "C10": "run_tests + get_test_job",
                          "C11": "not run per protocol competitor rule", "C12": "partial: play + screenshot + read_console + stop; no input automation"},
              "unsupported": {"C11": "Protocol specifies execute-code equivalent for Funplay only; Coplay C11 marked no.",
                              "C12": "No Coplay tool for simulate mouse click; measured partial sequence only."}, "scenarios": {}}
    selected = os.environ.get("BENCH_SCENARIOS")
    scenarios = SCENARIOS if not selected else {key: SCENARIOS[key] for key in selected.split(",") if key in SCENARIOS}
    for name, runner in scenarios.items():
        print(f"{name}: cold + {N - 1} warm")
        samples = []
        for index in range(N):
            try:
                client = new_session() if name == "C1" else discovery
                samples.append(runner(client))
            except Exception as exc:
                samples.append({"ms": None, "ok": False, "payload_B": 0, "error": repr(exc)})
        output["scenarios"][name] = {"cold": samples[0], "stats": summary(samples), "samples": samples}
    with open(OUT, "w", encoding="utf-8") as handle:
        json.dump(output, handle, indent=2, ensure_ascii=False)
    print(f"saved {OUT}")


if __name__ == "__main__":
    main()
