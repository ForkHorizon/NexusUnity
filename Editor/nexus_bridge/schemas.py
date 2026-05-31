# --- Static Tool Definitions (Hybrid Bridge Strategy) ---
# Optimized for 100% integrated AI development with strict parameter validation.
VECTOR3_SCHEMA = {
    "type": "object",
    "properties": {
        "x": {"type": "number"},
        "y": {"type": "number"},
        "z": {"type": "number"},
    },
}

STATIC_TOOLS = [
    # --- Consolidated Core Managers ---
    {
        "name": "unity_scene_manager",
        "description": "Unified scene management (create, open, save, list)",
        "inputSchema": {
            "type": "object",
            "properties": {
                "action": {"type": "string", "enum": ["create", "create_scene", "open", "open_scene", "save", "save_scene", "list", "list_scenes"]},
                "name": {"type": "string"},
                "path": {"type": "string"},
                "open_if_exists": {"type": "boolean"}
            },
            "required": ["action"]
        }
    },
    {
        "name": "unity_hierarchy_manager",
        "description": "Unified GameObject hierarchy and lifecycle management",
        "inputSchema": {
            "type": "object",
            "properties": {
                "action": {"type": "string", "enum": ["create_empty", "create", "create_gameobject", "create_game_object", "create_primitive", "create_hierarchy", "destroy", "duplicate", "rename", "set_name", "set_transform", "set_active", "set_parent", "set_sibling_index"]},
                "instance_id": {"type": "integer"},
                "name": {"type": "string"},
                "new_name": {"type": "string"},
                "parent_id": {"type": "integer"},
                "primitive_type": {"type": "string", "enum": ["Cube", "Sphere", "Capsule", "Cylinder", "Plane", "Quad"]},
                "position": VECTOR3_SCHEMA,
                "rotation": VECTOR3_SCHEMA,
                "scale": VECTOR3_SCHEMA,
                "eulerAngles": VECTOR3_SCHEMA,
                "localScale": VECTOR3_SCHEMA,
                "material_path": {"type": "string"},
                "tree": {"type": "object"},
                "active": {"type": "boolean"},
                "index": {"type": "string"}
            },
            "required": ["action"]
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
                {"properties": {"action": {"const": "create_material"}, "name": {"type": "string"}, "shader": {"type": "string"}, "path": {"type": "string"}, "base_color": {"type": "string"}, "color": {"type": "string"}, "emission_color": {"type": "string"}, "emission": {"type": "string"}}, "required": ["action", "name"]},
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
                {"properties": {"action": {"const": "clear_logs"}}, "required": ["action"]},
                {"properties": {"action": {"const": "get_state"}}, "required": ["action"]},
                {"properties": {"action": {"const": "get_server_status"}}, "required": ["action"]},
                {"properties": {"action": {"const": "refresh_assets"}}, "required": ["action"]},
                {"properties": {"action": {"const": "run_tests"}, "mode": {"type": "string"}, "filter": {"type": "string"}}, "required": ["action"]},
                {"properties": {"action": {"const": "get_test_results"}, "result_path": {"type": "string"}}, "required": ["action"]},
                {"properties": {"action": {"const": "run_tests_wait"}, "mode": {"type": "string"}, "filter": {"type": "string"}, "timeout_seconds": {"type": "integer"}, "poll_interval_seconds": {"type": "number"}}, "required": ["action"]},
                {"properties": {"action": {"const": "get_tool_usage_stats"}}, "required": ["action"]},
                {"properties": {"action": {"const": "reset_tool_usage_stats"}}, "required": ["action"]}
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
                {"properties": {"action": {"const": "get_hierarchy"}, "window_title": {"type": "string"}, "deep": {"type": "boolean"}}, "required": ["action", "window_title"]},
                {"properties": {"action": {"const": "query"}, "window_title": {"type": "string"}, "name": {"type": "string"}, "text": {"type": "string"}, "class_name": {"type": "string"}}, "required": ["action", "window_title"]},
                {"properties": {"action": {"const": "get_window_rect"}, "window_title": {"type": "string"}}, "required": ["action", "window_title"]},
                {"properties": {"action": {"const": "set_window_rect"}, "window_title": {"type": "string"}, "x": {"type": "number"}, "y": {"type": "number"}, "width": {"type": "number"}, "height": {"type": "number"}}, "required": ["action", "window_title"]},
                {"properties": {"action": {"const": "capture_window_snapshot"}, "window_title": {"type": "string"}, "include_image": {"type": "boolean"}, "include_hierarchy": {"type": "boolean"}}, "required": ["action", "window_title"]},
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
