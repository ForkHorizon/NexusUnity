"""Build a capability catalog from Pipeline and Nexus command sources.

The parser reads CliCommand / CliArg attributes and NexusCommandParameter
declarations. It does not hand-copy schemas. Live editor output can replace
parameter metadata later; until then schema_status is parsed_from_source.
"""

from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Any

from lab.canonical import sha256_json

_COMMAND = re.compile(r"\[CliCommand\(")
_ARG = re.compile(r"\[CliArg\(")
_STRING = re.compile(r'"((?:\\.|[^"\\])*)"')
_CONST_STRING = re.compile(
    r'public const string (\w+) =\s*"((?:\\.|[^"\\])*)"\s*;', re.S
)
_CONST_INT = re.compile(r"public const int (\w+) = (-?\d+)\s*;")
_NEXUS_PARAM = re.compile(
    r'NexusCommandParameter\.(Optional|Required)\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*[^,]+,\s*([^)]+?)\)'
)
_ALIAS = re.compile(r'public const string Alias = "([^"]+)"\s*;')
_DESCRIPTION = re.compile(
    r'public const string Description =\s*"((?:\\.|[^"\\])*)"\s*;', re.S
)


def _skip_string(text: str, i: int) -> int:
    if text[i] != '"':
        return i
    i += 1
    while i < len(text):
        if text[i] == "\\":
            i += 2
            continue
        if text[i] == '"':
            return i + 1
        i += 1
    return i


def _matching_paren(text: str, open_index: int) -> int:
    depth = 0
    i = open_index
    while i < len(text):
        ch = text[i]
        if ch == '"':
            i = _skip_string(text, i)
            continue
        if ch == "(":
            depth += 1
        elif ch == ")":
            depth -= 1
            if depth == 0:
                return i
        i += 1
    raise ValueError("unbalanced parenthesis")


def _strings(block: str) -> list[str]:
    return [bytes(item, "utf-8").decode("unicode_escape") for item in _STRING.findall(block)]


def _tags(block: str) -> list[str]:
    match = re.search(r"Tags\s*=\s*new\[\]\s*\{([^}]*)\}", block, re.S)
    if not match:
        return []
    return _strings(match.group(1))


def _flag(block: str, name: str) -> bool | None:
    match = re.search(rf"{name}\s*=\s*(true|false)", block)
    if not match:
        return None
    return match.group(1) == "true"


def _cli_type(cs_type: str) -> str:
    lowered = cs_type.strip().lower()
    if lowered in {"bool", "boolean"}:
        return "boolean"
    if lowered in {"int", "int32", "int64", "long", "short", "byte"}:
        return "integer"
    if lowered in {"float", "double", "single", "decimal"}:
        return "number"
    return "string"


def _parse_arg(block: str, cs_type: str) -> dict[str, Any]:
    strings = _strings(block)
    name = strings[0] if strings else "unnamed"
    description = strings[1] if len(strings) > 1 else ""
    required = _flag(block, "Required") is True
    default = None
    default_match = re.search(r"DefaultValue\s*=\s*([^,\]]+)", block)
    if default_match:
        default = _literal(default_match.group(1).strip())
    return {
        "name": name,
        "description": description,
        "type": _cli_type(cs_type),
        "required": required,
        "default": default,
    }


def _literal(token: str) -> Any:
    token = token.strip().rstrip(",")
    if token in {"null", "default"}:
        return None
    if token == "true":
        return True
    if token == "false":
        return False
    if re.fullmatch(r"-?\d+", token):
        return int(token)
    if re.fullmatch(r"-?\d+\.\d+", token):
        return float(token)
    strings = _strings(token)
    if strings:
        return strings[0]
    return token


def _args_until_body(text: str, start: int) -> list[dict[str, Any]]:
    """Collect CliArg attributes that belong to the command just parsed.

    The scan stops at the next CliCommand. Parentheses are matched on the
    full source so a brace inside an attribute does not truncate an argument.
    """
    nxt = _COMMAND.search(text, start)
    end = nxt.start() if nxt else min(len(text), start + 20000)
    window = text[start:end]
    args: list[dict[str, Any]] = []
    cursor = 0
    while True:
        found = _ARG.search(window, cursor)
        if not found:
            break
        open_paren = window.find("(", found.start())
        try:
            close = _matching_paren(window, open_paren)
        except ValueError:
            break
        block = window[found.start() : close + 1]
        after = window[close + 1 : close + 220]
        next_attr = re.search(r"\[", after)
        if next_attr:
            after = after[: next_attr.start()]
        type_match = re.search(r"\]\s*([\w.<>,\[\]?]+)", after)
        cs_type = type_match.group(1) if type_match else "string"
        eq = re.search(r"=\s*([^,\n\)]+)", after)
        parsed = _parse_arg(block, cs_type)
        if eq and parsed["default"] is None:
            parsed["default"] = _literal(eq.group(1))
        # A C# default means the argument is optional even if Required was omitted.
        if eq:
            parsed["required"] = False
        args.append(parsed)
        cursor = close + 1
    return args


def parse_cli_commands(text: str, source: str) -> list[dict[str, Any]]:
    """Return capabilities declared with CliCommand in one source file."""
    found: list[dict[str, Any]] = []
    cursor = 0
    while True:
        match = _COMMAND.search(text, cursor)
        if not match:
            break
        open_paren = text.find("(", match.start())
        close = _matching_paren(text, open_paren)
        block = text[match.start() : close + 1]
        strings = _strings(block)
        if not strings:
            cursor = close + 1
            continue
        name = strings[0]
        # Constant references such as ProjectMapCommand.Alias are not strings.
        if not re.fullmatch(r"[A-Za-z0-9_.-]+", name):
            cursor = close + 1
            continue
        description = strings[1] if len(strings) > 1 else ""
        params = _args_until_body(text, close + 1)
        required = [item["name"] for item in params if item["required"]]
        defaults = {
            item["name"]: item["default"]
            for item in params
            if item["default"] is not None
        }
        tags = _tags(block)
        found.append(
            _capability(
                name=name,
                description=description,
                parameters=params,
                required=required,
                defaults=defaults,
                tags=tags,
                source=source,
                main_thread=_flag(block, "MainThreadRequired"),
                origin="official_unity",
            )
        )
        cursor = close + 1
    return found


def _capability(
    *,
    name: str,
    description: str,
    parameters: list[dict[str, Any]],
    required: list[str],
    defaults: dict[str, Any],
    tags: list[str],
    source: str,
    main_thread: bool | None,
    origin: str,
) -> dict[str, Any]:
    domain = tags[0].split("/")[0] if tags else "untagged"
    return {
        "id": name,
        "backend_name": name,
        "domain_suggestion": domain,
        "description": description,
        "parameters": parameters,
        "required": required,
        "defaults": defaults,
        "tags": tags,
        "risk_class": _risk(name),
        "mutation_type": "surface",
        "output_characteristics": "unspecified",
        "version_support": "pipeline-0.8.0-exp.1",
        "schema_status": "parsed_from_source",
        "source": source,
        "main_thread_required": main_thread,
        "origin": origin,
        "allowed_backend_modes": ["PASSTHROUGH"],
        "allowed_normalizers": ["raw", "truncate_4k", "drop_base64"],
        "composite_id": None,
    }


def _risk(name: str) -> str:
    write_tokens = ("set_", "create_", "delete_", "move_", "write_", "save_", "build", "recompile", "import_", "rename_", "copy_", "add_", "remove_", "unpack_", "apply_", "revert_")
    if any(token in name for token in write_tokens):
        return "mutation"
    return "read"


def parse_nexus_commands(commands_dir: Path) -> list[dict[str, Any]]:
    """Read canonical Nexus descriptors from Editor/Commands sources."""
    capabilities: list[dict[str, Any]] = []
    for path in sorted(commands_dir.glob("*Command.cs")):
        text = path.read_text(encoding="utf-8")
        alias = _ALIAS.search(text)
        description = _DESCRIPTION.search(text)
        params = []
        consts = {name: value for name, value in _CONST_STRING.findall(text)}
        consts.update({name: int(value) for name, value in _CONST_INT.findall(text)})
        for kind, pname, ptype, default_token in _NEXUS_PARAM.findall(text):
            default = _literal(default_token.strip())
            if isinstance(default, str) and default in consts:
                default = consts[default]
            params.append(
                {
                    "name": pname,
                    "description": "",
                    "type": ptype,
                    "required": kind == "Required",
                    "default": default,
                }
            )
        if not alias:
            continue
        # Deduplicate parameters when a file mentions the same name twice.
        unique: dict[str, dict[str, Any]] = {}
        for item in params:
            unique.setdefault(item["name"], item)
        parameters = list(unique.values())
        required = [item["name"] for item in parameters if item["required"]]
        defaults = {
            item["name"]: item["default"]
            for item in parameters
            if item["default"] is not None
        }
        cap = _capability(
            name=alias.group(1),
            description=description.group(1) if description else "",
            parameters=parameters,
            required=required,
            defaults=defaults,
            tags=["nexus"],
            source=str(path.name),
            main_thread=None,
            origin="nexus_canonical",
        )
        if cap["id"] == "nexus_capture_game_view":
            cap["allowed_backend_modes"] = ["PASSTHROUGH", "ENHANCED"]
        elif cap["id"] == "nexus_group_compile_errors":
            cap["allowed_backend_modes"] = ["PASSTHROUGH", "ENHANCED", "COMPOSITE"]
            cap["composite_id"] = "lab_compile_status"
        elif cap["id"] == "nexus_project_map":
            cap["allowed_backend_modes"] = ["PASSTHROUGH", "ENHANCED"]
        capabilities.append(cap)
    return capabilities


def build_catalog(
    pipeline_root: Path | None,
    nexus_commands: Path | None,
    *,
    pipeline_version: str = "0.8.0-exp.1",
) -> dict[str, Any]:
    """Merge official and Nexus capabilities into one catalog document."""
    by_id: dict[str, dict[str, Any]] = {}
    if pipeline_root is not None:
        for path in sorted(pipeline_root.rglob("*.cs")):
            text = path.read_text(encoding="utf-8", errors="replace")
            if "CliCommand(" not in text:
                continue
            for cap in parse_cli_commands(text, str(path.relative_to(pipeline_root))):
                by_id[cap["id"]] = cap
    if nexus_commands is not None:
        for cap in parse_nexus_commands(nexus_commands):
            by_id[cap["id"]] = cap
    capabilities = [by_id[key] for key in sorted(by_id)]
    composites = {
        "lab_compile_status": {
            "steps": ["recompile", "recompile_status"],
            "note": "Fixed lab sequence. Mutations cannot invent steps.",
        }
    }
    body = {
        "schema_version": 1,
        "pipeline_version": pipeline_version,
        "capabilities": capabilities,
        "composites": composites,
        "class_map": CLASS_MAP,
    }
    body["catalog_hash"] = sha256_json(
        {"capabilities": capabilities, "composites": composites, "class_map": CLASS_MAP}
    )
    return body


# Capability classes the benchmark may require. Names are data, not a claim
# that these domains are the right production grouping.
CLASS_MAP = {
    "read": ["get_scene_hierarchy", "find_gameobjects", "get_serialized_fields", "get_component_properties"],
    "diagnose": ["recompile", "recompile_status", "console", "console_status"],
    "write": ["set_serialized_field"],
    "visual": ["capture_game_view", "capture_scene_view", "screenshot", "nexus_capture_game_view"],
    "project": ["editor_status", "nexus_project_map"],
    "rare": ["console_status"],
    "out_of_default": ["find_assets", "read_text_file", "reload_file"],
    "asset_read": ["find_assets", "read_text_file"],
}


def load_catalog(path: Path) -> dict[str, Any]:
    document = json.loads(path.read_text(encoding="utf-8"))
    expected = sha256_json(
        {
            "capabilities": document["capabilities"],
            "composites": document["composites"],
            "class_map": document["class_map"],
        }
    )
    if document.get("catalog_hash") != expected:
        raise ValueError(f"catalog hash mismatch for {path}")
    return document


def capability_index(catalog: dict[str, Any]) -> dict[str, dict[str, Any]]:
    return {item["id"]: item for item in catalog["capabilities"]}
