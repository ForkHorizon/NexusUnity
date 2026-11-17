"""Editor controller, execution, and utility routing for NexusUnity."""

from __future__ import annotations

import time
from typing import Any

from . import _transport
from .routes_base import (
    ActionHandler,
    JsonObject,
    JsonRpcResponse,
    _compact,
    _dispatch,
    _error_object,
    _invalid_action,
    _result_object,
)
from .schemas import MCP_TOOLS


def _poll_test_results(
    start_time: float, timeout: float, poll_interval: float, prev_time: str | None, result_path: str | None
) -> tuple[JsonRpcResponse | None, str | None]:
    """Poll until new results appear; returns (response or None on timeout, last unreachable error)."""
    last_unreachable: str | None = None
    while time.time() - start_time < timeout:
        params = {"result_path": result_path} if result_path else {}
        current_resp = _transport.call_unity("get_test_results", params)
        error = current_resp.get("error") if current_resp else None
        if error:
            message = str(error.get("message", ""))
            # The package tests stop and restart the server, and a run can trigger a domain reload,
            # so the server drops out for a few seconds mid-run. Keep polling until the timeout.
            if not message.startswith("Unity Server unreachable"):
                return current_resp, last_unreachable
            last_unreachable = message
        else:
            payload = _result_object(current_resp)
            if payload.get("status") == "Success" and payload.get("timestamp_utc") != prev_time:
                test_results = dict(payload)
                test_results["time_waited_seconds"] = round(time.time() - start_time, 2)
                return {"result": test_results}, last_unreachable

        time.sleep(poll_interval)
    return None, last_unreachable


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
    polled, last_unreachable = _poll_test_results(start_time, timeout, poll_interval, prev_time, result_path)
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
    if last_unreachable:
        timeout_result["last_error"] = last_unreachable
    return {"result": timeout_result}


_READINESS_POLL_SECONDS = 0.2


def _epoch_value(epoch: object, key: str) -> int:
    if not isinstance(epoch, dict):
        return 0
    value = epoch.get(key, 0)
    return value if isinstance(value, int) else 0


def _compilation_result(status: str, ready: bool, reason: str, epoch: JsonObject, start_time: float) -> JsonRpcResponse:
    return {
        "result": {
            "status": status,
            "ready": ready,
            "reason": reason,
            "epoch": epoch,
            "time_waited_seconds": round(time.time() - start_time, 2),
        }
    }


def _readiness_cycle_finished(
    baseline_compile: int, baseline_reload: int, payload: JsonObject, saw_blocked: bool
) -> bool:
    """Accept ready immediately when idle. After a blocked snapshot, require an epoch move or an explicit no-op."""
    if not payload.get("ready") or payload.get("reconnect"):
        return False
    if not saw_blocked:
        return payload.get("reason") in ("ready", "already_ready")
    epoch = payload.get("epoch") if isinstance(payload.get("epoch"), dict) else {}
    if _epoch_value(epoch, "reload") > baseline_reload or _epoch_value(epoch, "compile") > baseline_compile:
        return True
    return payload.get("reason") == "already_ready"


def _wait_for_compilation(timeout: float, start_time: float | None = None) -> JsonRpcResponse:
    """Re-poll Editor readiness. A dropped connection is not success; one HTTP call cannot span a domain reload."""
    start_time = time.time() if start_time is None else start_time
    baseline_compile = 0
    baseline_reload = 0
    have_baseline = False
    saw_blocked = False
    last_reason = "timeout"
    last_epoch: JsonObject = {}
    while time.time() - start_time < timeout:
        resp = _transport.call_unity("is_editor_idle")
        payload = _result_object(resp)
        epoch = payload.get("epoch")
        if "ready" not in payload or not isinstance(epoch, dict):
            last_reason = "domain_reload" if not resp or "error" in (resp or {}) else "unknown"
            time.sleep(_READINESS_POLL_SECONDS)
            continue
        if not have_baseline:
            baseline_compile = _epoch_value(epoch, "compile")
            baseline_reload = _epoch_value(epoch, "reload")
            have_baseline = True
        last_epoch = epoch
        last_reason = str(payload.get("reason") or "unknown")
        if payload.get("reconnect") or last_reason == "domain_reload" or not payload.get("ready"):
            saw_blocked = True
            time.sleep(_READINESS_POLL_SECONDS)
            continue
        if _readiness_cycle_finished(baseline_compile, baseline_reload, payload, saw_blocked):
            return _compilation_result("Ready", True, last_reason, epoch, start_time)
        saw_blocked = True
        time.sleep(_READINESS_POLL_SECONDS)
    return _compilation_result("Timeout", False, last_reason, last_epoch, start_time)


def _route_list_tools(_: JsonObject) -> JsonRpcResponse:
    return {"result": {"tools": MCP_TOOLS}}


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


def _call(method: str) -> ActionHandler:
    return lambda _args: _transport.call_unity(method)


def _run_tests_wait_action(args: JsonObject) -> JsonRpcResponse:
    """Look up _run_tests_wait at call time so callers and tests can replace it."""
    return _run_tests_wait(args)


_EDITOR_ACTIONS: dict[str, ActionHandler] = {
    "undo": _call("undo"),
    "redo": _call("redo"),
    "play": lambda args: _transport.call_unity("toggle_play_mode", {"value": args.get("state")}),
    "pause": lambda args: _transport.call_unity("pause_play_mode", {"value": args.get("state")}),
    "step": _call("step_frame"),
    "menu": lambda args: _transport.call_unity("execute_menu_item", {"item_path": args.get("item_path")}),
    "read_logs": lambda args: _transport.call_unity("read_logs", {"count": args.get("count", 100)}),
    "clear_logs": _call("clear_logs"),
    "get_state": _call("get_editor_state"),
    "get_server_status": _call("get_server_status"),
    "refresh_assets": _call("refresh_asset_database"),
    "run_tests": lambda args: _transport.call_unity(
        "run_tests", _compact({"mode": args.get("mode", "EditMode"), "filter": args.get("filter")})
    ),
    "get_test_results": lambda args: _transport.call_unity(
        "get_test_results", _compact({"result_path": args.get("result_path")})
    ),
    "run_tests_wait": _run_tests_wait_action,
    "get_tool_usage_stats": _call("get_tool_usage_stats"),
    "reset_tool_usage_stats": _call("reset_tool_usage_stats"),
}


def _route_editor_controller(args: JsonObject) -> JsonRpcResponse:
    action = args.get("action")
    result = _dispatch(_EDITOR_ACTIONS, action, args)
    if result is not None:
        return result
    return _invalid_action(action, list(_EDITOR_ACTIONS))


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
