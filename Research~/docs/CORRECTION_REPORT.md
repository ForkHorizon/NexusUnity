# Historical Correction Report: Nexus Unity vs. Unity CLI / Pipeline / MCP

> Historical research archive. This file is retained as evidence of corrected evaluation work; it is not current product documentation. Do not use its implementation-attribution, hardware-encoder, benchmark, or performance language as a current Nexus Unity claim. Current public truth is in `README.md`, `DOCUMENTATION.MD`, and `API_REFERENCE.MD`.

**Evaluation Date**: September 21, 2026  
**Target Environments**:
- **Unity Editor**: `6000.4.3f1` (Apple Silicon arm64, Metal API, URP 17.5.0, Linear Color Space)
- **Unity CLI**: `1.0.0-beta.10` (Release Date: September 16, 2026)
- **Unity Pipeline Package**: `com.unity.pipeline@0.7.0-exp.1`
- **Nexus Unity Candidate**: Branch `rework/T01` (Capture V2 Pipeline)
- **Host Machine**: macOS 27.0 (Build 26A428), Apple M5 (Mac17,2)
- **Empirical Dataset**: [`ambiguity-validation-results.json`](./ambiguity-validation-results.json)

---

## Section 1: Identify the Actual AsyncGPUReadback Backend

### Source Code Inspection
- **Source File**: [`Editor/NexusHybridCommands.cs`](./Editor/NexusHybridCommands.cs) (lines 174–180) and [`Editor/CaptureValidation.T1T2.cs`](./Editor/CaptureValidation.T1T2.cs) (lines 193–199).
- **Method**: `NexusHybridCommands.CaptureGameView` / `CaptureValidation.ExecuteT1SingleSampleAsync`.
- **Exact API Call**: `AsyncGPUReadback.Request(RenderTexture src)`
- **Preallocated NativeArray Supplied**: **`false`**. No user-allocated or pinned `NativeArray` buffer is passed to `Request`.
- **Raw Readback Memory Owner**: **Unity Graphics Driver**. The memory is held in an internal driver-managed ring buffer allocated and scheduled by the native Unity Engine runtime.
- **Buffer Disposal Model**: **`false` (Not manually disposed)**. The call `req.GetData<byte>()` returns a non-owning `NativeArray<byte>` view slice pointing into driver-managed memory. This memory is automatically released by the engine when the asynchronous request completes. Callers do not call `.Dispose()` on this view.
- **Object Passed to ImageConversion**: A non-owning `NativeArray<byte>` slice returned from `req.GetData<byte>()`, passed directly into `ImageConversion.EncodeNativeArrayToJPG` or `ImageConversion.EncodeNativeArrayToPNG`.
- **Architecture Classification**: **`DriverOwnedReadback`**. The Nexus Capture V2 candidate in this repository strictly uses `DriverOwnedReadback` (`AsyncGPUReadback.Request(rt) -> req.GetData<byte>()`). It does **not** use `PersistentNativeArrayReadback` (`AsyncGPUReadback.RequestIntoNativeArray`).

### Production Baseline Performance (100 Samples)
Measured over 100 consecutive executions under steady-state conditions:
- **Submit-to-Done Latency**: $p50 = 6.76\text{ ms}$, $p95 = 7.84\text{ ms}$ (consistently 3 player loop ticks).
- **Encode Latency (JPEG Q85)**: $p50 = 1.53\text{ ms}$, $p95 = 1.56\text{ ms}$.
- **Main-Thread Stall**: $p50 = 1.59\text{ ms}$, $p95 = 1.63\text{ ms}$.
- **Total Request Latency**: $p50 = 10.69\text{ ms}$, $p95 = 12.09\text{ ms}$.
- **Success Rate**: $100\%$ (0 errors, 0 dropped frames).

---

## Section 2: Fair Capture Benchmark Under Identical and Fair Conditions

All captures were performed at $1280 \times 720$ resolution over warm persistent transports across 100 interleaved iterations.

### Table 1: Same Resolution, Same Format (Apples-to-Apples PNG vs PNG)

| Metric | Nexus Capture V2 (Lossless PNG) | Official Unity (`capture_game_view` PNG) | Fair Comparison / Advantage |
| :--- | :--- | :--- | :--- |
| **Format & Resolution** | $1280 \times 720$ PNG | $1280 \times 720$ PNG | Identical ($1:1$) |
| **Roundtrip Latency ($p50$)** | **$143.14\text{ ms}$** | $157.33\text{ ms}$ | **$1.10\times$ faster** roundtrip ($-14.19\text{ ms}$) |
| **Roundtrip Latency ($p95$)** | **$151.16\text{ ms}$** | $169.09\text{ ms}$ | **$1.12\times$ faster** ($-17.93\text{ ms}$) |
| **Roundtrip Latency ($p99$)** | **$200.90\text{ ms}$** | $222.99\text{ ms}$ | **$1.11\times$ faster** ($-22.09\text{ ms}$) |
| **Roundtrip Latency (Mean)** | **$142.15\text{ ms}$** | $157.71\text{ ms}$ | **$1.11\times$ faster** ($-15.56\text{ ms}$) |
| **Wire Payload Size ($p50$)** | **$112,914\text{ B}$ ($112.9\text{ KB}$)** | $289,529\text{ B}$ ($289.5\text{ KB}$)** | **$2.56\times$ smaller** ($-176.6\text{ KB}$) |
| **Main-Thread Stall ($p50$)** | **$1.59\text{ ms}$** | $\sim 48.60\text{ ms}$ | **$30.6\times$ less main-thread freeze** |
| **Success Rate** | $100\%$ | $100\%$ | Parity |
| **Stale / Black Frame Rate** | $0\% / 0\%$ | $0\% / 0\%$ | Parity |
| **Screen Space - Overlay UI** | **Captured (Full Support)** | **Missing (Fails completely)** | Nexus captures overlay UI via reflected backbuffer |
| **Visual Quality** | Pass (crisp, normal upright) | Pass for 3D meshes; misses canvas | Validated on disk |

### Table 2: Product Workflow Comparison (Nexus Product Fast-Path vs. Unity Official Normal)

> [!NOTE]
> This compares the recommended production configuration for each stack: Nexus Capture V2 optimized for AI agent consumption (JPEG Q85) vs. Official Unity's default fixed configuration (PNG).

| Metric | Nexus Product Fast-Path (JPEG Q85) | Unity Official Normal (PNG) | Product Workflow Difference |
| :--- | :--- | :--- | :--- |
| **Format & Resolution** | $1280 \times 720$ JPEG (Quality 85) | $1280 \times 720$ PNG | Optimized lossy vs Lossless |
| **Roundtrip Latency ($p50$)** | **$102.34\text{ ms}$** | $157.33\text{ ms}$ | **$1.54\times$ faster** ($-54.99\text{ ms}$) |
| **Roundtrip Latency ($p95$)** | **$143.99\text{ ms}$** | $169.09\text{ ms}$ | **$1.17\times$ faster** ($-25.10\text{ ms}$) |
| **Roundtrip Latency ($p99$)** | **$189.86\text{ ms}$** | $222.99\text{ ms}$ | **$1.17\times$ faster** ($-33.13\text{ ms}$) |
| **Roundtrip Latency (Mean)** | **$101.98\text{ ms}$** | $157.71\text{ ms}$ | **$1.55\times$ faster** ($-55.73\text{ ms}$) |
| **Wire Payload Size ($p50$)** | **$43,406\text{ B}$ ($43.4\text{ KB}$)** | $289,529\text{ B}$ ($289.5\text{ KB}$)** | **$6.67\times$ smaller** ($-246.1\text{ KB}$) |
| **Main-Thread Stall ($p50$)** | **$1.59\text{ ms}$** | $\sim 48.60\text{ ms}$ | **$30.6\times$ less main-thread freeze** |
| **Agent Visual Usability** | High (text and icons legible) | High (lossless) | Identical semantic comprehension |

### Part C: Transport vs. Capture Separation
Evaluating transport overhead independently shows that loopback HTTP and stdio MCP perform similarly under persistent connections:
- **Official Unity MCP Path**: Total $p50 = 157.33\text{ ms}$ = $100.20\text{ ms}$ (stdio MCP JSON-RPC transport overhead) + **$57.13\text{ ms}$** (Editor synchronous `ReadPixels` + PNG encode).
- **Nexus HTTP Path**: Total $p50 = 143.14\text{ ms}$ = $99.80\text{ ms}$ (HTTP loopback JSON-RPC transport overhead) + **$43.34\text{ ms}$** (Editor GPU readback + PNG encode).
- **Editor-Only Capture Execution**: Nexus is **$13.79\text{ ms}$ faster** ($43.34\text{ ms}$ vs $57.13\text{ ms}$, or **$1.32\times$ faster**) during in-editor execution for PNG, and **$32.65\text{ ms}$ faster** for JPEG Q85 ($10.69\text{ ms}$ vs $43.34\text{ ms}$).

### Part D: UI Overlay and Visual Semantic Comparison
- **Gameplay Geometry (3D Meshes & Shaders)**: Both capture accurately.
- **Screen Space - Overlay UI**: Nexus captures accurately via the reflected Game View backbuffer. Official Unity capture **fails completely** (`source="camera"` skips overlay canvases; `source="screen"` throws `InvalidOperationException: Screen source is only available in Play Mode`).
- **Camera-Space & World-Space UI**: Both capture accurately.
- **Post-Processing Volume Effects (URP)**: Both capture accurately.

---

## Section 3: Verification of Unity MCP Desktop Fallback Claim

### Tool Contract Under Test
- **Tool**: `capture_game_view`
- **Advertised Description**: `"Capture the game view. In play mode, captures the live game. In edit mode, captures the current scene as viewed through the active camera. If the editor does not respond, falls back to a desktop screen capture."`

### Test Procedure and Measured Behavior
Five consecutive trials were executed through persistent `unity mcp` while the Unity main thread was blocked with a 6.0-second synchronous sleep (`System.Threading.Thread.Sleep(6000)`):
- **Trial 1**: Returned at $t = 6054.75\text{ ms}$ (image returned only after main thread unblocked).
- **Trial 2**: Returned at $t = 6077.65\text{ ms}$ (image returned only after main thread unblocked).
- **Trial 3**: Returned at $t = 6028.94\text{ ms}$ (image returned only after main thread unblocked).
- **Trial 4**: Returned at $t = 6026.87\text{ ms}$ (image returned only after main thread unblocked).
- **Trial 5**: Returned at $t = 5586.41\text{ ms}$ (image returned only after main thread unblocked).
- **Scene View Trial**: Returned at $t = 6018.54\text{ ms}$ (image returned only after main thread unblocked).

In all trials, the request remained queued in the Editor main-thread dispatcher. At no point during the 6-second freeze was an external desktop screen capture triggered. Direct source code inspection of `CaptureCommands.cs` in `com.unity.pipeline@0.7.0-exp.1` confirmed that no desktop capture fallback implementation exists in the package.

### Authoritative Conclusion
**Conclusion B: "Desktop fallback is advertised but did not trigger under this tested beta.10 configuration."**

---

## Section 4: Hybrid Transport Parity Under Warm Persistent Connections

Evaluated over 100 iterations per command (preceded by 20 warmup calls) connecting to the active Unity instance:

| Tool / Command | Category | Nexus HTTP ($p50$ / $p95$) | Unity MCP Hybrid ($p50$ / $p95$) | Delta ($p50$ / $p95$) |
| :--- | :--- | :--- | :--- | :--- |
| `nexus_group_compile_errors` | Cheap | $99.74\text{ ms}$ / $125.34\text{ ms}$ | $101.83\text{ ms}$ / $112.95\text{ ms}$ | **$+2.10\text{ ms}$** / $-12.39\text{ ms}$ |
| `nexus_project_map` | Medium | $97.96\text{ ms}$ / $113.90\text{ ms}$ | $102.39\text{ ms}$ / $119.47\text{ ms}$ | **$+4.43\text{ ms}$** / $+5.57\text{ ms}$ |
| `nexus_capture_game_view` | Heavy | $97.03\text{ ms}$ / $137.24\text{ ms}$ | $111.67\text{ ms}$ / $140.57\text{ ms}$ | **$+14.64\text{ ms}$** / $+3.33\text{ ms}$ |

### Empirical Finding
On warm persistent connections, the transport latency delta between Nexus HTTP loopback and Unity MCP stdio IPC is **$2\text{--}14\text{ ms}$**. The earlier impression that Unity CLI / Pipeline was "100x slower" was caused entirely by cold process startup overhead (spawning a fresh CLI process per command), not transport protocol inefficiencies.

---

## Section 5: Explicit Corrections to Previous Report

The following claims in previous reports were incorrect, confounded, or based on mistaken API identification, and are formally corrected:

1. **The "177x Faster" Claim (Retracted)**:
   - *Previous claim*: Nexus capture is $177\times$ faster than official Unity capture ($5.78\text{ ms}$ vs. $1026.09\text{ ms}$).
   - *Why invalid*: The comparison contrasted an internal in-engine timestamp ($5.78\text{ ms}$) of Nexus JPEG against a cold-spawned CLI invocation of Unity CLI ($1026\text{ ms}$) that incurred operating system process creation, .NET assembly discovery, and socket setup overhead on every call.
   - *Correct ratio*: Under fair, warm persistent conditions at identical resolution and format ($1280 \times 720$ PNG), Nexus roundtrip is **$1.10\times$ faster** ($143.14\text{ ms}$ vs. $157.33\text{ ms}$), and in-engine execution is **$1.32\times$ faster** ($43.34\text{ ms}$ vs. $57.13\text{ ms}$). In the product fast-path (JPEG Q85 vs. PNG), Nexus roundtrip is **$1.54\times$ faster** ($102.34\text{ ms}$ vs. $157.33\text{ ms}$).
2. **The "13.3x Smaller Payload" Claim (Retracted)**:
   - *Previous claim*: Nexus produces payloads $13.3\times$ smaller than official Unity capture ($17.5\text{ KB}$ vs. $232.3\text{ KB}$).
   - *Why invalid*: The comparison contrasted a lossy 720p JPEG at Quality 85 against an uncompressed, high-DPI full-resolution PNG.
   - *Correct ratio*: At identical $1280 \times 720$ resolution in PNG, Nexus payload is **$2.56\times$ smaller** ($112.9\text{ KB}$ vs. $289.5\text{ KB}$). In the product fast-path (JPEG Q85 vs. PNG), the payload is **$6.67\times$ smaller** ($43.4\text{ KB}$ vs. $289.5\text{ KB}$).
3. **Readback Backend Terminology & Classification**:
   - *Previous report*: Inverted the naming convention and erroneously asserted that the production pipeline was using preallocated pinned native arrays with zero GC allocations.
   - *Correction*: The production candidate uses `DriverOwnedReadback` (`AsyncGPUReadback.Request(rt) -> req.GetData<byte>()`). It does not preallocate arrays. The legacy shorthand labels are permanently retired.
4. **Hardware JPEG Encoding on Apple Silicon (Retracted)**:
   - *Previous claim*: Asserted that `ImageConversion.EncodeNativeArrayToJPG` utilized hardware VideoToolbox ASIC encoders on Apple Silicon.
   - *Correction*: Unity's `ImageConversion` uses libjpeg-turbo running on CPU cores with ARM NEON SIMD vectorization. It is CPU-bound, not GPU/ASIC hardware-accelerated.
5. **Unity MCP Screenshot Capabilities (Corrected)**:
   - *Previous claim*: Claimed Unity MCP does not support screenshots.
   - *Correction*: Unity MCP *does* support `capture_game_view` and `capture_scene_view`. However, its implementation is limited to camera rendering (`Camera.Render()`), causing main-thread stalls of $\sim 48.6\text{ ms}$ and completely omitting Screen Space - Overlay Canvas UI.

---

## Section 6: Architecture Evidence Gate

1. **Does Unity CLI / Pipeline / MCP provide acceptable performance for core commands (hierarchy, compile, inspect) under warm persistent connections?**  
   **YES**. Under persistent stdio MCP (`unity mcp`), command dispatch overhead is within $2\text{--}4\text{ ms}$ of native HTTP loopback ($p50 \approx 101\text{--}102\text{ ms}$). Performance is fully production-grade.

2. **Does Nexus capture provide a meaningful advantage over Unity capture when compared fairly?**  
   **YES**. While the roundtrip speedup is modest ($1.10\times\text{--}1.54\times$), the **functional and architectural advantages are critical**:
   - Nexus captures Screen Space - Overlay UI; official Unity capture fails completely.
   - Nexus main-thread stall is **$1.59\text{ ms}$** vs. **$\sim 48.60\text{ ms}$** for official capture ($30.6\times$ reduction in editor freeze).
   - Nexus offers configurable JPEG encoding ($43.4\text{ KB}$ vs. $289.5\text{ KB}$ bandwidth).

3. **Is that capture advantage worth maintaining a custom HTTP server?**  
   **NO**. The capture pipeline's advantage is entirely an in-engine C# rendering and readback optimization (`AsyncGPUReadback` + `NativeArray` encoding). It does not require a custom HTTP networking stack, custom thread pools, or custom port discovery logic.

4. **Can Nexus capture be delivered as a Pipeline extension instead of standalone HTTP?**  
   **YES**. Nexus Capture V2 has been validated as a `com.unity.pipeline` `[CliCommand]` extension (`nexus_capture_game_view`). Under persistent `unity mcp`, it executes with only a $+14.6\text{ ms}$ delta over direct HTTP ($111.67\text{ ms}$ vs. $97.03\text{ ms}$) while retaining full async GPU readback and overlay UI fidelity.

5. **Does the desktop fallback claim hold up under verification?**  
   **NO**. Verified across 5 empirical trials with a frozen main thread. Unity MCP blocked on the dispatcher queue until the main thread awoke, and direct package inspection confirmed no desktop fallback mechanism exists.
