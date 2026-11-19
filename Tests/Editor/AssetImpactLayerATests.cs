// EditMode tests for nexus.asset_impact Layer A against the frozen V3-10 AssetImpact corpus.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.Commands;

namespace UnityMCP.Editor.Tests
{
    public class AssetImpactLayerATests
    {
        private const string ExpectedSha256 = "4675bba6510c5945fc2c8a0868eaf90d7b0727c7578366c2bf76f090f18c71e7";
        private static readonly string[] Scope = { "Assets" };

        private static string FixtureRoot([CallerFilePath] string here = "") =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here), "..", "Fixtures~", "AssetImpact"));

        private static JObject Expected => JObject.Parse(File.ReadAllText(Path.Combine(FixtureRoot(), "expected_findings.json")));

        private static AssetImpactCommand.Request Q(string op, string kind = null, string type = null, string name = null, string change = null) =>
            new AssetImpactCommand.Request { Op = op, Kind = kind, Type = type, Name = name, Change = change };

        private static AssetImpactCommand.Request Max(AssetImpactCommand.Request request, int max)
        {
            request.MaxResults = max;
            return request;
        }

        private static JObject Run(AssetImpactCommand.Request request, string root = null, ScanBudget budget = null) =>
            AssetImpactCommand.Run(request, root ?? FixtureRoot(), Scope, null, budget);

        [Test]
        public void GroundTruthIsFrozen()
        {
            using (var sha = SHA256.Create())
            {
                string hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(FixtureRoot(), "expected_findings.json"))))
                    .Replace("-", "").ToLowerInvariant();
                Assert.AreEqual(ExpectedSha256, hash, "expected_findings.json changed; V3-10 ground truth is frozen.");
            }
        }

        [Test]
        public void FindReferencesMatchesGroundTruth()
        {
            int tp = 0, fp = 0, fn = 0;
            foreach (JObject q in Expected["references"])
            {
                JObject s = (JObject)q["symbol"];
                JObject data = Run(Q("find_references", (string)s["kind"], (string)s["type"], (string)s["name"]));
                foreach (string list in new[] { "values", "bindings", "direct", "via_prefab_instance" })
                {
                    if (q[list] == null) continue;
                    var expected = q[list].Select(e => Key(e, list)).ToList();
                    var actual = data[list].Select(e => Key(e, list)).ToList();
                    tp += expected.Intersect(actual).Count();
                    fn += expected.Except(actual).Count();
                    fp += actual.Except(expected).Count();
                    CollectionAssert.AreEquivalent(expected, actual, q["id"] + "." + list);
                }
                if (q["csharp_default"] != null) Assert.AreEqual((string)q["csharp_default"], (string)data["csharp_default"], (string)q["id"]);
                if (q["unique_values"] is JObject unique)
                    CollectionAssert.AreEquivalent(unique.Properties().Select(p => p.Name + "x" + p.Value),
                        data["unique_values"].Select(u => u["value"] + "x" + u["count"]), q["id"] + ".unique_values");
                Assert.IsTrue((bool)data["complete"], (string)q["id"]);
            }
            Debug.Log($"[AssetImpact] find_references precision={Ratio(tp, tp + fp)} recall={Ratio(tp, tp + fn)} tp={tp} fp={fp} fn={fn}");
        }

        [Test]
        public void IntegrityMatchesGroundTruth()
        {
            JObject data = Run(Q("validate_asset_integrity"));
            var expected = Expected["integrity"].Select(f => $"{f["kind"]}|{f["asset"]}|{f["file_id"]}|{f["severity"]}|{f["confidence"]}").ToList();
            var actual = data["findings"].Select(f => $"{f["kind"]}|{f["asset"]}|{f["file_id"]}|{f["severity"]}|{f["confidence"]}").ToList();
            CollectionAssert.AreEquivalent(expected, actual);
            CollectionAssert.AreEquivalent(Expected["integrity_skipped"].Select(a => (string)a["asset"]), data["skipped"].Select(a => (string)a["asset"]));
            CollectionAssert.AreEquivalent(Expected["integrity_unknown"].Select(a => (string)a["asset"]), data["unknown"].Select(a => (string)a["asset"]));
            StringAssert.StartsWith("YAML_PARSE_ERROR", (string)data["unknown"][0]["reason"]);
            Assert.AreEqual("UNSUPPORTED_SERIALIZATION", (string)data["skipped"][0]["reason"]);
            Debug.Log($"[AssetImpact] integrity expected={expected.Count} actual={actual.Count} missing={string.Join(";", expected.Except(actual))} extra={string.Join(";", actual.Except(expected))}");
        }

        [Test]
        public void ChangeImpactMatchesGroundTruth()
        {
            foreach (JObject q in Expected["impact"])
            {
                JObject s = (JObject)q["symbol"];
                JObject data = Run(Q("analyze_change_impact", null, (string)s["type"], (string)s["name"], (string)q["op"]));
                JArray findings = (JArray)data["findings"];
                if (q["assets_losing_values"] is JArray losing)
                    CollectionAssert.AreEquivalent(losing.Select(a => (string)a),
                        findings.Where(f => (string)f["severity"] == "Warning" && (string)f["confidence"] == "exact").Select(f => (string)f["asset"]).Distinct());
                if (q["assets"] is JArray assets)
                    CollectionAssert.AreEquivalent(assets.Select(a => (string)a),
                        findings.Where(f => (string)f["severity"] == "Error").Select(f => (string)f["asset"]).Distinct(), (string)q["id"]);
                if ((string)q["op"] == "rename_field")
                {
                    Assert.IsTrue(findings.Any(f => (string)f["source"] == "animation_curve" && (string)f["confidence"] == "strong"), "anim binding");
                    Assert.IsTrue(findings.Where(f => (string)f["severity"] == "Warning" && (string)f["source"] != "animation_curve")
                        .All(f => ((string)f["recommended_check"]).Contains("FormerlySerializedAs(\"maxHealth\")")));
                }
                if (q["unknown"] is JArray unknown)
                    CollectionAssert.AreEquivalent(unknown.Select(a => (string)a), data["unknown"].Select(a => (string)a["asset"]));
            }
        }

        [Test]
        public void ErrorSeverityAlwaysHasExactConfidence()
        {
            var outputs = new List<JObject> { Run(Q("validate_asset_integrity")) };
            foreach (string change in new[] { "rename_field", "change_field_type" })
                outputs.Add(Run(Q("analyze_change_impact", null, "Fixture.Health", "maxHealth", change)));
            foreach (string change in new[] { "delete_script", "move_type" })
                foreach (string type in new[] { "Fixture.Health", "Fixture.Spawner", "Fixture.GameConfig", "Legacy.Gone" })
                    outputs.Add(Run(Q("analyze_change_impact", null, type, null, change)));
            outputs.Add(Run(Q("analyze_change_impact", null, "Spawner", "Spawn", "rename_method")));
            var findings = outputs.SelectMany(o => o["findings"]).ToList();
            Assert.Greater(findings.Count(f => (string)f["severity"] == "Error"), 0);
            foreach (JToken f in findings.Where(f => (string)f["severity"] == "Error"))
                Assert.AreEqual("exact", (string)f["confidence"], f.ToString());
        }

        [Test]
        public void ScanIsReadOnlyAndDeterministic()
        {
            string before = TreeHash(FixtureRoot());
            string first = Run(Q("validate_asset_integrity")).ToString();
            Run(Q("find_references", "field", "Fixture.Health", "maxHealth"));
            Run(Q("analyze_change_impact", null, "Fixture.GameConfig", null, "delete_script"));
            string second = Run(Q("validate_asset_integrity")).ToString();
            Assert.AreEqual(before, TreeHash(FixtureRoot()), "fixture tree changed");
            Assert.AreEqual(Strip(first), Strip(second), "output order or content is not deterministic");
        }

        [Test]
        public void BudgetsTruncateWithCompleteFalse()
        {
            JObject files = Run(Q("validate_asset_integrity"), budget: new ScanBudget { MaxFiles = 2 });
            Assert.IsFalse((bool)files["complete"]);
            Assert.AreEqual("max_files", (string)files["truncated_reason"]);
            JObject results = Run(Max(Q("find_references", "field", "Fixture.Health", "maxHealth"), 1));
            Assert.IsFalse((bool)results["complete"]);
            Assert.AreEqual("max_results", (string)results["truncated_reason"]);
            Assert.AreEqual(1, ((JArray)results["values"]).Count);
        }

        [Test]
        public void SymlinkedFoldersAreNotFollowed()
        {
            string temp = Path.Combine(Path.GetTempPath(), "nexus-asset-impact-" + Guid.NewGuid().ToString("N"));
            string outside = temp + "-outside";
            try
            {
                Directory.CreateDirectory(Path.Combine(temp, "Assets"));
                Directory.CreateDirectory(outside);
                File.Copy(Path.Combine(FixtureRoot(), "Assets", "Prefabs", "Broken.prefab"), Path.Combine(outside, "Escaped.prefab"));
                if (Application.platform == RuntimePlatform.WindowsEditor) Assert.Ignore("ln -s is unavailable on Windows.");
                using (var ln = System.Diagnostics.Process.Start("ln", "-s \"" + outside + "\" \"" + Path.Combine(temp, "Assets", "Link") + "\""))
                    ln.WaitForExit();
                Assert.IsTrue(Directory.Exists(Path.Combine(temp, "Assets", "Link", ".")), "symlink was not created");
                JObject data = Run(Q("validate_asset_integrity"), root: temp);
                Assert.AreEqual(0, ((JArray)data["findings"]).Count, data.ToString());
                Assert.AreEqual(0, (int)data["scanned"]["files"]);
            }
            finally
            {
                if (Directory.Exists(temp)) Directory.Delete(temp, true);
                if (Directory.Exists(outside)) Directory.Delete(outside, true);
            }
        }

        [Test]
        public void WrappedFlowMappingsParse()
        {
            string temp = Path.Combine(Path.GetTempPath(), "nexus-asset-impact-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(temp, "Assets"));
                File.WriteAllText(Path.Combine(temp, "Assets", "Wrapped.asset"),
                    "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_Name: Wrapped\n" +
                    "  debugShaders:\n    replacement: {fileID: 4800000, guid: 1111111111111111111111111111aaaa,\n      type: 3}\n    next: 1\n");
                JObject data = Run(Q("validate_asset_integrity"), root: temp);
                Assert.AreEqual(0, ((JArray)data["unknown"]).Count, data.ToString());
                Assert.AreEqual("dangling_guid", (string)data["findings"].Single()["kind"]);
                Assert.AreEqual(7, (int)data["findings"].Single()["evidence"]["line"]);
            }
            finally
            {
                if (Directory.Exists(temp)) Directory.Delete(temp, true);
            }
        }

        [Test]
        public void TraversalOutsideProjectIsInvalidArgument()
        {
            NexusCommandEnvelope envelope = AssetImpactCommand.ExecuteEnveloped(new AssetImpactCommand.Request { Op = "validate_asset_integrity", Paths = "../../" });
            Assert.IsFalse(envelope.Ok);
            Assert.AreEqual(NexusCommandCodes.InvalidArgument, envelope.Error.Code);
        }

        [Test]
        public void UnknownOpIsInvalidArgument()
        {
            NexusCommandEnvelope envelope = AssetImpactCommand.ExecuteEnveloped(new AssetImpactCommand.Request { Op = "delete_everything", Paths = "Assets/NexusUnity/Tests/Fixtures~" });
            Assert.IsFalse(envelope.Ok);
            Assert.AreEqual(NexusCommandCodes.InvalidArgument, envelope.Error.Code);
        }

        private static string Key(JToken e, string list)
        {
            string key = $"{e["asset"]}|{e["file_id"]}|{e["confidence"]}";
            if (list == "values") key += $"|{e["source"]}|{e["object_path"]}|{e["value"]}|{e["differs_from_default"]}";
            if (list == "bindings") key += $"|{e["event"]}|{e["call_index"]}";
            if (list == "via_prefab_instance") key += $"|{e["source_prefab"]}";
            return key;
        }

        private static string Ratio(int a, int b) => b == 0 ? "1.000" : ((double)a / b).ToString("0.000");

        private static string Strip(string json)
        {
            JObject o = JObject.Parse(json);
            o["scanned"]["ms"] = 0;
            return o.ToString();
        }

        private static string TreeHash(string root)
        {
            using (var sha = SHA256.Create())
            {
                foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
                {
                    byte[] name = System.Text.Encoding.UTF8.GetBytes(file);
                    sha.TransformBlock(name, 0, name.Length, null, 0);
                    byte[] body = File.ReadAllBytes(file);
                    sha.TransformBlock(body, 0, body.Length, null, 0);
                }
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return BitConverter.ToString(sha.Hash);
            }
        }
    }
}
