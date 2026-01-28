# Changelog - NexusUnity

All notable changes to the `NexusUnity` library will be documented in this file.

## [1.3.0] - 2026-01-28

### Added
- **Comprehensive API Expansion**: Documented and stabilized 28 JSON-RPC methods covering:
  - **Scene Management**: `open_scene`, `create_scene`, `save_scene`.
  - **GameObject Logic**: `create_game_object`, `destroy_game_object`, `set_transform`, `set_parent`.
  - **Component Interaction**: `add_component`, `inspect_component`, `update_component`.
  - **Asset Management**: `list_assets`, `create_material`, `refresh_asset_database`, `import_asset`.
  - **Prefab Support**: `instantiate_prefab`.
- **Modular Partial Classes**: Refactored `MCPServerMethods` and `MCPServerWindow` into modular files (Scene, Asset, UI, Component, Core, etc.).
- **Universal AI Diagnostic Tools**: Integrated robust, project-independent diagnostic and linting scripts in the `AI/` folder.
- **Enhanced Linter**: Improved method length detection logic to support non-public methods and standard Unity lifecycle functions.
- **Full XML Documentation**: Comprehensive documentation for all public APIs across the modularized library.

### Fixed
- Fixed missing assembly references for `EditorCoroutines`.
- Resolved naming convention violations and method length issues.
- Optimized UI verification scripts and error handling.

## [1.2.0] - 2026-01-26

### Added
- **Console Log Capturing**: Added ability to capture Unity console logs in real-time.
- **Log API**: New JSON-RPC methods `read_logs` and `clear_logs` for external log retrieval and management.
- **Port Update**: Default port moved to `8081` to avoid common local development conflicts.

## [1.1.0] - 2026-01-26

### Changed
- **Architectural Split**: Separated `MCPServerWindow.cs` into `MCPServerWindow.cs` (Lifecycle/UI) and `MCPServerMethods.cs` (Logic) for better maintainability and compliance with file length standards.
- **JSON Engine**: Integrated `Newtonsoft.Json` (com.unity.nuget.newtonsoft-json) for more robust and flexible JSON-RPC 2.0 processing.
- **Performance**: Optimized main-thread dispatching using `ManualResetEventSlim`.

### Fixed
- Resolved compilation issues caused by missing assembly references.
- Fixed thread safety issues in server-to-main-thread communication.
- Corrected naming convention violations across the codebase.

### Added
- Comprehensive XML documentation for all public APIs.
- Support for dynamic script attachment with `SessionState` persistence across assembly reloads.

## [1.0.0] - 2026-01-25

### Added
- Basic MCP Server implementation with support for `initialize`, `create_primitive`, and `attach_script`.
- Internal main-thread task queue.
- Basic Editor GUI for server control.
