#!/usr/bin/env python3
"""Third-party T9* runs on the Puzzle copy (PREREG A1.8/A2/A5). Same arms/proxy/metrics as the fixture pilot."""
import os, sys, json, re, time, subprocess, random
W = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
os.environ["PILOT_PROJECT"] = os.path.join(W, "puzzle")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pilot, metrics
SCENE = "Assets/Libs/Layer Lab/GUI PRO Kit - Simple Casual/Scene/DemoScene.unity"
TARGET = "Main Camera/Canvas/DarkMode/Caht/Popup/Panel_Middle/ScrollRect"
PROMPT = ("In the GUI PRO Kit demo scene (Assets/Libs/Layer Lab/GUI PRO Kit - Simple Casual/Scene/DemoScene.unity) there is a RawImage "
          "on an object deep in the hierarchy (6 or more levels down) that never shows up when the scene starts. Find that object and tell "
          "me why. Don't change anything.\nFinish your reply with one line in exactly this form:\nANSWER: path=<full hierarchy path of the object>; reason=<short reason>")

def reset(arm):
    if not pilot.wait_ready(60): pilot.restart_editor()
    pilot.ucmd("editor_stop", timeout=30)
    pilot.git("reset", "--hard", "pilot-baseline"); pilot.git("clean", "-fd")
    pilot.write_agents_md(arm)
    pilot.ev('UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single); AssetDatabase.Refresh(); return "ok";', 120)
    nexus_ok = None
    if arm in ("S4", "S5"):
        pilot.ev('if (UnityMCP.Editor.MCPServer.IsRunning) return "running"; UnityMCP.Editor.MCPServer.Start(); return "started";', 60)
        time.sleep(3); nexus_ok = pilot.nexus_alive()
    pilot.ucmd("open_scene", {"path": SCENE}, timeout=120)
    return {"nexus_bridge_ok": nexus_ok, "port_file": os.path.exists(pilot.FIX + "/Library/Pipeline/.unity-pipeline-port")}

def oracle(final):
    m = pilot.ANS.findall(final or ""); ans = m[-1].strip().strip("`") if m else None
    det = {"answer": ans}
    st = pilot.git("status", "--porcelain", "--", "Assets", "ProjectSettings", "Packages").stdout.strip()
    det["readonly_git"] = st[:300]
    if not ans: return False, det | {"why": "no ANSWER"}
    if st: return False, det | {"why": "mutated"}
    pm = re.search(r"path\s*=\s*(.*?);\s*reason\s*=\s*(.*)$", ans, re.I)
    if not pm: return False, det | {"why": "format"}
    path = re.sub(r"\s*/\s*", "/", pm.group(1).strip().strip("`").lstrip("/")).strip("/")
    path = re.sub(r"^DemoScene(\.unity)?[:/]", "", path); reason = pm.group(2)
    ok = path == TARGET and re.search(r"Caht|DarkMode", reason) and re.search(r"inactive|disabled|not active|deactivated|turned off", reason, re.I)
    return bool(ok), det | {"why": "" if ok else f"path={path} reason={reason[:150]}"}

def run(arm, rep=1):
    run_id = f"T9P_{arm}_{rep}"; out = os.path.join(pilot.RUNS, run_id + ".jsonl")
    if os.path.exists(out + ".result.json"): return json.load(open(out + ".result.json"))
    t = time.time(); rs = reset(arm); t_reset = time.time() - t
    home = pilot.codex_home(arm, run_id); env = dict(os.environ, CODEX_HOME=home, PATH=pilot.path_env(arm))
    t0 = time.time()
    with open(out, "w") as f, open(out + ".err", "w") as e:
        p = subprocess.Popen([pilot.CODEX, "exec", "--json", "--skip-git-repo-check", "-C", pilot.FIX, PROMPT], stdin=subprocess.DEVNULL, stdout=f, stderr=e, env=env, cwd=pilot.FIX)
        try: p.wait(timeout=1800)
        except subprocess.TimeoutExpired: p.kill(); p.wait()
    wall = time.time() - t0; final = ""
    for line in open(out):
        try: ev = json.loads(line)
        except Exception: continue
        if ev.get("type") == "item.completed" and ev["item"].get("type") == "agent_message": final = ev["item"].get("text", "")
    ok, det = oracle(final); m = metrics.run_metrics(out, "T9", arm); cl = metrics.claimed("T9", final)
    infra = rs.get("nexus_bridge_ok") is False or not rs.get("port_file") or m["tokens_total"] == 0
    res = {"run": run_id, "task": "T9P", "arm": arm, "rep": rep, "semantic_ok": ok, "claimed_ok": cl, "false_success": bool(cl and not ok),
           "prod_policy_ok": bool(ok and (not arm.startswith("S6") or m["eval_like_calls"] == 0)), "infra_fail": infra, "void_leak": bool(m["leak_cmds"]),
           "final": final[-1500:], "oracle": det, "metrics": m, "wall_s": round(wall, 1), "reset_s": round(t_reset, 1), "reset": rs}
    json.dump(res, open(out + ".result.json", "w"), indent=1); return res

if __name__ == "__main__":
    arms = ["S4", "S5", "S6domain"]; random.Random(6000318).shuffle(arms)
    for a in arms:
        r = run(a)
        if r["infra_fail"] or r["void_leak"]: print("RERUN", a, flush=True); r = run(a, "1r")
        print(a, "ok" if r["semantic_ok"] else "FAIL", r["wall_s"], r["metrics"]["tokens_total"], r["oracle"].get("why"), flush=True)
    print("DONE")
