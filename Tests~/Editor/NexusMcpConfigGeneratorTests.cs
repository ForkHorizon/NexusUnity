using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    public class NexusMcpConfigGeneratorTests
    {
        [Test]
        public void BuildsExpectedConfigSnippets()
        {
            string json = NexusMcpConfigGenerator.BuildJsonConfig("mcpServers", "/tmp/nexus_unity_bridge.py", "/usr/bin/python3");
            string vsCode = NexusMcpConfigGenerator.BuildJsonConfig("servers", "/tmp/nexus_unity_bridge.py", "/usr/bin/python3");
            string codex = NexusMcpConfigGenerator.BuildCodexToml("/tmp/nexus_unity_bridge.py", "/usr/bin/python3");

            Assert.IsTrue(json.Contains("\"mcpServers\""));
            Assert.IsTrue(json.Contains("\"nexus-unity\""));
            Assert.IsTrue(vsCode.Contains("\"servers\""));
            Assert.IsTrue(codex.Contains("[mcp_servers.nexus-unity]"));
            Assert.IsTrue(codex.Contains("command = \"/usr/bin/python3\""));
        }

        [Test]
        public void WriteJsonConfigPreservesUnrelatedServersAndCreatesBackup()
        {
            string root = CreateTempRoot();
            try
            {
                string projectRoot = Path.Combine(root, "Project");
                string homeRoot = Path.Combine(root, "Home");
                var cursor = NexusMcpConfigGenerator.BuildAll("/tmp/nexus_unity_bridge.py", "/usr/bin/python3", projectRoot, homeRoot)
                    .First(client => client.Kind == NexusMcpClientKind.Cursor);

                Directory.CreateDirectory(Path.GetDirectoryName(cursor.ConfigPath));
                File.WriteAllText(cursor.ConfigPath, "{ \"mcpServers\": { \"other-server\": { \"command\": \"node\" } } }");

                var result = NexusMcpConfigGenerator.WriteConfig(cursor);
                var json = JObject.Parse(File.ReadAllText(cursor.ConfigPath));

                Assert.IsTrue(result.Success);
                Assert.IsTrue(File.Exists(result.BackupPath));
                Assert.IsNotNull(json["mcpServers"]["other-server"]);
                Assert.AreEqual("/usr/bin/python3", (string)json["mcpServers"]["nexus-unity"]["command"]);
            }
            finally
            {
                DeleteTempRoot(root);
            }
        }

        [Test]
        public void WriteVsCodeConfigUsesServersRoot()
        {
            string root = CreateTempRoot();
            try
            {
                var client = NexusMcpConfigGenerator.BuildAll("/bridge.py", "/python3", Path.Combine(root, "Project"), Path.Combine(root, "Home"))
                    .First(item => item.Kind == NexusMcpClientKind.VsCode);

                var result = NexusMcpConfigGenerator.WriteConfig(client);
                var json = JObject.Parse(File.ReadAllText(client.ConfigPath));

                Assert.IsTrue(result.Success);
                Assert.IsNotNull(json["servers"]["nexus-unity"]);
                Assert.IsNull(json["mcpServers"]);
            }
            finally
            {
                DeleteTempRoot(root);
            }
        }

        [Test]
        public void DetectsConfiguredJsonServer()
        {
            string root = CreateTempRoot();
            try
            {
                string projectRoot = Path.Combine(root, "Project");
                string homeRoot = Path.Combine(root, "Home");
                string sourceBridge = Path.Combine(root, "Package", "nexus_unity_bridge.py");
                string deployedBridge = Path.Combine(projectRoot, "nexus_unity_bridge.py");
                WriteBridge(sourceBridge, "1.2.0");
                WriteBridge(deployedBridge, "1.2.0");

                var client = NexusMcpConfigGenerator.BuildAll(deployedBridge, "/python3", projectRoot, homeRoot, sourceBridge)
                    .First(item => item.Kind == NexusMcpClientKind.Cursor);
                NexusMcpConfigGenerator.WriteConfig(client);

                var configured = NexusMcpConfigGenerator.BuildAll(deployedBridge, "/python3", projectRoot, homeRoot, sourceBridge)
                    .First(item => item.Kind == NexusMcpClientKind.Cursor);

                Assert.AreEqual(NexusMcpClientStatus.Configured, configured.Status);
            }
            finally
            {
                DeleteTempRoot(root);
            }
        }

        [Test]
        public void DetectsOutdatedDeployedBridgeVersion()
        {
            string root = CreateTempRoot();
            try
            {
                string projectRoot = Path.Combine(root, "Project");
                string homeRoot = Path.Combine(root, "Home");
                string sourceBridge = Path.Combine(root, "Package", "nexus_unity_bridge.py");
                string deployedBridge = Path.Combine(projectRoot, "nexus_unity_bridge.py");
                WriteBridge(sourceBridge, "1.2.0");
                WriteBridge(deployedBridge, "1.1.2");

                var client = NexusMcpConfigGenerator.BuildAll(deployedBridge, "/python3", projectRoot, homeRoot, sourceBridge)
                    .First(item => item.Kind == NexusMcpClientKind.Cursor);
                NexusMcpConfigGenerator.WriteConfig(client);

                var configured = NexusMcpConfigGenerator.BuildAll(deployedBridge, "/python3", projectRoot, homeRoot, sourceBridge)
                    .First(item => item.Kind == NexusMcpClientKind.Cursor);

                Assert.AreEqual(NexusMcpClientStatus.Outdated, configured.Status);
                StringAssert.Contains("1.1.2", configured.StatusDetail);
                StringAssert.Contains("1.2.0", configured.StatusDetail);
            }
            finally
            {
                DeleteTempRoot(root);
            }
        }

        [Test]
        public void DetectsCodexConfigPointingAtDifferentProject()
        {
            string root = CreateTempRoot();
            try
            {
                string projectRoot = Path.Combine(root, "Project");
                string otherProject = Path.Combine(root, "OtherProject");
                string homeRoot = Path.Combine(root, "Home");
                string sourceBridge = Path.Combine(root, "Package", "nexus_unity_bridge.py");
                string deployedBridge = Path.Combine(projectRoot, "nexus_unity_bridge.py");
                string wrongBridge = Path.Combine(otherProject, "nexus_unity_bridge.py");
                WriteBridge(sourceBridge, "1.2.0");
                WriteBridge(deployedBridge, "1.2.0");
                WriteBridge(wrongBridge, "1.0.0");

                string configPath = Path.Combine(homeRoot, ".codex", "config.toml");
                Directory.CreateDirectory(Path.GetDirectoryName(configPath));
                File.WriteAllText(configPath, NexusMcpConfigGenerator.BuildCodexToml(wrongBridge, "/python3"));

                var codex = NexusMcpConfigGenerator.BuildAll(deployedBridge, "/python3", projectRoot, homeRoot, sourceBridge)
                    .First(item => item.Kind == NexusMcpClientKind.Codex);

                Assert.AreEqual(NexusMcpClientStatus.Outdated, codex.Status);
                Assert.AreEqual(wrongBridge.Replace("\\", "/"), codex.ConfiguredBridgePath);
                StringAssert.Contains("Current project bridge", codex.StatusDetail);
            }
            finally
            {
                DeleteTempRoot(root);
            }
        }

        private static string CreateTempRoot()
        {
            string root = Path.Combine(Path.GetTempPath(), "NexusMcpConfigGeneratorTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void WriteBridge(string path, string version)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "\"serverInfo\": {\"name\": \"NexusUnity-Bridge\", \"version\": \"" + version + "\"}");
        }

        private static void DeleteTempRoot(string root)
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
