# Nexus Unity AI Toolset Reference

This document provides a comprehensive, AI-optimized reference for all 74+ tools available in the `Nexus Unity` MCP server. These tools allow for full programmatic control over the Unity Editor.

> **Note for AI Agents**: All tools are prefixed with `unity_` when called via the Gemini CLI or Codex CLI/MCP bridge.

---

## 🏗 Scene Management

### `unity_create_scene`
Creates a new scene with default GameObjects (Main Camera and Directional Light).
- **Parameters**:
  - `name` (string): The name of the new scene.
- **Returns**: Status object `{status, message}`.
- **Behavior**: Automatically saves the current scene if dirty.

### `unity_open_scene`
Opens an existing scene file.
- **Parameters**:
  - `path` (string, required): Asset path (e.g., `Assets/Scenes/Main.unity`).
- **Returns**: Status object `{status, message}`.

### `unity_save_scene`
Saves the currently active scene.
- **Parameters**:
  - `path` (string, optional): Specific save location. If empty, saves to current path.
- **Returns**: Status object `{status, message}`.

### `unity_list_scenes`
Lists all scene assets in the project.
- **Returns**: Object with `scenes` array.

---

## 🌳 Hierarchy & GameObject Lifecycle

### `unity_create_game_object`
Creates an empty GameObject.
- **Parameters**:
  - `name` (string, required): Name of the object.
  - `parent_id` (integer, optional): Instance ID of the parent object.
- **Returns**: Status object with `data` (serialized GameObject).

### `unity_create_primitive`
Creates a standard Unity primitive.
- **Parameters**:
  - `primitive_type` (string, required): One of `Cube`, `Sphere`, `Capsule`, `Cylinder`, `Plane`, `Quad`.
- **Returns**: Status object with `data` (serialized GameObject).

### `unity_destroy_game_object`
Permanently deletes a GameObject from the scene.
- **Parameters**:
  - `instance_id` (integer, required): Target object ID.
- **Returns**: Status object `{status, message}`. Supports Undo.

### `unity_duplicate_object`
Creates a copy of a GameObject.
- **Parameters**:
  - `instance_id` (integer, required): Source object ID.
- **Returns**: Status object with `data` (serialized copy).

### `unity_set_active`
Enables or disables a GameObject.
- **Parameters**:
  - `instance_id` (integer, required): Target object ID.
  - `active` (boolean, required): `true` to enable, `false` to disable.
- **Returns**: Status object `{status, message}`.

### `unity_set_parent`
Changes the parent of a GameObject.
- **Parameters**:
  - `instance_id` (integer, required): Object to move.
  - `parent_id` (integer, required): New parent ID (use 0 for root).
- **Returns**: Status object `{status, message}`.

### `unity_set_sibling_index`
Reorders an object among its siblings.
- **Parameters**:
  - `instance_id` (integer, required): Target object ID.
  - `index` (string|integer, required): Integer index or `"first"` / `"last"`.
- **Returns**: Status object `{status, message}`.

### `unity_create_hierarchy`
Batch creates a full hierarchy of objects and components in one call.
- **Parameters**:
  - `tree` (object, required): JSON tree representing the hierarchy.
  - `parent_id` (integer, optional): Parent ID for the root of the tree.
- **Returns**: Status object with `data` (serialized root GameObject).
- **Example Tree**: `{ "name": "Root", "components": ["Canvas"], "children": [{ "name": "Child", "components": ["Image"] }] }`

---

## 🧩 Component & Property Manipulation

### `unity_add_component`
Attaches a new component to a GameObject.
- **Parameters**:
  - `instance_id` (integer, required): Target object ID.
  - `component_name` (string, required): Name of the class (e.g., `Rigidbody`, `MeshRenderer`).
- **Returns**: Status object `{status, message}`.

### `unity_remove_component`
Removes a component from a GameObject.
- **Parameters**:
  - `instance_id` (integer, required): Target object ID.
  - `component_name` (string, required): Component type name.
- **Returns**: Status object `{status, message}`.

### `unity_inspect_component`
Retrieves all visible properties and their current values.
- **Parameters**:
  - `instance_id` (integer, required): Target object ID.
  - `component_name` (string, required): Component type name.
- **Returns**: JSON object containing all serialized fields and their current values (with a `status: "Success"` key).

### `unity_get_component_schema`
Retrieves property names and types for a component (without current values).
- **Parameters**:
  - `instance_id` (integer, required): Target object ID.
  - `component_name` (string, required): Component type name.
- **Returns**: Object with `properties` array. Each property is `{name, type, displayName}`.

### `unity_update_component`
Batch updates component properties using JSON with detailed result reporting.
- **Parameters**:
  - `instance_id` (integer, required): Target object ID.
  - `component_name` (string, required): Component type name.
  - `properties` (object, required): JSON object of properties to update. Supports raw arrays for Unity Lists.
  - `json_data` (string, optional): *Deprecated.* Use `properties` instead.
- **Fuzzy Naming**: Automatically maps AI-friendly names (e.g. `sprite`) to internal Unity names (e.g. `m_Sprite`).
- **Object Referencing**: Supports multiple ways to set object references (e.g., Sprite, Material):
  - `integer`: Use the session-specific `instance_id`.
  - `string`: Use the asset project path.
  - `object`: Use `{ "guid": "...", "file_id": 123 }` for persistent sub-asset referencing (e.g., sliced sprites found via `explore_asset`).
- **Returns**: Result object `{status, updated_count, errors}`.
- **Status**: `"Success"`, `"Partial"`, or `"Failed"`.

### `unity_invoke_method`
Invokes a C# method on a Component or GameObject using reflection. Allows triggering internal behaviors without compilation.
- **Parameters**:
  - `instance_id` (integer, required): Target GameObject ID.
  - `component_name` (string, optional): Component name to invoke on. If omitted, invokes on the GameObject itself.
  - `method_name` (string, required): The exact method name (public or private).
  - `arguments` (array, optional): JSON array of arguments matching the method signature.
- **Returns**: Status object with a `result` key (serialized return value). If void, `result` is omitted.

### `unity_set_transform`
Surgical update of object position, rotation, or scale.
- **Parameters**:
  - `instance_id` (integer, required): Target object ID.
  - `position` (object, optional): `{x, y, z}` coordinates.
- **Returns**: Status object `{status, message}`.

### `unity_set_property`
Surgical update of a single field on a component or object.
- **Parameters**:
  - `instance_id` (integer, required): Target object ID.
  - `property_name` (string, required): The internal field name (e.g., `m_Enabled`, `m_Name`).
  - `value` (any, required): The new value. Supports bool, int, float, string, Vector3.
- **Returns**: Status object `{status, message}`.

### `unity_set_enabled`
Enables or disables a specific Component (Behaviour).
- **Parameters**:
  - `instance_id` (integer, required): Target object ID.
  - `component_name` (string, required): Component type name.
  - `enabled` (boolean, required): `true` or `false`.
- **Returns**: "OK".

---

## 📦 Asset Management

### `unity_list_assets`
Searches for assets in the project.
- **Parameters**:
  - `filter` (string, optional): Search string (e.g., `t:Material`, `my_texture`).
- **Returns**: Object with `assets` array.

### `unity_explore_asset`
Lists all internal sub-assets (e.g., sliced sprites) and their fileIDs within a single file.
- **Parameters**:
  - `path` (string, required): The project path to the asset.
- **Returns**: JSON object containing `guid`, `main_asset` info, and an array of `sub_assets` with their respective `file_id`s.

### `unity_create_material`
Creates a new material asset.
- **Parameters**:
  - `name` (string, required): File name.
  - `shader` (string, optional): Shader name (Default: `Standard`).
- **Returns**: Status object `{status, path}`.

### `unity_refresh_asset_database`
Forces Unity to scan for changed files on disk and import them.
- **Returns**: Object with `status` ("RefreshScheduled"), `is_compiling` (bool), and `is_updating` (bool).
- **AI Warning**: If `is_compiling` is true after this call, you MUST wait for it to become false (via `unity_get_editor_state`) before adding new components or scripts.

### `unity_import_asset`
Imports a file at the specified path into the project.
- **Parameters**:
  - `path` (string, required): Path to import.
- **Returns**: Status object `{status, message}`.

### `unity_instantiate_prefab`
Creates a prefab instance in the scene.
- **Parameters**:
  - `path` (string, required): Path to the prefab asset.
- **Returns**: Status object with `data` (serialized GameObject).

### `unity_create_prefab`
Saves a GameObject as a prefab asset.
- **Parameters**:
  - `instance_id` (integer, required): Object to save.
  - `path` (string, required): Target asset path.
- **Returns**: Status object `{status, path}`.

### `unity_apply_prefab_overrides`
Applies scene modifications back to the prefab asset.
- **Parameters**:
  - `instance_id` (integer, required): Instance ID of the prefab in the scene.
- **Returns**: Status object `{status, message}`.

### `unity_revert_prefab_overrides`
Reverts scene modifications to the prefab asset.
- **Parameters**:
  - `instance_id` (integer, required): Instance ID of the prefab in the scene.
- **Returns**: Status object `{status, message}`.

### `unity_move_asset` / `unity_copy_asset` / `unity_delete_asset`
Standard file operations within the project.
- **Parameters**: `old_path`, `new_path`, `source_path`, `dest_path`, or `path`.
- **Returns**: Status object `{status, message}`.

### `unity_create_folder`
Creates a directory in the project.
- **Parameters**:
  - `path` (string, required): Path like `Assets/NewFolder/SubFolder`.
- **Returns**: Status object `{status}`.

### `unity_read_file`
Direct text file manipulation (limited to project root).
- **Parameters**: `path`.
- **Returns**: Status object with `content` and `length`.

### `unity_write_file`
Direct text file manipulation (limited to project root).
- **Parameters**: `path`, `content`.
- **Returns**: Status object `{status}`.

### `unity_write_files_batch`
Writes multiple files in a single pass to save AI context and speed up execution.
- **Parameters**:
  - `files` (array of objects, required): The files to write. Each object must contain `path` and `content`.
- **Returns**: Status object `{status, message}`.
- **Behavior**: If any `.cs` script is included, it triggers a single deferred domain reload. Otherwise, it uses fast immediate imports.

---

## 🎮 Editor Control & State

### `unity_undo` / `unity_redo`
Standard Editor undo/redo operations.
- **Returns**: Status object `{status, message}`.

### `unity_toggle_play_mode`
Starts or stops the Unity Play Mode.
- **Parameters**:
  - `value` (boolean, optional): Set specific state. If omitted, toggles.
- **Returns**: Status object `{status, is_playing}`.

### `unity_pause_play_mode` / `unity_step_frame`
Controls playback while in Play Mode.
- **Returns**: Status object `{status, message}`.

### `unity_execute_menu_item`
Triggers any Unity menu command.
- **Parameters**:
  - `item_path` (string, required): Full path (e.g., `Edit/Select All`, `GameObject/Align With View`).
- **Returns**: Status object `{status, message}`.

### `unity_focus_scene_view`
Frames the current selection in the Scene View.
- **Returns**: Status object `{status, message}`.

### `unity_read_logs`
Retrieves Unity Console logs.
- **Parameters**:
  - `count` (integer, optional): Max logs (Default: 50).
- **Returns**: Object with `logs` array.

### `unity_attach_script`
Creates a new C# script and triggers compilation.
- **Parameters**:
  - `script_name` (string, required): Class name.
  - `script_content` (string, optional): Full C# code.
- **Returns**: Status object `{status, message}`.

### `unity_wait_for_ready`
Readiness probe that returns `true` only when the Unity editor is not compiling or importing assets.
- **Returns**: Boolean `true` when the editor is ready for follow-up tool calls.
- **AI Warning**: Use this after domain reloads before trying to attach newly written scripts.

### `unity_apply_code_change` (Macro)
The ultimate workflow optimization tool for the AI. This macro performs the full "Write -> Wait -> Check" loop in a single turn.
1. Clears Unity logs.
2. Writes multiple files to disk.
3. Triggers compilation and waits out the domain reload autonomously.
4. Gathers all post-compilation compiler errors.
- **Parameters**:
  - `files` (array of objects, required): The files to write. Each object must contain `path` and `content`.
- **Returns**: Status object `{status, time_waited_seconds, compiler_errors}`. If `compiler_errors` is non-empty, `status` will be `"Failed"`.

### `unity_wait_for_compilation`
Blocks the bridge until Unity finishes compiling scripts and reloads the domain. 
- **Use Case**: Call this immediately after `unity_write_file` or `unity_refresh_asset_database` to ensure your next command doesn't hit a "Connection Refused" error during reload.
- **Parameters**:
  - `timeout_seconds` (integer, optional): Max seconds to wait (Default: 90).
- **Returns**: Status object `{status, time_waited_seconds}`.

### `unity_wait_for_play_mode`
Blocks the bridge until Unity enters or exits Play Mode.
- **Parameters**:
  - `state` (boolean, required): `true` to wait for Play Mode, `false` for Edit Mode.
  - `timeout_seconds` (integer, optional): Max seconds to wait (Default: 60).
- **Returns**: Status object `{status, time_waited_seconds}`.

---

## 🔍 Discovery & Inspection

### `unity_get_game_object`
Retrieves basic metadata for a specific ID.
- **Parameters**: `instance_id`.
- **Returns**: Status object with `data` (serialized GameObject).

### `unity_get_active_game_object`
Returns the currently selected object in the Editor.
- **Returns**: Status object with `data` (serialized GameObject).

### `unity_get_root_game_objects`
Returns all top-level objects in the active scene.
- **Returns**: Object with `objects` array.

### `unity_get_object_path`
Returns the hierarchy path (breadcrumb) to an object.
- **Returns**: Status object with `path` string.
- **Example**: `Environment/Static/Tree_01`.

### `unity_find_objects`
Deep regex-based search for objects in the scene.
- **Parameters**:
  - `name` (string, optional): Regex for name.
  - `tag` (string, optional): Exact tag match.
  - `type` (string, optional): Component type filter.
- **Returns**: Object with `objects` array (max 50).

### `unity_find_references`
Finds which Assets and scene GameObjects reference a specific object. Essential for safe deletions and reorganizations.
- **Parameters** (Provide exactly one):
  - `target_guid` (string, optional): The GUID of the asset to search for.
  - `target_id` (integer, optional): The Instance ID of the object to search for.
- **Returns**: Status object with `asset_references` (array of file paths) and `scene_references` (array of GameObjects with their components that hold the reference).

### `unity_find_by_path`
Search for a GameObject using its hierarchy path.
- **Parameters**:
  - `path` (string, required): Absolute or relative path.
- **Returns**: Status object with `data` (serialized GameObject).
- **Example**: `Canvas/Main/Title`.

### `unity_get_tags_and_layers`
Lists all defined Tags and Layers in the project.
- **Returns**: Object with `tags` and `layers` arrays.

### `unity_get_children`
Returns all direct children of a GameObject.
- **Parameters**: `instance_id`.
- **Returns**: Object with `children` array. Each child is `{name, instance_id, active}`.

### `unity_get_editor_state`
Returns current editor state flags.
- **Returns**: `{is_playing, is_paused, is_compiling, is_updating, active_scene, platform}`.

### `unity_get_project_info`
Returns basic project metadata.
- **Returns**: JSON object with `project_path`, `unity_version`, `platform`, `product_name`, `company_name`.

---

## 🖥 UI Toolkit Automation

### `unity_ui_list_windows`
Lists all currently open Editor Windows.
- **Returns**: Object with `windows` array.

### `unity_ui_get_hierarchy`
Inspects the VisualElement tree of an Editor Window.
- **Parameters**:
  - `window_title` (string, required): Title of the target window.
  - `deep` (boolean, optional): If true, includes computed styles and layout for every node.
- **Returns**: JSON tree of UI elements (now includes `text` and `classes` by default).

### `unity_ui_query_elements`
Deep query of UI elements by text content, name, or CSS class. Essential for finding specific buttons or labels.
- **Parameters**:
  - `window_title` (string, required): Title of the target window.
  - `text` (string, optional): Text content to search for (e.g. "Buy").
  - `name` (string, optional): Element name.
  - `class_name` (string, optional): UXML CSS class name.
- **Returns**: Object with `elements` array. Each element includes deep computed styles (`layout`, `computed_style`, `text`, `classes`).

### `unity_ui_click`
Simulates a click on a UI element.
- **Parameters**: `window_title`, `element_name`.
- **Returns**: Status object `{status, message}`.

### `unity_ui_input_text`
Types text into a TextField or Label.
- **Parameters**: `window_title`, `element_name`, `text`.
- **Returns**: Status object `{status, message}`.

## 🛡️ Linter & Compliance
### `unity_enforce_forced_defaults`
Enforces `[ForceDefault]` attribute values on all components of a GameObject.
- **Parameters**:
  - `instance_id` (integer, required): Target GameObject ID.
- **Returns**: Status object `{status, message}`.

### `unity_capture_inspector_screenshot`
Captures a PNG screenshot of the Inspector window and its full UI layout tree.
- **Parameters**:
  - `instance_id` (integer, optional): If provided, automatically selects this object before taking the screenshot.
- **Returns**: Object with `image_base64` (PNG) and `ui_layout` (JSON tree).
- **macOS Note**: Requires screen recording permissions for Unity. If permissions are missing, it fallbacks to only returning the `ui_layout`.

### `unity_capture_game_view_screenshot`
Captures a PNG screenshot of the Game View.
- **Returns**: Object with `image_base64` (PNG).

### `unity_generate_mermaid_diagram`
Generates a Mermaid.js compatible graph string of the current scene hierarchy and component dependencies.
- **Returns**: Object with `mermaid` string.

### `unity_semantic_find`
Finds GameObjects by scanning class names, field names, method names, and object names for semantic meaning (intent-based discovery).
- **Parameters**:
  - `query` (string, required): Semantic search term (e.g., "Player Controller", "Inventory", "Update").
- **Returns**: Object with `matches` array, ranked by relevance score and reasons for the match.

### `unity_lint_project`
...
Runs a comprehensive project and scene health audit.
- **Deep Code Analysis**: Uses the official Unity Project Auditor to find performance bottlenecks, memory leaks, and API misuse in your C# code.
- **Scene Health Scan**: Recursively scans the active scene for missing scripts, broken prefab instances, and "pink" (Internal Error) materials.
- **Returns**: Object containing `code_issues` and `scene_issues` arrays (with a `status: "Success"` key).

---
*Generated for Nexus Unity v2.5.1*
