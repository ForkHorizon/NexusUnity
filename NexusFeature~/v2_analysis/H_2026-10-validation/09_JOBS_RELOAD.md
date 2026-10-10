# 09 — Jobs, reload, and whether L-02 is still needed

Date: 2026-10-04. Agent H. Read-only. No L-02 code, no Editor, no port 7800.

**Recommendation: WAIT.** Do not build `NexusJobStore` now. Script compile still unloads the Mono domain, and this harness still reloads the domain on play entry, so the failure is real. Nothing scheduled before L-04 / V-02b needs a general step cursor, and Pipeline 0.7 already has the receipt pattern for the commands that cross a reload. A general store with no caller would copy the wrong Unity object (`/api/job`) and would be tested against an acceptance line that 6.6+ new projects no longer satisfy.

Labels: **CONFIRMED** = this package cache, this clone, or this `ProjectSettings` file. **DOCUMENTED** = Unity manual or a public repo page fetched this session. **MEASURED** = a live Editor observation already recorded by the sprint (not re-run here). **INFERRED** = follows from those, not executed. **UNKNOWN** = not in source or docs seen here.

Competitor teardowns `03` / `04` / `05_COMPETITOR_*.md` were not in this folder at write time. Not blocked on them. Coplay and akiojin were not cloned under `/tmp`. Funplay was: `/tmp/funplay-unity-mcp`.

## What the plan actually says

L-02, quoted (`NexusFeature~/MASTER_PLAN_V2.md`):

> **L-02 · Resumable job store** (3d): `NexusJobStore` with step cursor in SessionState + `[InitializeOnLoad]` resume, idempotent step contract, size/retention cap, `nexus_job_status/wait/cancel`. E: a job that crosses a forced `RequestScriptReload` **and** a play-entry reload completes 20/20; `interrupted` + partial artifacts only after a simulated Editor restart.

P0-13, the decision input L-02 is supposed to wait on, quoted:

> **P0-13 · Version matrix + CoreCLR spike** (2d) [spike, DECISION input for L-02/V-02b/P0-02 scope] (F16)
> - Run the fixture on 6000.3 LTS, 6000.6 (Fast Enter Play Mode default) and a 6.8/Unity 7 alpha/beta when available. Record: does play entry reload? which Nexus statics (`MCPServer`, queues, capture state, runtime selector) misbehave without a reload? does compile still reload the domain?
> - Output: which reload-survival work is still required (legacy projects) and which becomes static-reset work. E: `Research~/evidence/P0-13/version_matrix.md`.

That evidence file does not exist. P0-13 has not been run.

The same plan says three different things about jobs:

- Principle 3: where a reload happens, persist a resumable job in SessionState / `Library/Nexus/jobs`, resume with `[InitializeOnLoad]`, client reconnects and long-polls once. `interrupted` is only Editor restart or crash. A reload may or may not happen (F16).
- §4 job model: same store. Pipeline mode uses Unity `--detach` + `unity job` only if P0-10 confirms they exist. Legacy and MCP get `nexus_job_status/wait/cancel`.
- §14b.2: Unity jobs are **CONFIRMED** and **do not survive reload**. Decision: **USE** Unity jobs in pipeline mode; Nexus L-02 only for reload-crossing Nexus workflows and legacy mode.

L-04 and V-02b are the only scheduled consumers, and both depend on L-02. S-05 is not one of them: it says persist compile messages in SessionState and, in pipeline mode, take truth from `unity recompile`.

## Unity reload, by version

Harness Editor is Unity `6000.3.16f1` (**MEASURED** in `00_UPSTREAM_PRIMARY_SOURCES.md` and `00_BASELINE.md`: PID 4465, port 7800, not compiling, domain reload not in progress, play mode stopped). This note did not press Play and did not compile.

| Version | Play-entry domain reload | Script-change code reload |
|---|---|---|
| 6000.3 LTS | **DOCUMENTED** default ON. Manual: disable it for speed; script refresh still reloads the domain even if play-entry reload is off. https://docs.unity3d.com/6000.3/Documentation/Manual/domain-reloading.html | **DOCUMENTED** still happens. Test Framework course: actions that affect scripts perform a domain reload. |
| 6000.4 | **DOCUMENTED** Project Auditor still tells you to disable domain reload. Not described as off by default. | **DOCUMENTED** same Test Framework sentence: script changes perform a domain reload. |
| 6000.5 | **DOCUMENTED** default still ON: "By default the Editor performs domain reload on entering Play mode." Code+scene reload page agrees. `[AutoStaticsCleanup]` / `[NoAutoStaticsCleanup]` exist for the off case. https://docs.unity3d.com/6000.5/Documentation/Manual/domain-reloading.html | **DOCUMENTED** code lifecycle: reload on Asset Database refresh when scripts change. "Domain reload" is the Mono name for that. |
| 6000.6 | **DOCUMENTED** default OFF: "the Editor doesn't perform domain reload on entering Play mode by default." Scene reload stays on. 6.6 release note (cited in `00_UPSTREAM_PRIMARY_SOURCES.md`): this default is for **new** projects. https://docs.unity3d.com/6000.6/Documentation/Manual/domain-reloading.html (manual built 2026-10-03) | **DOCUMENTED** still on. 6.6 code lifecycle: "By default Unity automatically reloads code as part of an Asset Database refresh, when it detects changes to your scripts." https://docs.unity3d.com/6000.6/Documentation/Manual/programming-code-lifecycle.html |
| 6000.7 beta | **DOCUMENTED** diagram: domain reload on play entry is off by default. | Not separately re-fetched. Do not treat 6.7 as "compile no longer reloads." |
| 6.8 / Unity 7 CoreCLR | **Not shipped here.** F16 in the master plan (6.8 only mode, Unity 7 removes classic domain reload) is a forecast. 6.6's own manual still documents Mono domain reload and says the new lifecycle attributes are meant to survive later runtimes. **UNKNOWN** as a behaviour of an Editor we can run. | |

This project's setting is not the 6.3 manual default in form, but it is the same in effect. **CONFIRMED** `ProjectSettings/EditorSettings.asset`: `m_EnterPlayModeOptionsEnabled: 1`, `m_EnterPlayModeOptions: 0`. Unity's `EnterPlayModeOptions` uses bit 1 for `DisableDomainReload` and bit 2 for `DisableSceneReload`. Zero means neither disable flag is set, so play entry still reloads the domain and the scene. **INFERRED** from the enum plus the asset, not from pressing Play.

6.5+ static cleanup is a second failure mode that is not a domain reload. Fields reset on play entry unless marked `[NoAutoStaticsCleanup]`. Installed Pipeline already wraps that attribute in `#if UNITY_6000_5_OR_NEWER` on `PipelineServerStartup` and `RecompileCommand`. **CONFIRMED** in source. That branch is not compiled by this 6000.3 Editor (**DOCUMENTED** in `00_UPSTREAM_PRIMARY_SOURCES.md`).

## Pipeline 0.7.0-exp.1 today

Cache: `Library/PackageCache/com.unity.pipeline@ab05e9cd7f74`. One registry, one comment, no file behind it.

`Runtime/Common/PipelineJobs.cs`:

> In-memory by design: a domain reload (script recompilation) discards pending jobs the same way it discards every other C# static.

`GET /api/job` 404 text in `BasePipelineServer.cs`: "jobs do not survive domain reloads and are pruned after retention."

What a Pipeline job actually is: `POST /api/exec` with `"job": true` returns `{jobId, state:"queued"}` immediately, runs later under the single exec gate (`SemaphoreSlim`, one command at a time), and keeps the result in a `ConcurrentDictionary` on that server instance. Caps: 100 queued-or-running (then 429), 100 terminal jobs, 1 hour TTL. Cancel is cooperative (`PipelineCancellation`). Synchronous main-thread work is not aborted. There is no `request_id` result cache and no step cursor.

The server object is not the job store. `PipelineServerStartup` is `[InitializeOnLoad]`. After a domain reload the static constructor builds a **new** `EditorPipelineServer` and a **new** `PipelineJobRegistry`. `OnBeforeAssemblyReload` is empty: it does not `Stop()` the old listener. The watchdog comment says not to heal a reload; `[InitializeOnLoad]` is expected to recreate the server, and the watchdog returns early while `isCompiling` or `isUpdating`. Token is rehydrated from `SessionState` (`SecurityTokenManager`, key `Unity.Pipeline.SecurityToken`): same bearer after reload, new bearer after process restart. **CONFIRMED**. Port, when unset, is `FindAvailablePort()` from 7800 upward, not a remembered port. Whether the abandoned `HttpListener` still holds 7800 long enough to bump the port is **UNKNOWN** (not measured; the Editor was not reloaded). Clients must re-read `.unity-pipeline-port`. This harness was on 7800 at the baseline snapshot (**MEASURED**), which only says the listener was up then.

`WaitRegistry` is a second in-memory queue. `beforeAssemblyReload` marks active waits `interrupted`, then the static dictionary dies with the domain, so a later poll is "not found / does not survive domain reloads." Entering play mode does not interrupt waits; exiting play mode does. With domain reload disabled, a wait can survive into play mode. With it enabled, the reload interrupts it anyway. **CONFIRMED** comments in `WaitRegistry.cs`. Play-entry behaviour was not executed.

### Survival matrix (Pipeline 0.7, from source)

| Failure | Transport | Job record (`/api/job`) | Exec gate / in-flight command | Receipt you can still read |
|---|---|---|---|---|
| Client HTTP timeout, domain stays up | Same listener. This is what `job: true` is for. | Survives. Poll by id. | Command keeps running. | In-memory result, 1 h / last 100. |
| TCP drop during domain reload | Old connection dies. New server after `[InitializeOnLoad]`. Token survives. Port file rewritten. Old listener is not explicitly closed. | Gone. 404. | Gone. No drain record. | None for the job itself. |
| Script compile / `RequestScriptReload` / assembly reload on Mono | Same as domain reload. `RecompileCommand` states the triggering HTTP call cannot stay open. | Gone. | Gone, even if the compile is what caused the reload. | `Temp/pipeline_recompile_status.json` plus Unity's native `scriptCompilationFailed` flag. Temp is deleted on Editor launch. |
| Play entry, domain reload on (this project's asset) | Same as domain reload. Sync PlayMode `run_tests` is refused for this reason. | Gone. **INFERRED** for play entry specifically: the registry has no play-mode exception, and play entry with reload is a domain reload. Not executed. | Gone. | Async tests: `Temp/pipeline_test_request.json` + status file. After reload, `TestReloader` reattaches a collector. It does **not** call `Execute` again. |
| Play entry, domain reload off | Server is documented to keep running (`OnPlayModeStateChanged`: "Server continues running through play mode changes"). | Should still be the same instance. **INFERRED**, not measured. On 6.5+, only if the holder is `[NoAutoStaticsCleanup]` or the cursor is not a static. | Stays. | Waits survive entry and are interrupted on **exit**. |
| Editor restart or crash | New process, new token, stale port file removed if the old PID is dead. | Gone. SessionState is gone. | Gone. | Temp receipts gone. Native compile-failure flag may still be true after a failed compile, but the status file is not. |

Partial scene edits are not a job concern. `batch` rolls back with one Undo group only if the batch aborts inside the same domain (`Documentation~/commands/batch.md`). A reload that kills the domain mid-command does not write a job receipt, and whether that Undo group survives is **UNKNOWN**.

Operation idempotency is absent. Submitting the same logical compile or test again is a new operation. The test runner's comment is the caution: calling `Execute` again double-starts the run.

Continuation exists only for three narrow commands, all file- or callback-based, none of them `/api/job`:

- `recompile` / `recompile_status` — Temp status file, rewritten from compilation events after the new domain loads.
- `package_add` / `remove` / `resolve` — `Temp/pipeline_package_status.json`. `RecoverInterruptedOperation` settles a file left `in_progress`.
- `run_tests --async_tests` — Temp request file, collector reattached, Unity's own test job resumes.

That is the pattern L-02 would reinvent, already scoped to the command that needs it.

## Competitors (jobs and recovery only)

### Funplay — CONFIRMED from `/tmp/funplay-unity-mcp`

Not a general step machine.

- Transport: `MCPServerDomainReloadHandler` stores `WasRunning` and a restart deadline in SessionState and restarts the in-process server. README: experimental broker keeps the client HTTP port across script recompiles and play-mode reloads.
- The broker does **not** continue the call. `TryCreateBrokerRedeliveryResponse` returns the last recovery summary, or an error that says the tool was not re-run "to avoid duplicate side effects," and tells the client to call `get_reload_recovery_status` then retry only if more work is needed.
- `DomainReloadHandler` can save one pending function and a recovery blob in SessionState. Auto-resume is capped at 5 per 120 seconds. Pending state is one-shot and discarded if older than 120 seconds.
- Test jobs are the real continuation: one SessionState slot (`Funplay.TestRunner.ActiveJob`) plus `[InitializeOnLoad]` re-registering `TestRunnerApi` callbacks, because Unity allows one run and PlayMode reloads the domain mid-run. That resumes Unity's run; it does not submit a second one.
- UI audit (`UIAuditService`): SessionState. A job still `running` at reload is finished as `interrupted` / `domain_reload`. Findings already stored remain. Work does not resume.
- External script sync: a SessionState pending flag, max age 120 seconds, completed after the new domain is up (`CompilationFunctions`).
- README: a capture/recording domain reload finalizes early and keeps an `interrupted` receipt. Editor restart is out of SessionState's lifetime.

### Coplay (CoplayDev/unity-mcp) — DOCUMENTED, not cloned

Public issues and PRs, not this tree:

- The product invests in **transport** recovery. PR #656 raised reload wait from 2 s to about 20 s. PR #725 keeps the Python server running across domain reload and play-mode so the Editor plugin can reconnect. Issue #1164: HTTP `OnBeforeAssemblyReload` sometimes fails to kill the Python process and the Editor then refuses the occupied port. Issue #1207: the reconnect window is hard-capped at 20 s and wedges on long reloads. Those are connection bugs, not a job store.
- Issue #790: `script_apply_edits` retried a successful insert on every reconnect and duplicated the method dozens of times. That is the cost of continuation without idempotency.
- PR #936: Play Mode options changed for a test run are stored in SessionState **and** a `Library/` marker so a crash can restore settings. That is undo of a setting, not resume of the test.
- A general resumable tool cursor on current `main` is **UNKNOWN**.

### akiojin/unity-cli — DOCUMENTED, not cloned

`docs/tools.md` on `main`: PlayMode tests may run with domain reload still enabled; the bridge persists the result across the reload; poll `get_test_status`; a disconnect during reload is not a failed test; do not submit a second run. Bake and player-build jobs are described as status-polled. Whether the cursor is SessionState, a file, or both is **UNKNOWN** without the bridge source. Hot-reload `recover` is a different feature (FastScriptReload), not a job store.

## Six things the plan collapses into "a job"

| | Pipeline 0.7 today | What L-02 assumes | Who already does it |
|---|---|---|---|
| Transport recovery | New listener, same token, re-read port file. In-flight HTTP is dead. | Shim reconnects and long-polls `job_wait`. | Funplay broker / Coplay keep-server. Necessary and separate. |
| Job-state recovery | No. `/api/job` is RAM. | SessionState cursor survives domain reload, not process death. | Funplay tests; Pipeline test/recompile/package files. |
| Operation idempotency | No result cache. A retry is a new exec. | "Idempotent step contract," stated, not designed. | Funplay refuses re-run. Coplay #790 shows why. Pipeline test runner refuses a second `Execute`. |
| Continuation | Only test collector reattach, and package/recompile status settled on the next domain. | `[InitializeOnLoad]` resumes the step and the job **completes**. | Nobody resumes an arbitrary tool body. Unity resumes its own test job. |
| Partial artifacts | Batch undo only inside one domain. No job-level partial report. | `Library/Nexus/jobs/<id>/` partials, and `interrupted` only after restart. | Funplay UI audit keeps findings and marks `interrupted` at reload, which is stricter than the plan. |
| Result receipt | `/api/job` result dies with the domain. Temp JSON survives reload, not restart. | Client can `job_status` after reconnect and see the outcome. | `recompile_status`, `package_status`, `test_status`, Funplay `get_reload_recovery_status`. |

Nexus already uses SessionState for the auth token, readiness epochs, and a diagnostic timeline (`Editor/MCPServer.Identity.cs`, `Editor/Readiness/EditorReadiness.cs`, `Editor/MCPServer.Timeline.cs`). There is no `NexusJobStore`. **CONFIRMED** by search. Epochs surviving reload (P0-02) are not a job.

## Why not BUILD NOW, TEST FIRST, or DROP

**Not BUILD NOW.** Principle 2: a new capability has to replace at least three agent calls or do something Unity and the shipping tools cannot. A general store does neither until a workflow calls it. L-04 and V-02b are the callers; V-02b is behind Gate A2; neither is the next production task. §14b already says pipeline mode should use Unity jobs for detach, and those jobs are the wrong tool for reload. Building `nexus_job_status/wait/cancel` now adds three commands with no producer.

**Not TEST FIRST.** L-02's evidence line needs a forced `RequestScriptReload` and a play-entry reload, 20 times, in the Editor. This sprint is not allowed to drive that Editor. An EditMode test that clears a static and reads SessionState would not prove the evidence line. P0-13's matrix was the test that was supposed to come first; the docs above answer the part that does not need a second Editor.

**Not DROP.** On Mono, including 6.6, a script compile still unloads the domain (**DOCUMENTED**). This project still reloads on play entry (**CONFIRMED** asset). `/api/job` will not grow a cursor for Nexus workflows. Legacy mode has no Pipeline job API at all. The need comes back the day a Nexus command tries to stay logically open across compile or play entry (L-04's wait, V-02b's scenario). Dropping the requirement would make those commands lie about completion.

**WAIT** until one of those commands is actually scheduled. P0-13 does not need a Unity 7 install to unblock that decision anymore: play-entry reload is project- and version-dependent; compile reload is not. The remaining P0-13 question is which Nexus statics misbehave when reload is off. That is V-08's job, not L-02's.

## Smallest design, only when a caller exists

Do not build the 3-day store. Copy the three Pipeline commands that already cross a reload.

1. One workflow, one record. SessionState holds the cursor (survives domain reload, dies with the Editor, which matches `interrupted` on restart). Put partial artifacts in `Library/Nexus/` only if the client must read them after restart. Do not use `Temp/` if the evidence line promises a partial after restart. Do not use `/api/job` as the cursor.
2. Do not re-execute a step whose side effect may have landed. Persist "started" before the dangerous call and "finished" after a readback. If the new domain finds "started" but not "finished", return `outcome: unknown` plus whatever readback is safe. Funplay and the Pipeline test runner both refuse the blind retry. Coplay #790 is the counterexample.
3. `[InitializeOnLoad]` may reattach a listener (test collector, compilation callback). It must not be the thing that performs the mutation again.
4. The client reconnects, re-reads the port file, and polls the workflow's own status. A second HTTP call staying open across the reload is impossible on this server (`RecompileCommand`'s comment). Long-poll only after the new listener is up.
5. Play entry is not one path. On this project it is a domain reload, so the cursor has to be in SessionState before `EnterPlaymode`. On a 6.6 new project it is not a reload: `[InitializeOnLoad]` will not run, and a static cursor can be wiped by `[AutoStaticsCleanup]` unless the holder opts out. Poll `isPlaying` and the readiness epochs. Do not wait for a reload event that may never come (Principle 3 already says this).
6. Skip `nexus_job_status/wait/cancel` until a second workflow needs the same record. One status field on the caller is enough. Cap at one active record so a retry cannot queue a second run.

What that would survive, once written: domain reload and this project's play-entry reload, for that one workflow, if the step is idempotent or explicitly `unknown`. It would not survive Editor restart as a running job. It would not make Pipeline `/api/job` survive reload. It would not fix transport; the port file and the existing token already do that.

## Not done

- No `RequestScriptReload`, no play toggle, no `/api/job` probe against PID 4465.
- No measurement of whether the listener's port stays 7800 across a real reload.
- Coplay and akiojin source not on disk. Their rows stay DOCUMENTED / UNKNOWN.
- `03` / `04` / `05_COMPETITOR_*.md` were absent, so they are not cited.
