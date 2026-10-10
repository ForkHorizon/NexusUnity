# 01B — Test independence of the CLI drift lock

Reviewed commit `bfdd0f7e71c851fb4615956f3c420940a9623220`. Read-only. Tests were not edited. Unity was not run. Port 7800 was not contacted. Sources: `Tests/Editor/NexusCommandCliDriftTests.cs`, `GroupCompileErrorsCommand.ExecuteAsync` through `Execute`, `CaptureGameViewCommand` lines 60–210, `CaptureLookRequest.TryParse`, and `S02_ADVERSARIAL_REVIEW.md`.

`OmittedHttpArgumentsUseTheSameDefaults` always runs. It has no Pipeline probe. Ignore versus Fail does not apply to it. It applies only to `CliArgsMatchCanonicalDescriptors`, and therefore to every default check inside `AssertParameters`.

## Omitted HTTP assertions

`DefaultOf` is not a behavior method. It reads `NexusCommandParameter.DefaultValue` from `CreateDescriptor` / `Optional`.

| Line | Observes | Expected | Omit path can change while this line stays green? |
| --- | --- | --- | --- |
| 68 | descriptor `width` | literal `0` | Yes. Does not read HTTP or CLI. `TryParse` uses its own `width ?? 0`. CLI default is `width = 0`. |
| 69 | descriptor `height` | literal `0` | Yes. Same split. `height ?? 0` and CLI `height = 0` are unread. |
| 70 | descriptor `instance_id` | literal `0` | Yes. `TryReadView` uses `id ?? 0`. CLI default is `instanceId = 0`. |
| 71 | descriptor `format` | literal `"jpg"` | Yes. Descriptor stores `DefaultFormat`. HTTP applies format only in `ExecuteEnveloped`. CLI pre-applies `FormatOrDefault`. Neither is read. |
| 72 | descriptor `quality` | literal `85` | Yes. Descriptor stores `DefaultJpegQuality`. This line does not read `JpegQuality`. |
| 73 | descriptor `max_dimension` | literal `768` | Yes. Descriptor stores `DefaultMaxLongEdge`. `TryReadEdge` is not read here. |
| 74 | descriptor `view` | literal `"game"` | Yes. Descriptor stores `DefaultView`. The enum `TryParse` produces is not read here. |
| 75 | descriptor `delivery` | literal `"base64"` | Yes. Descriptor stores `DefaultDelivery`. `WriteFile` is not read here. |
| 76 | descriptor `target` | literal null (`IsNull`) | Yes. `TargetPath` is not read here. |
| 77 | descriptor `request_id` | literal null | Yes. `call.RequestId` is not read. Project-map HTTP omit is not in this test. |
| 79 | `TryParse(new JObject(), …)` return | literal `true` | Yes for any successful omit. A different width, format, or limit still returns true. |
| 80 | `call.Rejected` | literal null | Yes. Success with different defaults leaves `Rejected` null. |
| 81 | `Request.RequestedWidth` after `TryParse` | descriptor via `DefaultOf` | This line alone is circular: both sides can move together. With line 68, HTTP width omit must be `0`. `ExecuteAsync` calls `TryParse` and does not overwrite width. CLI is not called. `CaptureRequest.RequestedWidth` also defaults to 0, so a deleted assignment would still read 0. The value HTTP uses cannot leave 0. |
| 82 | `RequestedHeight` | descriptor via `DefaultOf` | Same as width. Strict for HTTP height only together with line 69. |
| 83 | `JpegQuality` | descriptor via `DefaultOf` | Alone, circular through `DefaultJpegQuality`. With line 72, the number must be 85. `TryParse` assigns `quality ?? DefaultJpegQuality`. The property initializer is also 85, so deleting the assignment still reads 85. The HTTP number cannot leave 85. CLI is not called. |
| 84 | `MaxLongEdge` | descriptor via `DefaultOf` | Alone, circular through `DefaultMaxLongEdge`. With line 73, HTTP omit must be 768. `TryReadEdge` uses `parsed ?? DefaultMaxLongEdge`. The property default is 0, not 768, so a deleted assignment fails this line. CLI is not called. |
| 85 | `TargetInstanceId` | descriptor via `DefaultOf` | Alone, circular. With line 70, HTTP omit must be 0 (`id ?? 0`). |
| 86 | `TargetPath` | literal null | No for the null contract. Omitted target stays null in `TryReadOptionalString`. CLI is not called. An explicit empty string is a different input and is rejected before this field is set. |
| 87 | `Request.View` | literal `CaptureView.Game` | No for game versus scene. `TryReadView` maps `DefaultView` to `Game`, and line 74 plus line 88 force that constant to `"game"`. A third token that still maps to `Game` stays green only if omit stops using `DefaultView`. The applied string is not read. Enum default is also `Game`, so a deleted assignment still reads `Game`. |
| 88 | `DefaultView` constant | descriptor via `DefaultOf` | Does not read `TryParse`. With line 74 the constant must be `"game"`. `TryReadView` can stop consulting it and this line stays green. |
| 89 | `WriteFile` | literal `false` | No for base64 versus file. `writeFile = delivery == "file"`, and lines 75 and 90 force `DefaultDelivery` to `"base64"`. A non-file token stays green. The string is not read. |
| 90 | `DefaultDelivery` constant | descriptor via `DefaultOf` | Same shape as line 88. Does not read the applied delivery. |
| 91 | `call.Format` (the raw string) | literal null | Yes. This locks the wrong layer. `TryParse` stores the raw string and does not set `Request.Format`. `CaptureRequest.Format` defaults to `CaptureFormat.Png` (`CaptureModels.cs`). HTTP jpeg is applied later: `ExecuteEnveloped` line 136, `ParseFormat(FormatOrDefault(call.Format))`. Replacing that with `?? "png"`, or deleting it, leaves this line green. CLI `ExecuteFromCli` writes `FormatOrDefault` into JSON before `TryParse`, so CLI is not this null. |
| 92 | return value of `FormatOrDefault(call.Format)` | descriptor via `DefaultOf` | Yes. The test calls the helper. Both sides are `DefaultFormat` when format is null, so the helper and the descriptor move together. Line 71 stops the constant leaving `"jpg"`, but `ExecuteEnveloped` can stop calling `FormatOrDefault` and this line stays green. `Request.Format` is never compared. |
| 95 | descriptor `max_logs` | literal `50` | Yes. Descriptor stores `DefaultMaxLogs`. HTTP uses a separate `maxLogs ?? DefaultMaxLogs` (`GroupCompileErrorsCommand.cs` line 69). |
| 97 | `Execute(int maxLogs = …)` `ParameterInfo.DefaultValue` | descriptor via `DefaultOf` | This locks `Execute()`'s C# default, not HTTP. With line 95 that default is 50. HTTP passes an explicit int into `Execute`, so the C# default is not the omit path. `?? 10` in `ExecuteAsync` stays green. CLI uses `NexusPipelineCommands.GroupCompileErrors`, not `Execute()`'s default. |
| 99 | `ExecuteAsync(new JObject()).IsCompleted` | literal `true` | Yes. `Task.FromResult` completes for any limit. |
| 106 | JSON of that HTTP envelope versus `ExecuteEnveloped((int)DefaultOf(max_logs), null)` after zeroing `meta.ms` and `data.execution_duration_ms` | the other production envelope | Yes. `NexusCompileErrorsResult` has no `max_logs` field. `Execute` keeps only messages that contain `"error CS"`. Equal CS sets, including an empty console, match for limit 50 versus 10 or 0. The integer `ExecuteAsync` passed is not read. Zeroing the clocks hides timing. `meta.request_id` is compared, and the oracle passes null, so a generated compile request id would fail this line. That is the only request-id lock, and it is incidental. |

Lines 81–85 are strict for the HTTP numbers only because the literal descriptor pins sit above them. Lines 91–92 and 95–106 are not strict for the format that is encoded or for the compile limit HTTP uses.

## Default checks in `AssertParameters`

These run only after the probe below. On Ignore, none of them run, so every CLI default can change and the suite stays green.

Three checks, per reflected parameter, by index:

1. `spec.HasDefault` is literal `true`. `Optional` always sets that flag. It does not lock the number. A descriptor that omitted the flag fails. HTTP behavior is not read.
2. `parameter.HasDefaultValue` is literal `true`. Removing the C# default fails. An attribute-only default does not pass. The value is not read.
3. `parameter.DefaultValue`, with `DBNull` mapped to null, equals `spec.DefaultValue`. The expected value is the descriptor, not a literal. The observed value is the CLI method's C# default. The check is circular unless another literal pins the descriptor.

What that third check sees today, from `NexusPipelineCommands`:

| CLI parameter | C# default it observes | Literal pin elsewhere |
| --- | --- | --- |
| project map `requestId` | `null` | None. Descriptor and CLI can move to the same string together. HTTP project map is never called. |
| compile `maxLogs` | `DefaultMaxLogs` | Line 95 pins the descriptor to 50, so this must be 50 when the test runs. HTTP `??` is a different expression. |
| compile `requestId` | `null` | None. Same circle as project map. |
| look `width`, `height`, `instanceId` | literals `0` | Lines 68–70. |
| look `format` | `DefaultFormat` | Line 71 pins `"jpg"`. This does not read `ExecuteFromCli`'s pre-apply. |
| look `quality` | `DefaultJpegQuality` | Line 72 pins 85. |
| look `maxDimension` | `DefaultMaxLongEdge` | Line 73 pins 768. |
| look `view` | `DefaultView` | Line 74 pins `"game"`. |
| look `target` | `null` | Line 76. |
| look `delivery` | `DefaultDelivery` | Line 75 pins `"base64"`. |
| look `requestId` | `null` | Line 77 pins the look descriptor, so the CLI default must stay null when this test runs. `call.RequestId` is still unread. |

`CliArgsMatchCanonicalDescriptors` has no other default assertion. Help, order, required, and CLR type are outside this note. `cliCommand` from `FindType` is never used after the null check. Attributes match `GetType().Name`, not that `Type`.

## Ignore versus Fail

Current probe: `FindType("Unity.Pipeline", "Unity.Pipeline.Commands.CliCommandAttribute")`. Null means Ignore. That null is both "assembly not loaded" and "assembly loaded, type absent".

| Situation | Current | Correct? |
| --- | --- | --- |
| Neither `Unity.Pipeline` nor `Unity.Pipeline.Attributes` is loaded | Ignore | Yes. Legacy is supported. `UnityMCP.Editor.Pipeline.asmdef` is behind `NEXUS_HAS_PIPELINE`. The test asmdef must not reference Pipeline. |
| `Unity.Pipeline` is loaded and the attribute type is missing from it (moved, renamed, or only in `Unity.Pipeline.Attributes`) | Ignore | No. This is present-but-attribute-missing. The CLI lock is skipped. On the claimed 0.8 layout that is a false pass. |
| Attribute found, `NexusPipelineCommands` missing | `Assert.IsNotNull` Fail | Yes, but only if the attribute probe did not Ignore first. |

Package CI that never loads either assembly still Ignores. A skip counted as success does not make Ignore the wrong local result. It means this test is not a CI signal.

## Intended fixes (not implemented)

### MF-1 — independent, not circular

`TryReadCompileArguments` returning the resolved int, the test calling it with `new JObject()` and asserting `maxLogs == 50`, and separate literal asserts that the descriptor, `Execute`'s default, and `DefaultMaxLogs` are all 50, drops the circular envelope oracle. The expected integer is a literal. Console contents cannot keep a wrong limit green. `Execute()`'s default being 50 is no longer mistaken for the HTTP coalesce.

False pass that remains:

- The test does not watch the int `ExecuteAsync` passes to `ExecuteEnveloped`. A second expression after the shared call stays green. Independence holds only while HTTP's only resolution is that out int.
- CLI still does not call the method. Its `= DefaultMaxLogs` is locked only when `AssertParameters` runs.
- Dropping JSON equality drops the incidental `meta.request_id` compare. The new method returns `requestId`, but the stated asserts never read it. A generated id stays green.
- `Execute(int maxLogs = 50)` is still not on the HTTP or CLI path. Pinning it is extra, not proof.

### MF-2 — independent, not circular

`TryPrepareLook` doing `TryParse` and then `ParseFormat(format ?? "jpg")` in one place, the test asserting `call.Request.Format == CaptureFormat.Jpeg` on an empty object, and not calling `FormatOrDefault` for the expected value, closes the real hole. Unset `Request.Format` is `Png`. Jpeg is a literal enum, not the helper and not `DefaultFormat`. The descriptor assert against literal `"jpg"` stays a separate pin. Sending CLI through the same method, without a pre-applied format, is what stops `ExecuteFromCli` line 109 from diverging.

False pass that remains:

- The test never calls `ExecuteAsync` or `ExecuteFromCli`. A later write in `ExecuteEnveloped`, or a CLI JSON pre-apply before the shared method, stays green. The enum assert locks only the object the test built.
- `ParseFormat` maps `""`, `"jpg"`, and `"jpeg"` to `Jpeg`. The enum assert does not pin the string `"jpg"` on the applied path. That is still JPEG, not a png hole.
- Two copies of the literal `"jpg"` are not detectable. Behavior diverges only when those copies disagree and one of them is actually called.
- View and delivery strings stay unread. Their game/scene and base64/file outcomes are already pinned by lines 87 and 89 and do not need this fix.

### MF-3 — independent policy, not a value oracle

Ignore only when neither `Unity.Pipeline` nor `Unity.Pipeline.Attributes` is loaded: correct. Fail when either assembly is loaded and `Unity.Pipeline.Commands.CliCommandAttribute` is in neither: correct, and it fixes today's false Ignore. Searching both assemblies makes the stated 0.8 move run the test instead of skipping it. `NexusPipelineCommands` missing while Pipeline is loaded must stay a Fail after that probe. If 0.8 is loaded but the Nexus pipeline assembly did not compile, Fail is the right result, not a false pass.

False pass that remains:

- Neither assembly loaded is still Ignore. CI that does not install Pipeline, and does not fail on skipped tests, still never runs the CLI lock.
- The stated fix does not use the resolved `Type` as the attribute filter. `AttributeNamed` short-name equality can still read a decoy `CliCommandAttribute` or `CliArgAttribute`.
- A third assembly name, with neither of these two loaded, still Ignores.
- This probe does not lock HTTP omit values.

## Bottom line

Lines 68–77 pin descriptors to literals and do not observe omit behavior. Lines 81–90, read with those literals, do pin HTTP width, height, quality, max edge, instance id, null target, game versus scene, and base64 versus file. Format and `max_logs` do not: format's expected value is `FormatOrDefault`, and `max_logs` is proved by JSON that does not contain the limit. MF-1 and MF-2 are the right shape and are not circular. Each still passes if production stops using the shared method's result. MF-3's Ignore versus Fail split is right; the CLI assertions remain unrun wherever both Pipeline assemblies are absent.
