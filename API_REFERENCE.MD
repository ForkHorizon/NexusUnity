# Nexus Unity API Reference

Version: `1.6.2`

Nexus Unity exposes two supported public API surfaces:

- Raw HTTP JSON-RPC: 117 base Unity Editor methods plus 4 canonical Nexus commands, for 121 visible `list_tools` names. The 4 `nexus_*` transport aliases remain dispatchable but hidden from the visible schema.
- MCP bridge: 14 consolidated `unity_` tools defined in `Editor/nexus_bridge/schemas.py`. `tools/list` advertises flat object schemas (`MCP_TOOLS`) without top-level `oneOf`, because Claude's tool API rejects top-level combinators; the action key is a required `enum`, and each action's required parameters are listed in its description.

## Raw HTTP JSON-RPC

Raw requests use unprefixed method names:

```bash
curl -s http://127.0.0.1:8081/ \
  -H 'Content-Type: application/json' \
  -H "X-Nexus-Unity-Token: $NEXUS_UNITY_AUTH_TOKEN" \
  -d '{"jsonrpc":"2.0","method":"get_server_status","params":{},"id":1}'
```

Call `list_tools` at runtime for full JSON schemas. Generated MCP configs set `NEXUS_UNITY_AUTH_TOKEN` for the Python bridge. The token persists across Unity restarts, and when it is missing or rejected the bridge rereads the project's `Library/NexusUnityAuthToken.txt`. MCP bridge aliases use the `unity_` prefix for the same raw method when directly routed.

Schema compatibility notes:

- `unity_write_and_compile` is the supported code-edit macro. Older references to `unity_apply_code_change` are stale and should be replaced.
- `update_component` accepts the preferred `properties` object and the legacy `json_data` string form.
- `create_scene` accepts optional `path` and `open_if_exists`, so agents can create or reopen a deterministic scene asset in one call.
- `create_primitive` accepts optional `name`, `parent_id`, `position`, `rotation`, `scale`, and `material_path` for visible non-origin object creation.
- Vector3 fields accept either `{ "x": 0, "y": 1, "z": 0 }` or `[0, 1, 0]`.
- `set_transform` accepts `position`, `rotation` / `eulerAngles`, and `scale` / `localScale`.
- `get_game_object` returns `transform.position`, `transform.rotation`, `transform.scale`, and compact `components` data for cheap read-back verification.
- Fast-path health calls (`get_server_status`, `attach_existing_session`, `wait_for_asset_import_idle`, and `wait_for_editor_idle`) read a published readiness snapshot because they may run on the listener thread. `get_server_status.readiness` and `is_editor_idle` share `ready`, `reason`, `reconnect`, and `epoch.compile` / `epoch.reload`. `wait_for_editor_idle` keeps `status` (`Ready`, `Timeout`, `Reloading`) and adds those fields. `Reloading` means the HTTP call stopped because a domain reload is armed; poll again after reconnect. `unity_wait` with `condition: compilation` re-polls `is_editor_idle` and does not call `refresh_asset_database` on its own.
- After script writes, readiness stays not-ready (`reason: script_write`, then `compiling` or `domain_reload`) until a compile that started after that write reloads and `epoch.reload` advances. A second write during an in-flight compile does not become ready on the older reload. A failed compile settles once the editor is quiet, with `epoch.compile` advanced and `epoch.reload` unchanged; the failure flag is read on the following editor tick because it is stale inside `compilationFinished`. A refresh that compiles nothing returns `reason: already_ready`. On macOS the script refresh waits until the Editor is focused, or 15 seconds, so an unfocused refresh does not settle as a no-op. `wait_for_ready` uses the same predicate and still returns a bare boolean.
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
- `nexus.project_map` / `unity_nexus.project_map`
- `nexus.group_compile_errors` / `unity_nexus.group_compile_errors`
- `nexus.capture_game_view` / `unity_nexus.capture_game_view`
- `nexus.asset_impact` / `unity_nexus.asset_impact`
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

`batch_execute` accepts at most 50 requests and rejects nested `batch_execute` calls. Asynchronous methods (`capture_game_view_screenshot`, `nexus.capture_game_view`) cannot run inside a batch and return an error asking you to call them directly.

### Screenshot results

`capture_game_view_screenshot` and `capture_inspector_screenshot` return `success`, `message`, and `duration_ms`, plus a `data` object containing `width`, `height`, `format`, and `image_base64`. The default Game View format remains `png`. Optional Game View parameters are `format` (`png` or `jpg`/`jpeg`), `quality` / `jpeg_quality` (1–100, JPEG only), `width`, `height`, `max_long_edge`, and `include_telemetry`. Output size is capped at 8192 pixels on the longest edge. The legacy top-level `status`, `format`, and successful `image_base64` fields remain available for existing raw clients; Inspector responses also retain `ui_layout`.

Canonical high-level commands `nexus.project_map`, `nexus.group_compile_errors`, `nexus.capture_game_view`, and `nexus.asset_impact` (and their `nexus_*` aliases) share one handler and description per command. Each returns the same envelope on Legacy HTTP and, when `com.unity.pipeline` is installed, from the optional Pipeline `[CliCommand]`. Those CLI parameters use the same name, description, order, and default as the canonical descriptor. `list_tools` still publishes type and description only.

```json
{"ok": true, "data": {}, "error": null, "meta": {"ms": 0, "mode": "legacy", "gen": 1, "request_id": null}}
```

`data` is the command payload. `nexus.capture_game_view` defaults to JPEG with a 768 px longest edge (`max_dimension` 1024 opts in; 0 keeps the source size). `view` is `game` or `scene`. A `target` name/path or `instance_id` crops Game View to that object, or frames the Scene View on it. `delivery:"file"` returns `file_path` and omits `base64`; the default returns `base64` only. `source` is `game_view` or `scene_view`. Aliases `nexus_look` and `nexus.look` call the same command and are not listed twice. `meta.gen` is the editor session generation (`sessionGeneration` on `get_server_status`), not `compile_epoch`. `meta.mode` is the effective runtime, `legacy` or `pipeline`. A string `request_id` argument is copied into `meta.request_id` and is not the JSON-RPC `id`. These reads are not cached.

### Asset impact (`nexus.asset_impact`)

Read-only static scan of Force Text `.prefab`, `.unity`, `.asset`, `.anim` and other text assets plus `.meta` guids and C# sources, including scenes and prefabs that are not loaded. It never imports, saves or reserializes. CLI: `unity command nexus asset_impact --op ... --type ... --name ...` (Pipeline 1.0.0+, read-only).

| Parameter | Meaning |
| --- | --- |
| `op` | `find_references` (default), `analyze_change_impact`, `validate_asset_integrity` |
| `change` | For `analyze_change_impact`: `rename_field`, `change_field_type`, `rename_method`, `delete_script`, `move_type` |
| `kind` | `type`, `field` or `method` (inferred when omitted) |
| `type` / `name` / `new_name` | C# type (full or simple name), member name, optional new name |
| `paths` | Comma-separated string or array of folders/files under `Assets/`, `Packages/` or `ProjectSettings/` (default `Assets`); symlinks are resolved and must stay in the project |
| `changed_scripts` | For `validate_asset_integrity`: only assets using these `.cs` files are checked |
| `max_results` | 1-5000 entries per list (default 500) |
| `level` | `static` (default, disk scan only), `editor` (also Layer B: load the assets in the Editor and report what Unity deserializes) or `auto` (Layer B only for assets the static scan left `unknown` or matched without `exact` confidence; no `editor` section when there are none) |

- `find_references` with `kind:"field"` returns `csharp_default`, `values` (one entry per stored component value, Prefab Variant / nested prefab / scene-instance `m_Modifications` override, and `AnimationClip` curve binding, each with `asset`, `file_id`, `object_path`, `source`, `value`, `differs_from_default`, `confidence`, `evidence.line`/`evidence.yaml`), `unique_values` and a one-line `summary` such as `default=100; overrides: Player.prefab/Player=250, Main.unity/Boss=1000`. `kind:"method"` returns UnityEvent `bindings`; `kind:"type"` returns `direct` usages and `via_prefab_instance`.
- `analyze_change_impact` and `validate_asset_integrity` return `findings` with `kind`, `severity` (`Error`, `Warning`, `Info`), `confidence` (`exact`, `strong`, `heuristic`), `consequence` and `recommended_check` (for example `[FormerlySerializedAs("maxHealth")]`), plus `counts`. `Error` is only ever reported with `exact` confidence. Integrity kinds: `missing_script`, `dangling_guid`, `broken_unityevent`.
- Every result lists `skipped` (binary assets, `UNSUPPORTED_SERIALIZATION`) and `unknown` (unparsable YAML), `complete`, `truncated_reason` (`max_files`, `max_bytes`, `max_ms`, `max_results`), `scanned` and `serialization_mode`. A project not in Force Text adds an `UNSUPPORTED_SERIALIZATION` warning and `complete: false`.
- `level: "editor"` / `"auto"` adds `editor` (read-only, main thread): `level`, `available` (`false` with `reason` such as `compiling`, `importing` or `not_main_thread`), `values` / `bindings` / `direct` / `findings` (by op and symbol kind), `unknown`, `complete` and `scanned` (`assets`, `objects`, `ms`). Field `values` carry `asset`, `object_path`, `class`, `source` (`component`, `prefab_modification` or `inherited` from a source prefab, via `SerializedProperty.prefabOverride`), `value` (written the way Force Text stores it: `0`/`1`, invariant numbers) and `property_type`; curve bindings have `source: "animation_curve"` and `property_exists`. `bindings` add `event`, `call_index`, `method`, `target_type`, `method_exists` and `source`. Editor integrity kinds: `missing_script`, `broken_unityevent`, `serialize_reference_missing_type`, `missing_animated_property`. Scenes are never opened: an unloaded `.unity` goes to `editor.unknown` with `scene_not_loaded_for_editor_validation`; an asset that fails to load gets `editor_load_failed`. Layer B does not check dangling guids (the static scan does).
- Raw `find_references` keeps its old target mode; passing `symbol: {kind, type, name}` (optional `scope`, `max_results`) runs this scan and returns its envelope.

`ok: false` is still a JSON-RPC `result`. `error` has `code`, `message`, `retryable`, and `hint`. Codes are `invalid_argument` (not retryable), `unavailable`, `busy`, `timeout` (a failed read, retryable), `capture_failed`, `partial` (compiler errors collected while the editor is compiling; `data` is kept and may be incomplete), `editor_not_ready`, and `internal` (not retryable). Read `ok` and `error`, not `data.success`. `data.success` only means the handler produced a payload. An unknown mutation outcome is `error.outcome: "unknown"` and is never retryable. Stack traces are omitted unless `NEXUS_DEBUG` is set. Legacy raw methods, including `capture_game_view_screenshot`, keep their current JSON. Transport failures such as parse errors and `Timeout waiting for Main Thread` stay JSON-RPC errors.

`get_server_status` includes an additive `runtime` object: `requested` and `effective` are `legacy`, `pipeline`, or `auto`; `eligible` is true when Unity 6000, the Pipeline package, Nexus command registration, a session port file, and a healthy loopback probe all pass. The Project Settings **Runtime** mode defaults to `auto`, which prefers Pipeline on eligible installs and Legacy otherwise. Set the mode to `legacy` to force HTTP immediately. Explicit `pipeline` does not silently fall back to HTTP. HTTP remains supported and is not removed.

`runtime.legacy` publishes the sunset ledger: `deprecated` is false until maturity gates pass, `still_supported` is true, `sunset_status` is `announced`, and `removal` is `not_scheduled`. Removal requires Unity CLI 1.0 stable, non-experimental `com.unity.pipeline`, and one Nexus stable release after Pipeline-primary `auto`. `runtime.unity_cli` is informational (`detected`, `version`, `prerelease`, `stable_1_0_or_newer`); the Editor does not shell out to `unity --version`. `runtime.pipeline` includes `detected`, `version`, `experimental`, `supported`, plus health/session fields. `runtime.legacy_unavailable_reason` is `foreign_project` when another Unity project owns port 8081.

`list_tools` accepts optional `profile`: `core`, `visual`, `scene`, or `compat`. Canonical command ids are listed once; `nexus_*` aliases stay dispatchable without being advertised twice.

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
Strategies: `regex`, `path`, `semantic`, `references`, `impact`.

`references` with `symbol: {kind, type, name}` (or flat `kind`/`type`/`name`) calls `nexus.asset_impact` `find_references`; without it the target mode (`target_id` / `target_guid`) is unchanged. `impact` calls `analyze_change_impact` and needs `change` and `type` (`name` for field and method changes). Both accept `paths` and `max_results`.

### `unity_asset_manager`
Actions: `search`, `explore`, `create_material`, `import`, `refresh`, `instantiate_prefab`, `create_prefab`, `apply_overrides`, `revert_overrides`.

`create_material` passes through `path`, `base_color` / `color`, and `emission_color`.

### `unity_editor_controller`
Actions: `undo`, `redo`, `play`, `pause`, `step`, `menu`, `read_logs`, `clear_logs`, `get_state`, `get_server_status`, `refresh_assets`, `run_tests`, `get_test_results`, `run_tests_wait`, `get_tool_usage_stats`, `reset_tool_usage_stats`.

`get_tool_usage_stats` returns in-memory call counts, average/total durations, `last_error_type`, and sanitized `last_error` with sensitive paths redacted; `reset_tool_usage_stats` resets the counters. Raw `run_tests` returns `status: "Submitted"` after Unity accepts the asynchronous request; it does not claim that execution has begun or completed. `run_tests_wait` triggers `run_tests` and polls raw `get_test_results` from the Python bridge so Unity's main thread is not blocked while the agent waits. If submission is rejected, `run_tests_wait` returns that result without polling.

### `unity_ui_automation`
Actions: `list_windows`, `get_hierarchy`, `query`, `get_window_rect`, `set_window_rect`, `capture_window_snapshot`, `click`, `input`.

`get_hierarchy` accepts `deep`, `max_depth`, and `max_elements` (with `children_truncated` and root `truncated` markers when capped). `query` accepts `name`, `text`, `class_name`, `max_depth`, and `max_results`. Window rect and snapshot actions are intended for resize/layout QA; window snapshots include rect and hierarchy (supporting `max_depth` and `max_elements`), with best-effort PNG image capture on macOS.

### `unity_wait`
Conditions: `compilation`, `play_mode`, `import`, `editor_idle`.

### `unity_playerprefs_manager`
Actions: `get`, `set`, `delete`, `list`.

Deleting every PlayerPrefs entry requires `action: "delete"`, `key: "all"`, and `confirm: true`; this operation is not undoable.

### `unity_write_and_compile`
Writes one or more files, waits for Unity compilation/import work, and returns compiler errors.

This is the public MCP bridge macro for code edits. Pass `confirm: true` when writing `.cs` files because Unity compilation is triggered. Use it instead of old internal or stale documentation references to `unity_apply_code_change`.

Raw `attach_script`, `write_file`, and `write_files_batch` calls also require `confirm: true` before writing `.cs` files.

### `delete_asset`
Deletes a file or folder in the project. Requires `path` and `confirm: true`. Deletions use `AssetDatabase.MoveAssetToTrash` to send items to the OS Trash. Deletion of `ProjectSettings/` or `Packages/` paths is forbidden.

### `unity_invoke_method`
Invokes a C# method on a component through reflection.

`arguments` is an optional positional JSON array. Omit it for parameterless methods.

### `unity_dump_scene_graph`
Returns a recursive tree of the active scene.

### `unity_get_scene_dependencies`
Returns a scene-wide dependency map.

### `unity_lint_project`
Runs the deterministic project audit. `scope: "assets"` instead runs `nexus.asset_impact` `validate_asset_integrity` (Missing Scripts, dangling guids, broken UnityEvent bindings; optional `paths`, `changed_scripts`, `max_results`).
