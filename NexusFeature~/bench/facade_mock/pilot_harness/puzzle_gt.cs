var s = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Libs/Layer Lab/GUI PRO Kit - Simple Casual/Scene/DemoScene.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
var all = s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UnityEngine.Transform>(true)).ToList();
var hit = all.Where(t => t.GetComponent<UnityEngine.UI.RawImage>() != null).ToList();
var res = new System.Collections.Generic.List<string>();
foreach (var h in hit) { var p = h.name; var d = 1; var inact = new System.Collections.Generic.List<string>(); var t = h; if (!t.gameObject.activeSelf) inact.Add(t.name + "(self)"); while (t.parent != null) { t = t.parent; p = t.name + "/" + p; d++; if (!t.gameObject.activeSelf) inact.Add(t.name); }
  res.Add(p + " | depth=" + d + " | inactive=" + string.Join(",", inact) + " | rawimage_enabled=" + h.GetComponent<UnityEngine.UI.RawImage>().enabled); }
return string.Join("\n", res);
