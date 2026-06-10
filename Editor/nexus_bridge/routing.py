"""Tool-call routing for the NexusUnity Python bridge.

:func:`route_tool` is the single dispatch entry point: given a tool name (with
the ``unity_`` prefix already stripped) and an arguments dict, it forwards the
call to the appropriate Unity JSON-RPC method via :func:`.client.call_unity`.
"""
from __future__ import annotations

import time
from typing import Any

from .client import call_unity, logger
from .schemas import STATIC_TOOLS


def _compact(params: dict[str, Any]) -> dict[str, Any]:
    return {key: value for key, value in params.items() if value is not None}


def _alias(action: str | None, aliases: dict[str, str]) -> str | None:
    return aliases.get(action, action)  # type: ignore[arg-type]


def _invalid_action(action: str | None, valid_actions: list[str]) -> dict[str, Any]:
    valid = ", ".join(valid_actions)
    return {"error": {"code": -32602, "message": f"Invalid action: {action}. Valid actions: {valid}"}}


def _transform_params(args: dict[str, Any], instance_id: int | None = None) -> dict[str, Any]:
    params: dict[str, Any] = {"instance_id": instance_id if instance_id is not None else args.get("instance_id")}
    for key in ["position", "rotation", "scale", "eulerAngles", "localScale"]:
        params[key] = args.get(key)
    return _compact(params)


def _extract_created_instance_id(response: dict[str, Any]) -> int | None:
    if not isinstance(response, dict) or "error" in response:
        return None
    result = response.get("result", {})
    data = result.get("data", {}) if isinstance(result, dict) else {}
    return data.get("instance_id") if isinstance(data, dict) else None


def _apply_created_transform(response: dict[str, Any], args: dict[str, Any]) -> dict[str, Any]:
    instance_id = _extract_created_instance_id(response)
    if not instance_id:
        return response
    params = _transform_params(args, instance_id)
    if len(params) <= 1:
        return response
    transform = call_unity("set_transform", params)
    if transform and "error" in transform:
        return transform
    return response


def _run_tests_wait(args: dict[str, Any]) -> dict[str, Any]:
    timeout = args.get("timeout_seconds", 180)
    poll_interval = args.get("poll_interval_seconds", 1.0)
    start_time = time.time()

    before = call_unity("get_test_results")
    before_result = before.get("result", {}) if isinstance(before, dict) else {}
    before_timestamp = before_result.get("timestamp_utc") if before_result.get("status") == "Success" else None

    run_params = _compact({
        "mode": args.get("mode", "EditMode"),
        "filter": args.get("filter"),
    })
    trigger = call_unity("run_tests", run_params)
    if trigger and "error" in trigger:
        return trigger

    trigger_result = trigger.get("result", {}) if isinstance(trigger, dict) else {}
    result_path = trigger_result.get("result_path")

    while time.time() - start_time < timeout:
        params = {"result_path": result_path} if result_path else {}
        current = call_unity("get_test_results", params)
        if current and "error" in current:
            return current

        result = current.get("result", {}) if isinstance(current, dict) else {}
        if result.get("status") == "Success" and result.get("timestamp_utc") != before_timestamp:
            result["time_waited_seconds"] = round(time.time() - start_time, 2)
            return {"result": result}

        time.sleep(poll_interval)

    return {
        "result": {
            "status": "Timeout",
            "message": "Timed out waiting for a new Unity TestResults XML file.",
            "time_waited_seconds": round(time.time() - start_time, 2),
            "result_path": result_path,
            "trigger": trigger_result,
        }
    }


def route_tool(name: str, args: dict[str, Any]) -> dict[str, Any]:
    if name in ["tools/list", "list_tools", "listTools"]:
        return {"result": {"tools": STATIC_TOOLS}}

    if name == "write_and_compile":
        files = args.get("files", [])
        start_time = time.time()
        call_unity("clear_logs")

        write_errors = []
        for f in files:
            res = call_unity("write_file", {"path": f["path"], "content": f["content"]})
            if res and "error" in res:
                write_errors.append({"path": f["path"], "error": res["error"]})

        if write_errors:
            return {"result": {"status": "Failed", "message": "Failed to write some files", "errors": write_errors}}
        else:
            timeout = 90
            reload_started = False
            while time.time() - start_time < 20:
                res = call_unity("initialize")
                if res is None or "error" in res:
                    reload_started = True
                    break
                time.sleep(0.5)

            if not reload_started:
                call_unity("refresh_asset_database")

            status = "Ready"
            while time.time() - start_time < timeout:
                res = call_unity("initialize")
                if res and "result" in res:
                    time.sleep(2.0)
                    state = call_unity("get_editor_state")
                    if state and "result" in state:
                        if not state["result"].get("is_compiling") and not state["result"].get("is_updating"):
                            break
                time.sleep(1.0)
            else:
                status = "Timeout"

            compiler_errors = []
            if status == "Ready":
                log_res = call_unity("read_logs", {"count": 200})
                if log_res and "result" in log_res:
                    for l in log_res["result"].get("logs", []):
                        if l.get("Type") in ["Error", "Exception", "Assert"]:
                            compiler_errors.append(l)

            return {
                "result": {
                    "status": "Failed" if compiler_errors else status,
                    "time_waited_seconds": round(time.time() - start_time, 2),
                    "compiler_errors": compiler_errors
                }
            }

    elif name == "scene_manager":
        aliases = {"create_scene": "create", "open_scene": "open", "save_scene": "save", "list_scenes": "list"}
        action = _alias(args.get("action"), aliases)
        if action == "create":
            return call_unity("create_scene", _compact({"name": args.get("name"), "path": args.get("path"), "open_if_exists": args.get("open_if_exists")}))
        elif action == "open":
            return call_unity("open_scene", {"path": args.get("path")})
        elif action == "save":
            return call_unity("save_scene", {"path": args.get("path")})
        elif action == "list":
            return call_unity("list_scenes")
        else:
            return _invalid_action(args.get("action"), ["create", "create_scene", "open", "open_scene", "save", "save_scene", "list", "list_scenes"])

    elif name == "hierarchy_manager":
        aliases = {
            "create": "create_empty",
            "create_gameobject": "create_empty",
            "create_game_object": "create_empty",
            "rename": "set_name",
            "transform": "set_transform",
        }
        action = _alias(args.get("action"), aliases)
        if action == "create_empty":
            res = call_unity("create_game_object", _compact({"name": args.get("name"), "parent_id": args.get("parent_id")}))
            return _apply_created_transform(res, args)
        elif action == "create_primitive":
            return call_unity("create_primitive", _compact({
                "primitive_type": args.get("primitive_type"),
                "name": args.get("name"),
                "parent_id": args.get("parent_id"),
                "position": args.get("position"),
                "rotation": args.get("rotation"),
                "scale": args.get("scale"),
                "material_path": args.get("material_path"),
            }))
        elif action == "create_hierarchy":
            return call_unity("create_hierarchy", _compact({"tree": args.get("tree"), "parent_id": args.get("parent_id")}))
        elif action == "set_name":
            return call_unity("set_property", {"instance_id": args.get("instance_id"), "property_name": "m_Name", "value": args.get("name") or args.get("new_name")})
        elif action == "set_transform":
            return call_unity("set_transform", _transform_params(args))
        elif action == "destroy": return call_unity("destroy_game_object", {"instance_id": args.get("instance_id")})
        elif action == "duplicate": return call_unity("duplicate_object", {"instance_id": args.get("instance_id")})
        elif action == "set_active": return call_unity("set_active", {"instance_id": args.get("instance_id"), "active": args.get("active")})
        elif action == "set_parent": return call_unity("set_parent", {"instance_id": args.get("instance_id"), "parent_id": args.get("parent_id")})
        elif action == "set_sibling_index": return call_unity("set_sibling_index", {"instance_id": args.get("instance_id"), "index": args.get("index")})
        else:
            return _invalid_action(args.get("action"), ["create_empty", "create", "create_gameobject", "create_game_object", "create_primitive", "create_hierarchy", "destroy", "duplicate", "rename", "set_name", "set_transform", "set_active", "set_parent", "set_sibling_index"])

    elif name == "component_manager":
        action = args.get("action")
        if action == "add": return call_unity("add_component", {"instance_id": args.get("instance_id"), "component_name": args.get("component_name")})
        elif action == "remove": return call_unity("remove_component", {"instance_id": args.get("instance_id"), "component_name": args.get("component_name")})
        elif action == "inspect": return call_unity("inspect_component", {"instance_id": args.get("instance_id"), "component_name": args.get("component_name")})
        elif action == "get_schema": return call_unity("get_component_schema", {"instance_id": args.get("instance_id"), "component_name": args.get("component_name")})
        elif action == "update_properties": return call_unity("update_component", {"instance_id": args.get("instance_id"), "component_name": args.get("component_name"), "properties": args.get("properties")})
        elif action == "set_property": return call_unity("set_property", {"instance_id": args.get("instance_id"), "property_name": args.get("property_name"), "value": args.get("value")})
        elif action == "set_enabled": return call_unity("set_enabled", {"instance_id": args.get("instance_id"), "component_name": args.get("component_name"), "enabled": args.get("enabled")})
        else: return _invalid_action(action, ["add", "remove", "inspect", "get_schema", "update_properties", "set_property", "set_enabled"])

    elif name == "search_manager":
        strategy = args.get("strategy")
        if strategy == "regex": return call_unity("find_objects", {"name": args.get("query"), "tag": args.get("tag"), "type": args.get("type")})
        elif strategy == "path": return call_unity("find_by_path", {"path": args.get("query")})
        elif strategy == "semantic": return call_unity("semantic_find", {"query": args.get("query")})
        elif strategy == "references": return call_unity("find_references", {"target_id": args.get("target_id"), "target_guid": args.get("target_guid")})
        else: return {"error": {"code": -32602, "message": f"Invalid strategy: {strategy}. Valid strategies: regex, path, semantic, references"}}

    elif name == "asset_manager":
        action = args.get("action")
        if action == "search": return call_unity("list_assets", {"filter": args.get("filter")})
        elif action == "explore": return call_unity("explore_asset", {"path": args.get("path")})
        elif action == "create_material":
            return call_unity("create_material", _compact({
                "name": args.get("name"),
                "shader": args.get("shader"),
                "path": args.get("path"),
                "base_color": args.get("base_color") or args.get("color"),
                "emission_color": args.get("emission_color") or args.get("emission"),
            }))
        elif action == "import": return call_unity("import_asset", {"path": args.get("path")})
        elif action == "refresh": return call_unity("refresh_asset_database")
        elif action == "instantiate_prefab": return call_unity("instantiate_prefab", {"path": args.get("path")})
        elif action == "create_prefab": return call_unity("create_prefab", {"instance_id": args.get("instance_id"), "path": args.get("path")})
        elif action == "apply_overrides": return call_unity("apply_prefab_overrides", {"instance_id": args.get("instance_id")})
        elif action == "revert_overrides": return call_unity("revert_prefab_overrides", {"instance_id": args.get("instance_id")})
        else: return _invalid_action(action, ["search", "explore", "create_material", "import", "refresh", "instantiate_prefab", "create_prefab", "apply_overrides", "revert_overrides"])

    elif name == "editor_controller":
        action = args.get("action")
        if action == "undo": return call_unity("undo")
        elif action == "redo": return call_unity("redo")
        elif action == "play": return call_unity("toggle_play_mode", {"value": args.get("state")})
        elif action == "pause": return call_unity("pause_play_mode", {"value": args.get("state")})
        elif action == "step": return call_unity("step_frame")
        elif action == "menu": return call_unity("execute_menu_item", {"item_path": args.get("item_path")})
        elif action == "read_logs": return call_unity("read_logs", {"count": args.get("count", 100)})
        elif action == "clear_logs": return call_unity("clear_logs")
        elif action == "get_state": return call_unity("get_editor_state")
        elif action == "get_server_status": return call_unity("get_server_status")
        elif action == "refresh_assets": return call_unity("refresh_asset_database")
        elif action == "run_tests": return call_unity("run_tests", _compact({"mode": args.get("mode", "EditMode"), "filter": args.get("filter")}))
        elif action == "get_test_results": return call_unity("get_test_results", _compact({"result_path": args.get("result_path")}))
        elif action == "run_tests_wait": return _run_tests_wait(args)
        elif action == "get_tool_usage_stats": return call_unity("get_tool_usage_stats")
        elif action == "reset_tool_usage_stats": return call_unity("reset_tool_usage_stats")
        else: return _invalid_action(action, ["undo", "redo", "play", "pause", "step", "menu", "read_logs", "clear_logs", "get_state", "get_server_status", "refresh_assets", "run_tests", "get_test_results", "run_tests_wait", "get_tool_usage_stats", "reset_tool_usage_stats"])

    elif name == "ui_automation":
        action = args.get("action")
        if action == "list_windows": return call_unity("ui_list_windows")
        elif action == "get_hierarchy": return call_unity("ui_get_hierarchy", _compact({"window_title": args.get("window_title"), "deep": args.get("deep")}))
        elif action == "query": return call_unity("ui_query_elements", _compact({"window_title": args.get("window_title"), "name": args.get("name"), "text": args.get("text"), "class_name": args.get("class_name")}))
        elif action == "get_window_rect": return call_unity("ui_get_window_rect", {"window_title": args.get("window_title")})
        elif action == "set_window_rect": return call_unity("ui_set_window_rect", _compact({"window_title": args.get("window_title"), "x": args.get("x"), "y": args.get("y"), "width": args.get("width"), "height": args.get("height")}))
        elif action == "capture_window_snapshot": return call_unity("ui_capture_window_snapshot", _compact({"window_title": args.get("window_title"), "include_image": args.get("include_image"), "include_hierarchy": args.get("include_hierarchy")}))
        elif action == "click": return call_unity("ui_click", {"window_title": args.get("window_title"), "element_name": args.get("element_name")})
        elif action == "input": return call_unity("ui_input_text", {"window_title": args.get("window_title"), "element_name": args.get("element_name"), "text": args.get("text")})
        else: return _invalid_action(action, ["list_windows", "get_hierarchy", "query", "get_window_rect", "set_window_rect", "capture_window_snapshot", "click", "input"])

    elif name == "playerprefs_manager":
        action = args.get("action")
        if action == "get": return call_unity("get_player_pref", {"key": args.get("key"), "type": args.get("type", "string")})
        elif action == "set": return call_unity("set_player_pref", {"key": args.get("key"), "value": args.get("value"), "type": args.get("type", "string")})
        elif action == "delete": return call_unity("delete_player_pref", {"key": args.get("key")})
        elif action == "list": return call_unity("list_player_prefs")
        else: return _invalid_action(action, ["get", "set", "delete", "list"])

    elif name == "wait":
        cond = args.get("condition")
        timeout = args.get("timeout_seconds", 60)
        start_time = time.time()
        status = "Ready"

        if cond == "compilation":
            reload_started = False
            while time.time() - start_time < 20:
                res = call_unity("initialize")
                if res is None or "error" in res:
                    reload_started = True
                    break
                time.sleep(0.5)
            if not reload_started: call_unity("refresh_asset_database")
            while time.time() - start_time < timeout:
                res = call_unity("initialize")
                if res and "result" in res:
                    time.sleep(2.0)
                    state = call_unity("get_editor_state")
                    if state and "result" in state:
                        if not state["result"].get("is_compiling") and not state["result"].get("is_updating"): break
                time.sleep(1.0)
            else: status = "Timeout"
        elif cond == "play_mode":
            target_state = args.get("state", True)
            while time.time() - start_time < timeout:
                state_res = call_unity("get_editor_state")
                if state_res and "result" in state_res:
                    if state_res["result"].get("is_playing") == target_state: break
                time.sleep(1.0)
            else: status = "Timeout"
        elif cond == "import":
            while time.time() - start_time < timeout:
                res = call_unity("is_asset_import_idle")
                if res and "result" in res:
                    if res["result"].get("is_idle"): break
                time.sleep(1.0)
            else: status = "Timeout"
        elif cond == "editor_idle":
            while time.time() - start_time < timeout:
                res = call_unity("is_editor_idle")
                if res and "result" in res:
                    if res["result"].get("is_idle"): break
                time.sleep(1.0)
            else: status = "Timeout"

        return {"result": {"status": status, "time_waited_seconds": round(time.time() - start_time, 2)}}

    else:
        return call_unity(name, args)
