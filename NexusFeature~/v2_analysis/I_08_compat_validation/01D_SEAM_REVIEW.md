# 01D seam review — capture gateway and the 0.8 attribute reference

Fresh read of the seam edit on top of 01C. This review did not edit product code, run Unity, or touch port 7800. `/tmp/nexus-p08-wt` was not modified. The 0.7 counts below are the measurements supplied with the request. The 0.8 scratch log and XML were read, not re-run.

`CaptureGameViewCommand.Capture` is the func `ExecuteAsync(CaptureLookCall)` calls (`CaptureGameViewCommand.cs:193`). HTTP `ExecuteAsync` reaches it through `TryPrepareLook` then `ExecuteEnveloped` (lines 76-79). `ExecuteFromCli` reaches it the same way (lines 111-116). The CLI wrapper only calls `ExecuteFromCli` (`NexusPipelineCommands.cs:58-60`). `ObserveCapture` replaces that func and reads `Format`, `RequestedWidth`, `RequestedHeight`, `JpegQuality`, and `MaxLongEdge` (`NexusCommandCliDriftTests.cs:248-270`). `CaptureRequest.Format` still defaults to PNG (`CaptureModels.cs:58`). Jpeg quality still defaults to 85 and the two sizes still default to 0 (`CaptureModels.cs:61-67`).

## The four open 01C edits

**FP-2 is closed.** 01C's edit replaced `ExecuteAsync`'s `TryPrepareLook` with `CaptureLookRequest.TryParse` and left the PNG initializer in place. The old assert called `TryPrepareLook` itself. That direct call is still at lines 127-138 and still would not see the edit. The seam call does. `ExecuteAsync(new JObject())` expects `CaptureFormat.Jpeg` (lines 205-209). `ExecuteFromCli(0, 0, null, 85, 768, ...)` expects Jpeg (lines 232-237). The reflected CLI omit passes `"jpg"` and expects Jpeg (lines 100-104). All three get PNG if the jpg assignment never runs, so the 01C edit fails. The explicit PNG call (lines 210-221) and the reflected `png` call (lines 105-109) still fail a hardcoded Jpeg. The substring `TryPrepareLook(` and the single `call.Request.Format =` count (lines 156-158) are not what closes this.

**FP-3 is closed.** 01C's HTTP edit, after a successful prepare, was:

```csharp
if (parameters?["width"] == null) call.Request.RequestedWidth = 640;
if (parameters?["height"] == null) call.Request.RequestedHeight = 480;
if (parameters?["max_dimension"] == null) call.Request.MaxLongEdge = 1024;
```

`ExecuteAsync({})` now expects 0, 0, 85, 768 at the seam (lines 205-209). That edit fails. 01C's CLI edit was `parameters["width"] = width > 0 ? width : 640` after `BuildCliArguments`, or `call.Request.RequestedWidth = 640` after `TryPrepareLook`. `ExecuteFromCli` with width 0 (lines 232-237) and the reflected call with width 0 (lines 100-104) both expect 0. The reflected call with 12, 9, 40, 100 expects those values (lines 105-109). `AssertParameters` is still only the signature. The body is what the seam sees. The direct `BuildCliArguments` / `TryPrepareLook` checks at lines 146-154 still do not enter `ExecuteFromCli`; they are not the close.

**FP-4 (height and quality) is closed.** 01C's rename of the parser keys to `"h"` and `"jpeg_quality"` stayed green because those canonical keys were never sent. The HTTP object at lines 212-219 sends `height` 9 and `quality` 40 and expects 9 and 40. `TryParse` still reads `parameters?["height"]` and `parameters?["quality"]` (`CaptureLookRequest.cs:36-38`). A rename yields 0 and 85 and fails. The alias probe sends `w` 640, `h` 33, and `jpeg_quality` 11 and expects 0, 0, 85 (lines 222-231), so accepting those names as a fallback also fails. The reflected call passes height 9 and quality 40 through `BuildCliArguments`, which writes `["height"]` and `["quality"]` (`CaptureGameViewCommand.cs:137-138`). Writing `["h"]` or `["jpeg_quality"]` there fails that same reflected call. `cliOmit.Value<int>("height")` at line 148 is still not a lock: a missing key is 0. The seam's non-zero height and quality are the lock. Width was already closed in 01C and still fails a rename to `"w"`.

**MF-2 is closed.** 01C said the literal `CaptureFormat.Jpeg` only fixed the oracle the test built, and did not make `ExecuteAsync` or `ExecuteFromCli` use the jpg default. Those two methods, and the CLI wrapper, are now the calls under `ObserveCapture`. Skipping `TryPrepareLook` and leaving the PNG initializer fails the omit cases above. The direct `TryPrepareLook(new JObject())` assert is still present and is still not the close.

A rewrite inside the default `Capture` lambda stays green, because `ObserveCapture` replaces the delegate before that body runs. That is the same kind of leftover as 01C's discarded `ReadErrorLogs` list. It is not the four edits above, and it does not keep them open.

## Dual asmdef reference

The reference list is `UnityMCP.Editor`, `Unity.Pipeline`, and `Unity.Pipeline.Attributes` (`UnityMCP.Editor.Pipeline.asmdef:4-7`). That is the smallest 0.7+0.8 compile fix. It does not leave a compile break.

On 0.7.0-exp.1, `CliCommandAttribute` and `CliArgAttribute` are in `Runtime/Common` and compile into `Unity.Pipeline`. `Unity.Pipeline.Attributes` exists in that package and has no `Cli*` type. Referencing only `Unity.Pipeline.Attributes` does not compile 0.7. Referencing it in addition resolves, because the assembly is already there.

On 0.8.0-exp.1, both attribute types moved to `Runtime/Attributes` (`Unity.Pipeline.Attributes`), still in namespace `Unity.Pipeline.Commands`. There is no `TypeForwardedTo`. `Unity.Pipeline.asmdef` references `Unity.Pipeline.Attributes`, and that reference is not transitive. `compile-08.log` is the tree without the Attributes reference: `NexusPipelineCommands.cs` fails with CS0246 on `CliCommand` and `CliArg`. `compile-08-ref.log` exits batchmode successfully. An asmdef cannot version-condition a reference, so one file that compiles on both versions has to name both assemblies. The type lives in exactly one of them on each version, so the pair is not ambiguous.

`drift-seam-on-08.xml` is 3 passed, 0 skipped, including `CliArgsMatchCanonicalDescriptors`. The supplied 0.7 run was drift 3, envelope 29, registry 9, capture look 10, skipped 0. This review does not approve a commit.
