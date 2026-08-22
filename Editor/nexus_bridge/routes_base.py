"""Routing base helpers and types for NexusUnity Python bridge."""

from __future__ import annotations

from typing import Any
from collections.abc import Mapping, Sequence

from . import _transport

JsonObject = dict[str, Any]
JsonRpcResponse = dict[str, Any]


def _compact(params: JsonObject) -> JsonObject:
    return {key: value for key, value in params.items() if value is not None}


def _first_present(*values: Any) -> Any:
    for value in values:
        if value is not None:
            return value
    return None


def _alias(action_name: str | None, aliases: Mapping[str, str]) -> str | None:
    if action_name is None:
        return None
    return aliases.get(action_name, action_name)


def _invalid_action(action_name: str | None, valid_actions: Sequence[str]) -> JsonRpcResponse:
    valid = ", ".join(valid_actions)
    error_payload = {
        "code": -32602,
        "message": f"Invalid action: {action_name}. Valid actions: {valid}",
    }
    return {"error": error_payload}


def _result_object(response: JsonRpcResponse | None) -> JsonObject:
    if not response:
        return {}
    result_payload = response.get("result")
    return result_payload if isinstance(result_payload, dict) else {}


def _error_object(response: JsonRpcResponse | None) -> JsonObject | None:
    if not response:
        return None
    return response.get("error")


def _transform_params(args: JsonObject, instance_id: int | None = None) -> JsonObject:
    params: JsonObject = {"instance_id": instance_id if instance_id is not None else args.get("instance_id")}
    for key in ["position", "rotation", "scale", "eulerAngles", "localScale"]:
        params[key] = args.get(key)
    return _compact(params)


def _extract_created_instance_id(response: JsonRpcResponse) -> int | None:
    if "error" in response:
        return None
    result_payload = _result_object(response)
    data = result_payload.get("data", {})
    return data.get("instance_id") if isinstance(data, dict) else None


def _apply_created_transform(response: JsonRpcResponse, args: JsonObject) -> JsonRpcResponse:
    instance_id = _extract_created_instance_id(response)
    if instance_id is None:
        return response
    params = _transform_params(args, instance_id)
    if len(params) <= 1:
        return response
    transform_response = _transport.call_unity("set_transform", params)
    if transform_response and "error" in transform_response:
        return transform_response
    return response
