"""Editor controller, wait, and compilation tool schemas for NexusUnity."""

from __future__ import annotations

from typing import Any

from .schemas_diagnostics import (
    DIAGNOSTIC_TOOLS,
    PLAYERPREFS_MANAGER_TOOL,
    STATIC_RESOURCES,
)
from .schemas_ui import UI_AUTOMATION_TOOL

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

__all__ = [
    "DIAGNOSTIC_TOOLS",
    "EDITOR_CONTROLLER_TOOL",
    "PLAYERPREFS_MANAGER_TOOL",
    "STATIC_RESOURCES",
    "UI_AUTOMATION_TOOL",
    "WAIT_TOOL",
    "WRITE_AND_COMPILE_TOOL",
]
