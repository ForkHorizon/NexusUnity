# S-02 false-pass fixes

Reviews before the edit: `01A_FALSE_PASS.md`, `01B_TEST_INDEPENDENCE.md`. Review of the first patch: `01C_FIX_REVIEW.md`. The seam follow-up is `01D_SEAM_REVIEW.md` when that reviewer finishes.

## What was wrong

`NexusCommandCliDriftTests` could stay green while HTTP `max_logs` and `format` diverged, because it compared empty error JSON and called `FormatOrDefault` for the expected value.

On Pipeline 0.8 the committed test also ignored the CLI lock. Measured XML `evidence/pipeline08/drift-committed-on-08.xml`: 2 passed, 1 skipped. The skip message was `com.unity.pipeline is not loaded`. Pipeline 0.8 was loaded. `CliCommandAttribute` is in assembly `Unity.Pipeline.Attributes`, and the old probe only searched `Unity.Pipeline`.

## What the tests observe now

- `max_logs`: descriptor default, CLI `DefaultValue`, `DefaultMaxLogs`, `TryReadCompileArguments` of `{}` and of `{max_logs:7}`, plus a `ReadErrorLogs` spy on `Execute()`, `ExecuteAsync({})`, `ExecuteAsync({max_logs:7})`, and the reflected CLI method invoked with 50 and with 7. Expected integers are literals.
- `format` and size: `ExecuteAsync`, `ExecuteFromCli`, and the reflected `nexus_capture_game_view` method are called. A `Capture` seam records the `CaptureRequest` that would be sent to the gateway. Expected format is the literal `CaptureFormat.Jpeg` or `Png`. `FormatOrDefault` is not used as the oracle.
- Wrong names `w`, `h`, and `jpeg_quality` are sent through `ExecuteAsync`. Height, width, quality, and max edge must stay at the omit defaults.
- Explicit `width=12`, `height=9`, `format=png`, `quality=40`, `max_dimension=100` must arrive at the seam from both HTTP and CLI.
- If `Unity.Pipeline` or `Unity.Pipeline.Attributes` is loaded and `Unity.Pipeline.Commands.CliCommandAttribute` is in neither, the test fails. Ignore remains only when neither assembly is loaded.

## Runs

| Host | Result |
| --- | --- |
| Live 0.7, after the seam and the dual asmdef reference | drift 3, envelope 29, registry 9, capture look 10. Failed 0, skipped 0. `evidence/mf_07/` |
| Scratch 0.8, seam suite | `evidence/pipeline08/drift-seam-on-08.xml`: 3 passed, 0 skipped. `CliArgsMatchCanonicalDescriptors` ran. |

Quality gate on the package after the edit: 0 errors, the same 5 pre-existing warnings. Pytest 47 passed. The full `prepush-validate.sh --static-only` script exits 1 on pre-existing `graphify-out` and on `__pycache__` that the script's own pytest writes. The deterministic gate command itself is the 0-error result.
