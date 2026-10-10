# Unity 0.8 capture comparison

Scratch editor, GPU `Metal` / `Apple M5`, play mode was `stopped` at the status check before the captures. Marker scene: orthographic camera, red top quad, blue bottom quad, green center quad, yellow quad on the right, magenta Screen Space Overlay 120×48 at the top left. Pixel table: `evidence/contracts08/pixels.json`. Copies of the images: `evidence/contracts08/images/`.

## Official `capture_game_view` / `capture_scene_view`

| Call | Result |
| --- | --- |
| First omitted source, before a Game view target existed | HTTP 400: `No Game view render target to capture`. Exit 6. |
| Later omitted source, 640×360, save path set | Success. `source=screen`. 4476 bytes. Top pixel red, bottom blue, center green, right yellow, top-left magenta. |
| Explicit `--source screen` | Same pixels and same byte size as the later omit. |
| `--source camera --camera MarkerCamera` | 4201 bytes. Same scene markers. Top-left is red, magenta fraction 0. |
| `capture_scene_view` | 22019 bytes. Samples are scene-view chrome colors. Red, blue, green, yellow, and magenta fractions are 0. |

Omitted source is `screen` once a Game view render target exists, including in Edit Mode. The overlay marker is in the screen image and absent from the camera image. That is the Edit Mode overlay split this sprint measured. The previous sprint did not measure it.

`save_path` of `Captures/...png` was stored as `Assets/Captures/...png`, and Unity wrote a `.meta` beside each file.

## Nexus `nexus_capture_game_view`

`nexus_look` is not a CLI command name. The catalog name is `nexus_capture_game_view`. The global CLI `--format` flag collides with the command's `format` argument; a `--` before the command arguments avoids that.

| Call | Image |
| --- | --- |
| Game view, jpg, file, max edge 768 | 768×407, 10074 bytes, `Temp/NexusCapture/...jpg`, no `.meta`. Top red, bottom blue, center green, right yellow, top-left magenta. |
| Game view, png, file | 768×407, 7119 bytes, no `.meta`. Same markers, including magenta. |
| Scene view, png, target `CenterGreen` | 768×403, 33818 bytes, `source=scene_view`, no `.meta`. Marker fractions are 0. Center pixel is about `(75,57,31)`, not green. |

Warm file jpg timings from the HTTP call: `encode_ms` 1.48, `base64_ms` 7.09, `total_ms` 13.79, envelope `meta.ms` 7. File delivery still filled `base64_ms`. This sprint does not treat that as a new double-count finding.

## S-07 classification from these images

| Piece | Class | Why |
| --- | --- | --- |
| JPEG | STILL DIFFERENTIATED | Official captures in this run are PNG. Nexus jpg is 10074 bytes at 768×407. |
| File outside Assets, no `.meta` | STILL DIFFERENTIATED | Official `save_path` wrote `Assets/Captures` plus `.meta`. Nexus wrote `Temp/NexusCapture` and no `.meta`. |
| Edit Mode overlay | UNKNOWN as parity | One Edit Mode scene. Official `source=screen` top-left sample is magenta and `frac_magenta` is 0.0071. Official `source=camera` top-left is red and `frac_magenta` is 0. A 120×48 rectangle on 640×360 would be about 0.025 of the pixels, so the fraction is smaller than the fixture. Nexus game images are 768×407, not the same bytes, and JPEG samples are off by one (`254,0,0` and `255,0,254`). `source` strings differ: official `screen`, Nexus `game_view`. Some magenta is in the screen image and absent from the camera image. That is not encoding, resolution, or source-token parity. |
| Scene target framing | UNKNOWN | Neither official `capture_scene_view` nor the Nexus HTTP scene file has a non-zero marker fraction. They are not the same picture: official top is about `(154,153,113)` and bottom about `(75,57,30)`; Nexus swaps those chrome colors. The CLI transcript that names `CenterGreen` failed before capture (`target` bound to `--quality`). The HTTP body that matches the image has no target field. |
| Orientation | Matched on the game images | Top is red and bottom is blue for both official screen and Nexus game. |

No production capture path was removed.
