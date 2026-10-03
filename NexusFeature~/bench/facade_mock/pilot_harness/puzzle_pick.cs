static System.Collections.Generic.IEnumerable<UnityEngine.GameObject> All(UnityEngine.SceneManagement.Scene s) => s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UnityEngine.Transform>(true)).Select(t => t.gameObject);
static string PathOf(UnityEngine.GameObject g) { var t = g.transform; var p = t.name; while (t.parent != null) { t = t.parent; p = t.name + "/" + p; } return p; }
static int Depth(UnityEngine.Transform t) { int d = 1; while (t.parent != null) { d++; t = t.parent; } return d; }
static bool InactiveAncestor(UnityEngine.Transform t) { t = t.parent; while (t != null) { if (!t.gameObject.activeSelf) return true; t = t.parent; } return false; }
var outp = new System.Collections.Generic.List<string>();
// T3*: scenes in order, first with a qualifying prefab
foreach (var sp in new[]{ "Assets/Scenes/Game.unity", "Assets/Scenes/EditorTool.unity", "Assets/Libs/Epic Toon FX/Demo/Scenes/Secrets/etfx_library.unity" }) {
  var s = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(sp, UnityEditor.SceneManagement.OpenSceneMode.Single);
  var inst = All(s).Where(g => PrefabUtility.IsOutermostPrefabInstanceRoot(g)).GroupBy(g => AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(g))).Where(g => g.Count() >= 5).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).ToList();
  string pick = null; string users = null;
  foreach (var g in inst) {
    var u = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && p != g.Key && !p.EndsWith(".unity") && AssetDatabase.GetDependencies(p, false).Contains(g.Key)).ToList();
    if (u.Count >= 1) { pick = g.Key + "=" + g.Count(); users = string.Join(",", u); break; } }
  outp.Add("T3|" + sp + "|" + (pick ?? "none") + "|" + (users ?? ""));
  if (pick != null) break; }
// T9*: top scene
var top = "Assets/Libs/Layer Lab/GUI PRO Kit - Simple Casual/Scene/DemoScene.unity";
var s2 = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(top, UnityEditor.SceneManagement.OpenSceneMode.Single);
var all = All(s2).ToList();
var builtin = new[]{ typeof(UnityEngine.AudioSource), typeof(UnityEngine.Light), typeof(UnityEngine.Collider), typeof(UnityEngine.Animator), typeof(UnityEngine.ParticleSystem), typeof(UnityEngine.Camera), typeof(UnityEngine.Canvas) };
var mbs = all.SelectMany(g => g.GetComponents<UnityEngine.MonoBehaviour>()).Where(c => c != null).Select(c => c.GetType()).Distinct().OrderBy(t => t.Name).ToList();
foreach (var ct in builtin.Concat(mbs)) {
  var hits = all.Where(g => Depth(g.transform) >= 6 && g.GetComponent(ct) != null && InactiveAncestor(g.transform)).ToList();
  if (hits.Count == 1) { var h = hits[0]; var anc = h.transform.parent; string hi = null; while (anc != null) { if (!anc.gameObject.activeSelf) hi = anc.name; anc = anc.parent; }
    outp.Add("T9|" + top + "|" + ct.Name + "|" + PathOf(h) + "|highest_inactive=" + hi + "|n_with_comp=" + all.Count(g => g.GetComponent(ct) != null) + "|total=" + all.Count); break; } }
return string.Join("\n", outp);
