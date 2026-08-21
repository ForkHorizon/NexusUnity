"""Tool-call routing for the NexusUnity Python bridge.

:func:`route_tool` is the single dispatch entry point: given a tool name (with
the ``unity_`` prefix already stripped) and an arguments dict, it forwards the
call to the appropriate Unity JSON-RPC method via :func:`.client.call_unity`.
"""

from __future__ import annotations

import time
from typing import Any
from collections.abc import Mapping, Sequence

from ._transport import call_unity
from .schemas import STATIC_TOOLS

JsonObject = dict[str, Any]
JsonRpcResponse = dict[str, Any]


def _compact(params: JsonObject) -> JsonObject:
    return {key: value for key, value in params.items() if value is not None}


def _alias(action_name: str | None, aliases: Mapping[str, str]) -> str | None:
    if action_name is None:
        return None
    return aliases.get(action_name, action_name)


def _invalid_action(action_name: str | None, valid_actions: Sequence[str]) -> JsonRpcResponse:
    valid = ", ".join(valid_actions)
    error_payload = {
        "code": -32602,
        "message": f"Invalid action: {action_name}. Valid actions: {valid}",
    }
    return {"error": error_payload}


def _result_object(response: JsonRpcResponse | None) -> JsonObject:
    if not response:
        return {}
    result_payload = response.get("result")
    return result_payload if isinstance(result_payload, dict) else {}


def _error_object(response: JsonRpcResponse | None) -> JsonObject | None:
    if not response:
        return None
    return response.get("error")


def _transform_params(args: JsonObject, instance_id: int | None = None) -> JsonObject:
    params: JsonObject = {"instance_id": instance_id if instance_id is not None else args.get("instance_id")}
    for key in ["position", "rotation", "scale", "eulerAngles", "localScale"]:
        params[key] = args.get(key)
    return _compact(params)


def _extract_created_instance_id(response: JsonRpcResponse) -> int | None:
    if "error" in response:
        return None
    result_payload = _result_object(response)
    data = result_payload.get("data", {})
    return data.get("instance_id") if isinstance(data, dict) else None


def _apply_created_transform(response: JsonRpcResponse, args: JsonObject) -> JsonRpcResponse:
    instance_id = _extract_created_instance_id(response)
    if instance_id is None:
        return response
    params = _transform_params(args, instance_id)
    if len(params) <= 1:
        return response
    transform_response = call_unity("set_transform", params)
    if transform_response and "error" in transform_response:
        return transform_response
    return response


def _run_tests_wait(args: JsonObject) -> JsonRpcResponse:
    timeout = args.get("timeout_seconds", 180)
    poll_interval = args.get("poll_interval_seconds", 1.0)
    start_time = time.time()

    previous_results_response = call_unity("get_test_results")
    previous_results_payload = _result_object(previous_results_response)
    previous_timestamp = (
        previous_results_payload.get("timestamp_utc") if previous_results_payload.get("status") == "Success" else None
    )

    run_params = _compact(
        {
            "mode": args.get("mode", "EditMode"),
            "filter": args.get("filter"),
        }
    )
    trigger_response = call_unity("run_tests", run_params)
    if trigger_response and "error" in trigger_response:
        return trigger_response

    trigger_payload = _result_object(trigger_response)
    if trigger_payload.get("status") not in ("Submitted", "Success"):
        return {"result": trigger_payload}

    result_path = trigger_payload.get("result_path")

    while time.time() - start_time < timeout:
        params = {"result_path": result_path} if result_path else {}
        current_results_response = call_unity("get_test_results", params)
        if current_results_response and "error" in current_results_response:
            return current_results_response

        current_results_payload = _result_object(current_results_response)
        if (
            current_results_payload.get("status") == "Success"
            and current_results_payload.get("timestamp_utc") != previous_timestamp
        ):
            test_results = dict(current_results_payload)
            test_results["time_waited_seconds"] = round(time.time() - start_time, 2)
            return {"result": test_results}

        time.sleep(poll_interval)

    timeout_result = {
        "status": "Timeout",
        "message": "Timed out waiting for a new Unity TestResults XML file.",
        "time_waited_seconds": round(time.time() - start_time, 2),
        "trigger": trigger_payload,
    }
    if isinstance(result_path, str):
        timeout_result["result_path"] = result_path
    return {"result": timeout_result}


def _wait_for_compilation(timeout: float, start_time: float | None = None) -> JsonRpcResponse:
    start_time = time.time() if start_time is None else start_time
    status: str = "Ready"

    reload_started: bool = False
    reload_wait_timeout = min(20.0, timeout)
    while time.time() - start_time < reload_wait_timeout:
        initialize_response = call_unity("initialize")
        if initialize_response is None or "error" in initialize_response:
            reload_started = True
            break
        time.sleep(0.5)

    if not reload_started:
        call_unity("refresh_asset_database")

    while time.time() - start_time < timeout:
        initialize_response = call_unity("initialize")
        if initialize_response and "result" in initialize_response:
            time.sleep(2.0)
            editor_state_response = call_unity("get_editor_state")
            editor_state = _result_object(editor_state_response)
            if (
                editor_state_response
                and "result" in editor_state_response
                and not editor_state.get("is_compiling")
                and not editor_state.get("is_updating")
            ):
                break
        time.sleep(1.0)
    else:
        status = "Timeout"

    wait_result = {
        "status": status,
        "time_waited_seconds": round(time.time() - start_time, 2),
    }
    return {"result": wait_result}


def _route_list_tools(_: JsonObject) -> JsonRpcResponse:
    return {"result": {"tools": STATIC_TOOLS}}


def _route_write_and_compile(args: JsonObject) -> JsonRpcResponse:
    files = args.get("files", [])
    confirm = args.get("confirm")
    start_time: float = time.time()
    call_unity("clear_logs")

    write_errors: list[JsonObject] = []
    for file_info in files:
        write_file_response = call_unity(
            "write_file",
            _compact({"path": file_info["path"], "content": file_info["content"], "confirm": confirm}),
        )
        write_error = _error_object(write_file_response)
        if write_error is not None:
            write_errors.append({"path": file_info["path"], "error": write_error})

    if write_errors:
        failure_result = {
            "status": "Failed",
            "message": "Failed to write some files",
            "errors": write_errors,
        }
        return {"result": failure_result}

    wait_response = _wait_for_compilation(timeout=90, start_time=start_time)
    wait_result = _result_object(wait_response)
    wait_status: str = wait_result["status"]
    time_waited_seconds: float = wait_result["time_waited_seconds"]

    compiler_errors: list[JsonObject] = []
    if wait_status == "Ready":
        log_response = call_unity("read_logs", {"count": 200})
        log_payload = _result_object(log_response)
        if log_response and "result" in log_response:
            for log_entry in log_payload.get("logs", []):
                if log_entry.get("Type") in ["Error", "Exception", "Assert"]:
                    compiler_errors.append(log_entry)

    success_result = {
        "status": "Failed" if compiler_errors else wait_status,
        "time_waited_seconds": time_waited_seconds,
        "compiler_errors": compiler_errors,
    }
    return {"result": success_result}


def _route_scene_manager(args: JsonObject) -> JsonRpcResponse:
    aliases = {"create_scene": "create", "open_scene": "open", "save_scene": "save", "list_scenes": "list"}
    action = _alias(args.get("action"), aliases)
    if action == "create":
        return call_unity(
            "create_scene",
            _compact(
                {"name": args.get("name"), "path": args.get("path"), "open_if_exists": args.get("open_if_exists")}
            ),
        )
    if action == "open":
        return call_unity("open_scene", {"path": args.get("path")})
    if action == "save":
        return call_unity("save_scene", {"path": args.get("path")})
    if action == "list":
        return call_unity("list_scenes")
    return _invalid_action(
        args.get("action"),
        ["create", "create_scene", "open", "open_scene", "save", "save_scene", "list", "list_scenes"],
    )


def _route_hierarchy_manager(args: JsonObject) -> JsonRpcResponse:
    aliases = {
        "create": "create_empty",
        "create_gameobject": "create_empty",
        "create_game_object": "create_empty",
        "rename": "set_name",
        "transform": "set_transform",
    }
    action = _alias(args.get("action"), aliases)
    if action == "create_empty":
        response = call_unity(
            "create_game_object", _compact({"name": args.get("name"), "parent_id": args.get("parent_id")})
        )
        return _apply_created_transform(response, args)
    if action == "create_primitive":
        return call_unity(
            "create_primitive",
            _compact(
                {
                    "primitive_type": args.get("primitive_type"),
                    "name": args.get("name"),
                    "parent_id": args.get("parent_id"),
                    "position": args.get("position"),
                    "rotation": args.get("rotation"),
                    "scale": args.get("scale"),
                    "material_path": args.get("material_path"),
                }
            ),
        )
    if action == "create_hierarchy":
        return call_unity("create_hierarchy", _compact({"tree": args.get("tree"), "parent_id": args.get("parent_id")}))
    if action == "set_name":
        return call_unity(
            "set_property",
            {
                "instance_id": args.get("instance_id"),
                "property_name": "m_Name",
                "value": args.get("name") or args.get("new_name"),
            },
        )
    if action == "set_transform":
        return call_unity("set_transform", _transform_params(args))
    if action == "destroy":
        return call_unity("destroy_game_object", {"instance_id": args.get("instance_id")})
    if action == "duplicate":
        return call_unity("duplicate_object", {"instance_id": args.get("instance_id")})
    if action == "set_active":
        return call_unity("set_active", {"instance_id": args.get("instance_id"), "active": args.get("active")})
    if action == "set_parent":
        return call_unity("set_parent", {"instance_id": args.get("instance_id"), "parent_id": args.get("parent_id")})
    if action == "set_sibling_index":
        return call_unity("set_sibling_index", {"instance_id": args.get("instance_id"), "index": args.get("index")})
    return _invalid_action(
        args.get("action"),
        [
            "create_empty",
            "create",
            "create_gameobject",
            "create_game_object",
            "create_primitive",
            "create_hierarchy",
            "destroy",
            "duplicate",
            "rename",
            "set_name",
            "set_transform",
            "set_active",
            "set_parent",
            "set_sibling_index",
        ],
    )


def _route_component_manager(args: JsonObject) -> JsonRpcResponse:
    action = args.get("action")
    if action == "add":
        return call_unity(
            "add_component", {"instance_id": args.get("instance_id"), "component_name": args.get("component_name")}
        )
    if action == "remove":
        return call_unity(
            "remove_component", {"instance_id": args.get("instance_id"), "component_name": args.get("component_name")}
        )
    if action == "inspect":
        return call_unity(
            "inspect_component", {"instance_id": args.get("instance_id"), "component_name": args.get("component_name")}
        )
    if action == "get_schema":
        return call_unity(
            "get_component_schema",
            {"instance_id": args.get("instance_id"), "component_name": args.get("component_name")},
        )
    if action == "update_properties":
        return call_unity(
            "update_component",
            {
                "instance_id": args.get("instance_id"),
                "component_name": args.get("component_name"),
                "properties": args.get("properties"),
            },
        )
    if action == "set_property":
        return call_unity(
            "set_property",
            {
                "instance_id": args.get("instance_id"),
                "property_name": args.get("property_name"),
                "value": args.get("value"),
            },
        )
    if action == "set_enabled":
        return call_unity(
            "set_enabled",
            {
                "instance_id": args.get("instance_id"),
                "component_name": args.get("component_name"),
                "enabled": args.get("enabled"),
            },
        )
    return _invalid_action(
        action, ["add", "remove", "inspect", "get_schema", "update_properties", "set_property", "set_enabled"]
    )


def _route_search_manager(args: JsonObject) -> JsonRpcResponse:
    strategy = args.get("strategy")
    if strategy == "regex":
        return call_unity("find_objects", {"name": args.get("query"), "tag": args.get("tag"), "type": args.get("type")})
    if strategy == "path":
        return call_unity("find_by_path", {"path": args.get("query")})
    if strategy == "semantic":
        return call_unity("semantic_find", {"query": args.get("query")})
    if strategy == "references":
        return call_unity(
            "find_references", {"target_id": args.get("target_id"), "target_guid": args.get("target_guid")}
        )
    return {
        "error": {
            "code": -32602,
            "message": f"Invalid strategy: {strategy}. Valid strategies: regex, path, semantic, references",
        }
    }


def _route_asset_manager(args: JsonObject) -> JsonRpcResponse:
    action = args.get("action")
    if action == "search":
        return call_unity("list_assets", {"filter": args.get("filter")})
    if action == "explore":
        return call_unity("explore_asset", {"path": args.get("path")})
    if action == "create_material":
        return call_unity(
            "create_material",
            _compact(
                {
                    "name": args.get("name"),
                    "shader": args.get("shader"),
                    "path": args.get("path"),
                    "base_color": args.get("base_color") or args.get("color"),
                    "emission_color": args.get("emission_color") or args.get("emission"),
                }
            ),
        )
    if action == "import":
        return call_unity("import_asset", {"path": args.get("path")})
    if action == "refresh":
        return call_unity("refresh_asset_database")
    if action == "instantiate_prefab":
        return call_unity("instantiate_prefab", {"path": args.get("path")})
    if action == "create_prefab":
        return call_unity("create_prefab", {"instance_id": args.get("instance_id"), "path": args.get("path")})
    if action == "apply_overrides":
        return call_unity("apply_prefab_overrides", {"instance_id": args.get("instance_id")})
    if action == "revert_overrides":
        return call_unity("revert_prefab_overrides", {"instance_id": args.get("instance_id")})
    return _invalid_action(
        action,
        [
            "search",
            "explore",
            "create_material",
            "import",
            "refresh",
            "instantiate_prefab",
            "create_prefab",
            "apply_overrides",
            "revert_overrides",
        ],
    )


def _route_editor_controller(args: JsonObject) -> JsonRpcResponse:
    action = args.get("action")
    if action == "undo":
        return call_unity("undo")
    if action == "redo":
        return call_unity("redo")
    if action == "play":
        return call_unity("toggle_play_mode", {"value": args.get("state")})
    if action == "pause":
        return call_unity("pause_play_mode", {"value": args.get("state")})
    if action == "step":
        return call_unity("step_frame")
    if action == "menu":
        return call_unity("execute_menu_item", {"item_path": args.get("item_path")})
    if action == "read_logs":
        return call_unity("read_logs", {"count": args.get("count", 100)})
    if action == "clear_logs":
        return call_unity("clear_logs")
    if action == "get_state":
        return call_unity("get_editor_state")
    if action == "get_server_status":
        return call_unity("get_server_status")
    if action == "refresh_assets":
        return call_unity("refresh_asset_database")
    if action == "run_tests":
        return call_unity("run_tests", _compact({"mode": args.get("mode", "EditMode"), "filter": args.get("filter")}))
    if action == "get_test_results":
        return call_unity("get_test_results", _compact({"result_path": args.get("result_path")}))
    if action == "run_tests_wait":
        return _run_tests_wait(args)
    if action == "get_tool_usage_stats":
        return call_unity("get_tool_usage_stats")
    if action == "reset_tool_usage_stats":
        return call_unity("reset_tool_usage_stats")
    return _invalid_action(
        action,
        [
            "undo",
            "redo",
            "play",
            "pause",
            "step",
            "menu",
            "read_logs",
            "clear_logs",
            "get_state",
            "get_server_status",
            "refresh_assets",
            "run_tests",
            "get_test_results",
            "run_tests_wait",
            "get_tool_usage_stats",
            "reset_tool_usage_stats",
        ],
    )


def _route_ui_automation(args: JsonObject) -> JsonRpcResponse:
    action = args.get("action")
    if action == "list_windows":
        return call_unity("ui_list_windows")
    if action == "get_hierarchy":
        return call_unity(
            "ui_get_hierarchy", _compact({"window_title": args.get("window_title"), "deep": args.get("deep")})
        )
    if action == "query":
        return call_unity(
            "ui_query_elements",
            _compact(
                {
                    "window_title": args.get("window_title"),
                    "name": args.get("name"),
                    "text": args.get("text"),
                    "class_name": args.get("class_name"),
                }
            ),
        )
    if action == "get_window_rect":
        return call_unity("ui_get_window_rect", {"window_title": args.get("window_title")})
    if action == "set_window_rect":
        return call_unity(
            "ui_set_window_rect",
            _compact(
                {
                    "window_title": args.get("window_title"),
                    "x": args.get("x"),
                    "y": args.get("y"),
                    "width": args.get("width"),
                    "height": args.get("height"),
                }
            ),
        )
    if action == "capture_window_snapshot":
        return call_unity(
            "ui_capture_window_snapshot",
            _compact(
                {
                    "window_title": args.get("window_title"),
                    "include_image": args.get("include_image"),
                    "include_hierarchy": args.get("include_hierarchy"),
                }
            ),
        )
    if action == "click":
        return call_unity(
            "ui_click", {"window_title": args.get("window_title"), "element_name": args.get("element_name")}
        )
    if action == "input":
        return call_unity(
            "ui_input_text",
            {
                "window_title": args.get("window_title"),
                "element_name": args.get("element_name"),
                "text": args.get("text"),
            },
        )
    return _invalid_action(
        action,
        [
            "list_windows",
            "get_hierarchy",
            "query",
            "get_window_rect",
            "set_window_rect",
            "capture_window_snapshot",
            "click",
            "input",
        ],
    )


def _route_playerprefs_manager(args: JsonObject) -> JsonRpcResponse:
    action = args.get("action")
    if action == "get":
        return call_unity("get_player_pref", {"key": args.get("key"), "type": args.get("type", "string")})
    if action == "set":
        return call_unity(
            "set_player_pref", {"key": args.get("key"), "value": args.get("value"), "type": args.get("type", "string")}
        )
    if action == "delete":
        return call_unity("delete_player_pref", _compact({"key": args.get("key"), "confirm": args.get("confirm")}))
    if action == "list":
        return call_unity("list_player_prefs")
    return _invalid_action(action, ["get", "set", "delete", "list"])


def _route_wait(args: JsonObject) -> JsonRpcResponse:
    condition: Any = args.get("condition")
    timeout: float = args.get("timeout_seconds", 60)
    start_time: float = time.time()
    status: str = "Ready"

    if condition == "compilation":
        return _wait_for_compilation(timeout=timeout, start_time=start_time)
    if condition == "play_mode":
        target_state = args.get("state", True)
        while time.time() - start_time < timeout:
            editor_state_response = call_unity("get_editor_state")
            editor_state = _result_object(editor_state_response)
            if (
                editor_state_response
                and "result" in editor_state_response
                and editor_state.get("is_playing") == target_state
            ):
                break
            time.sleep(1.0)
        else:
            status = "Timeout"
    elif condition == "import":
        while time.time() - start_time < timeout:
            import_idle_response = call_unity("is_asset_import_idle")
            import_idle_state = _result_object(import_idle_response)
            if import_idle_response and "result" in import_idle_response and import_idle_state.get("is_idle"):
                break
            time.sleep(1.0)
        else:
            status = "Timeout"
    elif condition == "editor_idle":
        while time.time() - start_time < timeout:
            editor_idle_response = call_unity("is_editor_idle")
            editor_idle_state = _result_object(editor_idle_response)
            if editor_idle_response and "result" in editor_idle_response and editor_idle_state.get("is_idle"):
                break
            time.sleep(1.0)
        else:
            status = "Timeout"

    wait_result = {
        "status": status,
        "time_waited_seconds": round(time.time() - start_time, 2),
    }
    return {"result": wait_result}


_HANDLERS = {
    "tools/list": _route_list_tools,
    "list_tools": _route_list_tools,
    "listTools": _route_list_tools,
    "write_and_compile": _route_write_and_compile,
    "scene_manager": _route_scene_manager,
    "hierarchy_manager": _route_hierarchy_manager,
    "component_manager": _route_component_manager,
    "search_manager": _route_search_manager,
    "asset_manager": _route_asset_manager,
    "editor_controller": _route_editor_controller,
    "ui_automation": _route_ui_automation,
    "playerprefs_manager": _route_playerprefs_manager,
    "wait": _route_wait,
}


def route_tool(name: str, args: JsonObject) -> JsonRpcResponse:
    handler = _HANDLERS.get(name)
    if handler is not None:
        return handler(args)
    return call_unity(name, args)
