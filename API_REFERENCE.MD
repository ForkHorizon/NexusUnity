# Nexus Unity API Reference (v2.8.0)

Nexus Unity uses a consolidated Manager pattern to minimize token usage and prevent parameter ambiguity. Most tools are grouped under unified managers using strict JSON schemas.

## 🛠 Core Managers

### `unity_scene_manager`
- **action**: `create`, `open`, `save`, `list`.
- **name**: (create) Scene name.
- **path**: (open/save) Asset path.

### `unity_hierarchy_manager`
- **action**: `create_empty`, `create_primitive`, `destroy`, `duplicate`, `set_active`, `set_parent`, `set_sibling_index`.
- **instance_id**: Target object ID.
- **name**: (create) Object name.
- **primitive_type**: `Cube`, `Sphere`, `Capsule`, `Cylinder`, `Plane`, `Quad`.
- **active**: (set_active) boolean.
- **index**: (set_sibling_index) integer or `first`/`last`.

### `unity_component_manager`
- **action**: `add`, `remove`, `inspect`, `get_schema`, `update_properties`, `set_property`, `set_enabled`.
- **instance_id**: Target object ID.
- **component_name**: Type name (e.g., `MeshRenderer`).
- **properties**: (update_properties) JSON object.
- **property_name**: (set_property) Field name.
- **value**: (set_property) Stringified value.
- **enabled**: (set_enabled) boolean.

### `unity_search_manager`
- **strategy**: `regex`, `path`, `semantic`, `references`.
- **query**: Search string (regex, path, or semantic query).
- **target_id**: (references) Object ID.

### `unity_asset_manager`
- **action**: `search`, `explore`, `create_material`, `import`, `refresh`, `instantiate_prefab`, `create_prefab`, `apply_overrides`, `revert_overrides`.
- **filter**: (search) Asset search filter.
- **path**: Asset path.

---

## 🎮 Runtime & Editor Control

### `unity_editor_controller`
- **action**: `undo`, `redo`, `play`, `pause`, `step`, `menu`, `read_logs`, `clear_logs`.
- **state**: (play/pause) boolean.
- **item_path**: (menu) Menu item path.
- **count**: (read_logs) Number of logs.

### `unity_ui_automation`
- **action**: `list_windows`, `get_hierarchy`, `query`, `click`, `input`.
- **window_title**: Target window title.
- **element_name**: UI element name.
- **text**: (input/query) Text value.

### `unity_wait`
- **condition**: `compilation`, `play_mode`, `import`, `editor_idle`.
- **state**: (play_mode) Target state.
- **timeout_seconds**: Default 60.

### `unity_playerprefs_manager`
- **action**: `get`, `set`, `delete`, `list`.
- **key**: Pref key.
- **value**: Pref value.
- **type**: `string`, `int`, `float`.

---

## ⚡ Macros & Diagnostics

### `unity_write_and_compile` (Recommended for all code changes)
Writes multiple files, waits for domain reload, and returns compiler errors.
- **files**: Array of `{path, content}`.

### `unity_invoke_method`
Invoke a C# method via reflection.
- **instance_id**, **component_name**, **method_name**, **arguments**.

### `unity_dump_scene_graph` / `unity_get_scene_dependencies`
Deep scene analysis tools.

### `unity_lint_project`
Full C# project audit.
