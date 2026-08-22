"""Manager routing (scene, hierarchy, component, search, asset) for NexusUnity."""

from __future__ import annotations

from . import _transport
from .routes_base import (
    JsonObject,
    JsonRpcResponse,
    _alias,
    _apply_created_transform,
    _compact,
    _invalid_action,
    _transform_params,
)


def _route_scene_manager(args: JsonObject) -> JsonRpcResponse:
    aliases = {"create_scene": "create", "open_scene": "open", "save_scene": "save", "list_scenes": "list"}
    action = _alias(args.get("action"), aliases)
    if action == "create":
        return _transport.call_unity(
            "create_scene",
            _compact(
                {"name": args.get("name"), "path": args.get("path"), "open_if_exists": args.get("open_if_exists")}
            ),
        )
    if action == "open":
        return _transport.call_unity("open_scene", {"path": args.get("path")})
    if action == "save":
        return _transport.call_unity("save_scene", {"path": args.get("path")})
    if action == "list":
        return _transport.call_unity("list_scenes")
    return _invalid_action(
        args.get("action"),
        ["create", "create_scene", "open", "open_scene", "save", "save_scene", "list", "list_scenes"],
    )


def _dispatch_hierarchy_create(action: str | None, args: JsonObject) -> JsonRpcResponse | None:
    if action == "create_empty":
        response = _transport.call_unity(
            "create_game_object", _compact({"name": args.get("name"), "parent_id": args.get("parent_id")})
        )
        return _apply_created_transform(response, args)
    if action == "create_primitive":
        return _transport.call_unity(
            "create_primitive",
            _compact(
                {
                    "primitive_type": args.get("primitive_type"),
                    "name": args.get("name"),
                    "parent_id": args.get("parent_id"),
                    "position": args.get("position"),
                    "rotation": args.get("rotation"),
                    "scale": args.get("scale"),
                    "material_path": args.get("material_path"),
                }
            ),
        )
    if action == "create_hierarchy":
        return _transport.call_unity(
            "create_hierarchy", _compact({"tree": args.get("tree"), "parent_id": args.get("parent_id")})
        )
    return None


def _dispatch_hierarchy_modify(action: str | None, args: JsonObject) -> JsonRpcResponse | None:
    if action == "set_name":
        return _transport.call_unity(
            "set_property",
            {
                "instance_id": args.get("instance_id"),
                "property_name": "m_Name",
                "value": args.get("name") or args.get("new_name"),
            },
        )
    if action == "set_transform":
        return _transport.call_unity("set_transform", _transform_params(args))
    if action == "destroy":
        return _transport.call_unity("destroy_game_object", {"instance_id": args.get("instance_id")})
    if action == "duplicate":
        return _transport.call_unity("duplicate_object", {"instance_id": args.get("instance_id")})
    if action == "set_active":
        return _transport.call_unity(
            "set_active", {"instance_id": args.get("instance_id"), "active": args.get("active")}
        )
    if action == "set_parent":
        return _transport.call_unity(
            "set_parent", {"instance_id": args.get("instance_id"), "parent_id": args.get("parent_id")}
        )
    if action == "set_sibling_index":
        return _transport.call_unity(
            "set_sibling_index", {"instance_id": args.get("instance_id"), "index": args.get("index")}
        )
    return None


def _route_hierarchy_manager(args: JsonObject) -> JsonRpcResponse:
    aliases = {
        "create": "create_empty",
        "create_gameobject": "create_empty",
        "create_game_object": "create_empty",
        "rename": "set_name",
        "transform": "set_transform",
    }
    action = _alias(args.get("action"), aliases)
    created = _dispatch_hierarchy_create(action, args)
    if created is not None:
        return created
    modified = _dispatch_hierarchy_modify(action, args)
    if modified is not None:
        return modified
    return _invalid_action(
        args.get("action"),
        [
            "create_empty",
            "create",
            "create_gameobject",
            "create_game_object",
            "create_primitive",
            "create_hierarchy",
            "destroy",
            "duplicate",
            "rename",
            "set_name",
            "set_transform",
            "set_active",
            "set_parent",
            "set_sibling_index",
        ],
    )


def _route_component_manager(args: JsonObject) -> JsonRpcResponse:
    action = args.get("action")
    if action == "add":
        return _transport.call_unity(
            "add_component", {"instance_id": args.get("instance_id"), "component_name": args.get("component_name")}
        )
    if action == "remove":
        return _transport.call_unity(
            "remove_component", {"instance_id": args.get("instance_id"), "component_name": args.get("component_name")}
        )
    if action == "inspect":
        return _transport.call_unity(
            "inspect_component", {"instance_id": args.get("instance_id"), "component_name": args.get("component_name")}
        )
    if action == "get_schema":
        return _transport.call_unity(
            "get_component_schema",
            {"instance_id": args.get("instance_id"), "component_name": args.get("component_name")},
        )
    if action == "update_properties":
        return _transport.call_unity(
            "update_component",
            {
                "instance_id": args.get("instance_id"),
                "component_name": args.get("component_name"),
                "properties": args.get("properties"),
            },
        )
    if action == "set_property":
        return _transport.call_unity(
            "set_property",
            {
                "instance_id": args.get("instance_id"),
                "property_name": "m_Name",
                "value": args.get("value"),
            },
        )
    if action == "set_enabled":
        return _transport.call_unity(
            "set_enabled",
            {
                "instance_id": args.get("instance_id"),
                "component_name": args.get("component_name"),
                "enabled": args.get("enabled"),
            },
        )
    return _invalid_action(
        action, ["add", "remove", "inspect", "get_schema", "update_properties", "set_property", "set_enabled"]
    )


def _route_search_manager(args: JsonObject) -> JsonRpcResponse:
    strategy = args.get("strategy")
    if strategy == "regex":
        return _transport.call_unity(
            "find_objects", {"name": args.get("query"), "tag": args.get("tag"), "type": args.get("type")}
        )
    if strategy == "path":
        return _transport.call_unity("find_by_path", {"path": args.get("query")})
    if strategy == "semantic":
        return _transport.call_unity("semantic_find", {"query": args.get("query")})
    if strategy == "references":
        return _transport.call_unity(
            "find_references", {"target_id": args.get("target_id"), "target_guid": args.get("target_guid")}
        )
    return {
        "error": {
            "code": -32602,
            "message": f"Invalid strategy: {strategy}. Valid strategies: regex, path, semantic, references",
        }
    }


def _route_asset_manager(args: JsonObject) -> JsonRpcResponse:
    action = args.get("action")
    if action == "search":
        return _transport.call_unity("list_assets", {"filter": args.get("filter")})
    if action == "explore":
        return _transport.call_unity("explore_asset", {"path": args.get("path")})
    if action == "create_material":
        return _transport.call_unity(
            "create_material",
            _compact(
                {
                    "name": args.get("name"),
                    "shader": args.get("shader"),
                    "path": args.get("path"),
                    "base_color": args.get("base_color") or args.get("color"),
                    "emission_color": args.get("emission_color") or args.get("emission"),
                }
            ),
        )
    if action == "import":
        return _transport.call_unity("import_asset", {"path": args.get("path")})
    if action == "refresh":
        return _transport.call_unity("refresh_asset_database")
    if action == "instantiate_prefab":
        return _transport.call_unity("instantiate_prefab", {"path": args.get("path")})
    if action == "create_prefab":
        return _transport.call_unity(
            "create_prefab", {"instance_id": args.get("instance_id"), "path": args.get("path")}
        )
    if action == "apply_overrides":
        return _transport.call_unity("apply_prefab_overrides", {"instance_id": args.get("instance_id")})
    if action == "revert_overrides":
        return _transport.call_unity("revert_prefab_overrides", {"instance_id": args.get("instance_id")})
    return _invalid_action(
        action,
        [
            "search",
            "explore",
            "create_material",
            "import",
            "refresh",
            "instantiate_prefab",
            "create_prefab",
            "apply_overrides",
            "revert_overrides",
        ],
    )
