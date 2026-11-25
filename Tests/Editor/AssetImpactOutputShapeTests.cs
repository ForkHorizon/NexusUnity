// EditMode tests for nexus.asset_impact output shape (results=auto|summary|full) and [FormerlySerializedAs] aliases (V3-13 findings).
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityMCP.Editor.Commands;

namespace UnityMCP.Editor.Tests
{
    public class AssetImpactOutputShapeTests
    {
        private const string Guid = "0123456789abcdef0123456789abcdef";
        private const string Script =
            "using UnityEngine;\nusing UnityEngine.Serialization;\nnamespace Game\n{\n    public class Health : MonoBehaviour\n    {\n" +
            "        [FormerlySerializedAs(\"maxHealth\")] public int maxHp = 100;\n" +
            "        [SerializeField]\n        [FormerlySerializedAs(\"speed\")]\n        private float moveSpeed = 3.5f;\n    }\n}\n";

        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "nexus-impact-shape-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_root, "Assets"));
            File.WriteAllText(Path.Combine(_root, "Assets", "Health.cs"), Script);
            File.WriteAllText(Path.Combine(_root, "Assets", "Health.cs.meta"), "fileFormatVersion: 2\nguid: " + Guid + "\n");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private void Assets(int count)
        {
            for (int i = 0; i < count; i++)
                File.WriteAllText(Path.Combine(_root, "Assets", "U" + i + ".asset"),
                    "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_Name: U" + i + "\n" +
                    "  m_Script: {fileID: 11500000, guid: " + Guid + ", type: 3}\n  maxHealth: " + (200 + i) + "\n  speed: 6\n");
        }

        private JObject Run(string name, string results = "auto") =>
            AssetImpactCommand.Run(new AssetImpactCommand.Request { Op = "find_references", Kind = "field", Type = "Game.Health", Name = name, Results = results },
                _root, new[] { "Assets" }, null);

        [Test]
        public void FormerlySerializedAsValuesAreFoundUnderTheNewName()
        {
            Assets(1);
            JObject single = Run("maxHp");
            JToken v = single["values"].Single();
            Assert.AreEqual("200", (string)v["value"]);
            Assert.AreEqual("maxHealth", (string)v["serialized_as"]);
            Assert.AreEqual("100", (string)single["csharp_default"]);
            JObject multiLine = Run("moveSpeed");
            Assert.AreEqual("6", (string)multiLine["values"].Single()["value"], "attribute on its own line");
            Assert.AreEqual("speed", (string)multiLine["values"].Single()["serialized_as"]);
            Assert.AreEqual("3.5", (string)multiLine["csharp_default"]);
        }

        [Test]
        public void SmallResultsStayFull()
        {
            Assets(3);
            JObject data = Run("maxHp");
            Assert.AreEqual("full", (string)data["results"]);
            Assert.AreEqual(3, ((JArray)data["values"]).Count);
            Assert.IsNull(data["values_by_asset"]);
            JObject forced = Run("maxHp", "summary");
            Assert.AreEqual("summary", (string)forced["results"]);
            Assert.AreEqual(3, ((JArray)forced["values_by_asset"]).Count);
        }

        [Test]
        public void LargeResultsAreSummarizedByAssetUnlessFullIsAsked()
        {
            Assets(AssetImpactResults.AutoFullLimit + 10);
            JObject summary = Run("maxHp");
            Assert.AreEqual("summary", (string)summary["results"]);
            Assert.AreEqual(AssetImpactResults.SampleSize, ((JArray)summary["values"]).Count);
            JArray byAsset = (JArray)summary["values_by_asset"];
            Assert.AreEqual(AssetImpactResults.AutoFullLimit + 10, byAsset.Count);
            Assert.IsTrue(byAsset.All(r => (int)r["count"] == 1 && r["lines"].Count() == 1 && r["values"].Count() == 1), byAsset.ToString());
            Assert.Less(summary.ToString().Length, Run("maxHp", "full").ToString().Length);
            Assert.AreEqual(AssetImpactResults.AutoFullLimit + 10, ((JArray)Run("maxHp", "full")["values"]).Count);
        }

        [Test]
        public void UnknownResultsModeIsRejected()
        {
            Assets(1);
            Assert.Throws<ArgumentException>(() => Run("maxHp", "everything"));
        }
    }
}
