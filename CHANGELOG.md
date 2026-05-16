# Changelog - Nexus Unity

All notable changes to Nexus Unity are documented here.

## [Unreleased] - Open Source Release Preparation

### Changed
- Renamed the Unity package id to `com.forkhorizon.nexus.unity`.
- Repositioned package documentation around standalone Nexus Unity usage.
- Removed references to non-public product planning from package docs.
- Updated the Python bridge to prefer the live Unity `list_tools` catalog and keep its offline fallback aligned with Unity's public tool list.

### Fixed
- Registered the previously listed `set_enabled` RPC handler.
- Registered public wait handlers for `wait_for_asset_import_idle` and `wait_for_editor_idle`.
- Added missing public tool schema entries for `shutdown_server`, `find_references`, and `ui_query_elements`.
- Removed the debug-only `test_coroutine` handler from the public dispatch registry.

### Added
- Added GPLv3 license notice.
- Added editor tests that fail when Unity `list_tools`, RPC dispatch registration, and Python bridge fallback drift apart.

### Removed
- Removed generated Graphify cache files from the public package.
- Removed previous experimental Mojo bridge artifacts from the public package.

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
