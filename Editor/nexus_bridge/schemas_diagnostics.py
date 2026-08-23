"""Diagnostic, resource, and PlayerPrefs tool schemas for NexusUnity."""

from __future__ import annotations

from typing import Any

JsonObject = dict[str, Any]

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
