# Nexus Unity Benchmarks

Measured on the line released as `1.6.2`  
Evidence date: 2026-11-19 (validation table: 2026-09-22, `1.6.1`)

This is the public benchmark ledger for Nexus Unity. It deliberately separates measured performance numbers from repeatable validation evidence. No latency, CPU, memory, or throughput improvement is claimed until a before/after measurement exists.

## Measurement rules

Every numeric benchmark should record:

- Unity version, package version, platform, branch, and commit;
- capture source, format, resolution, and warm/cold state;
- sample count, p50, p95, and outliers;
- CPU, memory, and allocation measurements when relevant;
- the baseline and comparison implementation.

## Current validation evidence

These are confirmed results, but they are not latency or resource benchmarks.

| Area | Configuration | Result |
|---|---|---|
| Unity EditMode suite | Parent interactive harness | 152 discovered, 152 passed, 0 failed, 0 skipped/inconclusive |
| Unity EditMode suite | Clean checkout batch mode | 152 discovered, 151 passed, 0 failed, 1 inconclusive because visible Game View is unavailable in batch mode |
| Python bridge | Static package validation | 43 tests passed |
| Capture blocking audit | Production Capture/Pipeline path | 0 `RequestIntoNativeArray`, `WaitForCompletion`, `Task.Result`, or `GetAwaiter().GetResult` patterns |
| Domain reload stability | Pending-GPU reload validation | 20/20 passed, 0 hangs |
| Multi-editor isolation | Two editors, Pipeline ports 7800 and 7801 | Passed; no Legacy HTTP 8081 bind fight and routing was correct |
| Clean compilation | Pipeline installed | Production and Pipeline assemblies compiled successfully |
| Clean compilation | Pipeline absent | Production assembly compiled with 0 C# errors; Pipeline assembly absent as expected |
| Capture smoke | Pipeline, PNG/JPEG, 1600×900 | Passed |
| API schema snapshot | Clean checkout | 120 visible tools, 3 hidden aliases, 29,160 UTF-8 bytes for the tools array |

## Numeric performance benchmarks

First numeric baseline: 2026-11-19, macOS (Apple Silicon), Unity 6000.3.16f1 harness Editor (not focused), Nexus `1.6.2` (`development` b1aeb1e), `com.unity.pipeline` 1.0.0, Unity CLI 1.0.1. Each arm: 5 warm-up + 30 measured Game View captures (`source=screen`), sequential, 0 failures. Latency is the full `unity command` round trip, CLI process start included (a no-op `editor status` costs about 1.1 s), so compare arms with each other, not with in-Editor timings. CPU and RAM were not measured. Raw output: `Research~/evidence/V3-04/bench_capture.out`.

| Date | Scenario | Format | Resolution | Samples | p50 | p95 | Payload p50 | CPU | RAM | Baseline |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|---|
| 2026-11-19 | Nexus `nexus capture_game_view` | PNG | 1600×900 | 30 | 1495 ms | 1506 ms | 272,255 B | — | — | Pipeline PNG row |
| 2026-11-19 | Nexus `nexus capture_game_view` | JPEG q85 | 1600×900 | 30 | 1399 ms | 1410 ms | 41,944 B | — | — | Pipeline PNG row |
| 2026-11-19 | Nexus `nexus capture_game_view` (default) | JPEG q85 | 768×407 | 30 | 1400 ms | 1409 ms | 12,188 B | — | — | Pipeline PNG 768 row |
| 2026-11-19 | Pipeline `capture game_view` | PNG | 1600×900 | 30 | 1190 ms | 1211 ms | 242,953 B | — | — | — |
| 2026-11-19 | Pipeline `capture game_view --max-resolution 768` | PNG | 768×432 | 30 | 1102 ms | 1196 ms | 96,374 B | — | — | — |
| TBD | Domain reload | Pending-GPU capture state | N/A | TBD | TBD | TBD | — | TBD | TBD | Previous capture implementation |

What these rows show: Nexus JPEG at 768 returns about 8× fewer bytes than Pipeline's 768 PNG, and about 20× fewer than a 1600×900 PNG. A Nexus capture takes about 210 ms longer per call than Pipeline's. The Nexus timing fields show the extra time: the capture spans two Editor ticks (`wait_ms` ≈ 300, `base64_ms` ≈ 317 while the Editor was not focused), while Pipeline captures in one tick. Fixing this is tracked for V3-22.

## What the current evidence supports

The current data supports these engineering claims:

- capture work no longer uses synchronous GPU waits on the Unity main thread;
- capture and domain reload paths are stable across the validated stress cases;
- two Unity editors can run concurrently without Legacy port contention;
- the package remains compilable with and without the optional Pipeline package.

The current data does **not** support claims of lower CPU use, lower memory use, higher capture throughput, or a percentage speed improvement.

## Future benchmark entry format

Add one row per scenario and keep the raw run output alongside the script or artifact that produced it. Recommended first numeric benchmark: 30 warm Game View captures for PNG and JPEG at 1600×900, reporting p50/p95 latency, peak RSS, and CPU time against the previous implementation.
