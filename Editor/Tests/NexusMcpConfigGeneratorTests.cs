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
                    .First(item => item.Kind == NexusMcpClientKind.VsCodeClineRoo);

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
                var client = NexusMcpConfigGenerator.BuildAll("/bridge.py", "/python3", projectRoot, homeRoot)
                    .First(item => item.Kind == NexusMcpClientKind.Cursor);
                NexusMcpConfigGenerator.WriteConfig(client);

                var configured = NexusMcpConfigGenerator.BuildAll("/bridge.py", "/python3", projectRoot, homeRoot)
                    .First(item => item.Kind == NexusMcpClientKind.Cursor);

                Assert.AreEqual(NexusMcpClientStatus.Configured, configured.Status);
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

        private static void DeleteTempRoot(string root)
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
