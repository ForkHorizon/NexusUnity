# 01A false-pass — `NexusCommandCliDriftTests`

Commit `bfdd0f7e71c851fb4615956f3c420940a9623220`. Read-only. Unity was not run. Port 7800 was not contacted. Product code was not edited. Paths are CONFIRMED when the line was read; "sibling would fail" is INFERRED from that sibling's asserts, not from a run.

Prior writeup `S02_ADVERSARIAL_REVIEW.md` still holds for MF-1 (JSON equality on an empty `error CS` set), MF-2 (`FormatOrDefault` called by the test), SF-1 (Ignore), and SF-2 (short name). This note only adds edits those four do not already spell out. MF-1's `?? 10` is not repeated.

`OmittedHttpArgumentsUseTheSameDefaults` never calls `CaptureGameViewCommand.ExecuteAsync`, `ExecuteFromCli`, or `ExecuteEnveloped`. It never reads `CaptureRequest.Format`. `CliArgsMatchCanonicalDescriptors` never invokes a CLI method body.

## FP-1. `max_logs` omit integer — must-fix

**Edit A** (`GroupCompileErrorsCommand.Execute`, line 111). CONFIRMED.

```csharp
var logs = MCPServer.GetLogs(10, "Error", string.Empty);
```

The parameter stays `int maxLogs = DefaultMaxLogs`. HTTP line 69 stays `maxLogs ?? DefaultMaxLogs`.

Why the test stays green: CONFIRMED. Line 96 locks `Execute`'s `DefaultValue` to the descriptor `50`. Lines 98–106 compare `ExecuteAsync(new JObject())` to `ExecuteEnveloped(50)`. Both call `Execute`, so both call `GetLogs(10)`. `meta.ms` and `data.execution_duration_ms` are zeroed. The `error CS` filter (lines 116–116) never sees two different windows. This stays green on a console full of CS errors. MF-1 needed an empty window; this does not.

**Edit B** (`NexusPipelineCommands.GroupCompileErrors`, line 37). CONFIRMED.

```csharp
return GroupCompileErrorsCommand.ExecuteEnveloped(10, requestId);
```

Signature stays `int maxLogs = GroupCompileErrorsCommand.DefaultMaxLogs` with `[CliArg("max_logs", ...)]`.

Why green: CONFIRMED. `AssertParameters` reads the attribute and `DefaultValue` only. Nothing invokes the method. `PipelineProjection_ReturnsEnvelopeType` invokes `ProjectMap` only (INFERRED from that test's `GetMethod("ProjectMap")`). CLI omit becomes 10 while HTTP omit stays 50.

Catch: plant 60 unique `error CS` lines in the Nexus log queue (not only the Unity console: `SyncWithUnityConsole` copies `count - 50` rows and dedupes message+stack, `MCPServer.Logs.cs` lines 79 and 91). HTTP `{}` must return `data.total_errors == 50`. HTTP `{"max_logs":10}` must return 10. CLI `GroupCompileErrors` invoked with 50 and with 10 must split the same way. Do not compare the omit envelope to `ExecuteEnveloped(50)`.

## FP-2. format omit jpg vs png — must-fix

**Edit** (`CaptureGameViewCommand.ExecuteEnveloped`, line 136). CONFIRMED. Delete the assignment, or replace it with:

```csharp
if (!string.IsNullOrEmpty(call.Format))
    call.Request.Format = ParseFormat(call.Format);
```

Leave `FormatOrDefault` as `format ?? DefaultFormat`.

Why green: CONFIRMED. The drift test stops at `call.Format == null` and `FormatOrDefault(call.Format)` (lines 91–92). `CaptureRequest.Format` initializes to `CaptureFormat.Png` (`CaptureModels.cs` line 58). HTTP omit never assigns it, so the encoder gets PNG. CLI `ExecuteFromCli` writes `FormatOrDefault(format)` into JSON before `TryParse` (line 109), so `call.Format` is `"jpg"` and the `if` keeps JPEG. That is HTTP png / CLI jpg. Deleting the line makes both PNG, and the test still passes. `NexusCommandEnvelopeTests` only checks `FormatOrDefault(null) == "jpg"` and that the file contains the substring `FormatOrDefault(` (lines 495–497). It does not capture. No test reads omit `data.encoding` (search CONFIRMED).

Catch: HTTP `nexus.capture_game_view` with `{}` and a live Game View returns `data.encoding == "jpg"`. CLI `CaptureGameView` invoked with `format: null` (the omitted-argument value, not the C# default `"jpg"`) returns the same. Do not assert `FormatOrDefault` or a helper's `Request.Format`.

## FP-3. width, height, `max_dimension` omit — must-fix

`TryParse` itself is closed. CONFIRMED. `width ?? 0`, `height ?? 0` (`CaptureLookRequest.cs` lines 53–54) and `parsed ?? DefaultMaxLongEdge` (line 80) are what lines 81–84 compare to the literals `0` and `768`. Changing those lines fails the test. This is not a false pass.

**Edit A** (`CaptureGameViewCommand.ExecuteAsync`, after `TryParse` succeeds, before `ExecuteEnveloped`). CONFIRMED the test never enters this method.

```csharp
if (parameters?["width"] == null) call.Request.RequestedWidth = 640;
if (parameters?["height"] == null) call.Request.RequestedHeight = 480;
if (parameters?["max_dimension"] == null) call.Request.MaxLongEdge = 1024;
```

Explicit `0` / `768` still have keys, so only true omit changes. CLI `ExecuteFromCli` always writes the three keys (lines 107–111), so CLI stays 0 / 0 / 768. HTTP omit captures 640×480 capped at 1024.

Why green: CONFIRMED. The drift test calls `TryParse(new JObject())` only. `CaptureLookTests.OmittedMaxEdgeIs768` also calls `TryParse`, not `ExecuteAsync` (INFERRED). `ResolveOutputSize` tests build their own `CaptureRequest`.

**Edit B** (`ExecuteFromCli`, lines 107–111). CONFIRMED no test calls `ExecuteFromCli` (search).

```csharp
["width"] = width > 0 ? width : 640,
["height"] = height > 0 ? height : 480,
["max_dimension"] = maxDimension == 768 ? 1024 : maxDimension,
```

Pipeline passes the C# defaults `0, 0, 768` for an omit, so the CLI omit image changes. The signature defaults stay `0` / `0` / `DefaultMaxLongEdge`, which is all `AssertParameters` checks. HTTP `TryParse` of `{}` is unchanged, so Edit A and Edit B can diverge.

Catch: one HTTP `{}` capture and one CLI invoke with the bound defaults `(width:0, height:0, maxDimension:768)`. `data.width` and `data.height` match, longest edge is 768, and a second HTTP call with `"width":0,"height":0,"max_dimension":768` matches the omit (explicit 0 stays native; explicit 768 stays 768). Do not re-read `TryParse`.

## FP-4. parameter names — should-fix

**Edit** (`CaptureLookRequest.TryParse`). CONFIRMED.

```csharp
parameters?["w"]      // was "width"  (line 34)
parameters?["h"]      // was "height" (line 36)
parameters?["jpeg_quality"] // was "quality" (line 38)
```

Why green: CONFIRMED. An empty object is null for both the old and new keys, so `?? 0` and `?? DefaultJpegQuality` still match lines 81–83. Descriptor names stay `"width"` / `"height"` / `"quality"`, so `DefaultOf` and `[CliArg].Name` stay green. `CaptureLookTests` asserts explicit `max_dimension`, not width. `ClampedQualityAndNegativeWidth` only asserts the call is not `invalid_argument` (INFERRED: a ignored `width: -4` still passes). Legacy `ParseGameViewCaptureRequest` is a different parser.

`max_logs`, `format`, and `max_dimension` key renames are the same drift-test hole but siblings reject them (`max_logs: "abc"`, `format: "bmp"`, `max_dimension: 1024`). Note, not a suite false pass. INFERRED.

Catch: HTTP `{"width":1600,"height":900,"quality":70}` returns a capture whose `data.width` / `data.height` are 1600×900 before the 768 cap (900 long edge stays 900) and whose bytes differ from quality 70 vs 85. The same JSON through CLI must match. A wrong key leaves native size and quality 85.

## FP-5. aliases — should-fix (secondary), note (primary)

**Edit** (`CaptureGameViewCommand`, line 21). CONFIRMED.

```csharp
public const string LookAlias = "nexus_peek";
```

`Aliases` stays `{ Alias, LookAlias, LookDotAlias }`.

Why green: CONFIRMED. Lines 54–55 compare the constant to the CLI name, not to `"nexus_look"`. `CollectionAssert` uses `Alias`, not `LookAlias`. Registry and open-source tests call `Get(LookAlias)` and list the constant (search CONFIRMED: no test file contains the literal `nexus_look`). HTTP `nexus_look` 404s; `nexus_peek` runs. Dropping the name from the array instead of renaming the constant fails `CanonicalIdAndAliasShareTheSameHandlerInstance` (INFERRED).

Primary `Alias` constant is also unpinned in this test (the expected CLI set is the constant). Note only: `NexusCommandEnvelopeTests` uses the literals `nexus_capture_game_view` and `nexus_group_compile_errors` (INFERRED those fail). `LookDotAlias` is the same hole as `LookAlias`.

Catch: HTTP POST `nexus_look` with `{}` returns the capture envelope (`ok` true or a typed capture error), and `nexus_peek` is not registered. CLI help's command name is still `nexus_capture_game_view`.

## FP-6. required flags — should-fix

CLI `[CliArg].Required` cannot flip while `CliArgsMatchCanonicalDescriptors` runs. CONFIRMED. Line 152 requires `false`, and 0.7 discovery uses that flag when the attribute is the real one (`CommandRegistry` as quoted in the prior review). SF-1/SF-2 are the only CLI bypasses; not repeated.

**Edit** (`MCPServerMethods.AddCanonicalCommandTools`, line 203). CONFIRMED.

```csharp
tools.Add(CreateTool(descriptor.Id, descriptor.Description, props, "max_logs"));
```

`CreateTool` writes `inputSchema.required` when the array is non-empty (lines 322–323). `ExecuteAsync` still treats a missing `max_logs` as optional.

Why green: CONFIRMED. `CanonicalSchemasStayFreeOfCliDefaultFields` only forbids a `default` key on each property (lines 132–136). It never reads `required`. The envelope list-tools check only asserts `request_id` is absent from `required` when that array exists (lines 457–459). `max_logs` may be required. Project-map and capture gain a required name they do not have; that is worse, and still unasserted.

Catch: `list_tools` for `nexus.group_compile_errors` has no `inputSchema.required`, or that array does not contain `max_logs`. Then HTTP `{}` still returns `ok: true` (when not compiling) with `data.total_errors` from the 50-line window in FP-1. The schema and the handler must agree.

## Proposed fixes

**`ResolveMaxLogs(int?)` plus `Assert.AreEqual(50, ResolveMaxLogs(null))`.** Does not close FP-1. Recreates the circular test. CONFIRMED shape: the test would call the helper, and Edit A never calls it (`GetLogs(10)` inside `Execute`). Edit B never calls it either. A bug inside the helper (`?? 10`) fails the literal, which is necessary and not sufficient. Comparing `ExecuteAsync(empty)` to `ExecuteEnveloped(ResolveMaxLogs(null))` is still one shared `Execute`.

**`TryPrepare` sets `Request.Format = ParseFormat(FormatOrDefault(call.Format))`, HTTP `ExecuteAsync` calls it, the test asserts `CaptureFormat.Jpeg` by literal.** Closes only the expected-value half of MF-2. The literal stops the test from treating `FormatOrDefault`'s return as the oracle. It does not close the hole. The test still does not enter `ExecuteAsync` / `ExecuteEnveloped`. Those methods can skip `TryPrepare` and leave the field initializer `Png` (FP-2). That is the same call-site circle as `FormatOrDefault`, with a better expected value.

Both close only if the observation is the one in FP-1 and FP-2: two HTTP limits, and `data.encoding` from the real omit call, not a helper return.
