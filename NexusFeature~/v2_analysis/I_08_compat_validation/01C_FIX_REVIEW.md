# 01C fix review — uncommitted S-02 false-pass patch

Fresh read. Parent `bfdd0f7` on `rework2/S-02`. This review did not edit product code, run Unity, or touch port 7800. `/tmp/nexus-p08-wt` was not opened. `Editor/Pipeline/UnityMCP.Editor.Pipeline.asmdef` still references only `Unity.Pipeline`.

The reported 0.7 pass (drift 3, envelope 29, registry 9, capture look 10, skipped 0) is not evidence. A green run does not close a hole the assertion does not observe.

## The eight checks

1. **No for a replacement. Yes if the hook return is thrown away.** `Execute` is `ReadErrorLogs(maxLogs)` (`GroupCompileErrorsCommand.cs:144`). The spy in `AssertHttpCompileUsesTheResolvedWindow` fails both hunt substitutions: `GetLogs(10, …)` never assigns `seen` (stays `-1`); `ReadErrorLogs(10)` sets `seen` to 10. Both miss `Assert.AreEqual(50, seen)` after `Execute()` (`NexusCommandCliDriftTests.cs:176`) and the HTTP omit check at line 172. What stays green is a second statement: call `ReadErrorLogs(maxLogs)`, ignore that list, and group `MCPServer.GetLogs(10, …)`. The assert reads `seen`, not the list `Execute` walks.

2. **No, once the CLI test actually runs.** `GroupCompileErrors` is `(int maxLogs, string requestId)` and returns `ExecuteEnveloped(maxLogs, requestId)` (`NexusPipelineCommands.cs:33-37`). `Invoke(null, new object[] { 7, null })` then `Assert.AreEqual(7, seen)` (`NexusCommandCliDriftTests.cs:86`) goes red if the body hardcodes `ExecuteEnveloped(10, requestId)`. The same invoke with `50` covers the other literal. This invoke sits under the Pipeline probe, so it still does not run when that probe Ignores.

3. **Yes.** `ExecuteEnveloped` already has no jpg assignment (`CaptureGameViewCommand.cs:164-169`). The only `call.Request.Format =` is inside `TryPrepareLook` (line 153). The test calls `TryPrepareLook` itself and expects `CaptureFormat.Jpeg` (line 121). `CaptureRequest.Format` still initializes to `Png` (`CaptureModels.cs:58`). Replacing `ExecuteAsync`'s `TryPrepareLook` call (line 70) with `CaptureLookRequest.TryParse` leaves that initializer in place. The file still contains one spaced assignment and the substring `TryPrepareLook(`, so lines 140-141 stay green. HTTP omit is PNG. CLI stays JPEG if `ExecuteFromCli` still calls `TryPrepareLook`.

4. **Yes.** Nothing in the new test calls `CaptureGameViewCommand.ExecuteAsync`. After a successful `TryPrepareLook`, this stays green and changes only a true omit:

```csharp
if (parameters?["width"] == null) call.Request.RequestedWidth = 640;
if (parameters?["height"] == null) call.Request.RequestedHeight = 480;
if (parameters?["max_dimension"] == null) call.Request.MaxLongEdge = 1024;
```

Explicit keys still parse as 12 / 100 (lines 125-126). `TryPrepareLook(new JObject())` still returns 0 / 0 / 768 (lines 113-116).

5. **Yes.** `BuildCliArguments` is what the test calls (`NexusCommandCliDriftTests.cs:129-133`). `ExecuteFromCli` is not. This stays green while the builder stays honest:

```csharp
JObject parameters = BuildCliArguments(...);
parameters["width"] = width > 0 ? width : 640;
```

The same is true of `call.Request.RequestedWidth = 640` after `TryPrepareLook`. Pipeline's omit still passes C# defaults `0, 0, 768` (`NexusPipelineCommands.cs:47-51`). Those signature defaults are locked only by `AssertParameters`, which does not enter the method body.

6. **Yes. It fails. It does not Ignore.** `AssemblyLoaded("Unity.Pipeline")` is true, both `FindType` calls miss `Unity.Pipeline.Commands.CliCommandAttribute`, and `Assert.IsNotNull` at line 36 fails. The CLI body below that line does not run, which is a Fail, not a skip.

7. **The rest of the test runs only when that assembly is already loaded and the full name is unchanged.** `FindType("Unity.Pipeline.Attributes", attributeName)` is the second probe (line 29). A hit does not Ignore. This package does not load that assembly: the Pipeline asmdef reference list is still `UnityMCP.Editor` and `Unity.Pipeline`. On a 0.8 editor where the attribute exists only in `Unity.Pipeline.Attributes` and nothing has referenced it, `Unity.Pipeline` is loaded, the type is missing, and line 36 Fails. That is the right Fail. It is not a run of the CLI asserts, and it is not the old Ignore.

8. **None of the five named defects is present in this diff.**
   - Hook: both spies restore `ReadErrorLogs` in `finally` (lines 90 and 180). An assertion failure still restores it.
   - Invoke: `{ 50, null }` matches `GroupCompileErrors(int, string)`. The method returns `NexusCommandEnvelope`, not a `Task`, so the hook runs before `Invoke` returns.
   - Source count: the command file contains `call.Request.Format =` once (line 153). That is not a false fail on this tree. It false-fails if that assignment is wrapped so the spaced needle splits. It does not see call sites (check 3).
   - Request id: no product regression. `ExecuteAsync` passes the parsed id into `ExecuteEnveloped` (`GroupCompileErrorsCommand.cs:63`). The test only asserts the helper's omit id is null (line 151). It never reads `meta.request_id`. `ExecuteEnveloped(maxLogs, null)` while the JSON carried an id stays green. The deleted envelope compare was the only previous look at that field, and only for an omit.
   - Quality gate: `CaptureGameViewCommand.cs` is 266 lines, `GroupCompileErrorsCommand.cs` is 180. Both are under the 300-line warning. New methods are internal and short. The gate ignores `Tests/`, so the 303-line test file does not warn.

## Verdicts

| Id | Status | Closing line, or the edit that stays green |
| --- | --- | --- |
| FP-1 | CLOSED | Hunt Edit A fails at `NexusCommandCliDriftTests.cs:172` and `:176`. Hunt Edit B fails at `:86`. Discarding the hook list is a narrower leftover, not those edits. |
| FP-2 | STILL OPEN | Check 3. Jpeg is asserted on the object `TryPrepareLook` returned to the test (line 121), not on `ExecuteEnveloped`. |
| FP-3 | STILL OPEN | Checks 4 and 5. `TryParse` / `TryPrepareLook` of an object the test built is unchanged and still strict for that object. |
| FP-4 | STILL OPEN | `["width"] = 12` fails a rename to `"w"` (line 125). `["height"]` and `["quality"]` are never sent, so `"h"` and `"jpeg_quality"` still omit to 0 and 85 (lines 114-115). |
| FP-5 | CLOSED | `Assert.AreEqual("nexus_look", LookAlias)` and the `nexus.look` pin (lines 66-67). Dropping the name from `Aliases` still belongs to `CanonicalIdAndAliasShareTheSameHandlerInstance`, which calls `Get(LookAlias)`. |
| FP-6 | CLOSED | `required == null \|\| required.Count == 0` for the three canonical tools (lines 208-209). `CreateTool(..., "max_logs")` writes `inputSchema.required` and fails. |
| MF-1 | CLOSED | The empty `error CS` JSON compare is gone. Omit, explicit 7, `Execute()`, and the CLI wrapper are the same spy as FP-1. `max_logs: 0` is still unasserted: treating 0 as omit stays green. |
| MF-2 | STILL OPEN | Same hole as FP-2. The literal `CaptureFormat.Jpeg` fixes the oracle half that used to call `FormatOrDefault`. It does not make `ExecuteAsync` or `ExecuteFromCli` use `TryPrepareLook`. |
| SF-1 | CLOSED | Loaded assembly plus a missing attribute Fails at line 36 instead of `Assert.Ignore`. Neither `Unity.Pipeline` nor `Unity.Pipeline.Attributes` loaded still Ignores (lines 30-33). Package CI that installs neither assembly still never runs the CLI lock. That skip is the 01B policy, not the moved-attribute false pass. |

## Left open on purpose

- `cliCommand` is still unused after the null check. `AttributeNamed` still matches the short name `CliCommandAttribute`. SF-2 is untouched.
- `ExecuteFromCli`'s C# default for format is `DefaultFormat` (`"jpg"`), not null (`NexusPipelineCommands.cs:49`). The new null argument is applied only by the direct `BuildCliArguments` call.
- Current product code does call `TryPrepareLook` from both `ExecuteAsync` (line 70) and `ExecuteFromCli` (line 107), and `ReadErrorLogs` is the only log read in `Execute`. Those are true of this diff. The new test does not keep the capture call sites true.
