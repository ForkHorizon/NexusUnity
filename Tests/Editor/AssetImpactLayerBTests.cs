// EditMode parity suite: Layer B (Editor truth) vs Layer A (static YAML) on the V3-10 corpus materialized into the project.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityMCP.Editor.Commands;
using Debug = UnityEngine.Debug;

namespace UnityMCP.Editor.Tests
{
    /// <summary>
    /// Copies <c>Tests/Fixtures~/AssetImpact/Assets</c> (with its .meta guids) into a temporary project folder, compiles the
    /// fixture scripts, compares Layer A and Layer B, then deletes the folder and recompiles. Domain reloads in between, so
    /// the outcome travels through <see cref="SessionState"/>.
    /// </summary>
    public class AssetImpactLayerBTests
    {
        private const string Root = "Assets/NexusLayerBParity";
        private const string ReportKey = "Nexus.LayerBParity.Report";
        private const int PerfCopies = 50;

        private static string FixtureAssets([CallerFilePath] string here = "") =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here), "..", "Fixtures~", "AssetImpact", "Assets"));

        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        [UnityTest]
        public IEnumerator LayerBAgreesWithLayerAOnMaterializedCorpus()
        {
            LogAssert.ignoreFailingMessages = true; // hand-written Broken/Malformed/Binary fixtures log import errors on purpose
            if (!AssetDatabase.IsValidFolder(Root))
            {
                CopyTree(FixtureAssets(), Path.Combine(ProjectRoot, Root));
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                yield return new RecompileScripts();
            }
            LogAssert.ignoreFailingMessages = true;
            string report;
            try { report = Compare().ToString(); }
            catch (Exception ex) { report = new JObject { ["exception"] = ex.ToString() }.ToString(); }
            SessionState.SetString(ReportKey, report);
            AssetDatabase.DeleteAsset(Root);
            yield return new RecompileScripts();

            JObject result = JObject.Parse(SessionState.GetString(ReportKey, "{}"));
            SessionState.EraseString(ReportKey);
            Debug.Log("[AssetImpact] layer_b_parity " + result.ToString(Newtonsoft.Json.Formatting.None));
            Assert.IsFalse(AssetDatabase.IsValidFolder(Root), "materialized corpus was not removed");
            Assert.IsNull(result["exception"], (string)result["exception"]);
            Assert.AreEqual(0, ((JArray)result["disagreements"]).Count, result["disagreements"].ToString());
            Assert.IsTrue((bool)result["assets_unchanged"], "Layer B changed a materialized asset");
            Assert.IsFalse((bool)result["scene_dirty_after"], "Layer B marked the loaded scene dirty");
            Assert.IsTrue((bool)result["unloaded_scene_reported"], "unloaded scene was not reported as " + AssetImpactEditorTruth.SceneNotLoaded);
            Assert.Greater((int)result["agree"], 0);
            Assert.Greater(((JArray)result["resolved_layer_a_unknown"]).Count, 0, "level=auto did not resolve the asset Layer A left unknown");
            Assert.AreEqual(PerfCopies, (int)result["perf"]["editor_values"], "Layer B missed values when paths excludes the .cs");
        }

        [Test]
        public void InvalidLevelIsInvalidArgument()
        {
            NexusCommandEnvelope envelope = AssetImpactCommand.ExecuteEnveloped(new AssetImpactCommand.Request { Op = "validate_asset_integrity", Level = "deep" });
            Assert.IsFalse(envelope.Ok);
            Assert.AreEqual(NexusCommandCodes.InvalidArgument, envelope.Error.Code);
        }

        [Test]
        public void AutoLevelSkipsEditorWhenStaticIsExact()
        {
            JObject data = Data(new AssetImpactCommand.Request { Op = "find_references", Kind = "type", Type = "NoSuchTypeAnywhere", Paths = "Assets/NexusUnity/Editor/Commands", Level = "auto" });
            Assert.IsNull(data["editor"], data.ToString());
        }

        // ---------------------------------------------------------------- comparison (runs while the corpus is materialized)

        private static JObject Compare()
        {
            var disagreements = new JArray();
            int agree = 0;
            string before = TreeHash(Path.Combine(ProjectRoot, Root));
            bool unloadedReported = false;
            JObject expected = JObject.Parse(File.ReadAllText(Path.Combine(FixtureAssets(), "..", "expected_findings.json")));
            foreach (JObject q in expected["references"])
            {
                JObject s = (JObject)q["symbol"];
                JObject data = Data(Request((string)s["kind"], (string)s["type"], (string)s["name"]));
                unloadedReported |= data["editor"]["unknown"].Any(u => (string)u["reason"] == AssetImpactEditorTruth.SceneNotLoaded);
                agree += Diff((string)q["id"], data, ExcludedFromEditor(data), disagreements);
            }
            JObject integrity = Data(new AssetImpactCommand.Request { Op = "validate_asset_integrity", Paths = Root, Level = "editor" });
            agree += DiffIntegrity(integrity, disagreements);
            JObject auto = Data(new AssetImpactCommand.Request { Op = "find_references", Kind = "field", Type = "Fixture.GameConfig", Name = "startingLives", Paths = Root, Level = "auto" });
            var resolved = new JArray(auto["editor"]["values"].Where(v => auto["unknown"].Any(u => (string)u["asset"] == (string)v["asset"])));

            Scene scene = EditorSceneManager.OpenScene(Root + "/Scenes/Main.unity", OpenSceneMode.Additive); // test setup, not Layer B
            bool dirty;
            try
            {
                JObject loaded = Data(Request("field", "Fixture.Health", "maxHealth"));
                agree += Diff("R1.loaded_scene", loaded, ExcludedFromEditor(loaded), disagreements);
                dirty = scene.isDirty;
            }
            finally { EditorSceneManager.CloseScene(scene, true); }

            return new JObject
            {
                ["agree"] = agree,
                ["disagreements"] = disagreements,
                ["assets_unchanged"] = before == TreeHash(Path.Combine(ProjectRoot, Root)),
                ["scene_dirty_after"] = dirty,
                ["unloaded_scene_reported"] = unloadedReported,
                ["resolved_layer_a_unknown"] = resolved,
                ["integrity_editor"] = integrity["editor"]["findings"],
                ["perf"] = Perf()
            };
        }

        private static AssetImpactCommand.Request Request(string kind, string type, string name) =>
            new AssetImpactCommand.Request { Op = "find_references", Kind = kind, Type = type, Name = name, Paths = Root, Level = "editor" };

        private static JObject Data(AssetImpactCommand.Request request)
        {
            NexusCommandEnvelope envelope = AssetImpactCommand.ExecuteEnveloped(request);
            Assert.IsTrue(envelope.Ok, envelope.ToJson().ToString());
            return (JObject)envelope.ToJson()["data"];
        }

        /// <summary>
        /// Assets either layer could not read are compared by neither side: Layer B <c>unknown</c> (unloaded scenes, unimportable
        /// fixtures) and Layer A <c>unknown</c> (unparsable YAML, which Layer B may still resolve; counted under <c>resolved_layer_a_unknown</c>).
        /// </summary>
        private static HashSet<string> ExcludedFromEditor(JObject data) =>
            new HashSet<string>(data["editor"]["unknown"].Concat(data["unknown"]).Select(u => (string)u["asset"]), StringComparer.Ordinal);

        private static int Diff(string id, JObject data, HashSet<string> excluded, JArray disagreements)
        {
            JObject editor = (JObject)data["editor"];
            string kind = (string)data["symbol"]["kind"];
            IEnumerable<JToken> a, b;
            Func<JToken, string> key;
            if (kind == "field")
            {
                a = data["values"];
                b = editor["values"].Where(v => (string)v["source"] != "inherited");
                key = v => v["asset"] + "|" + ((string)v["source"] == "animation_curve" ? "" : v["object_path"] + "|" + Normalize(v["value"])) + "|" + v["source"];
            }
            else if (kind == "method")
            {
                a = data["bindings"];
                b = editor["bindings"].Where(v => (string)v["source"] != "inherited");
                key = v => v["asset"] + "|" + v["event"] + "|" + v["call_index"];
            }
            else
            {
                a = data["direct"].Concat(data["via_prefab_instance"]);
                b = editor["direct"];
                key = v => (string)v["asset"];
            }
            var left = new HashSet<string>(a.Where(v => !excluded.Contains((string)v["asset"])).Select(key));
            var right = new HashSet<string>(b.Where(v => !excluded.Contains((string)v["asset"])).Select(key));
            foreach (string k in left.Except(right)) disagreements.Add(id + " layer_a_only " + k);
            foreach (string k in right.Except(left)) disagreements.Add(id + " layer_b_only " + k);
            return left.Intersect(right).Count();
        }

        private static int DiffIntegrity(JObject data, JArray disagreements)
        {
            HashSet<string> excluded = ExcludedFromEditor(data);
            var kinds = new[] { "missing_script", "broken_unityevent" };
            var left = new HashSet<string>(data["findings"].Where(f => kinds.Contains((string)f["kind"]) && (string)f["severity"] == "Error" && !excluded.Contains((string)f["asset"]))
                .Select(f => f["kind"] + "|" + f["asset"]));
            var right = new HashSet<string>(data["editor"]["findings"].Where(f => kinds.Contains((string)f["kind"]) && !excluded.Contains((string)f["asset"])).Select(f => f["kind"] + "|" + f["asset"]));
            foreach (string k in left.Except(right)) disagreements.Add("integrity layer_a_only " + k);
            foreach (string k in right.Except(left)) disagreements.Add("integrity layer_b_only " + k);
            return left.Intersect(right).Count();
        }

        private static string Normalize(JToken value)
        {
            string s = value?.Type == JTokenType.Null ? null : (string)value;
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d.ToString("R", CultureInfo.InvariantCulture) : s;
        }

        /// <summary>Layer A vs Layer B cost on <see cref="PerfCopies"/> prefab and ScriptableObject copies (frame-time impact of Layer B).</summary>
        private static JObject Perf()
        {
            string folder = Root + "/Perf";
            AssetDatabase.CreateFolder(Root, "Perf");
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < PerfCopies; i++)
                {
                    AssetDatabase.CopyAsset(Root + "/Prefabs/PlayerVariant.prefab", folder + "/P" + i + ".prefab");
                    AssetDatabase.CopyAsset(Root + "/Data/HardConfig.asset", folder + "/C" + i + ".asset");
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            var timings = new JObject { ["assets"] = PerfCopies * 2 };
            foreach (string level in new[] { "static", "editor", "editor" })
            {
                var sw = Stopwatch.StartNew();
                JObject data = Data(new AssetImpactCommand.Request { Op = "find_references", Kind = "field", Type = "Fixture.Health", Name = "maxHealth", Paths = folder, Level = level });
                string label = level == "static" ? "static_ms" : timings["editor_cold_ms"] == null ? "editor_cold_ms" : "editor_warm_ms";
                timings[label] = sw.ElapsedMilliseconds;
                if (level == "static") continue;
                timings[label.Replace("_ms", "_layer_b_ms")] = data["editor"]["scanned"]["ms"];
                timings["editor_values"] = data["editor"]["values"].Count();
            }
            return timings;
        }

        // ---------------------------------------------------------------- file helpers

        private static void CopyTree(string from, string to)
        {
            foreach (string dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(to, dir.Substring(from.Length + 1)));
            Directory.CreateDirectory(to);
            foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
                File.Copy(file, Path.Combine(to, file.Substring(from.Length + 1)), true);
        }

        private static string TreeHash(string root)
        {
            using (var sha = SHA256.Create())
            {
                foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories).Where(f => !f.Contains("/Perf")).OrderBy(f => f, StringComparer.Ordinal))
                {
                    byte[] body = File.ReadAllBytes(file);
                    sha.TransformBlock(body, 0, body.Length, null, 0);
                }
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return BitConverter.ToString(sha.Hash);
            }
        }
    }
}
