# Capture comparison — installed Pipeline 0.7 vs Nexus S-07

MEASURED 2026-10-04 on commit `bfdd0f7`, Unity `6000.3.16f1`, Pipeline `0.7.0-exp.1`, Metal, Linear, URP. Edit Mode only. Play Mode was not entered: the official 0.7 screen source requires Play Mode, and entering Play on a possibly-dirty scene can open a save dialog that wedges this Editor. The bench objects were created with official `eval` and destroyed in a `finally`. `editor_status` was `ready` afterward. Scene dirty flag stayed false (`gone=True dirty=False`). The four PNGs written under `Assets/Temp/nexus-dm-*` were deleted. Pre-existing `Assets/Temp/scene_view_test.png` was left alone.

Images: `evidence/deathmatch/images/`. Pixel samples: `evidence/deathmatch/report.json`. Machine samples are sips BMP reads of the center column at 5%, 20%, 50%, 80%, and 95% of height. RGB.

This is the **installed** capture command, not Pipeline 0.8. 0.8's release note says `capture_game_view` defaults to `source=screen` and works in Edit Mode. That was not installed and not measured. See `00_UPSTREAM_PRIMARY_SOURCES.md`.

## Fixture

Official `eval` created `nexus-bench-root`, a green unlit cube `nexus-bench-cube` (instance id −32136), and a Screen Space Overlay canvas with a red top band (anchors y 0.85–1) and a blue bottom band (anchors y 0–0.15). Shader fallback was Unlit/Color, URP Unlit, or Sprites/Default.

## Results

| Arm | HTTP | Client | What came back | Pixels |
|---|---|---:|---|---|
| Nexus look, Game View closed | 200 | ~99 ms | Envelope `unavailable`, retryable, no image | n/a |
| Nexus look, file, JPEG default, Game View open | 200 | 428 ms | 768×407 JPEG, 12387 bytes, wire 716 B | sky [95,119,155], center [241,255,255], bottom [104,97,91] |
| Nexus look, file, PNG | 200 | 377 ms | 768×407 PNG, 82679 bytes, wire 714 B | same view, within 1 level |
| Unity `capture_game_view` `source=camera` 768×432 | 200 | 1042 ms this call (earlier warm calls 49–138 ms) | PNG 75456 bytes, inline base64, wire 100798 B, source `camera:Main Camera` | top [96,120,156], center [240,255,255], bottom [104,97,92] |
| Unity `source=screen` in Edit Mode | 400 | 86 ms | Error text: screen source requires Play Mode | no image |
| Nexus look, `view=scene`, `target=nexus-bench-cube`, file JPEG | 200 | 403 ms | 768×403 JPEG, 12507 bytes | center and upper/lower are green [27,191,52]; top [102,97,93]; bottom [182,196,197] |
| Unity `capture_scene_view` 768×432 | 200 | 191 ms | PNG 41640 bytes | no green; top [182,195,197]; center [106,99,96]; bottom [102,96,90] |

## Where the images agree

Edit Mode Game View, Nexus JPEG versus Unity camera PNG: the same sky, the same bright center, the same grey bottom, within about 2 levels. Nexus is not rotated relative to Unity's camera encode on this shot. JPEG at the 768 default is 12.4 KB. The matching Unity PNG is 75.5 KB and, when inlined, about 100 KB on the wire.

Nexus file mode does not inline pixels. Unity's save_path result in this call was also path-only (wire ~300 B) and the file landed under `Assets/Temp/`, which the Asset Database imported (a `.meta` appeared). Nexus writes under `Temp/NexusCapture/`, outside `Assets/`.

## Where they do not agree

**Game View required.** Nexus refuses with a typed envelope when no Game View is open. Unity `source=camera` rendered `Main Camera` anyway. That is a real behaviour difference on 0.7, not a Nexus failure of the envelope.

**Edit Mode overlay.** The red and blue UI bands are absent from both the Nexus Game View JPEG and the Unity camera PNG. This run does **not** show Nexus capturing Overlay, and it does **not** show Unity capturing it either. 0.7 `source=screen` is Play Mode only (MEASURED error string). Whether a freshly created Overlay canvas appears in `GameView.m_RenderTexture` after a repaint was not given a second, delayed try. Historical Nexus notes say Overlay lives in that RT. This shot does not confirm it.

**Scene framing.** Nexus scene look contains the green cube through the middle of the frame. Unity's scene capture does not contain that green. Unity does not take a target. Nexus framed the object. That part of S-07 is visible in the pixels.

**Scene orientation versus Unity.** Unity's unframed scene top band matches Nexus's framed scene bottom band (about [182,196,197]), and Unity's bottom matches Nexus's top (about [102,97,93]). The camera pose was not the same, because Nexus called Frame/Aim and Unity did not. The match is tight enough to suspect a vertical flip and too confounded to call it a bug. Game View did **not** show that swap against Unity's camera encode.

**Size and tokens.** Nexus default look is a 768 JPEG. Unity default in this package is a 1280×720 PNG (the call here forced 768×432). No JPEG parameter exists on the 0.7 command (CONFIRMED in `CaptureCommands.cs`).

## 0.8, not measured

DOCUMENTED, Unity Discussions 2026-09-28: `capture_game_view` defaults to `source=screen` and works in Edit Mode. If that ships as described, Unity gains Edit Mode overlay capture and Nexus's largest capture difference shrinks to JPEG size, crop, target framing, file location outside `Assets/`, and the envelope. Those still need a 0.8 measurement. This Editor was not upgraded.

## What Nexus should not delete yet

Do not replace S-07 with `capture_game_view` on the strength of this 0.7 session. Unity's Edit Mode camera shot matches Nexus full-frame orientation and is faster, and it is a large PNG. It does not frame a named object, it does not crop, it does not emit JPEG, and its screen source does not run in Edit Mode. 0.8 might close the Edit Mode overlay gap. Crop and scene framing are still Nexus-only on the version this project runs.

## Not done

- Play Mode game view.
- A delayed Overlay repaint.
- Same-pose scene flip test.
- Unusual aspect as its own fixture (the Game View happened to be 768×407 after the 768 cap).
- Pixel check of a UI crop.
- Domain reload during an in-flight readback.
- Repeated successful-capture memory curve (separate file `evidence/perf/look_success.json` if the follow-up finished).
