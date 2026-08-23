"""UI automation routing for NexusUnity."""

from __future__ import annotations

from . import _transport
from .routes_base import JsonObject, JsonRpcResponse, _compact, _first_present, _invalid_action


def _dispatch_ui_inspect(action: str | None, args: JsonObject) -> JsonRpcResponse | None:
    if action == "list_windows":
        return _transport.call_unity("ui_list_windows")
    if action == "get_hierarchy":
        return _transport.call_unity(
            "ui_get_hierarchy",
            _compact(
                {
                    "window_title": args.get("window_title"),
                    "deep": args.get("deep"),
                    "max_depth": args.get("max_depth"),
                    "max_elements": _first_present(args.get("max_elements"), args.get("max_results")),
                }
            ),
        )
    if action == "query":
        return _transport.call_unity(
            "ui_query_elements",
            _compact(
                {
                    "window_title": args.get("window_title"),
                    "name": args.get("name"),
                    "text": args.get("text"),
                    "class_name": args.get("class_name"),
                    "max_depth": args.get("max_depth"),
                    "max_results": _first_present(args.get("max_results"), args.get("max_elements")),
                    "max_elements": args.get("max_elements"),
                }
            ),
        )
    return None


def _dispatch_ui_layout_and_input(action: str | None, args: JsonObject) -> JsonRpcResponse | None:
    if action == "get_window_rect":
        return _transport.call_unity("ui_get_window_rect", {"window_title": args.get("window_title")})
    if action == "set_window_rect":
        return _transport.call_unity(
            "ui_set_window_rect",
            _compact(
                {
                    "window_title": args.get("window_title"),
                    "x": args.get("x"),
                    "y": args.get("y"),
                    "width": args.get("width"),
                    "height": args.get("height"),
                }
            ),
        )
    if action == "capture_window_snapshot":
        return _transport.call_unity(
            "ui_capture_window_snapshot",
            _compact(
                {
                    "window_title": args.get("window_title"),
                    "include_image": args.get("include_image"),
                    "include_hierarchy": args.get("include_hierarchy"),
                    "max_depth": args.get("max_depth"),
                    "max_elements": _first_present(args.get("max_elements"), args.get("max_results")),
                }
            ),
        )
    if action == "click":
        return _transport.call_unity(
            "ui_click", {"window_title": args.get("window_title"), "element_name": args.get("element_name")}
        )
    if action == "input":
        return _transport.call_unity(
            "ui_input_text",
            {
                "window_title": args.get("window_title"),
                "element_name": args.get("element_name"),
                "text": args.get("text", ""),
            },
        )
    return None


def _route_ui_automation(args: JsonObject) -> JsonRpcResponse:
    action = args.get("action")
    inspect_result = _dispatch_ui_inspect(action, args)
    if inspect_result is not None:
        return inspect_result
    action_result = _dispatch_ui_layout_and_input(action, args)
    if action_result is not None:
        return action_result
    return _invalid_action(
        action,
        [
            "list_windows",
            "get_hierarchy",
            "query",
            "get_window_rect",
            "set_window_rect",
            "capture_window_snapshot",
            "click",
            "input",
        ],
    )
