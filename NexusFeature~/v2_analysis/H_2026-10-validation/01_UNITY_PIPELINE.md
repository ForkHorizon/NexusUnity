# Unity Pipeline: installed 0.7.0-exp.1 vs published latest (2026-10-04)

Forensic comparison of the harness-installed `com.unity.pipeline` against the newest package the Unity registry served on 2026-10-04. The 0.8 tarball was downloaded to `/tmp` and inspected. It was not added to `Packages/manifest.json`, not copied into the project, and not loaded by the Editor. The Editor on port 7800 was not contacted.

Labels: **CONFIRMED** = this pass read the file or tarball. **DOCUMENTED** = official text, not re-executed. **MEASURED** = a command or HTTP call this pass ran. **INFERRED** = follows from those, not separately executed. **UNKNOWN** = not established.

## Critical question

**Did `CliCommandAttribute` and `CliArgAttribute` move assemblies or namespaces after 0.7?**

**Assembly: yes, in 0.8.0-exp.1. Namespace: no.**

| | Installed 0.7.0-exp.1 | Published 0.8.0-exp.1 |
| --- | --- | --- |
| Assembly | `Unity.Pipeline` | `Unity.Pipeline.Attributes` |
| Source path | `Runtime/Common/CliCommandAttribute.cs`, `Runtime/Common/CliArgAttribute.cs` | `Runtime/Attributes/CliCommandAttribute.cs`, `Runtime/Attributes/CliArgAttribute.cs` |
| Namespace | `Unity.Pipeline.Commands` | `Unity.Pipeline.Commands` (unchanged) |
| Type forwarder back to `Unity.Pipeline` | n/a (types live there) | none. Full-tarball scan of `*.cs` for `TypeForwardedTo` / `TypeForwardedFrom` was empty |

**CONFIRMED** both sides. The 0.8 `Unity.Pipeline.asmdef` still references `Unity.Pipeline.Attributes`, but that reference is not transitive for a custom asmdef, and there is no `[assembly: TypeForwardedTo(...)]` in `Runtime/AssemblyInfo.cs`.

The active plan's P0-06 / F14 claim is **true, not an assumption**: 0.8 exists, and `[CliCommand]` / `[CliArg]` moved into the assembly that 0.7 already used for code-reload attributes only. What the plan does not say, and what the S-02 test depends on, is that the **namespace did not change**.

`Tests/Editor/NexusCommandCliDriftTests.cs` looks up the attribute with:

```csharp
FindType("Unity.Pipeline", "Unity.Pipeline.Commands.CliCommandAttribute")
```

`FindType` matches `Assembly.GetName().Name`, then `Assembly.GetType(fullName)`. On the installed 0.7 package that lookup matches. On 0.8 the full name is still `Unity.Pipeline.Commands.CliCommandAttribute`, but the assembly name is `Unity.Pipeline.Attributes`, so `FindType` returns null and the test hits:

```csharp
Assert.Ignore("com.unity.pipeline is not loaded. CLI drift is checked when Pipeline is installed.");
```

That Ignore sits **before** `Assert.IsNotNull(commands, "Pipeline is loaded but the Nexus CLI projection did not compile.")`. A 0.8 compile break of `UnityMCP.Editor.Pipeline` and a silent skip of the drift lock are the same branch.

**Is that Ignore risk real today?** Against the **installed** package, no. The harness manifest pins `com.unity.pipeline` to `0.7.0-exp.1`, the cache folder is `com.unity.pipeline@ab05e9cd7f74`, and that prefix is the registry shasum `ab05e9cd7f74cc7e8d9dee579ca5a122192e9335`. The type is in assembly `Unity.Pipeline`. The test was not executed this pass (Editor was not touched), so "the running domain currently contains that assembly" is **INFERRED** from the asmdef constraint `UNITY_EDITOR || DEVELOPMENT_BUILD || ENABLE_RUNTIME_PIPELINE` plus a loaded Editor package, not **MEASURED** by NUnit.

Against the **published latest**, the Ignore is real: upgrading the manifest to `0.8.0-exp.1` without changing the assembly string makes `CliArgsMatchCanonicalDescriptors` skip, including the assertion that the Nexus projection compiled. Parameter checks use the short name `CliCommandAttribute` / `CliArgAttribute`, so those would still work if the assembly string were updated. `Name`, `Description`, and `Required` are unchanged (**CONFIRMED** file diff; only the `RuntimeOnly` doc comment and a `see` vs `c` cref on `CliArg` differ).

## Sources

| Source | What it established |
| --- | --- |
| Registry `GET https://packages.unity.com/com.unity.pipeline` | **MEASURED** HTTP 200. Eight versions. `dist-tags.latest` = `0.8.0-exp.1`. No `0.8.0-exp.2`, `0.8.1`, or `0.9`. |
| Tarball `https://download.packages.unity.com/com.unity.pipeline/-/com.unity.pipeline-0.8.0-exp.1.tgz` | **MEASURED** download, 4.5 MB. shasum `62c08c808737255821f3449466689a527a2eea50`. Registry `time` `2026-09-25T18:42:14.022Z`. |
| Installed cache `/Users/daliys/Daliys/UnityProjects/UnityTestForNexus/Library/PackageCache/com.unity.pipeline@ab05e9cd7f74` | **CONFIRMED** `package.json` version `0.7.0-exp.1`, `unity: 6000.0`, `_fingerprint` equals the 0.7 registry shasum. Registry `time` `2026-09-11T19:04:01.417Z`. |
| Harness manifest `/Users/daliys/Daliys/UnityProjects/UnityTestForNexus/Packages/manifest.json` | **CONFIRMED** `"com.unity.pipeline": "0.7.0-exp.1"`. |
| Docs changelog https://docs.unity3d.com/Packages/com.unity.pipeline@0.8/changelog/CHANGELOG.html | **DOCUMENTED**. Header date `[0.8.0-exp.1] - 2026-09-22`. `[0.7.0-exp.1] - 2026-09-10`. Page title is 0.8.0-exp.1, which matches the registry latest. |
| Announcement https://discussions.unity.com/t/unity-pipeline-package-0-8-0-exp-1-is-available-now/1737832 | **DOCUMENTED** 2026-09-28. Same breaking list as the changelog, plus the URP `CS0246` thread. |
| CLI beta.12 https://discussions.unity.com/t/unity-cli-1-0-0-beta-12-is-rolling-out/1738085 | **DOCUMENTED** 2026-10-01. Tag index requires Pipeline 0.8; older packages still return the full command list. |
| GitHub `https://github.com/Unity-Technologies/com.unity.pipeline` | **MEASURED** 404. The issue tracker is linked from the announcement; the repo is not publicly readable. `package.json` `repository.url` is `https://github.cds.internal.unity3d.com/unity/com.unity.pipeline.git` (internal). |

Local CLI `/Users/daliys/.unity/bin/unity` was **not** re-run. The task states it reports `1.0.0-beta.10`. That is older than beta.12 and is not evidence of the package version.

## What changed since September 2026

September opened on 0.5/0.6 (Unsafe.dll report, CLI beta.8). The September package line that matters here:

- **0.7.0-exp.1** — registry 2026-09-11, changelog date 2026-09-10, forum 2026-09-14. This is what the harness runs.
- **0.8.0-exp.1** — changelog date 2026-09-22, registry 2026-09-25, forum 2026-09-28. This is still latest on 2026-10-04.
- **CLI 1.0.0-beta.12** — 2026-10-01. Not a package release. It changes `unity command` listing only when the Editor has Pipeline 0.8.

Nothing in the registry or the product-update forum, through 2026-10-04, publishes a Pipeline version after 0.8.0-exp.1. **MEASURED** (registry) and **DOCUMENTED** (forum index still points at the 0.8 thread; later staff posts are CLI beta.12, Hub, graphics).

### 0.8 breaking changes (all six "Heads up")

**DOCUMENTED** by the changelog and the 2026-09-28 post. Source spot-checks are marked.

1. **Attributes moved** to `Unity.Pipeline.Attributes`. **CONFIRMED** paths, namespace, and absence of a type forwarder. Forum: URP `RenderGraphDebugCLI.cs` `CS0246` on `CliCommandAttribute` under 6000.7.0b2 with 0.8, fine on 0.7 (AndreaGalet, 2026-09-28). Unity's reply was that the package cache must reload and that the URP package itself needs a fix. That is external evidence the old `Unity.Pipeline` reference is no longer enough.
2. **Loopback only.** Editor and Runtime servers bind `127.0.0.1` and accept only `Host: 127.0.0.1:<port>`. **CONFIRMED** `LoopbackHost`, `AddLoopbackPrefixes` → `http://127.0.0.1:{port}/`, and `IsHostAllowed`. `localhost` and `::1` are refused. 0.7 bound `http://+:{port}/` and checked `IPAddress.IsLoopback` on the peer, so `Host: localhost` worked. **CONFIRMED** 0.7 `BasePipelineServer.AddLoopbackPrefixes` comment and prefix.
3. **`capture_game_view` default is screen, not camera.** **CONFIRMED** below. Nuance the changelog compresses: omitted `source` is `screen` unless `camera` is non-empty, in which case the source becomes `camera`.
4. **Samples left the package.** **CONFIRMED** 0.8 `package.json` has `"samples": null`. **DOCUMENTED** new package id `com.unity.pipeline.samples`. 0.7 still embeds `Samples~/CodeReload`.
5. **Development players no longer get a generated code-reload `link.xml`.** Roslyn ships only to the Editor and desktop players. **DOCUMENTED**. Not re-read beyond the changelog.
6. **Editor server settings moved** to Project Settings > Pipeline > Editor, stored under `ProjectSettings/`. `EditorPipelineManager.asset` is not migrated. **DOCUMENTED**. No `EditorPipelineManager.asset` string under `Assets/NexusUnity/Editor/Pipeline` (**CONFIRMED** grep).

Also **DOCUMENTED**, and checked where noted:

- `CommandRegistry` is now a public type. **CONFIRMED**: 0.7 is `static class CommandRegistry` (internal); 0.8 is `public static class CommandRegistry`. `SetDiscovery` and `ClearCache` went from public-on-an-internal-class to `internal`. `DiscoverCommands`, `RegisterCommand`, and `UnregisterCommand` are public. The changelog sentence "discovery and caching stay internal" matches `SetDiscovery` / `ClearCache`, not `DiscoverCommands`.
- `/api/commands?detail=tags` added. **CONFIRMED** in 0.8 `HandleCommandsRequest`. `group_by` other than flat, and `sort=package`, return 400 when `detail=tags`.
- Player commands are listed on the Editor server and identified by the `runtime` tag. **CONFIRMED**: 0.8 `BasePipelineServer` no longer contains `IncludeRuntimeOnlyCommands`. 0.7 `EditorPipelineServer` overrides it to `false` and the catalog filter is `.Where(c => IncludeRuntimeOnlyCommands || !c.RuntimeOnly)`. `CommandInfo.RuntimeOnly` in 0.8 is `HasTagInSubtree("runtime")`; `WithRuntimeTag` still folds the bool into that tag. `simulate_key` / `simulate_pointer` in 0.8 drop `RuntimeOnly = true` and keep `Tags = { "runtime/input" }`. `cleanup_codereload` and `codereload_status` drop `RuntimeOnly = true` as well. Batch still refuses the runtime tag (**CONFIRMED** comment in `BatchCommand.cs`).
- The nine `*_status` commands return a JSON object instead of a JSON string. **CONFIRMED** signatures below. `ExecResponseSerializer.cs` is byte-identical, so the outer `{success, result}` wrap did not change.
- Runtime asmdef player gate changed from `DEVELOPMENT_BUILD` to `ENABLE_PROFILER`. **CONFIRMED** `defineConstraints`: 0.7 `UNITY_EDITOR || DEVELOPMENT_BUILD || ENABLE_RUNTIME_PIPELINE`; 0.8 `UNITY_EDITOR || ENABLE_PROFILER || ENABLE_RUNTIME_PIPELINE`.
- License: Unity Terms of Service replaces the Package Distribution License. **DOCUMENTED**.
- IL interpreter fixes and more interpreted C#. **DOCUMENTED**. Not relevant to Nexus command contracts.
- `codereload_status` and `cleanup_codereload` are on the Editor server even without a runtime driver. **DOCUMENTED**, consistent with dropping `RuntimeOnly`.
- Entering Play Mode uses `TypeCache.GetMethodsWithAttribute` for `[CodeReload]` instead of scanning every assembly. **DOCUMENTED**.
- Unity staff (ThomasLopez, 2026-09-29, same thread): out of experimental is "approximately a month-ish"; no GA date. As of 2026-10-04 the published version is still `-exp.1`. **DOCUMENTED**. No compatibility promise beyond the changelog's heads-up.

### What 0.8 did not change

**CONFIRMED** by set comparison and hashes:

- Production `[CliCommand("name"` registrations outside `Tests/`: **162 names, set equal**. No added or removed command names. No new capture command name. `recompile` / `recompile_status` still exist. `screenshot`, `capture_game_view`, `capture_scene_view`, `capture_editor_element`, `capture_runtime_element` still exist (0.7 `capture_scene_view` is in the same file; the name set matched).
- `Runtime/SecurityTokenManager.cs`, `Runtime/Common/PipelineJobs.cs`, and `Runtime/Common/ExecResponseSerializer.cs` are byte-identical.
- `.claude/skills/unity-pipeline/SKILL.md` is byte-identical. 0.8 did not ship a new skill and did not update the existing one for the listing or loopback changes.
- Assembly names are the same five product asmdefs: `Unity.Pipeline`, `Unity.Pipeline.Editor`, `Unity.Pipeline.Attributes`, `Unity.Pipeline.IlInterpreter`, `Unity.Pipeline.CodeGen`. 0.7 already had `Unity.Pipeline.Attributes` (`rootNamespace` `Unity.Pipeline.CodeReload`, `autoReferenced: true`, no references, no define constraints). 0.7 put only `CodeReloadAttribute`, `OnCodeReloadAttribute`, and `CodeReloadOverrideMethodAttribute` there (`namespace Unity.Pipeline.CodeReload`). 0.8 adds the two CLI attributes to that same asmdef without changing the asmdef's `rootNamespace` (the C# namespace of the moved types stays `Unity.Pipeline.Commands`).
- Dependencies are unchanged: `com.unity.test-framework` 1.1.33, Newtonsoft JSON, Mono.Cecil, UIElements module, screencapture module. `unity` field stays `6000.0`.
- Job routes stay `POST /api/exec` with `job: true`, `GET /api/job?id=`, `POST /api/job/cancel`. **CONFIRMED** those paths are still in 0.8 `BasePipelineServer`. Retention comment is unchanged: in-memory, discarded on domain reload, 100 terminal jobs. 0.7 constants `MaxRetainedTerminalJobs = 100` and `MaxQueuedJobs = 100`.

## Installed 0.7 surface the Nexus contracts sit on

### Attributes and discovery

**CONFIRMED.** `CliCommandAttribute` properties: `Name`, `Description`, `MainThreadRequired` (default true), `RuntimeOnly` (default false), `Tags` (default empty). `CliArgAttribute` targets parameter, field, and property; `Name`, `Description`, `Required` (default false), `DefaultValue`.

`CommandRegistry` is internal. External assemblies cannot call `RegisterCommand` even though the method is `public`. Registrations last until `UnregisterCommand` or a domain reload; a Play Mode registration is dropped when Play Mode exits (`PlayModeSessionEnded`). **DOCUMENTED** in the 0.7 changelog entry inside `package.json` `_upm.changelog` and `Documentation~/creating-commands.md` as cited by that changelog. Discovery (`ICommandDiscovery`, `TypeCacheCommandDiscovery`) stays inside the package.

Editor catalog: `GET /api/commands` defaults `detail=full`, `group_by=flat`. Accepted `detail` values are `full` and `compact` only (**CONFIRMED** the 400 string). `detail=tags` does not exist and would 400. Runtime-only commands are hidden from the Editor server and remain executable (**CONFIRMED** `RuntimeOnly` doc comment and `IncludeRuntimeOnlyCommands => false`).

### Transport, auth, jobs

**CONFIRMED** unless noted.

- Bind: `http://+:{port}/` (every interface). Peer must be loopback. Bearer `Authorization` checked in `IsAuthorized`. 0.8 changelog security note: the 0.7 port was reachable from the LAN, but requests still needed a loopback origin and the bearer token. **DOCUMENTED** for the LAN claim; the wildcard prefix is **CONFIRMED**.
- Exec envelope: success is `{"success":true,"result":...}`; a successful null result is still emitted; default mode strips `executedAt` / `command` / `executionTimeMs`. This is the wrap around whatever the command method returns.
- Jobs: `POST /api/exec` with `"job": true`, poll `GET /api/job?id=`, cancel `POST /api/job/cancel`. Serialized execution. Domain reload drops queued and running jobs because the registry is static memory.
- Token: `SecurityTokenManager` persists the token in `SessionState` so it **survives domain reload** and is regenerated on Editor restart or `RotateToken`. The July 2026 "Play Mode mints a new bearer token" failure is not the 0.7 behavior.
- `recompile` cannot return when compilation finishes, because a successful compile domain-reloads and kills the HTTP request. Clients poll `recompile_status` and must tolerate a dropped connection. Status file `Temp/pipeline_recompile_status.json`. `recompile_status` in 0.7 returns `string` (the payload passed through `JsonConvert.SerializeObject`), so the exec envelope's `result` is a **JSON string**, not an object. Same pattern for `build_status`, `switch_build_target_status`, `package_status`, `audit_status`, `test_status`, and the three bake status commands.

### Capture and compile, as shipped in 0.7

**CONFIRMED** `CaptureCommands.cs`: `capture_game_view` `source` default is the literal `"camera"`. `source=screen` throws unless Play Mode. Description says screen misses Edit Mode. Also `screenshot` (tag `capture`) and `capture_runtime_element` (`RuntimeOnly = true`).

`recompile` / `recompile_status` are the compile commands. No successor name in 0.8. CLI `unity recompile` (beta.11, **DOCUMENTED**) is a CLI entry point that drives the Editor; it is not a new package command.

### Skill already in 0.7

**CONFIRMED** path (dot-directory; a normal directory listing hides it):

`Library/PackageCache/com.unity.pipeline@ab05e9cd7f74/.claude/skills/unity-pipeline/SKILL.md`

Name `unity-pipeline`. It documents `set_autotick`, `recompile` → `recompile_status`, `run_tests`, `reload_file`, `run_script`, `eval` / `eval_file`, `audit`. It still says player commands (`log`, `set_timescale`, `runtime_status`) hit the Runtime server and do not run in the Editor. That sentence is **stale relative to 0.8's listing change** and was not edited in 0.8. Port ranges in the skill: Editor `7800-7849`, Runtime `7900-7949`.

## Nexus contracts vs 0.8

`Editor/Pipeline/UnityMCP.Editor.Pipeline.asmdef` **CONFIRMED**:

- references: `UnityMCP.Editor`, `Unity.Pipeline`. It does **not** reference `Unity.Pipeline.Attributes`.
- `versionDefines`: package `com.unity.pipeline`, expression `0.0.0-exp`, define `NEXUS_HAS_PIPELINE`.
- `defineConstraints`: `NEXUS_HAS_PIPELINE`.

Unity version defines treat `0.0.0-exp` as an open prerelease floor, so 0.7 and 0.8 both set the define. **INFERRED** from Unity version-define rules and the expression in the file; the define was not compiled this pass. There is no package.json dependency on `com.unity.pipeline` inside the Nexus package; the harness pins the version in the parent manifest.

| Contract | On installed 0.7 | If the manifest moves to 0.8 |
| --- | --- | --- |
| Readiness / domain reload | Token survives reload (**CONFIRMED**). Jobs do not (**CONFIRMED**). `recompile` drops the HTTP call across reload (**CONFIRMED**). | Same three files are byte-identical. No changelog entry about token or job lifetime. |
| Canonical envelope inside `{success, result}` | Outer serializer unchanged. | **CONFIRMED** identical `ExecResponseSerializer.cs`. A Nexus method that returns an object is still wrapped once. Methods that returned a pre-serialized JSON **string** are the hazard: eight/nine status commands stopped doing that. A client that `JSON.parse`s `result` a second time will break; a client that expected an object and was string-unwrapping will start working. |
| `*_status` shape | `result` is a string for `recompile_status`, `build_status`, `switch_build_target_status`, `package_status`, `audit_status`, lighting/navmesh/occlusion bake status, and the command `test_status` (its runner helper can still return `string`; the command method in 0.8 is `JToken` and `StatusFileReader.Parse`s it). | **CONFIRMED** those command return types became `JToken` or `RecompileStatusPayload`. Changelog calls this nine commands. |
| Capture | Nexus S-02 locks Nexus's own `capture_game_view` parameters (`format=jpg`, `quality=85`, `max_dimension=768`, `view=game`, `delivery=base64`). Those are not Pipeline's arguments (`source`, 1280×720 PNG). | Pipeline's default `source` becomes screen, with the camera-name exception above. That breaks a **direct** `unity command capture_game_view` that omitted `source` and wanted a camera render, including Edit Mode callers who relied on the camera path. It does not by itself change the Nexus descriptor the drift test locks. No Pipeline `capture_game_view` call was found while reading the drift test. |
| CLI drift Ignore vs Fail | Lookup assembly matches. Ignore means "package assembly not loaded", which is the wrong reason on 0.8 but the right reason when the package is absent. | Ignore fires even though the package **is** loaded. The compile-break assertion never runs. **CONFIRMED** control flow. |
| Command enumeration and tags | Editor listing hides `RuntimeOnly` commands. `detail=tags` is an error. 162 production names. | Same 162 names. Editor listing includes the old runtime-only set, keyed by the `runtime` tag rather than a hidden flag. `detail=tags` returns one row per tag with a count. CLI beta.12 with no arguments lists ~50 tags instead of ~160 commands **only if** Pipeline is 0.8; on this 0.7 harness it still lists commands (**DOCUMENTED**). |
| Runtime registration | API exists but the type is internal. Nexus cannot call it. | Public `RegisterCommand` / `UnregisterCommand` / `DiscoverCommands`. Still dropped on domain reload (method `PlayModeSessionEnded` still present; lifetime sentence not withdrawn). |
| Loopback | Wildcard bind, loopback peer, `Host: localhost` accepted. | Must dial `127.0.0.1`, not `localhost`. Nexus's **own** server already uses `http://127.0.0.1:{port}/` (`Editor/MCPServer.Discovery.cs`, `Editor/nexus_bridge/_transport.py`). That is not the Pipeline client. Whether CLI beta.10 sends `Host: localhost` to Pipeline was **not measured**. |
| Compile of the Pipeline asmdef | Attributes resolve through `Unity.Pipeline`. | **INFERRED** `CS0246` on `[CliCommand]` / `[CliArg]` in `UnityMCP.Editor.Pipeline`, same failure URP hit. Not compile-tested. Referencing only `Unity.Pipeline.Attributes` is enough to **declare** commands (that asmdef has zero references). Discovery and execution still live in `Unity.Pipeline`, which is define-stripped out of non-editor, non-profiler players unless `ENABLE_RUNTIME_PIPELINE` is set. |

`AttributeNamed` in the drift test matches `attribute.GetType().Name`, not the assembly. After a corrected `FindType` assembly string, the existing property checks do not need a namespace edit.

## What is stale in `NexusFeature~/MASTER_PLAN_V2.md`

Still right:

- 0.8.0-exp.1 was published around 2026-09-28 and moved `[CliCommand]` / `[CliArg]` to assembly `Unity.Pipeline.Attributes` (lines 83–84, F14, P0-06). **CONFIRMED**, with the namespace caveat above.
- The open range `0.0.0-exp` will define `NEXUS_HAS_PIPELINE` on 0.8, and the Pipeline asmdef does not reference `Unity.Pipeline.Attributes`. The "likely CS0246" is the right failure mode. **INFERRED**, not compiled.
- No GA date. **DOCUMENTED** still true on 2026-10-04. Staff added an informal "out of experimental ~ a month" on 2026-09-29, which the plan does not record.
- P0-06's preferred fix (reference the attributes assembly, keep 0.7 compiling) matches the 0.8 layout. On 0.7 the attributes assembly does **not** contain `CliCommandAttribute`, so a hard reference to `Unity.Pipeline.Attributes` for those two types fails on the installed package. A version-gated define or a thin asmdef, which P0-06 already suggests, is required. Do not switch the only reference to `Unity.Pipeline.Attributes` while the harness is on 0.7.

Stale or over-compressed:

- Line 738 timeline stops at CLI `1.0.0-beta.11` (2026-09-22). **DOCUMENTED** beta.12 shipped 2026-10-01. It does not invalidate the 2026-10-02 measurement of `unity mcp` tool count on Pipeline 0.7: beta.12 says older packages still return the full listing. The tag-index default applies only after a Pipeline 0.8 upgrade.
- Line 79: `simulate_key` / `simulate_pointer` are dev-player-only. That matches **0.7 hiding** (`RuntimeOnly = true`, Editor filter false) and the skill text. It does **not** match 0.8, which lists them on the Editor server under tag `runtime/input` and reports why they cannot run (missing Input System or Active Input Handling). **DOCUMENTED** plus the attribute diff. They are still not a general Edit Mode input API.
- "Attributes moved" is easy to misread as "the attributes assembly is new in 0.8". The assembly shipped in **0.7** for `[CodeReload]` / `[OnCodeReload]` / `[CodeReloadOverrideMethod]`. Only the CLI attributes moved later.
- The plan does not mention the `*_status` string-to-object break, the `127.0.0.1`-only Host check, or `capture_game_view` defaulting to screen. Those are the 0.8 breaks that touch readiness polling, any future Pipeline HTTP client, and direct capture calls. The S-02 canonical capture descriptor is a different command shape.
- The shipped skill is not new in 0.8, and 0.8 did not publish a second skill. P0-06 / skills notes that talk about "new official skills that ship with Pipeline" should treat `unity-pipeline` as already on disk in the 0.7 cache, and as **not updated** for 0.8 behavior.

## S-02 Ignore risk, today

**Not active on the installed package. Real on the published latest, the moment the pin moves.**

Today `FindType("Unity.Pipeline", "Unity.Pipeline.Commands.CliCommandAttribute")` names the assembly and the type that `Runtime/Common/CliCommandAttribute.cs` actually defines. Ignore means the package assembly is absent, which is what the message says. It does not mean "attributes moved".

On `0.8.0-exp.1` the same call returns null, the message is false (the package is loaded; the assembly name is wrong), and the test does not fail the missing projection. That is a lock that opens itself. The fix is to probe `Unity.Pipeline.Attributes` when the 0.7 lookup is null, and to fail rather than Ignore when some Pipeline assembly is loaded but the attribute type is missing. Namespace `Unity.Pipeline.Commands` can stay. This pass did not edit the test.
