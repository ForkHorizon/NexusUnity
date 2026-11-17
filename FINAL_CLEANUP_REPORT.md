# Nexus Unity 1.7.0 Final Cleanup Report

Date: 2026-09-22  
Branch: `rework/T01`  
Status: **READY FOR INTERNAL MERGE**

## Release identity note

The latest released tag is `v1.6.0`; this cleanup is being performed on the subsequent `1.7.0` development line. Package metadata and public documentation now identify `1.7.0` as the current development target; `v1.7.0` has not been released. This report does not create a release tag.

## A. Support matrix

| Environment | Support | Transport/behavior |
|---|---:|---|
| Unity `<6000.0` | No | Unsupported for Nexus Unity 1.7.0. |
| Unity `6000.0+`, no healthy `com.unity.pipeline` | Yes | Legacy HTTP/MCP compatibility path. |
| Unity `6000.0+` with healthy `com.unity.pipeline` | Yes | Pipeline is preferred automatically. |
| Validated Pipeline stack | Yes | Unity `6000.4.3f1` with `com.unity.pipeline@0.7.0-exp.1`. |

Python 3 is required for MCP bridge integrations. Pipeline remains optional and experimental.

## B. Legacy transport decision

**LEGACY RETAINED AS REQUIRED COMPATIBILITY.**

Nexus Unity 1.5 officially supports Unity 6000.0+ projects where `com.unity.pipeline` is absent, unavailable, or unhealthy. Legacy remains the minimal compatibility backend. Canonical command and Capture logic are transport-independent, so Legacy can be removed later only after documented maturity gates are met.

## C. Final inventory

| Area | Classification | Result |
|---|---|---|
| `Editor/Runtime/LegacyTransportAdapter.cs` | Compatibility | Retained. |
| HTTP auth, token rotation, and port 8081 ownership | Compatibility | Retained. |
| Python `_transport.py` | Compatibility/general client | Retained. |
| `capture_game_view_screenshot` | Compatibility API | Retained and routed through Capture V2. |
| `nexus_*` aliases | Dispatch compatibility | Retained as hidden aliases. |
| `Editor/Pipeline/` | Optional production | Retained behind package/asmdef gating. |
| `Editor/Capture/DriverOwnedReadback.cs` | Production | Retained. |
| `Editor/Capture/EditorWindowPixelCapture.cs` | Production | Retained for Inspector/editor windows only. |
| Old Game View V1 `ReadPixels` | Dead | Absent from production Game View capture. |
| `NexusOverlayVerify.cs` | Dead | Removed; it was unused and forced an undeclared UI dependency. |
| `Tests~/Editor` | Duplicate source | Removed; `Tests/Editor` is canonical. |
| `Research~/` | Research | Kept outside production compilation. |
| Captures/results | Generated | Removed and ignored. |

The command inventory is clean: one handler, input, output, and description per canonical command. The final schema snapshot contains 120 visible names, 3 hidden aliases, a 29,160-byte UTF-8 tools array, and core/visual/scene/compat profiles.

## D. Removed or relocated material

- Removed `Editor/Capture/NexusOverlayVerify.cs` and its `.meta` file.
- Removed the old `Tests~/Editor` duplicate suite.
- Relocated the stale root handoff to `Research~/docs/ARCHITECTURE_MIGRATION_HANDOFF_HISTORICAL_2026-09-22.md`.
- Removed local generated capture images.

## E. Retained compatibility details

The Legacy listener, authentication and 8081 ownership, Python HTTP transport, compatibility aliases, and the old screenshot schema adapter remain because supported Unity environments can lack a healthy Pipeline installation. These are compatibility surfaces, not duplicate production command implementations.

## F. Architecture

```text
Legacy HTTP / Python bridge ─┐
                             ├─> canonical commands / CaptureGateway
Unity Pipeline wrappers ─────┘                         │
                                                       ├─ Game View source
                                                       ├─ DriverOwnedReadback
                                                       └─ PNG/JPEG encoder
```

## G. Validation results

### Parent interactive baseline

- Discovered: 152
- Executed: 152
- Passed: 152
- Failed: 0
- Ignored/skipped/inconclusive: 0

### Clean-checkout batch run

- Discovered: 152
- Passed: 151
- Failed: 0
- Inconclusive: 1 — visible Game View is unavailable in batch mode.

### Static and package checks

- Python tests: 43 passed.
- Quality-gate errors: 0.
- Meta pairing: passed.
- Existing size warnings: 5.

## H. Clean-checkout verification

- Clean checkout imports successfully.
- Production and Pipeline assemblies compile with Pipeline installed.
- A no-Pipeline checkout compiles with zero C# errors and no Pipeline assembly.
- Canonical `Tests/Editor` tests are discovered.
- No generated JSON or untracked C# source is required for the package.

## I. Live smoke evidence

Accepted evidence includes:

- Pipeline `project_map`, `group_compile_errors`, JPEG, PNG, and 1600×900 capture checks passed.
- Explicit Pipeline mode did not fall back to HTTP.
- Auto mode selected Pipeline when eligible.
- Legacy mode selected Legacy and compatibility calls passed.

## J. Multi-editor and reload checks

- Multi-editor validation passed with ports 7800 and 7801; port 8081 had no bind fight and routing was correct.
- Domain reload validation passed 20/20 with zero hangs.

## K. Documentation

`README.md`, `DOCUMENTATION.MD`, `API_REFERENCE.MD`, and `CHANGELOG.md` are aligned with the current public behavior. Historical reports are under `Research~/docs`; the stale handoff was relocated and historical claims are labeled. Retracted claims were removed or marked historical. API documentation describes 120 visible tools and the hidden compatibility aliases.

## L. Repository cleanliness

The working branch is `rework/T01` and intentionally remains dirty/unstaged; no commit or push was performed. The remaining changes are package implementation, documentation, tests, research, and validation artifacts. Temporary captures, scratch JSON, generated benchmark output, duplicate test folders, and the stale root handoff are absent.

## M. Known debt

- CLI/Pipeline are beta/experimental; Legacy cannot yet be removed.
- Five existing quality size warnings remain.
- Batch mode cannot validate visible-window capture.
- Existing parent-harness prefab warnings are outside this package.

Deleted Legacy or duplicate implementations are not debt.

## N. Final disposition

**READY FOR INTERNAL MERGE**
