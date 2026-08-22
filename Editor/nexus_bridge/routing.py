"""Tool-call routing for the NexusUnity Python bridge.

:func:`route_tool` is the single dispatch entry point: given a tool name (with
the ``unity_`` prefix already stripped) and an arguments dict, it forwards the
call to the appropriate Unity JSON-RPC method via :func:`.client.call_unity`.
"""

from __future__ import annotations

from . import _transport
from .routes_base import (  # noqa: F401
    JsonObject,
    JsonRpcResponse,
    _alias,
    _apply_created_transform,
    _compact,
    _error_object,
    _extract_created_instance_id,
    _first_present,
    _invalid_action,
    _result_object,
    _transform_params,
)
from .routes_editor import (  # noqa: F401
    _poll_test_results,
    _route_editor_controller,
    _route_list_tools,
    _route_playerprefs_manager,
    _route_wait,
    _route_write_and_compile,
    _run_tests_wait,
    _wait_for_compilation,
)
from .routes_managers import (
    _route_asset_manager,
    _route_component_manager,
    _route_hierarchy_manager,
    _route_scene_manager,
    _route_search_manager,
)
from .routes_ui import _route_ui_automation

_HANDLERS = {
    "tools/list": _route_list_tools,
    "list_tools": _route_list_tools,
    "listTools": _route_list_tools,
    "write_and_compile": _route_write_and_compile,
    "apply_code_change": _route_write_and_compile,
    "scene_manager": _route_scene_manager,
    "hierarchy_manager": _route_hierarchy_manager,
    "component_manager": _route_component_manager,
    "search_manager": _route_search_manager,
    "asset_manager": _route_asset_manager,
    "editor_controller": _route_editor_controller,
    "ui_automation": _route_ui_automation,
    "playerprefs_manager": _route_playerprefs_manager,
    "wait": _route_wait,
}


def call_unity(name: str, args: JsonObject | None = None) -> JsonRpcResponse:
    return _transport.call_unity(name, args)


def route_tool(name: str, args: JsonObject) -> JsonRpcResponse:
    handler = _HANDLERS.get(name)
    if handler is not None:
        return handler(args)
    return _transport.call_unity(name, args)
