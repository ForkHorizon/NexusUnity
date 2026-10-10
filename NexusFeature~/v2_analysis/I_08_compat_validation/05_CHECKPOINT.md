# Checkpoint after objectives 1–3

## S-02

The omit-path spies and the fail-closed attribute probe are commit `5ecc7fb`. They passed on Pipeline 0.7 (drift 3, plus envelope 29, registry 9, capture look 10) and on Pipeline 0.8 (drift 3, 0 skipped). `01D_SEAM_REVIEW.md` closed FP-2, FP-3, FP-4, and MF-2. The suite that was on the branch before that commit ignores the CLI lock on 0.8 and says Pipeline is not loaded.

## Pipeline 0.8

Unchanged Nexus does not compile. CS0246 is only in `NexusPipelineCommands.cs` for `CliCommand`, `CliArg`, `Tags`, and `MainThreadRequired`. Adding `Unity.Pipeline.Attributes` next to `Unity.Pipeline` compiles on 0.8 and on the live 0.7 editor. P0-06 is a real break. The compatibility fix is commit `596d171`, separate from the drift-suite commit. `01D` called that reference the smallest fix that still compiles on 0.7 and did not stamp the commit.

## Recompile

CLI beta.12 returns structured file, line, column, code, and message for one `CS0103` and for three `CS0103`s in one file. `recompile_status` is an object whose `errors` are raw strings. A no-change recompile is `up_to_date` (CLI wall 1017.7 ms). Exit 6 is also a network error (R1) and an HTTP 401 (R4). R1's JSON does not say domain reload. CS1030 is in `/tmp/nexus-p08-logs/editor-08.log` and in `evidence/contracts08/editor-warning-cs1030.txt`. It is not in the saved recompile or status JSON. The recompile call beside that warning was the 401.

## Capture

With a Game view target, omitted `capture_game_view` is `source=screen` in Edit Mode and includes a magenta overlay marker. `source=camera` shows the same scene markers and not the overlay. Nexus game JPEG/PNG matches that screen image's markers, writes JPEG, and writes outside Assets without `.meta`. Scene framing was not shown.

## Can the discovery experiment run fairly?

Not on port 7800. It was not run. A fair run is a restart that was not done, not a missing editor.

U-TAGS needs CLI beta.12 and Pipeline 0.8. That pair existed on `/tmp/nexus-p08-harness`. Its Pipeline server bound `127.0.0.1:7800` while PID 4465 still listens on `*:7800`. Clients that dial `127.0.0.1:7800`, including a harness client that trusts the port file, can reach the scratch editor. The scratch editor was stopped. A follow-up check of the harness `editor_status` returned `ready` for `/Users/daliys/Daliys/UnityProjects/UnityTestForNexus`.

A fair rerun has to set `ProjectSettings/Packages/com.unity.pipeline/EditorPipelineConfig.json` `m_Port` to 7810 before starting that editor, and every CLI call has to pass `--project-path /tmp/nexus-p08-harness` to `/tmp/unity-cli-beta12/unity`. That restart was not done. The arms were not run. `06_DISCOVERY_PREREG.md` is frozen with zero scores. `07_DISCOVERY_RESULTS.md` records that there are no results. `09_ADVERSARIAL_REVIEW.md` was written while 06 was still absent; the file is present now.
