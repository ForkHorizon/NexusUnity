#!/usr/bin/env python3
"""Research-only mock façade MCP server for the P0-16 pilot (never shipped).

Every Unity action is delegated to the OFFICIAL Unity command through one warm `unity mcp`
child process (no reimplementation). Nexus adds only: surface shape (per mode), output
normalisation for high-volume reads, handles, and a call log.

Modes (env FACADE_MODE):
  s5        – only the normalised read tools (added next to Unity + current Nexus in S4)
  s6domain  – 13 typed domain managers (catalog_groups.json) + normalised reads + nexus_get
  s6curated – 3 typed tools + describe/invoke (curated.json) + normalised reads + nexus_get
  s6router  – list/describe/run + nexus_get (normalised reads reachable as run commands)

Normaliser feature set is frozen to the v2.4 spec written BEFORE the pilot tasks existed
(F_facade_hypothesis.md §2.3/§2.6, plan P0-12 wording): bounded hierarchy (root/depth/
name filter/limit, sibling collapse), grouped console (level, dedupe, origin
file:line, top-k, cursor), bounded find (count + top paths + handle), component/serialized
reads with null/id stripping. No property-value queries, no task-specific logic.
"""
import json, os, re, subprocess, sys, threading, time, hashlib, collections

HERE = os.path.dirname(os.path.abspath(__file__))
MODE = os.environ.get("FACADE_MODE", "s6domain")
PROJECT = os.environ["FACADE_PROJECT"]
UNITY = os.environ.get("FACADE_UNITY_BIN", os.path.expanduser("~/.unity/bin/unity"))
LOG = os.environ.get("FACADE_LOG")
CAT = json.load(open(os.path.join(HERE, "catalog", "catalog_groups.json")))["managers"]
CURATED = json.load(open(os.path.join(HERE, "catalog", "curated.json")))["result"]["tools"]
MAX_CHARS = 24000  # ~6k tokens per response; larger results are stored behind a handle

# ---------------------------------------------------------------- unity child
class Unity:
    def __init__(self):
        self.p = subprocess.Popen([UNITY, "mcp", "--project-path", PROJECT], stdin=subprocess.PIPE,
                                  stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True, bufsize=1)
        self.i = 0; self.lock = threading.Lock()
        self.rpc("initialize", {"protocolVersion": "2025-06-18", "capabilities": {}, "clientInfo": {"name": "nexus-facade-mock", "version": "0"}})
        self.p.stdin.write(json.dumps({"jsonrpc": "2.0", "method": "notifications/initialized"}) + "\n"); self.p.stdin.flush()
        self.tools = {t["name"]: t for t in self.rpc("tools/list", {}).get("result", {}).get("tools", [])}

    def rpc(self, method, params):
        with self.lock:
            self.i += 1; rid = self.i
            self.p.stdin.write(json.dumps({"jsonrpc": "2.0", "id": rid, "method": method, "params": params}) + "\n"); self.p.stdin.flush()
            while True:
                line = self.p.stdout.readline()
                if not line: raise RuntimeError("unity mcp child exited")
                msg = json.loads(line)
                if msg.get("id") == rid: return msg

    def call(self, command, args):
        r = self.rpc("tools/call", {"name": command, "arguments": args or {}})
        if "error" in r: return True, r["error"].get("message", str(r["error"]))
        res = r["result"]; texts = [c.get("text", "") for c in res.get("content", []) if c.get("type") == "text"]
        others = [c for c in res.get("content", []) if c.get("type") != "text"]
        body = "\n".join(texts)
        try: body = json.loads(body)
        except Exception: pass
        return bool(res.get("isError")), (body if not others else {"text": body, "nontext": others})

U = None
HANDLES = {}

def handle_put(obj):
    s = json.dumps(obj, separators=(",", ":"))
    h = "h" + hashlib.sha1(s.encode()).hexdigest()[:10]
    HANDLES[h] = s; return h

def clip(obj):
    s = json.dumps(obj, separators=(",", ":"))
    if len(s) <= MAX_CHARS: return obj
    h = handle_put(obj)
    return {"truncated": True, "chars": len(s), "handle": h, "head": s[:MAX_CHARS // 2],
            "hint": "Result too large; fetch ranges with nexus_get{handle, offset, length} or narrow the query."}

# ---------------------------------------------------------------- normalisers
DEFAULT_COMPS = {"Transform", "RectTransform", "CanvasRenderer"}

def _walk(n, depth=1):
    yield n, depth
    for c in n.get("children", []): yield from _walk(c, depth + 1)

def _shape(n):
    return (re.sub(r"(\s*\(\d+\)|_\d+)$", "", n["name"]), tuple(n.get("components", [])), len(n.get("children", [])))

def norm_hierarchy(args):
    root = (args.get("root") or "").strip("/")
    depth = int(args.get("depth", 2)); limit = int(args.get("limit", 200))
    name = args.get("name")
    err, raw = U.call("get_scene_hierarchy", {k: v for k, v in {"path": args.get("path") or args.get("scene")}.items() if v})
    if err: return err, raw
    roots = raw.get("roots", []) if isinstance(raw, dict) else []
    if root:
        found = [n for r in roots for n, _ in _walk(r) if n.get("hierarchyPath", "").strip("/") == root]
        if not found: return True, {"error": "root_not_found", "root": root}
        roots = found
    total = sum(1 for r in roots for _ in _walk(r))
    if name:  # filtered flat listing (bounded)
        rx = re.compile(name, re.I)
        hits = [{"path": n.get("hierarchyPath"), "activeSelf": n.get("activeSelf", True),
                 "components": [c for c in n.get("components", []) if c not in DEFAULT_COMPS]}
                for r in roots for n, _ in _walk(r) if rx.search(n["name"])]
        out = {"scene": raw.get("sceneName"), "objects_scanned": total, "matches": len(hits), "items": hits[:limit]}
        if len(hits) > limit: out["more"] = handle_put(hits)
        return False, out

    def node(n, d):
        o = {"n": n["name"]}
        if not n.get("activeSelf", True): o["off"] = 1
        cs = [c for c in n.get("components", []) if c not in DEFAULT_COMPS]
        if cs: o["c"] = cs
        kids = n.get("children", [])
        if kids:
            if d >= depth: o["desc"] = sum(1 for _ in _walk(n)) - 1
            else:
                groups = collections.OrderedDict()
                for c in kids: groups.setdefault(_shape(c), []).append(c)
                ch = []
                for k, g in groups.items():
                    x = node(g[0], d + 1)
                    if len(g) > 1:
                        x["n"] = k[0] + "*"; x["x"] = len(g); x["names"] = [c["name"] for c in g][:12]
                        off = sum(1 for c in g if not c.get("activeSelf", True))
                        if off: x["off_n"] = off
                    ch.append(x)
                o["ch"] = ch
        return o
    tree = [node(r, 1) for r in roots]
    return False, clip({"scene": raw.get("sceneName"), "root": root or "/", "objects": total, "depth": depth,
                        "tree": tree, "hint": "Increase depth, set root=<path>, or filter by name (regex)."})

ORIGIN = re.compile(r"\(at ([^)]+:\d+)\)")

def norm_console(args):
    level = args.get("level"); top = int(args.get("top", 10))
    a = {"tail": int(args.get("tail", 1000))}
    if level: a["level"] = level
    err, raw = U.call("console", a)
    if err: return err, raw
    groups = collections.OrderedDict()
    for e in raw.get("entries", []):
        first = e.get("message", "").split("\n")[0][:240]
        k = (e.get("level"), first)
        g = groups.setdefault(k, {"level": e.get("level"), "message": first, "count": 0})
        g["count"] += 1
        if "origin" not in g:
            m = ORIGIN.search(e.get("message", "") + "\n" + e.get("stackTrace", ""))
            if m: g["origin"] = m.group(1)
    order = {"error": 0, "exception": 0, "warn": 1, "warning": 1, "log": 2}
    gl = sorted(groups.values(), key=lambda g: (order.get(g["level"], 3), -g["count"]))
    out = {"counts": raw.get("counts"), "distinct": len(gl), "groups": gl[:top], "cursor": raw.get("cursor")}
    if len(gl) > top: out["more"] = handle_put(gl)
    if raw.get("groundTruth"): out["compilationFailed"] = raw["groundTruth"].get("compilationFailed")
    return False, out

def norm_find(args):
    limit = int(args.pop("limit", 10)) if "limit" in args else 10
    err, raw = U.call("find_gameobjects", args)
    if err: return err, raw
    gos = raw.get("gameObjects", []) if isinstance(raw, dict) else []
    items = [g.get("hierarchyPath") or g.get("assetPath") for g in gos]
    out = {"count": raw.get("count", len(items)), "items": items[:limit]}
    if len(items) > limit: out["more"] = handle_put(items)
    return False, out

def _strip(o):
    if isinstance(o, dict):
        return {k: _strip(v) for k, v in o.items() if v not in (None, [], {}, "") and k not in ("globalId", "guid", "fileId", "instanceId")}
    if isinstance(o, list): return [_strip(v) for v in o]
    return o

def norm_strip(command):
    def f(args):
        err, raw = U.call(command, args)
        return err, (raw if err else clip(_strip(raw)))
    return f

def nexus_get(args):
    s = HANDLES.get(args.get("handle", ""))
    if s is None: return True, {"error": "unknown_handle"}
    off = int(args.get("offset", 0)); ln = int(args.get("length", 8000))
    return False, {"handle": args["handle"], "total_chars": len(s), "offset": off, "data": s[off:off + ln]}

NORMALISED = {"get_scene_hierarchy": norm_hierarchy, "console": norm_console, "find_gameobjects": norm_find,
              "get_component_properties": norm_strip("get_component_properties"),
              "get_serialized_fields": norm_strip("get_serialized_fields")}

def delegate(command, args):
    args = dict(args or {})
    detail = args.pop("detail", "normal")
    if command in NORMALISED and detail != "full":
        return NORMALISED[command](args)
    allowed = set(U.tools.get(command, {}).get("inputSchema", {}).get("properties", {}).keys())
    unknown = [k for k in args if k not in allowed]
    if unknown: return True, {"error": "invalid_arguments", "command": command, "unknown": unknown, "accepted": sorted(allowed)}
    err, raw = U.call(command, args)
    return err, (raw if err else clip(raw))

# ---------------------------------------------------------------- surfaces
NORM_PROPS = {
    "detail": {"type": "string", "enum": ["normal", "full"], "description": "normal (default) = compact Nexus-normalised result for large reads; full = raw Unity result (large results go behind a handle)."},
    "root": {"type": "string", "description": "get_scene_hierarchy: hierarchy path of the subtree root, e.g. 'Canvas/Panel'."},
    "depth": {"type": "integer", "description": "get_scene_hierarchy: levels to expand (default 2)."},
    "name": {"type": "string", "description": "get_scene_hierarchy: regex filter on object names (returns flat matches with paths); find_gameobjects: exact name."},
    "limit": {"type": "integer", "description": "Max items returned (rest behind a handle)."},
    "top": {"type": "integer", "description": "console: number of message groups returned (default 10)."},
}

READ_TOOLS = [
    {"name": "nexus_hierarchy", "description": "Compact scene hierarchy from the live Editor (official Unity get_scene_hierarchy, normalised): subtree root, depth limit, sibling collapse, name filter with full paths.",
     "inputSchema": {"type": "object", "properties": {k: NORM_PROPS[k] for k in ("root", "depth", "name", "limit")} | {"scene": {"type": "string", "description": "Scene asset path (default: active scene)."}}, "additionalProperties": False}},
    {"name": "nexus_console", "description": "Grouped Unity console (official Unity console, normalised): counts, distinct messages with repeat counts and origin file:line, errors first.",
     "inputSchema": {"type": "object", "properties": {"level": {"type": "string", "description": "log | warn | error (minimum level)"}, "top": NORM_PROPS["top"], "tail": {"type": "integer"}}, "additionalProperties": False}},
    {"name": "nexus_find", "description": "Find scene GameObjects (official Unity find_gameobjects, normalised): count + top paths, rest behind a handle.",
     "inputSchema": {"type": "object", "properties": {"name": {"type": "string"}, "tag": {"type": "string"}, "type": {"type": "string"}, "hierarchy_path": {"type": "string"}, "include_inactive": {"type": "boolean"}, "limit": NORM_PROPS["limit"]}, "additionalProperties": False}},
    {"name": "nexus_inspect", "description": "Read a component's properties or serialized fields (official Unity get_component_properties / get_serialized_fields, ids and nulls stripped).",
     "inputSchema": {"type": "object", "properties": {"target": {"type": "string", "description": "Hierarchy path or handle of the GameObject/asset."}, "type": {"type": "string", "description": "Component type (properties mode)."}, "component": {"type": "string", "description": "Component type (serialized-fields mode)."}, "field": {"type": "string"}, "mode": {"type": "string", "enum": ["properties", "serialized"]}}, "required": ["target"], "additionalProperties": False}},
]
GET_TOOL = {"name": "nexus_get", "description": "Fetch a range of a large stored result by handle.",
            "inputSchema": {"type": "object", "properties": {"handle": {"type": "string"}, "offset": {"type": "integer"}, "length": {"type": "integer"}}, "required": ["handle"], "additionalProperties": False}}

def read_tool_call(name, args):
    if name == "nexus_hierarchy": return norm_hierarchy(args)
    if name == "nexus_console": return norm_console(args)
    if name == "nexus_find": return norm_find(dict(args))
    if name == "nexus_inspect":
        mode = args.pop("mode", None) or ("serialized" if "component" in args or "field" in args else "properties")
        return norm_strip("get_serialized_fields" if mode == "serialized" else "get_component_properties")(args)

def domain_tools():
    out = []
    for m, v in CAT.items():
        if not isinstance(v, dict) or "actions" not in v: continue
        name = "nexus_" + m.replace("unity_", "")
        props = {"action": {"type": "string", "enum": list(v["actions"].keys()), "description": "Official Unity command to run."}}
        for an, a in v["actions"].items():
            for p in a["params"]:
                if p["name"] in props: continue
                props[p["name"]] = dict(p["schema"])
        for k in ("detail", "root", "depth", "name", "limit", "top"):
            if k not in props and any(x in v["actions"] for x in ("get_scene_hierarchy", "console", "find_gameobjects", "get_component_properties")):
                props[k] = NORM_PROPS[k]
        acts = "; ".join(f"{an}: {a['description'][:90]}" for an, a in v["actions"].items())
        out.append({"name": name, "description": v["description"] + " Actions → " + acts,
                    "inputSchema": {"type": "object", "properties": props, "required": ["action"], "additionalProperties": True}})
    return out

def curated_tools():
    out = []
    for t in CURATED:
        t = json.loads(json.dumps(t)); t["name"] = t["name"].replace("unity_", "nexus_")
        t["description"] = t["description"].replace("unity_describe", "nexus_describe").replace("unity_invoke", "nexus_invoke")
        out.append(t)
    return out

ROUTER = [
    {"name": "nexus_list", "description": "List Unity Editor capabilities available through Nexus (official Unity commands: name, one-line description). Filter by query substring.",
     "inputSchema": {"type": "object", "properties": {"query": {"type": "string"}}, "additionalProperties": False}},
    {"name": "nexus_describe", "description": "Full input schema for one command.", "inputSchema": {"type": "object", "properties": {"command": {"type": "string"}}, "required": ["command"], "additionalProperties": False}},
    {"name": "nexus_run", "description": "Run a command: {command, args}. Large reads (get_scene_hierarchy, console, find_gameobjects, component reads) return compact normalised results unless args.detail='full'.",
     "inputSchema": {"type": "object", "properties": {"command": {"type": "string"}, "args": {"type": "object"}}, "required": ["command"], "additionalProperties": False}},
]

def action_lookup(tool, args):
    if MODE == "s6domain":
        return args.pop("action", None)
    if MODE == "s6curated":
        if tool in ("nexus_invoke",): return args.get("command")
        return args.pop("action", None)

def describe(args):
    cmd = args.get("command")
    if cmd:
        t = U.tools.get(cmd)
        return (False, t) if t else (True, {"error": "unknown_command", "command": cmd})
    q = (args.get("query") or args.get("domain") or "").lower()
    items = [{"name": n, "description": t.get("description", "")[:110]} for n, t in U.tools.items() if not q or q in n or q in t.get("description", "").lower()]
    if MODE == "s6curated" and not q:
        return False, {m.replace("unity_", ""): list(v["actions"].keys()) for m, v in CAT.items() if isinstance(v, dict) and "actions" in v}
    return False, {"count": len(items), "commands": items}

def tools_for_mode():
    if MODE == "s5": return READ_TOOLS + [GET_TOOL]
    if MODE == "s6domain": return domain_tools() + [GET_TOOL]
    if MODE == "s6curated": return curated_tools() + [GET_TOOL]
    if MODE == "s6router": return ROUTER + [GET_TOOL]
    raise SystemExit("bad mode")

def dispatch(name, args):
    args = dict(args or {})
    if name == "nexus_get": return "nexus_get", nexus_get(args)
    if MODE == "s5": return name, read_tool_call(name, args)
    if name in ("nexus_list", "nexus_describe"): return name, describe(args)
    if name in ("nexus_run", "nexus_invoke"):
        cmd = args.get("command"); return cmd, delegate(cmd, args.get("args") or {})
    cmd = action_lookup(name, args)
    if not cmd: return None, (True, {"error": "missing_action"})
    if cmd not in U.tools: return cmd, (True, {"error": "unknown_action", "action": cmd})
    return cmd, delegate(cmd, args)

# ---------------------------------------------------------------- MCP loop
def log(rec):
    if LOG:
        with open(LOG, "a") as f: f.write(json.dumps(rec) + "\n")

def main():
    global U
    tools = None
    for line in sys.stdin:
        msg = json.loads(line); mid = msg.get("id"); m = msg.get("method")
        if m == "initialize":
            if U is None: U = Unity()
            tools = tools_for_mode()
            resp = {"protocolVersion": msg["params"].get("protocolVersion", "2025-06-18"), "capabilities": {"tools": {}},
                    "serverInfo": {"name": "nexus", "version": "0.0-mock"},
                    "instructions": "Nexus: compact interface to the live Unity Editor. Every action runs the official Unity command; large reads come back normalised."}
        elif m == "tools/list": resp = {"tools": tools}
        elif m == "tools/call":
            t0 = time.time(); name = msg["params"]["name"]; args = msg["params"].get("arguments") or {}
            try:
                cmd, (err, body) = dispatch(name, args)
            except Exception as e:
                cmd, err, body = None, True, {"error": "facade_exception", "message": str(e)}
            text = json.dumps(body, separators=(",", ":")) if not isinstance(body, str) else body
            log({"t": t0, "ms": round((time.time() - t0) * 1000), "tool": name, "args": args, "unity_command": cmd, "isError": err, "chars": len(text)})
            resp = {"content": [{"type": "text", "text": text}], "isError": bool(err)}
        elif mid is None: continue
        else: resp = {}
        if mid is not None:
            sys.stdout.write(json.dumps({"jsonrpc": "2.0", "id": mid, "result": resp}) + "\n"); sys.stdout.flush()

if __name__ == "__main__":
    main()
