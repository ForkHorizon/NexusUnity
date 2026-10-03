#!/usr/bin/env python3
"""Pilot analysis exactly per PREREG_PILOT §5 + Amendment A1.6 (labels, formulas, sensitivity)."""
import json, glob, os, math, sys, collections
H = os.path.dirname(os.path.abspath(__file__))
R = [json.load(open(f)) for f in sorted(glob.glob(os.path.join(H, "runs", "*.result.json")))]
R = [r for r in R if str(r["rep"]) not in ("0",)]                      # smoke runs excluded
# keep the rerun when the original was infra/void
byk = {}
for r in R:
    k = (r["task"], r["arm"], str(r["rep"]).rstrip("r"))
    if k not in byk or (str(r["rep"]).endswith("r")): byk[k] = r
R = list(byk.values())
FIX_TASKS = ["T1", "T2", "T3", "T5", "T7", "T9"]
ARMS = ["S4", "S5", "S6domain", "S6curated", "S6router"]

def ok(r):  # primary success (production policy for S6, A1.4)
    return r["prod_policy_ok"] if r["arm"].startswith("S6") else r["semantic_ok"]

def cell(task, arm): return [r for r in R if r["task"] == task and r["arm"] == arm]

def cps(task, arm, key="tokens_total"):
    rs = cell(task, arm); s = sum(ok(r) for r in rs)
    tot = sum(r["metrics"][key] if key in r["metrics"] else r["metrics"]["calls_total"] for r in rs)
    return (tot / s) if s else None, s, len(rs)

def ratio(task, x, c, key="tokens_total"):
    a, sa, _ = cps(task, x, key); b, sb, _ = cps(task, c, key)
    if sa == 0 and sb == 0: return None
    if sa == 0: return 4.0
    if sb == 0: return 0.25
    return min(4.0, max(0.25, a / b))

def label(x, c, tasks):
    rs = [ratio(t, x, c) for t in tasks]; rs = [v for v in rs if v is not None]
    gm = math.exp(sum(math.log(v) for v in rs) / len(rs)) if rs else None
    sx = sum(ok(r) for t in tasks for r in cell(t, x)); sc = sum(ok(r) for t in tasks for r in cell(t, c))
    fx = sum(r["false_success"] for t in tasks for r in cell(t, x)); fc = sum(r["false_success"] for t in tasks for r in cell(t, c))
    le1 = sum(1 for v in rs if v <= 1.0)
    need = math.ceil(len(tasks) * 4 / 6)
    if gm is not None and sx >= sc - 1 and gm <= 0.75 and le1 >= need and fx <= fc + 1: lab = "toward-" + x
    elif sx <= sc - 3 or (gm is not None and gm >= 1.3): lab = "against-" + x
    else: lab = "neutral"
    return {"label": lab, "succ_x": sx, "succ_c": sc, "fs_x": fx, "fs_c": fc, "gm_cost_ratio": gm and round(gm, 3),
            "per_task_ratio": {t: (round(v, 3) if v is not None else None) for t, v in zip(tasks, [ratio(t, x, c) for t in tasks])}, "tasks_le1": le1}

def summary():
    out = {"n_runs": len(R), "per_cell": {}, "labels": {}, "labels_sensitivity_excl_T5_T9": {}, "arm_totals": {}}
    for t in FIX_TASKS:
        for a in ARMS:
            rs = cell(t, a)
            if not rs: continue
            m = lambda k: round(sum(r["metrics"][k] for r in rs) / len(rs), 1)
            out["per_cell"][f"{t}/{a}"] = {"n": len(rs), "success": sum(ok(r) for r in rs), "observed_success": sum(r["semantic_ok"] for r in rs),
                "false_success": sum(r["false_success"] for r in rs), "mean_tokens": m("tokens_total"), "mean_uncached": m("tokens_uncached"),
                "mean_tool_output_tokens": m("tool_output_tokens"), "mean_calls": m("calls_total"), "mean_mcp_errors": m("mcp_errors"),
                "mean_invalid": m("invalid_arg_or_action"), "mean_retries": m("retries"), "mean_wrong_cap": m("wrong_capability"),
                "mean_discovery": m("discovery_calls"), "mean_escape": m("escape_calls"), "eval_like_runs": sum(1 for r in rs if r["metrics"]["eval_like_calls"]),
                "bypass_runs": sum(1 for r in rs if r["metrics"]["bypass_cmds"]), "mean_wall_s": round(sum(r["wall_s"] for r in rs) / len(rs), 1),
                "cost_per_success": cps(t, a)[0] and round(cps(t, a)[0]), "fails": [r["oracle"].get("why") or r["oracle"].get("oracle") for r in rs if not ok(r)]}
    for x, c in (("S6domain", "S4"), ("S6domain", "S5"), ("S5", "S4")):
        out["labels"][f"{x} vs {c}"] = label(x, c, FIX_TASKS)
        out["labels_sensitivity_excl_T5_T9"][f"{x} vs {c}"] = label(x, c, ["T1", "T2", "T3", "T7"])
    for a in ARMS:
        rs = [r for r in R if r["arm"] == a and r["task"] in FIX_TASKS]
        if not rs: continue
        calls = collections.Counter(u for r in rs for u in r["metrics"]["unity_commands"])
        ev = sum(r["metrics"]["eval_like_calls"] for r in rs); allc = sum(r["metrics"]["mcp_calls"] for r in rs)
        first = collections.Counter()
        for r in rs:
            srv = r["metrics"]["mcp_calls_by_server"]
            first[",".join(sorted(srv)) or "shell-only"] += 1
        cov = collections.Counter()
        for r in rs:
            m = r["metrics"]
            if m["bypass_cmds"]: cov["bypass"] += 1
            elif m["eval_like_calls"]: cov["needed_eval"] += 1
            elif m["escape_calls"]: cov["escape_hatch"] += 1
            else: cov["compact_only" if a.startswith("S6") else "n/a"] += 1
        out["arm_totals"][a] = {"runs": len(rs), "success": sum(ok(r) for r in rs), "observed_success": sum(r["semantic_ok"] for r in rs),
            "false_success": sum(r["false_success"] for r in rs), "tokens_total": sum(r["metrics"]["tokens_total"] for r in rs),
            "mean_tokens": round(sum(r["metrics"]["tokens_total"] for r in rs) / len(rs)), "eval_share_of_mcp_calls": round(ev / allc, 3) if allc else 0,
            "servers_used": dict(first), "coverage": dict(cov), "top_commands": calls.most_common(12),
            "wall_s_mean": round(sum(r["wall_s"] for r in rs) / len(rs), 1), "infra_or_void": sum(1 for r in rs if r["infra_fail"] or r["void_leak"])}
    return out

if __name__ == "__main__":
    print(json.dumps(summary(), indent=1))
