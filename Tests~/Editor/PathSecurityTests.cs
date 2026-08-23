using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor.Tests
{
    /// <summary>
    /// Tests for path security and validation to prevent path traversal.
    /// </summary>
    public class PathSecurityTests
    {
        [SetUp]
        public void InitRegistry()
        {
            MCPServerMethods.Init();
        }

        /// <summary>
        /// Verifies that ValidatePath prevents access to sibling directories with similar prefixes.
        /// </summary>
        [Test]
        public void ValidatePath_PreventsSiblingDirectoryAccess()
        {
            // Setup paths based on current project environment
            string projectRoot = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');
            string projectFolderName = Path.GetFileName(projectRoot);
            string parentDir = Path.GetDirectoryName(projectRoot).Replace('\\', '/');

            // Construct a path that is a sibling to the project root with a similar prefix
            // e.g. if project is "MyProject", try "MyProjectSecrets"
            string maliciousFolderName = projectFolderName + "Secrets";
            string maliciousPath = Path.Combine(parentDir, maliciousFolderName, "file.txt").Replace('\\', '/');

            // Verify that accessing this sibling path throws an exception
            var ex = Assert.Throws<Exception>(() => MCPServerMethods.ValidatePath(maliciousPath));
            Assert.That(ex.Message, Does.Contain("Access denied"));
        }

        /// <summary>
        /// Verifies that ValidatePath allows access to valid subpaths within the project.
        /// </summary>
        [Test]
        public void ValidatePath_AllowsValidAssetPath()
        {
            string validPath = Path.Combine(Application.dataPath, "TestAsset.txt").Replace('\\', '/');
            Assert.DoesNotThrow(() => MCPServerMethods.ValidatePath(validPath));
        }

        /// <summary>
        /// Verifies that ValidatePath allows the project root path itself.
        /// </summary>
        [Test]
        public void ValidatePath_AllowsProjectRoot()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');
            Assert.DoesNotThrow(() => MCPServerMethods.ValidatePath(projectRoot));
        }

        /// <summary>
        /// Verifies that ValidatePath prevents access to the project's parent directory.
        /// </summary>
        [Test]
        public void ValidatePath_PreventsParentDirectoryAccess()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');
            string parentPath = Path.GetDirectoryName(projectRoot); // One level up

            var ex = Assert.Throws<Exception>(() => MCPServerMethods.ValidatePath(parentPath));
            Assert.That(ex.Message, Does.Contain("Access denied"));
        }

        [Test]
        public void ReadFile_PreventsTraversalThroughJsonRpc()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');
            string outsidePath = Path.Combine(projectRoot, "..", "nexus-traversal.txt").Replace('\\', '/');

            JObject response = Rpc("read_file", new JObject { ["path"] = outsidePath });

            AssertRpcErrorContains(response, "Access denied");
        }

        [Test]
        public void ExploreAsset_PreventsTraversalThroughJsonRpc()
        {
            JObject response = Rpc("explore_asset", new JObject { ["path"] = "Assets/../../nexus-traversal.asset" });

            AssertRpcErrorContains(response, "Access denied");
        }

        [Test]
        public void WriteFile_CSharpRequiresConfirm()
        {
            string path = "Assets/NexusUnityGeneratedTests/BlockedScript.cs";
            DeleteGeneratedRoot();

            try
            {
                JObject response = Rpc("write_file", new JObject { ["path"] = path, ["content"] = "class BlockedScript {}" });

                AssertRpcErrorContains(response, "confirm: true");
                Assert.IsFalse(File.Exists(MCPServerMethods.ValidatePath(path)));
            }
            finally
            {
                DeleteGeneratedRoot();
            }
        }

        [Test]
        public void WriteFilesBatch_CSharpRequiresConfirmBeforeAnyWrite()
        {
            string textPath = "Assets/NexusUnityGeneratedTests/Allowed.txt";
            string scriptPath = "Assets/NexusUnityGeneratedTests/BlockedBatchScript.cs";
            DeleteGeneratedRoot();

            try
            {
                JObject response = Rpc("write_files_batch", new JObject
                {
                    ["files"] = new JArray
                    {
                        new JObject { ["path"] = textPath, ["content"] = "should not be written first" },
                        new JObject { ["path"] = scriptPath, ["content"] = "class BlockedBatchScript {}" }
                    }
                });

                AssertRpcErrorContains(response, "confirm: true");
                Assert.IsFalse(File.Exists(MCPServerMethods.ValidatePath(textPath)));
                Assert.IsFalse(File.Exists(MCPServerMethods.ValidatePath(scriptPath)));
            }
            finally
            {
                DeleteGeneratedRoot();
            }
        }

        [Test]
        public void ValidatePath_PreventsSymlinkTraversalOutsideProject()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');
            string outsideDir = Path.Combine(Path.GetDirectoryName(projectRoot), "nexus-test-outside-" + Guid.NewGuid().ToString("N")).Replace('\\', '/');
            Directory.CreateDirectory(outsideDir);

            string symlinkPath = Path.Combine(Application.dataPath, "OutsideSymlinkTemp").Replace('\\', '/');

            try
            {
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    var processInfo = new System.Diagnostics.ProcessStartInfo("cmd", $"/c mklink /d \"{symlinkPath.Replace('/', '\\')}\" \"{outsideDir.Replace('/', '\\')}\"")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    System.Diagnostics.Process.Start(processInfo).WaitForExit();
                }
                else
                {
                    System.Diagnostics.Process.Start("ln", $"-s \"{outsideDir}\" \"{symlinkPath}\"").WaitForExit();
                }

                if (!Directory.Exists(symlinkPath))
                {
                    Assert.Ignore("Symlink could not be created (possible permission/platform limitation).");
                }

                string maliciousPath = Path.Combine(symlinkPath, "file.txt").Replace('\\', '/');

                var ex = Assert.Throws<Exception>(() => MCPServerMethods.ValidatePath(maliciousPath));
                Assert.That(ex.Message, Does.Contain("Access denied"));
            }
            finally
            {
                if (File.Exists(symlinkPath) || Directory.Exists(symlinkPath))
                {
                    if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                    {
                        Directory.Delete(symlinkPath);
                    }
                    else
                    {
                        File.Delete(symlinkPath);
                    }
                }
                if (Directory.Exists(outsideDir))
                {
                    Directory.Delete(outsideDir);
                }
            }
        }

        [Test]
        public void AttachScript_RequiresConfirm()
        {
            string path = "Assets/BlockedAttachScript.cs";
            string fullPath = MCPServerMethods.ValidatePath(path);
            if (File.Exists(fullPath)) File.Delete(fullPath);

            try
            {
                JObject response = Rpc("attach_script", new JObject { ["script_name"] = "BlockedAttachScript" });

                AssertRpcErrorContains(response, "confirm: true");
                Assert.IsFalse(File.Exists(fullPath));
            }
            finally
            {
                if (File.Exists(fullPath)) File.Delete(fullPath);
            }
        }

        private static JObject Rpc(string method, JObject parameters)
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = method,
                ["params"] = parameters,
                ["id"] = 1
            };

            return JObject.Parse(MCPServerMethods.ProcessJsonRpc(request.ToString(Formatting.None)));
        }

        private static void AssertRpcErrorContains(JObject response, string expectedMessage)
        {
            Assert.IsNull(response["result"], response.ToString(Formatting.None));
            Assert.IsNotNull(response["error"], response.ToString(Formatting.None));
            Assert.That(response["error"]?["message"]?.ToString(), Does.Contain(expectedMessage));
        }

        private static void DeleteGeneratedRoot()
        {
            string fullPath = MCPServerMethods.ValidatePath("Assets/NexusUnityGeneratedTests");
            if (Directory.Exists(fullPath)) Directory.Delete(fullPath, true);
        }
    }
}
