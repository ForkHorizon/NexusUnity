#!/usr/bin/env python3
"""Pipeline-only Capture V2 smoke: 20 warmup + 100 measured unity command captures.

Does not call Nexus HTTP. Requires a running Editor with Pipeline on 7800+.
"""

from __future__ import annotations

import json
import statistics
import sys
import time
from pathlib import Path

from _unity_cli import nexus

WARMUPS = 20
MEASURED = 100
BLOCKING_STALL_MS = (
    40  # median main-thread stall above this counts as a blocking regression
)
NORMALIZE_SIZE = (1600, 900)


def variant(*flags: str) -> dict:
    t0 = time.perf_counter()
    data = nexus("capture_game_view", *flags)
    data["_roundtrip_ms"] = (time.perf_counter() - t0) * 1000.0
    return data


def measure() -> tuple[list[dict], int]:
    """Runs warmup + measured default captures; returns measured samples and failed captures."""
    samples = []
    errors = 0
    for i in range(WARMUPS + MEASURED):
        t0 = time.perf_counter()
        data = nexus("capture_game_view")
        rt = (time.perf_counter() - t0) * 1000.0
        ok = (
            bool(data.get("success"))
            and data.get("encoding") == "jpg"
            and data.get("width", 0) > 0
        )
        if not ok:
            errors += 1
        if i >= WARMUPS:
            samples.append(
                {
                    "ok": ok,
                    "roundtrip_ms": rt,
                    "acquisition_ms": data.get("acquisition_ms"),
                    "submit_ms": data.get("submit_ms"),
                    "wait_ms": data.get("wait_ms"),
                    "encode_ms": data.get("encode_ms"),
                    "main_thread_stall_ms": data.get("main_thread_stall_ms"),
                    "total_ms": data.get("total_ms"),
                    "bytes": data.get("bytes"),
                }
            )
    return samples, errors


def main() -> int:
    samples, errors = measure()

    # Encoding is --encoding: Pipeline reserves --format for its own output format.
    png = variant("--encoding", "png")
    q50 = variant("--encoding", "jpg", "--quality", "50")
    norm = variant(
        "--width",
        str(NORMALIZE_SIZE[0]),
        "--height",
        str(NORMALIZE_SIZE[1]),
        "--encoding",
        "jpg",
        "--max-dimension",
        "0",
    )
    bad = variant("--encoding", "bmp")

    stalls = [
        s["main_thread_stall_ms"]
        for s in samples
        if isinstance(s["main_thread_stall_ms"], (int, float))
    ]
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
        "blocking_regression": (statistics.median(stalls) if stalls else 0)
        > BLOCKING_STALL_MS,
        "png": {
            "ok": png.get("encoding") == "png" and png.get("success"),
            "width": png.get("width"),
            "height": png.get("height"),
        },
        "quality50": {"ok": q50.get("success"), "bytes": q50.get("bytes")},
        "normalize_1600x900": {
            "ok": (norm.get("width"), norm.get("height")) == NORMALIZE_SIZE,
            "width": norm.get("width"),
            "height": norm.get("height"),
        },
        "unsupported_format_errors": not bool(bad.get("success")),
    }
    print(json.dumps(report, indent=2))
    Path("stabilize-pipeline-capture-results.json").write_text(
        json.dumps(report, indent=2)
    )
    return (
        0
        if errors == 0 and not report["blocking_regression"] and report["png"]["ok"]
        else 1
    )


if __name__ == "__main__":
    sys.exit(main())
