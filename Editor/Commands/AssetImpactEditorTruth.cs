// Layer B asset impact: read-only Editor ground truth via AssetDatabase, SerializedObject, PrefabUtility, UnityEventBase and AnimationUtility.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UnityMCP.Editor.Commands
{
    /// <summary>
    /// Layer B: what the Editor actually deserializes for the assets Layer A scanned. Loads assets through
    /// <see cref="AssetDatabase"/> and reads them with <see cref="SerializedObject"/>; never opens scenes, never writes,
    /// never marks anything dirty. Unloaded scenes are reported under <c>unknown</c>.
    /// </summary>
    internal static class AssetImpactEditorTruth
    {
        internal const string SceneNotLoaded = "scene_not_loaded_for_editor_validation";
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private const BindingFlags Methods = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly string[] Extensions = { ".prefab", ".asset", ".anim", ".unity" };

        /// <summary>One loaded object with the asset it came from and its hierarchy path.</summary>
        private struct Host
        {
            public string Asset;
            public string Path;
            public Object Obj;
        }

        /// <summary>
        /// Builds the <c>editor</c> section for a Layer A result. <paramref name="level"/> <c>editor</c> checks every candidate asset in
        /// <paramref name="scopes"/>; <c>auto</c> only assets Layer A left unknown or matched without exact confidence, and returns null
        /// when there are none.
        /// </summary>
        internal static JObject Evaluate(JObject layerA, AssetImpactCommand.Request request, IList<string> scopes)
        {
            var sw = Stopwatch.StartNew();
            var editor = new JObject { ["level"] = "editor", ["available"] = true, ["unknown"] = new JArray() };
            string busy = NotReadyReason();
            if (busy != null)
            {
                editor["available"] = false;
                editor["reason"] = busy;
                return editor;
            }
            List<string> paths = request.Level == "auto" ? AutoPaths(layerA) : Candidates(scopes);
            if (paths.Count == 0 && request.Level == "auto") return null;
            var unknown = (JArray)editor["unknown"];
            List<Host> hosts = Load(paths, unknown);
            Type type = string.IsNullOrEmpty(request.Type) ? null : ResolveType(layerA, request.Type);
            var results = new List<JObject>();
            string list;
            if (request.Op == "validate_asset_integrity") { list = "findings"; results.AddRange(Integrity(hosts)); }
            else if (type == null) { list = "values"; editor["warnings"] = new JArray("type_not_loaded: " + request.Type + " has no compiled MonoScript class"); }
            else
            {
                string kind = (string)layerA["symbol"]?["kind"];
                list = kind == "field" ? "values" : kind == "method" ? "bindings" : "direct";
                string name = (string)layerA["symbol"]?["name"];
                results.AddRange(kind == "field" ? FieldValues(hosts, type, name) : kind == "method" ? Bindings(hosts, type, name) : Usages(hosts, type));
            }
            bool truncated = results.Count > request.MaxResults;
            editor[list] = new JArray(results.Take(request.MaxResults));
            editor["complete"] = !truncated && unknown.Count == 0;
            editor["scanned"] = new JObject { ["assets"] = paths.Count, ["objects"] = hosts.Count, ["ms"] = sw.ElapsedMilliseconds };
            return editor;
        }

        // ---------------------------------------------------------------- gating and loading

        private static string NotReadyReason()
        {
            if (!UnityEditorInternal.InternalEditorUtility.CurrentThreadIsMainThread()) return "not_main_thread";
            if (EditorApplication.isCompiling) return "compiling";
            if (EditorApplication.isUpdating) return "importing";
            EditorReadiness.Snapshot snap = EditorReadiness.Published;
            return snap.IsCompiling || snap.Reason == EditorReadiness.ReasonDomainReload ? EditorReadiness.BusyReason(snap) : null;
        }

        private static List<string> Candidates(IList<string> scopes)
        {
            var paths = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string scope in scopes)
            {
                if (AssetDatabase.IsValidFolder(scope))
                    foreach (string guid in AssetDatabase.FindAssets("", new[] { scope }))
                        paths.Add(AssetDatabase.GUIDToAssetPath(guid));
                else paths.Add(scope);
            }
            return paths.Where(p => Extensions.Contains(Path.GetExtension(p).ToLowerInvariant())).ToList();
        }

        private static List<string> AutoPaths(JObject layerA)
        {
            var paths = new SortedSet<string>(StringComparer.Ordinal);
            foreach (JToken u in layerA["unknown"] ?? new JArray()) paths.Add((string)u["asset"]);
            foreach (string list in new[] { "values", "bindings", "direct", "via_prefab_instance", "findings" })
                foreach (JToken e in layerA[list] ?? new JArray())
                    if ((string)e["confidence"] != "exact") paths.Add((string)e["asset"]);
            return paths.Where(p => Extensions.Contains(Path.GetExtension(p).ToLowerInvariant())).ToList();
        }

        private static List<Host> Load(List<string> paths, JArray unknown)
        {
            var hosts = new List<Host>();
            foreach (string asset in paths)
            {
                if (asset.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                {
                    Scene scene = SceneManager.GetSceneByPath(asset);
                    if (!scene.IsValid() || !scene.isLoaded) { unknown.Add(Unknown(asset, SceneNotLoaded)); continue; }
                    foreach (GameObject root in scene.GetRootGameObjects()) AddTree(hosts, asset, root.transform);
                    continue;
                }
                Object main = AssetDatabase.LoadMainAssetAtPath(asset);
                if (main == null) { unknown.Add(Unknown(asset, "editor_load_failed")); continue; }
                if (main is GameObject go) { AddTree(hosts, asset, go.transform); continue; }
                // ponytail: main + sub-assets only; nested ScriptableObject graphs are reached through their own .asset paths.
                foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(asset).Where(o => o != null))
                    hosts.Add(new Host { Asset = asset, Path = o.name, Obj = o });
            }
            return hosts;
        }

        private static void AddTree(List<Host> hosts, string asset, Transform root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string path = t == root ? t.name : root.name + "/" + AnimationUtility.CalculateTransformPath(t, root);
                hosts.Add(new Host { Asset = asset, Path = path, Obj = t.gameObject });
                foreach (Component c in t.GetComponents<Component>())
                    if (c != null && !(c is Transform)) hosts.Add(new Host { Asset = asset, Path = path, Obj = c });
            }
        }

        /// <summary>The script Layer A resolved, or (when <c>paths</c> excludes the .cs) the loaded MonoBehaviour/ScriptableObject named <paramref name="type"/>.</summary>
        private static Type ResolveType(JObject layerA, string type)
        {
            foreach (JToken s in layerA["scripts"] ?? new JArray())
            {
                Type t = AssetDatabase.LoadAssetAtPath<MonoScript>((string)s["path"])?.GetClass();
                if (t != null) return t;
            }
            bool full = type.IndexOf('.') >= 0;
            return TypeCache.GetTypesDerivedFrom<MonoBehaviour>().Concat(TypeCache.GetTypesDerivedFrom<ScriptableObject>())
                .Where(t => full ? t.FullName == type : t.Name == type).OrderBy(t => t.FullName, StringComparer.Ordinal).FirstOrDefault();
        }

        // ---------------------------------------------------------------- collectors

        private static IEnumerable<JObject> FieldValues(List<Host> hosts, Type type, string field)
        {
            foreach (Host h in hosts.Where(h => h.Obj.GetType() == type))
                using (var so = new SerializedObject(h.Obj))
                {
                    SerializedProperty prop = so.FindProperty(field);
                    if (prop == null) continue;
                    JObject entry = Entry(h);
                    entry["source"] = Source(h.Obj, prop);
                    entry["value"] = Scalar(prop);
                    entry["property_type"] = prop.propertyType.ToString();
                    yield return entry;
                }
            foreach (Host h in hosts.Where(h => h.Obj is AnimationClip))
                foreach (EditorCurveBinding b in CurveBindings((AnimationClip)h.Obj))
                {
                    if (b.type != type || (b.propertyName != field && !b.propertyName.StartsWith(field + ".", StringComparison.Ordinal))) continue;
                    JObject entry = Entry(h);
                    entry["object_path"] = b.path;
                    entry["source"] = "animation_curve";
                    entry["value"] = null;
                    entry["property"] = b.propertyName;
                    entry["property_exists"] = FieldExists(b.type, b.propertyName);
                    yield return entry;
                }
        }

        private static IEnumerable<JObject> Bindings(List<Host> hosts, Type type, string method)
        {
            foreach (Host h in hosts)
            {
                List<Call> calls = Calls(h.Obj).Where(c => (method == null || c.Method == method) && c.Target != null && c.Target.GetType() == type).ToList();
                if (calls.Count == 0) continue;
                using (var so = new SerializedObject(h.Obj))
                    foreach (Call call in calls)
                    {
                        JObject entry = CallEntry(h, call);
                        entry["source"] = Source(h.Obj, so.FindProperty(call.Event + ".m_PersistentCalls.m_Calls.Array.data[" + call.Index + "].m_MethodName"));
                        yield return entry;
                    }
            }
        }

        /// <summary>
        /// <c>component</c> when the object is stored in this asset, <c>prefab_modification</c> when an instance overrides the property
        /// (the same data <see cref="PrefabUtility.GetPropertyModifications"/> lists), <c>inherited</c> when it comes unchanged from a source prefab.
        /// </summary>
        private static string Source(Object obj, SerializedProperty prop) =>
            !PrefabUtility.IsPartOfPrefabInstance(obj) ? "component" : prop != null && prop.prefabOverride ? "prefab_modification" : "inherited";

        private static IEnumerable<JObject> Usages(List<Host> hosts, Type type)
        {
            foreach (Host h in hosts.Where(h => h.Obj.GetType() == type)) yield return Entry(h);
            foreach (Host h in hosts.Where(h => h.Obj is AnimationClip && CurveBindings((AnimationClip)h.Obj).Any(b => b.type == type)))
                yield return Entry(h);
        }

        private static IEnumerable<JObject> Integrity(List<Host> hosts)
        {
            foreach (Host h in hosts)
            {
                if (h.Obj is GameObject go)
                {
                    int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                    if (missing > 0) yield return Issue(h, "missing_script", missing + " component(s) have no loadable script.");
                    continue;
                }
                if (h.Obj is AnimationClip clip)
                {
                    foreach (EditorCurveBinding b in CurveBindings(clip).Where(b => typeof(MonoBehaviour).IsAssignableFrom(b.type) && b.type != typeof(MonoBehaviour) && !FieldExists(b.type, b.propertyName)))
                        yield return Issue(h, "missing_animated_property", b.path + ": " + b.type.FullName + "." + b.propertyName + " is not a serialized field.");
                    continue;
                }
                if ((h.Obj is MonoBehaviour || h.Obj is ScriptableObject) && SerializationUtility.HasManagedReferencesWithMissingTypes(h.Obj))
                    yield return Issue(h, "serialize_reference_missing_type", "[SerializeReference] data names a type that no longer loads.");
                foreach (var call in Calls(h.Obj).Where(c => !string.IsNullOrEmpty(c.Method) && !c.MethodExists))
                {
                    JObject f = Issue(h, "broken_unityevent", call.Target == null
                        ? "Persistent call " + call.Event + "[" + call.Index + "] has no target object."
                        : "Persistent call " + call.Event + "[" + call.Index + "]: " + call.Target.GetType().FullName + "." + call.Method + " does not exist.");
                    f["event"] = call.Event;
                    f["call_index"] = call.Index;
                    f["method"] = call.Method;
                    yield return f;
                }
            }
        }

        // ---------------------------------------------------------------- UnityEvent and animation helpers

        private sealed class Call
        {
            public string Event;
            public int Index;
            public string Method;
            public Object Target;
            public bool MethodExists;
        }

        /// <summary>Persistent calls of every UnityEventBase field declared on the object's type chain (top-level fields only).</summary>
        private static IEnumerable<Call> Calls(Object obj)
        {
            if (!(obj is MonoBehaviour) && !(obj is ScriptableObject)) yield break;
            for (Type t = obj.GetType(); t != null && t != typeof(MonoBehaviour) && t != typeof(ScriptableObject); t = t.BaseType)
                foreach (FieldInfo f in t.GetFields(Fields).Where(f => typeof(UnityEventBase).IsAssignableFrom(f.FieldType)).OrderBy(f => f.Name, StringComparer.Ordinal))
                {
                    if (!(f.GetValue(obj) is UnityEventBase evt)) continue;
                    for (int i = 0; i < evt.GetPersistentEventCount(); i++)
                    {
                        Object target = evt.GetPersistentTarget(i);
                        string method = evt.GetPersistentMethodName(i);
                        yield return new Call
                        {
                            Event = f.Name, Index = i, Method = method, Target = target,
                            MethodExists = target != null && target.GetType().GetMethods(Methods).Any(m => m.Name == method)
                        };
                    }
                }
        }

        private static IEnumerable<EditorCurveBinding> CurveBindings(AnimationClip clip) =>
            AnimationUtility.GetCurveBindings(clip).Concat(AnimationUtility.GetObjectReferenceCurveBindings(clip));

        /// <summary>True when <paramref name="property"/> (or its first segment, e.g. <c>pos.x</c>) is a serialized field of <paramref name="type"/>.</summary>
        private static bool FieldExists(Type type, string property)
        {
            string root = property.Split('.')[0];
            for (Type t = type; t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(root, Fields);
                if (f != null && (f.IsPublic || f.IsDefined(typeof(SerializeField), false))) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- output

        private static JObject Entry(Host h) => new JObject
        {
            ["asset"] = h.Asset,
            ["object_path"] = h.Path,
            ["class"] = h.Obj.GetType().Name,
            ["confidence"] = "exact"
        };

        private static JObject CallEntry(Host h, Call call)
        {
            JObject entry = Entry(h);
            entry["event"] = call.Event;
            entry["call_index"] = call.Index;
            entry["method"] = call.Method;
            entry["target_type"] = call.Target.GetType().FullName;
            entry["method_exists"] = call.MethodExists;
            return entry;
        }

        private static JObject Issue(Host h, string kind, string detail)
        {
            JObject f = Entry(h);
            f["kind"] = kind;
            f["severity"] = "Error";
            f["detail"] = detail;
            return f;
        }

        private static JObject Unknown(string asset, string reason) => new JObject { ["asset"] = asset, ["reason"] = reason };

        /// <summary>Value as Force Text YAML writes it (bool 0/1, invariant numbers); null for compound kinds.</summary>
        private static JToken Scalar(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.LayerMask:
                    return prop.longValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Boolean: return prop.boolValue ? "1" : "0";
                case SerializedPropertyType.Float: return prop.floatValue.ToString("R", CultureInfo.InvariantCulture);
                case SerializedPropertyType.String: return prop.stringValue;
                case SerializedPropertyType.ManagedReference: return prop.managedReferenceFullTypename;
                case SerializedPropertyType.ObjectReference:
                    Object o = prop.objectReferenceValue;
                    return o == null ? null : AssetDatabase.GetAssetPath(o) is string p && p.Length > 0 ? p + ":" + o.name : o.name;
                default: return null; // ponytail: structs/arrays are reported by property_type only; serialize them when an agent needs it
            }
        }
    }
}
