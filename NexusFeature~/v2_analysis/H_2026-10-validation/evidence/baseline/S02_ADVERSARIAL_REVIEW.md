# S-02 adversarial review — CLI drift lock

Reviewed commit: `bfdd0f7e71c851fb4615956f3c420940a9623220` ("feat: lock canonical CLI parameters to the descriptor"), parent `23005b468236984e459a6aaf148e7e05b9240436`, branch `rework2/S-02`. This is the committed tip, including `FormatOrDefault` and `OmittedHttpArgumentsUseTheSameDefaults`. No product code was changed. Unity was not run. Port 7800 was not contacted.

Pipeline cache checked read-only: `Library/PackageCache/com.unity.pipeline@ab05e9cd7f74` (`0.7.0-exp.1`).

Harness evidence already on disk (`NexusCommandCliDriftTests.json`, 2026-10-04T21:47:21Z) shows the three tests **Passed**, **Skipped 0**. That Editor has `com.unity.pipeline` `0.7.0-exp.1` in the parent `Packages/manifest.json`, so `CliArgsMatchCanonicalDescriptors` actually ran there. That pass does not make the assertions strict.

## Verdict

The committed test still has an HTTP-omit hole. It is narrower than a draft that never called `TryParse` or `FormatOrDefault`.

Closed, and strict, against the descriptor (and against the literals `0`, `85`, `768`, `"jpg"`, `"game"`, `"base64"`):

- `width`, `height`, `instance_id` (`?? 0`)
- `quality` (`?? DefaultJpegQuality`)
- `max_dimension` (`?? DefaultMaxLongEdge`)
- `target` (descriptor null and `TargetPath` null are each asserted)
- observable `view` (`CaptureView.Game`) and observable `delivery` (`WriteFile == false`), because the descriptor is also pinned to `"game"` / `"base64"` and those constants must equal the descriptor

Not strict:

- **format.** `call.Format` is asserted null. The descriptor is compared to `FormatOrDefault(null)`, which the test calls itself. `Request.Format` is never set. `ExecuteEnveloped` / `ExecuteAsync` are never called. HTTP can apply a different format than the helper and this test stays green. CLI `ExecuteFromCli` pre-applies `FormatOrDefault` before `TryParse`, so the two transports can diverge.
- **max_logs.** `Execute()`'s C# default is locked to the descriptor (`50`). `ExecuteAsync(new JObject())` is compared to `ExecuteEnveloped(50)` only as JSON after zeroing `meta.ms` and `data.execution_duration_ms`. That stays green whenever both limits see the same `error CS` set, including an empty console. The integer `ExecuteAsync` passes is not read.
- **view / delivery strings** are not read back. A third token that still maps to `CaptureView.Game` and `WriteFile == false` would pass. The two real omit outcomes (game vs scene, base64 vs file) do not.
- **look `request_id`** is not read off `CaptureLookCall`. Project-map HTTP omit is not compared at all (its only parameter is `request_id`).

## Counts

| Class | Count |
| --- | --- |
| Must-fix | 2 |
| Should-fix | 2 |
| Note | 6 |

Must-fix means the committed test is green while the contract it claims to lock can be broken.

## Must-fix

### MF-1. `max_logs` JSON equality does not lock the omit integer

`GroupCompileErrorsCommand.ExecuteAsync` omits with `maxLogs ?? DefaultMaxLogs` (`Editor/Commands/GroupCompileErrorsCommand.cs` line 69). The test does this (`Tests/Editor/NexusCommandCliDriftTests.cs`):

```csharp
Assert.AreEqual(50, DefaultOf(compile, "max_logs"));
ParameterInfo executeMaxLogs = typeof(GroupCompileErrorsCommand).GetMethod("Execute").GetParameters()[0];
Assert.AreEqual(DefaultOf(compile, "max_logs"), executeMaxLogs.DefaultValue);
Task<JToken> omitted = new GroupCompileErrorsCommand().ExecuteAsync(new JObject(), CancellationToken.None);
// ...
JObject fromDefault = GroupCompileErrorsCommand.ExecuteEnveloped((int)DefaultOf(compile, "max_logs"), null).ToJson();
fromHttp["meta"]["ms"] = 0;
fromDefault["meta"]["ms"] = 0;
fromHttp["data"]["execution_duration_ms"] = 0;
fromDefault["data"]["execution_duration_ms"] = 0;
Assert.AreEqual(fromDefault.ToString(Formatting.None), fromHttp.ToString(Formatting.None));
```

`Execute(int maxLogs = DefaultMaxLogs)` is locked. The HTTP fallback is not. `Execute` only keeps lines containing `"error CS"` (`GroupCompileErrorsCommand.cs` lines 111–116). If `ExecuteAsync` used `?? 10` or `?? 0` while the descriptor and `Execute()` stayed at 50, both envelopes are identical whenever the console has no CS errors, or the same CS errors, inside the smaller window. Package CI's temporary project is that empty case. The harness run of this test (3 ms) does not show it pinned the integer.

CLI still calls `ExecuteEnveloped(maxLogs, requestId)` with the C# parameter default. HTTP and CLI can use different limits while `OmittedHttpArgumentsUseTheSameDefaults` stays green. `CliArgsMatchCanonicalDescriptors` would still catch a CLI-side default change, but only when it does not Ignore (see SF-1).

### MF-2. Format omit is locked on the helper, not on the HTTP path

`TryParse` stores the raw string and does not apply jpg (`CaptureLookRequest.cs` lines 48–52). The default is applied later:

```csharp
call.Request.Format = ParseFormat(FormatOrDefault(call.Format));
```

(`CaptureGameViewCommand.cs` line 136). `FormatOrDefault` is `format ?? DefaultFormat` (lines 189–192). The test never enters `ExecuteEnveloped`:

```csharp
Assert.IsNull(call.Format);
Assert.AreEqual(DefaultOf(look, "format"), CaptureGameViewCommand.FormatOrDefault(call.Format));
```

(`NexusCommandCliDriftTests.cs` lines 91–92).

Replace line 136 with `ParseFormat(call.Format ?? "png")` and leave `FormatOrDefault` as jpg. This test still passes. HTTP omit becomes png. CLI `ExecuteFromCli` writes `FormatOrDefault(format)` into the JSON **before** `TryParse` (line 109), so a C# default of `"jpg"` still captures jpeg. That is an HTTP/CLI drift with a green test.

`??` also does not treat `""` as omitted. `ParseFormat` treats empty as jpeg (lines 197–200). The omit path is null, and the test does assert null, so this is not a second hole by itself.

Fields the empty-HTTP compare does **not** compare:

| Field | What the test actually checks |
| --- | --- |
| width, height, instance_id, quality, max_dimension | `TryParse(empty)` value equals descriptor default. Strict. |
| target | Descriptor default is null, and `TargetPath` is null. Strict for today's null contract. |
| format | Helper only. `Request.Format` not compared. |
| view | Hardcoded `CaptureView.Game`, plus descriptor equals `DefaultView`. Applied string not read. |
| delivery | Hardcoded `WriteFile == false`, plus descriptor equals `DefaultDelivery`. Applied string not read. |
| look request_id | Descriptor null only. `call.RequestId` not read. |
| max_logs | `Execute()` default and payload JSON, not the integer `ExecuteAsync` used. |
| project map | No HTTP omit compare. |

## Should-fix

### SF-1. Missing or moved `CliCommandAttribute` Ignores. Package CI never loads Pipeline, so the CLI lock does not run there

```csharp
Type cliCommand = FindType("Unity.Pipeline", "Unity.Pipeline.Commands.CliCommandAttribute");
if (cliCommand == null)
{
    Assert.Ignore("com.unity.pipeline is not loaded. CLI drift is checked when Pipeline is installed.");
    return;
}
```

(`NexusCommandCliDriftTests.cs` lines 27–31). `FindType` (`lines 201–206`) returns null when assembly `Unity.Pipeline` is not loaded, or when that assembly has no type `Unity.Pipeline.Commands.CliCommandAttribute`. The test then skips. It does not fail.

In this 0.7.0-exp.1 cache the attribute is still where the probe looks:

- File: `Runtime/Common/CliCommandAttribute.cs`
- Namespace: `Unity.Pipeline.Commands`
- Assembly: `Unity.Pipeline` (`Runtime/Unity.Pipeline.asmdef`, `"name": "Unity.Pipeline"`)

`Unity.Pipeline.Attributes` already exists (`Runtime/Attributes/Unity.Pipeline.Attributes.asmdef`, root namespace `Unity.Pipeline.CodeReload`). It contains `CodeReloadAttribute` and `OnCodeReloadAttribute` only. The 0.7 changelog moved code-reload attributes there, not `[CliCommand]`. If `[CliCommand]` moves to assembly `Unity.Pipeline.Attributes` (the 0.8 claim) or changes namespace, `GetType` returns null, the test Ignores, and every CLI assertion below the probe is skipped.

`package.json` does not depend on `com.unity.pipeline`. `.github/workflows/validate.yml` job `unity-package-smoke` installs only `com.forkhorizon.nexus.unity` (`file:` the workspace) and `com.unity.test-framework` `1.5.1` (lines 246–252), then runs `UnityMCP.Editor.Tests` (lines 366–375). The result check uses `total` and `failed` only (lines 398–405). It does not fail on `skipped`. `CliArgsMatchCanonicalDescriptors` is Ignored in that job. A broken `[CliArg]` list stays green in GitHub CI.

Ignore is the right result for a Legacy-only Editor: failing there would punish a supported configuration (`UnityMCP.Editor.Pipeline.asmdef` is itself behind `NEXUS_HAS_PIPELINE`). The hole is that no CI job installs Pipeline, and a skip is treated as success. The other two tests do run without Pipeline. They do not lock `[CliArg]` names, order, descriptions, CLR types, or C# defaults.

On a machine that has Pipeline, the probe currently hits. The recorded harness run passed this test; it did not skip.

### SF-2. `FindType`'s `Type` is only a null check. Attributes match on short name

After the Ignore, `cliCommand` is never used. The scan is:

```csharp
MethodInfo[] methods = commands.Assembly.GetTypes()
    .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
    .Where(method => AttributeNamed(method, "CliCommandAttribute") != null)
```

(`NexusCommandCliDriftTests.cs` lines 35–37).

```csharp
return provider.GetCustomAttributes(false).FirstOrDefault(attribute => attribute.GetType().Name == typeName);
```

(lines 176–178). That is short-name equality (`Name == "CliCommandAttribute"`), not `cliCommand.IsInstanceOfType`, not `FullName`, and not a suffix search. `CliArgAttribute` is never resolved with `FindType` at all. Pipeline discovery uses the real types (`CommandRegistry.GetCustomAttribute<CliCommandAttribute>()` / `CliArgAttribute`).

False pass this allows: assembly `Unity.Pipeline` is loaded and still contains `Unity.Pipeline.Commands.CliCommandAttribute` (probe passes), while the method or parameter carries a different class that is also named `CliCommandAttribute` or `CliArgAttribute` and exposes `Name`, `Description`, and `Required`. The test reads the decoy. Pipeline ignores it and falls back to the C# parameter name (`maxLogs` instead of `max_logs`). `FirstOrDefault` also makes the winner depend on attribute order if both are present.

A missing `[CliArg]` with no decoy does **not** pass. `AssertParameters` requires the attribute (line 149). C# name similarity is not consulted. That part is not a false pass.

## Notes

### N-1. Primary alias is `Aliases[0]` only. A look alias as the CLI name fails. A second alias can drift

```csharp
INexusCommand canonical = NexusCommandRegistry.All.SingleOrDefault(
    command => command.Descriptor.Aliases != null && command.Descriptor.Aliases.Length > 0 &&
               command.Descriptor.Aliases[0] == cliName);
Assert.IsNotNull(canonical, "CLI command " + cliName + " is not the primary alias of a canonical command.");
Assert.AreNotEqual(CaptureGameViewCommand.LookAlias, cliName);
Assert.AreNotEqual(CaptureGameViewCommand.LookDotAlias, cliName);
```

(lines 49–55). Capture's alias list is `{ Alias, LookAlias, LookDotAlias }` (`CaptureGameViewCommand.cs` line 213). Registering the CLI name as `nexus_look` or `nexus.look` fails both the primary-alias lookup and `AreNotEqual`.

Nothing asserts `Aliases[1]` or `Aliases[2]`. Changing `LookAlias` from `nexus_look` to another string, while the CLI method stays on `Aliases[0]`, does not fail this test. `CollectionAssert.AreEquivalent` only checks that the CLI names found are the three primary `Alias` constants (lines 59–61).

### N-2. Not comparing `CliArg.DefaultValue` is not a 0.7 false pass

`CommandRegistry.DiscoverParameters` (`Runtime/Common/CommandRegistry.cs` lines 388–396):

```csharp
var argAttr = param.GetCustomAttribute<CliArgAttribute>();
var name = argAttr?.Name ?? param.Name;
var description = argAttr?.Description ?? $"Parameter: {param.Name}";
var required = argAttr?.Required ?? !param.HasDefaultValue;
var defaultValue = param.HasDefaultValue ? param.DefaultValue : argAttr?.DefaultValue;
yield return new CommandParameterInfo(name, description, required, param.ParameterType, defaultValue);
```

When `HasDefaultValue` is true, the C# default wins and `CliArg.DefaultValue` is ignored. The test requires `parameter.HasDefaultValue` (line 155), so the attribute default is dead on every parameter this test accepts.

Help and bind use that discovered value, not the attribute:

- Catalog: `BasePipelineServer.BuildCommandEntry` sets `defaultValue = p.DefaultValue` (around line 1054).
- JSON schema: `JsonSchemaGenerator.GenerateParameterSchema` writes `schema["default"]` from `parameter.DefaultValue` when it is non-null (lines 77–82). A null string default is omitted from the schema key and is still bound as null.
- Exec: `BasePipelineServer` uses `paramInfo.DefaultValue` when non-null, otherwise the CLR zero/null (lines 2293–2307). Boxed `0` is used. It is not replaced by `Activator.CreateInstance`.

A divergent `CliArg.DefaultValue` does not change what 0.7 prints or invokes. It would start to matter only if a later Pipeline stopped preferring the C# default. Removing the C# default while keeping an attribute default fails this test (`HasDefaultValue`).

### N-3. Exact CLR type has no int/long or float/double hole on the signature

```csharp
if (jsonType == "integer") return typeof(int);
if (jsonType == "string") return typeof(string);
if (jsonType == "boolean") return typeof(bool);
if (jsonType == "number") return typeof(float);
Assert.Fail("Unknown canonical JSON type " + jsonType);
```

(lines 166–173). A `long` or `double` parameter fails `Assert.AreEqual` on `ParameterType`. Unknown JSON types fail closed. No current canonical parameter uses `number` or `boolean`; those branches are unexecuted, not loose.

The boxed **descriptor** default's runtime type is not checked. NUnit's numeric comparer can treat `(long)50` and `(int)50` as equal. The CLI parameter would still be `int`, so the invoked value does not change. Not a user-visible hole.

### N-4. `list_tools` really does prove the three canonical schemas have no `default` key

`CanonicalSchemasStayFreeOfCliDefaultFields` refuses a hard reference on `Unity.Pipeline` or `UnityMCP.Editor.Pipeline` in the test asmdef, calls `MCPServerMethods.Init` and `ProcessJsonRpc` for `list_tools`, requires 120 tools, and for `ProjectMapCommand.Id`, `GroupCompileErrorsCommand.Id`, and `CaptureGameViewCommand.Id` asserts every `inputSchema.properties` object lacks the key `"default"` (lines 110–137).

That is the live builder. `AddCanonicalCommandTools` writes only `type` and `description` (`Editor/MCPServerMethods.Tools.cs` lines 193–199). The test would fail if that object gained a `default` key. It does not scan the other tools, nested schema objects, or the word "default" inside description strings (those strings still say "default 85" and similar). The commit claim that is actually locked is the three canonical property bags.

### N-5. Scan is the Pipeline assembly, including non-public static methods, not other assemblies

`commands.Assembly.GetTypes()` plus `Public | NonPublic | Static | DeclaredOnly` covers every static method declared in `UnityMCP.Editor.Pipeline`, private and internal included. It is not limited to `NexusPipelineCommands`. Instance methods are excluded. Pipeline's `CreateCommandInfo` also drops non-static methods, so that gap matches 0.7.

A `[CliCommand]` in another assembly is invisible. The count check is `NexusCommandRegistry.All.Count` against methods in this one assembly, so an extra wrapper elsewhere does not fail the count. Today the only asmdef that references `Unity.Pipeline` is `Editor/Pipeline/UnityMCP.Editor.Pipeline.asmdef`. `GetCustomAttributes(false)` also skips inherited attributes; Pipeline's `GetCustomAttribute` includes them. Current methods declare the attribute directly, so this is a future false-fail, not a false pass.

### N-6. Descriptions, order, required, and C# defaults are real comparisons when the CLI test runs

`AssertParameters` (lines 140–158) checks, by index: count, `[CliArg]` present, `CliArg.Name`, `CliArg.Description`, `CliArg.Required == false`, `ParameterType` equals `ExactClrType`, descriptor `HasDefault`, `HasDefaultValue`, and `DefaultValue` (mapping `DBNull` to null). Command help is `CliCommand.Description` against `Descriptor.Description` (line 53).

Those cannot diverge while `CliArgsMatchCanonicalDescriptors` runs. They can all diverge in CI because of SF-1. Not compared even when it runs: `CliArg.DefaultValue` (N-2), `Tags`, `MainThreadRequired`, `RuntimeOnly`. There is no descriptor required-flag; every parameter is forced optional via `NexusCommandParameter.Optional`, which always sets `HasDefault = true` (`NexusCommandDescriptor.cs` lines 61–70).

`view` and `delivery` are not string-compared (see the table under MF-2). Flipping omit from game to scene, or from base64 to file, fails the hardcoded `CaptureView.Game` / `IsFalse(WriteFile)` asserts, and the absolute `"game"` / `"base64"` asserts stop the constants from moving together. That is enough for the two legal values. It is not a general string compare.

## Strengths

- The test assembly does not reference Pipeline. Legacy-only compiles still build. The recorded harness run shows the reflection probe succeeds when 0.7 is installed: `CliArgsMatchCanonicalDescriptors` Passed, not Skipped.
- When the probe hits, the lock is positional and exact: wire name, help, optional, CLR type, and C# default, including a null string default. `DBNull` is normalized so a null optional does not false-fail.
- Absolute literals (`0`, `85`, `768`, `"jpg"`, `"game"`, `"base64"`, `50`) stop a constant from drifting in the descriptor, the CLI default, and the HTTP fallback at the same time.
- `TryParse(new JObject())` really compares width, height, quality, max edge, and instance id to those descriptor defaults. The `?? 0` path cannot move without failing. That part of the old omit hole is closed on this tip.
- A look alias registered as the CLI name fails. The set of CLI names must be the three primary aliases.
- A missing `[CliArg]` fails. CamelCase C# names are not accepted as wire names.
- `integer` must be `int` and `number` must be `float`. `long` and `double` fail.
- The `list_tools` check is a real RPC against the three canonical ids, not a search of the builder source.
- Leaving `CliArg.DefaultValue` unread matches what 0.7 actually binds and prints.

## Question index

1. **Attribute moves out of `Unity.Pipeline`.** Yes. Ignore, not Fail. Quote under SF-1. In this cache it still lives in assembly `Unity.Pipeline`, namespace `Unity.Pipeline.Commands`. `Unity.Pipeline.Attributes` does not contain it.
2. **`FindType` does not filter attributes.** Null check only. `AttributeNamed` matches `GetType().Name`. False pass: a same-named decoy while the real type still exists in `Unity.Pipeline`. SF-2.
3. **HTTP omit.** Width, height, instance id, quality, and max edge are strict. Format and max_logs are not. View and delivery observables are locked; their strings are not read. The committed test still has the HTTP-omit hole (MF-1, MF-2).
4. **`list_tools` defaults.** Yes for the three canonical property objects. Not for the rest of the catalog. N-4.
5. **Scan scope.** Whole `UnityMCP.Editor.Pipeline` assembly, including non-public static methods. Not instance methods. Not another assembly. N-5.
6. **Aliases.** Look alias as the CLI name fails. A second alias string can change without failing. N-1.
7. **CLR type.** No hole for `int` vs `long` or `float` vs `double` on the parameter. Boxed descriptor default type is unchecked and not user-visible. N-3.
8. **`CliArg` required.** Yes. Missing attribute fails. C# name similarity does not pass. A decoy of the same short name can. SF-2.
9. **`CliArg.DefaultValue`.** Not a false pass on 0.7. HasDefaultValue selects the C# default for discovery, help, schema, and exec. N-2.
10. **Ignore when Pipeline is absent.** Acceptable for a local Legacy-only Editor. Not acceptable as the only CI signal: `validate.yml` never installs Pipeline and does not fail on skipped tests, so the CLI lock never runs in CI. SF-1.
11. **Assertions that pass on real divergence.** While the CLI test runs: descriptions, order, `Required == false`, and C# defaults do not. While it is Ignored: all of them do. Also green if they diverge: `CliArg.DefaultValue` (ignored by 0.7), tags, main-thread and runtime-only flags, the `max_logs` integer behind identical JSON, and the format applied inside `ExecuteEnveloped`.
