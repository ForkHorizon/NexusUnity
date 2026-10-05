"""Turn a candidate manifest into a model-visible tool surface.

Unsupported client mechanisms raise. They are never rewritten into a
different strategy.
"""

from __future__ import annotations

from typing import Any

from lab.candidates.manifest import CLIENT_UNSUPPORTED
from lab.catalog.extract import capability_index
from lab.errors import CandidateInvalid, UnsupportedMechanism

DISCOVERY_TOOLS = {
    "lab_list_domains",
    "lab_describe",
    "lab_invoke",
    "lab_list_tags",
    "lab_commands_for_tag",
    "lab_describe_domain",
}


def generate_surface(candidate: dict[str, Any], catalog: dict[str, Any]) -> dict[str, Any]:
    architecture = candidate["architecture"]
    discovery = architecture["discovery_mechanism"]
    if discovery in CLIENT_UNSUPPORTED:
        raise UnsupportedMechanism(
            f"{discovery} is not supported ({CLIENT_UNSUPPORTED[discovery]}). "
            "The lab will not pretend this candidate was tested."
        )
    strategy = architecture["surface_strategy"]
    index = capability_index(catalog)
    resolution: dict[str, Any] = {}
    tools: list[dict[str, Any]] = []
    lazy: list[dict[str, Any]] = []

    visible = []
    for cap_id, entry in architecture["capabilities"].items():
        if entry["exposure"] == "ALWAYS_VISIBLE":
            visible.append((cap_id, entry))
        elif entry["exposure"] == "LAZY":
            lazy.append(_lazy_record(cap_id, entry, index[cap_id]))

    if strategy == "small_managers":
        tools.extend(_small_manager_tools(visible, index, resolution))
    else:
        for cap_id, entry in visible:
            _add_typed(tools, resolution, cap_id, entry, index[cap_id])

    mechanism = discovery
    if discovery == "describe_invoke":
        tools.extend(_describe_tools())
    elif discovery == "unity_tags":
        tools.extend(_tag_tools())
    elif discovery == "domain_discovery":
        tools.extend(_domain_tools())
    elif discovery != "none":
        raise UnsupportedMechanism(discovery)

    if strategy == "hybrid" and discovery == "none":
        raise CandidateInvalid("hybrid without discovery would hide lazy tools")

    model_names = [tool["name"] for tool in tools]
    if len(model_names) != len(set(model_names)):
        raise CandidateInvalid("surface produced duplicate tool names")

    return {
        "candidate_id": candidate["candidate_id"],
        "candidate_hash": candidate["architecture_hash"],
        "surface_strategy": strategy,
        "discovery_mechanism": discovery,
        "mechanism_actually_used": mechanism,
        "substituted": False,
        "tools": tools,
        "resolution": resolution,
        "lazy": lazy,
        "model_visible_names": model_names,
    }


def _lazy_record(cap_id: str, entry: dict[str, Any], capability: dict[str, Any]) -> dict[str, Any]:
    return {
        "capability_id": cap_id,
        "exposed_name": entry["wrapper"] or cap_id,
        "backend_name": entry["backend_name"],
        "backend_mode": entry["backend_mode"],
        "normalizer": entry["normalizer"],
        "domain": entry["domain"],
        "tags": capability.get("tags") or [],
        "description": capability.get("description") or "",
        "input_schema": _schema(capability),
    }


def _add_typed(tools, resolution, cap_id, entry, capability) -> None:
    name = entry["wrapper"] or cap_id
    tools.append(
        {
            "name": name,
            "description": capability.get("description") or cap_id,
            "inputSchema": _schema(capability),
        }
    )
    resolution[name] = {
        "kind": "typed",
        "capability_id": cap_id,
        "backend_name": entry["backend_name"],
        "backend_mode": entry["backend_mode"],
        "normalizer": entry["normalizer"],
        "composite_id": capability.get("composite_id"),
    }


def _schema(capability: dict[str, Any]) -> dict[str, Any]:
    properties = {}
    required = []
    for param in capability.get("parameters") or []:
        properties[param["name"]] = {
            "type": _json_type(param.get("type")),
            "description": param.get("description") or "",
        }
        if param.get("required"):
            required.append(param["name"])
    return {
        "type": "object",
        "properties": properties,
        "required": required,
        "additionalProperties": False,
    }


def _json_type(raw: str | None) -> str:
    if raw in {"string", "integer", "number", "boolean"}:
        return raw
    return "string"


def _small_manager_tools(visible, index, resolution) -> list[dict[str, Any]]:
    grouped: dict[str, list[tuple[str, dict[str, Any]]]] = {}
    individuals = []
    for cap_id, entry in visible:
        if entry.get("group"):
            grouped.setdefault(entry["group"], []).append((cap_id, entry))
        else:
            individuals.append((cap_id, entry))
    tools: list[dict[str, Any]] = []
    for cap_id, entry in individuals:
        _add_typed(tools, resolution, cap_id, entry, index[cap_id])
    for group, members in sorted(grouped.items()):
        if len(members) > 4:
            raise CandidateInvalid(f"group {group} has {len(members)} capabilities")
        branches = []
        member_map = {}
        for cap_id, entry in members:
            capability = index[cap_id]
            action_name = entry["wrapper"] or cap_id
            schema = _schema(capability)
            branch_required = ["action", *schema["required"]]
            properties = {
                "action": {"const": action_name},
                **schema["properties"],
            }
            branches.append(
                {
                    "type": "object",
                    "properties": properties,
                    "required": branch_required,
                    "additionalProperties": False,
                }
            )
            member_map[action_name] = {
                "capability_id": cap_id,
                "backend_name": entry["backend_name"],
                "backend_mode": entry["backend_mode"],
                "normalizer": entry["normalizer"],
                "composite_id": index[cap_id].get("composite_id"),
            }
        tools.append(
            {
                "name": group,
                "description": f"Small manager for {group}. Pass action and only that action's fields.",
                "inputSchema": {"oneOf": branches},
            }
        )
        resolution[group] = {"kind": "small_manager", "members": member_map}
    return tools


def _describe_tools() -> list[dict[str, Any]]:
    return [
        _discovery(
            "lab_list_domains",
            "List lazy capability domains. This does not execute Unity.",
            {"type": "object", "properties": {}, "additionalProperties": False},
        ),
        _discovery(
            "lab_describe",
            "Return the exact typed schema for one lazy capability name.",
            {
                "type": "object",
                "properties": {"name": {"type": "string"}},
                "required": ["name"],
                "additionalProperties": False,
            },
        ),
        _discovery(
            "lab_invoke",
            "Invoke one lazy capability by the name lab_describe returned.",
            {
                "type": "object",
                "properties": {
                    "name": {"type": "string"},
                    "arguments": {"type": "object"},
                },
                "required": ["name"],
                "additionalProperties": False,
            },
        ),
    ]


def _tag_tools() -> list[dict[str, Any]]:
    return [
        _discovery(
            "lab_list_tags",
            "List Unity command tags and counts. This does not execute a scene command.",
            {"type": "object", "properties": {}, "additionalProperties": False},
        ),
        _discovery(
            "lab_commands_for_tag",
            "List lazy commands that carry one Unity tag.",
            {
                "type": "object",
                "properties": {"tag": {"type": "string"}},
                "required": ["tag"],
                "additionalProperties": False,
            },
        ),
        _discovery(
            "lab_invoke",
            "Invoke one lazy command named by lab_commands_for_tag.",
            {
                "type": "object",
                "properties": {
                    "name": {"type": "string"},
                    "arguments": {"type": "object"},
                },
                "required": ["name"],
                "additionalProperties": False,
            },
        ),
    ]


def _domain_tools() -> list[dict[str, Any]]:
    return [
        _discovery(
            "lab_list_domains",
            "List domain names for lazy capabilities.",
            {"type": "object", "properties": {}, "additionalProperties": False},
        ),
        _discovery(
            "lab_describe_domain",
            "Return capability names and exact schemas for one domain.",
            {
                "type": "object",
                "properties": {"domain": {"type": "string"}},
                "required": ["domain"],
                "additionalProperties": False,
            },
        ),
        _discovery(
            "lab_invoke",
            "Invoke one lazy capability from a domain schema.",
            {
                "type": "object",
                "properties": {
                    "name": {"type": "string"},
                    "arguments": {"type": "object"},
                },
                "required": ["name"],
                "additionalProperties": False,
            },
        ),
    ]


def _discovery(name: str, description: str, schema: dict[str, Any]) -> dict[str, Any]:
    return {"name": name, "description": description, "inputSchema": schema}


def surfaces_differ(left: dict[str, Any], right: dict[str, Any]) -> bool:
    return left["model_visible_names"] != right["model_visible_names"]
