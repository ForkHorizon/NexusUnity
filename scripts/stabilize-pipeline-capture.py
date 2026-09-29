#!/usr/bin/env python3
"""Pipeline-only Capture V2 smoke: 20 warmup + 100 measured unity command captures.

Does not call Nexus HTTP. Requires a running Editor with Pipeline on 7800+.
"""
from __future__ import annotations

import json
import os
import statistics
import subprocess
import sys
import time
from pathlib import Path

PROJ = Path(__file__).resolve().parents[3]  # Unity project root from Assets/NexusUnity/scripts
if not (PROJ / "Assets").exists():
    sys.exit("Run from Assets/NexusUnity/scripts inside a Unity project (Assets/ not found at " + str(PROJ) + ").")

WARMUPS = 20
MEASURED = 100


def run_cmd(args: list[str], timeout: int = 60) -> dict:
    env = os.environ.copy()
    env["UNITY_NO_BANNER"] = "1"
    env["UNITY_NON_INTERACTIVE"] = "1"
    proc = subprocess.run(
        ["unity", "command", "--project-path", str(PROJ), "--json", "--result-only", "--timeout", str(timeout), *args],
        capture_output=True,
        text=True,
        env=env,
        timeout=timeout + 10,
        check=False,
    )
    raw = (proc.stdout or proc.stderr or "").strip()
    try:
        return json.loads(raw)
    except json.JSONDecodeError:
        return {"success": False, "parse_error": raw[:500], "returncode": proc.returncode}


def variant(args: list[str]) -> dict:
    t0 = time.perf_counter()
    data = run_cmd(args)
    data["_roundtrip_ms"] = (time.perf_counter() - t0) * 1000.0
    return data


def main() -> int:
    samples = []
    errors = 0
    for i in range(WARMUPS + MEASURED):
        t0 = time.perf_counter()
        data = run_cmd(["nexus_capture_game_view"])
        rt = (time.perf_counter() - t0) * 1000.0
        ok = bool(data.get("success")) and data.get("encoding") == "jpg" and data.get("width", 0) > 0
        if not ok:
            errors += 1
        if i >= WARMUPS:
            samples.append({
                "ok": ok,
                "roundtrip_ms": rt,
                "acquisition_ms": data.get("acquisition_ms"),
                "submit_ms": data.get("submit_ms"),
                "wait_ms": data.get("wait_ms"),
                "encode_ms": data.get("encode_ms"),
                "main_thread_stall_ms": data.get("main_thread_stall_ms"),
                "total_ms": data.get("total_ms"),
                "bytes": data.get("bytes"),
            })

    # CLI args are positional (width, height, format, quality, max_dimension).
    # Do not pass format=png: global --format collides, and named args bind the next flag.
    png = variant(["nexus_capture_game_view", "0", "0", "png"])
    q50 = variant(["nexus_capture_game_view", "0", "0", "jpg", "50"])
    norm = variant(["nexus_capture_game_view", "1600", "900", "jpg"])
    bad = variant(["nexus_capture_game_view", "0", "0", "bmp"])

    stalls = [s["main_thread_stall_ms"] for s in samples if isinstance(s["main_thread_stall_ms"], (int, float))]
    waits = [s["wait_ms"] for s in samples if isinstance(s["wait_ms"], (int, float))]
    rounds = [s["roundtrip_ms"] for s in samples]
    report = {
        "transport": "pipeline",
        "http_used": False,
        "warmups": WARMUPS,
        "measured": MEASURED,
        "errors": errors,
        "success": MEASURED - errors,
        "roundtrip_p50_ms": statistics.median(rounds) if rounds else None,
        "stall_p50_ms": statistics.median(stalls) if stalls else None,
        "wait_p50_ms": statistics.median(waits) if waits else None,
        "blocking_regression": (statistics.median(stalls) if stalls else 0) > 40,
        "png": {"ok": png.get("encoding") == "png" and png.get("success"), "width": png.get("width"), "height": png.get("height")},
        "quality50": {"ok": q50.get("success"), "bytes": q50.get("bytes")},
        "normalize_1600x900": {"ok": norm.get("width") == 1600 and norm.get("height") == 900, "width": norm.get("width"), "height": norm.get("height")},
        "unsupported_format_errors": not bool(bad.get("success")),
    }
    print(json.dumps(report, indent=2))
    Path("stabilize-pipeline-capture-results.json").write_text(json.dumps(report, indent=2))
    return 0 if errors == 0 and not report["blocking_regression"] and report["png"]["ok"] else 1


if __name__ == "__main__":
    sys.exit(main())
