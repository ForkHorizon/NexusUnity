"""Editor controller, execution, and utility routing for NexusUnity."""

from __future__ import annotations

import time
from typing import Any

from . import _transport
from .routes_base import (
    JsonObject,
    JsonRpcResponse,
    _compact,
    _error_object,
    _invalid_action,
    _result_object,
)
from .schemas import STATIC_TOOLS


def _poll_test_results(
    start_time: float, timeout: float, poll_interval: float, prev_time: str | None, result_path: str | None
) -> JsonRpcResponse | None:
    while time.time() - start_time < timeout:
        params = {"result_path": result_path} if result_path else {}
        current_resp = _transport.call_unity("get_test_results", params)
        if current_resp and "error" in current_resp:
            return current_resp

        payload = _result_object(current_resp)
        if payload.get("status") == "Success" and payload.get("timestamp_utc") != prev_time:
            test_results = dict(payload)
            test_results["time_waited_seconds"] = round(time.time() - start_time, 2)
            return {"result": test_results}

        time.sleep(poll_interval)
    return None


def _run_tests_wait(args: JsonObject) -> JsonRpcResponse:
    timeout = args.get("timeout_seconds", 180)
    poll_interval = args.get("poll_interval_seconds", 1.0)
    start_time = time.time()

    prev_resp = _transport.call_unity("get_test_results")
    prev_payload = _result_object(prev_resp)
    prev_time = prev_payload.get("timestamp_utc") if prev_payload.get("status") == "Success" else None

    run_params = _compact({"mode": args.get("mode", "EditMode"), "filter": args.get("filter")})
    trigger_resp = _transport.call_unity("run_tests", run_params)
    if trigger_resp and "error" in trigger_resp:
        return trigger_resp

    trigger_payload = _result_object(trigger_resp)
    if trigger_payload.get("status") not in ("Submitted", "Success"):
        return {"result": trigger_payload}

    result_path = trigger_payload.get("result_path")
    polled = _poll_test_results(start_time, timeout, poll_interval, prev_time, result_path)
    if polled is not None:
        return polled

    timeout_result = {
        "status": "Timeout",
        "message": "Timed out waiting for a new Unity TestResults XML file.",
        "time_waited_seconds": round(time.time() - start_time, 2),
        "trigger": trigger_payload,
    }
    if isinstance(result_path, str):
        timeout_result["result_path"] = result_path
    return {"result": timeout_result}


def _wait_for_compilation(timeout: float, start_time: float | None = None) -> JsonRpcResponse:
    start_time = time.time() if start_time is None else start_time
    status: str = "Ready"

    reload_started: bool = False
    reload_wait_timeout = min(20.0, timeout)
    while time.time() - start_time < reload_wait_timeout:
        init_resp = _transport.call_unity("initialize")
        if init_resp is None or "error" in init_resp:
            reload_started = True
            break
        time.sleep(0.5)

    if not reload_started:
        _transport.call_unity("refresh_asset_database")

    while time.time() - start_time < timeout:
        init_resp = _transport.call_unity("initialize")
        if init_resp and "result" in init_resp:
            time.sleep(2.0)
            state_resp = _transport.call_unity("get_editor_state")
            state = _result_object(state_resp)
            if state_resp and "result" in state_resp and not state.get("is_compiling") and not state.get("is_updating"):
                break
        time.sleep(1.0)
    else:
        status = "Timeout"

    return {"result": {"status": status, "time_waited_seconds": round(time.time() - start_time, 2)}}


def _route_list_tools(_: JsonObject) -> JsonRpcResponse:
    return {"result": {"tools": STATIC_TOOLS}}


def _route_write_and_compile(args: JsonObject) -> JsonRpcResponse:
    files = args.get("files", [])
    confirm = args.get("confirm", True)
    start_time: float = time.time()
    _transport.call_unity("clear_logs")

    write_errors: list[JsonObject] = []
    for file_info in files:
        write_resp = _transport.call_unity(
            "write_file",
            _compact({"path": file_info["path"], "content": file_info["content"], "confirm": confirm}),
        )
        write_error = _error_object(write_resp)
        if write_error is not None:
            write_errors.append({"path": file_info["path"], "error": write_error})

    if write_errors:
        return {"result": {"status": "Failed", "message": "Failed to write some files", "errors": write_errors}}

    wait_resp = _wait_for_compilation(timeout=90, start_time=start_time)
    wait_result = _result_object(wait_resp)
    wait_status: str = wait_result["status"]
    time_waited: float = wait_result["time_waited_seconds"]

    compiler_errors: list[JsonObject] = []
    if wait_status == "Ready":
        log_resp = _transport.call_unity("read_logs", {"count": 200})
        log_payload = _result_object(log_resp)
        if log_resp and "result" in log_resp:
            for log_entry in log_payload.get("logs", []):
                entry_type = str(log_entry.get("Type", log_entry.get("type", ""))).lower()
                if entry_type in ["error", "exception", "assert"]:
                    compiler_errors.append(log_entry)

    return {
        "result": {
            "status": "Failed" if compiler_errors else wait_status,
            "time_waited_seconds": time_waited,
            "compiler_errors": compiler_errors,
        }
    }


def _dispatch_editor_action(action: str | None, args: JsonObject) -> JsonRpcResponse | None:
    if action == "undo":
        return _transport.call_unity("undo")
    if action == "redo":
        return _transport.call_unity("redo")
    if action == "play":
        return _transport.call_unity("toggle_play_mode", {"value": args.get("state")})
    if action == "pause":
        return _transport.call_unity("pause_play_mode", {"value": args.get("state")})
    if action == "step":
        return _transport.call_unity("step_frame")
    if action == "menu":
        return _transport.call_unity("execute_menu_item", {"item_path": args.get("item_path")})
    if action == "read_logs":
        return _transport.call_unity("read_logs", {"count": args.get("count", 100)})
    if action == "clear_logs":
        return _transport.call_unity("clear_logs")
    if action == "get_state":
        return _transport.call_unity("get_editor_state")
    if action == "get_server_status":
        return _transport.call_unity("get_server_status")
    if action == "refresh_assets":
        return _transport.call_unity("refresh_asset_database")
    if action == "run_tests":
        return _transport.call_unity(
            "run_tests", _compact({"mode": args.get("mode", "EditMode"), "filter": args.get("filter")})
        )
    if action == "get_test_results":
        return _transport.call_unity("get_test_results", _compact({"result_path": args.get("result_path")}))
    if action == "run_tests_wait":
        return _run_tests_wait(args)
    if action == "get_tool_usage_stats":
        return _transport.call_unity("get_tool_usage_stats")
    if action == "reset_tool_usage_stats":
        return _transport.call_unity("reset_tool_usage_stats")
    return None


def _route_editor_controller(args: JsonObject) -> JsonRpcResponse:
    action = args.get("action")
    result = _dispatch_editor_action(action, args)
    if result is not None:
        return result
    return _invalid_action(
        action,
        [
            "undo",
            "redo",
            "play",
            "pause",
            "step",
            "menu",
            "read_logs",
            "clear_logs",
            "get_state",
            "get_server_status",
            "refresh_assets",
            "run_tests",
            "get_test_results",
            "run_tests_wait",
            "get_tool_usage_stats",
            "reset_tool_usage_stats",
        ],
    )


def _is_condition_ready(condition: str, target_state: bool) -> bool:
    if condition == "play_mode":
        resp = _transport.call_unity("get_editor_state")
        state = _result_object(resp)
        return bool(resp and "result" in resp and state.get("is_playing") == target_state)
    if condition == "import":
        resp = _transport.call_unity("is_asset_import_idle")
        state = _result_object(resp)
        return bool(resp and "result" in resp and state.get("is_idle"))
    if condition == "editor_idle":
        resp = _transport.call_unity("is_editor_idle")
        state = _result_object(resp)
        return bool(resp and "result" in resp and state.get("is_idle"))
    return False


def _wait_for_condition_loop(condition: str, timeout: float, start_time: float, target_state: bool) -> str:
    while time.time() - start_time < timeout:
        if _is_condition_ready(condition, target_state):
            return "Ready"
        time.sleep(1.0)
    return "Timeout"


def _route_wait(args: JsonObject) -> JsonRpcResponse:
    condition: Any = args.get("condition")
    timeout: float = args.get("timeout_seconds", 60)
    start_time: float = time.time()

    if condition == "compilation":
        return _wait_for_compilation(timeout=timeout, start_time=start_time)
    if condition in ("play_mode", "import", "editor_idle"):
        target_state = args.get("state", True)
        status = _wait_for_condition_loop(condition, timeout, start_time, target_state)
        return {"result": {"status": status, "time_waited_seconds": round(time.time() - start_time, 2)}}
    return _invalid_action(condition, ["compilation", "play_mode", "import", "editor_idle"])


def _route_playerprefs_manager(args: JsonObject) -> JsonRpcResponse:
    action = args.get("action")
    if action == "get":
        return _transport.call_unity(
            "get_player_pref", _compact({"key": args.get("key"), "type": args.get("type", "string")})
        )
    if action == "set":
        return _transport.call_unity(
            "set_player_pref",
            _compact({"key": args.get("key"), "value": args.get("value"), "type": args.get("type", "string")}),
        )
    if action == "delete":
        return _transport.call_unity(
            "delete_player_pref", _compact({"key": args.get("key"), "confirm": args.get("confirm")})
        )
    if action == "list":
        return _transport.call_unity("list_player_prefs")
    return _invalid_action(action, ["get", "set", "delete", "list"])
