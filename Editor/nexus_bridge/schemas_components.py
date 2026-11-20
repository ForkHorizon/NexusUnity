"""Component, search, and asset manager tool schemas for NexusUnity."""

from __future__ import annotations

from typing import Any

JsonObject = dict[str, Any]

COMPONENT_MANAGER_TOOL: JsonObject = {
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
}

SEARCH_MANAGER_TOOL: JsonObject = {
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
                "description": (
                    "Find all objects that reference a specific asset or component. With symbol (or type/name), "
                    "statically list every prefab, variant, scene and ScriptableObject using a C# type, field "
                    "(with each stored value vs the C# default) or UnityEvent method, loaded or not"
                ),
                "properties": {
                    "strategy": {"const": "references"},
                    "target_id": {"type": "integer"},
                    "target_guid": {"type": "string"},
                    "symbol": {
                        "type": "object",
                        "properties": {
                            "kind": {"type": "string", "enum": ["type", "field", "method"]},
                            "type": {"type": "string"},
                            "name": {"type": "string"},
                        },
                    },
                    "kind": {"type": "string", "enum": ["type", "field", "method"]},
                    "type": {"type": "string"},
                    "name": {"type": "string"},
                    "paths": {"type": "string"},
                    "max_results": {"type": "integer"},
                    "level": {"type": "string", "enum": ["static", "editor", "auto"]},
                },
                "required": ["strategy"],
            },
            {
                "description": (
                    "Before changing C# code: what breaks in prefabs, scenes and assets if a field is renamed or "
                    "retyped, a method renamed, a script deleted or a type moved (severity, confidence, fix hint)"
                ),
                "properties": {
                    "strategy": {"const": "impact"},
                    "change": {
                        "type": "string",
                        "enum": ["rename_field", "change_field_type", "rename_method", "delete_script", "move_type"],
                    },
                    "type": {"type": "string"},
                    "name": {"type": "string"},
                    "new_name": {"type": "string"},
                    "paths": {"type": "string"},
                    "max_results": {"type": "integer"},
                    "level": {"type": "string", "enum": ["static", "editor", "auto"]},
                },
                "required": ["strategy", "change", "type"],
            },
        ],
    },
}

ASSET_MANAGER_TOOL: JsonObject = {
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
}
