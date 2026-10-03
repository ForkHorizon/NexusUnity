#!/usr/bin/env python3
"""Pilot driver (PREREG_PILOT §3, A1.5): seeded order, reset+run+oracle, one rerun on infra failure/void."""
import random, json, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pilot
rng = random.Random(6000316)
tasks = ["T1", "T2", "T3", "T5", "T7", "T9"]; rng.shuffle(tasks)
plan = []
for t in tasks:
    cells = [("S4", 1), ("S4", 2), ("S5", 1), ("S5", 2), ("S6domain", 1), ("S6domain", 2), ("S6curated", 1), ("S6router", 1)]
    rng.shuffle(cells); plan += [(t, a, r) for a, r in cells]
json.dump(plan, open(os.path.join(pilot.H, "plan.json"), "w"))
for t, a, r in plan:
    res = pilot.run(t, a, r)
    if res.get("infra_fail") or res.get("void_leak"):
        print("RERUN", t, a, r, flush=True); res = pilot.run(t, a, str(r) + "r")
    print(t, a, r, "ok" if res["semantic_ok"] else "FAIL", res["wall_s"], res["metrics"]["tokens_total"], flush=True)
print("DONE")
