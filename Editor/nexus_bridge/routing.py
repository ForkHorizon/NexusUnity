import time
from .client import call_unity, log

def route_tool(name, args):
    if name in ["write_and_compile", "apply_code_change"]:
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
        action = args.get("action")
        if action == "create": return call_unity("create_scene", {"name": args.get("name")})
        elif action == "open": return call_unity("open_scene", {"path": args.get("path")})
        elif action == "save": return call_unity("save_scene", {"path": args.get("path")})
        elif action == "list": return call_unity("list_scenes")
        else: return {"error": {"code": -32602, "message": f"Invalid action: {action}"}}

    elif name == "hierarchy_manager":
        action = args.get("action")
        if action == "create_empty": return call_unity("create_game_object", {"name": args.get("name"), "parent_id": args.get("parent_id")})
        elif action == "create_primitive": return call_unity("create_primitive", {"primitive_type": args.get("primitive_type")})
        elif action == "destroy": return call_unity("destroy_game_object", {"instance_id": args.get("instance_id")})
        elif action == "duplicate": return call_unity("duplicate_object", {"instance_id": args.get("instance_id")})
        elif action == "set_active": return call_unity("set_active", {"instance_id": args.get("instance_id"), "active": args.get("active")})
        elif action == "set_parent": return call_unity("set_parent", {"instance_id": args.get("instance_id"), "parent_id": args.get("parent_id")})
        elif action == "set_sibling_index": return call_unity("set_sibling_index", {"instance_id": args.get("instance_id"), "index": args.get("index")})
        else: return {"error": {"code": -32602, "message": f"Invalid action: {action}"}}

    elif name == "component_manager":
        action = args.get("action")
        if action == "add": return call_unity("add_component", {"instance_id": args.get("instance_id"), "component_name": args.get("component_name")})
        elif action == "remove": return call_unity("remove_component", {"instance_id": args.get("instance_id"), "component_name": args.get("component_name")})
        elif action == "inspect": return call_unity("inspect_component", {"instance_id": args.get("instance_id"), "component_name": args.get("component_name")})
        elif action == "get_schema": return call_unity("get_component_schema", {"instance_id": args.get("instance_id"), "component_name": args.get("component_name")})
        elif action == "update_properties": return call_unity("update_component", {"instance_id": args.get("instance_id"), "component_name": args.get("component_name"), "properties": args.get("properties")})
        elif action == "set_property": return call_unity("set_property", {"instance_id": args.get("instance_id"), "property_name": args.get("property_name"), "value": args.get("value")})
        elif action == "set_enabled": return call_unity("set_enabled", {"instance_id": args.get("instance_id"), "component_name": args.get("component_name"), "enabled": args.get("enabled")})
        else: return {"error": {"code": -32602, "message": f"Invalid action: {action}"}}

    elif name == "search_manager":
        strategy = args.get("strategy")
        if strategy == "regex": return call_unity("find_objects", {"name": args.get("query"), "tag": args.get("tag"), "type": args.get("type")})
        elif strategy == "path": return call_unity("find_by_path", {"path": args.get("query")})
        elif strategy == "semantic": return call_unity("semantic_find", {"query": args.get("query")})
        elif strategy == "references": return call_unity("find_references", {"target_id": args.get("target_id"), "target_guid": args.get("target_guid")})
        else: return {"error": {"code": -32602, "message": f"Invalid strategy: {strategy}"}}

    elif name == "asset_manager":
        action = args.get("action")
        if action == "search": return call_unity("list_assets", {"filter": args.get("filter")})
        elif action == "explore": return call_unity("explore_asset", {"path": args.get("path")})
        elif action == "create_material": return call_unity("create_material", {"name": args.get("name"), "shader": args.get("shader")})
        elif action == "import": return call_unity("import_asset", {"path": args.get("path")})
        elif action == "refresh": return call_unity("refresh_asset_database")
        elif action == "instantiate_prefab": return call_unity("instantiate_prefab", {"path": args.get("path")})
        elif action == "create_prefab": return call_unity("create_prefab", {"instance_id": args.get("instance_id"), "path": args.get("path")})
        elif action == "apply_overrides": return call_unity("apply_prefab_overrides", {"instance_id": args.get("instance_id")})
        elif action == "revert_overrides": return call_unity("revert_prefab_overrides", {"instance_id": args.get("instance_id")})
        else: return {"error": {"code": -32602, "message": f"Invalid action: {action}"}}

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
        else: return {"error": {"code": -32602, "message": f"Invalid action: {action}"}}

    elif name == "ui_automation":
        action = args.get("action")
        if action == "list_windows": return call_unity("ui_list_windows")
        elif action == "get_hierarchy": return call_unity("ui_get_hierarchy", {"window_title": args.get("window_title")})
        elif action == "query": return call_unity("ui_query_elements", {"window_title": args.get("window_title"), "name": args.get("name"), "text": args.get("text")})
        elif action == "click": return call_unity("ui_click", {"window_title": args.get("window_title"), "element_name": args.get("element_name")})
        elif action == "input": return call_unity("ui_input_text", {"window_title": args.get("window_title"), "element_name": args.get("element_name"), "text": args.get("text")})
        else: return {"error": {"code": -32602, "message": f"Invalid action: {action}"}}

    elif name == "playerprefs_manager":
        action = args.get("action")
        if action == "get": return call_unity("get_player_pref", {"key": args.get("key"), "type": args.get("type", "string")})
        elif action == "set": return call_unity("set_player_pref", {"key": args.get("key"), "value": args.get("value"), "type": args.get("type", "string")})
        elif action == "delete": return call_unity("delete_player_pref", {"key": args.get("key")})
        elif action == "list": return call_unity("list_player_prefs")
        else: return {"error": {"code": -32602, "message": f"Invalid action: {action}"}}

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
