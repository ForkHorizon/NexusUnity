"""Compare the offline ranker with the real Codex tool_search outputs captured in codex_runs (runs with tool_search)."""
import glob, json, os
from pathlib import Path
import rank
HERE = Path(__file__).resolve().parent
U = [("unity", t) for t in rank.unity_tools()]
N = [("nexus-unity", t) for t in rank.nexus_tools()]
E = U + N
docs = [rank.search_text(t, s) for s, t in E]
for f in sorted(glob.glob(str(HERE / "codex_runs" / "run*_gpt55.jsonl"))):
    tid = next(json.loads(l)["thread_id"] for l in open(f) if '"thread.started"' in l)
    roll = glob.glob(os.path.expanduser(f"~/.codex/sessions/**/*{tid}.jsonl"), recursive=True)[0]
    calls, outs = [], []
    for l in open(roll):
        d = json.loads(l)
        if d.get("type") != "response_item":
            continue
        p = d["payload"]
        if p.get("type") == "tool_search_call":
            calls.append(p["arguments"])
        if p.get("type") == "tool_search_output":
            outs.append([(ns["name"], t["name"]) for ns in p["tools"] for t in ns.get("tools", [])])
    for a, real in zip(calls, outs):
        res = rank.bm25(docs, [a["query"]])["results"][0][: a.get("limit", 8)]
        mine = [("mcp__" + E[i][0].replace("-", "_"), E[i][1]["name"]) for i, _ in res]
        real_set, mine_set = set(real), set(mine)
        print(Path(f).stem, "| query:", a["query"], "| limit", a.get("limit"))
        print("   real (namespace-coalesced order):", [n for _, n in real])
        print("   offline (score order):           ", [f"{E[i][1]['name']}:{s:.2f}" for i, s in res])
        print("   set overlap:", len(real_set & mine_set), "/", len(real_set), "| missing:", sorted(n for _, n in real_set - mine_set),
              "| extra:", sorted(n for _, n in mine_set - real_set))
