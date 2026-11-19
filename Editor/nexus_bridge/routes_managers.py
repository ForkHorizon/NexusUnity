"""Manager routing (scene, hierarchy, component, search, asset) for NexusUnity."""

from __future__ import annotations

from . import _transport
from .routes_base import (
    ActionHandler,
    JsonObject,
    JsonRpcResponse,
    _alias,
    _apply_created_transform,
    _compact,
    _dispatch,
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


def _instance_call(method: str, *keys: str) -> ActionHandler:
    """Handler that sends ``instance_id`` plus the named argument keys, unchanged."""
    return lambda args: _transport.call_unity(
        method, {"instance_id": args.get("instance_id"), **{key: args.get(key) for key in keys}}
    )


_HIERARCHY_MODIFY_ACTIONS: dict[str, ActionHandler] = {
    "set_name": lambda args: _transport.call_unity(
        "set_property",
        {
            "instance_id": args.get("instance_id"),
            "property_name": "m_Name",
            "value": args.get("name") or args.get("new_name"),
        },
    ),
    "set_transform": lambda args: _transport.call_unity("set_transform", _transform_params(args)),
    "destroy": _instance_call("destroy_game_object"),
    "duplicate": _instance_call("duplicate_object"),
    "set_active": _instance_call("set_active", "active"),
    "set_parent": _instance_call("set_parent", "parent_id"),
    "set_sibling_index": _instance_call("set_sibling_index", "index"),
}


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
    modified = _dispatch(_HIERARCHY_MODIFY_ACTIONS, action, args)
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


_COMPONENT_ACTIONS: dict[str, ActionHandler] = {
    "add": _instance_call("add_component", "component_name"),
    "remove": _instance_call("remove_component", "component_name"),
    "inspect": _instance_call("inspect_component", "component_name"),
    "get_schema": _instance_call("get_component_schema", "component_name"),
    "update_properties": _instance_call("update_component", "component_name", "properties"),
    "set_property": _instance_call("set_property", "property_name", "value"),
    "set_enabled": _instance_call("set_enabled", "component_name", "enabled"),
}


def _route_component_manager(args: JsonObject) -> JsonRpcResponse:
    action = args.get("action")
    result = _dispatch(_COMPONENT_ACTIONS, action, args)
    if result is not None:
        return result
    return _invalid_action(action, list(_COMPONENT_ACTIONS))


_IMPACT_KEYS = ("change", "kind", "type", "name", "new_name", "paths", "changed_scripts", "max_results")


def _asset_impact(op: str, args: JsonObject) -> JsonRpcResponse:
    """Call the read-only nexus.asset_impact command; a nested symbol {kind, type, name} is flattened."""
    symbol = args.get("symbol")
    merged = {**symbol, **args} if isinstance(symbol, dict) else args
    return _transport.call_unity(
        "nexus.asset_impact", {"op": op, **_compact({key: merged.get(key) for key in _IMPACT_KEYS})}
    )


def _route_lint_project(args: JsonObject) -> JsonRpcResponse:
    if args.get("scope") == "assets":
        return _asset_impact("validate_asset_integrity", args)
    return _transport.call_unity("lint_project", args)


def _search_references(args: JsonObject) -> JsonRpcResponse:
    if args.get("symbol") or args.get("type"):
        return _asset_impact("find_references", args)
    return _transport.call_unity(
        "find_references", {"target_id": args.get("target_id"), "target_guid": args.get("target_guid")}
    )


_SEARCH_STRATEGIES: dict[str, ActionHandler] = {
    "regex": lambda args: _transport.call_unity(
        "find_objects", {"name": args.get("query"), "tag": args.get("tag"), "type": args.get("type")}
    ),
    "path": lambda args: _transport.call_unity("find_by_path", {"path": args.get("query")}),
    "semantic": lambda args: _transport.call_unity("semantic_find", {"query": args.get("query")}),
    "references": _search_references,
    "impact": lambda args: _asset_impact("analyze_change_impact", args),
}


def _route_search_manager(args: JsonObject) -> JsonRpcResponse:
    strategy = args.get("strategy")
    handler = _SEARCH_STRATEGIES.get(strategy) if isinstance(strategy, str) else None
    if handler is not None:
        return handler(args)
    return {
        "error": {
            "code": -32602,
            "message": f"Invalid strategy: {strategy}. Valid strategies: {', '.join(_SEARCH_STRATEGIES)}",
        }
    }


def _path_call(method: str) -> ActionHandler:
    return lambda args: _transport.call_unity(method, {"path": args.get("path")})


_ASSET_ACTIONS: dict[str, ActionHandler] = {
    "search": lambda args: _transport.call_unity("list_assets", {"filter": args.get("filter")}),
    "explore": _path_call("explore_asset"),
    "create_material": lambda args: _transport.call_unity(
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
    ),
    "import": _path_call("import_asset"),
    "refresh": lambda _args: _transport.call_unity("refresh_asset_database"),
    "instantiate_prefab": _path_call("instantiate_prefab"),
    "create_prefab": _instance_call("create_prefab", "path"),
    "apply_overrides": _instance_call("apply_prefab_overrides"),
    "revert_overrides": _instance_call("revert_prefab_overrides"),
}


def _route_asset_manager(args: JsonObject) -> JsonRpcResponse:
    action = args.get("action")
    result = _dispatch(_ASSET_ACTIONS, action, args)
    if result is not None:
        return result
    return _invalid_action(action, list(_ASSET_ACTIONS))
