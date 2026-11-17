"""Static tool and resource definitions for the NexusUnity MCP bridge.

:data:`STATIC_TOOLS` is the authoritative per-action description of the MCP
tools: manager tools list one ``oneOf`` variant per action.

:data:`MCP_TOOLS` is what ``tools/list`` advertises. Claude's tool API rejects
``oneOf`` / ``anyOf`` / ``allOf`` at the top level of an input schema, and
clients then drop every parameter, so each tool there has one flat object
schema: the union of the variant properties, the action key as an ``enum``,
and the per-action requirements in its description.

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

_COMBINATORS = ("oneOf", "anyOf", "allOf")


def _variant_values(schema: JsonObject) -> list[Any]:
    return [schema["const"]] if "const" in schema else list(schema.get("enum", []))


def _discriminator(variants: list[JsonObject]) -> str:
    keys = [
        {name for name, prop in variant.get("properties", {}).items() if _variant_values(prop)} for variant in variants
    ]
    return sorted(set.intersection(*keys))[0]


def _variant_line(variant: JsonObject, key: str) -> str:
    values = "/".join(f"`{value}`" for value in _variant_values(variant["properties"][key]))
    needs = [name for name in variant.get("required", []) if name != key]
    either = [" or ".join(option.get("required", [])) for option in variant.get("anyOf", [])]
    if either:
        needs.append("one of: " + ", ".join(either))
    line = f"{values}: {variant.get('description', '').rstrip('.')}"
    return line + (f" (needs {', '.join(needs)})" if needs else "")


def flatten_tool(tool: JsonObject) -> JsonObject:
    """Return ``tool`` with a top-level ``oneOf`` input schema merged into one object schema."""
    schema = tool["inputSchema"]
    variants = schema.get("oneOf")
    if not variants:
        return tool
    key = _discriminator(variants)
    properties: JsonObject = {}
    values: list[Any] = []
    for variant in variants:
        values += [value for value in _variant_values(variant["properties"][key]) if value not in values]
        for name, prop in variant.get("properties", {}).items():
            if name != key:
                properties.setdefault(name, prop)
    choice = {
        "type": "string",
        "enum": values,
        "description": "; ".join(_variant_line(variant, key) for variant in variants) + ".",
    }
    flat = {name: value for name, value in schema.items() if name not in _COMBINATORS}
    flat.update({"type": "object", "properties": {key: choice, **properties}, "required": [key]})
    return {**tool, "inputSchema": flat}


MCP_TOOLS: list[JsonObject] = [flatten_tool(tool) for tool in STATIC_TOOLS]

__all__ = ["MCP_TOOLS", "STATIC_RESOURCES", "STATIC_TOOLS", "VECTOR3_SCHEMA", "flatten_tool"]
