# P0-02 evidence status

Date: 2026-10-04
Branch: `rework2/P0-02`
Plan: `NexusFeature~/MASTER_PLAN_V2.md` v2.6, task P0-02

## Acceptance criterion

Twenty consecutive script writes, each compiled and domain-reloaded, with readiness becoming true only after that reload. A multi-second compile must not produce a sub-second ready verdict. Epochs must advance by one and continue from run to run. Editor.log must show the lifecycle for the write that just happened.

## Status: MET

The 20/20 sequence below is one consecutive run of commit `6fff2e8` (`fix: keep a newer script write from settling on an older compile`), which contains `d738dfc` plus the overlapping-write, focus-wait, and failed-compile probe fixes. It is not a splice of earlier attempts.

## Environment

- Unity `6000.3.16f1`, one Editor, PID 4465, project `/Users/daliys/Daliys/UnityProjects/UnityTestForNexus`
- macOS, Editor focused before the run started
- Stimulus: `Assets/NexusUnity/Editor/EvidenceTemp/P002Stimulus.cs` in assembly `UnityMCP.Editor` (deleted after the run)
- Each run rewrote that file through production `WriteFile` (`confirm: true`), so `ArmScriptWrite` and `TriggerSafeAssetRefresh` ran
- Judge: `Research~/evidence/P0-02/judge.py`. The driver recorded facts only

## EditMode and static checks on this commit

`UnityMCP.Editor.Tests.EditorReadinessTests`: 17 passed, 0 failed, 0 skipped. The four new tests cover an overlapping arm, the failure probe, a success flag that does not settle without reload, and Play Mode frames not counting as quiet ticks.

Python `Editor/tests`: 45 passed.

Nexus quality gate (`--ai off`): 0 errors, 5 pre-existing warnings.

## 20-run result

`judge.py` exit 0. Median write-to-ready 4.388 s (min 3.891, max 4.663). Median forced domain-reload refresh 1.909 s. Median domain-reload profiling 1.788 s. Compile epoch 3→23 and reload epoch 2→22, each run exactly +1. Every `before_reload` sample had `ready=false`. Every `after_write` reason was `script_write`. Every slice compiled `UnityMCP.Editor.dll` and contained one `Reloading assemblies after forced synchronous recompile.`

| run | wait_s | tundra_s | reload_profiling_s | forced_refresh_s | compile | reload | not_ready | ready_before_reload |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 00 | 3.891 | 0.87 | 1.663 | 1.921 | 3→4 | 2→3 | script_write | false |
| 01 | 4.170 | 0.83 | 1.695 | 1.801 | 4→5 | 3→4 | script_write | false |
| 02 | 4.330 | 0.83 | 1.731 | 1.822 | 5→6 | 4→5 | script_write | false |
| 03 | 4.313 | 0.78 | 1.684 | 1.774 | 6→7 | 5→6 | script_write | false |
| 04 | 4.378 | 0.86 | 1.788 | 1.909 | 7→8 | 6→7 | script_write | false |
| 05 | 4.318 | 0.86 | 1.803 | 1.914 | 8→9 | 7→8 | script_write | false |
| 06 | 4.104 | 0.80 | 1.729 | 1.833 | 9→10 | 8→9 | script_write | false |
| 07 | 4.663 | 0.97 | 1.708 | 1.799 | 10→11 | 9→10 | script_write | false |
| 08 | 4.290 | 0.81 | 1.728 | 1.823 | 11→12 | 10→11 | script_write | false |
| 09 | 4.268 | 0.89 | 1.702 | 1.809 | 12→13 | 11→12 | script_write | false |
| 10 | 4.394 | 0.93 | 1.792 | 1.899 | 13→14 | 12→13 | script_write | false |
| 11 | 4.529 | 0.87 | 1.810 | 1.911 | 14→15 | 13→14 | script_write | false |
| 12 | 4.256 | 0.80 | 1.749 | 1.859 | 15→16 | 14→15 | script_write | false |
| 13 | 4.465 | 0.89 | 1.819 | 1.915 | 16→17 | 15→16 | script_write | false |
| 14 | 4.559 | 0.88 | 1.830 | 1.928 | 17→18 | 16→17 | script_write | false |
| 15 | 4.417 | 0.91 | 1.738 | 1.838 | 18→19 | 17→18 | script_write | false |
| 16 | 4.567 | 0.82 | 1.881 | 2.004 | 19→20 | 18→19 | script_write | false |
| 17 | 4.464 | 0.79 | 1.811 | 1.915 | 20→21 | 19→20 | script_write | false |
| 18 | 4.401 | 0.78 | 1.878 | 1.990 | 21→22 | 20→21 | script_write | false |
| 19 | 4.388 | 0.81 | 1.800 | 1.931 | 22→23 | 21→22 | script_write | false |

## Raw artifacts

- `Research~/evidence/P0-02/live/runs.jsonl` — append-only facts
- `Research~/evidence/P0-02/live/log-00.txt` through `log-19.txt` — Editor.log slices anchored at each run's nonce. Licensing and access-token lines are removed
- `Research~/evidence/P0-02/verdict.json` — judge output
- `Research~/evidence/P0-02/judge.py`

The first judge pass failed because it used the first `Asset Pipeline Refresh` total in each slice. That line is `RefreshV2(NoUpdateAssetOptions)` at about 0.11 s and precedes the compile. The same raw logs contain one later `StopAssetImportingV2(ForceSynchronousImport | ForceDomainReload)` total. The judge now requires that line, plus `UnityMCP.Editor.dll` in the slice. Re-judging did not re-run the Editor.

## Limitations

- This Editor was already warm. Forced-reload totals are about 1.8–2.0 s and write-to-ready is about 3.9–4.7 s. That is not the cold 7.5–8.5 s figure. No run was ready in under a second, and none was ready before its reload.
- These live cases were not part of the 20: a compile error then a fix, a no-op refresh, two script writes in one cycle, a Play Mode transition during the wait, the 15 s unfocused timeout, `LockReloadAssemblies`, and an Editor restart. EditMode covers the overlapping arm, the failed-compile probe, the no-op refresh, and Play Mode quiet ticks.
- `unity_wait` compilation no longer calls `refresh_asset_database`. That does not close issue #0012 by itself.
