"""Static tool and resource definitions for the NexusUnity MCP bridge.

:data:`STATIC_TOOLS` is the authoritative list of MCP tools exposed to AI
clients. Each entry follows the JSON Schema / MCP tool-definition shape.

:data:`STATIC_RESOURCES` lists the static read-only resources advertised via
``resources/list``.
"""

from __future__ import annotations

from typing import Any

from .schemas_components import (
    ASSET_MANAGER_TOOL,
    COMPONENT_MANAGER_TOOL,
    SEARCH_MANAGER_TOOL,
)
from .schemas_editor import (
    DIAGNOSTIC_TOOLS,
    EDITOR_CONTROLLER_TOOL,
    PLAYERPREFS_MANAGER_TOOL,
    STATIC_RESOURCES,
    UI_AUTOMATION_TOOL,
    WAIT_TOOL,
    WRITE_AND_COMPILE_TOOL,
)
from .schemas_managers import (
    HIERARCHY_MANAGER_TOOL,
    SCENE_MANAGER_TOOL,
    VECTOR3_SCHEMA,
)

JsonObject = dict[str, Any]

STATIC_TOOLS: list[JsonObject] = [
    SCENE_MANAGER_TOOL,
    HIERARCHY_MANAGER_TOOL,
    COMPONENT_MANAGER_TOOL,
    SEARCH_MANAGER_TOOL,
    ASSET_MANAGER_TOOL,
    EDITOR_CONTROLLER_TOOL,
    UI_AUTOMATION_TOOL,
    WAIT_TOOL,
    PLAYERPREFS_MANAGER_TOOL,
    WRITE_AND_COMPILE_TOOL,
    *DIAGNOSTIC_TOOLS,
]

__all__ = ["STATIC_RESOURCES", "STATIC_TOOLS", "VECTOR3_SCHEMA"]
