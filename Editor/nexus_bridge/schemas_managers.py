"""Scene and hierarchy manager tool schemas for NexusUnity."""

from __future__ import annotations

from typing import Any

JsonObject = dict[str, Any]

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

SCENE_MANAGER_TOOL: JsonObject = {
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
}

HIERARCHY_MANAGER_TOOL: JsonObject = {
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
}
