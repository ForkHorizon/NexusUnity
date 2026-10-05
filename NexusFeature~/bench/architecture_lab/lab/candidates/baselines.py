"""Reference candidates. None of these is assumed to win."""

from __future__ import annotations

from typing import Any

from lab.candidates.manifest import normalize_candidate
from lab.catalog.extract import capability_index

# Official commands a curated surface keeps typed. This is a baseline, not a
# decision that these are the right production tools.
CURATED_IDS = [
    "get_scene_hierarchy",
    "find_gameobjects",
    "search",
    "get_selection",
    "get_component_properties",
    "get_serialized_fields",
    "set_serialized_field",
    "save_scene",
    "open_scene",
    "capture_game_view",
    "capture_scene_view",
    "screenshot",
    "recompile",
    "recompile_status",
    "console",
    "console_status",
    "editor_status",
    "wait_for",
    "reload_file",
    "find_assets",
    "read_text_file",
]

NEXUS_IDS = [
    "nexus_project_map",
    "nexus_group_compile_errors",
    "nexus_capture_game_view",
]

SMOKE_IDS = [
    "open_scene",
    "find_gameobjects",
    "get_serialized_fields",
    "set_serialized_field",
    "editor_status",
]


def _present(catalog: dict[str, Any], ids: list[str]) -> list[str]:
    index = capability_index(catalog)
    missing = [item for item in ids if item not in index]
    if missing:
        raise KeyError(f"catalog is missing baseline ids: {missing}")
    return ids


def _manifest(
    catalog: dict[str, Any],
    *,
    candidate_id: str,
    strategy: str,
    discovery: str,
    assign,
    why: str,
    core: list[str] | None = None,
) -> dict[str, Any]:
    index = capability_index(catalog)
    capabilities = {}
    for cap_id, capability in index.items():
        capabilities[cap_id] = assign(cap_id, capability)
    raw = {
        "candidate_id": candidate_id,
        "generation": 0,
        "surface_strategy": strategy,
        "discovery_mechanism": discovery,
        "output_normalization": "raw",
        "capabilities": capabilities,
        "core_always_visible": core or [],
        "why": why,
        "domains": {},
    }
    return normalize_candidate(raw, catalog)


def _passthrough(capability: dict[str, Any], exposure: str, **extra: Any) -> dict[str, Any]:
    entry = {
        "exposure": exposure,
        "backend_mode": "PASSTHROUGH",
        "backend_name": capability["backend_name"],
        "wrapper": extra.get("wrapper"),
        "group": extra.get("group"),
        "normalizer": "raw",
        "domain": capability["domain_suggestion"],
    }
    return entry


def baseline_unity_flat(catalog: dict[str, Any]) -> dict[str, Any]:
    """Every official command as its own typed tool. Nexus commands are off."""

    def assign(cap_id: str, capability: dict[str, Any]) -> dict[str, Any]:
        exposure = "ALWAYS_VISIBLE" if capability["origin"] == "official_unity" else "DISABLED"
        return _passthrough(capability, exposure)

    return _manifest(
        catalog,
        candidate_id="BASELINE_UNITY_FLAT",
        strategy="atomic",
        discovery="none",
        assign=assign,
        why="Official commands exposed directly. Not a claim that flat is optimal.",
    )


def baseline_unity_tags(catalog: dict[str, Any]) -> dict[str, Any]:
    """Official commands are reached through Unity tag discovery, not typed up front."""

    def assign(cap_id: str, capability: dict[str, Any]) -> dict[str, Any]:
        exposure = "LAZY" if capability["origin"] == "official_unity" else "DISABLED"
        return _passthrough(capability, exposure)

    return _manifest(
        catalog,
        candidate_id="BASELINE_UNITY_TAGS",
        strategy="unity_tags",
        discovery="unity_tags",
        assign=assign,
        why="Mirrors current Unity progressive tag discovery.",
    )


def baseline_curated(catalog: dict[str, Any]) -> dict[str, Any]:
    """A small official subset is typed. The rest stay discoverable, not deleted."""
    curated = set(_present(catalog, CURATED_IDS))

    def assign(cap_id: str, capability: dict[str, Any]) -> dict[str, Any]:
        if capability["origin"] != "official_unity":
            return _passthrough(capability, "DISABLED")
        exposure = "ALWAYS_VISIBLE" if cap_id in curated else "LAZY"
        return _passthrough(capability, exposure)

    return _manifest(
        catalog,
        candidate_id="BASELINE_CURATED",
        strategy="hybrid",
        discovery="describe_invoke",
        assign=assign,
        why="Curated official subset plus lazy access to the rest.",
        core=sorted(curated),
    )


def baseline_nexus_plus_unity(catalog: dict[str, Any]) -> dict[str, Any]:
    """Historical overlap: curated official tools and current Nexus tools together."""
    curated = set(_present(catalog, CURATED_IDS))
    nexus = set(_present(catalog, NEXUS_IDS))

    def assign(cap_id: str, capability: dict[str, Any]) -> dict[str, Any]:
        if cap_id in curated or cap_id in nexus:
            return _passthrough(capability, "ALWAYS_VISIBLE")
        if capability["origin"] == "official_unity":
            return _passthrough(capability, "LAZY")
        return _passthrough(capability, "DISABLED")

    return _manifest(
        catalog,
        candidate_id="BASELINE_CURRENT_NEXUS_PLUS_UNITY",
        strategy="hybrid",
        discovery="describe_invoke",
        assign=assign,
        why="Both surfaces visible. Overlap is a cost the lab can measure.",
        core=sorted(curated | nexus),
    )


def baseline_hidden_gateway(catalog: dict[str, Any]) -> dict[str, Any]:
    """The model sees Nexus wrapper names. Execution stays on official commands."""
    curated = set(_present(catalog, CURATED_IDS))

    def assign(cap_id: str, capability: dict[str, Any]) -> dict[str, Any]:
        if capability["origin"] != "official_unity":
            # Current Nexus commands are an alternate surface, not a second copy.
            return _passthrough(capability, "DISABLED")
        if cap_id in curated:
            return _passthrough(
                capability,
                "ALWAYS_VISIBLE",
                wrapper=f"nexus_{cap_id}",
            )
        return _passthrough(
            capability,
            "LAZY",
            wrapper=f"nexus_{cap_id}",
        )

    return _manifest(
        catalog,
        candidate_id="BASELINE_HIDDEN_GATEWAY",
        strategy="hidden_gateway",
        discovery="describe_invoke",
        assign=assign,
        why="Unity hidden. Nexus names pass through to official commands. Not claimed optimal.",
        core=[f"nexus_{item}" for item in sorted(curated)],
    )


def smoke_official_subset(catalog: dict[str, Any]) -> dict[str, Any]:
    """Smoke-only near-flat slice. This is not BASELINE_UNITY_FLAT."""
    chosen = set(_present(catalog, SMOKE_IDS))

    def assign(cap_id: str, capability: dict[str, Any]) -> dict[str, Any]:
        exposure = "ALWAYS_VISIBLE" if cap_id in chosen else "DISABLED"
        return _passthrough(capability, exposure)

    return _manifest(
        catalog,
        candidate_id="SMOKE_OFFICIAL_SUBSET",
        strategy="atomic",
        discovery="none",
        assign=assign,
        why="Smoke plumbing only. Not an official-baseline substitute.",
        core=sorted(chosen),
    )


def smoke_hidden_gateway(catalog: dict[str, Any]) -> dict[str, Any]:
    """Same smoke backends behind different model-visible names."""
    chosen = set(_present(catalog, SMOKE_IDS))
    wrappers = {
        "open_scene": "nexus_open_scene",
        "find_gameobjects": "nexus_find_gameobjects",
        "get_serialized_fields": "nexus_read_fields",
        "set_serialized_field": "nexus_set_field",
        "editor_status": "nexus_editor_status",
    }

    def assign(cap_id: str, capability: dict[str, Any]) -> dict[str, Any]:
        if cap_id not in chosen:
            return _passthrough(capability, "DISABLED")
        return _passthrough(capability, "ALWAYS_VISIBLE", wrapper=wrappers[cap_id])

    return _manifest(
        catalog,
        candidate_id="SMOKE_HIDDEN_GATEWAY",
        strategy="hidden_gateway",
        discovery="none",
        assign=assign,
        why="Smoke plumbing only. Proves the surface names change.",
        core=sorted(wrappers.values()),
    )


def zero_capability(catalog: dict[str, Any]) -> dict[str, Any]:
    """Intentionally useless architecture used by the degeneracy regression."""

    def assign(cap_id: str, capability: dict[str, Any]) -> dict[str, Any]:
        return _passthrough(capability, "DISABLED")

    return _manifest(
        catalog,
        candidate_id="ZERO_CAPABILITY",
        strategy="atomic",
        discovery="none",
        assign=assign,
        why="Degeneracy fixture. Must be INFEASIBLE.",
    )


def all_baselines(catalog: dict[str, Any]) -> list[dict[str, Any]]:
    return [
        baseline_unity_flat(catalog),
        baseline_unity_tags(catalog),
        baseline_curated(catalog),
        baseline_nexus_plus_unity(catalog),
        baseline_hidden_gateway(catalog),
    ]
