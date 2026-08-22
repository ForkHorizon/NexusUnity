"""Editor controller, UI automation, and execution tool schemas for NexusUnity."""

from __future__ import annotations

from typing import Any

JsonObject = dict[str, Any]

EDITOR_CONTROLLER_TOOL: JsonObject = {
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
}

UI_AUTOMATION_TOOL: JsonObject = {
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
                    "max_depth": {"type": "integer"},
                    "max_elements": {"type": "integer"},
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
                    "max_depth": {"type": "integer"},
                    "max_results": {"type": "integer"},
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
                    "max_depth": {"type": "integer"},
                    "max_elements": {"type": "integer"},
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
}

WAIT_TOOL: JsonObject = {
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
}

PLAYERPREFS_MANAGER_TOOL: JsonObject = {
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
}

WRITE_AND_COMPILE_TOOL: JsonObject = {
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
}

DIAGNOSTIC_TOOLS: list[JsonObject] = [
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
    {"uri": "unity://docs/api-reference", "name": "API Reference", "mimeType": "text/markdown"},
    {"uri": "unity://docs/setup", "name": "Setup Guide", "mimeType": "text/markdown"},
]
