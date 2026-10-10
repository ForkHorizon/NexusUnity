# Full implementation handoff — 2026-10-04

This document is for a planning agent that cannot open the NexusUnity repository. It records what was implemented, what was proven, what was deliberately left undone, and which constraints the next plan must keep. It is not a change to the master plan.

If this file and `NexusFeature~/MASTER_PLAN_V2.md` disagree, the master plan wins. The plan in force is v2.6, commit `c9cbbc6` (`docs: Master Plan v2.6 — architecture theory register, cheap falsification stage, confirmatory Gate F (planning only)`). This sprint did not edit that file.

Date of the work: 2026-10-04. Nothing was pushed. Nothing was merged to `development`.

---

## 0. How to use this file

Read sections 1–2 before scheduling anything. Sections 3–6 are the finished tasks and their frozen contracts. Section 7 is the evidence and the tests, with counts. Section 8 is the debt that must stay visible. Section 9 is what a later plan may schedule, and what it must not schedule. Section 10 is the operating rules of the harness that produced the evidence.

Do not treat a sentence in this file as permission to start S5, S6-P, Mixed, C1, Gate F, a façade, write delegation, or `eval` / `run_script` forwarding. Those stayed untouched.

---

## 1. Repository identity

| Fact | Value |
|---|---|
| Package id | `com.forkhorizon.nexus.unity` |
| Package version in the repo | **1.7.0 Unreleased** |
| Older memory that says 1.5.0 | Stale. Trust the repo |
| Git root | The package directory `Assets/NexusUnity` inside the Unity project `UnityTestForNexus` |
| Harness | `/Users/daliys/Daliys/UnityProjects/UnityTestForNexus` is only the local Editor. Do not treat harness edits as package work |
| GitHub | `ForkHorizon/NexusUnity` |
| Base branch | `development` |
| Current branch | `rework2/S-02` |
| Tip | `bfdd0f7e71c851fb4615956f3c420940a9623220` |
| Pushed | No |
| Dirty tracked files | None |
| Untracked, leave untracked | `.graphifyignore`, `graphify-out.meta`. Do not commit them. Do not delete them to make `scripts/prepush-validate.sh` green |

The parent Unity project is the test harness. Package source, bridge, tests, changelog, README, and docs live in the package repo.

Public docs that were updated for the behavior that actually shipped: `CHANGELOG.md`, `README.md`, `DOCUMENTATION.MD`, `API_REFERENCE.MD`. They describe 1.7.0 Unreleased.

---

## 2. Architecture constraint (unchanged)

The architecture decision is **unresolved**. Pilot results from 2026-10-03 are insufficient evidence. They do not approve or reject an architecture. The pilot's S6-domain arm (a flat regrouping of Unity actions) is not S6-P and is not evidence against Nexus-primary.

This sprint did not implement or alter:

- S5 selective façade
- S6-P Nexus-primary
- Mixed Nexus-primary
- C1 as a product surface
- Gate F
- a full Unity façade
- a generated 154-command façade
- broad write delegation
- `eval` forwarding
- `run_script` forwarding
- an architecture-specific router
- a master-plan edit that would approve an architecture

Bench-only prototypes, if any future session builds them, stay under `NexusFeature~/bench/` and must not ship.

Architecture-independent work that **did** ship: P0-02, S-04, S-07, S-02.

---

## 3. Git stack

The commits are stacked. Each parent is the row above. Do not rebase, squash, or rewrite them.

| Order | Short | Full | Subject |
|---|---|---|---|
| 1 | `d738dfc` | `d738dfc427cd8509d4e25f1a0e33e7e6b3592c2c` | fix: keep Editor readiness false until the compile cycle finishes |
| 2 | `6fff2e8` | `6fff2e8a622069deca37e871702366bfd698132c` | fix: keep a newer script write from settling on an older compile |
| 3 | `060cc13` | `060cc13d7307d4334e2e4ef34a40c15d48c4184b` | docs: record the P0-02 20-run Editor acceptance |
| 4 | `aa923f6` | `aa923f6302dcf2663f6c2fce98e7c95dcec6e33a` | feat: return one envelope from the canonical commands |
| 5 | `23005b4` | `23005b468236984e459a6aaf148e7e05b9240436` | feat: default canonical captures to a 768 JPEG look |
| 6 | `bfdd0f7` | `bfdd0f7e71c851fb4615956f3c420940a9623220` | feat: lock canonical CLI parameters to the descriptor |

Why the stack is not three independent branches from `development`:

- S-07 edits the same canonical capture command that S-04 wrapped in the envelope. Branching S-07 from P0-02 would have conflicted and would have dropped the envelope.
- S-02 locks the parameter list that S-04 and S-07 shipped (request id, scene view, delivery, 768 default). Branching it from P0-02 would have locked the old parameter list.

Suggested original names were `rework2/S-04`, `rework2/S-07`, and `rework2/S-02`. Only the tip branch `rework2/S-02` contains all six commits. The earlier branch names are ancestors, not parallel lines.

### 3.1 Files in `d738dfc` (20 files, +931 / −154)

Production readiness:

- `Editor/Readiness/EditorReadiness.cs` (new, 294 lines)
- `Editor/Readiness/EditorReadiness.Wire.cs` (new, 150 lines)
- `Editor/Readiness.meta`
- `Editor/MCPServer.cs` (subscription hook, +1)
- `Editor/MCPServerMethods.Status.cs`
- `Editor/MCPServerMethods.Sync.cs` (wait path uses the predicate)
- `Editor/MCPServerMethods.Asset.cs`
- `Editor/MCPServerMethods.Hierarchy.cs` (script-write arm)
- `Editor/MCPServerMethods.Tools.cs`
- `Editor/nexus_bridge/routes_editor.py` (bridge re-poll)
- `Editor/tests/test_routing_wait.py`
- `Tests/Editor/EditorReadinessTests.cs` (new)
- `Research~/evidence/P0-02/STATUS.md` (at this commit the 20/20 was explicitly **not run**)
- `API_REFERENCE.MD`, `CHANGELOG.md`, `DOCUMENTATION.MD`, `README.md`

Commit message states the 20/20 Editor.log evidence was not run yet. That gap was closed later by `060cc13`, after `6fff2e8`.

### 3.2 Files in `6fff2e8` (8 files, +143 / −14)

- `Editor/Readiness/EditorReadiness.cs`
- `Editor/Readiness/EditorReadiness.Wire.cs`
- `Editor/MCPServerMethods.Hierarchy.cs`
- `Tests/Editor/EditorReadinessTests.cs` (four new tests)
- docs: `API_REFERENCE.MD`, `CHANGELOG.md`, `DOCUMENTATION.MD`, `README.md`

This is the code the 20/20 actually exercised. `d738dfc` alone is not the accepted build.

### 3.3 Files in `060cc13` (24 files, +6191 / −24)

Evidence only. No product code.

- `Research~/evidence/P0-02/STATUS.md`
- `Research~/evidence/P0-02/judge.py`
- `Research~/evidence/P0-02/verdict.json`
- `Research~/evidence/P0-02/live/runs.jsonl`
- `Research~/evidence/P0-02/live/log-00.txt` through `log-19.txt`

The temporary acceptance driver and the stimulus script `Editor/EvidenceTemp/P002Stimulus.cs` were deleted after the run. They are not in git. Do not restore them.

### 3.4 Files in `aa923f6` (18 files, +1354 / −43)

- `Editor/Commands/NexusCommandEnvelope.cs` (new)
- `Editor/Commands/NexusCommandArguments.cs` (new)
- `Editor/Commands/ProjectMapCommand.cs`
- `Editor/Commands/GroupCompileErrorsCommand.cs`
- `Editor/Commands/CaptureGameViewCommand.cs`
- `Editor/Commands/NexusCommandResults.cs` (comment only: `Success` means a payload was produced)
- `Editor/Pipeline/NexusPipelineCommands.cs`
- `Tests/Editor/NexusCommandEnvelopeTests.cs` (new, 659 lines)
- `Tests/Editor/CaptureGatewayTests.cs` (small assertion update)
- `Editor/tests/test_canonical_envelope_passthrough.py` (new)
- docs

### 3.5 Files in `23005b4` (25 files, +1084 / −147)

New:

- `Editor/Capture/CaptureFraming.cs`
- `Editor/Capture/SceneViewCaptureSource.cs`
- `Editor/Commands/CaptureFileDelivery.cs`
- `Editor/Commands/CaptureLookRequest.cs`
- `Editor/Commands/NexusCaptureErrors.cs`
- `Tests/Editor/CaptureLookTests.cs`
- matching `.meta` files

Edited:

- `Editor/Capture/CaptureGateway.cs`
- `Editor/Capture/CaptureModels.cs`
- `Editor/Capture/GameViewCaptureSource.cs`
- `Editor/Commands/CaptureGameViewCommand.cs`
- `Editor/Commands/NexusCommandEnvelope.cs` (capture errors moved out so the envelope file stayed under the line limit)
- `Editor/Commands/NexusCommandResults.cs` (`file_path`, Base64 ignore)
- `Editor/Pipeline/NexusPipelineCommands.cs`
- `Tests/Editor/NexusCommandRegistryTests.cs`
- `Tests/Editor/OpenSourceApiContractTests.cs`
- docs

### 3.6 Files in `bfdd0f7` (13 files, +288 / −53)

- `Editor/Commands/NexusCommandDescriptor.cs` (`HasDefault`, `DefaultValue`, `Optional`)
- `Editor/Commands/ProjectMapCommand.cs`
- `Editor/Commands/GroupCompileErrorsCommand.cs` (`DefaultMaxLogs = 50`)
- `Editor/Commands/CaptureGameViewCommand.cs` (named defaults, `FormatOrDefault`)
- `Editor/Commands/CaptureLookRequest.cs` (HTTP omit uses those constants)
- `Editor/Pipeline/NexusPipelineCommands.cs` (CLI defaults use those constants)
- `Tests/Editor/NexusCommandCliDriftTests.cs` (new)
- `Tests/Editor/NexusCommandEnvelopeTests.cs` (jpg default assert now calls `FormatOrDefault`)
- docs

---

## 4. P0-02 — truthful readiness and epochs

**Status: accepted** on product commit `6fff2e8`, recorded by evidence commit `060cc13`.

Issue **#0013** was recorded fixed with the local `pjm` CLI only after `judge.py` exited 0. Do not record it again.

Issue **#0012** is **not** fixed. `unity_wait` compilation no longer calls `refresh_asset_database`. That change alone does not close #0012. Do not claim it closed.

### 4.1 What the predicate is

One predicate feeds `get_server_status`, `initialize`, `wait_for_ready`, and idle waits. The HTTP listener reads a published snapshot. It does not call Unity APIs off the main thread.

`compile_epoch` and `reload_epoch` persist in `SessionState` because a domain reload clears static fields. Session keys:

- `Nexus.Readiness.CompileEpoch`
- `Nexus.Readiness.ReloadEpoch`
- `Nexus.Readiness.CompileInFlight`
- `Nexus.Readiness.ReloadArmed`
- `Nexus.Readiness.ScriptWrite`
- `Nexus.Readiness.PendingRefresh`
- `Nexus.Readiness.RefreshInvoked`
- `Nexus.Readiness.SeenCompile`
- `Nexus.Readiness.ReloadExpected`
- `Nexus.Readiness.BaselineCompile`
- `Nexus.Readiness.BaselineReload`
- `Nexus.Readiness.QuietTicks`

Quiet settle count: `QuietTicksToSettle = 2` editor ticks.

### 4.2 Not-ready reasons, in blocking order

The first match wins:

1. `domain_reload` — a reload is armed and has not been consumed
2. `play_transition` — Play Mode is changing
3. `compiling` — a compile is in flight
4. `script_write` — a script write was armed and its compile-and-reload cycle is not finished
5. `pending_refresh` — a refresh was requested and not yet invoked
6. `importing` — the asset pipeline is updating
7. `pending_refresh` again if a refresh is still pending after the update flag

Idle reasons once nothing blocks:

- `ready` — a cycle finished and settled
- `already_ready` — nothing was pending (including a refresh that compiled nothing)

`wait_for_editor_idle` returns `ready`, `epoch`, and `reason`. If a domain reload is in progress it returns `Reloading` instead of blocking through the reload. Legacy fields `status` and `is_idle` remain. `wait_for_ready` still returns a boolean.

Busy-reason strings exposed beside the predicate:

- compiling → `compiling`
- play transition → `play_mode_transition`
- domain reload → `domain_reload`
- script write before compile starts → `importing` (the test `ScriptWrite_IsNotReadyBeforeCompileStarts` asserts this)

### 4.3 Lifecycle rules that were the actual bugs

- A successful script compile stays not-ready through the gap after compilation finishes and before the domain reload. Readiness becomes true only after `reload_epoch` advances.
- A failed compile does not wait for a reload that will never come. It settles only after the editor is quiet. Failure is read from `scriptCompilationFailed` on the **following** editor tick, not on the same tick as the compile callback.
- A refresh that causes no compilation returns `already_ready` and does not advance epochs.
- A script write that arrives while another compile is in flight does not settle on that older reload. The newer write stays not-ready through the old reload, then refreshes again. This is `6fff2e8`. Without it, `d738dfc` could report ready too early.
- Play Mode transition frames do not count as quiet ticks.
- On macOS, script refresh waits until the Editor is focused, or 15 seconds, whichever comes first.
- Pending asset refresh stays not-ready until that refresh finishes.
- The bridge `unity_wait` compilation path re-polls `is_editor_idle` after a dropped connection. An early idle flag, or a missing epoch, is not success.
- `ArmScriptWrite` runs from the production script-write path (`WriteFile` with `confirm: true` in the acceptance run), which also calls `TriggerSafeAssetRefresh`.

### 4.4 Acceptance criterion (do not weaken)

Twenty consecutive script writes of the **final** code. Each write must force compilation, a successful compile, an assembly/domain reload, and only then readiness true.

A run fails the criterion if:

- readiness is true before the reload for that write
- the log parser accepts an earlier reload or the early `RefreshV2(NoUpdateAssetOptions)` line
- the same epoch is reused
- the write did not recompile
- cached state is treated as a new cycle
- a multi-second compile produces a sub-second ready verdict

Acceptance is 20 consecutive runs of the final code. "19 old passes plus 1 new pass" does not count. If a run fails, the count stops, the bug is fixed, and a fresh 20 starts.

### 4.5 What the 20/20 actually showed

Environment:

- Unity `6000.3.16f1`
- one Editor, PID 4465
- project `/Users/daliys/Daliys/UnityProjects/UnityTestForNexus`
- macOS
- Editor focused before the run started
- stimulus file `Assets/NexusUnity/Editor/EvidenceTemp/P002Stimulus.cs` in assembly `UnityMCP.Editor`, rewritten each run through production `WriteFile` (`confirm: true`), deleted after the run
- judge `Research~/evidence/P0-02/judge.py` records facts only; it does not drive the Editor

Status: **MET**. `judge.py` exit 0. This is one consecutive run of `6fff2e8`. It is not a splice.

Timing:

- median write-to-ready 4.388 s
- min 3.891 s, max 4.663 s
- median forced domain-reload refresh 1.909 s
- median domain-reload profiling 1.788 s
- no run ready in under a second
- every `before_reload` sample had `ready=false`
- every `after_write` reason was `script_write`
- compile epoch 3→23, reload epoch 2→22, each run exactly +1
- every slice compiled `UnityMCP.Editor.dll`
- every slice contained one `Reloading assemblies after forced synchronous recompile.`

| run | wait_s | tundra_s | reload_profiling_s | forced_refresh_s | compile | reload | not_ready | ready_before_reload |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 00 | 3.891 | 0.87 | 1.663 | 1.921 | 3→4 | 2→3 | script_write | false |
| 01 | 4.170 | 0.83 | 1.695 | 1.801 | 4→5 | 3→4 | script_write | false |
| 02 | 4.330 | 0.83 | 1.731 | 1.822 | 5→6 | 4→5 | script_write | false |
| 03 | 4.313 | 0.78 | 1.684 | 1.774 | 6→7 | 5→6 | script_write | false |
| 04 | 4.378 | 0.86 | 1.788 | 1.909 | 7→8 | 6→7 | script_write | false |
| 05 | 4.318 | 0.86 | 1.803 | 1.914 | 8→9 | 7→8 | script_write | false |
| 06 | 4.104 | 0.80 | 1.729 | 1.833 | 9→10 | 8→9 | script_write | false |
| 07 | 4.663 | 0.97 | 1.708 | 1.799 | 10→11 | 9→10 | script_write | false |
| 08 | 4.290 | 0.81 | 1.728 | 1.823 | 11→12 | 10→11 | script_write | false |
| 09 | 4.268 | 0.89 | 1.702 | 1.809 | 12→13 | 11→12 | script_write | false |
| 10 | 4.394 | 0.93 | 1.792 | 1.899 | 13→14 | 12→13 | script_write | false |
| 11 | 4.529 | 0.87 | 1.810 | 1.911 | 14→15 | 13→14 | script_write | false |
| 12 | 4.256 | 0.80 | 1.749 | 1.859 | 15→16 | 14→15 | script_write | false |
| 13 | 4.465 | 0.89 | 1.819 | 1.915 | 16→17 | 15→16 | script_write | false |
| 14 | 4.559 | 0.88 | 1.830 | 1.928 | 17→18 | 16→17 | script_write | false |
| 15 | 4.417 | 0.91 | 1.738 | 1.838 | 18→19 | 17→18 | script_write | false |
| 16 | 4.567 | 0.82 | 1.881 | 2.004 | 19→20 | 18→19 | script_write | false |
| 17 | 4.464 | 0.79 | 1.811 | 1.915 | 20→21 | 19→20 | script_write | false |
| 18 | 4.401 | 0.78 | 1.878 | 1.990 | 21→22 | 20→21 | script_write | false |
| 19 | 4.388 | 0.81 | 1.800 | 1.931 | 22→23 | 21→22 | script_write | false |

Raw artifacts, all in the evidence commit:

- `Research~/evidence/P0-02/live/runs.jsonl` — append-only facts
- `Research~/evidence/P0-02/live/log-00.txt` through `log-19.txt` — Editor.log slices anchored at each run's nonce. Licensing and access-token lines were removed before commit
- `Research~/evidence/P0-02/verdict.json` — judge output
- `Research~/evidence/P0-02/judge.py`
- `Research~/evidence/P0-02/STATUS.md` — status **MET**

Judge bug that was fixed inside the evidence commit, without re-running the Editor: the first judge pass used the first `Asset Pipeline Refresh` total in each slice. That line is `RefreshV2(NoUpdateAssetOptions)` at about 0.11 s and happens before the compile. The judge now requires the later `StopAssetImportingV2(ForceSynchronousImport | ForceDomainReload)` total, plus `UnityMCP.Editor.dll` in the slice. Re-judging the same raw logs is not a new 20.

### 4.6 EditMode tests on the accepted code

`UnityMCP.Editor.Tests.EditorReadinessTests`: **17 passed**, 0 failed, 0 skipped. The STATUS file at `060cc13` records this count. The four tests added in `6fff2e8` are the last four behavior rows below.

| Test | What it locks |
|---|---|
| `AlreadyReady_WhenNothingIsPending` | idle reason `already_ready`, busy `idle` |
| `CompileEpoch_IncrementsWhenCompilationFinishes` | compile epoch |
| `ReloadEpoch_IncrementsOnceWhenArmedReloadIsConsumed` | reload epoch once |
| `Epochs_PersistAcrossSimulatedDomainReset` | SessionState survives a simulated reload |
| `ScriptWrite_IsNotReadyBeforeCompileStarts` | reason `script_write`, busy `importing` |
| `PendingRefresh_IsNotReadyUntilRefreshSettles` | pending, then importing, then already ready |
| `PlayTransition_IsNotReady` | reason `play_transition`, busy `play_mode_transition` |
| `ScriptWrite_NoOpRefresh_BecomesAlreadyReadyWithoutEpochAdvance` | no-op refresh does not bump epochs |
| `ScriptWrite_FailedCompile_SettlesWithoutReload` | failed compile can become `ready` without a reload |
| `ScriptWrite_SuccessfulCompile_StaysBlockedUntilReload` | success stays `script_write` until reload, then `ready` |
| `ScriptWrite_ReloadWithoutCompile_DoesNotReportReady` | a reload that is not the compile's reload is not success |
| `ScriptWrite_ReportsReadyOnlyAfterCompileAndReload` | both halves required |
| `ScriptWrite_OverlappingArm_DoesNotTreatOldCompileAsNoOp` | the `6fff2e8` race |
| `ScriptWrite_FailedCompile_ProbeSettlesOnTheFollowingTick` | failure probe waits one tick |
| `ScriptWrite_SuccessFlag_DoesNotSettleWithoutReload` | a success flag alone is not ready |
| `ScriptWrite_PlayTransition_DoesNotCountAsQuiet` | play frames are not quiet ticks |
| `StatusAndWait_ShareTheSameReadyFlag` | status and wait read the same snapshot |

Python `Editor/tests` on this commit: **45 passed**.

Quality gate: 0 errors, 5 pre-existing warnings (listed in section 7.4).

### 4.7 Live cases that were not part of the 20

Do not describe these as done. EditMode covers some of them. Live Editor stress was not run:

- a compile error, then a fix and a recompile
- a no-op refresh (EditMode only)
- two script writes close together (EditMode covers the overlapping arm)
- a Play Mode transition during a readiness wait (EditMode covers quiet-tick accounting)
- the 15 second unfocused timeout path
- `LockReloadAssemblies`
- an Editor restart / persisted SessionState check on a real restart (EditMode simulates the SessionState round-trip)

The Editor was already warm. Forced-reload totals of about 1.8–2.0 s and write-to-ready of about 3.9–4.7 s are not the cold 7.5–8.5 s figure from older notes. No run was ready before its reload, which is the criterion that matters.

---

## 5. S-04 — one envelope for the three canonical commands

**Status: committed** `aa923f6`. Parent is the P0-02 evidence commit `060cc13`.

### 5.1 Which commands

Only these three. Legacy raw methods are outside the envelope.

| Canonical id | Listed alias | Other aliases | Profile | CLI return |
|---|---|---|---|---|
| `nexus.project_map` | `nexus_project_map` | none | `core` | `NexusCommandEnvelope` (sync) |
| `nexus.group_compile_errors` | `nexus_group_compile_errors` | none | `core` | `NexusCommandEnvelope` (sync) |
| `nexus.capture_game_view` | `nexus_capture_game_view` | `nexus_look`, `nexus.look` added later in S-07, unlisted | `visual` | `Task<NexusCommandEnvelope>` |

`list_tools` count stays **120** = 117 raw tools + 3 canonical ids. Aliases are registered for dispatch and are not advertised a second time. `core` profile includes the project map and the compile-error command and omits capture. `visual` profile includes `nexus.capture_game_view` and the legacy `capture_game_view_screenshot`, and does not include the aliases.

Descriptions shared by HTTP and CLI:

- project map: "Generates a curated high-level project intelligence map for AI agents."
- compile errors: "Analyzes recent console logs, extracts C# compiler errors (CSxxxx), and groups them by root cause file."
- capture, after S-07: "Captures Game View Overlay or a Scene View framed on a target. JPEG by default, longest edge 768 unless raised."

### 5.2 Wire shape

Success:

```json
{
  "ok": true,
  "data": {},
  "error": null,
  "meta": { "ms": 0, "mode": "legacy", "gen": 1, "request_id": null }
}
```

Failure:

```json
{
  "ok": false,
  "data": null,
  "error": {
    "code": "invalid_argument",
    "message": "human text",
    "retryable": false,
    "hint": "Change the arguments. Do not send the same arguments again."
  },
  "meta": { "ms": 0, "mode": "pipeline", "gen": 1, "request_id": "caller-string" }
}
```

`outcome` and `stack_trace` are omitted unless set (`NullValueHandling.Ignore`).

Domain failures are a JSON-RPC **result** (`ok: false` inside `result`). They are not a JSON-RPC error. Pipeline's outer exec body is still Unity's `{success, result}`. The envelope is the command's return value. Handlers must return the envelope rather than throw, or Pipeline turns the failure into HTTP 400.

Transport failures stay JSON-RPC errors. Codes seen and left as transport: `-32700`, `-32600`, `-32602`, `-32000`. Those carry `stackTrace` in `error.data` and have **no** `retryable` field. Sync `ProcessJsonRpc` on the async capture command returns `-32000` with a message that the caller must use `ProcessJsonRpcAsync`. That is intentional.

### 5.3 meta

- `meta.ms` — handler elapsed milliseconds, clamped at 0. Not comparable across calls as a benchmark by itself.
- `meta.mode` — `NexusRuntimeSelector.ToWireName(NexusRuntimeHost.EffectiveMode)`. Values are `legacy` or `pipeline` only. Never `auto`. Effective(Auto) does not return Auto.
- `meta.gen` — `MCPServer.SessionGeneration`. This is the editor session generation. It is **not** `compile_epoch` and **not** `reload_epoch`.
- `meta.request_id` — a copy of a string `params.request_id`. It is not stored. It is not an idempotency cache. The P0-05 five-minute cache was **not** built. Absent request id → null. The JSON-RPC `id` is not copied into `request_id`.

A non-string `request_id` is `invalid_argument`, the handler does not run, and `meta.request_id` is null.

`request_id` is advertised as an optional string on the three canonical `list_tools` schemas and on the Pipeline `[CliArg]`. Description constant: "Optional string copied into meta.request_id. It is not stored."

`CreateMeta` reads session generation and the cached runtime mode. It is safe to call off the main thread. It does not modify the project.

### 5.4 Codes, retry bits, and the exact hints

| Code | retryable | When | Hint actually returned |
|---|---|---|---|
| success | — | `ok: true`, `error: null` | — |
| `invalid_argument` | false | bad arguments, unknown target, non-string request id, unsupported format | "Change the arguments. Do not send the same arguments again." |
| `unavailable` | true | Game View missing, capture source empty, Scene View missing, target disabled, target not visible | surface-specific, see below |
| `busy` | true | another capture holds the gate. This is not a job id and not a `pending` code | "Wait for the in-flight capture to finish, then call again." |
| `timeout` | true | readback message contains "timed out", and there is no mutation outcome | "The read timed out before an image was produced. It is safe to call again." |
| `capture_failed` | true | other readback failures | "GPU readback failed before an image was produced. It is safe to call again." |
| `partial` | true | compile-error list taken while `IsCompiling` | "Wait until compilation finishes, then call again. Do not treat this list as complete." Message: "The editor is compiling, so this error list may be incomplete." |
| `editor_not_ready` | true | domain reload interrupted the capture | "Wait until the editor finishes reloading, then call again." |
| `internal` | false | unexpected exception, or an unmapped capture code | "The command failed before it could finish. Do not retry it blindly." |

Unavailable hints:

- Game View missing or empty source: "Open a Game View that has a visible area, then call again."
- Scene View missing: "Open a Scene View, then call again."
- Target disabled: "Enable the object, then call again."
- Target not visible: "Move the target into the Game View, or capture the Scene View, then call again."

Empty-hint fallback, used only when the caller of `Failure` passes an empty hint:

- retryable → "The same call can be sent again."
- not retryable → "Do not send the same call again until the cause is understood."

Unknown mutation outcome is a separate field, not a `code`. `Failure(..., outcome: "unknown")` forces `retryable` false and replaces the hint with: "The outcome is unknown. Do not retry this call. Inspect the editor before doing anything else." The serialized field is `error.outcome: "unknown"`. No current command emits it. A future write command must not encourage a blind retry when the outcome is unknown.

`partial` keeps `data`. `data.success` on that payload stays **true**. The authoritative bits are `ok == false`, `error.code == "partial"`, and `data.is_compiling == true`. The comment on `NexusCompileErrorsResult.Success` says the flag means a payload was produced, not that the command succeeded. Do not flip `data.success` unless a product decision says so.

Stack traces: included only when environment variable `NEXUS_DEBUG` is set and is not `0` or `false`. Field name is `stack_trace`. Transport errors are different: they still put `stackTrace` on the JSON-RPC error data.

`ArgumentException`, `FormatException`, `OverflowException`, and `JsonReaderException` map to `invalid_argument`. `TargetInvocationException` is unwrapped. `CaptureException` goes through `NexusCaptureErrors.FromCapture`.

Capture domain codes and the envelope code they become:

| `CaptureErrorCode` | numeric | envelope code | retryable |
|---|---|---|---|
| `GameViewUnavailable` | 1 | `unavailable` | true |
| `CaptureSourceEmpty` | 2 | `unavailable` | true |
| `CaptureBusy` | 3 | `busy` | true |
| `ReadbackFailed` | 4 | `timeout` if the message contains "timed out", otherwise `capture_failed` | true |
| `UnsupportedFormat` | 5 | `invalid_argument` | false |
| `DomainReloadInterrupted` | 6 | `editor_not_ready` | true |
| `SceneViewUnavailable` | 7 | `unavailable` | true |
| `TargetNotFound` | 8 | `invalid_argument` | false |
| `TargetDisabled` | 9 | `unavailable` | true |
| `TargetNotVisible` | 10 | `unavailable` | true |
| anything else | — | `internal` | false |

A readback message that contains the word "pending" (the driver-owned path can say "GPU readback was pending") is **not** a `pending` code. It is `capture_failed` unless the message also contains "timed out".

### 5.5 Argument rules that are not errors

- JPEG quality `0` or `250` is clamped into 1–100 by `CaptureEncoder.ClampJpegQuality`. It is not `invalid_argument`.
- Negative width is native size (`RequestedWidth > 0` is the "was set" test). It is not `invalid_argument`.
- `max_logs < 0` is `invalid_argument`. A non-integer `max_logs` is `invalid_argument`. `max_logs` 0 is allowed.
- Omitted `max_logs` is 50 (`GroupCompileErrorsCommand.DefaultMaxLogs`).
- These defaults were unified with the CLI in S-02. See section 6.

### 5.6 What stayed frozen

Reviewed on purpose so a planner does not "finish" S-04 by wrapping legacy tools:

- `CreateScreenshotResult` is unchanged.
- `capture_game_view_screenshot` stays PNG by default, full size when max edge is omitted, and returns the structured legacy body (`success`, `message`, `duration_ms`, `format`, `image_base64`).
- A missing Game View on the **legacy** path still throws and becomes JSON-RPC `-32000` with a stack. The canonical path returns an envelope with `unavailable` instead.
- `CreateErrorResponse` is unchanged.
- The bridge `call_unity` path does not set MCP `isError` from a canonical `ok: false`.
- `OpenSourceApiContractTests` catalog freeze still expects 120 tools.
- Raw `Execute()` DTO methods still exist (`ProjectMapCommand.Execute`, `GroupCompileErrorsCommand.Execute`). Pipeline and HTTP enveloped entry points call them and then wrap.
- P0-02 epochs were not modified by S-04.
- Capture still returns `Task` so Pipeline can await GPU completion. `NexusPipelineCommands.cs` must not contain `GetAwaiter().GetResult()`, `GetResult`, or the substring `.Result`. The current comment says "The task result is the canonical envelope." and does not contain `.Result`. `Task.FromResult` does not contain the substring `.Result`.
- A source test fails if a comment in that file contains `.Result`.
- Stale projectmem `evt_33bf51c5` says Pipeline capture no longer calls `GetResult` / `WaitForCompletion`. That is still true. Do not supersede it.

### 5.7 Envelope tests, by name

`Tests/Editor/NexusCommandEnvelopeTests.cs`, 29 tests, all passed on `aa923f6` and again on the S-02 tip:

- `SuccessEnvelope_ProjectMap_EchoesRequestIdAndGen`
- `SuccessEnvelope_AliasMatchesCanonicalId`
- `SuccessEnvelope_CompileErrorsAreSuccessWhenNotCompiling`
- `SuccessEnvelope_UnsavedSceneIsNotUnavailable`
- `InvalidArgument_UnsupportedFormat_BothNames`
- `InvalidArgument_BadMaxLogs_DoesNotCallLogs`
- `InvalidArgument_NonStringRequestId_SkipsHandler`
- `ClampedQualityAndNegativeWidth_AreNotInvalidArgument` (UnityTest coroutine)
- `Unavailable_MissingGameView_IsResultNotJsonRpcError` (UnityTest)
- `Unavailable_EmptySource_SameCodeDifferentMessage`
- `Busy_CaptureInProgress_IsNotPending`
- `Timeout_Readback_IsRetryableAndHasNoUnknownOutcome`
- `CaptureFailed_ReadbackError_IsNotTimeoutOrInternal`
- `Partial_CompilingErrorList_DoesNotTrustDtoSuccess`
- `HttpCompileResult_MatchesProjector`
- `EditorNotReady_DomainReload_IsRetryable`
- `Internal_UnexpectedException_RetryableFalse_NoStack`
- `DebugEnv_AddsStackOnlyWhenSet`
- `UnknownMutationOutcomeForcesRetryableFalse`
- `RequestIdOmitted_IsNull_NotJsonRpcId`
- `MetaShape_Types`
- `LegacyScreenshot_Success_HasNoEnvelope`
- `LegacyScreenshot_Failure_StaysFailedBody`
- `LegacyRawMethod_HasNoEnvelope`
- `ListTools_AdvertisesOptionalRequestIdOnCanonicalCommands`
- `SyncProcessJsonRpc_Capture_StaysTransportError`
- `CanonicalCaptureData_IsNotLegacyScreenshot`
- `CanonicalProjectMap_IsNotBareDto`
- `PipelineProjection_ReturnsEnvelopeType` (also invokes the Pipeline method when the assembly is loaded and compares it to HTTP after zeroing `ms` and `execution_duration_ms`)

Python added in this commit: `Editor/tests/test_canonical_envelope_passthrough.py`. Python `Editor/tests` after S-04: **47 passed**. Not re-run after S-07 or S-02, because those commits did not change Python.

Independent review at the time: semantic review found no must-fix. The note that `data.success` stays true while compiling is documented above. Compatibility review found the legacy freeze held.

---

## 6. S-07 — image hygiene and Scene View

**Status: committed** `23005b4`. Plan line: `nexus_look` image hygiene + Scene View. The command id was not renamed to `nexus_look`.

### 6.1 Agent-facing contract

Id: `nexus.capture_game_view`.

Unlisted aliases, same handler, not in `list_tools`: `nexus_look`, `nexus.look`. Both are in the open-source contract's dispatch-only alias list and in the registry "same handler" asserts. `list_tools` count stays 120.

There is no `output_path` argument and no `selector` argument. The caller does not choose the file path.

| Parameter | JSON type | Default when omitted | Notes |
|---|---|---|---|
| `width` | integer | 0 | 0 means native width. Negative is also treated as native, not as `invalid_argument` |
| `height` | integer | 0 | same for height |
| `format` | string | `jpg` | `png` is the other value. `jpeg` is accepted as JPEG. Anything else is `invalid_argument` |
| `quality` | integer | 85 | values outside 1–100 are clamped |
| `max_dimension` | integer | 768 | explicit 0 stays unconstrained, capped at 8192 inside `ResolveOutputSize`. Explicit 0 wins over `max_long_edge`. 1024 is an allowed opt-in, not the only other legal value. A non-integer is `invalid_argument` |
| `view` | string | `game` | `scene` is the other value |
| `target` | string | null | hierarchy path if the string contains `/`, otherwise a unique name. Empty or whitespace is `invalid_argument` |
| `instance_id` | integer | 0 | 0 means none. Negative scene ids are valid. Passing both `instance_id` and `target` is `invalid_argument` |
| `delivery` | string | `base64` | `file` writes one image and omits base64 |
| `request_id` | string | null | S-04 rule |

Help strings shipped on both HTTP schemas and CLI:

- width: "Target width in pixels (0 for native Game View width)."
- height: "Target height in pixels (0 for native Game View height)."
- format: "Image encoding format: 'jpg' (default) or 'png'. Only one encoding is returned."
- quality: "JPEG compression quality (1-100, default 85)."
- max dimension: "Longest edge in pixels. Defaults to 768. Pass 1024 to opt in. 0 keeps the source size up to 8192."
- view: "game (default) or scene. Scene View is framed on the target."
- target: "Hierarchy path or unique object name to crop or frame."
- instance id: "Unity instance id to crop or frame, including a negative scene-object id. Zero means none. Do not also pass target."
- delivery: "base64 (default) or file. File writes one image under Temp and omits base64."

`CaptureRequest` (the gateway DTO) still defaults to **PNG** and max edge **0**. The JPEG and 768 defaults are applied by the canonical command when the caller omits the fields. Do not move those defaults onto `CaptureRequest`, or the legacy screenshot path changes.

Legacy `capture_game_view_screenshot`:

- PNG by default
- omitted max edge stays 0 (full size, then the 8192 cap)
- `CreateScreenshotResult`
- no envelope
- parameters remain `format`, `quality` / `jpeg_quality`, `width`, `height`, `max_long_edge`, `include_telemetry`
- do not flip Game View again (issue #0019 was upside-down Overlay when the blit flip was removed)
- do not touch issue #0018 `ForceUpdate`

### 6.2 Success payload

`data` is a `NexusCaptureResult`. Fields:

- `success` (bool) — payload produced, not "the command's ok bit"
- `width`, `height` (int)
- `encoding` — `jpg` or `png`. There is no second encoding field
- `bytes` — compressed size
- `base64` — present for `delivery: base64`, omitted for `delivery: file`
- `file_path` — present for `delivery: file`, omitted for base64
- `source` — `game_view` or `scene_view`
- timings: `acquisition_ms`, `submit_ms`, `wait_ms`, `getdata_ms`, `encode_ms`, `base64_ms`, `main_thread_stall_ms`, `total_ms`, `editor_ticks_submit_to_done`

Never both PNG and JPEG. Never both `base64` and a second encoding. File mode omits `base64`.

File path pattern:

`{projectRoot}/Temp/NexusCapture/nexus-look-{yyyyMMddHHmmssfff}-{8 hex}.{jpg|png}`

`projectRoot` is `Path.GetFullPath(Path.Combine(Application.dataPath, ".."))`. It is not `Directory.GetCurrentDirectory()`. On the harness machine the Editor's cwd happened to already be the project, so an earlier draft looked correct until review. Empty bytes throw `CaptureSourceEmpty` ("Capture produced no image bytes.") which becomes `unavailable`.

The Capture folder is banned, by `CaptureGatewayTests.CaptureSourcesStayTransportIndependent`, from containing `System.Net`, `HttpListener`, `Python`, `Unity.Pipeline`, `RequestIntoNativeArray`, `Convert.ToBase64String`, and `JsonRpc`. File IO and Base64 stay in `Editor/Commands`.

### 6.3 Size rule

`ResolveOutputSize` was not rewritten. Omitted width and height do not upscale. An explicit requested width or height larger than the source can upscale. That pre-existed.

Expected long-edge behavior used by the tests:

| Source | Max long edge | Result |
|---|---|---|
| 1920×1080 | 768 | 768×432 |
| 2000×2000 | 768 | 768×768 |
| 1080×1920 | 768 | 432×768 |
| 2000×200 | 768 | 768×77 |
| 1920×1080 | 1024 | 1024×576 |
| 1920×1080 | 0 | 1920×1080 |

Token proxy used in notes: `width * height / 750`. It is a proxy, not a tokenizer.

Live measurements on this Editor (Scene View, not a 16:9 Game View):

- Final Aim path, default 768: **768×403 JPEG, 11109 bytes**, about 412 tokens by that proxy. Request id `s07-aim`. Source `scene_view`. Image showed an upright framed grey cube with the pale horizon band at the bottom. Local file `/tmp/s07-scene-look.jpg`. Not in git.
- An earlier 1024 shot, before the Aim rewrite: 1024×537, 16917 bytes, about 733 tokens.
- An earlier explicit 0 shot: 1584×831, 34062 bytes, about 1755 tokens (source size, unconstrained).
- The rejected presented-RT path: black 768×403 JPEG, 5619 bytes.

### 6.4 Game View path (unchanged on purpose)

Presented `GameView.m_RenderTexture` (reflected) → Nexus-owned copy via `Graphics.Blit` with scale `(1, -1)` and offset `(0, 1)` → `AsyncGPUReadback.Request` → poll on `EditorApplication.update` → `GetData<byte>()` → encode.

Do not use `RequestIntoNativeArray`, `WaitForCompletion`, `GetResult`, or `Thread.Sleep` on the main thread. Do not replace this with official `capture_game_view`. Overlay UI lives in the Game View render texture. Removing the flip makes Overlay text upside down on this Metal editor (`graphicsUVStartsAtTop` is true). The flip stays even if another GPU might not need it.

Full-frame crop scale is `(1, -1)`, offset `(0, 1)`. `CropScale` computes partial crops with the same Y direction. `CopyToOwned` delegates to the cropped copy of the full rect. Minimum crop is 4 pixels on either edge (`CaptureFraming.MinimumCropPixels`). Smaller or empty is `TargetNotVisible`.

Game View crop assumes `Camera.main` maps linearly onto the presented render texture. A camera stack, letterbox, or a different canvas camera can crop wrong. Partial crops are covered by UV tests only. Overlay crop was **not** pixel-verified. `Screen.width` may not match the Game View render texture size.

### 6.5 Scene View path (the one that shipped)

Synchronous. No `EditorApplication.update` await. No GPU readback inside the framing call. The gateway's scene branch calls `SceneViewCaptureSource.Capture` and then the existing encode path.

Steps:

1. `SceneView.lastActiveSceneView`. If the view or its camera is null: `SceneViewUnavailable` ("No active Scene View is open.") → envelope `unavailable`, retryable.
2. If a domain reload is in progress: `DomainReloadInterrupted`.
3. Save pose: pivot, rotation, size, orthographic, `in2DMode`.
4. `view.Frame(CaptureFraming.WorldBounds(target), true)`.
5. `Aim` (below). This replaces a reflection call to private `SetupCamera`.
6. `Camera.Render` into a temporary `R8G8B8A8_SRGB` texture, depth 24, sized by `ResolveOutputSize` of `camera.pixelWidth/Height`.
7. `GameViewCaptureSource.CopyToOwned` at the same size, which applies the Capture V2 flip. Do not skip the flip. On this Metal editor a double flip did not happen.
8. `RenderTexture.ReleaseTemporary` on the raw texture.
9. Restore the pose and `Repaint`, in `finally`.

Aim formula:

- perspective distance = `view.size / sin(fov * 0.5 * Deg2Rad)`, with fov at least 1
- orthographic distance = `view.size`, and `camera.orthographicSize = view.size`
- `camera.rotation = view.rotation`
- `camera.position = view.pivot - view.rotation * Vector3.forward * distance`

Bounds: enabled renderers in children, else `RectTransform` world corners, else a 1 m box at the transform.

### 6.6 Scene paths that were tried and rejected

Do not go back to these without a new proof:

1. Copy `SceneView.m_SceneTargetTexture`. The field exists on `UnityEditor.SceneView` and its type is `RenderTexture`. Sampling it produced a black 768×403 JPEG (5619 bytes) because the presented target was read before the camera drew.
2. `Frame`, `Repaint`, await one `EditorApplication.update`, then `Camera.Render`. The live Pipeline capture of that path did produce an upright framed cube. The EditMode `[UnityTest]` that awaited the task inside Pipeline's sync test runner hung until the runner timeout (first 300 s, then a 60 s cancel). The single HTTP command slot stayed occupied after cancel. `editor_status` then hung while the main thread was still ticking `EditorApplication.update`. The main thread was not stuck inside `Camera.Render`. Bringing Unity frontmost did not unstick HTTP. Recovery that worked: macOS System Events clicking the menu item Refresh under the Assets menu, which domain-reloads and logs `Start HTTP server: port:7800`. Do not kill the Editor to recover. Do not call Refresh over HTTP while the slot is stuck, because HTTP is the thing that is stuck.
3. Reflection invoke of private parameterless `SceneView.SetupCamera()`. It exists, and so do `CalcCameraDist`, `LookAtDirect`, and `PrepareCameraTargetTexture`. Called after `Frame`, `SetupCamera` threw `NullReferenceException`, which became envelope `internal`, retryable false. Do not call `SetupCamera` outside `OnGUI`.

The first CaptureLook 10/10, before `Camera.Render`, is not proof of the render path. It either took the missing-Scene-View branch, the presented-RT path, or failed earlier. The 10/10 that counts is the later run: suite elapsed about 0.5 s, scene test about 0.004 s, calling `SceneViewCaptureSource.Capture` directly. That test does not encode a JPEG and does not check pixels. The live 11109-byte JPEG is the visual proof.

The scene test creates a cube, and if a Scene View exists it captures and asserts the longest edge is in `(1, 768]` and that pivot xyz and size are restored within 0.001. It then releases the texture and destroys the cube. If no Scene View exists it calls `ExecuteAsync` and asserts the task is already completed with `ok: false`, `unavailable`, retryable true. On the harness a Scene View had been opened, so the capture branch ran.

### 6.7 Target resolution

Done on the main thread.

- Instance id: `EditorUtility.InstanceIDToObject`. This is obsolete (`CS0618`) and still works. `EntityIdToObject` / `GetRawId` were not adopted. The package elsewhere uses `GetRawId()` / `IdToObject`. Tests pass `GetInstanceID()`, so they do not prove an id copied from a hierarchy snapshot or from `EntityId`.
- Unity scene object ids are **negative**. Rejecting `instance_id < 0` at parse was a product bug. The parser accepts any int. `0` means none. A missing negative id fails later as `TargetNotFound` → `invalid_argument`, not at parse time.
- Live ids used while proving the cube: −19744, then −20834. The cube was named `nexus-look-evidence-cube`. It was created, captured, and deleted. `find_gameobjects` count was 0 afterward. Do not leave a cube. Do not save the scene. Creating and deleting it dirtied the scene.
- A component instance id fails `TargetNotFound`. `ById` only accepts a `GameObject`.
- Name: if the string contains `/` it is a hierarchy path. Otherwise it must be a unique name. More than one match → `TargetNotFound` with a "more than one object" message → `invalid_argument`. Persistent assets are skipped.
- Disabled (`!activeInHierarchy`) → `TargetDisabled` → `unavailable`, retryable.
- Game-view crop off-screen or under 4 px → `TargetNotVisible` → `unavailable`, retryable.
- Scene view requires a target (id or name). Game view crop is optional; omitted target captures the full frame.

### 6.8 CaptureLook tests, by name

All 10 passed on the committed code:

| Test | What it locks |
|---|---|
| `FullFrameCropUsesTheOverlayFlip` | full-frame blit is the Overlay flip |
| `PartialCropKeepsTheSameFlipDirection` | partial crop keeps the same Y sign |
| `CropOutsideTheFrameIsNotVisible` | off-screen crop is `TargetNotVisible` |
| `TinyCropIsNotVisible` | under 4 px is `TargetNotVisible` |
| `DefaultLongEdgeFitsTheTokenBudgetAndDoesNotUpscale` | the size table above, no upscale when width/height are omitted |
| `OmittedMaxEdgeIs768AndExplicitZeroStaysUnconstrained` | omit → 768, explicit 0 stays source size |
| `MalformedLookArgumentsAreNotRetryable` | bad view, bad delivery, empty target, both selectors |
| `FileDeliveryWritesOneEncoding` | one file under the project Temp, the other encoding absent |
| `DisabledAndUnknownTargetsFailBeforeCapture` | disabled is retryable unavailable; unknown is not retryable |
| `SceneCaptureFramesATargetAndRestoresTheView` | frame, size ≤ 768, pose restored. No JPEG encode |

Also re-run on that tree: `CaptureGatewayTests` 10/10, envelope 29/29, registry 9/9, open-source contract 6/6.

Registry tests that mention the new aliases:

- `CanonicalIdAndAliasShareTheSameHandlerInstance` includes `LookAlias` and `LookDotAlias`
- `VisualProfileListsCanonicalCaptureOnly` asserts those aliases are not listed
- `ListToolsProjectsSharedDescriptionsAndKeepsLegacyScreenshot` still requires the legacy screenshot tool

### 6.9 Capture debt (do not bury inside the next task)

- `base64_ms` starts before the gateway await, so it includes the whole capture, and `total_ms` adds it again. Research scripts read `base64_ms`. Left unchanged on purpose.
- `DriverOwnedReadback` timeout (8 s) and the 60 s abandon, and a domain reload while a readback is pending, do not `ReleaseTemporary`. Pre-existing. The scene path does release its own raw texture after `CopyToOwned`. The Game View source render texture is never released by Nexus.
- UI / Overlay crop is not pixel-verified.
- `InstanceIDToObject` obsolete warning remains. Console after the S-07 compile: 0 errors, 2 warnings, one of them CS0618.
- 2D mode: the rotation setter ignores writes while `Frame` can assign `m_Rotation`. The test checks pivot and size only.
- Component instance ids are `TargetNotFound`.
- The scene EditMode test does not encode a JPEG or inspect pixels.

---

## 7. S-02 — CLI parameters locked to the descriptor

**Status: committed** `bfdd0f7`. This is plan task S-02: hand-written `[CliCommand]` wrappers plus a drift test. No generator script.

The wrappers already existed before this commit. The new work is the shared default constants, the descriptor fields `HasDefault` and `DefaultValue`, and the EditMode test that fails if one side adds, removes, reorders, retypes, rewrites, or retargets a parameter.

### 7.1 What is compared

Reflection over every `[CliCommand]` in assembly `UnityMCP.Editor.Pipeline`, including non-public methods. Today that is exactly three methods, all on `NexusPipelineCommands`.

For each:

- CLI command name equals the descriptor's **first** alias (`nexus_project_map`, `nexus_group_compile_errors`, `nexus_capture_game_view`). Matching "any alias" would let `nexus_look` become the CLI name. That must fail.
- CLI command description equals `descriptor.Description`.
- Parameter count and order match.
- Each parameter has exactly one `[CliArg]`. A missing attribute must fail even if the C# identifier already equals the wire name (`requestId` is not `request_id`). Pipeline would otherwise fall back to the C# name and the description "Parameter: {name}".
- `CliArg.Name` equals the descriptor name.
- `CliArg.Description` equals the descriptor description.
- `CliArg.Required` is false.
- JSON type maps to an exact CLR type: `integer` → `int` only (not `long`), `string` → `string`, `boolean` → `bool`, `number` → `float` only (not `double`). Pipeline help prints `ParameterType.Name`, so `int` versus `long` changes the help even though both could be called "integer".
- The descriptor declares `HasDefault`.
- The C# parameter `HasDefaultValue` is true. Pipeline 0.7 uses `ParameterInfo.DefaultValue` when that bit is set, and ignores `CliArg.DefaultValue`. The test compares the C# default to `descriptor.DefaultValue`. It does not require `CliArg.DefaultValue` to be set. `DBNull` is treated as null, and only after `HasDefaultValue` is true.

Closed set of CLI names: the three aliases above. `nexus_look` and `nexus.look` are not CLI commands.

Tags (`nexus` / `context`, `nexus` / `diagnostics`, `nexus` / `capture`) are not compared to descriptor profiles (`core`, `visual`). They are different fields.

### 7.2 Defaults that are now one constant

| Command | Parameter | Default |
|---|---|---|
| project map | `request_id` | null |
| compile errors | `max_logs` | `GroupCompileErrorsCommand.DefaultMaxLogs` = 50 |
| compile errors | `request_id` | null |
| capture | `width` | 0 |
| capture | `height` | 0 |
| capture | `format` | `CaptureGameViewCommand.DefaultFormat` = `jpg` |
| capture | `quality` | `DefaultJpegQuality` = 85 |
| capture | `max_dimension` | `DefaultMaxLongEdge` = 768 |
| capture | `view` | `DefaultView` = `game` |
| capture | `target` | null |
| capture | `instance_id` | 0 |
| capture | `delivery` | `DefaultDelivery` = `base64` |
| capture | `request_id` | null |

HTTP omit path uses the same constants:

- `quality ?? DefaultJpegQuality`
- omitted max edge → `DefaultMaxLongEdge` (explicit 0 stays 0)
- empty view → `DefaultView`, and `DefaultView` is also the accepted token for the game branch
- empty delivery → `DefaultDelivery`, and `DefaultDelivery` is also the accepted token alongside `"file"`
- `maxLogs ?? DefaultMaxLogs`
- format: `TryParse` of `{}` leaves `call.Format` null. `FormatOrDefault` applies `DefaultFormat` both when the CLI builds the JSON and when `ExecuteEnveloped` parses. `FormatOrDefault(null)` is `jpg`.

`GroupCompileErrorsCommand.Execute(int maxLogs = DefaultMaxLogs)` uses the same 50. A direct `Execute()` call used to hardcode 50 separately. The drift test reflects that parameter default and compares it to the descriptor.

`list_tools` still writes only `type` and `description` for canonical properties. The drift test fails if a `default` key appears. Publishing defaults into the MCP schema was judged out of S-02 scope.

The test assembly `UnityMCP.Editor.Tests` does not reference `Unity.Pipeline` or `UnityMCP.Editor.Pipeline`. The test reflects them. `UnityMCP.Editor.asmdef` also must not reference `Unity.Pipeline`. If `CliCommandAttribute` is not loaded, the drift test `Assert.Ignore`s. On Pipeline 0.7 (`com.unity.pipeline@ab05e9cd7f74`) the attribute is in assembly `Unity.Pipeline`. A future package that moves it to `Unity.Pipeline.Attributes` would ignore instead of fail. That limitation is accepted, not hidden.

Pinned numbers inside the HTTP test, so a shared-constant edit cannot silently change the advertised help:

- format `jpg`, quality `85`, max edge `768`, view `game`, delivery `base64`, `max_logs` `50`, width/height/instance id `0`, target and request id null.

### 7.3 Drift tests, by name

All 3 passed on the committed tree:

- `CliArgsMatchCanonicalDescriptors`
- `OmittedHttpArgumentsUseTheSameDefaults` — parses empty capture JSON and compares the resulting request to the descriptor; also compares an omitted HTTP compile-error call with `ExecuteEnveloped(50)` after zeroing `meta.ms` and `data.execution_duration_ms`
- `CanonicalSchemasStayFreeOfCliDefaultFields` — test asmdef has no Pipeline reference; `list_tools` count is 120; canonical properties have no `default` key

`NexusCommandEnvelopeTests` 29/29 was re-run after this commit, including `CanonicalCaptureData_IsNotLegacyScreenshot`, which now asserts `DefaultFormat == "jpg"`, `FormatOrDefault(null) == "jpg"`, and that the capture source contains `FormatOrDefault(`.

Registry 9/9, open-source 6/6, and CaptureLook 10/10 were run on the tree immediately before `FormatOrDefault` was extracted. That extraction did not change behavior. The envelope suite was re-run after it.

### 7.4 Quality gate

Command, from the package root:

`dotnet run --project tools~/NexusQualityGate/NexusQualityGate.csproj -- --root . --ai off --format github`

Result after the final S-02 edit, and also after S-07: **0 errors, 5 warnings**. The warnings pre-exist and were not introduced by this sprint:

- `Editor/MCPServerMethods.HighValue.Screenshots.cs` line 22, `CaptureInspectorScreenshot`, 52 lines (warn threshold 50)
- same file line 75, `TryCaptureVisualElement`, 75 lines
- `Editor/MCPServerMethods.Tools.cs`, 328 lines (warn threshold 300, error 450)
- `Editor/MCPServerMethods.cs`, 313 lines
- `Editor/Runtime/NexusRuntimeCapabilities.cs`, 343 lines

Rules the gate enforces, for the next planner: files over 450 lines error, over 300 warn. Methods over 150 lines error, over 50 warn. Public types and public methods need an XML `<summary>` of at least 18 characters. Private and internal methods do not. A nested type's private members are invisible to the enclosing type (`CS0122`); use `internal`. NQG120 warns when a member's code contains `File.`, `AssetDatabase.`, `EditorPrefs.`, or `SessionState.` and the summary lacks a side-effect word. The quality gate ignores `Tests/`.

`scripts/prepush-validate.sh --static-only` also checks that tracked C# files have tracked `.meta` files. New files in this sprint have metas. Do not run that script by deleting historical or untracked graphify files.

---

## 8. Reviews, and which findings changed code

### 8.1 P0-02

An independent lifecycle review and an evidence-harness review ran before the 20. The must-fix race (a newer script write settling on an older compile) became `6fff2e8`, with the four EditMode tests listed in section 4.6. The judge's early-refresh false pass was fixed in `judge.py` inside `060cc13` and re-applied to the same logs. It was not a new Editor run.

### 8.2 S-04

Semantic review: no must-fix. Notes kept: `data.success` stays true while compiling; a readback message containing "pending" is not a code. Compatibility review: legacy freeze held, no findings.

### 8.3 S-07

Agents before implementation mapped capture paths, payload size, and adversarial cases. Those were folded into the design (one encoding, 768, crop, scene frame, file under project Temp).

Independent review after the first scene implementation: no must-fix. It read an earlier draft that awaited one editor tick, so line references to "wait one EditorApplication.update" are stale. The contract notes that still hold:

- file path must be the project root (fixed before commit)
- `InstanceIDToObject` is obsolete (left)
- component ids fail `TargetNotFound` (left)
- 2D rotation restore is incomplete (left)
- Game View crop assumes `Camera.main` (left, documented)
- README and DOCUMENTATION name `nexus_look`; API_REFERENCE and CHANGELOG also name `nexus.look` (both are true; the dotted alias is unlisted)

### 8.4 S-02

Design review (read-only) required: exact CLR type, scan the whole Pipeline assembly, do not publish defaults into `list_tools`, do not treat a missing `[CliArg]` as success, do not accept any alias as the CLI name. Those are in the committed test.

Diff review (read-only) found no must-fix on the first draft and one should-fix: HTTP omit could keep an old literal while the CLI default and the descriptor moved together. Fixed before commit by `FormatOrDefault`, by comparing empty-argument HTTP results to the descriptor, and by pointing `Execute`'s default at `DefaultMaxLogs`. Tests were re-run. There was no second reviewer pass on that last delta.

Pre-commit `pjm` warnings on these commits were churn and stale memories. They are not test failures. Do not supersede them, in particular:

- `evt_33bf51c5` — Pipeline capture does not call `GetResult` / `WaitForCompletion` on the main thread. Still true.
- security memories on `delete_asset` (`evt_6b4dc360`, `evt_14c39322`, `evt_f24a5476`)
- older changelog memories (`evt_69c8153a`, `evt_b31382ba`, `evt_56400bb2`)
- `evt_cf79241b` on `ProjectMapCommand`

Project memory writes, when used, go through the local `pjm` CLI from the package root. The projectmem MCP server has been observed bound to a different repository. Do not edit `.projectmem` files by hand. Do not call projectmem MCP write tools unless the server is confirmed bound to this package.

---

## 9. What a later plan may schedule

v2.6 work classification says the following may continue without an architecture decision: job model (L-02), bounded output, token budgets and handles (C-04, S-06), legacy-noise cleanup (S-10), discovery metadata (S-11), Pipeline compatibility (P0-06, A-01), skills (S-01), `nexus_snapshot` (L-03), `nexus_diagnose` (S-06), `nexus_compile` (S-05), `nexus_run` (L-04), and the existing benchmark infrastructure.

v2.6 says these are **not** scheduled as production until Gate F: a full Nexus-primary façade, broad Unity write delegation, a generated domain-manager façade, replacing Unity primitives with Nexus actions, a fully hidden-Unity distribution, a large routing layer, and any production escape / `invoke` used only by S6.

### 9.1 Finished, do not re-open unless a real bug appears

| Id | State | Commit |
|---|---|---|
| P0-02 | Accepted 20/20 | product `6fff2e8`, evidence `060cc13` |
| S-04 | Envelope on the three commands | `aa923f6` |
| S-07 | JPEG 768, scene frame, file delivery | `23005b4` |
| S-02 | CLI drift lock | `bfdd0f7` |

### 9.2 Not started, with the dependency that matters

| Task | Plan size | Dependency after this sprint | Planning note |
|---|---|---|---|
| S-01 skill pack | 3d | no code dependency | Needs a public listing (skills.sh, Claude/Codex). Not a code slice. `nexus doctor` in that task means `nexus_status detail:"doctor"`, and `nexus_status` does not exist yet |
| S-03 bridge profiles | 3d | none stated | Changes the default agent surface (`core` ≤ 8 tools, ≤ 2.5k tokens). Multi-day |
| S-05 `nexus_compile` | 3d | **P0-02 is met** | The next composite the plan lists whose dependency is actually satisfied. Still 3 days. In pipeline mode it must obtain compile truth from `unity recompile` and only add grouping, hints, and ±3 lines of context. It replaces scraping inside `GroupCompileErrorsCommand`. Persist messages in SessionState across reload. `no_change` under 50 ms is allowed only if P0-01b has landed; otherwise report the measured time. Hot reload: detect and warn, do not claim the hot-patched state. Sandboxed writes in that task are legacy-only in the Pipeline projection. Do not start it unless it can be finished, tested, reviewed, and committed. It is not a substitute for unfinished capture or envelope work; those are finished |
| S-06 `nexus_diagnose` + `nexus_get` | 3d | handles, capabilities map | Safe Mode path is `Editor.log` + asmdefs + last persisted compile result, because compile errors can prevent Pipeline from loading. Fixture target: ≤ 15 line report. Path-like handles must be rejected. 3 days |
| S-08 testers | 0.5d | none | Not code. Needs the owner's contacts. No in-product telemetry |
| S-09 hooks | 3d | wants S-05 and S-06 | Those commands do not exist. Fail-open when no Editor is reachable |
| S-10 legacy noise | 2d | plan text says `replacement: nexus_snapshot` | **L-03 does not exist.** Do not mark `unity_dump_scene_graph` or unbounded `read_logs` as replaced by a command that is not shipped. Bounding them without that replacement string is a separate product decision the sprint did not make. The pilot fact being cleaned up: 12 S4 outputs over 40k characters came from `unity_dump_scene_graph` |
| S-11 discovery metadata | 1.5d | none for the text change | Acceptance is an offline retrieval rerun (pilot recall@8 moved from .55 to .88 when action lists were added to descriptions). This sprint was told not to spend itself on another S4/S5/S6 benchmark. S4, the frozen baseline, must not absorb S-10/S-11. S4+ is the arm that carries them |
| L-01 seams | 3d | none | `IMainThreadDispatcher`, `IConsoleLogSource`, `IUnityPrimitives`. Acceptance is a grep test that Core files contain no `MCPServer.` references. L-03 depends on it. Not started |
| L-02 job store | 3d | none, but the acceptance is a 20/20 | `NexusJobStore`, step cursor in SessionState, `[InitializeOnLoad]` resume, idempotent steps, size cap, `nexus_job_status` / `wait` / `cancel`. A job must survive a forced script reload **and** a play-entry reload, 20/20 consecutive on the final code. `interrupted` plus partial artifacts only after a simulated Editor restart. Unity's own jobs do not survive reload. Do not start without budget for a full consecutive 20 after the last fix. Same discipline as P0-02 |
| L-03 `nexus_snapshot` | 3d | **L-01 and S-07**. S-07 is met. L-01 is not | Do not build it to manufacture S6 research. Summary budget in the plan is ≤ 1k tokens. It may include an optional cropped image, which is why it depends on S-07 |
| L-04 `nexus_run` | 3d | **L-02** | Allowlisted menu item or `[NexusRunnable]`. Arbitrary code stays `unity eval`. If the main thread stops ticking, return `PROBABLE_MODAL_DIALOG`. Native folder panels cannot be intercepted |
| C-04 `detail` / `max_tokens` | 2d | the three canonical commands exist | Plan acceptance is a bench token column per command. S-02 explicitly did not put defaults into `list_tools`. A `detail` field would be a new schema. Not started |
| P0-17, P0-16c, Gate F | research | not production | Do not implement. Stage A2 waits until S-05, S-06, L-03, and L-04 are merged. Building those commands in order to unlock a benchmark is the thing the sprint was told not to do |
| V-02b and the verify track | various | Gate A2, and L-02 for the runner | Not in scope |

### 9.3 Suggested next production task

If the owner wants more code before an architecture decision, **S-05** is the plan task whose stated dependency (P0-02) is actually met and whose output (compile truth with positions) is what later tasks scrape around. It is still a 3-day task with a fixture acceptance (exactly 3 errors, with file/line/col). Do not open it as a partial commit.

S-02 was done out of numeric order only because the three wrappers already existed and the missing piece was the drift test. That does not reorder the plan. S-01 and S-03 are still open and earlier on the Phase 1 list. S-01 is external publishing. S-03 changes the default tool surface.

---

## 10. Harness facts the evidence depends on

### 10.1 Editor

- One Editor only. PID **4465**. Unity **6000.3.16f1**. Project `UnityTestForNexus`.
- Do not start a second Editor. Do not run `Unity -runTests` (that launches another Editor).
- macOS, Metal. `SystemInfo.graphicsUVStartsAtTop` is true. The Capture V2 flip is still required here.
- Pipeline package in the harness cache: `com.unity.pipeline@ab05e9cd7f74`, experimental 0.7.0-exp.1.
- Persistent exec: `POST http://127.0.0.1:7800/api/exec` with JSON `{"command","parameters"}` and `Authorization: Bearer` from the harness file `Library/Pipeline/.unity-pipeline-port`, field `evalToken`. Do not print that token. Do not print the Unity process command line; it contains `accessToken`.
- Nexus Legacy HTTP port 8081 was **not** listening during this sprint. Do not assume it is up.
- Catalog: `GET /api/commands?limit=500` with the same bearer.
- Domain reload drops the port for a moment. The port stayed 7800. Re-read the port file anyway. Wait until `editor_status` says `status: ready`, `compiling: false`, `domainReloadInProgress: false`, and `recompile_status` says `failed: false` with an empty error list.
- `recompile_status.result` is a JSON **string**, not an object. A stale "completed" from the previous compile can still be sitting there if the new file was not imported. New C# files need `Assets/Refresh` (the `menu` command with path `Assets/Refresh`) before tests see them.
- `run_tests` arguments: `mode: editor`, `filter_type: testName`, `filter` is a case-insensitive **partial** match, not a regex. Do not pass `A|B`. Run filters separately. Unity's default test timeout is 300 s. The driver used a client timeout around 120–150 s.
- Tests that passed quickly and did not await a later editor tick are safe on this runner. Tests that await `EditorApplication.update` or GPU readback inside `run_tests` can wedge the single HTTP slot. See section 6.6 for the recovery. Do not save the scene. Do not revert the scene (it may contain unrelated user work). The capture cube was deleted. The scene may still be dirty from that create/delete.

### 10.2 Assemblies

- `UnityMCP.Editor` must not reference `Unity.Pipeline` or `HttpListener`. Domain handlers must not reference them either.
- `UnityMCP.Editor.Pipeline` references both, and it compiles only when `NEXUS_HAS_PIPELINE` is set (`versionDefines` on `com.unity.pipeline` `0.0.0-exp`).
- `UnityMCP.Editor.Tests` references `UnityMCP.Editor` only, with `overrideReferences` for `Newtonsoft.Json.dll` and `nunit.framework.dll`. Define constraint `UNITY_INCLUDE_TESTS`. `InternalsVisibleTo("UnityMCP.Editor.Tests")` is on the editor assembly.
- Pipeline CLI methods must be static. Capture's method sets `MainThreadRequired = true` and returns `Task<NexusCommandEnvelope>`. The other two return the envelope directly. `CancellationToken.None` is passed from the CLI wrapper. The HTTP path passes the real token into capture so GPU readback can be aborted.

### 10.3 Commands a planner will see in the catalog

Canonical, enveloped:

- `nexus_project_map` — tags `nexus`, `context` — one optional `request_id`
- `nexus_group_compile_errors` — tags `nexus`, `diagnostics` — `max_logs` default 50, `request_id`
- `nexus_capture_game_view` — tags `nexus`, `capture` — the ten parameters in section 7.2

Not canonical, still present, still legacy:

- `capture_game_view_screenshot` — PNG structured result
- the rest of the 117 raw tools

Runtime selection was not changed by this sprint. Default requested mode remains `auto`. Wire names remain `pipeline` / `auto` / `legacy`. Explicit legacy still wins. Explicit pipeline does not fall back to HTTP. `meta.mode` on the new envelope reports the **effective** mode, so a caller in auto mode sees `legacy` or `pipeline`, never `auto`.

### 10.4 Project memory and docs

- Local `pjm` CLI from the package root. Do not install git hooks with `pjm hooks install` (this package uses `.githooks` wrappers).
- #0013 is already recorded fixed. Do not record it again.
- #0012 is not fixed.
- #0018 (`ForceUpdate`) was not touched.
- #0019 (Overlay upside down if the blit flip is removed) is why the flip stays.
- Package docs updated only for behavior that shipped: changelog bullets under 1.7.0 Unreleased for the readiness predicate, the envelope, the JPEG/scene/file capture, and the CLI drift lock.

---

## 11. Sub-agents in this sprint

They analyzed and reviewed. They did not commit. They did not drive the Editor. One production owner edited the tree.

| When | Role | Result that changed code or tests |
|---|---|---|
| P0-02 | Lifecycle / race review | Overlapping script write, focus wait, failed-compile probe, play frames. Fixed in `6fff2e8` |
| P0-02 | Evidence harness review | Judge must use the forced domain-reload refresh, not the earlier no-update refresh. Fixed in `judge.py` inside `060cc13` |
| S-04 | Result-contract, compatibility, and adversarial test design | Envelope codes and the legacy freeze. No must-fix after implementation |
| S-07 | Capture archaeology, payload size, adversarial cases | JPEG 768, one encoding, crop, scene frame, file under project Temp |
| S-07 | Independent review after the first scene draft | No must-fix. Project-root file path fixed before `23005b4`. Tick-wait notes in that review are stale |
| S-02 design | Drift-test contract | Exact CLR type, whole-assembly scan, no `default` key in `list_tools`, `[CliArg]` required, primary alias only |
| S-02 diff review | Semantic / false-pass | Should-fix applied before `bfdd0f7`: HTTP omit bound to the descriptor, `FormatOrDefault`, `Execute` default |

---

## 12. Test inventory (final, not mixed across commits)

Separate the layers. Do not add them into one "all green" number.

**EditMode on `6fff2e8` (P0-02 acceptance build):**

- `EditorReadinessTests` 17/17
- Python `Editor/tests` 45 passed
- Quality gate 0 errors, 5 pre-existing warnings

**EditMode on `aa923f6` (S-04):**

- `NexusCommandEnvelopeTests` 29/29
- registry and open-source contract suites were part of the S-04 run (open-source 6)
- Python `Editor/tests` 47 passed (the new file is `test_canonical_envelope_passthrough.py`)
- Quality gate 0 errors

**EditMode on `23005b4` (S-07), after the Aim rewrite and the synchronous scene test:**

- `CaptureLookTests` 10/10 in about 0.5 s (scene test about 0.004 s)
- `CaptureGatewayTests` 10/10
- `NexusCommandEnvelopeTests` 29/29
- `NexusCommandRegistryTests` 9/9
- `OpenSourceApiContractTests` 6/6
- Quality gate 0 errors, 5 warnings
- Python not re-run

**EditMode on `bfdd0f7` (S-02 tip):**

- `NexusCommandCliDriftTests` 3/3
- `NexusCommandEnvelopeTests` 29/29 re-run after the commit
- Registry 9/9, open-source 6/6, CaptureLook 10/10 ran on the tree just before `FormatOrDefault` was extracted. Behavior did not change. The one envelope test that reads the capture source was included in the 29.
- Quality gate re-run after the final edit: 0 errors, 5 warnings
- Python not re-run

**Editor acceptance, not a unit suite:**

- P0-02 live 20/20 on `6fff2e8`, judge exit 0, artifacts in `060cc13`
- S-07 live scene JPEG: 768×403, 11109 bytes, upright, not in git

There is no single command that was run as "the whole package suite" at the tip. Historical `Tests~/Editor` was merged earlier into `Tests/Editor` and is not a second suite.

---

## 13. Explicit non-claims

- Secondary P0-02 stress was not run live.
- #0012 is open.
- #0013 was recorded once, after the judge passed, and must not be recorded again.
- The v2.6 plan's review issues were not line-audited. This sprint did not rewrite the plan.
- S-05, S-06, S-10, S-11, L-01, L-02, L-03, L-04, and C-04 were not started.
- No façade, no write delegation, no eval forwarding, no Gate F implementation.
- Python 47 is an S-04 result. It is not a fresh run of the S-07 or S-02 trees.
- The scene EditMode test does not prove pixel contents. The live JPEG does, for one cube, on one Metal editor, at 768×403.
- Warm P0-02 timings are not the cold 7.5–8.5 s figure.
- `list_tools` does not advertise parameter defaults.
- `meta.gen` is not a compile or reload epoch.
- `busy` is not a job.
- There is no `pending` error code.
- File delivery does not accept a caller path.
- `nexus_look` is an alias, not a new command id.
- The acceptance driver and stimulus script are deleted on purpose.
- Graphify files are untracked on purpose.
- Nothing was pushed.
