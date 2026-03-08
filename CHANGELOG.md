# Changelog - NexusUnity

All notable changes to the `NexusUnity` library will be documented in this file.

## [2.4.0] - 2026-03-09

### Added
- **Native macOS App Nap Bypass**: Implemented a dual-layer anti-throttle system: (1) a native `NSProcessInfo` integration via P/Invoke (`AppNapBypass.cs`) that disables macOS App Nap, and (2) a `delayCall` heartbeat that calls `QueuePlayerLoopUpdate` to keep Unity's editor loop responsive.
- **Hybrid Deep Auditor**: Upgraded `unity_lint_project` into a comprehensive project health tool. It now integrates the official **Unity Project Auditor** for deep static code analysis (identifying memory leaks and performance bottlenecks) and a custom **Nexus Scene Scanner** that detects missing scripts, broken prefabs, and "pink" (error) materials.
- **Intuitive JSON Payloads**: Upgraded `unity_update_component` to accept a native JSON `properties` object instead of a stringified payload. It now seamlessly accepts raw JSON arrays for assigning Unity `List<>` or arrays.
- **Fuzzy Property Matching**: The `unity_update_component` tool now automatically maps AI-friendly property names (e.g., `sprite`, `myList`) to Unity's internal serialization backing fields (e.g., `m_Sprite`, `_myList`, `m_MyList`), eliminating the need to guess internal naming conventions.
- **Native OS Focus Synchronization Loop**: Autonomous scripts now wait for `isApplicationActive` to become true before executing `AssetDatabase.Refresh()`, perfectly respecting Unity's background Domain Reload safety locks.
- **Persistent App Path Tracking**: The Unity Editor location is now cached in `SessionState`, ensuring that focus-steal commands (`open -a`) remain functional even after multiple Domain Reloads.

### Fixed
- **macOS AppleEvent Focus Blocking**: Replaced the restricted `osascript` application activation with the native LaunchServices `open -a` command. This forcefully bypasses macOS Monterey+ security that was silently suppressing background focus steals, ensuring 100% reliable 0-touch script compilation.
- **Double-Compile FSEvent Desync**: By utilizing the canonical `AssetDatabase.Refresh()` synchronously with OS focus, the FSEvents queue is cleanly consumed natively, permanently solving the bug where Unity would redundantly re-compile scripts when the user manually clicked the window later.

### Changed
- **UI Decoupling**: Removed the persistent "Nexus" progress bar in the Unity Editor. App Nap is now entirely handled at the OS-level via `NSProcessInfo`, eliminating redundant UI-level background activity indicators.

## [2.3.0] - 2026-03-08

### Added
- **UXML Deep-Query**: Added the `unity_ui_query_elements` tool to search UI Toolkit hierarchies by text content (e.g., finding a button labeled "Buy") or CSS classes, returning full computed styles and layout dimensions.
- **Enhanced UI Serialization**: `unity_ui_get_hierarchy` now automatically includes `text` and `classes` for all VisualElements, with an optional `deep` parameter to expose full computed layouts.
- **Scriptless Method Invoker**: Introduced `unity_invoke_method` which uses C# reflection to let the AI dynamically trigger any method (public or private) on GameObjects and Components during Play Mode without writing temporary test scripts.
- **Scene Reference Finder**: Added `unity_find_references` tool to safely query which GameObjects and Assets depend on a specific Asset GUID or Instance ID, enabling safe refactoring and deletion analysis.
- **Sub-Asset Identification**: The `unity_explore_asset` tool now returns the `instance_id` for every sub-asset found, allowing for immediate session-based referencing.
- **Persistent Object Referencing**: Upgraded `unity_update_component` to support persistent object assignment using a `{ "guid": "...", "file_id": ... }` object format. This enables the AI to safely link sub-assets like sliced sprites across different machines and sessions.

### Changed
- **Tool Semantic Renaming**: Renamed `get_asset_metadata` to `explore_asset` and updated its description to improve AI discoverability for exploring internal file contents like sliced sprite sheets.

## [2.2.0] - 2026-03-07

### Added
- **Autonomous Core Service**: Decoupled the MCP Server from the Editor Window lifecycle. The server now runs as a standalone static service (`MCPServer.cs`) that initializes on domain load and persists through window closures.
- **Project-Specific Persistence**: Implemented unique `EditorPrefs` keys based on project paths. The server now remembers its "Enabled/Disabled" state independently for every project.
- **Anti-Throttling Heartbeat**: Integrated `EditorApplication.QueuePlayerLoopUpdate()` into the dispatch queue. This ensures instantaneous responses even when the Unity Editor is in the background or macOS App Nap is active.
- **Recursive Serialization Engine**: Upgraded `unity_inspect_component` to recursively unpack Arrays, Lists, and Generic structs. It now correctly serializes deep data structures like `BoxCollider` center/size and custom serializable classes.
- **Extended Math Support**: Added full JSON serialization for `Vector2`, `Vector4`, `Rect`, and `Bounds` types.
- **Intelligent Asset Merging**: Enhanced `unity_move_asset` to automatically merge directories when the destination already exists. It now recursively moves all contents instead of failing with a "Destination already exists" error.
- **Sub-Asset Explorer**: Added `unity_explore_asset` tool to safely retrieve asset GUIDs and list all internal sub-assets and their `fileID`s (e.g., sliced sprites), eliminating the need for brittle `.meta` file parsing.

### Fixed
- **Serialized Field Blindness**: Resolved an issue where `unity_inspect_component` was blind to fields marked with `[HideInInspector]` or internal Unity fields. Replaced `NextVisible` with `Next` to ensure 100% data visibility.
- **OS Port Conflict Resilience**: Added an intelligent startup delay and OS-level socket verification to handle "Port already in use" errors during rapid Unity restarts.
- **Initialization Stability**: Fixed `TypeInitializationException` by moving forbidden Unity API calls out of static constructors and into safe `delayCall` phases.
- **Menu Item Conflict**: Resolved a GUI bug where the "Run Audit" menu item was swallowing the "Nexus Unity" dashboard link.

### Changed
- **Architectural Consolidation**: Merged redundant partial window files into a unified `MCPServerWindow.cs` for improved maintainability and compilation speed.
- **Zero-Interaction Reliability**: The server is now truly "Autonomous," requiring zero manual intervention to remain active across development sessions.
- **Bi-Directional Updates**: Enhanced `unity_update_component` to support writing values back to nested structs and arrays, enabling the AI to manipulate complex data models.

## [2.1.2] - 2026-03-02

### Fixed
- **Submodule Integrity**: Restored missing `MCPServerWindow.UI.cs.meta` file in the submodule, ensuring the asset is correctly imported when the library is added to other projects as a package.
- **Server UI Bug**: Fixed string interpolation bug in `MCPServerWindow.Server.cs` where the port number was not being correctly displayed in the console logs.

### Changed
- **Code Hygiene**: Added `#pragma warning disable 0618` across all core files to suppress obsolete `InstanceIDToObject` warnings, ensuring a cleaner console in Unity 2021.3+.
- **Architectural Cleanup**: Removed unused fields and refactored compilation tracking in `MCPServerWindow.cs` to reduce redundant state updates.

## [2.1.1] - 2026-03-01

### Fixed
- **HTTP Origin Security**: Implemented strict `Origin` header validation for standard HTTP requests to prevent Cross-Site Request Forgery (CSRF) and DNS Rebinding attacks.
- **Linter Compliance**: Refactored request handling logic in `MCPServerWindow.Server.cs` to maintain method length limits (< 40 lines).

### Performance
- **Search Optimization (Bolt)**: Optimized `unity_find_objects` by eliminating N+1 component allocations. It now uses `Resources.FindObjectsOfTypeAll(type)` for targeted discovery and pre-instantiated local `Regex` objects, significantly improving performance in large projects.

### Added
- **Transient UI Notifications (Palette)**: Integrated `ShowNotification` for key developer actions (Server Start/Stop, Clear Logs, CLI Linking). This provides immediate visual feedback in the Unity Editor without cluttering the console.

## [2.1.0] - 2026-02-27

### Added
- `unity_get_component_schema`: Returns names and types of serializable fields for a component.
- `unity_create_hierarchy`: Batch create a full GameObject hierarchy from a JSON tree.
- `unity_find_by_path`: Search for GameObjects using their hierarchy path (e.g., "Canvas/Main/Title").
- `unity_wait_for_ready`: Simple tool to poll for server responsiveness.

### Changed
- `unity_update_component`: Now returns a detailed result object with `status` ("Success", "Partial", "Failed"), `updated_count`, and a list of `errors` for individual fields.

## [2.0.6] - 2026-02-24

### Fixed
- **Inspector Object Linking**: Added support for `ObjectReference` in `unity_update_component`. AI agents can now link materials, textures, and other game objects in the Inspector by providing an InstanceID or asset path.
- **Enhanced Property Inspection**: Improved `unity_inspect_component` to correctly return metadata (ID, name, type) for linked objects instead of just a generic string.

## [2.0.5] - 2026-02-24

### Added
- **Global Context Capture**: Initial release of the `read_logs` and `get_editor_state` tools.
- **Object Manipulation**: Basic support for `create_primitive`, `set_transform`, and `destroy_game_object`.
- **Core JSON-RPC Engine**: Native C# implementation of the Model Context Protocol.
