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
# These are always returned to the AI CLI (Gemini, Codex, etc.) even if Unity is offline.
# Descriptions are optimized for LLM context windows.
STATIC_TOOLS = [
    {
        "name": "unity_add_component",
        "description": "Add component",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_apply_prefab_overrides",
        "description": "Apply changes to prefab asset",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_attach_existing_session",
        "description": "Attach to an existing healthy session",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_attach_script",
        "description": "Create & Link C#",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_batch_execute",
        "description": "Execute multiple JSON-RPC calls in a single HTTP request",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_capture_game_view_screenshot",
        "description": "Capture PNG of Game View",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_capture_inspector_screenshot",
        "description": "Capture PNG of Inspector (macOS only)",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_clear_logs",
        "description": "Clear Console",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_click_object_in_game",
        "description": "Click a GameObject in the Game View",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_close_prefab_stage",
        "description": "Exit prefab isolation mode",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_compact_scene_snapshot",
        "description": "Get a highly compressed hierarchy (name/id/component list only) for fast full-scene overview",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_component_values",
        "description": "Surgically read specific component fields as a clean key-value object",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_copy_asset",
        "description": "Duplicate file",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_create_folder",
        "description": "Create directory",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_create_game_object",
        "description": "Create empty GameObject",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_create_hierarchy",
        "description": "Batch create full hierarchy",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_create_material",
        "description": "Create material",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_create_prefab",
        "description": "Save as Prefab",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_create_primitive",
        "description": "Create primitive (Cube, Sphere...)",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_create_scene",
        "description": "Create new scene",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_create_scriptable_object_asset",
        "description": "Create new ScriptableObject asset",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_delete_asset",
        "description": "Delete file",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_delete_player_pref",
        "description": "Delete PlayerPref key or 'all'",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_destroy_game_object",
        "description": "Delete GameObject",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_diff_scriptable_object_against_defaults",
        "description": "Compare an asset against its default state",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_diff_scriptable_objects",
        "description": "Compare two ScriptableObject assets",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_dump_scene_graph",
        "description": "Dump a recursive tree of the active scene with components and key fields",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_duplicate_object",
        "description": "Copy GameObject",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_duplicate_scriptable_object_asset",
        "description": "Duplicate ScriptableObject asset",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_edit_prefab_asset",
        "description": "Directly modify a prefab asset on disk without instantiating it",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_enforce_forced_defaults",
        "description": "Enforce [ForceDefault] attributes",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_execute_menu_item",
        "description": "Execute Menu",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_explore_asset",
        "description": "List all internal sub-assets (e.g., sliced sprites) and their fileIDs within a single file.",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_find_by_path",
        "description": "Search by exact path",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_find_objects",
        "description": "Deep search",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_find_references",
        "description": "Find scene and asset references to a target object or GUID",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_focus_scene_view",
        "description": "Frame selection",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_generate_mermaid_diagram",
        "description": "Generate Mermaid diagram of scene",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_get_active_game_object",
        "description": "Get current selection",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_get_children",
        "description": "Get direct children",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_get_component_schema",
        "description": "Get serializable fields names/types",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_get_dependencies",
        "description": "Get file deps",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_get_editor_state",
        "description": "Get Play/Paused/Compiling",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_get_editor_timeline",
        "description": "Returns a list of recent Editor actions (imports, scene changes, play mode transitions)",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_get_game_object",
        "description": "Get object data",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_get_object_path",
        "description": "Get hierarchy breadcrumb",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_get_player_pref",
        "description": "Get PlayerPref value",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_get_prefab_overrides",
        "description": "Get list of property and component modifications on a prefab instance",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_get_project_info",
        "description": "Get Project metadata",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_get_root_game_objects",
        "description": "Get top-level objects",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_get_scene_dependencies",
        "description": "Scans the scene for cross-object references and returns a dependency map",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_get_selected_object_full_context",
        "description": "Returns a massive, context-rich JSON payload for the currently selected GameObject (including all serialized components, prefab status, and hierarchy path)",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_get_server_status",
        "description": "Get explicit health and state of the MCP server and Unity editor",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_get_tags_and_layers",
        "description": "Get Tags/Layers list",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_import_asset",
        "description": "Import file",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_inspect_component",
        "description": "Get properties and values",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_inspect_object",
        "description": "Universal inspector for ANY Unity object (Material, Texture, Mesh, ScriptableObject, etc.)",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_instantiate_prefab",
        "description": "Create from Prefab",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_invoke_method",
        "description": "Invoke a C# method on a component",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_lint_project",
        "description": "Run deterministic C# audit",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_list_assets",
        "description": "List assets",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_list_fields_for_type",
        "description": "List serializable fields for a type",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_list_player_prefs",
        "description": "List all PlayerPref keys and values",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_list_scenes",
        "description": "List all scene files",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_move_asset",
        "description": "Move/Rename",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_open_prefab_stage",
        "description": "Open prefab asset in isolation mode",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_open_scene",
        "description": "Open scene file",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_patch_scriptable_object",
        "description": "Surgically update ScriptableObject fields (alias for update_scriptable_object)",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_pause_play_mode",
        "description": "Pause/Unpause",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_ping_main_thread",
        "description": "Explicit liveness check for Unity API execution on main thread",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_ping_object",
        "description": "Ping in Editor",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_read_file",
        "description": "Read text content",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_read_logs",
        "description": "Get Console logs with optional noise reduction",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_read_logs_since_cursor",
        "description": "Read only new logs since last poll with optional noise reduction",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_read_scriptable_object",
        "description": "Read ScriptableObject properties",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_redo",
        "description": "Unity Redo",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_refresh_asset_database",
        "description": "Refresh Assets",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_remove_component",
        "description": "Remove component",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_revert_prefab_overrides",
        "description": "Revert scene changes to prefab defaults",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_run_tests",
        "description": "Run NUnit tests in the editor",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_save_scene",
        "description": "Save active scene",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_scene_delta",
        "description": "Returns a list of scene changes (created, destroyed, reparented, property changes) since a specific generation",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_semantic_find",
        "description": "Find objects by semantic meaning",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_set_active",
        "description": "Enable/Disable GameObject",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_set_enabled",
        "description": "Enable/Disable Component",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_set_parent",
        "description": "Parent an object",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_set_player_pref",
        "description": "Set PlayerPref value",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_set_property",
        "description": "Surgical field edit",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_set_selection",
        "description": "Select objects",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_set_sibling_index",
        "description": "Reorder in hierarchy",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_set_transform",
        "description": "Move/Rotate",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_show_unresolved_missing_references",
        "description": "Scans the active scene for 'Missing Script' components or broken ObjectReferences",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_shutdown_server",
        "description": "Safely stop the MCP server for this Unity instance",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_simulate_mouse",
        "description": "Simulate mouse input in Play Mode",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_simulate_touch",
        "description": "Simulate touch input in Play Mode",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_step_frame",
        "description": "Advance frame",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_symbol_index",
        "description": "Index and search all compiled symbols (Classes, Methods, Fields)",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_toggle_play_mode",
        "description": "Start/Stop",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_ui_click",
        "description": "Simulate UI Click",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_ui_get_hierarchy",
        "description": "Inspect Window UI",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_ui_input_text",
        "description": "Type into UI field",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_ui_list_windows",
        "description": "List Editor Windows",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_ui_query_elements",
        "description": "Find UI Toolkit elements by text, name, or USS class",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_undo",
        "description": "Unity Undo",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_update_component",
        "description": "Update component with detailed result",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_update_scriptable_object",
        "description": "Update ScriptableObject properties",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_wait_for_asset_import_idle",
        "description": "Wait until Unity is done importing assets",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_wait_for_editor_idle",
        "description": "Wait until the editor is fully idle (not compiling, not importing, no background tasks)",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_wait_for_ready",
        "description": "Wait until server is responsive",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_write_file",
        "description": "Write text content",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "unity_write_files_batch",
        "description": "Write multiple files in a single pass",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    }
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
        return {"error": {"code": -32000, "message": f"Unity Server unreachable. Ensure 'Window > Nexus Unity' is open and 'START SERVER' is clicked. Error: {str(e)}"}}

def prefixed_unity_tools():
    """Return the live Unity tool catalog with the MCP bridge unity_ prefix."""
    res = call_unity("list_tools")
    if not res or "error" in res or "result" not in res:
        return None

    tools = []
    for tool in res["result"]:
        if not isinstance(tool, dict) or "name" not in tool:
            continue
        copied = dict(tool)
        name = copied["name"]
        copied["name"] = name if name.startswith("unity_") else f"unity_{name}"
        tools.append(copied)
    return tools

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
            
            # Simple parameter parsing
            params = {}
            for arg in sys.argv[2:]:
                if "=" in arg:
                    k, v = arg.split("=", 1)
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
                    "capabilities": {
                        "tools": {}, 
                        "resources": {}, 
                        "prompts": {}
                    }, 
                    "serverInfo": {"name": "NexusUnity-Bridge", "version": "3.1.2"}                }
                response = {"jsonrpc": "2.0", "id": req_id, "result": res}
            elif method == "notifications/initialized":
                continue 
            elif method in ["tools/list", "listTools", "list_tools"]:
                tools = prefixed_unity_tools() or STATIC_TOOLS
                response = {"jsonrpc": "2.0", "id": req_id, "result": {"tools": tools}}
            elif method in ["resources/list", "listResources", "list_resources"]:
                resources = [
                    {
                        "uri": "unity://docs/api-reference",
                        "name": "Nexus Unity API Reference",
                        "mimeType": "text/markdown",
                        "description": "Reference for the public Nexus Unity tool surface."
                    },
                    {
                        "uri": "unity://docs/setup",
                        "name": "Nexus Unity Setup Guide",
                        "mimeType": "text/markdown",
                        "description": "General architecture and configuration."
                    }
                ]
                response = {"jsonrpc": "2.0", "id": req_id, "result": {"resources": resources}}
            elif method in ["resources/templates/list", "listResourceTemplates"]:
                response = {"jsonrpc": "2.0", "id": req_id, "result": {"resourceTemplates": []}}
            elif method in ["prompts/list", "listPrompts"]:
                response = {"jsonrpc": "2.0", "id": req_id, "result": {"prompts": []}}
            elif method in ["resources/read", "readResource"]:
                uri = request.get("params", {}).get("uri")
                filename = None
                if uri == "unity://docs/api-reference": filename = "API_REFERENCE.MD"
                elif uri == "unity://docs/setup": filename = "DOCUMENTATION.MD"

                content = "Documentation file not found. Please check Assets/NexusUnity/ or project root."
                if filename:
                    search_paths = [
                        filename,
                        f"Assets/NexusUnity/{filename}",
                        f"Packages/com.forkhorizon.nexus.unity/{filename}"
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
            elif method in ["tools/call", "callTool"]:
                params = request.get("params", {})
                name = params.get("name", "").replace("unity_", "")
                args = params.get("arguments", {})

                if name == "apply_code_change":
                    files = args.get("files", [])
                    start_time = time.time()
                    call_unity("clear_logs")
                    
                    write_errors = []
                    for f in files:
                        res = call_unity("write_file", {"path": f["path"], "content": f["content"]})
                        if res and "error" in res:
                            write_errors.append({"path": f["path"], "error": res["error"]})
                    
                    if write_errors:
                        unity_res = {"result": {"status": "Failed", "message": "Failed to write some files", "errors": write_errors}}
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

                        unity_res = {
                            "result": {
                                "status": "Failed" if compiler_errors else status,
                                "time_waited_seconds": round(time.time() - start_time, 2),
                                "compiler_errors": compiler_errors
                            }
                        }

                elif name == "wait_for_compilation":
                    timeout = args.get("timeout_seconds", 90)
                    start_time = time.time()
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

                    unity_res = {"result": {"status": status, "time_waited_seconds": round(time.time() - start_time, 2)}}

                elif name == "wait_for_play_mode":
                    target_state = args.get("state", True)
                    timeout = args.get("timeout_seconds", 60)
                    start_time = time.time()
                    status = "Ready"
                    while time.time() - start_time < timeout:
                        state_res = call_unity("get_editor_state")
                        if state_res and "result" in state_res:
                            if state_res["result"].get("is_playing") == target_state:
                                break
                        time.sleep(1.0)
                    else:
                        status = "Timeout"
                    
                    unity_res = {"result": {"status": status, "time_waited_seconds": round(time.time() - start_time, 2)}}

                elif name == "wait_for_asset_import_idle":
                    timeout = args.get("timeout_seconds", 60)
                    start_time = time.time()
                    status = "Ready"
                    while time.time() - start_time < timeout:
                        res = call_unity("is_asset_import_idle")
                        if res and "result" in res:
                            if res["result"].get("is_idle"):
                                break
                        time.sleep(1.0)
                    else:
                        status = "Timeout"
                    unity_res = {"result": {"status": status, "time_waited_seconds": round(time.time() - start_time, 2)}}

                elif name == "wait_for_editor_idle":
                    timeout = args.get("timeout_seconds", 120)
                    start_time = time.time()
                    status = "Ready"
                    while time.time() - start_time < timeout:
                        res = call_unity("is_editor_idle")
                        if res and "result" in res:
                            if res["result"].get("is_idle"):
                                break
                        time.sleep(1.0)
                    else:
                        status = "Timeout"
                    unity_res = {"result": {"status": status, "time_waited_seconds": round(time.time() - start_time, 2)}}

                else:
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
