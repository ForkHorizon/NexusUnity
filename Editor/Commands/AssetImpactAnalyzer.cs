// Read-only Layer A asset impact: symbol references with stored values, change impact, and static integrity findings.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor.Commands
{
    /// <summary>The C# member an agent is about to change.</summary>
    internal sealed class ImpactSymbol
    {
        public string Kind; // type | field | method
        public string Type;
        public string Name;
        public string NewName;
    }

    /// <summary>
    /// Static (Layer A) asset impact over an <see cref="AssetYamlIndex"/>. Never writes. <c>Error</c> is only emitted with
    /// <c>exact</c> confidence; name-only matches are <c>heuristic</c> at most.
    /// </summary>
    internal sealed partial class AssetImpactAnalyzer
    {
        private const long FileIdMask = 0x7FFFFFFFFFFFFFFF;
        private const BindingFlags AllMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private static readonly Dictionary<string, Type> UnityBases = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["MonoBehaviour"] = typeof(UnityEngine.MonoBehaviour),
            ["ScriptableObject"] = typeof(UnityEngine.ScriptableObject),
            ["Behaviour"] = typeof(UnityEngine.Behaviour),
            ["Component"] = typeof(UnityEngine.Component),
            ["StateMachineBehaviour"] = typeof(UnityEngine.StateMachineBehaviour)
        };

        private readonly AssetYamlIndex _index;
        private readonly int _maxResults;
        private bool _truncated;

        /// <summary>Wraps a built index. <paramref name="maxResults"/> caps every returned list.</summary>
        public AssetImpactAnalyzer(AssetYamlIndex index, int maxResults)
        {
            _index = index;
            _maxResults = maxResults;
        }

        // ---------------------------------------------------------------- find_references

        /// <summary>Usages of a type, stored values of a field, or UnityEvent bindings of a method. Read-only.</summary>
        public JObject FindReferences(ImpactSymbol symbol)
        {
            List<ScriptInfo> scripts = ResolveScripts(symbol.Type);
            var data = Header(symbol, scripts);
            if (symbol.Kind == "field")
            {
                string fallback = scripts.Select(s => FieldDefault(s, symbol.Name)).FirstOrDefault(d => d != null);
                JArray values = Cap(FieldValues(scripts, symbol.Name, fallback));
                data["csharp_default"] = fallback;
                data["values"] = values;
                data["unique_values"] = UniqueValues(values);
                data["summary"] = Summary(fallback, values);
            }
            else if (symbol.Kind == "method")
            {
                data["bindings"] = Cap(Bindings(scripts, symbol));
            }
            else
            {
                data["direct"] = Cap(TypeUsages(scripts));
                data["via_prefab_instance"] = Cap(PrefabInstanceUsages(scripts));
            }
            return Finish(data);
        }

        // ---------------------------------------------------------------- analyze_change_impact

        /// <summary>Advisory findings for a planned C# change. Throws <see cref="ArgumentException"/> for an unknown op.</summary>
        public JObject AnalyzeChange(string op, ImpactSymbol symbol)
        {
            List<ScriptInfo> scripts = ResolveScripts(symbol.Type);
            var findings = new List<JObject>();
            switch (op)
            {
                case "rename_field":
                case "change_field_type":
                    string fallback = scripts.Select(s => FieldDefault(s, symbol.Name)).FirstOrDefault(d => d != null);
                    foreach (JObject v in FieldValues(scripts, symbol.Name, fallback)) findings.Add(FieldImpact(op, symbol, v, fallback));
                    break;
                case "rename_method":
                    foreach (JObject b in Bindings(scripts, symbol))
                        findings.Add(Finding(b, "Error", (string)b["confidence"], new Advice("broken_unityevent",
                            "Persistent call " + b["event"] + "[" + b["call_index"] + "] still names '" + symbol.Name + "'; after the rename Unity cannot find the method and the call silently stops firing.",
                            "Rename m_MethodName in these assets in the same change, or keep a forwarding method named " + symbol.Name + ".")));
                    break;
                case "delete_script":
                case "move_type":
                    findings.AddRange(TypeImpact(op, scripts, symbol));
                    break;
                default:
                    throw new ArgumentException("op must be rename_field, change_field_type, rename_method, delete_script or move_type.");
            }
            var data = Header(symbol, scripts);
            data["op"] = op;
            data["findings"] = Cap(findings);
            data["counts"] = Counts(findings);
            return Finish(data);
        }

        private JObject FieldImpact(string op, ImpactSymbol symbol, JObject v, string fallback)
        {
            bool curve = (string)v["source"] == "animation_curve";
            bool? differs = (bool?)v["differs_from_default"];
            string severity = curve || differs != false ? "Warning" : "Info";
            string confidence = curve ? "strong" : differs == null ? "heuristic" : "exact";
            string consequence;
            string check;
            if (curve)
            {
                consequence = "Animation curve binds '" + symbol.Name + "' by name; [FormerlySerializedAs] does not migrate curve bindings, so the curve goes missing.";
                check = "Re-bind the curve in the Animation window or edit the clip's attribute after the change.";
            }
            else if (op == "rename_field")
            {
                consequence = differs == false
                    ? "Stored value equals the C# default (" + fallback + "); nothing is lost on rename."
                    : "Stored value " + v["value"] + " resets to " + (fallback ?? "the C# default") + " on next load unless the old name is kept.";
                check = "[FormerlySerializedAs(\"" + symbol.Name + "\")]";
            }
            else
            {
                consequence = "Stored value " + v["value"] + " must convert to the new type; incompatible values reset to default.";
                check = "Keep a compatible type or migrate with ISerializationCallbackReceiver.";
            }
            return Finding(v, severity, confidence, new Advice(op, consequence, check));
        }

        private IEnumerable<JObject> TypeImpact(string op, List<ScriptInfo> scripts, ImpactSymbol symbol)
        {
            bool delete = op == "delete_script";
            foreach (JObject u in TypeUsages(scripts))
            {
                bool anim = u["asset"].ToString().EndsWith(".anim", StringComparison.OrdinalIgnoreCase);
                if (delete)
                    yield return Finding(u, anim ? "Warning" : "Error", "exact", new Advice("missing_script",
                        anim ? "Clip animates a field of this script; the binding goes missing." : "Stored instance becomes a Missing Script and its serialized values are lost.",
                        "Remove or replace the component in these assets before deleting the script."));
                else
                    yield return Finding(u, "Info", "exact", new Advice("move_type",
                        "Bound by script guid; keeps working if the .cs moves together with its .meta and the class still matches the file name.",
                        "Move the .meta with the .cs; rename the file if the class is renamed."));
            }
            if (delete)
                foreach (JObject pi in PrefabInstanceUsages(scripts))
                    yield return Finding(pi, "Warning", "strong", new Advice("missing_script",
                        "Prefab instance inherits the component from " + pi["source_prefab"] + " and shows a Missing Script.", "Fix the source prefab first."));
            foreach (JObject b in Bindings(scripts, new ImpactSymbol { Kind = "method", Type = symbol.Type, Name = null }))
                yield return Finding(b, delete ? "Error" : "Warning", delete ? (string)b["confidence"] : "strong", new Advice("broken_unityevent",
                    "Persistent call " + b["event"] + "[" + b["call_index"] + "] targets " + b["method"] + " on this type by assembly-qualified name.",
                    delete ? "Remove the persistent call." : "Add [MovedFrom] or re-pick the method in the inspector."));
            if (!delete)
                foreach (JObject r in SerializeReferenceUsages(scripts, symbol.Type))
                    yield return Finding(r, "Error", "exact", new Advice("serialize_reference",
                        "[SerializeReference] data stores the class/ns/asm string; moving the type orphans the managed reference.",
                        "[UnityEngine.Scripting.APIUpdating.MovedFrom(...)] on the moved type."));
        }

        // ---------------------------------------------------------------- validate_asset_integrity

        /// <summary>Missing Scripts, dangling guids and broken UnityEvent bindings; optionally only assets using the given script guids.</summary>
        public JObject ValidateIntegrity(ISet<string> changedScriptGuids)
        {
            var findings = new List<JObject>();
            foreach (YamlAsset asset in Scanned())
            {
                if (changedScriptGuids != null && !asset.Docs.Values.Any(d => d.GuidRefs.Any(g => changedScriptGuids.Contains(g.Text))))
                    continue;
                foreach (YamlDoc doc in Ordered(asset))
                {
                    MissingScript(asset, doc, findings);
                    DanglingGuids(asset, doc, findings);
                    foreach (var call in Calls(doc)) BrokenCall(asset, doc, call, findings);
                }
            }
            var data = new JObject { ["level"] = "static", ["findings"] = Cap(findings), ["counts"] = Counts(findings), ["warnings"] = new JArray() };
            return Finish(data);
        }

        private void MissingScript(YamlAsset asset, YamlDoc doc, List<JObject> findings)
        {
            if (doc.ClassId != 114 || doc.Stripped) return;
            string script = doc.Field("m_Script");
            if (script == null) return;
            string guid = AssetYamlIndex.GuidOf(script);
            if (guid == null && AssetYamlIndex.FileIdOf(script) == 0)
                findings.Add(Finding(Locate(asset, doc, "m_Script"), "Error", "exact", new Advice("missing_script", "m_Script is {fileID: 0}: the component shows as Missing Script and its data cannot load.", "Re-assign or remove the component.")));
            else if (guid != null && !_index.KnowsGuid(guid))
                findings.Add(Finding(Locate(asset, doc, "m_Script"), "Error", "exact", new Advice("missing_script", "Script guid " + guid + " has no .meta in the project: Missing Script.", "Restore the script with its original .meta, or remove the component.")));
        }

        private void DanglingGuids(YamlAsset asset, YamlDoc doc, List<JObject> findings)
        {
            int scriptLine = doc.Fields.TryGetValue("m_Script", out YamlValue s) ? s.Line : -1;
            foreach (YamlValue g in doc.GuidRefs.Where(g => g.Line != scriptLine && !_index.KnowsGuid(g.Text)).GroupBy(g => g.Text).Select(x => x.First()))
                findings.Add(Finding(Locate(asset, doc, null, g.Line), "Error", "exact", new Advice("dangling_guid",
                    "References guid " + g.Text + " that no .meta in the project declares; Unity loads it as None/Missing.", "Restore the asset with its .meta or re-assign the reference.")));
        }

        private void BrokenCall(YamlAsset asset, YamlDoc doc, UnityEventCall call, List<JObject> findings)
        {
            if (string.IsNullOrEmpty(call.Method)) return;
            string typeName = StripAssembly(call.TypeName);
            bool? exists;
            string confidence;
            if (!string.IsNullOrEmpty(typeName))
            {
                exists = MethodExists(typeName, call.Method, out confidence);
                if (exists == null)
                {
                    string why = call.TargetFileId == 0 ? ", target null" : "";
                    findings.Add(Finding(CallLocation(asset, doc, call), "Warning", call.TargetFileId == 0 ? "strong" : "heuristic", new Advice("broken_unityevent",
                        "Persistent call " + call.Event + "[" + call.Index + "]: type " + typeName + " not found" + why + "; the call cannot run.", "Re-pick the target method in the inspector or remove the call.")));
                    return;
                }
            }
            else
            {
                ScriptInfo target = TargetScript(asset, call.TargetFileId);
                if (target == null) return;
                exists = MethodExists(target.FullName, call.Method, out _);
                confidence = "strong";
                typeName = target.FullName;
            }
            if (exists == false)
                findings.Add(Finding(CallLocation(asset, doc, call), confidence == "exact" ? "Error" : "Warning", confidence, new Advice("broken_unityevent",
                    "Persistent call " + call.Event + "[" + call.Index + "]: " + typeName + "." + call.Method + " does not exist; the call silently never fires.",
                    "Restore the method, or re-pick the target in the inspector.")));
        }

        // ---------------------------------------------------------------- collectors

        private IEnumerable<JObject> FieldValues(List<ScriptInfo> scripts, string field, string fallback)
        {
            var guids = new HashSet<string>(scripts.Select(s => s.Guid), StringComparer.OrdinalIgnoreCase);
            foreach (YamlAsset asset in Scanned())
            foreach (YamlDoc doc in Ordered(asset))
            {
                if (doc.ClassId == 114 && !doc.Stripped && guids.Contains(AssetYamlIndex.GuidOf(doc.Field("m_Script")) ?? ""))
                {
                    if (!doc.Fields.TryGetValue(field, out YamlValue v)) continue;
                    string value = v.Text.Length > 0 ? v.Text : Block(asset, v.Line);
                    yield return Value(Locate(asset, doc, null, v.Line), "component", value, fallback, "exact");
                }
                foreach (var mod in doc.List("m_Modification.m_Modifications"))
                {
                    string path = Text(mod, "propertyPath");
                    if (path != field && !path.StartsWith(field + ".", StringComparison.Ordinal)) continue;
                    string target = Text(mod, "target");
                    var resolved = ResolveDoc(AssetYamlIndex.GuidOf(target), AssetYamlIndex.FileIdOf(target), 0);
                    if (resolved.doc == null || !guids.Contains(AssetYamlIndex.GuidOf(resolved.doc.Field("m_Script")) ?? "")) continue;
                    string value = Text(mod, "value");
                    if (value.Length == 0 && AssetYamlIndex.FileIdOf(Text(mod, "objectReference")) != 0) value = Text(mod, "objectReference");
                    JObject entry = Locate(asset, doc, null, mod["propertyPath"].Line);
                    entry["object_path"] = InstancePath(asset, doc, 0) + Relative(resolved.asset, resolved.doc);
                    if (path != field) entry["property_path"] = path;
                    yield return Value(entry, "prefab_modification", value, path == field ? fallback : null, "exact");
                }
                foreach (var curve in Curves(doc))
                {
                    if (Text(curve, "attribute") != field || !guids.Contains(AssetYamlIndex.GuidOf(Text(curve, "script")) ?? "")) continue;
                    JObject entry = Locate(asset, doc, null, curve["attribute"].Line);
                    entry["object_path"] = Text(curve, "path");
                    yield return Value(entry, "animation_curve", null, null, "strong");
                }
            }
        }

        private IEnumerable<JObject> Bindings(List<ScriptInfo> scripts, ImpactSymbol symbol)
        {
            var names = new HashSet<string>(scripts.Select(s => s.FullName), StringComparer.Ordinal) { symbol.Type };
            var guids = new HashSet<string>(scripts.Select(s => s.Guid), StringComparer.OrdinalIgnoreCase);
            foreach (YamlAsset asset in Scanned())
            foreach (YamlDoc doc in Ordered(asset))
            foreach (UnityEventCall call in Calls(doc))
            {
                if (symbol.Name != null && call.Method != symbol.Name) continue;
                string typeName = StripAssembly(call.TypeName);
                string confidence;
                if (!string.IsNullOrEmpty(typeName)) { if (!names.Contains(typeName)) continue; confidence = "exact"; }
                else
                {
                    ScriptInfo target = TargetScript(asset, call.TargetFileId);
                    if (target == null || !guids.Contains(target.Guid)) continue;
                    confidence = "strong";
                }
                JObject entry = CallLocation(asset, doc, call);
                entry["confidence"] = confidence;
                yield return entry;
            }
        }

        private IEnumerable<JObject> TypeUsages(List<ScriptInfo> scripts)
        {
            var guids = new HashSet<string>(scripts.Select(s => s.Guid), StringComparer.OrdinalIgnoreCase);
            foreach (YamlAsset asset in Scanned())
            foreach (YamlDoc doc in Ordered(asset))
            {
                bool component = doc.ClassId == 114 && !doc.Stripped && guids.Contains(AssetYamlIndex.GuidOf(doc.Field("m_Script")) ?? "");
                YamlValue? curve = Curves(doc).Where(c => guids.Contains(AssetYamlIndex.GuidOf(Text(c, "script")) ?? "")).Select(c => (YamlValue?)c["script"]).FirstOrDefault();
                if (!component && curve == null) continue;
                JObject entry = component ? Locate(asset, doc, "m_Script") : Locate(asset, doc, null, curve.Value.Line);
                entry["confidence"] = "exact";
                yield return entry;
            }
        }

        private IEnumerable<JObject> PrefabInstanceUsages(List<ScriptInfo> scripts)
        {
            var guids = new HashSet<string>(scripts.Select(s => s.Guid), StringComparer.OrdinalIgnoreCase);
            var containing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool grew = true;
            while (grew)
            {
                grew = false;
                foreach (YamlAsset a in Scanned().Where(a => a.Guid != null && !containing.Contains(a.Guid)))
                {
                    if (!a.Docs.Values.Any(d => (d.ClassId == 114 && guids.Contains(AssetYamlIndex.GuidOf(d.Field("m_Script")) ?? ""))
                                             || (d.ClassId == 1001 && containing.Contains(AssetYamlIndex.GuidOf(d.Field("m_SourcePrefab")) ?? ""))))
                        continue;
                    containing.Add(a.Guid);
                    grew = true;
                }
            }
            foreach (YamlAsset asset in Scanned())
            foreach (YamlDoc doc in Ordered(asset).Where(d => d.ClassId == 1001))
            {
                string source = AssetYamlIndex.GuidOf(doc.Field("m_SourcePrefab"));
                if (source == null || !containing.Contains(source)) continue;
                JObject entry = Locate(asset, doc, "m_SourcePrefab");
                entry["source_prefab"] = _index.GuidToPath.TryGetValue(source, out string p) ? p : source;
                entry["confidence"] = "strong";
                yield return entry;
            }
        }

        private IEnumerable<JObject> SerializeReferenceUsages(List<ScriptInfo> scripts, string type)
        {
            string full = scripts.Select(s => s.FullName).FirstOrDefault() ?? type;
            foreach (YamlAsset asset in Scanned())
            foreach (YamlDoc doc in Ordered(asset))
            foreach (var r in doc.List("references.RefIds"))
            {
                Match m = Regex.Match(Text(r, "type"), @"class: ([^,]*), ns: ([^,]*),");
                if (!m.Success) continue;
                string ns = m.Groups[2].Value.Trim();
                string name = (ns.Length > 0 ? ns + "." : "") + m.Groups[1].Value.Trim();
                if (name == full) yield return Locate(asset, doc, null, r["type"].Line);
            }
        }
    }
}
