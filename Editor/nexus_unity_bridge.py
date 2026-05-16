#!/usr/bin/env python3
import sys
import json
import urllib.request
import os
import time
import threading

# Port can be overridden via command line arg if needed
PORT = 8081
if len(sys.argv) > 1:
    try: PORT = int(sys.argv[1])
    except: pass

UNITY_URL = f"http://127.0.0.1:{PORT}/"
PARENT_PID = os.getppid()

# --- Static Tool Definitions (Hybrid Bridge Strategy) ---
# Optimized for 100% integrated AI development with strict parameter validation.
STATIC_TOOLS = [
    # --- Consolidated Core Managers ---
    {
        "name": "unity_scene_manager",
        "description": "Unified scene management (create, open, save, list)",
        "inputSchema": {
            "type": "object",
            "oneOf": [
                {"properties": {"action": {"const": "create"}, "name": {"type": "string"}}, "required": ["action", "name"]},
                {"properties": {"action": {"const": "open"}, "path": {"type": "string"}}, "required": ["action", "path"]},
                {"properties": {"action": {"const": "save"}, "path": {"type": "string"}}, "required": ["action", "path"]},
                {"properties": {"action": {"const": "list"}}, "required": ["action"]}
            ]
        }
    },
    {
        "name": "unity_hierarchy_manager",
        "description": "Unified GameObject hierarchy and lifecycle management",
        "inputSchema": {
            "type": "object",
            "oneOf": [
                {"properties": {"action": {"const": "create_empty"}, "name": {"type": "string"}, "parent_id": {"type": "integer"}}, "required": ["action", "name"]},
                {"properties": {"action": {"const": "create_primitive"}, "primitive_type": {"type": "string", "enum": ["Cube", "Sphere", "Capsule", "Cylinder", "Plane", "Quad"]}}, "required": ["action", "primitive_type"]},
                {"properties": {"action": {"const": "destroy"}, "instance_id": {"type": "integer"}}, "required": ["action", "instance_id"]},
                {"properties": {"action": {"const": "duplicate"}, "instance_id": {"type": "integer"}}, "required": ["action", "instance_id"]},
                {"properties": {"action": {"const": "set_active"}, "instance_id": {"type": "integer"}, "active": {"type": "boolean"}}, "required": ["action", "instance_id", "active"]},
                {"properties": {"action": {"const": "set_parent"}, "instance_id": {"type": "integer"}, "parent_id": {"type": "integer"}}, "required": ["action", "instance_id", "parent_id"]},
                {"properties": {"action": {"const": "set_sibling_index"}, "instance_id": {"type": "integer"}, "index": {"type": "string"}}, "required": ["action", "instance_id", "index"]}
            ]
        }
    },
    {
        "name": "unity_component_manager",
        "description": "Unified component and property management",
        "inputSchema": {
            "type": "object",
            "oneOf": [
                {"properties": {"action": {"const": "add"}, "instance_id": {"type": "integer"}, "component_name": {"type": "string"}}, "required": ["action", "instance_id", "component_name"]},
                {"properties": {"action": {"const": "remove"}, "instance_id": {"type": "integer"}, "component_name": {"type": "string"}}, "required": ["action", "instance_id", "component_name"]},
                {"properties": {"action": {"const": "inspect"}, "instance_id": {"type": "integer"}, "component_name": {"type": "string"}}, "required": ["action", "instance_id", "component_name"]},
                {"properties": {"action": {"const": "get_schema"}, "instance_id": {"type": "integer"}, "component_name": {"type": "string"}}, "required": ["action", "instance_id", "component_name"]},
                {"properties": {"action": {"const": "update_properties"}, "instance_id": {"type": "integer"}, "component_name": {"type": "string"}, "properties": {"type": "object"}}, "required": ["action", "instance_id", "component_name", "properties"]},
                {"properties": {"action": {"const": "set_property"}, "instance_id": {"type": "integer"}, "property_name": {"type": "string"}, "value": {"type": "string"}}, "required": ["action", "instance_id", "property_name", "value"]},
                {"properties": {"action": {"const": "set_enabled"}, "instance_id": {"type": "integer"}, "component_name": {"type": "string"}, "enabled": {"type": "boolean"}}, "required": ["action", "instance_id", "component_name", "enabled"]}
            ]
        }
    },
    {
        "name": "unity_search_manager",
        "description": "Unified discovery and reference search",
        "inputSchema": {
            "type": "object",
            "oneOf": [
                {"properties": {"strategy": {"const": "regex"}, "query": {"type": "string"}, "tag": {"type": "string"}, "type": {"type": "string"}}, "required": ["strategy"]},
                {"properties": {"strategy": {"const": "path"}, "query": {"type": "string"}}, "required": ["strategy", "query"]},
                {"properties": {"strategy": {"const": "semantic"}, "query": {"type": "string"}}, "required": ["strategy", "query"]},
                {"properties": {"strategy": {"const": "references"}, "target_id": {"type": "integer"}, "target_guid": {"type": "string"}}, "required": ["strategy"]}
            ]
        }
    },
    {
        "name": "unity_asset_manager",
        "description": "Unified asset and prefab pipeline management",
        "inputSchema": {
            "type": "object",
            "oneOf": [
                {"properties": {"action": {"const": "search"}, "filter": {"type": "string"}}, "required": ["action"]},
                {"properties": {"action": {"const": "explore"}, "path": {"type": "string"}}, "required": ["action", "path"]},
                {"properties": {"action": {"const": "create_material"}, "name": {"type": "string"}, "shader": {"type": "string"}}, "required": ["action", "name"]},
                {"properties": {"action": {"const": "import"}, "path": {"type": "string"}}, "required": ["action", "path"]},
                {"properties": {"action": {"const": "refresh"}}, "required": ["action"]},
                {"properties": {"action": {"const": "instantiate_prefab"}, "path": {"type": "string"}}, "required": ["action", "path"]},
                {"properties": {"action": {"const": "create_prefab"}, "instance_id": {"type": "integer"}, "path": {"type": "string"}}, "required": ["action", "instance_id", "path"]},
                {"properties": {"action": {"const": "apply_overrides"}, "instance_id": {"type": "integer"}}, "required": ["action", "instance_id"]},
                {"properties": {"action": {"const": "revert_overrides"}, "instance_id": {"type": "integer"}}, "required": ["action", "instance_id"]}
            ]
        }
    },
    {
        "name": "unity_editor_controller",
        "description": "Unified editor state and play mode control",
        "inputSchema": {
            "type": "object",
            "oneOf": [
                {"properties": {"action": {"const": "undo"}}, "required": ["action"]},
                {"properties": {"action": {"const": "redo"}}, "required": ["action"]},
                {"properties": {"action": {"const": "play"}, "state": {"type": "boolean"}}, "required": ["action", "state"]},
                {"properties": {"action": {"const": "pause"}, "state": {"type": "boolean"}}, "required": ["action", "state"]},
                {"properties": {"action": {"const": "step"}}, "required": ["action"]},
                {"properties": {"action": {"const": "menu"}, "item_path": {"type": "string"}}, "required": ["action", "item_path"]},
                {"properties": {"action": {"const": "read_logs"}, "count": {"type": "integer"}}, "required": ["action"]},
                {"properties": {"action": {"const": "clear_logs"}}, "required": ["action"]}
            ]
        }
    },
    {
        "name": "unity_ui_automation",
        "description": "Unified UI Toolkit window automation",
        "inputSchema": {
            "type": "object",
            "oneOf": [
                {"properties": {"action": {"const": "list_windows"}}, "required": ["action"]},
                {"properties": {"action": {"const": "get_hierarchy"}, "window_title": {"type": "string"}}, "required": ["action", "window_title"]},
                {"properties": {"action": {"const": "query"}, "window_title": {"type": "string"}, "name": {"type": "string"}, "text": {"type": "string"}}, "required": ["action", "window_title"]},
                {"properties": {"action": {"const": "click"}, "window_title": {"type": "string"}, "element_name": {"type": "string"}}, "required": ["action", "window_title", "element_name"]},
                {"properties": {"action": {"const": "input"}, "window_title": {"type": "string"}, "element_name": {"type": "string"}, "text": {"type": "string"}}, "required": ["action", "window_title", "element_name", "text"]}
            ]
        }
    },
    {
        "name": "unity_wait",
        "description": "Wait for specific Unity editor states or events",
        "inputSchema": {
            "type": "object",
            "oneOf": [
                {"properties": {"condition": {"const": "compilation"}, "timeout_seconds": {"type": "integer"}}, "required": ["condition"]},
                {"properties": {"condition": {"const": "play_mode"}, "state": {"type": "boolean"}, "timeout_seconds": {"type": "integer"}}, "required": ["condition", "state"]},
                {"properties": {"condition": {"const": "import"}, "timeout_seconds": {"type": "integer"}}, "required": ["condition"]},
                {"properties": {"condition": {"const": "editor_idle"}, "timeout_seconds": {"type": "integer"}}, "required": ["condition"]}
            ]
        }
    },
    {
        "name": "unity_playerprefs_manager",
        "description": "Unified PlayerPrefs management",
        "inputSchema": {
            "type": "object",
            "oneOf": [
                {"properties": {"action": {"const": "get"}, "key": {"type": "string"}, "type": {"type": "string", "enum": ["string", "int", "float"]}}, "required": ["action", "key"]},
                {"properties": {"action": {"const": "set"}, "key": {"type": "string"}, "value": {"type": "string"}, "type": {"type": "string", "enum": ["string", "int", "float"]}}, "required": ["action", "key", "value"]},
                {"properties": {"action": {"const": "delete"}, "key": {"type": "string"}}, "required": ["action", "key"]},
                {"properties": {"action": {"const": "list"}}, "required": ["action"]}
            ]
        }
    },

    # --- Specialized Diagnostics ---
    {"name": "unity_write_and_compile", "description": "High-level macro: Writes multiple files, waits for domain reload, and returns compiler errors. Use for ALL code changes.", "inputSchema": {"type": "object", "properties": {"files": {"type": "array", "items": {"type": "object", "properties": {"path": {"type": "string"}, "content": {"type": "string"}}, "required": ["path", "content"]}}}, "required": ["files"]}},
    {"name": "unity_invoke_method", "description": "Invoke a C# method on a component via reflection", "inputSchema": {"type": "object", "properties": {"instance_id": {"type": "integer"}, "component_name": {"type": "string"}, "method_name": {"type": "string"}, "arguments": {"type": "array"}}, "required": ["instance_id", "method_name"]}},
    {"name": "unity_dump_scene_graph", "description": "Dump recursive tree of active scene with components and key fields", "inputSchema": {"type": "object", "properties": {"root_id": {"type": "integer"}, "max_depth": {"type": "integer"}, "include_all_properties": {"type": "boolean"}}}},
    {"name": "unity_get_scene_dependencies", "description": "Return a scene-wide dependency map of cross-object references", "inputSchema": {"type": "object", "properties": {}}},
    {"name": "unity_lint_project", "description": "Run Roslyn-based C# audit of the entire project", "inputSchema": {"type": "object", "properties": {}}}
]

def log(msg):
    sys.stderr.write(f"DEBUG: {msg}\n")
    sys.stderr.flush()

def call_unity(method, params=None):
    payload = {"jsonrpc": "2.0", "method": method, "params": params or {}, "id": 1}
    data = json.dumps(payload).encode('utf-8')
    req = urllib.request.Request(UNITY_URL, data=data, headers={'Content-Type': 'application/json'})
    try:
        # Increased timeout to 120s to support heavy operations like project-wide linting.
        with urllib.request.urlopen(req, timeout=120) as f:
            return json.loads(f.read().decode('utf-8'))
    except Exception as e:
        return {"error": {"code": -32000, "message": f"Unity Server unreachable. Error: {str(e)}"}}

def orphan_monitor():
    """Monitor if the parent process (AI CLI) is still alive."""
    while True:
        try:
            # os.getppid() returns 1 if the parent has died (on Unix)
            if os.getppid() != PARENT_PID or os.getppid() == 1:
                log("Parent process died. Shutting down bridge.")
                os._exit(0)
        except:
            os._exit(0)
        time.sleep(5)

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

def main():
    # --- DUAL MODE: CLI vs MCP ---
    if len(sys.argv) > 1:
        arg1 = sys.argv[1]
        try:
            int(arg1)
        except ValueError:
            method_name = arg1.replace("unity_", "")
            params = {}
            for arg in sys.argv[2:]:
                if "=" in arg:
                    k, v = arg.split("=", 1)
                    try: params[k] = json.loads(v)
                    except: params[k] = v

            log(f"CLI Mode: Calling {method_name} with {params}")
            res = route_tool(method_name, params)
            if "error" in res:
                print(json.dumps(res["error"], indent=2))
                sys.exit(1)
            else:
                final_res = res.get("result", res)
                print(json.dumps(final_res, indent=2))
                sys.exit(0)

    log(f"NexusUnity Bridge started (Parent PID: {PARENT_PID})")
    
    monitor_thread = threading.Thread(target=orphan_monitor, daemon=True)
    monitor_thread.start()

    while True:
        line = sys.stdin.readline()
        if not line:
            log("Stdin closed. Shutting down bridge.")
            break
        try:
            request = json.loads(line)
            method = request.get("method")
            req_id = request.get("id")
            
            if method == "initialize":
                res = {
                    "protocolVersion": "2024-11-05", 
                    "capabilities": {"tools": {}, "resources": {}, "prompts": {}}, 
                    "serverInfo": {"name": "NexusUnity-Bridge", "version": "2.8.0"}
                }
                response = {"jsonrpc": "2.0", "id": req_id, "result": res}
            elif method in ["tools/list", "listTools", "list_tools"]:
                response = {"jsonrpc": "2.0", "id": req_id, "result": {"tools": STATIC_TOOLS}}
            elif method in ["resources/list", "listResources"]:
                resources = [
                    {"uri": "unity://docs/api-reference", "name": "API Reference", "mimeType": "text/markdown"},
                    {"uri": "unity://docs/setup", "name": "Setup Guide", "mimeType": "text/markdown"}
                ]
                response = {"jsonrpc": "2.0", "id": req_id, "result": {"resources": resources}}
            elif method in ["tools/call", "callTool"]:
                params = request.get("params", {})
                name = params.get("name", "").replace("unity_", "")
                args = params.get("arguments", {})

                unity_res = route_tool(name, args)

                if "error" in unity_res:
                    response = {"jsonrpc": "2.0", "id": req_id, "error": unity_res["error"]}
                else:
                    result_content = unity_res.get("result", unity_res)
                    if isinstance(result_content, dict) and "content" in result_content:
                        response = {"jsonrpc": "2.0", "id": req_id, "result": result_content}
                    else:
                        response = {
                            "jsonrpc": "2.0", 
                            "id": req_id, 
                            "result": {"content": [{"type": "text", "text": json.dumps(result_content) }]}}
            elif req_id is not None:
                response = {"jsonrpc": "2.0", "id": req_id, "error": {"code": -32601, "message": "Method not found"}}
            else:
                continue

            sys.stdout.write(json.dumps(response) + "\n")
            sys.stdout.flush()
        except Exception as e:
            log(f"Error in bridge loop: {str(e)}")

if __name__ == "__main__":
    main()
