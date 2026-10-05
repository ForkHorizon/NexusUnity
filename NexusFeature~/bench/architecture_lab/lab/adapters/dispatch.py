"""Resolve a model-visible tool call onto one Unity backend call."""

from __future__ import annotations

import json
from typing import Any, Callable

from lab.adapters.normalize import apply_normalizer
from lab.catalog.extract import capability_index

Backend = Callable[[str, dict[str, Any]], dict[str, Any]]


def dispatch(surface: dict[str, Any], catalog: dict[str, Any], name: str, arguments: dict[str, Any] | None, backend: Backend) -> dict[str, Any]:
    arguments = arguments or {}
    if name == "lab_list_domains":
        domains: dict[str, list[str]] = {}
        for item in surface["lazy"]:
            domains.setdefault(item["domain"], []).append(item["exposed_name"])
        return _meta(name, {"domains": domains}, discovery=True)
    if name == "lab_describe":
        return _meta(name, _describe_one(surface, str(arguments.get("name") or "")), discovery=True)
    if name == "lab_describe_domain":
        domain = str(arguments.get("domain") or "")
        matches = [item for item in surface["lazy"] if item["domain"] == domain]
        return _meta(name, {"domain": domain, "capabilities": [_public_lazy(item) for item in matches]}, discovery=True)
    if name == "lab_list_tags":
        tags: dict[str, int] = {}
        for item in surface["lazy"]:
            for tag in item.get("tags") or ["untagged"]:
                tags[tag] = tags.get(tag, 0) + 1
        return _meta(name, {"tags": [{"tag": key, "count": tags[key]} for key in sorted(tags)]}, discovery=True)
    if name == "lab_commands_for_tag":
        tag = str(arguments.get("tag") or "")
        matches = [item for item in surface["lazy"] if tag in (item.get("tags") or [])]
        return _meta(name, {"tag": tag, "commands": [_public_lazy(item) for item in matches]}, discovery=True)
    if name == "lab_invoke":
        target = str(arguments.get("name") or "")
        inner = arguments.get("arguments") or {}
        if not isinstance(inner, dict):
            return _error(name, "arguments must be an object", discovery=True)
        found = _lazy_by_name(surface, target)
        if found is None:
            return _error(name, f"{target} is not a lazy capability on this surface", discovery=True)
        return _execute(found["backend_name"], inner, found, backend, exposed_name=name, discovery=True)

    resolved = surface["resolution"].get(name)
    if resolved is None:
        return _error(name, f"{name} is not on this surface")
    if resolved["kind"] == "typed":
        return _execute(resolved["backend_name"], arguments, resolved, backend, exposed_name=name, catalog=catalog)
    if resolved["kind"] == "small_manager":
        action = str(arguments.get("action") or "")
        member = resolved["members"].get(action)
        if member is None:
            return _error(name, f"unknown action {action}")
        payload = {key: value for key, value in arguments.items() if key != "action"}
        return _execute(member["backend_name"], payload, member, backend, exposed_name=name, catalog=catalog)
    return _error(name, "unsupported resolution")


def _execute(command: str, arguments: dict[str, Any], resolved: dict[str, Any], backend: Backend, *, exposed_name: str, catalog: dict[str, Any] | None = None, discovery: bool = False) -> dict[str, Any]:
    mode = resolved.get("backend_mode") or "PASSTHROUGH"
    if mode == "COMPOSITE":
        composite_id = resolved.get("composite_id")
        steps = (((catalog or {}).get("composites") or {}).get(composite_id) or {}).get("steps")
        if not steps:
            return _error(exposed_name, "composite has no fixed steps")
        parts = [backend(step, arguments if index == 0 else {}) for index, step in enumerate(steps)]
        raw = {"composite_id": composite_id, "steps": parts, "backend_commands": steps}
        text = apply_normalizer(resolved.get("normalizer") or "raw", raw)
        ok = all(part.get("ok") for part in parts)
        return {
            "ok": ok,
            "discovery": discovery,
            "exposed_name": exposed_name,
            "capability_id": resolved.get("capability_id"),
            "backend_command": ",".join(steps),
            "text": text,
            "raw": raw,
            "error": None if ok else "composite step failed",
        }
    raw_result = backend(command, arguments)
    text = apply_normalizer(resolved.get("normalizer") or "raw", raw_result)
    return {
        "ok": bool(raw_result.get("ok")),
        "discovery": discovery,
        "exposed_name": exposed_name,
        "capability_id": resolved.get("capability_id"),
        "backend_command": command,
        "text": text,
        "raw": raw_result,
        "error": None if raw_result.get("ok") else "backend error",
    }


def _describe_one(surface: dict[str, Any], name: str) -> dict[str, Any]:
    found = _lazy_by_name(surface, name)
    if found is None:
        return {"error": f"{name} is not lazy on this surface"}
    return _public_lazy(found)


def _lazy_by_name(surface: dict[str, Any], name: str) -> dict[str, Any] | None:
    for item in surface["lazy"]:
        if item["exposed_name"] == name or item["capability_id"] == name:
            # Hidden-gateway lazy tools are called by wrapper. Accepting the
            # official id here would leak a second name. Prefer the wrapper.
            if item["exposed_name"] != item["capability_id"] and name == item["capability_id"]:
                return None
            return item
    return None


def _public_lazy(item: dict[str, Any]) -> dict[str, Any]:
    return {
        "name": item["exposed_name"],
        "description": item.get("description") or "",
        "inputSchema": item["input_schema"],
        "domain": item["domain"],
        "tags": item.get("tags") or [],
    }


def _meta(name: str, payload: dict[str, Any], *, discovery: bool) -> dict[str, Any]:
    text = json.dumps(payload, sort_keys=True)
    return {
        "ok": True,
        "discovery": discovery,
        "exposed_name": name,
        "capability_id": None,
        "backend_command": None,
        "text": text,
        "raw": payload,
        "error": None,
    }


def _error(name: str, message: str, *, discovery: bool = False) -> dict[str, Any]:
    return {
        "ok": False,
        "discovery": discovery,
        "exposed_name": name,
        "capability_id": None,
        "backend_command": None,
        "text": json.dumps({"error": message}),
        "raw": {"error": message},
        "error": message,
    }


def capability_ids_from_catalog(catalog: dict[str, Any]) -> dict[str, dict[str, Any]]:
    return capability_index(catalog)
