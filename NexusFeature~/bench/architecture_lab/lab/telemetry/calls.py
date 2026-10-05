"""Classify model tool calls against a generated surface."""

from __future__ import annotations

from typing import Any

DISCOVERY_NAMES = {
    "lab_list_domains",
    "lab_describe",
    "lab_describe_domain",
    "lab_invoke",
    "lab_list_tags",
    "lab_commands_for_tag",
}


def classify_call(surface: dict[str, Any], call: dict[str, Any]) -> dict[str, Any]:
    name = call.get("exposed_name") or ""
    if name == "lab_invoke":
        target = str((call.get("arguments") or {}).get("name") or "")
        for item in surface.get("lazy") or []:
            if item.get("exposed_name") == target:
                return {
                    "kind": "tool",
                    "capability_id": item.get("capability_id"),
                    "backend_command": item.get("backend_name"),
                    "exposed_name": name,
                }
        resolved = (surface.get("resolution") or {}).get(target) or {}
        if resolved.get("kind") == "typed":
            return {
                "kind": "tool",
                "capability_id": resolved.get("capability_id"),
                "backend_command": resolved.get("backend_name"),
                "exposed_name": name,
            }
        return {"kind": "tool", "capability_id": None, "backend_command": None, "exposed_name": name}
    if name in DISCOVERY_NAMES:
        return {"kind": "discovery", "capability_id": None, "backend_command": None, "exposed_name": name}
    resolved = (surface.get("resolution") or {}).get(name)
    if not resolved:
        return {"kind": "unknown", "capability_id": None, "backend_command": None, "exposed_name": name}
    if resolved["kind"] == "small_manager":
        action = str((call.get("arguments") or {}).get("action") or "")
        member = (resolved.get("members") or {}).get(action) or {}
        return {
            "kind": "tool",
            "capability_id": member.get("capability_id"),
            "backend_command": member.get("backend_name"),
            "exposed_name": name,
        }
    return {
        "kind": "tool",
        "capability_id": resolved.get("capability_id"),
        "backend_command": resolved.get("backend_name"),
        "exposed_name": name,
    }


def summarize_calls(surface: dict[str, Any], calls: list[dict[str, Any]], acceptable: set[str]) -> dict[str, int]:
    discovery = 0
    wrong = 0
    tools = 0
    for call in calls:
        classified = classify_call(surface, call)
        if classified["kind"] == "discovery":
            # List and describe do not reach Unity. They are still model calls.
            # Leaving them out of tool_calls lets a chatty arm look cheaper.
            discovery += 1
            tools += 1
            continue
        if classified["exposed_name"] == "lab_invoke":
            discovery += 1
            tools += 1
            capability = classified.get("capability_id")
            if capability not in acceptable:
                wrong += 1
            continue
        tools += 1
        capability = classified["capability_id"]
        if capability not in acceptable:
            wrong += 1
    return {"discovery_calls": discovery, "tool_calls": tools, "wrong_tool_calls": wrong}
