# Technical Documentation - NexusUnity

This document provides a comprehensive reference for the `NexusUnity` library.

## 🔌 Unity MCP Server

The library implements a Model Context Protocol (MCP) server that enables external tools to interact with the Unity Editor via JSON-RPC 2.0.

### Architecture
- **Server**: Runs on a background thread using `System.Net.HttpListener`.
- **Main Thread Dispatching**: Unity APIs are not thread-safe; all Editor operations are executed on the main thread via `EditorApplication.update`.
- **Persistence**: Uses `SessionState` and `EditorPrefs` to maintain state across assembly reloads.
- **Port**: 8081 (Default). Configurable via Project Settings or CLI.

---

## 🛠 JSON-RPC 2.0 API Reference

### Core & System

#### `initialize`
Returns server capabilities and metadata.
- **Result**: `{ "protocolVersion": "2024-11-05", "serverInfo": { "name": "Unity MCP Server", "version": "1.4.0" } }`

#### `read_logs`
Retrieves captured console logs.
- **Params**:
  - `count` (int, optional): Max logs to return (Default: 50).
  - `filter_type` (string, optional): "Log", "Warning", "Error", etc.
  - `search_text` (string, optional): String to search in message/stacktrace.
- **Result**: Array of `LogEntry` objects.

#### `clear_logs`
Clears the internal log buffer.
- **Result**: `"Logs cleared"`

#### `test_coroutine`
Starts a test asynchronous operation in the editor.
- **Result**: `"Started"`

---

### Scene & GameObject Lifecycle

#### `open_scene`
Opens a specific scene file.
- **Params**: `path` (string): Relative to project root (e.g., "Assets/Scenes/Main.unity").

#### `create_scene`
Creates a new scene.
- **Params**: `name` (string, optional): "New Scene" by default.

#### `save_scene`
Saves the currently active scene.

#### `get_game_object`
Returns serialized details of a GameObject.
- **Params**: `instance_id` (int): Unity Instance ID.

#### `get_active_game_object`
Returns the currently selected GameObject in the editor.

#### `get_root_game_objects`
Returns an array of all top-level GameObjects in the active scene.

#### `create_game_object`
Creates a new empty GameObject.
- **Params**: 
  - `name` (string): Required.
  - `parent_id` (int, optional): Instance ID of the parent.

#### `create_primitive`
Creates a standard Unity primitive (Cube, Sphere, etc.).
- **Params**: `primitive_type` (string): "Cube", "Sphere", "Capsule", "Cylinder", "Plane", "Quad".

#### `destroy_game_object`
Deletes an object from the scene (Undo-supported).
- **Params**: `instance_id` (int).

#### `instantiate_prefab`
Creates an instance of a prefab asset.
- **Params**: `path` (string): Path to the prefab asset.

#### `create_prefab`
Converts a scene GameObject into a prefab asset.
- **Params**: `instance_id` (int), `path` (string).

#### `apply_prefab_overrides`
Saves instance changes back to the source prefab.
- **Params**: `instance_id` (int).

#### `revert_prefab_overrides`
Resets an instance to its prefab defaults.
- **Params**: `instance_id` (int).

---

### Transforms & Components

#### `set_transform`
Updates an object's position.
- **Params**:
  - `instance_id` (int).
  - `position` (object, optional): `{ "x": float, "y": float, "z": float }`.

#### `set_parent`
Sets the parent of an object.
- **Params**: `instance_id` (int), `parent_id` (int).

#### `add_component`
Adds a project component or built-in Unity component.
- **Params**: `instance_id` (int), `component_name` (string).

#### `inspect_component`
Returns all public fields and properties of a specific component.
- **Params**: `instance_id` (int), `component_name` (string).

#### `update_component`
Updates component values using Unity's `JsonUtility.FromJsonOverwrite`.
- **Params**:
  - `instance_id` (int).
  - `component_name` (string).
  - `json_data` (string): JSON representation of the component data to overwrite.

#### `attach_script`
Generates a C# script and schedules it for attachment to the current selection.
- **Params**: `script_name` (string), `script_content` (string, optional).

#### `set_property`
Surgical update of a single property using SerializedObject.
- **Params**: 
  - `instance_id` (int)
  - `property_name` (string): e.g., "m_IsActive", "m_TagString".
  - `value` (bool | int | float | string | object): Object for Vector3 `{x,y,z}`.

---

### Asset Management

#### `list_assets`
Lists project assets.
- **Params**: 
  - `filter` (string, optional): t:[type], l:[label], etc.
  - `folders` (array, optional): Folders to search within.

#### `create_material`
Generates a new material asset.
- **Params**: `name` (string), `shader` (string, optional - Default: "Standard").

#### `refresh_asset_database`
Forces Unity to re-scan for asset changes.

#### `import_asset`
Triggers an immediate import of a specific path.
- **Params**: `path` (string).

---

### Discovery & Search

#### `find_objects`
Search the hierarchy with regex and filters.
- **Params**: 
  - `name` (string, optional): Regex for object name.
  - `tag` (string, optional).
  - `type` (string, optional): Component type name.
- **Result**: Array of GameObject summaries.

#### `get_object_path`
Returns the full hierarchy path (breadcrumb).
- **Params**: `instance_id` (int).

#### `list_scenes`
Lists all `.unity` files in the project.

#### `get_tags_and_layers`
Returns all defined Tags and Layers in the project.

#### `ping_object`
Highight an object in the Unity Editor.
- **Params**: `instance_id` (int).

---

### Editor State & Control

#### `set_selection`
Sets the active selection in the editor.
- **Params**: `instance_ids` (array of int).

#### `focus_scene_view`
Frames the current selection in the Scene View camera.

#### `undo` / `redo`
Triggers Unity's native Undo or Redo operation.

#### `toggle_play_mode`
Starts or stops the game.
- **Params**: `value` (bool, optional): Explicit target state.

#### `execute_menu_item`
Triggers a menu command by path.
- **Params**: `item_path` (string): e.g., "Edit/Select All".

---

### UI Toolkit Automation

#### `ui_list_windows`
Returns an array of all open `EditorWindow` instances.

#### `ui_get_hierarchy`
Generates a JSON tree of all `VisualElement` objects in a window.
- **Params**: `window_title` (string).

#### `ui_click`
Simulates a click on a VisualElement by name.
- **Params**: `window_title` (string), `element_name` (string).

#### `ui_input_text`
Injects text into a text field in an Editor Window.
- **Params**: `window_title` (string), `element_name` (string), `text` (string).

---

## ⚙️ Configuration & Tooling

### Project Settings
Go to **Edit > Project Settings > Unity MCP Server** to:
- Configure the default server port (Default: 8081).

### Command Line Arguments
When launching Unity from terminal, you can override the port:
```bash
./Unity -projectPath . --mcp-port 9090
```

### Universal AI Tools (`AI/`)
A collection of scripts for CI/CD and automated code quality:
- `diagnose.sh`: Universal project health and compilation check.
- `linter.sh`: Orchestrates file length, method length, and documentation checks.
