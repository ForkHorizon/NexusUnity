// Asset impact resolution: UnityEvent calls, prefab-instance target resolution, method lookup, object paths and output shaping.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor.Commands
{
    /// <summary>A project C# script: its .meta guid and the type Unity binds to it (class named like the file).</summary>
    internal sealed class ScriptInfo
    {
        public string Guid;
        public string Path;
        public string ClassName;
        public string FullName;
        public string BaseName;
        public bool Partial;
        public string Source;
    }

    internal sealed partial class AssetImpactAnalyzer
    {
        // ---------------------------------------------------------------- resolution helpers

        internal sealed class UnityEventCall
        {
            public string Event;
            public int Index;
            public long TargetFileId;
            public string TypeName;
            public string Method;
            public int Line;
        }

        private static IEnumerable<UnityEventCall> Calls(YamlDoc doc)
        {
            const string suffix = ".m_PersistentCalls.m_Calls";
            foreach (var list in doc.Lists.Where(l => l.Key.EndsWith(suffix, StringComparison.Ordinal)).OrderBy(l => l.Key, StringComparer.Ordinal))
                for (int i = 0; i < list.Value.Count; i++)
                {
                    var c = list.Value[i];
                    yield return new UnityEventCall
                    {
                        Event = list.Key.Substring(0, list.Key.Length - suffix.Length),
                        Index = i,
                        TargetFileId = AssetYamlIndex.FileIdOf(Text(c, "m_Target")),
                        TypeName = Text(c, "m_TargetAssemblyTypeName"),
                        Method = Text(c, "m_MethodName"),
                        Line = c.TryGetValue("m_MethodName", out YamlValue v) ? v.Line : 0
                    };
                }
        }

        private static IEnumerable<Dictionary<string, YamlValue>> Curves(YamlDoc doc) =>
            doc.ClassId != 74 ? Enumerable.Empty<Dictionary<string, YamlValue>>()
                : doc.List("m_FloatCurves").Concat(doc.List("m_PPtrCurves"));

        private List<ScriptInfo> ResolveScripts(string type)
        {
            if (string.IsNullOrEmpty(type)) throw new ArgumentException("symbol.type is required.");
            return _index.Scripts.Values
                .Where(s => s.FullName == type || (type.IndexOf('.') < 0 && s.ClassName == type))
                .OrderBy(s => s.Path, StringComparer.Ordinal).ToList();
        }

        private ScriptInfo TargetScript(YamlAsset asset, long fileId)
        {
            if (fileId == 0 || !asset.Docs.TryGetValue(fileId, out YamlDoc target)) return null;
            string guid = AssetYamlIndex.GuidOf(target.Field("m_Script"));
            return guid != null && _index.Scripts.TryGetValue(guid, out ScriptInfo s) ? s : null;
        }

        /// <summary>Follows a modification target into its source prefab, through nested instances (Unity's fileID XOR rule).</summary>
        private (YamlAsset asset, YamlDoc doc) ResolveDoc(string guid, long fileId, int depth)
        {
            if (guid == null || depth > 8 || !_index.AssetsByGuid.TryGetValue(guid, out YamlAsset asset)) return (null, null);
            if (asset.Docs.TryGetValue(fileId, out YamlDoc doc))
            {
                if (!doc.Stripped) return (asset, doc);
                string corr = doc.Field("m_CorrespondingSourceObject");
                return ResolveDoc(AssetYamlIndex.GuidOf(corr), AssetYamlIndex.FileIdOf(corr), depth + 1);
            }
            foreach (YamlDoc pi in asset.Docs.Values.Where(d => d.ClassId == 1001))
            {
                var hit = ResolveDoc(AssetYamlIndex.GuidOf(pi.Field("m_SourcePrefab")), (fileId ^ pi.FileId) & FileIdMask, depth + 1);
                if (hit.doc != null) return hit;
            }
            return (null, null);
        }

        private bool? MethodExists(string fullName, string method, out string confidence)
        {
            confidence = "exact";
            Type loaded = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(fullName, false)).FirstOrDefault(t => t != null);
            if (loaded != null) return HasMethod(loaded, method);
            ScriptInfo script = _index.Scripts.Values.FirstOrDefault(s => s.FullName == fullName);
            for (int depth = 0; script != null && depth < 8; depth++)
            {
                if (Regex.IsMatch(script.Source, @"[\w>\]?]\s+" + Regex.Escape(method) + @"\s*(<[^>]*>)?\s*\(")) return true;
                if (script.Partial) confidence = "strong";
                if (script.BaseName != null && UnityBases.TryGetValue(script.BaseName, out Type unityBase)) return HasMethod(unityBase, method);
                ScriptInfo next = _index.Scripts.Values.FirstOrDefault(s => s.ClassName == script.BaseName);
                if (next == null) { confidence = "strong"; return false; }
                script = next;
            }
            return null;
        }

        private static bool HasMethod(Type type, string method)
        {
            for (Type t = type; t != null; t = t.BaseType)
                if (t.GetMethods(AllMembers).Any(m => m.Name == method)) return true;
            return false;
        }

        private static string FieldDefault(ScriptInfo script, string field)
        {
            Match m = Regex.Match(script.Source, @"(?m)^[^/\n(]*?\b([\w.<>\[\],]+)\s+" + Regex.Escape(field) + @"\s*(?:=\s*([^;]+))?;");
            if (!m.Success) return null;
            string type = m.Groups[1].Value;
            if (!m.Groups[2].Success)
                return type == "string" ? "" : new[] { "int", "float", "double", "long", "short", "byte", "uint", "bool" }.Contains(type) ? "0" : null;
            string init = m.Groups[2].Value.Trim();
            if (init == "true") return "1";
            if (init == "false") return "0";
            if (init.Length > 1 && init[0] == '"' && init[init.Length - 1] == '"') return init.Substring(1, init.Length - 2);
            string number = Regex.Replace(init, "[fFdDmMuUlL]+$", "");
            return double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out _) ? number : null;
        }

        // ---------------------------------------------------------------- paths

        private string GoPath(YamlAsset asset, long goId, int depth)
        {
            if (depth > 64 || !asset.Docs.TryGetValue(goId, out YamlDoc go)) return "";
            string name = go.Field("m_Name") ?? "";
            YamlDoc transform = asset.Docs.Values.FirstOrDefault(d => (d.ClassId == 4 || d.ClassId == 224) && AssetYamlIndex.FileIdOf(d.Field("m_GameObject")) == goId);
            long father = AssetYamlIndex.FileIdOf(transform?.Field("m_Father"));
            return father == 0 ? name : Prefix(TransformPath(asset, father, depth + 1)) + name;
        }

        private string TransformPath(YamlAsset asset, long transformId, int depth)
        {
            if (!asset.Docs.TryGetValue(transformId, out YamlDoc t)) return "";
            if (!t.Stripped) return GoPath(asset, AssetYamlIndex.FileIdOf(t.Field("m_GameObject")), depth);
            long pi = AssetYamlIndex.FileIdOf(t.Field("m_PrefabInstance"));
            return asset.Docs.TryGetValue(pi, out YamlDoc instance) ? InstancePath(asset, instance, depth) : "";
        }

        private string InstancePath(YamlAsset asset, YamlDoc instance, int depth)
        {
            string name = instance.List("m_Modification.m_Modifications")
                .Where(m => Text(m, "propertyPath") == "m_Name").Select(m => Text(m, "value")).FirstOrDefault();
            if (name == null)
            {
                var source = ResolveRoot(AssetYamlIndex.GuidOf(instance.Field("m_SourcePrefab")), depth);
                name = source.asset == null ? "" : GoPath(source.asset, source.goId, depth + 1);
            }
            long parent = AssetYamlIndex.FileIdOf(instance.Field("m_Modification.m_TransformParent"));
            return parent == 0 ? name : Prefix(TransformPath(asset, parent, depth + 1)) + name;
        }

        private (YamlAsset asset, long goId) ResolveRoot(string guid, int depth)
        {
            if (guid == null || depth > 8 || !_index.AssetsByGuid.TryGetValue(guid, out YamlAsset asset)) return (null, 0);
            YamlDoc root = asset.Docs.Values.FirstOrDefault(d => (d.ClassId == 4 || d.ClassId == 224) && !d.Stripped && AssetYamlIndex.FileIdOf(d.Field("m_Father")) == 0);
            return root != null ? (asset, AssetYamlIndex.FileIdOf(root.Field("m_GameObject"))) : (null, 0);
        }

        /// <summary>Path of a source-prefab object below its root, as <c>/Child/Grandchild</c>, or empty for the root.</summary>
        private string Relative(YamlAsset asset, YamlDoc doc)
        {
            string full = ObjectPath(asset, doc);
            int slash = full.IndexOf('/');
            return slash < 0 ? "" : full.Substring(slash);
        }

        private string ObjectPath(YamlAsset asset, YamlDoc doc)
        {
            if (doc.ClassId == 1001) return InstancePath(asset, doc, 0);
            long go = AssetYamlIndex.FileIdOf(doc.Field("m_GameObject"));
            if (doc.ClassId == 1) go = doc.FileId;
            return go != 0 ? GoPath(asset, go, 0) : doc.Field("m_Name") ?? "";
        }

        private static string Prefix(string path) => path.Length == 0 ? "" : path + "/";

        // ---------------------------------------------------------------- output

        private IEnumerable<YamlAsset> Scanned() => _index.Assets.Where(a => a.Status == "ok");

        private static IEnumerable<YamlDoc> Ordered(YamlAsset asset) => asset.Docs.Values.OrderBy(d => d.Line);

        private JObject Locate(YamlAsset asset, YamlDoc doc, string field, int line = 0)
        {
            if (field != null && doc.Fields.TryGetValue(field, out YamlValue v)) line = v.Line;
            if (line == 0) line = doc.Line;
            return new JObject
            {
                ["asset"] = asset.Path,
                ["file_id"] = doc.FileId.ToString(CultureInfo.InvariantCulture),
                ["class"] = doc.ClassName,
                ["object_path"] = ObjectPath(asset, doc),
                ["evidence"] = new JObject { ["line"] = line, ["yaml"] = asset.Lines[line - 1].Trim() }
            };
        }

        private JObject CallLocation(YamlAsset asset, YamlDoc doc, UnityEventCall call)
        {
            JObject entry = Locate(asset, doc, null, call.Line);
            entry["event"] = call.Event;
            entry["call_index"] = call.Index;
            entry["method"] = call.Method;
            entry["target_type"] = StripAssembly(call.TypeName);
            return entry;
        }

        private static JObject Value(JObject entry, string source, string value, string fallback, string confidence)
        {
            entry["source"] = source;
            entry["value"] = value == null ? JValue.CreateNull() : (JToken)value;
            entry["differs_from_default"] = value == null || fallback == null ? null : (JToken)!SameValue(value, fallback);
            entry["confidence"] = confidence;
            return entry;
        }

        /// <summary>What a finding is, what happens, and what to do about it.</summary>
        private readonly struct Advice
        {
            public readonly string Kind;
            public readonly string Consequence;
            public readonly string Check;
            public Advice(string kind, string consequence, string check) { Kind = kind; Consequence = consequence; Check = check; }
        }

        /// <summary>Copies a location into a finding. An <c>Error</c> without <c>exact</c> confidence is downgraded to <c>Warning</c>.</summary>
        private static JObject Finding(JObject at, string severity, string confidence, Advice advice)
        {
            var f = (JObject)at.DeepClone();
            f["kind"] = advice.Kind;
            f["severity"] = severity == "Error" && confidence != "exact" ? "Warning" : severity;
            f["confidence"] = confidence;
            f["consequence"] = advice.Consequence;
            f["recommended_check"] = advice.Check;
            return f;
        }

        private static bool SameValue(string a, string b)
        {
            if (double.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out double x) &&
                double.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out double y)) return x.Equals(y);
            return string.Equals(a, b, StringComparison.Ordinal);
        }

        private static JArray UniqueValues(JArray values)
        {
            var unique = new JArray();
            foreach (var g in values.Where(v => v["value"].Type != JTokenType.Null).GroupBy(v => (string)v["value"]).OrderBy(g => g.Key, StringComparer.Ordinal))
                unique.Add(new JObject
                {
                    ["value"] = g.Key,
                    ["count"] = g.Count(),
                    ["at"] = new JArray(g.Select(v => v["asset"] + (string.IsNullOrEmpty((string)v["object_path"]) ? "" : ":" + v["object_path"])))
                });
            return unique;
        }

        private static string Summary(string fallback, JArray values)
        {
            var overrides = values.Where(v => (bool?)v["differs_from_default"] != false && v["value"].Type != JTokenType.Null)
                .Select(v => System.IO.Path.GetFileName((string)v["asset"]) + "/" + v["object_path"] + "=" + v["value"]);
            return "default=" + (fallback ?? "unknown") + "; overrides: " + string.Join(", ", overrides);
        }

        private JObject Header(ImpactSymbol symbol, List<ScriptInfo> scripts)
        {
            var header = new JObject
            {
                ["symbol"] = JObject.FromObject(new { kind = symbol.Kind, type = symbol.Type, name = symbol.Name }),
                ["scripts"] = new JArray(scripts.Select(s => new JObject { ["guid"] = s.Guid, ["path"] = s.Path, ["type"] = s.FullName, ["confidence"] = "exact" })),
                ["warnings"] = new JArray()
            };
            if (scripts.Count == 0)
                ((JArray)header["warnings"]).Add(new JObject
                {
                    ["code"] = "SYMBOL_NOT_FOUND",
                    ["message"] = "No scanned .cs file declares " + symbol.Type + " (class name must match the file name). Built-in and DLL types are only matched through UnityEvent type names."
                });
            return header;
        }

        private JArray Cap(IEnumerable<JObject> items)
        {
            var sorted = items.OrderBy(i => (string)i["asset"], StringComparer.Ordinal)
                .ThenBy(i => long.Parse((string)i["file_id"], CultureInfo.InvariantCulture))
                .ThenBy(i => (int?)i["evidence"]?["line"] ?? 0).ToList();
            if (sorted.Count > _maxResults) { _truncated = true; sorted = sorted.Take(_maxResults).ToList(); }
            return new JArray(sorted);
        }

        private static JObject Counts(IEnumerable<JObject> findings) =>
            JObject.FromObject(findings.GroupBy(f => (string)f["severity"]).OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count()));

        private JObject Finish(JObject data)
        {
            data["skipped"] = new JArray(_index.Assets.Where(a => a.Status == "skipped").Select(a => new JObject { ["asset"] = a.Path, ["status"] = a.Status, ["reason"] = a.Reason }));
            data["unknown"] = new JArray(_index.Assets.Where(a => a.Status == "unknown").Select(a => new JObject { ["asset"] = a.Path, ["status"] = a.Status, ["reason"] = a.Reason }));
            data["complete"] = _index.Complete && !_truncated;
            data["truncated_reason"] = _index.TruncatedReason ?? (_truncated ? "max_results" : null);
            data["scanned"] = new JObject { ["files"] = _index.FilesRead, ["bytes"] = _index.BytesRead };
            return data;
        }

        private static string Text(Dictionary<string, YamlValue> item, string key) => item.TryGetValue(key, out YamlValue v) ? v.Text : "";

        private static string StripAssembly(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return typeName;
            int comma = typeName.IndexOf(',');
            return (comma < 0 ? typeName : typeName.Substring(0, comma)).Trim();
        }

        private static string Block(YamlAsset asset, int line)
        {
            int indent = asset.Lines[line - 1].Length - asset.Lines[line - 1].TrimStart(' ').Length;
            var parts = asset.Lines.Skip(line).TakeWhile(l => l.Length - l.TrimStart(' ').Length > indent || l.TrimStart().StartsWith("- ", StringComparison.Ordinal) && l.Length - l.TrimStart(' ').Length == indent)
                .Select(l => l.Trim()).Take(20);
            string block = string.Join(" ", parts);
            return block.Length > 300 ? block.Substring(0, 300) + "…" : block;
        }
    }
}
