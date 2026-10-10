# 09 adversarial review

Fresh read of the compatibility writeups and the files they cite. This review did not edit product code, run Unity, or call port 7800. `01D_SEAM_REVIEW.md` is present. It closes four 01C edits and does not approve a commit. `06_DISCOVERY_PREREG.md` is not in this directory.

CREDIBLE means the cited file contradicts the claim or leaves it unmeasured. WEAK means the attack does not survive the file.

## Dual asmdef reference

The break is real. `evidence/pipeline08/compile-08.log` reports CS0246 only from `NexusPipelineCommands.cs`, for `CliCommandAttribute`, `CliCommand`, `CliArgAttribute`, `CliArg`, `Tags`, and `MainThreadRequired`. The current list is `UnityMCP.Editor`, `Unity.Pipeline`, `Unity.Pipeline.Attributes` (`Editor/Pipeline/UnityMCP.Editor.Pipeline.asmdef:4-7`). 02 says `compile-08-ref.log` exits batchmode with no `error CS`, and the live 0.7 editor recompiled that list. That is a compile result for these two editors. It is not "enough for 0.7 and 0.8."

| Objection | Mark | Why |
| --- | --- | --- |
| Two editors are not the two version lines. | CREDIBLE | The runs are Pipeline `0.7.0-exp.1` on PID 4465 and `0.8.0-exp.1` in `/tmp/nexus-p08-harness`. The asmdef `versionDefines` expression is `0.0.0-exp` (`UnityMCP.Editor.Pipeline.asmdef:20-24`), so every installed pipeline compiles this reference list. No other 0.7 or 0.8 build is in the logs. |
| 0.8 did not repeat the 0.7 suite. | CREDIBLE | `drift-seam-on-08.xml` is `testcasecount="3"` `passed="3"` for `NexusCommandCliDriftTests` only. The parent suite still says `testcasecount="216"`. Envelope 29, registry 9, and capture look 10 are the 0.7 counts in `evidence/mf_07/`. 02 also says Unity Test Framework resolved `1.6.0` while the manifest asked for `1.7.0`, and that a filter matching nothing is exit 0. The seam file is not that empty run. It is still three tests. |
| 01D's "no compile break" is only those logs. | CREDIBLE | 01D line 29 calls the three-name list the smallest fix that does not leave a compile break. 01D line 35 does not approve a commit. 02's last paragraph still says Q5–Q6 need the live `/api/commands` catalog. `catalog_nexus.json` is a Pipeline CLI catalog (`command`, project `/private/tmp/nexus-p08-harness`, port 7800), not that HTTP catalog. 08's P0-06 cell treats the question as closed. |
| Attributes-only was shown to fail on 0.7. | WEAK | 01D infers it from where `CliCommandAttribute` lived on 0.7. There is no 0.7 log of an Attributes-only reference list. The measured 0.7 result is the dual list. |
| `Tags` and `MainThreadRequired` survived only as names in the log. | WEAK | They are in the CS0246 list, and `compile-08-ref.log` is reported clean, so those names resolved. `catalog_nexus.json` then shows `tags` and `mainThreadRequired: true` on all three wrappers. That catalog was taken on port 7800 during the collision 05 describes; `editor_status.json` does report `projectPath` `/private/tmp/nexus-p08-harness`. |
| The seam XML is a zero-assert false pass. | WEAK | `asserts="0"` is on the passed cases. The three method names ran, `result="Passed"`, duration is non-zero, and `CliArgsMatchCanonicalDescriptors` is not `Ignored`. |

## False-pass holes, with 01D open

01D is not absent. Re-opening the four edits it closed does not match the current test.

| 01C edit | 01D | This read |
| --- | --- | --- |
| FP-2, FP-3, FP-4, MF-2 | Closed at 01D lines 9–23 | Agree. `ExecuteAsync`, `ExecuteFromCli`, and the reflected wrapper now sit under `ObserveCapture`. `CaptureLookRequest.TryParse` reads `height` and `quality` (`CaptureLookRequest.cs:36-38`). A WEAK attack is to call those four still open. |
| Discarded `ReadErrorLogs` list, and a rewrite of the default `Capture` lambda | Left open at 01D line 25 | Still open. See below. |
| Commit | Not approved, 01D line 35 | 08 still says S-02 BUILD NOW. |

Remaining holes:

1. **CREDIBLE. The compile spy still ignores the list it hooks.** `AssertHttpCompileUsesTheResolvedWindow` sets `seen` and returns an empty list (`NexusCommandCliDriftTests.cs:183-194`). `Execute` walks whatever `ReadErrorLogs` returned (`GroupCompileErrorsCommand.cs:144-147`). Calling `ReadErrorLogs(maxLogs)` and grouping a different list stays green. 01D already classifies this as outside the four edits. 08's "test holes / BUILD NOW" does not.

2. **CREDIBLE. The capture seam does not record the request 01_S02 describes.** `ObserveCapture` keeps format, width, height, quality, and max edge (`NexusCommandCliDriftTests.cs:251-270`). It does not read `View`, `TargetPath`, `TargetInstanceId`, or `CaptureLookCall.WriteFile`. An omit path that flips `view` to scene, or delivery to file, stays green. The direct `TryPrepareLook` block still checks some of those fields on an object the test built (`NexusCommandCliDriftTests.cs:127-137`). 01D line 19 says that block is not the close.

3. **CREDIBLE. Green drift is an in-process invoke, and the saved CLI transcripts show the host parser is a different lock.** The test calls `look.Invoke` with a typed array (`NexusCommandCliDriftTests.cs:102-108`). These stdout files never reached that method:

   - `nexus_jpg.stdout.txt`: `--width` got `format=jpg`, `--height` got `delivery=file`, `--quality` got `max_dimension=768`. `INVALID_COMMAND_ARGS`.
   - `nexus_png.stdout.txt`: the same shift for `format=png`.
   - `nexus_scene.stdout.txt`: `target=CenterGreen` was bound to `--quality`, and `max_dimension=768` was rejected as not a parameter.

   `nexus_cli_jpg.stdout.txt` is a later success (`source` `game_view`). 04 says a `--` before the arguments avoids the global `--format` collision. The drift suite does not encode that command line. `AttributeNamed` still matches the short name `CliCommandAttribute` (`NexusCommandCliDriftTests.cs:350`). That is the SF-2 leftover 01C left open. 01D does not mention it.

4. **CREDIBLE, narrower.** `max_logs: 0` is never sent. `TryReadCompileArguments` treats a missing value as 50 and a present 0 as 0 (`GroupCompileErrorsCommand.cs:100`). Treating 0 as omit stays green. `request_id` is asserted only as null on the omit helper (`NexusCommandCliDriftTests.cs:166-168`), never as `meta.request_id` on an `ExecuteAsync` envelope. 01D did not close either 01C leftover.

5. **WEAK.** `CaptureLookRequest` also accepts `max_long_edge` when `max_dimension` is absent (`CaptureLookRequest.cs:66-71`). The alias probe does not send it. That is an unlocked second name, not one of the 01C edits.

6. **WEAK.** The `TryPrepareLook(` substring and the single `call.Request.Format =` count can false-fail if the needle is split. 01D line 9 says they are not what closes FP-2. They do not keep the seam green by themselves.

## Recompile: exit 6 and the warning

03 is right that exit 6 is not one condition. It is wrong that the saved files show only two, and the warning sentence is not in the evidence directory.

| Run | Saved body | Exit | Mark |
| --- | --- | --- | --- |
| R1 `r1_recompile.json` | `data: null`, `COMMAND_FAILED`, `Network error: An error occurred while sending the request.` | 6 (`r1_recompile.meta.json`, 935.1 ms) | Measured network failure. |
| R1 `r1_status.json` | `completed`, `failed: false`, `errors: []`, `compilationFailed: false` | 0 | A later status. No id ties it to the failed call. |
| R2 `r2_recompile.json` | one structured `CS0103` at `One.cs(2,34)`; `warnings: []` | 6 | Structured error path. Holds. |
| R2 `r2_status.json` | `errors` is one raw string, not the structured object | 0 | 03's status-versus-recompile split. Holds. |
| R3 `r3_recompile.json` | three `CS0103` lines, lines 2–4, column 22, one file | 6 | Order holds for this file only. |
| R4 `r4_recompile.json` | `data: null`, `401 Unauthorized`, `Missing or invalid authentication token` | 6 (3154.0 ms) | Third body, not R1's network string. |
| R4 `r4_status.json` | `completed`, `failed: false`, `errors: []` | 0 | No warning field. |
| R4 `r4_strict.json` | `up_to_date`, `warnings: []`, `compilationFailed: null` | 0 | Not a strict failure. |
| R6 `r6_recompile.json` | `up_to_date`, `warnings: []`, `compilationFailed: null`; wall 1017.7 ms | 0 | No sub-50 ms figure. 03 does not invent one. |

`r2_restore.json`, `r3_restore.json`, and `r4_restore.json` are the same 401 text. Exit 6 in this folder is a network drop, a populated `data.errors` array, or an auth failure. 05 and 08 fold that into "dropped reload." **CREDIBLE.** R1's JSON does not contain "domain reload." Using it as a second measurement of L-02's mechanism is an interpretation. The network error itself is on disk.

**CREDIBLE. The CS1030 gap is not in the captured payloads.** A search of this folder finds `warning CS1030` and `P08_WARNING_MARKER` only in `03_RECOMPILE_CONTRACT.md`, `05_CHECKPOINT.md`, `S05_INPUT_CONTRACT.md`, and `08_SYNTHESIS.md`. The R4 recompile body is the 401. `r4_strict.json` has an empty `warnings` array after status was already clean, which is the limit 03 states. It does not show that a successful `unity recompile` omitted a warning that was inside that call.

**CREDIBLE, bounded.** "In source order" is three errors in `Three.cs`. There is no second file, no mixed warning, and no assembly name or diagnostic id on those objects. `compilationFailed` is `null` on `up_to_date` (R6, strict) and `false` on the clean R1 status object. S05's "already returns" list does not mention that split.

**WEAK.** Attacking the R2/R3 field list. Those objects do have `file`, `line`, `column`, `code`, `message`, and `raw`.

## Capture parity

`editor_status.json` before the later calls: `playMode` `stopped`, `projectPath` `/private/tmp/nexus-p08-harness`, Unity `6000.3.16f1`. One scratch scene. 04's marker story is that scene.

SHA-256 of the copies under `evidence/contracts08/images/`:

| Pair | Bytes | SHA-256 | Same bytes? |
| --- | --- | --- | --- |
| `unity_omit2.png`, `unity_screen.png` | 4476 | `f32106ca30a8c342a1ba739f35267490da54d870a22489eebcfa149d91409435` | Yes |
| `nexus_jpg.jpg`, `nexus_cli_jpg.jpg` | 10074 | `4584c7312c78464ffdaaf0e4cbefa1da7e0b7293c488983849dfe5bacc0973d4` | Yes |
| `nexus_png.png` | 7119 | `0aadb6f7f156994b93d906b1f1b791babb988ff3da619849b3eb1f90f6badc92` | No match to either official PNG |
| `unity_camera.png` | 4201 | `2c89706c295e014c1ea73419ad40d104bccdc5ecdb3a33168a916283aaa2cfd4` | No |
| `unity_scene.png` | 22019 | `4405ee18c58ef0609c66cf662175d1728be5a0f1add89e4bae66ead127dec24d` | No |
| `nexus_scene.png` | 33818 | `fc081575a4794ebeab59abee7549717aabb1cd6c6faf1e66977e55425cdf97f3` | No |

| Objection | Mark | Why |
| --- | --- | --- |
| Official omit versus explicit `source=screen` is only "the same size." | WEAK | The two copies are the same bytes, not merely the same length. 04's "same pixels and same byte size" holds for that pair. |
| Nexus game images are the same pixels as that screen image. | CREDIBLE | 04 does not say they are. The files are not. Official screen is 640×360 PNG. Nexus JPEG and PNG are 768×407. JPEG samples are not the PNG samples: top is `(254,0,0)` not `(255,0,0)`; top-left is `(255,0,254)` not `(255,0,255)` (`pixels.json`). `frac_magenta` is 0.0066 on the JPEG and 0.0071 on the official PNG. "Same markers" means a classifier fired, not equal pixels and not equal bytes. |
| The 120×48 overlay is what was captured. | CREDIBLE | 04 states that rectangle. On a 640×360 image it would be 5760/230400 = 0.025 of the pixels. `unity_screen` `frac_magenta` is 0.0071, about 1636 pixels, under a third of that rectangle. The top-left sample is magenta, and `unity_camera` top-left is red with `frac_magenta` 0. Some overlay signal is in `source=screen` and absent from `source=camera`. The fixture size is not what the table measures. |
| Overlay is now redundant with official capture. | CREDIBLE | The comparison is one Edit Mode Game view on Metal, and only against official `source=screen` (`unity_screen.json`). Official `source` is the string `screen`. Nexus `source` is `game_view` (`nexus_http_jpg.json`, `nexus_cli_jpg.stdout.txt`). `source=camera` still has no magenta. "NOW REDUNDANT versus `source=screen`" is a one-scene visual claim, not parity of the source token, the resolution, or the encoding. |
| Scene framing failed the same way on both sides. | CREDIBLE | Neither image has a non-zero green, red, blue, yellow, or magenta fraction. They are not the same picture. Official `unity_scene` is 640×360, `source` `sceneView`, top `(154,153,113)`, bottom `(75,57,30)`, center `(75,57,31)`. Nexus `nexus_scene` is 768×403, top `(75,57,30)`, bottom `(156,155,115)`, center `(75,57,31)`. The chrome colors are swapped vertically. The CLI transcript that names `CenterGreen` is `nexus_scene.stdout.txt`, and it failed before a capture. The image file matches `nexus_http_scene.json` (`source` `scene_view`, `file_path` under `Temp/NexusCapture`). That HTTP body has no target field. 04's UNKNOWN is the right label. "Not shown" does not mean "both captures missed in the same way," and it does not mean the CLI applied the target. |
| JPEG and external file delivery are still different. | WEAK as an attack | Official saves are PNG under `Assets/Captures` with `meta: true`. Nexus JPEG is 10074 bytes at 768×407 under `Temp/NexusCapture` with `meta: false`. 04 and 08 can say those two are still differentiated without claiming pixel equality. |
| `base64_ms` on a file delivery is a hidden double count. | WEAK | `nexus_http_scene.json` has `file_path` and `base64_ms` 12.40. 04 reports that and refuses the finding. |

## Not running discovery

07 is consistent with the folder: no arm scores, no token counts, no H1–H5 labels. `tags.stdout.txt` is a tag catalog (`animation/animator` count 6 and the rest) aimed at `127.0.0.1:7800` with `projectPath` `/private/tmp/nexus-p08-harness`. It is not an agent transcript. Inventing a result from it would not answer those hypotheses. 07 is right about that.

| Objection | Mark | Why |
| --- | --- | --- |
| Discovery could not be run fairly on port 7800 in this session. | WEAK as an attack on the abstention | 05 records the scratch server on `127.0.0.1:7800` while PID 4465 still listened on `*:7800`, then says the scratch editor was stopped and the harness `editor_status` came back for `UnityTestForNexus`. An agent arm on that port could talk to the wrong editor. Keeping the project-path-tagged capture and recompile files, and not running the arms, is the conservative reading of that collision. |
| "No … without another editor launch" means the experiment was impossible here. | CREDIBLE | 05's own rerun is the same scratch project, `m_Port` 7810, and `--project-path` on the beta.12 binary. That is a restart they did not do, not a missing editor. 07's "Not run" is the accurate result. "Cannot" is stronger than the file. |
| A frozen prereg is waiting to be executed. | CREDIBLE | 08 action 2 says to run the frozen discovery prereg. `06_DISCOVERY_PREREG.md` is not in this directory. 05 already conditions that file on whether a methodology reviewer wrote it. |
| The non-run is a failed cheap falsification, or a reason to pick an architecture. | WEAK | 07 does not record a failed arm. 08 marks S-06, S5, S6-P, Mixed, and Gate F WAIT because the arms did not run. Those WAIT cells match the empty result. |

## What 08 asserts beyond the files

08's WAIT rows for S-06, S-03, L-01, L-03, L-04, C-04, S-10, S-11, S5, S6-P, Mixed, and Gate F cite missing evidence. They are not an architecture. They should stay reads of an experiment that did not run.

| 08 call | Mark | Why it is stronger than the files |
| --- | --- | --- |
| P0-06 BUILD NOW | CREDIBLE | The CS0246 list and the clean dual-reference compile are real. "Enough," and a build decision, skip the 216-test assembly that was not filtered in, 02's still-open `/api/commands` sentence, and 01D's refusal to approve a commit. |
| S-02 test holes BUILD NOW | CREDIBLE | 01D closed FP-2, FP-3, FP-4, and MF-2, named the list-discard and default-lambda leftovers, and did not approve a commit. The 0.7 and 0.8 drift XML files are green. 01C's own rule was that a green run does not close a hole the assertion does not observe. Action 1 ("commit after 01D is read") is already behind 01D line 35. |
| S-05 TEST FIRST, "already returns file, line, column, code, and message, in order" | CREDIBLE on the premise | R2 and R3 show those fields for `CS0103` in one file. Exit 6 is three saved shapes, not a reload plus compiler errors. The CS1030 sentence has no saved log. Action 3 turns that into a fixture assignment. TEST FIRST is a gate, not a command design; the premise it sits on is the R2/R3 payload. |
| L-02 WAIT because R1 measured the reload drop again | CREDIBLE on the mechanism, not on the WAIT | R1 measured exit 6 and a network error, then a clean status object. It did not measure a domain reload. Waiting is still inside the evidence. The reason is not. |
| S-07 DROP as a deletion | WEAK on the deletion, CREDIBLE on the overlay sentence | Nothing in 04 or the image hashes says a capture path was removed. JPEG versus official PNG, and `Temp/NexusCapture` with `meta: false` versus `Assets/Captures` with `meta: true`, are in `pixels.json`. "Overlay is no longer Nexus-only against `source=screen`" is the one-scene claim above, and the scene cell hides the vertical disagreement and the failed CLI parse. |
| Ranked actions as the result of the sprint | CREDIBLE | Actions 1–3 are a commit, a discovery rerun of a file that is not here, and an S-05 fixture. The files support a narrower statement: these two editors compiled with the dual reference, four named test edits are closed, three exit-6 shapes were saved, official screen omit matches official screen bytes, Nexus does not match those bytes, and discovery has no scores. |

This review does not pick a replacement architecture. It does not say to implement S-05, a façade, or Gate F.
