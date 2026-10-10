# Nexus performance on the current tip

MEASURED 2026-10-04. Commit `bfdd0f7`. Branch `rework2/S-02`. Unity `6000.3.16f1` PID 4465. Pipeline `0.7.0-exp.1`. CLI installed `1.0.0-beta.10` (not used for these calls). Transport: persistent `POST /api/exec` on `127.0.0.1:7800`, verbose true. Focus was not recorded. The Editor had been up for about 7 hours. Play mode was stopped.

No product code was changed to improve these numbers.

Raw rows: `evidence/perf/runs.jsonl`, `evidence/perf/summary.json`, samples `evidence/perf/sample_*.json`.

## What the clock is actually measuring

Handler time and client time are different.

| Command | n | HTTP success | Envelope | Client p50 | Client min–max | Wire p50 | Handler time inside the result |
|---|---:|---|---|---:|---|---:|---|
| `editor_status` | 15 | 15 | n/a (Unity command) | 100.3 ms | 93.4–102.6 | 398 B | not in this payload |
| `nexus_project_map` | 8 | 8 | `ok: true` | 99.3 ms | 97.0–102.1 | 1417 B | `data.execution_duration_ms` 0.26; `meta.ms` 0 |
| `nexus_group_compile_errors` | 8 | 8 | `ok: true`, 0 errors, not compiling | 100.2 ms | 95.3–101.9 | ~392 B | `data.execution_duration_ms` 0.29; `meta.ms` 0 |
| `nexus_capture_game_view` with no Game View | 6 + 3 file + 12 file | all HTTP 200 | `ok: false`, `unavailable`, retryable true, hint to open a Game View | 98–100 ms | 91.6–102.1 | 429 B | no image |

`meta.ms` is 0 because the canonical work is sub-millisecond and the field is an integer. The agent still waits about 100 ms. That floor showed up on every cheap call in this session. One Unity `capture_game_view` sample returned in 49.4 ms, so 100 ms is not a hard floor of the machine. Do not quote 100 ms as Nexus handler cost.

`get_performance_stats` at the same time reported `cpuFrameTimeMs` about 100 and `cpuMainThreadFrameTimeMs` under 0.001. Those two frame fields disagree. They are recorded in `memory_before.json` / `memory_after.json` and are not used as a latency claim.

Approximate text tokens are wire bytes / 4. Project map is about 350 tokens. Empty compile groups are about 100 tokens. A failed look is about 100 tokens and names the next action.

## Capture once a Game View exists

`menu` `Window/General/Game` was executed. The next file-delivery look succeeded.

| Call | Client | Wire | Image |
|---|---:|---:|---|
| First file look after opening Game View | 346.7 ms | 727 B | JPEG on disk, path under `Temp/NexusCapture/` |
| File look after the bench cube existed | 428.1 ms | 716 B | 768×407 JPEG, 12387 bytes |
| File PNG | 377.4 ms | 714 B | 768×407 PNG, 82679 bytes |
| Scene view, file JPEG, target `nexus-bench-cube` | 402.9 ms | 719 B | 768×403 JPEG, 12507 bytes |

File mode is the cheap tool result: about 180 tokens of JSON and no pixels. The pixels are a file the model does not see unless something reads it. Inlined base64 of that 12387-byte JPEG would be 16516 characters (about 4100 tokens) before the envelope. The plan's area proxy `768*407/750` is about 417, which is the proxy, not the base64 cost.

Unity `capture_game_view` `source=camera`, 768×432, inline PNG: 3 calls, client 138.2 / 116.1 / 49.4 ms, wire 100798 bytes, image 75384 bytes. That is about 25k tokens if the client keeps the base64. Same Edit Mode camera view as the Nexus JPEG (pixel check is in `08_CAPTURE_DEATHMATCH.md`). Unity was faster and much larger.

## Warm capture after the Game View stayed open

`evidence/perf/look_success.json`. Same 768×407 view, image bytes identical on every call (12387).

| Series | n | Client times | Wire |
|---|---:|---|---|
| File delivery | 12 | first 232.2 ms, then 8.0–14.4 ms (median of the last 11 ≈ 9.3 ms) | 690–752 B |
| Base64 delivery | 4 | 8.3, 8.7, 8.4, 10.9 ms | 17099–17141 B, base64 chars 16516 |

Inside the base64 payload, `encode_ms` was 1.39–1.53 and `base64_ms` was 6.62–9.22, with `total_ms` 13.2–18.4. `base64_ms` is several times `encode_ms`. The previous sprint's code note says that timer starts before the gateway await. This sample matches "larger than the encode" and does not by itself re-prove the double-count. Not re-audited in source this session.

File mode is about 700 bytes on the wire (about 175 tokens at 4 chars/token) and does not include pixels. Base64 mode is about 17 KB (about 4300 tokens). The area proxy `768*407/750` ≈ 417 is not the base64 cost.

The 350–430 ms figures in the deathmatch were the first captures after `eval` and opening the Game View. Steady repeats on an idle Editor were about 10 ms. Quote the steady number for warm cost and the first-call number for cold cost.

## Memory

Closed-Game-View series (no encode) plus three Unity PNGs: allocated 517408660 → 518685121 (+1.3 MB), mono used +5.1 MB. Not a capture leak test.

Open-Game-View series, 12 file looks + 4 base64 looks, no other intentional scene edits in that script: allocated 563240048 → 586498838 (+23.3 MB), reserved +29.8 MB, mono used +5.6 MB, mono heap unchanged. About 1.5 MB allocated per capture if the whole delta is blamed on capture, which it should not be: there is no idle control interval. Sixteen shots did not run away. This does not clear the temporary-RT question and does not prove a leak.

## Not measured

- Unfocused versus focused as a controlled pair.
- Immediately after a domain reload.
- p95 with a large N. These p90 figures are from 8–15 calls.
- Token counts from a model tokenizer. Figures above are bytes/4 or explicit base64 length.
- Task-level agent economics (discovery, retries, semantic success). That is the discovery experiment, which is not run.
