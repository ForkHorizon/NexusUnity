"""UI automation tool schemas for NexusUnity."""

from __future__ import annotations

from typing import Any

JsonObject = dict[str, Any]

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
