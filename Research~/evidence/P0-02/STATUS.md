# P0-02 evidence status

Date: 2026-10-04
Branch: `rework2/P0-02`
Plan: `NexusFeature~/MASTER_PLAN_V2.md` v2.6, task P0-02

## Acceptance criterion

Write → compile, 20/20 runs, report ready only after domain reload.
Wall time matches Editor.log (about 7.5–8.5 s on the harness), not the old ~99 ms false-ready.

## Status: NOT MET

The 20/20 Editor.log comparison was not run in this session.
Do not treat this file as acceptance.

## EditMode tests

Ran in the already-open Unity `6000.3.16f1` Editor (project `UnityTestForNexus`, PID 78398) via the Test Runner API. No second Editor was started.

`UnityMCP.Editor.Tests.EditorReadinessTests`: 13 passed, 0 failed, 0 skipped.

The first run failed `PendingRefresh_IsNotReadyUntilRefreshSettles` (ready became true after one quiet tick). The predicate now keeps an invoked pending refresh blocked until the latch settles. The rerun passed.

Python `Editor/tests` (`unittest discover`): 45 passed.

Nexus quality gate before the pending-refresh fix: 0 errors, 5 pre-existing warnings.

## What was implemented

- One readiness predicate in `Editor/Readiness/EditorReadiness.cs`, shared by `get_server_status`, `initialize`, `wait_for_ready`, `is_editor_idle`, and `wait_for_editor_idle`. `wait_for_ready` still returns a boolean.
- `compile_epoch` and `reload_epoch` are stored in `SessionState`.
- A successful script compile stays not-ready until `reload_epoch` advances, including the gap after `compilationFinished` and before `beforeAssemblyReload`.
- A failed compile (`EditorUtility.scriptCompilationFailed`) settles only after two quiet ticks, with no reload.
- A refresh that never compiles settles as `already_ready`.
- A focus-wait longer than 15 seconds still calls `AssetDatabase.Refresh` so the script-write latch is not left stuck.
- `wait_for_editor_idle` returns `{ready, epoch, reason, status}` and returns `Reloading` when a reload is armed. It does not sleep across the reload.
- `unity_wait` condition `compilation` re-polls `is_editor_idle`. A dropped connection is not success. An unchanged epoch after `script_write` is not success. `already_ready` covers a refresh with nothing to compile.

## What remains

1. On the harness, 20 script writes that force a compile and domain reload.
2. For each run, record the wait result time, `epoch.reload` before and after, and the Editor.log reload timestamp.
3. Pass only if all 20 become ready after the reload epoch advances and none return ready in under a second while Editor.log shows a multi-second compile.
