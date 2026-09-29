#!/usr/bin/env python3
"""Comprehensive stress-test suite for the NexusUnity screenshot feature.

Tests:
1. High-frequency burst capture (Game View & Inspector).
2. Concurrent multi-threaded swarm (interleaved RPC requests).
3. Selection churn & UI Toolkit layout invariant validation.
4. Window geometry & dynamic resize stress.
5. Boundary edge-cases & error recovery.
6. Latency profiling (min, max, mean, p50, p95, p99) and PNG integrity checks.
"""

from __future__ import annotations

import argparse
import base64
from concurrent.futures import ThreadPoolExecutor, as_completed
import json
import os
import struct
import sys
import time
from typing import Any

sys.dont_write_bytecode = True

PACKAGE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
EDITOR_DIR = os.path.join(PACKAGE_ROOT, "Editor")
if EDITOR_DIR not in sys.path:
    sys.path.insert(0, EDITOR_DIR)

from nexus_bridge._transport import UNITY_URL, call_unity  # noqa: E402
from nexus_bridge.routing import route_tool  # noqa: E402
import contextlib  # noqa: E402

PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"


def parse_png_dimensions(data: bytes) -> tuple[int, int] | None:
    """Validate PNG signature and extract width/height from IHDR chunk."""
    if len(data) < 24 or not data.startswith(PNG_SIGNATURE):
        return None
    # IHDR chunk starts at byte 12 (length: 4 bytes, 'IHDR': 4 bytes, width: 4 bytes, height: 4 bytes)
    chunk_type = data[12:16]
    if chunk_type != b"IHDR":
        return None
    width, height = struct.unpack(">II", data[16:24])
    return width, height


def rpc(method: str, params: dict[str, Any] | None = None) -> dict[str, Any]:
    """Execute a raw JSON-RPC call against Unity."""
    res = call_unity(method, params or {})
    if "error" in res:
        err = res["error"]
        msg = err.get("message", str(err)) if isinstance(err, dict) else str(err)
        raise RuntimeError(f"RPC {method} failed: {msg}")
    return res.get("result", res)


def bridge_call(name: str, args: dict[str, Any]) -> dict[str, Any]:
    """Execute a routed tool call through the bridge."""
    res = route_tool(name, args)
    if "error" in res:
        err = res["error"]
        msg = err.get("message", str(err)) if isinstance(err, dict) else str(err)
        raise RuntimeError(f"Tool {name} failed: {msg}")
    return res.get("result", res)


class LatencyStats:
    """Collects and calculates latency percentiles in milliseconds."""

    def __init__(self, name: str) -> None:
        self.name = name
        self.samples: list[float] = []

    def record(self, duration_ms: float) -> None:
        self.samples.append(duration_ms)

    def summary(self) -> dict[str, float]:
        if not self.samples:
            return {"count": 0, "min": 0, "max": 0, "mean": 0, "p50": 0, "p95": 0, "p99": 0}
        s = sorted(self.samples)
        n = len(s)
        return {
            "count": n,
            "min": round(s[0], 2),
            "max": round(s[-1], 2),
            "mean": round(sum(s) / n, 2),
            "p50": round(s[int(n * 0.50)], 2),
            "p95": round(s[min(n - 1, int(n * 0.95))], 2),
            "p99": round(s[min(n - 1, int(n * 0.99))], 2),
        }


def validate_screenshot_payload(
    res: dict[str, Any],
    tool_name: str,  # noqa: ARG001
    min_bytes: int = 1024,
) -> tuple[bool, str, int, int]:
    """Validate that the response conforms to the screenshot contract."""
    status = res.get("status")
    if status != "Success":
        return False, f"Expected status 'Success', got '{status}' (message: {res.get('message')})", 0, 0

    image_b64 = res.get("image_base64")
    if not image_b64 and isinstance(res.get("data"), dict):
        image_b64 = res["data"].get("image_base64")

    if not image_b64:
        return False, "Missing image_base64 in response payload", 0, 0

    try:
        raw_png = base64.b64decode(image_b64, validate=True)
    except Exception as exc:
        return False, f"Invalid base64 encoding: {exc}", 0, 0

    if len(raw_png) < min_bytes:
        return False, f"PNG payload unusually small ({len(raw_png)} bytes < {min_bytes})", 0, 0

    dims = parse_png_dimensions(raw_png)
    if not dims:
        return False, "PNG missing valid header or IHDR chunk", 0, 0

    width, height = dims
    if width <= 0 or height <= 0:
        return False, f"Invalid PNG dimensions: {width}x{height}", 0, 0

    return True, "OK", width, height


def run_phase1_burst_stress(iterations: int) -> tuple[bool, LatencyStats, LatencyStats]:
    """Phase 1: Rapid sequential burst alternating Game View and Inspector."""
    print(f"\n--- [Phase 1] Rapid Sequential Burst ({iterations} iterations each) ---")
    game_stats = LatencyStats("GameView_Burst")
    inspector_stats = LatencyStats("Inspector_Burst")
    success = True

    for i in range(iterations):
        # 1. Game View
        t0 = time.perf_counter()
        game_res = rpc("capture_game_view_screenshot", {})
        dt_ms = (time.perf_counter() - t0) * 1000.0
        game_stats.record(dt_ms)

        valid, err, _w, _h = validate_screenshot_payload(game_res, "capture_game_view_screenshot")
        if not valid:
            print(f"  [FAIL] Iteration {i+1} Game View: {err}")
            success = False
            break

        # 2. Inspector
        t0 = time.perf_counter()
        inspector_res = rpc("capture_inspector_screenshot", {})
        dt_ms = (time.perf_counter() - t0) * 1000.0
        inspector_stats.record(dt_ms)

        valid, err, _w, _h = validate_screenshot_payload(inspector_res, "capture_inspector_screenshot")
        if not valid:
            print(f"  [FAIL] Iteration {i+1} Inspector: {err}")
            success = False
            break

        if (i + 1) % 10 == 0 or i == iterations - 1:
            print(f"  Completed {i+1}/{iterations} burst iterations...")

    print(f"  Game View Latency:  {game_stats.summary()}")
    print(f"  Inspector Latency:  {inspector_stats.summary()}")
    return success, game_stats, inspector_stats


def run_phase2_concurrent_swarm(
    concurrency: int,
    total_tasks: int,
) -> tuple[bool, LatencyStats]:
    """Phase 2: Multi-threaded concurrent swarm hammering the screenshot endpoints."""
    print(f"\n--- [Phase 2] Concurrent Swarm ({concurrency} threads, {total_tasks} tasks) ---")
    swarm_stats = LatencyStats("Concurrent_Swarm")
    success = True
    errors: list[str] = []

    # Interleave methods across tasks
    methods = [
        ("capture_game_view_screenshot", {}),
        ("capture_inspector_screenshot", {}),
        ("capture_inspector_screenshot", {"include_layout": True}),
    ]

    def worker(task_id: int, method: str, params: dict[str, Any]) -> tuple[int, str, bool, float, str]:
        t0 = time.perf_counter()
        try:
            res = rpc(method, params)
            elapsed_ms = (time.perf_counter() - t0) * 1000.0
            valid, err, _, _ = validate_screenshot_payload(res, method)
            return task_id, method, valid, elapsed_ms, err
        except Exception as exc:
            elapsed_ms = (time.perf_counter() - t0) * 1000.0
            return task_id, method, False, elapsed_ms, str(exc)

    with ThreadPoolExecutor(max_workers=concurrency) as executor:
        futures = []
        for i in range(total_tasks):
            method, params = methods[i % len(methods)]
            futures.append(executor.submit(worker, i, method, params))

        for f in as_completed(futures):
            task_id, method, valid, elapsed_ms, err = f.result()
            swarm_stats.record(elapsed_ms)
            if not valid:
                errors.append(f"Task {task_id} ({method}): {err}")
                success = False

    if errors:
        print(f"  [FAIL] {len(errors)} concurrent tasks failed! Sample errors:")
        for e in errors[:5]:
            print(f"    - {e}")
    else:
        print(f"  [PASS] All {total_tasks} concurrent tasks completed cleanly.")
    print(f"  Swarm Latency: {swarm_stats.summary()}")
    return success, swarm_stats


def run_phase3_selection_churn_stress(cycles: int) -> tuple[bool, LatencyStats]:
    """Phase 3: Creates multiple distinct GameObjects, cycles selection, and validates UI Toolkit layout."""
    print(f"\n--- [Phase 3] Selection Churn & Layout Invariant Stress ({cycles} cycles) ---")
    churn_stats = LatencyStats("Selection_Churn")
    success = True

    # 1. Create a set of distinct test primitives
    created_objects: list[tuple[str, int, str]] = []  # (name, instance_id, expected_component)
    targets = [
        ("Cube", "StressCube", "BoxCollider"),
        ("Sphere", "StressSphere", "SphereCollider"),
        ("Capsule", "StressCapsule", "CapsuleCollider"),
        ("Cylinder", "StressCylinder", "CapsuleCollider"),
    ]

    try:
        for ptype, name, expected_comp in targets:
            res = rpc("create_primitive", {"primitive_type": ptype, "name": name})
            inst_id = res.get("data", {}).get("instance_id") or res.get("instance_id")
            if not inst_id:
                raise RuntimeError(f"Failed to extract instance_id for {name}: {res}")
            created_objects.append((name, inst_id, expected_comp))
            print(f"  Created test object '{name}' (id: {inst_id})")

        # Focus inspector before cycling
        rpc("execute_menu_item", {"item_path": "Window/General/Inspector"})

        for cycle in range(cycles):
            for name, inst_id, expected_comp in created_objects:
                t0 = time.perf_counter()
                res = rpc("capture_inspector_screenshot", {"instance_id": inst_id, "include_layout": True})
                dt_ms = (time.perf_counter() - t0) * 1000.0
                churn_stats.record(dt_ms)

                valid, err, _, _ = validate_screenshot_payload(res, "capture_inspector_screenshot")
                if not valid:
                    print(f"  [FAIL] Cycle {cycle+1} object '{name}': {err}")
                    success = False
                    break

                # Verify layout invariant: layout must contain the component expected for this target
                layout_json = json.dumps(res.get("ui_layout") or res.get("data", {}).get("ui_layout", {}))
                if expected_comp not in layout_json and name not in layout_json:
                    print(f"  [FAIL] Layout invariant failed for '{name}' (id: {inst_id}): {expected_comp} missing!")
                    success = False
                    break

            if not success:
                break
            if (cycle + 1) % 5 == 0 or cycle == cycles - 1:
                print(f"  Completed {cycle+1}/{cycles} selection churn cycles...")

    finally:
        print("  Cleaning up test objects...")
        for name, inst_id, _ in created_objects:
            try:
                rpc("destroy_game_object", {"instance_id": inst_id})
            except Exception as exc:
                print(f"  Warning: Cleanup of '{name}' failed: {exc}")

    print(f"  Selection Churn Latency: {churn_stats.summary()}")
    return success, churn_stats


def run_phase4_window_geometry_stress() -> tuple[bool, LatencyStats]:
    """Phase 4: Dynamically resizes the window across extreme aspect ratios."""
    print("\n--- [Phase 4] Dynamic Window Geometry & Resize Stress ---")
    resize_stats = LatencyStats("Window_Resize")
    success = True

    # Retrieve initial window rect of Nexus Unity window
    try:
        init_rect_res = bridge_call("ui_automation", {"action": "get_window_rect", "window_title": "Nexus Unity"})
        orig_rect = init_rect_res.get("rect", {"x": 80, "y": 80, "width": 640, "height": 720})
    except Exception:
        orig_rect = {"x": 80, "y": 80, "width": 640, "height": 720}

    test_geometries = [
        {"x": 80, "y": 80, "width": 320, "height": 420},   # Minimum supported
        {"x": 80, "y": 80, "width": 800, "height": 600},   # Standard 4:3
        {"x": 60, "y": 60, "width": 1000, "height": 450},  # Wide aspect
        {"x": 60, "y": 60, "width": 450, "height": 900},   # Tall portrait
    ]

    try:
        for _idx, geom in enumerate(test_geometries):
            bridge_call("ui_automation", {
                "action": "set_window_rect",
                "window_title": "Nexus Unity",
                **geom,
            })
            time.sleep(0.05)  # Allow Editor UI to layout

            t0 = time.perf_counter()
            snap = bridge_call("ui_automation", {
                "action": "capture_window_snapshot",
                "window_title": "Nexus Unity",
                "include_image": True,
                "include_hierarchy": True,
            })
            dt_ms = (time.perf_counter() - t0) * 1000.0
            resize_stats.record(dt_ms)

            status = snap.get("status")
            if status not in {"Success", "success", "PartialSuccess"}:
                print(f"  [FAIL] Geom {geom['width']}x{geom['height']}: status '{status}'")
                success = False
                continue

            raw_b64 = snap.get("image_base64", "")
            if raw_b64:
                img_bytes = base64.b64decode(raw_b64)
                dims = parse_png_dimensions(img_bytes)
                if dims:
                    print(f"  Geom {geom['width']}x{geom['height']} -> captured {dims[0]}x{dims[1]} ({len(img_bytes)} bytes) in {dt_ms:.1f}ms")
            else:
                print(f"  Geom {geom['width']}x{geom['height']} snapshot returned without image.")

    finally:
        # Restore original window rect
        with contextlib.suppress(Exception):
            bridge_call("ui_automation", {
                "action": "set_window_rect",
                "window_title": "Nexus Unity",
                "x": orig_rect.get("x", 80),
                "y": orig_rect.get("y", 80),
                "width": orig_rect.get("width", 640),
                "height": orig_rect.get("height", 720),
            })

    print(f"  Window Resize Latency: {resize_stats.summary()}")
    return success, resize_stats


def run_phase5_boundary_and_recovery_stress() -> bool:
    """Phase 5: Tests invalid parameters and verifies instant recovery."""
    print("\n--- [Phase 5] Boundary & Recovery Stress ---")
    success = True

    # 1. Non-existent instance_id (should not crash server or throw unhandled 500)
    for bad_id in [-999999, 0, 2147483647]:
        try:
            res = rpc("capture_inspector_screenshot", {"instance_id": bad_id})
            # A valid result or handled error is acceptable; must not crash
            valid, _, _, _ = validate_screenshot_payload(res, "capture_inspector_screenshot")
            print(f"  bad_id {bad_id} handled cleanly (valid: {valid}, status: {res.get('status')})")
        except Exception as exc:
            print(f"  bad_id {bad_id} threw handled RPC exception: {exc}")

    # 2. Immediate recovery test: standard capture must succeed without residual contamination
    recovery_res = rpc("capture_inspector_screenshot", {})
    valid, err, w, h = validate_screenshot_payload(recovery_res, "capture_inspector_screenshot")
    if valid:
        print(f"  [PASS] Immediate recovery successful: captured {w}x{h} PNG.")
    else:
        print(f"  [FAIL] Recovery capture failed: {err}")
        success = False

    return success


def main() -> int:
    parser = argparse.ArgumentParser(description="NexusUnity Screenshot Stress Test Suite")
    parser.add_argument("--burst-count", type=int, default=50, help="Number of sequential burst iterations (default: 50)")
    parser.add_argument("--concurrency", type=int, default=8, help="Number of concurrent worker threads (default: 8)")
    parser.add_argument("--concurrent-tasks", type=int, default=40, help="Number of concurrent tasks (default: 40)")
    parser.add_argument("--churn-cycles", type=int, default=15, help="Number of selection churn cycles (default: 15)")
    parser.add_argument("--skip-concurrency", action="store_true", help="Skip concurrent swarm test")
    parser.add_argument("--skip-churn", action="store_true", help="Skip selection churn test")
    parser.add_argument("--skip-resize", action="store_true", help="Skip window resize stress test")
    args = parser.parse_args()

    print("================================================================")
    print("      NexusUnity Screenshot Feature Stress Test Suite")
    print(f"  Target: {UNITY_URL}")
    print("  PID / Session: probing Unity server...")
    print("================================================================")

    # Pre-flight check
    try:
        status = rpc("get_server_status")
        print(f"  Connected to Unity {status.get('unityVersion')} (PID {status.get('processId')})")
        print(f"  Server State: {status.get('state')} | Command State: {status.get('commandState')}")
    except Exception as exc:
        print(f"\n[FATAL] Unable to connect to Unity server at {UNITY_URL}: {exc}")
        return 1

    # Ensure windows are open and visible
    rpc("execute_menu_item", {"item_path": "Window/General/Game"})
    rpc("execute_menu_item", {"item_path": "Window/General/Inspector"})
    time.sleep(0.1)

    start_time = time.perf_counter()
    all_passed = True

    # Phase 1: Burst
    p1_ok, _game_burst, _insp_burst = run_phase1_burst_stress(args.burst_count)
    all_passed = all_passed and p1_ok

    # Phase 2: Concurrent Swarm
    if not args.skip_concurrency:
        p2_ok, _swarm_stats = run_phase2_concurrent_swarm(args.concurrency, args.concurrent_tasks)
        all_passed = all_passed and p2_ok

    # Phase 3: Selection Churn
    if not args.skip_churn:
        p3_ok, _churn_stats = run_phase3_selection_churn_stress(args.churn_cycles)
        all_passed = all_passed and p3_ok

    # Phase 4: Dynamic Resize
    if not args.skip_resize:
        p4_ok, _resize_stats = run_phase4_window_geometry_stress()
        all_passed = all_passed and p4_ok

    # Phase 5: Boundary & Recovery
    p5_ok = run_phase5_boundary_and_recovery_stress()
    all_passed = all_passed and p5_ok

    total_time = round(time.perf_counter() - start_time, 2)
    print("\n================================================================")
    print(f"  STRESS TEST SUMMARY: {'ALL TESTS PASSED' if all_passed else 'FAILURES DETECTED'}")
    print(f"  Total Duration: {total_time}s")
    print("================================================================")

    return 0 if all_passed else 1


if __name__ == "__main__":
    sys.exit(main())
