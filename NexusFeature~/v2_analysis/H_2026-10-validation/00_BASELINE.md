# Baseline validation — 2026-10-04

MEASURED on one Editor. No product code changed. No dependency upgraded.

## Identity

| Item | Value |
|---|---|
| Package | `com.forkhorizon.nexus.unity` 1.7.0, Unity min `6000.0` |
| Branch | `rework2/S-02` |
| Commit | `bfdd0f7e71c851fb4615956f3c420940a9623220` |
| Parent of tip | `23005b4` (S-07). Stack also contains `aa923f6`, `060cc13`, `6fff2e8`, `d738dfc`. Not rebased. |
| `development` | `329990e8` (ancestor). This stack is not merged. |
| Unity | `6000.3.16f1` PID 4465 |
| Pipeline | `com.unity.pipeline` `0.7.0-exp.1` cache `com.unity.pipeline@ab05e9cd7f74` |
| CLI installed | `1.0.0-beta.10` at `/Users/daliys/.unity/bin/unity` |
| Port | 7800, `editor_status` ready, compiling false, domain reload false, play mode stopped |
| Focus | not recorded |
| Model | this validation did not call an LLM |

Raw JSON: `evidence/baseline/`. Tokens were redacted before write. Per-test names: `evidence/baseline/per_test.json`.

## EditMode suites (separate counts)

`run_tests` mode `editor`, filter_type `testName`, one filter per class. HTTP 200, Pipeline `success=true`, skipped 0, inconclusive 0. Runner `Duration` is about 5.2–6.8 s per invocation; almost all of that is runner overhead. The test bodies are the `Duration` values below.

| Suite | Passed | Failed | Skipped | Runner seconds | Slowest test body |
|---|---:|---:|---:|---:|---|
| NexusCommandCliDriftTests | 3 | 0 | 0 | 5.64 | CanonicalSchemasStayFreeOfCliDefaultFields 0.012 s |
| NexusCommandRegistryTests | 9 | 0 | 0 | 5.21 | ListToolsProjectsSharedDescriptionsAndKeepsLegacyScreenshot 0.010 s |
| OpenSourceApiContractTests | 6 | 0 | 0 | 5.29 | ApiReferenceDocumentsRawAndBridgeSurfaces 0.040 s |
| EditorReadinessTests | 17 | 0 | 0 | 5.23 | AlreadyReady_WhenNothingIsPending 0.003 s |
| NexusCommandEnvelopeTests | 29 | 0 | 0 | 6.81 | ClampedQualityAndNegativeWidth_AreNotInvalidArgument 1.292 s |
| CaptureGatewayTests | 10 | 0 | 0 | 5.61 | MissingGameViewFailsWithDomainError 0.278 s |
| CaptureLookTests | 10 | 0 | 0 | 5.21 | SceneCaptureFramesATargetAndRestoresTheView 0.017 s |

Sum of passed tests: 84. That sum is not a single suite. CliDrift did not Ignore: `CliArgsMatchCanonicalDescriptors` passed, so `CliCommandAttribute` was found in assembly `Unity.Pipeline` on this 0.7 Editor.

`SceneCaptureFramesATargetAndRestoresTheView` passed in 0.017 s. That does not prove an encoded JPEG was pixel-checked. The test does not decode pixels.

## Python

`python3 -m pytest Editor/tests -q` from the package root.

MEASURED: 47 passed, 9 subtests passed, 1.15 s, exit 0. Log: `evidence/baseline/pytest.txt`.

This is the first Python run recorded at tip `bfdd0f7`. The previous sprint ran these 47 tests at S-04 (`aa923f6`) and did not re-run them for S-07 or S-02.

## Quality gate

`dotnet run --project tools~/NexusQualityGate/NexusQualityGate.csproj -- --root . --ai off --format github`

MEASURED: 0 errors, 5 warnings, exit 0. Same five warnings as the previous sprint (pre-existing):

- `CaptureInspectorScreenshot` 52 lines
- `TryCaptureVisualElement` 75 lines
- `MCPServerMethods.Tools.cs` 328 lines
- `MCPServerMethods.cs` 313 lines
- `NexusRuntimeCapabilities.cs` 343 lines

Log: `evidence/baseline/quality_gate.txt`.

## Not in this baseline

- No whole-package Unity suite beyond the seven classes above.
- No Play Mode suite.
- No second Editor.
- No Pipeline 0.8 and no CLI beta.12. Those exist upstream; see `00_UPSTREAM_PRIMARY_SOURCES.md`.
- S-02 adversarial review, static only, no Editor: `evidence/baseline/S02_ADVERSARIAL_REVIEW.md`. Verdict: the three tests passed and did not Ignore on 0.7, and the lock still has two false-pass holes (format is checked through `FormatOrDefault`, `max_logs` is checked by JSON equality on an empty CS-error set). A moved `CliCommandAttribute` still `Assert.Ignore`s. Package CI does not install Pipeline and does not fail on skipped tests.
- P0-02's 20/20 live compile evidence is the older commit `060cc13` on build `6fff2e8`. It was not re-run today. Readiness EditMode 17/17 passed on this tip.

## Worktree

Untracked, not committed: this research directory, `NexusFeature~/HANDOFF_IMPLEMENTATION_2026-10-04.md`, `.graphifyignore`, `graphify-out.meta`. Product source matches `bfdd0f7`.
