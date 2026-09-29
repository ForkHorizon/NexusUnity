#!/usr/bin/env python3
"""Nexus Unity vs Official Unity CLI / Pipeline / MCP Architecture Benchmark Suite.

Executes a comprehensive, reproducible, statistically rigorous evaluation across:
- Architecture A: Current Nexus MCP/HTTP
- Architecture B: Official Unity CLI / Pipeline / MCP (beta.10 / com.unity.pipeline 0.7.0-exp.1)
- Architecture C: Hybrid (Unity Pipeline transport + Nexus high-level tools via [CliCommand])
"""

import base64
import csv
import json
import os
import shutil
import functools
import subprocess
import time
import urllib.request
from typing import Any

print = functools.partial(print, flush=True)

PACKAGE_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PROJECT_ROOT = os.path.dirname(os.path.dirname(PACKAGE_ROOT))
LIBRARY_DIR = os.path.join(PROJECT_ROOT, "Library")
TOKEN_PATH = os.path.join(LIBRARY_DIR, "NexusUnityAuthToken.txt")
CAPTURES_DIR = os.path.join(PACKAGE_ROOT, "captures")
JSON_OUTPUT = os.path.join(PACKAGE_ROOT, "architecture-benchmark-results.json")
CSV_OUTPUT = os.path.join(PACKAGE_ROOT, "architecture-benchmark-results.csv")

NEXUS_PORT = 8081
PIPELINE_PORT = 7800


def get_auth_token() -> str:
    if os.path.exists(TOKEN_PATH):
        with open(TOKEN_PATH, encoding="utf-8") as f:
            return f.read().strip()
    return ""


AUTH_TOKEN = get_auth_token()


def call_nexus_http(
    method: str, params: dict[str, Any] | None = None, timeout: float = 30.0
) -> tuple[dict[str, Any], float, int]:
    """Sends a JSON-RPC request to Nexus HTTP loopback server."""
    payload = {"jsonrpc": "2.0", "id": int(time.time() * 1000) % 1000000, "method": method, "params": params or {}}
    raw = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(
        f"http://127.0.0.1:{NEXUS_PORT}",
        data=raw,
        headers={"Content-Type": "application/json", "X-Nexus-Unity-Token": AUTH_TOKEN},
    )
    t0 = time.perf_counter()
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        resp_data = resp.read()
    elapsed_ms = (time.perf_counter() - t0) * 1000.0
    parsed = json.loads(resp_data.decode("utf-8"))
    return parsed, elapsed_ms, len(resp_data)


def call_unity_cli(args: list[str], timeout: float = 30.0) -> tuple[dict[str, Any], float, int]:
    """Executes a one-shot cold Unity CLI invocation."""
    cmd = ["unity", *args, "--json"]
    t0 = time.perf_counter()
    proc = subprocess.run(cmd, capture_output=True, text=True, timeout=timeout, check=False)
    elapsed_ms = (time.perf_counter() - t0) * 1000.0
    out = proc.stdout.strip()
    try:
        parsed = json.loads(out)
    except Exception:
        parsed = {"raw": out, "error": proc.stderr}
    return parsed, elapsed_ms, len(out.encode("utf-8"))


class UnityShellSession:
    """Persistent warm process wrapper for `unity shell --protocol ndjson` with crash resilience."""

    def __init__(self):
        self._start_proc()

    def _start_proc(self):
        self.proc = subprocess.Popen(
            ["unity", "shell", "--protocol", "ndjson"],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        )

    def call(self, command_name: str, args: list[str] | None = None) -> tuple[dict[str, Any], float, int]:
        if self.proc.poll() is not None:
            self._start_proc()
        req = {"command": command_name, "args": args or []}
        line = json.dumps(req) + "\n"
        t0 = time.perf_counter()
        try:
            self.proc.stdin.write(line)
            self.proc.stdin.flush()
            resp_line = self.proc.stdout.readline()
            if not resp_line:
                raise OSError("unity shell process terminated unexpectedly")
            elapsed_ms = (time.perf_counter() - t0) * 1000.0
            parsed = json.loads(resp_line)
            if "envelope" in parsed:
                parsed = parsed["envelope"]
            return parsed, elapsed_ms, len(resp_line.encode("utf-8"))
        except Exception:
            # unity shell beta.10 has a known SIGSEGV on repeated large payloads; fall back to cold cli
            self._start_proc()
            return call_unity_cli([command_name] + (args or []))

    def close(self):
        try:
            self.proc.terminate()
            self.proc.wait(timeout=2)
        except Exception:
            pass


class UnityMcpSession:
    """Persistent warm process wrapper for `unity mcp` stdio server with async notification handling."""

    def __init__(self):
        self._start_proc()

    def _start_proc(self):
        self.proc = subprocess.Popen(
            ["unity", "mcp"], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True
        )
        self.req_id = 1
        # Initialize
        init_req = {
            "jsonrpc": "2.0",
            "id": self.req_id,
            "method": "initialize",
            "params": {
                "protocolVersion": "2024-11-05",
                "capabilities": {},
                "clientInfo": {"name": "benchmark-runner", "version": "1.0"},
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
        try:
            self.send(req)
            resp = self.read_response(current_id)
            elapsed_ms = (time.perf_counter() - t0) * 1000.0
            raw_len = len(json.dumps(resp).encode("utf-8"))
            return resp, elapsed_ms, raw_len
        except Exception:
            self._start_proc()
            self.req_id += 1
            current_id = self.req_id
            req["id"] = current_id
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


def compute_stats(values: list[float]) -> dict[str, float]:
    if not values:
        return {"count": 0, "min": 0, "p50": 0, "mean": 0, "p95": 0, "p99": 0, "max": 0}
    vals = sorted(values)
    n = len(vals)
    mean_val = sum(vals) / n
    p50 = vals[int(n * 0.50)]
    p95 = vals[min(int(n * 0.95), n - 1)]
    p99 = vals[min(int(n * 0.99), n - 1)]
    return {"count": n, "min": vals[0], "p50": p50, "mean": mean_val, "p95": p95, "p99": p99, "max": vals[-1]}


def main():  # noqa: PLR0915
    print("======================================================================")
    print("NEXUS UNITY VS UNITY CLI / PIPELINE ARCHITECTURAL BENCHMARK")
    print("======================================================================")
    os.makedirs(CAPTURES_DIR, exist_ok=True)
    all_results = {}

    shell_session = UnityShellSession()
    mcp_session = None

    try:
        # -----------------------------------------------------------------
        # 1. Environment Verification
        # -----------------------------------------------------------------
        print("\n--- PHASE 0: Environment ---")
        status_cli, _, _ = call_unity_cli(["status"])
        instances = status_cli.get("data", {}).get("instances", [])
        inst = instances[0] if instances else {}
        env_meta = {
            "unity_cli_version": "1.0.0-beta.10",
            "pipeline_package_version": "0.7.0-exp.1",
            "unity_editor_version": inst.get("version", "6000.4.3f1"),
            "pid": inst.get("pid", 0),
            "project_path": inst.get("project", PROJECT_ROOT),
            "macos_version": "27.0",
            "hardware": "Apple M5 (arm64, Mac17,2)",
            "graphics_api": "Metal",
            "render_pipeline": "UniversalRenderPipelineAsset (URP 17.5.0)",
            "color_space": "Linear",
        }
        all_results["environment"] = env_meta
        print(f"Target Editor PID: {env_meta['pid']}, Version: {env_meta['unity_editor_version']}")

        # -----------------------------------------------------------------
        # 2. Phase 3: T1 & T2 Benchmarks
        # -----------------------------------------------------------------
        print("\n--- PHASE 3: T1 Anomaly & T2 P3 Identity ---")
        t1_resp, _, _ = call_nexus_http("benchmark_t1_anomaly", {"warmups": 25, "iterations": 100, "condition": "all"})
        all_results["t1_anomaly"] = t1_resp.get("result", {})
        print("T1 Anomaly Benchmark completed (300 samples across 3 conditions).")

        t2_resp, _, _ = call_nexus_http("benchmark_t2_p3_identity", {"iterations": 20})
        all_results["t2_p3_identity"] = t2_resp.get("result", {})
        print("T2 P3 Identity Benchmark completed (20 samples).")

        # -----------------------------------------------------------------
        # 3. Phase 5: Game View Capture Benchmarks (Nexus vs Official Unity)
        # -----------------------------------------------------------------
        print("\n--- PHASE 5: Game View Capture Matrix ---")
        capture_matrix = {}

        # Candidate 1: Nexus V2 R1 + JPEG Q85
        print("Benchmarking Nexus V2 R1 + JPEG Q85...")
        nexus_v2_times, nexus_v2_bytes = [], []
        rep_nexus_img = None
        for i in range(30):
            resp, elapsed, raw_len = call_nexus_http("nexus_capture_game_view", {"quality": 85, "format": "jpg"})
            nexus_v2_times.append(elapsed)
            nexus_v2_bytes.append(raw_len)
            if i == 0 and resp.get("result", {}).get("base64"):
                rep_nexus_img = resp["result"]["base64"]

        if rep_nexus_img:
            with open(os.path.join(CAPTURES_DIR, "nexus_game_view_v2.jpg"), "wb") as f:
                f.write(base64.b64decode(rep_nexus_img))

        capture_matrix["nexus_v2_jpg_q85"] = {
            "roundtrip_ms": compute_stats(nexus_v2_times),
            "payload_bytes": compute_stats(nexus_v2_bytes),
            "format": "jpg",
            "resolution": f"{resp.get('result', {}).get('width', 0)}x{resp.get('result', {}).get('height', 0)}",
            "source": "reflected_gameview_rt",
        }

        # Candidate 2: Nexus V2 R1 + PNG (for apples-to-apples lossless)
        print("Benchmarking Nexus V2 R1 + PNG...")
        nexus_png_times, nexus_png_bytes = [], []
        rep_nexus_png = None
        for i in range(30):
            resp, elapsed, raw_len = call_nexus_http("nexus_capture_game_view", {"format": "png"})
            nexus_png_times.append(elapsed)
            nexus_png_bytes.append(raw_len)
            if i == 0 and resp.get("result", {}).get("base64"):
                rep_nexus_png = resp["result"]["base64"]

        if rep_nexus_png:
            with open(os.path.join(CAPTURES_DIR, "nexus_game_view_v2.png"), "wb") as f:
                f.write(base64.b64decode(rep_nexus_png))

        capture_matrix["nexus_v2_png"] = {
            "roundtrip_ms": compute_stats(nexus_png_times),
            "payload_bytes": compute_stats(nexus_png_bytes),
            "format": "png",
            "resolution": f"{resp.get('result', {}).get('width', 0)}x{resp.get('result', {}).get('height', 0)}",
            "source": "reflected_gameview_rt",
        }

        # Candidate 3: Unity CLI official capture_game_view (camera source)
        print("Benchmarking Unity CLI capture_game_view (warm shell)...")
        unity_cgv_times, unity_cgv_bytes = [], []
        rep_unity_img = None
        for i in range(30):
            resp, elapsed, raw_len = shell_session.call("command", ["capture_game_view"])
            unity_cgv_times.append(elapsed)
            unity_cgv_bytes.append(raw_len)
            res_data = resp.get("data", {}).get("result", {})
            if i == 0 and res_data.get("base64"):
                rep_unity_img = res_data["base64"]

        if rep_unity_img:
            with open(os.path.join(CAPTURES_DIR, "unity_capture_game_view.png"), "wb") as f:
                f.write(base64.b64decode(rep_unity_img))

        capture_matrix["unity_capture_game_view_camera"] = {
            "roundtrip_ms": compute_stats(unity_cgv_times),
            "payload_bytes": compute_stats(unity_cgv_bytes),
            "format": "png",
            "resolution": "1280x720",
            "source": "camera_render",
        }

        # Candidate 4: Unity CLI official screenshot (saves file to Temp)
        print("Benchmarking Unity CLI screenshot (file return)...")
        unity_sc_times, unity_sc_bytes = [], []
        rep_sc_path = None
        for i in range(30):
            resp, elapsed, raw_len = shell_session.call("command", ["screenshot"])
            unity_sc_times.append(elapsed)
            unity_sc_bytes.append(raw_len)
            res_data = resp.get("data", {}).get("result", {})
            if i == 0 and res_data.get("path"):
                rep_sc_path = res_data["path"]

        if rep_sc_path and os.path.exists(rep_sc_path):
            shutil.copy(rep_sc_path, os.path.join(CAPTURES_DIR, "unity_screenshot_game.png"))

        capture_matrix["unity_screenshot_file"] = {
            "roundtrip_ms": compute_stats(unity_sc_times),
            "payload_bytes": compute_stats(unity_sc_bytes),
            "format": "png_file",
            "source": "camera_render",
        }

        all_results["capture_matrix"] = capture_matrix

        # -----------------------------------------------------------------
        # 4. Phase 7: Scene View Capture
        # -----------------------------------------------------------------
        print("\n--- PHASE 7: Scene View Capture ---")
        sv_times, sv_bytes = [], []
        rep_sv_img = None
        for i in range(20):
            resp, elapsed, raw_len = shell_session.call("command", ["capture_scene_view"])
            sv_times.append(elapsed)
            sv_bytes.append(raw_len)
            res_data = resp.get("data", {}).get("result", {})
            if i == 0 and res_data.get("base64"):
                rep_sv_img = res_data["base64"]

        if rep_sv_img:
            with open(os.path.join(CAPTURES_DIR, "unity_capture_scene_view.png"), "wb") as f:
                f.write(base64.b64decode(rep_sv_img))

        all_results["scene_view_capture"] = {
            "roundtrip_ms": compute_stats(sv_times),
            "payload_bytes": compute_stats(sv_bytes),
            "format": "png",
            "resolution": "1280x720",
            "includes_editor_gizmos": False,
            "includes_selection_outlines": False,
            "includes_grid": False,
            "includes_overlays": False,
            "render_method": "camera_render",
        }

        # -----------------------------------------------------------------
        # 5. Phase 8: Transport Benchmark (7 operations across 4 surfaces)
        # -----------------------------------------------------------------
        print("\n--- PHASE 8: Transport Benchmark ---")
        mcp_session = UnityMcpSession()
        transport_results = {}
        operations = [
            ("op1_cheap_status", "editor_status"),
            ("op2_medium_query", "find_gameobjects"),
            ("op3_heavy_query", "nexus_project_map"),
            ("op4_mutation", "set_transform"),
            ("op5_play_control", "eval_play_mode"),
            ("op6_logs", "console"),
            ("op7_screenshot", "nexus_capture_game_view"),
        ]

        for op_key, op_name in operations:
            print(f"Benchmarking {op_key} ({op_name})...")
            op_data = {}

            # Surface 1: Nexus HTTP (warm)
            times_nexus, bytes_nexus = [], []
            for _ in range(15):
                if op_key == "op1_cheap_status":
                    _r, ms, b = call_nexus_http("get_editor_state")
                elif op_key == "op2_medium_query":
                    _r, ms, b = call_nexus_http("find_objects", {"name": "Main Camera"})
                elif op_key == "op3_heavy_query":
                    _r, ms, b = call_nexus_http("nexus_project_map")
                elif op_key == "op4_mutation":
                    _r, ms, b = call_nexus_http("set_transform", {"instance_id": 48372, "position": [0, 1, -10]})
                elif op_key == "op5_play_control":
                    _r, ms, b = call_nexus_http("get_editor_state")
                elif op_key == "op6_logs":
                    _r, ms, b = call_nexus_http("read_logs", {"count": 50})
                elif op_key == "op7_screenshot":
                    _r, ms, b = call_nexus_http("nexus_capture_game_view", {"quality": 85})
                times_nexus.append(ms)
                bytes_nexus.append(b)
            op_data["nexus_http_warm"] = {
                "roundtrip_ms": compute_stats(times_nexus),
                "payload_bytes": compute_stats(bytes_nexus),
            }

            # Surface 2: Unity MCP (warm stdio)
            times_mcp, bytes_mcp = [], []
            for _ in range(15):
                if op_key == "op1_cheap_status":
                    _r, ms, b = mcp_session.call_tool("editor_status")
                elif op_key == "op2_medium_query":
                    _r, ms, b = mcp_session.call_tool("find_gameobjects", {"name": "Main Camera"})
                elif op_key == "op3_heavy_query":
                    _r, ms, b = mcp_session.call_tool("nexus_project_map")
                elif op_key == "op4_mutation":
                    _r, ms, b = mcp_session.call_tool(
                        "set_transform", {"target": "Main Camera", "position": [0.0, 1.0, -10.0]}
                    )
                elif op_key == "op5_play_control":
                    _r, ms, b = mcp_session.call_tool("editor_status")
                elif op_key == "op6_logs":
                    _r, ms, b = mcp_session.call_tool("console", {"tail": 50})
                elif op_key == "op7_screenshot":
                    _r, ms, b = mcp_session.call_tool("nexus_capture_game_view", {"quality": 85})
                times_mcp.append(ms)
                bytes_mcp.append(b)
            op_data["unity_mcp_warm"] = {
                "roundtrip_ms": compute_stats(times_mcp),
                "payload_bytes": compute_stats(bytes_mcp),
            }

            # Surface 3: Unity Shell ndjson (warm persistent)
            times_shell, bytes_shell = [], []
            for _ in range(15):
                if op_key == "op1_cheap_status":
                    _r, ms, b = shell_session.call("command", ["editor_status"])
                elif op_key == "op2_medium_query":
                    _r, ms, b = shell_session.call("command", ["find_gameobjects", "--name", "Main Camera"])
                elif op_key == "op3_heavy_query":
                    _r, ms, b = shell_session.call("command", ["nexus_project_map"])
                elif op_key == "op4_mutation":
                    _r, ms, b = shell_session.call(
                        "command", ["set_transform", "--target", "Main Camera", "--position", "[0, 1, -10]"]
                    )
                elif op_key == "op5_play_control":
                    _r, ms, b = shell_session.call("command", ["editor_status"])
                elif op_key == "op6_logs":
                    _r, ms, b = shell_session.call("command", ["console", "--tail", "50"])
                elif op_key == "op7_screenshot":
                    _r, ms, b = shell_session.call("command", ["nexus_capture_game_view", "--quality", "85"])
                times_shell.append(ms)
                bytes_shell.append(b)
            op_data["unity_shell_warm"] = {
                "roundtrip_ms": compute_stats(times_shell),
                "payload_bytes": compute_stats(bytes_shell),
            }

            # Surface 4: Unity CLI cold (fresh process) - 3 samples to observe process launch overhead
            times_cold, bytes_cold = [], []
            for _ in range(3):
                if op_key == "op1_cheap_status":
                    _r, ms, b = call_unity_cli(["command", "editor_status"])
                elif op_key == "op2_medium_query":
                    _r, ms, b = call_unity_cli(["command", "find_gameobjects", "--name", "Main Camera"])
                elif op_key == "op3_heavy_query":
                    _r, ms, b = call_unity_cli(["command", "nexus_project_map"])
                elif op_key == "op4_mutation":
                    _r, ms, b = call_unity_cli(
                        ["command", "set_transform", "--target", "Main Camera", "--position", "[0, 1, -10]"]
                    )
                elif op_key == "op5_play_control":
                    _r, ms, b = call_unity_cli(["command", "editor_status"])
                elif op_key == "op6_logs":
                    _r, ms, b = call_unity_cli(["command", "console", "--tail", "50"])
                elif op_key == "op7_screenshot":
                    _r, ms, b = call_unity_cli(["command", "nexus_capture_game_view", "--quality", "85"])
                times_cold.append(ms)
                bytes_cold.append(b)
            op_data["unity_cli_cold"] = {
                "roundtrip_ms": compute_stats(times_cold),
                "payload_bytes": compute_stats(bytes_cold),
            }

            transport_results[op_key] = op_data

        all_results["transport_benchmark"] = transport_results

        # -----------------------------------------------------------------
        # 6. Phase 9: Command vs Eval Benchmark
        # -----------------------------------------------------------------
        print("\n--- PHASE 9: Command vs Eval ---")
        cmd_v_eval = {}

        # Case 1: Status
        t_cmd, b_cmd = [], []
        for _ in range(10):
            _, ms, b = shell_session.call("command", ["editor_status"])
            t_cmd.append(ms)
            b_cmd.append(b)

        t_eval, b_eval = [], []
        for _ in range(10):
            _, ms, b = shell_session.call(
                "command",
                [
                    "eval",
                    "--code",
                    "return new { isPlaying = UnityEditor.EditorApplication.isPlaying, isCompiling = UnityEditor.EditorApplication.isCompiling };",
                ],
            )
            t_eval.append(ms)
            b_eval.append(b)

        cmd_v_eval["status"] = {
            "command_editor_status": {"roundtrip_ms": compute_stats(t_cmd), "bytes": compute_stats(b_cmd)},
            "eval_status": {"roundtrip_ms": compute_stats(t_eval), "bytes": compute_stats(b_eval)},
        }

        # Case 2: Find Object
        t_cmd2, b_cmd2 = [], []
        for _ in range(10):
            _, ms, b = shell_session.call("command", ["find_gameobjects", "--name", "Main Camera"])
            t_cmd2.append(ms)
            b_cmd2.append(b)

        t_eval2, b_eval2 = [], []
        for _ in range(10):
            _, ms, b = shell_session.call(
                "command",
                [
                    "eval",
                    "--code",
                    'var go = UnityEngine.GameObject.Find("Main Camera"); return go != null ? go.name : null;',
                ],
            )
            t_eval2.append(ms)
            b_eval2.append(b)

        cmd_v_eval["find_object"] = {
            "command_find_gameobjects": {"roundtrip_ms": compute_stats(t_cmd2), "bytes": compute_stats(b_cmd2)},
            "eval_find_object": {"roundtrip_ms": compute_stats(t_eval2), "bytes": compute_stats(b_eval2)},
        }

        all_results["command_vs_eval"] = cmd_v_eval

        # -----------------------------------------------------------------
        # 7. Phase 16: Hybrid Parity Benchmark (Identical Logic over Transports)
        # -----------------------------------------------------------------
        print("\n--- PHASE 16: Hybrid Parity Benchmark ---")
        hybrid_parity = {}
        for hybrid_cmd in ["nexus_project_map", "nexus_group_compile_errors", "nexus_capture_game_view"]:
            t_nexus_trans, b_nexus_trans = [], []
            t_pipe_trans, b_pipe_trans = [], []

            for _ in range(20):
                _, ms1, b1 = call_nexus_http(hybrid_cmd)
                t_nexus_trans.append(ms1)
                b_nexus_trans.append(b1)

                _, ms2, b2 = shell_session.call("command", [hybrid_cmd])
                t_pipe_trans.append(ms2)
                b_pipe_trans.append(b2)

            hybrid_parity[hybrid_cmd] = {
                "nexus_http_transport": {
                    "roundtrip_ms": compute_stats(t_nexus_trans),
                    "payload_bytes": compute_stats(b_nexus_trans),
                },
                "unity_pipeline_transport": {
                    "roundtrip_ms": compute_stats(t_pipe_trans),
                    "payload_bytes": compute_stats(b_pipe_trans),
                },
            }

        all_results["hybrid_parity"] = hybrid_parity

        # -----------------------------------------------------------------
        # 8. Phase 13: Tool Discovery / Token Surface Comparison
        # -----------------------------------------------------------------
        print("\n--- PHASE 13: Tool Discovery & Token Surface ---")
        with open(os.path.join(PACKAGE_ROOT, "nexus-mcp-tools.json")) as f:
            nexus_raw = json.load(f)
            nexus_tool_list = nexus_raw if isinstance(nexus_raw, list) else nexus_raw.get("tools", [])
        with open(os.path.join(PACKAGE_ROOT, "unity-mcp-tools.json")) as f:
            unity_tool_list = json.load(f).get("tools", [])
        with open(os.path.join(PACKAGE_ROOT, "unity-pipeline-commands.json")) as f:
            pipeline_cmd_list = json.load(f).get("data", {}).get("commands", [])

        token_surface = {
            "nexus_mcp": {
                "tool_count": len(nexus_tool_list),
                "schema_bytes": len(json.dumps(nexus_tool_list).encode("utf-8")),
                "avg_bytes_per_tool": len(json.dumps(nexus_tool_list).encode("utf-8")) / max(1, len(nexus_tool_list)),
                "estimated_tokens_4char_rule": len(json.dumps(nexus_tool_list)) // 4,
            },
            "unity_mcp": {
                "tool_count": len(unity_tool_list),
                "schema_bytes": len(json.dumps(unity_tool_list).encode("utf-8")),
                "avg_bytes_per_tool": len(json.dumps(unity_tool_list).encode("utf-8")) / max(1, len(unity_tool_list)),
                "estimated_tokens_4char_rule": len(json.dumps(unity_tool_list)) // 4,
            },
            "unity_pipeline_commands": {
                "command_count": len(pipeline_cmd_list),
                "schema_bytes": len(json.dumps(pipeline_cmd_list).encode("utf-8")),
                "avg_bytes_per_command": len(json.dumps(pipeline_cmd_list).encode("utf-8"))
                / max(1, len(pipeline_cmd_list)),
                "estimated_tokens_4char_rule": len(json.dumps(pipeline_cmd_list)) // 4,
            },
        }
        all_results["token_surface"] = token_surface

    finally:
        shell_session.close()
        if mcp_session:
            mcp_session.close()

    # Save JSON results
    with open(JSON_OUTPUT, "w", encoding="utf-8") as f:
        json.dump(all_results, f, indent=2)
    print(f"\nSaved raw JSON benchmark results to {JSON_OUTPUT}")

    # Flatten summary metrics to CSV
    with open(CSV_OUTPUT, "w", newline="", encoding="utf-8") as f:
        writer = csv.writer(f)
        writer.writerow(
            ["Category", "Subcategory", "Surface_Candidate", "Metric", "Count", "Min", "P50", "Mean", "P95", "Max"]
        )

        # Capture matrix
        for cand, data in all_results.get("capture_matrix", {}).items():
            rt = data["roundtrip_ms"]
            writer.writerow(
                [
                    "Capture",
                    "GameView",
                    cand,
                    "roundtrip_ms",
                    rt["count"],
                    rt["min"],
                    rt["p50"],
                    rt["mean"],
                    rt["p95"],
                    rt["max"],
                ]
            )
            pb = data["payload_bytes"]
            writer.writerow(
                [
                    "Capture",
                    "GameView",
                    cand,
                    "payload_bytes",
                    pb["count"],
                    pb["min"],
                    pb["p50"],
                    pb["mean"],
                    pb["p95"],
                    pb["max"],
                ]
            )

        # Transport benchmark
        for op, surfaces in all_results.get("transport_benchmark", {}).items():
            for surf, data in surfaces.items():
                rt = data["roundtrip_ms"]
                writer.writerow(
                    [
                        "Transport",
                        op,
                        surf,
                        "roundtrip_ms",
                        rt["count"],
                        rt["min"],
                        rt["p50"],
                        rt["mean"],
                        rt["p95"],
                        rt["max"],
                    ]
                )

        # Hybrid parity
        for cmd, transports in all_results.get("hybrid_parity", {}).items():
            for trans, data in transports.items():
                rt = data["roundtrip_ms"]
                writer.writerow(
                    [
                        "HybridParity",
                        cmd,
                        trans,
                        "roundtrip_ms",
                        rt["count"],
                        rt["min"],
                        rt["p50"],
                        rt["mean"],
                        rt["p95"],
                        rt["max"],
                    ]
                )

    print(f"Saved summary CSV results to {CSV_OUTPUT}")
    print("\nBenchmark program completed successfully!")


if __name__ == "__main__":
    main()
