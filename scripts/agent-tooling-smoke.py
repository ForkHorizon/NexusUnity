#!/usr/bin/env python3
import base64
import json
import os
import sys
import time

sys.dont_write_bytecode = True

PACKAGE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
EDITOR_DIR = os.path.join(PACKAGE_ROOT, "Editor")
if EDITOR_DIR not in sys.path:
    sys.path.insert(0, EDITOR_DIR)

from nexus_bridge._transport import UNITY_URL, call_unity  # noqa: E402
from nexus_bridge.routing import route_tool  # noqa: E402


def rpc(method: str, params: dict | None = None) -> dict:
    result = call_unity(method, params)
    if "error" in result:
        raise RuntimeError(f"{method} failed: {result['error']}")
    return result.get("result", result)


def bridge_result(name: str, args: dict) -> dict:
    result = route_tool(name, args)
    if "error" in result:
        raise RuntimeError(f"{name} failed: {result['error']}")
    return result.get("result", result)


def summarize_snapshot(snapshot: dict) -> dict:
    return {
        "status": snapshot.get("status"),
        "has_image": bool(snapshot.get("image_base64")),
        "image_bytes_base64": len(snapshot.get("image_base64", "")),
        "has_hierarchy": "ui_hierarchy" in snapshot,
        "rect": snapshot.get("rect"),
        "message": snapshot.get("message"),
    }


def validate_png(result: dict) -> dict:
    data = result.get("data", result)
    try:
        image = base64.b64decode(data.get("image_base64", ""), validate=True)
    except (ValueError, TypeError):
        image = b""
    return {
        "success": result.get("success") is True,
        "png": image.startswith(b"\x89PNG\r\n\x1a\n"),
        "bytes": len(image),
        "width": data.get("width"),
        "height": data.get("height"),
        "duration_ms": result.get("duration_ms"),
        "message": result.get("message"),
    }


def capture_screenshot_series(method: str, attempts: int = 20) -> dict:
    captures = []
    for _ in range(attempts):
        try:
            captures.append(validate_png(rpc(method)))
        except RuntimeError as error:
            captures.append({"success": False, "png": False, "bytes": 0, "message": str(error)})
    valid = [capture for capture in captures if capture["success"] and capture["png"] and capture["bytes"] > 5 * 1024]
    return {
        "attempts": attempts,
        "valid": len(valid),
        "ok": len(valid) >= attempts - 1,
        "captures": captures,
    }


def _run_ui_smoke() -> tuple[dict, dict, dict]:
    rpc("execute_menu_item", {"item_path": "Window/Nexus Unity"})
    time.sleep(0.5)

    query = bridge_result(
        "ui_automation",
        {
            "action": "query",
            "window_title": "Nexus Unity",
            "name": "NexusServerWindowRoot",
        },
    )
    bridge_result(
        "ui_automation",
        {
            "action": "set_window_rect",
            "window_title": "Nexus Unity",
            "x": 80,
            "y": 80,
            "width": 640,
            "height": 720,
        },
    )
    rect = bridge_result("ui_automation", {"action": "get_window_rect", "window_title": "Nexus Unity"})
    snapshot = bridge_result(
        "ui_automation",
        {
            "action": "capture_window_snapshot",
            "window_title": "Nexus Unity",
            "include_image": True,
            "include_hierarchy": True,
        },
    )
    return query, rect, snapshot


def main() -> int:
    started = time.time()
    server = rpc("get_server_status")
    tools = rpc("list_tools")
    rpc("reset_tool_usage_stats")

    query, rect, snapshot = _run_ui_smoke()
    rpc("execute_menu_item", {"item_path": "Window/General/Game"})
    game_screenshots = capture_screenshot_series("capture_game_view_screenshot")
    rpc("execute_menu_item", {"item_path": "Window/General/Inspector"})
    inspector_screenshots = capture_screenshot_series("capture_inspector_screenshot")
    editor_state = bridge_result("editor_controller", {"action": "get_state"})
    stats = rpc("get_tool_usage_stats")
    success = (
        bool(tools)
        and server.get("state")
        and snapshot.get("status") in {None, "Success", "success", "PartialSuccess"}
        and game_screenshots["ok"]
        and inspector_screenshots["ok"]
    )

    summary = {
        "success": bool(success),
        "url": UNITY_URL,
        "elapsed_seconds": round(time.time() - started, 2),
        "server_state": server.get("state"),
        "editor_state": editor_state,
        "tool_count": len(tools),
        "window_query_count": len(query) if isinstance(query, list) else 0,
        "rect": rect.get("rect"),
        "snapshot": summarize_snapshot(snapshot),
        "game_screenshots": game_screenshots,
        "inspector_screenshots": inspector_screenshots,
        "usage_method_count": len(stats.get("tools", [])),
    }

    print(json.dumps(summary, indent=2, sort_keys=True))
    return 0 if summary["success"] else 1


if __name__ == "__main__":
    sys.exit(main())
