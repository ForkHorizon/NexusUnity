#!/usr/bin/env python3
"""Per-run metrics for the P0-16 pilot (PREREG_PILOT.md §4 + amendment A1)."""
import json, os, re, sys, glob
import tiktoken
ENC = tiktoken.get_encoding("cl100k_base")
T = lambda s: len(ENC.encode(s or ""))

EVAL_CMDS = {"eval", "eval_file", "run_script", "batch"}
HIER = {"get_scene_hierarchy", "nexus_hierarchy", "unity_hierarchy_manager"}
CONSOLE = {"console", "nexus_console", "console_status"}
WRONG = {"T5": HIER, "T2": HIER, "T9": CONSOLE, "T3": CONSOLE, "T1": CONSOLE, "T7": CONSOLE}
DISCOVERY = {"nexus_list", "nexus_describe"}
BYPASS = re.compile(r"(^|[\s;&|'\"(])unity(\s+(command|cmd|list|mcp|eval))|\.unity/bin|:7800|unity-pipeline-port|127\.0\.0\.1:78", re.I)
LEAK = re.compile(r"NexusFeature|TASKS_v1|G_nexus_primary|/harness/|Editor\.log|eval-usage", re.I)
NEG = re.compile(r"\b(couldn'?t|could not|unable|not able|failed to|blocked|cannot|can't|did not|didn'?t)\b", re.I)
POS = re.compile(r"\b(fixed|saved|now compiles?|compiles (cleanly|successfully)|compilation (now )?succeed|turned off|set .{0,40}(intensity|shadows)|resolved|done|updated)\b", re.I)

def unity_cmd(rec):
    """Official Unity command a logged MCP call maps to (S4/S5 unity server = tool name; façade = action/command)."""
    a = rec.get("args") or {}
    if rec["server"] == "unity": return rec["tool"]
    if rec["server"] == "nexus":
        if rec["tool"] in ("nexus_run", "nexus_invoke"): return a.get("command")
        return a.get("action") or rec["tool"]
    return rec["tool"]

def run_metrics(run_jsonl, task, arm):
    ev = [json.loads(l) for l in open(run_jsonl) if l.strip().startswith("{")]
    u = {"input": 0, "cached": 0, "output": 0, "reasoning": 0}
    items = [e["item"] for e in ev if e.get("type") == "item.completed"]
    for e in ev:
        if e.get("type") == "turn.completed":
            us = e.get("usage", {}); u["input"] += us.get("input_tokens", 0); u["cached"] += us.get("cached_input_tokens", 0)
            u["output"] += us.get("output_tokens", 0); u["reasoning"] += us.get("reasoning_output_tokens", 0)
    kinds = {}
    for it in items: kinds[it.get("type")] = kinds.get(it.get("type"), 0) + 1
    cmds = [it.get("command", "") for it in items if it.get("type") == "command_execution"]
    cmd_out = sum(T(it.get("aggregated_output", "")) for it in items if it.get("type") == "command_execution")
    mcp_out = 0
    for it in items:
        if it.get("type") == "mcp_tool_call" and it.get("result"):
            mcp_out += sum(T(c.get("text", "")) for c in it["result"].get("content", []) if c.get("type") == "text")
    plog = run_jsonl.replace(".jsonl", ".mcp.jsonl")
    calls = [json.loads(l) for l in open(plog)] if os.path.exists(plog) else []
    tl = [c for c in calls if "tools_list" in c]; calls = [c for c in calls if "tool" in c]
    errs = [c for c in calls if c["isError"]]
    inv = [c for c in errs if re.search(r"invalid_arguments|Parameter Validation|Invalid arg|has no parameter|required|unknown_action|unknown_command|missing_action", c.get("head", ""), re.I)]
    retries = 0
    for i, c in enumerate(calls):
        if c["isError"] and any(d["tool"] == c["tool"] for d in calls[i + 1:i + 3]): retries += 1
    ucmds = [unity_cmd(c) for c in calls]
    wrong = sum(1 for x, c in zip(ucmds, calls) if x in WRONG.get(task, set()) or c["tool"] in WRONG.get(task, set()))
    disc = sum(1 for c in calls if c["tool"] in DISCOVERY) + sum(1 for c in cmds if re.search(r"unity (command|cmd)\s*($|['\"]|--(query|tag|detail|group_by))|unity list", c))
    escape = sum(1 for c in calls if c["tool"] in ("nexus_invoke", "nexus_describe") or (c.get("args") or {}).get("detail") == "full")
    evalc = sum(1 for x in ucmds if x in EVAL_CMDS) + sum(1 for c in cmds if re.search(r"unity (command|cmd) (eval|eval_file|run_script|batch)\b", c))
    by_server = {}
    for c in calls: by_server[c["server"]] = by_server.get(c["server"], 0) + 1
    return {
        "tokens_input": u["input"], "tokens_cached": u["cached"], "tokens_uncached": u["input"] - u["cached"], "tokens_output": u["output"],
        "tokens_reasoning": u["reasoning"], "tokens_total": u["input"] + u["output"],
        "tool_output_tokens": mcp_out + cmd_out, "mcp_output_tokens": mcp_out, "shell_output_tokens": cmd_out,
        "items": kinds, "mcp_calls": len(calls), "mcp_calls_by_server": by_server, "shell_calls": len(cmds),
        "file_changes": kinds.get("file_change", 0), "calls_total": len(calls) + len(cmds) + kinds.get("file_change", 0),
        "mcp_errors": len(errs), "invalid_arg_or_action": len(inv), "retries": retries, "wrong_capability": wrong,
        "discovery_calls": disc, "escape_calls": escape, "eval_like_calls": evalc,
        "bypass_cmds": [c for c in cmds if BYPASS.search(c)] if arm.startswith("S6") else [],
        "leak_cmds": [c for c in cmds if LEAK.search(c)],
        "unity_ms": sum(c.get("ms", 0) for c in calls), "tools_list_chars": {c["server"]: c["chars"] for c in tl},
        "unity_commands": ucmds,
    }

def claimed(task, final):
    if task in ("T3", "T5", "T9"): return bool(re.search(r"^\s*\**\s*ANSWER:", final or "", re.I | re.M))
    return bool(POS.search(final or "")) and not NEG.search((final or "")[-600:])

if __name__ == "__main__":
    print(json.dumps(run_metrics(sys.argv[1], sys.argv[2], sys.argv[3]), indent=1)[:4000])
