# Nexus Unity API Reference

This reference is generated from the public Unity `list_tools` catalog used by the Python bridge fallback.

Public tool count: `111`

HTTP JSON-RPC method names are unprefixed. MCP bridge tool names use the `unity_` prefix.

Example HTTP call:

```bash
curl -s http://127.0.0.1:8081/ \
  -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","method":"get_server_status","params":{},"id":1}'
```

## Server Health And Protocol

- `get_server_status` / `unity_get_server_status`
- `attach_existing_session` / `unity_attach_existing_session`
- `ping_main_thread` / `unity_ping_main_thread`
- `shutdown_server` / `unity_shutdown_server`
- `batch_execute` / `unity_batch_execute`

## Scene And Hierarchy

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
- `get_root_game_objects` / `unity_get_root_game_objects`
- `get_children` / `unity_get_children`
- `get_object_path` / `unity_get_object_path`
- `find_by_path` / `unity_find_by_path`

## Components And Serialization

- `add_component` / `unity_add_component`
- `remove_component` / `unity_remove_component`
- `inspect_component` / `unity_inspect_component`
- `component_values` / `unity_component_values`
- `get_component_schema` / `unity_get_component_schema`
- `update_component` / `unity_update_component`
- `set_transform` / `unity_set_transform`
- `set_property` / `unity_set_property`
- `set_enabled` / `unity_set_enabled`
- `inspect_object` / `unity_inspect_object`
- `enforce_forced_defaults` / `unity_enforce_forced_defaults`

## Assets And Files

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

## Editor Control And Logs

- `undo` / `unity_undo`
- `redo` / `unity_redo`
- `toggle_play_mode` / `unity_toggle_play_mode`
- `pause_play_mode` / `unity_pause_play_mode`
- `step_frame` / `unity_step_frame`
- `execute_menu_item` / `unity_execute_menu_item`
- `focus_scene_view` / `unity_focus_scene_view`
- `read_logs` / `unity_read_logs`
- `read_logs_since_cursor` / `unity_read_logs_since_cursor`
- `clear_logs` / `unity_clear_logs`
- `attach_script` / `unity_attach_script`
- `wait_for_ready` / `unity_wait_for_ready`
- `run_tests` / `unity_run_tests`
- `lint_project` / `unity_lint_project`
- `get_editor_state` / `unity_get_editor_state`
- `get_project_info` / `unity_get_project_info`
- `set_selection` / `unity_set_selection`
- `get_tags_and_layers` / `unity_get_tags_and_layers`
- `ping_object` / `unity_ping_object`
- `get_active_game_object` / `unity_get_active_game_object`

## Search And Reflection

- `find_objects` / `unity_find_objects`
- `find_references` / `unity_find_references`
- `symbol_index` / `unity_symbol_index`
- `invoke_method` / `unity_invoke_method`
- `list_fields_for_type` / `unity_list_fields_for_type`

## ScriptableObjects And PlayerPrefs

- `read_scriptable_object` / `unity_read_scriptable_object`
- `update_scriptable_object` / `unity_update_scriptable_object`
- `patch_scriptable_object` / `unity_patch_scriptable_object`
- `create_scriptable_object_asset` / `unity_create_scriptable_object_asset`
- `duplicate_scriptable_object_asset` / `unity_duplicate_scriptable_object_asset`
- `diff_scriptable_objects` / `unity_diff_scriptable_objects`
- `diff_scriptable_object_against_defaults` / `unity_diff_scriptable_object_against_defaults`
- `get_player_pref` / `unity_get_player_pref`
- `set_player_pref` / `unity_set_player_pref`
- `delete_player_pref` / `unity_delete_player_pref`
- `list_player_prefs` / `unity_list_player_prefs`

## Sync And Runtime Input

- `wait_for_asset_import_idle` / `unity_wait_for_asset_import_idle`
- `wait_for_editor_idle` / `unity_wait_for_editor_idle`
- `simulate_mouse` / `unity_simulate_mouse`
- `simulate_touch` / `unity_simulate_touch`
- `click_object_in_game` / `unity_click_object_in_game`

## Snapshots And UI Toolkit

- `dump_scene_graph` / `unity_dump_scene_graph`
- `compact_scene_snapshot` / `unity_compact_scene_snapshot`
- `get_scene_dependencies` / `unity_get_scene_dependencies`
- `get_editor_timeline` / `unity_get_editor_timeline`
- `get_selected_object_full_context` / `unity_get_selected_object_full_context`
- `show_unresolved_missing_references` / `unity_show_unresolved_missing_references`
- `capture_inspector_screenshot` / `unity_capture_inspector_screenshot`
- `capture_game_view_screenshot` / `unity_capture_game_view_screenshot`
- `generate_mermaid_diagram` / `unity_generate_mermaid_diagram`
- `semantic_find` / `unity_semantic_find`
- `ui_list_windows` / `unity_ui_list_windows`
- `ui_get_hierarchy` / `unity_ui_get_hierarchy`
- `ui_query_elements` / `unity_ui_query_elements`
- `ui_click` / `unity_ui_click`
- `ui_input_text` / `unity_ui_input_text`
- `scene_delta` / `unity_scene_delta`

## Other Public Tools

- `close_prefab_stage` / `unity_close_prefab_stage`
- `get_game_object` / `unity_get_game_object`
- `open_prefab_stage` / `unity_open_prefab_stage`

## Notes

- Call `list_tools` at runtime for exact JSON schemas and current availability.
- Write operations run inside Unity and may require the editor to be idle.
- File operations are constrained to the Unity project root.
- `shutdown_server` stops the current local server instance and should be used only for diagnostics or cleanup.
