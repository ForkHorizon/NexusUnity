# Unity MCP benchmark results

Recorded 2026-09-01 in `/Users/daliys/Daliys/UnityProjects/NexusMetricsTest`.

## Environment and method

| Item | Value |
|---|---|
| Host | Mac17,2, Apple M5, macOS 27.0, Europe/Madrid |
| Unity | 6000.4.3f1 (`39d1a88d4dd1`) |
| Repetitions | N=10 per measured scenario; first sample is reported as cold, remaining samples warm |
| Measurement | Client-side elapsed milliseconds around each JSON-RPC call or scenario sequence; p95 uses the nearest-rank sample used by the harness |
| Protocol | `/Users/daliys/Daliys/NexusFeature/BENCHMARK_PROTOCOL.md` |
| Scope | Direct MCP JSON-RPC only; no LLM/subjective quality score |

Package/source changes were limited to switching the Unity package under test and the required registry entry. Benchmark harnesses and raw results are ignored by the project `.gitignore`. No benchmark-created `Assets/*Bench*` files remain.

## Setup and operational experience

These observations are part of the benchmark outcome. They cover the actual setup/restart work needed to obtain the measurements; they are not inferred from latency alone.

### Nexus raw MCP and Nexus bridge

Setup was comparatively direct for the raw Unity listener: Unity exposed the MCP server on port 8081 and the raw harness connected to it. The bridge path was a separate local integration surface and required its own configuration/connection step. Package changes required quitting Unity before replacing the package, then reopening the same project.

The main operational problem was reload fragility. C7 caused Unity compilation/domain reload activity to interrupt the raw listener: the raw run stopped after four samples, with one failed response. The bridge survived the sequence but showed an approximately 8.5-second compile/reload cost. C12 also lost the expected UI target and required cleanup back to edit mode. Game View screenshot capture failed in both the raw and bridge observations (the bridge returned `PartialSuccess`, not a valid image).

Pros:

- Small core surface (14 tools) and low schema overhead.
- Raw JSON-RPC calls are simple once the Unity listener is running.
- Strong direct support for the measured scene/object operations.

Cons:

- Unity domain reload can interrupt the listener and invalidate an otherwise healthy run.
- Screenshot/UI workflows were not reliable in this project.
- The bridge and raw paths produced materially different timings/payloads, so they need separate validation rather than being treated as one transport.

### FunplayAI

The Unity UI exposed a ready-to-use HTTP core server on port 27015 (v0.6.4). The practical setup was straightforward after reopening Unity and configuring the client against that port. Play Mode introduced a known short HTTP interruption during domain reload; the harness had to poll `get_reload_recovery_status` before continuing. Without that recovery step, C12 would have been recorded as a server outage rather than a completed interaction sequence.

Pros:

- The clearest setup path in this run: HTTP core server, visible port, and working connection.
- Fast basic editor/scene operations after warm-up.
- Explicit reload-recovery support made Play Mode automation usable.
- Full C12 sequence succeeded, including input, screenshot, logs, and exit.

Cons:

- The core exposure omitted batch execution and test-running tools.
- C7 is only an in-memory compile equivalent; it does not exercise the protocol's disk-script lifecycle.
- Compile/evaluate operations were several seconds slower than basic editor calls.

### CoplayDev / MCPForUnity

The package started its local HTTP server on the default port 8080, but that port was already occupied by an unrelated local Open WebUI process. Unity therefore had to be configured to use port 8090, and the local-server confirmation had to be accepted in the Unity MCP window. The plugin then launched its official `mcp-for-unity` server through `uvx`; the client had to retain the returned MCP session id.

The HTTP transport uses SSE responses and can emit multiple events for one request. The benchmark client therefore had to select the event matching the JSON-RPC request id. This was an integration detail, not a product success/failure result. C7 also left a temporary script asset during an interrupted probe; it was removed through the MCP cleanup path and the project was rechecked. C12 exposed a camera screenshot/log path but no mouse-input operation, so it remains partial.

Pros:

- Broadest directly useful surface in this comparison: 48 tools, batch execution, scripts, tests, camera capture, and console access.
- Disk-script lifecycle and test-job polling were available and executable.
- Game View/camera screenshot path worked and returned image data.
- After the port change, the HTTP server stayed usable for the full benchmark.

Cons:

- Most setup work: port collision, Unity UI confirmation, server/session handling, and SSE-specific client logic.
- Very large tool schema (about 29k approximate tokens), increasing discovery/context cost.
- Scene reads and object operations were slower than Funplay and Ivan in this project.
- C12 lacked the required mouse-input primitive, despite the other Play Mode steps being available.

### IvanMurzak / Unity-MCP

Switching to Ivan required Unity to be closed before changing the manifest. The first package resolution failed because the `extensions.unity` dependency could not be found until the `package.openupm.com` scoped registry was added. After reopening Unity, the package downloaded its Apple Silicon server binary into `Library/mcp-server/osx-arm64/`.

The package initially opened in Cloud/authenticated configuration and logged an authorization rejection. The Unity UI had to be changed to Custom/local configuration. Selecting stdio stopped the in-Editor HTTP server; the visible Start button did not itself launch a usable stdio process because stdio is intended to be spawned by the MCP client. The benchmark consequently launched the official binary directly with the project port 29185. Its wire format is newline-delimited JSON-RPC, with notifications interspersed between responses, so the harness had to match response ids and ignore notifications. The first live schema probe also had to be corrected to use Ivan's hyphenated tool names.

Pros:

- Very fast supported scene/object operations after the server process is connected.
- Official stdio binary is deterministic and easy to launch once the package is resolved.
- Exact create/destroy cleanup and rich path-scoped read schemas are available.
- No persistent benchmark asset remained after cleanup.

Cons:

- Highest setup friction in this run: package registry repair, Unity restart, Cloud-to-Custom reconfiguration, first-run binary download, and client-launched stdio semantics.
- Largest schema payload (about 36k approximate tokens).
- The live 38-tool exposure lacked editor state, substring search, disk-script lifecycle, Game View screenshot, batch execution, and Play Mode/input controls required by several protocol cases.
- `tests-run` returned a truthful “No tests found” failure for this project; the tool did not provide a passing benchmark result.

## Results

`ok%` means the direct call/sequence returned successfully. Where a tool could not implement the protocol case, it is listed as unsupported or partial instead of being counted as a success.

### Nexus raw MCP

| Scenario | Tool/method | Cold ms | p50 ms | p95 ms | ok% | Payload p50 B |
|---|---|---:|---:|---:|---:|---:|
| C1 | initialize + tools/list | 262.598 | 299.221 | 310.303 | 100 | 27,565 |
| C2 | editor state | 1.371 | 0.647 | 1.303 | 100 | 625 |
| C3 | find/read Main Camera | 190.678 | 100.299 | 200.515 | 100 | 718 |
| C4 | scene hierarchy/data | 205.761 | 194.386 | 205.761 | 100 | 1,529 |
| C5 | search Camera | 201.004 | 199.029 | 201.004 | 100 | 704 |
| C6 | create + destroy Cube | 309.156 | 299.639 | 307.521 | 100 | 704 |
| C7 | disk script compile/reload | 24.538 | 98.914 | 98.914 | 75 | 240 |
| C8 | Game View screenshot | 147.954 | 197.562 | 212.581 | 0 | 1,016 |
| C9 | batch execute | 34.894 | 189.983 | 209.915 | 100 | 16,666 |
| C10 | run tests | timeout | — | — | 0 | — |
| C12 | play + input + screenshot + logs | 16,458.643 | — | — | 0 | — |

C7 was interrupted by Unity domain reload/listener loss after four samples (3/4 successful). C8 failed to capture the Game View image. C10 timed out waiting for the test result. C12 failed during the play-mode/UI sequence and was cleaned back to edit mode.

### FunplayAI

Package UI reported v0.6.4, HTTP core on port 27015.

| Scenario | Tool/method | Cold ms | p50 ms | p95 ms | ok% | Payload p50 B |
|---|---|---:|---:|---:|---:|---:|
| C1 | initialize + tools/list | 249.559 | 249.468 | 250.923 | 100 | 29,777 |
| C2 | get_editor_state | 269.217 | 124.688 | 151.780 | 100 | 388 |
| C3 | get_hierarchy(Main Camera) | 162.098 | 125.272 | 150.164 | 100 | 193 |
| C4 | get_hierarchy(depth=3) | 125.125 | 125.255 | 149.658 | 100 | 297 |
| C5 | find_game_objects(Camera) | 138.000 | 125.261 | 149.825 | 100 | 139 |
| C6 | execute_code create/destroy | 4,933.776 | 4,739.294 | 4,904.555 | 100 | 205 |
| C7* | execute_code in-memory compile | 5,120.749 | 4,624.193 | 4,894.991 | 100 | 193 |
| C8 | capture_game_view | 430.306 | 498.948 | 501.673 | 100 | 36,932 |
| C12 | play + click + screenshot + logs + exit | 1,476.445 | 1,409.161 | 1,431.570 | 100 | 39,389 |

Funplay core exposed no C9 batch operation and no C10 test-run operation. `*` C7 is the documented closest equivalent but writes no disk `.cs` file and therefore is not a strict disk-script result.

### CoplayDev / MCPForUnity

Package/server reported v10.1.2. Port 8080 was occupied by an unrelated local Open WebUI process, so the Unity MCP server was configured on 8090. The Coplay HTTP transport used SSE responses; the harness was corrected to select the matching JSON-RPC request id when multiple events were returned.

| Scenario | Tool/method | Cold ms | p50 ms | p95 ms | ok% | Payload p50 B |
|---|---|---:|---:|---:|---:|---:|
| C1 | initialize + tools/list | 7.309 | 5.841 | 7.309 | 100 | 109,148 |
| C2 | get_editor_state | 262.363 | 300.649 | 399.931 | 100 | 470 |
| C3 | find_gameobjects + resources/read | 699.545 | 799.473 | 899.862 | 100 | 1,313 |
| C4 | manage_scene hierarchy | 701.288 | 501.139 | 699.590 | 100 | 3,955 |
| C5 | find_gameobjects(Camera) | 599.052 | 599.313 | 700.502 | 100 | 449 |
| C6 | manage_gameobject create/destroy | 1,299.889 | 1,299.053 | 1,399.590 | 100 | 2,110 |
| C7 | create_script + refresh + delete | 13,206.394 | 13,402.699 | 16,399.774 | 100 | 2,215 |
| C8 | manage_camera screenshot | 387.384 | 124.003 | 200.024 | 100 | 73,797 |
| C9 | batch_execute | 491.183 | 140.979 | 200.719 | 100 | 4,913 |
| C10 | run_tests + get_test_job | 16,039.948 | 20,120.012 | 20,258.703 | 100 | 2,465 |
| C12* | play + screenshot/logs + exit | 3,704.371 | 3,605.502 | 3,611.510 | 100 | 1,518 |

`*` C12 is partial: play mode, camera screenshot/log collection, and exit were exercised, but the live Coplay surface did not provide the protocol’s mouse-input operation. It is retained as a partial sequence, not a full interaction success.

### IvanMurzak / Unity-MCP

Unity package v0.90.0; stdio server `gamedev-mcp-server` v9.2.5.0. The official Apple Silicon binary was launched with `--port=29185 --plugin-timeout=10000 --client-transport=stdio`. The protocol is newline-delimited JSON-RPC; notifications interspersed with responses were ignored.

| Scenario | Tool/method | Cold ms | p50 ms | p95 ms | ok% | Payload p50 B |
|---|---|---:|---:|---:|---:|---:|
| C1 | initialize + tools/list | 2,415.693 | 16.875 | 24.814 | 100 | 146,331 |
| C2 | editor state | unsupported | — | — | — | — |
| C3 | gameobject-find(Main Camera), full read | 20.339 | 2.293 | 4.244 | 100 | 3,732 |
| C4 | scene-get-data, full hierarchy/data | 2.935 | 2.224 | 2.935 | 100 | 8,764 |
| C5 | gameobject-find(Camera) | 3.527 | 2.300 | 3.004 | 0 | 227 |
| C6 | gameobject-create + gameobject-destroy | 11.373 | 16.430 | 17.024 | 100 | 706 |
| C7 | disk script compile/reload | unsupported | — | — | — | — |
| C8 | Game View screenshot | unsupported | — | — | — | — |
| C9 | batch execute | unsupported | — | — | — | — |
| C10 | tests-run(EditMode) | 321.872 | 84.205 | 116.848 | 0 | 238 |
| C12 | play + input + screenshot + logs | unsupported | — | — | — | — |

Ivan’s live `tools/list` contains 38 tools but does not expose editor-state, disk script lifecycle, Game View screenshot, batch, or Play Mode/input tools. C5 is intentionally an exact-name probe; `Camera` is not an object name in this scene, and the API has no substring-search parameter. C10 returned the truthful failure “No tests found.”

## Tool-list size

| Tool | Live tool count | JSON schema chars | Approx. tokens |
|---|---:|---:|---:|
| Nexus raw | 14 | 19,655 | 4,913 |
| Funplay core | 34 | 28,271 | 7,067 |
| Coplay | 48 | 115,822 | 28,955 |
| IvanMurzak | 38 | 144,143 | 36,035 |

Nexus exposes fewer tools than Funplay core (14 vs 34), satisfying that protocol rule. Coplay and Ivan expose broader surfaces, with correspondingly larger schemas.

## Nexus bridge comparison

The Nexus bridge is included as a transport/harness observation, not as a second product result. Negative deltas show that the bridge and raw paths did not exercise identical server-side code paths and must not be interpreted as actual negative overhead.

| Scenario | Raw p50 ms | Bridge p50 ms | Bridge - raw ms | Bridge observation |
|---|---:|---:|---:|---|
| C2 | 0.647 | 0.357 | -0.290 | state path differs |
| C3 | 100.299 | 2.662 | -97.637 | bridge returned a smaller payload/path |
| C5 | 199.029 | 2.875 | -196.154 | bridge search path differs |
| C6 | 299.639 | 25.016 | -274.623 | bridge route differs materially |
| C7 | 98.914 | 8,511.751 | +8,412.837 | compile/domain-reload tax visible |
| C8 | 197.562 | 56.284 | -141.278 | bridge returned partial screenshot failure |
| C10 | timeout | 20,489.167 | — | raw timed out; bridge returned a result |
| C12 | failed | 0.929 | — | bridge sequence failed at UI click |

## Findings and limitations

1. For direct scene reads, Coplay crossed the protocol’s 500 ms warning line in C3/C4, while Funplay and Nexus raw remained below it. Ivan was fastest on the supported C3/C4 paths, but its schemas were the largest.
2. Compile/reload behavior dominates C7: Coplay disk-script C7 was about 13.4 s p50, Nexus bridge C7 about 8.5 s, and Funplay’s in-memory equivalent about 4.6 s. Nexus raw C7 was interrupted by reload/listener loss and has only four samples.
3. C12 was fully successful only for Funplay. Coplay’s result is partial because mouse input was unavailable in the measured surface. Nexus failed the UI/screenshot sequence. Ivan lacks the required control surface.
4. No subjective LLM usability score was collected; results are protocol-level latency, payload, success, and capability measurements.
5. The harnesses explicitly record failed calls, timeouts, partial responses, unsupported tools, and protocol deviations. No unsupported capability was converted into a pass.

## Raw artifacts and harnesses

- [environment.json](/Users/daliys/Daliys/UnityProjects/NexusMetricsTest/results/environment.json)
- [bench_nexus_raw_precompile.json](/Users/daliys/Daliys/UnityProjects/NexusMetricsTest/results/bench_nexus_raw_precompile.json)
- [bench_nexus_raw_c7.json](/Users/daliys/Daliys/UnityProjects/NexusMetricsTest/results/bench_nexus_raw_c7.json)
- [bench_nexus_raw_postcompile.json](/Users/daliys/Daliys/UnityProjects/NexusMetricsTest/results/bench_nexus_raw_postcompile.json)
- [bench_nexus_bridge_fast.json](/Users/daliys/Daliys/UnityProjects/NexusMetricsTest/results/bench_nexus_bridge_fast.json)
- [bench_nexus_bridge_c7.json](/Users/daliys/Daliys/UnityProjects/NexusMetricsTest/results/bench_nexus_bridge_c7.json)
- [bench_nexus_bridge_c8.json](/Users/daliys/Daliys/UnityProjects/NexusMetricsTest/results/bench_nexus_bridge_c8.json)
- [bench_nexus_bridge_c10.json](/Users/daliys/Daliys/UnityProjects/NexusMetricsTest/results/bench_nexus_bridge_c10.json)
- [bench_nexus_bridge_c12.json](/Users/daliys/Daliys/UnityProjects/NexusMetricsTest/results/bench_nexus_bridge_c12.json)
- [bench_funplay_c1.json](/Users/daliys/Daliys/UnityProjects/NexusMetricsTest/results/bench_funplay_c1.json)
- [bench_funplay_fast.json](/Users/daliys/Daliys/UnityProjects/NexusMetricsTest/results/bench_funplay_fast.json)
- [bench_funplay_code.json](/Users/daliys/Daliys/UnityProjects/NexusMetricsTest/results/bench_funplay_code.json)
- [bench_funplay_c8.json](/Users/daliys/Daliys/UnityProjects/NexusMetricsTest/results/bench_funplay_c8.json)
- [bench_funplay_c12_final.json](/Users/daliys/Daliys/UnityProjects/NexusMetricsTest/results/bench_funplay_c12_final.json)
- [bench_coplay.json](/Users/daliys/Daliys/UnityProjects/NexusMetricsTest/results/bench_coplay.json)
- [bench_ivan.json](/Users/daliys/Daliys/UnityProjects/NexusMetricsTest/results/bench_ivan.json)
- [ivan_tools_list.json](/Users/daliys/Daliys/UnityProjects/NexusMetricsTest/results/ivan_tools_list.json)
- [bench_ivan.py](/Users/daliys/Daliys/UnityProjects/NexusMetricsTest/bench/bench_ivan.py)
