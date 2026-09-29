# Nexus Unity Benchmarks

Current development line: `1.7.0`  
Latest released version: `1.6.0`  
Evidence date: 2026-09-22

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

No numeric baseline has been recorded yet.

| Date | Scenario | Format | Resolution | Samples | p50 | p95 | CPU | RAM | Baseline |
|---|---|---|---:|---:|---:|---:|---:|---:|---|
| TBD | Game View capture | PNG | 1600×900 | TBD | TBD | TBD | TBD | TBD | TBD |
| TBD | Game View capture | JPEG | 1600×900 | TBD | TBD | TBD | TBD | TBD | TBD |
| TBD | Domain reload | Pending-GPU capture state | N/A | TBD | TBD | TBD | TBD | Previous capture implementation |

## What the current evidence supports

The current data supports these engineering claims:

- capture work no longer uses synchronous GPU waits on the Unity main thread;
- capture and domain reload paths are stable across the validated stress cases;
- two Unity editors can run concurrently without Legacy port contention;
- the package remains compilable with and without the optional Pipeline package.

The current data does **not** support claims of lower CPU use, lower memory use, higher capture throughput, or a percentage speed improvement.

## Future benchmark entry format

Add one row per scenario and keep the raw run output alongside the script or artifact that produced it. Recommended first numeric benchmark: 30 warm Game View captures for PNG and JPEG at 1600×900, reporting p50/p95 latency, peak RSS, and CPU time against the previous implementation.
