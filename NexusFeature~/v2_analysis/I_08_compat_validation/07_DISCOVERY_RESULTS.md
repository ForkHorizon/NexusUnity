# Discovery experiment results

Not run. No arm scores, token counts, or hypothesis labels.

The frozen protocol in `06_DISCOVERY_PREREG.md` says to abort before run 1 when fewer than 5 tasks survive their self-checks. That happened on the port-7810 editor. H1–H5 stay unscored.

## Environment that did come up

Scratch editor Unity `6000.3.16f1`, project `/tmp/nexus-p08-harness`, Pipeline `0.8.0-exp.1`. `EditorPipelineConfig.json` `m_Port` was 7810 before the process existed. The live port file and `editor_status` both reported port 7810 and project path `/private/tmp/nexus-p08-harness`. The harness PID 4465 stayed on `*:7800`. CLI binary `/tmp/unity-cli-beta12/unity` `1.0.0-beta.12`. `unity command --tags` returned 50 tags. Every name on the C1 allowlist was present, so none was deleted.

The first scratch process on this port blocked its main thread on `open_scene` (30s timeout, then an 8s main-thread timeout). It was stopped without saving. The relaunch loaded `Yard.unity` from disk. `YARD_OPEN_READY` is in `/tmp/nexus-p08-logs/editor-7810b.log`.

## Self-checks before any agent

| Task | Result |
| --- | --- |
| T-VIS | Pass. Omitted `capture_game_view` returned `source=screen`, 320×180. Top-left pixel `(38, 217, 217)` (ShadeBand over black). Top pixel `(255, 0, 0)`. `source=camera` `YardCamera` top-left and top were `(255, 0, 0)`. |
| T-BULK | Pass. Scene file has 1006 transforms. Live `Light.m_Intensity` on `/PierLamp` is 2.75. |
| T-SCENE | Drop. Disk YAML for `noticeTemplate` is guid `c0ffee00c0ffee00c0ffee00c0ffee00`. `get_serialized_fields` and `get_component_properties` both return `noticeTemplate: null`. `headerIcon` resolves to `Assets/DiscFixture/Art/header.png`. The official read does not show an empty-path guid. |
| T-RARE | Drop. First selectable command `get_import_settings` returns an object. `read_text_file` returns an object whose `contents` is the whole script. `get_authoring_root` returns `{"root": "Assets"}`. Three skips, no scalar or one-line string. |
| T-OUT | Drop. Its rule starts at the command after T-RARE's choice. T-RARE has no choice. |
| T-COMP, T-AMB | Not planted. They could not raise the surviving set to 5. |
| T-SPARE | Not used. T-VIS passed, and the spare replaces only T-VIS. |

Surviving tasks: T-VIS and T-BULK. That is below 5. No model addendum, no fixture-key hash, no agent process.

The capture PNGs used for the T-VIS check are scratch files under `Assets/Captures/`. They are not agent results.
