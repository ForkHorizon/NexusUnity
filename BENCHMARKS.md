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

### Asset impact (`nexus.asset_impact`, V3-13)

2026-11-20, macOS (Apple Silicon), Unity 6000.3.16f1 batchmode scratch project (no harness Editor involved), `rework3/V3-12` (`1a53493`), `com.unity.pipeline` 1.0.0. A seeded generator writes 40% base prefabs (Health + Spawner wired by a UnityEvent), 25% Prefab Variants, 10% nested prefabs, 25% ScriptableObjects and one scene that is left unloaded. In-process `ExecuteEnveloped` timing, one sample per cell (single run, no p95). "Cold" is the first call after generation; the OS file cache is already warm. "1-script change" edits `Health.cs` on disk without an import. Layer A has no cache, so that call is a full rescan. `max_results` is the default 500. Allocation and peak RSS were not measured: Mono returns 0 for per-thread allocation, and the process peak (~0.9–1.3 GB) is the Unity Editor itself. Raw output and generator: `Research~/evidence/V3-13/`.

| Assets | Query | Static cold | Static warm | After 1-script change | `level=editor` cold (Layer B part) | `level=editor` warm (Layer B part) | Response bytes (static / editor) | Complete |
|---:|---|---:|---:|---:|---:|---:|---:|---|
| 20 | `find_references` field | 22 ms | 5 ms | 5 ms | 12 ms (6) | 6 ms (0) | 8,228 / 10,920 | yes |
| 300 | `find_references` field | 75 ms | 58 ms | 62 ms | 89 ms (25) | 66 ms (4) | 83,110 / 120,983 | yes |
| 2,000 | `find_references` field | 461 ms | 412 ms | 411 ms | 594 ms (157) | 458 ms (41) | 178,467 / 263,278 | no (`max_results`) |
| 2,000 | `validate_asset_integrity` | — | 392 ms | — | — | 454 ms (28) | 352 / 574 | yes |

What these rows show: Layer A scales roughly linearly, about 0.2 ms per asset. Layer B adds about 0.08 ms per asset cold and 0.02 ms warm. At 2,000 assets the field query returns 178–263 KB even after the 500-entry cap. That is about 45–65k tokens if an agent reads it whole, so `unique_values`/`summary` (or a lower `max_results`) is the shape to steer agents to before promoting `#213` into skills.

Output shape after the V3-13 fix (`results: "auto"`, same generator and machine, warm): `find_references` field 83,290 → 35,894 B at 300 assets and 178,614 → 73,033 B at 2,000; `analyze_change_impact rename_field` 106,640 → 28,130 B and 226,936 → 55,004 B; `level=editor` field 120,983 → 52,898 B at 300. Every asset is still listed in `<list>_by_asset`; `results: "full"` returns the old shape. Raw: `Research~/evidence/V3-13/shape/`.

A/B agent efficacy (V3-13, 2026-11-20). 300-asset project, 8 rename/delete tasks per arm, blind batchmode oracle over 1,249 stored values and 235 UnityEvent calls:

| Arm | Pass | Regressions | Tool adoption | Avg tokens | Avg wall time |
|---|---:|---:|---:|---:|---:|
| A: Unity CLI + `unity vcs` + grep | 8/8 | 0 | 1/8 found it unhinted | 75,490 | 74 s |
| B: A + `nexus asset_impact` hint | 8/8 | 0 | 8/8 | 77,283 | 69 s |

Rerun with a smaller model (Haiku agents, same tasks and grader, current build with `results: "auto"`):

| Arm | Pass | Regressions | Tool adoption | Avg tokens | Avg wall time |
|---|---:|---:|---:|---:|---:|
| A: Unity CLI + `unity vcs` + grep | 8/8 | 0 | 0/8 | 82,382 | 61 s |
| B: A + `nexus asset_impact` hint | 8/8 | 0 | 8/8 (50 calls) | 88,287 | 72 s |

Agents adopt the tool when hinted, but in both runs it prevented no regressions: strong agents grepping Force Text YAML with unique member names already found every stored value. The next step is the output shape: a compact default and verification with `level=editor` after `[FormerlySerializedAs]`. More features should wait. Details: `Research~/evidence/V3-13/impact_benchmark.md`.

## What the current evidence supports

The current data supports these engineering claims:

- capture work no longer uses synchronous GPU waits on the Unity main thread;
- capture and domain reload paths are stable across the validated stress cases;
- two Unity editors can run concurrently without Legacy port contention;
- the package remains compilable with and without the optional Pipeline package.

The current data does **not** support claims of lower CPU use, lower memory use, higher capture throughput, or a percentage speed improvement.

## Future benchmark entry format

Add one row per scenario and keep the raw run output alongside the script or artifact that produced it. Recommended first numeric benchmark: 30 warm Game View captures for PNG and JPEG at 1600×900, reporting p50/p95 latency, peak RSS, and CPU time against the previous implementation.
