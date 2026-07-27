using System.IO;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class AssetDeleteSecurityTests
    {
        private static JObject CallRaw(string method, JObject paramsObj)
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = method,
                ["params"] = paramsObj,
                ["id"] = 1
            };

            string responseJson = MCPServerMethods.ProcessJsonRpc(request.ToString(Newtonsoft.Json.Formatting.None));
            return JObject.Parse(responseJson);
        }

        private static void CleanupGeneratedAssetRoot(string root)
        {
            AssetDatabase.DeleteAsset(root);

            string absoluteRoot = MCPServerMethods.ValidatePath(root);
            if (Directory.Exists(absoluteRoot)) Directory.Delete(absoluteRoot, true);

            string meta = absoluteRoot + ".meta";
            if (File.Exists(meta)) File.Delete(meta);

            AssetDatabase.Refresh();
        }

        [Test]
        public void DeleteAsset_WithoutConfirm_ReturnsError()
        {
            string root = "Assets/NexusUnityGeneratedTests";
            string path = $"{root}/DeleteNoConfirm.txt";
            CleanupGeneratedAssetRoot(root);

            try
            {
                CallRaw("write_file", new JObject { ["path"] = path, ["content"] = "test content" });
                var res = CallRaw("delete_asset", new JObject { ["path"] = path });
                Assert.IsNotNull(res["error"], "delete_asset without confirm should return error");
                Assert.IsTrue(res["error"]["message"].ToString().Contains("confirm: true"), "Error message should mention confirm: true requirement");
            }
            finally
            {
                CleanupGeneratedAssetRoot(root);
            }
        }

        [Test]
        public void DeleteAsset_ProjectSettings_ReturnsError()
        {
            var res = CallRaw("delete_asset", new JObject { ["path"] = "ProjectSettings/ProjectSettings.asset", ["confirm"] = true });
            Assert.IsNotNull(res["error"], "delete_asset on ProjectSettings should return error");
            Assert.IsTrue(res["error"]["message"].ToString().Contains("forbidden"), "Error message should state ProjectSettings deletion is forbidden");
        }

        [Test]
        public void DeleteAsset_AssetsRootFolder_ReturnsError()
        {
            var res = CallRaw("delete_asset", new JObject { ["path"] = "Assets", ["confirm"] = true });
            Assert.IsNotNull(res["error"], "delete_asset on Assets root folder should return error");
            Assert.IsTrue(res["error"]["message"].ToString().Contains("forbidden"), "Error message should state root folder deletion is forbidden");

            var res2 = CallRaw("delete_asset", new JObject { ["path"] = "Assets/", ["confirm"] = true });
            Assert.IsNotNull(res2["error"], "delete_asset on Assets/ root folder should return error");
            Assert.IsTrue(res2["error"]["message"].ToString().Contains("forbidden"), "Error message should state root folder deletion is forbidden");
        }

        [Test]
        public void DeleteAsset_MetaFileDirectly_ReturnsError()
        {
            var res = CallRaw("delete_asset", new JObject { ["path"] = "Assets/Test.cs.meta", ["confirm"] = true });
            Assert.IsNotNull(res["error"], "delete_asset on .meta file directly should return error");
            Assert.IsTrue(res["error"]["message"].ToString().Contains("Cannot delete .meta files directly"), "Error message should state .meta deletion is blocked");
        }

        [Test]
        public void DeleteAsset_NonExistentFile_ReturnsError()
        {
            var res = CallRaw("delete_asset", new JObject { ["path"] = "Assets/NexusNonExistentAsset_12345.png", ["confirm"] = true });
            Assert.IsNotNull(res["error"], "delete_asset on non-existent file should return error");
            Assert.IsTrue(res["error"]["message"].ToString().Contains("Asset not found"), "Error message should state asset not found");
        }

        [Test]
        public void DeleteAsset_WithConfirm_DeletesSuccessfully()
        {
            string root = "Assets/NexusUnityGeneratedTests";
            string path = $"{root}/DeleteWithConfirm.txt";
            CleanupGeneratedAssetRoot(root);

            try
            {
                CallRaw("write_file", new JObject { ["path"] = path, ["content"] = "test content" });
                Assert.IsTrue(File.Exists(MCPServerMethods.ValidatePath(path)), "Test file should exist before delete_asset");

                var res = CallRaw("delete_asset", new JObject { ["path"] = path, ["confirm"] = true });
                Assert.IsNotNull(res["result"], $"Expected success result, got error: {res["error"]}");
                Assert.IsFalse(File.Exists(MCPServerMethods.ValidatePath(path)), "Test file should no longer exist after delete_asset");
            }
            finally
            {
                CleanupGeneratedAssetRoot(root);
            }
        }

        [Test]
        public void MoveAsset_ToProjectSettings_ReturnsError()
        {
            var res = CallRaw("move_asset", new JObject { ["old_path"] = "Assets/Test.txt", ["new_path"] = "ProjectSettings/ProjectSettings.asset" });
            Assert.IsNotNull(res["error"], "move_asset to ProjectSettings should return error");
            Assert.IsTrue(res["error"]["message"].ToString().Contains("forbidden"), "Error message should state modifying ProjectSettings is forbidden");
        }

        [Test]
        public void CopyAsset_ToProjectSettings_ReturnsError()
        {
            var res = CallRaw("copy_asset", new JObject { ["source_path"] = "Assets/Test.txt", ["dest_path"] = "ProjectSettings/ProjectSettings.asset" });
            Assert.IsNotNull(res["error"], "copy_asset to ProjectSettings should return error");
            Assert.IsTrue(res["error"]["message"].ToString().Contains("forbidden"), "Error message should state modifying ProjectSettings is forbidden");
        }
    }
}
