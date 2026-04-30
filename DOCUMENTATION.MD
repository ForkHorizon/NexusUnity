# Technical Documentation - Nexus Unity

This document provides a comprehensive reference for the `Nexus Unity` library.

**Requirements**: Unity 6000.0 or newer.

## 🔌 Unity MCP Server

The library implements a Model Context Protocol (MCP) server that enables external tools to interact with the Unity Editor via JSON-RPC 2.0.

### Architecture
- **Server**: Runs on a background thread using `System.Net.HttpListener`.
- **Self-Healing**: Automatically restarts the server loop if a network crash occurs.
- **Persistence**: Uses `SessionState` and `[InitializeOnLoad]` to survive Unity domain reloads (compilation) autonomously.
- **Main Thread Dispatching**: Unity APIs are not thread-safe; all operations are executed on the main thread via a thread-safe queue.
- **Port**: 8081 (Default). Configurable via Project Settings or CLI.

### Performance & Scalability (Bolt)
The v3.1+ architecture is optimized for low-latency and large-scale Unity projects:
- **Zero-Allocation Parsing**: Uses `JsonTextReader` to process incoming JSON-RPC payloads directly from network streams, preventing Large Object Heap (LOH) fragmentation.
- **Pooled Resource Management**: Implements `UnityEngine.Pool` for internal list buffers and path stacks, minimizing Garbage Collection (GC) spikes during frequent scene traversal or property inspection.
- **Native Spatial Culling**: Offloads distance-based object filtering to the Mojo bridge, reducing the processing load on the Unity Main Thread.

### Security (Sentinel)
Nexus Unity implements multiple layers of protection:
- **Payload Limiting**: A strict 10MB limit is enforced on all incoming requests to prevent memory exhaustion Denial of Service (DoS) attacks.
- **Origin Validation**: Strict enforcement of `Origin` and `Host` loopback headers prevents Cross-Site Request Forgery (CSRF), DNS Rebinding, and unauthorized browser-based access.
- **Path Isolation**: All file operations (`unity_read_file`, `unity_write_file`) are sandboxed to the project root directory with path-traversal protection.

---

## 🛠 JSON-RPC 2.0 API Reference

### Core & System

#### `initialize`
Returns server capabilities and metadata.
- **Result**: `{ "protocolVersion": "2024-11-05", "serverInfo": { "name": "Unity MCP Server", "version": "3.1.1" } }`

#### `get_server_status`
Returns explicit health and state of the MCP server and Unity editor.
- **Responsiveness**: Reports if the Unity main thread is currently responsive or blocked.
- **Engine State**: Reports `isCompiling`, `isImporting`, `isPlaying`, `isPaused`, and `isPlayModeTransition`.
- **Busy Reason**: Provides a human-readable `busyReason` ("idle", "compiling", "importing", "play_mode_transition").
- **Server State**: Reports `state` (`Running`, `Attached`, `Starting`, `Error`) and `lastError` if any.
- **Session Tracking**: Includes `sessionId` and `sessionGeneration` (increments on domain reload).
- **Process ID**: Includes `processId` of the host Unity instance.
- **Background execution**: This method runs on the listener thread, meaning it remains responsive even when Unity is compiling or the main thread is blocked.

#### `shutdown_server`
Safely stops the MCP server listener for the current Unity instance. Useful for resolving port conflicts or manual resets.

#### `ping_main_thread`
Explicit main-thread liveness check. Returns `pong` only when Unity can execute main-thread API calls.

#### `batch_execute`
Executes multiple JSON-RPC calls in a single HTTP request. This is the primary tool for reducing network latency and improving AI responsiveness by bundling independent operations.
- **Processing**: Iterates through the `requests` array on the Main Thread.
- **Recursive Guard**: To prevent stack overflow, `batch_execute` cannot call itself recursively.
- **Failures**: Independent (Best Effort). If one method fails, its error is captured in the results array, and execution continues for remaining requests.
- **Security**: Inherits project path validation and security checks from the individual methods being executed.
- **Params**: `requests` (Array of objects with `method` and optional `params`).

#### `attach_existing_session`
Verifies and attaches to an already running MCP session on the configured port. This is automatically handled during server startup if the project path matches.

#### `inspect_object`
Universal inspector capable of extracting all serialized data and type information from ANY Unity object. Supports deep nesting and a `detailed` flag for full type metadata.

#### `list_fields_for_type`
Lists all serializable fields for a given type name. Useful for discovering what fields can be patched.

#### `diff_scriptable_objects`
Compares two ScriptableObject assets and returns a JSON object mapping changed fields to their values in asset A and asset B.

#### `diff_scriptable_object_against_defaults`
Compares a ScriptableObject asset against a fresh instance of its type. This is the primary tool for identifying "dirty" or modified data in balancing assets.

#### `update_scriptable_object`
Surgically patches specific fields of a ScriptableObject. Supports complex types (Vectors, Colors, Arrays).

#### `edit_prefab_asset`
Modifies a prefab asset directly on disk without instantiating it in the active scene.

#### `get_prefab_overrides`
Inspects a prefab instance in the scene and returns a structured list of modifications (changed properties, added/removed components).

#### `open_prefab_stage` / `close_prefab_stage`
Allows AI agents to open a prefab asset in isolation mode and interact with it just like a normal scene.

---

## 🐍 Python Bridge (`nexus_unity_bridge.py`)

The Python bridge is a dual-mode script located in the project root. It handles communication between external CLI tools (like Gemini) and the Unity Editor.

### 1. MCP Mode (Default)
When run without arguments, the bridge acts as a persistent JSON-RPC 2.0 translator via `stdin`/`stdout`.
```bash
python3 nexus_unity_bridge.py
```

### 2. CLI Mode (New in v2.0.5)
You can now execute any Unity tool directly from the command line. This is ideal for quick automation tasks.
```bash
python3 nexus_unity_bridge.py unity_refresh_asset_database
```
Parameters can be passed as `key=value` pairs:
```bash
python3 nexus_unity_bridge.py unity_read_logs count=10
```

### 3. Sync Mode (New in v2.7.0)
The bridge includes deterministic waiting tools to synchronize with Unity's state.
- `unity_wait_until_ready`: Blocks until Unity is not compiling or importing and the main thread is responsive.

---

## 🔥 Mojo High-Performance Bridge (`nexus_unity_bridge_mojo`)

The Mojo bridge is a compiled native binary that replaces the Python bridge for high-performance and AI-integrated workflows. It is located in `Assets/NexusUnity/Editor/`.

### Key Advantages
- **2.2x Faster Latency**: Native binary execution and optimized socket transport eliminate Python interpreter overhead.
- **Native Spatial Culling**: Automatically filters distant Unity objects (>50m) using a native Mojo distance kernel, drastically reducing LLM prompt size and token cost.
- **Local AI Reasoning**: Integrates directly with Llama 3.2 3B for semantic search and architectural scene analysis.
- **Zero-Touch Sync**: Features a low-level background worker for real-time log streaming and Unity state tracking during AI reasoning.

### Usage
```bash
./Assets/NexusUnity/Editor/nexus_unity_bridge_mojo
```

---

## 🛠 Complete Tool Reference

For a detailed breakdown of every tool, its parameters, and return types, see:
**[👉 API_REFERENCE.MD](./API_REFERENCE.MD)**

### Categorized Tool List (85+ Tools)

#### 🏥 Server Health & State
`unity_get_server_status`, `unity_ping_main_thread`, `unity_attach_existing_session`, `unity_wait_until_ready`, `unity_shutdown_server`, `unity_batch_execute`.

#### 🏞 Scene & Hierarchy
`unity_create_scene`, `unity_open_scene`, `unity_save_scene`, `unity_list_scenes`, `unity_create_game_object`, `unity_create_primitive`, `unity_destroy_game_object`, `unity_duplicate_object`, `unity_set_active`, `unity_set_parent`, `unity_set_sibling_index`, `unity_create_hierarchy`.

#### 🧩 Components & Properties
`unity_add_component`, `unity_remove_component`, `unity_inspect_component`, `unity_component_values`, `unity_get_component_schema`, `unity_update_component`, `unity_invoke_method`, `unity_set_transform`, `unity_set_property`, `unity_set_enabled`.

#### 📦 Asset Management
`unity_list_assets`, `unity_explore_asset`, `unity_create_material`, `unity_refresh_asset_database`, `unity_import_asset`, `unity_instantiate_prefab`, `unity_create_prefab`, `unity_apply_prefab_overrides`, `unity_revert_prefab_overrides`, `unity_move_asset`, `unity_delete_asset`, `unity_copy_asset`, `unity_get_dependencies`, `unity_create_folder`, `unity_read_file`, `unity_write_file`, `unity_write_files_batch`.

#### 🎮 Editor Control
`unity_undo`, `unity_redo`, `unity_toggle_play_mode`, `unity_pause_play_mode`, `unity_step_frame`, `unity_execute_menu_item`, `unity_focus_scene_view`, `unity_read_logs`, `unity_read_logs_since_cursor`, `unity_clear_logs`, `unity_attach_script`, `unity_lint_project`, `unity_run_tests`, `unity_wait_for_ready`, `unity_wait_for_compilation`, `unity_wait_for_play_mode`, `unity_enforce_forced_defaults`.

#### 🔍 Discovery & Inspection
`unity_get_game_object`, `unity_get_active_game_object`, `unity_get_root_game_objects`, `unity_get_object_path`, `unity_find_objects`, `unity_find_references`, `unity_find_by_path`, `unity_get_tags_and_layers`, `unity_ping_object`, `unity_get_children`, `unity_get_editor_state`, `unity_get_project_info`, `unity_set_selection`.

#### 🔄 Sync & Pipeline
`unity_wait_for_compilation`, `unity_wait_for_play_mode`, `unity_wait_for_asset_import_idle`, `unity_wait_for_editor_idle`, `unity_refresh_asset_database`.

#### 💾 PlayerPrefs
`unity_get_player_pref`, `unity_set_player_pref`, `unity_delete_player_pref`, `unity_list_player_prefs`.

#### 📄 ScriptableObjects
`unity_read_scriptable_object`, `unity_update_scriptable_object`, `unity_create_scriptable_object_asset`, `unity_duplicate_scriptable_object_asset`, `unity_list_fields_for_type`.

#### 🎮 Runtime Input (Play Mode Only)
`unity_simulate_mouse`, `unity_simulate_touch`, `unity_click_object_in_game`.

#### 📸 Snapshots & Dependency Mapping
`unity_dump_scene_graph`, `unity_get_scene_dependencies`, `unity_get_editor_timeline`.

#---

### 📸 Snapshots & Dependency Mapping

#### `unity_dump_scene_graph`
Generates a recursive JSON tree of the active scene.
- **`root_id`** (optional): ID of specific object to start from.
- **`max_depth`** (default 5): Recursion limit.
- **`include_all_properties`** (default false): If true, dumps every field of every component.
- **Note**: Always includes vital "Key Properties" (Transform, Camera, Light, etc.) even when deep dumping is off.

#### `unity_get_scene_dependencies`
Maps all cross-object references in the current scene.
- **Returns**: A flat list of dependency links showing which components/fields point to which objects.

#### `unity_get_editor_timeline`
Returns a history of the last 50 Editor events (imports, scene changes, reloads).
- **Note**: Persists across domain reloads using `SessionState`.

### 🖥 UI Toolkit Automation
`unity_ui_list_windows`, `unity_ui_get_hierarchy`, `unity_ui_query_elements`, `unity_ui_click`, `unity_ui_input_text`.

---

### Scene & GameObject Lifecycle

#### `open_scene` / `create_scene`
Manages scene transitions.
- **Note**: Automatically saves the current scene to `Assets/AutoSavedScene.unity` if it has unsaved changes, preventing blocking dialogs.

#### `save_scene`
Saves the currently active scene.
- **Params**: `path` (string, optional): Specific save location. If empty, saves to current path (or default for new scenes).

#### `create_primitive`
Creates a standard Unity primitive.
- **Result**: Returns the full serialized GameObject (Name and InstanceID).

---

### Transforms & Components

#### `inspect_component`
Returns full serialized state of any component.
- **`fields`** (optional): Array of specific field names to return. If omitted, all fields are returned.
- **Recursive Unpacking**: Automatically unpacks Arrays, Lists, and Generic Structs into nested JSON.
- **Full Visibility**: Exposes all serialized fields, including those with `[HideInInspector]`.
- **Note**: Optimized for complex types like `BoxCollider` (unpacks `m_Center` and `m_Size`).

#### `component_values`
Surgically read specific component fields as a clean key-value object.
- **`fields`** (required): Array of field names to read.
- **Clean Output**: Returns a flat key-value object of the requested fields without extra status metadata (unless requested in the RPC wrapper). Ideal for high-speed balancing and state polling.

#### `batch_execute`
Executes multiple JSON-RPC calls in a single HTTP request. This is the primary tool for reducing network latency and improving AI responsiveness by bundling independent operations.
- **Processing**: Iterates through the `requests` array on the Main Thread.
- **Recursive Guard**: To prevent stack overflow, `batch_execute` cannot call itself recursively.
- **Failures**: Independent (Best Effort). If one method fails, its error is captured in the results array, and execution continues for remaining requests.
- **Security**: Inherits project path validation and security checks from the individual methods being executed.
- **Params**: `requests` (Array of objects with `method` and optional `params`).

#### `scene_delta`
Retrieves incremental hierarchy changes since a specific generation ID.
- **Params**: `generation` (long, required): The last known session generation ID.
- **Returns**: A list of changes (added, removed, modified) to optimize state synchronization.

#### `symbol_index`
Fuzzy and regex-based searching of all compiled C# symbols (Classes, Methods, Fields).
- **Params**: `query` (string, required): Search query.
- **Returns**: A list of matching symbols with their metadata.

#### `compact_scene_snapshot`
High-compression hierarchy dump for large scenes.
- **Returns**: A minimized JSON representation of the scene graph to reduce token usage.

#### `update_component`
Updates component values using `SerializedObject`. Returns a detailed result with `status` and `errors`.
- **Intuitive Properties**: Accepts a `properties` JSON object natively. You can pass raw arrays (e.g., `[id1, id2]`) for Lists/Arrays.
- **Fuzzy Naming**: Automatically resolves AI-friendly names (e.g., `sprite`) to Unity's internal backing fields (e.g., `m_Sprite` or `_sprite`).
- **Advanced Referencing**: Supports setting object references via `instance_id` (integer), string (asset path), or persistent `{ "guid": "...", "file_id": 123 }` objects. Useful for sub-assets like sliced sprites.

#### `invoke_method`
Scriptless Method Invoker. Executes a specific method (public or private) on a component or GameObject via C# reflection. Excellent for triggering behavior in Play Mode without writing test scripts.

#### `set_property`
Surgical update of a single property (bool, int, float, string, Vector3).

---

### Asset Management

#### `move_asset`
Moves or renames an asset.
- **Intelligent Merging**: If moving a directory to a path that already exists, it will recursively merge the contents rather than failing.

#### `refresh_asset_database`
Forces Unity to scan for changed files. Returns compilation and updating status.

---

#### `wait_for_ready`
Returns `true` only when the editor is idle enough for follow-up tool calls.
- **Behavior**: Checks Unity compile/import readiness instead of just confirming that the HTTP server socket is reachable.
- **Use Case**: Poll this after domain reloads before attaching freshly compiled scripts or issuing other editor-mutating commands.

### UI Toolkit Automation

#### `ui_get_hierarchy`
Generates a JSON tree of all `VisualElement` objects in a window.

#### `ui_click` / `ui_input_text`
Simulates interaction with Editor Window elements.

---

### 🛡️ Linter & Compliance

#### `unity_lint_project`
Runs a deterministic C# audit using Roslyn to enforce high code quality standards.
- **Note**: Generates `LINT_REPORT.txt` in the project root.

#### `unity_enforce_forced_defaults`
Enforces `[ForceDefault]` attribute values on all components of a GameObject.

---

## ⚙️ Configuration & Tooling

### Project Settings
Go to **Edit > Project Settings > Nexus Unity** to configure the port.

### Integration
1. Open **Window > Nexus Unity > Server Control Panel**.
2. Click **Link to Gemini CLI** or **Link to Codex CLI**.
3. Your local CLI is now connected to this Unity instance with all 61+ tools.
