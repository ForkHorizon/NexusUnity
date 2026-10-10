# Upstream primary sources checked by the lead

Date: 2026-10-04. These notes are the lead's own reads. Agent reports `01` and `02` may add source inspection. Do not upgrade the harness from this file.

Label key: DOCUMENTED = official post text. CONFIRMED = this machine or this package cache. UNKNOWN = not run here.

## Installed on this harness (CONFIRMED)

- Git tip: `bfdd0f7e71c851fb4615956f3c420940a9623220` on `rework2/S-02`. Not merged to `development`.
- Unity Editor: `6000.3.16f1`, PID 4465, play mode stopped, `editor_status` ready at 2026-10-04T21:47:16Z.
- `com.unity.pipeline` `0.7.0-exp.1` in `Library/PackageCache/com.unity.pipeline@ab05e9cd7f74`.
- Unity CLI binary `/Users/daliys/.unity/bin/unity` version `1.0.0-beta.10`.
- Pipeline HTTP listens on `*:7800` (all interfaces), which matches 0.7. 0.8 changes this (below).
- `CliCommandAttribute` in this cache: namespace `Unity.Pipeline.Commands`, file `Runtime/Common/CliCommandAttribute.cs`. No `Unity.Pipeline.Attributes` asmdef in this cache.

## Pipeline 0.8.0-exp.1 (DOCUMENTED)

Source: Unity Discussions, Thomas Lopez, 2026-09-28.
https://discussions.unity.com/t/unity-pipeline-package-0-8-0-exp-1-is-available-now/1737832

Heads-up items that touch Nexus:

1. `[CliCommand]` and `[CliArg]` moved to assembly `Unity.Pipeline.Attributes`. An asmdef that referenced `Unity.Pipeline` for those attributes must add `Unity.Pipeline.Attributes`. A follow-up on that thread shows `CS0246 CliCommandAttribute` inside URP's package cache on 6000.7.02b until the cache is rebuilt. This is the S-02 Ignore risk: `NexusCommandCliDriftTests` calls `FindType("Unity.Pipeline", "Unity.Pipeline.Commands.CliCommandAttribute")` and `Assert.Ignore` when the type is null. Not compiled against 0.8 in this sprint yet (UNKNOWN whether Nexus's own asmdef fails the same way; the published break matches the plan's P0-06 claim).
2. Servers bind to `127.0.0.1` only, and the post says to dial the port by IP, not `localhost`.
3. `capture_game_view` defaults to `source=screen` and **now works in Edit Mode**. `source=camera` is the old behaviour. This is the capture-deathmatch fact. On installed 0.7, `source=screen` throws unless `Application.isPlaying` (CONFIRMED in `CaptureCommands.cs`).
4. `*_status` commands return real JSON objects. In 0.7, `recompile_status` returns `JsonConvert.SerializeObject(...)`, a JSON **string** (CONFIRMED in `RecompileCommand.cs`).
5. `CommandRegistry` is public. `/api/commands?detail=tags` returns one row per tag with a count. Needs the latest CLI.
6. Samples moved to `com.unity.pipeline.samples`.
7. Player commands are listed on the Editor server with a `runtime` tag.
8. Estimate from the same thread (Thomas Lopez, 2026-09-29): out of experimental in "approximately a month-ish". Not a promise. GA date unknown.

GitHub tracker linked from the post: https://github.com/Unity-Technologies/com.unity.pipeline/issues

## Unity CLI (DOCUMENTED)

Installed: beta.10 (CONFIRMED). Newer releases exist and were **not** installed. `unity self-update` was not run.

- beta.11, 2026-09-22: `unity recompile` lists each compiler error with file and line; exit 0 success, 6 compile errors, 7 no Editor. https://discussions.unity.com/t/unity-cli-1-0-0-beta-11-is-rolling-out/1737353
- beta.12, 2026-10-01: https://discussions.unity.com/t/unity-cli-1-0-0-beta-12-is-rolling-out/1738085
  - `unity command` with no arguments lists **about fifty tags with counts**, not the 160-odd commands. Drill in with `unity command --tag <tag>`. Full catalog is `unity command --detail full`. **The tag index needs Pipeline 0.8.0-exp.1. Older packages still return the full listing.**
  - `unity status --until-ready` waits until the Editor reports ready (default timeout 300s). This is Unity's ready flag, not Nexus compile/reload epochs.
  - `unity command <name>` waits while the Editor is briefly busy after Play Mode entry or script reload.
  - `unity commands --grep <pattern>` searches commands and options.
  - A user reply (2026-10-02) describes the Editor hanging on a save-scene dialog during tests, with the CLI looking stuck and status not reporting the dialog. That matches the existing Nexus `PROBABLE_MODAL_DIALOG` idea. Not reproduced here.

## Unity Editor play-mode reload (DOCUMENTED)

https://discussions.unity.com/t/unity-6-6-is-now-available/1735357 (2026-09-01):

- Unity 6.6 default for **new** projects is Enter Play Mode with scene reload only. Domain is not reloaded; statics are not reset.
- Stated reason: Unity 7 / CoreCLR removes traditional domain reload.
- This harness is **6000.3.16f1** (Unity 6.3), so this default is not in effect here (CONFIRMED version; the project Enter Play Mode setting was not re-read this session).

Unity 6.5 docs describe `[AutoStaticsCleanup]` / `[NoAutoStaticsCleanup]` for Play Mode without domain reload:
https://docs.unity.com/en-us/engine/6000.5/manual/scripting/compilation-and-code-reload/code-reloading-editor/domain-reloading

Installed Pipeline 0.7 already has `#if UNITY_6000_5_OR_NEWER` and `[NoAutoStaticsCleanup]` on `RecompileCommand` (CONFIRMED). That branch is not compiled in this 6000.3 Editor.

## What this does to the sprint questions

- The "154 flat commands" discovery cost is still true on **this** CLI beta.10 + Pipeline 0.7 (DOCUMENTED: older packages still return the full listing). It is no longer the latest Unity model.
- Official Edit Mode overlay capture is a 0.8 change. Nexus overlay capture is still differentiated on 0.7. Whether 0.8 matches Nexus orientation, crop, JPEG, and Scene View framing is UNKNOWN until measured.
- S-05 should be checked against `unity recompile` (beta.11+) file/line output before building a second compiler-message scraper. Not measured on this machine.
- L-02's "play-entry reload" acceptance is about 6.3-class Editors. On 6.6 new projects the domain survives play entry and statics survive unless cleaned up. Do not treat L-02 as obviously required for play entry on current Unity.
- No production package was upgraded. No second Editor was started.
