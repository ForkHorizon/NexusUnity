"""Summarise codex exec runs: discovery calls (tool_search / code-mode exec filters over ALL_TOOLS), their output
size, and the MCP tool finally called. Reads codex_runs/<tag>.jsonl (exec --json) + the matching rollout file."""
import glob, json, os, sys
from pathlib import Path
import tiktoken
ENC = tiktoken.get_encoding("o200k_base")
HERE = Path(__file__).resolve().parent
out = []
for f in sorted(glob.glob(str(HERE / "codex_runs" / "run*.jsonl"))):
    if "stub_calls" in f:
        continue
    tag = Path(f).stem
    ev = []
    for l in open(f):
        try:
            ev.append(json.loads(l))
        except Exception:
            pass  # exec --json stream occasionally interleaves two events on one line
    tid = next(e["thread_id"] for e in ev if e.get("type") == "thread.started")
    usage = next((e["usage"] for e in ev if e.get("type") == "turn.completed"), None)
    final = [e["item"].get("text") for e in ev if e.get("type") == "item.completed" and e["item"].get("type") == "agent_message"]
    mcp = [(e["item"]["server"], e["item"]["tool"], e["item"].get("arguments")) for e in ev
           if e.get("type") == "item.completed" and e["item"].get("type") == "mcp_tool_call"]
    roll = glob.glob(os.path.expanduser(f"~/.codex/sessions/**/*{tid}.jsonl"), recursive=True)
    steps = []
    if roll:
        for l in open(roll[0]):
            d = json.loads(l)
            if d.get("type") != "response_item":
                continue
            p = d["payload"]; t = p.get("type")
            if t in ("custom_tool_call", "function_call", "tool_search_call"):
                steps.append({"call": t, "name": p.get("name"), "input": (lambda v: v if isinstance(v, str) else json.dumps(v))(p.get("input") or p.get("arguments") or p.get("action"))[:400]})
            elif t in ("custom_tool_call_output", "function_call_output", "tool_search_output"):
                body = json.dumps(p.get("output") if t != "tool_search_output" else p.get("tools"))
                steps.append({"output": t, "tokens": len(ENC.encode(body)),
                              "head": body[:300]})
    out.append({"tag": tag, "thread": tid, "model_args": None, "mcp_calls": mcp, "final": final, "usage": usage, "steps": steps})
json.dump(out, open(HERE / "codex_runs" / "summary.json", "w"), indent=1)
for r in out:
    print("==", r["tag"], "usage", r["usage"] and {k: r["usage"][k] for k in ("input_tokens", "output_tokens")})
    for s in r["steps"]:
        print("   ", s)
    print("   MCP:", r["mcp_calls"], "| final:", r["final"])
