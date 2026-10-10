# 07 — Stress and reliability, what was actually run

Date: 2026-10-04. Lead note. No new Editor session was started for this file. It records the stress that the baseline, performance, and deathmatch runs already produced, and the stress that was not run.

Commit `bfdd0f7e71c851fb4615956f3c420940a9623220`. Unity `6000.3.16f1` PID 4465. Pipeline `0.7.0-exp.1`. CLI `1.0.0-beta.10`. No LLM.

Labels: **MEASURED** means a number in `evidence/`. **NOT RUN** means the experiment was not performed.

## What ran

### Readiness

**MEASURED**, EditMode only: `EditorReadinessTests` 17 passed, 0 failed, 0 skipped (`evidence/baseline/index.json`). That is the unit suite on this tip. It is not a live compile / reload / ready cycle.

**NOT RUN**: the P0-02 20/20 live compile acceptance. That evidence stays on `6fff2e8` / `060cc13`. It was not repeated.

### Canonical envelope

**MEASURED**, EditMode: `NexusCommandEnvelopeTests` 29 passed, 0 failed, 0 skipped. Runner 6.81 s. Slowest body `ClampedQualityAndNegativeWidth_AreNotInvalidArgument` 1.292 s.

**MEASURED**, live HTTP, Game View closed: `nexus_look` returned HTTP 200, envelope `ok:false`, code `unavailable`, retryable true, about 429 bytes, client about 99 ms. Repeated in the closed-view performance series (`evidence/perf/summary.json`). That is a typed failure, not an envelope regression.

**NOT RUN**: request-id idempotency under retry, session-generation stability across a domain reload, or a serialization soak of success and failure envelopes beyond the suite and those live calls.

### Capture

**MEASURED**: 12 file deliveries and 4 base64 deliveries with the Game View already open (`evidence/perf/look_success.json`). Every image was 768×407 and 12387 bytes. File calls: first 232.2 ms, then 8.0–14.4 ms. Base64 calls: 8.3–10.9 ms client, wire about 17 KB. No encode failure in that set. No stuck exec slot was observed; `editor_status` was ready after the deathmatch.

**MEASURED** memory, no idle control:

| Window | Allocated | Reserved | Mono used | Mono heap |
|---|---:|---:|---:|---:|
| Closed Game View series | 517408660 → 518685121 (+1.3 MB) | not used as a leak claim | +5.1 MB | — |
| 16 open-view looks | 563240048 → 586498838 (+23.3 MB) | +29.8 MB | +5.6 MB | unchanged |

Sixteen shots did not run away. Blaming the +23.3 MB on temporary RenderTextures is not justified. There is no idle interval of the same length with no capture.

## Capture-debt experiments

| Id | Status | Result |
|---|---|---|
| T-CAP-1 repeated RT soak | Partial | 16 successful looks. No idle control. Not a leak verdict. |
| T-CAP-2 reload during a pending capture | NOT RUN | Would domain-reload the only Editor. Not attempted. |
| T-CAP-3 overlay crop pixels | Attempted, inconclusive | Red/blue overlay bars were absent from both the Nexus JPEG and the Unity camera PNG. No second delayed shot. Not a pass and not a fail. See `08_CAPTURE_DEATHMATCH.md`. |
| T-CAP-4 Scene View JPEG pixels | Partial | Green bench cube is in the Nexus scene JPEG and not in Unity's scene PNG. The EditMode test `SceneCaptureFramesATargetAndRestoresTheView` passed in 0.017 s and does not decode a JPEG. |

Also **NOT RUN**, and not fixed: `base64_ms` source audit (the new sample shows 6.6–9.2 ms against `encode_ms` 1.4–1.5 ms, which matches "larger than encode" and does not re-prove a double count), `InstanceIDToObject` obsolescence, 2D rotation restore, component instance ids.

## What this does not support

- A capture leak fix.
- A domain-reload capture fix.
- An overlay-crop fix.
- A scene-flip fix. The edge-color swap is confounded because Nexus framed the cube and Unity did not.
- Any claim that the Editor was soaked across compile, play entry, or focus changes.

No production change was made from these results.
