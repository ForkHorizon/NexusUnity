#!/usr/bin/env python3
"""IvanMurzak Unity-MCP stdio benchmark using newline-delimited JSON-RPC."""
import json
import os
import select
import statistics
import subprocess
import sys
import time

PROJECT = sys.argv[1] if len(sys.argv) > 1 else os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
N = int(os.environ.get("BENCH_N", "10"))
OUT = os.environ.get("BENCH_OUT", os.path.join(PROJECT, "results", "bench_ivan.json"))
BINARY = os.path.join(PROJECT, "Library", "mcp-server", "osx-arm64", "gamedev-mcp-server")
PORT = int(os.environ.get("IVAN_PORT", "29185"))


class Client:
    def __init__(self):
        self.proc = subprocess.Popen(
            [BINARY, f"--port={PORT}", "--plugin-timeout=10000", "--client-transport=stdio"],
            stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
            text=True, bufsize=1,
        )
        self.request_id = 0
        self.stderr = []

    def _request(self, method, params=None, timeout=120):
        self.request_id += 1
        request_id = self.request_id
        request = {"jsonrpc": "2.0", "id": request_id, "method": method, "params": params or {}}
        encoded = json.dumps(request, separators=(",", ":"))
        self.proc.stdin.write(encoded + "\n")
        self.proc.stdin.flush()
        deadline = time.perf_counter() + timeout
        unmatched = []
        while time.perf_counter() < deadline:
            ready, _, _ = select.select([self.proc.stdout], [], [], min(0.2, max(0.01, deadline - time.perf_counter())))
            if not ready:
                continue
            line = self.proc.stdout.readline()
            if not line:
                break
            try:
                body = json.loads(line)
            except json.JSONDecodeError:
                continue
            if body.get("id") == request_id:
                return body, len(line.encode("utf-8")), unmatched
            unmatched.append(body)
        return {"timeout": True, "request": request}, 0, unmatched

    def notify_initialized(self):
        self.proc.stdin.write(json.dumps({"jsonrpc": "2.0", "method": "notifications/initialized"}) + "\n")
        self.proc.stdin.flush()

    def call(self, method, params=None, timeout=120):
        started = time.perf_counter()
        body, payload, unmatched = self._request(method, params, timeout)
        elapsed = round((time.perf_counter() - started) * 1000, 3)
        result = body.get("result", {}) if isinstance(body, dict) else {}
        ok = bool(body.get("id") is not None and "error" not in body and not result.get("isError", False))
        return {
            "method": method, "params": params or {}, "ms": elapsed, "ok": ok,
            "payload_B": payload, "response": body, "unmatched_events": len(unmatched),
        }

    def close(self):
        try:
            self.proc.terminate()
            self.proc.wait(timeout=5)
        except Exception:
            self.proc.kill()
        try:
            self.stderr = self.proc.stderr.read().splitlines()
        except Exception:
            self.stderr = []


def tool(client, name, arguments=None, timeout=120):
    return client.call("tools/call", {"name": name, "arguments": arguments or {}}, timeout)


def c1(client):
    init = client.call("initialize", {
        "protocolVersion": "2025-06-18", "capabilities": {},
        "clientInfo": {"name": "benchmark", "version": "1"},
    }, 30)
    client.notify_initialized()
    listing = client.call("tools/list", {}, 30)
    return {
        "ms": round(init["ms"] + listing["ms"], 3),
        "ok": init["ok"] and listing["ok"],
        "payload_B": init["payload_B"] + listing["payload_B"],
        "calls": [init, listing],
    }


def c3(client):
    return tool(client, "gameobject-find", {
        "gameObjectRef": {"instanceID": 0, "name": "Main Camera"},
        "includeData": True, "includeComponents": True,
        "includeHierarchy": True, "hierarchyDepth": 1,
    })


def c4(client):
    return tool(client, "scene-get-data", {
        "includeRootGameObjects": True, "includeChildrenDepth": 3,
        "includeData": True,
    })


def c5(client):
    # Ivan's API accepts an exact GameObject name, not the protocol's substring search query.
    return tool(client, "gameobject-find", {
        "gameObjectRef": {"instanceID": 0, "name": "Camera"},
        "includeData": True, "includeComponents": True,
    })


def _text(result):
    response = result.get("response", {})
    for item in response.get("result", {}).get("content", []):
        if item.get("type") == "text":
            try:
                return json.loads(item.get("text", ""))
            except Exception:
                return {}
    return response.get("structuredContent", {}) or {}


def c6(client):
    created = tool(client, "gameobject-create", {"name": "IvanBenchCube", "primitiveType": "Cube"})
    ref = _text(created).get("result", {})
    instance_id = ref.get("instanceID")
    if instance_id is None:
        return {"ms": created["ms"], "ok": False, "payload_B": created["payload_B"], "calls": [created], "error": "create returned no instanceID"}
    destroyed = tool(client, "gameobject-destroy", {"gameObjectRef": {"instanceID": instance_id}})
    return {
        "ms": round(created["ms"] + destroyed["ms"], 3),
        "ok": created["ok"] and destroyed["ok"],
        "payload_B": created["payload_B"] + destroyed["payload_B"],
        "calls": [created, destroyed],
    }


def c10(client):
    return tool(client, "tests-run", {
        "testMode": "EditMode", "includeMessages": True,
        "includePassingTests": True,
    }, 180)


SCENARIOS = {"C1": c1, "C3": c3, "C4": c4, "C5": c5, "C6": c6, "C10": c10}


def summary(samples):
    valid = [s["ms"] for s in samples if s.get("ms") is not None]
    if not valid:
        return {"n": len(samples), "ok_percent": 0}
    ordered = sorted(valid)
    return {
        "n": len(samples), "min_ms": round(ordered[0], 3),
        "p50_ms": round(statistics.median(ordered), 3),
        "p95_ms": round(ordered[max(0, int(len(ordered) * 0.95) - 1)], 3),
        "max_ms": round(ordered[-1], 3),
        "ok_percent": round(100 * sum(s.get("ok", False) for s in samples) / len(samples), 1),
        "payload_B_p50": round(statistics.median([s.get("payload_B", 0) for s in samples]), 1),
    }


def main():
    client = Client()
    output = {
        "tool": "ivanmurzak",
        "transport": "stdio",
        "command": [BINARY, f"--port={PORT}", "--plugin-timeout=10000", "--client-transport=stdio"],
        "n": N,
        "mapping": {
            "C1": "initialize + tools/list",
            "C2": "unsupported: no editor-state tool in live tools/list",
            "C3": "gameobject-find(name=Main Camera, full data/components/hierarchy)",
            "C4": "scene-get-data(full root hierarchy/data)",
            "C5": "gameobject-find(name=Camera; exact-name probe, substring search unavailable)",
            "C6": "gameobject-create(Cube) + gameobject-destroy",
            "C7": "unsupported: no disk script create/compile/delete tool in live tools/list",
            "C8": "unsupported: no Game View screenshot tool in live tools/list",
            "C9": "unsupported: no batch execution tool in live tools/list",
            "C10": "tests-run(EditMode)",
            "C11": "omitted per protocol (execute-code equivalent not benchmarked for Ivan)",
            "C12": "unsupported: no play-state, input, or Game View screenshot tools in live tools/list",
        },
        "unsupported": {
            "C2": "No editor state tool exposed; unity-tool-list only lists the 38 live tools.",
            "C7": "README capabilities are not counted unless exposed by tools/list; no disk script lifecycle tools were exposed.",
            "C8": "Only screenshot-isolated is exposed; it is not a Game View screenshot.",
            "C9": "No batch execution tool exposed.",
            "C12": "No editor play-mode control, mouse input, or Game View capture exposed.",
        },
        "scenarios": {},
    }
    try:
        selected = os.environ.get("BENCH_SCENARIOS")
        scenarios = SCENARIOS if not selected else {k: SCENARIOS[k] for k in selected.split(",") if k in SCENARIOS}
        for name, runner in scenarios.items():
            print(f"{name}: cold + {N - 1} warm", flush=True)
            samples = []
            for _ in range(N):
                try:
                    samples.append(runner(client))
                except Exception as exc:
                    samples.append({"ms": None, "ok": False, "payload_B": 0, "error": repr(exc)})
            output["scenarios"][name] = {"cold": samples[0], "stats": summary(samples), "samples": samples}
            # Creating/destroying an object can dirty the open scene even though the
            # benchmark leaves no object behind; satisfy tests-run's save precondition.
            if name == "C6":
                tool(client, "scene-save", {}, 30)
        listing = client.call("tools/list", {}, 30)
        response = listing.get("response", {})
        live_tools = response.get("result", {}).get("tools", [])
        output["tools_list"] = {
            "count": len(live_tools), "schema_chars": len(json.dumps(live_tools, ensure_ascii=False)),
            "approx_tokens": len(json.dumps(live_tools, ensure_ascii=False)) // 4,
            "names": [t.get("name") for t in live_tools],
        }
        with open(os.path.join(PROJECT, "results", "ivan_initialize.json"), "w", encoding="utf-8") as f:
            json.dump(output["scenarios"].get("C1", {}).get("cold", {}).get("calls", [])[0].get("response", {}), f, indent=2)
        with open(os.path.join(PROJECT, "results", "ivan_tools_list.json"), "w", encoding="utf-8") as f:
            json.dump(response, f, indent=2, ensure_ascii=False)
    finally:
        client.close()
    output["stderr_tail"] = client.stderr[-20:]
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as handle:
        json.dump(output, handle, indent=2, ensure_ascii=False)
    print(f"saved {OUT}")


if __name__ == "__main__":
    main()
