// EditMode test: rename advice for a field already renamed with [FormerlySerializedAs] names the key stored in the asset (#0014).
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityMCP.Editor.Commands;

namespace UnityMCP.Editor.Tests
{
    public class AssetImpactAliasAdviceTests
    {
        [Test]
        public void RenameAdviceNamesTheStoredKey()
        {
            string root = Path.Combine(Path.GetTempPath(), "nexus-impact-alias-" + System.Guid.NewGuid().ToString("N"));
            const string guid = "fedcba9876543210fedcba9876543210";
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "Assets"));
                File.WriteAllText(Path.Combine(root, "Assets", "Health.cs"),
                    "using UnityEngine;\nusing UnityEngine.Serialization;\npublic class Health : MonoBehaviour\n{\n    [FormerlySerializedAs(\"speed\")] public float moveSpeed = 3.5f;\n}\n");
                File.WriteAllText(Path.Combine(root, "Assets", "Health.cs.meta"), "fileFormatVersion: 2\nguid: " + guid + "\n");
                File.WriteAllText(Path.Combine(root, "Assets", "U.asset"),
                    "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_Name: U\n" +
                    "  m_Script: {fileID: 11500000, guid: " + guid + ", type: 3}\n  speed: 6\n");
                JObject data = AssetImpactCommand.Run(new AssetImpactCommand.Request { Op = "analyze_change_impact", Change = "rename_field", Type = "Health", Name = "moveSpeed" },
                    root, new[] { "Assets" }, null);
                JToken finding = data["findings"].Single();
                Assert.AreEqual("speed", (string)finding["serialized_as"]);
                StringAssert.Contains("FormerlySerializedAs(\"speed\")", (string)finding["recommended_check"]);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
