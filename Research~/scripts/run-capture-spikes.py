#!/usr/bin/env python3
import json
import os
import sys
import time

sys.dont_write_bytecode = True

PACKAGE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
EDITOR_DIR = os.path.join(PACKAGE_ROOT, "Editor")
if EDITOR_DIR not in sys.path:
    sys.path.insert(0, EDITOR_DIR)

from nexus_bridge._transport import call_unity  # noqa: E402


def wait_for_server(timeout_sec=30):
    start = time.time()
    while time.time() - start < timeout_sec:
        try:
            status = call_unity("get_server_status")
            if "result" in status and status["result"].get("serverAlive"):
                return status["result"]
        except Exception:
            pass
        time.sleep(1)
    raise RuntimeError("Timed out waiting for Unity MCP server")


def call_or_fail(method, params=None):
    res = call_unity(method, params or {})
    if "error" in res:
        raise RuntimeError(f"RPC {method} failed: {res['error']}")
    return res.get("result", res)


def run_all_spikes():  # noqa: PLR0915
    print("=" * 80)
    print(" NEXUS CAPTURE V2 — EXPERIMENTAL SPIKES A-E BENCHMARK SUITE")
    print("=" * 80)

    server_status = wait_for_server()
    print(f"Unity Version   : {server_status.get('unityVersion')}")
    print(f"Process ID      : {server_status.get('processId')}")
    print(f"Session ID      : {server_status.get('sessionId')}")
    print(f"Project Path    : {server_status.get('projectPath')}")
    print(f"Server State    : {server_status.get('state')}")
    print("=" * 80)

    # -------------------------------------------------------------------------
    # SPIKE A: ASYNC RPC LIFECYCLE
    # -------------------------------------------------------------------------
    print("\n[+] Running Spike A: Async RPC Lifecycle & Thread Dispatch Trace...")
    spike_a = call_or_fail("test_spike_a_async_rpc")
    trace_a = spike_a.get("trace", {})

    print("-" * 80)
    print(" SPIKE A: THREAD ID TRACE & TIMESTAMPS")
    print("-" * 80)
    stages = [
        ("1. HTTP request received", trace_a.get("http_received_thread_id"), f"{trace_a.get('http_received_time_ms', 0):.4f} ms"),
        ("2. Main action dispatched & start", trace_a.get("main_action_thread_id"), f"{trace_a.get('main_action_start_ms', 0):.4f} ms"),
        ("3. Source acquisition complete", trace_a.get("main_action_thread_id"), f"{trace_a.get('source_acquisition_ms', 0):.4f} ms"),
        ("4. Readback request issued", trace_a.get("main_action_thread_id"), f"{trace_a.get('readback_request_start_ms', 0):.4f} ms"),
        ("5. Readback done (update tick poll)", trace_a.get("readback_done_thread_id"), f"{trace_a.get('readback_done_ms', 0):.4f} ms (ticks: {trace_a.get('update_tick_count')})"),
        ("6. ImageConversion encoding", trace_a.get("encode_thread_id"), f"duration: {trace_a.get('encode_duration_ms', 0):.4f} ms"),
        ("7. TCS.SetResult called", trace_a.get("tcs_set_result_thread_id"), f"{trace_a.get('tcs_set_result_ms', 0):.4f} ms"),
        ("8. TCS continuation executed", trace_a.get("continuation_thread_id"), f"{trace_a.get('continuation_start_ms', 0):.4f} ms"),
        ("9. Base64 & JSON serialization", trace_a.get("base64_thread_id"), f"duration: {trace_a.get('base64_duration_ms', 0):.4f} ms"),
        ("10. HTTP response sent", trace_a.get("response_send_thread_id"), f"total: {trace_a.get('total_latency_ms', 0):.4f} ms"),
    ]
    print(f"{'Lifecycle Stage':<38} | {'Thread ID':<10} | {'Timing / Notes'}")
    print("-" * 80)
    for name, tid, timing in stages:
        print(f"{name:<38} | {tid!s:<10} | {timing}")
    print("-" * 80)
    print(f"Proof: Main thread never blocked (.Wait/.Result) : {trace_a.get('proof_main_thread_never_blocked')}")
    print(f"Proof: Continuation ran on worker thread         : {trace_a.get('proof_continuation_off_thread')} (Thread {trace_a.get('continuation_thread_id')} != {trace_a.get('main_action_thread_id')})")
    print(f"Proof: Base64 ran on worker thread              : {trace_a.get('proof_base64_off_thread')} (Thread {trace_a.get('base64_thread_id')} != {trace_a.get('main_action_thread_id')})")
    print(f"Proof: HTTP response sent from worker thread    : {trace_a.get('proof_response_off_thread')} (Thread {trace_a.get('response_send_thread_id')} != {trace_a.get('main_action_thread_id')})")

    # -------------------------------------------------------------------------
    # SPIKE B: SOURCE ACQUISITION COMPARISON
    # -------------------------------------------------------------------------
    print("\n[+] Running Spike B: Source Acquisition Comparison (Option A vs Option B)...")
    spike_b_edit = call_or_fail("test_spike_b_source_acquisition")

    # Now enter play mode to test play mode acquisition
    print("    Switching to Play Mode for comparison...")
    call_or_fail("toggle_play_mode", {"value": True})
    time.sleep(2)
    wait_for_server()
    spike_b_play = call_or_fail("test_spike_b_source_acquisition")
    print("    Restoring Edit Mode...")
    call_or_fail("toggle_play_mode", {"value": False})
    time.sleep(2)
    wait_for_server()

    matrix_cells = list(spike_b_edit.get("matrix", [])) + list(spike_b_play.get("matrix", []))

    print("-" * 96)
    print(" SPIKE B: COMPATIBILITY & LATENCY MATRIX (Option A vs Option B)")
    print("-" * 96)
    print(f"{'Condition':<22} | {'Option A Works?':<16} | {'Option A Latency':<16} | {'Option B Works?':<16} | {'Option B Latency':<16}")
    print("-" * 96)
    for cell in matrix_cells:
        cond = cell.get("condition", "unknown")
        opt_a = cell.get("option_a_screencapture", {})
        opt_b = cell.get("option_b_reflected_blit", {})
        a_works = "YES" if opt_a.get("works_in_edit_mode") else "NO"
        a_lat = f"{opt_a.get('latency_ms', 0):.4f} ms"
        b_works = "YES" if opt_b.get("works_in_edit_mode") else "NO"
        b_lat = f"{opt_b.get('latency_ms', 0):.4f} ms"
        print(f"{cond:<22} | {a_works:<16} | {a_lat:<16} | {b_works:<16} | {b_lat:<16}")
    print("-" * 96)

    # -------------------------------------------------------------------------
    # SPIKE C: NORMALIZED RT CORRECTNESS
    # -------------------------------------------------------------------------
    print("\n[+] Running Spike C: Normalized RT Correctness...")
    spike_c = call_or_fail("test_spike_c_normalized_rt")

    print("-" * 80)
    print(" SPIKE C: BLIT COSTS ON METAL GPU")
    print("-" * 80)
    blit_costs = spike_c.get("blit_costs_metal", {})
    for res_name, data in blit_costs.items():
        print(f"Resolution {res_name:<6} ({data.get('width')}x{data.get('height')}): avg={data.get('avg_ms', 0):.4f} ms, min={data.get('min_ms', 0):.4f} ms, max={data.get('max_ms', 0):.4f} ms")

    print("\n" + "-" * 80)
    print(" SPIKE C: FORMAT COMPATIBILITY & ENCODER SUPPORT")
    print("-" * 80)
    print(f"{'Format':<18} | {'Readback OK?':<14} | {'Row Bytes (Actual/Expected)':<28} | {'PNG Encoder OK?'}")
    print("-" * 80)
    for fmt in spike_c.get("format_compatibility", []):
        name = fmt.get("format")
        rb = "YES" if fmt.get("readback_success") else "NO"
        rows = f"{fmt.get('actual_row_size')} / {fmt.get('expected_row_size')}"
        enc = "YES" if fmt.get("encoder_png_supported") else "NO"
        print(f"{name:<18} | {rb:<14} | {rows:<28} | {enc}")

    print("\n" + "-" * 80)
    print(" SPIKE C: ROWBYTES ALIGNMENT FINDINGS")
    print("-" * 80)
    for align in spike_c.get("row_alignments", []):
        w = align.get("width")
        h = align.get("height")
        act = align.get("actual_row_data_size")
        exp = align.get("expected_row_bytes")
        pad = align.get("padding_bytes")
        has_p = align.get("has_padding")
        print(f"Width {w}x{h}: actual row size = {act}, expected = {exp}, padding = {pad} bytes (has padding: {has_p})")

    orient = spike_c.get("orientation_test", {})
    print("\n" + "-" * 80)
    print(" SPIKE C: ORIENTATION & Y-FLIP VERDICT")
    print("-" * 80)
    print(f"Row 0 RGB (top of readback) : {orient.get('row_0_rgb')}")
    print(f"Row H-1 RGB (bottom)        : {orient.get('row_last_rgb')}")
    print(f"Requires Vertical Y-Flip    : {orient.get('requires_y_flip')}")
    print(f"Verdict                     : {orient.get('verdict')}")

    color_hdr = spike_c.get("color_space_and_hdr", {})
    print("\n" + "-" * 80)
    print(" SPIKE C: COLOR SPACE & HDR CLAMPING")
    print("-" * 80)
    print(f"Active Color Space          : {color_hdr.get('active_color_space')}")
    print(f"HDR R clamped byte (2.5f)   : {color_hdr.get('hdr_r_clamped_byte')} (expected 255)")
    print(f"HDR G clamped byte (1.8f)   : {color_hdr.get('hdr_g_clamped_byte')} (expected 255)")
    print(f"HDR B byte (0.5f linear)    : {color_hdr.get('hdr_b_byte')} (sRGB converted: ~188)")
    print(f"HDR safely clamped          : {color_hdr.get('hdr_safely_clamped')}")

    downscale = spike_c.get("downscaling", {})
    print("\n" + "-" * 80)
    print(" SPIKE C: DOWNSCALING GPU BLIT VS CPU")
    print("-" * 80)
    print(f"4K -> 1080p GPU Blit Time   : {downscale.get('gpu_downscale_blit_ms', 0):.4f} ms")
    print(f"4K -> 1080p CPU Downscale   : ~{downscale.get('cpu_downscale_estimated_ms', 0):.1f} ms")
    print(f"GPU is Faster               : {downscale.get('gpu_is_faster')}")

    # -------------------------------------------------------------------------
    # SPIKE D: ENCODER COMPARISON
    # -------------------------------------------------------------------------
    print("\n[+] Running Spike D: Encoder Comparison on Identical 1080p Pixels...")
    spike_d = call_or_fail("test_spike_d_encoder_comparison")

    print("-" * 90)
    print(" SPIKE D: ENCODER BENCHMARK (1080p, 10 iterations each)")
    print("-" * 90)
    print(f"{'Encoder Candidate':<28} | {'Median ms':<10} | {'Min ms':<10} | {'Max ms':<10} | {'GC Alloc':<10} | {'Output Bytes'}")
    print("-" * 90)
    candidates = [
        ("Baseline: Texture2D PNG", spike_d.get("baseline_texture2d_png", {})),
        ("Candidate 1: Native PNG", spike_d.get("candidate1_native_png", {})),
        ("Candidate 2a: Native JPG 75", spike_d.get("candidate2_jpg_75", {})),
        ("Candidate 2b: Native JPG 85", spike_d.get("candidate2_jpg_85", {})),
        ("Candidate 2c: Native JPG 95", spike_d.get("candidate2_jpg_95", {})),
    ]
    for name, c in candidates:
        print(f"{name:<28} | {c.get('median_ms', 0):<10.2f} | {c.get('min_ms', 0):<10.2f} | {c.get('max_ms', 0):<10.2f} | {c.get('gc_alloc_bytes_per_call', 0)!s:<10} | {c.get('output_bytes', 0)}")
    print("-" * 90)

    cand1 = spike_d.get("candidate1_native_png", {})
    print(f"NativeArray -> byte[] (.ToArray()) overhead: {cand1.get('to_array_median_ms', 0):.4f} ms")
    vis = spike_d.get("visual_equivalence", {})
    print(f"Baseline PNG vs Candidate 1 PNG identical bytes: {vis.get('identical_bytes')}")
    print(f"Baseline MD5: {vis.get('hash_a')} == Candidate 1 MD5: {vis.get('hash_b')}")

    # -------------------------------------------------------------------------
    # SPIKE E: R1 VS R2 COMPARISON (100 ITERATIONS)
    # -------------------------------------------------------------------------
    print("\n[+] Running Spike E: R1 vs R2 100-Iteration Comparison...")
    spike_e = call_or_fail("test_spike_e_r1_vs_r2", {"iterations": 100})

    r1 = spike_e.get("r1_benchmark", {})
    r2 = spike_e.get("r2_benchmark", {})
    neg = spike_e.get("r1_negative_test", {})

    print("-" * 88)
    print(" SPIKE E: R1 vs R2 100-CAPTURE BENCHMARK")
    print("-" * 88)
    print(f"{'Metric':<34} | {'R1 (GetData View)':<24} | {'R2 (Persistent NativeArray)':<24}")
    print("-" * 88)
    print(f"{'Iterations':<34} | {r1.get('iterations'):<24} | {r2.get('iterations'):<24}")
    print(f"{'Latency Min (ms)':<34} | {r1.get('min_ms', 0):<24.2f} | {r2.get('min_ms', 0):<24.2f}")
    print(f"{'Latency Median (ms)':<34} | {r1.get('median_ms', 0):<24.2f} | {r2.get('median_ms', 0):<24.2f}")
    print(f"{'Latency p95 (ms)':<34} | {r1.get('p95_ms', 0):<24.2f} | {r2.get('p95_ms', 0):<24.2f}")
    print(f"{'Latency Max (ms)':<34} | {r1.get('max_ms', 0):<24.2f} | {r2.get('max_ms', 0):<24.2f}")
    print(f"{'GC Alloc per Capture (bytes)':<34} | {r1.get('gc_alloc_bytes_per_capture', 0):<24} | {r2.get('gc_alloc_bytes_per_capture', 0):<24}")
    get_data_str = f"{r1.get('get_data_avg_ms', 0):.4f} ms"
    print(f"{'GetData<byte>() Avg Duration':<34} | {get_data_str:<24} | {'N/A':<24}")
    print(f"{'GetData is Zero-Alloc View?':<34} | {r1.get('get_data_is_zero_alloc_view')!s:<24} | {'N/A':<24}")
    print("-" * 88)

    print("\n" + "-" * 88)
    print(" SPIKE E: R1 NEGATIVE TEST (LATE GETDATA)")
    print("-" * 88)
    print(f"Delayed GetData Succeeded   : {neg.get('success')}")
    print(f"Readback HasError Flag      : {neg.get('has_error')}")
    print(f"Bytes Read Length           : {neg.get('length')}")
    print(f"Verdict                     : {neg.get('verdict')}")

    # Output full JSON artifact
    full_artifact = {
        "server_status": server_status,
        "spike_a": spike_a,
        "spike_b": {"matrix": matrix_cells},
        "spike_c": spike_c,
        "spike_d": spike_d,
        "spike_e": spike_e,
    }
    artifact_path = os.path.join(PACKAGE_ROOT, "capture_spikes_report.json")
    with open(artifact_path, "w") as f:
        json.dump(full_artifact, f, indent=2)
    print(f"\n[✓] Raw benchmark data written to {artifact_path}")
    print("=" * 80)


if __name__ == "__main__":
    run_all_spikes()
