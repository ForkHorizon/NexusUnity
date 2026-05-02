## [3.1.2] - 2026-05-02

### Added
- **Project-Wide Knowledge Graph**: Integrated Jules' background daemon to index C# classes, inheritance, and usages.
- **`unity_knowledge_graph`**: New tool for querying the Knowledge Graph.

### Fixed
- **Reflection Ambiguity**: Resolved `AmbiguousMatchException` in `EntityId` conversion by filtering by return type.
- **Unity 6 Compatibility**: Fixed scene validity checks for the new `Scene` struct.

### Optimized
- **High-Performance Audits**: Re-implemented `Renderer` and `Material` list pooling in `ScanSceneHealth` to eliminate GC spikes.
- **Fast UI Search**: Restored `VisualElement.Q` optimization for editor window element lookups.
- **Bolt Bridge**: Cached Knowledge Graph parsing in the Mojo bridge interop.

# Changelog - NexusUnity

All notable changes to the `NexusUnity` library will be documented in this file.

## [3.1.1] - 2026-04-30

### Added
- **Knowledge Graph Tool**: Introduced `unity_knowledge_graph` (Mojo-native) for high-speed indexing and querying of project C# classes, inheritance, and usages.
- **Sentinel Security**: Hardened the server against Denial of Service (DoS) by enforcing a 10MB payload limit on all HTTP and WebSocket requests.
- **Robust Origin Validation**: Implemented strict loopback validation for `Origin` and `Host` headers to protect against CSRF, CSWSH, and DNS Rebinding attacks.

### Improved
- **🔥 Bolt Performance Architecture**:
  - **Zero-Allocation Stream Parsing**: Refactored JSON-RPC handling to parse directly from network streams using `JsonTextReader`. This eliminates intermediate string allocations and Large Object Heap (LOH) churn.
  - **Stack-Based Path Construction**: Optimized hierarchy path retrieval (`GetObjectPath`) using a pooled `Stack<string>` to eliminate O(N^2) string allocations during deep scene traversal.
  - **Pooled Component Caching**: Optimized `ApplyPropertiesToHierarchyObject` and `FindReferences` using non-allocating `GetComponents(list)` overloads with a shared `List<Component>` pool.
- **Visual Polish**:
  - Enhanced the MCP Server Control Panel with standard Unity Editor icons and descriptive tooltips using `GUIContent`.
  - Improved accessibility for primary actions (Documentation, API Reference, Server Control) in the main window.
- **Unity 2022 Compatibility**: Backported frame-based auto-start reliability and thread-safety locks to ensure stability on older Unity versions.

### Fixed
- **Unity 6 Compatibility**: Resolved multiple compiler errors in `MCPServerMethods.Delta.cs` related to obsolete `ObjectChangeStream` event members. Replaced `instanceId` with `entityId` and updated `InstanceIDToObject` calls to use the modern `EntityIdToObject` API.
- **EntityId Handling**: Implemented safe conversion of `EntityId` to legacy `int` values using `ConvertEntityIdToLegacyInt` to maintain backward compatibility with existing JSON-RPC clients while adhering to the modern Unity identification standards.
- **Input Simulation Reliability**: Refactored `QueueCrossFrameMouseClick` to use a deterministic `EditorApplication.update` timer instead of `Task.Delay`. This significantly improves the reliability of mouse click and touch simulations during Play Mode.
- **Type Discovery**: Enhanced the internal `FindType` method with better assembly-priority logic to ensure dynamically created scripts are correctly identified immediately after compilation.

## [3.1.0] - 2026-04-18

### Added
- **Batch Execution**: Introduced `unity_batch_execute` to execute multiple JSON-RPC calls in a single request, reducing network round-trips.
- **Scene Delta Tracking**: Added `unity_scene_delta` to retrieve incremental hierarchy changes since a specific generation ID, optimizing state synchronization.
- **Symbol Indexing**: Introduced `unity_symbol_index` for fuzzy and regex-based searching of all compiled C# symbols (Classes, Methods, Fields).
- **Surgical Component Values**: Introduced `unity_component_values` for high-speed, clean reading of specific component fields as a flat key-value object.
- **Compact Scene Snapshot**: Added `unity_compact_scene_snapshot` for high-compression hierarchy dumps, significantly reducing data transfer for large scenes.

### Improved
- **Enhanced Log Reading**: `unity_read_logs` now supports a `structured` parameter for retrieving logs as typed objects instead of raw strings.
- **Granular Component Inspection**: `unity_inspect_component` now supports a `fields` parameter to filter specific properties, reducing prompt size and token usage.

## [3.0.0] - 2026-04-17

### Added
- **🔥 Mojo High-Performance Bridge**: A complete architectural shift from a Python-interpreted bridge to a native-compiled Mojo binary, delivering **2.2x faster** end-to-end performance.
- **🛰️ Native Spatial Culling**: Implemented a native Mojo distance kernel that automatically filters distant Unity objects (>50m) from the LLM prompt. This reduces token usage by up to **90%** in large scenes, ensuring the AI focuses only on relevant local context.
- **⚡ Zero-Copy Byte Scanning**: Replaced heavy `json.loads` parsing with a high-speed Python-to-Mojo byte scanner. This allows sub-millisecond extraction of GameObject names, IDs, and positions directly from the raw Unity response stream.
- **🧵 GIL-Bypass Background Worker**: Established a true concurrent background worker that handles log streaming and heartbeats independently of the main reasoning thread, ensuring smooth log delivery even during intensive AI processing.
- **🔌 Optimized Socket Transport**: Purged the `requests` library in favor of a native HTTP protocol layer over raw sockets, eliminating all networking-related interpreter overhead.
- **🧠 Intelligent Default Reasoning**: Switched to **Llama 3.2 3B** as the core reasoning engine, optimized for structural scene analysis with a significantly reduced RAM footprint (2.0GB).

### Improved
- **📦 Zero-Dependency Binary**: The bridge is now distributed as a portable Mojo-compiled binary (`nexus_unity_bridge_mojo`) within the library folder, making it instantly usable in any project.
- **🛡️ Type-Safe Interop**: Enhanced Mojo/Python memory management with robust error recovery for heavy JSON-RPC payloads.
- **⏳ Low-Latency Heartbeats**: Heartbeats and service discovery are now performed at the socket level, making startup and reconnection nearly instantaneous.

## [2.9.2] - 2026-04-16

### Fixed
- **Stale "Attached" State**: Resolved a bug where the server remained in an `Attached` state when only one Unity instance was active. This occurred due to "zombie" listeners surviving domain reloads without a proper handle cleanup. 
- **Domain Reload Resilience**: Added `processId` to the status response and implemented a PID-based verification. If the server detects its own PID holding the port from a previous domain, it now sends a `shutdown_server` command to the stale listener before starting a fresh instance.

### Added
- **Reliable Status Diagnostic**: Enhanced `get_server_status` to provide explicit health and state tracking for AI agents.
  - Added `busyReason` ("idle", "compiling", "importing", "play_mode_transition") to distinguish between different types of unresponsiveness.
  - Added `isPlayModeTransition` to `editorState` to reliably detect when Unity is entering/exiting Play Mode.
  - Bypassed the Main Thread for these status checks, ensuring the AI can always verify server liveness even when the Unity UI is blocked.
- **New Tool**: `unity_shutdown_server` RPC method added to safely terminate the listener for a specific Unity instance (useful for troubleshooting and automated cleanup).

## [2.9.1] - 2026-04-16

### Improved
- **Port Conflict Resilience**: Refactored `MCPServer.Start` to handle "Unknown Process" port collisions gracefully. Instead of failing when `IsPortBusy` returns true but the owner is unidentified (common on macOS due to path restrictions), the server now proceeds with a force-bind attempt, allowing the native `HttpListener` to be the final authority.
- **Robust Port Ownership Detection**: Updated `GetPortOwner` on macOS to use the full path `/usr/sbin/lsof` as a fallback, ensuring reliable process identification even when the Editor's execution environment has a restricted `PATH`.

## [2.9.0] - 2026-04-13

### Added
- **Selection & Context Helpers**: Added specialized tools to give AI agents deep situational awareness about selected objects and broken references.
  - `get_selected_object_full_context`: Returns a massive, context-rich JSON payload for the currently selected GameObject, including all serialized components, prefab status, and exact hierarchy path.
  - `show_unresolved_missing_references`: Scans the active scene for "Missing Script" components or broken `ObjectReference` fields and returns their exact locations.
- **NUnit Test Execution**: Introduced `run_tests` tool to programmatically trigger EditMode and PlayMode tests from the MCP server. Uses reflection for zero-dependency portability.
- **Enhanced Reference Tracking**: Upgraded `find_references` to not just return the component type, but the precise `propertyPath` (field name) that holds the reference, answering exactly "why is this object referenced here?".
- **Workflow Optimization**: Added `unity_write_files_batch` to allow AI agents to write multiple files in a singlepass, dramatically reducing turn usage and improving synchronization.
- **Component Balancing**: Introduced `unity_enforce_forced_defaults` to programmatically apply `[ForceDefault]` attribute values across GameObjects.

### Improved
- **Robust Port Management & UX**: Completely overhauled the server's networking and state management.
  - **Process Owner Detection**: The server now identifies the exact process (name and PID) holding a busy port using `lsof` (macOS/Linux) and `netstat` (Windows).
  - **IPv4/IPv6 Accuracy**: Replaced connection-based port checking with `IPGlobalProperties` for 100% reliable detection across all IP stacks, preventing "Address already in use" crashes.
  - **ServerState Lifecycle**: Introduced a formal `ServerState` machine (`Stopped`, `Starting`, `Running`, `Attached`, `Error`) to replace simple booleans, fixing race conditions and providing accurate "Attached" status for multi-instance projects.
  - **Dynamic Port Assignment**: Added support for **Port 0**, allowing the OS to automatically assign a free port to the MCP server.
  - **Visual Diagnostics**: Updated the Unity Editor dashboard to display explicit error messages and real-time state transitions, making server startup failures transparent and easy to debug.

## [2.8.0] - 2026-04-04

### Added
- **Scene Snapshot & Graph Dumping**: Introduced `dump_scene_graph`, a high-performance recursive tool that serializes the entire scene hierarchy into a JSON tree.
  - Automatically captures "Key Properties" for common components (Transform, Camera, Light, MeshRenderer) even when deep serialization is disabled.
  - Supports configurable recursion depth and root filtering.
- **Scene Dependency Mapping**: Added `get_scene_dependencies`, which performs a deep scan of all scene components to identify cross-object references.
  - Maps connections between GameObjects and Components within the active scene.
  - Useful for architectural analysis and identifying broken or redundant links.
- **Editor Action Timeline**: Introduced `get_editor_timeline`, providing a persistent history of recent events.
  - Tracks asset imports, deletes, and moves.
  - Tracks scene transitions (opened, saved).
  - Automatically records domain reloads and play mode state changes.
  - Helps AI agents maintain context of "what just happened" in the Editor.

## [2.7.1] - 2026-04-04

### Fixed
- **Autonomous Script Attachment**: Restored the logic to automatically attach newly compiled scripts to their target GameObjects. Fixed Unity 6 compatibility by using version-agnostic ID helpers.
- **Server Auto-Start Reliability**: Replaced `Task.Delay` with a robust 60-frame delay in `MCPServer.Init` to ensure the server starts only after the engine has fully settled post-compilation.
- **Input System Stability**: Eliminated `InvalidCastException` and nested update crashes by removing manual `InputSystem.Update()` calls and migrating to sequential `EditorApplication.delayCall`s for simulated clicks.
- **Play Mode Liveness**: Forced `Application.runInBackground = true` during server initialization to prevent timeouts when the Editor loses focus in Play Mode.
- **Race Condition Protection**: Added thread-synchronization locks to `MCPServer.Start()` to prevent duplicate initialization attempts on the same port.
- **Mode Transition Focus**: Integrated `AppNapBypass.ScheduleActivation()` into `TogglePlayMode` to ensure consistent background behavior through mode changes.

## [2.7.0] - 2026-04-01

### Added
- **Comprehensive Server Health Monitoring**: Introduced explicit, structured health and state reporting.
  - `unity_get_server_status`: Returns detailed JSON including `sessionId`, `sessionGeneration`, `projectPath`, `unityVersion`, and real-time responsiveness of the main thread.
  - `unity_ping_main_thread`: Explicit main-thread liveness check to verify execution capability.
  - `unity_attach_existing_session`: Gracefully attaches to a healthy session on an already bound port.
- **Intelligent Session Management**: 
  - Implemented `SessionId` persistence and `SessionGeneration` incrementing across Unity domain reloads using `SessionState`.
  - Added caching for critical engine states (Compiling, Importing, Playing, Paused) to allow non-blocking health checks from background threads.
- **Deterministic Ready Wait**: Added `unity_wait_until_ready` to the Python bridge, ensuring AI agents wait until Unity is fully idle and the main thread is responsive before proceeding.
- **ScriptableObject Diff & Balancing Tools**:
  - `unity_diff_scriptable_objects`: Compare two assets of the same type and get a structured JSON diff of all serialized fields.
  - `unity_diff_scriptable_object_against_defaults`: Compare an asset against its default (code-defined) state to identify all modifications.
  - Enhanced `unity_update_scriptable_object` with surgical field patching for precise balancing passes.
- **Strong Object Inspection**:
  - `unity_inspect_object`: Universal inspector capable of extracting all serialized data and type information from ANY Unity object (Materials, Textures, Meshes, ScriptableObjects, GameObjects, etc.).
  - Deep Nesting Support: Array, List, and `[SerializeReference]` (ManagedReference) types are now fully supported with structured, recursively expanding JSON outputs.
  - Added optional `detailed` flag to `unity_inspect_object`, `unity_inspect_component`, and `unity_read_scriptable_object` to retrieve full type metadata (C# type, Unity propertyType, displayName, tooltip) alongside values.
- **Prefab Editing Helpers**:
  - `unity_open_prefab_stage` and `unity_close_prefab_stage`: Allow AI agents to open a prefab asset in isolation mode and interact with it like a scene.
  - `unity_edit_prefab_asset`: Directly modify a prefab asset on disk without instantiating it in the active scene.
  - `unity_get_prefab_overrides`: Inspect a prefab instance in the scene and return a structured list of modifications (changed properties, added/removed components).
  - Guided Overrides: `unity_apply_prefab_overrides` and `unity_revert_prefab_overrides` now return exactly what was applied or reverted in their response.

### Improved
- **Concurrency & Responsiveness**: Refactored the internal HTTP listener to process requests asynchronously via `Task.Run`. This prevents a single blocked main-thread command from hanging the entire server, allowing health checks to remain responsive even during long engine operations.
- **Port Conflict UX**: Completely overhauled "Address already in use" handling. The server now identifies the owner of a busy port. If it belongs to the same project, it attaches automatically. If it belongs to a different project, it provides a clear, actionable error message with the remote project path.

### Fixed
- **Heartbeat Stability**: Migrated the background heartbeat from unstable `Task.Delay` continuations to the native `EditorApplication.update` loop, ensuring reliable responsiveness tracking and state caching.
- **Compiler Compatibility**: Fixed numerous compilation errors caused by improper assembly referencing and missing `using` directives in log and input modules.

## [2.6.0] - 2026-04-01

### Added
- **Runtime Gameplay Input Tools**: Programmatic interaction with the game during Play Mode.
  - `unity_simulate_mouse`: Full mouse simulation (move, press, release, click) with support for normalized and absolute coordinates.
  - `unity_simulate_touch`: Multi-touch simulation for mobile-focused testing. Automatically injects a virtual `Touchscreen` device if a physical one isn't present in the Editor.
  - `unity_click_object_in_game`: Smart helper that raycasts from the main camera to any GameObject and performs a simulated cross-frame click.
- **Enhanced Log Consumption**: Improved tools for iterative debugging.
  - `unity_read_logs_since_cursor`: Efficiently fetch only new logs using a persistent cursor.
  - **Advanced Filtering**: Support for multi-severity filtering (e.g., fetching both Errors and Warnings in one call) and content-based search.
  - **Sequential Tracking**: Every log entry now has a unique, sequential `Id` for accurate synchronization.
  - **Play Mode Log Bridging**: Implemented `MCPRuntimeLogger` to capture and forward logs from the player execution context directly to the Editor server.
- **Asset Pipeline Sync Tools**: New deterministic waiting tools to eliminate race conditions.
  - `unity_wait_for_asset_import_idle`: Blocks until all background asset imports are complete.
  - `unity_wait_for_editor_idle`: Blocks until the editor is fully idle (compilation, imports, and background tasks finished).
- **ScriptableObject Tools**: Native tools for data-driven asset management.
  - `unity_read_scriptable_object`: High-fidelity property extraction for ScriptableObject assets.
  - `unity_update_scriptable_object`: Surgical property updates with JSON payloads.
  - `unity_create_scriptable_object_asset`: Programmatic creation of ScriptableObject assets.
  - `unity_duplicate_scriptable_object_asset`: Fast asset duplication for balancing/iteration.
  - `unity_list_fields_for_type`: Schema discovery for serializable fields.
- **PlayerPrefs Tools**: New set of tools to interact with Unity's `PlayerPrefs` system.
  - `unity_get_player_pref`: Retrieve stored values (int, float, string) with optional default values.
  - `unity_set_player_pref`: Store or update values securely.
  - `unity_delete_player_pref`: Delete specific keys or clear all preferences.
  - `unity_list_player_prefs`: List all available preference keys and their values using platform-specific extraction (macOS defaults and Windows Registry). This eliminates the need for manual shell-level workarounds.

### Fixed
- **Input System Stability**: Completely overhauled the runtime input simulation to use native struct state manipulation combined with 50ms task-delayed `InputSystem.QueueStateEvent` processing. This ensures simulated clicks span multiple Unity engine frames, allowing `Update()` loops to reliably detect `isPressed` states without dropping inputs.
- **Server State Persistence**: Fixed a bug where a domain reload would occasionally strand the server. The server now leverages a robust `AutoStartServer` attribute combined with proper `RuntimeInitializeLoadType.BeforeSceneLoad` hooks to guarantee 100% reliable initialization across all Editor states.
- **API Compatability**: Removed references to volatile internal APIs (like `InputSystem.time`) ensuring compilation stability across minor Unity version updates.

### Optimized
- **Domain Reload Performance**: Dramatically reduced Unity unresponsiveness during script compilation.
  - **Dynamic Heartbeat**: Heartbeat frequency automatically throttles from 100ms to 1000ms when `isCompiling` is detected, reducing CPU contention.
  - **Asynchronous Asset Refresh**: Replaced synchronous `AssetDatabase.Refresh` with asynchronous updates to prevent Main Thread lockups and "Not Responding" states.
  - **Smart OS Wake**: Suppressed `CFRunLoopWakeUp` calls during domain reloads to allow the engine to prioritize the reload process without OS-level UI interruptions.
  - **Efficient Polling**: Updated Python bridge to use exponential backoff/slower polling during server reboots, giving the Unity process more resources to complete the reload.

## [2.5.1] - 2026-03-30

### Fixed
- **Unity 6 Legacy ID Bridging**: Restored correct round-tripping between legacy JSON-RPC `instance_id` values and Unity 6 `EntityId`s. This fixes follow-up operations like `get_game_object`, `add_component`, prefab overrides, and other workflows that reuse returned object IDs.
- **Readiness Semantics**: `wait_for_ready` now reports actual editor readiness by checking compilation/import state instead of always returning `true`.

### Changed
- **Documentation Sync**: Updated README, DOCUMENTATION, and API reference to describe the corrected readiness behavior and the stable `instance_id` compatibility layer.

## [2.5.0] - 2026-03-19

### Added
- **Codex CLI Integration**: Added a one-click setup button in the "Nexus Unity" panel to instantly link the project to the Codex CLI. It automatically configures `~/.codex/config.toml` with the correct bridge script path.
- **Unified CLI Management**: The Unity Editor panel now supports multiple external AI tools under a new "CLI Integrations" section, displaying separate links for Gemini and Codex.

## [2.4.1] - 2026-03-10

### Changed
- **Unity 6 Exclusive Support**: Transitioned the library to exclusively support **Unity 6000.x** and newer. This allows for native utilization of modern engine APIs and improved performance.
- **API Modernization**: Replaced all internal uses of the obsolete `EditorUtility.InstanceIDToObject` with the new `EditorUtility.EntityIdToObject`.
- **Project Requirements**: Updated `package.json` and documentation to enforce Unity 6000.0 as the minimum required version.

## [2.4.0] - 2026-03-09

### Added
- **Consistent Return Types**: Standardized the JSON-RPC response format across all 61+ tools. Every tool now returns a structured `JObject` with consistent keys (e.g., `status`, `message`, `data`, `items`). This eliminates parsing inconsistencies where some tools previously returned raw strings or arrays, enabling robust error handling for AI agents.
- **Workflow Macro Tool**: Introduced `unity_apply_code_change` to the Python bridge. This single tool performs a complete write -> compile -> verify loop, dramatically reducing AI turn usage and eliminating manual terminal waits.
- **Native Wait Tools**: Built-in Python bridge tools (`unity_wait_for_compilation`, `unity_wait_for_play_mode`) to wait out asynchronous engine states like domain reloads and mode transitions without dropping connections.
- **Inspector Visual Insight**: Added `unity_capture_inspector_screenshot` which captures both a PNG and a high-fidelity UI Toolkit hierarchy of the current Inspector, allowing AI to "see" custom inspector layouts.
- **Scene Topology Mapping**: Added `unity_generate_mermaid_diagram` to instantly generate a Mermaid.js map of the scene hierarchy and component dependencies.
- **Semantic Discovery**: Introduced `unity_semantic_find` to locate objects based on class names, field names, and intent rather than just exact name matches.
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

