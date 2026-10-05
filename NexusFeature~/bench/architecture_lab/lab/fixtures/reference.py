"""Reference solutions the self-check runs through the official backend.

A task whose reference path fails is INVALID and is not scored.
"""

from __future__ import annotations

from typing import Any


def open_lab(backend) -> dict[str, Any]:
    return backend("open_scene", {"path": "Assets/LabFixture/Scenes/Lab.unity", "additive": False})


def find_marker(backend) -> dict[str, Any] | None:
    result = backend("find_gameobjects", {"type": "LabMarker", "include_inactive": True})
    data = _data(result)
    objects = data.get("gameObjects") if isinstance(data, dict) else None
    if not objects:
        return None
    path = objects[0].get("hierarchyPath") or ""
    name = path.rstrip("/").split("/")[-1]
    return {"object_name": name, "hierarchy_path": path, "raw": result}


def read_field(backend, target: str, field: str) -> Any:
    result = backend(
        "get_serialized_fields",
        {"target": target, "component": "LabMarker", "field": field, "format": "value"},
    )
    return _value(_data(result), field)


def write_field(backend, target: str, field: str, value: Any) -> dict[str, Any]:
    return backend(
        "set_serialized_field",
        {"target": target, "component": "LabMarker", "field": field, "value": value},
    )


def editor_status(backend) -> dict[str, Any]:
    return _data(backend("editor_status", {})) or {}


def _data(result: dict[str, Any]) -> Any:
    from lab.adapters.unity_cli import command_body

    payload = result.get("payload") if isinstance(result, dict) else None
    return command_body(payload)


def _value(data: Any, field: str | None = None) -> Any:
    """Pull a scalar out of the Pipeline serialized-field descriptor."""
    if isinstance(data, dict) and isinstance(data.get("fields"), list):
        fields = data["fields"]
        for item in fields:
            if not isinstance(item, dict):
                continue
            if field is None or item.get("name") == field or item.get("path") == field:
                return item.get("value")
        if fields and isinstance(fields[0], dict):
            return fields[0].get("value")
    if isinstance(data, dict) and "value" in data and not isinstance(data.get("fields"), list):
        return data["value"]
    return data
