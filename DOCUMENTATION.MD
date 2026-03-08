# Technical Documentation - Nexus Unity

This document provides a comprehensive reference for the `Nexus Unity` library.

## 🔌 Unity MCP Server

The library implements a Model Context Protocol (MCP) server that enables external tools to interact with the Unity Editor via JSON-RPC 2.0.

### Architecture
- **Server**: Runs on a background thread using `System.Net.HttpListener`.
- **Self-Healing**: Automatically restarts the server loop if a network crash occurs.
- **Persistence**: Uses `SessionState` and `[InitializeOnLoad]` to survive Unity domain reloads (compilation) autonomously.
- **Main Thread Dispatching**: Unity APIs are not thread-safe; all operations are executed on the main thread via a thread-safe queue.
- **Port**: 8081 (Default). Configurable via Project Settings or CLI.

---

## 🛠 JSON-RPC 2.0 API Reference

### Core & System

#### `initialize`
Returns server capabilities and metadata.
- **Result**: `{ "protocolVersion": "2024-11-05", "serverInfo": { "name": "Unity MCP Server", "version": "2.3.0" } }`

#### `list_tools`
Dynamically returns JSON schemas for all available tools. Used by the Python bridge for LLM discovery.

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

---

## 🛠 Complete Tool Reference

For a detailed breakdown of every tool, its parameters, and return types, see:
**[👉 API_REFERENCE.MD](./API_REFERENCE.MD)**

### Categorized Tool List (61+ Tools)

#### 🏞 Scene & Hierarchy
`unity_create_scene`, `unity_open_scene`, `unity_save_scene`, `unity_list_scenes`, `unity_create_game_object`, `unity_create_primitive`, `unity_destroy_game_object`, `unity_duplicate_object`, `unity_set_active`, `unity_set_parent`, `unity_set_sibling_index`, `unity_create_hierarchy`.

#### 🧩 Components & Properties
`unity_add_component`, `unity_remove_component`, `unity_inspect_component`, `unity_get_component_schema`, `unity_update_component`, `unity_set_transform`, `unity_set_property`, `unity_set_enabled`.

#### 📦 Asset Management
`unity_list_assets`, `unity_explore_asset`, `unity_create_material`, `unity_refresh_asset_database`, `unity_import_asset`, `unity_instantiate_prefab`, `unity_create_prefab`, `unity_apply_prefab_overrides`, `unity_revert_prefab_overrides`, `unity_move_asset`, `unity_delete_asset`, `unity_copy_asset`, `unity_get_dependencies`, `unity_create_folder`, `unity_read_file`, `unity_write_file`.

#### 🎮 Editor Control
`unity_undo`, `unity_redo`, `unity_toggle_play_mode`, `unity_pause_play_mode`, `unity_step_frame`, `unity_execute_menu_item`, `unity_focus_scene_view`, `unity_read_logs`, `unity_clear_logs`, `unity_attach_script`, `unity_lint_project`, `unity_wait_for_ready`, `unity_enforce_forced_defaults`.

#### 🔍 Discovery & Inspection
`unity_get_game_object`, `unity_get_active_game_object`, `unity_get_root_game_objects`, `unity_get_object_path`, `unity_find_objects`, `unity_find_by_path`, `unity_get_tags_and_layers`, `unity_ping_object`, `unity_get_children`, `unity_get_editor_state`, `unity_get_project_info`, `unity_set_selection`.

#### 🖥 UI Toolkit Automation
`unity_ui_list_windows`, `unity_ui_get_hierarchy`, `unity_ui_click`, `unity_ui_input_text`.

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
- **Recursive Unpacking**: Automatically unpacks Arrays, Lists, and Generic Structs into nested JSON.
- **Full Visibility**: Exposes all serialized fields, including those with `[HideInInspector]`.
- **Note**: Optimized for complex types like `BoxCollider` (unpacks `m_Center` and `m_Size`).

#### `update_component`
Updates component values using `SerializedObject`. Returns a detailed result with `status` and `errors`.
- **Advanced Referencing**: Supports setting object references via `instance_id` or persistent `{ "guid": "...", "file_id": 123 }` objects. Useful for sub-assets like sliced sprites.

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
1. Open **Window > Nexus Unity**.
2. Click **Link to Gemini CLI**.
3. Your local Gemini CLI is now connected to this Unity instance with all 61+ tools.