#!/usr/bin/env python3
"""RESEARCH PROTOTYPE. Approximate 0.3.1-exp.1 catalog (community skill menstood/unity-pipeline-commands-skill,
references/commands-*.md tables) vs live 0.7.0-exp.1 catalog. Param extraction from prose tables is approximate."""
import json, re, os, sys, collections
HERE = os.path.dirname(os.path.abspath(__file__))
REF = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "ups/references")
EV = os.path.normpath(os.path.join(HERE, "../../../v2_analysis/F_facade_evidence"))
new = {c["name"]: c for c in json.load(open(f"{EV}/unity_list.json"))["data"]["tools"]}
cg = json.load(open(f"{HERE}/catalog_groups.json"))["managers"]
mgr_of = {a: m for m, g in cg.items() for a in g["actions"]}

old = {}
for f in ("commands-content.md", "commands-editor.md"):
    for line in open(os.path.join(REF, f)):
        if not line.startswith("|"): continue
        cols = [c.strip() for c in line.strip().strip("|").split("|")]
        names = re.findall(r"`([a-z_0-9]+)`", cols[0])
        rest = " | ".join(cols[1:])
        if not names and len(cols) > 2 and re.search(r"`(get|set)_[a-z_]+`", cols[1]):  # settings table layout
            names = re.findall(r"`([a-z_0-9]+)`", cols[1]); rest = " | ".join(cols[2:]) + " `settings` `confirm` `dry_run`"
        toks = set(re.findall(r"`([A-Za-z_][A-Za-z0-9_]*)`", rest))
        for n in names: old[n] = toks
old_names, new_names = set(old), set(new) - {"nexus_capture_game_view", "nexus_group_compile_errors", "nexus_project_map"}
added, removed = sorted(new_names - old_names), sorted(old_names - new_names)
param_added, param_removed = {}, {}
for n in sorted(old_names & new_names):
    np = {p["name"] for p in new[n]["parameters"]}
    # a param 'exists' in 0.3.1 if mentioned as a backticked token in its row
    miss = sorted(p for p in np if p not in old[n])
    if miss: param_added[n] = miss
out = {"old_version": "0.3.1-exp.1 (community doc, approximate)", "new_version": "0.7.0-exp.1 (live)",
       "old_count": len(old_names), "new_count_unity_only": len(new_names), "common": len(old_names & new_names),
       "added": added, "removed_or_renamed": removed,
       "params_in_0.7_not_mentioned_in_0.3.1_row": param_added,
       "managers_touched_by_added": dict(collections.Counter(mgr_of[a] for a in added))}
json.dump(out, open(f"{HERE}/churn_0.3.1_to_0.7.0.json", "w"), indent=1)
print(json.dumps({k: (v if not isinstance(v, (list, dict)) else (len(v), v)) for k, v in out.items()}, indent=0)[:5000])
