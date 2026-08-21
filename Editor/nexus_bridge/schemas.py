"""Static tool and resource definitions for the NexusUnity MCP bridge.

:data:`STATIC_TOOLS` is the authoritative list of MCP tools exposed to AI
clients.  Each entry follows the JSON Schema / MCP tool-definition shape.

:data:`STATIC_RESOURCES` lists the static read-only resources advertised via
``resources/list``.
"""

from __future__ import annotations

from typing import Any

JsonObject = dict[str, Any]

# --- Shared sub-schemas ---
VECTOR3_SCHEMA: JsonObject = {
    "oneOf": [
        {
            "type": "object",
            "properties": {
                "x": {"type": "number"},
                "y": {"type": "number"},
                "z": {"type": "number"},
            },
        },
        {
            "type": "array",
            "items": {"type": "number"},
            "minItems": 3,
            "maxItems": 3,
        },
    ],
}

STATIC_TOOLS: list[JsonObject] = [
    # --- Consolidated Core Managers ---
    {
        "name": "unity_scene_manager",
        "description": "Unified scene management (create, open, save, list)",
        "inputSchema": {
            "type": "object",
            "oneOf": [
                {
                    "properties": {
                        "action": {"type": "string", "enum": ["create", "create_scene"]},
                        "name": {"type": "string"},
                        "path": {"type": "string"},
                        "open_if_exists": {"type": "boolean"},
                    },
                    "required": ["action"],
                },
                {
                    "properties": {
                        "action": {"type": "string", "enum": ["open", "open_scene"]},
                        "path": {"type": "string"},
                    },
                    "required": ["action", "path"],
                },
                {
                    "properties": {
                        "action": {"type": "string", "enum": ["save", "save_scene"]},
                        "path": {"type": "string"},
                    },
                    "required": ["action"],
                },
                {"properties": {"action": {"type": "string", "enum": ["list", "list_scenes"]}}, "required": ["action"]},
            ],
        },
    },
    {
        "name": "unity_hierarchy_manager",
        "description": "Unified GameObject hierarchy and lifecycle management",
        "inputSchema": {
            "type": "object",
            "oneOf": [
                {
                    "description": "Create an empty GameObject, including common create-action aliases",
                    "properties": {
                        "action": {
                            "type": "string",
                            "enum": ["create_empty", "create", "create_gameobject", "create_game_object"],
                        },
                        "name": {"type": "string"},
                        "parent_id": {"type": "integer"},
                        "position": VECTOR3_SCHEMA,
                        "rotation": VECTOR3_SCHEMA,
                        "scale": VECTOR3_SCHEMA,
                        "eulerAngles": VECTOR3_SCHEMA,
                        "localScale": VECTOR3_SCHEMA,
                    },
                    "required": ["action", "name"],
                },
                {
                    "description": "Create a primitive GameObject",
                    "properties": {
                        "action": {"const": "create_primitive"},
                        "primitive_type": {
                            "type": "string",
                            "enum": ["Cube", "Sphere", "Capsule", "Cylinder", "Plane", "Quad"],
                        },
                        "name": {"type": "string"},
                        "parent_id": {"type": "integer"},
                        "position": VECTOR3_SCHEMA,
                        "rotation": VECTOR3_SCHEMA,
                        "scale": VECTOR3_SCHEMA,
                        "material_path": {"type": "string"},
                    },
                    "required": ["action", "primitive_type"],
                },
                {
                    "description": "Batch-create a hierarchy of GameObjects",
                    "properties": {
                        "action": {"const": "create_hierarchy"},
                        "tree": {"type": "object"},
                        "parent_id": {"type": "integer"},
                    },
                    "required": ["action", "tree"],
                },
                {
                    "description": "Destroy a GameObject",
                    "properties": {
                        "action": {"const": "destroy"},
                        "instance_id": {"type": "integer"},
                    },
                    "required": ["action", "instance_id"],
                },
                {
                    "description": "Duplicate a GameObject",
                    "properties": {
                        "action": {"const": "duplicate"},
                        "instance_id": {"type": "integer"},
                    },
                    "required": ["action", "instance_id"],
                },
                {
                    "description": "Rename a GameObject, including the rename alias",
                    "properties": {
                        "action": {"type": "string", "enum": ["rename", "set_name"]},
                        "instance_id": {"type": "integer"},
                        "name": {"type": "string"},
                        "new_name": {"type": "string"},
                    },
                    "required": ["action", "instance_id"],
                    "anyOf": [{"required": ["name"]}, {"required": ["new_name"]}],
                },
                {
                    "description": "Move, rotate, or scale a GameObject, including the transform alias",
                    "properties": {
                        "action": {"type": "string", "enum": ["set_transform", "transform"]},
                        "instance_id": {"type": "integer"},
                        "position": VECTOR3_SCHEMA,
                        "rotation": VECTOR3_SCHEMA,
                        "scale": VECTOR3_SCHEMA,
                        "eulerAngles": VECTOR3_SCHEMA,
                        "localScale": VECTOR3_SCHEMA,
                    },
                    "required": ["action", "instance_id"],
                },
                {
                    "description": "Enable or disable a GameObject",
                    "properties": {
                        "action": {"const": "set_active"},
                        "instance_id": {"type": "integer"},
                        "active": {"type": "boolean"},
                    },
                    "required": ["action", "instance_id", "active"],
                },
                {
                    "description": "Reparent a GameObject",
                    "properties": {
                        "action": {"const": "set_parent"},
                        "instance_id": {"type": "integer"},
                        "parent_id": {"type": "integer"},
                    },
                    "required": ["action", "instance_id", "parent_id"],
                },
                {
                    "description": "Reorder a GameObject within its siblings",
                    "properties": {
                        "action": {"const": "set_sibling_index"},
                        "instance_id": {"type": "integer"},
                        "index": {"oneOf": [{"type": "integer"}, {"type": "string", "enum": ["first", "last"]}]},
                    },
                    "required": ["action", "instance_id", "index"],
                },
            ],
        },
    },
    {
        "name": "unity_component_manager",
        "description": "Unified component and property management",
        "inputSchema": {
            "type": "object",
            "oneOf": [
                {
                    "description": "Add a component to a GameObject",
                    "properties": {
                        "action": {"const": "add"},
                        "instance_id": {"type": "integer"},
                        "component_name": {"type": "string"},
                    },
                    "required": ["action", "instance_id", "component_name"],
                },
                {
                    "description": "Remove a component from a GameObject",
                    "properties": {
                        "action": {"const": "remove"},
                        "instance_id": {"type": "integer"},
                        "component_name": {"type": "string"},
                    },
                    "required": ["action", "instance_id", "component_name"],
                },
                {
                    "description": "Inspect all serialized fields of a component",
                    "properties": {
                        "action": {"const": "inspect"},
                        "instance_id": {"type": "integer"},
                        "component_name": {"type": "string"},
                    },
                    "required": ["action", "instance_id", "component_name"],
                },
                {
                    "description": "Get the serialized-field schema for a component type",
                    "properties": {
                        "action": {"const": "get_schema"},
                        "instance_id": {"type": "integer"},
                        "component_name": {"type": "string"},
                    },
                    "required": ["action", "instance_id", "component_name"],
                },
                {
                    "description": "Batch-update multiple properties on a component",
                    "properties": {
                        "action": {"const": "update_properties"},
                        "instance_id": {"type": "integer"},
                        "component_name": {"type": "string"},
                        "properties": {"type": "object"},
                    },
                    "required": ["action", "instance_id", "component_name", "properties"],
                },
                {
                    "description": "Set a single serialized property by name",
                    "properties": {
                        "action": {"const": "set_property"},
                        "instance_id": {"type": "integer"},
                        "property_name": {"type": "string"},
                        "value": {"type": "string"},
                    },
                    "required": ["action", "instance_id", "property_name", "value"],
                },
                {
                    "description": "Enable or disable a component",
                    "properties": {
                        "action": {"const": "set_enabled"},
                        "instance_id": {"type": "integer"},
                        "component_name": {"type": "string"},
                        "enabled": {"type": "boolean"},
                    },
                    "required": ["action", "instance_id", "component_name", "enabled"],
                },
            ],
        },
    },
    {
        "name": "unity_search_manager",
        "description": "Unified discovery and reference search",
        "inputSchema": {
            "type": "object",
            "oneOf": [
                {
                    "description": "Find objects by name regex, tag, or type",
                    "properties": {
                        "strategy": {"const": "regex"},
                        "query": {"type": "string"},
                        "tag": {"type": "string"},
                        "type": {"type": "string"},
                    },
                    "required": ["strategy"],
                },
                {
                    "description": "Find a single object by its scene hierarchy path",
                    "properties": {"strategy": {"const": "path"}, "query": {"type": "string"}},
                    "required": ["strategy", "query"],
                },
                {
                    "description": "Find objects using a natural-language semantic query",
                    "properties": {"strategy": {"const": "semantic"}, "query": {"type": "string"}},
                    "required": ["strategy", "query"],
                },
                {
                    "description": "Find all objects that reference a specific asset or component",
                    "properties": {
                        "strategy": {"const": "references"},
                        "target_id": {"type": "integer"},
                        "target_guid": {"type": "string"},
                    },
                    "required": ["strategy"],
                },
            ],
        },
    },
    {
        "name": "unity_asset_manager",
        "description": "Unified asset and prefab pipeline management",
        "inputSchema": {
            "type": "object",
            "oneOf": [
                {
                    "description": "Search for assets by filter string",
                    "properties": {"action": {"const": "search"}, "filter": {"type": "string"}},
                    "required": ["action"],
                },
                {
                    "description": "Explore metadata and sub-assets at a given asset path",
                    "properties": {"action": {"const": "explore"}, "path": {"type": "string"}},
                    "required": ["action", "path"],
                },
                {
                    "description": "Create a new material asset",
                    "properties": {
                        "action": {"const": "create_material"},
                        "name": {"type": "string"},
                        "shader": {"type": "string"},
                        "path": {"type": "string"},
                        "base_color": {"type": "string"},
                        "color": {"type": "string"},
                        "emission_color": {"type": "string"},
                        "emission": {"type": "string"},
                    },
                    "required": ["action", "name"],
                },
                {
                    "description": "Import an asset from disk into the Unity project",
                    "properties": {"action": {"const": "import"}, "path": {"type": "string"}},
                    "required": ["action", "path"],
                },
                {
                    "description": "Refresh the AssetDatabase to pick up filesystem changes",
                    "properties": {"action": {"const": "refresh"}},
                    "required": ["action"],
                },
                {
                    "description": "Instantiate a prefab from its asset path into the active scene",
                    "properties": {"action": {"const": "instantiate_prefab"}, "path": {"type": "string"}},
                    "required": ["action", "path"],
                },
                {
                    "description": "Create a new prefab asset from an existing scene GameObject",
                    "properties": {
                        "action": {"const": "create_prefab"},
                        "instance_id": {"type": "integer"},
                        "path": {"type": "string"},
                    },
                    "required": ["action", "instance_id", "path"],
                },
                {
                    "description": "Apply all prefab instance overrides back to the prefab asset",
                    "properties": {"action": {"const": "apply_overrides"}, "instance_id": {"type": "integer"}},
                    "required": ["action", "instance_id"],
                },
                {
                    "description": "Revert all prefab instance overrides to match the prefab asset",
                    "properties": {"action": {"const": "revert_overrides"}, "instance_id": {"type": "integer"}},
                    "required": ["action", "instance_id"],
                },
            ],
        },
    },
    {
        "name": "unity_editor_controller",
        "description": "Unified editor state and play mode control",
        "inputSchema": {
            "type": "object",
            "oneOf": [
                {
                    "description": "Undo the last editor action",
                    "properties": {"action": {"const": "undo"}},
                    "required": ["action"],
                },
                {
                    "description": "Redo the previously undone editor action",
                    "properties": {"action": {"const": "redo"}},
                    "required": ["action"],
                },
                {
                    "description": "Enter or exit Play Mode",
                    "properties": {"action": {"const": "play"}, "state": {"type": "boolean"}},
                    "required": ["action", "state"],
                },
                {
                    "description": "Pause or unpause Play Mode",
                    "properties": {"action": {"const": "pause"}, "state": {"type": "boolean"}},
                    "required": ["action", "state"],
                },
                {
                    "description": "Advance Play Mode by one frame while paused",
                    "properties": {"action": {"const": "step"}},
                    "required": ["action"],
                },
                {
                    "description": "Execute an editor menu item by its path (e.g. 'File/Save')",
                    "properties": {"action": {"const": "menu"}, "item_path": {"type": "string"}},
                    "required": ["action", "item_path"],
                },
                {
                    "description": "Read recent Unity console log entries",
                    "properties": {"action": {"const": "read_logs"}, "count": {"type": "integer"}},
                    "required": ["action"],
                },
                {
                    "description": "Clear all Unity console log entries",
                    "properties": {"action": {"const": "clear_logs"}},
                    "required": ["action"],
                },
                {
                    "description": "Get the current editor state (play mode, compiling, etc.)",
                    "properties": {"action": {"const": "get_state"}},
                    "required": ["action"],
                },
                {
                    "description": "Get the Nexus Unity server status and version info",
                    "properties": {"action": {"const": "get_server_status"}},
                    "required": ["action"],
                },
                {
                    "description": "Refresh the AssetDatabase",
                    "properties": {"action": {"const": "refresh_assets"}},
                    "required": ["action"],
                },
                {
                    "description": "Trigger a Unity Test Runner run without waiting for results",
                    "properties": {
                        "action": {"const": "run_tests"},
                        "mode": {"type": "string"},
                        "filter": {"type": "string"},
                    },
                    "required": ["action"],
                },
                {
                    "description": "Retrieve the most recent Test Runner results XML",
                    "properties": {"action": {"const": "get_test_results"}, "result_path": {"type": "string"}},
                    "required": ["action"],
                },
                {
                    "description": "Run tests and block until results are available (or timeout)",
                    "properties": {
                        "action": {"const": "run_tests_wait"},
                        "mode": {"type": "string"},
                        "filter": {"type": "string"},
                        "timeout_seconds": {"type": "integer"},
                        "poll_interval_seconds": {"type": "number"},
                    },
                    "required": ["action"],
                },
                {
                    "description": "Get per-tool invocation statistics",
                    "properties": {"action": {"const": "get_tool_usage_stats"}},
                    "required": ["action"],
                },
                {
                    "description": "Reset all per-tool invocation statistics counters",
                    "properties": {"action": {"const": "reset_tool_usage_stats"}},
                    "required": ["action"],
                },
            ],
        },
    },
    {
        "name": "unity_ui_automation",
        "description": "Unified UI Toolkit window automation",
        "inputSchema": {
            "type": "object",
            "oneOf": [
                {
                    "description": "List all open editor windows",
                    "properties": {"action": {"const": "list_windows"}},
                    "required": ["action"],
                },
                {
                    "description": "Get the UI element hierarchy for a window",
                    "properties": {
                        "action": {"const": "get_hierarchy"},
                        "window_title": {"type": "string"},
                        "deep": {"type": "boolean"},
                    },
                    "required": ["action", "window_title"],
                },
                {
                    "description": "Query UI elements by name, text, or class within a window",
                    "properties": {
                        "action": {"const": "query"},
                        "window_title": {"type": "string"},
                        "name": {"type": "string"},
                        "text": {"type": "string"},
                        "class_name": {"type": "string"},
                    },
                    "required": ["action", "window_title"],
                },
                {
                    "description": "Get the screen rect (position and size) of a window",
                    "properties": {"action": {"const": "get_window_rect"}, "window_title": {"type": "string"}},
                    "required": ["action", "window_title"],
                },
                {
                    "description": "Reposition and resize a window",
                    "properties": {
                        "action": {"const": "set_window_rect"},
                        "window_title": {"type": "string"},
                        "x": {"type": "number"},
                        "y": {"type": "number"},
                        "width": {"type": "number"},
                        "height": {"type": "number"},
                    },
                    "required": ["action", "window_title"],
                },
                {
                    "description": "Capture a screenshot and/or UI hierarchy snapshot of a window",
                    "properties": {
                        "action": {"const": "capture_window_snapshot"},
                        "window_title": {"type": "string"},
                        "include_image": {"type": "boolean"},
                        "include_hierarchy": {"type": "boolean"},
                    },
                    "required": ["action", "window_title"],
                },
                {
                    "description": "Click a named UI element inside a window",
                    "properties": {
                        "action": {"const": "click"},
                        "window_title": {"type": "string"},
                        "element_name": {"type": "string"},
                    },
                    "required": ["action", "window_title", "element_name"],
                },
                {
                    "description": "Type text into a named UI input element inside a window",
                    "properties": {
                        "action": {"const": "input"},
                        "window_title": {"type": "string"},
                        "element_name": {"type": "string"},
                        "text": {"type": "string"},
                    },
                    "required": ["action", "window_title", "element_name", "text"],
                },
            ],
        },
    },
    {
        "name": "unity_wait",
        "description": "Wait for specific Unity editor states or events",
        "inputSchema": {
            "type": "object",
            "oneOf": [
                {
                    "description": "Wait until script compilation and domain reload finishes",
                    "properties": {"condition": {"const": "compilation"}, "timeout_seconds": {"type": "integer"}},
                    "required": ["condition"],
                },
                {
                    "description": "Wait until Play Mode reaches the specified state (true=playing, false=stopped)",
                    "properties": {
                        "condition": {"const": "play_mode"},
                        "state": {"type": "boolean"},
                        "timeout_seconds": {"type": "integer"},
                    },
                    "required": ["condition", "state"],
                },
                {
                    "description": "Wait until all pending asset imports are complete",
                    "properties": {"condition": {"const": "import"}, "timeout_seconds": {"type": "integer"}},
                    "required": ["condition"],
                },
                {
                    "description": "Wait until the editor is fully idle (not compiling, importing, or playing)",
                    "properties": {"condition": {"const": "editor_idle"}, "timeout_seconds": {"type": "integer"}},
                    "required": ["condition"],
                },
            ],
        },
    },
    {
        "name": "unity_playerprefs_manager",
        "description": "Unified PlayerPrefs management",
        "inputSchema": {
            "type": "object",
            "oneOf": [
                {
                    "description": "Read a PlayerPref value by key",
                    "properties": {
                        "action": {"const": "get"},
                        "key": {"type": "string"},
                        "type": {"type": "string", "enum": ["string", "int", "float"]},
                    },
                    "required": ["action", "key"],
                },
                {
                    "description": "Write a PlayerPref value",
                    "properties": {
                        "action": {"const": "set"},
                        "key": {"type": "string"},
                        "value": {"type": "string"},
                        "type": {"type": "string", "enum": ["string", "int", "float"]},
                    },
                    "required": ["action", "key", "value"],
                },
                {
                    "description": "Delete a PlayerPref entry by key; use key 'all' with confirm true to clear all PlayerPrefs",
                    "properties": {
                        "action": {"const": "delete"},
                        "key": {"type": "string"},
                        "confirm": {"type": "boolean"},
                    },
                    "required": ["action", "key"],
                },
                {
                    "description": "List all stored PlayerPref keys and their values",
                    "properties": {"action": {"const": "list"}},
                    "required": ["action"],
                },
            ],
        },
    },
    # --- Specialized Diagnostics ---
    {
        "name": "unity_write_and_compile",
        "description": "High-level macro: Writes multiple files, waits for domain reload, and returns compiler errors. Use for ALL code changes.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "files": {
                    "type": "array",
                    "items": {
                        "type": "object",
                        "properties": {"path": {"type": "string"}, "content": {"type": "string"}},
                        "required": ["path", "content"],
                    },
                },
                "confirm": {
                    "type": "boolean",
                    "description": "Required when writing .cs files because Unity compilation is triggered",
                },
            },
            "required": ["files"],
        },
    },
    {
        "name": "unity_invoke_method",
        "description": "Invoke a C# method on a component via reflection",
        "inputSchema": {
            "type": "object",
            "properties": {
                "instance_id": {"type": "integer"},
                "component_name": {"type": "string"},
                "method_name": {"type": "string"},
                "arguments": {"type": "array"},
            },
            "required": ["instance_id", "method_name"],
        },
    },
    {
        "name": "unity_dump_scene_graph",
        "description": "Dump recursive tree of active scene with components and key fields",
        "inputSchema": {
            "type": "object",
            "properties": {
                "root_id": {"type": "integer"},
                "max_depth": {"type": "integer"},
                "include_all_properties": {"type": "boolean"},
            },
        },
    },
    {
        "name": "unity_get_scene_dependencies",
        "description": "Return a scene-wide dependency map of cross-object references",
        "inputSchema": {"type": "object", "properties": {}},
    },
    {
        "name": "unity_lint_project",
        "description": "Run Roslyn-based C# audit of the entire project",
        "inputSchema": {"type": "object", "properties": {}},
    },
]

STATIC_RESOURCES: list[JsonObject] = [
    {
        "uri": "unity://docs/api-reference",
        "name": "API Reference",
        "mimeType": "text/markdown",
    },
    {
        "uri": "unity://docs/setup",
        "name": "Setup Guide",
        "mimeType": "text/markdown",
    },
]
