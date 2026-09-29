You are taking ownership of the implementation phase for the Nexus Unity architecture migration.

The research, benchmarking, competitive analysis, and architecture decision phases are COMPLETE.

This is no longer an architecture exploration task.

Your job is to IMPLEMENT the agreed architecture carefully and incrementally.

Do not restart research.
Do not redesign Capture V2.
Do not replace the plan with a different architecture unless you discover a concrete code-level blocker that makes the approved design impossible.

---

# 0. HIGH-LEVEL OBJECTIVE

Nexus Unity is evolving from:

```text
AI Client
   ↓
Nexus custom MCP / HTTP transport
   ↓
Nexus Unity tools
   ↓
Unity Editor
```

toward:

```text
AI Client / IDE
       ↓
Nexus curated tool surface
       ↓
Nexus domain/application layer
       ↓
transport/runtime adapter
   ↙                     ↘
Legacy Nexus          Unity Pipeline
HTTP/MCP              / unity mcp
   ↘                     ↙
          Unity Editor
```

The target strategic direction is:

```text
D now → C later
```

Meaning:

## Current program

Dual backend.

Nexus supports:

* existing Legacy HTTP/MCP transport;
* optional Unity Pipeline / `unity mcp` transport.

## Long-term target

Hybrid primary.

Unity eventually owns:

* transport;
* Editor discovery;
* multi-project routing;
* domain reload lifecycle;
* MCP/CLI plumbing;
* common atomic Editor commands.

Nexus continues to own:

* project intelligence;
* context preparation and compression;
* diagnostics;
* reference analysis;
* high-level agent workflows;
* curated tool surfaces;
* Capture V2;
* agent-oriented visual semantics.

Do NOT remove the Legacy backend during the early migration.

---

# 1. VALIDATED FACTS — DO NOT REOPEN

Treat the following as architecture facts.

---

## 1.1 Capture V2 production readback

The production capture backend is:

```csharp
AsyncGPUReadback.Request(RenderTexture)
```

followed after completion by:

```csharp
request.GetData<byte>()
```

This architecture is called:

```text
DriverOwnedReadback
```

Do NOT use old ambiguous labels such as:

```text
R1
R2
```

unless reading historical test data.

Do NOT replace the production path with:

```csharp
AsyncGPUReadback.RequestIntoNativeArray(...)
```

without an explicit new architecture review.

---

## 1.2 Capture pipeline

Current frozen Capture V2 pipeline:

```text
GameView private m_RenderTexture
        ↓ immediate copy
Nexus-owned captureTargetRT
        ↓
optional normalizeRT
        ↓
AsyncGPUReadback.Request(submittedRT)
        ↓
EditorApplication.update polling
        ↓
first update observing done
        ↓
GetData<byte>()
        ↓
ImageConversion.EncodeNativeArrayToJPG / PNG
        ↓
compressed NativeArray.ToArray()
        ↓
CaptureResult
```

Important invariants:

* never retain Unity private GameView RT across the asynchronous request;
* copy it immediately into Nexus-owned RT;
* the submitted Nexus-owned RT must remain alive and immutable until readback completes;
* no `.Wait()`;
* no `.Result`;
* no `GetAwaiter().GetResult()` on Unity main thread;
* no `AsyncGPUReadback.WaitForCompletion()` in normal production path;
* GetData and encode happen immediately when completion is observed;
* JPEG/PNG encoder currently remains on Unity main thread;
* JPEG quality must remain configurable;
* PNG remains explicit lossless mode;
* optional downscale/normalize remains supported;
* 1600×900 is a validated technical fast profile;
* do NOT claim proven identical VLM comprehension between JPEG/downscale and PNG/full-res.

---

## 1.3 Capture ownership

Nexus Capture V2 remains Nexus-owned.

Do NOT replace it with Unity's official `capture_game_view`.

Reasons:

* Nexus captures the Game View as presented, including Screen Space - Overlay UI;
* Unity's tested official camera capture misses Overlay Canvas;
* Nexus has much lower measured Editor stall;
* Nexus controls JPEG/PNG;
* Nexus controls JPEG quality;
* Nexus controls normalization/downscale;
* Nexus has Editor-window capture behavior outside Game View.

The reason to keep Nexus Capture is NOT a fake 177× latency claim.

The old 177× claim has been retracted.

Fair PNG vs PNG warm comparison showed only a modest total roundtrip advantage.

Nexus Capture remains strategically valuable because of:

```text
visual semantics
+
Editor responsiveness
+
output control
```

---

## 1.4 Official Unity stack status

Current tested stack:

```text
Unity CLI:            1.0.0-beta.10
com.unity.pipeline:   0.7.0-exp.1
```

This is NOT stable enough to make Nexus depend exclusively on it.

Pipeline support must initially be optional.

---

## 1.5 Hybrid feasibility

Nexus commands have already been successfully exposed through `[CliCommand]` and invoked through persistent `unity mcp`.

The Hybrid architecture works.

It introduces a relatively small end-to-end latency delta compared with Nexus HTTP.

Therefore Pipeline is viable as a future primary runtime.

---

# 2. PRODUCT / ARCHITECTURE OWNERSHIP BOUNDARY

This boundary is extremely important.

---

# UNITY SHOULD EVENTUALLY OWN

When Unity Pipeline is available and stable:

### Transport/lifecycle

* Editor discovery;
* project routing;
* per-project Pipeline ports;
* domain reload transport lifecycle;
* stdio MCP;
* CLI integration.

### Atomic Editor primitives

Prefer official primitives for commodity operations such as:

* set transform;
* find basic GameObjects;
* simple object mutations;
* play/pause;
* tests;
* builds;
* basic console access;
* generic Editor status.

Nexus should NOT duplicate all ~151 Unity commands.

---

# NEXUS MUST CONTINUE TO OWN

### Intelligence

* project maps;
* contextual project summaries;
* relevant-file discovery;
* context packs;
* reference inspection;
* dependency/context preparation.

### Diagnostics

* grouped compile errors;
* deduplication;
* high-signal summaries;
* agent-oriented error interpretation structures.

### Visual layer

* Capture V2;
* true Game View presented pixels;
* Overlay UI;
* Scene/editor presentation where Nexus supports it;
* arbitrary Editor-window visual capture;
* JPEG/PNG;
* quality control;
* resolution/downscale control.

### Agent-oriented abstraction

Nexus tools should answer:

```text
"What information does the agent need?"
```

rather than merely exposing every low-level Unity API.

---

# 3. TARGET MODULE ARCHITECTURE

The target conceptual layers are:

```text
Nexus.Contracts

Nexus.Application / Nexus.Domain

Nexus.Capture

Nexus.Transport.Legacy

Nexus.Transport.UnityPipeline

Nexus.Compatibility
```

Names may be adjusted to fit current repository conventions.

The separation itself is mandatory.

---

# 4. IMPORTANT DESIGN REFINEMENT

Do NOT make the runtime adapter the owner of Nexus business logic.

Avoid an architecture where every high-level command becomes:

```text
application
→ INexusRuntime.Invoke("command")
→ business logic
```

That reverses the desired dependency direction.

Preferred conceptual flow:

```text
incoming transport request
        ↓
command descriptor / registry
        ↓
Nexus command handler
        ↓
domain/application services
        ↓
Unity APIs / Capture gateway
        ↓
CommandResult
        ↓
transport adapter serialization
```

Transport adapters are outside the domain logic.

Recommended conceptual interfaces:

```text
INexusCommand
INexusCommandRegistry

INexusTransportAdapter
IRuntimeCapabilities

ICaptureGateway
```

Exact names are your implementation choice.

---

# 5. CANONICAL COMMAND MODEL

Nexus should have ONE canonical definition of each Nexus command.

Example conceptually:

```csharp
[NexusCommand(
    Id = "nexus.project_map",
    Profiles = "core,project",
    Title = "...",
    Description = "..."
)]
sealed class ProjectMapCommand : INexusCommand
{
    ...
}
```

The canonical command definition should contain:

* command id;
* description;
* parameter schema;
* result schema if applicable;
* tags/profiles;
* required capabilities;
* handler.

From this ONE definition Nexus should eventually project to:

```text
Legacy Nexus MCP

Unity Pipeline [CliCommand]

unity mcp
```

Avoid maintaining separate descriptions in:

```text
Nexus MCP tool definition
Pipeline command definition
docs metadata
```

Three sources of truth will inevitably drift.

---

# 6. TOOL PROFILES / CURATION

Tool curation is a strategic Nexus feature.

Do NOT expose:

```text
151 Unity tools
+
117 Nexus tools
```

to every model session.

The goal is a compact, high-signal surface.

Ship a small number of profiles.

Recommended initial profiles:

---

## core

Very small default.

Suggested:

```text
status
project_map
group_compile_errors
prepare_context
profile/tool discovery
```

Potentially `capture_game_view`, but consider loading visual explicitly if keeping default context minimal is more important.

---

## visual

Contains:

```text
capture_game_view
capture_scene_presented
capture_editor_window
capture configuration / metadata
```

---

## scene

Only high-level Nexus scene intelligence that is not redundant with Unity's basic GameObject commands.

Do NOT build a second copy of:

```text
find_gameobjects
set_transform
etc.
```

---

## compat

Very small compatibility surface for environments where Pipeline is not available.

Do not mirror the entire official Unity API.

---

# 7. BACKEND SELECTION POLICY

Eventually support configuration:

```text
nexus.runtime = legacy
nexus.runtime = pipeline
nexus.runtime = auto
```

But behavior changes by milestone.

---

## Before M4

`auto` MUST effectively prefer Legacy.

Pipeline can be explicitly enabled for testing/use.

Do not silently switch existing users to Pipeline during M1–M3.

---

## Starting M4

`auto` may prefer Pipeline when all required capability checks pass.

---

# 8. PIPELINE CAPABILITY DETECTION

Pipeline is optional.

Detect it safely.

Use:

* Unity package information;
* Pipeline port/session metadata;
* asynchronous health check.

Do NOT block Unity main thread for 100–200 ms while probing Pipeline.

Preferred behavior:

```text
check package metadata
        ↓
check Pipeline session/port metadata
        ↓
async health probe
        ↓
cache result for current Editor generation
```

If status is still unknown:

Legacy remains available.

After domain reload:

invalidate/re-evaluate capability state.

Never fail Nexus initialization merely because Pipeline is unavailable.

---

# 9. MILESTONE IMPLEMENTATION PLAN

Implement sequentially.

Do not combine everything into one rewrite.

---

# M0 — CAPTURE V2 PRODUCTION BASELINE

If current branch already satisfies M0, verify rather than rewrite.

Required state:

```text
DriverOwnedReadback

JPEG Q configurable

PNG explicit lossless

Overlay UI supported

optional normalize/downscale

existing external API preserved
```

### M0 acceptance

* no RequestIntoNativeArray in production hot path;
* correct Game View output;
* Overlay UI captured;
* existing screenshot schema remains valid;
* errors do not hang requests;
* telemetry exists for capture stages;
* no regression compared with currently validated branch.

Do not re-benchmark the entire architecture.

---

# M1 — EXTRACT NEXUS.CAPTURE

THIS IS THE FIRST IMPLEMENTATION MILESTONE.

Implement it before Pipeline integration.

---

## Goal

Completely separate Capture V2 business logic from:

* HTTP;
* MCP;
* JSON-RPC;
* Python bridge;
* networking;
* transport-specific DTOs.

---

## Introduce

Conceptually:

```text
Nexus.Capture
```

and:

```csharp
ICaptureGateway
```

Possible API:

```csharp
Task<CaptureResult> CaptureGameView(
    CaptureRequest request,
    CancellationToken cancellationToken
);
```

Also potentially:

```csharp
Task<CaptureResult> CaptureScenePresented(...)
Task<CaptureResult> CaptureEditorWindow(...)
```

depending on current existing capture functionality.

---

## CaptureRequest should represent semantic options

For example:

```text
source

format
jpeg quality

requested resolution
or max-long-edge

lossless/fast profile

orientation/color expectations
```

Do not expose transport concepts.

No Base64 in CaptureRequest.

No JSON-RPC ids.

No HttpListener.

---

## CaptureResult

Represent compressed output plus metadata.

For example conceptually:

```text
byte[] compressedBytes

format

width
height

source

capture metadata

timings if telemetry enabled
```

Do NOT return Base64 from the domain/capture layer.

Base64 is transport serialization.

---

## M1 dependency rule

`Nexus.Capture` must NOT reference:

```text
System.Net

HttpListener

MCP server types

Python bridge types

Unity.Pipeline
```

It may obviously depend on Unity Editor/Engine APIs required for capture.

---

## Existing HTTP adapter

Current screenshot endpoint becomes:

```text
parse HTTP/MCP request
        ↓
create CaptureRequest
        ↓
ICaptureGateway.Capture...
        ↓
CaptureResult
        ↓
Base64 / JSON serialization
        ↓
existing response
```

External behavior should remain unchanged.

---

## M1 acceptance criteria

Must verify:

* exact current API response schema unchanged;
* same Game View semantic output;
* same Overlay UI behavior;
* same JPEG/PNG options;
* same DriverOwnedReadback;
* no new synchronous wait;
* no meaningful performance regression;
* Capture assembly contains no networking types;
* Legacy screenshot tests still pass.

Performance acceptance:

do not chase exact microseconds.

Expect measurements within normal benchmark noise versus M0.

---

# M2 — CANONICAL COMMANDS + DUAL REGISTRATION

Only begin after M1 is clean.

---

## Introduce Nexus.Contracts

Create one canonical command description model.

Each Nexus-owned high-level command should eventually exist as one command handler + one metadata definition.

Start with ONLY the proven hybrid POC set:

```text
nexus.project_map

nexus.group_compile_errors

nexus.capture_game_view
```

Do not migrate 117 tools at once.

---

## Legacy projection

Existing Nexus MCP exposes canonical command descriptors.

---

## Pipeline projection

When Pipeline package is available:

register equivalent `[CliCommand]` wrappers.

They must call the SAME Nexus handler/business logic.

Do not duplicate implementation.

---

## M2 command parity

For each first migrated command verify:

```text
same semantic arguments

same underlying business logic

equivalent structured result

equivalent error semantics
```

Transport envelopes may differ.

---

## M2 acceptance criteria

* HTTP path unchanged;
* Pipeline optional;
* no hard package dependency;
* all three POC commands callable through persistent `unity mcp`;
* same handler reached from both adapters;
* command descriptions originate from one source;
* disabling Pipeline registrar leaves Legacy completely functional.

---

# M3 — TRANSPORT / RUNTIME ABSTRACTION

After dual registration is proven.

---

## Goal

Make transport adapters explicit first-class modules.

Conceptual:

```text
Nexus.Transport.Legacy
Nexus.Transport.UnityPipeline
```

Both feed canonical Nexus command handlers.

---

## Legacy adapter

Owns:

```text
HttpListener

current MCP protocol

port 8081 compatibility

Legacy connection lifecycle

Base64/JSON
```

---

## Pipeline adapter

Owns:

```text
[CliCommand] projection

Pipeline availability

unity mcp compatibility

Pipeline-specific serialization/adaptation

Pipeline capability metadata
```

---

## Do not leak adapters upward

Application/domain handlers may not reference:

```text
HttpListener
Unity.Pipeline
MCP protocol DTOs
```

---

## M3 backend settings

Introduce:

```text
legacy
pipeline
auto
```

But default remains:

```text
legacy
```

until M4.

---

## M3 acceptance

Test:

### Pipeline available

Explicit `pipeline` works.

### Pipeline unavailable

`legacy` works exactly as today.

### auto

Still preserves compatibility behavior according to current milestone policy.

### broken Pipeline

Nexus can recover/fallback where policy allows.

### multiple Editors

Pipeline projects do not collide on Nexus's historical 8081 assumption.

---

# M4 — HYBRID DEFAULT FOR ELIGIBLE INSTALLS

DO NOT START UNTIL PRE-M4 GATES PASS.

At M4:

```text
auto
```

may prefer Pipeline.

---

## Eligible install

Conceptually requires:

```text
supported Unity version

compatible Pipeline package

working Pipeline server

persistent MCP-capable client

required Nexus command registration healthy
```

---

## Pipeline default does NOT remove HTTP

This distinction is mandatory.

M4:

```text
Pipeline primary
Legacy fallback
```

NOT:

```text
delete Legacy
```

---

## M4 user behavior

Modern capable installations:

```text
agent
→ unity mcp
→ Nexus [CliCommand]
→ Nexus Application
```

Old installations:

```text
agent
→ Nexus MCP/HTTP
→ Nexus Application
```

Same business logic.

Same Capture.

---

## M4 acceptance

* `core` Nexus profile usable entirely without binding HTTP;
* Capture V2 still returns Overlay UI;
* project_map works;
* grouped diagnostics works;
* domain reload recovers;
* two Editor projects route correctly;
* docs clearly explain both backends;
* user can force Legacy instantly.

---

# M5 — LEGACY TRANSPORT DEPRECATION

This is NOT removal.

Only mark Legacy backend deprecated after ALL deprecation gates pass.

---

# 10. LEGACY REMOVAL / DEPRECATION GATES

All major gates are required.

---

## Gate 1

Unity CLI reaches stable GA:

```text
>= 1.0 stable
```

not beta.

---

## Gate 2

Pipeline is no longer experimental,

or Unity ships an officially supported equivalent.

---

## Gate 3

Custom command API is stable across at least one relevant release line.

---

## Gate 4

Supported Unity version matrix is addressed.

Either:

* every supported Nexus Unity version has Pipeline;

or:

* Legacy remains for unsupported versions;

or:

* Nexus formally drops those Unity versions.

No silent abandonment.

---

## Gate 5

Domain reload lifecycle proven.

Required behavior:

* in-flight request fails predictably;
* next request after reload works;
* no manual reconnect normally required.

---

## Gate 6

Multi-editor operation proven.

Two projects:

* correctly isolated;
* correctly routed;
* no port collision.

---

## Gate 7

Warm Pipeline/MCP performance remains acceptable.

Capture and high-level commands must not regress materially versus Legacy.

Do not require Pipeline to beat Legacy.

Small overhead is acceptable in exchange for lifecycle/maintenance benefits.

---

## Gate 8

`unity shell` is NOT required for Nexus production operation.

Persistent `unity mcp` is the relevant supported path.

---

## Gate 9

Real adoption is sufficient.

Before removing Legacy:

either telemetry or real-world usage evidence should show most eligible users have transitioned,

OR publish a clear sunset schedule.

---

## Gate 10

Capture remains Nexus-owned even if official Unity capture improves.

Re-evaluate official capture only if it achieves Nexus semantic requirements:

* presented Game View;
* Overlay UI;
* acceptable stall;
* output control.

---

## Gate 11 — ADDITIONAL SAFETY GATE

After M4 makes Pipeline the default:

run at least ONE complete stable Nexus release cycle before removing Legacy.

During that cycle monitor:

* bug reports;
* reconnect problems;
* domain reload issues;
* Editor-version incompatibilities;
* multi-editor routing;
* capture failures.

Only after a stable release cycle should actual Legacy removal be considered.

---

# 11. CAPTURE IMPLEMENTATION DETAILS TO PRESERVE

Capture must remain transport-independent.

---

## Surface acquisition

Current fast Game View source:

```text
private GameView m_RenderTexture
```

Treat it as a temporary alias.

Validate it.

Immediately blit/copy into Nexus-owned RT.

Never retain it for the duration of async readback.

---

## Nexus-owned RT

Maintain separate conceptual roles:

```text
captureTargetRT

normalizeRT
```

Alias them only when compatible.

Normalization may include:

* resolution change;
* format;
* orientation;
* color conversion.

---

## Lifetime

The exact RT submitted to AsyncGPUReadback must:

```text
remain alive

remain immutable
```

until request completion/error.

---

## Concurrency

Do not invent a large pool/ring unless actual usage requires it.

Keep bounded screenshot concurrency.

Prefer:

```text
one active GPU screenshot
```

plus existing bounded queue/busy semantics.

---

## Completion

Poll via:

```text
EditorApplication.update
```

When request first reports completion:

```text
GetData

encode

compressed copy

complete TCS
```

same update.

---

## TCS

Use:

```csharp
TaskCreationOptions.RunContinuationsAsynchronously
```

Transport continuation must not inline:

```text
JSON

Base64

socket/HTTP writes
```

onto Unity main thread.

---

## Cancellation / reload

Never hang indefinitely.

Do not synchronously wait for GPU request during teardown.

Best-effort fault/cancel pending logical request.

Do not destroy/reuse the submitted RT while GPU request is pending.

---

# 12. ERROR MODEL

Standardize domain errors before projecting to transports.

Conceptually:

```text
NexusError
  code
  category
  message
  context
  recoverable
```

Examples:

```text
GameViewUnavailable

CaptureBusy

ReadbackFailed

UnsupportedFormat

PipelineUnavailable

CommandUnavailable

DomainReloadInterrupted
```

Transport adapters decide how to serialize them.

Do NOT create unrelated error formats for Legacy and Pipeline.

---

# 13. OBSERVABILITY

Keep lightweight telemetry.

Important capture metrics:

```text
source_acquire_ms

readback_submit_ms

submit_to_done_ms

encode_ms

main_thread_stall_ms

total_internal_ms

output_bytes

dimensions

format

fallback_used

success/error
```

Do not log absurd false precision.

For very tiny operations below timer reliability, report:

```text
< reliable threshold
```

where appropriate.

---

# 14. PUBLIC PERFORMANCE CLAIMS

Do not resurrect old retracted claims.

NEVER publish:

```text
177x faster than Unity

zero-copy driver ring buffer

hardware JPEG

97.4% less GC

Q85 has identical VLM comprehension
```

without new valid evidence.

Current safer messaging:

```text
Nexus uses asynchronous GPU readback and agent-oriented image encoding.

On the validated Apple Silicon / Metal / Unity 6000.4.3f1 setup,
the capture path produced approximately 1.6 ms median Editor stall.

Nexus captures presented Game View content including Screen Space Overlay UI.

JPEG/PNG and resolution are configurable.
```

Always scope benchmarks to the tested environment.

---

# 15. OFFICIAL UNITY COMMAND USAGE

Do not reproduce commodity official commands unless Nexus adds meaningful semantics.

If Unity already provides a good atomic:

```text
set_transform

play mode

basic selection

test execution

build
```

then Nexus high-level workflows may CALL or depend on those primitives in Pipeline mode.

Legacy mode may keep compatibility equivalents.

The goal is to reduce long-term duplicated infrastructure.

---

# 16. HIGH-LEVEL NEXUS TOOLS

Prioritize Nexus tools that reduce agent reasoning/work.

Examples:

```text
project_map

prepare_context

find_relevant_files

group_compile_errors

inspect_unity_references

analyze_logs

visual verification

capture_game_view
```

A useful Nexus command should ideally:

```text
replace multiple low-level calls

reduce irrelevant output

produce agent-ready structure
```

This is the product differentiator.

---

# 17. REPOSITORY STRUCTURE

Map current files into conceptual ownership.

Do not perform a huge physical directory move merely for aesthetics.

Refactor incrementally.

Target ownership might resemble:

```text
Assets/NexusUnity/

  Editor/
    Contracts/
    Application/
    Capture/
    Runtime/
      Legacy/
      Pipeline/
    Commands/
    Compatibility/
```

Use repository conventions if a better structure already exists.

Avoid file churn that provides no architectural value.

---

# 18. OPTIONAL PIPELINE DEPENDENCY

Until M4:

do NOT force:

```text
com.unity.pipeline
```

as a mandatory dependency for every Nexus installation.

Use optional integration.

Potential mechanisms:

* optional asmdef;
* compile defines;
* reflection/TypeCache registration;
* separate integration assembly/package layer.

Choose the cleanest compatible mechanism for the current repo.

The requirement is:

```text
Nexus must compile and run when Pipeline is absent.
```

---

# 19. TESTING STRATEGY

Do not create another benchmark research suite.

Tests now serve regression/acceptance.

---

## M1 regression

Verify:

* same HTTP schema;
* same output;
* Overlay UI;
* JPG;
* PNG;
* invalid Game View errors;
* cancellation;
* second request behavior.

---

## M2 parity

Same Nexus handler invoked through:

```text
Legacy

Pipeline
```

Compare semantic result, not byte-identical transport envelope.

---

## M3 compatibility

Test:

```text
Pipeline absent

Pipeline present

Pipeline unavailable/broken

two Editors

domain reload
```

---

## M4 integration

Test actual persistent:

```text
unity mcp
```

Do NOT test production latency by repeatedly spawning cold:

```text
unity command
```

---

# 20. DOCUMENTATION CHANGES

Documentation should evolve with milestones.

---

## M1

No user-facing architecture migration announcement necessary.

Internal architecture docs only.

---

## M2

Document experimental Pipeline integration.

Make clear:

```text
optional
```

---

## M3

Document runtime setting:

```text
legacy
pipeline
auto
```

---

## M4

Update recommended setup for capable Unity versions.

Still document Legacy fallback.

---

## M5

Publish deprecation timeline before any removal.

---

# 21. PRODUCT POSITIONING

Use this strategic positioning:

> Nexus Unity is the agent intelligence, context, and visual layer for Unity — providing curated project understanding, diagnostics, and editor-faithful capture while using the official Unity execution stack when available.

Do NOT position Nexus as:

```text
a better MCP server than Unity
```

Unity transport becomes infrastructure.

Nexus value is:

```text
context
curation
visual truth
agent workflows
```

---

# 22. EXECUTION RULES

Implementation should proceed:

```text
M0
↓
M1
↓
M2
↓
M3
↓
M4
↓
M5
```

Do NOT skip directly to M4.

Do NOT delete working Legacy infrastructure during M1–M3.

Do NOT rewrite all 117 Nexus tools during M2.

Do NOT add all 151 Unity tools to Nexus.

Do NOT make Pipeline mandatory before maturity gates.

---

# 23. REQUIRED IMPLEMENTATION REPORT AFTER EACH MILESTONE

After completing a milestone report:

## Changed

Files/modules created or modified.

## Architecture

What dependency moved where.

## Compatibility

What remained unchanged.

## Tests

What was run and passed.

## Regressions

Any behavior/performance difference.

## Technical debt

Anything temporarily retained intentionally.

## Next milestone readiness

Explicit:

```text
READY FOR M<N+1>
```

or:

```text
BLOCKED
```

with concrete reason.

Do not begin a destructive next milestone while current acceptance criteria fail.

---

# 24. YOUR IMMEDIATE TASK

START WITH M1 ONLY.

Do not implement M2 yet.

Your immediate engineering task:

> Extract Capture V2 into a transport-independent `Nexus.Capture` subsystem behind `ICaptureGateway`, and route the existing Legacy HTTP/MCP Game View capture endpoint through it without changing its external contract.

Required result:

```text
Legacy HTTP/MCP
      ↓
thin transport adapter
      ↓
ICaptureGateway
      ↓
Nexus.Capture
      ↓
Unity
```

The current external screenshot API must continue working unchanged.

No Pipeline dependency should be introduced during M1.

---

# 25. M1 DEFINITION OF DONE

M1 is complete only when ALL are true:

* existing Game View capture goes through ICaptureGateway;
* Capture implementation contains no HTTP/MCP/Python/network concerns;
* production still uses AsyncGPUReadback.Request + GetData;
* Overlay UI behavior remains correct;
* JPEG works;
* PNG works;
* configurable quality works;
* normalize/downscale path remains available;
* Legacy response schema unchanged;
* no new main-thread synchronous wait;
* no Pipeline dependency;
* existing screenshot tests pass;
* newly added capture abstraction tests pass;
* capture errors still terminate requests predictably;
* performance remains within reasonable normal noise of current baseline.

Then STOP.

Return the milestone report.

Do not start M2 until M1 has been reviewed.
