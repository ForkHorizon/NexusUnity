# Nexus Unity API Reference

Version: `1.4.2`

Nexus Unity exposes two supported public API surfaces:

- Raw HTTP JSON-RPC: 117 Unity Editor methods returned by `list_tools`.
- MCP bridge: 14 consolidated `unity_` tools defined in `Editor/nexus_bridge/schemas.py`.

## Raw HTTP JSON-RPC

Raw requests use unprefixed method names:

```bash
curl -s http://127.0.0.1:8081/ \
  -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","method":"get_server_status","params":{},"id":1}'
```

Call `list_tools` at runtime for full JSON schemas. MCP bridge aliases use the `unity_` prefix for the same raw method when directly routed.

Schema compatibility notes:

- `unity_write_and_compile` is the supported code-edit macro. Older references to `unity_apply_code_change` are stale and should be replaced.
- `update_component` accepts the preferred `properties` object and the legacy `json_data` string form.
- `create_scene` accepts optional `path` and `open_if_exists`, so agents can create or reopen a deterministic scene asset in one call.
- `create_primitive` accepts optional `name`, `parent_id`, `position`, `rotation`, `scale`, and `material_path` for visible non-origin object creation.
- Vector3 fields accept either `{ "x": 0, "y": 1, "z": 0 }` or `[0, 1, 0]`.
- `set_transform` accepts `position`, `rotation` / `eulerAngles`, and `scale` / `localScale`.
- `get_game_object` returns `transform.position`, `transform.rotation`, `transform.scale`, and compact `components` data for cheap read-back verification.
- After script writes, readiness probes remain busy until the scheduled asset refresh window has passed.
- During Play Mode transitions, readiness probes remain busy until Unity finishes entering or exiting Play Mode.
- `create_material` accepts optional `path`, `base_color` / `color`, and `emission_color` so callers can create visible materials in a chosen folder.
- `write_file` and `write_files_batch` create missing parent directories after path validation.
- `invoke_method.arguments` is an optional JSON array of positional arguments.
- `click_object_in_game` accepts either `instance_id` or hierarchy `path`.

### Raw Tool Catalog

- `scene_delta` / `unity_scene_delta`
- `symbol_index` / `unity_symbol_index`
- `invoke_method` / `unity_invoke_method`
- `get_selected_object_full_context` / `unity_get_selected_object_full_context`
- `show_unresolved_missing_references` / `unity_show_unresolved_missing_references`
- `get_editor_timeline` / `unity_get_editor_timeline`
- `dump_scene_graph` / `unity_dump_scene_graph`
- `compact_scene_snapshot` / `unity_compact_scene_snapshot`
- `get_scene_dependencies` / `unity_get_scene_dependencies`
- `get_server_status` / `unity_get_server_status`
- `attach_existing_session` / `unity_attach_existing_session`
- `ping_main_thread` / `unity_ping_main_thread`
- `get_tool_usage_stats` / `unity_get_tool_usage_stats`
- `reset_tool_usage_stats` / `unity_reset_tool_usage_stats`
- `shutdown_server` / `unity_shutdown_server`
- `batch_execute` / `unity_batch_execute`
- `simulate_mouse` / `unity_simulate_mouse`
- `simulate_touch` / `unity_simulate_touch`
- `click_object_in_game` / `unity_click_object_in_game`
- `wait_for_asset_import_idle` / `unity_wait_for_asset_import_idle`
- `wait_for_editor_idle` / `unity_wait_for_editor_idle`
- `read_scriptable_object` / `unity_read_scriptable_object`
- `update_scriptable_object` / `unity_update_scriptable_object`
- `patch_scriptable_object` / `unity_patch_scriptable_object`
- `create_scriptable_object_asset` / `unity_create_scriptable_object_asset`
- `duplicate_scriptable_object_asset` / `unity_duplicate_scriptable_object_asset`
- `list_fields_for_type` / `unity_list_fields_for_type`
- `diff_scriptable_objects` / `unity_diff_scriptable_objects`
- `diff_scriptable_object_against_defaults` / `unity_diff_scriptable_object_against_defaults`
- `get_player_pref` / `unity_get_player_pref`
- `set_player_pref` / `unity_set_player_pref`
- `delete_player_pref` / `unity_delete_player_pref`
- `list_player_prefs` / `unity_list_player_prefs`
- `capture_inspector_screenshot` / `unity_capture_inspector_screenshot`
- `capture_game_view_screenshot` / `unity_capture_game_view_screenshot`
- `generate_mermaid_diagram` / `unity_generate_mermaid_diagram`
- `semantic_find` / `unity_semantic_find`
- `enforce_forced_defaults` / `unity_enforce_forced_defaults`
- `inspect_object` / `unity_inspect_object`
- `lint_project` / `unity_lint_project`
- `create_scene` / `unity_create_scene`
- `open_scene` / `unity_open_scene`
- `save_scene` / `unity_save_scene`
- `list_scenes` / `unity_list_scenes`
- `create_game_object` / `unity_create_game_object`
- `create_primitive` / `unity_create_primitive`
- `destroy_game_object` / `unity_destroy_game_object`
- `duplicate_object` / `unity_duplicate_object`
- `set_active` / `unity_set_active`
- `set_parent` / `unity_set_parent`
- `set_sibling_index` / `unity_set_sibling_index`
- `create_hierarchy` / `unity_create_hierarchy`
- `add_component` / `unity_add_component`
- `remove_component` / `unity_remove_component`
- `inspect_component` / `unity_inspect_component`
- `component_values` / `unity_component_values`
- `get_component_schema` / `unity_get_component_schema`
- `update_component` / `unity_update_component`
- `set_transform` / `unity_set_transform`
- `set_property` / `unity_set_property`
- `set_enabled` / `unity_set_enabled`
- `list_assets` / `unity_list_assets`
- `explore_asset` / `unity_explore_asset`
- `create_material` / `unity_create_material`
- `refresh_asset_database` / `unity_refresh_asset_database`
- `import_asset` / `unity_import_asset`
- `instantiate_prefab` / `unity_instantiate_prefab`
- `create_prefab` / `unity_create_prefab`
- `apply_prefab_overrides` / `unity_apply_prefab_overrides`
- `revert_prefab_overrides` / `unity_revert_prefab_overrides`
- `get_prefab_overrides` / `unity_get_prefab_overrides`
- `edit_prefab_asset` / `unity_edit_prefab_asset`
- `move_asset` / `unity_move_asset`
- `delete_asset` / `unity_delete_asset`
- `copy_asset` / `unity_copy_asset`
- `get_dependencies` / `unity_get_dependencies`
- `create_folder` / `unity_create_folder`
- `read_file` / `unity_read_file`
- `write_file` / `unity_write_file`
- `write_files_batch` / `unity_write_files_batch`
- `undo` / `unity_undo`
- `redo` / `unity_redo`
- `toggle_play_mode` / `unity_toggle_play_mode`
- `pause_play_mode` / `unity_pause_play_mode`
- `step_frame` / `unity_step_frame`
- `execute_menu_item` / `unity_execute_menu_item`
- `focus_scene_view` / `unity_focus_scene_view`
- `open_prefab_stage` / `unity_open_prefab_stage`
- `close_prefab_stage` / `unity_close_prefab_stage`
- `read_logs` / `unity_read_logs`
- `read_logs_since_cursor` / `unity_read_logs_since_cursor`
- `clear_logs` / `unity_clear_logs`
- `attach_script` / `unity_attach_script`
- `wait_for_ready` / `unity_wait_for_ready`
- `run_tests` / `unity_run_tests`
- `get_test_results` / `unity_get_test_results`
- `get_game_object` / `unity_get_game_object`
- `get_active_game_object` / `unity_get_active_game_object`
- `get_root_game_objects` / `unity_get_root_game_objects`
- `get_object_path` / `unity_get_object_path`
- `find_objects` / `unity_find_objects`
- `find_by_path` / `unity_find_by_path`
- `find_references` / `unity_find_references`
- `get_tags_and_layers` / `unity_get_tags_and_layers`
- `ping_object` / `unity_ping_object`
- `get_children` / `unity_get_children`
- `get_editor_state` / `unity_get_editor_state`
- `get_project_info` / `unity_get_project_info`
- `set_selection` / `unity_set_selection`
- `ui_list_windows` / `unity_ui_list_windows`
- `ui_get_hierarchy` / `unity_ui_get_hierarchy`
- `ui_get_window_rect` / `unity_ui_get_window_rect`
- `ui_set_window_rect` / `unity_ui_set_window_rect`
- `ui_capture_window_snapshot` / `unity_ui_capture_window_snapshot`
- `ui_query_elements` / `unity_ui_query_elements`
- `ui_click` / `unity_ui_click`
- `ui_input_text` / `unity_ui_input_text`

`batch_execute` accepts at most 50 requests and rejects nested `batch_execute` calls.

## MCP Bridge Tools

The MCP bridge is the recommended AI-client surface. It exposes 14 tools and two static read-only resources through `resources/list` and `resources/read`.

### `unity_scene_manager`
Actions: `create`, `open`, `save`, `list`. Aliases: `create_scene`, `open_scene`, `save_scene`, `list_scenes`.

`create` accepts optional `path` and `open_if_exists` for deterministic scene assets.

### `unity_hierarchy_manager`
Actions: `create_empty`, `create_primitive`, `create_hierarchy`, `destroy`, `duplicate`, `rename`, `set_name`, `set_transform`, `set_active`, `set_parent`, `set_sibling_index`.

Aliases: `create`, `create_gameobject`, and `create_game_object` map to `create_empty`. `create_primitive` accepts `name`, `parent_id`, `position`, `rotation`, `scale`, and `material_path`; Vector3 fields accept either object or array form.

### `unity_component_manager`
Actions: `add`, `remove`, `inspect`, `get_schema`, `update_properties`, `set_property`, `set_enabled`.

### `unity_search_manager`
Strategies: `regex`, `path`, `semantic`, `references`.

### `unity_asset_manager`
Actions: `search`, `explore`, `create_material`, `import`, `refresh`, `instantiate_prefab`, `create_prefab`, `apply_overrides`, `revert_overrides`.

`create_material` passes through `path`, `base_color` / `color`, and `emission_color`.

### `unity_editor_controller`
Actions: `undo`, `redo`, `play`, `pause`, `step`, `menu`, `read_logs`, `clear_logs`, `get_state`, `get_server_status`, `refresh_assets`, `run_tests`, `get_test_results`, `run_tests_wait`, `get_tool_usage_stats`, `reset_tool_usage_stats`.

`run_tests_wait` triggers `run_tests` and polls raw `get_test_results` from the Python bridge so Unity's main thread is not blocked while the agent waits.

### `unity_ui_automation`
Actions: `list_windows`, `get_hierarchy`, `query`, `get_window_rect`, `set_window_rect`, `capture_window_snapshot`, `click`, `input`.

`get_hierarchy` accepts `deep`. `query` accepts `name`, `text`, and `class_name`. Window rect and snapshot actions are intended for resize/layout QA; window snapshots include rect and hierarchy, with best-effort PNG image capture on macOS.

### `unity_wait`
Conditions: `compilation`, `play_mode`, `import`, `editor_idle`.

### `unity_playerprefs_manager`
Actions: `get`, `set`, `delete`, `list`.

Deleting every PlayerPrefs entry requires `action: "delete"`, `key: "all"`, and `confirm: true`; this operation is not undoable.

### `unity_write_and_compile`
Writes one or more files, waits for Unity compilation/import work, and returns compiler errors.

This is the public MCP bridge macro for code edits. Pass `confirm: true` when writing `.cs` files because Unity compilation is triggered. Use it instead of old internal or stale documentation references to `unity_apply_code_change`.

Raw `attach_script`, `write_file`, and `write_files_batch` calls also require `confirm: true` before writing `.cs` files.

### `unity_invoke_method`
Invokes a C# method on a component through reflection.

`arguments` is an optional positional JSON array. Omit it for parameterless methods.

### `unity_dump_scene_graph`
Returns a recursive tree of the active scene.

### `unity_get_scene_dependencies`
Returns a scene-wide dependency map.

### `unity_lint_project`
Runs the deterministic project audit.
