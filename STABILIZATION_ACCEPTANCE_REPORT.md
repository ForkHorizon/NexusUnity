# Nexus Unity Dual → Hybrid Stabilization / Acceptance Report

**Date:** 2026-09-22  
**Branch:** `rework/T01`  
**Package:** `com.forkhorizon.nexus.unity` 1.5.0 Unreleased  
**Editor A:** Unity 6000.4.3f1, PID 98978, UnityTestForNexus, Pipeline 7800, Legacy 8081  
**Editor B:** Unity 6000.4.3f1, PID 14884, NexusUnityDemo, Pipeline 7801  
**Related:** `Research~/docs/ARCHITECTURE_MIGRATION_HANDOFF_HISTORICAL_2026-09-22.md`

> Final cleanup pass addendum (2026-09-22): the canonical `Tests/Editor` suite now supersedes the earlier 37-test M4 subset. The current parent-harness EditMode result is 152/152 passed, 0 failed, 0 skipped. A clean package checkout imports and compiles both production and Pipeline assemblies; its batch-mode Test Runner discovers 152 tests and reports 151 passed, 0 failed, 1 inconclusive because no visible editor window exists for the Game View capture test. The earlier acceptance figures remain historical evidence for the transport/capture work.

This is the full engineering report after closing the remaining M4 acceptance blockers. It is not a redesign and not a public sunset.

---

# A. Executive verdict

```text
M1: IMPLEMENTED / ACCEPTED
M2: IMPLEMENTED / ACCEPTED
M3: IMPLEMENTED / ACCEPTED
M4: IMPLEMENTED / ACCEPTED
M5 ledger: IMPLEMENTED
Legacy removal: BLOCKED
Release readiness: READY FOR INTERNAL MERGE ONLY
```

```text
legacy.deprecated = false
legacy.still_supported = true
removal = not_scheduled
```

---

# B. Unity Test Runner

Assembly `UnityMCP.Editor.Tests` is now imported from `Tests/Editor` (Unity-visible). Consumers enable it through their own `Packages/manifest.json` `testables`. The asmdef uses `overrideReferences` + `Newtonsoft.Json.dll` / `nunit.framework.dll` (standard Unity test-assembly setup; no copied DLLs, no absolute paths).

A first run hung because `MissingGameViewFailsWithDomainError` called `.GetResult()` on the Unity main thread while Game View could still exist. That test is now a `[UnityTest]` coroutine. The Editor was restarted to recover the deadlock.

```text
Tests discovered: 152
Executed: 152
Passed: 152
Failed: 0
Ignored: 0
Skipped: 0
Inconclusive: 0
Duration: ~20 s (async Test Runner submission and poll)
Filter: assembly UnityMCP.Editor.Tests, EditMode
```

Classes executed:

- `CaptureGatewayTests`
- `NexusCommandRegistryTests`
- `NexusRuntimeSelectorTests`
- `OpenSourceApiContractTests`

The earlier 37-test `Tests~/Editor` subset was UPM-hidden and was not part of that historical run; the current canonical suite is `Tests/Editor`.

---

# C. Multi-editor

Two Editors open simultaneously.

| | Project A | Project B |
| :--- | :--- | :--- |
| Path | `/Users/daliys/Daliys/UnityProjects/UnityTestForNexus` | `/Users/daliys/Daliys/UnityProjects/NexusUnityDemo` |
| PID | 98978 | 14884 |
| Pipeline port | 7800 | 7801 |
| Session file | A `Library/Pipeline/.unity-pipeline-port` pid 98978 | B file pid 14884 port 7801 |
| requested / effective | auto / pipeline | auto / pipeline |
| Pipeline health | healthy | healthy |
| Legacy HTTP | **owns 8081**, Running | **not bound** |

Targeting:

- `unity command --project-path A eval MARKER_A` → `MARKER_A`
- `unity command --project-path B eval MARKER_B` → `MARKER_B`
- `nexus_project_map` A path vs B path; no cross-project leakage.

### Case A — foreign 8081

B status:

```text
legacy_http_bound = false
legacy_unavailable_reason = foreign_project
effective = pipeline
pipeline.port = 7801 healthy
MCPServer.State = Stopped
```

B did not fight for 8081. Pipeline remained usable.

### Case B — same-project reload (A owns 8081)

After `RequestScriptReload` on A: 8081 returned Running, pid 98978, path A, `legacy_unavailable_reason = null`, next `nexus.project_map` succeeded. Own listener was not classified as foreign.

### Case C — owner releases 8081

A `shutdown_server` closed 8081. B domain reload: `legacy_unavailable_reason` cleared to null, **B did not auto-bind 8081** (`State=Stopped`, `legacy_http_bound=false`). Pipeline stayed eligible.

Recovery is **not** opportunistic steal. After the owner releases 8081, B needs an explicit Nexus server start (or equivalent user action). That is the intended predictable behavior.

---

# D. Pending readback reload

Harness: `NexusCaptureReloadProbe.SubmitPendingThenReload()` submits Capture V2, logs `request.done` **before** reload, then `EditorApplication.delayCall` → `RequestScriptReload`. JSONL: `Library/NexusPendingReload.jsonl`.

```text
20 cycles
submitted && done_before_reload == false : 20 / 20
reload_completed : 20 / 20
next_capture_success : 20 / 20
hangs : 0
```

The probe returns to the caller **before** reload (so the pending flag is recorded). The in-flight Task is destroyed with the domain; `FailPendingForReload` sets `DomainReloadInterrupted` for waiters still in-process. External transports that already returned the probe JSON will not later receive that exception. Next capture after recovery succeeded every cycle. Teardown does not `WaitForCompletion`.

---

# E. Persistent MCP capture latency

Classification of the old ~299 ms submit-to-done: **C — Game View / Editor tick cadence, independent of Pipeline.**

Same Editor session, Game View visible, Overlay markers present:

| Path | submit→done p50/p95 | ticks p50/p95 | stall p50/p95 | total internal p50 | MCP roundtrip p50 |
| :--- | ---: | ---: | ---: | ---: | ---: |
| Persistent Pipeline `/api/exec` (20 warmup + 50) | **296.9 / 299.4 ms** | **3 / 3** | 10.3 / 23.0 ms | 615.6 ms | **400 ms** |
| Persistent Legacy HTTP control (20) | **298.6 / 299.5 ms** | **3 / 3** | 17.9 / 22.1 ms | 712.9 ms | **400 ms** |
| Cold `unity command` (previous pass) | ~299 ms | n/a | 7.25 ms | — | **1218 ms** |

Evidence:

- Persistent Pipeline and persistent HTTP share the same ~297 ms wait and **exactly 3** `EditorApplication.update` ticks.
- Cold CLI roundtrip was ~1.2 s; persistent roundtrip is ~400 ms. The extra ~800 ms was process spawn (**A** for roundtrip only, not for wait_ms).
- Stall stays ~10–18 ms; GPU wait is not counted as stall.
- 3 ticks × ~100 ms ≈ 300 ms: Editor update cadence, not a Pipeline-only delay and not an instrumentation bug (ticks and wait agree on both transports).

M4 does not require Pipeline to match historical in-engine 6–8 ms. Persistent MCP does not add a *Pipeline-specific* 300 ms tax. The 300 ms wait is Editor tick observation of `request.done`.

---

# F. Visual correctness

Historical acceptance fixture (the unused production helper was removed in the final cleanup) `NexusOverlayVerifyRoot`:

- Top-left 48×48 **red** (`RED_A`)
- Bottom-right 48×48 **blue** (`BLUE_B`)
- Center green text `OVERLAY_TEST_123`

HTTP PNG 1010×978 decoded and scanned:

| Marker | Pixel blob | Image location |
| :--- | :--- | :--- |
| Red | 2304 px, centroid (47.5, 47.5) | top-left |
| Blue | 2304 px, centroid (961.5, 929.5) | bottom-right |
| Green | 4503 px, centroid ~ (494, 489) | center |

```text
orientation_ok = true
vertically_flipped = false
black_frame = false
Overlay present = true
```

If the blit y-flip were wrong, red would sit at the bottom of the PNG. Pipeline JPEG/PNG used the same gateway at 1010×978; HTTP PNG is the pixel-proofed file.

---

# G. Runtime modes

| Mode | Result |
| :--- | :--- |
| `pipeline` (eval on main) | `requested=pipeline`, `effective=pipeline`, `legacy_fallback=false` |
| Pipeline-only commands via `/api/exec` | project_map, group_compile_errors, JPEG, PNG, 1600×900 all success; project path A |
| Hide `.unity-pipeline-port` then probe | `eligible=false`, `effective=pipeline` still (no HTTP fallback) |
| Restore file + `auto` | `effective=pipeline`, `eligible=true` |
| `legacy` (eval on main) | `effective=legacy`; HTTP map/PNG/JPEG/errors succeed; `deprecated=false`, `still_supported=true`, `removal=not_scheduled` |

Re-probe is **not periodic**. Health is captured on Editor init / `BeginHealthProbe` (domain reload, explicit probe). Last-healthy SessionState seeds Auto across reload so the mode does not flap during `health_unknown`.

Note: runtime snapshots are published on Legacy listener transitions and overlay the live binding state, so the fast-path response now agrees with the top-level Legacy binding status. Main-thread `ToStatusJson()` remains authoritative for Unity-owned preference changes.

---

# H. Static / clean-clone

```text
scripts/prepush-validate.sh --static-only
Python tests: 43 ran, 43 passed
Quality gate errors: 0
.meta pairing: pass
```

Production compile does not call CaptureSpikes / CaptureValidation / T1T2. Research C# lives under `Research~/`. Generated JSON/captures remain gitignored.

---

# I. Changes made (acceptance and final cleanup)

- Visible EditMode assembly: `Tests/Editor` (guarded by `UNITY_INCLUDE_TESTS`); asmdef `Newtonsoft.Json.dll` + `nunit.framework.dll`.
- `MissingGameViewFailsWithDomainError` converted to `[UnityTest]` (no main-thread `.GetResult()`).
- `OpenSourceApiContractTests` locates `nexus_bridge/*.py` on disk; API_REFERENCE lists canonical ids in the raw `unity_` contract form.
- `NexusCaptureReloadProbe`, overlay marker evidence, and `editor_ticks_submit_to_done` telemetry.
- Removed the unused `NexusOverlayVerify` helper and its orphan `UnityEngine.UI` asmdef reference; the current package has no undeclared UGUI dependency.
- Quality-gate / linter ignore `Tests/`.

---

# J. Remaining technical debt

Non-blocking for M4; blocking for a public RC:

1. Unity CLI 1.0.0-beta.10 and `com.unity.pipeline` 0.7.0-exp.1 — M5 removal gates fail.
2. Case C does not auto-bind 8081 (by design).
3. Listener-thread `get_server_status` vs EditorPrefs `requested` mode.
4. The former `Tests~/Editor` duplicate was removed; `Tests/Editor` is now the single canonical EditMode suite.
5. Editor update cadence (~3 ticks / ~300 ms wait) vs historical in-engine 6–8 ms if Game View/tick rate is improved later.
6. Do not start a new architectural migration.

---

# K. Final decision

```text
READY FOR INTERNAL MERGE ONLY
```

M4 is **ACCEPTED**. Do not call this a release candidate: Pipeline/CLI are still experimental/beta, Legacy is still fully supported, and removal is not scheduled.

Do not start another migration or optimization phase.
