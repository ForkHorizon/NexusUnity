# Pipeline 0.8 scratch compile

Measured 2026-10-05. The live harness (PID 4465, Pipeline 0.7.0-exp.1, port 7800) was not upgraded and its Library was not reused.

## Environment

| Item | Value |
| --- | --- |
| Editor | Unity 6000.3.16f1 (a56f230f6470), `/Applications/Unity/Hub/Editor/6000.3.16f1` |
| Scratch project | `/tmp/nexus-p08-harness` (created with `-createProject`, then a local manifest) |
| Package | file package `com.forkhorizon.nexus.unity` → detached worktree `/tmp/nexus-p08-wt` at `bfdd0f7` |
| Pipeline | `0.8.0-exp.1`, unity `6000.0`, cache `com.unity.pipeline@62c08c808737`, fingerprint `62c08c808737255821f3449466689a527a2eea50` |
| Test framework resolved | `com.unity.test-framework@1.6.0` (`aebcb98460c6`) even though the manifest asked for 1.7.0 |
| CLI used for this compile | none. Batchmode `-runTests` / compile only. Isolated CLI `1.0.0-beta.12` is at `/tmp/unity-cli-beta12/unity` (sha256 `7f21746fe7b2cf1010a7f293763ad9590715166147dcc0401a7b52de0d95f107`). `~/.unity/bin/unity` is still `1.0.0-beta.10`. |

`Packages/manifest.json` in the scratch project lists the file package, `com.unity.pipeline` `0.8.0-exp.1`, Input System `1.19.0`, Newtonsoft `2.0.0`, and `"testables": ["com.forkhorizon.nexus.unity"]`. Without `testables`, `UnityMCP.Editor.Tests` is not compiled (`UNITY_INCLUDE_TESTS`), and `-runTests` exits 0 with `testcasecount=0`.

Unity Test Framework 1.6 stores `-testFilter` in `Filter.groupNames` (a regex), not as an NUnit name. `SettingsBuilder.cs` in that package. A filter that matches nothing is still exit 0.

## Q1 — unchanged Nexus against Pipeline 0.8

No. Exit 1. Log: `evidence/pipeline08/compile-08.log` (1409 lines). Unity printed `Aborting batchmode due to failure: Scripts have compiler errors.`

Every error is CS0246 in `/tmp/nexus-p08-wt/Editor/Pipeline/NexusPipelineCommands.cs`. 108 error lines, 6 unique missing names, repeated across import passes:

- `CliCommandAttribute`
- `CliCommand`
- `CliArgAttribute`
- `CliArg`
- `Tags`
- `MainThreadRequired`

No other file reported `error CS`.

## Q2 — where the types live on 0.8

Probe written by an Editor script during a later empty batch run, after the dual reference compiled. Raw file: `evidence/pipeline08/probe.txt`.

| Type | Assembly | Namespace | Package path |
| --- | --- | --- | --- |
| `CliCommandAttribute` | `Unity.Pipeline.Attributes` | `Unity.Pipeline.Commands` | `Runtime/Attributes/CliCommandAttribute.cs` |
| `CliArgAttribute` | `Unity.Pipeline.Attributes` | `Unity.Pipeline.Commands` | `Runtime/Attributes/CliArgAttribute.cs` |
| `CommandRegistry` | `Unity.Pipeline` | `Unity.Pipeline.Commands` | `Runtime/Common/CommandRegistry.cs` |

`Unity.Pipeline.Attributes.asmdef` has `"references": []` and `"autoReferenced": true`. Custom asmdef references are not transitive, so auto-reference did not give `UnityMCP.Editor.Pipeline` those attributes. `Unity.Pipeline` itself references `Unity.Pipeline.Attributes`.

On the installed 0.7 cache, `CliCommandAttribute` is still in assembly `Unity.Pipeline` (`Runtime/Common/CliCommandAttribute.cs`). Assembly `Unity.Pipeline.Attributes` exists on 0.7 and does not contain that type.

## Q4 — `NexusPipelineCommands` before the reference

It does not compile. The CS0246 list is that file.

## Dual reference, 0.8 only

Worktree `Editor/Pipeline/UnityMCP.Editor.Pipeline.asmdef` references `UnityMCP.Editor`, `Unity.Pipeline`, and `Unity.Pipeline.Attributes`. `Unity.Pipeline` was not removed.

Recompile log `evidence/pipeline08/compile-08-ref.log` contains `Exiting batchmode successfully now!` and no `error CS`. The probe then found `UnityMCP.Editor.Pipeline.NexusPipelineCommands` in `UnityMCP.Editor.Pipeline.dll`.

## Q3 — drift suite

Both runs used `-assemblyNames UnityMCP.Editor.Tests -testFilter NexusCommandCliDriftTests` after `testables` was set. Two earlier runs with the same exit code executed zero tests; those XML files are not evidence.

Committed suite (worktree tests still at `bfdd0f7`, production asmdef already dual-referenced so the projection compiled):

`evidence/pipeline08/drift-committed-on-08.xml`

- total 3, passed 2, failed 0, skipped 1, result `Skipped:Ignored`
- `CliArgsMatchCanonicalDescriptors` ignored with `com.unity.pipeline is not loaded. CLI drift is checked when Pipeline is installed.`
- Pipeline was loaded. The committed probe looks only in assembly `Unity.Pipeline`. On 0.8 the attribute is in `Unity.Pipeline.Attributes`, so the probe takes the Ignore branch. That message is false.

New suite (MF-1/MF-2/MF-3 tests, before the capture-gateway seam) on the same 0.8 project:

`evidence/pipeline08/drift-new-on-08.xml`

- total 3, passed 3, failed 0, skipped 0
- `CliArgsMatchCanonicalDescriptors` ran. The attribute was found. The three wrapper names, help strings, and parameter defaults matched the descriptors inside that test.

## Q5–Q6

Read from the live 0.8 catalog, `evidence/contracts08/catalog_nexus.json`, while the scratch editor was up. Project path in the response was `/private/tmp/nexus-p08-harness`.

The three wrappers are present:

| Catalog name | Description | Required |
| --- | --- | --- |
| `nexus_capture_game_view` | Captures Game View Overlay or a Scene View framed on a target. JPEG by default, longest edge 768 unless raised. | `[]` |
| `nexus_group_compile_errors` | Analyzes recent console logs, extracts C# compiler errors (CSxxxx), and groups them by root cause file. | `[]` |
| `nexus_project_map` | Generates a curated high-level project intelligence map for AI agents. | `[]` |

`max_logs` default in the catalog is 50. Capture defaults are width 0, height 0, format `jpg`, quality 85, max_dimension 768, view `game`, delivery `base64`. `nexus_look` is not a catalog name. A call to that string returns Command Not Found. The unlisted alias is HTTP-side, not a second CLI command.

## 0.7 and the capture seam

The same three-name reference list is now on the main package asmdef. The live 0.7 editor (PID 4465, Pipeline 0.7.0-exp.1) recompiled it. EditMode results in `evidence/mf_07/` after that reload:

- NexusCommandCliDriftTests 3 passed, 0 failed, 0 skipped
- NexusCommandEnvelopeTests 29 / 0 / 0
- NexusCommandRegistryTests 9 / 0 / 0
- CaptureLookTests 10 / 0 / 0

The same tree, including the capture-gateway seam, ran on the 0.8 scratch project: `evidence/pipeline08/drift-seam-on-08.xml` is total 3, passed 3, failed 0, skipped 0. `CliArgsMatchCanonicalDescriptors` ran.

That is compile evidence for these two editors: Pipeline `0.7.0-exp.1` on PID 4465, and Pipeline `0.8.0-exp.1` in the scratch project. On 0.8 the repeated suite is the drift class only (`testcasecount` 3). Envelope, registry, and capture-look ran on 0.7. The parent 0.8 assembly count of 216 was not filtered in.

Q5 and Q6 above are the Pipeline CLI catalog in `catalog_nexus.json`. The response `projectPath` is the scratch project. This sprint did not separately save an HTTP `GET /api/commands` body. `01D_SEAM_REVIEW.md` closed FP-2, FP-3, FP-4, and MF-2 and did not stamp a commit. The product commits are `5ecc7fb` (drift spies) and `596d171` (asmdef plus changelog). They are local.
