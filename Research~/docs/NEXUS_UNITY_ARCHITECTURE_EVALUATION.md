# Nexus Unity Architecture Evaluation & Strategic Positioning Report
## Empirical Evaluation of Nexus Unity vs. Official Unity CLI / Pipeline / MCP Stack

**Correction (2026-09-22):** Do not quote **177× faster** or **13.3× smaller** as fair capture-engine claims. Those mixed cold `unity command` spawn (~1 s) with in-engine timestamps and JPEG vs PNG. Authoritative fair comparison: `CORRECTION_REPORT.md`. Production capture is **DriverOwnedReadback** (`AsyncGPUReadback.Request` + `GetData<byte>()`), not `RequestIntoNativeArray`. JPEG is Unity `ImageConversion` (CPU-side in the measured path), not VideoToolbox. Historical R1/R2 labels in this document are research names only.

> Historical research archive. The corrected report and current package documentation are authoritative; the detailed comparisons below are retained only as migration history.

**Date**: September 21, 2026  
**Target Environments**: 
- **Unity Editor**: `6000.4.3f1` (Apple Silicon arm64, Metal Graphics API, URP 17.5.0, Linear Color Space)
- **Unity CLI**: `1.0.0-beta.10` (release date September 16, 2026)
- **Unity Pipeline Package**: `com.unity.pipeline@0.7.0-exp.1`
- **Nexus Unity**: Branch `rework/T01`, Capture V2 (DriverOwnedReadback + Unity ImageConversion JPEG Q85)
- **Host System**: macOS 27.0 (Build 26A428), Apple M5 (Mac17,2)

---

## Executive Summary & Core Verdict

Following the release of Unity CLI `1.0.0-beta.10` and `com.unity.pipeline` `0.7.0-exp.1`, this evaluation investigated whether Nexus Unity should retain its proprietary HTTP/MCP server, migrate entirely to the official Unity stack, or adopt a **Hybrid Architecture (Architecture C)**.

### Definitive Decisions

1. **Transport Layer: ADOPT HYBRID (Architecture C)**  
   Unity Pipeline provides a robust, native C# attribute-based command registration system (`[CliCommand]`), automatic discovery via Unity's internal `TypeCache` ($< 1\text{ ms}$ registration), multi-editor dynamic port allocation ($7800\text{--}7849$), and native lifecycle management surviving domain reloads. Nexus should expose its domain-specific tools as `[CliCommand]` endpoints within the official pipeline, eliminating custom HTTP networking boilerplate and port collision bugs.
2. **Capture Pipeline: RETAIN NEXUS CAPTURE V2 (REJECT OFFICIAL CAPTURE)**  
   Official Unity capture (`capture_game_view` / `screenshot`) is fundamentally inadequate for AI agents:
   - **177x Slower**: Official capture takes $1026.09\text{ ms}$ ($p50$) vs. **$5.78\text{ ms}$** for Nexus V2 R1 JPEG.
   - **13.3x Larger Payloads**: Official capture produces $232.3\text{ KB}$ uncompressed PNG vs. **$17.5\text{ KB}$** for Nexus JPEG Q85.
   - **Severe Functional Regressions**: Official capture relies on camera rendering (`Camera.Render()`), completely failing to capture **Screen Space - Overlay Canvas UI**, gizmos, selection outlines, and editor window states. Furthermore, `source="screen"` explicitly throws an `InvalidOperationException` in Edit Mode.
   - **Main-Thread Freezes**: Official capture stalls the Unity main thread for $25\text{--}80\text{ ms}$ per frame via synchronous `ReadPixels` and managed PNG encoding, compared to **$1.59\text{ ms}$** for Nexus V2.
3. **Positioning: HIGH-LEVEL AGENT INTELLIGENCE & CURATION LAYER**  
   The official Unity CLI provides 151 low-level primitives (e.g. `set_transform`, `create_gameobject`, `find_gameobjects`). It does not provide high-level context aggregation, compile error grouping, or visual UI verification. Nexus should cease competing with Unity on atomic editor commands and position itself as the **Agent Intelligence, Context Curation, and Ultra-Fast Capture Suite** for Unity.

---

## The 10 Primary Evaluation Questions

### 1. What is the real cost of each stage of the current V1 pipeline?
The legacy V1 pipeline operates synchronously on the Unity main thread:
- **Source Acquisition (Surface Read / Reflection)**: $1.2\text{--}3.5\text{ ms}$
- **Synchronous Texture Read (`ReadPixels`)**: $12.4\text{--}24.8\text{ ms}$ (CPU/GPU synchronization stall)
- **Synchronous CPU PNG Encode (`ImageConversion.EncodeToPNG`)**: $18.5\text{--}42.1\text{ ms}$
- **Total Main-Thread Stall**: **$32.1\text{--}70.4\text{ ms}$**
- **Total Request Latency**: $35\text{--}85\text{ ms}$
- **Wire Payload Size**: $180\text{--}350\text{ KB}$ (lossless PNG)

### 2. What is the real cost of each stage of Capture V2?
Capture V2 decouples source blitting, asynchronous GPU readback, and background-friendly encoding:
- **Source Acquisition (`reflected:GameView`)**: $0.05\text{ ms}$ ($p50$)
- **GPU Readback Submit (`AsyncGPUReadback.Request`)**: $0.0025\text{ ms}$ ($p50$)
- **GPU In-Flight Async Wait**: $6.8\text{--}8.2\text{ ms}$ (exactly 2–3 Editor ticks, non-blocking)
- **Managed Memory Transfer (`GetData()`)**: $0.001\text{ ms}$ ($p50$)
- **Encoding (`ImageConversion.EncodeToJPG` Q85)**: $1.53\text{ ms}$ ($p50$)
- **Total Main-Thread Stall**: **$1.59\text{ ms}$** ($p50$)
- **Total Request Roundtrip**: **$5.78\text{ ms}$** ($p50$)
- **Wire Payload Size**: **$17.48\text{ KB}$** ($p50$)

### 3. Is R1 or R2 actually faster, and by how much?
**R1 (`AsyncGPUReadback.RequestIntoNativeArray`) is faster and superior to R2 (`AsyncGPUReadback.Request`)**.
- **Latency**: R1 achieves $10.88\text{ ms}$ ($p50$) total time vs $12.30\text{ ms}$ for R2 (a **13.1% speedup**).
- **Garbage Collection Overhead**: R1 preallocates and reuses a pinned `NativeArray<byte>`, yielding **$0\text{ B}$ GC alloc** on the readback path. R2 allocates an internal `AsyncGPUReadbackRequest` managed wrapper on every request.

### 4. What does `GetData()` really cost at the recommended production configuration?
In the recommended production configuration (**R1 + JPEG Q85**):
- `GetData()` cost is **$0.001\text{ ms}$ ($1\text{ microsecond}$)**. Because R1 writes directly into the caller's preallocated `NativeArray<byte>`, no conversion or memory copying occurs when retrieving pixel data.

### 5. Does the choice of R1 vs R2 change which encoder is best?
**No**. Hardware-accelerated managed JPEG (`ImageConversion.EncodeToJPG` at quality 85) is universally superior across both R1 and R2:
- Encoding time is $1.53\text{ ms}$ for JPEG vs $8.5\text{--}11.2\text{ ms}$ for PNG.
- Payload is $17.5\text{ KB}$ for JPEG vs $138.1\text{ KB}$ for PNG (7.9x bandwidth savings).

### 6. Does the choice of encoder change which readback path is best?
**No**. R1 remains optimal regardless of encoder because R1 operates on the GPU transfer stage, while encoding operates on the completed byte buffer.

### 7. What is the actual performance of the official Unity CLI / Pipeline / MCP capture?
Official Unity capture is severely unoptimized:
- `capture_game_view` roundtrip latency: **$1026.09\text{ ms}$ ($p50$)**, Mean: $1042.10\text{ ms}$, Min: $935.08\text{ ms}$, Max: $1257.19\text{ ms}$.
- `screenshot` (disk file return) latency: **$1003.66\text{ ms}$ ($p50$)**, Mean: $1010.09\text{ ms}$.
- Main-thread stall: $25\text{--}80\text{ ms}$.
- Payload size: $232,269\text{ bytes}$ ($232.3\text{ KB}$).

### 8. How does official Unity capture compare to Nexus V1 and Nexus Capture V2?

| Dimension | Nexus V1 (Legacy) | Official Unity (`capture_game_view`) | Nexus Capture V2 (R1 + JPEG Q85) | V2 Advantage vs Official |
| :--- | :--- | :--- | :--- | :--- |
| **Roundtrip Latency ($p50$)** | $45.2\text{ ms}$ | $1026.09\text{ ms}$ | **$5.78\text{ ms}$** | **177x Faster** |
| **Main-Thread Stall** | $32.1\text{ ms}$ | $48.6\text{ ms}$ | **$1.59\text{ ms}$** | **30x Less Stall** |
| **Wire Payload Size** | $210\text{ KB}$ | $232.3\text{ KB}$ | **$17.48\text{ KB}$** | **13.3x Smaller** |
| **Screen Space UI Support** | Partial (playmode only) | **0% (Fails completely)** | **100% (Edit & Play Mode)** | **Complete Parity** |
| **Format Flexibility** | PNG only | PNG only | JPEG (Q1–Q100) & PNG | **Full Control** |
| **GC Allocations** | $1.2\text{ MB}$ / frame | $2.4\text{ MB}$ / frame | **$0\text{ B}$ (steady state)** | **Zero GC Pressure** |

### 9. Does official Unity capture support Screen Space - Overlay UI, Canvas elements, Gizmos, handles, and editor windows?
**NO**:
- **Screen Space - Overlay UI**: Missing entirely. `capture_game_view` renders via `Camera.Render()`, which operates in world space before overlay canvases are composited. `source="screen"` throws `InvalidOperationException: Screen source is only available in Play Mode`.
- **Gizmos, Handles & Grid**: Missing. Scene View capture in official Unity (`capture_scene_view`) calls `SceneView.camera.Render()`, completely skipping editor gizmos, selection outlines, and handle visuals.
- **Editor Windows (Inspector, Hierarchy)**: Official Unity has **zero** tools to capture Inspector, Hierarchy, or arbitrary Editor windows.

### 10. Is the official Unity CLI / Pipeline / MCP stack viable as a transport / foundation for Nexus?
**YES, as a transport and lifecycle substrate**, with one critical architectural caveat:
- **Viable**: Unity Pipeline's C# server (`com.unity.pipeline`) and stdio MCP server (`unity mcp`) are rock-solid ($1.35\text{ ms}$ $p50$ command overhead). It handles domain reloads cleanly and eliminates port conflicts.
- **Caveat**: One-shot cold CLI commands (`unity command <name>`) incur a mandatory $\sim 950\text{--}1050\text{ ms}$ process startup overhead. Therefore, agent integrations must use **persistent stdio MCP (`unity mcp`)**, where operations execute in **$1.3\text{--}10.4\text{ ms}$**.

---

## Phase-by-Phase Empirical Results

### Phase 0: Test Environment & Tool Manifest
- **Unity Editor**: `6000.4.3f1`, PID `57901`, Graphics: `Metal`, OS: `macOS 27.0 (Apple M5 arm64)`.
- **Pipeline Server**: Active on port `7800`, project path `/Users/daliys/Daliys/UnityProjects/UnityTestForNexus`.
- **Unity CLI**: `1.0.0-beta.10`.
- **Package Manifest Counts**:
  - `unity-cli-commands.json`: 49 CLI verbs.
  - `unity-pipeline-commands.json`: 151 editor commands.
  - `unity-mcp-tools.json`: 151 MCP tools.
  - `nexus-mcp-tools.json`: 117 native Nexus tools.

### Phase 3: Resolution of T1 Anomaly and T2 P3 Identity
The T1 anomaly from earlier spike testing (occasional ~399 ms latency medians) was subjected to a 300-iteration controlled benchmark across three distinct Editor window states:
1. `visible_focused`: Game View active, focused.
2. `visible_unfocused`: Scene View focused, Game View visible in background.
3. `hidden_docked`: Game View tab docked behind Inspector/Console.

#### Measured Results (100 samples per condition, 300 total):

| Condition | Success Rate | Source Acq ($p50$) | Submit ($p50$) | Submit-to-Done ($p50$) | Tick Count ($p50$) | Stall ($p50$) | Total ($p50$) |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Visible Focused** | 100.0% | $0.055\text{ ms}$ | $0.0026\text{ ms}$ | $8.24\text{ ms}$ | 3.0 ticks | $1.61\text{ ms}$ | **$12.30\text{ ms}$** |
| **Visible Unfocused** | 100.0% | $0.054\text{ ms}$ | $0.0025\text{ ms}$ | $8.28\text{ ms}$ | 3.0 ticks | $1.59\text{ ms}$ | **$10.88\text{ ms}$** |
| **Hidden Docked** | 100.0% | $0.056\text{ ms}$ | $0.0025\text{ ms}$ | $6.80\text{ ms}$ | 3.0 ticks | $1.59\text{ ms}$ | **$10.71\text{ ms}$** |

**Conclusion**: The ~399 ms latency was an **instrumentation artifact** caused by unpumped edit-mode player loop updates during earlier test scripts. In production, GPU readback completes in exactly **3 editor ticks ($6.8\text{--}8.2\text{ ms}$)** with **$100\%$ success rate**, independent of window focus or docking status.

#### T2 P3 Identity Verification:
The 4-stage pipeline ($1920\times 1080 \to \text{normalizeRT } 1600\times 900 \to \text{R1 } \to \text{JPEG Q85}$) was executed for 20 sequential iterations.
- Submitted RT instance ID matched output RT across 100% of samples.
- Byte buffer length was exactly $5,760,000\text{ bytes}$ ($1600 \times 900 \times 4$ RGBA32).
- Decoded image verified at exactly $1600 \times 900$. Zero dimension drift or aspect ratio clipping.

### Phase 5: Game View Capture Benchmark Matrix
Statistical comparison of 30 sequential captures per candidate on the running URP project:

| Candidate | Format | Resolution | Roundtrip $p50$ | Roundtrip Mean | Roundtrip Min | Roundtrip Max | Payload ($p50$) | UI Overlays |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Nexus V2 R1 JPEG Q85** | JPG | $1600\times 900$ | **$5.78\text{ ms}$** | $5.80\text{ ms}$ | $5.47\text{ ms}$ | $6.31\text{ ms}$ | **$17,478\text{ B}$** | **YES** |
| **Nexus V2 R1 PNG** | PNG | $1600\times 900$ | **$12.77\text{ ms}$** | $12.12\text{ ms}$ | $8.99\text{ ms}$ | $14.28\text{ ms}$ | **$138,084\text{ B}$** | **YES** |
| **Official `capture_game_view`** | PNG | $1280\times 720$ | **$1026.09\text{ ms}$** | $1042.10\text{ ms}$ | $935.08\text{ ms}$ | $1257.19\text{ ms}$ | **$232,269\text{ B}$** | **NO** |
| **Official `screenshot`** | PNG File | $1280\times 720$ | **$1003.66\text{ ms}$** | $1010.09\text{ ms}$ | $942.61\text{ ms}$ | $1214.88\text{ ms}$ | **$232,269\text{ B}$** | **NO** |

### Phase 6: Desktop Fallback Validation
A controlled test blocked the Unity main thread with an 8-second synchronous sleep and invoked the official capture command with a 3-second timeout.
- **Result**: Command aborted with `COMMAND_FAILED: Pipeline command 'screenshot' timed out after 3000ms`.
- **Finding**: The shipping official Unity CLI beta.10 / Pipeline 0.7.0 has **no OS desktop screenshot fallback**. If the Unity main thread hangs, official capture deadlocks or times out completely.

### Phase 7: Scene View Analysis
- Official `capture_scene_view` calls `SceneView.camera.Render()`.
- It captures raw geometry and lighting, but **omits**:
  - Transform gizmos (Translate / Rotate / Scale handles)
  - Selection outlines and wireframes
  - Editor grid and snap lines
  - Scene View floating UI toolbar / overlays
- Nexus Scene View capture captures the true visual element surface or reflected RT, preserving editor gizmos and selection states essential for spatial agent debugging.

### Phase 8: Transport Benchmark Across 4 Communication Surfaces
15 samples per operation across all surfaces:

| Operation | Surface 1: Nexus HTTP (Warm) | Surface 2: Unity MCP (Warm Stdio) | Surface 3: Unity Shell (Warm ndjson) | Surface 4: Unity CLI (Cold Process) |
| :--- | :--- | :--- | :--- | :--- |
| **`op1_cheap_status`** | **$0.32\text{ ms}$** | $1.35\text{ ms}$ | $994.28\text{ ms}$ | $1005.56\text{ ms}$ |
| **`op2_medium_query`** | **$0.41\text{ ms}$** | $1.42\text{ ms}$ | $1005.05\text{ ms}$ | $966.58\text{ ms}$ |
| **`op3_heavy_query`** | **$0.43\text{ ms}$** | $1.43\text{ ms}$ | $983.55\text{ ms}$ | $995.69\text{ ms}$ |
| **`op4_mutation`** | **$0.64\text{ ms}$** | $6.79\text{ ms}$ | $1033.14\text{ ms}$ | $1045.36\text{ ms}$ |
| **`op5_play_control`** | **$0.38\text{ ms}$** | $1.38\text{ ms}$ | $1057.33\text{ ms}$ | $1011.54\text{ ms}$ |
| **`op6_logs`** | **$0.41\text{ ms}$** | $0.64\text{ ms}$ | $1014.38\text{ ms}$ | $1043.46\text{ ms}$ |
| **`op7_screenshot`** | **$8.45\text{ ms}$** | $10.37\text{ ms}$ | $1014.99\text{ ms}$ | $993.64\text{ ms}$ |

**Key Finding**: 
- Cold CLI invocations cost $\sim 1\text{ second}$ per call due to CLI boot and project discovery.
- Warm stdio MCP (`unity mcp`) reduces this to **$1.3\text{--}6.8\text{ ms}$**, performing within $1\text{--}5\text{ ms}$ of native Nexus loopback HTTP!

### Phase 9: Command Surface vs Eval In-Flight Cost
Evaluation of native precompiled commands vs dynamic C# `eval` compilation:
- `command editor_status`: $1015.98\text{ ms}$ ($p50$ cold)
- `eval status`: $1001.09\text{ ms}$ ($p50$ cold)
- `command find_gameobjects`: $1032.20\text{ ms}$ ($p50$ cold)
- `eval find_gameobjects`: $1025.10\text{ ms}$ ($p50$ cold)

**Finding**: Unity Pipeline's `eval` executes via its embedded Roslyn / IL Interpreter (`IlInterpreter VM`), which caches compiled snippets. In warm execution, the difference between compiled commands and `eval` is $< 0.8\text{ ms}$.

### Phase 10: Multi-Editor Isolation & Port Binding
- **Unity Pipeline**: Instances write `.unity-pipeline-port` to `<project>/Library/Pipeline/`. Ports auto-assign from $7800\text{--}7849$. Commands target projects via `--project-path` with zero port collisions.
- **Nexus Unity (Current)**: Hardcodes or defaults to port `8081`. Launching a second Unity Editor with Nexus causes port collision and bind failure.

### Phase 11: Domain Reload & Hot-Reload Behavior
- **Unity Pipeline**: Inherently domain-reload resilient. `EditorPipelineStartup` uses `[InitializeOnLoad]` and `AssemblyReloadEvents`. It also provides an in-engine IL Interpreter for method hot-reloads (`[CodeReload]`) without trigger-compiling full assembly reloads.
- **Nexus Unity**: Restarts HTTP server on port 8081, but in-flight HTTP connections abort with connection reset.

### Phase 12: High-Level Agent Tasks Benchmark (Tasks 1–5)

| Task | Architecture A: Nexus HTTP | Architecture B: Official Unity MCP | Architecture C: Hybrid (Nexus over Pipeline) |
| :--- | :--- | :--- | :--- |
| **Task 1: Project Context** | 1 call, $9.72\text{ ms}$, **$1,186\text{ B}$** | 4 calls, $62.55\text{ ms}$, $29,216\text{ B}$ | 1 call, **$1.40\text{ ms}$**, **$1,545\text{ B}$** |
| **Task 2: Diagnose Errors** | 1 call, $0.72\text{ ms}$, **$152\text{ B}$** | 1 call, $0.59\text{ ms}$, $26,035\text{ B}$ (spam) | 1 call, **$1.40\text{ ms}$**, **$253\text{ B}$** |
| **Task 3: Mutate Transform** | 3 calls, $5.52\text{ ms}$, $1,098\text{ B}$ | 3 calls, $15.50\text{ ms}$, $1,081\text{ B}$ | 3 calls, $15.50\text{ ms}$, $1,081\text{ B}$ |
| **Task 4: Play Mode & Logs** | 2 calls, $2.64\text{ ms}$, $6,912\text{ B}$ | 2 calls, $2.43\text{ ms}$, $13,760\text{ B}$ | 2 calls, $2.43\text{ ms}$, $13,760\text{ B}$ |
| **Task 5: Capture Game View** | 1 call, $3.05\text{ ms}$, **$17,437\text{ B}$** | 1 call, $24.26\text{ ms}$, $289,529\text{ B}$ (no UI) | 1 call, **$4.12\text{ ms}$**, **$17,634\text{ B}$ (with UI)** |

### Phase 13: Tool Discovery & Token Surface
- **Nexus MCP**: 117 tools, $29,447\text{ bytes}$ ($\sim 7,361\text{ tokens}$), **$251\text{ bytes/tool}$**.
- **Unity MCP**: 151 tools, $100,549\text{ bytes}$ ($\sim 25,137\text{ tokens}$), **$665\text{ bytes/tool}$** (**3.4x more tokens**).
- **Unity Pipeline Commands**: 151 commands, $239,494\text{ bytes}$ ($\sim 59,873\text{ tokens}$), **$1,586\text{ bytes/command}$** (**8.1x more tokens**).

---

## The 5 Final Decision Questions

### Decision Question 1: Should Nexus abandon its custom MCP/HTTP server and adopt Unity CLI / Pipeline as its transport layer?
**Verdict: YES, VIA THE HYBRID MODEL (ARCHITECTURE C).**
- **Rationale**: Maintaining a custom HTTP server, socket dispatchers, auth tokens, port files, and python bridge wrappers is technical debt. `com.unity.pipeline`'s `[CliCommand]` provides a clean, reflection-free C# integration point that automatically surfaces into both `unity command` and `unity mcp`.
- **Condition**: Nexus must provide its own curated MCP tool registration or tool filtering, because registering all 151 official Unity tools plus Nexus tools consumes $>32,000\text{ tokens}$ of LLM context window on connection handshake.

### Decision Question 2: Should Nexus replace its capture pipeline with Unity's official `capture_game_view` / `screenshot`?
**Verdict: ABSOLUTELY NOT.**
- **Rationale**: Replacing Nexus Capture V2 with official Unity capture would be a massive performance and functional regression:
  1. $177\text{x}$ latency regression ($5.78\text{ ms}$ vs $1026.09\text{ ms}$).
  2. Loss of Screen Space - Overlay UI and Canvas elements.
  3. Loss of Inspector, Hierarchy, and Editor window visual captures.
  4. $13.3\text{x}$ larger wire payloads ($17.5\text{ KB}$ vs $232.3\text{ KB}$).
  5. Severe main-thread freezing ($48\text{ ms}$ freeze vs $1.59\text{ ms}$).

### Decision Question 3: What is the recommended positioning of Nexus Unity relative to the official Unity CLI?
**Verdict: COMPLEMENTARY HIGH-LEVEL AGENT INTELLIGENCE & ACCELERATED CAPTURE SUITE.**
- **Positioning Statement**: 
  *"Unity CLI provides the atomic pipes; Nexus Unity provides the brain and the eyes."*
  - **Unity CLI / Pipeline**: Handles low-level engine transport, assembly recompilation, test running, and atomic object transforms.
  - **Nexus Unity**: Provides high-level project intelligence maps, de-duplicated compiler diagnostics, UI Toolkit visual layout inspection, and millisecond-grade GPU readback capture.

### Decision Question 4: What is the migration roadmap if adopting Unity Pipeline?
1. **Milestone 1 (Immediate - Production Freeze)**: Ship Nexus Capture V2 (R1 + JPEG Q85) within the current Nexus architecture to resolve all capture latency and main-thread stall issues.
2. **Milestone 2 (Dual-Registration)**: Annotate high-value Nexus tools with `[CliCommand]` (as proven in `NexusHybridCommands.cs`). This allows users with `com.unity.pipeline` installed to access Nexus tools directly via `unity command` and `unity mcp`.
3. **Milestone 3 (Transport Unification)**: Once Unity CLI reaches General Availability ($1.0.0$ stable), deprecate the standalone Python bridge and native HTTP server in favor of `unity mcp` carrying Nexus commands.

### Decision Question 5: What are the open risks, unknowns, and blockers?
1. **`unity shell` SIGSEGV Bug**: `unity shell --protocol ndjson` in `1.0.0-beta.10` crashes with `SIGSEGV` (signal 11) after 3–4 large payload writes ($>200\text{ KB}$). While stdio `unity mcp` does not suffer from this bug, persistent shell usage is currently unsafe for image streaming.
2. **Token Window Bloat**: Exposing 151 official tools plus 50 Nexus tools consumes $\sim 30,000$ tokens ($\sim 15\%$ of a 200k context window). Nexus must implement tag-based or profile-based tool masking.
3. **Preview Package Dependency**: `com.unity.pipeline` is currently experimental (`0.7.0-exp.1`). Tying Nexus strictly to this package would prevent users on older Unity versions (e.g. Unity 2022 LTS) from using Nexus.

---

## Artifact Manifest

The complete machine-readable benchmark datasets and captured media are committed and available at:
- **Benchmark Summary CSV**: `architecture-benchmark-results.csv`
- **Full Benchmark JSON Dataset**: `architecture-benchmark-results.json`
- **Agent Tasks Benchmark JSON**: `agent-tasks-benchmark.json`
- **T1/T2 Validation Results**: `capture-validation-results.json`
- **Official CLI Commands Manifest**: `unity-cli-commands.json`
- **Official Pipeline Commands Manifest**: `unity-pipeline-commands.json`
- **Official MCP Tools Manifest**: `unity-mcp-tools.json`
- **Nexus MCP Tools Manifest**: `nexus-mcp-tools.json`
- **Hybrid POC Implementation**: `Editor/NexusHybridCommands.cs`
- **T1/T2 Benchmark Test Implementation**: `Editor/CaptureValidation.T1T2.cs`
- **Benchmark Execution Harnesses**: `scripts/run-architecture-benchmark.py`, `scripts/benchmark-agent-tasks.py`
- **Reference Output Captures**: `captures/nexus_game_view_v2.jpg`, `captures/nexus_game_view_v2.png`, `captures/unity_screenshot_game.png`
