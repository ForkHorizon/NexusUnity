# Nexus Unity — full report of the compatibility, contract, and discovery iterations

Date of this report: 2026-10-05.
Package: `com.forkhorizon.nexus.unity` at `/Users/daliys/Daliys/UnityProjects/UnityTestForNexus/Assets/NexusUnity`.
Branch: `rework2/S-02`.
Tip: `596d17141b9f4929204fa018ab804b04b052a25f`.
Merge-base with `development`: `329990e8c308f5c9d2c8e99cfb1d3ce7d6d52d66`.
Nothing in this series was pushed or merged.

This file is the single handoff for a later research agent. It restates the measurements, the failed attempts, the reviews, and the decision limits. It does not replace `MASTER_PLAN_V2.md`. It does not authorize an architecture. Where a number is absent, this file says so. Do not fill those gaps from release notes.

Raw siblings, if a byte-level check is required, live under `NexusFeature~/v2_analysis/I_08_compat_validation/`. This report is written so that agent does not have to reconstruct the argument from those files.

---

## 1. What these iterations were

Two sprints, then one continuation.

1. Intelligence + Validation sprint (already complete before this series' last stretch). It measured the current product, upstream Pipeline 0.8 from the package tarball, and wrote `NexusFeature~/v2_analysis/H_2026-10-validation/`. It did not compile Nexus against Pipeline 0.8. It did not run the discovery experiment. It did not approve S5, S6-P, Mixed, C1-as-production, a façade, or Gate F.

2. Compatibility + contract validation sprint. Objectives, in order:
   - Close S-02 false-pass holes MF-1 and MF-2, and fail closed when Pipeline is loaded but `CliCommandAttribute` cannot be found (MF-3).
   - Scratch-compile unchanged Nexus against Pipeline `0.8.0-exp.1`.
   - Capture real `unity recompile` and capture contracts on CLI `1.0.0-beta.12` + Pipeline 0.8.
   - Only if the environment was fair, run a cheap discovery falsification. Not Gate F.

3. Continuation after the written checkpoint. The checkpoint had stopped discovery because the scratch editor and the harness both touched port 7800. The continuation set `m_Port` to 7810, rebuilt a fixture, ran the frozen pre-run self-checks, and aborted before any agent. No arm was scored.

Production code that landed is two local commits on top of the previous six-commit stack. Research files, including this one, are untracked.

---

## 2. Hard constraints that still bind

- Do not rebase, squash, or push the stack.
- Do not upgrade or kill harness Unity PID 4465. It is Unity `6000.3.16f1`, Pipeline `0.7.0-exp.1`, listener on `*:7800`.
- Do not edit the harness `Packages/manifest.json` or its Library.
- Do not run `unity self-update`. Installed user CLI stays `1.0.0-beta.10` at `~/.unity/bin/unity`.
- Do not edit `MASTER_PLAN_V2.md` unless the owner asks.
- Do not implement S5, S6-P, Mixed, a façade, broad write delegation, a generic invoke/router, eval or `run_script` forwarding, L-02, or Gate F from this report.
- Do not start S-05 until a fixture covers the three exit-6 bodies and a warning that is inside the same `unity recompile` call. Do not ship a partial command.
- Do not record projectmem issue `#0013` again. P0-02 was accepted earlier (`6fff2e8`, evidence `060cc13`, 20/20).
- Do not treat handler micro-optimizations as the bottleneck. Prior measurement: cheap handler time under 1 ms, client round trip about 100 ms.
- Domain failures of Nexus commands are HTTP 200 with envelope `ok: false`.
- A second editor must not bind port 7800. Loopback `127.0.0.1:7800` is stolen by whichever process listens there even while PID 4465 holds `*:7800`.

---

## 3. Git state

Local stack above `development` that matters for the product, oldest first:

| Commit | Subject |
| --- | --- |
| `6fff2e8` | fix: keep Editor readiness false until the compile cycle finishes |
| `d738dfc` | fix: keep a newer script write from settling on an older compile |
| `060cc13` | docs: record the P0-02 20-run Editor acceptance |
| `aa923f6` | feat: return one envelope from the canonical commands (S-04) |
| `23005b4` | feat: default canonical captures to a 768 JPEG look (S-07) |
| `bfdd0f7` | feat: lock canonical CLI parameters to the descriptor (S-02) |
| `5ecc7fb` | test: observe omitted CLI defaults on the real entry points |
| `596d171` | fix: compile Pipeline commands against 0.7 and 0.8 |

`596d171` is only the asmdef reference plus one changelog bullet. `5ecc7fb` is the test seams. They were not mixed.

Working tree at report time: those commits are clean. Untracked and intentionally untracked:

- `NexusFeature~/HANDOFF_IMPLEMENTATION_2026-10-04.md`
- `NexusFeature~/v2_analysis/H_2026-10-validation/`
- `NexusFeature~/v2_analysis/I_08_compat_validation/`
- this file
- `.graphifyignore`, `graphify-out.meta`

Scratch worktree `/tmp/nexus-p08-wt` is detached, not on a branch, and was left with the four product files still modified relative to `bfdd0f7`. It is not at `596d171`. The scratch Unity project `/tmp/nexus-p08-harness` references that worktree via `file:/tmp/nexus-p08-wt`. Its editor is stopped. Port 7810 is free.

Worktree status command that does not lie:

```text
git --git-dir=/Users/daliys/Daliys/UnityProjects/UnityTestForNexus/.git/modules/Assets/NexusUnity/worktrees/nexus-p08-wt --work-tree=/tmp/nexus-p08-wt status -sb
```

`git -C /tmp/nexus-p08-wt status` shows false deletions because the submodule `core.worktree` is relative. Do not `git reset --hard` that worktree without the explicit `--work-tree`.

Package version in the changelog section touched by `596d171` is `[1.7.0]`. README was not changed. No public tool schema changed.

---

## 4. What was tried, in order

### 4.1 Reviews before any test edit

Two independent readers, no edits:

- `01A_FALSE_PASS.md` — tried to keep the drift suite green while changing `max_logs`, `format`, width, height, `max_dimension`, aliases, names, and required flags. Must-fix: FP-1, FP-2, FP-3. Should-fix: FP-4, FP-5, FP-6.
- `01B_TEST_INDEPENDENCE.md` — checked that assertions do not call the helper they claim to test.

### 4.2 First patch and its review

A first patch made the HTTP omit parser and a prepare-look helper observable. `01C_FIX_REVIEW.md` closed FP-1, FP-5, FP-6, MF-1, and SF-1. It left FP-2, FP-3, FP-4, and MF-2 open because the test called `TryPrepareLook` itself instead of `ExecuteAsync` / `ExecuteFromCli`. `max_logs: 0` stayed unasserted. A short-name attribute decoy (SF-2) was left open on purpose.

### 4.3 Seam that closed the four remaining edits

`GroupCompileErrorsCommand` gained an internal `ReadErrorLogs` delegate. Default still calls `MCPServer.GetLogs(count, "Error", string.Empty)`. `TryReadCompileArguments` returns `parsed ?? DefaultMaxLogs` (`DefaultMaxLogs` is 50).

`CaptureGameViewCommand` gained an internal `Capture` delegate. Default still calls `CaptureGateway.Shared.CaptureGameView`. `ExecuteAsync(CaptureLookCall)` awaits that delegate. `FormatOrDefault` remains in production. The single `call.Request.Format =` assignment stays inside `TryPrepareLook`. The test does not call `FormatOrDefault` to obtain the expected value. The expected omit format is the literal `CaptureFormat.Jpeg`.

`01D_SEAM_REVIEW.md` closed FP-2, FP-3, FP-4, and MF-2. It called the dual asmdef the smallest 0.7+0.8 compile fix. Its last line does not approve a commit. The commits were made after that read because the sprint's six conditions were otherwise met. Treat "01D did not stamp the commit" as a real limit, not as a request to revert.

### 4.4 Unchanged Nexus versus Pipeline 0.8

Scratch project `/tmp/nexus-p08-harness`, created with `-createProject`, manifest pinned to Pipeline `0.8.0-exp.1` and the worktree package. Harness Library was not copied.

Result: Unity exit 1. `evidence/pipeline08/compile-08.log` (1409 lines). Message: `Aborting batchmode due to failure: Scripts have compiler errors.`

108 `CS0246` lines, all in `Editor/Pipeline/NexusPipelineCommands.cs`, six names:

- `CliCommandAttribute`
- `CliCommand`
- `CliArgAttribute`
- `CliArg`
- `Tags`
- `MainThreadRequired`

No other file emitted `error CS`.

### 4.5 Where the types actually are

On Pipeline 0.8.0-exp.1 (fingerprint `62c08c808737255821f3449466689a527a2eea50`):

| Type | Assembly | Namespace | Path |
| --- | --- | --- | --- |
| `CliCommandAttribute` | `Unity.Pipeline.Attributes` | `Unity.Pipeline.Commands` | `Runtime/Attributes/CliCommandAttribute.cs` |
| `CliArgAttribute` | `Unity.Pipeline.Attributes` | `Unity.Pipeline.Commands` | `Runtime/Attributes/CliArgAttribute.cs` |
| `CommandRegistry` | `Unity.Pipeline` | `Unity.Pipeline.Commands` | `Runtime/Common/CommandRegistry.cs` |

No type forwarder. `Unity.Pipeline.Attributes.asmdef` has empty `references` and `autoReferenced: true`. Custom asmdef references are not transitive, so auto-reference did not compile Nexus. `Unity.Pipeline` itself references Attributes. Changelog note UUM-153151 says to reference Attributes instead of `Unity.Pipeline`. Dropping `Unity.Pipeline` would break 0.7.

On the installed 0.7 cache, `CliCommandAttribute` is still in assembly `Unity.Pipeline` at `Runtime/Common/CliCommandAttribute.cs`. Assembly `Unity.Pipeline.Attributes` already exists on 0.7 and does not contain that type. An Attributes-only reference was not compiled on 0.7. The inference that it would fail is from the type's location, not from a 0.7 log of that one-name list.

### 4.6 Dual reference

`Editor/Pipeline/UnityMCP.Editor.Pipeline.asmdef` references, in order:

- `UnityMCP.Editor`
- `Unity.Pipeline`
- `Unity.Pipeline.Attributes`

`versionDefines` still maps `com.unity.pipeline` `0.0.0-exp` to `NEXUS_HAS_PIPELINE`. That expression is true for every installed pipeline, so both 0.7 and 0.8 compile this same list. There is no version-gated asmdef.

Measured:

- 0.8 batch recompile `compile-08-ref.log`: `Exiting batchmode successfully now!`, no `error CS`. Probe found `NexusPipelineCommands` in `UnityMCP.Editor.Pipeline.dll`.
- Live 0.7 editor recompiled the same list. Suites below.

Version-gated asmdefs were the fallback if the extra reference had broken 0.7. It did not, so they were not built.

### 4.7 Test-runner false passes that are not product passes

Unity Test Framework resolved `1.6.0` (`aebcb98460c6`) although the scratch manifest asked for `1.7.0`. In 1.6, `-testFilter` is stored as `Filter.groupNames` (a regex), not an NUnit name. A filter that matches nothing exits 0 with `testcasecount=0`. That happened on the first scratch drift runs.

Package tests with `UNITY_INCLUDE_TESTS` are not compiled for a `file:` package unless the package name is in manifest `testables`. The scratch manifest has `"testables": ["com.forkhorizon.nexus.unity"]`.

The run that counts for the pre-fix suite is `drift-committed-on-08.xml`: 3 tests, 2 passed, 1 skipped. `CliArgsMatchCanonicalDescriptors` ignored with the message `com.unity.pipeline is not loaded. CLI drift is checked when Pipeline is installed.` Pipeline 0.8 was loaded. The old probe searched only assembly `Unity.Pipeline`. The attribute had moved, so the test took the Ignore branch and lied.

The run that counts after the new probe, before the capture seam: `drift-new-on-08.xml`, 3 passed, 0 skipped.

The run that counts after the seam: `drift-seam-on-08.xml`, 3 passed, 0 skipped. `asserts="0"` on those cases is a Unity XML quirk. The cases have non-zero duration and `result="Passed"`. They are not the empty-filter run.

### 4.8 0.7 suites after the seam and the asmdef

`evidence/mf_07/`, live PID 4465, Pipeline 0.7.0-exp.1:

| Suite | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| `NexusCommandCliDriftTests` | 3 | 0 | 0 |
| `NexusCommandEnvelopeTests` | 29 | 0 | 0 |
| `NexusCommandRegistryTests` | 9 | 0 | 0 |
| `CaptureLookTests` | 10 | 0 | 0 |

0.8 did not repeat envelope, registry, or capture-look. The parent 0.8 test-run element still says `testcasecount="216"`. Only the drift class was filtered in.

### 4.9 Quality gate

Deterministic command, after the product edits and before the commits:

```text
dotnet run --project tools~/NexusQualityGate/NexusQualityGate.csproj -- --root . --ai off --format github
```

0 errors, 5 pre-existing warnings (`Screenshots.cs`, `Tools.cs`, `MCPServerMethods.cs`, `NexusRuntimeCapabilities.cs`). The edited command files added no warning. `CaptureGameViewCommand.cs` stayed under the 300-line warning (about 270). Tests are outside the gate.

`scripts/prepush-validate.sh --static-only` exits 1 before that gate because it finds pre-existing `graphify-out`, `.DS_Store`, and `__pycache__` that the script's own pytest writes. Pytest inside that script: 47 passed. Do not delete `graphify-out` to make the wrapper green.

### 4.10 CLI isolation

`/tmp/unity-cli-beta12/unity` is exactly `1.0.0-beta.12`. sha256 `7f21746fe7b2cf1010a7f293763ad9590715166147dcc0401a7b52de0d95f107`. It was downloaded directly. The official `install.sh` was not executed, because that installer rewrites `~/.zshrc` and can replace `~/.unity/bin/unity`. Hub-bundled Unity CLI is also beta.10.

`--project-path` is an option of `unity command`, not of the top-level binary. This works:

```text
/tmp/unity-cli-beta12/unity --format json --non-interactive command --project-path /tmp/nexus-p08-harness editor_status
```

Putting `--project-path` before the word `command` returns `unknown option '--project-path'`.

Global `--format` accepts `human|json|tsv|ndjson|github`. A command argument `--format jpg` is captured by that global flag. Positional `format=jpg` is then bound in parameter order, so `width` received `format=jpg`. A `--` before the command arguments made the later JPEG call succeed. The drift suite invokes the C# method in process. It does not lock that host parser.

`nexus_look` is not a registered CLI command. The catalog name is `nexus_capture_game_view`. HTTP aliases `nexus_look` and `nexus.look` are unlisted and are pinned as constants in the test. A CLI call of `nexus_look` returns 400 Command Not Found.

### 4.11 Catalog on the scratch editor (Q5, Q6)

`evidence/contracts08/catalog_nexus.json`. Response `projectPath` is `/private/tmp/nexus-p08-harness`. This was taken while that editor was the process answering `127.0.0.1:7800` (see the port collision below). It is a Pipeline CLI catalog, not a saved HTTP `GET /api/commands` body.

| Name | Required | Defaults that were read |
| --- | --- | --- |
| `nexus_project_map` | `[]` | — |
| `nexus_group_compile_errors` | `[]` | `max_logs` 50 |
| `nexus_capture_game_view` | `[]` | width 0, height 0, format `jpg`, quality 85, `max_dimension` 768, view `game`, delivery `base64` |

Descriptions in the catalog match the passing drift test's canonical strings. Package field `UnityMCP.Editor.Pipeline`. Tags include `nexus`. `mainThreadRequired` is true on the three wrappers.

### 4.12 Port collision, then 7810

Both the harness and the first scratch editor wanted port 7800. Harness PID 4465 listens on `*:7800`. Scratch PID 94542 listened on `127.0.0.1:7800`. Clients to `127.0.0.1:7800` hit the scratch project. Only PID 94542 was killed. After that, `editor_status` on 7800 returned the harness path, ready, not compiling.

`EditorPipelineConfig` reads `ProjectSettings/Packages/com.unity.pipeline/EditorPipelineConfig.json`. Field `m_Port`. Default 0 auto-assigns 7800–7849. The continuation wrote `{ "m_Port": 7810 }` before the next launch. The port file then said port 7810, project `/private/tmp/nexus-p08-harness`, Unity `6000.3.16f1`. `editor_status` agreed. PID 4465 stayed on 7800.

That first 7810 process later blocked its main thread. `open_scene` timed out at 30s. A following `editor_status` returned HTTP 400: main thread operation timed out after 8000 ms. Likely a modal reload dialog on a dirty scene. The process was SIGKILL'd (exit 137) so it would not save the in-memory scene over the patched YAML. Harness was not killed.

A relaunch loaded `Yard.unity` from disk (`YARD_OPEN_READY` in `/tmp/nexus-p08-logs/editor-7810b.log`) and again bound 7810. After the discovery self-checks, that editor was also SIGKILL'd. Final state: 7810 free, 7800 is only PID 4465.

Do not print `evalToken` from the port file. It is a bearer.

---

## 5. S-02: what the tests now observe, and what they still miss

Commit `5ecc7fb`.

Observed, with literal expectations:

- `max_logs`: descriptor default, CLI `DefaultValue`, `DefaultMaxLogs`, `TryReadCompileArguments` of `{}` and of `{max_logs:7}`, and a `ReadErrorLogs` spy on `Execute()`, `ExecuteAsync({})`, `ExecuteAsync({max_logs:7})`, and the reflected CLI method invoked with `{50, null}` and `{7, null}`.
- Capture omit: `ExecuteAsync(new JObject())`, `ExecuteFromCli` with a null format, and the reflected CLI method invoked with `"jpg"` in the format slot. The seam snapshot must be `CaptureFormat.Jpeg`, width 0, height 0, quality 85, max edge 768.
- Explicit HTTP and CLI: width 12, height 9, format png, quality 40, max edge 100 must arrive as `Png` / 12 / 9 / 40 / 100.
- Wrong names `w`, `h`, `jpeg_quality` must leave the omit defaults.
- If `Unity.Pipeline` or `Unity.Pipeline.Attributes` is loaded and `Unity.Pipeline.Commands.CliCommandAttribute` is in neither, the test fails. Ignore only when neither assembly is loaded.

Still green if someone does these (credible leftovers, not the four edits 01D closed):

1. `Execute` calls `ReadErrorLogs(maxLogs)` and then groups a different list. The spy records the integer and returns an empty list.
2. The capture seam snapshots format, width, height, quality, and max edge. It does not snapshot view, target, or delivery. An omit path that flips view to scene or delivery to file stays green. A direct `TryPrepareLook` block in the test is not that close.
3. The host CLI parser. Saved transcripts `nexus_jpg.stdout.txt`, `nexus_png.stdout.txt`, and `nexus_scene.stdout.txt` never reached the C# method: `format=jpg` bound to `--width`, and `target=CenterGreen` bound to `--quality`. `nexus_cli_jpg.stdout.txt` is the later success after `--`.
4. `max_logs: 0` is never sent. A missing value is 50. A present 0 is 0. Treating 0 as omit stays green.
5. Short-name `CliCommandAttribute` lookup can still match a decoy type in another assembly (SF-2).

`CaptureLookRequest` also accepts `max_long_edge` when `max_dimension` is absent. The alias probe does not send it. That is an unlocked second name, not one of the closed edits.

---

## 6. Recompile contract (beta.12 + Pipeline 0.8)

Scratch project, not the harness. CLI wall times are the CLI process, not a measured Editor compile interval.

`unity recompile`, when it returns errors, adds `type`, `raw`, `file`, `line`, `column`, `code`, `message`, `warnings`, and `summary`. `recompile_status` (`--result-only`) is a JSON object. Its `errors` are raw strings. On 0.8 it is not a JSON string. The 0.7 wire shape was not re-captured this series. Assembly name and diagnostic id are not fields.

| Run | Exit | CLI wall | Body |
| --- | --- | --- | --- |
| R1 first recompile while the editor was already up | 6 | 935.1 ms | `COMMAND_FAILED`. `Network error: An error occurred while sending the request.` `data` null. |
| R1 status immediately after | 0 | — | Object: `status=completed`, `failed=false`, `errors=[]`, `compilationFailed=false`. No id ties it to the failed call. The JSON does not contain the words "domain reload." |
| R2 one error | 6 | 13459.9 ms | `Assets/P08ProbeErrors/One.cs` line 2 column 34, `CS0103`, message `The name 'missingName' does not exist in the current context`. `success=false`, `failed` and `compilationFailed` true, `status=completed`, summary errors 1 warnings 0. First import of that file, so the wall time is not a steady-state compile. |
| R2 status | 0 | — | `errors` is one raw string, not the structured object. |
| R3 three errors | 6 | 2474.0 ms | `Three.cs` methods A, B, C. Three `CS0103`, source order, column 22, lines 2–4, one file, no truncation at three. `summary.errors` 3. |
| R3 status | 0 | — | Three raw strings, same order. |
| R4 warning attempt | 6 | 3154.0 ms | HTTP 401 `Unauthorized`, `Missing or invalid authentication token`, empty data. Not a warning payload. |
| R4 status | 0 | — | `completed`, `failed=false`, `errors=[]`. No warning field. |
| R4 `--strict` | 0 | 878.1 ms | `up_to_date`, `warnings=[]`, `compilationFailed=null`. Ran after status was already clean. Did not prove `--strict` fails on CS1030. |
| R6 no change | 0 | 1017.7 ms | `up_to_date`, `compiling=false`, `failed=false`, `compilationFailed=null`, errors and warnings empty, summary 0/0. Not a sub-50 ms compile. |
| `r2_restore`, `r3_restore`, `r4_restore` | 6 | about 2.4–2.5 s | Same 401 text as R4. |

Editor log `/tmp/nexus-p08-logs/editor-08.log` lines 1992 and 2007, copied to `evidence/contracts08/editor-warning-cs1030.txt`:

```text
Assets/P08ProbeErrors/Warn.cs(2,10): warning CS1030: #warning: 'P08_WARNING_MARKER'
```

That warning is in the editor log twice. It is absent from every saved recompile and status JSON. The `unity recompile` call beside that compile was the 401, so this series does not contain a successful recompile body that omitted a warning from the same call.

Exit 6 in this folder is three different bodies: a network error, a populated `data.errors` array, and HTTP 401. A client that treats exit 6 as "there are compiler errors" is wrong for R1 and for the 401s.

`compilationFailed` is `null` on `up_to_date` (R6 and strict) and `false` on the clean R1 status object. It is `true` on R2/R3.

Pipeline `RecompileCommand` stores `CompilerMessage.message` only when the type is Error. That source fact explains an empty warning list. It is not a substitute for a successful recompile response that contained CS1030.

Docs that say CLI exit 6 means compile errors and exit 7 means a dropped request are not what this folder measured. Exit 6 was used for both the network drop and the real errors.

---

## 7. What S-05 would still have to add

S-05 was not started. `S05_INPUT_CONTRACT.md` does not authorize an implementation.

When R2/R3 return, Unity already provides file, line, column, compiler code, message, raw line, count, order for one file of `CS0103`, `failed`, `compilationFailed`, and status `completed` or `up_to_date`.

Nexus would still be adding:

- grouping by root file across console logs (what `nexus_group_compile_errors` does now)
- source context around the line
- did-you-mean
- the canonical envelope
- a hot-reload or Safe Mode explanation
- a result that remains when the triggering CLI call has died and only a later status object exists

A command also has to define the three exit-6 bodies. Do not assume `recompile_status.errors` has structured file/line fields. Do not assume warnings are in that object. Do not assume a no-change compile is under 50 ms.

---

## 8. Capture comparison on 0.8

GPU Metal, Apple M5, play mode stopped, not `-nographics`. Marker scene: orthographic camera `MarkerCamera`, red top, blue bottom, green center, yellow right, magenta Screen Space Overlay 120×48 at the top left. Pixel table: `evidence/contracts08/pixels.json`. Image copies under `evidence/contracts08/images/`. Base64 in the saved JSON was shrunk before some files were written. Use the image copies and `pixels.json` for pixels, not a shrunk JSON field.

### Official

| Call | Result |
| --- | --- |
| First omitted `capture_game_view`, no Game view target | HTTP 400: `No Game view render target to capture: open a Game view window, or pass source="camera".` Exit 6, 1024.5 ms. This is a missing target, not "Edit Mode forbids screen." |
| Later omit, and explicit `source=screen` | Same bytes. sha256 prefix `f32106ca`. 640×360 PNG, 4476 bytes, `source=screen`, saved as `Assets/Captures/...png` with a `.meta`. Top red, bottom blue, center green, right yellow, top-left magenta `(255,0,255)`. Fractions: red 0.476, blue 0.483, green 0.024, yellow 0.010, magenta 0.0071. |
| `source=camera` `MarkerCamera` | 640×360, 4201 bytes, `.meta` present. Same scene markers. Top-left red. `frac_magenta` 0. |
| `capture_scene_view` | 640×360, 22019 bytes, `.meta` present. Marker fractions 0. Top about `(154,153,113)`, bottom about `(75,57,30)`. Scene-view chrome, not the quads. |

A 120×48 rectangle on 640×360 would be about 0.025 of the pixels. The measured magenta fraction is about 0.007. The top-left sample is magenta on screen and red on camera. That is one scene, one sample, not encoding or resolution parity.

`save_path` of `Captures/...` was rewritten to `Assets/Captures/...` and a `.meta` was written beside each official file.

Pipeline capture refuses batchmode/headless when `graphicsDeviceType` is Null. This editor reported Metal.

### Nexus

Registered name `nexus_capture_game_view`.

| Call | Result |
| --- | --- |
| CLI after `--`: `--format jpg --delivery file --view game --max_dimension 768` | Exit 0. 768×407 JPEG, 10074 bytes, `/private/tmp/nexus-p08-harness/Temp/NexusCapture/nexus-look-...jpg`, `source=game_view`, no `.meta`. Markers match, including magenta, with JPEG off-by-one (`254,0,0`, top-left `255,0,254`). `encode_ms` 2.18, `base64_ms` 130.03 (cold, includes wait), `total_ms` 258.7, `meta.ms` 130, gen 6. |
| HTTP jpg file | Same 768×407 and 10074 bytes as the CLI jpeg. No `.meta`. `encode_ms` 1.48, `base64_ms` 7.09, `total_ms` 13.79, `meta.ms` 7. File delivery still filled `base64_ms`. Do not call that a new double-count finding. |
| HTTP png file | 768×407, 7119 bytes, no `.meta`. Exact magenta `(255,0,255)`. Not the official PNG. |
| HTTP scene, the body that matches the image has no target field | 768×403, 33818 bytes, `source=scene_view`, no `.meta`. Marker fractions 0. Center about `(75,57,31)`, not green. Chrome colors are swapped versus the official scene image: Nexus top about `(75,57,30)`, bottom about `(156,155,115)`. |

The CLI invocation that passed `target=CenterGreen` did not reach the method. `target` was bound to `--quality`.

### S-07 classification

Nothing was deleted.

| Piece | Class |
| --- | --- |
| JPEG | STILL DIFFERENTIATED. Official saves in this run are PNG. |
| File outside Assets, no `.meta` | STILL DIFFERENTIATED. Official path became `Assets/Captures` plus `.meta`. Nexus wrote `Temp/NexusCapture` with no `.meta`. |
| Edit Mode overlay versus official `source=screen` | UNKNOWN as parity. One scene shows some magenta on screen and none on camera. Nexus images are 768×407, not those bytes, and the source strings differ (`screen` versus `game_view`). |
| Scene target framing | UNKNOWN. Neither scene image contains the markers, and they disagree vertically. The CLI target argument did not arrive. |
| Orientation on the game images | Top red, bottom blue, on official screen and on Nexus game. |

The previous sprint did not prove Edit Mode overlay differentiation. This series measured the one-scene signal above and then refused to call it parity.

A later continuation, on the port-7810 editor and a different scene (`ShadeBand` cyan overlay, `RedMarker` at the top edge, camera `YardCamera`), repeated the directional signal at 320×180: omitted capture `source=screen`, top-left `(38, 217, 217)`, top `(255, 0, 0)`; camera top-left and top `(255, 0, 0)`. Those PNGs are scratch files `Assets/Captures/disc-omit.png` and `disc-camera.png`. They are a self-check, not an agent result, and they are not the deathmatch images.

---

## 9. Discovery experiment

### 9.1 What was frozen, and not run

`06_DISCOVERY_PREREG.md` is frozen as of 2026-10-05 with zero agent runs. It replaces the unfrozen draft `H_2026-10-validation/10_DISCOVERY_PREREG.md` for a later scratch run. It does not replace Gate F. The October 4 draft was also not executed.

Allowed labels, and only these: strong negative, strong positive, neutral, insufficient. Forbidden words for a result: SURVIVED, FALSIFIED, CONFIRMED, and any Gate F margin. Do not recommend building or rejecting a façade, S6-P, Mixed, S5, or "Unity is enough" from this protocol.

N, if an arm is actually started, is 14 scored runs (7 tasks × 2 seeds). That can contradict a large task-stable gap. It cannot see a 15 percentage-point gap. The plan's power note of about 235–290 paired runs is why Gate F is out of scope. Do not add seeds, tasks, or metrics after any agent output is visible. There is no agent output, so that ban has not started. It starts at the first agent transcript.

Large-effect rules, frozen:

- Large success: net paired wins at least 5 of 14, the loser has at most 1 win, and every leave-one-task-out cut still has net at least 3 in the same direction. A paired win is semantic success 1 against 0 on the same task and seed. Ties are not wins. Voids are not scored and are not imputed.
- Large false-success: counts differ by at least 4 of 14, and every leave-one-task-out cut still differs by at least 3.
- Large cost: geometric mean of tokens-per-success ratios at least 1.50 or at most 0.67, on at least 5 tasks where both arms have at least one semantic success, and the uncached-token ratio is on the same side of 1. If uncached tokens are null, there is no cost win.
- Match band: success net at most 2 and false-success counts differ by at most 1, including after every leave-one-task-out cut.
- Output-length only: success and false-success stay in the match band, but the large-cost rule fires. Record it. It is not a semantic win.

### 9.2 Hypotheses the protocol can actually score

The plan wording is wider than the contrast. A label applies only to the contrast.

| Id | Contrast this protocol scores | Label limit |
| --- | --- | --- |
| H1 | C1 versus S4 | A match kills only a large gap on this set. It does not confirm an architecture. If only S6-P-mini beats C1, H1 is not strong positive. |
| H2 | S6-P-mini versus S4 only | Not plan S6-P. Not 154 actions behind 13 managers. Unbuilt `nexus_status` / `snapshot` / `diagnose` / `compile` / `run` / `get` are absent on purpose. Quote any positive label with the sentence NOT AN S6-P TEST. This is narrower than "semantic Nexus versus an equally small official surface." That wider claim would be mini versus C1, and this file does not score it as H2. |
| H3 | No Mixed arm | Always insufficient. Escape share on T-OUT is a table, not a label. Do not relabel U-TAGS as Mixed. |
| H4 | S4+ versus S4, with E-SKILL versus C1 as the control | S4+ beating S4 without the E-SKILL control is insufficient. If both large-rules fire, write `SKILL_TEXT_ALSO_LARGE` and label neutral. |
| H5 | No agent contrast | Always insufficient. A wrong port or wrong CLI voids H1–H4. It is not an H5 result. Do not infer H5 from wall time. |

Arms, if a later runner starts them:

- U-TAGS: official current Unity CLI tag/index discovery.
- C1: curated official subset, task-relevant commands only. Critical control for H1.
- E-SKILL: small official surface plus skill text. Tests whether skills plus a smaller CLI reproduce the benefit attributed to a Nexus-primary façade.
- S4: frozen current baseline. No new discovery metadata. Descriptions must match `bfdd0f7`.
- S4+: current architecture plus the approved discovery-metadata lines only.
- S6-P-mini: bench only. Small semantic surface, official Unity backend, real Nexus composites that already exist, bounded outputs, rare escape. Not 154 tools behind manager names. Do not ship it.

Minimum that can speak to H1 is C1 and S4, 28 runs. Fewer than 5 surviving tasks aborts before run 1. Stop at 100 attempted runs. One editor. Agents must not read the prereg, the answer key, `07_DISCOVERY_RESULTS.md`, or `evidence/contracts08/`. An unrestricted shell that can open the package worktree voids the run. Contract JSON and `tags.stdout.txt` are environment facts, not agent results.

Seeds named by the protocol: 17 and 43. Arm order is a rotation of the six-arm list by `seed mod 6`. If only C1 and S4 are started, run them in the order they appear in that rotation.

### 9.3 What the continuation actually did

It did not start an arm. It tried to satisfy the pre-run gates on port 7810.

Environment gates that passed:

- Port file and `editor_status`: port 7810, project `/private/tmp/nexus-p08-harness`, Unity `6000.3.16f1`, ready, not compiling, play mode stopped.
- `unity command --tags`: 50 tags.
- Every C1 allowlist name existed (`search`, `wait_for`, `run_tests`, `recompile`, `console`, the scene and capture names, and the rest of the frozen list). None was deleted.
- Harness stayed on 7800.

Fixture that was built, then checked:

- Scene `Assets/DiscFixture/Scenes/Yard.unity`.
- 1006 transforms (1008 GameObjects in the YAML count).
- `/PierLamp` light intensity 2.75, read live through `get_serialized_fields`.
- `/Market/Board` `NoticeBoard.headerIcon` resolves to `Assets/DiscFixture/Art/header.png` guid `f5004df784a604b0396ff8c36405ec2a`.
- `noticeTemplate` in the YAML is guid `c0ffee00c0ffee00c0ffee00c0ffee00`. Live `get_serialized_fields` and `get_component_properties` both return `null` for that field.
- Overlay `ShadeBand` and top-edge `RedMarker` behaved as in section 8's later 320×180 check.
- `DiscBuild.cs` was removed after the first build so it would not rebuild. `YardOpen.cs` remains in the scratch project and only opens the scene and the Game view.

Self-checks:

| Task | Result | Why |
| --- | --- | --- |
| T-VIS | Pass | Omitted capture `source=screen` includes the cyan overlay sample. Camera capture does not. |
| T-BULK | Pass | 1006 transforms. Intensity 2.75. |
| T-SCENE | Drop | Official read returns `null`, not an empty-path guid. The oracle treats a bare "null" answer as failure. The task would not be solvable through the commands the allowlist exposes. |
| T-RARE | Drop | First selectable small-tag command `get_import_settings` returns an object. Next, `read_text_file`, returns an object whose `contents` is the whole script. Next, `get_authoring_root`, returns `{"root":"Assets"}`. Three skips. No scalar or one-line string. |
| T-OUT | Drop | Selection starts at the command after T-RARE's choice. There is no choice. |
| T-COMP, T-AMB | Not planted | They could not raise the surviving set to 5 even if both passed. |
| T-SPARE | Not used | The spare replaces only a failed T-VIS. T-VIS passed. |

Surviving tasks: T-VIS and T-BULK. The protocol aborts below 5. No model id was written into the freeze block. No fixture-key hash. No agent process. No tokens. No hypothesis label.

`tags.stdout.txt` from the earlier 7800-collision catalog is not an agent transcript.

### 9.4 What a later runner may still do

No agent output exists, so the ban on changing tasks after seeing output has not started. Repairing the fixture and re-running self-checks before run 1 is still before the experiment. The following are not allowed:

- Lowering the 5-task bar.
- Scoring the aborted attempt.
- Calling `get_authoring_root`'s object a scalar.
- Treating T-SCENE as passed while the official property API returns null.
- Using T-SPARE while T-VIS passes.
- Starting arms on port 7800 or with `~/.unity/bin/unity`.
- Letting the agent read this file, the prereg, or the contract JSON.

A dangling guid that the official `get_serialized_fields` reports as null will fail T-SCENE again. A rare command whose result is an object will fail T-RARE again. Those are properties of Pipeline 0.8's command results, not of a flaky editor.

---

## 10. Adversarial review and what was adopted

`09_ADVERSARIAL_REVIEW.md` was written while `06_DISCOVERY_PREREG.md` was still absent. The prereg is on disk now. The review's other objections were adopted. It did not design the experiment and did not edit product code.

Adopted limits:

- Two editors are not every 0.7 and 0.8 build. The runs are `0.7.0-exp.1` on PID 4465 and `0.8.0-exp.1` in the scratch project.
- 0.8 repeated three drift tests, not the 216-count assembly.
- 01D did not approve the commit. The commit exists anyway.
- The catalog used for Q5–Q6 is the CLI catalog, not HTTP `/api/commands`.
- Exit 6 is three saved bodies, not "a domain reload."
- CS1030 is an editor-log fact. It was copied into `editor-warning-cs1030.txt` after the review noted that the evidence directory did not contain the line. It is still absent from the JSON payloads.
- Official omit and explicit `source=screen` are the same bytes. Nexus images are not.
- The two scene captures disagree vertically.
- Discovery had no scores. The later 7810 attempt still has no scores. It aborted earlier than "agents ran and the comparison was unfair."

Attacks that did not hold:

- The seam XML is not the zero-test false pass. The cases ran and passed.
- R2/R3 really do have file, line, column, code, message, and raw.
- Re-opening FP-2, FP-3, FP-4, and MF-2 contradicts 01D. The leftovers in section 5 are the ones that stay open.

An Attributes-only asmdef was not shown to fail on 0.7 by a compile log. Do not cite a log that does not exist.

---

## 11. Decision table

Calls a later agent may repeat without new evidence. "BUILD NOW" for P0-06 and the S-02 holes means those commits already exist. It does not mean "do them again."

| Item | Call | One-sentence evidence |
| --- | --- | --- |
| P0-06 | BUILD NOW, and it is committed | Unchanged Nexus fails CS0246 on Pipeline 0.8. The dual reference compiles on these two editors and the drift suite passes on both. 0.8 did not repeat the other three suites. |
| S-02 required holes | BUILD NOW, and it is committed | MF-1, MF-2, FP-2, FP-3, and FP-4 are closed in `5ecc7fb`. The leftovers in section 5 stay open. |
| S-05 | TEST FIRST | Structured `CS0103` fields exist for one file. Exit 6 is also a network error and an HTTP 401. CS1030 is in the editor log and absent from the JSON. |
| S-06, S-03, L-01, L-03, L-04, C-04, S-10, S-11 | WAIT | No new measurement in this series. |
| L-02 | WAIT | An in-flight recompile call can fail with a network error while a later status object is clean. The JSON does not say domain reload. |
| S5, S6-P, Mixed, Gate F | WAIT | The cheap experiment did not run. S6-P is not a production design. |
| S-07 removal | DROP as a deletion | JPEG and external file delivery are still different. Overlay parity and scene framing are unknown. |

No architecture was selected. H1–H5 are unanswered.

---

## 12. Statistics that exist, and statistics that do not

Exist:

- Compiler: 108 CS0246 lines, 6 unique names, 1 file, Unity exit 1 unchanged; exit 0 after the dual reference.
- Drift: pre-fix 2 passed / 1 ignored on 0.8; post-fix 3 passed / 0 skipped on 0.8 and on 0.7. Plus 0.7 envelope 29, registry 9, capture look 10.
- Recompile walls and exits in section 6. Three structured errors, no truncation at three, one file, all `CS0103`.
- Capture byte sizes, dimensions, one sha256 prefix, pixel samples, and the two timing triples for Nexus JPEG (cold CLI and warm HTTP).
- Tag index width: 50 tags on the 7810 editor. Nexus tag count 3, the three wrappers.
- Discovery: 0 agent runs, 0 tokens, 2 tasks passed self-check, 3 tasks dropped, 2 not planted, 1 spare unused.

Do not exist, and must not be invented:

- Arm scores, false-success counts, tokens per success, uncached tokens, discovery calls, wrong-tool calls, wall time per task.
- A 0.7 recompile JSON to compare with the 0.8 object shape.
- A successful `unity recompile` body that contains or omits CS1030.
- A server-side duration for the no-change compile.
- Proof that `--strict` fails on a warning.
- Proof that scene framing works or is broken when the target argument actually arrives.
- Pixel equality between Nexus and official captures.
- A second Pipeline 0.7 build or a second 0.8 build.
- Envelope, registry, and capture-look results on 0.8.
- An HTTP `GET /api/commands` body.

Prior series, not re-measured here, and not to be "confirmed" by this file: warm file look about 232 ms then about 9 ms; client round trip about 100 ms; handler under 1 ms; Unity `source=screen` on 0.7 Edit Mode returned HTTP 400. On 0.8, screen succeeds once a Game view target exists and fails when it does not.

---

## 13. Production changes

- `5ecc7fb` — `Editor/Commands/CaptureGameViewCommand.cs`, `Editor/Commands/GroupCompileErrorsCommand.cs`, `Tests/Editor/NexusCommandCliDriftTests.cs`. Internal static seams so tests can see the integer and the request. Behavior for callers is the same delegate default.
- `596d171` — `Editor/Pipeline/UnityMCP.Editor.Pipeline.asmdef` gains `Unity.Pipeline.Attributes` beside `Unity.Pipeline`. `CHANGELOG.md` under `[1.7.0]` Fixed records that 0.7 and 0.8 both compile. README and `API_REFERENCE` were not changed.

Not changed: `MASTER_PLAN_V2.md`, capture implementation behavior beyond the test seam, tool schemas, the harness project.

---

## 14. What was deliberately not implemented

S-05, S5, S6-P, Mixed, L-02, a façade, broad routing, generic invoke, eval forwarding, a write façade, Gate F, and any deletion of S-07. The discovery arms were not scored. `DiscBuild` / `YardOpen` / `NoticeBoard` exist only in the scratch project, not in the package.

---

## 15. Ranked next actions

1. Do not restart discovery on port 7800. A fair editor is `m_Port` 7810, project `/tmp/nexus-p08-harness`, binary `/tmp/unity-cli-beta12/unity`, and `--project-path` after the word `command`. The last attempt aborted at the self-checks. Repair the fixture so at least five tasks pass those checks, then start agents. Do not score the abort.
2. Before any S-05 command, fixture the three exit-6 bodies and a warning that is inside the `unity recompile` call itself.
3. Repeat scene framing only with a call that actually receives the target, and with a scene camera aimed at the marker.
4. If the drift suite is edited again, snapshot view and delivery, send `max_logs: 0`, and fail when `Execute` discards the hooked log list. On 0.8, repeat envelope, registry, and capture-look, not only the three drift tests.
5. Point the scratch `file:` package at a worktree that is actually `596d171` before the next 0.8 compile. The current worktree is detached at `bfdd0f7` with a dirty copy of the same edits.

---

## 16. Gotchas a later agent will otherwise relearn

- Shell `rg` is not installed. Use the editor search.
- `git` worktree status on this submodule needs `--git-dir` and `--work-tree`.
- UTF 1.6 `-testFilter` is a group-name regex. Exit 0 with zero tests is a false pass. `testables` is required for package tests.
- CLI `--format` collides with Nexus `--format`. Use `--` before command arguments. Named flags (`--name`, `--target`) work after the command name. `name=Value` was rejected for `find_gameobjects`.
- `open_scene` on a dirty in-memory scene can block the main thread until the editor is killed. Kill only the scratch PID. Confirm the command line contains `/tmp/nexus-p08-harness` or `/private/tmp/nexus-p08-harness`. Never kill PID 4465.
- A Python loop that `kill`s every command line containing the project path will also kill the shell whose script text contains that path. Kill by PID.
- Official capture `save_path` is forced under `Assets/` and writes `.meta`.
- Missing sprite references serialize in YAML as a guid and come back from `get_serialized_fields` as `null`.
- `base64_ms` is still populated on file delivery. Do not reopen that as a new finding.
- Do not commit `.graphifyignore`, `graphify-out.meta`, `.DS_Store`, or `__pycache__`.

---

## 17. Evidence map

All under `NexusFeature~/v2_analysis/I_08_compat_validation/` unless noted.

| File | What it is |
| --- | --- |
| `00_BASELINE.md` | Branch and tip at the start of the compatibility sprint |
| `01_S02_FALSE_PASS.md` | What the drift tests observe |
| `01A_FALSE_PASS.md`, `01B_TEST_INDEPENDENCE.md` | Pre-edit reviews |
| `01C_FIX_REVIEW.md` | Review of the first patch; four holes still open at that time |
| `01D_SEAM_REVIEW.md` | Those four closed; commit not stamped |
| `02_PIPELINE_08_COMPILE.md` | Q1–Q6 |
| `03_RECOMPILE_CONTRACT.md` | R1–R6 |
| `04_CAPTURE_08.md` | Pixel comparison and S-07 classes |
| `05_CHECKPOINT.md` | Stop before discovery, later updated for the commits |
| `06_DISCOVERY_PREREG.md` | Frozen protocol, zero scores inside the file |
| `07_DISCOVERY_RESULTS.md` | Abort at the self-checks |
| `08_SYNTHESIS.md` | Decision table. Its first next-action is older than section 9.3 of this report |
| `09_ADVERSARIAL_REVIEW.md` | Objections. Written before the prereg file existed |
| `S05_INPUT_CONTRACT.md` | Official primitive versus Nexus additions. Not an implementation ticket |
| `evidence/pipeline08/compile-08.log` | Raw CS0246 |
| `evidence/pipeline08/compile-08-ref.log` | Clean 0.8 batch compile |
| `evidence/pipeline08/drift-*.xml` | The three drift runs that count, plus the empty ones that do not |
| `evidence/mf_07/` | 0.7 suites after the seam |
| `evidence/contracts08/` | Recompile JSON, catalog, pixels, images, CS1030 excerpt |
| `H_2026-10-validation/` | Previous sprint. Do not rerun that research to answer a question this file already measured |

Scratch-only, not in the package repo: `/tmp/nexus-p08-harness`, `/tmp/nexus-p08-wt`, `/tmp/nexus-p08-logs/`, `/tmp/unity-cli-beta12/unity`, `/tmp/disc-runner/ucli.py`.
