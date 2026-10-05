"""Read the lab scene back from Unity. The model's text is not an input."""

from __future__ import annotations

import re
from typing import Any

from lab.fixtures.reference import editor_status, find_marker, open_lab, read_field

_CS = re.compile(r"\bCS\d{4}\b")
_GUID = re.compile(r"\b[0-9a-f]{32}\b")


def observe_state(
    backend,
    *,
    include_assets: bool = False,
    reload_scene: bool = True,
    asset_name: str | None = None,
    text_path: str | None = None,
    want_other: bool = False,
) -> dict[str, Any]:
    """One consistent read of the fields the benchmark tasks name.

    reload_scene=False reads the scene that is already open. open_scene reloads
    the file from disk and drops unsaved serialized edits, so a write oracle
    must not reopen before it reads.
    """
    opened = open_lab(backend) if reload_scene else {"ok": True}
    marker = find_marker(backend)
    name = marker.get("object_name") if marker else None
    status = editor_status(backend)
    observation: dict[str, Any] = {
        "open_ok": bool(opened.get("ok")) if isinstance(opened, dict) else False,
        "object_name": name,
        "intensity": _number(read_field(backend, name, "intensity")) if name else None,
        "revision": _whole(read_field(backend, name, "revision")) if name else None,
        "marker_name": read_field(backend, name, "markerName") if name else None,
        "play_mode": status.get("playMode") or status.get("play_mode"),
        "editor_status": status.get("status"),
        "compiling": bool(status.get("compiling")),
        "project_path": status.get("projectPath"),
        "unity_version": status.get("unityVersion"),
        "port": status.get("port"),
    }
    if include_assets or asset_name or text_path:
        observation["compilation_failed"] = _compilation_failed(backend("console_status", {}))
        observation["guid"] = _guid_from_assets(backend, asset_name or "readme")
        observation["error_code"] = extract_cs(backend("recompile_status", {}))
    if text_path:
        observation["file_text"] = _file_text(backend, text_path)
    if want_other:
        observation["other_object_name"] = _other_root(backend, name)
    return observation


def pristine_match(observation: dict[str, Any]) -> list[str]:
    """Differences from the built Lab fixture. Empty means the live scene matches."""
    problems = []
    if observation.get("object_name") != "LabReadTarget":
        problems.append("object_name")
    if not _close(observation.get("intensity"), 1.25):
        problems.append("intensity")
    if observation.get("revision") != 7:
        problems.append("revision")
    if observation.get("marker_name") != "readable":
        problems.append("marker_name")
    if observation.get("play_mode") != "stopped":
        problems.append("play_mode")
    if observation.get("editor_status") != "ready":
        problems.append("editor_status")
    if observation.get("compiling"):
        problems.append("compiling")
    return problems


def extract_cs(payload: Any) -> str | None:
    found = _CS.findall(_flatten(payload))
    return found[0] if found else None


_COMPILE_FLAGS = ("compilationFailed", "compilation_failed", "hasCompileErrors", "hasErrors")


def _compilation_failed(result: dict[str, Any]) -> bool | None:
    """Read the compiler flag. Pipeline 0.8 puts it on groundTruth, not the root."""
    return _find_bool(_body(result), _COMPILE_FLAGS)


def _find_bool(node: Any, keys: tuple[str, ...]) -> bool | None:
    if not isinstance(node, dict):
        return None
    for key in keys:
        if key in node and isinstance(node[key], bool):
            return node[key]
    for value in node.values():
        if isinstance(value, dict):
            found = _find_bool(value, keys)
            if found is not None:
                return found
    return None


def _command_result(result: dict[str, Any]) -> Any:
    from lab.adapters.unity_cli import command_body

    payload = result.get("payload") if isinstance(result, dict) else None
    return command_body(payload)


def _guid_from_assets(backend, name: str = "readme") -> str | None:
    result = backend(
        "find_assets",
        {"name": name, "search_in": "Assets/LabFixture/Notes", "limit": 20},
    )
    data = _command_result(result)
    assets = data.get("assets") if isinstance(data, dict) else None
    if isinstance(assets, list):
        for asset in assets:
            if not isinstance(asset, dict):
                continue
            path = str(asset.get("assetPath") or "")
            if name in path and isinstance(asset.get("guid"), str):
                return asset["guid"]
    text = _flatten(result)
    match = _GUID.search(text)
    return match.group(0) if match else None


def _file_text(backend, path: str) -> str | None:
    result = backend("read_text_file", {"path": path})
    data = _command_result(result)
    if isinstance(data, dict) and isinstance(data.get("contents"), str):
        return data["contents"].strip()
    return None


def _other_root(backend, marker_name: str | None) -> str | None:
    result = backend("find_gameobjects", {"include_inactive": True})
    data = _command_result(result)
    objects = data.get("gameObjects") if isinstance(data, dict) else None
    if not isinstance(objects, list):
        return None
    names = []
    for item in objects:
        if not isinstance(item, dict):
            continue
        path = str(item.get("hierarchyPath") or "")
        leaf = path.rstrip("/").split("/")[-1]
        if leaf:
            names.append(leaf)
    others = [leaf for leaf in names if leaf != marker_name]
    if len(others) != 1:
        return None
    return others[0]


def _body(result: dict[str, Any]) -> Any:
    payload = result.get("payload") if isinstance(result, dict) else None
    if isinstance(payload, dict):
        for key in ("data", "result"):
            if isinstance(payload.get(key), dict):
                return payload[key]
        return payload
    return result


def _flatten(value: Any) -> str:
    if isinstance(value, str):
        return value
    if isinstance(value, dict):
        return " ".join(_flatten(item) for item in value.values())
    if isinstance(value, list):
        return " ".join(_flatten(item) for item in value)
    if value is None:
        return ""
    return str(value)


def _number(value: Any) -> Any:
    if isinstance(value, bool) or value is None:
        return value
    if isinstance(value, (int, float)):
        return float(value)
    if isinstance(value, str):
        try:
            return float(value)
        except ValueError:
            return value
    return value


def _whole(value: Any) -> Any:
    number = _number(value)
    if isinstance(number, float) and number.is_integer():
        return int(number)
    if isinstance(number, int):
        return number
    return value


def _close(actual: Any, expected: float) -> bool:
    return isinstance(actual, (int, float)) and not isinstance(actual, bool) and abs(float(actual) - expected) <= 1e-3
