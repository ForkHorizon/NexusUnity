"""Surface mutations. They do not rewrite backend algorithms."""

from __future__ import annotations

import json
import random
from typing import Any

from lab.candidates.manifest import normalize_candidate
from lab.catalog.extract import capability_index
from lab.errors import CandidateInvalid

_EXPOSURE_CYCLE = ("ALWAYS_VISIBLE", "LAZY", "HIDDEN_INTERNAL")


def mutate(
    candidate: dict[str, Any],
    catalog: dict[str, Any],
    seed: int,
) -> dict[str, Any] | None:
    """Return one valid child, or None when every operator is rejected."""
    rng = random.Random(seed)
    operators = [
        _flip_exposure,
        _move_core,
        _change_normalizer,
        _change_backend_mode,
        _reassign_domain,
        _group_pair,
        _split_group,
        _change_strategy,
    ]
    order = list(range(len(operators)))
    rng.shuffle(order)
    index = capability_index(catalog)
    mutable = [
        cap_id
        for cap_id, entry in candidate["architecture"]["capabilities"].items()
        if entry["exposure"] != "DISABLED"
    ]
    if not mutable:
        mutable = list(candidate["architecture"]["capabilities"])
    for slot in order:
        cap_id = mutable[rng.randrange(len(mutable))]
        try:
            child = operators[slot](candidate, catalog, index, cap_id, rng)
        except CandidateInvalid:
            continue
        if child is None:
            continue
        if child["architecture_hash"] == candidate["architecture_hash"]:
            continue
        if _same_executable_surface(candidate, child, catalog):
            continue
        child["parent_id"] = candidate["candidate_id"]
        child["parent_hash"] = candidate["architecture_hash"]
        child["generation"] = int(candidate.get("generation") or 0) + 1
        child["candidate_id"] = f"gen{child['generation']}_{child['architecture_hash'][:8]}"
        return child
    return None


def _raw_from(candidate: dict[str, Any]) -> dict[str, Any]:
    architecture = candidate["architecture"]
    return {
        "surface_strategy": architecture["surface_strategy"],
        "discovery_mechanism": architecture["discovery_mechanism"],
        "output_normalization": architecture["output_normalization"],
        "domains": {key: dict(value) for key, value in architecture["domains"].items()},
        "core_always_visible": list(architecture["core_always_visible"]),
        "capabilities": {
            cap_id: dict(entry)
            for cap_id, entry in architecture["capabilities"].items()
            if entry["exposure"] != "DISABLED" or entry.get("group") or entry.get("wrapper")
        },
    }


def _finish(raw: dict[str, Any], catalog: dict[str, Any], why: str) -> dict[str, Any]:
    raw["why"] = why
    # Drop disabled leftovers so normalize treats true omissions as disabled,
    # while explicit DISABLED rows that the operator wrote are kept.
    return normalize_candidate(raw, catalog)


def _flip_exposure(candidate, catalog, index, cap_id, rng) -> dict[str, Any] | None:
    raw = _raw_from(candidate)
    entry = raw["capabilities"].setdefault(cap_id, {})
    current = entry.get("exposure") or candidate["architecture"]["capabilities"][cap_id]["exposure"]
    if current == "DISABLED":
        entry["exposure"] = "LAZY"
        entry["backend_mode"] = "PASSTHROUGH"
        entry["backend_name"] = index[cap_id]["backend_name"]
        entry["normalizer"] = entry.get("normalizer") or "raw"
        entry["domain"] = entry.get("domain") or index[cap_id]["domain_suggestion"]
        if raw["discovery_mechanism"] == "none":
            raw["discovery_mechanism"] = "describe_invoke"
            if raw["surface_strategy"] in {"atomic", "hidden_gateway"}:
                raw["surface_strategy"] = "hybrid"
        why = f"enable {cap_id} as LAZY"
    else:
        nxt = _EXPOSURE_CYCLE[(_EXPOSURE_CYCLE.index(current) + 1) % len(_EXPOSURE_CYCLE)]
        entry["exposure"] = nxt
        if nxt != "ALWAYS_VISIBLE":
            raw["core_always_visible"] = [
                name for name in raw["core_always_visible"] if name not in {_visible_name(cap_id, entry)}
            ]
        if nxt == "LAZY" and raw["discovery_mechanism"] == "none":
            raw["discovery_mechanism"] = "describe_invoke"
            if raw["surface_strategy"] == "atomic":
                raw["surface_strategy"] = "hybrid"
        why = f"{cap_id} exposure {current} -> {nxt}"
    _sync_hidden_wrapper(raw, cap_id, index)
    return _finish(raw, catalog, why)


def _visible_name(cap_id: str, entry: dict[str, Any]) -> str:
    return entry.get("wrapper") or cap_id


def _sync_hidden_wrapper(raw: dict[str, Any], cap_id: str, index) -> None:
    if raw["surface_strategy"] != "hidden_gateway":
        return
    entry = raw["capabilities"][cap_id]
    if index[cap_id]["origin"] == "official_unity" and entry["exposure"] in {"ALWAYS_VISIBLE", "LAZY"}:
        if not entry.get("wrapper") or entry["wrapper"] == entry.get("backend_name"):
            entry["wrapper"] = f"nexus_{cap_id}"


def _move_core(candidate, catalog, index, cap_id, rng) -> dict[str, Any] | None:
    raw = _raw_from(candidate)
    entry = dict(candidate["architecture"]["capabilities"][cap_id])
    raw["capabilities"][cap_id] = entry
    if entry["exposure"] != "ALWAYS_VISIBLE":
        return None
    name = _visible_name(cap_id, entry)
    core = set(raw["core_always_visible"])
    if name in core and len(core) > 1:
        core.remove(name)
        why = f"remove {name} from always-visible core"
    else:
        core.add(name)
        why = f"add {name} to always-visible core"
    raw["core_always_visible"] = sorted(core)
    return _finish(raw, catalog, why)


def _change_normalizer(candidate, catalog, index, cap_id, rng) -> dict[str, Any] | None:
    raw = _raw_from(candidate)
    entry = dict(candidate["architecture"]["capabilities"][cap_id])
    allowed = [item for item in index[cap_id]["allowed_normalizers"] if item != entry["normalizer"]]
    if not allowed:
        return None
    entry["normalizer"] = allowed[rng.randrange(len(allowed))]
    raw["capabilities"][cap_id] = entry
    return _finish(raw, catalog, f"{cap_id} normalizer -> {entry['normalizer']}")


def _change_backend_mode(candidate, catalog, index, cap_id, rng) -> dict[str, Any] | None:
    raw = _raw_from(candidate)
    entry = dict(candidate["architecture"]["capabilities"][cap_id])
    allowed = [item for item in index[cap_id]["allowed_backend_modes"] if item != entry["backend_mode"]]
    if not allowed:
        return None
    entry["backend_mode"] = allowed[rng.randrange(len(allowed))]
    # Backend command id stays. Only the labeled processing mode changes,
    # and only when the catalog already allows that fixed mode.
    entry["backend_name"] = index[cap_id]["backend_name"]
    raw["capabilities"][cap_id] = entry
    return _finish(raw, catalog, f"{cap_id} backend_mode -> {entry['backend_mode']}")


def _reassign_domain(candidate, catalog, index, cap_id, rng) -> dict[str, Any] | None:
    raw = _raw_from(candidate)
    entry = dict(candidate["architecture"]["capabilities"][cap_id])
    suggestion = index[cap_id]["domain_suggestion"]
    entry["domain"] = f"{suggestion}_split" if not str(entry["domain"]).endswith("_split") else suggestion
    raw["capabilities"][cap_id] = entry
    raw["domains"][entry["domain"]] = {"mode": entry["exposure"]}
    return _finish(raw, catalog, f"{cap_id} domain -> {entry['domain']}")


def _group_pair(candidate, catalog, index, cap_id, rng) -> dict[str, Any] | None:
    raw = _raw_from(candidate)
    entry = dict(candidate["architecture"]["capabilities"][cap_id])
    if entry["exposure"] != "ALWAYS_VISIBLE":
        return None
    partner = _partner(candidate, cap_id, entry["domain"])
    if partner is None:
        return None
    group = f"grp_{entry['domain']}"
    counts = _group_counts(raw, candidate)
    if counts.get(group, 0) + (0 if entry.get("group") == group else 1) > 4:
        return None
    entry["group"] = group
    other = dict(candidate["architecture"]["capabilities"][partner])
    other["group"] = group
    raw["capabilities"][cap_id] = entry
    raw["capabilities"][partner] = other
    raw["surface_strategy"] = "small_managers"
    raw["discovery_mechanism"] = raw["discovery_mechanism"] if raw["discovery_mechanism"] != "none" else "none"
    # Individual wrappers collide with nothing because the exposed tool is the group.
    return _finish(raw, catalog, f"group {cap_id} with {partner} as {group}")


def _split_group(candidate, catalog, index, cap_id, rng) -> dict[str, Any] | None:
    entry = candidate["architecture"]["capabilities"][cap_id]
    if not entry.get("group"):
        return None
    raw = _raw_from(candidate)
    updated = dict(entry)
    group = updated["group"]
    updated["group"] = None
    raw["capabilities"][cap_id] = updated
    return _finish(raw, catalog, f"split {cap_id} out of {group}")


def _change_strategy(candidate, catalog, index, cap_id, rng) -> dict[str, Any] | None:
    raw = _raw_from(candidate)
    current = raw["surface_strategy"]
    if current == "atomic":
        raw["surface_strategy"] = "hybrid"
        raw["discovery_mechanism"] = "describe_invoke"
        why = "atomic -> hybrid describe_invoke"
    elif current == "hybrid":
        raw["surface_strategy"] = "unity_tags"
        raw["discovery_mechanism"] = "unity_tags"
        why = "hybrid -> unity_tags"
    elif current == "hidden_gateway":
        raw["surface_strategy"] = "hybrid"
        raw["discovery_mechanism"] = "describe_invoke"
        why = "hidden_gateway -> hybrid"
    else:
        raw["surface_strategy"] = "atomic"
        # Atomic with lazy tools and no discovery makes those tools unreachable.
        # Drop lazy tools to disabled so the child stays a legal atomic surface.
        for item_id, item in list(raw["capabilities"].items()):
            exposure = item.get("exposure")
            if exposure is None:
                exposure = candidate["architecture"]["capabilities"][item_id]["exposure"]
            if exposure == "LAZY":
                item["exposure"] = "DISABLED"
        raw["discovery_mechanism"] = "none"
        why = f"{current} -> atomic"
    return _finish(raw, catalog, why)


def _partner(candidate: dict[str, Any], cap_id: str, domain: str) -> str | None:
    for other_id, entry in sorted(candidate["architecture"]["capabilities"].items()):
        if other_id == cap_id:
            continue
        if entry["exposure"] != "ALWAYS_VISIBLE":
            continue
        if entry["domain"] != domain:
            continue
        return other_id
    return None


def _same_executable_surface(parent: dict[str, Any], child: dict[str, Any], catalog: dict[str, Any]) -> bool:
    """True when the model-visible tools and backends did not change.

    Core-list and domain edits that the generator ignores must not become
    a new measured architecture.
    """
    from lab.surfaces.generate import generate_surface

    try:
        left = generate_surface(parent, catalog)
        right = generate_surface(child, catalog)
    except (CandidateInvalid, Exception):
        return False
    return _surface_key(left) == _surface_key(right)


def _surface_key(surface: dict[str, Any]) -> str:
    lazy = [
        (item.get("exposed_name"), item.get("backend_name"), item.get("normalizer"), item.get("domain"))
        for item in surface.get("lazy") or []
    ]
    return json.dumps(
        {
            "discovery": surface.get("discovery_mechanism"),
            "names": surface.get("model_visible_names"),
            "resolution": surface.get("resolution"),
            "lazy": lazy,
        },
        sort_keys=True,
        default=str,
    )


def _group_counts(raw: dict[str, Any], candidate: dict[str, Any]) -> dict[str, int]:
    counts: dict[str, int] = {}
    for cap_id, entry in candidate["architecture"]["capabilities"].items():
        group = raw["capabilities"].get(cap_id, entry).get("group") or entry.get("group")
        exposure = raw["capabilities"].get(cap_id, entry).get("exposure") or entry["exposure"]
        if group and exposure != "DISABLED":
            counts[group] = counts.get(group, 0) + 1
    return counts
