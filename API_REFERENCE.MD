# Nexus Unity AI Toolset Reference

This document provides a comprehensive, AI-optimized reference for all 61+ tools available in the `Nexus Unity` MCP server. These tools allow for full programmatic control over the Unity Editor.

> **Note for AI Agents**: All tools are prefixed with `unity_` when called via the Gemini CLI/MCP bridge.

---

## 🏗 Scene Management

### `unity_create_scene`
Creates a new scene with default GameObjects (Main Camera and Directional Light).
- **Parameters**:
  - `name` (string): The name of the new scene.
- **Returns**: Confirmation string.
- **Behavior**: Automatically saves the current scene if dirty.

### `unity_open_scene`
Opens an existing scene file.
- **Parameters**:
  - `path` (string, required): Asset path (e.g., `Assets/Scenes/Main.unity`).
- **Returns**: Confirmation string.

### `unity_save_scene`
Saves the currently active scene.
- **Parameters**:
  - `path` (string, optional): Specific save location. If empty, saves to current path.
- **Returns**: Confirmation string.

### `unity_list_scenes`
Lists all scene assets in the project.
- **Returns**: Array of scene asset paths.

---

## 🌳 Hierarchy & GameObject Lifecycle

### `unity_create_game_object`
Creates an empty GameObject.
- **Parameters**:
  - `name` (string, required): Name of the object.
  - `parent_id` (integer, optional): Instance ID of the parent object.
- **Returns**: Serialized GameObject (`name`, `instance_id`).

### `unity_create_primitive`
Creates a standard Unity primitive.
- **Parameters**:
  - `primitive_type` (string, required): One of `Cube`, `Sphere`, `Capsule`, `Cylinder`, `Plane`, `Quad`.
- **Returns**: Serialized GameObject.

### `unity_destroy_game_object`
Permanently deletes a GameObject from the scene.
- **Parameters**:
  - `instance_id` (integer, required): Target object ID.
- **Returns**: "Destroyed". Supports Undo.

### `unity_duplicate_object`
Creates a copy of a GameObject.
- **Parameters**:
  - `instance_id` (integer, required): Source object ID.
- **Returns**: Serialized copy GameObject.

### `unity_set_active`
Enables or disables a GameObject.
- **Parameters**:
  - `instance_id` (integer, required): Target object ID.
  - `active` (boolean, required): `true` to enable, `false` to disable.
- **Returns**: "OK".

### `unity_set_parent`
Changes the parent of a GameObject.
- **Parameters**:
  - `instance_id` (integer, required): Object to move.
  - `parent_id` (integer, required): New parent ID (use 0 for root).
- **Returns**: "Parent set".

### `unity_set_sibling_index`
Reorders an object among its siblings.
- **Parameters**:
  - `instance_id` (integer, required): Target object ID.
  - `index` (string|integer, required): Integer index or `"first"` / `"last"`.
- **Returns**: "OK".

### `unity_create_hierarchy`
Batch creates a full hierarchy of objects and components in one call.
- **Parameters**:
  - `tree` (object, required): JSON tree representing the hierarchy.
  - `parent_id` (integer, optional): Parent ID for the root of the tree.
- **Returns**: Serialized root GameObject.
- **Example Tree**: `{ "name": "Root", "components": ["Canvas"], "children": [{ "name": "Child", "components": ["Image"] }] }`

---

## 🧩 Component & Property Manipulation

### `unity_add_component`
Attaches a new component to a GameObject.
- **Parameters**:
  - `instance_id` (integer, required): Target object ID.
  - `component_name` (string, required): Name of the class (e.g., `Rigidbody`, `MeshRenderer`).
- **Returns**: Confirmation string.

### `unity_remove_component`
Removes a component from a GameObject.
- **Parameters**:
  - `instance_id` (integer, required): Target object ID.
  - `component_name` (string, required): Component type name.
- **Returns**: "OK".

### `unity_inspect_component`
Retrieves all visible properties and their current values.
- **Parameters**:
  - `instance_id` (integer, required): Target object ID.
  - `component_name` (string, required): Component type name.
- **Returns**: JSON object containing all serialized fields and their current values.

### `unity_get_component_schema`
Retrieves property names and types for a component (without current values).
- **Parameters**:
  - `instance_id` (integer, required): Target object ID.
  - `component_name` (string, required): Component type name.
- **Returns**: Array of property objects `{name, type, displayName}`.

### `unity_update_component`
Batch updates component properties using JSON with detailed result reporting.
- **Parameters**:
  - `instance_id` (integer, required): Target object ID.
  - `component_name` (string, required): Component type name.
  - `json_data` (string, required): JSON string matching the component's structure.
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
- **Returns**: The method's return value serialized as JSON, or `"Success"` if void.

### `unity_set_transform`
Surgical update of object position, rotation, or scale.
- **Parameters**:
  - `instance_id` (integer, required): Target object ID.
  - `position` (object, optional): `{x, y, z}` coordinates.
- **Returns**: Serialized GameObject.

### `unity_set_property`
Surgical update of a single field on a component or object.
- **Parameters**:
  - `instance_id` (integer, required): Target object ID.
  - `property_name` (string, required): The internal field name (e.g., `m_Enabled`, `m_Name`).
  - `value` (any, required): The new value. Supports bool, int, float, string, Vector3.
- **Returns**: "Property updated".

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
- **Returns**: Array of asset paths.

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
- **Returns**: Path to created material.

### `unity_refresh_asset_database`
Forces Unity to scan for changed files on disk and import them.
- **Returns**: Object with `status` ("Refreshed"), `is_compiling` (bool), and `is_updating` (bool).
- **AI Warning**: If `is_compiling` is true after this call, you MUST wait for it to become false (via `unity_get_editor_state`) before adding new components or scripts.

### `unity_import_asset`
Imports a file at the specified path into the project.
- **Parameters**:
  - `path` (string, required): Path to import.
- **Returns**: "Imported".

### `unity_instantiate_prefab`
Creates a prefab instance in the scene.
- **Parameters**:
  - `path` (string, required): Path to the prefab asset.
- **Returns**: Serialized GameObject.

### `unity_create_prefab`
Saves a GameObject as a prefab asset.
- **Parameters**:
  - `instance_id` (integer, required): Object to save.
  - `path` (string, required): Target asset path.
- **Returns**: Confirmation string.

### `unity_apply_prefab_overrides`
Applies scene modifications back to the prefab asset.
- **Parameters**:
  - `instance_id` (integer, required): Instance ID of the prefab in the scene.
- **Returns**: "Overrides applied".

### `unity_move_asset` / `unity_copy_asset` / `unity_delete_asset`
Standard file operations within the project.
- **Parameters**: `old_path`, `new_path`, `source_path`, `dest_path`, or `path`.
- **Returns**: "OK" or error.

### `unity_create_folder`
Creates a directory in the project.
- **Parameters**:
  - `path` (string, required): Path like `Assets/NewFolder/SubFolder`.
- **Returns**: "OK".

### `unity_read_file` / `unity_write_file`
Direct text file manipulation (limited to project root).
- **Parameters**: `path`, `content`.
- **Returns**: File content or "OK".

---

## 🎮 Editor Control & State

### `unity_undo` / `unity_redo`
Standard Editor undo/redo operations.

### `unity_toggle_play_mode`
Starts or stops the Unity Play Mode.
- **Parameters**:
  - `value` (boolean, optional): Set specific state. If omitted, toggles.

### `unity_pause_play_mode` / `unity_step_frame`
Controls playback while in Play Mode.

### `unity_execute_menu_item`
Triggers any Unity menu command.
- **Parameters**:
  - `item_path` (string, required): Full path (e.g., `Edit/Select All`, `GameObject/Align With View`).

### `unity_focus_scene_view`
Frames the current selection in the Scene View.

### `unity_read_logs`
Retrieves Unity Console logs.
- **Parameters**:
  - `count` (integer, optional): Max logs (Default: 50).
- **Returns**: Array of logs with type, message, and stack trace.

### `unity_attach_script`
Creates a new C# script and triggers compilation.
- **Parameters**:
  - `script_name` (string, required): Class name.
  - `script_content` (string, optional): Full C# code.
- **Returns**: "Script created and compilation triggered".

### `unity_wait_for_ready`
Simple ping tool that returns `true` once the Unity server is responsive.
- **Returns**: `true`.

---

## 🔍 Discovery & Inspection

### `unity_get_game_object`
Retrieves basic metadata for a specific ID.
- **Parameters**: `instance_id`.
- **Returns**: `{name, instance_id}`.

### `unity_get_active_game_object`
Returns the currently selected object in the Editor.

### `unity_get_root_game_objects`
Returns all top-level objects in the active scene.

### `unity_get_object_path`
Returns the hierarchy path (breadcrumb) to an object.
- **Example**: `Environment/Static/Tree_01`.

### `unity_find_objects`
Deep regex-based search for objects in the scene.
- **Parameters**:
  - `name` (string, optional): Regex for name.
  - `tag` (string, optional): Exact tag match.
  - `type` (string, optional): Component type filter.
- **Returns**: Array of GameObjects (max 50).

### `unity_find_references`
Finds which Assets and scene GameObjects reference a specific object. Essential for safe deletions and reorganizations.
- **Parameters** (Provide exactly one):
  - `target_guid` (string, optional): The GUID of the asset to search for.
  - `target_id` (integer, optional): The Instance ID of the object to search for.
- **Returns**: Object with `asset_references` (array of file paths) and `scene_references` (array of GameObjects with their components that hold the reference).

### `unity_find_by_path`
Search for a GameObject using its hierarchy path.
- **Parameters**:
  - `path` (string, required): Absolute or relative path.
- **Returns**: Serialized GameObject.
- **Example**: `Canvas/Main/Title`.

### `unity_get_tags_and_layers`
Lists all defined Tags and Layers in the project.

### `unity_get_children`
Returns all direct children of a GameObject.
- **Parameters**: `instance_id`.
- **Returns**: Array of objects `{name, instance_id, active}`.

### `unity_get_editor_state`
Returns current editor state flags.
- **Returns**: `{is_playing, is_paused, is_compiling, is_updating, active_scene, platform}`.

### `unity_get_project_info`
Returns basic project metadata.
- **Returns**: `{project_path, unity_version, platform, product_name, company_name}`.

---

## 🖥 UI Toolkit Automation

### `unity_ui_list_windows`
Lists all currently open Editor Windows.

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
- **Returns**: Array of matching VisualElements with their deep computed styles (`layout`, `computed_style`, `text`, `classes`).

### `unity_ui_click`
Simulates a click on a UI element.
- **Parameters**: `window_title`, `element_name`.

### `unity_ui_input_text`
Types text into a TextField or Label.
- **Parameters**: `window_title`, `element_name`, `text`.

## 🛡️ Linter & Compliance

### `unity_enforce_forced_defaults`
Enforces `[ForceDefault]` attribute values on all components of a GameObject.
- **Parameters**:
  - `instance_id` (integer, required): Target GameObject ID.
- **Returns**: Confirmation string with the count of enforced values.

### `unity_lint_project`
Runs a deterministic C# audit using Roslyn to enforce high code quality standards.
- **Returns**: JSON object with `report` (full text) and `violation_count`.
- **Note**: Generates `LINT_REPORT.txt` in the project root.

---
*Generated for Nexus Unity v2.1.1*
