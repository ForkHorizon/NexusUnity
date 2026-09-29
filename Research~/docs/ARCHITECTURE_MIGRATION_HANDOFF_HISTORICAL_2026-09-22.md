# Nexus Unity Stabilization Handoff

**Date:** 2026-09-22  
**Branch:** `rework/T01`  
**Package:** `com.forkhorizon.nexus.unity` 1.5.0 Unreleased  
**Editor:** Unity 6000.4.3f1 Metal / URP 17.5.0 / Apple M5  
**Full A–J report:** `STABILIZATION_ACCEPTANCE_REPORT.md`  
**This is not a redesign and not a public-release document.**

> Historical handoff snapshot. Superseded by the final cleanup pass and the current acceptance report above; its earlier test-import, file paths, and multi-editor gaps are retained only as implementation history.

Architecture remains:

```text
Nexus high-level commands / Capture
        ↓
shared domain/application logic
        ↓
Legacy HTTP adapter
        OR
Unity Pipeline / unity mcp adapter
```

Capture V2 remains Nexus-owned **DriverOwnedReadback**. Legacy HTTP is still fully supported.

---

# A. Executive status

```text
M1: IMPLEMENTED / ACCEPTED
M2: IMPLEMENTED / ACCEPTED
M3: IMPLEMENTED / ACCEPTED
M4: IMPLEMENTED / ACCEPTED
M5 ledger: IMPLEMENTED
Legacy removal: BLOCKED
Release readiness: READY FOR INTERNAL MERGE ONLY
```

Full evidence: `STABILIZATION_ACCEPTANCE_REPORT.md`. Unity Test Runner **37/37**. Two Editors live (A 7800/8081, B 7801/foreign 8081). Pending-GPU reload **20/20**. Persistent MCP wait ~297 ms = **3 Editor ticks** (same as HTTP; not a Pipeline-only delay). Overlay red top-left / blue bottom-right.

M1 is accepted because Game View production capture now goes through `ICaptureGateway` / DriverOwnedReadback on both HTTP and Pipeline, with the Legacy PNG screenshot schema unchanged.

M2 is accepted because `nexus.project_map`, `nexus.group_compile_errors`, and `nexus.capture_game_view` share one handler each; live HTTP and Pipeline both reached those handlers.

M3 is accepted because `legacy` / `pipeline` / `auto` selection, PID-aware Pipeline health, and explicit-pipeline-no-HTTP-fallback are implemented and live. Forcing `legacy` switched effective runtime immediately.

M4 stays **IMPLEMENTED, PENDING ACCEPTANCE**. Pipeline-only Capture V2 works without HTTP and does not sync-wait GPU on the Unity main thread, and 20 domain-reload recoveries did not hang. These gates are still open:

- Unity Test Runner did **not** execute `Tests~/Editor` in this Assets harness (0 tests discovered until a working test assembly import exists).
- Two Unity Editor projects were **not** opened simultaneously in this pass.
- Overlay was not independently OCR/vision-checked in this pass (y-flip blit remains in production).
- GPU submit-to-done on this machine via `unity command` was ~299 ms p50, versus the earlier in-engine ~6–8 ms; stall stayed ~7 ms and did not absorb the GPU wait.

M5 is a sunset **ledger**, not deprecation:

```text
legacy.deprecated = false
legacy.still_supported = true
removal = not_scheduled
```

Do not call sunset announcement “Legacy deprecated.”

---

# B. Pipeline capture async fix

## Old blocking path

`[CliCommand] CaptureGameView` invoked `CaptureGameViewCommand.ExecuteAsync(...).GetAwaiter().GetResult()` on the Unity main thread. `CaptureGateway` treated main-thread callers as inline/complete, which could `WaitForCompletion` / block while `AsyncGPUReadback` still needed future `EditorApplication.update` ticks. That deadlocks or freezes the Editor.

## Thread ownership (current Unity Pipeline 0.7.0-exp.1)

| Step | Thread |
| :--- | :--- |
| Persistent `unity mcp` / `unity command` request | Pipeline worker |
| `[CliCommand] MainThreadRequired=true` method body | Unity main, via `Dispatcher.Invoke` |
| GameView RT copy, Blit, `AsyncGPUReadback.Request` | Unity main |
| Returned `Task<NexusCaptureResult>` | Incomplete after submit |
| `UnwrapResult` awaits that Task | Pipeline **background** thread (`ConfigureAwait(false)`) |
| `EditorApplication.update` poll until `request.done` | Unity main, Editor loop free |
| `GetData<byte>()` + `ImageConversion` encode | Unity main, on the poll tick |
| TCS with `RunContinuationsAsynchronously` | Continuations not inlined on the poll tick |
| Base64 (`ConfigureAwait(false)`) + JSON/stdio | Off main (Pipeline unwrap / worker) |

`MainThreadRequired` can be set false, but Game View / Graphics APIs must stay on main. The safe model is: main submits GPU work and returns a Task; Pipeline unwraps off-main.

HTTP `ProcessJsonRpc` on main for async methods now returns error `-32000` instead of `GetResult()`. HTTP capture uses `ProcessJsonRpcAsync`.

## Proof no synchronous GPU wait remains

Production `DriverOwnedReadback` has no `WaitForCompletion` / `RequestIntoNativeArray`. Pipeline wrapper has no `.GetResult()` / `.Result`.

Live Pipeline-only 20 warmup + 100 measured JPEG (`scripts/stabilize-pipeline-capture.py`, `unity command`, no HTTP):

| Metric | Value |
| :--- | :--- |
| Success | 100 / 100 |
| `main_thread_stall_ms` p50 | **7.25 ms** |
| `wait_ms` (submit→done) p50 | **298.9 ms** |
| Blocking regression (`stall p50 > 40 ms`) | **false** |
| PNG | ok, 792×421 |
| JPEG quality=50 | ok, 10811 bytes (Q85 was ~15935) |
| 1600×900 | ok |
| `format=bmp` | errors as required |

Stall does **not** include the GPU in-flight interval. A 50 ms-class main-thread freeze of the GPU wait did not occur. Do not treat 7.25 ms as a claim of the historical 1.59 ms in-engine stall; this transport includes encode + acquire on this Game View size (792×421) plus CLI roundtrip spawn (~1.2 s p50 for `unity command`, which is process spawn, not capture engine).

---

# C. Tests

## Static

`scripts/prepush-validate.sh --static-only`

- Python bridge: **43 ran, 43 passed, 0 failed**
- Quality gate: **0 errors**, 5 pre-existing line-count warnings

## Unity EditMode Test Runner

**Not executed.** `list_tests` returned `Count: 0`.

`Tests~/Editor` is UPM-hidden (`~`). Copying it to `EditModeTests/` made Unity compile the test asmdef, then failed with `CS0246 Newtonsoft` even when `Unity.Newtonsoft.Json` was referenced. That import was removed so production could compile. Source tests exist:

- `Tests~/Editor/CaptureGatewayTests.cs`
- `Tests~/Editor/NexusCommandRegistryTests.cs`
- `Tests~/Editor/NexusRuntimeSelectorTests.cs`
- `Tests~/Editor/OpenSourceApiContractTests.cs`

Recipe: `scripts/enable-assets-editmode-tests.sh` (currently copies four files; Newtonsoft reference still needs a harness fix). Until Test Runner prints pass/fail counts, do not mark M4 accepted.

## Live integration (this pass)

| Scenario | Result |
| :--- | :--- |
| HTTP `get_server_status` | alive, port 8081, runtime object present |
| HTTP `nexus.project_map` | success |
| HTTP `nexus.group_compile_errors` | success, 0 errors |
| HTTP `capture_game_view_screenshot` PNG | success, 792×421 |
| HTTP `nexus.capture_game_view` JPEG | success, stall 6.99 ms, wait 287.9 ms |
| Pipeline `nexus_project_map` | success |
| Pipeline `nexus_group_compile_errors` | success |
| Pipeline JPEG 20+100 | 100/100, stall p50 7.25 ms |
| Pipeline PNG / Q50 / 1600×900 / bad format | pass |
| Force `legacy` | `requested=legacy`, `effective=legacy` immediately |
| Restore `auto` | `effective=pipeline` when eligible |

`list_tools` live: **120** visible names, **31275** schema bytes, **zero** `nexus_*` aliases advertised. Profiles:

| Profile | Count | Names |
| :--- | ---: | :--- |
| core | 3 | `get_server_status`, `nexus.project_map`, `nexus.group_compile_errors` |
| visual | 2 | `nexus.capture_game_view`, `capture_game_view_screenshot` |
| scene | 4 | high-level scene/context only |
| compat | 2 | `get_server_status`, `capture_game_view_screenshot` |

`prepare_context` was not migrated; it is not in core.

---

# D. Domain reload

Implemented:

- New capture rejected while `_reloading`
- `beforeAssemblyReload` fails pending TCS with `DomainReloadInterrupted`
- Submitted RT is **not** `ReleaseTemporary`’d under a live GPU request; the field is cleared and Unity destroys objects on reload
- Teardown does not wait on GPU
- Last-healthy Pipeline state is persisted on the main thread (SessionState from a worker threw and left `health_unknown=true` until that was fixed)

Live:

| Batch | Cycles | Recoveries | Hangs | Notes |
| :--- | ---: | ---: | ---: | :--- |
| RequestScriptReload + capture | 10 | 10 | 0 | pending capture completed before reload |
| RequestScriptReload + capture | 10 | 10 | 0 | second batch |
| `--detach` capture then compile/reload | 1 | next capture succeeded | 0 | detached job was queued; exact pending-fail payload not harvested |

**20 / 20** reload recoveries, **0 hangs**. Session generation advanced (observed 30 → 43+). Runtime reinitialized; Pipeline health returned to healthy; next capture succeeded without manual recovery.

The 20 cycles were `EditorUtility.RequestScriptReload()` (real domain reload, no script churn). A 20-cycle compile-dirty loop was not repeated. One detached in-flight capture was overlapped with reload; the Editor did not deadlock.

---

# E. Multi-editor

**Not live-tested.** One Editor PID (57901) was running this project. Puzzle had Nexus Python bridges but no second Unity Editor.

Implemented / unit-level:

- Pipeline session file is per-project `Library/Pipeline/.unity-pipeline-port` (pid, port, projectPath). Ports 7800–7849.
- `unity command --project-path` targets that project.
- Case A: foreign 8081 → `_foreignProjectOwnsPort`, skip bind when Pipeline is selected, `runtime.legacy_unavailable_reason = foreign_project`, no bind fight.
- Case B: same-project attach stays Attached; skip-to-Stopped only for foreign + skip-bind.
- Case C: recovery after the other project closes is a later probe/restart, not a destructive steal.

Until two Editors are opened, M4 cannot be accepted.

---

# F. Runtime selection

| Mode | Behavior |
| :--- | :--- |
| `legacy` | Always Legacy. Never silently switches to Pipeline. Live-verified. |
| `pipeline` | Effective stays Pipeline even if unhealthy. `CanSkipLegacyHttpBind` true. No silent HTTP fallback. |
| `auto` | Pipeline only when eligible (Unity 6000, package, commands, session file, live PID, Unity-like process, TCP 50 ms). Else Legacy. |

Health:

- Re-reads port file on probe (missing file → unhealthy)
- Dead PID → stale file, skip TCP
- Live PID whose process name is not Unity → port-may-be-stolen
- TCP 50 ms on a worker thread
- Last-healthy seed across domain reload so Auto does not flap to Legacy during `health_unknown`
- PersistHealth marshalled to main thread

Informational maturity (not a removal justification):

```text
unity_cli.detected = false
unity_cli.prerelease = true
unity_cli.stable_1_0_or_newer = false
note: Editor does not shell unity --version; last observed CLI is 1.0.0-beta.10

pipeline.detected = true
pipeline.version = 0.7.0-exp.1
pipeline.experimental = true
pipeline.supported = true   # Unity 6000 + package + commands
```

Live after this pass: `requested=auto`, `effective=pipeline`, `eligible=true`, `legacy_http_bound=true`.

---

# G. Tool curation

Canonical visible commands are not the full 117/151 dumps.

- One canonical id, one visible schema, aliases dispatchable only.
- Live full list: 120 names (117 original + 3 canonical ids, aliases hidden).
- Profiles: `core`, `visual`, `scene`, `compat` as above.
- Inspector / window capture stay on the legacy ReadPixels path and are **not** in `visual`. Optional gateway routing for those was skipped.

---

# H. Documentation corrections

| Claim | Action |
| :--- | :--- |
| `GetData` takes exactly 0.0001 ms | Retracted as a public precision claim. Cost was **below the benchmark’s reliable timing threshold** (≥ 0.005 ms). |
| Driver ring-buffer | Do not publish. Prefer **Unity-managed readback memory**. No full-frame managed raw pixel copy is performed by our C# before encoding. |
| 23.57 MB → 0.61 MB, **97.4% less GC** | Relabeled **derived / estimated memory footprint**. Not a measured GC reduction. |
| VideoToolbox / hardware JPEG / libjpeg-turbo + NEON | Not independently proven for this Unity build. Public wording: **Unity ImageConversion JPEG encoder, CPU-side in the measured path.** |
| 177× faster / 13.3× smaller | Remain retracted as unfair (cold CLI spawn + JPEG vs PNG). Banner added on `NEXUS_UNITY_ARCHITECTURE_EVALUATION.md`. Fair comparison: `CORRECTION_REPORT.md`. |
| JPEG Q85 identical VLM comprehension / 1600×900 negligible vision loss | Not claimed. Q85 is the configurable product default from image-quality metrics and manual inspection. |

Updated: this handoff, `CAPTURE_VALIDATION_REPORT.md` GetData + GC sections, `NEXUS_UNITY_ARCHITECTURE_EVALUATION.md` header, `API_REFERENCE.MD`, `CHANGELOG.md`.

---

# I. Remaining technical debt

1. **Unity Test Runner** for `Tests~/Editor` in the Assets harness (Newtonsoft reference when the test asmdef is imported).
2. **Two-editor live** Cases A/B/C.
3. **20 compile-dirty reload cycles** with a capture still GPU-pending (`--detach` was only one sample).
4. Overlay / orientation **image** check (production still y-flips; no OCR this pass).
5. `unity command` per-process spawn ~1.2 s p50 — use persistent `unity mcp` for transport timing, not CLI spawn.
6. Submit-to-done ~299 ms via CLI vs ~6–8 ms in-engine; likely tick/focus. Not counted as stall.
7. Research C# moved to `Research~/capture/` so a clean clone compiles. Generated JSON/captures gitignored. Keep reports as research docs.
8. Inspector/window capture still legacy ReadPixels; optional gateway wrap skipped.
9. CLI executable version is not probed from Editor (no shell-out).
10. Do not start a new architectural migration.

---

# J. Release readiness

```text
READY FOR INTERNAL MERGE
```

Not a release candidate. Not a public sunset.

Why internal merge is appropriate: the blocking Pipeline capture bug is fixed and live-measured; HTTP and Pipeline share Capture V2; runtime modes behave; Legacy remains supported; static validation is green; docs no longer resurrect the retracted ratios.

Why not RC / M4 accepted: Test Runner never printed Unity EditMode results; two Editors were not run; overlay was not image-verified; CLI/Pipeline remain beta/experimental.

---

## Frozen production capture (do not rename)

```text
GameView.m_RenderTexture
→ immediate copy to Nexus-owned RT (Blit scale y=-1, offset y=1)
→ optional normalize
→ AsyncGPUReadback.Request
→ return control to Editor
→ EditorApplication.update polling
→ request.done
→ GetData<byte>()
→ EncodeNativeArrayToJPG / PNG
→ CaptureResult
```

Canonical name: **DriverOwnedReadback**. Do not reintroduce R1/R2. Do not replace production with `RequestIntoNativeArray`.

## Artifact classification

| Class | Items |
| :--- | :--- |
| Ship | `Editor/Capture`, `Editor/Commands`, `Editor/Pipeline`, `Editor/Runtime`, `Tests~/Editor/*Tests.cs`, `scripts/stabilize-pipeline-capture.py`, `scripts/stabilize-domain-reload.py`, `scripts/enable-assets-editmode-tests.sh` |
| Research/docs | `CAPTURE_VALIDATION_REPORT.md`, `CORRECTION_REPORT.md`, `CAPTURE_SPIKES_REPORT.md`, `NEXUS_UNITY_ARCHITECTURE_EVALUATION.md`, `Research~/capture/*.cs`, research `scripts/run-*.py` |
| Gitignore | `captures/`, `*-results.json/csv`, `*-benchmark-results.*`, `*-tools.json`, `EditModeTests/` |
| Remove | none blindly; research C# was moved out of `Editor/` so it does not compile |

Production `MCPServerMethods.Init` does not register CaptureSpikes / CaptureValidation / T1T2.

## Do not do next

Do not delete Legacy HTTP. Do not design Capture V3. Do not wrap all 151 Unity tools. Do not announce deprecation. Stop after this review.
