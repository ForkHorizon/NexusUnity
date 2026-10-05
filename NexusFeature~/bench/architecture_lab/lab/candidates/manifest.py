"""Candidate manifests, canonical hashes, and coverage.

The hash ignores names, parents, generation, and prose. It covers the
model-facing architecture and nothing else.
"""

from __future__ import annotations

from typing import Any

from lab.canonical import canonical_dumps, sha256_json, sort_strings
from lab.catalog.extract import capability_index
from lab.errors import CandidateInvalid, UnsupportedMechanism

EXPOSURES = {"ALWAYS_VISIBLE", "LAZY", "HIDDEN_INTERNAL", "DISABLED"}
BACKENDS = {"PASSTHROUGH", "ENHANCED", "COMPOSITE"}
STRATEGIES = {
    "atomic",
    "small_managers",
    "domain_discovery",
    "describe_invoke",
    "unity_tags",
    "hidden_gateway",
    "hybrid",
}
DISCOVERY = {"none", "describe_invoke", "unity_tags", "domain_discovery"}
REJECTED_DISCOVERY = {"dynamic_tool_list", "deferred_retrieval"}
NORMALIZERS = {"raw", "truncate_4k", "drop_base64", "scalar_value"}
MAX_GROUP = 4

# Mechanisms this Codex build does not provide. Surface generation must fail.
CLIENT_UNSUPPORTED = {
    "dynamic_tool_list": "Codex CLI 0.160 has deferred_executor and executor_capability_discovery disabled",
    "deferred_retrieval": "Codex CLI 0.160 has deferred_tool_world_state disabled",
}


def _entry(
    *,
    exposure: str,
    backend_mode: str,
    backend_name: str,
    wrapper: str | None,
    group: str | None,
    normalizer: str,
    domain: str,
) -> dict[str, Any]:
    return {
        "exposure": exposure,
        "backend_mode": backend_mode,
        "backend_name": backend_name,
        "wrapper": wrapper,
        "group": group,
        "normalizer": normalizer,
        "domain": domain,
    }


def blank_entry(capability: dict[str, Any], exposure: str) -> dict[str, Any]:
    return _entry(
        exposure=exposure,
        backend_mode="PASSTHROUGH",
        backend_name=capability["backend_name"],
        wrapper=None,
        group=None,
        normalizer="raw",
        domain=capability["domain_suggestion"],
    )


def normalize_candidate(raw: dict[str, Any], catalog: dict[str, Any]) -> dict[str, Any]:
    """Fill defaults, sort unordered fields, and reject illegal manifests."""
    index = capability_index(catalog)
    strategy = raw.get("surface_strategy")
    discovery = raw.get("discovery_mechanism")
    if strategy not in STRATEGIES:
        raise CandidateInvalid(f"unknown surface_strategy {strategy}")
    if discovery in REJECTED_DISCOVERY:
        raise UnsupportedMechanism(
            f"{discovery} is not supported ({CLIENT_UNSUPPORTED[discovery]}). "
            "Refusing to substitute another mechanism."
        )
    if discovery not in DISCOVERY:
        raise CandidateInvalid(f"unknown discovery_mechanism {discovery}")
    if strategy == "hybrid" and discovery == "none":
        raise CandidateInvalid("hybrid requires a discovery mechanism for lazy tools")
    if strategy == "unity_tags" and discovery != "unity_tags":
        raise CandidateInvalid("unity_tags strategy requires discovery_mechanism unity_tags")
    if strategy == "describe_invoke" and discovery != "describe_invoke":
        raise CandidateInvalid("describe_invoke strategy requires that discovery mechanism")
    if strategy == "domain_discovery" and discovery != "domain_discovery":
        raise CandidateInvalid("domain_discovery strategy requires that discovery mechanism")

    normalization = raw.get("output_normalization", "raw")
    if normalization not in NORMALIZERS:
        raise CandidateInvalid(f"unknown output_normalization {normalization}")

    declared = raw.get("capabilities") or {}
    if not isinstance(declared, dict):
        raise CandidateInvalid("capabilities must be an object keyed by catalog id")

    unknown = sorted(set(declared) - set(index))
    if unknown:
        raise CandidateInvalid(f"unknown capabilities: {unknown}")

    # Omitted catalog ids are DISABLED. Silence is not implicit access.
    normalized_caps: dict[str, Any] = {}
    for cap_id in sorted(index):
        capability = index[cap_id]
        incoming = declared.get(cap_id) or {}
        entry = blank_entry(capability, "DISABLED")
        entry.update({k: incoming[k] for k in entry if k in incoming})
        entry["backend_name"] = capability["backend_name"]
        if entry["exposure"] not in EXPOSURES:
            raise CandidateInvalid(f"{cap_id} exposure {entry['exposure']}")
        if entry["backend_mode"] not in BACKENDS:
            raise CandidateInvalid(f"{cap_id} backend {entry['backend_mode']}")
        if entry["backend_mode"] not in capability["allowed_backend_modes"]:
            raise CandidateInvalid(
                f"{cap_id} backend {entry['backend_mode']} is not allowed for this capability"
            )
        if entry["backend_mode"] == "COMPOSITE" and not capability.get("composite_id"):
            raise CandidateInvalid(f"{cap_id} has no fixed composite")
        if entry["normalizer"] not in capability["allowed_normalizers"]:
            raise CandidateInvalid(f"{cap_id} normalizer {entry['normalizer']}")
        if incoming.get("parameter_union") == "flat":
            raise CandidateInvalid("flat parameter unions are rejected")
        if entry["wrapper"] is not None and not isinstance(entry["wrapper"], str):
            raise CandidateInvalid(f"{cap_id} wrapper must be a string or null")
        if strategy == "hidden_gateway" and entry["exposure"] == "ALWAYS_VISIBLE":
            if capability["origin"] == "official_unity":
                if not entry["wrapper"] or entry["wrapper"] == entry["backend_name"]:
                    raise CandidateInvalid(
                        f"hidden gateway must not expose official name {cap_id}"
                    )
        normalized_caps[cap_id] = entry

    _reject_duplicate_names(normalized_caps)
    _reject_large_groups(normalized_caps, strategy)

    domains = {}
    for name, spec in sorted((raw.get("domains") or {}).items()):
        mode = spec.get("mode") if isinstance(spec, dict) else spec
        if mode not in EXPOSURES:
            raise CandidateInvalid(f"domain {name} mode {mode}")
        domains[name] = {"mode": mode}

    core = sort_strings(list(raw.get("core_always_visible") or []))
    exposed = exposed_names(normalized_caps)
    missing_core = [name for name in core if name not in exposed]
    if missing_core:
        raise CandidateInvalid(f"core names are not always visible: {missing_core}")

    architecture = {
        "schema_version": 1,
        "surface_strategy": strategy,
        "discovery_mechanism": discovery,
        "output_normalization": normalization,
        "domains": domains,
        "core_always_visible": core,
        "capabilities": normalized_caps,
    }
    return {
        "candidate_id": raw.get("candidate_id") or "unnamed",
        "parent_id": raw.get("parent_id"),
        "parent_hash": raw.get("parent_hash"),
        "generation": int(raw.get("generation") or 0),
        "why": raw.get("why") or "",
        "architecture_hash": sha256_json(architecture),
        "catalog_hash": catalog["catalog_hash"],
        "architecture": architecture,
    }


def exposed_names(capabilities: dict[str, Any]) -> set[str]:
    names = set()
    for cap_id, entry in capabilities.items():
        if entry["exposure"] != "ALWAYS_VISIBLE":
            continue
        names.add(entry["wrapper"] or cap_id)
    return names


def _reject_duplicate_names(capabilities: dict[str, Any]) -> None:
    seen: dict[str, str] = {}
    for cap_id, entry in capabilities.items():
        if entry["exposure"] == "DISABLED":
            continue
        name = entry["wrapper"] or cap_id
        if entry["exposure"] == "HIDDEN_INTERNAL":
            continue
        if name in seen:
            raise CandidateInvalid(f"duplicate model-visible name {name}")
        seen[name] = cap_id


def _reject_large_groups(capabilities: dict[str, Any], strategy: str) -> None:
    if strategy != "small_managers":
        return
    counts: dict[str, int] = {}
    for entry in capabilities.values():
        if entry["exposure"] == "DISABLED" or not entry["group"]:
            continue
        counts[entry["group"]] = counts.get(entry["group"], 0) + 1
    too_big = sorted(name for name, count in counts.items() if count > MAX_GROUP)
    if too_big:
        raise CandidateInvalid(f"groups exceed {MAX_GROUP}: {too_big}")


def reachable_ids(candidate: dict[str, Any]) -> set[str]:
    """Capabilities the model can eventually call. Hidden and disabled are not."""
    discovery = candidate["architecture"]["discovery_mechanism"]
    lazy_ok = discovery in {"describe_invoke", "unity_tags", "domain_discovery"}
    reached = set()
    for cap_id, entry in candidate["architecture"]["capabilities"].items():
        if entry["exposure"] == "ALWAYS_VISIBLE":
            reached.add(cap_id)
        elif entry["exposure"] == "LAZY" and lazy_ok:
            reached.add(cap_id)
    return reached


def coverage_report(candidate: dict[str, Any], tasks: list[dict[str, Any]], catalog: dict[str, Any]) -> dict[str, Any]:
    """Each scored task must be able to reach one of its own acceptable capabilities.

    A class stays covered when any sibling in the class map is reachable. That
    lets a candidate delete the tool the task actually accepts and still look
    complete. The task's acceptable list is the requirement.
    """
    class_map = catalog["class_map"]
    reached = reachable_ids(candidate)
    required = sorted({task["required_capability_class"] for task in tasks})
    missing = []
    for task in tasks:
        options = list(task.get("acceptable_capabilities") or [])
        if not options:
            options = list(class_map.get(task["required_capability_class"]) or [])
        if not any(option in reached for option in options):
            missing.append(task["task_id"])
    ratio = 1.0 if not tasks else (len(tasks) - len(missing)) / len(tasks)
    return {
        "required_classes": required,
        "missing_classes": missing,
        "coverage": ratio,
        "reachable_count": len(reached),
    }


def architecture_hash(candidate: dict[str, Any]) -> str:
    return candidate["architecture_hash"]


def same_architecture(left: dict[str, Any], right: dict[str, Any]) -> bool:
    return left["architecture_hash"] == right["architecture_hash"]


def canonical_architecture_text(candidate: dict[str, Any]) -> str:
    return canonical_dumps(candidate["architecture"])
