# Changelog - NexusUnity

All notable changes to the `NexusUnity` library will be documented in this file.

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
- **CLI Dual Mode**: Upgraded `nexus_unity_bridge.py` to support direct command-line execution of Unity tools. This allows AI agents and developers to run commands like `python3 nexus_unity_bridge.py unity_refresh_asset_database` without needing a persistent MCP session.
- **Version Synchronization**: Synchronized version `2.0.5` across `package.json`, `MCPServerMethods.Core.cs`, and `nexus_unity_bridge.py` to ensure consistent reporting.

## [2.0.4] - 2026-02-17

### Added
- **Hybrid Bridge Discovery**: Implemented static tool definitions in `nexus_unity_bridge.py`. This ensures that all 61+ tools are always visible to the Gemini CLI, even if the Unity Editor is temporarily offline or busy.
- **Improved Error UX**: The bridge now provides a clear, actionable error message if a tool is called while the Unity MCP server is not running.

## [2.0.3] - 2026-02-17

### Fixed
- **IPv4 Connectivity**: Changed bridge script's `UNITY_URL` from `localhost` to `127.0.0.1` to prevent "Bad Request" errors caused by IPv6 address resolution (`::1`) on systems where the Unity server is bound to IPv4 loopback.
- **Dynamic Version UI**: Implemented live versioning in the Unity Editor dashboard and window title (v2.0.2/v2.0.3).

## [2.0.2] - 2026-02-17

### Fixed
- **CLI Documentation Linking**: Fixed a critical bug in `MCPCliInstaller` where documentation links in `NEXUS_UNITY_DOCS.md` were broken when the library was installed via Package Manager. 
- **Automatic Doc Deployment**: The installer now automatically copies `API_REFERENCE.MD` and `DOCUMENTATION.MD` to the project root, ensuring they are always accessible to AI agents.
- **Library Root Discovery**: Implemented a robust traversal logic to correctly locate the library root across both standard `Assets` and `Library/PackageCache` installations.

## [2.0.1] - 2026-02-15

### Added
- **Deterministic C# Linter (The Daily Auditor)**: Introduced `ProjectAuditor.cs`, a robust, code-aware linter that replaces brittle regex-based bash scripts. It performs deep structural analysis of C# code, including:
    - **Nesting Depth**: Detects complex "arrow code" (> 5 levels).
    - **Code Complexity**: Calculates cyclomatic complexity for method bodies.
    - **Accurate Length Checks**: Validates file and method lengths by ignoring comments and whitespace.
    - **Naming Compliance**: Enforces strict `_camelCase` for private fields.
- **MCP Linting Tool**: Added `unity_lint_project` to the MCP toolset, allowing external AI agents to trigger a full project audit and receive a structured `LINT_REPORT.txt`.

## [2.0.0] - 2026-02-14

### Major Integration Release
This major release marks the successful consolidation of 35+ feature and architectural enhancements (Pull Requests #20 through #55), finalizing the library's position as a robust, AI-native control protocol for Unity.

### Fixed
- **Main Thread Synchronization**: Added `[InitializeOnLoad]` to `MCPServerMethods` to reliably capture the Unity Main Thread ID, preventing deadlocks when the server is first accessed via background network threads.
- **UI Verification Robustness**: Enhanced `UIVerification` and `MCPTestWindow` with explicit state reset logic and granular JSON-RPC error reporting.
- **Component Property Serialization**: Improved `unity_update_component` to use `SerializedProperty` for surgical field edits, adding support for `Enum`, `Color`, and `Vector3` types.

### Key Milestones Integrated in v2.0.0:
- **Streaming JSON-RPC Utility**: High-performance, zero-allocation request processing using `TextReader`.
- **Security Hardening**: Strict path validation for `unity_read_file`/`unity_write_file` and protection against sibling-directory traversal.
- **Architectural Scalability**: Full refactor into modular partial classes with an O(1) constant-time dispatcher.
- **Enhanced UX**: Unified tabbed dashboard in Unity with real-time connectivity status and copyable server URL.
- **Self-Healing Infrastructure**: Robust server persistence through Unity domain reloads and automatic recovery from network-level crashes.
- **Native CLI Bridge**: Integrated Python bridge for direct stdio communication with the Gemini CLI.


## [1.9.6] - 2026-02-03

### Fixed
- **Synchronous Asset Refresh**: Upgraded `unity_refresh_asset_database` to use `ForceSynchronousImport`. It now returns a detailed status object including `is_compiling` and `is_updating` flags. This prevents AI agents from proceeding before assets are fully processed by the engine.
- **Hierarchy Code Cleanup**: Resolved a formatting regression in `MCPServerMethods.Hierarchy.cs` and added `is_updating` to the editor state discovery.

## [1.9.5] - 2026-02-03

### Added
- **AI-Native API Reference**: Created `API_REFERENCE.MD` providing exhaustive, AI-friendly documentation for all 60+ JSON-RPC tools. Each command now includes clear descriptions, parameter requirements, and return types optimized for LLM context windows.
- **Documentation Overhaul**: Updated `DOCUMENTATION.MD` with a categorized tool overview and quick-reference index.

## [1.9.4] - 2026-01-29

### Fixed
- **Stale Process Prevention**: Added an "Orphan Monitor" to the Python bridge script. The bridge will now automatically detect if the Gemini CLI (its parent process) has terminated and will shut itself down immediately, preventing background stale processes.
- **Instance Conflict Protection**: Implemented a pre-startup check in Unity to detect if another Nexus Unity server is already running on the same port. This prevents port conflicts and ensures only one server instance is active at a time.

## [1.9.3] - 2026-01-29

### Improved
- **Reliable CLI Integration**: Implemented a "Stable Path" strategy for the Gemini CLI link. The installer now automatically deploys a copy of the bridge script to the project root. This ensures that Unity Package Manager updates or cache clears no longer break the terminal connection.
- **Clean Slate Linking**: The installer now automatically removes old `nexus-unity` registrations before adding the new one, preventing stale process conflicts.

## [1.9.2] - 2026-01-29

### Fixed
- **WebSocket Stability**: Implemented robust fragmentation handling for WebSocket communication. The server now correctly accumulates multi-packet messages using a `MemoryStream`, allowing support for large JSON-RPC payloads (> 4KB) without data loss.

## [1.9.1] - 2026-01-29

### Added
- **Copy URL Convenience**: Added a "Copy URL" button to the main dashboard, allowing users to quickly copy the server address (`http://localhost:[PORT]`) to their clipboard for use in external tools.
- **Palette's Journal**: Integrated the `.jules/palette.md` UX design journal to track critical architectural and user experience learnings.

## [1.9.0] - 2026-01-29

### Added
- **WebSocket Security Hardening**: Implemented protection against Cross-Site WebSocket Hijacking (CSWSH) and DNS Rebinding for WebSocket connections. The server now validates both the `Origin` header and the loopback interface before accepting any WebSocket upgrades.

## [1.8.9] - 2026-01-29

### Performance
- **High-Performance Dispatcher**: Refactored the internal JSON-RPC dispatcher to use a `Dictionary` for method lookups. This achieves O(1) constant-time performance for tool execution, replacing the previous sequential category checks.

## [1.8.8] - 2026-01-29

### Fixed
- **Path Traversal Security Fix**: Implemented a strict path validation system for `read_file` and `write_file`. All file paths are now normalized and resolved to ensure they remain within the Unity project root, preventing unauthorized access to sensitive system files.

## [1.8.7] - 2026-01-29

### Added
- **Stability Release (Self-Healing)**: Implemented a robust crash-recovery loop in the MCP server. It will now automatically attempt to restart and re-bind if a network error or silent crash occurs.
- **Improved InitializeOnLoad**: Refactored static initialization to reliably restart the server after every Unity domain reload, ensuring zero-interaction persistence.
- **MCP Discovery Fix**: Added support for standard MCP method names (`tools/list`, `resources/list`, `prompts/list`), resolving visibility issues in Gemini CLI and other strict clients.
- **Universal Documentation**: Fully updated README, Documentation, and Branding to reflect the unified "Nexus Unity" dashboard and architecture.

## [1.8.6] - 2026-01-29

### Fixed
- **Self-Healing Server**: Implemented a robust error-recovery loop in the MCP server. If the server loop crashes due to a network error, it will now automatically attempt to self-heal and restart.
- **Enhanced Cinema Mode**: Upgraded the demonstration script with "Asynchronous Swarm" logic, showcasing the AI's ability to generate and apply complex, per-object organic movement scripts.

## [1.8.5] - 2026-01-29

### Fixed
- **HttpListener Resource Cleanup**: Added explicit `Close()` calls to the `HttpListener` to ensure sockets are immediately released by the OS, resolving "Port already in use" errors during rapid restarts.
- **Robust Demo script**: Added error boundary checks to `demo_presentation.py` to prevent script crashes on server connection failures.

## [1.8.3] - 2026-01-29

### Fixed
- **Server Lifecycle Persistence**: Refactored `MCPServerWindow` to correctly persist the "Running" state through Unity domain reloads (compilation). The server will now automatically restart after scripts are modified.
- **Reliable Demo Automation**: Updated `AI/demo_presentation.py` with intelligent waiting logic to handle Unity compilation delays, ensuring smooth recording sessions.

## [1.8.2] - 2026-01-29

### Improved
- **API Consistency**: Updated `CreatePrimitive` to return full serialized GameObject details (ID and Name) instead of a status string. This enables high-speed automation scripts to immediately interact with newly created objects.
- **Cinematic Tools**: Released the `AI/demo_presentation.py` script for high-performance autonomous demonstrations.

## [1.8.1] - 2026-01-29

### Added
- **Cinema Mode**: Introduced the initial cinematic demonstration script for automated scene building and presentation.

## [1.8.0] - 2026-01-29

### Added
- **Full AI Toolset Release**: Expsosed **all 59+ functionalities** to the Gemini CLI (MCP). This provides the AI with a complete suite of tools for professional game development, including:
    - **Scene & Hierarchy**: Scene creation, object duplication, parenting, and reordering.
    - **Components**: Advanced inspection, surgical property edits, and component lifecycle management.
    - **Assets & Prefabs**: Full UPM-compliant asset manipulation and prefab override support.
    - **Automation**: Real-time log streaming, Play Mode control, and UI Toolkit automation.
- **Dynamic Schema Discovery**: Implemented modular JSON schema generation for all tools, ensuring accurate parameter handling by LLM agents.

## [1.7.9] - 2026-01-29

### Fixed
- **MCP Discovery Fix**: Updated the bridge script to use standard MCP method names (`tools/list`, `resources/list`, `prompts/list`). This resolves the "No prompts, tools, or resources found" error in the Gemini CLI.
- **Enhanced Capabilities**: Explicitly declared tools, resources, and prompts capabilities in the initialization response.

## [1.7.8] - 2026-01-29

### Fixed
- **Bridge Error Reporting**: Improved the Python bridge to explicitly report Unity server errors back to the Gemini CLI, preventing "No tools found" messages when the server is unreachable or returning errors.
- **Notification Handling**: Ensured the bridge correctly ignores JSON-RPC notifications to maintain connection stability.

## [1.7.5] - 2026-01-29

### Fixed
- **Regression Fix**: Restored the missing `ParseCommandLineArgs` method in `MCPServerWindow` to resolve compilation errors.
- **Linter Compliance**: Refactored the CLI installer to adhere to method length limits.
- **GEMINI.md Update**: Formalized the requirement for mandatory pre-push verification of compilation and functionality.

## [1.7.4] - 2026-01-29

### Improved
- **UI Restoration**: Restored "Nexus Unity" as a single clickable window button.
- **Unified Control Panel**: Consolidated the server start/stop controls and the Gemini CLI linking tools into a single, high-visibility dashboard.
- **Link Status**: Added a real-time status indicator for the Gemini CLI connection.

## [1.7.3] - 2026-01-29

### Improved
- **Robust macOS Installer**: Updated the CLI installer to intelligently resolve absolute paths for `gemini` and `python3` on macOS (checking Homebrew and system locations). This fixes the "command not found" error caused by Unity not inheriting terminal PATH variables.

## [1.7.2] - 2026-01-29

### Fixed
- **Regression Fix**: Restored the `ParseCommandLineArgs` method in `MCPServerWindow` which was accidentally removed during the UI refactor, causing compilation errors.

## [1.7.1] - 2026-01-29

### Changed
- **UI Consolidation**: Refactored the Unity Editor menu structure. Replaced the cluttered "Window > Unity MCP" submenus with a single, centralized "Window > Nexus Unity" control panel. 
- **Tabbed Interface**: The new panel features a tabbed layout for managing the Server, Developer Tools, and API Verification in one place.

## [1.7.0] - 2026-01-29

### Improved
- **CLI Installer Diagnostics**: Enhanced the error reporting logic in the "Link to Gemini CLI" feature to provide more detailed feedback during failures (exit codes, exact error messages, and command output). This helps diagnose environmental path issues on macOS and Windows.

## [1.6.9] - 2026-01-29

### Removed
- **Zero-Dependency Release**: Removed the hard dependency on `com.unity.editorcoroutines`. The library now has **ZERO** mandatory Unity package dependencies besides the built-in `Newtonsoft.Json`.
- **Test Refactor**: Replaced `TestCoroutine` with a simulated delay using `EditorApplication.delayCall`, ensuring the library remains functional and easy to install in any Unity project without extra steps.

## [1.6.8] - 2026-01-29

### Changed
- **Dependency Flexibility**: Lowered the minimum required version for `newtonsoft-json` to `2.0.0` in `package.json`. This ensures the library is compatible with a wider range of projects without forcing unnecessary package upgrades or downgrades.

## [1.6.7] - 2026-01-29

### Fixed
- **Dependency Hardening**: Added `versionDefines` and conditional compilation for the `EditorCoroutines` package. The library will now compile successfully even if the package is missing in the target project, providing a graceful warning instead of a compilation error.

## [1.6.6] - 2026-01-29

### Added
- **Security Hardening**: Integrated DNS Rebinding protection by validating the loopback interface for all incoming requests.
- **CSRF Protection**: Enforced strict `application/json` content-type validation and restricted server communication to `POST` methods only.

## [1.6.5] - 2026-01-29

### Fixed
- **UPM Installation Fix**: Removed hardcoded precompiled assembly references from `UnityMCP.Editor.asmdef`. This resolves the "Duplicate DLL" errors when importing the library into projects that already use Newtonsoft.Json.
- **Cross-Platform Installer**: Updated `MCPCliInstaller` to support both Windows (`cmd.exe`) and Mac/Linux (`bash`), ensuring the "Link to Gemini CLI" feature works across all development environments.

## [1.6.4] - 2026-01-29

### Fixed
- **Linter Compliance**: Refactored JSON schema generation and CLI installer code to strictly adhere to the AI-friendly diagnostic limits (method length, parameter counts). This resolves compilation blocks in strict projects.

## [1.6.3] - 2026-01-29

### Changed
- **Branding**: Updated the Package Manager display name to "Nexus Unity".

## [1.6.2] - 2026-01-29

### Fixed
- **CLI Installer Reliability**: Improved the bridge script discovery logic by using directory-relative pathing and a project-wide fallback, ensuring it works across complex project structures and Package Manager setups.

## [1.6.1] - 2026-01-29

### Fixed
- **Package Manager Support**: Refactored CLI installer to dynamically locate the bridge script, ensuring the "Link to Gemini CLI" feature works when the library is installed as a Unity Package.

## [1.6.0] - 2026-01-29

### Added
- **Gemini CLI Integration**: Added a "Link to Gemini CLI" menu item in Unity for one-click setup.
- **Dynamic Tool Discovery**: Added `list_tools` method that returns JSON schemas for tools, enabling automatic discovery by LLM agents.
- **Embedded Bridge**: The Python MCP bridge is now included directly in the library's `Editor` folder.

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
