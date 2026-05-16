# Changelog - Nexus Unity

All notable changes to Nexus Unity are documented here.

## [Unreleased] - Open Source Release Preparation (Deep Consolidation Phase)

### Added
- **Deep Tool Consolidation:** Groups 64+ granular tools into **14 core entry points**. New managers include `search_manager`, `asset_manager`, `editor_controller`, and `ui_automation`.
- **Strict JSON Schemas:** Implemented `oneOf` logic for all Managers, ensuring the AI cannot provide invalid parameters for a specific action.
- **Integrated Dev Macro:** Renamed `apply_code_change` to `write_and_compile` to reinforce the recommended autonomous development workflow.

### Changed
- Refactored Python bridge with unified `route_tool` logic for consistent CLI and MCP behavior.
- Updated API Reference to v2.8.0.
- Synchronized all root documentation with the `Assets/NexusUnity` package.

### Removed
- Removed 50+ redundant micro-tools from the public schema to minimize LLM token overhead and analysis paralysis.
- Removed `write_files_batch` to prevent race conditions during domain reloads.


## [3.1.2] - 2026-05-02

### Added
- Project-wide knowledge graph support for development-time indexing.
- `unity_knowledge_graph` for querying code structure.

### Fixed
- Resolved `AmbiguousMatchException` in `EntityId` conversion by filtering reflection candidates by return type.
- Fixed Unity 6 scene validity checks for the updated `Scene` struct.

### Optimized
- Reworked renderer and material list pooling in scene health scanning to reduce GC pressure.
- Restored optimized UI Toolkit element lookup paths.

## [3.1.1] - 2026-04-30

### Added
- Payload limiting for HTTP and WebSocket requests.
- Loopback host and origin validation.
- Additional server health, state, and session reporting.

### Improved
- Reduced large-payload allocations in JSON-RPC parsing.
- Improved path construction and component cache reuse during scene traversal.
- Improved dashboard clarity for server control and diagnostics.

### Fixed
- Updated Unity 6 object change handling for modern `EntityId` APIs.
- Improved input simulation reliability during Play Mode.
- Improved dynamic type discovery after script compilation.

## [3.1.0] - 2026-04-18

### Added
- `unity_batch_execute`
- `unity_scene_delta`
- `unity_symbol_index`
- `unity_component_values`
- `unity_compact_scene_snapshot`

### Improved
- Added structured log reading support.
- Added field filtering to component inspection.

## Earlier History

Earlier development history is intentionally collapsed for the first public release. Public releases should keep complete changelog entries from this point forward.
