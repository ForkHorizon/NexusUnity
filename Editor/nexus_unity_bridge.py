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
# These are always returned to the Gemini CLI even if Unity is offline.
# Descriptions are optimized for LLM context windows.
STATIC_TOOLS = [
    # Scene Management
    {"name": "unity_create_scene", "description": "Create a new Unity scene", "inputSchema": {"type": "object", "properties": {"name": {"type": "string"}}}},
    {"name": "unity_open_scene", "description": "Open an existing scene file", "inputSchema": {"type": "object", "properties": {"path": {"type": "string"}}, "required": ["path"]}},
    {"name": "unity_save_scene", "description": "Save the active scene", "inputSchema": {"type": "object", "properties": {"path": {"type": "string"}}}},
    {"name": "unity_list_scenes", "description": "List all scene assets in the project", "inputSchema": {"type": "object", "properties": {}}},

    # Hierarchy & GameObject Lifecycle
    {"name": "unity_create_game_object", "description": "Create an empty GameObject", "inputSchema": {"type": "object", "properties": {"name": {"type": "string"}, "parent_id": {"type": "integer"}}, "required": ["name"]}},
    {"name": "unity_create_primitive", "description": "Create a primitive (Cube, Sphere, Capsule, Cylinder, Plane, Quad)", "inputSchema": {"type": "object", "properties": {"primitive_type": {"type": "string", "enum": ["Cube", "Sphere", "Capsule", "Cylinder", "Plane", "Quad"]}}, "required": ["primitive_type"]}},
    {"name": "unity_destroy_game_object", "description": "Delete a GameObject", "inputSchema": {"type": "object", "properties": {"instance_id": {"type": "integer"}}, "required": ["instance_id"]}},
    {"name": "unity_duplicate_object", "description": "Copy a GameObject", "inputSchema": {"type": "object", "properties": {"instance_id": {"type": "integer"}}, "required": ["instance_id"]}},
    {"name": "unity_set_active", "description": "Enable/Disable a GameObject", "inputSchema": {"type": "object", "properties": {"instance_id": {"type": "integer"}, "active": {"type": "boolean"}}, "required": ["instance_id", "active"]}},
    {"name": "unity_set_parent", "description": "Parent an object", "inputSchema": {"type": "object", "properties": {"instance_id": {"type": "integer"}, "parent_id": {"type": "integer"}}, "required": ["instance_id", "parent_id"]}},
    {"name": "unity_set_sibling_index", "description": "Reorder in hierarchy", "inputSchema": {"type": "object", "properties": {"instance_id": {"type": "integer"}, "index": {"type": "string", "description": "int or 'first' or 'last'"}}, "required": ["instance_id", "index"]}},

    # Components & Properties
    {"name": "unity_add_component", "description": "Add a component to a GameObject", "inputSchema": {"type": "object", "properties": {"instance_id": {"type": "integer"}, "component_name": {"type": "string"}}, "required": ["instance_id", "component_name"]}},
    {"name": "unity_remove_component", "description": "Remove a component", "inputSchema": {"type": "object", "properties": {"instance_id": {"type": "integer"}, "component_name": {"type": "string"}}, "required": ["instance_id", "component_name"]}},
    {"name": "unity_inspect_component", "description": "Get all properties of a component", "inputSchema": {"type": "object", "properties": {"instance_id": {"type": "integer"}, "component_name": {"type": "string"}}, "required": ["instance_id", "component_name"]}},
    {"name": "unity_update_component", "description": "Update component data using JSON", "inputSchema": {"type": "object", "properties": {"instance_id": {"type": "integer"}, "component_name": {"type": "string"}, "json_data": {"type": "string"}}, "required": ["instance_id", "component_name", "json_data"]}},
    {"name": "unity_set_transform", "description": "Update position/rotation/scale", "inputSchema": {"type": "object", "properties": {"instance_id": {"type": "integer"}, "position": {"type": "object", "properties": {"x": {"type": "number"}, "y": {"type": "number"}, "z": {"type": "number"}}}}}},
    {"name": "unity_set_property", "description": "Surgical field edit (m_Enabled, m_Name, etc)", "inputSchema": {"type": "object", "properties": {"instance_id": {"type": "integer"}, "property_name": {"type": "string"}, "value": {"type": "string"}}, "required": ["instance_id", "property_name", "value"]}},
    {"name": "unity_set_enabled", "description": "Enable/Disable a component", "inputSchema": {"type": "object", "properties": {"instance_id": {"type": "integer"}, "component_name": {"type": "string"}, "enabled": {"type": "boolean"}}, "required": ["instance_id", "component_name", "enabled"]}},

    # Asset Management
    {"name": "unity_list_assets", "description": "Search for assets", "inputSchema": {"type": "object", "properties": {"filter": {"type": "string"}}}},
    {"name": "unity_create_material", "description": "Create a material", "inputSchema": {"type": "object", "properties": {"name": {"type": "string"}, "shader": {"type": "string"}}, "required": ["name"]}},
    {"name": "unity_refresh_asset_database", "description": "Force Unity to scan for changed files", "inputSchema": {"type": "object", "properties": {}}},
    {"name": "unity_import_asset", "description": "Import a file into the project", "inputSchema": {"type": "object", "properties": {"path": {"type": "string"}}, "required": ["path"]}},
    {"name": "unity_instantiate_prefab", "description": "Create an instance of a prefab", "inputSchema": {"type": "object", "properties": {"path": {"type": "string"}}, "required": ["path"]}},
    {"name": "unity_create_prefab", "description": "Save an object as a prefab", "inputSchema": {"type": "object", "properties": {"instance_id": {"type": "integer"}, "path": {"type": "string"}}, "required": ["instance_id", "path"]}},
    {"name": "unity_apply_prefab_overrides", "description": "Apply scene changes back to prefab asset", "inputSchema": {"type": "object", "properties": {"instance_id": {"type": "integer"}}, "required": ["instance_id"]}},
    {"name": "unity_revert_prefab_overrides", "description": "Revert scene changes to prefab asset", "inputSchema": {"type": "object", "properties": {"instance_id": {"type": "integer"}}, "required": ["instance_id"]}},
    {"name": "unity_move_asset", "description": "Move/Rename an asset", "inputSchema": {"type": "object", "properties": {"old_path": {"type": "string"}, "new_path": {"type": "string"}}, "required": ["old_path", "new_path"]}},
    {"name": "unity_delete_asset", "description": "Delete an asset", "inputSchema": {"type": "object", "properties": {"path": {"type": "string"}}, "required": ["path"]}},
    {"name": "unity_copy_asset", "description": "Duplicate an asset", "inputSchema": {"type": "object", "properties": {"source_path": {"type": "string"}, "dest_path": {"type": "string"}}, "required": ["source_path", "dest_path"]}},
    {"name": "unity_get_dependencies", "description": "Get file dependencies", "inputSchema": {"type": "object", "properties": {"path": {"type": "string"}}, "required": ["path"]}},
    {"name": "unity_create_folder", "description": "Create a directory in the project", "inputSchema": {"type": "object", "properties": {"path": {"type": "string"}}, "required": ["path"]}},
    {"name": "unity_read_file", "description": "Read text content from a project file", "inputSchema": {"type": "object", "properties": {"path": {"type": "string"}}, "required": ["path"]}},
    {"name": "unity_write_file", "description": "Write text content to a project file", "inputSchema": {"type": "object", "properties": {"path": {"type": "string"}, "content": {"type": "string"}}, "required": ["path", "content"]}},

    # Editor Control & State
    {"name": "unity_undo", "description": "Perform Unity Undo", "inputSchema": {"type": "object", "properties": {}}},
    {"name": "unity_redo", "description": "Perform Unity Redo", "inputSchema": {"type": "object", "properties": {}}},
    {"name": "unity_toggle_play_mode", "description": "Start or stop Play Mode", "inputSchema": {"type": "object", "properties": {"value": {"type": "boolean"}}}},
    {"name": "unity_pause_play_mode", "description": "Pause or unpause Play Mode", "inputSchema": {"type": "object", "properties": {"value": {"type": "boolean"}}}},
    {"name": "unity_step_frame", "description": "Advance one frame in Play Mode", "inputSchema": {"type": "object", "properties": {}}},
    {"name": "unity_execute_menu_item", "description": "Execute any Unity menu command", "inputSchema": {"type": "object", "properties": {"item_path": {"type": "string"}}, "required": ["item_path"]}},
    {"name": "unity_focus_scene_view", "description": "Frame the current selection", "inputSchema": {"type": "object", "properties": {}}},
    {"name": "unity_read_logs", "description": "Get current Console logs", "inputSchema": {"type": "object", "properties": {"count": {"type": "integer"}}}},
    {"name": "unity_clear_logs", "description": "Clear the Console", "inputSchema": {"type": "object", "properties": {}}},
    {"name": "unity_attach_script", "description": "Create a C# script and link it", "inputSchema": {"type": "object", "properties": {"script_name": {"type": "string"}, "script_content": {"type": "string"}}, "required": ["script_name"]}},

    # Discovery & Inspection
    {"name": "unity_get_game_object", "description": "Get basic metadata for an ID", "inputSchema": {"type": "object", "properties": {"instance_id": {"type": "integer"}}, "required": ["instance_id"]}},
    {"name": "unity_get_active_game_object", "description": "Get currently selected object", "inputSchema": {"type": "object", "properties": {}}},
    {"name": "unity_get_root_game_objects", "description": "Get top-level scene objects", "inputSchema": {"type": "object", "properties": {}}},
    {"name": "unity_get_object_path", "description": "Get hierarchy breadcrumb", "inputSchema": {"type": "object", "properties": {"instance_id": {"type": "integer"}}, "required": ["instance_id"]}},
    {"name": "unity_find_objects", "description": "Deep regex/tag search for objects", "inputSchema": {"type": "object", "properties": {"name": {"type": "string"}, "tag": {"type": "string"}, "type": {"type": "string"}}}},
    {"name": "unity_get_tags_and_layers", "description": "List all project tags/layers", "inputSchema": {"type": "object", "properties": {}}},
    {"name": "unity_ping_object", "description": "Highlight object in Editor", "inputSchema": {"type": "object", "properties": {"instance_id": {"type": "integer"}}, "required": ["instance_id"]}},
    {"name": "unity_get_children", "description": "Get direct child objects", "inputSchema": {"type": "object", "properties": {"instance_id": {"type": "integer"}}, "required": ["instance_id"]}},
    {"name": "unity_get_editor_state", "description": "Get Play/Paused/Compiling state", "inputSchema": {"type": "object", "properties": {}}},
    {"name": "unity_get_project_info", "description": "Get project metadata", "inputSchema": {"type": "object", "properties": {}}},
    {"name": "unity_set_selection", "description": "Select objects in Editor", "inputSchema": {"type": "object", "properties": {"instance_ids": {"type": "array", "items": {"type": "integer"}}}, "required": ["instance_ids"]}},

    # UI Toolkit Automation
    {"name": "unity_ui_list_windows", "description": "List open Editor windows", "inputSchema": {"type": "object", "properties": {}}},
    {"name": "unity_ui_get_hierarchy", "description": "Inspect window UI tree", "inputSchema": {"type": "object", "properties": {"window_title": {"type": "string"}}, "required": ["window_title"]}},
    {"name": "unity_ui_click", "description": "Click a UI element", "inputSchema": {"type": "object", "properties": {"window_title": {"type": "string"}, "element_name": {"type": "string"}}, "required": ["window_title", "element_name"]}},
    {"name": "unity_ui_input_text", "description": "Input text into UI element", "inputSchema": {"type": "object", "properties": {"window_title": {"type": "string"}, "element_name": {"type": "string"}, "text": {"type": "string"}}, "required": ["window_title", "element_name", "text"]}},

    # Compliance
    {"name": "unity_lint_project", "description": "Run Roslyn-based C# audit", "inputSchema": {"type": "object", "properties": {}}}
]

def log(msg):
    sys.stderr.write(f"DEBUG: {msg}\n")
    sys.stderr.flush()

def call_unity(method, params=None):
    payload = {"jsonrpc": "2.0", "method": method, "params": params or {}, "id": 1}
    data = json.dumps(payload).encode('utf-8')
    req = urllib.request.Request(UNITY_URL, data=data, headers={'Content-Type': 'application/json'})
    try:
        # Use a short timeout to prevent the bridge from hanging if Unity is unresponsive
        with urllib.request.urlopen(req, timeout=2) as f:
            return json.loads(f.read().decode('utf-8'))
    except Exception as e:
        return {"error": {"code": -32000, "message": f"Unity Server unreachable. Ensure 'Window > Nexus Unity' is open and 'START SERVER' is clicked. Error: {str(e)}"}}

def orphan_monitor():
    """Monitor if the parent process (Gemini CLI) is still alive."""
    while True:
        try:
            # os.getppid() returns 1 if the parent has died (on Unix)
            if os.getppid() != PARENT_PID or os.getppid() == 1:
                log("Parent process died. Shutting down bridge.")
                os._exit(0)
        except:
            os._exit(0)
        time.sleep(5)

def main():
    # --- DUAL MODE: CLI vs MCP ---
    # If arguments are provided and the first one isn't just a number (port),
    # we treat it as a direct CLI call to a Unity tool.
    if len(sys.argv) > 1:
        arg1 = sys.argv[1]
        try:
            # If it's an integer, it's a port override for MCP mode
            int(arg1)
        except ValueError:
            # It's a string, treat as a tool name
            method_name = arg1.replace("unity_", "")
            
            # Simple parameter parsing: treat subsequent args as key=value pairs or just positional?
            # For now, let's support simple positional args as params if needed, 
            # but most tools like refresh_asset_database take no params.
            params = {}
            for arg in sys.argv[2:]:
                if "=" in arg:
                    k, v = arg.split("=", 1)
                    # Try to parse v as JSON/number/bool
                    try: params[k] = json.loads(v)
                    except: params[k] = v

            log(f"CLI Mode: Calling {method_name} with {params}")
            res = call_unity(method_name, params)
            if "error" in res:
                print(json.dumps(res["error"], indent=2))
                sys.exit(1)
            else:
                print(json.dumps(res["result"], indent=2))
                sys.exit(0)

    log(f"NexusUnity Bridge started (Parent PID: {PARENT_PID})")
    
    # Start the orphan monitor in a background thread
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
            
            # Standard MCP lifecycle methods
            if method == "initialize":
                res = {
                    "protocolVersion": "2024-11-05", 
                    "capabilities": {"tools": {}, "resources": {}, "prompts": {}}, 
                    "serverInfo": {"name": "NexusUnity-Bridge", "version": "2.0.6"}
                }
                response = {"jsonrpc": "2.0", "id": req_id, "result": res}
            elif method == "notifications/initialized":
                continue 
            elif method in ["tools/list", "listTools"]:
                # --- HYBRID STRATEGY ---
                # We return the static list of tools. 
                # This ensures the Gemini CLI ALWAYS sees the tools.
                response = {"jsonrpc": "2.0", "id": req_id, "result": {"tools": STATIC_TOOLS}}
            elif method in ["resources/list", "listResources"]:
                resources = [
                    {
                        "uri": "unity://docs/api-reference",
                        "name": "Nexus Unity API Reference",
                        "mimeType": "text/markdown",
                        "description": "Full reference for all 60+ tools."
                    },
                    {
                        "uri": "unity://docs/setup",
                        "name": "Nexus Unity Setup Guide",
                        "mimeType": "text/markdown",
                        "description": "General architecture and configuration."
                    }
                ]
                response = {"jsonrpc": "2.0", "id": req_id, "result": {"resources": resources}}
            elif method in ["resources/read", "readResource"]:
                uri = request.get("params", {}).get("uri")
                doc_path = ""
                if uri == "unity://docs/api-reference": filename = "API_REFERENCE.MD"
                elif uri == "unity://docs/setup": filename = "DOCUMENTATION.MD"
                else: filename = None

                content = "Documentation file not found. Please check Assets/NexusUnity/ or project root."
                if filename:
                    # Strategy: Bridge is usually in project root. 
                    search_paths = [
                        filename,
                        f"Assets/NexusUnity/{filename}",
                        f"Packages/com.custom.unity.mcp/{filename}"
                    ]
                    for p in search_paths:
                        if os.path.exists(p):
                            with open(p, 'r') as f:
                                content = f.read()
                                break
                
                response = {
                    "jsonrpc": "2.0", 
                    "id": req_id, 
                    "result": {"contents": [{"uri": uri, "mimeType": "text/markdown", "text": content}]}}
            elif method in ["prompts/list", "listPrompts"]:
                response = {"jsonrpc": "2.0", "id": req_id, "result": {"prompts": []}}
            elif method in ["tools/call", "callTool"]:
                params = request.get("params", {})
                name = params.get("name", "").replace("unity_", "")
                args = params.get("arguments", {})
                unity_res = call_unity(name, args)
                if "error" in unity_res:
                    response = {"jsonrpc": "2.0", "id": req_id, "error": unity_res["error"]}
                else:
                    response = {
                        "jsonrpc": "2.0", 
                        "id": req_id, 
                        "result": {"content": [{"type": "text", "text": json.dumps(unity_res["result"]) }]}}
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
