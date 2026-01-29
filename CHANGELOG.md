# Changelog - NexusUnity

All notable changes to the `NexusUnity` library will be documented in this file.

## [1.5.2] - 2026-01-29

### Fixed
- **Non-Blocking Scene Transitions**: Refactored `OpenScene` and `CreateScene` to automatically save the current scene to a default path if modifications exist, preventing blocking Unity dialogs.
- **Safer Property Serialization**: Refactored `InspectComponent` to use `SerializedObject` traversal, resolving crashes on internal Unity engine handles.
- **Component Update Fix**: Switched `UpdateComponent` to `EditorJsonUtility` for reliable engine-type modification.

## [1.5.1] - 2026-01-29

### Fixed
- **Dispatcher Routing**: Resolved critical bug where `Hierarchy`, `Asset`, and several `Editor` methods (e.g., `add_component`, `read_file`) were unreachable due to rigid prefix matching.
- **Asset Persistence**: Added mandatory `AssetDatabase.SaveAssets()` calls after `CreateMaterial`, `CreatePrefab`, and `ApplyPrefabOverrides` to ensure changes are written to disk.
- **Dispatch Reliability**: Refactored the core dispatcher with explicit capability checks to prevent cascading failures.

## [1.5.0] - 2026-01-28

### Added
- **Gap Closure Release**: 17 new JSON-RPC methods expanding toolset to 59 total.
- **Hierarchy Manipulation**: `get_children`, `duplicate_object`, `set_active`, `set_enabled`, `remove_component`, `set_sibling_index`.
- **File I/O**: `read_file`, `write_file` for direct project file manipulation.
- **Asset Management**: `move_asset`, `delete_asset`, `copy_asset`, `get_dependencies`, `create_folder`.
- **Editor State**: `get_editor_state`, `pause_play_mode`, `step_frame`, `get_project_info`.
- **Architectural Scaling**: Dispatcher refactored into tiered sub-methods for maintainability.

## [1.4.0] - 2026-01-28

### Added
- **AI Power Tools Expansion**: 14 new JSON-RPC methods to enable 100% AI-driven workflows.
- **Advanced Discovery**: New `find_objects`, `get_object_path`, `list_scenes`, and `get_tags_and_layers` methods.
- **Editor Control**: Added `undo`, `redo`, `toggle_play_mode`, and `execute_menu_item`.
- **Selection & View**: New `set_selection`, `focus_scene_view`, and `ping_object` for improved visual feedback.
- **Surgical Edits**: New `set_property` method for precise manipulation of single values (bool, int, float, string, Vector3).
- **Prefab Lifecycle**: New `create_prefab`, `apply_prefab_overrides`, and `revert_prefab_overrides`.
- **Architectural Scaling**: Refactored the core dispatcher into categorized sub-methods to maintain clean code standards.

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
