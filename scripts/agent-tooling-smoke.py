#!/usr/bin/env python3
import json
import os
import sys
import time
import urllib.request

sys.dont_write_bytecode = True

PACKAGE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
EDITOR_DIR = os.path.join(PACKAGE_ROOT, "Editor")
if EDITOR_DIR not in sys.path:
    sys.path.insert(0, EDITOR_DIR)

from nexus_bridge.routing import route_tool  # noqa: E402


def nexus_url():
    if os.environ.get("NEXUS_UNITY_URL"):
        url = os.environ["NEXUS_UNITY_URL"]
        return url if url.endswith("/") else url + "/"

    port = os.environ.get("NEXUS_UNITY_PORT", "8081")
    return f"http://127.0.0.1:{port}/"


def timeout_seconds():
    return max(1.0, float(os.environ.get("NEXUS_UNITY_TIMEOUT_SECONDS", "120")))


def rpc(method, params=None):
    payload = {
        "jsonrpc": "2.0",
        "method": method,
        "params": params or {},
        "id": 1,
    }
    data = json.dumps(payload).encode("utf-8")
    request = urllib.request.Request(
        nexus_url(),
        data=data,
        headers={"Content-Type": "application/json"},
    )
    with urllib.request.urlopen(request, timeout=timeout_seconds()) as response:
        result = json.loads(response.read().decode("utf-8"))
    if "error" in result:
        raise RuntimeError(f"{method} failed: {result['error']}")
    return result["result"]


def bridge_result(name, args):
    result = route_tool(name, args)
    if "error" in result:
        raise RuntimeError(f"{name} failed: {result['error']}")
    return result.get("result", result)


def summarize_snapshot(snapshot):
    return {
        "status": snapshot.get("status"),
        "has_image": bool(snapshot.get("image_base64")),
        "image_bytes_base64": len(snapshot.get("image_base64", "")),
        "has_hierarchy": "ui_hierarchy" in snapshot,
        "rect": snapshot.get("rect"),
        "message": snapshot.get("message"),
    }


def main():
    started = time.time()
    summary = {"success": False, "url": nexus_url()}

    server = rpc("get_server_status")
    tools = rpc("list_tools")
    rpc("reset_tool_usage_stats")

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
    rect = bridge_result(
        "ui_automation",
        {
            "action": "get_window_rect",
            "window_title": "Nexus Unity",
        },
    )
    snapshot = bridge_result(
        "ui_automation",
        {
            "action": "capture_window_snapshot",
            "window_title": "Nexus Unity",
            "include_image": True,
            "include_hierarchy": True,
        },
    )
    editor_state = bridge_result("editor_controller", {"action": "get_state"})
    stats = rpc("get_tool_usage_stats")
    success = bool(tools) and server.get("state") and snapshot.get("status") in {None, "Success", "success"}

    summary.update(
        {
            "success": bool(success),
            "elapsed_seconds": round(time.time() - started, 2),
            "server_state": server.get("state"),
            "editor_state": editor_state,
            "tool_count": len(tools),
            "window_query_count": len(query) if isinstance(query, list) else 0,
            "rect": rect.get("rect"),
            "snapshot": summarize_snapshot(snapshot),
            "usage_method_count": len(stats.get("tools", [])),
        }
    )

    print(json.dumps(summary, indent=2, sort_keys=True))
    return 0 if summary["success"] else 1


if __name__ == "__main__":
    sys.exit(main())
