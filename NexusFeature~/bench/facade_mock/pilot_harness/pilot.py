#!/usr/bin/env python3
"""P0-16 pilot harness (research only). Reset → run Codex arm → oracle → metrics. See PREREG_PILOT.md."""
import json, os, re, subprocess, sys, time, shutil, random, glob

H = os.path.dirname(os.path.abspath(__file__))
W = os.path.dirname(H)
FIX = os.environ.get("PILOT_PROJECT", os.path.join(W, "fixture"))
UNITY = os.path.expanduser("~/.unity/bin/unity")
CODEX = os.path.expanduser("~/.local/bin/codex")
REPO = "/Users/daliys/Daliys/UnityProjects/UnityTestForNexus/Assets/NexusUnity"
FACADE = REPO + "/NexusFeature~/bench/facade_mock/facade_mcp.py"
PROXY = REPO + "/NexusFeature~/bench/facade_mock/mcp_log_proxy.py"
RUNS = os.path.join(H, "runs")
KEY = json.load(open(os.path.join(H, "key", "key.json")))
FX = "Assets/BenchFixture/"

# ------------------------------------------------------------------ unity helpers (harness channel, not visible to arms)
def ucmd(cmd, args=None, timeout=120):
    a = [UNITY, "command", cmd, "--project-path", FIX, "--timeout", str(timeout), "--format", "json"]
    if args:
        a.append("--")
        for k, v in args.items(): a += ["--" + k, v if isinstance(v, str) else json.dumps(v)]
    r = subprocess.run(a, capture_output=True, text=True)
    try: d = json.loads(r.stdout)
    except Exception: return False, r.stdout + r.stderr
    if not d.get("success"): return False, (d.get("errors") or [{}])[0].get("message", "")
    return True, d["data"]["result"]

def ev(code, timeout=300):
    ok, r = ucmd("eval", {"code": code, "timeout": str(timeout * 1000)}, timeout=timeout + 10)
    if not ok: return "EVALFAIL:" + str(r)[:500]
    if isinstance(r, dict):
        if r.get("success") is False: return "EVALFAIL:" + json.dumps(r.get("diagnostics") or r)[:500]
        return r.get("result")
    return r

def wait_ready(t=600):
    t0 = time.time()
    while time.time() - t0 < t:
        ok, r = ucmd("editor_status", timeout=15)
        if ok and r.get("status") == "ready" and not r.get("compiling") and not r.get("domainReloadInProgress"): return True
        time.sleep(3)
    return False

PRE = r'''
static UnityEngine.GameObject F(UnityEngine.SceneManagement.Scene s, string path) { var parts = path.Split('/'); foreach (var r in s.GetRootGameObjects()) { if (r.name != parts[0]) continue; if (parts.Length == 1) return r; var t = r.transform.Find(string.Join("/", parts, 1, parts.Length - 1)); if (t != null) return t.gameObject; } return null; }
static UnityEngine.SceneManagement.Scene Open(string p) => UnityEditor.SceneManagement.EditorSceneManager.OpenScene(p, UnityEditor.SceneManagement.OpenSceneMode.Single);
static System.Collections.Generic.IEnumerable<UnityEngine.GameObject> All(UnityEngine.SceneManagement.Scene s) => s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UnityEngine.Transform>(true)).Select(t => t.gameObject);
static string PathOf(UnityEngine.GameObject g) { var t = g.transform; var p = t.name; while (t.parent != null) { t = t.parent; p = t.name + "/" + p; } return p; }
var FX = "Assets/BenchFixture/";
'''

ORACLE = {
"T1": PRE + r'''
var s = Open(FX+"Scenes/MainMenu.unity");
var ov = F(s, "FxCanvas/VignetteOverlay"); if (ov == null || !ov.activeInHierarchy) return "FAIL:overlay removed/inactive";
var img = ov.GetComponent<UnityEngine.UI.Image>(); if (img == null || !img.enabled || img.color.a < 0.3f) return "FAIL:vignette look changed";
var fx = F(s, "FxCanvas").GetComponent<UnityEngine.Canvas>(); var menu = F(s, "MenuCanvas").GetComponent<UnityEngine.Canvas>();
if (fx.sortingOrder <= menu.sortingOrder) return "FAIL:vignette no longer drawn over menu";
bool groupsBlock = ov.GetComponentsInParent<UnityEngine.CanvasGroup>(true).All(g => g.blocksRaycasts);
var gr = fx.GetComponent<UnityEngine.UI.GraphicRaycaster>();
bool stillBlocks = img.raycastTarget && groupsBlock && gr != null && gr.enabled;
if (stillBlocks) return "FAIL:overlay still blocks raycasts";
var play = F(s, "MenuCanvas/MainPanel/Buttons/PlayButton").GetComponent<UnityEngine.UI.Button>();
if (!play.interactable || play.onClick.GetPersistentEventCount() != 1 || play.onClick.GetPersistentMethodName(0) != "OnPlayPressed") return "FAIL:play button wiring changed";
if (F(s, "MenuCanvas/MainPanel/Buttons/CreditsButton").GetComponent<UnityEngine.UI.Button>().interactable) return "FAIL:credits button changed";
return "PASS";''',
"T7": PRE + r'''
var s = Open(FX+"Scenes/Harbor.unity");
string[] pier3 = { "Harbor/Waterfront/Piers/Pier_03/Lamps/Lantern_Post", "Harbor/Waterfront/Piers/Pier_03/Lamps/lamp", "Harbor/Waterfront/Piers/Pier_03/Deck/bollard_row (1)/Lantern_Post (3)", "Harbor/Waterfront/Piers/Pier_03/End/Light Pole" };
foreach (var p in pier3) { var go = F(s, p); if (go == null) return "FAIL:missing " + p;
  var l = go.transform.Find("Head/Lamp").GetComponent<UnityEngine.Light>();
  if (l.shadows != UnityEngine.LightShadows.None || UnityEngine.Mathf.Abs(l.intensity - 1.5f) > 0.001f) return "FAIL:not changed " + p; }
var work = F(s, "Harbor/Waterfront/Piers/Pier_03/End/WorkLight").GetComponent<UnityEngine.Light>();
if (work.shadows != UnityEngine.LightShadows.Hard || UnityEngine.Mathf.Abs(work.intensity - 3f) > 0.001f) return "FAIL:WorkLight changed";
var props = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(FX+"Prefabs/Props/Lantern_Post.prefab");
var pl = props.transform.Find("Head/Lamp").GetComponent<UnityEngine.Light>();
if (pl.shadows != UnityEngine.LightShadows.Soft || UnityEngine.Mathf.Abs(pl.intensity - 2.2f) > 0.001f) return "FAIL:prefab asset changed";
foreach (var l in All(s).Select(g => g.GetComponent<UnityEngine.Light>()).Where(l => l != null && l.name == "Lamp")) {
  var path = PathOf(l.gameObject); if (pier3.Any(p => path.StartsWith(p + "/"))) continue;
  var root = PrefabUtility.GetCorrespondingObjectFromOriginalSource(l.gameObject);
  bool legacy = AssetDatabase.GetAssetPath(root).Contains("/Art/Legacy/");
  bool rusty = PrefabUtility.GetCorrespondingObjectFromSource(PrefabUtility.GetNearestPrefabInstanceRoot(l.gameObject)) is UnityEngine.GameObject src && src.name == "Lantern_Post_Rusty";
  float want = legacy ? 1.2f : rusty ? 1.8f : 2.2f; var sh = legacy ? UnityEngine.LightShadows.None : UnityEngine.LightShadows.Soft;
  if (l.shadows != sh || UnityEngine.Mathf.Abs(l.intensity - want) > 0.001f) return "FAIL:other lantern changed " + path; }
if (All(s).Count() != %d) return "FAIL:objects added/removed " + All(s).Count();
return "PASS";''' % KEY["KEY_TOTAL"],
"T2": r'''
if (EditorUtility.scriptCompilationFailed) return "FAIL:Unity compile still failing";
if (System.Type.GetType("Saltmarsh.Weather.WeatherProfile, Saltmarsh.Weather") == null) return "FAIL:WeatherProfile moved/removed";
var baker = System.Type.GetType("Saltmarsh.EditorTools.HarborLightBaker, Saltmarsh.Editor");
if (baker == null || baker.GetMethod("Bake", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static) == null) return "FAIL:merged HarborLightBaker removed";
var path = "Assets/BenchFixture/Scripts/Editor/Lighting/HarborLightBaker.cs";
var src = System.IO.File.ReadAllLines(path).Where(l => !l.TrimStart().StartsWith("//"));
if (!src.Any(l => l.Contains("WeatherProfile")) || System.IO.File.ReadAllText(path).Contains("#if false")) return "FAIL:merged code disabled";
return "PASS";''',
}

GT = {  # ground-truth self-checks (TASKS_v1)
"T3": PRE + r'''
var src = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(FX+"Prefabs/Props/Lantern_Post.prefab");
var res = new System.Collections.Generic.List<string>();
foreach (var sc in new[]{"Harbor","Docks_Test","MainMenu"}) { var s = Open(FX+"Scenes/"+sc+".unity");
  res.Add(sc + "=" + All(s).Count(g => PrefabUtility.IsOutermostPrefabInstanceRoot(g) && PrefabUtility.GetCorrespondingObjectFromSource(g) == src)); }
var path = AssetDatabase.GetAssetPath(src);
var users = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && p != path && !p.EndsWith(".unity") && AssetDatabase.GetDependencies(p, false).Contains(path));
return string.Join(";", res) + " | " + string.Join(",", users);''',
"T9": PRE + r'''
var clip = AssetDatabase.LoadAssetAtPath<UnityEngine.AudioClip>(FX+"Audio/bell_buoy.wav");
var s = Open(FX+"Scenes/Harbor.unity");
var hits = All(s).Select(g => g.GetComponent<UnityEngine.AudioSource>()).Where(a => a != null && a.clip == clip && a.playOnAwake && a.enabled && !a.gameObject.activeInHierarchy).Select(a => PathOf(a.gameObject)).ToList();
var n = All(s).Select(g => g.GetComponent<UnityEngine.AudioSource>()).Count(a => a != null && a.clip == clip);
return n + " | " + string.Join(";", hits);''',
"T1": ORACLE["T1"], "T7": ORACLE["T7"],
}

START_SCENE = {"T1": "MainMenu", "T8": "MainMenu"}

TASKS = {}
def load_prompts():
    t = open(REPO + "/NexusFeature~/v2_analysis/G_nexus_primary/TASKS_v1.md").read()
    for tid in ("T1", "T2", "T3", "T5", "T7", "T9"):
        sec = t.split("### " + tid + " ")[1].split("**Required")[0]
        lines = [l[2:] if l.startswith("> ") else l[1:] for l in sec.splitlines() if l.startswith(">")]
        TASKS[tid] = "\n".join(lines).replace("`ANSWER:", "ANSWER:").replace(">`", ">").strip().rstrip("`")
load_prompts()

# ------------------------------------------------------------------ reset (TASKS_v1 §5, pilot subset)
def git(*a): return subprocess.run(["git", "-C", FIX, *a], capture_output=True, text=True)

SEED = ('var m = System.Type.GetType("Saltmarsh.EditorTools.SaltmarshStartupChecks, Saltmarsh.Editor").GetMethod("RunAll", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static); '
        'm.Invoke(null, null); return "seeded";')

def nexus_alive():
    import urllib.request
    tok = open(FIX + "/Library/NexusUnityAuthToken.txt").read().strip()
    req = urllib.request.Request("http://127.0.0.1:8081/", data=json.dumps({"jsonrpc": "2.0", "method": "get_editor_state", "params": {}, "id": 1}).encode(),
                                 headers={"Content-Type": "application/json", "X-Nexus-Unity-Token": tok})
    try: return b'"result"' in urllib.request.urlopen(req, timeout=10).read()
    except Exception: return False

def recompile_wait(expect_fail=None, t=300):
    ucmd("recompile", timeout=60)
    t0 = time.time(); st = None
    while time.time() - t0 < t:
        time.sleep(3)
        ok, st = ucmd("recompile_status", timeout=15)
        if ok and isinstance(st, str):
            try: st = json.loads(st)
            except Exception: pass
        if ok and isinstance(st, dict) and st.get("status") in ("completed", "up_to_date", "idle"): break
    wait_ready(t)
    return st

def restart_editor():
    subprocess.run(["pkill", "-f", "projectpath " + FIX]); time.sleep(10)
    subprocess.run(["pkill", "-9", "-f", "projectpath " + FIX]); time.sleep(3)
    subprocess.run([UNITY, "open", FIX, "--non-interactive", "--args", "--mcp-port 8081"], capture_output=True)
    time.sleep(20); return wait_ready(600)

def reset(task, arm):
    if not wait_ready(60): restart_editor()   # A4: recover from an unreachable Pipeline instead of hanging
    ucmd("editor_stop", timeout=30)
    git("reset", "--hard", "fixture-v1"); git("clean", "-fdx", "-e", "Library", "-e", "Temp", "-e", "Logs", "-e", "UserSettings", "-e", "obj")
    write_agents_md(arm)
    wait_ready()
    ev('UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); return "ok";', 120)
    recompile_wait()
    if task == "T2":
        os.makedirs(FIX + "/Assets/BenchFixture/Scripts/Editor/Lighting", exist_ok=True)
        shutil.copy(H + "/variants/T2/TideClock.cs", FIX + "/Assets/BenchFixture/Scripts/Runtime/Tides/TideClock.cs")
        shutil.copy(H + "/variants/T2/HarborLightBaker.cs", FIX + "/Assets/BenchFixture/Scripts/Editor/Lighting/HarborLightBaker.cs")
        ev('AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); return "ok";', 120)
        recompile_wait()
    ucmd("clear_console")
    ev(SEED, 60)
    counts = None
    for _ in range(30):
        time.sleep(2); ok, cs = ucmd("console_status", timeout=15)
        if ok and cs.get("counts", {}).get("warn", 0) >= 421: counts = cs.get("counts"); break
    nexus_ok = None
    if arm in ("S4", "S5"):  # A3: Nexus auto-start never fires in an unfocused idle Editor; start it like a user would
        ev('if (UnityMCP.Editor.MCPServer.IsRunning) return "running"; UnityMCP.Editor.MCPServer.Start(); return "started";', 60)
        time.sleep(3); nexus_ok = nexus_alive()
    scene = START_SCENE.get(task, "Harbor")
    ucmd("open_scene", {"path": FX + "Scenes/" + scene + ".unity"})
    ok, cs2 = ucmd("console_status")
    return {"nexus_bridge_ok": nexus_ok, "port_file": os.path.exists(FIX + "/Library/Pipeline/.unity-pipeline-port"), "counts_after_seed": counts, "compileFailed_at_start": (cs2 or {}).get("groundTruth", {}).get("compilationFailed") if ok else None}

# ------------------------------------------------------------------ arms
COMMON = ("This is a Unity 6 project. A Unity Editor is running with this project open; use the available Unity tools to inspect "
          "and change Editor state instead of editing scene/prefab YAML by hand. Save scenes you change.")
ARM_LINE = {
    "S4": "Unity Editor tools: the `unity` MCP server (official Unity CLI commands; also `unity command <name>` in the shell) and the `nexus-unity` MCP server (Nexus).",
    "S5": "Unity Editor tools: the `unity` MCP server (official Unity CLI commands; also `unity command <name>` in the shell) and the `nexus-unity` MCP server (Nexus). The `nexus` server adds compact reads (nexus_hierarchy, nexus_console, nexus_find, nexus_inspect).",
}
for a in ("S6domain", "S6curated", "S6router"):
    ARM_LINE[a] = "Unity Editor tools: the `nexus` MCP server (Nexus; it runs the official Unity commands for you)."

def write_agents_md(arm):
    open(os.path.join(FIX, "AGENTS.md"), "w").write("# Project notes\n\n" + COMMON + "\n\n" + ARM_LINE[arm] + "\n")

def codex_home(arm, run_id):
    home = os.path.join(H, "homes", arm); os.makedirs(home, exist_ok=True)
    if not os.path.exists(home + "/auth.json"): os.symlink(os.path.expanduser("~/.codex/auth.json"), home + "/auth.json")
    net = "true"  # equal for all arms (critic C, HIGH); S6 hiding = PATH only, bypass detected and reported
    log = os.path.join(RUNS, run_id + ".facade.jsonl")
    cfg = [f'model = "gpt-6-luna"', 'model_reasoning_effort = "medium"', 'approval_policy = "never"', 'sandbox_mode = "workspace-write"',
           'allow_login_shell = false', 'web_search = "disabled"', '', '[sandbox_workspace_write]', f'network_access = {net}', '']
    plog = os.path.join(RUNS, run_id + ".mcp.jsonl")
    def srv(name, cmd, args, env=None):
        args = [PROXY, plog, name, "--", cmd] + args; cmd = "/opt/homebrew/bin/python3"
        cfg.extend([f"[mcp_servers.{name}]", f'command = "{cmd}"', "args = " + json.dumps(args), "startup_timeout_sec = 90",
                    "tool_timeout_sec = 600", 'default_tools_approval_mode = "approve"'])
        if env: cfg.append("env = { " + ", ".join(f'{k} = "{v}"' for k, v in env.items()) + " }")
        cfg.append("")
    if arm in ("S4", "S5"):
        srv("unity", UNITY, ["mcp", "--project-path", FIX])
        tok = open(FIX + "/Library/NexusUnityAuthToken.txt").read().strip()
        srv("nexus-unity", "/opt/homebrew/bin/python3", [FIX + "/nexus_unity_bridge.py"], {"NEXUS_UNITY_AUTH_TOKEN": tok})
    if arm != "S4":
        mode = {"S5": "s5", "S6domain": "s6domain", "S6curated": "s6curated", "S6router": "s6router"}[arm]
        srv("nexus", "/opt/homebrew/bin/python3", [FACADE], {"FACADE_MODE": mode, "FACADE_PROJECT": FIX, "FACADE_LOG": log})
    open(home + "/config.toml", "w").write("\n".join(cfg))
    return home

def path_env(arm):
    base = "/usr/bin:/bin:/usr/sbin:/sbin:/opt/homebrew/bin"
    return (os.path.join(H, "sandbox_bin") + ":" + base) if arm in ("S4", "S5") else base  # A4 shim

# ------------------------------------------------------------------ oracle
ANS = re.compile(r"^\s*\**\s*ANSWER:\s*(.*)$", re.I | re.M)

def readonly_ok():
    st = git("status", "--porcelain", "--", "Assets", "ProjectSettings", "Packages").stdout.strip()
    dirty = ev('var d = new System.Collections.Generic.List<string>(); for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++) { var sc = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i); if (sc.isDirty) d.Add(sc.name); } return string.Join(",", d);', 30)
    return (st == "" and not dirty), {"git": st[:500], "dirty_scenes": dirty}

def norm_path(p):
    p = p.strip().strip("`").strip().lstrip("/")
    p = re.sub(r"^(Harbor\.unity[:/])", "", p); p = re.sub(r"\s*/\s*", "/", p)
    return p.strip("/")

def oracle(task, final):
    m = ANS.findall(final or ""); ans = m[-1].strip().strip("`") if m else None
    det = {"answer": ans}
    if task in ("T3", "T5", "T9"):
        ro, info = readonly_ok(); det["readonly"] = info
        if not ans: return False, det | {"why": "no ANSWER"}
        if not ro: return False, det | {"why": "mutated during read-only task"}
        if task == "T3":
            parts = [x.strip() for x in ans.split(";")]
            scenes = {}; assets = set()
            for p in parts:
                if p.lower().startswith("assets="):
                    assets = {re.sub(r"\.(prefab|asset)$", "", a.strip().split("/")[-1].lower()) for a in p[7:].split(",") if a.strip()}
                elif "=" in p:
                    k, v = p.split("=", 1); scenes[k.strip().lower().replace(".unity", "")] = v.strip()
            ok = scenes.get("harbor") == "17" and scenes.get("docks_test") == "3" and all(v in ("0",) for k, v in scenes.items() if k not in ("harbor", "docks_test")) \
                 and assets == {"streetkit", "lantern_post_rusty", "lanternset"}
            return ok, det | {"why": "" if ok else f"scenes={scenes} assets={assets}"}
        if task == "T5":
            cm = re.search(r"count\s*=\s*(\d+)", ans, re.I); toks = re.findall(r"([A-Za-z0-9_]+\.cs):(\d+)", ans)
            got = {(f.lower(), int(l)) for f, l in toks}
            need = {("manifestvalidator.cs", 47), ("localizationcheck.cs", 31), ("harborambience.cs", 58)}
            bad = any(x in ans for x in ("AudioBankCheck", "SaltmarshStartupChecks", "[Net]")) or "tides" in ans.lower()
            ok = bool(cm) and cm.group(1) == "3" and need <= got and not bad
            return ok, det | {"why": "" if ok else f"count={cm and cm.group(1)} got={sorted(got)} bad={bad}"}
        if task == "T9":
            pm = re.search(r"path\s*=\s*(.*?);\s*reason\s*=\s*(.*)$", ans, re.I)
            if not pm: return False, det | {"why": "format"}
            path, reason = norm_path(pm.group(1)), pm.group(2)
            ok = path == "Harbor/Waterfront/Breakwater/Segment_C (2)/Markers/BuoyField_East/buoy.r12/Mount/BellAudio" and \
                 "BuoyField_East" in reason and re.search(r"inactive|disabled|not active|deactivated|turned off", reason, re.I) is not None
            return ok, det | {"why": "" if ok else f"path={path} reason={reason[:120]}"}
    if task in ("T1", "T7"):
        r = ev(ORACLE[task], 120); det["oracle"] = r
        return r == "PASS", det
    if task == "T2":
        ev('AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); return "ok";', 120)
        det["recompile"] = recompile_wait()
        r = ev(ORACLE["T2"], 60); det["oracle"] = r
        if r != "PASS": return False, det
        ok, st = ucmd("run_tests", {"mode": "EditMode", "filter": "Saltmarsh.Tests.EditMode", "filter_type": "assembly", "timeout": "300"}, timeout=330)
        det["tests"] = json.dumps(st)[:1500]
        s = json.dumps(st)
        # require TideMath (3) + TideClock (1) passing; baseline has PriceTable bulk failing
        tm_fail = re.search(r'TideMath[^"]*"[^}]*"(Failed|failed)', s) or re.search(r'TideClock[^"]*"[^}]*"(Failed|failed)', s)
        det["tests_ok"] = ok and not tm_fail
        return bool(ok and not tm_fail), det
    return False, det

CLAIM = re.compile(r"\b(fixed|done|resolved|compiles|now compile|saved|turned off|set (?:their|the) .*intensity|passes|working)\b", re.I)

# ------------------------------------------------------------------ run
def run(task, arm, rep):
    run_id = f"{task}_{arm}_{rep}"
    out = os.path.join(RUNS, run_id + ".jsonl")
    if os.path.exists(out + ".result.json"): return json.load(open(out + ".result.json"))
    t_reset = time.time(); rs = reset(task, arm); t_reset = time.time() - t_reset
    home = codex_home(arm, run_id)
    env = dict(os.environ, CODEX_HOME=home, PATH=path_env(arm))
    t0 = time.time()
    with open(out, "w") as f, open(out + ".err", "w") as e:
        p = subprocess.Popen([CODEX, "exec", "--json", "--skip-git-repo-check", "-C", FIX, TASKS[task]], stdin=subprocess.DEVNULL, stdout=f, stderr=e, env=env, cwd=FIX)
        try: p.wait(timeout=1800)
        except subprocess.TimeoutExpired: p.kill(); p.wait()
    wall = time.time() - t0
    final = ""; items = []
    for line in open(out):
        try: ev_ = json.loads(line)
        except Exception: continue
        if ev_.get("type") == "item.completed" and ev_["item"].get("type") == "agent_message": final = ev_["item"].get("text", "")
    ok, det = oracle(task, final)
    import metrics
    m = metrics.run_metrics(out, task, arm)
    cl = metrics.claimed(task, final)
    infra = (rs.get("counts_after_seed") is None) or rs.get("nexus_bridge_ok") is False or not rs.get("port_file", True) or (task == "T2" and not rs.get("compileFailed_at_start")) or m["tokens_total"] == 0
    res = {"run": run_id, "task": task, "arm": arm, "rep": rep, "semantic_ok": ok, "claimed_ok": cl, "false_success": bool(cl and not ok),
           "prod_policy_ok": bool(ok and (not arm.startswith("S6") or m["eval_like_calls"] == 0)), "infra_fail": infra,
           "void_leak": bool(m["leak_cmds"]), "final": final[-1500:], "oracle": det, "metrics": m,
           "wall_s": round(wall, 1), "reset_s": round(t_reset, 1), "reset": rs, "timed_out": wall >= 1799}
    json.dump(res, open(out + ".result.json", "w"), indent=1)
    return res

if __name__ == "__main__":
    if sys.argv[1] == "gt":
        for t in ("T3", "T9", "T1", "T7"): print(t, ev(GT[t], 120))
    elif sys.argv[1] == "reset":
        print(reset(sys.argv[2], sys.argv[3]))
    elif sys.argv[1] == "oracle":
        print(oracle(sys.argv[2], sys.argv[3] if len(sys.argv) > 3 else ""))
    elif sys.argv[1] == "run":
        print(json.dumps(run(sys.argv[2], sys.argv[3], int(sys.argv[4])), indent=1)[:3000])
    elif sys.argv[1] == "prompts":
        for k, v in TASKS.items(): print("==", k, "\n" + v)
