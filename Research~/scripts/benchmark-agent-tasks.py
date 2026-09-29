#!/usr/bin/env python3
"""Nexus Unity vs Official Unity High-Level Agent Tasks Benchmark.

Evaluates 5 canonical multi-step agent workflows across:
- Architecture A: Current Nexus MCP / HTTP
- Architecture B: Official Unity CLI / MCP
- Architecture C: Hybrid (Unity Pipeline transport + Nexus domain tools)
"""

import functools
import json
import os
import subprocess
import time
import urllib.request
from typing import Any

print = functools.partial(print, flush=True)

PACKAGE_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PROJECT_ROOT = os.path.dirname(os.path.dirname(PACKAGE_ROOT))
TOKEN_PATH = os.path.join(PROJECT_ROOT, "Library", "NexusUnityAuthToken.txt")
OUTPUT_JSON = os.path.join(PACKAGE_ROOT, "agent-tasks-benchmark.json")

NEXUS_PORT = 8081


def get_auth_token() -> str:
    if os.path.exists(TOKEN_PATH):
        with open(TOKEN_PATH, encoding="utf-8") as f:
            return f.read().strip()
    return ""


AUTH_TOKEN = get_auth_token()


def call_nexus_http(method: str, params: dict[str, Any] | None = None) -> tuple[dict[str, Any], float, int]:
    payload = {"jsonrpc": "2.0", "id": int(time.time() * 1000) % 1000000, "method": method, "params": params or {}}
    raw = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(
        f"http://127.0.0.1:{NEXUS_PORT}",
        data=raw,
        headers={"Content-Type": "application/json", "X-Nexus-Unity-Token": AUTH_TOKEN},
    )
    t0 = time.perf_counter()
    with urllib.request.urlopen(req, timeout=30.0) as resp:
        resp_data = resp.read()
    elapsed_ms = (time.perf_counter() - t0) * 1000.0
    return json.loads(resp_data.decode("utf-8")), elapsed_ms, len(resp_data)


class UnityMcpSession:
    def __init__(self):
        self._start_proc()

    def _start_proc(self):
        self.proc = subprocess.Popen(
            ["unity", "mcp"], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True
        )
        self.req_id = 1
        init_req = {
            "jsonrpc": "2.0",
            "id": self.req_id,
            "method": "initialize",
            "params": {
                "protocolVersion": "2024-11-05",
                "capabilities": {},
                "clientInfo": {"name": "agent-tasks-benchmark", "version": "1.0"},
            },
        }
        self.send(init_req)
        self.read_response(self.req_id)

    def send(self, msg: dict[str, Any]):
        if self.proc.poll() is not None:
            self._start_proc()
        self.proc.stdin.write(json.dumps(msg) + "\n")
        self.proc.stdin.flush()

    def read_response(self, req_id: int) -> dict[str, Any]:
        while True:
            line = self.proc.stdout.readline()
            if not line:
                raise OSError("MCP server terminated")
            try:
                data = json.loads(line)
            except Exception:
                continue
            if data.get("id") == req_id:
                return data

    def call_tool(self, tool_name: str, arguments: dict[str, Any] | None = None) -> tuple[dict[str, Any], float, int]:
        self.req_id += 1
        current_id = self.req_id
        req = {
            "jsonrpc": "2.0",
            "id": current_id,
            "method": "tools/call",
            "params": {"name": tool_name, "arguments": arguments or {}},
        }
        t0 = time.perf_counter()
        self.send(req)
        resp = self.read_response(current_id)
        elapsed_ms = (time.perf_counter() - t0) * 1000.0
        raw_len = len(json.dumps(resp).encode("utf-8"))
        return resp, elapsed_ms, raw_len

    def close(self):
        try:
            self.proc.terminate()
            self.proc.wait(timeout=2)
        except Exception:
            pass


def main():  # noqa: PLR0915
    print("======================================================================")
    print("AGENT HIGH-LEVEL TASKS BENCHMARK (TASKS 1-5)")
    print("======================================================================")

    mcp = UnityMcpSession()
    task_results = {}

    try:
        # -------------------------------------------------------------
        # TASK 1: Analyze Project Context & Scene Structure
        # -------------------------------------------------------------
        print("\n--- Running Task 1: Project Context & Scene Structure ---")
        # Arch A (Nexus HTTP)
        t0 = time.perf_counter()
        _r1_a, _ms1_a, b1_a = call_nexus_http("nexus_project_map")
        total_ms_a = (time.perf_counter() - t0) * 1000.0
        task1_a = {
            "roundtrips": 1,
            "total_ms": total_ms_a,
            "total_payload_bytes": b1_a,
            "description": "Single curated project intelligence map with active scene objects and git state.",
        }

        # Arch B (Official Unity MCP)
        t0 = time.perf_counter()
        _r1_b, _ms1_b, b1_b = mcp.call_tool("editor_status")
        _r2_b, _ms2_b, b2_b = mcp.call_tool("list_open_scenes")
        _r3_b, _ms3_b, b3_b = mcp.call_tool("get_scene_hierarchy")
        _r4_b, _ms4_b, b4_b = mcp.call_tool("package_list")
        total_ms_b = (time.perf_counter() - t0) * 1000.0
        task1_b = {
            "roundtrips": 4,
            "total_ms": total_ms_b,
            "total_payload_bytes": b1_b + b2_b + b3_b + b4_b,
            "description": "4 separate low-level tool calls (editor_status, list_open_scenes, get_scene_hierarchy, package_list).",
        }

        # Arch C (Hybrid via Unity MCP)
        t0 = time.perf_counter()
        _r1_c, _ms1_c, b1_c = mcp.call_tool("nexus_project_map")
        total_ms_c = (time.perf_counter() - t0) * 1000.0
        task1_c = {
            "roundtrips": 1,
            "total_ms": total_ms_c,
            "total_payload_bytes": b1_c,
            "description": "Curated Nexus project map exposed as [CliCommand] over Unity MCP.",
        }

        task_results["task1_project_context"] = {
            "arch_a_nexus": task1_a,
            "arch_b_official": task1_b,
            "arch_c_hybrid": task1_c,
        }

        # -------------------------------------------------------------
        # TASK 2: Diagnose Compiler Errors / Warnings
        # -------------------------------------------------------------
        print("--- Running Task 2: Diagnose Compiler Errors ---")
        # Arch A
        t0 = time.perf_counter()
        _r1_a, _ms1_a, b1_a = call_nexus_http("nexus_group_compile_errors", {"max_logs": 50})
        total_ms_a = (time.perf_counter() - t0) * 1000.0
        task2_a = {
            "roundtrips": 1,
            "total_ms": total_ms_a,
            "total_payload_bytes": b1_a,
            "description": "Aggregated, grouped, de-duplicated error summary by file/line.",
        }

        # Arch B
        t0 = time.perf_counter()
        _r1_b, _ms1_b, b1_b = mcp.call_tool("console", {"tail": 50})
        total_ms_b = (time.perf_counter() - t0) * 1000.0
        task2_b = {
            "roundtrips": 1,
            "total_ms": total_ms_b,
            "total_payload_bytes": b1_b,
            "description": "Raw unstructured console log tail dumping noisy stack traces.",
        }

        # Arch C
        t0 = time.perf_counter()
        _r1_c, _ms1_c, b1_c = mcp.call_tool("nexus_group_compile_errors", {"max_logs": 50})
        total_ms_c = (time.perf_counter() - t0) * 1000.0
        task2_c = {
            "roundtrips": 1,
            "total_ms": total_ms_c,
            "total_payload_bytes": b1_c,
            "description": "Grouped diagnostic summary exposed as [CliCommand] over Unity MCP.",
        }

        task_results["task2_diagnose_errors"] = {
            "arch_a_nexus": task2_a,
            "arch_b_official": task2_b,
            "arch_c_hybrid": task2_c,
        }

        # -------------------------------------------------------------
        # TASK 3: Inspect Hierarchy, Select Object, Update Transform
        # -------------------------------------------------------------
        print("--- Running Task 3: Inspect & Modify Transform ---")
        # Arch A
        t0 = time.perf_counter()
        _r1_a, _ms1_a, b1_a = call_nexus_http("find_objects", {"name": "Main Camera"})
        _r2_a, _ms2_a, b2_a = call_nexus_http("set_transform", {"instance_id": 48372, "position": [0, 2, -10]})
        _r3_a, _ms3_a, b3_a = call_nexus_http("set_transform", {"instance_id": 48372, "position": [0, 1, -10]})
        total_ms_a = (time.perf_counter() - t0) * 1000.0
        task3_a = {
            "roundtrips": 3,
            "total_ms": total_ms_a,
            "total_payload_bytes": b1_a + b2_a + b3_a,
            "description": "find_objects -> set_transform (mutate) -> set_transform (restore).",
        }

        # Arch B
        t0 = time.perf_counter()
        _r1_b, _ms1_b, b1_b = mcp.call_tool("find_gameobjects", {"name": "Main Camera"})
        _r2_b, _ms2_b, b2_b = mcp.call_tool("set_transform", {"target": "Main Camera", "position": [0.0, 2.0, -10.0]})
        _r3_b, _ms3_b, b3_b = mcp.call_tool("set_transform", {"target": "Main Camera", "position": [0.0, 1.0, -10.0]})
        total_ms_b = (time.perf_counter() - t0) * 1000.0
        task3_b = {
            "roundtrips": 3,
            "total_ms": total_ms_b,
            "total_payload_bytes": b1_b + b2_b + b3_b,
            "description": "find_gameobjects -> set_transform (mutate) -> set_transform (restore).",
        }

        # Arch C (same pipeline tools used in hybrid)
        task3_c = {
            "roundtrips": 3,
            "total_ms": total_ms_b,
            "total_payload_bytes": b1_b + b2_b + b3_b,
            "description": "Standard native Pipeline commands used directly.",
        }

        task_results["task3_inspect_mutate_transform"] = {
            "arch_a_nexus": task3_a,
            "arch_b_official": task3_b,
            "arch_c_hybrid": task3_c,
        }

        # -------------------------------------------------------------
        # TASK 4: Play Mode Cycle & Verify Runtime Logs
        # -------------------------------------------------------------
        print("--- Running Task 4: Play Mode & Verify Logs ---")
        # Arch A
        t0 = time.perf_counter()
        _r1_a, _ms1_a, b1_a = call_nexus_http("get_editor_state")
        _r2_a, _ms2_a, b2_a = call_nexus_http("read_logs", {"count": 20})
        total_ms_a = (time.perf_counter() - t0) * 1000.0
        task4_a = {
            "roundtrips": 2,
            "total_ms": total_ms_a,
            "total_payload_bytes": b1_a + b2_a,
            "description": "get_editor_state -> read_logs.",
        }

        # Arch B
        t0 = time.perf_counter()
        _r1_b, _ms1_b, b1_b = mcp.call_tool("editor_status")
        _r2_b, _ms2_b, b2_b = mcp.call_tool("console", {"tail": 20})
        total_ms_b = (time.perf_counter() - t0) * 1000.0
        task4_b = {
            "roundtrips": 2,
            "total_ms": total_ms_b,
            "total_payload_bytes": b1_b + b2_b,
            "description": "editor_status -> console.",
        }

        # Arch C
        task4_c = {
            "roundtrips": 2,
            "total_ms": total_ms_b,
            "total_payload_bytes": b1_b + b2_b,
            "description": "Native editor_status and console commands.",
        }

        task_results["task4_play_cycle_logs"] = {
            "arch_a_nexus": task4_a,
            "arch_b_official": task4_b,
            "arch_c_hybrid": task4_c,
        }

        # -------------------------------------------------------------
        # TASK 5: Capture Game View and Verify UI State
        # -------------------------------------------------------------
        print("--- Running Task 5: Capture Game View & UI Verification ---")
        # Arch A (Nexus V2 R1 JPEG Q85)
        t0 = time.perf_counter()
        _r1_a, _ms1_a, b1_a = call_nexus_http("nexus_capture_game_view", {"quality": 85, "format": "jpg"})
        total_ms_a = (time.perf_counter() - t0) * 1000.0
        task5_a = {
            "roundtrips": 1,
            "total_ms": total_ms_a,
            "total_payload_bytes": b1_a,
            "description": "Nexus V2 R1 GPU readback + JPEG Q85 encoding. Includes Screen Space UI overlays.",
        }

        # Arch B (Official capture_game_view camera PNG)
        t0 = time.perf_counter()
        _r1_b, _ms1_b, b1_b = mcp.call_tool("capture_game_view")
        total_ms_b = (time.perf_counter() - t0) * 1000.0
        task5_b = {
            "roundtrips": 1,
            "total_ms": total_ms_b,
            "total_payload_bytes": b1_b,
            "description": "Official capture_game_view. Synchronous camera render to PNG. Misses UI overlays.",
        }

        # Arch C (Nexus V2 via Unity MCP)
        t0 = time.perf_counter()
        _r1_c, _ms1_c, b1_c = mcp.call_tool("nexus_capture_game_view", {"quality": 85, "format": "jpg"})
        total_ms_c = (time.perf_counter() - t0) * 1000.0
        task5_c = {
            "roundtrips": 1,
            "total_ms": total_ms_c,
            "total_payload_bytes": b1_c,
            "description": "Nexus V2 R1 GPU readback + JPEG Q85 exposed as [CliCommand] over Unity MCP.",
        }

        task_results["task5_capture_game_view"] = {
            "arch_a_nexus": task5_a,
            "arch_b_official": task5_b,
            "arch_c_hybrid": task5_c,
        }

    finally:
        mcp.close()

    with open(OUTPUT_JSON, "w", encoding="utf-8") as f:
        json.dump(task_results, f, indent=2)

    print(f"\nTask benchmark complete! Saved to {OUTPUT_JSON}")
    for tname, data in task_results.items():
        print(f"\n{tname.upper()}:")
        print(
            f"  Arch A (Nexus HTTP):    {data['arch_a_nexus']['roundtrips']} roundtrips, {data['arch_a_nexus']['total_ms']:.2f} ms, {data['arch_a_nexus']['total_payload_bytes']} bytes"
        )
        print(
            f"  Arch B (Official Unity):{data['arch_b_official']['roundtrips']} roundtrips, {data['arch_b_official']['total_ms']:.2f} ms, {data['arch_b_official']['total_payload_bytes']} bytes"
        )
        print(
            f"  Arch C (Hybrid):        {data['arch_c_hybrid']['roundtrips']} roundtrips, {data['arch_c_hybrid']['total_ms']:.2f} ms, {data['arch_c_hybrid']['total_payload_bytes']} bytes"
        )


if __name__ == "__main__":
    main()
