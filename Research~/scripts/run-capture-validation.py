#!/usr/bin/env python3
"""Nexus Unity Capture V2 - Full Benchmark & Validation Program.

Orchestrates multi-session, statistically rigorous benchmarking across
all 14 core validation questions to select the final production configuration.
"""

import csv
import json
import math
import os
import platform
import subprocess
import sys
import time
from typing import Any, Dict, List, Optional

sys.dont_write_bytecode = True

PACKAGE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
EDITOR_DIR = os.path.join(PACKAGE_ROOT, "Editor")
if EDITOR_DIR not in sys.path:
    sys.path.insert(0, EDITOR_DIR)

from nexus_bridge._transport import call_unity  # noqa: E402


def wait_for_server(timeout_sec: float = 30.0) -> Dict[str, Any]:
    """Wait for the Unity MCP server to be responsive."""
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


def call_or_fail(method: str, params: Optional[Dict[str, Any]] = None) -> Any:
    """Execute a JSON-RPC call and assert success."""
    res = call_unity(method, params or {})
    if "error" in res:
        raise RuntimeError(f"RPC {method} failed: {res['error']}")
    return res.get("result", res)


def bootstrap_ci_95(
    data: List[float], resamples: int = 1000
) -> Dict[str, float]:
    """Calculate 95% bootstrap confidence interval for the mean."""
    if not data:
        return {"ci_low": 0.0, "ci_high": 0.0}
    n = len(data)
    if n == 1:
        return {"ci_low": data[0], "ci_high": data[0]}

    import random

    rng = random.Random(42)
    means = []
    for _ in range(resamples):
        sample = [rng.choice(data) for _ in range(n)]
        means.append(sum(sample) / n)
    means.sort()
    low_idx = int(0.025 * resamples)
    high_idx = int(0.975 * resamples)
    return {"ci_low": means[low_idx], "ci_high": means[high_idx]}


def compute_python_stats(samples: List[float]) -> Dict[str, Any]:
    """Compute standard summary statistics for a sample list."""
    if not samples:
        return {
            "count": 0,
            "min": 0.0,
            "p50": 0.0,
            "mean": 0.0,
            "p95": 0.0,
            "p99": 0.0,
            "max": 0.0,
            "std_dev": 0.0,
            "ci_95_low": 0.0,
            "ci_95_high": 0.0,
        }
    sorted_s = sorted(samples)
    n = len(sorted_s)
    mean_val = sum(sorted_s) / n
    variance = sum((x - mean_val) ** 2 for x in sorted_s) / n
    ci = bootstrap_ci_95(sorted_s)
    return {
        "count": n,
        "min": sorted_s[0],
        "p50": sorted_s[n // 2],
        "mean": mean_val,
        "p95": sorted_s[int(n * 0.95)],
        "p99": sorted_s[int(n * 0.99)],
        "max": sorted_s[-1],
        "std_dev": math.sqrt(variance),
        "ci_95_low": ci["ci_low"],
        "ci_95_high": ci["ci_high"],
    }


def get_hardware_environment() -> Dict[str, Any]:
    """Collect host machine, OS, and GPU environment details."""
    env = {
        "os_version": platform.platform(),
        "processor": platform.processor(),
        "machine": platform.machine(),
        "python_version": sys.version.split()[0],
    }
    if sys.platform == "darwin":
        try:
            model = subprocess.check_output(
                ["sysctl", "-n", "hw.model"], text=True
            ).strip()
            mem_bytes = int(
                subprocess.check_output(
                    ["sysctl", "-n", "hw.memsize"], text=True
                ).strip()
            )
            env["mac_model"] = model
            env["ram_gb"] = round(mem_bytes / (1024**3), 1)
        except Exception:
            pass
    return env


def main():
    print("=" * 80)
    print(" NEXUS CAPTURE V2 — FULL VALIDATION & MEASUREMENT PROGRAM")
    print("=" * 80)

    server_status = wait_for_server()
    hw_env = get_hardware_environment()
    print(
        f"Host Machine    : {hw_env.get('mac_model', 'Unknown')} ({hw_env.get('ram_gb')} GB RAM)"
    )
    print(f"Unity Version   : {server_status.get('unityVersion')}")
    print(f"Session Gen     : {server_status.get('sessionGeneration')}")
    print(f"Server Port     : {server_status.get('port')}")
    print("=" * 80)

    results: Dict[str, Any] = {
        "environment": {
            "hardware": hw_env,
            "unity_version": server_status.get("unityVersion"),
            "session_id": server_status.get("sessionId"),
            "session_generation": server_status.get("sessionGeneration"),
            "timestamp_utc": time.strftime(
                "%Y-%m-%dT%H:%M:%SZ", time.gmtime()
            ),
        }
    }

    # -------------------------------------------------------------------------
    # 0. WARMUP PHASE (>= 25 samples)
    # -------------------------------------------------------------------------
    print("\n[Stage 0/14] Warmup Phase (25 iterations)...")
    for _ in range(25):
        call_or_fail("benchmark_timer_resolution")
    print("Warmup complete.")

    # -------------------------------------------------------------------------
    # 1. TIMER RESOLUTION BENCHMARK
    # -------------------------------------------------------------------------
    print("\n[Stage 1/14] Benchmark Timer Resolution...")
    timer_res = call_or_fail("benchmark_timer_resolution")
    results["timer_resolution"] = timer_res
    print(f"  Stopwatch Freq : {timer_res['stopwatch_frequency_hz']:,} Hz")
    print(f"  Noise Floor p95: {timer_res['noise_floor_p95_ms']:.4f} ms")
    print(f"  Reliable Thresh: {timer_res['reliable_threshold_ms']:.4f} ms")

    # -------------------------------------------------------------------------
    # 2. ASYNC RPC STRESS TEST
    # -------------------------------------------------------------------------
    print(
        "\n[Stage 2/14] Async RPC Stress Test (50 requests, 10 concurrency)..."
    )
    rpc_stress = call_or_fail(
        "benchmark_async_rpc_stress", {"concurrency": 10, "requests": 50}
    )
    results["async_rpc_stress"] = rpc_stress
    print(f"  Completed      : {rpc_stress['completed']}/50")
    print(f"  Errors         : {rpc_stress['errors']}")
    print(f"  Latency p50    : {rpc_stress['latency_ms']['p50']:.2f} ms")
    print(f"  Latency p95    : {rpc_stress['latency_ms']['p95']:.2f} ms")

    # -------------------------------------------------------------------------
    # 3. METAL WIDTH / ROW ALIGNMENT SWEEP
    # -------------------------------------------------------------------------
    print("\n[Stage 3/14] Metal Row Alignment Sweep (450 test points)...")
    metal_sweep = call_or_fail("benchmark_metal_alignment")
    results["metal_alignment_sweep"] = metal_sweep
    pts = metal_sweep.get("results", [])
    successes = [p for p in pts if p.get("success")]
    failures = [p for p in pts if not p.get("success")]
    print(f"  Total points   : {len(pts)}")
    print(f"  Pass count     : {len(successes)}")
    print(f"  Fail count     : {len(failures)}")
    if failures:
        unaligned_ex = failures[0]
        print(
            f"  First failure  : width={unaligned_ex['width']}, format={unaligned_ex['format']}, mod_4={unaligned_ex['mod_4']}"
        )

    # -------------------------------------------------------------------------
    # 4. PADDED PHYSICAL WIDTH TEST
    # -------------------------------------------------------------------------
    print("\n[Stage 4/14] Padded Physical Width Test (logical 1921 on 1924)...")
    padded_res = call_or_fail("benchmark_padded_physical_width")
    results["padded_physical_width"] = padded_res
    print(f"  Readback Pass  : {padded_res['physical_rt_readback_success']}")
    print(f"  Decoded Width  : {padded_res['decoded_width']}")
    print(f"  Dimensions Match: {padded_res['dimensions_exact_match']}")

    # -------------------------------------------------------------------------
    # 5. R1 TEMPORAL CONTRACT DELAYS
    # -------------------------------------------------------------------------
    print(
        "\n[Stage 5/14] R1 GetData() Temporal Contract Delays (0, 1, 2, 3, 5, 10 ticks)..."
    )
    temporal_delays = call_or_fail("benchmark_r1_temporal_delays")
    results["r1_temporal_delays"] = temporal_delays
    for d in temporal_delays.get("delay_records", []):
        print(
            f"  Delay {d['delay_ticks']:2d} ticks: success={d['getdata_success']}, "
            f"len={d['data_length']:,}, hash={d['content_hash_16']}"
        )

    # -------------------------------------------------------------------------
    # 6. DYNAMIC RESOLUTION CYCLING
    # -------------------------------------------------------------------------
    print("\n[Stage 6/14] Dynamic Resolution Cycling (50 cycles)...")
    dyn_res = call_or_fail("benchmark_dynamic_resolution", {"cycles": 50})
    results["dynamic_resolution_cycling"] = dyn_res
    print(
        f"  R1 Success     : {dyn_res['r1_success_count']}/{dyn_res['total_cycles']}"
    )
    print(
        f"  R2 Success     : {dyn_res['r2_success_count']}/{dyn_res['total_cycles']}"
    )
    print(f"  Net Heap Growth: {dyn_res['net_heap_growth_bytes']:,} bytes")

    # -------------------------------------------------------------------------
    # 7. READBACK-ONLY R1 VS R2 (5 sessions x 200 samples = 1,000 samples each)
    # -------------------------------------------------------------------------
    print(
        "\n[Stage 7/14] Readback-Only R1 vs R2 (5 sessions x 200 samples = 1,000 each)..."
    )
    readback_sessions = []
    all_r1_samples = []
    all_r2_samples = []

    for s_idx in range(1, 6):
        print(f"  Running Session {s_idx}/5 (200 samples)...")
        sess_data = call_or_fail(
            "benchmark_readback",
            {
                "iterations": 200,
                "width": 1920,
                "height": 1080,
                "session_index": s_idx,
            },
        )
        readback_sessions.append(sess_data)
        for s in sess_data.get("samples", []):
            if s.get("backend") == "R1":
                all_r1_samples.append(s)
            elif s.get("backend") == "R2":
                all_r2_samples.append(s)

    # Compute aggregate stats for R1 vs R2
    r1_wait_times = [s["readback_wait_ms"] for s in all_r1_samples]
    r2_wait_times = [s["readback_wait_ms"] for s in all_r2_samples]
    r1_getdata_times = [s["getdata_ms"] for s in all_r1_samples]
    r2_getdata_times = [s["getdata_ms"] for s in all_r2_samples]
    r1_submit_times = [s["request_submit_cpu_ms"] for s in all_r1_samples]
    r2_submit_times = [s["request_submit_cpu_ms"] for s in all_r2_samples]

    results["readback_r1_vs_r2"] = {
        "r1_samples_total": len(all_r1_samples),
        "r2_samples_total": len(all_r2_samples),
        "r1_wait_ms": compute_python_stats(r1_wait_times),
        "r2_wait_ms": compute_python_stats(r2_wait_times),
        "r1_getdata_ms": compute_python_stats(r1_getdata_times),
        "r2_getdata_ms": compute_python_stats(r2_getdata_times),
        "r1_submit_cpu_ms": compute_python_stats(r1_submit_times),
        "r2_submit_cpu_ms": compute_python_stats(r2_submit_times),
    }
    print(f"  R1 Wait p50    : {results['readback_r1_vs_r2']['r1_wait_ms']['p50']:.2f} ms "
          f"(mean={results['readback_r1_vs_r2']['r1_wait_ms']['mean']:.2f} ms)")
    print(f"  R2 Wait p50    : {results['readback_r1_vs_r2']['r2_wait_ms']['p50']:.2f} ms "
          f"(mean={results['readback_r1_vs_r2']['r2_wait_ms']['mean']:.2f} ms)")
    print(f"  R1 GetData p50 : {results['readback_r1_vs_r2']['r1_getdata_ms']['p50']:.4f} ms")

    # -------------------------------------------------------------------------
    # 8. MANAGED ALLOCATION BREAKDOWN
    # -------------------------------------------------------------------------
    print("\n[Stage 8/14] Managed Allocation Breakdown...")
    managed_alloc = call_or_fail("benchmark_managed_allocations")
    results["managed_allocations"] = managed_alloc
    print(f"  V1 ReadPixels  : {managed_alloc.get('v1_texture2d_readpixels_alloc_bytes', 0):,} bytes")
    print(f"  V1 Encode PNG  : {managed_alloc.get('v1_encode_to_png_alloc_bytes', 0):,} bytes")
    print(f"  V2 Async Readback: {managed_alloc.get('v2_async_readback_and_getdata_alloc_bytes', 0):,} bytes")
    print(f"  V2 Native JPG  : {managed_alloc.get('v2_native_jpg_and_toarray_alloc_bytes', 0):,} bytes")
    print(f"  B64 Alloc      : {managed_alloc.get('base64_string_alloc_bytes', 0):,} bytes")
    print(f"  JSON Alloc     : {managed_alloc.get('json_serialization_alloc_bytes', 0):,} bytes")

    # -------------------------------------------------------------------------
    # 9. ENCODER CORPUS BENCHMARK (8 categories x 9 encoders x 10 iterations)
    # -------------------------------------------------------------------------
    print("\n[Stage 9/14] Encoder Corpus Benchmark (8 categories x 9 encoders x 10 iterations)...")
    corpus_categories = [
        "corpus_flat_ui",
        "corpus_text_heavy_ui",
        "corpus_high_frequency",
        "corpus_gradients",
        "corpus_textured_gameplay",
        "corpus_particle_noise",
        "corpus_mixed_gameplay_ui",
        "corpus_real_game_view"
    ]
    encoder_corpus_results = {}
    for cat in corpus_categories:
        print(f"  Benchmarking {cat}...")
        enc_res = call_or_fail("benchmark_encoders", {"corpus_id": cat, "iterations": 10})
        encoder_corpus_results[cat] = enc_res
    results["encoder_corpus"] = encoder_corpus_results

    # Print summary row for text-heavy and gameplay
    for cat_key in ["corpus_text_heavy_ui", "corpus_mixed_gameplay_ui"]:
        cr = encoder_corpus_results[cat_key]
        print(f"  Summary for {cat_key}:")
        print(f"    Native PNG    : {cr['native_png']['median_ms']:.2f} ms, {cr['native_png']['output_bytes']:,} B, PSNR={cr['native_png']['psnr_db']:.1f} dB")
        print(f"    JPG Q75       : {cr['native_jpg_q75']['median_ms']:.2f} ms, {cr['native_jpg_q75']['output_bytes']:,} B, PSNR={cr['native_jpg_q75']['psnr_db']:.1f} dB")
        print(f"    JPG Q85       : {cr['native_jpg_q85']['median_ms']:.2f} ms, {cr['native_jpg_q85']['output_bytes']:,} B, PSNR={cr['native_jpg_q85']['psnr_db']:.1f} dB")
        print(f"    JPG Q95       : {cr['native_jpg_q95']['median_ms']:.2f} ms, {cr['native_jpg_q95']['output_bytes']:,} B, PSNR={cr['native_jpg_q95']['psnr_db']:.1f} dB")

    # -------------------------------------------------------------------------
    # 10. GPU DOWNSCALE MATRIX
    # -------------------------------------------------------------------------
    print("\n[Stage 10/14] GPU Downscale Matrix (Native 4K -> 2560, 2048, 1920, 1600, 1280)...")
    downscale_matrix = call_or_fail("benchmark_downscale_matrix", {"iterations": 10})
    results["downscale_matrix"] = downscale_matrix
    for row in downscale_matrix.get("downscale_targets", []):
        print(f"  Target {row['target_long_edge']}p ({row['dimensions']}): "
              f"blit_cpu={row['blit_submit_cpu_ms']:.4f}ms, readback={row['readback_wait_ms']:.2f}ms, "
              f"jpg_enc={row['encode_ms']:.2f}ms, bytes={row['output_bytes']:,}")

    # -------------------------------------------------------------------------
    # 11. SOURCE FRESHNESS MATRIX (Public vs Reflected across 12 states)
    # -------------------------------------------------------------------------
    print("\n[Stage 11/14] Source Freshness Matrix (Public vs Reflected across 12 states)...")
    conditions = [
        "visible_focused",
        "visible_unfocused",
        "hidden_docked",
        "scene_view_active",
        "resized_before_capture",
        "scale_changed_before_capture",
        "play_visible_focused",
        "play_visible_unfocused",
        "play_hidden_docked",
        "play_paused",
        "play_entering",
        "play_exiting"
    ]
    freshness_matrix = {}
    for backend in ["public", "reflected"]:
        freshness_matrix[backend] = {}
        for cond in conditions:
            print(f"  Testing {backend} in {cond} (30 samples)...")
            res = call_or_fail("benchmark_source_freshness", {
                "backend": backend,
                "condition": cond,
                "iterations": 30
            })
            freshness_matrix[backend][cond] = res
            print(f"    -> success={res['success_rate']*100:.0f}%, "
                  f"stale={res['stale_rate']*100:.0f}%, "
                  f"supported={res['public_edit_mode_supported']}, "
                  f"acq_p50={res['acquisition_ms']['p50']:.3f}ms")
    results["source_freshness_matrix"] = freshness_matrix

    # Private RT lifetime verification
    print("  Testing Private RT Lifetime Isolation...")
    private_rt_life = call_or_fail("benchmark_private_rt_lifetime")
    results["private_rt_lifetime"] = private_rt_life
    print(f"    -> Status: {private_rt_life.get('status')}, "
          f"Intact after mutation: {private_rt_life.get('nexus_copy_intact_after_mutation')}")

    # -------------------------------------------------------------------------
    # 12. LEGACY INSPECTOR / EDITOR WINDOW BASELINE
    # -------------------------------------------------------------------------
    print("\n[Stage 12/14] Legacy Inspector / Window Baseline (50 iterations)...")
    legacy_win = call_or_fail("benchmark_legacy_window", {"iterations": 50})
    results["legacy_window_baseline"] = legacy_win
    print(f"  Window Size    : {legacy_win['window_width']}x{legacy_win['window_height']}")
    print(f"  Main Stall p50 : {legacy_win['total_main_thread_stall_ms']['p50']:.2f} ms")
    print(f"  Main Stall p95 : {legacy_win['total_main_thread_stall_ms']['p95']:.2f} ms")
    print(f"  Managed GC p50 : {legacy_win['managed_allocation_bytes']['p50']:,} bytes")

    # -------------------------------------------------------------------------
    # 13. FINAL END-TO-END PIPELINES (P0 - P4, 100 samples each)
    # -------------------------------------------------------------------------
    print("\n[Stage 13/14] Final End-to-End Candidate Pipelines P0-P4 (100 samples each)...")
    candidate_pipelines = ["P0", "P1", "P2", "P3", "P4"]
    e2e_results = {}

    for pipe in candidate_pipelines:
        print(f"  Benchmarking Pipeline {pipe} (100 samples)...")
        python_roundtrips = []
        unity_metrics = None

        for batch in range(5):  # 5 batches of 20 = 100 samples
            t0 = time.perf_counter()
            u_res = call_or_fail("benchmark_pipeline", {
                "pipeline": pipe,
                "iterations": 20,
                "width": 1920,
                "height": 1080,
                "jpeg_quality": 85
            })
            t1 = time.perf_counter()
            # Approximate per-request python roundtrip in batch
            batch_total_ms = (t1 - t0) * 1000.0
            per_req_ms = batch_total_ms / 20.0
            python_roundtrips.extend([per_req_ms] * 20)
            unity_metrics = u_res  # keep latest batch metrics

        py_stats = compute_python_stats(python_roundtrips)
        e2e_results[pipe] = {
            "pipeline": pipe,
            "python_roundtrip_ms": py_stats,
            "unity_metrics": unity_metrics
        }
        print(f"    Python Roundtrip p50: {py_stats['p50']:.2f} ms (p95={py_stats['p95']:.2f} ms)")
        print(f"    Unity Main Stall p50: {unity_metrics['main_thread_stall_ms']['p50']:.2f} ms")
        print(f"    Payload Bytes       : {unity_metrics['payload_bytes']:,} bytes")

    results["end_to_end_pipelines"] = e2e_results

    # -------------------------------------------------------------------------
    # 14. EXPORT RAW DATA (JSON + CSV)
    # -------------------------------------------------------------------------
    print("\n[Stage 14/14] Exporting Raw Benchmark Artifacts...")
    json_path = os.path.join(PACKAGE_ROOT, "capture-validation-results.json")
    with open(json_path, "w", encoding="utf-8") as f:
        json.dump(results, f, indent=2)
    print(f"Saved: {json_path}")

    # Generate flat CSV summary
    csv_path = os.path.join(PACKAGE_ROOT, "capture-validation-results.csv")
    with open(csv_path, "w", newline="", encoding="utf-8") as f:
        writer = csv.writer(f)
        writer.writerow(["Category", "Candidate", "Metric", "Count", "Min", "p50", "Mean", "p95", "p99", "Max", "StdDev", "CI95_Low", "CI95_High"])

        # Readback R1 vs R2
        for cand, key in [("R1", "r1_wait_ms"), ("R2", "r2_wait_ms")]:
            m = results["readback_r1_vs_r2"][key]
            writer.writerow(["Readback_Wait", cand, "wait_ms", m["count"], m["min"], m["p50"], m["mean"], m["p95"], m["p99"], m["max"], m["std_dev"], m["ci_95_low"], m["ci_95_high"]])

        # End-to-End Python Roundtrip
        for pipe in candidate_pipelines:
            m = e2e_results[pipe]["python_roundtrip_ms"]
            writer.writerow(["EndToEnd_Roundtrip", pipe, "python_roundtrip_ms", m["count"], m["min"], m["p50"], m["mean"], m["p95"], m["p99"], m["max"], m["std_dev"], m["ci_95_low"], m["ci_95_high"]])

        # End-to-End Unity Stall
        for pipe in candidate_pipelines:
            m = e2e_results[pipe]["unity_metrics"]["main_thread_stall_ms"]
            writer.writerow(["EndToEnd_Stall", pipe, "main_thread_stall_ms", m["count"], m["min"], m["p50"], m["mean"], m["p95"], m["p99"], m["max"], m["std_dev"], 0.0, 0.0])

        # Encoders for text-heavy and mixed
        for cat in ["corpus_text_heavy_ui", "corpus_mixed_gameplay_ui"]:
            c_res = encoder_corpus_results[cat]
            for enc_name in ["baseline_png", "native_png", "native_jpg_q60", "native_jpg_q70", "native_jpg_q75", "native_jpg_q80", "native_jpg_q85", "native_jpg_q90", "native_jpg_q95"]:
                e = c_res[enc_name]
                writer.writerow([f"Encoder_{cat}", enc_name, "duration_ms", 10, e["min_ms"], e["median_ms"], e["mean_ms"], e["p95_ms"], e["p95_ms"], e["max_ms"], e["stddev_ms"], 0.0, 0.0])

    print(f"Saved: {csv_path}")

    # Generate .meta files for the raw data artifacts so Unity quality gates pass
    for artifact_file in [json_path, csv_path]:
        meta_file = artifact_file + ".meta"
        if not os.path.exists(meta_file):
            import uuid
            guid_hex = uuid.uuid4().hex
            with open(meta_file, "w", encoding="utf-8") as mf:
                mf.write(f"fileFormatVersion: 2\nguid: {guid_hex}\n")

    print("\n" + "=" * 80)
    print(" BENCHMARK SUITE SUCCESSFULLY COMPLETED")
    print("=" * 80)


if __name__ == "__main__":
    main()
